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
        // 13/11: o guaxinim no lixo da cidade (a sacola mexida, o remexer na lata e a tampa da caçamba)
        Sacola, Remexer, Cacamba,
        // 19/11/2026 (as gravações CC0 do Pedro): o filhote de urso chorando e o urso mastigando o salmão
        Filhote, Comendo,
        // 20/11/2026 (os do sfxjar): o chiado do guaxinim
        Guaxinim,
    }

    /// <summary>
    /// Os sons provisórios dos bichos (a etapa 2, 12/11/2026), feitos na hora em código como os do resto do jogo
    /// (Combat/Efeitos.cs): o bufo do veado, o uivo do lobo, o coro do coiote, o rosnado, a mordida, o bufo e os dentes do
    /// urso, o rugido, a patada e o guincho do coelho. O Pedro grava ou troca depois (decisão de 12/11/2026). Cada som tem
    /// o alcance dele: o uivo se ouve longe.
    /// Desde 19/11/2026, os que têm gravação de verdade tocam ela (Resources/Bichos, uma pasta por som: o Uivo e o Rosnado do
    /// lobo, o BufoDoUrso e o Rugido, o Bufo do veado, o Mugido do alce, o Grito da águia, o Coaxar e o Chocalho); os
    /// outros continuam feitos aqui.
    /// (20/11/2026, a pedido: "podemos excluir os sons que já foram substituídos") os feitos aqui dos que têm a pasta saíram;
    /// ficam só os que ainda não têm gravação: a mordida, o guincho do coelho, a patada, a lata do lixo, o espirro do gambá,
    /// a sacola, o remexer e a caçamba. Sem a pasta, um dos outros fica calado.
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
            SomDoBicho.Sacola => 22f,
            SomDoBicho.Remexer => 30f,
            SomDoBicho.Cacamba => 80f,
            SomDoBicho.Filhote => 90f,
            SomDoBicho.Comendo => 30f,
            SomDoBicho.Guaxinim => 45f,
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
            Efeitos.Som(c, ponto, Volume(s), true, Alcance(s), CanalDoSom.Animais);   // (20/11/2026: o canal Animais do mixer)
        }

        static AudioClip[] Clips(SomDoBicho s)
        {
            if (_clips == null) _clips = new AudioClip[System.Enum.GetValues(typeof(SomDoBicho)).Length][];
            int i = (int)s;
            if (i < 0 || i >= _clips.Length) return null;
            if (_clips[i] == null || _clips[i].Length == 0 || _clips[i][0] == null)
            {
                // os gravados (19/11/2026, do Animal Sound Effect Pack Lite: o lobo, o urso, o veado, o alce, a águia, o
                // sapo e a cascavel) em Resources/Bichos/<o som>; sem eles, os feitos aqui (20/11/2026: só os que não têm
                // gravação; os outros, sem a pasta, ficam calados)
                var gravados = Resources.LoadAll<AudioClip>("Bichos/" + s);
                if (gravados != null && gravados.Length > 0) _clips[i] = gravados;
                else
                {
                    var feitos = new AudioClip[2];
                    for (int k = 0; k < 2; k++) feitos[k] = Gerar(s, k);
                    _clips[i] = feitos[0] != null ? feitos : null;
                }
            }
            return _clips[i];
        }

        // (20/11/2026) só os que não têm gravação; os que têm a pasta em Resources/Bichos (o Bufo, o Uivo, o Latido, o
        // Rosnado, o BufoDoUrso, os Dentes, o Rugido, o Respingo, o Grasnado, o Ganso, o Grito, o Estalo, o Mugido, o Coaxar, o
        // Chocalho, o Chiado, o GluGlu, o Urubu, o Filhote, o Comendo e o Guaxinim) saíram daqui: null
        static AudioClip Gerar(SomDoBicho s, int variacao)
        {
            var r = new System.Random(1000 + (int)s * 17 + variacao * 7919);
            float[] d = s switch
            {
                SomDoBicho.Mordida => Mordida(r),
                SomDoBicho.Guincho => Guincho(r),
                SomDoBicho.Patada => Patada(r),
                SomDoBicho.Lata => Lata(r),
                SomDoBicho.Sacola => Sacola(r),
                SomDoBicho.Remexer => Remexer(r),
                SomDoBicho.Cacamba => Cacamba(r),
                SomDoBicho.Espirro => Bufo(r, 0.7f, 0.5f, 2200f),
                _ => null,
            };
            if (d == null) return null;
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

        // a sacola plástica mexida: os estalos finos e o chiado do plástico, em rajadas (13/11)
        static float[] Sacola(System.Random r)
        {
            int n = (int)(0.9f * Taxa);
            var d = new float[n];
            float antes = 0f;
            for (int k = 0; k < 6; k++)
            {
                int i0 = (int)((0.05f + 0.13f * k + (float)r.NextDouble() * 0.05f) * Taxa);
                float dur = 0.06f + (float)r.NextDouble() * 0.1f;
                for (int i = 0; i < (int)(dur * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float ruido = Ruido(r);
                    float agudo = ruido - antes;   // o chiado do plástico (só os agudos)
                    antes = ruido;
                    float estalo = r.NextDouble() < 0.004 ? Ruido(r) * 3f : 0f;
                    d[i0 + i] += (agudo * 0.6f + estalo) * Mathf.Sin(Mathf.PI * t / dur);
                }
            }
            return d;
        }

        // o remexer na lata: os tinidos curtos de latinhas e vidros batendo (13/11)
        static float[] Remexer(System.Random r)
        {
            int n = (int)(0.8f * Taxa);
            var d = new float[n];
            for (int k = 0; k < 5; k++)
            {
                int i0 = (int)((float)r.NextDouble() * 0.65f * Taxa);
                float f = 1800f + (float)r.NextDouble() * 2600f;
                float forca = 0.4f + (float)r.NextDouble() * 0.6f;
                for (int i = 0; i < (int)(0.12f * Taxa) && i0 + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    d[i0 + i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.7f * t) + Ruido(r) * 0.4f) * Mathf.Exp(-t * 45f) * forca;
                }
            }
            return d;
        }

        // a tampa da caçamba batendo: o golpe grave do metal grande e o eco (13/11)
        static float[] Cacamba(System.Random r)
        {
            int n = (int)(1.3f * Taxa);
            var d = new float[n];
            float[] quando = { 0f, 0.22f };
            for (int k = 0; k < quando.Length; k++)
            {
                int i0 = (int)(quando[k] * Taxa);
                float forca = k == 0 ? 1f : 0.35f;
                for (int i = 0; i + i0 < n; i++)
                {
                    float t = i / (float)Taxa;
                    float metal = Mathf.Sin(2f * Mathf.PI * 180f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 410f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 760f * t);
                    d[i0 + i] += (metal * 0.6f + Ruido(r) * 0.5f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 5f) * forca;
                }
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
