using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 2: o blockout), sem o Play: a planta fecha (os
    /// cômodos de cada andar cobrem o corpo sem se sobrepor, os anexos só no térreo, cada porta numa parede de verdade, as de
    /// fora numa parede de fora), o núcleo fica no mesmo lugar em todos os andares e a escada cabe na caixa; e, com a casca e
    /// os 4 andares montados num NavMesh só deles (o agente do jogo, o do zumbi), cada pedaço de chão (de 0,5 m) de cada
    /// cômodo tem caminho da praça da frente, entrando pela porta principal e subindo e descendo as escadas; os cômodos
    /// lacrados de propósito (a A-01, as salas técnicas dela e o quarto 4 do isolamento) ficam sem caminho nenhum.
    /// Nas falhas, o log mostra o mapa do cômodo (o = tem caminho, x = sem caminho, # = sem NavMesh).
    /// </summary>
    public class HospitalTestes
    {
        static readonly Medidas M = Medidas.DoJogo;
        static readonly Vector3 Longe = new Vector3(-20000f, 0f, -20000f);

        [Test]
        public void OsComodosCobremCadaAndarSemSeSobrepor()
        {
            float area = (X1 - X0) * (Z1 - Z0);
            var divisas = new List<Divisa>();
            var bordas = new List<Borda>();
            for (int f = 0; f < Andares; f++)
            {
                var an = DoAndar(f);
                string A = Codigos[f];
                float soma = an.comodos.Where(k => !Anexo(k)).Sum(k => (k.x1 - k.x0) * (k.z1 - k.z0));
                Assert.AreEqual(area, soma, 0.01f, $"{A}: os cômodos do corpo somam {soma:0.##} m², o corpo tem {area:0.##}");
                foreach (var k in an.comodos)
                {
                    if (Anexo(k)) Assert.AreEqual(Terreo, f, $"{A}: o anexo {an.nomes[k]} fora do térreo");
                    else Assert.IsTrue(k.x0 >= X0 - 0.01f && k.x1 <= X1 + 0.01f && k.z0 >= Z0 - 0.01f && k.z1 <= Z1 + 0.01f, $"{A}: {an.nomes[k]} sai do corpo");
                }
                for (int i = 0; i < an.comodos.Count; i++)
                    for (int j = i + 1; j < an.comodos.Count; j++)
                    {
                        Comodo a = an.comodos[i], b = an.comodos[j];
                        float sx = Mathf.Min(a.x1, b.x1) - Mathf.Max(a.x0, b.x0), sz = Mathf.Min(a.z1, b.z1) - Mathf.Max(a.z0, b.z0);
                        Assert.IsFalse(sx > 0.01f && sz > 0.01f, $"{A}: {an.nomes[a]} e {an.nomes[b]} se sobrepõem");
                    }
                Linhas(an, divisas, bordas);
                foreach (var p in an.portas)
                {
                    string nome = p.b == null ? $"a porta de fora de {an.nomes[p.a]}" : $"a porta entre {an.nomes[p.a]} e {an.nomes[p.b]}";
                    float meia = p.aberto ? 0f : (p.largura > 0f ? p.largura : p.folhas == 2 ? 2f : p.folhas == 0 ? M.passagem : M.porta) / 2f;
                    if (p.b == null)
                    {
                        bool vertical = p.lado == 1 || p.lado == 3;
                        int fora = p.lado == 0 || p.lado == 3 ? -1 : 1;
                        bool numaBorda = bordas.Any(b => b.k == p.a && b.vertical == vertical && b.fora == fora && p.pos - meia >= b.lo - 0.01f && p.pos + meia <= b.hi + 0.01f);
                        Assert.IsTrue(numaBorda, $"{A}: {nome} não fica numa parede de fora dele");
                        continue;
                    }
                    var d = divisas.Where(x => (x.a == p.a && x.b == p.b) || (x.a == p.b && x.b == p.a)).ToList();
                    Assert.IsNotEmpty(d, $"{A}: {nome} não tem parede em comum");
                    if (!p.aberto)
                        Assert.IsTrue(d.Any(x => p.pos - meia >= x.lo - 0.01f && p.pos + meia <= x.hi + 0.01f),
                                      $"{A}: {nome} não cabe na parede ({p.pos:0.##} ± {meia:0.##}, a parede de {d[0].lo:0.##} a {d[0].hi:0.##})");
                }
            }
        }

        [Test]
        public void ONucleoFicaNoMesmoLugarEmTodosOsAndares()
        {
            for (int f = 0; f < Andares; f++)
            {
                var an = DoAndar(f);
                foreach (var (nome, caixa, de, ate) in Escadas)
                {
                    var k = an.comodos.FirstOrDefault(c => c.tipo == "escada" && an.nomes[c] == nome);
                    if (f < de || f > ate) { Assert.IsNull(k, $"{Codigos[f]}: a {nome} não devia chegar aqui"); continue; }
                    Assert.IsNotNull(k, $"{Codigos[f]}: falta a {nome}");
                    Assert.IsTrue(Mathf.Abs(k.x0 - caixa.xMin) < 0.01f && Mathf.Abs(k.z0 - caixa.yMin) < 0.01f && Mathf.Abs(k.x1 - caixa.xMax) < 0.01f && Mathf.Abs(k.z1 - caixa.yMax) < 0.01f,
                                  $"{Codigos[f]}: a {nome} saiu do lugar");
                }
                var el = an.comodos.Single(c => c.tipo == "elevador");
                Assert.IsTrue(Mathf.Abs(el.x0 - Elevador.xMin) < 0.01f && Mathf.Abs(el.z0 - Elevador.yMin) < 0.01f, $"{Codigos[f]}: o elevador saiu do lugar");
            }
            // a escada cabe na caixa: o patamar do andar, o lance (11 espelhos de uns 18 cm) e o patamar do meio com 1 m ou mais
            foreach (var (nome, caixa, _, _) in Escadas)
            {
                int espelhos = Obra.Espelhos(EntreAndares / 2f);
                float lance = (espelhos - 1) * M.pisada, meio = Zf(caixa) - (ZL(caixa) + lance);
                Assert.Greater(meio, 1.0f, $"a {nome}: o patamar do meio tem só {meio:0.##} m");
                Assert.That(EntreAndares / 2f / espelhos, Is.InRange(0.16f, 0.19f), $"a {nome}: o degrau tem {EntreAndares / 2f / espelhos:0.###} m");
            }
        }

        /// <summary>
        /// Os documentos (a parte 6, a narrativa): os 20 papéis foram postos (nenhum cômodo ou móvel faltando), cada um num
        /// cômodo aberto, no andar dele, e todo papel tem o texto no Resources/Documentos/Hospital.txt (e todo texto, o papel).
        /// </summary>
        [Test]
        public void OsPapeisFicamNosComodosEComOTexto()
        {
            var papeis = DocumentosDoHospital.Papeis();
            Assert.IsEmpty(DocumentosDoHospital.Faltas, string.Join("\n", DocumentosDoHospital.Faltas));
            Assert.AreEqual(20, papeis.Count, "o número de papéis");
            Assert.AreEqual(papeis.Count, papeis.Select(p => p.id).Distinct().Count(), "os ids repetidos");
            foreach (var p in papeis)
            {
                var an = DoAndar(p.andar);
                var k = an.Em(p.lugar.x, p.lugar.z);
                Assert.IsNotNull(k, $"{p.id}: fora de todo cômodo");
                Assert.IsFalse(an.fechados.Contains(k), $"{p.id}: num cômodo fechado ({an.nomes[k]})");
                Assert.That(p.lugar.y - Base(p.andar), Is.InRange(0f, 2.2f), $"{p.id}: a altura");
            }
            string texto = System.IO.File.ReadAllText("Assets/_Game/Resources/Documentos/Hospital.txt");
            var ids = texto.Replace("\r\n", "\n").Split('\n').Where(l => l.StartsWith("=== ")).Select(l => l.Substring(4).Trim()).ToList();
            CollectionAssert.AreEquivalent(ids, papeis.Select(p => p.id).ToList(), "os ids do texto e dos papéis");
        }

        /// <summary>
        /// O Hospital Principal no mundo sorteado (a parte 7): nas sementes 1 a 10 do Médio e do Grande, a maior cidade grande
        /// tem o quarteirão dele (inteiro, com o terreno rígido dentro, de frente para uma rua), o lote é um só e o hospital
        /// sorteado de antes não aparece nela. O Pequeno só conta (pode não ter cidade grande que caiba).
        /// </summary>
        [Test]
        public void OHospitalTemQuarteiraoNosMapas()
        {
            var relato = new StringBuilder();
            int faltam = 0;
            foreach (var tamanho in new[] { TamanhoDoMundo.Pequeno, TamanhoDoMundo.Medio, TamanhoDoMundo.Grande })
            {
                int com = 0;
                var falhas = new List<int>();
                for (int semente = 1; semente <= 10; semente++)
                {
                    var d = GeradorDoMundo.SoOPlano(semente, tamanho);
                    var l = d.Plano.LoteDoHospital;
                    if (l == null)
                    {
                        falhas.Add(semente);
                        continue;
                    }
                    com++;
                    var t = d.Plano.TracadoDoHospital;
                    Assert.AreEqual(TipoDeLote.Hospital, l.Tipo);
                    Assert.AreEqual(1, t.Lotes.Count(x => x.Tipo == TipoDeLote.Hospital), $"{tamanho} {semente}: um lote só");
                    Assert.IsFalse(t.Lotes.Any(x => x.Predio != null && x.Predio.hospital), $"{tamanho} {semente}: o hospital sorteado ficou");
                    Assert.AreSame(d.Plano.CidadeDoHospital, t.Cidade);
                    // os cantos do terreno, rígidos, no quarteirão
                    var r = TerrenoNoMundo;
                    foreach (var p in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) })
                    {
                        var q = t.Local(t.NaObraDoHospital(l, p));
                        Assert.That(q.x, Is.InRange(l.U0, l.U1), $"{tamanho} {semente}: o canto {p} fora do quarteirão em u");
                        Assert.That(q.y, Is.InRange(l.V0, l.V1), $"{tamanho} {semente}: o canto {p} fora do quarteirão em v");
                    }
                    relato.AppendLine($"{tamanho} {semente}: {d.Plano.CidadeDoHospital.Nome}, quarteirão de {l.U1 - l.U0:F0} × {l.V1 - l.V0:F0} m, chão {l.ChaoDaCasa:F1} m");
                }
                if (tamanho != TamanhoDoMundo.Pequeno) faltam += falhas.Count;
                relato.AppendLine($"{tamanho}: {com} de 10 com o hospital" + (falhas.Count > 0 ? $" (sem: {string.Join(", ", falhas)})" : ""));
            }
            Debug.Log("[Hospital] " + relato);
            Assert.AreEqual(0, faltam, relato.ToString());
        }

        [Test]
        public void TodoComodoTemCaminhoDaEntrada() => Caminhos(false);

        /// <summary>
        /// O mesmo, como no mundo (a parte 7): com as portas que abrem fora da malha (as folhas são do jogo, e a fechada barra
        /// o zumbi pelo obstáculo do NavMesh), todas abertas: os mesmos cômodos têm caminho, e os lacrados continuam sem.
        /// </summary>
        [Test]
        public void TodoComodoTemCaminhoComAsPortasDoMundo() => Caminhos(true);

        static void Caminhos(bool vivas)
        {
            var malhas = new List<Mesh>();
            var casca = new Hospital(M, -1) { portasVivas = vivas };
            casca.MontarCasca();
            malhas.Add(casca.col.ParaMesh("casca"));
            MalhaDeObra.Devolver(casca.vis);
            MalhaDeObra.Devolver(casca.col);
            for (int f = 0; f < Andares; f++)
            {
                var a = new Hospital(M, f) { portasVivas = vivas };
                a.MontarAndar();
                malhas.Add(a.col.ParaMesh(Codigos[f]));
                MalhaDeObra.Devolver(a.vis);
                MalhaDeObra.Devolver(a.col);
            }
            var fontes = malhas.Select(m => new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh, sourceObject = m, transform = Matrix4x4.Translate(Longe), area = 0,
            }).ToList();
            // o chão em volta (o da cena, 45 cm abaixo do térreo: sobe-se pelos degraus e pelas rampas da casca): a praça da
            // frente, o pátio das ambulâncias, a saída do leste e o pátio de serviço
            void Chao(Rect r) => fontes.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box, size = new Vector3(r.width, 0.2f, r.height), area = 0,
                transform = Matrix4x4.Translate(new Vector3(r.center.x, ChaoDeFora - 0.1f, r.center.y) + Longe),
            });
            Chao(Rect.MinMaxRect(-12f, -40f, 12f, Entrada.yMin));
            Chao(Rect.MinMaxRect(-62f, -7f, GaragemDasAmbulancias.xMin - 0.1f, 10f));
            Chao(Rect.MinMaxRect(X1 + 0.1f, -9f, X1 + 3f, -4f));
            Chao(Rect.MinMaxRect(2f, Z1 + 0.1f, 16f, 24f));
            var limites = new Bounds(new Vector3(-17f, 3f, -8f) + Longe, new Vector3(100f, 24f, 70f));
            var dados = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fontes, limites, Vector3.zero, Quaternion.identity);
            var instancia = NavMesh.AddNavMeshData(dados);
            try
            {
                Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, ChaoDeFora + 0.05f, -32f) + Longe, out var inicio, 1.5f, NavMesh.AllAreas), "sem NavMesh na praça");
                var caminho = new NavMeshPath();
                bool Chega(Vector3 p) => NavMesh.CalculatePath(inicio.position, p, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete;
                var falhas = new List<string>();
                int pedacos = 0, comodos = 0, lacrados = 0;
                for (int f = 0; f < Andares; f++)
                {
                    var an = DoAndar(f);
                    float chao = Base(f);
                    foreach (var k in an.comodos)
                    {
                        if (DoNucleo(k)) continue;
                        bool lacrado = an.fechados.Contains(k);
                        if (lacrado) lacrados++; else comodos++;
                        int total = 0, sem = 0, chegam = 0;
                        var mapa = new StringBuilder();
                        for (float z = k.z1 - 0.4f; z >= k.z0 + 0.4f - 0.001f; z -= 0.5f)
                        {
                            for (float x = k.x0 + 0.4f; x <= k.x1 - 0.4f + 0.001f; x += 0.5f)
                            {
                                char c = '#';
                                var p = new Vector3(x, chao + 0.05f, z) + Longe;
                                if (NavMesh.SamplePosition(p, out var h, 0.2f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - (chao + Longe.y)) < 0.25f)
                                {
                                    total++;
                                    bool chega = Chega(h.position);
                                    if (chega) chegam++; else sem++;
                                    c = chega ? 'o' : 'x';
                                }
                                mapa.Append(c);
                            }
                            mapa.AppendLine();
                        }
                        pedacos += total;
                        string porque = null;
                        if (lacrado) { if (chegam > 0) porque = $"lacrado, mas {chegam} pedaços têm caminho"; }
                        else if (total == 0) porque = "sem chão com NavMesh";
                        else if (sem > 0) porque = $"{sem} de {total} pedaços sem caminho";
                        if (porque == null) continue;
                        falhas.Add($"{Codigos[f]}: {an.nomes[k]} ({porque})");
                        if (falhas.Count <= 8) Debug.Log($"[Hospital] {Codigos[f]}, {an.nomes[k]}: {porque}\n{mapa}");
                    }
                    // o patamar de cada escada neste andar
                    foreach (var (nome, caixa, de, ate) in Escadas)
                    {
                        if (f < de || f > ate) continue;
                        var patamar = new Vector3(caixa.center.x, chao + 0.05f, Zi(caixa) + 0.9f) + Longe;
                        if (!(NavMesh.SamplePosition(patamar, out var hp, 0.3f, NavMesh.AllAreas) && Chega(hp.position)))
                            falhas.Add($"{Codigos[f]}: o patamar da {nome}");
                    }
                }
                Debug.Log($"[Hospital] {comodos} cômodos abertos e {lacrados} lacrados, {pedacos} pedaços de chão; {falhas.Count} com problema");
                Assert.IsEmpty(falhas, $"{falhas.Count} lugares com problema:\n" + string.Join("\n", falhas));
            }
            finally
            {
                instancia.Remove();
                Object.DestroyImmediate(dados);
                foreach (var m in malhas) Object.DestroyImmediate(m);
            }
        }
    }
}
