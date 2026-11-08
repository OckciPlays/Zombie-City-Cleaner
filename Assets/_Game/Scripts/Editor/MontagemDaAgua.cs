using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O nado e a remada (07/11/2026, a pedido: "a braçada pelo Blender; vamos aproveitar e fazer a remada do caiaque"). As
    /// animações vêm do <c>Blender/agua_animacoes.py</c> (em segundo plano, a partir dos FBX do personagem que estão no
    /// projeto):
    /// <list type="bullet">
    /// <item><c>Jogador_FP_Nadar.fbx</c>: os braços de primeira pessoa nadando (Idle boiando, Walk a braçada, Run a braçada
    /// rápida, Sacar). Vira o prefab <c>Resources/BracosNadando</c>, com o controlador <c>Jogador_FP_Nadar.controller</c>
    /// (o Sacar e a árvore pelo Speed), que o <see cref="ArmaJogador"/> mostra nadando.</item>
    /// <item><c>Jogador_FP_Remo.fbx</c>: os braços com o remo do caiaque (Idle, Walk a remada, Sacar), o prefab
    /// <c>Resources/BracosRemando</c> (para a etapa do caiaque).</item>
    /// <item><c>Personagem_PrincipalBloco_Agua.fbx</c>: o corpo boiando, nadando e remando (Boiar, Nadar, Remar,
    /// Remar_Parado), no avatar do personagem. Entra no controlador do corpo (<see cref="PorONadoNoControlador"/>): o
    /// parâmetro "Nadando" leva ao estado "Nadando" (a árvore pelo Speed: boiando, nadando e, mais rápido, a braçada
    /// acelerada), e o <see cref="NadoNoCorpo"/> liga.</item>
    /// </list>
    /// Menu Zombie City Cleaner > Jogador > Montar o nado e a remada. A montagem dos personagens (MontagemDasArmas) também
    /// põe o nado no controlador do corpo que ela refaz.
    /// </summary>
    public static class MontagemDaAgua
    {
        const string ArteJogador = "Assets/_Game/Art/Characters/Jogador";
        public const string FbxNadar = ArteJogador + "/Jogador_FP_Nadar.fbx";
        public const string FbxRemo = ArteJogador + "/Jogador_FP_Remo.fbx";
        const string ControladorNadar = ArteJogador + "/Jogador_FP_Nadar.controller";
        const string ControladorRemo = ArteJogador + "/Jogador_FP_Remo.controller";
        const string PrefabNadar = "Assets/_Game/Resources/BracosNadando.prefab";
        const string PrefabRemo = "Assets/_Game/Resources/BracosRemando.prefab";
        public const string FbxDoCorpo = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco_Agua.fbx";
        const string FbxDoPersonagem = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco.fbx";
        const string MaterialDoCorpo = MontagemDasArmas.ArtePersonagens + "/Materials/Personagem_PrincipalBloco.mat";
        const string MaterialCidade = "Assets/_Game/Art/Materials/Cidade.mat";
        // as cópias dos clipes do corpo, com os caminhos dos ossos a partir da raiz do personagem (veja ClipesDoCorpo)
        const string PastaDosClipesDoCorpo = MontagemDasArmas.ArtePersonagens + "/Agua";
        const string RigDoCorpo = "PrincipalBloco_Rig";
        static readonly string[] ClipesDaAgua = { "Boiar", "Nadar", "Remar", "Remar_Parado" };

        /// <summary>A velocidade de cada braçada na árvore (m/s): a do nado normal e a do rápido (MovimentoPrimeiraPessoa).</summary>
        const float NadoNormal = 1.9f, NadoRapido = 2.8f;

        [MenuItem("Zombie City Cleaner/Jogador/Montar o nado e a remada")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar o nado.");
                return;
            }
            int feitos = 0;
            if (File.Exists(FbxNadar))
            {
                ImportarFP(FbxNadar);
                MontarBracos(FbxNadar, ControladorNadar, PrefabNadar, new[] { ("Idle", 0f, 1f), ("Walk", NadoNormal, 1f), ("Run", NadoRapido, 1f) });
                feitos++;
            }
            else Debug.LogWarning($"Nado: falta {FbxNadar} (rode o Blender/agua_animacoes.py).");
            if (File.Exists(FbxRemo))
            {
                ImportarFP(FbxRemo);
                // (a remada vai pelo Speed do caiaque: parado, o Idle; remando, a remada, mais rápida até 3,2 m/s)
                MontarBracos(FbxRemo, ControladorRemo, PrefabRemo, new[] { ("Idle", 0f, 1f), ("Walk", 1.6f, 1f), ("Walk", 3.2f, 1.4f) });
                feitos++;
            }
            else Debug.LogWarning($"Remada: falta {FbxRemo} (rode o Blender/agua_animacoes.py).");
            if (File.Exists(FbxDoCorpo))
            {
                ImportarCorpo();
                ClipesDoCorpo();
                for (int p = 0; p < Personagens.Modelos.Length; p++)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(MontagemDasArmas.ControladorDoCorpo(p));
                    if (ctrl != null) PorONadoNoControlador(ctrl);
                }
                feitos++;
            }
            else Debug.LogWarning($"Nado: falta {FbxDoCorpo} (rode o Blender/agua_animacoes.py).");
            AssetDatabase.SaveAssets();
            Debug.Log($"O nado e a remada montados ({feitos} de 3 arquivos).");
        }

        // os braços de primeira pessoa: como os das armas (MontagemDasArmas.ImportarFP), Generic com o avatar deles, sem a
        // conversão de eixos e sem compressão (a 30 cm da câmera, os milímetros aparecem)
        static void ImportarFP(string caminho)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PrincipalBloco"), AssetDatabase.LoadAssetAtPath<Material>(MaterialDoCorpo));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pistola"), AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Paleta"), AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade));   // (o remo)
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.SaveAndReimport();
            imp.clipAnimations = Clipes(imp, n => n != "Sacar");
            imp.SaveAndReimport();
        }

        // o corpo: Generic, no avatar do personagem (os clipes valem nos ossos dele, pelo caminho), todos em laço
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
            imp.clipAnimations = Clipes(imp, _ => true);
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Os clipes do corpo, copiados com os caminhos certos. O FBX da água só tem o esqueleto, e a Unity faz dele a raiz
        /// do modelo: os caminhos saem a partir do <c>PrincipalBloco_Rig</c> ("root/hips"; o giro dele no caminho vazio,
        /// que no personagem é a raiz e o deitava). No personagem, o esqueleto é um filho ("PrincipalBloco_Rig/root/hips"):
        /// cada curva ganha o prefixo, e a cópia vai para <c>Personagens/Agua/&lt;clipe&gt;.anim</c> (em laço).
        /// </summary>
        static void ClipesDoCorpo()
        {
            if (!AssetDatabase.IsValidFolder(PastaDosClipesDoCorpo)) AssetDatabase.CreateFolder(MontagemDasArmas.ArtePersonagens, "Agua");
            foreach (var nome in ClipesDaAgua)
            {
                var fonte = Clipe(FbxDoCorpo, nome);
                if (fonte == null) continue;
                string caminho = $"{PastaDosClipesDoCorpo}/{nome}.anim";
                var copia = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
                bool nova = copia == null;
                if (nova) copia = new AnimationClip { name = nome };
                else copia.ClearCurves();
                copia.frameRate = fonte.frameRate;
                foreach (var b in AnimationUtility.GetCurveBindings(fonte))
                {
                    var curva = AnimationUtility.GetEditorCurve(fonte, b);
                    var novo = b;
                    novo.path = string.IsNullOrEmpty(b.path) ? RigDoCorpo : RigDoCorpo + "/" + b.path;
                    AnimationUtility.SetEditorCurve(copia, novo, curva);
                }
                var ajustes = AnimationUtility.GetAnimationClipSettings(copia);
                ajustes.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copia, ajustes);
                if (nova) AssetDatabase.CreateAsset(copia, caminho);
                else EditorUtility.SetDirty(copia);
            }
            AssetDatabase.SaveAssets();
        }

        static AnimationClip ClipeDoCorpo(string nome)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{PastaDosClipesDoCorpo}/{nome}.anim");
            if (c == null) Debug.LogError($"Falta {PastaDosClipesDoCorpo}/{nome}.anim (Zombie City Cleaner > Jogador > Montar o nado e a remada).");
            return c;
        }

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

        // o controlador dos braços (o Sacar ao aparecer, depois a árvore pelo Speed) e o prefab em Resources
        static void MontarBracos(string fbx, string caminhoDoControlador, string caminhoDoPrefab, (string clipe, float speed, float ritmo)[] arvoreDe)
        {
            AssetDatabase.DeleteAsset(caminhoDoControlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(caminhoDoControlador);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;
            var arvore = new BlendTree { name = "Movimento", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arvore, ctrl);
            foreach (var (clipe, speed, _) in arvoreDe) arvore.AddChild(Clipe(fbx, clipe), speed);
            var filhos = arvore.children;
            for (int i = 0; i < filhos.Length; i++) filhos[i].timeScale = arvoreDe[i].ritmo;
            arvore.children = filhos;
            var movimento = sm.AddState("Movimento");
            movimento.motion = arvore;
            var sacar = sm.AddState("Sacar");
            sacar.motion = Clipe(fbx, "Sacar");
            sm.defaultState = sacar;
            var t = sacar.AddTransition(movimento);
            t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.1f; t.hasFixedDuration = true;
            EditorUtility.SetDirty(ctrl);

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
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
                PrefabUtility.SaveAsPrefabAsset(go, caminhoDoPrefab);
            }
            finally { Object.DestroyImmediate(go); }
            Debug.Log($"Braços montados: {caminhoDoPrefab} ({string.Join(", ", arvoreDe.Select(a => $"{a.clipe} a {a.speed} m/s"))}).");
        }

        /// <summary>
        /// O nado no controlador do corpo (pode rodar de novo: refaz): os parâmetros "Nadando" e "NadandoRapido" e o estado
        /// "Nadando" (a árvore pelo Speed: o Boiar parado, o Nadar a 1,9 m/s e, a 2,8, o Nadar 1,5 vez mais rápido), com a
        /// ida de qualquer estado e a volta à locomoção. A camada dos braços sai pelo código (o NadoNoCorpo).
        /// </summary>
        public static void PorONadoNoControlador(AnimatorController ctrl)
        {
            if (!File.Exists(FbxDoCorpo)) return;
            var boiar = ClipeDoCorpo("Boiar");
            var nadar = ClipeDoCorpo("Nadar");
            if (boiar == null || nadar == null) return;
            if (!ctrl.parameters.Any(p => p.name == "Nadando")) ctrl.AddParameter("Nadando", AnimatorControllerParameterType.Bool);
            if (!ctrl.parameters.Any(p => p.name == "NadandoRapido")) ctrl.AddParameter("NadandoRapido", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var velho = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Nadando");
            if (velho != null)
            {
                foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == velho).ToArray()) sm.RemoveAnyStateTransition(t);
                if (velho.motion is BlendTree arvoreVelha) Object.DestroyImmediate(arvoreVelha, true);
                sm.RemoveState(velho);
            }
            var loc = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomoção") ?? sm.defaultState;
            var arvore = new BlendTree { name = "Nado", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arvore, ctrl);
            arvore.AddChild(boiar, 0f);
            arvore.AddChild(nadar, NadoNormal);
            arvore.AddChild(nadar, NadoRapido);
            var filhos = arvore.children;
            filhos[2].timeScale = MovimentoPrimeiraPessoa.BracadaNormal / MovimentoPrimeiraPessoa.BracadaRapida;
            arvore.children = filhos;
            var nado = sm.AddState("Nadando");
            nado.motion = arvore;
            nado.writeDefaultValues = false;
            var ida = sm.AddAnyStateTransition(nado);
            ida.AddCondition(AnimatorConditionMode.If, 0f, "Nadando");
            ida.canTransitionToSelf = false; ida.hasExitTime = false; ida.duration = 0.3f; ida.hasFixedDuration = true;
            if (loc != null)
            {
                var volta = nado.AddTransition(loc);
                volta.AddCondition(AnimatorConditionMode.IfNot, 0f, "Nadando");
                volta.hasExitTime = false; volta.duration = 0.3f; volta.hasFixedDuration = true;
            }
            PorORemarNoControlador(ctrl, sm, loc);
            EditorUtility.SetDirty(ctrl);
        }

        /// <summary>
        /// O caiaque no controlador do corpo (07/11/2026; o PassageiroDoCaiaque põe): o parâmetro inteiro "Remando" (1 sentado
        /// no caiaque, o Remar_Parado: as pernas esticadas e as mãos no remo; 2 remando, o Remar) e a volta à locomoção com 0.
        /// </summary>
        static void PorORemarNoControlador(AnimatorController ctrl, AnimatorStateMachine sm, AnimatorState loc)
        {
            var remar = ClipeDoCorpo("Remar");
            var parado = ClipeDoCorpo("Remar_Parado");
            if (remar == null || parado == null) return;
            if (!ctrl.parameters.Any(p => p.name == "Remando")) ctrl.AddParameter("Remando", AnimatorControllerParameterType.Int);
            foreach (var nome in new[] { "Remar", "Remar_Parado" })
            {
                var velho = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == nome);
                if (velho == null) continue;
                foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == velho).ToArray()) sm.RemoveAnyStateTransition(t);
                sm.RemoveState(velho);
            }
            foreach (var (nome, clipe, valor) in new[] { ("Remar", remar, 2), ("Remar_Parado", parado, 1) })
            {
                var e = sm.AddState(nome);
                e.motion = clipe;
                e.writeDefaultValues = false;
                var ida = sm.AddAnyStateTransition(e);
                ida.AddCondition(AnimatorConditionMode.Equals, valor, "Remando");
                ida.canTransitionToSelf = false; ida.hasExitTime = false; ida.duration = 0.25f; ida.hasFixedDuration = true;
                if (loc == null) continue;
                var volta = e.AddTransition(loc);
                volta.AddCondition(AnimatorConditionMode.Equals, 0, "Remando");
                volta.hasExitTime = false; volta.duration = 0.25f; volta.hasFixedDuration = true;
            }
        }
    }
}
