using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O sorteio dos NPCs (a parte 2 do Plano_NPCs_Bunker.md da pasta de arte e a tela das cartas, de 04/11/2026): sorteia
    /// 300 mundos de 52 (15.600 fichas) e confere as regras (o parente primeiro, as idades das histórias, os traços sem o par,
    /// as condições, o que ele não faz, os laços dos dois lados e com as idades certas, os nomes sem repetir no mundo, o
    /// sobrenome só na família e a família no grupo 0) e os números (os laços, os grupos, os fumantes, as condições por idade);
    /// e as cartas (o parente na primeira mão, as 25 cartas sem repetir) e a separação entre o bunker e o mapa.
    /// </summary>
    public class MoradoresTestes
    {
        const int Mundos = 300;

        [Test]
        public void AsListasEstaoCertas()
        {
            var antes = new HashSet<string>();
            foreach (var h in ListasDosMoradores.Antes)
            {
                Assert.IsTrue(antes.Add(h.chave), $"a história {h.chave} repetida");
                Assert.LessOrEqual(h.idadeMin, h.idadeMax, $"{h.chave}: a idade mínima passa da máxima");
                Assert.Contains(h.roupa, ListasDosMoradores.TiposDeRoupa, $"{h.chave}: a roupa {h.roupa} não é um tipo do sorteio");
                StringAssert.DoesNotContain("{", h.frase.Replace("{N}", "").Replace("{o}", "").Replace("{anos}", "").Replace("{escola}", ""), h.chave);
                if (h.puxa != null) Assert.IsNotNull(ListasDosMoradores.Traco(h.puxa), $"{h.chave} puxa {h.puxa}, que não existe");
            }
            var depois = new HashSet<string>();
            foreach (var h in ListasDosMoradores.Depois)
            {
                Assert.IsTrue(depois.Add(h.chave), $"a história {h.chave} repetida");
                if (h.puxa != null) Assert.IsNotNull(ListasDosMoradores.Traco(h.puxa), $"{h.chave} puxa {h.puxa}, que não existe");
            }
            Assert.IsNotNull(ListasDosMoradores.DeDepois(SorteioDosMoradores.SeparadoDaFamilia), "falta a história de quem se perdeu da família");
            // toda idade tem uma história de antes
            for (int i = SorteioDosMoradores.IdadeMinima; i <= SorteioDosMoradores.IdadeMaxima; i++)
                Assert.IsTrue(ListasDosMoradores.Antes.Any(h => i >= h.idadeMin && i <= h.idadeMax), $"nenhuma história de antes para {i} anos");
            foreach (var t in ListasDosMoradores.Tracos)
                if (t.oposto != null)
                    Assert.AreEqual(t.chave, ListasDosMoradores.Traco(t.oposto)?.oposto, $"o par de {t.chave} não aponta de volta");
            Assert.AreEqual(ListasDosMoradores.Habilidades, ListasDosMoradores.NomesDasHabilidades.Length);
            Assert.AreEqual(ListasDosMoradores.Habilidades, Enum.GetValues(typeof(Habilidade)).Length);
        }

        [Test]
        public void AMesmaSementeDaOMesmoMundo()
        {
            for (int s = 1; s <= 20; s++)
            {
                var a = JsonUtility.ToJson(SorteioDosMoradores.SortearMundo(s));
                var b = JsonUtility.ToJson(SorteioDosMoradores.SortearMundo(s));
                Assert.AreEqual(a, b, $"a semente {s} deu dois mundos diferentes");
                var m = SorteioDosMoradores.SortearMundo(s);
                CollectionAssert.AreEqual(SorteioDosMoradores.Baralho(m), SorteioDosMoradores.Baralho(m), $"a semente {s} deu dois baralhos");
            }
            Assert.AreNotEqual(JsonUtility.ToJson(SorteioDosMoradores.SortearMundo(1)), JsonUtility.ToJson(SorteioDosMoradores.SortearMundo(2)));
        }

        [Test]
        public void AsRegrasValemEm300Mundos()
        {
            var falhas = new List<string>();
            int fichas = 0, fumantes = 0, lacos = 0, lacosDeFora = 0, comApelido = 0, grupos = 0, comComAFamilia = 0;
            var tamanhos = new Dictionary<int, int>();
            var faixas = new (int de, int ate, float esperada)[] { (16, 30, 0.10f), (31, 55, 0.25f), (56, 70, 0.5f), (71, 80, 0.75f) };
            var naFaixa = new int[faixas.Length];
            var comCondicao = new int[faixas.Length];
            var porAntes = new Dictionary<string, int>();
            var porDepois = new Dictionary<string, int>();
            var porTraco = new Dictionary<string, int>();
            var porCondicao = new Dictionary<string, int>();
            var somaHab = new double[ListasDosMoradores.Habilidades];
            var paixoes = new int[3];
            int incapazes = 0;
            var porRoupa = new Dictionary<string, int>();

            void Falha(int s, FichaDoMorador f, string o)
            {
                if (falhas.Count < 40) falhas.Add($"mundo {s}, {f.NomeCompleto} ({f.idade}): {o}");
            }

            for (int s = 1; s <= Mundos; s++)
            {
                var g = SorteioDosMoradores.SortearMundo(s);
                if (g.fichas.Count != SorteioDosMoradores.NpcsNoMundo) falhas.Add($"mundo {s}: {g.fichas.Count} NPCs");
                var primeiro = g.fichas[0];
                if (primeiro.parente == ParenteDaFamilia.Nenhum) Falha(s, primeiro, "o primeiro não é parente da família");
                if (primeiro.idade < 55) Falha(s, primeiro, "o parente com menos de 55 anos");
                if (primeiro.grupo != 0) Falha(s, primeiro, "o parente fora do grupo 0");
                var nomes = new HashSet<string>();
                var ids = new HashSet<int>();
                int lacosDoMundo = 0;
                foreach (var grupo in g.fichas.GroupBy(f => f.grupo))
                {
                    if (grupo.Key == 0) continue;
                    grupos++;
                    tamanhos[grupo.Count()] = tamanhos.TryGetValue(grupo.Count(), out var nt) ? nt + 1 : 1;
                }
                foreach (var f in g.fichas)
                {
                    fichas++;
                    if (!nomes.Add(f.nome)) Falha(s, f, "o nome repetido no mundo");
                    if (!ids.Add(f.id)) Falha(s, f, "o id repetido");
                    if (f.id <= 0 || f.id >= g.proximoId) Falha(s, f, $"o id {f.id} fora de 1 a {g.proximoId - 1}");
                    if (f.idade < SorteioDosMoradores.IdadeMinima || f.idade > SorteioDosMoradores.IdadeMaxima) Falha(s, f, "a idade fora");
                    var antes = ListasDosMoradores.DeAntes(f.antes);
                    if (antes == null) Falha(s, f, $"a história {f.antes} não existe");
                    else if (f.idade < antes.idadeMin || f.idade > antes.idadeMax) Falha(s, f, $"{f.antes} com {f.idade} anos");
                    if (ListasDosMoradores.DeDepois(f.depois) == null) Falha(s, f, $"a história {f.depois} não existe");
                    if (f.parente != ParenteDaFamilia.Nenhum && f.depois != ListasDosMoradores.ComAFamilia) Falha(s, f, "o parente não entrou com a família");
                    if (f.parente != ParenteDaFamilia.Nenhum && f.grupo != 0) Falha(s, f, "o parente fora do grupo da família");
                    if (f.depois == ListasDosMoradores.ComAFamilia)
                    {
                        comComAFamilia++;
                        if (f.grupo != 0) Falha(s, f, "entrou no bunker com a família sem ser do grupo dela");
                    }
                    porDepois[f.depois] = porDepois.TryGetValue(f.depois, out var nd) ? nd + 1 : 1;

                    // as habilidades e as paixões
                    for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
                    {
                        if (f.habilidades[h] < 0 || f.habilidades[h] > 20) Falha(s, f, $"a habilidade {h} = {f.habilidades[h]}");
                        if (f.Incapaz((Habilidade)h) && (f.habilidades[h] != 0 || f.paixoes[h] != Paixao.Nenhuma)) Falha(s, f, $"incapaz em {h}, com pontos ou paixão");
                        somaHab[h] += f.habilidades[h];
                        paixoes[(int)f.paixoes[h]]++;
                    }
                    int quantasPaixoes = f.paixoes.Count(p => p != Paixao.Nenhuma);
                    if (quantasPaixoes < 1 || quantasPaixoes > 3) Falha(s, f, $"{quantasPaixoes} paixões");
                    if (f.naoFaz != Incapacidade.Nenhuma) incapazes++;
                    if (f.TemCondicao("CoracaoFraco") && !f.NaoFaz(Incapacidade.NadaDeTrabalhoPesado)) Falha(s, f, "o coração fraco faz trabalho pesado");

                    // os traços e as condições
                    if (f.tracos.Count < 1 || f.tracos.Count > 3) Falha(s, f, $"{f.tracos.Count} traços");
                    if (f.tracos.Distinct().Count() != f.tracos.Count) Falha(s, f, "o traço repetido");
                    foreach (var t in f.tracos)
                    {
                        var dados = ListasDosMoradores.Traco(t);
                        if (dados == null) { Falha(s, f, $"o traço {t} não existe"); continue; }
                        if (dados.oposto != null && f.Tem(dados.oposto)) Falha(s, f, $"{t} com {dados.oposto}");
                        porTraco[t] = porTraco.TryGetValue(t, out var n) ? n + 1 : 1;
                    }
                    if (f.Tem("Rapido") && f.TemCondicao("JoelhoRuim")) Falha(s, f, "rápido com o joelho ruim");
                    if (f.condicoes.Count > 2 || f.condicoes.Distinct().Count() != f.condicoes.Count) Falha(s, f, "as condições");
                    foreach (var c in f.condicoes)
                    {
                        if (ListasDosMoradores.Condicao(c) == null) Falha(s, f, $"a condição {c} não existe");
                        porCondicao[c] = porCondicao.TryGetValue(c, out var n) ? n + 1 : 1;
                    }
                    if (f.Tem(ListasDosMoradores.Fumante)) fumantes++;
                    if (!string.IsNullOrEmpty(f.apelido)) comApelido++;
                    porAntes[f.antes] = porAntes.TryGetValue(f.antes, out var na) ? na + 1 : 1;

                    // a aparência
                    var a = f.aparencia;
                    if (!ListasDosMoradores.TiposDeRoupa.Contains(a.tipoDaRoupa)) Falha(s, f, $"a roupa {a.tipoDaRoupa}");
                    if (a.mulher != f.mulher) Falha(s, f, "a aparência com o outro sexo");
                    if (a.pele < 0 || a.pele > 5 || a.cabelo < 0 || a.cabelo > 5) Falha(s, f, "a pele ou o cabelo fora da lista");
                    if (f.idade > 65 && a.tipoDaRoupa != "Idoso") Falha(s, f, "acima de 65 sem a roupa do idoso");
                    if (f.idade >= 62 && !a.grisalho) Falha(s, f, "acima de 62 sem o cabelo grisalho");
                    porRoupa[a.tipoDaRoupa] = porRoupa.TryGetValue(a.tipoDaRoupa, out var nr) ? nr + 1 : 1;

                    // as condições por idade (sem o parente, que quase sempre tem uma)
                    if (f.parente == ParenteDaFamilia.Nenhum)
                        for (int k = 0; k < faixas.Length; k++)
                            if (f.idade >= faixas[k].de && f.idade <= faixas[k].ate)
                            {
                                naFaixa[k]++;
                                if (f.condicoes.Count > 0) comCondicao[k]++;
                            }

                    // os laços: os dois lados, um laço por par, as idades e os sexos
                    foreach (var l in f.lacos)
                    {
                        var o = g.DoId(l.outro);
                        if (o == null) { Falha(s, f, $"o laço com {l.outro}, que não está no mundo"); continue; }
                        var volta = o.LacoCom(f.id);
                        if (volta == null || volta.tipo != SorteioDosMoradores.Inverso(l.tipo)) Falha(s, f, $"o laço {l.tipo} com {o.nome} sem a volta");
                        if (f.lacos.Count(x => x.outro == l.outro) > 1) Falha(s, f, $"dois laços com {o.nome}");
                        if (o.grupo != f.grupo) lacosDeFora++;
                        int dif = o.idade - f.idade; // o outro menos este
                        switch (l.tipo)
                        {
                            case LacoDoMorador.Casal:
                                if (o.mulher == f.mulher) Falha(s, f, "o casal do mesmo sexo");
                                if (Math.Abs(dif) > 12) Falha(s, f, $"o casal com {dif} anos de diferença");
                                if (f.idade < 18) Falha(s, f, "casado com menos de 18");
                                if (o.grupo != f.grupo) Falha(s, f, "o casal em grupos diferentes");
                                break;
                            case LacoDoMorador.Pai:
                                if (dif < 18 || dif > 45) Falha(s, f, $"o pai {o.nome} {dif} anos mais velho");
                                break;
                            case LacoDoMorador.Avo:
                                if (dif < 40 || dif > 60) Falha(s, f, $"o avô {o.nome} {dif} anos mais velho");
                                break;
                            case LacoDoMorador.Irmao:
                                if (Math.Abs(dif) > 27) Falha(s, f, $"o irmão com {dif} anos de diferença");
                                break;
                        }
                        lacosDoMundo++;
                    }
                    if (f.lacos.Count(l => l.tipo == LacoDoMorador.Casal) > 1) Falha(s, f, "dois casais");
                    if (f.lacos.Count(l => l.tipo == LacoDoMorador.Pai) > 2) Falha(s, f, "mais de dois pais");
                    var pais = f.lacos.Where(l => l.tipo == LacoDoMorador.Pai).Select(l => g.DoId(l.outro)).ToList();
                    if (pais.Count == 2 && pais[0].mulher == pais[1].mulher) Falha(s, f, "os dois pais do mesmo sexo");
                    // os filhos do mesmo pai são irmãos
                    foreach (var filho1 in f.lacos.Where(l => l.tipo == LacoDoMorador.Filho))
                        foreach (var filho2 in f.lacos.Where(l => l.tipo == LacoDoMorador.Filho))
                            if (filho1.outro < filho2.outro && g.DoId(filho1.outro).LacoCom(filho2.outro)?.tipo != LacoDoMorador.Irmao)
                                Falha(s, f, $"os filhos {g.DoId(filho1.outro).nome} e {g.DoId(filho2.outro).nome} não são irmãos");
                }
                foreach (var f in g.fichas.Skip(1))
                    if (f.parente != ParenteDaFamilia.Nenhum && f.LacoCom(primeiro.id)?.tipo != LacoDoMorador.Casal)
                        Falha(s, f, "parente da família sem ser o casal do primeiro");
                // o mesmo sobrenome só entre quem está ligado por laços (a família de cada um)
                var familia = g.fichas.ToDictionary(f => f.id, f => f.id);
                int Raiz(int id) => familia[id] == id ? id : familia[id] = Raiz(familia[id]);
                foreach (var f in g.fichas)
                    foreach (var l in f.lacos)
                        if (familia.ContainsKey(l.outro)) familia[Raiz(f.id)] = Raiz(l.outro);
                for (int a = 0; a < g.fichas.Count; a++)
                    for (int b = a + 1; b < g.fichas.Count; b++)
                        if (g.fichas[a].sobrenome == g.fichas[b].sobrenome && Raiz(g.fichas[a].id) != Raiz(g.fichas[b].id))
                            Falha(s, g.fichas[b], $"o sobrenome de {g.fichas[a].NomeCompleto}, sem ser da família");
                lacos += lacosDoMundo / 2;
            }

            var log = new StringBuilder();
            log.AppendLine($"[Moradores] {Mundos} mundos, {fichas} fichas: {lacos / (float)Mundos:F1} laços por mundo ({lacosDeFora / 2f / Mundos:F1} entre grupos), " +
                           $"{grupos / (float)Mundos:F1} grupos no mapa por mundo, {fumantes * 100f / fichas:F1}% fumantes, {comApelido * 100f / fichas:F0}% com apelido, " +
                           $"{incapazes * 100f / fichas:F0}% com um \"não faz\", {comComAFamilia / (float)Mundos:F2} com a família por mundo");
            log.AppendLine("  os tamanhos dos grupos: " + string.Join(", ", tamanhos.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value * 100f / grupos:F0}%")));
            for (int k = 0; k < faixas.Length; k++)
                log.AppendLine($"  com condição, de {faixas[k].de} a {faixas[k].ate} anos: {comCondicao[k] * 100f / Math.Max(1, naFaixa[k]):F1}% (o plano: {faixas[k].esperada * 100f:F0}%), {naFaixa[k]} fichas");
            log.AppendLine("  as habilidades, em média: " + string.Join(", ", Enumerable.Range(0, ListasDosMoradores.Habilidades)
                .Select(h => $"{ListasDosMoradores.NomesDasHabilidades[h]} {somaHab[h] / fichas:F1}")));
            log.AppendLine($"  as paixões por ficha: {(paixoes[1] + paixoes[2]) / (float)fichas:F2} ({paixoes[2] * 100f / Math.Max(1, paixoes[1] + paixoes[2]):F0}% apaixonado)");
            log.AppendLine("  as histórias de antes: " + string.Join(", ", porAntes.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100f / fichas:F1}%")));
            log.AppendLine("  as dos anos do apocalipse: " + string.Join(", ", porDepois.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100f / fichas:F1}%")));
            log.AppendLine("  os traços: " + string.Join(", ", porTraco.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100f / fichas:F1}%")));
            log.AppendLine("  as condições: " + string.Join(", ", porCondicao.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100f / fichas:F1}%")));
            log.AppendLine("  as roupas: " + string.Join(", ", porRoupa.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value * 100f / fichas:F1}%")));
            var exemplo = SorteioDosMoradores.SortearMundo(1);
            for (int r = 0; r < 2; r++)
            {
                log.AppendLine($"--- o mundo 1, a mão {r + 1}");
                foreach (var f in SorteioDosMoradores.Mao(exemplo, r)) log.AppendLine($"(grupo {f.grupo}) " + SorteioDosMoradores.Descrever(f, exemplo));
            }
            Debug.Log(log.ToString());

            Assert.IsEmpty(falhas, string.Join("\n", falhas));
            Assert.That(lacos / (float)Mundos, Is.InRange(14f, 40f), "os laços por mundo");
            Assert.That(fumantes / (float)fichas, Is.InRange(0.35f, 0.45f), "os fumantes longe dos 40%");
            for (int k = 0; k < faixas.Length; k++)
                Assert.That(comCondicao[k] / (float)Math.Max(1, naFaixa[k]), Is.InRange(faixas[k].esperada - 0.06f, faixas[k].esperada + 0.06f),
                            $"as condições de {faixas[k].de} a {faixas[k].ate} anos longe da tabela");
        }

        [Test]
        public void AsCartasNaoRepetemEOParenteVemNaPrimeiraMao()
        {
            for (int s = 1; s <= 300; s++)
            {
                var m = SorteioDosMoradores.SortearMundo(s);
                var b = SorteioDosMoradores.Baralho(m);
                Assert.AreEqual(m.fichas.Count, b.Count, $"mundo {s}: o baralho sem todos");
                Assert.AreEqual(b.Count, b.Distinct().Count(), $"mundo {s}: a carta repetida no baralho");
                var vistas = new HashSet<int>();
                for (int r = 0; r < SorteioDosMoradores.MoradoresNoComeco; r++)
                {
                    var mao = SorteioDosMoradores.Mao(m, r);
                    Assert.AreEqual(SorteioDosMoradores.CartasPorRodada, mao.Count, $"mundo {s}, rodada {r}");
                    foreach (var f in mao) Assert.IsTrue(vistas.Add(f.id), $"mundo {s}: {f.nome} em duas mãos");
                }
                Assert.Contains(m.Parente, SorteioDosMoradores.Mao(m, 0), $"mundo {s}: o parente fora da primeira mão");
            }
            // o parente não fica sempre no mesmo lugar da mão
            var lugares = Enumerable.Range(1, 200).Select(s =>
            {
                var m = SorteioDosMoradores.SortearMundo(s);
                return SorteioDosMoradores.Mao(m, 0).IndexOf(m.Parente);
            }).Distinct().Count();
            Assert.AreEqual(SorteioDosMoradores.CartasPorRodada, lugares, "o parente sempre nos mesmos lugares da mão");
        }

        [Test]
        public void SepararOBunkerDoMapa()
        {
            for (int s = 1; s <= 100; s++)
            {
                var m = SorteioDosMoradores.SortearMundo(s);
                // um de cada mão; em metade dos mundos, sem o parente
                var escolhidos = new List<int>();
                for (int r = 0; r < SorteioDosMoradores.MoradoresNoComeco; r++)
                {
                    var mao = SorteioDosMoradores.Mao(m, r);
                    var f = r == 0 && s % 2 == 0 ? mao.First(x => x != m.Parente) : r == 0 ? m.Parente : mao[s % mao.Count];
                    escolhidos.Add(f.id);
                }
                var (bunker, mapa) = SorteioDosMoradores.Separar(m, escolhidos);
                Assert.AreEqual(SorteioDosMoradores.MoradoresNoComeco, bunker.Count);
                Assert.AreEqual(SorteioDosMoradores.NpcsNoMundo - SorteioDosMoradores.MoradoresNoComeco, mapa.Count);
                CollectionAssert.AreEqual(escolhidos, bunker.Select(f => f.id).ToList(), "a ordem dos escolhidos");
                Assert.IsEmpty(bunker.Select(f => f.id).Intersect(mapa.Select(f => f.id)), "alguém no bunker e no mapa");
                foreach (var f in mapa)
                    Assert.AreNotEqual(ListasDosMoradores.ComAFamilia, f.depois, $"mundo {s}: {f.nome} entrou no bunker com a família e está no mapa");
                if (s % 2 == 0) Assert.AreEqual(SorteioDosMoradores.SeparadoDaFamilia, m.Parente.depois, $"mundo {s}: o parente de fora sem a história");
            }
        }

        [Test]
        public void CompletarUmMundoDeAntes()
        {
            for (int s = 1; s <= 100; s++)
            {
                // os 5 de um mundo da tela de antes: os 5 primeiros, com os ids até 8 (os 3 da reserva não ficaram)
                var velho = SorteioDosMoradores.SortearMundo(1000 + s, 5);
                var m = new NpcsDoMundo { semente = s, proximoId = velho.proximoId + 3, fichas = new List<FichaDoMorador>(velho.fichas) };
                SorteioDosMoradores.CompletarMundo(m, s);
                Assert.AreEqual(SorteioDosMoradores.NpcsNoMundo, m.fichas.Count);
                Assert.AreEqual(m.fichas.Count, m.fichas.Select(f => f.id).Distinct().Count(), $"mundo {s}: o id repetido");
                Assert.IsTrue(m.fichas.Skip(5).All(f => f.id >= velho.proximoId + 3 && f.grupo > 0), $"mundo {s}: os novos com os ids ou o grupo errados");
                Assert.AreEqual(m.fichas.Count, m.fichas.Select(f => f.nome).Distinct().Count(), $"mundo {s}: o nome repetido");
            }
        }

        [Test]
        public void OSortearUmNaoTemAFamilia()
        {
            for (int s = 1; s <= 500; s++)
            {
                var f = SorteioDosMoradores.SortearUm(s, 100 + s);
                Assert.AreEqual(ParenteDaFamilia.Nenhum, f.parente);
                Assert.AreNotEqual(ListasDosMoradores.ComAFamilia, f.depois);
                Assert.IsEmpty(f.lacos);
            }
        }
    }
}
