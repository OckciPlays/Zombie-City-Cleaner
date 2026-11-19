using Unity.Netcode;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O salmão preso na lança (18/11/2026, a etapa 3 da fogueira, a pedido: "quando a gente espetar o salmão com a lança
    /// ele ficaria preso na lança se batendo ... depois a gente conseguiria pegar ele como um peixe normal"). O golpe da lança
    /// que mata um salmão (o anfitrião confere, depois do acerto: JogadorRede.Fauna.cs) tira o bicho da água e põe o peixe na
    /// ponta da lança (<see cref="SalmaoNaLanca"/>, que todos veem e que se debate). O clique, com ele ali, tira e guarda na
    /// mochila (o item <see cref="Item.Salmao"/>); trocar de item na mão também guarda (o que não couber cai aos pés).
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>Há um salmão preso na ponta da lança (do anfitrião).</summary>
        public readonly NetworkVariable<bool> SalmaoNaLanca = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // no anfitrião, depois do acerto num bicho: o golpe da lança que matou um salmão o prende na ponta dela
        void SalmaoNaPontaDaLanca(ushort id, byte geracao, bool tiro)
        {
            if (!IsServer || tiro || SalmaoNaLanca.Value || Inventario == null || Inventario.NaMao != Item.Lanca) return;
            var f = FaunaDoMundo.Instancia;
            var b = f != null ? f.BichoPorId(id, geracao) : null;
            if (b == null || !b.Morto || b.Especie != Especie.Salmao) return;
            f.Sumir(b);
            SalmaoNaLanca.Value = true;
            Inventario.AvisarDono("Salmão na lança!   ·   [clique] Tirar e guardar");
        }

        /// <summary>O dono tira o salmão da lança (o clique): vai para a mochila.</summary>
        public void PedirTirarSalmao()
        {
            if (IsServer) TirarOSalmao();
            else TirarSalmaoRpc();
        }

        [Rpc(SendTo.Server)]
        void TirarSalmaoRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            TirarOSalmao();
        }

        /// <summary>Só no anfitrião: o salmão sai da lança para a mochila (o que não couber cai aos pés).</summary>
        internal void TirarOSalmao()
        {
            if (!IsServer || !SalmaoNaLanca.Value) return;
            SalmaoNaLanca.Value = false;
            Inventario?.EntregarItem(Item.Salmao, 1);
        }
    }
}
