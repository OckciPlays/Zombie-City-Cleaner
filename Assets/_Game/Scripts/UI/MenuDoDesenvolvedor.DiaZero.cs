using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel do Dia 0 no F1 (01/11/2026, a pedido: "o F1 pode ter o botão de pular o D0"), no lugar do painel do mundo
    /// enquanto o Dia 0 dura: o dinheiro, pular o Dia 0 (todos vão para o mundo com as compras, onde estiverem), mais
    /// 1.000 dólares e ir até a nossa casa ou até cada loja (só nesta máquina).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        Rect PainelDoDiaZero(float u, Rect menu)
        {
            var dia = DiaZeroDaPartida.Instancia;
            var rua = RuaDoDiaZero.Instancia;
            if (!DiaZeroDaPartida.NoDia || dia == null || rua == null) return default;
            var area = new Rect(menu.xMax + 8 * u, menu.y, 280 * u, 456 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "DIA 0", _titulo);
            y += 26 * u;
            ulong eu = NetworkManager.LocalClientId;
            GUI.Label(new Rect(x, y, w, 40 * u), $"Dinheiro: {PrecosDoDiaZero.Dinheiro(dia.Dinheiro(eu))} (gastou {PrecosDoDiaZero.Dinheiro(dia.Gasto(eu))})\n" +
                                                 $"{dia.Compras(eu)} compras em casa · {rua.Npcs.Count} NPCs", _texto);
            y += 46 * u;
            if (GUI.Button(new Rect(x, y, w, 30 * u), "Pular o Dia 0 (ir para o mundo)", _botao))
            {
                dia.PedirPular();
                Avisar("Pulando o Dia 0: todos vão para o mundo, com as compras.");
            }
            y += 36 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), "+ $ 1.000", _botao))
            {
                dia.PedirMaisDinheiro();
                Avisar("Mais 1.000 dólares.");
            }
            y += 36 * u;
            GUI.enabled = dia.Fase != FaseDoDia.Rua;
            if (GUI.Button(new Rect(x, y, w, 28 * u), dia.Fase == FaseDoDia.Rua ? "A rua já está liberada" : "Liberar a rua (sem a TV)", _botao))
            {
                dia.PedirLiberarARua();
                Avisar("A rua foi liberada.");
            }
            GUI.enabled = true;
            y += 36 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Ir para", _titulo);
            y += 24 * u;
            float meio = (w - 6 * u) / 2f;
            int k = 0;
            void Ir(string nome, Vector3 ponto, float giro)
            {
                var r = new Rect(k % 2 == 0 ? x : x + meio + 6 * u, y + (k / 2) * 30 * u, meio, 26 * u);
                k++;
                if (!GUI.Button(r, nome, _botao)) return;
                var mov = GetComponent<MovimentoPrimeiraPessoa>();
                if (mov != null) mov.Teleportar(ponto + Vector3.up * 0.1f, giro);
            }
            Ir("Nossa casa", rua.Nascimentos[0], rua.GirosDosNascimentos[0]);
            foreach (var loja in rua.Lojas)
            {
                // na calçada, de frente para a porta da loja
                var l = loja.transform;
                var frente = l.TransformPoint(new Vector3(0f, 0f, -loja.Area.extents.z - 2.5f)) ;
                if (loja.Tipo == LojaDoDia.Posto) frente = l.TransformPoint(new Vector3(0f, 0f, 6f));
                var olhar = l.position - frente;
                Ir(loja.Nome, new Vector3(frente.x, 0.1f, frente.z), Mathf.Atan2(olhar.x, olhar.z) * Mathf.Rad2Deg);
            }
            return area;
        }
    }
}
