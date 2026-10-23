using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta a cidade (Parte 4): ruas, calçadas, o prédio de 4 andares, as 5 casas (uma é a da família, com o bunker
    /// embaixo), carros abandonados, cercas, postes, o anel de pinheiros e a barreira invisível.
    /// A arte final (Parte 8) trocou os blocos pelos modelos do Blender (pasta de arte, `Modelo 3D/Cidade`): objetos de
    /// rua, casas, prédio, bunker, móveis e o chão. O chão (ruas, calçadas, grama, praça, estacionamento) ainda colide
    /// com caixas invisíveis, nas mesmas alturas do visual.
    /// Menu: Zombie City Cleaner > Montar cidade em blocos. Pode rodar de novo: apaga e refaz a raiz "Cidade".
    /// </summary>
    public static class MontagemDaCidade
    {
        const string PastaMateriais = "Assets/_Game/Art/Materials/Blocos";
        const string PastaMalhas = "Assets/_Game/Art/Meshes";
        const string FbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
        const string FbxCasas = "Assets/_Game/Art/Cidade/Cidade_Casas.fbx";
        const string FbxCasasColisao = "Assets/_Game/Art/Cidade/Cidade_Casas_Colisao.fbx";
        const string FbxPredio = "Assets/_Game/Art/Cidade/Cidade_Predio.fbx";
        const string FbxPredioColisao = "Assets/_Game/Art/Cidade/Cidade_Predio_Colisao.fbx";
        const string FbxMoveis = "Assets/_Game/Art/Cidade/Cidade_Moveis.fbx";
        const string FbxBunker = "Assets/_Game/Art/Cidade/Cidade_Bunker.fbx";
        const string FbxBunkerColisao = "Assets/_Game/Art/Cidade/Cidade_Bunker_Colisao.fbx";
        const string FbxChao = "Assets/_Game/Art/Cidade/Cidade_Chao.fbx";
        const string CaminhoPaleta = "Assets/_Game/Art/Paleta.png";
        const string CaminhoMaterialCidade = "Assets/_Game/Art/Materials/Cidade.mat";
        const float Porta = 1.1f;      // largura das portas das casas (a mesma do cidade_casas.py)

        static Dictionary<string, Material> _m;
        static Dictionary<string, Mesh> _malhas;
        static Rect? _buracoNoChao;
        static Mesh _cone;
        static System.Random _rng;

        [MenuItem("Zombie City Cleaner/Montar cidade em blocos")]
        public static void Montar()
        {
            Directory.CreateDirectory(PastaMateriais);
            Directory.CreateDirectory(PastaMalhas);
            _rng = new System.Random(1987);
            Materiais();
            Malhas();
            ArteDaCidade();

            var cena = EditorSceneManager.GetActiveScene();
            foreach (var nome in new[] { "Cidade", "Teste (temporário)" })
            {
                var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == nome);
                if (velho != null) Object.DestroyImmediate(velho);
            }
            var raiz = new GameObject("Cidade").transform;

            _buracoNoChao = null;
            Ruas(raiz);
            Postes(raiz);
            Predio(Grupo(raiz, "Prédio"), new Vector3(20f, 0f, 21f), 0f);
            // o tamanho (W × D) tem de bater com o CASAS do cidade_casas.py, que fez o modelo de cada uma
            Casa(Grupo(raiz, "Casa 1"), "Casa_1", new Vector3(-27f, 0f, 19f), 0f, 10f, 8f, false);
            Casa(Grupo(raiz, "Casa 2"), "Casa_2", new Vector3(-12f, 0f, 23f), 0f, 9f, 8f, false);
            Casa(Grupo(raiz, "Casa da família"), "Casa_Familia", new Vector3(-20f, 0f, -18f), 180f, 12f, 10f, true);
            Casa(Grupo(raiz, "Casa 4"), "Casa_4", new Vector3(13f, 0f, -14f), 180f, 9f, 8f, false);
            Casa(Grupo(raiz, "Casa 5"), "Casa_5", new Vector3(28f, 0f, -26f), 270f, 10f, 8f, false);
            Praca(Grupo(raiz, "Praça"), new Vector3(24f, 0f, -12f));
            Estacionamento(Grupo(raiz, "Estacionamento"), new Vector3(10f, 0f, 33f));
            Carros(Grupo(raiz, "Carros"));
            Cercas(Grupo(raiz, "Cercas"));
            Arvores(Grupo(raiz, "Árvores"));
            Barricadas(Grupo(raiz, "Barricadas"));
            ObjetosDeRua(Grupo(raiz, "Objetos de rua"));
            Pinheiros(Grupo(raiz, "Pinheiros"));
            Barreira(Grupo(raiz, "Barreira invisível"));
            Chao(raiz);   // por último: precisa do buraco da escada do bunker, que a casa da família marca

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null || t.GetComponent<PosteDeLuz>() != null || t.name == "Cone de luz") continue;
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                if (EscondeOQueEstaAtras(t)) flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
            PontosNoBunker(cena);
            MontagemDosZumbis.AssarNavegacao(cena);   // a cidade mudou: o NavMesh dos zumbis precisa ser refeito
            AssarOclusao();
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Debug.Log("Cidade em blocos montada.");
        }

        // ------------------------------------------------------------------ oclusão

        /// <summary>
        /// Só a arquitetura grande esconde o que está atrás dela (Occluder): as casas, o prédio, o bunker e os pedaços do
        /// chão (que escondem o bunker de quem está na rua). O resto é só escondido (Occludee): objeto pequeno como
        /// oclusor só deixa a oclusão mais lenta de assar e maior, sem esconder nada.
        /// </summary>
        static bool EscondeOQueEstaAtras(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            string n = mf.sharedMesh.name;
            return n.StartsWith("Casa_") || n == "Predio" || n == "Bunker" || n.StartsWith("Chao_");
        }

        /// <summary>
        /// Assa a oclusão da cena (Umbra): o que está atrás de uma casa, do prédio ou embaixo do chão não é desenhado.
        /// Fica num arquivo junto da cena; precisa ser refeita sempre que a cidade é montada de novo (por isso fica aqui).
        /// </summary>
        static void AssarOclusao()
        {
            StaticOcclusionCulling.smallestOccluder = 3f;     // as paredes das casas e do prédio
            StaticOcclusionCulling.smallestHole = 0.25f;      // portas e janelas deixam ver através
            StaticOcclusionCulling.backfaceThreshold = 100f;
            var t0 = System.DateTime.Now;
            StaticOcclusionCulling.Compute();
            Debug.Log($"Oclusão assada em {(System.DateTime.Now - t0).TotalSeconds:F1} s.");
        }

        // ------------------------------------------------------------------ materiais e malhas

        static void Materiais()
        {
            // a cidade inteira usa o material "Cidade" (a paleta); aqui só ficaram a lente dos postes (que acende) e o cone
            _m = new Dictionary<string, Material>();
            string caminhoLampada = $"{PastaMateriais}/Lampada.mat";
            var lampada = AssetDatabase.LoadAssetAtPath<Material>(caminhoLampada);
            if (lampada == null) { lampada = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(lampada, caminhoLampada); }
            ColorUtility.TryParseHtmlString("#FFB46B", out var corDaLampada);
            lampada.SetColor("_BaseColor", corDaLampada);
            lampada.SetFloat("_Smoothness", 0.08f);
            lampada.enableInstancing = true;
            lampada.EnableKeyword("_EMISSION"); lampada.SetColor("_EmissionColor", corDaLampada * 2.5f);
            lampada.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lampada);
            _m["Lampada"] = lampada;
            // cone de luz dos postes: aditivo, dos dois lados, mais forte perto da lâmpada
            string caminhoCone = $"{PastaMateriais}/ConeDeLuz.mat";
            var cone = AssetDatabase.LoadAssetAtPath<Material>(caminhoCone);
            if (cone == null) { cone = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(cone, caminhoCone); }
            cone.SetFloat("_Surface", 1f); cone.SetFloat("_Blend", 2f); cone.SetFloat("_Cull", 0f);
            cone.SetOverrideTag("RenderType", "Transparent"); cone.renderQueue = (int)RenderQueue.Transparent;
            cone.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); cone.SetInt("_DstBlend", (int)BlendMode.One); cone.SetInt("_ZWrite", 0);
            cone.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cone.SetColor("_BaseColor", new Color(1f, 0.71f, 0.42f, 0.11f));
            cone.SetTexture("_BaseMap", Degrade());
            EditorUtility.SetDirty(cone);
            _m["ConeDeLuz"] = cone;
        }

        static Texture2D Degrade()
        {
            string caminho = $"{PastaMateriais}/DegradeCone.png";
            {
                var t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                {
                    float v = y / 63f;                       // 1 = ponta do cone (lâmpada)
                    float a = Mathf.Pow(v, 2.4f);
                    for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                File.WriteAllBytes(caminho, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(caminho);
                var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        static Mesh SalvarMalha(Mesh m, string nome)
        {
            string caminho = $"{PastaMalhas}/{nome}.asset";
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha != null) { EditorUtility.CopySerialized(m, velha); Object.DestroyImmediate(m); return velha; }
            AssetDatabase.CreateAsset(m, caminho);
            return m;
        }

        static void Malhas()
        {
            // cone aberto: ponta em y = 0, base de raio 1 em y = -1; v = 1 na ponta
            var c = new Mesh { name = "ConeDeLuz" };
            var cv = new List<Vector3>(); var cuv = new List<Vector2>(); var ct = new List<int>();
            const int lados = 20;
            for (int i = 0; i <= lados; i++)
            {
                float ang = i * Mathf.PI * 2f / lados;
                cv.Add(Vector3.zero); cuv.Add(new Vector2(i / (float)lados, 1f));
                cv.Add(new Vector3(Mathf.Cos(ang), -1f, Mathf.Sin(ang))); cuv.Add(new Vector2(i / (float)lados, 0f));
            }
            for (int i = 0; i < lados; i++) { int b = i * 2; ct.AddRange(new[] { b, b + 3, b + 1 }); }
            c.SetVertices(cv); c.SetUVs(0, cuv); c.SetTriangles(ct, 0); c.RecalculateNormals(); c.RecalculateBounds();
            _cone = SalvarMalha(c, "ConeDeLuz");
        }

        // ------------------------------------------------------------------ arte final (Parte 8)

        /// <summary>
        /// Os modelos do Blender (`Modelo 3D/Cidade` na pasta de arte): a importação do FBX, o material único "Cidade"
        /// e as malhas por nome. O material é a paleta, com a suavidade de cada cor guardada no alfa.
        /// </summary>
        static void ArteDaCidade()
        {
            var paleta = (TextureImporter)AssetImporter.GetAtPath(CaminhoPaleta);
            if (paleta.alphaIsTransparency || paleta.mipmapEnabled || paleta.filterMode != FilterMode.Point || paleta.textureCompression != TextureImporterCompression.Uncompressed)
            {
                // 16 × 16 com um quadradinho por cor: sem filtro, sem mipmap (misturaria as cores) e sem compressão
                paleta.alphaIsTransparency = false; paleta.mipmapEnabled = false; paleta.filterMode = FilterMode.Point;
                paleta.textureCompression = TextureImporterCompression.Uncompressed;
                paleta.SaveAndReimport();
            }
            _malhas = new Dictionary<string, Mesh>();
            foreach (var (caminho, colisao) in new[] { (FbxRua, false), (FbxCasas, false), (FbxCasasColisao, true), (FbxPredio, false), (FbxPredioColisao, true),
                                                       (FbxMoveis, false), (FbxBunker, false), (FbxBunkerColisao, true), (FbxChao, false) })
            {
                var fbx = (ModelImporter)AssetImporter.GetAtPath(caminho);
                // visual: UV de lightmap, para a luz assada. Colisão: legível (o MeshCollider cozinha a malha em Play)
                if (fbx.materialImportMode != ModelImporterMaterialImportMode.None || fbx.generateSecondaryUV == colisao || fbx.isReadable != colisao || fbx.importAnimation)
                {
                    fbx.materialImportMode = ModelImporterMaterialImportMode.None;
                    fbx.generateSecondaryUV = !colisao;
                    fbx.isReadable = colisao;
                    fbx.importAnimation = false; fbx.animationType = ModelImporterAnimationType.None;
                    fbx.importCameras = false; fbx.importLights = false;
                    fbx.SaveAndReimport();
                }
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Mesh>()) _malhas[m.name] = m;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(CaminhoMaterialCidade);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, CaminhoMaterialCidade); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CaminhoPaleta));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_SmoothnessTextureChannel", 1f);   // suavidade = alfa da paleta × _Smoothness
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            _m["Cidade"] = mat;
        }

        /// <summary>Um modelo dos FBX da cidade (pelo nome da malha), com o material da cidade. Sem colisor.</summary>
        static GameObject Modelo(Transform pai, string malha, Vector3 pos, float yaw = 0f, string nome = null)
        {
            if (!_malhas.TryGetValue(malha, out var m)) throw new System.Exception($"A malha {malha} não está nos FBX da cidade (Art/Cidade).");
            var go = new GameObject(nome ?? malha);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = _m["Cidade"];
            return go;
        }

        static BoxCollider Colisor(GameObject go, Vector3 centro, Vector3 tamanho)
        {
            var c = go.AddComponent<BoxCollider>(); c.center = centro; c.size = tamanho; return c;
        }

        /// <summary>Uma caixa do tamanho da malha (carros, barreiras, lixeiras: peças cheias).</summary>
        static BoxCollider ColisorDaMalha(GameObject go)
        {
            var b = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            return Colisor(go, b.center, b.size);
        }

        // ------------------------------------------------------------------ peças básicas

        static Transform Grupo(Transform pai, string nome)
        {
            var g = new GameObject(nome).transform; g.SetParent(pai, false); return g;
        }

        /// <summary>
        /// Caixa só de colisão (sem renderer): o chão, as calçadas, a praça e o estacionamento. O que se vê deles vem do
        /// Cidade_Chao.fbx, feito com as mesmas alturas (rua no 0, calçada em 0,12, grama em -0,02...).
        /// </summary>
        static GameObject Bloco(Transform pai, string nome, Vector3 centro, Vector3 tamanho)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = centro;
            go.AddComponent<BoxCollider>().size = tamanho;
            return go;
        }

        /// <summary>Laje retangular de colisão (x0..x1, z0..z1) com o topo em y e um buraco opcional.</summary>
        static void Laje(Transform pai, string nome, float x0, float x1, float z0, float z1, float topo, float espessura, Rect? buraco = null)
        {
            // o buraco encostado na borda deixaria uma faixa de largura zero: não é criada
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
            if (buraco == null)
            {
                Bloco(pai, nome, new Vector3((x0 + x1) / 2f, topo - espessura / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, espessura, z1 - z0));
                return;
            }
            var h = buraco.Value;
            Laje(pai, nome, x0, x1, z0, h.yMin, topo, espessura);
            Laje(pai, nome, x0, x1, h.yMax, z1, topo, espessura);
            Laje(pai, nome, x0, h.xMin, h.yMin, h.yMax, topo, espessura);
            Laje(pai, nome, h.xMax, x1, h.yMin, h.yMax, topo, espessura);
        }

        // ------------------------------------------------------------------ terreno, ruas e calçadas

        /// <summary>
        /// A colisão da grama (com o buraco da escada do bunker) e o visual do chão inteiro: os 25 pedaços "Chao_i_j" do
        /// Cidade_Chao.fbx (ruas, calçadas, grama, entulho, coreto, estacionamento), já nas coordenadas do mundo.
        /// </summary>
        static void Chao(Transform raiz)
        {
            Laje(raiz, "Chão", -110f, 110f, -110f, 110f, -0.02f, 0.1f, _buracoNoChao);
            var arte = Grupo(raiz, "Chão (arte)");
            foreach (var nome in _malhas.Keys.Where(n => n.StartsWith("Chao_")).OrderBy(n => n))
                Modelo(arte, nome, Vector3.zero);
        }

        static readonly float[] Linhas = { -40f, 0f, 40f };

        static void Ruas(Transform raiz)
        {
            var g = Grupo(raiz, "Ruas");
            const float meia = 4f, fim = 44f;
            // anel e cruzamento central
            foreach (float x in Linhas) Bloco(g, "Rua", new Vector3(x, -0.05f, 0f), new Vector3(meia * 2f, 0.1f, fim * 2f));
            foreach (float z in Linhas)
            {
                Bloco(g, "Rua", new Vector3(-22f, -0.049f, z), new Vector3(36f, 0.1f, meia * 2f));
                Bloco(g, "Rua", new Vector3(22f, -0.049f, z), new Vector3(36f, 0.1f, meia * 2f));
            }
            // saídas da cidade (bloqueadas mais à frente)
            Bloco(g, "Saída norte", new Vector3(0f, -0.05f, 54f), new Vector3(meia * 2f, 0.1f, 20f));
            Bloco(g, "Saída sul", new Vector3(0f, -0.05f, -54f), new Vector3(meia * 2f, 0.1f, 20f));
            Bloco(g, "Saída leste", new Vector3(54f, -0.05f, 0f), new Vector3(20f, 0.1f, meia * 2f));
            Bloco(g, "Saída oeste", new Vector3(-54f, -0.05f, 0f), new Vector3(20f, 0.1f, meia * 2f));
            // calçadas: um anel em volta de cada quarteirão (2,5 m de largura, 12 cm acima da rua)
            var calcadas = Grupo(raiz, "Calçadas");
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x0 = Linhas[i] + meia, x1 = Linhas[i + 1] - meia, z0 = Linhas[j] + meia, z1 = Linhas[j + 1] - meia;
                    AnelDeCalcada(calcadas, x0, x1, z0, z1);
                }
        }

        static void AnelDeCalcada(Transform pai, float x0, float x1, float z0, float z1)
        {
            const float w = 2.5f, y = 0.06f, h = 0.12f;
            Bloco(pai, "Calçada", new Vector3((x0 + x1) / 2f, y, z0 + w / 2f), new Vector3(x1 - x0, h, w));
            Bloco(pai, "Calçada", new Vector3((x0 + x1) / 2f, y, z1 - w / 2f), new Vector3(x1 - x0, h, w));
            Bloco(pai, "Calçada", new Vector3(x0 + w / 2f, y, (z0 + z1) / 2f), new Vector3(w, h, z1 - z0 - 2 * w));
            Bloco(pai, "Calçada", new Vector3(x1 - w / 2f, y, (z0 + z1) / 2f), new Vector3(w, h, z1 - z0 - 2 * w));
        }

        // ------------------------------------------------------------------ postes de luz

        static void Postes(Transform raiz)
        {
            var g = Grupo(raiz, "Postes");
            var lugares = new List<(Vector3 pos, float yaw)>();
            // ao longo das ruas do cruzamento (até as saídas) e do anel, alternando os lados
            int k = 0;
            for (float t = -58f; t <= 58f; t += 14f)
            {
                if (Mathf.Abs(t) < 7f || Mathf.Abs(Mathf.Abs(t) - 40f) < 7f) continue;
                float lado = (k++ % 2 == 0) ? 1f : -1f;
                lugares.Add((new Vector3(5.2f * lado, 0f, t), lado > 0 ? 270f : 90f));
                lugares.Add((new Vector3(t, 0f, -5.2f * lado), lado > 0 ? 0f : 180f));
            }
            foreach (float l in new[] { -40f, 40f })
                for (float t = -30f; t <= 30f; t += 20f)
                {
                    if (Mathf.Abs(t) < 7f) continue;
                    lugares.Add((new Vector3(l - 5.2f * Mathf.Sign(l), 0f, t), l > 0 ? 90f : 270f));
                    lugares.Add((new Vector3(t, 0f, l - 5.2f * Mathf.Sign(l)), l > 0 ? 180f : 0f));
                }
            for (int i = 0; i < lugares.Count; i++)
            {
                var (pos, yaw) = lugares[i];
                var poste = Grupo(g, $"Poste {i + 1}");
                poste.localPosition = pos; poste.localRotation = Quaternion.Euler(0f, yaw, 0f);
                // o modelo tem a haste, o braço e a cabeça; a lente (embaixo da cabeça, em z = 1,4) é à parte, porque
                // acende e apaga com o material da lâmpada
                var modelo = Modelo(poste, "Poste", Vector3.zero);
                Colisor(modelo, new Vector3(0f, 3.05f, 0f), new Vector3(0.24f, 6.1f, 0.24f));
                var lampada = Modelo(poste, "Poste_Lente", Vector3.zero, 0f, "Lâmpada");
                var rl = lampada.GetComponent<MeshRenderer>(); rl.sharedMaterial = _m["Lampada"]; rl.shadowCastingMode = ShadowCastingMode.Off;
                var luzGo = new GameObject("Luz"); luzGo.transform.SetParent(poste, false);
                luzGo.transform.localPosition = new Vector3(0f, 5.8f, 1.4f); luzGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Spot; ColorUtility.TryParseHtmlString("#FFB46B", out var cor); luz.color = cor;
                luz.intensity = 95f; luz.range = 16f; luz.spotAngle = 105f; luz.innerSpotAngle = 35f; luz.shadows = LightShadows.None;
                var cone = new GameObject("Cone de luz"); cone.transform.SetParent(poste, false);
                cone.transform.localPosition = new Vector3(0f, 5.82f, 1.4f); cone.transform.localScale = new Vector3(3.2f, 5.82f, 3.2f);
                cone.AddComponent<MeshFilter>().sharedMesh = _cone;
                var r = cone.AddComponent<MeshRenderer>(); r.sharedMaterial = _m["ConeDeLuz"]; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var p = poste.gameObject.AddComponent<PosteDeLuz>();
                p.luz = luz; p.cone = r; p.lampada = lampada.GetComponent<MeshRenderer>();
                p.pisca = i % 7 == 3;                                  // alguns postes falham
                p.quebrado = i % 11 == 5;                              // e alguns nem acendem
            }
        }

        // ------------------------------------------------------------------ casas

        /// <summary>
        /// Casa térrea com sala na frente e dois cômodos atrás (cozinha à esquerda, quarto à direita). A frente fica
        /// para -Z local (gire com yaw). Paredes, piso, forro, telhado, varanda e acabamento vêm do modelo do Blender
        /// (`modelo`, no Cidade_Casas.fbx), feito com a mesma planta; a colisão é a malha `modelo_Colisao` (só as partes
        /// sólidas). Os móveis ainda são blocos.
        /// </summary>
        static void Casa(Transform g, string modelo, Vector3 centro, float yaw, float W, float D, bool bunker)
        {
            g.localPosition = centro; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float piso = 0.15f;
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f;
            float zDiv = z0 + D * 0.55f;
            // buraco no piso para a escada do bunker: do meio do cômodo de trás (à esquerda) até a borda da laje (o
            // modelo já vem com ele; aqui ele serve para abrir o mesmo buraco no chão de grama)
            Rect? buraco = bunker ? new Rect(x0 + 0.4f, EscadaDoBunkerInicio(zDiv), 1.2f, z1 + 0.2f - EscadaDoBunkerInicio(zDiv)) : (Rect?)null;
            Modelo(g, modelo, Vector3.zero, 0f, "Casa");
            var colisor = new GameObject("Colisão");
            colisor.transform.SetParent(g, false);
            colisor.AddComponent<MeshCollider>().sharedMesh = _malhas[modelo + "_Colisao"];
            var interior = new GameObject("Interior").AddComponent<AreaInterna>();
            interior.transform.SetParent(g, false);
            interior.transform.localPosition = new Vector3(0f, 1.55f, 0f);     // do piso (0,15) ao forro (2,95)
            interior.tamanho = new Vector3(W, 2.9f, D);
            MoveisDaCasa(Grupo(g, "Móveis"), W, D, bunker);
            var eco = new GameObject("Eco da casa").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            eco.reverbPreset = AudioReverbPreset.Room; eco.minDistance = Mathf.Min(W, D) * 0.4f; eco.maxDistance = Mathf.Max(W, D) * 0.6f;
            if (bunker)
            {
                Bunker(g, x0, zDiv, z1, piso);
                // o chão de grama passa por baixo da casa: abre nele o mesmo buraco da escada
                var b = buraco.Value;
                var cantos = new[] { new Vector3(b.xMin, 0f, b.yMin), new Vector3(b.xMax, 0f, b.yMin), new Vector3(b.xMin, 0f, b.yMax), new Vector3(b.xMax, 0f, b.yMax) }.Select(g.TransformPoint).ToArray();
                float mx = cantos.Min(c => c.x), Mx = cantos.Max(c => c.x), mz = cantos.Min(c => c.z), Mz = cantos.Max(c => c.z);
                _buracoNoChao = new Rect(mx, mz, Mx - mx, Mz - mz);
            }
        }

        /// <summary>
        /// O bunker da família, embaixo do quintal: a escada começa no meio do cômodo de trás, desce para os fundos
        /// (passando por baixo da parede) e chega à porta do bunker, com sala comum, quarto e despensa.
        /// Escada, poço, grades, salas, porta blindada, canos e luminárias vêm do modelo "Bunker" do Blender
        /// (`cidade_bunker.py`, mesma planta, nas coordenadas da casa); a colisão é `Bunker_Colisao`. Aqui ficam os
        /// móveis, a luz e o eco.
        /// </summary>
        static void Bunker(Transform casa, float x0, float zDiv, float z1, float piso)
        {
            var g = Grupo(casa, "Bunker");
            const float fundo = -3.2f, teto = -0.55f;
            float xEsc = x0 + 1.0f, zTopo = EscadaDoBunkerInicio(zDiv);
            float zPorta = zTopo + 0.26f * 19 + 1.2f;                          // pé da escada e o patamar antes da porta
            float bx0 = xEsc - 0.7f, bx1 = bx0 + 9.5f, bz0 = zPorta, bz1 = zPorta + 7.5f;
            float xDiv = bx0 + 5.5f, zMeio = (bz0 + bz1) / 2f;
            Modelo(g, "Bunker", Vector3.zero, 0f);
            var colisor = new GameObject("Colisão");
            colisor.transform.SetParent(g, false);
            colisor.AddComponent<MeshCollider>().sharedMesh = _malhas["Bunker_Colisao"];
            // sala comum: mesa com rádio e quatro cadeiras; tambor e caixas no canto
            var m = Grupo(g, "Móveis");
            float mx = bx0 + 2.8f, mz = zMeio + 0.5f;
            Movel(m, "Mesa_Grande", mx, fundo, mz, 0f);
            foreach (float dx in new[] { -0.5f, 0.5f })
            {
                Movel(m, "Cadeira", mx + dx, fundo, mz - 0.75f, 0f);
                Movel(m, "Cadeira", mx + dx, fundo, mz + 0.75f, 180f);
            }
            Movel(m, "Radio", mx - 0.3f, fundo + 0.76f, mz - 0.1f, 15f, false);
            Movel(m, "Caixas", bx0 + 0.55f, fundo, bz1 - 0.6f, 10f);
            Movel(m, "Tambor", bx0 + 0.45f, fundo, bz1 - 1.5f, 0f);
            // o suporte de armas na parede da esquerda, logo depois da porta (as costas na parede, de 20 cm, e na faixa
            // verde pintada nela); as armas, a montagem das armas põe em cima ("Parede de armas")
            Movel(m, "Suporte_Armas", bx0 + 0.12f, fundo, bz0 + 1.8f, 90f);
            // quarto: dois beliches (a família tem quatro)
            Movel(m, "Beliche", bx1 - 0.6f, fundo, bz0 + 1.4f, 0f);
            Movel(m, "Beliche", bx1 - 2.0f, fundo, bz0 + 1.4f, 0f);
            // despensa: prateleiras cheias na parede da direita e no fundo, tambores de água perto da porta
            Movel(m, "Prateleira_Cheia", bx1 - 0.37f, fundo, bz1 - 0.95f, 270f);
            Movel(m, "Prateleira_Cheia", bx1 - 0.37f, fundo, bz1 - 2.35f, 270f);
            Movel(m, "Prateleira_Cheia", xDiv + 1.6f, fundo, bz1 - 0.37f, 180f);
            Movel(m, "Tambor", xDiv + 0.55f, fundo, bz1 - 0.5f, 0f);
            Movel(m, "Tambor", xDiv + 0.6f, fundo, bz1 - 1.2f, 30f);
            var lampada = new GameObject("Lâmpada do bunker").AddComponent<Light>();
            // a luz do dia não chega aqui (AmbienteDoDia apaga o ambiente no subsolo): a lâmpada ilumina a sala sozinha
            lampada.transform.SetParent(g, false); lampada.transform.localPosition = new Vector3(bx0 + 2.7f, teto - 0.45f, zMeio);
            lampada.type = LightType.Point; lampada.color = new Color(1f, 0.85f, 0.6f); lampada.intensity = 5f; lampada.range = 9f;
            var eco = new GameObject("Eco do bunker").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3((bx0 + bx1) / 2f, fundo + 1.3f, zMeio);
            eco.reverbPreset = AudioReverbPreset.StoneCorridor; eco.minDistance = 3f; eco.maxDistance = 7f;
        }

        static float EscadaDoBunkerInicio(float zDiv) => zDiv + 0.7f;

        // ------------------------------------------------------------------ prédio de 4 andares

        /// <summary>
        /// Prédio de 4 andares, 22 × 14 m: corredor central da frente até a escada em U no fundo, um apartamento de cada
        /// lado (sala na frente, quarto atrás) e cobertura com platibanda. Tudo (paredes, lajes, escada, fachada, escada
        /// de incêndio, caixa d'água) vem do modelo "Predio" do Blender (`cidade_predio.py`, mesma planta); a colisão é a
        /// malha `Predio_Colisao`. Os móveis de cada apartamento ainda são blocos.
        /// </summary>
        static void Predio(Transform g, Vector3 centro, float yaw)
        {
            g.localPosition = centro; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float W = 22f, D = 14f, H = 3.0f; const int andares = 4;
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f;
            Modelo(g, "Predio", Vector3.zero, 0f, "Prédio");
            var colisor = new GameObject("Colisão");
            colisor.transform.SetParent(g, false);
            colisor.AddComponent<MeshCollider>().sharedMesh = _malhas["Predio_Colisao"];
            var interior = new GameObject("Interior").AddComponent<AreaInterna>();
            interior.transform.SetParent(g, false);
            interior.transform.localPosition = new Vector3(0f, 0.15f + andares * H / 2f, 0f);
            interior.tamanho = new Vector3(W, andares * H, D);
            for (int a = 0; a < andares; a++)
            {
                float y = 0.15f + a * H + 0.005f;          // em cima do revestimento do piso
                var andar = Grupo(g, a == 0 ? "Térreo" : $"Andar {a}");
                foreach (float lado in new[] { -1f, 1f })
                    MoveisDoApartamento(Grupo(andar, lado < 0 ? "Apartamento oeste" : "Apartamento leste"), y, lado, x0, x1, z0, z1);
            }
            var eco = new GameObject("Eco do prédio").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(0f, 6f, 0f);
            eco.reverbPreset = AudioReverbPreset.Hallway; eco.minDistance = 6f; eco.maxDistance = 12f;
        }

        // ------------------------------------------------------------------ móveis

        /// <summary>
        /// Um móvel do Cidade_Moveis.fbx no chão (y do piso), virado por yaw (a frente do modelo olha para +Z), com uma
        /// caixa de colisão do tamanho da malha. O nome do objeto é o da malha: a Parte 7 vai achar os de saque por ele.
        /// </summary>
        static GameObject Movel(Transform pai, string malha, float x, float y, float z, float yaw, bool colisor = true)
        {
            var go = Modelo(pai, malha, new Vector3(x, y, z), yaw);
            if (colisor) ColisorDaMalha(go);
            return go;
        }

        /// <summary>
        /// Os móveis de uma casa (coordenadas locais; a planta é a da Casa). Os lugares fogem das portas: o sofá acaba
        /// antes da porta da frente (se não cabe, vira poltrona), o rack fica antes da porta da cozinha, e na casa da
        /// família nada fica na frente da boca da escada do bunker (o balcão vai para a parede da sala).
        /// </summary>
        static void MoveisDaCasa(Transform g, float W, float D, bool bunker)
        {
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f, zDiv = z0 + D * 0.55f;
            const float y = 0.155f;
            string tecido = _rng.NextDouble() < 0.5 ? "Verde" : "Marrom";
            // sala
            float fimDoSofa = x0 + 0.3f * W - Porta / 2f - 0.15f;
            bool cabe = fimDoSofa - 2.1f > x0 + 0.15f;
            float xSofa = cabe ? fimDoSofa - 1.05f : fimDoSofa - 0.48f;
            Movel(g, "Tapete", xSofa, y, z0 + 1.9f, 0f, false);
            Movel(g, cabe ? "Sofa_" + tecido : "Poltrona_Vinho", xSofa, y, z0 + 0.55f, 0f);
            if (cabe) Movel(g, "Poltrona_" + (tecido == "Verde" ? "Vinho" : "Azul"), x0 + 0.62f, y, z0 + 2.4f, 90f);
            float portaDaCozinha = x0 + 0.1f + 0.25f * W - Porta / 2f;
            Movel(g, "Rack_TV", Mathf.Min(xSofa, portaDaCozinha - 0.8f), y, zDiv - 0.38f, 180f);
            float mx = x1 - 1.6f, mz = z0 + 2.2f;
            Movel(g, "Mesa", mx, y, mz, 0f);
            Movel(g, "Cadeira", mx - 0.35f, y, mz - 0.68f, 0f);
            Movel(g, "Cadeira", mx + 0.35f, y, mz + 0.68f, 180f);
            // cozinha: geladeira e fogão na divisória do quarto; o balcão sob a janela da esquerda (ou, na casa da
            // família, na parede da sala, depois da porta)
            Movel(g, "Geladeira", -0.47f, y, z1 - 0.55f, 270f);
            Movel(g, "Fogao", -0.44f, y, z1 - 1.45f, 270f);
            if (bunker) Movel(g, "Balcao", -1.25f, y, zDiv + 0.43f, 0f);
            else Movel(g, "Balcao", x0 + 0.43f, y, z1 - 0.3f * D, 90f);
            // quarto: cama com a cabeceira na parede do fundo, criado-mudo, armário e cômoda na divisória
            Movel(g, "Cama", x1 - 1.4f, y, z1 - 1.15f, 180f);
            Movel(g, "Criado_Mudo", x1 - 2.6f, y, z1 - 0.35f, 180f);
            Movel(g, _rng.NextDouble() < 0.5 ? "Armario" : "Armario_Aberto", 0.41f, y, zDiv + 0.7f, 90f);
            Movel(g, "Comoda", 0.36f, y, z1 - 1.0f, 90f);
        }

        /// <summary>
        /// Os móveis de um apartamento do prédio (lado -1 = oeste, +1 = leste). Sala: sofá contra a fachada, TV, mesa
        /// com cadeiras e a cozinha americana na parede do quarto. Quarto: cama no fundo, criado-mudo, armário e cômoda.
        /// </summary>
        static void MoveisDoApartamento(Transform g, float y, float lado, float x0, float x1, float z0, float z1)
        {
            float xm = (1.6f * lado + (lado < 0 ? x0 + 0.1f : x1 - 0.1f)) / 2f;
            string tecido = _rng.NextDouble() < 0.5 ? "Verde" : "Marrom";
            Movel(g, "Tapete", xm, y, -4.9f, 0f, false);
            Movel(g, "Sofa_" + tecido, xm, y, z0 + 0.55f, 0f);
            Movel(g, "Rack_TV", xm, y, -3.0f, 180f);
            float mx = xm - 1.5f * lado;
            Movel(g, "Mesa", mx, y, -2.0f, 0f);
            Movel(g, "Cadeira", mx - 0.35f, y, -2.68f, 0f);
            Movel(g, "Cadeira", mx + 0.35f, y, -1.32f, 180f);
            Movel(g, "Geladeira", 2.2f * lado, y, 0.04f, 180f);
            Movel(g, "Fogao", 3.05f * lado, y, 0.07f, 180f);
            Movel(g, "Balcao", 4.3f * lado, y, 0.08f, 180f);
            Movel(g, "Cama", xm + 1.2f * lado, y, z1 - 1.15f, 180f);
            Movel(g, "Criado_Mudo", xm, y, z1 - 0.35f, 180f);
            Movel(g, _rng.NextDouble() < 0.5 ? "Armario" : "Armario_Aberto", 2.5f * lado, y, 0.93f, 0f);
            Movel(g, "Comoda", 4.0f * lado, y, z1 - 0.35f, 180f);
        }

        // ------------------------------------------------------------------ resto da cidade

        static void Praca(Transform g, Vector3 centro)
        {
            g.localPosition = centro;
            // o piso e o coreto que se veem estão no chão de arte (o coreto octogonal cabe nesta caixa de 4 × 4 × 0,6)
            Bloco(g, "Piso da praça", new Vector3(0f, 0.04f, 0f), new Vector3(10f, 0.08f, 10f));
            Bloco(g, "Coreto", new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.6f, 4f));
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f;
                Vector3 p = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, 4f);
                // a frente do banco olha para o coreto; o piso da praça tem 8 cm
                ColisorDaMalha(Modelo(g, "Banco", p + Vector3.up * 0.08f, ang + 180f));
            }
            Lixeira(g, new Vector3(4.3f, 0.08f, 2.6f), 0f);
            Lixeira(g, new Vector3(-2.6f, 0.08f, -4.4f), 30f, true);
            Arvore(g, new Vector3(-3.5f, 0f, 3.5f)); Arvore(g, new Vector3(3.5f, 0f, -3.5f));
        }

        static void Estacionamento(Transform g, Vector3 centro)
        {
            g.localPosition = centro;
            Bloco(g, "Pátio", new Vector3(0f, -0.04f, 0f), new Vector3(10f, 0.1f, 4.5f));
            Carro(g, new Vector3(-2.7f, 0f, 0f), 90f, "Carro_Sedan_Cinza");
            Carro(g, new Vector3(2.7f, 0f, 0.3f), 95f, "Carro_Sedan_Azul");
        }

        /// <summary>Carro abandonado: um dos modelos (Carro_Sedan_Azul, Carro_Pickup_Bege...), com uma caixa de colisão.</summary>
        static void Carro(Transform pai, Vector3 pos, float yaw, string modelo)
        {
            ColisorDaMalha(Modelo(pai, modelo, pos, yaw, "Carro"));
        }

        static void Carros(Transform g)
        {
            Carro(g, new Vector3(2.2f, 0f, 18f), 8f, "Carro_Sedan_Vinho");
            Carro(g, new Vector3(-1.5f, 0f, -24f), 175f, "Carro_Pickup_Bege");
            Carro(g, new Vector3(-26f, 0f, 1.8f), 80f, "Carro_Sedan_Azul");
            Carro(g, new Vector3(31f, 0f, -2.5f), 260f, "Carro_Sedan_Cinza");
            Carro(g, new Vector3(38.5f, 0f, 22f), 20f, "Carro_Pickup_Azul");
            Carro(g, new Vector3(-41.5f, 0f, -18f), 190f, "Carro_Sedan_Verde");
            Carro(g, new Vector3(14f, 0f, 41.5f), 275f, "Carro_Pickup_Branco");
            Carro(g, new Vector3(-12f, 0f, -38.5f), 95f, "Carro_Sedan_Vinho");
        }

        /// <summary>
        /// Linha de cerca de madeira de a até b: trechos de ~2 m (esticados para fechar a conta; alguns quebrados), o
        /// mourão da ponta e uma caixa de colisão só, fina, na linha toda.
        /// </summary>
        static void Cerca(Transform pai, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a; float n = d.magnitude; Vector2 u = d / n;
            float yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;   // +X local = de a para b
            var linha = Grupo(pai, "Cerca");
            int trechos = Mathf.Max(1, Mathf.RoundToInt(n / 2f));
            float l = n / trechos;
            for (int i = 0; i < trechos; i++)
            {
                Vector2 c = a + u * (l * (i + 0.5f));
                var t = Modelo(linha, _rng.NextDouble() < 0.15 ? "Cerca_Quebrada" : "Cerca", new Vector3(c.x, 0f, c.y), yaw);
                t.transform.localScale = new Vector3(l / 2f, 1f, 1f);
            }
            // cada trecho tem o mourão só no lado +X (o de b): falta o da ponta a
            Modelo(linha, "Cerca_Mourao", new Vector3(a.x, 0f, a.y), yaw);
            var m = (a + b) / 2f;
            var colisor = new GameObject("Colisor");
            colisor.transform.SetParent(linha, false);
            colisor.transform.localPosition = new Vector3(m.x, 0f, m.y);
            colisor.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Colisor(colisor, new Vector3(0f, 0.55f, 0f), new Vector3(n + 0.1f, 1.1f, 0.12f));
        }

        static void Cercas(Transform g)
        {
            // quintal da casa da família (com portão aberto para a rua) e das casas do noroeste
            Cerca(g, new Vector2(-33f, -31f), new Vector2(-7f, -31f));
            Cerca(g, new Vector2(-33f, -31f), new Vector2(-33f, -7f));
            Cerca(g, new Vector2(-33f, -7f), new Vector2(-23f, -7f));
            Cerca(g, new Vector2(-17f, -7f), new Vector2(-7f, -7f));
            Cerca(g, new Vector2(-33f, 31f), new Vector2(-6f, 31f));
            Cerca(g, new Vector2(-19.5f, 12f), new Vector2(-19.5f, 31f));
            Cerca(g, new Vector2(7f, -31f), new Vector2(20f, -31f));
        }

        /// <summary>Árvore de rua: com copa rala ou seca (sem folhas), girada ao acaso. Colide só o tronco.</summary>
        static void Arvore(Transform pai, Vector3 pos)
        {
            string malha = _rng.NextDouble() < 0.4 ? "Arvore_Seca" : "Arvore";
            var go = Modelo(pai, malha, pos, (float)_rng.NextDouble() * 360f, "Árvore");
            var tronco = go.AddComponent<CapsuleCollider>();
            tronco.center = new Vector3(0f, 1.5f, 0f); tronco.radius = 0.22f; tronco.height = 3f;
        }

        static void Arvores(Transform g)
        {
            foreach (var p in new[] { new Vector3(-34f, 0f, 10f), new Vector3(-8f, 0f, 9f), new Vector3(-32f, 0f, -33f), new Vector3(-9f, 0f, -33f),
                                      new Vector3(33f, 0f, 9f), new Vector3(7.5f, 0f, 9f), new Vector3(33f, 0f, -9f), new Vector3(8f, 0f, -33f), new Vector3(-24f, 0f, 33f) })
                Arvore(g, p);
        }

        static void Barricadas(Transform g)
        {
            // +Z local aponta para fora da cidade: as barreiras de concreto, o cavalete e os cones do lado de dentro,
            // e os carros que fecharam a estrada do lado de fora
            foreach (var (pos, yaw) in new[] { (new Vector3(0f, 0f, 60f), 0f), (new Vector3(0f, 0f, -60f), 180f), (new Vector3(60f, 0f, 0f), 90f), (new Vector3(-60f, 0f, 0f), 270f) })
            {
                var b = Grupo(g, "Barricada"); b.localPosition = pos; b.localRotation = Quaternion.Euler(0f, yaw, 0f);
                for (int i = -2; i <= 2; i++) ColisorDaMalha(Modelo(b, "Barreira", new Vector3(i * 1.9f, 0f, (i % 2) * 0.15f), (i % 2) * 6f));
                ColisorDaMalha(Modelo(b, "Cavalete", new Vector3(0.8f, 0f, -2.3f), 8f));
                foreach (var (x, z) in new[] { (-3.0f, -1.6f), (-1.2f, -2.8f), (3.1f, -1.9f) })
                    ColisorDaMalha(Modelo(b, "Cone", new Vector3(x, 0f, z), (float)_rng.NextDouble() * 90f));
                Carro(b, new Vector3(-2.2f, 0f, 3.5f), 70f, "Carro_Sedan_Vinho");
                Carro(b, new Vector3(2.8f, 0f, 4.2f), 115f, "Carro_Pickup_Branco");
            }
        }

        /// <summary>Lixeira de metal, em pé ou caída de lado (a tampa para a frente).</summary>
        static void Lixeira(Transform pai, Vector3 pos, float yaw, bool caida = false)
        {
            var go = Modelo(pai, "Lixeira", pos, yaw);
            if (caida)
            {
                // deita girando 90° em X (o fundo fica na origem): sobe o raio para encostar no chão
                go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                go.transform.localPosition = pos + Vector3.up * 0.3f;
            }
            ColisorDaMalha(go);
        }

        /// <summary>
        /// O que fica nas calçadas: placas de pare nos cruzamentos (a frente olha para quem chega), placas com o nome
        /// das ruas, hidrantes virados para a rua, lixeiras (algumas caídas), caçambas e sacos de lixo.
        /// Os lugares evitam os postes (nas calçadas, a 5,2 m do meio da rua) e as portas das casas.
        /// </summary>
        static void ObjetosDeRua(Transform g)
        {
            foreach (var (x, z, yaw) in new[] { (5.5f, -5.5f, 180f), (-5.5f, 5.5f, 0f), (-5.5f, -5.5f, 270f), (5.5f, 5.5f, 90f),
                                                 (5.5f, 34.5f, 180f), (-5.5f, -34.5f, 0f), (34.5f, -5.5f, 270f), (-34.5f, 5.5f, 90f) })
                Colisor(Modelo(g, "Placa_Pare", new Vector3(x, 0.12f, z), yaw), new Vector3(0f, 1.1f, -0.02f), new Vector3(0.08f, 2.2f, 0.08f));
            foreach (var (x, z, yaw) in new[] { (-5.5f, 34.5f, 0f), (5.5f, -34.5f, 180f), (34.5f, 5.5f, 90f), (-34.5f, -5.5f, 270f) })
                Colisor(Modelo(g, "Placa_Rua", new Vector3(x, 0.12f, z), yaw), new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 2.6f, 0.08f));
            foreach (var (x, z, yaw) in new[] { (4.8f, -23f, 270f), (-4.8f, 31f, 90f), (23f, -4.8f, 0f), (-27f, 4.8f, 180f), (34.6f, -20f, 90f), (-20f, -34.6f, 180f) })
                ColisorDaMalha(Modelo(g, "Hidrante", new Vector3(x, 0.12f, z), yaw));
            foreach (var (x, z) in new[] { (5.9f, -9.5f), (-5.9f, 18.5f), (14.5f, 5.9f), (-15.5f, -5.9f), (30.5f, -5.9f), (-31f, 5.9f), (34.6f, 21.5f), (-34.6f, -21.5f) })
                Lixeira(g, new Vector3(x, 0.12f, z), (float)_rng.NextDouble() * 360f);
            Lixeira(g, new Vector3(16.5f, 0.12f, 5.4f), 75f, true);
            Lixeira(g, new Vector3(-34.9f, 0.12f, 14f), 200f, true);
            // caçambas: ao lado do prédio (entre a parede leste e a calçada) e atrás da casa 2, fora da cerca
            ColisorDaMalha(Modelo(g, "Cacamba", new Vector3(32.6f, 0f, 18.5f), 90f));
            ColisorDaMalha(Modelo(g, "Cacamba", new Vector3(-9f, 0f, 32.3f), 0f));
            // sacos: y = 0,12 em cima da calçada, 0 na grama
            foreach (var (pos, yaw) in new[] { (new Vector3(32.5f, 0f, 16.3f), 20f), (new Vector3(-6.9f, 0f, 32.4f), 110f), (new Vector3(6.2f, 0.12f, -10.6f), 300f),
                                               (new Vector3(-31.2f, 0f, 7.4f), 45f), (new Vector3(29.4f, 0.12f, -5.5f), 160f) })
                ColisorDaMalha(Modelo(g, "Sacos", pos, yaw));
        }

        static readonly string[] TiposDePinheiro = { "Pinheiro_A", "Pinheiro_B", "Pinheiro_C" };

        static void Pinheiros(Transform g)
        {
            int n = 0;
            for (int i = 0; i < 2200 && n < 320; i++)
            {
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float r = 64f + (float)_rng.NextDouble() * 34f;
                var pos = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (Mathf.Abs(pos.x) > 100f || Mathf.Abs(pos.z) > 100f) continue;
                var go = Modelo(g, TiposDePinheiro[_rng.Next(TiposDePinheiro.Length)], pos, (float)_rng.NextDouble() * 360f, "Pinheiro");
                go.transform.localScale = Vector3.one * (0.9f + (float)_rng.NextDouble() * 0.7f);
                n++;
            }
        }

        static void Barreira(Transform g)
        {
            const int lados = 48; const float r = 63f;
            for (int i = 0; i < lados; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 2f / lados;
                var go = new GameObject("Parede invisível");
                go.transform.SetParent(g, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, 3f, Mathf.Sin(ang) * r);
                go.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(2f * Mathf.PI * r / lados + 0.6f, 6f, 1f);
            }
        }

        /// <summary>
        /// Os 4 pontos de nascimento vão para a sala comum do bunker (nas coordenadas da casa da família, como o
        /// <see cref="Bunker"/>): dois entre a porta blindada e a mesa, dois dos lados da mesa, todos olhando para ela.
        /// </summary>
        internal static void PontosNoBunker(UnityEngine.SceneManagement.Scene cena)
        {
            var pontos = cena.GetRootGameObjects().FirstOrDefault(r => r.name == "Pontos de nascimento");
            var bunker = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Bunker" && t.parent != null && t.parent.name == "Casa da família");
            if (pontos == null || bunker == null) return;
            const float fundo = -3.2f;
            var mesa = new Vector3(-2.9f, fundo, 11.6f);
            var lugares = new[] { new Vector3(-3.4f, fundo, 8.7f), new Vector3(-1.5f, fundo, 8.7f), new Vector3(-4.8f, fundo, 10.2f), new Vector3(-1.1f, fundo, 10.6f) };
            for (int i = 0; i < pontos.transform.childCount && i < lugares.Length; i++)
            {
                var p = pontos.transform.GetChild(i);
                p.position = bunker.TransformPoint(lugares[i] + Vector3.up * 0.05f);
                var olhar = bunker.TransformDirection(mesa - lugares[i]);
                olhar.y = 0f;
                p.rotation = Quaternion.LookRotation(olhar);
            }
        }
    }
}
