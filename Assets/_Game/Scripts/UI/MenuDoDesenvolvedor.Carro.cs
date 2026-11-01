using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste do carro (01/11/2026, com o desgaste e a gasolina: Carro.Desgaste.cs), embaixo do painel do Oco: o
    /// estado do carro mais perto (até 25 m; o carro em que se está, primeiro) e os botões que provocam cada falha (um furo
    /// lento, um estouro, os pneus gastos ou novos, o radiador vazando, a vela gasta, o motor novo, a reserva, o tanque cheio e o fim da carência,
    /// para a próxima falha sorteada poder vir já). Os pedidos vão para o anfitrião.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly (string nome, int acao)[] TestesDoCarro =
        {
            ("Furo lento", 0), ("Estourar um pneu", 1), ("Pneus a 30%", 2), ("Pneus novos", 9), ("Vazar o radiador", 3),
            ("Vela a 25%", 6), ("Motor novo", 8), ("Gasolina: 1 L", 4), ("Tanque cheio", 5), ("Sem carência", 7),
        };

        Rect PainelDoCarro(float u, Rect emCima)
        {
            var area = new Rect(emCima.x, emCima.yMax + 8 * u, emCima.width, 268 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "CARRO (TESTE)", _titulo);
            y += 24 * u;
            var c = CarroPerto(transform.position);
            if (c == null)
            {
                GUI.Label(new Rect(x, y, w, 40 * u), "Nenhum carro a menos de 25 m.", _texto);
                return area;
            }
            var p = c.Pneus;
            string pneus = "";
            for (int i = 0; i < PneusDoCarro.Quantas; i++)
            {
                string e = !p.Tem(i) ? "–" : p[i] + "%" + ((c.PneusMurchando & (1 << i)) != 0 ? "↓" : "");
                pneus += (i == 0 ? "" : i == 2 ? " · tr " : " ") + e;
            }
            var pc = c.Pecas;
            GUI.Label(new Rect(x, y, w, 54 * u),
                $"pneus: fr {pneus}\n" +
                $"gasolina: {c.Gasolina:0.0} de {c.Tanque:0} L\n" +
                $"vela {pc.Estado(PecasDoMotor.Vela)}% · radiador {pc.Estado(PecasDoMotor.Radiador)}%{(c.RadiadorVazando ? " (vaza)" : "")}" +
                (IsServer ? $" · carência {Mathf.Max(0f, c.Carencia):0} s" : ""), _texto);
            y += 58 * u;
            for (int k = 0; k < TestesDoCarro.Length; k++)
            {
                var r = new Rect(k % 2 == 0 ? x : x2, y + (k / 2) * 30 * u, meio, 26 * u);
                if (GUI.Button(r, TestesDoCarro[k].nome, _botao)) TesteDoCarroRpc((byte)TestesDoCarro[k].acao);
            }
            return area;
        }

        /// <summary>O carro em que o jogador está, ou o mais perto (até 25 m).</summary>
        static Carro CarroPerto(Vector3 de)
        {
            Carro melhor = null;
            float menor = 25f * 25f;
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.IsSpawned) continue;
                float d2 = (c.transform.position - de).sqrMagnitude;
                if (d2 < menor)
                {
                    menor = d2;
                    melhor = c;
                }
            }
            return melhor;
        }

        [Rpc(SendTo.Server)]
        void TesteDoCarroRpc(byte acao, RpcParams parametros = default)
        {
            if (!Aceita(parametros)) return;
            var c = CarroPerto(transform.position);
            string feito = c != null ? c.Teste(acao) : "Nenhum carro a menos de 25 m";
            if (!string.IsNullOrEmpty(feito)) AvisoDoCarroRpc(feito);
        }

        [Rpc(SendTo.Owner)]
        void AvisoDoCarroRpc(string texto) => Avisar(texto);
    }
}
