using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O painel de armas do salão do bunker: cada arma tem o seu lugar (as 4 longas e 4 pistolas), em pé, com o lado
    /// esquerdo para fora. Quem mira uma arma de perto e aperta E pega ela, e a da mão volta para uma vaga livre do tipo
    /// dela (se não houver, fica no lugar da que foi pega). Quem manda é o anfitrião: o pedido vai para ele, que confere a
    /// distância e troca (as vagas e a <see cref="JogadorRede.Arma"/> do jogador). Quem sai da partida devolve a arma.
    /// O conteúdo das vagas vai pela rede num número só, 8 bits por vaga (0 = vazia, senão o tipo + 1), até 8 vagas.
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
            if (IsServer) _conteudo.Value = Codificar(inicio);
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

        /// <summary>O jogador local pede para trocar a arma da mão pela da vaga.</summary>
        public void PedirTroca(int indice)
        {
            if (IsSpawned && indice >= 0 && indice < vagas.Length) TrocarRpc((byte)indice);
        }

        [Rpc(SendTo.Server)]
        void TrocarRpc(byte indice, RpcParams parametros = default)
        {
            if (indice >= vagas.Length) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var jogador = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (jogador == null || Vector3.Distance(jogador.transform.position, vagas[indice].transform.position) > distanciaMaxima) return;
            var naVaga = Ler(_conteudo.Value, indice);
            var naMao = (TipoDeArma)jogador.Arma.Value;
            if (naVaga == null || naVaga.Value == naMao) return;
            long c = Gravar(_conteudo.Value, indice, null);
            int casa = VagaLivre(c, naMao);
            _conteudo.Value = Gravar(c, casa >= 0 ? casa : indice, naMao);
            jogador.Arma.Value = (byte)naVaga.Value;
        }

        /// <summary>No anfitrião: a arma de quem saiu da partida volta para uma vaga livre do tipo dela (se houver).</summary>
        public void Devolver(TipoDeArma arma)
        {
            if (!IsSpawned || !IsServer) return;
            int casa = VagaLivre(_conteudo.Value, arma);
            if (casa >= 0) _conteudo.Value = Gravar(_conteudo.Value, casa, arma);
        }

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
                var b = malha.bounds;
                var t = vaga.filtro.transform;
                t.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                t.localPosition = new Vector3(-b.center.z, -b.min.y, b.max.x + 0.004f);
            }
        }
    }
}
