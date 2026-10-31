using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os móveis novos de uma construção numa malha só, em dados (montada em outra thread; o Mesh sai na principal): o que
    /// o material da mobília lê (o UV0 em metros, o UV1 com a textura e a troca e a cor do vértice), com a variação de cada
    /// móvel já pintada. As listas vêm de um estoque (<see cref="Emprestar"/>/<see cref="Devolver"/>), como as das obras.
    /// </summary>
    public sealed class MalhaDaMobilia
    {
        public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
        public readonly List<Vector2> U0 = new List<Vector2>(), U1 = new List<Vector2>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<int> T = new List<int>();

        public int Vertices => V.Count;
        public int Triangulos => T.Count / 3;

        /// <summary>Um modelo em dados (as listas da malha dele, só lidas).</summary>
        public sealed class Modelo
        {
            public Vector3[] v, n;
            public Vector2[] u0, u1;
            public Color[] c;
            public int[] t;

            public static Modelo De(Mesh m)
            {
                var r = new Modelo { v = m.vertices, n = m.normals, c = m.colors, t = m.triangles };
                var uv = new List<Vector2>();
                m.GetUVs(0, uv);
                r.u0 = uv.Count == r.v.Length ? uv.ToArray() : new Vector2[r.v.Length];
                m.GetUVs(1, uv);
                r.u1 = uv.Count == r.v.Length ? uv.ToArray() : new Vector2[r.v.Length];
                if (r.c == null || r.c.Length != r.v.Length)
                {
                    r.c = new Color[r.v.Length];
                    for (int i = 0; i < r.c.Length; i++) r.c[i] = Color.white;
                }
                if (r.n == null || r.n.Length != r.v.Length) r.n = new Vector3[r.v.Length];
                return r;
            }
        }

        static readonly ConcurrentBag<MalhaDaMobilia> _estoque = new ConcurrentBag<MalhaDaMobilia>();

        public static MalhaDaMobilia Emprestar() => _estoque.TryTake(out var m) ? m : new MalhaDaMobilia();

        public static void Devolver(MalhaDaMobilia m)
        {
            if (m == null) return;
            m.Limpar();
            _estoque.Add(m);
        }

        public void Limpar()
        {
            V.Clear(); N.Clear(); U0.Clear(); U1.Clear(); C.Clear(); T.Clear();
        }

        /// <summary>O modelo no lugar (a matriz: o giro em y, a escala por igual), pintado com a variação.</summary>
        public void Juntar(Modelo m, Matrix4x4 matriz, CatalogoDaMobilia.Variacao variacao)
        {
            int b = V.Count;
            for (int i = 0; i < m.v.Length; i++)
            {
                V.Add(matriz.MultiplyPoint3x4(m.v[i]));
                N.Add(matriz.MultiplyVector(m.n[i]).normalized);
                Vector2 a = m.u0[i], u = m.u1[i];
                var c = m.c[i];
                CatalogoDaMobilia.Pintar(ref a, ref u, ref c, variacao);
                U0.Add(a);
                U1.Add(u);
                C.Add(c);
            }
            foreach (int t in m.t) T.Add(b + t);
        }

        /// <summary>O Mesh (na thread principal).</summary>
        public Mesh ParaMesh(string nome)
        {
            var m = new Mesh { name = nome, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, U0);
            m.SetUVs(1, U1);
            m.SetColors(C);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
