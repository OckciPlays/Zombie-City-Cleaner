using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O levantar numa perna só (31/10/2026, a pedido: "Ele está levantando de repente, sem utilizar o apoio dos braços
    /// [...] Vamos fazer as animações pelo blender, faça bem detalhadas"). Os clipes LevantarE (sem a perna esquerda) e
    /// LevantarD (o espelho) são feitos pose a pose no esqueleto do Oco pelo Blender/equilibrio_oco.py: de bruços, ele
    /// arrasta as mãos para baixo dos ombros, empurra o chão (os braços falham uma vez), põe o joelho embaixo do corpo,
    /// quase tomba para o lado do coto e se segura na mão, arrasta o pé até ficar agachado numa perna e sobe empurrando
    /// o chão, os braços abertos para se equilibrar. Saem num FBX (Oco_AnimacoesEquilibrio.fbx), importado como Humanoid,
    /// e são gravados no esqueleto de cada Oco como os outros clipes (<see cref="Gravar"/>).
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string AnimacoesHumanoidesDoEquilibrio = "Assets/_Game/Art/Characters/Oco/Oco_AnimacoesEquilibrio.fbx";

        static readonly (string nome, bool loop)[] ClipesDoEquilibrio = { ("LevantarE", false), ("LevantarD", false) };

        static Dictionary<string, AnimationClip> ClipesHumanoidesDoEquilibrio() =>
            File.Exists(AnimacoesHumanoidesDoEquilibrio)
                ? ImportarHumanoide(AnimacoesHumanoidesDoEquilibrio, ClipesDoEquilibrio)
                : new Dictionary<string, AnimationClip>();

        /// <summary>
        /// Só o levantar, sem remontar os zumbis: grava os dois clipes em cada Oco, põe os estados no Zombie.controller (com
        /// os clipes do Oco_Original) e as trocas nos controladores dos outros Ocos.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Zumbis/Gravar o levantar numa perna (sem remontar)")]
        public static void GravarOLevantar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de gravar o levantar.");
                return;
            }
            var fontes = ClipesHumanoidesDoEquilibrio();
            if (fontes.Count < ClipesDoEquilibrio.Length)
                throw new FileNotFoundException("Faltam os clipes do levantar (rode o Blender/equilibrio_oco.py)", AnimacoesHumanoidesDoEquilibrio);
            var porModelo = new Dictionary<string, Dictionary<string, AnimationClip>>();
            foreach (var nome in Modelos)
            {
                string fbx = FbxDe(nome);
                var humano = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
                if (humano == null) throw new System.Exception($"{nome}: sem o avatar Humanoid (monte os zumbis uma vez).");
                porModelo[nome] = Gravar(nome, fbx, humano, fontes, ClipesDoEquilibrio);
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            var doPrimeiro = porModelo[Modelos[0]];
            EstadoDeLevantar(ctrl, doPrimeiro);
            AssetDatabase.SaveAssets();
            for (int i = 1; i < Modelos.Length; i++)
            {
                string caminho = $"{Arte}/Zombie_{Modelos[i]}.overrideController";
                var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(caminho);
                if (aoc == null) throw new FileNotFoundException("Falta o controlador do Oco", caminho);
                aoc.runtimeAnimatorController = ctrl;
                var trocas = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
                aoc.GetOverrides(trocas);
                for (int k = 0; k < trocas.Count; k++)
                    foreach (var (nome, _) in ClipesDoEquilibrio)
                        if (trocas[k].Key == doPrimeiro[nome])
                            trocas[k] = new KeyValuePair<AnimationClip, AnimationClip>(trocas[k].Key, porModelo[Modelos[i]][nome]);
                aoc.ApplyOverrides(trocas);
                EditorUtility.SetDirty(aoc);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("O levantar numa perna foi gravado em " + string.Join(", ", Modelos) + ".");
        }
    }
}
