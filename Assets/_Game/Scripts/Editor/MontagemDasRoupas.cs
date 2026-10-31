using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As roupas e os NPCs (o Plano_Roupas.md da pasta de arte, 30/10/2026), do <c>Roupas.fbx</c>, das tiras das texturas
    /// e do <c>Roupas.json</c> do <c>roupas_exportar.py</c> (o <c>Roupas.blend</c> de lá), em <c>Art/Characters/Roupas</c>:
    ///  - a importação do FBX: Generic, o avatar dele, as malhas legíveis (a <see cref="Vestimenta"/> tira as faces
    ///    cobertas), as blend shapes (as formas do corpo: a "Mulher"), sem animação nem materiais (as animações são as do
    ///    personagem principal: o esqueleto é o mesmo);
    ///  - as duas Texture2DArray das roupas (a cor em sRGB e a máscara linear), uma camada por peça do catálogo, e as duas
    ///    texturas do corpo (<c>T_Corpo_Cor</c> e <c>T_Corpo_Mascara</c>: a pele, a cueca e as sobrancelhas);
    ///  - o material das roupas e do corpo (<c>M_Roupas.mat</c>, o shader ZombieCityCleaner/Roupas);
    ///  - o controlador do mostruário (<c>NPC_Mostruario.controller</c>: o corpo todo de cada animação e, na camada dos
    ///    braços, a guarda);
    ///  - o prefab do NPC (<c>Prefabs/Roupas/NPC.prefab</c>: o modelo, o material das roupas no corpo e nas peças, o
    ///    Animator e a <see cref="Vestimenta"/>).
    /// Menu: Zombie City Cleaner > Roupas e NPCs > Montar; a cena do mostruário: Zombie City Cleaner > Exemplos > Roupas e NPCs.
    /// </summary>
    public static class MontagemDasRoupas
    {
        public const string Arte = "Assets/_Game/Art/Characters/Roupas";
        public const string Fbx = Arte + "/Roupas.fbx";
        public const string Json = Arte + "/Roupas.json";
        const string TiraDasCores = Arte + "/T_Roupas_Cor.png";
        const string TiraDasMascaras = Arte + "/T_Roupas_Mascara.png";
        const string CorDoCorpo = Arte + "/T_Corpo_Cor.png";
        const string MascaraDoCorpo = Arte + "/T_Corpo_Mascara.png";
        public const string ArquivoDoMaterial = Arte + "/M_Roupas.mat";
        public const string Controlador = Arte + "/NPC_Mostruario.controller";
        public const string Prefab = "Assets/_Game/Prefabs/Roupas/NPC.prefab";
        public const string Cena = "Assets/_Game/Scenes/Exemplos/Roupas.unity";
        // os sorteados de cada tipo no fim da fila do mostruário (as sementes 1 a esta)
        const int SorteadosPorTipo = 2;
        const string FbxDoPrincipal = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco.fbx";
        const string MascaraDoTronco = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco_Tronco.mask";

        [MenuItem("Zombie City Cleaner/Roupas e NPCs/Montar")]
        public static void Montar()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Json);
            if (json == null) throw new FileNotFoundException("Falta o catálogo das roupas (o roupas_exportar.py da pasta de arte)", Json);
            var catalogo = CatalogoDasRoupas.Ler(json);
            Importar();
            int colunas = Mathf.Max(1, catalogo.colunas_da_tira);
            var cores = Array(TiraDasCores, catalogo.pecas.Length, colunas, true);
            var mascaras = Array(TiraDasMascaras, catalogo.pecas.Length, colunas, false);
            var material = Material(cores, mascaras, Textura(CorDoCorpo, true), Textura(MascaraDoCorpo, false));
            var ctrl = MontarControlador();
            MontarPrefab(material, ctrl, json);
            int itens = MontarItens(material, catalogo);
            AssetDatabase.SaveAssets();
            Debug.Log($"Roupas montadas: {catalogo.pecas.Length} peças ({string.Join(", ", catalogo.pecas.Select(p => $"{p.nome} {p.triangulos}"))}), " +
                      $"{catalogo.npcs.Length} NPCs, o material, o controlador do mostruário, o prefab {Prefab} e {itens} itens em {PastaDosItens}.");
        }

        // ------------------------------------------------------------------ as roupas como itens

        public const string FbxDosItens = Arte + "/Roupas_Itens.fbx";
        public const string PastaDosItens = "Assets/_Game/Prefabs/Roupas/Itens";

        /// <summary>
        /// Os itens das roupas (o lote 6, 31/10/2026): do <c>Roupas_Itens.fbx</c> (o <c>roupas_itens.py</c> da pasta de
        /// arte: as malhas paradas, com a base na origem), um prefab por peça em <c>Prefabs/Roupas/Itens</c>
        /// (<c>Item_&lt;peça&gt;</c>): a malha, o material das roupas, a caixa de colisão e o <see cref="RoupaComoItem"/> com o
        /// lugar e as cores do catálogo. Os de peças que saíram são apagados.
        /// </summary>
        static int MontarItens(Material material, CatalogoDasRoupas catalogo)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxDosItens);
            if (imp == null)
            {
                Debug.LogWarning($"Falta o {FbxDosItens} (o roupas_itens.py da pasta de arte): sem os itens das roupas.");
                return 0;
            }
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.isReadable = false;
            imp.importNormals = ModelImporterNormals.Calculate;
            imp.normalSmoothingAngle = 0f;
            imp.importCameras = false;
            imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.generateSecondaryUV = false;
            imp.SaveAndReimport();
            Directory.CreateDirectory(PastaDosItens);
            var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxDosItens).OfType<Mesh>().Where(m => m.name.StartsWith("Item_")).ToArray();
            var feitos = new System.Collections.Generic.HashSet<string>();
            foreach (var m in malhas)
            {
                var dados = catalogo.DaPeca(m.name.Substring(5));
                var go = new GameObject(m.name);
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                    var caixa = go.AddComponent<BoxCollider>();
                    caixa.center = m.bounds.center;
                    caixa.size = Vector3.Max(m.bounds.size, Vector3.one * 0.02f);
                    var it = go.AddComponent<RoupaComoItem>();
                    it.peca = m.name.Substring(5);
                    it.lugar = dados?.lugar;
                    it.cores = dados?.cores ?? new string[0];
                    string caminho = $"{PastaDosItens}/{m.name}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, caminho);
                    feitos.Add(caminho);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PastaDosItens }))
            {
                string c = AssetDatabase.GUIDToAssetPath(guid);
                if (!feitos.Contains(c)) AssetDatabase.DeleteAsset(c);
            }
            return malhas.Length;
        }

        /// <summary>Os prefabs dos itens das roupas, na ordem do catálogo, e o lugar de cada um (para as mesas da cena das
        /// variações, que não vê o <see cref="RoupaComoItem"/>).</summary>
        public static (GameObject[] prefabs, string[] lugares) CarregarItens()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Json);
            var ordem = json == null ? new string[0] : CatalogoDasRoupas.Ler(json).pecas.Select(p => p.nome).ToArray();
            var lista = AssetDatabase.FindAssets("t:Prefab", new[] { PastaDosItens })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.GetComponent<RoupaComoItem>() != null)
                .OrderBy(p => { int i = System.Array.IndexOf(ordem, p.GetComponent<RoupaComoItem>().peca); return i < 0 ? int.MaxValue : i; })
                .ToArray();
            return (lista, lista.Select(p => p.GetComponent<RoupaComoItem>().lugar).ToArray());
        }

        static void Importar()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) throw new FileNotFoundException("Falta o Roupas.fbx (o roupas_exportar.py da pasta de arte)", Fbx);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.isReadable = true;
            imp.importAnimation = false;
            // as formas do corpo (a "Mulher"), com as normais refeitas pela forma (o sombreado plano: o ângulo 0)
            imp.importBlendShapes = true;
            imp.importBlendShapeNormals = ModelImporterNormals.Calculate;
            imp.normalSmoothingAngle = 0f;
            imp.importCameras = false;
            imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.generateSecondaryUV = false;
            imp.SaveAndReimport();
        }

        // a grade das texturas (tantas colunas por linha, a 0 em cima à esquerda; a Unity lê da esquerda para a direita e de
        // cima para baixo) importada como Texture2DArray, uma camada por peça (as que sobram na última linha ficam pretas).
        // Numa coluna só, com 65 peças, a imagem passava de 32.768 px de altura, e a Unity não a lia.
        static Texture2DArray Array(string caminho, int camadas, int colunas, bool srgb)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.textureShape = TextureImporterShape.Texture2DArray;
            s.flipbookRows = (camadas + colunas - 1) / colunas;
            s.flipbookColumns = colunas;
            s.sRGBTexture = srgb;
            s.mipmapEnabled = true;
            s.wrapMode = TextureWrapMode.Clamp;
            s.filterMode = FilterMode.Trilinear;
            // o alfa da cor: os vidros (o shader os desenha em pontilhado)
            s.alphaSource = srgb ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            s.alphaIsTransparency = false;
            imp.SetTextureSettings(s);
            imp.maxTextureSize = 16384;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2DArray>(caminho);
        }

        // uma textura do corpo (a da textura do personagem principal, 1024 px)
        static Texture2D Textura(string caminho, bool srgb)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            if (imp == null) throw new FileNotFoundException("Falta a textura do corpo (o roupas_exportar.py da pasta de arte)", caminho);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        static Material Material(Texture2DArray cores, Texture2DArray mascaras, Texture2D corDoCorpo, Texture2D mascaraDoCorpo)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ArquivoDoMaterial);
            if (m == null)
            {
                m = new Material(Shader.Find("ZombieCityCleaner/Roupas"));
                AssetDatabase.CreateAsset(m, ArquivoDoMaterial);
            }
            m.shader = Shader.Find("ZombieCityCleaner/Roupas");
            m.SetTexture("_Cores", cores);
            m.SetTexture("_Mascaras", mascaras);
            m.SetTexture("_CorpoCor", corDoCorpo);
            m.SetTexture("_CorpoMascara", mascaraDoCorpo);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimationClip Clipe(string nome)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath(FbxDoPrincipal).OfType<AnimationClip>().FirstOrDefault(x => x.name == nome);
            if (c == null) Debug.LogWarning($"O clipe {nome} não está no {FbxDoPrincipal}.");
            return c;
        }

        /// <summary>O controlador do mostruário: um estado por animação do corpo todo (o pulo da janela repete) e, na
        /// camada dos braços (a máscara do peito para cima, o peso 0 até o mostruário pedir), a guarda.</summary>
        static AnimatorController MontarControlador()
        {
            AssetDatabase.DeleteAsset(Controlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var (estado, _) in MostruarioDasRoupas.Animacoes)
            {
                if (estado == "Guarda") continue;
                var e = sm.AddState(estado);
                e.motion = Clipe(estado);
                e.writeDefaultValues = false;
                if (estado == "Parado") sm.defaultState = e;
                if (estado == "Pular_Janela")
                {
                    var t = e.AddTransition(e);
                    t.hasExitTime = true; t.exitTime = 1f; t.duration = 0.15f; t.hasFixedDuration = true;
                }
            }
            ctrl.AddLayer("Braços");
            var camadas = ctrl.layers;
            camadas[1].defaultWeight = 0f;
            camadas[1].blendingMode = AnimatorLayerBlendingMode.Override;
            camadas[1].avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MascaraDoTronco);
            ctrl.layers = camadas;
            var guarda = ctrl.layers[1].stateMachine.AddState("Guarda");
            guarda.motion = Clipe("Maos_Guarda");
            guarda.writeDefaultValues = false;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void MontarPrefab(Material material, RuntimeAnimatorController ctrl, TextAsset json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            try
            {
                go.name = "NPC";
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = material;
                    r.updateWhenOffscreen = false;
                }
                // (no Editor, o GetComponent de um que falta não é null de verdade: o ?? não serve)
                var a = go.GetComponent<Animator>();
                if (a == null) a = go.AddComponent<Animator>();
                a.runtimeAnimatorController = ctrl;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var v = go.GetComponent<Vestimenta>();
                if (v == null) v = go.AddComponent<Vestimenta>();
                v.catalogo = json;
                PrefabUtility.SaveAsPrefabAsset(go, Prefab);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ a cena do mostruário

        /// <summary>
        /// A cena do mostruário (<c>Scenes/Exemplos/Roupas.unity</c>): o chão escuro, o sol e a luz de baixo, e os NPCs do
        /// catálogo em fila (o primeiro sem roupa: o corpo do personagem principal), com a câmera do
        /// <see cref="MostruarioDasRoupas"/>. Refaz a cena do zero; rode antes a montagem (o prefab).
        /// </summary>
        [MenuItem("Zombie City Cleaner/Exemplos/Roupas e NPCs")]
        public static void MontarCena()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena das roupas.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) throw new FileNotFoundException("Falta o prefab do NPC: rode Zombie City Cleaner > Roupas e NPCs > Montar", Prefab);
            var catalogo = CatalogoDasRoupas.Ler(AssetDatabase.LoadAssetAtPath<TextAsset>(Json));
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(cena, Cena);

            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.6f;
            sol.color = new Color(1f, 0.95f, 0.88f);
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
            var contra = new GameObject("Luz de trás").AddComponent<Light>();
            contra.type = LightType.Directional;
            contra.intensity = 0.5f;
            contra.color = new Color(0.75f, 0.82f, 1f);
            contra.shadows = LightShadows.None;
            contra.transform.rotation = Quaternion.Euler(30f, -20f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.43f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
            RenderSettings.fog = false;

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chão";
            chao.transform.localScale = new Vector3(6f, 1f, 2f);
            chao.transform.position = new Vector3(-catalogo.npcs.Length * 0.8f, 0f, 0f);
            var mChao = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mChao.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
            mChao.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mChao, Arte + "/M_Chao_Mostruario.mat");
            chao.GetComponent<MeshRenderer>().sharedMaterial = mChao;

            var camera = new GameObject("Câmera") { tag = "MainCamera" };
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.4f, 3.2f), Quaternion.Euler(10f, 180f, 0f));
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.055f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.AddComponent<AudioListener>();
            camera.AddComponent<MostruarioDasRoupas>();

            // os NPCs: o corpo sem roupa, os do catálogo e, no fim, os sorteados de cada tipo (as sementes 1 e 2), a 1,6 m
            // um do outro, de frente para a câmera (+Z); a fila vai para -X (de frente, da esquerda para a direita da tela)
            var fila = new List<(string nome, CatalogoDasRoupas.Npc npc, bool sorteado, int semente)> { ("Sem roupa", null, false, 0) };
            fila.AddRange(catalogo.npcs.Select(n => (n.nome, n, false, 0)));
            foreach (var t in catalogo.tipos ?? new CatalogoDasRoupas.Tipo[0])
                for (int s = 1; s <= SorteadosPorTipo; s++)
                    fila.Add(($"{t.nome} sorteado {s}", SorteioDosNPCs.Sortear(catalogo, t.nome, s), true, s));
            chao.transform.position = new Vector3(-fila.Count * 0.8f, 0f, 0f);
            chao.transform.localScale = new Vector3(Mathf.Max(6f, fila.Count * 0.17f), 1f, 2f);
            for (int i = 0; i < fila.Count; i++)
            {
                var (nome, npc, sorteado, semente) = fila[i];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = nome;
                go.transform.position = new Vector3(-i * 1.6f, 0f, 0f);
                var v = go.GetComponent<Vestimenta>();
                v.npc = sorteado || npc == null ? "" : npc.nome;
                v.tipo = npc?.tipo ?? "";
                v.semente = semente;
                if (npc != null)
                {
                    v.corpo = npc.corpo;
                    v.pele = npc.pele;
                }
                // no Editor (fora do Play), só as peças dele à mostra, no corpo dele
                var dele = npc == null ? new string[0] : npc.pecas.Select(p => Vestimenta.PrefixoDasPecas + p.nome).ToArray();
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.gameObject.SetActive(r.name == Vestimenta.Corpo || dele.Contains(r.name));
                    foreach (var f in catalogo.formas ?? new string[0])
                    {
                        int k = r.sharedMesh.GetBlendShapeIndex(f);
                        if (k >= 0) r.SetBlendShapeWeight(k, npc != null && npc.corpo == f ? 100f : 0f);
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Cena das roupas montada: {Cena} ({fila.Count} NPCs, {fila.Count(f => f.sorteado)} sorteados). No Play: ← → outro NPC, 1 a 8 a animação, R sorteia as cores, B troca o corpo, P a pele, G o tipo e N sorteia um NPC.");
        }
    }
}
