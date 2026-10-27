using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>A regra que dá a cor de uma face pela normal e pelo centro dela (nas coordenadas da obra).</summary>
    public delegate string RegraDeCor(Vector3 normal, Vector3 centro);

    /// <summary>O tamanho de uma malha pedida ao estoque (<see cref="MalhaDeObra.Emprestar"/>).</summary>
    public enum TamanhoDaMalha { Pequena, Media, Grande }

    /// <summary>A cor de uma face: um nome da Paleta ou uma regra (como as funções de cor do `cidade_kit.py`).</summary>
    public readonly struct Cor
    {
        readonly string _nome;
        readonly RegraDeCor _regra;

        Cor(string nome, RegraDeCor regra) { _nome = nome; _regra = regra; }

        public static implicit operator Cor(string nome) => new Cor(nome, null);
        public static implicit operator Cor(RegraDeCor regra) => new Cor(null, regra);
        public static Cor Por(RegraDeCor regra) => new Cor(null, regra);

        /// <summary>Uma cor em cima, outra embaixo e outra nos lados.</summary>
        public static Cor PorNormal(string cima, string lados, string baixo = null) =>
            new Cor(null, (n, c) => n.y > 0.7f ? cima : (n.y < -0.7f ? (baixo ?? lados) : lados));

        public string Da(Vector3 normal, Vector3 centro) => _regra != null ? _regra(normal, centro) : _nome;
    }

    /// <summary>
    /// Uma malha em construção, no estilo do kit da cidade: peças simples (caixa, cilindro, sólido, esfera) com
    /// sombreamento plano (cada face com os seus vértices) e o UV de cada face no quadradinho da cor dela na Paleta.
    /// Tudo nas coordenadas da Unity (x à direita, y para cima, a frente em -z). Não usa nada da cena: dá para montar
    /// fora da thread principal e só criar o Mesh (ParaMesh) na principal.
    /// </summary>
    public sealed class MalhaDeObra
    {
        readonly List<Vector3> _v, _n;
        readonly List<Vector2> _uv;
        readonly List<int> _t;
        // a altura de cada vértice sobre o chão da obra, para a umidade na base das paredes no shader do abandono (Parte
        // M9c): só nas malhas que juntam obras com 'altura' (o desenho das casas de um bloco), em centímetros nos dois
        // primeiros bytes da cor (assim a interpolação na face continua certa); vazia nas outras
        readonly List<Color32> _alt = new List<Color32>(0);
        static readonly Color32 SemAltura = new Color32(255, 255, 0, 255);
        readonly List<Vector3> _p = new List<Vector3>(8), _tampa = new List<Vector3>(8);
        Vector3[] _esfera;
        // o estoque de onde ela veio (e para onde volta)
        TamanhoDaMalha _tamanho;

        public int Triangulos => _t.Count / 3;
        public int Vertices => _v.Count;

        /// <summary>Uma malha vazia com lugar para 'vertices' vértices (as listas crescem se precisar).</summary>
        public MalhaDeObra(int vertices = 4096)
        {
            _v = new List<Vector3>(vertices);
            _n = new List<Vector3>(vertices);
            _uv = new List<Vector2>(vertices);
            _t = new List<int>(vertices * 3 / 2);
        }

        // o estoque das malhas soltas (Parte M6): as listas guardam a capacidade de uma vez para outra. Montando as casas
        // em outra thread, cada casa jogava fora uns 4 MB (as listas crescendo e a malha dela depois de juntada no bloco),
        // e a coleta de lixo dava picos acima de 33 ms com o jogador passando rápido pela cidade.
        // Em três estoques, um por tamanho pedido: cada malha volta para o estoque de onde saiu, e sai dele a maior que
        // houver (o corte das alocações, no fim da M6). Com um estoque só, de 16 malhas, um bloco do centro jogava fora
        // uns 6,6 MB: os prédios têm uma malha de janelas escuras por andar (de poucos vértices), e a casa que pegava uma
        // delas crescia as listas de novo até os 30 mil vértices dela; e o desenho de um bloco com prédios (até ~140 mil
        // vértices) pegava um que nunca tinha crescido. Separados pela capacidade, a casca de um prédio (mais de 65 mil
        // vértices) ia para o estoque das grandes e disputava com o desenho dos blocos
        static readonly List<MalhaDeObra>[] Estoques = { new List<MalhaDeObra>(), new List<MalhaDeObra>(), new List<MalhaDeObra>() };
        // por tamanho: até quantas o estoque guarda e com quantos vértices uma nova começa
        static readonly int[] MaxNoEstoque = { 192, 48, 10 }, Comeca = { 1024, 16384, 1 << 16 };
        const int MaxVerticesNoEstoque = 1 << 19;
        static int _criadas;

        /// <summary>Quantas malhas o estoque teve de criar (as que ele não tinha para emprestar), desde o começo.</summary>
        public static int Criadas => _criadas;

        /// <summary>
        /// Uma malha vazia do estoque do tamanho pedido (a maior que ele tiver) ou uma nova: pequena (as colisões, as
        /// janelas escuras), média (uma casa, um andar, as ruas de um bloco) ou grande (o desenho das casas de um bloco).
        /// Quem pega devolve com <see cref="Devolver"/> quando não precisa mais dela.
        /// </summary>
        public static MalhaDeObra Emprestar(TamanhoDaMalha tamanho = TamanhoDaMalha.Pequena)
        {
            var estoque = Estoques[(int)tamanho];
            lock (Estoques)
                if (estoque.Count > 0)
                {
                    int maior = 0;
                    for (int i = 1; i < estoque.Count; i++)
                        if (estoque[i]._v.Capacity > estoque[maior]._v.Capacity) maior = i;
                    var m = estoque[maior];
                    estoque[maior] = estoque[estoque.Count - 1];
                    estoque.RemoveAt(estoque.Count - 1);
                    return m;
                }
            System.Threading.Interlocked.Increment(ref _criadas);
            return new MalhaDeObra(Comeca[(int)tamanho]) { _tamanho = tamanho };
        }

        /// <summary>Esvazia a malha e a guarda no estoque de onde ela saiu (as muito grandes, ou com o estoque cheio, vão para o lixo).</summary>
        public static void Devolver(MalhaDeObra m)
        {
            if (m == null || m._v.Capacity > MaxVerticesNoEstoque) return;
            m._v.Clear(); m._n.Clear(); m._uv.Clear(); m._t.Clear(); m._alt.Clear();
            var estoque = Estoques[(int)m._tamanho];
            lock (Estoques)
                if (estoque.Count < MaxNoEstoque[(int)m._tamanho]) estoque.Add(m);
        }

        /// <summary>Um polígono plano e convexo, com os pontos em volta (em qualquer sentido); 'fora' diz para que lado ele olha.</summary>
        public void Face(IList<Vector3> p, Vector3 fora, Cor cor)
        {
            int k = p.Count;
            if (k < 3) return;
            // normal pelo método de Newell (aguenta pontos quase alinhados)
            Vector3 n = Vector3.zero, centro = Vector3.zero;
            for (int i = 0; i < k; i++)
            {
                Vector3 a = p[i], b = p[(i + 1) % k];
                n.x += (a.y - b.y) * (a.z + b.z);
                n.y += (a.z - b.z) * (a.x + b.x);
                n.z += (a.x - b.x) * (a.y + b.y);
                centro += a;
            }
            if (n.sqrMagnitude < 1e-14f) return;
            centro /= k;
            bool inverter = Vector3.Dot(n, fora) < 0f;
            if (inverter) n = -n;
            n.Normalize();
            Vector2 uv = Paleta.UV(cor.Da(n, centro));
            int b0 = _v.Count;
            for (int i = 0; i < k; i++)
            {
                _v.Add(p[inverter ? k - 1 - i : i]);
                _n.Add(n);
                _uv.Add(uv);
            }
            // na Unity, a frente do triângulo (a, b, c) é para onde aponta Cross(b - a, c - a)
            for (int i = 1; i < k - 1; i++) { _t.Add(b0); _t.Add(b0 + i); _t.Add(b0 + i + 1); }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 fora, Cor cor)
        {
            _p.Clear(); _p.Add(a); _p.Add(b); _p.Add(c); _p.Add(d);
            Face(_p, fora, cor);
        }

        /// <summary>Uma caixa de tamanho 'tam' (x, y, z) centrada na origem de M.</summary>
        public void Caixa(Matrix4x4 M, Vector3 tam, Cor cor)
        {
            Vector3 h = tam * 0.5f;
            Vector3 P(float x, float y, float z) => M.MultiplyPoint3x4(new Vector3(x * h.x, y * h.y, z * h.z));
            Vector3 c000 = P(-1, -1, -1), c100 = P(1, -1, -1), c010 = P(-1, 1, -1), c110 = P(1, 1, -1);
            Vector3 c001 = P(-1, -1, 1), c101 = P(1, -1, 1), c011 = P(-1, 1, 1), c111 = P(1, 1, 1);
            Quad(c100, c110, c111, c101, M.MultiplyVector(Vector3.right), cor);
            Quad(c000, c001, c011, c010, M.MultiplyVector(Vector3.left), cor);
            Quad(c010, c011, c111, c110, M.MultiplyVector(Vector3.up), cor);
            Quad(c000, c100, c101, c001, M.MultiplyVector(Vector3.down), cor);
            Quad(c001, c101, c111, c011, M.MultiplyVector(Vector3.forward), cor);
            Quad(c000, c010, c110, c100, M.MultiplyVector(Vector3.back), cor);
        }

        /// <summary>Ao longo de +y de M, de 0 a h. r1 = 0 faz um cone.</summary>
        public void Cilindro(Matrix4x4 M, float r0, float r1, float h, int lados, Cor cor, bool tampas = true)
        {
            Vector3 eixo = M.MultiplyVector(Vector3.up);
            Vector3 Anel(int i, float r, float y)
            {
                float a = i * Mathf.PI * 2f / lados;
                return M.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
            Vector3 topo = M.MultiplyPoint3x4(new Vector3(0, h, 0));
            for (int i = 0; i < lados; i++)
            {
                int j = (i + 1) % lados;
                float am = (i + 0.5f) * Mathf.PI * 2f / lados;
                Vector3 fora = M.MultiplyVector(new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am)));
                if (r1 <= 0f)
                {
                    _p.Clear(); _p.Add(Anel(i, r0, 0)); _p.Add(Anel(j, r0, 0)); _p.Add(topo);
                    Face(_p, fora, cor);
                }
                else Quad(Anel(i, r0, 0), Anel(j, r0, 0), Anel(j, r1, h), Anel(i, r1, h), fora, cor);
            }
            if (!tampas) return;
            _p.Clear();
            for (int i = 0; i < lados; i++) _p.Add(Anel(i, r0, 0));
            Face(_p, -eixo, cor);
            if (r1 > 0f)
            {
                _p.Clear();
                for (int i = 0; i < lados; i++) _p.Add(Anel(i, r1, h));
                Face(_p, eixo, cor);
            }
        }

        /// <summary>Um cilindro de a até b.</summary>
        public void Tubo(Vector3 a, Vector3 b, float r, int lados, Cor cor)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Cilindro(Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one), r, r, d.magnitude, lados, cor);
        }

        /// <summary>Dois polígonos convexos paralelos com o mesmo número de pontos, ligados pelos lados.</summary>
        public void Solido(IList<Vector3> A, IList<Vector3> B, Cor cor)
        {
            int k = A.Count;
            Vector3 ca = Vector3.zero, cb = Vector3.zero;
            for (int i = 0; i < k; i++) { ca += A[i]; cb += B[i]; }
            ca /= k; cb /= k;
            Vector3 meio = (ca + cb) * 0.5f;
            _tampa.Clear(); _tampa.AddRange(A);
            Face(_tampa, ca - cb, cor);
            _tampa.Clear(); _tampa.AddRange(B);
            Face(_tampa, cb - ca, cor);
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Vector3 centro = (A[i] + A[j] + B[j] + B[i]) * 0.25f;
                Quad(A[i], A[j], B[j], B[i], centro - meio, cor);
            }
        }

        static readonly Vector3[] Ico;
        static readonly int[] IcoFaces;

        static MalhaDeObra() { Ico = MontarIcosaedro(out IcoFaces); }

        static Vector3[] MontarIcosaedro(out int[] faces)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // uma subdivisão (80 faces), como a icoesfera de subdivisão 1 do Blender
            var meios = new Dictionary<long, int>();
            int Meio(int a, int b)
            {
                long chave = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (meios.TryGetValue(chave, out int id)) return id;
                v.Add(((v[a] + v[b]) * 0.5f));
                meios[chave] = v.Count - 1;
                return v.Count - 1;
            }
            var novas = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Meio(a, b), bc = Meio(b, c), ca = Meio(c, a);
                novas.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            faces = novas.ToArray();
            return v.ToArray();
        }

        /// <summary>Uma esfera baixa (80 faces), com os vértices tremidos (copas, arbustos).</summary>
        public void Esfera(Matrix4x4 M, float raio, Cor cor, float tremer = 0f, System.Random rnd = null)
        {
            var pts = _esfera ??= new Vector3[Ico.Length];
            for (int i = 0; i < Ico.Length; i++)
            {
                Vector3 p = Ico[i] * raio;
                if (tremer > 0f && rnd != null)
                    p += new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1) * tremer;
                pts[i] = M.MultiplyPoint3x4(p);
            }
            Vector3 centro = M.MultiplyPoint3x4(Vector3.zero);
            for (int i = 0; i < IcoFaces.Length; i += 3)
            {
                _p.Clear(); _p.Add(pts[IcoFaces[i]]); _p.Add(pts[IcoFaces[i + 1]]); _p.Add(pts[IcoFaces[i + 2]]);
                Face(_p, (_p[0] + _p[1] + _p[2]) / 3f - centro, cor);
            }
        }

        /// <summary>
        /// Junta outra malha nesta, levada por M (uma posição e um giro), espelhada em x antes se pedir (como o ParaMesh): as
        /// casas de um bloco viram uma malha só (Parte M6). Com 'altura', cada vértice guarda o y dele na outra (a altura
        /// sobre o chão da obra; Parte M9c).
        /// </summary>
        public void Juntar(MalhaDeObra outra, Matrix4x4 M, bool espelhar = false, bool altura = false)
        {
            int b0 = _v.Count;
            if (altura) Encher(b0);
            for (int i = 0; i < outra._v.Count; i++)
            {
                Vector3 p = outra._v[i], n = outra._n[i];
                if (altura) _alt.Add(Altura(p.y));
                if (espelhar) { p.x = -p.x; n.x = -n.x; }
                _v.Add(M.MultiplyPoint3x4(p));
                _n.Add(M.MultiplyVector(n).normalized);
                _uv.Add(outra._uv[i]);
            }
            if (!altura && _alt.Count > 0) Encher(_v.Count);
            var t = outra._t;
            for (int i = 0; i < t.Count; i += 3)
            {
                _t.Add(b0 + t[i]);
                _t.Add(b0 + (espelhar ? t[i + 2] : t[i + 1]));
                _t.Add(b0 + (espelhar ? t[i + 1] : t[i + 2]));
            }
        }

        static Color32 Altura(float y)
        {
            int cm = Mathf.Clamp(Mathf.RoundToInt(y * 100f), 0, 65535);
            return new Color32((byte)(cm >> 8), (byte)(cm & 255), 0, 255);
        }

        // os vértices sem a altura (o que entrou sem 'altura': os caminhos, a calçada do porão) ficam "bem alto" (655 m)
        void Encher(int ate)
        {
            while (_alt.Count < ate) _alt.Add(SemAltura);
        }

        /// <summary>Espelha a malha em x, nela mesma (sem copiar), com o sentido dos triângulos trocado (a frente continua fora).</summary>
        public void Espelhar()
        {
            for (int i = 0; i < _v.Count; i++)
            {
                var p = _v[i];
                _v[i] = new Vector3(-p.x, p.y, p.z);
                var n = _n[i];
                _n[i] = new Vector3(-n.x, n.y, n.z);
            }
            for (int i = 0; i < _t.Count; i += 3) (_t[i + 1], _t[i + 2]) = (_t[i + 2], _t[i + 1]);
        }

        /// <summary>O Mesh da Unity. Espelhar troca x de sinal (e o sentido dos triângulos, para a frente continuar fora).</summary>
        public Mesh ParaMesh(string nome, bool espelhar = false)
        {
            var m = new Mesh { name = nome };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            if (!espelhar)
            {
                m.SetVertices(_v); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetTriangles(_t, 0);
            }
            else
            {
                var v = new List<Vector3>(_v.Count);
                var n = new List<Vector3>(_n.Count);
                foreach (var p in _v) v.Add(new Vector3(-p.x, p.y, p.z));
                foreach (var p in _n) n.Add(new Vector3(-p.x, p.y, p.z));
                var t = new List<int>(_t.Count);
                for (int i = 0; i < _t.Count; i += 3) { t.Add(_t[i]); t.Add(_t[i + 2]); t.Add(_t[i + 1]); }
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, _uv); m.SetTriangles(t, 0);
            }
            if (_alt.Count > 0)
            {
                Encher(_v.Count);
                m.SetColors(_alt);
            }
            m.RecalculateBounds();
            return m;
        }
    }
}
