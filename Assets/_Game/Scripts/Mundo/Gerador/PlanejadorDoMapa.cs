using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Faz o <see cref="PlanoDoMapa"/> (Plano_Mapa, Parte M3), depois do lago e antes das alturas (usa o relevo natural com o
    /// lago). A ordem:
    ///  1. as cidades grandes, as pequenas e as vilas, pela tabela dos tamanhos, no chão mais liso (o menos áspero de vários
    ///     sorteios), fora da serra e longe do lago e umas das outras; cada uma com o giro das ruas, as zonas e os bairros;
    ///  2. o laboratório (no anel do comércio da maior cidade), as bases militares (mais para fora do mapa), as comunidades
    ///     aliadas e inimigas, as fazendas (no chão aberto e liso), as cabanas (algumas na beira do lago, outras soltas, até
    ///     o pé da serra; umas de preparador) e a casa do lago (na ilha);
    ///  3. as estradas (<see cref="CaminhosDoMapa"/>): as rodovias entre as cidades (a árvore mais curta e mais algumas
    ///     ligações, para haver dois caminhos), as saídas do mapa pela serra (com a barreira da quarentena na ponta), as
    ///     estradas para as vilas, as bases e as comunidades, e as de terra para as fazendas, as cabanas e a beira do lago;
    ///  4. os postos (um na saída de cada cidade e os outros espalhados na beira das estradas), os bunkers (nos bairros de
    ///     casas, nas fazendas e nas cabanas de preparador) e a casa da família (num bairro de casas de uma cidade pequena);
    ///  5. o metrô: a árvore mais curta entre as cidades, partida em linhas, com as estações.
    /// Cada etapa tem o seu sorteio; o resultado é o mesmo em todas as máquinas.
    /// </summary>
    public static class PlanejadorDoMapa
    {
        // a tabela dos tamanhos (Plano_Mapa): de, até
        struct Quantidades
        {
            public float[] RaiosDasGrandes;
            public int PequenasDe, PequenasAte, VilasDe, VilasAte, FazendasDe, FazendasAte, Bases, Comunidades;
            public int CabanasDe, CabanasAte, PostosDe, PostosAte, BunkersDe, BunkersAte, Saidas, LigacoesAMais;
        }

        static Quantidades Tabela(TamanhoDoMundo t) => t switch
        {
            TamanhoDoMundo.Pequeno => new Quantidades
            {
                RaiosDasGrandes = new[] { 450f }, PequenasDe = 2, PequenasAte = 2, VilasDe = 2, VilasAte = 3, FazendasDe = 5, FazendasAte = 8,
                Bases = 1, Comunidades = 2, CabanasDe = 8, CabanasAte = 12, PostosDe = 4, PostosAte = 6, BunkersDe = 5, BunkersAte = 8,
                Saidas = 2, LigacoesAMais = 1,
            },
            TamanhoDoMundo.Medio => new Quantidades
            {
                RaiosDasGrandes = new[] { 750f, 500f }, PequenasDe = 3, PequenasAte = 4, VilasDe = 4, VilasAte = 6, FazendasDe = 10, FazendasAte = 15,
                Bases = 2, Comunidades = 3, CabanasDe = 15, CabanasAte = 25, PostosDe = 8, PostosAte = 12, BunkersDe = 10, BunkersAte = 15,
                Saidas = 3, LigacoesAMais = 2,
            },
            _ => new Quantidades
            {
                RaiosDasGrandes = new[] { 1000f, 700f, 650f }, PequenasDe = 6, PequenasAte = 8, VilasDe = 8, VilasAte = 12, FazendasDe = 20, FazendasAte = 30,
                Bases = 3, Comunidades = 5, CabanasDe = 30, CabanasAte = 50, PostosDe = 15, PostosAte = 20, BunkersDe = 20, BunkersAte = 30,
                Saidas = 4, LigacoesAMais = 2,
            },
        };

        public static PlanoDoMapa Planejar(DadosDoMundo d)
        {
            var plano = new PlanoDoMapa();
            var nomes = new Nomes();
            var q = Tabela(d.Tamanho);
            var s = Sorteio.Da(d.Semente, "plano-do-mapa");
            var contorno = new Ruido(d.Semente, "contorno-das-cidades");

            // 1. as cidades
            foreach (float raio in q.RaiosDasGrandes)
                PorCidade(d, plano, nomes, s, contorno, TipoDeLugar.CidadeGrande, raio * s.Entre(0.92f, 1.08f), 420f);
            int pequenas = s.Entre(q.PequenasDe, q.PequenasAte + 1);
            for (int i = 0; i < pequenas; i++)
                PorCidade(d, plano, nomes, s, contorno, TipoDeLugar.CidadePequena, s.Entre(200f, 290f), 300f);
            int vilas = s.Entre(q.VilasDe, q.VilasAte + 1);
            for (int i = 0; i < vilas; i++)
                PorCidade(d, plano, nomes, s, contorno, TipoDeLugar.Vila, s.Entre(80f, 125f), 220f);
            // o traçado de cada uma (Parte M5): as ruas, os portões e o chão aplainado
            var ondas = new Ruido(d.Semente, "ruas-tortas");
            for (int ci = 0; ci < plano.Cidades.Count; ci++)
            {
                var t = new TracadoDaCidade(d, plano.Cidades[ci], ci, ondas);
                t.TracarRuas();
                t.MedirOChao();
                plano.Cidades[ci].Tracado = t;
                plano.Tracados.Add(t);
            }
            d.IndexarCidades(plano.Tracados);

            // 2. os outros lugares
            PorLaboratorio(plano, nomes, s);
            for (int i = 0; i < q.Bases; i++)
            {
                var b = PorLugar(d, plano, s, TipoDeLugar.BaseMilitar, s.Entre(150f, 200f), 0.7f, 250f, 40,
                    c => Aspereza(d, c, 170f) - c.magnitude * 0.004f);
                if (b != null) b.Nome = nomes.Base(s);
            }
            for (int i = 0; i < q.Comunidades * 2; i++)
            {
                bool aliada = i % 2 == 0;
                var c = PorLugar(d, plano, s, aliada ? TipoDeLugar.ComunidadeAliada : TipoDeLugar.ComunidadeInimiga, s.Entre(70f, 105f), 0.85f, 200f, 30,
                    p => Aspereza(d, p, 90f));
                if (c != null) c.Nome = aliada ? nomes.Aliada(s) : nomes.Inimiga(s);
            }
            int fazendas = s.Entre(q.FazendasDe, q.FazendasAte + 1);
            for (int i = 0; i < fazendas; i++)
            {
                var f = PorLugar(d, plano, s, TipoDeLugar.Fazenda, s.Entre(70f, 110f), 0.9f, 140f, 25, p => Aspereza(d, p, 100f));
                if (f != null) f.Nome = nomes.Fazenda(s);
            }
            int cabanas = s.Entre(q.CabanasDe, q.CabanasAte + 1);
            for (int i = 0; i < cabanas; i++)
            {
                // um quarto na beira do lago; o resto solto, até o pé da serra (bem antes da cerca da quarentena, que fica na
                // metade da faixa)
                bool noLago = d.Lago != null && i % 4 == 0;
                var cab = noLago ? CabanaNoLago(d, plano, s) : PorLugar(d, plano, s, TipoDeLugar.Cabana, 14f, 0.65f, 90f, 8, p => s.Fracao());
                if (cab != null) cab.Preparador = s.Chance(0.3f);
            }
            if (d.Lago != null)
            {
                plano.CasaDoLago = new Lugar { Tipo = TipoDeLugar.CasaDoLago, Nome = "Casa do lago", Centro = d.Lago.Centro, Raio = d.Lago.RaioDaIlha };
                plano.Lugares.Add(plano.CasaDoLago);
            }

            // 3. as estradas
            var caminhos = new CaminhosDoMapa(d, plano);
            TracarAsRodovias(d, plano, caminhos, s, q);
            TracarAsSaidas(d, plano, caminhos, s, q);
            foreach (var l in plano.Lugares.ToArray())
            {
                if (l.Tipo == TipoDeLugar.Vila || l.Tipo == TipoDeLugar.BaseMilitar || l.Tipo == TipoDeLugar.ComunidadeAliada || l.Tipo == TipoDeLugar.ComunidadeInimiga)
                    LigarNaRede(plano, caminhos, l, TipoDeEstrada.Estrada);
            }
            foreach (var l in plano.Lugares.ToArray())
                if (l.Tipo == TipoDeLugar.Fazenda || l.Tipo == TipoDeLugar.Cabana) LigarNaRede(plano, caminhos, l, TipoDeEstrada.Terra);
            if (plano.CasaDoLago != null) LigarABeiraDoLago(d, plano, caminhos);
            // os caminhos por dentro das cidades: do meio até cada portão usado, pelas avenidas
            PorOsConectores(plano);

            // 4. os postos, os lotes das cidades (fora das estradas e dos postos), a casa da família e os bunkers
            PorPostos(d, plano, nomes, s, q);
            foreach (var t in plano.Tracados) t.Lotear(plano);
            PorACasaDaFamilia(plano, s);
            PorBunkers(plano, s, q);
            foreach (var t in plano.Tracados) t.Indexar();

            // 5. o metrô
            TracarOMetro(plano, s);
            return plano;
        }

        // ------------------------------------------------------------------ os lugares

        /// <summary>O quanto o chão é áspero em volta do ponto: o maior desvio da média de 17 alturas até o raio (m).</summary>
        static float Aspereza(DadosDoMundo d, Vector2 c, float raio)
        {
            float soma = 0f;
            var h = new float[17];
            h[0] = GeradorDoMundo.AlturaComLago(d, c.x, c.y);
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                h[1 + a] = GeradorDoMundo.AlturaComLago(d, c.x + dir.x * raio * 0.5f, c.y + dir.y * raio * 0.5f);
                h[9 + a] = GeradorDoMundo.AlturaComLago(d, c.x + dir.x * raio, c.y + dir.y * raio);
            }
            foreach (float v in h) soma += v;
            float media = soma / h.Length, desvio = 0f;
            foreach (float v in h) desvio = Mathf.Max(desvio, Mathf.Abs(v - media));
            return desvio;
        }

        /// <summary>
        /// O lugar cabe ali: dentro do mapa (fora da serra até a fração dela), fora do lago (com folga) e longe dos outros
        /// lugares (a soma dos raios mais a folga; os pequenos, só o raio deles).
        /// </summary>
        static bool Cabe(DadosDoMundo d, PlanoDoMapa plano, Vector2 c, float raio, float fracaoDaSerra, float folga)
        {
            float limite = d.Metade - d.FaixaDaSerra * fracaoDaSerra - raio;
            if (Mathf.Abs(c.x) > limite || Mathf.Abs(c.y) > limite) return false;
            if (d.Lago != null && d.Lago.Q(c.x, c.y) < 1.3f + raio / d.Lago.Raio) return false;
            foreach (var l in plano.Lugares)
            {
                bool pequeno = l.Tipo == TipoDeLugar.Cabana || l.Tipo == TipoDeLugar.Posto || l.Tipo == TipoDeLugar.Bunker;
                float minimo = l.Raio + raio + (pequeno ? 30f : folga);
                if ((l.Centro - c).sqrMagnitude < minimo * minimo) return false;
            }
            return true;
        }

        static Lugar PorLugar(DadosDoMundo d, PlanoDoMapa plano, Sorteio s, TipoDeLugar tipo, float raio, float fracaoDaSerra, float folga,
                              int tentativas, Func<Vector2, float> nota)
        {
            float limite = d.Metade - d.FaixaDaSerra * fracaoDaSerra - raio;
            Vector2 melhor = Vector2.zero;
            float melhorNota = float.MaxValue;
            bool achou = false;
            for (int k = 0; k < tentativas * 6 && (k < tentativas || !achou); k++)
            {
                var c = new Vector2(Mathf.Round(s.Entre(-limite, limite)), Mathf.Round(s.Entre(-limite, limite)));
                if (!Cabe(d, plano, c, raio, fracaoDaSerra, folga)) continue;
                float n = nota(c);
                if (n < melhorNota)
                {
                    melhorNota = n;
                    melhor = c;
                    achou = true;
                }
            }
            if (!achou) return null;
            var lugar = new Lugar { Tipo = tipo, Centro = melhor, Raio = raio, Giro = Mathf.Round(s.Entre(0f, 90f)) };
            plano.Lugares.Add(lugar);
            return lugar;
        }

        static void PorCidade(DadosDoMundo d, PlanoDoMapa plano, Nomes nomes, Sorteio s, Ruido contorno, TipoDeLugar tipo, float raio, float folga)
        {
            var lugar = PorLugar(d, plano, s, tipo, Mathf.Round(raio), 1f, folga, tipo == TipoDeLugar.Vila ? 25 : 40,
                c => Aspereza(d, c, raio));
            if (lugar == null) return;
            plano.Lugares.Remove(lugar);
            var cidade = new Cidade
            {
                Tipo = tipo, Centro = lugar.Centro, Raio = lugar.Raio, Giro = lugar.Giro, Fase = s.Entre(0f, 100f), Contorno = contorno,
                Nome = nomes.Cidade(s, false),
            };
            switch (tipo)
            {
                case TipoDeLugar.CidadeGrande:
                    cidade.FimDoCentro = 0.28f;
                    cidade.FimDoComercio = 0.52f;
                    cidade.Bairros.Add(new Bairro { Nome = "Centro", Tipo = TipoDeBairro.Centro, Centro = cidade.Centro });
                    cidade.Bairros.Add(new Bairro { Nome = nomes.Bairro(s), Tipo = TipoDeBairro.Comercio, Centro = cidade.Centro + Direcao(s.Entre(0f, 360f)) * cidade.Raio * 0.4f });
                    SetoresDeCasas(cidade, nomes, s, s.Entre(6, 9));
                    break;
                case TipoDeLugar.CidadePequena:
                    cidade.FimDoCentro = 0f;
                    cidade.FimDoComercio = 0.35f;
                    cidade.Bairros.Add(new Bairro { Nome = "Centro", Tipo = TipoDeBairro.Comercio, Centro = cidade.Centro });
                    SetoresDeCasas(cidade, nomes, s, s.Entre(3, 5));
                    break;
                default:
                    // a vila: um bairro de casas só, com o nome dela
                    cidade.Bairros.Add(new Bairro { Nome = cidade.Nome, Tipo = TipoDeBairro.Residencial, AnguloDe = 0f, AnguloAte = 360f, Centro = cidade.Centro });
                    break;
            }
            plano.Lugares.Add(cidade);
            plano.Cidades.Add(cidade);
        }

        static Vector2 Direcao(float graus) => new Vector2(Mathf.Cos(graus * Mathf.Deg2Rad), Mathf.Sin(graus * Mathf.Deg2Rad));

        // os bairros de casas em setores, com as divisas um pouco tortas
        static void SetoresDeCasas(Cidade cidade, Nomes nomes, Sorteio s, int setores)
        {
            float inicio = s.Entre(0f, 360f), passo = 360f / setores;
            for (int k = 0; k < setores; k++)
            {
                float de = Mathf.Repeat(inicio + k * passo + (k == 0 ? 0f : s.Entre(-passo * 0.15f, passo * 0.15f)), 360f);
                float ate = Mathf.Repeat(inicio + (k + 1) * passo, 360f);
                float meio = de + Mathf.Repeat(ate - de, 360f) * 0.5f;
                cidade.Bairros.Add(new Bairro
                {
                    Nome = nomes.Bairro(s), Tipo = TipoDeBairro.Residencial, AnguloDe = de, AnguloAte = ate,
                    Centro = cidade.Centro + Direcao(meio) * cidade.Raio * (cidade.FimDoComercio + 1f) * 0.5f,
                });
            }
            // as divisas: o fim de cada setor é o começo do próximo
            for (int k = 0; k < setores; k++)
            {
                var b = cidade.Bairros[cidade.Bairros.Count - setores + k];
                b.AnguloAte = cidade.Bairros[cidade.Bairros.Count - setores + (k + 1) % setores].AnguloDe;
            }
        }

        // o laboratório: no anel do comércio da maior cidade, murado
        static void PorLaboratorio(PlanoDoMapa plano, Nomes nomes, Sorteio s)
        {
            Cidade maior = null;
            foreach (var c in plano.Cidades)
                if (c.Tipo == TipoDeLugar.CidadeGrande && (maior == null || c.Raio > maior.Raio)) maior = c;
            if (maior == null) return;
            float distancia = maior.Raio * (maior.FimDoCentro + maior.FimDoComercio) * 0.5f;
            var lab = new Lugar
            {
                Tipo = TipoDeLugar.Laboratorio, Nome = nomes.Laboratorio(s), Raio = 85f, Giro = maior.Giro,
                Centro = maior.Centro + Direcao(s.Entre(0f, 360f)) * distancia,
            };
            plano.Lugares.Add(lab);
            plano.Laboratorio = lab;
        }

        static Lugar CabanaNoLago(DadosDoMundo d, PlanoDoMapa plano, Sorteio s)
        {
            var lago = d.Lago;
            for (int k = 0; k < 20; k++)
            {
                float ang = s.Entre(0f, Mathf.PI * 2f);
                float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                float r = lago.RaioNaDirecao(cx, cz) * s.Entre(1.45f, 1.8f);
                var c = new Vector2(Mathf.Round(lago.Centro.x + cx * r), Mathf.Round(lago.Centro.y + cz * r));
                if (!d.Dentro(c.x, c.y, d.FaixaDaSerra * 0.6f)) continue;
                bool livre = true;
                foreach (var l in plano.Lugares)
                    if ((l.Centro - c).magnitude < l.Raio + 40f) livre = false;
                if (!livre) continue;
                var cab = new Lugar { Tipo = TipoDeLugar.Cabana, Centro = c, Raio = 14f, Giro = Mathf.Round(Mathf.Atan2(-cz, -cx) * Mathf.Rad2Deg) };
                plano.Lugares.Add(cab);
                return cab;
            }
            return null;
        }

        // ------------------------------------------------------------------ as estradas

        static void TracarAsRodovias(DadosDoMundo d, PlanoDoMapa plano, CaminhosDoMapa caminhos, Sorteio s, Quantidades q)
        {
            var cidades = new List<Cidade>();
            foreach (var c in plano.Cidades)
                if (c.Tipo != TipoDeLugar.Vila) cidades.Add(c);
            if (cidades.Count < 2) return;
            var arestas = ArvoreMaisCurta(cidades);
            // as ligações a mais: as mais curtas que não estão na árvore e que encurtam muito o caminho pela árvore
            var extras = new List<(int a, int b, float d)>();
            for (int a = 0; a < cidades.Count; a++)
            for (int b = a + 1; b < cidades.Count; b++)
            {
                if (arestas.Contains((a, b)) || arestas.Contains((b, a))) continue;
                float direto = Vector2.Distance(cidades[a].Centro, cidades[b].Centro);
                float pelaArvore = DistanciaNaArvore(cidades, arestas, a, b);
                if (pelaArvore > direto * 1.6f) extras.Add((a, b, direto));
            }
            extras.Sort((x, y) => x.d.CompareTo(y.d));
            for (int k = 0; k < extras.Count && k < q.LigacoesAMais; k++) arestas.Add((extras[k].a, extras[k].b));
            // as mais curtas primeiro: as longas aproveitam o que já existe
            arestas.Sort((x, y) => Vector2.Distance(cidades[x.Item1].Centro, cidades[x.Item2].Centro)
                .CompareTo(Vector2.Distance(cidades[y.Item1].Centro, cidades[y.Item2].Centro)));
            foreach (var (a, b) in arestas)
            {
                // de portão a portão: o de cada cidade que olha para a outra (se não der, o segundo melhor)
                var ta = cidades[a].Tracado;
                var tb = cidades[b].Tracado;
                Estrada e = null;
                var pa1 = ta.PortaoPara(cidades[b].Centro);
                var pb1 = tb.PortaoPara(cidades[a].Centro);
                var pa2 = ta.PortaoPara(cidades[b].Centro, pa1);
                var pb2 = tb.PortaoPara(cidades[a].Centro, pb1);
                foreach (var (pa, pb) in new[] { (pa1, pb1), (pa2, pb1), (pa1, pb2), (pa2, pb2) })
                {
                    if (pa == null || pb == null) continue;
                    e = Ligar(caminhos, pa, pb.Saida, pb, TipoDeEstrada.Rodovia, false, cidades[a], cidades[b]);
                    if (e != null) break;
                }
                if (e == null) continue;
                e.De = plano.Indice(cidades[a]);
                e.Para = plano.Indice(cidades[b]);
                plano.Estradas.Add(e);
            }
        }

        /// <summary>
        /// A estrada que sai do portão "de" (a avenida até fora do contorno, o <see cref="Portao.Prefixo"/>) e vai pelo
        /// relevo até "alvo"; se "ate" for outro portão, chega pela avenida dele (o prefixo de trás para a frente). A busca
        /// sai e chega na direção das avenidas. Marca os portões usados.
        /// </summary>
        static Estrada Ligar(CaminhosDoMapa caminhos, Portao de, Vector2 alvo, Portao ate, TipoDeEstrada tipo, bool pelaSerra, Lugar lugarDe, Lugar lugarAte)
        {
            var inicio = de != null ? de.Saida : alvo;
            var fim = ate != null ? ate.Saida : alvo;
            var meio = caminhos.Tracar(inicio, fim, tipo, pelaSerra, lugarDe, lugarAte, de?.Dir, ate != null ? -ate.Dir : (Vector2?)null);
            if (meio == null) return null;
            var e = new Estrada { Tipo = tipo };
            var juntos = new List<Vector2>();
            if (de != null) juntos.AddRange(de.Prefixo);
            for (int i = de != null ? 1 : 0; i < meio.Pontos.Count; i++) juntos.Add(meio.Pontos[i]);
            if (ate != null)
                for (int i = ate.Prefixo.Count - 2; i >= 0; i--) juntos.Add(ate.Prefixo[i]);
            // arredonda as emendas (a avenida reta e a busca, que pode virar 22° no primeiro passo): na avenida os pontos
            // continuam na reta dela, e a ponta (a beira do cruzamento) fica
            CaminhosDoMapa.Reamostrar(CaminhosDoMapa.Chaikin(CaminhosDoMapa.Chaikin(juntos)), 12f, e.Pontos);
            // (sem um último pedaço curto: na serra, ele virava uma rampa)
            while (e.Pontos.Count > 2 && Vector2.Distance(e.Pontos[e.Pontos.Count - 1], e.Pontos[e.Pontos.Count - 2]) < 8f) e.Pontos.RemoveAt(e.Pontos.Count - 2);
            for (int i = 1; i < e.Pontos.Count; i++) e.Comprimento += Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
            if (de != null) de.Usado = true;
            if (ate != null) ate.Usado = true;
            return e;
        }

        // os caminhos por dentro das cidades (Estrada.Conector), um por portão usado: do meio da cidade até a beira do
        // cruzamento do portão, pela avenida. Entram no grafo das placas; as ruas de verdade são as da cidade
        static void PorOsConectores(PlanoDoMapa plano)
        {
            foreach (var t in plano.Tracados)
                foreach (var p in t.Portoes)
                {
                    if (!p.Usado) continue;
                    var e = new Estrada { Tipo = t.EhVila ? TipoDeEstrada.Estrada : TipoDeEstrada.Rodovia, Conector = true, De = plano.Indice(t.Cidade) };
                    e.Pontos.AddRange(p.Conector);
                    for (int i = 1; i < e.Pontos.Count; i++) e.Comprimento += Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
                    if (e.Pontos.Count >= 2) plano.Estradas.Add(e);
                }
        }

        /// <summary>A árvore mais curta (Prim) entre os lugares: pares de índices.</summary>
        static List<(int, int)> ArvoreMaisCurta<T>(List<T> lugares) where T : Lugar
        {
            var arestas = new List<(int, int)>();
            var dentro = new bool[lugares.Count];
            dentro[0] = true;
            for (int n = 1; n < lugares.Count; n++)
            {
                float menor = float.MaxValue;
                int ma = -1, mb = -1;
                for (int a = 0; a < lugares.Count; a++)
                {
                    if (!dentro[a]) continue;
                    for (int b = 0; b < lugares.Count; b++)
                    {
                        if (dentro[b]) continue;
                        float dist = Vector2.Distance(lugares[a].Centro, lugares[b].Centro);
                        if (dist < menor)
                        {
                            menor = dist;
                            ma = a;
                            mb = b;
                        }
                    }
                }
                dentro[mb] = true;
                arestas.Add((ma, mb));
            }
            return arestas;
        }

        static float DistanciaNaArvore<T>(List<T> lugares, List<(int, int)> arestas, int de, int ate) where T : Lugar
        {
            // Dijkstra pequeno (poucos lugares)
            var dist = new float[lugares.Count];
            var feito = new bool[lugares.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;
            dist[de] = 0f;
            for (int it = 0; it < lugares.Count; it++)
            {
                int u = -1;
                for (int i = 0; i < dist.Length; i++)
                    if (!feito[i] && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || dist[u] == float.MaxValue) break;
                feito[u] = true;
                foreach (var (a, b) in arestas)
                {
                    int v = a == u ? b : b == u ? a : -1;
                    if (v < 0) continue;
                    float nd = dist[u] + Vector2.Distance(lugares[u].Centro, lugares[v].Centro);
                    if (nd < dist[v]) dist[v] = nd;
                }
            }
            return dist[ate];
        }

        // as saídas do mapa: dos 4 lados (sorteados), a cidade mais perto do lado leva uma rodovia subindo a serra até um
        // pouco além da cerca da quarentena; a barreira fica onde a rodovia cruza a cerca
        static void TracarAsSaidas(DadosDoMundo d, PlanoDoMapa plano, CaminhosDoMapa caminhos, Sorteio s, Quantidades q)
        {
            var lados = new List<Vector2> { Vector2.up, Vector2.down, Vector2.right, Vector2.left };
            for (int i = lados.Count - 1; i > 0; i--)
            {
                int j = s.Entre(0, i + 1);
                (lados[i], lados[j]) = (lados[j], lados[i]);
            }
            for (int k = 0; k < q.Saidas && k < lados.Count; k++)
            {
                var lado = lados[k];
                Cidade melhor = null;
                float maior = float.MinValue;
                foreach (var c in plano.Cidades)
                {
                    if (c.Tipo == TipoDeLugar.Vila) continue;
                    float nota = Vector2.Dot(c.Centro, lado) + c.Raio * 0.5f;
                    if (nota > maior)
                    {
                        maior = nota;
                        melhor = c;
                    }
                }
                if (melhor == null) return;
                // a saída passa 16 m da cerca da quarentena (a barreira fica em cima da cerca)
                float borda = d.Cerca + 16f;
                var lateral = new Vector2(lado.y, lado.x);
                float ao = Mathf.Clamp(Vector2.Dot(melhor.Centro, lateral) + s.Entre(-300f, 300f), -d.Cerca * 0.6f, d.Cerca * 0.6f);
                var alvo = lado * borda + lateral * ao;
                caminhos.PassarDaCerca = true;
                // pelo portão da cidade que olha para o lado
                var portao = melhor.Tracado.PortaoPara(melhor.Centro + lado * 4000f);
                var e = Ligar(caminhos, portao, alvo, null, TipoDeEstrada.Rodovia, true, melhor, null);
                caminhos.PassarDaCerca = false;
                if (e == null) continue;
                e.De = plano.Indice(melhor);
                plano.Estradas.Add(e);
                var barreira = new Lugar
                {
                    Tipo = TipoDeLugar.Barreira, Nome = "Barreira da quarentena", Raio = 20f,
                    Centro = alvo - lado * 16f, Giro = Mathf.Atan2(lado.y, lado.x) * Mathf.Rad2Deg,
                };
                plano.Lugares.Add(barreira);
                e.Para = plano.Indice(barreira);
            }
        }

        static void LigarNaRede(PlanoDoMapa plano, CaminhosDoMapa caminhos, Lugar lugar, TipoDeEstrada tipo)
        {
            var alvo = CaminhosDoMapa.MaisPerto(plano, lugar.Centro, out float distancia, caminhos.Dados);
            if (distancia == float.MaxValue || distancia < lugar.Raio * 0.5f) return;
            Estrada e;
            if (lugar is Cidade vila && vila.Tracado != null)
            {
                // a vila sai pela ponta da rua principal que olha para a rede
                var portao = vila.Tracado.PortaoPara(alvo);
                e = Ligar(caminhos, portao, alvo, null, tipo, false, lugar, null)
                    ?? Ligar(caminhos, vila.Tracado.PortaoPara(alvo, portao), alvo, null, tipo, false, lugar, null);
            }
            else
            {
                // a de terra sobe a serra (as cabanas no pé dela); as outras, não
                e = caminhos.Tracar(lugar.Centro, alvo, tipo, tipo == TipoDeEstrada.Terra, lugar, null);
            }
            if (e == null) return;
            e.De = plano.Indice(lugar);
            plano.Estradas.Add(e);
        }

        // a estrada de terra até a beira do lago, no ponto da margem mais perto da rede (a casa fica na ilha, sem ponte)
        static void LigarABeiraDoLago(DadosDoMundo d, PlanoDoMapa plano, CaminhosDoMapa caminhos)
        {
            var lago = d.Lago;
            var rede = CaminhosDoMapa.MaisPerto(plano, lago.Centro, out float distancia, d);
            if (distancia == float.MaxValue) return;
            var dir = (rede - lago.Centro).normalized;
            var beira = lago.Centro + dir * lago.RaioNaDirecao(dir.x, dir.y) * 1.22f;
            var e = caminhos.Tracar(beira, rede, TipoDeEstrada.Terra, false, plano.CasaDoLago, null);
            if (e == null) return;
            e.De = plano.Indice(plano.CasaDoLago);
            plano.Estradas.Add(e);
        }

        // ------------------------------------------------------------------ os postos, a casa da família e os bunkers

        static void PorPostos(DadosDoMundo d, PlanoDoMapa plano, Nomes nomes, Sorteio s, Quantidades q)
        {
            int total = s.Entre(q.PostosDe, q.PostosAte + 1);
            var postos = new List<Lugar>();
            bool Longe(Vector2 p, float minimo)
            {
                foreach (var o in postos)
                    if ((o.Centro - p).magnitude < minimo) return false;
                return true;
            }
            void Por(Estrada e, int i)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(e.Pontos.Count - 1, i + 1);
                var dir = (e.Pontos[b] - e.Pontos[a]).normalized;
                var lado = new Vector2(-dir.y, dir.x) * (s.Chance(0.5f) ? 1f : -1f);
                var posto = new Lugar
                {
                    Tipo = TipoDeLugar.Posto, Nome = nomes.Posto(s), Raio = 22f, Centro = e.Pontos[i] + lado * 20f,
                    Giro = Mathf.Atan2(-lado.y, -lado.x) * Mathf.Rad2Deg,
                };
                postos.Add(posto);
                plano.Lugares.Add(posto);
            }
            bool Livre(Vector2 p) => plano.CidadeEm(p.x, p.y) == null && (d.Lago == null || d.Lago.Q(p.x, p.y) > 1.5f) && d.Borda(p.x, p.y) < 0.2f;

            // na saída de cada cidade, na primeira rodovia que sai dela
            foreach (var c in plano.Cidades)
            {
                if (postos.Count >= total) break;
                if (c.Tipo == TipoDeLugar.Vila) continue;
                foreach (var e in plano.Estradas)
                {
                    if (e.Tipo != TipoDeEstrada.Rodovia) continue;
                    int achado = -1;
                    for (int i = 0; i < e.Pontos.Count; i++)
                    {
                        float qc = c.Q(e.Pontos[i].x, e.Pontos[i].y);
                        if (qc > 1.12f && qc < 1.35f && Livre(e.Pontos[i])) { achado = i; break; }
                    }
                    if (achado < 0 || !Longe(e.Pontos[achado], 350f)) continue;
                    Por(e, achado);
                    break;
                }
            }
            // os outros, espalhados na beira das rodovias e das estradas, longe uns dos outros
            for (int tentativa = 0; tentativa < 400 && postos.Count < total; tentativa++)
            {
                var e = plano.Estradas[s.Entre(0, plano.Estradas.Count)];
                if (e.Tipo == TipoDeEstrada.Terra || e.Pontos.Count < 10) continue;
                int i = s.Entre(3, e.Pontos.Count - 3);
                if (!Livre(e.Pontos[i]) || !Longe(e.Pontos[i], 650f)) continue;
                Por(e, i);
            }
        }

        // num bairro de casas de uma cidade pequena (ou de uma vila, sem cidade pequena): dois lotes de casa vizinhos, numa
        // rua (não numa avenida), longe dos portões, viram o lote da família (cabe a laje da partida e o carro do lado). A
        // cidade e o bairro são sorteados; dentro do bairro, o par mais perto do meio dele
        static void PorACasaDaFamilia(PlanoDoMapa plano, Sorteio s)
        {
            var candidatas = new List<Cidade>();
            foreach (var c in plano.Cidades) if (c.Tipo == TipoDeLugar.CidadePequena) candidatas.Add(c);
            if (candidatas.Count == 0) foreach (var c in plano.Cidades) if (c.Tipo == TipoDeLugar.Vila) candidatas.Add(c);
            if (candidatas.Count == 0) foreach (var c in plano.Cidades) candidatas.Add(c);
            if (candidatas.Count == 0) return;
            int primeira = s.Entre(0, candidatas.Count);
            for (int ci = 0; ci < candidatas.Count; ci++)
            {
                var cidade = candidatas[(primeira + ci) % candidatas.Count];
                var t = cidade.Tracado;
                if (t == null) continue;
                var bairros = new List<Bairro>();
                foreach (var b in cidade.Bairros) if (b.Tipo == TipoDeBairro.Residencial) bairros.Add(b);
                if (bairros.Count == 0) continue;
                int sorteado = s.Entre(0, bairros.Count);
                for (int bi = 0; bi < bairros.Count; bi++)
                {
                    var bairro = bairros[(sorteado + bi) % bairros.Count];
                    int melhor = -1;
                    float menor = float.MaxValue;
                    for (int k = 0; k + 1 < t.Lotes.Count; k++)
                    {
                        Lote a = t.Lotes[k], b = t.Lotes[k + 1];
                        if (!ParDeCasas(t, a, b)) continue;
                        var meio = t.Mundo((a.Meio + b.Meio) * 0.5f);
                        if (cidade.BairroEm(meio.x, meio.y) != bairro) continue;
                        float dist = Vector2.Distance(meio, bairro.Centro);
                        if (dist < menor)
                        {
                            menor = dist;
                            melhor = k;
                        }
                    }
                    if (melhor < 0) continue;
                    // os dois lotes viram um, com a casa da família no meio (Parte M6: uma térrea sem porão, com a garagem
                    // aberta ou a vaga; o bunker entra depois)
                    Lote l = t.Lotes[melhor], o = t.Lotes[melhor + 1];
                    l.U0 = Mathf.Min(l.U0, o.U0);
                    l.U1 = Mathf.Max(l.U1, o.U1);
                    l.V0 = Mathf.Min(l.V0, o.V0);
                    l.V1 = Mathf.Max(l.V1, o.V1);
                    l.Tipo = TipoDeLote.Familia;
                    // sempre a mesma casa (a do Dia 0, 01/11/2026; antes, sorteada pelo lote)
                    var planta = PlantasDeCasa.DaFamilia(Medidas.DoJogo);
                    var lim = Casa.Limites(planta, Medidas.DoJogo);
                    float frenteDoLote = l.Frente.y != 0f ? l.U1 - l.U0 : l.V1 - l.V0;
                    t.PorCasa(l, planta, 4f, Mathf.Max(0.5f, (frenteDoLote - (lim.y - lim.x)) * 0.5f));
                    t.Lotes.RemoveAt(melhor + 1);
                    var centro = t.Mundo(l.MeioDaCasa);
                    var frente = (t.Mundo(l.Meio + l.Frente) - centro).normalized;
                    var casa = new Lugar
                    {
                        Tipo = TipoDeLugar.CasaDaFamilia, Nome = "Casa da família", Raio = 12f, Centro = centro,
                        Giro = Mathf.Atan2(frente.x, frente.y) * Mathf.Rad2Deg,
                    };
                    plano.Lugares.Add(casa);
                    plano.CasaDaFamilia = casa;
                    plano.CidadeDaFamilia = cidade;
                    plano.BairroDaFamilia = bairro;
                    plano.LoteDaFamilia = l;
                    return;
                }
            }
        }

        // dois lotes de casa seguidos na mesma fileira (encostados, de frente para o mesmo lado), numa rua (não numa
        // avenida) e longe dos portões (lá passa a rodovia)
        static bool ParDeCasas(TracadoDaCidade t, Lote a, Lote b)
        {
            if (a.Tipo != TipoDeLote.Casa || b.Tipo != TipoDeLote.Casa || a.Frente != b.Frente) return false;
            bool aoLongoDeU = a.Frente.y != 0f;
            if (aoLongoDeU ? Mathf.Abs(a.V0 - b.V0) > 0.01f || Mathf.Abs(a.U1 - b.U0) > 0.01f
                           : Mathf.Abs(a.U0 - b.U0) > 0.01f || Mathf.Abs(a.V1 - b.V0) > 0.01f) return false;
            float frente = aoLongoDeU ? b.U1 - a.U0 : b.V1 - a.V0;
            if (frente < 30f) return false;
            // a rua da frente: a linha mais perto da frente do lote
            var meio = (a.Meio + b.Meio) * 0.5f;
            if (aoLongoDeU)
            {
                float v = a.Frente.y < 0f ? a.V0 : a.V1;
                int j = MaisPerto(t.Vs, v);
                if (t.TipoV[j] == TipoDeRua.Avenida) return false;
            }
            else
            {
                float u = a.Frente.x < 0f ? a.U0 : a.U1;
                int i = MaisPerto(t.Us, u);
                if (t.TipoU[i] == TipoDeRua.Avenida) return false;
            }
            var noMapa = t.Mundo(meio);
            foreach (var p in t.Portoes)
                if (Vector2.Distance(noMapa, p.Ponto) < 90f) return false;
            return true;
        }

        static int MaisPerto(float[] linhas, float x)
        {
            int melhor = 0;
            for (int k = 1; k < linhas.Length; k++)
                if (Mathf.Abs(linhas[k] - x) < Mathf.Abs(linhas[melhor] - x)) melhor = k;
            return melhor;
        }

        // os bunkers: nas cabanas de preparador, nas fazendas e no quintal de casas das cidades
        static void PorBunkers(PlanoDoMapa plano, Sorteio s, Quantidades q)
        {
            int total = s.Entre(q.BunkersDe, q.BunkersAte + 1);
            var bunkers = 0;
            void Por(Vector2 p)
            {
                plano.Lugares.Add(new Lugar { Tipo = TipoDeLugar.Bunker, Nome = "Bunker", Raio = 6f, Centro = p });
                bunkers++;
            }
            foreach (var l in plano.Lugares.ToArray())
                if (bunkers < total && l.Tipo == TipoDeLugar.Cabana && l.Preparador) Por(l.Centro + Direcao(l.Giro + 90f) * 8f);
            var fazendas = new List<Lugar>(plano.DoTipo(TipoDeLugar.Fazenda));
            for (int k = 0; k < fazendas.Count && bunkers < total * 0.6f; k += 2) Por(fazendas[k].Centro + Direcao(s.Entre(0f, 360f)) * fazendas[k].Raio * 0.4f);
            var usados = new HashSet<Lote>();
            for (int tentativa = 0; tentativa < 400 && bunkers < total; tentativa++)
            {
                var t = plano.Tracados[s.Entre(0, plano.Tracados.Count)];
                if (t.Lotes.Count == 0) continue;
                var l = t.Lotes[s.Entre(0, t.Lotes.Count)];
                if (l.Tipo != TipoDeLote.Casa || !usados.Add(l)) continue;
                // no quintal: entre o fundo da casa e o fundo do lote
                float fundoDaCasa = l.Frente.y < 0f ? l.BV1 : l.Frente.y > 0f ? l.BV0 : l.Frente.x < 0f ? l.BU1 : l.BU0;
                float fundoDoLote = l.Frente.y < 0f ? l.V1 : l.Frente.y > 0f ? l.V0 : l.Frente.x < 0f ? l.U1 : l.U0;
                float quintal = (fundoDaCasa + fundoDoLote) * 0.5f;
                var local = l.Frente.y != 0f ? new Vector2((l.U0 + l.U1) * 0.5f, quintal) : new Vector2(quintal, (l.V0 + l.V1) * 0.5f);
                var p = t.Mundo(local);
                if (plano.CasaDaFamilia != null && (p - plano.CasaDaFamilia.Centro).magnitude < 60f) continue;
                Por(p);
            }
        }

        // ------------------------------------------------------------------ o metrô

        // a árvore mais curta entre as cidades (grandes e pequenas), partida em linhas: cada linha começa numa ponta e segue
        // pelo caminho mais reto. Uma estação em cada cidade pequena e três nas grandes (no meio e nos dois lados da linha)
        static void TracarOMetro(PlanoDoMapa plano, Sorteio s)
        {
            var cidades = new List<Cidade>();
            foreach (var c in plano.Cidades) if (c.Tipo != TipoDeLugar.Vila) cidades.Add(c);
            if (cidades.Count < 2) return;
            var arestas = ArvoreMaisCurta(cidades);
            var usada = new bool[arestas.Count];
            string[] cores = { "Vermelha", "Azul", "Verde", "Amarela", "Laranja", "Roxa", "Cinza", "Marrom" };
            int linhas = 0;
            while (true)
            {
                // uma ponta: a cidade com só uma aresta livre (ou qualquer uma com aresta livre)
                int inicio = -1;
                for (int c = 0; c < cidades.Count && inicio < 0; c++)
                    if (Livres(arestas, usada, c) == 1) inicio = c;
                for (int c = 0; c < cidades.Count && inicio < 0; c++)
                    if (Livres(arestas, usada, c) > 0) inicio = c;
                if (inicio < 0) break;
                var caminho = new List<int> { inicio };
                int atual = inicio;
                Vector2 vindo = Vector2.zero;
                while (true)
                {
                    int melhor = -1;
                    float maisReto = float.MinValue;
                    for (int k = 0; k < arestas.Count; k++)
                    {
                        if (usada[k]) continue;
                        int outro = arestas[k].Item1 == atual ? arestas[k].Item2 : arestas[k].Item2 == atual ? arestas[k].Item1 : -1;
                        if (outro < 0) continue;
                        var dir = (cidades[outro].Centro - cidades[atual].Centro).normalized;
                        float reto = vindo == Vector2.zero ? 0f : Vector2.Dot(dir, vindo);
                        if (reto > maisReto)
                        {
                            maisReto = reto;
                            melhor = k;
                        }
                    }
                    if (melhor < 0) break;
                    usada[melhor] = true;
                    int proximo = arestas[melhor].Item1 == atual ? arestas[melhor].Item2 : arestas[melhor].Item1;
                    vindo = (cidades[proximo].Centro - cidades[atual].Centro).normalized;
                    atual = proximo;
                    caminho.Add(atual);
                }
                var linha = new LinhaDeMetro { Nome = $"Linha {linhas + 1} – {cores[linhas % cores.Length]}" };
                for (int i = 0; i < caminho.Count; i++)
                {
                    var c = cidades[caminho[i]];
                    var antes = i > 0 ? cidades[caminho[i - 1]].Centro : c.Centro;
                    var depois = i < caminho.Count - 1 ? cidades[caminho[i + 1]].Centro : c.Centro;
                    var dir = (depois - antes).sqrMagnitude > 1f ? (depois - antes).normalized : Vector2.right;
                    if (c.Tipo == TipoDeLugar.CidadeGrande)
                    {
                        Estacao(linha, c, c.Centro - dir * c.Raio * 0.5f);
                        Estacao(linha, c, c.Centro);
                        Estacao(linha, c, c.Centro + dir * c.Raio * 0.5f);
                    }
                    else Estacao(linha, c, c.Centro);
                }
                plano.Metro.Add(linha);
                linhas++;
            }
        }

        static int Livres(List<(int, int)> arestas, bool[] usada, int c)
        {
            int n = 0;
            for (int k = 0; k < arestas.Count; k++)
                if (!usada[k] && (arestas[k].Item1 == c || arestas[k].Item2 == c)) n++;
            return n;
        }

        static void Estacao(LinhaDeMetro linha, Cidade c, Vector2 p)
        {
            linha.Estacoes.Add(p);
            var bairro = c.BairroEm(p.x, p.y);
            linha.NomesDasEstacoes.Add(bairro != null && bairro.Nome != "Centro" ? bairro.Nome : $"{c.Nome} (Centro)");
        }
    }
}
