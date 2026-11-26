using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O tutorial do Gus na rede (25/11/2026, a etapa 6: <see cref="ConversaDoGus"/> e LakewoodNaCena.Tutorial.cs): o machado e
    /// a mochila que ele dá, e a conversa que o segura parado.
    /// </summary>
    public partial class MundoDeLakewood
    {
        /// <summary>Qualquer um (a conversa com o Gus): ele dá o machado (e a mochila pequena, a quem não tem) a quem pediu, uma vez.</summary>
        [Rpc(SendTo.Server)]
        public void PedirOMachadoRpc(RpcParams parametros = default)
        {
            if (Fase != FaseDaCampanha.OBunker) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var lw = LakewoodNaCena.Instancia;
            if (lw != null) lw.DarOMachado(cliente.PlayerObject.GetComponent<Player.JogadorRede>());
        }

        /// <summary>Quem conversa com o Gus: a tela abriu (true) ou fechou. O anfitrião o segura parado, olhando para quem conversa.</summary>
        [Rpc(SendTo.Server)]
        public void ConversaComOGusRpc(bool comecou, RpcParams parametros = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var lw = LakewoodNaCena.Instancia;
            if (lw != null) lw.ConversaComOGus(cliente.PlayerObject.GetComponent<Player.JogadorRede>(), comecou);
        }
    }
}
