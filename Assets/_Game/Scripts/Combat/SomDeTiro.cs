using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O som do tiro, gerado por código em camadas: o estalo (ruído agudo de poucos milissegundos), o soco (um grave que
    /// cai de tom), o corpo (ruído na faixa média), o mecanismo (os cliques do ferrolho) e a cauda (o ronco que a cidade
    /// devolve, com dois rebatimentos). Cada camada é normalizada pela energia e entra com o peso do perfil (o quadrado
    /// do peso é a parte da energia); no fim, uma saturação suave deixa o som denso e o pico vai a 0,95.
    /// Cada perfil sai em algumas variações, sorteadas a cada tiro. Há um perfil por arma (veja <see cref="Armas"/>).
    /// </summary>
    public static class SomDeTiro
    {
        const int Taxa = 44100;

        public class Perfil
        {
            public float duracao = 1.2f;
            public float estalo = 0.5f, estaloDecai = 0.006f, estaloFreq = 3000f, estaloQ = 0.7f;
            public float soco = 0.55f, socoDe = 160f, socoPara = 60f, socoQueda = 0.015f, socoDecai = 0.045f;
            public float corpo = 0.65f, corpoFreq = 700f, corpoQ = 0.5f, corpoDecai = 0.04f;
            public float[] mecanismo = { 0.048f, 0.09f };
            public float mecanismoVolume = 0.15f, mecanismoFreq = 4300f;
            public float cauda = 0.45f, caudaDecai = 0.34f, caudaCorte = 1200f;
            public float[] ecos = { 0.13f, 0.29f }, ecosVolume = { 0.2f, 0.09f };
            public float saturacao = 2.5f;
        }

        public static readonly Perfil Pistola = new Perfil();

        /// <summary>O rifle: estalo mais agudo e forte, corpo mais alto, ferrolho rápido e uma cauda mais longa.</summary>
        public static readonly Perfil Rifle = new Perfil
        {
            duracao = 1.4f, estalo = 0.6f, estaloDecai = 0.005f, estaloFreq = 3800f, estaloQ = 0.8f,
            soco = 0.5f, socoDe = 190f, socoPara = 70f, socoQueda = 0.012f, socoDecai = 0.04f,
            corpo = 0.6f, corpoFreq = 950f, corpoQ = 0.6f, corpoDecai = 0.035f,
            mecanismo = new[] { 0.03f }, mecanismoVolume = 0.08f, mecanismoFreq = 4600f,
            cauda = 0.5f, caudaDecai = 0.42f, caudaCorte = 1600f, ecos = new[] { 0.14f, 0.31f }, ecosVolume = new[] { 0.2f, 0.1f },
            saturacao = 2.8f,
        };

        /// <summary>A submetralhadora: parecida com a pistola, mais seca e mais curta (atira muito rápido).</summary>
        public static readonly Perfil Submetralhadora = new Perfil
        {
            duracao = 0.9f, estalo = 0.5f, estaloDecai = 0.005f, estaloFreq = 3200f,
            soco = 0.5f, socoDe = 170f, socoPara = 65f, socoQueda = 0.012f, socoDecai = 0.035f,
            corpo = 0.6f, corpoFreq = 800f, corpoQ = 0.5f, corpoDecai = 0.03f,
            mecanismo = new[] { 0.025f }, mecanismoVolume = 0.1f, mecanismoFreq = 4800f,
            cauda = 0.38f, caudaDecai = 0.25f, caudaCorte = 1300f, ecos = new[] { 0.12f, 0.27f }, ecosVolume = new[] { 0.16f, 0.07f },
            saturacao = 2.5f,
        };

        /// <summary>A espingarda: um estrondo grave e comprido, sem ferrolho (a bomba é um som à parte).</summary>
        public static readonly Perfil Espingarda = new Perfil
        {
            duracao = 1.6f, estalo = 0.4f, estaloDecai = 0.008f, estaloFreq = 2400f,
            soco = 0.7f, socoDe = 130f, socoPara = 42f, socoQueda = 0.02f, socoDecai = 0.08f,
            corpo = 0.6f, corpoFreq = 520f, corpoQ = 0.5f, corpoDecai = 0.06f,
            mecanismo = new float[0], mecanismoVolume = 0f,
            cauda = 0.6f, caudaDecai = 0.5f, caudaCorte = 1000f, ecos = new[] { 0.15f, 0.34f }, ecosVolume = new[] { 0.25f, 0.12f },
            saturacao = 3.2f,
        };

        public static AudioClip[] Gerar(string nome, Perfil p, int variacoes = 3)
        {
            var clips = new AudioClip[variacoes];
            for (int v = 0; v < variacoes; v++)
            {
                var dados = Amostras(p, nome.GetHashCode() + v * 7919);
                clips[v] = AudioClip.Create($"{nome} {v + 1}", dados.Length, 1, Taxa, false);
                clips[v].SetData(dados, 0);
            }
            return clips;
        }

        public static float[] Amostras(Perfil p, int semente)
        {
            int n = Mathf.RoundToInt(p.duracao * Taxa);
            var r = new System.Random(semente);
            float Ruido() => (float)(r.NextDouble() * 2 - 1);
            float Variar(float valor, float quanto) => valor * (1f + ((float)r.NextDouble() * 2f - 1f) * quanto);
            float socoDe = Variar(p.socoDe, 0.06f), socoPara = Variar(p.socoPara, 0.06f);
            float corpoFreq = Variar(p.corpoFreq, 0.1f), mecFreq = Variar(p.mecanismoFreq, 0.08f);

            float[] estalo = new float[n], soco = new float[n], corpo = new float[n], mec = new float[n], cauda = new float[n];
            float fase = 0f, c1 = 0f, c2 = 0f;
            var agudo = new PassaFaixa(Variar(p.estaloFreq, 0.08f), p.estaloQ);
            var passaFaixa = new PassaFaixa(corpoFreq, p.corpoQ);
            var metal = new PassaFaixa(mecFreq, 5f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                // estalo: ruído agudo, quase instantâneo
                estalo[i] = agudo.Passar(Ruido()) * Mathf.Exp(-t / p.estaloDecai);
                // soco: um seno que cai de tom (o grave da explosão)
                float f = socoPara + (socoDe - socoPara) * Mathf.Exp(-t / p.socoQueda);
                fase += 2f * Mathf.PI * f / Taxa;
                soco[i] = Mathf.Sin(fase) * Mathf.Exp(-t / p.socoDecai) * Mathf.Clamp01(t / 0.0008f);
                // corpo: ruído na faixa média
                corpo[i] = passaFaixa.Passar(Ruido()) * Mathf.Exp(-t / p.corpoDecai);
                // mecanismo: cliques metálicos (ruído estreito e um tilintar)
                float m = 0f;
                foreach (float tk in p.mecanismo)
                {
                    float d = t - tk;
                    if (d < 0f || d > 0.04f) continue;
                    m += Ruido() * Mathf.Exp(-d / 0.003f) + Mathf.Sin(2f * Mathf.PI * mecFreq * 1.37f * d) * Mathf.Exp(-d / 0.006f) * 0.5f;
                }
                mec[i] = metal.Passar(m) + m * 0.15f;
                // cauda: ruído grave que escurece e morre devagar
                float corte = p.caudaCorte * (0.35f + 0.65f * Mathf.Exp(-t / 0.3f));
                float a = Coef(corte);
                c1 += a * (Ruido() - c1); c2 += a * (c1 - c2);
                cauda[i] = c2 * (1f - Mathf.Exp(-t / 0.012f)) * Mathf.Exp(-t / p.caudaDecai);
            }
            foreach (var camada in new[] { estalo, soco, corpo, mec, cauda }) PelaEnergia(camada);

            var seco = new float[n];
            for (int i = 0; i < n; i++) seco[i] = estalo[i] * p.estalo + soco[i] * p.soco + corpo[i] * p.corpo;
            // os rebatimentos: o tiro abafado, voltando das paredes da cidade
            var abafado = new float[n];
            float e1 = 0f, e2 = 0f, aEco = Coef(1400f);
            for (int i = 0; i < n; i++) { e1 += aEco * (seco[i] - e1); e2 += aEco * (e1 - e2); abafado[i] = e2; }

            var atrasos = new int[p.ecos.Length];
            for (int k = 0; k < atrasos.Length; k++) atrasos[k] = Mathf.RoundToInt(Variar(p.ecos[k], 0.04f) * Taxa);
            var saida = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = seco[i] + mec[i] * p.mecanismoVolume + cauda[i] * p.cauda;
                for (int k = 0; k < atrasos.Length; k++)
                    if (i >= atrasos[k]) s += abafado[i - atrasos[k]] * p.ecosVolume[k];
                saida[i] = s;
            }
            Normalizar(saida);
            float tanh = Tanh(p.saturacao), dc = 0f, aDc = Coef(25f);
            int fim = Mathf.RoundToInt(0.08f * Taxa);
            for (int i = 0; i < n; i++)
            {
                float s = Tanh(saida[i] * p.saturacao) / tanh;
                dc += aDc * (s - dc);
                s -= dc;                                                // tira o deslocamento do grave
                if (i > n - fim) s *= (n - i) / (float)fim;              // some no fim, sem estalo
                saida[i] = s;
            }
            Normalizar(saida, 0.95f);
            return saida;
        }

        static float Coef(float corte) => 1f - Mathf.Exp(-2f * Mathf.PI * corte / Taxa);

        static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }

        /// <summary>Deixa a soma dos quadrados em 1 (a energia), para os pesos do perfil valerem entre camadas.</summary>
        static void PelaEnergia(float[] dados)
        {
            double soma = 0;
            foreach (float d in dados) soma += d * d;
            if (soma < 1e-12) return;
            float k = (float)(1.0 / System.Math.Sqrt(soma));
            for (int i = 0; i < dados.Length; i++) dados[i] *= k;
        }

        static void Normalizar(float[] dados, float pico = 1f)
        {
            float max = 0f;
            foreach (float d in dados) max = Mathf.Max(max, Mathf.Abs(d));
            if (max < 1e-6f) return;
            float k = pico / max;
            for (int i = 0; i < dados.Length; i++) dados[i] *= k;
        }

        /// <summary>Passa-faixa de segunda ordem (biquad, ganho 0 dB no centro).</summary>
        struct PassaFaixa
        {
            readonly float b0, b2, a1, a2;
            float x1, x2, y1, y2;

            public PassaFaixa(float freq, float q)
            {
                float w = 2f * Mathf.PI * freq / Taxa, alfa = Mathf.Sin(w) / (2f * q), a0 = 1f + alfa;
                b0 = alfa / a0; b2 = -alfa / a0; a1 = -2f * Mathf.Cos(w) / a0; a2 = (1f - alfa) / a0;
                x1 = x2 = y1 = y2 = 0f;
            }

            public float Passar(float x)
            {
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
