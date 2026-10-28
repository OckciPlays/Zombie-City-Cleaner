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
    /// O Oco de teste (a pedido em 27/10/2026): o modelo do Blender (a versão otimizada com o esqueleto, ~125 mil
    /// triângulos, exportada de "D:\teste blender" com a escala do Unity) com as animações dos zumbis.
    ///  - O_Oco.fbx: Humanoid (o esqueleto tem nomes de jogo: pelvis, spine_01, upperarm_l...).
    ///  - Oco_Animacoes.fbx e Oco_AnimacoesExtra.fbx: cópias do ZombieGaroto.fbx e do _Extra, importadas como Humanoid
    ///    (o mesmo arquivo não pode ser Generic para os zumbis e Humanoid aqui); os clipes passam para o Oco pelo avatar.
    ///  - Os materiais (URP Lit) com as texturas assadas no Blender: a cor, o normal e a rugosidade (que vira a lisura no
    ///    alfa de um mapa "_MetalLiso" feito aqui).
    ///  - O prefab Prefabs/Zumbis/Resources/Oco_Teste.prefab (o F1 carrega pelo nome), com o <see cref="OcoDeTeste"/>.
    /// Menu Zombie City Cleaner > Zumbis > Montar o Oco (teste).
    /// </summary>
    public static class MontagemDoOco
    {
        const string Pasta = "Assets/_Game/Art/Characters/Oco";
        const string Modelo = Pasta + "/O_Oco.fbx";
        const string Animacoes = Pasta + "/Oco_Animacoes.fbx";
        const string AnimacoesExtra = Pasta + "/Oco_AnimacoesExtra.fbx";
        const string Texturas = Pasta + "/Texturas";
        const string Materiais = Pasta + "/Materiais";
        const string Controlador = Pasta + "/Oco.controller";
        const string PastaDoPrefab = "Assets/_Game/Prefabs/Zumbis/Resources";

        static readonly (string nome, bool loop)[] Clipes =
        {
            ("Idle", true), ("Walk", true), ("Chase", true), ("Attack", false), ("HitReact", false),
        };
        static readonly (string nome, bool loop)[] ClipesExtra =
        {
            ("Cair", false), ("Rastejar", true), ("RastejarParado", true), ("MancarE", true), ("MancarD", true),
        };
        // as partes (OCO_LP_<parte>) e se o material desenha os dois lados (a camisa tem bordas abertas da decimação)
        static readonly (string parte, bool doisLados)[] Partes =
        {
            ("Corpo", false), ("Cabeca", false), ("Camisa", true), ("Calca", true), ("Tenis_L", false), ("Tenis_R", false),
        };

        [MenuItem("Zombie City Cleaner/Zumbis/Montar o Oco (teste)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar o Oco.");
                return;
            }
            var avatar = ImportarModelo();
            if (avatar == null) return;
            var clipes = ImportarClipes(Animacoes, Clipes);
            foreach (var c in ImportarClipes(AnimacoesExtra, ClipesExtra)) clipes[c.Key] = c.Value;
            var ctrl = MontarControlador(clipes);
            var mats = MontarMateriais();
            var prefab = MontarPrefab(avatar, ctrl, mats);
            Debug.Log($"O Oco de teste montado: {AssetDatabase.GetAssetPath(prefab)} ({clipes.Count} animações). No jogo, F1 > O Oco.");
        }

        static Avatar ImportarModelo()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Modelo);
            if (imp == null)
            {
                Debug.LogWarning($"Falta o {Modelo} (o FBX do Blender, O_OCO_Game_LP_Rig_Unity.fbx).");
                return null;
            }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("O avatar Humanoid do Oco não ficou válido: confira o mapeamento dos ossos no importador do O_Oco.fbx.");
                return null;
            }
            return avatar;
        }

        static System.Collections.Generic.Dictionary<string, AnimationClip> ImportarClipes(string fbx, (string nome, bool loop)[] lista)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            var padrao = imp.defaultClipAnimations;
            imp.clipAnimations = lista.Select(c =>
            {
                var clipe = padrao.First(p => p.takeName.EndsWith("|Zombie_" + c.nome));
                clipe.name = c.nome;
                clipe.loopTime = c.loop;
                clipe.loopPose = c.loop;
                // no lugar: o giro e a altura do quadril ficam na pose (o script move o Oco)
                clipe.lockRootRotation = true;
                clipe.keepOriginalOrientation = true;
                clipe.lockRootHeightY = true;
                clipe.keepOriginalPositionY = true;
                clipe.lockRootPositionXZ = true;
                clipe.keepOriginalPositionXZ = true;
                return clipe;
            }).ToArray();
            imp.SaveAndReimport();
            // o mapeamento automático deixa o "chest" do zumbi de fora (entre o Spine e o Neck): sem ele, a curvatura do
            // tronco (o zumbi curvado) se perdia
            var hd = imp.humanDescription;
            if (hd.human != null && hd.human.Length > 0 && !hd.human.Any(h => h.humanName == "Chest"))
            {
                var ossos = hd.human.ToList();
                ossos.Add(new HumanBone { humanName = "Chest", boneName = "chest", limit = new HumanLimit { useDefaultValues = true } });
                hd.human = ossos.ToArray();
                imp.humanDescription = hd;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        }

        static AnimatorController MontarControlador(System.Collections.Generic.Dictionary<string, AnimationClip> clipes)
        {
            // o mesmo arquivo (apagar e criar de novo deixava o prefab sem o controlador): só os estados são refeitos
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var velho in sm.states.Select(s => s.state).ToArray()) sm.RemoveState(velho);
            foreach (var nome in Clipes.Concat(ClipesExtra).Select(c => c.nome))
            {
                if (!clipes.TryGetValue(nome, out var clipe)) continue;
                var estado = sm.AddState(nome);
                estado.motion = clipe;
                if (nome == "Idle") sm.defaultState = estado;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static System.Collections.Generic.Dictionary<string, Material> MontarMateriais()
        {
            Directory.CreateDirectory(Materiais);
            var mats = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var (parte, doisLados) in Partes)
            {
                string cor = $"{Texturas}/OCO_{parte}_BaseColor.png", normal = $"{Texturas}/OCO_{parte}_Normal.png";
                string rug = $"{Texturas}/OCO_{parte}_Roughness.png", liso = $"{Texturas}/OCO_{parte}_MetalLiso.png";
                Textura(cor, TextureImporterType.Default, true);
                Textura(normal, TextureImporterType.NormalMap, false);
                if (!File.Exists(liso) && File.Exists(rug)) LisuraDaRugosidade(rug, liso);
                Textura(liso, TextureImporterType.Default, false);

                string caminho = $"{Materiais}/Oco_{parte}.mat";
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
                m.SetFloat("_Cull", (float)(doisLados ? CullMode.Off : CullMode.Back));
                m.doubleSidedGI = doisLados;
                EditorUtility.SetDirty(m);
                mats[parte] = m;
            }
            AssetDatabase.SaveAssets();
            return mats;
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

        static GameObject MontarPrefab(Avatar avatar, AnimatorController ctrl, System.Collections.Generic.Dictionary<string, Material> mats)
        {
            Directory.CreateDirectory(PastaDoPrefab);
            var raiz = new GameObject("Oco_Teste");
            var cap = raiz.AddComponent<CapsuleCollider>();
            cap.radius = 0.3f;
            cap.height = 1.85f;
            cap.center = new Vector3(0f, 0.925f, 0f);
            raiz.layer = 2;   // segura o jogador; os tiros e a mira passam (como a cápsula dos zumbis de perto)

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo));
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            foreach (var r in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string parte = r.name.Replace("OCO_LP_", "");
                if (mats.TryGetValue(parte, out var m)) r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.updateWhenOffscreen = false;
            }
            var animador = modelo.GetComponent<Animator>();
            if (animador == null) animador = modelo.AddComponent<Animator>();
            animador.avatar = avatar;
            animador.runtimeAnimatorController = ctrl;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // (sem girar o modelo: com a animação Humanoid, o corpo fica de frente para o +Z do Animator sozinho; conferido
            // no Play, a ponta do pé para a frente)

            var oco = raiz.AddComponent<OcoDeTeste>();
            oco.animador = animador;
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, $"{PastaDoPrefab}/{OcoDeTeste.Prefab}.prefab");
            Object.DestroyImmediate(raiz);
            return prefab;
        }
    }
}
