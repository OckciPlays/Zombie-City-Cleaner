using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma malha com textura montada por código: a posição, a normal, a tangente, o UV e a cor do vértice (o vento do
    /// ArvoreRealista.shader: R = o balanço pela altura, G = a fase, B = o tremor), em duas submalhas (0 o tronco, 1 as
    /// cartas dos galhos), para cada uma ter o seu material.
    /// </summary>
    public sealed class MalhaTexturada
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector4> _t = new List<Vector4>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<int>[] _tri = { new List<int>(), new List<int>() };

        public int Vertices => _v.Count;
        public int Triangulos => (_tri[0].Count + _tri[1].Count) / 3;
        public Vector3 Posicao(int i) => _v[i];

        public int Vertice(Vector3 p, Vector3 n, Vector4 t, Vector2 uv, Color32 c)
        {
            _v.Add(p); _n.Add(n); _t.Add(t); _uv.Add(uv); _c.Add(c);
            return _v.Count - 1;
        }

        /// <summary>Um triângulo (a frente é a ordem horária olhando de fora, como na Unity).</summary>
        public void Tri(int sub, int a, int b, int c)
        {
            _tri[sub].Add(a); _tri[sub].Add(b); _tri[sub].Add(c);
        }

        public void Quad(int sub, int a, int b, int c, int d)
        {
            Tri(sub, a, b, c);
            Tri(sub, a, c, d);
        }

        /// <summary>Um quadrilátero com a frente para o lado de 'frente' (a ordem é virada se preciso).</summary>
        public void QuadVirado(int sub, int a, int b, int c, int d, Vector3 frente)
        {
            var geo = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            if (Vector3.Dot(geo, frente) >= 0f) Quad(sub, a, b, c, d);
            else Quad(sub, a, d, c, b);
        }

        /// <summary>O Mesh (só na thread principal); uma submalha só quando a segunda está vazia.</summary>
        public Mesh Malha(string nome)
        {
            var m = new Mesh { name = nome };
            m.indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetTangents(_t);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            int subs = _tri[1].Count > 0 ? 2 : 1;
            m.subMeshCount = subs;
            for (int s = 0; s < subs; s++) m.SetTriangles(_tri[s], s, true);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>
    /// O pinheiro realista (o spruce da referência do usuário; o Plano_Floresta.md, "Os pinheiros realistas",
    /// 09/11/2026): o tronco em anéis com a textura da casca (reto, afinando, com a base alargada e um pouco torto, e uns
    /// tocos de galho morto na parte nua); a copa em verticilos de galhos, cada galho uma carta dobrada (um Λ raso em
    /// três linhas, caindo e subindo na ponta) com a foto de um galho do atlas (Pinheiro_Galhos.png, fotografado no
    /// Blender), mais compridos embaixo e curtos em cima, com a normal "esférica" (sai do eixo da árvore); e a ponta em
    /// cartas em pé cruzadas. Os LODs: o 0 inteiro; o 1 com metade dos verticilos e as cartas maiores; o 2 um cartaz (duas
    /// cartas cruzadas com a foto da árvore inteira, assada pelo Editor em Pinheiro_Cartazes.png).
    /// </summary>
    public static class PinheiroRealista
    {
        public enum Tamanho { Alto, Medio, Pequeno }

        [Serializable]
        public struct Parametros
        {
            public Tamanho tamanho;
            public int semente;
            [Tooltip("A altura total (m).")] public float altura;
            [Tooltip("A largura da copa (vezes o normal).")] public float largura;
            [Tooltip("Onde a copa começa (fração da altura).")] public float comecoDaCopa;
            [Tooltip("O raio do tronco na base (m).")] public float raioDoTronco;
            [Tooltip("A inclinação e a torção do tronco (0 reto).")] public float torto;
            [Tooltip("Quanto os galhos caem e sobem na ponta (0 retos).")] public float caida;
            [Tooltip("A distância entre os verticilos de galhos (m).")] public float espacoDosVerticilos;
            [Tooltip("Quantos galhos por verticilo (de perto).")] public int galhosPorVerticilo;
            [Tooltip("A versão leve: o tronco de 6 lados, a copa mais alta, menos galhos, as cartas de 2 linhas, sem as penduradas.")] public bool leve;
        }

        public static readonly string[] NomesDosTamanhos = { "PinheiroRealAlto", "PinheiroRealMedio", "PinheiroRealPequeno" };
        public static readonly string[] NomesDosTamanhosLeves = { "PinheiroLeveAlto", "PinheiroLeveMedio", "PinheiroLevePequeno" };
        public const int Variacoes = 5;
        public const int Tamanhos = 3;
        /// <summary>Metros de casca por repetição da textura.</summary>
        public const float EscalaDaCasca = 0.6f;
        /// <summary>No atlas dos galhos (4 × 2 cartas), as 6 primeiras são galhos de lado e as 2 últimas a ponta.</summary>
        public const int CartasDeLado = 6;

        public static string Nome(Tamanho t, int variacao, bool leve = false) => $"{(leve ? NomesDosTamanhosLeves : NomesDosTamanhos)[(int)t]}_{variacao}";
        /// <summary>A semente de cada variação (a leve numa faixa própria: as árvores saem diferentes).</summary>
        public static int Semente(Tamanho t, int variacao, bool leve = false) => (leve ? 3000 : 2000) + (int)t * 100 + variacao;
        /// <summary>O índice de 0 a 14 (a vaga do cartaz no atlas).</summary>
        public static int Indice(Tamanho t, int variacao) => (int)t * Variacoes + (variacao - 1);

        /// <summary>O retângulo (UV) da carta k do atlas dos galhos.</summary>
        public static Rect Carta(int k)
        {
            int i = k % 4, j = k / 4;
            return new Rect(i * 0.25f, 1f - (j + 1) * 0.5f, 0.25f, 0.5f);
        }

        /// <summary>O retângulo (UV) da vaga de um cartaz no atlas dos cartazes (8 × 2 de 512 × 1024).</summary>
        public static Rect Cartaz(int indice)
        {
            int i = indice % 8, j = indice / 8;
            return new Rect(i / 8f, 1f - (j + 1) * 0.5f, 1f / 8f, 0.5f);
        }

        /// <summary>O quadro do cartaz: da base (um pouco abaixo do chão) ao alto, com 5% de folga, na proporção 1 × 2.</summary>
        public static float AlturaDoCartaz(Parametros p) => p.altura * 1.05f;
        public static float LarguraDoCartaz(Parametros p) => AlturaDoCartaz(p) * 0.5f;
        public static float BaseDoCartaz(Parametros p) => -p.altura * 0.025f;
        /// <summary>O raio da copa na base dela.</summary>
        public static float RaioDaCopa(Parametros p) => p.altura * 0.17f * p.largura + 0.25f;

        public static Parametros Sortear(Tamanho t, int semente, bool leve = false)
        {
            var r = new System.Random(semente);
            float Entre(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var p = new Parametros { tamanho = t, semente = semente, leve = leve };
            switch (t)
            {
                case Tamanho.Alto:
                    p.altura = Entre(14f, 20f);
                    p.raioDoTronco = Entre(0.2f, 0.3f);
                    p.comecoDaCopa = leve ? Entre(0.34f, 0.44f) : Entre(0.22f, 0.32f);
                    p.galhosPorVerticilo = leve ? r.Next(5, 8) : r.Next(6, 9);
                    p.espacoDosVerticilos = leve ? Entre(0.55f, 0.68f) : Entre(0.38f, 0.48f);
                    break;
                case Tamanho.Medio:
                    p.altura = Entre(8f, 13f);
                    p.raioDoTronco = Entre(0.13f, 0.19f);
                    p.comecoDaCopa = leve ? Entre(0.3f, 0.4f) : Entre(0.15f, 0.25f);
                    p.galhosPorVerticilo = leve ? r.Next(5, 7) : r.Next(5, 8);
                    p.espacoDosVerticilos = leve ? Entre(0.45f, 0.55f) : Entre(0.32f, 0.4f);
                    break;
                default:
                    p.altura = Entre(3f, 6f);
                    p.raioDoTronco = Entre(0.06f, 0.1f);
                    p.comecoDaCopa = leve ? Entre(0.25f, 0.35f) : Entre(0.08f, 0.15f);
                    p.galhosPorVerticilo = leve ? r.Next(4, 6) : r.Next(4, 7);
                    p.espacoDosVerticilos = leve ? Entre(0.32f, 0.4f) : Entre(0.24f, 0.3f);
                    break;
            }
            p.largura = Entre(0.85f, 1.2f);
            p.caida = leve ? Entre(0.1f, 0.4f) : Entre(0.3f, 0.8f);
            p.torto = Entre(0.1f, 0.8f);
            return p;
        }

        /// <summary>A malha do pinheiro no LOD 0 (inteiro) ou 1 (metade dos verticilos, as cartas maiores).</summary>
        public static Mesh Malha(Parametros p, int lod, string nome)
        {
            var m = new MalhaTexturada();
            Montar(m, p, lod);
            return m.Malha(nome);
        }

        public static int Triangulos(Parametros p, int lod)
        {
            var m = new MalhaTexturada();
            Montar(m, p, lod);
            return m.Triangulos;
        }

        /// <summary>
        /// O cartaz (o LOD 2): duas cartas em pé cruzadas, do tamanho do quadro, com a foto da árvore no atlas; ou, com
        /// 'simples', uma carta só no plano x-y, que o shader vira para a câmera (_Cartaz).
        /// </summary>
        public static Mesh MalhaDoCartaz(Parametros p, Rect uv, string nome, bool simples = false)
        {
            var m = new MalhaTexturada();
            float alt = AlturaDoCartaz(p), larg = LarguraDoCartaz(p), y0 = BaseDoCartaz(p);
            for (int q = 0; q < (simples ? 1 : 2); q++)
            {
                var lat = q == 0 ? Vector3.right : Vector3.forward;
                var normalDoPlano = q == 0 ? Vector3.back : Vector3.right;
                var idx = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    float fc = (k == 0 || k == 3) ? -1f : 1f;      // 0 e 3 à esquerda, 1 e 2 à direita
                    float fy = k < 2 ? 0f : 1f;                    // 0 e 1 embaixo
                    var pos = lat * (fc * larg * 0.5f) + Vector3.up * (y0 + fy * alt);
                    var n = (lat * (fc * 0.35f) + Vector3.up * 0.9f).normalized;
                    var uvv = new Vector2(uv.xMin + (fc + 1f) * 0.5f * uv.width, uv.yMin + fy * uv.height);
                    idx[k] = m.Vertice(pos, n, new Vector4(lat.x, lat.y, lat.z, 1f), uvv, new Color32(Balanco(pos.y, 0.8f), 0, 0, 255));
                }
                m.QuadVirado(0, idx[0], idx[3], idx[2], idx[1], normalDoPlano);
            }
            return m.Malha(nome);
        }

        static byte Balanco(float y, float quanto) => (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(y / 16f), 1.5f) * quanto) * 255f);
        static byte B(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        static void Montar(MalhaTexturada m, Parametros p, int lod)
        {
            var rnd = new System.Random(p.semente * 7 + 3);
            float Entre(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float H = p.altura;
            float fi = Entre(0f, 6.283f);
            var lean = new Vector2(Mathf.Cos(fi), Mathf.Sin(fi));
            var perp = new Vector2(-lean.y, lean.x);
            float fase = Entre(0f, 6.283f);
            Vector3 Eixo(float t)
            {
                float l = p.torto * 0.45f * t * t;
                float s = Mathf.Sin(t * 6.5f + fase) * p.torto * 0.09f * t;
                return new Vector3(lean.x * l + perp.x * s, t * H, lean.y * l + perp.y * s);
            }
            float Raio(float t) => p.raioDoTronco * Mathf.Pow(1f - t, 0.9f) * (1f + 0.6f * Mathf.Exp(-t * 30f)) + 0.012f;

            // ---------------------------------------------------------------- o tronco
            float[] ts = p.leve
                ? (lod == 0 ? new[] { 0f, 0.04f, 0.12f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f } : new[] { 0f, 0.1f, 0.35f, 0.65f, 1f })
                : lod == 0
                    ? new[] { 0f, 0.02f, 0.05f, 0.1f, 0.17f, 0.25f, 0.34f, 0.44f, 0.55f, 0.66f, 0.77f, 0.88f, 0.95f, 1f }
                    : new[] { 0f, 0.05f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
            int lados = p.leve ? (lod == 0 ? 6 : 5) : (lod == 0 ? 9 : 6);
            float voltas = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * p.raioDoTronco * 1.1f / EscalaDaCasca));
            var anel = new int[ts.Length, lados + 1];
            for (int i = 0; i < ts.Length; i++)
            {
                var centro = Eixo(ts[i]);
                float r = Raio(ts[i]);
                for (int k = 0; k <= lados; k++)
                {
                    float a = 2f * Mathf.PI * k / lados;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    anel[i, k] = m.Vertice(centro + radial * r, radial, new Vector4(-Mathf.Sin(a), 0f, Mathf.Cos(a), -1f),
                                           new Vector2(k / (float)lados * voltas, centro.y / EscalaDaCasca),
                                           new Color32(Balanco(centro.y, 0.45f), 0, 0, 255));
                }
            }
            for (int i = 0; i + 1 < ts.Length; i++)
                for (int k = 0; k < lados; k++)
                    m.Quad(0, anel[i, k], anel[i + 1, k], anel[i + 1, k + 1], anel[i, k + 1]);

            float yb = H * p.comecoDaCopa;
            // os tocos de galho morto na parte nua do tronco (só de perto)
            if (lod == 0)
            {
                for (float y = 1.3f + Entre(0f, 0.5f); y < yb - 0.4f; y += (p.leve ? Entre(1.4f, 2.4f) : Entre(0.45f, 0.8f)))
                {
                    float t = y / H;
                    float a = Entre(0f, 6.283f);
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var baseC = Eixo(t) + radial * (Raio(t) * 0.9f);
                    var dir = (radial * Mathf.Cos(25f * Mathf.Deg2Rad) - Vector3.up * Mathf.Sin(25f * Mathf.Deg2Rad) + new Vector3(Entre(-0.2f, 0.2f), 0f, Entre(-0.2f, 0.2f))).normalized;
                    float comp = Entre(0.12f, 0.32f) * Mathf.Clamp(H / 16f, 0.4f, 1.2f);
                    var u = Vector3.Cross(dir, Vector3.up).normalized;
                    var w = Vector3.Cross(u, dir).normalized;
                    var ponta = m.Vertice(baseC + dir * comp, dir, new Vector4(u.x, u.y, u.z, -1f), new Vector2(0.5f, (y + comp) / EscalaDaCasca), new Color32(Balanco(y, 0.45f), 0, 0, 255));
                    var basePts = new int[5];
                    for (int k = 0; k <= 4; k++)
                    {
                        float b = 2f * Mathf.PI * k / 4;
                        var rad = u * Mathf.Cos(b) + w * Mathf.Sin(b);
                        basePts[k] = m.Vertice(baseC + rad * 0.022f, rad, new Vector4(-Mathf.Sin(b), 0f, Mathf.Cos(b), -1f), new Vector2(k / 4f, y / EscalaDaCasca), new Color32(Balanco(y, 0.45f), 0, 0, 255));
                    }
                    for (int k = 0; k < 4; k++) m.Tri(0, basePts[k], ponta, basePts[k + 1]);
                }
            }

            // ---------------------------------------------------------------- os galhos
            float pontaAlt = Mathf.Clamp(H * 0.1f, 0.6f, 2.0f);
            float topo = H - pontaAlt * 0.8f;
            float dy = p.espacoDosVerticilos * (lod == 0 ? 1f : 1.9f);
            float Lmax = RaioDaCopa(p);
            for (float y = yb + dy * 0.5f; y < topo; y += dy)
            {
                float t = Mathf.Clamp01((y - yb) / (topo - yb));
                int n = Mathf.Max(3, Mathf.RoundToInt(p.galhosPorVerticilo * (0.75f + 0.25f * (1f - t)) * (lod == 0 ? 1f : 0.7f)));
                float a0 = Entre(0f, 6.283f);
                for (int k = 0; k < n; k++)
                {
                    float a = a0 + 6.283f * k / n + Entre(-0.25f, 0.25f);
                    float yk = Mathf.Clamp(y + Entre(-0.2f, 0.2f) * dy, yb, topo);
                    float tk = Mathf.Clamp01((yk - yb) / (topo - yb));
                    float L = Lmax * (Mathf.Pow(1f - tk, 0.8f) * 0.85f + 0.15f) * Entre(0.85f, 1.15f) * (lod == 0 ? 1f : 1.1f);
                    float W = 0.5f * L * Entre(0.95f, 1.1f) * (lod == 0 ? 1f : 1.15f);
                    float ang0 = p.leve ? Mathf.Lerp(-8f, 6f, tk) + Entre(-4f, 4f) : Mathf.Lerp(-22f, 4f, tk) + Entre(-6f, 6f);
                    float ang1 = ang0 + (p.leve ? 14f : 22f) * p.caida * (1f - 0.5f * tk) + Entre(-4f, 4f);
                    Galho(m, rnd, Eixo, Raio, H, yk, a, L, W, ang0, ang1, Carta(rnd.Next(CartasDeLado)), lod, tk, (float)rnd.NextDouble(), p.leve ? Entre(-40f, 40f) : Entre(-30f, 30f), p.leve);
                }
            }

            // ---------------------------------------------------------------- a ponta: as cartas em pé cruzadas
            int planos = lod == 0 && !p.leve ? 3 : 2;
            var alto = Eixo(1f);
            var baseP = Eixo(Mathf.Clamp01((H - pontaAlt) / H));
            float altP = pontaAlt * 1.15f, largP = altP * 0.5f;
            var sobe = (alto - baseP).normalized;
            float fq = Entre(0f, 3.14f);
            for (int q = 0; q < planos; q++)
            {
                float aq = fq + Mathf.PI * q / planos;
                var lat = new Vector3(Mathf.Cos(aq), 0f, Mathf.Sin(aq));
                var uv = Carta(CartasDeLado + rnd.Next(2));
                var idx = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    float fc = (k == 0 || k == 3) ? -1f : 1f;
                    float fy = k < 2 ? 0f : 1f;
                    var pos = baseP + sobe * (fy * altP) + lat * (fc * largP * 0.5f);
                    var nrm = (lat * (fc * 0.45f) + Vector3.up * 0.8f).normalized;
                    var uvv = new Vector2(uv.xMin + (fc + 1f) * 0.5f * uv.width, uv.yMin + fy * uv.height);
                    idx[k] = m.Vertice(pos, nrm, new Vector4(lat.x, lat.y, lat.z, 1f), uvv, new Color32(Balanco(pos.y, 1f), B((float)rnd.NextDouble()), B(0.3f + 0.7f * fy), 255));
                }
                m.QuadVirado(1, idx[0], idx[3], idx[2], idx[1], Vector3.Cross(lat, Vector3.up));
            }
        }

        /// <summary>
        /// Um galho: a carta em três linhas (a base no tronco, o meio e a ponta) por três colunas, dobrada num Λ (as
        /// beiradas mais baixas que o meio), caindo da base e subindo na ponta, girada em volta do eixo dela (o rolo); a
        /// normal esférica; o tremor crescendo para a ponta. Nos galhos compridos, uma ou duas cartas penduradas (em pé,
        /// com a ponta para baixo: os raminhos que caem dos galhos do spruce, que dão o volume visto de lado).
        /// </summary>
        static void Galho(MalhaTexturada m, System.Random rnd, Func<float, Vector3> eixo, Func<float, float> raio, float H, float y, float a, float L, float W,
                          float ang0, float ang1, Rect uv, int lod, float t, float fase, float rolo, bool leve)
        {
            float Entre(float p, float q) => p + (float)rnd.NextDouble() * (q - p);
            float ty = Mathf.Clamp01(y / H);
            var centro = eixo(ty);
            var fora = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var lateral = new Vector3(-fora.z, 0f, fora.x);
            Vector3 Dir(float graus) => (fora * Mathf.Cos(graus * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(graus * Mathf.Deg2Rad)).normalized;
            Vector3 d0 = Dir(ang0), d1 = Dir(ang1);
            var b0 = centro + fora * (raio(ty) * 0.7f);
            var b1 = b0 + d0 * (L * 0.5f);
            var b2 = b1 + d1 * (L * 0.5f);
            var rot = Quaternion.AngleAxis(rolo, d0);
            var lat = rot * lateral;
            var nc = Vector3.Cross(lat, d0).normalized;
            if (nc.y < 0f) nc = -nc;
            float dobra = Mathf.Sin((leve ? 26f : 22f) * Mathf.Deg2Rad) * W * 0.5f;
            int linhas = lod == 0 && !leve ? 3 : 2;
            var idx = new int[linhas, 3];
            var nRef = Vector3.zero;
            for (int r = 0; r < linhas; r++)
            {
                float fr = linhas == 3 ? r / 2f : r;
                var pr = fr <= 0.5f ? Vector3.Lerp(b0, b1, fr * 2f) : Vector3.Lerp(b1, b2, (fr - 0.5f) * 2f);
                for (int c = 0; c < 3; c++)
                {
                    float fc = c - 1;
                    float meia = W * 0.5f * (r == linhas - 1 ? 0.9f : 1f) * (r == 0 ? 0.85f : 1f);
                    var pos = pr + lat * (fc * meia) - nc * (dobra * Mathf.Abs(fc));
                    var ax = eixo(Mathf.Clamp01(pos.y / H));
                    var outv = pos - ax;
                    outv.y = 0f;
                    outv = outv.sqrMagnitude < 1e-4f ? fora : outv.normalized;
                    var n = (outv * (1f - 0.35f * t) + Vector3.up * 0.55f).normalized;
                    nRef += n;
                    var uvv = new Vector2(uv.xMin + (fc + 1f) * 0.5f * uv.width, uv.yMin + fr * uv.height);
                    idx[r, c] = m.Vertice(pos, n, new Vector4(lat.x, lat.y, lat.z, 1f), uvv, new Color32(Balanco(pos.y, 1f), B(fase), B(fr), 255));
                }
            }
            for (int r = 0; r + 1 < linhas; r++)
                for (int c = 0; c < 2; c++)
                    m.QuadVirado(1, idx[r, c], idx[r + 1, c], idx[r + 1, c + 1], idx[r, c + 1], nRef);

            // a segunda carta, cruzada (só de perto): a mesma foto girada uns 60° em volta do eixo do galho
            if (lod == 0 && L > 0.4f && (!leve || (L > 0.6f && rnd.NextDouble() < 0.4)))
            {
                var rot2 = Quaternion.AngleAxis(rolo + Entre(50f, 70f) * (rnd.NextDouble() < 0.5 ? 1f : -1f), d0);
                var lat2 = rot2 * lateral;
                var nc2 = Vector3.Cross(lat2, d0).normalized;
                var uv2 = Carta(rnd.Next(CartasDeLado));
                var i2 = new int[2, 3];
                for (int r = 0; r < 2; r++)
                {
                    var pr = r == 0 ? b0 : b2;
                    for (int c = 0; c < 3; c++)
                    {
                        float fc = c - 1;
                        float meia = W * 0.5f * (r == 0 ? 0.75f : 0.85f);
                        var pos = pr + lat2 * (fc * meia) - nc2 * (dobra * 0.6f * Mathf.Abs(fc));
                        var ax = eixo(Mathf.Clamp01(pos.y / H));
                        var outv = pos - ax;
                        outv.y = 0f;
                        outv = outv.sqrMagnitude < 1e-4f ? fora : outv.normalized;
                        var n = (outv * (1f - 0.35f * t) + Vector3.up * 0.55f).normalized;
                        var uvv = new Vector2(uv2.xMin + (fc + 1f) * 0.5f * uv2.width, uv2.yMin + r * uv2.height);
                        i2[r, c] = m.Vertice(pos, n, new Vector4(lat2.x, lat2.y, lat2.z, 1f), uvv, new Color32(Balanco(pos.y, 1f), B(fase), B(r), 255));
                    }
                }
                for (int c = 0; c < 2; c++) m.QuadVirado(1, i2[0, c], i2[1, c], i2[1, c + 1], i2[0, c + 1], nRef);
            }

            // as cartas penduradas
            int penduradas = leve || L < 0.55f ? 0 : lod == 0 ? (L > 1.4f ? 2 : 1) : (L > 1.4f ? 1 : 0);
            for (int q = 0; q < penduradas; q++)
            {
                float f = penduradas == 2 ? (q == 0 ? 0.42f : 0.78f) : 0.6f;
                var pr = f <= 0.5f ? Vector3.Lerp(b0, b1, f * 2f) : Vector3.Lerp(b1, b2, (f - 0.5f) * 2f);
                float comp = L * Entre(0.35f, 0.55f) * (0.6f + 0.4f * (1f - t));
                float larg = comp * Entre(0.45f, 0.6f);
                var horiz = Quaternion.AngleAxis(Entre(-30f, 30f), Vector3.up) * new Vector3(d0.x, 0f, d0.z).normalized;
                var tilt = horiz * (Entre(-0.1f, 0.1f) * comp) + fora * (-0.08f * comp);
                var uvP = Carta(rnd.Next(CartasDeLado));
                float faseP = (float)rnd.NextDouble();
                var ip = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    float fc = (k == 0 || k == 3) ? -1f : 1f;
                    float fy = k < 2 ? 1f : 0f;                 // 0 e 1 em cima (presos ao galho), 2 e 3 embaixo
                    var pos = pr + horiz * (fc * larg * 0.5f) - Vector3.up * ((1f - fy) * comp) + tilt * (1f - fy);
                    var ax = eixo(Mathf.Clamp01(pos.y / H));
                    var outv = pos - ax;
                    outv.y = 0f;
                    outv = outv.sqrMagnitude < 1e-4f ? fora : outv.normalized;
                    var n = (outv * (1f - 0.35f * t) + Vector3.up * 0.55f).normalized;
                    // a ponta do galho da foto para baixo: o v da base em cima
                    var uvv = new Vector2(uvP.xMin + (fc + 1f) * 0.5f * uvP.width, uvP.yMin + (1f - fy) * uvP.height);
                    ip[k] = m.Vertice(pos, n, new Vector4(horiz.x, horiz.y, horiz.z, 1f), uvv, new Color32(Balanco(pos.y, 1f), B(faseP), B(0.3f + 0.7f * (1f - fy)), 255));
                }
                m.QuadVirado(1, ip[0], ip[1], ip[2], ip[3], Vector3.Cross(horiz, Vector3.up));
            }
        }
    }
}
