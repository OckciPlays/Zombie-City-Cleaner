using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// O mapa de Lakewood (Plano_Lakewood, Etapas 1 e 2; os critérios de qualidade do plano): o tamanho e os setores, o
    /// reservatório com um quinto do mapa e o fundo abaixo da água, as cinco ilhas acima dela, os rios descendo até o lago
    /// ou a borda (o rio de entrada ao norte e o de saída ao sul, pela barragem), as cotas por região (a planície do oeste,
    /// as montanhas do norte e do leste, o vale abaixo da barragem), as cidades em terra firme e suaves, a cobertura lida da
    /// imagem e a pintura igual duas vezes; e a hidrelétrica (Etapa 4: as peças, os pátios no chão, a crista livre, as
    /// estradas de acesso sem ponte, o canal de fuga com o começo do rio e as torres com os cabos acima do chão). O relevo e
    /// o chão são feitos uma vez só.
    /// </summary>
    public class LakewoodTestes
    {
        static MapaDeLakewood _m;
        static RelevoDeLakewood _relevo;
        static ChaoDeLakewood _chao;

        static MapaDeLakewood Mapa()
        {
            if (_m == null) _m = AssetDatabase.LoadAssetAtPath<MapaDeLakewood>(MapaDeLakewood.Caminho);
            Assert.IsNotNull(_m, $"o mapa em {MapaDeLakewood.Caminho} (Zombie City Cleaner > Lakewood > 1 e 2)");
            return _m;
        }

        static RelevoDeLakewood Relevo() => _relevo ??= new RelevoDeLakewood(Mapa());

        static ChaoDeLakewood Chao() => _chao ??= new ChaoDeLakewood(Mapa(), Relevo());

        static List<Vector2> Principal() => MapaDeLakewood.Contorno(Mapa().lagos[Mapa().LagoPrincipal()].contorno);

        [Test]
        public void OMapaTemOTamanhoEOsSetoresDoPlano()
        {
            var m = Mapa();
            Assert.AreEqual(12000f, m.largura, 1f, "12 km de oeste a leste");
            Assert.AreEqual(10000f, m.comprimento, 1f, "10 km de sul a norte");
            Assert.AreEqual(4, m.setoresX);
            Assert.AreEqual(4, m.setoresZ);
            Assert.AreEqual(3000f, m.LadoDoSetorX, 1f);
            Assert.AreEqual(2500f, m.LadoDoSetorZ, 1f);
            Assert.AreEqual("A1", m.NomeDoSetorEm(10f, 10f), "o canto sudoeste");
            Assert.AreEqual("D4", m.NomeDoSetorEm(11990f, 9990f), "o canto nordeste");
            Assert.AreEqual("C2", m.NomeDoSetorEm(7000f, 3000f));
            var c = Chao();
            Assert.AreEqual(188, c.BlocosX);
            Assert.AreEqual(157, c.BlocosZ);
            Assert.AreEqual(601, Relevo().NX);
            Assert.AreEqual(501, Relevo().NZ);
        }

        [Test]
        public void OReservatorioOcupaUmQuintoDoMapa()
        {
            var m = Mapa();
            int p = m.LagoPrincipal();
            Assert.GreaterOrEqual(p, 0, "o reservatório lido da imagem");
            Assert.AreEqual("Lakewood Reservoir", m.lagos[p].nome);
            Assert.AreEqual(150f, m.lagos[p].nivel, 0.01f, "a cota fixa da água");
            Assert.AreEqual(150f, Relevo().NivelDosLagos[p], 0.01f);
            float agua = 0f;
            foreach (var l in m.lagos) if (l.contorno.Count >= 3) agua += Linhas.Area(MapaDeLakewood.Contorno(l.contorno));
            foreach (var i in m.ilhas) if (i.contorno.Count >= 3) agua -= Linhas.Area(MapaDeLakewood.Contorno(i.contorno));
            float fracao = agua / (m.largura * m.comprimento);
            Assert.That(fracao, Is.InRange(0.16f, 0.28f), $"a água ocupa {100f * fracao:F1}% do mapa (o plano: de 20 a 25%)");
            // a ponta sudoeste do lago chega na barragem (as cabeceiras ficam no contorno)
            var c = Principal();
            Assert.Less(Linhas.Distancia(m.represa.a, c, true), 60f, "a cabeceira a fica na margem");
            Assert.Less(Linhas.Distancia(m.represa.b, c, true), 60f, "a cabeceira b fica na margem");
        }

        [Test]
        public void AsCincoIlhasExistem()
        {
            var m = Mapa();
            var r = Relevo();
            foreach (TipoDeIlha tipo in System.Enum.GetValues(typeof(TipoDeIlha)))
            {
                if (tipo == TipoDeIlha.Ilhote) continue;
                var achadas = m.ilhas.FindAll(i => i.tipo == tipo);
                Assert.AreEqual(1, achadas.Count, $"uma ilha do tipo {tipo}");
                var ilha = achadas[0];
                Assert.GreaterOrEqual(ilha.contorno.Count, 3, $"{ilha.nome} tem contorno");
                var c = MapaDeLakewood.Contorno(ilha.contorno);
                Assert.Greater(Linhas.Area(c) / 1e6f, 0.1f, $"{ilha.nome} tem mais de 0,1 km²");
                Assert.IsTrue(Linhas.Dentro(Linhas.Centro(c), Principal()), $"{ilha.nome} fica no reservatório");
                var meio = Linhas.Centro(c);
                Assert.Greater(r.Maxima(meio, 300f), 150f + 8f, $"{ilha.nome} tem chão explorável acima da água");
            }
            var central = m.ilhas.Find(i => i.tipo == TipoDeIlha.Central);
            Assert.Greater(r.Maxima(Linhas.Centro(MapaDeLakewood.Contorno(central.contorno)), 400f), 150f + 25f, "a Ilha Central tem a elevação do laboratório");
        }

        [Test]
        public void OFundoDoLagoFicaAbaixoDaAguaEAsIlhasAcima()
        {
            var m = Mapa();
            var r = Relevo();
            var ilhas = new List<List<Vector2>>();
            foreach (var il in m.ilhas) ilhas.Add(MapaDeLakewood.Contorno(il.contorno));
            for (int li = 0; li < m.lagos.Count; li++)
            {
                var l = m.lagos[li];
                if (l.contorno.Count < 3) continue;
                float nivel = r.NivelDosLagos[li];
                var contorno = MapaDeLakewood.Contorno(l.contorno);
                int pontos = 0, fundos = 0;
                float maisFundo = float.MaxValue;
                for (int j = 0; j < r.NZ; j += 3)
                for (int i = 0; i < r.NX; i += 3)
                {
                    var pt = new Vector2(r.X(i), r.Z(j));
                    if (!Linhas.Dentro(pt, contorno)) continue;
                    // a linha d'água do relevo é tremida (até 14 m para cada lado): a beira não se confere
                    if (Linhas.Distancia(pt, contorno, true) < 25f) continue;
                    bool naIlha = false, naBeira = false;
                    foreach (var il in ilhas)
                    {
                        naIlha |= Linhas.Dentro(pt, il);
                        naBeira |= Linhas.Distancia(pt, il, true) < 25f;
                    }
                    if (naBeira) continue;
                    float h = r.Alturas[j * r.NX + i];
                    if (naIlha) Assert.Greater(h, nivel, $"a ilha em {pt}, no {l.nome}");
                    else
                    {
                        Assert.Less(h, nivel, $"o fundo do {l.nome} em {pt}");
                        maisFundo = Mathf.Min(maisFundo, h);
                        if (h < 145f) fundos++;
                    }
                    pontos++;
                }
                Assert.Greater(pontos, 3, $"o {l.nome} tem água");
                if (li == m.LagoPrincipal()) Assert.Less(maisFundo, 125f, "o reservatório tem partes fundas (o plano: o fundo abaixo de 145 m)");
            }
        }

        [Test]
        public void OsRiosDescemEChegamNoLagoOuNaBorda()
        {
            var m = Mapa();
            var r = Relevo();
            var lagos = new List<List<Vector2>>();
            foreach (var l in m.lagos) lagos.Add(MapaDeLakewood.Contorno(l.contorno));
            var rios = new List<List<Vector2>>();
            for (int i = 0; i < m.rios.Count; i++) rios.Add(r.LinhasDosRios[i] ?? MapaDeLakewood.Linha(m.rios[i].pontos));
            bool temEntrada = false, temSaida = false;
            for (int i = 0; i < m.rios.Count; i++)
            {
                var rio = m.rios[i];
                var foz = rio.pontos[rio.pontos.Count - 1];
                bool borda = r.NaBorda(foz, 250f), lago = false, outro = false;
                foreach (var l in lagos) lago |= Linhas.Dentro(foz, l) || Linhas.Distancia(foz, l, true) < 150f;
                for (int j = 0; j < rios.Count; j++) outro |= j != i && Linhas.Distancia(foz, rios[j]) < 150f;
                Assert.IsTrue(borda || lago || outro, $"o rio {rio.nome}, de {rio.pontos[0]} a {foz}, não chega num lago, num rio nem na borda");
                var niveis = r.NiveisDosRios[i];
                Assert.IsNotNull(niveis, $"o {rio.nome} tem a água calculada");
                for (int k = 1; k < niveis.Length; k++) Assert.LessOrEqual(niveis[k], niveis[k - 1] + 0.01f, $"a água do {rio.nome} não sobe");
                Assert.LessOrEqual(rio.larguraNoComeco, rio.larguraNoFim + 0.01f, $"o {rio.nome} alarga para a foz");
                if (lago && rio.pontos[0].y > m.comprimento - 300f) temEntrada = true;
                if (borda && foz.y < 300f && rio.pontos[0].y < m.represa.Meio.y) temSaida = true;
            }
            Assert.IsTrue(temEntrada, "um rio entra pelo norte e chega no reservatório");
            Assert.IsTrue(temSaida, "um rio sai da barragem e vai até a borda sul");
            // o rio de saída começa logo abaixo da barragem, do lado de fora
            var saida = m.rios.Find(x => x.nome == "Lakewood River");
            Assert.IsNotNull(saida);
            var comeco = saida.pontos[0];
            Assert.Less(Linhas.DistanciaDoSegmento(comeco, m.represa.a, m.represa.b, out _), 350f, "o rio de saída nasce no pé da barragem");
            Assert.Greater(Vector2.Dot(comeco - m.represa.Meio, r.Jusante), 0f, "do lado de fora dela");
        }

        [Test]
        public void OsRiosTemOsMeandrosOLeitoEOsBarrancos()
        {
            var m = Mapa();
            var r = Relevo();
            var chao = Chao();
            for (int i = 0; i < m.rios.Count; i++)
            {
                var rio = m.rios[i];
                var linha = r.LinhasDosRios[i];
                Assert.IsNotNull(linha, $"o {rio.nome} tem a linha");
                float reta = Linhas.Comprimento(MapaDeLakewood.Linha(rio.pontos));
                Assert.Greater(Linhas.Comprimento(linha) / reta, 1.02f, $"o {rio.nome} tem meandros");
                Assert.Less(Vector2.Distance(linha[0], rio.pontos[0]), 10f, $"a nascente do {rio.nome} fica onde o desenho diz");
                Assert.Less(Vector2.Distance(linha[linha.Count - 1], rio.pontos[rio.pontos.Count - 1]), 10f, $"a foz do {rio.nome} fica onde o desenho diz");
                int leito = 0, barranco = 0, amostras = 0;
                for (float t = 0.15f; t <= 0.85f; t += 0.05f)
                {
                    var p = Linhas.PontoNa(linha, t, out var dir);
                    if (chao.NoLago(p.x, p.y)) continue;
                    float nivel = r.NivelDoRio(i, t);
                    float meia = MapaDeLakewood.LarguraDoRio(rio, t) * 0.5f;
                    amostras++;
                    if (chao.Altura(p.x, p.y) < nivel - 0.3f) leito++;
                    Assert.IsTrue(chao.Agua(p.x, p.y, out float na) && Mathf.Abs(na - nivel) < 0.5f, $"a água do {rio.nome} em {p}");
                    var lado = new Vector2(-dir.y, dir.x);
                    float fora = meia + MapaDeLakewood.MargemDoRio(meia * 2f) * 3.5f + 1f;
                    var q1 = p + lado * fora;
                    var q2 = p - lado * fora;
                    if (chao.Altura(q1.x, q1.y) > nivel + 0.25f && chao.Altura(q2.x, q2.y) > nivel + 0.25f) barranco++;
                }
                Assert.AreEqual(amostras, leito, $"o leito do {rio.nome} fica abaixo da água ({leito} de {amostras})");
                Assert.GreaterOrEqual(barranco, Mathf.RoundToInt(amostras * 0.9f), $"as margens do {rio.nome} ficam acima da água ({barranco} de {amostras})");
            }
        }

        [Test]
        public void ORelevoSegueOPlano()
        {
            var m = Mapa();
            var r = Relevo();
            var c = Chao();
            float terco = m.largura / 3f, tercoZ = m.comprimento / 3f;
            List<float> Amostra(float x0, float x1, float z0, float z1)
            {
                var v = new List<float>();
                for (float z = z0 + 50f; z < z1; z += 100f)
                for (float x = x0 + 50f; x < x1; x += 100f)
                    if (!c.Agua(x, z, out _)) v.Add(r.Altura(x, z));
                v.Sort();
                return v;
            }
            float Media(List<float> v)
            {
                float s = 0f;
                foreach (var h in v) s += h;
                return v.Count > 0 ? s / v.Count : 0f;
            }
            var oeste = Amostra(0f, terco, 0f, m.comprimento);
            var norte = Amostra(0f, m.largura, 2f * tercoZ, m.comprimento);
            var leste = Amostra(2f * terco, m.largura, 0f, m.comprimento);
            float mo = Media(oeste), mn = Media(norte), ml = Media(leste);
            Assert.That(mo, Is.InRange(160f, 260f), $"a planície do oeste (média {mo:F0} m)");
            Assert.Less(oeste[Mathf.RoundToInt(0.9f * (oeste.Count - 1))], 340f, "o oeste quase sem morros altos");
            Assert.Greater(mn, mo + 60f, $"o norte ({mn:F0} m) bem mais alto que o oeste ({mo:F0} m)");
            Assert.Greater(ml, mo + 25f, $"o leste ({ml:F0} m) mais alto que o oeste ({mo:F0} m)");
            // o ponto mais alto: nas montanhas do norte ou do leste, entre 560 e 760 m
            float max = float.MinValue;
            Vector2 onde = default;
            for (int j = 0; j < r.NZ; j += 2)
            for (int i = 0; i < r.NX; i += 2)
                if (r.Alturas[j * r.NX + i] > max) { max = r.Alturas[j * r.NX + i]; onde = new Vector2(r.X(i), r.Z(j)); }
            Assert.That(max, Is.InRange(560f, 760f), $"o ponto mais alto ({max:F0} m em {onde})");
            Assert.IsTrue(onde.x > 2f * terco || onde.y > 2f * tercoZ, $"o ponto mais alto fica no norte ou no leste ({onde})");
            // o vale abaixo da barragem
            var rp = m.represa;
            var jus = r.Jusante;
            float vale = r.Altura(rp.Meio.x + jus.x * 400f, rp.Meio.y + jus.y * 400f);
            Assert.That(vale, Is.InRange(85f, 120f), $"o vale 400 m abaixo da barragem ({vale:F0} m; o plano: de 90 a 110)");
            float pe = r.Altura(rp.Meio.x + jus.x * 60f, rp.Meio.y + jus.y * 60f);
            Assert.Less(pe, rp.alturaDaCrista - 35f, $"a barragem tem altura no pé ({pe:F0} m contra a crista a {rp.alturaDaCrista:F0})");
            // nada afunda no mapa inteiro abaixo do vale
            float min = float.MaxValue;
            foreach (var h in r.Alturas) min = Mathf.Min(min, h);
            Assert.Greater(min, 60f, $"o chão mais baixo ({min:F0} m)");
        }

        [Test]
        public void AsCidadesFicamEmTerraFirmeESuaves()
        {
            var m = Mapa();
            var r = Relevo();
            var c = Chao();
            Assert.GreaterOrEqual(m.cidades.Count, 5, "as 4 cidades e a comunidade rural");
            foreach (var nome in new[] { "Lakewood", "Riverside", "Fairview", "Woodcreek", "Sullivan Farms" })
                Assert.IsNotNull(m.cidades.Find(x => x.nome == nome), nome);
            var lagoPrincipal = Principal();
            foreach (var cid in m.cidades)
            {
                var cont = MapaDeLakewood.Contorno(cid.contorno);
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (var p in cont) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y); }
                int pontos = 0, naAgua = 0;
                float soma = 0f, inclinacao = 0f;
                for (float z = z0; z < z1; z += 60f)
                for (float x = x0; x < x1; x += 60f)
                {
                    if (!Linhas.Dentro(new Vector2(x, z), cont)) continue;
                    pontos++;
                    if (c.Agua(x, z, out _)) { naAgua++; continue; }
                    soma += r.Altura(x, z);
                    inclinacao += r.Inclinacao(x, z);
                }
                int secos = pontos - naAgua;
                Assert.Greater(secos, 20, $"{cid.nome} tem chão");
                Assert.Less(naAgua / (float)pontos, 0.12f, $"{cid.nome} quase toda em terra firme ({100f * naAgua / pontos:F0}% na água)");
                Assert.Less(inclinacao / secos, 0.14f, $"{cid.nome} com o chão suave (inclinação média {100f * inclinacao / secos:F1}%)");
                Assert.Greater(soma / secos, 152f, $"{cid.nome} acima da água");
            }
            var lakewood = m.cidades.Find(x => x.nome == "Lakewood");
            Assert.AreEqual(TipoDeCidade.Principal, lakewood.tipo);
            Assert.IsTrue(lakewood.centro.x > m.largura * 0.6f && lakewood.centro.y < m.comprimento * 0.45f, "a Lakewood no sudeste");
            Assert.Less(Linhas.Distancia(lakewood.centro, lagoPrincipal, true), 2200f, "a Lakewood na margem do reservatório");
            var riverside = m.cidades.Find(x => x.nome == "Riverside");
            Assert.IsTrue(riverside.centro.x < m.largura * 0.45f && riverside.centro.y > m.comprimento * 0.7f, "a Riverside no noroeste");
            var fairview = m.cidades.Find(x => x.nome == "Fairview");
            Assert.Less(fairview.centro.x, m.largura * 0.25f, "a Fairview no oeste");
            var woodcreek = m.cidades.Find(x => x.nome == "Woodcreek");
            Assert.IsTrue(woodcreek.centro.x > m.largura * 0.7f && woodcreek.centro.y > m.comprimento * 0.55f, "a Woodcreek no nordeste");
            // os lugares com chão aplainado ficam em terra firme
            foreach (var l in m.lugares)
                if (l.aplainar > 0.5f) Assert.IsFalse(c.Agua(l.ponto.x, l.ponto.y, out _), $"{l.nome} na água");
        }

        [Test]
        public void ACoberturaFoiLidaDaImagem()
        {
            var m = Mapa();
            Assert.IsTrue(m.TemGrade, "a grade da cobertura");
            Assert.AreEqual(480, m.gradeX);
            Assert.AreEqual(400, m.gradeZ);
            var porClasse = new int[6];
            foreach (var c in m.cobertura) porClasse[c]++;
            int terra = 0;
            for (int k = 0; k < 6; k++) if (k != (int)Cobertura.Agua) terra += porClasse[k];
            Assert.Greater(porClasse[(int)Cobertura.Mata] / (float)terra, 0.4f, "a mata é a maior parte da terra");
            Assert.Greater(porClasse[(int)Cobertura.Lavoura], 0, "há lavouras");
            Assert.Greater(porClasse[(int)Cobertura.Rocha], 0, "há rocha");
            // o urbano só dentro das cidades e das instalações aplainadas (o aeroporto, a estação), com a folga de uma célula
            var cidades = new List<List<Vector2>>();
            foreach (var c in m.cidades) cidades.Add(MapaDeLakewood.Contorno(c.contorno));
            int fora = 0;
            for (int j = 0; j < m.gradeZ; j++)
            for (int i = 0; i < m.gradeX; i++)
            {
                if (m.cobertura[j * m.gradeX + i] != (byte)Cobertura.Urbano) continue;
                var p = new Vector2((i + 0.5f) * m.celulaDaGrade, (j + 0.5f) * m.celulaDaGrade);
                bool dentro = false;
                foreach (var c in cidades) dentro |= Linhas.Dentro(p, c) || Linhas.Distancia(p, c, true) < m.celulaDaGrade * 1.5f;
                foreach (var l in m.lugares) dentro |= l.aplainar > 0f && Vector2.Distance(p, l.ponto) < l.raio + m.celulaDaGrade * 1.5f;
                if (!dentro) fora++;
            }
            Assert.AreEqual(0, fora, "células urbanas fora das cidades e das instalações");
            // a água da grade bate com o contorno do reservatório
            var lago = Principal();
            int bate = 0, total = 0;
            for (int j = 0; j < m.gradeZ; j += 2)
            for (int i = 0; i < m.gradeX; i += 2)
            {
                var p = new Vector2((i + 0.5f) * m.celulaDaGrade, (j + 0.5f) * m.celulaDaGrade);
                bool dentro = Linhas.Dentro(p, lago);
                if (!dentro) continue;
                bool naIlha = false;
                foreach (var il in m.ilhas) naIlha |= Linhas.Dentro(p, MapaDeLakewood.Contorno(il.contorno));
                if (naIlha) continue;
                total++;
                if (m.cobertura[j * m.gradeX + i] == (byte)Cobertura.Agua) bate++;
            }
            Assert.Greater(bate / (float)total, 0.95f, "as células dentro do reservatório são água");
            // as montanhas da imagem ficam no norte e no leste
            float oeste = 0f, norteLeste = 0f;
            int no = 0, nl = 0;
            for (int j = 0; j < m.gradeZ; j++)
            for (int i = 0; i < m.gradeX; i++)
            {
                float x = (i + 0.5f) * m.celulaDaGrade, z = (j + 0.5f) * m.celulaDaGrade;
                if (x < m.largura / 3f) { oeste += m.montanha[j * m.gradeX + i]; no++; }
                else if (x > 2f * m.largura / 3f || z > 2f * m.comprimento / 3f) { norteLeste += m.montanha[j * m.gradeX + i]; nl++; }
            }
            // (a imagem também tem morros rochosos na beira oeste e no canto sudoeste; o relevo os baixa pelo fator da zona)
            Assert.Greater(norteLeste / nl, oeste / no * 1.1f, "a imagem tem mais montanha no norte e no leste que no oeste");
        }

        [Test]
        public void OsSetoresCobremOMapaComORelevoContinuo()
        {
            var m = Mapa();
            var c = Chao();
            for (int j = 0; j < m.setoresZ; j++)
            for (int i = 0; i < m.setoresX; i++)
            {
                var limites = m.LimitesDoSetor(i, j);
                var malha = c.Longe(i, j);
                Assert.AreEqual(120 * 100 * 6, malha.Vertices.Length, $"o relevo de longe do setor {MapaDeLakewood.NomeDoSetor(i, j)}");
                Assert.IsNotNull(malha.Cores, "o relevo de longe tem a cor nos vértices");
                foreach (var v in malha.Vertices)
                {
                    Assert.That(v.x, Is.InRange(limites.xMin - 1f, limites.xMax + 1f), "o x dentro do setor");
                    Assert.That(v.z, Is.InRange(limites.yMin - 1f, limites.yMax + 1f), "o z dentro do setor");
                }
            }
            // a altura nas divisas dos setores é a mesma vista dos dois lados (o chão é uma função só)
            for (int i = 1; i < m.setoresX; i++)
                for (float z = 100f; z < m.comprimento; z += 500f)
                    Assert.AreEqual(c.Altura(i * m.LadoDoSetorX - 0.001f, z), c.Altura(i * m.LadoDoSetorX + 0.001f, z), 0.05f, "a divisa em x");
            for (int j = 1; j < m.setoresZ; j++)
                for (float x = 100f; x < m.largura; x += 500f)
                    Assert.AreEqual(c.Altura(x, j * m.LadoDoSetorZ - 0.001f), c.Altura(x, j * m.LadoDoSetorZ + 0.001f), 0.05f, "a divisa em z");
            // um bloco de terreno de cada setor sai com a colisão possível (a malha de 2 m)
            var bloco = c.Terreno(c.BlocosX / 2, c.BlocosZ / 2, 2f);
            Assert.Greater(bloco.Vertices.Length, 32 * 32 * 6);
            Assert.IsNotNull(bloco.Cores);
        }

        [Test]
        public void OReservatorioTemOValeAfogadoEAsMargens()
        {
            var m = Mapa();
            var r = Relevo();
            var c = Chao();
            Assert.GreaterOrEqual(m.canalDoReservatorio.Count, 2, "o vale afogado do esboço");
            float nivel = r.NivelDosLagos[m.LagoPrincipal()];
            // fundo junto da barragem, no canal; raso nos braços do oeste
            var pertoDaRepresa = m.represa.Meio - r.Jusante * 220f;
            Assert.Less(r.Altura(pertoDaRepresa.x, pertoDaRepresa.y), nivel - 28f, "o lago é fundo junto da barragem");
            var meioDoCanal = m.canalDoReservatorio[m.canalDoReservatorio.Count / 2];
            Assert.Less(r.Altura(meioDoCanal.x, meioDoCanal.y), nivel - 14f, "o canal é fundo no meio do caminho");
            Assert.IsTrue(c.NoLago(3150f, 5000f), "o braço oeste é água");
            Assert.Greater(r.Altura(3150f, 5000f), nivel - 14f, "o braço oeste é raso");
            // a beira da água: na margem, a terra e o cascalho (não a grama nem as folhas) em cima da linha d'água
            var contorno = MapaDeLakewood.Contorno(m.lagos[m.LagoPrincipal()].contorno);
            int amostras = 0;
            float praia = 0f;
            for (int i = 0; i < contorno.Count; i += 5)
            {
                var ponto = contorno[i];
                // sobe da água até achar o chão seco logo acima da linha d'água
                var dir = (ponto - Linhas.Centro(contorno)).normalized;
                for (float s = 0f; s < 80f; s += 2f)
                {
                    var q = ponto + dir * s;
                    if (c.Agua(q.x, q.y, out _)) continue;
                    float h = c.Altura(q.x, q.y);
                    if (h - nivel > 1.2f) break;
                    var pesos = c.Pesos(q.x, q.y, h);
                    praia += (pesos.g + pesos.b) / 255f;
                    amostras++;
                    break;
                }
            }
            Assert.Greater(amostras, 40, "achou chão seco na beira da água");
            Assert.Greater(praia / amostras, 0.6f, "a beira da água é de terra e cascalho (a praia)");
        }

        [Test]
        public void APinturaSaiIgualDuasVezes()
        {
            var m = Mapa();
            var a = PintorDeLakewood.Pintar(m, Relevo(), 240);
            var b = PintorDeLakewood.Pintar(m, Relevo(), 240);
            Assert.AreEqual(240 * 200, a.Length);
            for (int k = 0; k < a.Length; k++)
                if (!a[k].Equals(b[k])) Assert.Fail($"o pixel {k} mudou");
            Assert.Greater(PintorDeLakewood.Rotulos(m, Relevo()).Count, 20, "os nomes");
        }

        // ------------------------------------------------------------------ a hidrelétrica (Etapa 4)

        static List<PecaDaUsina> _pecas;

        static List<PecaDaUsina> PecasDaUsina() => _pecas ??= Chao().Usina.Montar((x, z) => Chao().Altura(x, z));

        [Test]
        public void AUsinaTemAsPecasEOsPatiosNoChao()
        {
            var c = Chao();
            var u = c.Usina;
            Assert.IsTrue(u.Tem, "a barragem no mapa");
            var pecas = PecasDaUsina();
            foreach (var nome in new[] { "Barragem: Lakewood Dam", "Casa de força", "Canal de fuga", "Subestação", "Administração", "Estacionamento",
                                         "Galeria de serviço", "Oficina de manutenção", "Galpão 1", "Galpão 2", "Tanque de diesel", "Guarita do portão",
                                         "Guarita leste", "Guarita oeste", "Alambrado e portão", "Alambrado da subestação", "Posto de controle" })
                Assert.IsTrue(pecas.Exists(p => p.Nome == nome), nome);
            Assert.IsTrue(pecas.Exists(p => p.Tipo == TipoDaPecaDaUsina.Agua), "a água do canal de fuga");
            Assert.GreaterOrEqual(pecas.FindAll(p => p.Tipo == TipoDaPecaDaUsina.Carro).Count, 10, "os carros abandonados");
            Assert.GreaterOrEqual(u.Torres.Count, 15, "as torres das duas linhas");
            // os prédios no chão plano do pátio deles, fora d'água
            foreach (var pt in u.Patios)
            {
                if (pt.Tipo != UsinaDeLakewood.TipoDoPatio.Edificio && pt.Tipo != UsinaDeLakewood.TipoDoPatio.Cascalho && pt.Tipo != UsinaDeLakewood.TipoDoPatio.Asfalto) continue;
                foreach (var (fu, fv) in new[] { (0.5f, 0.5f), (0.15f, 0.15f), (0.85f, 0.85f), (0.15f, 0.85f), (0.85f, 0.15f) })
                {
                    var w = u.Mundo(Mathf.Lerp(pt.Area.xMin, pt.Area.xMax, fu), Mathf.Lerp(pt.Area.yMin, pt.Area.yMax, fv));
                    Assert.AreEqual(pt.Altura, c.Altura(w.x, w.y), 0.3f, $"o chão do pátio {pt.Nome}");
                    Assert.IsFalse(c.Agua(w.x, w.y, out _), $"o pátio {pt.Nome} fora d'água");
                }
            }
            // o peso: a usina inteira com menos de 120 mil triângulos
            int tri = 0;
            var vistos = new HashSet<MalhaDeObra>();
            foreach (var p in pecas)
                if (p.Desenho != null && vistos.Add(p.Desenho)) tri += p.Desenho.Triangulos;
            Assert.Less(tri, 120000, "os triângulos da usina");
        }

        [Test]
        public void ABarragemFechaAGargantaEACristaFicaLivre()
        {
            var c = Chao();
            var u = c.Usina;
            // o chão não cobre a estrada da crista (nas pontas, ela entra na rocha cortada)
            for (float s = -20f; s <= u.Comprimento + 20f; s += 20f)
            {
                var w = u.Mundo(s, 0f);
                Assert.LessOrEqual(c.Altura(w.x, w.y), u.Crista + 0.01f, $"o chão em cima da crista em u = {s:F0}");
            }
            // a crista acima da água, o vale de jusante bem abaixo dela (o reservatório não continua depois da barragem)
            Assert.Greater(u.Crista, u.Nivel + 5f, "a folga da crista");
            var pe = u.Mundo(u.Comprimento * 0.6f, 300f);
            Assert.Less(c.Altura(pe.x, pe.y), u.Nivel - 30f, "o vale a jusante");
            Assert.IsFalse(c.AguaDoLagoEm(pe.x, pe.y, out _), "o lago não passa da barragem");
            var barragem = PecasDaUsina().Find(p => p.Nome == "Barragem: Lakewood Dam");
            Assert.IsNotNull(barragem.Colisao, "a colisão da barragem");
            Assert.Greater(barragem.Colisao.Triangulos, 500);
        }

        [Test]
        public void AAguaDoLagoParaNaBarragem()
        {
            // a malha da água do lago passa duas células além do contorno (o chão da margem esconde a beirada); na
            // barragem, ela é cortada na linha da chapa das comportas, e nada dela fica a jusante (10/11/2026: a água
            // aparecia 60 m para fora da barragem, por cima do vale e do canal de fuga)
            var c = Chao();
            var u = c.Usina;
            var m = c.AguaDoLago(Mapa().LagoPrincipal());
            Assert.IsNotNull(m);
            int naFaixa = 0, aCeuAberto = 0;
            for (int t = 0; t < m.Triangulos.Length; t += 3)
            {
                var p0 = m.Vertices[m.Triangulos[t]];
                var p1 = m.Vertices[m.Triangulos[t + 1]];
                var p2 = m.Vertices[m.Triangulos[t + 2]];
                var g = (p0 + p1 + p2) / 3f;
                // da crista e 100 m além das pontas (na beira da faixa do corte, os morros das cabeceiras escondem a água)
                foreach (var p in new[] { p0, p1, p2, g })
                {
                    var l = u.Local(p.x, p.z);
                    if (l.x < -100f || l.x > u.Comprimento + 100f || l.y > 380f || !c.CorteNaBarragem(p.x, p.z, out float lado)) continue;
                    naFaixa++;
                    Assert.LessOrEqual(lado, 0.01f, $"a água do lago a jusante da barragem em ({p.x:F0}; {p.z:F0}), u = {l.x:F0}");
                }
                // em nenhum lugar a água fica por cima de um vão aberto fora do lago (o chão bem abaixo dela, longe da água)
                if (c.Altura(g.x, g.z) < u.Nivel - 2f && !c.Agua(g.x, g.z, out _, 30f)) aCeuAberto++;
            }
            Assert.Greater(naFaixa, 50, "a água chega na barragem");
            Assert.AreEqual(0, aCeuAberto, "triângulos da água do lago por cima do chão fora do lago");
            // o lago chega na face da barragem (a água encosta na chapa das comportas)
            var vertedouro = u.Mundo(UsinaDeLakewood.MeioDoVertedouro, -3f);
            Assert.IsTrue(c.AguaDoLagoEm(vertedouro.x, vertedouro.y, out _, 6f), "a água na frente das comportas");
        }

        [Test]
        public void AsEstradasDeAcessoSaemDaCristaSemPonte()
        {
            var c = Chao();
            var u = c.Usina;
            int achadas = 0;
            foreach (var e in c.Estradas)
            {
                if (!u.Vias.Contains(e.Via)) continue;
                achadas++;
                if (e.Via.alturaNoComeco > 0f) Assert.AreEqual(e.Via.alturaNoComeco, e.Alturas[0], 0.01f, $"{e.Via.nome} começa na altura presa");
                float rampa = e.Via.rampaMaxima > 0f ? e.Via.rampaMaxima : 0.09f;
                for (int i = 1; i < e.Pontos.Length; i++)
                {
                    Assert.IsFalse(e.Ponte[i], $"{e.Via.nome}: ponte no ponto {i}");
                    float ds = Vector2.Distance(e.Pontos[i - 1], e.Pontos[i]);
                    Assert.LessOrEqual(Mathf.Abs(e.Alturas[i] - e.Alturas[i - 1]), ds * rampa + 0.05f, $"{e.Via.nome}: a rampa no ponto {i}");
                }
                // o chão embaixo do asfalto (o corte e o aterro)
                for (int i = 0; i < e.Pontos.Length; i += 9)
                    Assert.AreEqual(e.Alturas[i] - 0.08f, c.Altura(e.Pontos[i].x, e.Pontos[i].y), 0.35f, $"{e.Via.nome}: o chão no ponto {i}");
            }
            Assert.AreEqual(3, achadas, "a da barragem, a da casa de força e a oeste");
            // a da casa de força chega na da barragem, na mesma altura
            var barragem = c.Estradas.Find(e => e.Via.nome == "Lakewood Dam Road");
            var casa = c.Estradas.Find(e => e.Via.nome == "Powerhouse Road");
            var fim = casa.Pontos[casa.Pontos.Length - 1];
            float perto = float.MaxValue, altura = 0f;
            for (int i = 1; i < barragem.Pontos.Length; i++)
            {
                float d = Linhas.DistanciaDoSegmento(fim, barragem.Pontos[i - 1], barragem.Pontos[i], out float t);
                if (d < perto)
                {
                    perto = d;
                    altura = Mathf.Lerp(barragem.Alturas[i - 1], barragem.Alturas[i], t);
                }
            }
            Assert.Less(perto, 9f, "a da casa de força encosta na da barragem");
            Assert.AreEqual(altura, casa.Alturas[casa.Alturas.Length - 1], 0.6f, "na mesma altura");
        }

        [Test]
        public void OCanalDeFugaEhAguaEORioComecaNele()
        {
            var c = Chao();
            var u = c.Usina;
            var meio = u.Mundo(UsinaDeLakewood.Poco.center.x, UsinaDeLakewood.Poco.center.y);
            Assert.IsTrue(c.Agua(meio.x, meio.y, out float nivel), "a água no poço");
            Assert.AreEqual(UsinaDeLakewood.NivelDaFuga, nivel, 0.01f);
            Assert.Less(c.Altura(meio.x, meio.y), UsinaDeLakewood.FundoDaFuga, "o chão embaixo do fundo do poço");
            var bacia = u.Mundo(UsinaDeLakewood.Bacia.center.x, UsinaDeLakewood.Bacia.center.y);
            Assert.IsTrue(c.Agua(bacia.x, bacia.y, out _), "a água na bacia de dissipação");
            int ri = Mapa().rios.FindIndex(r => r.nome == "Lakewood River");
            Assert.GreaterOrEqual(ri, 0);
            var inicio = Relevo().LinhasDosRios[ri][0];
            var l = u.Local(inicio.x, inicio.y);
            Assert.AreEqual(UsinaDeLakewood.Poco.yMax, l.y, 4f, "o rio começa na saída do poço");
            Assert.That(l.x, Is.InRange(UsinaDeLakewood.SaidaDoRio.x, UsinaDeLakewood.SaidaDoRio.y), "pela abertura do muro");
            Assert.AreEqual(UsinaDeLakewood.NivelDaFuga, Mapa().rios[ri].nivelNoComeco, 0.01f, "na mesma água");
        }

        [Test]
        public void AsTorresFicamEmTerraFirmeEOsCabosAcimaDoChao()
        {
            var c = Chao();
            var u = c.Usina;
            foreach (var t in u.Torres)
            {
                Assert.IsTrue(Mapa().Dentro(t.Ponto.x, t.Ponto.y), "a torre dentro do mapa");
                Assert.IsFalse(c.Agua(t.Ponto.x, t.Ponto.y, out _, 5f), $"a torre em ({t.Ponto.x:F0}; {t.Ponto.y:F0}) fora d'água");
                Assert.IsFalse(c.EstradaPerto(t.Ponto.x, t.Ponto.y, 6f, out _, out _, out _, out _), $"a torre em ({t.Ponto.x:F0}; {t.Ponto.y:F0}) fora da estrada");
            }
            int cabos = 0;
            foreach (var p in PecasDaUsina())
            {
                if (!p.Nome.StartsWith("Cabos")) continue;
                cabos++;
                var malha = p.Desenho.ParaMesh("teste");
                var v = malha.vertices;
                for (int k = 0; k < v.Length; k += 7)
                {
                    var w = p.Posicao + p.Giro * v[k];
                    Assert.Greater(w.y - c.Altura(w.x, w.z), 3f, $"{p.Nome}: o cabo em ({w.x:F0}; {w.z:F0}) acima do chão");
                }
                Object.DestroyImmediate(malha);
            }
            Assert.Greater(cabos, 10, "os vãos de cabos");
        }

        // ------------------------------------------------------------------ as estradas (Etapa 5)

        static ChaoDeLakewood.Estrada Estrada(string nome)
        {
            var e = Chao().Estradas.Find(x => x.Via.nome == nome);
            Assert.IsNotNull(e, $"a estrada {nome} (Zombie City Cleaner > Lakewood > Etapa 5)");
            return e;
        }

        [Test]
        public void AsEstradasFormamUmaRedeLigada()
        {
            var c = Chao();
            Assert.GreaterOrEqual(c.Estradas.Count, 20, "as estradas do mapa e as da usina");
            // as estradas ligadas pelos nós: uma rede só
            var grupo = new int[c.Estradas.Count];
            for (int i = 0; i < grupo.Length; i++) grupo[i] = i;
            int Raiz(int i) { while (grupo[i] != i) i = grupo[i] = grupo[grupo[i]]; return i; }
            foreach (var no in c.Nos)
                for (int k = 1; k < no.Pontos.Count; k++) grupo[Raiz(no.Pontos[k].estrada)] = Raiz(no.Pontos[0].estrada);
            for (int e = 0; e < c.Estradas.Count; e++)
                Assert.AreEqual(Raiz(0), Raiz(e), $"{c.Estradas[e].Via.nome} ligada às outras");
            // as cidades e os lugares do plano têm uma estrada até eles
            var m = Mapa();
            float MaisPerto(Vector2 p)
            {
                float melhor = float.MaxValue;
                foreach (var e in c.Estradas) foreach (var q in e.Pontos) melhor = Mathf.Min(melhor, Vector2.Distance(p, q));
                return melhor;
            }
            foreach (var cd in m.cidades) Assert.Less(MaisPerto(cd.centro), 30f, $"a estrada até a {cd.nome}");
            foreach (var nome in new[] { "Lakewood Regional Airport", "Water Treatment Plant", "Pine Ridge State Park", "Fort Crowley", "Crowley Quarry",
                                         "East Lookout", "Westshore Cottages", "North Arm Marina", "Woodcreek Sawmill", "Visitor Center" })
                Assert.Less(MaisPerto(m.lugares.Find(l => l.nome == nome).ponto), 150f, $"a estrada até {nome}");
            // o anel passa pela crista da barragem (as duas estradas da usina) e pela ponte do norte
            Assert.IsTrue(c.Nos.Exists(no => no.Pontos.Exists(p => c.Estradas[p.estrada].Via.nome == "Dam West Road") && no.Pontos.Exists(p => c.Estradas[p.estrada].Via.nome == "West Shore Highway")), "o anel na cabeceira oeste");
            Assert.IsTrue(c.Nos.Exists(no => no.Pontos.Exists(p => c.Estradas[p.estrada].Via.nome == "Lakewood Dam Road") && no.Pontos.Exists(p => c.Estradas[p.estrada].Via.nome == "South Shore Highway")), "o anel na cabeceira leste");
        }

        [Test]
        public void NosCruzamentosAsEstradasFicamNaMesmaAlturaEPlanas()
        {
            var c = Chao();
            Assert.Greater(c.Nos.Count, 10, "os cruzamentos");
            foreach (var no in c.Nos)
            {
                Assert.IsFalse(float.IsNaN(no.Altura), $"a altura do nó em ({no.Posicao.x:F0}; {no.Posicao.y:F0})");
                Assert.GreaterOrEqual(no.Pontos.Count, 2);
                foreach (var (ei, pi) in no.Pontos)
                {
                    var e = c.Estradas[ei];
                    Assert.Less(Vector2.Distance(e.Pontos[pi], no.Posicao), 0.05f, $"{e.Via.nome} passa pelo nó");
                    for (int i = 0; i < e.Pontos.Length; i++)
                        if (Mathf.Abs(e.AoLongo[i] - e.AoLongo[pi]) <= ChaoDeLakewood.RaioDoPatio)
                            Assert.AreEqual(no.Altura, e.Alturas[i], 0.01f, $"{e.Via.nome}: plana em volta do nó em ({no.Posicao.x:F0}; {no.Posicao.y:F0})");
                }
            }
        }

        [Test]
        public void AsEstradasRespeitamARampaESoPassamNaAguaPelaPonte()
        {
            var c = Chao();
            bool lago = false;
            foreach (var e in c.Estradas)
            {
                float rampa = ChaoDeLakewood.RampaDe(e.Via);
                for (int i = 1; i < e.Pontos.Length; i++)
                {
                    float ds = e.AoLongo[i] - e.AoLongo[i - 1];
                    if (ds > 0.5f) Assert.LessOrEqual(Mathf.Abs(e.Alturas[i] - e.Alturas[i - 1]) / ds, rampa + 0.002f, $"{e.Via.nome}: a rampa no ponto {i}");
                    var p = e.Pontos[i];
                    float agua;
                    bool noLago = c.AguaDoLagoEm(p.x, p.y, out agua) && c.Relevo.Altura(p.x, p.y) < agua - 0.5f;
                    if (!noLago)
                    {
                        if (!c.RioMaisPerto(p.x, p.y, 0f, out var rio) || rio.Dist >= rio.Meia) continue;
                        agua = rio.Nivel;
                    }
                    Assert.IsTrue(e.Ponte[i], $"{e.Via.nome}: ponte em ({p.x:F0}; {p.y:F0}), em cima da água");
                    Assert.Greater(e.Alturas[i], agua + 2f, $"{e.Via.nome}: o tabuleiro acima da água");
                    lago |= noLago;
                }
            }
            Assert.IsTrue(lago, "a ponte do norte atravessa o lago");
        }

        [Test]
        public void AsEstradasSoSeEncostamNosCruzamentos()
        {
            // duas estradas a menos que as duas meias larguras (mais 2 m) uma da outra só perto de um nó delas (nada de
            // uma andando por cima da outra, nem de um cruzamento sem o nó)
            var c = Chao();
            for (int a = 0; a < c.Estradas.Count; a++)
            for (int b = a + 1; b < c.Estradas.Count; b++)
            {
                var ea = c.Estradas[a];
                var eb = c.Estradas[b];
                float limite = ea.MeiaLargura + eb.MeiaLargura + 2f;
                var nos = c.Nos.FindAll(no => no.Pontos.Exists(p => p.estrada == a) && no.Pontos.Exists(p => p.estrada == b));
                for (int i = 0; i < ea.Pontos.Length; i += 2)
                for (int j = 0; j < eb.Pontos.Length; j += 2)
                {
                    if ((ea.Pontos[i] - eb.Pontos[j]).sqrMagnitude > limite * limite) continue;
                    bool perto = nos.Exists(no => Vector2.Distance(no.Posicao, ea.Pontos[i]) < 40f);
                    Assert.IsTrue(perto, $"{ea.Via.nome} e {eb.Via.nome} se encostam em ({ea.Pontos[i].x:F0}; {ea.Pontos[i].y:F0}) sem um cruzamento");
                }
            }
        }

        [Test]
        public void AsSaidasChegamNaBeiraDoMapa()
        {
            var m = Mapa();
            foreach (var nome in new[] { "Eastern Highway", "River Valley Road", "Fairview West Road" })
            {
                var e = Estrada(nome);
                var fim = e.Pontos[e.Pontos.Length - 1];
                float beira = Mathf.Min(Mathf.Min(fim.x, m.largura - fim.x), Mathf.Min(fim.y, m.comprimento - fim.y));
                Assert.Less(beira, 10f, $"{nome} chega na beira");
                // e não anda junto dela antes de chegar
                for (int i = 0; i < e.Pontos.Length - 12; i++)
                {
                    var p = e.Pontos[i];
                    Assert.Greater(Mathf.Min(Mathf.Min(p.x, m.largura - p.x), Mathf.Min(p.y, m.comprimento - p.y)), 30f, $"{nome} longe da beira no ponto {i}");
                }
            }
        }

        // a superfície do terreno de perto num passo (os mesmos triângulos do ChaoDeLakewood.Terreno: os pontos a cada
        // "passo" desde o canto do bloco de 64 m e a diagonal trocando de quadrado em quadrado)
        static float TerrenoDePerto(ChaoDeLakewood c, float x, float z, float passo)
        {
            int bx = Mathf.FloorToInt(x / ChaoDeLakewood.TamanhoDoBloco), bz = Mathf.FloorToInt(z / ChaoDeLakewood.TamanhoDoBloco);
            int n = Mathf.RoundToInt(ChaoDeLakewood.TamanhoDoBloco / passo);
            float lx = (x - bx * ChaoDeLakewood.TamanhoDoBloco) / passo, lz = (z - bz * ChaoDeLakewood.TamanhoDoBloco) / passo;
            int i = Mathf.Min((int)lx, n - 1), j = Mathf.Min((int)lz, n - 1);
            float fx = lx - i, fz = lz - j;
            float H(int a, int b) => c.Altura(bx * ChaoDeLakewood.TamanhoDoBloco + a * passo, bz * ChaoDeLakewood.TamanhoDoBloco + b * passo, passo);
            if (((i + j) & 1) == 0)
                return fz >= fx ? H(i, j) * (1 - fz) + H(i, j + 1) * (fz - fx) + H(i + 1, j + 1) * fx
                                : H(i, j) * (1 - fx) + H(i + 1, j) * (fx - fz) + H(i + 1, j + 1) * fz;
            return fx + fz <= 1 ? H(i, j) * (1 - fx - fz) + H(i + 1, j) * fx + H(i, j + 1) * fz
                                : H(i + 1, j + 1) * (fx + fz - 1) + H(i, j + 1) * (1 - fx) + H(i + 1, j) * (1 - fz);
        }

        [Test]
        public void ORelevoDeLongeFicaEmbaixoDoChaoDePerto()
        {
            // 10/11/2026 ("no meio da pista o terreno tá cobrindo ela, mas dá pra passar"): o relevo de longe, sempre
            // desenhado, aparecia por cima da South Shore Highway (até 32 m), da Dam Road e dos pátios da usina, dos canais dos
            // rios e do pé dos penhascos
            var m = Mapa();
            var c = Chao();
            int ruins = 0;
            float pior = 0f;
            Vector2 onde = default;
            void Conferir(float x, float z)
            {
                float d = c.AlturaDeLonge(x, z) - c.Altura(x, z);
                if (d <= 0.01f) return;
                ruins++;
                if (d > pior) { pior = d; onde = new Vector2(x, z); }
            }
            // o mapa todo (fora da grade de conferência) e a transversal de cada estrada, de 2 em 2 m
            for (float z = 3.75f; z < m.comprimento; z += 7.5f)
            for (float x = 3.75f; x < m.largura; x += 7.5f)
                Conferir(x, z);
            foreach (var e in c.Estradas)
                for (int k = 0; k < e.Pontos.Length; k += 2)
                {
                    var d = (e.Pontos[Mathf.Min(k + 1, e.Pontos.Length - 1)] - e.Pontos[Mathf.Max(k - 1, 0)]).normalized;
                    var lado = new Vector2(-d.y, d.x);
                    for (float o = -45f; o <= 45f; o += 2f) Conferir(e.Pontos[k].x + lado.x * o, e.Pontos[k].y + lado.y * o);
                }
            Assert.AreEqual(0, ruins, $"o relevo de longe por cima do chão de perto (o pior: {pior:F2} m em ({onde.x:F0}; {onde.y:F0}))");
            // e ele só desce onde precisa: o resto fica 6 m abaixo do relevo
            Assert.Less(c.ResumoDoLonge.abaixados, 6000, "os vértices abaixados (de 193 mil)");
        }

        [Test]
        public void OTerrenoDePertoNaoCobreAPista()
        {
            // a pista (o meio e as duas beiradas, de 4 em 4 m) contra o terreno de cada passo: no de 2 m (perto, com a
            // colisão) e no de 4 m, nada por cima; nos longe (8 e 16 m, só sem a neblina), uns pontos nas curvas em rampa (o
            // triângulo pega um canto na pista mais acima da curva)
            var c = Chao();
            foreach (var (passo, maximo, fracao) in new[] { (2f, 0.05f, 0f), (4f, 0.1f, 0.0002f), (8f, 0.5f, 0.0002f), (16f, 0.6f, 0.002f) })
            {
                int ruins = 0, total = 0;
                float pior = 0f;
                string onde = "";
                foreach (var e in c.Estradas)
                    for (int k = 0; k < e.Pontos.Length - 1; k++)
                    {
                        if (e.Ponte[k] || e.Ponte[k + 1]) continue;
                        for (float t = 0f; t < 1f; t += 0.5f)
                        {
                            var q = Vector2.Lerp(e.Pontos[k], e.Pontos[k + 1], t);
                            var d = (e.Pontos[k + 1] - e.Pontos[k]).normalized;
                            var lado = new Vector2(-d.y, d.x);
                            float hr = Mathf.Lerp(e.Alturas[k], e.Alturas[k + 1], t);
                            for (int s = -1; s <= 1; s++)
                            {
                                var p = q + lado * (s * e.MeiaLargura);
                                float acima = TerrenoDePerto(c, p.x, p.y, passo) - hr;
                                total++;
                                if (acima <= 0.05f) continue;
                                ruins++;
                                if (acima > pior) { pior = acima; onde = $"{e.Via.nome} ({p.x:F0}; {p.y:F0})"; }
                            }
                        }
                    }
                Assert.LessOrEqual(pior, maximo, $"passo {passo}: o terreno {pior:F2} m acima da pista em {onde}");
                Assert.LessOrEqual(ruins, fracao * total, $"passo {passo}: {ruins} de {total} pontos da pista cobertos");
            }
        }

        [Test]
        public void ACasaDaPartidaFicaNoTerreiroPertoDaEstrada()
        {
            // 10/11/2026 ("coloque para o jogador nascer perto de alguma estrada... uma casa perto da estrada... sem mobília,
            // só com mesas e armas", com os carros e as motos ao lado): a térrea no terreiro plano na frente da estrada (na
            // South Shore até 12/11/2026; desde então, na Sawmill Road, no lote da Woodcreek perto do lago)
            var c = Chao();
            var lugar = c.Partida;
            Assert.IsNotNull(lugar, "a casa da partida no mapa (Zombie City Cleaner > Lakewood > Etapa 5: a casa da partida)");
            Assert.IsTrue(c.EstradaPerto(lugar.Origem.x, lugar.Origem.y, 1f, out float de, out _, out bool ponte, out _) && de < 1f && !ponte, "a estrada na frente da casa");
            Assert.AreEqual("Terrea", lugar.Planta.molde);
            Assert.IsFalse(lugar.Planta.porao, "sem porão");
            // a casa (com a garagem e a varanda), as vagas e os nascimentos dentro do terreiro; os veículos e os nascimentos
            // fora da casa e da pista
            Assert.IsTrue(lugar.Terreiro.Contains(lugar.Casa.min) && lugar.Terreiro.Contains(lugar.Casa.max), $"a casa {lugar.Casa} no terreiro {lugar.Terreiro}");
            var borda = c.Estradas.Find(e => e.Via.nome == c.Mapa.casaDaPartida.estrada).MeiaLargura + 3f;
            foreach (var a in LugarDaPartida.VagasEmA)
            {
                Assert.IsTrue(lugar.Terreiro.Contains(new Vector2(a, LugarDaPartida.LinhaDosVeiculos - 2.5f)) && lugar.Terreiro.Contains(new Vector2(a, LugarDaPartida.LinhaDosVeiculos + 2.5f)), $"a vaga em {a}");
                Assert.Greater(LugarDaPartida.LinhaDosVeiculos - 2.5f, borda, "o veículo fora da pista e do acostamento");
                Assert.Less(LugarDaPartida.LinhaDosVeiculos + 2.5f, lugar.Casa.yMin, "o veículo fora da casa");
            }
            foreach (var a in LugarDaPartida.PartidasEmA)
                Assert.IsTrue(lugar.Terreiro.Contains(new Vector2(a, LugarDaPartida.LinhaDaPartida)) && LugarDaPartida.LinhaDaPartida < lugar.Casa.yMin, $"o nascimento em {a}");
            // o terreiro plano na altura dele, sem grama, sem pinheiros e fora da água
            int pinheiros = 0;
            var plantas = new List<PlantaDaFloresta>();
            var vistos = new HashSet<Vector2Int>();
            for (float d = lugar.Terreiro.yMin + 0.5f; d <= lugar.Terreiro.yMax - 0.5f; d += 1.5f)
            for (float a = lugar.Terreiro.xMin + 0.5f; a <= lugar.Terreiro.xMax - 0.5f; a += 1.5f)
            {
                var p = lugar.NoMapa(a, d);
                // (12/11/2026: na Sawmill Road, a casa perto do lago, a pista desce uns 0,4 m nos 14 m de cada lado; nos 3 m
                // junto do acostamento, o chão acompanha a pista em rampa, e o plano começa depois, antes das vagas)
                float tolerancia = d < borda + 3f ? 0.5f : 0.12f;
                Assert.AreEqual(lugar.Altura, c.Altura(p.x, p.y), tolerancia, $"o chão do terreiro em ({a:F1}; {d:F1})");
                Assert.AreEqual(0f, c.Grama(p.x, p.y), 1e-4f, $"a grama no terreiro em ({a:F1}; {d:F1})");
                Assert.IsFalse(c.Agua(p.x, p.y, out _), "a água no terreiro");
                vistos.Add(new Vector2Int(Mathf.FloorToInt(p.x / ChaoDeLakewood.TamanhoDoBloco), Mathf.FloorToInt(p.y / ChaoDeLakewood.TamanhoDoBloco)));
            }
            foreach (var b in vistos)
            {
                plantas.Clear();
                c.Plantas(b.x, b.y, plantas);
                foreach (var pl in plantas)
                    if (lugar.DistanciaDoTerreiro(pl.Posicao.x, pl.Posicao.z) < 4f) pinheiros++;
            }
            Assert.AreEqual(0, pinheiros, "os pinheiros no terreiro");
        }

        // ------------------------------------------------------------------ as cidades (Etapa 6)

        static List<LoteDoMapa> LotesDe(string cidade)
        {
            var l = Mapa().lotes.FindAll(x => x.cidade == cidade);
            Assert.Greater(l.Count, 0, $"os lotes da {cidade} (Zombie City Cleaner > Lakewood > Etapa 6: a Woodcreek)");
            return l;
        }

        // as linhas do meio das estradas da Etapa 5 e das ruas novas, com a meia largura de cada uma
        static List<(string nome, List<Vector2> pontos, float meia, bool fechada)> LinhasDasRuas()
        {
            var linhas = new List<(string, List<Vector2>, float, bool)>();
            // as estradas da Etapa 5 (as ruas das cidades também estão na rede do chão: entram pelo mapa)
            foreach (var e in Chao().Estradas)
                if (e.Via.tipo != TipoDeVia.Rua) linhas.Add((e.Via.nome, new List<Vector2>(e.Pontos), e.MeiaLargura, false));
            foreach (var r in Mapa().ruas) if (r.pontos.Count >= 2) linhas.Add((r.nome, r.pontos, r.MeiaLargura, r.fechada));
            return linhas;
        }

        [Test]
        public void AWoodcreekTemAsRuasEOsLotesDoPlano()
        {
            var m = Mapa();
            var lotes = LotesDe("Woodcreek");
            int Conta(UsoDoLote t) => lotes.FindAll(l => l.tipo == t).Count;
            Assert.GreaterOrEqual(m.ruas.FindAll(r => r.cidade == "Woodcreek").Count, 6, "as 5 ruas novas e a alça das cabanas");
            Assert.GreaterOrEqual(Conta(UsoDoLote.Casa), 60, "as casas");
            Assert.GreaterOrEqual(Conta(UsoDoLote.Loja), 10, "as lojas da Main Street");
            Assert.AreEqual(1, Conta(UsoDoLote.Posto), "o posto");
            Assert.AreEqual(1, Conta(UsoDoLote.Hospital), "o hospital");
            Assert.AreEqual(1, Conta(UsoDoLote.LojaDeArmas), "a loja de armas");
            Assert.GreaterOrEqual(Conta(UsoDoLote.Cabana), 15, "as cabanas");
            foreach (var nome in new[] { "igreja", "escola", "bombeiros", "guarda florestal", "motel" })
                Assert.IsTrue(lotes.Exists(l => l.tipo == UsoDoLote.Especial && l.nome == nome), $"o lote reservado: {nome}");
            float vazios = Conta(UsoDoLote.Vazio) / (float)(Conta(UsoDoLote.Vazio) + Conta(UsoDoLote.Casa));
            Assert.That(vazios, Is.InRange(0.05f, 0.22f), $"uns um em cada oito lotes de casa vazios ({100f * vazios:F0}%)");
            // o hospital e a loja de armas cabem no lote com o terreno deles
            var h = lotes.Find(l => l.tipo == UsoDoLote.Hospital);
            Assert.GreaterOrEqual(h.largura, PlantaDoHospital.TerrenoNoMundo.width, "a largura do lote do hospital");
            Assert.GreaterOrEqual(h.fundo, PlantaDoHospital.TerrenoNoMundo.height, "o fundo do lote do hospital");
            Assert.AreEqual("Pine Street", h.rua, "o hospital de frente para a Pine Street");
            var a = lotes.Find(l => l.tipo == UsoDoLote.LojaDeArmas);
            Assert.GreaterOrEqual(a.largura, PlantaDaLojaDeArmas.Terreno.width, "a largura do lote da loja de armas");
            Assert.GreaterOrEqual(a.fundo, PlantaDaLojaDeArmas.Terreno.height, "o fundo do lote da loja de armas");
            // a loja de armas na entrada sul, do lado leste da Main Street, entre o posto e o comércio
            var posto = lotes.Find(l => l.tipo == UsoDoLote.Posto);
            Assert.AreEqual("East Shore Highway", a.rua);
            Assert.Greater(a.meio.y, posto.meio.y, "a loja de armas ao norte do posto");
            foreach (var l in lotes)
                if (l.tipo == UsoDoLote.Loja) Assert.Greater(l.meio.y, a.meio.y, "o comércio ao norte da loja de armas");
            Assert.Greater(Vector2.Dot(a.Frente, Vector2.left), 0.7f, "a frente da loja de armas para o oeste (a rodovia)");
            // as sementes saem do lugar: quase todas diferentes
            var sementes = new HashSet<int>();
            foreach (var l in lotes) sementes.Add(l.semente);
            Assert.Greater(sementes.Count, lotes.Count * 0.95f, "as sementes dos lotes");
        }

        [Test]
        public void OsLotesDasCidadesFicamNoLugar()
        {
            var m = Mapa();
            var c = Chao();
            Assert.Greater(m.lotes.Count, 0, "os lotes (Zombie City Cleaner > Lakewood > Etapa 6)");
            var linhas = LinhasDasRuas();
            var erros = new List<string>();
            foreach (var l in m.lotes)
            {
                string qual = $"{l.cidade}: {l.Descricao} em ({l.meio.x:F0}; {l.meio.y:F0})";
                var cidade = m.cidades.Find(x => x.nome == l.cidade);
                Assert.IsNotNull(cidade, qual);
                var contorno = MapaDeLakewood.Contorno(cidade.contorno);
                // (o acampamento dos comerciantes fica fora da cidade, na beira da rodovia: a parte 6 do "O jogo do mundo
                // sorteado no Lakewood", 12/11/2026)
                if (l.tipo != UsoDoLote.Acampamento)
                    foreach (var canto in l.Cantos())
                        if (!Linhas.Dentro(canto, contorno)) erros.Add($"{qual}: fora da cidade");
                // o chão quase plano (os grandes ganham o patamar na parte 2) e longe da água e dos rios
                float desnivel = LotesDeLakewood.Desnivel(c, l, out bool agua);
                float maximo = LotesDeLakewood.Comum(l) ? LotesDeLakewood.DesnivelMaximo : LotesDeLakewood.DesnivelMaximoDosGrandes;
                if (desnivel > maximo + 0.01f) erros.Add($"{qual}: desnível de {desnivel:F1} m");
                if (agua) erros.Add($"{qual}: na água ou perto de um rio");
                // fora das ruas e das estradas
                for (int i = 0; i <= 8; i++)
                for (int j = 0; j <= 8; j++)
                {
                    var p = l.NoMapa((i / 8f - 0.5f) * l.largura, (j / 8f - 0.5f) * l.fundo);
                    foreach (var (nome, pontos, meia, fechada) in linhas)
                        if (Linhas.Distancia(p, pontos, fechada) < meia + 1f)
                        {
                            erros.Add($"{qual}: em cima da {nome}");
                            i = j = 9;
                            break;
                        }
                }
                // de frente para a rua dele: a beira da frente perto dela, e a frente olhando para ela
                float melhor = float.MaxValue;
                Vector2 maisPerto = default;
                float meiaDaRua = 0f;
                foreach (var (nome, pontos, meia, fechada) in linhas)
                {
                    if (nome != l.rua) continue;
                    int lados = fechada ? pontos.Count : pontos.Count - 1;
                    for (int s = 0; s < lados; s++)
                    {
                        var pa = pontos[s];
                        var pb = pontos[(s + 1) % pontos.Count];
                        float d = Linhas.DistanciaDoSegmento(l.MeioDaFrente, pa, pb, out float t);
                        if (d >= melhor) continue;
                        melhor = d;
                        maisPerto = Vector2.Lerp(pa, pb, t);
                        meiaDaRua = meia;
                    }
                }
                if (melhor == float.MaxValue) { erros.Add($"{qual}: a rua {l.rua} não existe"); continue; }
                if (melhor > meiaDaRua + 10f) erros.Add($"{qual}: a frente a {melhor - meiaDaRua:F1} m da beira da {l.rua}");
                else if (Vector2.Dot((maisPerto - l.MeioDaFrente).normalized, l.Frente) < 0.7f) erros.Add($"{qual}: de costas ou de lado para a {l.rua}");
            }
            // nenhum em cima do outro
            for (int i = 0; i < m.lotes.Count; i++)
            for (int j = i + 1; j < m.lotes.Count; j++)
                if (LoteDoMapa.Encostam(m.lotes[i], m.lotes[j], 0.5f))
                    erros.Add($"{m.lotes[i].Descricao} em ({m.lotes[i].meio.x:F0}; {m.lotes[i].meio.y:F0}) e {m.lotes[j].Descricao} em ({m.lotes[j].meio.x:F0}; {m.lotes[j].meio.y:F0}) se encostam");
            Assert.IsEmpty(erros, string.Join("\n", erros));
        }

        [Test]
        public void OsPatamaresDosLotesSaoPlanos()
        {
            var c = Chao();
            Assert.Greater(c.Lotes.Count, 0, "os lotes no chão");
            var erros = new List<string>();
            foreach (var l in c.Lotes)
            {
                if (!l.Ocupado) continue;
                Assert.Less(Mathf.Abs(l.Altura - (l.AlturaDaRua - 0.08f)), 0.65f, $"{l.Lote.Descricao}: o patamar perto da altura da rua");
                for (int a = 0; a <= 6; a++)
                for (int b = 0; b <= 6; b++)
                {
                    var p = l.Lote.NoMapa((a / 6f - 0.5f) * l.Lote.largura * 0.99f, (b / 6f - 0.5f) * l.Lote.fundo * 0.99f);
                    float h = c.Altura(p.x, p.y);
                    if (Mathf.Abs(h - l.Altura) > 0.01f) erros.Add($"{l.Lote.Descricao} em ({p.x:F0}; {p.y:F0}): {h:F2} m, o patamar {l.Altura:F2}");
                    if (c.Grama(p.x, p.y) > 0.5f) erros.Add($"{l.Lote.Descricao} em ({p.x:F0}; {p.y:F0}): a grama alta");
                }
            }
            // nenhum pinheiro na construção, na frente dela ou na trilha de um lote ocupado (12/11/2026: os quintais têm umas
            // árvores atrás e dos lados, a pedido)
            var plantas = new List<PlantaDaFloresta>();
            var blocos = new HashSet<Vector2Int>();
            foreach (var l in c.Lotes) blocos.Add(new Vector2Int(Mathf.FloorToInt(l.Lote.meio.x / ChaoDeLakewood.TamanhoDoBloco), Mathf.FloorToInt(l.Lote.meio.y / ChaoDeLakewood.TamanhoDoBloco)));
            foreach (var b in blocos)
            {
                plantas.Clear();
                c.Plantas(b.x, b.y, plantas);
                foreach (var pl in plantas)
                {
                    var lote = c.LoteEm(pl.Posicao.x, pl.Posicao.z, 0f);
                    if (lote == null || !lote.Ocupado) continue;
                    var q = lote.Lote.NoLote(new Vector2(pl.Posicao.x, pl.Posicao.z));
                    bool naObra = lote.DistanciaDaConstrucao(pl.Posicao.x, pl.Posicao.z) < 3.5f;
                    bool naFrente = lote.Lote.tipo != UsoDoLote.Cabana && q.y < lote.Construcao.yMin + 0.5f;
                    bool naTrilha = c.DistanciaDaTrilha(pl.Posicao.x, pl.Posicao.z, 3f) < 2f;
                    bool semQuintal = lote.Lote.tipo != UsoDoLote.Casa && lote.Lote.tipo != UsoDoLote.Cabana && lote.Lote.tipo != UsoDoLote.Especial;
                    if (naObra || naFrente || naTrilha || semQuintal)
                        erros.Add($"um pinheiro no lote {lote.Lote.Descricao} em ({pl.Posicao.x:F0}; {pl.Posicao.z:F0}){(naObra ? ", na construção" : "")}{(naFrente ? ", na frente" : "")}{(naTrilha ? ", na trilha" : "")}{(semQuintal ? ", sem quintal" : "")}");
                }
            }
            Assert.IsEmpty(erros, string.Join("\n", erros.GetRange(0, Mathf.Min(erros.Count, 40))));
        }

        [Test]
        public void AsRuasDasCidadesSeLigamNasEstradasESaoSecas()
        {
            var m = Mapa();
            var c = Chao();
            Assert.Greater(m.ruas.Count, 0, "as ruas novas (Zombie City Cleaner > Lakewood > Etapa 6)");
            var linhas = LinhasDasRuas();
            int estradas = c.Estradas.Count;
            // ligadas: a ponta de uma em cima da linha de outra; as estradas da Etapa 5 já formam uma rede só
            var grupo = new int[linhas.Count];
            for (int i = 0; i < grupo.Length; i++) grupo[i] = i < estradas ? 0 : i;
            int Raiz(int i) { while (grupo[i] != i) i = grupo[i] = grupo[grupo[i]]; return i; }
            for (int i = estradas; i < linhas.Count; i++)
            {
                var (nome, pontos, meia, fechada) = linhas[i];
                var pontas = fechada ? pontos : new List<Vector2> { pontos[0], pontos[pontos.Count - 1] };
                for (int j = 0; j < linhas.Count; j++)
                {
                    if (j == i) continue;
                    foreach (var p in pontas)
                        if (Linhas.Distancia(p, linhas[j].pontos, linhas[j].fechada) < 0.5f) grupo[Raiz(i)] = Raiz(j);
                    if (!linhas[j].fechada && j >= estradas)
                        foreach (var p in new[] { linhas[j].pontos[0], linhas[j].pontos[linhas[j].pontos.Count - 1] })
                            if (Linhas.Distancia(p, pontos, fechada) < 0.5f) grupo[Raiz(j)] = Raiz(i);
                }
            }
            for (int i = estradas; i < linhas.Count; i++)
                Assert.AreEqual(Raiz(0), Raiz(i), $"a {linhas[i].nome} ligada nas estradas");
            // secas: nenhum ponto na água nem em cima de um rio
            foreach (var r in m.ruas)
            {
                var pts = Linhas.Reamostrar(r.pontos, 3f, r.fechada);
                foreach (var p in pts)
                {
                    float h = c.Altura(p.x, p.y);
                    Assert.IsFalse(c.Agua(p.x, p.y, out float nivel) && nivel > h, $"a {r.nome} na água em ({p.x:F0}; {p.y:F0})");
                    Assert.IsFalse(c.RioMaisPerto(p.x, p.y, r.MeiaLargura + 2f, out _), $"a {r.nome} em cima de um rio em ({p.x:F0}; {p.y:F0})");
                }
            }
        }

        [Test]
        public void OsEspeciaisEAsCabanasTemPlantaCabemEMontam()
        {
            // Plano_Lakewood, Etapa 6, a parte 6: a igreja, a escola, os bombeiros, a guarda florestal, o motel e as cabanas
            // pelo kit das casas (o EspeciaisDeLakewood), cada um dentro do lote dele e montando sem erro
            var m = Mapa();
            var planos = ObrasDeLakewood.Planejar(m, "Woodcreek");
            var tipos = new HashSet<string>();
            int cabanas = 0;
            for (int i = 0; i < m.lotes.Count; i++)
            {
                var l = m.lotes[i];
                if (l.cidade != "Woodcreek" || (l.tipo != UsoDoLote.Especial && l.tipo != UsoDoLote.Cabana)) continue;
                Assert.IsTrue(planos.TryGetValue(i, out var p), $"o lote {i} ({l.Descricao}) sem planta: não coube");
                var r = p.Pegada;
                Assert.IsTrue(r.xMin >= -l.largura / 2 && r.xMax <= l.largura / 2 && r.yMin >= -l.fundo / 2 && r.yMax <= l.fundo / 2,
                    $"o {p.Nome} do lote {i} sai do lote ({r} num de {l.largura} x {l.fundo})");
                if (l.tipo == UsoDoLote.Cabana) cabanas++;
                else tipos.Add(p.Especial);
                var o = ObrasDeLakewood.Montar(p);
                Assert.Greater(o.Desenho.Triangulos, 1000, $"o {p.Nome} do lote {i} saiu vazio");
                Assert.Greater(o.Casa.Colisao.Triangulos, 100, $"o {p.Nome} do lote {i} sem colisão");
                Assert.Greater(o.Casa.Portas.Count, 0, $"o {p.Nome} do lote {i} sem as portas que abrem");
            }
            foreach (var t in new[] { "igreja", "escola", "bombeiros", "guarda florestal", "motel" })
                Assert.IsTrue(tipos.Contains(t), $"a Woodcreek sem o especial \"{t}\"");
            Assert.AreEqual(26, cabanas, "as cabanas do Cabin Loop");
        }
    }
}
