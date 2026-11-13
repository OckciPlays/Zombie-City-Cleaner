using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A grama densa da cena de floresta (o Plano_Floresta.md, "A grama densa", 09/11/2026). Menu Zombie City Cleaner >
    /// Floresta > Gerar a grama densa: desenha o atlas das folhas (4 colunas, cada uma um tufo de 9 a 13 folhas curvas
    /// recortadas pelo alfa, verdes com umas secas, mais escuras no pé), faz a malha do tufo (três cartas cruzadas, em três
    /// linhas, para dobrar no vento) e o material (o GramaDensa.shader). <see cref="PorNaCena"/> põe a
    /// <see cref="GramaDensa"/> no objeto da floresta de teste (a cena leve).
    /// </summary>
    public static class GeradorDeGrama
    {
        public const string Textura = GeradorDePinheirosReais.PastaDasTexturas + "/Grama_Folhas.png";
        public const string Malha = GeradorDeFloresta.PastaDasMalhas + "/GramaTufo.asset";
        public const string MaterialDaGrama = GeradorDePinheirosReais.PastaDosMateriais + "/Grama_Densa.mat";
        const string NomeDoShader = "ZombieCityCleaner/GramaDensa";
        const int Colunas = 4, LarguraDaColuna = 256, Altura = 512;
        const float LarguraDoTufo = 0.9f, AlturaDoTufo = 0.5f, Enterrado = 0.03f;

        [MenuItem("Zombie City Cleaner/Floresta/Gerar a grama densa", false, 14)]
        public static void Gerar()
        {
            Directory.CreateDirectory(GeradorDePinheirosReais.PastaDasTexturas);
            Directory.CreateDirectory(GeradorDePinheirosReais.PastaDosMateriais);
            Directory.CreateDirectory(GeradorDeFloresta.PastaDasMalhas);
            TexturaDasFolhas();
            var malha = GravarMalha(MalhaDoTufo(), Malha);
            var material = Material();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Floresta] A grama densa: o atlas {Textura}, a malha {Malha} ({malha.triangles.Length / 3} triângulos) e o material {MaterialDaGrama}.");
        }

        /// <summary>Põe (ou atualiza) a grama densa no objeto da floresta de teste, gerando os assets se faltam.</summary>
        public static GramaDensa PorNaCena(GameObject go)
        {
            if (!File.Exists(MaterialDaGrama) || !File.Exists(Malha) || !File.Exists(Textura)) Gerar();
            var grama = go.GetComponent<GramaDensa>();
            if (grama == null) grama = go.AddComponent<GramaDensa>();
            grama.malha = AssetDatabase.LoadAssetAtPath<Mesh>(Malha);
            grama.material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaGrama);
            return grama;
        }

        // ------------------------------------------------------------------ o atlas das folhas

        struct Folha
        {
            public Vector2 P0, P1, P2;   // a curva (quadrática): o pé, o controle e a ponta, na coluna (0..1 × 0..1)
            public float Largura;        // a largura no pé (na coluna)
            public Color Pe, Ponta;
        }

        static Vector2 Curva(in Folha f, float t)
        {
            float u = 1f - t;
            return u * u * f.P0 + 2f * u * t * f.P1 + t * t * f.P2;
        }

        /// <summary>
        /// Cada coluna é um tufo: as folhas saem do meio do pé, abrem para os lados e caem na ponta, afinando. A cor vai
        /// do verde escuro do pé ao verde claro da ponta (uma em cinco seca, amarelada), com a nervura mais escura no meio
        /// e as beiradas um pouco mais claras. As áreas transparentes recebem a cor da folha mais perto (o Espalhar), para
        /// os mips não escurecerem as bordas.
        /// </summary>
        static void TexturaDasFolhas()
        {
            int w = Colunas * LarguraDaColuna, h = Altura;
            var px = new Color32[w * h];
            const int segmentos = 32;
            for (int coluna = 0; coluna < Colunas; coluna++)
            {
                var rnd = new System.Random(900 + coluna * 17);
                float Entre(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
                int n = rnd.Next(13, 18);
                var folhas = new Folha[n];
                for (int i = 0; i < n; i++)
                {
                    float altura = Entre(0.35f, 0.97f);
                    // o pé espalhado pela base inteira da coluna (um tufo largo, sem o vão entre os tufos vizinhos), as
                    // folhas de fora caindo para fora
                    float px0 = 0.5f + Entre(-0.4f, 0.4f);
                    float lado = px0 < 0.5f ? (rnd.NextDouble() < 0.75 ? -1f : 1f) : (rnd.NextDouble() < 0.75 ? 1f : -1f);
                    float ponta = Mathf.Clamp(px0 + lado * Entre(0.04f, 0.5f) * altura, 0.04f, 0.96f);
                    bool seca = rnd.NextDouble() < 0.18;
                    float tom = Entre(0.85f, 1.15f);
                    var pe = seca ? new Color(0.30f, 0.27f, 0.12f) : new Color(0.085f, 0.16f, 0.045f);
                    var pt = seca ? new Color(0.62f, 0.56f, 0.28f) : new Color(0.40f, 0.53f, 0.17f);
                    // um pouco de variação de matiz entre as folhas
                    float matiz = Entre(-0.04f, 0.04f);
                    pe = new Color(pe.r * tom + matiz, pe.g * tom, pe.b * tom - matiz * 0.5f);
                    pt = new Color(pt.r * tom + matiz * 1.5f, pt.g * tom, pt.b * tom - matiz);
                    folhas[i] = new Folha
                    {
                        P0 = new Vector2(px0, 0f),
                        P1 = new Vector2(px0 + (ponta - px0) * Entre(0.1f, 0.35f), altura * Entre(0.55f, 0.7f)),
                        P2 = new Vector2(ponta, altura),
                        Largura = Entre(0.07f, 0.11f),
                        Pe = pe,
                        Ponta = pt,
                    };
                }
                // os pontos das curvas, uma vez
                var pontos = new Vector2[n, segmentos + 1];
                for (int i = 0; i < n; i++)
                for (int s = 0; s <= segmentos; s++)
                    pontos[i, s] = Curva(folhas[i], s / (float)segmentos);

                for (int y = 0; y < h; y++)
                for (int x = 0; x < LarguraDaColuna; x++)
                {
                    var p = new Vector2((x + 0.5f) / LarguraDaColuna, (y + 0.5f) / h);
                    float melhor = float.MinValue, melhorT = 0f, melhorU = 0f, melhorW = 1f;
                    int qual = -1;
                    for (int i = 0; i < n; i++)
                    {
                        // a distância à folha e onde ela está ao longo dela
                        float d2 = float.MaxValue, t = 0f, u = 0f;
                        for (int s = 0; s < segmentos; s++)
                        {
                            Vector2 a = pontos[i, s], b = pontos[i, s + 1];
                            Vector2 ab = b - a;
                            float l2 = Mathf.Max(ab.sqrMagnitude, 1e-9f);
                            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                            Vector2 q = a + ab * k;
                            float dd = (p - q).sqrMagnitude;
                            if (dd < d2)
                            {
                                d2 = dd;
                                t = (s + k) / segmentos;
                                // de que lado da folha (para a nervura e as beiradas)
                                u = Mathf.Sign((p - q).x * ab.y - (p - q).y * ab.x);
                            }
                        }
                        float d = Mathf.Sqrt(d2);
                        float meia = folhas[i].Largura * 0.5f * Mathf.Pow(1f - t, 0.65f) * (0.8f + 0.2f * Mathf.Sin(Mathf.PI * Mathf.Min(t * 2.5f, 1f)));
                        float dentro = meia - d;
                        if (dentro > melhor) { melhor = dentro; qual = i; melhorT = t; melhorU = d / Mathf.Max(meia, 1e-4f) * u; melhorW = meia; }
                    }
                    if (qual < 0) continue;
                    float alfa = Mathf.Clamp01(melhor * LarguraDaColuna + 0.5f);
                    if (alfa <= 0f) continue;
                    var f = folhas[qual];
                    var cor = Color.Lerp(f.Pe, f.Ponta, Mathf.Pow(melhorT, 0.8f));
                    float nervura = 1f - 0.28f * Mathf.Clamp01(1f - Mathf.Abs(melhorU) / 0.22f) * (1f - melhorT * 0.7f);
                    float beirada = 1f + 0.1f * Mathf.Clamp01((Mathf.Abs(melhorU) - 0.65f) / 0.35f);
                    cor *= nervura * beirada;
                    px[y * w + coluna * LarguraDaColuna + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cor.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cor.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cor.b) * 255f),
                        (byte)Mathf.RoundToInt(alfa * 255f));
                }
            }
            GeradorDePinheirosReais.Espalhar(px, w, h, 24);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(Textura, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Textura, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(Textura);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.4f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ a malha do tufo

        /// <summary>
        /// Três cartas em pé cruzadas (a 0°, 60° e 120°), cada uma em três linhas (o pé, enterrado um pouco; o meio; a
        /// ponta), com a coluna do atlas no UV (o shader soma a coluna sorteada pelo pé do tufo) e a cor do vértice do
        /// vento (R o balanço pela altura, G a fase, B o tremor). 12 triângulos.
        /// </summary>
        static Mesh MalhaDoTufo()
        {
            var m = new MalhaTexturada();
            float[] alturas = { -Enterrado, AlturaDoTufo * 0.5f, AlturaDoTufo };
            byte[] balanco = { 0, 90, 255 }, tremor = { 0, 70, 255 };
            float[] larguras = { 0.85f, 1f, 1f };
            for (int q = 0; q < 3; q++)
            {
                float a = q * 60f * Mathf.Deg2Rad;
                var lado = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var normal = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                var tangente = new Vector4(lado.x, 0f, lado.z, 1f);
                var ids = new int[3, 2];
                for (int r = 0; r < 3; r++)
                for (int c = 0; c < 2; c++)
                {
                    float x = (c == 0 ? -0.5f : 0.5f) * LarguraDoTufo * larguras[r];
                    var p = lado * x + Vector3.up * alturas[r];
                    var uv = new Vector2(q / (float)Colunas + c / (float)Colunas, (alturas[r] + Enterrado) / (AlturaDoTufo + Enterrado));
                    ids[r, c] = m.Vertice(p, normal, tangente, uv, new Color32(balanco[r], 128, tremor[r], 255));
                }
                for (int r = 0; r < 2; r++)
                    m.QuadVirado(0, ids[r, 0], ids[r, 1], ids[r + 1, 1], ids[r + 1, 0], normal);
            }
            return m.Malha("GramaTufo");
        }

        static Mesh GravarMalha(Mesh nova, string caminho)
        {
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha == null)
            {
                AssetDatabase.CreateAsset(nova, caminho);
                return nova;
            }
            velha.Clear();
            velha.SetVertices(nova.vertices);
            velha.SetNormals(nova.normals);
            velha.SetTangents(nova.tangents);
            velha.SetUVs(0, nova.uv);
            velha.SetColors(nova.colors32);
            velha.SetTriangles(nova.triangles, 0, true);
            velha.RecalculateBounds();
            EditorUtility.SetDirty(velha);
            Object.DestroyImmediate(nova);
            return velha;
        }

        // ------------------------------------------------------------------ o material

        static Material Material()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaGrama);
            var shader = Shader.Find(NomeDoShader);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, MaterialDaGrama);
            }
            else m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textura));
            m.SetColor("_Cor", Color.white);
            m.SetColor("_CorDaBase", new Color(0.42f, 0.48f, 0.36f));
            // a cor média da textura de grama do chão (T_Chao_Grama, medida em 10/11/2026): os tufos de longe vão para ela
            m.SetColor("_CorDoChao", new Color(0.353f, 0.433f, 0.208f));
            m.SetFloat("_Cutoff", 0.4f);
            m.SetFloat("_Suavidade", 0.05f);
            m.SetFloat("_Translucencia", 0.35f);
            m.SetFloat("_Balanco", 1f);
            m.SetFloat("_Tremor", 0.03f);
            m.SetFloat("_Variacao", 0.5f);
            m.SetFloat("_Dobra", 1f);
            m.SetFloat("_Cima", 0.85f);
            m.SetFloat("_Colunas", Colunas);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
