using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da mente (06/11/2026, a sanidade, a etapa 2: a crise de pânico e as alucinações):
    /// - a palpitação: o "tum-tum" do coração no peito, tocado em cada batida do monitor (Player/CriseDePanico): o gravado;
    /// - o sussurro: o ar de uma voz sem palavras, que vem de onde o vulto estava (Player/Alucinacoes): os gravados;
    /// - o desmaio: o baque abafado e o zumbido agudo do ouvido tampando, como o do nocaute (Player/ColapsoDoJogador),
    ///   gerado por código no jeito do <see cref="SomDoCorpo"/>, uma vez só, na primeira vez que toca.
    /// (20/11/2026: a palpitação e o sussurro feitos em código, a reserva dos gravados, saíram; sem eles, null.)
    /// </summary>
    public static class SomDaMente
    {
        const int Taxa = 44100;
        static AudioClip _desmaio;

        /// <summary>A palpitação: duas batidas surdas no peito (o gravado; null sem ele).</summary>
        public static AudioClip Coracao => SonsGravados.Um("Corpo", "coracao_");   // (20/11/2026: o gravado)
        /// <summary>O sussurro: o ar de uma voz, sem palavras (os gravados, sorteados; null sem eles).</summary>
        public static AudioClip Sussurro => SonsGravados.Um("Corpo", "sussurro_");   // (20/11/2026: os gravados, sorteados)
        /// <summary>O desmaio: o baque fundo e o zumbido do ouvido tampando, que some devagar (3,5 s).</summary>
        public static AudioClip Desmaio => _desmaio != null ? _desmaio : (_desmaio = SomGerado.Clip("Desmaio", GerarDesmaio(307), Taxa));

        // o desmaio: o baque fundo (o seno de 38 Hz que cai) e o zumbido de 3,4 kHz que fica e some devagar
        static float[] GerarDesmaio(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(3.5f * Taxa)];
            float fase = 0f, faseZ = 0f, surdo = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                fase += 2f * Mathf.PI * 38f * (1f + 0.8f * Mathf.Exp(-t * 12f)) / Taxa;
                surdo += 0.03f * ((float)(r.NextDouble() * 2 - 1) - surdo);
                float baque = (Mathf.Sin(fase) + surdo * 2.5f) * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 4f);
                faseZ += 2f * Mathf.PI * (3400f + 30f * Mathf.Sin(t * 5f)) / Taxa;
                float zumbido = Mathf.Sin(faseZ) * 0.22f * Mathf.Clamp01((t - 0.08f) / 0.4f) * Mathf.Clamp01((3.5f - t) / 2.6f);
                y[i] = baque + zumbido;
            }
            return Normalizar(y, 0.85f);
        }

        static float[] Normalizar(float[] y, float pico)
        {
            float max = 1e-6f;
            for (int i = 0; i < y.Length; i++) max = Mathf.Max(max, Mathf.Abs(y[i]));
            float k = pico / max;
            for (int i = 0; i < y.Length; i++) y[i] *= k;
            return y;
        }
    }
}
