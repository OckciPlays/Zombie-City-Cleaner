using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O quadro de instrumentos do carro, feito por código (para a <see cref="MontagemDosVeiculos"/>): a textura
    /// (`Art/Veiculos/Painel.png`: o velocímetro de 0 a 160 km/h e o marcador da gasolina, com as marcas em branco
    /// quente, e, embaixo, o ícone verde do farol) e as malhas do quadro, do ícone e dos dois ponteiros
    /// (`Art/Veiculos/Malhas/`). O quadro tem 30 × 9 cm e cobre a frente do quadro de instrumentos do modelo; olha para
    /// o motorista (-Z). Os ângulos das marcas são os dos ponteiros (<see cref="Carro.AnguloDoVelocimetro"/>).
    /// </summary>
    public static class PainelDoCarro
    {
        // a textura: o quadro nas 288 linhas de cima, o ícone do farol (96 × 96) no canto de baixo, à esquerda
        public const int L = 960, A = 384, AQuadro = 288, Icone = 96;
        public const float Largura = 0.30f, Altura = 0.09f;
        // em pixels do quadro (x para a direita, y para baixo, a partir do canto de cima)
        public static readonly Vector2 Velocimetro = new Vector2(330f, 150f), Gasolina = new Vector2(690f, 162f), LugarDoIcone = new Vector2(510f, 62f);
        const float RaioVel = 128f, RaioGas = 88f;
        public const float TamanhoDoIcone = 0.014f;

        static readonly Color Fundo = new Color(0.035f, 0.037f, 0.042f);
        static readonly Color Mostrador = new Color(0.055f, 0.058f, 0.064f);
        static readonly Color Aro = new Color(0.32f, 0.31f, 0.29f);
        static readonly Color Marca = new Color(1f, 0.91f, 0.74f);
        static readonly Color MarcaFraca = new Color(0.82f, 0.74f, 0.58f);
        static readonly Color Vermelho = new Color(1f, 0.2f, 0.1f);
        static readonly Color Verde = new Color(0.35f, 1f, 0.45f);

        /// <summary>Um ponto do quadro (em pixels) na posição local do objeto do quadro.</summary>
        public static Vector3 NoQuadro(Vector2 px) => new Vector3(-Largura / 2f + px.x * Largura / L, Altura / 2f - px.y * Altura / AQuadro, 0f);

        public static Vector3 CentroDoVelocimetro => NoQuadro(Velocimetro);
        public static Vector3 CentroDaGasolina => NoQuadro(Gasolina);
        public static Vector3 CentroDoIcone => NoQuadro(LugarDoIcone);

        // ------------------------------------------------------------------------------------------ a textura

        public static Texture2D Textura(string caminho)
        {
            var t = new Tela(L, A, Fundo);
            // o quadro: o fundo e um filete em volta
            t.Retangulo(0, 0, L, AQuadro, Fundo);
            t.Moldura(4, 4, L - 8, AQuadro - 8, 3f, new Color(0.13f, 0.13f, 0.13f));

            // o velocímetro
            Vector2 v = Velocimetro;
            t.Disco(v, RaioVel + 8f, Aro);
            t.Disco(v, RaioVel + 4f, Mostrador);
            for (int kmh = 0; kmh <= 160; kmh += 10)
            {
                float a = Carro.AnguloDoVelocimetro(kmh);
                bool grande = kmh % 20 == 0;
                t.Segmento(Ponto(v, RaioVel - (grande ? 26f : 15f), a), Ponto(v, RaioVel - 3f, a), grande ? 5.5f : 3f, grande ? Marca : MarcaFraca);
            }
            for (int kmh = 0; kmh <= 160; kmh += 40)
                t.Texto(kmh.ToString(), Ponto(v, RaioVel - 52f, Carro.AnguloDoVelocimetro(kmh)), 4, Marca);
            t.Texto("km/h", v + new Vector2(0f, 54f), 3, MarcaFraca);

            // a gasolina: o arco, a faixa vermelha da reserva, as marcas, o E, o F e a bomba
            Vector2 g = Gasolina;
            t.Disco(g, RaioGas + 8f, Aro);
            t.Disco(g, RaioGas + 4f, Mostrador);
            t.Arco(g, RaioGas - 10f, RaioGas - 6f, Carro.AnguloDoCombustivel(0.12f), Carro.AnguloDoCombustivel(1f), MarcaFraca);
            t.Arco(g, RaioGas - 14f, RaioGas - 2f, Carro.AnguloDoCombustivel(0f), Carro.AnguloDoCombustivel(0.12f), Vermelho);
            for (int i = 0; i <= 4; i++)
            {
                float a = Carro.AnguloDoCombustivel(i / 4f);
                bool grande = i % 2 == 0;
                t.Segmento(Ponto(g, RaioGas - (grande ? 24f : 18f), a), Ponto(g, RaioGas - 2f, a), grande ? 5f : 3f, i == 0 ? Vermelho : Marca);
            }
            t.Texto("E", Ponto(g, RaioGas - 44f, Carro.AnguloDoCombustivel(0f) - 14f), 4, Marca);
            t.Texto("F", Ponto(g, RaioGas - 44f, Carro.AnguloDoCombustivel(1f) + 14f), 4, Marca);
            Bomba(t, g + new Vector2(0f, 40f), MarcaFraca);

            // o ícone do farol (embaixo, fora do quadro): o farol de lado e os 3 raios para a frente
            var c = new Vector2(Icone * 0.62f, AQuadro + Icone / 2f);
            t.Retangulo(0, AQuadro, Icone, Icone, new Color(0.02f, 0.02f, 0.02f));
            t.Arco(c, 15f, 22f, 0f, 180f, Verde);
            t.Segmento(c + new Vector2(0f, -22f), c + new Vector2(0f, 22f), 6f, Verde);
            for (int i = -1; i <= 1; i++)
                t.Segmento(c + new Vector2(-10f, i * 13f), c + new Vector2(-38f, i * 13f + 6f), 5f, Verde);

            var tex = new Texture2D(L, A, TextureFormat.RGBA32, false);
            tex.SetPixels(t.px);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        // a bomba de gasolina: o corpo, o visor e a mangueira
        static void Bomba(Tela t, Vector2 c, Color cor)
        {
            t.Retangulo(Mathf.RoundToInt(c.x - 9), Mathf.RoundToInt(c.y - 12), 18, 24, cor);
            t.Retangulo(Mathf.RoundToInt(c.x - 6), Mathf.RoundToInt(c.y - 9), 12, 7, Mostrador);
            t.Segmento(c + new Vector2(9f, -6f), c + new Vector2(15f, -2f), 3f, cor);
            t.Segmento(c + new Vector2(15f, -2f), c + new Vector2(15f, 10f), 3f, cor);
        }

        // um ponto a uma distância e um ângulo (graus, no sentido do relógio a partir de cima) do centro
        static Vector2 Ponto(Vector2 centro, float raio, float angulo)
        {
            float a = angulo * Mathf.Deg2Rad;
            return centro + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * raio;
        }

        /// <summary>Uma imagem em que se desenha com y para baixo (0 em cima), com as bordas suavizadas.</summary>
        class Tela
        {
            public readonly Color[] px;
            readonly int _l, _a;

            public Tela(int l, int a, Color fundo)
            {
                _l = l;
                _a = a;
                px = new Color[l * a];
                for (int i = 0; i < px.Length; i++) px[i] = fundo;
            }

            void Pintar(int x, int y, Color c, float cobre)
            {
                if (x < 0 || y < 0 || x >= _l || y >= _a || cobre <= 0f) return;
                int i = (_a - 1 - y) * _l + x;
                var p = Color.Lerp(px[i], c, Mathf.Clamp01(cobre));
                p.a = 1f;
                px[i] = p;
            }

            public void Retangulo(int x, int y, int l, int a, Color c)
            {
                for (int j = y; j < y + a; j++)
                for (int i = x; i < x + l; i++)
                    Pintar(i, j, c, 1f);
            }

            public void Moldura(int x, int y, int l, int a, float e, Color c)
            {
                Segmento(new Vector2(x, y), new Vector2(x + l, y), e, c);
                Segmento(new Vector2(x + l, y), new Vector2(x + l, y + a), e, c);
                Segmento(new Vector2(x + l, y + a), new Vector2(x, y + a), e, c);
                Segmento(new Vector2(x, y + a), new Vector2(x, y), e, c);
            }

            public void Disco(Vector2 c, float r, Color cor) => Arco(c, -1f, r, -180f, 180f, cor);

            /// <summary>Um anel de r0 a r1, de um ângulo a outro (graus, no sentido do relógio a partir de cima).</summary>
            public void Arco(Vector2 c, float r0, float r1, float de, float ate, Color cor)
            {
                int x0 = Mathf.FloorToInt(c.x - r1 - 1), x1 = Mathf.CeilToInt(c.x + r1 + 1);
                int y0 = Mathf.FloorToInt(c.y - r1 - 1), y1 = Mathf.CeilToInt(c.y + r1 + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cobre = Mathf.Min(d - r0, r1 - d) + 0.5f;
                    if (cobre <= 0f) continue;
                    float a = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
                    // a borda do ângulo, suavizada pela distância em pixels
                    float dentro = Mathf.Min(Mathf.DeltaAngle(de, a), Mathf.DeltaAngle(a, ate)) * Mathf.Deg2Rad * Mathf.Max(d, 1f) + 0.5f;
                    if (ate - de >= 360f) dentro = 1f;
                    Pintar(x, y, cor, Mathf.Min(cobre, dentro));
                }
            }

            public void Segmento(Vector2 a, Vector2 b, float espessura, Color cor)
            {
                float r = espessura / 2f;
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1);
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1);
                Vector2 ab = b - a;
                float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float d = (p - (a + ab * k)).magnitude;
                    Pintar(x, y, cor, r - d + 0.5f);
                }
            }

            /// <summary>Um texto na fonte de 5 linhas (cada ponto vira um quadrado de 'escala' pixels), centrado.</summary>
            public void Texto(string texto, Vector2 centro, int escala, Color cor)
            {
                int largura = 0;
                foreach (char ch in texto) largura += (Fonte.TryGetValue(ch, out var gl) ? gl[0].Length : 3) + 1;
                largura = (largura - 1) * escala;
                int x = Mathf.RoundToInt(centro.x - largura / 2f), y = Mathf.RoundToInt(centro.y - 2.5f * escala);
                foreach (char ch in texto)
                {
                    if (!Fonte.TryGetValue(ch, out var gl)) { x += 4 * escala; continue; }
                    for (int j = 0; j < 5; j++)
                    for (int i = 0; i < gl[j].Length; i++)
                        if (gl[j][i] == '1')
                            for (int b = 0; b < escala; b++)
                            for (int a = 0; a < escala; a++)
                                Pintar(x + i * escala + a, y + j * escala + b, cor, 1f);
                    x += (gl[0].Length + 1) * escala;
                }
            }
        }

        static readonly Dictionary<char, string[]> Fonte = new Dictionary<char, string[]>
        {
            ['0'] = new[] { "111", "101", "101", "101", "111" },
            ['1'] = new[] { "010", "110", "010", "010", "111" },
            ['2'] = new[] { "111", "001", "111", "100", "111" },
            ['3'] = new[] { "111", "001", "111", "001", "111" },
            ['4'] = new[] { "101", "101", "111", "001", "001" },
            ['5'] = new[] { "111", "100", "111", "001", "111" },
            ['6'] = new[] { "111", "100", "111", "101", "111" },
            ['7'] = new[] { "111", "001", "001", "001", "001" },
            ['8'] = new[] { "111", "101", "111", "101", "111" },
            ['9'] = new[] { "111", "101", "111", "001", "111" },
            ['E'] = new[] { "111", "100", "110", "100", "111" },
            ['F'] = new[] { "111", "100", "110", "100", "100" },
            ['k'] = new[] { "100", "101", "110", "101", "101" },
            ['m'] = new[] { "00000", "11110", "10101", "10101", "10101" },
            ['h'] = new[] { "100", "100", "111", "101", "101" },
            ['/'] = new[] { "001", "001", "010", "100", "100" },
        };

        // ------------------------------------------------------------------------------------------ as malhas

        /// <summary>O quadro (com a parte de cima da textura).</summary>
        public static Mesh MalhaDoQuadro() => Retangulo("Painel_Quadro", Largura, Altura, new Rect(0f, (A - AQuadro) / (float)A, 1f, AQuadro / (float)A));

        /// <summary>O ícone do farol (o canto de baixo da textura).</summary>
        public static Mesh MalhaDoIcone() => Retangulo("Painel_Icone", TamanhoDoIcone, TamanhoDoIcone, new Rect(0f, 0f, Icone / (float)L, Icone / (float)A));

        /// <summary>Um ponteiro: a agulha para +Y a partir do eixo (com um rabicho atrás) e o miolo em volta do eixo.</summary>
        public static Mesh MalhaDoPonteiro(string nome, float comprimento, float largura)
        {
            var vs = new List<Vector3>();
            var ts = new List<int>();
            Quad(vs, ts, new Vector3(-largura / 2f, -0.006f), new Vector3(-largura / 5f, comprimento), new Vector3(largura / 5f, comprimento), new Vector3(largura / 2f, -0.006f));
            float m = 0.0045f;
            Quad(vs, ts, new Vector3(-m, -m, -0.0005f), new Vector3(-m, m, -0.0005f), new Vector3(m, m, -0.0005f), new Vector3(m, -m, -0.0005f));
            return Montar(nome, vs, ts, null);
        }

        static Mesh Retangulo(string nome, float l, float a, Rect uv)
        {
            var vs = new List<Vector3>();
            var ts = new List<int>();
            Quad(vs, ts, new Vector3(-l / 2f, -a / 2f), new Vector3(-l / 2f, a / 2f), new Vector3(l / 2f, a / 2f), new Vector3(l / 2f, -a / 2f));
            var uvs = new List<Vector2> { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) };
            return Montar(nome, vs, ts, uvs);
        }

        // de baixo à esquerda, no sentido do relógio para quem olha de -Z (a frente da face)
        static void Quad(List<Vector3> vs, List<int> ts, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vs.Count;
            vs.Add(a); vs.Add(b); vs.Add(c); vs.Add(d);
            ts.Add(i); ts.Add(i + 1); ts.Add(i + 2);
            ts.Add(i); ts.Add(i + 2); ts.Add(i + 3);
        }

        static Mesh Montar(string nome, List<Vector3> vs, List<int> ts, List<Vector2> uvs)
        {
            var malha = new Mesh { name = nome };
            malha.SetVertices(vs);
            malha.SetTriangles(ts, 0);
            var normais = new Vector3[vs.Count];
            for (int i = 0; i < normais.Length; i++) normais[i] = Vector3.back;
            malha.normals = normais;
            if (uvs == null) { uvs = new List<Vector2>(); for (int i = 0; i < vs.Count; i++) uvs.Add(Vector2.zero); }
            malha.SetUVs(0, uvs);
            malha.RecalculateBounds();
            malha.RecalculateTangents();
            return malha;
        }

        /// <summary>Guarda a malha em `caminho` (se já existe, troca o conteúdo, e quem a usa continua usando).</summary>
        public static Mesh Salvar(Mesh nova, string caminho)
        {
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha == null)
            {
                AssetDatabase.CreateAsset(nova, caminho);
                return nova;
            }
            EditorUtility.CopySerialized(nova, velha);
            Object.DestroyImmediate(nova);
            EditorUtility.SetDirty(velha);
            return velha;
        }
    }
}
