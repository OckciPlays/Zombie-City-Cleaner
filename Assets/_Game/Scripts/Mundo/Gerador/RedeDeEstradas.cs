using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As estradas no chão (Plano_Mapa, Parte M4): o desenho da M3 vira uma rede de verdade, com a altura do asfalto e o
    /// terreno acomodado embaixo.
    ///  - <b>Sem estrada repetida:</b> no traçado, uma estrada nova anda por cima das que já existem (é mais barato). Aqui
    ///    o pedaço que passa a menos de <see cref="Coberto"/> m de uma estrada anterior sai, e as pontas dos pedaços que
    ///    sobram encostam num ponto dela (o cruzamento). Cada <see cref="Estrada"/> do plano vira um pedaço sem repetição;
    ///    De e Para continuam sendo os da ligação inteira.
    ///  - <b>Os nós e os trechos:</b> um nó em cada ponta de pedaço (os cruzamentos e os lugares); um trecho entre dois nós
    ///    seguidos de um pedaço. É o grafo das placas de direção.
    ///  - <b>A altura do asfalto:</b> o relevo (com o lago) borrado em ~22 m em volta de cada ponto, depois suavizado ao
    ///    longo do pedaço com os nós presos (dois pedaços que se encontram têm a mesma altura no nó), e plano em volta de
    ///    cada nó (<see cref="RaioDoPatio"/>, voltando ao normal em <see cref="Transicao"/> m): no cruzamento, todos os
    ///    pedaços ficam na mesma altura, e as malhas que se cobrem ficam no mesmo plano.
    ///  - <b>O terreno acomodado</b> (<see cref="Acomodar"/>, no relevo final): plano na altura da estrada até
    ///    <see cref="Acostamento"/> m além do asfalto (o acostamento de cascalho), e depois o corte ou o aterro, mais largo
    ///    quanto maior a diferença (uns 30°). Perto de várias estradas, a mais perto manda (é aplicada por último): a beira
    ///    de uma nunca fica embaixo do aterro da outra (nas curvas da serra, os dois lados ficam a 20 m).
    ///  - <b>As plataformas:</b> o chão plano dos postos, na altura da estrada.
    ///  - <b>O guard-rail:</b> nos lados da rodovia e da estrada onde o aterro passa de 1,5 m.
    /// Tudo em dados (seguro em outras threads, igual em todas as máquinas). As malhas saem de <see cref="MalhasDasEstradas"/>.
    /// </summary>
    public sealed class RedeDeEstradas
    {
        /// <summary>A faixa plana de cascalho além do asfalto (m), dos dois lados: no relevo mais grosso de longe, o corte não cobre a estrada.</summary>
        public const float Acostamento = 4f;
        /// <summary>Em volta de cada nó de duas estradas ou mais, elas ficam planas até aqui (m, ao longo dela).</summary>
        public const float RaioDoPatio = 9f;
        public const float Transicao = 30f;
        /// <summary>Quanto o asfalto fica acima do terreno aplainado (m).</summary>
        public const float AcimaDoChao = 0.05f;
        /// <summary>Um ponto a menos disso de uma estrada anterior é dela (m).</summary>
        const float Coberto = 8f;
        /// <summary>O maior corte ou aterro além do acostamento (m).</summary>
        const float MaiorTalude = 34f;

        /// <summary>A meia largura do asfalto (m): rodovia 10 m (2 faixas de 3,5 m e o acostamento asfaltado), estrada 7,5 m, terra 4,5 m.</summary>
        public static float MeiaLargura(TipoDeEstrada t) => t == TipoDeEstrada.Rodovia ? 5f : t == TipoDeEstrada.Estrada ? 3.75f : 2.25f;

        public sealed class No
        {
            public Vector2 Posicao;
            public float Altura;
            /// <summary>O lugar no nó (o índice em <see cref="PlanoDoMapa.Lugares"/>; -1 = um cruzamento).</summary>
            public int Lugar = -1;
            public readonly List<int> Trechos = new List<int>();
        }

        /// <summary>Um pedaço de estrada entre dois nós: os pontos De a Ate da estrada.</summary>
        public sealed class Trecho
        {
            public int Estrada, De, Ate, NoDe, NoAte;
            public float Comprimento;
            public TipoDeEstrada Tipo;
        }

        /// <summary>Um chão plano (o pátio de um posto): o meio, as meias medidas no giro dele, o giro e a altura.</summary>
        public sealed class Plataforma
        {
            public Vector2 Centro, Meia;
            public float Giro, Altura;
            internal float Cos, Sen;
        }

        public readonly List<No> Nos = new List<No>();
        public readonly List<Trecho> Trechos = new List<Trecho>();
        public readonly List<Plataforma> Plataformas = new List<Plataforma>();

        readonly DadosDoMundo _d;
        readonly PlanoDoMapa _plano;
        // as referências de cada bloco: (estrada << 12) | ponto, ou -(plataforma + 1)
        int[][] _porBloco;

        RedeDeEstradas(DadosDoMundo d)
        {
            _d = d;
            _plano = d.Plano;
        }

        /// <summary>Monta a rede a partir do desenho do mapa (depois do lago, do plano e da partida; antes do relevo final).</summary>
        public static RedeDeEstradas Montar(DadosDoMundo d)
        {
            var r = new RedeDeEstradas(d);
            if (d.Plano == null) return r;
            r.Desdobrar();
            r.MontarOsNos();
            r.Alturas();
            r.GuardRails();
            r.PorAsPlataformas();
            r.Indexar();
            return r;
        }

        // ------------------------------------------------------------------ sem estrada repetida

        void Desdobrar()
        {
            var originais = new List<Estrada>(_plano.Estradas);
            _plano.Estradas.Clear();
            var grade = new Dictionary<long, List<int>>();   // os segmentos aceitos, por célula de 16 m
            const float lado = 16f;
            long Chave(int i, int j) => ((long)i << 32) ^ (uint)j;

            void Guardar(int e)
            {
                var pts = _plano.Estradas[e].Pontos;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    int i0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - Coberto) / lado), i1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + Coberto) / lado);
                    int j0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - Coberto) / lado), j1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + Coberto) / lado);
                    for (int j = j0; j <= j1; j++)
                    for (int ii = i0; ii <= i1; ii++)
                    {
                        long k = Chave(ii, j);
                        if (!grade.TryGetValue(k, out var lista)) grade[k] = lista = new List<int>();
                        lista.Add((e << 12) | i);
                    }
                }
            }

            // a distância até a estrada aceita mais perto e o ponto dela mais perto
            float Perto(Vector2 p, out Vector2 ponto)
            {
                ponto = p;
                float melhor = float.MaxValue;
                if (!grade.TryGetValue(Chave(Mathf.FloorToInt(p.x / lado), Mathf.FloorToInt(p.y / lado)), out var lista)) return melhor;
                foreach (int r in lista)
                {
                    var pts = _plano.Estradas[r >> 12].Pontos;
                    int i = r & 4095;
                    float dist = DistanciaAoSegmento(p, pts[i], pts[i + 1], out float t);
                    if (dist < melhor)
                    {
                        melhor = dist;
                        ponto = t < 0.5f ? pts[i] : pts[i + 1];
                    }
                }
                return melhor;
            }

            foreach (var e in originais)
            {
                int n = e.Pontos.Count;
                var coberto = new bool[n];
                for (int i = 0; i < n; i++) coberto[i] = Perto(e.Pontos[i], out _) < Coberto;
                int inicio = 0;
                while (inicio < n)
                {
                    while (inicio < n && coberto[inicio]) inicio++;
                    if (inicio >= n) break;
                    int fim = inicio;
                    while (fim + 1 < n && !coberto[fim + 1]) fim++;
                    var pedaco = new Estrada { Tipo = e.Tipo, De = e.De, Para = e.Para, Conector = e.Conector };
                    bool juntaNoComeco = inicio > 0, juntaNoFim = fim < n - 1;
                    if (juntaNoComeco)
                    {
                        Perto(e.Pontos[inicio - 1], out var j0);
                        pedaco.Pontos.Add(j0);
                    }
                    for (int i = inicio; i <= fim; i++) pedaco.Pontos.Add(e.Pontos[i]);
                    if (juntaNoFim)
                    {
                        Perto(e.Pontos[fim + 1], out var j1);
                        pedaco.Pontos.Add(j1);
                    }
                    int livres = fim - inicio + 1;
                    inicio = fim + 1;
                    for (int i = 1; i < pedaco.Pontos.Count; i++) pedaco.Comprimento += Vector2.Distance(pedaco.Pontos[i - 1], pedaco.Pontos[i]);
                    // um desvio curto entre dois pontos da mesma estrada (a busca saiu e voltou) não vira estrada
                    if (pedaco.Pontos.Count < 2 || pedaco.Comprimento < 20f) continue;
                    if (juntaNoComeco && juntaNoFim && livres < 3 && pedaco.Comprimento < 60f) continue;
                    if (juntaNoComeco && juntaNoFim && pedaco.Pontos[0] == pedaco.Pontos[pedaco.Pontos.Count - 1]) continue;
                    _plano.Estradas.Add(pedaco);
                    Guardar(_plano.Estradas.Count - 1);
                }
            }
        }

        public static float DistanciaAoSegmento(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ o grafo

        readonly Dictionary<Vector2, int> _noEm = new Dictionary<Vector2, int>();

        int No_(Vector2 p)
        {
            if (_noEm.TryGetValue(p, out int id)) return id;
            id = Nos.Count;
            Nos.Add(new No { Posicao = p });
            _noEm[p] = id;
            return id;
        }

        /// <summary>O nó no ponto exato (-1 se não houver).</summary>
        public int NoEm(Vector2 p) => _noEm.TryGetValue(p, out int id) ? id : -1;

        void MontarOsNos()
        {
            var estradas = _plano.Estradas;
            foreach (var e in estradas)
            {
                No_(e.Pontos[0]);
                No_(e.Pontos[e.Pontos.Count - 1]);
            }
            for (int ei = 0; ei < estradas.Count; ei++)
            {
                var pts = estradas[ei].Pontos;
                int de = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    if (i < pts.Count - 1 && !_noEm.ContainsKey(pts[i])) continue;
                    var t = new Trecho { Estrada = ei, De = de, Ate = i, NoDe = _noEm[pts[de]], NoAte = _noEm[pts[i]], Tipo = estradas[ei].Tipo };
                    for (int k = de + 1; k <= i; k++) t.Comprimento += Vector2.Distance(pts[k - 1], pts[k]);
                    int id = Trechos.Count;
                    Trechos.Add(t);
                    Nos[t.NoDe].Trechos.Add(id);
                    Nos[t.NoAte].Trechos.Add(id);
                    de = i;
                }
            }
            // os lugares nos nós (as cidades no meio, os outros no começo da estrada deles)
            for (int li = 0; li < _plano.Lugares.Count; li++)
                if (_noEm.TryGetValue(_plano.Lugares[li].Centro, out int no) && Nos[no].Lugar < 0) Nos[no].Lugar = li;
        }

        // ------------------------------------------------------------------ as alturas

        /// <summary>O relevo (com o lago e o chão das cidades) borrado em volta do ponto, nunca abaixo da beira da água.</summary>
        float Borrada(Vector2 p)
        {
            float soma = 4f * GeradorDoMundo.AlturaDoChao(_d, p.x, p.y), pesos = 4f;
            for (int a = 0; a < 6; a++)
            {
                float ang = a * Mathf.PI / 3f, ang2 = ang + Mathf.PI / 6f;
                soma += GeradorDoMundo.AlturaDoChao(_d, p.x + Mathf.Cos(ang) * 10f, p.y + Mathf.Sin(ang) * 10f);
                soma += 0.5f * GeradorDoMundo.AlturaDoChao(_d, p.x + Mathf.Cos(ang2) * 22f, p.y + Mathf.Sin(ang2) * 22f);
                pesos += 1.5f;
            }
            float h = soma / pesos;
            if (_d.Lago != null && _d.Lago.Q(p.x, p.y) < 1.9f) h = Mathf.Max(h, _d.Lago.Nivel + 0.9f);
            return h;
        }

        // dentro de uma cidade (Parte M5) a estrada fica no chão dela, sem borrar: na beira do cruzamento do portão, a
        // rodovia e a rua da cidade ficam na mesma altura
        bool NaCidade(Vector2 p) => _d.CidadeEm(p.x, p.y) != null;

        float AlturaDoPonto(Vector2 p) => NaCidade(p) ? GeradorDoMundo.AlturaDoChao(_d, p.x, p.y) : Borrada(p);

        void Alturas()
        {
            foreach (var no in Nos) no.Altura = AlturaDoPonto(no.Posicao);
            foreach (var e in _plano.Estradas)
            {
                int n = e.Pontos.Count;
                var h = new float[n];
                // presos e com pátio: os nós de duas estradas ou mais (as que se encontram ali têm a mesma altura e ficam
                // planas em volta: as malhas que se cobrem ficam no mesmo plano). As pontas soltas (o fim de uma saída, um
                // sítio) só seguem o relevo. Dentro das cidades, presos no chão delas e sem pátio (lá os cruzamentos são da cidade)
                var preso = new bool[n];
                var patio = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    int no = NoEm(e.Pontos[i]);
                    bool naCidade = e.Conector || NaCidade(e.Pontos[i]);
                    preso[i] = naCidade || (no >= 0 && Nos[no].Trechos.Count >= 2);
                    patio[i] = !naCidade && no >= 0 && Nos[no].Trechos.Count >= 2;
                    h[i] = no >= 0 ? Nos[no].Altura : AlturaDoPonto(e.Pontos[i]);
                }
                // suaviza ao longo da estrada, com os nós no lugar
                var aux = new float[n];
                for (int it = 0; it < 16; it++)
                {
                    for (int i = 0; i < n; i++)
                        aux[i] = preso[i] || i == 0 || i == n - 1 ? h[i] : 0.25f * h[i - 1] + 0.5f * h[i] + 0.25f * h[i + 1];
                    (h, aux) = (aux, h);
                }
                // o pátio plano em volta de cada cruzamento
                var s = new float[n];
                for (int i = 1; i < n; i++) s[i] = s[i - 1] + Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
                e.AteONo = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float peso = 0f, alvo = h[i], perto = float.MaxValue;
                    for (int k = 0; k < n; k++)
                    {
                        if (!patio[k]) continue;
                        float dist = Mathf.Abs(s[i] - s[k]);
                        perto = Mathf.Min(perto, dist);
                        float w = 1f - Geo.Suave(RaioDoPatio, RaioDoPatio + Transicao, dist);
                        if (w > peso)
                        {
                            peso = w;
                            alvo = h[k];
                        }
                    }
                    e.Alturas.Add(Mathf.Lerp(h[i], alvo, peso));
                    e.AteONo[i] = perto;
                }
            }
        }

        // ------------------------------------------------------------------ o guard-rail

        void GuardRails()
        {
            foreach (var e in _plano.Estradas)
            {
                int n = e.Pontos.Count;
                e.GuardRailEsquerda = new bool[Mathf.Max(0, n - 1)];
                e.GuardRailDireita = new bool[Mathf.Max(0, n - 1)];
                if (e.Tipo == TipoDeEstrada.Terra || e.Conector) continue;
                float fora = MeiaLargura(e.Tipo) + Acostamento + 2.5f;
                for (int lado = 0; lado < 2; lado++)
                {
                    var marca = lado == 0 ? e.GuardRailEsquerda : e.GuardRailDireita;
                    for (int i = 0; i < n - 1; i++)
                    {
                        var meio = (e.Pontos[i] + e.Pontos[i + 1]) * 0.5f;
                        var dir = (e.Pontos[i + 1] - e.Pontos[i]).normalized;
                        var esquerda = new Vector2(-dir.y, dir.x);
                        var p = meio + esquerda * (lado == 0 ? fora : -fora);
                        float estrada = (e.Alturas[i] + e.Alturas[i + 1]) * 0.5f;
                        marca[i] = estrada - GeradorDoMundo.AlturaDoChao(_d, p.x, p.y) > 1.5f && !NaCidade(meio);
                    }
                    // os buracos de um ou dois segmentos são tapados; as pontas soltas de um segmento saem
                    for (int i = 1; i < n - 2; i++)
                        if (!marca[i] && marca[i - 1] && (marca[i + 1] || (i + 2 < n - 1 && marca[i + 2]))) marca[i] = true;
                    for (int i = 0; i < n - 1; i++)
                        if (marca[i] && (i == 0 || !marca[i - 1]) && (i == n - 2 || !marca[i + 1])) marca[i] = false;
                }
            }
        }

        // ------------------------------------------------------------------ as plataformas (os postos)

        void PorAsPlataformas()
        {
            foreach (var l in _plano.Lugares)
            {
                if (l.Tipo != TipoDeLugar.Posto) continue;
                // a altura do ponto de estrada mais perto (o pátio fica no nível da pista)
                float melhor = float.MaxValue, altura = Borrada(l.Centro);
                TipoDeEstrada tipo = TipoDeEstrada.Estrada;
                foreach (var e in _plano.Estradas)
                    for (int i = 0; i < e.Pontos.Count && !e.Conector; i++)
                    {
                        float dist = (e.Pontos[i] - l.Centro).sqrMagnitude;
                        if (dist < melhor)
                        {
                            melhor = dist;
                            altura = e.Alturas[i];
                            tipo = e.Tipo;
                        }
                    }
                // o pátio vai do fundo do posto até a beira do asfalto (a frente olha para a estrada)
                float ate = Mathf.Sqrt(melhor) - MeiaLargura(tipo) - 0.3f, de = -16f;
                float giro = l.Giro * Mathf.Deg2Rad;
                var frente = new Vector2(Mathf.Cos(giro), Mathf.Sin(giro));
                Plataformas.Add(new Plataforma
                {
                    Centro = l.Centro + frente * ((ate + de) * 0.5f), Meia = new Vector2((ate - de) * 0.5f, 17f),
                    Giro = l.Giro, Altura = altura, Cos = frente.x, Sen = frente.y,
                });
            }
        }

        // ------------------------------------------------------------------ a consulta por bloco

        static float Alcance(TipoDeEstrada t) => MeiaLargura(t) + Acostamento + MaiorTalude;
        const float AlcanceDaPlataforma = 30f;

        void Indexar()
        {
            int lado = _d.BlocosPorLado;
            var listas = new List<int>[lado * lado];
            void Por(float x0, float z0, float x1, float z1, int r)
            {
                var a = _d.BlocoDe(x0, z0);
                var b = _d.BlocoDe(x1, z1);
                for (int bz = a.y; bz <= b.y; bz++)
                for (int bx = a.x; bx <= b.x; bx++)
                {
                    int k = bz * lado + bx;
                    (listas[k] ??= new List<int>()).Add(r);
                }
            }
            for (int ei = 0; ei < _plano.Estradas.Count; ei++)
            {
                var e = _plano.Estradas[ei];
                // os caminhos por dentro das cidades não têm malha nem mexem no chão
                if (e.Conector) continue;
                float al = Alcance(e.Tipo);
                for (int i = 0; i + 1 < e.Pontos.Count; i++)
                {
                    Vector2 a = e.Pontos[i], b = e.Pontos[i + 1];
                    Por(Mathf.Min(a.x, b.x) - al, Mathf.Min(a.y, b.y) - al, Mathf.Max(a.x, b.x) + al, Mathf.Max(a.y, b.y) + al, (ei << 12) | i);
                }
            }
            for (int pi = 0; pi < Plataformas.Count; pi++)
            {
                var p = Plataformas[pi];
                float al = Mathf.Max(p.Meia.x, p.Meia.y) + AlcanceDaPlataforma;
                Por(p.Centro.x - al, p.Centro.y - al, p.Centro.x + al, p.Centro.y + al, -(pi + 1));
            }
            _porBloco = new int[listas.Length][];
            for (int k = 0; k < listas.Length; k++) _porBloco[k] = listas[k]?.ToArray();
        }

        /// <summary>As referências do bloco (null se nenhuma estrada passa perto): (estrada &lt;&lt; 12) | ponto, ou -(plataforma + 1).</summary>
        public int[] DoBloco(int bx, int bz) => _porBloco?[bz * _d.BlocosPorLado + bx];

        /// <summary>A estrada e o ponto de uma referência (≥ 0).</summary>
        public static int EstradaDe(int r) => r >> 12;
        public static int PontoDe(int r) => r & 4095;

        // o mais perto de cada estrada (até 12 estradas perto de um ponto) e as influências (estradas e plataformas)
        [System.ThreadStatic] static int[] _quais;
        [System.ThreadStatic] static float[] _dist, _alvo, _pesos, _alvos;

        /// <summary>
        /// O relevo acomodado às estradas e às plataformas: h é a altura do terreno no ponto (com o lago). Cada estrada perto
        /// puxa o chão para a altura dela: tudo até o fim do acostamento, e depois menos, até o fim do corte ou do aterro.
        /// </summary>
        public float Acomodar(float x, float z, float h) => Acomodar(x, z, h, out _);

        /// <summary>O mesmo, dizendo o quanto o ponto está no plano das estradas (1 no asfalto e no acostamento).</summary>
        public float Acomodar(float x, float z, float h, out float noPlano)
        {
            noPlano = 0f;
            var b = _d.BlocoDe(x, z);
            var lista = DoBloco(b.x, b.y);
            if (lista == null) return h;
            _quais ??= new int[12];
            _dist ??= new float[12];
            _alvo ??= new float[12];
            _pesos ??= new float[16];
            _alvos ??= new float[16];
            int quantas = 0, influencias = 0;
            var p = new Vector2(x, z);
            foreach (int r in lista)
            {
                if (r < 0)
                {
                    var pl = Plataformas[-r - 1];
                    float dx = x - pl.Centro.x, dz = z - pl.Centro.y;
                    float u = Mathf.Abs(dx * pl.Cos + dz * pl.Sen) - pl.Meia.x, v = Mathf.Abs(-dx * pl.Sen + dz * pl.Cos) - pl.Meia.y;
                    float fora = Mathf.Sqrt(Mathf.Max(u, 0f) * Mathf.Max(u, 0f) + Mathf.Max(v, 0f) * Mathf.Max(v, 0f));
                    float wp = 1f - Geo.Suave(0f, Mathf.Clamp(Mathf.Abs(h - pl.Altura) * 1.8f, 3f, AlcanceDaPlataforma), fora);
                    if (wp > 0f && influencias < _pesos.Length)
                    {
                        _pesos[influencias] = wp;
                        _alvos[influencias++] = pl.Altura;
                        noPlano = Mathf.Max(noPlano, fora <= 0f ? 1f : 0f);
                    }
                    continue;
                }
                int ei = r >> 12, i = r & 4095;
                var e = _plano.Estradas[ei];
                float dist = DistanciaAoSegmento(p, e.Pontos[i], e.Pontos[i + 1], out float t);
                float alvo = Mathf.Lerp(e.Alturas[i], e.Alturas[i + 1], t);
                int onde = -1;
                for (int k = 0; k < quantas; k++)
                    if (_quais[k] == ei) { onde = k; break; }
                if (onde < 0)
                {
                    if (quantas == _quais.Length) continue;
                    onde = quantas++;
                    _quais[onde] = ei;
                    _dist[onde] = float.MaxValue;
                }
                if (dist < _dist[onde])
                {
                    _dist[onde] = dist;
                    _alvo[onde] = alvo;
                }
            }
            for (int k = 0; k < quantas; k++)
            {
                var tipo = _plano.Estradas[_quais[k]].Tipo;
                float plano = MeiaLargura(tipo) + Acostamento;
                float talude = Mathf.Clamp(Mathf.Abs(h - _alvo[k]) * 1.8f, 3f, MaiorTalude);
                float w = 1f - Geo.Suave(plano, plano + talude, _dist[k]);
                if (w <= 0f || influencias >= _pesos.Length) continue;
                _pesos[influencias] = w;
                _alvos[influencias++] = _alvo[k];
                if (_dist[k] <= plano) noPlano = 1f;
            }
            if (influencias == 0) return h;
            // da mais fraca para a mais forte: a mais perto é aplicada por último e manda no próprio plano
            for (int a = 1; a < influencias; a++)
                for (int c = a; c > 0 && _pesos[c] < _pesos[c - 1]; c--)
                {
                    (_pesos[c], _pesos[c - 1]) = (_pesos[c - 1], _pesos[c]);
                    (_alvos[c], _alvos[c - 1]) = (_alvos[c - 1], _alvos[c]);
                }
            for (int k = 0; k < influencias; k++) h = Mathf.Lerp(h, _alvos[k], _pesos[k]);
            return h;
        }

        /// <summary>
        /// O quanto o ponto é acostamento (de 0 a 1): o cascalho da beira das estradas e o pátio dos postos; deTerra diz
        /// que é a beira de uma estrada de terra (vira terra, não cascalho).
        /// </summary>
        public float Acostado(float x, float z, out bool deTerra)
        {
            deTerra = false;
            var b = _d.BlocoDe(x, z);
            var lista = DoBloco(b.x, b.y);
            if (lista == null) return 0f;
            var p = new Vector2(x, z);
            float maior = 0f;
            foreach (int r in lista)
            {
                if (r < 0)
                {
                    var pl = Plataformas[-r - 1];
                    float dx = x - pl.Centro.x, dz = z - pl.Centro.y;
                    float u = Mathf.Abs(dx * pl.Cos + dz * pl.Sen) - pl.Meia.x, v = Mathf.Abs(-dx * pl.Sen + dz * pl.Cos) - pl.Meia.y;
                    maior = Mathf.Max(maior, 1f - Geo.Suave(-0.5f, 2f, Mathf.Max(u, v)));
                    continue;
                }
                var e = _plano.Estradas[r >> 12];
                int i = r & 4095;
                float dist = DistanciaAoSegmento(p, e.Pontos[i], e.Pontos[i + 1], out _);
                float plano = MeiaLargura(e.Tipo) + Acostamento;
                float w = 1f - Geo.Suave(plano - 0.8f, plano + 1.6f, dist);
                if (w > maior)
                {
                    maior = w;
                    deTerra = e.Tipo == TipoDeEstrada.Terra;
                }
            }
            return maior;
        }

        /// <summary>
        /// A estrada mais perto do ponto (por um bloco de distância): a estrada, a distância ao meio dela e a altura do
        /// asfalto ali. -1 se não houver nenhuma no bloco.
        /// </summary>
        public int MaisPerto(float x, float z, out float distancia, out float altura)
        {
            distancia = float.MaxValue;
            altura = 0f;
            int melhor = -1;
            var b = _d.BlocoDe(x, z);
            var lista = DoBloco(b.x, b.y);
            if (lista == null) return -1;
            var p = new Vector2(x, z);
            foreach (int r in lista)
            {
                if (r < 0) continue;
                var e = _plano.Estradas[r >> 12];
                int i = r & 4095;
                float dist = DistanciaAoSegmento(p, e.Pontos[i], e.Pontos[i + 1], out float t);
                if (dist < distancia)
                {
                    distancia = dist;
                    altura = Mathf.Lerp(e.Alturas[i], e.Alturas[i + 1], t);
                    melhor = r >> 12;
                }
            }
            return melhor;
        }
    }
}
