using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// A floresta (Plano_Mapa, Parte M9a): a mesma da mesma semente (em qualquer ordem e em dois mundos gerados), nada nas
    /// estradas, no acostamento, nas ruas, nas calçadas e nas construções das cidades (e, nos lotes, só nos baldios e nos
    /// fundos dos quintais), na água e nos lugares do mapa (nem a grama), nenhuma árvore encostada nos objetos da beira, o
    /// pé no chão, o mapa com mata de verdade, plantios e todas as espécies, a mata chegando nas cidades e nas estradas,
    /// sem vazios no campo (a pedido: o mapa coberto de árvores, mato e grama), as malhas no Cidade_Arvores.fbx (com o
    /// vento na cor do vértice) e o mato das frestas (Parte M9c) só nas ruas e nas calçadas das cidades.
    /// </summary>
    public class FlorestaTestes
    {
        static DadosDoMundo _medio;
        static DadosDoMundo Medio => _medio ??= GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);

        // um bloco em cada `passo`, dos que têm terreno
        static IEnumerable<Vector2Int> Blocos(DadosDoMundo d, int passo)
        {
            for (int bz = 0; bz < d.BlocosPorLado; bz += passo)
            for (int bx = 0; bx < d.BlocosPorLado; bx += passo)
                yield return new Vector2Int(bx, bz);
        }

        [Test]
        public void AMesmaSementeDaAMesmaFloresta()
        {
            var a = GeradorDoMundo.Gerar(3, TamanhoDoMundo.Pequeno);
            var b = GeradorDoMundo.Gerar(3, TamanhoDoMundo.Pequeno);
            Assert.AreEqual(a.Resumo, b.Resumo);
            var la = new List<PlantaDaFloresta>();
            var lb = new List<PlantaDaFloresta>();
            // em b, os blocos na ordem contrária
            var blocos = Blocos(a, 2).ToList();
            var porB = new Dictionary<Vector2Int, List<PlantaDaFloresta>>();
            for (int i = blocos.Count - 1; i >= 0; i--)
            {
                b.Floresta.DoBloco(blocos[i].x, blocos[i].y, lb);
                porB[blocos[i]] = new List<PlantaDaFloresta>(lb);
            }
            int total = 0;
            foreach (var k in blocos)
            {
                a.Floresta.DoBloco(k.x, k.y, la);
                var outra = porB[k];
                Assert.AreEqual(la.Count, outra.Count, $"bloco {k}");
                for (int i = 0; i < la.Count; i++)
                {
                    Assert.AreEqual(la[i].Especie, outra[i].Especie, $"bloco {k}, planta {i}");
                    Assert.AreEqual(la[i].Posicao, outra[i].Posicao, $"bloco {k}, planta {i}");
                    Assert.AreEqual(la[i].Giro, outra[i].Giro, $"bloco {k}, planta {i}");
                }
                total += la.Count;
            }
            Assert.Greater(total, 1000, "quase nenhuma planta no mapa pequeno");
        }

        [Test]
        public void NadaNasEstradasNasCidadesNaAguaNemNosLugares()
        {
            var d = Medio;
            var lista = new List<PlantaDaFloresta>();
            var erros = new List<string>();
            int plantas = 0;
            var grama = new List<PlantaDaFloresta>();
            foreach (var k in Blocos(d, 2))
            {
                d.Floresta.DoBloco(k.x, k.y, lista);
                if (k.x % 6 == 0)
                {
                    d.Floresta.GramaDoBloco(k.x, k.y, grama);
                    lista.AddRange(grama);
                }
                foreach (var p in lista)
                {
                    plantas++;
                    float x = p.Posicao.x, z = p.Posicao.z;
                    string onde = $"{Floresta.Nomes[(int)p.Especie]} em ({x:0.0}, {z:0.0})";
                    if (Mathf.Abs(p.Posicao.y - d.Altura(x, z)) > 0.2f) erros.Add($"{onde}: fora do chão");
                    // a grama sai da mancha da célula de 3,2 m: até 1,5 m do ponto conferido (a folga do Livre)
                    float folga = Floresta.EhGrama(p.Especie) ? -1.6f : 0f;
                    if (d.FunduraDaAgua(x, z) > 0f) erros.Add($"{onde}: na água");
                    var cidade = d.CidadeNoChao(x, z, out float peso);
                    if (cidade != null && peso > 0f)
                    {
                        var ponto = new Vector2(x, z);
                        if (cidade.Ocupado(ponto, Floresta.EhGrama(p.Especie) ? 0f : 1.2f)) erros.Add($"{onde}: numa rua, calçada ou construção");
                        var lote = cidade.LoteEm(ponto);
                        if (lote != null && lote.Tipo != TipoDeLote.Baldio && lote.Tipo != TipoDeLote.Casa) erros.Add($"{onde}: num lote de {lote.Tipo}");
                    }
                    if (!Floresta.EhGrama(p.Especie))
                    {
                        var objetos = d.Objetos.DoBloco(d.BlocoDe(x, z).x, d.BlocoDe(x, z).y);
                        if (objetos != null)
                            foreach (int i in objetos)
                                if (Vector2.Distance(d.Objetos.Todos[i].Posicao, new Vector2(x, z)) < 2f)
                                    erros.Add($"{onde}: encostada em {d.Objetos.Todos[i].Tipo}");
                    }
                    int e = d.Rede.MaisPerto(x, z, out float dist, out _);
                    if (e >= 0 && dist < RedeDeEstradas.MeiaLargura(d.Plano.Estradas[e].Tipo) + RedeDeEstradas.Acostamento + folga)
                        erros.Add($"{onde}: na estrada (a {dist:0.0} m do meio)");
                    foreach (var l in d.Plano.Lugares)
                        if (!l.EhCidade && Vector2.Distance(l.Centro, new Vector2(x, z)) < l.Raio)
                            erros.Add($"{onde}: dentro de {l.Tipo} {l.Nome}");
                    if (erros.Count > 20) break;
                }
            }
            Assert.IsEmpty(erros, string.Join("\n", erros.Take(20)));
            Assert.Greater(plantas, 20000, "quase nenhuma planta no mapa médio");
        }

        [Test]
        public void OMatoDasFrestasFicaNasRuasDasCidades()
        {
            // Parte M9c: a grama e o capim nas frestas (a sarjeta, a beira da calçada e as rachaduras), só nas ruas das cidades
            var d = Medio;
            var lista = new List<PlantaDaFloresta>();
            var erros = new List<string>();
            int tufos = 0, blocos = 0;
            for (int bz = 0; bz < d.BlocosPorLado; bz++)
            for (int bx = 0; bx < d.BlocosPorLado; bx++)
            {
                if (d.CidadesDoBloco(bx, bz) == null) continue;
                lista.Clear();
                d.Floresta.FrestasDoBloco(bx, bz, lista);
                if (lista.Count > 0) blocos++;
                foreach (var p in lista)
                {
                    tufos++;
                    float x = p.Posicao.x, z = p.Posicao.z;
                    string onde = $"{Floresta.Nomes[(int)p.Especie]} em ({x:0.0}, {z:0.0})";
                    if (!Floresta.EhGrama(p.Especie) && p.Especie != Especie.CapimB) erros.Add($"{onde}: não é mato");
                    float acima = p.Posicao.y - d.Altura(x, z);
                    if (acima < 0f || acima > TracadoDaCidade.AlturaDaCalcada) erros.Add($"{onde}: fora do chão da rua ({acima:0.00} m)");
                    var cidade = d.CidadeNoChao(x, z, out float peso);
                    if (cidade == null || peso <= 0f) erros.Add($"{onde}: fora da cidade");
                    else if (!cidade.Ocupado(new Vector2(x, z), 0f)) erros.Add($"{onde}: fora das ruas e das calçadas");
                    if (erros.Count > 20) break;
                }
            }
            Assert.IsEmpty(erros, string.Join("\n", erros.Take(20)));
            Assert.Greater(blocos, 20, "quase nenhum bloco de cidade com mato nas frestas");
            Assert.Greater(tufos / Mathf.Max(1, blocos), 50, "pouco mato nas frestas");
        }

        [Test]
        public void OMapaTemMataPlantiosETodasAsEspecies()
        {
            var d = Medio;
            Assert.Greater(d.Floresta.Plantios, 2, "sem eucaliptais e pinheirais");
            var lista = new List<PlantaDaFloresta>();
            var grama = new List<PlantaDaFloresta>();
            var porEspecie = new int[Floresta.Quantas];
            int blocos = 0, comMata = 0;
            foreach (var k in Blocos(d, 2))
            {
                var c = d.CentroDoBloco(k.x, k.y);
                if (!d.Dentro(c.x, c.y)) continue;
                d.Floresta.DoBloco(k.x, k.y, lista);
                foreach (var p in lista) porEspecie[(int)p.Especie]++;
                if (k.x % 6 == 0)
                {
                    d.Floresta.GramaDoBloco(k.x, k.y, grama);
                    foreach (var p in grama) porEspecie[(int)p.Especie]++;
                }
                blocos++;
                if (lista.Count(p => Floresta.EhArvore(p.Especie)) >= 60) comMata++;
            }
            var faltam = Enumerable.Range(0, Floresta.Quantas).Where(i => porEspecie[i] == 0).Select(i => Floresta.Nomes[i]).ToList();
            Assert.IsEmpty(faltam, "espécies que não nasceram: " + string.Join(", ", faltam));
            float fracao = comMata / (float)blocos;
            Debug.Log($"Floresta do Médio 7: {fracao:P0} dos blocos com mata fechada; " +
                      string.Join(", ", Enumerable.Range(0, Floresta.Quantas).Select(i => $"{Floresta.Nomes[i]} {porEspecie[i]}")));
            int coniferas = porEspecie[(int)Especie.AbetoA] + porEspecie[(int)Especie.AbetoB] + porEspecie[(int)Especie.AbetoC]
                + porEspecie[(int)Especie.PinheiroBrancoA] + porEspecie[(int)Especie.PinheiroBrancoB]
                + porEspecie[(int)Especie.PinheiroVermelhoA] + porEspecie[(int)Especie.PinheiroVermelhoB];
            int arvores = Enumerable.Range(0, Floresta.Quantas).Where(i => Floresta.EhArvore((Especie)i)).Sum(i => porEspecie[i]);
            Debug.Log($"As coníferas são {coniferas / (float)arvores:P0} das árvores");
            Assert.Greater(coniferas, arvores * 0.55f, "poucos pinheiros e abetos (a pedido, a maioria)");
            Assert.Greater(fracao, 0.3f, "pouca mata fechada");
            Assert.Less(fracao, 0.95f, "mata demais (sem campo)");
        }

        [Test]
        public void AMataChegaNasCidadesENasEstradas()
        {
            // a pedido: em volta das cidades e na beira das estradas, a mata fechada (sem as faixas vazias de antes), e nas
            // cidades, árvores nos baldios e nos quintais
            var d = Medio;
            var lista = new List<PlantaDaFloresta>();
            int naCidade = 0, emVolta = 0, naBeira = 0, pontosEmVolta = 0, pontosNaBeira = 0;
            foreach (var k in Blocos(d, 1))
            {
                var c = d.CentroDoBloco(k.x, k.y);
                if (!d.Dentro(c.x, c.y, 300f)) continue;
                d.Floresta.DoBloco(k.x, k.y, lista);
                foreach (var p in lista)
                {
                    if (!Floresta.EhArvore(p.Especie)) continue;
                    var cidade = d.CidadeNoChao(p.Posicao.x, p.Posicao.z, out float peso);
                    if (cidade != null && d.CidadeEm(p.Posicao.x, p.Posicao.z) != null) naCidade++;
                    else if (cidade != null && peso > 0f) emVolta++;
                    if (d.Rede.MaisPerto(p.Posicao.x, p.Posicao.z, out float dist, out _) >= 0 && dist < 16f) naBeira++;
                }
                // a área: os pontos de 8 m do bloco em volta das cidades e a até 16 m de uma estrada
                for (int j = 0; j < 8; j++)
                for (int i = 0; i < 8; i++)
                {
                    float x = d.CantoDoBloco(k.x, k.y).x + (i + 0.5f) * 8f, z = d.CantoDoBloco(k.x, k.y).y + (j + 0.5f) * 8f;
                    var cidade = d.CidadeNoChao(x, z, out float peso);
                    if (cidade != null && peso > 0f && d.CidadeEm(x, z) == null) pontosEmVolta++;
                    if (d.Rede.MaisPerto(x, z, out float dist, out _) >= 0 && dist < 16f) pontosNaBeira++;
                }
            }
            // árvores por hectare (cada ponto vale 64 m²)
            float porHaEmVolta = emVolta / (pontosEmVolta * 64f / 10000f), porHaNaBeira = naBeira / (pontosNaBeira * 64f / 10000f);
            Debug.Log($"Médio 7: {naCidade} árvores dentro das cidades; {porHaEmVolta:0} árvores por hectare em volta delas e {porHaNaBeira:0} na beira das estradas");
            Assert.Greater(naCidade, 500, "quase nenhuma árvore nos baldios e nos quintais");
            Assert.Greater(porHaEmVolta, 80f, "a volta das cidades vazia");
            Assert.Greater(porHaNaBeira, 60f, "a beira das estradas vazia");
        }

        [Test]
        public void OCampoNaoTemVazios()
        {
            // os blocos todos no campo (longe das cidades, da água, das estradas e dos lugares): todos com árvores, mato e
            // grama, sem um chão pelado
            var d = Medio;
            var lista = new List<PlantaDaFloresta>();
            var grama = new List<PlantaDaFloresta>();
            int blocos = 0, vazios = 0, semGrama = 0;
            float menor = float.MaxValue;
            foreach (var k in Blocos(d, 3))
            {
                var canto = d.CantoDoBloco(k.x, k.y);
                bool todo = true;
                for (int j = 0; j <= 4 && todo; j++)
                for (int i = 0; i <= 4 && todo; i++)
                    if (d.Floresta.ManchaEm(canto.x + i * 16f, canto.y + j * 16f, out _) == Mancha.Nada) todo = false;
                if (!todo) continue;
                d.Floresta.DoBloco(k.x, k.y, lista);
                d.Floresta.GramaDoBloco(k.x, k.y, grama);
                blocos++;
                // as plantas do bloco: as árvores valem pela copa (mais ou menos 25 m²), o mato e a grama pelo tufo
                float coberto = lista.Sum(p => Floresta.EhArvore(p.Especie) ? 25f * p.Escala * p.Escala : 1f) + grama.Count * 0.5f;
                menor = Mathf.Min(menor, coberto);
                if (coberto < 600f) vazios++;
                if (grama.Count < 200) semGrama++;
            }
            Debug.Log($"Campo do Médio 7: {blocos} blocos inteiros no campo, {vazios} com pouca planta, {semGrama} com pouca grama; o menos coberto, {menor:0} m²");
            Assert.Greater(blocos, 50);
            Assert.LessOrEqual(vazios, blocos / 50, "blocos do campo quase sem planta");
            Assert.LessOrEqual(semGrama, blocos / 10, "blocos do campo quase sem grama");
        }

        [Test]
        public void AsMalhasEstaoNoFbx()
        {
            var malhas = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Cidade/Cidade_Arvores.fbx").OfType<Mesh>().ToDictionary(m => m.name);
            foreach (var nome in Floresta.Nomes)
                foreach (var sufixo in new[] { "", "_Longe1", "_Longe2" })
                {
                    Assert.IsTrue(malhas.TryGetValue(nome + sufixo, out var m), $"falta a malha {nome}{sufixo}");
                    Assert.Greater(m.vertexCount, 0, nome + sufixo);
                    Assert.IsTrue(m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color), $"{nome}{sufixo} sem o vento (a cor do vértice)");
                }
        }
    }
}
