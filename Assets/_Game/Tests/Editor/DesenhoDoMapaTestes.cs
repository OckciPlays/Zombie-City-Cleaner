using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O desenho do mapa fixo (Plano_Mapa_Fixo; desde 29/10/2026, o mapa limpo, que cresce aos poucos): a quarentena fecha o
    /// mapa jogável do tamanho certo, o que já está no mapa (as cidades, os lugares e a casa da família, quando entrarem)
    /// fica dentro dela, os nomes não repetem, as estradas se ligam (nas pontas: outra estrada, uma cidade, um lugar, um
    /// bloqueio ou a borda), os rios chegam num lago, noutro rio, no brejo ou na borda, as pontes acham o cruzamento delas, o relevo
    /// tem as alturas do plano, as estradas principais têm a rampa de uma estrada e a pintura sai igual duas vezes. O
    /// relevo e o chão são feitos uma vez só.
    /// </summary>
    public class DesenhoDoMapaTestes
    {
        static DesenhoDoMapa _d;
        static RelevoDoDesenho _relevo;
        static ChaoDoMapaFixo _chao;

        static DesenhoDoMapa Desenho()
        {
            if (_d == null) _d = AssetDatabase.LoadAssetAtPath<DesenhoDoMapa>(DesenhoDoMapa.Caminho);
            Assert.IsNotNull(_d, $"o desenho em {DesenhoDoMapa.Caminho}");
            return _d;
        }

        static RelevoDoDesenho Relevo() => _relevo ??= new RelevoDoDesenho(Desenho());

        static ChaoDoMapaFixo Chao() => _chao ??= new ChaoDoMapaFixo(Desenho(), Relevo());

        static List<Vector2> Quarentena() => DesenhoDoMapa.Contorno(Desenho().quarentena);

        [Test]
        public void AQuarentenaFechaOMapaJogavel()
        {
            var d = Desenho();
            Assert.GreaterOrEqual(d.quarentena.Count, 8);
            foreach (var p in Quarentena())
                Assert.IsTrue(Mathf.Abs(p.x) < DesenhoDoMapa.Metade && Mathf.Abs(p.y) < DesenhoDoMapa.Metade, $"a quarentena sai do mapa em {p}");
            float km2 = Linhas.Area(Quarentena()) / 1e6f;
            Assert.That(km2, Is.InRange(100f, 180f), "a área jogável (decisão 1: uns 141 km²; a do mapa limpo, uns 151)");
            Assert.IsNotEmpty(d.bloqueios, "os bloqueios onde as estradas saem");
            foreach (var b in d.bloqueios)
                Assert.Less(Linhas.Distancia(b.ponto, Quarentena(), true), 150f, $"o bloqueio {b.nome} fica na linha da quarentena");
        }

        [Test]
        public void OQueEstaNoMapaFicaDentroDaQuarentena()
        {
            var d = Desenho();
            var q = Quarentena();
            foreach (var c in d.cidades)
            {
                Assert.IsTrue(Linhas.Dentro(c.centro, q), $"{c.nome} fora da quarentena");
                Assert.IsNotEmpty(c.distritos, $"{c.nome} sem distrito");
                foreach (var dis in c.distritos)
                {
                    Assert.GreaterOrEqual(dis.contorno.Count, 3, $"um distrito de {c.nome} sem contorno");
                    Assert.IsTrue(Linhas.Dentro(Linhas.Centro(DesenhoDoMapa.Contorno(dis.contorno)), q), $"o distrito {dis.nome} de {c.nome} fora da quarentena");
                }
            }
            foreach (var l in d.lugares) Assert.IsTrue(Linhas.Dentro(l.ponto, q), $"{l.nome} fora da quarentena");
            // a casa da família, quando as cidades entrarem no mapa limpo (decisão 9: em Maple Grove, dentro de um distrito)
            if (d.cidades.Count == 0) return;
            Assert.IsTrue(Linhas.Dentro(d.casaDaFamilia, q), "a casa da família fora da quarentena");
            bool numaCidade = false;
            foreach (var c in d.cidades)
            foreach (var dis in c.distritos)
                numaCidade |= Linhas.Dentro(d.casaDaFamilia, DesenhoDoMapa.Contorno(dis.contorno));
            Assert.IsTrue(numaCidade, "a casa da família fica dentro de um distrito");
        }

        [Test]
        public void OsNomesNaoSeRepetem()
        {
            var d = Desenho();
            var vistos = new HashSet<string>();
            void Ver(string nome, string o)
            {
                if (string.IsNullOrEmpty(nome)) return;
                Assert.IsTrue(vistos.Add(nome), $"o nome {nome} ({o}) repete");
            }
            foreach (var c in d.cidades) Ver(c.nome, "cidade");
            foreach (var l in d.lugares) Ver(l.nome, "lugar");
            foreach (var l in d.lagos) Ver(l.nome, "lago");
            foreach (var r in d.rios) Ver(r.nome, "rio");
            foreach (var e in d.estradas) Ver(e.nome, "via");
            foreach (var p in d.relevo.picos) Ver(p.nome, "pico");
            foreach (var c in d.cidades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(c.nome), "uma cidade sem nome");
                var avenidas = new HashSet<string>();
                foreach (var a in c.avenidas) Assert.IsTrue(avenidas.Add(a.nome), $"a avenida {a.nome} repete em {c.nome}");
            }
        }

        [Test]
        public void AsEstradasSeLigam()
        {
            var d = Desenho();
            Assert.IsNotEmpty(d.estradas, "as estradas principais");
            var linhas = new List<List<Vector2>>();
            foreach (var e in d.estradas) linhas.Add(DesenhoDoMapa.Linha(e.pontos));
            var distritos = new List<List<Vector2>>();
            foreach (var c in d.cidades)
            foreach (var dis in c.distritos) distritos.Add(DesenhoDoMapa.Contorno(dis.contorno));

            bool Ligada(Vector2 p, int propria)
            {
                // a que sai do mapa (pelo horizonte, depois do bloqueio)
                if (Mathf.Abs(p.x) >= DesenhoDoMapa.Metade - 250f || Mathf.Abs(p.y) >= DesenhoDoMapa.Metade - 250f) return true;
                for (int i = 0; i < linhas.Count; i++)
                    if (i != propria && Linhas.Distancia(p, linhas[i]) < 120f) return true;
                foreach (var dis in distritos)
                    if (Linhas.Dentro(p, dis) || Linhas.Distancia(p, dis, true) < 150f) return true;
                foreach (var l in d.lugares)
                    if (Vector2.Distance(p, l.ponto) < l.raio + 150f) return true;
                foreach (var b in d.bloqueios)
                    if (Vector2.Distance(p, b.ponto) < 300f) return true;
                return false;
            }

            for (int i = 0; i < d.estradas.Count; i++)
            {
                var e = d.estradas[i];
                Assert.GreaterOrEqual(e.pontos.Count, 2, $"{e.nome} sem pontos");
                bool comeco = Ligada(e.pontos[0], i), fim = Ligada(e.pontos[e.pontos.Count - 1], i);
                // a interestadual liga nas duas pontas; as outras, por enquanto, numa só: no mapa limpo só entraram as
                // estradas vermelhas da imagem, e uma delas pode acabar onde a imagem continua numa estrada menor (a
                // State Route 17, no laço do norte)
                if (e.tipo != TipoDeVia.Interestadual) Assert.IsTrue(comeco || fim, $"a {e.nome} não liga em nada");
                else
                {
                    Assert.IsTrue(comeco, $"o começo de {e.nome} não liga em nada");
                    Assert.IsTrue(fim, $"o fim de {e.nome} não liga em nada");
                }
            }
        }

        [Test]
        public void OsRiosChegamNumLagoNumRioOuNaBorda()
        {
            var d = Desenho();
            var r = Relevo();
            var lagos = new List<List<Vector2>>();
            foreach (var l in d.lagos) lagos.Add(DesenhoDoMapa.Contorno(l.contorno));
            var rios = new List<List<Vector2>>();
            foreach (var rio in d.rios) rios.Add(DesenhoDoMapa.Linha(rio.pontos));
            for (int i = 0; i < d.rios.Count; i++)
            {
                var rio = d.rios[i];
                var foz = rio.pontos[rio.pontos.Count - 1];
                bool chega = Mathf.Abs(foz.x) >= DesenhoDoMapa.Metade - 250f || Mathf.Abs(foz.y) >= DesenhoDoMapa.Metade - 250f;
                foreach (var l in lagos) chega |= Linhas.Dentro(foz, l) || Linhas.Distancia(foz, l, true) < 150f;
                for (int j = 0; j < rios.Count; j++) chega |= j != i && Linhas.Distancia(foz, rios[j]) < 150f;
                // ou no brejo
                for (float dx = -150f; dx <= 150f && !chega; dx += 50f)
                for (float dz = -150f; dz <= 150f && !chega; dz += 50f)
                    chega = d.Fracao(Cobertura.Brejo, foz.x + dx, foz.y + dz) + d.Fracao(Cobertura.AguaDoBrejo, foz.x + dx, foz.y + dz) > 0.3f;
                Assert.IsTrue(chega, $"o rio {i} ({rio.nome}), de {rio.pontos[0]} a {foz}, não chega num lago, num rio, no brejo nem na borda");
                // a água desce: a do desenho ou, com o nível pelo relevo, a calculada
                var niveis = r.NiveisDosRios[i];
                Assert.IsNotNull(niveis, $"o {rio.nome} tem a água calculada");
                if (rio.nivelPeloRelevo) Assert.GreaterOrEqual(niveis[0], niveis[niveis.Length - 1], $"a água do {rio.nome} desce");
                else Assert.Greater(rio.nivelNoComeco, rio.nivelNoFim, $"a água do {rio.nome} desce");
                for (int k = 1; k < niveis.Length; k++)
                    Assert.LessOrEqual(niveis[k], niveis[k - 1] + 0.01f, $"a água do {rio.nome} não sobe");
                Assert.LessOrEqual(rio.larguraNoComeco, rio.larguraNoFim + 0.01f, $"o {rio.nome} alarga para a foz");
            }
        }

        [Test]
        public void AsPontesAcharamOCruzamento()
        {
            var d = Desenho();
            var pontes = PontesDoDesenho.Calcular(d, out var sem);
            Assert.IsEmpty(sem, "pontes do desenho sem cruzamento: " + string.Join(", ", sem.ConvertAll(k => d.pontes[k].nome)));
            Assert.IsNotEmpty(pontes, "as estradas cruzam os rios");
            int bloqueadas = 0;
            foreach (var p in pontes)
            {
                if (p.Estado == EstadoDaPonte.Bloqueada) bloqueadas++;
                Assert.Greater(p.Comprimento, p.LarguraDoRio, $"a ponte de {p.Via} no {p.Rio} atravessa o rio");
            }
            int noDesenho = 0;
            foreach (var p in d.pontes) if (p.estado == EstadoDaPonte.Bloqueada) noDesenho++;
            Assert.AreEqual(noDesenho, bloqueadas, "as bloqueadas do desenho");
        }

        [Test]
        public void ORelevoTemAsAlturasDoPlano()
        {
            var d = Desenho();
            var r = Relevo();
            foreach (var p in d.relevo.picos)
                Assert.That(r.Maxima(p.ponto, 650f), Is.InRange(1300f, 2250f), $"o {p.nome}");
            var q = Quarentena();
            float maxDentro = 0f;
            for (int j = 0; j < r.N; j += 3)
            for (int i = 0; i < r.N; i += 3)
            {
                var pt = new Vector2(r.X(i), r.X(j));
                if (Linhas.Dentro(pt, q)) maxDentro = Mathf.Max(maxDentro, r.Alturas[j * r.N + i]);
            }
            Assert.Less(maxDentro, 1000f, "nada alto demais dentro da quarentena (as montanhas altas são o horizonte)");
            // o fundo de cada lago (fora das ilhas) fica abaixo da água, e as ilhas acima; os bloqueios num chão que dá
            // para subir de carro
            var ilhas = new List<List<Vector2>>();
            foreach (var il in d.ilhas) ilhas.Add(DesenhoDoMapa.Contorno(il.contorno));
            for (int li = 0; li < d.lagos.Count; li++)
            {
                var l = d.lagos[li];
                float nivel = r.NivelDosLagos[li];
                var contorno = DesenhoDoMapa.Contorno(l.contorno);
                int pontos = 0;
                for (int j = 0; j < r.N; j += 3)
                for (int i = 0; i < r.N; i += 3)
                {
                    var pt = new Vector2(r.X(i), r.X(j));
                    if (!Linhas.Dentro(pt, contorno)) continue;
                    bool naIlha = false;
                    foreach (var il in ilhas) naIlha |= Linhas.Dentro(pt, il);
                    float h = r.Alturas[j * r.N + i];
                    if (naIlha) Assert.Greater(h, nivel, $"a ilha em {pt}, no {l.nome}");
                    else Assert.Less(h, nivel, $"o fundo do {l.nome} em {pt}");
                    pontos++;
                }
                Assert.Greater(pontos, 3, $"o {l.nome} tem água");
            }
            foreach (var b in d.bloqueios) Assert.Less(r.Altura(b.ponto.x, b.ponto.y), 400f, $"o bloqueio {b.nome}");
        }

        [Test]
        public void AsEstradasPrincipaisTemARampaDeUmaEstrada()
        {
            // a rampa em trechos de 50 m: até 6% na interestadual e 8% nas outras (com a folga da conta)
            foreach (var e in Chao().Estradas)
            {
                float limite = (e.Via.tipo == TipoDeVia.Interestadual ? 0.06f : 0.08f) + 0.005f;
                var acum = new float[e.Pontos.Length];
                for (int i = 1; i < e.Pontos.Length; i++) acum[i] = acum[i - 1] + Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
                int j = 0;
                for (int i = 0; i < e.Pontos.Length; i++)
                {
                    while (j < e.Pontos.Length - 1 && acum[j] - acum[i] < 50f) j++;
                    if (acum[j] - acum[i] < 40f) break;
                    float rampa = Mathf.Abs(e.Alturas[j] - e.Alturas[i]) / (acum[j] - acum[i]);
                    Assert.LessOrEqual(rampa, limite, $"a {e.Via.nome} sobe {rampa * 100f:F1}% a {acum[i]:F0} m do começo");
                }
                // e nenhum ponto dela fica dentro da água sem a ponte
                for (int i = 0; i < e.Pontos.Length; i++)
                    if (Chao().Agua(e.Pontos[i].x, e.Pontos[i].y, out float nivel))
                        Assert.IsTrue(e.Ponte[i] || e.Alturas[i] > nivel + 1f, $"a {e.Via.nome} entra na água a {acum[i]:F0} m do começo");
            }
        }

        [Test]
        public void APinturaSaiIgualDuasVezes()
        {
            var d = Desenho();
            var a = PintorDoDesenho.Pintar(d, Relevo(), 256);
            var b = PintorDoDesenho.Pintar(d, Relevo(), 256);
            Assert.AreEqual(256 * 256, a.Length);
            for (int k = 0; k < a.Length; k++)
                if (!a[k].Equals(b[k])) Assert.Fail($"o pixel {k} mudou");
            Assert.Greater(PintorDoDesenho.Rotulos(d, Relevo()).Count, 10, "os nomes");
            Assert.Greater(PintorDoDesenho.Escudos(d).Count, 3, "os escudos");
        }
    }
}
