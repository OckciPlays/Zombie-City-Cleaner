using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// As lojas de verdade e o hospital (Plano_Mapa, Parte M6, terceira fatia): toda loja do comércio tem a planta, cabe no
    /// lote, fica fora das ruas e não encosta em outra; as cidades têm as lojas que precisam (e a grande, o hospital); os
    /// cômodos ficam dentro dela, sem se cruzar, e todos têm caminho da rua (pelas portas e pela escada); a escada cabe nos
    /// fundos; os balcões deixam passar até todas as portas do salão; o chão não entra nelas; e as malhas saem.
    /// </summary>
    public class LojasTestes
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

        static IEnumerable<(TracadoDaCidade t, Lote l)> Lojas(DadosDoMundo d)
        {
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                    if (l.Loja != null) yield return (t, l);
        }

        [Test]
        public void AsLojasCabemNosLotes()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                int n = 0;
                foreach (var t in d.Plano.Tracados)
                {
                    foreach (var l in t.Lotes)
                    {
                        if (l.Tipo == TipoDeLote.Loja) Assert.IsNotNull(l.Loja, $"{onde}: lote de loja sem a planta");
                        if (l.Loja == null) continue;
                        n++;
                        var r = l.RetDaCasa;
                        Assert.IsTrue(r.xMin >= l.U0 - 0.01f && r.xMax <= l.U1 + 0.01f && r.yMin >= l.V0 - 0.01f && r.yMax <= l.V1 + 0.01f, $"{onde}: loja fora do lote em {t.Cidade.Nome}");
                        foreach (var c in new[] { new Vector2(r.xMin + 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMin + 0.1f), new Vector2(r.xMax - 0.1f, r.yMax - 0.1f), new Vector2(r.xMin + 0.1f, r.yMax - 0.1f) })
                        {
                            var oque = t.OQueTem(t.Mundo(c));
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Asfalto, oque, $"{onde}: loja na rua em {t.Mundo(c)}");
                            Assert.AreNotEqual(TracadoDaCidade.NoMapa.Calcada, oque, $"{onde}: loja na calçada em {t.Mundo(c)}");
                        }
                        Assert.AreEqual(l.Loja.andares, l.Andares);
                        Assert.That(l.Loja.andares, l.Loja.tipo == TipoDeLoja.Mercado ? Is.EqualTo(1) : Is.InRange(1, 4));
                    }
                    for (int i = 0; i < t.Lotes.Count; i++)
                    for (int j = i + 1; j < t.Lotes.Count; j++)
                    {
                        Lote a = t.Lotes[i], b = t.Lotes[j];
                        bool ca = a.Loja != null || a.Predio != null || a.Casa != null, cb = b.Loja != null || b.Predio != null || b.Casa != null;
                        if ((a.Loja != null || b.Loja != null) && ca && cb)
                            Assert.IsFalse(a.RetDaCasa.Overlaps(b.RetDaCasa), $"{onde}: uma loja encosta em outra construção em {t.Cidade.Nome}");
                    }
                }
                Assert.Greater(n, 150, onde);
            }
        }

        [Test]
        public void AsCidadesTemAsLojas()
        {
            foreach (var d in Todos())
                foreach (var t in d.Plano.Tracados)
                {
                    if (t.EhVila) continue;
                    var n = new Dictionary<TipoDeLoja, int>();
                    int hospitais = 0;
                    foreach (var l in t.Lotes)
                    {
                        if (l.Predio != null && l.Predio.hospital) hospitais++;
                        if (l.Loja != null) n[l.Loja.tipo] = n.TryGetValue(l.Loja.tipo, out int c) ? c + 1 : 1;
                    }
                    int Q(TipoDeLoja tipo) => n.TryGetValue(tipo, out int c) ? c : 0;
                    string onde = $"{d.Tamanho}, semente {d.Semente}, {t.Cidade.Nome}";
                    if (t.Cidade.Tipo == TipoDeLugar.CidadeGrande)
                    {
                        Assert.AreEqual(1, hospitais, $"{onde}: o hospital");
                        Assert.AreEqual(1, Q(TipoDeLoja.Armas), $"{onde}: a loja de armas");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.Mercado), 2, $"{onde}: os mercados");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.Farmacia), 3, $"{onde}: as farmácias");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.VinteQuatroHoras), 3, $"{onde}: as lojas 24 horas");
                    }
                    else
                    {
                        Assert.AreEqual(0, hospitais, $"{onde}: hospital numa cidade pequena");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.Mercado), 1, $"{onde}: o mercado");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.Farmacia), 1, $"{onde}: a farmácia");
                        Assert.GreaterOrEqual(Q(TipoDeLoja.VinteQuatroHoras), 1, $"{onde}: a loja 24 horas");
                    }
                }
        }

        [Test]
        public void OsComodosTemCaminhoDaRua()
        {
            foreach (var d in Todos())
                foreach (var (t, l) in Lojas(d))
                {
                    var p = l.Loja;
                    string onde = $"{d.Tamanho}, semente {d.Semente}, {PlantaDeLoja.Nome(p.tipo)} de {t.Cidade.Nome} ({p.W} × {p.D}, {p.andares} andares)";
                    for (int f = 0; f < p.andares; f++)
                    {
                        var lista = p.ComodosDo(f);
                        Assert.Greater(lista.Count, 0, $"{onde}: o andar {f} sem cômodos");
                        foreach (var k in lista)
                        {
                            Assert.IsTrue(k.x0 >= p.X0 - 0.01f && k.x1 <= p.X1 + 0.01f && k.z0 >= p.Z0 - 0.01f && k.z1 <= p.Z1 + 0.01f, $"{onde}: {k.tipo} fora da loja");
                            Assert.GreaterOrEqual(k.x1 - k.x0, 1.8f, $"{onde}: {k.tipo} estreito");
                            Assert.GreaterOrEqual(k.z1 - k.z0, 1.8f, $"{onde}: {k.tipo} estreito");
                        }
                        for (int i = 0; i < lista.Count; i++)
                        for (int j = i + 1; j < lista.Count; j++)
                        {
                            Comodo a = lista[i], b = lista[j];
                            bool cruzam = a.x0 < b.x1 - 0.01f && b.x0 < a.x1 - 0.01f && a.z0 < b.z1 - 0.01f && b.z0 < a.z1 - 0.01f;
                            Assert.IsFalse(cruzam, $"{onde}: {a.tipo} e {b.tipo} se cruzam no andar {f}");
                        }
                    }
                    // o caminho: da rua pelas portas da frente, pelas ligações e pela escada (a de um andar liga na do outro)
                    var chegou = new HashSet<Comodo>();
                    var fila = new Queue<Comodo>();
                    foreach (var lg in p.ligacoes)
                        if (lg.b == null && lg.lado == 0 && chegou.Add(lg.a)) fila.Enqueue(lg.a);
                    Assert.Greater(fila.Count, 0, $"{onde}: sem porta da frente");
                    while (fila.Count > 0)
                    {
                        var k = fila.Dequeue();
                        foreach (var lg in p.ligacoes)
                        {
                            if (lg.b == null) continue;
                            var outro = lg.a == k ? lg.b : lg.b == k ? lg.a : null;
                            if (outro != null && chegou.Add(outro)) fila.Enqueue(outro);
                        }
                        if (k.tipo == "escada")
                            foreach (var e in p.comodos)
                                if (e.tipo == "escada" && Mathf.Abs(e.andar - k.andar) == 1 && chegou.Add(e)) fila.Enqueue(e);
                    }
                    foreach (var k in p.comodos) Assert.IsTrue(chegou.Contains(k), $"{onde}: sem caminho até o {k.tipo} (andar {k.andar})");
                    if (p.tipo == TipoDeLoja.Armas) Assert.IsTrue(p.ligacoes.Exists(x => x.deAco), $"{onde}: sem a porta de aço");
                    if (p.tipo == TipoDeLoja.Mercado) Assert.IsTrue(p.ligacoes.Exists(x => x.b == null && x.lado == 2 && x.largura >= 3f), $"{onde}: sem a doca");
                }
        }

        [Test]
        public void AEscadaCabeNosFundos()
        {
            int com = 0;
            foreach (var d in Todos())
                foreach (var (_, l) in Lojas(d))
                {
                    var p = l.Loja;
                    if (!p.TemEscada) continue;
                    com++;
                    // o lance mais comprido (o do térreo, mais alto) e o patamar do meio de pelo menos 1,2 m
                    float meio = (p.Base(1) - p.Base(0)) / 2;
                    float comp = (Obra.Espelhos(meio) - 1) * M.pisada;
                    float zL = p.zs + 0.1f + PlantaDeLoja.Patamar;
                    Assert.LessOrEqual(zL + comp + 1.2f, p.Z1 - 0.1f, $"a escada da loja de {p.D} m de fundo (os fundos com {p.Z1 - p.zs:F1} m) não cabe");
                    Assert.That(meio / Obra.Espelhos(meio), Is.InRange(0.15f, 0.19f));
                    float andar = (p.peDireito + p.laje) / 2;
                    Assert.That(andar / Obra.Espelhos(andar), Is.InRange(0.15f, 0.19f));
                    Assert.AreEqual(PlantaDeLoja.LarguraDaEscada, p.ex1 - p.ex0, 0.01f);
                    Assert.IsTrue(p.ex0 <= p.X0 + 0.01f || p.ex1 >= p.X1 - 0.01f, "a escada encostada num lado");
                }
            Assert.Greater(com, 20, "lojas com apartamento em cima");
        }

        [Test]
        public void OsBalcoesDeixamPassar()
        {
            // no salão (com a cápsula de 0,4 m de raio, longe das paredes e dos balcões), da porta da frente até todas as portas dele
            const float passo = 0.2f, raio = 0.4f;
            int conferidas = 0;
            foreach (var d in Todos())
                foreach (var (t, l) in Lojas(d))
                {
                    var p = l.Loja;
                    if (p.balcoes.Count == 0) continue;
                    conferidas++;
                    var s = p.Salao;
                    float a0 = s.x0 + M.esp / 2 + raio, a1 = s.x1 - M.esp / 2 - raio, b0 = s.z0 + M.esp / 2 + raio, b1 = s.z1 - M.esp / 2 - raio;
                    int nx = Mathf.FloorToInt((a1 - a0) / passo) + 1, nz = Mathf.FloorToInt((b1 - b0) / passo) + 1;
                    bool Livre(int i, int j)
                    {
                        float x = a0 + i * passo, z = b0 + j * passo;
                        foreach (var r in p.balcoes)
                            if (x > r.xMin - raio && x < r.xMax + raio && z > r.yMin - raio && z < r.yMax + raio) return false;
                        return true;
                    }
                    (int, int) Celula(float x, float z) => (Mathf.Clamp(Mathf.RoundToInt((x - a0) / passo), 0, nx - 1), Mathf.Clamp(Mathf.RoundToInt((z - b0) / passo), 0, nz - 1));
                    var visto = new bool[nx, nz];
                    var fila = new Queue<(int, int)>();
                    var porta = p.ligacoes.Find(x => x.b == null && x.lado == 0 && x.a == s);
                    var (ci, cj) = Celula(porta.pos, b0);
                    string onde = $"{d.Tamanho}, semente {d.Semente}, {PlantaDeLoja.Nome(p.tipo)} de {t.Cidade.Nome}";
                    Assert.IsTrue(Livre(ci, cj), $"{onde}: um balcão na porta da frente");
                    visto[ci, cj] = true;
                    fila.Enqueue((ci, cj));
                    while (fila.Count > 0)
                    {
                        var (i, j) = fila.Dequeue();
                        foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int a = i + di, b = j + dj;
                            if (a < 0 || b < 0 || a >= nx || b >= nz || visto[a, b] || !Livre(a, b)) continue;
                            visto[a, b] = true;
                            fila.Enqueue((a, b));
                        }
                    }
                    // as portas do salão para os fundos e para a escada (na parede de zs) e as outras portas da frente
                    foreach (var lg in p.ligacoes)
                    {
                        if (lg.a != s && lg.b != s) continue;
                        float z = lg.b == null ? b0 : b1;
                        var (pi, pj) = Celula(lg.pos, z);
                        Assert.IsTrue(visto[pi, pj], $"{onde}: o balcão fecha a passagem até a porta em x = {lg.pos:F1} ({(lg.b == null ? "a da frente" : (lg.a == s ? lg.b : lg.a).tipo)})");
                    }
                }
            Assert.Greater(conferidas, 10);
        }

        [Test]
        public void OChaoNaoEntraNasLojas()
        {
            foreach (var d in Todos())
                foreach (var (t, l) in Lojas(d))
                {
                    var p = l.Loja;
                    for (float x = p.X0; x <= p.X1 + 0.01f; x += 0.5f)
                    for (float z = p.Z0; z <= p.Z1 + 0.01f; z += 0.5f)
                    {
                        var q = t.Mundo(l.NaCasa(x, z));
                        Assert.LessOrEqual(d.Altura(q.x, q.y), l.ChaoDaCasa + PlantaDeLoja.Piso - 0.05f, $"{d.Tamanho}, semente {d.Semente}: o chão entra na loja em {q}");
                    }
                }
        }

        [Test]
        public void AsMalhasDasLojasSaem()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            var tipos = new HashSet<TipoDeLoja>();
            int lojas = 0;
            foreach (var (t, l) in Lojas(d))
            {
                var p = l.Loja;
                if (!tipos.Add(p.tipo) && lojas > 12) continue;
                lojas++;
                var desenho = MalhaDeObra.Emprestar();
                var r = MalhasDasCasas.MontarLoja(d, t, l, desenho);
                Assert.Greater(desenho.Triangulos, 0);
                Assert.Greater(r.Colisao.Triangulos, 0);
                Assert.Less(r.Triangulos, 30000, $"{PlantaDeLoja.Nome(p.tipo)} pesada demais");
                Assert.AreEqual(p.comodos.Count, r.Zonas.Count, "os pisos de cada cômodo");
                Assert.GreaterOrEqual(r.TamanhoDeDentro.y, p.PeDireitoDe(0) - 0.01f);
                MalhaDeObra.Devolver(desenho);
                MalhaDeObra.Devolver(r.Colisao);
            }
            Assert.AreEqual(5, tipos.Count, "os 5 tipos das cidades");
            // a loja de conveniência do posto
            var posto = PlantasDeLoja.DoPosto(new System.Random(3), M);
            var conveniencia = new Loja(posto, M);
            conveniencia.Montar((x, z) => 0f, 0f);
            Assert.Greater(conveniencia.vis.Triangulos, 0);
            Assert.Greater(conveniencia.col.Triangulos, 0);
        }
    }
}
