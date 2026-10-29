using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cena de teste da sinuca (a pedido em 29/10/2026: a mesa só na cena de teste, as bolas e os tacos só pelo F1):
    /// `Scenes/Exemplos/Sinuca.unity`, menu Zombie City Cleaner > Exemplos > Sinuca (teste). Refaz a cena do zero:
    ///  - um porão de 12 × 10 m (o chão de cimento, as paredes, o teto a 4 m e as luzes amarelas em cima da mesa), com uns
    ///    móveis da mobília nova encostados (as estantes de aço, o freezer e o acampamento);
    ///  - a mesa de sinuca no meio: o prefab da mobília nova (<c>MesaSinuca</c>, desempacotado, em ×1,30), com o
    ///    <see cref="MesaDeSinuca"/> e o NetworkObject na raiz, as 16 bolas dele sem colisor e o gatilho da mira em volta;
    ///  - a rede e o jogador de hoje (<see cref="MontagemDaRede.MontarCena"/>, os pontos de nascimento de frente para a
    ///    mesa) e os itens no chão (<see cref="MontagemDasArmas.MontarItensNoMundo"/>, sem itens no começo: o F1 cria a
    ///    caixa de bolas e os tacos).
    /// Rode antes a montagem da mobília nova (os prefabs) e a das armas (as malhas da sinuca no chão e os ícones).
    /// </summary>
    public static class MontagemDaSinuca
    {
        public const string Cena = "Assets/_Game/Scenes/Exemplos/Sinuca.unity";
        const string Pasta = "Assets/_Game/Art/Sinuca";
        const float EscalaDosMoveis = 1.3f;
        const float Largura = 12f, Fundo = 10f, Altura = 4f;

        [MenuItem("Zombie City Cleaner/Exemplos/Sinuca (teste)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena da sinuca.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(cena, Cena);
            System.IO.Directory.CreateDirectory(Pasta);

            // a luz de fora (fraca: é um porão) e o ambiente
            var sol = new GameObject("Luz de fora").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 0.25f;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.21f, 0.2f);
            RenderSettings.fog = false;

            // a câmera do menu (some quando o jogador local nasce), olhando a mesa
            var camera = new GameObject("Câmera do menu") { tag = "MainCamera" };
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2.6f, -4.2f), Quaternion.Euler(28f, 0f, 0f));
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.AddComponent<AudioListener>();

            Sala();
            var mesa = Mesa();
            Moveis();

            var jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            MontagemDaRede.MontarCena(jogador);
            // os pontos de nascimento: na frente da mesa (do lado comprido), virados para ela
            var pontos = cena.GetRootGameObjects().First(g => g.name == "Pontos de nascimento").transform;
            int i = 0;
            foreach (Transform p in pontos)
            {
                p.position = new Vector3(-1.6f + 1.1f * i, 0.05f, -2.4f - 0.6f * (i % 2));
                p.rotation = Quaternion.identity;
                i++;
            }
            MontagemDasArmas.MontarItensNoMundo(cena);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Selection.activeGameObject = mesa;
            Debug.Log($"Cena da sinuca montada: {Cena}. Dê Play, hospede local e pegue no F1 a caixa de bolas e um taco.");
        }

        // ------------------------------------------------------------------ a sala

        static Material Material(string nome, Color cor, float suavidade)
        {
            string caminho = $"{Pasta}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", suavidade);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Caixa(Transform pai, string nome, Vector3 centro, Vector3 tamanho, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nome;
            go.transform.SetParent(pai, false);
            go.transform.localPosition = centro;
            go.transform.localScale = tamanho;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;
        }

        static void Sala()
        {
            var sala = new GameObject("Porão").transform;
            var piso = Material("Sala_Piso", new Color(0.36f, 0.35f, 0.33f), 0.15f);
            var parede = Material("Sala_Parede", new Color(0.52f, 0.5f, 0.46f), 0.1f);
            var teto = Material("Sala_Teto", new Color(0.4f, 0.39f, 0.37f), 0.05f);
            float e = 0.2f;
            Caixa(sala, "Chão", new Vector3(0f, -0.1f, 0f), new Vector3(Largura + 2 * e, 0.2f, Fundo + 2 * e), piso);
            Caixa(sala, "Teto", new Vector3(0f, Altura + 0.1f, 0f), new Vector3(Largura + 2 * e, 0.2f, Fundo + 2 * e), teto);
            Caixa(sala, "Parede do fundo", new Vector3(0f, Altura / 2f, Fundo / 2f + e / 2f), new Vector3(Largura, Altura, e), parede);
            Caixa(sala, "Parede da frente", new Vector3(0f, Altura / 2f, -Fundo / 2f - e / 2f), new Vector3(Largura, Altura, e), parede);
            Caixa(sala, "Parede da esquerda", new Vector3(-Largura / 2f - e / 2f, Altura / 2f, 0f), new Vector3(e, Altura, Fundo), parede);
            Caixa(sala, "Parede da direita", new Vector3(Largura / 2f + e / 2f, Altura / 2f, 0f), new Vector3(e, Altura, Fundo), parede);
            // as luzes em cima da mesa (as lâmpadas amarelas de porão) e uma fraca em cada canto
            void Luz(string nome, Vector3 onde, float intensidade, float alcance, bool sombra)
            {
                var l = new GameObject(nome).AddComponent<Light>();
                l.transform.SetParent(sala, false);
                l.transform.localPosition = onde;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.86f, 0.62f);
                l.intensity = intensidade;
                l.range = alcance;
                l.shadows = sombra ? LightShadows.Soft : LightShadows.None;
            }
            Luz("Luz da mesa 1", new Vector3(-0.8f, 3.4f, 0f), 6f, 7f, true);
            Luz("Luz da mesa 2", new Vector3(0.8f, 3.4f, 0f), 6f, 7f, false);
            Luz("Luz do canto 1", new Vector3(-4.5f, 3.2f, 3.5f), 2.5f, 6f, false);
            Luz("Luz do canto 2", new Vector3(4.5f, 3.2f, -3.5f), 2.5f, 6f, false);
        }

        // ------------------------------------------------------------------ a mesa

        static GameObject Prefab(string modelo) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/MobiliaNova/{modelo}.prefab");

        static GameObject Mesa()
        {
            var prefab = Prefab("MesaSinuca");
            if (prefab == null) throw new System.Exception("Falta o prefab MesaSinuca: rode antes Zombie City Cleaner > Mobília nova > Montar.");
            var raiz = new GameObject("Mesa de sinuca");
            raiz.AddComponent<NetworkObject>();
            var mesa = raiz.AddComponent<MesaDeSinuca>();
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelo.name = "Mesa";
            modelo.transform.SetParent(raiz.transform, false);
            modelo.transform.localScale = Vector3.one * EscalaDosMoveis;
            mesa.mesa = modelo.transform;
            // as bolas: as filhas do modelo, na ordem do número, sem colisor (a mesa mexe nelas)
            mesa.bolas = new Transform[FisicaDaSinuca.Bolas];
            foreach (Transform filho in modelo.transform)
            {
                int k = filho.name.IndexOf(".Bola.");
                if (k < 0 || !int.TryParse(filho.name.Substring(k + 6), out int n) || n < 0 || n >= FisicaDaSinuca.Bolas) continue;
                mesa.bolas[n] = filho;
                foreach (var c in filho.GetComponents<Collider>()) Object.DestroyImmediate(c);
                filho.gameObject.SetActive(false);
            }
            if (mesa.bolas.Any(b => b == null)) Debug.LogWarning("A mesa de sinuca está sem alguma das 16 bolas (MesaSinuca.Bola.0 a .15).");
            // o gatilho da mira: um pouco maior que a mesa (a mira acha ele antes do colisor dela)
            var mira = new GameObject("Mira");
            mira.transform.SetParent(modelo.transform, false);
            var gatilho = mira.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = new Vector3(0f, 0.46f, 0f);
            gatilho.size = new Vector3(2.4f, 0.96f, 1.42f);
            mira.AddComponent<MesaDeSinucaNaMira>().mesa = mesa;
            // o taco que mira (as malhas dos 4 tacos, as do chão) e as linhas
            mesa.tacos = Enumerable.Range(0, Itens.QuantosDaSinuca).Select(k => Itens.DaSinuca(k)).Where(Itens.EhTaco)
                .Select(t => AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDaSinuca(Itens.IndiceDaSinuca(t)))).ToArray();
            if (mesa.tacos.Any(t => t == null)) Debug.LogWarning("Faltam as malhas dos tacos: rode a montagem das armas (Zombie City Cleaner > Montar armas).");
            mesa.materialDoTaco = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Characters/Jogador/Materials/Pistola.mat");
            mesa.materialDaLinha = MaterialDaLinha();
            return raiz;
        }

        static Material MaterialDaLinha()
        {
            string caminho = $"{Pasta}/LinhaDaMira.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.86f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // uns móveis do porão encostados nas paredes (só para ver), da mobília nova, em ×1,30
        static void Moveis()
        {
            var pai = new GameObject("Móveis do porão").transform;
            void Por(string modelo, Vector3 onde, float giro)
            {
                var prefab = Prefab(modelo);
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
                go.transform.SetPositionAndRotation(onde, Quaternion.Euler(0f, giro, 0f));
                go.transform.localScale = Vector3.one * EscalaDosMoveis;
            }
            // a frente de cada móvel é o +Z dele: os do fundo giram 180° (de costas para a parede)
            Por("EstanteAco120", new Vector3(-3.6f, 0f, Fundo / 2f - 0.3f), 180f);
            Por("EstanteAco90", new Vector3(-1.6f, 0f, Fundo / 2f - 0.3f), 180f);
            Por("FreezerHorizontal", new Vector3(1.4f, 0f, Fundo / 2f - 0.45f), 180f);
            Por("PrateleiraMadeira150", new Vector3(4f, 0f, Fundo / 2f - 0.3f), 180f);
            Por("Colchonete", new Vector3(-Largura / 2f + 0.7f, 0f, -1.5f), 90f);
            Por("SacoDeDormir", new Vector3(-Largura / 2f + 1.9f, 0f, -2.2f), 80f);
            Por("Latas", new Vector3(-Largura / 2f + 1.3f, 0f, 0.4f), 0f);
            Por("Fogareiro", new Vector3(-Largura / 2f + 2.3f, 0f, 0.6f), 30f);
            Por("Garrafas", new Vector3(-Largura / 2f + 1.6f, 0f, 1.4f), 0f);
        }
    }
}
