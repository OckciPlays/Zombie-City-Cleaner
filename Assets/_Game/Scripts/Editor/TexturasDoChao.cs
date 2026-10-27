using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As texturas do chão do mundo gerado (Plano_Mapa, Parte M2; o TerrainTextureGenerator do Guardiões da Floresta, nos
    /// tons dessaturados do clima daqui) e os materiais do chão e da água:
    /// Os tons médios seguem as cores do chão da paleta da cidade (GramaSeca, Terra, Entulho, FolhaMorta), para o terreno
    /// combinar com as calçadas, os lotes e o resto do cenário.
    ///  - T_Chao_Grama: a grama seca, palha e oliva, em tufos e pinceladas;
    ///  - T_Chao_Terra: a terra escura, com rachaduras e pedrinhas;
    ///  - T_Chao_Cascalho: as pedras cinza soltas (a serra, a volta da partida e, depois, as estradas de terra);
    ///  - T_Chao_Folhas: as folhas mortas e os gravetos sobre a terra;
    ///  - T_Chao_Rocha: a pedra em camadas com rachaduras e líquen (as encostas das serras; vai de lado).
    /// Todas repetem sem emenda (ruído periódico e desenhos que dão a volta na borda). O ALFA guarda o relevo (tufos,
    /// pedras e folhas altas), que o shader ZombieCityCleaner/Chao usa para recortar as transições.
    /// Menu Zombie City Cleaner > Mundo > Gerar as texturas do chão. Saída em Art/Mundo/ (Chao.mat e Agua.mat) e
    /// Art/Mundo/Chao/ (as texturas). A montagem da cena do mundo chama, se os materiais ainda não existirem.
    /// </summary>
    public static class TexturasDoChao
    {
        public const string Pasta = "Assets/_Game/Art/Mundo";
        public const string PastaDasTexturas = Pasta + "/Chao";
        public const string MaterialDoChao = Pasta + "/Chao.mat";
        public const string MaterialDaAgua = Pasta + "/Agua.mat";
        const int Lado = 512;

        [MenuItem("Zombie City Cleaner/Mundo/Gerar as texturas do chão")]
        public static void Gerar()
        {
            Directory.CreateDirectory(PastaDasTexturas);
            var grama = Salvar(Grama(), "T_Chao_Grama");
            var terra = Salvar(Terra(), "T_Chao_Terra");
            var cascalho = Salvar(Cascalho(), "T_Chao_Cascalho");
            var folhas = Salvar(Folhas(), "T_Chao_Folhas");
            var rocha = Salvar(Rocha(), "T_Chao_Rocha");

            var chao = Material(MaterialDoChao, "ZombieCityCleaner/Chao");
            chao.SetTexture("_GramaTex", grama);
            chao.SetTexture("_TerraTex", terra);
            chao.SetTexture("_CascalhoTex", cascalho);
            chao.SetTexture("_FolhasTex", folhas);
            chao.SetTexture("_RochaTex", rocha);
            EditorUtility.SetDirty(chao);
            Material(MaterialDaAgua, "ZombieCityCleaner/Agua");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mundo] Texturas do chão geradas em {PastaDasTexturas}; materiais {MaterialDoChao} e {MaterialDaAgua}.");
        }

        static Material Material(string caminho, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.shader = Shader.Find(shader);
            m.enableInstancing = true;
            return m;
        }

        // ------------------------------------------------------------------ as texturas

        static Color[] Grama()
        {
            var px = new Color[Lado * Lado];
            Color escuro = new Color(0.33f, 0.33f, 0.24f), meio = new Color(0.41f, 0.4f, 0.29f), claro = new Color(0.5f, 0.48f, 0.35f);
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float n = Fbm(x, y, 4, 7.1f);
                float fino = Fbm(x, y, 32, 2.3f);
                Color c = n < 0.5f ? Color.Lerp(escuro, meio, n * 2f) : Color.Lerp(meio, claro, (n - 0.5f) * 2f);
                c *= 0.9f + fino * 0.2f;
                c.a = 0.3f + fino * 0.2f;
                px[y * Lado + x] = c;
            }
            // os tufos: traços curtos, a palha clara (alta) e o oliva escuro (baixo), uns poucos ainda esverdeados
            var rnd = new System.Random(21);
            for (int k = 0; k < 6200; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float comp = 4f + (float)rnd.NextDouble() * 8f;
                double tipo = rnd.NextDouble();
                Color c = tipo < 0.5 ? Color.Lerp(claro, new Color(0.58f, 0.54f, 0.38f), (float)rnd.NextDouble())
                        : tipo < 0.85 ? Color.Lerp(escuro, new Color(0.26f, 0.28f, 0.2f), (float)rnd.NextDouble())
                        : new Color(0.31f, 0.34f, 0.25f);
                c.a = tipo < 0.5 ? 0.85f : 0.35f;
                Traco(px, p, ang, comp, 1.1f, c, 0.75f);
            }
            return px;
        }

        static Color[] Terra()
        {
            var px = new Color[Lado * Lado];
            Color escuro = new Color(0.27f, 0.22f, 0.18f), meio = new Color(0.35f, 0.29f, 0.24f), claro = new Color(0.43f, 0.36f, 0.29f);
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float n = Fbm(x, y, 4, 3.3f);
                float fino = Fbm(x, y, 48, 8.8f);
                Color c = n < 0.5f ? Color.Lerp(escuro, meio, n * 2f) : Color.Lerp(meio, claro, (n - 0.5f) * 2f);
                c *= 0.88f + fino * 0.24f;
                // as rachaduras: vales estreitos de um ruído "dobrado"
                float r = Mathf.Abs(Fbm(x, y, 8, 5.5f) - 0.5f);
                if (r < 0.018f) c = Color.Lerp(c, escuro * 0.65f, 0.7f);
                c.a = 0.3f + fino * 0.15f;
                px[y * Lado + x] = c;
            }
            var rnd = new System.Random(5);
            for (int k = 0; k < 300; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float r = 1.5f + (float)rnd.NextDouble() * 3f;
                float g = 0.4f + (float)rnd.NextDouble() * 0.12f;
                var pedra = new Color(g * 1.04f, g * 0.95f, g * 0.84f, 0.9f);
                Disco(px, p + new Vector2(0.7f, -1f), r, new Color(0.2f, 0.16f, 0.13f, 0.4f));   // a sombra
                Disco(px, p, r, pedra);
                Disco(px, p + new Vector2(-r * 0.3f, r * 0.3f), r * 0.4f, Color.Lerp(pedra, Color.white, 0.1f));
            }
            return px;
        }

        static Color[] Cascalho()
        {
            var px = new Color[Lado * Lado];
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float n = Fbm(x, y, 8, 4.7f);
                var c = Color.Lerp(new Color(0.33f, 0.32f, 0.3f), new Color(0.4f, 0.39f, 0.36f), n);
                c.a = 0.1f;
                px[y * Lado + x] = c;
            }
            // as pedras soltas, em camadas (as de cima mais altas), cinza com um pouco de quente ou de frio
            var rnd = new System.Random(31);
            for (int k = 0; k < 2600; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float r = 1.5f + (float)rnd.NextDouble() * 3.2f;
                float g = 0.38f + (float)rnd.NextDouble() * 0.16f;
                float tom = (float)rnd.NextDouble() - 0.5f;
                var pedra = new Color(g * (1f + tom * 0.12f), g, g * (1f - tom * 0.12f), 0.4f + k / 2600f * 0.55f);
                Disco(px, p + new Vector2(0.8f, -1.1f), r, new Color(0.22f, 0.21f, 0.19f, pedra.a - 0.25f));
                Disco(px, p, r, pedra);
                Disco(px, p + new Vector2(-r * 0.35f, r * 0.35f), r * 0.35f, Color.Lerp(pedra, Color.white, 0.08f));
            }
            return px;
        }

        static Color[] Folhas()
        {
            var px = new Color[Lado * Lado];
            Color solo = new Color(0.22f, 0.18f, 0.14f), solo2 = new Color(0.29f, 0.24f, 0.18f);
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                var c = Color.Lerp(solo, solo2, Fbm(x, y, 8, 6.6f));
                c.a = 0.15f;
                px[y * Lado + x] = c;
            }
            var rnd = new System.Random(77);
            // os gravetos
            for (int k = 0; k < 90; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                Traco(px, p, ang, 18f + (float)rnd.NextDouble() * 26f, 1.4f, new Color(0.36f, 0.28f, 0.2f, 0.6f), 1f);
            }
            // as folhas secas, em tons apagados, em camadas
            Color[] tons =
            {
                new Color(0.47f, 0.36f, 0.23f), new Color(0.53f, 0.41f, 0.27f), new Color(0.4f, 0.3f, 0.2f),
                new Color(0.44f, 0.38f, 0.27f), new Color(0.36f, 0.34f, 0.24f), new Color(0.56f, 0.46f, 0.3f),
            };
            for (int k = 0; k < 1400; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float comp = 9f + (float)rnd.NextDouble() * 9f;
                var c = tons[rnd.Next(tons.Length)] * (0.85f + (float)rnd.NextDouble() * 0.3f);
                c.a = 0.5f + k / 1400f * 0.45f;   // as de cima ficam "mais altas"
                Folha(px, p + new Vector2(1f, -1.5f), ang, comp, new Color(0.14f, 0.11f, 0.08f, c.a - 0.15f));   // a sombra
                Folha(px, p, ang, comp, c);
            }
            return px;
        }

        static Color[] Rocha()
        {
            var px = new Color[Lado * Lado];
            Color escuro = new Color(0.33f, 0.33f, 0.32f), meio = new Color(0.43f, 0.42f, 0.4f), claro = new Color(0.53f, 0.52f, 0.49f);
            Color quente = new Color(0.47f, 0.42f, 0.36f);
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float n = Fbm(x, y, 4, 4.4f);
                float fino = Fbm(x, y, 48, 9.1f);
                // as camadas horizontais onduladas (a textura vai de lado nas encostas: y é a altura)
                float camada = Mathf.Sin((y + Fbm(x, y, 2, 2.2f) * 70f) / Lado * Mathf.PI * 2f * 9f) * 0.5f + 0.5f;
                Color c = n < 0.5f ? Color.Lerp(escuro, meio, n * 2f) : Color.Lerp(meio, claro, (n - 0.5f) * 2f);
                c = Color.Lerp(c, quente, Mathf.Clamp01(camada - 0.55f) * 0.7f);
                c *= 0.88f + fino * 0.24f;
                float r = Mathf.Abs(Fbm(x, y, 6, 1.3f) - 0.5f);
                if (r < 0.016f) c = Color.Lerp(c, escuro * 0.6f, 0.75f);
                if (camada < 0.06f) c *= 0.75f;
                c.a = 0.45f + camada * 0.25f + fino * 0.1f;
                px[y * Lado + x] = c;
            }
            var rnd = new System.Random(53);
            for (int k = 0; k < 140; k++)
            {
                var p = new Vector2(rnd.Next(Lado), rnd.Next(Lado));
                float r = 2f + (float)rnd.NextDouble() * 5f;
                var liquen = rnd.NextDouble() < 0.5 ? new Color(0.42f, 0.44f, 0.3f, 0.55f) : new Color(0.3f, 0.34f, 0.22f, 0.55f);
                Disco(px, p, r, liquen);
            }
            return px;
        }

        // ------------------------------------------------------------------ as primitivas (tudo dá a volta na borda)

        // ruído fractal periódico (0..1): repete a cada Lado pixels
        static float Fbm(int x, int y, int periodoBase, float semente)
        {
            float soma = 0f, amp = 0.5f, norma = 0f;
            int periodo = periodoBase;
            for (int o = 0; o < 4; o++)
            {
                soma += RuidoPeriodico(x * periodo / (float)Lado, y * periodo / (float)Lado, periodo, semente + o * 13.7f) * amp;
                norma += amp;
                amp *= 0.5f;
                periodo *= 2;
            }
            return soma / norma;
        }

        static float RuidoPeriodico(float x, float y, int periodo, float semente)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, periodo, semente), b = Hash(x0 + 1, y0, periodo, semente);
            float c = Hash(x0, y0 + 1, periodo, semente), d = Hash(x0 + 1, y0 + 1, periodo, semente);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int periodo, float semente)
        {
            x = ((x % periodo) + periodo) % periodo;
            y = ((y % periodo) + periodo) % periodo;
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + semente * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static void Ponto(Color[] px, int x, int y, Color c, float a)
        {
            x = ((x % Lado) + Lado) % Lado;
            y = ((y % Lado) + Lado) % Lado;
            int i = y * Lado + x;
            var antes = px[i];
            var cor = Color.Lerp(antes, c, a);
            cor.a = Mathf.Lerp(antes.a, c.a, a);
            px[i] = cor;
        }

        static void Disco(Color[] px, Vector2 c, float r, Color cor, float forca = 1f)
        {
            for (int y = Mathf.FloorToInt(c.y - r - 1); y <= Mathf.CeilToInt(c.y + r + 1); y++)
            for (int x = Mathf.FloorToInt(c.x - r - 1); x <= Mathf.CeilToInt(c.x + r + 1); x++)
            {
                float a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x, y), c) + 0.5f) * forca;
                if (a > 0f) Ponto(px, x, y, cor, a);
            }
        }

        static void Traco(Color[] px, Vector2 p, float angulo, float comp, float largura, Color c, float forca)
        {
            var d = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));
            int passos = Mathf.CeilToInt(comp * 2f);
            for (int i = 0; i <= passos; i++)
            {
                float t = i / (float)passos;
                Disco(px, p + d * (t * comp), largura * (1f - t * 0.6f), c, forca);
            }
        }

        // a folha: um losango comprido com a nervura do meio mais escura
        static void Folha(Color[] px, Vector2 c, float angulo, float comp, Color cor)
        {
            var d = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));
            for (float t = -0.5f; t <= 0.5f; t += 0.03f)
                Disco(px, c + d * (t * comp), (0.5f - Mathf.Abs(t)) * comp * 0.5f, cor);
            var nervura = cor * 0.7f;
            nervura.a = cor.a;
            for (float t = -0.45f; t <= 0.45f; t += 0.05f) Disco(px, c + d * (t * comp), 0.5f, nervura, 0.6f);
        }

        static Texture2D Salvar(Color[] px, string nome)
        {
            string caminho = $"{PastaDasTexturas}/{nome}.png";
            var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.alphaIsTransparency = false;
            imp.sRGBTexture = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }
    }
}
