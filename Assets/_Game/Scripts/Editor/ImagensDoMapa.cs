using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As imagens do desenho do mapa (Plano_Mapa, Parte M3: "10 sementes de cada tamanho viram imagens"): o mapa pintado
    /// (<see cref="PintorDoMapa"/>), com os nomes, o título e a legenda por cima, escritos pela UI da Unity numa câmera que
    /// desenha numa textura (sem dar Play). Menu Zombie City Cleaner > Mundo > Imagens do mapa (10 sementes de cada
    /// tamanho): as sementes 1 a 10 dos três tamanhos em `Imgs/Mapas/Mapa_<tamanho>_<semente>.png` da pasta de arte, e uma
    /// folha com as 10 de cada tamanho lado a lado (`Mapas_<tamanho>.png`).
    /// </summary>
    public static class ImagensDoMapa
    {
        public const string Pasta = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Imgs\Mapas";
        const int Camada = 31;
        const int LadoDoMapa = 1024, Cabecalho = 46, Rodape = 64;

        [MenuItem("Zombie City Cleaner/Mundo/Imagens do mapa (10 sementes de cada tamanho)")]
        public static void Gerar() => Gerar(10);

        /// <summary>As imagens das sementes 1 a "sementes" dos tamanhos dados (todos, sem nenhum).</summary>
        public static void Gerar(int sementes, params TamanhoDoMundo[] tamanhos)
        {
            Directory.CreateDirectory(Pasta);
            float t0 = Time.realtimeSinceStartup;
            foreach (var tamanho in tamanhos.Length > 0 ? tamanhos : Tamanhos.Todos)
            {
                var miniaturas = new List<Texture2D>();
                for (int semente = 1; semente <= sementes; semente++)
                {
                    EditorUtility.DisplayProgressBar("Imagens do mapa", $"{Tamanhos.NomeComLado(tamanho)}, semente {semente}", 0f);
                    var d = GeradorDoMundo.Gerar(semente, tamanho);
                    var imagem = Imagem(d);
                    File.WriteAllBytes(Path.Combine(Pasta, $"Mapa_{Tamanhos.Nome(tamanho).Replace("é", "e")}_{semente:00}.png"), imagem.EncodeToPNG());
                    miniaturas.Add(Reduzir(imagem, 2));
                    Object.DestroyImmediate(imagem);
                }
                var folha = Folha(miniaturas, 5);
                File.WriteAllBytes(Path.Combine(Pasta, $"Mapas_{Tamanhos.Nome(tamanho).Replace("é", "e")}.png"), folha.EncodeToPNG());
                Object.DestroyImmediate(folha);
                foreach (var m in miniaturas) Object.DestroyImmediate(m);
            }
            EditorUtility.ClearProgressBar();
            Debug.Log($"[Mapa] Imagens do mapa em {Pasta} ({(Time.realtimeSinceStartup - t0):F0} s).");
        }

        /// <summary>A imagem de um mundo já gerado: o mapa, os nomes, o título e a legenda. Quem chama destrói a textura.</summary>
        public static Texture2D Imagem(DadosDoMundo d)
        {
            var mapa = new Texture2D(LadoDoMapa, LadoDoMapa, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            mapa.SetPixels32(PintorDoMapa.Pintar(d, LadoDoMapa));
            mapa.Apply();
            int largura = LadoDoMapa, altura = LadoDoMapa + Cabecalho + Rodape;

            var raiz = new GameObject("Imagem do mapa (temporária)") { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            var camera = new GameObject("Câmera") { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Camera>();
            camera.transform.SetParent(raiz.transform, false);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.115f);
            camera.cullingMask = 1 << Camada;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            var canvas = new GameObject("Tela", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Canvas>();
            canvas.transform.SetParent(raiz.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // o fundo das faixas de cima e de baixo, e o mapa entre elas (a origem da UI é embaixo, à esquerda)
            Elemento<Image>(canvas.transform, 0, 0, largura, altura).color = new Color(0.12f, 0.12f, 0.115f);
            var imagem = Elemento<RawImage>(canvas.transform, 0, Rodape, LadoDoMapa, LadoDoMapa);
            imagem.texture = mapa;
            Vector2 NoMapa(Vector2 p) => new Vector2((p.x + d.Metade) / d.Lado * LadoDoMapa, Rodape + (p.y + d.Metade) / d.Lado * LadoDoMapa);

            // os nomes, dos maiores para os menores; o que encostaria num já escrito fica de fora
            var rotulos = PintorDoMapa.Rotulos(d);
            rotulos.Sort((a, b) => a.Nivel.CompareTo(b.Nivel));
            var ocupados = new List<Rect>();
            foreach (var r in rotulos)
            {
                int tam = r.Nivel == 0 ? 22 : r.Nivel == 1 ? 16 : r.Nivel == 2 ? 12 : 10;
                var p = NoMapa(r.Ponto);
                var caixa = new Rect(p.x - r.Texto.Length * tam * 0.29f, p.y - tam * 0.6f, r.Texto.Length * tam * 0.58f, tam * 1.2f);
                bool encosta = false;
                foreach (var o in ocupados) encosta |= o.Overlaps(caixa);
                if (encosta) continue;
                ocupados.Add(caixa);
                var cor = r.Nivel == 3 ? new Color(0.24f, 0.22f, 0.2f) : new Color(0.08f, 0.08f, 0.08f);
                var estilo = r.Nivel <= 1 ? FontStyle.Bold : r.Nivel == 3 ? FontStyle.Italic : FontStyle.Normal;
                Texto(canvas.transform, fonte, r.Texto, p, tam, cor, estilo, new Color(1f, 0.98f, 0.92f, 0.85f));
            }

            string titulo = $"{Tamanhos.NomeComLado(d.Tamanho)}  ·  semente {d.Semente}  ·  gerador {d.Versao}  ·  " +
                            $"família em {d.Plano?.BairroDaFamilia?.Nome}, {d.Plano?.CidadeDaFamilia?.Nome}";
            Texto(canvas.transform, fonte, titulo, new Vector2(largura / 2f, altura - Cabecalho / 2f), 18, new Color(0.9f, 0.88f, 0.82f), FontStyle.Bold, Color.clear);
            Legenda(canvas.transform, fonte);

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

        // um texto com o meio no ponto (em pixels da imagem), com contorno se a cor dele não for transparente
        static void Texto(Transform pai, Font fonte, string texto, Vector2 meio, int tamanho, Color cor, FontStyle estilo, Color contorno)
        {
            var t = Elemento<Text>(pai, meio.x - 200f, meio.y - tamanho, 400f, tamanho * 2f);
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
                o.effectDistance = new Vector2(1.2f, -1.2f);
            }
        }

        static void Legenda(Transform pai, Font fonte)
        {
            var itens = new (string nome, Color cor)[]
            {
                ("Rodovia", new Color(0.93f, 0.72f, 0.28f)), ("Estrada", new Color(0.95f, 0.94f, 0.9f)), ("Terra", new Color(0.47f, 0.36f, 0.23f)),
                ("Metrô", new Color(0.72f, 0.12f, 0.12f)), ("Posto", new Color(0.95f, 0.5f, 0.12f)), ("Bunker", new Color(0.98f, 0.82f, 0.2f)),
                ("Cabana", new Color(0.55f, 0.38f, 0.22f)), ("Barreira e cerca", new Color(0.8f, 0.1f, 0.1f)), ("Base militar", new Color(0.45f, 0.48f, 0.36f)),
                ("Comunidade aliada", new Color(0.22f, 0.5f, 0.26f)), ("Comunidade inimiga", new Color(0.68f, 0.17f, 0.14f)),
                ("Laboratório", new Color(0.5f, 0.46f, 0.56f)), ("Casa da família", new Color(1f, 0.85f, 0.15f)),
            };
            float x = 14f, y = Rodape - 26f;
            foreach (var (nome, cor) in itens)
            {
                var quadrado = Elemento<Image>(pai, x, y + 4f, 14f, 14f);
                quadrado.color = cor;
                var t = Elemento<Text>(pai, x + 18f, y, 160f, 22f);
                t.font = fonte;
                t.text = nome;
                t.fontSize = 12;
                t.color = new Color(0.86f, 0.84f, 0.78f);
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                x += 18f + nome.Length * 6.6f + 16f;
                if (x > LadoDoMapa - 140f)
                {
                    x = 14f;
                    y -= 26f;
                }
            }
        }

        // a imagem reduzida pela metade (ou por "fator"), tirando a média dos pixels
        static Texture2D Reduzir(Texture2D t, int fator)
        {
            int w = t.width / fator, h = t.height / fator;
            var origem = t.GetPixels();
            var px = new Color[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var soma = Color.clear;
                for (int b = 0; b < fator; b++)
                for (int a = 0; a < fator; a++)
                    soma += origem[(j * fator + b) * t.width + i * fator + a];
                px[j * w + i] = soma / (fator * fator);
            }
            var r = new Texture2D(w, h, TextureFormat.RGB24, false);
            r.SetPixels(px);
            r.Apply();
            return r;
        }

        // as miniaturas em colunas (a primeira em cima, à esquerda)
        static Texture2D Folha(List<Texture2D> miniaturas, int colunas)
        {
            int w = miniaturas[0].width, h = miniaturas[0].height, linhas = (miniaturas.Count + colunas - 1) / colunas;
            var folha = new Texture2D(w * colunas, h * linhas, TextureFormat.RGB24, false);
            var fundo = new Color[folha.width * folha.height];
            for (int k = 0; k < fundo.Length; k++) fundo[k] = new Color(0.12f, 0.12f, 0.115f);
            folha.SetPixels(fundo);
            for (int n = 0; n < miniaturas.Count; n++)
            {
                int c = n % colunas, l = linhas - 1 - n / colunas;
                folha.SetPixels(c * w, l * h, w, h, miniaturas[n].GetPixels());
            }
            folha.Apply();
            return folha;
        }
    }
}
