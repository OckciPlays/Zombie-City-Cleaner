using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons de desmontar o metal (09/11/2026, a sucata, 2ª leva), gerados por código como os das portas
    /// (<see cref="SomGerado"/>): batendo (enquanto o E é segurado no painel: a chapa vibrando com duas pancadas e o
    /// raspado do metal) e soltando (o fim: a pancada que solta a peça e os pedaços caindo no chão).
    /// </summary>
    public static class SomDaSucata
    {
        static AudioClip _batendo, _soltando, _desmontando;

        /// <summary>Duas pancadas na chapa e o raspado (0,55 s; o jogo repete enquanto o E é segurado).</summary>
        public static AudioClip Batendo => _batendo ??= SomGerado.Clip("Sucata: batendo", Batida(71));
        /// <summary>A pancada que solta a peça e os pedaços caindo (1 s; todos ouvem).</summary>
        public static AudioClip Soltando => _soltando ??= SomGerado.Clip("Sucata: soltando", Soltar(72));
        /// <summary>O desmontar na mochila: os parafusos girando e uns cliques de metal (0,7 s; o jogo repete).</summary>
        public static AudioClip Desmontando => _desmontando ??= SomGerado.Clip("Sucata: desmontando", Parafusos(73));

        const int Taxa = 44100;

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // uma pancada de chapa: os modos fora de harmonia (os de uma placa fina), cada um caindo no seu tempo, e o
        // estalo do começo
        static void Pancada(float[] d, System.Random r, float inicio, float tom, float forca, float cai = 1f)
        {
            float[] razoes = { 1f, 1.59f, 2.14f, 2.83f, 3.97f, 5.21f };
            float[] pesos = { 0.55f, 0.4f, 0.32f, 0.22f, 0.15f, 0.1f };
            int i0 = Mathf.RoundToInt(inicio * Taxa);
            int n = Mathf.Min(d.Length - i0, Mathf.RoundToInt(0.6f * Taxa));
            var fases = new float[razoes.Length];
            for (int k = 0; k < fases.Length; k++) fases[k] = (float)r.NextDouble();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa, s = 0f;
                for (int k = 0; k < razoes.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * (tom * razoes[k] * t + fases[k])) * pesos[k] * Mathf.Exp(-t * (7f + k * 5f) * cai);
                s += Ruido(r) * Mathf.Exp(-t * 90f) * 0.5f;
                d[i0 + i] += s * forca;
            }
        }

        // um clique pequeno de metal (um parafuso, um pedaço caindo)
        static void Clique(float[] d, System.Random r, float inicio, float tom, float forca)
        {
            int i0 = Mathf.RoundToInt(inicio * Taxa);
            int n = Mathf.Min(d.Length - i0, Mathf.RoundToInt(0.07f * Taxa));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i0 + i] += (Mathf.Sin(2f * Mathf.PI * tom * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * tom * 2.31f * t) * 0.3f + Ruido(r) * 0.35f)
                             * Mathf.Exp(-t * 120f) * forca;
            }
        }

        static float[] Fechar(float[] d, float ganho)
        {
            float max = 0.0001f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float k = ganho / max;
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i] * k, -1f, 1f);
            return d;
        }

        static float[] Batida(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(0.55f * Taxa)];
            Pancada(d, r, 0.01f, 190f + (float)r.NextDouble() * 30f, 0.9f, 1.3f);
            Pancada(d, r, 0.24f, 240f + (float)r.NextDouble() * 40f, 0.6f, 1.6f);
            // o raspado do metal entre as pancadas (um chiado áspero que sobe)
            float filtro = 0f;
            for (int i = Mathf.RoundToInt(0.08f * Taxa); i < Mathf.RoundToInt(0.22f * Taxa); i++)
            {
                float t = i / (float)Taxa;
                filtro += (Ruido(r) - filtro) * 0.45f;
                d[i] += filtro * Mathf.Sin((t - 0.08f) / 0.14f * Mathf.PI) * 0.18f * (0.6f + 0.4f * Mathf.Sin(t * 260f));
            }
            return Fechar(d, 0.8f);
        }

        static float[] Soltar(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(1.0f * Taxa)];
            Pancada(d, r, 0.01f, 150f, 1f, 0.8f);
            Pancada(d, r, 0.03f, 310f, 0.5f, 1.2f);
            // os pedaços caindo e quicando no chão, cada vez mais fracos
            float t0 = 0.18f;
            for (int k = 0; k < 9; k++)
            {
                Clique(d, r, t0, 1700f + (float)r.NextDouble() * 1900f, 0.45f * Mathf.Pow(0.82f, k));
                t0 += 0.04f + (float)r.NextDouble() * 0.08f;
            }
            return Fechar(d, 0.85f);
        }

        static float[] Parafusos(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(0.7f * Taxa)];
            // a catraca girando (os cliques num ritmo) e uma pancadinha no fim
            float t = 0.03f;
            while (t < 0.5f)
            {
                Clique(d, r, t, 2600f + (float)r.NextDouble() * 500f, 0.22f);
                t += 0.045f + (float)r.NextDouble() * 0.02f;
            }
            Pancada(d, r, 0.52f, 420f, 0.25f, 2.5f);
            return Fechar(d, 0.6f);
        }
    }
}
