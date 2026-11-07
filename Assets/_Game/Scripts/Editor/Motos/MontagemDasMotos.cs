using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As motos (06/11/2026, a pedido: "vamos pôr as duas motos em jogo, adicionar luz e buzina... para arrumar o motor a
    /// gente precisa agachar e ir em direção ao motor. Na moto, apenas a vela estará comprometida"). Monta os prefabs
    /// `Prefabs/Veiculos/Moto_FatBoy.prefab` (a Fat Boy, dois lugares) e `Moto_Cross.prefab` (a cross, dois lugares, a
    /// pedido), dos FBX de `Art/Veiculos/Motos/` (feitos pelos scripts de `Blender/Motos/`: o moto.py, a v3, e o cross.py,
    /// a v2, com o farol e as pedaleiras do garupa). Cada moto é um <see cref="Carro"/> com <see cref="Carro.moto"/>:
    /// <list type="bullet">
    /// <item>"Modelo" (o que inclina nas curvas): o corpo, a direção (o pivô no eixo da direção, tombada pelo ângulo do
    /// garfo; dentro dela a roda da frente, os punhos, o farol e, na cross, a ignição), a roda de trás, o descanso, os
    /// lugares, as pedaleiras e o motor com o lugar da vela (<see cref="CofreDoMotor"/>, só a vela);</item>
    /// <item>na raiz (em pé): as duas rodas da física (WheelCollider), a colisão, o gatilho de subir
    /// (<see cref="PortaDoCarro"/>: o piloto e, ocupado, a garupa), o gatilho do motor (<see cref="MotorDaMoto"/>, agachado),
    /// as saídas (do lado esquerdo, o do descanso) e os sons (o motor, a buzina e o arranque).</item>
    /// </list>
    /// Menus: Zombie City Cleaner > Veículos > Montar as motos (depois de "Montar as peças do carro (e a chave)", para as
    /// chaves) e > Pôr as motos no mundo e na Demo_Cidade.
    /// </summary>
    public static partial class MontagemDosVeiculos
    {
        public const string PrefabFatBoy = PastaPrefabs + "/Moto_FatBoy.prefab";
        public const string PrefabCross = PastaPrefabs + "/Moto_Cross.prefab";
        const string PastaMotos = "Assets/_Game/Art/Veiculos/Motos";
        const string PastaMateriaisDasMotos = PastaMotos + "/Materiais";

        /// <summary>As medidas e os números de uma moto (eixos da Unity: x direita, y cima, z frente; o chão em 0 e o meio entre os eixos em z = 0).</summary>
        class DadosDaMoto
        {
            public string nome, fbx, prefixo, nomeNoJogo, ondeFicaAIgnicao;
            public float eixo, raioF, raioT, rake;
            public Vector3 pivo, rodaNaDirecao, descanso, peDoDescanso;
            public Vector3 lugarPiloto, lugarGarupa;
            public Vector3[] pes;              // o piloto direito e esquerdo, o garupa direito e esquerdo
            public Vector3 punho;              // o direito (o esquerdo é o espelho)
            public Vector3 farol;
            public float farolBaixo;
            public bool ignicaoNaDirecao;
            public Vector3 ignicao, ignicaoDentro, ignicaoCima;
            public Vector3 vela, velaPara, olhar, olharPara, pontoDoMotor, tampaDoTanque, centro;
            public Vector3 corpoCentro, corpoTamanho, frenteCentro, frenteTamanho;
            public Vector3 subirCentro, subirTamanho, motorCentro, motorTamanho;
            public Vector3 buzina, somDoMotor;
            public float saidaZ0, saidaZ1;
            public float suspensao, mola, amortecedor, massa;
            public float torque, velocidadeMaxima, freio, freioDeMao, giroMaximo, giroRapido, tronco, inclinacao;
            public float tanque, gastoParado, gastoNoMaximo, reserva, combustivel;
            public Loot.Item chave;
            public Color chaveiro;
            // os sons (Ferramentas/motos.py): o nome nos arquivos, o giro de cada laço (vezes a marcha lenta; os mesmos
            // GIROS de lá) e o giro com a rotação no máximo
            public string som;
            public float[] giros;
            public float giroNoMaximo;
        }

        const string PastaSons = "Assets/_Game/Audio/Motos";

        static readonly DadosDaMoto FatBoy = new DadosDaMoto
        {
            nome = "Moto_FatBoy", fbx = PastaMotos + "/Moto_FatBoy.fbx", prefixo = "Moto", nomeNoJogo = "Fat Boy",
            ondeFicaAIgnicao = "no tanque, atrás do velocímetro",
            eixo = 0.8325f, raioF = 0.325f, raioT = 0.325f, rake = 32f,
            pivo = new Vector3(0f, 0.985f, 0.34344f), rodaNaDirecao = new Vector3(0f, -0.81887f, 0.065f),
            descanso = new Vector3(-0.13f, 0.155f, 0.10f), peDoDescanso = new Vector3(-0.29f, 0.035f, 0.17f),
            // o quadril: 9 cm acima do assento (o piloto no ponto mais baixo, o garupa no degrau de trás)
            lugarPiloto = new Vector3(0f, 0.765f, -0.30f), lugarGarupa = new Vector3(0f, 0.885f, -0.68f),
            pes = new[] { new Vector3(0.31f, 0.32f, 0.22f), new Vector3(-0.31f, 0.32f, 0.22f), new Vector3(0.24f, 0.578f, -0.40f), new Vector3(-0.24f, 0.578f, -0.40f) },
            punho = new Vector3(0.355f, 1.128f, 0.146f),
            farol = new Vector3(0f, 0.885f, 0.67f), farolBaixo = 5f,
            ignicaoNaDirecao = false,
            ignicao = new Vector3(0f, 0.838f, -0.045f), ignicaoDentro = new Vector3(0f, -0.76f, 0.65f), ignicaoCima = new Vector3(0f, 0.65f, 0.76f),
            // a vela: do lado direito do cabeçote da frente, na frente do filtro de ar
            vela = new Vector3(0.095f, 0.66f, 0.205f), velaPara = new Vector3(1f, 0.35f, 0.25f),
            olhar = new Vector3(0.62f, 0.74f, 0.50f), olharPara = new Vector3(0.12f, 0.665f, 0.21f),
            pontoDoMotor = new Vector3(0f, 0.45f, 0.08f), tampaDoTanque = new Vector3(0.066f, 0.955f, 0.20f),
            centro = new Vector3(0f, 0.45f, -0.05f),
            corpoCentro = new Vector3(0f, 0.63f, -0.25f), corpoTamanho = new Vector3(0.46f, 0.66f, 1.75f),
            frenteCentro = new Vector3(0f, 0.88f, 0.58f), frenteTamanho = new Vector3(0.36f, 0.42f, 0.34f),
            subirCentro = new Vector3(0f, 0.97f, -0.38f), subirTamanho = new Vector3(0.66f, 0.42f, 1.15f),
            motorCentro = new Vector3(0f, 0.44f, 0.06f), motorTamanho = new Vector3(0.62f, 0.56f, 0.86f),
            buzina = new Vector3(0f, 0.55f, 0.45f), somDoMotor = new Vector3(0f, 0.45f, 0.05f),
            saidaZ0 = -0.25f, saidaZ1 = -0.85f,
            suspensao = 0.10f, mola = 26000f, amortecedor = 2800f, massa = 300f,
            torque = 520f, velocidadeMaxima = 34f, freio = 900f, freioDeMao = 650f, giroMaximo = 30f, giroRapido = 5f, tronco = 3f, inclinacao = 34f,
            tanque = 18.9f, gastoParado = 0.05f, gastoNoMaximo = 1.1f, reserva = 3.5f, combustivel = 0.55f,
            chave = Loot.Item.ChaveDaFatBoy, chaveiro = new Color(0.353f, 0.204f, 0.141f),
            // o V2: a marcha lenta a 10,9 ciclos por segundo, até ~4 vezes isso
            som = "FatBoy", giros = new[] { 1.0f, 1.6f, 2.5f, 3.6f }, giroNoMaximo = 4.0f,
        };

        static readonly DadosDaMoto Cross = new DadosDaMoto
        {
            nome = "Moto_Cross", fbx = PastaMotos + "/Moto_Cross.fbx", prefixo = "Cross", nomeNoJogo = "Moto cross",
            ondeFicaAIgnicao = "no meio do guidão",
            eixo = 0.7425f, raioF = 0.345f, raioT = 0.335f, rake = 26.5f,
            pivo = new Vector3(0f, 1.14f, 0.32154f), rodaNaDirecao = new Vector3(0f, -0.89930f, 0.022f),
            descanso = new Vector3(-0.105f, 0.365f, -0.29f), peDoDescanso = new Vector3(-0.30f, 0.012f, -0.16f),
            lugarPiloto = new Vector3(0f, 1.04f, -0.25f), lugarGarupa = new Vector3(0f, 1.045f, -0.56f),
            pes = new[] { new Vector3(0.165f, 0.397f, -0.235f), new Vector3(-0.165f, 0.397f, -0.235f), new Vector3(0.165f, 0.531f, -0.43f), new Vector3(-0.165f, 0.531f, -0.43f) },
            punho = new Vector3(0.321f, 1.238f, 0.230f),
            farol = new Vector3(0f, 1.09f, 0.46f), farolBaixo = 4f,
            // a ignição em cima da mesa de cima, na frente dos suportes do guidão (vira com o guidão)
            ignicaoNaDirecao = true,
            ignicao = new Vector3(0f, 1.192f, 0.36f), ignicaoDentro = new Vector3(0f, -0.895f, 0.446f), ignicaoCima = new Vector3(0f, 0.446f, 0.895f),
            // a vela: do lado direito do cabeçote, que aparece na janela da carenagem (v2)
            vela = new Vector3(0.082f, 0.75f, 0.05f), velaPara = new Vector3(1f, 0.2f, 0.1f),
            olhar = new Vector3(0.56f, 0.78f, 0.25f), olharPara = new Vector3(0.12f, 0.755f, 0.06f),
            pontoDoMotor = new Vector3(0f, 0.6f, 0.05f), tampaDoTanque = new Vector3(0f, 1.05f, 0.165f),
            centro = new Vector3(0f, 0.52f, -0.05f),
            corpoCentro = new Vector3(0f, 0.70f, -0.25f), corpoTamanho = new Vector3(0.40f, 0.62f, 1.55f),
            frenteCentro = new Vector3(0f, 0.93f, 0.62f), frenteTamanho = new Vector3(0.22f, 0.14f, 0.60f),
            subirCentro = new Vector3(0f, 1.14f, -0.35f), subirTamanho = new Vector3(0.60f, 0.36f, 1.05f),
            motorCentro = new Vector3(0f, 0.60f, 0.02f), motorTamanho = new Vector3(0.50f, 0.50f, 0.62f),
            buzina = new Vector3(0f, 0.85f, 0.45f), somDoMotor = new Vector3(0f, 0.55f, 0.05f),
            saidaZ0 = -0.20f, saidaZ1 = -0.75f,
            suspensao = 0.16f, mola = 18000f, amortecedor = 2000f, massa = 150f,
            torque = 330f, velocidadeMaxima = 30f, freio = 600f, freioDeMao = 420f, giroMaximo = 32f, giroRapido = 5.5f, tronco = 16f, inclinacao = 40f,
            tanque = 7.2f, gastoParado = 0.03f, gastoNoMaximo = 0.6f, reserva = 1.2f, combustivel = 0.6f,
            chave = Loot.Item.ChaveDaCross, chaveiro = new Color(0.878f, 0.478f, 0.125f),
            // o monocilíndrico: o ponto morto a 15,5 explosões por segundo, até 10,5 vezes isso (era 13,5, como as aceleradas
            // do vídeo, e o som ficava agudo e irritante andando; depois 9, grave demais; 10,5 é o meio, a pedido)
            som = "Cross", giros = new[] { 1.0f, 2.0f, 3.4f, 5.5f, 8.5f }, giroNoMaximo = 10.5f,
        };

        // os materiais das motos (URP Lit): a cor (linear, como no Blender), o metal e a suavidade. O metal ficou mais escuro
        // e menos liso depois do primeiro teste (06/11/2026, a pedido: "o brilho delas está muito alto, o cromo das partes
        // metálicas"): era o cromado 0,93 / 1 / 0,92, o alumínio 0,77 / 1 / 0,75, o titânio 0,62 / 1 / 0,68 e o escuro
        // 0,55 / 0,55 de metal e suavidade
        static readonly Dictionary<string, (Color cor, float metal, float suave)> CoresDasMotos = new Dictionary<string, (Color, float, float)>
        {
            { "Pintura", (new Color(0.030f, 0.030f, 0.032f), 0.15f, 0.5f) },
            { "Preto", (new Color(0.011f, 0.011f, 0.012f), 0f, 0.55f) },
            { "Escuro", (new Color(0.038f, 0.038f, 0.042f), 0.4f, 0.45f) },
            { "Cromado", (new Color(0.50f, 0.50f, 0.52f), 0.9f, 0.72f) },
            { "Aluminio", (new Color(0.42f, 0.42f, 0.44f), 0.85f, 0.55f) },
            { "Borracha", (new Color(0.013f, 0.013f, 0.013f), 0f, 0.12f) },
            { "Couro", (new Color(0.016f, 0.015f, 0.014f), 0f, 0.45f) },
            { "Lente", (new Color(1.0f, 0.33f, 0.02f), 0f, 0.85f) },
            { "Farol", (new Color(0.92f, 0.95f, 1.0f), 0f, 0.95f) },
            { "Lanterna", (new Color(0.75f, 0.02f, 0.02f), 0f, 0.85f) },
            { "Placa", (new Color(0.86f, 0.86f, 0.84f), 0f, 0.55f) },
            { "Laranja", (new Color(0.95f, 0.15f, 0.004f), 0f, 0.62f) },
            { "Branco", (new Color(0.80f, 0.80f, 0.79f), 0f, 0.6f) },
            { "Roxo", (new Color(0.10f, 0.022f, 0.26f), 0f, 0.62f) },
            { "Titanio", (new Color(0.40f, 0.36f, 0.31f), 0.85f, 0.5f) },
        };

        [MenuItem("Zombie City Cleaner/Veículos/Montar as motos")]
        public static void MontarAsMotos()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Motos] Saia do Play para montar as motos.");
                return;
            }
            Directory.CreateDirectory(PastaMateriaisDasMotos);
            Directory.CreateDirectory(PastaPrefabs);
            Camada();
            var m = MateriaisDoCarroProntos();
            var mats = MateriaisDasMotos();
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            foreach (var d in new[] { FatBoy, Cross })
            {
                ImportarMoto(d, mats);
                var prefab = MontarMoto(d, m, mats);
                if (lista != null && !lista.PrefabList.Any(p => p.Prefab == prefab))
                {
                    lista.Add(new NetworkPrefab { Prefab = prefab });
                    EditorUtility.SetDirty(lista);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Motos] As motos montadas: " + PrefabFatBoy + " e " + PrefabCross + " (na lista de prefabs da rede).");
        }

        /// <summary>
        /// No `MundoDaPartida` da cena Mundo, as motos (o anfitrião cria perto da partida); na Demo_Cidade, as duas na rua,
        /// atrás do cupê. Volta para a cena que estava aberta.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Veículos/Pôr as motos no mundo e na Demo_Cidade")]
        public static void PorAsMotosNasCenas()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var fatboy = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFatBoy);
            var cross = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCross);
            if (fatboy == null || cross == null)
            {
                Debug.LogWarning("[Motos] Monte as motos antes (Veículos > Montar as motos).");
                return;
            }
            string antes = EditorSceneManager.GetActiveScene().path;

            var mundo = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Mundo.unity", OpenSceneMode.Single);
            var partida = mundo.GetRootGameObjects().Select(g => g.GetComponentInChildren<MundoDaPartida>(true)).FirstOrDefault(p => p != null);
            if (partida != null)
            {
                partida.motos = new[] { fatboy, cross };
                partida.chavesDasMotos = new[] { FatBoy.chave, Cross.chave };
                EditorUtility.SetDirty(partida);
                EditorSceneManager.MarkSceneDirty(mundo);
                EditorSceneManager.SaveScene(mundo);
            }
            else Debug.LogWarning("[Motos] Sem o MundoDaPartida na cena Mundo.");

            var demo = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Demo_Cidade.unity", OpenSceneMode.Single);
            foreach (var velho in demo.GetRootGameObjects().Where(g => g.name == FatBoy.nome || g.name == Cross.nome).ToArray()) Object.DestroyImmediate(velho);
            var perua = demo.GetRootGameObjects().FirstOrDefault(g => g.name == "Carro");
            if (perua != null)
            {
                var t = perua.transform;
                int i = 0;
                foreach (var prefab in new[] { fatboy, cross })
                {
                    var moto = (GameObject)PrefabUtility.InstantiatePrefab(prefab, demo);
                    moto.name = prefab.name;
                    // atrás do cupê (que fica 6,5 m atrás da perua), lado a lado
                    moto.transform.SetPositionAndRotation(t.position - t.forward * 11.5f + t.right * (i == 0 ? -0.9f : 0.9f), t.rotation);
                    i++;
                }
                EditorSceneManager.MarkSceneDirty(demo);
                EditorSceneManager.SaveScene(demo);
            }
            else Debug.LogWarning("[Motos] Sem a perua na Demo_Cidade: as motos não foram postas lá.");

            if (!string.IsNullOrEmpty(antes) && antes != demo.path) EditorSceneManager.OpenScene(antes, OpenSceneMode.Single);
            Debug.Log("[Motos] As motos estão no MundoDaPartida (cena Mundo) e na Demo_Cidade.");
        }

        // ------------------------------------------------------------------------------------------ os materiais

        /// <summary>Os materiais do carro que as motos usam (as lentes acesas, o fantasma da vela e a ignição), já feitos pela "Montar veículos".</summary>
        static Mats MateriaisDoCarroProntos()
        {
            Material Ler(string nome)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PastaMateriais}/{nome}.mat");
                if (mat == null) throw new FileNotFoundException("Rode a \"Montar veículos\" antes (os materiais do carro).", nome);
                return mat;
            }
            var r = new Mats
            {
                cidade = AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade),
                farolAceso = Ler("FarolAceso"), lanternaAcesa = Ler("LanternaAcesa"), fantasma = Ler("Fantasma"),
                chaveMetal = Ler("ChaveMetal"), chavePlastico = Ler("ChavePlastico"), chaveiro = Ler("ChaveiroDaPerua"),
                miolo = Ler("IgnicaoMiolo"), anelApagado = Ler("IgnicaoAnel"), anelAceso = Ler("IgnicaoAnelAceso"), marca = Ler("IgnicaoMarca"),
            };
            if (r.cidade == null) throw new FileNotFoundException("Sem o material da cidade.", MaterialCidade);
            return r;
        }

        static Dictionary<string, Material> MateriaisDasMotos()
        {
            var r = new Dictionary<string, Material>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (nome, (cor, metal, suave)) in CoresDasMotos.Select(kv => (kv.Key, kv.Value)))
            {
                string caminho = $"{PastaMateriaisDasMotos}/Moto_{nome}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, caminho);
                }
                mat.shader = lit;
                mat.shaderKeywords = new string[0];
                mat.SetColor("_BaseColor", cor.gamma);   // a cor do Blender é linear; a do material, sRGB
                mat.SetFloat("_Metallic", metal);
                mat.SetFloat("_Smoothness", suave);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                r[nome] = mat;
            }
            return r;
        }

        static void ImportarMoto(DadosDaMoto d, Dictionary<string, Material> mats)
        {
            var imp = AssetImporter.GetAtPath(d.fbx) as ModelImporter;
            if (imp == null) throw new FileNotFoundException($"{d.fbx} não encontrado (copie o FBX do Blender/Motos).", d.fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.SaveAndReimport();
        }

        static MeshRenderer VisualDaMoto(Transform pai, string nome, DadosDaMoto d, string peca, Vector3 pos, Quaternion rot)
        {
            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(d.fbx);
            var t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == peca);
            if (t == null) throw new System.Exception($"A peça {peca} não está no {d.fbx}.");
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = t.GetComponent<MeshRenderer>().sharedMaterials;
            return r;
        }

        static int Submesh(MeshRenderer r, Material mat)
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) if (ms[i] == mat) return i;
            return -1;
        }

        static Transform Ponto(Transform pai, string nome, Vector3 noMundo)
        {
            var t = new GameObject(nome) { layer = Carro.CamadaVeiculo }.transform;
            t.SetParent(pai, false);
            t.position = noMundo;   // (a raiz na origem: o mundo é o espaço da moto)
            return t;
        }

        /// <summary>Um som da moto (`Audio/Motos/<nome>.wav`): mono, descomprimido ao carregar e em PCM (o laço sem emenda).</summary>
        static AudioClip SomDaMoto(string nome)
        {
            string caminho = $"{PastaSons}/{nome}.wav";
            var imp = AssetImporter.GetAtPath(caminho) as AudioImporter;
            if (imp == null) throw new FileNotFoundException("Rode o Ferramentas/motos.py antes (os sons das motos).", caminho);
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(caminho);
        }

        static AudioSource Som(Transform pai, string nome, Vector3 pos, float min, float max)
        {
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0.3f;
            s.volume = 0f;
            return s;
        }

        // ------------------------------------------------------------------------------------------ o prefab

        static GameObject MontarMoto(DadosDaMoto d, Mats m, Dictionary<string, Material> mats)
        {
            var raiz = new GameObject(d.nome) { layer = Carro.CamadaVeiculo };
            var corpo = raiz.AddComponent<Rigidbody>();
            corpo.mass = d.massa;
            corpo.linearDamping = 0.03f;
            corpo.angularDamping = 0.6f;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var no = raiz.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            var nt = raiz.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.UseQuaternionSynchronization = true;
            nt.Interpolate = true;
            raiz.AddComponent<NetworkRigidbody>();
            var carro = raiz.AddComponent<Carro>();
            var obstaculo = raiz.AddComponent<NavMeshObstacle>();
            obstaculo.shape = NavMeshObstacleShape.Box;
            obstaculo.center = new Vector3(0f, 0.6f, -0.12f);
            obstaculo.size = new Vector3(0.8f, 1.2f, 2.4f);
            obstaculo.carving = true;
            obstaculo.carveOnlyStationary = true;
            raiz.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            // os números
            carro.moto = true;
            carro.nomeDoVeiculo = d.nomeNoJogo;
            carro.ondeFicaAIgnicao = d.ondeFicaAIgnicao;
            carro.pontoDoMotor = d.pontoDoMotor;
            carro.centroDaMoto = d.centro;
            carro.troncoDoPiloto = d.tronco;
            carro.inclinacaoMaxima = d.inclinacao;
            carro.lugaresUsados = 2;
            carro.chaveDoVeiculo = d.chave;
            carro.velaInicial = 0;   // "na moto, apenas a vela estará comprometida": ela chega com a vela queimada
            carro.bateriaInicial = 100;
            carro.radiadorInicial = 100;
            carro.pneusIniciais = new[] { 100, 100, 100, 100 };
            carro.macacoInicial = false;
            carro.estepeInicial = -1;
            carro.combustivel = d.combustivel;
            carro.torque = d.torque;
            carro.velocidadeMaxima = d.velocidadeMaxima;
            carro.fimDasMarchas = d.velocidadeMaxima;
            carro.velocidadeDeRe = 1.6f;
            carro.freio = d.freio;
            carro.freioDeMao = d.freioDeMao;
            carro.giroMaximo = d.giroMaximo;
            carro.giroNaVelocidadeMaxima = d.giroRapido;
            carro.velocidadeDoVolante = 100f;
            carro.desgaste = new AjustesDoDesgaste { tanque = d.tanque, gastoParado = d.gastoParado, gastoNoMaximo = d.gastoNoMaximo, reserva = d.reserva };
            carro.tampaDoTanque = d.tampaDoTanque;
            carro.desenharTampa = false;

            // o que inclina: o corpo, a direção (com a roda da frente, os punhos e o farol), a roda de trás e o descanso
            var modelo = new GameObject("Modelo") { layer = Carro.CamadaVeiculo }.transform;
            modelo.SetParent(raiz.transform, false);
            carro.modelo = modelo;
            var corpoVisto = VisualDaMoto(modelo, "Corpo", d, d.prefixo + "_Corpo", Vector3.zero, Quaternion.identity);
            carro.lanterna2 = corpoVisto;
            carro.submeshLanterna2 = Submesh(corpoVisto, mats["Lanterna"]);
            carro.lanterna2Acesa = m.lanternaAcesa;
            var direcao = VisualDaMoto(modelo, "Direção", d, d.prefixo + "_Direcao", d.pivo, Quaternion.Euler(-d.rake, 0f, 0f));
            carro.guidao = direcao.transform;
            carro.carroceria = direcao;
            carro.submeshFarol = Submesh(direcao, mats["Farol"]);
            carro.submeshLanterna = -1;
            carro.farolAceso = m.farolAceso;
            carro.lanternaAceso = m.lanternaAcesa;
            var rodaFrente = VisualDaMoto(direcao.transform, "Roda da frente", d, d.prefixo + "_RodaFrente", d.rodaNaDirecao, Quaternion.identity);
            var rodaTras = VisualDaMoto(modelo, "Roda de trás", d, d.prefixo + "_RodaTras", new Vector3(0f, d.raioT, -d.eixo), Quaternion.identity);
            carro.rodasVisuais = new[] { rodaFrente.transform, rodaTras.transform };
            var descanso = VisualDaMoto(modelo, "Descanso", d, d.prefixo + "_Descanso", d.descanso, Quaternion.identity);
            carro.descanso = descanso.transform;
            carro.peDoDescanso = d.peDoDescanso;

            // os lugares (o quadril de quem senta), as pedaleiras e os punhos
            var lugares = new GameObject("Lugares") { layer = Carro.CamadaVeiculo }.transform;
            lugares.SetParent(modelo, false);
            carro.assentos = new[] { Ponto(lugares, "Lugar 0 (piloto)", d.lugarPiloto), Ponto(lugares, "Lugar 1 (garupa)", d.lugarGarupa) };
            carro.pedaleiras = new Transform[4];
            string[] nomesDosPes = { "Pé do piloto D", "Pé do piloto E", "Pé do garupa D", "Pé do garupa E" };
            for (int i = 0; i < 4; i++) carro.pedaleiras[i] = Ponto(lugares, nomesDosPes[i], d.pes[i]);
            carro.punhos = new[]
            {
                Ponto(direcao.transform, "Punho D", d.punho),
                Ponto(direcao.transform, "Punho E", new Vector3(-d.punho.x, d.punho.y, d.punho.z)),
            };

            // o farol (vira com o guidão), desligado no começo; mais forte e mais longe desde 06/11/2026 (eram 48, 30 m, 62°/30°)
            var farol = Ponto(direcao.transform, "Farol", d.farol);
            farol.rotation = Quaternion.Euler(d.farolBaixo, 0f, 0f);
            var luz = farol.gameObject.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.color = new Color(1f, 0.93f, 0.8f);
            luz.intensity = 130f;
            luz.range = 54f;
            luz.spotAngle = 70f;
            luz.innerSpotAngle = 38f;
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            carro.farois = new[] { luz };

            // a ignição (a do carro, com o chaveiro da moto), olhando para dentro do tanque ou da mesa do guidão
            Ignicao(d.ignicaoNaDirecao ? direcao.transform : modelo, carro, m);
            carro.ignicao.SetPositionAndRotation(d.ignicao, Quaternion.LookRotation(d.ignicaoDentro.normalized, d.ignicaoCima.normalized));
            var chaveiro = Lit("ChaveiroDa" + (d.chave == Loot.Item.ChaveDaFatBoy ? "FatBoy" : "Cross"), d.chaveiro, 0.45f, null);
            foreach (var p in carro.pecasDaChave) if (p != null && p.name == "Chaveiro") p.sharedMaterial = chaveiro;

            // o motor: só o lugar da vela, que se troca agachado
            carro.cofre = MotorDaMotoEm(modelo, carro, m, d);

            // na raiz (em pé): as rodas da física, a colisão, os gatilhos, as saídas e os sons
            var fisica = new GameObject("Rodas (física)") { layer = Carro.CamadaVeiculo }.transform;
            fisica.SetParent(raiz.transform, false);
            carro.rodas = new[]
            {
                Roda(fisica, "Frente", new Vector3(0f, d.raioF, d.eixo), d.raioF, d),
                Roda(fisica, "Trás", new Vector3(0f, d.raioT, -d.eixo), d.raioT, d),
            };
            var colisao = new GameObject("Colisão") { layer = Carro.CamadaVeiculo }.transform;
            colisao.SetParent(raiz.transform, false);
            Caixa(colisao, "Corpo", d.corpoCentro, d.corpoTamanho, Quaternion.identity);
            Caixa(colisao, "Frente", d.frenteCentro, d.frenteTamanho, Quaternion.identity);

            var subir = new GameObject("Subir (mira)") { layer = Carro.CamadaVeiculo };
            subir.transform.SetParent(raiz.transform, false);
            var bs = subir.AddComponent<BoxCollider>();
            bs.isTrigger = true;
            bs.center = d.subirCentro;
            bs.size = d.subirTamanho;
            var porta = subir.AddComponent<PortaDoCarro>();
            porta.carro = carro;
            porta.lugar = 0;
            porta.lugarDeTras = 1;
            carro.portas = new[] { porta, porta };

            var motorMira = new GameObject("Motor (mira)") { layer = Carro.CamadaVeiculo };
            motorMira.transform.SetParent(raiz.transform, false);
            var bm = motorMira.AddComponent<BoxCollider>();
            bm.isTrigger = true;
            bm.center = d.motorCentro;
            bm.size = d.motorTamanho;
            motorMira.AddComponent<MotorDaMoto>().carro = carro;

            var saidas = new GameObject("Saídas") { layer = Carro.CamadaVeiculo }.transform;
            saidas.SetParent(raiz.transform, false);
            carro.saidas = new[] { Ponto(saidas, "Saída 0", new Vector3(-0.9f, 0f, d.saidaZ0)), Ponto(saidas, "Saída 1", new Vector3(-0.9f, 0f, d.saidaZ1)) };

            carro.buzina = Som(raiz.transform, "Buzina", d.buzina, 4f, 70f);
            carro.motor = Som(raiz.transform, "Motor", d.somDoMotor, 3f, 45f);
            carro.arranque = Som(raiz.transform, "Arranque", d.somDoMotor + new Vector3(0.1f, -0.1f, 0f), 3f, 30f);
            // os sons da moto (Ferramentas/motos.py): os laços do motor, o motor pegando e a buzina
            carro.lacosDoMotor = d.giros.Select((_, i) => SomDaMoto($"{d.som}_Motor_{i}")).ToArray();
            carro.giroDosLacos = d.giros;
            carro.giroNoMaximo = d.giroNoMaximo;
            carro.pegarProprio = SomDaMoto(d.som + "_Pegar");
            carro.motor.clip = carro.lacosDoMotor[0];
            carro.buzina.clip = SomDaMoto(d.som + "_Buzina");

            string caminho = d == FatBoy ? PrefabFatBoy : PrefabCross;
            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        // uma roda da física: pendura da posição do WheelCollider; em repouso (na metade do curso), fica no lugar do modelo.
        // As forças dos pneus na altura do centro de massa: a moto não tomba para fora nas curvas (Carro.Moto.cs)
        static WheelCollider Roda(Transform pai, string nome, Vector3 eixo, float raio, DadosDaMoto d)
        {
            const float alvo = 0.5f;
            var w = new GameObject("Roda " + nome) { layer = Carro.CamadaVeiculo };
            w.transform.SetParent(pai, false);
            w.transform.localPosition = eixo + Vector3.up * (d.suspensao * (1f - alvo));
            var wc = w.AddComponent<WheelCollider>();
            wc.radius = raio;
            wc.mass = 15f;
            wc.wheelDampingRate = 0.35f;
            wc.suspensionDistance = d.suspensao;
            wc.forceAppPointDistance = d.centro.y;
            wc.suspensionSpring = new JointSpring { spring = d.mola, damper = d.amortecedor, targetPosition = alvo };
            wc.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.6f };
            wc.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.25f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 2f };
            return wc;
        }

        /// <summary>
        /// O motor da moto em 3D para a tela da vela: o lugar da vela (a malha do item, a apagada e o gatilho do mouse, na
        /// camada Ignore Raycast), de onde se olha (agachado, do lado direito) e a luz de quem olha.
        /// </summary>
        static CofreDoMotor MotorDaMotoEm(Transform pai, Carro carro, Mats m, DadosDaMoto d)
        {
            var raiz = new GameObject("Motor (vela)") { layer = Carro.CamadaVeiculo };
            raiz.transform.SetParent(pai, false);
            var cofre = raiz.AddComponent<CofreDoMotor>();
            cofre.dono = carro;
            string caminho = MontagemDasArmas.MalhaDaPeca(PecasDoMotor.Vela);
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (malha == null) throw new FileNotFoundException("Rode a montagem das armas antes (as malhas das peças).", caminho);
            var vela = new GameObject("Vela") { layer = Carro.CamadaVeiculo };
            vela.transform.SetParent(raiz.transform, false);
            vela.transform.SetPositionAndRotation(d.vela, Quaternion.FromToRotation(Vector3.up, d.velaPara.normalized));
            vela.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = vela.AddComponent<MeshRenderer>();
            r.sharedMaterial = m.cidade;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var f = new GameObject("Apagada") { layer = Carro.CamadaVeiculo };
            f.transform.SetParent(vela.transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = malha;
            var fr = f.AddComponent<MeshRenderer>();
            fr.sharedMaterial = m.fantasma;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.receiveShadows = false;
            fr.enabled = false;
            var g = new GameObject("Gatilho") { layer = 2 };
            g.transform.SetParent(vela.transform, false);
            var bc = g.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = malha.bounds.center;
            bc.size = Vector3.Max(malha.bounds.size + Vector3.one * 0.04f, Vector3.one * 0.08f);
            cofre.pecas = new[] { r };
            cofre.fantasmas = new[] { fr };
            cofre.lugares = new Collider[] { bc };

            var olhar = new GameObject("Olhar").transform;
            olhar.SetParent(raiz.transform, false);
            olhar.SetPositionAndRotation(d.olhar, Quaternion.LookRotation(d.olharPara - d.olhar));
            cofre.olhar = olhar;
            var luz = new GameObject("Luz de quem olha").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.transform.position = d.olhar + new Vector3(0f, 0.06f, 0f);
            luz.type = LightType.Point;
            luz.range = 1.8f;
            luz.intensity = 0.25f;
            luz.color = new Color(1f, 0.94f, 0.84f);
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            cofre.luz = luz;
            return cofre;
        }
    }
}
