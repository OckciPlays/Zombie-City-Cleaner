using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// Os sons do Dia 0, gerados por código (provisórios, como os do carro): a tosse dos doentes (homem e mulher, 3 de
    /// cada), o motor do cortador de grama e o dos carros que passam (laços) e a água da mangueira (laço).
    /// </summary>
    public static class SonsDoDiaZero
    {
        const int Taxa = 44100;
        static AudioClip[] _tosses;
        static AudioClip _cortador, _motor, _agua;

        /// <summary>As tosses: 0 a 2 de homem, 3 a 5 de mulher.</summary>
        public static AudioClip Tosse(bool mulher, int k)
        {
            if (_tosses == null)
            {
                _tosses = new AudioClip[6];
                for (int i = 0; i < 6; i++) _tosses[i] = SomGerado.Clip($"Tosse {i}", GerarTosse(i >= 3, 1 + i % 3, 300 + i * 17));
            }
            return _tosses[(mulher ? 3 : 0) + Mathf.Abs(k) % 3];
        }

        public static AudioClip Cortador => _cortador ??= SomGerado.Clip("Cortador de grama", GerarMotor(31f, 2f, 0.55f, 77));
        public static AudioClip MotorDoCarro => _motor ??= SomGerado.Clip("Carro passando", GerarMotor(24f, 2f, 0.35f, 91));
        public static AudioClip Agua => _agua ??= SomGerado.Clip("Mangueira", GerarAgua(2f, 55));
        static AudioClip _festa;
        /// <summary>A festa na sala (a introdução): as vozes misturadas conversando e a música abafada (laço de 8 s).</summary>
        public static AudioClip Festa => _festa ??= SomGerado.Clip("Festa", GerarFesta(8f, 31));

        // a conversa de várias pessoas (cada voz, o pulso da garganta no tom dela passando por duas formantes que mudam a cada
        // sílaba, com as pausas) e a música do som da sala, grave e abafada
        static float[] GerarFesta(float segundos, int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(segundos * Taxa);
            var s = new float[n];
            float[,] vogais = { { 730, 1090 }, { 270, 2290 }, { 530, 1840 }, { 570, 840 }, { 300, 870 }, { 660, 1700 } };
            for (int v = 0; v < 6; v++)
            {
                float f0 = v % 2 == 0 ? 105f + (float)r.NextDouble() * 40f : 185f + (float)r.NextDouble() * 50f;
                float fase = 0f, vol = 0.5f + (float)r.NextDouble() * 0.5f;
                int i = 0;
                while (i < n)
                {
                    // uma frase: de 4 a 12 sílabas, e uma pausa
                    int silabas = 4 + r.Next(9);
                    for (int k = 0; k < silabas && i < n; k++)
                    {
                        int vo = r.Next(vogais.GetLength(0));
                        var a = new Banda(vogais[vo, 0], 6f);
                        var b = new Banda(vogais[vo, 1], 8f);
                        int m = Mathf.RoundToInt((0.11f + (float)r.NextDouble() * 0.14f) * Taxa);
                        float tom = f0 * (0.9f + (float)r.NextDouble() * 0.25f);
                        for (int j = 0; j < m && i < n; j++, i++)
                        {
                            fase += tom / Taxa;
                            float pulso = 2f * (fase - Mathf.Floor(fase)) - 1f;
                            float env = Mathf.Sin(Mathf.PI * j / m);
                            s[i] += (a.Passo(pulso) * 1.2f + b.Passo(pulso) * 0.7f) * env * vol * 0.5f;
                        }
                    }
                    i += Mathf.RoundToInt((0.3f + (float)r.NextDouble() * 1.4f) * Taxa);
                }
            }
            // a música: o grave e a batida (110 bpm), abafados pela parede
            var grave = new Banda(90f, 1.2f);
            float batida = 60f / 110f;
            int[] notas = { 41, 41, 45, 38 };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                int tempo = Mathf.FloorToInt(t / batida);
                float dentro = t - tempo * batida;
                float f = 440f * Mathf.Pow(2f, (notas[(tempo / 4) % notas.Length] - 69) / 12f);
                float bumbo = Mathf.Sin(2f * Mathf.PI * (50f + 60f * Mathf.Exp(-dentro * 30f)) * dentro) * Mathf.Exp(-dentro * 9f);
                float baixo = Mathf.Sin(2f * Mathf.PI * f * t) * (0.6f + 0.4f * Mathf.Exp(-dentro * 4f));
                s[i] += grave.Passo(bumbo * 1.2f + baixo * 0.5f) * 0.9f;
            }
            int c = Taxa / 10;
            for (int i = 0; i < c; i++)
            {
                float k = i / (float)c;
                s[i] = s[i] * k + s[n - c + i] * (1f - k);
            }
            System.Array.Resize(ref s, n - c);
            Normalizar(s, 0.5f);
            return s;
        }

        // um filtro passa-banda (biquad, a forma do RBJ), estável para qualquer frequência abaixo da metade da taxa
        struct Banda
        {
            float b0, b2, a1, a2, x1, x2, y1, y2;

            public Banda(float f, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Min(f, Taxa * 0.45f) / Taxa, alfa = Mathf.Sin(w) / (2f * q), a0 = 1f + alfa;
                b0 = alfa / a0;
                b2 = -alfa / a0;
                a1 = -2f * Mathf.Cos(w) / a0;
                a2 = (1f - alfa) / a0;
                x1 = x2 = y1 = y2 = 0f;
            }

            public float Passo(float x)
            {
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x;
                y2 = y1; y1 = y;
                return y;
            }
        }

        // a tosse: 'vezes' golpes de ar (o "k" agudo no começo, o corpo nas formantes da voz e um baque grave), cada um
        // mais fraco que o anterior
        static float[] GerarTosse(bool mulher, int vezes, int semente)
        {
            var r = new System.Random(semente);
            float f1 = mulher ? 700f : 520f, f2 = mulher ? 1750f : 1350f, grave = mulher ? 170f : 110f;
            float cada = 0.34f, entre = 0.12f;
            int n = Mathf.CeilToInt((vezes * (cada + entre) + 0.15f) * Taxa);
            var s = new float[n];
            for (int v = 0; v < vezes; v++)
            {
                int ini = Mathf.RoundToInt(v * (cada + entre + (float)r.NextDouble() * 0.05f) * Taxa);
                float forca = 1f - v * 0.22f;
                var a = new Banda(f1 * (0.9f + 0.2f * (float)r.NextDouble()), 3f);
                var b = new Banda(f2 * (0.9f + 0.2f * (float)r.NextDouble()), 4f);
                var k = new Banda(2800f, 2f);
                int m = Mathf.RoundToInt(cada * Taxa);
                for (int i = 0; i < m && ini + i < n; i++)
                {
                    float t = i / (float)Taxa;
                    float env = (1f - Mathf.Exp(-t / 0.008f)) * Mathf.Exp(-t / 0.11f);
                    float ruido = (float)r.NextDouble() * 2f - 1f;
                    float corpo = a.Passo(ruido) * 1.6f + b.Passo(ruido) * 0.9f;
                    float estalo = k.Passo(ruido) * Mathf.Exp(-t / 0.02f) * 1.4f;
                    float baque = Mathf.Sin(2f * Mathf.PI * grave * t * (1f - t)) * Mathf.Exp(-t / 0.05f) * 0.5f;
                    s[ini + i] += forca * (env * corpo + estalo + baque);
                }
            }
            Normalizar(s, 0.85f);
            return s;
        }

        // um motor pequeno (o cortador, o carro de longe): o dente de serra no giro com as harmônicas, o ruído da descarga
        // e o tremor do giro; um laço sem emenda (um número inteiro de voltas)
        static float[] GerarMotor(float giro, float segundos, float ruido, int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(segundos * Taxa);
            giro = Mathf.Round(giro * segundos) / segundos;
            var s = new float[n];
            var filtro = new Banda(900f, 1.2f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float fase = giro * t;
                float serra = 2f * (fase - Mathf.Floor(fase)) - 1f;
                float h = 0.6f * Mathf.Sin(2f * Mathf.PI * fase * 2f) + 0.3f * Mathf.Sin(2f * Mathf.PI * fase * 3f);
                float tremor = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * (4f / segundos) * t);
                float desc = filtro.Passo((float)r.NextDouble() * 2f - 1f) * ruido * 3f;
                s[i] = (serra * 0.5f + h * 0.4f) * tremor + desc * (0.6f + 0.4f * Mathf.Max(0f, serra));
            }
            // a emenda: as pontas se misturam (o ruído não volta igual)
            int c = Taxa / 20;
            for (int i = 0; i < c; i++)
            {
                float k = i / (float)c;
                s[i] = s[i] * k + s[n - c + i] * (1f - k);
            }
            System.Array.Resize(ref s, n - c);
            Normalizar(s, 0.6f);
            return s;
        }

        // a água da mangueira: o chiado (ruído entre 2 e 6 kHz) com o borbulhar
        static float[] GerarAgua(float segundos, int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(segundos * Taxa);
            var s = new float[n];
            var a = new Banda(3200f, 0.8f);
            var b = new Banda(700f, 2f);
            for (int i = 0; i < n; i++)
            {
                float x = (float)r.NextDouble() * 2f - 1f;
                s[i] = a.Passo(x) + b.Passo(x) * 0.3f * (0.5f + 0.5f * Mathf.Sin(i * 0.0021f));
            }
            int c = Taxa / 20;
            for (int i = 0; i < c; i++)
            {
                float k = i / (float)c;
                s[i] = s[i] * k + s[n - c + i] * (1f - k);
            }
            System.Array.Resize(ref s, n - c);
            Normalizar(s, 0.4f);
            return s;
        }

        static void Normalizar(float[] s, float pico)
        {
            float m = 1e-6f;
            foreach (float v in s) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < s.Length; i++) s[i] = s[i] / m * pico;
        }
    }
}
