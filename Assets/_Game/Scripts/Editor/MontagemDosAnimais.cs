using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Fauna;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// (14/11/2026) Os animais do Tripo do Gustavo (Blender/Fauna/Animais/Animais.blend, exportados pelo
    /// exportar_unity.py para Art/Fauna/Tripo: &lt;Nome&gt;.fbx, &lt;Nome&gt;_Cor.jpg e o Medidas.json) viram os prefabs
    /// Art/Fauna/Resources/Fauna/Tripo/&lt;Nome&gt;, que o Fauna/ModelosDosBichos carrega (sem o prefab, o bicho sai de
    /// caixas). Um material por bicho (URP Lit, fosco, a textura do Tripo com filtro e mipmap, no máximo 1024), sem o
    /// Animator (a animação ainda é a do código: AnimacaoDoBicho; as ações do Gustavo estão nos FBX como takes, com a
    /// importação de animação desligada até a fase 2) e com as medidas (MedidasDoModelo: os colisores, a boca, a altura).
    /// Menu Zombie City Cleaner > Fauna > Montar os animais do Tripo.
    /// </summary>
    public static class MontagemDosAnimais
    {
        const string Pasta = "Assets/_Game/Art/Fauna/Tripo";
        const string PastaDosPrefabs = "Assets/_Game/Art/Fauna/Resources/Fauna/Tripo";

        [System.Serializable]
        class Osso
        {
            public string nome;
            public float[] pos;
        }

        [System.Serializable]
        class Medida
        {
            public string nome, grupo;
            public float[] corpoCentro, corpoTamanho, cabecaCentro, cabecaTamanho, boca;
            public float altura, comprimento, desvioDoRepouso;
            public int triangulos;
            public Osso[] ossos;
            public string[] acoes;
        }

        [System.Serializable]
        class Lista
        {
            public Medida[] bichos;
        }

        static Vector3 V(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        [MenuItem("Zombie City Cleaner/Fauna/Montar os animais do Tripo")]
        public static void Montar()
        {
            string arq = Pasta + "/Medidas.json";
            if (!File.Exists(arq))
            {
                Debug.LogWarning($"MontagemDosAnimais: falta {arq} (rode o Blender/Fauna/Animais/exportar_unity.py).");
                return;
            }
            var lista = JsonUtility.FromJson<Lista>(File.ReadAllText(arq));
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art/Fauna/Resources/Fauna/Tripo"))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Fauna/Resources/Fauna", "Tripo");
            int feitos = 0;
            float pior = 0f;
            string doPior = "";
            var relato = new System.Text.StringBuilder();
            foreach (var med in lista.bichos)
            {
                string nome = med.nome, fbx = $"{Pasta}/{nome}.fbx", jpg = $"{Pasta}/{nome}_Cor.jpg";
                if (AssetImporter.GetAtPath(fbx) is not ModelImporter mi || AssetImporter.GetAtPath(jpg) is not TextureImporter ti)
                {
                    Debug.LogWarning($"MontagemDosAnimais: falta {fbx} ou {jpg}.");
                    continue;
                }
                // a textura: com filtro e mipmap (a pintura do Tripo), comprimida, no máximo 1024
                if (ti.textureType != TextureImporterType.Default || !ti.sRGBTexture || ti.filterMode != FilterMode.Bilinear || !ti.mipmapEnabled
                    || ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 1024 || ti.textureCompression != TextureImporterCompression.Compressed
                    || ti.alphaSource != TextureImporterAlphaSource.None)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.mipmapEnabled = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 1024;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.alphaSource = TextureImporterAlphaSource.None;
                    ti.SaveAndReimport();
                }
                // o modelo: sem os materiais do FBX, sem câmera e luz, a animação genérica e (por enquanto) sem os clipes
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importNormals != ModelImporterNormals.Import || mi.importBlendShapes
                    || mi.importCameras || mi.importLights || mi.animationType != ModelImporterAnimationType.Generic || mi.importAnimation || mi.isReadable)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importBlendShapes = false;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.importAnimation = false;
                    mi.isReadable = false;
                    mi.SaveAndReimport();
                }

                var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(jpg);
                string caminhoDoMaterial = $"{Pasta}/{nome}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(caminhoDoMaterial);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, caminhoDoMaterial);
                }
                mat.SetTexture("_BaseMap", textura);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Metallic", 0f);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);

                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var inst = (GameObject)Object.Instantiate(modelo);
                inst.name = nome;
                var animador = inst.GetComponent<Animator>();
                if (animador != null) Object.DestroyImmediate(animador);
                int malhas = 0;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = false;
                    malhas++;
                }
                var m = inst.AddComponent<MedidasDoModelo>();
                m.corpoCentro = V(med.corpoCentro);
                m.corpoTamanho = V(med.corpoTamanho);
                m.cabecaCentro = V(med.cabecaCentro);
                m.cabecaTamanho = V(med.cabecaTamanho);
                m.boca = V(med.boca);
                m.altura = med.altura;
                m.comprimento = med.comprimento;

                // conferir: os ossos no lugar do Blender (a frente em +z, o lado L à esquerda: x < 0)
                float desvio = 0f;
                string ladoErrado = "";
                var todos = inst.GetComponentsInChildren<Transform>(true);
                if (med.ossos != null)
                    foreach (var o in med.ossos)
                    {
                        Transform t = null;
                        foreach (var c in todos) if (c.name == o.nome) { t = c; break; }
                        if (t == null) { ladoErrado += $" falta {o.nome};"; continue; }
                        Vector3 aqui = inst.transform.InverseTransformPoint(t.position);
                        desvio = Mathf.Max(desvio, Vector3.Distance(aqui, V(o.pos)));
                        if (o.nome.EndsWith(".L") && aqui.x > 0.005f) ladoErrado += $" {o.nome} à direita;";
                    }
                if (desvio > pior) { pior = desvio; doPior = nome; }
                relato.Append($"\n  {nome}: {med.triangulos} triângulos, {malhas} malha(s), {med.acoes?.Length ?? 0} ações, ossos a {desvio * 100f:0.0} cm do Blender{ladoErrado}");

                PrefabUtility.SaveAsPrefabAsset(inst, $"{PastaDosPrefabs}/{nome}.prefab");
                Object.DestroyImmediate(inst);
                feitos++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"MontagemDosAnimais: {feitos} de {lista.bichos.Length} animais montados em {PastaDosPrefabs}; o maior desvio dos ossos: {pior * 100f:0.0} cm ({doPior}).{relato}");
        }
    }
}
