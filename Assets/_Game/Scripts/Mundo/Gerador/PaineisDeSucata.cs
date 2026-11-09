using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O chão de um painel de metal: a terra, o asfalto, a calçada ou o concreto da garagem (a altura de cada um).</summary>
    public enum ChaoDoPainel : byte { Terra, Asfalto, Calcada, Garagem }

    /// <summary>Um painel de metal largado no mapa: o lugar (x, z), o giro em y (graus), o modelo (0 a 2) e o chão dele.</summary>
    public struct PainelNoMapa
    {
        public Vector2 Posicao;
        public float Giro;
        public byte Modelo;
        public ChaoDoPainel Chao;
    }

    /// <summary>
    /// Os painéis de metal pelo mundo (09/11/2026, a 2ª leva da etapa 1b; a decisão do Pedro: "um material novo, encontrado
    /// em alguns itens de metais no chão, como alguns painéis de metal"): a chapa ondulada amassada, a porta de carro solta e
    /// a placa com o poste quebrado (<see cref="NomeDoModelo"/>), deitadas no chão. Desmontados, viram sucata
    /// (<c>World/MundoDaPartida.Sucata.cs</c>). Saem da semente do mundo, iguais em todas as máquinas, com um sorteio só
    /// deles ("paineis-de-sucata"), depois da beira das estradas: o resto do gerador não muda.
    ///  - nas ruas das cidades e das vilas: de <see cref="PorTrechoMin"/> a <see cref="PorTrechoMax"/> por trecho de rua entre
    ///    duas esquinas, na beira do asfalto ou na calçada. Cada quarteirão divide os trechos em volta com os vizinhos: uns 2
    ///    a 4 por quarteirão;
    ///  - perto dos carros abandonados (<see cref="ChanceDoCarro"/>, ao lado dele) e nas garagens e vagas das casas
    ///    (<see cref="ChanceDaGaragem"/>, atrás do carro, na boca da garagem);
    ///  - nos postos (de 1 a <see cref="NoPosto"/>, dos lados do pátio), nas barreiras e cavaletes da estrada
    ///    (<see cref="ChanceDaBarreira"/>) e nos estacionamentos e terrenos baldios das cidades (o mapa não tem área
    ///    industrial: <see cref="ChanceDoEstacionamento"/>, <see cref="ChanceDoBaldio"/>).
    /// Nada cai em cima de um carro, de um poste, de uma árvore da rua ou de outro painel (<see cref="Livre"/>). O número de
    /// cada um é o índice em <see cref="Todos"/>: o save do mundo guarda os desmontados por ele.
    /// </summary>
    public sealed class PaineisDeSucata
    {
        /// <summary>Os três modelos do Loot_Sucata.fbx (Painel_Chapa_corpo, Painel_Porta_corpo e Painel_Placa_corpo), nesta ordem.</summary>
        public const int Modelos = 3;
        public const byte Chapa = 0, Porta = 1, Placa = 2;

        // ------------------------------------------------------------------ a densidade (para o Pedro ajustar)

        /// <summary>Quantos painéis em cada trecho de rua entre duas esquinas (do mínimo ao máximo, os dois inclusive).</summary>
        public const int PorTrechoMin = 1, PorTrechoMax = 2;
        /// <summary>A chance de um carro abandonado (na rua ou na estrada) ter um painel ao lado.</summary>
        public const float ChanceDoCarro = 0.12f;
        /// <summary>A chance de o carro da garagem (ou da vaga) da casa ter um painel atrás dele, na boca da garagem.</summary>
        public const float ChanceDaGaragem = 0.25f;
        /// <summary>A chance de uma barreira, um cavalete ou os sacos de areia da estrada terem um painel perto.</summary>
        public const float ChanceDaBarreira = 0.3f;
        /// <summary>No máximo tantos painéis em cada posto (de 1 a este).</summary>
        public const int NoPosto = 3;
        /// <summary>A chance de um estacionamento (que pode ter dois) e de um terreno baldio (um) terem um painel.</summary>
        public const float ChanceDoEstacionamento = 0.6f, ChanceDoBaldio = 0.35f;

        /// <summary>O nome de cada modelo (com e sem o artigo).</summary>
        public static string NomeDoModelo(int modelo) => modelo == Porta ? "porta de carro" : modelo == Placa ? "placa caída" : "chapa de metal";
        public static string NomeComArtigo(int modelo) => modelo == Porta ? "a porta de carro" : modelo == Placa ? "a placa caída" : "a chapa de metal";

        public readonly List<PainelNoMapa> Todos = new List<PainelNoMapa>();
        int[][] _porBloco;
        readonly DadosDoMundo _d;
        readonly Sorteio _s;
        // o que já ocupa o chão (os carros, os postes, as árvores da rua, as barreiras e os painéis), numa grade de 8 m
        readonly Dictionary<long, List<(Vector2 p, float r, int dono)>> _ocupado = new Dictionary<long, List<(Vector2, float, int)>>();
        const float Celula = 8f;

        PaineisDeSucata(DadosDoMundo d)
        {
            _d = d;
            _s = Sorteio.Da(d.Semente, "paineis-de-sucata");
        }

        static PaineisDeSucata _ultimo;
        static DadosDoMundo _doUltimo;

        /// <summary>Os painéis deste mundo (feitos na primeira vez; só na thread principal).</summary>
        public static PaineisDeSucata Do(DadosDoMundo d)
        {
            if (d == null) return null;
            if (!ReferenceEquals(d, _doUltimo))
            {
                _ultimo = Planejar(d);
                _doUltimo = d;
            }
            return _ultimo;
        }

        public static PaineisDeSucata Planejar(DadosDoMundo d)
        {
            var p = new PaineisDeSucata(d);
            if (d.Plano != null && d.Objetos != null)
            {
                p.Ocupar();
                p.PorNasRuas();
                p.PorPertoDosObjetos();
            }
            p.Indexar();
            return p;
        }

        /// <summary>Os índices (em <see cref="Todos"/>) dos painéis do bloco, ou null.</summary>
        public int[] DoBloco(int bx, int bz) => _porBloco?[bz * _d.BlocosPorLado + bx];

        /// <summary>O painel com este número existe?</summary>
        public bool Existe(int indice) => indice >= 0 && indice < Todos.Count;

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

        // ------------------------------------------------------------------ o chão ocupado

        static long ChaveDa(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        void Marcar(Vector2 p, float r, int dono)
        {
            var k = ChaveDa(Mathf.FloorToInt(p.x / Celula), Mathf.FloorToInt(p.y / Celula));
            if (!_ocupado.TryGetValue(k, out var l)) _ocupado[k] = l = new List<(Vector2, float, int)>();
            l.Add((p, r, dono));
        }

        /// <summary>Cabe um painel (de raio 'r') aqui? (longe dos carros, dos postes e dos outros painéis; 'ignorar': o objeto perto do qual ele vai)</summary>
        bool Livre(Vector2 p, float r, int ignorar = -1)
        {
            int cx = Mathf.FloorToInt(p.x / Celula), cz = Mathf.FloorToInt(p.y / Celula);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_ocupado.TryGetValue(ChaveDa(cx + dx, cz + dz), out var l)) continue;
                foreach (var o in l)
                {
                    if (o.dono >= 0 && o.dono == ignorar) continue;
                    float m = o.r + r;
                    if ((o.p - p).sqrMagnitude < m * m) return false;
                }
            }
            return true;
        }

        // o raio de cada coisa da beira da estrada no chão (0: não conta)
        static float RaioDe(TipoDeObjeto t) => t switch
        {
            TipoDeObjeto.Carro => 2.3f,
            TipoDeObjeto.Poste or TipoDeObjeto.Arvore or TipoDeObjeto.Hidrante or TipoDeObjeto.Lixeira or TipoDeObjeto.PlacaDeRua => 0.8f,
            TipoDeObjeto.PlacaDeAviso or TipoDeObjeto.PlacaDeDirecao or TipoDeObjeto.PlacaDeEntrada => 1.2f,
            TipoDeObjeto.Barreira => 1.7f,
            TipoDeObjeto.Cavalete or TipoDeObjeto.Cone => 1f,
            TipoDeObjeto.Sacos => 1.6f,
            TipoDeObjeto.Conteiner or TipoDeObjeto.Blindado => 4f,
            TipoDeObjeto.CaminhaoMilitar => 5f,
            TipoDeObjeto.Posto => 13f,
            _ => 0f,
        };

        void Ocupar()
        {
            var todos = _d.Objetos.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                float r = RaioDe(todos[i].Tipo);
                if (r > 0f) Marcar(todos[i].Posicao, r, i);
            }
        }

        // o painel no mapa (se couber), e o lugar dele fica ocupado
        bool Por(Vector2 p, float giro, byte modelo, ChaoDoPainel chao, int perto = -1)
        {
            const float raio = 0.75f;
            if (!Livre(p, raio, perto)) return false;
            Todos.Add(new PainelNoMapa { Posicao = p, Giro = Mathf.Repeat(giro, 360f), Modelo = modelo, Chao = chao });
            Marcar(p, raio, -1);
            return true;
        }

        // o modelo pelos pesos (a chapa, a porta, a placa)
        byte Modelo(float chapa, float porta)
        {
            float x = _s.Fracao();
            return x < chapa ? Chapa : x < chapa + porta ? Porta : Placa;
        }

        // ------------------------------------------------------------------ as ruas das cidades

        void PorNasRuas()
        {
            foreach (var t in _d.Plano.Tracados)
            {
                int nu = t.Us.Length, nv = t.Vs.Length;
                for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    if (j + 1 < nv && t.RuaU[i, j]) Rua(t, true, i, t.Vs[j] + t.AV(j) + t.SV(j), t.Vs[j + 1] - t.AV(j + 1) - t.SV(j + 1));
                    if (i + 1 < nu && t.RuaV[i, j]) Rua(t, false, j, t.Us[i] + t.AU(i) + t.SU(i), t.Us[i + 1] - t.AU(i + 1) - t.SU(i + 1));
                }
                // os estacionamentos e os terrenos baldios (no lugar da área industrial, que o mapa não tem)
                foreach (var l in t.Lotes)
                {
                    if (l.Tipo != TipoDeLote.Estacionamento && l.Tipo != TipoDeLote.Baldio) continue;
                    if (l.U1 - l.U0 < 6f || l.V1 - l.V0 < 6f) continue;
                    bool estacionamento = l.Tipo == TipoDeLote.Estacionamento;
                    int n = _s.Chance(estacionamento ? ChanceDoEstacionamento : ChanceDoBaldio) ? (estacionamento && _s.Chance(0.4f) ? 2 : 1) : 0;
                    for (int k = 0; k < n; k++)
                        for (int tentativa = 0; tentativa < 3; tentativa++)
                        {
                            var local = new Vector2(_s.Entre(l.U0 + 2.5f, l.U1 - 2.5f), _s.Entre(l.V0 + 2.5f, l.V1 - 2.5f));
                            if (Por(t.Mundo(local), _s.Entre(0f, 360f), Modelo(0.6f, 0.25f), estacionamento ? ChaoDoPainel.Asfalto : ChaoDoPainel.Terra)) break;
                        }
                }
            }
        }

        // um trecho de rua entre duas esquinas (como no ObjetosDaCidade.Rua): na beira do asfalto ou na calçada
        void Rua(TracadoDaCidade t, bool daLinhaU, int linha, float de, float ate)
        {
            if (ate - de < 10f) return;
            var tipo = daLinhaU ? t.TipoU[linha] : t.TipoV[linha];
            float pista = TracadoDaCidade.MeiaPista(tipo), calcada = TracadoDaCidade.Calcada(tipo);
            float eixo = daLinhaU ? t.Us[linha] : t.Vs[linha];
            Vector2 Ponto(float aoLongoDe, float atravessado) => daLinhaU ? new Vector2(eixo + atravessado, aoLongoDe) : new Vector2(aoLongoDe, eixo + atravessado);
            int n = _s.Entre(PorTrechoMin, PorTrechoMax + 1);
            for (int k = 0; k < n; k++)
                for (int tentativa = 0; tentativa < 3; tentativa++)
                {
                    float sinal = _s.Chance(0.5f) ? 1f : -1f;
                    bool naCalcada = calcada > 1.6f && _s.Chance(0.45f);
                    float atravessado = naCalcada ? pista + _s.Entre(0.7f, calcada - 0.7f) : pista - _s.Entre(0.5f, 1.4f);
                    var local = Ponto(_s.Entre(de + 3f, ate - 3f), sinal * atravessado);
                    if (Por(t.Mundo(local), _s.Entre(0f, 360f), Modelo(0.45f, 0.2f), naCalcada ? ChaoDoPainel.Calcada : ChaoDoPainel.Asfalto)) break;
                }
        }

        // ------------------------------------------------------------------ perto dos carros, dos postos e das barreiras

        static Vector2 FrenteDe(float giro) => new Vector2(Mathf.Sin(giro * Mathf.Deg2Rad), Mathf.Cos(giro * Mathf.Deg2Rad));

        void PorPertoDosObjetos()
        {
            var todos = _d.Objetos.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                var o = todos[i];
                var frente = FrenteDe(o.Giro);
                var direita = new Vector2(frente.y, -frente.x);
                switch (o.Tipo)
                {
                    case TipoDeObjeto.Carro when o.NaGaragem:
                        // atrás do carro (ele entrou de frente): na boca da garagem ou da vaga
                        if (_s.Chance(ChanceDaGaragem))
                            Por(o.Posicao - frente * _s.Entre(3.1f, 3.5f) + direita * _s.Entre(-0.4f, 0.4f), o.Giro + _s.Entre(-30f, 30f), Modelo(0.5f, 0.5f), ChaoDoPainel.Garagem, i);
                        break;
                    case TipoDeObjeto.Carro:
                        // ao lado do carro abandonado (a porta que caiu dele, na maioria)
                        if (!_s.Chance(ChanceDoCarro)) break;
                        for (int tentativa = 0; tentativa < 2; tentativa++)
                        {
                            float lado = _s.Chance(0.5f) ? 1f : -1f;
                            var p = o.Posicao + direita * (lado * _s.Entre(2.2f, 2.8f)) + frente * _s.Entre(-1.3f, 1.3f);
                            if (Por(p, o.Giro + _s.Entre(-60f, 60f), Modelo(0.25f, 0.6f), o.NaEstrada ? ChaoDoPainel.Asfalto : ChaoDoPainel.Terra, i)) break;
                        }
                        break;
                    case TipoDeObjeto.Posto:
                    {
                        // dos lados do pátio de concreto (19 × 13 m, do -3,5 ao 9,5 em z do posto), no chão
                        int n = _s.Entre(1, NoPosto + 1);
                        for (int k = 0; k < n; k++)
                        {
                            float x = (_s.Chance(0.5f) ? 1f : -1f) * _s.Entre(10.6f, 12.4f), z = _s.Entre(-4f, 8f);
                            Por(o.Posicao + direita * x + frente * z, _s.Entre(0f, 360f), Modelo(0.5f, 0.2f), ChaoDoPainel.Terra, i);
                        }
                        break;
                    }
                    case TipoDeObjeto.Barreira:
                    case TipoDeObjeto.Cavalete:
                    case TipoDeObjeto.Sacos:
                        if (!_s.Chance(ChanceDaBarreira)) break;
                        for (int tentativa = 0; tentativa < 2; tentativa++)
                        {
                            float a = _s.Entre(0f, 360f) * Mathf.Deg2Rad;
                            var p = o.Posicao + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _s.Entre(2.8f, 4.5f);
                            if (Por(p, _s.Entre(0f, 360f), Modelo(0.5f, 0f), o.NaEstrada ? ChaoDoPainel.Asfalto : ChaoDoPainel.Terra, i)) break;
                        }
                        break;
                }
            }
        }

        /// <summary>A altura do chão do painel pelo mapa (sem a colisão: a montagem na cena acerta com um raio para baixo).</summary>
        public static float AlturaDoChao(DadosDoMundo d, PainelNoMapa p)
        {
            float y = d.Altura(p.Posicao.x, p.Posicao.y);
            return p.Chao switch
            {
                ChaoDoPainel.Asfalto => y + RedeDeEstradas.AcimaDoChao,
                ChaoDoPainel.Calcada => y + TracadoDaCidade.AlturaDaCalcada,
                ChaoDoPainel.Garagem => y + 0.05f,
                _ => y,
            };
        }
    }
}
