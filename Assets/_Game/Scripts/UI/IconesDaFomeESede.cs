using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Os medidores redondos da fome e da sede (12/11/2026, a pedido: "um símbolo igual da sanidade para a fome e a sede,
    /// redondo ao invés de barra horizontal"), gerados por código uma vez como o cérebro (<see cref="IconesDaMente"/>), em
    /// 100 × 100 px, pela distância com sinal: o estômago (a fome) e a gota (a sede). O miolo é branco (para tingir e encher de
    /// baixo para cima), o contorno e as dobras são escuros, e o brilho da gota é claro. O <see cref="Player.FomeESede"/> desenha.
    /// </summary>
    public static class IconesDaFomeESede
    {
        public const int Lado = 100;
        static Texture2D _estomago, _contornoDoEstomago, _gota, _contornoDaGota, _brilhoDaGota;

        /// <summary>O estômago inteiro, branco (o alfa é a forma).</summary>
        public static Texture2D Estomago => _estomago != null ? _estomago : _estomago = Gerar("Estômago (miolo)", (x, y) => Mathf.Clamp01(0.5f - FormaDoEstomago(x, y)), out _faixaDoEstomago);
        /// <summary>O contorno do estômago e as dobras de dentro, escuros.</summary>
        public static Texture2D ContornoDoEstomago => _contornoDoEstomago != null ? _contornoDoEstomago : _contornoDoEstomago = Gerar("Estômago (contorno)", DobrasDoEstomago);
        /// <summary>A gota inteira, branca.</summary>
        public static Texture2D Gota => _gota != null ? _gota : _gota = Gerar("Gota (miolo)", (x, y) => Mathf.Clamp01(0.5f - Gota01(x, y)), out _faixaDaGota);

        static Vector2 _faixaDoEstomago, _faixaDaGota;

        /// <summary>
        /// A parte da altura (0 embaixo, 1 em cima, nas coordenadas da textura) até onde o medidor enche com a fração: do
        /// fundo ao topo da forma, e não do quadrado (o esôfago do estômago enganava: 70% já enchia o corpo todo).
        /// </summary>
        public static float Altura(Texture2D miolo, float fracao)
        {
            var faixa = miolo == Estomago ? _faixaDoEstomago : miolo == Gota ? _faixaDaGota : new Vector2(0f, 1f);
            return Mathf.Lerp(faixa.x, faixa.y, Mathf.Clamp01(fracao));
        }
        /// <summary>O contorno da gota, escuro.</summary>
        public static Texture2D ContornoDaGota => _contornoDaGota != null ? _contornoDaGota : _contornoDaGota = Gerar("Gota (contorno)", (x, y) => Contorno(Gota01(x, y)));
        /// <summary>O brilho da gota (o reflexo do lado esquerdo), claro.</summary>
        public static Texture2D BrilhoDaGota => _brilhoDaGota != null ? _brilhoDaGota : _brilhoDaGota = Gerar("Gota (brilho)", BrilhoDaGota01);

        // ------------------------------------------------ as formas (y para cima, 0 embaixo) ------------------------------------------------

        static float Elipse(float x, float y, float cx, float cy, float rx, float ry, float graus = 0f)
        {
            float a = graus * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float dx = x - cx, dy = y - cy;
            float px = (dx * c + dy * s) / rx, py = (-dx * s + dy * c) / ry;
            return (Mathf.Sqrt(px * px + py * py) - 1f) * Mathf.Min(rx, ry);
        }

        static float Uniao(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        static float Segmento(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x - ax, y - ay);
            var ab = new Vector2(bx - ax, by - ay);
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
            return (p - ab * t).magnitude;
        }

        // o estômago: o corpo deitado (o J), o fundo em cima à esquerda, o esôfago entrando por cima e o piloro saindo à direita
        static float FormaDoEstomago(float x, float y)
        {
            float d = Elipse(x, y, 50f, 38f, 37f, 27f, -18f);
            d = Uniao(d, Elipse(x, y, 30f, 58f, 20f, 19f), 7f);
            d = Uniao(d, Segmento(x, y, 28f, 72f, 24f, 93f) - 7f, 4f);
            d = Uniao(d, Segmento(x, y, 76f, 30f, 89f, 42f) - 8.5f, 5f);
            d = Uniao(d, Segmento(x, y, 89f, 42f, 92f, 27f) - 6f, 3f);
            return d;
        }

        // a gota: a bola embaixo e a ponta em cima (a cápsula desigual)
        static float Gota01(float x, float y)
        {
            const float r1 = 29f, r2 = 1.5f, h = 58f;
            float px = Mathf.Abs(x - 50f), py = y - 35f;
            float b = (r1 - r2) / h, a = Mathf.Sqrt(1f - b * b);
            float k = -b * px + a * py;
            if (k < 0f) return Mathf.Sqrt(px * px + py * py) - r1;
            if (k > a * h) return Mathf.Sqrt(px * px + (py - h) * (py - h)) - r2;
            return a * px + b * py - r1;
        }

        // ------------------------------------------------ as camadas (0 a 1) ------------------------------------------------

        static float Contorno(float f) => f > 1.5f ? 0f : Mathf.Clamp01(1.9f - Mathf.Abs(f + 0.8f)) * Mathf.Clamp01(0.8f - f);

        static float DobrasDoEstomago(float x, float y)
        {
            float f = FormaDoEstomago(x, y);
            float linha = Contorno(f);
            if (f < -3f)
            {
                // as dobras de dentro (as rugas da mucosa): três traços curvos ao longo do corpo
                float d = Mathf.Min(Mathf.Min(Segmento(x, y, 30f, 44f, 46f, 32f), Segmento(x, y, 46f, 32f, 62f, 28f)),
                                    Mathf.Min(Segmento(x, y, 40f, 56f, 56f, 42f), Segmento(x, y, 56f, 42f, 72f, 38f)));
                linha = Mathf.Max(linha, Mathf.Clamp01(1.5f - d) * 0.7f);
            }
            return linha;
        }

        static float BrilhoDaGota01(float x, float y)
        {
            if (Gota01(x, y) > -2f) return 0f;
            return Mathf.Clamp01(0.5f - Elipse(x, y, 37f, 42f, 5f, 11f, -15f)) * 0.85f;
        }

        // 2 × 2 amostras por pixel (as bordas lisas)
        static Texture2D Gerar(string nome, System.Func<float, float, float> camada) => Gerar(nome, camada, out _);

        static Texture2D Gerar(string nome, System.Func<float, float, float> camada, out Vector2 faixa)
        {
            var px = new Color32[Lado * Lado];
            int baixo = Lado, alto = -1;
            for (int y = 0; y < Lado; y++)
                for (int x = 0; x < Lado; x++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++) a += camada(x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy);
                    px[y * Lado + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 4f) * 255f));
                    if (a > 2f)
                    {
                        baixo = Mathf.Min(baixo, y);
                        alto = Mathf.Max(alto, y);
                    }
                }
            faixa = alto < 0 ? new Vector2(0f, 1f) : new Vector2(baixo / (float)Lado, (alto + 1) / (float)Lado);
            var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = nome };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
