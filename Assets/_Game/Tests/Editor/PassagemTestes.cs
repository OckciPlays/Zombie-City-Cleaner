using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// A passagem pelas construções com os móveis ("A mobília"), sem o Play: cada casa, loja e andar de prédio do Médio 7
    /// é montado como no jogo, com as caixas de colisão dos móveis, e ganha um NavMesh só dele, com o agente do jogo (o do
    /// zumbi, 0,35 m de raio). Em cada cômodo, cada pedaço de chão com NavMesh (numa grade de 0,5 m) precisa ter caminho da
    /// rua (nos prédios, do corredor do andar): um pedaço sem caminho é um canto que os móveis fecharam (o NavMesh tira as
    /// ilhas menores que 2 m², mas não todas: um canto de 0,15 m² entre uma poltrona e um rack ficava). O mesmo NavMesh sem os móveis separa o que já era assim antes deles (contado à parte, não
    /// falha). Com a mobília nova (a etapa 4 do Plano_Mobilia.md da pasta de arte), as caixas dela também. É a parte "o zumbi chega" das conferências do F1 (no Play, até uma hora para uma amostra) em uns 4 minutos
    /// para o mapa todo; as do Play continuam para o jogador andando, as escadas dos prédios e o mundo carregando.
    /// Nas falhas, o log mostra o mapa do NavMesh do cômodo (o = tem caminho, x = sem caminho, # = sem NavMesh, M = móvel).
    /// </summary>
    public class PassagemTestes
    {
        static readonly Medidas M = Medidas.DoJogo;
        // longe de qualquer NavMesh da cena aberta
        static readonly Vector3 Longe = new Vector3(20000f, 0f, 20000f);
        const float Passo = 0.5f, Margem = 0.3f;

        [OneTimeSetUp]
        public void Registrar() => Mobilia.Registrar(AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Cidade/Cidade_Mobilia.fbx").OfType<Mesh>());

        [OneTimeTearDown]
        public void Esquecer() => Mobilia.Limpar();

        struct Alvo
        {
            public string Nome;
            public Comodo K;
            public float Chao;
        }

        sealed class Conta
        {
            public int Construcoes, Comodos, Pedacos, JaAntes;
            public double Ms;
            public readonly List<string> Falhas = new List<string>();
        }

        // uns 4 minutos (o NavMesh de cada construção, duas vezes)
        [Test, Timeout(600000)]
        public void OsMoveisNaoFechamCaminhoNasConstrucoes()
        {
            var d = GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);
            var conta = new Conta();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa != null) ConferirCasa(l.Casa, $"casa em {t.Cidade.Nome} ({l.Casa.molde}{(l.Casa.porao ? ", porão" : "")})", conta);
                    else if (l.Loja != null) ConferirLoja(l.Loja, $"{PlantaDeLoja.Nome(l.Loja.tipo)} em {t.Cidade.Nome}", conta);
                    else if (l.Predio != null) ConferirPredio(l.Predio, $"{(l.Predio.hospital ? "hospital" : "prédio")} de {l.Predio.andares} andares em {t.Cidade.Nome}", conta);
                }
            Debug.Log($"[Passagem] Médio 7: {conta.Construcoes} construções (casas, lojas e andares de prédio), {conta.Comodos} cômodos, " +
                      $"{conta.Pedacos} pedaços de chão em {conta.Ms / 1000:0.0} s; {conta.Falhas.Count} cômodos fechados pelos móveis, " +
                      $"{conta.JaAntes} que já eram assim sem eles");
            Assert.Greater(conta.Construcoes, 1000, "poucas construções conferidas");
            Assert.IsEmpty(conta.Falhas, $"{conta.Falhas.Count} cômodos com chão sem caminho por causa dos móveis:\n" + string.Join("\n", conta.Falhas.Take(25)));
        }

        // ------------------------------------------------------------------ as construções

        static void ConferirCasa(Planta p, string nome, Conta conta)
        {
            var casa = new Casa(p, M);
            casa.Montar();
            var alvos = p.comodos.Select(k => new Alvo
            {
                Nome = k.tipo, K = k,
                Chao = k.andar >= 0 ? Casa.PisoDe(p) + k.andar * (M.altura + M.laje) : Casa.PisoDe(p) - M.laje - M.alturaPorao,
            }).ToList();
            // o chão em volta (sem os buracos do porão e do fosso dele) e a rua na frente
            var chao = Chao(Rect.MinMaxRect(p.X0 - 6f, p.Z0 - 6f, p.X1 + 6f, p.Z1 + 6f), Casa.BuracosDa(p, M));
            Conferir(casa, casa.moveis, p.semente, chao, new Vector3(0f, 0.05f, p.Z0 - 4f), alvos, -4f, 10f, nome, conta);
        }

        static void ConferirLoja(PlantaDeLoja p, string nome, Conta conta)
        {
            var loja = new Loja(p, M);
            loja.Montar((x, z) => 0f, 0f);
            var alvos = p.comodos.Where(k => k.tipo != "escada").Select(k => new Alvo { Nome = k.tipo + (k.andar > 0 ? $" (andar {k.andar})" : ""), K = k, Chao = p.Base(k.andar) }).ToList();
            var chao = Chao(Rect.MinMaxRect(p.X0 - 6f, p.Z0 - 6f, p.X1 + 6f, p.Z1 + 6f), null);
            Conferir(loja, loja.moveis, p.semente, chao, new Vector3(0f, 0.05f, p.Z0 - 4f), alvos, -1f, p.Cobertura + 1f, nome, conta);
        }

        static void ConferirPredio(PlantaDePredio p, string nome, Conta conta)
        {
            // a casca (as lajes, a fachada e a escada) uma vez; cada andar conferido do corredor dele, e o térreo da rua (as
            // lojas do térreo abrem para ela; a escada entre os andares não muda com os móveis: é da conferência do Play)
            var casca = new Predio(p, M, -1);
            casca.MontarCasca((x, z) => 0f, 0f);
            var malhaDaCasca = casca.col.ParaMesh("casca");
            MalhaDeObra.Devolver(casca.vis);
            MalhaDeObra.Devolver(casca.col);
            foreach (var e in casca.Escuros) MalhaDeObra.Devolver(e);
            foreach (int f in new[] { 0, 1, p.andares / 2, p.andares - 1 }.Distinct())
            {
                var andar = new Predio(p, M, f);
                andar.MontarAndar();
                float baseF = p.Base(f);
                var corredor = andar.Comodos.FirstOrDefault(k => k.tipo == "corredor");
                foreach (var e in andar.Escuros) MalhaDeObra.Devolver(e);
                if (corredor == null && f > 0)
                {
                    MalhaDeObra.Devolver(andar.vis);
                    MalhaDeObra.Devolver(andar.col);
                    continue;
                }
                var de = f == 0 ? new Vector3(0f, 0.05f, p.Z0 - 4f) : new Vector3(corredor.Cx, baseF + 0.05f, corredor.Cz);
                var alvos = andar.Comodos.Where(k => k.tipo != "escada").Select(k => new Alvo { Nome = k.tipo, K = k, Chao = baseF }).ToList();
                var extra = new List<NavMeshBuildSource> { Fonte(malhaDaCasca) };
                extra.AddRange(Chao(Rect.MinMaxRect(p.X0 - 6f, p.Z0 - 6f, p.X1 + 6f, p.Z1 + 6f), null));
                Conferir(andar, andar.Moveis, p.semente * 31 + f, extra, de, alvos, Mathf.Min(0f, baseF) - 0.6f, baseF + 2.6f, $"{nome}, andar {f}", conta);
            }
            Object.DestroyImmediate(malhaDaCasca);
        }

        // ------------------------------------------------------------------ a conferência de uma obra

        // a obra já montada (sem os móveis na colisão): o NavMesh sem e com as caixas dos móveis, e cada cômodo
        static void Conferir(Obra obra, List<PontoDeMovel> moveis, int semente, List<NavMeshBuildSource> extra, Vector3 de, List<Alvo> alvos,
                             float y0, float y1, string nome, Conta conta)
        {
            double t0 = EditorApplication.timeSinceStartup;
            var sem = obra.col.ParaMesh("sem os móveis");
            Mobilia.Por(moveis, null, obra.col, semente);
            // e as da mobília nova (as casas e os apartamentos; aqui sem espelhar, como os pontos)
            MobiliaNova.Caixas(moveis, obra.col);
            var com = obra.col.ParaMesh("com os móveis");
            var b = com.bounds;
            var limites = new Bounds(new Vector3(b.center.x, (y0 + y1) / 2, b.center.z) + Longe, new Vector3(b.size.x + 12f, y1 - y0, b.size.z + 12f));
            var antes = Medir(sem, extra, limites, de, alvos, null);
            var depois = Medir(com, extra, limites, de, alvos, moveis);
            conta.Construcoes++;
            for (int i = 0; i < alvos.Count; i++)
            {
                conta.Comodos++;
                conta.Pedacos += depois[i].total;
                if (depois[i].sem == 0 && (depois[i].total > 0 || antes[i].total == 0)) continue;
                // o que já era assim sem os móveis não é da mobília
                if (antes[i].sem > 0 || antes[i].total == 0)
                {
                    conta.JaAntes++;
                    continue;
                }
                string porque = depois[i].total == 0 ? "sem chão com NavMesh" : $"{depois[i].sem} de {depois[i].total} pedaços sem caminho";
                conta.Falhas.Add($"{nome}: {alvos[i].Nome} ({porque})");
                if (conta.Falhas.Count <= 6) Debug.Log($"[Passagem] {nome}: {alvos[i].Nome}, {porque}\n{depois[i].mapa}");
            }
            Object.DestroyImmediate(sem);
            Object.DestroyImmediate(com);
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            conta.Ms += (EditorApplication.timeSinceStartup - t0) * 1000;
        }

        // o NavMesh da colisão (e do chão), e em cada cômodo os pedaços de chão com NavMesh e quantos não têm caminho de 'de'
        static List<(int total, int sem, string mapa)> Medir(Mesh colisao, List<NavMeshBuildSource> extra, Bounds limites, Vector3 de,
                                                            List<Alvo> alvos, List<PontoDeMovel> moveis)
        {
            var fontes = new List<NavMeshBuildSource>(extra) { Fonte(colisao) };
            var dados = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fontes, limites, Vector3.zero, Quaternion.identity);
            var instancia = NavMesh.AddNavMeshData(dados);
            var r = new List<(int, int, string)>();
            var caminho = new NavMeshPath();
            bool temInicio = NavMesh.SamplePosition(de + Longe, out var inicio, 1.5f, NavMesh.AllAreas);
            foreach (var a in alvos)
            {
                int total = 0, sem = 0;
                var mapa = moveis != null ? new StringBuilder() : null;
                var k = a.K;
                float x0 = Mathf.Min(k.x0, k.x1), x1 = Mathf.Max(k.x0, k.x1);
                for (float z = k.z1 - Margem; z >= k.z0 + Margem - 0.001f; z -= Passo)
                {
                    for (float x = x0 + Margem; x <= x1 - Margem + 0.001f; x += Passo)
                    {
                        char c = '#';
                        var p = new Vector3(x, a.Chao + 0.05f, z) + Longe;
                        if (NavMesh.SamplePosition(p, out var h, 0.2f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - (a.Chao + Longe.y)) < 0.25f)
                        {
                            total++;
                            bool chega = temInicio && NavMesh.CalculatePath(inicio.position, h.position, NavMesh.AllAreas, caminho) &&
                                         caminho.status == NavMeshPathStatus.PathComplete;
                            if (!chega) sem++;
                            c = chega ? 'o' : 'x';
                        }
                        if (mapa != null && moveis.Any(m => Mathf.Abs(m.centro.y - a.Chao) < 0.3f && m.Pe.Contains(new Vector2(x, z)))) c = c == '#' ? 'M' : c;
                        mapa?.Append(c);
                    }
                    mapa?.AppendLine();
                }
                r.Add((total, sem, mapa?.ToString()));
            }
            instancia.Remove();
            Object.DestroyImmediate(dados);
            return r;
        }

        static NavMeshBuildSource Fonte(Mesh m) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Mesh, sourceObject = m, transform = Matrix4x4.Translate(Longe), area = 0,
        };

        // o chão em volta da obra (y = 0), em faixas, sem os buracos
        static List<NavMeshBuildSource> Chao(Rect area, List<Rect> buracos)
        {
            var r = new List<NavMeshBuildSource>();
            void Faixa(float xa, float xb, float za, float zb)
            {
                if (xb - xa < 0.01f || zb - za < 0.01f) return;
                r.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box, size = new Vector3(xb - xa, 0.2f, zb - za), area = 0,
                    transform = Matrix4x4.Translate(new Vector3((xa + xb) / 2, -0.1f, (za + zb) / 2) + Longe),
                });
            }
            if (buracos == null || buracos.Count == 0)
            {
                Faixa(area.xMin, area.xMax, area.yMin, area.yMax);
                return r;
            }
            var cortes = new List<float> { area.yMin, area.yMax };
            foreach (var b in buracos) { cortes.Add(Mathf.Clamp(b.yMin, area.yMin, area.yMax)); cortes.Add(Mathf.Clamp(b.yMax, area.yMin, area.yMax)); }
            cortes.Sort();
            for (int i = 0; i + 1 < cortes.Count; i++)
            {
                float za = cortes[i], zb = cortes[i + 1], zm = (za + zb) / 2;
                float x = area.xMin;
                foreach (var b in buracos.Where(b => zm > b.yMin && zm < b.yMax).OrderBy(b => b.xMin))
                {
                    Faixa(x, b.xMin, za, zb);
                    x = Mathf.Max(x, b.xMax);
                }
                Faixa(x, area.xMax, za, zb);
            }
            return r;
        }
    }
}
