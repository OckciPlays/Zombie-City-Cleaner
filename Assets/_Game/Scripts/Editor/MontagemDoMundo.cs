using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena do jogo de verdade, `Scenes/Mundo.unity` (Plano_Mapa, Partes M1 a M5): o mundo gerado pela semente, com o
    /// relevo, as serras, o lago, as estradas e as cidades. Menu Zombie City Cleaner > Mundo > Montar a cena do mundo. Refaz
    /// a cena do zero:
    ///  - a luz e o volume, e por cima o clima da demo (<see cref="MontagemDoClima"/>: o ciclo do dia em rede, o ambiente,
    ///    a neblina e as partículas);
    ///  - a câmera do menu e a rede (<see cref="MontagemDaRede.MontarCena"/>, com o prefab do jogador de hoje), com a
    ///    aprovação de conexão ligada: o personagem nasce quando o mundo fica pronto na máquina de cada um;
    ///  - os itens no chão (<see cref="MontagemDasArmas.MontarItensNoMundo"/>, ainda sem lugares);
    ///  - o "Mundo": o NetworkObject com o <see cref="MundoDaPartida"/> (com o carro, que o anfitrião põe ao lado da
    ///    partida), o <see cref="TerrenoDoMundo"/> (os materiais do chão e da água, de <see cref="TexturasDoChao"/>), a
    ///    <see cref="TelaDeCarregamento"/>, o <see cref="MapaDoMundo"/> (o M), os <see cref="ObjetosDoMundo"/> (a beira das
    ///    estradas e as ruas das cidades, com os modelos do Cidade_Rua.fbx e o material das letras das placas) e a
    ///    <see cref="FlorestaDoMundo"/> (Parte M9a: as plantas do Cidade_Arvores.fbx e o material da vegetação, por
    ///    <see cref="PorAFloresta"/>, que também põe a floresta numa cena já montada) e a <see cref="MobiliaDoMundo"/> (os
    ///    móveis do Cidade_Mobilia.fbx, por <see cref="PorAMobilia"/>). Sem as luzes da cidade: anos depois do
    ///    apocalipse, não há energia (os postes ficam apagados e as janelas escuras).
    /// A cena fica fora das Build Settings por enquanto (a demo continua sendo a Demo_Cidade).
    /// </summary>
    public static class MontagemDoMundo
    {
        public const string Cena = "Assets/_Game/Scenes/Mundo.unity";

        [MenuItem("Zombie City Cleaner/Mundo/Montar a cena do mundo")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena do mundo.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(cena, Cena);   // com o caminho, as montagens de baixo salvam sem perguntar

            // o que a montagem do clima espera achar: a luz e o volume da noite
            var sol = new GameObject("Sol e lua").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var noite = new GameObject("Pós-processamento (noite)").AddComponent<Volume>();
            noite.isGlobal = true;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;

            // a câmera do menu (some quando o jogador local nasce), olhando o horizonte na neblina
            var camera = new GameObject("Câmera do menu") { tag = "MainCamera" };
            camera.transform.SetPositionAndRotation(new Vector3(0f, 6f, -20f), Quaternion.Euler(8f, 0f, 0f));
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.AddComponent<AudioListener>();

            MontagemDoClima.Montar();
            var jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            MontagemDaRede.MontarCena(jogador);
            var rede = cena.GetRootGameObjects().First(g => g.name == "Rede").GetComponent<NetworkManager>();
            rede.NetworkConfig.ConnectionApproval = true;
            MontagemDasArmas.MontarItensNoMundo(cena);

            // o carro que se dirige: o anfitrião cria ao lado da partida, então ele vai para a lista dos prefabs da rede
            var carro = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Veiculos/Carro.prefab");
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (carro != null && lista != null && !lista.PrefabList.Any(p => p.Prefab == carro))
            {
                lista.Add(new NetworkPrefab { Prefab = carro });
                EditorUtility.SetDirty(lista);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao) == null) TexturasDoChao.Gerar();
            var mundo = new GameObject("Mundo");
            mundo.AddComponent<NetworkObject>();
            var partida = mundo.AddComponent<MundoDaPartida>();
            var terreno = mundo.AddComponent<TerrenoDoMundo>();
            terreno.materialDoChao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            terreno.materialDaAgua = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            partida.terreno = terreno;
            partida.carro = carro;
            mundo.AddComponent<TelaDeCarregamento>();
            mundo.AddComponent<MapaDoMundo>();

            // a beira das estradas (M4): os modelos do Blender, a paleta e o material das letras das placas
            var objetos = mundo.AddComponent<ObjetosDoMundo>();
            objetos.materialDasLetras = MaterialDasLetras();
            const string fbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
            Mesh Malha(string nome) => AssetDatabase.LoadAllAssetsAtPath(fbxRua).OfType<Mesh>().FirstOrDefault(m => m.name == nome);
            objetos.carros = ZombieCityCleaner.Mundo.ModelosDaEstrada.NomesDosCarros.Select(Malha).ToArray();
            objetos.barreira = Malha("Barreira");
            objetos.cavalete = Malha("Cavalete");
            objetos.cone = Malha("Cone");
            // as ruas das cidades (M5)
            objetos.poste = Malha("Poste");
            objetos.posteLente = Malha("Poste_Lente");
            objetos.arvore = Malha("Arvore");
            objetos.arvoreSeca = Malha("Arvore_Seca");
            objetos.hidrante = Malha("Hidrante");
            objetos.lixeira = Malha("Lixeira");

            // o NavMesh em volta dos jogadores, no anfitrião (M6)
            mundo.AddComponent<NavMeshDoMundo>();
            // a floresta (M9a) e o abandono (M9c: os materiais da cidade, a hera e as pichações)
            PorAFloresta(mundo);
            PorOAbandono(mundo);
            // os carros abandonados com o capô, as rodas e o macaco (os kits e o PecasDosCarrosDoMundo)
            MontagemDosVeiculos.PorOsCarrosNoMundo(mundo);
            // a mobília (os móveis nos pontos das casas, das lojas e dos prédios)
            PorAMobilia(mundo);
            // os zumbis, sem hordas (o F1 cria perto do jogador)
            MontagemDosZumbis.PorOsZumbisNaCena(cena);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Cena do mundo montada: {Cena}. Dê Play e crie um mundo no menu (ou use a prévia: Zombie City Cleaner > Mundo > Prévia com semente).");
        }

        public const string MaterialDoLetreiro = "Assets/_Game/Art/Mundo/Letreiro.mat";
        public const string FbxDasArvores = "Assets/_Game/Art/Cidade/Cidade_Arvores.fbx";
        public const string MaterialDaVegetacao = "Assets/_Game/Art/Mundo/Vegetacao.mat";

        /// <summary>
        /// A floresta no objeto "Mundo" (Parte M9a): o <see cref="FlorestaDoMundo"/> com as malhas do Cidade_Arvores.fbx (3 por
        /// espécie, na ordem de <see cref="Mundo.Floresta.Nomes"/>) e o material da vegetação (o shader
        /// ZombieCityCleaner/Vegetacao com a paleta e as instâncias ligadas). O FBX é importado como o da rua: sem
        /// materiais, sem animação e sem o UV de lightmap. Serve para a montagem da cena e para pôr a floresta na cena
        /// já montada (menu Zombie City Cleaner > Mundo > Pôr a floresta na cena).
        /// </summary>
        public static FlorestaDoMundo PorAFloresta(GameObject mundo)
        {
            if (AssetImporter.GetAtPath(FbxDasArvores) is ModelImporter imp &&
                (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation || imp.generateSecondaryUV))
            {
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.generateSecondaryUV = false;
                imp.isReadable = false;
                imp.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaVegetacao);
            if (material == null)
            {
                material = new Material(Shader.Find("ZombieCityCleaner/Vegetacao")) { name = "Vegetacao" };
                AssetDatabase.CreateAsset(material, MaterialDaVegetacao);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Paleta.png"));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var floresta = mundo.GetComponent<FlorestaDoMundo>();
            if (floresta == null) floresta = mundo.AddComponent<FlorestaDoMundo>();
            floresta.material = material;
            var todas = AssetDatabase.LoadAllAssetsAtPath(FbxDasArvores).OfType<Mesh>().ToDictionary(m => m.name);
            string[] sufixos = { "", "_Longe1", "_Longe2" };
            floresta.malhas = ZombieCityCleaner.Mundo.Floresta.Nomes
                .SelectMany(n => sufixos.Select(s => todas.TryGetValue(n + s, out var m) ? m : null)).ToArray();
            int faltam = floresta.malhas.Count(m => m == null);
            if (faltam > 0) Debug.LogWarning($"Faltam {faltam} malhas da floresta no {FbxDasArvores}.");
            EditorUtility.SetDirty(floresta);
            return floresta;
        }

        public const string FbxDaMobilia = "Assets/_Game/Art/Cidade/Cidade_Mobilia.fbx";

        /// <summary>
        /// A mobília no objeto "Mundo" ("A mobília", primeira fatia): a <see cref="MobiliaDoMundo"/> com os modelos do
        /// Cidade_Mobilia.fbx, importado sem materiais, sem animação e legível (a malha de cada modelo é copiada para as obras
        /// montadas em outra thread). Serve para a montagem da cena e para o menu Zombie City Cleaner > Mundo > Pôr a mobília
        /// na cena (rode de novo quando os modelos mudarem de nome).
        /// </summary>
        public static MobiliaDoMundo PorAMobilia(GameObject mundo)
        {
            if (AssetImporter.GetAtPath(FbxDaMobilia) is ModelImporter imp &&
                (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation || imp.generateSecondaryUV || !imp.isReadable))
            {
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.generateSecondaryUV = false;
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
            var mobilia = mundo.GetComponent<MobiliaDoMundo>();
            if (mobilia == null) mobilia = mundo.AddComponent<MobiliaDoMundo>();
            mobilia.malhas = AssetDatabase.LoadAllAssetsAtPath(FbxDaMobilia).OfType<Mesh>()
                .Where(m => ZombieCityCleaner.Mundo.Mobilia.Feitos.ContainsKey(m.name)).OrderBy(m => m.name).ToArray();
            int faltam = ZombieCityCleaner.Mundo.Mobilia.Feitos.Keys.Count(n => mobilia.malhas.All(m => m.name != n));
            if (faltam > 0) Debug.LogWarning($"Faltam {faltam} modelos da mobília no {FbxDaMobilia}.");
            EditorUtility.SetDirty(mobilia);
            return mobilia;
        }

        [MenuItem("Zombie City Cleaner/Mundo/Pôr a mobília na cena")]
        public static void PorAMobiliaNaCena()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de mexer na cena do mundo.");
                return;
            }
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != Cena) cena = EditorSceneManager.OpenScene(Cena);
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("A cena do mundo não tem o objeto \"Mundo\": monte a cena antes.");
                return;
            }
            var m = PorAMobilia(mundo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Mobília posta na cena: {m.malhas.Length} modelos.");
        }

        [MenuItem("Zombie City Cleaner/Mundo/Pôr a floresta na cena")]
        public static void PorAFlorestaNaCena()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de mexer na cena do mundo.");
                return;
            }
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != Cena) cena = EditorSceneManager.OpenScene(Cena);
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("A cena do mundo não tem o objeto \"Mundo\": monte a cena antes.");
                return;
            }
            var floresta = PorAFloresta(mundo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"A floresta está na cena do mundo ({floresta.malhas.Count(m => m != null)} malhas).");
        }

        /// <summary>
        /// O abandono no objeto "Mundo" (Parte M9c): os materiais do abandono (<see cref="TexturasDoAbandono.MateriaisDoMundo"/>,
        /// refeitos a partir do Abandono.mat) no terreno (as estradas, as ruas e as caixas de longe; as casas de verdade) e nos
        /// objetos de rua, e os da hera (o da vegetação) e das pichações. Serve para a montagem da cena e para pôr o abandono
        /// na cena já montada (menu Zombie City Cleaner > Mundo > Pôr o abandono na cena).
        /// </summary>
        public static void PorOAbandono(GameObject mundo)
        {
            TexturasDoAbandono.MateriaisDoMundo();
            if (AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasPichacoes) == null) TexturasDoAbandono.Pichacoes();
            var terreno = mundo.GetComponent<TerrenoDoMundo>();
            terreno.materialDaCidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoMundo);
            terreno.materialDasCasas = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasCasas);
            terreno.materialDaHera = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaVegetacao);
            terreno.materialDasPichacoes = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasPichacoes);
            EditorUtility.SetDirty(terreno);
            var objetos = mundo.GetComponent<ObjetosDoMundo>();
            objetos.materialDaCidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDosObjetos);
            EditorUtility.SetDirty(objetos);
        }

        [MenuItem("Zombie City Cleaner/Mundo/Pôr o abandono na cena")]
        public static void PorOAbandonoNaCena()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de mexer na cena do mundo.");
                return;
            }
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != Cena) cena = EditorSceneManager.OpenScene(Cena);
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("A cena do mundo não tem o objeto \"Mundo\": monte a cena antes.");
                return;
            }
            PorOAbandono(mundo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("O abandono está na cena do mundo.");
        }

        /// <summary>O material das letras das placas (o shader ZombieCityCleaner/Letreiro); feito se não existir.</summary>
        static Material MaterialDasLetras()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDoLetreiro);
            if (m != null) return m;
            m = new Material(Shader.Find("ZombieCityCleaner/Letreiro")) { name = "Letreiro" };
            AssetDatabase.CreateAsset(m, MaterialDoLetreiro);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>
        /// A build de teste da cena Mundo (só ela), em `Builds/TesteMundo/` (Development, ignorada pelo git), para jogar com
        /// o Editor: `ZombieCityCleaner.exe -batchmode -nographics -entrar-local 127.0.0.1 -bot -logFile caminho.log`.
        /// Os Preloaded Assets do Player Settings voltam a ser o que eram (a build põe o InputSystem_Actions lá).
        /// O resultado vai para o console e para `Builds/TesteMundo/build.txt`.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Mundo/Build de teste da cena do mundo")]
        public static void BuildDeTeste()
        {
            var preloaded = PlayerSettings.GetPreloadedAssets();
            string texto;
            try
            {
                var relatorio = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Cena },
                    locationPathName = "Builds/TesteMundo/ZombieCityCleaner.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                var s = relatorio.summary;
                texto = $"{s.result} em {s.totalTime.TotalSeconds:F0} s, {s.totalErrors} erros, {s.totalSize / 1048576} MB";
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                AssetDatabase.SaveAssets();
            }
            System.IO.Directory.CreateDirectory("Builds/TesteMundo");
            System.IO.File.WriteAllText("Builds/TesteMundo/build.txt", texto);
            Debug.Log("[Build de teste do mundo] " + texto);
        }
    }
}
