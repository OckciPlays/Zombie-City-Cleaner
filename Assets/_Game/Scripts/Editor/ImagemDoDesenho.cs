using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A imagem do desenho do mapa fixo (Plano_Mapa_Fixo, Parte F1: "o mapa pintado a partir dele, em tamanho real"): o mapa
    /// pintado (<see cref="PintorDoDesenho"/>, 4096 px nos 15 km, 3,7 m por pixel), com os nomes, os números dos escudos e,
    /// do lado, a legenda, a escala e os números do mapa, escritos pela UI da Unity numa câmera que desenha numa textura (como
    /// as <see cref="ImagensDoMapa"/> da M3). Menu Zombie City Cleaner > Mapa fixo > Imagem do mapa: `Imgs/Mapas/MapaFixo.png`
    /// da pasta de arte e a metade dela, `MapaFixo_Metade.png`.
    /// </summary>
    public static class ImagemDoDesenho
    {
        const int Camada = 31;
        const int LadoDoMapa = 4096, Painel = 1500;
        static readonly Color Fundo = new Color(0.11f, 0.13f, 0.15f);
        static readonly Color TextoClaro = new Color(0.86f, 0.89f, 0.91f);

        [MenuItem("Zombie City Cleaner/Mapa fixo/Imagem do mapa")]
        public static void Gravar()
        {
            var d = AssetDatabase.LoadAssetAtPath<DesenhoDoMapa>(DesenhoDoMapa.Caminho);
            if (d == null)
            {
                Debug.LogError($"[Mapa fixo] Não achei o desenho em {DesenhoDoMapa.Caminho}.");
                return;
            }
            float t0 = Time.realtimeSinceStartup;
            EditorUtility.DisplayProgressBar("Mapa fixo", "Levantando o relevo", 0.1f);
            try
            {
                var relevo = new RelevoDoDesenho(d);
                EditorUtility.DisplayProgressBar("Mapa fixo", "Pintando o mapa", 0.4f);
                var imagem = Imagem(d, relevo, out string numeros);
                Directory.CreateDirectory(ImagensDoMapa.Pasta);
                File.WriteAllBytes(Path.Combine(ImagensDoMapa.Pasta, "MapaFixo.png"), imagem.EncodeToPNG());
                var metade = Metade(imagem);
                File.WriteAllBytes(Path.Combine(ImagensDoMapa.Pasta, "MapaFixo_Metade.png"), metade.EncodeToPNG());
                Object.DestroyImmediate(imagem);
                Object.DestroyImmediate(metade);
                Debug.Log($"[Mapa fixo] Imagem do mapa em {ImagensDoMapa.Pasta} ({Time.realtimeSinceStartup - t0:F0} s). {numeros}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Os números do desenho, para a legenda e o log.</summary>
        public static string Numeros(DesenhoDoMapa d, List<PonteCalculada> pontes)
        {
            float jogavel = d.quarentena.Count >= 3 ? Linhas.Area(DesenhoDoMapa.Contorno(d.quarentena)) : 0f;
            float agua = 0f;
            foreach (var l in d.lagos) if (l.contorno.Count >= 3) agua += Linhas.Area(DesenhoDoMapa.Contorno(l.contorno));
            foreach (var i in d.ilhas) if (i.contorno.Count >= 3) agua -= Linhas.Area(DesenhoDoMapa.Contorno(i.contorno));
            float rios = 0f;
            foreach (var r in d.rios) if (r.pontos.Count >= 2) rios += Linhas.Comprimento(DesenhoDoMapa.Linha(r.pontos));
            var vias = new float[4];
            foreach (var e in d.estradas) if (e.pontos.Count >= 2) vias[(int)e.tipo] += Linhas.Comprimento(DesenhoDoMapa.Linha(e.pontos));
            int bloqueadas = 0;
            foreach (var p in pontes) if (p.Estado == EstadoDaPonte.Bloqueada) bloqueadas++;
            return $"Jogável: {jogavel / 1e6f:F0} km². Lagos: {agua / 1e6f:F1} km². Rios: {rios / 1000f:F1} km. " +
                   $"Vias: {vias[0] / 1000f:F1} km de interestadual, {vias[1] / 1000f:F1} de estaduais, {vias[2] / 1000f:F1} do condado e {vias[3] / 1000f:F1} de terra. " +
                   $"Pontes: {pontes.Count} ({bloqueadas} bloqueadas). Cidades: {d.cidades.Count}. Lugares: {d.lugares.Count}.";
        }

        /// <summary>A imagem do desenho: o mapa com os nomes e o painel da legenda do lado. Quem chama destrói a textura.</summary>
        public static Texture2D Imagem(DesenhoDoMapa d, RelevoDoDesenho relevo, out string numeros)
        {
            var pontes = PontesDoDesenho.Calcular(d);
            numeros = Numeros(d, pontes);
            var mapa = new Texture2D(LadoDoMapa, LadoDoMapa, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            mapa.SetPixels32(PintorDoDesenho.Pintar(d, relevo, LadoDoMapa, pontes));
            mapa.Apply();
            int largura = LadoDoMapa + Painel, altura = LadoDoMapa;
            float mpp = DesenhoDoMapa.Lado / LadoDoMapa;
            Vector2 NoMapa(Vector2 p) => (p + Vector2.one * DesenhoDoMapa.Metade) / mpp;

            var raiz = new GameObject("Imagem do mapa fixo (temporária)") { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            var camera = new GameObject("Câmera") { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Camera>();
            camera.transform.SetParent(raiz.transform, false);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Fundo;
            camera.cullingMask = 1 << Camada;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = rt;
            var canvas = new GameObject("Tela", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Canvas>();
            canvas.transform.SetParent(raiz.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var t = canvas.transform;

            Elemento<Image>(t, 0, 0, largura, altura).color = Fundo;
            Elemento<RawImage>(t, 0, 0, LadoDoMapa, LadoDoMapa).texture = mapa;

            // os nomes e os números dos escudos
            foreach (var r in PintorDoDesenho.Rotulos(d, relevo))
            {
                int tam = Mathf.Clamp(Mathf.RoundToInt(r.Tamanho / mpp), 8, 300);
                Texto(t, fonte, r.Espacado ? Espacar(r.Texto) : r.Texto, NoMapa(r.Ponto), tam, r.Cor, r.Estilo, new Color(0f, 0f, 0f, 0.85f), r.Giro);
            }
            foreach (var es in PintorDoDesenho.Escudos(d))
            {
                float icone = Mathf.Max(120f, 7f * mpp);
                int tam = Mathf.RoundToInt(icone * (es.Interestadual ? 1.25f : 1.35f) / mpp);
                var cor = es.Interestadual ? Color.white : new Color(0.08f, 0.08f, 0.08f);
                Texto(t, fonte, es.Numero, NoMapa(es.Ponto) + (es.Interestadual ? Vector2.down * (icone * 0.25f / mpp) : Vector2.zero),
                      tam, cor, FontStyle.Bold, Color.clear, 0f);
            }

            Lateral(t, fonte, d, numeros);

            Canvas.ForceUpdateCanvases();
            camera.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var saida = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            saida.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
            saida.Apply();
            RenderTexture.active = antes;
            camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(raiz);
            Object.DestroyImmediate(mapa);
            return saida;
        }

        // o painel da direita: o título, os números, a legenda, a escala e o norte
        static void Lateral(Transform t, Font fonte, DesenhoDoMapa d, string numeros)
        {
            float x = LadoDoMapa + 80f, y = LadoDoMapa - 120f, w = Painel - 140f;
            TextoAEsquerda(t, fonte, "Mapa fixo", x, y, 104, Color.white, FontStyle.Bold, w);
            y -= 130f;
            TextoAEsquerda(t, fonte, $"{d.nome}, Washington", x, y, 50, TextoClaro, FontStyle.Normal, w);
            y -= 66f;
            TextoAEsquerda(t, fonte, "15 × 15 km, o norte para cima", x, y, 40, TextoClaro, FontStyle.Normal, w);
            y -= 58f;
            string parte = d.cidades.Count == 0 ? "O mapa limpo (Parte F1b)" : "O desenho (Parte F1)";
            TextoAEsquerda(t, fonte, $"{parte}, {System.DateTime.Now:dd/MM/yyyy}", x, y, 36, new Color(0.62f, 0.66f, 0.7f), FontStyle.Italic, w);
            y -= 100f;
            var linhas = numeros.Replace(". ", ".\n");
            var corpo = TextoAEsquerda(t, fonte, linhas, x, y, 36, TextoClaro, FontStyle.Normal, w);
            corpo.lineSpacing = 1.15f;
            int quantas = 0;
            foreach (var l in linhas.Split('\n')) quantas += l.Length > 46 ? 2 : 1;
            y -= quantas * 48f + 70f;

            TextoAEsquerda(t, fonte, "Legenda", x, y, 60, Color.white, FontStyle.Bold, w);
            y -= 96f;
            void Item(string nome, System.Action<float, float> amostra)
            {
                amostra(x, y);
                TextoAEsquerda(t, fonte, nome, x + 230f, y + 6f, 40, TextoClaro, FontStyle.Normal, w - 230f);
                y -= 70f;
            }
            void Faixa(float ax, float ay, Color borda, Color miolo, float grossa, float fina)
            {
                Elemento<Image>(t, ax, ay - grossa * 0.5f, 190f, grossa).color = borda;
                Elemento<Image>(t, ax, ay - fina * 0.5f, 190f, fina).color = miolo;
            }
            void Tracos(float ax, float ay, Color cor, float grossa, int n)
            {
                for (int k = 0; k < n; k++) Elemento<Image>(t, ax + k * 190f / n, ay - grossa * 0.5f, 190f / n * 0.6f, grossa).color = cor;
            }
            void Quadro(float ax, float ay, Color cor, Color borda)
            {
                Elemento<Image>(t, ax + 40f, ay - 26f, 110f, 52f).color = borda;
                Elemento<Image>(t, ax + 49f, ay - 17f, 92f, 34f).color = cor;
            }
            void Cheio(float ax, float ay, Color cor) => Elemento<Image>(t, ax, ay - 24f, 190f, 48f).color = cor;
            Item("Interestadual (I-90)", (ax, ay) => Faixa(ax, ay, PintorDoDesenho.InterestadualBorda, PintorDoDesenho.InterestadualCor, 28f, 17f));
            // só o que o desenho tem (o mapa limpo cresce aos poucos), com os números das estaduais dele
            bool Tem(TipoDeVia tipo) => d.estradas.Exists(e => e.tipo == tipo);
            var rotas = new SortedSet<int>();
            foreach (var via in d.estradas)
                if (via.tipo == TipoDeVia.Estadual && int.TryParse(via.numero, out int rota)) rotas.Add(rota);
            if (Tem(TipoDeVia.Estadual))
                Item(rotas.Count > 0 ? $"Rodovia estadual ({string.Join(", ", rotas)})" : "Rodovia estadual",
                    (ax, ay) => Faixa(ax, ay, PintorDoDesenho.Contorno, PintorDoDesenho.EstadualCor, 22f, 13f));
            if (Tem(TipoDeVia.Condado)) Item("Estrada do condado", (ax, ay) => Faixa(ax, ay, PintorDoDesenho.Contorno, PintorDoDesenho.CondadoCor, 16f, 9f));
            if (d.cidades.Exists(c => c.avenidas.Count > 0)) Item("Avenida da cidade", (ax, ay) => Faixa(ax, ay, PintorDoDesenho.Contorno, PintorDoDesenho.Rua, 12f, 7f));
            if (Tem(TipoDeVia.Terra)) Item("Estrada de terra", (ax, ay) => Tracos(ax, ay, PintorDoDesenho.TerraCor, 8f, 5));
            if (d.ferrovia.Count >= 2)
                Item("Ferrovia", (ax, ay) =>
                {
                    Elemento<Image>(t, ax, ay - 6f, 190f, 12f).color = PintorDoDesenho.FerroviaCor;
                    Tracos(ax, ay, new Color(0.91f, 0.91f, 0.91f), 5f, 4);
                });
            Item("Ponte", (ax, ay) => Quadro(ax, ay, PintorDoDesenho.PonteCor, PintorDoDesenho.PonteBorda));
            if (d.pontes.Exists(p => p.estado == EstadoDaPonte.Bloqueada))
                Item("Ponte bloqueada (só a pé)", (ax, ay) => Quadro(ax, ay, PintorDoDesenho.PonteCor, PintorDoDesenho.BloqueadaCor));
            Item("Linha da quarentena", (ax, ay) => Tracos(ax, ay, PintorDoDesenho.QuarentenaCor, 12f, 3));
            Item("Bloqueio (saída do mapa)", (ax, ay) => Quadro(ax, ay, PintorDoDesenho.QuarentenaCor, Color.white));
            Item("Rio e lago", (ax, ay) => Cheio(ax, ay, PintorDoDesenho.AguaRasa));
            if (PintorDoDesenho.TemCasaDaFamilia(d)) Item("Casa da família", (ax, ay) => Quadro(ax, ay, PintorDoDesenho.FamiliaCor, new Color(0.23f, 0.16f, 0f)));
            Item("Horizonte (fora do jogo)", (ax, ay) => Cheio(ax, ay, PintorDoDesenho.MataEscura * 0.78f + new Color(0.08f, 0.08f, 0.08f, 1f)));
            if (d.cidades.Count > 0)
            {
                y -= 30f;
                TextoAEsquerda(t, fonte, "Os distritos", x, y, 50, Color.white, FontStyle.Bold, w);
                y -= 84f;
                var nomes = new Dictionary<TipoDeDistrito, string>
                {
                    { TipoDeDistrito.Centro, "Centro (downtown)" }, { TipoDeDistrito.RuaPrincipal, "Rua principal (Main Street)" },
                    { TipoDeDistrito.Comercial, "Faixa comercial" }, { TipoDeDistrito.Bairro, "Bairro" },
                    { TipoDeDistrito.Suburbio, "Subúrbio (ruas curvas)" }, { TipoDeDistrito.Industrial, "Industrial" },
                    { TipoDeDistrito.Parque, "Parque" }, { TipoDeDistrito.Marina, "Marina" },
                };
                foreach (var par in nomes)
                {
                    var cor = PintorDoDesenho.CorDoDistrito(par.Key);
                    Item(par.Value, (ax, ay) => Cheio(ax, ay, cor));
                }
            }

            // a escala (0, 1 e 2,5 km) e o norte, embaixo
            float mpp = DesenhoDoMapa.Lado / LadoDoMapa, ey = 170f;
            Elemento<Image>(t, x, ey, 1000f / mpp, 26f).color = Color.white;
            Elemento<Image>(t, x + 1000f / mpp, ey, 1500f / mpp, 26f).color = Color.white;
            Elemento<Image>(t, x + 1000f / mpp + 4f, ey + 4f, 1500f / mpp - 8f, 18f).color = new Color(0.12f, 0.12f, 0.12f);
            Texto(t, fonte, "0", new Vector2(x, ey + 70f), 40, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "1", new Vector2(x + 1000f / mpp, ey + 70f), 40, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "2,5 km", new Vector2(x + 2500f / mpp, ey + 70f), 40, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "↑ N", new Vector2(LadoDoMapa + Painel - 150f, ey + 40f), 84, Color.white, FontStyle.Bold, Color.clear, 0f);
        }

        static string Espacar(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in texto)
            {
                if (sb.Length > 0) sb.Append(c == ' ' ? "  " : " ");
                if (c != ' ') sb.Append(c);
            }
            return sb.ToString();
        }

        static T Elemento<T>(Transform pai, float x, float y, float w, float h) where T : Component
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            go.transform.SetParent(pai, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return go.AddComponent<T>();
        }

        // um texto com o meio no ponto (em pixels da imagem), girado, com contorno se a cor dele não for transparente
        static void Texto(Transform pai, Font fonte, string texto, Vector2 meio, int tamanho, Color cor, FontStyle estilo, Color contorno, float giro)
        {
            var t = Elemento<Text>(pai, meio.x, meio.y, 4000f, tamanho * 2f);
            var rt = (RectTransform)t.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, giro);
            t.font = fonte;
            t.text = texto;
            t.fontSize = tamanho;
            t.fontStyle = estilo;
            t.color = cor;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (contorno.a > 0f)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = contorno;
                float dist = Mathf.Max(1.5f, tamanho * 0.06f);
                o.effectDistance = new Vector2(dist, -dist);
            }
        }

        static Text TextoAEsquerda(Transform pai, Font fonte, string texto, float x, float y, int tamanho, Color cor, FontStyle estilo, float largura)
        {
            var t = Elemento<Text>(pai, x, y - tamanho, largura, tamanho * 1.6f);
            ((RectTransform)t.transform).pivot = new Vector2(0f, 1f);
            ((RectTransform)t.transform).anchoredPosition = new Vector2(x, y + tamanho * 0.6f);
            t.font = fonte;
            t.text = texto;
            t.fontSize = tamanho;
            t.fontStyle = estilo;
            t.color = cor;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Texture2D Metade(Texture2D t)
        {
            int w = t.width / 2, h = t.height / 2;
            var origem = t.GetPixels32();
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int r = 0, g = 0, b = 0;
                for (int dj = 0; dj < 2; dj++)
                for (int di = 0; di < 2; di++)
                {
                    var c = origem[(j * 2 + dj) * t.width + i * 2 + di];
                    r += c.r; g += c.g; b += c.b;
                }
                px[j * w + i] = new Color32((byte)(r / 4), (byte)(g / 4), (byte)(b / 4), 255);
            }
            var saida = new Texture2D(w, h, TextureFormat.RGB24, false);
            saida.SetPixels32(px);
            saida.Apply();
            return saida;
        }
    }
}
