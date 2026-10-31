using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As contas de linhas e polígonos do mapa fixo (Plano_Mapa_Fixo, Parte F1): suavizar os pontos do desenho (Chaikin),
    /// comprimentos, distâncias, cruzamentos, dentro ou fora e a varredura de um polígono numa grade. Código puro: pode rodar
    /// em outra thread.
    /// </summary>
    public static class Linhas
    {
        /// <summary>
        /// Os pontos suavizados por Chaikin ("vezes" passadas): cada lado vira dois pontos a 1/4 e 3/4 dele. Aberta, a linha
        /// guarda as pontas; fechada, o último ponto liga no primeiro.
        /// </summary>
        public static List<Vector2> Suavizar(IList<Vector2> pontos, int vezes, bool fechada)
        {
            var atual = new List<Vector2>(pontos);
            for (int v = 0; v < vezes && atual.Count >= (fechada ? 3 : 2); v++)
            {
                int n = atual.Count;
                var novo = new List<Vector2>(n * 2 + 2);
                if (!fechada) novo.Add(atual[0]);
                int lados = fechada ? n : n - 1;
                for (int i = 0; i < lados; i++)
                {
                    var a = atual[i];
                    var b = atual[(i + 1) % n];
                    novo.Add(a * 0.75f + b * 0.25f);
                    novo.Add(a * 0.25f + b * 0.75f);
                }
                if (!fechada) novo.Add(atual[n - 1]);
                atual = novo;
            }
            return atual;
        }

        public static float Comprimento(IList<Vector2> pontos, bool fechada = false)
        {
            float soma = 0f;
            for (int i = 1; i < pontos.Count; i++) soma += Vector2.Distance(pontos[i - 1], pontos[i]);
            if (fechada && pontos.Count > 2) soma += Vector2.Distance(pontos[pontos.Count - 1], pontos[0]);
            return soma;
        }

        /// <summary>A distância do ponto ao segmento, e onde (de 0 em a a 1 em b) fica o ponto mais perto.</summary>
        public static float DistanciaDoSegmento(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A distância até a linha e a fração do comprimento no ponto mais perto (de 0 no começo a 1 no fim).</summary>
        public static float Distancia(Vector2 p, IList<Vector2> pontos, out float fracao, bool fechada = false)
        {
            float melhor = float.MaxValue, acum = 0f, total = Mathf.Max(Comprimento(pontos, fechada), 1e-6f);
            fracao = 0f;
            int n = pontos.Count, lados = fechada ? n : n - 1;
            for (int i = 0; i < lados; i++)
            {
                var a = pontos[i];
                var b = pontos[(i + 1) % n];
                float d = DistanciaDoSegmento(p, a, b, out float t);
                float l = Vector2.Distance(a, b);
                if (d < melhor)
                {
                    melhor = d;
                    fracao = (acum + l * t) / total;
                }
                acum += l;
            }
            return melhor;
        }

        public static float Distancia(Vector2 p, IList<Vector2> pontos, bool fechada = false) => Distancia(p, pontos, out _, fechada);

        /// <summary>O ponto da linha na fração do comprimento, e a direção dela ali.</summary>
        public static Vector2 PontoNa(IList<Vector2> pontos, float fracao, out Vector2 direcao)
        {
            direcao = Vector2.right;
            if (pontos.Count == 0) return Vector2.zero;
            if (pontos.Count == 1) return pontos[0];
            float alvo = Mathf.Clamp01(fracao) * Comprimento(pontos), acum = 0f;
            for (int i = 1; i < pontos.Count; i++)
            {
                float l = Vector2.Distance(pontos[i - 1], pontos[i]);
                direcao = l > 1e-6f ? (pontos[i] - pontos[i - 1]) / l : direcao;
                if (acum + l >= alvo || i == pontos.Count - 1)
                    return Vector2.Lerp(pontos[i - 1], pontos[i], l > 1e-6f ? Mathf.Clamp01((alvo - acum) / l) : 0f);
                acum += l;
            }
            return pontos[pontos.Count - 1];
        }

        public static bool Dentro(Vector2 p, IList<Vector2> poligono)
        {
            bool dentro = false;
            for (int i = 0, j = poligono.Count - 1; i < poligono.Count; j = i++)
            {
                var a = poligono[i];
                var b = poligono[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
            }
            return dentro;
        }

        public static float Area(IList<Vector2> poligono)
        {
            float s = 0f;
            for (int i = 0; i < poligono.Count; i++)
            {
                var a = poligono[i];
                var b = poligono[(i + 1) % poligono.Count];
                s += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(s) * 0.5f;
        }

        /// <summary>O centro de massa do polígono (a média dos pontos, se ele não tiver área).</summary>
        public static Vector2 Centro(IList<Vector2> poligono)
        {
            double cx = 0, cy = 0, a2 = 0;
            for (int i = 0; i < poligono.Count; i++)
            {
                var p = poligono[i];
                var q = poligono[(i + 1) % poligono.Count];
                double c = (double)p.x * q.y - (double)q.x * p.y;
                a2 += c;
                cx += (p.x + q.x) * c;
                cy += (p.y + q.y) * c;
            }
            if (Math.Abs(a2) < 1e-6)
            {
                var m = Vector2.zero;
                foreach (var p in poligono) m += p;
                return poligono.Count > 0 ? m / poligono.Count : m;
            }
            return new Vector2((float)(cx / (3 * a2)), (float)(cy / (3 * a2)));
        }

        /// <summary>Onde os segmentos ab e cd se cruzam (ta e tc: a fração em cada um).</summary>
        public static bool Cruzam(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 ponto, out float ta, out float tc)
        {
            var r = b - a;
            var s = d - c;
            float den = r.x * s.y - r.y * s.x;
            ponto = default;
            ta = tc = 0f;
            if (Mathf.Abs(den) < 1e-9f) return false;
            var ac = c - a;
            ta = (ac.x * s.y - ac.y * s.x) / den;
            tc = (ac.x * r.y - ac.y * r.x) / den;
            if (ta < 0f || ta > 1f || tc < 0f || tc > 1f) return false;
            ponto = a + r * ta;
            return true;
        }

        /// <summary>
        /// A varredura do polígono numa grade de ni × nj elementos (o elemento (i, j) tem o meio em x0 + i·passo,
        /// z0 + j·passo): para cada linha j, as faixas de i0 a i1 (com as pontas) que ficam dentro dele. Sem testar ponto por
        /// ponto: os cruzamentos de cada linha com os lados, como no preenchimento de polígonos.
        /// </summary>
        public static void Varrer(IList<Vector2> poligono, float x0, float z0, float passo, int ni, int nj, Action<int, int, int> faixa)
        {
            if (poligono.Count < 3) return;
            float zmin = float.MaxValue, zmax = float.MinValue;
            foreach (var p in poligono)
            {
                zmin = Mathf.Min(zmin, p.y);
                zmax = Mathf.Max(zmax, p.y);
            }
            int j0 = Mathf.Max(0, Mathf.CeilToInt((zmin - z0) / passo)), j1 = Mathf.Min(nj - 1, Mathf.FloorToInt((zmax - z0) / passo));
            var cortes = new List<float>(16);
            for (int j = j0; j <= j1; j++)
            {
                float z = z0 + j * passo;
                cortes.Clear();
                for (int a = 0, b = poligono.Count - 1; a < poligono.Count; b = a++)
                {
                    var p = poligono[a];
                    var q = poligono[b];
                    if ((p.y > z) != (q.y > z)) cortes.Add(p.x + (z - p.y) / (q.y - p.y) * (q.x - p.x));
                }
                cortes.Sort();
                for (int k = 0; k + 1 < cortes.Count; k += 2)
                {
                    int i0 = Mathf.Max(0, Mathf.CeilToInt((cortes[k] - x0) / passo));
                    int i1 = Mathf.Min(ni - 1, Mathf.FloorToInt((cortes[k + 1] - x0) / passo));
                    if (i1 >= i0) faixa(j, i0, i1);
                }
            }
        }
    }
}
