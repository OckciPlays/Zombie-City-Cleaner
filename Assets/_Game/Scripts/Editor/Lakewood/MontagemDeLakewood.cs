using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As ferramentas do mapa de Lakewood (Plano_Lakewood, Etapas 1 e 2), no menu Zombie City Cleaner > Lakewood:
    ///  1. Refazer o esboço do mapa (<see cref="EsbocoDeLakewood"/>: o desenho feito à mão no código, sem mexer no que vem
    ///     da imagem);
    ///  2. Ler a imagem de referência (<see cref="LeituraDaReferencia"/>: o lago, as ilhas e a cobertura);
    ///  3. Montar a cena (`Scenes/Lakewood.unity`, separada da cena Mundo): a luz, o volume, o clima, a câmera do menu, a
    ///     rede e o jogador de hoje, os itens no chão, o carro e o "Lakewood", com o <see cref="LakewoodNaCena"/>; a partida
    ///     fica no chão seco mais perto do ponto do mapa (a margem da Lakewood, olhando o reservatório);
    ///  - a prévia do relevo na cena aberta (o relevo de longe dos 16 setores, a água e a barragem, em objetos que não são
    ///    salvos), as fotos do relevo (`Imgs/Lakewood/Lakewood_Relevo_*.png` da pasta de arte) e o relatório dos critérios
    ///    de qualidade do plano (<see cref="Relatorio"/>).
    /// </summary>
    public static class MontagemDeLakewood
    {
        public const string Cena = "Assets/_Game/Scenes/Lakewood.unity";
        /// <summary>O material do relevo de longe (a cor nos vértices), criado se não houver.</summary>
        public const string MaterialDeLonge = "Assets/_Game/Art/Mundo/RelevoDeLonge.mat";

        /// <summary>O material do relevo de longe, salvo (10/11/2026: o da cena era feito na hora e não ia para a build).</summary>
        public const string MaterialDoChaoDeLonge = "Assets/_Game/Settings/Lakewood/ChaoDeLonge.mat";

        public static Material MaterialDoRelevoDeLonge()
        {
            // o ChaoDeLonge (as texturas do chão de perto mais a copa da mata), a partir do material do chão. Salvo num asset:
            // feito na hora (HideAndDontSave), ele não ficava na cena, e na build o Shader.Find não achava o shader (nenhum
            // material o usava), e o relevo de longe saía com o material da cidade
            var shader = Shader.Find("ZombieCityCleaner/ChaoDeLonge");
            var chao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            if (shader == null || chao == null)
            {
                Debug.LogWarning("[Lakewood] Não achei o shader ZombieCityCleaner/ChaoDeLonge ou o material do chão; o relevo de longe sai com o material da cidade.");
                return null;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDoChaoDeLonge);
            if (m == null)
            {
                m = new Material(shader) { name = "ChaoDeLonge" };
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialDoChaoDeLonge));
                AssetDatabase.CreateAsset(m, MaterialDoChaoDeLonge);
            }
            m.shader = shader;
            m.CopyPropertiesFromMaterial(chao);
            m.SetColor("_CopaCor", new Color(0.16f, 0.24f, 0.13f));
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }
        const string CenaAntiga = "Assets/_Game/Scenes/MapaFixo.unity";
        const string NomeDaPrevia = "Lakewood (prévia do relevo)";

        /// <summary>O mapa do projeto (criado vazio, se pedido e ainda não houver).</summary>
        public static MapaDeLakewood Carregar(bool criar)
        {
            var m = AssetDatabase.LoadAssetAtPath<MapaDeLakewood>(MapaDeLakewood.Caminho);
            if (m == null && criar)
            {
                m = ScriptableObject.CreateInstance<MapaDeLakewood>();
                Directory.CreateDirectory(Path.GetDirectoryName(MapaDeLakewood.Caminho));
                AssetDatabase.CreateAsset(m, MapaDeLakewood.Caminho);
            }
            return m;
        }

        [MenuItem("Zombie City Cleaner/Lakewood/1. Refazer o esboço do mapa", false, 1)]
        public static void Esboco()
        {
            var m = Carregar(true);
            Undo.RecordObject(m, "Esboço do mapa de Lakewood");
            EsbocoDeLakewood.Preencher(m);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lakewood] Esboço gravado em {MapaDeLakewood.Caminho}: {m.relevo.zonas.Count} zonas, {m.rios.Count} rios, {m.cidades.Count} cidades, {m.lugares.Count} lugares, {m.tuneis.Count} túneis. " +
                      (m.lagos.Count == 0 ? "Agora leia a imagem de referência (2)." : "O lago e as ilhas da imagem continuam."));
        }

        [MenuItem("Zombie City Cleaner/Lakewood/2. Ler a imagem de referência", false, 2)]
        public static void Ler()
        {
            var m = Carregar(true);
            if (m.cidades.Count == 0) Debug.LogWarning("[Lakewood] O mapa está sem o esboço (as cidades e a barragem): a leitura sai sem o urbano e sem o recorte da barragem. Rode o 1 antes.");
            float t0 = Time.realtimeSinceStartup;
            Undo.RecordObject(m, "Leitura da referência de Lakewood");
            string resumo;
            try
            {
                EditorUtility.DisplayProgressBar("Lakewood", "Lendo a imagem de referência...", 0.3f);
                resumo = LeituraDaReferencia.Ler(m);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lakewood] Referência lida em {Time.realtimeSinceStartup - t0:F1} s. {resumo}");
        }

        // ------------------------------------------------------------------ a cena

        [MenuItem("Zombie City Cleaner/Lakewood/3. Montar a cena", false, 3)]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena do Lakewood.");
                return;
            }
            var mapa = Carregar(false);
            if (mapa == null || mapa.lagos.Count == 0)
            {
                Debug.LogWarning($"Falta o mapa ({MapaDeLakewood.Caminho}) com o lago lido da referência: rode o 1 e o 2 antes.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorUtility.DisplayProgressBar("Lakewood", "O relevo do mapa...", 0.2f);
            ChaoDeLakewood chao;
            try { chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa)); }
            finally { EditorUtility.ClearProgressBar(); }

            // a partida: o chão seco mais perto do ponto do mapa, olhando o meio do reservatório
            var partida = ChaoSeco(chao, mapa.partida, out _);
            int principal = mapa.LagoPrincipal();
            var alvo = principal >= 0 ? Linhas.Centro(MapaDeLakewood.Contorno(mapa.lagos[principal].contorno)) : mapa.Meio;
            var frente = (alvo - partida).normalized;
            var lado = new Vector2(frente.y, -frente.x);
            Vector3 NoChao(Vector2 p, float acima) => new Vector3(p.x, chao.Altura(p.x, p.y) + acima, p.y);
            float giro = Mathf.Atan2(frente.x, frente.y) * Mathf.Rad2Deg;
            var carro2 = ChaoSeco(chao, partida + lado * 7f, out _);

            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(cena, Cena);

            var sol = new GameObject("Sol e lua").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var noite = new GameObject("Pós-processamento (noite)").AddComponent<Volume>();
            noite.isGlobal = true;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;

            // a câmera do menu (some quando o jogador local nasce), olhando o reservatório
            var camera = new GameObject("Câmera do menu") { tag = "MainCamera" };
            var olho = NoChao(partida - frente * 12f, 6f);
            camera.transform.SetPositionAndRotation(olho, Quaternion.LookRotation(NoChao(partida + frente * 40f, 0f) - olho));
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.AddComponent<AudioListener>();

            MontagemDoClima.Montar();
            var jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            MontagemDaRede.MontarCena(jogador);
            var pontos = cena.GetRootGameObjects().First(g => g.name == "Pontos de nascimento").transform;
            int i = 0;
            foreach (Transform p in pontos)
            {
                var onde = partida + lado * (2.5f * i - 3.75f);
                p.SetPositionAndRotation(NoChao(onde, 0.1f), Quaternion.Euler(0f, giro, 0f));
                i++;
            }
            MontagemDasArmas.MontarItensNoMundo(cena);

            // o carro: o anfitrião cria ao lado da partida, então ele vai para a lista dos prefabs da rede
            var carro = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Veiculos/Carro.prefab");
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (carro != null && lista != null && !lista.PrefabList.Any(p => p.Prefab == carro))
            {
                lista.Add(new NetworkPrefab { Prefab = carro });
                EditorUtility.SetDirty(lista);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao) == null) TexturasDoChao.Gerar();
            TexturasDoAbandono.MateriaisDoMundo();
            var lakewood = new GameObject("Lakewood").AddComponent<LakewoodNaCena>();
            lakewood.mapa = mapa;
            lakewood.materialDoChao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            lakewood.materialDaAgua = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            lakewood.materialDaCidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoMundo);
            lakewood.materialDeLonge = MaterialDoRelevoDeLonge();
            lakewood.partida = NoChao(partida, 0f);
            lakewood.carro = carro;
            lakewood.lugarDoCarro = NoChao(carro2, 0f);
            lakewood.giroDoCarro = giro;
            PorAFloresta(lakewood);
            PorAUsina(lakewood);
            // a casa da partida (com ela no mapa): os pontos de nascimento no terreiro, os veículos e a mesa
            CasaDaPartidaDeLakewood.PorNaCena(cena, chao, lakewood);
            // as cidades (Etapa 6): os materiais das obras, a mobília e as portas
            CidadesNaCenaDeLakewood.PorNaCena(cena, lakewood);
            // o jogo do mundo sorteado (12/11/2026): o "Mundo (Lakewood)" na rede, com os sistemas do mundo
            JogoNoLakewood.PorNaCena(cena);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            NasBuildSettings();
            Selection.activeGameObject = lakewood.gameObject;
            Debug.Log($"Cena do Lakewood montada: {Cena}. A partida fica em ({partida.x:F0}; {partida.y:F0}), setor {mapa.NomeDoSetorEm(partida.x, partida.y)}, " +
                      "olhando o reservatório. Dê Play e hospede local; o F1 tem o painel do Lakewood (a neblina, o voo e o ir para).");
        }

        /// <summary>O ponto de chão seco (fora da água e das ilhas sem chão) mais perto do pedido, em anéis de 20 m.</summary>
        public static Vector2 ChaoSeco(ChaoDeLakewood chao, Vector2 pedido, out float altura)
        {
            var m = chao.Mapa;
            bool Serve(Vector2 p) => m.Dentro(p.x, p.y) && !chao.Agua(p.x, p.y, out _, 10f) && chao.Relevo.Inclinacao(p.x, p.y) < 0.25f;
            if (Serve(pedido))
            {
                altura = chao.Altura(pedido.x, pedido.y);
                return pedido;
            }
            for (float raio = 20f; raio < 3000f; raio += 20f)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(raio / 10f));
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    var p = pedido + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio;
                    if (!Serve(p)) continue;
                    altura = chao.Altura(p.x, p.y);
                    return p;
                }
            }
            altura = chao.Altura(pedido.x, pedido.y);
            return pedido;
        }

        // a cena nova entra nas Build Settings no lugar da antiga (e as que não existem mais saem)
        static void NasBuildSettings()
        {
            var cenas = new List<EditorBuildSettingsScene>();
            bool tem = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(s.path) || s.path == CenaAntiga || !File.Exists(s.path)) continue;
                if (s.path == Cena) tem = true;
                cenas.Add(s);
            }
            if (!tem) cenas.Add(new EditorBuildSettingsScene(Cena, true));
            EditorBuildSettings.scenes = cenas.ToArray();
        }

        /// <summary>
        /// A floresta nova (09/11/2026, o Plano_Floresta.md): os 15 pinheiros leves (o alto, o médio e o pequeno, 5 de cada,
        /// na ordem das espécies PinheiroAlto1 em diante) e a malha e o material da grama densa, gerados se faltam.
        /// </summary>
        public static void PorAFloresta(LakewoodNaCena mapa)
        {
            var lista = new List<GameObject>();
            foreach (PinheiroRealista.Tamanho t in System.Enum.GetValues(typeof(PinheiroRealista.Tamanho)))
                for (int v = 1; v <= PinheiroRealista.Variacoes; v++)
                    lista.Add(AssetDatabase.LoadAssetAtPath<GameObject>(GeradorDePinheirosReais.Prefab(t, v, true)));
            mapa.pinheiros = lista.ToArray();
            int faltam = lista.Count(p => p == null);
            if (faltam > 0) Debug.LogWarning($"Faltam {faltam} pinheiros leves: rode Zombie City Cleaner > Floresta > Gerar os pinheiros leves.");
            var grama = new GameObject("temp");
            try
            {
                var g = GeradorDeGrama.PorNaCena(grama);
                mapa.malhaDaGrama = g.malha;
                mapa.materialDaGrama = g.material;
            }
            finally { Object.DestroyImmediate(grama); }
            // as árvores até 480 m sem a neblina (o Plano_Lakewood; a copa de longe sobe de 430 a 650 m): a cena guardava os
            // 320 m de antes, e entre 320 e 430 m os morros ficavam pelados
            mapa.plantasSemNeblina = 480f;
            EditorUtility.SetDirty(mapa);
        }

        /// <summary>A hidrelétrica na cena (Etapa 4): o material do abandono pela altura do objeto, os carros parados e a barreira.</summary>
        public static void PorAUsina(LakewoodNaCena mapa)
        {
            TexturasDoAbandono.MateriaisDoMundo();
            mapa.materialDaUsina = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDosObjetos);
            const string fbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
            var malhas = AssetDatabase.LoadAllAssetsAtPath(fbxRua).OfType<Mesh>().ToList();
            mapa.carrosParados = ModelosDaEstrada.NomesDosCarros.Select(n => malhas.FirstOrDefault(m => m.name == n)).ToArray();
            mapa.barreira = malhas.FirstOrDefault(m => m.name == "Barreira");
            if (mapa.carrosParados.Any(c => c == null) || mapa.barreira == null) Debug.LogWarning($"[Lakewood] Faltam modelos do {fbxRua} (os carros ou a barreira).");
            EditorUtility.SetDirty(mapa);
        }

        /// <summary>
        /// A Etapa 4 no mapa e na cena (10/11/2026): no asset, o colo da cabeceira oeste (a passagem do relevo), a crista de
        /// 12 m e o fim do túnel da barragem no prédio da galeria de serviço; na cena do Lakewood, os materiais e os modelos da
        /// usina. A cena aberta não muda (a do Lakewood abre junto e sai).
        /// </summary>
        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 4: a usina no mapa e na cena", false, 4)]
        public static void EtapaQuatro()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Lakewood] Saia do Play antes.");
                return;
            }
            var m = Carregar(false);
            if (m == null) return;
            Undo.RecordObject(m, "A usina no mapa");
            var colo = EsbocoDeLakewood.ColoDaBarragem();
            int k = m.relevo.passagens.FindIndex(p => p.nome == colo.nome);
            if (k >= 0) m.relevo.passagens[k] = colo;
            else m.relevo.passagens.Add(colo);
            m.represa.larguraDaCrista = 12f;
            var tunel = m.tuneis.Find(t => t.nome == "Lab – Dam");
            if (tunel != null && tunel.pontos.Count > 0) tunel.pontos[tunel.pontos.Count - 1] = EsbocoDeLakewood.FimDoTunelDaBarragem;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            var cena = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Cena);
            bool abriu = !cena.isLoaded;
            if (abriu) cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Additive);
            LakewoodNaCena lw = null;
            foreach (var raiz in cena.GetRootGameObjects())
                if (lw == null) lw = raiz.GetComponentInChildren<LakewoodNaCena>(true);
            if (lw == null) Debug.LogWarning($"[Lakewood] A cena {Cena} não tem o LakewoodNaCena: rode o 3 (Montar a cena).");
            else
            {
                PorAUsina(lw);
                EditorSceneManager.MarkSceneDirty(cena);
                EditorSceneManager.SaveScene(cena);
            }
            if (abriu) EditorSceneManager.CloseScene(cena, true);
            var chao = new ChaoDeLakewood(m, new RelevoDeLakewood(m));
            Debug.Log($"[Lakewood] A usina no mapa: o colo a {colo.altura:F0} m, a crista de {m.represa.larguraDaCrista:F0} m, {chao.Usina.Vias.Count} estradas de acesso, {chao.Usina.Torres.Count} torres.");
        }

        /// <summary>As malhas das plantas (3 por espécie, na ordem de Floresta.Nomes) e o material da vegetação (feito, se faltar).</summary>
        static Mesh[] MalhasDasPlantas(out Material material)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            if (material == null)
            {
                var temp = new GameObject("temp");
                MontagemDoMundo.PorAFloresta(temp);
                Object.DestroyImmediate(temp);
                material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            }
            return GeradorDeFloresta.MalhasDasPlantas();
        }

        // ------------------------------------------------------------------ as fotos de perto (o terreno de 2 m, as plantas e a água)

        /// <summary>
        /// Fotos na beira da água, com o que o jogador vê de perto: o terreno de 2 m (até 180 m; de 8 m até 520 m), as plantas
        /// (com a grama até 90 m) e a água, em cima da prévia do relevo de longe. Cada foto nasce de um ponto em terra e um na
        /// água: a câmera fica na linha d'água entre os dois, 9 m para dentro da terra e 1,8 m acima do chão, olhando ao longo
        /// da margem (ou para o ponto pedido). `Imgs/Lakewood/Lakewood_Perto_*.png` da pasta de arte.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Lakewood/Fotos de perto", false, 44)]
        public static void FotosDePerto()
        {
            var mapa = Carregar(false);
            if (mapa == null) return;
            float t0 = Time.realtimeSinceStartup;
            var relevo = new RelevoDeLakewood(mapa);
            var chao = new ChaoDeLakewood(mapa, relevo);
            if (AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao) == null) TexturasDoChao.Gerar();
            var matChao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            var malhas = MalhasDasPlantas(out var matVegetacao);
            var matPlantas = new Material(matVegetacao) { hideFlags = HideFlags.HideAndDontSave };
            matPlantas.SetVector("_Some", new Vector4(3000f, 4000f, 0f, 0f));
            var matGrama = new Material(matPlantas) { hideFlags = HideFlags.HideAndDontSave };
            matGrama.SetFloat("_Cima", 0.8f);
            Shader.SetGlobalVector("_ZccVento", new Vector4(0.92f, 0.38f, 0.2f, 0f));

            var vistas = new List<(string nome, Vector2 terra, Vector2 agua, Vector2? olhar, Vector3? camera)>
            {
                ("Praia_Oeste", new Vector2(2600f, 4350f), new Vector2(3300f, 4850f), null, null),
                ("Costa_Leste", new Vector2(9600f, 6100f), new Vector2(9000f, 6500f), null, null),
                ("Ilha_Central", new Vector2(6125f, 5180f), new Vector2(6125f, 4200f), new Vector2(8300f, 3800f), null),
                ("Foz_Pine_River", new Vector2(7100f, 7100f), new Vector2(7100f, 7100f), new Vector2(6980f, 7500f), new Vector3(7100f, 153f, 7100f)),
                ("Enseada_Norte", new Vector2(5700f, 7450f), new Vector2(6050f, 7450f), new Vector2(6250f, 6950f), null),
            };
            // a encosta mais em pé da margem leste, vista de cima da água
            {
                var contorno = MapaDeLakewood.Contorno(mapa.lagos[mapa.LagoPrincipal()].contorno);
                float nivel = relevo.NivelDosLagos[mapa.LagoPrincipal()];
                float melhor = 0f;
                Vector2 pe = Vector2.zero, fora = Vector2.right;
                for (int i = 0; i < contorno.Count; i++)
                {
                    var p = contorno[i];
                    if (p.x < 8300f) continue;
                    var tang = (contorno[(i + 1) % contorno.Count] - p).normalized;
                    var normal = new Vector2(-tang.y, tang.x);
                    var q1 = p + normal * 30f;
                    var q2 = p - normal * 30f;
                    var lado = chao.Altura(q1.x, q1.y) > chao.Altura(q2.x, q2.y) ? normal : -normal;
                    var q = p + lado * 30f;
                    float incl = relevo.Inclinacao(q.x, q.y);
                    if (incl > melhor) { melhor = incl; pe = p; fora = lado; }
                }
                if (melhor > 0f)
                {
                    var sobreAAgua = pe - fora * 55f;
                    vistas.Add(("Encosta_Leste", pe + fora * 20f, sobreAAgua, pe + fora * 45f, new Vector3(sobreAAgua.x, nivel + 3f, sobreAAgua.y)));
                }
            }
            // os rios: da margem, olhando rio abaixo (o canal, os barrancos e a água correndo)
            foreach (var (nomeDoRio, t) in new[] { ("Pine River", 0.5f), ("Fairview Creek", 0.55f), ("Riverside Creek", 0.6f), ("Lakewood River", 0.5f) })
            {
                int ri = mapa.rios.FindIndex(x => x.nome == nomeDoRio);
                if (ri < 0 || relevo.LinhasDosRios[ri] == null) continue;
                var linha = relevo.LinhasDosRios[ri];
                var p = Linhas.PontoNa(linha, t, out var dir);
                var lado = new Vector2(-dir.y, dir.x);
                float meia = MapaDeLakewood.LarguraDoRio(mapa.rios[ri], t) * 0.5f;
                float rampa = MapaDeLakewood.MargemDoRio(meia * 2f) * 3.4f;
                var pe = p + lado * (meia + rampa + 0.8f);
                var alvo = p + dir * 45f;
                vistas.Add(("Rio_" + nomeDoRio.Replace(" ", ""), pe, p, alvo, new Vector3(pe.x, -1f, pe.y)));
            }
            var previa = GameObject.Find(NomeDaPrevia);
            bool minha = previa == null;
            if (minha) previa = MontarPrevia(mapa, 25f);
            var raiz = new GameObject("Fotos de perto (temporário)") { hideFlags = HideFlags.HideAndDontSave };
            var fogAntes = RenderSettings.fog;
            RenderSettings.fog = false;
            // a luz ambiente do dia do jogo (a da cena guardada pode estar escura): o reflexo do céu na água e as sombras
            var ambienteAntes = (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor, RenderSettings.ambientIntensity);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.57f, 0.59f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.45f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.29f, 0.29f, 0.27f);
            RenderSettings.ambientIntensity = 1f;
            // a água com a refração (a cópia da tela), como na Alta
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool opacaAntes = urp != null && urp.supportsCameraOpaqueTexture;
            if (urp != null) urp.supportsCameraOpaqueTexture = true;
            Shader.SetGlobalFloat("_ZccAguaRefracao", 1f);
            var camera = new GameObject("Câmera das fotos") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 30000f;
            camera.fieldOfView = 62f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            try
            {
                Directory.CreateDirectory(ImagemDeLakewood.Pasta);
                foreach (var v in vistas)
                {
                    // a linha d'água entre a terra e a água
                    var dir = (v.agua - v.terra).normalized;
                    var w = v.agua;
                    float alcance = Vector2.Distance(v.terra, v.agua);
                    for (float s = 0f; s < alcance; s += 3f)
                    {
                        var p = v.terra + dir * s;
                        if (!chao.Agua(p.x, p.y, out _)) continue;
                        w = p;
                        break;
                    }
                    // em terra seca: volta até o chão ficar acima da água (a beira pode descer antes da célula da água)
                    chao.Agua(w.x, w.y, out float nivelEmW);
                    var pos2 = w - dir * 6f;
                    for (int passo = 0; passo < 60 && chao.Altura(pos2.x, pos2.y) < nivelEmW + 0.4f; passo++) pos2 -= dir * 2f;
                    pos2 -= dir * 5f;
                    var pos = v.camera ?? new Vector3(pos2.x, chao.Altura(pos2.x, pos2.y) + 1.8f, pos2.y);
                    if (v.camera.HasValue && v.camera.Value.y < 0f) pos.y = chao.Altura(pos.x, pos.z) + 2.5f;
                    Vector3 alvo;
                    if (v.olhar.HasValue) alvo = new Vector3(v.olhar.Value.x, chao.Altura(v.olhar.Value.x, v.olhar.Value.y) + 1f, v.olhar.Value.y);
                    else
                    {
                        var lado = new Vector2(-dir.y, dir.x);
                        var a2 = w + lado * 90f + dir * 15f;
                        alvo = new Vector3(a2.x, 150f, a2.y);
                    }
                    var objetos = new List<GameObject>();
                    var malhasDoChao = new List<Mesh>();
                    MontarPerto(chao, pos, raiz.transform, matChao, malhas, matPlantas, matGrama, objetos, malhasDoChao);
                    camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(alvo - pos));
                    Foto(camera, rt, Path.Combine(ImagemDeLakewood.Pasta, $"Lakewood_Perto_{v.nome}.png"));
                    foreach (var go in objetos) Object.DestroyImmediate(go);
                    foreach (var m in malhasDoChao) Object.DestroyImmediate(m);
                }
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(raiz);
                Object.DestroyImmediate(matPlantas);
                Object.DestroyImmediate(matGrama);
                RenderSettings.fog = fogAntes;
                (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor, RenderSettings.ambientIntensity) = ambienteAntes;
                if (urp != null) urp.supportsCameraOpaqueTexture = opacaAntes;
                Shader.SetGlobalFloat("_ZccAguaRefracao", 0f);
                if (minha) Object.DestroyImmediate(previa);
            }
            Debug.Log($"[Lakewood] Fotos de perto em {ImagemDeLakewood.Pasta} ({Time.realtimeSinceStartup - t0:F0} s).");
        }

        // o terreno de perto em volta de um ponto (2 m até 180 m, 8 m até 520 m) e as plantas (até 300 m; a grama até 90 m)
        static void MontarPerto(ChaoDeLakewood chao, Vector3 pos, Transform pai, Material matChao, Mesh[] malhas, Material matPlantas, Material matGrama, List<GameObject> objetos, List<Mesh> malhasDoChao)
        {
            var p = new Vector2(pos.x, pos.z);
            float t = ChaoDeLakewood.TamanhoDoBloco;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((p.x - 520f) / t)), i1 = Mathf.Min(chao.BlocosX - 1, Mathf.FloorToInt((p.x + 520f) / t));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((p.y - 520f) / t)), j1 = Mathf.Min(chao.BlocosZ - 1, Mathf.FloorToInt((p.y + 520f) / t));
            var plantas = new List<PlantaDaFloresta>();
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                var canto = ChaoDeLakewood.CantoDoBloco(i, j);
                float dx = Mathf.Max(0f, Mathf.Max(canto.x - p.x, p.x - canto.x - t)), dz = Mathf.Max(0f, Mathf.Max(canto.y - p.y, p.y - canto.y - t));
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > 520f) continue;
                var mesh = new Mesh { name = "Chão" };
                chao.Terreno(i, j, d < 180f ? 2f : 8f).Para(mesh);
                malhasDoChao.Add(mesh);
                var go = new GameObject($"Chão {i} {j}") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(pai, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = matChao;
                objetos.Add(go);
                if (d < 300f) chao.Plantas(i, j, plantas, d < 90f);
            }
            foreach (var pl in plantas)
            {
                float d2 = (pl.Posicao - pos).sqrMagnitude;
                int e = (int)pl.Especie;
                int lod = d2 < 55f * 55f ? 0 : d2 < 110f * 110f ? 1 : 2;
                var mesh = e * 3 + lod < malhas.Length ? malhas[e * 3 + lod] : null;
                if (mesh == null) continue;
                var go = new GameObject(Floresta.Nomes[e]) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(pai, false);
                go.transform.SetPositionAndRotation(pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f));
                go.transform.localScale = Vector3.one * pl.Escala;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                bool grama = Floresta.EhGrama(pl.Especie) || pl.Especie == Especie.CapimA || pl.Especie == Especie.CapimB || pl.Especie == Especie.Taboa;
                go.AddComponent<MeshRenderer>().sharedMaterial = grama ? matGrama : matPlantas;
                objetos.Add(go);
            }
        }

        // ------------------------------------------------------------------ a prévia do relevo na cena aberta

        [MenuItem("Zombie City Cleaner/Lakewood/Prévia do relevo na cena", false, 40)]
        public static void Previa()
        {
            var antiga = GameObject.Find(NomeDaPrevia);
            if (antiga != null)
            {
                Object.DestroyImmediate(antiga);
                Debug.Log("[Lakewood] A prévia do relevo saiu da cena.");
                return;
            }
            var mapa = Carregar(false);
            if (mapa == null) return;
            float t0 = Time.realtimeSinceStartup;
            MontarPrevia(mapa, 25f);
            Debug.Log($"[Lakewood] A prévia do relevo está na cena ({Time.realtimeSinceStartup - t0:F1} s): os 16 setores a 25 m, a água e a barragem. O mesmo menu tira.");
        }

        /// <summary>A prévia: o relevo de longe de cada setor, a água e a barragem, em objetos que não são salvos.</summary>
        public static GameObject MontarPrevia(MapaDeLakewood mapa, float passo)
        {
            var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
            TexturasDoAbandono.MateriaisDoMundo();
            var cidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoMundo);
            if (AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua) == null) TexturasDoChao.Gerar();
            var agua = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            var longe = MaterialDoRelevoDeLonge() ?? cidade;
            var raiz = new GameObject(NomeDaPrevia) { hideFlags = HideFlags.DontSave };
            GameObject Objeto(string nome, Mesh malha, Material material)
            {
                var go = new GameObject(nome) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                return go;
            }
            for (int j = 0; j < mapa.setoresZ; j++)
            for (int i = 0; i < mapa.setoresX; i++)
            {
                var mesh = new Mesh { name = $"Longe {MapaDeLakewood.NomeDoSetor(i, j)}" };
                chao.Longe(i, j, passo).Para(mesh);
                Objeto($"Setor {MapaDeLakewood.NomeDoSetor(i, j)}", mesh, longe);
            }
            for (int l = 0; l < mapa.lagos.Count; l++)
            {
                var m = chao.AguaDoLago(l);
                if (m == null) continue;
                var mesh = new Mesh { name = "Água" };
                m.Para(mesh);
                Objeto($"Água: {mapa.lagos[l].nome}", mesh, agua);
            }
            for (int r = 0; r < mapa.rios.Count; r++)
            {
                var m = chao.AguaDoRio(r);
                if (m == null) continue;
                var mesh = new Mesh { name = "Rio" };
                m.Para(mesh);
                Objeto($"Rio: {mapa.rios[r].nome}", mesh, agua);
            }
            // a hidrelétrica (as peças de obra: a barragem, os prédios, as torres e os cabos)
            var objetos = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDosObjetos) ?? cidade;
            var feitas = new Dictionary<MalhaDeObra, Mesh>();
            foreach (var p in chao.Usina.Montar((x, z) => chao.Altura(x, z)))
            {
                if (p.Tipo != TipoDaPecaDaUsina.Obra || p.Desenho == null) continue;
                if (!feitas.TryGetValue(p.Desenho, out var malha)) feitas[p.Desenho] = malha = p.Desenho.ParaMesh(p.Nome);
                Objeto(p.Nome, malha, objetos).transform.SetPositionAndRotation(p.Posicao, p.Giro);
            }
            return raiz;
        }

        // ------------------------------------------------------------------ as fotos do relevo

        [MenuItem("Zombie City Cleaner/Lakewood/Fotos do relevo", false, 41)]
        public static void Fotos()
        {
            var mapa = Carregar(false);
            if (mapa == null) return;
            float t0 = Time.realtimeSinceStartup;
            var previa = GameObject.Find(NomeDaPrevia);
            bool minha = previa == null;
            if (minha) previa = MontarPrevia(mapa, 25f);
            var relevo = new RelevoDeLakewood(mapa);
            float H(float x, float z) => relevo.Altura(x, z);
            var represa = mapa.represa;
            var lago = mapa.LagoPrincipal() >= 0 ? Linhas.Centro(MapaDeLakewood.Contorno(mapa.lagos[mapa.LagoPrincipal()].contorno)) : mapa.Meio;
            var vistas = new (string nome, Vector3 de, Vector3 para, float fov)[]
            {
                ("Represa", new Vector3(4250f, H(4250f, 2050f) + 70f, 2050f), new Vector3(represa.Meio.x, 140f, represa.Meio.y), 60f),
                ("Lakewood_Margem", new Vector3(8750f, H(8750f, 3650f) + 40f, 3650f), new Vector3(6200f, 150f, 5200f), 60f),
                ("Norte", new Vector3(6400f, H(6400f, 9200f) + 160f, 9200f), new Vector3(6900f, 150f, 7300f), 60f),
                ("Oeste", new Vector3(1500f, H(1500f, 6600f) + 80f, 6600f), new Vector3(4500f, 150f, 5500f), 60f),
                ("Aereo_Sul", new Vector3(6000f, 4800f, -3500f), new Vector3(6000f, 150f, 5200f), 55f),
                ("Aereo_Leste", new Vector3(16500f, 4500f, 5000f), new Vector3(7000f, 150f, 5000f), 55f),
            };
            var fogAntes = RenderSettings.fog;
            RenderSettings.fog = false;
            var camera = new GameObject("Câmera das fotos") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.nearClipPlane = 2f;
            camera.farClipPlane = 30000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            try
            {
                Directory.CreateDirectory(ImagemDeLakewood.Pasta);
                foreach (var (nome, de, para, fov) in vistas)
                {
                    camera.orthographic = false;
                    camera.fieldOfView = fov;
                    camera.transform.SetPositionAndRotation(de, Quaternion.LookRotation(para - de));
                    Foto(camera, rt, Path.Combine(ImagemDeLakewood.Pasta, $"Lakewood_Relevo_{nome}.png"));
                }
                // de cima, o mapa inteiro em ortográfica
                camera.orthographic = true;
                camera.orthographicSize = mapa.comprimento * 0.5f * 1.02f;
                camera.transform.SetPositionAndRotation(new Vector3(mapa.largura * 0.5f, 5000f, mapa.comprimento * 0.5f), Quaternion.Euler(90f, 0f, 0f));
                var rt2 = new RenderTexture(Mathf.RoundToInt(2400f * mapa.largura / mapa.comprimento), 2400, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = rt2;
                Foto(camera, rt2, Path.Combine(ImagemDeLakewood.Pasta, "Lakewood_Relevo_Cima.png"));
                camera.targetTexture = null;
                rt2.Release();
                Object.DestroyImmediate(rt2);
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camera.gameObject);
                RenderSettings.fog = fogAntes;
                if (minha) Object.DestroyImmediate(previa);
            }
            Debug.Log($"[Lakewood] Fotos do relevo em {ImagemDeLakewood.Pasta} ({Time.realtimeSinceStartup - t0:F0} s).");
        }

        static void Foto(Camera camera, RenderTexture rt, string caminho)
        {
            camera.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ o relatório dos critérios do plano

        [MenuItem("Zombie City Cleaner/Lakewood/Conferir o relevo (relatório)", false, 42)]
        public static void Conferir()
        {
            var mapa = Carregar(false);
            if (mapa == null) return;
            float t0 = Time.realtimeSinceStartup;
            var relevo = new RelevoDeLakewood(mapa);
            var chao = new ChaoDeLakewood(mapa, relevo);
            Debug.Log($"[Lakewood] Relatório do relevo ({Time.realtimeSinceStartup - t0:F1} s):\n" + Relatorio(mapa, relevo, chao));
        }

        /// <summary>Os números dos critérios de qualidade do plano (a área do lago, as cotas por região, a barragem, as cidades).</summary>
        public static string Relatorio(MapaDeLakewood m, RelevoDeLakewood r, ChaoDeLakewood chao)
        {
            var sb = new System.Text.StringBuilder();
            float areaDoMapa = m.largura * m.comprimento;
            float agua = 0f;
            foreach (var l in m.lagos) if (l.contorno.Count >= 3) agua += Linhas.Area(MapaDeLakewood.Contorno(l.contorno));
            foreach (var i in m.ilhas) if (i.contorno.Count >= 3) agua -= Linhas.Area(MapaDeLakewood.Contorno(i.contorno));
            sb.AppendLine($"Mapa: {m.largura / 1000f:F1} × {m.comprimento / 1000f:F1} km, {m.setoresX} × {m.setoresZ} setores de {m.LadoDoSetorX / 1000f:F2} × {m.LadoDoSetorZ / 1000f:F2} km, {chao.BlocosX} × {chao.BlocosZ} blocos de 64 m.");
            sb.AppendLine($"Água: {agua / 1e6f:F1} km² ({100f * agua / areaDoMapa:F1}% do mapa; o plano pede de 20 a 25%). Lagos: {m.lagos.Count}. Ilhas: {m.ilhas.Count}. Rios: {m.rios.Count}.");

            // as cotas por região (as médias e os extremos, numa amostra de 100 m)
            float Media(float x0, float x1, float z0, float z1, out float min, out float max, out float p90)
            {
                var v = new List<float>();
                for (float z = z0 + 50f; z < z1; z += 100f)
                for (float x = x0 + 50f; x < x1; x += 100f)
                    if (!chao.Agua(x, z, out _)) v.Add(r.Altura(x, z));
                v.Sort();
                min = v.Count > 0 ? v[0] : 0f;
                max = v.Count > 0 ? v[v.Count - 1] : 0f;
                p90 = v.Count > 0 ? v[Mathf.Min(v.Count - 1, Mathf.RoundToInt(0.9f * (v.Count - 1)))] : 0f;
                float soma = 0f;
                foreach (var h in v) soma += h;
                return v.Count > 0 ? soma / v.Count : 0f;
            }
            float terco = m.largura / 3f, tercoZ = m.comprimento / 3f;
            float mo = Media(0f, terco, 0f, m.comprimento, out float minO, out float maxO, out float p90O);
            float mn = Media(0f, m.largura, 2f * tercoZ, m.comprimento, out float minN, out float maxN, out _);
            float ml = Media(2f * terco, m.largura, 0f, m.comprimento, out float minL, out float maxL, out _);
            float ms = Media(terco, 2f * terco, 0f, tercoZ, out float minS, out float maxS, out _);
            sb.AppendLine($"Oeste (terço): média {mo:F0} m (de {minO:F0} a {maxO:F0}; 90% abaixo de {p90O:F0}). O plano: a planície de 160 a 260 m, com colinas baixas.");
            sb.AppendLine($"Norte (terço): média {mn:F0} m (de {minN:F0} a {maxN:F0}). Leste (terço): média {ml:F0} m (de {minL:F0} a {maxL:F0}). O plano: as montanhas até 600 a 650 m.");
            sb.AppendLine($"Sul (meio): média {ms:F0} m (de {minS:F0} a {maxS:F0}).");
            float maxTudo = float.MinValue;
            Vector2 ondeMax = default;
            for (int j = 0; j < r.NZ; j += 2)
            for (int i = 0; i < r.NX; i += 2)
                if (r.Alturas[j * r.NX + i] > maxTudo) { maxTudo = r.Alturas[j * r.NX + i]; ondeMax = new Vector2(r.X(i), r.Z(j)); }
            sb.AppendLine($"O ponto mais alto: {maxTudo:F0} m em ({ondeMax.x:F0}; {ondeMax.y:F0}), setor {m.NomeDoSetorEm(ondeMax.x, ondeMax.y)}.");
            float minTerra = float.MaxValue;
            Vector2 ondeMin = default;
            int abaixoDaAgua = 0;
            for (int j = 0; j < r.NZ; j += 2)
            for (int i = 0; i < r.NX; i += 2)
            {
                float x = r.X(i), z = r.Z(j);
                if (chao.Agua(x, z, out _, 14f)) continue;
                float h = r.Alturas[j * r.NX + i];
                if (h < 150f) abaixoDaAgua++;
                if (h < minTerra) { minTerra = h; ondeMin = new Vector2(x, z); }
            }
            sb.AppendLine($"O ponto mais baixo em terra (longe da água): {minTerra:F0} m em ({ondeMin.x:F0}; {ondeMin.y:F0}), setor {m.NomeDoSetorEm(ondeMin.x, ondeMin.y)}; {abaixoDaAgua} pontos de terra abaixo de 150 m (o vale do rio de saída conta).");
            foreach (var p in m.relevo.picos) sb.AppendLine($"Pico {p.nome}: {r.Maxima(p.ponto, p.raio * 0.6f):F0} m.");

            // a barragem e o vale
            if (m.represa.Tem)
            {
                var rp = m.represa;
                var jus = r.Jusante;
                var meio = rp.Meio;
                float hA = r.Altura(rp.a.x, rp.a.y), hB = r.Altura(rp.b.x, rp.b.y);
                float pe = r.Altura(meio.x + jus.x * 80f, meio.y + jus.y * 80f);
                float fundo = r.Altura(meio.x - jus.x * 80f, meio.y - jus.y * 80f);
                float vale = r.Altura(meio.x + jus.x * 400f, meio.y + jus.y * 400f);
                sb.AppendLine($"Barragem {rp.nome}: {rp.Comprimento:F0} m de crista a {rp.alturaDaCrista:F0} m; o chão nas cabeceiras {hA:F0} e {hB:F0} m; o fundo do lago junto dela {fundo:F0} m; o pé {pe:F0} m; o vale 400 m abaixo {vale:F0} m (o plano: de 90 a 110).");
            }

            // os rios
            for (int k = 0; k < m.rios.Count; k++)
            {
                var n = r.NiveisDosRios[k];
                if (n == null) continue;
                var pts = m.rios[k].pontos;
                sb.AppendLine($"Rio {m.rios[k].nome}: a água de {n[0]:F0} a {n[n.Length - 1]:F0} m, de ({pts[0].x:F0}; {pts[0].y:F0}) a ({pts[pts.Count - 1].x:F0}; {pts[pts.Count - 1].y:F0}){(r.NaBorda(pts[pts.Count - 1], 250f) ? ", sai pela borda" : chao.NoLago(pts[pts.Count - 1].x, pts[pts.Count - 1].y) ? ", chega no lago" : ", NÃO chega no lago nem na borda")}.");
            }

            // as cidades: a cota, a inclinação e a água dentro
            foreach (var c in m.cidades)
            {
                if (c.contorno.Count < 3) continue;
                var cont = MapaDeLakewood.Contorno(c.contorno);
                var caixa = Caixa(cont);
                int pontos = 0, naAgua = 0;
                float soma = 0f, inclinacao = 0f, min = float.MaxValue, max = float.MinValue;
                for (float z = caixa.yMin; z < caixa.yMax; z += 60f)
                for (float x = caixa.xMin; x < caixa.xMax; x += 60f)
                {
                    if (!Linhas.Dentro(new Vector2(x, z), cont)) continue;
                    pontos++;
                    if (chao.Agua(x, z, out _)) { naAgua++; continue; }
                    float h = r.Altura(x, z);
                    soma += h;
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                    inclinacao += r.Inclinacao(x, z);
                }
                int secos = pontos - naAgua;
                sb.AppendLine($"Cidade {c.nome} ({c.tipo}): {Linhas.Area(cont) / 1e6f:F2} km², cota média {(secos > 0 ? soma / secos : 0f):F0} m (de {min:F0} a {max:F0}), inclinação média {(secos > 0 ? 100f * inclinacao / secos : 0f):F1}%, {100f * naAgua / Mathf.Max(1, pontos):F0}% na água.");
            }
            foreach (var l in m.lugares)
                sb.AppendLine($"Lugar {l.nome} ({l.tipo}): cota {r.Altura(l.ponto.x, l.ponto.y):F0} m, inclinação {100f * r.Inclinacao(l.ponto.x, l.ponto.y):F1}%, setor {m.NomeDoSetorEm(l.ponto.x, l.ponto.y)}{(chao.Agua(l.ponto.x, l.ponto.y, out _) ? ", NA ÁGUA" : "")}.");
            foreach (var il in m.ilhas)
            {
                if (il.contorno.Count < 3 || il.tipo == TipoDeIlha.Ilhote) continue;
                var meio = Linhas.Centro(MapaDeLakewood.Contorno(il.contorno));
                sb.AppendLine($"Ilha {il.nome}: {Linhas.Area(MapaDeLakewood.Contorno(il.contorno)) / 1e6f:F2} km², o meio em ({meio.x:F0}; {meio.y:F0}) a {r.Maxima(meio, 300f):F0} m.");
            }
            return sb.ToString();
        }

        static Rect Caixa(List<Vector2> pts)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in pts)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y);
            }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }
    }
}
