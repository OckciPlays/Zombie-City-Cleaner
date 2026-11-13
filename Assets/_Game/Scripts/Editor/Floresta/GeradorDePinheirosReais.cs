using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os pinheiros realistas (o Plano_Floresta.md, "Os pinheiros realistas", 09/11/2026) e a cena de floresta andável.
    /// Menu Zombie City Cleaner > Floresta > Gerar os pinheiros realistas: importa o atlas dos galhos (feito no Blender
    /// pelo galhos_pinheiro.py da pasta de arte), desenha a casca (a cor e o normal map), faz os materiais (o
    /// ArvoreRealista.shader), as malhas dos LODs 0 e 1 (<see cref="PinheiroRealista"/>), assa o atlas dos cartazes (cada
    /// árvore fotografada de lado, sem luz, pela câmera do Editor), as malhas do LOD 2 e os prefabs (o LODGroup com o
    /// cross-fade, os dois materiais e a cápsula do tronco). Menu Exemplos > Floresta (andar): a cena com a
    /// <see cref="FlorestaDeTeste"/> e o jogador das cenas de exemplo.
    /// </summary>
    public static class GeradorDePinheirosReais
    {
        public const string PastaDasTexturas = GeradorDeFloresta.Pasta + "/Texturas";
        public const string PastaDosMateriais = GeradorDeFloresta.Pasta + "/Materiais";
        public const string Galhos = PastaDasTexturas + "/Pinheiro_Galhos.png";
        public const string GalhosNormal = PastaDasTexturas + "/Pinheiro_Galhos_Normal.png";
        public const string Casca = PastaDasTexturas + "/T_Casca_Pinheiro.png";
        public const string CascaNormal = PastaDasTexturas + "/T_Casca_Pinheiro_Normal.png";
        public const string Cartazes = PastaDasTexturas + "/Pinheiro_Cartazes.png";
        public const string CartazesLeve = PastaDasTexturas + "/Pinheiro_Cartazes_Leve.png";
        public const string MaterialDoTronco = PastaDosMateriais + "/Pinheiro_Tronco.mat";
        public const string MaterialDosGalhos = PastaDosMateriais + "/Pinheiro_Galhos.mat";
        public const string MaterialDoCartaz = PastaDosMateriais + "/Pinheiro_Cartaz.mat";
        public const string MaterialDosGalhosLeve = PastaDosMateriais + "/Pinheiro_Galhos_Leve.mat";
        public const string MaterialDoCartazLeve = PastaDosMateriais + "/Pinheiro_Cartaz_Leve.mat";
        /// <summary>O material só do pinheiro leve (o tronco e os galhos juntos: o _UM_MATERIAL do ArvoreRealista.shader).</summary>
        public const string MaterialDoPinheiroLeve = PastaDosMateriais + "/Pinheiro_Leve.mat";
        public const string CenaAndar = "Assets/_Game/Scenes/Exemplos/Floresta_Andar.unity";
        public const string CenaLeve = "Assets/_Game/Scenes/Exemplos/Floresta_Leve.unity";
        const string NomeDoShader = "ZombieCityCleaner/ArvoreRealista";
        const int LadoDaCasca = 1024;
        // o atlas dos cartazes: 8 × 2 vagas de 512 × 1024
        const int CartazW = 512, CartazH = 1024, CartazColunas = 8, CartazLinhas = 2;

        public static string Prefab(PinheiroRealista.Tamanho t, int v, bool leve = false) => $"{GeradorDeFloresta.PastaDosPrefabs}/{PinheiroRealista.Nome(t, v, leve)}.prefab";

        /// <summary>Os 15 prefabs na ordem dos tamanhos e das variações (null onde faltar).</summary>
        public static GameObject[] Prefabs(bool leve = false)
        {
            var lista = new List<GameObject>();
            for (int t = 0; t < PinheiroRealista.Tamanhos; t++)
                for (int v = 1; v <= PinheiroRealista.Variacoes; v++)
                    lista.Add(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab((PinheiroRealista.Tamanho)t, v, leve)));
            return lista.ToArray();
        }

        [MenuItem("Zombie City Cleaner/Floresta/Gerar os pinheiros realistas", false, 12)]
        public static void Gerar() => GerarConjunto(false);

        /// <summary>A versão leve (o Plano_Floresta.md, "A versão leve"): os mesmos passos, com os parâmetros leves e o cartaz que vira para a câmera.</summary>
        [MenuItem("Zombie City Cleaner/Floresta/Gerar os pinheiros leves", false, 13)]
        public static void GerarLeves() => GerarConjunto(true);

        static void GerarConjunto(bool leve)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Saia do Play antes de gerar os pinheiros."); return; }
            Directory.CreateDirectory(PastaDasTexturas);
            Directory.CreateDirectory(PastaDosMateriais);
            Directory.CreateDirectory(GeradorDeFloresta.PastaDasMalhas);
            Directory.CreateDirectory(GeradorDeFloresta.PastaDosPrefabs);
            AssetDatabase.Refresh();
            if (!File.Exists(Galhos))
            {
                Debug.LogError($"Falta o atlas dos galhos ({Galhos}): rode o galhos_pinheiro.py (Modelo 3D/Floresta) no Blender.");
                return;
            }
            ImportarGalhos();
            TexturasDaCasca();
            var tronco = Material(MaterialDoTronco, Casca, CascaNormal, 1f, 0.1f, 0f, CullMode.Back, false, 1f, 0f);
            // na leve, as agulhas sem brilho (a pedido: "as folhas parecem ter um brilho") e menos luz atravessando
            var galhos = leve
                ? Material(MaterialDosGalhosLeve, Galhos, GalhosNormal, 0.5f, 0f, 0.15f, CullMode.Off, true, 1f, 0.05f)
                : Material(MaterialDosGalhos, Galhos, GalhosNormal, 0.6f, 0.12f, 0.35f, CullMode.Off, true, 1f, 0.06f);
            // o leve com um material só (09/11/2026): um lote por árvore e por passada, em vez de dois (o tronco e os galhos
            // separados davam a metade dos draw calls das árvores). Os dois de antes continuam: o assar dos cartazes usa
            var unico = leve ? MaterialUnico() : null;

            // as malhas de perto (os LODs 0 e 1)
            var lista = new List<(PinheiroRealista.Parametros p, string nome)>();
            int triangulos0 = 0, triangulos1 = 0;
            var porTamanho = new int[PinheiroRealista.Tamanhos];
            for (int t = 0; t < PinheiroRealista.Tamanhos; t++)
                for (int v = 1; v <= PinheiroRealista.Variacoes; v++)
                {
                    var tam = (PinheiroRealista.Tamanho)t;
                    var p = PinheiroRealista.Sortear(tam, PinheiroRealista.Semente(tam, v, leve), leve);
                    string nome = PinheiroRealista.Nome(tam, v, leve);
                    var m0 = GravarMalha(PinheiroRealista.Malha(p, 0, nome), $"{GeradorDeFloresta.PastaDasMalhas}/{nome}.asset");
                    var m1 = GravarMalha(PinheiroRealista.Malha(p, 1, nome + "_Longe1"), $"{GeradorDeFloresta.PastaDasMalhas}/{nome}_Longe1.asset");
                    triangulos0 += m0.triangles.Length / 3;
                    triangulos1 += m1.triangles.Length / 3;
                    porTamanho[t] += m0.triangles.Length / 3;
                    lista.Add((p, nome));
                }
            AssetDatabase.SaveAssets();

            // os cartazes de longe (precisam das malhas de perto e dos materiais) e o LOD 2
            AssarCartazes(lista, tronco, galhos, leve ? CartazesLeve : Cartazes);
            var cartaz = leve
                ? Material(MaterialDoCartazLeve, CartazesLeve, null, 0f, 0f, 0f, CullMode.Off, true, 0.6f, 0f, true)
                : Material(MaterialDoCartaz, Cartazes, null, 0f, 0.1f, 0f, CullMode.Off, true, 0.6f, 0f);
            foreach (var (p, nome) in lista)
            {
                int indice = PinheiroRealista.Indice(p.tamanho, p.semente - PinheiroRealista.Semente(p.tamanho, 0, leve));
                var m2 = GravarMalha(PinheiroRealista.MalhaDoCartaz(p, PinheiroRealista.Cartaz(indice), nome + "_Longe2", leve), $"{GeradorDeFloresta.PastaDasMalhas}/{nome}_Longe2.asset");
                var malhas = new[]
                {
                    AssetDatabase.LoadAssetAtPath<Mesh>($"{GeradorDeFloresta.PastaDasMalhas}/{nome}.asset"),
                    AssetDatabase.LoadAssetAtPath<Mesh>($"{GeradorDeFloresta.PastaDasMalhas}/{nome}_Longe1.asset"),
                    m2,
                };
                // os LODs 0 e 1 do leve numa submalha só (depois dos cartazes, que fotografam com os dois materiais)
                if (unico != null)
                {
                    malhas[0] = GravarMalha(UmaSubmalha(malhas[0]), $"{GeradorDeFloresta.PastaDasMalhas}/{nome}.asset");
                    malhas[1] = GravarMalha(UmaSubmalha(malhas[1]), $"{GeradorDeFloresta.PastaDasMalhas}/{nome}_Longe1.asset");
                }
                GravarPrefab(nome, malhas, tronco, galhos, cartaz, p, unico);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Floresta] {lista.Count} pinheiros {(leve ? "leves" : "realistas")}: em média {triangulos0 / lista.Count} triângulos de perto (o alto {porTamanho[0] / PinheiroRealista.Variacoes}, o médio {porTamanho[1] / PinheiroRealista.Variacoes}, o pequeno {porTamanho[2] / PinheiroRealista.Variacoes}) e {triangulos1 / lista.Count} no LOD 1; os prefabs em {GeradorDeFloresta.PastaDosPrefabs}.");
        }

        // ------------------------------------------------------------------ as texturas

        static void ImportarGalhos()
        {
            AssetDatabase.ImportAsset(Galhos, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(Galhos);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.35f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            if (File.Exists(GalhosNormal))
            {
                AssetDatabase.ImportAsset(GalhosNormal, ImportAssetOptions.ForceUpdate);
                var impN = (TextureImporter)AssetImporter.GetAtPath(GalhosNormal);
                impN.textureType = TextureImporterType.NormalMap;
                impN.mipmapEnabled = true;
                impN.wrapMode = TextureWrapMode.Clamp;
                impN.maxTextureSize = 2048;
                impN.textureCompression = TextureImporterCompression.CompressedHQ;
                impN.SaveAndReimport();
            }
        }

        /// <summary>
        /// A casca do pinheiro, repetível (0,6 m): as placas (células alongadas na vertical, cada uma no seu tom, com as
        /// fendas escuras entre elas), as fibras verticais, uns liquens claros e o normal map pela altura das placas.
        /// </summary>
        static void TexturasDaCasca()
        {
            const int L = LadoDaCasca;
            var px = new Color[L * L];
            var h = new float[L * L];
            var base0 = new Color(0.30f, 0.235f, 0.185f);
            var fenda = new Color(0.13f, 0.1f, 0.075f);
            var liquen = new Color(0.52f, 0.56f, 0.44f);
            for (int y = 0; y < L; y++)
            for (int x = 0; x < L; x++)
            {
                float u = x / (float)L, v = y / (float)L;
                // as escamas: células pequenas (uns 2 × 3 cm), um pouco mais altas que largas
                Worley(u, v, 30, 20, out float f1, out float f2, out float id);
                float borda = f2 - f1;
                float placa = Passo(0f, 0.12f, borda);
                float cupula = Mathf.Clamp01(1f - f1 * 1.8f);
                // os sulcos verticais finos (a fibra), a variação lenta e o grão
                float fibra = Periodico(u * 96f, v * 9f, 96, 9, 3.1f);
                float fibra2 = Periodico(u * 220f, v * 14f, 220, 14, 8.9f);
                float lento = Periodico(u * 4f, v * 3f, 4, 3, 1.7f);
                float grao = Periodico(u * 200f, v * 200f, 200, 200, 9.3f);
                float tom = 0.86f + 0.26f * Hash01(id * 17.3f);
                float altura = placa * (0.35f + 0.35f * cupula) + fibra * 0.3f + fibra2 * 0.15f + grao * 0.06f;
                h[y * L + x] = altura;
                var cor = base0 * tom * (0.78f + 0.3f * fibra) * (0.9f + 0.18f * fibra2) * (0.9f + 0.2f * lento) * (0.93f + 0.14f * grao);
                cor = Color.Lerp(fenda, cor, 0.35f + 0.65f * placa);
                // uns pontos de líquen, poucos e pequenos
                float liq = Passo(0.8f, 0.88f, Periodico(u * 44f, v * 32f, 44, 32, 5.7f)) * 0.35f;
                cor = Color.Lerp(cor, liquen, liq * placa);
                cor.a = 1f;
                px[y * L + x] = cor;
            }
            Salvar(px, Casca, L, true, false);
            SalvarNormal(h, CascaNormal, L, 5f);
        }

        // as células de Worley periódicas, alongadas na vertical (nx × ny células no quadrado), e o id da mais perto
        static void Worley(float u, float v, int nx, int ny, out float f1, out float f2, out float id)
        {
            float cx = u * nx, cy = v * ny;
            int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
            f1 = f2 = 9f; id = 0f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = ix + i, gy = iy + j;
                int mx = ((gx % nx) + nx) % nx, my = ((gy % ny) + ny) % ny;
                float jx = Hash01(mx * 12.9898f + my * 78.233f + 1f), jy = Hash01(mx * 93.9898f + my * 67.345f + 7f);
                float dx = (gx + jx - cx), dy = (gy + jy - cy) * (nx / (float)ny) * 0.8f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < f1) { f2 = f1; f1 = d; id = mx * 131f + my * 17f; }
                else if (d < f2) f2 = d;
            }
        }

        // o smoothstep do GLSL (o Mathf.SmoothStep interpola de a até b)
        static float Passo(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Hash01(float s)
        {
            float h = Mathf.Sin(s) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // o ruído de valor periódico (0..1), com o período em cada eixo
        static float Periodico(float x, float y, int px, int py, float semente)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float C(int a, int b) => Hash01((((a % px) + px) % px) * 127.1f + (((b % py) + py) % py) * 311.7f + semente * 74.7f);
            return Mathf.Lerp(Mathf.Lerp(C(x0, y0), C(x0 + 1, y0), fx), Mathf.Lerp(C(x0, y0 + 1), C(x0 + 1, y0 + 1), fx), fy);
        }

        static Texture2D Salvar(Color[] px, string caminho, int lado, bool repete, bool transparente)
        {
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = repete ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = transparente;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        static Texture2D SalvarNormal(float[] h, string caminho, int lado, float forca)
        {
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = (h[y * lado + (x + 1) % lado] - h[y * lado + (x + lado - 1) % lado]) * 0.5f * forca;
                float dy = (h[((y + 1) % lado) * lado + x] - h[((y + lado - 1) % lado) * lado + x]) * 0.5f * forca;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                px[y * lado + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt(n.z * 255f), 255);
            }
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        // ------------------------------------------------------------------ os materiais

        static Material Material(string caminho, string cor, string normal, float forcaDaNormal, float brilho, float translucencia, CullMode lados, bool recorte, float balanco, float tremor, bool cartaz = false)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            var shader = Shader.Find(NomeDoShader);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, caminho);
            }
            else m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor));
            if (normal != null) m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            m.SetFloat("_BumpForca", forcaDaNormal);
            m.SetFloat("_Suavidade", brilho);
            m.SetFloat("_Translucencia", translucencia);
            m.SetFloat("_Cull", (float)lados);
            m.SetFloat("_Recorte", recorte ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.35f);
            m.SetFloat("_Variacao", recorte ? 0.5f : 0.3f);
            m.SetFloat("_Cartaz", cartaz ? 1f : 0f);
            m.SetFloat("_Balanco", balanco);
            m.SetFloat("_Tremor", tremor);
            if (recorte) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// O material só do pinheiro leve: o dos galhos leve (recortado, dos dois lados, sem brilho, a luz atravessando a 0,15)
        /// com a casca do tronco (a textura, a normal a 1, o brilho a 0,1 e a variação de tom a 0,3, como no material dele).
        /// </summary>
        static Material MaterialUnico()
        {
            var m = Material(MaterialDoPinheiroLeve, Galhos, GalhosNormal, 0.5f, 0f, 0.15f, CullMode.Off, true, 1f, 0.05f);
            m.SetTexture("_CascaMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Casca));
            m.SetTexture("_CascaNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(CascaNormal));
            m.SetFloat("_CascaBump", 1f);
            m.SetFloat("_CascaSuavidade", 0.1f);
            m.SetFloat("_CascaVariacao", 0.3f);
            m.SetFloat("_UmMaterial", 1f);
            m.EnableKeyword("_UM_MATERIAL");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// As duas submalhas do pinheiro (0 o tronco, 1 as cartas) numa só, com o tronco primeiro (ele tapa as cartas de trás
        /// antes delas) e a cor do vértice A dizendo o que é cada parte: 0 a casca, 1 os galhos (o R, o G e o B, o vento,
        /// ficam como estão).
        /// </summary>
        static Mesh UmaSubmalha(Mesh m)
        {
            if (m.subMeshCount < 2) return Object.Instantiate(m);
            var cores = m.colors32;
            var tronco = m.GetTriangles(0);
            var galhos = m.GetTriangles(1);
            for (int i = 0; i < cores.Length; i++) cores[i].a = 255;
            foreach (int i in tronco) cores[i].a = 0;
            int divididos = 0;
            foreach (int i in galhos) if (cores[i].a == 0) divididos++;
            if (divididos > 0) Debug.LogWarning($"[Floresta] {m.name}: {divididos} vértices são do tronco e dos galhos ao mesmo tempo.");
            var tri = new int[tronco.Length + galhos.Length];
            tronco.CopyTo(tri, 0);
            galhos.CopyTo(tri, tronco.Length);
            var nova = new Mesh { name = m.name, indexFormat = m.indexFormat };
            nova.SetVertices(m.vertices);
            nova.SetNormals(m.normals);
            nova.SetTangents(m.tangents);
            nova.SetUVs(0, m.uv);
            nova.SetColors(cores);
            nova.SetTriangles(tri, 0, true);
            nova.RecalculateBounds();
            return nova;
        }

        // ------------------------------------------------------------------ as malhas e os prefabs

        /// <summary>Grava a malha no asset (trocando o conteúdo do que existe: os prefabs e as cenas continuam valendo).</summary>
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
            velha.SetTangents(nova.tangents);
            velha.SetUVs(0, nova.uv);
            velha.SetColors(nova.colors32);
            velha.subMeshCount = nova.subMeshCount;
            for (int s = 0; s < nova.subMeshCount; s++) velha.SetTriangles(nova.GetTriangles(s), s, true);
            velha.RecalculateBounds();
            EditorUtility.SetDirty(velha);
            Object.DestroyImmediate(nova);
            return velha;
        }

        static void GravarPrefab(string nome, Mesh[] malhas, Material tronco, Material galhos, Material cartaz, PinheiroRealista.Parametros p, Material unico = null)
        {
            string caminho = $"{GeradorDeFloresta.PastaDosPrefabs}/{nome}.prefab";
            var raiz = new GameObject(nome);
            var grupo = raiz.AddComponent<LODGroup>();
            grupo.fadeMode = LODFadeMode.CrossFade;
            // Sem o cross-fade animado: com ele, o GPU Resident Drawer exclui todos os renderers do grupo
            // (GRDExclusionReason.LODAnimateCrossFading) e cada árvore vira um draw call (26 mil por quadro na cena leve,
            // contra 900 com ele desligado). O cross-fade pela distância (o pontilhado na faixa de transição) continua.
            grupo.animateCrossFading = false;
            var lods = new LOD[3];
            // as trocas (a fração da altura da tela; com o FOV de 70° e o lodBias 2 da qualidade PC, o da Alta, a distância =
            // tamanho × 2 / (1,4 × fração)): o alto leve (15–19 m) fica no LOD 0 até 54–66 m e no LOD 1 até 178–221 m; o médio
            // (10–13 m) até 47–61 m e 157–205 m; o pequeno (3–5 m) até 28–45 m e 76–118 m (com a fração do alto, o pequeno
            // trocava a 11–18 m e virava cartaz a 38–59 m, na frente do jogador). O LOD 2 (o cartaz) nunca some. As frações
            // dobraram em 09/11/2026: as primeiras foram medidas com o lodBias em 1, que um teste tinha deixado no projeto.
            float[] telas = !p.leve ? new[] { 0.28f, 0.09f, 0.008f }
                : p.tamanho == PinheiroRealista.Tamanho.Alto ? new[] { 0.4f, 0.12f, 0.006f }
                : p.tamanho == PinheiroRealista.Tamanho.Medio ? new[] { 0.3f, 0.09f, 0.006f }
                : new[] { 0.16f, 0.06f, 0.006f };
            for (int lod = 0; lod < 3; lod++)
            {
                var go = new GameObject($"LOD{lod}");
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malhas[lod];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = lod < 2 ? (unico != null ? new[] { unico } : new[] { tronco, galhos }) : new[] { cartaz };
                // os LODs 0 e 1 fazem sombra (só o cartaz do LOD 2 não): com só o LOD 0, a sombra das árvores sumia a uns
                // 50 m, antes do alcance da sombra do sol
                r.shadowCastingMode = lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // a faixa do cross-fade pela distância (o pontilhado): 10% do alcance de cada LOD, um anel estreito (com 20%,
                // o médio ficava pontilhado de 20 a 46 m e o pequeno de 6 a 18 m, na frente do jogador)
                lods[lod] = new LOD(telas[lod], new Renderer[] { r }) { fadeTransitionWidth = 0.1f };
            }
            grupo.SetLODs(lods);
            grupo.RecalculateBounds();
            var capsula = raiz.AddComponent<CapsuleCollider>();
            capsula.radius = p.raioDoTronco;
            float alturaDoTronco = p.altura * 0.9f;
            capsula.height = alturaDoTronco + p.raioDoTronco * 2f;
            capsula.center = new Vector3(0f, alturaDoTronco * 0.5f, 0f);
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
        }

        // ------------------------------------------------------------------ os cartazes

        /// <summary>
        /// Cada árvore (o LOD 0) fotografada de lado por uma câmera ortográfica, sem o sol e com a luz ambiente branca
        /// (sai quase a cor pura; o shader ilumina o cartaz de novo), sem vento, longe de tudo; as fotos num atlas
        /// 4096 × 2048 com a cor espalhada para fora do alfa (o filtro não puxa o preto).
        /// </summary>
        static void AssarCartazes(List<(PinheiroRealista.Parametros p, string nome)> lista, Material tronco, Material galhos, string caminhoDoAtlas)
        {
            int W = CartazW * CartazColunas, H = CartazH * CartazLinhas;
            var atlas = new Color32[W * H];
            var rt = new RenderTexture(CartazW, CartazH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            rt.Create();
            var tex = new Texture2D(CartazW, CartazH, TextureFormat.RGBA32, false);

            // o mundo parado e sem luz direcional, só a ambiente branca
            var modo = RenderSettings.ambientMode;
            var ambiente = RenderSettings.ambientLight;
            Color ceu = RenderSettings.ambientSkyColor, equador = RenderSettings.ambientEquatorColor, chao = RenderSettings.ambientGroundColor;
            bool neblina = RenderSettings.fog;
            var vento = Shader.GetGlobalVector("_ZccVento");
            var luzes = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            var ligadas = new bool[luzes.Length];
            for (int i = 0; i < luzes.Length; i++) { ligadas[i] = luzes[i].enabled; luzes[i].enabled = false; }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.fog = false;
            Shader.SetGlobalVector("_ZccVento", Vector4.zero);
            DynamicGI.UpdateEnvironment();

            var camGo = new GameObject("Câmera dos cartazes") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.targetTexture = rt;
            cam.enabled = false;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            cam.cullingMask = ~0;
            var dados = cam.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            dados.requiresColorOption = CameraOverrideOption.Off;
            dados.requiresDepthOption = CameraOverrideOption.Off;

            const float Longe = 20000f;
            var arvore = new GameObject("Árvore do cartaz") { hideFlags = HideFlags.HideAndDontSave };
            arvore.transform.position = new Vector3(0f, Longe, 0f);
            var mf = arvore.AddComponent<MeshFilter>();
            var mr = arvore.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { tronco, galhos };
            try
            {
                foreach (var (p, nome) in lista)
                {
                    int indice = PinheiroRealista.Indice(p.tamanho, p.semente - PinheiroRealista.Semente(p.tamanho, 0, p.leve));
                    mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{GeradorDeFloresta.PastaDasMalhas}/{nome}.asset");
                    float alt = PinheiroRealista.AlturaDoCartaz(p);
                    cam.orthographicSize = alt * 0.5f;
                    cam.transform.SetPositionAndRotation(new Vector3(0f, Longe + PinheiroRealista.BaseDoCartaz(p) + alt * 0.5f, -200f), Quaternion.identity);
                    var pedido = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, pedido)) RenderPipeline.SubmitRenderRequest(cam, pedido);
                    else cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, CartazW, CartazH), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    var px = tex.GetPixels32();
                    var vaga = PinheiroRealista.Cartaz(indice);
                    int x0 = Mathf.RoundToInt(vaga.xMin * W), y0 = Mathf.RoundToInt(vaga.yMin * H);
                    for (int y = 0; y < CartazH; y++)
                        for (int x = 0; x < CartazW; x++)
                            atlas[(y0 + y) * W + x0 + x] = px[y * CartazW + x];
                }
            }
            finally
            {
                Object.DestroyImmediate(arvore);
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                RenderSettings.ambientMode = modo;
                RenderSettings.ambientLight = ambiente;
                RenderSettings.ambientSkyColor = ceu; RenderSettings.ambientEquatorColor = equador; RenderSettings.ambientGroundColor = chao;
                RenderSettings.fog = neblina;
                Shader.SetGlobalVector("_ZccVento", vento);
                for (int i = 0; i < luzes.Length; i++) if (luzes[i] != null) luzes[i].enabled = ligadas[i];
                DynamicGI.UpdateEnvironment();
            }
            Espalhar(atlas, W, H, 12);
            var saida = new Texture2D(W, H, TextureFormat.RGBA32, false);
            saida.SetPixels32(atlas);
            File.WriteAllBytes(caminhoDoAtlas, saida.EncodeToPNG());
            Object.DestroyImmediate(saida);
            AssetDatabase.ImportAsset(caminhoDoAtlas, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminhoDoAtlas);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.35f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        /// <summary>A cor dos pixels transparentes vira a do vizinho opaco mais perto (até 'passos' pixels).</summary>
        internal static void Espalhar(Color32[] px, int w, int h, int passos)
        {
            var sabe = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) sabe[i] = px[i].a > 8;
            var novo = new Color32[px.Length];
            var novoSabe = new bool[px.Length];
            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            for (int passo = 0; passo < passos; passo++)
            {
                System.Array.Copy(px, novo, px.Length);
                System.Array.Copy(sabe, novoSabe, sabe.Length);
                bool mudou = false;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (sabe[i]) continue;
                    for (int k = 0; k < 8; k++)
                    {
                        int xx = x + dx[k], yy = y + dy[k];
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        int j = yy * w + xx;
                        if (!sabe[j]) continue;
                        novo[i] = new Color32(px[j].r, px[j].g, px[j].b, 0);
                        novoSabe[i] = true;
                        mudou = true;
                        break;
                    }
                }
                System.Array.Copy(novo, px, px.Length);
                System.Array.Copy(novoSabe, sabe, sabe.Length);
                if (!mudou) break;
            }
            var fundo = new Color32(28, 48, 24, 0);
            for (int i = 0; i < px.Length; i++) if (!sabe[i]) px[i] = fundo;
        }

        // ------------------------------------------------------------------ a cena andável

        [MenuItem("Zombie City Cleaner/Exemplos/Floresta (andar)", false, 61)]
        public static void MontarCenaAndavel() => MontarCena(false);

        [MenuItem("Zombie City Cleaner/Exemplos/Floresta (andar, leve)", false, 62)]
        public static void MontarCenaLeve() => MontarCena(true);

        static void MontarCena(bool leve)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Saia do Play antes de montar a cena da floresta."); return; }
            if (!File.Exists(Prefab(PinheiroRealista.Tamanho.Alto, 1, leve))) GerarConjunto(leve);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.27f, 0.24f);
            var ceu = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoCeu.Caminho);
            if (ceu != null) RenderSettings.skybox = ceu;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.62f, 0.67f, 0.7f);

            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.color = new Color(1f, 0.95f, 0.86f);
            sol.intensity = 1.15f;
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.9f;
            sol.transform.rotation = Quaternion.Euler(42f, -30f, 0f);

            var go = new GameObject("Floresta de teste");
            go.SetActive(false);
            var floresta = go.AddComponent<FlorestaDeTeste>();
            floresta.pinheiros = Prefabs(leve);
            floresta.chao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            floresta.agua = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            var ajustes = go.AddComponent<AjustesDaFlorestaDeTeste>();
            if (leve) ajustes.reflexoAte = 120f;   // a sombra do sol e a grama vêm pela qualidade (os padrões do componente)
            if (leve) GeradorDeGrama.PorNaCena(go);
            go.SetActive(true);
            if (floresta.Arvores == 0) floresta.Gerar();

            float z = -floresta.lado * 0.5f + 25f;
            float x = floresta.RioX(z) + 12f;
            MontagemDoExemploDasCasas.Jogador(new Vector3(x, floresta.Altura(x, z) + 0.3f, z));
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = ceu != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; cam.farClipPlane = 1500f; }

            string caminho = leve ? CenaLeve : CenaAndar;
            Directory.CreateDirectory(Path.GetDirectoryName(caminho));
            EditorSceneManager.SaveScene(cena, caminho);
            Debug.Log($"[Floresta] A cena andável em {caminho}: {floresta.Resumo}.");
        }
    }
}
