using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons da porta blindada do bunker, gerados por código com uma física simples de metal:
    /// - a folha de aço (grossa e pesada) e as peças da tranca são bancos de ressonadores: cada modo de vibração é um seno
    ///   que morre sozinho, em frequências fora da série harmônica (é isso que soa "metal"). Os graves da folha duram mais;
    ///   as peças pequenas tilintam;
    /// - as batidas tocam esses ressonadores com um impulso curto de ruído, mais um grave seco (o tranco no batente);
    /// - o rangido das dobradiças é atrito de gruda e solta: pulsos num ritmo que sobe com a velocidade da folha, cada um
    ///   tocando ressonadores estreitos (a dobradiça) e um pouco da folha. Por baixo, o ronco grave do peso andando.
    /// Os tempos batem com o movimento da <see cref="ZombieCityCleaner.World.PortaDoBunker"/>: ela espera
    /// <see cref="Destrancar"/> antes de abrir, abre em <see cref="TempoAbrir"/> (devagar no começo e no fim) e fecha em
    /// <see cref="TempoFechar"/>, ganhando velocidade até bater.
    /// </summary>
    public static class SomDaPorta
    {
        const int Taxa = 44100;

        /// <summary>O volante girando e as trancas soltando, antes de a folha andar (s).</summary>
        public const float Destrancar = 0.75f;
        public const float TempoAbrir = 1.8f, TempoFechar = 1.5f;

        // os modos (frequência em Hz, quanto dura em s, peso): a folha de aço, grossa (um "bum" grave); as peças da tranca
        // e do batente (o "clanc"); a dobradiça; e a lingueta da catraca do volante
        static readonly (float f, float d, float g)[] Folha =
        {
            (48f, 0.55f, 1f), (79f, 0.5f, 0.85f), (113f, 0.45f, 0.7f), (157f, 0.4f, 0.55f), (204f, 0.35f, 0.45f),
            (271f, 0.3f, 0.38f), (347f, 0.26f, 0.3f), (433f, 0.22f, 0.25f), (561f, 0.18f, 0.2f), (712f, 0.15f, 0.16f),
            (905f, 0.12f, 0.12f), (1180f, 0.09f, 0.09f), (1530f, 0.07f, 0.07f), (2050f, 0.05f, 0.05f),
        };
        static readonly (float f, float d, float g)[] Tranca =
        {
            (310f, 0.25f, 1f), (520f, 0.22f, 0.8f), (787f, 0.2f, 0.7f), (1130f, 0.16f, 0.6f), (1690f, 0.12f, 0.45f),
            (2410f, 0.09f, 0.35f), (3320f, 0.06f, 0.25f),
        };
        // a dobradiça: ressonâncias curtas (abafadas), para o ritmo do atrito ser o que se ouve, e não um tom parado
        static readonly (float f, float d, float g)[] Dobradica =
        {
            (260f, 0.008f, 1f), (540f, 0.007f, 0.8f), (930f, 0.006f, 0.6f), (1520f, 0.005f, 0.4f), (2300f, 0.004f, 0.25f),
        };
        static readonly (float f, float d, float g)[] Lingueta = { (1450f, 0.02f, 1f), (2300f, 0.015f, 0.7f), (3700f, 0.01f, 0.5f) };

        /// <summary>Destrancar: a mão no volante, o volante girando (a catraca pesada e o eixo raspando) e as duas trancas
        /// soltando (um deslizar e o "tunc").</summary>
        public static float[] Destranca(int semente)
        {
            var r = new System.Random(semente);
            var saida = new float[Mathf.RoundToInt(1.4f * Taxa)];
            var folha = new Banco(Folha, r, 0.03f);
            var tranca = new Banco(Tranca, r, 0.04f);
            var lingueta = new Banco(Lingueta, r, 0.05f);
            var raspa = new PassaFaixa(520f, 1.2f);
            var desliza = new PassaFaixa(1900f, 1f);
            var cliques = Tempos(r, 0.07f, 0.46f, 0.05f, 0.07f);
            float macio = 0f, aMacio = Coef(2500f);
            for (int i = 0; i < saida.Length; i++)
            {
                float t = i / (float)Taxa;
                float clique = 0f;
                foreach (float c in cliques) clique += Pancada(t, c, 0.8f, 0.0008f, r);
                float golpe = Pancada(t, 0f, 0.25f, 0.002f, r) + Pancada(t, 0.55f, 1f, 0.003f, r) + Pancada(t, 0.68f, 0.8f, 0.003f, r);
                macio += aMacio * (golpe - macio);
                float s = lingueta.Passar(clique) * 0.6f + tranca.Passar(macio + clique * 0.25f) * 0.9f + folha.Passar(macio * 0.5f) * 0.6f;
                s += raspa.Passar(Ruido(r)) * Janela(t, 0.05f, 0.48f) * 0.05f;
                s += desliza.Passar(Ruido(r)) * (Rampa(t, 0.48f, 0.55f) + Rampa(t, 0.61f, 0.68f) * 0.8f) * 0.12f;
                s += Tranco(t, 0.55f, 0.5f) + Tranco(t, 0.68f, 0.4f);
                saida[i] = s;
            }
            return Acabar(saida, 0.75f, 1.2f);
        }

        /// <summary>A folha abrindo: devagar no começo e no fim (a velocidade de um SmoothStep).</summary>
        public static float[] Abrindo(int semente) => Rangido(TempoAbrir, u => 4f * u * (1f - u), semente);

        /// <summary>A folha fechando: ganha velocidade até bater (o som acaba no instante da batida).</summary>
        public static float[] Fechando(int semente) => Rangido(TempoFechar, u => u, semente);

        /// <summary>
        /// A folha andando: o rangido das dobradiças (gruda e solta: estalos secos quase parada, um "rrraau" grave quando
        /// ela anda rápido, mais regular), um ou dois guinchos curtos de metal no meio do caminho e o ronco grave do
        /// peso. velocidade(u) dá a velocidade (0 a 1) no ponto u (0 a 1) do movimento.
        /// </summary>
        static float[] Rangido(float duracao, System.Func<float, float> velocidade, int semente)
        {
            var r = new System.Random(semente);
            var saida = new float[Mathf.RoundToInt((duracao + 0.12f) * Taxa)];
            var dobradica = new Banco(Dobradica, r, 0.05f);
            var folha = new Banco(Folha, r, 0.03f);
            // os guinchos: onde (em u), quanto duram, o tom e quanto ele escorrega
            int quantos = 1 + r.Next(2);
            var guinchos = new (float u, float dur, float f, float desliza)[quantos];
            for (int k = 0; k < quantos; k++)
                guinchos[k] = (0.3f + 0.4f * (k + (float)r.NextDouble()) / quantos, 0.14f + 0.14f * (float)r.NextDouble(),
                               900f + 400f * (float)r.NextDouble(), Ruido(r) * 0.12f);
            float fase = 0f, deriva = 0f, r1 = 0f, r2 = 0f, aRonco = Coef(80f);
            var faseGuincho = new float[quantos];
            for (int i = 0; i < saida.Length; i++)
            {
                float t = i / (float)Taxa, u = t / duracao;
                float v = u <= 1f ? velocidade(u) : 0f;
                // o ritmo do atrito: de ~30 pulsos por segundo (quase parada: estalos) a ~120 (rápida: um ronco de
                // rangido), com uma deriva lenta
                deriva = Mathf.Clamp(deriva * 0.9999f + Ruido(r) * 0.0015f, -0.25f, 0.25f);
                fase += (30f + 90f * v) * (1f + deriva) / Taxa;
                float pulso = 0f;
                if (fase >= 1f)
                {
                    fase -= 1f + Ruido(r) * 0.2f * (1f - 0.6f * v);
                    if (v > 0.02f) pulso = (0.5f + 0.5f * (float)r.NextDouble()) * (0.35f + 0.65f * v);
                }
                float s = dobradica.Passar(pulso) + folha.Passar(pulso * 0.05f) * 0.5f;
                // o guincho: um tom que escorrega, com dois harmônicos, que sobe e some
                for (int k = 0; k < quantos; k++)
                {
                    var g = guinchos[k];
                    float d = (t - g.u * duracao) / g.dur;
                    if (d < 0f || d > 1f) continue;
                    faseGuincho[k] += g.f * (1f + g.desliza * d + 0.015f * Mathf.Sin(2f * Mathf.PI * 7f * t)) / Taxa;
                    float x = 2f * Mathf.PI * faseGuincho[k];
                    s += (Mathf.Sin(x) + 0.45f * Mathf.Sin(2f * x) + 0.2f * Mathf.Sin(3f * x)) * Mathf.Sin(Mathf.PI * d) * 0.25f * v;
                }
                r1 += aRonco * (Ruido(r) - r1); r2 += aRonco * (r1 - r2);
                saida[i] = s + r2 * v * 2f;
            }
            return Acabar(saida, 0.5f, 1f, 0.02f);
        }

        /// <summary>
        /// A batida no batente: o tranco grave, o "bum" da folha e o batente (com dois quiques), e depois a tranca: o
        /// volante girando e as duas trancas entrando.
        /// </summary>
        public static float[] Batida(int semente)
        {
            var r = new System.Random(semente);
            var saida = new float[Mathf.RoundToInt(2.4f * Taxa)];
            var folha = new Banco(Folha, r, 0.03f);
            var batente = new Banco(Tranca, r, 0.04f);
            var lingueta = new Banco(Lingueta, r, 0.05f);
            var raspa = new PassaFaixa(520f, 1.2f);
            var desliza = new PassaFaixa(1900f, 1f);
            var cliques = Tempos(r, 0.5f, 0.85f, 0.05f, 0.07f);
            float macio = 0f, aMacio = Coef(1800f);
            for (int i = 0; i < saida.Length; i++)
            {
                float t = i / (float)Taxa;
                float impacto = Pancada(t, 0f, 1f, 0.004f, r) + Pancada(t, 0.045f, 0.3f, 0.003f, r) + Pancada(t, 0.08f, 0.12f, 0.003f, r);
                float clique = 0f;
                foreach (float c in cliques) clique += Pancada(t, c, 0.7f, 0.0008f, r);
                float trancas = Pancada(t, 0.95f, 1f, 0.003f, r) + Pancada(t, 1.08f, 0.85f, 0.003f, r);
                macio += aMacio * (impacto - macio);
                // a folha leva a pancada abafada (o "bum"); o batente leva também a seca, aço no aço (o "clanc")
                float s = folha.Passar(macio + trancas * 0.4f) + batente.Passar(macio * 0.35f + impacto * 0.3f + trancas + clique * 0.2f) * 0.8f;
                s += lingueta.Passar(clique) * 0.5f;
                s += Tranco(t, 0f, 1f) + Pancada(t, 0f, 0.35f, 0.0006f, r);
                s += raspa.Passar(Ruido(r)) * Janela(t, 0.48f, 0.87f) * 0.05f;
                s += desliza.Passar(Ruido(r)) * (Rampa(t, 0.88f, 0.95f) + Rampa(t, 1.01f, 1.08f) * 0.8f) * 0.12f;
                s += Tranco(t, 0.95f, 0.45f) + Tranco(t, 1.08f, 0.35f);
                saida[i] = s;
            }
            return Acabar(saida, 0.95f, 1.6f);
        }

        /// <summary>Abrir inteiro, como se ouve no jogo (para ouvir fora dele): destrancar e a folha andando.</summary>
        public static float[] AbrirInteiro(int semente) => Juntar((Destranca(semente), 0f), (Abrindo(semente + 1), Destrancar));

        /// <summary>Fechar inteiro: a folha andando, a batida e a tranca.</summary>
        public static float[] FecharInteiro(int semente) => Juntar((Fechando(semente + 2), 0f), (Batida(semente + 3), TempoFechar));

        public static AudioClip Clip(string nome, float[] dados)
        {
            var clip = AudioClip.Create(nome, dados.Length, 1, Taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }

        // ------------------------------------------------------------------ peças

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        static float Coef(float corte) => 1f - Mathf.Exp(-2f * Mathf.PI * corte / Taxa);

        /// <summary>Um impulso de ruído que morre em tau segundos (o que excita os ressonadores).</summary>
        static float Pancada(float t, float em, float forca, float tau, System.Random r)
        {
            float d = t - em;
            if (d < 0f || d > tau * 8f) return 0f;
            return Ruido(r) * Mathf.Exp(-d / tau) * forca;
        }

        /// <summary>O tranco grave de uma coisa pesada batendo: um seno que cai de ~78 para 38 Hz e some em ~0,1 s.</summary>
        static float Tranco(float t, float em, float forca)
        {
            float d = t - em;
            if (d < 0f || d > 0.6f) return 0f;
            float fase = 38f * d + 40f * 0.02f * (1f - Mathf.Exp(-d / 0.02f));
            return Mathf.Sin(2f * Mathf.PI * fase) * Mathf.Exp(-d / 0.09f) * Mathf.Clamp01(d / 0.002f) * forca;
        }

        /// <summary>1 de a até b, com 10 ms de subida e de descida.</summary>
        static float Janela(float t, float a, float b) => Mathf.Clamp01((t - a) / 0.01f) * Mathf.Clamp01((b - t) / 0.01f);

        /// <summary>Sobe de 0 em a até 1 em b e acaba ali (um deslizar que termina numa batida).</summary>
        static float Rampa(float t, float a, float b) => t < a || t > b ? 0f : (t - a) / (b - a);

        /// <summary>Instantes de a até b, separados por intervalos sorteados entre min e max.</summary>
        static List<float> Tempos(System.Random r, float a, float b, float min, float max)
        {
            var lista = new List<float>();
            for (float t = a; t <= b; t += min + (max - min) * (float)r.NextDouble()) lista.Add(t);
            return lista;
        }

        /// <summary>Tira o deslocamento do grave, satura de leve (som denso), some no fim sem estalo e põe o pico.</summary>
        static float[] Acabar(float[] dados, float pico, float saturacao, float fimEm = 0.08f)
        {
            Normalizar(dados, 1f);
            float tanh = Tanh(saturacao), dc = 0f, aDc = Coef(25f);
            int fim = Mathf.Max(1, Mathf.RoundToInt(fimEm * Taxa));
            for (int i = 0; i < dados.Length; i++)
            {
                float s = Tanh(dados[i] * saturacao) / tanh;
                dc += aDc * (s - dc);
                s -= dc;
                if (i > dados.Length - fim) s *= (dados.Length - i) / (float)fim;
                dados[i] = s;
            }
            Normalizar(dados, pico);
            return dados;
        }

        static float[] Juntar(params (float[] dados, float em)[] partes)
        {
            int n = 0;
            foreach (var (d, em) in partes) n = Mathf.Max(n, Mathf.RoundToInt(em * Taxa) + d.Length);
            var saida = new float[n];
            foreach (var (d, em) in partes)
            {
                int k = Mathf.RoundToInt(em * Taxa);
                for (int i = 0; i < d.Length; i++) saida[k + i] += d[i];
            }
            float max = 0f;
            foreach (float s in saida) max = Mathf.Max(max, Mathf.Abs(s));
            if (max > 1f) Normalizar(saida, 1f);
            return saida;
        }

        static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }

        static void Normalizar(float[] dados, float pico)
        {
            float max = 0f;
            foreach (float d in dados) max = Mathf.Max(max, Mathf.Abs(d));
            if (max < 1e-6f) return;
            float k = pico / max;
            for (int i = 0; i < dados.Length; i++) dados[i] *= k;
        }

        /// <summary>
        /// Um banco de ressonadores de segunda ordem, um por modo: um impulso de 1 faz cada um tocar um seno com a
        /// amplitude do peso dele, que some em d segundos. As frequências variam um pouco com a semente.
        /// </summary>
        class Banco
        {
            readonly float[] a1, a2, g, y1, y2;

            public Banco((float f, float d, float g)[] modos, System.Random r, float variar)
            {
                int n = modos.Length;
                a1 = new float[n]; a2 = new float[n]; g = new float[n]; y1 = new float[n]; y2 = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float f = modos[i].f * (1f + Ruido(r) * variar);
                    float raio = Mathf.Exp(-1f / (modos[i].d * Taxa)), w = 2f * Mathf.PI * f / Taxa;
                    a1[i] = 2f * raio * Mathf.Cos(w); a2[i] = -raio * raio; g[i] = modos[i].g * Mathf.Sin(w);
                }
            }

            public float Passar(float x)
            {
                float s = 0f;
                for (int i = 0; i < a1.Length; i++)
                {
                    float y = g[i] * x + a1[i] * y1[i] + a2[i] * y2[i];
                    y2[i] = y1[i]; y1[i] = y;
                    s += y;
                }
                return s;
            }
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
