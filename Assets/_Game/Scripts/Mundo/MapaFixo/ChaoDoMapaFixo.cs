using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O chão da cena do mapa fixo (a cena de construir o mapa aos poucos, 29/10/2026): o relevo do desenho de perto e o
    /// que fica em cima dele. Feito uma vez a partir do <see cref="DesenhoDoMapa"/> e do <see cref="RelevoDoDesenho"/>:
    ///  - o perfil de cada estrada (os pontos a cada 8 m, a altura suavizada e, por cima da água, a ponte com as rampas);
    ///  - a altura do chão em qualquer ponto (<see cref="Altura"/>): o relevo, um detalhe pequeno, o leito dos rios finos e o
    ///    chão acomodado às estradas (o asfalto, o acostamento e o corte ou o aterro);
    ///  - as malhas: o terreno de um bloco (<see cref="Terreno"/>, com os pesos das texturas do chão na cor do vértice),
    ///    o relevo de longe (<see cref="Longe"/>, em cores da paleta), a água dos lagos, do brejo e dos rios, as estradas
    ///    com as pontes e a cerca da quarentena;
    ///  - as plantas de um bloco (<see cref="Plantas"/>), pela cobertura da grade.
    /// Código puro: depois de feito, tudo pode rodar em outra thread.
    /// </summary>
    public sealed class ChaoDoMapaFixo
    {
        public const float TamanhoDoBloco = 64f;
        public static readonly int BlocosPorLado = Mathf.CeilToInt(DesenhoDoMapa.Lado / TamanhoDoBloco);
        const float CelulaDoIndice = 128f;
        const float Acostamento = 3f, FolgaDaPonte = 6f;

        public readonly DesenhoDoMapa Desenho;
        public readonly RelevoDoDesenho Relevo;
        readonly Ruido _detalhe, _sorte;

        /// <summary>Uma estrada no chão: os pontos, a altura do asfalto e se o ponto é ponte.</summary>
        public sealed class Estrada
        {
            public ViaDoDesenho Via;
            public Vector2[] Pontos;
            public float[] Alturas;
            public bool[] Ponte;
            public float MeiaLargura;
        }

        public readonly List<Estrada> Estradas = new List<Estrada>();
        readonly Dictionary<long, List<(int e, int s)>> _estradas = new Dictionary<long, List<(int, int)>>();
        readonly Dictionary<long, List<(int r, int s)>> _rios = new Dictionary<long, List<(int, int)>>();
        readonly List<List<Vector2>> _lagos = new List<List<Vector2>>(), _ilhas = new List<List<Vector2>>();

        public ChaoDoMapaFixo(DesenhoDoMapa d, RelevoDoDesenho relevo)
        {
            Desenho = d;
            Relevo = relevo;
            _detalhe = new Ruido(d.semente, "detalhe-do-mapa-fixo");
            _sorte = new Ruido(d.semente, "plantas-do-mapa-fixo");
            foreach (var l in d.lagos) _lagos.Add(l.contorno.Count >= 3 ? DesenhoDoMapa.Contorno(l.contorno) : null);
            foreach (var i in d.ilhas) _ilhas.Add(i.contorno.Count >= 3 ? DesenhoDoMapa.Contorno(i.contorno) : null);
            for (int r = 0; r < d.rios.Count; r++)
            {
                var linha = relevo.LinhasDosRios[r];
                if (linha == null) continue;
                for (int s = 1; s < linha.Count; s++) Indexar(_rios, linha[s - 1], linha[s], DesenhoDoMapa.LarguraDoRio(d.rios[r], 1f) * 0.5f + 30f, (r, s));
            }
            GradeDaAgua();
            foreach (var via in d.estradas)
            {
                if (via.pontos.Count < 2) continue;
                var e = PerfilDaEstrada(via);
                int k = Estradas.Count;
                Estradas.Add(e);
                for (int s = 1; s < e.Pontos.Length; s++) Indexar(_estradas, e.Pontos[s - 1], e.Pontos[s], e.MeiaLargura + 40f, (k, s));
            }
        }

        public static float MeiaLarguraDe(TipoDeVia t) => t switch { TipoDeVia.Interestadual => 6.5f, TipoDeVia.Estadual => 4.2f, TipoDeVia.Condado => 3.5f, _ => 2.4f };

        // ------------------------------------------------------------------ o índice (os segmentos de cada célula de 128 m)

        static long Chave(int i, int j) => ((long)i << 32) ^ (uint)j;

        static void Indexar<T>(Dictionary<long, List<T>> indice, Vector2 a, Vector2 b, float folga, T item)
        {
            int i0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - folga) / CelulaDoIndice), i1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + folga) / CelulaDoIndice);
            int j0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - folga) / CelulaDoIndice), j1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + folga) / CelulaDoIndice);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                long k = Chave(i, j);
                if (!indice.TryGetValue(k, out var l)) indice[k] = l = new List<T>();
                l.Add(item);
            }
        }

        static List<T> Celula<T>(Dictionary<long, List<T>> indice, float x, float z) =>
            indice.TryGetValue(Chave(Mathf.FloorToInt(x / CelulaDoIndice), Mathf.FloorToInt(z / CelulaDoIndice)), out var l) ? l : null;

        // ------------------------------------------------------------------ a água

        // a grade da água (12 m): 1 lago, 2 rio, 3 brejo, e a altura da água de cada célula
        const float PassoDaAgua = 12f;
        int _nAgua;
        byte[] _tipoDaAgua;
        float[] _nivelDaAgua;

        void GradeDaAgua()
        {
            int n = _nAgua = Mathf.CeilToInt(DesenhoDoMapa.Lado / PassoDaAgua);
            _tipoDaAgua = new byte[n * n];
            _nivelDaAgua = new float[n * n];
            float x0 = -DesenhoDoMapa.Metade + PassoDaAgua * 0.5f;
            // o brejo (onde o chão fica abaixo da água dele)
            if (!float.IsNaN(Relevo.NivelDoBrejo) && Desenho.TemGrade)
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + i * PassoDaAgua, z = x0 + j * PassoDaAgua;
                    if (Desenho.Fracao(Cobertura.Brejo, x, z) + Desenho.Fracao(Cobertura.AguaDoBrejo, x, z) > 0.3f && Relevo.Altura(x, z) < Relevo.NivelDoBrejo + 0.2f)
                    {
                        _tipoDaAgua[j * n + i] = 3;
                        _nivelDaAgua[j * n + i] = Relevo.NivelDoBrejo;
                    }
                }
            // os rios, segmento por segmento
            for (int r = 0; r < Desenho.rios.Count; r++)
            {
                var linha = Relevo.LinhasDosRios[r];
                if (linha == null) continue;
                var f = Relevo.FracoesDosRios[r];
                for (int s = 1; s < linha.Count; s++)
                {
                    var a = linha[s - 1];
                    var b = linha[s];
                    float meia = DesenhoDoMapa.LarguraDoRio(Desenho.rios[r], f[s]) * 0.5f;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - meia + DesenhoDoMapa.Metade) / PassoDaAgua) - 1);
                    int i1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + meia + DesenhoDoMapa.Metade) / PassoDaAgua));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - meia + DesenhoDoMapa.Metade) / PassoDaAgua) - 1);
                    int j1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + meia + DesenhoDoMapa.Metade) / PassoDaAgua));
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float dd = Linhas.DistanciaDoSegmento(new Vector2(x0 + i * PassoDaAgua, x0 + j * PassoDaAgua), a, b, out float t);
                        if (dd > Mathf.Max(meia, PassoDaAgua * 0.6f)) continue;
                        _tipoDaAgua[j * n + i] = 2;
                        _nivelDaAgua[j * n + i] = Relevo.NivelDoRio(r, Mathf.Lerp(f[s - 1], f[s], t));
                    }
                }
            }
            // os lagos (por varredura) e as ilhas (tiradas)
            for (int l = 0; l < _lagos.Count; l++)
            {
                if (_lagos[l] == null) continue;
                float nivel = Relevo.NivelDosLagos[l];
                Linhas.Varrer(_lagos[l], x0, x0, PassoDaAgua, n, n, (j, i0, i1) =>
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        _tipoDaAgua[j * n + i] = 1;
                        _nivelDaAgua[j * n + i] = nivel;
                    }
                });
            }
            foreach (var ilha in _ilhas)
                if (ilha != null)
                    Linhas.Varrer(ilha, x0, x0, PassoDaAgua, n, n, (j, i0, i1) =>
                    {
                        for (int i = i0; i <= i1; i++) _tipoDaAgua[j * n + i] = 0;
                    });
        }

        /// <summary>A água no ponto (o lago, o rio ou o brejo) e a altura dela; com a folga, também perto dela.</summary>
        public bool Agua(float x, float z, out float nivel, float folga = 0f)
        {
            int ci = Mathf.FloorToInt((x + DesenhoDoMapa.Metade) / PassoDaAgua), cj = Mathf.FloorToInt((z + DesenhoDoMapa.Metade) / PassoDaAgua);
            int r = Mathf.CeilToInt(folga / PassoDaAgua);
            for (int dj = -r; dj <= r; dj++)
            for (int di = -r; di <= r; di++)
            {
                int i = ci + di, j = cj + dj;
                if (i < 0 || j < 0 || i >= _nAgua || j >= _nAgua) continue;
                int k = j * _nAgua + i;
                if (_tipoDaAgua[k] == 0) continue;
                if (r > 0 && (di * di + dj * dj) * PassoDaAgua * PassoDaAgua > (folga + PassoDaAgua) * (folga + PassoDaAgua)) continue;
                nivel = _nivelDaAgua[k];
                return true;
            }
            nivel = 0f;
            return false;
        }

        // ------------------------------------------------------------------ as estradas

        Estrada PerfilDaEstrada(ViaDoDesenho via)
        {
            var linha = DesenhoDoMapa.Linha(via.pontos);
            var pts = new List<Vector2>();
            for (int s = 1; s < linha.Count; s++)
            {
                var a = linha[s - 1];
                var b = linha[s];
                int partes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 8f));
                for (int q = 0; q < partes; q++) pts.Add(Vector2.Lerp(a, b, q / (float)partes));
            }
            pts.Add(linha[linha.Count - 1]);
            int n = pts.Count;
            var chao = new float[n];
            var agua = new float[n];
            var ponte = new bool[n];
            for (int i = 0; i < n; i++)
            {
                chao[i] = Relevo.Altura(pts[i].x, pts[i].y);
                ponte[i] = Agua(pts[i].x, pts[i].y, out agua[i], MeiaLarguraDe(via.tipo) + 4f);
            }
            // o chão suavizado ao longo da estrada (uns 100 m para cada lado), e a subida limitada
            var h = (float[])chao.Clone();
            for (int passada = 0; passada < 3; passada++)
            {
                var novo = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float soma = 0f, peso = 0f;
                    for (int k = -12; k <= 12; k++)
                    {
                        int j = Mathf.Clamp(i + k, 0, n - 1);
                        float w = 1f - Mathf.Abs(k) / 13f;
                        soma += h[j] * w;
                        peso += w;
                    }
                    novo[i] = soma / peso;
                }
                h = novo;
            }
            // a rampa limitada (6% na interestadual, 8% nas outras): a média do perfil cortado por cima (até o chão) com o
            // aterrado por baixo, as duas com a rampa no limite
            float rampa = via.tipo == TipoDeVia.Interestadual ? 0.06f : 0.08f;
            var cima = (float[])h.Clone();
            var baixo = (float[])h.Clone();
            for (int i = 1; i < n; i++)
            {
                float ds = Vector2.Distance(pts[i - 1], pts[i]) * rampa;
                cima[i] = Mathf.Min(cima[i], cima[i - 1] + ds);
                baixo[i] = Mathf.Max(baixo[i], baixo[i - 1] - ds);
            }
            for (int i = n - 2; i >= 0; i--)
            {
                float ds = Vector2.Distance(pts[i + 1], pts[i]) * rampa;
                cima[i] = Mathf.Min(cima[i], cima[i + 1] + ds);
                baixo[i] = Mathf.Max(baixo[i], baixo[i + 1] - ds);
            }
            for (int i = 0; i < n; i++) h[i] = (cima[i] + baixo[i]) * 0.5f;
            // por cima da água, a ponte (6 m acima dela) com as rampas de 5%
            var minimo = new float[n];
            for (int i = 0; i < n; i++) minimo[i] = ponte[i] ? agua[i] + FolgaDaPonte : float.MinValue;
            for (int i = 1; i < n; i++) minimo[i] = Mathf.Max(minimo[i], minimo[i - 1] - 0.05f * Vector2.Distance(pts[i - 1], pts[i]));
            for (int i = n - 2; i >= 0; i--) minimo[i] = Mathf.Max(minimo[i], minimo[i + 1] - 0.05f * Vector2.Distance(pts[i + 1], pts[i]));
            for (int i = 0; i < n; i++)
            {
                h[i] = Mathf.Max(h[i], minimo[i]);
                // a ponte também onde o asfalto passa alto por cima do chão (o vale fundo): os pilares, sem o aterro
                ponte[i] |= h[i] - chao[i] > 7f;
            }
            return new Estrada { Via = via, Pontos = pts.ToArray(), Alturas = h, Ponte = ponte, MeiaLargura = MeiaLarguraDe(via.tipo) };
        }

        /// <summary>A estrada mais perto (até o alcance): a distância, a altura do asfalto ali e se é ponte.</summary>
        public bool EstradaPerto(float x, float z, float alcance, out float dist, out float altura, out bool ponte, out float meia)
        {
            dist = float.MaxValue;
            altura = 0f;
            ponte = false;
            meia = 0f;
            var l = Celula(_estradas, x, z);
            if (l == null) return false;
            var p = new Vector2(x, z);
            foreach (var (e, s) in l)
            {
                var est = Estradas[e];
                float dd = Linhas.DistanciaDoSegmento(p, est.Pontos[s - 1], est.Pontos[s], out float t);
                if (dd - est.MeiaLargura >= dist - meia) continue;
                dist = dd;
                meia = est.MeiaLargura;
                altura = Mathf.Lerp(est.Alturas[s - 1], est.Alturas[s], t);
                ponte = t < 0.5f ? est.Ponte[s - 1] : est.Ponte[s];
            }
            return dist < meia + alcance;
        }

        // ------------------------------------------------------------------ o chão

        /// <summary>A altura do chão de perto: o relevo, o detalhe, o leito dos rios finos e o chão das estradas.</summary>
        public float Altura(float x, float z)
        {
            float h = Relevo.Altura(x, z);
            var p = new Vector2(x, z);
            // perto dos rios, o leito na largura de verdade (o relevo é de 20 m) e sem o detalhe
            float perto = 1f;
            var rios = Celula(_rios, x, z);
            if (rios != null)
                foreach (var (r, s) in rios)
                {
                    var linha = Relevo.LinhasDosRios[r];
                    float dd = Linhas.DistanciaDoSegmento(p, linha[s - 1], linha[s], out float t);
                    float f = Mathf.Lerp(Relevo.FracoesDosRios[r][s - 1], Relevo.FracoesDosRios[r][s], t);
                    float meia = DesenhoDoMapa.LarguraDoRio(Desenho.rios[r], f) * 0.5f;
                    perto = Mathf.Min(perto, Geo.Suave(meia + 4f, meia + 30f, dd));
                    if (dd < meia) h = Mathf.Min(h, Relevo.NivelDoRio(r, f) - 1.0f - 1.2f * (1f - dd / meia));
                }
            h += perto * (1.1f * _detalhe.Fbm(x / 45f, z / 45f, 2) + 0.3f * _detalhe.Amostra(x / 11f, z / 11f));
            // as estradas: o asfalto, o acostamento e o corte ou o aterro (na ponte, o chão fica como está)
            if (EstradaPerto(x, z, Acostamento + 34f, out float de, out float hr, out bool ponte, out float meiaEst) && !ponte)
            {
                float talude = Mathf.Clamp(Mathf.Abs(h - hr) * 1.8f, 3f, 34f);
                float borda = meiaEst + Acostamento;
                if (de <= borda) h = hr - 0.08f;
                else if (de < borda + talude) h = Mathf.Lerp(hr - 0.08f, h, Geo.Suave(borda, borda + talude, de));
            }
            return h;
        }

        /// <summary>Os pesos das texturas do chão (R grama seca, G terra, B cascalho, A folhas), pela cobertura e pela estrada.</summary>
        public Color32 Pesos(float x, float z, float h)
        {
            var c = Desenho.CoberturaEm(x, z);
            float r = 0.4f, g = 0.1f, b = 0f, a = 0.4f;
            switch (c)
            {
                case Cobertura.Mata: r = 0.35f; g = 0.1f; a = 0.55f; break;
                case Cobertura.Campo: r = 0.85f; g = 0.1f; a = 0.05f; break;
                case Cobertura.Lavoura: r = 0.4f; g = 0.55f; a = 0.05f; break;
                case Cobertura.Rocha: r = 0.2f; g = 0.25f; b = 0.55f; break;
                case Cobertura.Brejo: case Cobertura.AguaDoBrejo: r = 0.2f; g = 0.55f; a = 0.25f; break;
            }
            float n = _sorte.Amostra(x / 60f, z / 60f);
            r += 0.15f * n;
            a -= 0.15f * n;
            if (EstradaPerto(x, z, Acostamento + 2f, out float de, out _, out bool ponte, out float meia) && !ponte && de < meia + Acostamento + 1f)
            {
                r = 0.05f; g = 0.1f; b = 0.85f; a = 0f;
            }
            float s = Mathf.Max(0.01f, r + g + b + a);
            return new Color32((byte)(Mathf.Clamp01(r / s) * 255), (byte)(Mathf.Clamp01(g / s) * 255), (byte)(Mathf.Clamp01(b / s) * 255), (byte)(Mathf.Clamp01(a / s) * 255));
        }

        // ------------------------------------------------------------------ o terreno de um bloco

        public static Vector2 CantoDoBloco(int bx, int bz) => new Vector2(-DesenhoDoMapa.Metade + bx * TamanhoDoBloco, -DesenhoDoMapa.Metade + bz * TamanhoDoBloco);

        /// <summary>
        /// O terreno de um bloco de 64 m, low-poly de faces planas, com os pesos na cor do vértice e as saias de 3 m nas
        /// beiradas (escondem as frestas entre blocos de detalhes diferentes). O passo é a distância entre os pontos (2 a 16 m).
        /// </summary>
        public MalhaDoBloco Terreno(int bx, int bz, float passo)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(TamanhoDoBloco / passo));
            var canto = CantoDoBloco(bx, bz);
            int lado = n + 1;
            var alt = new float[lado * lado];
            var pesos = new Color32[lado * lado];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = canto.x + i * passo, z = canto.y + j * passo;
                float h = Altura(x, z);
                alt[j * lado + i] = h;
                pesos[j * lado + i] = Pesos(x, z, h);
            }
            var v = new List<Vector3>(n * n * 6 + n * 48);
            var c = new List<Color32>(v.Capacity);
            Vector3 P(int i, int j) => new Vector3(canto.x + i * passo, alt[j * lado + i], canto.y + j * passo);
            Color32 C(int i, int j) => pesos[j * lado + i];
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color32 ca, Color32 cb, Color32 cd)
            {
                v.Add(a); v.Add(b); v.Add(d);
                c.Add(ca); c.Add(cb); c.Add(cd);
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (((i + j) & 1) == 0)
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j + 1), C(i, j), C(i, j + 1), C(i + 1, j + 1));
                    Tri(P(i, j), P(i + 1, j + 1), P(i + 1, j), C(i, j), C(i + 1, j + 1), C(i + 1, j));
                }
                else
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j), C(i, j), C(i, j + 1), C(i + 1, j));
                    Tri(P(i + 1, j), P(i, j + 1), P(i + 1, j + 1), C(i + 1, j), C(i, j + 1), C(i + 1, j + 1));
                }
            }
            void Saia(int ia, int ja, int ib, int jb)
            {
                Vector3 a = P(ia, ja), b = P(ib, jb), a2 = a + Vector3.down * 3f, b2 = b + Vector3.down * 3f;
                Color32 ca = C(ia, ja), cb = C(ib, jb);
                Tri(a, b, b2, ca, cb, cb);
                Tri(a, b2, a2, ca, cb, ca);
                Tri(a, b2, b, ca, cb, cb);
                Tri(a, a2, b2, ca, ca, cb);
            }
            for (int q = 0; q < n; q++)
            {
                Saia(q, 0, q + 1, 0);
                Saia(q, n, q + 1, n);
                Saia(0, q, 0, q + 1);
                Saia(n, q, n, q + 1);
            }
            return Fechar(v, c, null);
        }

        // as normais das faces planas, os triângulos em ordem e a caixa
        static MalhaDoBloco Fechar(List<Vector3> v, List<Color32> c, List<Vector2> uv)
        {
            var vertices = v.ToArray();
            var normais = new Vector3[vertices.Length];
            var tri = new int[vertices.Length];
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            for (int k = 0; k < vertices.Length; k += 3)
            {
                var nrm = Vector3.Cross(vertices[k + 1] - vertices[k], vertices[k + 2] - vertices[k]).normalized;
                normais[k] = normais[k + 1] = normais[k + 2] = nrm;
            }
            for (int k = 0; k < vertices.Length; k++)
            {
                tri[k] = k;
                min = Vector3.Min(min, vertices[k]);
                max = Vector3.Max(max, vertices[k]);
            }
            var m = new MalhaDoBloco { Vertices = vertices, Normais = normais, Cores = c?.ToArray(), Triangulos = tri };
            m.Uvs = uv?.ToArray();
            m.Limites = vertices.Length > 0 ? new Bounds((min + max) * 0.5f, max - min) : new Bounds();
            return m;
        }

        // ------------------------------------------------------------------ o relevo de longe

        /// <summary>Os ladrilhos do relevo de longe (de 1,5 km) de cada lado.</summary>
        public const int LadrilhosDeLonge = 10;

        /// <summary>
        /// Um ladrilho do relevo de longe (para ver o mapa sem a neblina): 50 m entre os pontos, em cores sólidas da paleta
        /// pela cobertura (a mata um pouco levantada, como a copa), afundado 6 m (o terreno de perto fica por cima dele).
        /// </summary>
        public MalhaDoBloco Longe(int tx, int tz)
        {
            const float passo = 50f;
            float lado = DesenhoDoMapa.Lado / LadrilhosDeLonge;
            int n = Mathf.RoundToInt(lado / passo);
            var canto = new Vector2(-DesenhoDoMapa.Metade + tx * lado, -DesenhoDoMapa.Metade + tz * lado);
            var alt = new float[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = canto.x + i * passo, z = canto.y + j * passo;
                float h = Relevo.Altura(x, z) - 6f;
                if (Desenho.CoberturaEm(x, z) == Cobertura.Mata && !Agua(x, z, out _)) h += 11f;
                alt[j * (n + 1) + i] = h;
            }
            var v = new List<Vector3>(n * n * 6);
            var uv = new List<Vector2>(n * n * 6);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                Vector3 P(int a, int b) => new Vector3(canto.x + a * passo, alt[b * (n + 1) + a], canto.y + b * passo);
                var meio = new Vector2(canto.x + (i + 0.5f) * passo, canto.y + (j + 0.5f) * passo);
                float hm = Relevo.Altura(meio.x, meio.y);
                string cor = CorDeLonge(meio, hm);
                var u = Paleta.UV(cor);
                Vector3 a0 = P(i, j), a1 = P(i + 1, j), a2 = P(i + 1, j + 1), a3 = P(i, j + 1);
                v.Add(a0); v.Add(a3); v.Add(a2);
                v.Add(a0); v.Add(a2); v.Add(a1);
                for (int k = 0; k < 6; k++) uv.Add(u);
            }
            return Fechar(v, null, uv);
        }

        string CorDeLonge(Vector2 p, float h)
        {
            if (h > 1350f + 120f * _sorte.Amostra(p.x / 300f, p.y / 300f)) return "PlacaBranca";
            switch (Desenho.CoberturaEm(p.x, p.y))
            {
                case Cobertura.Mata:
                {
                    // as copas de longe: os abetos, os pinheiros-brancos e as folhosas (só o abeto, ficava preto)
                    float s = _sorte.Amostra(p.x / 200f, p.y / 200f);
                    return s > 0.35f ? "Folha" : s > -0.1f ? "PinheiroBranco" : s > -0.45f ? "AbetoClaro" : "Abeto";
                }
                case Cobertura.Campo: return "GramaSeca";
                case Cobertura.Lavoura: return _sorte.Amostra(p.x / 150f, p.y / 150f) > 0f ? "Palha" : "Trigo";
                case Cobertura.Rocha: return h > 900f ? "Pedra" : "PedraEscura";
                case Cobertura.Brejo: case Cobertura.AguaDoBrejo: return "Musgo";
                default: return "Terra";
            }
        }

        // ------------------------------------------------------------------ a água

        /// <summary>
        /// A água de um lago (ou do brejo, com lago = -1): uma grade de 24 m na altura da água, nas células dentro do
        /// contorno e duas além (a margem fica acima da água: o chão esconde a beirada). O R da cor é a fundura (de 0 a 3 m).
        /// </summary>
        public MalhaDoBloco AguaDoLago(int lago)
        {
            const float passo = 24f;
            int n = Mathf.CeilToInt(DesenhoDoMapa.Lado / passo);
            float x0 = -DesenhoDoMapa.Metade + passo * 0.5f;
            var marca = new bool[n * n];
            float nivel;
            if (lago >= 0)
            {
                if (_lagos[lago] == null) return null;
                nivel = Relevo.NivelDosLagos[lago];
                Linhas.Varrer(_lagos[lago], x0, x0, passo, n, n, (j, i0, i1) =>
                {
                    for (int i = i0; i <= i1; i++) marca[j * n + i] = true;
                });
            }
            else
            {
                if (float.IsNaN(Relevo.NivelDoBrejo) || !Desenho.TemGrade) return null;
                nivel = Relevo.NivelDoBrejo;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + i * passo, z = x0 + j * passo;
                    marca[j * n + i] = Desenho.Fracao(Cobertura.Brejo, x, z) + Desenho.Fracao(Cobertura.AguaDoBrejo, x, z) > 0.05f && Relevo.Altura(x, z) < nivel + 1.5f;
                }
            }
            var dilatada = new bool[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (!marca[j * n + i]) continue;
                for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a >= 0 && b >= 0 && a < n && b < n) dilatada[b * n + a] = true;
                }
            }
            var v = new List<Vector3>();
            var c = new List<Color32>();
            Color32 Fundura(float x, float z) => new Color32((byte)(Mathf.Clamp01((nivel - Relevo.Altura(x, z)) / 3f) * 255), 0, 0, 255);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (!dilatada[j * n + i]) continue;
                float xa = -DesenhoDoMapa.Metade + i * passo, za = -DesenhoDoMapa.Metade + j * passo, xb = xa + passo, zb = za + passo;
                Vector3 a0 = new Vector3(xa, nivel, za), a1 = new Vector3(xb, nivel, za), a2 = new Vector3(xb, nivel, zb), a3 = new Vector3(xa, nivel, zb);
                Color32 c0 = Fundura(xa, za), c1 = Fundura(xb, za), c2 = Fundura(xb, zb), c3 = Fundura(xa, zb);
                v.Add(a0); v.Add(a3); v.Add(a2); c.Add(c0); c.Add(c3); c.Add(c2);
                v.Add(a0); v.Add(a2); v.Add(a1); c.Add(c0); c.Add(c2); c.Add(c1);
            }
            return v.Count == 0 ? null : Fechar(v, c, null);
        }

        /// <summary>A água de um rio: a faixa ao longo da linha, na largura dele mais 3 m de cada lado, descendo com a água.</summary>
        public MalhaDoBloco AguaDoRio(int rio)
        {
            var linha = Relevo.LinhasDosRios[rio];
            if (linha == null) return null;
            var f = Relevo.FracoesDosRios[rio];
            var v = new List<Vector3>();
            var c = new List<Color32>();
            Vector3 Lado(int i, float sinal)
            {
                var dir = (linha[Mathf.Min(i + 1, linha.Count - 1)] - linha[Mathf.Max(i - 1, 0)]).normalized;
                var lado = new Vector2(-dir.y, dir.x);
                float meia = DesenhoDoMapa.LarguraDoRio(Desenho.rios[rio], f[i]) * 0.5f + 3f;
                var p = linha[i] + lado * meia * sinal;
                return new Vector3(p.x, Relevo.NiveisDosRios[rio][i], p.y);
            }
            var funda = new Color32(255, 0, 0, 255);
            var rasa = new Color32(40, 0, 0, 255);
            for (int i = 0; i + 1 < linha.Count; i++)
            {
                Vector3 a = Lado(i, 1f), b = Lado(i, -1f), d = Lado(i + 1, 1f), e = Lado(i + 1, -1f);
                v.Add(a); v.Add(d); v.Add(e); c.Add(rasa); c.Add(rasa); c.Add(rasa);
                v.Add(a); v.Add(e); v.Add(b); c.Add(rasa); c.Add(rasa); c.Add(rasa);
                // o meio mais fundo: uma segunda faixa estreita por cima não precisa; a fundura vem do rio inteiro
            }
            for (int k = 0; k < c.Count; k++) c[k] = DesenhoDoMapa.LarguraDoRio(Desenho.rios[rio], 1f) > 25f ? funda : rasa;
            return v.Count == 0 ? null : Fechar(v, c, null);
        }

        // ------------------------------------------------------------------ as estradas e as pontes

        /// <summary>
        /// Um pedaço (de "ini" a "fim", nos pontos) de uma estrada: o asfalto com as linhas da beira e a do meio, e onde é ponte,
        /// o tabuleiro grosso, as muretas e os pilares até o chão (a cada 24 m).
        /// </summary>
        public MalhaDeObra Pedaco(int estrada, int ini, int fim)
        {
            var e = Estradas[estrada];
            var m = new MalhaDeObra();
            bool interestadual = e.Via.tipo == TipoDeVia.Interestadual;
            Vector2 Lado(int i)
            {
                var dir = (e.Pontos[Mathf.Min(i + 1, e.Pontos.Length - 1)] - e.Pontos[Mathf.Max(i - 1, 0)]).normalized;
                return new Vector2(-dir.y, dir.x);
            }
            Vector3 P(int i, float off, float y)
            {
                var p = e.Pontos[i] + Lado(i) * off;
                return new Vector3(p.x, e.Alturas[i] + y, p.y);
            }
            void Faixa(int i, float o0, float o1, float y, string cor)
            {
                var q = new[] { P(i, o0, y), P(i, o1, y), P(i + 1, o1, y), P(i + 1, o0, y) };
                m.Face(q, Vector3.up, cor);
            }
            float meia = e.MeiaLargura;
            for (int i = ini; i < fim && i + 1 < e.Pontos.Length; i++)
            {
                Faixa(i, -meia, meia, 0.05f, "Asfalto");
                Faixa(i, -meia + 0.25f, -meia + 0.45f, 0.06f, "Faixa");
                Faixa(i, meia - 0.45f, meia - 0.25f, 0.06f, "Faixa");
                if ((i / 2) % 2 == 0) Faixa(i, -0.08f, 0.08f, 0.06f, interestadual ? "Faixa" : "PlacaAmarela");
                if (!(e.Ponte[i] || e.Ponte[i + 1])) continue;
                // a ponte: o tabuleiro (os lados e o fundo), as muretas e os pilares
                foreach (float s in new[] { -1f, 1f })
                {
                    var lado = new[] { P(i, s * (meia + 0.6f), 0.05f), P(i + 1, s * (meia + 0.6f), 0.05f), P(i + 1, s * (meia + 0.6f), -1.2f), P(i, s * (meia + 0.6f), -1.2f) };
                    m.Face(lado, new Vector3(Lado(i).x * s, 0f, Lado(i).y * s), "Concreto");
                    var a = P(i, s * (meia + 0.35f), 0.5f);
                    var b = P(i + 1, s * (meia + 0.35f), 0.5f);
                    var meio = (a + b) * 0.5f;
                    var dir = b - a;
                    m.Caixa(Matrix4x4.TRS(meio, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward), Vector3.one), new Vector3(0.4f, 0.9f, dir.magnitude + 0.05f), "Concreto");
                }
                var fundo = new[] { P(i, -meia - 0.6f, -1.2f), P(i + 1, -meia - 0.6f, -1.2f), P(i + 1, meia + 0.6f, -1.2f), P(i, meia + 0.6f, -1.2f) };
                m.Face(fundo, Vector3.down, "Concreto");
                if (i % 3 == 0)
                {
                    var p = e.Pontos[i];
                    float chao = Relevo.Altura(p.x, p.y) - 2f;
                    float topo = e.Alturas[i] - 1.2f;
                    if (topo - chao > 1f)
                    {
                        var dir = e.Pontos[Mathf.Min(i + 1, e.Pontos.Length - 1)] - e.Pontos[Mathf.Max(i - 1, 0)];
                        float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        m.Caixa(Matrix4x4.TRS(new Vector3(p.x, (topo + chao) * 0.5f, p.y), Quaternion.Euler(0f, giro, 0f), Vector3.one),
                                new Vector3(meia * 1.6f, topo - chao, 1.2f), "Concreto");
                    }
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ a cerca da quarentena

        /// <summary>A linha da cerca (o contorno da quarentena suavizado, em pontos a cada 3 m).</summary>
        public List<Vector2> LinhaDaCerca()
        {
            var c = DesenhoDoMapa.Contorno(Desenho.quarentena);
            var saida = new List<Vector2>();
            for (int s = 0; s < c.Count; s++)
            {
                var a = c[s];
                var b = c[(s + 1) % c.Count];
                int partes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 3f));
                for (int q = 0; q < partes; q++) saida.Add(Vector2.Lerp(a, b, q / (float)partes));
            }
            return saida;
        }

        /// <summary>
        /// Um pedaço da cerca (os pontos de "ini" a "fim" da linha): os postes a cada 3 m, o tubo de cima, os três arames e o
        /// arame farpado, em pé no chão; onde a cerca cruza uma estrada, ela para (o bloqueio fecha ali).
        /// </summary>
        public MalhaDeObra Cerca(List<Vector2> linha, int ini, int fim)
        {
            var m = new MalhaDeObra();
            for (int i = ini; i < fim; i++)
            {
                var a = linha[i % linha.Count];
                var b = linha[(i + 1) % linha.Count];
                if (EstradaPerto(a.x, a.y, 2f, out _, out _, out _, out _)) continue;
                float ha = Altura(a.x, a.y), hb = Altura(b.x, b.y);
                var pa = new Vector3(a.x, ha, a.y);
                var pb = new Vector3(b.x, hb, b.y);
                m.Caixa(Matrix4x4.Translate(pa + Vector3.up * 1.4f), new Vector3(0.09f, 2.8f, 0.09f), "Metal");
                foreach (float y in new[] { 0.15f, 1.25f, 2.35f, 2.7f, 2.9f })
                {
                    var p0 = pa + Vector3.up * y;
                    var p1 = pb + Vector3.up * y;
                    var meio = (p0 + p1) * 0.5f;
                    var dir = p1 - p0;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    m.Caixa(Matrix4x4.TRS(meio, Quaternion.LookRotation(dir), Vector3.one), new Vector3(0.025f, y > 2.5f ? 0.02f : 0.035f, dir.magnitude), y > 2.5f ? "Grade" : "Metal");
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ as plantas

        /// <summary>
        /// As plantas de um bloco: as árvores numa grade sacudida de 7 m pela cobertura (a mata fechada, com 3/4 de
        /// coníferas; o campo com umas soltas e o capim; a rocha com os abetos ralos e as pedras; o brejo com a taboa e as
        /// árvores mortas; o salgueiro e a bétula perto da água), fora da água e das estradas. Com "chao", também a grama e o
        /// capim baixo (de 3 em 3 m), só para os blocos de perto.
        /// </summary>
        public void Plantas(int bx, int bz, List<PlantaDaFloresta> saida, bool chao)
        {
            var canto = CantoDoBloco(bx, bz);
            var sorte = new System.Random(unchecked((int)Sorteio.Hash(Desenho.semente, bx, bz, 77)));
            float passo = 7f;
            int n = Mathf.RoundToInt(TamanhoDoBloco / passo);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = canto.x + (i + (float)sorte.NextDouble()) * passo, z = canto.y + (j + (float)sorte.NextDouble()) * passo;
                float r = (float)sorte.NextDouble(), r2 = (float)sorte.NextDouble();
                var cob = Desenho.CoberturaEm(x, z);
                float h = Relevo.Altura(x, z);
                float chance = cob switch { Cobertura.Mata => 0.85f, Cobertura.Campo => 0.06f, Cobertura.Rocha => h < 1250f ? 0.22f : 0.04f,
                                            Cobertura.Brejo => 0.3f, Cobertura.AguaDoBrejo => 0.15f, Cobertura.Lavoura => 0.01f, _ => 0f };
                if (r > chance) continue;
                if (Agua(x, z, out _, 4f)) continue;
                if (EstradaPerto(x, z, 9f, out _, out _, out _, out _)) continue;
                float hf = Altura(x, z);
                bool perto = PertoDaAgua(x, z, 40f);
                Especie e;
                if (cob == Cobertura.Brejo || cob == Cobertura.AguaDoBrejo)
                    e = r2 < 0.45f ? Especie.Taboa : r2 < 0.65f ? Especie.MortaA : r2 < 0.8f ? Especie.Salgueiro : r2 < 0.9f ? Especie.TocoA : Especie.ArbustoA;
                else if (cob == Cobertura.Rocha)
                    e = r2 < 0.55f ? Especie.AbetoB : r2 < 0.75f ? Especie.MortaB : r2 < 0.9f ? Especie.PedraA : Especie.PedraC;
                else if (perto && r2 < 0.35f)
                    e = r2 < 0.15f ? Especie.Salgueiro : r2 < 0.25f ? Especie.BetulaA : Especie.BetulaB;
                else if (cob == Cobertura.Campo || cob == Cobertura.Lavoura)
                    e = r2 < 0.4f ? Especie.ArbustoB : r2 < 0.7f ? Especie.PinheiroVermelhoA : r2 < 0.85f ? Especie.CarvalhoA : Especie.BordoVerde;
                else
                {
                    // a mata: 3/4 de coníferas, e os bordos, os carvalhos e as bétulas em manchas
                    float mancha = _sorte.Amostra(x / 260f, z / 260f);
                    if (r2 < 0.75f - 0.2f * mancha)
                    {
                        float q = (float)sorte.NextDouble();
                        e = q < 0.3f ? Especie.AbetoA : q < 0.5f ? Especie.AbetoB : q < 0.6f ? Especie.AbetoC : q < 0.75f ? Especie.PinheiroBrancoA
                          : q < 0.85f ? Especie.PinheiroBrancoB : q < 0.93f ? Especie.PinheiroVermelhoA : Especie.PinheiroVermelhoB;
                    }
                    else
                    {
                        float q = (float)sorte.NextDouble();
                        e = q < 0.2f ? Especie.BordoVerde : q < 0.3f ? Especie.BordoVermelho : q < 0.38f ? Especie.BordoLaranja : q < 0.55f ? Especie.CarvalhoA
                          : q < 0.65f ? Especie.CarvalhoB : q < 0.8f ? Especie.BetulaA : q < 0.88f ? Especie.BetulaB : q < 0.94f ? Especie.MortaA : Especie.TroncoCaidoA;
                    }
                }
                saida.Add(new PlantaDaFloresta { Especie = e, Posicao = new Vector3(x, hf, z), Giro = (float)sorte.NextDouble() * 360f, Escala = 0.8f + 0.45f * (float)sorte.NextDouble() });
            }
            if (!chao) return;
            n = Mathf.RoundToInt(TamanhoDoBloco / 3f);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = canto.x + (i + (float)sorte.NextDouble()) * 3f, z = canto.y + (j + (float)sorte.NextDouble()) * 3f;
                float r = (float)sorte.NextDouble(), r2 = (float)sorte.NextDouble();
                var cob = Desenho.CoberturaEm(x, z);
                float chance = cob switch { Cobertura.Campo => 0.55f, Cobertura.Lavoura => 0.35f, Cobertura.Mata => 0.18f, Cobertura.Brejo => 0.4f, _ => 0f };
                if (r > chance) continue;
                if (Agua(x, z, out _, 2f) || EstradaPerto(x, z, 4.5f, out _, out _, out _, out _)) continue;
                Especie e = cob == Cobertura.Mata ? (r2 < 0.5f ? Especie.SamambaiaA : r2 < 0.75f ? Especie.SamambaiaB : Especie.GramaA)
                          : cob == Cobertura.Brejo ? (r2 < 0.5f ? Especie.Taboa : Especie.CapimB)
                          : r2 < 0.45f ? Especie.GramaA : r2 < 0.75f ? Especie.GramaB : r2 < 0.9f ? Especie.CapimA : Especie.CapimB;
                saida.Add(new PlantaDaFloresta { Especie = e, Posicao = new Vector3(x, Altura(x, z), z), Giro = (float)sorte.NextDouble() * 360f, Escala = 0.8f + 0.5f * (float)sorte.NextDouble() });
            }
        }

        bool PertoDaAgua(float x, float z, float alcance) => Agua(x, z, out _, alcance);
    }
}
