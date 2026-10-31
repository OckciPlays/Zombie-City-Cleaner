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
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Roupas;
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

        /// <summary>Dá nome a uma camada (Project Settings > Tags and Layers), se ela ainda não tiver.</summary>
        static void NomearCamada(int camada, string nome)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var camadas = tags.FindProperty("layers");
            var item = camadas.GetArrayElementAtIndex(camada);
            if (item.stringValue == nome) return;
            if (!string.IsNullOrEmpty(item.stringValue)) Debug.LogWarning($"A camada {camada} já se chama {item.stringValue}; o retrato do jogador usa ela.");
            else
            {
                item.stringValue = nome;
                tags.ApplyModifiedProperties();
            }
        }

        /// <summary>O material do retrato do jogador na tela do inventário (criado na primeira vez).</summary>
        static Material MaterialDoRetrato()
        {
            string caminho = Materiais + "/Retrato.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m != null) return m;
            m = new Material(Shader.Find("ZombieCityCleaner/Retrato")) { name = "Retrato" };
            AssetDatabase.CreateAsset(m, caminho);
            return m;
        }

        /// <summary>
        /// Só o prefab do jogador (a montagem dos zumbis usa isto para dar a vida ao jogador). Precisa das armas preparadas
        /// (<see cref="MontagemDasArmas.Preparar"/>): os braços de cada arma, as malhas e os controladores.
        /// </summary>
        public static GameObject MontarPrefab()
        {
            var raiz = new GameObject("Jogador") { layer = JogadorRede.CamadaJogador };
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
            raiz.AddComponent<VidaJogador>();
            var inventario = raiz.AddComponent<InventarioJogador>();
            var tela = raiz.AddComponent<TelaDoInventario>();
            raiz.AddComponent<FomeESede>();
            var passos = raiz.AddComponent<PassosJogador>();
            raiz.AddComponent<MenuDoDesenvolvedor>();
            raiz.AddComponent<PassageiroDoCarro>();
            var telaDoCapo = raiz.AddComponent<TelaDoCapo>();
            var terceira = raiz.AddComponent<CameraTerceiraPessoa>();
            raiz.AddComponent<PuloDaJanela>();
            raiz.AddComponent<EquipamentoDoJogador>();
            // a tela de criar o personagem (ao entrar na partida), com o parado do mostruário das roupas no estúdio
            raiz.AddComponent<TelaDeCriarPersonagem>().controladorDoEstudio = Carregar<RuntimeAnimatorController>(MontagemDasRoupas.Controlador);

            // os corpos (o que os outros veem; para o dono vira só sombra): um por personagem, só o escolhido ligado
            var corpos = Enumerable.Range(0, Personagens.Quantos).Select(p => MontarCorpo(raiz.transform, p)).ToArray();

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
            cam.cullingMask = ~((1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaRetrato));
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
            // renderizador 1 (PC_Renderer_Bracos): sem a neblina de altura, que já foi aplicada pela câmera do mundo
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dadosBracos.SetRenderer(1);
            dadosCam.cameraStack.Add(camBracos);

            // os braços de primeira pessoa: um modelo por arma ou item (com ele na mão) e os punhos da mão vazia, só o da
            // mão ligado
            var bracos = new GameObject("Bracos") { layer = JogadorRede.CamadaBracos };
            bracos.transform.SetParent(camGo.transform, false);
            int n = Itens.QuantosModelos, nFP = Itens.QuantosModelosFP;
            var modelosFP = new GameObject[nFP];
            var animadoresFP = new Animator[nFP];
            var bocasFP = new Transform[nFP];
            var smrBracos = new SkinnedMeshRenderer[nFP];
            for (int i = 0; i < nFP; i++)
            {
                string modelo = Itens.ModeloFP(i);
                var fp = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(MontagemDasArmas.FbxFP(i)));
                fp.name = "Bracos_" + modelo;
                fp.transform.SetParent(bracos.transform, false);
                Camada(fp, JogadorRede.CamadaBracos);
                foreach (var r in fp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                var anim = fp.GetComponent<Animator>();
                anim.runtimeAnimatorController = Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorFPDa(i));
                anim.applyRootMotion = false;
                modelosFP[i] = fp;
                animadoresFP[i] = anim;
                bocasFP[i] = Buscar(fp.transform, "Boca_FP_" + modelo);
                smrBracos[i] = Buscar(fp.transform, "FP_Bracos_" + modelo).GetComponent<SkinnedMeshRenderer>();
                fp.SetActive(i == 0);
            }

            // lanterna no peito
            var luzGo = new GameObject("Lanterna");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = lanterna.posicaoNoPeito;
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Spot; luz.color = new Color(1f, 0.94f, 0.82f);
            // a intensidade, a abertura e o cookie do facho vêm do LanternaJogador (Awake)
            luz.intensity = lanterna.intensidade; luz.range = 28f; luz.spotAngle = lanterna.abertura; luz.innerSpotAngle = lanterna.aberturaInterna;
            // a sombra ignora o que está a menos de 0,6 m da luz: a pistola e os braços do corpo (que o dono vê só como
            // sombra) ficam bem na frente do peito e cortavam a luz ao meio
            luz.shadows = LightShadows.Soft; luz.shadowNearPlane = 0.6f;

            // ligações
            rede.cameraMundo = cam; rede.cameraBracos = camBracos; rede.ouvido = ouvido;
            rede.bracos = bracos;
            // os corpos; até o personagem chegar pela rede, o primeiro
            rede.corpos = corpos;
            var primeiro = corpos[0];
            rede.animadorCorpo = primeiro.animador;
            rede.renderersCorpo = primeiro.renderers;
            rede.ossoPeito = primeiro.ossoPeito;
            rede.ossoCabeca = primeiro.ossoCabeca;
            rede.naMaoCorpo = primeiro.naMao;
            rede.bocasCorpo = primeiro.bocas;
            rede.controladoresCorpo = primeiro.controladores;
            rede.controladorDasMaos = primeiro.controladorDasMaos;
            rede.controladorDoTaco = primeiro.controladorDoTaco;
            rede.tacoCorpo = primeiro.taco;
            rede.pontaDoTacoCorpo = primeiro.pontaDoTaco;
            // a pele dos braços de primeira pessoa: o material do corpo de cada personagem (o principal em blocos: a textura)
            rede.variacoes = Enumerable.Range(0, Personagens.Quantos).Select(p => Carregar<Material>(MontagemDasArmas.MaterialDoPersonagem(p))).ToArray();
            rede.pintaveis = smrBracos.Cast<Renderer>().ToArray();
            movimento.rede = rede; movimento.cabeca = cabeca;
            arma.rede = rede; arma.movimento = movimento;
            arma.modelos = modelosFP; arma.animadores = animadoresFP; arma.bocas = bocasFP;
            lanterna.rede = rede; lanterna.luz = luz;
            inventario.icones = Enumerable.Range(0, n).Select(i => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.IconeDo(i))).ToArray();
            inventario.fotos = Enumerable.Range(0, n).Select(i => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.FotoDo(i))).ToArray();
            int ne = Itens.QuantosEquipamentos;
            inventario.iconesDeEquipamento = Enumerable.Range(0, ne).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.IconeDoEquipamento(k))).ToArray();
            inventario.fotosDeEquipamento = Enumerable.Range(0, ne).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.FotoDoEquipamento(k))).ToArray();
            int nm = Itens.QuantasMunicoes;
            inventario.iconesDeMunicao = Enumerable.Range(0, nm).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.IconeDaMunicao(k))).ToArray();
            inventario.fotosDeMunicao = Enumerable.Range(0, nm).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.FotoDaMunicao(k))).ToArray();
            int np = Itens.QuantasPecas;
            inventario.iconesDePeca = Enumerable.Range(0, np).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.IconeDaPeca(k))).ToArray();
            inventario.fotosDePeca = Enumerable.Range(0, np).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.FotoDaPeca(k))).ToArray();
            int ns = Itens.QuantosDaSinuca;
            inventario.iconesDaSinuca = Enumerable.Range(0, ns).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.IconeDaSinuca(k))).ToArray();
            inventario.fotosDaSinuca = Enumerable.Range(0, ns).Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>(MontagemDasArmas.FotoDaSinuca(k))).ToArray();
            tela.rede = rede; tela.inventario = inventario; tela.arma = arma;
            telaDoCapo.rede = rede; telaDoCapo.inventario = inventario; telaDoCapo.telaDoInventario = tela;
            terceira.rede = rede; terceira.movimento = movimento; terceira.arma = arma; terceira.capo = telaDoCapo;
            tela.materialDoRetrato = MaterialDoRetrato();
            NomearCamada(JogadorRede.CamadaRetrato, "Retrato");
            passos.rede = rede;

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabJogador);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        /// <summary>
        /// As medidas do corpo de cada personagem em repouso, para o equipamento no corpo (na ordem de Personagens.Modelos:
        /// as costas, o topo da mochila, onde a arma da alça cruza as costas, o cinto, o lado do quadril e o topo da
        /// cabeça). O principal em blocos (28/10/2026): as costas e o quadril medidos na malha, o resto nas juntas dele
        /// (o pescoço, o peito e a cintura da cueca).
        /// </summary>
        static readonly float[][] MedidasDosCorpos =
        {
            new[] { -0.120f, 1.460f, 1.220f, 1.020f, 0.152f, 1.80f },   // o principal em blocos
        };

        /// <summary>
        /// O corpo de um personagem ("Corpo_&lt;modelo&gt;", o Personagem_&lt;modelo&gt;.fbx) com o controlador da pistola, as
        /// armas e os itens do próprio FBX e o <see cref="CorpoDoPersonagem"/>. Só o primeiro fica ligado no prefab.
        /// </summary>
        static CorpoDoPersonagem MontarCorpo(Transform raiz, int p)
        {
            var corpo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(MontagemDasArmas.FbxDoPersonagem(p)));
            corpo.name = "Corpo_" + Personagens.Modelos[p];
            corpo.transform.SetParent(raiz, false);
            var anim = corpo.GetComponent<Animator>();
            anim.runtimeAnimatorController = Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorTPDoPersonagem(p, 0));
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // o que está na mão, no corpo: as armas e os itens do FBX (Arma_<modelo>, presos ao weapon.R com a pegada e o
            // tamanho de cada um), só o da mão ligado, com as peças que mexem (P_<modelo>_*, nos ossos delas, ligadas junto
            // pelo PecasDaArma) e a boca do cano (Boca_<modelo>)
            int n = Itens.QuantosModelos;
            var naMao = new GameObject[n];
            var bocas = new Transform[Armas.Quantas];
            var renderersArmas = new System.Collections.Generic.List<Renderer>();
            var todos = corpo.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < n; i++)
            {
                string modelo = Itens.Modelos[i];
                var a = Buscar(corpo.transform, "Arma_" + modelo).gameObject;
                var pecas = todos.Where(t => t.name.StartsWith($"P_{modelo}_") && !t.IsChildOf(a.transform)).Select(t => t.gameObject).ToArray();
                if (pecas.Length > 0) a.AddComponent<PecasDaArma>().pecas = pecas;
                foreach (var peca in pecas) peca.SetActive(i == 0);
                if (i < Armas.Quantas) bocas[i] = Buscar(corpo.transform, "Boca_" + modelo);
                renderersArmas.AddRange(PecasDaArma.Renderers(a));
                naMao[i] = a;
                a.SetActive(i == 0);
            }

            var c = corpo.AddComponent<CorpoDoPersonagem>();
            c.animador = anim;
            // o corpo vestido (o Plano_Personagem.md da pasta de arte, a parte 1): o corpo do FBX do personagem sai, e no
            // lugar dele entram o corpo das roupas e as peças, presos aos ossos deste corpo
            Buscar(corpo.transform, "Jogador_Corpo").gameObject.SetActive(false);
            c.vestimenta = Vestir(corpo, out var roupas);
            c.renderers = roupas.Concat(renderersArmas).ToArray();
            c.ossoPeito = Buscar(corpo.transform, "chest");
            c.ossoCabeca = Buscar(corpo.transform, "head");
            c.naMao = naMao;
            c.bocas = bocas;
            c.controladores = Enumerable.Range(0, Armas.Quantas).Select(i => Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorTPDoPersonagem(p, i))).ToArray();
            c.controladorDasMaos = Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorTPDasMaosDoPersonagem(p));
            // o taco de sinuca: o vazio e a ponta (sem malha: o TacoNaMao põe o taco de cada cor)
            c.controladorDoTaco = Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorTPDoTacoDoPersonagem(p));
            c.taco = todos.FirstOrDefault(t => t.name == "Arma_Taco");
            c.pontaDoTaco = todos.FirstOrDefault(t => t.name == "Boca_Taco");
            c.controladoresDosItens = Enumerable.Range(Armas.Quantas, n - Armas.Quantas)
                .Select(i => Carregar<RuntimeAnimatorController>(MontagemDasArmas.ControladorTPDoItemDoPersonagem(p, i))).ToArray();
            var m = MedidasDosCorpos[p];
            c.costas = m[0]; c.topoDaMochila = m[1]; c.centroDasCostas = m[2]; c.cinto = m[3]; c.ladoDoQuadril = m[4]; c.topoDaCabeca = m[5];
            corpo.SetActive(p == 0);
            return c;
        }

        /// <summary>
        /// As roupas no corpo do jogador: o corpo das roupas (NPC_Corpo) e todas as peças (Roupa_&lt;nome&gt;) do Roupas.fbx,
        /// num filho "Roupas" do corpo, cada uma presa aos ossos dele pelo nome (o esqueleto é o mesmo: o do personagem
        /// principal), com o material das roupas, e a <see cref="Vestimenta"/>, que liga as vestidas (a aparência que chega
        /// pela rede; no prefab, só o corpo). Precisa da montagem das roupas (o material e o FBX legível).
        /// </summary>
        static Vestimenta Vestir(GameObject corpo, out Renderer[] renderers)
        {
            var modelo = Carregar<GameObject>(MontagemDasRoupas.Fbx);
            var material = Carregar<Material>(MontagemDasRoupas.ArquivoDoMaterial);
            var ossos = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in corpo.GetComponentsInChildren<Transform>(true)) ossos.TryAdd(t.name, t);
            var pasta = new GameObject("Roupas").transform;
            pasta.SetParent(corpo.transform, false);
            var lista = new System.Collections.Generic.List<Renderer>();
            foreach (var smr in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name != Vestimenta.Corpo && !smr.name.StartsWith(Vestimenta.PrefixoDasPecas)) continue;
                var go = new GameObject(smr.name);
                go.transform.SetParent(pasta, false);
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = smr.sharedMesh;
                r.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, smr.sharedMesh.subMeshCount)).ToArray();
                var semOsso = smr.bones.Where(b => b != null && !ossos.ContainsKey(b.name)).Select(b => b.name).ToArray();
                if (semOsso.Length > 0) throw new System.Exception($"{smr.name}: o corpo do jogador não tem os ossos {string.Join(", ", semOsso)}");
                r.bones = smr.bones.Select(b => b != null ? ossos[b.name] : null).ToArray();
                r.rootBone = smr.rootBone != null ? ossos[smr.rootBone.name] : null;
                r.localBounds = smr.localBounds;
                r.skinnedMotionVectors = false;
                go.SetActive(smr.name == Vestimenta.Corpo);
                lista.Add(r);
            }
            var v = corpo.AddComponent<Vestimenta>();
            v.catalogo = Carregar<TextAsset>(MontagemDasRoupas.Json);
            v.npc = "";
            v.tipo = "";
            renderers = lista.ToArray();
            return v;
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

        internal static void MontarCena(GameObject prefab)
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
            MontagemDaCidade.PontosNoBunker(cena);   // com a cidade montada, os pontos vão para a sala comum do bunker

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
