using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O gerador do mapa (Plano_Mapa, Partes M1 e M2): a mesma semente gera os mesmos números (em qualquer thread),
    /// sementes diferentes geram mundos diferentes, e o sorteio, o ruído, os tamanhos, o relevo, o lago, a partida, os
    /// buracos e as malhas dos blocos fazem o que prometem. Gerar o mapa grande leva ~1 s: os testes de regra usam os
    /// mundos guardados em <see cref="Guardados"/>.
    /// </summary>
    public class GeradorDoMundoTestes
    {
        const int Semente = 12345;
        static readonly Dictionary<TamanhoDoMundo, DadosDoMundo> Guardados = new Dictionary<TamanhoDoMundo, DadosDoMundo>();

        static DadosDoMundo Mundo(TamanhoDoMundo t)
        {
            if (!Guardados.TryGetValue(t, out var d)) Guardados[t] = d = GeradorDoMundo.Gerar(Semente, t);
            return d;
        }

        [OneTimeTearDown]
        public void Limpar() => Guardados.Clear();

        [Test]
        public void SorteioDaMesmaSementeRepete()
        {
            var a = Sorteio.Da(42, "estradas");
            var b = Sorteio.Da(42, "estradas");
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.Proximo(), b.Proximo());
        }

        [Test]
        public void CadaEtapaTemOSeuSorteio()
        {
            var a = Sorteio.Da(42, "estradas");
            var b = Sorteio.Da(42, "cidades");
            var c = Sorteio.Da(43, "estradas");
            int iguaisAB = 0, iguaisAC = 0;
            for (int i = 0; i < 100; i++)
            {
                ulong va = a.Proximo();
                if (va == b.Proximo()) iguaisAB++;
                if (va == c.Proximo()) iguaisAC++;
            }
            Assert.AreEqual(0, iguaisAB);
            Assert.AreEqual(0, iguaisAC);
        }

        [Test]
        public void SorteioFicaNosLimites()
        {
            var s = Sorteio.Da(7, "limites");
            for (int i = 0; i < 10000; i++)
            {
                float f = s.Fracao();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int n = s.Entre(-3, 5);
                Assert.That(n, Is.GreaterThanOrEqualTo(-3).And.LessThan(5));
                float r = s.Entre(10f, 20f);
                Assert.That(r, Is.GreaterThanOrEqualTo(10f).And.LessThanOrEqualTo(20f));
            }
            Assert.AreEqual(4, s.Entre(4, 4));
            Assert.AreEqual(Sorteio.Hash(1, 10, -20, 3), Sorteio.Hash(1, 10, -20, 3));
            Assert.AreNotEqual(Sorteio.Hash(1, 10, -20, 3), Sorteio.Hash(1, -20, 10, 3));
        }

        [Test]
        public void RuidoRepeteEFicaNaFaixa()
        {
            var a = new Ruido(5, "teste");
            var b = new Ruido(5, "teste");
            var c = new Ruido(6, "teste");
            int diferentes = 0;
            for (int i = 0; i < 2000; i++)
            {
                float x = i * 0.37f - 300f, y = i * 0.11f + 50f;
                float va = a.Fbm(x, y, 4);
                Assert.AreEqual(va, b.Fbm(x, y, 4));
                Assert.That(va, Is.InRange(-1.01f, 1.01f));
                Assert.That(a.Amostra01(x, y), Is.InRange(0f, 1f));
                if (Mathf.Abs(va - c.Fbm(x, y, 4)) > 1e-4f) diferentes++;
            }
            Assert.Greater(diferentes, 1500);
        }

        [Test]
        public void OsTamanhosEAGrade()
        {
            Assert.AreEqual(3000, Tamanhos.Lado(TamanhoDoMundo.Pequeno));
            Assert.AreEqual(5000, Tamanhos.Lado(TamanhoDoMundo.Medio));
            Assert.AreEqual(8000, Tamanhos.Lado(TamanhoDoMundo.Grande));
            foreach (var t in Tamanhos.Todos)
            {
                var d = Mundo(t);
                Assert.AreEqual(Tamanhos.Lado(t), d.Lado);
                // a grade cobre o mapa e a margem (as serras passam da borda)
                Assert.GreaterOrEqual(d.MetadeDaGrade, d.Metade + DadosDoMundo.Margem);
                Assert.Less(d.MetadeDaGrade, d.Metade + DadosDoMundo.Margem + DadosDoMundo.TamanhoDoBloco);
                Assert.AreEqual(d.BlocosPorLado * DadosDoMundo.CelulasPorBloco, d.Celulas);
                Assert.AreEqual(d.Celulas + 1, d.Vertices);
                Assert.AreEqual(d.Vertices * d.Vertices, d.Alturas.Length);
                Assert.AreEqual(Vector2Int.zero, d.BlocoDe(-d.MetadeDaGrade, -d.MetadeDaGrade));
                Assert.AreEqual(new Vector2Int(d.BlocosPorLado - 1, d.BlocosPorLado - 1), d.BlocoDe(d.MetadeDaGrade - 0.01f, d.MetadeDaGrade - 0.01f));
            }
            Assert.IsTrue(Tamanhos.Ler("médio", out var medio) && medio == TamanhoDoMundo.Medio);
            Assert.IsTrue(Tamanhos.Ler("3", out var pequeno) && pequeno == TamanhoDoMundo.Pequeno);
            Assert.IsFalse(Tamanhos.Ler("enorme", out _));
        }

        [Test]
        public void MesmaSementeGeraOMesmoMundo()
        {
            var a = Mundo(TamanhoDoMundo.Medio);
            var b = GeradorDoMundo.Gerar(Semente, TamanhoDoMundo.Medio);
            Assert.AreEqual(a.Resumo, b.Resumo);
            Assert.AreEqual(a.Partida, b.Partida);
            Assert.AreEqual(a.Lago.Centro, b.Lago.Centro);
            Assert.AreEqual(a.Lago.Nivel, b.Lago.Nivel);
            for (int k = 0; k < a.Alturas.Length; k += 997) Assert.AreEqual(a.Alturas[k], b.Alturas[k]);
            for (int i = 0; i < DadosDoMundo.Jogadores; i++) Assert.AreEqual(a.Nascimentos[i], b.Nascimentos[i]);
        }

        [Test]
        public void GerarEmOutraThreadDaOMesmoMundo()
        {
            var aqui = Mundo(TamanhoDoMundo.Pequeno);
            var tarefas = new Task<DadosDoMundo>[3];
            for (int i = 0; i < tarefas.Length; i++) tarefas[i] = Task.Run(() => GeradorDoMundo.Gerar(Semente, TamanhoDoMundo.Pequeno));
            Task.WaitAll(tarefas);
            foreach (var t in tarefas) Assert.AreEqual(aqui.Resumo, t.Result.Resumo);
        }

        [Test]
        public void SementesDiferentesGeramMundosDiferentes()
        {
            var resumos = new HashSet<ulong>();
            var partidas = new HashSet<Vector3>();
            for (int s = 1; s <= 12; s++)
            {
                var d = GeradorDoMundo.Gerar(s * 7919, TamanhoDoMundo.Pequeno);
                resumos.Add(d.Resumo);
                partidas.Add(d.Partida);
            }
            Assert.AreEqual(12, resumos.Count);
            Assert.AreEqual(12, partidas.Count);
            // o mesmo número em tamanhos diferentes também é outro mundo
            Assert.AreNotEqual(Mundo(TamanhoDoMundo.Pequeno).Resumo, Mundo(TamanhoDoMundo.Medio).Resumo);
        }

        [Test]
        public void ARelevoTemColinasESerras()
        {
            var d = Mundo(TamanhoDoMundo.Grande);
            float minimo = float.MaxValue, maximo = float.MinValue, somaMeio = 0f, somaBorda = 0f;
            int nMeio = 0, nBorda = 0;
            for (int j = 0; j < d.Vertices; j += 8)
            for (int i = 0; i < d.Vertices; i += 8)
            {
                float h = d.AlturaDoVertice(i, j);
                Assert.IsFalse(float.IsNaN(h));
                minimo = Mathf.Min(minimo, h);
                maximo = Mathf.Max(maximo, h);
                var p = d.PosicaoDoVertice(i, j);
                float fora = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                if (fora < d.Metade * 0.5f) { somaMeio += h; nMeio++; }
                else if (fora > d.Metade - 60f && fora < d.Metade) { somaBorda += h; nBorda++; }
            }
            // nada abaixo dos -20 m em que o jogador volta à partida, e as serras bem acima do meio do mapa
            Assert.Greater(minimo, -12f);
            Assert.Less(maximo, GeradorDoMundo.Base + 50f + d.AlturaDaSerra * 1.1f);
            Assert.Greater(somaBorda / nBorda, somaMeio / nMeio + d.AlturaDaSerra * 0.5f);
            // as colinas variam: o meio do mapa não é plano
            float varia = 0f;
            for (int k = 1; k < 200; k++) varia = Mathf.Max(varia, Mathf.Abs(d.Altura(k * 13f, 0f) - d.Altura((k - 1) * 13f, 0f)));
            Assert.Greater(varia, 0.3f);
        }

        [Test]
        public void OLagoFicaNaBaciaComAIlha()
        {
            foreach (var t in Tamanhos.Todos)
            {
                var d = Mundo(t);
                var lago = d.Lago;
                Assert.IsNotNull(lago);
                // a água abaixo de toda a faixa da margem e o fundo bem abaixo da água
                for (int a = 0; a < 72; a++)
                {
                    float ang = a * Mathf.PI * 2f / 72f;
                    float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                    float r = lago.RaioNaDirecao(cx, cz);
                    foreach (float q in new[] { 1.05f, 1.2f, 1.5f, 1.65f })
                        Assert.Greater(d.Altura(lago.Centro.x + cx * r * q, lago.Centro.y + cz * r * q), lago.Nivel - 0.05f, $"{t}: margem a {q} na direção {a}");
                    Assert.Less(d.Altura(lago.Centro.x + cx * r * 0.7f, lago.Centro.y + cz * r * 0.7f), lago.Nivel - 1.5f, $"{t}: fundo na direção {a}");
                }
                // a ilha do meio sai da água
                Assert.Greater(d.Altura(lago.Centro.x, lago.Centro.y), lago.Nivel + 1f);
                Assert.Greater(d.FunduraDaAgua(lago.Centro.x + lago.Raio * 0.6f, lago.Centro.y), 1.5f);
                Assert.AreEqual(0f, d.FunduraDaAgua(d.Partida.x, d.Partida.z));
            }
        }

        [Test]
        public void APartidaFicaNoLoteDaFamilia()
        {
            foreach (var t in Tamanhos.Todos)
            {
                var d = Mundo(t);
                // a partida é a casa da família, num bairro de casas: dentro do mapa, fora da serra e longe da água
                Assert.IsTrue(d.Dentro(d.Partida.x, d.Partida.z, 100f), $"{t}: a partida em {d.Partida}");
                Assert.Less(d.Borda(d.Partida.x, d.Partida.z), 0.05f, $"{t}: a partida na serra");
                Assert.Greater(d.Lago.Q(d.Partida.x, d.Partida.z), 1.4f, $"{t}: a partida no lago");
                // na casa da família (Parte M6), sem platô: a casa no lote dela, os 4 na sala (em cima do piso, longe das
                // paredes e um do outro) e o carro no chão da garagem ou da vaga
                Assert.IsFalse(d.ComPlato, $"{t}: a partida fora de uma cidade");
                var lote = d.Plano.LoteDaFamilia;
                Assert.IsNotNull(lote?.Casa, $"{t}: sem a casa da família");
                var tracado = d.Plano.CidadeDaFamilia.Tracado;
                var r = lote.RetDaCasa;
                Assert.IsTrue(r.xMin >= lote.U0 - 0.01f && r.xMax <= lote.U1 + 0.01f && r.yMin >= lote.V0 - 0.01f && r.yMax <= lote.V1 + 0.01f, $"{t}: a casa fora do lote");
                var paraACasa = tracado.MatrizDaCasa(lote).inverse;
                var sala = lote.Casa.comodos.Find(k => k.tipo == "sala");
                float piso = lote.ChaoDaCasa + Casa.PisoDe(lote.Casa);
                for (int i = 0; i < DadosDoMundo.Jogadores; i++)
                {
                    var p = d.Nascimentos[i];
                    var q = paraACasa.MultiplyPoint3x4(p);
                    float x = lote.Casa.espelhar ? -q.x : q.x;
                    Assert.IsTrue(x > sala.x0 + 0.5f && x < sala.x1 - 0.5f && q.z > sala.z0 + 0.5f && q.z < sala.z1 - 0.5f, $"{t}: o jogador {i} fora da sala");
                    Assert.AreEqual(piso + 0.05f, p.y, 0.01f);
                    for (int j = 0; j < i; j++) Assert.Greater(Vector3.Distance(p, d.Nascimentos[j]), 1.5f);
                }
                Assert.IsTrue(d.LugarDoCarro.HasValue, $"{t}: sem o lugar do carro");
                Assert.AreEqual(lote.ChaoDaCasa, d.LugarDoCarro.Value.y, 0.01f);
            }
        }

        [Test]
        public void OChaoTemAsQuatroTexturas()
        {
            var d = Mundo(TamanhoDoMundo.Grande);
            int[] fortes = new int[4];
            for (int j = 0; j < d.Vertices; j += 37)
            for (int i = 0; i < d.Vertices; i += 37)
            {
                var c = d.PesosDoChao(i, j);
                if (c.r > 200) fortes[0]++;
                if (c.g > 200) fortes[1]++;
                if (c.b > 200) fortes[2]++;
                if (c.a > 200) fortes[3]++;
            }
            for (int k = 0; k < 4; k++) Assert.Greater(fortes[k], 50, $"textura {k}");
        }

        // um bloco do meio do mapa sem cidade (sem os buracos dos porões das casas, Parte M6)
        static Vector2Int BlocoSemCidade(DadosDoMundo d)
        {
            int meio = d.BlocosPorLado / 2;
            for (int r = 0; r < meio; r++)
            for (int bz = meio - r; bz <= meio + r; bz++)
            for (int bx = meio - r; bx <= meio + r; bx++)
                if (d.CidadesDoBloco(bx, bz) == null) return new Vector2Int(bx, bz);
            return new Vector2Int(meio, meio);
        }

        [Test]
        public void AMalhaDoBlocoBateComAAltura()
        {
            var d = Mundo(TamanhoDoMundo.Pequeno);
            var b = BlocoSemCidade(d);
            var m = MalhasDoTerreno.Terreno(d, b.x, b.y, 1);
            int quads = DadosDoMundo.CelulasPorBloco * DadosDoMundo.CelulasPorBloco;
            Assert.AreEqual(quads * 6 + DadosDoMundo.CelulasPorBloco * 4 * 12, m.Vertices.Length);
            // o meio de cada triângulo de cima está na superfície que a consulta de altura dá
            for (int t = 0; t < quads * 6; t += 3)
            {
                var c = (m.Vertices[t] + m.Vertices[t + 1] + m.Vertices[t + 2]) / 3f;
                Assert.AreEqual(c.y, d.Altura(c.x, c.z), 0.002f);
                Assert.Greater(Vector3.Cross(m.Vertices[t + 1] - m.Vertices[t], m.Vertices[t + 2] - m.Vertices[t]).y, 0f, "a frente para cima");
            }
            // menos detalhe longe: um quarto e um dezesseis avos dos quadrados
            Assert.AreEqual(quads / 4 * 6 + DadosDoMundo.CelulasPorBloco / 2 * 4 * 12, MalhasDoTerreno.Terreno(d, b.x, b.y, 2).Vertices.Length);
            Assert.AreEqual(quads / 16 * 6 + DadosDoMundo.CelulasPorBloco / 4 * 4 * 12, MalhasDoTerreno.Terreno(d, b.x, b.y, 4).Vertices.Length);
        }

        [Test]
        public void OsBuracosTiramAsCelulasDaMalha()
        {
            var d = GeradorDoMundo.Gerar(99, TamanhoDoMundo.Pequeno);
            var b = BlocoSemCidade(d);
            var canto = d.CantoDoBloco(b.x, b.y);
            var cheio = MalhasDoTerreno.Terreno(d, b.x, b.y, 1);
            // um porão de 6 × 4 m: 3 × 2 células de 2 m (o mapa já tem os das casas)
            int antes = d.QuantosBuracos;
            d.MarcarBuraco(canto.x + 10f, canto.y + 10f, canto.x + 16f, canto.y + 14f);
            Assert.AreEqual(antes + 6, d.QuantosBuracos);
            var furado = MalhasDoTerreno.Terreno(d, b.x, b.y, 1);
            Assert.AreEqual(cheio.Vertices.Length - 6 * 6, furado.Vertices.Length);
            Assert.IsTrue(d.Buraco(b.x * DadosDoMundo.CelulasPorBloco + 5, b.y * DadosDoMundo.CelulasPorBloco + 5));
            Assert.IsFalse(d.Buraco(b.x * DadosDoMundo.CelulasPorBloco + 8, b.y * DadosDoMundo.CelulasPorBloco + 5));
            // os níveis de longe não furam
            Assert.AreEqual(MalhasDoTerreno.Terreno(d, b.x, b.y, 2).Vertices.Length, quadsDoPasso(2));

            int quadsDoPasso(int passo)
            {
                int n = DadosDoMundo.CelulasPorBloco / passo;
                return n * n * 6 + n * 4 * 12;
            }
        }

        [Test]
        public void AAguaSoNoLago()
        {
            var d = Mundo(TamanhoDoMundo.Pequeno);
            var noLago = d.BlocoDe(d.Lago.Centro.x + d.Lago.Raio * 0.5f, d.Lago.Centro.y);
            var agua = MalhasDoTerreno.Agua(d, noLago.x, noLago.y);
            Assert.IsFalse(agua.Vazia);
            foreach (var v in agua.Vertices) Assert.AreEqual(d.Lago.Nivel, v.y);
            var naPartida = d.BlocoDe(d.Partida.x, d.Partida.z);
            Assert.IsTrue(MalhasDoTerreno.Agua(d, naPartida.x, naPartida.y).Vazia);
        }
    }
}
