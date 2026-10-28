using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As animações de agonia no sol dos Ocos (a etapa 7, a pedido em 28/10/2026: "levantando as duas mãos e colocando
    /// sobre o rosto, outra tentando limpar a pele, alguns se encolhendo, e alguns como está hoje"). Feitas aqui, por
    /// código, nos músculos do esqueleto Humanoid (o mesmo jeito dos clipes do ZombieGaroto), a partir da pose parada dos
    /// zumbis (o primeiro quadro do Idle), e gravadas em cada Oco como as outras (GravarClipes). Só o tronco conta (elas
    /// tocam na camada "Ações", com a máscara do tronco: as pernas continuam andando ou fugindo):
    ///  - <b>AgoniaRosto</b> (2,4 s): curvado, as duas mãos cobrindo o rosto, a cabeça balançando ("não"), as mãos esfregando;
    ///  - <b>AgoniaPele</b> (3 s): os braços cruzados à frente, uma mão esfregando o outro braço (a pele que vira pedra), e
    ///    depois troca de lado, olhando para o braço;
    ///  - <b>AgoniaEncolher</b> (2 s): bem curvado, os ombros encolhidos, os braços apertados contra o peito, tremendo e se
    ///    balançando para a frente e para trás.
    /// As poses foram achadas olhando fotos do Oco_Original posado (os sinais: "Arm Front-Back" negativo leva o braço para a
    /// frente; "Arm Down-Up" abre o braço para o lado; "Forearm Stretch" -1 dobra o cotovelo todo).
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string PastaDaAgonia = PastaOco + "/Agonia";

        // o nome do estado no Animator, o valor do parâmetro "Agonia" (1, 2, 3; 0: como antes, sem agonia)
        static readonly (string nome, bool loop)[] ClipesDaAgonia =
        {
            ("AgoniaRosto", true), ("AgoniaPele", true), ("AgoniaEncolher", true),
        };

        const float QuadrosPorSegundo = 30f;

        static bool EhMusculoDeDedo(string n) =>
            n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little");

        /// <summary>A pose parada dos zumbis (o Idle no quadro 0), nos músculos, e o corpo (para as curvas da raiz).</summary>
        static HumanPose PoseParada(string fbxDoOco, AnimationClip idle)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxDoOco).OfType<Avatar>().First(a => a.isHuman);
            var inst = (GameObject)UnityEngine.Object.Instantiate(Carregar<GameObject>(fbxDoOco));
            try
            {
                var an = inst.GetComponent<Animator>();
                if (an == null) an = inst.AddComponent<Animator>();
                an.avatar = avatar;
                idle.SampleAnimation(inst, 0f);
                var h = new HumanPoseHandler(avatar, inst.transform);
                var pose = new HumanPose();
                h.GetHumanPose(ref pose);
                h.Dispose();
                return pose;
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }

        /// <summary>Os 3 clipes Humanoid da agonia (criados ou refeitos em Oco/Agonia/), pelo nome do estado.</summary>
        static Dictionary<string, AnimationClip> ClipesHumanoidesDaAgonia(string fbxDoOco, AnimationClip idle)
        {
            Directory.CreateDirectory(PastaDaAgonia);
            var parada = PoseParada(fbxDoOco, idle);
            var saida = new Dictionary<string, AnimationClip>();
            saida["AgoniaRosto"] = ClipeDeMusculos("AgoniaRosto", 2.4f, parada, Rosto);
            saida["AgoniaPele"] = ClipeDeMusculos("AgoniaPele", 3f, parada, Pele);
            saida["AgoniaEncolher"] = ClipeDeMusculos("AgoniaEncolher", 2f, parada, Encolher);
            AssetDatabase.SaveAssets();
            return saida;
        }

        static float Onda(float t, float periodo, float fase = 0f) => Mathf.Sin(2f * Mathf.PI * t / periodo + fase);

        // o corpo curvado, comum às três
        static void Curvo(Dictionary<string, float> m, float t)
        {
            m["Spine Front-Back"] = -0.45f;
            m["Chest Front-Back"] = -0.35f;
            m["Neck Nod Down-Up"] = -0.4f;
            m["Head Nod Down-Up"] = -0.45f;
            m["Head Tilt Left-Right"] = 0f;
            Dois(m, "Shoulder Down-Up", 0.25f);
        }

        static void Dois(Dictionary<string, float> m, string musculo, float valor)
        {
            m["Left " + musculo] = valor;
            m["Right " + musculo] = valor;
        }

        // as mãos no rosto: a cabeça balança ("não"), o peito arfa, as mãos esfregam o rosto, os ombros tremem
        static void Rosto(Dictionary<string, float> m, float t)
        {
            Curvo(m, t);
            Dois(m, "Arm Down-Up", -0.1f);
            Dois(m, "Arm Front-Back", -1f);
            Dois(m, "Forearm Stretch", -1f);
            m["Left Arm Twist In-Out"] = 0.3f + 0.08f * Onda(t, 0.6f);
            m["Right Arm Twist In-Out"] = 0.3f + 0.08f * Onda(t, 0.6f, Mathf.PI);
            m["Head Turn Left-Right"] = 0.22f * Onda(t, 1.2f);
            m["Neck Turn Left-Right"] = 0.1f * Onda(t, 1.2f, 0.4f);
            m["Spine Left-Right"] = 0.08f * Onda(t, 2.4f);
            m["Spine Twist Left-Right"] = 0.06f * Onda(t, 1.2f, 1f);
            m["Chest Front-Back"] = -0.35f + 0.08f * Onda(t, 1.2f, 2f);
            Dois(m, "Shoulder Down-Up", 0.25f + 0.05f * Onda(t, 0.3f));
        }

        // limpando a pele: a direita esfrega o braço esquerdo (de 0 a 1,2 s), cruza, e a esquerda esfrega o direito (de 1,5
        // a 2,7 s), olhando para o braço
        static void Pele(Dictionary<string, float> m, float t)
        {
            Curvo(m, t);
            float lado = Lado(t);   // 0: a direita esfrega; 1: a esquerda esfrega
            float esfrega = Onda(t, 3f / 7f);   // 7 idas e voltas no laço
            float esfrega2 = Onda(t, 3f / 7f, 0.6f);
            // o braço que é esfregado: à frente, quase esticado; o que esfrega: cruzado por cima, indo e voltando
            void Braco(string s, bool esfregando, float peso)
            {
                float du = -0.35f, fb = -0.75f;
                float tw = esfregando ? -0.7f : -0.2f;
                float fs = esfregando ? -0.45f : -0.1f;
                if (esfregando)
                {
                    fb += 0.13f * esfrega;
                    fs += 0.22f * esfrega2;
                }
                Misturar(m, s + " Arm Down-Up", du, peso);
                Misturar(m, s + " Arm Front-Back", fb, peso);
                Misturar(m, s + " Arm Twist In-Out", tw, peso);
                Misturar(m, s + " Forearm Stretch", fs, peso);
            }
            Zerar(m, "Left Arm Down-Up", "Left Arm Front-Back", "Left Arm Twist In-Out", "Left Forearm Stretch",
                  "Right Arm Down-Up", "Right Arm Front-Back", "Right Arm Twist In-Out", "Right Forearm Stretch");
            Braco("Right", true, 1 - lado);
            Braco("Left", false, 1 - lado);
            Braco("Left", true, lado);
            Braco("Right", false, lado);
            m["Head Turn Left-Right"] = Mathf.Lerp(-0.35f, 0.35f, lado) + 0.04f * Onda(t, 0.75f);
            m["Spine Twist Left-Right"] = Mathf.Lerp(-0.12f, 0.12f, lado);
            m["Head Nod Down-Up"] = -0.45f + 0.05f * Onda(t, 1.5f);
            Dois(m, "Shoulder Down-Up", 0.25f + 0.04f * Onda(t, 3f / 7f, 1f));
        }

        static float Lado(float t)
        {
            if (t < 1.2f) return 0f;
            if (t < 1.5f) return Mathf.SmoothStep(0f, 1f, (t - 1.2f) / 0.3f);
            if (t < 2.7f) return 1f;
            return Mathf.SmoothStep(1f, 0f, (t - 2.7f) / 0.3f);
        }

        static void Zerar(Dictionary<string, float> m, params string[] musculos)
        {
            foreach (var n in musculos) m[n] = 0f;
        }

        static void Misturar(Dictionary<string, float> m, string musculo, float valor, float peso) => m[musculo] += valor * peso;

        // encolhido: bem curvado, os ombros para cima, os braços apertados no peito; tremendo e se balançando
        static void Encolher(Dictionary<string, float> m, float t)
        {
            Curvo(m, t);
            m["Spine Front-Back"] = -0.75f + 0.08f * Onda(t, 2f);
            m["Chest Front-Back"] = -0.6f + 0.04f * Onda(t, 0.2f, 1f);
            m["Spine Twist Left-Right"] = 0.05f * Onda(t, 2f, 0.5f);
            m["Head Tilt Left-Right"] = 0.1f * Onda(t, 2f, 1.3f);
            Dois(m, "Shoulder Front-Back", -0.4f);
            m["Left Shoulder Down-Up"] = 0.55f + 0.05f * Onda(t, 1f / 6f);
            m["Right Shoulder Down-Up"] = 0.55f + 0.05f * Onda(t, 1f / 6f, 1.1f);
            Dois(m, "Arm Down-Up", -0.3f);
            Dois(m, "Arm Front-Back", -0.75f);
            Dois(m, "Arm Twist In-Out", -0.7f);
            m["Left Forearm Stretch"] = -0.95f + 0.03f * Onda(t, 0.2f);
            m["Right Forearm Stretch"] = -0.95f + 0.03f * Onda(t, 0.2f, 1.7f);
        }

        /// <summary>
        /// Um clipe Humanoid: todos os músculos do corpo (menos os dedos) em quadros de 1/30 s, partindo da pose parada e
        /// mudados pela pose da agonia; a raiz parada. Em laço: as ondas fecham um número inteiro de voltas na duração.
        /// </summary>
        static AnimationClip ClipeDeMusculos(string nome, float duracao, HumanPose parada, Action<Dictionary<string, float>, float> pose)
        {
            string caminho = $"{PastaDaAgonia}/{nome}.anim";
            var clipe = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
            bool novo = clipe == null;
            if (novo) clipe = new AnimationClip();
            clipe.ClearCurves();
            clipe.name = nome;
            clipe.frameRate = QuadrosPorSegundo;
            var nomes = HumanTrait.MuscleName;
            int quadros = Mathf.RoundToInt(duracao * QuadrosPorSegundo);
            var valores = new Dictionary<string, float[]>();
            var m = new Dictionary<string, float>();
            for (int q = 0; q <= quadros; q++)
            {
                float t = q / QuadrosPorSegundo;
                m.Clear();
                for (int i = 0; i < nomes.Length; i++) if (!EhMusculoDeDedo(nomes[i])) m[nomes[i]] = parada.muscles[i];
                pose(m, t);
                foreach (var kv in m)
                {
                    if (!valores.TryGetValue(kv.Key, out var arr)) valores[kv.Key] = arr = new float[quadros + 1];
                    arr[q] = kv.Value;
                }
            }
            foreach (var kv in valores)
                AnimationUtility.SetEditorCurve(clipe, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), Curva(kv.Value));
            // a raiz parada, na pose parada
            void Fixo(string p, float v) => AnimationUtility.SetEditorCurve(clipe, EditorCurveBinding.FloatCurve("", typeof(Animator), p),
                AnimationCurve.Constant(0f, duracao, v));
            Fixo("RootT.x", parada.bodyPosition.x); Fixo("RootT.y", parada.bodyPosition.y); Fixo("RootT.z", parada.bodyPosition.z);
            Fixo("RootQ.x", parada.bodyRotation.x); Fixo("RootQ.y", parada.bodyRotation.y); Fixo("RootQ.z", parada.bodyRotation.z); Fixo("RootQ.w", parada.bodyRotation.w);
            var ajustes = AnimationUtility.GetAnimationClipSettings(clipe);
            ajustes.loopTime = true;
            ajustes.loopBlend = true;
            ajustes.keepOriginalOrientation = true;
            ajustes.keepOriginalPositionY = true;
            ajustes.keepOriginalPositionXZ = true;
            ajustes.loopBlendOrientation = true;
            ajustes.loopBlendPositionY = true;
            ajustes.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clipe, ajustes);
            if (novo) AssetDatabase.CreateAsset(clipe, caminho);
            else EditorUtility.SetDirty(clipe);
            return clipe;
        }

        // uma curva suave pelos quadros (a tangente pela diferença dos vizinhos)
        static AnimationCurve Curva(float[] v)
        {
            var chaves = new Keyframe[v.Length];
            float dt = 1f / QuadrosPorSegundo;
            for (int i = 0; i < v.Length; i++)
            {
                float antes = v[Mathf.Max(0, i - 1)], depois = v[Mathf.Min(v.Length - 1, i + 1)];
                float tan = (depois - antes) / (dt * (Mathf.Min(v.Length - 1, i + 1) - Mathf.Max(0, i - 1)));
                chaves[i] = new Keyframe(i * dt, v[i], tan, tan);
            }
            return new AnimationCurve(chaves);
        }
    }
}
