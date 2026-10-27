using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Os dados de uma malha montada fora da thread principal (a Unity só cria o Mesh na principal).</summary>
    public sealed class MalhaDoBloco
    {
        public Vector3[] Vertices;
        public Vector3[] Normais;
        public Color32[] Cores;
        public int[] Triangulos;
        public Bounds Limites;
        public bool Vazia => Triangulos == null || Triangulos.Length == 0;

        /// <summary>Passa para um Mesh (novo ou reaproveitado). Só na thread principal.</summary>
        public void Para(Mesh m)
        {
            m.Clear();
            m.indexFormat = Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(Vertices);
            m.SetNormals(Normais);
            if (Cores != null) m.SetColors(Cores);
            m.SetTriangles(Triangulos, 0, false);
            m.bounds = Limites;
        }
    }

    /// <summary>
    /// As malhas do terreno a partir do <see cref="DadosDoMundo"/> (arrays puros, seguros em outra thread; o
    /// ChunkMeshBuilder do Guardiões da Floresta):
    ///  - o terreno de um bloco, low-poly de faces planas (os vértices não são divididos), com os pesos das texturas do
    ///    chão na cor do vértice (<see cref="DadosDoMundo.PesosDoChao"/>). O nível de detalhe é o passo da grade (1, 2 ou
    ///    4 células). Saias verticais nas bordas escondem as frestas entre blocos de níveis diferentes. No passo 1, a
    ///    diagonal dos triângulos é a mesma do <see cref="DadosDoMundo.Altura"/>, e as células de buraco ficam de fora;
    ///  - a água do lago: as células com algum canto abaixo da água, e as vizinhas. A cor do vértice (R) diz a fundura
    ///    (0 na margem, 1 a partir de 3 m);
    ///  - o terreno de concreto da partida e a prévia do mapa inteiro.
    /// Os vértices ficam em coordenadas do MUNDO (o objeto do bloco fica na origem).
    /// </summary>
    public static class MalhasDoTerreno
    {
        const float FunduraDaSaia = 3f;

        public static MalhaDoBloco Terreno(DadosDoMundo d, int bx, int bz, int passo)
        {
            int n = DadosDoMundo.CelulasPorBloco / passo;
            int i0 = bx * DadosDoMundo.CelulasPorBloco, j0 = bz * DadosDoMundo.CelulasPorBloco;
            var vertices = new Vector3[n * n * 6 + n * 4 * 12];
            var normais = new Vector3[vertices.Length];
            var cores = new Color32[vertices.Length];
            int v = 0;
            float minY = float.MaxValue, maxY = float.MinValue;

            Vector3 P(int i, int j)
            {
                var p = d.PosicaoDoVertice(i, j);
                return new Vector3(p.x, d.AlturaDoVertice(i, j), p.y);
            }
            // os pesos de cada vértice do bloco, calculados uma vez só
            int lado = n + 1;
            var pesos = new Color32[lado * lado];
            for (int qj = 0; qj <= n; qj++)
            for (int qi = 0; qi <= n; qi++)
                pesos[qj * lado + qi] = d.PesosDoChao(i0 + qi * passo, j0 + qj * passo);
            Color32 C(int qi, int qj) => pesos[qj * lado + qi];

            void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc)
            {
                var nrm = Vector3.Cross(b - a, c - a).normalized;
                vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c;
                normais[v] = nrm; normais[v + 1] = nrm; normais[v + 2] = nrm;
                cores[v] = ca; cores[v + 1] = cb; cores[v + 2] = cc;
                v += 3;
                minY = Mathf.Min(minY, Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
                maxY = Mathf.Max(maxY, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            }

            for (int qj = 0; qj < n; qj++)
            for (int qi = 0; qi < n; qi++)
            {
                int i = i0 + qi * passo, j = j0 + qj * passo;
                if (passo == 1 && d.Buraco(i, j)) continue;
                int i2 = i + passo, j2 = j + passo;
                Vector3 a = P(i, j), b = P(i2, j), c = P(i2, j2), dd = P(i, j2);
                Color32 ca = C(qi, qj), cb = C(qi + 1, qj), cc = C(qi + 1, qj + 1), cd = C(qi, qj + 1);
                // a diagonal alterna (no passo 1, a mesma da consulta de altura)
                if ((((i / passo) + (j / passo)) & 1) == 0)
                {
                    Tri(a, dd, c, ca, cd, cc);
                    Tri(a, c, b, ca, cc, cb);
                }
                else
                {
                    Tri(a, dd, b, ca, cd, cb);
                    Tri(b, dd, c, cb, cd, cc);
                }
            }

            // as saias: faixas verticais nas 4 bordas (dos dois lados, para não depender da ordem dos vértices). Na beira de uma
            // célula de buraco, não: a saia ficava de pé no meio do porão (uma parede de terra de 3 m, e o NavMesh partido)
            void Saia(int qa, int ra, int qb, int rb)
            {
                if (passo == 1)
                {
                    int ci = i0 + Mathf.Min(Mathf.Min(qa, qb), n - 1), cj = j0 + Mathf.Min(Mathf.Min(ra, rb), n - 1);
                    if (d.Buraco(ci, cj)) return;
                }
                Vector3 a = P(i0 + qa * passo, j0 + ra * passo), b = P(i0 + qb * passo, j0 + rb * passo);
                Vector3 a2 = a + Vector3.down * FunduraDaSaia, b2 = b + Vector3.down * FunduraDaSaia;
                Color32 ca = C(qa, ra), cb = C(qb, rb);
                Tri(a, b, b2, ca, cb, cb);
                Tri(a, b2, a2, ca, cb, ca);
                Tri(a, b2, b, ca, cb, cb);
                Tri(a, a2, b2, ca, ca, cb);
            }
            for (int q = 0; q < n; q++)
            {
                Saia(q, 0, q + 1, 0);
                Saia(q, n, q + 1, n);
                Saia(0, q, 0, q + 1);
                Saia(n, q, n, q + 1);
            }

            var triangulos = new int[v];
            for (int k = 0; k < v; k++) triangulos[k] = k;
            if (v < vertices.Length)
            {
                System.Array.Resize(ref vertices, v);
                System.Array.Resize(ref normais, v);
                System.Array.Resize(ref cores, v);
            }
            var canto = d.CantoDoBloco(bx, bz);
            float t = DadosDoMundo.TamanhoDoBloco;
            return new MalhaDoBloco
            {
                Vertices = vertices,
                Normais = normais,
                Cores = cores,
                Triangulos = triangulos,
                Limites = new Bounds(new Vector3(canto.x + t * 0.5f, (minY + maxY) * 0.5f, canto.y + t * 0.5f),
                                     new Vector3(t, Mathf.Max(0.1f, maxY - minY), t)),
            };
        }

        /// <summary>A água do lago no bloco (vazia se não houver). Células de uma célula da grade.</summary>
        public static MalhaDoBloco Agua(DadosDoMundo d, int bx, int bz)
        {
            var vazia = new MalhaDoBloco { Vertices = new Vector3[0], Triangulos = new int[0] };
            var lago = d.Lago;
            if (lago == null) return vazia;
            var centro = d.CentroDoBloco(bx, bz);
            float alcance = lago.Raio * 1.35f + DadosDoMundo.TamanhoDoBloco;
            if (Mathf.Abs(centro.x - lago.Centro.x) > alcance || Mathf.Abs(centro.y - lago.Centro.y) > alcance) return vazia;
            return AguaNaGrade(d, bx * DadosDoMundo.CelulasPorBloco, bz * DadosDoMundo.CelulasPorBloco, DadosDoMundo.CelulasPorBloco, 1);
        }

        // a água num retângulo da grade (i0, j0) de n × n passos de "passo" células
        static MalhaDoBloco AguaNaGrade(DadosDoMundo d, int i0, int j0, int n, int passo)
        {
            float nivel = d.Lago.Nivel;
            // só na bacia: fora dela, um vale mais baixo que a água não ganha água (ficaria uma lâmina no ar)
            bool Molhada(int i, int j)
            {
                if (i < 0 || j < 0 || i + passo > d.Celulas || j + passo > d.Celulas) return false;
                var meio = d.PosicaoDoVertice(i, j) + Vector2.one * (passo * DadosDoMundo.Celula * 0.5f);
                if (d.Lago.Q(meio.x, meio.y) > 1.35f) return false;
                return d.AlturaDoVertice(i, j) < nivel + 0.15f || d.AlturaDoVertice(i + passo, j) < nivel + 0.15f
                    || d.AlturaDoVertice(i, j + passo) < nivel + 0.15f || d.AlturaDoVertice(i + passo, j + passo) < nivel + 0.15f;
            }
            var molhada = new bool[n + 2, n + 2];
            bool alguma = false;
            for (int qj = -1; qj <= n; qj++)
            for (int qi = -1; qi <= n; qi++)
            {
                bool w = Molhada(i0 + qi * passo, j0 + qj * passo);
                molhada[qi + 1, qj + 1] = w;
                alguma |= w;
            }
            if (!alguma) return new MalhaDoBloco { Vertices = new Vector3[0], Triangulos = new int[0] };

            var vertices = new List<Vector3>();
            var cores = new List<Color32>();
            var tris = new List<int>();
            var indice = new Dictionary<int, int>();
            int V(int qi, int qj)
            {
                int chave = qj * (n + 1) + qi;
                if (indice.TryGetValue(chave, out int id)) return id;
                id = vertices.Count;
                int i = i0 + qi * passo, j = j0 + qj * passo;
                var p = d.PosicaoDoVertice(i, j);
                vertices.Add(new Vector3(p.x, nivel, p.y));
                float fundura = nivel - d.AlturaDoVertice(Mathf.Min(i, d.Celulas), Mathf.Min(j, d.Celulas));
                cores.Add(new Color32((byte)(Mathf.Clamp01(fundura / 3f) * 255f), 0, 0, 255));
                indice[chave] = id;
                return id;
            }
            for (int qj = 0; qj < n; qj++)
            for (int qi = 0; qi < n; qi++)
            {
                bool usa = false;
                for (int dj = 0; dj <= 2 && !usa; dj++)
                for (int di = 0; di <= 2 && !usa; di++)
                    usa = molhada[qi + di, qj + dj];
                if (!usa) continue;
                int a = V(qi, qj), b = V(qi + 1, qj), c = V(qi + 1, qj + 1), e = V(qi, qj + 1);
                tris.Add(a); tris.Add(e); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            var normais = new Vector3[vertices.Count];
            for (int k = 0; k < normais.Length; k++) normais[k] = Vector3.up;
            var canto = d.PosicaoDoVertice(i0, j0);
            float lado = n * passo * DadosDoMundo.Celula;
            return new MalhaDoBloco
            {
                Vertices = vertices.ToArray(),
                Normais = normais,
                Cores = cores.ToArray(),
                Triangulos = tris.ToArray(),
                Limites = new Bounds(new Vector3(canto.x + lado * 0.5f, nivel, canto.y + lado * 0.5f), new Vector3(lado, 1f, lado)),
            };
        }

        /// <summary>O terreno de concreto da partida, no lugar e no giro dela (no lote da casa da família, até a casa de verdade, na Parte M6).</summary>
        public static Mesh Partida(DadosDoMundo dados)
        {
            var obra = new MalhaDeObra();
            float h = DadosDoMundo.AlturaDaPartida, l = DadosDoMundo.LadoDaPartida;
            var giro = Quaternion.Euler(0f, dados.GiroDaPartida, 0f);
            // a laje desce até o chão mais baixo embaixo dela (no lote, que pode ser um pouco inclinado; no platô, 0,3 m), e a
            // borda de cima fica em AlturaDaPartida
            float fundo = Mathf.Min(dados.FundoDaPartida, dados.Partida.y + h - 0.3f), alto = dados.Partida.y + h - fundo;
            obra.Caixa(Matrix4x4.TRS(new Vector3(dados.Partida.x, fundo + alto * 0.5f, dados.Partida.z), giro, Vector3.one), new Vector3(l, alto, l), Cor.PorNormal("Concreto", "Calcada"));
            // uma faixa amarela na frente, para onde a família olha
            obra.Caixa(Matrix4x4.TRS(dados.Partida + giro * new Vector3(0f, h + 0.004f, l / 2f - 0.6f), giro, Vector3.one), new Vector3(l - 1.2f, 0.01f, 0.25f), "Faixa");
            return obra.ParaMesh("Partida");
        }

        /// <summary>O mapa inteiro numa malha só, com células de "celula" m (a prévia do Editor). UInt32.</summary>
        public static Mesh Mapa(DadosDoMundo d, float celula)
        {
            int passo = Mathf.Max(1, Mathf.RoundToInt(celula / DadosDoMundo.Celula));
            int n = d.Celulas / passo;
            var vertices = new Vector3[n * n * 6];
            var normais = new Vector3[vertices.Length];
            var cores = new Color32[vertices.Length];
            int v = 0;
            Vector3 P(int i, int j)
            {
                var p = d.PosicaoDoVertice(i, j);
                return new Vector3(p.x, d.AlturaDoVertice(i, j), p.y);
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc)
            {
                var nrm = Vector3.Cross(b - a, c - a).normalized;
                vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c;
                normais[v] = normais[v + 1] = normais[v + 2] = nrm;
                cores[v] = ca; cores[v + 1] = cb; cores[v + 2] = cc;
                v += 3;
            }
            for (int qj = 0; qj < n; qj++)
            for (int qi = 0; qi < n; qi++)
            {
                int i = qi * passo, j = qj * passo;
                Vector3 a = P(i, j), b = P(i + passo, j), c = P(i + passo, j + passo), e = P(i, j + passo);
                Color32 ca = d.PesosDoChao(i, j), cb = d.PesosDoChao(i + passo, j), cc = d.PesosDoChao(i + passo, j + passo), ce = d.PesosDoChao(i, j + passo);
                Tri(a, e, c, ca, ce, cc);
                Tri(a, c, b, ca, cc, cb);
            }
            var tris = new int[v];
            for (int k = 0; k < v; k++) tris[k] = k;
            var m = new Mesh { name = "Prévia do terreno", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(vertices, 0, v);
            m.SetNormals(normais, 0, v);
            m.SetColors(cores, 0, v);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A água do lago inteira numa malha só, com células de "celula" m (a prévia do Editor); null sem lago.</summary>
        public static Mesh AguaDoMapa(DadosDoMundo d, float celula)
        {
            if (d.Lago == null) return null;
            int passo = Mathf.Max(1, Mathf.RoundToInt(celula / DadosDoMundo.Celula));
            float alcance = d.Lago.Raio * 1.35f;
            var canto = d.Lago.Centro - Vector2.one * alcance;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((canto.x + d.MetadeDaGrade) / DadosDoMundo.Celula / passo) * passo);
            int j0 = Mathf.Max(0, Mathf.FloorToInt((canto.y + d.MetadeDaGrade) / DadosDoMundo.Celula / passo) * passo);
            int n = Mathf.CeilToInt(alcance * 2f / DadosDoMundo.Celula / passo);
            n = Mathf.Min(n, (d.Celulas - Mathf.Max(i0, j0)) / passo);
            var malha = AguaNaGrade(d, i0, j0, n, passo);
            if (malha.Vazia) return null;
            var m = new Mesh { name = "Prévia da água" };
            malha.Para(m);
            return m;
        }
    }
}
