using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O saque e a fabricação (31/10/2026), na montagem: as malhas das construções montadas (a fogueira, a cama e a
    /// barricada, e a bancada nova; Construcao_&lt;nome&gt;, das peças do Loot.fbx) e as bancadas da cena (o móvel
    /// "Bancada" do bunker, embaixo do painel de armas, vira uma bancada de trabalho: o colisor e o <see cref="Bancada"/>).
    /// Chamada pela montagem das armas e pela dos itens no mundo.
    /// </summary>
    public static class MontagemDoSaque
    {
        const string Malhas = "Assets/_Game/Art/Characters/Jogador/Malhas";

        /// <summary>As construções do loot.py que não vão na mochila (as montadas no mundo e a bancada).</summary>
        public static readonly string[] Montadas = { "FogueiraMontada", "CamaMontada", "BarricadaMontada", "Bancada" };

        public static string MalhaDaMontada(string nome) => $"{Malhas}/Construcao_{nome}.asset";

        internal static void JuntarMontadas()
        {
            if (!System.IO.File.Exists(MontagemDasArmas.FbxLoot)) return;
            var nomes = AssetDatabase.LoadAllAssetsAtPath(MontagemDasArmas.FbxLoot).OfType<Mesh>().Select(m => m.name).ToList();
            foreach (var nome in Montadas)
                if (nomes.Any(n => n.StartsWith(nome + "_"))) MontagemDasArmas.Juntar(MontagemDasArmas.FbxLoot, nome + "_", MalhaDaMontada(nome));
        }

        /// <summary>
        /// As construções montadas pelos jogadores (a fogueira, a cama e a barricada): o "Construções no mundo" da cena (um
        /// NetworkObject com o <see cref="ConstrucoesNoMundo"/>, as malhas e os materiais), refeito a cada montagem.
        /// </summary>
        public static void MontarConstrucoes(UnityEngine.SceneManagement.Scene cena)
        {
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Construções no mundo");
            if (velho != null) Object.DestroyImmediate(velho);
            var go = new GameObject("Construções no mundo");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, cena);
            go.AddComponent<NetworkObject>();
            var c = go.AddComponent<ConstrucoesNoMundo>();
            c.malhas = new[] { "FogueiraMontada", "CamaMontada", "BarricadaMontada" }.Select(n => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDaMontada(n))).ToArray();
            c.material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            c.materialDoFogo = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Clarao.mat");
            if (c.malhas.Any(m => m == null)) Debug.LogWarning("Construções no mundo: faltam malhas (rode o Blender/loot.py e a montagem das armas).");
        }

        /// <summary>
        /// Os móveis "Bancada" da cena viram bancadas de trabalho (E abre a aba Criar; as receitas de bancada valem perto):
        /// um colisor da caixa do móvel e o componente. Pode rodar de novo (o que já tem fica).
        /// </summary>
        public static void BancadasNaCena(UnityEngine.SceneManagement.Scene cena)
        {
            int n = 0;
            foreach (var t in cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Bancada"))
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (t.GetComponent<Collider>() == null)
                {
                    var col = t.gameObject.AddComponent<BoxCollider>();
                    if (t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                    {
                        col.center = mf.sharedMesh.bounds.center;
                        col.size = mf.sharedMesh.bounds.size;
                    }
                }
                if (t.GetComponent<Bancada>() == null) t.gameObject.AddComponent<Bancada>();
                EditorUtility.SetDirty(t.gameObject);
                n++;
            }
            Debug.Log($"Bancadas de trabalho na cena {cena.name}: {n}.");
        }
    }
}
