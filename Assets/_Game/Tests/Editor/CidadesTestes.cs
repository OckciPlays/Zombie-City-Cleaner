using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// As cidades em blocos (Plano_Mapa, Parte M5): as ruas e os lotes de cada cidade e vila, as quantidades, os lotes
    /// longe das estradas, o chão quase plano, as rodovias entrando só pelos portões, as coisas das ruas no lugar certo e
    /// as malhas.
    /// </summary>
    public class CidadesTestes
    {
        static readonly Dictionary<(TamanhoDoMundo, int), DadosDoMundo> Guardados = new Dictionary<(TamanhoDoMundo, int), DadosDoMundo>();

        static DadosDoMundo Mundo(TamanhoDoMundo t, int semente)
        {
            if (!Guardados.TryGetValue((t, semente), out var d)) Guardados[(t, semente)] = d = GeradorDoMundo.Gerar(semente, t);
            return d;
        }

        static IEnumerable<DadosDoMundo> Todos()
        {
            foreach (var t in Tamanhos.Todos)
            foreach (int s in new[] { 12345, 7 })
                yield return Mundo(t, s);
        }

        static int Quarteiroes(TracadoDaCidade t)
        {
            int n = 0;
            for (int i = 0; i + 1 < t.Us.Length; i++)
            for (int j = 0; j + 1 < t.Vs.Length; j++)
                if (t.RuaU[i, j] && t.RuaU[i + 1, j] && t.RuaV[i, j] && t.RuaV[i, j + 1]) n++;
            return n;
        }

        [Test]
        public void TodaCidadeTemRuasELotes()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                Assert.AreEqual(d.Plano.Cidades.Count, d.Plano.Tracados.Count, onde);
                foreach (var t in d.Plano.Tracados)
                {
                    int casas = 0;
                    foreach (var l in t.Lotes) if (l.TemConstrucao) casas++;
                    Assert.Greater(t.Pecas.Count, 0, $"{onde}: {t.Cidade.Nome} sem ruas");
                    // a vila: de 15 a 40 casas (Plano_Mapa; com as casas de verdade da M6, a menor vila, de raio 80 m, fica com
                    // umas 13); a cidade pequena, de 90 a 200 (a de raio 200 m fica perto de 90); a grande, de umas 500 (no mapa
                    // Pequeno) a 3 mil
                    int minimo = t.EhVila ? 12 : t.Cidade.Tipo == TipoDeLugar.CidadePequena ? 60 : 400;
                    Assert.GreaterOrEqual(casas, minimo, $"{onde}: {t.Cidade.Nome} com {casas} construções");
                    Assert.GreaterOrEqual(t.Portoes.Count, 2, $"{onde}: {t.Cidade.Nome} sem portões");
                }
            }
            // a maior cidade do mapa Grande: uns 380 quarteirões e por volta de 4 mil construções (a conta do Plano_Mapa; com
            // os lotes do tamanho de cada casa de verdade, da Parte M6, com a garagem e o recuo dos lados, saem uns 2,5 mil)
            var g = Mundo(TamanhoDoMundo.Grande, 12345);
            TracadoDaCidade maior = null;
            foreach (var t in g.Plano.Tracados) if (maior == null || t.Cidade.Raio > maior.Cidade.Raio) maior = t;
            int construcoes = 0;
            foreach (var l in maior.Lotes) if (l.TemConstrucao) construcoes++;
            Assert.That(Quarteiroes(maior), Is.InRange(250, 500), "os quarteirões da maior cidade");
            Assert.That(construcoes, Is.InRange(2000, 6000), "as construções da maior cidade");
        }

        [Test]
        public void OsLotesFicamNosQuarteiroesEForaDasEstradas()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                var segmentos = new List<(Vector2 a, Vector2 b, float meia)>();
                foreach (var e in d.Plano.Estradas)
                {
                    if (e.Conector) continue;
                    for (int i = 0; i + 1 < e.Pontos.Count; i++) segmentos.Add((e.Pontos[i], e.Pontos[i + 1], RedeDeEstradas.MeiaLargura(e.Tipo) + 1f));
                }
                foreach (var t in d.Plano.Tracados)
                    foreach (var l in t.Lotes)
                    {
                        // a construção dentro do terreno, e o terreno fora das ruas e das calçadas
                        Assert.IsTrue(l.BU0 >= l.U0 - 0.01f && l.BU1 <= l.U1 + 0.01f && l.BV0 >= l.V0 - 0.01f && l.BV1 <= l.V1 + 0.01f, $"{onde}: construção fora do lote em {t.Cidade.Nome}");
                        var meio = t.Mundo(l.Meio);
                        Assert.AreNotEqual(TracadoDaCidade.NoMapa.Asfalto, t.OQueTem(meio), $"{onde}: lote na rua em {meio}");
                        Assert.AreNotEqual(TracadoDaCidade.NoMapa.Calcada, t.OQueTem(meio), $"{onde}: lote na calçada em {meio}");
                        // nenhuma estrada passa por cima de uma construção
                        if (!l.TemConstrucao) continue;
                        var b = t.Mundo((l.BU0 + l.BU1) * 0.5f, (l.BV0 + l.BV1) * 0.5f);
                        float raio = Mathf.Max(l.BU1 - l.BU0, l.BV1 - l.BV0) * 0.5f;
                        foreach (var (a, c, meia) in segmentos)
                        {
                            if (Mathf.Abs(a.x - b.x) > raio + 40f || Mathf.Abs(a.y - b.y) > raio + 40f) continue;
                            float dist = RedeDeEstradas.DistanciaAoSegmento(b, a, c, out _);
                            Assert.Greater(dist, meia + Mathf.Min(l.BU1 - l.BU0, l.BV1 - l.BV0) * 0.5f, $"{onde}: estrada em cima da construção em {b}");
                        }
                    }
            }
        }

        [Test]
        public void OChaoDaCidadeEQuasePlano()
        {
            foreach (var d in Todos())
                foreach (var t in d.Plano.Tracados)
                {
                    float maior = 0f;
                    foreach (var k in t.Cruzamentos)
                    {
                        var p = t.Mundo(t.Us[k.x], t.Vs[k.y]);
                        float h = d.Altura(p.x, p.y);
                        foreach (var dir in new[] { Vector2.right, Vector2.up })
                        {
                            var q = p + dir * 8f;
                            if (t.Cidade.Q(q.x, q.y) > 1f) continue;
                            maior = Mathf.Max(maior, Mathf.Abs(d.Altura(q.x, q.y) - h) / 8f);
                        }
                    }
                    Assert.Less(maior, 0.07f, $"{d.Tamanho}, semente {d.Semente}: {t.Cidade.Nome} com {maior * 100f:F1}% de subida");
                }
        }

        [Test]
        public void AsEstradasEntramPelosPortoes()
        {
            // dentro do contorno, uma estrada (que não é o caminho por dentro) só anda numa avenida do meio, a do portão
            foreach (var d in Todos())
                foreach (var e in d.Plano.Estradas)
                {
                    if (e.Conector) continue;
                    foreach (var p in e.Pontos)
                    {
                        var t = d.CidadeEm(p.x, p.y);
                        if (t == null) continue;
                        var l = t.Local(p);
                        Assert.Less(Mathf.Min(Mathf.Abs(l.x), Mathf.Abs(l.y)), 3f, $"{d.Tamanho}, semente {d.Semente}: estrada cortando {t.Cidade.Nome} em {p}");
                    }
                }
        }

        [Test]
        public void AsCoisasDasRuasFicamNoLugar()
        {
            foreach (var d in Todos())
            {
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                int fora = 0, total = 0;
                foreach (var o in d.Objetos.Todos)
                {
                    bool daCalcada = o.Tipo == TipoDeObjeto.Poste || o.Tipo == TipoDeObjeto.PlacaDeRua || o.Tipo == TipoDeObjeto.Hidrante || o.Tipo == TipoDeObjeto.Lixeira;
                    bool arvore = o.Tipo == TipoDeObjeto.Arvore;
                    // (os carros das garagens e das vagas das casas, da Parte M6, são do CasasTestes)
                    bool carroNaCidade = o.Tipo == TipoDeObjeto.Carro && !o.NaGaragem && d.CidadeEm(o.Posicao.x, o.Posicao.y) != null;
                    if (!daCalcada && !arvore && !carroNaCidade) continue;
                    var t = d.CidadeNoChao(o.Posicao.x, o.Posicao.y, out _);
                    Assert.IsNotNull(t, $"{onde}: {o.Tipo} fora das cidades em {o.Posicao}");
                    var oque = t.OQueTem(o.Posicao);
                    total++;
                    bool certo = daCalcada ? oque == TracadoDaCidade.NoMapa.Calcada
                               : arvore ? oque == TracadoDaCidade.NoMapa.Calcada || oque == TracadoDaCidade.NoMapa.Praca
                               : oque == TracadoDaCidade.NoMapa.Asfalto;
                    if (!certo) fora++;
                }
                Assert.Greater(total, 100, onde);
                // (umas poucas na beira do asfalto do portão, onde a rua vira rodovia)
                Assert.Less(fora, total * 0.005f + 2, $"{onde}: {fora} de {total} coisas das ruas fora do lugar");
            }
        }

        [Test]
        public void AsMalhasDaCidadeSaem()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int blocos = 0, triangulos = 0, construcoes = 0;
            for (int bz = 0; bz < d.BlocosPorLado; bz++)
            for (int bx = 0; bx < d.BlocosPorLado; bx++)
            {
                if (d.CidadesDoBloco(bx, bz) == null) continue;
                var m = MalhasDaCidade.Bloco(d, bx, bz);
                if (m.Vazio) continue;
                blocos++;
                triangulos += m.Ruas.Triangulos + m.Construcoes.Triangulos + m.CasasDeLonge.Triangulos;
                if (m.Construcoes.Triangulos + m.CasasDeLonge.Triangulos > 0) construcoes++;
                // (as casas de longe não têm colisão: perto, quem colide é a casa de verdade, Parte M6)
                if (m.Ruas.Triangulos + m.Construcoes.Triangulos > 0) Assert.Greater(m.Colisao.Triangulos, 0, $"o bloco {bx}, {bz} sem colisão");
            }
            Assert.Greater(blocos, 300);
            Assert.Greater(construcoes, 200);
            Assert.Less(triangulos / blocos, 20000, "malha pesada demais por bloco");
        }
    }
}
