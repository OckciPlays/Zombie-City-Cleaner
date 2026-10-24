using UnityEngine;
using Faixa = ZombieCityCleaner.Combat.SomDePasso.Faixa;
using Ressonador = ZombieCityCleaner.Combat.SomDePasso.Ressonador;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da comida e da água, gerados por código: abrir a lata (o anel estala, o ar escapa e a tampa rasga), mastigar,
    /// abrir a garrafa (o lacre estalando dente a dente e o ar), o gole e o baque de largar uma coisa no chão.
    /// </summary>
    public static class SomDosItens
    {
        const int Taxa = 44100;

        // a tampa de lata: modos de chapa fina e aguda
        static readonly (float f, float d, float g)[] Chapa = { (2150f, 0.025f, 1f), (3380f, 0.02f, 0.8f), (5100f, 0.012f, 0.5f), (7300f, 0.008f, 0.3f) };

        /// <summary>O anel da lata estalando, o "pss" do ar e a tampa rasgando em volta.</summary>
        public static float[] AbrirLata(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.5f * Taxa)];
            var chapa = Banco(Chapa, r);
            var ar = new Faixa(4200f, 0.8f);
            var rasga = new Faixa(2600f, 1.5f);
            var macio = new SomDePasso.PassaBaixa(7000f, 2);   // sem ele, o "pss" chiava (70% da energia acima de 8 kHz)
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                // o estalo do anel (em 20 ms) e o da tampa soltando no fim (em 0,4 s)
                float golpe = Impulso(t, 0.02f, 0.002f) + 0.6f * Impulso(t, 0.40f, 0.002f);
                float x = Ruido(r) * golpe;
                float metal = 0f;
                foreach (var m in chapa) metal += m.Passar(x);
                // o ar escapando, logo depois do anel
                float pss = t > 0.03f ? Mathf.Exp(-(t - 0.03f) / 0.05f) * Mathf.Clamp01((t - 0.03f) / 0.005f) : 0f;
                // a tampa rasgando: grãos de metal em rajadas entre 0,12 e 0,38 s
                float rasgando = t > 0.12f && t < 0.38f ? 0.6f + 0.4f * Mathf.Sin(t * 70f) : 0f;
                float grao = r.NextDouble() < 0.004 * rasgando ? Ruido(r) * 3f : 0f;
                y[i] = macio.Passar(metal * 1.5f + ar.Passar(Ruido(r) * pss * 0.6f) + rasga.Passar(grao));
            }
            return Acabar(y, 0.8f);
        }

        /// <summary>
        /// Uma bocada de comida de lata (macia e molhada): o maxilar fecha três vezes, e cada vez sai um "nhac" abafado
        /// (ruído que sobe e desce, com o centro descendo de ~1 kHz para ~450 Hz, a boca fechando), uns estalinhos de
        /// saliva e o baque surdo da mandíbula.
        /// </summary>
        public static float[] Mastigar(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.42f * Taxa)];
            var boca = new Faixa(900f, 1.4f);
            var saliva = new Faixa(3400f, 2f);
            var abafa = new SomDePasso.PassaBaixa(2200f);
            float[] mordidas = { 0.01f, 0.13f + (float)r.NextDouble() * 0.03f, 0.26f + (float)r.NextDouble() * 0.04f };
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float env = 0f, mand = 0f, fase = 1f;
                for (int k = 0; k < mordidas.Length; k++)
                {
                    float d = t - mordidas[k];
                    if (d < 0f) continue;
                    float forca = k == 0 ? 1f : 0.75f;
                    float e = forca * Mathf.Clamp01(d / 0.015f) * Mathf.Exp(-d / 0.045f);
                    if (e > env) { env = e; fase = Mathf.Clamp01(d / 0.07f); }
                    mand += forca * Mathf.Sin(2f * Mathf.PI * 95f * d) * Mathf.Exp(-d / 0.025f);
                }
                boca.Centro(Mathf.Lerp(1050f, 450f, fase));
                float estalo = r.NextDouble() < 0.003 * env ? Ruido(r) * 1.5f : 0f;
                y[i] = abafa.Passar(boca.Passar(Ruido(r) * env) * 1.2f + saliva.Passar(estalo) * 0.5f) + mand * 0.18f;
            }
            return Acabar(y, 0.6f);
        }

        /// <summary>A tampa da garrafa girando: o lacre estala dente a dente, e o ar sai.</summary>
        public static float[] AbrirTampa(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.42f * Taxa)];
            var plastico = new Ressonador[] { new Ressonador(3600f, 0.004f, 1f), new Ressonador(4700f, 0.003f, 0.7f), new Ressonador(2300f, 0.005f, 0.5f) };
            var ar = new Faixa(5500f, 0.5f);
            // os dentes do lacre: 7 estalos, cada vez mais juntos (a mão acelera)
            var dentes = new float[7];
            float tempo = 0.02f;
            for (int k = 0; k < dentes.Length; k++)
            {
                dentes[k] = tempo;
                tempo += 0.045f - k * 0.004f + (float)r.NextDouble() * 0.01f;
            }
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = 0f;
                for (int k = 0; k < dentes.Length; k++) golpe += Impulso(t, dentes[k], 0.0012f) * (0.6f + 0.4f * (k % 2));
                float x = Ruido(r) * golpe;
                float clique = 0f;
                foreach (var m in plastico) clique += m.Passar(x);
                float fim = tempo + 0.01f;
                float pss = t > fim ? Mathf.Exp(-(t - fim) / 0.04f) * Mathf.Clamp01((t - fim) / 0.005f) : 0f;
                y[i] = clique * 2f + ar.Passar(Ruido(r) * pss * 0.35f);
            }
            return Acabar(y, 0.6f);
        }

        /// <summary>Um gole: a bolha que sobe na garganta (um tom que sobe depressa), o baque grave e o líquido mexendo.</summary>
        public static float[] Gole(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.24f * Taxa)];
            var liquido = new Faixa(520f, 1.5f);
            float fase = 0f, f0 = 150f + (float)r.NextDouble() * 30f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float f = f0 + 280f * Mathf.Clamp01(t / 0.07f);
                fase += 2f * Mathf.PI * f / Taxa;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t / 0.06f);
                float baque = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / 0.03f);
                y[i] = Mathf.Sin(fase) * env * 0.8f + baque * 0.4f + liquido.Passar(Ruido(r) * env) * 0.5f;
            }
            return Acabar(y, 0.7f);
        }

        /// <summary>Uma coisa caindo no chão: o baque abafado e um chacoalho curto.</summary>
        public static float[] Largar(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.3f * Taxa)];
            var corpo = new Faixa(380f, 0.8f);
            var chacoalho = new Faixa(2400f, 1.5f);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = Impulso(t, 0.005f, 0.012f) + 0.35f * Impulso(t, 0.07f, 0.008f);
                float baque = t > 0.005f ? Mathf.Sin(2f * Mathf.PI * 105f * (t - 0.005f)) * Mathf.Exp(-(t - 0.005f) / 0.04f) : 0f;
                float grao = r.NextDouble() < 0.003 * golpe ? Ruido(r) * 2f : 0f;
                y[i] = corpo.Passar(Ruido(r) * golpe) * 1.5f + baque * 0.5f + chacoalho.Passar(grao);
            }
            return Acabar(y, 0.7f);
        }

        public static AudioClip Clip(string nome, float[] dados) => SomGerado.Clip(nome, dados, Taxa);

        static Ressonador[] Banco((float f, float d, float g)[] modos, System.Random r)
        {
            var banco = new Ressonador[modos.Length];
            for (int k = 0; k < modos.Length; k++)
                banco[k] = new Ressonador(modos[k].f * (0.97f + 0.06f * (float)r.NextDouble()), modos[k].d, modos[k].g);
            return banco;
        }

        // um impulso que nasce em 'em' e morre em 'tau' segundos
        static float Impulso(float t, float em, float tau) => t < em ? 0f : Mathf.Exp(-(t - em) / tau);

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
