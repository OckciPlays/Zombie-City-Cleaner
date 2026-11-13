using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os cervos do Blender (Blender/Fauna/familia.py, "unity": Art/Fauna/Cervo_&lt;Animal&gt;.fbx e a textura de cores
    /// Cervo_&lt;Animal&gt;_Cor.png, cada face num ponto) viram os prefabs Art/Fauna/Resources/Fauna/Cervo_&lt;Animal&gt;, que o
    /// Fauna/ModelosDosBichos carrega (sem o prefab, o veado sai de caixas). A textura sem filtro e sem mipmap (cada face
    /// pega a sua cor), um material só (URP Lit, fosco), sem o Animator (a animação é feita na hora: AnimacaoDoBicho).
    /// Menu Zombie City Cleaner > Fauna > Montar os cervos.
    /// </summary>
    public static class MontagemDaFauna
    {
        const string Pasta = "Assets/_Game/Art/Fauna";
        const string PastaDosPrefabs = Pasta + "/Resources/Fauna";
        static readonly string[] Cervos = { "Cervo_Macho", "Cervo_Femea", "Cervo_Filhote" };

        [MenuItem("Zombie City Cleaner/Fauna/Montar os cervos")]
        public static void Montar()
        {
            if (!AssetDatabase.IsValidFolder(Pasta + "/Resources")) AssetDatabase.CreateFolder(Pasta, "Resources");
            if (!AssetDatabase.IsValidFolder(PastaDosPrefabs)) AssetDatabase.CreateFolder(Pasta + "/Resources", "Fauna");
            int feitos = 0;
            foreach (var nome in Cervos)
            {
                string fbx = $"{Pasta}/{nome}.fbx", png = $"{Pasta}/{nome}_Cor.png";
                if (AssetImporter.GetAtPath(fbx) is not ModelImporter mi || AssetImporter.GetAtPath(png) is not TextureImporter ti)
                {
                    Debug.LogWarning($"MontagemDaFauna: falta {fbx} ou {png} (rode o Blender/Fauna/familia.py -- unity).");
                    continue;
                }
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.SaveAndReimport();
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = false;
                mi.isReadable = false;
                mi.SaveAndReimport();

                var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
                string caminhoDoMaterial = $"{Pasta}/{nome}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(caminhoDoMaterial);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, caminhoDoMaterial);
                }
                mat.SetTexture("_BaseMap", textura);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.1f);
                mat.SetFloat("_Metallic", 0f);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);

                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var inst = (GameObject)Object.Instantiate(modelo);
                inst.name = nome;
                var animador = inst.GetComponent<Animator>();
                if (animador != null) Object.DestroyImmediate(animador);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = false;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, $"{PastaDosPrefabs}/{nome}.prefab");
                Object.DestroyImmediate(inst);
                feitos++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"MontagemDaFauna: {feitos} cervos montados em {PastaDosPrefabs}.");
        }
    }
}
