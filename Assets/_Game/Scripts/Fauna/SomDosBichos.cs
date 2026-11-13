using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>Os sons dos bichos (vão pela rede num byte: novos no fim).</summary>
    public enum SomDoBicho : byte
    {
        Bufo, Uivo, Latido, Rosnado, Mordida, BufoDoUrso, Dentes, Rugido, Guincho, Patada,
        // a etapa 3 (a água): o respingo, o pato, o ganso, a águia, a cauda do castor e o alce
        Respingo, Grasnado, Ganso, Grito, Estalo, Mugido,
        // a etapa 4 (a noite): o sapo, o chocalho, o esquilo, o peru, a lata do lixo, o espirro do gambá e o urubu
        Coaxar, Chocalho, Chiado, GluGlu, Lata, Espirro, Urubu,
    }

    /// <summary>
    /// Os sons provisórios dos bichos (a etapa 2, 12/11/2026), feitos na hora em código como os do resto do jogo
    /// (Combat/Efeitos.cs): o bufo do veado, o uivo do lobo, o coro do coiote, o rosnado, a mordida, o bufo e os dentes do
    /// urso, o rugido, a patada e o guincho do coelho. O Pedro grava ou troca depois (decisão de 12/11/2026). Cada som tem
    /// o alcance dele: o uivo se ouve longe.
    /// </summary>
    public static class SomDosBichos
    {
        const int Taxa = 44100;
        static AudioClip[][] _clips;

        public static float Alcance(SomDoBicho s) => s switch
        {
            SomDoBicho.Uivo => 420f,
            SomDoBicho.Latido => 300f,
            SomDoBicho.Rugido => 120f,
            SomDoBicho.Guincho => 60f,
            SomDoBicho.Ganso => 260f,
            SomDoBicho.Grito => 220f,
            SomDoBicho.Mugido => 160f,
            SomDoBicho.Respingo => 45f,
            SomDoBicho.Coaxar => 70f,
            SomDoBicho.Chocalho => 25f,
            SomDoBicho.GluGlu => 120f,
            SomDoBicho.Lata => 60f,
            _ => 70f,
        };

        static float Volume(SomDoBicho s) => s switch
        {
            SomDoBicho.Uivo => 1f,
            SomDoBicho.Latido => 0.85f,
            SomDoBicho.Rugido => 1f,
            SomDoBicho.Mordida => 0.8f,
            SomDoBicho.Patada => 0.9f,
            _ => 0.75f,
        };

        /// <summary>Toca o som no ponto (em qualquer máquina).</summary>
        public static void Tocar(SomDoBicho s, Vector3 ponto)
        {
            var c = Clips(s);
            if (c == null || c.Length == 0) return;
            Efeitos.Som(c, ponto, Volume(s), true, Alcance(s));
        }

        static AudioClip[] Clips(SomDoBicho s)
        {
            if (_clips == null) _clips = new AudioClip[System.Enum.GetValues(typeof(SomDoBicho)).Length][];
            int i = (int)s;
            if (i < 0 || i >= _clips.Length) return null;
            if (_clips[i] == null || _clips[i].Length == 0 || _clips[i][0] == null)
            {
                _clips[i] = new AudioClip[2];
                for (int k = 0; k < 2; k++) _clips[i][k] = Gerar(s, k);
            }
            return _clips[i];
        }

        static AudioClip Gerar(SomDoBicho s, int variacao)
        {
            var r = new System.Random(1000 + (int)s * 17 + variacao * 7919);
            float[] d = s switch
            {
                SomDoBicho.Bufo => Bufo(r, 0.4f, 0.32f, 900f),
                SomDoBicho.Uivo => Uivo(r, variacao),
                SomDoBicho.Latido => Latido(r),
                SomDoBicho.Rosnado => Rosnado(r, 1.3f, 85f),
                SomDoBicho.Mordida => Mordida(r),
                SomDoBicho.BufoDoUrso => BufoDuplo(r),
                SomDoBicho.Dentes => Dentes(r),
                SomDoBicho.Rugido => Rugido(r),
                SomDoBicho.Guincho => Guincho(r),
                SomDoBicho.Respingo => Respingo(r),
                SomDoBicho.Grasnado => Grasnado(r, 380f, 0.16f, 3),
                SomDoBicho.Ganso => Grasnado(r, 300f, 0.28f, 2),
                SomDoBicho.Grito => Grito(r),
                SomDoBicho.Estalo => Estalo(r),
                SomDoBicho.Mugido => Mugido(r),
                SomDoBicho.Coaxar => Coaxar(r, variacao),
                SomDoBicho.Chocalho => Chocalho(r),
                SomDoBicho.Chiado => Chiado(r),
                SomDoBicho.GluGlu => GluGlu(r),
                SomDoBicho.Lata => Lata(r),
                SomDoBicho.Espirro => Bufo(r, 0.7f, 0.5f, 2200f),
                SomDoBicho.Urubu => Bufo(r, 0.5f, 0.35f, 1400f),
                _ => Patada(r),
            };
            Normalizar(d, 0.9f);
            return SomGerado.Clip($"Bicho {s} {variacao + 1}", d, Taxa);
        }

        // ------------------------------------------------------------------ as peças

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static void Normalizar(float[] d, float pico)
        {
            float m = 1e-6f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = pico / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        // um passa-baixa de uma volta (a frequência de corte em Hz)
        static float Coef(float corte) => 1f - Mathf.Exp(-2f * Mathf.PI * corte / Taxa);

        // o bufo: o ar saindo pelo nariz (ruído com um corte, um ataque rápido e a queda)
        static float[] Bufo(System.Random r, float dur, float queda, float corte)
        {
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, a = Coef(corte), b = Coef(160f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t / queda * 3f);
                lp += (Ruido(r) - lp) * a;
                lp2 += (lp - lp2) * b;
                d[i] = (lp - lp2) * env + Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.08f * env;
            }
            return d;
        }

        static float[] BufoDuplo(System.Random r)
        {
            var a = Bufo(r, 0.45f, 0.35f, 650f);
            var b = Bufo(r, 0.45f, 0.3f, 600f);
            int deslocar = (int)(0.32f * Taxa);
            var d = new float[a.Length + deslocar];
            for (int i = 0; i < a.Length; i++) d[i] += a[i];
            for (int i = 0; i < b.Length; i++) d[i + deslocar] += b[i] * 0.85f;
            return d;
        }

        // o uivo: a nota sobe, segura com um tremido e cai no fim (os harmônicos e um pouco de fôlego)
        static float[] Uivo(System.Random r, int variacao)
        {
            float dur = 3.4f + variacao * 0.5f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float f0 = 360f + variacao * 25f, pico = 540f - variacao * 30f, fim = 320f;
            float fase = 0f, lp = 0f, a = Coef(2500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa, u = t / dur;
                float f = u < 0.18f ? Mathf.Lerp(f0, pico, Mathf.SmoothStep(0f, 1f, u / 0.18f))
                    : u < 0.78f ? pico + Mathf.Sin(u * 9f) * 8f
                    : Mathf.Lerp(pico, fim, Mathf.SmoothStep(0f, 1f, (u - 0.78f) / 0.22f));
                f += Mathf.Sin(2f * Mathf.PI * 5.2f * t) * 6f;
                fase += 2f * Mathf.PI * f / Taxa;
                float env = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((dur - t) / 0.7f);
                lp += (Ruido(r) - lp) * a;
                d[i] = (Mathf.Sin(fase) + 0.32f * Mathf.Sin(2f * fase) + 0.12f * Mathf.Sin(3f * fase) + 0.05f * lp) * env;
            }
            return d;
        }

        // o coro do coiote: latidinhos agudos que caem e um uivo curto e tremido no fim
        static float[] Latido(System.Random r)
        {
            float dur = 1.9f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float fase = 0f;
            int latidos = 5 + r.Next(3);
            for (int k = 0; k < latidos; k++)
            {
                float t0 = 0.05f + k * 0.17f + (float)r.NextDouble() * 0.03f, f0 = 900f + (float)r.NextDouble() * 400f;
                int i0 = (int)(t0 * Taxa), len = (int)(0.11f * Taxa);
                for (int i = 0; i < len && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float f = f0 * (1f - 0.35f * t / 0.11f);
                    fase += 2f * Mathf.PI * f / Taxa;
                    float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 22f);
                    d[i0 + i] += (Mathf.Sin(fase) + 0.4f * Mathf.Sin(2f * fase)) * env;
                }
            }
            float tu = 0.05f + latidos * 0.17f;
            int iu = (int)(tu * Taxa);
            for (int i = iu; i < n; i++)
            {
                float t = (i - iu) / (float)Taxa, resto = (n - i) / (float)Taxa;
                float f = 820f + Mathf.Sin(2f * Mathf.PI * 9f * t) * 40f - t * 120f;
                fase += 2f * Mathf.PI * f / Taxa;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01(resto / 0.25f) * 0.8f;
                d[i] += (Mathf.Sin(fase) + 0.3f * Mathf.Sin(2f * fase)) * env;
            }
            return d;
        }

        // o rosnado: grave, áspero (a nota tremendo depressa) e com o ar por cima
        static float[] Rosnado(System.Random r, float dur, float f0)
        {
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float fase = 0f, lp = 0f, a = Coef(700f), aspero = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float f = f0 + Mathf.Sin(2f * Mathf.PI * 0.7f * t) * 12f;
                fase += 2f * Mathf.PI * f / Taxa;
                aspero += ((float)r.NextDouble() - aspero) * Coef(35f);
                float am = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 24f * t + aspero * 6f);
                float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((dur - t) / 0.25f);
                lp += (Ruido(r) - lp) * a;
                float serra = 0f;
                for (int h = 1; h <= 8; h++) serra += Mathf.Sin(h * fase) / h;
                d[i] = (serra * 0.6f + lp * 0.5f) * am * env;
            }
            return d;
        }

        // a mordida: o estalo dos dentes e o baque
        static float[] Mordida(System.Random r)
        {
            int n = (int)(0.22f * Taxa);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i] = Ruido(r) * Mathf.Exp(-t * 90f) * 0.8f + Mathf.Sin(2f * Mathf.PI * 150f * t) * Mathf.Exp(-t * 30f)
                       + Ruido(r) * Mathf.Exp(-Mathf.Max(0f, t - 0.04f) * 140f) * (t > 0.04f ? 0.6f : 0f);
            }
            return d;
        }

        // os dentes batendo: 3 ou 4 estalos secos
        static float[] Dentes(System.Random r)
        {
            int n = (int)(0.6f * Taxa);
            var d = new float[n];
            int k = 3 + r.Next(2);
            for (int c = 0; c < k; c++)
            {
                int i0 = (int)((0.02f + c * (0.11f + (float)r.NextDouble() * 0.04f)) * Taxa);
                for (int i = 0; i < (int)(0.05f * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    d[i0 + i] += Ruido(r) * Mathf.Exp(-t * 260f) + Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 180f) * 0.5f;
                }
            }
            return d;
        }

        // o rugido do urso: o rosnado bem grave e forte, que cresce e cai
        static float[] Rugido(System.Random r)
        {
            var d = Rosnado(r, 1.6f, 110f);
            var ar = Bufo(r, 1.6f, 1.2f, 1200f);
            for (int i = 0; i < d.Length; i++)
            {
                float u = i / (float)d.Length;
                d[i] = (d[i] + ar[i] * 0.7f) * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f));
            }
            return d;
        }

        // a patada: o vento do braço e o baque surdo
        static float[] Patada(System.Random r)
        {
            int n = (int)(0.35f * Taxa);
            var d = new float[n];
            float lp = 0f, a = Coef(500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                lp += (Ruido(r) - lp) * a;
                float vento = lp * Mathf.Exp(-Mathf.Pow((t - 0.07f) / 0.04f, 2f)) * 1.2f;
                float baque = t > 0.1f ? (Mathf.Sin(2f * Mathf.PI * 90f * (t - 0.1f)) + Ruido(r) * 0.4f) * Mathf.Exp(-(t - 0.1f) * 22f) : 0f;
                d[i] = vento + baque;
            }
            return d;
        }

        // o respingo: a água batida (o ruído claro que cai depressa, e as gotas)
        static float[] Respingo(System.Random r)
        {
            int n = (int)(0.6f * Taxa);
            var d = new float[n];
            float lp = 0f, a = Coef(3500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                lp += (Ruido(r) - lp) * a;
                float gotas = r.NextDouble() < 0.0009 ? 1f : 0f;
                d[i] = lp * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t / 0.01f) + gotas * Mathf.Exp(-t * 4f) * 0.8f;
            }
            return d;
        }

        // o grasnado (o pato: curto, anasalado, 3 vezes; o ganso: mais longo e grave, 2 vezes)
        static float[] Grasnado(System.Random r, float f0, float dur, int vezes)
        {
            float pausa = dur * 0.6f;
            int n = (int)((dur + pausa) * vezes * Taxa);
            var d = new float[n];
            float fase = 0f;
            for (int k = 0; k < vezes; k++)
            {
                int i0 = (int)(k * (dur + pausa) * Taxa);
                float fk = f0 * (1f + 0.06f * (float)r.NextDouble());
                for (int i = 0; i < (int)(dur * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float f = fk * (1f + 0.12f * Mathf.Sin(Mathf.PI * t / dur));
                    fase += 2f * Mathf.PI * f / Taxa;
                    float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((dur - t) / 0.04f);
                    // anasalado: a onda quadrada com os harmônicos ímpares, e um pouco de ar
                    float s = 0f;
                    for (int h = 1; h <= 9; h += 2) s += Mathf.Sin(h * fase) / h;
                    d[i0 + i] += (s * 0.8f + Ruido(r) * 0.08f) * env;
                }
            }
            return d;
        }

        // o grito da águia: os piados agudos e trêmulos que caem
        static float[] Grito(System.Random r)
        {
            int n = (int)(1.3f * Taxa);
            var d = new float[n];
            float fase = 0f;
            for (int k = 0; k < 4; k++)
            {
                int i0 = (int)((0.04f + k * 0.26f) * Taxa);
                float f0 = 2600f - k * 180f;
                for (int i = 0; i < (int)(0.2f * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float f = f0 * (1f - 0.25f * t / 0.2f) + Mathf.Sin(2f * Mathf.PI * 32f * t) * 120f;
                    fase += 2f * Mathf.PI * f / Taxa;
                    float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 9f);
                    d[i0 + i] += (Mathf.Sin(fase) + 0.25f * Mathf.Sin(2f * fase)) * env;
                }
            }
            return d;
        }

        // o estalo da cauda do castor batendo na água: o tapa seco e o respingo
        static float[] Estalo(System.Random r)
        {
            var resp = Respingo(r);
            int n = resp.Length;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i] = resp[i] * 0.7f + (Ruido(r) * Mathf.Exp(-t * 120f) + Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 40f)) * 1.2f;
            }
            return d;
        }

        // o mugido do alce: grave, nasal, subindo e caindo
        static float[] Mugido(System.Random r)
        {
            float dur = 1.3f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float fase = 0f, lp = 0f, a = Coef(900f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa, u = t / dur;
                float f = 95f + 45f * Mathf.Sin(Mathf.PI * u);
                fase += 2f * Mathf.PI * f / Taxa;
                float s = 0f;
                for (int h = 1; h <= 10; h++) s += Mathf.Sin(h * fase) / (h * 0.8f);
                lp += (s - lp) * a;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((dur - t) / 0.3f);
                d[i] = (lp + Ruido(r) * 0.05f) * env;
            }
            return d;
        }

        // o coaxar: os pulsos graves e rápidos do sapo-boi (a variação muda o tom)
        static float[] Coaxar(System.Random r, int variacao)
        {
            float dur = 0.9f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float f0 = variacao == 0 ? 160f : 210f, fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float pulso = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 22f * t));
                fase += 2f * Mathf.PI * f0 * (1f + 0.1f * pulso) / Taxa;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((dur - t) / 0.2f);
                float s = Mathf.Sin(fase) + 0.5f * Mathf.Sin(2f * fase) + 0.25f * Mathf.Sin(3f * fase);
                d[i] = s * pulso * pulso * env;
            }
            return d;
        }

        // o chocalho da cascavel: o chiado de alta, tremendo depressa (uns 60 batidas por segundo)
        static float[] Chocalho(System.Random r)
        {
            float dur = 1.6f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float lp = 0f, hp = 0f, a = Coef(9000f), b = Coef(2500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                lp += (Ruido(r) - lp) * a;
                hp += (lp - hp) * b;
                float bate = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 30f * t));
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((dur - t) / 0.15f);
                d[i] = (lp - hp) * bate * env;
            }
            return d;
        }

        // o chiado do esquilo: os "tchic" agudos e rápidos do alarme
        static float[] Chiado(System.Random r)
        {
            int n = (int)(0.9f * Taxa);
            var d = new float[n];
            float fase = 0f;
            for (int k = 0; k < 7; k++)
            {
                int i0 = (int)((0.02f + k * 0.12f) * Taxa);
                for (int i = 0; i < (int)(0.05f * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    fase += 2f * Mathf.PI * (4200f - t * 20000f) / Taxa;
                    d[i0 + i] += Mathf.Sin(fase) * Mathf.Exp(-t * 60f);
                }
            }
            return d;
        }

        // o glu-glu do peru: o gorgolejo descendo, rápido
        static float[] GluGlu(System.Random r)
        {
            float dur = 1.0f;
            int n = (int)(dur * Taxa);
            var d = new float[n];
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa, u = t / dur;
                float f = 900f - 450f * u;
                float gorgo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 18f * t);
                fase += 2f * Mathf.PI * f * (1f + 0.15f * gorgo) / Taxa;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((dur - t) / 0.2f);
                d[i] = (Mathf.Sin(fase) + 0.4f * Mathf.Sin(2f * fase)) * gorgo * env;
            }
            return d;
        }

        // a lata do lixo caindo e rolando: o golpe metálico e os ressaltos
        static float[] Lata(System.Random r)
        {
            int n = (int)(1.0f * Taxa);
            var d = new float[n];
            float[] quando = { 0f, 0.18f, 0.31f, 0.4f, 0.47f };
            for (int k = 0; k < quando.Length; k++)
            {
                int i0 = (int)(quando[k] * Taxa);
                float forca = 1f / (1f + k * 0.8f);
                for (int i = 0; i < (int)(0.25f * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float metal = Mathf.Sin(2f * Mathf.PI * 1250f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 2730f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 4100f * t);
                    d[i0 + i] += (metal * 0.5f + Ruido(r) * 0.6f) * Mathf.Exp(-t * 28f) * forca;
                }
            }
            return d;
        }

        // o guincho do coelho pego
        static float[] Guincho(System.Random r)
        {
            int n = (int)(0.5f * Taxa);
            var d = new float[n];
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float f = 1900f + Mathf.Sin(2f * Mathf.PI * 13f * t) * 160f - t * 500f;
                fase += 2f * Mathf.PI * f / Taxa;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.5f - t) / 0.15f);
                d[i] = (Mathf.Sin(fase) + 0.3f * Mathf.Sin(2f * fase) + Ruido(r) * 0.1f) * env;
            }
            return d;
        }
    }
}
