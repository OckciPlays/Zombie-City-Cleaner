using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As texturas da água (08/11/2026, a água nova; veja o Agua.shader): o normal map das ondinhas, feito por código
    /// (sem imagem de fora) e repetível, e o material da água com ele. Menu Zombie City Cleaner > Mundo > Gerar as
    /// texturas da água.
    /// </summary>
    public static class TexturasDaAgua
    {
        public const string Caminho = TexturasDoChao.Pasta + "/T_Agua_Normal.png";
        const int Lado = 512;

        [MenuItem("Zombie City Cleaner/Mundo/Gerar as texturas da água")]
        public static void Gerar()
        {
            Directory.CreateDirectory(TexturasDoChao.Pasta);
            var normal = SalvarNormal(Ondinhas(), Caminho);
            var m = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            if (m == null)
            {
                m = new Material(Shader.Find("ZombieCityCleaner/Agua"));
                AssetDatabase.CreateAsset(m, TexturasDoChao.MaterialDaAgua);
            }
            m.shader = Shader.Find("ZombieCityCleaner/Agua");
            m.SetTexture("_NormalTex", normal);
            // as cores mais vivas (10/11/2026, a pedido: "esse lago muito escuro e morto")
            m.SetColor("_CorRasa", new Color(0.5f, 0.76f, 0.74f));
            m.SetColor("_CorFunda", new Color(0.08f, 0.24f, 0.28f));
            m.SetColor("_TintaDoReflexo", new Color(0.64f, 0.9f, 1f));
            m.SetVector("_Absorcao", new Vector4(0.55f, 0.2f, 0.15f, 0f));
            m.SetFloat("_Turvo", 0.35f);
            m.SetFloat("_OndasEscala", 6f);
            m.SetFloat("_OndasForca", 0.35f);
            m.SetFloat("_OndasVelocidade", 0.15f);
            m.SetFloat("_Fluxo", 0.9f);
            m.SetFloat("_Refracao", 0.025f);
            m.SetFloat("_Fresnel", 0.05f);
            m.SetFloat("_Reflexo", 1.15f);
            m.SetFloat("_ReflexoPlanar", 0.85f);
            m.SetFloat("_Suavidade", 0.93f);
            m.SetFloat("_Espuma", 0.28f);
            m.SetColor("_CorEspuma", new Color(0.75f, 0.78f, 0.74f));
            m.renderQueue = -1;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mundo] Textura da água em {Caminho}; material {TexturasDoChao.MaterialDaAgua}.");
        }

        // ------------------------------------------------------------------ as ondinhas

        // o relevo das ondinhas: ondas curtas em várias direções (o ruído de valor repetível em oitavas, dobrado para dar as
        // cristas) e umas ondas compridas, todas com um número inteiro de períodos na textura (ela repete sem emenda)
        static float[] Ondinhas()
        {
            var h = new float[Lado * Lado];
            var rnd = new System.Random(7);
            // as ondas compridas: a direção, os períodos inteiros e a fase
            var ondas = new (float cx, float cy, float amp, float fase)[6];
            for (int k = 0; k < ondas.Length; k++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI);
                int periodos = 2 + k;
                // os períodos inteiros em x e em y (10/11/2026: o cos e o sen do ângulo vezes os períodos não davam inteiros, e
                // a textura não emendava: um risco no lago a cada 6 m e a cada 16 m)
                int kx = Mathf.RoundToInt(Mathf.Cos(ang) * periodos), ky = Mathf.RoundToInt(Mathf.Sin(ang) * periodos);
                if (kx == 0 && ky == 0) kx = periodos;
                ondas[k] = (kx, ky, 0.35f / (1f + k * 0.4f), (float)(rnd.NextDouble() * 6.283f));
            }
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float u = x / (float)Lado, v = y / (float)Lado;
                float soma = 0f, amp = 1f, norma = 0f;
                int periodo = 5;
                for (int o = 0; o < 5; o++)
                {
                    // as cristas: o ruído dobrado (1 - |2n - 1|) dá os vincos das ondinhas
                    float n = Ruido(u * periodo, v * periodo, periodo, 11 + o * 7);
                    float crista = 1f - Mathf.Abs(2f * n - 1f);
                    soma += (0.5f * n + 0.5f * crista) * amp;
                    norma += amp;
                    amp *= 0.55f;
                    periodo *= 2;
                }
                float r = soma / norma;
                foreach (var w in ondas) r += w.amp * Mathf.Sin((u * w.cx + v * w.cy) * 6.2831853f + w.fase) * 0.12f;
                h[y * Lado + x] = r;
            }
            return h;
        }

        // o ruído de valor repetível: a grade de "periodo" células, com os cantos sorteados pela posição (módulo o período)
        static float Ruido(float x, float y, int periodo, int semente)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = Canto(xi, yi, periodo, semente), b = Canto(xi + 1, yi, periodo, semente);
            float c = Canto(xi, yi + 1, periodo, semente), d = Canto(xi + 1, yi + 1, periodo, semente);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        static float Canto(int x, int y, int periodo, int semente)
        {
            x = ((x % periodo) + periodo) % periodo;
            y = ((y % periodo) + periodo) % periodo;
            uint v = (uint)(x * 374761393 + y * 668265263 + semente * 2246822519);
            v = (v ^ (v >> 13)) * 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }

        // o normal map do relevo (a inclinação pelas diferenças, repetindo nas bordas), gravado como um normal map
        static Texture2D SalvarNormal(float[] h, string caminho)
        {
            const float forca = 9f;
            var px = new Color32[Lado * Lado];
            for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float dx = (h[y * Lado + (x + 1) % Lado] - h[y * Lado + (x + Lado - 1) % Lado]) * 0.5f * forca;
                float dy = (h[((y + 1) % Lado) * Lado + x] - h[((y + Lado - 1) % Lado) * Lado + x]) * 0.5f * forca;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                px[y * Lado + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                                               (byte)Mathf.RoundToInt(n.z * 255f), 255);
            }
            var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = false;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }
    }
}
