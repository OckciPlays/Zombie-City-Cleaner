using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena do Dia 0 (01/11/2026, a pedido: "criar essa cena separada da cena do mundo principal"), menu Zombie City
    /// Cleaner > Dia 0 > Montar a cena:
    ///  - refaz <c>Scenes/DiaZero.unity</c>: o objeto "Dia zero" com a <see cref="RuaDoDiaZero"/> (a rua montada pelo jogo
    ///    ao abrir: o material da cidade, os objetos de rua, a mobília antiga das lojas, o material das letras e as roupas
    ///    como itens) e os <see cref="NpcsDoDiaZero"/> (o NPC das roupas e o material das partículas); a luz da prévia (só
    ///    fora do Play);
    ///  - põe a cena nas Build Settings (ela é carregada por cima da do mundo, por nome);
    ///  - põe o <see cref="DiaZeroDaPartida"/> no "Mundo" da cena do mundo (e salva).
    /// A cena do mundo fica aberta (a do Dia 0 é montada ao lado e fechada).
    /// </summary>
    public static class MontagemDoDiaZero
    {
        public const string Cena = "Assets/_Game/Scenes/DiaZero.unity";
        const string FbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
        const string FbxMobilia = "Assets/_Game/Art/Cidade/Cidade_Mobilia.fbx";

        static Mesh Malha(string fbx, string nome) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name == nome);

        [MenuItem("Zombie City Cleaner/Dia 0/Montar a cena")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena do Dia 0.");
                return;
            }
            var aberta = SceneManager.GetSceneByPath(Cena);
            if (aberta.isLoaded) EditorSceneManager.CloseScene(aberta, true);
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var raiz = new GameObject("Dia zero");
            SceneManager.MoveGameObjectToScene(raiz, cena);
            var rua = raiz.AddComponent<RuaDoDiaZero>();
            rua.enabled = false;   // monta uma vez, com tudo ligado
            rua.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Cidade.mat");
            rua.poste = Malha(FbxRua, "Poste");
            rua.carros = new[] { "Carro_Sedan_Azul", "Carro_Sedan_Vinho", "Carro_Pickup_Bege", "Carro_Sedan_Cinza" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
            rua.arvores = new[] { "Pinheiro_A", "Pinheiro_B", "Pinheiro_C", "Arvore" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
            rua.barreira = Malha(FbxRua, "Barreira");
            rua.cavalete = Malha(FbxRua, "Cavalete");
            rua.cone = Malha(FbxRua, "Cone");
            rua.hidrante = Malha(FbxRua, "Hidrante");
            rua.lixeira = Malha(FbxRua, "Lixeira");
            rua.mobilia = AssetDatabase.LoadAllAssetsAtPath(FbxMobilia).OfType<Mesh>().ToArray();
            rua.materialDasLetras = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDoLetreiro);
            rua.roupas = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Roupas/Itens" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToArray();
            var npcs = raiz.AddComponent<NpcsDoDiaZero>();
            npcs.npc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Roupas/NPC.prefab");
            npcs.particulas = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Particulas.mat");
            // a introdução (a trilha do Ferramentas/trilha/intro_dia0.py e a tela da TV)
            var intro = raiz.AddComponent<IntroDoDiaZero>();
            intro.trilha = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/DiaZero/IntroDoDiaZero.ogg");
            intro.telaDaTv = MaterialDaTela();
            rua.enabled = true;

            // a luz da prévia (no jogo, a luz é a do clima da cena do mundo)
            var luz = new GameObject("Luz da prévia (só fora do Play)");
            SceneManager.MoveGameObjectToScene(luz, cena);
            luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = luz.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.shadows = LightShadows.Soft;
            luz.AddComponent<LuzDaPrevia>();

            // o pós-processamento do Dia 0: as cores vivas do dia limpo (o do dia do mundo, dessaturado, sai no Dia 0)
            var pos = new GameObject("Pós-processamento (Dia 0)");
            SceneManager.MoveGameObjectToScene(pos, cena);
            var volume = pos.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = PerfilDoDiaZero();

            EditorSceneManager.SaveScene(cena, Cena);
            PorNasBuildSettings();
            var resumo = rua.Resumo;
            EditorSceneManager.CloseScene(cena, true);
            PorNoMundo();
            Debug.Log($"[Dia 0] Cena montada em {Cena}. {resumo}");
        }

        const string Perfil = "Assets/_Game/Settings/DiaZero_Volume.asset";
        const string Tela = "Assets/_Game/Art/Materials/TelaDaTV.mat";

        // a tela da TV da introdução: o URP Unlit, com a imagem do estúdio (posta no jogo)
        static Material MaterialDaTela()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Tela);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "TelaDaTV" };
            m.SetColor("_BaseColor", new Color(1.15f, 1.15f, 1.15f));
            AssetDatabase.CreateAsset(m, Tela);
            AssetDatabase.SaveAssets();
            return m;
        }

        // o perfil do Dia 0: o ACES do jogo, a exposição e o contraste um pouco acima, as cores sem a dessaturação do mundo,
        // a vinheta e o granulado fracos e o brilho
        static UnityEngine.Rendering.VolumeProfile PerfilDoDiaZero()
        {
            var p = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(Perfil);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(p, Perfil);
            }
            foreach (var c in p.components.ToArray()) Object.DestroyImmediate(c, true);
            p.components.Clear();
            var tom = p.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tom.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.ACES;
            var cor = p.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            cor.postExposure.value = 0.3f;
            cor.contrast.value = 10f;
            cor.colorFilter.value = new Color(1f, 0.985f, 0.95f);
            cor.saturation.value = 6f;
            var vinheta = p.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vinheta.intensity.value = 0.14f;
            vinheta.smoothness.value = 0.5f;
            var grao = p.Add<UnityEngine.Rendering.Universal.FilmGrain>(true);
            grao.intensity.value = 0.06f;
            var brilho = p.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            brilho.threshold.value = 1.0f;
            brilho.intensity.value = 0.3f;
            brilho.scatter.value = 0.6f;
            foreach (var c in p.components) if (!AssetDatabase.IsSubAsset(c)) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void PorNasBuildSettings()
        {
            var lista = EditorBuildSettings.scenes.ToList();
            if (lista.Any(s => s.path == Cena)) return;
            lista.Add(new EditorBuildSettingsScene(Cena, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }

        /// <summary>O <see cref="DiaZeroDaPartida"/> no "Mundo" da cena do mundo (aberta, ou aberta ao lado), salvando a cena.</summary>
        [MenuItem("Zombie City Cleaner/Dia 0/Pôr o Dia 0 na cena do mundo")]
        public static void PorNoMundo()
        {
            var cena = SceneManager.GetSceneByPath(MontagemDoMundo.Cena);
            bool abriu = false;
            if (!cena.isLoaded)
            {
                cena = EditorSceneManager.OpenScene(MontagemDoMundo.Cena, OpenSceneMode.Additive);
                abriu = true;
            }
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("A cena do mundo não tem o objeto \"Mundo\".");
                return;
            }
            PorNoMundo(mundo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            if (abriu) EditorSceneManager.CloseScene(cena, true);
        }

        /// <summary>Põe o componente no objeto "Mundo" (a montagem da cena do mundo também chama).</summary>
        public static void PorNoMundo(GameObject mundo)
        {
            if (mundo.GetComponent<DiaZeroDaPartida>() == null) mundo.AddComponent<DiaZeroDaPartida>();
            EditorUtility.SetDirty(mundo);
        }
    }
}
