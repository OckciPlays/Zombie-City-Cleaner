using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os lotes das cidades no chão do Lakewood (Plano_Lakewood, Etapa 6, a parte 2): as ruas novas entram na rede das
    /// estradas (<see cref="ViasDasCidades"/>: o tipo <see cref="TipoDeVia.Rua"/>, retas, na largura delas, e a alça das
    /// cabanas em duas metades), e cada lote ocupado ganha o patamar plano na altura da rua da frente (com um pouco do chão
    /// de verdade: no máximo 30 cm abaixo e 60 cm acima), com o talude até o chão de fora, a grama do quintal com a terra
    /// batida, sem árvores e sem a grama alta onde vai a construção. Os lotes vazios ficam com a mata, mais fechada.
    /// </summary>
    public sealed partial class ChaoDeLakewood
    {
        /// <summary>Um lote no chão: o do mapa, a altura do patamar, o talude e onde fica a construção (no espaço do lote).</summary>
        public sealed class LoteNoChao
        {
            public LoteDoMapa Lote;
            public int Indice;
            /// <summary>O chão do patamar (m).</summary>
            public float Altura;
            /// <summary>A altura do asfalto da rua na frente do lote (m).</summary>
            public float AlturaDaRua;
            /// <summary>A passagem do patamar para o chão de fora (m).</summary>
            public float Talude;
            /// <summary>Onde vai a construção, no espaço do lote (x pela frente, z da frente para os fundos, a origem no meio).</summary>
            public Rect Construcao;
            /// <summary>O que vai nele (a casa, a loja ou o posto do kit; null nos vazios e nos que são montados à parte).</summary>
            public PlanoDaObra Plano;
            /// <summary>
            /// A trilha da porta até a rua (as casas, as cabanas e os especiais; 12/11/2026, a pedido: "um trilho em vez de ser
            /// reto"): os pontos no mapa, a cada meio metro, e a caixa deles com a folga da terra batida. Null sem trilha.
            /// </summary>
            public List<Vector2> Trilha;
            public Rect CaixaDaTrilha;

            /// <summary>A distância do ponto até o lote (0 dentro).</summary>
            public float Distancia(float x, float z)
            {
                var l = Lote.NoLote(new Vector2(x, z));
                float dx = Mathf.Max(Mathf.Abs(l.x) - Lote.largura * 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(l.y) - Lote.fundo * 0.5f, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            /// <summary>A distância do ponto até a construção (0 dentro).</summary>
            public float DistanciaDaConstrucao(float x, float z)
            {
                var l = Lote.NoLote(new Vector2(x, z));
                float dx = Mathf.Max(Construcao.xMin - l.x, 0f, l.x - Construcao.xMax), dz = Mathf.Max(Construcao.yMin - l.y, 0f, l.y - Construcao.yMax);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            /// <summary>Um ponto do lote no mundo (x pela frente, z para os fundos, y acima do patamar).</summary>
            public Vector3 NoMundo(float x, float z, float y = 0f)
            {
                var p = Lote.NoMapa(x, z);
                return new Vector3(p.x, Altura + y, p.y);
            }

            public Quaternion Rotacao => Quaternion.Euler(0f, Lote.giro, 0f);

            /// <summary>Se o lote leva o patamar (os vazios ficam com a mata).</summary>
            public bool Ocupado => Lote.tipo != UsoDoLote.Vazio;
        }

        /// <summary>Os lotes das cidades (os do mapa, na mesma ordem: o índice é o do <see cref="MapaDeLakewood.lotes"/>).</summary>
        public readonly List<LoteNoChao> Lotes = new List<LoteNoChao>();

        readonly Dictionary<long, List<int>> _lotes = new Dictionary<long, List<int>>();

        /// <summary>O talude máximo dos lotes (m).</summary>
        public const float TaludeMaximo = 14f;

        /// <summary>
        /// As ruas novas das cidades como vias da rede: o tipo Rua, a largura delas, a rampa de 10%; a alça (fechada) em duas
        /// metades, as duas saindo do primeiro ponto (o nó com a rua que chega nela) e se encontrando no do meio.
        /// </summary>
        public static List<ViaDoMapa> ViasDasCidades(MapaDeLakewood d)
        {
            var vias = new List<ViaDoMapa>();
            if (d.ruas == null) return vias;
            foreach (var r in d.ruas)
            {
                if (r.pontos == null || r.pontos.Count < 2) continue;
                ViaDoMapa Via(List<Vector2> pts) => new ViaDoMapa { nome = r.nome, tipo = TipoDeVia.Rua, largura = r.largura, rampaMaxima = 0.1f, pontos = pts };
                if (!r.fechada || r.pontos.Count < 4)
                {
                    vias.Add(Via(new List<Vector2>(r.pontos)));
                    continue;
                }
                int k = r.pontos.Count / 2;
                vias.Add(Via(r.pontos.GetRange(0, k + 1)));
                var volta = r.pontos.GetRange(k, r.pontos.Count - k);
                volta.Add(r.pontos[0]);
                volta.Reverse();
                vias.Add(Via(volta));
            }
            return vias;
        }

        // os lotes no chão: a altura de cada patamar (a da rua na frente, com um pouco do chão de verdade), o talude pela
        // diferença para o chão em volta e a construção (por enquanto, a pegada do desenho: a parte 3 põe a de verdade)
        void MontarOsLotes()
        {
            Lotes.Clear();
            _lotes.Clear();
            if (Mapa.lotes == null) return;
            for (int i = 0; i < Mapa.lotes.Count; i++)
            {
                var l = Mapa.lotes[i];
                var frente = l.MeioDaFrente;
                float hr = EstradaPerto(frente.x, frente.y, 25f, out _, out float alt, out _, out _) ? alt : Relevo.Altura(frente.x, frente.y);
                // o chão de verdade no lote (a média de 5 × 5 pontos)
                float soma = 0f;
                for (int a = 0; a <= 4; a++)
                for (int b = 0; b <= 4; b++)
                {
                    var p = l.NoMapa((a / 4f - 0.5f) * l.largura, (b / 4f - 0.5f) * l.fundo);
                    soma += Relevo.Altura(p.x, p.y);
                }
                float media = soma / 25f, rua = hr - 0.08f;
                float altura = rua + Mathf.Clamp(media - rua, -0.3f, 0.6f);
                // o talude: a maior diferença para o chão logo fora do lote
                float maior = 0f;
                for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                {
                    if (a == 0 && b == 0) continue;
                    var p = l.NoMapa(a * (l.largura * 0.5f + 5f), b * (l.fundo * 0.5f + 5f));
                    maior = Mathf.Max(maior, Mathf.Abs(Relevo.Altura(p.x, p.y) - altura));
                }
                PintorDeLakewood.Pegada(l, out var pm, out var pmeia);
                var lote = new LoteNoChao
                {
                    Lote = l,
                    Indice = i,
                    Altura = altura,
                    AlturaDaRua = hr,
                    Talude = Mathf.Clamp(maior * 2.5f + 2f, 3f, TaludeMaximo),
                    Construcao = Rect.MinMaxRect(pm.x - pmeia.x, pm.y - pmeia.y, pm.x + pmeia.x, pm.y + pmeia.y),
                };
                Lotes.Add(lote);
                float raio = 0.5f * Mathf.Sqrt(l.largura * l.largura + l.fundo * l.fundo) + TaludeMaximo + 3f;
                Indexar(_lotes, l.meio, l.meio, raio, i);
            }
            // o que vai em cada lote (Etapa 6, a parte 3): a construção de verdade e a pegada dela
            var cidades = new HashSet<string>();
            foreach (var l in Mapa.lotes) if (l.cidade != null) cidades.Add(l.cidade);
            foreach (var c in cidades)
                foreach (var kv in ObrasDeLakewood.Planejar(Mapa, c))
                {
                    var lote = Lotes[kv.Key];
                    lote.Plano = kv.Value;
                    lote.Construcao = kv.Value.Pegada;
                    // o subsolo do hospital: o buraco no terreno de perto (as calçadas em volta do corpo tapam a beira)
                    if (kv.Value.Uso == UsoDoLote.Hospital)
                    {
                        var corpo = ObrasDeLakewood.CorpoDoHospital(kv.Value);
                        Buracos.Add((lote.Lote.NoMapa(corpo.center.x, corpo.center.y), lote.Lote.Lado, lote.Lote.Fundos, corpo.size * 0.5f + Vector2.one * 0.3f));
                    }
                    TracarATrilha(lote);
                }
            // as cidades com os lotes: dentro do contorno delas, fora dos lotes e das ruas, a mata (12/11/2026, a pedido:
            // "colocar árvores nos espaços que ficarem muito vazios")
            _cidadesComLotes.Clear();
            if (Mapa.cidades != null)
                foreach (var c in Mapa.cidades)
                {
                    if (!cidades.Contains(c.nome) || c.contorno == null || c.contorno.Count < 3) continue;
                    float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                    foreach (var q in c.contorno)
                    {
                        x0 = Mathf.Min(x0, q.x); z0 = Mathf.Min(z0, q.y);
                        x1 = Mathf.Max(x1, q.x); z1 = Mathf.Max(z1, q.y);
                    }
                    _cidadesComLotes.Add((Rect.MinMaxRect(x0, z0, x1, z1), c.contorno));
                }
        }

        readonly List<(Rect caixa, List<Vector2> contorno)> _cidadesComLotes = new List<(Rect, List<Vector2>)>();

        /// <summary>O ponto fica dentro do contorno de uma cidade que já tem os lotes.</summary>
        public bool NaCidadeComLotes(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var (caixa, contorno) in _cidadesComLotes)
                if (caixa.Contains(p) && Linhas.Dentro(p, contorno)) return true;
            return false;
        }

        /// <summary>A meia largura da terra batida da trilha (m) e onde a grama some.</summary>
        public const float MeiaTrilha = 0.75f, SemGramaNaTrilha = 0.6f;

        // a trilha de uma casa (de uma cabana ou de um especial): da frente dos degraus até a beira do asfalto, numa curva em
        // S que chega na frente do lote de 2 a 5 m para o lado (o contrário da entrada do carro), sorteada pela semente do lote
        void TracarATrilha(LoteNoChao lote)
        {
            var p = lote.Plano;
            lote.Trilha = null;
            if (p == null || !p.Entrada.HasValue) return;
            if (p.Uso != UsoDoLote.Casa && p.Uso != UsoDoLote.Cabana && p.Uso != UsoDoLote.Especial) return;
            var l = lote.Lote;
            var e = p.Entrada.Value;
            var sorte = new System.Random(unchecked(l.semente * 31 + 7177));
            float frente = -l.fundo * 0.5f;
            float lado = (sorte.NextDouble() < 0.5 ? -1f : 1f) * (2f + 3f * (float)sorte.NextDouble());
            if (p.EntradaDoCarro.HasValue)
            {
                float longe = e.x - p.EntradaDoCarro.Value.x;
                if (Mathf.Abs(longe) > 0.5f) lado = Mathf.Sign(longe) * Mathf.Abs(lado);
            }
            float xf = Mathf.Clamp(e.x + lado, -l.largura * 0.5f + 1.5f, l.largura * 0.5f - 1.5f);
            // além da frente do lote, até o asfalto (no máximo 16 m)
            var naFrente = l.NoMapa(xf, frente);
            float alem = 16f;
            for (float d = 0f; d < 16f; d += 0.25f)
            {
                var q = naFrente + l.Frente * d;
                if (EstradaPerto(q.x, q.y, Acostamento + 0.5f, out float dist, out _, out _, out float meia) && dist <= meia + Acostamento + 0.2f)
                {
                    alem = d;
                    break;
                }
            }
            Vector2 p0 = e, p3 = new Vector2(xf, frente - alem);
            float comp = Mathf.Max(1f, p0.y - p3.y);
            // a curva: sai reto da porta, desvia no meio e chega reta na rua, com um pouco de torto
            float torto = ((float)sorte.NextDouble() - 0.5f) * 1.2f;
            Vector2 p1 = new Vector2(e.x + torto, e.y - comp * 0.42f), p2 = new Vector2(xf - torto, p3.y + comp * 0.42f);
            float aprox = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
            int n = Mathf.Max(4, Mathf.CeilToInt(aprox / 0.5f));
            lote.Trilha = new List<Vector2>(n + 1);
            float xa = float.MaxValue, za = float.MaxValue, xb = float.MinValue, zb = float.MinValue;
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n, v = 1f - u;
                var q = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
                var w = l.NoMapa(q.x, q.y);
                lote.Trilha.Add(w);
                xa = Mathf.Min(xa, w.x); za = Mathf.Min(za, w.y);
                xb = Mathf.Max(xb, w.x); zb = Mathf.Max(zb, w.y);
            }
            float f = MeiaTrilha + 1.2f;
            lote.CaixaDaTrilha = Rect.MinMaxRect(xa - f, za - f, xb + f, zb + f);
        }

        /// <summary>A distância do ponto até a trilha mais perto (até "alcance"; além, o próprio alcance).</summary>
        public float DistanciaDaTrilha(float x, float z, float alcance = 3f)
        {
            var lista = Celula(_lotes, x, z);
            if (lista == null) return alcance;
            var q = new Vector2(x, z);
            float melhor = alcance;
            foreach (int i in lista)
            {
                var tr = Lotes[i].Trilha;
                if (tr == null || !Lotes[i].CaixaDaTrilha.Contains(q)) continue;
                for (int k = 0; k + 1 < tr.Count; k++)
                    melhor = Mathf.Min(melhor, Linhas.DistanciaDoSegmento(q, tr[k], tr[k + 1], out _));
            }
            return melhor;
        }

        /// <summary>
        /// A altura que a malha do terreno de perto tem no ponto (os vértices a cada 2 m, com a altura do chão; entre eles,
        /// a mais alta das duas diagonais): o que fica rente ao chão (as pedras da trilha, as entradas dos carros) fica por
        /// cima dela. Pela altura exata, uma faixa reta afundava entre os vértices no talude dos lotes.
        /// </summary>
        public float AlturaDaMalha(float x, float z)
        {
            const float g = 2f;
            float ix = Mathf.Floor(x / g), iz = Mathf.Floor(z / g);
            float fx = x / g - ix, fz = z / g - iz;
            float x0 = ix * g, z0 = iz * g;
            float h00 = Altura(x0, z0), h10 = Altura(x0 + g, z0), h01 = Altura(x0, z0 + g), h11 = Altura(x0 + g, z0 + g);
            float a = fx > fz ? h00 + fx * (h10 - h00) + fz * (h11 - h10) : h00 + fz * (h01 - h00) + fx * (h11 - h01);
            float b = fx + fz < 1f ? h00 + fx * (h10 - h00) + fz * (h01 - h00) : h11 + (1f - fx) * (h01 - h11) + (1f - fz) * (h10 - h11);
            return Mathf.Max(a, b);
        }

        /// <summary>
        /// Os buracos no terreno de perto (o subsolo do hospital): o meio, os eixos (o x e o z da obra) e as meias medidas.
        /// O terreno de passo 2 (o de perto, com a colisão) não tem os triângulos dentro deles; o chão continua com a altura
        /// do patamar (o relevo de longe fica 6 m abaixo, embaixo do subsolo).
        /// </summary>
        public readonly List<(Vector2 centro, Vector2 eixoX, Vector2 eixoZ, Vector2 meia)> Buracos = new List<(Vector2, Vector2, Vector2, Vector2)>();

        /// <summary>Se o ponto fica num buraco do terreno (com a folga).</summary>
        public bool NoBuraco(float x, float z, float folga = 0f)
        {
            foreach (var (c, ex, ez, meia) in Buracos)
            {
                var q = new Vector2(x - c.x, z - c.y);
                if (Mathf.Abs(Vector2.Dot(q, ex)) <= meia.x + folga && Mathf.Abs(Vector2.Dot(q, ez)) <= meia.y + folga) return true;
            }
            return false;
        }

        // se o bloco encosta num buraco (para o terreno não conferir ponto a ponto à toa)
        bool BlocoComBuraco(int bx, int bz)
        {
            if (Buracos.Count == 0) return false;
            var canto = CantoDoBloco(bx, bz);
            foreach (var (c, _, _, meia) in Buracos)
            {
                float r = meia.magnitude;
                if (c.x + r < canto.x || c.x - r > canto.x + TamanhoDoBloco || c.y + r < canto.y || c.y - r > canto.y + TamanhoDoBloco) continue;
                return true;
            }
            return false;
        }

        /// <summary>Um lote ocupado da cidade no ponto (até "folga" fora dele), ou null.</summary>
        public LoteNoChao LoteEm(float x, float z, float folga = 0f)
        {
            var lista = Celula(_lotes, x, z);
            if (lista == null) return null;
            LoteNoChao melhor = null;
            float md = float.MaxValue;
            foreach (int i in lista)
            {
                var l = Lotes[i];
                float d = l.Distancia(x, z);
                if (d <= folga && d < md)
                {
                    md = d;
                    melhor = l;
                }
            }
            return melhor;
        }

        // o chão com os patamares dos lotes ocupados: plano dentro de cada um e o talude até o chão de fora (do mais longe
        // para o mais perto: perto de um lote, o patamar dele manda)
        float ChaoDosLotes(float x, float z, float h)
        {
            var lista = Celula(_lotes, x, z);
            if (lista == null) return h;
            Span<int> quais = stackalloc int[12];
            Span<float> dists = stackalloc float[12];
            int n = 0;
            foreach (int i in lista)
            {
                var l = Lotes[i];
                if (!l.Ocupado) continue;
                float d = l.Distancia(x, z);
                if (d <= 0f) return l.Altura;
                if (d >= l.Talude || n == quais.Length) continue;
                int k = n++;
                // em ordem da distância, a maior primeiro
                while (k > 0 && dists[k - 1] < d)
                {
                    quais[k] = quais[k - 1];
                    dists[k] = dists[k - 1];
                    k--;
                }
                quais[k] = i;
                dists[k] = d;
            }
            for (int k = 0; k < n; k++)
            {
                var l = Lotes[quais[k]];
                h = Mathf.Lerp(l.Altura, h, Geo.Suave(0f, l.Talude, dists[k]));
            }
            return h;
        }

        // dentro de um lote ocupado, o chão do patamar
        float NoPatamar(float x, float z, float h)
        {
            var lista = Celula(_lotes, x, z);
            if (lista == null) return h;
            foreach (int i in lista)
            {
                var l = Lotes[i];
                if (l.Ocupado && l.Distancia(x, z) <= 0f) return l.Altura;
            }
            return h;
        }

        // os pesos do chão dos lotes ocupados: a grama do quintal com manchas de terra batida, sem as folhas da mata
        void PesosDosLotes(float x, float z, ref float r, ref float g, ref float b, ref float a)
        {
            var l = LoteEm(x, z, 2f);
            if (l != null && l.Ocupado)
            {
                float w = 1f - Geo.Suave(0f, 2f, l.Distancia(x, z));
                if (w > 0f)
                {
                    float n = 0.5f + 0.5f * _sorte.Amostra(x / 9f, z / 9f);
                    float terra = Mathf.Lerp(0.08f, 0.38f, Geo.Suave(0.55f, 0.85f, n));
                    r = Mathf.Lerp(r, 0.9f - terra, w);
                    g = Mathf.Lerp(g, terra, w);
                    b = Mathf.Lerp(b, 0.03f, w);
                    a = Mathf.Lerp(a, 0f, w);
                }
            }
            // a trilha: a terra batida na grama, por cima do lote (antes, o peso do lote a cobria), mais larga que as pedras
            // (o chão de perto tem um vértice a cada 2 m)
            float dt = DistanciaDaTrilha(x, z, 2.5f);
            if (dt < 2.4f)
            {
                float wt = 1f - Geo.Suave(MeiaTrilha, 2.4f, dt);
                r = Mathf.Lerp(r, 0.12f, wt);
                g = Mathf.Lerp(g, 0.8f, wt);
                b = Mathf.Lerp(b, 0.08f, wt);
                a = Mathf.Lerp(a, 0f, wt);
            }
        }

        // as plantas nas cidades (12/11/2026, a pedido: "colocar árvores nos espaços que ficarem muito vazios"): -1 nada; acima
        // de 0, a chance mínima de uma árvore no ponto. Nos vazios, a mata fechada; nos quintais das casas, das cabanas e dos
        // especiais, umas árvores atrás e dos lados (a 4 m da construção; a frente fica limpa); entre os lotes, nada (a
        // folga de 2,5 m); e no resto da cidade com os lotes (entre as ruas, atrás dos quintais), a mata com as clareiras
        float PlantaNaCidade(float x, float z)
        {
            var lista = Celula(_lotes, x, z);
            float saida = 0f;
            if (lista != null)
                foreach (int i in lista)
                {
                    var l = Lotes[i];
                    float d = l.Distancia(x, z);
                    if (!l.Ocupado)
                    {
                        if (d <= 0f && DentroComFolga(l, x, z, 2f)) saida = Mathf.Max(saida, 0.85f);
                        continue;
                    }
                    if (d > 2.5f) continue;
                    if (d > 0f) return -1f;
                    return QuintalDaPlanta(l, x, z);
                }
            if (saida > 0f) return saida;
            if (_cidadesComLotes.Count > 0 && NaCidadeComLotes(x, z)) return Mathf.Lerp(0.45f, 0.03f, Clareira(x, z));
            return 0f;
        }

        // dentro de um lote ocupado: a chance de uma árvore no quintal
        float QuintalDaPlanta(LoteNoChao l, float x, float z)
        {
            var uso = l.Plano != null ? l.Plano.Uso : l.Lote.tipo;
            if (uso != UsoDoLote.Casa && uso != UsoDoLote.Cabana && uso != UsoDoLote.Especial) return -1f;
            var p = l.Lote.NoLote(new Vector2(x, z));
            if (Mathf.Abs(p.x) > l.Lote.largura * 0.5f - 1.2f || Mathf.Abs(p.y) > l.Lote.fundo * 0.5f - 1.2f) return -1f;
            if (l.DistanciaDaConstrucao(x, z) < 4f) return -1f;
            if (DistanciaDaTrilha(x, z, 3f) < 2.5f) return -1f;
            if (uso == UsoDoLote.Cabana) return 0.55f;   // a cabana no meio da mata
            if (p.y < l.Construcao.yMin + 1f) return -1f;   // a frente limpa (o caminho e a entrada do carro)
            // atrás das casas, umas árvores; atrás dos especiais (o pátio da escola, o dos bombeiros), menos
            float atras = uso == UsoDoLote.Especial ? 0.07f : 0.2f;
            return p.y > l.Construcao.yMax + 2f ? atras : 0.04f;
        }

        static bool DentroComFolga(LoteNoChao l, float x, float z, float folga)
        {
            var p = l.Lote.NoLote(new Vector2(x, z));
            return Mathf.Abs(p.x) <= l.Lote.largura * 0.5f - folga && Mathf.Abs(p.y) <= l.Lote.fundo * 0.5f - folga;
        }

        // a grama densa nos lotes ocupados: nada onde vai a construção (os tufos furariam o piso), rala no quintal
        float GramaDosLotes(float x, float z)
        {
            float trilha = Geo.Suave(SemGramaNaTrilha, SemGramaNaTrilha + 0.6f, DistanciaDaTrilha(x, z, 2f));
            if (trilha <= 0f) return 0f;
            return trilha * GramaSoDosLotes(x, z);
        }

        float GramaSoDosLotes(float x, float z)
        {
            var l = LoteEm(x, z, 1.5f);
            if (l == null || !l.Ocupado) return 1f;
            float dentro = 1f - Geo.Suave(0f, 1.5f, l.Distancia(x, z));
            float longeDaObra = Geo.Suave(2f, 3.5f, l.DistanciaDaConstrucao(x, z));
            return Mathf.Lerp(1f, 0.45f * longeDaObra, dentro);
        }
    }
}
