using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A vara de pesca nova (07/11/2026, a pedido: "deveria segurar a carretilha com a mão esquerda e a base da vara de pesca
    /// com a mão direita"). Os arquivos vêm do <c>Blender/vara_animacoes.py</c> (em segundo plano, a partir dos FBX do
    /// personagem que estão no projeto):
    /// <list type="bullet">
    /// <item><c>Jogador_FP_Vara.fbx</c>: os braços de primeira pessoa com a vara (o osso "vara", o "manivela" e o
    /// "ponta"), com os clipes Idle, Walk, Sacar, Lancar, Recolher e Puxar. Vira o prefab <c>Resources/BracosVara</c>, com
    /// o controlador <c>Jogador_FP_Vara.controller</c> (o Sacar, o "Movimento" pelo Speed e os estados da pesca, que o
    /// <see cref="PescaJogador"/> toca), que o <see cref="ArmaJogador"/> mostra no lugar dos braços da lata.</item>
    /// <item><c>Art/Pesca/Vara.fbx</c>: a vara sozinha, com a carretilha, a "Manivela" e a "Ponta", para o corpo. Vira o
    /// <c>Resources/VaraDoCorpo</c>: a raiz é a mão direita (hand.R), e a vara dentro dela fica na pegada medida nos
    /// clipes do corpo (o osso "vara" deles, parado no espaço da mão); a <see cref="VaraNoCorpo"/> prende na mão.</item>
    /// <item><c>Personagem_PrincipalBloco_Pesca.fbx</c>: o corpo com a vara (Vara_Segurar, Vara_Lancar, Vara_Recolher,
    /// Vara_Puxar), no avatar do personagem. Os clipes vão para <c>Personagens/Pesca</c> (com os caminhos a partir da raiz
    /// do personagem) e entram como estados na camada dos braços do controlador do corpo
    /// (<see cref="PorAVaraNoControlador"/>), que a VaraNoCorpo toca.</item>
    /// </list>
    /// Menu Zombie City Cleaner > Jogador > Montar a vara de pesca. A montagem dos personagens (MontagemDasArmas) também
    /// põe os estados no controlador do corpo que ela refaz.
    /// </summary>
    public static class MontagemDaVara
    {
        const string ArteJogador = "Assets/_Game/Art/Characters/Jogador";
        public const string FbxFP = ArteJogador + "/Jogador_FP_Vara.fbx";
        const string ControladorFP = ArteJogador + "/Jogador_FP_Vara.controller";
        const string PrefabFP = "Assets/_Game/Resources/" + ArmaJogador.RecursoDosBracosDaVara + ".prefab";
        public const string FbxDaVara = "Assets/_Game/Art/Pesca/Vara.fbx";
        const string PrefabDaVara = "Assets/_Game/Resources/" + VaraNoCorpo.Recurso + ".prefab";
        public const string FbxDoCorpo = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco_Pesca.fbx";
        const string FbxDoPersonagem = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco.fbx";
        const string MaterialDoCorpo = MontagemDasArmas.ArtePersonagens + "/Materials/Personagem_PrincipalBloco.mat";
        const string MaterialCidade = "Assets/_Game/Art/Materials/Cidade.mat";
        const string PastaDosClipes = MontagemDasArmas.ArtePersonagens + "/Pesca";
        const string RigDoCorpo = "PrincipalBloco_Rig";
        /// <summary>Os clipes do corpo e os que repetem.</summary>
        static readonly (string nome, bool laco)[] ClipesDoCorpo =
            { ("Vara_Segurar", true), ("Vara_Lancar", false), ("Vara_Recolher", true), ("Vara_Puxar", true) };
        /// <summary>
        /// O lado de cima da vara no Vara.fbx, como ele chega na Unity (onde fica a carretilha). A ponta sai da "Ponta" (da
        /// origem até ela).
        /// </summary>
        static readonly Vector3 CimaDaVaraNoModelo = Vector3.up;

        [MenuItem("Zombie City Cleaner/Jogador/Montar a vara de pesca")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a vara.");
                return;
            }
            AssetDatabase.Refresh();
            int feitos = 0;
            if (File.Exists(FbxFP))
            {
                ImportarFP();
                MontarOsBracos();
                feitos++;
            }
            else Debug.LogWarning($"Vara: falta {FbxFP} (rode o Blender/vara_animacoes.py).");
            if (File.Exists(FbxDoCorpo))
            {
                ImportarCorpo();
                CopiarClipesDoCorpo();
                for (int p = 0; p < Personagens.Modelos.Length; p++)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(MontagemDasArmas.ControladorDoCorpo(p));
                    if (ctrl != null) PorAVaraNoControlador(ctrl);
                }
                feitos++;
            }
            else Debug.LogWarning($"Vara: falta {FbxDoCorpo} (rode o Blender/vara_animacoes.py).");
            if (File.Exists(FbxDaVara))
            {
                ImportarAVara();
                if (MontarAVaraDoCorpo()) feitos++;
            }
            else Debug.LogWarning($"Vara: falta {FbxDaVara} (rode o Blender/vara_animacoes.py).");
            AssetDatabase.SaveAssets();
            Debug.Log($"A vara de pesca montada ({feitos} de 3 partes).");
        }

        // ------------------------------------------------------------------ a primeira pessoa

        // como os braços das armas e da remada: Generic com o avatar deles, sem a conversão de eixos e sem compressão
        static void ImportarFP()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxFP);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PrincipalBloco"), AssetDatabase.LoadAssetAtPath<Material>(MaterialDoCorpo));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pistola"), AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Paleta"), AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade));
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.SaveAndReimport();
            imp.clipAnimations = Clipes(imp, n => n != "Sacar" && n != "Lancar");
            imp.SaveAndReimport();
        }

        /// <summary>
        /// O controlador dos braços: o Sacar ao aparecer; o "Movimento" (a árvore pelo Speed: parado, andando e, mais
        /// rápido, correndo); o Lancar (uma vez, e volta ao Movimento), o Recolher e o Puxar (em laço), que o PescaJogador
        /// toca pelo nome. E o prefab em Resources.
        /// </summary>
        static void MontarOsBracos()
        {
            AssetDatabase.DeleteAsset(ControladorFP);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControladorFP);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;
            var arvore = new BlendTree { name = "Movimento", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arvore, ctrl);
            arvore.AddChild(Clipe(FbxFP, "Idle"), 0f);
            arvore.AddChild(Clipe(FbxFP, "Walk"), 2f);
            arvore.AddChild(Clipe(FbxFP, "Walk"), 4.2f);
            var filhos = arvore.children;
            filhos[2].timeScale = 1.4f;
            arvore.children = filhos;
            var movimento = sm.AddState("Movimento");
            movimento.motion = arvore;
            var sacar = sm.AddState("Sacar");
            sacar.motion = Clipe(FbxFP, "Sacar");
            sm.defaultState = sacar;
            var t = sacar.AddTransition(movimento);
            t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.1f; t.hasFixedDuration = true;
            var lancar = sm.AddState("Lancar");
            lancar.motion = Clipe(FbxFP, "Lancar");
            var volta = lancar.AddTransition(movimento);
            volta.hasExitTime = true; volta.exitTime = 1f; volta.duration = 0.15f; volta.hasFixedDuration = true;
            foreach (var nome in new[] { "Recolher", "Puxar" }) sm.AddState(nome).motion = Clipe(FbxFP, nome);
            EditorUtility.SetDirty(ctrl);

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxFP);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            try
            {
                var animador = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animador.runtimeAnimatorController = ctrl;
                animador.applyRootMotion = false;
                animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                }
                go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (Achar(go.transform, "ponta") == null) Debug.LogWarning("Vara: os braços não têm o osso \"ponta\" (a linha sai de um ponto chutado).");
                PrefabUtility.SaveAsPrefabAsset(go, PrefabFP);
            }
            finally { Object.DestroyImmediate(go); }
            Debug.Log($"Vara: os braços montados ({PrefabFP}).");
        }

        // ------------------------------------------------------------------ o corpo

        static void ImportarCorpo()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxDoCorpo);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxDoPersonagem).OfType<Avatar>().FirstOrDefault();
            imp.animationType = ModelImporterAnimationType.Generic;
            if (avatar != null)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
            }
            else imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            MontagemDasArmas.ErroPequeno(imp);
            imp.SaveAndReimport();
            imp.clipAnimations = Clipes(imp, n => ClipesDoCorpo.Any(c => c.nome == n && c.laco));
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Os clipes do corpo, copiados com os caminhos a partir da raiz do personagem (como os da água: o FBX só tem o
        /// esqueleto, e a Unity faz dele a raiz). As curvas do osso "vara" (que o personagem não tem) ficam de fora.
        /// </summary>
        static void CopiarClipesDoCorpo()
        {
            if (!AssetDatabase.IsValidFolder(PastaDosClipes)) AssetDatabase.CreateFolder(MontagemDasArmas.ArtePersonagens, "Pesca");
            foreach (var (nome, laco) in ClipesDoCorpo)
            {
                var fonte = Clipe(FbxDoCorpo, nome);
                if (fonte == null) continue;
                string caminho = $"{PastaDosClipes}/{nome}.anim";
                var copia = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
                bool nova = copia == null;
                if (nova) copia = new AnimationClip { name = nome };
                else copia.ClearCurves();
                copia.frameRate = fonte.frameRate;
                foreach (var b in AnimationUtility.GetCurveBindings(fonte))
                {
                    if (b.path.EndsWith("/vara") || b.path.Contains("/vara/")) continue;
                    var curva = AnimationUtility.GetEditorCurve(fonte, b);
                    var novo = b;
                    novo.path = string.IsNullOrEmpty(b.path) ? RigDoCorpo : RigDoCorpo + "/" + b.path;
                    AnimationUtility.SetEditorCurve(copia, novo, curva);
                }
                var ajustes = AnimationUtility.GetAnimationClipSettings(copia);
                ajustes.loopTime = laco;
                AnimationUtility.SetAnimationClipSettings(copia, ajustes);
                if (nova) AssetDatabase.CreateAsset(copia, caminho);
                else EditorUtility.SetDirty(copia);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// A vara no controlador do corpo (pode rodar de novo: refaz): na camada dos braços (a do peito para cima), os estados
        /// Vara_Segurar, Vara_Lancar (uma vez, e volta ao Vara_Segurar), Vara_Recolher e Vara_Puxar, sem transições de
        /// qualquer estado: quem toca é a <see cref="VaraNoCorpo"/>, pelo nome, e ela volta ao "Segurar" quando a vara sai da
        /// mão. Os override (o da lata, que a vara usa) herdam os estados.
        /// </summary>
        public static void PorAVaraNoControlador(AnimatorController ctrl)
        {
            if (ctrl.layers.Length < 2) return;
            var clipes = ClipesDoCorpo.Select(c => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{PastaDosClipes}/{c.nome}.anim")).ToArray();
            if (clipes.Any(c => c == null)) return;
            var sm = ctrl.layers[1].stateMachine;
            foreach (var (nome, _) in ClipesDoCorpo)
            {
                var velho = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == nome);
                if (velho != null) sm.RemoveState(velho);
            }
            AnimatorState segurar = null;
            for (int i = 0; i < ClipesDoCorpo.Length; i++)
            {
                var e = sm.AddState(ClipesDoCorpo[i].nome);
                e.motion = clipes[i];
                e.writeDefaultValues = false;
                if (i == 0) segurar = e;
            }
            var lancar = sm.states.Select(s => s.state).First(s => s.name == "Vara_Lancar");
            var volta = lancar.AddTransition(segurar);
            volta.hasExitTime = true; volta.exitTime = 1f; volta.duration = 0.15f; volta.hasFixedDuration = true;
            EditorUtility.SetDirty(ctrl);
        }

        // ------------------------------------------------------------------ a vara do corpo

        static void ImportarAVara()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxDaVara);
            var cidade = AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade);
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (cidade != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Paleta"), cidade);
            imp.SaveAndReimport();
        }

        /// <summary>
        /// O <c>Resources/VaraDoCorpo</c>: a raiz no lugar da mão direita; dentro, a vara na pegada dos clipes do corpo (o osso
        /// "vara" do Vara_Segurar, no espaço do hand.R: a cabeça na pegada, a cauda para a ponta, o Z para o lado de cima),
        /// girada para a ponta dela ir pelo osso e a carretilha ficar em cima.
        /// </summary>
        static bool MontarAVaraDoCorpo()
        {
            var fbxVara = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDaVara);
            var fbxCorpo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDoCorpo);
            var segurar = File.Exists(FbxDoCorpo) ? Clipe(FbxDoCorpo, "Vara_Segurar") : null;
            if (fbxVara == null || fbxCorpo == null || segurar == null)
            {
                Debug.LogWarning("Vara: sem o Vara.fbx, o FBX do corpo ou o Vara_Segurar, a vara do corpo não foi montada.");
                return false;
            }
            // a pegada: o osso "vara" no espaço da mão, no primeiro quadro do segurar (ele fica parado na mão)
            var corpo = Object.Instantiate(fbxCorpo);
            Vector3 pegada;
            Quaternion giro;
            try
            {
                segurar.SampleAnimation(corpo, 0f);
                var mao = Achar(corpo.transform, "hand.R");
                var osso = Achar(corpo.transform, "vara");
                if (mao == null || osso == null)
                {
                    Debug.LogError("Vara: o FBX do corpo não tem o hand.R com o osso \"vara\" (o Blender/vara_animacoes.py põe).");
                    return false;
                }
                // em metros, no giro da mão (sem a escala dos ossos: a VaraNoCorpo põe a raiz no lugar e no giro da mão)
                pegada = Quaternion.Inverse(mao.rotation) * (osso.position - mao.position);
                giro = Quaternion.Inverse(mao.rotation) * osso.rotation;
            }
            finally { Object.DestroyImmediate(corpo); }

            var raiz = new GameObject(VaraNoCorpo.Recurso);
            try
            {
                var modelo = Object.Instantiate(fbxVara, raiz.transform, false);
                modelo.name = "Vara";
                foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var ponta = Achar(modelo.transform, "Ponta");
                // a ponta e o lado de cima da vara, no espaço da raiz do modelo
                Vector3 paraAPonta = ponta != null ? modelo.transform.InverseTransformPoint(ponta.position) : Vector3.forward;
                if (paraAPonta.sqrMagnitude < 1e-6f) paraAPonta = Vector3.forward;
                var doModelo = Quaternion.LookRotation(paraAPonta.normalized, CimaDaVaraNoModelo);
                // no osso: a ponta pelo Y dele, o lado de cima pelo Z
                var doOsso = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                var giroDoModelo = giro * doOsso * Quaternion.Inverse(doModelo);
                modelo.transform.SetLocalPositionAndRotation(pegada, giroDoModelo);
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDaVara);
                Debug.Log($"Vara: a vara do corpo montada ({PrefabDaVara}): a pegada {pegada.x:0.000}, {pegada.y:0.000}, {pegada.z:0.000} na mão, " +
                          $"a ponta a {paraAPonta.magnitude:0.00} m.");
            }
            finally { Object.DestroyImmediate(raiz); }
            return true;
        }

        // ------------------------------------------------------------------ ajudantes

        // um clipe por faixa de NLA (o nome dela), do primeiro ao último quadro
        static ModelImporterClipAnimation[] Clipes(ModelImporter imp, System.Func<string, bool> laco) =>
            imp.importedTakeInfos.Select(t =>
            {
                string nome = t.name.Contains("|") ? t.name.Substring(t.name.LastIndexOf('|') + 1) : t.name;
                return new ModelImporterClipAnimation
                {
                    name = nome, takeName = t.name,
                    firstFrame = Mathf.Round(t.startTime * t.sampleRate), lastFrame = Mathf.Round(t.stopTime * t.sampleRate),
                    loopTime = laco(nome),
                };
            }).ToArray();

        static AnimationClip Clipe(string fbx, string nome)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(x => x.name == nome);
            if (c == null) Debug.LogError($"{fbx} não tem o clipe {nome}.");
            return c;
        }

        static Transform Achar(Transform raiz, string nome) =>
            raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nome);
    }
}
