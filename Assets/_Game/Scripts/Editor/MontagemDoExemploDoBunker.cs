using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena de teste do bunker novo (o Plano_Bunker.md da pasta de arte, a parte 2): `Scenes/Exemplos/Bunker.unity`
    /// (CenaDoBunker), no menu Zombie City Cleaner > Exemplos > Bunker (o novo), que refaz a cena; as fotos, em
    /// Zombie City Cleaner > Exemplos > Fotos do bunker (com a cena aberta): Imgs/Renders/Bunker_Casca_*.png da pasta de
    /// arte (as vistas, as plantas de cada andar e os dois cortes).
    /// </summary>
    public static partial class MontagemDoExemploDasCasas
    {
        const string CenaBunker = PastaCena + "/Bunker.unity";

        [MenuItem("Zombie City Cleaner/Exemplos/Bunker (o novo)")]
        public static void MontarBunker()
        {
            var cena = CenaNova();
            if (cena == null) return;
            var go = new GameObject("Bunker de teste");
            go.SetActive(false);
            var c = go.AddComponent<CenaDoBunker>();
            Comum(c);
            c.mobilia = MontagemDaMobiliaNova.Carregar();
            c.painelDeArmas = Malha("Assets/_Game/Art/Cidade/Cidade_Moveis.fbx", "Painel_Armas");
            c.fundadores = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Fundadores.mat");
            go.SetActive(true);
            // a parede de armas (as vagas) no lugar do painel do arsenal, como no bunker antigo
            MontagemDasArmas.MontarSuporte(cena.Value);
            Jogador(c.Paradas.Count > 0 ? c.Paradas[0].Chegada : new Vector3(4f, 0.1f, -32f));
            EditorSceneManager.SaveScene(cena.Value, CenaBunker);
            Debug.Log($"Cena do bunker montada: {CenaBunker}\n{c.Resumo}");
        }

        [MenuItem("Zombie City Cleaner/Exemplos/Fotos do bunker")]
        public static void FotosDoBunker()
        {
            var c = Object.FindAnyObjectByType<CenaDoBunker>();
            if (c == null) { Debug.LogWarning("Abra a cena do bunker antes."); return; }
            var sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            var sombras = sol != null ? sol.shadows : LightShadows.None;
            try
            {
                // as vistas, cada uma com a luz de onde a câmera está
                Vistas(c, PastaDasFotos + "Bunker_Casca_Vistas.png", 64f, 960, 540, 3,
                    (new Vector3(4f, 1.7f, -46f), new Vector3(4f, 3.5f, -12f)),          // a frente, da estrada
                    (new Vector3(-40f, 24f, -55f), new Vector3(2f, 2f, -4f)),            // o morro, de cima
                    (new Vector3(4f, 9.6f, -30f), new Vector3(-12f, 8.4f, 0f)),          // o alto do morro e a casinha
                    (new Vector3(4.5f, 1.65f, -10.8f), new Vector3(1.5f, 1.3f, 8f)),     // a garagem, do portão
                    (new Vector3(8f, 1.65f, -1f), new Vector3(-6f, 1.6f, -6f)),         // as operações, da garagem
                    (new Vector3(-10.6f, -2.35f, -1.6f), new Vector3(-15f, -4.6f, -1.6f)), // a escada, no B1
                    (new Vector3(-6f, -2.35f, -10.5f), new Vector3(5f, -3.0f, 7f)),     // B1, a sala de estar e a de jogos
                    (new Vector3(14f, -6.35f, -11f), new Vector3(-4f, -7.0f, 0f)),      // B2, o refeitório
                    (new Vector3(15f, -10.35f, 0f), new Vector3(-7f, -10.8f, 0f)),      // B3, o corredor dos quartos
                    (new Vector3(-8.5f, -10.35f, -11f), new Vector3(-8.5f, -10.8f, 10f)), // B3, o corredor do núcleo
                    (new Vector3(-6f, -14.35f, -1f), new Vector3(10f, -15f, -7f)),      // B4, a manutenção
                    (new Vector3(-10.5f, 8.75f, -0.4f), new Vector3(-15f, 5.5f, -2.4f))); // a casinha, por dentro

                // os cortes: pelo meio (a metade do fundo, de frente) e pela escada (de lado), com a câmera ortográfica
                c.Iluminar(new Vector3(0f, 1f, 0f));
                Corte(PastaDasFotos + "Bunker_Casca_Corte.png", new Vector3(0f, -2.5f, -80f), Vector3.forward, 80f, 17.5f, 2000, 1100);
                Corte(PastaDasFotos + "Bunker_Casca_Corte_Lado.png", new Vector3(-90f, -2.5f, -6f), Vector3.right, 90f - 13.2f, 17.5f, 2000, 1100);

                // as plantas de cada andar (sem a sombra do sol, que deixaria os cômodos no escuro)
                if (sol != null) sol.shadows = LightShadows.None;
                var p = new CenaDeExemplo.Parada { Ficha = "Bunker", Limites = c.Paradas[0].Limites };
                p.Pisos.AddRange(c.Paradas[0].Pisos);
                p.NomesDosPisos.AddRange(c.Paradas[0].NomesDosPisos);
                p.Comodos.AddRange(c.Paradas[0].Comodos);
                var imagens = new List<Texture2D>();
                for (int piso = p.Pisos.Count - 2; piso >= 0; piso--)
                {
                    c.Iluminar(new Vector3(0f, p.Pisos[piso] + 2.25f, 0f));
                    imagens.Add(Planta(p, piso, 900));
                }
                Grade(PastaDasFotos + "Bunker_Casca_Plantas.png", imagens, 3, 900);
            }
            finally
            {
                if (sol != null) sol.shadows = sombras;
                c.LuzDeFora();
            }
            Debug.Log("[Bunker] Fotos em Imgs/Renders/Bunker_Casca_*.png");
        }

        // a primeira fatia da parte 4 (o B0 e o B1), um cômodo por vista
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos da mobília do bunker")]
        public static void FotosDaMobiliaDoBunker() => FotosDosAndares("B0B1", new[] { 0, 1 },
            (new Vector3(-14.6f, 1.9f, -3.9f), new Vector3(-6f, 0.7f, -9.5f)),      // B0, as operações
            (new Vector3(-5.2f, 1.9f, 4.1f), new Vector3(-13f, 1.4f, 11.8f)),       // B0, o arsenal e a fabricação
            (new Vector3(4f, 1.7f, -11.3f), new Vector3(4f, 2.5f, 12f)),            // B0, a garagem, do portão
            (new Vector3(8f, 1.8f, 2f), new Vector3(-4f, 1.5f, -8f)),               // B0, a garagem, para as operações
            (new Vector3(2f, 1.8f, -10f), new Vector3(15f, 0.8f, 1f)),              // B0, a oficina e o combustível
            (new Vector3(-7.6f, -2.2f, -5.4f), new Vector3(-15f, -3.4f, -10.5f)),   // B1, o depósito
            (new Vector3(-7.6f, -2.2f, 5.0f), new Vector3(-15f, -3.4f, 9f)),        // B1, os banheiros
            (new Vector3(4.5f, -2.2f, -0.6f), new Vector3(-4f, -3.4f, -11f)),       // B1, a sala de estar
            (new Vector3(10.4f, -2.2f, 0.6f), new Vector3(-4f, -3.4f, 10f)),        // B1, a sala de jogos
            (new Vector3(-0.5f, -2.2f, 5f), new Vector3(-0.5f, -3.4f, -11f)),       // B1, dos jogos para a TV
            (new Vector3(6.6f, -2.2f, -0.6f), new Vector3(15f, -3.4f, -10f)),       // B1, a enfermaria
            (new Vector3(11.6f, -2.2f, 0.6f), new Vector3(15f, -3.4f, 10f)));       // B1, o isolamento

        // a segunda fatia (o B2 e o B3)
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos da mobília do bunker (B2 e B3)")]
        public static void FotosDaMobiliaDoB2EB3() => FotosDosAndares("B2B3", new[] { 2, 3 },
            (new Vector3(-5f, -6.1f, -0.6f), new Vector3(8f, -7.5f, -10f)),         // B2, o refeitório
            (new Vector3(15.4f, -6.1f, -11.4f), new Vector3(-1f, -7.0f, -1f)),      // B2, o refeitório, para o balcão
            (new Vector3(4.4f, -6.1f, 0.6f), new Vector3(-4f, -7.2f, 10f)),         // B2, a cozinha
            (new Vector3(-2f, -6.3f, 5f), new Vector3(-7f, -7.0f, 7.7f)),           // B2, a porta da câmara fria
            (new Vector3(-8.4f, -6.1f, 3.7f), new Vector3(-15f, -7.4f, 10f)),       // B2, a câmara fria
            (new Vector3(5.6f, -6.1f, 0.6f), new Vector3(14f, -7.2f, 9f)),          // B2, a despensa
            (new Vector3(-7.6f, -6.1f, -4.1f), new Vector3(-15f, -7.4f, -10.5f)),   // B2, a lavagem
            (new Vector3(4.4f, -10.1f, -4.5f), new Vector3(-6f, -11.4f, -9f)),      // B3, o dormitório
            (new Vector3(15.4f, -10.1f, 0f), new Vector3(-7f, -10.6f, 0f)),         // B3, o corredor dos quartos
            (new Vector3(-2.4f, -10.1f, 1.5f), new Vector3(-6f, -11.2f, 6.5f)),     // B3, o quarto 1
            (new Vector3(10.6f, -10.1f, -1.6f), new Vector3(6f, -11.2f, -10.5f)),   // B3, a lavanderia
            (new Vector3(14.6f, -10.1f, -2.7f), new Vector3(11.6f, -11.2f, -11f)),  // B3, a rouparia
            (new Vector3(-6.4f, -10.1f, 8.0f), new Vector3(14f, -11.2f, 10f)),      // B3, os banheiros e os chuveiros
            (new Vector3(-10.5f, -10.1f, -4.1f), new Vector3(-15.5f, -11.2f, -8.5f)), // B3, o quarto 5
            (new Vector3(-11.7f, -10.1f, 11.4f), new Vector3(-15.5f, -11.2f, 7f))); // B3, o quarto 6

        // a terceira fatia (o B4, a manutenção)
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos da mobília do bunker (B4)")]
        public static void FotosDaMobiliaDoB4() => FotosDosAndares("B4", new[] { 4 },
            (new Vector3(3.4f, -14.2f, 0.6f), new Vector3(-5f, -15.2f, 8f)),        // o gerador
            (new Vector3(-6.2f, -14.2f, 9f), new Vector3(2f, -15f, 3f)),            // o gerador, do fundo
            (new Vector3(-1.5f, -14.2f, -1.2f), new Vector3(-2f, -15f, -11f)),      // a elétrica
            (new Vector3(4.6f, -14.2f, -0.6f), new Vector3(13f, -15.2f, -9f)),      // a água
            (new Vector3(15.4f, -14.2f, -0.6f), new Vector3(6f, -15.2f, -10f)),     // a água, do fundo
            (new Vector3(4.6f, -14.2f, 0.6f), new Vector3(13f, -15.4f, 10f)),       // o ar
            (new Vector3(-7.4f, -14.2f, -5.2f), new Vector3(-14.5f, -15.2f, -10f)), // o esgoto
            (new Vector3(-7.6f, -14.2f, 4.8f), new Vector3(-15f, -15.2f, 10f)),     // as peças
            (new Vector3(-7.4f, -14.2f, 2.6f), new Vector3(-10f, -15f, -1.6f)));    // o hall e a porta restrita

        // as vistas (Bunker_Andares_<nome>_Vistas.png) e as plantas dos andares (_Plantas.png)
        static void FotosDosAndares(string nome, int[] andares, params (Vector3 pos, Vector3 alvo)[] vistas)
        {
            var c = Object.FindAnyObjectByType<CenaDoBunker>();
            if (c == null) { Debug.LogWarning("Abra a cena do bunker antes."); return; }
            var sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            var sombras = sol != null ? sol.shadows : LightShadows.None;
            try
            {
                Vistas(c, PastaDasFotos + $"Bunker_Andares_{nome}_Vistas.png", 64f, 960, 540, 3, vistas);
                if (sol != null) sol.shadows = LightShadows.None;
                Plantas(c, PastaDasFotos + $"Bunker_Andares_{nome}_Plantas.png", andares);
            }
            finally
            {
                if (sol != null) sol.shadows = sombras;
                c.LuzDeFora();
            }
            Debug.Log($"[Bunker] Fotos em Imgs/Renders/Bunker_Andares_{nome}_*.png");
        }

        // as plantas dos andares pedidos, lado a lado (a sombra do sol desligada antes)
        static void Plantas(CenaDoBunker c, string arquivo, params int[] andares)
        {
            var p = new CenaDeExemplo.Parada { Ficha = "Bunker", Limites = c.Paradas[0].Limites };
            p.Pisos.AddRange(c.Paradas[0].Pisos);
            p.NomesDosPisos.AddRange(c.Paradas[0].NomesDosPisos);
            p.Comodos.AddRange(c.Paradas[0].Comodos);
            var imagens = new List<Texture2D>();
            foreach (int f in andares)
            {
                int piso = p.Pisos.FindIndex(y => Mathf.Abs(y - PlantaDeBunker.Base(f)) < 0.01f);
                if (piso < 0) continue;
                c.Iluminar(new Vector3(0f, p.Pisos[piso] + 2.25f, 0f));
                imagens.Add(Planta(p, piso, 900));
            }
            Grade(arquivo, imagens, imagens.Count, 900);
        }

        // várias vistas numa grade, cada uma com a luz de onde a câmera está
        static void Vistas(CenaDoBunker c, string arquivo, float campo, int largura, int altura, int colunas, params (Vector3 pos, Vector3 alvo)[] vistas)
        {
            const int faixa = 6;
            int linhas = (vistas.Length + colunas - 1) / colunas;
            int W = colunas * largura + (colunas - 1) * faixa, H = linhas * altura + (linhas - 1) * faixa;
            var junta = new Texture2D(W, H, TextureFormat.RGB24, false);
            var fundo = new Color32[W * H];
            for (int i = 0; i < fundo.Length; i++) fundo[i] = new Color32(20, 20, 22, 255);
            junta.SetPixels32(fundo);
            for (int i = 0; i < vistas.Length; i++)
            {
                c.Iluminar(vistas[i].pos);
                var t = Renderizar(vistas[i].pos, vistas[i].alvo, campo, largura, altura, false);
                int col = i % colunas, lin = i / colunas;
                junta.SetPixels(col * (largura + faixa), H - (lin + 1) * altura - lin * faixa, largura, altura, t.GetPixels());
                Object.DestroyImmediate(t);
            }
            junta.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, junta.EncodeToPNG());
            Object.DestroyImmediate(junta);
        }

        // um corte: a câmera ortográfica olhando na direção 'dir', com o plano de perto a 'corte' metros dela (o que fica
        // antes dele some, e o bunker aparece aberto)
        static void Corte(string arquivo, Vector3 pos, Vector3 dir, float corte, float tamanho, int largura, int altura)
        {
            var go = new GameObject("Câmera do corte") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            Ajustar(cam, 60f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            cam.orthographic = true;
            cam.orthographicSize = tamanho;
            cam.nearClipPlane = corte;
            cam.farClipPlane = corte + 80f;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.18f);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
