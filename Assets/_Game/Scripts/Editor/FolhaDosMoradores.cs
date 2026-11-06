using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A folha dos moradores sorteados (a parte 2 do Plano_NPCs_Bunker.md da pasta de arte): a primeira mão das cartas de 4
    /// mundos (uma linha por mundo: o parente e mais 4), cada um vestido pela ficha (o <see cref="RoupaDoMorador"/>, na pose parada) e com a
    /// ficha em texto ao lado. Os corpos são fotografados numa cena de prévia (a deformação assada num MeshRenderer: a cena
    /// de prévia não refaz a deformação sozinha), e a folha é montada numa tela da UI, como as imagens do mapa.
    /// Menu: Zombie City Cleaner > Moradores > Folha dos sorteados.
    /// </summary>
    public static class FolhaDosMoradores
    {
        public const string Arquivo = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Imgs\Renders\Moradores_Folha.png";
        const string Catalogo = "Assets/_Game/Art/Characters/Roupas/Roupas.json";
        const string Npc = "Assets/_Game/Prefabs/Roupas/NPC.prefab";
        const string Corpo = "Assets/_Game/Art/Characters/Personagens/Personagem_PrincipalBloco.fbx";
        const int Camada = 31;

        // a folha: as células (o retrato e a ficha), as linhas (uma por grupo, com a faixa de cima) e o título
        const int RetratoL = 200, RetratoA = 400, CelulaL = 570, CelulaA = 500, Faixa = 30, Titulo = 50, Margem = 20;

        static readonly Color Fundo = new Color(0.11f, 0.11f, 0.105f), FundoDaFaixa = new Color(0.18f, 0.18f, 0.17f),
                              Letra = new Color(0.92f, 0.9f, 0.84f), Apagada = new Color(0.6f, 0.58f, 0.53f),
                              Barra = new Color(0.5f, 0.68f, 0.42f), FundoDaBarra = new Color(0.24f, 0.24f, 0.23f),
                              Chama = new Color(0.98f, 0.58f, 0.16f), FundoDoRetrato = new Color(0.16f, 0.16f, 0.17f);

        [MenuItem("Zombie City Cleaner/Moradores/Folha dos sorteados")]
        public static void Fazer() => Fazer(new[] { 1, 2, 3, 4 }, Arquivo);

        public static string Fazer(int[] sementes, string arquivo)
        {
            var catalogo = CatalogoDasRoupas.Ler(AssetDatabase.LoadAssetAtPath<TextAsset>(Catalogo));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Npc);
            var parado = AssetDatabase.LoadAllAssetsAtPath(Corpo).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Parado");
            // a primeira mão das cartas de cada mundo (o parente e mais 4)
            var grupos = sementes.Select(s =>
            {
                var m = SorteioDosMoradores.SortearMundo(s);
                return (mundo: m, mao: SorteioDosMoradores.Mao(m, 0));
            }).ToList();
            var retratos = new Dictionary<FichaDoMorador, Texture2D>();
            var log = new System.Text.StringBuilder();

            // os retratos, numa cena de prévia com luz de estúdio
            var cena = EditorSceneManager.NewPreviewScene();
            bool ligou = !AnimationMode.InAnimationMode();
            if (ligou) AnimationMode.StartAnimationMode();
            var criados = new List<Object>();
            try
            {
                Luz(cena, new Vector3(30f, 150f, 0f), 1.25f, new Color(1f, 0.96f, 0.9f));
                Luz(cena, new Vector3(20f, -120f, 0f), 0.55f, new Color(0.8f, 0.86f, 1f));
                Luz(cena, new Vector3(-40f, 0f, 0f), 0.3f, Color.white);
                var camGo = new GameObject("Câmera");
                SceneManager.MoveGameObjectToScene(camGo, cena);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = cena;
                cam.fieldOfView = 20f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = FundoDoRetrato;
                // de três quartos: o personagem olha para +Z, e a câmera fica na frente, 22° para a direita dele
                const float distancia = 5.9f;
                var giro = Quaternion.Euler(0f, 180f + 22f, 0f);
                cam.transform.SetPositionAndRotation(new Vector3(0f, 0.93f, 0f) - giro * Vector3.forward * distancia, giro);
                var rt = new RenderTexture(RetratoL * 2, RetratoA * 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                criados.Add(rt);
                cam.targetTexture = rt;

                foreach (var g in grupos)
                    foreach (var f in g.mao)
                    {
                        retratos[f] = Retrato(cena, cam, rt, prefab, catalogo, parado, f, criados);
                        log.AppendLine(SorteioDosMoradores.Descrever(f, g.mundo));
                    }
            }
            finally
            {
                if (ligou) AnimationMode.StopAnimationMode();
                EditorSceneManager.ClosePreviewScene(cena);
                foreach (var o in criados) if (o != null) Object.DestroyImmediate(o);
            }

            var folha = Montar(grupos, retratos);
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, folha.EncodeToPNG());
            Object.DestroyImmediate(folha);
            foreach (var t in retratos.Values) Object.DestroyImmediate(t);
            Debug.Log($"[Moradores] A folha dos sorteados em {arquivo}.\n{log}");
            return arquivo;
        }

        // um morador vestido, na pose parada, fotografado de três quartos
        static Texture2D Retrato(Scene cena, Camera cam, RenderTexture rt, GameObject prefab, CatalogoDasRoupas catalogo,
                                 AnimationClip parado, FichaDoMorador f, List<Object> criados)
        {
            var inst = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(inst, cena);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var assados = new List<GameObject>();
            try
            {
                var v = inst.GetComponent<Vestimenta>();
                RoupaDoMorador.Vestir(v, f.aparencia);
                if (parado != null)
                {
                    // a primeira amostra às vezes não pega: duas
                    for (int k = 0; k < 2; k++)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(inst, parado, 0f);
                        AnimationMode.EndSampling();
                    }
                }
                var bloco = new MaterialPropertyBlock();
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null) continue;
                    var malha = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    smr.BakeMesh(malha, true);
                    criados.Add(malha);
                    var go = new GameObject(smr.name + " (assado)");
                    SceneManager.MoveGameObjectToScene(go, cena);
                    go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    go.AddComponent<MeshFilter>().sharedMesh = malha;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = smr.sharedMaterials;
                    smr.GetPropertyBlock(bloco);
                    mr.SetPropertyBlock(bloco);
                    assados.Add(go);
                    smr.enabled = false;
                }
                cam.Render();
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var grande = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                grande.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                grande.Apply();
                RenderTexture.active = antes;
                var t = Reduzir(grande);
                Object.DestroyImmediate(grande);
                return t;
            }
            finally
            {
                foreach (var go in assados) Object.DestroyImmediate(go);
                Object.DestroyImmediate(inst);
            }
        }

        // pela metade (a foto sai no dobro, para as bordas ficarem lisas)
        static Texture2D Reduzir(Texture2D t)
        {
            int w = t.width / 2, h = t.height / 2;
            var p = t.GetPixels();
            var s = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = 2 * y * t.width + 2 * x;
                    s[y * w + x] = (p[i] + p[i + 1] + p[i + t.width] + p[i + t.width + 1]) * 0.25f;
                }
            var r = new Texture2D(w, h, TextureFormat.RGB24, false);
            r.SetPixels(s);
            r.Apply();
            return r;
        }

        static void Luz(Scene cena, Vector3 giro, float intensidade, Color cor)
        {
            var go = new GameObject("Luz");
            SceneManager.MoveGameObjectToScene(go, cena);
            go.transform.rotation = Quaternion.Euler(giro);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensidade;
            l.color = cor;
            l.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------------------------------------ a folha

        static int _altura;
        static Transform _tela;
        static Font _fonte;

        static Texture2D Montar(List<(NpcsDoMundo mundo, List<FichaDoMorador> mao)> grupos, Dictionary<FichaDoMorador, Texture2D> retratos)
        {
            int colunas = grupos.Max(g => g.mao.Count);
            int largura = Margem * 2 + colunas * CelulaL;
            int altura = Titulo + grupos.Count * (Faixa + CelulaA) + Margem;
            _altura = altura;

            var raiz = new GameObject("Folha dos moradores (temporária)") { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            var camera = new GameObject("Câmera") { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Camera>();
            camera.transform.SetParent(raiz.transform, false);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Fundo;
            camera.cullingMask = 1 << Camada;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            var canvas = new GameObject("Tela", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = Camada }.AddComponent<Canvas>();
            canvas.transform.SetParent(raiz.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            _tela = canvas.transform;
            _fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Caixa(0, 0, largura, altura, Fundo);
            Texto("OS MORADORES DO BUNKER  ·  sorteados pela ficha (o Plano_NPCs_Bunker.md, a parte 2)  ·  ♦ interessado, ♦♦ apaixonado",
                  Margem, 14, largura - 2 * Margem, 20, Letra, FontStyle.Bold);

            for (int gi = 0; gi < grupos.Count; gi++)
            {
                var g = grupos[gi];
                int y0 = Titulo + gi * (Faixa + CelulaA);
                Caixa(Margem, y0, largura - 2 * Margem, Faixa - 4, FundoDaFaixa);
                Texto($"MUNDO {gi + 1}  ·  semente {g.mundo.semente}  ·  a primeira mão das cartas  ·  {g.mundo.Lacos} laços entre os {g.mundo.fichas.Count}",
                      Margem + 8, y0 + 5, largura - 2 * Margem - 16, 14, Letra, FontStyle.Bold);
                for (int i = 0; i < g.mao.Count; i++)
                    Celula(g.mao[i], g.mundo, retratos[g.mao[i]], Margem + i * CelulaL, y0 + Faixa);
            }

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
            return saida;
        }

        // uma célula: o retrato à esquerda (com a roupa embaixo) e a ficha à direita
        static void Celula(FichaDoMorador f, NpcsDoMundo g, Texture2D retrato, int x0, int y0)
        {
            var img = Elemento<RawImage>(x0 + 4, y0 + 8, RetratoL, RetratoA);
            img.texture = retrato;
            Texto($"roupa: {f.aparencia.tipoDaRoupa}", x0 + 4, y0 + 12 + RetratoA, RetratoL, 11, Apagada, FontStyle.Italic);

            int x = x0 + RetratoL + 14, w = CelulaL - RetratoL - 26, y = y0 + 8;
            y += Texto(f.NomeCompleto, x, y, w, 16, Letra, FontStyle.Bold) + 1;
            y += Texto(SorteioDosMoradores.TextoDaIdade(f), x, y, w, 12, Apagada, FontStyle.Normal) + 6;
            y += Texto($"<b>ANTES:</b> {SorteioDosMoradores.NomeDeAntes(f)}. {SorteioDosMoradores.FraseDeAntes(f)}", x, y, w, 11, Letra, FontStyle.Normal) + 3;
            y += Texto($"<b>NOS ANOS DO APOCALIPSE:</b> {SorteioDosMoradores.NomeDeDepois(f)}. {SorteioDosMoradores.FraseDeDepois(f)}", x, y, w, 11, Letra, FontStyle.Normal) + 7;

            // as habilidades: o nome, a barra (de 0 a 20), o valor e as chamas
            for (int h = 0; h < ListasDosMoradores.Habilidades; h++)
            {
                bool incapaz = f.Incapaz((Habilidade)h);
                Texto(ListasDosMoradores.NomesDasHabilidades[h], x, y, 80, 11, incapaz ? Apagada : Letra, FontStyle.Normal);
                Caixa(x + 82, y + 3, 120, 9, FundoDaBarra);
                if (!incapaz && f.habilidades[h] > 0) Caixa(x + 82, y + 3, Mathf.RoundToInt(120f * f.habilidades[h] / 20f), 9, Barra);
                Texto(incapaz ? "—" : f.habilidades[h].ToString(), x + 208, y, 24, 11, incapaz ? Apagada : Letra, FontStyle.Bold);
                if (f.paixoes[h] != Paixao.Nenhuma)
                    Texto(f.paixoes[h] == Paixao.Apaixonado ? "♦♦" : "♦", x + 230, y - 1, 30, 12, Chama, FontStyle.Normal);
                y += 15;
            }
            y += 5;
            y += Texto($"<b>TRAÇOS:</b> {SorteioDosMoradores.TextoDosTracos(f)}", x, y, w, 11, Letra, FontStyle.Normal) + 2;
            y += Texto($"<b>CONDIÇÕES:</b> {SorteioDosMoradores.TextoDasCondicoes(f)}", x, y, w, 11, Letra, FontStyle.Normal) + 2;
            y += Texto($"<b>NÃO FAZ:</b> {SorteioDosMoradores.TextoDoNaoFaz(f)}", x, y, w, 11, Letra, FontStyle.Normal) + 2;
            var lacos = SorteioDosMoradores.TextosDosLacos(f, g);
            Texto($"<b>LAÇOS:</b> {(lacos.Count == 0 ? "—" : string.Join("; ", lacos))}", x, y, w, 11, Letra, FontStyle.Normal);
        }

        // um elemento com o canto de cima à esquerda em (x, y), em pixels da folha (y para baixo)
        static T Elemento<T>(float x, float y, float w, float h) where T : Component
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave, layer = Camada };
            go.transform.SetParent(_tela, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, _altura - y - h);
            rt.sizeDelta = new Vector2(w, h);
            return go.AddComponent<T>();
        }

        static void Caixa(float x, float y, float w, float h, Color cor) => Elemento<Image>(x, y, w, h).color = cor;

        // um texto que quebra na largura; devolve a altura que ele ocupou
        static int Texto(string texto, float x, float y, float w, int tamanho, Color cor, FontStyle estilo)
        {
            var t = Elemento<Text>(x, y, w, tamanho * 1.4f);
            t.font = _fonte;
            t.text = texto;
            t.fontSize = tamanho;
            t.fontStyle = estilo;
            t.color = cor;
            t.supportRichText = true;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1f;
            int alto = Mathf.CeilToInt(Mathf.Max(tamanho * 1.2f, t.preferredHeight));
            var rt = (RectTransform)t.transform;
            rt.anchoredPosition = new Vector2(x, _altura - y - alto);
            rt.sizeDelta = new Vector2(w, alto);
            return alto;
        }
    }
}
