using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Fauna;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A fauna (12/11/2026): o tiro ou o golpe do dono num bicho e o pedido de carnear a carcaça vão ao anfitrião, que
    /// confere (Fauna/FaunaDoMundo.cs). Os bichos não são NetworkObjects: vão pelo Id e pela geração.
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>O dono acertou um bicho (o tiro ou o golpe): o anfitrião tira a vida (a cabeça vale mais).</summary>
        public void AcertarBicho(Bicho b, float dano, Vector3 ponto, bool cabeca, bool tiro)
        {
            if (b == null || b.Morto) return;
            if (IsServer) FaunaDoMundo.Instancia?.Acertar(b.Id, b.Geracao, dano, ponto, cabeca, tiro, this);
            else AcertoNoBichoRpc(b.Id, b.Geracao, dano, ponto, cabeca, tiro);
        }

        [Rpc(SendTo.Server)]
        void AcertoNoBichoRpc(ushort id, byte geracao, float dano, Vector3 ponto, bool cabeca, bool tiro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            FaunaDoMundo.Instancia?.Acertar(id, geracao, Mathf.Clamp(dano, 0f, 1000f), ponto, cabeca, tiro, this);
        }

        /// <summary>O dono segurou E na carcaça com a faca na mão: o anfitrião dá a carne, o couro e a gordura.</summary>
        public void PedirCarnear(Bicho b)
        {
            if (b == null || !b.Morto) return;
            if (IsServer) FaunaDoMundo.Instancia?.Carnear(b.Id, b.Geracao, this);
            else CarnearRpc(b.Id, b.Geracao);
        }

        [Rpc(SendTo.Server)]
        void CarnearRpc(ushort id, byte geracao, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            FaunaDoMundo.Instancia?.Carnear(id, geracao, this);
        }

        // ------------------------------------------------------------------ o cheiro do gambá (a etapa 4, 12/11/2026)

        /// <summary>
        /// O cheiro do gambá: os segundos que faltam (0: limpo). Dura <see cref="SegundosDoFedor"/> s (10 minutos reais;
        /// o relógio do jogo corre 1 hora por minuto); na água sai 6 vezes mais rápido. Enquanto dura, os zumbis sentem de
        /// longe (a cada 6 s, o cheiro chama os de até 70 m: ZombieManager.Cheiro) e o símbolo "Fedendo" aparece.
        /// </summary>
        public readonly NetworkVariable<float> Fedor = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public const float SegundosDoFedor = 600f, CheiroAlcance = 70f;
        float _proximoCheiro;

        /// <summary>Só no anfitrião: o espirro do gambá pegou.</summary>
        public void ReceberFedor()
        {
            if (!IsServer || !IsSpawned) return;
            bool novo = Fedor.Value <= 0f;
            Fedor.Value = SegundosDoFedor;
            _proximoCheiro = Time.time + 2f;
            if (novo && Inventario != null) Inventario.AvisarDaFauna("O gambá espirrou em você: o cheiro chama os zumbis de longe (na água sai mais rápido)");
        }

        /// <summary>No anfitrião, a cada quadro (a fauna chama): o cheiro passa (na água, depressa) e chama os zumbis.</summary>
        public void CorrerFedor(float dt)
        {
            if (!IsServer || !IsSpawned || Fedor.Value <= 0f) return;
            bool naAgua = Agua.Fundura(transform.position) > 0.6f;
            Fedor.Value = Mathf.Max(0f, Fedor.Value - dt * (naAgua ? 6f : 1f));
            if (Fedor.Value <= 0f)
            {
                if (Inventario != null) Inventario.AvisarDaFauna("O cheiro do gambá passou");
                return;
            }
            if (Time.time < _proximoCheiro) return;
            _proximoCheiro = Time.time + 6f;
            Zombies.ZombieManager.Cheiro(transform.position, CheiroAlcance);
        }

        /// <summary>
        /// Só no anfitrião: o golpe de um bicho empurra o jogador (a patada do urso joga longe; a mordida do lobo só puxa)
        /// e sacode a câmera. O movimento é do dono: vai para a máquina dele.
        /// </summary>
        public void EmpurraoDoBicho(Vector3 velocidade, float tremor)
        {
            if (!IsServer) return;
            EmpurraoDoBichoRpc(velocidade, Mathf.Clamp01(tremor));
        }

        [Rpc(SendTo.Owner)]
        void EmpurraoDoBichoRpc(Vector3 velocidade, float tremor)
        {
            var m = GetComponent<MovimentoPrimeiraPessoa>();
            if (m == null) return;
            if (velocidade.sqrMagnitude > 0.01f) m.Arremessar(velocidade);
            m.Tremer(tremor);
        }
    }
}
