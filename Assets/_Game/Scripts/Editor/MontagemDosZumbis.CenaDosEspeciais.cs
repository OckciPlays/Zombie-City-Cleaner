using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena de teste dos zumbis especiais (03/11/2026, a pedido: "uma cena de testes para a gente testar os zumbis especiais,
    /// começando pelo brutamonte ... montar a cena de comparação com os dois lado a lado"): Scenes/Exemplos/ZumbisEspeciais.unity,
    /// com o Brutamonte de hoje (o Brutamonte_V1) e o novo (o Brutamonte_Novo, do Blender/Especiais/brutamonte_novo.py) lado a
    /// lado, cada um com o esqueleto, o material e as animações do jogo: o novo entra como os especiais (<see cref="ImportarOco"/>:
    /// o avatar Humanoid e os clipes dos zumbis gravados no esqueleto dele, em Oco/Clipes/Brutamonte_Novo), mas fica fora da
    /// lista do ZombieManager (<see cref="EspeciaisDeTeste"/>). Um controlador por modelo (um estado por animação) e o
    /// <see cref="MostruarioDosEspeciais"/> (as teclas no Play).
    /// <para>As animações próprias (03/11/2026, a pedido: "refaça as animações dele, porque tem umas bem estranhas; ainda
    /// mantenha somente na cena de comparação"): um modelo com o FBX &lt;modelo&gt;_Animacoes.fbx na pasta dos especiais (o
    /// Brutamonte novo: Blender/Especiais/brutamonte_novo_animacoes.py, feitas no esqueleto dele) toca as dele, importadas como
    /// Generic (os mesmos ossos do modelo), no lugar das gravadas do Oco (<see cref="AnimacoesProprias"/>). As que ele não tem
    /// saem da cena (as setas só passam pelas que os dois têm).</para>
    /// <para>Menu Zombie City Cleaner > Exemplos > Zumbis especiais (comparar). Refaz a cena.</para>
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string CenaDosEspeciais = "Assets/_Game/Scenes/Exemplos/ZumbisEspeciais.unity";
        const string PastaDaCenaDosEspeciais = "Assets/_Game/Scenes/Exemplos/ZumbisEspeciais";

        /// <summary>Os modelos da cena, da esquerda para a direita, e o nome embaixo de cada um.</summary>
        static readonly (string modelo, string nome)[] ModelosDaCenaDosEspeciais =
        {
            ("Brutamonte_V1", "Brutamonte de hoje"), ("Brutamonte_Novo", "Brutamonte novo"),
        };

        /// <summary>As animações, na ordem das setas (as do Brutamonte primeiro); só as que os dois têm.</summary>
        static readonly string[] AnimacoesDaCenaDosEspeciais =
        {
            "Idle", "Walk", "Chase", "Correr", "Attack", "HitReact", "Emergir", "Furia", "Gritar", "Cair", "Rastejar",
            "RastejarParado", "MancarE", "MancarD", "LevantarE", "LevantarD", "AgoniaRosto", "AgoniaPele", "AgoniaEncolher", "Cuspir",
        };

        [MenuItem("Zombie City Cleaner/Exemplos/Zumbis especiais (comparar)")]
        public static void MontarACenaDosEspeciais()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena dos zumbis especiais.");
                return;
            }
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("A cena aberta tem mudanças não salvas: salve ou descarte antes de montar a cena dos zumbis especiais.");
                return;
            }
            // os de teste ainda sem os clipes gravados: como os especiais do jogo (o avatar e os clipes no esqueleto deles)
            foreach (var (nome, _) in EspeciaisDeTeste) ImportarOco(nome, -1);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(PastaDaCenaDosEspeciais);

            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.38f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
            RenderSettings.fog = false;

            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.3f;
            sol.color = new Color(1f, 0.96f, 0.9f);
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(42f, -150f, 0f);
            var contra = new GameObject("Luz de trás").AddComponent<Light>();
            contra.type = LightType.Directional;
            contra.intensity = 0.45f;
            contra.color = new Color(0.75f, 0.82f, 1f);
            contra.shadows = LightShadows.None;
            contra.transform.rotation = Quaternion.Euler(25f, 30f, 0f);

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chão";
            chao.transform.localScale = new Vector3(6f, 1f, 6f);
            chao.GetComponent<MeshRenderer>().sharedMaterial = MaterialDoChaoDosEspeciais();

            var camGo = new GameObject("Câmera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.15f);
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2.1f, -6.5f), Quaternion.Euler(8f, 0f, 0f));

            var animadores = new List<Animator>();
            var nomes = new List<string>();
            var comuns = new HashSet<string>(AnimacoesDaCenaDosEspeciais);
            int n = ModelosDaCenaDosEspeciais.Length;
            for (int i = 0; i < n; i++)
            {
                var (modelo, nome) = ModelosDaCenaDosEspeciais[i];
                var proprias = AnimacoesProprias(modelo);
                var clipes = proprias ?? ClipesGravados(modelo);
                if (proprias != null) nome += ", animações próprias";
                comuns.IntersectWith(clipes.Keys);
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{ClipesDosOcos}/{modelo}/{modelo}_Generico.asset");
                if (avatar == null) throw new FileNotFoundException("Falta o avatar do modelo (monte os zumbis especiais)", modelo);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(FbxDe(modelo)));
                go.name = modelo;
                go.transform.SetPositionAndRotation(new Vector3((i - (n - 1) * 0.5f) * 3.4f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                var an = go.GetComponent<Animator>();
                if (an == null) an = go.AddComponent<Animator>();
                an.avatar = avatar;
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                an.runtimeAnimatorController = ControladorDaCenaDosEspeciais(modelo, clipes);
                var mat = MaterialDoOco(modelo);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, smr.sharedMaterials.Length)).ToArray();
                    smr.updateWhenOffscreen = true;
                }
                animadores.Add(an);
                nomes.Add($"{nome} ({Altura(go):0.00} m)");
            }
            var mostruario = new GameObject("Mostruário dos especiais").AddComponent<MostruarioDosEspeciais>();
            mostruario.modelos = animadores.ToArray();
            mostruario.nomes = nomes.ToArray();
            mostruario.animacoes = AnimacoesDaCenaDosEspeciais.Where(comuns.Contains).ToArray();
            mostruario.cameraDaCena = cam;
            EditorSceneManager.SaveScene(cena, CenaDosEspeciais);
            Debug.Log($"[Especiais] A cena {CenaDosEspeciais}: {string.Join(", ", nomes)}; {mostruario.animacoes.Length} animações.");
        }

        /// <summary>As animações que são laço (as outras tocam uma vez e param no último quadro).</summary>
        static readonly HashSet<string> LacosDasAnimacoesProprias = new HashSet<string> { "Idle", "Walk", "Chase", "Correr", "Furia" };

        /// <summary>
        /// As animações próprias de um modelo (o &lt;modelo&gt;_Animacoes.fbx do Blender, uma faixa do NLA por animação, com o
        /// nome dela), importadas como Generic; null se o modelo não tem.
        /// </summary>
        static Dictionary<string, AnimationClip> AnimacoesProprias(string modelo)
        {
            string fbx = $"{PastaEspeciais}/{modelo}_Animacoes.fbx";
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) return null;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            imp.importAnimation = true;
            imp.importBlendShapes = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.generateSecondaryUV = false;
            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.name = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.loopTime = LacosDasAnimacoesProprias.Contains(c.name);
                c.loopPose = false;   // o laço já fecha (o último quadro é o primeiro)
                return c;
            }).ToArray();
            imp.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        }

        static float Altura(GameObject go)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return smr != null && smr.sharedMesh != null ? smr.sharedMesh.bounds.size.z * go.transform.lossyScale.y : 0f;
        }

        // um estado por clipe gravado (o primeiro, o Idle, é o de começo)
        static AnimatorController ControladorDaCenaDosEspeciais(string modelo, Dictionary<string, AnimationClip> clipes)
        {
            string caminho = $"{PastaDaCenaDosEspeciais}/{modelo}.controller";
            AssetDatabase.DeleteAsset(caminho);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(caminho);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var nome in AnimacoesDaCenaDosEspeciais)
            {
                if (!clipes.TryGetValue(nome, out var clipe)) continue;
                var e = sm.AddState(nome);
                e.motion = clipe;
                e.writeDefaultValues = false;
                if (nome == "Idle") sm.defaultState = e;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static Material MaterialDoChaoDosEspeciais()
        {
            string caminho = $"{PastaDaCenaDosEspeciais}/Chao.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", new Color(0.27f, 0.26f, 0.24f));
            m.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
