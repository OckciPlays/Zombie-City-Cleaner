using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O esboço do Condado de Lakewood (Plano_Lakewood, Etapas 1 e 2): o que foi lido à mão da imagem de referência
    /// (`Imgs/Lakewood/Lakewood_Referencia.webp` da pasta de arte, com a moldura de x 32 a 1264 e y 29 a 984: 1 px ≈ 9,7 ×
    /// 10,5 m) e do prompt do usuário: o tamanho, os setores, as zonas de relevo, os picos, os rios, a barragem, as
    /// cidades (as áreas reservadas), os lugares, os túneis reservados e a partida. O lago, as ilhas e a cobertura vêm da
    /// leitura da imagem (<see cref="LeituraDaReferencia"/>) e este esboço não mexe neles. Menu Zombie City Cleaner >
    /// Lakewood > 1. Refazer o esboço do mapa. Depois disso, o asset é a fonte: o que for mexido na janela do desenho se
    /// perde se o esboço for refeito.
    /// </summary>
    public static class EsbocoDeLakewood
    {
        /// <summary>
        /// O colo da cabeceira oeste da barragem (Plano_Lakewood, Etapa 4): a passagem do relevo a 166 m, da ponta da crista até
        /// a planície do oeste, por onde a Dam West Road sai (a crista fica 55 m abaixo do morro da cabeceira).
        /// </summary>
        public static PassagemDoMapa ColoDaBarragem() =>
            new PassagemDoMapa { nome = "Dam West Saddle", pontos = new List<Vector2>(UsinaDeLakewood.ColoOeste), largura = 100f, altura = 166f };

        /// <summary>O fim do túnel Lab – Dam: o prédio da galeria de serviço da usina (Etapa 8).</summary>
        public static readonly Vector2 FimDoTunelDaBarragem = new Vector2(4745f, 2620f);

        static List<Vector2> P(params float[] v)
        {
            var l = new List<Vector2>(v.Length / 2);
            for (int i = 0; i + 1 < v.Length; i += 2) l.Add(new Vector2(v[i], v[i + 1]));
            return l;
        }

        public static void Preencher(MapaDeLakewood m)
        {
            m.nome = "Lakewood County";
            m.largura = 12000f;
            m.comprimento = 10000f;
            m.setoresX = 4;
            m.setoresZ = 4;
            m.semente = 1;
            m.partida = new Vector2(8600f, 3700f);
            m.celulaDaGrade = 25f;

            // ------------------------------------------------------------------ o relevo (as cotas do prompt: a água a
            // 150 m, o campo de 160 a 260, as colinas de 220 a 400, as montanhas até 600 a 650, o vale abaixo da barragem
            // de 90 a 110)
            var r = m.relevo;
            r.altitudeBase = 185f;
            r.amplitude = 30f;
            r.escala = 2200f;
            r.alturaDasMontanhas = 230f;
            r.asperezaDasMontanhas = 45f;
            r.suavidadeDasMontanhas = 220f;
            r.aberturaDosVales = 300f;
            r.raioDoAplainamento = 250f;
            r.zonas = new List<ZonaDeRelevo>
            {
                new ZonaDeRelevo { nome = "West Plains", contorno = P(0, 0, 3300, 0, 3250, 2300, 2800, 4300, 3000, 5500, 3100, 7300, 3500, 10000, 0, 10000),
                                   altitude = 176f, amplitude = 16f, escala = 1400f, aspereza = 0f, fatorDasMontanhas = 0.35f, borda = 900f },
                new ZonaDeRelevo { nome = "North Range", contorno = P(3500, 10000, 9600, 10000, 9600, 8100, 8300, 8200, 6600, 8400, 5200, 8800, 4700, 9250, 3500, 9500),
                                   altitude = 300f, amplitude = 80f, escala = 1600f, aspereza = 45f, fatorDasMontanhas = 1f, borda = 900f },
                new ZonaDeRelevo { nome = "Crowley Mountains", contorno = P(9600, 10000, 12000, 10000, 12000, 7300, 10900, 7300, 9900, 8000, 9600, 8600),
                                   altitude = 340f, amplitude = 80f, escala = 1500f, aspereza = 50f, fatorDasMontanhas = 1.1f, borda = 800f },
                new ZonaDeRelevo { nome = "East Hills", contorno = P(9300, 7300, 10900, 7300, 12000, 7300, 12000, 1400, 11400, 1400, 11600, 2500, 11750, 4900, 10800, 5000, 9500, 5600, 9100, 6400),
                                   altitude = 245f, amplitude = 70f, escala = 1500f, aspereza = 25f, fatorDasMontanhas = 0.9f, borda = 800f },
                new ZonaDeRelevo { nome = "Southeast Ridge", contorno = P(7800, 0, 9100, 0, 9000, 1900, 8400, 1500, 7900, 600),
                                   altitude = 235f, amplitude = 60f, escala = 1400f, aspereza = 35f, fatorDasMontanhas = 1f, borda = 600f },
                new ZonaDeRelevo { nome = "Southeast Corner", contorno = P(10600, 0, 12000, 0, 12000, 1900, 11300, 1900, 10700, 1300),
                                   altitude = 260f, amplitude = 60f, escala = 1400f, aspereza = 40f, fatorDasMontanhas = 1f, borda = 600f },
                new ZonaDeRelevo { nome = "Woodcreek Bench", contorno = P(9300, 7400, 10300, 7600, 10800, 7200, 10800, 6300, 10300, 5900, 9400, 6100, 9200, 6700),
                                   altitude = 190f, amplitude = 14f, escala = 900f, aspereza = 0f, fatorDasMontanhas = 0.2f, borda = 500f },
                new ZonaDeRelevo { nome = "South Hills", contorno = P(3300, 0, 7800, 0, 7900, 600, 8400, 1500, 8300, 2200, 7000, 2600, 5600, 2500, 5000, 2350, 4300, 2300, 3300, 2300),
                                   altitude = 195f, amplitude = 40f, escala = 1500f, aspereza = 10f, fatorDasMontanhas = 0.8f, borda = 700f },
                new ZonaDeRelevo { nome = "Dam West Abutment", contorno = P(3650, 2650, 4150, 2600, 4250, 3050, 3950, 3250, 3600, 3150),
                                   altitude = 232f, amplitude = 10f, escala = 800f, aspereza = 18f, fatorDasMontanhas = 0f, borda = 250f },
                new ZonaDeRelevo { nome = "Dam East Abutment", contorno = P(4950, 2550, 5450, 2550, 5550, 3000, 5300, 3100, 5000, 2900),
                                   altitude = 236f, amplitude = 10f, escala = 800f, aspereza = 18f, fatorDasMontanhas = 0f, borda = 250f },
                new ZonaDeRelevo { nome = "Powerhouse Yard", contorno = P(4300, 2250, 4950, 2250, 4950, 2850, 4350, 2900),
                                   altitude = 112f, amplitude = 2f, escala = 500f, aspereza = 0f, fatorDasMontanhas = 0f, borda = 150f },
            };
            r.picos = new List<PicoDoMapa>
            {
                new PicoDoMapa { nome = "Mount Crowley", ponto = new Vector2(10990f, 9520f), subida = 40f, raio = 900f, rotulo = new Vector2(0f, -380f) },
                new PicoDoMapa { nome = "Pine Ridge", ponto = new Vector2(5900f, 9550f), subida = 35f, raio = 800f, rotulo = new Vector2(0f, -330f) },
            };
            // o colo da cabeceira oeste da barragem (Etapa 4, 10/11/2026): por onde a estrada da crista sai para a planície
            r.passagens = new List<PassagemDoMapa> { ColoDaBarragem() };

            // ------------------------------------------------------------------ os rios (da nascente à foz)
            m.rios = new List<RioDoMapa>
            {
                new RioDoMapa { nome = "Pine River", pontos = P(7237, 10000, 7383, 9466, 7481, 8838, 7300, 8450, 7120, 8150, 7000, 7800, 6970, 7500, 6960, 7200),
                                larguraNoComeco = 12f, larguraNoFim = 110f, nivelPeloRelevo = true, posicaoDoRotulo = 0.3f },
                new RioDoMapa { nome = "Riverside Creek", pontos = P(4412, 10000, 4510, 9466, 4607, 8941, 4753, 8522, 4950, 8150),
                                larguraNoComeco = 7f, larguraNoFim = 22f, nivelPeloRelevo = true, posicaoDoRotulo = 0.35f },
                new RioDoMapa { nome = "Wood Creek", pontos = P(10597, 8208, 10013, 7580, 9526, 6952, 9250, 6700),
                                larguraNoComeco = 5f, larguraNoFim = 14f, nivelPeloRelevo = true },
                new RioDoMapa { nome = "Fairview Creek", pontos = P(273, 7161, 1149, 7056, 2123, 7161, 2902, 6952, 3500, 6750),
                                larguraNoComeco = 5f, larguraNoFim = 14f, nivelPeloRelevo = true, posicaoDoRotulo = 0.3f },
                new RioDoMapa { nome = "Sullivan Creek", pontos = P(175, 4439, 1052, 4649, 1734, 5068, 2200, 5380),
                                larguraNoComeco = 5f, larguraNoFim = 14f, nivelPeloRelevo = true, posicaoDoRotulo = 0.4f },
                new RioDoMapa { nome = "Lookout Creek", pontos = P(11200, 5450, 10500, 5700, 9800, 5800, 9180, 5745),
                                larguraNoComeco = 4f, larguraNoFim = 12f, nivelPeloRelevo = true, posicaoDoRotulo = 0.2f },
                new RioDoMapa { nome = "Lakewood River", pontos = P(4620, 2860, 4570, 2500, 4530, 1900, 4380, 1350, 4330, 880, 4430, 360, 4500, 0),
                                larguraNoComeco = 40f, larguraNoFim = 55f, nivelPeloRelevo = false, nivelNoComeco = 106f, nivelNoFim = 84f, curva = 1f, posicaoDoRotulo = 0.55f },
            };

            // ------------------------------------------------------------------ a barragem, na saída sudoeste do reservatório
            m.represa = new RepresaDoMapa
            {
                nome = "Lakewood Dam",
                a = new Vector2(4150f, 3130f),
                b = new Vector2(5000f, 2900f),
                alturaDaCrista = 158f,
                larguraDaCrista = 12f,
                alturaDoPe = 108f,
                funilOeste = P(3620, 3470, 3800, 3380, 3980, 3270),
                funilLeste = P(5200, 3050, 5420, 3250, 5680, 3450),
                recorte = new Rect(3550f, 2500f, 2200f, 970f),
            };
            // o vale afogado: o rio de antes da barragem, do braço norte, pelo leste da Ilha Central, até a barragem
            m.canalDoReservatorio = P(7150, 7250, 7050, 6400, 7050, 5600, 7000, 4900, 6700, 4450, 6200, 4250, 5700, 3950, 4950, 3350, 4575, 3015);
            m.larguraDoCanal = 450f;
            m.funduraDoCanal = 26f;

            // ------------------------------------------------------------------ as cidades (as áreas reservadas; os pontos vêm
            // das manchas urbanas da imagem)
            m.cidades = new List<CidadeDoMapa>
            {
                new CidadeDoMapa { nome = "Lakewood", descricao = "A cidade principal, na margem sudeste: o centro, os bairros, o hospital, a zona industrial e a marina.",
                                   tipo = TipoDeCidade.Principal, centro = new Vector2(9820f, 3500f), giro = 8f, aplainar = 0.85f, borda = 400f, rotulo = new Vector2(0f, 560f),
                                   contorno = P(8357, 4439, 9039, 4753, 10013, 4806, 10889, 4596, 11376, 4020, 11474, 3183, 11182, 2345, 10597, 2031, 9720, 2031, 9039, 2345, 8649, 2973, 8260, 3602, 8162, 4020) },
                new CidadeDoMapa { nome = "Riverside", descricao = "A cidade turística, na margem noroeste: o hotel, os restaurantes, a marina e as casas de veraneio.",
                                   tipo = TipoDeCidade.Pequena, centro = new Vector2(4022f, 8470f), giro = -15f, aplainar = 0.8f, borda = 300f,
                                   contorno = P(3390, 8941, 3877, 9203, 4461, 9098, 4704, 8522, 4558, 7946, 4071, 7737, 3584, 7894, 3292, 8365) },
                new CidadeDoMapa { nome = "Fairview", descricao = "A cidade agrícola do oeste: o centro pequeno, o mercado, o posto, os silos e os armazéns.",
                                   tipo = TipoDeCidade.Pequena, centro = new Vector2(1782f, 6324f), giro = 0f, aplainar = 0.85f, borda = 300f,
                                   contorno = P(955, 6743, 1636, 6900, 2513, 6795, 2659, 6219, 2416, 5800, 1539, 5748, 906, 6114) },
                new CidadeDoMapa { nome = "Woodcreek", descricao = "A vila florestal do nordeste: as casas de madeira, a serraria, o posto e os chalés.",
                                   tipo = TipoDeCidade.Vila, centro = new Vector2(9964f, 6690f), giro = 20f, aplainar = 0.85f, borda = 300f,
                                   contorno = P(9526, 7266, 10110, 7423, 10548, 7161, 10597, 6428, 10208, 6062, 9526, 6219, 9331, 6743) },
                new CidadeDoMapa { nome = "Sullivan Farms", descricao = "A comunidade rural do sudoeste: as fazendas, os celeiros, os silos e as pastagens.",
                                   tipo = TipoDeCidade.Rural, centro = new Vector2(1393f, 2188f), giro = 0f, aplainar = 0.6f, borda = 500f,
                                   contorno = P(614, 2869, 1636, 3026, 2221, 2659, 2172, 1612, 1539, 1298, 662, 1508) },
            };

            // ------------------------------------------------------------------ os lugares
            m.lugares = new List<LugarDoMapa>
            {
                new LugarDoMapa { nome = "Lakewood Hydroelectric Plant", tipo = TipoDeLugarDoMapa.Usina, ponto = new Vector2(4800f, 2600f), raio = 300f, rotulo = new Vector2(0f, -330f) },
                new LugarDoMapa { nome = "Central Island Laboratory", tipo = TipoDeLugarDoMapa.Laboratorio, ponto = new Vector2(6125f, 5180f), raio = 350f, rotulo = new Vector2(0f, -520f) },
                new LugarDoMapa { nome = "Lakewood Regional Airport", tipo = TipoDeLugarDoMapa.Aeroporto, ponto = new Vector2(10050f, 985f), raio = 1000f, aplainar = 0.95f, rotulo = new Vector2(0f, -330f) },
                new LugarDoMapa { nome = "Water Treatment Plant", tipo = TipoDeLugarDoMapa.EstacaoDeTratamento, ponto = new Vector2(6300f, 1150f), raio = 260f, aplainar = 0.9f, rotulo = new Vector2(0f, -330f) },
                new LugarDoMapa { nome = "Pine Ridge State Park", tipo = TipoDeLugarDoMapa.Parque, ponto = new Vector2(5700f, 9300f), raio = 400f, aplainar = 0.4f, rotulo = new Vector2(0f, -470f) },
                new LugarDoMapa { nome = "Visitor Center", tipo = TipoDeLugarDoMapa.CentroDeVisitantes, ponto = new Vector2(6900f, 8050f), raio = 150f, aplainar = 0.6f },
                new LugarDoMapa { nome = "Fort Crowley", tipo = TipoDeLugarDoMapa.BaseMilitar, ponto = new Vector2(8900f, 9100f), raio = 380f, aplainar = 0.85f, rotulo = new Vector2(0f, -450f) },
                new LugarDoMapa { nome = "Riverside Marina", tipo = TipoDeLugarDoMapa.Marina, ponto = new Vector2(4600f, 8050f), raio = 120f },
                new LugarDoMapa { nome = "Lakewood Marina", tipo = TipoDeLugarDoMapa.Marina, ponto = new Vector2(8370f, 3790f), raio = 150f },
                new LugarDoMapa { nome = "North Arm Marina", tipo = TipoDeLugarDoMapa.Marina, ponto = new Vector2(8520f, 7260f), raio = 100f },
                new LugarDoMapa { nome = "Camp Island", tipo = TipoDeLugarDoMapa.Acampamento, ponto = new Vector2(4400f, 6520f), raio = 250f, rotulo = new Vector2(0f, 300f) },
                new LugarDoMapa { nome = "Antenna Island", tipo = TipoDeLugarDoMapa.Antena, ponto = new Vector2(7800f, 6460f), raio = 150f, rotulo = new Vector2(0f, 300f) },
                new LugarDoMapa { nome = "Cottage Island", tipo = TipoDeLugarDoMapa.CasasDeLago, ponto = new Vector2(7940f, 4650f), raio = 250f, rotulo = new Vector2(0f, 300f) },
                new LugarDoMapa { nome = "Research Island", tipo = TipoDeLugarDoMapa.EstacaoAmbiental, ponto = new Vector2(4055f, 4220f), raio = 250f, rotulo = new Vector2(0f, 300f) },
                new LugarDoMapa { nome = "Woodcreek Sawmill", tipo = TipoDeLugarDoMapa.Serraria, ponto = new Vector2(9500f, 6150f), raio = 150f },
                new LugarDoMapa { nome = "Crowley Quarry", tipo = TipoDeLugarDoMapa.Pedreira, ponto = new Vector2(11280f, 8100f), raio = 250f, aplainar = 0.5f },
                new LugarDoMapa { nome = "East Lookout", tipo = TipoDeLugarDoMapa.Mirante, ponto = new Vector2(11300f, 5100f), raio = 60f },
                new LugarDoMapa { nome = "North Crossing", tipo = TipoDeLugarDoMapa.Ponte, ponto = new Vector2(6330f, 7780f), raio = 100f },
                new LugarDoMapa { nome = "Westshore Cottages", tipo = TipoDeLugarDoMapa.CasasDeLago, ponto = new Vector2(2440f, 4100f), raio = 200f, aplainar = 0.4f },
            };

            // ------------------------------------------------------------------ a rede subterrânea (Etapa 8: só a reserva)
            var lab = new Vector2(6125f, 5180f);
            m.tuneis = new List<TunelDoMapa>
            {
                new TunelDoMapa { nome = "Lab – Dam", pontos = new List<Vector2> { lab, new Vector2(5500f, 3900f), FimDoTunelDaBarragem } },
                new TunelDoMapa { nome = "Lab – Lakewood Hospital", pontos = new List<Vector2> { lab, new Vector2(8200f, 4700f), new Vector2(10160f, 4160f) } },
                new TunelDoMapa { nome = "Lab – Fairview", pontos = new List<Vector2> { lab, new Vector2(4000f, 5800f), new Vector2(1782f, 6324f) } },
                new TunelDoMapa { nome = "Lab – Woodcreek", pontos = new List<Vector2> { lab, new Vector2(8000f, 6000f), new Vector2(9964f, 6690f) } },
                new TunelDoMapa { nome = "Lab – Riverside", pontos = new List<Vector2> { lab, new Vector2(5200f, 6900f), new Vector2(4022f, 8470f) } },
            };

            m.estradas ??= new List<ViaDoMapa>();
            m.pontes ??= new List<PonteDoMapa>();
            m.lagos ??= new List<LagoDoMapa>();
            m.ilhas ??= new List<IlhaDoMapa>();
        }
    }
}
