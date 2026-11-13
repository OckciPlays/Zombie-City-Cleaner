using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.UI;
using Forma = ZombieCityCleaner.Mundo.DocumentosDoHospital.Forma;

namespace ZombieCityCleaner.Documentos
{
    /// <summary>
    /// A folha na tela (o Plano_Hospital.md da pasta de arte, a parte 6, a decisão 2): o E num papel abre a folha no meio da
    /// tela, por cima do jogo, que não para (os zumbis continuam vindo); o E ou o ESC fecha. A folha é o papel velho do atlas
    /// (<see cref="AtlasDosPapeis.Liso"/>), com o timbre (a cruz e as letras de cima, em inglês), o carimbo vermelho torto e
    /// o texto na letra do papel: a de máquina (o Courier), a de mão (o Ink Free, a tinta azul e as linhas do papel) ou o
    /// formulário (a de mão nas linhas do formulário). As linhas "~" foram escritas à mão por cima, em vermelho (ou em azul,
    /// nos datilografados). A letra diminui até o texto caber. No jogo, a folha é uma tela do <see cref="MenuOnline"/> (o
    /// mouse solto e o jogador sem comandos); nas cenas de exemplo, ela trava o <see cref="AndarNoExemplo"/>. Sem som ao
    /// abrir e ao fechar (a pedido, 08/11/2026).
    /// </summary>
    public class LeituraDePapel : MonoBehaviour
    {
        public static LeituraDePapel Instancia { get; private set; }

        /// <summary>Há uma folha aberta agora.</summary>
        public static bool Aberta => Instancia != null && Instancia._doc != null;

        /// <summary>A folha aberta (null: nenhuma).</summary>
        public static Documento Atual => Instancia != null ? Instancia._doc : null;

        Documento _doc;
        Forma _forma;
        int _abertoNoQuadro;
        bool _comMenu;
        Texture2D _atlas;
        Font _fonteMaquina, _fonteMao, _fonteTimbre;
        GUIStyle _maquina, _mao, _acima, _timbre, _tipo, _carimbo, _dica;

        /// <summary>Abre a folha do documento (a forma do papel diz o fundo: as linhas do caderno e do bilhete).</summary>
        public static void Abrir(Documento documento, Forma forma)
        {
            if (documento == null) return;
            if (Instancia == null) new GameObject("Leitura dos papéis").AddComponent<LeituraDePapel>();
            Instancia.Mostrar(documento, forma);
        }

        void Awake() => Instancia = this;

        void OnEnable() => MenuOnline.FecharTelas += Fechar;

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (_doc != null) Fechar();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void Mostrar(Documento d, Forma forma)
        {
            bool ja = _doc != null;
            _doc = d;
            _forma = forma;
            _abertoNoQuadro = Time.frameCount;
            if (!ja)
            {
                MenuOnline.Tela(this, true);
                AndarNoExemplo.Travado = true;
                // sem o menu (as cenas de exemplo), o ESC é daqui; com ele, o ESC fecha as telas (o FecharTelas)
                _comMenu = FindAnyObjectByType<MenuOnline>() != null;
            }
        }

        /// <summary>Fecha a folha.</summary>
        public void Fechar()
        {
            if (_doc == null) return;
            _doc = null;
            MenuOnline.Tela(this, false);
            AndarNoExemplo.Travado = false;
        }

        // no LateUpdate: o E que fecha não chega na mira do mesmo quadro (ela já passou), e o que abriu não fecha
        void LateUpdate()
        {
            if (_doc == null) return;
            var k = Keyboard.current;
            if (k == null || Time.frameCount <= _abertoNoQuadro) return;
            if (k.eKey.wasPressedThisFrame || (!_comMenu && k.escapeKey.wasPressedThisFrame)) Fechar();
        }

        // ------------------------------------------------------------------ a folha

        void Estilos(float h)
        {
            if (_fonteMaquina == null)
            {
                _fonteMaquina = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas", "Lucida Console" }, 18);
                _fonteMao = Font.CreateDynamicFontFromOSFont(new[] { "Ink Free", "Segoe Print", "Comic Sans MS", "Segoe Script" }, 18);
                _fonteTimbre = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 18);
                _atlas = Resources.Load<Texture2D>("Documentos/T_Papeis");
            }
            if (_maquina == null)
            {
                GUIStyle Novo(Font f, FontStyle estilo, bool quebra, TextAnchor ancora) => new GUIStyle
                {
                    font = f, fontStyle = estilo, wordWrap = quebra, alignment = ancora, richText = false,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow,
                };
                _maquina = Novo(_fonteMaquina, FontStyle.Normal, true, TextAnchor.UpperLeft);
                _mao = Novo(_fonteMao, FontStyle.Normal, true, TextAnchor.UpperLeft);
                _acima = Novo(_fonteMao, FontStyle.Normal, true, TextAnchor.UpperLeft);
                _timbre = Novo(_fonteTimbre, FontStyle.Bold, true, TextAnchor.UpperLeft);
                _tipo = Novo(_fonteTimbre, FontStyle.Normal, true, TextAnchor.UpperLeft);
                _carimbo = Novo(_fonteTimbre, FontStyle.Bold, false, TextAnchor.MiddleCenter);
                _dica = Novo(_fonteTimbre, FontStyle.Normal, false, TextAnchor.UpperCenter);
            }
            _timbre.fontSize = Mathf.RoundToInt(h * 0.0185f);
            _tipo.fontSize = Mathf.RoundToInt(h * 0.0142f);
            _carimbo.fontSize = Mathf.RoundToInt(h * 0.03f);
            _dica.fontSize = Mathf.Max(11, Mathf.RoundToInt(h * 0.017f));
            _timbre.normal.textColor = new Color(0.16f, 0.16f, 0.18f);
            _tipo.normal.textColor = new Color(0.33f, 0.31f, 0.28f);
            _carimbo.normal.textColor = new Color(0.66f, 0.11f, 0.09f, 0.72f);
            _dica.normal.textColor = new Color(0.86f, 0.84f, 0.78f);
        }

