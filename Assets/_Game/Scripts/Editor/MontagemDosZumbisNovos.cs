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
    /// Os zumbis novos para teste (a pedido em 31/10/2026), feitos no Blender em "D:\teste blender\O &lt;nome&gt;" (fora do
    /// repositório), ~5 mil triângulos, visual low-poly facetado (sombreamento plano; textura só de cor e rugosidade):
    ///  - O Parrudo: o tanque; o único olho, nas costas, é o ponto fraco (<see cref="OlhoDoBrutamonte"/>, que era o OlhoDoParrudo: abre, olha e fecha).
    /// Cada um: Art/Characters/&lt;Nome&gt;/&lt;Nome&gt;.fbx (Humanoid, o mesmo esqueleto de nomes do Oco, então as animações
    /// dos zumbis servem pelo Oco.controller), Texturas/ e Materiais/, e o prefab Prefabs/Zumbis/Resources/&lt;Nome&gt;_Teste,
    /// com o <see cref="OcoDeTeste"/> (o F1 põe pelo nome; mesmas regras: só nesta máquina, sem dano).
    /// Menu Zombie City Cleaner > Zumbis > Montar os zumbis novos (teste).
    /// </summary>
    public static class MontagemDosZumbisNovos
    {
        const string Controlador = "Assets/_Game/Art/Characters/Oco/Oco.controller";
        const string PastaDoPrefab = "Assets/_Game/Prefabs/Zumbis/Resources";

        // nome, altura da cápsula, raio da cápsula
        static readonly (string nome, float altura, float raio)[] Zumbis =
        {
            ("Parrudo", 2.25f, 0.45f),
        };

        [MenuItem("Zombie City Cleaner/Zumbis/Montar os zumbis novos (teste)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os zumbis novos.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador) == null) MontagemDoOco.Montar();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null)
            {
                Debug.LogError("Falta o Oco.controller (as animações dos zumbis para o Humanoid).");
                return;
            }
            int feitos = 0;
            foreach (var (nome, altura, raio) in Zumbis)
            {
                string pasta = $"Assets/_Game/Art/Characters/{nome}";
                string modelo = $"{pasta}/{nome}.fbx";
                if (!File.Exists(modelo))
                {
                    Debug.LogWarning($"Zumbi novo sem modelo: {modelo}");
                    continue;
                }
                var avatar = ImportarModelo(modelo);
                if (avatar == null) continue;
                var mat = MontarMaterial(pasta, nome);
                MontarPrefab(nome, modelo, avatar, ctrl, mat, altura, raio);
                feitos++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Zumbis novos montados: {feitos} de {Zumbis.Length}. No jogo, F1 > O Oco > escolha a versão.");
        }

        static Avatar ImportarModelo(string caminho)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importNormals = ModelImporterNormals.Import;      // o sombreamento plano vem do Blender
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

        static Material MontarMaterial(string pasta, string nome)
        {
            string tex = $"{pasta}/Texturas/{nome}";
            string cor = tex + "_BaseColor.png", rug = tex + "_Roughness.png", liso = tex + "_MetalLiso.png";
            Textura(cor, true);
            if (File.Exists(rug) && (!File.Exists(liso) || File.GetLastWriteTimeUtc(rug) > File.GetLastWriteTimeUtc(liso)))
                LisuraDaRugosidade(rug, liso);
            Textura(liso, false);
            Directory.CreateDirectory($"{pasta}/Materiais");
            string caminho = $"{pasta}/Materiais/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor));
            m.SetColor("_BaseColor", Color.white);
            var mapa = AssetDatabase.LoadAssetAtPath<Texture2D>(liso);
            if (mapa != null)
            {
                m.SetTexture("_MetallicGlossMap", mapa);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Cull", (float)CullMode.Off);   // bordas abertas onde o corpo some debaixo da roupa
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Textura(string caminho, bool srgb)
        {
            var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (ti == null) return;
            if (ti.textureType == TextureImporterType.Default && ti.sRGBTexture == srgb && ti.maxTextureSize == 2048) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        // o mapa de metal e lisura do URP: R = 0 (nada de metal), A = 1 - rugosidade
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

        static Transform Achar(Transform raiz, string nome) =>
            raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nome);

        static void MontarPrefab(string nome, string modeloFbx, Avatar avatar, AnimatorController ctrl, Material mat, float altura, float raio)
        {
            Directory.CreateDirectory(PastaDoPrefab);
            string nomeDoPrefab = nome + "_Teste";
            var raiz = new GameObject(nomeDoPrefab);
            var cap = raiz.AddComponent<CapsuleCollider>();
            cap.radius = raio;
            cap.height = altura;
            cap.center = new Vector3(0f, altura * 0.5f, 0f);
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

            if (nome == "Parrudo") MontarOlho(raiz, modelo.transform, oco);

            string caminho = $"{PastaDoPrefab}/{nomeDoPrefab}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        }

        // o olho das costas: os ossos do Blender (olho, palpebra_sup, palpebra_inf) e o colisor do ponto fraco no globo
        static void MontarOlho(GameObject raiz, Transform modelo, OcoDeTeste oco)
        {
            var olho = raiz.AddComponent<OlhoDoBrutamonte>();
            olho.olho = Achar(modelo, "olho");
            olho.palpebraSup = Achar(modelo, "palpebra_sup");
            olho.palpebraInf = Achar(modelo, "palpebra_inf");
            if (olho.olho == null || olho.palpebraSup == null || olho.palpebraInf == null)
            {
                Debug.LogError("O Parrudo veio sem os ossos do olho (olho, palpebra_sup, palpebra_inf).");
                return;
            }
            // um filho novo (o osso é dirigido pelo modelo; o colisor fica num transform nosso)
            var pf = new GameObject("PontoFraco");
            pf.layer = 2;
            pf.transform.SetParent(olho.olho, false);
            var esfera = pf.AddComponent<SphereCollider>();
            esfera.radius = 0.085f;
            esfera.isTrigger = true;
            olho.pontoFraco = esfera;
            // tanque: anda mais devagar que o Oco (as mesmas animações, um pouco mais lentas)
            oco.velocidadeCorrendo = 0.9f;
            oco.velocidadeAndando = 0.45f;
        }
    }
}
