using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As animações dos zumbis especiais (01/11/2026): Correr (o Corredor em disparada, e o avanço do Brutamonte), Emergir
    /// (o Brutamonte brotando da terra), Furia (as braçadas dele), Cuspir e Gritar (o Long). Feitas pose a pose no
    /// esqueleto do Oco pelo Blender/especiais_animacoes.py, saem num FBX (Oco_AnimacoesEspeciais.fbx), importado como
    /// Humanoid, e são gravadas no esqueleto de cada zumbi como os outros clipes (<see cref="Gravar"/>).
    /// <para>
    /// No Zombie.controller, camada base: "Andar dos especiais" (o Andar do Oco com a disparada a 3,8 m/s; "Pernas" em 7, que
    /// só o Corredor e o Brutamonte usam, então os Ocos ficam como eram) e os estados Emergir, Furia, Cuspir e Gritar, sem
    /// transição: o código entra neles com "Pernas" em 6 (Zumbi.Brutamonte.cs e Zumbi.Long.cs) e sai pondo "Pernas" de volta.
    /// </para>
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string AnimacoesHumanoidesEspeciais = "Assets/_Game/Art/Characters/Oco/Oco_AnimacoesEspeciais.fbx";
        const string EstadoDoAndarDosEspeciais = "Andar dos especiais";

        static readonly (string nome, bool loop)[] ClipesEspeciais =
        {
            ("Correr", true), ("Emergir", false), ("Furia", true), ("Cuspir", false), ("Gritar", false),
        };

        static Dictionary<string, AnimationClip> ClipesHumanoidesEspeciais() =>
            File.Exists(AnimacoesHumanoidesEspeciais)
                ? ImportarHumanoide(AnimacoesHumanoidesEspeciais, ClipesEspeciais)
                : new Dictionary<string, AnimationClip>();

        /// <summary>
        /// Só as animações dos especiais, sem remontar os zumbis: grava os cinco clipes em cada modelo (os Ocos também, para
        /// o controlador ter os clipes do Oco_Original), põe os estados no Zombie.controller e as trocas nos controladores
        /// dos outros modelos.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Zumbis/Gravar as animações dos especiais (sem remontar)")]
        public static void GravarAsAnimacoesDosEspeciais()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de gravar as animações dos especiais.");
                return;
            }
            var fontes = ClipesHumanoidesEspeciais();
            if (fontes.Count < ClipesEspeciais.Length)
                throw new FileNotFoundException("Faltam as animações dos especiais (rode o Blender/especiais_animacoes.py)", AnimacoesHumanoidesEspeciais);
            var porModelo = new Dictionary<string, Dictionary<string, AnimationClip>>();
            foreach (var nome in TodosOsModelos)
            {
                string fbx = FbxDe(nome);
                var humano = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
                if (humano == null) throw new System.Exception($"{nome}: sem o avatar Humanoid (monte os zumbis uma vez).");
                porModelo[nome] = Gravar(nome, fbx, humano, fontes, ClipesEspeciais);
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null) throw new FileNotFoundException("Falta o controlador dos zumbis (monte os zumbis uma vez)", Controlador);
            var doPrimeiro = ClipesGravados(Modelos[0]);
            EstadosDosEspeciais(ctrl, doPrimeiro);
            AssetDatabase.SaveAssets();
            foreach (var nome in TodosOsModelos.Skip(1))
            {
                string caminho = $"{Arte}/Zombie_{nome}.overrideController";
                var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(caminho);
                if (aoc == null) throw new FileNotFoundException("Falta o controlador do modelo", caminho);
                aoc.runtimeAnimatorController = ctrl;
                var trocas = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
                aoc.GetOverrides(trocas);
                for (int k = 0; k < trocas.Count; k++)
                    foreach (var (clipe, _) in ClipesEspeciais)
                        if (trocas[k].Key == doPrimeiro[clipe])
                            trocas[k] = new KeyValuePair<AnimationClip, AnimationClip>(trocas[k].Key, porModelo[nome][clipe]);
                aoc.ApplyOverrides(trocas);
                EditorUtility.SetDirty(aoc);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"As animações dos especiais foram gravadas em {porModelo.Count} modelos.");
        }

        /// <summary>Os estados dos especiais na camada base (refeitos se já existem), com os clipes do primeiro modelo.</summary>
        internal static void EstadosDosEspeciais(AnimatorController ctrl, Dictionary<string, AnimationClip> c)
        {
            if (c == null || ClipesEspeciais.Any(e => !c.ContainsKey(e.nome))) return;
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState Achar(string nome) => sm.states.Select(s => s.state).FirstOrDefault(s => s.name == nome);

            // o andar dos especiais: refeito (a árvore velha sai do arquivo do controlador)
            var velho = Achar(EstadoDoAndarDosEspeciais);
            if (velho != null)
            {
                var arvoreVelha = velho.motion as BlendTree;
                sm.RemoveState(velho);   // (as transições para ele saem junto)
                if (arvoreVelha != null && AssetDatabase.IsSubAsset(arvoreVelha)) AssetDatabase.RemoveObjectFromAsset(arvoreVelha);
            }
            var andar = Arvore(ctrl, EstadoDoAndarDosEspeciais, (c["Idle"], 0f), (c["Walk"], 0.6f), (c["Chase"], 1.12f), (c["Correr"], 3.8f));
            var ida = sm.AddAnyStateTransition(andar);
            ida.AddCondition(AnimatorConditionMode.Equals, 7, "Pernas");
            ida.canTransitionToSelf = false;
            ida.hasExitTime = false;
            ida.duration = 0.2f;

            // as ações (sem transição: o código entra nelas)
            foreach (var nome in new[] { "Emergir", "Furia", "Cuspir", "Gritar" })
            {
                var e = Achar(nome) ?? Estado(sm, nome, c[nome]);
                e.motion = c[nome];
                e.writeDefaultValues = false;
            }
            EditorUtility.SetDirty(ctrl);
        }
    }
}
