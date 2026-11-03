using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta o sobrevivente (02/11/2026): o prefab (Prefabs/Sobreviventes/Sobrevivente.prefab) e o lugar dele na cena Mundo.
    /// Menu: Zombie City Cleaner > Montar os sobreviventes. Pode rodar de novo à vontade.
    ///  - a raiz: NetworkObject, NetworkTransform do anfitrião (a posição e o giro em Y), NavMeshAgent, a cápsula (camada 2,
    ///    a do "Ignore Raycast": o jogador esbarra, o tiro não pega) e o <see cref="Sobrevivente"/>;
    ///  - o "Corpo": a cópia do corpo do jogador (Corpo_PrincipalBloco do Jogador.prefab: as roupas, as armas na mão, as
    ///    bocas e o controlador de cada arma);
    ///  - as partes que levam tiro (<see cref="ParteDoSobrevivente"/>), presas aos ossos, na camada 12 ("Sobrevivente"):
    ///    a cabeça (uma esfera), o tronco (duas cápsulas), os braços e as pernas;
    ///  - a lanterna no peito (desligada); a queda é com física (Sobrevivente.Queda.cs: o corpo não tem animação de morte);
    ///  - na lista dos prefabs da rede; o <see cref="SobreviventesDoMundo"/> e as <see cref="MochilasNoChao"/> no "Mundo".
    /// </summary>
    public static class MontagemDosSobreviventes
    {
        public const string Pasta = "Assets/_Game/Prefabs/Sobreviventes";
        public const string Prefab = Pasta + "/Sobrevivente.prefab";
        const string Cena = "Assets/_Game/Scenes/Mundo.unity";

        [MenuItem("Zombie City Cleaner/Montar os sobreviventes")]
        public static void Montar()
        {
            NomearCamada();
            var prefab = MontarPrefab();
            PorNaRede(prefab);
            PorNaCena(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Sobreviventes] Prefab montado e posto na cena Mundo.");
        }

        static void NomearCamada()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var item = tags.FindProperty("layers").GetArrayElementAtIndex(Sobrevivente.Camada);
            if (item.stringValue != "Sobrevivente")
            {
                if (!string.IsNullOrEmpty(item.stringValue)) Debug.LogWarning($"A camada {Sobrevivente.Camada} se chamava {item.stringValue}.");
                item.stringValue = "Sobrevivente";
                tags.ApplyModifiedProperties();
            }
            // as partes do corpo não esbarram nos braços de primeira pessoa, nos zumbis, nos corpos, nos carros nem umas
            // nas outras (são colisores parados que andam com os ossos); o jogador esbarra nelas
            var fisica = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            var matriz = fisica.FindProperty("m_LayerCollisionMatrix");
            void Tirar(int a, int b)
            {
                var item = matriz.GetArrayElementAtIndex(a);
                item.longValue = (item.longValue & 0xFFFFFFFFL) & ~(1L << b);
            }
            foreach (int c in new[] { 2, 6, 7, 8, 11, Sobrevivente.Camada })
            {
                Tirar(Sobrevivente.Camada, c);
                Tirar(c, Sobrevivente.Camada);
                Physics.IgnoreLayerCollision(Sobrevivente.Camada, c, true);
            }
            fisica.ApplyModifiedProperties();
        }

        public static GameObject MontarPrefab()
        {
            Directory.CreateDirectory(Pasta);
            var jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            var corpoDoJogador = jogador != null ? jogador.transform.Find("Corpo_PrincipalBloco") : null;
            if (corpoDoJogador == null) throw new FileNotFoundException("O corpo do jogador (Corpo_PrincipalBloco) não está no Jogador.prefab.");

            var raiz = new GameObject("Sobrevivente") { layer = 2 };
            raiz.AddComponent<NetworkObject>();
            var nt = raiz.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            var agente = raiz.AddComponent<NavMeshAgent>();
            agente.radius = 0.3f; agente.height = 1.8f; agente.baseOffset = 0f;
            agente.speed = 3.2f; agente.angularSpeed = 540f; agente.acceleration = 14f; agente.stoppingDistance = 0.4f;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agente.enabled = false;
            var capsula = raiz.AddComponent<CapsuleCollider>();
            capsula.center = new Vector3(0f, 0.9f, 0f); capsula.height = 1.8f; capsula.radius = 0.3f;
            var sob = raiz.AddComponent<Sobrevivente>();

            // o corpo: a cópia do do jogador
            var corpoGo = Object.Instantiate(corpoDoJogador.gameObject, raiz.transform, false);
            corpoGo.name = "Corpo";
            corpoGo.SetActive(true);
            corpoGo.transform.localPosition = Vector3.zero;
            corpoGo.transform.localRotation = Quaternion.identity;
            var corpo = corpoGo.GetComponent<CorpoDoPersonagem>();
            corpo.animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            corpo.animador.applyRootMotion = false;
            foreach (var r in corpoGo.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;

            // as partes que levam tiro
            var ossos = corpoGo.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            Transform Osso(string nome) => ossos.TryGetValue(nome, out var t) ? t : throw new MissingReferenceException("Osso " + nome);
            Esfera(Osso("head"), RegiaoDoSobrevivente.Cabeca, Vector3.up * 0.1f, 0.13f);
            Capsula(Osso("spine"), Osso("hips").position, Osso("chest").position, 0.17f, RegiaoDoSobrevivente.Tronco, "Tronco_Baixo");
            Capsula(Osso("chest"), Osso("chest").position, Osso("neck").position, 0.18f, RegiaoDoSobrevivente.Tronco, "Tronco_Cima");
            foreach (var l in new[] { "L", "R" })
            {
                Capsula(Osso("upper_arm." + l), Osso("upper_arm." + l).position, Osso("forearm." + l).position, 0.065f, RegiaoDoSobrevivente.Membro, "Braco." + l);
                var mao = Osso("hand." + l).position;
                var ante = Osso("forearm." + l).position;
                Capsula(Osso("forearm." + l), ante, mao + (mao - ante).normalized * 0.08f, 0.055f, RegiaoDoSobrevivente.Membro, "Antebraco." + l);
                Capsula(Osso("thigh." + l), Osso("thigh." + l).position, Osso("shin." + l).position, 0.085f, RegiaoDoSobrevivente.Membro, "Coxa." + l);
                Capsula(Osso("shin." + l), Osso("shin." + l).position, Osso("foot." + l).position, 0.065f, RegiaoDoSobrevivente.Membro, "Canela." + l);
            }

            // a lanterna no peito (como a do jogador, sem sombra: são vários)
            var luzGo = new GameObject("Lanterna");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = new Vector3(0.08f, 1.28f, 0.22f);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Spot; luz.color = new Color(1f, 0.92f, 0.78f);
            luz.intensity = 26f; luz.range = 24f; luz.spotAngle = 58f; luz.innerSpotAngle = 30f;
            luz.shadows = LightShadows.None;
            luz.enabled = false;

            sob.corpo = corpo;
            sob.agente = agente;
            sob.lanterna = luz;
            sob.agachado = ClipeAgachado();

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        /// <summary>O clipe "Agachado" do FBX do personagem (os dois do carro quebrado ajoelham na roda com ele).</summary>
        public static AnimationClip ClipeAgachado()
        {
            var clipe = AssetDatabase.LoadAllAssetsAtPath(MontagemDasArmas.FbxDoPersonagem(0)).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == "Agachado");
            if (clipe == null) Debug.LogWarning("[Sobreviventes] O FBX do personagem não tem o clipe \"Agachado\".");
            return clipe;
        }

        static void Esfera(Transform osso, RegiaoDoSobrevivente regiao, Vector3 acimaNoMundo, float raio)
        {
            var go = new GameObject("Parte_Cabeca") { layer = Sobrevivente.Camada };
            go.transform.SetParent(osso, false);
            go.transform.position = osso.position + acimaNoMundo;
            var c = go.AddComponent<SphereCollider>();
            c.radius = raio / Mathf.Max(0.0001f, go.transform.lossyScale.x);
            var p = go.AddComponent<ParteDoSobrevivente>();
            p.regiao = regiao;
            p.colisor = c;
        }

        // uma cápsula de "de" a "ate" (no mundo, na pose do prefab), presa ao osso
        static void Capsula(Transform osso, Vector3 de, Vector3 ate, float raio, RegiaoDoSobrevivente regiao, string nome)
        {
            var go = new GameObject("Parte_" + nome) { layer = Sobrevivente.Camada };
            go.transform.SetParent(osso, false);
            Vector3 eixo = ate - de;
            go.transform.SetPositionAndRotation((de + ate) * 0.5f, Quaternion.FromToRotation(Vector3.up, eixo.sqrMagnitude > 1e-6f ? eixo : Vector3.up));
            float escala = Mathf.Max(0.0001f, go.transform.lossyScale.y);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = raio / escala;
            c.height = (eixo.magnitude + raio * 2f) / escala;
            var p = go.AddComponent<ParteDoSobrevivente>();
            p.regiao = regiao;
            p.colisor = c;
        }

        static void PorNaRede(GameObject prefab)
        {
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista == null || lista.PrefabList.Any(p => p.Prefab == prefab)) return;
            lista.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(lista);
        }

        /// <summary>O SobreviventesDoMundo e as MochilasNoChao no objeto "Mundo" (a cena aberta, ou a Mundo.unity).</summary>
        public static void PorNoMundo(GameObject mundo, GameObject prefab)
        {
            var s = mundo.GetComponent<SobreviventesDoMundo>();
            if (s == null) s = mundo.AddComponent<SobreviventesDoMundo>();
            s.prefab = prefab;
            if (mundo.GetComponent<MochilasNoChao>() == null) mundo.AddComponent<MochilasNoChao>();
            // os acampamentos (02/11/2026, a etapa 3)
            if (mundo.GetComponent<AcampamentosDoMundo>() == null) mundo.AddComponent<AcampamentosDoMundo>();
            // os carros quebrados (a etapa 4)
            if (mundo.GetComponent<CarrosQuebradosDoMundo>() == null) mundo.AddComponent<CarrosQuebradosDoMundo>();
            EditorUtility.SetDirty(mundo);
        }

        static void PorNaCena(GameObject prefab)
        {
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != Cena)
            {
                if (EditorSceneManager.GetSceneManagerSetup().Any(c => c.isLoaded && EditorSceneManager.GetSceneByPath(c.path).isDirty))
                {
                    Debug.LogWarning("[Sobreviventes] Há uma cena aberta sem salvar: abra a Mundo.unity e rode o menu de novo.");
                    return;
                }
                cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
            }
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("[Sobreviventes] A cena Mundo não tem o objeto \"Mundo\".");
                return;
            }
            PorNoMundo(mundo, prefab);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }
    }
}
