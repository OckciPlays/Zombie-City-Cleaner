using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O bunker novo (o Plano_Bunker.md da pasta de arte, a parte 2: a casca), sem o Play: a planta fecha (os cômodos de
    /// cada andar cobrem a caixa sem se sobrepor, as portas ficam numa parede de verdade) e, com a casca e os 5 andares
    /// montados e um NavMesh só deles (o agente do jogo, o do zumbi), cada pedaço de chão de cada cômodo (numa grade de
    /// 0,5 m) tem caminho do pátio, entrando pelo portão aberto e descendo a escada; e a casinha no alto do morro também.
    /// Nas falhas, o log mostra o mapa do cômodo (o = tem caminho, x = sem caminho, # = sem NavMesh).
    /// </summary>
    public class BunkerTestes
    {
        static readonly Medidas M = Medidas.DoJogo;
        static readonly Vector3 Longe = new Vector3(-20000f, 0f, 20000f);

        [Test]
        public void OsComodosCobremCadaAndarSemSeSobrepor()
        {
            float area = (X1 - X0) * (Z1 - Z0);
            for (int f = 0; f < Andares; f++)
            {
                var an = DoAndar(f);
                float soma = an.comodos.Sum(k => (k.x1 - k.x0) * (k.z1 - k.z0));
                Assert.AreEqual(area, soma, 0.01f, $"{Codigos[f]}: os cômodos somam {soma:0.##} m², a caixa tem {area:0.##}");
                for (int i = 0; i < an.comodos.Count; i++)
                    for (int j = i + 1; j < an.comodos.Count; j++)
                    {
                        Comodo a = an.comodos[i], b = an.comodos[j];
                        float sx = Mathf.Min(a.x1, b.x1) - Mathf.Max(a.x0, b.x0), sz = Mathf.Min(a.z1, b.z1) - Mathf.Max(a.z0, b.z0);
                        Assert.IsFalse(sx > 0.01f && sz > 0.01f, $"{Codigos[f]}: {an.nomes[a]} e {an.nomes[b]} se sobrepõem");
                    }
                foreach (var l in an.ligacoes)
                {
                    Comodo a = l.a, b = l.b;
                    bool vertical = Mathf.Abs(a.x1 - b.x0) < 0.01f || Mathf.Abs(b.x1 - a.x0) < 0.01f;
                    bool horizontal = Mathf.Abs(a.z1 - b.z0) < 0.01f || Mathf.Abs(b.z1 - a.z0) < 0.01f;
                    Assert.IsTrue(vertical || horizontal, $"{Codigos[f]}: a porta entre {an.nomes[a]} e {an.nomes[b]} não tem parede em comum");
                    float lo = vertical ? Mathf.Max(a.z0, b.z0) : Mathf.Max(a.x0, b.x0), hi = vertical ? Mathf.Min(a.z1, b.z1) : Mathf.Min(a.x1, b.x1);
                    Assert.IsTrue(l.pos > lo && l.pos < hi, $"{Codigos[f]}: a porta entre {an.nomes[a]} e {an.nomes[b]} fica fora da parede ({l.pos:0.##} fora de {lo:0.##} a {hi:0.##})");
                }
            }
        }

        [Test]
        public void TodoComodoTemCaminhoDoPatio()
        {
            var malhas = new List<Mesh>();
            var casca = new Bunker(M, -1);
            casca.MontarCasca();
            malhas.Add(casca.col.ParaMesh("casca"));
            MalhaDeObra.Devolver(casca.vis);
            MalhaDeObra.Devolver(casca.col);
            for (int f = 0; f < Andares; f++)
            {
                var a = new Bunker(M, f);
                a.MontarAndar();
                malhas.Add(a.col.ParaMesh(Codigos[f]));
                MalhaDeObra.Devolver(a.vis);
                MalhaDeObra.Devolver(a.col);
            }
            var fontes = malhas.Select(m => new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh, sourceObject = m, transform = Matrix4x4.Translate(Longe), area = 0,
            }).ToList();
            // o pátio na frente do portão (o chão do teste; o morro é da cena)
            fontes.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box, size = new Vector3(30f, 0.2f, 30f), area = 0,
                transform = Matrix4x4.Translate(new Vector3(4f, -0.1f, Z0 - 0.2f - 15f) + Longe),
            });
            var limites = new Bounds(new Vector3(0f, -4.5f, -6f) + Longe, new Vector3(40f, 28f, 50f));
            var dados = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fontes, limites, Vector3.zero, Quaternion.identity);
            var instancia = NavMesh.AddNavMeshData(dados);
            try
            {
                Assert.IsTrue(NavMesh.SamplePosition(new Vector3(4f, 0.05f, -24f) + Longe, out var inicio, 1.5f, NavMesh.AllAreas), "sem NavMesh no pátio");
                var caminho = new NavMeshPath();
                bool Chega(Vector3 p) => NavMesh.CalculatePath(inicio.position, p, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete;
                var falhas = new List<string>();
                int pedacos = 0, comodos = 0;
                for (int f = 0; f < Andares; f++)
                {
                    var an = DoAndar(f);
                    float chao = Base(f);
                    foreach (var k in an.comodos)
                    {
                        if (DoNucleo(k)) continue;
                        comodos++;
                        int total = 0, sem = 0;
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
                                    if (!chega) sem++;
                                    c = chega ? 'o' : 'x';
                                }
                                mapa.Append(c);
                            }
                            mapa.AppendLine();
                        }
                        pedacos += total;
                        if (total == 0 || sem > 0)
                        {
                            string porque = total == 0 ? "sem chão com NavMesh" : $"{sem} de {total} pedaços sem caminho";
                            falhas.Add($"{Codigos[f]}: {an.nomes[k]} ({porque})");
                            if (falhas.Count <= 6) Debug.Log($"[Bunker] {Codigos[f]}, {an.nomes[k]}: {porque}\n{mapa}");
                        }
                    }
                }
                // a casinha: o patamar de cima da escada e o alto do morro do lado de fora da porta (a soleira)
                var patamar = new Vector3((FimDoPatamar + EscadaXf) / 2, Morro + 0.05f, PortaDaEscadaZ) + Longe;
                bool casinha = NavMesh.SamplePosition(patamar, out var hc, 0.3f, NavMesh.AllAreas) && Chega(hc.position);
                if (!casinha) falhas.Add("a casinha (o patamar de cima da escada)");
                Debug.Log($"[Bunker] {comodos} cômodos, {pedacos} pedaços de chão; {falhas.Count} com problema");
                Assert.IsEmpty(falhas, $"{falhas.Count} lugares sem caminho do pátio:\n" + string.Join("\n", falhas));
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
