using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A parede dos fundadores: os nomes de quem comentou primeiro nos vídeos (a promessa do primeiro vídeo), pintados à
    /// mão, em tinta spray, na parede do fundo do salão do bunker, à direita do painel de armas. "FUNDADORES" em vermelho
    /// em cima e os nomes em branco embaixo, em colunas, cada um um pouco torto, com as bordas ásperas e uns escorridos.
    /// Os nomes ficam em <c>Assets/_Game/Settings/Fundadores.txt</c> (um por linha; linhas vazias e as que começam com #
    /// não contam). Menu: Zombie City Cleaner > Escrever os fundadores no bunker. Ela refaz a imagem
    /// (<c>Art/Decals/Fundadores.png</c>), o material e o objeto "Fundadores (bunker)" na cena aberta (a Demo_Cidade) e
    /// salva a cena. Depois, rode a montagem da cena de vídeo, para a Video.unity ganhar a parede nova.
    /// </summary>
    public static class MontagemDosFundadores
    {
        const string Lista = "Assets/_Game/Settings/Fundadores.txt";
        const string Imagem = "Assets/_Game/Art/Decals/Fundadores.png";
        const string Material = "Assets/_Game/Art/Materials/Fundadores.mat";
        const string Raiz = "Fundadores (bunker)";

        // a parede: 3 × 2,25 m, nas coordenadas da casa da família (as da PlantaDoBunker): entre o painel de armas (que
        // acaba em x = -3,5) e o canto (x = 0), de 1,4 m a 3,65 m do chão (-5,85), 1 cm na frente da parede do fundo
        // (a face de dentro em z = 25,7)
        const float Largura = 3f, Altura = 2.25f;
        static readonly Vector3 MeioNaCasa = new Vector3(-1.75f, -5.85f + 1.4f + Altura / 2f, 25.69f);
        const int PixelsX = 2048, PixelsY = 1536;

        // as tintas (sRGB): o branco encardido dos nomes e o vermelho do título
        static readonly Color Branco = new Color(0.88f, 0.87f, 0.82f), Vermelho = new Color(0.62f, 0.08f, 0.06f);

        [MenuItem("Zombie City Cleaner/Escrever os fundadores no bunker")]
        public static void Montar()
        {
            var nomes = LerNomes();
            if (nomes.Count == 0)
            {
                Debug.LogWarning($"[fundadores] nenhum nome em {Lista}");
                return;
            }
            var casa = GameObject.Find("Cidade")?.transform.Find("Casa da família");
            if (casa == null)
            {
                Debug.LogError("[fundadores] abra a Demo_Cidade (falta a casa da família)");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Imagem));
            File.WriteAllBytes(Imagem, Pintar(nomes).EncodeToPNG());
            AssetDatabase.ImportAsset(Imagem);
            var importador = (TextureImporter)AssetImporter.GetAtPath(Imagem);
            importador.alphaIsTransparency = true;
            importador.sRGBTexture = true;
            importador.mipmapEnabled = true;
            importador.mipMapsPreserveCoverage = true;   // o recorte da tinta não some de longe
            importador.alphaTestReferenceValue = 0.5f;
            importador.wrapMode = TextureWrapMode.Clamp;
            importador.maxTextureSize = 2048;
            importador.SaveAndReimport();

            var cena = EditorSceneManager.GetActiveScene();
            foreach (var r in cena.GetRootGameObjects())
                if (r.name == Raiz) Object.DestroyImmediate(r);
            var raiz = new GameObject(Raiz);
            var parede = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(parede.GetComponent<Collider>());
            parede.name = "Parede dos fundadores";
            parede.transform.SetParent(raiz.transform, false);
            // o quad olha para -z: na casa, para dentro do salão
            parede.transform.SetPositionAndRotation(casa.TransformPoint(MeioNaCasa), casa.rotation);
            parede.transform.localScale = new Vector3(Largura, Altura, 1f);
            var desenho = parede.GetComponent<MeshRenderer>();
            desenho.sharedMaterial = MaterialDaTinta();
            desenho.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(parede, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"[fundadores] {nomes.Count} nomes na parede do bunker ({cena.path})");
        }

        static List<string> LerNomes()
        {
            var nomes = new List<string>();
            if (!File.Exists(Lista)) return nomes;
            foreach (var linha in File.ReadAllLines(Lista, System.Text.Encoding.UTF8))
            {
                string nome = linha.Trim();
                if (nome.Length == 0 || nome.StartsWith("#")) continue;
                nomes.Add(nome.Length > 26 ? nome.Substring(0, 26) : nome);
            }
            return nomes;
        }

        static Material MaterialDaTinta()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Material);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Fundadores" };
                AssetDatabase.CreateAsset(material, Material);
            }
            // a tinta é recortada (sem transparência: as bordas ásperas ficam nítidas), fosca e recebe a luz do bunker
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Imagem));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness", 0.12f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // ------------------------------------------------------------------ a pintura

        /// <summary>
        /// A imagem da parede: as letras desenhadas pela fonte da Unity (TextMesh, numa câmera à parte, o título em
        /// vermelho e os nomes em branco), e depois a tinta: as bordas ásperas (o ruído no alfa, que o material recorta
        /// em 0,5) e os escorridos que descem das letras.
        /// </summary>
        static Texture2D Pintar(List<string> nomes)
        {
            var cobertura = Letras(nomes);
            var sorteio = new System.Random(nomes.Count * 7919 + string.Join("|", nomes).GetHashCode());
            var tinta = new float[PixelsX * PixelsY];
            var cor = new byte[PixelsX * PixelsY];   // 0 nada, 1 branco, 2 vermelho
            for (int y = 0; y < PixelsY; y++)
                for (int x = 0; x < PixelsX; x++)
                {
                    var c = cobertura[y * PixelsX + x];
                    float titulo = Mathf.Clamp01((c.r - c.g) / 255f), nome = c.g / 255f;
                    float a = Mathf.Max(titulo, nome);
                    if (a <= 0f) continue;
                    // o spray: a borda come aos poucos (dois ruídos, um largo e um fino)
                    float n = 0.6f * Mathf.PerlinNoise(x * 0.06f + 11.3f, y * 0.06f + 4.7f) + 0.4f * Mathf.PerlinNoise(x * 0.3f + 2.1f, y * 0.3f + 9.9f);
                    tinta[y * PixelsX + x] = a * (0.62f + 0.55f * n);
                    cor[y * PixelsX + x] = (byte)(titulo > nome ? 2 : 1);
                }
            Escorrer(tinta, cor, sorteio);

            var imagem = new Texture2D(PixelsX, PixelsY, TextureFormat.RGBA32, true);
            var pixels = new Color32[PixelsX * PixelsY];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (cor[i] == 0) continue;
                var baseDaTinta = cor[i] == 2 ? Vermelho : Branco;
                int x = i % PixelsX, y = i / PixelsX;
                float variacao = 0.92f + 0.12f * Mathf.PerlinNoise(x * 0.02f + 5f, y * 0.02f + 3f);   // a tinta mais grossa em uns lugares
                var final = baseDaTinta * variacao;
                pixels[i] = new Color(final.r, final.g, final.b, Mathf.Clamp01(tinta[i]));
            }
            imagem.SetPixels32(pixels);
            imagem.Apply();
            return imagem;
        }

        // embaixo das letras, umas colunas de tinta descem: um escorrido fino, de 1 a 7 cm, que afina e termina numa gota
        static void Escorrer(float[] tinta, byte[] cor, System.Random sorteio)
        {
            for (int x = 0; x < PixelsX; x += 2)
                for (int y = 1; y < PixelsY; y++)
                {
                    int i = y * PixelsX + x;
                    // a borda de baixo de um traço (a imagem começa embaixo: y - 1 é o pixel de baixo)
                    if (tinta[i] < 0.5f || tinta[i - PixelsX] >= 0.5f || sorteio.NextDouble() > 0.045) continue;
                    int comprimento = 8 + (int)(sorteio.NextDouble() * sorteio.NextDouble() * 45);
                    float largura = 1.2f + (float)sorteio.NextDouble() * 1.6f;
                    for (int k = 1; k <= comprimento && y - k >= 0; k++)
                    {
                        float f = k / (float)comprimento;
                        float meia = largura * (1f - 0.5f * f) + (k > comprimento - 3 ? 1f : 0f);   // a gota da ponta
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int px = x + dx;
                            if (px < 0 || px >= PixelsX || Mathf.Abs(dx) > meia) continue;
                            int j = (y - k) * PixelsX + px;
                            if (tinta[j] < 0.9f)
                            {
                                tinta[j] = 0.9f;
                                cor[j] = cor[i];
                            }
                        }
                    }
                }
        }

        /// <summary>
        /// As letras, cobertura de 0 a 255: o vermelho é o título e os nomes; o verde, só os nomes. Uma câmera ortográfica
        /// de 1 unidade por pixel, longe de tudo, desenha os TextMesh numa textura.
        /// </summary>
        static Color32[] Letras(List<string> nomes)
        {
            const int Camada = 31;
            var longe = new Vector3(0f, -9000f, 0f);
            var objetos = new List<GameObject>();
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            float pxPorMetro = PixelsX / Largura;

            GameObject Texto(string texto, Color tom, Vector2 meio, float alturaPx, float larguraMaxima, float giro)
            {
                var objeto = new GameObject("letras") { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
                var malha = objeto.AddComponent<TextMesh>();
                malha.font = fonte;
                malha.fontSize = 128;
                malha.fontStyle = FontStyle.Bold;
                malha.characterSize = 1f;
                malha.anchor = TextAnchor.MiddleCenter;
                malha.alignment = TextAlignment.Center;
                malha.color = tom;
                malha.text = texto;
                var desenho = objeto.GetComponent<MeshRenderer>();
                desenho.sharedMaterial = fonte.material;
                // a altura das maiúsculas pela caixa do texto; estreita, se não couber na largura da coluna
                var caixa = desenho.bounds;
                float escala = alturaPx / Mathf.Max(1f, caixa.size.y);
                if (caixa.size.x * escala > larguraMaxima) escala = larguraMaxima / caixa.size.x;
                objeto.transform.localScale = Vector3.one * escala;
                objeto.transform.SetPositionAndRotation(longe + new Vector3(meio.x - PixelsX / 2f, meio.y - PixelsY / 2f, 0f), Quaternion.Euler(0f, 0f, giro));
                objetos.Add(objeto);
                return objeto;
            }

            var sorteio = new System.Random(31 + nomes.Count);
            float R(float a, float b) => a + (float)sorteio.NextDouble() * (b - a);
            // o título, em cima
            Texto("FUNDADORES", Color.red, new Vector2(PixelsX / 2f, PixelsY - 0.2f * pxPorMetro), 0.24f * pxPorMetro, PixelsX * 0.9f, R(-1.5f, 1.5f));
            // os nomes, em colunas (até 10 por coluna), de cima para baixo e da esquerda para a direita
            int colunas = Mathf.Clamp(Mathf.CeilToInt(nomes.Count / 10f), 1, 4);
            int porColuna = Mathf.CeilToInt(nomes.Count / (float)colunas);
            float topo = PixelsY - 0.42f * pxPorMetro, fundo = 0.06f * pxPorMetro;
            float passo = (topo - fundo) / Mathf.Max(porColuna, 8);
            float larguraDaColuna = PixelsX * 0.94f / colunas;
            for (int i = 0; i < nomes.Count; i++)
            {
                int coluna = i / porColuna, linha = i % porColuna;
                var meio = new Vector2(PixelsX * 0.03f + larguraDaColuna * (coluna + 0.5f) + R(-12f, 12f), topo - passo * (linha + 0.5f) + R(-5f, 5f));
                Texto(nomes[i], Color.white, meio, passo * R(0.64f, 0.72f), larguraDaColuna * 0.92f, R(-3f, 3f));
            }

            var camera = new GameObject("câmera das letras") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = PixelsY / 2f;
            camera.aspect = PixelsX / (float)PixelsY;
            camera.transform.SetPositionAndRotation(longe + new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.cullingMask = 1 << Camada;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var dados = camera.GetUniversalAdditionalCameraData();
            dados.SetRenderer(1);   // o renderizador sem a neblina de altura (o dos braços)
            dados.renderPostProcessing = false;
            dados.antialiasing = AntialiasingMode.None;
            var alvo = new RenderTexture(PixelsX, PixelsY, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = alvo;
            camera.Render();
            camera.targetTexture = null;
            var ativa = RenderTexture.active;
            RenderTexture.active = alvo;
            var lida = new Texture2D(PixelsX, PixelsY, TextureFormat.RGBA32, false);
            lida.ReadPixels(new Rect(0, 0, PixelsX, PixelsY), 0, 0);
            lida.Apply();
            RenderTexture.active = ativa;
            var pixels = lida.GetPixels32();

            Object.DestroyImmediate(lida);
            alvo.Release();
            Object.DestroyImmediate(alvo);
            Object.DestroyImmediate(camera.gameObject);
            foreach (var o in objetos) Object.DestroyImmediate(o);
            return pixels;
        }
    }
}
