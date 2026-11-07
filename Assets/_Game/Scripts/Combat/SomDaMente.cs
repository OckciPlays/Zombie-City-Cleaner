using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da mente (06/11/2026, a sanidade, a etapa 2: a crise de pânico e as alucinações), gerados por código no jeito
    /// do <see cref="SomDoCorpo"/>, cada um uma vez só, na primeira vez que toca:
    /// - a palpitação: o "tum-tum" do coração no peito, tocado em cada batida do monitor (Player/CriseDePanico);
    /// - o sussurro: o ar de uma voz sem palavras, que vem de onde o vulto estava (Player/Alucinacoes);
    /// - o desmaio: o baque abafado e o zumbido agudo do ouvido tampando, como o do nocaute (Player/ColapsoDoJogador).
    /// </summary>
    public static class SomDaMente
    {
        const int Taxa = 44100;
        static AudioClip _coracao, _sussurro, _desmaio;

        /// <summary>A palpitação: duas batidas surdas no peito (a primeira mais forte), uns 0,45 s.</summary>
        public static AudioClip Coracao => _coracao != null ? _coracao : (_coracao = SomGerado.Clip("Palpitação", GerarCoracao(), Taxa));
        /// <summary>O sussurro: o ar de uma voz, em sílabas, sem palavras (1,5 s).</summary>
        public static AudioClip Sussurro => _sussurro != null ? _sussurro : (_sussurro = SomGerado.Clip("Sussurro", GerarSussurro(211), Taxa));
        /// <summary>O desmaio: o baque fundo e o zumbido do ouvido tampando, que some devagar (3,5 s).</summary>
        public static AudioClip Desmaio => _desmaio != null ? _desmaio : (_desmaio = SomGerado.Clip("Desmaio", GerarDesmaio(307), Taxa));

        // uma batida: um seno grave que cai de altura e morre rápido, com um pouco de ruído surdo por cima
        static void Batida(float[] y, float inicio, float forca, float freq, System.Random r)
        {
            int i0 = Mathf.RoundToInt(inicio * Taxa), n = Mathf.RoundToInt(0.16f * Taxa);
            float fase = 0f, surdo = 0f;
            for (int k = 0; k < n && i0 + k < y.Length; k++)
            {
                float t = k / (float)Taxa;
                float f = freq * (1f + 0.6f * Mathf.Exp(-t * 40f));
                fase += 2f * Mathf.PI * f / Taxa;
                surdo += 0.08f * ((float)(r.NextDouble() * 2 - 1) - surdo);
                float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * 26f);
                y[i0 + k] += (Mathf.Sin(fase) + surdo * 0.8f) * env * forca;
            }
        }

        static float[] GerarCoracao()
        {
            var r = new System.Random(97);
            var y = new float[Mathf.RoundToInt(0.45f * Taxa)];
            Batida(y, 0f, 1f, 52f, r);
            Batida(y, 0.13f, 0.7f, 64f, r);
            return Normalizar(y, 0.9f);
        }

        // o sussurro: ruído com o chiado da voz (o passa-alta e o passa-baixa), em sílabas de 120 a 220 ms
        static float[] GerarSussurro(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(1.5f * Taxa)];
            float grave = 0f, macio = 0f;
            float aGrave = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / Taxa), aMacio = 1f - Mathf.Exp(-2f * Mathf.PI * 5200f / Taxa);
            float silaba = 0f, duracao = 0.15f, forca = 0.8f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                if (t - silaba > duracao)
                {
                    silaba = t;
                    duracao = 0.12f + (float)r.NextDouble() * 0.1f;
                    forca = r.NextDouble() < 0.2 ? 0.15f : 0.55f + (float)r.NextDouble() * 0.45f;
                }
                float x = (float)(r.NextDouble() * 2 - 1);
                grave += aGrave * (x - grave);
                macio += aMacio * ((x - grave) - macio);
                float s = (t - silaba) / duracao;
                float env = Mathf.Sin(Mathf.Clamp01(s) * Mathf.PI) * forca;
                float geral = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((1.5f - t) / 0.3f);
                y[i] = macio * env * geral;
            }
            return Normalizar(y, 0.7f);
        }

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
