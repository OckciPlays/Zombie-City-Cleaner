using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A fonte das construções (o <see cref="Obra.Letreiro"/>: as placas do Hospital Principal, o letreiro da fachada e os
    /// nomes dos andares do bunker). Refeita em 07/11/2026, a pedido ("não gosto da fonte"): no lugar dos traços finos numa
    /// grade de 2 × 4, uma sem serifa geométrica, de placa (como as das sinalizações americanas), com as letras cheias e a
    /// largura de cada uma: cada letra é uma lista de polígonos convexos (as barras, as diagonais de pontas retas e os arcos
    /// de verdade nas letras redondas), na altura 1 (a das maiúsculas) e com o traço de 0,16. Desenhados chatos (a placa) ou
    /// extrudados (as letras em relevo da fachada). Só maiúsculas, os números e uns sinais: o ponto, os dois-pontos, o hífen, a
    /// barra, o apóstrofo, o mais, o #, o &amp;, a estrela (o '*'), e as setas cheias, que são o '&lt;' (para a esquerda de quem lê), o '&gt;' (para a
    /// direita), o '^' (em frente) e o '~' (para baixo). O que não está aqui vira espaço.
    /// </summary>
    public static class Letras
    {
        /// <summary>Uma letra: a largura (na altura 1) e os polígonos convexos dela.</summary>
        public sealed class Glifo
        {
            public float largura;
            public readonly List<Vector2[]> partes = new List<Vector2[]>();
        }

        /// <summary>O traço, o espaço entre as letras e a largura do espaço em branco, na altura 1.</summary>
        public const float Traco = 0.16f, Entre = 0.13f, Espaco = 0.32f;
        const float Diag = 0.2f;

        static readonly Dictionary<char, Glifo> Glifos = Montar();

        /// <summary>A letra (null: vira espaço).</summary>
        public static Glifo De(char c) => Glifos.TryGetValue(char.ToUpperInvariant(c), out var g) ? g : null;

        /// <summary>A largura de um texto com as letras de altura h.</summary>
        public static float Largura(string texto, float h)
        {
            float x = 0f;
            for (int i = 0; i < texto.Length; i++)
            {
                var g = De(texto[i]);
                x += (g != null ? g.largura : Espaco) + (i < texto.Length - 1 ? Entre : 0f);
            }
            return x * h;
        }

        // ------------------------------------------------------------ as peças

        sealed class Desenho
        {
            public readonly Glifo g;
            public Desenho(float largura) { g = new Glifo { largura = largura }; }

            /// <summary>Um retângulo.</summary>
            public Desenho R(float x0, float y0, float x1, float y1)
            {
                g.partes.Add(new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) });
                return this;
            }

            /// <summary>Uma diagonal de pontas retas (na horizontal): embaixo de xb a xb + w em y0, em cima de xt a xt + w em y1.</summary>
            public Desenho D(float xb, float xt, float y0, float y1, float w = Diag)
            {
                g.partes.Add(new[] { new Vector2(xb, y0), new Vector2(xb + w, y0), new Vector2(xt + w, y1), new Vector2(xt, y1) });
                return this;
            }

            /// <summary>Um traço de a até b, de largura t (as pontas cortadas em ângulo reto).</summary>
            public Desenho L(float ax, float ay, float bx, float by, float t = Traco)
            {
                var a = new Vector2(ax, ay);
                var d = new Vector2(bx, by) - a;
                var n = new Vector2(-d.y, d.x).normalized * (t / 2f);
                g.partes.Add(new[] { a - n, a + d - n, a + d + n, a + n });
                return this;
            }

            /// <summary>Um polígono convexo.</summary>
            public Desenho P(params float[] xy)
            {
                var p = new Vector2[xy.Length / 2];
                for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
                g.partes.Add(p);
                return this;
            }

            /// <summary>
            /// Um arco de elipse com o traço t: o meio (cx, cy), os raios de fora (rx, ry), de a0 a a1 graus (no sentido
            /// anti-horário, 0 para a direita); um pedaço a cada 18° no máximo.
            /// </summary>
            public Desenho A(float cx, float cy, float rx, float ry, float a0, float a1, float t = Traco)
            {
                int n = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / 18f));
                for (int i = 0; i < n; i++)
                {
                    float u0 = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad, u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n) * Mathf.Deg2Rad;
                    Vector2 F(float u, float r, float s) => new Vector2(cx + Mathf.Cos(u) * r, cy + Mathf.Sin(u) * s);
                    g.partes.Add(new[] { F(u0, rx, ry), F(u1, rx, ry), F(u1, rx - t, ry - t), F(u0, rx - t, ry - t) });
                }
                return this;
            }

            /// <summary>As peças de outra letra giradas meia volta em torno de (cx, cy) (o 9 é o 6 de cabeça para baixo).</summary>
            public Desenho Girada(Glifo outra, float cx, float cy)
            {
                foreach (var p in outra.partes)
                {
                    var q = new Vector2[p.Length];
                    for (int i = 0; i < p.Length; i++) q[i] = new Vector2(2f * cx - p[i].x, 2f * cy - p[i].y);
                    g.partes.Add(q);
                }
                return this;
            }
        }

        // ------------------------------------------------------------ as letras

        static Dictionary<char, Glifo> Montar()
        {
            const float T = Traco;
            var d = new Dictionary<char, Glifo>();
            void Por(char c, Desenho x) => d[c] = x.g;
            Desenho N(float w) => new Desenho(w);

            Por('A', N(0.74f).D(0f, 0.27f, 0f, 1f).D(0.54f, 0.27f, 0f, 1f).R(0.15f, 0.25f, 0.59f, 0.25f + T));
            Por('B', N(0.62f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.36f, 1f).R(0f, 0.42f, 0.38f, 0.42f + T).R(0f, 0f, 0.38f, T)
                .A(0.36f, 0.71f, 0.24f, 0.29f, -90f, 90f).A(0.38f, 0.29f, 0.24f, 0.29f, -90f, 90f));
            Por('C', N(0.7f).A(0.36f, 0.5f, 0.36f, 0.5f, 42f, 318f, T + 0.01f));
            Por('D', N(0.68f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.3f, 1f).R(0f, 0f, 0.3f, T).A(0.3f, 0.5f, 0.38f, 0.5f, -90f, 90f));
            Por('E', N(0.54f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.54f, 1f).R(0f, 0.42f, 0.47f, 0.42f + T).R(0f, 0f, 0.54f, T));
            Por('F', N(0.52f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.52f, 1f).R(0f, 0.42f, 0.45f, 0.42f + T));
            Por('G', N(0.74f).A(0.37f, 0.5f, 0.37f, 0.5f, 35f, 345f, T + 0.01f).R(0.58f, 0.1f, 0.74f, 0.5f).R(0.4f, 0.42f, 0.74f, 0.42f + T));
            Por('H', N(0.68f).R(0f, 0f, T, 1f).R(0.68f - T, 0f, 0.68f, 1f).R(0f, 0.42f, 0.68f, 0.42f + T));
            Por('I', N(T).R(0f, 0f, T, 1f));
            Por('J', N(0.52f).R(0.52f - T, 0.3f, 0.52f, 1f).A(0.26f, 0.3f, 0.26f, 0.3f, 180f, 360f));
            Por('K', N(0.66f).R(0f, 0f, T, 1f).D(0.1f, 0.45f, 0.3f, 1f, 0.21f).D(0.45f, 0.2f, 0f, 0.58f, 0.21f));
            Por('L', N(0.52f).R(0f, 0f, T, 1f).R(0f, 0f, 0.52f, T));
            Por('M', N(0.86f).R(0f, 0f, T, 1f).R(0.86f - T, 0f, 0.86f, 1f).D(0.33f, 0f, 0.2f, 1f).D(0.33f, 0.66f, 0.2f, 1f));
            Por('N', N(0.7f).R(0f, 0f, T, 1f).R(0.7f - T, 0f, 0.7f, 1f).D(0.5f, 0f, 0f, 1f));
            Por('O', N(0.78f).A(0.39f, 0.5f, 0.39f, 0.5f, 0f, 360f, T + 0.01f));
            var p = N(0.62f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.32f, 1f).R(0f, 0.4f, 0.32f, 0.4f + T).A(0.32f, 0.7f, 0.3f, 0.3f, -90f, 90f);
            Por('P', p);
            Por('Q', N(0.8f).A(0.39f, 0.5f, 0.39f, 0.5f, 0f, 360f, T + 0.01f).L(0.48f, 0.25f, 0.8f, -0.05f, 0.18f));
            Por('R', N(0.64f).R(0f, 0f, T, 1f).R(0f, 1f - T, 0.32f, 1f).R(0f, 0.4f, 0.32f, 0.4f + T).A(0.32f, 0.7f, 0.3f, 0.3f, -90f, 90f)
                .D(0.44f, 0.22f, 0f, 0.42f));
            Por('S', N(0.62f).A(0.31f, 0.71f, 0.29f, 0.29f, 25f, 270f).A(0.31f, 0.29f, 0.31f, 0.29f, -155f, 90f));
            Por('T', N(0.62f).R(0f, 1f - T, 0.62f, 1f).R(0.23f, 0f, 0.39f, 1f));
            Por('U', N(0.68f).R(0f, 0.34f, T, 1f).R(0.68f - T, 0.34f, 0.68f, 1f).A(0.34f, 0.34f, 0.34f, 0.34f, 180f, 360f));
            Por('V', N(0.72f).D(0.26f, 0f, 0f, 1f).D(0.26f, 0.52f, 0f, 1f));
            Por('W', N(1f).D(0.18f, 0f, 0f, 1f, 0.19f).D(0.18f, 0.41f, 0f, 0.82f, 0.19f).D(0.63f, 0.4f, 0f, 0.82f, 0.19f).D(0.63f, 0.81f, 0f, 1f, 0.19f));
            Por('X', N(0.7f).D(0f, 0.5f, 0f, 1f).D(0.5f, 0f, 0f, 1f));
            Por('Y', N(0.7f).D(0.25f, 0f, 0.45f, 1f).D(0.25f, 0.5f, 0.45f, 1f).R(0.27f, 0f, 0.43f, 0.5f));
            Por('Z', N(0.62f).R(0f, 1f - T, 0.62f, 1f).R(0f, 0f, 0.62f, T).D(0f, 0.62f - 0.22f, T - 0.01f, 1f - T + 0.01f, 0.22f));

            Por('0', N(0.62f).A(0.31f, 0.5f, 0.31f, 0.5f, 0f, 360f));
            Por('1', N(0.42f).R(0.24f, 0f, 0.4f, 1f).L(0.04f, 0.73f, 0.33f, 0.93f));
            Por('2', N(0.62f).A(0.31f, 0.69f, 0.3f, 0.31f, -40f, 165f).L(0.55f, 0.6f, 0.08f, 0.08f, 0.19f).R(0f, 0f, 0.62f, T));
            Por('3', N(0.6f).A(0.29f, 0.71f, 0.28f, 0.29f, -90f, 155f).A(0.29f, 0.29f, 0.31f, 0.29f, -155f, 90f));
            Por('4', N(0.66f).R(0.42f, 0f, 0.58f, 1f).D(0f, 0.33f, 0.26f, 1f, 0.21f).R(0f, 0.22f, 0.66f, 0.22f + T));
            Por('5', N(0.62f).R(0.04f, 1f - T, 0.58f, 1f).R(0.04f, 0.44f, 0.04f + T, 1f).R(0.04f, 0.44f, 0.3f, 0.6f)
                .A(0.3f, 0.3f, 0.32f, 0.3f, -150f, 90f));
            var seis = N(0.62f).A(0.31f, 0.3f, 0.31f, 0.3f, 0f, 360f).A(0.31f, 0.5f, 0.31f, 0.5f, 55f, 200f);
            Por('6', seis);
            Por('7', N(0.6f).R(0f, 1f - T, 0.6f, 1f).D(0.1f, 0.4f, 0f, 1f - T + 0.01f));
            Por('8', N(0.62f).A(0.31f, 0.71f, 0.27f, 0.29f, 0f, 360f).A(0.31f, 0.29f, 0.31f, 0.29f, 0f, 360f));
            Por('9', N(0.62f).Girada(seis.g, 0.31f, 0.5f));

            Por('-', N(0.42f).R(0.04f, 0.42f, 0.38f, 0.42f + T));
            Por('.', N(0.18f).R(0f, 0f, 0.18f, 0.18f));
            Por(':', N(0.18f).R(0f, 0f, 0.18f, 0.18f).R(0f, 0.5f, 0.18f, 0.68f));
            Por('/', N(0.46f).D(0f, 0.27f, 0f, 1f, 0.19f));
            Por('\'', N(T).R(0f, 0.68f, T, 1f));
            Por('+', N(0.6f).R(0f, 0.42f, 0.6f, 0.58f).R(0.22f, 0.2f, 0.38f, 0.8f));
            Por('#', N(0.66f).R(0.14f, 0f, 0.28f, 1f).R(0.38f, 0f, 0.52f, 1f).R(0f, 0.28f, 0.66f, 0.42f).R(0f, 0.6f, 0.66f, 0.74f));
            Por('<', N(0.86f).P(0f, 0.5f, 0.42f, 0.1f, 0.42f, 0.9f).R(0.38f, 0.42f, 0.86f, 0.58f));
            Por('>', N(0.86f).P(0.86f, 0.5f, 0.44f, 0.9f, 0.44f, 0.1f).R(0f, 0.42f, 0.48f, 0.58f));
            var cima = N(0.72f).P(0.36f, 1f, 0f, 0.6f, 0.72f, 0.6f).R(0.28f, 0f, 0.44f, 0.64f);
            Por('^', cima);
            Por('~', N(0.72f).Girada(cima.g, 0.36f, 0.5f));
            // o '&' (a loja de armas, 08/11/2026: "GUNS & AMMO"): a volta de cima, a barriga de baixo aberta à direita e a
            // perna em diagonal até a quina de baixo
            Por('&', N(0.74f).A(0.3f, 0.77f, 0.18f, 0.21f, -60f, 240f, 0.14f).A(0.32f, 0.29f, 0.3f, 0.29f, 70f, 330f)
                .L(0.18f, 0.6f, 0.72f, 0.02f, 0.17f).L(0.5f, 0.38f, 0.72f, 0.5f, 0.14f));
            // o '*' é a estrela de 5 pontas (o pentágono do meio e as 5 pontas)
            var estrela = N(0.8f);
            {
                const float cx = 0.4f, cy = 0.48f, R = 0.42f, r = 0.17f;
                var miolo = new float[10];
                for (int i = 0; i < 5; i++)
                {
                    float a = (90f + 36f + 72f * i) * Mathf.Deg2Rad;
                    miolo[2 * i] = cx + Mathf.Cos(a) * r;
                    miolo[2 * i + 1] = cy + Mathf.Sin(a) * r;
                }
                estrela.P(miolo);
                for (int i = 0; i < 5; i++)
                {
                    float a = (90f + 72f * i) * Mathf.Deg2Rad, a0 = a - 36f * Mathf.Deg2Rad, a1 = a + 36f * Mathf.Deg2Rad;
                    estrela.P(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a) * R, cy + Mathf.Sin(a) * R, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r);
                }
            }
            Por('*', estrela);
            return d;
        }
    }
}
