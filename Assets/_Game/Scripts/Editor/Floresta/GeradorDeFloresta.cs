using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O gerador de floresta (o Plano_Floresta, 09/11/2026, pelo prompt do usuário: "Forest Asset Generator"): a janela
    /// Zombie City Cleaner > Floresta > Gerador, que monta os modelos por código (<see cref="ArvoresProcedurais"/>),
    /// mostra a prévia na cena, grava as malhas (<c>Art/Floresta/Malhas/&lt;nome&gt;[_Longe1|_Longe2].asset</c>) e os
    /// prefabs (<c>Art/Floresta/Prefabs</c>: o LODGroup com os três LODs, o material da vegetação, a cápsula do tronco), e
    /// a cena de teste (<c>Scenes/Exemplos/Floresta.unity</c>: a fileira das variações, a mata de teste e a área dos
    /// LODs). As malhas geradas entram no jogo pelo nome, como as do Cidade_Arvores.fbx (<see cref="MalhaGerada"/>).
    /// A fase 1 são os pinheiros (os três tamanhos, cinco variações cada).
    /// </summary>
    public class GeradorDeFloresta : EditorWindow
    {
        public const string Pasta = "Assets/_Game/Art/Floresta";
        public const string PastaDasMalhas = Pasta + "/Malhas";
        public const string PastaDosPrefabs = Pasta + "/Prefabs";
        public const string Cena = "Assets/_Game/Scenes/Exemplos/Floresta.unity";
        const string NomeDaPrevia = "Prévia do gerador de floresta";
        static readonly string[] Sufixos = { "", "_Longe1", "_Longe2" };

        ArvoresProcedurais.Pinheiro _p = ArvoresProcedurais.Sortear(ArvoresProcedurais.TamanhoDoPinheiro.Alto, 1001);
        int _variacao = 1;
        Vector2 _rolagem;

        [MenuItem("Zombie City Cleaner/Floresta/Gerador", false, 1)]
        public static void Abrir() => GetWindow<GeradorDeFloresta>("Gerador de floresta");

        void OnGUI()
        {
            _rolagem = EditorGUILayout.BeginScrollView(_rolagem);
            GUILayout.Label("Pinheiros (a fase 1)", EditorStyles.boldLabel);
            var tamanho = (ArvoresProcedurais.TamanhoDoPinheiro)EditorGUILayout.EnumPopup("Tamanho", _p.tamanho);
            int variacao = EditorGUILayout.IntSlider("Variação", _variacao, 1, ArvoresProcedurais.VariacoesPorTamanho);
            if (tamanho != _p.tamanho || variacao != _variacao)
            {
                _variacao = variacao;
                _p = ArvoresProcedurais.Sortear(tamanho, ArvoresProcedurais.Semente(tamanho, variacao));
            }
            int semente = EditorGUILayout.IntField("Semente", _p.semente);
            if (semente != _p.semente) _p = ArvoresProcedurais.Sortear(tamanho, semente);
            _p.altura = EditorGUILayout.Slider("Altura (m)", _p.altura, 2f, 20f);
            _p.largura = EditorGUILayout.Slider("Largura da copa", _p.largura, 0.6f, 1.6f);
            _p.andares = EditorGUILayout.IntSlider("Andares", _p.andares, 3, 14);
            _p.pontas = EditorGUILayout.IntSlider("Pontas por andar", _p.pontas, 5, 10);
            _p.caida = EditorGUILayout.Slider("Caída das pontas", _p.caida, 0f, 1f);
            _p.torto = EditorGUILayout.Slider("Tronco torto", _p.torto, 0f, 1f);
            _p.raioDoTronco = EditorGUILayout.Slider("Raio do tronco (m)", _p.raioDoTronco, 0.05f, 0.6f);
            _p.comecoDaCopa = EditorGUILayout.Slider("Começo da copa", _p.comecoDaCopa, 0.05f, 0.5f);
            _p.tom = EditorGUILayout.Slider("Tom (pinheiro / abeto)", _p.tom, 0f, 1f);
            GUILayout.Label($"Triângulos: {ArvoresProcedurais.TriangulosDoPinheiro(_p, 0)} / {ArvoresProcedurais.TriangulosDoPinheiro(_p, 1)} / {ArvoresProcedurais.TriangulosDoPinheiro(_p, 2)} (LOD 0 / 1 / 2)");
            if (GUILayout.Button("Prévia na cena (os 3 LODs lado a lado)")) Previa(_p);
            if (GUILayout.Button("Sortear de novo")) _p = ArvoresProcedurais.Sortear(tamanho, _p.semente + 17);
            GUILayout.Space(10);
            GUILayout.Label("Os assets", EditorStyles.boldLabel);
            if (GUILayout.Button("Gerar todos os pinheiros (15 variações, 3 LODs, os prefabs)")) GerarPinheiros();
            if (GUILayout.Button("Montar a cena de teste (Scenes/Exemplos/Floresta)")) MontarCena();
            if (GUILayout.Button("Pôr as malhas geradas no Lakewood (a cena aberta)")) AtualizarLakewood();
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ a prévia

        static void Previa(ArvoresProcedurais.Pinheiro p)
        {
            var velha = GameObject.Find(NomeDaPrevia);
            if (velha != null) DestroyImmediate(velha);
            var raiz = new GameObject(NomeDaPrevia);
            var material = MaterialDaVegetacao();
            for (int lod = 0; lod < 3; lod++)
            {
                var malha = ArvoresProcedurais.MalhaDoPinheiro(p, lod, $"Prévia LOD{lod}");
                var go = new GameObject($"LOD {lod} ({malha.triangles.Length / 3} tris)");
                go.transform.SetParent(raiz.transform, false);
                go.transform.localPosition = new Vector3(lod * (p.altura * 0.5f + 3f), 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            Selection.activeGameObject = raiz;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // ------------------------------------------------------------------ os assets

        public static Material MaterialDaVegetacao()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            if (m != null) return m;
            var temp = new GameObject("temp");
            MontagemDoMundo.PorAFloresta(temp);
            DestroyImmediate(temp);
            return AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
        }

        /// <summary>A malha gerada de um nome (com o sufixo do LOD), ou null se não foi gerada.</summary>
        public static Mesh MalhaGerada(string nomeComSufixo) => AssetDatabase.LoadAssetAtPath<Mesh>($"{PastaDasMalhas}/{nomeComSufixo}.asset");

        [MenuItem("Zombie City Cleaner/Floresta/Gerar os pinheiros", false, 10)]
        public static void GerarPinheiros()
        {
            Directory.CreateDirectory(PastaDasMalhas);
            Directory.CreateDirectory(PastaDosPrefabs);
            var material = MaterialDaVegetacao();
            int tris = 0;
            var relatorio = new List<string>();
            foreach (ArvoresProcedurais.TamanhoDoPinheiro t in System.Enum.GetValues(typeof(ArvoresProcedurais.TamanhoDoPinheiro)))
                for (int v = 1; v <= ArvoresProcedurais.VariacoesPorTamanho; v++)
                {
                    var p = ArvoresProcedurais.Sortear(t, ArvoresProcedurais.Semente(t, v));
                    string nome = ArvoresProcedurais.Nome(t, v);
                    var malhas = new Mesh[3];
                    for (int lod = 0; lod < 3; lod++)
                    {
                        malhas[lod] = GravarMalha(ArvoresProcedurais.MalhaDoPinheiro(p, lod, nome + Sufixos[lod]), $"{PastaDasMalhas}/{nome}{Sufixos[lod]}.asset");
                        if (lod == 0) tris += malhas[0].triangles.Length / 3;
                    }
                    GravarPrefab(nome, malhas, material, Floresta.RaioDoTronco[(int)EspecieDe(t)], p.altura * 0.9f);
                    relatorio.Add($"{nome}: {p.altura:F1} m, {malhas[0].triangles.Length / 3}/{malhas[1].triangles.Length / 3}/{malhas[2].triangles.Length / 3} tris");
                }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Floresta] 15 pinheiros gerados em {PastaDasMalhas} e {PastaDosPrefabs} (LOD0 médio: {tris / 15} tris).\n" + string.Join("\n", relatorio));
        }

        static Especie EspecieDe(ArvoresProcedurais.TamanhoDoPinheiro t) => t switch
        {
            ArvoresProcedurais.TamanhoDoPinheiro.Alto => Especie.PinheiroAlto1,
            ArvoresProcedurais.TamanhoDoPinheiro.Medio => Especie.PinheiroMedio1,
            _ => Especie.PinheiroPequeno1,
        };

        // grava a malha num asset (trocando o conteúdo do que já existe: os prefabs e as cenas que a usam continuam valendo)
        static Mesh GravarMalha(Mesh nova, string caminho)
        {
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha == null)
            {
                AssetDatabase.CreateAsset(nova, caminho);
                return nova;
            }
            velha.Clear();
            velha.indexFormat = nova.indexFormat;
            velha.SetVertices(nova.vertices);
            velha.SetNormals(nova.normals);
            velha.SetUVs(0, nova.uv);
            velha.SetColors(nova.colors32);
            velha.SetTriangles(nova.triangles, 0, true);
            velha.RecalculateBounds();
            EditorUtility.SetDirty(velha);
            DestroyImmediate(nova);
            return velha;
        }

        static void GravarPrefab(string nome, Mesh[] malhas, Material material, float raioDoTronco, float alturaDoTronco)
        {
            var raiz = new GameObject(nome);
            var grupo = raiz.AddComponent<LODGroup>();
            var lods = new LOD[3];
            float[] telas = { 0.18f, 0.06f, 0.012f };
            for (int lod = 0; lod < 3; lod++)
            {
                var go = new GameObject($"LOD{lod}");
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malhas[lod];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                lods[lod] = new LOD(telas[lod], new Renderer[] { r });
            }
            grupo.SetLODs(lods);
            grupo.RecalculateBounds();
            if (raioDoTronco > 0f)
            {
                var capsula = raiz.AddComponent<CapsuleCollider>();
                capsula.radius = raioDoTronco;
                capsula.height = alturaDoTronco + raioDoTronco * 2f;
                capsula.center = new Vector3(0f, alturaDoTronco * 0.5f, 0f);
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, $"{PastaDosPrefabs}/{nome}.prefab");
            DestroyImmediate(raiz);
        }

        // ------------------------------------------------------------------ a cena de teste

        [MenuItem("Zombie City Cleaner/Exemplos/Floresta (os modelos)", false, 60)]
        public static void MontarCena()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena da floresta.");
                return;
            }
            if (!File.Exists($"{PastaDosPrefabs}/{ArvoresProcedurais.Nome(ArvoresProcedurais.TamanhoDoPinheiro.Alto, 1)}.prefab")) GerarPinheiros();
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.57f, 0.59f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.45f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.29f, 0.29f, 0.27f);
            var ceu = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoCeu.Caminho);
            if (ceu != null) RenderSettings.skybox = ceu;

            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.color = new Color(1f, 0.95f, 0.86f);
            sol.intensity = 1.1f;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            // o céu de dia na cena de teste (no jogo, o AmbienteDoDia põe as globais a cada quadro)
            var direcao = -sol.transform.forward;
            Shader.SetGlobalColor("_ZccCeuTopo", new Color(0.4f, 0.52f, 0.7f));
            Shader.SetGlobalColor("_ZccCeuHorizonte", new Color(0.6f, 0.64f, 0.68f));
            Shader.SetGlobalColor("_ZccCeuNeblina", new Color(0.66f, 0.7f, 0.72f));
            Shader.SetGlobalVector("_ZccSol", new Vector4(direcao.x, direcao.y, direcao.z, 1f));
            Shader.SetGlobalColor("_ZccSolCor", new Color(1.3f, 1.25f, 1.15f));
            Shader.SetGlobalVector("_ZccLua", new Vector4(direcao.x, direcao.y, direcao.z, 0f));
            Shader.SetGlobalFloat("_ZccNuvens", 0.1f);
            Shader.SetGlobalFloat("_ZccEstrelas", 0f);

            // o chão: um plano grande com o material do chão (a cor do vértice branca: a mistura das texturas)
            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chão";
            chao.transform.localScale = new Vector3(40f, 1f, 40f);
            chao.transform.position = new Vector3(100f, 0f, 60f);
            var matChao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            if (matChao != null) chao.GetComponent<MeshRenderer>().sharedMaterial = matChao;

            // a fileira: uma linha por tamanho, as cinco variações de cada
            var fileira = new GameObject("Fileira");
            var alturas = new[] { ArvoresProcedurais.TamanhoDoPinheiro.Alto, ArvoresProcedurais.TamanhoDoPinheiro.Medio, ArvoresProcedurais.TamanhoDoPinheiro.Pequeno };
            for (int t = 0; t < 3; t++)
                for (int v = 1; v <= ArvoresProcedurais.VariacoesPorTamanho; v++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosPrefabs}/{ArvoresProcedurais.Nome(alturas[t], v)}.prefab");
                    if (prefab == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetParent(fileira.transform, false);
                    go.transform.position = new Vector3(10f + (v - 1) * 9f, 0f, 10f + t * 16f);
                }

            // a mata de teste: uma grade sacudida de 5 m, 40 × 40 m, os três tamanhos misturados
            var mata = new GameObject("Mata de teste");
            var rnd = new System.Random(5);
            for (int j = 0; j < 8; j++)
            for (int i = 0; i < 8; i++)
            {
                double s = rnd.NextDouble();
                var tam = s < 0.45 ? ArvoresProcedurais.TamanhoDoPinheiro.Alto : s < 0.8 ? ArvoresProcedurais.TamanhoDoPinheiro.Medio : ArvoresProcedurais.TamanhoDoPinheiro.Pequeno;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosPrefabs}/{ArvoresProcedurais.Nome(tam, 1 + rnd.Next(ArvoresProcedurais.VariacoesPorTamanho))}.prefab");
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(mata.transform, false);
                go.transform.position = new Vector3(70f + (i + (float)rnd.NextDouble()) * 5f, 0f, 10f + (j + (float)rnd.NextDouble()) * 5f);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                float escala = 0.85f + (float)rnd.NextDouble() * 0.3f;
                go.transform.localScale = Vector3.one * escala;
            }

            // a área dos LODs: a mesma árvore a 10, 40, 90 e 160 m da câmera
            var lods = new GameObject("Área dos LODs");
            float[] distancias = { 10f, 40f, 90f, 160f };
            var alto = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosPrefabs}/{ArvoresProcedurais.Nome(ArvoresProcedurais.TamanhoDoPinheiro.Alto, 2)}.prefab");
            foreach (var d in distancias)
            {
                if (alto == null) break;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(alto);
                go.transform.SetParent(lods.transform, false);
                go.transform.position = new Vector3(130f, 0f, 60f + d);
            }

            var cam = new GameObject("Câmera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(28f, 1.7f, -6f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            cam.farClipPlane = 2000f;
            cam.clearFlags = ceu != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.68f, 0.78f);

            Directory.CreateDirectory(Path.GetDirectoryName(Cena));
            EditorSceneManager.SaveScene(cena, Cena);
            Debug.Log($"[Floresta] A cena de teste em {Cena}: a fileira (15 pinheiros), a mata de teste (64) e a área dos LODs.");
        }

        // ------------------------------------------------------------------ o Lakewood

        /// <summary>As malhas das plantas na ordem de Floresta.Nomes × 3 LODs: as do FBX ou as geradas (pelo nome).</summary>
        public static Mesh[] MalhasDasPlantas()
        {
            var fbx = AssetDatabase.LoadAllAssetsAtPath(MontagemDoMundo.FbxDasArvores).OfType<Mesh>().ToDictionary(m => m.name);
            return Floresta.Nomes.SelectMany(n => Sufixos.Select(s => fbx.TryGetValue(n + s, out var m) ? m : MalhaGerada(n + s))).ToArray();
        }

        /// <summary>
        /// A floresta nova (os pinheiros leves e a grama densa) na cena do Lakewood (09/11/2026), sem trocar a cena aberta: a
        /// do Lakewood abre junto, recebe os prefabs e sai.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Floresta/Pôr a floresta nova no Lakewood", false, 11)]
        public static void AtualizarLakewood()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Floresta] Saia do Play antes.");
                return;
            }
            var cena = SceneManager.GetSceneByPath(MontagemDeLakewood.Cena);
            bool abriu = !cena.isLoaded;
            if (abriu) cena = EditorSceneManager.OpenScene(MontagemDeLakewood.Cena, OpenSceneMode.Additive);
            LakewoodNaCena lw = null;
            foreach (var raiz in cena.GetRootGameObjects())
                if (lw == null) lw = raiz.GetComponentInChildren<LakewoodNaCena>(true);
            if (lw == null) Debug.LogWarning($"[Floresta] A cena {MontagemDeLakewood.Cena} não tem o LakewoodNaCena.");
            else
            {
                MontagemDeLakewood.PorAFloresta(lw);
                EditorSceneManager.MarkSceneDirty(cena);
                EditorSceneManager.SaveScene(cena);
                Debug.Log($"[Floresta] A floresta nova no Lakewood: {lw.pinheiros.Count(p => p != null)} pinheiros leves e a grama densa ({(lw.materialDaGrama != null ? "ok" : "faltando")}).");
            }
            if (abriu) EditorSceneManager.CloseScene(cena, true);
        }
    }
}
