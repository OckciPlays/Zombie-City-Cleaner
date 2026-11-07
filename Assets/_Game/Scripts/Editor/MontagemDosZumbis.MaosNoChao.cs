using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As mãos que não entram no chão (05/11/2026, com o golpe no chão do Brutamonte: "bater no chão com os dois punhos").
    /// A Furia é feita no esqueleto do Oco e tocada no de cada zumbi pelo Humanoid, que guarda os ângulos e não as posições:
    /// os Brutamontes têm os braços mais compridos (para o tamanho deles) e as mãos afundavam no chão no impacto (11 a 14 cm
    /// no NovoBrutamonte, ~35 cm no Brutamonte_V1). Na gravação (<see cref="Gravar"/>), depois de cada quadro dos clipes de
    /// <see cref="ClipesComAsMaosNoChao"/>, a mão que passou do chão sobe até encostar nele: um IK de dois ossos (o braço e o
    /// antebraço) leva o pulso para cima e o cotovelo dobra para fora (o lado em que ele já dobrava), como o gorila batendo.
    /// O chão é a altura da raiz do modelo (os pés estão nela); a parte mais baixa da mão sai dos vértices dela (os presos à
    /// mão e aos dedos), no espaço de cada osso.
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        static readonly HashSet<string> ClipesComAsMaosNoChao = new HashSet<string> { "Furia" };

        sealed class MaosNoChao
        {
            const float Folga = 0.01f;   // a mão para 1 cm acima do chão (a sombra encosta)

            struct Lado
            {
                public Transform braco, antebraco, mao;
                public Transform[] ossos;     // de cada ponto da mão: o osso dele
                public Vector3[] pontos;      // e o ponto no espaço do osso
                public Vector3 fora;          // para onde o cotovelo dobra, se o braço estiver reto (no espaço da raiz)
            }

            readonly Transform _raiz;
            readonly Lado[] _lados;

            MaosNoChao(Transform raiz, Lado[] lados)
            {
                _raiz = raiz;
                _lados = lados;
            }

            /// <summary>
            /// As duas mãos do modelo (null se faltar algum osso do braço). Os vértices vêm da malha inteira que a montagem
            /// fez (Malhas/&lt;modelo&gt;_Inteira: a do FBX não é legível), com os mesmos ossos e bindposes do FBX.
            /// </summary>
            public static MaosNoChao De(GameObject inst, SkinnedMeshRenderer smr, string nomeDoModelo)
            {
                var malha = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>($"{Malhas}/{nomeDoModelo}_Inteira.asset");
                if (malha == null || !malha.isReadable) malha = smr.sharedMesh;
                if (malha == null || !malha.isReadable || malha.bindposes.Length != smr.bones.Length) return null;
                var ossos = smr.bones;
                var bind = malha.bindposes;
                var lados = new Lado[2];
                for (int k = 0; k < 2; k++)
                {
                    string s = k == 0 ? "L" : "R";
                    var l = new Lado
                    {
                        braco = Achar(inst.transform, "upper_arm." + s),
                        antebraco = Achar(inst.transform, "forearm." + s),
                        mao = Achar(inst.transform, "hand." + s),
                        fora = new Vector3(k == 0 ? -1f : 1f, 0.4f, 0.3f),
                    };
                    if (l.braco == null || l.antebraco == null || l.mao == null) return null;
                    // os pontos da mão: os vértices cujo osso mais forte é a mão ou um osso embaixo dela (os dedos)
                    var deQuem = new List<Transform>();
                    var onde = new List<Vector3>();
                    var vertices = malha.vertices;
                    var pesos = malha.boneWeights;
                    for (int v = 0; v < vertices.Length && v < pesos.Length; v++)
                    {
                        int i = pesos[v].boneIndex0;
                        if (pesos[v].weight0 < 0.5f || i < 0 || i >= ossos.Length || ossos[i] == null) continue;
                        if (ossos[i] != l.mao && !ossos[i].IsChildOf(l.mao)) continue;
                        deQuem.Add(ossos[i]);
                        onde.Add(bind[i].MultiplyPoint3x4(vertices[v]));
                    }
                    if (onde.Count == 0) return null;
                    l.ossos = deQuem.ToArray();
                    l.pontos = onde.ToArray();
                    lados[k] = l;
                }
                return new MaosNoChao(inst.transform, lados);
            }

            static Transform Achar(Transform raiz, string nome)
            {
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                    if (t.name == nome) return t;
                return null;
            }

            /// <summary>Sobe a mão que passou do chão (duas voltas: o giro do antebraço muda a ponta da mão).</summary>
            public void Aplicar()
            {
                float chao = _raiz.position.y + Folga;
                for (int k = 0; k < _lados.Length; k++)
                    for (int volta = 0; volta < 2; volta++)
                    {
                        float falta = chao - MaisBaixo(_lados[k]);
                        if (falta <= 0.002f) break;
                        Ik(_lados[k], _lados[k].mao.position + Vector3.up * falta);
                    }
            }

            static float MaisBaixo(in Lado l)
            {
                float menor = float.MaxValue;
                for (int i = 0; i < l.pontos.Length; i++)
                    menor = Mathf.Min(menor, l.ossos[i].localToWorldMatrix.MultiplyPoint3x4(l.pontos[i]).y);
                return menor;
            }

            /// <summary>O IK de dois ossos: o pulso no alvo, o cotovelo no plano em que ele já estava.</summary>
            void Ik(in Lado l, Vector3 alvo)
            {
                Vector3 a = l.braco.position, b = l.antebraco.position, c = l.mao.position;
                float la = (b - a).magnitude, lb = (c - b).magnitude;
                Vector3 para = alvo - a;
                float d = Mathf.Clamp(para.magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
                Vector3 n = para.normalized;
                Vector3 polo = (b - a) - n * Vector3.Dot(b - a, n);
                if (polo.sqrMagnitude < 1e-6f)
                {
                    polo = _raiz.TransformDirection(l.fora);
                    polo -= n * Vector3.Dot(polo, n);
                }
                polo.Normalize();
                float cos = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
                Vector3 cotovelo = a + n * (la * cos) + polo * (la * Mathf.Sqrt(1f - cos * cos));
                l.braco.rotation = Quaternion.FromToRotation(b - a, cotovelo - a) * l.braco.rotation;
                b = l.antebraco.position;
                c = l.mao.position;
                Vector3 ponta = a + n * d;
                l.antebraco.rotation = Quaternion.FromToRotation(c - b, ponta - b) * l.antebraco.rotation;
            }
        }
    }
}
