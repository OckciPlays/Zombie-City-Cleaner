using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A perua nova, versão 2 (leve, 29/10/2026): o mesmo visual da v1 (<see cref="VisualDaPerua"/>) com ~9.700 triângulos
    /// em vez de ~31.700 (os detalhes miúdos foram projetados nas texturas) e o pneu facetado de 18 lados, como o antigo.
    /// O FBX é o `Art/Veiculos/PeruaNova2/PeruaNova2.fbx` (feito pelo `Blender/PeruaNova2/perua_nova2.py`).
    /// Como a v1: troca só a malha e os materiais das 12 peças visuais do prefab do carro e os índices das lentes, e não
    /// salva se a impressão do prefab (<see cref="VisualDaPerua.Impressao"/>) mudar.
    /// Antes de aplicar, guarda o prefab como está (com a v1) em `Backups/Perua_v1_antes_da_v2_2026-10-29/`.
    /// Menus: Zombie City Cleaner > Veículos > Aplicar o visual novo da perua (v2, leve) / Voltar para a v1 da perua.
    /// A "Montar veículos" volta o prefab ao Carro.fbx antigo: depois dela, rode o "Aplicar" de novo.
    /// </summary>
    public static class VisualDaPerua2
    {
        const string Pasta = "Assets/_Game/Art/Veiculos/PeruaNova2";
        const string Fbx = Pasta + "/PeruaNova2.fbx";
        const string PastaTexturas = Pasta + "/Texturas";
        const string PastaMateriais = Pasta + "/Materiais";
        const string Prefab = "Assets/_Game/Prefabs/Veiculos/Carro.prefab";
        const string Backup = "Backups/Perua_v1_antes_da_v2_2026-10-29";

        static readonly (string objeto, string peca)[] Pecas =
        {
            ("Carroceria", "Perua2_Carroceria"), ("Capô", "Perua2_Capo"), ("Tampa traseira", "Perua2_Tampa"), ("Volante", "Perua2_Volante"),
            ("Porta DE", "Perua2_Porta_DE"), ("Porta DD", "Perua2_Porta_DD"), ("Porta TE", "Perua2_Porta_TE"), ("Porta TD", "Perua2_Porta_TD"),
            ("Roda DE (modelo)", "Perua2_Roda"), ("Roda DD (modelo)", "Perua2_Roda"), ("Roda TE (modelo)", "Perua2_Roda"), ("Roda TD (modelo)", "Perua2_Roda"),
        };

        [MenuItem("Zombie City Cleaner/Veículos/Aplicar o visual novo da perua (v2, leve)")]
        public static void Aplicar()
        {
            if (!File.Exists(Fbx)) throw new FileNotFoundException("Rode o Blender/PeruaNova2/rodar.py (compor_exportar) antes.", Fbx);
            if (!Directory.Exists(Backup))
            {
                Directory.CreateDirectory(Backup + "/Prefabs/Veiculos");
                File.Copy(Prefab, Backup + "/Prefabs/Veiculos/Carro.prefab");
                File.Copy(Prefab + ".meta", Backup + "/Prefabs/Veiculos/Carro.prefab.meta");
                Debug.Log($"Backup do prefab do carro (com a v1) em {Backup}.");
            }
            foreach (var atlas in new[] { "Perua2_Lataria", "Perua2_Interior" })
            {
                Textura($"{PastaTexturas}/{atlas}_Cor.png", true, false);
                Textura($"{PastaTexturas}/{atlas}_MS.png", false, false);
                Textura($"{PastaTexturas}/{atlas}_Normal.png", false, true);
            }
            var lataria = Pbr("Perua2_Lataria", "Perua2_Lataria", 0.85f);
            var interior = Pbr("Perua2_Interior", "Perua2_Interior", 1f);
            var lanterna = Pbr("Perua2_Lanterna", "Perua2_Lataria", 0.5f);
            var vidro = Transparente("Perua2_Vidro", new Color(0.11f, 0.14f, 0.16f, 0.34f), 0.93f, null);
            var farol = Transparente("Perua2_Farol", new Color(0.92f, 0.94f, 0.96f, 0.14f), 0.96f,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/Perua2_Lataria_Normal.png"));
            AssetDatabase.SaveAssets();

            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var (nome, m) in new[] { ("Perua2_Lataria", lataria), ("Perua2_Interior", interior), ("Perua2_Vidro", vidro), ("Perua2_Farol", farol), ("Perua2_Lanterna", lanterna) })
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nome), m);
            imp.SaveAndReimport();

            var raiz = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                string antes = VisualDaPerua.Impressao(raiz);
                var carro = raiz.GetComponent<Carro>();
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
                foreach (var (objeto, peca) in Pecas)
                {
                    var t = raiz.transform.Cast<Transform>().FirstOrDefault(x => x.name == objeto && x.GetComponent<MeshFilter>() != null);
                    var p = fbx.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.name == peca);
                    if (t == null || p == null) throw new System.Exception($"Falta a peça {objeto} no prefab ou {peca} no FBX.");
                    t.GetComponent<MeshFilter>().sharedMesh = p.sharedMesh;
                    t.GetComponent<MeshRenderer>().sharedMaterials = p.GetComponent<MeshRenderer>().sharedMaterials;
                }
                var mats = carro.carroceria.sharedMaterials;
                carro.submeshFarol = System.Array.IndexOf(mats, farol);
                carro.submeshLanterna = System.Array.IndexOf(mats, lanterna);
                if (carro.submeshFarol < 0 || carro.submeshLanterna < 0) throw new System.Exception("Sem os submeshes das lentes na Perua2_Carroceria.");
                string depois = VisualDaPerua.Impressao(raiz);
                if (antes != depois)
                {
                    Debug.LogError("O visual v2 mudaria algo além da malha e dos materiais; o prefab NÃO foi salvo.");
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
                Debug.Log($"Visual v2 (leve) da perua aplicado (farol no submesh {carro.submeshFarol}, lanterna no {carro.submeshLanterna}); física e rede iguais.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        [MenuItem("Zombie City Cleaner/Veículos/Voltar para a v1 da perua")]
        public static void VoltarParaV1()
        {
            string copia = Backup + "/Prefabs/Veiculos/Carro.prefab";
            if (!File.Exists(copia)) { Debug.LogError("Sem o backup da v1 em " + Backup); return; }
            File.Copy(copia, Prefab, true);
            AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate);
            Debug.Log("O prefab do carro voltou à v1 da perua.");
        }

        static void Textura(string caminho, bool srgb, bool normal)
        {
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura da perua v2 não encontrada.", caminho);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static Material Novo(string nome)
        {
            Directory.CreateDirectory(PastaMateriais);
            string caminho = $"{PastaMateriais}/{nome}.mat";
            var s = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, caminho); }
            m.shader = s;
            m.shaderKeywords = new string[0];
            m.enableInstancing = true;
            return m;
        }

        static Material Pbr(string nome, string atlas, float oclusao)
        {
            var m = Novo(nome);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Cor.png"));
            m.SetColor("_BaseColor", Color.white);
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_MS.png");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Normal.png"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ms);
            m.SetFloat("_OcclusionStrength", oclusao);
            m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Transparente(string nome, Color cor, float suavidade, Texture normal)
        {
            var m = Novo(nome);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", suavidade);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
