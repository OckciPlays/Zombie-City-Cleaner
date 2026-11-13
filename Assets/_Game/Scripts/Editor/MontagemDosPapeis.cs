using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Documentos;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O atlas e o material dos papéis que se leem (o Plano_Hospital.md da pasta de arte, a parte 6, "A narrativa, os
    /// documentos"): <c>Resources/Documentos/T_Papeis.png</c> (1024 × 1024, os retângulos do <see cref="AtlasDosPapeis"/>),
    /// desenhado aqui, sem imagem de fora: o papel velho (o amarelado das beiradas, as manchas de café e de água, a dobra e os
    /// pontinhos de sujeira), as linhas de texto de mentira (as de máquina, as de mão e o pincel atômico), o timbre com a
    /// cruz, a faixa vermelha do aviso, a tabela do formulário, as páginas pautadas do caderno e do bilhete, a pasta parda com
    /// a etiqueta e o carimbo, o eucatex da prancheta, o pano da capa, o metal do prendedor e a fita crepe. O texto de
    /// verdade só aparece na folha da tela. E o <c>M_Papeis.mat</c> (o Simple Lit da URP, fosco, sem brilho).
    /// </summary>
    public static class MontagemDosPapeis
    {
        public const string Pasta = "Assets/_Game/Resources/Documentos";
        const int L = AtlasDosPapeis.Lado;

        static Color[] _p;

        [MenuItem("Zombie City Cleaner/Documentos/Montar os papéis (o atlas e o material)")]
        public static void Montar()
        {
            Directory.CreateDirectory(Pasta);
            string caminho = Pasta + "/T_Papeis.png";
            File.WriteAllBytes(caminho, Desenhar().EncodeToPNG());
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 1024;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.isReadable = false;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);

            string cm = Pasta + "/M_Papeis.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(cm);
            var sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (mat == null)
            {
                mat = new Material(sh) { name = "M_Papeis" };
                AssetDatabase.CreateAsset(mat, cm);
            }
            else mat.shader = sh;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.DisableKeyword("_SPECULAR_COLOR");
            mat.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[Documentos] O atlas e o material dos papéis estão em " + Pasta + ".");
        }

        // ------------------------------------------------------------------ o desenho

        static Texture2D Desenhar()
        {
            _p = new Color[L * L];
            for (int i = 0; i < _p.Length; i++) _p[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            var creme = new Color(0.89f, 0.86f, 0.76f);
            var branco = new Color(0.92f, 0.91f, 0.86f);
            var tinta = new Color(0.20f, 0.19f, 0.18f);
            var azul = new Color(0.14f, 0.20f, 0.42f);
            var vermelho = new Color(0.62f, 0.13f, 0.10f);

            // a folha datilografada, com o timbre, umas anotações de caneta e a rubrica
            var r = AtlasDosPapeis.Folha;
            Papel(r, creme, 1, manchas: 2);
            float y = Timbre(r, 2);
            Maquina(r, 22, y + 14, r.width - 22, r.height - 40, 9, tinta, 3);
            Mao(r, 150, r.height - 34, 230, r.height - 24, 10, azul, 4, 1);
            // o aviso: a faixa vermelha com as letras brancas, o título em preto e as linhas
            r = AtlasDosPapeis.Aviso;
            Papel(r, branco, 5, manchas: 1);
            Caixa(r, 0, 0, r.width, 46, vermelho, 0.92f);
            Blocos(r, 30, 14, r.width - 30, 32, 1, 18, 4, new Color(0.96f, 0.93f, 0.88f), 6);
            Blocos(r, 22, 58, r.width - 22, 76, 1, 18, 5, tinta, 7);
            Maquina(r, 22, 92, r.width - 22, r.height - 30, 10, tinta, 8);
            // o formulário: o timbre, a tabela e as letras de mão nas casas
            r = AtlasDosPapeis.Ficha;
            Papel(r, creme, 9, manchas: 2);
            y = Timbre(r, 10);
            for (float ly = y + 22; ly < r.height - 22; ly += 17) Linha(r, 16, ly, r.width - 16, ly, 1f, new Color(0.35f, 0.38f, 0.45f), 0.55f);
            Linha(r, 70, y + 10, 70, r.height - 22, 1f, new Color(0.35f, 0.38f, 0.45f), 0.55f);
            Mao(r, 20, y + 8, 66, r.height - 26, 17, azul, 11, 2);
            Mao(r, 76, y + 8, r.width - 20, r.height - 60, 17, azul, 12, 6);
            Mao(r, 40, r.height - 58, r.width - 30, r.height - 26, 17, vermelho, 13, 3);
            // a carta à mão no papel timbrado
            r = AtlasDosPapeis.Carta;
            Papel(r, creme, 14, manchas: 3);
            y = Timbre(r, 15);
            Mao(r, 24, y + 18, r.width - 22, r.height - 46, 13, azul, 16, 7);
            Mao(r, 120, r.height - 40, 220, r.height - 28, 12, azul, 17, 1);
            // o recado de pincel atômico
            r = AtlasDosPapeis.Recado;
            Papel(r, branco, 18, manchas: 1);
            Pincel(r, 26, 40, r.width - 26, r.height - 40, 44, new Color(0.08f, 0.08f, 0.09f), 19);
            // o papel liso (o fundo da folha na tela) e as páginas do caderno
            Papel(AtlasDosPapeis.Liso, creme, 20, manchas: 2, dobra: true);
            Caderno(AtlasDosPapeis.CadernoE, creme, azul, 21, esquerda: true);
            Caderno(AtlasDosPapeis.CadernoD, creme, azul, 22, esquerda: false);
            // a pasta parda: a etiqueta, o carimbo e as manchas
            r = AtlasDosPapeis.Pasta;
            Papel(r, new Color(0.78f, 0.64f, 0.41f), 23, manchas: 2, fibra: 0.06f);
            Caixa(r, 22, 28, 150, 70, new Color(0.93f, 0.91f, 0.84f), 1f);
            Mao(r, 30, 36, 142, 64, 12, azul, 24, 2);
            CarimboDesenhado(r, 128, 180, 190, 46, -12f, vermelho, 25);
            // a prancheta, o caderno, o bilhete e as cores sólidas
            Papel(AtlasDosPapeis.Prancheta, new Color(0.43f, 0.29f, 0.17f), 26, manchas: 0, fibra: 0.08f, amarelo: false);
            Papel(AtlasDosPapeis.Capa, new Color(0.14f, 0.21f, 0.16f), 27, manchas: 0, fibra: 0.07f, amarelo: false);
            r = AtlasDosPapeis.Bilhete;
            Papel(r, creme, 28, manchas: 1);
            for (float ly = 22; ly < r.height - 8; ly += 12) Linha(r, 4, ly, r.width - 4, ly, 1f, new Color(0.45f, 0.58f, 0.78f), 0.6f);
            Linha(r, 18, 2, 18, r.height - 2, 1f, new Color(0.75f, 0.30f, 0.30f), 0.55f);
            Mao(r, 24, 12, r.width - 14, r.height - 16, 12, azul, 29, 5);
            Linha(r, r.width / 2f, 0, r.width / 2f, r.height, 1.4f, new Color(0.62f, 0.58f, 0.50f), 0.5f);
            Metal(AtlasDosPapeis.Clipe);
            Papel(AtlasDosPapeis.Fita, new Color(0.86f, 0.80f, 0.62f), 30, manchas: 0, amarelo: false);
            Papel(AtlasDosPapeis.Borda, new Color(0.86f, 0.83f, 0.74f), 31, manchas: 0, amarelo: false);
            Papel(AtlasDosPapeis.Manila, new Color(0.72f, 0.58f, 0.37f), 32, manchas: 0, fibra: 0.05f, amarelo: false);
            Papel(AtlasDosPapeis.Madeira, new Color(0.34f, 0.23f, 0.14f), 33, manchas: 0, fibra: 0.08f, amarelo: false);

            var tex = new Texture2D(L, L, TextureFormat.RGBA32, false);
            var linhas = new Color[L * L];
            for (int yy = 0; yy < L; yy++)
            for (int xx = 0; xx < L; xx++)
                linhas[(L - 1 - yy) * L + xx] = _p[yy * L + xx];
            tex.SetPixels(linhas);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ as ferramentas (x e y no retângulo, y para baixo)

        static void Por(RectInt r, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= r.width || y >= r.height || a <= 0f) return;
            int i = (r.y + y) * L + r.x + x;
            _p[i] = Color.Lerp(_p[i], new Color(c.r, c.g, c.b, 1f), Mathf.Clamp01(a));
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float Ruido(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int s) =>
            0.5f * Ruido(x, y, s) + 0.25f * Ruido(x * 2.03f, y * 2.03f, s + 1) + 0.125f * Ruido(x * 4.1f, y * 4.1f, s + 2) + 0.125f * Ruido(x * 8.3f, y * 8.3f, s + 3);

        /// <summary>O papel: a cor com o ruído, a fibra, o amarelado das beiradas, as manchas, a dobra e a sujeira.</summary>
        static void Papel(RectInt r, Color cor, int s, int manchas, float fibra = 0.03f, bool amarelo = true, bool dobra = false)
        {
            var rnd = new System.Random(s);
            for (int y = 0; y < r.height; y++)
            for (int x = 0; x < r.width; x++)
            {
                float n = Fbm(x / 38f, y / 38f, s) - 0.5f;
                float f = (Ruido(x / 1.6f, y / 9f, s + 9) - 0.5f) * fibra;
                var c = cor * (1f + n * 0.08f + f);
                if (amarelo)
                {
                    float borda = Mathf.Min(Mathf.Min(x, r.width - 1 - x), Mathf.Min(y, r.height - 1 - y)) / (float)Mathf.Min(r.width, r.height);
                    float b = Mathf.Clamp01(1f - borda / 0.16f) * (0.6f + 0.4f * Fbm(x / 14f, y / 14f, s + 4));
                    c = Color.Lerp(c, new Color(0.70f, 0.58f, 0.36f), b * 0.38f);
                    c = Color.Lerp(c, new Color(0.80f, 0.72f, 0.52f), Mathf.Clamp01(Fbm(x / 60f, y / 60f, s + 5) - 0.45f) * 0.5f);
                }
                c.a = 1f;
                _p[(r.y + y) * L + r.x + x] = c;
                if (Hash(x, y, s + 6) > 0.9985f) Por(r, x, y, new Color(0.30f, 0.26f, 0.2f), 0.5f);
            }
            for (int k = 0; k < manchas; k++)
            {
                float cx = (float)(rnd.NextDouble() * r.width), cy = (float)(rnd.NextDouble() * r.height);
                float raio = 14f + (float)rnd.NextDouble() * 22f;
                bool anel = rnd.NextDouble() < 0.6;
                for (int y = (int)(cy - raio - 4); y <= cy + raio + 4; y++)
                for (int x = (int)(cx - raio - 4); x <= cx + raio + 4; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) * (0.9f + 0.2f * Ruido(x / 6f, y / 6f, s + k));
                    float a = anel ? Mathf.Clamp01(1f - Mathf.Abs(d - raio) / 2.4f) * 0.32f + (d < raio ? 0.05f : 0f)
                                   : Mathf.Clamp01(1f - d / raio) * 0.16f;
                    Por(r, x, y, anel ? new Color(0.45f, 0.30f, 0.16f) : new Color(0.55f, 0.48f, 0.34f), a);
                }
            }
            if (dobra)
            {
                float yd = r.height * 0.36f;
                Linha(r, 0, yd, r.width, yd + 2, 1f, new Color(1f, 0.98f, 0.92f), 0.35f);
                Linha(r, 0, yd + 1.5f, r.width, yd + 3.5f, 1f, new Color(0.45f, 0.40f, 0.30f), 0.25f);
            }
        }

        static void Caixa(RectInt r, int x0, int y0, int x1, int y1, Color c, float a)
        {
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                Por(r, x, y, c * (0.96f + 0.08f * Ruido(x / 5f, y / 5f, 77)), a);
        }

        /// <summary>Um traço de a até b, com a grossura (a beira suave).</summary>
        static void Linha(RectInt r, float ax, float ay, float bx, float by, float esp, Color c, float a)
        {
            float minx = Mathf.Min(ax, bx) - esp - 1, maxx = Mathf.Max(ax, bx) + esp + 1, miny = Mathf.Min(ay, by) - esp - 1, maxy = Mathf.Max(ay, by) + esp + 1;
            Vector2 p0 = new Vector2(ax, ay), d = new Vector2(bx - ax, by - ay);
            float l2 = Mathf.Max(d.sqrMagnitude, 1e-4f);
            for (int y = (int)miny; y <= maxy; y++)
            for (int x = (int)minx; x <= maxx; x++)
            {
                var q = new Vector2(x + 0.5f, y + 0.5f) - p0;
                float t = Mathf.Clamp01(Vector2.Dot(q, d) / l2);
                float dist = (q - d * t).magnitude;
                Por(r, x, y, c, a * Mathf.Clamp01(esp / 2f + 0.5f - dist));
            }
        }

        // a cruz vermelha e as letras do timbre (em blocos), com o traço embaixo; devolve onde acabou
        static float Timbre(RectInt r, int s)
        {
            var vermelho = new Color(0.55f, 0.13f, 0.11f);
            Caixa(r, 22, 16, 28, 34, vermelho, 0.9f);
            Caixa(r, 16, 22, 34, 28, vermelho, 0.9f);
            Blocos(r, 42, 17, 210, 24, 1, 10, 3, new Color(0.18f, 0.18f, 0.2f), s);
            Blocos(r, 42, 28, 160, 32, 1, 10, 3, new Color(0.35f, 0.33f, 0.3f), s + 1);
            Linha(r, 16, 42, r.width - 16, 42, 1.2f, new Color(0.25f, 0.22f, 0.2f), 0.7f);
            return 44;
        }

        /// <summary>As "palavras" em blocos (as letras grandes de impressão): linhas de y0 a y1 com a altura h.</summary>
        static void Blocos(RectInt r, int x0, int y0, int x1, int y1, int linhas, int h, int vao, Color c, int s)
        {
            var rnd = new System.Random(s);
            float altura = (y1 - y0) / (float)linhas;
            for (int li = 0; li < linhas; li++)
            {
                int y = y0 + Mathf.RoundToInt(li * altura);
                int x = x0;
                while (x < x1 - 8)
                {
                    int w = 8 + rnd.Next(26);
                    if (x + w > x1) w = x1 - x;
                    Caixa(r, x, y, x + w, y + Mathf.Min(h, y1 - y), c, 0.92f);
                    x += w + vao;
                }
            }
        }

        /// <summary>As linhas de máquina: as palavras em traços de 3 px, com o fim de parágrafo mais curto.</summary>
        static void Maquina(RectInt r, int x0, float y0, int x1, int y1, float passo, Color c, int s)
        {
            var rnd = new System.Random(s);
            for (float y = y0; y < y1; y += passo)
            {
                bool fim = rnd.NextDouble() < 0.16, vazia = rnd.NextDouble() < 0.07;
                if (vazia) continue;
                float limite = fim ? x0 + (x1 - x0) * (0.25f + 0.5f * (float)rnd.NextDouble()) : x1;
                float x = x0;
                while (x < limite - 4)
                {
                    float w = 4 + (float)rnd.NextDouble() * 24f;
                    if (x + w > limite) w = limite - x;
                    float tom = 0.7f + 0.3f * (float)rnd.NextDouble();
                    for (int yy = 0; yy < 3; yy++)
                    for (int xx = 0; xx < w; xx++)
                        Por(r, (int)(x + xx), (int)y + yy, c, tom * (0.65f + 0.35f * Hash((int)x + xx, (int)y + yy, s)));
                    x += w + 4;
                }
            }
        }

        /// <summary>As linhas de mão: as palavras em traços ondulados de 2 px, um pouco tortos.</summary>
        static void Mao(RectInt r, float x0, float y0, float x1, float y1, float passo, Color c, int s, int maxLinhas)
        {
            var rnd = new System.Random(s);
            int n = 0;
            for (float y = y0 + passo * 0.6f; y < y1 && n < maxLinhas; y += passo, n++)
            {
                float limite = rnd.NextDouble() < 0.25 ? x0 + (x1 - x0) * (0.3f + 0.5f * (float)rnd.NextDouble()) : x1;
                float x = x0 + (float)rnd.NextDouble() * 4f, inclina = ((float)rnd.NextDouble() - 0.5f) * 0.03f;
                while (x < limite - 6)
                {
                    float w = 8 + (float)rnd.NextDouble() * 30f;
                    if (x + w > limite) w = limite - x;
                    float fase = (float)rnd.NextDouble() * 6f, ax = x, ay = y + (x - x0) * inclina;
                    for (float t = 1.5f; t <= w; t += 1.5f)
                    {
                        float bx = x + t, by = y + (bx - x0) * inclina + Mathf.Sin(t * 0.7f + fase) * 2.2f + Mathf.Sin(t * 0.23f + fase) * 1.2f;
                        Linha(r, ax, ay, bx, by, 1.6f, c, 0.85f);
                        ax = bx;
                        ay = by;
                    }
                    x += w + 5 + (float)rnd.NextDouble() * 4f;
                }
            }
        }

        /// <summary>O pincel atômico: traços grossos e tortos, em blocos de letras grandes.</summary>
        static void Pincel(RectInt r, float x0, float y0, float x1, float y1, float passo, Color c, int s)
        {
            var rnd = new System.Random(s);
            for (float y = y0 + passo * 0.5f; y < y1; y += passo)
            {
                float x = x0 + (float)rnd.NextDouble() * 10f;
                float limite = x1 - (float)rnd.NextDouble() * 50f;
                while (x < limite - 10)
                {
                    // uma letra: dois ou três traços entre o alto e o baixo da linha
                    float w = 14 + (float)rnd.NextDouble() * 10f, alto = y - passo * 0.32f, baixo = y + passo * 0.22f;
                    int tracos = 2 + rnd.Next(2);
                    for (int t = 0; t < tracos; t++)
                    {
                        float ax = x + (float)rnd.NextDouble() * w, bx = x + (float)rnd.NextDouble() * w;
                        bool deitado = rnd.NextDouble() < 0.35;
                        if (deitado) Linha(r, x, Mathf.Lerp(alto, baixo, (float)rnd.NextDouble()), x + w, Mathf.Lerp(alto, baixo, (float)rnd.NextDouble()), 4.2f, c, 0.9f);
                        else Linha(r, ax, alto, bx, baixo, 4.2f, c, 0.9f);
                    }
                    x += w + 6f;
                    if (rnd.NextDouble() < 0.2) x += 14f;
                }
            }
        }

        static void Caderno(RectInt r, Color creme, Color azul, int s, bool esquerda)
        {
            Papel(r, creme, s, manchas: 1, amarelo: true);
            for (float ly = 30; ly < r.height - 10; ly += 11) Linha(r, 4, ly, r.width - 4, ly, 1f, new Color(0.45f, 0.58f, 0.78f), 0.55f);
            float margem = esquerda ? 26f : 22f;
            Linha(r, margem, 2, margem, r.height - 2, 1f, new Color(0.75f, 0.30f, 0.30f), 0.5f);
            Mao(r, margem + 6, 22, r.width - 14, r.height - 20, 11, azul, s + 1, esquerda ? 26 : 14);
            // a sombra da lombada
            for (int y = 0; y < r.height; y++)
            for (int x = 0; x < 26; x++)
                Por(r, esquerda ? r.width - 1 - x : x, y, new Color(0.30f, 0.26f, 0.2f), (1f - x / 26f) * 0.38f);
        }

        static void CarimboDesenhado(RectInt r, float cx, float cy, float w, float h, float graus, Color c, int s)
        {
            float a = graus * Mathf.Deg2Rad, co = Mathf.Cos(a), si = Mathf.Sin(a);
            Vector2 P(float x, float y) => new Vector2(cx + x * co - y * si, cy + x * si + y * co);
            var cantos = new[] { P(-w / 2, -h / 2), P(w / 2, -h / 2), P(w / 2, h / 2), P(-w / 2, h / 2) };
            for (int i = 0; i < 4; i++)
            {
                var p = cantos[i];
                var q = cantos[(i + 1) % 4];
                Linha(r, p.x, p.y, q.x, q.y, 3f, c, 0.75f);
            }
            // as letras: blocos no espaço do carimbo, com a tinta falhando
            var rnd = new System.Random(s);
            var letras = new System.Collections.Generic.List<(float de, float ate)>();
            for (float lx = -w / 2 + 12; lx < w / 2 - 16;)
            {
                float lw = 9 + (float)rnd.NextDouble() * 6;
                letras.Add((lx, Mathf.Min(lx + lw, w / 2 - 12)));
                lx += lw + 5;
            }
            float alcance = Mathf.Max(w, h);
            for (int y = (int)(cy - alcance); y <= cy + alcance; y++)
            for (int x = (int)(cx - alcance); x <= cx + alcance; x++)
            {
                float dx = x - cx, dy = y - cy, lx = dx * co + dy * si, ly = -dx * si + dy * co;
                if (Mathf.Abs(ly) > h / 2 - 12) continue;
                foreach (var (de, ate) in letras)
                {
                    if (lx < de || lx > ate) continue;
                    Por(r, x, y, c, 0.62f * Mathf.Clamp01(Fbm(x / 3f, y / 3f, s) * 1.6f - 0.25f));
                    break;
                }
            }
        }

        static void Metal(RectInt r)
        {
            for (int y = 0; y < r.height; y++)
            for (int x = 0; x < r.width; x++)
            {
                float g = 0.48f + 0.22f * (1f - y / (float)r.height) + 0.05f * (Ruido(x / 2f, y / 12f, 41) - 0.5f);
                var c = new Color(g, g * 1.01f, g * 1.04f, 1f);
                if (Fbm(x / 9f, y / 9f, 42) > 0.66f) c = Color.Lerp(c, new Color(0.45f, 0.26f, 0.14f), 0.6f);
                _p[(r.y + y) * L + r.x + x] = c;
            }
        }
    }
}
