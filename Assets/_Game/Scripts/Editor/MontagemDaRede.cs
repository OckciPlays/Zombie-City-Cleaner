using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta o prefab do jogador de rede e os objetos de rede da cena aberta (NetworkManager, efeitos, pontos de
    /// nascimento, câmera do menu). Menu: Zombie City Cleaner > Montar jogador e rede. Pode rodar de novo à vontade.
    /// </summary>
    public static class MontagemDaRede
    {
        const string Arte = "Assets/_Game/Art/Characters/Jogador";
        const string Prefabs = "Assets/_Game/Prefabs";
        const string PrefabJogador = Prefabs + "/Jogador.prefab";
        const string Materiais = "Assets/_Game/Art/Materials";

        [MenuItem("Zombie City Cleaner/Montar jogador e rede")]
        public static void Montar()
        {
            Directory.CreateDirectory(Prefabs);
            var prefab = MontarPrefab();
            MontarCena(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Jogador de rede e objetos da cena montados.");
        }

        static T Carregar<T>(string caminho) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(caminho);
            if (a == null) throw new FileNotFoundException("Não encontrado", caminho);
            return a;
        }

        static Transform Buscar(Transform raiz, string nome) => raiz.GetComponentsInChildren<Transform>(true).First(t => t.name == nome);

        static void Camada(GameObject go, int camada)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camada;
        }

        static GameObject MontarPrefab()
        {
            var raiz = new GameObject("Jogador");
            var cc = raiz.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.35f; cc.skinWidth = 0.04f;
            raiz.AddComponent<NetworkObject>();
            var nt = raiz.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            var rede = raiz.AddComponent<JogadorRede>();
            var movimento = raiz.AddComponent<MovimentoPrimeiraPessoa>();
            var arma = raiz.AddComponent<ArmaJogador>();
            var lanterna = raiz.AddComponent<LanternaJogador>();

            // corpo (o que os outros veem; para o dono vira só sombra)
            var corpo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(Arte + "/Jogador.fbx"));
            corpo.name = "Corpo";
            corpo.transform.SetParent(raiz.transform, false);
            var animCorpo = corpo.GetComponent<Animator>();
            animCorpo.runtimeAnimatorController = Carregar<RuntimeAnimatorController>(Arte + "/Jogador_TP.controller");
            animCorpo.applyRootMotion = false;
            animCorpo.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // cabeça: câmera do mundo, câmera dos braços por cima e os braços de primeira pessoa
            var cabeca = new GameObject("Cabeca").transform;
            cabeca.SetParent(raiz.transform, false);
            cabeca.localPosition = new Vector3(0f, 1.62f, 0f);
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(cabeca, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
            cam.cullingMask = ~(1 << JogadorRede.CamadaBracos);
            var dadosCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            dadosCam.renderPostProcessing = true;
            dadosCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var ouvido = camGo.AddComponent<AudioListener>();

            var camBracosGo = new GameObject("CameraBracos");
            camBracosGo.transform.SetParent(camGo.transform, false);
            var camBracos = camBracosGo.AddComponent<Camera>();
            camBracos.fieldOfView = 55f; camBracos.nearClipPlane = 0.01f; camBracos.farClipPlane = 5f;
            camBracos.cullingMask = 1 << JogadorRede.CamadaBracos;
            var dadosBracos = camBracosGo.AddComponent<UniversalAdditionalCameraData>();
            dadosBracos.renderType = CameraRenderType.Overlay;
            dadosBracos.renderPostProcessing = false;
            dadosBracos.renderShadows = false;
            dadosCam.cameraStack.Add(camBracos);

            var bracos = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(Arte + "/Jogador_FP.fbx"));
            bracos.name = "Bracos";
            bracos.transform.SetParent(camGo.transform, false);
            Camada(bracos, JogadorRede.CamadaBracos);
            foreach (var r in bracos.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            var animBracos = bracos.GetComponent<Animator>();
            animBracos.runtimeAnimatorController = Carregar<RuntimeAnimatorController>(Arte + "/Jogador_FP.controller");
            animBracos.applyRootMotion = false;

            // lanterna no peito
            var luzGo = new GameObject("Lanterna");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = lanterna.posicaoNoPeito;
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Spot; luz.color = new Color(1f, 0.94f, 0.82f);
            luz.intensity = 14f; luz.range = 24f; luz.spotAngle = 55f; luz.innerSpotAngle = 24f;
            luz.shadows = LightShadows.Soft; luz.shadowNearPlane = 0.2f;

            // ligações
            rede.cameraMundo = cam; rede.cameraBracos = camBracos; rede.ouvido = ouvido;
            rede.bracos = bracos; rede.animadorBracos = animBracos; rede.animadorCorpo = animCorpo;
            var smrCorpo = Buscar(corpo.transform, "Jogador_Corpo").GetComponent<SkinnedMeshRenderer>();
            var smrPistola = Buscar(corpo.transform, "Pistola_TP").GetComponent<SkinnedMeshRenderer>();
            var smrBracos = Buscar(bracos.transform, "FP_Bracos").GetComponent<SkinnedMeshRenderer>();
            rede.renderersCorpo = new Renderer[] { smrCorpo, smrPistola };
            rede.ossoPeito = Buscar(corpo.transform, "chest");
            rede.ossoCabeca = Buscar(corpo.transform, "head");
            rede.bocaCorpo = Buscar(corpo.transform, "Boca_TP");
            rede.variacoes = Enumerable.Range(0, 4).Select(k => Carregar<Material>($"{Arte}/Materials/Jogador_Variacao{k}.mat")).ToArray();
            rede.pintaveis = new Renderer[] { smrCorpo, smrBracos };
            movimento.rede = rede; movimento.cabeca = cabeca;
            arma.rede = rede; arma.movimento = movimento; arma.bocaBracos = Buscar(bracos.transform, "Boca_FP");
            lanterna.rede = rede; lanterna.luz = luz;

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabJogador);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        static Material MaterialParticula(string nome, bool aditivo, Texture2D textura)
        {
            string caminho = $"{Materiais}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, caminho); }
            m.SetFloat("_Surface", 1f);                       // transparente
            m.SetFloat("_Blend", aditivo ? 2f : 0f);          // 2 = aditivo, 0 = alpha
            m.SetColor("_BaseColor", aditivo ? new Color(1f, 0.78f, 0.45f) : Color.white);
            if (textura != null) m.SetTexture("_BaseMap", textura);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", aditivo ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D TexturaRedonda()
        {
            string caminho = $"{Materiais}/Brilho.png";
            if (!File.Exists(caminho))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
                        float a = Mathf.Clamp01(1f - d); a *= a;
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                File.WriteAllBytes(caminho, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(caminho);
                var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        static void MontarCena(GameObject prefab)
        {
            var cena = EditorSceneManager.GetActiveScene();
            foreach (var nome in new[] { "Rede", "Efeitos", "Pontos de nascimento" })
            {
                var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == nome);
                if (velho != null) Object.DestroyImmediate(velho);
            }

            // NetworkManager + transporte + sessão + menu
            var redeGo = new GameObject("Rede");
            var utp = redeGo.AddComponent<UnityTransport>();
            var nm = redeGo.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, PlayerPrefab = prefab, TickRate = 30, ConnectionApproval = false };
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista != null)
            {
                if (!lista.PrefabList.Any(p => p.Prefab == prefab)) lista.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(lista);
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lista);
            }
            redeGo.AddComponent<SessaoOnline>();
            redeGo.AddComponent<MenuOnline>();

            var efeitos = new GameObject("Efeitos").AddComponent<Efeitos>();
            var brilho = TexturaRedonda();
            efeitos.materialParticulas = MaterialParticula("Particulas", false, brilho);
            efeitos.materialClarao = MaterialParticula("Clarao", true, brilho);

            var pontos = new GameObject("Pontos de nascimento").AddComponent<PontosDeNascimento>();
            var posicoes = new[] { new Vector3(-1.4f, 0.05f, -7f), new Vector3(1.4f, 0.05f, -7f), new Vector3(-1.4f, 0.05f, -9f), new Vector3(1.4f, 0.05f, -9f) };
            for (int i = 0; i < posicoes.Length; i++)
            {
                var p = new GameObject($"Ponto {i + 1}").transform;
                p.SetParent(pontos.transform, false);
                p.position = posicoes[i];
            }

            // a câmera de teste vira a câmera do menu (some quando o jogador local nasce)
            var camMenu = Camera.main;
            if (camMenu != null && camMenu.GetComponent<CameraDoMenu>() == null) camMenu.gameObject.AddComponent<CameraDoMenu>();
            if (camMenu != null)
                foreach (Transform filho in camMenu.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(filho.gameObject);
            var testeJogador = GameObject.Find("Jogador (terceira pessoa)");
            if (testeJogador != null) Object.DestroyImmediate(testeJogador);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }
    }
}
