using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Um nome do mapa pintado: o texto, o meio dele, a altura das letras em metros e o giro (graus, anti-horário).</summary>
    public struct RotuloDoDesenho
    {
        public string Texto;
        public Vector2 Ponto;
        public float Tamanho;
        public float Giro;
        public FontStyle Estilo;
        public Color Cor;
        /// <summary>Letras espaçadas (as regiões e os lagos: "S I L V E R   L A K E").</summary>
        public bool Espacado;
    }

    /// <summary>Um escudo de estrada no mapa (o número vai por cima, escrito por quem mostra).</summary>
    public struct EscudoDoDesenho
    {
        public Vector2 Ponto;
        public string Numero;
        public bool Interestadual;
    }

    /// <summary>
    /// Pinta o desenho do mapa fixo (Plano_Mapa_Fixo, Parte F1) como imagem, no estilo do esboço: o relevo sombreado de
    /// noroeste, a mata, as lavouras em retalhos, o brejo, a rocha e a neve das montanhas, o horizonte (fora da quarentena)
    /// apagado, os lagos pela fundura, os rios na largura de verdade, as cidades com os distritos e o sinal das ruas no giro
    /// de cada um, as vias (a interestadual laranja, as estaduais amarelas, as do condado brancas, as de terra tracejadas e
    /// as avenidas das cidades), a ferrovia, as pontes (as bloqueadas em laranja), a quarentena tracejada em vermelho, os
    /// bloqueios, os picos, os ícones dos lugares, a casa da família e os escudos. Os nomes não vão na imagem:
    /// <see cref="Rotulos"/> e <see cref="Escudos"/> dizem onde ficam, e quem mostra escreve por cima. Código puro: pode
    /// rodar em outra thread. A imagem cobre os 15 km, com a linha 0 no sul (como uma Texture2D).
    /// </summary>
    public static class PintorDoDesenho
    {
        public static readonly Color MataEscura = new Color(0.2f, 0.3f, 0.18f), MataClara = new Color(0.31f, 0.42f, 0.25f);
        public static readonly Color FlorestaNacional = new Color(0.16f, 0.26f, 0.16f);
        public static readonly Color Rocha = new Color(0.5f, 0.49f, 0.46f), Neve = new Color(0.92f, 0.94f, 0.96f);
        public static readonly Color AguaRasa = new Color(0.25f, 0.45f, 0.58f), AguaFunda = new Color(0.13f, 0.27f, 0.39f);
        public static readonly Color Margem = new Color(0.14f, 0.29f, 0.39f);
        static readonly Color[] Campos =
        {
            new Color(0.66f, 0.62f, 0.4f), new Color(0.55f, 0.58f, 0.35f), new Color(0.72f, 0.66f, 0.45f), new Color(0.5f, 0.52f, 0.3f),
        };
        static readonly Color Brejo1 = new Color(0.32f, 0.4f, 0.3f), Brejo2 = new Color(0.28f, 0.42f, 0.44f);
        static readonly Color CampoCor = new Color(0.42f, 0.5f, 0.27f), CampoClaro = new Color(0.52f, 0.58f, 0.32f);
        public static readonly Color Rua = new Color(0.86f, 0.84f, 0.79f);
        public static readonly Color Contorno = new Color(0.23f, 0.22f, 0.2f);
        public static readonly Color InterestadualCor = new Color(0.91f, 0.57f, 0.23f), InterestadualBorda = new Color(0.29f, 0.17f, 0.07f);
        public static readonly Color EstadualCor = new Color(0.95f, 0.82f, 0.48f);
        public static readonly Color CondadoCor = new Color(0.91f, 0.9f, 0.85f);
        public static readonly Color TerraCor = new Color(0.54f, 0.42f, 0.27f);
        public static readonly Color FerroviaCor = new Color(0.12f, 0.12f, 0.12f);
        public static readonly Color QuarentenaCor = new Color(0.85f, 0.2f, 0.17f);
        public static readonly Color BloqueadaCor = new Color(0.94f, 0.63f, 0.13f);
        public static readonly Color FamiliaCor = new Color(0.95f, 0.76f, 0.19f);
        public static readonly Color PonteCor = new Color(0.07f, 0.07f, 0.07f), PonteBorda = new Color(0.96f, 0.95f, 0.91f);

        public static Color CorDoDistrito(TipoDeDistrito t) => t switch
        {
            TipoDeDistrito.Centro => new Color(0.42f, 0.41f, 0.4f),
            TipoDeDistrito.RuaPrincipal => new Color(0.56f, 0.49f, 0.44f),
            TipoDeDistrito.Comercial => new Color(0.62f, 0.6f, 0.55f),
            TipoDeDistrito.Bairro => new Color(0.66f, 0.63f, 0.57f),
            TipoDeDistrito.Suburbio => new Color(0.71f, 0.69f, 0.61f),
            TipoDeDistrito.Industrial => new Color(0.55f, 0.5f, 0.42f),
            TipoDeDistrito.Parque => new Color(0.36f, 0.48f, 0.3f),
            _ => new Color(0.6f, 0.63f, 0.64f),
        };

        /// <summary>A largura de cada via no mapa (m; maior que a de verdade, para ler) e a da borda escura dela.</summary>
        public static float LarguraDaVia(TipoDeVia t) => t switch
        {
            TipoDeVia.Interestadual => 42f,
            TipoDeVia.Estadual => 30f,
            TipoDeVia.Condado => 20f,
            _ => 11f,
        };

        const float LarguraDaAvenida = 14f;

        public static Color32[] Pintar(DesenhoDoMapa d, RelevoDoDesenho relevo, int lado, List<PonteCalculada> pontes = null)
        {
            var p = new Pincel(lado);
            var px = p.Px;
            float mpp = p.Mpp;

            // as máscaras: as zonas, a quarentena e os lagos (sem as ilhas)
            var zona = new byte[lado * lado];
            foreach (var z in d.zonas)
                if (z.contorno.Count >= 3)
                    p.Varrer(DesenhoDoMapa.Contorno(z.contorno), (k) => zona[k] = (byte)(z.tipo + 1));
            var dentro = new bool[lado * lado];
            if (d.quarentena.Count >= 3) p.Varrer(DesenhoDoMapa.Contorno(d.quarentena), k => dentro[k] = true);
            else for (int k = 0; k < dentro.Length; k++) dentro[k] = true;
            var lago = new short[lado * lado];
            for (int l = 0; l < d.lagos.Count; l++)
                if (d.lagos[l].contorno.Count >= 3)
                {
                    short valor = (short)(l + 1);
                    p.Varrer(DesenhoDoMapa.Contorno(d.lagos[l].contorno), k => lago[k] = valor);
                }
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3) p.Varrer(DesenhoDoMapa.Contorno(ilha.contorno), k => lago[k] = 0);

            // o chão: o relevo sombreado com a mata, os campos, a rocha e a neve; o lago pela fundura
            var tons = new Ruido(d.semente, "tons-do-mapa");
            var luz = new Vector3(-0.55f, 0.63f, 0.55f).normalized;
            Parallel.For(0, lado, j =>
            {
                float z = p.Z(j);
                for (int i = 0; i < lado; i++)
                {
                    int k = j * lado + i;
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
                    float r1 = tons.Fbm(x / 700f, z / 700f, 2);
                    var c = Color.Lerp(MataEscura, MataClara, Mathf.Clamp01(0.5f + 0.7f * r1));
                    switch (zona[k] - 1)
                    {
                        case (int)TipoDeZona.FlorestaNacional:
                            c = Color.Lerp(c, FlorestaNacional, 0.6f);
                            break;
                        case (int)TipoDeZona.Lavouras:
                        {
                            ulong hs = Sorteio.Hash(d.semente, Mathf.FloorToInt(x / 380f), Mathf.FloorToInt(z / 260f), 41);
                            float f = Sorteio.FracaoDoHash(hs);
                            if (f > 0.3f) c = Campos[(int)(hs % 4)];
                            break;
                        }
                        case (int)TipoDeZona.Brejo:
                            c = tons.Amostra(x / 120f, z / 120f) > 0f ? Brejo1 : Brejo2;
                            break;
                    }
                    // a grade da imagem: o campo aberto, as lavouras em retalhos e o brejo (alagado onde o chão fica abaixo da água dele)
                    if (d.TemGrade)
                    {
                        switch (d.CoberturaEm(x, z))
                        {
                            case Cobertura.Campo:
                                c = Color.Lerp(CampoCor, CampoClaro, Mathf.Clamp01(0.5f + 0.8f * r1));
                                break;
                            case Cobertura.Lavoura:
                            {
                                ulong hs = Sorteio.Hash(d.semente, Mathf.FloorToInt(x / 300f), Mathf.FloorToInt(z / 220f), 43);
                                c = Sorteio.FracaoDoHash(hs) > 0.25f ? Campos[(int)(hs % 4)] : CampoCor;
                                break;
                            }
                            case Cobertura.Brejo: case Cobertura.AguaDoBrejo:
                                c = !float.IsNaN(relevo.NivelDoBrejo) && h < relevo.NivelDoBrejo ? AguaRasa
                                  : tons.Amostra(x / 90f, z / 90f) > 0.1f ? Brejo2 : Brejo1;
                                if (c == AguaRasa)
                                {
                                    c.a = 1f;
                                    px[k] = dentro[k] ? c : c * 0.78f + new Color(0.08f, 0.08f, 0.08f);
                                    continue;
                                }
                                break;
                        }
                    }
                    float inclinacao = Mathf.Sqrt(hx * hx + hz * hz);
                    float pedra = Geo.Suave(650f, 1050f, h + r1 * 110f);
                    pedra = Mathf.Max(pedra, Geo.Suave(0.7f, 1.1f, inclinacao) * Geo.Suave(250f, 500f, h));
                    c = Color.Lerp(c, Rocha, pedra);
                    float r2 = tons.Fbm(x / 400f + 50f, z / 400f, 2);
                    c = Color.Lerp(c, Neve, Geo.Suave(1300f, 1450f, h + r2 * 180f) * (1f - 0.5f * Geo.Suave(1.2f, 1.8f, inclinacao)));
                    c *= sombra;
                    if (!dentro[k]) c = c * 0.78f + new Color(0.08f, 0.08f, 0.08f);
                    c.a = 1f;
                    px[k] = c;
                }
            });

            // as cidades: os distritos (fora da água) com o sinal das ruas, e o contorno de cada um
            foreach (var cidade in d.cidades)
            foreach (var dis in cidade.distritos)
            {
                if (dis.contorno.Count < 3) continue;
                var contorno = DesenhoDoMapa.Contorno(dis.contorno);
                var meio = Linhas.Centro(contorno);
                float giro = dis.giro * Mathf.Deg2Rad, cos = Mathf.Cos(giro), sen = Mathf.Sin(giro);
                var fundo = CorDoDistrito(dis.tipo);
                float q = Mathf.Max(30f, dis.quadra), rua = Mathf.Max(9f, mpp * 0.9f);
                bool ruas = dis.tipo != TipoDeDistrito.Parque && dis.tipo != TipoDeDistrito.Marina;
                p.Varrer(contorno, k =>
                {
                    if (lago[k] > 0) return;
                    int i = k % lado, j = k / lado;
                    float rx = p.X(i) - meio.x, rz = p.Z(j) - meio.y;
                    float u = rx * cos + rz * sen, v = -rx * sen + rz * cos;
                    if (dis.tipo == TipoDeDistrito.Suburbio)
                    {
                        float u0 = u;
                        u += 0.14f * q * Mathf.Sin(v / (0.7f * q));
                        v += 0.14f * q * Mathf.Sin(u0 / (0.6f * q));
                    }
                    var c = fundo;
                    if (ruas && (Mathf.Repeat(u + q * 0.5f, q) < rua || Mathf.Repeat(v + q * 0.4f, q * 0.8f) < rua))
                        c = Color.Lerp(Rua, fundo, 0.25f);
                    c = Color.Lerp(px[k], c, 0.88f);
                    c.a = 1f;
                    px[k] = c;
                });
                p.Linha(contorno, true, 1.2f * mpp, Contorno, 0.55f);
            }

            // os rios na largura de verdade (pelo menos 2 pixels), e a margem dos lagos
            foreach (var rio in d.rios)
            {
                if (rio.pontos.Count < 2) continue;
                var linha = DesenhoDoMapa.Linha(rio.pontos);
                p.LinhaVariavel(linha, t => Mathf.Max(DesenhoDoMapa.LarguraDoRio(rio, t), 2.2f * mpp), AguaRasa);
            }
            foreach (var lg in d.lagos)
                if (lg.contorno.Count >= 3) p.Linha(DesenhoDoMapa.Contorno(lg.contorno), true, Mathf.Max(8f, 1.3f * mpp), Margem, 0.9f);
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3) p.Linha(DesenhoDoMapa.Contorno(ilha.contorno), true, Mathf.Max(8f, 1.3f * mpp), new Color(0.79f, 0.76f, 0.63f), 0.9f);

            // a ferrovia
            if (d.ferrovia.Count >= 2)
            {
                var trilho = DesenhoDoMapa.Linha(d.ferrovia);
                p.Linha(trilho, false, Mathf.Max(22f, 2.4f * mpp), FerroviaCor, 1f);
                p.Linha(trilho, false, Mathf.Max(10f, 1.1f * mpp), new Color(0.91f, 0.91f, 0.91f), 1f, 110f, 110f);
            }

            // as vias: as bordas de todas e depois o miolo (os cruzamentos ficam limpos); as avenidas das cidades
            var vias = new List<(List<Vector2> linha, TipoDeVia tipo)>();
            foreach (var e in d.estradas)
                if (e.pontos.Count >= 2) vias.Add((DesenhoDoMapa.Linha(e.pontos), e.tipo));
            foreach (var c in d.cidades)
            foreach (var a in c.avenidas)
                if (a.pontos.Count >= 2)
                {
                    var linha = DesenhoDoMapa.Linha(a.pontos);
                    p.Linha(linha, false, Mathf.Max(LarguraDaAvenida + 10f, 2f * mpp), Contorno, 0.85f);
                    p.Linha(linha, false, Mathf.Max(LarguraDaAvenida, 1.2f * mpp), Rua, 1f);
                }
            for (int passada = 0; passada < 2; passada++)
                foreach (var (linha, tipo) in vias)
                {
                    float largura = Mathf.Max(LarguraDaVia(tipo), (tipo == TipoDeVia.Terra ? 1.2f : 1.6f) * mpp);
                    if (tipo == TipoDeVia.Terra)
                    {
                        if (passada == 1) p.Linha(linha, false, largura, TerraCor, 1f, 90f, 60f);
                        continue;
                    }
                    if (passada == 0)
                        p.Linha(linha, false, largura + Mathf.Max(18f, 1.4f * mpp), tipo == TipoDeVia.Interestadual ? InterestadualBorda : Contorno, 0.9f);
                    else
                        p.Linha(linha, false, largura, tipo == TipoDeVia.Interestadual ? InterestadualCor : tipo == TipoDeVia.Estadual ? EstadualCor : CondadoCor, 1f);
                }

            // as pontes: o tabuleiro escuro atravessando o rio, com a borda clara (laranja nas bloqueadas)
            foreach (var pt in pontes ?? PontesDoDesenho.Calcular(d))
            {
                float largura = (pt.TipoDaVia.HasValue ? (pt.DaCidade ? LarguraDaAvenida : LarguraDaVia(pt.TipoDaVia.Value)) : 22f) + 10f;
                largura = Mathf.Max(largura, 2.4f * mpp);
                float comprimento = Mathf.Max(pt.Comprimento, 4f * mpp);
                var borda = pt.Estado == EstadoDaPonte.Bloqueada ? BloqueadaCor : PonteBorda;
                float folga = Mathf.Max(pt.Estado == EstadoDaPonte.Bloqueada ? 16f : 10f, 1.3f * mpp);
                p.Caixa(pt.Ponto, pt.Direcao, comprimento * 0.5f + folga, largura * 0.5f + folga, borda);
                p.Caixa(pt.Ponto, pt.Direcao, comprimento * 0.5f, largura * 0.5f, PonteCor);
            }

            // a quarentena e os bloqueios
            if (d.quarentena.Count >= 3)
            {
                var linha = DesenhoDoMapa.Contorno(d.quarentena);
                p.Linha(linha, true, Mathf.Max(70f, 3f * mpp), Color.white, 0.3f);
                p.Linha(linha, true, Mathf.Max(34f, 1.8f * mpp), QuarentenaCor, 1f, 220f, 130f);
            }
            float icone = Mathf.Max(120f, 7f * mpp);
            foreach (var b in d.bloqueios)
            {
                p.CaixaRedonda(b.ponto, icone * 1.4f, icone * 0.3f, Color.white);
                p.CaixaRedonda(b.ponto, icone * 1.2f, icone * 0.25f, QuarentenaCor);
                var diag = new Vector2(1f, 1f).normalized;
                p.Caixa(b.ponto, diag, icone * 0.8f, icone * 0.16f, Color.white);
                p.Caixa(b.ponto, new Vector2(1f, -1f).normalized, icone * 0.8f, icone * 0.16f, Color.white);
            }

            // os picos, os lugares e a casa da família
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
            if (TemCasaDaFamilia(d))
            {
                p.Disco(d.casaDaFamilia, icone * 1.25f, new Color(0.23f, 0.16f, 0f));
                p.Disco(d.casaDaFamilia, icone * 1.05f, FamiliaCor);
                p.Poligono(Simbolo(TipoDeLugarFixo.CasaDoLago, d.casaDaFamilia, icone * 0.62f), new Color(0.23f, 0.16f, 0f));
            }

            // os escudos (os números vão por cima)
            foreach (var es in Escudos(d))
            {
                if (es.Interestadual)
                {
                    p.Poligono(Escudo(es.Ponto, icone * 1.75f), Color.white);
                    p.Poligono(Escudo(es.Ponto, icone * 1.55f), new Color(0.11f, 0.31f, 0.61f));
                    p.Poligono(new List<Vector2>
                    {
                        es.Ponto + new Vector2(-1.55f, 1.42f) * icone, es.Ponto + new Vector2(1.55f, 1.42f) * icone,
                        es.Ponto + new Vector2(1.55f, 0.72f) * icone, es.Ponto + new Vector2(-1.55f, 0.72f) * icone,
                    }, new Color(0.78f, 0.15f, 0.17f));
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
        public static List<RotuloDoDesenho> Rotulos(DesenhoDoMapa d, RelevoDoDesenho relevo)
        {
            var lista = new List<RotuloDoDesenho>();
            var branco = Color.white;
            var claro = new Color(0.9f, 0.94f, 0.96f);
            void Por(string texto, Vector2 ponto, float tamanho, FontStyle estilo, Color cor, float giro = 0f, bool espacado = false)
            {
                if (string.IsNullOrEmpty(texto)) return;
                lista.Add(new RotuloDoDesenho { Texto = texto, Ponto = ponto, Tamanho = tamanho, Estilo = estilo, Cor = cor, Giro = giro, Espacado = espacado });
            }

            foreach (var r in d.regioes) Por(r.nome, r.ponto, r.tamanho, FontStyle.Italic, claro, r.giro, true);
            foreach (var z in d.zonas)
                if (z.contorno.Count >= 3) Por(z.nome, z.rotulo != Vector2.zero ? z.rotulo : Linhas.Centro(DesenhoDoMapa.Contorno(z.contorno)), 220f, FontStyle.Normal, branco);
            foreach (var lg in d.lagos)
            {
                if (lg.contorno.Count < 3) continue;
                var ponto = lg.rotulo != Vector2.zero ? lg.rotulo : Linhas.Centro(DesenhoDoMapa.Contorno(lg.contorno));
                bool grande = Linhas.Area(DesenhoDoMapa.Contorno(lg.contorno)) > 3e6f;
                Por(grande ? lg.nome?.ToUpperInvariant() : lg.nome, ponto, grande ? 380f : 220f, FontStyle.Italic, claro, lg.giroDoRotulo, grande);
            }
            foreach (var ilha in d.ilhas)
                if (ilha.contorno.Count >= 3) Por(ilha.nome, Linhas.Centro(DesenhoDoMapa.Contorno(ilha.contorno)), 220f, FontStyle.Normal, branco);
            foreach (var rio in d.rios)
            {
                if (rio.pontos.Count < 2) continue;
                var ponto = Linhas.PontoNa(DesenhoDoMapa.Linha(rio.pontos), rio.posicaoDoRotulo, out var dir);
                float giro = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (giro > 90f) giro -= 180f;
                else if (giro < -90f) giro += 180f;
                // um pouco para o lado, para não cobrir a água
                var lado = new Vector2(-Mathf.Sin(giro * Mathf.Deg2Rad), Mathf.Cos(giro * Mathf.Deg2Rad));
                Por(rio.nome, ponto + lado * (DesenhoDoMapa.LarguraDoRio(rio, rio.posicaoDoRotulo) * 0.5f + 150f), 200f, FontStyle.Italic, claro, giro);
            }
            foreach (var pc in d.relevo.picos)
            {
                var ponto = pc.ponto + pc.rotulo;
                Por(pc.nome, ponto, 230f, FontStyle.Normal, branco);
                if (relevo != null) Por($"{Mathf.Round(relevo.Maxima(pc.ponto, pc.raio * 0.5f) / 10f) * 10f:0} m", ponto + Vector2.down * 250f, 180f, FontStyle.Normal, new Color(0.88f, 0.88f, 0.88f));
            }
            foreach (var c in d.cidades)
            {
                Por(c.nome, c.centro + c.rotulo, TamanhoDoNome(c.tipo), FontStyle.Bold, branco);
                foreach (var dis in c.distritos)
                    if (dis.contorno.Count >= 3 && !string.IsNullOrEmpty(dis.nome))
                        Por(dis.nome, dis.rotulo != Vector2.zero ? dis.rotulo : Linhas.Centro(DesenhoDoMapa.Contorno(dis.contorno)), 170f, FontStyle.Italic, branco);
            }
            foreach (var l in d.lugares) Por(l.nome, l.ponto + l.rotulo, 220f, FontStyle.Normal, branco);
            if (TemCasaDaFamilia(d)) Por("Casa da família", d.casaDaFamilia + d.rotuloDaFamilia, 220f, FontStyle.Bold, new Color(0.97f, 0.84f, 0.35f));
            return lista;
        }

        /// <summary>O desenho tem a casa da família (no mapa limpo, só com a natureza, ainda não).</summary>
        public static bool TemCasaDaFamilia(DesenhoDoMapa d) => d.casaDaFamilia != Vector2.zero || d.cidades.Count > 0;

        /// <summary>A altura das letras do nome de cada tipo de cidade (m).</summary>
        public static float TamanhoDoNome(TipoDeCidade t) => t switch
        {
            TipoDeCidade.Grande => 520f,
            TipoDeCidade.Media => 330f,
            TipoDeCidade.Pequena => 280f,
            _ => 230f,
        };

        /// <summary>
        /// Os escudos das estradas com número: um a cada ~5 km, fora das cidades e dos nomes delas (o que cairia num distrito
        /// ou embaixo de um nome anda pela estrada até sair dele).
        /// </summary>
        public static List<EscudoDoDesenho> Escudos(DesenhoDoMapa d)
        {
            var lista = new List<EscudoDoDesenho>();
            var distritos = new List<List<Vector2>>();
            foreach (var c in d.cidades)
            foreach (var dis in c.distritos)
                if (dis.contorno.Count >= 3) distritos.Add(DesenhoDoMapa.Contorno(dis.contorno));
            // os nomes retos (a caixa do texto, com folga)
            var nomes = new List<Rect>();
            var girados = new List<(Vector2 a, Vector2 b, float folga)>();
            foreach (var r in Rotulos(d, null))
            {
                float mw = r.Texto.Length * r.Tamanho * (r.Espacado ? 0.55f : 0.3f) + 180f, mh = r.Tamanho * 0.6f + 180f;
                if (Mathf.Abs(r.Giro) <= 1f) nomes.Add(new Rect(r.Ponto.x - mw, r.Ponto.y - mh, mw * 2f, mh * 2f));
                else
                {
                    // o girado: um segmento ao longo do texto, com a folga da altura dele
                    var dir = new Vector2(Mathf.Cos(r.Giro * Mathf.Deg2Rad), Mathf.Sin(r.Giro * Mathf.Deg2Rad));
                    girados.Add((r.Ponto - dir * (mw - 180f), r.Ponto + dir * (mw - 180f), mh));
                }
            }
            bool NaCidade(Vector2 ponto)
            {
                foreach (var dis in distritos)
                    if (Linhas.Dentro(ponto, dis) || Linhas.Distancia(ponto, dis, true) < 250f) return true;
                foreach (var r in nomes)
                    if (r.Contains(ponto)) return true;
                foreach (var (a, b, folga) in girados)
                    if (Linhas.DistanciaDoSegmento(ponto, a, b, out _) < folga) return true;
                return false;
            }
            foreach (var e in d.estradas)
            {
                if (string.IsNullOrEmpty(e.numero) || e.pontos.Count < 2) continue;
                var linha = DesenhoDoMapa.Linha(e.pontos);
                float comprimento = Linhas.Comprimento(linha);
                int n = Mathf.Max(1, Mathf.RoundToInt(comprimento / 5000f));
                for (int s = 0; s < n; s++)
                {
                    float f = (s + 0.5f) / n;
                    Vector2 ponto = Linhas.PontoNa(linha, f, out _);
                    // o lugar livre mais perto, andando pela estrada de 1% em 1% para os dois lados (sem lugar, sem escudo)
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
                    // nem perto de outro escudo da mesma estrada
                    bool junto = false;
                    foreach (var outro in lista) junto |= outro.Numero == e.numero && Vector2.Distance(outro.Ponto, ponto) < 1500f;
                    if (junto) continue;
                    // nem fora do mapa
                    if (Mathf.Abs(ponto.x) > DesenhoDoMapa.Metade - 300f || Mathf.Abs(ponto.y) > DesenhoDoMapa.Metade - 300f) continue;
                    lista.Add(new EscudoDoDesenho { Ponto = ponto, Numero = e.numero, Interestadual = e.tipo == TipoDeVia.Interestadual });
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

        static List<Vector2> Simbolo(TipoDeLugarFixo t, Vector2 c, float r)
        {
            var l = new List<Vector2>();
            switch (t)
            {
                case TipoDeLugarFixo.BaseMilitar:
                    for (int k = 0; k < 10; k++)
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                        float rr = k % 2 == 0 ? r : r * 0.42f;
                        l.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                    }
                    break;
                case TipoDeLugarFixo.CasaDoLago: case TipoDeLugarFixo.Resort: case TipoDeLugarFixo.EstacaoDosGuardas:
                    l.AddRange(new[] { c + new Vector2(-0.7f, -0.75f) * r, c + new Vector2(0.7f, -0.75f) * r, c + new Vector2(0.7f, 0.15f) * r,
                                       c + new Vector2(0f, 0.85f) * r, c + new Vector2(-0.7f, 0.15f) * r });
                    break;
                case TipoDeLugarFixo.TorreDeVigia: case TipoDeLugarFixo.Pedreira:
                    l.AddRange(Triangulo(c, r * 0.8f));
                    break;
                case TipoDeLugarFixo.Fazenda: case TipoDeLugarFixo.PostoDeEstrada:
                    l.AddRange(new[] { c + new Vector2(0f, r), c + new Vector2(r, 0f), c + new Vector2(0f, -r), c + new Vector2(-r, 0f) });
                    break;
                case TipoDeLugarFixo.Represa:
                    l.AddRange(new[] { c + new Vector2(-r, -0.3f * r), c + new Vector2(r, -0.3f * r), c + new Vector2(r, 0.3f * r), c + new Vector2(-r, 0.3f * r) });
                    break;
                default:   // a usina, a serraria, o pátio da ferrovia: o galpão com a chaminé
                    l.AddRange(new[] { c + new Vector2(-0.8f, -0.7f) * r, c + new Vector2(0.8f, -0.7f) * r, c + new Vector2(0.8f, 0.9f) * r,
                                       c + new Vector2(0.45f, 0.9f) * r, c + new Vector2(0.45f, 0.1f) * r, c + new Vector2(-0.8f, 0.1f) * r });
                    break;
            }
            return l;
        }

        // ------------------------------------------------------------------ o pincel (do mundo em metros para os pixels)

        sealed class Pincel
        {
            public readonly int Lado;
            public readonly float Mpp;
            public readonly Color[] Px;

            public Pincel(int lado)
            {
                Lado = lado;
                Mpp = DesenhoDoMapa.Lado / lado;
                Px = new Color[lado * lado];
            }

            public float X(int i) => -DesenhoDoMapa.Metade + (i + 0.5f) * Mpp;
            public float Z(int j) => X(j);
            Vector2 NoPixel(Vector2 p) => (p + Vector2.one * DesenhoDoMapa.Metade) / Mpp - Vector2.one * 0.5f;

            public void Varrer(List<Vector2> poligono, System.Action<int> cada) =>
                Linhas.Varrer(poligono, X(0), Z(0), Mpp, Lado, Lado, (j, i0, i1) =>
                {
                    for (int i = i0; i <= i1; i++) cada(j * Lado + i);
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
                    int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - meia - 1)), i1 = Mathf.Min(Lado - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + meia + 1));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - meia - 1)), j1 = Mathf.Min(Lado - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + meia + 1));
                    float antes = acum;
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float dd = Linhas.DistanciaDoSegmento(new Vector2(i, j), a, b, out float t);
                        float alfa = Mathf.Clamp01(meia - dd + 0.5f) * forca;
                        if (alfa <= 0f) continue;
                        if (ligado > 0f && Mathf.Repeat(antes + lm * t, ligado + desligado) > ligado) continue;
                        Misturar(j * Lado + i, cor, alfa);
                    }
                    acum += lm;
                }
            }

            public void Disco(Vector2 centro, float raio, Color cor)
            {
                var c = NoPixel(centro);
                float r = raio / Mpp;
                for (int j = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 1)); j <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.y + r + 1)); j++)
                for (int i = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 1)); i <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.x + r + 1)); i++)
                    Misturar(j * Lado + i, cor, r - Vector2.Distance(new Vector2(i, j), c) + 0.5f);
            }

            /// <summary>Uma caixa girada (a ponte): o meio, a direção do comprido e as meias medidas, em metros.</summary>
            public void Caixa(Vector2 centro, Vector2 dir, float meioComprimento, float meiaLargura, Color cor)
            {
                var c = NoPixel(centro);
                float mc = meioComprimento / Mpp, ml = meiaLargura / Mpp, alcance = Mathf.Sqrt(mc * mc + ml * ml) + 1f;
                var perp = new Vector2(-dir.y, dir.x);
                for (int j = Mathf.Max(0, Mathf.FloorToInt(c.y - alcance)); j <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.y + alcance)); j++)
                for (int i = Mathf.Max(0, Mathf.FloorToInt(c.x - alcance)); i <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.x + alcance)); i++)
                {
                    var q = new Vector2(i, j) - c;
                    float fora = Mathf.Max(Mathf.Abs(Vector2.Dot(q, dir)) - mc, Mathf.Abs(Vector2.Dot(q, perp)) - ml);
                    Misturar(j * Lado + i, cor, 0.5f - fora);
                }
            }

            /// <summary>Um quadrado de cantos redondos, com a meia medida e o raio do canto em metros.</summary>
            public void CaixaRedonda(Vector2 centro, float meia, float canto, Color cor)
            {
                var c = NoPixel(centro);
                float m = meia / Mpp, r = canto / Mpp;
                for (int j = Mathf.Max(0, Mathf.FloorToInt(c.y - m - 1)); j <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.y + m + 1)); j++)
                for (int i = Mathf.Max(0, Mathf.FloorToInt(c.x - m - 1)); i <= Mathf.Min(Lado - 1, Mathf.CeilToInt(c.x + m + 1)); i++)
                {
                    float qx = Mathf.Max(0f, Mathf.Abs(i - c.x) - (m - r)), qy = Mathf.Max(0f, Mathf.Abs(j - c.y) - (m - r));
                    Misturar(j * Lado + i, cor, r - Mathf.Sqrt(qx * qx + qy * qy) + 0.5f);
                }
            }
        }
    }
}
