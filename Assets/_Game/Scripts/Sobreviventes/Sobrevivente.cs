using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O papel de um sobrevivente: os dos grupos de busca, os do acampamento (02/11/2026, a etapa 3) e os dois do carro
    /// quebrado na estrada (a etapa 4: o mecânico, ajoelhado na roda, e o vigia, de rifle) e o militar da base
    /// (03/11/2026, <see cref="BasesMilitaresDoMundo"/>).
    /// </summary>
    public enum PapelDoSobrevivente : byte { Lider, Busca, Guarda, Comerciante, Morador, Mecanico, Vigia, Militar }

    /// <summary>O que vai pela rede de cada sobrevivente: a semente (as roupas), a arma, o tipo de roupa, o grupo e o papel.</summary>
    public struct IdentidadeDoSobrevivente : INetworkSerializeByMemcpy, IEquatable<IdentidadeDoSobrevivente>
    {
        public int semente;
        public TipoDeArma arma;
        /// <summary>0 sobrevivente, 1 militar, 2 policial, 3 civil (o tipo do sorteio das roupas).</summary>
        public byte roupa;
        public ushort grupo;
        public PapelDoSobrevivente papel;
        public byte indice;
        /// <summary>A arma branca na mão (a faca, o machado, o bastão, a lança; Nada: a arma de fogo). 03/11/2026, os civis.</summary>
        public Item branca;

        public bool Equals(IdentidadeDoSobrevivente o) => semente == o.semente && arma == o.arma && roupa == o.roupa && grupo == o.grupo && papel == o.papel && indice == o.indice && branca == o.branca;
        public static readonly string[] Roupas = { "Sobrevivente", "Militar", "Policial", "Civil" };
    }

    /// <summary>
    /// Um sobrevivente (02/11/2026, a pedido: os grupos armados e hostis que a gente encontra explorando as cidades; vida 100,
    /// um tiro na cabeça mata, com qualquer arma). É o corpo do jogador (o "Corpo", cópia do do <c>Jogador.prefab</c>: as
    /// roupas, as armas na mão e o controlador de cada arma), com um NavMeshAgent, a lanterna no peito e as partes do
    /// corpo que levam tiro (<see cref="ParteDoSobrevivente"/>, na camada 12). Um NetworkObject por sobrevivente; o
    /// anfitrião pensa por ele (Sobrevivente.Cabeca.cs) e move (o NetworkTransform leva aos outros). Em todas as máquinas:
    /// as roupas e a arma (pela <see cref="Identidade"/>), a animação (a velocidade medida, o mirar, o tiro), a lanterna,
    /// o clarão, o som e o impacto dos tiros (<see cref="TiroRpc"/>), as falas (<see cref="FalaRpc"/>) e a queda (morto).
    /// Para os zumbis, ele é um alvo como o jogador (<see cref="IOutroAlvoDeZumbi"/>).
    /// </summary>
    public partial class Sobrevivente : NetworkBehaviour, IOutroAlvoDeZumbi
    {
        public const int Camada = 12;
        public const float VidaMaxima = 100f;

        [Tooltip("O corpo (a cópia do corpo do jogador, com o CorpoDoPersonagem).")]
        public CorpoDoPersonagem corpo;
        public NavMeshAgent agente;
        [Tooltip("A lanterna no peito (desligada; liga à noite, explorando).")]
        public Light lanterna;
        [Tooltip("O clipe \"Agachado\" do corpo: os dois do carro quebrado ajoelham na roda com ele (Sobrevivente.Estrada.cs).")]
        public AnimationClip agachado;

        /// <summary>Os sobreviventes nesta máquina.</summary>
        public static readonly List<Sobrevivente> Todos = new List<Sobrevivente>();

        public readonly NetworkVariable<IdentidadeDoSobrevivente> Identidade = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _mirando = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _luz = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O empurrão do tiro (ou do golpe) que matou: a queda (Sobrevivente.Queda.cs). Antes do morto, de propósito.</summary>
        readonly NetworkVariable<Vector3> _empurrao = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _morto = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Ajoelhado na roda do carro quebrado (Sobrevivente.Estrada.cs): sem a arma na mão.</summary>
        readonly NetworkVariable<bool> _ajoelhado = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool Morto => IsSpawned && _morto.Value;
        public bool Mirando => IsSpawned && _mirando.Value;
        public TipoDeArma Arma => Identidade.Value.arma;
        /// <summary>O que ele tem na mão: a arma branca, ou a de fogo.</summary>
        public Item ItemDaArma => ComBranca ? Identidade.Value.branca : Itens.DaArma(Arma);
        public DadosDaArma DadosDaArma => Armas.Dados(Arma);
        /// <summary>De onde ele olha (a cabeça, 1,6 m).</summary>
        public Vector3 Olho => transform.position + Vector3.up * 1.6f;

        // ------------------------------------------------------------------ o alvo dos zumbis
        public bool AlvoValido => this != null && IsSpawned && !_morto.Value;
        public bool LuzLigada => IsSpawned && _luz.Value;
        public float VelocidadeNoChao => _velocidade;
        public void LevarGolpe(float dano, Vector3 de) => Ferir(dano, de, null, false);

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AimingId = Animator.StringToHash("Aiming");
        static readonly int FireId = Animator.StringToHash("Fire");

        float _velocidade, _medida, _janela;
        Vector3 _posJanela;
        IdentidadeDoSobrevivente _vestido;
        bool _vestidoUmaVez, _caido;
        ParteDoSobrevivente[] _partes;

        void Awake()
        {
            _partes = GetComponentsInChildren<ParteDoSobrevivente>(true);
            foreach (var p in _partes) p.sobrevivente = this;
        }

        public override void OnNetworkSpawn()
        {
            Todos.Add(this);
            Identidade.OnValueChanged += AoMudarIdentidade;
            _morto.OnValueChanged += AoMudarMorto;
            _luz.OnValueChanged += AoMudarLuz;
            _ajoelhado.OnValueChanged += AoMudarAjoelhado;
            // o agente só no anfitrião, e só com NavMesh embaixo (o do acampamento pode nascer antes da área ficar pronta)
            if (agente != null) agente.enabled = false;
            if (IsServer) LigarOAgente();
            _posJanela = transform.position;
            Vestir();
            // o comerciante do acampamento: E abre o comércio
            if (Identidade.Value.papel == PapelDoSobrevivente.Comerciante && GetComponent<ComercianteNaMira>() == null)
                gameObject.AddComponent<ComercianteNaMira>().sobrevivente = this;
            // os dois do carro quebrado: E fala com eles (e dá o pneu)
            if (DaEstrada && GetComponent<CarroQuebradoNaMira>() == null)
                gameObject.AddComponent<CarroQuebradoNaMira>().sobrevivente = this;
            AoMudarLuz(false, _luz.Value);
            if (_morto.Value) Cair(true);
            if (IsServer) AoNascerNoServidor();
        }

        public override void OnNetworkDespawn()
        {
            Todos.Remove(this);
            Identidade.OnValueChanged -= AoMudarIdentidade;
            _morto.OnValueChanged -= AoMudarMorto;
            _luz.OnValueChanged -= AoMudarLuz;
            _ajoelhado.OnValueChanged -= AoMudarAjoelhado;
            ZombieManager.OutrosAlvos.Remove(this);
            if (IsServer) AoSumirNoServidor();
        }

        void AoMudarIdentidade(IdentidadeDoSobrevivente antes, IdentidadeDoSobrevivente depois) => Vestir();

        void AoMudarMorto(bool antes, bool depois)
        {
            if (depois) Cair(false);
        }

        void AoMudarLuz(bool antes, bool depois)
        {
            if (lanterna != null) lanterna.enabled = depois && !_morto.Value;
        }

        // as roupas (o sorteio do tipo, pela semente) e a arma na mão, com o controlador dela
        void Vestir()
        {
            var id = Identidade.Value;
            if (corpo == null || (_vestidoUmaVez && id.Equals(_vestido))) return;
            _vestidoUmaVez = true;
            _vestido = id;
            var v = corpo.vestimenta;
            if (v != null)
            {
                var roupas = v.transform.Find("Roupas");
                if (roupas != null) roupas.gameObject.SetActive(true);
                v.VestirSorteado(IdentidadeDoSobrevivente.Roupas[Mathf.Min(id.roupa, (byte)3)], id.semente);
                if (id.papel == PapelDoSobrevivente.Militar) VestirABlindagem(v);
            }
            MostrarArma();
            if (corpo.controladores != null && (int)id.arma < corpo.controladores.Length && corpo.animador != null && !_caido)
            {
                var daArma = Itens.EhBranca(id.branca) ? ControladorDoItem(id.branca) : corpo.controladores[(int)id.arma];
                corpo.animador.runtimeAnimatorController = DaEstrada ? ControladorAjoelhado(daArma) : daArma;
            }
        }

        /// <summary>O que o militar veste por cima do sorteio (03/11/2026): o colete tático e o capacete (é o que segura o tiro).</summary>
        static readonly string[] Blindagem = { "Colete_Tatico", "Capacete_Militar" };

        // o colete e o capacete no lugar das peças do mesmo lugar do corpo (o colete, a cabeça), com as cores do catálogo
        static void VestirABlindagem(Roupas.Vestimenta v)
        {
            var catalogo = v.Catalogo;
            if (catalogo == null) return;
            var lista = new List<Roupas.CatalogoDasRoupas.PecaVestida>(v.Vestidas);
            foreach (var nome in Blindagem)
            {
                var dados = catalogo.DaPeca(nome);
                if (dados == null) continue;
                lista.RemoveAll(p => catalogo.DaPeca(p.nome)?.lugar == dados.lugar);
                var cores = dados.cores != null && dados.cores.Length > 0 ? (string[])dados.cores.Clone() : new[] { "#4B5320", "#3A3F1F", "#2B2B2B" };
                lista.Add(new Roupas.CatalogoDasRoupas.PecaVestida { nome = nome, cores = cores });
            }
            v.Vestir(lista);
        }

        // a arma na mão (a da identidade): some com ele morto ou ajoelhado
        void MostrarArma()
        {
            if (corpo == null || corpo.naMao == null) return;
            int m = Itens.MaoDe(ItemDaArma);
            for (int i = 0; i < corpo.naMao.Length; i++)
                if (corpo.naMao[i] != null) corpo.naMao[i].SetActive(i == m && !_morto.Value && !_ajoelhado.Value);
        }

        // ------------------------------------------------------------------ em todas as máquinas: a animação

        void Update()
        {
            if (!IsSpawned || corpo == null || corpo.animador == null) return;
            float medida;
            if (IsServer && agente != null && agente.enabled) medida = new Vector3(agente.velocity.x, 0f, agente.velocity.z).magnitude;
            else
            {
                // os outros medem a posição recebida (como o corpo dos outros jogadores)
                _janela += Time.deltaTime;
                if (_janela >= 0.1f)
                {
                    var d = transform.position - _posJanela;
                    d.y = 0f;
                    _medida = Mathf.Min(d.magnitude / _janela, 6f);
                    _posJanela = transform.position;
                    _janela = 0f;
                }
                medida = _medida;
            }
            _velocidade = Mathf.Lerp(_velocidade, medida, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (_caido)
            {
                Assentar();
                return;
            }
            corpo.animador.SetFloat(SpeedId, _velocidade);
            corpo.animador.SetBool(AimingId, _mirando.Value);
            Ajoelhar();
            if (!IsServer) return;
            if (agente != null && !agente.enabled && Time.time >= _proximoLigar)
            {
                _proximoLigar = Time.time + 0.5f;
                LigarOAgente();
            }
            Pensar();
        }

        float _proximoLigar;

        // no anfitrião: o agente liga quando há NavMesh embaixo (até 2 m)
        void LigarOAgente()
        {
            if (agente == null || agente.enabled || _morto.Value) return;
            // (o chão primeiro: perto, na altura dos pés; senão, o mais perto, sem subir mais de meio metro, como no tampo
            // de um balcão)
            if (!NavMesh.SamplePosition(transform.position, out var hit, 0.6f, NavMesh.AllAreas)
                && (!NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas) || hit.position.y > transform.position.y + 0.5f)) return;
            agente.enabled = true;
            agente.Warp(hit.position);
        }

        /// <summary>
        /// O comércio deste sobrevivente (em qualquer máquina, pela identidade): o acampamento, a banca e a categoria;
        /// falso se ele não é um comerciante.
        /// </summary>
        public bool Comercio(out int acampamento, out int banca, out CategoriaDoComercio categoria)
        {
            var id = Identidade.Value;
            acampamento = id.grupo - AcampamentosDoMundo.GrupoBase;
            banca = -1;
            categoria = default;
            if (id.papel != PapelDoSobrevivente.Comerciante || acampamento < 0) return false;
            var p = AcampamentosDoMundo.Instancia != null ? AcampamentosDoMundo.Instancia.Planta(acampamento) : null;
            if (p == null) return false;
            banca = id.indice - p.PrimeiroComerciante;
            if (banca < 0 || banca >= p.Bancas.Count) return false;
            categoria = p.Bancas[banca].categoria;
            return true;
        }

        // a queda: sem a arma na mão (ela vai na mochila) e sem a lanterna; o corpo desaba com física (Sobrevivente.Queda.cs)
        void Cair(bool ja)
        {
            if (_caido) return;
            _caido = true;
            if (lanterna != null) lanterna.enabled = false;
            if (corpo != null && corpo.naMao != null) foreach (var a in corpo.naMao) if (a != null) a.SetActive(false);
            var capsula = GetComponent<CapsuleCollider>();
            if (capsula != null) capsula.enabled = false;
            Desmoronar(ja);
        }

        // ------------------------------------------------------------------ os tiros (o anfitrião manda; todos mostram)

        /// <summary>Anfitrião → todos: o tiro (o clarão, o som, os impactos e a animação).</summary>
        [Rpc(SendTo.Everyone)]
        void TiroRpc(byte qual, JogadorRede.Impacto[] impactos)
        {
            var arma = (TipoDeArma)Mathf.Min(qual, Armas.Quantas - 1);
            if (corpo != null && corpo.animador != null && !_caido) corpo.animador.SetTrigger(FireId);
            var boca = corpo != null && corpo.bocas != null && (int)arma < corpo.bocas.Length ? corpo.bocas[(int)arma] : transform;
            if (boca == null) boca = transform;
            Efeitos.Clarao(boca, 0);
            Efeitos.Som(Efeitos.Tiros[(int)arma], boca.position, 1f, true, 150f);
            if (arma == TipoDeArma.Espingarda) Efeitos.Som(Efeitos.Bomba, boca.position, 0.7f, true, 25f);
            foreach (var i in impactos)
            {
                if (i.tipo == JogadorRede.TipoDeAcerto.Carne) Sangue.Respingo(i.ponto, i.normal);
                else if (i.tipo != JogadorRede.TipoDeAcerto.Nada) Efeitos.Impacto(i.ponto, i.normal);
                if (i.tipo == JogadorRede.TipoDeAcerto.Cenario) Sangue.FuroDeTiro(i.ponto, i.normal);
            }
        }

        /// <summary>Anfitrião → todos: uma fala (o texto na tela, letra a letra, com o som das letras).</summary>
        [Rpc(SendTo.Everyone)]
        void FalaRpc(byte frase) => FalasDosSobreviventes.Mostrar(this, frase);

        /// <summary>Anfitrião → todos: o sangue de um tiro de jogador nele (quem atirou já viu o dele, na hora).</summary>
        [Rpc(SendTo.Everyone)]
        void SangueRpc(Vector3 ponto, Vector3 normal, ulong quemAtirou)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == quemAtirou) return;
            Sangue.Respingo(ponto, normal);
        }

        // ------------------------------------------------------------------ o dano (no anfitrião)

        /// <summary>
        /// No anfitrião: um tiro (ou um golpe) de um jogador numa parte do corpo. O tiro na cabeça mata na hora (a pedido:
        /// com qualquer arma); o golpe na cabeça vale 2,5 vezes; nos braços e nas pernas, metade do dano. O militar
        /// (03/11/2026) tem o capacete, e o tiro na cabeça vale 2,5 vezes, sem matar de uma vez; e o colete, que corta pela
        /// metade o dano no tronco.
        /// </summary>
        public void LevarTiro(RegiaoDoSobrevivente regiao, float dano, Vector3 ponto, JogadorRede autor, bool tiro = true)
        {
            if (!IsServer || _morto.Value) return;
            SangueRpc(ponto, autor != null ? (autor.transform.position + Vector3.up * 1.5f - ponto).normalized : Vector3.up,
                      autor != null ? autor.OwnerClientId : ulong.MaxValue);
            bool blindado = Identidade.Value.papel == PapelDoSobrevivente.Militar;
            float valor = regiao == RegiaoDoSobrevivente.Cabeca ? (tiro && !blindado ? VidaMaxima * 10f : dano * 2.5f)
                        : regiao == RegiaoDoSobrevivente.Membro ? dano * 0.5f : blindado ? dano * 0.5f : dano;
            Ferir(valor, ponto, autor, regiao == RegiaoDoSobrevivente.Cabeca);
        }
    }

    /// <summary>A parte do corpo de um sobrevivente que levou o tiro.</summary>
    public enum RegiaoDoSobrevivente : byte { Tronco, Cabeca, Membro }
}
