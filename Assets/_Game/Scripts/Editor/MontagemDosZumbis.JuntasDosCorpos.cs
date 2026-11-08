using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As juntas do corpo que cai, acertadas pelas animações do próprio modelo (07/11/2026, com a morte "muito mole"). As
    /// da montagem (<see cref="MontarCorpo"/>) tinham o eixo e os limites escritos à mão, e a física conta o giro ao
    /// contrário do que estava pensado: o joelho dobrava 120° para a frente e só 5° para trás, o quadril, a coluna e o
    /// pescoço também ao contrário, e o cotovelo dobrava no "balanço" (que tinha 5°). Medido nas poses do jogo: o joelho
    /// andando, -27°; correndo, -110°; o cotovelo na agonia, 100° de balanço. Ao morrer, tudo dava um tranco e depois
    /// dobrava para o lado errado.
    /// Agora, para cada junta, as poses de todos os clipes gravados do modelo (o andar, a corrida, o ataque, as agonias, o
    /// rastejar...), tiradas do repouso do corpo, dão o eixo dela (o giro que ela mais faz: o eixo principal dos giros), o
    /// do balanço (o segundo) e os limites: o giro que as animações usam, de um lado e do outro (sem os 2% de cada ponta),
    /// mais uma folga de um quarto do lado (5 a <see cref="FolgaNoGiro"/>), e o balanço que elas usam em cada eixo, mais
    /// <see cref="FolgaNoBalanco"/>. O sentido do giro é o da física (o
    /// contrário do eixo da junta, medido na prévia).
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const float FolgaNoGiro = 20f, FolgaNoBalanco = 15f;

        [MenuItem("Zombie City Cleaner/Zumbis/Acertar as juntas dos corpos (sem remontar)")]
        public static void AcertarAsJuntasDosCorpos()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de acertar as juntas.");
                return;
            }
            var nomes = TodosOsModelos.ToList();
            foreach (var (nome, _) in Novos)
                if (LugarNoJogo(nome) < 0) nomes.Add(nome);
            var linhas = new List<string>();
            foreach (var nome in nomes)
            {
                string caminho = $"{Prefabs}/Corpo_{nome}.prefab";
                if (!File.Exists(caminho)) continue;
                var raiz = PrefabUtility.LoadPrefabContents(caminho);
                try
                {
                    string resumo = CalibrarJuntas(raiz, nome);
                    if (resumo == null) continue;
                    PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                    linhas.Add($"{nome}: {resumo}");
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"As juntas de {linhas.Count} corpos foram acertadas pelas animações:\n{string.Join("\n", linhas)}");
        }

        /// <summary>
        /// Acerta as juntas do corpo (na pose de repouso) pelos clipes gravados do modelo. Null se ele não tem os clipes.
        /// </summary>
        static string CalibrarJuntas(GameObject raiz, string nomeDoModelo)
        {
            string pasta = $"{ClipesDosOcos}/{nomeDoModelo}";
            if (!Directory.Exists(pasta)) return null;
            var clipes = AssetDatabase.FindAssets("t:AnimationClip", new[] { pasta })
                .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToList();
            if (clipes.Count == 0) return null;
            var modelo = raiz.transform.Find("Modelo");
            if (modelo == null) return null;
            var juntas = raiz.GetComponentsInChildren<CharacterJoint>(true);
            var todos = modelo.GetComponentsInChildren<Transform>(true);
            var guardado = todos.Select(t => (t.localPosition, t.localRotation)).ToArray();
            var repouso = juntas.Select(j => Quaternion.Inverse(j.connectedBody.transform.rotation) * j.transform.rotation).ToArray();
            var amostras = juntas.Select(_ => new List<Quaternion>()).ToArray();
            try
            {
                foreach (var c in clipes)
                    for (int k = 0; k <= 12; k++)
                    {
                        c.SampleAnimation(modelo.gameObject, c.length * k / 12f);
                        for (int i = 0; i < juntas.Length; i++)
                        {
                            var j = juntas[i];
                            var agora = Quaternion.Inverse(j.connectedBody.transform.rotation) * j.transform.rotation;
                            amostras[i].Add(Quaternion.Inverse(repouso[i]) * agora);   // do repouso até a pose, no espaço do filho
                        }
                    }
            }
            finally
            {
                for (int i = 0; i < todos.Length; i++) todos[i].SetLocalPositionAndRotation(guardado[i].localPosition, guardado[i].localRotation);
            }
            var resumo = new List<string>();
            for (int i = 0; i < juntas.Length; i++)
            {
                var j = juntas[i];
                var vetores = amostras[i].Select(VetorDoGiro).ToList();
                // o eixo principal (o giro que ela mais faz) e o segundo: a matriz dos momentos dos giros
                var m = new float[3, 3];
                foreach (var v in vetores)
                    for (int a = 0; a < 3; a++)
                        for (int b = 0; b < 3; b++) m[a, b] += v[a] * v[b];
                Vector3 e1 = Principal(m, Vector3.zero, j.axis.normalized);
                Vector3 e2 = Principal(m, e1, j.swingAxis.normalized);
                if (e1.sqrMagnitude < 0.5f || e2.sqrMagnitude < 0.5f) continue;
                Vector3 e3 = Vector3.Cross(e1, e2).normalized;
                var giros = new List<float>();
                var balancos1 = new List<float>();
                var balancos2 = new List<float>();
                foreach (var d in amostras[i])
                {
                    var (giro, balanco) = GiroEBalanco(d, e1);
                    giros.Add(-giro);   // a física conta ao contrário do eixo
                    var s = VetorDoGiro(balanco);
                    balancos1.Add(Mathf.Abs(Vector3.Dot(s, e2)));
                    balancos2.Add(Mathf.Abs(Vector3.Dot(s, e3)));
                }
                // sem os 2% de cada ponta (as poses fora do comum do rastejar e do emergir, e o giro que dá a volta perto
                // de 180°); a folga é um quarto do lado, de 5 a 20° (o joelho e o cotovelo quase não esticam além do reto)
                float min = Mathf.Min(0f, Percentil(giros, 0.02f)), max = Mathf.Max(0f, Percentil(giros, 0.98f));
                float b1 = Percentil(balancos1, 0.98f), b2 = Percentil(balancos2, 0.98f);
                float Folga(float lado) => Mathf.Clamp(Mathf.Abs(lado) * 0.25f, 5f, FolgaNoGiro);
                j.axis = e1;
                j.swingAxis = e2;
                j.lowTwistLimit = new SoftJointLimit { limit = Mathf.Clamp(min - Folga(min), -177f, -5f) };
                j.highTwistLimit = new SoftJointLimit { limit = Mathf.Clamp(max + Folga(max), 5f, 177f) };
                j.swing1Limit = new SoftJointLimit { limit = Mathf.Clamp(b1 + FolgaNoBalanco, 10f, 150f) };
                j.swing2Limit = new SoftJointLimit { limit = Mathf.Clamp(b2 + FolgaNoBalanco, 10f, 150f) };
                resumo.Add($"{j.name} {j.lowTwistLimit.limit:F0}..{j.highTwistLimit.limit:F0} ({j.swing1Limit.limit:F0}/{j.swing2Limit.limit:F0})");
            }
            return string.Join(", ", resumo);
        }

        static float Percentil(List<float> valores, float p)
        {
            if (valores.Count == 0) return 0f;
            var ordem = valores.OrderBy(x => x).ToList();
            return ordem[Mathf.Clamp(Mathf.RoundToInt(p * (ordem.Count - 1)), 0, ordem.Count - 1)];
        }

        /// <summary>O giro como vetor (o eixo vezes o ângulo, em graus; o menor caminho).</summary>
        static Vector3 VetorDoGiro(Quaternion q)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            q.ToAngleAxis(out float graus, out Vector3 eixo);
            if (graus < 1e-3f || float.IsNaN(eixo.x)) return Vector3.zero;
            if (graus > 180f) graus -= 360f;
            return eixo.normalized * graus;
        }

        /// <summary>O giro em volta do eixo (graus, no sentido do eixo) e o que sobra (o balanço).</summary>
        static (float giro, Quaternion balanco) GiroEBalanco(Quaternion d, Vector3 eixo)
        {
            var v = new Vector3(d.x, d.y, d.z);
            Vector3 p = eixo * Vector3.Dot(v, eixo);
            var giro = new Quaternion(p.x, p.y, p.z, d.w);
            float norma = Mathf.Sqrt(giro.x * giro.x + giro.y * giro.y + giro.z * giro.z + giro.w * giro.w);
            if (norma < 1e-6f) return (0f, d);
            giro = new Quaternion(giro.x / norma, giro.y / norma, giro.z / norma, giro.w / norma);
            giro.ToAngleAxis(out float graus, out Vector3 ax);
            if (graus > 180f) graus -= 360f;
            if (Vector3.Dot(ax, eixo) < 0f) graus = -graus;
            return (graus, d * Quaternion.Inverse(giro));
        }

        /// <summary>O autovetor maior da matriz simétrica, perpendicular a <paramref name="fora"/> (se dado), pela potência.</summary>
        static Vector3 Principal(float[,] m, Vector3 fora, Vector3 inicio)
        {
            Vector3 v = inicio.sqrMagnitude > 0.5f ? inicio : Vector3.right;
            if (fora.sqrMagnitude > 0.5f) v = (v - fora * Vector3.Dot(v, fora)).normalized;
            if (v.sqrMagnitude < 0.5f) v = Vector3.Cross(fora, Vector3.up).normalized;
            for (int k = 0; k < 60; k++)
            {
                var w = new Vector3(
                    m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z,
                    m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z,
                    m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z);
                if (fora.sqrMagnitude > 0.5f) w -= fora * Vector3.Dot(w, fora);
                if (w.sqrMagnitude < 1e-12f) break;
                v = w.normalized;
            }
            return v;
        }
    }
}
