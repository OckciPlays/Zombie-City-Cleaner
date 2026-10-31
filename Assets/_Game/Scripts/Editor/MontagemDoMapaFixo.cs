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
    /// A cena do mapa fixo, `Scenes/MapaFixo.unity` (29/10/2026, a pedido: o mapa limpo, só com o relevo, as montanhas, a
    /// mata, os rios, os lagos, a quarentena e as estradas principais, para construir aos poucos): menu Zombie City Cleaner
    /// > Mapa fixo > Montar a cena do mapa. Refaz a cena do zero:
    ///  - a luz e o volume, e por cima o clima (<see cref="MontagemDoClima"/>: o ciclo do dia, o ambiente, a neblina e as
    ///    partículas);
    ///  - a câmera do menu, a rede e o jogador de hoje (<see cref="MontagemDaRede.MontarCena"/>), com os pontos de
    ///    nascimento na beira da interestadual, e os itens no chão (para o F1);
    ///  - o "Mapa fixo", com o <see cref="MapaFixoNaCena"/>: o <see cref="DesenhoDoMapa"/>, os materiais do chão, da água,
    ///    da cidade (o do abandono, como nas estradas do mundo) e da vegetação, as plantas do Cidade_Arvores.fbx e o carro
    ///    ao lado da partida.
    /// A cena fica fora das Build Settings.
    /// </summary>
    public static class MontagemDoMapaFixo
    {
        public const string Cena = "Assets/_Game/Scenes/MapaFixo.unity";

        [MenuItem("Zombie City Cleaner/Mapa fixo/Montar a cena do mapa")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena do mapa fixo.");
                return;
            }
            var desenho = AssetDatabase.LoadAssetAtPath<DesenhoDoMapa>(DesenhoDoMapa.Caminho);
            if (desenho == null)
            {
                Debug.LogWarning($"Falta o desenho do mapa ({DesenhoDoMapa.Caminho}).");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // a partida: na beira da interestadual, no ponto mais perto do meio do mapa (fora das pontes e da água)
            EditorUtility.DisplayProgressBar("Mapa fixo", "O relevo do desenho...", 0.2f);
            ChaoDoMapaFixo chao;
            try { chao = new ChaoDoMapaFixo(desenho, new RelevoDoDesenho(desenho)); }
            finally { EditorUtility.ClearProgressBar(); }
            Partida(chao, out var naEstrada, out var dir, out float meia);
            var lado = new Vector2(dir.y, -dir.x);
            Vector3 NoChao(Vector2 p, float acima) => new Vector3(p.x, chao.Altura(p.x, p.y) + acima, p.y);
            var partida = naEstrada + lado * (meia + 5f);
            var carro2 = naEstrada + lado * (meia - 2.2f);
            float giroDaEstrada = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

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

            // a câmera do menu (some quando o jogador local nasce), olhando a estrada da partida
            var camera = new GameObject("Câmera do menu") { tag = "MainCamera" };
            var olho = NoChao(partida + lado * 18f, 7f);
            camera.transform.SetPositionAndRotation(olho, Quaternion.LookRotation(NoChao(naEstrada, 1f) - olho));
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
                var onde = partida + dir * (2.5f * i - 3.75f);
                p.SetPositionAndRotation(NoChao(onde, 0.1f), Quaternion.Euler(0f, giroDaEstrada, 0f));
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
            var mapa = new GameObject("Mapa fixo").AddComponent<MapaFixoNaCena>();
            mapa.desenho = desenho;
            mapa.materialDoChao = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao);
            mapa.materialDaAgua = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua);
            mapa.materialDaCidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDoMundo);
            mapa.partida = NoChao(partida, 0f);
            mapa.carro = carro;
            mapa.lugarDoCarro = NoChao(carro2, 0f);
            mapa.giroDoCarro = giroDaEstrada;
            PorAsPlantas(mapa);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Selection.activeGameObject = mapa.gameObject;
            Debug.Log($"Cena do mapa fixo montada: {Cena}. A partida fica na beira da {chao.Estradas.First(e => e.Via.tipo == TipoDeVia.Interestadual).Via.nome}, " +
                      $"em ({partida.x:F0}; {partida.y:F0}). Dê Play e hospede local; o F1 tem o painel do mapa fixo (a neblina, o voo e o ir para).");
        }

        /// <summary>O ponto da interestadual mais perto do meio do mapa, fora das pontes e da água, com a direção e a meia largura.</summary>
        static void Partida(ChaoDoMapaFixo chao, out Vector2 ponto, out Vector2 dir, out float meia)
        {
            ponto = Vector2.zero;
            dir = Vector2.up;
            meia = 6f;
            float melhor = float.MaxValue;
            foreach (var e in chao.Estradas)
            {
                if (e.Via.tipo != TipoDeVia.Interestadual) continue;
                for (int i = 8; i < e.Pontos.Length - 8; i++)
                {
                    var p = e.Pontos[i];
                    float d = p.magnitude;
                    if (d >= melhor || e.Ponte[i]) continue;
                    // longe das pontes (40 m para cada lado) e da água
                    bool perto = false;
                    for (int k = i - 8; k <= i + 8 && !perto; k++) perto = e.Ponte[k];
                    if (perto || chao.Agua(p.x, p.y, out _, 30f)) continue;
                    melhor = d;
                    ponto = p;
                    dir = (e.Pontos[i + 1] - e.Pontos[i - 1]).normalized;
                    meia = e.MeiaLargura;
                }
            }
        }

        /// <summary>As plantas do Cidade_Arvores.fbx (3 por espécie, na ordem de Floresta.Nomes) e o material da vegetação.</summary>
        static void PorAsPlantas(MapaFixoNaCena mapa)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            if (material == null)
            {
                // a montagem da floresta do mundo faz o material (e acerta a importação do FBX)
                var temp = new GameObject("temp");
                MontagemDoMundo.PorAFloresta(temp);
                Object.DestroyImmediate(temp);
                material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            }
            mapa.materialDaVegetacao = material;
            var todas = AssetDatabase.LoadAllAssetsAtPath(MontagemDoMundo.FbxDasArvores).OfType<Mesh>().ToDictionary(m => m.name);
            string[] sufixos = { "", "_Longe1", "_Longe2" };
            mapa.malhasDasPlantas = Floresta.Nomes.SelectMany(n => sufixos.Select(s => todas.TryGetValue(n + s, out var m) ? m : null)).ToArray();
            int faltam = mapa.malhasDasPlantas.Count(m => m == null);
            if (faltam > 0) Debug.LogWarning($"Faltam {faltam} malhas das plantas no {MontagemDoMundo.FbxDasArvores}.");
            EditorUtility.SetDirty(mapa);
        }
    }
}
