using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Salvar;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel do save no F1 (02/11/2026), embaixo do do carro: como este mundo salva e quando salvou, o dinheiro do
    /// personagem, "Salvar agora" (no anfitrião, em qualquer modo) e "+ $ 1.000".
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        Rect PainelDoSave(float u, Rect emCima)
        {
            var s = Salvamento.Instancia;
            var rede = GetComponent<JogadorRede>();
            if (s == null || !s.IsSpawned || rede == null || DiaZeroDaPartida.NoDia) return default;
            var area = new Rect(emCima.x, emCima.yMax + 8 * u, emCima.width, 118 * u);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            float meio = (w - 6 * u) / 2f;
            GUI.Label(new Rect(x, y, w, 22 * u), "SAVE", _titulo);
            y += 24 * u;
            string estado = !s.Salva ? "Esta partida não salva (não veio do menu)."
                : $"{MundoSalvo.NomeDoModo(s.Modo)}" + (s.SalvouEm >= 0f ? $" · salvo há {Mathf.FloorToInt((Time.unscaledTime - s.SalvouEm) / 60f)} min" : " · ainda não salvou");
            var p = EscolhaDoPersonagem.Atual;
            GUI.Label(new Rect(x, y, w, 36 * u), $"{estado}\n{(p != null ? p.nome : "sem personagem salvo")} · {PrecosDoDiaZero.Dinheiro(rede.Inventario.Dinheiro.Value)}", _texto);
            y += 40 * u;
            GUI.enabled = s.Salva && s.IsServer;
            if (GUI.Button(new Rect(x, y, meio, 28 * u), "Salvar agora", _botao))
                Avisar(s.Salvar("F1") ? "Salvo." : "Agora não dá para salvar.");
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + meio + 6 * u, y, meio, 28 * u), "+ $ 1.000", _botao))
            {
                rede.Inventario.PedirMaisDinheiro();
                Avisar("Mais 1.000 dólares.");
            }
            return area;
        }
    }
}
