using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Music;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena de teste do Hospital Principal (o Plano_Hospital.md da pasta de arte: a parte 2, o blockout, e a parte 3, a
    /// arquitetura final): `Scenes/Exemplos/Hospital.unity` (CenaDoHospital), no menu Zombie City Cleaner > Exemplos >
    /// Hospital principal, que refaz a cena; as fotos, em Zombie City Cleaner > Exemplos > Fotos do hospital, com a cena
    /// aberta: Imgs/Renders/Hospital_Arquitetura_*.png da pasta de arte (as vistas de fora e de dentro, os dois cortes e as
    /// plantas dos 4 andares; as do blockout, Hospital_Blockout_*, ficaram como estavam).
    /// </summary>
    public static partial class MontagemDoExemploDasCasas
    {
        const string CenaHospital = PastaCena + "/Hospital.unity";

        [MenuItem("Zombie City Cleaner/Exemplos/Hospital principal")]
        public static void MontarHospital()
        {
            var cena = CenaNova();
            if (cena == null) return;
            var go = new GameObject("Hospital de teste");
            go.SetActive(false);
            var c = go.AddComponent<CenaDoHospital>();
            Comum(c);
            // o material do abandono (o tijolo aparente, as manchas, a umidade, o mofo) e a vegetação (a hera e os arbustos)
            var abandono = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoAbandono);
            if (abandono == null) abandono = TexturasDoAbandono.Gerar();
            c.material = abandono;
            c.vegetacao = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            c.arbustoA = Malha(FbxArvores, "Arbusto_A");
            c.arbustoB = Malha(FbxArvores, "Arbusto_B");
            c.arbustoC = Malha(FbxArvores, "Arbusto_C");
            c.lixeira = Malha(FbxRua, "Lixeira");
            // a mobília (a parte 4: os prefabs da mobília nova, com o lote do hospital)
            c.mobilia = MontagemDaMobiliaNova.Carregar();
            // as marcas do abandono (a parte 5)
            c.materialDasMarcas = TexturasDoAbandono.Marcas();
            go.SetActive(true);
            Jogador(c.Paradas.Count > 0 ? c.Paradas[0].Chegada : new Vector3(0f, ChaoDeFora + 0.1f, -36f));
            // os sons (a parte 6)
            SonsDoHospitalNaCena();
            // os documentos (a parte 6, a narrativa)
            PapeisDoHospitalNaCena();
            EditorSceneManager.SaveScene(cena.Value, CenaHospital);
            Debug.Log($"Cena do hospital montada: {CenaHospital}\n{c.Resumo}");
        }

        const string PastaDosSons = "Assets/_Game/Audio/Hospital";

        // os tipos de som do hospital (na ordem do SonsDoHospital.Tipos): os pesos por andar (o subsolo, o térreo, o 2º e o
        // 3º), o volume, a distância de quem escuta, o alcance e, na voz atrás da porta, o trecho e o corte dos agudos
        static readonly (string nome, float[] pesos, Vector2 volume, Vector2 distancia, float alcance, Vector2 trecho, float corte, Vector2 tom)[] TiposDeSom =
        {
            ("Porta", new[] { 0.5f, 3f, 2f, 1f }, new Vector2(0.45f, 0.65f), new Vector2(8f, 30f), 40f, default, 0f, new Vector2(0.9f, 1.08f)),
            ("Vento", new[] { 0f, 3f, 1f, 2.5f }, new Vector2(0.4f, 0.6f), new Vector2(5f, 26f), 32f, default, 0f, new Vector2(0.92f, 1.06f)),
            ("Goteira", new[] { 3f, 2f, 1f, 1.5f }, new Vector2(0.4f, 0.6f), new Vector2(4f, 18f), 24f, default, 0f, new Vector2(0.9f, 1.12f)),
            ("Metal", new[] { 2f, 0.5f, 0.5f, 1f }, new Vector2(0.4f, 0.6f), new Vector2(8f, 30f), 40f, default, 0f, new Vector2(0.92f, 1.08f)),
            ("Cano", new[] { 3f, 0f, 0f, 0f }, new Vector2(0.45f, 0.65f), new Vector2(6f, 28f), 38f, default, 0f, new Vector2(0.9f, 1.06f)),
            ("Estrutura", new[] { 0.5f, 0.5f, 1f, 3f }, new Vector2(0.4f, 0.6f), new Vector2(6f, 30f), 38f, default, 0f, new Vector2(0.9f, 1.06f)),
            ("Papeis", new[] { 0f, 1f, 0.5f, 3f }, new Vector2(0.4f, 0.6f), new Vector2(4f, 18f), 22f, default, 0f, new Vector2(0.92f, 1.08f)),
            ("Pancada", new[] { 1f, 1f, 1.5f, 0.5f }, new Vector2(0.5f, 0.7f), new Vector2(15f, 45f), 60f, default, 0f, new Vector2(0.85f, 1.05f)),
            ("Maca", new[] { 0f, 0.5f, 2.5f, 0f }, new Vector2(0.4f, 0.6f), new Vector2(6f, 25f), 34f, default, 0f, new Vector2(0.92f, 1.06f)),
            ("Zumbi", new[] { 1f, 0.5f, 1.5f, 0f }, new Vector2(0.35f, 0.5f), new Vector2(6f, 25f), 30f, new Vector2(1.5f, 3f), 650f, new Vector2(0.78f, 0.95f)),
        };

        /// <summary>
        /// Os sons do hospital na cena (a parte 6 do plano): o objeto "Sons do hospital" com o AmbienteSonoro (os tipos, com os
        /// sons do Audio/Hospital, gerados pelo Ferramentas/hospital_sons.py, e a voz do zumbi, e os pontos da planta, do
        /// SonsDoHospital). Sem música (a pedido: "vamos deixar sem a música, somente os sons"). A importação dos sons também.
        /// </summary>
        static void SonsDoHospitalNaCena()
        {
            var go = new GameObject("Sons do hospital");
            var amb = go.AddComponent<AmbienteSonoro>();
            amb.tipos = TiposDeSomDoHospital();
            amb.areas = SonsDoHospital.Areas();
            amb.limites = SonsDoHospital.Limites();
            amb.pontos = SonsDoHospital.Pontos().Select(p => new AmbienteSonoro.Ponto { lugar = p.lugar, tipo = p.tipo, area = p.andar }).ToList();
            Debug.Log($"[Hospital] Sons: {amb.pontos.Count} pontos, {amb.tipos.Sum(t => t.clipes.Length)} sons");
        }

        /// <summary>
        /// Os tipos de som do hospital (os do Audio/Hospital, gerados pelo Ferramentas/hospital_sons.py, e a voz do zumbi),
        /// com os pesos por andar de <see cref="TiposDeSom"/>, já importados: a cena de teste e o hospital do mundo (o
        /// HospitalDoMundo, na cena Mundo) usam os mesmos.
        /// </summary>
        public static AmbienteSonoro.Tipo[] TiposDeSomDoHospital()
        {
            ImportarOsSons();
            var tipos = new List<AmbienteSonoro.Tipo>();
            foreach (var (nome, pesos, volume, distancia, alcance, trecho, corte, tom) in TiposDeSom)
            {
                var clipes = new List<AudioClip>();
                if (nome == "Zumbi") clipes.Add(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SomZombie1.mp3"));
                else
                    for (int i = 1; i <= 9; i++)
                    {
                        var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{PastaDosSons}/{nome}_{i:00}.wav");
                        if (c != null) clipes.Add(c);
                    }
                clipes.RemoveAll(c => c == null);
                if (clipes.Count == 0) Debug.LogWarning($"[Hospital] Faltam os sons de {nome}: rode o Ferramentas/hospital_sons.py.");
                tipos.Add(new AmbienteSonoro.Tipo
                {
                    nome = nome, clipes = clipes.ToArray(), pesos = pesos, volume = volume, distancia = distancia, alcance = alcance,
                    trecho = trecho, corte = corte, tom = tom,
                });
            }
            return tipos.ToArray();
        }

        /// <summary>
        /// Os documentos do hospital na cena (a parte 6 do plano, a narrativa): o objeto "Papéis do hospital" com o
        /// PapeisDoLugar (os 20 papéis do DocumentosDoHospital, nas coordenadas da obra, que aqui são as da cena), o atlas e o
        /// material dos papéis (o MontagemDosPapeis) e o texto do Resources/Documentos/Hospital.txt.
        /// </summary>
        static void PapeisDoHospitalNaCena()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MontagemDosPapeis.Pasta + "/M_Papeis.mat") == null) MontagemDosPapeis.Montar();
            var go = new GameObject("Papéis do hospital");
            var papeis = go.AddComponent<Documentos.PapeisDoLugar>();
            papeis.arquivo = "Documentos/Hospital";
            papeis.material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDosPapeis.Pasta + "/M_Papeis.mat");
            papeis.papeis = DocumentosDoHospital.Papeis().Select(p => new Documentos.PapeisDoLugar.Papel
            {
                id = p.id, forma = p.forma, lugar = p.lugar, giro = p.giro, naParede = p.naParede,
                temCaixa = p.temCaixa, caixaMeio = p.caixaMeio, caixaTamanho = p.caixaTamanho, caixaGiro = p.caixaGiro,
            }).ToList();
            foreach (var f in DocumentosDoHospital.Faltas) Debug.LogWarning("[Hospital] Papel: " + f);
            Debug.Log($"[Hospital] Papéis: {papeis.papeis.Count}");
        }

        // os sons: mono, carregados inteiros (Vorbis)
        static void ImportarOsSons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { PastaDosSons }))
            {
                var caminho = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(caminho) is AudioImporter imp)) continue;
                var s = imp.defaultSampleSettings;
                var querido = s;
                querido.compressionFormat = AudioCompressionFormat.Vorbis;
                querido.quality = 0.7f;
                querido.loadType = AudioClipLoadType.CompressedInMemory;
                querido.preloadAudioData = true;
                if (!imp.forceToMono || s.loadType != querido.loadType || s.compressionFormat != querido.compressionFormat || Mathf.Abs(s.quality - querido.quality) > 0.01f)
                {
                    imp.forceToMono = true;
                    imp.defaultSampleSettings = querido;
                    imp.SaveAndReimport();
                }
            }
        }

        [MenuItem("Zombie City Cleaner/Exemplos/Fotos do hospital")]
        public static void FotosDoHospital()
        {
            var c = Object.FindAnyObjectByType<CenaDoHospital>();
            if (c == null) { Debug.LogWarning("Abra a cena do hospital antes."); return; }
            var sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            var sombras = sol != null ? sol.shadows : LightShadows.None;
            bool neblina = RenderSettings.fog;
            try
            {
                const string prefixo = "Hospital_Arquitetura_";
                VistasDoHospital(c, PastaDasFotos + prefixo + "Fora.png", 55f, 960, 640, 3,
                    (new Vector3(-2f, 1.8f, -50f), new Vector3(-2f, 6.5f, -16f)),      // a frente, do estacionamento
                    (new Vector3(38f, 2.2f, -46f), new Vector3(0f, 6f, -10f)),         // de três quartos, do sudeste
                    (new Vector3(-62f, 1.8f, -12f), new Vector3(-44f, 2.6f, 2f)),      // a ala da emergência e a rampa
                    (new Vector3(-62f, 42f, -58f), new Vector3(-8f, 0f, -4f)),         // de cima, do sudoeste
                    (new Vector3(-10f, 1.7f, -31f), new Vector3(2f, 5.5f, -20f)),      // o pórtico e a torre
                    (new Vector3(-15.5f, 1.5f, -19.5f), new Vector3(-12f, 3.0f, -16.1f))); // o tijolo, as faixas e a hera
                VistasDoHospital(c, PastaDasFotos + prefixo + "Dentro.png", 68f, 960, 540, 3,
                    (new Vector3(0f, 1.7f, -18.5f), new Vector3(0f, 1.4f, 1.5f)),      // T: a entrada, para a recepção e o elevador
                    (new Vector3(-1f, 1.7f, -6.5f), new Vector3(-20f, 1.3f, -6.5f)),   // T: o corredor da frente, para a emergência
                    (new Vector3(-26.8f, 1.7f, -4.5f), new Vector3(-14f, 1.3f, 2.5f)), // T: a emergência, do recebimento
                    (new Vector3(-5.6f, 1.7f, -0.4f), new Vector3(-1.4f, 1.6f, 2.6f)), // T: o elevador e o diretório
                    (new Vector3(0.5f, 5.65f, -0.9f), new Vector3(0f, 5.4f, 3f)),      // 2º: a porta do elevador forçada
                    (new Vector3(-19.5f, 5.7f, -9f), new Vector3(7f, 5.4f, -9f)),      // 2º: o corredor do isolamento
                    (new Vector3(-15f, 5.7f, 0.5f), new Vector3(-28f, 5.3f, -1.5f)),   // 2º: a enfermaria A, as janelas
                    (new Vector3(-4.5f, 9.7f, -9f), new Vector3(3f, 9.2f, -15.5f)),    // 3º: a sala do diretor
                    (new Vector3(-12f, 9.7f, 3f), new Vector3(-28f, 9.2f, -2f)),       // 3º: o escritório A, as persianas
                    (new Vector3(7f, -2.3f, 4f), new Vector3(17f, -3f, -3f)),          // SS: o necrotério
                    (new Vector3(0f, -2.3f, -6.8f), new Vector3(0f, -2.6f, -13.5f)),   // SS: o acesso à A-01
                    (new Vector3(-25.6f, 1.6f, 9.85f), new Vector3(-25.3f, 1.5f, 8.0f))); // T: o patamar da escada oeste, o número do andar
                VistasDoHospital(c, PastaDasFotos + prefixo + "Detalhes.png", 60f, 960, 540, 3,
                    (new Vector3(20.5f, 1.7f, -8.6f), new Vector3(20.5f, 0.9f, -15.5f)), // T: o banheiro feminino, o azulejo
                    (new Vector3(-14f, 1.6f, -6.6f), new Vector3(-17.5f, 1.3f, -4.9f)),  // T: a porta de vai-e-vem da emergência
                    (new Vector3(-4f, 1.6f, -14f), new Vector3(0f, 1.4f, -16.5f)),       // T: as portas de correr da entrada
                    (new Vector3(5f, 5.7f, 3.2f), new Vector3(16f, 5.0f, 7.9f)),         // 2º: o corredor do centro cirúrgico
                    (new Vector3(-6f, 9.6f, -6.5f), new Vector3(-12f, 9.6f, -8f)),       // 3º: as placas das portas
                    (new Vector3(-21f, 5.6f, -7.2f), new Vector3(-6f, 4.6f, -8.2f)));     // 2º: a barreira do isolamento

                // os cortes (sem a sombra do sol, que deixaria os cômodos no escuro: o hospital não tem energia, e sem a
                // neblina, a de altura e a da cena, que lavavam o subsolo): de lado, pelo meio do lance de ida da escada
                // oeste, e de frente, pelo meio (o elevador)
                c.LuzDeFora();
                if (sol != null) sol.shadows = LightShadows.None;
                SemNeblina();
                Corte(PastaDasFotos + prefixo + "Corte_Escada.png", new Vector3(-120f, 3f, -4f), Vector3.right, 120f - 27.0f, 15f, 2000, 1100);
                Corte(PastaDasFotos + prefixo + "Corte_Meio.png", new Vector3(-7.5f, 3f, -120f), Vector3.forward, 120f + 1.6f, 21f, 2000, 1100);

                // as plantas dos 4 andares (sem a sombra do sol, que deixaria os cômodos no escuro)
                if (sol != null) sol.shadows = LightShadows.None;
                var imagens = new List<Texture2D>();
                for (int f = Andares - 1; f >= 0; f--) imagens.Add(PlantaDoAndar(f, 1700));
                GradeRetangular(PastaDasFotos + prefixo + "Plantas.png", imagens, 2);
            }
            finally
            {
                if (sol != null) sol.shadows = sombras;
                RenderSettings.fog = neblina;
                c.LuzDeFora();
            }
            Debug.Log("[Hospital] Fotos em Imgs/Renders/Hospital_Arquitetura_*.png");
        }

        /// <summary>As fotos da mobília (a parte 4): 12 vistas de dentro dos cômodos principais e as plantas com os móveis.</summary>
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos da mobília do hospital")]
        public static void FotosDaMobiliaDoHospital()
        {
            var c = Object.FindAnyObjectByType<CenaDoHospital>();
            if (c == null) { Debug.LogWarning("Abra a cena do hospital antes."); return; }
            var sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            var sombras = sol != null ? sol.shadows : LightShadows.None;
            bool neblina = RenderSettings.fog;
            try
            {
                const string prefixo = "Hospital_Mobilia_";
                VistasDoHospital(c, PastaDasFotos + prefixo + "Dentro.png", 70f, 960, 540, 3,
                    (new Vector3(-13f, 1.7f, -4.2f), new Vector3(-22f, 0.9f, 4f)),     // T: a emergência
                    (new Vector3(8.5f, 1.7f, -9f), new Vector3(-6f, 0.9f, -14f)),      // T: a sala de espera
                    (new Vector3(14.2f, 1.7f, -4.2f), new Vector3(26f, 0.9f, 4f)),     // T: o laboratório
                    (new Vector3(-15.6f, 1.7f, 8.6f), new Vector3(-23f, 0.9f, 15f)),   // T: a sala vermelha
                    (new Vector3(6.8f, -2.3f, -4.2f), new Vector3(17f, -3.2f, 4f)),    // SS: o necrotério
                    (new Vector3(-6.8f, -2.3f, -4.2f), new Vector3(-17f, -3.2f, 4f)),  // SS: a autópsia
                    (new Vector3(-6f, 5.7f, 8.7f), new Vector3(-6.5f, 4.8f, 15.5f)),  // 2º: a UTI
                    (new Vector3(7.8f, 5.7f, 8.6f), new Vector3(17f, 4.8f, 14.5f)),    // 2º: a sala de cirurgia 1
                    (new Vector3(-12.8f, 5.7f, -4.2f), new Vector3(-27f, 4.9f, 4f)),   // 2º: a enfermaria A
                    (new Vector3(-8.6f, 9.8f, 0.3f), new Vector3(-26f, 8.8f, -0.8f)),  // 3º: o escritório A, pelo corredor das baias
                    (new Vector3(5.2f, 9.7f, -8.6f), new Vector3(-4f, 8.9f, -15f)),    // 3º: a sala do diretor
                    (new Vector3(4.8f, 9.7f, 8.6f), new Vector3(22f, 8.9f, 15f)));     // 3º: o arquivo
                // as plantas dos 4 andares com os móveis (sem a sombra do sol)
                if (sol != null) sol.shadows = LightShadows.None;
                var imagens = new List<Texture2D>();
                for (int f = Andares - 1; f >= 0; f--) imagens.Add(PlantaDoAndar(f, 1700));
                GradeRetangular(PastaDasFotos + prefixo + "Plantas.png", imagens, 2);
            }
            finally
            {
                if (sol != null) sol.shadows = sombras;
                RenderSettings.fog = neblina;
                c.LuzDeFora();
            }
            Debug.Log("[Hospital] Fotos em Imgs/Renders/Hospital_Mobilia_*.png");
        }

        // várias vistas numa grade, cada uma com a luz de onde a câmera está
        public static void VistasDoHospital(CenaDoHospital c, string arquivo, float campo, int largura, int altura, int colunas, params (Vector3 pos, Vector3 alvo)[] vistas)
        {
            var imagens = new List<Texture2D>();
            foreach (var (pos, alvo) in vistas)
            {
                c.Iluminar(pos);
                // dentro, a lanterna (o hospital não tem energia)
                var lanterna = CenaDoHospital.Dentro(pos) ? CenaDoHospital.LanternaDaFoto(pos, alvo) : null;
                imagens.Add(Renderizar(pos, alvo, campo, largura, altura, false));
                if (lanterna != null) Object.DestroyImmediate(lanterna.gameObject);
            }
            GradeRetangular(arquivo, imagens, colunas);
        }

        // as imagens (do mesmo tamanho) numa grade de 'colunas' colunas, com uma faixa escura entre elas; destrói as imagens
        static void GradeRetangular(string arquivo, List<Texture2D> imagens, int colunas)
        {
            if (imagens.Count == 0) return;
            const int faixa = 6;
            int w = imagens[0].width, h = imagens[0].height;
            colunas = Mathf.Min(colunas, imagens.Count);
            int linhas = (imagens.Count + colunas - 1) / colunas;
            int W = colunas * w + (colunas - 1) * faixa, H = linhas * h + (linhas - 1) * faixa;
            var junta = new Texture2D(W, H, TextureFormat.RGB24, false);
            var fundo = new Color32[W * H];
            for (int i = 0; i < fundo.Length; i++) fundo[i] = new Color32(20, 20, 22, 255);
            junta.SetPixels32(fundo);
            for (int i = 0; i < imagens.Count; i++)
            {
                int col = i % colunas, lin = i / colunas;
                junta.SetPixels(col * (w + faixa), H - (lin + 1) * h - lin * faixa, w, h, imagens[i].GetPixels());
                Object.DestroyImmediate(imagens[i]);
            }
            junta.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo));
            File.WriteAllBytes(arquivo, junta.EncodeToPNG());
            Object.DestroyImmediate(junta);
        }

        static Material _tracoEscuro, _tracoVermelho, _tracoCinza;

        // sem a neblina de altura (o efeito de tela do jogo, mais grossa embaixo: lavava o subsolo) e sem a da cena
        static void SemNeblina()
        {
            Shader.SetGlobalVector("_NeblinaParams", new Vector4(0f, 0.2f, 0.32f, 140f));
            RenderSettings.fog = false;
        }

        // as paredes do andar desenhadas por cima da planta (de cima, as paredes de verdade quase não aparecem): os traços
        // escuros, com os vãos das portas e das passagens; as portas fechadas e as bloqueadas, em vermelho; as janelas de
        // dentro, em cinza. Os objetos vão para 'temporarios' (quem chama apaga)
        static void Paredes(Andar an, float y, List<GameObject> temporarios)
        {
            var M = Medidas.DoJogo;
            if (_tracoEscuro == null)
            {
                _tracoEscuro = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.04f, 0.04f, 0.05f), hideFlags = HideFlags.HideAndDontSave };
                _tracoVermelho = new Material(_tracoEscuro) { color = new Color(0.85f, 0.12f, 0.1f), hideFlags = HideFlags.HideAndDontSave };
                _tracoCinza = new Material(_tracoEscuro) { color = new Color(0.55f, 0.62f, 0.7f), hideFlags = HideFlags.HideAndDontSave };
            }
            Material escura = _tracoEscuro, vermelha = _tracoVermelho, cinza = _tracoCinza;
            void Traco(Vector2 a, Vector2 b, float grossura, Material m, float sobe = 0f)
            {
                Vector2 d = b - a;
                if (d.magnitude < 0.02f) return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
                var meio = (a + b) / 2f;
                go.transform.SetPositionAndRotation(new Vector3(meio.x, y + sobe, meio.y), Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up));
                go.transform.localScale = new Vector3(grossura, 0.02f, d.magnitude);
                temporarios.Add(go);
            }
            float Largura(Porta p) => p.largura > 0f ? p.largura : p.folhas == 2 ? 2f : p.folhas == 0 ? M.passagem : M.porta;
            // um trecho de parede de lo a hi (na linha c, em x ou em z), com os vãos
            void Trecho(bool vertical, float c, float lo, float hi, List<(float de, float ate, bool fechada)> vaos)
            {
                Vector2 P(float s) => vertical ? new Vector2(c, s) : new Vector2(s, c);
                vaos.Sort((p, q) => p.de.CompareTo(q.de));
                float s0 = lo;
                foreach (var (de, ate, fechada) in vaos)
                {
                    Traco(P(s0), P(Mathf.Max(s0, de)), 0.26f, escura);
                    if (fechada) Traco(P(de), P(ate), 0.12f, vermelha, 0.01f);
                    s0 = Mathf.Max(s0, ate);
                }
                Traco(P(s0), P(hi), 0.26f, escura);
            }
            var divisas = new List<Divisa>();
            var bordas = new List<Borda>();
            Linhas(an, divisas, bordas);
            foreach (var d in divisas)
            {
                var vaos = new List<(float, float, bool)>();
                bool aberto = false;
                foreach (var p in PortasEntre(an, d.a, d.b))
                {
                    if (p.aberto) { aberto = true; break; }
                    float l = Largura(p), s = Mathf.Clamp(p.pos, d.lo + l / 2f + 0.15f, d.hi - l / 2f - 0.15f);
                    vaos.Add((s - l / 2f, s + l / 2f, p.Fechada));
                }
                if (aberto) continue;
                foreach (var j in an.janelas)
                    if ((j.a == d.a && j.b == d.b) || (j.a == d.b && j.b == d.a))
                    {
                        vaos.Add((j.pos - j.largura / 2f, j.pos + j.largura / 2f, false));
                        Traco(d.vertical ? new Vector2(d.c, j.pos - j.largura / 2f) : new Vector2(j.pos - j.largura / 2f, d.c),
                              d.vertical ? new Vector2(d.c, j.pos + j.largura / 2f) : new Vector2(j.pos + j.largura / 2f, d.c), 0.1f, cinza);
                    }
                Trecho(d.vertical, d.c, d.lo, d.hi, vaos);
            }
            foreach (var b in bordas)
            {
                var vaos = new List<(float, float, bool)>();
                int lado = b.vertical ? (b.fora > 0 ? 1 : 3) : (b.fora < 0 ? 0 : 2);
                foreach (var p in an.portas)
                    if (p.b == null && p.a == b.k && p.lado == lado && p.pos >= b.lo && p.pos <= b.hi)
                        vaos.Add((p.pos - Largura(p) / 2f, p.pos + Largura(p) / 2f, p.Fechada));
                Trecho(b.vertical, b.c, b.lo - 0.13f, b.hi + 0.13f, vaos);
            }
        }

        // quebra o rótulo em linhas de até 'max' letras (sem quebrar as palavras)
        static string Quebrar(string texto, int max)
        {
            var sb = new StringBuilder();
            foreach (var linha in texto.Split('\n'))
            {
                int atual = 0;
                foreach (var palavra in linha.Split(' '))
                {
                    if (atual > 0 && atual + 1 + palavra.Length > max) { sb.Append('\n'); atual = 0; }
                    else if (atual > 0) { sb.Append(' '); atual++; }
                    sb.Append(palavra);
                    atual += palavra.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        // um andar do hospital de cima, logo abaixo do teto (ortográfica, do tamanho do terreno do prédio com os anexos e a
        // marquise), com os rótulos dos cômodos (quebrados na largura de cada um) e o título embaixo
        static Texture2D PlantaDoAndar(int f, int largura)
        {
            float x0 = GaragemDasAmbulancias.xMin - 1.5f, x1 = X1 + 1.5f, z0 = Marquise.yMin - 4.5f, z1 = Z1 + 1.5f;
            int altura = Mathf.RoundToInt(largura * (z1 - z0) / (x1 - x0));
            float y = Base(f);
            var giro = Quaternion.Euler(90f, 0f, 0f);
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rotulos = new List<GameObject>();
            void Rotulo(string texto, Vector3 pos, float letra)
            {
                rotulos.Add(TextoNoChao(texto, pos + new Vector3(1f, 0f, -1f) * letra * 0.07f - Vector3.up * 0.01f, giro, letra, fonte, Color.black));
                rotulos.Add(TextoNoChao(texto, pos, giro, letra, fonte, Color.white));
            }
            var an = CenaDoHospital.AndarDaPlanta(f);
            const float letra = 0.62f;
            foreach (var k in an.comodos)
            {
                if (an.semRotulo.Contains(k) || k.tipo == "elevador") continue;
                float w = k.x1 - k.x0;
                bool passa = k.tipo == "corredor" || k.tipo == "escada";
                string nome = an.nomes[k];
                string texto = passa ? nome : $"{nome}\n{w:0.#} × {(k.z1 - k.z0):0.#} m";
                if (k.tipo == "corredor") { Rotulo(nome, new Vector3(k.Cx - 14f, y + 1.7f, k.Cz), letra); continue; }
                int max = Mathf.Max(6, Mathf.FloorToInt((w - 0.4f) / (letra * 0.56f)));
                Rotulo(Quebrar(texto, max), new Vector3(k.Cx, y + 1.7f, k.Cz), letra);
            }
            Rotulo($"Hospital Principal · {NomesDosAndares[f]} · a arquitetura", new Vector3((x0 + x1) / 2f, y + 1.7f, z0 + 2.2f), 1.5f);
            Paredes(an, y + 1.6f, rotulos);
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
            go.transform.SetPositionAndRotation(new Vector3((x0 + x1) / 2f, y + 2.25f, (z0 + z1) / 2f), giro);
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
            foreach (var r in rotulos) Object.DestroyImmediate(r);
            return tex;
        }
    }
}
