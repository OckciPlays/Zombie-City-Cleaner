using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os símbolos de estado e os medidores redondos no HUD (12/11/2026, a pedido):
    ///  - os medidores (a mente, a fome e a sede) ficam lado a lado, em cima do monitor cardíaco (a vida): "um símbolo igual da
    ///    sanidade para a fome e a sede, redondo ao invés de barra horizontal, ai deixaria todos do lado em cima da barra de
    ///    vida" (<see cref="AreaDoMedidor"/>; a mente desenha o cérebro, a FomeESede o estômago e a gota);
    ///  - os símbolos dos estados (<see cref="EstadosDoJogador"/>) ficam "em outro lugar": no canto de baixo à direita, em
    ///    cima da arma, da direita para a esquerda, e ficam lá até o problema se resolver. O que acabou de aparecer pulsa um
    ///    pouco. Com a mochila aberta eles saem daqui e vão para a aba CORPO (<see cref="Fileira"/>), onde o mouse em cima
    ///    diz o nome e o porquê.
    /// </summary>
    public static class EstadosNoHud
    {
        /// <summary>O lado de cada medidor e o vão entre eles (em unidades de 720 p).</summary>
        public const float LadoDoMedidor = 42f, VaoDoMedidor = 12f;
        /// <summary>O lado de um símbolo no HUD e o vão (em unidades de 720 p).</summary>
        public const float LadoNoHud = 40f, VaoNoHud = 6f;
        const int PorFileira = 6;
        const float TempoDoPulso = 1.4f;

        /// <summary>O lugar do medidor (0 a mente, 1 a fome, 2 a sede): lado a lado, em cima do monitor cardíaco.</summary>
        public static Rect AreaDoMedidor(int i, float u)
        {
            var monitor = MonitorCardiaco.Area(u);
            return new Rect(monitor.x + i * (LadoDoMedidor + VaoDoMedidor) * u, monitor.y - (LadoDoMedidor + 6f) * u, LadoDoMedidor * u, LadoDoMedidor * u);
        }

        /// <summary>O mouse está solto (a mochila, o menu): dá para passar em cima e ler.</summary>
        public static bool MouseSolto => Cursor.lockState != CursorLockMode.Locked;

        // ------------------------------------------------------------------ a lista (refeita a cada 0,2 s)

        static readonly List<EstadosDoJogador.Estado> _lista = new List<EstadosDoJogador.Estado>();
        static readonly Dictionary<EstadoDoJogador, float> _desde = new Dictionary<EstadoDoJogador, float>();
        static readonly HashSet<EstadoDoJogador> _agora = new HashSet<EstadoDoJogador>();
        static VidaJogador _de;
        static float _proxima;

        /// <summary>Os estados de agora do jogador local (o dono).</summary>
        public static IReadOnlyList<EstadosDoJogador.Estado> Atuais(VidaJogador v)
        {
            if (v != _de || Time.unscaledTime >= _proxima)
            {
                _de = v;
                _proxima = Time.unscaledTime + 0.2f;
                EstadosDoJogador.Montar(v, _lista);
                // quando cada um apareceu (o pulso)
                _agora.Clear();
                foreach (var e in _lista)
                {
                    _agora.Add(e.id);
                    if (!_desde.ContainsKey(e.id)) _desde[e.id] = Time.unscaledTime;
                }
                _sumiram.Clear();
                foreach (var k in _desde.Keys) if (!_agora.Contains(k)) _sumiram.Add(k);
                foreach (var k in _sumiram) _desde.Remove(k);
            }
            return _lista;
        }

        static readonly List<EstadoDoJogador> _sumiram = new List<EstadoDoJogador>();

        // ------------------------------------------------------------------ o HUD

        /// <summary>Os símbolos no canto de baixo à direita (só no jogo, sem a mochila aberta).</summary>
        public static void NoHud(VidaJogador v, float u)
        {
            var lista = Atuais(v);
            if (lista.Count == 0) return;
            float lado = LadoNoHud * u, vao = VaoNoHud * u;
            // em cima da arma e dos avisos dela (que sobem até ~122u do fundo), da direita para a esquerda
            float direita = Screen.width - 20f * u, baixo = Screen.height - 132f * u;
            Vector2 mouse = Event.current.mousePosition;
            EstadosDoJogador.Estado? sob = null;
            Rect rSob = default;
            for (int i = 0; i < lista.Count; i++)
            {
                int col = i % PorFileira, lin = i / PorFileira;
                var r = new Rect(direita - (col + 1) * lado - col * vao, baixo - (lin + 1) * lado - lin * vao, lado, lado);
                Icone(r, lista[i], Pulso(lista[i].id));
                if (MouseSolto && r.Contains(mouse))
                {
                    sob = lista[i];
                    rSob = r;
                }
            }
            if (sob != null) Dica(rSob, sob.Value.nome, sob.Value.porque, u);
        }

        /// <summary>
        /// Uma fileira de símbolos (a aba CORPO), da esquerda para a direita, dentro da área; o que está sob o mouse volta em
        /// <paramref name="sob"/> (para a tela mostrar o nome e o porquê).
        /// </summary>
        public static bool Fileira(VidaJogador v, Rect area, float lado, float vao, Vector2 mouse, out EstadosDoJogador.Estado sob)
        {
            sob = default;
            bool achou = false;
            var lista = Atuais(v);
            float x = area.x;
            foreach (var e in lista)
            {
                if (x + lado > area.xMax + 0.5f) break;
                var r = new Rect(x, area.y + (area.height - lado) / 2f, lado, lado);
                Icone(r, e, 0f);
                if (r.Contains(mouse))
                {
                    sob = e;
                    achou = true;
                }
                x += lado + vao;
            }
            return achou;
        }

        // o pulso do que acabou de aparecer (0 a 1)
        static float Pulso(EstadoDoJogador id)
        {
            if (!_desde.TryGetValue(id, out var t)) return 0f;
            float d = Time.unscaledTime - t;
            if (d > TempoDoPulso) return 0f;
            return (0.5f - 0.5f * Mathf.Cos(d / TempoDoPulso * Mathf.PI * 6f)) * (1f - d / TempoDoPulso);
        }

        /// <summary>Um símbolo: o ícone (o rosto do personagem); sem ele ainda, um disco escuro com as letras.</summary>
        public static void Icone(Rect r, EstadosDoJogador.Estado e, float pulso)
        {
            if (pulso > 0f)
            {
                float k = 1f + 0.18f * pulso;
                r = new Rect(r.center.x - r.width * k / 2f, r.center.y - r.height * k / 2f, r.width * k, r.height * k);
            }
            var antes = GUI.color;
            var tex = EstadosDoJogador.Icone(e.id);
            if (tex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.color = e.bom ? new Color(0.12f, 0.24f, 0.16f, 0.9f) : new Color(0.1f, 0.1f, 0.1f, 0.88f);
                GUI.DrawTexture(r, Disco);
                var estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.Max(8, Mathf.RoundToInt(r.height * 0.3f)), FontStyle.Bold, TextAnchor.MiddleCenter));
                estilo.normal.textColor = e.bom ? new Color(0.7f, 0.95f, 0.7f) : new Color(0.95f, 0.8f, 0.6f);
                GUI.Label(r, e.nome.Length > 3 ? e.nome.Substring(0, 3).ToUpperInvariant() : e.nome.ToUpperInvariant(), estilo);
            }
            GUI.color = antes;
        }

        static Texture2D _disco;
        static Texture2D Disco
        {
            get
            {
                if (_disco != null) return _disco;
                const int n = 64;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - 1f - d) * 255f));
                    }
                _disco = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Disco do estado" };
                _disco.SetPixels32(px);
                _disco.Apply(false, true);
                return _disco;
            }
        }

        // ------------------------------------------------------------------ a dica (o nome e o porquê)

        static readonly StringBuilder _sb = new StringBuilder();

        /// <summary>O texto da dica: o nome em cima e o porquê embaixo, quebrado em linhas curtas.</summary>
        public static string TextoDaDica(string nome, string porque, int largura = 58)
        {
            _sb.Clear();
            _sb.Append(nome.ToUpperInvariant());
            if (string.IsNullOrEmpty(porque)) return _sb.ToString();
            _sb.Append('\n');
            int linha = 0;
            foreach (var palavra in porque.Split(' '))
            {
                if (linha > 0 && linha + 1 + palavra.Length > largura)
                {
                    _sb.Append('\n');
                    linha = 0;
                }
                else if (linha > 0)
                {
                    _sb.Append(' ');
                    linha++;
                }
                _sb.Append(palavra);
                linha += palavra.Length;
            }
            return _sb.ToString();
        }

        static readonly GUIContent _conteudo = new GUIContent();

        /// <summary>A caixa da dica perto de <paramref name="sobre"/> (para cima e para dentro da tela).</summary>
        public static void Dica(Rect sobre, string nome, string porque, float u)
        {
            var estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(12 * u), FontStyle.Normal)) { wordWrap = false };
            _conteudo.text = TextoDaDica(nome, porque);
            var tam = estilo.CalcSize(_conteudo);
            var r = new Rect(sobre.center.x - tam.x / 2f - 8f * u, sobre.y - tam.y - 16f * u, tam.x + 16f * u, tam.y + 10f * u);
            r.x = Mathf.Clamp(r.x, 4f * u, Screen.width - r.width - 4f * u);
            if (r.y < 4f * u) r.y = sobre.yMax + 8f * u;
            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.065f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
            estilo.normal.textColor = new Color(0.92f, 0.9f, 0.84f);
            GUI.Label(new Rect(r.x + 8f * u, r.y + 5f * u, tam.x, tam.y), _conteudo, estilo);
        }
    }
}
