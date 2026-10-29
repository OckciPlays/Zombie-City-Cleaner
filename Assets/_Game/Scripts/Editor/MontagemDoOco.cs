using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As animações do Oco de teste (a pedido em 27/10/2026): os clipes dos zumbis num controlador Humanoid, o Oco.controller,
    /// que as versões do Oco usam (<see cref="MontagemDasVariantesDoOco"/>; cada uma passa os clipes pelo avatar dela).
    ///  - Oco_Animacoes.fbx e Oco_AnimacoesExtra.fbx: cópias do ZombieGaroto.fbx e do _Extra, importadas como Humanoid
    ///    (o mesmo arquivo não pode ser Generic para os zumbis e Humanoid aqui).
    /// Até 28/10/2026, esta montagem fazia também o Oco de teste antigo (o O_Oco.fbx do Blender, ~125 mil triângulos, com os
    /// materiais, as texturas e o prefab Resources/Oco_Teste): ele saiu do jogo a pedido (pesado demais), e o F1 põe só as
    /// versões de ~5 mil. Menu Zombie City Cleaner > Zumbis > Montar as animações do Oco (teste).
    /// </summary>
    public static class MontagemDoOco
    {
        const string Pasta = "Assets/_Game/Art/Characters/Oco";
        const string Animacoes = Pasta + "/Oco_Animacoes.fbx";
        const string AnimacoesExtra = Pasta + "/Oco_AnimacoesExtra.fbx";
        const string Controlador = Pasta + "/Oco.controller";

        static readonly (string nome, bool loop)[] Clipes =
        {
            ("Idle", true), ("Walk", true), ("Chase", true), ("Attack", false), ("HitReact", false),
        };
        static readonly (string nome, bool loop)[] ClipesExtra =
        {
            ("Cair", false), ("Rastejar", true), ("RastejarParado", true), ("MancarE", true), ("MancarD", true),
        };

        [MenuItem("Zombie City Cleaner/Zumbis/Montar as animações do Oco (teste)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as animações do Oco.");
                return;
            }
            var clipes = ImportarClipes(Animacoes, Clipes);
            foreach (var c in ImportarClipes(AnimacoesExtra, ClipesExtra)) clipes[c.Key] = c.Value;
            MontarControlador(clipes);
            Debug.Log($"As animações do Oco montadas: {Controlador} ({clipes.Count} animações).");
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
    }
}
