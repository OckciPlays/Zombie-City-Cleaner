using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// As colunas do F1 (13/11/2026). São quatro, lado a lado:
    /// <list type="number">
    /// <item>o menu e o vídeo;</item>
    /// <item>o painel do mundo (o Dia 0, o mundo sorteado ou o Lakewood);</item>
    /// <item>o Oco, o carro e o save;</item>
    /// <item>a aba do canto e a trilha.</item>
    /// </list>
    /// Cada coluna rola sozinha quando passa do fim da tela, com a roda do mouse em cima dela ou com a barrinha à direita.
    /// Antes, com o Lakewood, a fauna, o carro e o save, a parte de baixo saía da tela e o carro ficava embaixo do Oco.
    /// Dentro da coluna, os painéis desenham nas coordenadas da tela, como antes: a rolagem só desloca tudo.
    /// Também aqui: o botão cujo texto não cabe (os nomes dos lugares do Lakewood saíam cortados dos dois lados).
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        const int Colunas = 4;
        readonly Vector2[] _rolagemDaColuna = new Vector2[Colunas];
        // o que cada coluna desenhou no último evento, sem a rolagem (a largura e o fim dela valem para o próximo)
        readonly Rect[] _conteudoDaColuna = new Rect[Colunas];
        Rect _janelaDaColuna;      // a coluna aberta, na tela, com a barra
        float _barraDaColuna;      // a largura que a barra ocupa na coluna aberta (0 sem rolagem)
        int _colunaAberta = -1;
        float _pegaDoPolegar;      // onde o mouse pegou o polegar da barra (do topo dele)

        static float LarguraDaBarra(float u) => 10 * u;

        /// <summary>
        /// Abre a coluna <paramref name="i"/> em <paramref name="x"/>, do <paramref name="topo"/> até o fim da tela. A
        /// largura e a altura são as do que ela desenhou no evento anterior (a primeira vez, 300).
        /// </summary>
        void AbrirColuna(int i, float x, float topo, float u)
        {
            var e = Event.current;
            float fundo = Screen.height - 8 * u, visivel = Mathf.Max(fundo - topo, 40 * u);
            var antes = _conteudoDaColuna[i];
            float largura = antes.width > 0f ? antes.width : 300 * u;
            float total = Mathf.Max(antes.yMax - topo, 0f);
            bool rola = total > visivel + 0.5f;
            _barraDaColuna = rola ? LarguraDaBarra(u) + 4 * u : 0f;
            _janelaDaColuna = new Rect(x, topo, largura + _barraDaColuna, visivel);
            var rolagem = _rolagemDaColuna[i];
            // o número da barra sempre, com ou sem ela: os campos de texto embaixo não trocam de número no meio do quadro
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (rola)
            {
                // a roda do mouse em cima da coluna (uns 40 pontos por dente)
                if (e.type == EventType.ScrollWheel && _janelaDaColuna.Contains(e.mousePosition))
                {
                    rolagem.y += e.delta.y * 14f * u;
                    e.Use();
                }
                var trilho = new Rect(_janelaDaColuna.xMax - LarguraDaBarra(u), topo, LarguraDaBarra(u), visivel);
                rolagem.y = BarraDeRolagem(id, trilho, rolagem.y, visivel, total, u);
            }
            rolagem.x = 0f;
            rolagem.y = Mathf.Clamp(rolagem.y, 0f, Mathf.Max(total - visivel, 0f));
            _rolagemDaColuna[i] = rolagem;
            _colunaAberta = i;
            GUI.BeginScrollView(new Rect(x, topo, largura, visivel), rolagem, new Rect(x, topo, largura, Mathf.Max(total, visivel)),
                false, false, GUIStyle.none, GUIStyle.none);
        }

        /// <summary>
        /// Fecha a coluna aberta. <paramref name="conteudo"/> é o que ela desenhou (vazio: nada, e o próximo painel fica ao
        /// lado de <paramref name="anterior"/>). Devolve a coluna para o próximo painel ir ao lado (no xMax e no topo dela).
        /// </summary>
        Rect FecharColuna(Rect conteudo, Rect anterior)
        {
            GUI.EndScrollView(false);
            int i = _colunaAberta;
            _colunaAberta = -1;
            if (conteudo.width <= 0f)
            {
                _conteudoDaColuna[i] = default;
                return anterior;
            }
            var j = _janelaDaColuna;
            _conteudoDaColuna[i] = Rect.MinMaxRect(j.xMin, j.yMin, conteudo.xMax, conteudo.yMax);
            float direita = conteudo.xMax + _barraDaColuna;
            // a tela do inventário não pega os cliques no que se vê da coluna
            var naTela = Rect.MinMaxRect(j.xMin, j.yMin, direita, Mathf.Min(conteudo.yMax, j.yMax));
            Area = Area.width > 0f
                ? Rect.MinMaxRect(Mathf.Min(Area.xMin, naTela.xMin), Mathf.Min(Area.yMin, naTela.yMin), Mathf.Max(Area.xMax, naTela.xMax), Mathf.Max(Area.yMax, naTela.yMax))
                : naTela;
            return Rect.MinMaxRect(j.xMin, j.yMin, direita, conteudo.yMax);
        }

        /// <summary>
        /// A barrinha à direita da coluna, nas cores do menu. Arrastar o polegar rola; clicar no trilho leva o polegar até
        /// o clique.
        /// </summary>
        float BarraDeRolagem(int id, Rect trilho, float valor, float visivel, float total, float u)
        {
            var e = Event.current;
            float maximo = Mathf.Max(total - visivel, 1f);
            float altura = Mathf.Clamp(trilho.height * visivel / Mathf.Max(total, 1f), 30 * u, trilho.height);
            float curso = Mathf.Max(trilho.height - altura, 1f);
            var polegar = new Rect(trilho.x, trilho.y + curso * Mathf.Clamp01(valor / maximo), trilho.width, altura);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && trilho.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    _pegaDoPolegar = polegar.Contains(e.mousePosition) ? e.mousePosition.y - polegar.y : altura / 2f;
                    valor = Mathf.Clamp01((e.mousePosition.y - trilho.y - _pegaDoPolegar) / curso) * maximo;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    valor = Mathf.Clamp01((e.mousePosition.y - trilho.y - _pegaDoPolegar) / curso) * maximo;
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
                case EventType.Repaint:
                    var antes = GUI.color;
                    GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
                    GUI.DrawTexture(trilho, Texture2D.whiteTexture);
                    polegar.y = trilho.y + curso * Mathf.Clamp01(valor / maximo);
                    bool emCima = GUIUtility.hotControl == id || polegar.Contains(e.mousePosition);
                    GUI.color = emCima ? new Color(0.98f, 0.82f, 0.4f, 0.95f) : new Color(0.72f, 0.58f, 0.2f, 0.9f);
                    GUI.DrawTexture(new Rect(polegar.x + 2 * u, polegar.y + 2 * u, polegar.width - 4 * u, polegar.height - 4 * u), Texture2D.whiteTexture);
                    GUI.color = antes;
                    break;
            }
            return valor;
        }

        /// <summary>
        /// Para os testes pelo MCP: abre o F1 na aba <paramref name="aba"/> (de 0 a 6, na ordem das abas do canto) e põe
        /// todas as colunas na mesma altura da rolagem (de 0, o topo, a 1, o fim).
        /// </summary>
        public void MostrarDeTeste(int aba, float rolagem01)
        {
            if (!_aberto) Abrir(true);
            MostrarAba(aba);
            for (int i = 0; i < Colunas; i++)
            {
                var c = _conteudoDaColuna[i];
                float maximo = Mathf.Max(c.yMax - c.yMin - (Screen.height - 8 * _uDosEstilos - c.yMin), 0f);
                _rolagemDaColuna[i].y = rolagem01 >= 1f ? 1e6f : Mathf.Clamp01(rolagem01) * maximo;   // (o fim: a coluna corta no próximo evento)
            }
        }

        // ------------------------------------------------------------------ o botão cujo texto não cabe

        // a letra menor que cabe no botão, por tamanho (e em duas linhas, a menor de todas)
        readonly Dictionary<int, GUIStyle> _botoesQueCabem = new();
        static readonly GUIContent _textoDoBotao = new();

        /// <summary>
        /// O estilo do botão para <paramref name="texto"/> caber em <paramref name="largura"/>. A letra diminui até caber,
        /// até 9; se nem assim cabe, fica em 10 e quebra em duas linhas.
        /// </summary>
        GUIStyle BotaoQueCabe(string texto, float largura)
        {
            _textoDoBotao.text = texto;
            if (_botao.CalcSize(_textoDoBotao).x <= largura) return _botao;
            int menor = Mathf.Max(Mathf.RoundToInt(9 * _uDosEstilos), 8);
            for (int t = _botao.fontSize - 1; t >= menor; t--)
            {
                var s = BotaoDeLetra(t, false);
                if (s.CalcSize(_textoDoBotao).x <= largura) return s;
            }
            return BotaoDeLetra(Mathf.Max(Mathf.RoundToInt(10 * _uDosEstilos), 8), true);
        }

        GUIStyle BotaoDeLetra(int tamanho, bool duasLinhas)
        {
            int chave = tamanho * 2 + (duasLinhas ? 1 : 0);
            if (_botoesQueCabem.TryGetValue(chave, out var s)) return s;
            s = new GUIStyle(_botao) { fontSize = tamanho, wordWrap = duasLinhas };
            if (duasLinhas) s.padding = new RectOffset(s.padding.left, s.padding.right, 1, 1);
            _botoesQueCabem[chave] = s;
            return s;
        }
    }
}
