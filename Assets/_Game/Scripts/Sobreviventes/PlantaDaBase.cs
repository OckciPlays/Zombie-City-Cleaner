using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O desenho da base militar provisória (03/11/2026, a pedido: "os militares, mais resistentes, só nas bases militares";
    /// como a base de verdade ainda não existe, uma provisória para os testes), em dados, igual em todas as máquinas (sai da
    /// semente do mundo e do índice da base). Fica no meio de cada lugar <see cref="TipoDeLugar.BaseMilitar"/> do mapa, sem
    /// mexer no gerador: o chão não é aplainado, e cada peça acompanha o relevo (<see cref="Chao"/>).
    /// Nas coordenadas da base: o meio na origem, +z para o portão (a estrada que chega no meio do lugar).
    ///  - a cerca quadrada de 40 × 40 m: contêineres, alambrado e sacos de areia, com o portão (7 m) entre duas barreiras
    ///    de concreto, a cancela levantada e a guarita;
    ///  - dentro: 2 ou 3 barracas do exército, o caminhão e o blindado, dois ninhos de sacos de areia nos cantos, as
    ///    pilhas de caixas de munição, o mastro com a bandeira e dois postes de luz;
    ///  - os postos dos militares (de 6 a 8), na ordem dos índices: os dois do portão, os dos ninhos, a ronda do meio, o do
    ///    caminhão, o das barracas e o do canto de trás.
    /// </summary>
    public sealed class PlantaDaBase
    {
        public const float Meia = 20f;

        public struct Posto
        {
            public Vector3 pos, ronda;
            public float giro;
        }

        public enum Coisa : byte { Conteiner, Sacos, Alambrado, Barreira, Caminhao, Blindado, Barraca, Guarita, Caixas, Mastro, Poste, Cancela }

        public struct Peca
        {
            public Coisa tipo;
            public Vector3 pos;
            public float giro;
            public int variante;
            /// <summary>O comprimento no eixo x da peça (para assentar as pontas no chão).</summary>
            public float comprimento;
        }

        public readonly Lugar Lugar;
        /// <summary>O índice da base entre as bases do mapa (na ordem do plano).</summary>
        public readonly int Indice;
        public readonly List<Peca> Pecas = new List<Peca>();
        public readonly List<Posto> Postos = new List<Posto>();
        /// <summary>Quantos militares a base tem (de 6 a 8).</summary>
        public readonly int Militares;
        /// <summary>Fora do portão, a 25 m (para o F1 levar o jogador).</summary>
        public readonly Vector3 Portao = new Vector3(0f, 0f, Meia + 25f);

        readonly DadosDoMundo _d;
        readonly Matrix4x4 _paraOMundo;
        readonly float _giro;
        readonly float _altura;

        // o cache é só do mundo de agora (03/11/2026: a planta guarda o DadosDoMundo, e o cache static guardava os mundos de
        // todas as partidas da sessão: o mundo grande inteiro preso na memória a cada mundo novo)
        static readonly Dictionary<int, PlantaDaBase> _cache = new Dictionary<int, PlantaDaBase>();
        static DadosDoMundo _doCache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => Esquecer();

        /// <summary>Solta as plantas guardadas (e o mundo delas): a partida acabou ou o mundo mudou.</summary>
        public static void Esquecer()
        {
            _cache.Clear();
            _doCache = null;
        }

        /// <summary>As bases militares do mapa, na ordem do plano.</summary>
        public static List<Lugar> Bases(DadosDoMundo d)
        {
            var lista = new List<Lugar>();
            if (d?.Plano == null) return lista;
            foreach (var l in d.Plano.Lugares) if (l.Tipo == TipoDeLugar.BaseMilitar) lista.Add(l);
            return lista;
        }

        /// <summary>O desenho da base (feito uma vez por mundo).</summary>
        public static PlantaDaBase Da(DadosDoMundo d, Lugar l, int indice)
        {
            if (_doCache != d)
            {
                Esquecer();
                _doCache = d;
            }
            if (!_cache.TryGetValue(indice, out var p))
            {
                p = new PlantaDaBase(d, l, indice);
                _cache[indice] = p;
            }
            return p;
        }

        PlantaDaBase(DadosDoMundo d, Lugar l, int indice)
        {
            _d = d;
            Lugar = l;
            Indice = indice;
            var r = new System.Random(unchecked(d.Semente * 7349 + indice * 51203 + 11));
            _giro = GiroDaEstrada(d, l);
            _altura = d.Altura(l.Centro.x, l.Centro.y);
            _paraOMundo = Matrix4x4.TRS(new Vector3(l.Centro.x, _altura, l.Centro.y), Quaternion.Euler(0f, _giro, 0f), Vector3.one);
            float H = Meia;

            // a cerca (o lado do arame farpado, o -z das peças, para fora), com o portão de 7 m no meio da frente
            Trecho(r, new Vector3(-H, 0f, H), Vector3.right, H - 3.5f, 180f);
            Trecho(r, new Vector3(H, 0f, H), Vector3.left, H - 3.5f, 180f);
            Trecho(r, new Vector3(-H, 0f, -H), Vector3.right, 2f * H, 0f);
            Trecho(r, new Vector3(-H, 0f, -H), Vector3.forward, 2f * H, 90f);
            Trecho(r, new Vector3(H, 0f, -H), Vector3.forward, 2f * H, 270f);
            Por(Coisa.Barreira, new Vector3(-4.3f, 0f, H), 0f, 0, 2f);
            Por(Coisa.Barreira, new Vector3(4.3f, 0f, H), 0f, 0, 2f);
            Por(Coisa.Cancela, new Vector3(-3.4f, 0f, H + 0.6f), 0f, 0, 0.3f);
            Por(Coisa.Guarita, new Vector3(7.6f, 0f, H - 2.8f), 0f, 0, 2.4f);

            // os veículos, as barracas, os ninhos e o resto
            Por(Coisa.Caminhao, new Vector3(12.5f, 0f, -8f), r.Next(2) == 0 ? 0f : 180f, 0, 2.6f);
            Por(Coisa.Blindado, new Vector3(13.2f, 0f, 6.5f), 195f + (float)r.NextDouble() * 20f, 0, 3f);
            int barracas = 2 + r.Next(2);
            for (int k = 0; k < barracas; k++) Por(Coisa.Barraca, new Vector3(-13f + k * 6.5f, 0f, -13f), 0f, k, 4f);
            Ninho(new Vector3(-15f, 0f, 15f));
            Ninho(new Vector3(15f, 0f, -15.5f));
            Por(Coisa.Caixas, new Vector3(-13f, 0f, 2f), (float)r.NextDouble() * 30f, 0, 2f);
            Por(Coisa.Caixas, new Vector3(5.5f, 0f, -4f), 90f + (float)r.NextDouble() * 30f, 1, 2f);
            Por(Coisa.Mastro, new Vector3(0f, 0f, -3f), 0f, 0, 0.3f);
            Por(Coisa.Poste, new Vector3(-17f, 0f, -1f), 90f, 0, 0.3f);
            Por(Coisa.Poste, new Vector3(16.8f, 0f, 1f), 270f, 0, 0.3f);

            // os postos (o giro: para onde olha parado; 0 é para o portão)
            Postos.Add(new Posto { pos = new Vector3(-3.2f, 0f, H - 2.5f), ronda = new Vector3(-3.2f, 0f, H - 9f), giro = 0f });
            Postos.Add(new Posto { pos = new Vector3(3.2f, 0f, H - 2.5f), ronda = new Vector3(3.2f, 0f, H - 9f), giro = 0f });
            Postos.Add(new Posto { pos = new Vector3(-15f, 0f, 15f), ronda = new Vector3(-15f, 0f, 8f), giro = 315f });
            Postos.Add(new Posto { pos = new Vector3(15f, 0f, -15.5f), ronda = new Vector3(8f, 0f, -16f), giro = 135f });
            Postos.Add(new Posto { pos = new Vector3(-9f, 0f, 7f), ronda = new Vector3(8f, 0f, 9f), giro = 0f });
            Postos.Add(new Posto { pos = new Vector3(9f, 0f, -8f), ronda = new Vector3(8.5f, 0f, 1f), giro = 90f });
            Postos.Add(new Posto { pos = new Vector3(-7.5f, 0f, -8f), ronda = new Vector3(-15f, 0f, -6f), giro = 0f });
            Postos.Add(new Posto { pos = new Vector3(-16f, 0f, -16.5f), ronda = new Vector3(-16f, 0f, -7f), giro = 225f });
            Militares = 6 + r.Next(3);
        }

        // o giro da base: o portão para a estrada que sai do meio do lugar (a de terra que liga a base à rede); sem ela, o do lugar
        static float GiroDaEstrada(DadosDoMundo d, Lugar l)
        {
            int indice = d.Plano.Lugares.IndexOf(l);
            foreach (var e in d.Plano.Estradas)
            {
                if (e.De != indice || e.Pontos.Count < 2) continue;
                foreach (var p in e.Pontos)
                {
                    var para = p - l.Centro;
                    if (para.magnitude < Meia + 8f) continue;
                    return Mathf.Atan2(para.x, para.y) * Mathf.Rad2Deg;
                }
            }
            return l.Giro;
        }

        void Por(Coisa tipo, Vector3 pos, float giro, int variante, float comprimento) =>
            Pecas.Add(new Peca { tipo = tipo, pos = pos, giro = giro, variante = variante, comprimento = comprimento });

        // o ninho: três muros de sacos em U, aberto para trás (para dentro da base)
        void Ninho(Vector3 c)
        {
            // a frente do U olha para fora da base (para o canto)
            var fora = new Vector3(Mathf.Sign(c.x), 0f, Mathf.Sign(c.z)).normalized;
            float giro = Mathf.Atan2(fora.x, fora.z) * Mathf.Rad2Deg;
            var R = Quaternion.Euler(0f, giro, 0f);
            Por(Coisa.Sacos, c + R * new Vector3(0f, 0f, 1.6f), giro, 0, 3f);
            Por(Coisa.Sacos, c + R * new Vector3(-1.6f, 0f, 0.1f), giro + 90f, 0, 3f);
            Por(Coisa.Sacos, c + R * new Vector3(1.6f, 0f, 0.1f), giro + 90f, 0, 3f);
        }

        // um trecho da cerca, do canto 'de' na direção 'dir': mais contêineres que no acampamento, alambrado e sacos de areia
        void Trecho(System.Random r, Vector3 de, Vector3 dir, float comprimento, float giro)
        {
            float s = 0f;
            while (comprimento - s > 0.6f)
            {
                float resta = comprimento - s;
                double sorte = r.NextDouble();
                var tipo = resta < 6.1f ? (sorte < 0.6 && resta >= 2.6f ? Coisa.Alambrado : Coisa.Sacos)
                         : sorte < 0.45 ? Coisa.Conteiner : sorte < 0.82 ? Coisa.Alambrado : Coisa.Sacos;
                float tamanho = tipo == Coisa.Conteiner ? 6.1f : 3f;
                float meio = tamanho > resta ? comprimento - tamanho * 0.5f : s + tamanho * 0.5f;
                Por(tipo, de + dir * meio, giro, r.Next(4), tamanho);
                s += tamanho;
            }
        }

        /// <summary>O giro da base no mundo (graus).</summary>
        public float GiroNoMundo(float local) => local + _giro;

        /// <summary>A base no mundo: a posição do meio (na altura do chão ali) e o giro.</summary>
        public Matrix4x4 ParaOMundo => _paraOMundo;

        /// <summary>A altura do chão no meio da base.</summary>
        public float AlturaDoMeio => _altura;

        /// <summary>Um ponto da base no mundo, no chão de lá (mais o y do ponto).</summary>
        public Vector3 Mundo(Vector3 local)
        {
            var p = _paraOMundo.MultiplyPoint3x4(new Vector3(local.x, 0f, local.z));
            p.y = _d.Altura(p.x, p.z) + local.y;
            return p;
        }

        /// <summary>
        /// A altura (relativa ao meio da base) onde a peça assenta: a mais baixa do chão no meio e nas duas pontas (o
        /// comprimento no eixo x dela), um pouco enterrada, para nenhuma ponta ficar no ar.
        /// </summary>
        public float Chao(Vector3 local, float giro, float comprimento)
        {
            var eixo = Quaternion.Euler(0f, giro, 0f) * Vector3.right * (comprimento * 0.5f);
            float menor = float.MaxValue;
            foreach (var q in new[] { local, local + eixo, local - eixo })
            {
                var p = _paraOMundo.MultiplyPoint3x4(new Vector3(q.x, 0f, q.z));
                menor = Mathf.Min(menor, _d.Altura(p.x, p.z));
            }
            return menor - _altura - 0.05f;
        }

        /// <summary>O ponto (no mundo) está dentro da cerca.</summary>
        public bool Dentro(Vector3 mundo, float folga = 0f)
        {
            var l = _paraOMundo.inverse.MultiplyPoint3x4(mundo);
            return Mathf.Abs(l.x) < Meia + folga && Mathf.Abs(l.z) < Meia + folga;
        }

        /// <summary>O meio da base no mundo (no chão).</summary>
        public Vector3 Meio => new Vector3(Lugar.Centro.x, _altura, Lugar.Centro.y);
    }
}