        void OnGUI()
        {
            if (_doc == null) return;
            GUI.depth = -100;
            float H = Mathf.Round(Screen.height * 0.9f), W = Mathf.Round(H * 0.773f);
            if (W > Screen.width * 0.94f)
            {
                W = Mathf.Round(Screen.width * 0.94f);
                H = Mathf.Round(W / 0.773f);
            }
            Estilos(H);
            var pagina = new Rect(Mathf.Round((Screen.width - W) / 2f), Mathf.Round((Screen.height - H) / 2f - H * 0.012f), W, H);
            var antes = GUI.color;
            Pintar(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f));
            Pintar(new Rect(pagina.x + H * 0.01f, pagina.y + H * 0.012f, W, H), new Color(0f, 0f, 0f, 0.45f));
            GUI.color = Color.white;
            if (_atlas != null) GUI.DrawTextureWithTexCoords(pagina, _atlas, AtlasDosPapeis.UV(AtlasDosPapeis.Liso));
            else Pintar(pagina, new Color(0.85f, 0.81f, 0.69f));

            float m = W * 0.09f, x = pagina.x + m, largura = W - 2f * m, y = pagina.y + H * 0.06f;
            float fimDoTimbre = Timbre(x, y, largura, H);
            // o carimbo tem a faixa dele, à direita, logo embaixo do timbre: o texto começa abaixo
            if (!string.IsNullOrEmpty(_doc.carimbo)) fimDoTimbre = Carimbo(pagina, m, H, fimDoTimbre);
            y = fimDoTimbre + (fimDoTimbre > y ? H * 0.03f : 0f);
            Corpo(pagina, x, y, largura, pagina.yMax - H * 0.065f - y, H);

