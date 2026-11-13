using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Music;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena de teste da loja de armas (o Plano_LojaDeArmas.md da pasta de arte): `Scenes/Exemplos/LojaDeArmas.unity`
    /// (<see cref="CenaDaLojaDeArmas"/>), no menu Zombie City Cleaner > Exemplos > Loja de armas, que refaz a cena (com os sons
    /// e os papéis); as fotos em Zombie City Cleaner > Exemplos > Fotos da loja de armas, com a cena aberta
    /// (Imgs/Renders/LojaDeArmas_*.png da pasta de arte: por fora, por dentro, os três estados da sala segura e a planta).
    /// </summary>
    public static partial class MontagemDoExemploDasCasas
    {
        const string CenaLojaDeArmas = PastaCena + "/LojaDeArmas.unity";

        [MenuItem("Zombie City Cleaner/Exemplos/Loja de armas")]
        public static void MontarLojaDeArmas()
        {
            var cena = CenaNova();
            if (cena == null) return;
            var go = new GameObject("Loja de armas de teste");
            go.SetActive(false);
            var c = go.AddComponent<CenaDaLojaDeArmas>();
            Comum(c);
            var abandono = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoAbandono);
            if (abandono == null) abandono = TexturasDoAbandono.Gerar();
            c.material = abandono;
            c.vegetacao = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            c.arbustoA = Malha(FbxArvores, "Arbusto_A");
            c.arbustoB = Malha(FbxArvores, "Arbusto_B");
            c.arbustoC = Malha(FbxArvores, "Arbusto_C");
            c.lixeira = Malha(FbxRua, "Lixeira");
            c.cacamba = Malha(FbxRua, "Cacamba");
            c.mobilia = MontagemDaMobiliaNova.Carregar();
            c.materialDasMarcas = TexturasDoAbandono.Marcas();
            c.materialDasPichacoes = TexturasDoAbandono.Pichacoes();
            go.SetActive(true);
            Jogador(c.Paradas.Count > 1 ? c.Paradas[0].Chegada : new Vector3(MeioDaEntrada, ChaoDeFora + 0.1f, Z0 - 13f));
            // os sons e os papéis (do assembly do jogo), que acompanham a variação no Play (o LojaDeArmasNaCena)
            var sons = SonsDaLojaNaCena(c);
            var papeis = PapeisDaLojaNaCena(c);
            var junto = new GameObject("Papéis e sons da loja (acompanham a variação)").AddComponent<World.LojaDeArmasNaCena>();
            junto.papeis = papeis;
            junto.sons = sons;
            EditorSceneManager.SaveScene(cena.Value, CenaLojaDeArmas);
            Debug.Log($"Cena da loja de armas montada: {CenaLojaDeArmas}\n{c.Resumo}");
        }

        /// <summary>Os sons da loja (os do hospital, com os pesos da loja numa área só) e os pontos da variação da cena.</summary>
        static AmbienteSonoro SonsDaLojaNaCena(CenaDaLojaDeArmas c)
        {
            var go = new GameObject("Sons da loja de armas");
            var amb = go.AddComponent<AmbienteSonoro>();
            var tipos = TiposDeSomDoHospital();
            for (int i = 0; i < tipos.Length; i++) tipos[i].pesos = new[] { i < SonsDaLojaDeArmas.Pesos.Length ? SonsDaLojaDeArmas.Pesos[i] : 0f };
            amb.tipos = tipos;
            amb.areas = SonsDaLojaDeArmas.Areas();
            amb.limites = SonsDaLojaDeArmas.Limites();
            amb.pontos = SonsDaLojaDeArmas.Pontos(c.Variacao).Select(p => new AmbienteSonoro.Ponto { lugar = p.lugar, tipo = p.tipo, area = p.area }).ToList();
            Debug.Log($"[Loja de armas] Sons: {amb.pontos.Count} pontos, {amb.tipos.Sum(t => t.clipes.Length)} sons");
            return amb;
        }

        static Documentos.PapeisDoLugar PapeisDaLojaNaCena(CenaDaLojaDeArmas c)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MontagemDosPapeis.Pasta + "/M_Papeis.mat") == null) MontagemDosPapeis.Montar();
            var go = new GameObject("Papéis da loja de armas");
            var papeis = go.AddComponent<Documentos.PapeisDoLugar>();
            papeis.arquivo = "Documentos/LojaDeArmas";
            papeis.material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDosPapeis.Pasta + "/M_Papeis.mat");
            papeis.papeis = DocumentosDaLojaDeArmas.Papeis(c.Variacao).Select(Documentos.PapeisDoLugar.De).ToList();
            foreach (var f in DocumentosDaLojaDeArmas.Faltas) Debug.LogWarning("[Loja de armas] Papel: " + f);
            Debug.Log($"[Loja de armas] Papéis: {papeis.papeis.Count}");
            return papeis;
        }

        // ------------------------------------------------------------ as fotos

        [MenuItem("Zombie City Cleaner/Exemplos/Fotos da loja de armas")]
        public static void FotosDaLojaDeArmas()
        {
            var c = Object.FindAnyObjectByType<CenaDaLojaDeArmas>();
            if (c == null) { Debug.LogWarning("Abra a cena da loja de armas antes."); return; }
            var sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            var sombras = sol != null ? sol.shadows : LightShadows.None;
            bool neblina = RenderSettings.fog;
            var salaAntes = c.sala;
            try
            {
                const string p = "LojaDeArmas_";
                VistasDaLoja(c, PastaDasFotos + p + "Fora.png", 58f, 960, 600, 3,
                    (new Vector3(-4.2f, 1.7f, -30f), new Vector3(-1.5f, 3.6f, -15f)),        // a frente, do estacionamento
                    (new Vector3(-25f, 4f, -36f), new Vector3(-1f, 3f, -11f)),               // de três quartos, do sudoeste
                    (new Vector3(-4.2f, 2.0f, -22.5f), new Vector3(-4.2f, 5.0f, -15f)),     // o letreiro e as vitrines
                    (new Vector3(19f, 2.0f, -25f), new Vector3(12f, 2.4f, -3f)),             // a ala do estande e a faixa vermelha
                    (new Vector3(-13.5f, 1.8f, 18.6f), new Vector3(4f, 1.4f, 15.4f)),        // o beco dos fundos
                    (new Vector3(-32f, 30f, -46f), new Vector3(0f, 0f, -4f)));               // de cima
                VistasDaLoja(c, PastaDasFotos + p + "Dentro.png", 70f, 960, 540, 3,
                    (new Vector3(-4.2f, 1.7f, -13.6f), new Vector3(-4.2f, 1.2f, -1f)),      // a entrada, para dentro
                    (new Vector3(-1.5f, 1.7f, -6f), new Vector3(-9f, 1.1f, 2.5f)),           // o balcão em L
                    (new Vector3(1.6f, 1.7f, 5.7f), new Vector3(-11f, 1.4f, 5.2f)),          // atrás do balcão, as armas longas
                    (new Vector3(2.6f, 1.7f, -10.5f), new Vector3(-6f, 0.9f, -3f)),          // as gôndolas e os painéis
                    (new Vector3(4.2f, 1.7f, -14.3f), new Vector3(7.5f, 1.3f, -8.4f)),       // a antessala do estande
                    (new Vector3(7.6f, 1.7f, -7.4f), new Vector3(7.4f, 1.4f, 10f)),          // o estande, da área de espera
                    (new Vector3(4.3f, 1.7f, -3.2f), new Vector3(7.8f, 0.7f, 4f)),           // o fundo do estande, da passagem
                    (new Vector3(-1.3f, 1.7f, 8.1f), new Vector3(-5.6f, 1.3f, 11.4f)),       // o estoque e a porta de cofre
                    (new Vector3(-6.5f, 1.7f, 8.4f), new Vector3(-10.5f, 0.9f, 13.6f)),      // dentro da sala segura
                    (new Vector3(0.2f, 1.7f, 7.8f), new Vector3(2.4f, 0.9f, 14.5f)),         // o escritório
                    (new Vector3(9.0f, 1.7f, -13.7f), new Vector3(11.6f, 0.9f, -11.9f)),     // o banheiro
                    (new Vector3(-1.2f, 1.6f, -3.5f), new Vector3(3.4f, 1.7f, 1.5f)));        // os painéis dos acessórios e o troféu
                // os três estados da sala segura: do estoque para a porta e de dentro
                var imagens = new List<Texture2D>();
                var linhaDeFora = new List<Texture2D>();
                foreach (var s in new[] { CenaDaLojaDeArmas.Sala.A_Saqueada, CenaDaLojaDeArmas.Sala.B_Parcial, CenaDaLojaDeArmas.Sala.C_Intacta })
                {
                    c.sala = s;
                    c.Gerar();
                    linhaDeFora.Add(Vista(c, new Vector3(-1.3f, 1.7f, 8.1f), new Vector3(-5.6f, 1.3f, 11.4f), 70f, 960, 540));
                    imagens.Add(Vista(c, new Vector3(-6.6f, 1.7f, 8.4f), new Vector3(-10.5f, 0.8f, 13.6f), 70f, 960, 540));
                }
                linhaDeFora.AddRange(imagens);
                GradeRetangular(PastaDasFotos + p + "SalaSegura_Estados.png", linhaDeFora, 3);
                c.sala = salaAntes;
                c.Gerar();
                if (sol != null) sol.shadows = LightShadows.None;
                var planta = PlantaDaLoja(1800);
                GradeRetangular(PastaDasFotos + p + "Planta.png", new List<Texture2D> { planta }, 1);
            }
            finally
            {
                if (c.sala != salaAntes) { c.sala = salaAntes; c.Gerar(); }
                if (sol != null) sol.shadows = sombras;
                RenderSettings.fog = neblina;
                c.LuzDeFora();
            }
            Debug.Log("[Loja de armas] Fotos em Imgs/Renders/LojaDeArmas_*.png");
        }

        // uma vista com a luz de onde a câmera está (dentro, com a lanterna)
        static Texture2D Vista(CenaDaLojaDeArmas c, Vector3 pos, Vector3 alvo, float campo, int largura, int altura)
        {
            c.Iluminar(pos);
            var lanterna = CenaDaLojaDeArmas.Dentro(pos) ? CenaDaLojaDeArmas.LanternaDaFoto(pos, alvo) : null;
            var t = Renderizar(pos, alvo, campo, largura, altura, false);
            if (lanterna != null) Object.DestroyImmediate(lanterna.gameObject);
            return t;
        }

        public static void VistasDaLoja(CenaDaLojaDeArmas c, string arquivo, float campo, int largura, int altura, int colunas, params (Vector3 pos, Vector3 alvo)[] vistas)
        {
            var imagens = new List<Texture2D>();
            foreach (var (pos, alvo) in vistas) imagens.Add(Vista(c, pos, alvo, campo, largura, altura));
            GradeRetangular(arquivo, imagens, colunas);
        }

        // a planta da loja de cima (logo abaixo do forro: o forro, a cobertura e o alto das paredes ficam fora), com os
        // rótulos dos cômodos, as paredes desenhadas por cima e o título
        static Texture2D PlantaDaLoja(int largura)
        {
            float x0 = X0 - 4.5f, x1 = X1 + 4.5f, z0 = Z0 - Calcada - 3f, z1 = Z1 + 5.5f;
            int altura = Mathf.RoundToInt(largura * (z1 - z0) / (x1 - x0));
            var giro = Quaternion.Euler(90f, 0f, 0f);
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var temporarios = new List<GameObject>();
            void Rotulo(string texto, Vector3 pos, float letra)
            {
                temporarios.Add(TextoNoChao(texto, pos + new Vector3(1f, 0f, -1f) * letra * 0.07f - Vector3.up * 0.01f, giro, letra, fonte, Color.black));
                temporarios.Add(TextoNoChao(texto, pos, giro, letra, fonte, Color.white));
            }
            var an = PlantaPronta;
            const float letra = 0.42f;
            foreach (var k in an.comodos)
            {
                float w = k.x1 - k.x0;
                int max = Mathf.Max(6, Mathf.FloorToInt((w - 0.4f) / (letra * 0.56f)));
                var c = new Vector3(k.Cx, 2.0f, k.Cz);
                // o rótulo fora dos balcões e das gôndolas
                if (k.tipo == "vendas") c = new Vector3(X(12.4f), 2.0f, Z(4.6f));
                if (k.tipo == "estande") c = new Vector3(k.Cx, 2.0f, Z(22f));
                Rotulo(Quebrar($"{an.nomes[k]}\n{w:0.#} × {(k.z1 - k.z0):0.#} m", max), c, letra);
            }
            Rotulo("Loja de armas · a planta (24 × 30 m)", new Vector3((x0 + x1) / 2f, 2.0f, z0 + 1.2f), 1.0f);
            Paredes(an, 1.9f, temporarios);
            SemNeblina();
            var go = new GameObject("Câmera da planta") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            Ajustar(cam, 60f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            cam.orthographic = true;
            cam.orthographicSize = (z1 - z0) / 2f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 30f;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.18f);
            go.transform.SetPositionAndRotation(new Vector3((x0 + x1) / 2f, 2.4f, (z0 + z1) / 2f), giro);
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = largura / (float)altura;
            cam.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            foreach (var r in temporarios) Object.DestroyImmediate(r);
            return tex;
        }
    }
}
