using Unity.Netcode;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O saque dos recipientes (24/11/2026, a pedido: "o correto seria aparecer uma tela de inventário da lixeira para ver os
    /// itens que há dentro"): o anfitrião cria o recipiente (MochilasNoChao.CriarRecipiente) e manda a tela abrir na máquina de
    /// quem revirou.
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>Só no anfitrião: abre a tela do saque do recipiente na máquina do dono.</summary>
        public void AbrirSaque(ushort recipiente)
        {
            if (!IsServer || recipiente == 0) return;
            if (IsOwner) TelaDoSaque.Abrir(recipiente);
            else AbrirSaqueRpc(recipiente);
        }

        [Rpc(SendTo.Owner)]
        void AbrirSaqueRpc(ushort recipiente) => TelaDoSaque.Abrir(recipiente);
    }
}
