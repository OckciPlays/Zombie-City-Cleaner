using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta os zumbis (Partes 5 e 6):
    /// - a importação do NovoZombie.fbx (os 9 clipes e os materiais das 19 peças da malha);
    /// - a camada "Zumbi" (7), que não colide com ela mesma nem com os braços de primeira pessoa;
    /// - as malhas: a completa (regiões e tampas separadas, base das variantes sem membros) e a inteira (um submesh só,
    ///   um draw call), com o mapa de qual submesh é cada região e cada tampa, medido pela posição dos ossos;
    /// - o Animator (andar, cair, rastejar, morrer e, só no tronco, atacar e reagir ao tiro);
    /// - o prefab, com agente, cápsula, corpo cinemático, voz, hitboxes nos ossos e os pedaços que caem;
    /// - o jogador com vida (VidaJogador), refazendo o prefab dele;
    /// - na cena, o objeto "Zumbis" (ZombieManager, benchmark e pontos de horda), o sangue no "Efeitos" e o NavMesh;
    /// - os decals (manchas de sangue e furos de tiro) no renderizador.
    /// Menu: Zombie City Cleaner > Montar zumbis. Pode rodar de novo.
    /// </summary>
    public static class MontagemDosZumbis
    {
        const string Arte = "Assets/_Game/Art/Characters/Zombie";
        const string Fbx = Arte + "/NovoZombie.fbx";
        const string FbxPedacos = Arte + "/NovoZombie_Gibs.fbx";
        const string Controlador = Arte + "/Zombie.controller";
        const string MascaraTronco = Arte + "/Zombie_Tronco.mask";
        const string MalhaInteira = Arte + "/Malhas/Zumbi_Inteiro.asset";
        const string MalhaCompleta = Arte + "/Malhas/Zumbi_Completo.asset";
        const string PrefabZumbi = "Assets/_Game/Prefabs/Zumbi.prefab";
        const string ArquivoNavMesh = "Assets/_Game/Scenes/Demo_Cidade/NavMesh da cidade.asset";
        const string PastaDecals = "Assets/_Game/Art/Materials/Decals";
        const string RendererPrincipal = "Assets/Settings/PC_Renderer.asset";

        // nome, centro, raio, quantidade (~130 zumbis; nenhum perto da casa da família)
        static readonly (string nome, Vector3 centro, float raio, int quantidade)[] Hordas =
        {
            ("Cruzamento central", new Vector3(0f, 0f, 0f), 6f, 8),
            ("Rua norte", new Vector3(0f, 0f, 22f), 5f, 6),
            ("Rua sul", new Vector3(0f, 0f, -24f), 5f, 5),
            ("Rua leste", new Vector3(22f, 0f, 0f), 6f, 7),
            ("Rua oeste", new Vector3(-40f, 0f, 12f), 5f, 5),
            ("Praça", new Vector3(24f, 0f, -12f), 6f, 8),
            ("Estacionamento", new Vector3(10f, 0f, 33f), 6f, 7),
            ("Prédio, térreo", new Vector3(20f, 0.15f, 19f), 4f, 4),
            ("Prédio, 1º andar", new Vector3(20f, 3.15f, 21f), 5f, 4),
            ("Prédio, 2º andar", new Vector3(20f, 6.15f, 21f), 5f, 3),
            ("Prédio, 3º andar", new Vector3(20f, 9.15f, 21f), 5f, 3),
            ("Casa 1", new Vector3(-27f, 0.15f, 19f), 2.5f, 3),
            ("Casa 2", new Vector3(-12f, 0.15f, 23f), 2.5f, 3),
            ("Casa 4", new Vector3(13f, 0.15f, -14f), 2.5f, 3),
            ("Casa 5", new Vector3(28f, 0.15f, -26f), 2.5f, 3),
            ("Rua (40, 20)", new Vector3(40f, 0f, 20f), 5f, 5),
            ("Rua (40, -20)", new Vector3(40f, 0f, -20f), 5f, 5),
            ("Rua (20, 40)", new Vector3(20f, 0f, 40f), 5f, 5),
            ("Rua (-20, 40)", new Vector3(-20f, 0f, 40f), 5f, 4),
            ("Rua (-40, 32)", new Vector3(-40f, 0f, 32f), 5f, 4),
            ("Rua (20, -40)", new Vector3(20f, 0f, -40f), 5f, 5),
            ("Rua (-12, -40)", new Vector3(-12f, 0f, -40f), 5f, 4),
            ("Esquina nordeste", new Vector3(40f, 0f, 40f), 5f, 4),
            ("Esquina sudeste", new Vector3(40f, 0f, -40f), 5f, 4),
            ("Esquina sudoeste", new Vector3(-40f, 0f, -40f), 5f, 3),
            ("Saída norte", new Vector3(0f, 0f, 54f), 4f, 4),
            ("Saída leste", new Vector3(54f, 0f, 0f), 4f, 4),
            ("Saída sul", new Vector3(0f, 0f, -54f), 4f, 3),
            ("Saída oeste", new Vector3(-54f, 0f, 0f), 4f, 3),
        };

        // clipe (take do Blender "Zombie_Rig|Zombie_X"), em loop?
        static readonly (string nome, bool loop)[] Clipes =
        {
            ("Idle", true), ("Walk", true), ("Chase", true), ("Attack", false), ("HitReact", false),
            ("Death", false), ("Fall", false), ("Crawl", true), ("CrawlIdle", true),
        };

        [MenuItem("Zombie City Cleaner/Montar zumbis")]
        public static void Montar()
        {
            CamadaDosZumbis();
            var clipes = ImportarZumbi();
            var (inteira, completa, mapa) = MalhasDoZumbi();
            var controlador = ControladorDoZumbi(clipes, MascaraDoTronco());
            var prefab = MontarPrefab(inteira, controlador);
            MontagemDaRede.MontarPrefab();
            var decals = MateriaisDosDecals();
            DecalsNoRenderizador();
            var cena = EditorSceneManager.GetActiveScene();
            MontarCena(cena, prefab, inteira, completa, mapa, decals);
            AssarNavegacao(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Debug.Log("Zumbis montados.");
        }

        static T Carregar<T>(string caminho) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(caminho);
            if (a == null) throw new FileNotFoundException("Não encontrado", caminho);
            return a;
        }

        /// <summary>
        /// As camadas: 7 "Zumbi" (cápsulas e hitboxes), 8 "Pedacos" (os membros caídos) e 9 "Jogador" (a raiz dos jogadores).
        /// Os pedaços só batem no cenário: atravessam zumbis, jogadores e os braços de primeira pessoa.
        /// </summary>
        static void CamadaDosZumbis()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var camadas = tags.FindProperty("layers");
            camadas.GetArrayElementAtIndex(Zumbi.CamadaZumbi).stringValue = "Zumbi";
            camadas.GetArrayElementAtIndex(Sangue.CamadaPedacos).stringValue = "Pedacos";
            camadas.GetArrayElementAtIndex(JogadorRede.CamadaJogador).stringValue = "Jogador";
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, Zumbi.CamadaZumbi, true);
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, true);
            foreach (int outra in new[] { 2, Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, JogadorRede.CamadaJogador })
                Physics.IgnoreLayerCollision(Sangue.CamadaPedacos, outra, true);
        }

        // ------------------------------------------------------------------ importação, malhas e mapa

        /// <summary>Os 9 clipes (renomeados, com loop onde precisa) e os materiais: regiões no Zombie, tampas na carne.</summary>
        static Dictionary<string, AnimationClip> ImportarZumbi()
        {
            var importador = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            var padrao = importador.defaultClipAnimations.ToDictionary(c => c.takeName);
            importador.clipAnimations = Clipes.Select(c =>
            {
                var clipe = padrao["Zombie_Rig|Zombie_" + c.nome];
                clipe.name = c.nome;
                clipe.loopTime = c.loop;
                return clipe;
            }).ToArray();
            var corpo = Carregar<Material>(Arte + "/Materials/Zombie.mat");
            var carne = Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat");
            foreach (var n in new[] { "Zombie_Body", "Zombie_Head", "Zombie_UpperArm.L", "Zombie_Forearm.L", "Zombie_UpperArm.R", "Zombie_Forearm.R",
                                      "Zombie_Thigh.L", "Zombie_Shin.L", "Zombie_Thigh.R", "Zombie_Shin.R" })
                importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), corpo);
            foreach (var n in new[] { "Cap_Neck", "Cap_Elbow.L", "Cap_Shoulder.L", "Cap_Elbow.R", "Cap_Shoulder.R", "Cap_Knee.L", "Cap_Hip.L", "Cap_Knee.R", "Cap_Hip.R" })
                importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), carne);
            importador.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        }

        /// <summary>
        /// A Unity reordena os materiais do FBX, então cada submesh é identificado pela posição: o centro dele (no espaço da
        /// malha) é comparado com pontos esperados nos ossos da bindpose (o meio do antebraço, o joelho...), um por vez.
        /// </summary>
        static (Mesh inteira, Mesh completa, MapaDaMalha mapa) MalhasDoZumbi()
        {
            var smr = Carregar<GameObject>(Fbx).GetComponentInChildren<SkinnedMeshRenderer>();
            var original = smr.sharedMesh;
            var carne = Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat");
            Vector3 J(string osso) => original.bindposes[System.Array.FindIndex(smr.bones, b => b.name == osso)].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 Entre(string a, string b, float t) => Vector3.Lerp(J(a), J(b), t);
            var regioes = new Dictionary<RegiaoDaMalha, Vector3>
            {
                [RegiaoDaMalha.Corpo] = J("chest"),
                [RegiaoDaMalha.Cabeca] = J("head") + (J("head") - J("neck")) * 1.5f,
            };
            var tampas = new Dictionary<Tampa, Vector3> { [Tampa.Pescoco] = Entre("neck", "head", 0.56f) };
            foreach (var (s, e) in new[] { (".L", true), (".R", false) })
            {
                regioes[e ? RegiaoDaMalha.BracoE : RegiaoDaMalha.BracoD] = Entre("upper_arm" + s, "forearm" + s, 0.8f);
                regioes[e ? RegiaoDaMalha.AntebracoE : RegiaoDaMalha.AntebracoD] = J("hand" + s);
                regioes[e ? RegiaoDaMalha.CoxaE : RegiaoDaMalha.CoxaD] = Entre("thigh" + s, "shin" + s, 0.69f);
                regioes[e ? RegiaoDaMalha.CanelaE : RegiaoDaMalha.CanelaD] = Entre("shin" + s, "foot" + s, 0.6f);
                tampas[e ? Tampa.OmbroE : Tampa.OmbroD] = Entre("upper_arm" + s, "forearm" + s, 0.62f);
                tampas[e ? Tampa.CotoveloE : Tampa.CotoveloD] = J("forearm" + s);
                tampas[e ? Tampa.QuadrilE : Tampa.QuadrilD] = Entre("thigh" + s, "shin" + s, 0.38f);
                tampas[e ? Tampa.JoelhoE : Tampa.JoelhoD] = J("shin" + s);
            }
            var submeshesCorpo = new List<int>();
            var submeshesCarne = new List<int>();
            for (int i = 0; i < original.subMeshCount; i++) (smr.sharedMaterials[i] == carne ? submeshesCarne : submeshesCorpo).Add(i);
            var mapa = new MapaDaMalha
            {
                regioes = Casar(original, submeshesCorpo, regioes.ToDictionary(k => (int)k.Key, k => k.Value), 10),
                tampas = Casar(original, submeshesCarne, tampas.ToDictionary(k => (int)k.Key, k => k.Value), 9),
            };
            Debug.Log("Mapa da malha do zumbi: regiões " + string.Join(", ", mapa.regioes.Select((s, i) => $"{(RegiaoDaMalha)i}={s}")) +
                      "; tampas " + string.Join(", ", mapa.tampas.Select((s, i) => $"{(Tampa)i}={s}")));

            Directory.CreateDirectory(Path.GetDirectoryName(MalhaCompleta));
            var completa = Object.Instantiate(original);
            completa.name = "Zumbi_Completo";
            completa = Salvar(completa, MalhaCompleta);
            // a cópia herda o "não legível" do FBX; as variantes são montadas lendo os triângulos dela no jogo
            var legivel = new SerializedObject(completa);
            legivel.FindProperty("m_IsReadable").boolValue = true;
            legivel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(completa);
            var inteira = Object.Instantiate(original);
            inteira.name = "Zumbi_Inteiro";
            var triangulos = new List<int>();
            foreach (int s in mapa.regioes) triangulos.AddRange(original.GetTriangles(s));
            inteira.subMeshCount = 1;
            inteira.SetTriangles(triangulos, 0, false);
            inteira = Salvar(inteira, MalhaInteira);
            return (inteira, completa, mapa);
        }

        // casa cada ponto esperado com o submesh mais perto, do par mais próximo para o mais distante
        static int[] Casar(Mesh malha, List<int> submeshes, Dictionary<int, Vector3> esperados, int quantos)
        {
            if (submeshes.Count != quantos || esperados.Count != quantos)
                throw new System.Exception($"A malha do zumbi tem {submeshes.Count} submeshes deste tipo; eram esperados {quantos}.");
            var pares = new List<(float d, int submesh, int alvo)>();
            foreach (int s in submeshes)
                foreach (var e in esperados)
                    pares.Add(((malha.GetSubMesh(s).bounds.center - e.Value).sqrMagnitude, s, e.Key));
            var resultado = Enumerable.Repeat(-1, quantos).ToArray();
            var usados = new HashSet<int>();
            foreach (var p in pares.OrderBy(p => p.d))
            {
                if (resultado[p.alvo] >= 0 || usados.Contains(p.submesh)) continue;
                resultado[p.alvo] = p.submesh;
                usados.Add(p.submesh);
            }
            return resultado;
        }

        static Mesh Salvar(Mesh nova, string caminho)
        {
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (existente == null)
            {
                AssetDatabase.CreateAsset(nova, caminho);
                return nova;
            }
            EditorUtility.CopySerialized(nova, existente);
            Object.DestroyImmediate(nova);
            return existente;
        }

        // ------------------------------------------------------------------ Animator

        static AvatarMask MascaraDoTronco()
        {
            var mascara = new AvatarMask();
            mascara.AddTransformPath(Carregar<GameObject>(Fbx).transform, true);
            for (int i = 0; i < mascara.transformCount; i++) mascara.SetTransformActive(i, mascara.GetTransformPath(i).Contains("/spine"));
            AssetDatabase.DeleteAsset(MascaraTronco);
            AssetDatabase.CreateAsset(mascara, MascaraTronco);
            return mascara;
        }

        /// <summary>
        /// Camada base: Andar (Idle/Walk/Chase pela Speed) → Cair → Rastejar (CrawlIdle/Crawl), e a morte de pé ou deitado.
        /// Camada "Ações" (só do tronco para cima): Ataque e Tiro (reação), disparados por trigger, voltando ao vazio.
        /// Write Defaults desligado em todos os estados.
        /// </summary>
        static AnimatorController ControladorDoZumbi(Dictionary<string, AnimationClip> c, AvatarMask tronco)
        {
            AssetDatabase.DeleteAsset(Controlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Rastejando", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Morto", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Tiro", AnimatorControllerParameterType.Trigger);

            var baseSm = ctrl.layers[0].stateMachine;
            var andar = Arvore(ctrl, "Andar", (c["Idle"], 0f), (c["Walk"], 0.6f), (c["Chase"], 1.12f));
            var rastejar = Arvore(ctrl, "Rastejar", (c["CrawlIdle"], 0f), (c["Crawl"], 0.45f));
            var cair = Estado(baseSm, "Cair", c["Fall"]);
            var morte = Estado(baseSm, "Morte", c["Death"]);
            // morrendo já deitado: fica no último quadro da morte (de bruços), sem levantar
            var morteDeitado = Estado(baseSm, "Morte deitado", c["Death"]);
            morteDeitado.speed = 0f;
            morteDeitado.cycleOffset = 0.98f;
            baseSm.defaultState = andar;
            var t = andar.AddTransition(cair);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Rastejando"); t.hasExitTime = false; t.duration = 0.1f;
            t = cair.AddTransition(rastejar);
            t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f;
            t = baseSm.AddAnyStateTransition(morte);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Morto"); t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Rastejando");
            t.duration = 0.15f; t.canTransitionToSelf = false;
            t = baseSm.AddAnyStateTransition(morteDeitado);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Morto"); t.AddCondition(AnimatorConditionMode.If, 0f, "Rastejando");
            t.duration = 0.4f; t.canTransitionToSelf = false;

            ctrl.AddLayer("Ações");
            var camadas = ctrl.layers;
            camadas[1].avatarMask = tronco;
            camadas[1].defaultWeight = 1f;
            camadas[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = camadas;
            var acoes = ctrl.layers[1].stateMachine;
            var vazio = acoes.AddState("Vazio");
            vazio.writeDefaultValues = false;
            acoes.defaultState = vazio;
            var ataque = Estado(acoes, "Ataque", c["Attack"]);
            var tiro = Estado(acoes, "Tiro", c["HitReact"]);
            t = acoes.AddAnyStateTransition(ataque);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Atacar"); t.duration = 0.1f; t.canTransitionToSelf = false;
            t = ataque.AddTransition(vazio); t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f;
            t = acoes.AddAnyStateTransition(tiro);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Tiro"); t.duration = 0.05f; t.canTransitionToSelf = true;
            t = tiro.AddTransition(vazio); t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static AnimatorState Arvore(AnimatorController ctrl, string nome, params (Motion clipe, float limite)[] filhos)
        {
            var estado = ctrl.CreateBlendTreeInController(nome, out var arvore, 0);
            arvore.blendType = BlendTreeType.Simple1D;
            arvore.blendParameter = "Speed";
            arvore.useAutomaticThresholds = false;
            foreach (var (clipe, limite) in filhos) arvore.AddChild(clipe, limite);
            estado.writeDefaultValues = false;
            return estado;
        }

        static AnimatorState Estado(AnimatorStateMachine sm, string nome, Motion clipe)
        {
            var e = sm.AddState(nome);
            e.motion = clipe;
            e.writeDefaultValues = false;
            return e;
        }

        // ------------------------------------------------------------------ prefab

        static Zumbi MontarPrefab(Mesh malha, AnimatorController controlador)
        {
            var raiz = new GameObject("Zumbi") { layer = Zumbi.CamadaZumbi };
            var z = raiz.AddComponent<Zumbi>();
            var agente = raiz.AddComponent<NavMeshAgent>();
            agente.enabled = false;   // o anfitrião liga quando o zumbi está perto de alguém; nas cópias fica desligado
            agente.agentTypeID = 0;
            agente.radius = 0.3f; agente.height = 1.75f; agente.baseOffset = 0f;
            agente.speed = 0.55f; agente.angularSpeed = 240f; agente.acceleration = 6f;
            agente.stoppingDistance = 0.4f; agente.autoBraking = true;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agente.avoidancePriority = 50;
            var capsula = raiz.AddComponent<CapsuleCollider>();
            capsula.center = new Vector3(0f, 0.875f, 0f); capsula.height = 1.75f; capsula.radius = 0.28f;
            // a cápsula também é a hitbox do nível médio (o corpo inteiro)
            var hitboxCorpo = raiz.AddComponent<Hitbox>();
            hitboxCorpo.zumbi = z; hitboxCorpo.regiao = RegiaoDoCorpo.Corpo; hitboxCorpo.colisor = capsula;
            // corpo cinemático: as hitboxes que se mexem com os ossos viram partes dele (mais barato que colisores estáticos andando)
            var corpo = raiz.AddComponent<Rigidbody>();
            corpo.isKinematic = true; corpo.useGravity = false;
            var voz = raiz.AddComponent<AudioSource>();
            voz.playOnAwake = false; voz.spatialBlend = 1f; voz.rolloffMode = AudioRolloffMode.Linear;
            voz.minDistance = 1.5f; voz.maxDistance = 22f; voz.dopplerLevel = 0f; voz.volume = 0.9f;

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(Fbx));
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            var animador = modelo.GetComponent<Animator>();
            animador.runtimeAnimatorController = controlador;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animador.keepAnimatorStateOnDisable = true;   // o nível Longe desliga o Animator; ao voltar, ele continua rastejando ou morto
            var smr = modelo.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = smr.sharedMesh;

            // hitboxes: as cápsulas vão de osso a osso (na pose em que o FBX abre, que não é a T-pose); a esfera da
            // cabeça é medida na malha e levada para o espaço do osso pela bindpose dele
            Transform Osso(string nome) => modelo.GetComponentsInChildren<Transform>(true).First(t => t.name == nome);
            var hitboxes = new List<Hitbox>();
            hitboxes.Add(EsferaDaCabeca(z, original, smr));
            hitboxes.Add(Capsula(z, Osso("spine"), Osso("hips").position, Osso("chest").position, 0.15f, RegiaoDoCorpo.Tronco));
            hitboxes.Add(Capsula(z, Osso("chest"), Osso("chest").position, Osso("neck").position, 0.17f, RegiaoDoCorpo.Tronco));
            foreach (var (lado, braco, antebraco, coxa, canela) in new[]
                     {
                         ("L", RegiaoDoCorpo.BracoE, RegiaoDoCorpo.AntebracoE, RegiaoDoCorpo.CoxaE, RegiaoDoCorpo.CanelaE),
                         ("R", RegiaoDoCorpo.BracoD, RegiaoDoCorpo.AntebracoD, RegiaoDoCorpo.CoxaD, RegiaoDoCorpo.CanelaD),
                     })
            {
                Transform ombro = Osso($"upper_arm.{lado}"), cotovelo = Osso($"forearm.{lado}"), mao = Osso($"hand.{lado}");
                hitboxes.Add(Capsula(z, ombro, ombro.position, cotovelo.position, 0.065f, braco));
                hitboxes.Add(Capsula(z, cotovelo, cotovelo.position, mao.position + (mao.position - cotovelo.position).normalized * 0.12f, 0.06f, antebraco));
                Transform quadril = Osso($"thigh.{lado}"), joelho = Osso($"shin.{lado}"), pe = Osso($"foot.{lado}");
                hitboxes.Add(Capsula(z, quadril, quadril.position, joelho.position, 0.085f, coxa));
                hitboxes.Add(Capsula(z, joelho, joelho.position, pe.position, 0.07f, canela));
            }

            smr.sharedMesh = malha;
            smr.sharedMaterials = new[] { Carregar<Material>(Arte + "/Materials/Zombie.mat") };
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;

            z.animador = animador;
            z.agente = agente;
            z.capsula = capsula;
            z.hitboxes = hitboxes.ToArray();
            z.renderers = new Renderer[] { smr };
            z.voz = voz;
            z.ossoPeito = Osso("chest");
            z.ossoBracoE = Osso("upper_arm.L");
            z.ossoBracoD = Osso("upper_arm.R");
            z.pedacos = Pedacos(modelo.transform, smr, original, Osso);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabZumbi));
            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabZumbi);
            Object.DestroyImmediate(raiz);
            return salvo.GetComponent<Zumbi>();
        }

        // a esfera da cabeça: no centro da região da cabeça (o submesh de textura mais perto do topo da cabeça), com o
        // raio dela, levada para o espaço do osso pela bindpose
        static Hitbox EsferaDaCabeca(Zumbi z, Mesh malha, SkinnedMeshRenderer smr)
        {
            const string osso = "head";
            var carne = Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat");
            Vector3 J(string b) => malha.bindposes[System.Array.FindIndex(smr.bones, t => t.name == b)].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 esperado = J("head") + (J("head") - J("neck")) * 1.5f;
            int melhor = -1; float menor = float.MaxValue;
            for (int i = 0; i < malha.subMeshCount; i++)
            {
                if (smr.sharedMaterials[i] == carne) continue;
                float d = (malha.GetSubMesh(i).bounds.center - esperado).sqrMagnitude;
                if (d < menor) { menor = d; melhor = i; }
            }
            var vertices = malha.vertices;
            var triangulos = malha.GetTriangles(melhor);
            var limites = new Bounds(vertices[triangulos[0]], Vector3.zero);
            foreach (int i in triangulos) limites.Encapsulate(vertices[i]);
            int indice = System.Array.FindIndex(smr.bones, t => t.name == osso);
            var bind = malha.bindposes[indice];
            // o raio é a média das três metades da cabeça (com o cabelo): de perto, mirar na borda do cabelo também acerta
            float media = (limites.extents.x + limites.extents.y + limites.extents.z) / 3f;
            float raio = bind.MultiplyVector(Vector3.right * media).magnitude;
            var h = NovaHitbox(smr.bones[indice], RegiaoDoCorpo.Cabeca.ToString(), RegiaoDoCorpo.Cabeca, out var go);
            h.zumbi = z;
            go.transform.localPosition = bind.MultiplyPoint3x4(limites.center);
            var c = go.AddComponent<SphereCollider>();
            c.radius = raio;
            h.colisor = c;
            return h;
        }

        static Hitbox NovaHitbox(Transform osso, string nome, RegiaoDoCorpo regiao, out GameObject go)
        {
            go = new GameObject($"Hitbox {nome}") { layer = Zumbi.CamadaZumbi };
            go.transform.SetParent(osso, false);
            var h = go.AddComponent<Hitbox>();
            h.regiao = regiao;
            return h;
        }

        /// <summary>Uma cápsula entre dois pontos (na pose em que o modelo abre), presa ao osso que a move.</summary>
        static Hitbox Capsula(Zumbi z, Transform osso, Vector3 de, Vector3 ate, float raio, RegiaoDoCorpo regiao)
        {
            var h = NovaHitbox(osso, $"{regiao} ({osso.name})", regiao, out var go);
            h.zumbi = z;
            go.transform.SetPositionAndRotation((de + ate) * 0.5f, Quaternion.FromToRotation(Vector3.up, ate - de));
            float escala = Mathf.Max(0.0001f, go.transform.lossyScale.x);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = raio / escala;
            c.height = (Vector3.Distance(de, ate) + 2f * raio) / escala;
            h.colisor = c;
            return h;
        }

        /// <summary>
        /// Os membros que caem. A peça solta (do NovoZombie_Gibs.fbx) foi exportada na pose de repouso, no mesmo espaço
        /// do modelo: a pose dela em relação ao osso é a da bindpose (osso⁻¹ · peça), e no jogo ela nasce no osso animado.
        /// </summary>
        static Zumbi.Pedaco[] Pedacos(Transform modelo, SkinnedMeshRenderer smr, Mesh malha, System.Func<string, Transform> osso)
        {
            var arquivo = Carregar<GameObject>(FbxPedacos);
            Matrix4x4 smrNoModelo = modelo.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            Zumbi.Pedaco Pedaco(Membros membro, string nomeOsso, string nomeFilho, string nomePeca)
            {
                var p = new Zumbi.Pedaco { membro = membro, osso = osso(nomeOsso), ossoFilho = nomeFilho != null ? osso(nomeFilho) : null, rotacao = Quaternion.identity };
                if (nomePeca == null) return p;
                var peca = arquivo.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == nomePeca);
                Matrix4x4 bindDoOsso = smrNoModelo * malha.bindposes[System.Array.FindIndex(smr.bones, b => b.name == nomeOsso)].inverse;
                Matrix4x4 pecaNoModelo = arquivo.transform.worldToLocalMatrix * peca.transform.localToWorldMatrix;
                Matrix4x4 relativo = bindDoOsso.inverse * pecaNoModelo;
                p.malha = peca.sharedMesh;
                p.materiais = peca.GetComponent<MeshRenderer>().sharedMaterials;
                p.posicao = relativo.MultiplyPoint3x4(Vector3.zero);
                p.rotacao = relativo.rotation;
                return p;
            }
            return new[]
            {
                Pedaco(Membros.Cabeca, "head", null, null),
                Pedaco(Membros.BracoE, "upper_arm.L", "forearm.L", "Gib_Arm.L"),
                Pedaco(Membros.AntebracoE, "forearm.L", "hand.L", "Gib_Forearm.L"),
                Pedaco(Membros.BracoD, "upper_arm.R", "forearm.R", "Gib_Arm.R"),
                Pedaco(Membros.AntebracoD, "forearm.R", "hand.R", "Gib_Forearm.R"),
                Pedaco(Membros.CoxaE, "thigh.L", "shin.L", "Gib_Leg.L"),
                Pedaco(Membros.CanelaE, "shin.L", "foot.L", "Gib_Shin.L"),
                Pedaco(Membros.CoxaD, "thigh.R", "shin.R", "Gib_Leg.R"),
                Pedaco(Membros.CanelaD, "shin.R", "foot.R", "Gib_Shin.R"),
            };
        }

        // ------------------------------------------------------------------ decals (manchas de sangue e furos de tiro)

        static (Material sangue, Material furo) MateriaisDosDecals()
        {
            Directory.CreateDirectory(PastaDecals);
            var sangue = MaterialDecal("Decal_Sangue", Textura("Sangue", 128, TexturaSangue));
            var furo = MaterialDecal("Decal_Furo", Textura("Furo", 64, TexturaFuro));
            return (sangue, furo);
        }

        static Material MaterialDecal(string nome, Texture2D textura)
        {
            string caminho = $"{PastaDecals}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Shader Graphs/Decal"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetTexture("Base_Map", textura);
            m.SetFloat("Normal_Blend", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Textura(string nome, int tamanho, System.Func<float, float, System.Random, Color> pixel)
        {
            string caminho = $"{PastaDecals}/{nome}.png";
            var t = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
            var r = new System.Random(nome.GetHashCode());
            for (int y = 0; y < tamanho; y++)
                for (int x = 0; x < tamanho; x++)
                    t.SetPixel(x, y, pixel((x + 0.5f) / tamanho * 2f - 1f, (y + 0.5f) / tamanho * 2f - 1f, r));
            File.WriteAllBytes(caminho, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(caminho);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        // uma poça irregular (borda com ruído) e respingos em volta, vermelho escuro
        static Color TexturaSangue(float x, float y, System.Random r)
        {
            float ang = Mathf.Atan2(y, x), d = Mathf.Sqrt(x * x + y * y);
            float borda = 0.55f + 0.18f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2.2f + 5f, Mathf.Sin(ang) * 2.2f + 5f) + 0.08f * Mathf.Sin(ang * 7f);
            float a = Mathf.Clamp01((borda - d) * 14f);
            for (int i = 0; i < 9; i++)
            {
                float ga = i * 2.39f, gd = 0.62f + 0.07f * (i % 3);
                float gx = Mathf.Cos(ga) * gd, gy = Mathf.Sin(ga) * gd, gr = 0.035f + 0.02f * (i % 2);
                a = Mathf.Max(a, Mathf.Clamp01((gr - Mathf.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy))) * 40f));
            }
            float tom = 0.75f + 0.25f * Mathf.PerlinNoise(x * 3f + 11f, y * 3f + 7f);
            return new Color(0.32f * tom, 0.015f, 0.015f, a * 0.92f);
        }

        // furo escuro no meio, com uma auréola de pó/queimado em volta
        static Color TexturaFuro(float x, float y, System.Random r)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float buraco = Mathf.Clamp01((0.22f - d) * 30f);
            float aureola = Mathf.Clamp01((0.7f - d) * 2.2f) * (0.5f + 0.5f * Mathf.PerlinNoise(x * 6f + 3f, y * 6f + 1f)) * 0.55f;
            float a = Mathf.Max(buraco, aureola);
            float v = Mathf.Lerp(0.18f, 0.03f, buraco);
            return new Color(v, v * 0.95f, v * 0.9f, a);
        }

        /// <summary>O recurso de decals no renderizador principal (técnica de tela, a mais barata).</summary>
        static void DecalsNoRenderizador()
        {
            var dados = Carregar<UniversalRendererData>(RendererPrincipal);
            var recurso = dados.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
            if (recurso == null)
            {
                recurso = ScriptableObject.CreateInstance<DecalRendererFeature>();
                recurso.name = "Decals";
                AssetDatabase.AddObjectToAsset(recurso, dados);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(recurso, out string _, out long id);
                var so = new SerializedObject(dados);
                var lista = so.FindProperty("m_RendererFeatures");
                var mapa = so.FindProperty("m_RendererFeatureMap");
                lista.arraySize++; lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = recurso;
                mapa.arraySize++; mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var ajustes = new SerializedObject(recurso);
            var tecnica = ajustes.FindProperty("m_Settings.technique");
            var distancia = ajustes.FindProperty("m_Settings.maxDrawDistance");
            if (tecnica != null) tecnica.enumValueIndex = 2;   // ScreenSpace
            if (distancia != null) distancia.floatValue = 60f;
            if (tecnica == null || distancia == null) Debug.LogWarning("Decals: não achei os campos de ajuste do DecalRendererFeature; ficou no padrão.");
            ajustes.ApplyModifiedPropertiesWithoutUndo();
            recurso.SetActive(true);
            EditorUtility.SetDirty(recurso);
            EditorUtility.SetDirty(dados);
        }

        // ------------------------------------------------------------------ cena

        static void MontarCena(Scene cena, Zumbi prefab, Mesh inteira, Mesh completa, MapaDaMalha mapa, (Material sangue, Material furo) decals)
        {
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Zumbis");
            if (velho != null) Object.DestroyImmediate(velho);
            var go = new GameObject("Zumbis");
            go.AddComponent<NetworkObject>();
            var m = go.AddComponent<ZombieManager>();
            go.AddComponent<BenchmarkDeZumbis>();
            m.prefab = prefab;
            m.vozDoZumbi = Carregar<AudioClip>("Assets/_Game/Audio/SomZombie1.mp3");
            m.malhaInteira = inteira;
            m.malhaCompleta = completa;
            m.mapa = mapa;
            m.materialCorpo = Carregar<Material>(Arte + "/Materials/Zombie.mat");
            m.materialCarne = Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat");
            m.areasLivres = new[]
            {
                new ZombieManager.AreaLivre { centro = new Vector3(-20f, 0f, -20f), raio = 11f },   // a casa da família e o bunker
                new ZombieManager.AreaLivre { centro = new Vector3(-20f, 0f, -4.4f), raio = 12f },  // onde os jogadores aparecem
            };
            foreach (var (nome, centro, raio, quantidade) in Hordas)
            {
                var h = new GameObject($"Horda: {nome}").AddComponent<PontoDeHorda>();
                h.transform.SetParent(go.transform, false);
                h.transform.position = centro;
                h.raio = raio;
                h.quantidade = quantidade;
            }

            // o sangue mora no "Efeitos", ao lado dos efeitos de tiro
            var efeitos = cena.GetRootGameObjects().First(g => g.name == "Efeitos");
            var sangue = efeitos.GetComponent<Sangue>();
            if (sangue == null) sangue = efeitos.AddComponent<Sangue>();
            sangue.materialParticulas = Carregar<Material>("Assets/_Game/Art/Materials/Particulas.mat");
            sangue.decalSangue = decals.sangue;
            sangue.decalFuro = decals.furo;
        }

        /// <summary>O NavMesh da cidade, pelos colisores (o mesmo que segura os jogadores), dentro da barreira.</summary>
        internal static void AssarNavegacao(Scene cena)
        {
            var raiz = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Navegação");
            if (raiz == null) raiz = new GameObject("Navegação");
            var superficie = raiz.GetComponent<NavMeshSurface>();
            if (superficie == null) superficie = raiz.AddComponent<NavMeshSurface>();
            superficie.agentTypeID = 0;
            superficie.collectObjects = CollectObjects.Volume;
            superficie.center = new Vector3(0f, 4f, 0f);
            superficie.size = new Vector3(128f, 24f, 128f);   // a cidade até a barreira (raio 63), do bunker ao telhado do prédio
            superficie.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            superficie.layerMask = ~((1 << JogadorRede.CamadaBracos) | (1 << Zumbi.CamadaZumbi));
            superficie.BuildNavMesh();
            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoNavMesh));
            AssetDatabase.CreateAsset(superficie.navMeshData, ArquivoNavMesh);
            EditorUtility.SetDirty(superficie);
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"NavMesh da cidade: {tri.vertices.Length} vértices, {tri.indices.Length / 3} triângulos.");
        }
    }
}