            GUI.color = antes;
            var dica = new Rect(pagina.x, pagina.yMax + H * 0.008f, W, H * 0.03f);
            Rotulo(dica, "[E] ou [Esc] para fechar", _dica, true);
        }

        // a cruz (no hospital), as letras do timbre e o tipo, com o traço embaixo; devolve onde acabou
        float Timbre(float x, float y, float largura, float H)
        {
            bool cab = !string.IsNullOrEmpty(_doc.cabecalho), tipo = !string.IsNullOrEmpty(_doc.tipo);
            if (!cab && !tipo) return y;
            float e = H * 0.034f, tx = x, tl = largura;
            // a cruz só no timbre do hospital (a loja de armas e os outros lugares têm o timbre só com as letras)
            if (cab && _doc.cabecalho.Contains("HOSPITAL"))
            {
                var vermelho = new Color(0.55f, 0.12f, 0.10f, 0.9f);
                Pintar(new Rect(x + e * 0.36f, y, e * 0.28f, e), vermelho);
                Pintar(new Rect(x, y + e * 0.36f, e, e * 0.28f), vermelho);
                tx = x + e * 1.4f;
                tl = largura - e * 1.4f;
            }
            float yy = y;
            if (cab)
            {
                float hc = _timbre.CalcHeight(new GUIContent(_doc.cabecalho), tl);
                Rotulo(new Rect(tx, yy, tl, hc), _doc.cabecalho, _timbre, false);
                yy += hc + H * 0.004f;
            }
            if (tipo)
            {
                float ht = _tipo.CalcHeight(new GUIContent(_doc.tipo), tl);
                Rotulo(new Rect(tx, yy, tl, ht), _doc.tipo, _tipo, false);
                yy += ht;
            }
            yy = Mathf.Max(yy, y + e) + H * 0.014f;
            Pintar(new Rect(x, yy, largura, Mathf.Max(1f, Mathf.Round(H * 0.0016f))), new Color(0.25f, 0.22f, 0.2f, 0.65f));
            return yy;
        }

        // o carimbo torto, à direita, logo embaixo do timbre; devolve onde ele acaba (com a ponta que o giro levanta)
        float Carimbo(Rect pagina, float m, float H, float y)
        {
            var conteudo = new GUIContent(_doc.carimbo);
            Vector2 t = _carimbo.CalcSize(conteudo);
            float pad = H * 0.012f;
            var caixa = new Rect(pagina.xMax - m - t.x - pad * 2f, y + H * 0.018f, t.x + pad * 2f, t.y + pad * 0.8f);
            var guardada = GUI.matrix;
            GUIUtility.RotateAroundPivot(-9f, caixa.center);
            var cor = _carimbo.normal.textColor;
            float l = Mathf.Max(2f, H * 0.0032f);
            Pintar(new Rect(caixa.x, caixa.y, caixa.width, l), cor);
            Pintar(new Rect(caixa.x, caixa.yMax - l, caixa.width, l), cor);
            Pintar(new Rect(caixa.x, caixa.y, l, caixa.height), cor);
            Pintar(new Rect(caixa.xMax - l, caixa.y, l, caixa.height), cor);
            GUI.color = Color.white;
            GUI.Label(caixa, conteudo, _carimbo);
            GUI.matrix = guardada;
            return caixa.yMax + Mathf.Sin(9f * Mathf.Deg2Rad) * caixa.width / 2f;
        }

        // o texto: a letra diminui até caber; nos escritos à mão e nos formulários, as linhas do papel atrás
        void Corpo(Rect pagina, float x, float y, float largura, float disponivel, float H)
        {
            bool maquina = _doc.letra == Letra.Maquina, caderno = _forma == Forma.Caderno || _forma == Forma.Bilhete;
            bool pautado = _doc.letra == Letra.Formulario || caderno;
            // o recado de pincel atômico (o aviso escrito à mão): preto, grande, grosso e no meio da folha
            bool pincel = _forma == Forma.Aviso && _doc.letra == Letra.Mao;
            var tinta = maquina ? new Color(0.12f, 0.11f, 0.10f) : pincel ? new Color(0.07f, 0.07f, 0.08f) : new Color(0.10f, 0.14f, 0.33f);
            var porCima = maquina ? new Color(0.10f, 0.18f, 0.50f) : new Color(0.62f, 0.13f, 0.10f);
            var base_ = maquina ? _maquina : _mao;
            _mao.fontStyle = pincel ? FontStyle.Bold : FontStyle.Normal;
            float tamanho = pincel ? H * 0.068f : _doc.Tamanho < 140 ? H * 0.05f : maquina ? H * 0.027f : H * 0.031f;
            float total = 0f, lh = 0f;
            for (int volta = 0; volta < 40; volta++)
            {
                base_.fontSize = Mathf.RoundToInt(tamanho);
                _acima.fontSize = Mathf.RoundToInt(maquina ? tamanho * 1.1f : tamanho);
                lh = base_.lineHeight;
                total = 0f;
                foreach (var (texto, aMao) in _doc.linhas)
                    total += texto.Length == 0 ? (pautado ? lh : lh * 0.6f) : (aMao ? _acima : base_).CalcHeight(new GUIContent(texto), largura);
                if (total <= disponivel || tamanho < H * 0.012f) break;
                tamanho *= 0.94f;
            }
            base_.normal.textColor = tinta;
            _acima.normal.textColor = porCima;
            if (pautado)
            {
                // as linhas do papel (azuis no caderno e no bilhete, cinza no formulário), na altura das linhas do texto, e a
                // margem vermelha do caderno e do bilhete
                var cor = caderno ? new Color(0.36f, 0.48f, 0.68f, 0.32f) : new Color(0.32f, 0.32f, 0.34f, 0.26f);
                float fio = Mathf.Max(1f, Mathf.Round(H * 0.0012f));
                for (float ly = y + lh * 0.93f; ly < pagina.yMax - H * 0.04f; ly += lh) Pintar(new Rect(pagina.x + H * 0.02f, Mathf.Round(ly), pagina.width - H * 0.04f, fio), cor);
                if (caderno) Pintar(new Rect(x - H * 0.012f, pagina.y + H * 0.02f, fio, pagina.height - H * 0.04f), new Color(0.72f, 0.25f, 0.25f, 0.35f));
            }
            float yy = pincel ? y + Mathf.Max(0f, (disponivel - total) * 0.4f) : y;
            int i = 0;
            foreach (var (texto, aMao) in _doc.linhas)
            {
                i++;
                if (texto.Length == 0)
                {
                    yy += pautado ? lh : lh * 0.6f;
                    continue;
                }
                var estilo = aMao ? _acima : base_;
                float alto = estilo.CalcHeight(new GUIContent(texto), largura);
                // a mão não escreve reto: um tico para um lado ou para o outro em cada linha
                float desvio = (maquina && !aMao) ? 0f : ((i * 37) % 5 - 2) * H * 0.0018f;
                GUI.color = Color.white;
                GUI.Label(new Rect(x + desvio, yy, largura, alto), texto, estilo);
                yy += alto;
            }
        }

        void Rotulo(Rect r, string texto, GUIStyle estilo, bool sombra)
        {
            if (sombra)
            {
                var cor = estilo.normal.textColor;
                estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), texto, estilo);
                estilo.normal.textColor = cor;
            }
            GUI.color = Color.white;
            GUI.Label(r, texto, estilo);
        }

        static void Pintar(Rect r, Color cor)
        {
            GUI.color = cor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }
    }
}
