using Unity.Netcode;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Sentado no chão perto da fogueira (18/11/2026, a etapa 1 da fogueira, a pedido: "um sistema de sentar em volta da
    /// fogueira pra descansar sem dormir, recupera um pouco de sanidade ... daria para fumar, assar sentado"). O dono decide
    /// (<see cref="SentarNoFogo"/>); a pose é a do PassageiroDoCarro (PassageiroDoCarro.NoChao.cs), em todas as máquinas, e a
    /// mente sobe no anfitrião (VidaJogador.Mente.cs).
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>Sentado no chão (do dono).</summary>
        public readonly NetworkVariable<bool> SentadoNoChao = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    }
}
