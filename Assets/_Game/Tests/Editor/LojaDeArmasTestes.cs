using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;
using Andar = ZombieCityCleaner.Mundo.PlantaDoHospital.Andar;
using Porta = ZombieCityCleaner.Mundo.PlantaDoHospital.Porta;
using Divisa = ZombieCityCleaner.Mundo.PlantaDoHospital.Divisa;
using Borda = ZombieCityCleaner.Mundo.PlantaDoHospital.Borda;
using TipoDePorta = ZombieCityCleaner.Mundo.PlantaDoHospital.TipoDePorta;
using Zona = ZombieCityCleaner.Mundo.PlantaDoHospital.Zona;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// A loja de armas (o Plano_LojaDeArmas.md da pasta de arte), sem o Play: a planta fecha (os cômodos cobrem o corpo sem
    /// se sobrepor, cada porta numa parede de verdade, as de fora numa parede de fora); os móveis são todos da mobília nova;
    /// os papéis ficam nos cômodos, com o texto; e, nos três estados da sala segura (com a casca, o lado de dentro e as
    /// caixas dos móveis num NavMesh só deles, o agente do zumbi), cada pedaço de chão (de 0,5 m) de cada cômodo tem caminho
    /// do estacionamento, entrando pela porta da frente; na sala segura trancada (o estado C), nenhum.
    /// Nas falhas, o log mostra o mapa do cômodo (o = tem caminho, x = sem caminho, # = sem NavMesh).
    /// </summary>
    public class LojaDeArmasTestes
    {
        static readonly Medidas M = Medidas.DoJogo;
        static readonly Vector3 Longe = new Vector3(-21000f, 0f, -21000f);

        [Test]
        public void OsComodosCobremOCorpoSemSeSobrepor()
        {
            var an = Planta();
            float area = (X1 - X0) * (Z1 - Z0);
            float soma = an.comodos.Sum(k => (k.x1 - k.x0) * (k.z1 - k.z0));
            Assert.AreEqual(area, soma, 0.01f, $"os cômodos somam {soma:0.##} m², o corpo tem {area:0.##}");
            for (int i = 0; i < an.comodos.Count; i++)
                for (int j = i + 1; j < an.comodos.Count; j++)
                {
                    Comodo a = an.comodos[i], b = an.comodos[j];
                    float sx = Mathf.Min(a.x1, b.x1) - Mathf.Max(a.x0, b.x0), sz = Mathf.Min(a.z1, b.z1) - Mathf.Max(a.z0, b.z0);
                    Assert.IsFalse(sx > 0.01f && sz > 0.01f, $"{an.nomes[a]} e {an.nomes[b]} se sobrepõem");
                }
            var divisas = new List<Divisa>();
            var bordas = new List<Borda>();
            PlantaDoHospital.Linhas(an, divisas, bordas);
            foreach (var p in an.portas)
            {
                float meia = (p.largura > 0f ? p.largura : p.folhas == 2 ? 2f : M.porta) / 2f;
                if (p.b == null)
                {
                    bool vertical = p.lado == 1 || p.lado == 3;
                    int fora = p.lado == 0 || p.lado == 3 ? -1 : 1;
                    Assert.IsTrue(bordas.Any(b => b.k == p.a && b.vertical == vertical && b.fora == fora && p.pos - meia >= b.lo - 0.01f && p.pos + meia <= b.hi + 0.01f),
                                  $"a porta de fora de {an.nomes[p.a]} não fica numa parede de fora dele");
                    continue;
                }
                var d = divisas.Where(x => (x.a == p.a && x.b == p.b) || (x.a == p.b && x.b == p.a)).ToList();
                Assert.IsNotEmpty(d, $"a porta entre {an.nomes[p.a]} e {an.nomes[p.b]} não tem parede em comum");
                Assert.IsTrue(d.Any(x => p.pos - meia >= x.lo - 0.01f && p.pos + meia <= x.hi + 0.01f), $"a porta entre {an.nomes[p.a]} e {an.nomes[p.b]} não cabe na parede");
            }
        }

        [Test]
        public void OsMoveisEOsPapeisExistem()
        {
            foreach (var s in new[] { EstadoDaSala.Saqueada, EstadoDaSala.Parcial, EstadoDaSala.Intacta })
                for (int semente = 1; semente <= 6; semente++)
                {
                    var v = Sortear(semente, s);
                    v.acampamento = semente % 2 == 0;
                    var moveis = MoveisDaLojaDeArmas.Da(v);
                    Assert.IsEmpty(MoveisDaLojaDeArmas.Faltam, $"{s} {semente}: faltam os modelos {string.Join(", ", MoveisDaLojaDeArmas.Faltam)}");
                    var papeis = DocumentosDaLojaDeArmas.Papeis(v);
                    Assert.IsEmpty(DocumentosDaLojaDeArmas.Faltas, string.Join("\n", DocumentosDaLojaDeArmas.Faltas));
                    foreach (var p in papeis)
                    {
                        bool naFrente = p.id == "fechamento";
                        Assert.IsTrue(naFrente || Onde(p.lugar) != null, $"{s} {semente}: {p.id} fora da loja ({p.lugar})");
                        Assert.That(p.lugar.y, Is.InRange(0f, 2.3f), $"{p.id}: a altura");
                    }
                    Assert.AreEqual(s == EstadoDaSala.Intacta, papeis.Any(p => p.id == "bilhete_dono"), "o bilhete do dono só na intacta");
                    Assert.AreEqual(v.acampamento, papeis.Any(p => p.id == "diario_sobrevivente"), "o diário só com o acampamento");
                }
            string texto = System.IO.File.ReadAllText("Assets/_Game/Resources/Documentos/LojaDeArmas.txt");
            var ids = texto.Replace("\r\n", "\n").Split('\n').Where(l => l.StartsWith("=== ")).Select(l => l.Substring(4).Trim()).ToList();
            CollectionAssert.AreEquivalent(ids, DocumentosDaLojaDeArmas.Todos, "os ids do texto e dos papéis");
        }

        [Test]
        public void TodoComodoTemCaminhoNaSalaSaqueada() => Caminhos(EstadoDaSala.Saqueada, false);

        [Test]
        public void TodoComodoTemCaminhoNaSalaParcial() => Caminhos(EstadoDaSala.Parcial, true);

        [Test]
        public void ASalaIntactaFicaFechada() => Caminhos(EstadoDaSala.Intacta, true);

        static void Caminhos(EstadoDaSala sala, bool acampamento)
        {
            var malhas = new List<Mesh>();
            var relato = new StringBuilder();
            foreach (int semente in new[] { 3, 8 })
            {
                var v = Sortear(semente, sala);
                v.acampamento = acampamento;
                v.barricada = true;
                v.gondolaTombada = semente == 3;
                foreach (var parte in new[] { LojaDeArmas.Parte.Casca, LojaDeArmas.Parte.Dentro })
                {
                    var l = new LojaDeArmas(M, parte, v);
                    l.Montar();
                    malhas.Add(l.col.ParaMesh(parte.ToString()));
                    MalhaDeObra.Devolver(l.vis);
                    MalhaDeObra.Devolver(l.col);
                }
                var fontes = malhas.Select(m => new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh, sourceObject = m, transform = Matrix4x4.Translate(Longe), area = 0,
                }).ToList();
                void Chao(Rect r, float topo) => fontes.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box, size = new Vector3(r.width, 0.2f, r.height), area = 0,
                    transform = Matrix4x4.Translate(new Vector3(r.center.x, topo - 0.1f, r.center.y) + Longe),
                });
                // a calçada da loja (no nível da porta) e o estacionamento na frente
                Chao(Rect.MinMaxRect(X0 - 4f, Z0 - Calcada, X1 + 4f, Z0 - 0.11f), 0f);
                Chao(Rect.MinMaxRect(X0 - 4f, Z0 - Calcada - 12f, X1 + 4f, Z0 - Calcada), ChaoDeFora);
                var limites = new Bounds(new Vector3(0f, 2f, -4f) + Longe, new Vector3(40f, 10f, 50f));
                var dados = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fontes, limites, Vector3.zero, Quaternion.identity);
                var instancia = NavMesh.AddNavMeshData(dados);
                try
                {
                    Assert.IsTrue(NavMesh.SamplePosition(new Vector3(MeioDaEntrada, ChaoDeFora + 0.05f, Z0 - 9f) + Longe, out var inicio, 1.5f, NavMesh.AllAreas), "sem NavMesh no estacionamento");
                    var caminho = new NavMeshPath();
                    bool Chega(Vector3 p) => NavMesh.CalculatePath(inicio.position, p, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete;
                    var falhas = new List<string>();
                    int pedacos = 0;
                    foreach (var k in PlantaPronta.comodos)
                    {
                        bool lacrado = k.tipo == "segura" && sala == EstadoDaSala.Intacta;
                        int total = 0, sem = 0, chegam = 0;
                        var mapa = new StringBuilder();
                        for (float z = k.z1 - 0.4f; z >= k.z0 + 0.4f - 0.001f; z -= 0.5f)
                        {
                            for (float x = k.x0 + 0.4f; x <= k.x1 - 0.4f + 0.001f; x += 0.5f)
                            {
                                char c = '#';
                                if (NavMesh.SamplePosition(new Vector3(x, 0.05f, z) + Longe, out var h, 0.2f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - Longe.y) < 0.25f)
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
                        if (lacrado) { if (chegam > 0) porque = $"lacrada, mas {chegam} pedaços têm caminho"; }
                        else if (total == 0) porque = "sem chão com NavMesh";
                        else if (sem > 0) porque = $"{sem} de {total} pedaços sem caminho";
                        if (porque == null) continue;
                        falhas.Add($"semente {semente}: {PlantaPronta.nomes[k]} ({porque})");
                        Debug.Log($"[Loja de armas] {sala}, semente {semente}, {PlantaPronta.nomes[k]}: {porque}\n{mapa}");
                    }
                    relato.AppendLine($"{sala}, semente {semente}: {pedacos} pedaços de chão, {falhas.Count} cômodos com problema");
                    Assert.IsEmpty(falhas, $"{falhas.Count} cômodos com problema:\n" + string.Join("\n", falhas));
                }
                finally
                {
                    instancia.Remove();
                    Object.DestroyImmediate(dados);
                    foreach (var m in malhas) Object.DestroyImmediate(m);
                    malhas.Clear();
                }
            }
            Debug.Log("[Loja de armas] " + relato);
        }
    }
}
