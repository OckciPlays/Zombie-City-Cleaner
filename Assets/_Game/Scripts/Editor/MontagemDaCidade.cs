using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;
using P = ZombieCityCleaner.EditorTools.PlantaDoBunker;

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
            _rngSaque = new System.Random(2077);   // à parte, para não mudar o sorteio dos móveis
            Materiais();
            Malhas();
            ArteDaCidade();

            var cena = EditorSceneManager.GetActiveScene();
            foreach (var nome in new[] { "Cidade", "Teste (temporário)", "Bunker (rede)" })
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
            Abandono(Grupo(raiz, "Abandono"));
            MarcasNoChao(Grupo(raiz, "Marcas no chão"));
            SinaisDeVida(raiz);
            Pinheiros(Grupo(raiz, "Pinheiros"));
            Barreira(Grupo(raiz, "Barreira invisível"));
            Chao(raiz);   // por último: precisa do buraco da escada do bunker, que a casa da família marca

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null || t.GetComponent<PosteDeLuz>() != null || t.name == "Cone de luz") continue;
                if (t.GetComponentInParent<SinalDeVida>() != null) continue;   // as lanternas do pisca-alerta mudam em Play
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                if (EscondeOQueEstaAtras(t)) flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
            MontarRedeDoBunker(raiz.Find("Casa da família"));
            PontosNoBunker(cena);
            MontagemDasArmas.MontarItensNoMundo(cena);   // a comida e a água, nos lugares marcados
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
            // a lâmpada acesa do bunker: Unlit com a cor em HDR (brilha no bloom); apagada, ela usa o material da cidade
            string caminhoLampadaBunker = $"{PastaMateriais}/LampadaBunker.mat";
            var lampadaBunker = AssetDatabase.LoadAssetAtPath<Material>(caminhoLampadaBunker);
            if (lampadaBunker == null) { lampadaBunker = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(lampadaBunker, caminhoLampadaBunker); }
            lampadaBunker.SetColor("_BaseColor", new Color(1f, 0.82f, 0.55f) * 3.5f);
            lampadaBunker.enableInstancing = true;
            EditorUtility.SetDirty(lampadaBunker);
            _m["LampadaBunker"] = lampadaBunker;
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
            MarcarPisos(g, modelo, W, D);
            var eco = new GameObject("Eco da casa").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            eco.reverbPreset = AudioReverbPreset.Room; eco.minDistance = Mathf.Min(W, D) * 0.4f; eco.maxDistance = Mathf.Max(W, D) * 0.6f;
            if (bunker)
            {
                Bunker(g);
                // o chão de grama passa por baixo da casa: abre nele o mesmo buraco da escada
                var b = buraco.Value;
                var cantos = new[] { new Vector3(b.xMin, 0f, b.yMin), new Vector3(b.xMax, 0f, b.yMin), new Vector3(b.xMin, 0f, b.yMax), new Vector3(b.xMax, 0f, b.yMax) }.Select(g.TransformPoint).ToArray();
                float mx = cantos.Min(c => c.x), Mx = cantos.Max(c => c.x), mz = cantos.Min(c => c.z), Mz = cantos.Max(c => c.z);
                _buracoNoChao = new Rect(mx, mz, Mx - mx, Mz - mz);
            }
        }

        /// <summary>
        /// O piso de cada cômodo das casas (os `pisos` do CASAS do cidade_casas.py: o taco é madeira, o "ConcretoClaro" da
        /// cozinha é concreto) e o de fora (o deck da varanda, ou o degrau de concreto). Ao mudar lá, mude aqui.
        /// </summary>
        static readonly Dictionary<string, (Superficie sala, Superficie cozinha, Superficie quarto, bool varanda)> PisosDasCasas =
            new Dictionary<string, (Superficie, Superficie, Superficie, bool)>
            {
                ["Casa_1"] = (Superficie.Madeira, Superficie.Concreto, Superficie.Carpete, true),
                ["Casa_2"] = (Superficie.Carpete, Superficie.Concreto, Superficie.Madeira, false),
                ["Casa_Familia"] = (Superficie.Madeira, Superficie.Concreto, Superficie.Madeira, true),
                ["Casa_4"] = (Superficie.Madeira, Superficie.Concreto, Superficie.Carpete, false),
                ["Casa_5"] = (Superficie.Carpete, Superficie.Concreto, Superficie.Madeira, true),
            };

        /// <summary>
        /// O chão de cada cômodo para o som dos passos (<see cref="PisoPorZona"/>, no grupo da casa): a sala na frente, a
        /// cozinha atrás à esquerda e o quarto atrás à direita (a planta da Casa), do porão ao meio da parede.
        /// </summary>
        static void MarcarPisos(Transform casa, string modelo, float W, float D)
        {
            if (!PisosDasCasas.TryGetValue(modelo, out var p)) return;
            float x0 = -W / 2f, x1 = W / 2f, z0 = -D / 2f, z1 = D / 2f, zDiv = z0 + D * 0.55f;
            Bounds Caixa(float xa, float xb, float za, float zb) =>
                new Bounds(new Vector3((xa + xb) / 2f, 0.4f, (za + zb) / 2f), new Vector3(xb - xa, 1.6f, zb - za));
            var piso = casa.gameObject.AddComponent<PisoPorZona>();
            piso.zonas = new[] { Caixa(x0, x1, z0, zDiv), Caixa(x0, 0f, zDiv, z1), Caixa(0f, x1, zDiv, z1) };
            piso.superficies = new[] { p.sala, p.cozinha, p.quarto };
            piso.resto = p.varanda ? Superficie.Madeira : Superficie.Concreto;
        }

        /// <summary>
        /// O bunker da família, embaixo do quintal (a planta é a <see cref="PlantaDoBunker"/>): a escada começa no meio
        /// da cozinha e desce para os fundos em dois lances até o vestíbulo e a porta blindada; depois dela, o salão alto,
        /// com dois quartos de cada lado e o depósito, no fim de corredores curtos.
        /// Escada, poço, grades, salas, canos e luminárias vêm do modelo "Bunker" do Blender (`cidade_bunker.py`, mesma
        /// planta, nas coordenadas da casa); a colisão é `Bunker_Colisao`. Aqui ficam os móveis e o eco. A porta, as luzes
        /// e os interruptores mudam em Play: ficam fora da cidade, no "Bunker (rede)" (<see cref="MontarRedeDoBunker"/>).
        /// </summary>
        static void Bunker(Transform casa)
        {
            var g = Grupo(casa, "Bunker");
            Modelo(g, "Bunker", Vector3.zero, 0f);
            // o som dos passos: os 4 quartos são de taco; o resto (salão, corredores, depósito e escada), de concreto
            var piso = g.gameObject.AddComponent<PisoPorZona>();
            piso.zonas = Enumerable.Range(0, 4).Select(i => P.Comodos[i]).Select(c =>
                new Bounds(new Vector3((c.X0 + c.X1) / 2f, P.Fundo + 0.4f, (c.Z0 + c.Z1) / 2f), new Vector3(c.X1 - c.X0, 1.6f, c.Z1 - c.Z0))).ToArray();
            piso.superficies = Enumerable.Repeat(Superficie.Madeira, 4).ToArray();
            piso.resto = Superficie.Concreto;
            var colisor = new GameObject("Colisão");
            colisor.transform.SetParent(g, false);
            colisor.AddComponent<MeshCollider>().sharedMesh = _malhas["Bunker_Colisao"];
            var m = Grupo(g, "Móveis");
            const float f = P.Fundo, x = P.XEsc, parede = P.Esp / 2f;
            // salão: o painel de armas e a bancada na parede do fundo, de frente para a porta (as armas, a montagem das
            // armas põe no painel: "Parede de armas")
            Movel(m, "Painel_Armas", x, f, P.SZ1 - parede, 180f, false);
            Movel(m, "Bancada", x, f, P.SZ1 - parede - 0.41f, 180f);
            // a mesa comprida no meio, com 6 cadeiras e o rádio
            float mz = P.SZ0 + 6.85f;
            Movel(m, "Mesa_Comprida", x, f, mz, 90f);
            foreach (float dz in new[] { -0.75f, 0.75f })
            {
                Movel(m, "Cadeira", x - 0.85f, f, mz + dz, 90f);
                Movel(m, "Cadeira", x + 0.85f, f, mz + dz, 270f);
            }
            Movel(m, "Cadeira", x, f, mz - 1.85f, 0f);
            Movel(m, "Cadeira", x, f, mz + 1.85f, 180f);
            Movel(m, "Radio", x + 0.2f, f + 0.76f, mz - 0.4f, 75f, false);
            // perto da porta, o sofá na parede da direita (embaixo do mapa) com o tapete; a poltrona no canto do fundo
            Movel(m, "Sofa_Marrom", P.SX1 - parede - 0.45f, f, P.SZ0 + 1.85f, 270f);
            Movel(m, "Tapete", P.SX1 - 1.75f, f, P.SZ0 + 1.85f, 90f, false);
            Movel(m, "Poltrona_Vinho", P.SX0 + parede + 0.5f, f, P.SZ0 + 13.2f, 100f);
            // as prateleiras na parede da esquerda (entre a pilastra e o corredor do quarto 2), caixas e um tambor no canto
            Movel(m, "Prateleira_Cheia", P.SX0 + parede + 0.27f, f, P.SZ0 + 4.6f, 90f);
            Movel(m, "Prateleira_Cheia", P.SX0 + parede + 0.27f, f, P.SZ0 + 5.95f, 90f);
            Movel(m, "Caixas", P.SX0 + 0.75f, f, P.SZ0 + 0.55f, 10f);
            Movel(m, "Tambor", P.SX0 + 0.5f, f, P.SZ0 + 1.35f, 0f);
            // os quartos, um para cada um da família (cada cama com o cobertor de uma cor): a cama com a cabeceira na
            // parede do fundo do quarto (a oposta à entrada), o criado-mudo, o armário no canto, a cômoda e o tapete.
            // a = a distância da parede do fundo, d = o sentido de lá para a entrada
            string[] camas = { "Cama_Solteiro", "Cama_Solteiro_Verde", "Cama_Solteiro_Vinho", "Cama_Solteiro_Marrom" };
            for (int i = 0; i < 4; i++)
            {
                var c = P.Comodos[i];
                float d = -c.ParaDentro, xf = (c.lado < 0 ? c.X0 : c.X1) + d * parede, yaw = d > 0 ? 90f : 270f;
                float A(float a) => xf + d * a;
                Movel(m, camas[i], A(1.0f), f, c.Z0 + 0.65f, yaw);
                Movel(m, "Criado_Mudo", A(0.22f), f, c.Z0 + 1.45f, yaw);
                Movel(m, "Armario", A(0.3f), f, c.Z1 - 0.65f, yaw);
                Movel(m, "Comoda", A(1.9f), f, c.Z1 - 0.36f, 180f);
                Movel(m, "Tapete", A(2.3f), f, c.ZMeio + 0.1f, 0f, false);
            }
            // o depósito: os armários de aço na parede do fundo, as prateleiras, a mesa (para a comida, na Parte 7), os
            // tambores de água e caixas perto da entrada
            {
                var c = P.Comodos[4];
                float xf = c.X0 + parede;
                foreach (float dz in new[] { -1f, 0f, 1f }) Movel(m, "Armario_Metal", xf + 0.27f, f, c.ZMeio + dz, 90f);
                Movel(m, "Prateleira_Cheia", xf + 1.35f, f, c.Z1 - 0.37f, 180f);
                Movel(m, "Prateleira_Cheia", xf + 2.7f, f, c.Z1 - 0.37f, 180f);
                var mesa = Movel(m, "Mesa_Grande", xf + 2.6f, f, c.ZMeio - 0.35f, 90f);
                // o que sobrou para a família: uma lata e uma garrafa na mesa
                var sobras = Grupo(g, "Comida e água");
                MarcarItem(sobras, mesa.transform.TransformPoint(new Vector3(-0.3f, Tampos["Mesa_Grande"].topo, 0.1f)), "Comida");
                MarcarItem(sobras, mesa.transform.TransformPoint(new Vector3(0.25f, Tampos["Mesa_Grande"].topo, -0.15f)), "Agua");
                Movel(m, "Tambor", xf + 1.1f, f, c.Z0 + 0.45f, 0f);
                Movel(m, "Tambor", xf + 1.75f, f, c.Z0 + 0.45f, 40f);
                Movel(m, "Caixas", xf + 3.9f, f, c.Z0 + 0.55f, 15f);
            }
            // o eco: de salão grande de concreto, e de corredor de pedra na escada
            var eco = new GameObject("Eco do bunker").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(g, false); eco.transform.localPosition = new Vector3(x, f + 2f, P.SZ0 + 7f);
            eco.reverbPreset = AudioReverbPreset.Hallway; eco.minDistance = 7f; eco.maxDistance = 11f;
            var ecoEscada = new GameObject("Eco da escada").AddComponent<AudioReverbZone>();
            ecoEscada.transform.SetParent(g, false); ecoEscada.transform.localPosition = new Vector3(x, P.YPatamar + 1f, P.ZLance2);
            ecoEscada.reverbPreset = AudioReverbPreset.StoneCorridor; ecoEscada.minDistance = 2.5f; ecoEscada.maxDistance = 5.5f;
        }

        /// <summary>
        /// O que muda em Play no bunker fica fora da cidade (que é estática): o "Bunker (rede)", um NetworkObject com o
        /// <see cref="BunkerRede"/>, nas coordenadas da casa da família. Dentro, a folha da porta (aberta, para o NavMesh
        /// passar por ela; em Play ela começa fechada e o obstáculo dela fecha a passagem), as luzes com as lâmpadas e os
        /// interruptores.
        /// </summary>
        static void MontarRedeDoBunker(Transform casa)
        {
            var raiz = new GameObject("Bunker (rede)");
            raiz.transform.SetPositionAndRotation(casa.position, casa.rotation);
            raiz.AddComponent<NetworkObject>();
            var bunker = raiz.AddComponent<BunkerRede>();
            // o lado de dentro da porta: o salão, os corredores e os cômodos
            var min = new Vector3(P.Comodos.Min(c => c.X0) - 0.3f, P.Fundo - 0.5f, P.SZ0 + 0.12f);
            var max = new Vector3(P.Comodos.Max(c => c.X1) + 0.3f, P.Teto + 0.2f, P.SZ1 + 0.3f);
            bunker.dentro = new Bounds((min + max) / 2f, max - min);

            var porta = Modelo(raiz.transform, "Bunker_Porta", P.Dobradica, 100f, "Porta");
            var caixa = porta.AddComponent<BoxCollider>();
            caixa.center = new Vector3(-0.67f, 1.21f, 0f); caixa.size = new Vector3(1.3f, 2.4f, 0.12f);
            var corpo = porta.AddComponent<Rigidbody>();
            corpo.isKinematic = true; corpo.useGravity = false;
            var obstaculo = porta.AddComponent<NavMeshObstacle>();
            obstaculo.shape = NavMeshObstacleShape.Box; obstaculo.center = caixa.center; obstaculo.size = caixa.size;
            obstaculo.carving = true; obstaculo.carveOnlyStationary = true;
            var folha = porta.AddComponent<PortaDoBunker>();
            folha.bunker = bunker;
            bunker.porta = folha;

            // as luzes: uma lâmpada (que brilha acesa) e a luz, sem sombra, em cada luminária do modelo
            var luzes = Grupo(raiz.transform, "Luzes");
            var lista = P.Luzes().ToList();
            bunker.luzes = new Light[lista.Count];
            bunker.lampadas = new MeshRenderer[lista.Count];
            bunker.grupoDaLuz = new int[lista.Count];
            for (int i = 0; i < lista.Count; i++)
            {
                var (grupo, pos) = lista[i];
                var lampada = Modelo(luzes, "Bunker_Lampada", pos, 0f, $"Lâmpada {i + 1} (grupo {grupo})");
                var r = lampada.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                var luz = new GameObject("Luz").AddComponent<Light>();
                luz.transform.SetParent(lampada.transform, false);
                luz.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                luz.type = LightType.Point; luz.color = new Color(1f, 0.85f, 0.6f); luz.shadows = LightShadows.None;
                // as da escada ficam na parede, perto do concreto: mais fracas, senão estouram a parede
                (luz.intensity, luz.range) = grupo == 0 ? (9f, 12f) : grupo == 6 ? (1.4f, 6f) : (4f, 7f);
                bunker.luzes[i] = luz; bunker.lampadas[i] = r; bunker.grupoDaLuz[i] = grupo;
            }
            bunker.lampadaAcesa = _m["LampadaBunker"];
            bunker.lampadaApagada = _m["Cidade"];

            // os interruptores: a caixa com o colisor que a mira procura e a alavanca
            var chaves = Grupo(raiz.transform, "Interruptores");
            var lugares = P.Interruptores().ToList();
            bunker.interruptores = new Interruptor[lugares.Count];
            for (int i = 0; i < lugares.Count; i++)
            {
                var (pos, yaw, grupo, oQue) = lugares[i];
                var go = Modelo(chaves, "Interruptor", pos, yaw, $"Interruptor ({oQue})");
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var gatilho = go.AddComponent<BoxCollider>();
                gatilho.isTrigger = true; gatilho.center = new Vector3(0f, 0f, 0.05f); gatilho.size = new Vector3(0.22f, 0.26f, 0.1f);
                var alavanca = Modelo(go.transform, "Interruptor_Alavanca", new Vector3(0f, 0f, 0.054f), 0f, "Alavanca");
                alavanca.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var chave = go.AddComponent<Interruptor>();
                chave.bunker = bunker; chave.indice = i; chave.grupo = grupo; chave.oQue = oQue; chave.alavanca = alavanca.transform;
                bunker.interruptores[i] = chave;
            }
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
            var mesa = Movel(g, "Mesa", mx, y, mz, 0f);
            Movel(g, "Cadeira", mx - 0.35f, y, mz - 0.68f, 0f);
            Movel(g, "Cadeira", mx + 0.35f, y, mz + 0.68f, 180f);
            // cozinha: geladeira e fogão na divisória do quarto; o balcão sob a janela da esquerda (ou, na casa da
            // família, na parede da sala, depois da porta)
            Movel(g, "Geladeira", -0.47f, y, z1 - 0.55f, 270f);
            Movel(g, "Fogao", -0.44f, y, z1 - 1.45f, 270f);
            GameObject balcao;
            if (bunker) balcao = Movel(g, "Balcao", -1.25f, y, zDiv + 0.43f, 0f);
            else balcao = Movel(g, "Balcao", x0 + 0.43f, y, z1 - 0.3f * D, 90f);
            // quarto: cama com a cabeceira na parede do fundo, criado-mudo, armário e cômoda na divisória
            Movel(g, "Cama", x1 - 1.4f, y, z1 - 1.15f, 180f);
            var criado = Movel(g, "Criado_Mudo", x1 - 2.6f, y, z1 - 0.35f, 180f);
            Movel(g, _rng.NextDouble() < 0.5 ? "Armario" : "Armario_Aberto", 0.41f, y, zDiv + 0.7f, 90f);
            var comoda = Movel(g, "Comoda", 0.36f, y, z1 - 1.0f, 90f);
            // a comida e a água: a casa da família já foi saqueada (o que sobrou está no bunker)
            if (!bunker)
                Saque(g.parent, 4, mesa, balcao, criado, comoda, mesa, balcao,
                      g.TransformPoint(new Vector3(-1.2f, y, z1 - 0.9f)), g.TransformPoint(new Vector3(x1 - 2.2f, y, zDiv + 1.0f)));
        }

        // ------------------------------------------------------------------ comida e água (Parte 7)

        /// <summary>
        /// O tampo de cada móvel onde pode haver comida ou água: a altura e a área livre em cima (x e z locais do móvel,
        /// na Unity; as medidas são as do cidade_moveis.py). No balcão, o lado sem a pia; no criado-mudo, o lado sem o
        /// abajur.
        /// </summary>
        static readonly Dictionary<string, (float topo, Rect area)> Tampos = new Dictionary<string, (float, Rect)>
        {
            ["Mesa"] = (0.76f, new Rect(-0.6f, -0.35f, 1.2f, 0.7f)),
            ["Mesa_Grande"] = (0.76f, new Rect(-0.8f, -0.4f, 1.6f, 0.8f)),
            ["Balcao"] = (0.9f, new Rect(0.08f, -0.18f, 0.64f, 0.38f)),
            ["Comoda"] = (0.87f, new Rect(-0.42f, -0.16f, 0.84f, 0.32f)),
            ["Criado_Mudo"] = (0.58f, new Rect(0.12f, -0.12f, 0.07f, 0.24f)),
        };

        static System.Random _rngSaque;

        /// <summary>
        /// Sorteia 'quantos' lugares entre os candidatos (móveis com tampo ou pontos no chão, que podem se repetir: dois
        /// itens na mesma mesa) e marca cada um com um objeto "Lugar de item (Comida)" ou "(Agua)", no ponto de apoio e
        /// girado. A montagem das armas junta as marcas no "Itens no mundo" (<see cref="MontagemDasArmas.MontarItensNoMundo"/>).
        /// </summary>
        static void Saque(Transform pai, int quantos, params object[] candidatos)
        {
            var g = pai.Find("Comida e água") ?? Grupo(pai, "Comida e água");
            var lista = candidatos.ToList();
            for (int k = 0; k < quantos && lista.Count > 0; k++)
            {
                int i = _rngSaque.Next(lista.Count);
                var c = lista[i];
                lista.RemoveAt(i);
                Vector3 ponto;
                if (c is GameObject movel && Tampos.TryGetValue(movel.name, out var tampo))
                {
                    var a = tampo.area;
                    ponto = movel.transform.TransformPoint(new Vector3(a.x + a.width * (float)_rngSaque.NextDouble(), tampo.topo, a.y + a.height * (float)_rngSaque.NextDouble()));
                }
                else if (c is Vector3 chao) ponto = chao + new Vector3((float)_rngSaque.NextDouble() - 0.5f, 0f, (float)_rngSaque.NextDouble() - 0.5f) * 0.4f;
                else continue;
                MarcarItem(g, ponto, _rngSaque.NextDouble() < 0.55 ? "Comida" : "Agua");
            }
        }

        static void MarcarItem(Transform g, Vector3 ponto, string item)
        {
            var marca = new GameObject($"Lugar de item ({item})").transform;
            marca.SetParent(g, false);
            marca.SetPositionAndRotation(ponto, Quaternion.Euler(0f, (float)_rngSaque.NextDouble() * 360f, 0f));
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
            var mesa = Movel(g, "Mesa", mx, y, -2.0f, 0f);
            Movel(g, "Cadeira", mx - 0.35f, y, -2.68f, 0f);
            Movel(g, "Cadeira", mx + 0.35f, y, -1.32f, 180f);
            Movel(g, "Geladeira", 2.2f * lado, y, 0.04f, 180f);
            Movel(g, "Fogao", 3.05f * lado, y, 0.07f, 180f);
            var balcao = Movel(g, "Balcao", 4.3f * lado, y, 0.08f, 180f);
            Movel(g, "Cama", xm + 1.2f * lado, y, z1 - 1.15f, 180f);
            var criado = Movel(g, "Criado_Mudo", xm, y, z1 - 0.35f, 180f);
            Movel(g, _rng.NextDouble() < 0.5 ? "Armario" : "Armario_Aberto", 2.5f * lado, y, 0.93f, 0f);
            var comoda = Movel(g, "Comoda", 4.0f * lado, y, z1 - 0.35f, 180f);
            Saque(g, 2, mesa, balcao, criado, comoda, g.TransformPoint(new Vector3(3.0f * lado, y, -0.9f)));
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
        static GameObject Carro(Transform pai, Vector3 pos, float yaw, string modelo)
        {
            var go = Modelo(pai, modelo, pos, yaw, "Carro");
            ColisorDaMalha(go);
            return go;
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

        // ------------------------------------------------------------------ abandono (ambientação, fatia 1)

        /// <summary>
        /// O começo do apocalipse na rua: a fila de carros de quem tentou fugir nas saídas norte e leste, batidas no
        /// meio da cidade (dois carros que se chocaram no cruzamento, um capotado, um no poste), uma blitz abandonada
        /// na avenida sul e lixo pelas calçadas.
        /// - Só peças que já existem nos FBX, com o material da cidade: entram no batching estático e na oclusão como
        ///   o resto (a marcação de estático, no fim da montagem, vale para tudo dentro da "Cidade").
        /// - Sacos, caixas e cones não fazem sombra: são baixos, quase não se vê a sombra deles, e cada um custaria
        ///   desenhos a mais nas duas cascatas da sombra do sol e da lua.
        /// - Os carros ficam no grupo "Carros abandonados", com o nome "Carro": no futuro, podem virar fonte de peças.
        /// - Os lugares fogem das portas das casas, do portão da casa da família e da saída do bunker. A colisão
        ///   entra no NavMesh, que a montagem refaz no fim.
        /// </summary>
        static void Abandono(Transform g)
        {
            var carros = Grupo(g, "Carros abandonados");
            // saída norte: a fila de quem tentou fugir, parada na barricada (dois carros se tocaram na traseira)
            Carro(carros, new Vector3(-1.9f, 0f, 52.0f), 4f, "Carro_Sedan_Cinza");
            Carro(carros, new Vector3(2.0f, 0f, 50.0f), 352f, "Carro_Pickup_Bege");
            Carro(carros, new Vector3(-2.0f, 0f, 45.6f), 3f, "Carro_Sedan_Verde");
            PiscaAlerta(Carro(carros, new Vector3(1.8f, 0f, 45.0f), 12f, "Carro_Sedan_Azul"));
            // saída leste: a mesma fila, com um carro atravessado
            Carro(carros, new Vector3(51.5f, 0f, 1.9f), 93f, "Carro_Pickup_Azul");
            Carro(carros, new Vector3(49.0f, 0f, -1.9f), 86f, "Carro_Sedan_Vinho");
            Carro(carros, new Vector3(45.2f, 0f, 1.6f), 118f, "Carro_Sedan_Cinza");
            // cruzamento do meio: a picape bateu de frente no lado do sedã
            PiscaAlerta(Carro(carros, new Vector3(-1.0f, 0f, 0.6f), 80f, "Carro_Sedan_Azul"));
            Carro(carros, new Vector3(0.23f, 0f, 4.17f), 200f, "Carro_Pickup_Branco");
            // rua oeste: um capotado; avenida norte: um que subiu na calçada e parou no poste
            CarroCapotado(carros, new Vector3(-17f, 0f, -1.0f), 70f, "Carro_Sedan_Verde");
            CarroInclinado(carros, new Vector3(2.93f, 0f, 24.33f), new Vector3(-2.5f, 52f, 1.5f), "Carro_Sedan_Cinza");

            // avenida sul: a blitz que não segurou ninguém (barreiras abertas no meio, cavalete e cones derrubados)
            var blitz = Grupo(g, "Blitz abandonada");
            ColisorDaMalha(Modelo(blitz, "Barreira", new Vector3(-2.4f, 0f, -32.6f), 8f));
            ColisorDaMalha(Modelo(blitz, "Barreira", new Vector3(2.5f, 0f, -33.4f), 346f));
            ColisorDaMalha(Peca(blitz, "Cavalete", new Vector3(0.4f, 0f, -31.0f), new Vector3(90f, 25f, 0f)));
            ColisorDaMalha(Peca(blitz, "Cone", new Vector3(-3.3f, 0f, -30.4f), new Vector3(0f, 20f, 0f), false));
            ColisorDaMalha(Peca(blitz, "Cone", new Vector3(1.0f, 0f, -30.0f), new Vector3(0f, 40f, 90f), false));
            ColisorDaMalha(Peca(blitz, "Cone", new Vector3(3.3f, 0f, -31.2f), new Vector3(0f, 130f, 90f), false));
            ColisorDaMalha(Peca(blitz, "Cone", new Vector3(-0.9f, 0f, -34.8f), new Vector3(0f, 70f, 0f), false));
            CarroInclinado(carros, new Vector3(2.2f, 0f, -27.6f), new Vector3(0f, 150f, 0f), "Carro_Pickup_Branco");

            // lixo nas calçadas (y = 0,12) e na grama (y = 0): sacos, caixas, tambores e lixeiras caídas
            var lixo = Grupo(g, "Lixo");
            foreach (var (malha, pos, yaw, deitado) in new[] {
                // frente do prédio
                ("Sacos", new Vector3(26.2f, 0.12f, 5.7f), 20f, false), ("Sacos", new Vector3(27.3f, 0.12f, 5.5f), 200f, false),
                ("Caixas", new Vector3(25.2f, 0.12f, 5.9f), 5f, false),
                // atrás do prédio
                ("Tambor", new Vector3(24.0f, 0f, 30.0f), 0f, false), ("Tambor", new Vector3(25.1f, 0f, 30.5f), 15f, true),
                ("Caixas", new Vector3(22.9f, 0f, 30.6f), 80f, false),
                // rua oeste, lado norte
                ("Sacos", new Vector3(-9.6f, 0.12f, 5.6f), 60f, false), ("Sacos", new Vector3(-10.6f, 0.12f, 5.3f), 250f, false),
                // avenida norte, calçada oeste
                ("Caixas", new Vector3(-5.5f, 0.12f, 22.0f), 95f, false), ("Sacos", new Vector3(-5.6f, 0.12f, 23.2f), 10f, false),
                // perto da casa 4 e da praça
                ("Sacos", new Vector3(19.6f, 0.12f, -5.4f), 140f, false), ("Caixas", new Vector3(20.6f, 0.12f, -5.6f), 170f, false),
                // avenida sul, calçada leste
                ("Sacos", new Vector3(5.4f, 0.12f, -19.5f), 300f, false), ("Tambor", new Vector3(5.6f, 0.12f, -20.8f), 10f, true),
                // leste, perto da casa 5; sudoeste, longe do portão da casa da família
                ("Sacos", new Vector3(34.9f, 0.12f, -14.0f), 35f, false),
                ("Caixas", new Vector3(-29.0f, 0.12f, -5.4f), 15f, false), ("Sacos", new Vector3(-28.0f, 0.12f, -5.6f), 280f, false),
                // saída norte: o que caiu dos carros
                ("Caixas", new Vector3(0.2f, 0f, 48.6f), 30f, false), ("Sacos", new Vector3(-0.4f, 0f, 55.6f), 120f, false) })
            {
                bool sombra = malha == "Tambor";
                ColisorDaMalha(Peca(lixo, malha, pos, new Vector3(deitado ? 90f : 0f, yaw, 0f), sombra));
            }
            Lixeira(lixo, new Vector3(-8.5f, 0.12f, 5.7f), 120f, true);
        }

        /// <summary>
        /// Uma peça girada em qualquer eixo (deitada, tombada), posta no chão: depois do giro, o ponto mais baixo da malha
        /// fica na altura de <paramref name="pos"/>.y. Sem colisor; sombra opcional.
        /// </summary>
        static GameObject Peca(Transform pai, string malha, Vector3 pos, Vector3 giro, bool sombra = true)
        {
            var go = Modelo(pai, malha, pos);
            go.transform.localRotation = Quaternion.Euler(giro);
            NoChao(go, pos.y);
            if (!sombra) go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Sobe ou desce a peça até o ponto mais baixo da malha (já girada) tocar a altura y.</summary>
        static void NoChao(GameObject go, float y)
        {
            var b = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            var m = Matrix4x4.TRS(Vector3.zero, go.transform.localRotation, go.transform.localScale);
            float menor = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var canto = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                menor = Mathf.Min(menor, m.MultiplyPoint3x4(canto).y);
            }
            var p = go.transform.localPosition;
            go.transform.localPosition = new Vector3(p.x, y - menor, p.z);
        }

        /// <summary>Carro de rodas para cima, com o teto no chão.</summary>
        static void CarroCapotado(Transform pai, Vector3 pos, float yaw, string modelo)
        {
            ColisorDaMalha(Peca(pai, modelo, pos, new Vector3(0f, yaw, 180f))).gameObject.name = "Carro";
        }

        /// <summary>Carro girado nos três eixos (com uma roda na calçada, batido), apoiado no chão.</summary>
        static void CarroInclinado(Transform pai, Vector3 pos, Vector3 giro, string modelo)
        {
            ColisorDaMalha(Peca(pai, modelo, pos, giro)).gameObject.name = "Carro";
        }

        // ------------------------------------------------------------------ ambientação, fatia 2: marcas e sinais de vida

        const string PastaDecals = "Assets/_Game/Art/Materials/Decals";

        /// <summary>
        /// Marcas no chão (ambientação, fatia 2): freadas atrás das batidas, óleo embaixo dos carros batidos, sangue seco
        /// arrastado e papéis espalhados. São decals (o recurso "Decals" do renderizador, técnica de tela, até 60 m),
        /// todos com um material só (`Decal_Rua`): a textura tem os 4 desenhos, um em cada quarto, e cada decal escolhe
        /// o seu pelo deslocamento do UV. A caixa de cada um tem 0,6 m de altura, para pegar só o chão (e o pé de quem
        /// passar por cima).
        /// </summary>
        static void MarcasNoChao(Transform g)
        {
            var material = MaterialDasMarcas();
            const int Sangue = 0, Pneu = 1, Oleo = 2, Papel = 3;
            // (x, y, z, yaw, largura, comprimento, desenho, força); o fim do desenho (o +V) fica para a frente do yaw
            foreach (var (x, y, z, yaw, w, l, desenho, forca) in new[] {
                // freadas: a picape antes de bater no sedã, o carro que subiu no poste e o que capotou (dois trechos)
                (1.75f, 0f, 8.6f, 200f, 1.8f, 7.5f, Pneu, 0.9f),
                (1.1f, 0f, 22.9f, 52f, 1.8f, 6f, Pneu, 0.85f),
                (-23.5f, 0f, -1.6f, 80f, 1.8f, 7f, Pneu, 0.8f),
                (-29.5f, 0f, -2.4f, 72f, 1.8f, 6f, Pneu, 0.55f),
                // óleo embaixo e em volta dos carros batidos
                (-0.4f, 0f, 1.9f, 30f, 2.2f, 2.2f, Oleo, 0.85f),
                (-17.6f, 0f, -2.2f, 110f, 2.6f, 2.6f, Oleo, 0.8f),
                (3.3f, 0.12f, 25.2f, 0f, 1.6f, 1.6f, Oleo, 0.7f),
                (49.2f, 0f, -1.2f, 200f, 1.5f, 1.5f, Oleo, 0.6f),
                // sangue seco, arrastado: a blitz, a fila da saída norte, a praça e as calçadas
                (-1.6f, 0f, -29.4f, 30f, 1.2f, 2.6f, Sangue, 0.95f),
                (1.5f, 0f, -35.6f, 300f, 1.0f, 1.8f, Sangue, 0.8f),
                (0.4f, 0f, 47.3f, 100f, 1.0f, 2.4f, Sangue, 0.9f),
                (26.7f, 0.08f, -9.3f, 220f, 1.2f, 2.0f, Sangue, 0.85f),
                (-24.5f, 0.12f, 5.3f, 90f, 1.0f, 2.2f, Sangue, 0.8f),
                (18.4f, 0.12f, 5.3f, 75f, 1.0f, 1.8f, Sangue, 0.75f),
                (-5.3f, 0.12f, -14.0f, 0f, 1.0f, 2.0f, Sangue, 0.8f),
                // papéis espalhados (os da praça ficam fora do coreto, que é mais alto que a caixa do decal)
                (21.1f, 0.08f, -14.4f, 0f, 2.2f, 2.2f, Papel, 1f),
                (26.4f, 0.08f, -14.6f, 70f, 2.0f, 2.0f, Papel, 0.9f),
                (21.6f, 0.12f, 5.2f, 20f, 2.0f, 2.0f, Papel, 1f),
                (-0.6f, 0f, -28.4f, 140f, 2.5f, 2.5f, Papel, 1f),
                (-12.0f, 0f, -1.4f, 200f, 2.0f, 2.0f, Papel, 0.9f),
                (1.2f, 0f, 51.6f, 45f, 2.0f, 2.0f, Papel, 1f),
                (-1.6f, 0f, 14.0f, 300f, 2.0f, 2.0f, Papel, 0.85f),
                (12.0f, 0f, 2.0f, 110f, 2.0f, 2.0f, Papel, 0.9f) })
            {
                var go = new GameObject(desenho switch { Sangue => "Sangue seco", Pneu => "Freada", Oleo => "Óleo", _ => "Papéis" });
                go.transform.SetParent(g, false);
                go.transform.localPosition = new Vector3(x, y + 0.05f, z);
                go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                var d = go.AddComponent<DecalProjector>();
                d.material = material;
                d.size = new Vector3(w, l, 0.6f);
                d.pivot = Vector3.zero;
                d.uvScale = new Vector2(0.5f, 0.5f);
                d.uvBias = new Vector2(desenho % 2 * 0.5f, desenho / 2 * 0.5f);
                d.fadeFactor = forca;
                d.drawDistance = 40f;
                d.fadeScale = 0.85f;
            }
        }

        static Material MaterialDasMarcas()
        {
            Directory.CreateDirectory(PastaDecals);
            const string caminhoTextura = PastaDecals + "/Rua.png";
            const int n = 512, q = n / 2;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int desenho = (x / q) + (y / q) * 2;
                    float u = (x % q + 0.5f) / q * 2f - 1f, v = (y % q + 0.5f) / q * 2f - 1f;
                    px[y * n + x] = desenho switch { 0 => SangueSeco(u, v), 1 => Freada(u, v), 2 => Oleo(u, v), _ => Papeis(u, v) };
                }
            t.SetPixels(px);
            File.WriteAllBytes(caminhoTextura, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(caminhoTextura);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminhoTextura);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
            imp.SaveAndReimport();

            const string caminho = PastaDecals + "/Decal_Rua.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, caminho); }
            m.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(caminhoTextura));
            m.SetFloat("Normal_Blend", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // os desenhos ficam dentro de |u|, |v| < 0,9: a borda de cada quarto é vazia, para os mipmaps não misturarem
        static float Borda(float u, float v) => Mathf.Clamp01((0.92f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) * 20f);

        // uma mancha comprida, arrastada para +V, com riscos de arrasto e a borda irregular; vermelho escuro de sangue velho
        // (marrom puro, de noite e com a gradação fria, virava cinza e parecia sombra)
        static Color SangueSeco(float u, float v)
        {
            float largura = 0.42f * (1f - 0.35f * Mathf.Clamp01(v)) + 0.1f * (Mathf.PerlinNoise(v * 3f + 2f, 4.1f) - 0.5f);
            float forma = Mathf.Clamp01((largura - Mathf.Abs(u + 0.06f * Mathf.Sin(v * 2.5f))) * 12f) * Mathf.Clamp01((0.85f - Mathf.Abs(v)) * 6f);
            float poca = Mathf.Clamp01((0.38f - new Vector2(u * 0.9f, (v + 0.45f) * 1.2f).magnitude) * 8f);
            float riscos = 0.55f + 0.45f * Mathf.PerlinNoise(u * 14f, v * 1.5f + 9f);
            float a = Mathf.Max(poca, forma * riscos * (0.95f - 0.45f * Mathf.Clamp01(v)));
            float tom = 0.75f + 0.25f * Mathf.PerlinNoise(u * 5f + 3f, v * 5f);
            return new Color(0.27f * tom, 0.035f * tom, 0.025f, a * 0.92f * Borda(u, v));
        }

        // duas faixas pretas de pneu, quase retas, que vão escurecendo até onde o carro parou (+V)
        static Color Freada(float u, float v)
        {
            float curva = 0.12f * v * v;
            float a = 0f;
            foreach (float c in new[] { -0.5f, 0.5f })
            {
                float faixa = Mathf.Clamp01((0.16f - Mathf.Abs(u - c - curva)) * 40f);
                a = Mathf.Max(a, faixa * (0.6f + 0.4f * Mathf.PerlinNoise(v * 9f + c * 7f, u * 4f)));
            }
            a *= Mathf.SmoothStep(0f, 1f, (v + 0.9f) / 0.9f) * Mathf.Clamp01((0.9f - v) * 30f);
            return new Color(0.012f, 0.012f, 0.012f, a * Borda(u, v));
        }

        // uma poça escura e irregular, com dois pingos em volta
        static Color Oleo(float u, float v)
        {
            float ang = Mathf.Atan2(v, u), d = Mathf.Sqrt(u * u + v * v);
            float raio = 0.5f + 0.16f * Mathf.PerlinNoise(Mathf.Cos(ang) * 1.8f + 3f, Mathf.Sin(ang) * 1.8f + 3f) + 0.05f * Mathf.Sin(ang * 5f);
            float a = Mathf.Clamp01((raio - d) * 10f) * (0.75f + 0.25f * Mathf.PerlinNoise(u * 4f, v * 4f));
            a = Mathf.Max(a, Mathf.Clamp01((0.07f - new Vector2(u - 0.66f, v - 0.25f).magnitude) * 40f));
            a = Mathf.Max(a, Mathf.Clamp01((0.05f - new Vector2(u + 0.2f, v + 0.72f).magnitude) * 40f));
            return new Color(0.025f, 0.025f, 0.03f, a * 0.8f * Borda(u, v));
        }

        // folhas de papel jogadas, com linhas de texto
        static readonly (float x, float y, float giro, float w, float h, float tom)[] Folhas = {
            (-0.45f, -0.4f, 20f, 0.2f, 0.27f, 0.8f), (0.3f, -0.5f, 75f, 0.18f, 0.25f, 0.72f), (0.45f, 0.35f, 130f, 0.2f, 0.27f, 0.78f),
            (-0.35f, 0.45f, 200f, 0.16f, 0.22f, 0.68f), (0.0f, 0.0f, 300f, 0.2f, 0.27f, 0.82f), (-0.62f, 0.05f, 95f, 0.12f, 0.16f, 0.6f) };

        static Color Papeis(float u, float v)
        {
            foreach (var f in Folhas)
            {
                float c = Mathf.Cos(f.giro * Mathf.Deg2Rad), s = Mathf.Sin(f.giro * Mathf.Deg2Rad);
                float lu = (u - f.x) * c + (v - f.y) * s, lv = -(u - f.x) * s + (v - f.y) * c;
                if (Mathf.Abs(lu) > f.w || Mathf.Abs(lv) > f.h) continue;
                bool linha = Mathf.Abs(lu) < f.w * 0.75f && lv < f.h * 0.75f && lv > -f.h * 0.8f && Mathf.Repeat(lv * 40f, 1f) < 0.3f;
                float tom = f.tom * (linha ? 0.55f : 1f) * (0.9f + 0.1f * Mathf.PerlinNoise(u * 8f, v * 8f));
                return new Color(tom, tom * 0.97f, tom * 0.9f, 0.95f * Borda(u, v));
            }
            return new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// Sinais de vida (ambientação, fatia 2): o que ficou ligado quando as pessoas fugiram. Duas TVs (a casa 2 e um
        /// apartamento do 2º andar, de frente para a rua) e duas lâmpadas esquecidas (a sala da casa 4 e um apartamento do
        /// 3º andar). Cada uma é uma luz pequena, sem sombra, que o `SinalDeVida` faz tremer; ela sai pelas janelas.
        /// </summary>
        static void SinaisDeVida(Transform raiz)
        {
            var g = Grupo(raiz, "Sinais de vida");
            Transform Achar(string caminho) => raiz.Find(caminho) ?? throw new System.Exception($"Sinais de vida: não achei {caminho}.");
            // a TV fica em cima do rack, e a luz sai da tela para a frente dele
            void TV(string rack)
            {
                var r = Achar(rack);
                LuzDeSinal(g, "TV ligada", r.position + Vector3.up * 0.95f + r.forward * 0.55f, SinalDeVida.Tipo.TV, new Color(0.62f, 0.74f, 1f), 4f, 7f);
            }
            TV("Casa 2/Móveis/Rack_TV");
            TV("Prédio/Andar 2/Apartamento leste/Rack_TV");
            // a lâmpada fica perto do forro, no meio da sala
            var casa4 = Achar("Casa 4");
            LuzDeSinal(g, "Lâmpada acesa", casa4.TransformPoint(new Vector3(1.2f, 2.55f, -2.0f)), SinalDeVida.Tipo.Lampada, new Color(1f, 0.76f, 0.48f), 3.2f, 7f);
            var rackDoAndar3 = Achar("Prédio/Andar 3/Apartamento oeste/Rack_TV");
            LuzDeSinal(g, "Lâmpada acesa", rackDoAndar3.position + Vector3.up * 2.45f + rackDoAndar3.forward * 1.3f, SinalDeVida.Tipo.Lampada, new Color(1f, 0.8f, 0.55f), 3f, 7f);
        }

        static SinalDeVida LuzDeSinal(Transform pai, string nome, Vector3 pos, SinalDeVida.Tipo tipo, Color cor, float intensidade, float alcance)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, true);
            go.transform.position = pos;
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point; luz.color = cor; luz.intensity = intensidade; luz.range = alcance; luz.shadows = LightShadows.None;
            var s = go.AddComponent<SinalDeVida>();
            s.tipo = tipo; s.luz = luz;
            return s;
        }

        /// <summary>
        /// Pisca-alerta ligado num carro abandonado: as 4 lanternas (caixinhas com o material `PiscaAlerta`, que brilha)
        /// nos cantos, na altura dos faróis, e uma luz laranja pequena que pisca junto.
        /// </summary>
        static void PiscaAlerta(GameObject carro)
        {
            var b = carro.GetComponent<MeshFilter>().sharedMesh.bounds;
            var raiz = new GameObject("Pisca-alerta").transform;
            raiz.SetParent(carro.transform, false);
            var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var material = MaterialDoPisca();
            float y = b.min.y + b.size.y * 0.5f, x = b.extents.x - 0.22f;
            // o carro não é uma caixa: na altura das lanternas a lataria fica para dentro do limite da malha (o para-choque
            // vai mais longe). Um raio na malha (um colisor temporário) acha a lataria de verdade, e a lanterna encosta nela.
            var medidor = new GameObject("Medidor temporário").AddComponent<MeshCollider>();
            medidor.sharedMesh = carro.GetComponent<MeshFilter>().sharedMesh;
            var lanternas = new List<Renderer>();
            foreach (float lado in new[] { 1f, -1f })
                foreach (float lx in new[] { b.center.x - x, b.center.x + x })
                {
                    float borda = lado > 0 ? b.max.z : b.min.z;
                    var raio = new Ray(new Vector3(lx, y, borda + lado), new Vector3(0f, 0f, -lado));
                    float lz = medidor.Raycast(raio, out var acerto, 3f) ? acerto.point.z + lado * 0.025f : borda;
                    var l = new GameObject("Lanterna");
                    l.transform.SetParent(raiz, false);
                    l.transform.localPosition = new Vector3(lx, y, lz);
                    l.transform.localScale = new Vector3(0.26f, 0.1f, 0.07f);
                    l.AddComponent<MeshFilter>().sharedMesh = cubo;
                    var r = l.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
                    lanternas.Add(r);
                }
            Object.DestroyImmediate(medidor.gameObject);
            // o SinalDeVida fica na raiz do pisca (o pai das lanternas): a marcação de estático pula tudo o que está dentro dele
            var luz = new GameObject("Luz do pisca").AddComponent<Light>();
            luz.transform.SetParent(raiz, false);
            luz.transform.localPosition = new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
            luz.type = LightType.Point; luz.color = new Color(1f, 0.55f, 0.12f); luz.intensity = 3f; luz.range = 5f; luz.shadows = LightShadows.None;
            var s = raiz.gameObject.AddComponent<SinalDeVida>();
            s.tipo = SinalDeVida.Tipo.PiscaAlerta; s.luz = luz; s.lanternas = lanternas.ToArray();
        }

        /// <summary>
        /// Unlit com a cor acima de 1 (HDR): brilha e o bloom espalha, sem depender da palavra-chave _EMISSION do Lit
        /// (ela some do material depois de carregado; o mesmo acontece com a `Lampada`).
        /// </summary>
        static Material MaterialDoPisca()
        {
            string caminho = $"{PastaMateriais}/PiscaAlerta.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (m == null) { m = new Material(unlit); AssetDatabase.CreateAsset(m, caminho); }
            m.shader = unlit;
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", new Color(1f, 0.38f, 0.03f) * 1.5f);   // mais forte, a gradação (ACES) deixa quase branco
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
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
        /// Os 4 pontos de nascimento vão para o salão do bunker (nas coordenadas da casa da família, como o
        /// <see cref="Bunker"/>): dois de cada lado da mesa comprida, atrás das cadeiras, todos olhando para ela.
        /// </summary>
        internal static void PontosNoBunker(UnityEngine.SceneManagement.Scene cena)
        {
            var pontos = cena.GetRootGameObjects().FirstOrDefault(r => r.name == "Pontos de nascimento");
            var bunker = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Bunker" && t.parent != null && t.parent.name == "Casa da família");
            if (pontos == null || bunker == null) return;
            var mesa = new Vector3(P.XEsc, P.Fundo, P.SZ0 + 6.85f);
            var lugares = new[] { new Vector3(-1.6f, 0f, -1.25f), new Vector3(1.6f, 0f, -1.25f), new Vector3(-1.6f, 0f, 1.25f), new Vector3(1.6f, 0f, 1.25f) }
                .Select(d => mesa + d).ToArray();
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
