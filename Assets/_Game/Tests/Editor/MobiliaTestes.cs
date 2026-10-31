using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// A mobília ("A mobília", primeira fatia): os modelos estão no Cidade_Mobilia.fbx (legíveis), todo ponto dos móveis
    /// das casas, das lojas, dos andares dos prédios e da loja do posto tem um modelo, os móveis entram na malha da mobília
    /// do bloco (e nos andares, no desenho deles) com uma caixa de colisão por móvel, sempre os mesmos para a mesma semente,
    /// e cada modelo cabe no ponto dele (com a folga do que sai para a frente: as cadeiras, as portas entreabertas).
    /// Os modelos ficam registrados só durante estes testes. A mobília nova (a etapa 4 do Plano_Mobilia.md da pasta de
    /// arte, nas casas e nos apartamentos): os de longe de cada casa numa malha, com uma caixa por móvel no chão na colisão.
    /// </summary>
    public class MobiliaTestes
    {
        const string Fbx = "Assets/_Game/Art/Cidade/Cidade_Mobilia.fbx";
        static DadosDoMundo _medio;
        static DadosDoMundo Medio => _medio ??= GeradorDoMundo.Gerar(7, TamanhoDoMundo.Medio);

        static Mesh[] Malhas() => AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().ToArray();

        [OneTimeSetUp]
        public void Registrar() => Mobilia.Registrar(Malhas());

        [OneTimeTearDown]
        public void Esquecer() => Mobilia.Limpar();

        // os blocos com casas, um em cada 'passo'
        static IEnumerable<Vector2Int> BlocosComCasas(DadosDoMundo d, int passo)
        {
            for (int bz = 0; bz < d.BlocosPorLado; bz += passo)
            for (int bx = 0; bx < d.BlocosPorLado; bx += passo)
                if (MalhasDasCasas.TemCasas(d, bx, bz)) yield return new Vector2Int(bx, bz);
        }

        [Test]
        public void OsModelosEstaoNoFbx()
        {
            var malhas = Malhas().ToDictionary(m => m.name);
            foreach (var nome in Mobilia.Feitos.Keys)
            {
                Assert.IsTrue(malhas.TryGetValue(nome, out var m), $"falta o modelo {nome}");
                Assert.IsTrue(m.isReadable, $"{nome} não está legível (a montagem copia a malha)");
                Assert.Greater(m.triangles.Length, 0, nome);
            }
            Assert.AreEqual(Mobilia.Feitos.Count, Mobilia.Quantos, "modelos que não foram registrados");
        }

        [Test]
        public void CadaModeloCabeNoPonto()
        {
            // o que sai do tamanho do catálogo: na frente (as cadeiras empurradas, as portas entreabertas, a gaveta puxada)
            // e em cima (a televisão, o abajur, a torneira, o espelho na parede), nunca atrás (a parede) nem para os lados
            var erros = new List<string>();
            foreach (var m in Malhas())
            {
                if (!Mobilia.Feitos.TryGetValue(m.name, out var tam)) continue;
                var b = m.bounds;
                if (b.min.y < -0.06f) erros.Add($"{m.name}: abaixo do chão ({b.min.y:0.00})");
                // (a frente do móvel é +z; as costas, na parede, -z)
                if (b.min.z < -tam.z / 2 - 0.06f) erros.Add($"{m.name}: passa da parede ({-tam.z / 2 - b.min.z:0.00} m)");
                if (b.min.x < -tam.x / 2 - 0.08f || b.max.x > tam.x / 2 + 0.08f) erros.Add($"{m.name}: passa dos lados ({b.size.x:0.00} m de {tam.x:0.00})");
                if (b.max.z > tam.z / 2 + 0.45f) erros.Add($"{m.name}: sai demais na frente ({b.max.z - tam.z / 2:0.00} m)");
                if (b.max.y > tam.y + 0.9f) erros.Add($"{m.name}: alto demais ({b.max.y:0.00} m de {tam.y:0.00})");
            }
            Assert.IsEmpty(erros, string.Join("\n", erros));
        }

        [Test]
        public void TodoPontoTemUmModelo()
        {
            var d = Medio;
            var semModelo = new Dictionary<string, int>();
            int pontos = 0;
            void Conferir(IEnumerable<PontoDeMovel> lista)
            {
                foreach (var p in lista)
                {
                    pontos++;
                    // (os da mobília nova são os modelos dela)
                    string nome = Mobilia.ModeloDo(p, 0);
                    if (MobiliaNova.EhNovo(p.tipo)) continue;
                    if (nome == null || !Mobilia.Feitos.ContainsKey(nome)) semModelo[p.tipo] = semModelo.TryGetValue(p.tipo, out int n) ? n + 1 : 1;
                }
            }
            foreach (var k in BlocosComCasas(d, 3))
            {
                var r = MalhasDasCasas.Bloco(d, k.x, k.y);
                foreach (var c in r.Casas) Conferir(c.Moveis);
                foreach (var pr in r.Predios)
                {
                    // os andares de cada prédio (o hospital e os apartamentos)
                    for (int f = 0; f < pr.Planta.andares; f += 3)
                    {
                        var a = MalhasDasCasas.MontarAndar(pr.Planta, f);
                        Conferir(a.Moveis);
                        MalhasDasCasas.Devolver(a);
                    }
                }
                MalhasDasCasas.Devolver(r);
            }
            Conferir(ModelosDaEstrada.Posto(1).Moveis);
            Debug.Log($"Médio 7: {pontos} pontos conferidos");
            Assert.Greater(pontos, 3000, "poucos pontos conferidos");
            Assert.IsEmpty(semModelo, "tipos sem modelo: " + string.Join(", ", semModelo.Select(k => $"{k.Key} ({k.Value})")));
        }

        [Test]
        public void OsMoveisEntramNoDesenhoENaColisao()
        {
            var d = Medio;
            MalhasDasCasas.DoBloco com = null;
            var k = Vector2Int.zero;
            foreach (var b in BlocosComCasas(d, 2))
            {
                var r = MalhasDasCasas.Bloco(d, b.x, b.y);
                if (r.Casas.Count >= 3 && r.Casas.Any(c => c.Moveis.Any(p => Mobilia.ModeloDo(p, 0) != null)))
                {
                    com = r;
                    k = b;
                    break;
                }
                MalhasDasCasas.Devolver(r);
            }
            Assert.IsNotNull(com, "nenhum bloco com 3 casas");
            Assert.IsNotNull(com.Mobilia, "o bloco sem a malha da mobília");
            // (os da mobília antiga: as lojas e as lavanderias)
            int pontos = com.Casas.Sum(c => c.Moveis.Count(p => Mobilia.ModeloDo(p, 0) != null));
            Assert.Greater(com.Mobilia.Triangulos, pontos * 30, "poucos triângulos de mobília");
            var colisoes = com.Casas.Select(c => c.Colisao.Triangulos).ToArray();
            // sem os modelos, a mesma casa: a colisão tem 12 triângulos a menos por móvel
            Mobilia.Limpar();
            var sem = MalhasDasCasas.Bloco(d, k.x, k.y);
            Mobilia.Registrar(Malhas());
            Assert.IsNull(sem.Mobilia);
            for (int i = 0; i < com.Casas.Count; i++)
            {
                int moveis = com.Casas[i].Moveis.Count(p => Mobilia.ModeloDo(p, 0) != null);
                Assert.AreEqual(sem.Casas[i].Colisao.Triangulos + moveis * 12, colisoes[i], $"{com.Casas[i].Nome}: a colisão sem as caixas dos móveis");
            }
            MalhasDasCasas.Devolver(com);
            MalhasDasCasas.Devolver(sem);
        }

        [Test]
        public void AMobiliaNovaEntraNoDesenhoENaColisao()
        {
            var longes = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/MobiliaNova" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Select(p =>
                {
                    var l = p.transform.Find(p.name + "_Longe");
                    return (p.name, (l != null ? l.GetComponent<MeshFilter>() : p.GetComponent<MeshFilter>()).sharedMesh);
                }).ToList();
            Assert.Greater(longes.Count, 80, "os prefabs da mobília nova (Mobília nova > Montar)");
            MobiliaNova.Registrar(longes);
            try
            {
                var d = Medio;
                var k = BlocosComCasas(d, 2).First(b => d.Plano != null && MalhasDasCasas.TemCasas(d, b.x, b.y));
                var com = MalhasDasCasas.Bloco(d, k.x, k.y);
                MobiliaNova.Limpar();
                var sem = MalhasDasCasas.Bloco(d, k.x, k.y);
                var pecas = new List<MobiliaNova.Peca>();
                int comNovos = 0;
                for (int i = 0; i < com.Casas.Count; i++)
                {
                    int caixas = 0, novos = 0;
                    foreach (var p in com.Casas[i].Moveis.Where(p => MobiliaNova.EhNovo(p.tipo)))
                    {
                        novos++;
                        pecas.Clear();
                        MobiliaNova.Pecas(p, 0, pecas);
                        caixas += pecas.Where(q => q.caixa).Sum(q => MobiliaNova.Camadas(q.tamanho.y));
                    }
                    Assert.AreEqual(sem.Casas[i].Colisao.Triangulos + caixas * 12, com.Casas[i].Colisao.Triangulos, $"{com.Casas[i].Nome}: as caixas dos móveis novos");
                    Assert.IsNull(sem.Casas[i].MobiliaNova);
                    if (novos == 0) continue;
                    comNovos++;
                    Assert.IsNotNull(com.Casas[i].MobiliaNova, $"{com.Casas[i].Nome}: sem os de longe");
                    Assert.Greater(com.Casas[i].MobiliaNova.Triangulos, novos * 10, $"{com.Casas[i].Nome}: poucos triângulos de longe");
                }
                Assert.Greater(comNovos, 0, "nenhuma casa com os móveis novos");
                MalhasDasCasas.Devolver(com);
                MalhasDasCasas.Devolver(sem);
            }
            finally
            {
                MobiliaNova.Limpar();
            }
        }

        [Test]
        public void AMesmaSementeDaOsMesmosMoveis()
        {
            var d = Medio;
            var k = BlocosComCasas(d, 4).First();
            var a = MalhasDasCasas.Bloco(d, k.x, k.y);
            int ta = a.Mobilia?.Triangulos ?? 0;
            var ma = a.Mobilia?.ParaMesh("a");
            MalhasDasCasas.Devolver(a);
            var b = MalhasDasCasas.Bloco(d, k.x, k.y);
            var mb = b.Mobilia?.ParaMesh("b");
            Assert.AreEqual(ta, b.Mobilia?.Triangulos ?? 0);
            MalhasDasCasas.Devolver(b);
            if (ma == null) return;
            CollectionAssert.AreEqual(ma.vertices, mb.vertices, "os móveis mudaram de uma montagem para a outra");
            Object.DestroyImmediate(ma);
            Object.DestroyImmediate(mb);
        }
    }
}
