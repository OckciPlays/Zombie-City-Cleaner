using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O que está acontecendo no carro quebrado: esperando um pneu, trocando, ajudados (amigos), hostis ou os dois mortos.
    /// </summary>
    public enum EstadoDoCarroQuebrado : byte { Esperando, Trocando, Ajudados, Hostis, Mortos }

    /// <summary>A cena de um carro quebrado na rede: o estado, quem morreu (bit 0 o mecânico, bit 1 o vigia), o pneu dado e o que falta da troca (s).</summary>
    public struct CenaDoCarroQuebrado : INetworkSerializeByMemcpy, IEquatable<CenaDoCarroQuebrado>
    {
        public EstadoDoCarroQuebrado estado;
        public byte mortos, pneu, falta;

        public bool Equals(CenaDoCarroQuebrado o) => estado == o.estado && mortos == o.mortos && pneu == o.pneu && falta == o.falta;
    }

    /// <summary>
    /// O carro quebrado na estrada (02/11/2026, a etapa 4 do plano, a pedido: "um carro quebrado na estrada com dois
    /// sobreviventes trocando o pneu; o estepe deles furou: a gente dá um pneu e protege dos zumbis enquanto trocam; depois
    /// pagam e ficam ali. Ou a gente mata e saqueia"), no objeto "Mundo":
    ///  - <b>o carro</b> é um carro do acostamento marcado pelo gerador (<see cref="ObjetosDaEstrada.Quebrados"/>): o macaco
    ///    erguido na roda do lado de fora e a roda sem pneu (<c>Vehicles/PecasDosCarrosDoMundo.Padrao</c>), sem alarme e
    ///    sem rastejante. Ninguém mexe nele enquanto um dos dois está vivo (<see cref="Ocupado"/>);
    ///  - <b>os dois</b> (no anfitrião, com alguém a menos de <see cref="chegarPerto"/> m): o mecânico, de pistola, ajoelhado
    ///    na roda; o vigia, de rifle, de pé (Sobrevivente.Estrada.cs). Desconfiados: a 25 m, "Ei! Fica aí!"; quem mira
    ///    neles ouve "Abaixa essa arma" e, mirando por 2 s, eles atiram; quem chega a menos de 6 m com a arma na mão ouve
    ///    "Guarda essa arma" e, em 3 s, eles atiram. Hostis, lutam como os grupos (para sempre neste mundo);
    ///  - <b>a ajuda</b>: E num deles: "O estepe furou também. Tem um pneu aí?"; com um pneu de 30% ou mais, E dá o pneu
    ///    (ele vai na mochila de quem troca). A troca leva <see cref="duracaoDaTroca"/> s (só conta com quem troca ajoelhado
    ///    na roda): as batidas fazem barulho (<see cref="ZombieManager.Barulho"/>) e duas ondas de zumbis vêm atrás dele (à
    ///    noite, maiores). O vigia atira neles; o jogador protege. Quem troca morreu: o pneu fica na mochila dele, e a troca
    ///    volta a esperar. No fim, a roda fica com o pneu, o macaco sai, e eles pagam quem ajudou (dinheiro e uma caixa de
    ///    munição da arma dele) e ficam ali, amigos;
    ///  - <b>mortos</b>: as duas mochilas no chão (o pneu dado, se a troca não acabou), e o carro fica livre;
    ///  - <b>o visual</b> (em cada máquina): o pneu furado deitado no chão, do lado, e as batidas durante a troca;
    ///  - <b>o save</b>: o estado de cada cena (<see cref="Copia"/>, <see cref="Restaurar"/>).
    /// </summary>
    public class CarrosQuebradosDoMundo : NetworkBehaviour
    {
        public static CarrosQuebradosDoMundo Instancia { get; private set; }

        /// <summary>O grupo dos dois de um carro quebrado na rede é este mais o índice dele.</summary>
        public const int GrupoBase = 2000;
        /// <summary>O pneu que eles aceitam: 30% ou mais.</summary>
        public const int PneuMinimo = 30;

        [Tooltip("Os dois aparecem com alguém a menos disto do carro (m).")]
        public float chegarPerto = 110f;
        [Tooltip("E saem com todos a mais disto (m).")]
        public float sairLonge = 170f;
        [Tooltip("Quanto a troca do pneu leva (s, com quem troca ajoelhado na roda).")]
        public int duracaoDaTroca = 60;
        [Tooltip("A esta distância do carro, o vigia manda parar (m).")]
        public float distanciaDoAviso = 25f;
        [Tooltip("Mais perto que isto de um deles, com a arma na mão, é ameaça (m).")]
        public float pertoArmado = 6f;
        [Tooltip("Até onde o pneu furado no chão aparece (m).")]
        public float raioDoVisual = 160f;

        NetworkList<CenaDoCarroQuebrado> _cenas;

        // no anfitrião, por cena
        sealed class Dupla
        {
            public GrupoDeSobreviventes grupo;
            public Sobrevivente mecanico, vigia;
            public Vector3 carro, trabalho;
            public float giroDoTrabalho;
            public float proximoBarulho, proximaFala;
            public int ondas;
            public ulong quemAjudou = ulong.MaxValue;
            public readonly Dictionary<JogadorRede, float> mirando = new Dictionary<JogadorRede, float>();
            public readonly Dictionary<JogadorRede, float> armado = new Dictionary<JogadorRede, float>();
            public readonly HashSet<JogadorRede> avisados = new HashSet<JogadorRede>();
        }

        readonly List<Dupla> _duplas = new List<Dupla>();
        List<CenaDoCarroQuebrado> _doSave;
        bool _comecou;
        float _proximaConferencia, _proximoPensar, _proximoSegundo, _proximoVisual;

        void Awake()
        {
            Instancia = this;
            _cenas = new NetworkList<CenaDoCarroQuebrado>();
        }

        public override void OnDestroy()
        {
            ApagarVisuais();
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Instancia = this;
            _comecou = false;
            if (IsServer) _cenas.Clear();
        }

        public override void OnNetworkDespawn()
        {
            _duplas.Clear();
            ApagarVisuais();
        }

        static DadosDoMundo Dados
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                return m != null && m.Pronto && !m.NoDiaZero ? m.Dados : null;
            }
        }

        /// <summary>Quantos carros quebrados o mundo tem.</summary>
        public int Quantos => Dados?.Objetos?.Quebrados.Count ?? 0;

        /// <summary>O carro quebrado (o lugar, a roda, o número do carro do mundo), ou null.</summary>
        public CarroQuebrado Carro(int q)
        {
            var d = Dados;
            return d?.Objetos != null && q >= 0 && q < d.Objetos.Quebrados.Count ? d.Objetos.Quebrados[q] : null;
        }

        public EstadoDoCarroQuebrado Estado(int q) => IsSpawned && q >= 0 && q < _cenas.Count ? _cenas[q].estado : EstadoDoCarroQuebrado.Esperando;
        public int Falta(int q) => IsSpawned && q >= 0 && q < _cenas.Count ? _cenas[q].falta : 0;
        public bool Trocando(int q) => Estado(q) == EstadoDoCarroQuebrado.Trocando;
        public bool Hostil(int q) => Estado(q) == EstadoDoCarroQuebrado.Hostis;
        /// <summary>Quantos dos dois estão vivos.</summary>
        public int Vivos(int q)
        {
            if (!IsSpawned || q < 0 || q >= _cenas.Count) return 2;
            int m = _cenas[q].mortos;
            return 2 - (m & 1) - ((m >> 1) & 1);
        }

        /// <summary>
        /// Por que ninguém mexe no carro do mundo com este número agora (o carro quebrado com um dos dois vivo: o capô, as
        /// rodas e o macaco são deles), ou null. Em qualquer máquina.
        /// </summary>
        public string Ocupado(int carro)
        {
            var d = Dados;
            var c = d?.Objetos?.QuebradoDe(carro);
            if (c == null) return null;
            int q = d.Objetos.Quebrados.IndexOf(c);
            if (IsSpawned && q >= 0 && q < _cenas.Count && _cenas[q].mortos == 3) return null;
            return "O carro é deles";
        }

        /// <summary>O carro quebrado mais perto do ponto e a distância (-1 sem nenhum).</summary>
        public int MaisPerto(Vector3 ponto, out float distancia)
        {
            distancia = float.MaxValue;
            int melhor = -1;
            for (int q = 0; q < Quantos; q++)
            {
                float d = Vector2.Distance(new Vector2(ponto.x, ponto.z), Carro(q).Posicao);
                if (d < distancia)
                {
                    distancia = d;
                    melhor = q;
                }
            }
            return melhor;
        }

        /// <summary>Onde está o carro (pela pose do carro do mundo; sem ela, pelo plano), a frente e o lado de fora.</summary>
        public bool Pose(int q, out Vector3 carro, out Vector3 frente, out Vector3 fora)
        {
            var c = Carro(q);
            carro = frente = fora = default;
            if (c == null) return false;
            var objetos = ObjetosDoMundo.Instancia;
            if (objetos != null && objetos.PoseDoCarro(c.Indice, out carro, out var giro, out _))
            {
                frente = giro * Vector3.forward;
                fora = giro * Vector3.right;
                frente.y = fora.y = 0f;
                frente.Normalize();
                fora.Normalize();
                return true;
            }
            var d = Dados;
            carro = new Vector3(c.Posicao.x, d.Altura(c.Posicao.x, c.Posicao.y), c.Posicao.y);
            frente = new Vector3(c.Frente.x, 0f, c.Frente.y);
            fora = new Vector3(c.Fora.x, 0f, c.Fora.y);
            return true;
        }

        /// <summary>O F1: um lugar na estrada, 32 m atrás do carro, olhando para ele.</summary>
        public bool LugarParaChegar(int q, out Vector3 ponto, out float giro)
        {
            ponto = default;
            giro = 0f;
            if (!Pose(q, out var carro, out var frente, out var fora)) return false;
            ponto = carro - frente * 32f - fora * 2.5f;
            ponto.y = Dados.Altura(ponto.x, ponto.z) + 0.6f;
            giro = Quaternion.LookRotation(frente).eulerAngles.y;
            return true;
        }

        void Update()
        {
            if (!IsSpawned) return;
            AtualizarVisuais();
            if (!IsServer || Dados == null) return;
            if (!_comecou) Comecar();
            float agora = Time.time;
            if (agora >= _proximoPensar)
            {
                _proximoPensar = agora + 0.2f;
                for (int q = 0; q < _duplas.Count; q++)
                {
                    _duplas[q].grupo?.Pensar();
                    Vigiar(q, 0.2f);
                }
            }
            if (agora >= _proximoSegundo)
            {
                _proximoSegundo = agora + 1f;
                for (int q = 0; q < _duplas.Count; q++) Trocar(q, agora);
            }
            if (agora < _proximaConferencia) return;
            _proximaConferencia = agora + 1f;
            ConferirAsPessoas();
        }

        // o mundo ficou pronto: uma cena por carro quebrado (a do save, se veio)
        void Comecar()
        {
            _comecou = true;
            int n = Quantos;
            if (_doSave != null)
            {
                _cenas.Clear();
                for (int q = 0; q < n; q++) _cenas.Add(q < _doSave.Count ? _doSave[q] : default);
                _doSave = null;
            }
            while (_cenas.Count < n) _cenas.Add(default);
            while (_duplas.Count < n) _duplas.Add(new Dupla());
            Debug.Log($"[Carros quebrados] {n} no mundo.");
        }

        static bool Vivo(Sobrevivente s) => s != null && s.IsSpawned && !s.Morto;

        // ------------------------------------------------------------------ os dois (no anfitrião)

        void ConferirAsPessoas()
        {
            var prefab = SobreviventesDoMundo.Instancia != null ? SobreviventesDoMundo.Instancia.prefab : null;
            if (prefab == null) return;
            for (int q = 0; q < _duplas.Count && q < _cenas.Count; q++)
            {
                var e = _duplas[q];
                if (!Pose(q, out var carro, out _, out _)) continue;
                e.carro = carro;
                float perto = GrupoDeSobreviventes.PertoDeUmJogador(carro);
                if (e.grupo == null && _cenas[q].mortos != 3 && perto < chegarPerto) Povoar(q, e, prefab);
                else if (e.grupo != null && perto > sairLonge) Recolher(e);
                // sem munição, um deles fugiu e sumiu: não volta
                if (e.grupo != null)
                    foreach (var m in e.grupo.membros.ToArray())
                        if (m != null && !m.Morto && m.Fugiu)
                        {
                            var c = _cenas[q];
                            c.mortos |= (byte)(1 << m.Identidade.Value.indice);
                            if (c.mortos == 3) c.estado = EstadoDoCarroQuebrado.Mortos;
                            _cenas[q] = c;
                            e.grupo.membros.Remove(m);
                            var no = m.GetComponent<NetworkObject>();
                            if (no != null && no.IsSpawned) no.Despawn(true);
                        }
            }
        }

        void Povoar(int q, Dupla e, GameObject prefab)
        {
            var c = Carro(q);
            var objetos = ObjetosDoMundo.Instancia;
            if (c == null || objetos == null || !Pose(q, out var carro, out var frente, out var fora)) return;
            if (!objetos.PontoDaRoda(c.Indice, c.Roda, out var roda, out _)) roda = carro + fora * 0.8f + frente * (c.Roda < 2 ? 1.3f : -1.3f);
            bool naFrente = c.Roda < 2;
            // o mecânico ajoelha do lado da roda, virado para ela; o vigia fica do lado de fora, na outra metade do carro
            e.trabalho = NoChao(roda + fora * 0.75f);
            e.giroDoTrabalho = Quaternion.LookRotation(-fora).eulerAngles.y;
            var lugarDoVigia = NoChao(carro + fora * 3.4f + frente * (naFrente ? -2.6f : 2.6f));
            float giroDoVigia = Quaternion.LookRotation(fora * 0.6f + frente * (naFrente ? -0.8f : 0.8f)).eulerAngles.y;
            var depois = NoChao(roda + fora * 1.8f);   // o mecânico depois da troca: de pé, do lado do carro
            var cena = _cenas[q];
            var g = new GrupoDeSobreviventes { cidade = -1, cena = q, quantos = 2 };
            var s = new System.Random(unchecked(Dados.Semente * 7919 + q * 104729 + 23));
            for (int i = 0; i < 2; i++)
            {
                bool mecanico = i == 0;
                var arma = mecanico ? TipoDeArma.Pistola : s.Next(100) < 70 ? TipoDeArma.Rifle : TipoDeArma.Espingarda;
                byte roupa = (byte)(s.Next(100) < 55 ? 3 : 0);
                int sementeDaRoupa = s.Next();
                if ((cena.mortos & (1 << i)) != 0) continue;   // este morreu
                bool ajudados = cena.estado == EstadoDoCarroQuebrado.Ajudados;
                var lugar = mecanico ? (ajudados ? depois : e.trabalho) : lugarDoVigia;
                float giro = mecanico ? (ajudados ? Quaternion.LookRotation(fora).eulerAngles.y : e.giroDoTrabalho) : giroDoVigia;
                var go = Instantiate(prefab, lugar, Quaternion.Euler(0f, giro, 0f));
                var sob = go.GetComponent<Sobrevivente>();
                sob.Identidade.Value = new IdentidadeDoSobrevivente
                {
                    semente = sementeDaRoupa, arma = arma, roupa = roupa, grupo = (ushort)(GrupoBase + q),
                    papel = mecanico ? PapelDoSobrevivente.Mecanico : PapelDoSobrevivente.Vigia, indice = (byte)i,
                };
                sob.grupo = g;
                sob.cena = q;
                sob.posto = lugar;
                sob.giroDoPosto = giro;
                SobreviventesDoMundo.Encher(sob, arma);
                if (mecanico && (cena.estado == EstadoDoCarroQuebrado.Esperando || cena.estado == EstadoDoCarroQuebrado.Trocando)) sob.NascerAjoelhado();
                go.GetComponent<NetworkObject>().Spawn(true);
                g.membros.Add(sob);
                if (mecanico) e.mecanico = sob;
                else e.vigia = sob;
            }
            e.grupo = g;
            // na troca, o pneu dado está com quem troca
            if (cena.estado == EstadoDoCarroQuebrado.Trocando)
            {
                var t = QuemTroca(e);
                if (t != null) t.mochila.Add(new ItemNaMochila { item = Item.Pneu, qtd = cena.pneu });
            }
            Debug.Log($"[Carros quebrados] Os dois do carro {q} apareceram ({g.membros.Count} vivos, {cena.estado}).");
        }

        void Recolher(Dupla e)
        {
            foreach (var m in e.grupo.membros.ToArray())
            {
                if (m == null) continue;
                var no = m.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            e.grupo.membros.Clear();
            e.grupo = null;
            e.mecanico = e.vigia = null;
            e.mirando.Clear();
            e.armado.Clear();
            e.avisados.Clear();
        }

        /// <summary>No anfitrião (o F1): os dois de todos os carros saem (voltam quando alguém chegar perto).</summary>
        public void TirarAsPessoas()
        {
            foreach (var e in _duplas)
                if (e.grupo != null) Recolher(e);
        }

        // o chão embaixo do ponto: o NavMesh perto, ou o cenário
        static Vector3 NoChao(Vector3 p)
        {
            if (NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) return hit.position;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var chao, 8f, 1, QueryTriggerInteraction.Ignore)) return chao.point;
            return p;
        }

        // quem troca o pneu: o mecânico; sem ele, o vigia
        static Sobrevivente QuemTroca(Dupla e) => Vivo(e.mecanico) ? e.mecanico : Vivo(e.vigia) ? e.vigia : null;

        /// <summary>
        /// No anfitrião: quem fica ajoelhado na roda agora (esperando, o mecânico; na troca, o mecânico ou, sem ele, o
        /// vigia), ou null.
        /// </summary>
        internal Sobrevivente Trabalhador(int q)
        {
            if (q < 0 || q >= _duplas.Count || q >= _cenas.Count) return null;
            var e = _duplas[q];
            var estado = _cenas[q].estado;
            if (estado == EstadoDoCarroQuebrado.Esperando) return Vivo(e.mecanico) ? e.mecanico : null;
            return estado == EstadoDoCarroQuebrado.Trocando ? QuemTroca(e) : null;
        }

        /// <summary>No anfitrião: onde quem troca ajoelha (no NavMesh) e para onde olha (a roda).</summary>
        internal void LugarDoTrabalho(int q, out Vector3 ponto, out float giro)
        {
            var e = _duplas[q];
            ponto = NavMesh.SamplePosition(e.trabalho, out var hit, 1f, NavMesh.AllAreas) ? hit.position : e.trabalho;
            giro = e.giroDoTrabalho;
        }

        /// <summary>
        /// No anfitrião: o jogador para o vigia olhar (o mais perto, até 30 m) e se ele levanta a arma para ele (esperando,
        /// com o jogador armado a menos de 25 m do carro, ou mirando neles).
        /// </summary>
        internal JogadorRede QuemVigiar(int q, Vector3 de, out bool apontar)
        {
            apontar = false;
            var estado = Estado(q);
            if (estado != EstadoDoCarroQuebrado.Esperando && estado != EstadoDoCarroQuebrado.Trocando && estado != EstadoDoCarroQuebrado.Ajudados) return null;
            JogadorRede melhor = null;
            float menor = 30f * 30f;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                float d2 = (j.transform.position - de).sqrMagnitude;
                if (d2 >= menor) continue;
                menor = d2;
                melhor = j;
            }
            if (melhor != null && estado == EstadoDoCarroQuebrado.Esperando)
            {
                var e = _duplas[q];
                bool perto = (melhor.transform.position - e.carro).sqrMagnitude < distanciaDoAviso * distanciaDoAviso;
                apontar = (perto && Armado(melhor)) || e.mirando.ContainsKey(melhor) || e.armado.ContainsKey(melhor);
            }
            return melhor;
        }

        /// <summary>O jogador está com uma arma na mão (de fogo, ou uma branca que não seja a tocha). Em qualquer máquina.</summary>
        public static bool Armado(JogadorRede j)
        {
            var it = j != null ? j.NaMao : Item.Nada;
            return Itens.EhArma(it) || (Itens.EhBranca(it) && it != Item.Tocha);
        }

        // ------------------------------------------------------------------ a desconfiança (no anfitrião)

        // esperando: o aviso a 25 m, a mira (2 s) e a arma na mão a menos de 6 m (3 s) deixam os dois hostis; na troca e
        // depois, a mira só leva a bronca (quem protege mira nos zumbis do lado deles)
        void Vigiar(int q, float passo)
        {
            if (q >= _cenas.Count) return;
            var estado = _cenas[q].estado;
            var e = _duplas[q];
            if (e.grupo == null || (estado != EstadoDoCarroQuebrado.Esperando && estado != EstadoDoCarroQuebrado.Trocando && estado != EstadoDoCarroQuebrado.Ajudados)) return;
            var falante = Vivo(e.vigia) ? e.vigia : e.mecanico;
            if (!Vivo(falante)) return;
            bool esperando = estado == EstadoDoCarroQuebrado.Esperando;
            float agora = Time.time;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                Vector3 pj = j.transform.position;
                float dc = Vector2.Distance(new Vector2(pj.x, pj.z), new Vector2(e.carro.x, e.carro.z));
                if (dc > 40f) e.avisados.Remove(j);
                if (esperando && dc < distanciaDoAviso && e.avisados.Add(j)) falante.Falar(Frase.FicaAi);
                // a mira (com o botão direito) num dos dois
                var mirado = Mirado(j, e);
                if (mirado == null) e.mirando.Remove(j);
                else
                {
                    e.mirando.TryGetValue(j, out float antes);
                    float tempo = antes + passo;
                    e.mirando[j] = tempo;
                    if (esperando)
                    {
                        if (antes < 0.4f && tempo >= 0.4f) mirado.Falar(Frase.AbaixaArma, true);
                        if (tempo >= 2f)
                        {
                            TornarHostil(q, j);
                            return;
                        }
                    }
                    else if (tempo >= 2f && agora >= e.proximaFala)
                    {
                        e.proximaFala = agora + 6f;
                        mirado.Falar(Frase.AbaixaArma);
                    }
                }
                // armado, perto de um deles
                if (!esperando) continue;
                float dn = float.MaxValue;
                foreach (var m in e.grupo.membros)
                    if (Vivo(m)) dn = Mathf.Min(dn, Vector3.Distance(m.transform.position, pj));
                if (dn < pertoArmado && Armado(j))
                {
                    e.armado.TryGetValue(j, out float antes);
                    if (antes <= 0f) falante.Falar(Frase.GuardaArma, true);
                    float tempo = antes + passo;
                    e.armado[j] = tempo;
                    if (tempo >= 3f)
                    {
                        TornarHostil(q, j);
                        return;
                    }
                }
                else e.armado.Remove(j);
            }
        }

        // o jogador mirando (com o botão direito) no peito de um dos dois, até 35 m
        static Sobrevivente Mirado(JogadorRede j, Dupla e)
        {
            if (!j.Mirando.Value) return null;
            Vector3 olho = j.transform.position + Vector3.up * 1.6f;
            Vector3 dir = Quaternion.Euler(j.Pitch.Value, j.transform.eulerAngles.y, 0f) * Vector3.forward;
            foreach (var m in e.grupo.membros)
            {
                if (!Vivo(m)) continue;
                Vector3 v = m.transform.position + Vector3.up * (m.Ajoelhado ? 0.8f : 1.3f) - olho;
                float t = Vector3.Dot(v, dir);
                if (t < 0.5f || t > 35f || (v - dir * t).sqrMagnitude > 0.75f * 0.75f) continue;
                return m;
            }
            return null;
        }

        /// <summary>No anfitrião: alguém mirou, chegou armado ou feriu um deles; os dois ficam contra os jogadores, para sempre.</summary>
        public void TornarHostil(int q, JogadorRede quem)
        {
            if (!IsServer || q < 0 || q >= _cenas.Count) return;
            var c = _cenas[q];
            if (c.estado == EstadoDoCarroQuebrado.Mortos) return;
            bool novo = c.estado != EstadoDoCarroQuebrado.Hostis;
            c.estado = EstadoDoCarroQuebrado.Hostis;   // (na troca, o pneu fica na mochila de quem trocava)
            c.falta = 0;
            _cenas[q] = c;
            var e = q < _duplas.Count ? _duplas[q] : null;
            var g = e?.grupo;
            if (g != null && quem != null) g.Ver(quem, quem.transform.position, null);
            if (!novo) return;
            Debug.Log($"[Carros quebrados] Os dois do carro {q} ficaram hostis.");
            if (g != null)
                foreach (var m in g.membros)
                    if (Vivo(m))
                    {
                        m.Falar(Frase.EuAvisei, true);
                        break;
                    }
        }

        /// <summary>No anfitrião: um dos dois morreu (a máscara dos mortos e a mochila no chão).</summary>
        public void AoMorrer(Sobrevivente s)
        {
            if (!IsServer || s == null) return;
            int q = s.cena;
            if (q < 0 || q >= _cenas.Count) return;
            var c = _cenas[q];
            c.mortos |= (byte)(1 << s.Identidade.Value.indice);
            // quem trocava levou o pneu (está na mochila dele): a troca volta a esperar
            if (c.estado == EstadoDoCarroQuebrado.Trocando && s.mochila.Exists(it => it.item == Item.Pneu))
            {
                c.estado = EstadoDoCarroQuebrado.Esperando;
                c.falta = 0;
            }
            if (c.mortos == 3) c.estado = EstadoDoCarroQuebrado.Mortos;
            _cenas[q] = c;
            SobreviventesDoMundo.LargarAMochila(s, null);
            Debug.Log($"[Carros quebrados] Morreu {(s.Identidade.Value.papel == PapelDoSobrevivente.Mecanico ? "o mecânico" : "o vigia")} do carro {q} ({c.estado}).");
        }

        // ------------------------------------------------------------------ a ajuda (no anfitrião)

        /// <summary>O jogador local dá um pneu (pelo Id; 0, o das mãos) para os dois do carro quebrado.</summary>
        public void PedirDarPneu(int q, ushort id) => DarPneuRpc((byte)q, id);

        /// <summary>O jogador local fala com os dois.</summary>
        public void PedirConversa(int q) => ConversaRpc((byte)q);

        // quem pede: o inventário dele, se está a até 5 m de um dos dois vivos
        InventarioJogador QuemFala(RpcParams parametros, int q, out JogadorRede jogador, out Sobrevivente maisPerto)
        {
            jogador = null;
            maisPerto = null;
            if (q >= _cenas.Count || q >= _duplas.Count) return null;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return null;
            var e = _duplas[q];
            if (e.grupo == null) return null;
            float menor = 5f;
            foreach (var m in e.grupo.membros)
            {
                if (!Vivo(m)) continue;
                float d = Vector3.Distance(m.transform.position, cliente.PlayerObject.transform.position);
                if (d >= menor) continue;
                menor = d;
                maisPerto = m;
            }
            if (maisPerto == null) return null;
            jogador = cliente.PlayerObject.GetComponent<JogadorRede>();
            return cliente.PlayerObject.GetComponent<InventarioJogador>();
        }

        [Rpc(SendTo.Server)]
        void DarPneuRpc(byte q, ushort id, RpcParams parametros = default)
        {
            var inv = QuemFala(parametros, q, out var j, out var perto);
            if (inv == null || _cenas[q].estado != EstadoDoCarroQuebrado.Esperando || Armado(j)) return;
            var e = _duplas[q];
            var t = QuemTroca(e);
            if (t == null) return;
            int estado;
            if (id == 0)
            {
                if (inv.EstadoDoPneuNasMaos < PneuMinimo || !inv.TirarPneuDasMaos(out estado)) return;
            }
            else
            {
                if (!inv.Achar(id, out var it) || it.item != Item.Pneu || it.qtd < PneuMinimo) return;
                inv.Tirar(id);
                estado = it.qtd;
            }
            t.mochila.Add(new ItemNaMochila { item = Item.Pneu, qtd = (ushort)Mathf.Clamp(estado, 0, 100) });
            var c = _cenas[q];
            c.estado = EstadoDoCarroQuebrado.Trocando;
            c.pneu = (byte)Mathf.Clamp(estado, 0, 100);
            c.falta = (byte)Mathf.Clamp(duracaoDaTroca, 1, 250);
            _cenas[q] = c;
            e.quemAjudou = j != null ? j.OwnerClientId : ulong.MaxValue;
            e.ondas = 0;
            e.proximoBarulho = Time.time + 3f;
            perto.Falar(Frase.Valeu, true);
            inv.AvisarDono("Você deu o pneu. Proteja os dois enquanto eles trocam.");
            Debug.Log($"[Carros quebrados] O carro {q} ganhou um pneu de {estado}%: a troca começou.");
        }

        [Rpc(SendTo.Server)]
        void ConversaRpc(byte q, RpcParams parametros = default)
        {
            var inv = QuemFala(parametros, q, out _, out var perto);
            if (inv == null) return;
            var estado = _cenas[q].estado;
            if (estado == EstadoDoCarroQuebrado.Esperando)
            {
                bool temRuim = Vehicles.PortaMalasDoCarro.AcharPneu(inv, out int e, out _) && e < PneuMinimo;
                perto.Falar(temRuim ? Frase.PneuRuim : Frase.Estepe, true);
            }
            else if (estado == EstadoDoCarroQuebrado.Ajudados) perto.Falar(Frase.Obrigado, true);
        }

        // a troca, uma vez por segundo: conta só com quem troca ajoelhado na roda (longe, ela espera); as batidas chamam os
        // zumbis em volta, e duas ondas vêm atrás de quem troca
        void Trocar(int q, float agora)
        {
            if (q >= _cenas.Count) return;
            var c = _cenas[q];
            if (c.estado != EstadoDoCarroQuebrado.Trocando) return;
            var e = _duplas[q];
            var t = Trabalhador(q);
            if (e.grupo == null || t == null || !t.Ajoelhado) return;
            if (c.falta > 0) c.falta--;
            if (agora >= e.proximoBarulho)
            {
                e.proximoBarulho = agora + UnityEngine.Random.Range(6f, 10f);
                ZombieManager.Barulho(e.trabalho, t);
            }
            int passou = duracaoDaTroca - c.falta;
            if ((e.ondas == 0 && passou >= 12) || (e.ondas == 1 && passou >= 38))
            {
                e.ondas++;
                Onda(q, e, t);
            }
            if (c.falta == 0) Terminar(q, ref c, e, t);
            _cenas[q] = c;
        }

        // uma onda: 2 ou 3 de dia (o sol para muitos no caminho), 4 a 6 à noite, mais 1 por jogador a mais; nascem juntos
        // a 40–55 m do carro, do lado contrário de quem está perto e fora da vista, e vêm atrás de quem troca
        void Onda(int q, Dupla e, Sobrevivente t)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null) return;
            int jogadores = 0;
            Vector3 de = e.carro;
            float menor = float.MaxValue;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido) continue;
                jogadores++;
                float d = (j.transform.position - e.carro).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    de = j.transform.position;
                }
            }
            bool noite = CicloDoDia.Noite > 0.5f;
            int n = (noite ? UnityEngine.Random.Range(4, 7) : UnityEngine.Random.Range(2, 4)) + Mathf.Max(0, jogadores - 1);
            Vector3 longe = e.carro - de;
            longe.y = 0f;
            longe = longe.sqrMagnitude > 1f ? longe.normalized : Vector3.forward;
            Vector3 centro = e.carro + longe * 45f;
            bool achou = false;
            for (int k = 0; k < 20 && !achou; k++)
            {
                var dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-75f, 75f), 0f) * longe;
                var p = e.carro + dir * UnityEngine.Random.Range(40f, 55f);
                if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas) || ZombieManager.NaZonaSegura(hit.position)) continue;
                if (k < 14 && AlguemVe(hit.position + Vector3.up * 1.5f)) continue;
                centro = hit.position;
                achou = true;
            }
            int nasceram = zm.CriarHordaPerseguindo(t, centro, n);
            Debug.Log($"[Carros quebrados] Onda {e.ondas} no carro {q}: {nasceram} de {n} zumbis{(noite ? " (noite)" : "")}.");
        }

        static bool AlguemVe(Vector3 ponto)
        {
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned) continue;
                var olho = j.transform.position + Vector3.up * 1.6f;
                if ((olho - ponto).sqrMagnitude < 70f * 70f && !Physics.Linecast(olho, ponto, 1 << 0, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // a troca acabou: o pneu da mochila de quem trocou vai para a roda, o macaco sai, e eles pagam quem ajudou
        void Terminar(int q, ref CenaDoCarroQuebrado c, Dupla e, Sobrevivente t)
        {
            int k = t.mochila.FindIndex(it => it.item == Item.Pneu);
            if (k >= 0) t.mochila.RemoveAt(k);
            var carro = Carro(q);
            if (carro != null) Vehicles.PecasDosCarrosDoMundo.Instancia?.TrocaDosSobreviventes(carro.Indice, carro.Roda, c.pneu);
            c.estado = EstadoDoCarroQuebrado.Ajudados;
            c.falta = 0;
            (Vivo(e.vigia) ? e.vigia : t).Falar(Frase.Pronto, true);
            Pagar(q, e);
            Debug.Log($"[Carros quebrados] O pneu do carro {q} foi trocado.");
        }

        // quem deu o pneu (ou, sem ele, o jogador mais perto, até 40 m) ganha o dinheiro e uma caixa de munição da arma
        // dele (a da mão; senão, a primeira da mochila; sem arma, 9 mm). Sem lugar na mochila, a caixa fica no chão
        void Pagar(int q, Dupla e)
        {
            JogadorRede quem = null;
            float menor = 40f;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned) continue;
                if (j.OwnerClientId == e.quemAjudou)
                {
                    quem = j;
                    break;
                }
                float d = Vector3.Distance(j.transform.position, e.carro);
                if (d < menor)
                {
                    menor = d;
                    quem = j;
                }
            }
            var inv = quem != null ? quem.GetComponent<InventarioJogador>() : null;
            if (inv == null) return;
            var s = new System.Random(unchecked(Dados.Semente * 31 + q * 977 + 5));
            int dinheiro = 60 + s.Next(0, 9) * 10;
            inv.Pagar(dinheiro);
            Item arma = Itens.EhArma(inv.NaMao) ? inv.NaMao : Item.Nada;
            if (arma == Item.Nada)
                foreach (var it in inv.Mochila)
                    if (Itens.EhArma(it.item))
                    {
                        arma = it.item;
                        break;
                    }
            var caixa = arma != Item.Nada ? Itens.CaixaDe(Itens.CalibreDaArma(arma)) : Item.Municao9mm;
            int balas = arma != Item.Nada ? Mathf.Clamp(Armas.Dados(Itens.Arma(arma)).capacidade * 2, 6, Itens.MaxPorCaixa) : 24;
            if (!inv.Receber(caixa, balas))
                ItensNoMundo.Instancia?.Criar(caixa, NoChao(e.trabalho + (quem.transform.position - e.trabalho).normalized * 1.5f), 0f, balas);
            inv.AvisarDono($"Eles pagaram {DiaZero.PrecosDoDiaZero.Dinheiro(dinheiro)} e deram {balas} balas ({Itens.Nome(caixa)}).");
        }

        // ------------------------------------------------------------------ o F1 (no anfitrião)

        /// <summary>O F1: a cena volta ao começo (os dois vivos, esperando, a roda sem pneu e o macaco erguido).</summary>
        public void Recomecar(int q)
        {
            if (!IsServer || q < 0 || q >= _cenas.Count) return;
            var e = _duplas[q];
            if (e.grupo != null) Recolher(e);
            _cenas[q] = default;
            var carro = Carro(q);
            if (carro != null) Vehicles.PecasDosCarrosDoMundo.Instancia?.Esquecer(carro.Indice);
        }

        /// <summary>O F1: a troca termina no próximo segundo.</summary>
        public void AcabarATroca(int q)
        {
            if (!IsServer || q < 0 || q >= _cenas.Count || _cenas[q].estado != EstadoDoCarroQuebrado.Trocando) return;
            var c = _cenas[q];
            c.falta = 1;
            _cenas[q] = c;
            _duplas[q].ondas = 2;
        }

        // ------------------------------------------------------------------ o save

        /// <summary>O estado de cada cena (o save).</summary>
        public List<CenaDoCarroQuebrado> Copia()
        {
            var lista = new List<CenaDoCarroQuebrado>(_cenas.Count);
            foreach (var c in _cenas) lista.Add(c);
            return lista;
        }

        /// <summary>No anfitrião: as cenas do mundo salvo (aplicadas quando o mundo fica pronto).</summary>
        public void Restaurar(List<CenaDoCarroQuebrado> lista)
        {
            if (!IsServer || lista == null) return;
            _doSave = new List<CenaDoCarroQuebrado>(lista);
            if (_comecou)
            {
                foreach (var e in _duplas)
                    if (e.grupo != null) Recolher(e);
                _comecou = false;   // o Comecar aplica
            }
        }

        // ------------------------------------------------------------------ o visual (em cada máquina)

        readonly Dictionary<int, GameObject> _pneusNoChao = new Dictionary<int, GameObject>();
        readonly Dictionary<int, float> _proximaBatida = new Dictionary<int, float>();
        static AudioClip[] _batidas;

        void AtualizarVisuais()
        {
            var d = Dados;
            var j = JogadorRede.Local;
            if (d == null || j == null) return;
            Vector3 aqui = j.transform.position;
            if (Time.time >= _proximoVisual)
            {
                _proximoVisual = Time.time + 0.5f;
                for (int q = 0; q < Quantos; q++)
                {
                    var c = Carro(q);
                    float dist = Vector2.Distance(new Vector2(aqui.x, aqui.z), c.Posicao);
                    bool tem = _pneusNoChao.TryGetValue(q, out var go) && go != null;
                    if (!tem && dist < raioDoVisual) PorOPneuFurado(q);
                    else if (tem && dist > raioDoVisual + 40f)
                    {
                        Destroy(go);
                        _pneusNoChao.Remove(q);
                    }
                }
            }
            // as batidas da troca, perto
            for (int q = 0; q < Quantos && q < _cenas.Count; q++)
            {
                if (_cenas[q].estado != EstadoDoCarroQuebrado.Trocando) continue;
                var c = Carro(q);
                if (Vector2.Distance(new Vector2(aqui.x, aqui.z), c.Posicao) > 45f) continue;
                _proximaBatida.TryGetValue(q, out float quando);
                if (Time.time < quando) continue;
                _proximaBatida[q] = Time.time + UnityEngine.Random.Range(0.7f, 1.6f);
                var objetos = ObjetosDoMundo.Instancia;
                if (objetos == null || !objetos.PontoDaRoda(c.Indice, c.Roda, out var roda, out _)) continue;
                var batidas = Batidas();
                Efeitos.Som(batidas[UnityEngine.Random.Range(0, batidas.Length)], roda, 0.55f, true, 40f);
            }
        }

        // o pneu furado deitado no chão, do lado de quem troca
        void PorOPneuFurado(int q)
        {
            var c = Carro(q);
            var objetos = ObjetosDoMundo.Instancia;
            var itens = ItensNoMundo.Instancia;
            if (c == null || objetos == null || itens == null || !Pose(q, out _, out var frente, out var fora)) return;
            if (!objetos.PontoDaRoda(c.Indice, c.Roda, out var roda, out _)) return;
            var malha = itens.MalhaDe(Item.Pneu);
            var material = itens.MaterialDe(Item.Pneu);
            if (malha == null || material == null) return;
            var p = roda + fora * 1.35f + frente * (c.Roda < 2 ? -0.95f : 0.95f);
            float chao = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 5f, 1, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y - 0.35f;
            // deitado: o eixo (a menor medida da malha) para cima
            var b = malha.bounds;
            Vector3 eixo = b.size.x <= b.size.y && b.size.x <= b.size.z ? Vector3.right : b.size.y <= b.size.z ? Vector3.up : Vector3.forward;
            float meiaEspessura = Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z)) * 0.5f;
            var giro = Quaternion.AngleAxis((q * 137f + 40f) % 360f, Vector3.up) * Quaternion.FromToRotation(eixo, Vector3.up);
            var go = new GameObject($"Pneu furado do carro {q}");
            go.transform.SetParent(transform, true);
            var meio = giro * b.center;
            go.transform.SetPositionAndRotation(new Vector3(p.x - meio.x, chao + meiaEspessura - meio.y + 0.01f, p.z - meio.z), giro);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            _pneusNoChao[q] = go;
        }

        void ApagarVisuais()
        {
            foreach (var go in _pneusNoChao.Values)
                if (go != null) Destroy(go);
            _pneusNoChao.Clear();
        }

        // a batida de ferro (a chave de roda, o martelo na roda): três variações, geradas uma vez
        static AudioClip[] Batidas()
        {
            if (_batidas != null) return _batidas;
            _batidas = new AudioClip[3];
            int taxa = 22050, n = Mathf.RoundToInt(taxa * 0.3f);
            float[] parciais = { 1f, 1.63f, 2.71f, 4.23f };
            float[] quedas = { 16f, 24f, 34f, 46f };
            float[] pesos = { 0.55f, 0.35f, 0.25f, 0.15f };
            for (int v = 0; v < 3; v++)
            {
                var r = new System.Random(71 + v);
                float base_ = 690f + v * 95f;
                var dados = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)taxa;
                    float s = 0f;
                    for (int k = 0; k < parciais.Length; k++) s += Mathf.Sin(2f * Mathf.PI * base_ * parciais[k] * t) * pesos[k] * Mathf.Exp(-t * quedas[k]);
                    s += ((float)r.NextDouble() * 2f - 1f) * 0.5f * Mathf.Exp(-t * 400f);   // o estalo do golpe
                    dados[i] = s * 0.7f * Mathf.Min(1f, i / 12f);
                }
                var clip = AudioClip.Create("Batida na roda " + v, n, 1, taxa, false);
                clip.SetData(dados, 0);
                _batidas[v] = clip;
            }
            return _batidas;
        }
    }
}
