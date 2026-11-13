using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Moradores;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os pedidos do terminal do bunker (a parte 7 do Plano_NPCs_Bunker.md da pasta de arte, a <c>UI/TerminalDoBunker</c>):
    /// os dados da aba aberta (a cada segundo), a ficha de um NPC, a meta de um lugar e a cama de um morador. Só quem está
    /// no bunker pede. O anfitrião responde ao dono em pedaços (o transporte leva até uns 6 KB por mensagem).
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>O tamanho de cada pedaço da resposta (em caracteres: a rede manda 2 bytes por caractere).</summary>
        const int PedacoDoTerminal = 2000;
        int _serieDoTerminal;

        public void PedirTerminal(AbaDoTerminal aba) => TerminalRpc((byte)aba);
        public void PedirFicha(int id) => FichaDoTerminalRpc(id);
        public void PedirMeta(TipoDoEstoque tipo, int meta) => MetaDoTerminalRpc((byte)tipo, meta);
        public void PedirCama(int morador, int movel) => CamaDoTerminalRpc(morador, movel);

        // só quem está no bunker (no terminal da sala de operações)
        bool PodeUsarOTerminal(RpcParams parametros) =>
            parametros.Receive.SenderClientId == OwnerClientId && BunkerDoMundo.Dentro(transform.position);

        [Rpc(SendTo.Server)]
        void TerminalRpc(byte aba, RpcParams parametros = default)
        {
            if (!PodeUsarOTerminal(parametros)) return;
            var dados = TerminalNoAnfitriao.Montar((AbaDoTerminal)Mathf.Min(aba, (byte)AbaDoTerminal.Cartas));
            ResponderAoTerminal(TerminalDoBunker.Dados, JsonUtility.ToJson(dados));
        }

        [Rpc(SendTo.Server)]
        void FichaDoTerminalRpc(int id, RpcParams parametros = default)
        {
            if (!PodeUsarOTerminal(parametros)) return;
            string ficha = TerminalNoAnfitriao.Ficha(id);
            ResponderAoTerminal(TerminalDoBunker.Ficha, id + "\n" + (ficha ?? ""));
        }

        [Rpc(SendTo.Server)]
        void MetaDoTerminalRpc(byte tipo, int meta, RpcParams parametros = default)
        {
            if (!PodeUsarOTerminal(parametros)) return;
            MoradoresDoBunker.Instancia?.MudarMeta((TipoDoEstoque)tipo, meta, NomeNoMapa);
        }

        [Rpc(SendTo.Server)]
        void CamaDoTerminalRpc(int morador, int movel, RpcParams parametros = default)
        {
            if (!PodeUsarOTerminal(parametros)) return;
            MoradoresDoBunker.Instancia?.MudarCama(morador, movel, NomeNoMapa);
        }

        // no anfitrião: a resposta ao dono, em pedaços
        void ResponderAoTerminal(byte tipo, string texto)
        {
            int serie = ++_serieDoTerminal;
            int n = Mathf.Max(1, (texto.Length + PedacoDoTerminal - 1) / PedacoDoTerminal);
            for (int i = 0; i < n; i++)
            {
                int de = i * PedacoDoTerminal;
                PedacoDoTerminalRpc(tipo, serie, (ushort)i, (ushort)n, texto.Substring(de, Mathf.Min(PedacoDoTerminal, texto.Length - de)));
            }
        }

        [Rpc(SendTo.Owner)]
        void PedacoDoTerminalRpc(byte tipo, int serie, ushort i, ushort n, string pedaco) => TerminalDoBunker.Receber(tipo, serie, i, n, pedaco);
    }
}
