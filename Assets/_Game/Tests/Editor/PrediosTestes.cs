using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// Os prédios de verdade (Plano_Mapa, Parte M6, segunda fatia): todo prédio do centro tem a planta, cabe no lote, fica
    /// fora das ruas e não encosta em outro; eles variam; os cômodos ficam dentro dele, fora do núcleo, sem se cruzar, e
    /// todos têm caminho da rua (pelas portas, pelo corredor e pela escada); a escada cabe no núcleo; o chão não entra no
    /// térreo; e as malhas saem (a casca, as janelas escuras e os andares).
    /// </summary>
    public class PrediosTestes
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
            yield return Mundo(TamanhoDoMundo.Medio, 7);
            yield return Mundo(TamanhoDoMundo.Grande, 12345);
        }

        static IEnumerable<(TracadoDaCidade t, Lote l)> Predios(DadosDoMundo d)
        {
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                    if (l.Predio != null) yield return (t, l);
        }

        [Test]
        public void OsPrediosCabemNosLotes()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                int n = 0;
                foreach (var t in d.Plano.Tracados)
                {
                    foreach (var l in t.Lotes)
                    {
                        if (l.Tipo == TipoDeLote.Predio) Assert.IsNotNull(l.Predio, $"{onde}: lote de prédio sem a planta");
                        if (l.Predio == null) continue;
                        n++;
                        var r = l.RetDaCasa;
                        Assert.IsTrue(r.xMin >= l.U0 - 0.01f && r.xMax <= l.U1 + 0.01f && r.yMin >= l.V0 - 0.01f && r.yMax <= l.V1 + 0.01f, $"{onde}: prédio fora do lote em {t.Cidade.Nome}");
                        foreach (var c in new[] { new Vector2(r.xMin + 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMax - 0.1f), new Vector2(r.xMin + 0.1f, r.yMax - 0.1f) })
                        {
                            var oque = t.OQueTem(t.Mundo(c));
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Asfalto, oque, $"{onde}: prédio na rua em {t.Mundo(c)}");
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Calcada, oque, $"{onde}: prédio na calçada em {t.Mundo(c)}");
                        }
                        Assert.AreEqual(l.Predio.andares, l.Andares);
                        Assert.That(l.Predio.andares, l.Predio.hospital ? Is.InRange(4, 6) : Is.InRange(5, 15));
                    }
                    for (int i = 0; i < t.Lotes.Count; i++)
                    for (int j = i + 1; j < t.Lotes.Count; j++)
                    {
                        Lote a = t.Lotes[i], b = t.Lotes[j];
                        if (a.Predio != null && b.Predio != null) Assert.IsFalse(a.RetDaCasa.Overlaps(b.RetDaCasa), $"{onde}: dois prédios se encostam em {t.Cidade.Nome}");
                    }
                }
                Assert.Greater(n, 80, onde);
            }
        }

        [Test]
        public void OsPrediosVariam()
        {
            int n = 0, lojas = 0, poco = 0, espelhados = 0, estreitos = 0, largos = 0, altos = 0, baixos = 0, nucleoNaBeira = 0;
            foreach (var d in Todos())
                foreach (var (_, l) in Predios(d))
                {
                    var p = l.Predio;
                    n++;
                    if (p.lojasNoTerreo) lojas++;
                    if (p.andarDoPocoAberto >= 0) poco++;
                    if (p.espelhar) espelhados++;
                    if (p.W < 16f) estreitos++;
                    if (p.W > 30f) largos++;
                    if (p.andares >= 12) altos++;
                    if (p.andares <= 7) baixos++;
                    if (p.NucleoX0 <= p.X0 + 0.01f || p.NucleoX1 >= p.X1 - 0.01f) nucleoNaBeira++;
                }
            Assert.That(lojas, Is.InRange(n * 0.4f, n * 0.8f), "o térreo com lojas e com apartamentos");
            Assert.That(poco, Is.InRange(n * 0.3f, n * 0.7f), "o poço do elevador aberto");
            Assert.That(espelhados, Is.InRange(n * 0.35f, n * 0.65f), "espelhados");
            Assert.Greater(estreitos, 0, "estreitos");
            Assert.Greater(largos, 0, "largos");
            Assert.Greater(altos, 0, "altos (12 andares ou mais)");
            Assert.Greater(baixos, 0, "baixos (7 andares ou menos)");
            Assert.Greater(nucleoNaBeira, 0, "o núcleo encostado num lado");
        }

        [Test]
        public void OsComodosTemCaminhoDaRua()
        {
            foreach (var d in Todos())
                foreach (var (t, l) in Predios(d))
                {
                    var p = l.Predio;
                    string onde = $"{d.Tamanho}, semente {d.Semente}, o prédio de {t.Cidade.Nome} ({p.W} × {p.D}, {p.andares} andares)";
                    var nucleo = Rect.MinMaxRect(p.NucleoX0, p.zc1, p.NucleoX1, p.Z1);
                    foreach (var lista in new[] { p.terreo, p.tipo })
                    {
                        var ligs = lista == p.terreo ? p.ligacoesDoTerreo : p.ligacoesDoTipo;
                        // os cômodos: dentro do prédio, fora do núcleo (menos a lavanderia, atrás do poço), sem se cruzar e de um tamanho que se anda
                        foreach (var k in lista)
                        {
                            Assert.IsTrue(k.x0 >= p.X0 - 0.01f && k.x1 <= p.X1 + 0.01f && k.z0 >= p.Z0 - 0.01f && k.z1 <= p.Z1 + 0.01f, $"{onde}: {k.tipo} fora do prédio");
                            Assert.GreaterOrEqual(k.x1 - k.x0, 1.8f, $"{onde}: {k.tipo} estreito");
                            Assert.GreaterOrEqual(k.z1 - k.z0, 1.8f, $"{onde}: {k.tipo} estreito");
                            if (k.tipo != "lavanderia" && k.tipo != "rouparia")
                                Assert.IsFalse(Rect.MinMaxRect(k.x0 + 0.01f, k.z0 + 0.01f, k.x1 - 0.01f, k.z1 - 0.01f).Overlaps(nucleo), $"{onde}: {k.tipo} no núcleo");
                        }
                        for (int i = 0; i < lista.Count; i++)
                        for (int j = i + 1; j < lista.Count; j++)
                        {
                            Comodo a = lista[i], b = lista[j];
                            bool cruzam = a.x0 < b.x1 - 0.01f && b.x0 < a.x1 - 0.01f && a.z0 < b.z1 - 0.01f && b.z0 < a.z1 - 0.01f;
                            Assert.IsFalse(cruzam, $"{onde}: {a.tipo} e {b.tipo} se cruzam");
                        }
                        // o caminho: da rua pelas portas de fora (no térreo), pelas ligações e, de qualquer andar, pela escada (o corredor)
                        var chegou = new HashSet<Comodo>();
                        var fila = new Queue<Comodo>();
                        foreach (var lg in ligs)
                            if (lg.b == null && chegou.Add(lg.a)) fila.Enqueue(lg.a);
                        foreach (var k in lista)
                            if (k.tipo == "corredor" && chegou.Add(k)) fila.Enqueue(k);
                        while (fila.Count > 0)
                        {
                            var k = fila.Dequeue();
                            foreach (var lg in ligs)
                            {
                                if (lg.b == null) continue;
                                var outro = lg.a == k ? lg.b : lg.b == k ? lg.a : null;
                                if (outro != null && chegou.Add(outro)) fila.Enqueue(outro);
                            }
                        }
                        foreach (var k in lista) Assert.IsTrue(chegou.Contains(k), $"{onde}: sem caminho até o {k.tipo} ({(lista == p.terreo ? "térreo" : "andar tipo")})");
                    }
                    Assert.IsTrue(p.terreo.Exists(k => k.tipo == (p.hospital ? "recepcao" : "portaria")), $"{onde}: sem a entrada");
                }
        }

        [Test]
        public void AEscadaCabeNoNucleo()
        {
            foreach (var d in Todos())
                foreach (var (_, l) in Predios(d))
                {
                    var p = l.Predio;
                    // o lance mais comprido (o do térreo, mais alto) e o patamar do meio de pelo menos 1,2 m
                    float meio = p.AlturaDoTerreo / 2;
                    float comp = (Obra.Espelhos(meio) - 1) * M.pisada;
                    float zL = p.zc1 + 0.1f + PlantaDePredio.Patamar;
                    Assert.LessOrEqual(zL + comp + 1.2f, p.Z1 - 0.1f, $"a escada do prédio de {p.D} m de fundo não cabe");
                    // os degraus: de 15 a 19 cm
                    Assert.That(meio / Obra.Espelhos(meio), Is.InRange(0.15f, 0.19f));
                    Assert.That(p.alturaDoAndar / 2 / Obra.Espelhos(p.alturaDoAndar / 2), Is.InRange(0.15f, 0.19f));
                    // o lance tem 1,6 m e a cabeça passa: entre um lance e o de cima, a altura de um andar
                    Assert.GreaterOrEqual(p.alturaDoAndar - 0.35f, 2.6f);
                }
        }

        [Test]
        public void OChaoNaoEntraNosPredios()
        {
            foreach (var d in Todos())
                foreach (var (t, l) in Predios(d))
                {
                    var p = l.Predio;
                    // o piso do térreo fica acima do chão em todo o prédio (a cada 0,5 m)
                    for (float x = p.X0; x <= p.X1 + 0.01f; x += 0.5f)
                    for (float z = p.Z0; z <= p.Z1 + 0.01f; z += 0.5f)
                    {
                        var q = t.Mundo(l.NaCasa(x, z));
                        Assert.LessOrEqual(d.Altura(q.x, q.y), l.ChaoDaCasa + PlantaDePredio.Piso - 0.05f, $"{d.Tamanho}, semente {d.Semente}: o chão entra no prédio em {q}");
                    }
                }
        }

        [Test]
        public void AsMalhasDosPrediosSaem()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int blocos = 0, predios = 0;
            for (int bz = 0; bz < d.BlocosPorLado && predios < 6; bz++)
            for (int bx = 0; bx < d.BlocosPorLado && predios < 6; bx++)
            {
                if (!MalhasDasCasas.TemCasas(d, bx, bz)) continue;
                var r = MalhasDasCasas.Bloco(d, bx, bz);
                if (r.Predios.Count == 0) continue;
                blocos++;
                Assert.Greater(r.Desenho.Triangulos, 0);
                foreach (var pr in r.Predios)
                {
                    predios++;
                    var p = pr.Planta;
                    Assert.Greater(pr.Colisao.Triangulos, 0);
                    Assert.Less(pr.Triangulos, 40000, "a casca pesada demais");
                    Assert.AreEqual(p.andares, pr.Escuros.Length);
                    int escuros = 0;
                    foreach (var e in pr.Escuros) escuros += e.Triangulos;
                    Assert.Greater(escuros, 0, "sem janelas escuras");
                    Assert.AreEqual(p.terreo.Count, pr.Zonas.Count, "os pisos do térreo");
                    Assert.Greater(pr.TamanhoDeDentro.y, p.alturaDoAndar * (p.andares - 1));
                    foreach (int f in new[] { 0, 1, p.andares - 1 })
                    {
                        var a = MalhasDasCasas.MontarAndar(p, f);
                        Assert.AreEqual(f, a.Andar);
                        Assert.Greater(a.Desenho.Triangulos, 0);
                        Assert.Less(a.Desenho.Triangulos, 20000, $"o andar {f} pesado demais");
                        Assert.Greater(a.Colisao.Triangulos, 0);
                        Assert.AreEqual(p.ComodosDo(f).Count, a.Zonas.Count, "os pisos do andar");
                        MalhasDasCasas.Devolver(a);
                    }
                }
                MalhasDasCasas.Devolver(r);
            }
            Assert.Greater(predios, 0);
        }
    }
}
