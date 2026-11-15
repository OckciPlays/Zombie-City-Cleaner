using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os chefes no jogo (15/11/2026, a pedido: "IA e habilidades"; o comportamento em Zumbi.Chefes.cs). Menu Zombie City
    /// Cleaner > Chefes > Montar os chefes no jogo: para cada um, o prefab Zumbi_Chefe_&lt;Nome&gt; em
    /// Prefabs/Chefes/Resources/Chefes, feito do Chefe_&lt;Nome&gt; da vitrine (o mesmo Modelo, com o Animator e as animações) e
    /// com o que um zumbi do jogo tem: o Zumbi (o tipo Chefe e qual deles), o agente de navegação, a cápsula (que segura o
    /// jogador e é a hitbox do nível médio), o corpo cinemático e as hitboxes presas nos ossos (cápsulas de osso a osso e
    /// esferas na cabeça e no ponto fraco, com o raio medido na malha: cada vértice conta para a hitbox do osso dele, ou do
    /// osso de cima mais perto que tem uma; assim os Ocos fundidos no tronco engordam a hitbox do tronco). Nenhuma tem
    /// membro (nada cai: os chefes não são cortados).
    /// <para>
    /// Eles não entram na lista da cena: o ZombieManager os põe no fim da lista dos modelos ao começar a partida (de
    /// Resources/Chefes, na ordem de <see cref="QualChefe"/>; ZombieManager.Chefes.cs), porque as montagens dos zumbis
    /// refazem a lista da cena Mundo e da do Lakewood. Rode de novo depois de montar os chefes da vitrine (o Modelo vem de
    /// lá). Só os prefabs dos chefes são salvos; nenhuma cena é aberta.
    /// </para>
    /// </summary>
    public static partial class MontagemDosChefes
    {
        const string PastaDoJogo = "Assets/_Game/Prefabs/Chefes/Resources/Chefes";
        const string Carne = "Assets/_Game/Art/Characters/Zombie/Materials/Zombie_Flesh.mat";

        static readonly (string nome, QualChefe qual)[] NoJogo =
        {
            ("O_Blindado", QualChefe.Blindado), ("Agregado_Medio_A", QualChefe.AgregadoA), ("Agregado_Medio_B", QualChefe.AgregadoB),
            ("O_Alongado", QualChefe.Alongado), ("O_Longo", QualChefe.Longo), ("O_Condutor", QualChefe.Condutor),
        };

        [MenuItem("Zombie City Cleaner/Chefes/Montar os chefes no jogo")]
        public static void MontarNoJogo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os chefes no jogo.");
                return;
            }
            Directory.CreateDirectory(PastaDoJogo);
            AssetDatabase.Refresh();
            int feitos = 0;
            foreach (var (nome, qual) in NoJogo)
            {
                try
                {
                    var z = MontarChefeNoJogo(nome, qual, out string resumo);
                    if (z == null) continue;
                    feitos++;
                    Debug.Log($"Chefe no jogo: {nome} ({qual}) → {AssetDatabase.GetAssetPath(z)}: {resumo}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Chefe no jogo {nome}: a montagem parou: {e.Message}");
                    Debug.LogException(e);
                }
            }
            Debug.Log($"Chefes no jogo: {feitos} de {NoJogo.Length} em {PastaDoJogo}. O ZombieManager os põe no fim da lista ao começar a partida (F1 > Chefes para testar).");
        }

        // ------------------------------------------------------------------ um chefe

        struct Peca
        {
            public Transform osso;      // o osso que a move (e que dá nome a ela)
            public Transform ate;       // o fim da cápsula (null: a esfera, ou a ponta medida)
            public Vector3 ponta;       // sem 'ate': o fim da cápsula (a mão, o pé), ou o centro da esfera (calculado)
            public bool esfera;
            public RegiaoDoCorpo regiao;
        }

        static Zumbi MontarChefeNoJogo(string nome, QualChefe qual, out string resumo)
        {
            resumo = "";
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosPrefabs}/Chefe_{nome}.prefab");
            if (visual == null)
            {
                Debug.LogError($"Chefe no jogo {nome}: falta o {PastaDosPrefabs}/Chefe_{nome}.prefab (rode Chefes > Montar os chefes (vitrine) antes).");
                return null;
            }
            var raiz = new GameObject($"Zumbi_Chefe_{nome}") { layer = Zumbi.CamadaZumbi };
            try
            {
                var copia = (GameObject)PrefabUtility.InstantiatePrefab(visual);
                PrefabUtility.UnpackPrefabInstance(copia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var modelo = copia.transform.Find("Modelo");
                if (modelo == null) throw new System.Exception($"o Chefe_{nome} não tem o filho Modelo.");
                modelo.SetParent(raiz.transform, false);
                Object.DestroyImmediate(copia);
                var animador = modelo.GetComponent<Animator>();
                if (animador == null || animador.runtimeAnimatorController == null) throw new System.Exception("o Modelo está sem o Animator com o controlador.");
                animador.applyRootMotion = false;
                animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // (o Zumbi troca pelo nível; perto, o chefe anima sempre)
                animador.keepAnimatorStateOnDisable = true;
                var smr = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh != null ? s.sharedMesh.vertexCount : 0).First();
                smr.updateWhenOffscreen = false;
                smr.skinnedMotionVectors = false;
                var mat = smr.sharedMaterial;

                // a malha na pose de repouso, no mundo (a raiz na origem): a altura e os vértices de cada osso
                var (pontos, dono) = VerticesPorOsso(smr);
                float altura = pontos.Count > 0 ? pontos.Max(p => p.y) - pontos.Min(p => p.y) : 2f;

                var z = raiz.AddComponent<Zumbi>();
                var agente = raiz.AddComponent<NavMeshAgent>();
                agente.enabled = false;   // o anfitrião liga perto de alguém; nas cópias fica desligado
                agente.agentTypeID = 0;
                agente.radius = qual == QualChefe.Blindado ? 1.2f : qual == QualChefe.Condutor ? 1.3f : qual == QualChefe.Alongado ? 1f : qual == QualChefe.Longo ? 0.8f : 0.7f;
                agente.height = Mathf.Max(1.8f, altura);
                agente.baseOffset = 0f;
                agente.speed = 1.5f;
                agente.angularSpeed = 140f;
                agente.acceleration = 6f;
                agente.stoppingDistance = 1f;
                agente.autoBraking = true;
                agente.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                agente.avoidancePriority = 5;
                var corpo = raiz.AddComponent<Rigidbody>();
                corpo.isKinematic = true;
                corpo.useGravity = false;
                var voz = raiz.AddComponent<AudioSource>();
                voz.playOnAwake = false;
                voz.spatialBlend = 1f;
                voz.rolloffMode = AudioRolloffMode.Linear;
                voz.minDistance = 3f;
                voz.maxDistance = 60f;
                voz.dopplerLevel = 0f;

                // as hitboxes
                var ossos = smr.bones;
                Transform Osso(string n) => ossos.FirstOrDefault(t => t != null && t.name == n);
                var pecas = PecasDoChefe(ossos, qual);
                var porPeca = VerticesPorPeca(pecas, pontos, dono, ossos);
                var hitboxes = new List<Hitbox>();
                var raios = new List<float>();
                foreach (var p in pecas)
                {
                    var (h, r) = NovaPeca(z, p, porPeca.TryGetValue(p.osso, out var meus) ? meus : new List<Vector3>(), altura);
                    if (h == null) continue;
                    hitboxes.Add(h);
                    if (p.regiao == RegiaoDoCorpo.Tronco) raios.Add(r);
                }
                // a cápsula da raiz: segura o jogador (no nível Perto) e é o corpo inteiro no nível Médio
                var capsula = raiz.AddComponent<CapsuleCollider>();
                float raioDaCapsula = Mathf.Clamp(raios.Count > 0 ? raios.Average() : altura * 0.12f, 0.4f, Mathf.Max(0.5f, altura * 0.22f));
                capsula.radius = raioDaCapsula;
                capsula.height = Mathf.Max(altura, raioDaCapsula * 2f + 0.1f);
                capsula.center = new Vector3(0f, capsula.height / 2f, 0f);
                var hitboxCorpo = raiz.AddComponent<Hitbox>();
                hitboxCorpo.zumbi = z;
                hitboxCorpo.regiao = RegiaoDoCorpo.Corpo;
                hitboxCorpo.colisor = capsula;

                // o Zumbi
                z.modelo = 0;   // (o ZombieManager dá o número: o lugar dele na lista, com os chefes no fim)
                z.tipo = TipoDeZumbi.Chefe;
                z.qualChefe = qual;
                z.alturaDoChefe = altura;
                z.animador = animador;
                z.agente = agente;
                z.capsula = capsula;
                z.hitboxes = hitboxes.ToArray();
                z.renderers = new Renderer[] { smr };
                z.voz = voz;
                z.raizDoModelo = modelo;
                z.esqueleto = ossos;
                z.malhaInteira = null;   // (sem os cortes: nada cai)
                z.malhasDeLonge = new Mesh[0];
                z.materialPele = mat;
                z.materialCarne = AssetDatabase.LoadAssetAtPath<Material>(Carne);
                z.partes = PartesDoChefe(ossos);
                z.maoQueAgarra = qual switch
                {
                    QualChefe.Blindado => Osso("braco_R_mao"),
                    QualChefe.AgregadoA => Osso("braco_carne_R_mao"),
                    QualChefe.AgregadoB => Osso("braco_carne_L_mao"),
                    QualChefe.Alongado => Osso("braco_longo_L_mao"),
                    _ => null,
                };
                z.maoDoGolpe = qual switch
                {
                    QualChefe.Blindado => Osso("braco_L_mao"),
                    QualChefe.AgregadoA => Osso("braco_L_mao"),
                    QualChefe.AgregadoB => Osso("braco_R_mao"),
                    QualChefe.Alongado => Osso("braco_longo_R_mao"),
                    QualChefe.Condutor => Osso("braco_L_mao"),
                    _ => null,
                };
                z.outraMao = qual == QualChefe.Blindado ? Osso("braco_L_mao") : null;
                z.bocaDoChefe = Osso("mandibula") ?? Osso("cabeca");
                z.cabecaDoChefe = Osso("cabeca");

                var faltam = new List<string>();
                if (z.partes.Any(t => t == null)) faltam.Add("partes");
                if (qual != QualChefe.Longo && qual != QualChefe.Condutor && z.maoQueAgarra == null) faltam.Add("a mão que agarra");
                if (qual != QualChefe.Longo && z.maoDoGolpe == null) faltam.Add("a mão do golpe");
                if (faltam.Count > 0) Debug.LogWarning($"Chefe no jogo {nome}: não achei {string.Join(", ", faltam)} (os ossos do chefe_jogo.py mudaram?).");

                string caminho = $"{PastaDoJogo}/Zumbi_Chefe_{nome}.prefab";
                var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, caminho, out bool ok);
                if (!ok || salvo == null) throw new IOException($"não consegui salvar {caminho}.");
                int fracos = hitboxes.Count(h => h.regiao == RegiaoDoCorpo.Papo), cabecas = hitboxes.Count(h => h.regiao == RegiaoDoCorpo.Cabeca);
                resumo = $"{altura:F2} m, {hitboxes.Count} hitboxes ({cabecas} cabeça, {fracos} ponto fraco), cápsula de raio {raioDaCapsula:F2} m";
                return salvo.GetComponent<Zumbi>();
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        /// <summary>Os vértices da malha na pose de repouso (no mundo) e o osso mais forte de cada um.</summary>
        static (List<Vector3> pontos, List<int> dono) VerticesPorOsso(SkinnedMeshRenderer smr)
        {
            var temp = new Mesh();
            smr.BakeMesh(temp, true);
            var m = smr.transform.localToWorldMatrix;
            var verts = temp.vertices;
            var pesos = smr.sharedMesh.boneWeights;
            var pontos = new List<Vector3>(verts.Length);
            var dono = new List<int>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                pontos.Add(m.MultiplyPoint3x4(verts[i]));
                dono.Add(i < pesos.Length ? pesos[i].boneIndex0 : -1);   // (o peso 0 é o maior)
            }
            Object.DestroyImmediate(temp);
            return (pontos, dono);
        }

        static readonly Regex Pescoco = new Regex(@"(^|_)pescoco(_\d+)?$");
        static readonly Regex SegmentoDoBraco = new Regex(@"^braco_.*_(ombro|\d\d)$");

        /// <summary>As peças de hitbox do chefe, pelos nomes dos ossos (os do chefe_jogo.py).</summary>
        static List<Peca> PecasDoChefe(Transform[] ossos, QualChefe qual)
        {
            var pecas = new List<Peca>();
            var porNome = new Dictionary<string, Transform>();
            foreach (var o in ossos)
                if (o != null && !porNome.ContainsKey(o.name)) porNome[o.name] = o;
            Transform O(string n) => porNome.TryGetValue(n, out var t) ? t : null;
            Transform Filho(Transform o, System.Func<string, bool> qualFilho)
            {
                foreach (Transform f in o)
                    if (porNome.ContainsKey(f.name) && qualFilho(f.name)) return f;
                return null;
            }
            void Capsula(Transform de, Transform ate, RegiaoDoCorpo r)
            {
                if (de != null && ate != null) pecas.Add(new Peca { osso = de, ate = ate, regiao = r });
            }
            void Esfera(Transform o, RegiaoDoCorpo r)
            {
                if (o != null) pecas.Add(new Peca { osso = o, esfera = true, regiao = r });
            }
            // o tronco
            Capsula(O("pelvis"), O("lombar"), RegiaoDoCorpo.Tronco);
            Capsula(O("lombar"), O("peito"), RegiaoDoCorpo.Tronco);
            Capsula(O("peito"), O("pescoco") ?? O("pescoco_1"), RegiaoDoCorpo.Tronco);
            // os pescoços (o do Longo em S, os dos Longs fundidos nele)
            foreach (var o in ossos)
            {
                if (o == null || !Pescoco.IsMatch(o.name)) continue;
                var f = Filho(o, n => Pescoco.IsMatch(n) || n.EndsWith("cabeca"));
                Capsula(o, f, RegiaoDoCorpo.Tronco);
            }
            // as cabeças (a dele e as dos Longs fundidos no Longo)
            Esfera(O("cabeca"), RegiaoDoCorpo.Cabeca);
            if (qual == QualChefe.Longo)
            {
                Esfera(O("long2_cabeca"), RegiaoDoCorpo.Cabeca);
                Esfera(O("long3_cabeca"), RegiaoDoCorpo.Cabeca);
                Esfera(O("garganta"), RegiaoDoCorpo.Papo);   // o papo do ácido: o ponto fraco
            }
            if (qual == QualChefe.Condutor) Esfera(O("mandibula"), RegiaoDoCorpo.Papo);   // o rosto humano no peito
            // os braços: osso a osso, até a mão (a mão até a ponta, na direção do antebraço)
            foreach (var o in ossos)
            {
                if (o == null || o.name.Contains("dedo")) continue;
                if (SegmentoDoBraco.IsMatch(o.name))
                {
                    var f = Filho(o, n => n.StartsWith("braco_") && !n.Contains("dedo"));
                    Capsula(o, f, RegiaoDoCorpo.Braco);
                }
                else if (o.name.StartsWith("braco_") && o.name.EndsWith("_mao") && o.parent != null)
                {
                    Vector3 dir = o.position - o.parent.position;
                    float l = dir.magnitude;
                    if (l > 0.01f) pecas.Add(new Peca { osso = o, ponta = o.position + dir / l * Mathf.Max(0.15f, l * 0.45f), regiao = RegiaoDoCorpo.Braco });
                }
            }
            // as pernas
            foreach (var lado in new[] { "L", "R" })
            {
                Capsula(O("coxa_" + lado), O("canela_" + lado), RegiaoDoCorpo.Perna);
                Capsula(O("canela_" + lado), O("pe_" + lado), RegiaoDoCorpo.Perna);
                Esfera(O("pe_" + lado), RegiaoDoCorpo.Perna);
            }
            return pecas;
        }

        /// <summary>
        /// Uma hitbox no osso: a cápsula de osso a osso (ou até a ponta), com o raio de 70% dos vértices dela; a esfera no
        /// meio dos vértices, com o raio de 80% deles. Cada vértice conta para a peça do osso dele, ou do osso de cima mais
        /// perto que tem uma peça.
        /// </summary>
        static (Hitbox h, float raio) NovaPeca(Zumbi z, Peca p, List<Vector3> meus, float altura)
        {
            var go = new GameObject($"Hitbox {p.regiao} ({p.osso.name})") { layer = Zumbi.CamadaZumbi };
            go.transform.SetParent(p.osso, false);
            var h = go.AddComponent<Hitbox>();
            h.zumbi = z;
            h.regiao = p.regiao;
            h.membro = Membro.Nenhum;
            float escala = Mathf.Max(0.0001f, go.transform.lossyScale.x);
            float raio;
            if (p.esfera)
            {
                Vector3 centro = meus.Count >= 8 ? meus.Aggregate(Vector3.zero, (a, v) => a + v) / meus.Count : p.osso.position;
                raio = meus.Count >= 8 ? Percentil(meus.Select(v => Vector3.Distance(v, centro)), 0.8f) : altura * 0.05f;
                raio = Mathf.Clamp(raio, 0.08f, Mathf.Max(0.2f, altura * 0.12f));
                go.transform.position = centro;
                var esfera = go.AddComponent<SphereCollider>();
                esfera.radius = raio / escala;
                h.colisor = esfera;
                return (h, raio);
            }
            Vector3 de = p.osso.position, ate = p.ate != null ? p.ate.position : p.ponta;
            float l = Vector3.Distance(de, ate);
            if (l < 0.02f)
            {
                Object.DestroyImmediate(go);
                return (null, 0f);
            }
            raio = meus.Count >= 8 ? Percentil(meus.Select(v => DistanciaAoSegmento(v, de, ate)), 0.7f) : l * 0.25f;
            raio = Mathf.Clamp(raio, 0.06f, Mathf.Max(0.2f, altura * 0.18f));
            go.transform.SetPositionAndRotation((de + ate) * 0.5f, Quaternion.FromToRotation(Vector3.up, ate - de));
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = raio / escala;
            c.height = (l + 2f * raio) / escala;
            h.colisor = c;
            return (h, raio);
        }

        /// <summary>
        /// Os vértices de cada peça (pelo osso dela): o de cada vértice é o osso mais forte dele, ou o primeiro de cima que tem
        /// uma peça (os dedos vão para a mão, os Ocos fundidos para o osso do tronco ou do ombro em que estão presos).
        /// </summary>
        static Dictionary<Transform, List<Vector3>> VerticesPorPeca(List<Peca> pecas, List<Vector3> pontos, List<int> dono, Transform[] ossos)
        {
            var comPeca = new HashSet<Transform>(pecas.Select(p => p.osso));
            var donoDoOsso = new Transform[ossos.Length];
            for (int b = 0; b < ossos.Length; b++)
                for (var t = ossos[b]; t != null; t = t.parent)
                    if (comPeca.Contains(t))
                    {
                        donoDoOsso[b] = t;
                        break;
                    }
            var porPeca = new Dictionary<Transform, List<Vector3>>();
            for (int i = 0; i < pontos.Count; i++)
            {
                int b = dono[i];
                if (b < 0 || b >= ossos.Length || donoDoOsso[b] == null) continue;
                if (!porPeca.TryGetValue(donoDoOsso[b], out var l)) porPeca[donoDoOsso[b]] = l = new List<Vector3>();
                l.Add(pontos[i]);
            }
            return porPeca;
        }

        static float Percentil(IEnumerable<float> valores, float p)
        {
            var l = valores.OrderBy(v => v).ToList();
            if (l.Count == 0) return 0f;
            return l[Mathf.Clamp(Mathf.RoundToInt(p * (l.Count - 1)), 0, l.Count - 1)];
        }

        static float DistanciaAoSegmento(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>
        /// As 11 partes na ordem de CorpoDeZumbi.Partes (o quadril, o peito, a cabeça, os braços, as pernas): o Zumbi usa para
        /// a parte que levou o tiro da morte (o chefe não tem o corpo com física, mas a conta é a mesma).
        /// </summary>
        static Transform[] PartesDoChefe(Transform[] ossos)
        {
            Transform O(string n) => ossos.FirstOrDefault(t => t != null && t.name == n);
            Transform Braco(string lado, string seg) => ossos.Where(t => t != null && t.name.StartsWith("braco_") && t.name.Contains($"_{lado}_") && t.name.EndsWith("_" + seg))
                                                            .OrderBy(t => t.name.Contains("carne") || t.name.Contains("long3") ? 1 : 0).FirstOrDefault();
            var peito = O("peito") ?? O("lombar");
            return new[]
            {
                O("pelvis"), peito, O("cabeca") ?? peito,
                Braco("L", "01") ?? peito, Braco("L", "02") ?? peito, Braco("R", "01") ?? peito, Braco("R", "02") ?? peito,
                O("coxa_L"), O("canela_L"), O("coxa_R"), O("canela_R"),
            };
        }
    }
}
