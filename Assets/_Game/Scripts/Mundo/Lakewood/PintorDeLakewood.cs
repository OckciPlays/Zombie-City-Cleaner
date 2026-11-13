using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Um nome do mapa pintado: o texto, o meio dele, a altura das letras em metros e o giro (graus, anti-horário).</summary>
    public struct RotuloDoMapa
    {
        public string Texto;
        public Vector2 Ponto;
        public float Tamanho;
        public float Giro;
        public FontStyle Estilo;
        public Color Cor;
        /// <summary>Letras espaçadas (o reservatório: "L A K E W O O D   R E S E R V O I R").</summary>
        public bool Espacado;
    }

    /// <summary>Um escudo de estrada no mapa (o número vai por cima, escrito por quem mostra).</summary>
    public struct EscudoDoMapa
    {
        public Vector2 Ponto;
        public string Numero;
        public bool Rodovia;
    }

    /// <summary>
    /// Pinta o mapa de Lakewood como imagem: o relevo sombreado de noroeste, a cobertura da imagem de referência (a mata, o
    /// campo, as lavouras em retalhos, a rocha e a neve das montanhas), o reservatório pela fundura, as ilhas, os rios na
    /// largura de verdade, a barragem, as cidades (a área reservada, com o sinal das ruas no giro de cada uma), as vias e
    /// as pontes (Etapa 5), os túneis reservados (Etapa 8), a grade dos setores, os picos, os ícones dos lugares e os
    /// escudos. Os nomes não vão na imagem: <see cref="Rotulos"/> e <see cref="Escudos"/> dizem onde ficam, e quem mostra
    /// escreve por cima. Código puro: pode rodar em outra thread. A imagem cobre o mapa inteiro, com a linha 0 no sul.
    /// </summary>
    public static class PintorDeLakewood
    {
        public static readonly Color MataEscura = new Color(0.2f, 0.3f, 0.18f), MataClara = new Color(0.31f, 0.42f, 0.25f);
        public static readonly Color Rocha = new Color(0.5f, 0.49f, 0.46f), Neve = new Color(0.92f, 0.94f, 0.96f);
        public static readonly Color AguaRasa = new Color(0.25f, 0.45f, 0.58f), AguaFunda = new Color(0.13f, 0.27f, 0.39f);
        public static readonly Color Margem = new Color(0.14f, 0.29f, 0.39f);
        public static readonly Color[] Campos =
        {
            new Color(0.66f, 0.62f, 0.4f), new Color(0.55f, 0.58f, 0.35f), new Color(0.72f, 0.66f, 0.45f), new Color(0.5f, 0.52f, 0.3f),
        };
        public static readonly Color CampoCor = new Color(0.42f, 0.5f, 0.27f), CampoClaro = new Color(0.52f, 0.58f, 0.32f);
        public static readonly Color MargemRasa = new Color(0.45f, 0.42f, 0.33f);
        public static readonly Color Urbano = new Color(0.62f, 0.6f, 0.55f);
        public static readonly Color Rua = new Color(0.86f, 0.84f, 0.79f);
        public static readonly Color Contorno = new Color(0.23f, 0.22f, 0.2f);
        public static readonly Color RodoviaCor = new Color(0.91f, 0.57f, 0.23f), RodoviaBorda = new Color(0.29f, 0.17f, 0.07f);
        public static readonly Color SecundariaCor = new Color(0.93f, 0.92f, 0.86f);
        public static readonly Color TerraCor = new Color(0.54f, 0.42f, 0.27f);
        public static readonly Color TunelCor = new Color(0.78f, 0.2f, 0.15f);
        public static readonly Color RepresaCor = new Color(0.74f, 0.73f, 0.69f), RepresaBorda = new Color(0.3f, 0.3f, 0.28f);
        public static readonly Color BloqueadaCor = new Color(0.94f, 0.63f, 0.13f);
        public static readonly Color PonteCor = new Color(0.07f, 0.07f, 0.07f), PonteBorda = new Color(0.96f, 0.95f, 0.91f);
        public static readonly Color SetorCor = new Color(1f, 1f, 1f, 0.32f);
        public static readonly Color PredioDaUsina = new Color(0.36f, 0.36f, 0.35f), PatioDaUsina = new Color(0.56f, 0.55f, 0.51f);
        public static readonly Color LinhaDeTransmissao = new Color(0.18f, 0.18f, 0.2f, 0.85f);

        public static Color CorDaCidade(TipoDeCidade t) => t switch
        {
            TipoDeCidade.Principal => new Color(0.56f, 0.54f, 0.5f),
            TipoDeCidade.Pequena => new Color(0.64f, 0.61f, 0.55f),
            TipoDeCidade.Vila => new Color(0.68f, 0.64f, 0.56f),
            _ => new Color(0.7f, 0.66f, 0.5f),
        };

        /// <summary>O chão dos lotes, a beira deles e as ruas das cidades (Etapa 6).</summary>
        public static readonly Color Quintal = new Color(0.55f, 0.57f, 0.42f), BeiraDoLote = new Color(0.2f, 0.19f, 0.16f);
        public static readonly Color LoteVazio = new Color(0.33f, 0.42f, 0.27f);
        public static readonly Color RuaNova = new Color(0.74f, 0.73f, 0.7f);

        /// <summary>A cor da construção de cada tipo de lote (no mapa, no plano da cidade e na legenda).</summary>
        public static Color CorDoLote(UsoDoLote t) => t switch
        {
            UsoDoLote.Casa => new Color(0.9f, 0.84f, 0.66f),
            UsoDoLote.Loja => new Color(0.92f, 0.58f, 0.36f),
            UsoDoLote.Posto => new Color(0.95f, 0.35f, 0.3f),
            UsoDoLote.Hospital => new Color(0.97f, 0.97f, 0.97f),
            UsoDoLote.LojaDeArmas => new Color(0.6f, 0.4f, 0.68f),
            UsoDoLote.Especial => new Color(0.62f, 0.72f, 0.92f),
            UsoDoLote.Cabana => new Color(0.72f, 0.5f, 0.31f),
            UsoDoLote.Acampamento => new Color(0.45f, 0.75f, 0.42f),
            _ => LoteVazio,
        };

        /// <summary>
        /// Onde fica a construção dentro do lote, só para o desenho (o mapa e o plano): o meio e as meias medidas, nas
        /// coordenadas do lote (x pela frente, z para os fundos). O hospital e a loja de armas com as medidas de verdade (o
        /// lote no meio do terreno deles); os outros por frações do lote. O lote vazio não tem.
        /// </summary>
        public static bool Pegada(LoteDoMapa l, out Vector2 meio, out Vector2 meia)
        {
            switch (l.tipo)
            {
                case UsoDoLote.Vazio:
                    meio = meia = Vector2.zero;
                    return false;
                case UsoDoLote.Hospital:
                    // o corpo de 56 × 32 m, o terreno de x -50,5 a 32 e z -30 (a frente) a 21,5 na obra
                    meio = new Vector2(9.25f, 4.25f);
                    meia = new Vector2(28f, 16f);
                    return true;
                case UsoDoLote.LojaDeArmas:
                    // o prédio de 24 × 30 m, o terreno de x -16 a 16 e z -29,5 a 20 na obra
                    meio = new Vector2(0f, 4.75f);
                    meia = new Vector2(12f, 15f);
                    return true;
            }
            (float fl, float ff, float recuo) = l.tipo switch
            {
                UsoDoLote.Casa => (0.62f, 0.42f, 0.12f),
                UsoDoLote.Loja => (0.86f, 0.8f, 0.06f),
                UsoDoLote.Posto => (0.8f, 0.7f, 0.08f),
                UsoDoLote.Cabana => (0.6f, 0.55f, 0.2f),
                _ => (0.7f, 0.6f, 0.15f),
            };
            meia = new Vector2(fl * l.largura * 0.5f, ff * l.fundo * 0.5f);
            meio = new Vector2(0f, -l.fundo * 0.5f + recuo * l.fundo + meia.y);
            return true;
        }

        /// <summary>A largura de cada via no mapa (m; maior que a de verdade, para ler).</summary>
        public static float LarguraDaVia(TipoDeVia t) => t switch
        {
            TipoDeVia.Rodovia => 42f,
            TipoDeVia.Secundaria => 24f,
            _ => 12f,
        };

        /// <summary>A altura da imagem (px) para a largura pedida, na proporção do mapa.</summary>
        public static int AlturaPara(MapaDeLakewood d, int larguraPx) => Mathf.Max(1, Mathf.RoundToInt(larguraPx * d.comprimento / d.largura));

        public static Color32[] Pintar(MapaDeLakewood d, RelevoDeLakewood relevo, int larguraPx, List<PonteCalculada> pontes = null)
        {
            int alturaPx = AlturaPara(d, larguraPx);
            var p = new Pincel(d, larguraPx, alturaPx);
            var px = p.Px;
            float mpp = p.Mpp;
            int W = larguraPx, H = alturaPx;

            // as máscaras: os lagos (sem as ilhas) e as cidades
            var lago = new short[W * H];
            for (int l = 0; l < d.lagos.Count; l++)
                if (d.lagos[l].contorno.Count >= 3)
                {
                    short valor = (short)(l + 1);
                    p.Varrer(MapaDeLakewood.Contorno(d.lagos[l].contorno), k => lago[k] = valor);
                }
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3) p.Varrer(MapaDeLakewood.Contorno(ilha.contorno), k => lago[k] = 0);

            // o chão: o relevo sombreado com a cobertura, a rocha e a neve; o lago pela fundura
            var tons = new Ruido(d.semente, "tons-do-mapa");
            var luz = new Vector3(-0.55f, 0.63f, 0.55f).normalized;
            Parallel.For(0, H, j =>
            {
                float z = p.Z(j);
                Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    float x = p.X(i);
                    float h = relevo.Altura(x, z);
                    if (lago[k] > 0)
                    {
                        float nivelDoLago = relevo.NivelDosLagos != null ? relevo.NivelDosLagos[lago[k] - 1] : d.lagos[lago[k] - 1].nivel;
                        var ca = Color.Lerp(AguaRasa, AguaFunda, Mathf.Clamp01((nivelDoLago - h) / 18f));
                        ca.a = 1f;
                        px[k] = ca;
                        continue;
                    }
                    float hx = (relevo.Altura(x + mpp, z) - relevo.Altura(x - mpp, z)) / (2f * mpp);
                    float hz = (relevo.Altura(x, z + mpp) - relevo.Altura(x, z - mpp)) / (2f * mpp);
                    var n = new Vector3(-hx * 1.4f, 1f, -hz * 1.4f).normalized;
                    float sombra = 0.45f + 0.75f * Mathf.Max(0f, Vector3.Dot(n, luz));
                    // a cobertura: a mistura das células em volta do ponto deformado pelo ruído (as beiras tortas, sem as
                    // quadras da grade), com a textura das copas e das pastagens por cima
                    float r1 = tons.Fbm(x / 700f, z / 700f, 2);
                    float r3 = tons.Amostra(x / 55f, z / 55f) + 0.5f * tons.Amostra(x / 23f + 9f, z / 23f);
                    var pd = MapaDeLakewood.Deformar(tons, x, z);
                    d.Fracoes(pd.x, pd.y, f);
                    var mata = Color.Lerp(MataEscura, MataClara, Mathf.Clamp01(0.5f + 0.7f * r1)) * (1f + 0.1f * r3);
                    var campo = Color.Lerp(CampoCor, CampoClaro, Mathf.Clamp01(0.5f + 0.8f * r1)) * (1f + 0.05f * r3);
                    ulong hs = Sorteio.Hash(d.semente, Mathf.FloorToInt(x / 300f), Mathf.FloorToInt(z / 220f), 43);
                    var lavoura = (Sorteio.FracaoDoHash(hs) > 0.25f ? Campos[(int)(hs % 4)] : CampoCor) * (1f + 0.04f * r3);
                    var rocha = Color.Lerp(mata, Rocha, 0.75f) * (1f + 0.06f * r3);
                    var margem = Color.Lerp(mata, MargemRasa, 0.6f);   // a margem rasa, fora do contorno
                    var c = mata * f[(int)Cobertura.Mata] + campo * f[(int)Cobertura.Campo] + lavoura * f[(int)Cobertura.Lavoura]
                          + rocha * f[(int)Cobertura.Rocha] + margem * f[(int)Cobertura.Agua] + Urbano * f[(int)Cobertura.Urbano];
                    float inclinacao = Mathf.Sqrt(hx * hx + hz * hz);
                    float pedra = Geo.Suave(420f, 560f, h + r1 * 60f);
                    pedra = Mathf.Max(pedra, Geo.Suave(0.7f, 1.1f, inclinacao) * Geo.Suave(220f, 400f, h));
                    c = Color.Lerp(c, Rocha, pedra);
                    float r2 = tons.Fbm(x / 400f + 50f, z / 400f, 2);
                    c = Color.Lerp(c, Neve, Geo.Suave(615f, 690f, h + r2 * 40f) * (1f - 0.5f * Geo.Suave(1.2f, 1.8f, inclinacao)));
                    c *= sombra;
                    c.a = 1f;
                    px[k] = c;
                }
            });

            // as cidades: a área reservada (fora da água) com o sinal das ruas, e o contorno
            foreach (var cidade in d.cidades)
            {
                if (cidade.contorno.Count < 3) continue;
                var contorno = MapaDeLakewood.Contorno(cidade.contorno);
                if (d.TemLotes(cidade.nome))
                {
                    // com as ruas e os lotes da Etapa 6: a mata fica, o contorno tracejado e as ruas de verdade (os lotes vão
                    // por cima das estradas, mais à frente)
                    p.Linha(contorno, true, 1.2f * mpp, Contorno, 0.6f, 90f, 60f);
                    foreach (var rc in d.ruas)
                    {
                        if (rc.cidade != cidade.nome || rc.pontos.Count < 2) continue;
                        float larg = Mathf.Max(rc.largura + 5f, 1.6f * mpp);
                        p.Linha(rc.pontos, rc.fechada, larg + Mathf.Max(5f, 1.2f * mpp), Contorno, 0.9f);
                        p.Linha(rc.pontos, rc.fechada, larg, RuaNova, 1f);
                    }
                    continue;
                }
                var meio = cidade.centro;
                float giro = cidade.giro * Mathf.Deg2Rad, cos = Mathf.Cos(giro), sen = Mathf.Sin(giro);
                var fundo = CorDaCidade(cidade.tipo);
                float q = cidade.tipo == TipoDeCidade.Principal ? 110f : cidade.tipo == TipoDeCidade.Rural ? 260f : 130f, rua = Mathf.Max(9f, mpp * 0.9f);
                p.Varrer(contorno, k =>
                {
                    if (lago[k] > 0) return;
                    int i = k % W, j = k / W;
                    float rx = p.X(i) - meio.x, rz = p.Z(j) - meio.y;
                    float u = rx * cos + rz * sen, v = -rx * sen + rz * cos;
                    var c = fundo;
                    if (Mathf.Repeat(u + q * 0.5f, q) < rua || Mathf.Repeat(v + q * 0.4f, q * 0.8f) < rua) c = Color.Lerp(Rua, fundo, 0.25f);
                    c = Color.Lerp(px[k], c, 0.8f);
                    c.a = 1f;
                    px[k] = c;
                });
                p.Linha(contorno, true, 1.4f * mpp, Contorno, 0.6f);
            }

            // os rios (com os meandros) na largura de verdade (pelo menos 2 pixels), e a margem dos lagos e das ilhas
            for (int ri = 0; ri < d.rios.Count; ri++)
            {
                var rio = d.rios[ri];
                var linha = relevo.LinhasDosRios[ri];
                if (linha == null) continue;
                p.LinhaVariavel(linha, t => Mathf.Max(MapaDeLakewood.LarguraDoRio(rio, t), 2.2f * mpp), AguaRasa);
            }
            foreach (var lg in d.lagos)
                if (lg.contorno.Count >= 3) p.Linha(MapaDeLakewood.Contorno(lg.contorno), true, Mathf.Max(8f, 1.3f * mpp), Margem, 0.9f);
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3) p.Linha(MapaDeLakewood.Contorno(ilha.contorno), true, Mathf.Max(8f, 1.3f * mpp), new Color(0.79f, 0.76f, 0.63f), 0.9f);

            // a barragem
            if (d.represa.Tem)
            {
                var r = d.represa;
                float meiaLargura = Mathf.Max(22f, 2.2f * mpp);
                p.Caixa(r.Meio, r.Direcao, r.Comprimento * 0.5f + 12f, meiaLargura + Mathf.Max(6f, mpp), RepresaBorda);
                p.Caixa(r.Meio, r.Direcao, r.Comprimento * 0.5f + 6f, meiaLargura, RepresaCor);
            }

            // a hidrelétrica (Etapa 4): os pátios e os prédios, o canal de fuga e as linhas de transmissão (as estradas de
            // acesso vão com as outras)
            var usina = relevo != null ? new UsinaDeLakewood(d, relevo) : null;
            if (usina != null && usina.Tem)
            {
                foreach (var pt in usina.Patios)
                {
                    if (pt.Tipo == UsinaDeLakewood.TipoDoPatio.Superficie || pt.Tipo == UsinaDeLakewood.TipoDoPatio.Corte) continue;
                    var cor = pt.Tipo == UsinaDeLakewood.TipoDoPatio.Edificio ? PredioDaUsina : PatioDaUsina;
                    p.Caixa(usina.Mundo(pt.Area.center.x, pt.Area.center.y), usina.D, Mathf.Max(pt.Area.width * 0.5f, mpp), Mathf.Max(pt.Area.height * 0.5f, mpp), cor);
                }
                foreach (var r in new[] { UsinaDeLakewood.Bacia, UsinaDeLakewood.Poco })
                    p.Caixa(usina.Mundo(r.center.x, r.center.y), usina.D, r.width * 0.5f, r.height * 0.5f, AguaRasa);
                for (int l = 0; l < usina.LinhasDeTransmissao.Count; l++)
                {
                    var pts = new List<Vector2>();
                    foreach (var t in usina.Torres) if (t.Linha == l) pts.Add(t.Ponto);
                    if (pts.Count >= 2) p.Linha(pts, false, Mathf.Max(6f, 0.7f * mpp), LinhaDeTransmissao, 0.85f, 70f, 45f);
                }
            }

            // os túneis reservados (Etapa 8): tracejados em vermelho escuro
            foreach (var t in d.tuneis)
                if (t.pontos.Count >= 2) p.Linha(MapaDeLakewood.Linha(t.pontos), false, Mathf.Max(16f, 1.6f * mpp), TunelCor, 0.85f, 130f, 80f);

            // as vias: as bordas de todas e depois o miolo (os cruzamentos ficam limpos)
            var vias = new List<(List<Vector2> linha, TipoDeVia tipo)>();
            foreach (var e in d.estradas)
                if (e.pontos.Count >= 2) vias.Add((MapaDeLakewood.Linha(e.pontos), e.tipo));
            if (usina != null)
                foreach (var e in usina.Vias)
                    if (e.pontos.Count >= 2) vias.Add((MapaDeLakewood.Linha(e.pontos), e.tipo));
            for (int passada = 0; passada < 2; passada++)
                foreach (var (linha, tipo) in vias)
                {
                    float largura = Mathf.Max(LarguraDaVia(tipo), (tipo == TipoDeVia.Terra ? 1.2f : 1.6f) * mpp);
                    if (tipo == TipoDeVia.Terra)
                    {
                        if (passada == 1) p.Linha(linha, false, largura, TerraCor, 1f, 90f, 60f);
                        continue;
                    }
                    if (passada == 0) p.Linha(linha, false, largura + Mathf.Max(18f, 1.4f * mpp), tipo == TipoDeVia.Rodovia ? RodoviaBorda : Contorno, 0.9f);
                    else p.Linha(linha, false, largura, tipo == TipoDeVia.Rodovia ? RodoviaCor : SecundariaCor, 1f);
                }

            // os lotes das cidades (Etapa 6), por cima das estradas (no mapa elas são mais largas que as de verdade): o chão
            // do lote e a construção, sem os vazios (a mata)
            if (d.lotes != null)
                foreach (var l in d.lotes)
                {
                    if (l.tipo == UsoDoLote.Vazio) continue;
                    p.Caixa(l.meio, l.Lado, l.largura * 0.5f, l.fundo * 0.5f, Quintal);
                    if (!Pegada(l, out var pm, out var pmeia)) continue;
                    var c = l.NoMapa(pm.x, pm.y);
                    p.Caixa(c, l.Lado, pmeia.x + 0.5f * mpp, pmeia.y + 0.5f * mpp, BeiraDoLote);
                    p.Caixa(c, l.Lado, pmeia.x, pmeia.y, CorDoLote(l.tipo));
                }

            // as pontes: o tabuleiro escuro atravessando o rio, com a borda clara (laranja nas bloqueadas)
            foreach (var pt in pontes ?? PontesDeLakewood.Calcular(d, relevo))
            {
                float largura = LarguraDaVia(pt.TipoDaVia) + 10f;
                largura = Mathf.Max(largura, 2.4f * mpp);
                float comprimento = Mathf.Max(pt.Comprimento, 4f * mpp);
                var borda = pt.Estado == EstadoDaPonte.Bloqueada ? BloqueadaCor : PonteBorda;
                float folga = Mathf.Max(pt.Estado == EstadoDaPonte.Bloqueada ? 16f : 10f, 1.3f * mpp);
                p.Caixa(pt.Ponto, pt.Direcao, comprimento * 0.5f + folga, largura * 0.5f + folga, borda);
                p.Caixa(pt.Ponto, pt.Direcao, comprimento * 0.5f, largura * 0.5f, PonteCor);
            }

            // a grade dos setores
            float fina = Mathf.Max(mpp * 1.2f, 4f);
            for (int i = 1; i < d.setoresX; i++)
                p.Linha(new List<Vector2> { new Vector2(i * d.LadoDoSetorX, 0f), new Vector2(i * d.LadoDoSetorX, d.comprimento) }, false, fina, SetorCor, 1f);
            for (int j = 1; j < d.setoresZ; j++)
                p.Linha(new List<Vector2> { new Vector2(0f, j * d.LadoDoSetorZ), new Vector2(d.largura, j * d.LadoDoSetorZ) }, false, fina, SetorCor, 1f);

            // os picos e os lugares
            float icone = Mathf.Max(100f, 7f * mpp);
            foreach (var pc in d.relevo.picos)
            {
                p.Poligono(Triangulo(pc.ponto, icone * 1.6f), Contorno);
                p.Poligono(Triangulo(pc.ponto, icone * 1.25f), new Color(0.96f, 0.96f, 0.96f));
            }
            foreach (var l in d.lugares)
            {
                p.Disco(l.ponto, icone * 1.1f, Color.white);
                p.Disco(l.ponto, icone * 0.95f, new Color(0.13f, 0.15f, 0.17f));
                p.Poligono(Simbolo(l.tipo, l.ponto, icone * 0.6f), Color.white);
            }

            // os escudos (os números vão por cima)
            foreach (var es in Escudos(d))
            {
                if (es.Rodovia)
                {
                    p.Poligono(Escudo(es.Ponto, icone * 1.75f), Color.white);
                    p.Poligono(Escudo(es.Ponto, icone * 1.55f), new Color(0.11f, 0.31f, 0.61f));
                }
                else
                {
                    p.Disco(es.Ponto, icone * 1.45f, new Color(0.13f, 0.13f, 0.13f));
                    p.Disco(es.Ponto, icone * 1.25f, Color.white);
                }
            }

            var saida = new Color32[px.Length];
            for (int k = 0; k < px.Length; k++) saida[k] = px[k];
            return saida;
        }

        // ------------------------------------------------------------------ os nomes e os escudos

        /// <summary>Os nomes do mapa e onde ficam (o relevo dá a altura dos picos; sem ele, eles saem sem a altura).</summary>
        public static List<RotuloDoMapa> Rotulos(MapaDeLakewood d, RelevoDeLakewood relevo)
        {
            var lista = new List<RotuloDoMapa>();
            var branco = Color.white;
            var claro = new Color(0.9f, 0.94f, 0.96f);
            void Por(string texto, Vector2 ponto, float tamanho, FontStyle estilo, Color cor, float giro = 0f, bool espacado = false)
            {
                if (string.IsNullOrEmpty(texto)) return;
                lista.Add(new RotuloDoMapa { Texto = texto, Ponto = ponto, Tamanho = tamanho, Estilo = estilo, Cor = cor, Giro = giro, Espacado = espacado });
            }

            // os setores, pequenos, no canto sudoeste de cada um
            for (int j = 0; j < d.setoresZ; j++)
            for (int i = 0; i < d.setoresX; i++)
                Por(MapaDeLakewood.NomeDoSetor(i, j), new Vector2(i * d.LadoDoSetorX + 160f, j * d.LadoDoSetorZ + 110f), 150f, FontStyle.Bold, new Color(1f, 1f, 1f, 0.55f));

            foreach (var lg in d.lagos)
            {
                if (lg.contorno.Count < 3) continue;
                var ponto = lg.rotulo != Vector2.zero ? lg.rotulo : Linhas.Centro(MapaDeLakewood.Contorno(lg.contorno));
                bool grande = Linhas.Area(MapaDeLakewood.Contorno(lg.contorno)) > 3e6f;
                Por(grande ? lg.nome?.ToUpperInvariant() : lg.nome, ponto, grande ? 330f : 180f, FontStyle.Italic, claro, lg.giroDoRotulo, grande);
            }
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3 && ilha.tipo != TipoDeIlha.Ilhote) Por(ilha.nome, Linhas.Centro(MapaDeLakewood.Contorno(ilha.contorno)) + Vector2.down * 260f, 180f, FontStyle.Normal, branco);
            for (int ri = 0; ri < d.rios.Count; ri++)
            {
                var rio = d.rios[ri];
                if (relevo.LinhasDosRios[ri] == null) continue;
                var ponto = Linhas.PontoNa(relevo.LinhasDosRios[ri], rio.posicaoDoRotulo, out var dir);
                float giro = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (giro > 90f) giro -= 180f;
                else if (giro < -90f) giro += 180f;
                var lado = new Vector2(-Mathf.Sin(giro * Mathf.Deg2Rad), Mathf.Cos(giro * Mathf.Deg2Rad));
                Por(rio.nome, ponto + lado * (MapaDeLakewood.LarguraDoRio(rio, rio.posicaoDoRotulo) * 0.5f + 130f), 160f, FontStyle.Italic, claro, giro);
            }
            foreach (var pc in d.relevo.picos)
            {
                var ponto = pc.ponto + pc.rotulo;
                Por(pc.nome, ponto, 200f, FontStyle.Normal, branco);
                if (relevo != null) Por($"{Mathf.Round(relevo.Maxima(pc.ponto, pc.raio * 0.5f) / 10f) * 10f:0} m", ponto + Vector2.down * 220f, 150f, FontStyle.Normal, new Color(0.88f, 0.88f, 0.88f));
            }
            foreach (var c in d.cidades) Por(c.nome, c.centro + c.rotulo, TamanhoDoNome(c.tipo), FontStyle.Bold, branco);
            foreach (var l in d.lugares) Por(l.nome, l.ponto + l.rotulo, 180f, FontStyle.Normal, branco);
            if (d.represa.Tem) Por(d.represa.nome, d.represa.Meio + (relevo != null ? relevo.Jusante : Vector2.down) * 330f, 170f, FontStyle.Normal, branco);
            return lista;
        }

        /// <summary>A altura das letras do nome de cada tipo de cidade (m).</summary>
        public static float TamanhoDoNome(TipoDeCidade t) => t switch
        {
            TipoDeCidade.Principal => 460f,
            TipoDeCidade.Pequena => 280f,
            TipoDeCidade.Vila => 220f,
            _ => 200f,
        };

        /// <summary>Os escudos das estradas com número: um a cada ~4 km, fora das cidades.</summary>
        public static List<EscudoDoMapa> Escudos(MapaDeLakewood d)
        {
            var lista = new List<EscudoDoMapa>();
            var cidades = new List<List<Vector2>>();
            foreach (var c in d.cidades)
                if (c.contorno.Count >= 3) cidades.Add(MapaDeLakewood.Contorno(c.contorno));
            bool NaCidade(Vector2 ponto)
            {
                foreach (var c in cidades)
                    if (Linhas.Dentro(ponto, c) || Linhas.Distancia(ponto, c, true) < 250f) return true;
                return false;
            }
            foreach (var e in d.estradas)
            {
                if (string.IsNullOrEmpty(e.numero) || e.pontos.Count < 2) continue;
                var linha = MapaDeLakewood.Linha(e.pontos);
                float comprimento = Linhas.Comprimento(linha);
                int n = Mathf.Max(1, Mathf.RoundToInt(comprimento / 4000f));
                for (int s = 0; s < n; s++)
                {
                    float f = (s + 0.5f) / n;
                    Vector2 ponto = Linhas.PontoNa(linha, f, out _);
                    bool livre = !NaCidade(ponto);
                    for (int k = 1; k <= 25 && !livre; k++)
                    for (int lado = -1; lado <= 1 && !livre; lado += 2)
                    {
                        float g = f + lado * k * 0.01f;
                        if (g < 0f || g > 1f) continue;
                        var q = Linhas.PontoNa(linha, g, out _);
                        if (NaCidade(q)) continue;
                        ponto = q;
                        livre = true;
                    }
                    if (!livre) continue;
                    bool junto = false;
                    foreach (var outro in lista) junto |= outro.Numero == e.numero && Vector2.Distance(outro.Ponto, ponto) < 1500f;
                    if (junto || ponto.x < 300f || ponto.y < 300f || ponto.x > d.largura - 300f || ponto.y > d.comprimento - 300f) continue;
                    lista.Add(new EscudoDoMapa { Ponto = ponto, Numero = e.numero, Rodovia = e.tipo == TipoDeVia.Rodovia });
                }
            }
            return lista;
        }

        // ------------------------------------------------------------------ as formas dos ícones (em metros)

        static List<Vector2> Triangulo(Vector2 c, float r) =>
            new List<Vector2> { c + new Vector2(-r, -r * 0.75f), c + new Vector2(r, -r * 0.75f), c + new Vector2(0f, r) };

        static List<Vector2> Escudo(Vector2 c, float r) => new List<Vector2>
        {
            c + new Vector2(-r, r * 0.92f), c + new Vector2(r, r * 0.92f), c + new Vector2(r, -0.1f * r),
            c + new Vector2(0.62f * r, -0.78f * r), c + new Vector2(0f, -1.18f * r), c + new Vector2(-0.62f * r, -0.78f * r),
            c + new Vector2(-r, -0.1f * r),
        };

        static List<Vector2> Simbolo(TipoDeLugarDoMapa t, Vector2 c, float r)
        {
            var l = new List<Vector2>();
            switch (t)
            {
                case TipoDeLugarDoMapa.BaseMilitar:
                    for (int k = 0; k < 10; k++)
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                        float rr = k % 2 == 0 ? r : r * 0.42f;
                        l.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                    }
                    break;
                case TipoDeLugarDoMapa.CasasDeLago: case TipoDeLugarDoMapa.CentroDeVisitantes: case TipoDeLugarDoMapa.Laboratorio: case TipoDeLugarDoMapa.Acampamento:
                    l.AddRange(new[] { c + new Vector2(-0.7f, -0.75f) * r, c + new Vector2(0.7f, -0.75f) * r, c + new Vector2(0.7f, 0.15f) * r,
                                       c + new Vector2(0f, 0.85f) * r, c + new Vector2(-0.7f, 0.15f) * r });
                    break;
                case TipoDeLugarDoMapa.Antena: case TipoDeLugarDoMapa.Mirante: case TipoDeLugarDoMapa.Parque:
                    l.AddRange(new[] { c + new Vector2(-0.55f, -0.9f) * r, c + new Vector2(0.55f, -0.9f) * r, c + new Vector2(0f, 1f) * r });
                    break;
                case TipoDeLugarDoMapa.Aeroporto: case TipoDeLugarDoMapa.Marina: case TipoDeLugarDoMapa.Pedreira: case TipoDeLugarDoMapa.Ponte:
                    l.AddRange(new[] { c + new Vector2(0f, r), c + new Vector2(r, 0f), c + new Vector2(0f, -r), c + new Vector2(-r, 0f) });
                    break;
                case TipoDeLugarDoMapa.Represa:
                    l.AddRange(new[] { c + new Vector2(-r, -0.3f * r), c + new Vector2(r, -0.3f * r), c + new Vector2(r, 0.3f * r), c + new Vector2(-r, 0.3f * r) });
                    break;
                default:   // a usina, a estação, a serraria: o galpão com a chaminé
                    l.AddRange(new[] { c + new Vector2(-0.8f, -0.7f) * r, c + new Vector2(0.8f, -0.7f) * r, c + new Vector2(0.8f, 0.9f) * r,
                                       c + new Vector2(0.45f, 0.9f) * r, c + new Vector2(0.45f, 0.1f) * r, c + new Vector2(-0.8f, 0.1f) * r });
                    break;
            }
            return l;
        }

        // ------------------------------------------------------------------ o pincel (do mundo em metros para os pixels)

        sealed class Pincel
        {
            public readonly int W, H;
            public readonly float Mpp;
            public readonly Color[] Px;

            public Pincel(MapaDeLakewood d, int w, int h)
            {
                W = w;
                H = h;
                Mpp = d.largura / w;
                Px = new Color[w * h];
            }

            public float X(int i) => (i + 0.5f) * Mpp;
            public float Z(int j) => (j + 0.5f) * Mpp;
            Vector2 NoPixel(Vector2 p) => p / Mpp - Vector2.one * 0.5f;

            public void Varrer(List<Vector2> poligono, System.Action<int> cada) =>
                Linhas.Varrer(poligono, X(0), Z(0), Mpp, W, H, (j, i0, i1) =>
                {
                    for (int i = i0; i <= i1; i++) cada(j * W + i);
                });

            public void Poligono(List<Vector2> poligono, Color cor) =>
                Varrer(poligono, k =>
                {
                    var c = Color.Lerp(Px[k], cor, cor.a);
                    c.a = 1f;
                    Px[k] = c;
                });

            void Misturar(int k, Color cor, float alfa)
            {
                if (alfa <= 0f) return;
                var c = Color.Lerp(Px[k], cor, Mathf.Clamp01(alfa) * cor.a);
                c.a = 1f;
                Px[k] = c;
            }

            /// <summary>Uma linha com a largura em metros, e o traço opcional (ligado e desligado, em metros; 0 = cheia).</summary>
            public void Linha(List<Vector2> pontos, bool fechada, float largura, Color cor, float forca, float ligado = 0f, float desligado = 0f) =>
                Traco(pontos, fechada, _ => largura, cor, forca, ligado, desligado);

            public void LinhaVariavel(List<Vector2> pontos, System.Func<float, float> largura, Color cor) =>
                Traco(pontos, false, largura, cor, 1f, 0f, 0f);

            void Traco(List<Vector2> pontos, bool fechada, System.Func<float, float> largura, Color cor, float forca, float ligado, float desligado)
            {
                int n = pontos.Count, lados = fechada ? n : n - 1;
                float total = Mathf.Max(Linhas.Comprimento(pontos, fechada), 1e-3f), acum = 0f;
                for (int s = 0; s < lados; s++)
                {
                    var wa = pontos[s];
                    var wb = pontos[(s + 1) % n];
                    float lm = Vector2.Distance(wa, wb);
                    float meia = largura((acum + lm * 0.5f) / total) * 0.5f / Mpp;
                    var a = NoPixel(wa);
                    var b = NoPixel(wb);
                    int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - meia - 1)), i1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + meia + 1));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - meia - 1)), j1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + meia + 1));
                    float antes = acum;
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float dd = Linhas.DistanciaDoSegmento(new Vector2(i, j), a, b, out float t);
                        float alfa = Mathf.Clamp01(meia - dd + 0.5f) * forca;
                        if (alfa <= 0f) continue;
                        if (ligado > 0f && Mathf.Repeat(antes + lm * t, ligado + desligado) > ligado) continue;
                        Misturar(j * W + i, cor, alfa);
                    }
                    acum += lm;
                }
            }

            public void Disco(Vector2 centro, float raio, Color cor)
            {
                var c = NoPixel(centro);
                float r = raio / Mpp;
                for (int j = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 1)); j <= Mathf.Min(H - 1, Mathf.CeilToInt(c.y + r + 1)); j++)
                for (int i = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 1)); i <= Mathf.Min(W - 1, Mathf.CeilToInt(c.x + r + 1)); i++)
                    Misturar(j * W + i, cor, r - Vector2.Distance(new Vector2(i, j), c) + 0.5f);
            }

            /// <summary>Uma caixa girada (a ponte, a barragem): o meio, a direção do comprido e as meias medidas, em metros.</summary>
            public void Caixa(Vector2 centro, Vector2 dir, float meioComprimento, float meiaLargura, Color cor)
            {
                var c = NoPixel(centro);
                float mc = meioComprimento / Mpp, ml = meiaLargura / Mpp, alcance = Mathf.Sqrt(mc * mc + ml * ml) + 1f;
                var perp = new Vector2(-dir.y, dir.x);
                for (int j = Mathf.Max(0, Mathf.FloorToInt(c.y - alcance)); j <= Mathf.Min(H - 1, Mathf.CeilToInt(c.y + alcance)); j++)
                for (int i = Mathf.Max(0, Mathf.FloorToInt(c.x - alcance)); i <= Mathf.Min(W - 1, Mathf.CeilToInt(c.x + alcance)); i++)
                {
                    var q = new Vector2(i, j) - c;
                    float fora = Mathf.Max(Mathf.Abs(Vector2.Dot(q, dir)) - mc, Mathf.Abs(Vector2.Dot(q, perp)) - ml);
                    Misturar(j * W + i, cor, 0.5f - fora);
                }
            }
        }
    }

    /// <summary>Uma ponte achada no mapa: onde a via cruza o rio, o tamanho e o que o mapa diz dela (Etapa 5).</summary>
    public struct PonteCalculada
    {
        public string Nome;
        public Vector2 Ponto;
        public Vector2 Direcao;
        public float Comprimento;
        public float LarguraDoRio;
        public string Via;
        public TipoDeVia TipoDaVia;
        public string Rio;
        public EstadoDaPonte Estado;
    }

    /// <summary>
    /// As pontes do mapa: cada cruzamento de uma via com um rio vira uma ponte, e o que o mapa diz (<see cref="MapaDeLakewood.pontes"/>:
    /// o nome e o estado) vale para o cruzamento mais perto do ponto dado, até 250 m. Código puro.
    /// </summary>
    public static class PontesDeLakewood
    {
        public const float AlcanceDoDesenho = 250f;

        public static List<PonteCalculada> Calcular(MapaDeLakewood d, RelevoDeLakewood relevo = null) => Calcular(d, out _, relevo);

        /// <summary>Com o relevo, os rios são as linhas dele (com os meandros); sem, as do desenho.</summary>
        public static List<PonteCalculada> Calcular(MapaDeLakewood d, out List<int> semCruzamento, RelevoDeLakewood relevo = null)
        {
            var lista = new List<PonteCalculada>();
            var rios = new List<(RioDoMapa rio, List<Vector2> linha)>();
            for (int i = 0; i < d.rios.Count; i++)
            {
                var r = d.rios[i];
                var linha = relevo != null && relevo.LinhasDosRios != null ? relevo.LinhasDosRios[i] : (r.pontos.Count >= 2 ? MapaDeLakewood.Linha(r.pontos) : null);
                if (linha != null) rios.Add((r, linha));
            }
            foreach (var e in d.estradas)
            {
                if (e.pontos.Count < 2) continue;
                var via = MapaDeLakewood.Linha(e.pontos);
                foreach (var (rio, linha) in rios)
                {
                    float total = Mathf.Max(Linhas.Comprimento(linha), 1f), acum = 0f;
                    for (int s = 1; s < linha.Count; s++)
                    {
                        var a = linha[s - 1];
                        var b = linha[s];
                        float l = Vector2.Distance(a, b);
                        for (int v = 1; v < via.Count; v++)
                        {
                            if (!Linhas.Cruzam(via[v - 1], via[v], a, b, out var p, out _, out float tr)) continue;
                            bool repetido = false;
                            foreach (var j in lista) repetido |= j.Via == e.nome && j.Rio == rio.nome && Vector2.Distance(j.Ponto, p) < 40f;
                            if (repetido) continue;
                            float fr = (acum + l * tr) / total;
                            float largura = MapaDeLakewood.LarguraDoRio(rio, fr);
                            var dir = (via[v] - via[v - 1]).normalized;
                            var dirRio = (b - a).normalized;
                            float seno = Mathf.Max(0.35f, Mathf.Abs(dir.x * dirRio.y - dir.y * dirRio.x));
                            lista.Add(new PonteCalculada
                            {
                                Nome = "", Ponto = p, Direcao = dir, LarguraDoRio = largura, Comprimento = largura / seno + 30f,
                                Via = e.nome, TipoDaVia = e.tipo, Rio = rio.nome, Estado = EstadoDaPonte.Inteira,
                            });
                        }
                        acum += l;
                    }
                }
            }
            // as pontes sobre os lagos (a North Crossing, Etapa 5): os trechos da via dentro do contorno de um lago
            var lagos = new List<(LagoDoMapa lago, List<Vector2> contorno)>();
            foreach (var l in d.lagos) if (l.contorno.Count >= 3) lagos.Add((l, MapaDeLakewood.Contorno(l.contorno)));
            foreach (var e in d.estradas)
            {
                if (e.pontos.Count < 2) continue;
                var via = Linhas.Reamostrar(MapaDeLakewood.Linha(e.pontos), 6f, false);
                foreach (var (lago, contorno) in lagos)
                    for (int i = 0; i < via.Count; i++)
                    {
                        if (!Linhas.Dentro(via[i], contorno)) continue;
                        int j = i;
                        while (j + 1 < via.Count && Linhas.Dentro(via[j + 1], contorno)) j++;
                        float comprimento = 0f;
                        for (int k = i + 1; k <= j; k++) comprimento += Vector2.Distance(via[k - 1], via[k]);
                        if (comprimento > 20f)
                            lista.Add(new PonteCalculada
                            {
                                Nome = "", Ponto = (via[i] + via[j]) * 0.5f, Direcao = (via[j] - via[i]).normalized, LarguraDoRio = comprimento,
                                Comprimento = comprimento + 30f, Via = e.nome, TipoDaVia = e.tipo, Rio = lago.nome, Estado = EstadoDaPonte.Inteira,
                            });
                        i = j;
                    }
            }
            semCruzamento = new List<int>();
            for (int k = 0; k < d.pontes.Count; k++)
            {
                var pd = d.pontes[k];
                int melhor = -1;
                float perto = AlcanceDoDesenho;
                for (int i = 0; i < lista.Count; i++)
                {
                    float dist = Vector2.Distance(lista[i].Ponto, pd.perto);
                    if (dist < perto)
                    {
                        perto = dist;
                        melhor = i;
                    }
                }
                if (melhor < 0)
                {
                    semCruzamento.Add(k);
                    continue;
                }
                var p = lista[melhor];
                if (!string.IsNullOrEmpty(pd.nome)) p.Nome = pd.nome;
                p.Estado = pd.estado;
                lista[melhor] = p;
            }
            return lista;
        }
    }
}
