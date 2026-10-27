using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// As estradas no chão e a beira delas (Plano_Mapa, Parte M4): a rede sem estrada repetida, o chão acomodado embaixo do
    /// asfalto, as rampas, as placas (quem segue as placas chega na cidade) e os objetos dentro do mapa.
    /// </summary>
    public class RedeDeEstradasTestes
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

        [Test]
        public void NenhumaEstradaRepetida()
        {
            foreach (var d in Todos())
            {
                var estradas = d.Plano.Estradas;
                // cada ponto de uma estrada fica longe das outras, a não ser perto de um nó (onde elas se encontram)
                // (os caminhos por dentro das cidades não contam: lá as ruas são da cidade)
                int perto = 0, pontos = 0;
                for (int a = 0; a < estradas.Count; a++)
                    for (int i = 0; i < estradas[a].Pontos.Count && !estradas[a].Conector; i += 2)
                    {
                        var p = estradas[a].Pontos[i];
                        if (estradas[a].AteONo[i] < 40f) continue;
                        pontos++;
                        for (int b = 0; b < estradas.Count; b++)
                        {
                            if (b == a || estradas[b].Conector) continue;
                            var pb = estradas[b].Pontos;
                            bool achou = false;
                            for (int k = 0; k + 1 < pb.Count && !achou; k++)
                                achou = RedeDeEstradas.DistanciaAoSegmento(p, pb[k], pb[k + 1], out _) < 4f;
                            if (achou) { perto++; break; }
                        }
                    }
                Assert.Less(perto, pontos * 0.01f + 1, $"{d.Tamanho}, semente {d.Semente}: {perto} de {pontos} pontos em cima de outra estrada");
            }
        }

        [Test]
        public void AsPontasSaoNosEOsTrechosFecham()
        {
            foreach (var d in Todos())
            {
                var rede = d.Rede;
                foreach (var e in d.Plano.Estradas)
                {
                    Assert.GreaterOrEqual(rede.NoEm(e.Pontos[0]), 0);
                    Assert.GreaterOrEqual(rede.NoEm(e.Pontos[e.Pontos.Count - 1]), 0);
                    Assert.AreEqual(e.Pontos.Count, e.Alturas.Count);
                }
                float soma = 0f, total = 0f;
                foreach (var t in rede.Trechos) soma += t.Comprimento;
                foreach (var e in d.Plano.Estradas) total += e.Comprimento;
                Assert.AreEqual(total, soma, total * 0.001f, "os trechos cobrem as estradas inteiras");
                foreach (var no in rede.Nos)
                    foreach (int ti in no.Trechos)
                        Assert.IsTrue(rede.Trechos[ti].NoDe == rede.Nos.IndexOf(no) || rede.Trechos[ti].NoAte == rede.Nos.IndexOf(no));
            }
        }

        [Test]
        public void OChaoFicaEmbaixoDoAsfalto()
        {
            foreach (var d in Todos())
            {
                int meio = 0, beira = 0, n = 0;
                foreach (var e in d.Plano.Estradas)
                    for (int i = 1; i + 1 < e.Pontos.Count && !e.Conector; i++)
                    {
                        n++;
                        if (Mathf.Abs(d.Altura(e.Pontos[i].x, e.Pontos[i].y) - e.Alturas[i]) > 0.1f) meio++;
                        var dir = (e.Pontos[i + 1] - e.Pontos[i - 1]).normalized;
                        var esquerda = new Vector2(-dir.y, dir.x);
                        foreach (float s in new[] { -1f, 1f })
                        {
                            var q = e.Pontos[i] + esquerda * (s * RedeDeEstradas.MeiaLargura(e.Tipo));
                            if (d.Altura(q.x, q.y) > e.Alturas[i] + MalhasDasEstradas.Acima(e.Tipo)) beira++;
                        }
                    }
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                Assert.Less(meio, n * 0.005f + 1, $"{onde}: o chão longe do meio do asfalto em {meio} de {n} pontos");
                Assert.Less(beira, n * 2 * 0.015f + 1, $"{onde}: o chão por cima da beira do asfalto em {beira} de {n * 2} pontos");
            }
        }

        [Test]
        public void AsRampasDoAsfalto()
        {
            foreach (var d in Todos())
            {
                int fortes = 0, segmentos = 0;
                float maior = 0f;
                foreach (var e in d.Plano.Estradas)
                {
                    if (e.Tipo == TipoDeEstrada.Terra || e.Conector) continue;
                    for (int i = 1; i < e.Pontos.Count; i++)
                    {
                        float dl = Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
                        if (dl < 2f) continue;
                        float rampa = Mathf.Abs(e.Alturas[i] - e.Alturas[i - 1]) / dl;
                        segmentos++;
                        if (rampa > 0.15f) fortes++;
                        maior = Mathf.Max(maior, rampa);
                    }
                }
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                Assert.Less(maior, 0.3f, $"{onde}: uma rampa de {maior * 100f:F0}%");
                Assert.Less(fortes, segmentos * 0.01f + 1, $"{onde}: {fortes} de {segmentos} segmentos acima de 15%");
            }
        }

        /// <summary>
        /// Quem vai de carro de cada cidade (grande ou pequena) para cada outra só pelas placas: sai da primeira cidade (e de
        /// cada cidade do caminho) pela estrada certa (as placas dentro das cidades são da M5) e, em cada cruzamento na
        /// estrada, segue o destino se a placa mostra;
        /// se não, a cidade escrita mais perto dele (quem dirige tem o mapa, o M); sem placa, segue em frente. Devolve quantas
        /// viagens chegaram (e quantas só pelo nome do destino, sem precisar de outro lugar no caminho).
        /// </summary>
        public static int Viagens(DadosDoMundo d, out int pares, out int soPeloNome, out string primeiroErro)
        {
            var rede = d.Rede;
            var plano = d.Plano;
            primeiroErro = null;
            var cidades = new List<int>();
            for (int li = 0; li < plano.Lugares.Count; li++)
                if (plano.Lugares[li].Tipo == TipoDeLugar.CidadeGrande || plano.Lugares[li].Tipo == TipoDeLugar.CidadePequena) cidades.Add(li);
            int NoDe(int li)
            {
                for (int ni = 0; ni < rede.Nos.Count; ni++) if (rede.Nos[ni].Lugar == li) return ni;
                return -1;
            }
            pares = soPeloNome = 0;
            int certos = 0;
            foreach (int a in cidades)
            foreach (int b in cidades)
            {
                if (a == b) continue;
                int na = NoDe(a), nb = NoDe(b);
                if (na < 0 || nb < 0) continue;
                pares++;
                var cidadeA = (Cidade)plano.Lugares[a];
                var cidadeB = (Cidade)plano.Lugares[b];
                var dist = Distancias(rede, nb);
                int no = na, chegada = -1, passos = 0;
                bool chegou = false, peloNome = true;
                var visitados = new HashSet<long>();
                string motivo = "perdido";
                while (passos++ < 600)
                {
                    var p = rede.Nos[no].Posicao;
                    if (no == nb || cidadeB.Q(p.x, p.y) < 1.05f) { chegou = true; break; }
                    var trechos = rede.Nos[no].Trechos;
                    int escolhido = -1;
                    // dentro de uma cidade (a primeira ou uma do caminho), sai pela estrada certa: as placas das ruas são da M5
                    var cidadeAqui = plano.CidadeEm(p.x, p.y);
                    if (chegada < 0 || cidadeA.Q(p.x, p.y) < 1.05f || (cidadeAqui != null && cidadeAqui.Tipo != TipoDeLugar.Vila))
                    {
                        float menor = float.MaxValue;
                        foreach (int ti in trechos)
                        {
                            var t = rede.Trechos[ti];
                            int outro = t.NoDe == no ? t.NoAte : t.NoDe;
                            if (t.Comprimento + dist[outro] < menor) { menor = t.Comprimento + dist[outro]; escolhido = ti; }
                        }
                    }
                    else if (trechos.Count == 1) { motivo = $"beco em {p}"; break; }
                    else if (trechos.Count == 2) escolhido = trechos[0] == chegada ? trechos[1] : trechos[0];
                    else if (d.Objetos.Direcoes.TryGetValue(ObjetosDaEstrada.Chave(no, chegada), out var saidas))
                    {
                        // o destino; se não, a cidade escrita mais perto dele (uma vila ou um sítio no caminho, não)
                        float melhor = float.MaxValue;
                        foreach (var s in saidas)
                            foreach (int li in s.Lugares)
                            {
                                var tipo = plano.Lugares[li].Tipo;
                                if (li != b && tipo != TipoDeLugar.CidadeGrande && tipo != TipoDeLugar.CidadePequena) continue;
                                float dd = li == b ? -1f : Vector2.Distance(plano.Lugares[li].Centro, cidadeB.Centro);
                                if (dd < melhor) { melhor = dd; escolhido = s.Trecho; }
                            }
                        if (melhor >= 0f) peloNome = false;
                    }
                    if (escolhido < 0)
                    {
                        // sem placa: em frente (pelo que se vê no cruzamento, a estrada uns 20 m para dentro de cada saída)
                        var veio = -d.Objetos.Saida(chegada, no);
                        float melhor = float.MinValue;
                        foreach (int ti in trechos)
                        {
                            if (ti == chegada) continue;
                            float nota = Vector2.Dot(veio, d.Objetos.Saida(ti, no));
                            if (nota > melhor) { melhor = nota; escolhido = ti; }
                        }
                    }
                    if (escolhido < 0 || !visitados.Add(((long)no << 32) | (uint)escolhido)) { motivo = $"rodando em {p}"; break; }
                    var tm = rede.Trechos[escolhido];
                    chegada = escolhido;
                    no = tm.NoDe == no ? tm.NoAte : tm.NoDe;
                }
                if (chegou)
                {
                    certos++;
                    if (peloNome) soPeloNome++;
                }
                else primeiroErro ??= $"de {cidadeA.Nome} para {cidadeB.Nome}: {motivo}";
            }
            return certos;
        }

        static float[] Distancias(RedeDeEstradas rede, int origem)
        {
            var dist = new float[rede.Nos.Count];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.PositiveInfinity;
            dist[origem] = 0f;
            var feito = new bool[dist.Length];
            for (int it = 0; it < dist.Length; it++)
            {
                int u = -1;
                for (int i = 0; i < dist.Length; i++) if (!feito[i] && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || float.IsInfinity(dist[u])) break;
                feito[u] = true;
                foreach (int ti in rede.Nos[u].Trechos)
                {
                    var t = rede.Trechos[ti];
                    int v = t.NoDe == u ? t.NoAte : t.NoDe;
                    dist[v] = Mathf.Min(dist[v], dist[u] + t.Comprimento);
                }
            }
            return dist;
        }

        [Test]
        public void QuemSegueAsPlacasChegaNaCidade()
        {
            foreach (var d in Todos())
            {
                // nas sementes 1 a 10 e 12345 de cada tamanho (26/10/2026, com as cidades da M5): 66 de 66 no Pequeno, 300 de
                // 300 no Médio e 996 de 996 no Grande (no Médio, 254 só pelo nome do destino; no Grande, 549)
                int certos = Viagens(d, out int pares, out _, out string erro);
                Assert.Greater(pares, 0);
                Assert.AreEqual(pares, certos, $"{d.Tamanho}, semente {d.Semente}: {certos} de {pares} viagens pelas placas; {erro}");
            }
        }

        [Test]
        public void ABeiraDaEstradaFicaNoMapa()
        {
            foreach (var d in Todos())
            {
                var conta = new Dictionary<TipoDeObjeto, int>();
                foreach (var o in d.Objetos.Todos)
                {
                    Assert.IsTrue(d.DentroDaCerca(o.Posicao.x, o.Posicao.y, -4f), $"{o.Tipo} além da cerca em {o.Posicao}");
                    if (d.Lago != null && o.Tipo != TipoDeObjeto.Alambrado) Assert.Greater(d.Lago.Q(o.Posicao.x, o.Posicao.y), 1.05f, $"{o.Tipo} na água");
                    conta.TryGetValue(o.Tipo, out int n);
                    conta[o.Tipo] = n + 1;
                    if (o.Tipo == TipoDeObjeto.PlacaDeDirecao || o.Tipo == TipoDeObjeto.PlacaDeEntrada) Assert.Greater(o.Linhas.Count, 0);
                }
                string onde = $"{d.Tamanho}, semente {d.Semente}";
                Assert.AreEqual(d.Plano.Quantos(TipoDeLugar.Posto), conta.GetValueOrDefault(TipoDeObjeto.Posto), onde);
                Assert.Greater(conta.GetValueOrDefault(TipoDeObjeto.PlacaDeDirecao), 0, onde);
                Assert.GreaterOrEqual(conta.GetValueOrDefault(TipoDeObjeto.PlacaDeEntrada), d.Plano.Cidades.Count, onde);
                Assert.Greater(conta.GetValueOrDefault(TipoDeObjeto.Carro), 0, onde);
                Assert.Greater(conta.GetValueOrDefault(TipoDeObjeto.Conteiner), 0, $"{onde}: sem barreira da quarentena");
            }
        }

        [Test]
        public void AsCurvasNaoDobram()
        {
            // a faixa do asfalto dobra por cima dela mesma numa curva de raio menor que a meia largura (os retornos
            // apertados da serra, antes de a busca limitar a virada a 45°)
            foreach (var d in Todos())
                foreach (var e in d.Plano.Estradas)
                {
                    if (e.Tipo == TipoDeEstrada.Terra || e.Conector) continue;
                    for (int i = 1; i + 1 < e.Pontos.Count; i++)
                    {
                        var a = e.Pontos[i] - e.Pontos[i - 1];
                        var b = e.Pontos[i + 1] - e.Pontos[i];
                        if (a.magnitude < 3f || b.magnitude < 3f) continue;
                        float angulo = Vector2.Angle(a, b) * Mathf.Deg2Rad;
                        if (angulo < 1e-3f) continue;
                        float raio = Mathf.Min(a.magnitude, b.magnitude) / angulo;
                        Assert.Greater(raio, RedeDeEstradas.MeiaLargura(e.Tipo) * 1.5f, $"{d.Tamanho}, semente {d.Semente}: curva de {raio:F1} m em {e.Pontos[i]}");
                    }
                }
        }

        [Test]
        public void AsPlacasNaoFicamNaPista()
        {
            foreach (var d in Todos())
            {
                Assert.AreEqual(0, d.Objetos.PlacasSemLugar, $"{d.Tamanho}, semente {d.Semente}: placas sem lugar");
                foreach (var o in d.Objetos.Todos)
                {
                    if (o.Tipo != TipoDeObjeto.PlacaDeDirecao && o.Tipo != TipoDeObjeto.PlacaDeEntrada) continue;
                    float largura = o.Tipo == TipoDeObjeto.PlacaDeDirecao ? ModelosDaEstrada.LarguraDaPlacaDeDirecao(o.Linhas) : ModelosDaEstrada.LarguraDaPlacaDeEntrada(o.Linhas, o.Variante);
                    float g = o.Giro * Mathf.Deg2Rad;
                    var x = new Vector2(Mathf.Cos(g), -Mathf.Sin(g));   // o x da placa no plano (x, z)
                    for (int k = -2; k <= 2; k++)
                    {
                        var p = o.Posicao + x * (largura * 0.25f * k);
                        int ei = d.Rede.MaisPerto(p.x, p.y, out float dist, out _);
                        if (ei < 0) continue;
                        Assert.Greater(dist, RedeDeEstradas.MeiaLargura(d.Plano.Estradas[ei].Tipo) + 0.5f, $"{d.Tamanho}, semente {d.Semente}: {o.Tipo} em cima da estrada em {p}");
                    }
                }
            }
        }

        [Test]
        public void ACercaFechaOMapa()
        {
            foreach (var d in Todos())
            {
                // ao longo de cada lado, de metro em metro: um pedaço de alambrado ou um contêiner da barreira cobre
                for (int lado = 0; lado < 4; lado++)
                {
                    bool emX = lado < 2;
                    float sinal = (lado & 1) == 0 ? 1f : -1f;
                    var cobertos = new List<(float de, float ate)>();
                    foreach (var o in d.Objetos.Todos)
                    {
                        if (o.Tipo != TipoDeObjeto.Alambrado && !(o.Tipo == TipoDeObjeto.Conteiner && !o.EmCima)) continue;
                        float naLinha = emX ? o.Posicao.x * sinal : o.Posicao.y * sinal;
                        if (Mathf.Abs(naLinha - d.Cerca) > 1f) continue;
                        float ao = emX ? o.Posicao.y : o.Posicao.x;
                        float meia = o.Tipo == TipoDeObjeto.Alambrado ? 1.55f : 3.1f;
                        cobertos.Add((ao - meia, ao + meia));
                    }
                    cobertos.Sort((a, b) => a.de.CompareTo(b.de));
                    float ate = -d.Cerca + 0.5f;
                    foreach (var (de, fim) in cobertos)
                    {
                        Assert.LessOrEqual(de, ate + 0.2f, $"{d.Tamanho}, semente {d.Semente}: buraco na cerca no lado {lado}, de {ate:F1} a {de:F1}");
                        ate = Mathf.Max(ate, fim);
                    }
                    Assert.GreaterOrEqual(ate, d.Cerca - 0.5f, $"{d.Tamanho}, semente {d.Semente}: a cerca do lado {lado} acaba em {ate:F1}");
                }
            }
        }

        [Test]
        public void NadaFicaAlemDaCerca()
        {
            foreach (var d in Todos())
            {
                foreach (var l in d.Plano.Lugares)
                    if (l.Tipo != TipoDeLugar.Barreira)
                        Assert.IsTrue(d.DentroDaCerca(l.Centro.x, l.Centro.y, l.Raio), $"{d.Tamanho}, semente {d.Semente}: {l.Tipo} {l.Nome} além da cerca");
                // as estradas passam da cerca só nas saídas, e só um pouco (a barreira fica em cima da cerca)
                foreach (var e in d.Plano.Estradas)
                    foreach (var q in e.Pontos)
                        Assert.IsTrue(d.DentroDaCerca(q.x, q.y, -40f), $"{d.Tamanho}, semente {d.Semente}: estrada longe além da cerca em {q}");
            }
        }

        [Test]
        public void AsMalhasDasEstradasSaem()
        {
            var d = Mundo(TamanhoDoMundo.Medio, 7);
            int blocos = 0, triangulos = 0, comGuardRail = 0;
            for (int bz = 0; bz < d.BlocosPorLado; bz++)
            for (int bx = 0; bx < d.BlocosPorLado; bx++)
            {
                var m = MalhasDasEstradas.Bloco(d, bx, bz);
                if (m.Vazio) continue;
                blocos++;
                triangulos += m.Desenho.Triangulos;
                if (m.Colisao.Triangulos > 0) comGuardRail++;
            }
            Assert.Greater(blocos, 50);
            Assert.Greater(comGuardRail, 0, "nenhum guard-rail");
            Assert.Less(triangulos / blocos, 6000, "malha pesada demais por bloco");
        }
    }
}
