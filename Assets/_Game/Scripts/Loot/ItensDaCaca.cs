using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Os itens da caça (12/11/2026, a fauna: a carne crua, a carne assada, o couro e a gordura), provisórios até o Pedro
    /// trocar os modelos: a malha (o chão e a mão) é feita na hora com caixinhas pintadas pela paleta (Art/Paleta.png, 16×16,
    /// a mesma dos outros itens), e o ícone (128×128) e a foto (600×500) são desenhados das mesmas caixinhas, vistas de cima
    /// e de lado como os ícones do loot.py. Nada disso vai para as cenas nem para os arquivos: tudo nasce no Play.
    /// </summary>
    public static partial class ItensDaCaca
    {
        /// <summary>
        /// Os itens com o modelo daqui (a caça; e o bagre da pesca, da etapa 3; e, em 18/11/2026, os gravetos, o espeto e a
        /// panela da fogueira).
        /// </summary>
        public static readonly Item[] Todos = { Item.CarneCrua, Item.CarneAssada, Item.Couro, Item.Gordura, Item.Bagre, Item.Gravetos, Item.Espeto, Item.Panela, Item.Salmao,
                                                Item.Violao, Item.Pandeiro, Item.Gaita, Item.Flauta,
                                                // (24/11/2026, a base: ItensDaCaca.Base.cs)
                                                Item.OlhoDoBrutamonte, Item.GargantaDoLong, Item.CabecaDoOco, Item.CabecaDeChefe,
                                                Item.NucleoDaBase, Item.MuroDeToras, Item.PortaoDeToras };

        struct Caixa
        {
            public Vector3 centro, tamanho;
            public int px, py;   // a célula da paleta (x, y de cima para baixo)
            public float giro;   // (19/11/2026) o giro em volta do eixo vertical, no centro dela (graus): as peças redondas
        }

        static Caixa C(float x, float y, float z, float lx, float ly, float lz, int px, int py, float giro = 0f) =>
            new Caixa { centro = new Vector3(x, y, z), tamanho = new Vector3(lx, ly, lz), px = px, py = py, giro = giro };

        // a face de uma caixa girada: a normal e os cantos no giro dela
        static Vector3 Girada(Caixa c, Vector3 v) => c.giro == 0f ? v : Quaternion.Euler(0f, c.giro, 0f) * v;
        static Vector3 PontoGirado(Caixa c, Vector3 p) => c.giro == 0f ? p : c.centro + Quaternion.Euler(0f, c.giro, 0f) * (p - c.centro);

        // um disco (o pandeiro, a boca e o bojo do violão): seis faixas pelo meio, giradas de 30 em 30 graus, com a ponta de
        // cada uma num lado do dodecágono de raio de dentro 'raio' (as pontas se encontram nos cantos: o contorno fica liso;
        // quadrados girados deixariam pontas, como uma estrela); a altura 'alto' em y
        static IEnumerable<Caixa> Disco(float x, float y, float z, float raio, float alto, int px, int py)
        {
            float largura = 2f * raio * Mathf.Tan(15f * Mathf.Deg2Rad);
            for (int k = 0; k < 6; k++) yield return C(x, y, z, raio * 2f, alto, largura, px, py, k * 30f);
        }

        // um aro de 12 lados (a parede do pandeiro): o raio do meio da parede, a espessura e a altura (cada tábua do tamanho
        // do lado de fora: os cantos se encontram por fora, sem dentes)
        static IEnumerable<Caixa> Aro(float y, float raio, float espessura, float alto, int px, int py)
        {
            float lado = 2f * (raio + espessura * 0.5f) * Mathf.Tan(15f * Mathf.Deg2Rad);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                yield return C(raio * Mathf.Cos(a), y, raio * Mathf.Sin(a), espessura, alto, lado, px, py, -k * 30f);
            }
        }

        // as caixinhas de cada item (metros; o chão em y = 0) e as cores da paleta
        static Caixa[] Pecas(Item i) => i switch
        {
            // a carne crua: o bife vermelho com a capa de gordura num lado e um calombo
            Item.CarneCrua => new[]
            {
                C(0f, 0.0225f, -0.008f, 0.15f, 0.045f, 0.085f, 10, 3),
                C(0f, 0.0235f, 0.043f, 0.15f, 0.047f, 0.018f, 10, 9),
                C(-0.02f, 0.051f, -0.01f, 0.08f, 0.012f, 0.05f, 6, 11),
            },
            // a carne assada: marrom, as marcas da grelha por cima e a borda dourada
            Item.CarneAssada => new[]
            {
                C(0f, 0.021f, -0.006f, 0.14f, 0.042f, 0.08f, 1, 14),
                C(0f, 0.022f, 0.042f, 0.14f, 0.044f, 0.016f, 7, 2),
                C(-0.04f, 0.0435f, -0.006f, 0.012f, 0.004f, 0.08f, 2, 0),
                C(0f, 0.0435f, -0.006f, 0.012f, 0.004f, 0.08f, 2, 0),
                C(0.04f, 0.0435f, -0.006f, 0.012f, 0.004f, 0.08f, 2, 0),
            },
            // o couro: enrolado, com duas tiras
            Item.Couro => new[]
            {
                C(0f, 0.0375f, 0f, 0.22f, 0.075f, 0.075f, 12, 4),
                C(-0.06f, 0.04f, 0f, 0.02f, 0.08f, 0.08f, 5, 14),
                C(0.06f, 0.04f, 0f, 0.02f, 0.08f, 0.08f, 5, 14),
                C(0.112f, 0.0375f, 0f, 0.004f, 0.06f, 0.06f, 3, 2),
            },
            // a gordura: um bloco creme com a parte mais clara por cima
            Item.Gordura => new[]
            {
                C(0f, 0.03f, 0f, 0.08f, 0.06f, 0.07f, 12, 8),
                C(0.005f, 0.07f, -0.004f, 0.06f, 0.02f, 0.05f, 10, 9),
            },
            // o bagre: o corpo escuro e achatado, a barriga clara, a cabeça larga, os bigodes e a cauda
            Item.Bagre => new[]
            {
                C(0f, 0.03f, 0f, 0.24f, 0.05f, 0.07f, 5, 14),
                C(0f, 0.012f, 0f, 0.22f, 0.02f, 0.065f, 12, 8),
                C(0.14f, 0.028f, 0f, 0.06f, 0.045f, 0.085f, 3, 2),
                C(0.185f, 0.025f, 0.035f, 0.05f, 0.006f, 0.006f, 2, 0),
                C(0.185f, 0.025f, -0.035f, 0.05f, 0.006f, 0.006f, 2, 0),
                C(-0.15f, 0.03f, 0f, 0.05f, 0.05f, 0.012f, 5, 14),
            },
            // os gravetos (18/11/2026): um feixe de galhos finos com a casca e um raminho
            Item.Gravetos => new[]
            {
                C(0f, 0.008f, 0f, 0.36f, 0.016f, 0.016f, 7, 3),
                C(0.02f, 0.008f, 0.022f, 0.32f, 0.014f, 0.014f, 8, 3),
                C(-0.015f, 0.022f, 0.01f, 0.34f, 0.014f, 0.014f, 7, 3),
                C(0.01f, 0.007f, -0.02f, 0.3f, 0.013f, 0.013f, 8, 3),
                C(0.09f, 0.03f, 0.03f, 0.07f, 0.008f, 0.008f, 7, 3),
            },
            // o espeto: o graveto reto, a ponta afiada mais clara (o comprimido no X: no fogo, ele estica)
            Item.Espeto => new[]
            {
                C(-0.03f, 0.008f, 0f, 0.42f, 0.015f, 0.015f, 7, 3),
                C(0.205f, 0.008f, 0f, 0.05f, 0.009f, 0.009f, 13, 4),
                C(-0.2f, 0.008f, 0f, 0.04f, 0.017f, 0.017f, 8, 3),
            },
            // a panela: o fundo, as quatro paredes, a borda mais clara e o cabo preto (centrada: o corpo e o cabo)
            Item.Panela => new[]
            {
                C(-0.09f, 0.006f, 0f, 0.24f, 0.012f, 0.24f, 1, 3),
                C(-0.205f, 0.042f, 0f, 0.01f, 0.072f, 0.24f, 1, 3),
                C(0.025f, 0.042f, 0f, 0.01f, 0.072f, 0.24f, 1, 3),
                C(-0.09f, 0.042f, 0.115f, 0.24f, 0.072f, 0.01f, 1, 3),
                C(-0.09f, 0.042f, -0.115f, 0.24f, 0.072f, 0.01f, 1, 3),
                C(-0.09f, 0.081f, 0.115f, 0.25f, 0.006f, 0.014f, 4, 3),
                C(-0.09f, 0.081f, -0.115f, 0.25f, 0.006f, 0.014f, 4, 3),
                C(-0.205f, 0.081f, 0f, 0.014f, 0.006f, 0.25f, 4, 3),
                C(0.025f, 0.081f, 0f, 0.014f, 0.006f, 0.25f, 4, 3),
                C(0.035f, 0.012f, 0f, 0.015f, 0.016f, 0.2f, 0, 3),
                C(0.125f, 0.07f, 0f, 0.17f, 0.016f, 0.032f, 6, 3),
            },
            // o salmão (18/11/2026): deitado de lado (o Y é a grossura; o Z, a altura do corpo), prateado, o dorso escuro, a
            // barriga rosada, a cabeça, a cauda, a nadadeira e o olho
            Item.Salmao => new[]
            {
                C(0f, 0.0225f, 0f, 0.36f, 0.045f, 0.1f, 2, 8),
                C(0f, 0.0235f, 0.04f, 0.34f, 0.047f, 0.026f, 6, 6),
                C(0f, 0.0235f, -0.038f, 0.3f, 0.046f, 0.022f, 13, 11),
                C(0.21f, 0.021f, 0f, 0.07f, 0.042f, 0.08f, 3, 10),
                C(-0.215f, 0.012f, 0f, 0.07f, 0.012f, 0.11f, 6, 6),
                C(0.01f, 0.022f, 0.064f, 0.07f, 0.006f, 0.03f, 6, 6),
                C(0.225f, 0.0435f, 0.012f, 0.014f, 0.004f, 0.014f, 2, 3),
            },
            // os instrumentos (18/11/2026; 19/11/2026, mais detalhados para o vídeo da fogueira: ItensDaCaca.Instrumentos)
            Item.Violao => Violao(),
            Item.Pandeiro => Pandeiro(),
            Item.Gaita => Gaita(),
            Item.Flauta => Flauta(),
            // a base (24/11/2026: ItensDaCaca.Base.cs)
            Item.OlhoDoBrutamonte => Olho(),
            Item.GargantaDoLong => Garganta(),
            Item.CabecaDoOco => CabecaDoOco(),
            Item.CabecaDeChefe => CabecaDeChefe(),
            Item.NucleoDaBase => KitDoNucleo(),
            Item.MuroDeToras => KitDoMuro(false),
            Item.PortaoDeToras => KitDoMuro(true),
            _ => null,
        };

        static int Indice(Item i)
        {
            for (int k = 0; k < Todos.Length; k++) if (Todos[k] == i) return k;
            return -1;
        }

        static readonly Mesh[] _malhas = new Mesh[Todos.Length];
        static readonly Texture2D[] _icones = new Texture2D[Todos.Length], _fotos = new Texture2D[Todos.Length];

        /// <summary>A malha do item (o chão e a mão; null: não é da caça).</summary>
        public static Mesh MalhaDoItem(Item i)
        {
            int k = Indice(i);
            if (k < 0) return null;
            if (_malhas[k] == null) _malhas[k] = Malha(Pecas(i), i.ToString());
            return _malhas[k];
        }

        public static Texture2D IconeDo(Item i)
        {
            int k = Indice(i);
            if (k < 0) return null;
            if (_icones[k] == null) _icones[k] = Desenho(Pecas(i), 128, 128, "Icone_" + i);
            return _icones[k];
        }

        public static Texture2D FotoDo(Item i)
        {
            int k = Indice(i);
            if (k < 0) return null;
            if (_fotos[k] == null) _fotos[k] = Desenho(Pecas(i), 600, 500, "Foto_" + i);
            return _fotos[k];
        }

        // ------------------------------------------------------------------ a malha (as faces chapadas, a cor pela paleta)

        static readonly Vector3[] Normais = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        static Mesh Malha(Caixa[] caixas, string nome)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            foreach (var c in caixas)
            {
                var cor = new Vector2((c.px + 0.5f) / 16f, 1f - (c.py + 0.5f) / 16f);
                foreach (var normal in Normais)
                {
                    Cantos(c, normal, out var a, out var b, out var d, out var e);
                    int i0 = v.Count;
                    v.Add(a); v.Add(b); v.Add(d); v.Add(e);
                    var girada = Girada(c, normal);
                    for (int k = 0; k < 4; k++) { n.Add(girada); uv.Add(cor); }
                    t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
                    t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
                }
            }
            var m = new Mesh { name = "Caca_" + nome };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        // os 4 cantos da face (no sentido horário visto de fora, como a Unity quer)
        static void Cantos(Caixa c, Vector3 normal, out Vector3 a, out Vector3 b, out Vector3 d, out Vector3 e)
        {
            Vector3 h = c.tamanho * 0.5f;
            Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(normal, u);
            Vector3 meio = c.centro + Vector3.Scale(normal, h);
            Vector3 du = Vector3.Scale(u, h), dw = Vector3.Scale(w, h);
            a = meio - du - dw;
            b = meio + du - dw;
            d = meio + du + dw;
            e = meio - du + dw;
            // o sentido: na Unity, a frente do triângulo (a, b, d) é para onde (b - a) × (d - a) aponta (o horário visto de fora)
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), normal) < 0f) { var x = b; b = e; e = x; }
            // a caixa girada: os cantos giram em volta do centro dela (o sentido não muda)
            a = PontoGirado(c, a); b = PontoGirado(c, b); d = PontoGirado(c, d); e = PontoGirado(c, e);
        }

        // ------------------------------------------------------------------ o ícone e a foto (desenhados das caixinhas)

        // as cores das células usadas (as mesmas da Art/Paleta.png, que não está nos Resources nem fica legível no jogo)
        static Color32 CorDaPaleta(int px, int py) => (px, py) switch
        {
            (10, 3) => new Color32(0x8a, 0x2e, 0x26, 255),
            (10, 9) => new Color32(0xd8, 0xd2, 0xc0, 255),
            (6, 11) => new Color32(0xc8, 0x30, 0x2a, 255),
            (1, 14) => new Color32(0x6b, 0x3e, 0x26, 255),
            (7, 2) => new Color32(0xa9, 0x60, 0x2c, 255),
            (2, 0) => new Color32(0x4a, 0x28, 0x18, 255),
            (12, 4) => new Color32(0xa7, 0x7d, 0x4f, 255),
            (5, 14) => new Color32(0x4a, 0x3b, 0x30, 255),
            (3, 2) => new Color32(0x7a, 0x4a, 0x34, 255),
            (12, 8) => new Color32(0xc8, 0xc2, 0xa8, 255),
            // (18/11/2026: a madeira dos gravetos e do espeto, e o metal da panela)
            (7, 3) => new Color32(0x6b, 0x4a, 0x36, 255),
            (8, 3) => new Color32(0x7e, 0x5b, 0x44, 255),
            (13, 4) => new Color32(0xb4, 0x9f, 0x73, 255),
            (1, 3) => new Color32(0x33, 0x36, 0x3b, 255),
            (4, 3) => new Color32(0x6c, 0x70, 0x75, 255),
            (0, 3) => new Color32(0x26, 0x28, 0x2c, 255),
            (6, 3) => new Color32(0x20, 0x22, 0x25, 255),
            // (o salmão)
            (2, 8) => new Color32(0x9a, 0x9c, 0x98, 255),
            (6, 6) => new Color32(0x3e, 0x4a, 0x5a, 255),
            (13, 11) => new Color32(0xd8, 0x7a, 0x8a, 255),
            (3, 10) => new Color32(0x6a, 0x6a, 0x64, 255),
            (2, 3) => new Color32(0x1b, 0x1c, 0x1f, 255),
            // (os instrumentos)
            (14, 3) => new Color32(0xa3, 0xa6, 0xa4, 255),
            _ => new Color32(0x80, 0x80, 0x80, 255),
        };

        static Texture2D Desenho(Caixa[] caixas, int largura, int altura, string nome)
        {
            // a vista: de frente-direita e de cima (como os ícones), ortográfica
            var giro = Quaternion.Euler(28f, -35f, 0f);
            var paraTela = Quaternion.Inverse(giro);
            Vector3 olho = giro * Vector3.back;   // de onde se olha (para a câmera)

            // os limites na tela, para caber com margem
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var c in caixas)
                for (int k = 0; k < 8; k++)
                {
                    var p = paraTela * PontoGirado(c, c.centro + Vector3.Scale(c.tamanho * 0.5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                    y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                }
            float escala = Mathf.Min(largura * 0.8f / (x1 - x0), altura * 0.8f / (y1 - y0));
            Vector2 meio = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

            var px = new Color32[largura * altura];
            var fundo = new float[largura * altura];
            for (int i = 0; i < fundo.Length; i++) fundo[i] = float.MaxValue;

            Vector2 Tela(Vector3 p, out float z)
            {
                var q = paraTela * p;
                z = q.z;
                return new Vector2((q.x - meio.x) * escala + largura * 0.5f, (q.y - meio.y) * escala + altura * 0.5f);
            }

            foreach (var c in caixas)
            {
                var cor = CorDaPaleta(c.px, c.py);
                foreach (var reta in Normais)
                {
                    var normal = Girada(c, reta);
                    float luz = Vector3.Dot(normal, olho);
                    if (luz <= 0.01f) continue;
                    float sombra = normal.y > 0.5f ? 1.12f : normal.x > 0.5f || normal.x < -0.5f ? 0.72f : 0.92f;
                    var tinta = new Color32((byte)Mathf.Clamp(cor.r * sombra, 0, 255), (byte)Mathf.Clamp(cor.g * sombra, 0, 255), (byte)Mathf.Clamp(cor.b * sombra, 0, 255), 255);
                    Cantos(c, reta, out var a, out var b, out var d, out var e);
                    var q = new[] { Tela(a, out float za), Tela(b, out float zb), Tela(d, out float zd), Tela(e, out float ze) };
                    Preencher(px, fundo, largura, altura, q, new Vector3(za, zb, zd), tinta);
                }
            }

            var tex = new Texture2D(largura, altura, TextureFormat.RGBA32, false) { name = nome, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // pinta o quadrilátero convexo (a face), o mais perto da câmera por cima: a profundidade de cada ponto pelo plano da
        // face (os três primeiros cantos, 'zs')
        static void Preencher(Color32[] px, float[] fundo, int largura, int altura, Vector2[] q, Vector3 zs, Color32 tinta)
        {
            Vector2 e1 = q[1] - q[0], e2 = q[2] - q[0];
            float det = e1.x * e2.y - e1.y * e2.x;
            if (Mathf.Abs(det) < 1e-6f) return;
            int xa = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(q[0].x, q[1].x), Mathf.Min(q[2].x, q[3].x))));
            int xb = Mathf.Min(largura - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(q[0].x, q[1].x), Mathf.Max(q[2].x, q[3].x))));
            int ya = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(q[0].y, q[1].y), Mathf.Min(q[2].y, q[3].y))));
            int yb = Mathf.Min(altura - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(q[0].y, q[1].y), Mathf.Max(q[2].y, q[3].y))));
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                int sinal = 0;
                bool dentro = true;
                for (int k = 0; k < 4 && dentro; k++)
                {
                    Vector2 a = q[k], b = q[(k + 1) % 4];
                    float cruz = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                    int lado = cruz > 0f ? 1 : cruz < 0f ? -1 : 0;
                    if (lado == 0) continue;
                    if (sinal == 0) sinal = lado;
                    else if (lado != sinal) dentro = false;
                }
                if (!dentro) continue;
                // p = q0 + s·e1 + t·e2 (a face é plana: a profundidade varia igual)
                Vector2 r = p - q[0];
                float s = (r.x * e2.y - r.y * e2.x) / det, tt = (e1.x * r.y - e1.y * r.x) / det;
                float z = zs.x + s * (zs.y - zs.x) + tt * (zs.z - zs.x);
                int i = y * largura + x;
                if (z >= fundo[i] - 1e-5f) continue;
                fundo[i] = z;
                px[i] = tinta;
            }
        }
    }
}
