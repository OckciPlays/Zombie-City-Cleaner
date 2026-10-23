using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta os zumbis da Parte 5:
    /// - a camada "Zumbi" (7), que não colide com ela mesma nem com os braços de primeira pessoa;
    /// - a malha do zumbi inteiro: as 6 regiões juntas num submesh só (um draw call em vez de seis);
    /// - o prefab, com agente, cápsula, corpo cinemático, voz e as hitboxes presas nos ossos;
    /// - o jogador com vida (VidaJogador), refazendo o prefab dele;
    /// - o objeto "Zumbis" da cena (ZombieManager, benchmark e os pontos de horda);
    /// - o NavMesh da cidade.
    /// Menu: Zombie City Cleaner > Montar zumbis. Pode rodar de novo.
    /// </summary>
    public static class MontagemDosZumbis
    {
        const string Arte = "Assets/_Game/Art/Characters/Zombie";
        const string MalhaInteira = Arte + "/Malhas/Zumbi_Inteiro.asset";
        const string PrefabZumbi = "Assets/_Game/Prefabs/Zumbi.prefab";
        const string ArquivoNavMesh = "Assets/_Game/Scenes/Demo_Cidade/NavMesh da cidade.asset";

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

        [MenuItem("Zombie City Cleaner/Montar zumbis")]
        public static void Montar()
        {
            CamadaDosZumbis();
            var prefab = MontarPrefab(MalhaDoZumbiInteiro());
            MontagemDaRede.MontarPrefab();
            var cena = EditorSceneManager.GetActiveScene();
            MontarCena(cena, prefab);
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

        static void CamadaDosZumbis()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var camada = tags.FindProperty("layers").GetArrayElementAtIndex(Zumbi.CamadaZumbi);
            if (camada.stringValue != "Zumbi")
            {
                camada.stringValue = "Zumbi";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, Zumbi.CamadaZumbi, true);
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, true);
        }

        /// <summary>As 6 regiões do corpo num submesh só. As tampas (submeshes 6 a 10) ficam de fora até a Parte 6.</summary>
        static Mesh MalhaDoZumbiInteiro()
        {
            var original = Carregar<GameObject>(Arte + "/NovoZombie.fbx").GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            var nova = Object.Instantiate(original);
            nova.name = "Zumbi_Inteiro";
            var triangulos = new List<int>();
            for (int s = 0; s < 6; s++) triangulos.AddRange(original.GetTriangles(s));
            nova.subMeshCount = 1;
            nova.SetTriangles(triangulos, 0, false);
            Directory.CreateDirectory(Path.GetDirectoryName(MalhaInteira));
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaInteira);
            if (existente == null)
            {
                AssetDatabase.CreateAsset(nova, MalhaInteira);
                return nova;
            }
            EditorUtility.CopySerialized(nova, existente);
            Object.DestroyImmediate(nova);
            return existente;
        }

        static Zumbi MontarPrefab(Mesh malha)
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
            // corpo cinemático: as hitboxes que se mexem com os ossos viram partes dele (mais barato que colisores estáticos andando)
            var corpo = raiz.AddComponent<Rigidbody>();
            corpo.isKinematic = true; corpo.useGravity = false;
            var voz = raiz.AddComponent<AudioSource>();
            voz.playOnAwake = false; voz.spatialBlend = 1f; voz.rolloffMode = AudioRolloffMode.Linear;
            voz.minDistance = 1.5f; voz.maxDistance = 22f; voz.dopplerLevel = 0f; voz.volume = 0.9f;

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(Arte + "/NovoZombie.fbx"));
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            var animador = modelo.GetComponent<Animator>();
            animador.runtimeAnimatorController = Carregar<RuntimeAnimatorController>(Arte + "/Zombie.controller");
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var smr = modelo.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = smr.sharedMesh;

            // hitboxes: as cápsulas vão de osso a osso (na pose em que o FBX abre, que não é a T-pose); a esfera da
            // cabeça é medida na malha e levada para o espaço do osso pela bindpose dele
            Transform Osso(string nome) => modelo.GetComponentsInChildren<Transform>(true).First(t => t.name == nome);
            var hitboxes = new List<Hitbox>();
            var cabeca = LimitesDaRegiao(original, 2);   // submesh 2 = Zombie_Head (com o cabelo)
            var bindCabeca = original.bindposes[System.Array.IndexOf(smr.bones, Osso("head"))];
            float raioCabeca = bindCabeca.MultiplyVector(Vector3.right * Mathf.Min(cabeca.extents.x, cabeca.extents.y, cabeca.extents.z)).magnitude * 1.05f;
            hitboxes.Add(Esfera(z, Osso("head"), bindCabeca.MultiplyPoint3x4(cabeca.center), raioCabeca, RegiaoDoCorpo.Cabeca));
            hitboxes.Add(Capsula(z, Osso("spine"), Osso("hips").position, Osso("chest").position, 0.15f, RegiaoDoCorpo.Tronco));
            hitboxes.Add(Capsula(z, Osso("chest"), Osso("chest").position, Osso("neck").position, 0.17f, RegiaoDoCorpo.Tronco));
            foreach (var (lado, braco, antebraco, perna) in new[] { ("L", RegiaoDoCorpo.BracoE, RegiaoDoCorpo.AntebracoE, RegiaoDoCorpo.PernaE), ("R", RegiaoDoCorpo.BracoD, RegiaoDoCorpo.AntebracoD, RegiaoDoCorpo.PernaD) })
            {
                Transform ombro = Osso($"upper_arm.{lado}"), cotovelo = Osso($"forearm.{lado}"), mao = Osso($"hand.{lado}");
                hitboxes.Add(Capsula(z, ombro, ombro.position, cotovelo.position, 0.065f, braco));
                hitboxes.Add(Capsula(z, cotovelo, cotovelo.position, mao.position + (mao.position - cotovelo.position).normalized * 0.12f, 0.06f, antebraco));
                Transform coxa = Osso($"thigh.{lado}"), canela = Osso($"shin.{lado}"), pe = Osso($"foot.{lado}");
                hitboxes.Add(Capsula(z, coxa, coxa.position, canela.position, 0.085f, perna));
                hitboxes.Add(Capsula(z, canela, canela.position, pe.position, 0.07f, perna));
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

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabZumbi));
            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabZumbi);
            Object.DestroyImmediate(raiz);
            return salvo.GetComponent<Zumbi>();
        }

        /// <summary>Os limites de uma região (submesh), no espaço da malha.</summary>
        static Bounds LimitesDaRegiao(Mesh malha, int submesh)
        {
            var vertices = malha.vertices;
            var triangulos = malha.GetTriangles(submesh);
            var limites = new Bounds(vertices[triangulos[0]], Vector3.zero);
            foreach (int i in triangulos) limites.Encapsulate(vertices[i]);
            return limites;
        }

        static Hitbox NovaHitbox(Zumbi z, Transform osso, string nome, RegiaoDoCorpo regiao, out GameObject go)
        {
            go = new GameObject($"Hitbox {nome}") { layer = Zumbi.CamadaZumbi };
            go.transform.SetParent(osso, false);
            var h = go.AddComponent<Hitbox>();
            h.zumbi = z;
            h.regiao = regiao;
            return h;
        }

        /// <summary>Uma esfera no espaço do osso (centro e raio já nas medidas do osso).</summary>
        static Hitbox Esfera(Zumbi z, Transform osso, Vector3 centroNoOsso, float raioNoOsso, RegiaoDoCorpo regiao)
        {
            var h = NovaHitbox(z, osso, regiao.ToString(), regiao, out var go);
            go.transform.localPosition = centroNoOsso;
            var c = go.AddComponent<SphereCollider>();
            c.radius = raioNoOsso;
            h.colisor = c;
            return h;
        }

        /// <summary>Uma cápsula entre dois pontos (na pose de repouso), presa ao osso que a move.</summary>
        static Hitbox Capsula(Zumbi z, Transform osso, Vector3 de, Vector3 ate, float raio, RegiaoDoCorpo regiao)
        {
            var h = NovaHitbox(z, osso, $"{regiao} ({osso.name})", regiao, out var go);
            go.transform.SetPositionAndRotation((de + ate) * 0.5f, Quaternion.FromToRotation(Vector3.up, ate - de));
            float escala = Mathf.Max(0.0001f, go.transform.lossyScale.x);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = raio / escala;
            c.height = (Vector3.Distance(de, ate) + 2f * raio) / escala;
            h.colisor = c;
            return h;
        }

        static void MontarCena(Scene cena, Zumbi prefab)
        {
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Zumbis");
            if (velho != null) Object.DestroyImmediate(velho);
            var go = new GameObject("Zumbis");
            go.AddComponent<NetworkObject>();
            var m = go.AddComponent<ZombieManager>();
            go.AddComponent<BenchmarkDeZumbis>();
            m.prefab = prefab;
            m.vozDoZumbi = Carregar<AudioClip>("Assets/_Game/Audio/SomZombie1.mp3");
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
