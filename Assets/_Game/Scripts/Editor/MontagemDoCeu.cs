using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O céu (09/11/2026; veja o <c>Ceu.shader</c>): o material do skybox em <c>Resources/Ceu.mat</c>, que o
    /// <see cref="World.AmbienteDoDia"/> carrega no Play e põe no RenderSettings.skybox (as câmeras do mundo passam a
    /// limpar com ele). Menu Zombie City Cleaner > Mundo > Montar o céu.
    /// </summary>
    public static class MontagemDoCeu
    {
        public const string Caminho = "Assets/_Game/Resources/Ceu.mat";

        [MenuItem("Zombie City Cleaner/Mundo/Montar o céu")]
        public static void Montar()
        {
            var shader = Shader.Find("ZombieCityCleaner/Ceu");
            if (shader == null)
            {
                Debug.LogError("[Mundo] Não achei o shader ZombieCityCleaner/Ceu.");
                return;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(Caminho);
            if (m == null)
            {
                m = new Material(shader) { name = "Ceu" };
                AssetDatabase.CreateAsset(m, Caminho);
            }
            m.shader = shader;
            m.SetFloat("_Nuvens", 0.35f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mundo] O céu em {Caminho}.");
        }
    }
}
