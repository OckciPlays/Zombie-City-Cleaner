using UnityEngine;
using Faixa = ZombieCityCleaner.Combat.SomDePasso.Faixa;
using PassaBaixa = ZombieCityCleaner.Combat.SomDePasso.PassaBaixa;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do corpo doente (05/11/2026, a etapa 2 do sistema de vida: a febre e a sepse), gerados por código no jeito
    /// do <see cref="SomDosCurativos"/>, cada um uma vez só, na primeira vez que toca: o calafrio (o ar saindo aos trancos
    /// entre os dentes), os dentes batendo, a respiração tremida da febre e a respiração pesada da sepse. Tocam no dono,
    /// na cabeça (não espaciais), pelo <see cref="Efeitos.Som(AudioClip, Vector3, float, bool, float)"/>; quem toca é o
    /// Player/EfeitosDoCorpo.
    /// </summary>
    public static class SomDoCorpo
    {
        const int Taxa = 44100;

        static AudioClip _calafrio, _dentes, _respiracao, _pesada;

        /// <summary>O calafrio: o ar saindo aos trancos entre os dentes cerrados ("brrr"), junto com a onda do tremor.</summary>
        public static AudioClip Calafrio => _calafrio != null ? _calafrio : (_calafrio = SomGerado.Clip("Calafrio", GerarCalafrio(71), Taxa));
        /// <summary>Os dentes batendo: estalos curtos e agudos, 14 por segundo, que crescem e somem (1,2 s).</summary>
        public static AudioClip Dentes => _dentes != null ? _dentes : (_dentes = SomGerado.Clip("Dentes batendo", GerarDentes(83), Taxa));
        /// <summary>A respiração tremida da febre: inspira 1,2 s e expira 1,8 s, a expiração tremendo.</summary>
        public static AudioClip Respiracao => _respiracao != null ? _respiracao : (_respiracao = SomGerado.Clip("Respiração tremida", GerarRespiracao(97, false), Taxa));
        /// <summary>A respiração pesada da sepse: mais grave, mais curta e mais funda, com um chiado no fundo do peito.</summary>
        public static AudioClip RespiracaoPesada => _pesada != null ? _pesada : (_pesada = SomGerado.Clip("Respiração pesada", GerarRespiracao(109, true), Taxa));

        // ------------------------------------------------ os sons ------------------------------------------------

        /// <summary>
        /// O calafrio: um sopro entre os dentes (ruído na faixa de 1,8 kHz, mais um pouco de 4 kHz, o "s" dos dentes),
        /// cortado por um tremor de amplitude de uns 11 Hz que acelera e afrouxa, com o peito dando um tranco no começo.
        /// </summary>
        static float[] GerarCalafrio(int semente)
        {
            var r = new System.Random(semente);
            float dur = 1.5f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var sopro = new Faixa(1800f, 0.8f);
            var dentes = new Faixa(4200f, 1.2f);
            var peito = new Faixa(320f, 0.9f);
            var macio = new PassaBaixa(7000f, 2);
            float fase = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float k = t / dur;
                // o ar: entra rápido, segura e vai acabando
                float ar = Mathf.Clamp01(t / 0.06f) * Mathf.Pow(1f - k, 0.8f);
                // o tremor: 13 Hz no começo, 9 Hz no fim (o corpo cansando)
                fase += Mathf.Lerp(13f, 9f, k) / Taxa;
                float treme = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(fase * 2f * Mathf.PI));
                sopro.Centro(Mathf.Lerp(2000f, 1500f, k));
                float x = Ruido(r) * ar * treme;
                // o tranco do peito (o corpo inteiro encolhendo), só no começo
                float tranco = Impulso(t, 0.02f, 0.09f);
                y[i] = macio.Passar(sopro.Passar(x) + dentes.Passar(x) * 0.35f + peito.Passar(Ruido(r) * tranco) * 0.6f);
            }
            return Acabar(y, 0.6f);
        }

        /// <summary>
        /// Os dentes batendo: estalos de 3 ms a 14 por segundo (com um pouco de pressa e de atraso, como o queixo
        /// tremendo), na faixa de 2,5 kHz, que crescem e caem ao longo de 1,2 s.
        /// </summary>
        static float[] GerarDentes(int semente)
        {
            var r = new System.Random(semente);
            float dur = 1.2f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var dente = new Faixa(2500f, 2.5f);
            var osso = new Faixa(1100f, 1.5f);
            var macio = new PassaBaixa(9000f, 2);
            float proximo = 0.01f, inicio = -1f, forca = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float k = t / dur;
                if (t >= proximo)
                {
                    inicio = t;
                    // sobe e cai (o queixo tremendo mais no meio)
                    forca = Mathf.Sin(k * Mathf.PI) * (0.6f + 0.4f * (float)r.NextDouble());
                    proximo = t + (1f / 14f) * (0.8f + 0.4f * (float)r.NextDouble());
                }
                float estalo = inicio >= 0f && t - inicio < 0.003f ? Impulso(t, inicio, 0.0008f) * forca : 0f;
                float x = Ruido(r) * estalo;
                y[i] = macio.Passar(dente.Passar(x) * 1.6f + osso.Passar(x) * 0.5f);
            }
            return Acabar(y, 0.55f);
        }

        /// <summary>
        /// A respiração: ruído numa faixa larga (Q baixo; 700 Hz na febre, 450 Hz na sepse), o ar entrando (inspira) e
        /// saindo (expira), com um tremor de amplitude de 6 Hz na expiração. A pesada é mais curta e mais forte, com um
        /// chiado grave no fundo do peito.
        /// </summary>
        static float[] GerarRespiracao(int semente, bool pesada)
        {
            var r = new System.Random(semente);
            float inspira = pesada ? 0.75f : 1.2f, pausa = pesada ? 0.08f : 0.15f, expira = pesada ? 1.1f : 1.8f;
            float dur = inspira + pausa + expira + 0.1f;
            float centro = pesada ? 450f : 700f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var ar = new Faixa(centro, 0.6f);
            var garganta = new Faixa(centro * 2.6f, 0.9f);
            var chiado = new Faixa(pesada ? 180f : 240f, 3f);
            var macio = new PassaBaixa(pesada ? 3200f : 4500f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float volume, alto;
                if (t < inspira)
                {
                    // o ar entrando: incha e para (mais agudo e mais fraco que a expiração)
                    float k = t / inspira;
                    volume = Mathf.Sin(k * Mathf.PI * 0.5f) * Mathf.Clamp01((inspira - t) / 0.12f) * 0.55f;
                    alto = 1.25f;
                }
                else if (t < inspira + pausa)
                {
                    volume = 0f;
                    alto = 1f;
                }
                else
                {
                    // o ar saindo: forte no começo e morrendo, tremendo a 6 Hz (o queixo e o peito tremendo de frio)
                    float te = t - inspira - pausa;
                    float k = Mathf.Clamp01(te / expira);
                    float treme = 0.55f + 0.45f * Mathf.Sin(te * 6f * 2f * Mathf.PI);
                    volume = Mathf.Clamp01(te / 0.05f) * Mathf.Pow(1f - k, 1.3f) * treme;
                    alto = Mathf.Lerp(1f, 0.85f, k);
                }
                ar.Centro(centro * alto);
                float x = Ruido(r) * volume;
                float fundo = pesada ? chiado.Passar(Ruido(r) * volume) * 0.9f : chiado.Passar(Ruido(r) * volume) * 0.2f;
                y[i] = macio.Passar(ar.Passar(x) + garganta.Passar(x) * 0.25f + fundo);
            }
            return Acabar(y, pesada ? 0.7f : 0.5f);
        }

        // ------------------------------------------------ ajudantes ------------------------------------------------

        // um impulso que nasce em 'em' e morre em 'tau' segundos
        static float Impulso(float t, float em, float tau) => t < em ? 0f : Mathf.Exp(-(t - em) / tau);

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // normaliza no pico e apaga os primeiros 5 ms e os últimos 20 ms (sem clique)
        static float[] Acabar(float[] dados, float pico)
        {
            float max = 1e-6f;
            foreach (var v in dados) max = Mathf.Max(max, Mathf.Abs(v));
            float k = pico / max;
            int fim = Mathf.Min(dados.Length, Taxa / 50), comeco = Mathf.Min(dados.Length, Taxa / 200);
            for (int i = 0; i < dados.Length; i++)
            {
                float rampa = i >= dados.Length - fim ? (dados.Length - 1 - i) / (float)fim : 1f;
                if (i < comeco) rampa *= i / (float)comeco;
                dados[i] *= k * rampa;
            }
            return dados;
        }
    }
}
