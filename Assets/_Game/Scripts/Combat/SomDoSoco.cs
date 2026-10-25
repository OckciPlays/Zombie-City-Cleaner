using UnityEngine;
using Faixa = ZombieCityCleaner.Combat.SomDePasso.Faixa;
using PassaBaixa = ZombieCityCleaner.Combat.SomDePasso.PassaBaixa;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do soco, gerados por código: o vento do braço passando (ruído numa faixa que sobe e desce com a
    /// velocidade do punho), a pancada num zumbi (o baque grave do corpo, o tapa da pele e o "chape" molhado) e a batida
    /// dos nós dos dedos numa parede (seca e curta).
    /// </summary>
    public static class SomDoSoco
    {
        const int Taxa = 44100;

        /// <summary>Quando o punho chega no alvo, desde o começo do soco (o quadro 3 do clipe, a 24 fps).</summary>
        public const float Impacto = 3 / 24f;

        /// <summary>O braço cortando o ar: sobe até o golpe e some logo depois.</summary>
        public static float[] Vento(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.32f * Taxa)];
            var faixa = new Faixa(500f, 0.9f);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                // a velocidade do punho: cresce até o golpe (0,125 s) e cai depressa
                float v = t < Impacto ? Mathf.SmoothStep(0f, 1f, t / Impacto) : Mathf.Exp(-(t - Impacto) / 0.05f);
                faixa.Centro(300f + 900f * v);
                y[i] = faixa.Passar(Ruido(r) * v);
            }
            return Acabar(y, 0.5f);
        }

        /// <summary>O soco num zumbi: o baque do corpo, o tapa da pele e a carne molhada.</summary>
        public static float[] NaCarne(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.28f * Taxa)];
            var tapa = new Faixa(1800f, 0.9f);
            var chape = new Faixa(650f, 1.3f);
            var corpo = new PassaBaixa(220f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = Mathf.Exp(-t / 0.006f);
                float molhado = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / 0.04f);
                float baque = Mathf.Sin(2f * Mathf.PI * (110f - 40f * Mathf.Clamp01(t / 0.05f)) * t) * Mathf.Exp(-t / 0.045f);
                y[i] = corpo.Passar(Ruido(r) * golpe) * 3f + tapa.Passar(Ruido(r) * golpe) * 1.6f
                       + chape.Passar(Ruido(r) * molhado) * 1.0f + baque * 0.3f;
            }
            return Acabar(y, 0.8f);
        }

        /// <summary>Os nós dos dedos batendo numa parede: um estalo seco e um baque curto.</summary>
        public static float[] NaParede(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.18f * Taxa)];
            var estalo = new Faixa(1400f, 1f);
            var baque = new PassaBaixa(300f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = Mathf.Exp(-t / 0.004f);
                y[i] = estalo.Passar(Ruido(r) * golpe) * 0.8f + baque.Passar(Ruido(r) * Mathf.Exp(-t / 0.012f)) * 2.5f;
            }
            return Acabar(y, 0.6f);
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        static float[] Acabar(float[] dados, float pico)
        {
            float max = 1e-6f;
            foreach (var v in dados) max = Mathf.Max(max, Mathf.Abs(v));
            float k = pico / max;
            int fim = Mathf.Min(dados.Length, Taxa / 50);
            for (int i = 0; i < dados.Length; i++)
            {
                float rampa = i >= dados.Length - fim ? (dados.Length - 1 - i) / (float)fim : 1f;
                dados[i] *= k * rampa;
            }
            return dados;
        }
    }
}
