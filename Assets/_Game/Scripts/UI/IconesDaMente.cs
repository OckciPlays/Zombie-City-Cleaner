using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O cérebro do HUD (06/11/2026, a sanidade: "um medidor de nível de ansiedade ... como se fosse um cérebro"), gerado por
    /// código uma vez, de perfil (a testa para a esquerda), em 128 × 100 px, pela distância com sinal (como os ícones do
    /// corpo): o <see cref="Miolo"/> (branco, para tingir e encher de baixo para cima com a mente), as <see cref="Dobras"/>
    /// (o contorno, os sulcos do córtex, a fissura do lado e as folhas do cerebelo, escuros) e as <see cref="Rachaduras"/>
    /// (o colapso). O <see cref="Player.MenteNoHud"/> desenha.
    /// </summary>
    public static class IconesDaMente
    {
        public const int Largura = 128, Altura = 100;
        static Texture2D _miolo, _dobras, _rachaduras;

        /// <summary>O cérebro inteiro, branco (o alfa é a forma).</summary>
        public static Texture2D Miolo => _miolo != null ? _miolo : _miolo = Gerar("Cérebro (miolo)", Miolo01);
        /// <summary>As linhas escuras por cima: o contorno e as dobras.</summary>
        public static Texture2D Dobras => _dobras != null ? _dobras : _dobras = Gerar("Cérebro (dobras)", Dobras01);
        /// <summary>As rachaduras (o colapso), escuras.</summary>
        public static Texture2D Rachaduras => _rachaduras != null ? _rachaduras : _rachaduras = Gerar("Cérebro (rachaduras)", Rachaduras01);

        // ------------------------------------------------ a forma ------------------------------------------------

        // a elipse (distância aproximada, em px; negativa dentro)
        static float Elipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float px = (x - cx) / rx, py = (y - cy) / ry;
            return (Mathf.Sqrt(px * px + py * py) - 1f) * Mathf.Min(rx, ry);
        }

        // a união suave (as bordas dos lobos se fundem)
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

        // o cérebro grande (o córtex): o meio, a frente, a nuca e o lobo de baixo (y para cima, 0 embaixo)
        static float Cortex(float x, float y)
        {
            float d = Elipse(x, y, 64f, 60f, 54f, 32f);
            d = Uniao(d, Elipse(x, y, 34f, 56f, 28f, 28f), 6f);
            d = Uniao(d, Elipse(x, y, 98f, 57f, 24f, 24f), 6f);
            d = Uniao(d, Elipse(x, y, 56f, 38f, 34f, 15f), 5f);
            // a borda ondulada (as dobras chegando no contorno)
            return d + 1.8f * (Mathf.PerlinNoise(x * 0.16f + 4.4f, y * 0.16f + 8.1f) - 0.5f);
        }

        static float Cerebelo(float x, float y) => Elipse(x, y, 94f, 28f, 19f, 11f);

        static float Tronco(float x, float y) => Segmento(x, y, 80f, 4f, 75f, 30f) - 4.5f;

        static float Forma(float x, float y) => Mathf.Min(Uniao(Cortex(x, y), Cerebelo(x, y), 2f), Tronco(x, y));

        // ------------------------------------------------ as camadas (0 a 1) ------------------------------------------------

        static float Miolo01(float x, float y) => Mathf.Clamp01(0.5f - Forma(x, y));

        static float Dobras01(float x, float y)
        {
            float f = Forma(x, y);
            if (f > 1.5f) return 0f;
            // o contorno
            float linha = Mathf.Clamp01(1.6f - Mathf.Abs(f + 0.6f));
            float cortex = Cortex(x, y), cerebelo = Cerebelo(x, y);
            if (cortex < -2.5f)
            {
                // os sulcos: as curvas de nível de um ruído torcido (os giros sinuosos entre elas, como no córtex)
                float wx = x + 7f * (Mathf.PerlinNoise(x * 0.045f + 3.1f, y * 0.045f + 7.7f) - 0.5f) * 2f;
                float wy = y + 7f * (Mathf.PerlinNoise(x * 0.045f + 11.3f, y * 0.045f + 1.9f) - 0.5f) * 2f;
                float n = Mathf.PerlinNoise(wx * 0.06f + 0.4f, wy * 0.06f + 2.2f) + 0.45f * Mathf.PerlinNoise(wx * 0.13f + 5f, wy * 0.13f + 9f);
                float nivel = n * 4.2f;
                float perto = Mathf.Abs(nivel - Mathf.Round(nivel));
                float sulco = Mathf.Clamp01(1f - perto / 0.11f) * 0.85f;
                // a fissura do lado (a de Sylvius) e o sulco do meio (o central), mais marcados
                float fissura = Mathf.Clamp01(2f - Mathf.Min(Segmento(x, y, 26f, 42f, 48f, 47f), Mathf.Min(Segmento(x, y, 48f, 47f, 66f, 55f), Segmento(x, y, 66f, 55f, 80f, 63f))));
                float central = Mathf.Clamp01(1.8f - Mathf.Min(Segmento(x, y, 72f, 91f, 66f, 80f), Segmento(x, y, 66f, 80f, 63f, 64f)));
                linha = Mathf.Max(linha, Mathf.Max(sulco * Mathf.Clamp01(-cortex / 4f), Mathf.Max(fissura, central)));
            }
            else if (cerebelo < -1.5f)
            {
                // as folhas do cerebelo: linhas deitadas
                float folha = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(0.85f * y + 0.15f * x)) / 0.22f) * 0.8f;
                linha = Mathf.Max(linha, folha);
            }
            // a borda entre o córtex e o cerebelo
            if (Mathf.Abs(cortex) < 1.4f && cerebelo < 0f) linha = Mathf.Max(linha, 0.9f);
            return linha * Mathf.Clamp01(0.8f - f);
        }

        // as rachaduras: três linhas quebradas que saem do meio
        static readonly Vector2[][] Trincas =
        {
            new[] { new Vector2(60, 64), new Vector2(52, 74), new Vector2(56, 82), new Vector2(46, 92) },
            new[] { new Vector2(60, 64), new Vector2(72, 58), new Vector2(80, 66), new Vector2(96, 62), new Vector2(108, 70) },
            new[] { new Vector2(60, 64), new Vector2(54, 54), new Vector2(40, 52), new Vector2(30, 42) },
        };

        static float Rachaduras01(float x, float y)
        {
            if (Forma(x, y) > -0.5f) return 0f;
            float d = 99f;
            foreach (var t in Trincas)
                for (int i = 0; i + 1 < t.Length; i++) d = Mathf.Min(d, Segmento(x, y, t[i].x, t[i].y, t[i + 1].x, t[i + 1].y));
            return Mathf.Clamp01(1.7f - d);
        }

        // 2 × 2 amostras por pixel (as bordas lisas)
        static Texture2D Gerar(string nome, System.Func<float, float, float> camada)
        {
            var px = new Color32[Largura * Altura];
            for (int y = 0; y < Altura; y++)
                for (int x = 0; x < Largura; x++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++) a += camada(x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy);
                    px[y * Largura + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 4f) * 255f));
                }
            var tex = new Texture2D(Largura, Altura, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = nome };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
