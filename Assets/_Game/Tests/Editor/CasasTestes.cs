using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// As casas de verdade (Plano_Mapa, Parte M6, primeira fatia): toda casa de bairro tem a planta, cabe no lote, fica
    /// fora das ruas e longe das outras; elas variam; o chão é plano embaixo delas (o patamar); com porão, o terreno tem o
    /// buraco; os carros das casas ficam na garagem ou na vaga; e as malhas saem, com a colisão e os pisos.
    /// </summary>
    public class CasasTestes
    {
        static readonly Dictionary<(TamanhoDoMundo, int), DadosDoMundo> Guardados = new Dictionary<(TamanhoDoMundo, int), DadosDoMundo>();
        static readonly Medidas M = Medidas.DoJogo;

        static DadosDoMundo Mundo(TamanhoDoMundo t, int semente)
        {
            if (!Guardados.TryGetValue((t, semente), out var d)) Guardados[(t, semente)] = d = GeradorDoMundo.Gerar(semente, t);
            return d;
        }

        static IEnumerable<DadosDoMundo> Todos()
        {
            yield return Mundo(TamanhoDoMundo.Pequeno, 12345);
            yield return Mundo(TamanhoDoMundo.Medio, 7);
        }

        static IEnumerable<(TracadoDaCidade t, Lote l)> Casas(DadosDoMundo d)
        {
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                    if (l.Casa != null) yield return (t, l);
        }

        [Test]
        public void AsCasasCabemNosLotes()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                int casas = 0;
                foreach (var t in d.Plano.Tracados)
                {
                    foreach (var l in t.Lotes)
                    {
                        if (l.Tipo == TipoDeLote.Casa || l.Tipo == TipoDeLote.Familia) Assert.IsNotNull(l.Casa, $"{onde}: lote de casa sem a planta");
                        if (l.Casa == null) continue;
                        casas++;
                        var r = l.RetDaCasa;
                        Assert.IsTrue(r.xMin >= l.U0 - 0.01f && r.xMax <= l.U1 + 0.01f && r.yMin >= l.V0 - 0.01f && r.yMax <= l.V1 + 0.01f, $"{onde}: casa fora do lote em {t.Cidade.Nome}");
                        // nenhum canto da casa (com a varanda, a garagem e os degraus) na rua ou na calçada
                        foreach (var c in new[] { new Vector2(r.xMin + 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMax - 0.1f), new Vector2(r.xMin + 0.1f, r.yMax - 0.1f) })
                        {
                            var oque = t.OQueTem(t.Mundo(c));
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Asfalto, oque, $"{onde}: casa na rua em {t.Mundo(c)}");
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Calcada, oque, $"{onde}: casa na calçada em {t.Mundo(c)}");
                        }
                    }
                    // duas casas nunca se encostam (o retângulo de cada uma, com o recuo de 1,2 m dos lados)
                    for (int i = 0; i < t.Lotes.Count; i++)
                    for (int j = i + 1; j < t.Lotes.Count; j++)
                    {
                        Lote a = t.Lotes[i], b = t.Lotes[j];
                        if (a.Casa != null && b.Casa != null && a.Celula == b.Celula) Assert.IsFalse(a.RetDaCasa.Overlaps(b.RetDaCasa), $"{onde}: duas casas se encostam em {t.Cidade.Nome}");
                    }
                }
                Assert.Greater(casas, 300, onde);
            }
        }

        [Test]
        public void AsCasasVariam()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int n = 0, terreas = 0, porao = 0, garagem = 0, comPorta = 0, vaga = 0, espelhadas = 0;
            foreach (var (_, l) in Casas(d))
            {
                var p = l.Casa;
                n++;
                if (p.molde == "Terrea") terreas++;
                if (p.porao) porao++;
                if (p.espelhar) espelhadas++;
                if (p.garagem == null || !p.garagem.coberta) vaga++;
                else if (p.garagem.portaParaDentro) comPorta++;
                else garagem++;
            }
            Assert.That(terreas, Is.InRange(n * 0.3f, n * 0.75f), "térreas e sobrados");
            Assert.That(porao, Is.InRange(n * 0.3f, n * 0.7f), "com porão");
            Assert.That(espelhadas, Is.InRange(n * 0.35f, n * 0.65f), "espelhadas");
            Assert.Greater(garagem, n / 10, "garagem");
            Assert.Greater(comPorta, n / 10, "garagem com porta para dentro");
            Assert.Greater(vaga, n / 5, "vaga");
        }

        [Test]
        public void OChaoFicaPlanoEmbaixoDasCasas()
        {
            foreach (var d in Todos())
                foreach (var (t, l) in Casas(d))
                {
                    var p = l.Casa;
                    float sx = p.espelhar ? -1f : 1f;
                    // o corpo da casa e 0,5 m em volta, a cada 0,5 m (a fundação e as paredes encostam no chão)
                    for (float x = p.X0 - 0.5f; x <= p.X1 + 0.5f; x += 0.5f)
                    for (float z = p.Z0 - 0.5f; z <= p.Z1 + 0.5f; z += 0.5f)
                    {
                        var q = t.Mundo(l.NaCasa(sx * x, z));
                        Assert.AreEqual(l.ChaoDaCasa, d.Altura(q.x, q.y), 0.03f, $"{d.Tamanho}, semente {d.Semente}: o chão fora do patamar em {q}");
                    }
                }
        }

        [Test]
        public void OsPoroesTemBuracoNoChao()
        {
            foreach (var d in Todos())
            {
                int com = 0;
                foreach (var (t, l) in Casas(d))
                {
                    // o meio do corpo da casa: buraco com porão, chão sem
                    var meio = t.Mundo(l.NaCasa(0f, 0f));
                    int i = Mathf.FloorToInt((meio.x + d.MetadeDaGrade) / DadosDoMundo.Celula);
                    int j = Mathf.FloorToInt((meio.y + d.MetadeDaGrade) / DadosDoMundo.Celula);
                    Assert.AreEqual(l.Casa.porao, d.Buraco(i, j), $"{d.Tamanho}, semente {d.Semente}: a casa em {meio} ({(l.Casa.porao ? "com" : "sem")} porão)");
                    if (l.Casa.porao) com++;
                }
                Assert.Greater(com, 0);
            }
        }

        [Test]
        public void AsSaiasDoTerrenoNaoFicamNosPoroes()
        {
            // num bloco com buracos na beira, a malha de detalhe máximo tira as células dos buracos e as saias da beira
            // delas (de pé no meio do porão, a saia era uma parede de terra, e o NavMesh do porão ficava partido)
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int n = DadosDoMundo.CelulasPorBloco, conferidos = 0;
            for (int bz = 0; bz < d.BlocosPorLado && conferidos < 8; bz++)
            for (int bx = 0; bx < d.BlocosPorLado && conferidos < 8; bx++)
            {
                if (d.CidadesDoBloco(bx, bz) == null) continue;
                int i0 = bx * n, j0 = bz * n, buracos = 0, naBeira = 0;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (!d.Buraco(i0 + i, j0 + j)) continue;
                    buracos++;
                    if (i == 0) naBeira++;
                    if (i == n - 1) naBeira++;
                    if (j == 0) naBeira++;
                    if (j == n - 1) naBeira++;
                }
                if (naBeira == 0) continue;
                conferidos++;
                var m = MalhasDoTerreno.Terreno(d, bx, bz, 1);
                Assert.AreEqual((n * n - buracos) * 6 + (4 * n - naBeira) * 12, m.Vertices.Length, $"o bloco {bx}, {bz}");
            }
            Assert.Greater(conferidos, 0);
        }

        [Test]
        public void OsCarrosDasCasasFicamNaGaragemOuNaVaga()
        {
            foreach (var d in Todos())
            {
                int carros = 0;
                foreach (var o in d.Objetos.Todos)
                {
                    if (!o.NaGaragem) continue;
                    carros++;
                    Assert.AreEqual(TipoDeObjeto.Carro, o.Tipo);
                    // (as casas das vilas vão um pouco além das ruas: vale a cidade que manda no chão)
                    var t = d.CidadeNoChao(o.Posicao.x, o.Posicao.y, out _);
                    Assert.IsNotNull(t, $"carro de casa fora das cidades em {o.Posicao}");
                    var local = t.Local(o.Posicao);
                    bool achou = false;
                    foreach (var l in t.Lotes)
                        if (l.Casa != null && l.RetDaCasa.Contains(local)) achou = true;
                    Assert.IsTrue(achou, $"carro de casa fora de uma casa em {o.Posicao}");
                }
                Assert.Greater(carros, 100);
            }
        }

        [Test]
        public void AsMalhasDasCasasSaem()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int blocos = 0, casas = 0;
            for (int bz = 0; bz < d.BlocosPorLado && blocos < 6; bz++)
            for (int bx = 0; bx < d.BlocosPorLado && blocos < 6; bx++)
            {
                if (!MalhasDasCasas.TemCasas(d, bx, bz)) continue;
                var r = MalhasDasCasas.Bloco(d, bx, bz);
                blocos++;
                Assert.IsFalse(r.Vazio);
                foreach (var c in r.Casas)
                {
                    casas++;
                    Assert.Greater(c.Colisao.Triangulos, 200, c.Nome);
                    Assert.That(c.Triangulos, Is.InRange(3000, 16000), c.Nome);
                    Assert.Greater(c.Zonas.Count, 4, c.Nome);
                    Assert.Greater(c.TamanhoDeDentro.y, 3f, c.Nome);
                }
                Assert.Less(r.Desenho.Triangulos / r.Casas.Count, 18000);
            }
            Assert.Greater(casas, 5);
        }
    }
}
