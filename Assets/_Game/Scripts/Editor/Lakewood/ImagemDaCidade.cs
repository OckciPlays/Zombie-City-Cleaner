using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O plano de uma cidade do Lakewood (Plano_Lakewood, Etapa 6), a 1 m por pixel: o chão sombreado com as curvas de nível
    /// de 2 m, a água, as estradas da Etapa 5 e as ruas novas na largura de verdade, os lotes (a beira, o chão e a
    /// construção de cada tipo) e, pela UI da Unity, os nomes das ruas e dos especiais e o painel com a legenda, os números,
    /// a escala e o norte. Tudo sai do mapa (<see cref="MapaDeLakewood.ruas"/> e <see cref="MapaDeLakewood.lotes"/>), em
    /// `Imgs/Lakewood/Lakewood_&lt;cidade&gt;.png` da pasta de arte.
    /// </summary>
    public static class ImagemDaCidade
    {
        const float Margem = 70f;
        const int Painel = 400;
        static readonly Color Fundo = new Color(0.11f, 0.13f, 0.15f);
        static readonly Color Claro = new Color(0.88f, 0.9f, 0.92f);
        static readonly Color Cinza = new Color(0.62f, 0.66f, 0.7f);
        static readonly Color Amarelo = new Color(1f, 0.84f, 0.36f);
        static readonly Color Estrada = new Color(0.34f, 0.34f, 0.36f), BeiraDaEstrada = new Color(0.12f, 0.12f, 0.12f);

        /// <summary>Grava o plano da cidade e devolve o caminho (nulo sem lotes).</summary>
        public static string Gravar(MapaDeLakewood mapa, ChaoDeLakewood chao, string cidade, IDictionary<string, string> apelidos = null)
        {
            var lotes = mapa.lotes.FindAll(l => l.cidade == cidade);
            var ruas = mapa.ruas.FindAll(r => r.cidade == cidade);
            if (lotes.Count == 0) return null;

            // a caixa: os lotes e as ruas, com a margem
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            void Abrir(Vector2 p)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y);
            }
            foreach (var l in lotes) foreach (var c in l.Cantos()) Abrir(c);
            foreach (var r in ruas) foreach (var p in r.pontos) Abrir(p);
            x0 = Mathf.Floor(x0 - Margem); z0 = Mathf.Floor(z0 - Margem);
            int W = Mathf.CeilToInt(x1 + Margem - x0), H = Mathf.Max(Mathf.CeilToInt(z1 + Margem - z0), 900);
            var px = Chao(mapa, chao, x0, z0, W, H);

            void Pinta(int i, int j, Color c)
            {
                if (i < 0 || j < 0 || i >= W || j >= H) return;
                c.a = 1f;
                px[j * W + i] = c;
            }
            // uma caixa girada (o meio, o eixo da largura e as meias medidas, em metros)
            void Caixa(Vector2 meio, Vector2 lado, float meiaA, float meiaB, Color cor)
            {
                var fr = new Vector2(-lado.y, lado.x);
                float alcance = Mathf.Sqrt(meiaA * meiaA + meiaB * meiaB) + 1f;
                for (int j = Mathf.FloorToInt(meio.y - z0 - alcance); j <= Mathf.CeilToInt(meio.y - z0 + alcance); j++)
                for (int i = Mathf.FloorToInt(meio.x - x0 - alcance); i <= Mathf.CeilToInt(meio.x - x0 + alcance); i++)
                {
                    var q = new Vector2(x0 + i + 0.5f, z0 + j + 0.5f) - meio;
                    if (Mathf.Abs(Vector2.Dot(q, lado)) <= meiaA && Mathf.Abs(Vector2.Dot(q, fr)) <= meiaB) Pinta(i, j, cor);
                }
            }
            void Linha(IList<Vector2> pts, bool fechada, float meia, Color cor)
            {
                int n = pts.Count, lados = fechada ? n : n - 1;
                for (int s = 0; s < lados; s++)
                {
                    var a = pts[s];
                    var b = pts[(s + 1) % n];
                    int i0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - x0 - meia - 1), i1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) - x0 + meia + 1);
                    int j0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - z0 - meia - 1), j1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) - z0 + meia + 1);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        if (Linhas.DistanciaDoSegmento(new Vector2(x0 + i + 0.5f, z0 + j + 0.5f), a, b, out _) <= meia) Pinta(i, j, cor);
                }
            }

            // o contorno da cidade, tracejado
            var cid = mapa.cidades.Find(c => c.nome == cidade);
            if (cid != null && cid.contorno.Count >= 3)
            {
                var cont = Linhas.Reamostrar(MapaDeLakewood.Contorno(cid.contorno), 2f, true);
                for (int k = 0; k < cont.Count; k++)
                    if (k % 12 < 7) Linha(new[] { cont[k], cont[(k + 1) % cont.Count] }, false, 1.2f, new Color(0.95f, 0.9f, 0.7f));
            }

            // os lotes: a beira, o chão e a construção
            foreach (var l in lotes)
            {
                Caixa(l.meio, l.Lado, l.largura * 0.5f, l.fundo * 0.5f, PintorDeLakewood.BeiraDoLote);
                var chaoDoLote = l.tipo == UsoDoLote.Vazio ? PintorDeLakewood.LoteVazio : PintorDeLakewood.Quintal;
                Caixa(l.meio, l.Lado, l.largura * 0.5f - 1f, l.fundo * 0.5f - 1f, chaoDoLote);
                if (!PintorDeLakewood.Pegada(l, out var pm, out var meia)) continue;
                var c = l.NoMapa(pm.x, pm.y);
                Caixa(c, l.Lado, meia.x + 0.8f, meia.y + 0.8f, PintorDeLakewood.BeiraDoLote);
                Caixa(c, l.Lado, meia.x, meia.y, PintorDeLakewood.CorDoLote(l.tipo));
            }

            // as estradas da Etapa 5 e as ruas novas, na largura de verdade
            var caixa = Rect.MinMaxRect(x0 - 50f, z0 - 50f, x0 + W + 50f, z0 + H + 50f);
            var trechos = new List<(string nome, List<Vector2> pts, float meia, bool nova)>();
            foreach (var e in chao.Estradas)
            {
                List<Vector2> t = null;
                for (int i = 0; i <= e.Pontos.Length; i++)
                {
                    if (i < e.Pontos.Length && caixa.Contains(e.Pontos[i]))
                    {
                        (t ??= new List<Vector2>()).Add(e.Pontos[i]);
                        continue;
                    }
                    if (t != null && t.Count > 1) trechos.Add((e.Via.nome, t, e.MeiaLargura, false));
                    t = null;
                }
            }
            foreach (var r in ruas)
                if (r.pontos.Count >= 2) trechos.Add((r.nome, r.fechada ? new List<Vector2>(r.pontos) { r.pontos[0] } : r.pontos, r.MeiaLargura, true));
            foreach (var t in trechos) Linha(t.pts, false, t.meia + 0.9f, BeiraDaEstrada);
            foreach (var t in trechos) Linha(t.pts, false, t.meia, t.nova ? PintorDeLakewood.RuaNova * 0.85f : Estrada);

            var mapaTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            mapaTex.SetPixels(px);
            mapaTex.Apply();

            // os nomes: o trecho mais comprido de cada rua, no meio dele, em cima do asfalto (do lado, tapariam os lotes)
            var nomes = new List<(string texto, Vector2 ponto, float giro, int tam, FontStyle estilo, Color cor)>();
            var feitos = new HashSet<string>();
            trechos.Sort((a, b) => Linhas.Comprimento(b.pts).CompareTo(Linhas.Comprimento(a.pts)));
            foreach (var t in trechos)
            {
                if (!feitos.Add(t.nome) || Linhas.Comprimento(t.pts) < 60f) continue;
                var meio = Linhas.PontoNa(t.pts, 0.5f, out var dir);
                float giro = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (giro > 90f) giro -= 180f;
                if (giro < -90f) giro += 180f;
                string texto = apelidos != null && apelidos.TryGetValue(t.nome, out var ap) ? $"{ap} ({t.nome})" : t.nome;
                nomes.Add((texto, meio, giro, t.nova ? 12 : 14, t.nova ? FontStyle.Normal : FontStyle.Bold, Claro));
            }
            foreach (var l in lotes)
            {
                if (l.tipo == UsoDoLote.Casa || l.tipo == UsoDoLote.Vazio || l.tipo == UsoDoLote.Loja || l.tipo == UsoDoLote.Cabana) continue;
                PintorDeLakewood.Pegada(l, out var pm, out _);
                string nome = l.tipo == UsoDoLote.Hospital ? "Hospital" : char.ToUpper(l.Descricao[0]) + l.Descricao.Substring(1);
                var cor = l.tipo == UsoDoLote.Hospital ? new Color(0.1f, 0.1f, 0.14f) : Amarelo;
                nomes.Add((nome, l.NoMapa(pm.x, pm.y), 0f, l.tipo == UsoDoLote.Hospital ? 18 : 14, FontStyle.Bold, cor));
            }
            var cabanas = lotes.FindAll(l => l.tipo == UsoDoLote.Cabana);
            if (cabanas.Count > 0) nomes.Add(("Cabanas", DesenhoDaCidade.MeioDos(cabanas), 0f, 15, FontStyle.Bold, Amarelo));

            // o painel
            var linhas = new StringBuilder();
            int Conta(UsoDoLote t) => lotes.FindAll(l => l.tipo == t).Count;
            float metros = 0f;
            foreach (var r in ruas) metros += Linhas.Comprimento(r.pontos, r.fechada);
            linhas.Append($"{lotes.Count} lotes:\n  {Conta(UsoDoLote.Casa)} casas e {Conta(UsoDoLote.Vazio)} vazios\n  {Conta(UsoDoLote.Loja)} lojas\n");
            var outros = new List<string>();
            if (Conta(UsoDoLote.Posto) > 0) outros.Add("o posto");
            if (Conta(UsoDoLote.Hospital) > 0) outros.Add("o hospital");
            if (Conta(UsoDoLote.LojaDeArmas) > 0) outros.Add("a loja de armas");
            if (outros.Count > 0) linhas.Append("  " + string.Join(", ", outros) + "\n");
            linhas.Append($"  {Conta(UsoDoLote.Especial)} especiais e {Conta(UsoDoLote.Cabana)} cabanas\n");
            linhas.Append($"{ruas.Count} ruas novas ({metros / 1000f:F1} km)");
            string corpo = linhas.ToString();

            var saida = ImagemDeLakewood.Renderizar(W + Painel, H, Fundo, (t, fonte) =>
            {
                ImagemDeLakewood.Elemento<RawImage>(t, 0, 0, W, H).texture = mapaTex;
                foreach (var nm in nomes)
                    ImagemDeLakewood.Texto(t, fonte, nm.texto, nm.ponto - new Vector2(x0, z0), nm.tam, nm.cor, nm.estilo,
                                           nm.cor.grayscale < 0.3f ? Color.clear : new Color(0f, 0f, 0f, 0.9f), nm.giro);
                float x = W + 30f, y = H - 40f, w = Painel - 50f;
                ImagemDeLakewood.TextoAEsquerda(t, fonte, cidade, x, y, 38, Color.white, FontStyle.Bold, w);
                y -= 48f;
                ImagemDeLakewood.TextoAEsquerda(t, fonte, "A Etapa 6: as ruas e os lotes", x, y, 17, Claro, FontStyle.Normal, w);
                y -= 24f;
                ImagemDeLakewood.TextoAEsquerda(t, fonte, $"do mapa, {System.DateTime.Now:dd/MM/yyyy}; 1 px = 1 m", x, y, 14, Cinza, FontStyle.Italic, w);
                y -= 44f;
                ImagemDeLakewood.TextoAEsquerda(t, fonte, "Legenda", x, y, 22, Color.white, FontStyle.Bold, w);
                y -= 34f;
                void Item(string nome, Color cor, Color? borda = null)
                {
                    ImagemDeLakewood.Elemento<Image>(t, x - 2f, y - 13f, 44f, 24f).color = borda ?? PintorDeLakewood.BeiraDoLote;
                    ImagemDeLakewood.Elemento<Image>(t, x, y - 11f, 40f, 20f).color = cor;
                    ImagemDeLakewood.TextoAEsquerda(t, fonte, nome, x + 56f, y + 8f, 16, Claro, FontStyle.Normal, w - 56f);
                    y -= 30f;
                }
                Item("Casa", PintorDeLakewood.CorDoLote(UsoDoLote.Casa));
                Item("Lote vazio (a mata)", PintorDeLakewood.LoteVazio);
                Item("Loja", PintorDeLakewood.CorDoLote(UsoDoLote.Loja));
                Item("Posto", PintorDeLakewood.CorDoLote(UsoDoLote.Posto));
                Item("Hospital", PintorDeLakewood.CorDoLote(UsoDoLote.Hospital));
                Item("Loja de armas", PintorDeLakewood.CorDoLote(UsoDoLote.LojaDeArmas));
                Item("Especial (a fazer)", PintorDeLakewood.CorDoLote(UsoDoLote.Especial));
                Item("Cabana (a fazer)", PintorDeLakewood.CorDoLote(UsoDoLote.Cabana));
                Item("Chão do lote", PintorDeLakewood.Quintal);
                Item("Estrada (Etapa 5)", Estrada, BeiraDaEstrada);
                Item("Rua nova", PintorDeLakewood.RuaNova * 0.85f, BeiraDaEstrada);
                y -= 16f;
                var texto = ImagemDeLakewood.TextoAEsquerda(t, fonte, corpo, x, y, 16, Claro, FontStyle.Normal, w);
                texto.lineSpacing = 1.2f;
                // a escala (0, 100 e 200 m) e o norte
                float ey = 60f;
                ImagemDeLakewood.Elemento<Image>(t, x, ey, 200f, 10f).color = Color.white;
                ImagemDeLakewood.Elemento<Image>(t, x + 101f, ey + 2f, 97f, 6f).color = new Color(0.16f, 0.16f, 0.16f);
                ImagemDeLakewood.Texto(t, fonte, "0", new Vector2(x, ey + 26f), 14, Claro, FontStyle.Normal, Color.clear, 0f);
                ImagemDeLakewood.Texto(t, fonte, "100", new Vector2(x + 100f, ey + 26f), 14, Claro, FontStyle.Normal, Color.clear, 0f);
                ImagemDeLakewood.Texto(t, fonte, "200 m", new Vector2(x + 200f, ey + 26f), 14, Claro, FontStyle.Normal, Color.clear, 0f);
                ImagemDeLakewood.Texto(t, fonte, "↑ N", new Vector2(x + 300f, ey + 12f), 30, Color.white, FontStyle.Bold, Color.clear, 0f);
            });
            Object.DestroyImmediate(mapaTex);
            Directory.CreateDirectory(ImagemDeLakewood.Pasta);
            string caminho = Path.Combine(ImagemDeLakewood.Pasta, $"Lakewood_{cidade.Replace(" ", "")}.png");
            File.WriteAllBytes(caminho, saida.EncodeToPNG());
            Object.DestroyImmediate(saida);
            return caminho;
        }

        // o chão: o relevo sombreado (a luz do noroeste), a mata em manchas, as curvas de nível de 2 m e a água
        static Color[] Chao(MapaDeLakewood mapa, ChaoDeLakewood chao, float x0, float z0, int W, int H)
        {
            var alt = new float[(W + 2) * (H + 2)];
            for (int j = 0; j < H + 2; j++)
            for (int i = 0; i < W + 2; i++)
                alt[j * (W + 2) + i] = chao.Altura(x0 + i - 0.5f, z0 + j - 0.5f);
            float A(int i, int j) => alt[(j + 1) * (W + 2) + i + 1];
            var px = new Color[W * H];
            var sol = new Vector3(-0.5f, 0.75f, 0.45f).normalized;
            var mataClara = new Color(0.29f, 0.4f, 0.25f);
            var mataEscura = new Color(0.2f, 0.3f, 0.19f);
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float x = x0 + i + 0.5f, z = z0 + j + 0.5f, h = A(i, j);
                float dx = (A(i + 1, j) - A(i - 1, j)) * 0.5f, dz = (A(i, j + 1) - A(i, j - 1)) * 0.5f;
                var n = new Vector3(-dx, 1f, -dz).normalized;
                float luz = Mathf.Clamp01(Vector3.Dot(n, sol)) * 0.55f + 0.45f;
                float mancha = Mathf.PerlinNoise(x * 0.04f, z * 0.04f) * 0.6f + Mathf.PerlinNoise(x * 0.15f, z * 0.15f) * 0.4f;
                var cor = Color.Lerp(mataClara, mataEscura, mancha);
                cor = Color.Lerp(cor, PintorDeLakewood.CampoCor, mapa.Fracao(Cobertura.Campo, x, z) * 0.7f) * luz;
                if (chao.Agua(x, z, out float nivel) && nivel > h) cor = new Color(0.22f, 0.4f, 0.6f);
                else if (Mathf.FloorToInt(h / 2f) != Mathf.FloorToInt(A(i + 1, j) / 2f) || Mathf.FloorToInt(h / 2f) != Mathf.FloorToInt(A(i, j + 1) / 2f)) cor *= 0.85f;
                cor.a = 1f;
                px[j * W + i] = cor;
            }
            return px;
        }
    }
}
