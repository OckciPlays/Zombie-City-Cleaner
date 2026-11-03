using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O painel de teste do clima (02/11/2026, World/CicloDoDia.Tempo.cs e World/ClimaNaTela.cs), no lugar do painel dos
    /// zumbis, ao lado do Oco (as abas "Zumbis" e "Clima" no canto de cima). Embaixo do Oco ou do carro ele passava da
    /// altura da tela e os botões não apareciam. Mostra o tempo agora e o próximo evento do calendário e onde quem joga
    /// está (fora, dentro, no carro, debaixo da terra). Os botões forçam cada nível, chamam o tornado, derrubam um raio
    /// perto ou longe e enchem as poças; os pedidos vão para o anfitrião (quem manda no tempo).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        static readonly (string nome, int acao)[] TestesDoClima =
        {
            ("Sol", 0), ("Chuvisco", 1), ("Chuva", 2), ("Tempestade", 3), ("Com raios", 4), ("Calendário", 5),
            ("Tornado agora", 6), ("Acabar o tornado", 7), ("Raio perto", 8), ("Raio longe", 9), ("Só o clarão", 12), ("Encher as poças", 10),
            ("Próximo evento", 11),
        };

        bool _abaDoClima;
        static readonly GUIContent _conteudoDoClima = new GUIContent();

        // as abas no canto de cima do painel (o dos zumbis ou o do clima)
        void AbasDosZumbisEDoClima(float u, Rect painel)
        {
            float w = 50 * u, h = 20 * u, y = painel.y + 6 * u;
            var antes = GUI.color;
            GUI.color = _abaDoClima ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            if (GUI.Button(new Rect(painel.xMax - 2 * w - 14 * u, y, w, h), "Zumbis", _botao)) MostrarAba(0);
            GUI.color = _abaDoClima ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            if (GUI.Button(new Rect(painel.xMax - w - 10 * u, y, w, h), "Clima", _botao)) MostrarAba(1);
            GUI.color = antes;
        }

        // 0: os zumbis; 1: o clima; 2: os sobreviventes (MenuDoDesenvolvedor.Sobreviventes.cs)
        void MostrarAba(int qual)
        {
            _abaDoClima = qual == 1;
            _abaDosSobreviventes = qual == 2;
        }

        Rect PainelDoClima(float u, Rect aoLado)
        {
            float x0 = aoLado.xMax + 8 * u, largura = 250 * u;
            float x = x0 + 12 * u, w = largura - 24 * u;
            float meio = (w - 6 * u) / 2f, x2 = x + meio + 6 * u;
            // o texto primeiro: a altura do painel acompanha (as linhas do tornado e do próximo evento vêm e vão)
            var ciclo = CicloDoDia.Instancia;
            string texto = ciclo != null ? ciclo.ResumoDoTempo() : "sem o ciclo do dia nesta cena";
            if (ClimaNaTela.Instancia != null) texto += "\n" + ClimaNaTela.Instancia.Resumo();
            _conteudoDoClima.text = texto;
            float alturaDoTexto = _texto.CalcHeight(_conteudoDoClima, w);
            int linhas = (TestesDoClima.Length + 1) / 2;
            float altura = 8 * u + 28 * u + alturaDoTexto + 8 * u + linhas * 30 * u + 6 * u;
            var area = new Rect(x0, aoLado.y, largura, altura);

            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.08f, 0.09f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.55f, 0.75f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "CLIMA", _titulo);
            y += 28 * u;
            GUI.Label(new Rect(x, y, w, alturaDoTexto), _conteudoDoClima, _texto);
            y += alturaDoTexto + 8 * u;
            for (int k = 0; k < TestesDoClima.Length; k++)
            {
                var r = new Rect(k % 2 == 0 ? x : x2, y + (k / 2) * 30 * u, meio, 26 * u);
                if (GUI.Button(r, TestesDoClima[k].nome, _botao)) TesteDoClimaRpc((byte)TestesDoClima[k].acao);
            }
            return area;
        }

        [Rpc(SendTo.Server)]
        void TesteDoClimaRpc(byte acao)
        {
            var ciclo = CicloDoDia.Instancia;
            if (ciclo == null) return;
            switch (acao)
            {
                case <= 4: ciclo.ForcarTempo(acao); break;
                case 5: ciclo.ForcarTempo(-1); break;
                case 6:
                    if (ciclo.TempoForcado < (int)NivelDoTempo.Raios) ciclo.ForcarTempo((int)NivelDoTempo.Raios);
                    ciclo.ComecarTornado(300f);
                    break;
                case 7: ciclo.AcabarTornado(); break;
                case 8: ciclo.RaioPerto(); break;
                case 9: ciclo.RaioLonge(); break;
                case 10: ciclo.EncherPocas(); break;
                case 11: ciclo.ProximoEventoAgora(); break;
                case 12: ciclo.RaioLonge(true); break;
            }
        }
    }
}
