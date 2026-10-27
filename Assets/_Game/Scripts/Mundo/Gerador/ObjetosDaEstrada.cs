using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public enum TipoDeObjeto : byte
    {
        PlacaDeDirecao, PlacaDeEntrada, Carro, Barreira, Cavalete, Cone, CaminhaoMilitar, Sacos, Conteiner, Blindado,
        Alambrado, Posto, PlacaDeAviso,
        // as ruas das cidades (Parte M5)
        Poste, PlacaDeRua, Arvore, Hidrante, Lixeira,
    }

    /// <summary>
    /// Uma coisa na beira da estrada: o tipo, o lugar (x, z; a altura é a do chão, ou a do asfalto se <see cref="NaEstrada"/>),
    /// o giro em y (graus; a frente do modelo, +z, olha para lá: a das placas olha para quem lê), o quanto ela tomba
    /// (graus, em volta da frente) e a variante (a cor, o modelo). As placas guardam as linhas.
    /// </summary>
    public sealed class ObjetoDaEstrada
    {
        public TipoDeObjeto Tipo;
        public Vector2 Posicao;
        public float Giro, Tombo;
        public int Variante;
        public bool NaEstrada;
        /// <summary>Encosta no chão inclinado (os carros fora da pista).</summary>
        public bool NoTalude;
        /// <summary>Fica em cima de outro igual (os contêineres empilhados).</summary>
        public bool EmCima;
        /// <summary>Fica na calçada de uma cidade (<see cref="TracadoDaCidade.AlturaDaCalcada"/> acima do chão).</summary>
        public bool NaCalcada;
        /// <summary>O carro na garagem ou na vaga de uma casa (Parte M6): em cima do chão de concreto dela (5 cm acima do patamar).</summary>
        public bool NaGaragem;
        public List<LinhaDePlaca> Linhas;
    }

    /// <summary>Uma saída numa placa de direção: o trecho da <see cref="RedeDeEstradas"/> e os lugares escritos para ela.</summary>
    public sealed class SaidaDaPlaca
    {
        public int Trecho;
        public List<int> Lugares;
    }

    /// <summary>Uma linha da placa: o nome, a distância (km; negativa = sem distância) e a seta (graus a partir de "em frente", para a direita; null = sem seta).</summary>
    public struct LinhaDePlaca
    {
        public string Texto;
        public float Km;
        public float? Seta;
        /// <summary>A linha riscada à mão (a população da placa de entrada).</summary>
        public bool Riscada;
        /// <summary>O tamanho da letra (1 = normal).</summary>
        public float Tamanho;
    }

    /// <summary>
    /// O que fica na beira das estradas (Plano_Mapa, Parte M4), em dados, igual em todas as máquinas:
    ///  - <b>as placas de direção</b>, verdes como as das rodovias brasileiras: em cada cruzamento, uma para quem chega por
    ///    cada lado, com as outras saídas (a seta) e os lugares por elas, com a distância pelo caminho mais curto. Por
    ///    saída: as cidades por ela (até 3, as mais perto, e sempre a cidade grande mais perto); sem cidade, a vila ou o
    ///    lugar com nome mais perto. Quem segue as placas chega na cidade que procura;
    ///  - <b>as placas de entrada</b> ("Bem-vindo a ...", com a população riscada à mão), em cada estrada que entra numa cidade;
    ///  - <b>as blitz</b> abandonadas em parte das entradas das cidades;
    ///  - <b>a cerca da quarentena</b> em volta do mapa (<see cref="DadosDoMundo.Cerca"/>): o alambrado nos 4 lados, com as
    ///    placas de aviso, e <b>as barreiras</b> onde as saídas cruzam a linha dela (os contêineres fechando o vão, as
    ///    barreiras de concreto, o blindado e os sacos de areia): não tem por onde passar;
    ///  - <b>os postos</b> (o modelo vem de <see cref="ModelosDaEstrada"/>);
    ///  - <b>os carros abandonados</b> nas rodovias e estradas: parados na pista, no acostamento ou batidos fora dela, e
    ///    alguns engavetamentos perto das blitz.
    /// Quem mostra (na cena) cria os objetos dos blocos perto do jogador (<see cref="DoBloco"/>).
    /// </summary>
    public sealed class ObjetosDaEstrada
    {
        public readonly List<ObjetoDaEstrada> Todos = new List<ObjetoDaEstrada>();
        int[][] _porBloco;
        readonly DadosDoMundo _d;
        readonly PlanoDoMapa _plano;
        readonly RedeDeEstradas _rede;
        readonly Sorteio _s;
        // onde já tem algo grande (as blitz, os postos, os cruzamentos): os carros não caem em cima
        readonly List<(Vector2 p, float r)> _ocupado = new List<(Vector2, float)>();

        ObjetosDaEstrada(DadosDoMundo d)
        {
            _d = d;
            _plano = d.Plano;
            _rede = d.Rede;
            _s = Sorteio.Da(d.Semente, "beira-da-estrada");
        }

        public static ObjetosDaEstrada Planejar(DadosDoMundo d)
        {
            var o = new ObjetosDaEstrada(d);
            if (d.Plano != null && d.Rede != null)
            {
                foreach (var no in d.Rede.Nos) o._ocupado.Add((no.Posicao, 26f));
                o.PorOsPostos();
                o.PorAsBarreiras();
                o.PorACerca();
                o.PorAsEntradas();
                o.PorAsPlacasDeDirecao();
                o.PorOsCarros();
                ObjetosDaCidade.Por(d, o.Todos);
            }
            o.Indexar();
            return o;
        }

        /// <summary>Os índices (em <see cref="Todos"/>) dos objetos do bloco, ou null.</summary>
        public int[] DoBloco(int bx, int bz) => _porBloco?[bz * _d.BlocosPorLado + bx];

        void Indexar()
        {
            int lado = _d.BlocosPorLado;
            var listas = new List<int>[lado * lado];
            for (int i = 0; i < Todos.Count; i++)
            {
                var b = _d.BlocoDe(Todos[i].Posicao.x, Todos[i].Posicao.y);
                (listas[b.y * lado + b.x] ??= new List<int>()).Add(i);
            }
            _porBloco = new int[listas.Length][];
            for (int k = 0; k < listas.Length; k++) _porBloco[k] = listas[k]?.ToArray();
        }

        ObjetoDaEstrada Por(TipoDeObjeto tipo, Vector2 p, float giro, int variante = 0, bool naEstrada = false, float tombo = 0f)
        {
            var o = new ObjetoDaEstrada { Tipo = tipo, Posicao = p, Giro = Mathf.Repeat(giro, 360f), Variante = variante, NaEstrada = naEstrada, Tombo = tombo };
            Todos.Add(o);
            return o;
        }

        /// <summary>O giro em y (graus) de quem olha na direção (x, z).</summary>
        public static float GiroDe(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        static Vector2 Direita(Vector2 dir) => new Vector2(dir.y, -dir.x);

        /// <summary>As placas de direção que não acharam lugar sem estrada embaixo (devia ser 0; os testes conferem).</summary>
        public int PlacasSemLugar;

        // o ponto está fora do asfalto de qualquer estrada (com a folga além da beira), bem dentro da cerca e fora das ruas,
        // calçadas e construções das cidades
        bool ForaDasEstradas(Vector2 p, float folga)
        {
            if (!_d.DentroDaCerca(p.x, p.y, 3f) || _d.OcupadoPelaCidade(p, folga)) return false;
            int ei = _rede.MaisPerto(p.x, p.y, out float dist, out _);
            return ei < 0 || dist >= RedeDeEstradas.MeiaLargura(_plano.Estradas[ei].Tipo) + folga;
        }

        // a estrada mais perto do ponto é essa (o carro na pista ou no acostamento não fica em cima de outra)
        bool NaPropria(Vector2 p, Estrada e)
        {
            int ei = _rede.MaisPerto(p.x, p.y, out _, out _);
            return ei >= 0 && _plano.Estradas[ei] == e && _d.DentroDaCerca(p.x, p.y, 3f);
        }

        // o lugar de uma placa para quem anda pela estrada no sentido -sentido a partir do ponto i (as distâncias em ordem
        // de preferência): do lado direito, com a borda de dentro 1,2 m além do asfalto; senão, do lado esquerdo; a chapa
        // inteira fora de qualquer estrada. Devolve o meio da placa e para onde ela olha (para quem chega)
        bool LugarDaPlaca(Estrada e, int i, int sentido, float[] distancias, float largura, out Vector2 onde, out Vector2 olha)
        {
            float meia = RedeDeEstradas.MeiaLargura(e.Tipo);
            foreach (float d in distancias)
            {
                var p = AoLongo(e, i, sentido, d, out var dirFora);
                var dir = -dirFora;   // quem chega anda para o ponto i
                olha = -dir;
                foreach (float lado in new[] { 1f, -1f })
                {
                    onde = p + Direita(dir) * (lado * (meia + 1.2f + largura * 0.5f));
                    var x = Direita(olha);
                    bool livre = true;
                    for (int k = -2; k <= 2 && livre; k++) livre = ForaDasEstradas(onde + x * (largura * 0.25f * k), 0.8f);
                    if (livre) return true;
                }
            }
            onde = olha = Vector2.zero;
            return false;
        }

        bool Livre(Vector2 p, float r)
        {
            foreach (var (q, rq) in _ocupado)
                if ((q - p).sqrMagnitude < (r + rq) * (r + rq)) return false;
            return true;
        }

        // um ponto da estrada a "distancia" m do ponto i, andando na direção "sentido" (+1 ou -1), e a direção de quem anda
        static Vector2 AoLongo(Estrada e, int i, int sentido, float distancia, out Vector2 dir)
        {
            var pts = e.Pontos;
            int k = i;
            float resta = distancia;
            dir = (pts[Mathf.Clamp(i + sentido, 0, pts.Count - 1)] - pts[i]).normalized;
            while (true)
            {
                int prox = k + sentido;
                if (prox < 0 || prox >= pts.Count) return pts[k];
                float trecho = Vector2.Distance(pts[k], pts[prox]);
                dir = (pts[prox] - pts[k]) / Mathf.Max(trecho, 1e-4f);
                if (trecho >= resta) return pts[k] + dir * resta;
                resta -= trecho;
                k = prox;
            }
        }

        // ------------------------------------------------------------------ os postos

        void PorOsPostos()
        {
            foreach (var l in _plano.Lugares)
            {
                if (l.Tipo != TipoDeLugar.Posto) continue;
                // o Giro do lugar é o ângulo do mapa (do leste para o norte) para onde a frente olha: a estrada
                float g = l.Giro * Mathf.Deg2Rad;
                var frente = new Vector2(Mathf.Cos(g), Mathf.Sin(g));
                Por(TipoDeObjeto.Posto, l.Centro, GiroDe(frente), _s.Entre(0, 3));
                _ocupado.Add((l.Centro, 30f));
            }
        }

        // ------------------------------------------------------------------ as barreiras da quarentena

        // os portões da cerca: onde uma estrada cruza a linha dela (as saídas). O lado (em x ou em z, e o sinal), o meio do
        // vão ao longo do lado e a meia largura do vão
        readonly List<(bool emX, float sinal, float meio, float meia)> _portoes = new List<(bool, float, float, float)>();

        // a barreira da quarentena: em cada estrada que cruza a cerca, uma fila de contêineres na linha da cerca fechando
        // o vão da estrada e do acostamento (uns com outro em cima); para dentro, as barreiras de concreto, o blindado, os
        // sacos de areia e um cone derrubado. O resto da linha é a cerca (PorACerca)
        void PorAsBarreiras()
        {
            foreach (var e in _plano.Estradas)
                for (int i = 0; i + 1 < e.Pontos.Count; i++)
                {
                    Vector2 a = e.Pontos[i], b = e.Pontos[i + 1];
                    float fa = NaLinha(a) - _d.Cerca, fb = NaLinha(b) - _d.Cerca;
                    if ((fa < 0f) == (fb < 0f)) continue;
                    var ponto = Vector2.Lerp(a, b, fa / (fa - fb));
                    var paraFora = (fa < 0f ? b - a : a - b).normalized;
                    bool emX = Mathf.Abs(ponto.x) >= Mathf.Abs(ponto.y);
                    var normal = emX ? new Vector2(Mathf.Sign(ponto.x), 0f) : new Vector2(0f, Mathf.Sign(ponto.y));
                    var aoLongo = new Vector2(Mathf.Abs(normal.y), Mathf.Abs(normal.x));
                    // na linha da cerca, o vão é mais largo quanto mais torta a estrada a cruza
                    float cruza = Mathf.Max(Mathf.Abs(Vector2.Dot(paraFora, normal)), 0.4f);
                    float vao = (RedeDeEstradas.MeiaLargura(e.Tipo) + RedeDeEstradas.Acostamento) / cruza + 2f;
                    int quantos = Mathf.CeilToInt(2f * vao / 6.1f);
                    float meia = quantos * 6.1f * 0.5f;
                    float meio = Vector2.Dot(ponto, aoLongo);
                    var centro = emX ? new Vector2(normal.x * _d.Cerca, meio) : new Vector2(meio, normal.y * _d.Cerca);
                    var giro = GiroDe(-normal);   // a frente olha para dentro do mapa
                    int cor = _s.Entre(0, 4);
                    for (int k = 0; k < quantos; k++)
                    {
                        var p = centro + aoLongo * (-meia + 3.05f + k * 6.1f) + normal * _s.Entre(-0.3f, 0.3f);
                        Por(TipoDeObjeto.Conteiner, p, giro + _s.Entre(-1.5f, 1.5f), cor++ % 4, true);
                        if (_s.Chance(0.4f))
                            Por(TipoDeObjeto.Conteiner, p + normal * _s.Entre(-0.3f, 0.3f), giro + _s.Entre(-4f, 4f), _s.Entre(0, 4), true).EmCima = true;
                    }
                    _portoes.Add((emX, emX ? normal.x : normal.y, meio, meia));
                    // para dentro (pela direção da cerca: a estrada pode cruzar torta): as barreiras de concreto
                    // atravessando o vão, o blindado e os sacos de areia
                    var dentro = centro - normal * 5f;
                    for (float u = -meia + 1.5f; u <= meia - 1.4f; u += 2.1f)
                        Por(TipoDeObjeto.Barreira, dentro + aoLongo * u, giro + _s.Entre(-4f, 4f), 0, true);
                    Por(TipoDeObjeto.Blindado, centro - normal * 13f + aoLongo * (meia - 3f), giro + 90f + _s.Entre(-20f, 20f), 0, true);
                    Por(TipoDeObjeto.Sacos, centro - normal * 8f - aoLongo * (meia - 2f), giro + _s.Entre(-10f, 10f), 0, true);
                    Por(TipoDeObjeto.Cone, centro - normal * 15f + aoLongo * _s.Entre(-3f, 3f), _s.Entre(0f, 360f), 0, true, 90f);
                    _ocupado.Add((ponto, 60f));
                }
        }

        // o quanto o ponto está para fora (a distância "quadrada" do meio: a cerca é um quadrado)
        static float NaLinha(Vector2 p) => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));

        // ------------------------------------------------------------------ a cerca da quarentena

        // o alambrado de 3 m em 3 m nos 4 lados do quadrado da cerca, menos nos portões, acompanhando a subida da serra
        // (cada pedaço inclina de uma ponta à outra); a cada 120 m, a placa de aviso virada para dentro
        void PorACerca()
        {
            float c = _d.Cerca;
            for (int lado = 0; lado < 4; lado++)
            {
                bool emX = lado < 2;
                float sinal = (lado & 1) == 0 ? 1f : -1f;
                var normal = emX ? new Vector2(sinal, 0f) : new Vector2(0f, sinal);
                var giro = GiroDe(-normal);
                // o x do modelo (ao longo do pedaço), com a frente para dentro
                var xDoModelo = Direita(-normal);
                // os pedaços cobrem o lado inteiro, de quina a quina (um pouco menos de 3 m cada: o modelo sobra um pouco)
                int pedacos = Mathf.CeilToInt(2f * c / 3f);
                float passo = 2f * c / pedacos;
                for (int n = 0; n < pedacos; n++)
                {
                    float u = -c + passo * (n + 0.5f);
                    // no portão, só sai o pedaço que os contêineres cobrem inteiro (os das pontas entram neles: sem vão)
                    bool noPortao = false;
                    foreach (var pt in _portoes)
                        if (pt.emX == emX && pt.sinal == sinal && Mathf.Abs(u - pt.meio) < pt.meia - 1.5f) noPortao = true;
                    if (noPortao) continue;
                    var p = emX ? new Vector2(sinal * c, u) : new Vector2(u, sinal * c);
                    var p0 = p - xDoModelo * 1.5f;
                    var p1 = p + xDoModelo * 1.5f;
                    float inclina = Mathf.Atan2(_d.Altura(p1.x, p1.y) - _d.Altura(p0.x, p0.y), 3f) * Mathf.Rad2Deg;
                    Por(TipoDeObjeto.Alambrado, p, giro, _s.Chance(0.08f) ? 1 : 0, false, inclina);
                    if (n % 40 == 20)
                    {
                        var placa = Por(TipoDeObjeto.PlacaDeAviso, p - normal * 0.18f, giro, 0, false, inclina);
                        placa.Linhas = new List<LinhaDePlaca>
                        {
                            new LinhaDePlaca { Texto = "ÁREA DE QUARENTENA", Km = -1f, Tamanho = 0.85f },
                            new LinhaDePlaca { Texto = "Proibida a passagem", Km = -1f, Tamanho = 0.75f },
                        };
                    }
                }
            }
        }

        // ------------------------------------------------------------------ as entradas das cidades (as placas e as blitz)

        void PorAsEntradas()
        {
            var feitas = new List<Vector2>();
            for (int ci = 0; ci < _plano.Cidades.Count; ci++)
            {
                var c = _plano.Cidades[ci];
                int populacao = c.Tipo == TipoDeLugar.CidadeGrande ? _s.Entre(38000, 126000)
                              : c.Tipo == TipoDeLugar.CidadePequena ? _s.Entre(3200, 14800) : _s.Entre(240, 1400);
                feitas.Clear();
                int entrada = 0;
                foreach (var e in _plano.Estradas)
                {
                    if (e.Tipo == TipoDeEstrada.Terra) continue;
                    var pts = e.Pontos;
                    for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        float qa = c.Q(pts[i].x, pts[i].y), qb = c.Q(pts[i + 1].x, pts[i + 1].y);
                        bool entra = qa > 1f && qb <= 1f, sai = qa <= 1f && qb > 1f;
                        if (!entra && !sai) continue;
                        // quem chega: anda de fora para dentro
                        int sentido = entra ? 1 : -1;
                        int deFora = entra ? i : i + 1;
                        var p = AoLongo(e, deFora, -sentido, 18f, out var dirFora);
                        var dir = -dirFora;   // a direção de quem entra
                        bool repetida = false;
                        foreach (var f in feitas) repetida |= (f - p).sqrMagnitude < 60f * 60f;
                        if (repetida) continue;
                        feitas.Add(p);
                        var linhas = new List<LinhaDePlaca>
                        {
                            new LinhaDePlaca { Texto = "Bem-vindo a", Km = -1f, Tamanho = 0.6f },
                            new LinhaDePlaca { Texto = c.Nome, Km = -1f, Tamanho = 1.1f },
                            new LinhaDePlaca
                            {
                                Texto = "População " + populacao.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "."),
                                Km = -1f, Tamanho = 0.6f, Riscada = true,
                            },
                        };
                        int variante = c.Tipo == TipoDeLugar.Vila ? 1 : 0;
                        if (LugarDaPlaca(e, deFora, -sentido, new[] { 18f, 30f, 10f, 45f, 60f }, ModelosDaEstrada.LarguraDaPlacaDeEntrada(linhas, variante), out var ondePlaca, out var olha))
                            Por(TipoDeObjeto.PlacaDeEntrada, ondePlaca, GiroDe(olha), variante).Linhas = linhas;
                        // as blitz (abandonadas) em parte das entradas das cidades grandes e pequenas
                        if (c.Tipo != TipoDeLugar.Vila && _s.Chance(0.45f))
                        {
                            // longe da placa de entrada: o caminhão do exército não a cobre
                            var naPista = AoLongo(e, deFora, -sentido, 52f, out _);
                            if (Livre(naPista, 12f) && NaPropria(naPista, e)) Blitz(naPista, dir, e, entrada);
                        }
                        entrada++;
                    }
                }
            }
        }

        // a blitz que não segurou ninguém: as barreiras abertas no meio, o cavalete e os cones derrubados, o caminhão do
        // exército no acostamento, os sacos de areia e uma fila de carros parados chegando nela
        void Blitz(Vector2 p, Vector2 dir, Estrada e, int qual)
        {
            var tipo = e.Tipo;
            var dirt = Direita(dir);
            float meia = RedeDeEstradas.MeiaLargura(tipo);
            float g = GiroDe(dir);
            Por(TipoDeObjeto.Barreira, p + dirt * (-meia + 1.4f), g + _s.Entre(-10f, 10f), 0, true);
            Por(TipoDeObjeto.Barreira, p + dirt * (meia - 1.4f) + dir * 0.6f, g + _s.Entre(-25f, 15f), 0, true);
            if (tipo == TipoDeEstrada.Rodovia) Por(TipoDeObjeto.Barreira, p + dirt * (-meia + 3.3f) - dir * 0.4f, g + _s.Entre(-8f, 30f), 0, true);
            Por(TipoDeObjeto.Cavalete, p + dirt * _s.Entre(-1f, 1f) + dir * 2.5f, g + _s.Entre(-40f, 40f), 0, true, 90f);
            for (int k = 0; k < 4; k++)
                Por(TipoDeObjeto.Cone, p + dirt * _s.Entre(-meia + 0.5f, meia - 0.5f) + dir * _s.Entre(-6f, 4f), _s.Entre(0f, 360f), 0, true, _s.Chance(0.6f) ? 90f : 0f);
            // o caminhão e os sacos no acostamento, se ali não for outra estrada
            float giroCaminhao = GiroDe(-dir) + _s.Entre(-6f, 6f), giroSacos = g + 90f + _s.Entre(-8f, 8f);
            var caminhao = p + dirt * (meia + 2.2f) + dir * 9f;
            if (NaPropria(caminhao, e)) Por(TipoDeObjeto.CaminhaoMilitar, caminhao, giroCaminhao, 0, true);
            var sacos = p - dirt * (meia + 1.6f) + dir * 1.5f;
            if (NaPropria(sacos, e)) Por(TipoDeObjeto.Sacos, sacos, giroSacos, 0, true);
            // a fila dos que tentaram sair (na contramão de quem entra: indo para fora)
            int fila = _s.Entre(1, 4);
            for (int k = 0; k < fila; k++)
            {
                var c = p + dir * (6.5f + k * 6.2f) + dirt * (-1.8f + _s.Entre(-0.4f, 0.4f));
                float giroCarro = GiroDe(-dir) + _s.Entre(-7f, 7f);
                int modelo = _s.Entre(0, ModelosDaEstrada.Carros);
                if (NaPropria(c, e)) Por(TipoDeObjeto.Carro, c, giroCarro, modelo, true);
            }
            _ocupado.Add((p, 34f));
        }

        // ------------------------------------------------------------------ as placas de direção

        sealed class Destino
        {
            public int Lugar, No, Classe;
            public float[] Dist;
        }

        void PorAsPlacasDeDirecao()
        {
            var nos = _rede.Nos;
            var trechos = _rede.Trechos;
            // os destinos: as cidades (no meio), e os lugares com nome no começo da estrada deles
            var destinos = new List<Destino>();
            for (int li = 0; li < _plano.Lugares.Count; li++)
            {
                var l = _plano.Lugares[li];
                int classe = l.Tipo switch
                {
                    TipoDeLugar.CidadeGrande => 0,
                    TipoDeLugar.CidadePequena => 1,
                    TipoDeLugar.Vila => 2,
                    TipoDeLugar.BaseMilitar or TipoDeLugar.ComunidadeAliada or TipoDeLugar.ComunidadeInimiga or TipoDeLugar.Fazenda => 3,
                    _ => -1,
                };
                if (classe < 0 || string.IsNullOrEmpty(l.Nome)) continue;
                int no = -1;
                float melhor = l.Raio + 20f;
                for (int ni = 0; ni < nos.Count; ni++)
                {
                    if (nos[ni].Lugar == li) { no = ni; break; }
                    float dist = Vector2.Distance(nos[ni].Posicao, l.Centro);
                    if (dist < melhor)
                    {
                        melhor = dist;
                        no = ni;
                    }
                }
                if (no < 0) continue;
                destinos.Add(new Destino { Lugar = li, No = no, Classe = classe, Dist = Dijkstra(no) });
            }

            for (int ni = 0; ni < nos.Count; ni++)
            {
                var no = nos[ni];
                if (no.Trechos.Count < 3) continue;
                // dentro das cidades as placas são as das ruas (Parte M5)
                if (_d.CidadeEm(no.Posicao.x, no.Posicao.y) != null) continue;
                // o que cada saída leva: os destinos cujo caminho mais curto sai por ela (num empate, pela primeira),
                // menos os que já estão ali (o nó dentro deles)
                var todos = new List<Destino>[no.Trechos.Count];
                var ja = new HashSet<Destino>();
                for (int k = 0; k < no.Trechos.Count; k++)
                {
                    var t = trechos[no.Trechos[k]];
                    int outro = t.NoDe == ni ? t.NoAte : t.NoDe;
                    todos[k] = new List<Destino>();
                    foreach (var de in destinos)
                    {
                        if (de.No == ni || float.IsInfinity(de.Dist[ni]) || ja.Contains(de) || Ali(de, no.Posicao)) continue;
                        if (Mathf.Abs(de.Dist[ni] - (t.Comprimento + de.Dist[outro])) > 0.5f) continue;
                        todos[k].Add(de);
                        ja.Add(de);
                    }
                    todos[k].Sort((a, b) => a.Dist[ni].CompareTo(b.Dist[ni]));
                }

                // uma placa para quem chega por cada trecho
                for (int a = 0; a < no.Trechos.Count; a++)
                {
                    var ta = trechos[no.Trechos[a]];
                    var chegada = DirecaoNoNo(ta, ni, out var e, out int iNo, out int sentidoParaFora);
                    if (ta.Comprimento < 6f) continue;
                    var saidas = new List<(float ang, int b)>();
                    float maisReta = float.MaxValue, segunda = float.MaxValue;
                    int bMaisReta = -1;
                    for (int b = 0; b < no.Trechos.Count; b++)
                    {
                        if (b == a) continue;
                        var saida = DirecaoNoNo(trechos[no.Trechos[b]], ni, out _, out _, out _);
                        // a curva de quem chega (andando no sentido -chegada) para a saída: no plano (x, z), o ângulo
                        // positivo é para a esquerda; na placa, positivo é para a direita
                        float ang = -Vector2.SignedAngle(-chegada, saida);
                        if (Mathf.Abs(ang) < maisReta)
                        {
                            segunda = maisReta;
                            maisReta = Mathf.Abs(ang);
                            bMaisReta = b;
                        }
                        else segunda = Mathf.Min(segunda, Mathf.Abs(ang));
                        if (Escolher(todos[b], ni, 3).Count > 0) saidas.Add((ang, b));
                    }
                    // só uma saída leva a algum lugar, e é a mais reta, bem mais reta que as outras: quem chega segue em
                    // frente, sem placa (com duas quase em frente, a placa diz qual)
                    if (saidas.Count == 0 || (saidas.Count == 1 && saidas[0].b == bMaisReta && segunda - maisReta >= 25f)) continue;
                    // da esquerda para a direita; a seta de cada saída só na primeira linha dela; até 6 linhas
                    saidas.Sort((x, y) => x.ang.CompareTo(y.ang));
                    int porUma = saidas.Count >= 3 ? 2 : 3;
                    var placa = new ObjetoDaEstrada { Tipo = TipoDeObjeto.PlacaDeDirecao, Linhas = new List<LinhaDePlaca>() };
                    var registro = new List<SaidaDaPlaca>();
                    foreach (var (ang, b) in saidas)
                    {
                        var lista = Escolher(todos[b], ni, porUma);
                        var saida = new SaidaDaPlaca { Trecho = no.Trechos[b], Lugares = new List<int>() };
                        for (int k = 0; k < lista.Count && k < porUma; k++)
                        {
                            placa.Linhas.Add(new LinhaDePlaca
                            {
                                Texto = _plano.Lugares[lista[k].Lugar].Nome, Km = lista[k].Dist[ni] / 1000f, Seta = k == 0 ? Arredondar(ang) : (float?)null, Tamanho = 1f,
                            });
                            saida.Lugares.Add(lista[k].Lugar);
                        }
                        registro.Add(saida);
                    }
                    // até 40 m antes do nó, do lado direito; se ali for outra estrada (os zigue-zagues da serra), do outro
                    // lado ou em outra distância
                    float[] distancias =
                    {
                        Mathf.Min(40f, ta.Comprimento * 0.45f), Mathf.Min(25f, ta.Comprimento * 0.3f), Mathf.Min(60f, ta.Comprimento * 0.6f),
                        Mathf.Min(15f, ta.Comprimento * 0.2f), Mathf.Min(80f, ta.Comprimento * 0.7f),
                    };
                    if (!LugarDaPlaca(e, iNo, sentidoParaFora, distancias, ModelosDaEstrada.LarguraDaPlacaDeDirecao(placa.Linhas), out var onde, out var olha))
                    {
                        PlacasSemLugar++;
                        continue;
                    }
                    placa.Posicao = onde;
                    placa.Giro = Mathf.Repeat(GiroDe(olha), 360f);
                    Todos.Add(placa);
                    Direcoes[Chave(ni, no.Trechos[a])] = registro;
                }
            }
        }

        /// <summary>
        /// O que cada placa de direção diz (para os testes e para quem quiser seguir as placas): pela chave (nó, trecho por
        /// onde se chega), as saídas com os lugares escritos para cada uma. Sem placa, quem chega segue em frente (a saída
        /// mais reta é a única que leva a algum lugar).
        /// </summary>
        public readonly Dictionary<long, List<SaidaDaPlaca>> Direcoes = new Dictionary<long, List<SaidaDaPlaca>>();

        public static long Chave(int no, int trechoDeChegada) => ((long)no << 32) | (uint)trechoDeChegada;

        /// <summary>Para onde o trecho sai do nó, uns 20 m para dentro dele (o que quem chega no cruzamento vê).</summary>
        public Vector2 Saida(int trecho, int no) => DirecaoNoNo(_rede.Trechos[trecho], no, out _, out _, out _);

        // o nó já está no destino (dentro da cidade, na beira da fazenda): a placa não manda para lá
        bool Ali(Destino de, Vector2 p)
        {
            var l = _plano.Lugares[de.Lugar];
            if (l is Cidade c) return c.Q(p.x, p.y) < 1.05f;
            return Vector2.Distance(l.Centro, p) < Mathf.Max(300f, l.Raio * 1.5f);
        }

        // as setas em passos de 45° (em frente, meio de lado, de lado, voltando)
        static float Arredondar(float ang) => Mathf.Round(ang / 45f) * 45f;

        // o que vai na placa por uma saída (até "quantas" linhas), como nas rodovias: a próxima cidade, a cidade grande e a
        // mais longe daquele lado (quem vai para uma cidade que não está na placa segue a escrita mais perto dela no mapa,
        // até ela aparecer); sobrando lugar, as outras cidades e uma vila logo ali. Sem cidade, a vila mais perto, ou o lugar
        // com nome mais perto (até 6 km). Em ordem de distância
        static List<Destino> Escolher(List<Destino> porAli, int no, int quantas)
        {
            var cidades = porAli.FindAll(d => d.Classe <= 1);
            var escolhidos = new List<Destino>();
            void Por(Destino d)
            {
                if (d != null && escolhidos.Count < quantas && !escolhidos.Contains(d)) escolhidos.Add(d);
            }
            if (cidades.Count > 0)
            {
                Por(cidades[0]);
                Por(cidades.Find(d => d.Classe == 0));
                Por(cidades[cidades.Count - 1]);
                for (int i = 1; i < cidades.Count; i++) Por(cidades[i]);
                var vila = porAli.Find(d => d.Classe == 2);
                if (vila != null && vila.Dist[no] < cidades[0].Dist[no]) Por(vila);
            }
            else Por(porAli.Find(d => d.Classe == 2) ?? porAli.Find(d => d.Classe == 3 && d.Dist[no] < 6000f));
            escolhidos.Sort((a, b) => a.Dist[no].CompareTo(b.Dist[no]));
            return escolhidos;
        }

        // a direção do trecho saindo do nó (no primeiro segmento), a estrada, o ponto do nó nela e o sentido para fora
        Vector2 DirecaoNoNo(RedeDeEstradas.Trecho t, int no, out Estrada e, out int iNo, out int sentidoParaFora)
        {
            e = _plano.Estradas[t.Estrada];
            if (t.NoDe == no)
            {
                iNo = t.De;
                sentidoParaFora = 1;
            }
            else
            {
                iNo = t.Ate;
                sentidoParaFora = -1;
            }
            // uns 20 m para dentro do trecho: a direção geral, sem o último zigue-zague
            var longe = AoLongo(e, iNo, sentidoParaFora, Mathf.Min(20f, t.Comprimento * 0.5f), out _);
            return (longe - e.Pontos[iNo]).normalized;
        }

        float[] Dijkstra(int origem)
        {
            var nos = _rede.Nos;
            var trechos = _rede.Trechos;
            var dist = new float[nos.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.PositiveInfinity;
            dist[origem] = 0f;
            var fila = new SortedSet<(float d, int n)>();
            fila.Add((0f, origem));
            while (fila.Count > 0)
            {
                var (du, u) = fila.Min;
                fila.Remove(fila.Min);
                if (du > dist[u]) continue;
                foreach (int ti in nos[u].Trechos)
                {
                    var t = trechos[ti];
                    int v = t.NoDe == u ? t.NoAte : t.NoDe;
                    float nd = du + t.Comprimento;
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        fila.Add((nd, v));
                    }
                }
            }
            return dist;
        }

        // ------------------------------------------------------------------ os carros abandonados

        void PorOsCarros()
        {
            foreach (var e in _plano.Estradas)
            {
                if (e.Tipo == TipoDeEstrada.Terra || e.Conector || e.Comprimento < 80f) continue;
                float meia = RedeDeEstradas.MeiaLargura(e.Tipo);
                float intervalo = e.Tipo == TipoDeEstrada.Rodovia ? 230f : 420f;
                float andado = _s.Entre(40f, intervalo);
                float s = 0f;
                for (int i = 0; i + 1 < e.Pontos.Count; i++)
                {
                    float trecho = Vector2.Distance(e.Pontos[i], e.Pontos[i + 1]);
                    while (andado <= s + trecho)
                    {
                        var dir = (e.Pontos[i + 1] - e.Pontos[i]) / Mathf.Max(trecho, 1e-4f);
                        var p = Vector2.Lerp(e.Pontos[i], e.Pontos[i + 1], (andado - s) / Mathf.Max(trecho, 1e-4f));
                        Carro(e, p, dir, meia);
                        andado += intervalo * _s.Entre(0.4f, 1.6f);
                    }
                    s += trecho;
                }
            }
        }

        void Carro(Estrada e, Vector2 p, Vector2 dir, float meia)
        {
            // (perto da cerca, não: lá a estrada sobe a serra até a barreira; nem dentro das cidades, que têm os carros delas)
            if (!Livre(p, 6f) || !_d.DentroDaCerca(p.x, p.y, 40f) || _d.CidadeEm(p.x, p.y) != null) return;
            if (_d.Lago != null && _d.Lago.Q(p.x, p.y) < 1.3f) return;
            int modelo = _s.Entre(0, ModelosDaEstrada.Carros);
            // a mão do carro: a da direita de quem anda no sentido dele
            int mao = _s.Chance(0.5f) ? 1 : -1;
            var frente = dir * mao;
            var direita = Direita(frente);
            float jeito = _s.Fracao();
            if (jeito < 0.35f)
            {
                // parado na pista, meio torto
                var c = p + direita * (meia * 0.36f + _s.Entre(-0.3f, 0.3f));
                float giro = GiroDe(frente) + _s.Entre(-12f, 12f);
                if (NaPropria(c, e)) Por(TipoDeObjeto.Carro, c, giro, modelo, true);
            }
            else if (jeito < 0.75f)
            {
                // encostado no acostamento
                var c = p + direita * (meia + 1.4f + _s.Entre(-0.2f, 0.4f));
                float giro = GiroDe(frente) + _s.Entre(-6f, 6f);
                if (NaPropria(c, e)) Por(TipoDeObjeto.Carro, c, giro, modelo, true);
            }
            else
            {
                // batido fora da pista, no barranco
                float fora = meia + RedeDeEstradas.Acostamento + _s.Entre(2.5f, 7f);
                var c = p + direita * fora;
                float giro = GiroDe(frente) + _s.Entre(25f, 75f) * (_s.Chance(0.5f) ? 1f : -1f);
                bool capotado = _s.Chance(0.12f);
                if (ForaDasEstradas(c, 3f))
                {
                    var o = Por(TipoDeObjeto.Carro, c, giro, modelo);
                    o.NoTalude = true;
                    if (capotado) o.Tombo = 180f;
                }
            }
            _ocupado.Add((p, 8f));
        }
    }
}
