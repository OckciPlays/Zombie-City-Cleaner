using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O suporte de armas na parede do bunker: 4 vagas, cada uma com uma arma deitada na prateleira. Quem mira uma arma
    /// de perto e aperta E troca com a da mão (a da mão fica no lugar). Quem manda é o anfitrião: o pedido vai para ele,
    /// que confere a distância e troca as duas (a vaga e a <see cref="JogadorRede.Arma"/> do jogador).
    /// O conteúdo das vagas vai pela rede num número só, 8 bits por vaga (0 = vazia, senão o tipo + 1).
    /// </summary>
    public class ParedeDeArmas : NetworkBehaviour
    {
        [Tooltip("As vagas, de baixo para cima.")]
        public VagaDeArma[] vagas;
        [Tooltip("A malha de cada arma, na ordem de TipoDeArma (origem no cabo, cano em +Z, topo em +Y).")]
        public Mesh[] malhas;
        [Tooltip("O que cada vaga tem no começo da partida.")]
        public TipoDeArma[] inicio = { TipoDeArma.Pistola, TipoDeArma.Submetralhadora, TipoDeArma.Rifle, TipoDeArma.Espingarda };
        [Tooltip("Até onde o anfitrião aceita o pedido de troca (m, dos pés do jogador ao meio da vaga).")]
        public float distanciaMaxima = 3f;

        readonly NetworkVariable<int> _conteudo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        void AoMudar(int antes, int depois) => Aplicar(depois);

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
            _conteudo.Value = Gravar(_conteudo.Value, indice, naMao);
            jogador.Arma.Value = (byte)naVaga.Value;
        }

        static int Codificar(TipoDeArma[] armas)
        {
            int v = 0;
            for (int i = 0; i < armas.Length && i < 4; i++) v = Gravar(v, i, armas[i]);
            return v;
        }

        static int Gravar(int conteudo, int indice, TipoDeArma arma) => (conteudo & ~(0xFF << (8 * indice))) | (((int)arma + 1) << (8 * indice));

        static TipoDeArma? Ler(int conteudo, int indice)
        {
            int v = (conteudo >> (8 * indice)) & 0xFF;
            return v == 0 ? null : (TipoDeArma)(v - 1);
        }

        // cada arma deitada na prateleira da vaga: o cano ao longo da parede, o lado esquerdo para fora, a parte de baixo
        // (o carregador ou o cabo) encostada na prateleira e o meio dela no meio da vaga
        void Aplicar(int conteudo)
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
