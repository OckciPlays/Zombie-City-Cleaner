using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O desenho do mapa (Plano_Mapa, Parte M3): as quantidades da tabela dos tamanhos, nada encostando, tudo dentro do mapa
    /// e fora do lago, as cidades ligadas por rodovias e cada lugar com a sua estrada, os nomes sem repetir, a casa da
    /// família num bairro de casas, o laboratório na cidade grande, os postos na beira da estrada, as barreiras na borda, o
    /// metrô em todas as cidades e a pintura. Os mundos são gerados uma vez só (<see cref="Mundo"/>).
    /// </summary>
    public class PlanoDoMapaTestes
    {
        static readonly Dictionary<(TamanhoDoMundo, int), DadosDoMundo> Guardados = new Dictionary<(TamanhoDoMundo, int), DadosDoMundo>();

        static DadosDoMundo Mundo(TamanhoDoMundo t, int semente)
        {
            if (!Guardados.TryGetValue((t, semente), out var d)) Guardados[(t, semente)] = d = GeradorDoMundo.Gerar(semente, t);
            return d;
        }

        // as sementes de cada teste: duas por tamanho (o pequeno é o mais apertado: mais uma)
        static IEnumerable<DadosDoMundo> Todos()
        {
            foreach (var t in Tamanhos.Todos)
            {
                yield return Mundo(t, 12345);
                yield return Mundo(t, 7);
            }
            yield return Mundo(TamanhoDoMundo.Pequeno, 3);
        }

        [OneTimeTearDown]
        public void Limpar() => Guardados.Clear();

        static bool Pequeno(Lugar l) => l.Tipo == TipoDeLugar.Cabana || l.Tipo == TipoDeLugar.Posto || l.Tipo == TipoDeLugar.Bunker
                                        || l.Tipo == TipoDeLugar.Barreira || l.Tipo == TipoDeLugar.CasaDaFamilia || l.Tipo == TipoDeLugar.Laboratorio
                                        || l.Tipo == TipoDeLugar.CasaDoLago;

        [Test]
        public void AsQuantidadesSeguemATabela()
        {
            foreach (var d in Todos())
            {
                var p = d.Plano;
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                int grandes = d.Tamanho == TamanhoDoMundo.Pequeno ? 1 : d.Tamanho == TamanhoDoMundo.Medio ? 2 : 3;
                Assert.AreEqual(grandes, p.Quantos(TipoDeLugar.CidadeGrande), onde);
                var (pequenas, vilas, fazendas, cabanas, postos, bunkers) = d.Tamanho switch
                {
                    TamanhoDoMundo.Pequeno => ((2, 2), (2, 3), (5, 8), (8, 12), (4, 6), (5, 8)),
                    TamanhoDoMundo.Medio => ((3, 4), (4, 6), (10, 15), (15, 25), (8, 12), (10, 15)),
                    _ => ((6, 8), (8, 12), (20, 30), (30, 50), (15, 20), (20, 30)),
                };
                // o que não coube some (o mapa pequeno é apertado): até 1 a menos nas vilas, 2 nas fazendas e 3 nas cabanas
                Assert.That(p.Quantos(TipoDeLugar.CidadePequena), Is.InRange(pequenas.Item1, pequenas.Item2), onde);
                Assert.That(p.Quantos(TipoDeLugar.Vila), Is.InRange(vilas.Item1 - 1, vilas.Item2), onde);
                Assert.That(p.Quantos(TipoDeLugar.Fazenda), Is.InRange(fazendas.Item1 - 2, fazendas.Item2), onde);
                Assert.That(p.Quantos(TipoDeLugar.Cabana), Is.InRange(cabanas.Item1 - 3, cabanas.Item2), onde);
                Assert.That(p.Quantos(TipoDeLugar.Posto), Is.InRange(postos.Item1 - 1, postos.Item2), onde);
                Assert.That(p.Quantos(TipoDeLugar.Bunker), Is.InRange(bunkers.Item1, bunkers.Item2), onde);
                Assert.AreEqual(grandes, p.Quantos(TipoDeLugar.BaseMilitar), onde);
                int comunidades = d.Tamanho == TamanhoDoMundo.Pequeno ? 2 : d.Tamanho == TamanhoDoMundo.Medio ? 3 : 5;
                Assert.AreEqual(comunidades, p.Quantos(TipoDeLugar.ComunidadeAliada), onde);
                Assert.AreEqual(comunidades, p.Quantos(TipoDeLugar.ComunidadeInimiga), onde);
                Assert.AreEqual(1, p.Quantos(TipoDeLugar.Laboratorio), onde);
                Assert.AreEqual(1, p.Quantos(TipoDeLugar.CasaDoLago), onde);
                Assert.AreEqual(1, p.Quantos(TipoDeLugar.CasaDaFamilia), onde);
                Assert.Greater(p.Quantos(TipoDeLugar.Barreira), 1, onde);
            }
        }

        [Test]
        public void NadaSeEncostaETudoCabe()
        {
            foreach (var d in Todos())
            {
                var lugares = d.Plano.Lugares;
                for (int a = 0; a < lugares.Count; a++)
                {
                    var la = lugares[a];
                    string onde = $"{d.Tamanho}, semente {d.Semente}: {la.Tipo} {la.Nome}";
                    Assert.IsTrue(d.Dentro(la.Centro.x, la.Centro.y), onde);
                    if (la.Tipo != TipoDeLugar.CasaDoLago) Assert.Greater(d.Lago.Q(la.Centro.x, la.Centro.y), 1.25f, onde);
                    if (la.EhCidade) Assert.Less(d.Borda(la.Centro.x, la.Centro.y), 0.05f, onde);
                    if (Pequeno(la)) continue;
                    for (int b = a + 1; b < lugares.Count; b++)
                    {
                        var lb = lugares[b];
                        if (Pequeno(lb)) continue;
                        Assert.GreaterOrEqual(Vector2.Distance(la.Centro, lb.Centro), la.Raio + lb.Raio, $"{onde} e {lb.Tipo} {lb.Nome}");
                    }
                }
            }
        }

        [Test]
        public void AsCidadesSeLigamETodoLugarTemEstrada()
        {
            foreach (var d in Todos())
            {
                var p = d.Plano;
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                // as rodovias juntam todas as cidades (grandes e pequenas) num grupo só
                var grupo = new Dictionary<int, int>();
                int Raiz(int x)
                {
                    while (grupo.TryGetValue(x, out int pai) && pai != x) x = pai;
                    return x;
                }
                foreach (var e in p.Estradas)
                {
                    if (e.Tipo != TipoDeEstrada.Rodovia || e.De < 0 || e.Para < 0) continue;
                    if (!p.Lugares[e.Para].EhCidade) continue;
                    grupo[Raiz(e.De)] = Raiz(e.Para);
                }
                int primeira = -1;
                foreach (var c in p.Cidades)
                {
                    if (c.Tipo == TipoDeLugar.Vila) continue;
                    int raiz = Raiz(p.Indice(c));
                    if (primeira < 0) primeira = raiz;
                    Assert.AreEqual(primeira, raiz, $"{onde}: {c.Nome} fora da rede de rodovias");
                }
                // cada vila, base, comunidade, fazenda e cabana tem a estrada dela (ou fica em cima de uma)
                foreach (var l in p.Lugares)
                {
                    bool precisa = l.Tipo == TipoDeLugar.Vila || l.Tipo == TipoDeLugar.BaseMilitar || l.Tipo == TipoDeLugar.ComunidadeAliada
                                   || l.Tipo == TipoDeLugar.ComunidadeInimiga || l.Tipo == TipoDeLugar.Fazenda || l.Tipo == TipoDeLugar.Cabana;
                    if (!precisa) continue;
                    int indice = p.Indice(l);
                    bool tem = false;
                    foreach (var e in p.Estradas) tem |= e.De == indice;
                    if (!tem)
                    {
                        // (ou do lado de uma: um acesso de menos de 20 m não vira estrada)
                        CaminhosDoMapa.MaisPerto(p, l.Centro, out float distancia);
                        tem = distancia < l.Raio + 20f;
                    }
                    Assert.IsTrue(tem, $"{onde}: {l.Tipo} {l.Nome} sem estrada");
                }
                // as estradas não entram no lago
                foreach (var e in p.Estradas)
                    foreach (var pt in e.Pontos)
                        Assert.Greater(d.Lago.Q(pt.x, pt.y), 1.05f, $"{onde}: estrada {e.Tipo} no lago");
            }
        }

        [Test]
        public void OsNomesNaoRepetem()
        {
            foreach (var d in Todos())
            {
                var nomes = new HashSet<string>();
                foreach (var l in d.Plano.Lugares)
                {
                    if (l.Tipo == TipoDeLugar.Bunker || l.Tipo == TipoDeLugar.Barreira || l.Tipo == TipoDeLugar.Cabana) continue;
                    Assert.IsFalse(string.IsNullOrEmpty(l.Nome), $"{l.Tipo} sem nome");
                    Assert.IsTrue(nomes.Add(l.Nome), $"{d.Tamanho}, semente {d.Semente}: {l.Nome} repetido");
                }
                // nenhuma cidade começa com o nome inteiro de outra ("São Pedro" e "São Pedro da Serra")
                foreach (var a in d.Plano.Cidades)
                    foreach (var b in d.Plano.Cidades)
                        if (a != b) Assert.IsFalse(b.Nome.StartsWith(a.Nome + " "), $"{a.Nome} e {b.Nome}");
                foreach (var c in d.Plano.Cidades)
                {
                    var bairros = new HashSet<string>();
                    foreach (var b in c.Bairros) Assert.IsTrue(bairros.Add(b.Nome), $"{c.Nome}: o bairro {b.Nome} repetido");
                }
            }
        }

        [Test]
        public void ACasaDaFamiliaFicaNumBairroDeCasas()
        {
            foreach (var d in Todos())
            {
                var p = d.Plano;
                Assert.IsNotNull(p.CasaDaFamilia);
                var c = p.CidadeDaFamilia;
                Assert.IsNotNull(c);
                Assert.Less(c.Q(p.CasaDaFamilia.Centro.x, p.CasaDaFamilia.Centro.y), 1f);
                Assert.AreEqual(TipoDeBairro.Residencial, c.ZonaEm(p.CasaDaFamilia.Centro.x, p.CasaDaFamilia.Centro.y));
                Assert.AreEqual(p.BairroDaFamilia, c.BairroEm(p.CasaDaFamilia.Centro.x, p.CasaDaFamilia.Centro.y));
                Assert.AreEqual(p.CasaDaFamilia.Centro.x, d.Partida.x, 0.01f);
                Assert.AreEqual(p.CasaDaFamilia.Centro.y, d.Partida.z, 0.01f);
                if (d.Tamanho != TamanhoDoMundo.Pequeno || p.Quantos(TipoDeLugar.CidadePequena) > 0)
                    Assert.AreEqual(TipoDeLugar.CidadePequena, c.Tipo);
            }
        }

        [Test]
        public void OLaboratorioOsPostosAsBarreirasEOMetro()
        {
            foreach (var d in Todos())
            {
                var p = d.Plano;
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                var lab = p.Laboratorio;
                var cidade = p.CidadeEm(lab.Centro.x, lab.Centro.y);
                Assert.IsNotNull(cidade, onde);
                Assert.AreEqual(TipoDeLugar.CidadeGrande, cidade.Tipo, onde);
                foreach (var posto in p.DoTipo(TipoDeLugar.Posto))
                {
                    CaminhosDoMapa.MaisPerto(p, posto.Centro, out float distancia);
                    Assert.Less(distancia, 30f, $"{onde}: {posto.Nome} longe da estrada");
                }
                // a barreira fica na cerca da quarentena (desde a M4)
                foreach (var b in p.DoTipo(TipoDeLugar.Barreira))
                    Assert.Less(Mathf.Abs(Mathf.Max(Mathf.Abs(b.Centro.x), Mathf.Abs(b.Centro.y)) - d.Cerca), 30f, onde);
                foreach (var c in p.Cidades)
                {
                    if (c.Tipo == TipoDeLugar.Vila) continue;
                    bool temEstacao = false;
                    foreach (var linha in p.Metro)
                        foreach (var e in linha.Estacoes) temEstacao |= c.Q(e.x, e.y) <= 1f;
                    Assert.IsTrue(temEstacao, $"{onde}: {c.Nome} sem metrô");
                }
                Assert.Greater(p.Metro.Count, 0, onde);
            }
        }

        [Test]
        public void AMesmaSementeDaOMesmoDesenho()
        {
            var a = Mundo(TamanhoDoMundo.Medio, 7);
            var b = GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);
            Assert.AreEqual(a.Resumo, b.Resumo);
            Assert.AreEqual(a.Plano.Lugares.Count, b.Plano.Lugares.Count);
            for (int i = 0; i < a.Plano.Lugares.Count; i++)
            {
                Assert.AreEqual(a.Plano.Lugares[i].Nome, b.Plano.Lugares[i].Nome);
                Assert.AreEqual(a.Plano.Lugares[i].Centro, b.Plano.Lugares[i].Centro);
            }
            Assert.AreEqual(a.Plano.Estradas.Count, b.Plano.Estradas.Count);
            for (int i = 0; i < a.Plano.Estradas.Count; i++) Assert.AreEqual(a.Plano.Estradas[i].Pontos.Count, b.Plano.Estradas[i].Pontos.Count);
        }

        [Test]
        public void OMapaPintado()
        {
            var d = Mundo(TamanhoDoMundo.Pequeno, 12345);
            const int lado = 256;
            var px = PintorDoMapa.Pintar(d, lado);
            Assert.AreEqual(lado * lado, px.Length);
            Color32 Em(Vector2 p) => px[Mathf.FloorToInt((p.y + d.Metade) / d.Lado * lado) * lado + Mathf.FloorToInt((p.x + d.Metade) / d.Lado * lado)];
            // a água do lago é azulada (fora da ilha), e o centro da cidade grande é cinza
            var lago = Em(d.Lago.Centro + Vector2.right * d.Lago.Raio * 0.6f);
            Assert.Greater(lago.b, lago.r);
            // (oito pontos em volta do meio: a rodovia passa pelo meio da cidade, e uns caem nela)
            foreach (var c in d.Plano.Cidades)
            {
                if (c.Tipo != TipoDeLugar.CidadeGrande) continue;
                int cinzas = 0;
                for (int a = 0; a < 8; a++)
                {
                    var cor = Em(c.Centro + new Vector2(Mathf.Cos(a * 0.785f), Mathf.Sin(a * 0.785f)) * c.Raio * 0.15f);
                    if (Mathf.Abs(cor.r - cor.b) < 30) cinzas++;
                }
                Assert.GreaterOrEqual(cinzas, 5);
            }
            Assert.Greater(PintorDoMapa.Rotulos(d).Count, d.Plano.Cidades.Count);
        }
    }
}
