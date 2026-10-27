namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Ruído de Perlin 2D com a tabela embaralhada pela semente (o Noise2D do Guardiões da Floresta). Código gerenciado
    /// puro: o mesmo resultado em todas as máquinas, e seguro fora da thread principal (o Mathf.PerlinNoise é nativo e
    /// não tem semente).
    /// </summary>
    public sealed class Ruido
    {
        readonly int[] _perm = new int[512];

        public Ruido(int semente, string etapa)
        {
            var s = Sorteio.Da(semente, etapa);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = s.Entre(0, i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>O ruído em mais ou menos [-1, 1].</summary>
        public float Amostra(float x, float y)
        {
            int xi = Piso(x), yi = Piso(y);
            float xf = x - xi, yf = y - yi;
            int X = xi & 255, Y = yi & 255;
            float u = Suave(xf), v = Suave(yf);
            int aa = _perm[_perm[X] + Y], ab = _perm[_perm[X] + Y + 1];
            int ba = _perm[_perm[X + 1] + Y], bb = _perm[_perm[X + 1] + Y + 1];
            float x1 = Lerp(Gradiente(aa, xf, yf), Gradiente(ba, xf - 1f, yf), u);
            float x2 = Lerp(Gradiente(ab, xf, yf - 1f), Gradiente(bb, xf - 1f, yf - 1f), u);
            return Lerp(x1, x2, v);
        }

        /// <summary>O ruído em [0, 1].</summary>
        public float Amostra01(float x, float y)
        {
            float n = Amostra(x, y) * 0.5f + 0.5f;
            return n < 0f ? 0f : n > 1f ? 1f : n;
        }

        /// <summary>A soma de oitavas (fBm), normalizada em mais ou menos [-1, 1].</summary>
        public float Fbm(float x, float y, int oitavas, float lacunaridade = 2f, float ganho = 0.5f)
        {
            float soma = 0f, amp = 1f, norma = 0f, f = 1f;
            for (int o = 0; o < oitavas; o++)
            {
                soma += Amostra(x * f + o * 17.3f, y * f - o * 9.1f) * amp;
                norma += amp;
                amp *= ganho;
                f *= lacunaridade;
            }
            return soma / norma;
        }

        static int Piso(float v) => v >= 0f ? (int)v : (int)v - 1;
        static float Suave(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Gradiente(int hash, float x, float y) => (hash & 7) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            3 => -x - y,
            4 => x,
            5 => -x,
            6 => y,
            _ => -y,
        };
    }
}
