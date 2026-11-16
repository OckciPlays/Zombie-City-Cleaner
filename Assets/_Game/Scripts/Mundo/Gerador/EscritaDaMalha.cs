using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Escreve as listas de uma malha num Mesh de uma vez (16/11/2026, o teste de desempenho no Lakewood): o formato inteiro dos
    /// vértices primeiro e os dados copiados direto em cada fluxo, com os índices em 32 bits. Com o SetVertices, o SetNormals,
    /// o SetUVs e o SetColors um depois do outro, cada canal novo refazia o formato do Mesh e copiava os que já estavam (o
    /// "CopyChannels" do Profiler: de 2 a 14 ms numa obra, no quadro principal). Só na thread principal.
    /// Os fluxos (a Unity aceita até 4): 0 a posição, 1 a normal, 2 a cor e 3 os UVs (com os dois UVs, juntos no mesmo fluxo,
    /// um Vector4 por vértice). Os canais que vierem null (ou menores que a posição) ficam de fora.
    /// </summary>
    public static class EscritaDaMalha
    {
        const MeshUpdateFlags Sem = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        // os dois UVs juntos (reaproveitado; só na thread principal)
        static readonly List<Vector4> _uvs = new List<Vector4>();

        public static void Escrever(Mesh m, List<Vector3> v, List<Vector3> n, List<Vector2> uv0, List<int> t,
                                    List<Vector2> uv1 = null, List<Color> cor = null, List<Color32> cor32 = null)
        {
            int nv = v.Count;
            bool temN = n != null && n.Count >= nv && nv > 0;
            bool temUv0 = uv0 != null && uv0.Count >= nv && nv > 0;
            bool temUv1 = uv1 != null && uv1.Count >= nv && nv > 0;
            bool temCor = cor != null && cor.Count >= nv && nv > 0;
            bool temCor32 = !temCor && cor32 != null && cor32.Count >= nv && nv > 0;
            bool doisUvs = temUv1;   // (o uv0 vai junto, mesmo vazio)
            int atributos = 1 + (temN ? 1 : 0) + (temCor || temCor32 ? 1 : 0) + (doisUvs ? 2 : temUv0 ? 1 : 0);
            var formato = new VertexAttributeDescriptor[atributos];
            int c = 0, fluxo = 0;
            formato[c++] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, fluxo++);
            if (temN) formato[c++] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, fluxo++);
            if (temCor) formato[c++] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, fluxo++);
            if (temCor32) formato[c++] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, fluxo++);
            if (doisUvs)
            {
                formato[c++] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, fluxo);
                formato[c++] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, fluxo++);
            }
            else if (temUv0) formato[c++] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, fluxo++);
            m.SetVertexBufferParams(nv, formato);
            fluxo = 0;
            if (nv > 0) m.SetVertexBufferData(v, 0, 0, nv, fluxo, Sem);
            fluxo++;
            if (temN) m.SetVertexBufferData(n, 0, 0, nv, fluxo++, Sem);
            if (temCor) m.SetVertexBufferData(cor, 0, 0, nv, fluxo++, Sem);
            if (temCor32) m.SetVertexBufferData(cor32, 0, 0, nv, fluxo++, Sem);
            if (doisUvs)
            {
                _uvs.Clear();
                if (_uvs.Capacity < nv) _uvs.Capacity = nv;
                for (int i = 0; i < nv; i++)
                {
                    var a = temUv0 ? uv0[i] : Vector2.zero;
                    var b = uv1[i];
                    _uvs.Add(new Vector4(a.x, a.y, b.x, b.y));
                }
                m.SetVertexBufferData(_uvs, 0, 0, nv, fluxo++, Sem);
            }
            else if (temUv0) m.SetVertexBufferData(uv0, 0, 0, nv, fluxo++, Sem);
            int nt = t != null ? t.Count : 0;
            m.SetIndexBufferParams(nt, IndexFormat.UInt32);
            if (nt > 0) m.SetIndexBufferData(t, 0, 0, nt, Sem);
            m.subMeshCount = 1;
            m.SetSubMesh(0, new SubMeshDescriptor(0, nt) { firstVertex = 0, vertexCount = nv }, Sem);
            m.RecalculateBounds();
        }
    }
}
