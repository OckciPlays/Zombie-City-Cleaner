using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O painel de armas do salão do bunker: cada arma tem o seu lugar (as 4 longas e 4 pistolas), em pé, com o lado
    /// esquerdo para fora. Quem mira uma arma de perto e aperta E pega ela: vai para a mochila
    /// (<see cref="InventarioJogador"/>), se couber, e para um atalho livre e a mão. Sem espaço na mochila, ela fica (a
    /// dica avisa). Mirando uma vaga vazia com a arma dela na mão, E guarda a arma.
    /// Quem manda é o anfitrião: o pedido vai para ele, que confere a distância e muda as vagas e o inventário. Quem sai
    /// da partida devolve as armas.
    /// O conteúdo das vagas vai pela rede num número só, 8 bits por vaga (0 = vazia, senão o tipo + 1), até 8 vagas; o
    /// pente de cada arma guardada, em outro (8 bits por vaga): a arma volta com as balas que tinha. No começo, cheias.
    /// </summary>
    public class ParedeDeArmas : NetworkBehaviour
    {
        [Tooltip("As vagas, na ordem do painel (as longas de cima para baixo e as pistolas).")]
        public VagaDeArma[] vagas;
        [Tooltip("A malha de cada arma, na ordem de TipoDeArma (origem no cabo, cano em +Z, topo em +Y).")]
        public Mesh[] malhas;
        [Tooltip("O que cada vaga tem no começo da partida, na ordem das vagas (as que faltam começam vazias: os " +
                 "jogadores nascem com a pistola).")]
        public TipoDeArma[] inicio = { TipoDeArma.Sniper, TipoDeArma.Rifle, TipoDeArma.Espingarda, TipoDeArma.Submetralhadora };
        [Tooltip("Até onde o anfitrião aceita o pedido de troca (m, dos pés do jogador ao meio da vaga).")]
        public float distanciaMaxima = 3.5f;

        readonly NetworkVariable<long> _conteudo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // as balas no pente da arma de cada vaga (8 bits por vaga), só para o anfitrião saber o que entregar
        readonly NetworkVariable<long> _pentes = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public static ParedeDeArmas Instancia { get; private set; }

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        void Start()
        {
            if (!IsSpawned) Aplicar(Codificar(inicio));   // no menu, antes da partida
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _conteudo.Value = Codificar(inicio);
                long pentes = 0;
                for (int i = 0; i < inicio.Length && i < 8; i++) pentes = GravarPente(pentes, i, Armas.Dados(inicio[i]).capacidade);
                _pentes.Value = pentes;
            }
            _conteudo.OnValueChanged += AoMudar;
            Aplicar(_conteudo.Value);
        }

        public override void OnNetworkDespawn()
        {
            _conteudo.OnValueChanged -= AoMudar;
            Aplicar(Codificar(inicio));
        }

        void AoMudar(long antes, long depois) => Aplicar(depois);

        /// <summary>A arma de uma vaga (ou null, vazia).</summary>
        public TipoDeArma? Vaga(int indice) => Ler(IsSpawned ? _conteudo.Value : Codificar(inicio), indice);

        /// <summary>O jogador local usou a vaga: pega a arma dela (ou troca pela da mão), ou guarda a da mão nela.</summary>
        public void PedirVaga(int indice, InventarioJogador quem)
        {
            if (!IsSpawned || indice < 0 || indice >= vagas.Length) return;
            quem.AvisarQuePegou();
            quem.GuardarPenteDaMao();   // guardando a arma da mão, ela vai com o pente que tem agora
            VagaRpc((byte)indice);
        }

        [Rpc(SendTo.Server)]
        void VagaRpc(byte indice, RpcParams parametros = default)
        {
            if (indice >= vagas.Length) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            if (inventario == null || Vector3.Distance(cliente.PlayerObject.transform.position, vagas[indice].transform.position) > distanciaMaxima) return;
            var naVaga = Ler(_conteudo.Value, indice);
            var naMao = inventario.NaMao;
            if (naVaga == null)
            {
                // guardar: só a arma desta vaga
                if (!Itens.EhArma(naMao) || Itens.Arma(naMao) != vagas[indice].dono) return;
                inventario.Achar(inventario.IdNaMao, out var arma);
                inventario.Tirar(inventario.IdNaMao);
                _pentes.Value = GravarPente(_pentes.Value, indice, arma.qtd);
                _conteudo.Value = Gravar(_conteudo.Value, indice, Itens.Arma(naMao));
                return;
            }
            // pegar: só se couber na mochila (sem espaço, nada; a dica avisa)
            if (inventario.Receber(Itens.DaArma(naVaga.Value), LerPente(_pentes.Value, indice))) _conteudo.Value = Gravar(_conteudo.Value, indice, null);
        }

        /// <summary>
        /// No anfitrião: a arma de quem saiu da partida volta para uma vaga livre do tipo dela (se houver), com o pente que
        /// tinha.
        /// </summary>
        public void Devolver(TipoDeArma arma, int pente)
        {
            if (!IsSpawned || !IsServer) return;
            int casa = VagaLivre(_conteudo.Value, arma);
            if (casa < 0) return;
            _pentes.Value = GravarPente(_pentes.Value, casa, pente);
            _conteudo.Value = Gravar(_conteudo.Value, casa, arma);
        }

        static long GravarPente(long pentes, int indice, int balas) =>
            (pentes & ~(0xFFL << (8 * indice))) | ((long)Mathf.Clamp(balas, 0, 255) << (8 * indice));

        static int LerPente(long pentes, int indice) => (int)((pentes >> (8 * indice)) & 0xFF);

        int VagaLivre(long conteudo, TipoDeArma tipo)
        {
            for (int i = 0; i < vagas.Length; i++)
                if (vagas[i] != null && vagas[i].dono == tipo && Ler(conteudo, i) == null) return i;
            return -1;
        }

        static long Codificar(TipoDeArma[] armas)
        {
            long v = 0;
            for (int i = 0; i < armas.Length && i < 8; i++) v = Gravar(v, i, armas[i]);
            return v;
        }

        static long Gravar(long conteudo, int indice, TipoDeArma? arma) =>
            (conteudo & ~(0xFFL << (8 * indice))) | ((arma == null ? 0L : (long)arma.Value + 1) << (8 * indice));

        static TipoDeArma? Ler(long conteudo, int indice)
        {
            int v = (int)((conteudo >> (8 * indice)) & 0xFF);
            return v == 0 ? null : (TipoDeArma)(v - 1);
        }

        // não serializado: no editor, ao entrar no Play, a Unity guardava o campo como uma lista vazia (e o ??= não criava a
        // de verdade: o índice estourava e o painel não aparecia)
        [System.NonSerialized] Material[] _paleta;

        // cada arma em pé no apoio da vaga: o cano ao longo do painel, o lado esquerdo para fora, a parte de baixo (o
        // carregador ou o cabo) no apoio e o meio dela no meio da vaga
        void Aplicar(long conteudo)
        {
            if (vagas == null) return;
            for (int i = 0; i < vagas.Length; i++)
            {
                var vaga = vagas[i];
                if (vaga == null || vaga.filtro == null) continue;
                var tipo = Ler(conteudo, i);
                var r = vaga.filtro.GetComponent<MeshRenderer>();
                r.enabled = tipo != null && malhas != null && (int)tipo.Value < malhas.Length;
                if (!r.enabled) continue;
                var malha = malhas[(int)tipo.Value];
                vaga.filtro.sharedMesh = malha;
                // a paleta (o material da montagem) ou o material com a textura própria da arma (a pistola nova)
                if (_paleta == null || _paleta.Length != vagas.Length) _paleta = new Material[vagas.Length];
                if (_paleta[i] == null) _paleta[i] = r.sharedMaterial;
                var proprio = Loot.Itens.MaterialProprio((int)tipo.Value);
                r.sharedMaterial = proprio != null ? proprio : _paleta[i];
                var b = malha.bounds;
                var t = vaga.filtro.transform;
                t.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                t.localPosition = new Vector3(-b.center.z, -b.min.y, b.max.x + 0.004f);
            }
        }
    }
}
