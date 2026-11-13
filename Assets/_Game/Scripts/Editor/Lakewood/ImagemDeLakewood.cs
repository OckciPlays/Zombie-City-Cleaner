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
    /// A imagem do mapa de Lakewood (Plano_Lakewood): o mapa pintado (<see cref="PintorDeLakewood"/>, 3600 px nos 12 km,
    /// 3,3 m por pixel), com os nomes, os números dos escudos e, do lado, a legenda, a escala e os números do mapa, escritos
    /// pela UI da Unity numa câmera que desenha numa textura. Menu Zombie City Cleaner > Lakewood > Imagem do mapa:
    /// `Imgs/Lakewood/Lakewood_Mapa.png` da pasta de arte e a metade dela, `Lakewood_Mapa_Metade.png`.
    /// </summary>
    public static class ImagemDeLakewood
    {
        /// <summary>A pasta das imagens do Lakewood na pasta de arte (a referência do usuário, o mapa e as fotos).</summary>
        public const string Pasta = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Imgs\Lakewood";

        const int Camada = 31;
        const int LarguraDoMapa = 3600, Painel = 1300;
        static readonly Color Fundo = new Color(0.11f, 0.13f, 0.15f);
        static readonly Color TextoClaro = new Color(0.86f, 0.89f, 0.91f);

        [MenuItem("Zombie City Cleaner/Lakewood/Imagem do mapa", false, 21)]
        public static void Gravar()
        {
            var d = AssetDatabase.LoadAssetAtPath<MapaDeLakewood>(MapaDeLakewood.Caminho);
            if (d == null)
            {
                Debug.LogError($"[Lakewood] Não achei o mapa em {MapaDeLakewood.Caminho}.");
                return;
            }
            float t0 = Time.realtimeSinceStartup;
            EditorUtility.DisplayProgressBar("Lakewood", "Levantando o relevo", 0.1f);
            try
            {
                var relevo = new RelevoDeLakewood(d);
                EditorUtility.DisplayProgressBar("Lakewood", "Pintando o mapa", 0.4f);
                var imagem = Imagem(d, relevo, out string numeros);
                Directory.CreateDirectory(Pasta);
                File.WriteAllBytes(Path.Combine(Pasta, "Lakewood_Mapa.png"), imagem.EncodeToPNG());
                var metade = Metade(imagem);
                File.WriteAllBytes(Path.Combine(Pasta, "Lakewood_Mapa_Metade.png"), metade.EncodeToPNG());
                Object.DestroyImmediate(imagem);
                Object.DestroyImmediate(metade);
                Debug.Log($"[Lakewood] Imagem do mapa em {Pasta} ({Time.realtimeSinceStartup - t0:F0} s). {numeros}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Os números do mapa, para a legenda e o log.</summary>
        public static string Numeros(MapaDeLakewood d, List<PonteCalculada> pontes)
        {
            float agua = 0f;
            foreach (var l in d.lagos) if (l.contorno.Count >= 3) agua += Linhas.Area(MapaDeLakewood.Contorno(l.contorno));
            foreach (var i in d.ilhas) if (i.contorno.Count >= 3) agua -= Linhas.Area(MapaDeLakewood.Contorno(i.contorno));
            float rios = 0f;
            foreach (var r in d.rios) if (r.pontos.Count >= 2) rios += Linhas.Comprimento(MapaDeLakewood.Linha(r.pontos));
            var vias = new float[3];
            foreach (var e in d.estradas) if (e.pontos.Count >= 2) vias[(int)e.tipo] += Linhas.Comprimento(MapaDeLakewood.Linha(e.pontos));
            int ilhas = 0;
            foreach (var i in d.ilhas) if (i.tipo != TipoDeIlha.Ilhote) ilhas++;
            var cidades = new StringBuilder();
            foreach (var c in d.cidades)
            {
                if (!d.TemLotes(c.nome)) continue;
                int lotes = d.lotes.FindAll(l => l.cidade == c.nome).Count, vazios = d.lotes.FindAll(l => l.cidade == c.nome && l.tipo == UsoDoLote.Vazio).Count;
                cidades.Append($" {c.nome}: {lotes} lotes ({vazios} vazios) e {d.ruas.FindAll(r => r.cidade == c.nome).Count} ruas novas.");
            }
            return $"Mapa: {d.largura / 1000f:F0} × {d.comprimento / 1000f:F0} km ({d.largura * d.comprimento / 1e6f:F0} km²), {d.setoresX * d.setoresZ} setores. " +
                   $"Água: {agua / 1e6f:F1} km² ({100f * agua / (d.largura * d.comprimento):F0}%). Ilhas: {ilhas} (e {d.ilhas.Count - ilhas} ilhotes). Rios: {rios / 1000f:F1} km. " +
                   $"Vias: {vias[0] / 1000f:F1} km de rodovias, {vias[1] / 1000f:F1} de secundárias e {vias[2] / 1000f:F1} de terra. " +
                   $"Pontes: {pontes.Count}. Cidades: {d.cidades.Count}. Lugares: {d.lugares.Count}. Túneis reservados: {d.tuneis.Count}." + cidades;
        }

        /// <summary>A imagem do mapa: o mapa com os nomes e o painel da legenda do lado. Quem chama destrói a textura.</summary>
        public static Texture2D Imagem(MapaDeLakewood d, RelevoDeLakewood relevo, out string numeros)
        {
            var pontes = PontesDeLakewood.Calcular(d, relevo);
            numeros = Numeros(d, pontes);
            int alturaDoMapa = PintorDeLakewood.AlturaPara(d, LarguraDoMapa);
            var mapa = new Texture2D(LarguraDoMapa, alturaDoMapa, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            mapa.SetPixels32(PintorDeLakewood.Pintar(d, relevo, LarguraDoMapa, pontes));
            mapa.Apply();
            int largura = LarguraDoMapa + Painel, altura = alturaDoMapa;
            float mpp = d.largura / LarguraDoMapa;
            Vector2 NoMapa(Vector2 p) => p / mpp;

            var txt = numeros;
            var saida = Renderizar(largura, altura, Fundo, (t, fonte) =>
            {
                Elemento<RawImage>(t, 0, 0, LarguraDoMapa, alturaDoMapa).texture = mapa;

                // os nomes e os números dos escudos
                foreach (var r in PintorDeLakewood.Rotulos(d, relevo))
                {
                    int tam = Mathf.Clamp(Mathf.RoundToInt(r.Tamanho / mpp), 8, 300);
                    Texto(t, fonte, r.Espacado ? Espacar(r.Texto) : r.Texto, NoMapa(r.Ponto), tam, r.Cor, r.Estilo, new Color(0f, 0f, 0f, 0.85f), r.Giro);
                }
                foreach (var es in PintorDeLakewood.Escudos(d))
                {
                    float icone = Mathf.Max(100f, 7f * mpp);
                    int tam = Mathf.RoundToInt(icone * (es.Rodovia ? 1.25f : 1.35f) / mpp);
                    var cor = es.Rodovia ? Color.white : new Color(0.08f, 0.08f, 0.08f);
                    Texto(t, fonte, es.Numero, NoMapa(es.Ponto), tam, cor, FontStyle.Bold, Color.clear, 0f);
                }

                Lateral(t, fonte, d, txt, alturaDoMapa, mpp);
            });
            Object.DestroyImmediate(mapa);
            return saida;
        }

        /// <summary>
        /// Uma imagem feita pela UI da Unity: o fundo e o que "desenhar" põe na tela (os elementos com
        /// <see cref="Elemento{T}"/>, em pixels a partir do canto de baixo à esquerda), desenhada por uma câmera numa
        /// textura. Quem chama destrói a textura.
        /// </summary>
        internal static Texture2D Renderizar(int largura, int altura, Color fundo, System.Action<Transform, Font> desenhar)
        {
            var raiz = new GameObject("Imagem do Lakewood (temporária)") { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            var camera = new GameObject("Câmera") { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Camera>();
            camera.transform.SetParent(raiz.transform, false);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = fundo;
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
            Elemento<Image>(t, 0, 0, largura, altura).color = fundo;
            Texture2D saida;
            try
            {
                desenhar(t, fonte);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                saida = new Texture2D(largura, altura, TextureFormat.RGB24, false);
                saida.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
                saida.Apply();
                RenderTexture.active = antes;
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(raiz);
            }
            return saida;
        }

        // o painel da direita: o título, os números, a legenda, a escala e o norte
        static void Lateral(Transform t, Font fonte, MapaDeLakewood d, string numeros, int alturaDoMapa, float mpp)
        {
            float x = LarguraDoMapa + 70f, y = alturaDoMapa - 110f, w = Painel - 130f;
            TextoAEsquerda(t, fonte, "Lakewood County", x, y, 92, Color.white, FontStyle.Bold, w);
            y -= 118f;
            TextoAEsquerda(t, fonte, $"{d.nome}, Washington", x, y, 46, TextoClaro, FontStyle.Normal, w);
            y -= 60f;
            TextoAEsquerda(t, fonte, $"{d.largura / 1000f:F0} × {d.comprimento / 1000f:F0} km, o norte para cima; a origem no canto sudoeste", x, y, 34, TextoClaro, FontStyle.Normal, w);
            y -= 54f;
            string parte = d.estradas.Count == 0 ? "A geografia (Etapas 1 e 2)" : d.lotes != null && d.lotes.Count > 0 ? "O mapa (até a Etapa 6, em andamento)" : "O mapa (até a Etapa 5)";
            TextoAEsquerda(t, fonte, $"{parte}, {System.DateTime.Now:dd/MM/yyyy}", x, y, 32, new Color(0.62f, 0.66f, 0.7f), FontStyle.Italic, w);
            y -= 90f;
            var linhas = numeros.Replace(". ", ".\n");
            var corpo = TextoAEsquerda(t, fonte, linhas, x, y, 32, TextoClaro, FontStyle.Normal, w);
            corpo.lineSpacing = 1.15f;
            int quantas = 0;
            foreach (var l in linhas.Split('\n')) quantas += l.Length > 52 ? 2 : 1;
            y -= quantas * 43f + 60f;

            TextoAEsquerda(t, fonte, "Legenda", x, y, 54, Color.white, FontStyle.Bold, w);
            y -= 86f;
            void Item(string nome, System.Action<float, float> amostra)
            {
                amostra(x, y);
                TextoAEsquerda(t, fonte, nome, x + 210f, y + 6f, 36, TextoClaro, FontStyle.Normal, w - 210f);
                y -= 64f;
            }
            void Faixa(float ax, float ay, Color borda, Color miolo, float grossa, float fina)
            {
                Elemento<Image>(t, ax, ay - grossa * 0.5f, 170f, grossa).color = borda;
                Elemento<Image>(t, ax, ay - fina * 0.5f, 170f, fina).color = miolo;
            }
            void Tracos(float ax, float ay, Color cor, float grossa, int n)
            {
                for (int k = 0; k < n; k++) Elemento<Image>(t, ax + k * 170f / n, ay - grossa * 0.5f, 170f / n * 0.6f, grossa).color = cor;
            }
            void Quadro(float ax, float ay, Color cor, Color borda)
            {
                Elemento<Image>(t, ax + 35f, ay - 24f, 100f, 48f).color = borda;
                Elemento<Image>(t, ax + 43f, ay - 16f, 84f, 32f).color = cor;
            }
            void Cheio(float ax, float ay, Color cor) => Elemento<Image>(t, ax, ay - 22f, 170f, 44f).color = cor;
            bool Tem(TipoDeVia tipo) => d.estradas.Exists(e => e.tipo == tipo);
            if (Tem(TipoDeVia.Rodovia)) Item("Rodovia principal", (ax, ay) => Faixa(ax, ay, PintorDeLakewood.RodoviaBorda, PintorDeLakewood.RodoviaCor, 26f, 16f));
            if (Tem(TipoDeVia.Secundaria)) Item("Estrada secundária", (ax, ay) => Faixa(ax, ay, PintorDeLakewood.Contorno, PintorDeLakewood.SecundariaCor, 18f, 10f));
            if (Tem(TipoDeVia.Terra)) Item("Estrada de terra", (ax, ay) => Tracos(ax, ay, PintorDeLakewood.TerraCor, 8f, 5));
            if (PontesDeLakewood.Calcular(d).Count > 0) Item("Ponte", (ax, ay) => Quadro(ax, ay, PintorDeLakewood.PonteCor, PintorDeLakewood.PonteBorda));
            if (d.represa.Tem) Item("Barragem", (ax, ay) => Quadro(ax, ay, PintorDeLakewood.RepresaCor, PintorDeLakewood.RepresaBorda));
            Item("Rio e lago (pela fundura)", (ax, ay) => Cheio(ax, ay, PintorDeLakewood.AguaRasa));
            Item("Mata", (ax, ay) => Cheio(ax, ay, PintorDeLakewood.MataClara * 0.9f + new Color(0f, 0f, 0f, 1f)));
            Item("Campo e lavoura", (ax, ay) => Cheio(ax, ay, new Color(0.62f, 0.6f, 0.38f)));
            Item("Rocha e montanha", (ax, ay) => Cheio(ax, ay, PintorDeLakewood.Rocha));
            Item("Área urbana reservada", (ax, ay) => Cheio(ax, ay, PintorDeLakewood.CorDaCidade(TipoDeCidade.Pequena)));
            if (d.ruas != null && d.ruas.Count > 0) Item("Rua da cidade (Etapa 6)", (ax, ay) => Faixa(ax, ay, PintorDeLakewood.Contorno, PintorDeLakewood.RuaNova, 14f, 8f));
            if (d.lotes != null && d.lotes.Count > 0) Item("Lote e construção (Etapa 6)", (ax, ay) => Quadro(ax, ay, PintorDeLakewood.CorDoLote(UsoDoLote.Casa), PintorDeLakewood.Quintal));
            if (d.tuneis.Count > 0) Item("Túnel reservado (subterrâneo)", (ax, ay) => Tracos(ax, ay, PintorDeLakewood.TunelCor, 10f, 4));
            Item("Lugar de interesse", (ax, ay) =>
            {
                Elemento<Image>(t, ax + 62f, ay - 24f, 48f, 48f).color = Color.white;
                Elemento<Image>(t, ax + 68f, ay - 18f, 36f, 36f).color = new Color(0.13f, 0.15f, 0.17f);
            });
            Item("Setor (A1 no sudoeste, D4 no nordeste)", (ax, ay) => Tracos(ax, ay, new Color(1f, 1f, 1f, 0.6f), 3f, 1));

            // a escala (0, 1 e 2 km) e o norte, embaixo
            float ey = 150f;
            Elemento<Image>(t, x, ey, 1000f / mpp, 24f).color = Color.white;
            Elemento<Image>(t, x + 1000f / mpp, ey, 1000f / mpp, 24f).color = Color.white;
            Elemento<Image>(t, x + 1000f / mpp + 4f, ey + 4f, 1000f / mpp - 8f, 16f).color = new Color(0.12f, 0.12f, 0.12f);
            Texto(t, fonte, "0", new Vector2(x, ey + 62f), 36, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "1", new Vector2(x + 1000f / mpp, ey + 62f), 36, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "2 km", new Vector2(x + 2000f / mpp, ey + 62f), 36, TextoClaro, FontStyle.Normal, Color.clear, 0f);
            Texto(t, fonte, "↑ N", new Vector2(LarguraDoMapa + Painel - 130f, ey + 36f), 76, Color.white, FontStyle.Bold, Color.clear, 0f);
        }

        internal static string Espacar(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in texto)
            {
                if (sb.Length > 0) sb.Append(c == ' ' ? "  " : " ");
                if (c != ' ') sb.Append(c);
            }
            return sb.ToString();
        }

        internal static T Elemento<T>(Transform pai, float x, float y, float w, float h) where T : Component
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
        internal static void Texto(Transform pai, Font fonte, string texto, Vector2 meio, int tamanho, Color cor, FontStyle estilo, Color contorno, float giro)
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

        internal static Text TextoAEsquerda(Transform pai, Font fonte, string texto, float x, float y, int tamanho, Color cor, FontStyle estilo, float largura)
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

        internal static Texture2D Metade(Texture2D t)
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
