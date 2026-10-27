using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As cenas de exemplo das construções feitas pelo jogo (Plano_Mapa), fora das Build Settings, com a luz de um dia
    /// nublado, o pós-processamento do dia e um jogador simples para andar (AndarNoExemplo):
    ///  - `Scenes/Exemplos/Exemplo_Casas.unity`: a rua de exemplo (RuaDeExemplo). Menu Zombie City Cleaner > Exemplos >
    ///    Casas geradas pelo jogo;
    ///  - `Scenes/Exemplos/Sala_de_Medidas.unity`: a sala de medidas da Parte M0 (SalaDeMedidas), a mesma casa em 3
    ///    escalas. Menu Zombie City Cleaner > Exemplos > Sala de medidas (Parte M0);
    ///  - `Scenes/Exemplos/Abandono.unity`: a cena de testes do abandono da Parte M9b (CenaDoAbandono), os mesmos trechos
    ///    de rua em 4 graus de abandono. Menu Zombie City Cleaner > Exemplos > Abandono (Parte M9b); as fotos, em
    ///    Zombie City Cleaner > Exemplos > Fotos do abandono.
    /// Os menus refazem a cena. Foto() tira fotos da cena aberta sem dar Play.
    /// </summary>
    public static class MontagemDoExemploDasCasas
    {
        const string PastaCena = "Assets/_Game/Scenes/Exemplos";
        const string Cena = PastaCena + "/Exemplo_Casas.unity";
        const string CenaDaSala = PastaCena + "/Sala_de_Medidas.unity";
        const string CenaAbandono = PastaCena + "/Abandono.unity";
        const string FbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
        const string FbxArvores = "Assets/_Game/Art/Cidade/Cidade_Arvores.fbx";
        static readonly Color CorDaNeblina = new Color(0.60f, 0.62f, 0.62f);

        static Mesh Malha(string fbx, string nome) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name == nome);

        [MenuItem("Zombie City Cleaner/Exemplos/Casas geradas pelo jogo")]
        public static void Montar()
        {
            var cena = CenaNova();
            if (cena == null) return;
            // o objeto nasce desligado: a rua só gera quando já tem o material e os modelos
            var goRua = new GameObject("Rua de exemplo");
            goRua.SetActive(false);
            var rua = goRua.AddComponent<RuaDeExemplo>();
            Comum(rua);
            rua.casaDoBlender = Malha("Assets/_Game/Art/Cidade/Cidade_Casas.fbx", "Casa_1");
            rua.casaDoBlenderColisao = Malha("Assets/_Game/Art/Cidade/Cidade_Casas_Colisao.fbx", "Casa_1_Colisao");
            goRua.SetActive(true);
            Jogador(new Vector3(-36f, 0.1f, -2.5f));
            EditorSceneManager.SaveScene(cena.Value, Cena);
            Debug.Log($"Cena de exemplo montada: {Cena}\n{rua.Resumo}");
        }

        [MenuItem("Zombie City Cleaner/Exemplos/Sala de medidas (Parte M0)")]
        public static void MontarSala()
        {
            var cena = CenaNova();
            if (cena == null) return;
            var goSala = new GameObject("Sala de medidas");
            goSala.SetActive(false);
            var sala = goSala.AddComponent<SalaDeMedidas>();
            Comum(sala);
            sala.zumbi = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Zumbis/Zumbi_ZombieGaroto.prefab");
            sala.jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            goSala.SetActive(true);
            Jogador(new Vector3(-20f, 0.1f, -2.5f));
            EditorSceneManager.SaveScene(cena.Value, CenaDaSala);
            Debug.Log($"Sala de medidas montada: {CenaDaSala}\n{sala.Resumo}");
        }

        [MenuItem("Zombie City Cleaner/Exemplos/Abandono (Parte M9b)")]
        public static void MontarAbandono()
        {
            var cena = CenaNova();
            if (cena == null) return;
            var abandono = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoAbandono);
            if (abandono == null) abandono = TexturasDoAbandono.Gerar();
            var go = new GameObject("Cena do abandono");
            go.SetActive(false);
            var c = go.AddComponent<CenaDoAbandono>();
            Comum(c);
            c.materialDeHoje = c.material;
            c.material = abandono;
            c.vegetacao = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            c.pichacao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasPichacoes);
            if (c.pichacao == null) c.pichacao = TexturasDoAbandono.Pichacoes();
            c.cacamba = Malha(FbxRua, "Cacamba");
            c.sacos = Malha(FbxRua, "Sacos");
            c.lixeira = Malha(FbxRua, "Lixeira");
            Mesh A(string n) => Malha(FbxArvores, n);
            c.gramaA = A("Grama_A"); c.gramaB = A("Grama_B"); c.capimA = A("Capim_A"); c.capimB = A("Capim_B");
            c.arbustoA = A("Arbusto_A"); c.arbustoB = A("Arbusto_B"); c.arbustoC = A("Arbusto_C");
            c.abetoA = A("Abeto_A"); c.abetoB = A("Abeto_B"); c.abetoC = A("Abeto_C"); c.pinheiroBranco = A("PinheiroBranco_B");
            c.bordoVerde = A("Bordo_Verde"); c.bordoVermelho = A("Bordo_Vermelho"); c.betula = A("Betula_A"); c.morta = A("Morta_A");
            go.SetActive(true);
            Jogador(new Vector3(CenaDoAbandono.InicioDoTrecho(1) - 4f, 0.1f, -2.5f));
            EditorSceneManager.SaveScene(cena.Value, CenaAbandono);
            Debug.Log($"Cena do abandono montada: {CenaAbandono}\n{c.Resumo}");
        }

        /// <summary>
        /// As fotos da cena do abandono (aberta): cada vista nos 4 trechos, numa grade 2 × 2 (hoje, pouco / médio, muito),
        /// em Imgs/Renders/Abandono_*.png da pasta de arte.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos do abandono")]
        public static void FotosDoAbandono()
        {
            var c = Object.FindAnyObjectByType<CenaDoAbandono>();
            if (c == null) { Debug.LogWarning("Abra a cena do abandono antes."); return; }
            const string pasta = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Imgs\Renders\";
            // com a neblina do jogo, as fotos saem com "_Neblina" no nome
            string sufixo = c.neblinaDoJogo ? "_Neblina" : "";
            // as construções de cada trecho, na ordem: as 3 casas, a loja e o prédio
            Vector3 Meio(int t, int i) => c.Construcoes[t * 5 + i].GetComponent<Renderer>().bounds.center;
            // a frente da casa 2 (as do lado norte olham para -z), de perto
            Vector3 Frente(int t) { var b = c.Construcoes[t * 5 + 1].GetComponent<Renderer>().bounds; return new Vector3(b.center.x, 1.5f, b.min.z); }
            var vistas = new (string nome, System.Func<int, (Vector3 pos, Vector3 alvo)> vista)[]
            {
                ("Rua", t => (new Vector3(CenaDoAbandono.InicioDoTrecho(t) - 3f, 1.7f, -1.5f), new Vector3(CenaDoAbandono.InicioDoTrecho(t) + 30f, 3f, 5f))),
                ("Casa", t => (Meio(t, 0) + new Vector3(-4f, 0f, -16f) + Vector3.up * (1.7f - Meio(t, 0).y), Meio(t, 0) + Vector3.up * 0.5f)),
                ("Loja", t => (Meio(t, 3) + new Vector3(-6f, 0f, -14f) + Vector3.up * (1.7f - Meio(t, 3).y), Meio(t, 3) - Vector3.up * 1f)),
                ("Predio", t => (Meio(t, 4) + new Vector3(6f, 0f, 16f) + Vector3.up * (1.7f - Meio(t, 4).y), Meio(t, 4) + Vector3.up * (5f - Meio(t, 4).y))),
                ("Parede", t => (Frente(t) + new Vector3(-1.5f, 0.2f, -6.5f), Frente(t) + new Vector3(0f, 0.3f, 0f))),
                ("Calcada", t => (new Vector3(CenaDoAbandono.InicioDoTrecho(t) + 20f, 1.6f, -5.3f), new Vector3(CenaDoAbandono.InicioDoTrecho(t) + 26f, 0f, -4.2f))),
            };
            foreach (var (nome, vista) in vistas)
                FotosEmGrade(pasta + $"Abandono_{nome}{sufixo}.png", 60f, 960, 540, 2, vista(0), vista(1), vista(2), vista(3));
            // as pichações: as 4 paredes do muito abandono, viradas para a rua, com mais delas, de perto
            var paredes = new System.Collections.Generic.Dictionary<(Transform, int, int, int), (Vector3 soma, Vector3 fora, int n)>();
            foreach (var mf in c.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name != "Pichações" || mf.GetComponent<Renderer>().bounds.center.x < CenaDoAbandono.InicioDoTrecho(3) - CenaDoAbandono.Entre) continue;
                var v = mf.sharedMesh.vertices;
                var nn = mf.sharedMesh.normals;
                for (int i = 0; i < v.Length; i += 4)
                {
                    var centro = mf.transform.TransformPoint((v[i] + v[i + 2]) / 2f);
                    var fora = mf.transform.TransformDirection(nn[i]).normalized;
                    // só as viradas para a rua (z = 0), que a câmera vê da calçada
                    if (fora.z * Mathf.Sign(centro.z) > -0.7f) continue;
                    var chave = (mf.transform.parent, Mathf.RoundToInt(fora.x * 2f), Mathf.RoundToInt(fora.z * 2f), Mathf.RoundToInt(Vector3.Dot(centro, fora)));
                    paredes.TryGetValue(chave, out var p);
                    paredes[chave] = (p.soma + centro, fora, p.n + 1);
                }
            }
            var maiores = paredes.Values.OrderByDescending(p => p.n).Take(4)
                .Select(p => { var m = p.soma / p.n; m.y = 1.5f; return (m + p.fora * 5.5f + Vector3.up * 0.1f, m); }).ToList();
            if (maiores.Count > 0)
            {
                while (maiores.Count < 4) maiores.Add(maiores[0]);
                FotosEmGrade(pasta + $"Abandono_Pichacoes{sufixo}.png", 60f, 960, 540, 2, maiores.ToArray());
            }
            Debug.Log("[Abandono] Fotos em Imgs/Renders/Abandono_*.png");
        }

        /// <summary>Várias vistas numa grade de 'colunas' colunas (de cima para baixo, da esquerda para a direita), com uma faixa escura entre elas.</summary>
        public static void FotosEmGrade(string arquivo, float campo, int largura, int altura, int colunas, params (Vector3 pos, Vector3 alvo)[] vistas)
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

        /// <summary>O material e os objetos de rua (do Blender) que as duas cenas usam.</summary>
        static void Comum(CenaDeExemplo c)
        {
            c.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Cidade.mat");
            c.poste = Malha(FbxRua, "Poste");
            c.carros = new[] { "Carro_Sedan_Azul", "Carro_Sedan_Vinho", "Carro_Pickup_Bege", "Carro_Sedan_Cinza" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
            c.arvores = new[] { "Pinheiro_A", "Pinheiro_B", "Pinheiro_C", "Arvore", "Arvore_Seca" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
        }

        /// <summary>O jogador simples: a cápsula de 1,80 m, a cabeça a 1,62 m e a câmera de 70°, como o do jogo.</summary>
        static void Jogador(Vector3 pos)
        {
            var jogador = new GameObject("Jogador de exemplo");
            jogador.transform.SetPositionAndRotation(pos, Quaternion.identity);
            var cc = jogador.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.3f;
            var cabeca = new GameObject("Cabeça");
            cabeca.transform.SetParent(jogador.transform, false);
            cabeca.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            var cam = cabeca.AddComponent<Camera>();
            Ajustar(cam, 70f);
            cabeca.tag = "MainCamera";
            cabeca.AddComponent<AudioListener>();
            jogador.AddComponent<AndarNoExemplo>().cabeca = cabeca.transform;
        }

        /// <summary>Uma cena vazia com a luz de um dia nublado, a neblina leve e o volume do dia (null se não deu para trocar de cena).</summary>
        static UnityEngine.SceneManagement.Scene? CenaNova()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Saia do Play antes de montar a cena de exemplo."); return null; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            Directory.CreateDirectory(PastaCena);
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // um dia nublado, com neblina leve (para ver as casas de longe)
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.color = new Color(0.88f, 0.87f, 0.83f);
            sol.intensity = 1.25f;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(44f, -38f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.58f, 0.60f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.45f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = CorDaNeblina;

            var volume = new GameObject("Pós-processamento (dia)").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Game/Settings/Demo_Volume_Dia.asset");
            return cena;
        }

        static void Ajustar(Camera cam, float campo)
        {
            cam.fieldOfView = campo;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CorDaNeblina;
            var dados = cam.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = true;
            dados.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        /// <summary>
        /// Uma foto da cena aberta, sem dar Play: da posição, olhando o alvo, gravada em PNG. Com 'lanterna', um facho
        /// sai da câmera (para os lugares escuros, como o porão).
        /// </summary>
        public static void Foto(string arquivo, Vector3 pos, Vector3 alvo, float campo = 60f, int largura = 1600, int altura = 900, bool lanterna = false)
        {
            var tex = Renderizar(pos, alvo, campo, largura, altura, lanterna);
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Várias vistas lado a lado numa imagem só, com uma faixa escura entre elas (a sala de medidas: a mesma vista
        /// nas 3 escalas). Cada vista é (posição, alvo).
        /// </summary>
        public static void FotosLadoALado(string arquivo, float campo, int largura, int altura, bool lanterna, params (Vector3 pos, Vector3 alvo)[] vistas)
        {
            const int faixa = 6;
            int total = vistas.Length * largura + (vistas.Length - 1) * faixa;
            var junta = new Texture2D(total, altura, TextureFormat.RGB24, false);
            var fundo = new Color32[total * altura];
            for (int i = 0; i < fundo.Length; i++) fundo[i] = new Color32(20, 20, 22, 255);
            junta.SetPixels32(fundo);
            for (int i = 0; i < vistas.Length; i++)
            {
                var t = Renderizar(vistas[i].pos, vistas[i].alvo, campo, largura, altura, lanterna);
                junta.SetPixels(i * (largura + faixa), 0, largura, altura, t.GetPixels());
                Object.DestroyImmediate(t);
            }
            junta.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, junta.EncodeToPNG());
            Object.DestroyImmediate(junta);
        }

        /// <summary>A imagem de uma câmera do jogo (sem Play), da posição olhando o alvo. Quem chama destrói a textura.</summary>
        public static Texture2D Renderizar(Vector3 pos, Vector3 alvo, float campo, int largura, int altura, bool lanterna)
        {
            var go = new GameObject("Câmera da foto") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            Ajustar(cam, campo);
            go.transform.position = pos;
            go.transform.LookAt(alvo);
            if (lanterna)
            {
                var luz = go.AddComponent<Light>();
                luz.type = LightType.Spot;
                luz.range = 28f; luz.spotAngle = 75f; luz.innerSpotAngle = 50f; luz.intensity = 30f;
                luz.color = new Color(1f, 0.95f, 0.85f);
                luz.shadows = LightShadows.Soft;
            }
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
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return tex;
        }
    }
}
