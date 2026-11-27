using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O círculo da briga com o peixe (27/11/2026, a pesca nova, a etapa 3, a pedido: "um circulo pequeno ... seria tipo uma
    /// representação do rio, então poderia ser azul da cor da agua, poderia ter um ícone do peixe ... um ícone da boia"), no
    /// jeito do HUD (OnGUI e texturas feitas aqui), sem texto nenhum, embaixo da mira:
    /// <list type="bullet">
    /// <item>o disco azul da água, com ondinhas andando;</item>
    /// <item>o peixe: uma silhueta virada para onde ele nada, na cor da espécie, maior no peixe maior;</item>
    /// <item>a boia: vermelha e branca, com o anel da área dela (onde ela pega o peixe);</item>
    /// <item>a barra de captura: um anel de contas na borda, do alto no sentido do relógio, verde enchendo (a boia em cima do
    /// peixe) e vermelho esvaziando.</item>
    /// </list>
    /// No fim: pegou, o peixe salta para fora do círculo com o respingo; escapou, ele foge para a borda e some. Quem chama é o
    /// <see cref="PescaJogador"/> (o OnGUI dele).
    /// </summary>
    public static class CirculoDaPesca
    {
        /// <summary>O raio do círculo e o quanto o centro fica abaixo da mira (pontos de uma tela de 720).</summary>
        const float Raio = 100f, AbaixoDaMira = 150f;
        /// <summary>Quanto dura o fim (o salto ou a fuga), em s.</summary>
        const float DuracaoDoFim = 0.9f;
        const int Contas = 60;

        static Texture2D _disco, _anel, _ponto, _peixe, _boia;
        static readonly Color CorDaBoia = new Color(0.86f, 0.16f, 0.13f);

        // o fim (a foto da briga no último quadro)
        static float _fimDesde = -10f;
        static bool _fimPegou;
        static Vector2 _fimPeixe;
        static float _fimRumo, _fimTamanho;
        static Color _fimCor;

        /// <summary>O fim ainda está na tela.</summary>
        public static bool Mostrando => Time.unscaledTime - _fimDesde < DuracaoDoFim;

        /// <summary>A briga acabou: guarda o último quadro para o salto (pegou) ou a fuga (escapou).</summary>
        public static void Acabou(BrigaDoPeixe b)
        {
            _fimDesde = Time.unscaledTime;
            _fimPegou = b.Pegou;
            _fimPeixe = b.Peixe;
            _fimRumo = b.Rumo;
            _fimTamanho = b.Jeito.tamanho;
            _fimCor = b.Jeito.cor;
        }

        /// <summary>A briga, a cada OnGUI (Repaint).</summary>
        public static void Desenhar(BrigaDoPeixe b)
        {
            Preparar();
            float u = Screen.height / 720f, R = Raio * u;
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + AbaixoDaMira * u);
            var antes = GUI.color;
            Agua(c, R, 1f);
            // a boia e a área dela
            Vector2 boia = c + new Vector2(b.Boia.x, -b.Boia.y) * R;
            float rb = BrigaDoPeixe.RaioDaBoia * R;
            GUI.color = b.NoPeixe ? new Color(0.6f, 1f, 0.65f, 0.75f) : new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(Quadrado(boia, rb * 2f), _anel);
            // o peixe (com o arranco, um rastro)
            Vector2 peixe = c + new Vector2(b.Peixe.x, -b.Peixe.y) * R;
            if (b.Arranco > 0.05f)
            {
                Vector2 atras = new Vector2(-Mathf.Cos(b.Rumo * Mathf.Deg2Rad), Mathf.Sin(b.Rumo * Mathf.Deg2Rad));
                for (int i = 1; i <= 3; i++)
                    Peixe(peixe + atras * (i * 0.06f * R), b.Rumo, b.Jeito.tamanho * R, b.Jeito.cor, 0.22f * b.Arranco / i);
            }
            Peixe(peixe, b.Rumo, b.Jeito.tamanho * R, b.Jeito.cor, 1f);
            GUI.color = Color.white;
            GUI.DrawTexture(Quadrado(boia, rb * 1.6f), _boia);
            // a barra de captura
            Contas_(c, R, b.Captura, b.NoPeixe ? new Color(0.35f, 0.9f, 0.4f, 0.95f) : new Color(0.95f, 0.35f, 0.25f, 0.95f), 1f);
            GUI.color = antes;
        }

        /// <summary>O fim, depois da briga, enquanto <see cref="Mostrando"/>.</summary>
        public static void DesenharFim()
        {
            if (!Mostrando) return;
            Preparar();
            float k = Mathf.Clamp01((Time.unscaledTime - _fimDesde) / DuracaoDoFim);
            float u = Screen.height / 720f, R = Raio * u;
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + AbaixoDaMira * u);
            var antes = GUI.color;
            float some = 1f - Mathf.SmoothStep(0.45f, 1f, k);
            Agua(c, R, some);
            Vector2 p = c + new Vector2(_fimPeixe.x, -_fimPeixe.y) * R;
            if (_fimPegou)
            {
                // salta para cima, para fora do círculo, girando, e o respingo
                Vector2 salto = p + new Vector2(0f, -(R * 1.5f) * Mathf.Sin(k * Mathf.PI * 0.6f) + R * 0.4f * k * k);
                Peixe(salto, _fimRumo + 200f * k, _fimTamanho * R * (1f + 0.5f * k), _fimCor, 1f - k * k);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad + 0.3f;
                    Vector2 gota = p + new Vector2(Mathf.Cos(a) * 0.55f, -Mathf.Abs(Mathf.Sin(a)) * 0.9f) * (R * 0.5f * k) + new Vector2(0f, R * 0.9f * k * k);
                    GUI.color = new Color(0.85f, 0.95f, 1f, (1f - k) * 0.9f);
                    GUI.DrawTexture(Quadrado(gota, 5f * u), _ponto);
                }
                Contas_(c, R, 1f, new Color(0.35f, 0.9f, 0.4f, 0.95f), some);
            }
            else
            {
                // foge pelo rumo dele até a borda e some
                Vector2 rumo = new Vector2(Mathf.Cos(_fimRumo * Mathf.Deg2Rad), -Mathf.Sin(_fimRumo * Mathf.Deg2Rad));
                Peixe(p + rumo * (R * 1.1f * k), _fimRumo, _fimTamanho * R, _fimCor, 1f - k);
                Contas_(c, R, 0f, Color.clear, some);
            }
            GUI.color = antes;
        }

        // ------------------------------------------------------------------ as partes

        static void Agua(Vector2 c, float R, float alfa)
        {
            GUI.color = new Color(1f, 1f, 1f, alfa);
            GUI.DrawTexture(Quadrado(c, R * 2f), _disco);
            // as ondinhas: anéis que nascem dentro do disco, crescem e somem (cada um num lugar a cada volta)
            for (int i = 0; i < 5; i++)
            {
                float t = Time.unscaledTime * 0.32f + i / 5f;
                int volta = Mathf.FloorToInt(t);
                float f = t - volta;
                float a = Hash(volta * 7 + i * 131) * Mathf.PI * 2f, d = Mathf.Sqrt(Hash(volta * 13 + i * 71)) * 0.55f;
                Vector2 centro = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (d * R);
                GUI.color = new Color(0.85f, 0.95f, 1f, (1f - f) * 0.28f * alfa);
                GUI.DrawTexture(Quadrado(centro, R * (0.12f + 0.55f * f)), _anel);
            }
        }

        static void Peixe(Vector2 centro, float rumo, float raio, Color cor, float alfa)
        {
            if (alfa <= 0.01f) return;
            var matriz = GUI.matrix;
            GUIUtility.RotateAroundPivot(-rumo, centro);
            GUI.color = new Color(cor.r, cor.g, cor.b, cor.a * alfa);
            float w = raio * 3.2f, h = w * 0.5f;
            GUI.DrawTexture(new Rect(centro.x - w * 0.5f, centro.y - h * 0.5f, w, h), _peixe);
            GUI.matrix = matriz;
        }

        // a barra de captura: contas na borda, do alto no sentido do relógio
        static void Contas_(Vector2 c, float R, float cheio, Color cor, float alfa)
        {
            float u = Screen.height / 720f, rc = R + 9f * u;
            int n = Mathf.RoundToInt(Mathf.Clamp01(cheio) * Contas);
            for (int i = 0; i < Contas; i++)
            {
                float a = (i + 0.5f) / Contas * Mathf.PI * 2f;
                Vector2 p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * rc;
                GUI.color = i < n ? new Color(cor.r, cor.g, cor.b, cor.a * alfa) : new Color(0.05f, 0.08f, 0.12f, 0.55f * alfa);
                GUI.DrawTexture(Quadrado(p, 6.5f * u), _ponto);
            }
        }

        static Rect Quadrado(Vector2 c, float lado) => new Rect(c.x - lado * 0.5f, c.y - lado * 0.5f, lado, lado);

        static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        // ------------------------------------------------------------------ as texturas (feitas uma vez)

        static void Preparar()
        {
            if (_disco != null) return;
            _disco = Fazer(256, 256, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > 1f) return Color.clear;
                float borda = Mathf.Clamp01((1f - d) * 128f);
                var fundo = Color.Lerp(new Color(0.13f, 0.38f, 0.56f), new Color(0.08f, 0.25f, 0.4f), d * d);
                fundo = Color.Lerp(new Color(0.04f, 0.12f, 0.2f), fundo, Mathf.Clamp01((1f - d) * 30f));   // (a beira escura)
                fundo.a = 0.9f * borda;
                return fundo;
            });
            _anel = Fazer(128, 128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 22f);
                return new Color(1f, 1f, 1f, a);
            });
            _ponto = Fazer(32, 32, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f));
            });
            // o peixe virado para a direita (a cabeça em +x): o corpo, o rabo em V e o olho; mais escuro no dorso
            _peixe = Fazer(128, 64, (x, y) =>
            {
                float corpo = (x - 0.18f) * (x - 0.18f) / (0.62f * 0.62f) + y * y / (0.62f * 0.62f);
                bool dentro = corpo <= 1f;
                float ax = -0.62f, rabo = Mathf.InverseLerp(ax + 0.05f, -1f, x);   // de onde o rabo sai até a ponta
                bool noRabo = x < ax + 0.1f && x > -1f && Mathf.Abs(y) < 0.12f + rabo * 0.75f && Mathf.Abs(y) > rabo * 0.35f - 0.05f;
                if (!dentro && !noRabo) return Color.clear;
                float olho = (x - 0.56f) * (x - 0.56f) + (y - 0.14f) * (y - 0.14f);
                if (olho < 0.006f) return new Color(0.05f, 0.05f, 0.05f, 1f);
                float tom = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(y * 1.4f + 0.3f));   // o dorso (y para cima) mais escuro
                return new Color(tom, tom, tom, 1f);
            });
            _boia = Fazer(64, 64, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > 1f) return Color.clear;
                if (d > 0.86f) return new Color(0.08f, 0.06f, 0.06f, Mathf.Clamp01((1f - d) * 40f));
                return y > -0.08f ? CorDaBoia : new Color(0.96f, 0.96f, 0.94f);
            });
        }

        // uma textura de w x h pela cor em cada ponto (x e y de -1 a 1, y para cima)
        static Texture2D Fazer(int w, int h, System.Func<float, float, Color> cor)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    px[j * w + i] = cor((i + 0.5f) / w * 2f - 1f, (j + 0.5f) / h * 2f - 1f);
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
