using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O lugar dos itens do saque na mão (31/10/2026): nos braços de primeira pessoa da lata e da garrafa e no item do corpo
    /// (Arma_Lata, Arma_Garrafa), o <see cref="ItemNaMaoProprio"/>, com o "Item na mão" na pose da lata (a malha avulsa,
    /// Item_Lata, encaixada na lata dos braços: o mesmo espaço, então os modelos do loot.py, feitos no espaço da lata, caem
    /// na mão do mesmo jeito) e, nos braços, a malha sem a lata (Malhas/FP_Bracos_&lt;modelo&gt;_SemItem.asset).
    /// Chamada pela montagem do prefab (<see cref="MontagemDaRede"/>).
    /// </summary>
    static class MontagemDoItemNaMao
    {
        const string Malhas = "Assets/_Game/Art/Characters/Jogador/Malhas";

        /// <summary>Os modelos de mão que levam os itens do saque: a lata e a garrafa.</summary>
        internal static bool Leva(int modelo) => modelo == Itens.Modelo(Item.Comida) || modelo == Itens.Modelo(Item.Agua);

        /// <summary>Nos braços de primeira pessoa do modelo (a lata ou a garrafa).</summary>
        internal static void NosBracos(GameObject fp, int modelo, SkinnedMeshRenderer smr)
        {
            var malha = smr.sharedMesh;
            var mats = smr.sharedMaterials;
            var materialDosItens = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            int c = System.Array.IndexOf(mats, materialDosItens);
            if (c < 0) c = mats.Length - 1;
            var avulsa = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDo(modelo));
            var ordem = PrimeiraVez(malha.GetTriangles(c));
            var verts = malha.vertices;
            var encaixe = Encaixe(PrimeiraVez(avulsa.triangles).Select(i => avulsa.vertices[i]).ToArray(), ordem.Select(i => verts[i]).ToArray(), out string como);
            // o osso que leva a lata (o de mais peso nos vértices dela)
            var pesos = malha.boneWeights;
            int osso = ordem.GroupBy(i => pesos[i].boneIndex0).OrderByDescending(g => g.Count()).First().Key;
            var lugar = Lugar(smr.bones[osso], malha.bindposes[osso] * encaixe, mats[c], fp.layer);
            lugar.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // a malha sem a lata
            var sem = Object.Instantiate(malha);
            sem.name = malha.name + "_SemItem";
            var partes = Enumerable.Range(0, malha.subMeshCount).Where(s => s != c).Select(s => malha.GetTriangles(s)).ToList();
            sem.subMeshCount = partes.Count;
            for (int s = 0; s < partes.Count; s++) sem.SetTriangles(partes[s], s);
            string caminho = $"{Malhas}/FP_Bracos_{Itens.Modelos[modelo]}_SemItem.asset";
            AssetDatabase.DeleteAsset(caminho);
            AssetDatabase.CreateAsset(sem, caminho);

            var p = fp.AddComponent<ItemNaMaoProprio>();
            p.bracos = smr;
            p.comLata = malha;
            p.semLata = sem;
            p.indiceDaLata = c;
            p.materialDaLata = mats[c];
            p.lugar = lugar;
            Debug.Log($"Item na mão ({Itens.Modelos[modelo]}, primeira pessoa): no osso {smr.bones[osso].name}, {como}.");
        }

        /// <summary>No item do corpo (Arma_Lata ou Arma_Garrafa).</summary>
        internal static void NoCorpo(GameObject arma, int modelo)
        {
            var filtro = arma.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null);
            if (filtro == null)
            {
                Debug.LogWarning($"Item na mão: {arma.name} sem malha.");
                return;
            }
            var avulsa = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDo(modelo));
            var m = filtro.sharedMesh;
            var encaixe = Encaixe(PrimeiraVez(avulsa.triangles).Select(i => avulsa.vertices[i]).ToArray(),
                                  PrimeiraVez(m.triangles).Select(i => m.vertices[i]).ToArray(), out string como);
            var lugar = Lugar(filtro.transform, encaixe, filtro.GetComponent<MeshRenderer>().sharedMaterial, arma.layer);
            var p = arma.AddComponent<ItemNaMaoProprio>();
            p.lata = filtro;
            p.malhaDaLata = m;
            p.lugar = lugar;
            Debug.Log($"Item na mão ({Itens.Modelos[modelo]}, corpo): {como}.");
        }

        /// <summary>
        /// A ponta da arma nos braços (a tocha: a chama nasce ali), um ponto novo preso ao osso: o meio dos vértices da cor
        /// dada (o UV), na parte do material dos itens. (A boca do FBX não serve: os clipes animam ela, e o lugar dela presa
        /// ao osso se perde.) Devolve o ponto (ou a boca, se não achar).
        /// </summary>
        internal static Transform BocaNaPonta(SkinnedMeshRenderer smr, Transform boca, Vector2 uvDaPonta)
        {
            var malha = smr.sharedMesh;
            var materialDosItens = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            int c = System.Array.IndexOf(smr.sharedMaterials, materialDosItens);
            if (c < 0) c = malha.subMeshCount - 1;
            var uv = malha.uv;
            var verts = malha.vertices;
            var pesos = malha.boneWeights;
            var ponta = PrimeiraVez(malha.GetTriangles(c)).Where(i => (uv[i] - uvDaPonta).sqrMagnitude < 0.0001f).ToList();
            if (ponta.Count == 0)
            {
                Debug.LogWarning($"A ponta de {smr.name} não foi achada (a cor dela no UV).");
                return boca;
            }
            var centro = Vector3.zero;
            foreach (int i in ponta) centro += verts[i];
            centro /= ponta.Count;
            int osso = ponta.GroupBy(i => pesos[i].boneIndex0).OrderByDescending(g => g.Count()).First().Key;
            var novo = new GameObject("Ponta_" + boca.name) { layer = boca.gameObject.layer }.transform;
            novo.SetParent(smr.bones[osso], false);
            novo.localPosition = malha.bindposes[osso].MultiplyPoint3x4(centro);
            novo.localRotation = Quaternion.identity;
            Debug.Log($"A ponta de {smr.name} ({ponta.Count} vértices), no osso {smr.bones[osso].name}.");
            return novo;
        }

        // o "Item na mão": filho do osso (ou da peça), na pose dada (do espaço da malha avulsa para o dele), desligado
        static GameObject Lugar(Transform pai, Matrix4x4 pose, Material material, int camada)
        {
            var go = new GameObject("Item na mão") { layer = camada };
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pose.GetColumn(3);
            go.transform.localRotation = Quaternion.LookRotation(pose.GetColumn(2), pose.GetColumn(1));
            go.transform.localScale = new Vector3(((Vector3)pose.GetColumn(0)).magnitude, ((Vector3)pose.GetColumn(1)).magnitude, ((Vector3)pose.GetColumn(2)).magnitude);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            return go;
        }

        // os vértices na ordem em que os triângulos usam pela primeira vez
        static List<int> PrimeiraVez(int[] triangulos)
        {
            var vistos = new HashSet<int>();
            var ordem = new List<int>();
            foreach (int i in triangulos)
                if (vistos.Add(i)) ordem.Add(i);
            return ordem;
        }

        /// <summary>
        /// A pose que leva os pontos da malha avulsa (<paramref name="a"/>) aos da lata (<paramref name="b"/>): pelos
        /// vértices na mesma ordem (os dois saem do mesmo objeto do Blender), se o erro for pequeno; senão, pelo eixo
        /// comprido (a lata e a garrafa são cilindros: o giro em volta dele não muda nada nelas) e pelo lado mais pesado.
        /// </summary>
        static Matrix4x4 Encaixe(Vector3[] a, Vector3[] b, out string como)
        {
            if (a.Length == b.Length && a.Length >= 3)
            {
                // três pontos bem separados dão o giro (e a escala); confere em todos
                int i0 = 0;
                int i1 = Enumerable.Range(0, a.Length).OrderByDescending(i => (a[i] - a[i0]).sqrMagnitude).First();
                var eixo = (a[i1] - a[i0]).normalized;
                int i2 = Enumerable.Range(0, a.Length).OrderByDescending(i => Vector3.Cross(eixo, a[i] - a[i0]).sqrMagnitude).First();
                float escala = (b[i1] - b[i0]).magnitude / Mathf.Max(1e-6f, (a[i1] - a[i0]).magnitude);
                var ra = Quaternion.LookRotation(a[i1] - a[i0], Vector3.Cross(a[i1] - a[i0], a[i2] - a[i0]));
                var rb = Quaternion.LookRotation(b[i1] - b[i0], Vector3.Cross(b[i1] - b[i0], b[i2] - b[i0]));
                var giro = rb * Quaternion.Inverse(ra);
                var m = Matrix4x4.TRS(b[i0] - giro * (a[i0] * escala), giro, Vector3.one * escala);
                float erro = 0f;
                for (int i = 0; i < a.Length; i++) erro = Mathf.Max(erro, (m.MultiplyPoint3x4(a[i]) - b[i]).magnitude);
                if (erro < 0.002f * Mathf.Max(1f, escala))
                {
                    como = $"encaixe exato (erro {erro * 1000f:0.###} mm, escala {escala:0.###})";
                    return m;
                }
            }
            // o eixo comprido (o maior espalhamento) e o lado mais pesado (a lata tem a borda; a garrafa, o fundo largo)
            var (ca, ea, la) = Eixo(a);
            var (cb, eb, lb) = Eixo(b);
            float s = lb / Mathf.Max(1e-6f, la);
            var r = Quaternion.FromToRotation(ea, eb);
            como = $"pelo eixo (aproximado; escala {s:0.###})";
            return Matrix4x4.TRS(cb - r * (ca * s), r, Vector3.one * s);
        }

        // o meio, o eixo comprido (para o lado onde há mais pontos longe do meio) e o comprimento nele
        static (Vector3 centro, Vector3 eixo, float comprimento) Eixo(Vector3[] p)
        {
            var c = Vector3.zero;
            foreach (var v in p) c += v;
            c /= p.Length;
            // a direção de maior espalhamento, pela iteração da potência na covariância
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var v in p)
            {
                var d = v - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            var e = new Vector3(0.3f, 0.9f, 0.2f).normalized;
            for (int k = 0; k < 60; k++)
                e = new Vector3(xx * e.x + xy * e.y + xz * e.z, xy * e.x + yy * e.y + yz * e.z, xz * e.x + yz * e.y + zz * e.z).normalized;
            float min = float.MaxValue, max = float.MinValue, terceiro = 0f;
            foreach (var v in p)
            {
                float t = Vector3.Dot(v - c, e);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
                terceiro += t * t * t;
            }
            if (terceiro < 0f) e = -e;
            return (c, e, max - min);
        }
    }
}
