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
    /// Monta os zumbis (Partes 5 e 6, os modelos dos ajustes antes da Parte 7, os zumbis leves de 24/10/2026 e os em
    /// blocos de 25/10/2026):
    /// - a importação dos modelos da lista (hoje o ZombieGaroto, leve; os 6 em blocos, com os LODs, e os 4 de ~3.100
    ///   vértices, Zombie1, ZombieHomem2, ZombieMulher e ZombieMulherLoira, continuam na pasta e voltam se entrarem na
    ///   lista), com os 5 clipes de cada;
    /// - as camadas "Zumbi" (7, que não colide com ela mesma nem com os braços de primeira pessoa) e "Corpos" (8);
    /// - de cada modelo, as duas malhas (inteira, um draw call; e sem cabeça, com a tampa de carne no pescoço) e o
    ///   material com a textura dele (nos zumbis leves, o shader ZombieCityCleaner/Zumbi com as camadas: a base, as
    ///   máscaras das cores, as 10 sujeiras e a folha de rostos; cada zumbi faz a cópia dele no jogo, veja Aparencia);
    /// - o Animator (andar e, só no tronco, atacar e reagir ao tiro), do primeiro modelo; os outros usam um override com
    ///   os clipes deles;
    /// - de cada modelo, o prefab do zumbi (agente, cápsula, corpo cinemático, voz e hitboxes nos ossos, medidas na
    ///   malha) e o do corpo que cai (ragdoll: 11 partes com física e juntas com os limites do corpo humano);
    /// - o jogador com vida (VidaJogador), refazendo o prefab dele;
    /// - na cena, o objeto "Zumbis" (ZombieManager, benchmark e pontos de horda), o sangue no "Efeitos" e o NavMesh;
    /// - os decals (manchas de sangue e furos de tiro) no renderizador.
    /// Menu: Zombie City Cleaner > Montar zumbis. Pode rodar de novo.
    /// </summary>
    public static class MontagemDosZumbis
    {
        const string Arte = "Assets/_Game/Art/Characters/Zombie";
        // o zumbi leve (24/10/2026, ~1.300 faces, a aparência variando por zumbi). Ficam na pasta e voltam se entrarem na
        // lista: os em blocos de 25/10/2026, o garoto e a mulher com os 3 cabelos de cada (um modelo por corpo e cabelo,
        // <corpo>_<cabelo>.fbx, com os LODs dentro: "ZombieBloco_Cuia", "_Espetado", "_Comprido",
        // "ZombieBlocoMulher_Longo", "_Selvagem" e "_Chanel"; saíram a pedido, ainda não estavam bons), e os 4 modelos
        // de ~3.100 vértices ("Zombie1", "ZombieHomem2", "ZombieMulher", "ZombieMulherLoira")
        static readonly string[] Modelos = { "ZombieGaroto" };
        // a pasta de arte dos zumbis leves e em blocos: <texturas>_Base.png, <texturas>_Mascaras.png, <texturas>_Sujeiras.png
        // (as 10 camadas numa folha de 5 × 2) e a folha de rostos (Rosto.png; a dos zumbis em blocos, Rosto_Bloco.png).
        // Nos zumbis em blocos, as texturas são as do corpo (<corpo>_*.png), as mesmas para os 3 cabelos
        const int ColunasDasSujeiras = 5, LinhasDasSujeiras = 2;
        static readonly Dictionary<string, string> FolhaDeRostos = new Dictionary<string, string>
        {
            { "ZombieBloco", "Rosto_Bloco.png" }, { "ZombieBlocoMulher", "Rosto_Bloco.png" },
        };
        // os LODs dos zumbis em blocos, no mesmo FBX e nos mesmos ossos da malha principal (Zombie_Body)
        // (com outro nome que "_LOD1": esse é o jeito da Unity de criar um LOD Group sozinha)
        static readonly string[] MalhasDeLonge = { "Zombie_Body_Longe1", "Zombie_Body_Longe2" };
        // os corpos (ragdoll) no total, divididos entre os modelos (eram 12 de cada um dos 4)
        const int CorposNoTotal = 48;
        const string Controlador = Arte + "/Zombie.controller";
        const string MascaraTronco = Arte + "/Zombie_Tronco.mask";
        const string Malhas = Arte + "/Malhas";
        const string Prefabs = "Assets/_Game/Prefabs/Zumbis";
        const string ArquivoNavMesh = "Assets/_Game/Scenes/Demo_Cidade/NavMesh da cidade.asset";
        const string PastaDecals = "Assets/_Game/Art/Materials/Decals";
        const string RendererPrincipal = "Assets/Settings/PC_Renderer.asset";

        // nome, centro, raio, quantidade: 1.000 zumbis (a pedido, 24/10/2026; eram 258, o dobro da Parte 5), nenhum perto
        // da casa da família. A maior parte foi para a rua, a praça e o estacionamento, com o raio maior para os grupos
        // não ficarem amontoados; nas casas e nos andares do prédio, pouco mais que o dobro (são lugares pequenos)
        static readonly (string nome, Vector3 centro, float raio, int quantidade)[] Hordas =
        {
            ("Cruzamento central", new Vector3(0f, 0f, 0f), 12f, 70),
            ("Rua norte", new Vector3(0f, 0f, 22f), 10f, 55),
            ("Rua sul", new Vector3(0f, 0f, -24f), 10f, 50),
            ("Rua leste", new Vector3(22f, 0f, 0f), 11f, 60),
            ("Rua oeste", new Vector3(-40f, 0f, 12f), 10f, 50),
            ("Praça", new Vector3(24f, 0f, -12f), 12f, 70),
            ("Estacionamento", new Vector3(10f, 0f, 33f), 12f, 65),
            ("Prédio, térreo", new Vector3(20f, 0.15f, 19f), 5f, 20),
            ("Prédio, 1º andar", new Vector3(20f, 3.15f, 21f), 6f, 18),
            ("Prédio, 2º andar", new Vector3(20f, 6.15f, 21f), 6f, 16),
            ("Prédio, 3º andar", new Vector3(20f, 9.15f, 21f), 6f, 16),
            ("Casa 1", new Vector3(-27f, 0.15f, 19f), 3f, 10),
            ("Casa 2", new Vector3(-12f, 0.15f, 23f), 3f, 10),
            ("Casa 4", new Vector3(13f, 0.15f, -14f), 3f, 10),
            ("Casa 5", new Vector3(28f, 0.15f, -26f), 3f, 10),
            ("Rua (40, 20)", new Vector3(40f, 0f, 20f), 10f, 45),
            ("Rua (40, -20)", new Vector3(40f, 0f, -20f), 10f, 40),
            ("Rua (20, 40)", new Vector3(20f, 0f, 40f), 10f, 40),
            ("Rua (-20, 40)", new Vector3(-20f, 0f, 40f), 10f, 35),
            ("Rua (-40, 32)", new Vector3(-40f, 0f, 32f), 10f, 35),
            ("Rua (20, -40)", new Vector3(20f, 0f, -40f), 10f, 45),
            ("Rua (-12, -40)", new Vector3(-12f, 0f, -40f), 10f, 35),
            ("Esquina nordeste", new Vector3(40f, 0f, 40f), 9f, 30),
            ("Esquina sudeste", new Vector3(40f, 0f, -40f), 9f, 30),
            ("Esquina sudoeste", new Vector3(-40f, 0f, -40f), 9f, 25),
            ("Saída norte", new Vector3(0f, 0f, 54f), 8f, 30),
            ("Saída leste", new Vector3(54f, 0f, 0f), 8f, 30),
            ("Saída sul", new Vector3(0f, 0f, -54f), 8f, 25),
            ("Saída oeste", new Vector3(-54f, 0f, 0f), 8f, 25),
        };

        // clipe (take do Blender "Zombie_Rig|Zombie_X"), em loop?
        static readonly (string nome, bool loop)[] Clipes =
        {
            ("Idle", true), ("Walk", true), ("Chase", true), ("Attack", false), ("HitReact", false),
        };

        // os clipes do desmembramento, no <modelo>_Extra.fbx (feito pelo zumbi_extras.py da pasta Arte-Sniper): cair,
        // rastejar (andando e parado) e mancar sem a perna esquerda ou sem a direita
        static readonly (string nome, bool loop)[] ClipesExtra =
        {
            ("Cair", false), ("Rastejar", true), ("RastejarParado", true), ("MancarE", true), ("MancarD", true),
        };

        // as partes do corpo com física: osso, osso onde a cápsula termina, massa (kg)
        static readonly (string osso, string ate, float massa)[] PartesDoCorpo =
        {
            ("hips", "chest", 12f), ("chest", "neck", 14f), ("head", null, 5f),
            ("upper_arm.L", "forearm.L", 2.5f), ("forearm.L", "hand.L", 1.8f), ("upper_arm.R", "forearm.R", 2.5f), ("forearm.R", "hand.R", 1.8f),
            ("thigh.L", "shin.L", 8f), ("shin.L", "foot.L", 4.5f), ("thigh.R", "shin.R", 8f), ("shin.R", "foot.R", 4.5f),
        };

        /// <summary>O que a montagem precisa de cada modelo importado.</summary>
        class Modelo
        {
            public int indice;
            public string nome, fbx;
            public Dictionary<string, AnimationClip> clipes;
            public Mesh original, inteira, semCabeca;
            public Mesh[] longe;
            public Material material;
            public int submeshCabeca, submeshCarne;
            public float altura;
        }

        [MenuItem("Zombie City Cleaner/Montar zumbis")]
        public static void Montar()
        {
            CamadasDosZumbis();
            var modelos = Modelos.Select(ImportarModelo).ToArray();
            var controlador = ControladorDoZumbi(modelos[0].clipes, MascaraDoTronco(modelos[0].fbx));
            var prefabs = new Zumbi[modelos.Length];
            var corpos = new CorpoDeZumbi[modelos.Length];
            Directory.CreateDirectory(Prefabs);
            foreach (var m in modelos)
            {
                RuntimeAnimatorController animacao = m.indice == 0 ? controlador : Sobrepor(controlador, modelos[0], m);
                prefabs[m.indice] = MontarPrefab(m, animacao);
                corpos[m.indice] = MontarCorpo(m);
            }
            MontagemDaRede.MontarPrefab();
            var decals = MateriaisDosDecals();
            DecalsNoRenderizador();
            var cena = EditorSceneManager.GetActiveScene();
            MontarCena(cena, prefabs, corpos, decals);
            AssarNavegacao(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            // O controlador é apagado e criado de novo (com o mesmo GUID). Ao ser apagado, os prefabs são reimportados
            // sem ele; salvos depois iguais, a Unity não os importa de novo, e os zumbis nasciam sem animação (pose T).
            foreach (var p in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }).Select(AssetDatabase.GUIDToAssetPath))
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            Debug.Log("Zumbis montados: " + string.Join(", ", modelos.Select(m => $"{m.nome} ({m.altura:F2} m)")));
        }

        static T Carregar<T>(string caminho) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(caminho);
            if (a == null) throw new FileNotFoundException("Não encontrado", caminho);
            return a;
        }

        /// <summary>
        /// As camadas: 7 "Zumbi" (cápsulas e hitboxes), 8 "Corpos" (os zumbis mortos, com física) e 9 "Jogador" (a raiz
        /// dos jogadores). Os corpos só batem no cenário e uns nos outros: o tiro passa, e zumbis e jogadores atravessam.
        /// </summary>
        static void CamadasDosZumbis()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var camadas = tags.FindProperty("layers");
            camadas.GetArrayElementAtIndex(Zumbi.CamadaZumbi).stringValue = "Zumbi";
            camadas.GetArrayElementAtIndex(CorpoDeZumbi.Camada).stringValue = "Corpos";
            camadas.GetArrayElementAtIndex(JogadorRede.CamadaJogador).stringValue = "Jogador";
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, Zumbi.CamadaZumbi, true);
            Physics.IgnoreLayerCollision(Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, true);
            foreach (int outra in new[] { 2, Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, JogadorRede.CamadaJogador })
                Physics.IgnoreLayerCollision(CorpoDeZumbi.Camada, outra, true);
        }

        // ------------------------------------------------------------------ importação, malhas e material

        /// <summary>
        /// Os 5 clipes (renomeados, com loop onde precisa), as duas malhas e o material. Os submeshes são achados pelo
        /// nome do material que veio do Blender: "_Corpo", "_Cabeca" e "Zombie_Flesh" (a tampa do pescoço).
        /// </summary>
        static Modelo ImportarModelo(string nome, int indice)
        {
            var m = new Modelo { indice = indice, nome = nome, fbx = $"{Arte}/{nome}.fbx" };
            var importador = (ModelImporter)AssetImporter.GetAtPath(m.fbx);
            importador.generateSecondaryUV = false;   // o segundo UV é o do rosto (zumbis leves)
            importador.animationType = ModelImporterAnimationType.Generic;
            importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importador.importAnimation = true;
            var padrao = importador.defaultClipAnimations;
            importador.clipAnimations = Clipes.Select(c =>
            {
                var clipe = padrao.First(p => p.takeName.EndsWith("|Zombie_" + c.nome));
                clipe.name = c.nome;
                clipe.loopTime = c.loop;
                return clipe;
            }).ToArray();
            importador.SaveAndReimport();
            m.clipes = AssetDatabase.LoadAllAssetsAtPath(m.fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            // os clipes do desmembramento: só o esqueleto, com os mesmos caminhos de osso do modelo
            string extra = $"{Arte}/{nome}_Extra.fbx";
            var ie = (ModelImporter)AssetImporter.GetAtPath(extra);
            if (ie == null) throw new FileNotFoundException("Faltam as animações do desmembramento", extra);
            ie.animationType = ModelImporterAnimationType.Generic;
            ie.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ie.bakeAxisConversion = false;
            ie.importAnimation = true;
            ie.materialImportMode = ModelImporterMaterialImportMode.None;
            var padraoExtra = ie.defaultClipAnimations;
            ie.clipAnimations = ClipesExtra.Select(c =>
            {
                var clipe = padraoExtra.First(p => p.takeName.EndsWith("|Zombie_" + c.nome));
                clipe.name = c.nome;
                clipe.loopTime = c.loop;
                return clipe;
            }).ToArray();
            ie.SaveAndReimport();
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(extra).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                m.clipes[c.name] = c;

            var smrs = Carregar<GameObject>(m.fbx).GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var smr = Principal(smrs);
            var original = smr.sharedMesh;
            m.original = original;
            int Sub(string fim) => System.Array.FindIndex(smr.sharedMaterials, x => x.name.EndsWith(fim));
            int corpo = Sub("_Corpo"), cabeca = Sub("_Cabeca"), carne = Sub("Zombie_Flesh");
            if (corpo < 0 || cabeca < 0 || carne < 0) throw new System.Exception($"{nome}: faltam os materiais _Corpo, _Cabeca ou Zombie_Flesh.");
            m.submeshCabeca = cabeca;
            m.submeshCarne = carne;
            Directory.CreateDirectory(Malhas);
            m.inteira = Malha(original, $"{nome}_Inteira", original.GetTriangles(corpo).Concat(original.GetTriangles(cabeca)));
            m.semCabeca = Malha(original, $"{nome}_SemCabeca", original.GetTriangles(corpo), original.GetTriangles(carne));
            m.altura = original.bounds.size.z;   // a malha do Blender tem o z para cima
            m.longe = MalhasDeLonge.Select(n => smrs.FirstOrDefault(s => s.name == n)).Where(s => s != null)
                                   .Select((s, i) => MalhaDeLonge(s, smr, $"{nome}_Inteira_LOD{i + 1}")).ToArray();
            m.material = MaterialDoModelo(nome);
            return m;
        }

        /// <summary>A malha principal do FBX: a "Zombie_Body" (os zumbis em blocos trazem também os LODs).</summary>
        static SkinnedMeshRenderer Principal(SkinnedMeshRenderer[] smrs) => smrs.FirstOrDefault(s => s.name == "Zombie_Body") ?? smrs[0];

        /// <summary>
        /// Uma malha de LOD pronta para entrar no lugar da inteira, no mesmo SkinnedMeshRenderer: os pesos com os índices
        /// dos ossos da malha principal (pelo nome) e as bindposes nessa ordem, e tudo num submesh só (um draw call).
        /// </summary>
        static Mesh MalhaDeLonge(SkinnedMeshRenderer lod, SkinnedMeshRenderer principal, string nome)
        {
            var original = lod.sharedMesh;
            var malha = Object.Instantiate(original);
            malha.name = nome;
            var mapa = lod.bones.Select(b => System.Array.FindIndex(principal.bones, p => p.name == b.name)).ToArray();
            if (mapa.Any(i => i < 0)) throw new System.Exception($"{nome}: o LOD tem ossos que a malha principal não tem.");
            malha.boneWeights = original.boneWeights.Select(w => new BoneWeight
            {
                boneIndex0 = mapa[w.boneIndex0], weight0 = w.weight0, boneIndex1 = mapa[w.boneIndex1], weight1 = w.weight1,
                boneIndex2 = mapa[w.boneIndex2], weight2 = w.weight2, boneIndex3 = mapa[w.boneIndex3], weight3 = w.weight3,
            }).ToArray();
            var binds = principal.sharedMesh.bindposes;
            var bindsDoLod = original.bindposes;
            for (int j = 0; j < mapa.Length; j++) binds[mapa[j]] = bindsDoLod[j];
            malha.bindposes = binds;
            var tris = Enumerable.Range(0, original.subMeshCount).SelectMany(i => original.GetTriangles(i)).ToList();
            malha.subMeshCount = 1;
            malha.SetTriangles(tris, 0, false);
            return Salvar(malha, $"{Malhas}/{nome}.asset");
        }

        /// <summary>
        /// O nome das texturas de um modelo: as dele (<modelo>_Base.png) ou, nos zumbis em blocos (<corpo>_<cabelo>),
        /// as do corpo, que servem para todos os cabelos.
        /// </summary>
        static string NomeDasTexturas(string nome)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{Arte}/{nome}_Base.png") != null) return nome;
            int i = nome.LastIndexOf('_');
            if (i > 0 && AssetDatabase.LoadAssetAtPath<Texture2D>($"{Arte}/{nome.Substring(0, i)}_Base.png") != null) return nome.Substring(0, i);
            return nome;
        }

        static Mesh Malha(Mesh original, string nome, params IEnumerable<int>[] submeshes)
        {
            var malha = Object.Instantiate(original);
            malha.name = nome;
            // os pesos no formato de 4 por vértice, o mesmo das malhas cortadas (Membros) e dos LODs: com os ossos rígidos
            // dos zumbis em blocos, o FBX vem com 1 osso por vértice, e o renderer parava de desenhar na troca de malha
            malha.boneWeights = original.boneWeights;
            malha.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) malha.SetTriangles(submeshes[i].ToList(), i, false);
            return Salvar(malha, $"{Malhas}/{nome}.asset");
        }

        static Mesh Salvar(Mesh nova, string caminho)
        {
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (existente == null)
            {
                AssetDatabase.CreateAsset(nova, caminho);
                existente = nova;
            }
            else
            {
                EditorUtility.CopySerialized(nova, existente);
                Object.DestroyImmediate(nova);
            }
            // legível no jogo: o desmembramento corta a malha na hora (Zombies/Membros)
            var so = new SerializedObject(existente);
            so.FindProperty("m_IsReadable").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return existente;
        }

        /// <summary>
        /// URP Lit com a textura do modelo, pouco brilho e sem o passe de motion vectors (como o zumbi antigo). Os zumbis
        /// leves (com <modelo>_Base.png na pasta) usam o shader ZombieCityCleaner/Zumbi com as camadas deles.
        /// </summary>
        static Material MaterialDoModelo(string nome)
        {
            string texturas = NomeDasTexturas(nome);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{Arte}/{texturas}_Base.png") != null)
                return MaterialLeve(texturas, $"{Arte}/Materials/{texturas}.mat");
            string caminho = $"{Arte}/Materials/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            var textura = Carregar<Texture2D>($"{Arte}/{nome}_basecolor.jpg");
            m.SetTexture("_BaseMap", textura);
            m.SetTexture("_MainTex", textura);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetShaderPassEnabled("MotionVectors", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// O material de um zumbi leve: as quatro texturas (importadas aqui: as cores em sRGB, as máscaras e as sujeiras
        /// lineares, as sujeiras como Texture2DArray de 10 camadas) e as cores do modelo como vieram do Tripo. Cada zumbi
        /// faz a cópia dele no jogo e sorteia as cores e as camadas (Aparencia).
        /// </summary>
        static Material MaterialLeve(string nome, string caminho)
        {
            var shader = Shader.Find("ZombieCityCleaner/Zumbi");
            if (shader == null) throw new System.Exception("Falta o shader ZombieCityCleaner/Zumbi (Assets/_Game/Shaders/Zumbi.shader).");
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", TexturaLeve<Texture2D>($"{nome}_Base.png", true, 2048));
            m.SetTexture("_Mascaras", TexturaLeve<Texture2D>($"{nome}_Mascaras.png", false, 1024));
            m.SetTexture("_Sujeiras", TexturaLeve<Texture2DArray>($"{nome}_Sujeiras.png", false, 8192, true));
            m.SetTexture("_Rosto", TexturaLeve<Texture2D>(FolhaDeRostos.TryGetValue(nome, out var folha) ? folha : "Rosto.png", true, 2048));
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static T TexturaLeve<T>(string arquivo, bool srgb, int tamanho, bool camadas = false) where T : Texture
        {
            string caminho = $"{Arte}/{arquivo}";
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            if (imp == null) throw new FileNotFoundException("Falta a textura do zumbi leve (veja Modelo 3D/Zumbis/texturas_leves.py)", caminho);
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.textureType = TextureImporterType.Default;
            s.textureShape = camadas ? TextureImporterShape.Texture2DArray : TextureImporterShape.Texture2D;
            if (camadas)
            {
                s.flipbookColumns = ColunasDasSujeiras;
                s.flipbookRows = LinhasDasSujeiras;
            }
            s.sRGBTexture = srgb;
            s.alphaSource = TextureImporterAlphaSource.FromInput;
            s.alphaIsTransparency = false;   // o fundo transparente da folha de rostos já tem a cor do traço vizinho
            s.mipmapEnabled = true;
            s.wrapMode = TextureWrapMode.Clamp;
            imp.SetTextureSettings(s);
            imp.maxTextureSize = tamanho;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;   // BC7: as chaves de cor do rosto ficam limpas
            imp.SaveAndReimport();
            return Carregar<T>(caminho);
        }

        // ------------------------------------------------------------------ Animator

        static AvatarMask MascaraDoTronco(string fbx)
        {
            var mascara = new AvatarMask();
            mascara.AddTransformPath(Carregar<GameObject>(fbx).transform, true);
            for (int i = 0; i < mascara.transformCount; i++) mascara.SetTransformActive(i, mascara.GetTransformPath(i).Contains("/spine"));
            AssetDatabase.DeleteAsset(MascaraTronco);
            AssetDatabase.CreateAsset(mascara, MascaraTronco);
            return mascara;
        }

        /// <summary>
        /// Camada base: Andar (Idle/Walk/Chase pela Speed) e, pelo inteiro "Pernas" (o desmembramento), Mancando sem a
        /// perna esquerda (1) ou sem a direita (2), Caindo (3) e Rastejando (4). Camada "Ações" (só do tronco para cima):
        /// Ataque e Tiro (reação), disparados por trigger, voltando ao vazio. Write Defaults desligado em todos os estados.
        /// A morte não tem estado: o zumbi some e o corpo com física cai no lugar.
        /// </summary>
        static AnimatorController ControladorDoZumbi(Dictionary<string, AnimationClip> c, AvatarMask tronco)
        {
            AssetDatabase.DeleteAsset(Controlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Tiro", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Pernas", AnimatorControllerParameterType.Int);

            var baseSm = ctrl.layers[0].stateMachine;
            var andar = Arvore(ctrl, "Andar", (c["Idle"], 0f), (c["Walk"], 0.6f), (c["Chase"], 1.12f));
            baseSm.defaultState = andar;
            // o desmembramento: cada valor de "Pernas" leva ao seu estado, de qualquer um (sem voltar para si mesmo)
            var mancarE = Arvore(ctrl, "Mancando sem a esquerda", (c["Idle"], 0f), (c["MancarE"], 0.45f));
            var mancarD = Arvore(ctrl, "Mancando sem a direita", (c["Idle"], 0f), (c["MancarD"], 0.45f));
            var caindo = Estado(baseSm, "Caindo", c["Cair"]);
            var rastejando = Arvore(ctrl, "Rastejando", (c["RastejarParado"], 0f), (c["Rastejar"], 0.35f));
            int valor = 0;
            foreach (var destino in new[] { andar, mancarE, mancarD, caindo, rastejando })
            {
                var ida = baseSm.AddAnyStateTransition(destino);
                ida.AddCondition(AnimatorConditionMode.Equals, valor++, "Pernas");
                ida.canTransitionToSelf = false;
                ida.hasExitTime = false;
                ida.duration = destino == caindo ? 0.1f : 0.2f;
            }

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
            var t = acoes.AddAnyStateTransition(ataque);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Atacar"); t.duration = 0.1f; t.canTransitionToSelf = false;
            t = ataque.AddTransition(vazio); t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f;
            t = acoes.AddAnyStateTransition(tiro);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Tiro"); t.duration = 0.05f; t.canTransitionToSelf = true;
            t = tiro.AddTransition(vazio); t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        /// <summary>
        /// Os outros modelos usam o mesmo Animator com os clipes deles: cada FBX traz as animações assadas no próprio
        /// esqueleto (as posições dos ossos são as dele), então os clipes de um modelo não servem no outro.
        /// </summary>
        static AnimatorOverrideController Sobrepor(AnimatorController baseCtrl, Modelo primeiro, Modelo m)
        {
            string caminho = $"{Arte}/Zombie_{m.nome}.overrideController";
            AssetDatabase.DeleteAsset(caminho);
            var aoc = new AnimatorOverrideController(baseCtrl) { name = "Zombie_" + m.nome };
            aoc.ApplyOverrides(Clipes.Concat(ClipesExtra).Select(c => new KeyValuePair<AnimationClip, AnimationClip>(primeiro.clipes[c.nome], m.clipes[c.nome])).ToList());
            AssetDatabase.CreateAsset(aoc, caminho);
            return aoc;
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

        // ------------------------------------------------------------------ pose de repouso e medidas na malha

        /// <summary>
        /// O FBX abre numa pose da animação: os ossos voltam à pose de repouso (braços abertos), pela bindpose. Tudo o que
        /// é medido (hitboxes, colisores e juntas do corpo) parte dela, e o corpo que cai precisa ser criado nela.
        /// </summary>
        static void PoseDeRepouso(SkinnedMeshRenderer smr)
        {
            var binds = smr.sharedMesh.bindposes;
            var ossos = smr.bones;
            // dos pais para os filhos: cada osso vai para a matriz do repouso no mundo
            foreach (int i in Enumerable.Range(0, ossos.Length).OrderBy(i => Profundidade(ossos[i])))
            {
                var mundo = smr.transform.localToWorldMatrix * binds[i].inverse;
                ossos[i].SetPositionAndRotation(mundo.GetPosition(), mundo.rotation);
            }
        }

        static int Profundidade(Transform t)
        {
            int n = 0;
            for (; t.parent != null; t = t.parent) n++;
            return n;
        }

        /// <summary>
        /// O raio de um pedaço do corpo: a distância dos vértices (presos sobretudo a estes ossos) até o segmento de-até,
        /// no percentil dado. Os vértices vão para o mundo pela pose de repouso.
        /// </summary>
        static float Raio(SkinnedMeshRenderer smr, Vector3 de, Vector3 ate, float percentil, params string[] ossos)
        {
            var malha = smr.sharedMesh;
            var indices = ossos.Select(o => System.Array.FindIndex(smr.bones, b => b.name == o)).ToArray();
            var pesos = malha.boneWeights;
            var vertices = malha.vertices;
            var m = smr.transform.localToWorldMatrix;
            var d = new List<float>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!indices.Contains(pesos[i].boneIndex0) || pesos[i].weight0 < 0.5f) continue;
                Vector3 p = m.MultiplyPoint3x4(vertices[i]);
                Vector3 seg = ate - de;
                float t = Mathf.Clamp01(Vector3.Dot(p - de, seg) / seg.sqrMagnitude);
                d.Add(Vector3.Distance(p, de + seg * t));
            }
            d.Sort();
            return d.Count == 0 ? 0.08f : d[Mathf.Min(d.Count - 1, Mathf.RoundToInt(percentil * (d.Count - 1)))];
        }

        /// <summary>
        /// A esfera da cabeça: a cabeça acima do corte do pescoço (a altura da tampa), com o queixo; o cabelo comprido que
        /// desce até os ombros fica de fora. O raio é a média das três metades: de perto, a borda do cabelo também conta.
        /// </summary>
        static (Vector3 centro, float raio) Cranio(Modelo m, Matrix4x4 malhaNoMundo)
        {
            var malha = m.original;
            var vertices = malha.vertices;
            float corte = malha.GetTriangles(m.submeshCarne).Average(i => malhaNoMundo.MultiplyPoint3x4(vertices[i]).y);
            var cranio = malha.GetTriangles(m.submeshCabeca).Distinct().Select(i => malhaNoMundo.MultiplyPoint3x4(vertices[i]))
                              .Where(p => p.y > corte + 0.03f).ToList();
            var limites = new Bounds(cranio[0], Vector3.zero);
            foreach (var p in cranio) limites.Encapsulate(p);
            return (limites.center, (limites.extents.x + limites.extents.y + limites.extents.z) / 3f);
        }

        // ------------------------------------------------------------------ prefab do zumbi

        static Zumbi MontarPrefab(Modelo m, RuntimeAnimatorController animacao)
        {
            var raiz = new GameObject("Zumbi_" + m.nome) { layer = Zumbi.CamadaZumbi };
            var z = raiz.AddComponent<Zumbi>();
            var agente = raiz.AddComponent<NavMeshAgent>();
            agente.enabled = false;   // o anfitrião liga quando o zumbi está perto de alguém; nas cópias fica desligado
            agente.agentTypeID = 0;
            agente.radius = 0.3f; agente.height = m.altura; agente.baseOffset = 0f;
            agente.speed = 0.55f; agente.angularSpeed = 240f; agente.acceleration = 6f;
            agente.stoppingDistance = 0.4f; agente.autoBraking = true;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agente.avoidancePriority = 50;
            var capsula = raiz.AddComponent<CapsuleCollider>();
            capsula.center = new Vector3(0f, m.altura / 2f, 0f); capsula.height = m.altura; capsula.radius = 0.28f;
            // a cápsula também é a hitbox do nível médio (o corpo inteiro)
            var hitboxCorpo = raiz.AddComponent<Hitbox>();
            hitboxCorpo.zumbi = z; hitboxCorpo.regiao = RegiaoDoCorpo.Corpo; hitboxCorpo.colisor = capsula;
            // corpo cinemático: as hitboxes que se mexem com os ossos viram partes dele (mais barato que colisores estáticos andando)
            var corpo = raiz.AddComponent<Rigidbody>();
            corpo.isKinematic = true; corpo.useGravity = false;
            var voz = raiz.AddComponent<AudioSource>();
            voz.playOnAwake = false; voz.spatialBlend = 1f; voz.rolloffMode = AudioRolloffMode.Linear;
            voz.minDistance = 1.5f; voz.maxDistance = 22f; voz.dopplerLevel = 0f; voz.volume = 0.9f;

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(m.fbx));
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            var animador = modelo.GetComponent<Animator>();
            animador.runtimeAnimatorController = animacao;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animador.keepAnimatorStateOnDisable = true;   // o nível Longe desliga o Animator; ao voltar, ele continua de onde estava
            var smrs = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var smr = Principal(smrs);
            // os LODs entram no lugar da malha, no mesmo renderer (Zumbi.DefinirDetalhe): os renderers deles ficam desligados
            foreach (var s in smrs)
                if (s != smr) s.gameObject.SetActive(false);
            PoseDeRepouso(smr);
            Transform Osso(string nome) => smr.bones.First(t => t.name == nome);

            // hitboxes: cápsulas de osso a osso, com o raio medido na malha; a esfera da cabeça no crânio
            var hitboxes = new List<Hitbox>();
            var (centro, raio) = Cranio(m, smr.transform.localToWorldMatrix);
            var cabeca = NovaHitbox(z, Osso("head"), "Cabeca", RegiaoDoCorpo.Cabeca, out var goCabeca);
            goCabeca.transform.position = centro;
            var esfera = goCabeca.AddComponent<SphereCollider>();
            esfera.radius = raio / Mathf.Max(0.0001f, goCabeca.transform.lossyScale.x);
            cabeca.colisor = esfera;
            hitboxes.Add(cabeca);
            Vector3 P(string nome) => Osso(nome).position;
            hitboxes.Add(Capsula(z, Osso("spine"), P("hips"), P("chest"), Raio(smr, P("hips"), P("chest"), 0.6f, "hips", "spine"), RegiaoDoCorpo.Tronco));
            hitboxes.Add(Capsula(z, Osso("chest"), P("chest"), P("neck"), Raio(smr, P("chest"), P("neck"), 0.6f, "chest"), RegiaoDoCorpo.Tronco));
            foreach (var lado in new[] { "L", "R" })
            {
                bool e = lado == "L";
                Vector3 ombro = P($"upper_arm.{lado}"), cotovelo = P($"forearm.{lado}"), mao = P($"hand.{lado}");
                Vector3 dedos = mao + (mao - cotovelo).normalized * 0.12f;
                hitboxes.Add(Capsula(z, Osso($"upper_arm.{lado}"), ombro, cotovelo, Raio(smr, ombro, cotovelo, 0.75f, $"upper_arm.{lado}"), RegiaoDoCorpo.Braco, e ? Membro.BracoE : Membro.BracoD));
                hitboxes.Add(Capsula(z, Osso($"forearm.{lado}"), cotovelo, dedos, Raio(smr, cotovelo, dedos, 0.75f, $"forearm.{lado}", $"hand.{lado}"), RegiaoDoCorpo.Braco, e ? Membro.AntebracoE : Membro.AntebracoD));
                Vector3 quadril = P($"thigh.{lado}"), joelho = P($"shin.{lado}"), pe = P($"foot.{lado}");
                hitboxes.Add(Capsula(z, Osso($"thigh.{lado}"), quadril, joelho, Raio(smr, quadril, joelho, 0.7f, $"thigh.{lado}"), RegiaoDoCorpo.Perna, e ? Membro.CoxaE : Membro.CoxaD));
                hitboxes.Add(Capsula(z, Osso($"shin.{lado}"), joelho, pe, Raio(smr, joelho, pe, 0.7f, $"shin.{lado}"), RegiaoDoCorpo.Perna, e ? Membro.CanelaE : Membro.CanelaD));
            }

            smr.sharedMesh = m.inteira;
            smr.sharedMaterials = new[] { m.material };
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;

            z.modelo = m.indice;
            z.animador = animador;
            z.agente = agente;
            z.capsula = capsula;
            z.hitboxes = hitboxes.ToArray();
            z.renderers = new Renderer[] { smr };
            z.voz = voz;
            z.raizDoModelo = modelo.transform;
            z.esqueleto = smr.bones;
            z.partes = CorpoDeZumbi.Partes.Select(Osso).ToArray();
            z.malhaInteira = m.inteira;
            z.malhasDeLonge = m.longe;
            z.materialPele = m.material;
            z.materialCarne = Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat");

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, $"{Prefabs}/Zumbi_{m.nome}.prefab");
            Object.DestroyImmediate(raiz);
            return salvo.GetComponent<Zumbi>();
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

        /// <summary>Uma cápsula entre dois pontos (na pose de repouso), presa ao osso que a move.</summary>
        static Hitbox Capsula(Zumbi z, Transform osso, Vector3 de, Vector3 ate, float raio, RegiaoDoCorpo regiao, Membro membro = Membro.Nenhum)
        {
            var h = NovaHitbox(z, osso, $"{regiao} ({osso.name})", regiao, out var go);
            h.membro = membro;
            go.transform.SetPositionAndRotation((de + ate) * 0.5f, Quaternion.FromToRotation(Vector3.up, ate - de));
            float escala = Mathf.Max(0.0001f, go.transform.lossyScale.x);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = raio / escala;
            c.height = (Vector3.Distance(de, ate) + 2f * raio) / escala;
            h.colisor = c;
            return h;
        }

        // ------------------------------------------------------------------ o corpo que cai (ragdoll)

        /// <summary>
        /// O corpo do modelo: o FBX desempacotado, sem Animator, na pose de repouso, na camada "Corpos". Onze partes têm
        /// corpo rígido e colisor (o mesmo tamanho das hitboxes, um pouco mais fino), ligadas por juntas com os limites do
        /// corpo humano. As juntas são criadas nesta pose, e é dela que os limites contam.
        /// </summary>
        static CorpoDeZumbi MontarCorpo(Modelo m)
        {
            var raiz = new GameObject("Corpo_" + m.nome) { layer = CorpoDeZumbi.Camada };
            var c = raiz.AddComponent<CorpoDeZumbi>();
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(Carregar<GameObject>(m.fbx));
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            Object.DestroyImmediate(modelo.GetComponent<Animator>());
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CorpoDeZumbi.Camada;
            var smrs = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var smr = Principal(smrs);
            foreach (var s in smrs)
                if (s != smr) Object.DestroyImmediate(s.gameObject);   // o corpo que cai é sempre o inteiro (LOD0)
            PoseDeRepouso(smr);
            Transform Osso(string nome) => smr.bones.First(t => t.name == nome);
            Vector3 P(string nome) => Osso(nome).position;

            var partes = new Rigidbody[PartesDoCorpo.Length];
            for (int i = 0; i < PartesDoCorpo.Length; i++)
            {
                var (nome, ate, massa) = PartesDoCorpo[i];
                var osso = Osso(nome);
                var rb = osso.gameObject.AddComponent<Rigidbody>();
                rb.mass = massa;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.solverIterations = 12;
                partes[i] = rb;
                float escala = Mathf.Max(0.0001f, osso.lossyScale.x);
                if (ate == null)
                {
                    var (centro, raio) = Cranio(m, smr.transform.localToWorldMatrix);
                    var esfera = osso.gameObject.AddComponent<SphereCollider>();
                    esfera.center = osso.InverseTransformPoint(centro);
                    esfera.radius = raio * 0.85f / escala;
                    continue;
                }
                Vector3 de = osso.position, fim = P(ate);
                if (nome.StartsWith("forearm")) fim += (fim - de).normalized * 0.1f;   // a mão vai junto
                var ossos = nome == "hips" ? new[] { "hips", "spine" } : nome.StartsWith("forearm") ? new[] { nome, nome.Replace("forearm", "hand") } : new[] { nome };
                float r = Raio(smr, de, fim, 0.6f, ossos) * 0.9f;
                var cap = osso.gameObject.AddComponent<CapsuleCollider>();
                Vector3 local = osso.InverseTransformPoint(fim);
                // o eixo da cápsula é o eixo do osso mais alinhado com o segmento (no Blender, o osso aponta em Y)
                Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                cap.direction = abs.y >= abs.x && abs.y >= abs.z ? 1 : abs.x >= abs.z ? 0 : 2;
                cap.center = local * 0.5f;
                cap.radius = r / escala;
                cap.height = local.magnitude + 2f * r / escala;
            }

            // as juntas: eixo de giro principal e eixo de balanço (no mundo, na pose de repouso: o modelo olha para +Z,
            // a direita dele é +X), limites do giro (baixo, alto) e dos dois balanços, em graus
            Vector3 D = Vector3.right, F = Vector3.forward, C = Vector3.up;
            void Junta(int parte, int pai, Vector3 giro, Vector3 balanco, float baixo, float alto, float balanco1, float balanco2)
            {
                var rb = partes[parte];
                var j = rb.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = partes[pai];
                j.anchor = Vector3.zero;
                j.axis = rb.transform.InverseTransformDirection(giro);
                j.swingAxis = rb.transform.InverseTransformDirection(balanco);
                j.lowTwistLimit = new SoftJointLimit { limit = baixo };
                j.highTwistLimit = new SoftJointLimit { limit = alto };
                j.swing1Limit = new SoftJointLimit { limit = balanco1 };
                j.swing2Limit = new SoftJointLimit { limit = balanco2 };
                j.enableProjection = true;
                j.enablePreprocessing = false;
            }
            // tronco e cabeça: dobrar para a frente é positivo (em volta da direita)
            Junta(1, 0, D, F, -20f, 40f, 15f, 15f);
            Junta(2, 1, D, F, -30f, 40f, 25f, 25f);
            // braços (abertos na pose): sobem e descem em volta da frente, vão para a frente e para trás em volta do alto;
            // o cotovelo dobra para a frente em volta do alto (o sinal muda de um lado para o outro)
            Junta(3, 1, F, C, -80f, 80f, 70f, 30f);
            Junta(4, 3, C, D, -5f, 130f, 5f, 5f);
            Junta(5, 1, F, C, -80f, 80f, 70f, 30f);
            Junta(6, 5, C, D, -130f, 5f, 5f, 5f);
            // pernas: o quadril dobra para a frente (negativo em volta da direita); o joelho só dobra para trás
            Junta(7, 0, D, F, -100f, 30f, 30f, 15f);
            Junta(8, 7, D, F, -5f, 120f, 3f, 3f);
            Junta(9, 0, D, F, -100f, 30f, 30f, 15f);
            Junta(10, 9, D, F, -5f, 120f, 3f, 3f);

            smr.sharedMesh = m.inteira;
            smr.sharedMaterials = new[] { m.material };
            smr.rootBone = Osso("hips");   // o quadril voa com o corpo; a raiz do esqueleto fica parada
            smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 2.6f);
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;

            c.pele = smr;
            c.malhaInteira = m.inteira;
            c.malhaSemCabeca = m.semCabeca;
            c.materiaisInteira = new[] { m.material };
            c.materiaisSemCabeca = new[] { m.material, Carregar<Material>(Arte + "/Materials/Zombie_Flesh.mat") };
            c.esqueleto = smr.bones;
            c.partes = partes;
            c.pescoco = Osso("neck");

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, $"{Prefabs}/Corpo_{m.nome}.prefab");
            Object.DestroyImmediate(raiz);
            return salvo.GetComponent<CorpoDeZumbi>();
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

        static void MontarCena(Scene cena, Zumbi[] prefabs, CorpoDeZumbi[] corpos, (Material sangue, Material furo) decals)
        {
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Zumbis");
            if (velho != null) Object.DestroyImmediate(velho);
            var go = new GameObject("Zumbis");
            go.AddComponent<NetworkObject>();
            var m = go.AddComponent<ZombieManager>();
            go.AddComponent<BenchmarkDeZumbis>();
            m.modelos = prefabs;
            m.corpos = corpos;
            m.corposPorModelo = Mathf.Max(8, CorposNoTotal / prefabs.Length);
            m.vozDoZumbi = Carregar<AudioClip>("Assets/_Game/Audio/SomZombie1.mp3");
            m.areasLivres = new[]
            {
                new ZombieManager.AreaLivre { centro = new Vector3(-20f, 0f, -20f), raio = 11f },   // a casa da família e o bunker
                new ZombieManager.AreaLivre { centro = new Vector3(-20f, 0f, -4.4f), raio = 12f },  // a frente da casa
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
            superficie.layerMask = ~((1 << JogadorRede.CamadaBracos) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));
            superficie.BuildNavMesh();
            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoNavMesh));
            AssetDatabase.CreateAsset(superficie.navMeshData, ArquivoNavMesh);
            EditorUtility.SetDirty(superficie);
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"NavMesh da cidade: {tri.vertices.Length} vértices, {tri.indices.Length / 3} triângulos.");
        }
    }
}
