using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A leitura da imagem de referência do Lakewood (`Imgs/Lakewood/Lakewood_Referencia.png` da pasta de arte, a cópia em
    /// PNG do webp do usuário): cada pixel do mapa (dentro da moldura) vira uma classe pela cor (a água azul, a mata verde
    /// escura, o campo verde claro, a lavoura amarela, a rocha cinza e o urbano bege, este só dentro das cidades do
    /// esboço); o que não dá para ler (os rótulos, as estradas, as linhas) pega a classe da vizinhança. Daí saem:
    ///  - a cobertura em células de 25 m e a grade das montanhas (a fração de rocha em volta), gravadas no mapa;
    ///  - os lagos: as manchas de água abertas (os rios finos somem), com o contorno traçado e simplificado; o maior é o
    ///    reservatório, e o contorno dele é recortado na barragem (o funil e a crista do esboço no lugar da ponta da imagem);
    ///  - as ilhas: a terra cercada de água, com os nomes das cinco pela posição.
    /// Menu Zombie City Cleaner > Lakewood > 2. Ler a imagem de referência. Rode depois do esboço (as cidades e a barragem
    /// vêm dele).
    /// </summary>
    public static class LeituraDaReferencia
    {
        public const string Imagem = ImagemDeLakewood.Pasta + @"\Lakewood_Referencia.png";
        /// <summary>A moldura: o mapa fica entre estas colunas e linhas da imagem (px; a linha 0 em cima).</summary>
        public const int X0 = 32, X1 = 1264, Y0 = 29, Y1 = 984;
        const byte Desconhecido = 255;
        const int Classes = 6;
        /// <summary>A abertura da máscara da água (px): o que for mais fino que o dobro some (os rios).</summary>
        const int RaioDaAbertura = 2;
        const float AreaMinimaDoLago = 0.08f, AreaMinimaDaIlha = 0.02f;   // km²

        static readonly (TipoDeIlha tipo, string nome, Vector2 perto, float altura)[] IlhasConhecidas =
        {
            (TipoDeIlha.Central, "Central Island", new Vector2(6180f, 5200f), 42f),
            (TipoDeIlha.Acampamento, "Camp Island", new Vector2(4100f, 6500f), 20f),
            (TipoDeIlha.Antena, "Antenna Island", new Vector2(7650f, 6380f), 24f),
            (TipoDeIlha.Residencial, "Cottage Island", new Vector2(7700f, 4330f), 16f),
            (TipoDeIlha.Pesquisa, "Research Island", new Vector2(3850f, 4200f), 18f),
        };

        /// <summary>Lê a imagem e grava no mapa a cobertura, as montanhas, os lagos e as ilhas. Devolve o resumo.</summary>
        public static string Ler(MapaDeLakewood m)
        {
            if (!File.Exists(Imagem)) throw new FileNotFoundException($"Não achei a referência em {Imagem}.");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Imagem));
            int W = tex.width, H = tex.height;
            var px = tex.GetPixels32();   // a linha 0 embaixo
            Object.DestroyImmediate(tex);
            int w = X1 - X0, h = Y1 - Y0;   // o recorte: i de oeste a leste, j de sul a norte
            float mppX = m.largura / w, mppZ = m.comprimento / h;
            Vector2 Metros(float i, float j) => new Vector2((i + 0.5f) * mppX, (j + 0.5f) * mppZ);

            // ---------------------------------------------------------------- a classe de cada pixel
            var classe = new byte[w * h];
            var cidades = new List<List<Vector2>>();
            foreach (var c in m.cidades)
                if (c.contorno.Count >= 3) cidades.Add(MapaDeLakewood.Contorno(c.contorno));
            var instalacoes = new List<(Vector2 ponto, float raio)>();
            foreach (var l in m.lugares)
                if (l.aplainar > 0f && l.raio > 0f) instalacoes.Add((l.ponto, l.raio));
            bool temRecorte = m.represa.Tem && m.represa.recorte.width > 0f;
            for (int j = 0; j < h; j++)
            {
                int linhaDaImagem = Y1 - 1 - j;            // de cima
                int linhaDaUnity = H - 1 - linhaDaImagem;   // de baixo
                for (int i = 0; i < w; i++)
                {
                    var c = px[linhaDaUnity * W + X0 + i];
                    float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                    byte k = Classificar(r, g, b);
                    if (k == (byte)Cobertura.Urbano || k == (byte)Cobertura.Rocha)
                    {
                        bool naCidade = false;
                        var p = Metros(i, j);
                        foreach (var cid in cidades)
                            if (Linhas.Dentro(p, cid))
                            {
                                naCidade = true;
                                break;
                            }
                        // as instalações (o aeroporto, a estação, a base): o cinza das pistas e dos tanques é chão feito, não montanha
                        foreach (var (ponto, raio) in instalacoes)
                            if (Vector2.Distance(p, ponto) < raio) naCidade = true;
                        if (naCidade) k = (byte)Cobertura.Urbano;
                        else if (k == (byte)Cobertura.Urbano) k = Desconhecido;   // as faces claras das montanhas
                        else if (temRecorte && m.represa.recorte.Contains(p)) k = Desconhecido;   // o concreto da barragem da imagem
                    }
                    classe[j * w + i] = k;
                }
            }
            PreencherDesconhecidos(classe, w, h);

            // ---------------------------------------------------------------- a água: a abertura, as manchas, os lagos e as ilhas
            var agua = new bool[w * h];
            for (int k = 0; k < agua.Length; k++) agua[k] = classe[k] == (byte)Cobertura.Agua;
            var aberta = Abrir(agua, w, h, RaioDaAbertura);
            var comp = new int[w * h];
            var areas = Componentes(aberta, w, h, comp, out int quantos);
            var caixas = Caixas(comp, w, h, quantos);
            var lagos = new List<(int id, float km2)>();
            for (int id = 1; id <= quantos; id++)
            {
                float km2 = areas[id] * mppX * mppZ / 1e6f;
                if (km2 < AreaMinimaDoLago) continue;
                // uma mancha comprida é um rio ou um canal (os rios são desenhados à mão no esboço): a espessura é o dobro
                // da maior distância de um pixel dela até a beira, e o comprimento, a área dividida por ela
                float espessura = Mathf.Max(1f, 2f * MaiorDistanciaDaBeira(comp, w, h, id, caixas[id]) * Mathf.Min(mppX, mppZ));
                float comprimento = km2 * 1e6f / espessura;
                if (km2 < 1.5f && comprimento > 4f * espessura) continue;
                lagos.Add((id, km2));
            }
            lagos.Sort((a, b) => b.km2.CompareTo(a.km2));
            var resumo = new System.Text.StringBuilder();
            m.lagos = new List<LagoDoMapa>();
            var idPrincipal = -1;
            for (int n = 0; n < lagos.Count; n++)
            {
                var (id, km2) = lagos[n];
                var contorno = Simplificar(TracarContorno(comp, w, h, id), 2.2f);
                var pontos = new List<Vector2>(contorno.Count);
                foreach (var q in contorno) pontos.Add(Metros(q.x, q.y));
                var lago = new LagoDoMapa { contorno = pontos };
                if (n == 0)
                {
                    idPrincipal = id;
                    lago.nome = "Lakewood Reservoir";
                    lago.nivel = 150f;
                    lago.fundura = 14f;   // longe da margem; o vale afogado do mapa cava mais, até a barragem
                    lago.rampaDaMargemOeste = 900f;
                    lago.rampaDaMargemLeste = 350f;
                    lago.rotulo = new Vector2(3700f, 5000f);
                    if (m.represa.Tem && m.represa.recorte.width > 0f) RecortarNaRepresa(lago.contorno, m.represa);
                }
                else
                {
                    var meio = Linhas.Centro(pontos);
                    lago.nome = Vector2.Distance(meio, new Vector2(3800f, 9500f)) < 1200f ? "Pine Ridge Lake" : $"Lake {n}";
                    lago.nivelPeloRelevo = true;
                    lago.fundura = 8f;
                    lago.rampaDaMargemOeste = lago.rampaDaMargemLeste = 250f;
                }
                m.lagos.Add(lago);
                resumo.Append($"{lago.nome}: {Linhas.Area(MapaDeLakewood.Contorno(lago.contorno)) / 1e6f:F2} km² ({lago.contorno.Count} pontos). ");
            }

            // as ilhas: a terra que a água cerca (o que o preenchimento a partir da borda não alcança)
            m.ilhas = new List<IlhaDoMapa>();
            if (idPrincipal > 0)
            {
                var terra = new bool[w * h];
                for (int k = 0; k < terra.Length; k++) terra[k] = !aberta[k];
                var alcancavel = new bool[w * h];
                var fila = new Stack<int>();
                for (int i = 0; i < w; i++) { Semear(i, 0); Semear(i, h - 1); }
                for (int j = 0; j < h; j++) { Semear(0, j); Semear(w - 1, j); }
                void Semear(int i, int j)
                {
                    int k = j * w + i;
                    if (terra[k] && !alcancavel[k]) { alcancavel[k] = true; fila.Push(k); }
                }
                while (fila.Count > 0)
                {
                    int k = fila.Pop();
                    int i = k % w, j = k / w;
                    if (i > 0 && terra[k - 1] && !alcancavel[k - 1]) { alcancavel[k - 1] = true; fila.Push(k - 1); }
                    if (i < w - 1 && terra[k + 1] && !alcancavel[k + 1]) { alcancavel[k + 1] = true; fila.Push(k + 1); }
                    if (j > 0 && terra[k - w] && !alcancavel[k - w]) { alcancavel[k - w] = true; fila.Push(k - w); }
                    if (j < h - 1 && terra[k + w] && !alcancavel[k + w]) { alcancavel[k + w] = true; fila.Push(k + w); }
                }
                var cercada = new bool[w * h];
                for (int k = 0; k < cercada.Length; k++) cercada[k] = terra[k] && !alcancavel[k];
                var compIlha = new int[w * h];
                var areasIlha = Componentes(cercada, w, h, compIlha, out int quantasIlhas);
                var usados = new bool[IlhasConhecidas.Length];
                var candidatas = new List<(int id, float km2, Vector2 meio, List<Vector2> pontos)>();
                for (int id = 1; id <= quantasIlhas; id++)
                {
                    float km2 = areasIlha[id] * mppX * mppZ / 1e6f;
                    if (km2 < AreaMinimaDaIlha) continue;
                    var contorno = Simplificar(TracarContorno(compIlha, w, h, id), 1.8f);
                    var pontos = new List<Vector2>(contorno.Count);
                    foreach (var q in contorno) pontos.Add(Metros(q.x, q.y));
                    if (pontos.Count < 3) continue;
                    candidatas.Add((id, km2, Linhas.Centro(pontos), pontos));
                }
                candidatas.Sort((a, b) => b.km2.CompareTo(a.km2));
                int ilhote = 1;
                foreach (var cand in candidatas)
                {
                    var ilha = new IlhaDoMapa { contorno = cand.pontos };
                    int melhor = -1;
                    float perto = 900f;
                    for (int q = 0; q < IlhasConhecidas.Length; q++)
                    {
                        if (usados[q]) continue;
                        float dist = Vector2.Distance(cand.meio, IlhasConhecidas[q].perto);
                        if (dist < perto && cand.km2 >= 0.12f)
                        {
                            perto = dist;
                            melhor = q;
                        }
                    }
                    if (melhor >= 0)
                    {
                        usados[melhor] = true;
                        ilha.tipo = IlhasConhecidas[melhor].tipo;
                        ilha.nome = IlhasConhecidas[melhor].nome;
                        ilha.altura = IlhasConhecidas[melhor].altura;
                    }
                    else
                    {
                        ilha.tipo = TipoDeIlha.Ilhote;
                        ilha.nome = $"Islet {ilhote++}";
                        ilha.altura = Mathf.Clamp(4f + cand.km2 * 40f, 4f, 12f);
                    }
                    m.ilhas.Add(ilha);
                }
                resumo.Append($"Ilhas: {m.ilhas.Count} (");
                foreach (var il in m.ilhas) if (il.tipo != TipoDeIlha.Ilhote) resumo.Append(il.nome).Append(", ");
                resumo.Append($"{ilhote - 1} ilhotes). ");
            }

            // ---------------------------------------------------------------- a cobertura em células e a grade das montanhas
            float celula = m.celulaDaGrade;
            int gx = m.gradeX = Mathf.CeilToInt(m.largura / celula), gz = m.gradeZ = Mathf.CeilToInt(m.comprimento / celula);
            var contagem = new int[gx * gz * Classes];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                byte k = classe[j * w + i];
                if (k >= Classes) continue;
                var p = Metros(i, j);
                int ci = Mathf.Clamp((int)(p.x / celula), 0, gx - 1), cj = Mathf.Clamp((int)(p.y / celula), 0, gz - 1);
                contagem[(cj * gx + ci) * Classes + k]++;
            }
            var cobertura = new byte[gx * gz];
            var rocha = new float[gx * gz];
            for (int c = 0; c < gx * gz; c++)
            {
                int melhor = 0, total = 0;
                for (int k = 0; k < Classes; k++)
                {
                    total += contagem[c * Classes + k];
                    if (contagem[c * Classes + k] > contagem[c * Classes + melhor]) melhor = k;
                }
                cobertura[c] = (byte)melhor;
                rocha[c] = total > 0 ? contagem[c * Classes + (int)Cobertura.Rocha] / (float)total : 0f;
            }
            // o pontilhado sai: cada célula pega a classe mais comum da vizinhança 3 × 3 (uma passada; a água não muda)
            for (int passada = 0; passada < 1; passada++)
            {
                var lisa = (byte[])cobertura.Clone();
                var votos = new int[Classes];
                for (int cj = 0; cj < gz; cj++)
                for (int ci = 0; ci < gx; ci++)
                {
                    int c = cj * gx + ci;
                    if (cobertura[c] == (byte)Cobertura.Agua) continue;
                    System.Array.Clear(votos, 0, Classes);
                    for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int a = ci + di, b = cj + dj;
                        if (a < 0 || b < 0 || a >= gx || b >= gz) continue;
                        byte k = cobertura[b * gx + a];
                        if (k != (byte)Cobertura.Agua) votos[k] += (di == 0 && dj == 0) ? 2 : 1;
                    }
                    int melhor = cobertura[c];
                    for (int k = 0; k < Classes; k++) if (votos[k] > votos[melhor]) melhor = k;
                    lisa[c] = (byte)melhor;
                }
                cobertura = lisa;
            }
            // a água pelos contornos gravados (e as ilhas de volta à terra)
            var lagosSuaves = new List<List<Vector2>>();
            foreach (var l in m.lagos) lagosSuaves.Add(MapaDeLakewood.Contorno(l.contorno));
            var ilhasSuaves = new List<List<Vector2>>();
            foreach (var il in m.ilhas) ilhasSuaves.Add(MapaDeLakewood.Contorno(il.contorno));
            for (int cj = 0; cj < gz; cj++)
            for (int ci = 0; ci < gx; ci++)
            {
                var p = new Vector2((ci + 0.5f) * celula, (cj + 0.5f) * celula);
                bool naAgua = false;
                foreach (var l in lagosSuaves) naAgua |= Linhas.Dentro(p, l);
                if (naAgua)
                    foreach (var il in ilhasSuaves) if (Linhas.Dentro(p, il)) naAgua = false;
                int c = cj * gx + ci;
                if (naAgua) cobertura[c] = (byte)Cobertura.Agua;
                else if (cobertura[c] == (byte)Cobertura.Agua) cobertura[c] = (byte)Cobertura.Mata;
            }
            var montanha = new byte[gx * gz];
            for (int cj = 0; cj < gz; cj++)
            for (int ci = 0; ci < gx; ci++)
            {
                float soma = 0f;
                int n = 0;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int a = ci + di, b = cj + dj;
                    if (a < 0 || b < 0 || a >= gx || b >= gz) continue;
                    soma += rocha[b * gx + a];
                    n++;
                }
                montanha[cj * gx + ci] = (byte)Mathf.RoundToInt(Mathf.Clamp01(soma / Mathf.Max(1, n)) * 255f);
            }
            m.cobertura = cobertura;
            m.montanha = montanha;
            var porCobertura = new int[Classes];
            foreach (var c in cobertura) porCobertura[c]++;
            resumo.Append("Cobertura: ");
            for (int k = 0; k < Classes; k++) resumo.Append($"{(Cobertura)k} {100f * porCobertura[k] / cobertura.Length:F1}%, ");
            return resumo.ToString();
        }

        // ------------------------------------------------------------------ as cores

        /// <summary>A classe de um pixel pela cor (0 a 1), ou Desconhecido.</summary>
        public static byte Classificar(float r, float g, float b)
        {
            float mx = Mathf.Max(r, Mathf.Max(g, b)), mn = Mathf.Min(r, Mathf.Min(g, b));
            float sat = mx > 1e-4f ? (mx - mn) / mx : 0f, val = mx;
            if (b > 0.28f && b > r * 1.5f && b > g * 1.12f) return (byte)Cobertura.Agua;
            // a lavoura é amarela (o verde perto do vermelho); as rodovias laranja ficam de fora
            if (r > 0.5f && g > 0.4f && b < 0.38f && r - b > 0.28f && g > r * 0.74f && r >= g * 0.95f) return (byte)Cobertura.Lavoura;
            if (g > r * 1.05f && g > b * 1.15f) return val > 0.5f && r > g * 0.72f ? (byte)Cobertura.Campo : (byte)Cobertura.Mata;
            if (val > 0.82f && sat < 0.12f) return Desconhecido;   // o branco: os rótulos e a neve (a vizinhança decide)
            if (val > 0.6f && sat < 0.3f && r - b > 0.06f && r - b < 0.22f) return (byte)Cobertura.Urbano;
            if (sat < 0.22f && val > 0.36f && val < 0.82f && (g <= r * 1.02f || val > 0.5f)) return (byte)Cobertura.Rocha;
            return Desconhecido;
        }

        // os desconhecidos pegam a classe mais comum entre os vizinhos conhecidos, em passadas, da beira para dentro
        static void PreencherDesconhecidos(byte[] classe, int w, int h)
        {
            var pendentes = new List<int>();
            for (int k = 0; k < classe.Length; k++) if (classe[k] == Desconhecido) pendentes.Add(k);
            var contagem = new int[Classes];
            for (int passada = 0; passada < 400 && pendentes.Count > 0; passada++)
            {
                var novos = new List<(int k, byte c)>();
                var ficam = new List<int>();
                foreach (int k in pendentes)
                {
                    int i = k % w, j = k / w;
                    System.Array.Clear(contagem, 0, Classes);
                    int conhecidos = 0;
                    for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= w || b >= h) continue;
                        byte c = classe[b * w + a];
                        if (c >= Classes) continue;
                        contagem[c]++;
                        conhecidos++;
                    }
                    if (conhecidos == 0)
                    {
                        ficam.Add(k);
                        continue;
                    }
                    int melhor = 0;
                    for (int c = 1; c < Classes; c++) if (contagem[c] > contagem[melhor]) melhor = c;
                    novos.Add((k, (byte)melhor));
                }
                foreach (var (k, c) in novos) classe[k] = c;
                pendentes = ficam;
            }
            foreach (int k in pendentes) classe[k] = (byte)Cobertura.Mata;
        }

        // ------------------------------------------------------------------ as máscaras

        // a abertura: a erosão e a dilatação por uma caixa de (2 raio + 1) pixels
        static bool[] Abrir(bool[] mascara, int w, int h, int raio)
        {
            var erodida = new bool[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                bool tudo = true;
                for (int dj = -raio; dj <= raio && tudo; dj++)
                for (int di = -raio; di <= raio && tudo; di++)
                {
                    int a = i + di, b = j + dj;
                    tudo = a >= 0 && b >= 0 && a < w && b < h && mascara[b * w + a];
                }
                erodida[j * w + i] = tudo;
            }
            var saida = new bool[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                if (!erodida[j * w + i]) continue;
                for (int dj = -raio; dj <= raio; dj++)
                for (int di = -raio; di <= raio; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a >= 0 && b >= 0 && a < w && b < h) saida[b * w + a] = true;
                }
            }
            return saida;
        }

        // as manchas ligadas (em 4 vizinhos): o número de cada pixel em comp (0: fora) e a área de cada uma (em pixels)
        static List<int> Componentes(bool[] mascara, int w, int h, int[] comp, out int quantos)
        {
            var areas = new List<int> { 0 };
            quantos = 0;
            var fila = new Stack<int>();
            for (int k0 = 0; k0 < mascara.Length; k0++)
            {
                if (!mascara[k0] || comp[k0] != 0) continue;
                int id = ++quantos, area = 0;
                comp[k0] = id;
                fila.Push(k0);
                while (fila.Count > 0)
                {
                    int k = fila.Pop();
                    area++;
                    int i = k % w, j = k / w;
                    void Ver(int q)
                    {
                        if (mascara[q] && comp[q] == 0) { comp[q] = id; fila.Push(q); }
                    }
                    if (i > 0) Ver(k - 1);
                    if (i < w - 1) Ver(k + 1);
                    if (j > 0) Ver(k - w);
                    if (j < h - 1) Ver(k + w);
                }
                areas.Add(area);
            }
            return areas;
        }

        // a maior distância (de Chebyshev, em pixels) de um pixel da mancha até a beira dela: a meia espessura
        static int MaiorDistanciaDaBeira(int[] comp, int w, int h, int id, Rect caixa)
        {
            int x0 = (int)caixa.xMin, y0 = (int)caixa.yMin, cw = (int)caixa.width, ch = (int)caixa.height;
            var d = new int[cw * ch];
            const int longe = 100000;
            for (int j = 0; j < ch; j++)
            for (int i = 0; i < cw; i++)
            {
                bool dentro = comp[(y0 + j) * w + x0 + i] == id;
                if (!dentro) { d[j * cw + i] = 0; continue; }
                // na beira da caixa, a beira está a 1
                int v = (i == 0 || j == 0 || i == cw - 1 || j == ch - 1) ? 1 : longe;
                if (i > 0) v = Mathf.Min(v, d[j * cw + i - 1] + 1);
                if (j > 0)
                {
                    v = Mathf.Min(v, d[(j - 1) * cw + i] + 1);
                    if (i > 0) v = Mathf.Min(v, d[(j - 1) * cw + i - 1] + 1);
                    if (i < cw - 1) v = Mathf.Min(v, d[(j - 1) * cw + i + 1] + 1);
                }
                d[j * cw + i] = v;
            }
            int maior = 0;
            for (int j = ch - 1; j >= 0; j--)
            for (int i = cw - 1; i >= 0; i--)
            {
                int v = d[j * cw + i];
                if (v == 0) continue;
                if (i < cw - 1) v = Mathf.Min(v, d[j * cw + i + 1] + 1);
                if (j < ch - 1)
                {
                    v = Mathf.Min(v, d[(j + 1) * cw + i] + 1);
                    if (i > 0) v = Mathf.Min(v, d[(j + 1) * cw + i - 1] + 1);
                    if (i < cw - 1) v = Mathf.Min(v, d[(j + 1) * cw + i + 1] + 1);
                }
                d[j * cw + i] = v;
                maior = Mathf.Max(maior, v);
            }
            return maior;
        }

        // a caixa (em pixels) de cada mancha
        static Rect[] Caixas(int[] comp, int w, int h, int quantos)
        {
            var x0 = new int[quantos + 1];
            var x1 = new int[quantos + 1];
            var y0 = new int[quantos + 1];
            var y1 = new int[quantos + 1];
            for (int id = 0; id <= quantos; id++) { x0[id] = y0[id] = int.MaxValue; x1[id] = y1[id] = int.MinValue; }
            for (int k = 0; k < comp.Length; k++)
            {
                int id = comp[k];
                if (id == 0) continue;
                int i = k % w, j = k / w;
                x0[id] = Mathf.Min(x0[id], i); x1[id] = Mathf.Max(x1[id], i);
                y0[id] = Mathf.Min(y0[id], j); y1[id] = Mathf.Max(y1[id], j);
            }
            var saida = new Rect[quantos + 1];
            for (int id = 1; id <= quantos; id++) saida[id] = Rect.MinMaxRect(x0[id], y0[id], x1[id] + 1, y1[id] + 1);
            return saida;
        }

        // o contorno de fora de uma mancha, pixel a pixel (o traçado de Moore, no sentido horário, a partir do pixel mais
        // ao sul e a oeste dela)
        static List<Vector2Int> TracarContorno(int[] comp, int w, int h, int id)
        {
            int inicio = -1;
            for (int k = 0; k < comp.Length && inicio < 0; k++) if (comp[k] == id) inicio = k;
            var saida = new List<Vector2Int>();
            if (inicio < 0) return saida;
            bool Dentro(int i, int j) => i >= 0 && j >= 0 && i < w && j < h && comp[j * w + i] == id;
            // os 8 vizinhos no sentido horário (a partir de cima, na grade com o j para o norte)
            var viz = new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1) };
            var s = new Vector2Int(inicio % w, inicio / w);
            var c = s;
            int dirDeOnde = 6;   // viemos do oeste (o vizinho de índice 6 é o oeste), que está fora
            int limite = comp.Length * 2;
            for (int passo = 0; passo < limite; passo++)
            {
                saida.Add(c);
                bool achou = false;
                // começa a olhar no vizinho depois de onde viemos
                for (int q = 1; q <= 8; q++)
                {
                    int d = (dirDeOnde + q) % 8;
                    var n = c + viz[d];
                    if (!Dentro(n.x, n.y)) continue;
                    // o novo "de onde viemos" é a direção oposta ao passo dado, girada para trás (o vizinho anterior)
                    dirDeOnde = (d + 5) % 8;
                    c = n;
                    achou = true;
                    break;
                }
                if (!achou) break;   // um pixel só
                if (c == s && saida.Count > 2) break;
            }
            return saida;
        }

        // Douglas-Peucker num laço fechado (em pixels): corta nos dois pontos mais distantes e simplifica cada metade
        static List<Vector2> Simplificar(List<Vector2Int> laco, float tolerancia)
        {
            var pts = new List<Vector2>(laco.Count);
            foreach (var p in laco) pts.Add(new Vector2(p.x, p.y));
            // tira os repetidos seguidos
            var limpos = new List<Vector2>();
            foreach (var p in pts) if (limpos.Count == 0 || Vector2.Distance(limpos[limpos.Count - 1], p) > 0.01f) limpos.Add(p);
            if (limpos.Count < 4) return limpos;
            int longe = 0;
            float maior = 0f;
            for (int i = 1; i < limpos.Count; i++)
            {
                float d = (limpos[i] - limpos[0]).sqrMagnitude;
                if (d > maior) { maior = d; longe = i; }
            }
            var a = limpos.GetRange(0, longe + 1);
            var b = limpos.GetRange(longe, limpos.Count - longe);
            b.Add(limpos[0]);
            var sa = DouglasPeucker(a, tolerancia);
            var sb = DouglasPeucker(b, tolerancia);
            var saida = new List<Vector2>(sa);
            for (int i = 1; i < sb.Count - 1; i++) saida.Add(sb[i]);
            return saida;
        }

        static List<Vector2> DouglasPeucker(List<Vector2> pts, float tolerancia)
        {
            if (pts.Count < 3) return new List<Vector2>(pts);
            int indice = -1;
            float maior = 0f;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float d = Linhas.DistanciaDoSegmento(pts[i], pts[0], pts[pts.Count - 1], out _);
                if (d > maior) { maior = d; indice = i; }
            }
            if (maior <= tolerancia || indice < 0) return new List<Vector2> { pts[0], pts[pts.Count - 1] };
            var esquerda = DouglasPeucker(pts.GetRange(0, indice + 1), tolerancia);
            var direita = DouglasPeucker(pts.GetRange(indice, pts.Count - indice), tolerancia);
            esquerda.RemoveAt(esquerda.Count - 1);
            esquerda.AddRange(direita);
            return esquerda;
        }

        // ------------------------------------------------------------------ a barragem

        /// <summary>
        /// Troca a ponta do contorno que cai na caixa do recorte pelo funil e pela crista da barragem (do lado de a para
        /// o de b, na ordem em que o contorno anda).
        /// </summary>
        public static void RecortarNaRepresa(List<Vector2> contorno, RepresaDoMapa r)
        {
            if (contorno.Count < 4) return;
            bool NaCaixa(Vector2 p) => r.recorte.Contains(p);
            // o laço começa fora da caixa
            int fora = contorno.FindIndex(p => !NaCaixa(p));
            if (fora < 0) return;
            var rodado = new List<Vector2>(contorno.Count);
            for (int i = 0; i < contorno.Count; i++) rodado.Add(contorno[(fora + i) % contorno.Count]);
            // a maior sequência de pontos dentro da caixa
            int melhorIni = -1, melhorFim = -1, ini = -1;
            for (int i = 0; i <= rodado.Count; i++)
            {
                bool dentro = i < rodado.Count && NaCaixa(rodado[i]);
                if (dentro && ini < 0) ini = i;
                if (!dentro && ini >= 0)
                {
                    if (melhorIni < 0 || i - ini > melhorFim - melhorIni) { melhorIni = ini; melhorFim = i; }
                    ini = -1;
                }
            }
            if (melhorIni < 0) return;
            var funil = new List<Vector2>();
            funil.AddRange(r.funilOeste);
            funil.Add(r.a);
            funil.Add(r.b);
            funil.AddRange(r.funilLeste);
            var antes = rodado[melhorIni - 1];
            if (Vector2.Distance(antes, funil[funil.Count - 1]) < Vector2.Distance(antes, funil[0])) funil.Reverse();
            rodado.RemoveRange(melhorIni, melhorFim - melhorIni);
            rodado.InsertRange(melhorIni, funil);
            contorno.Clear();
            contorno.AddRange(rodado);
        }
    }
}
