using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As versões do Oco para teste (a pedido em 28/10/2026): o original e as 11 "pessoas" feitas no Blender
    /// (pasta "D:\teste blender\O Oco", fora do repositório), cada uma com ~5 mil triângulos, 1 malha (OCO_Jogo), 1 material
    /// com 3 texturas 2048 e o mesmo esqueleto de 53 ossos (o do Oco de teste antigo, de ~125 mil, que saiu em 28/10/2026).
    ///  - Art/Characters/Oco/Variantes/&lt;versão&gt;/O_Oco_&lt;versão&gt;.fbx (Humanoid) e Texturas/ (cor, normal, rugosidade).
    ///  - Materiais URP Lit (Materiais/Oco_&lt;versão&gt;.mat), com os dois lados (a malha tem bordas abertas onde o corpo some
    ///    debaixo da roupa).
    ///  - Prefabs/Zumbis/Resources/Oco_&lt;versão&gt;.prefab, com o <see cref="OcoDeTeste"/> e o mesmo Oco.controller
    ///    (as animações dos zumbis passam pelo avatar de cada um).
    /// Menu Zombie City Cleaner > Zumbis > Montar as versões do Oco (teste).
    /// </summary>
    public static class MontagemDasVariantesDoOco
    {
        const string Pasta = "Assets/_Game/Art/Characters/Oco/Variantes";
        const string Controlador = "Assets/_Game/Art/Characters/Oco/Oco.controller";
        const string PastaDoPrefab = "Assets/_Game/Prefabs/Zumbis/Resources";

        [MenuItem("Zombie City Cleaner/Zumbis/Montar as versões do Oco (teste)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as versões do Oco.");
                return;
            }
            // o controlador (e as animações) vêm da montagem das animações do Oco
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador) == null) MontagemDoOco.Montar();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null)
            {
                Debug.LogError("Falta o Oco.controller: rode antes Zombie City Cleaner > Zumbis > Montar as animações do Oco (teste).");
                return;
            }
            int feitas = 0;
            foreach (var (prefab, nome) in OcoDeTeste.Versoes)
            {
                string versao = prefab.Substring("Oco_".Length);    // ex.: 01_O_Observador
                string pasta = $"{Pasta}/{versao}";
                string modelo = $"{pasta}/O_Oco_{versao}.fbx";
                if (!File.Exists(modelo))
                {
                    Debug.LogWarning($"Versão do Oco sem modelo: {modelo}");
                    continue;
                }
                var avatar = ImportarModelo(modelo);
                if (avatar == null) continue;
                var mat = MontarMaterial(pasta, versao);
                MontarPrefab(prefab, modelo, avatar, ctrl, mat);
                feitas++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Versões do Oco montadas: {feitas} de {OcoDeTeste.Versoes.Length}. No jogo, F1 > O Oco > escolha a versão.");
        }

        static Avatar ImportarModelo(string caminho)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"O avatar Humanoid não ficou válido: {caminho}");
                return null;
            }
            return avatar;
        }

        static Material MontarMaterial(string pasta, string versao)
        {
            string tex = $"{pasta}/Texturas/Oco_{versao}";
            string cor = tex + "_BaseColor.png", normal = tex + "_Normal.png", rug = tex + "_Roughness.png", liso = tex + "_MetalLiso.png";
            Textura(cor, TextureImporterType.Default, true);
            Textura(normal, TextureImporterType.NormalMap, false);
            // refeita também quando a rugosidade é mais nova (a versão foi assada de novo no Blender)
            if (File.Exists(rug) && (!File.Exists(liso) || File.GetLastWriteTimeUtc(rug) > File.GetLastWriteTimeUtc(liso)))
                LisuraDaRugosidade(rug, liso);
            Textura(liso, TextureImporterType.Default, false);

            Directory.CreateDirectory($"{pasta}/Materiais");
            string caminho = $"{pasta}/Materiais/Oco_{versao}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            var mapa = AssetDatabase.LoadAssetAtPath<Texture2D>(liso);
            if (mapa != null)
            {
                m.SetTexture("_MetallicGlossMap", mapa);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 1f);   // com o mapa, a lisura é o alfa dele vezes este valor
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Textura(string caminho, TextureImporterType tipo, bool srgb)
        {
            var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (ti == null) return;
            if (ti.textureType == tipo && ti.sRGBTexture == srgb && ti.maxTextureSize == 2048) return;
            ti.textureType = tipo;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        // o mapa de metal e lisura do URP: R = 0 (nada de metal), A = 1 - rugosidade (como no Oco de teste)
        static void LisuraDaRugosidade(string rugosidade, string saida)
        {
            var origem = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            origem.LoadImage(File.ReadAllBytes(rugosidade));
            var px = origem.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, (byte)(255 - px[i].r));
            var mapa = new Texture2D(origem.width, origem.height, TextureFormat.RGBA32, false, true);
            mapa.SetPixels32(px);
            File.WriteAllBytes(saida, mapa.EncodeToPNG());
            Object.DestroyImmediate(origem);
            Object.DestroyImmediate(mapa);
            AssetDatabase.ImportAsset(saida);
        }

        static void MontarPrefab(string nomeDoPrefab, string modeloFbx, Avatar avatar, AnimatorController ctrl, Material mat)
        {
            Directory.CreateDirectory(PastaDoPrefab);
            var raiz = new GameObject(nomeDoPrefab);
            var cap = raiz.AddComponent<CapsuleCollider>();
            cap.radius = 0.3f;
            cap.height = 1.85f;
            cap.center = new Vector3(0f, 0.925f, 0f);
            raiz.layer = 2;   // segura o jogador; os tiros e a mira passam (como o Oco de teste)

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modeloFbx));
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            foreach (var r in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.updateWhenOffscreen = false;
            }
            var animador = modelo.GetComponent<Animator>();
            if (animador == null) animador = modelo.AddComponent<Animator>();
            animador.avatar = avatar;
            animador.runtimeAnimatorController = ctrl;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var oco = raiz.AddComponent<OcoDeTeste>();
            oco.animador = animador;
            string caminho = $"{PastaDoPrefab}/{nomeDoPrefab}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
            // (o controlador não é recriado aqui, mas a reimportação garante o prefab com ele; veja "Controlador apagado e
            // recriado" no CLAUDE.md)
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        }
    }
}
