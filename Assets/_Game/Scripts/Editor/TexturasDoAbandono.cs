using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As texturas e o material do abandono das cidades (Plano_Mapa, Parte M9b; a cena de testes, para o usuário aprovar
    /// antes de ir para o jogo), para o shader ZombieCityCleaner/Abandono:
    ///  - T_Abandono_Ruido (256 px, linear, repete sem emenda): R e G, manchas em duas escalas; B, as rachaduras (as bordas
    ///    das células de Voronoi, finas e tortas, umas mais fortes); A, os escorridos (um ruído fino na horizontal e largo
    ///    na vertical, que o shader estica ainda mais).
    ///  - T_Abandono_Categorias (16 px, uma por quadradinho da Paleta.png, sem filtro): no R, o tipo da superfície de cada
    ///    cor × 25 (<see cref="Categorias"/>): a pintura de fora, a alvenaria e as calçadas, o asfalto, o telhado, o metal
    ///    (os carros, os postes, as placas), a madeira, o vidro e o que é de dentro.
    ///  - Abandono.mat: o material da cidade com o abandono (a paleta e as duas texturas);
    ///  - Pichacao.mat: o das pichações (shader ZombieCityCleaner/Pichacao), com o atlas T_Pichacoes (copiado da pasta de
    ///    arte, Modelo 3D/Cidade/Pichacoes.png, feito pelo cidade_pichacoes.py) e o ruído;
    ///  - os do mundo (Parte M9c, <see cref="MateriaisDoMundo"/>): cópias do Abandono.mat (as forças escolhidas na cena de
    ///    testes) no grau muito, uma para cada jeito de achar a altura sobre o chão.
    /// Menu Zombie City Cleaner > Exemplos > Gerar as texturas do abandono; a montagem da cena do abandono chama, se faltar.
    /// </summary>
    public static class TexturasDoAbandono
    {
        public const string Pasta = "Assets/_Game/Art/Mundo/Abandono";
        public const string MaterialDoAbandono = Pasta + "/Abandono.mat";
        public const string MaterialDasPichacoes = Pasta + "/Pichacao.mat";
        public const string MaterialDoMundo = Pasta + "/AbandonoDoMundo.mat";
        public const string MaterialDasCasas = Pasta + "/AbandonoDasCasas.mat";
        public const string MaterialDosObjetos = Pasta + "/AbandonoDosObjetos.mat";
        const string ArquivoDoAtlas = Pasta + "/T_Pichacoes.png";
        const int Lado = 256;

        /// <summary>O tipo de cada cor da paleta (0 = nenhum: só um pouco de sujeira).</summary>
        static readonly (int tipo, string[] cores)[] Categorias =
        {
            (1, new[] { "ParedeVerde", "ParedeBege", "ParedeAzul", "ParedeVerdeEscura", "ParedeBegeEscura", "ParedeAzulEscura", "Batente", "Cerca", "Rodape" }),
            (2, new[] { "Tijolo", "TijoloEscuro", "Concreto", "ConcretoClaro", "Calcada", "MeioFio", "Bunker", "Barreira", "Entulho" }),
            (3, new[] { "Asfalto", "AsfaltoRemendo", "Faixa", "Oleo" }),
            (4, new[] { "Telhado", "TelhadoAzul", "TelhadoEscuro", "TelhadoAzulEscuro", "Cumeeira" }),
            (5, new[] { "Metal", "Poste", "Cromo", "Grade", "CarroAzul", "CarroVinho", "CarroCinza", "CarroVerde", "CarroBege", "CarroBranco",
                        "Lixeira", "Cacamba", "Hidrante", "PlacaVermelha", "PlacaBranca", "PlacaVerde", "PlacaAmarela" }),
            (6, new[] { "Madeira", "MadeiraVelha", "BancoMadeira", "Papelao", "Compensado" }),
            (7, new[] { "Vidro", "VidroCarro", "VidroSujo", "Farol" }),
            (8, new[] { "ParedeInterna", "PapelVerde", "PapelRosa", "Azulejo", "Piso", "Carpete", "Taco", "Teto" }),
        };

        [MenuItem("Zombie City Cleaner/Exemplos/Gerar as texturas do abandono")]
        public static Material Gerar()
        {
            Directory.CreateDirectory(Pasta);
            var ruido = Salvar(Ruido(), Lado, "T_Abandono_Ruido", true);
            var categorias = Salvar(CategoriasDaPaleta(), 16, "T_Abandono_Categorias", false);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDoAbandono);
            if (m == null)
            {
                m = new Material(Shader.Find("ZombieCityCleaner/Abandono"));
                AssetDatabase.CreateAsset(m, MaterialDoAbandono);
            }
            m.shader = Shader.Find("ZombieCityCleaner/Abandono");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Paleta.png"));
            m.SetTexture("_Categorias", categorias);
            m.SetTexture("_Ruido", ruido);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Pichacoes();
            MateriaisDoMundo();
            Debug.Log($"[Abandono] Texturas e materiais gerados em {Pasta}.");
            return m;
        }

        /// <summary>
        /// Os materiais do mundo (Parte M9c), refeitos a partir do Abandono.mat, no grau muito: o das ruas, das estradas e
        /// das caixas de longe (sem a base: a altura é o y menos -10000), o das casas de verdade (a altura sobre o chão na cor
        /// do vértice) e o dos objetos de rua (o y do objeto, que tem a origem no chão).
        /// </summary>
        public static void MateriaisDoMundo()
        {
            var fonte = AssetDatabase.LoadAssetAtPath<Material>(MaterialDoAbandono);
            if (fonte == null)
            {
                Gerar();   // (que chama este no fim)
                return;
            }
            Copia(fonte, MaterialDoMundo, 0f);
            Copia(fonte, MaterialDasCasas, 1f);
            Copia(fonte, MaterialDosObjetos, 2f);
            AssetDatabase.SaveAssets();
        }

        static void Copia(Material fonte, string caminho, float modoDaAltura)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(fonte);
                AssetDatabase.CreateAsset(m, caminho);
            }
            else
            {
                m.shader = fonte.shader;
                m.CopyPropertiesFromMaterial(fonte);
            }
            m.SetFloat("_Intensidade", AbandonoDaObra.NoMundo);
            m.SetFloat("_Chao", -10000f);
            m.SetFloat("_ModoDaAltura", modoDaAltura);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        /// <summary>A importação do atlas das pichações e o material delas (null, sem o atlas).</summary>
        public static Material Pichacoes()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ArquivoDoAtlas);
            if (imp == null)
            {
                Debug.LogWarning($"[Abandono] Falta o atlas das pichações ({ArquivoDoAtlas}): copie o Pichacoes.png da pasta de arte.");
                return null;
            }
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            var shader = Shader.Find("ZombieCityCleaner/Pichacao");
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDasPichacoes);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, MaterialDasPichacoes);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArquivoDoAtlas));
            m.SetTexture("_Ruido", AssetDatabase.LoadAssetAtPath<Texture2D>(Pasta + "/T_Abandono_Ruido.png"));
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ------------------------------------------------------------------ o ruído

        static Color[] Ruido()
        {
            var px = new Color[Lado * Lado];
            // as sementes dos pontos de Voronoi (as rachaduras): 9 × 9 células, que dão a volta na borda
            const int celulas = 9;
            var pontos = new Vector2[celulas, celulas];
            var rnd = new System.Random(31);
            for (int j = 0; j < celulas; j++)
            for (int i = 0; i < celulas; i++)
                pontos[i, j] = new Vector2(i + 0.15f + 0.7f * (float)rnd.NextDouble(), j + 0.15f + 0.7f * (float)rnd.NextDouble());
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float r = Fbm(x, y, 4, 4, 1.7f);
                float g = Fbm(x, y, 3, 3, 9.3f);
                // a borda da célula: F2 - F1 pequeno; torta pelo ruído; uns trechos mais fortes que outros
                float tx = x + (Fbm(x, y, 8, 8, 4.4f) - 0.5f) * 14f, ty = y + (Fbm(x, y, 8, 8, 6.1f) - 0.5f) * 14f;
                Vector2 q = new Vector2(tx, ty) * (celulas / (float)Lado);
                float f1 = float.MaxValue, f2 = float.MaxValue;
                int ci = Mathf.FloorToInt(q.x), cj = Mathf.FloorToInt(q.y);
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int ii = ci + di, jj = cj + dj;
                    var s = pontos[((ii % celulas) + celulas) % celulas, ((jj % celulas) + celulas) % celulas];
                    s += new Vector2(ii - (((ii % celulas) + celulas) % celulas), jj - (((jj % celulas) + celulas) % celulas));
                    float d = (q - s).magnitude;
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }
                float borda = 1f - Mathf.Clamp01((f2 - f1) / 0.05f);
                float forca = Mathf.Clamp01((Fbm(x, y, 4, 4, 2.2f) - 0.35f) * 2.5f);
                float b = borda * borda * forca;
                // os escorridos: fino na horizontal (32 colunas) e largo na vertical (2)
                float a = Fbm(x, y, 32, 2, 5.9f);
                px[y * Lado + x] = new Color(r, g, b, a);
            }
            return px;
        }

        static float Fbm(int x, int y, int periodoX, int periodoY, float semente)
        {
            float soma = 0f, amp = 0.5f, norma = 0f;
            int px = periodoX, py = periodoY;
            for (int o = 0; o < 4; o++)
            {
                soma += RuidoPeriodico(x * px / (float)Lado, y * py / (float)Lado, px, py, semente + o * 13.7f) * amp;
                norma += amp;
                amp *= 0.5f;
                px *= 2;
                py *= 2;
            }
            return soma / norma;
        }

        static float RuidoPeriodico(float x, float y, int periodoX, int periodoY, float semente)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, periodoX, periodoY, semente), b = Hash(x0 + 1, y0, periodoX, periodoY, semente);
            float c = Hash(x0, y0 + 1, periodoX, periodoY, semente), d = Hash(x0 + 1, y0 + 1, periodoX, periodoY, semente);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int periodoX, int periodoY, float semente)
        {
            x = ((x % periodoX) + periodoX) % periodoX;
            y = ((y % periodoY) + periodoY) % periodoY;
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + semente * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ------------------------------------------------------------------ as categorias

        static Color[] CategoriasDaPaleta()
        {
            var px = new Color[16 * 16];
            foreach (var (tipo, cores) in Categorias)
                foreach (var nome in cores)
                {
                    Vector2 uv;
                    try { uv = Paleta.UV(nome); }
                    catch (System.Collections.Generic.KeyNotFoundException) { continue; }
                    int i = Mathf.FloorToInt(uv.x * 16f), j = Mathf.FloorToInt(uv.y * 16f);
                    px[j * 16 + i] = new Color(tipo * 25f / 255f, 0f, 0f, 1f);
                }
            return px;
        }

        static Texture2D Salvar(Color[] px, int lado, string nome, bool filtro)
        {
            string caminho = $"{Pasta}/{nome}.png";
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = filtro ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = filtro ? FilterMode.Bilinear : FilterMode.Point;
            imp.mipmapEnabled = filtro;
            imp.alphaIsTransparency = false;
            imp.sRGBTexture = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }
    }
}
