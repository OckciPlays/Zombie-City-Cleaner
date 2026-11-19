using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A casca de verdade dos pinheiros (18/11/2026, o corte em cunha, CorteNaMadeira.cs). O tronco do modelo é um prisma de 6
    /// lados que afina para cima, com os anéis torcidos uns em relação aos outros; a cápsula de colisão (e o raio da tabela da
    /// floresta) não serve para colar o corte na casca (ele ficava 4 a 6 cm para fora ou para dentro). Uma vez por espécie, a
    /// malha do LOD0 vira um colisor escondido, e raios de fora para dentro medem o raio da casca de 5 em 5 cm de altura (até
    /// 3 m) e de 5 em 5 graus: a tabela. Depois, cada pergunta é só a conta na tabela (com a escala e o giro da planta).
    /// </summary>
    public partial class LakewoodNaCena
    {
        const float PassoDaAltura = 0.05f, AlturaDaTabela = 3f;
        const int AngulosDaTabela = 72;
        static readonly int AlturasDaTabela = Mathf.RoundToInt(AlturaDaTabela / PassoDaAltura) + 1;

        // a tabela de cada espécie: [altura, ângulo] = o raio (m, no prefab sem escala); null: sem a malha
        readonly Dictionary<int, float[,]> _cascas = new Dictionary<int, float[,]>();

        float[,] TabelaDaCasca(int especie)
        {
            if (_cascas.TryGetValue(especie, out var tabela)) return tabela;
            _cascas[especie] = null;
            var prefab = pinheiros != null && especie >= 0 && especie < pinheiros.Length ? pinheiros[especie] : null;
            if (prefab == null) return null;
            var lod = prefab.GetComponent<LODGroup>();
            Renderer r0 = null;
            if (lod != null && lod.lodCount > 0)
                foreach (var r in lod.GetLODs()[0].renderers) if (r != null) { r0 = r; break; }
            if (r0 == null) r0 = prefab.GetComponentInChildren<MeshRenderer>();
            var mf = r0 != null ? r0.GetComponent<MeshFilter>() : null;
            var malha = mf != null ? mf.sharedMesh : null;
            if (malha == null) return null;
            // o colisor da malha, escondido longe de tudo, com a pose do LOD0 dentro do prefab
            var para = prefab.transform.worldToLocalMatrix * r0.transform.localToWorldMatrix;
            var longe = new Vector3(0f, -20000f, 0f);
            var go = new GameObject("Casca (medida)");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(longe + para.GetColumn(3).ToVector3(), para.rotation);
            go.transform.localScale = para.lossyScale;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = malha;
            Physics.SyncTransforms();
            tabela = new float[AlturasDaTabela, AngulosDaTabela];
            for (int i = 0; i < AlturasDaTabela; i++)
            {
                float y = i * PassoDaAltura;
                for (int k = 0; k < AngulosDaTabela; k++)
                {
                    float ang = k * 2f * Mathf.PI / AngulosDaTabela;
                    var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    var de = longe + Vector3.up * y + dir * 3f;
                    tabela[i, k] = mc.Raycast(new Ray(de, -dir), out var hit, 3f) ? 3f - hit.distance : -1f;
                }
            }
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            _cascas[especie] = tabela;
            return tabela;
        }

        /// <summary>
        /// O raio da casca do pinheiro na altura (m, do pé, ao longo do tronco) e no ângulo do mundo (radianos, o atan2(x, z),
        /// com o tronco em pé; no caído, no espaço do eixo dele). Sem a tabela (ou fora dela), -1.
        /// </summary>
        public float RaioDaCasca(PlantaDaFloresta pl, float altura, float angulo)
        {
            var tabela = TabelaDaCasca(IndiceDoPinheiro(pl.Especie));
            if (tabela == null) return -1f;
            float escala = Mathf.Max(0.05f, pl.Escala);
            float y = Mathf.Clamp(altura / escala / PassoDaAltura, 0f, AlturasDaTabela - 1.001f);
            float a = Mathf.Repeat((angulo - pl.Giro * Mathf.Deg2Rad) / (2f * Mathf.PI) * AngulosDaTabela, AngulosDaTabela);
            int i0 = Mathf.FloorToInt(y), k0 = Mathf.FloorToInt(a) % AngulosDaTabela, k1 = (k0 + 1) % AngulosDaTabela;
            float fy = y - i0, fa = a - Mathf.Floor(a);
            float v00 = tabela[i0, k0], v01 = tabela[i0, k1], v10 = tabela[i0 + 1, k0], v11 = tabela[i0 + 1, k1];
            if (v00 < 0f || v01 < 0f || v10 < 0f || v11 < 0f) return -1f;
            // (o maior dos vizinhos no ângulo, um pouco: entre dois raios, a quina do polígono pode passar da reta)
            float r = Mathf.Lerp(Mathf.Lerp(v00, v01, fa), Mathf.Lerp(v10, v11, fa), fy);
            float maior = Mathf.Max(Mathf.Max(v00, v01), Mathf.Max(v10, v11));
            return Mathf.Lerp(r, maior, 0.35f) * escala;
        }
    }

    static class MatrizDaCasca
    {
        public static Vector3 ToVector3(this Vector4 v) => new Vector3(v.x, v.y, v.z);
    }
}
