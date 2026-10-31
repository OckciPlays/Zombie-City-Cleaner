using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os sons da sinuca, gerados por código (como os outros do jogo, pelo <see cref="SomGerado"/>): a tacada (o couro da
    /// ponta na branca: um estalo seco e o oco da madeira), a batida entre as bolas (o clique agudo da resina, 3
    /// variações), a bola na tabela (o baque abafado da borracha) e a caçapa (a bola caindo no bolso e rolando um pouco).
    /// </summary>
    public static class SomDaSinuca
    {
        static AudioClip _tacada, _tabela, _cacapa;
        static AudioClip[] _bolas;

        public static AudioClip Tacada => _tacada ??= SomGerado.Clip("Sinuca: tacada", Tacar(81));
        public static AudioClip Tabela => _tabela ??= SomGerado.Clip("Sinuca: tabela", NaTabela(82));
        public static AudioClip Cacapa => _cacapa ??= SomGerado.Clip("Sinuca: caçapa", NaCacapa(83));

        public static AudioClip[] Bolas
        {
            get
            {
                if (_bolas != null) return _bolas;
                _bolas = new AudioClip[3];
                for (int i = 0; i < 3; i++) _bolas[i] = SomGerado.Clip($"Sinuca: bola {i + 1}", Bola(84 + i, 1f + i * 0.06f));
                return _bolas;
            }
        }

        const int Taxa = 44100;

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // o clique das bolas: dois modos agudos da resina (que somem em ~30 ms) e um pouco de ruído no impacto
        static float[] Bola(int semente, float tom)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.09f * Taxa);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i] = Mathf.Sin(2f * Mathf.PI * 3150f * tom * t) * Mathf.Exp(-t * 110f) * 0.55f
                     + Mathf.Sin(2f * Mathf.PI * 5300f * tom * t) * Mathf.Exp(-t * 170f) * 0.25f
                     + Ruido(r) * Mathf.Exp(-t * 900f) * 0.35f;
            }
            return d;
        }

        // a tacada: o estalo do couro e o "toc" grave da madeira
        static float[] Tacar(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.12f * Taxa);
            var d = new float[n];
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                f = Mathf.Lerp(f, Ruido(r), 0.4f);
                d[i] = f * Mathf.Exp(-t * 260f) * 0.7f
                     + Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 55f) * 0.35f
                     + Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 140f) * 0.2f;
            }
            return d;
        }

        // a tabela: o baque abafado (ruído grave) e o corpo da madeira embaixo da borracha
        static float[] NaTabela(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.14f * Taxa);
            var d = new float[n];
            float f = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                f = Mathf.Lerp(f, Ruido(r), 0.08f);
                d[i] = f * Mathf.Exp(-t * 45f) * 1.6f + Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 35f) * 0.45f;
            }
            return d;
        }

        // a caçapa: o baque da bola no bolso e ela rolando um pouco no couro, cada vez mais longe
        static float[] NaCacapa(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.45f * Taxa);
            var d = new float[n];
            float f = 0f, g = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                f = Mathf.Lerp(f, Ruido(r), 0.06f);
                g = Mathf.Lerp(g, Ruido(r), 0.2f);
                float baque = f * Mathf.Exp(-t * 30f) * 1.8f + Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 25f) * 0.5f;
                float rolar = t > 0.05f ? g * 0.18f * Mathf.Exp(-(t - 0.05f) * 6f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 18f * t)) : 0f;
                d[i] = baque + rolar;
            }
            return d;
        }
    }
}
