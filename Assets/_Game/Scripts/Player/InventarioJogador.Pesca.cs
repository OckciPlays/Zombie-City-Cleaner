using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pesca no anfitrião (06/11/2026, <see cref="PescaJogador"/>): o dono conta o lance, a mordida e a puxada, e manda
    /// o resultado; o anfitrião confere (a vara e a isca na mochila, água funda perto, um peixe a cada 3 s no máximo), gasta
    /// a isca (a Isca fina às vezes poupa: o dono decide), põe o peixe na mochila (sem lugar, aos pés) e dá o XP.
    /// </summary>
    public partial class InventarioJogador
    {
        float _ultimaPesca = -99f;

        /// <summary>No dono: o peixe pego (Nada: escapou) e se a isca foi gasta.</summary>
        public void PegouOPeixe(Item peixe, bool gastouAIsca)
        {
            if (!IsOwner || !IsSpawned) return;
            PescaRpc((byte)peixe, gastouAIsca);
        }

        [Rpc(SendTo.Server)]
        void PescaRpc(byte peixe, bool gastouAIsca, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var item = (Item)peixe;
            bool ehPeixe = item >= Item.PeixePequeno && item <= Item.PeixeDourado;
            if (item != Item.Nada && !ehPeixe) return;
            if (Quantos(Item.VaraDePesca) <= 0 || Quantos(Item.Isca) <= 0) return;
            if (Time.time - _ultimaPesca < 3f || !AguaPerto(transform.position)) return;
            _ultimaPesca = Time.time;
            if (gastouAIsca) GastarItem(Item.Isca, 1);
            if (!ehPeixe) return;
            bool cabe = Itens.EmPilha(item) ? ReceberEmPilha(item, 1) < 1 : Receber(item, -1, 0);
            if (!cabe) ItensNoMundo.Instancia?.Criar(item, transform.position + transform.forward * 0.6f, transform.eulerAngles.y + 30f, 1);
            AvisoRpc($"Pescou {Itens.NomeComArtigo(item)}!" + (cabe ? "" : " (sem lugar: ficou aos seus pés)"));
            GanharXp(item == Item.PeixeDourado ? Habilidades.XpDoDourado : Habilidades.XpDaPesca, "pesca");
        }

        // água funda (pelo menos 0,8 m) a até 22 m: na margem ou no caiaque
        static bool AguaPerto(Vector3 p)
        {
            if (Agua.Fundura(p) >= PescaJogador.FundoMinimo) return true;
            for (float d = 2f; d <= PescaJogador.AlcanceDoLance + 4f; d += 2f)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                    if (Agua.Fundura(q) >= PescaJogador.FundoMinimo) return true;
                }
            return false;
        }
    }
}
