using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os especiais novos, para comparar com os de hoje (04/11/2026, a pedido): o NovoBrutamonte (~2,2 m) e o NovoCorredor
    /// (~2,23 m), refeitos no Blender. Eles chegam em Art/Characters/Zombie/Especiais/ como os especiais de hoje:
    /// &lt;Nome&gt;.fbx e &lt;Nome&gt;_BaseColor, _Normal e _MetalLiso.png, com o mesmo contrato (o esqueleto "&lt;Nome&gt;_Rig" com
    /// os ossos do Oco, root &gt; hips &gt; spine &gt; spine_02 &gt; chest &gt; neck &gt; head, os braços e as pernas; uma malha só,
    /// "&lt;Nome&gt;_Jogo", com um material; e, no Brutamonte, o olho e as duas pálpebras, filhos do chest, com a cabeça do
    /// osso no meio do olho e o repouso de olho aberto, olhando para as costas).
    /// <para>
    /// Menu Zombie City Cleaner > Zumbis > Montar os zumbis novos (comparação): cada um passa pelos mesmos passos de um
    /// especial em <see cref="MontarOsEspeciais"/> (Humanoid → os 20 clipes gravados no esqueleto dele e o avatar Generic;
    /// o override do Zombie.controller com esses clipes; o prefab, com o tipo, o olho e a hitbox dele; e o corpo que cai), e
    /// saem Prefabs/Zumbis/Zumbi_&lt;Nome&gt; e Corpo_&lt;Nome&gt;. Mas eles ficam FORA do jogo: não entram em
    /// <see cref="TodosOsModelos"/> nem na lista do ZombieManager, e nada aqui abre ou salva a cena Mundo, refaz o
    /// Zombie.controller ou mexe nos outros modelos. As fontes Humanoid dos clipes (as animações do Oco, a agonia, o levantar
    /// e as dos especiais) são só lidas, como estão (a montagem dos especiais reimporta e refaz), e só os assets que esta
    /// montagem sujou são salvos (nada de AssetDatabase.SaveAssets: as mudanças em aberto de outro trabalho ficam como estão).
    /// Faltando o FBX ou uma textura, aquele modelo é pulado com o erro no console. No fim, a cena de comparação é refeita.
    /// </para>
    /// <para>
    /// Menu Zombie City Cleaner > Zumbis > Montar a cena de comparação (atual x novo): Scenes/Exemplos/Comparacao_Zumbis.unity
    /// (fora das Build Settings), refeita do zero: o chão, o sol, a luz de trás e o ambiente, a câmera, e duas fileiras, o
    /// Brutamonte (V1, V2, V3 e o novo) e o Corredor (V1, V2, V3 e o novo), com os rótulos e uma régua de 2,5 m entre o V1 e o
    /// novo. O painel do Play é o <see cref="ComparacaoDeZumbis"/>. Um prefab que falta é pulado (o do novo deixa o lugar
    /// vazio, com o rótulo). A montagem é feita numa cena à parte: as cenas abertas ficam como estão (nada é fechado nem
    /// salvo nelas, sem pergunta), e só a de comparação é salva. Ela não abre sozinha: fica marcada no Project (dois cliques
    /// abrem). Com uma cena sem nome aberta, não monta (avisa).
    /// </para>
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        // os novos: o nome (o do FBX e das texturas em Especiais/) e o tipo. Fora de TodosOsModelos: só para a comparação
        static readonly (string nome, TipoDeZumbi tipo)[] Novos =
        {
            ("NovoBrutamonte", TipoDeZumbi.Brutamonte), ("NovoCorredor", TipoDeZumbi.Corredor),
        };

        static bool EhNovo(string nome) => Novos.Any(e => e.nome == nome);

        // os ossos que a montagem usa (as hitboxes, o corpo que cai, a cabeça separada e o Humanoid)
        static readonly string[] OssosDoContrato =
        {
            "root", "hips", "spine", "spine_02", "chest", "neck", "head",
            "shoulder.L", "upper_arm.L", "forearm.L", "hand.L", "shoulder.R", "upper_arm.R", "forearm.R", "hand.R",
            "thigh.L", "shin.L", "foot.L", "toe.L", "thigh.R", "shin.R", "foot.R", "toe.R",
        };
        static readonly string[] OssosDoOlho = { "olho", "palpebra_sup", "palpebra_inf" };

        [MenuItem("Zombie City Cleaner/Zumbis/Montar os zumbis novos (comparação)")]
        public static void MontarOsZumbisNovos()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os zumbis novos.");
                return;
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null)
            {
                Debug.LogError($"Zumbis novos: falta o controlador dos zumbis ({Controlador}); monte os zumbis uma vez.");
                return;
            }
            // os clipes do primeiro modelo são os do Zombie.controller: o override troca cada um pelo do novo
            var doPrimeiro = ClipesGravados(Modelos[0]);
            if (doPrimeiro.Count == 0)
            {
                Debug.LogError($"Zumbis novos: faltam os clipes gravados do {Modelos[0]} ({ClipesDosOcos}/{Modelos[0]}); monte os zumbis uma vez.");
                return;
            }
            // as fontes Humanoid (as animações do Oco, a agonia, o levantar e as dos especiais) só lidas, uma vez para os dois:
            // a montagem dos especiais reimporta os FBX delas e refaz a agonia; aqui, nada do que é dos outros é regravado
            Dictionary<string, AnimationClip> fontes;
            try { fontes = ClipesHumanoidesSoLidos(); }
            catch (System.Exception e)
            {
                Debug.LogError($"Zumbis novos: {e.Message} Nenhum foi montado.");
                return;
            }
            CamadasDosZumbis();
            var primeiro = new Modelo { indice = 0, nome = Modelos[0], fbx = FbxDe(Modelos[0]), clipes = doPrimeiro };
            int depoisDaLista = TodosOsModelos.Count();   // o índice dos novos: depois da lista do jogo (eles não entram nela)
            var feitos = new List<string>();
            var sujosAntes = AssetsSujos();   // as mudanças não salvas de antes (de outras mãos) ficam como estão
            _fontesSoLidas = fontes;
            try
            {
                for (int k = 0; k < Novos.Length; k++)
                {
                    var (nome, tipo) = Novos[k];
                    var faltas = FaltasDoNovo(nome);
                    if (faltas.Count > 0)
                    {
                        Debug.LogError($"Zumbi novo {nome}: faltam arquivos (exportados do Blender): {string.Join(", ", faltas)}. Ele não foi montado.");
                        continue;
                    }
                    try
                    {
                        ConferirOFbx(nome, tipo);
                        var m = ImportarModelo(nome, depoisDaLista + k);   // (EhEspecial: montado como os Ocos e os especiais)
                        var aoc = Sobrepor(ctrl, primeiro, m);
                        var z = MontarPrefab(m, aoc);
                        MontarCorpo(m);
                        ConferirOPrefab(z, m, tipo);
                        feitos.Add(nome);
                        Debug.Log($"Zumbi novo montado: {nome} ({tipo}, {m.altura:F2} m, {m.clipes.Count} clipes) → {Prefabs}/Zumbi_{nome}.prefab e " +
                                  $"Corpo_{nome}.prefab (os clipes em {ClipesDosOcos}/{nome}/, o controlador {Arte}/Zombie_{nome}.overrideController). " +
                                  "Fora da lista do jogo.");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Zumbi novo {nome}: a montagem parou: {e.Message}");
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                _fontesSoLidas = null;
                SalvarOsSujosDaMontagem(sujosAntes);
                // recarregar o que foi refeito (já salvo). O Salvar copia a malha nova por cima da velha (CopySerialized): com
                // outro número de vértices, a cópia da placa de vídeo ficava a velha e o renderer parava ("does not match the
                // expected mesh data size and vertex stride"), e reimportar não resolve; descarregada, ela volta do disco. E o
                // Sobrepor apaga e recria o override: se o prefab sai igual (o mesmo GUID), a Unity não regrava o arquivo e a
                // cópia carregada fica apontando para o override apagado (o Animator sem controlador); reimportar recarrega
                foreach (var nome in feitos)
                {
                    foreach (var s in new[] { "Original", "Inteira", "SemCabeca" })
                    {
                        var malha = AssetDatabase.LoadAssetAtPath<Mesh>($"{Malhas}/{nome}_{s}.asset");
                        if (malha != null && !EditorUtility.IsDirty(malha)) Resources.UnloadAsset(malha);
                    }
                    foreach (var p in new[] { $"{Prefabs}/Zumbi_{nome}.prefab", $"{Prefabs}/Corpo_{nome}.prefab" })
                        if (File.Exists(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            if (feitos.Count == 0)
            {
                Debug.LogError("Zumbis novos: nenhum foi montado (veja os erros acima).");
                return;
            }
            Debug.Log($"Zumbis novos montados: {string.Join(", ", feitos)} (de {Novos.Length}). Refazendo a cena de comparação.");
            MontarCenaDeComparacao();
        }

        /// <summary>
        /// Com elas, a gravação dos clipes (<see cref="GravarClipes"/>) usa estas fontes em vez de reimportar as Humanoid, e
        /// o <see cref="Gravar"/> não salva o projeto inteiro (a montagem dos novos salva só o que ela mudou). Nulas fora dela.
        /// </summary>
        static Dictionary<string, AnimationClip> _fontesSoLidas;

        /// <summary>
        /// Os clipes Humanoid que a montagem dos especiais usa (<see cref="ClipesHumanoides"/>), como já estão no projeto: sem
        /// mudar a importação dos FBX nem refazer os clipes da agonia. Faltando algum, para (monte os zumbis uma vez).
        /// </summary>
        static Dictionary<string, AnimationClip> ClipesHumanoidesSoLidos()
        {
            var clipes = new Dictionary<string, AnimationClip>();
            void DoFbx(string fbx, (string nome, bool loop)[] lista, bool exigido)
            {
                if (!File.Exists(fbx))
                {
                    if (exigido) throw new FileNotFoundException($"falta {fbx} (as animações Humanoid dos zumbis).", fbx);
                    return;   // (o levantar e as dos especiais são opcionais, como na montagem dos especiais)
                }
                if (!(AssetImporter.GetAtPath(fbx) is ModelImporter imp) || imp.animationType != ModelImporterAnimationType.Human)
                    throw new System.Exception($"{fbx} não está importado como Humanoid (monte os zumbis especiais uma vez).");
                var achados = new Dictionary<string, AnimationClip>();
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview")) achados[c.name] = c;
                foreach (var (nome, _) in lista)
                {
                    if (!achados.TryGetValue(nome, out var c))
                        throw new System.Exception($"{fbx} não tem o clipe {nome} (monte os zumbis especiais uma vez).");
                    clipes[nome] = c;
                }
            }
            DoFbx(AnimacoesHumanoides, Clipes, true);
            DoFbx(AnimacoesHumanoidesExtra, ClipesExtra, true);
            foreach (var (nome, _) in ClipesDaAgonia)
            {
                string caminho = $"{PastaDaAgonia}/{nome}.anim";
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
                if (c == null) throw new FileNotFoundException($"falta {caminho} (a agonia; monte os zumbis especiais uma vez).", caminho);
                clipes[nome] = c;
            }
            DoFbx(AnimacoesHumanoidesDoEquilibrio, ClipesDoEquilibrio, false);
            DoFbx(AnimacoesHumanoidesEspeciais, ClipesEspeciais, false);
            if (ClipesEspeciais.Any(c => !clipes.ContainsKey(c.nome)))
                Debug.LogWarning($"Zumbis novos: sem {AnimacoesHumanoidesEspeciais}, os novos saem sem o Correr, o Emergir, a Fúria, o Cuspir e o Gritar.");
            return clipes;
        }

        /// <summary>Os assets do projeto com mudanças não salvas agora.</summary>
        static HashSet<Object> AssetsSujos()
        {
            var sujos = new HashSet<Object>();
            foreach (var o in Resources.FindObjectsOfTypeAll<Object>())
                if (EditorUtility.IsDirty(o) && EditorUtility.IsPersistent(o)) sujos.Add(o);
            return sujos;
        }

        /// <summary>
        /// Salva só os assets (em Assets/) que a montagem dos novos sujou: os clipes, o avatar, as malhas, o material e o
        /// controlador deles. O que já estava sujo antes (as mudanças de outro trabalho em aberto) não é salvo aqui.
        /// </summary>
        static void SalvarOsSujosDaMontagem(HashSet<Object> antes)
        {
            int salvos = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll<Object>())
            {
                if (!EditorUtility.IsDirty(o) || !EditorUtility.IsPersistent(o) || antes.Contains(o)) continue;
                string caminho = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(caminho) || !caminho.StartsWith("Assets/")) continue;
                AssetDatabase.SaveAssetIfDirty(o);
                salvos++;
            }
            if (salvos > 0) Debug.Log($"Zumbis novos: {salvos} assets salvos (só os da montagem).");
        }

        /// <summary>O FBX e as três texturas do novo (importados aqui, se o Blender acabou de escrever e a Unity ainda não viu).</summary>
        static List<string> FaltasDoNovo(string nome)
        {
            var faltas = new List<string>();
            foreach (var arquivo in new[]
                     {
                         $"{PastaEspeciais}/{nome}.fbx", $"{PastaEspeciais}/{nome}_BaseColor.png",
                         $"{PastaEspeciais}/{nome}_Normal.png", $"{PastaEspeciais}/{nome}_MetalLiso.png",
                     })
            {
                if (!File.Exists(arquivo))
                {
                    faltas.Add(arquivo);
                    continue;
                }
                if (AssetImporter.GetAtPath(arquivo) == null) AssetDatabase.ImportAsset(arquivo, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(arquivo) == null) faltas.Add(arquivo + " (a Unity não importou)");
            }
            return faltas;
        }

        /// <summary>O contrato do Blender, antes de montar: os ossos (sem eles, para), a malha e o esqueleto (avisos).</summary>
        static void ConferirOFbx(string nome, TipoDeZumbi tipo)
        {
            var fbx = Carregar<GameObject>($"{PastaEspeciais}/{nome}.fbx");
            var ossos = fbx.GetComponentsInChildren<Transform>(true);
            Transform Osso(string n) => ossos.FirstOrDefault(t => t.name == n);
            var faltam = OssosDoContrato.Where(n => Osso(n) == null).ToList();
            if (faltam.Count > 0) throw new System.Exception($"o esqueleto não tem os ossos {string.Join(", ", faltam)} (os nomes do Oco).");
            if (Osso($"{nome}_Rig") == null) Debug.LogWarning($"Zumbi novo {nome}: o esqueleto não se chama {nome}_Rig (os clipes gravados usam o caminho dele; segue assim mesmo).");
            var malhas = fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (malhas.Length != 1 || malhas[0].name != $"{nome}_Jogo")
                Debug.LogWarning($"Zumbi novo {nome}: esperava uma malha só, {nome}_Jogo; veio {string.Join(", ", malhas.Select(s => s.name))}.");
            if (malhas.Length > 0 && malhas[0].sharedMaterials.Length != 1)
                Debug.LogWarning($"Zumbi novo {nome}: esperava um material só; veio {malhas[0].sharedMaterials.Length} (a montagem junta tudo num material).");
            if (tipo != TipoDeZumbi.Brutamonte) return;
            var semOlho = OssosDoOlho.Where(n => Osso(n) == null).ToList();
            if (semOlho.Count > 0)
            {
                Debug.LogError($"Zumbi novo {nome}: o Brutamonte veio sem os ossos {string.Join(", ", semOlho)}; o prefab sai sem o olho.");
                return;
            }
            foreach (var n in OssosDoOlho)
            {
                var o = Osso(n);
                if (o.parent == null || o.parent.name != "chest")
                    Debug.LogWarning($"Zumbi novo {nome}: o osso {n} é filho de {(o.parent != null ? o.parent.name : "ninguém")}, não do chest.");
                if (malhas.Length > 0 && !malhas[0].bones.Contains(o))
                    Debug.LogWarning($"Zumbi novo {nome}: o osso {n} não está na malha (sem peso); o repouso do olho vai sair da pose atual, não da bindpose.");
            }
        }

        /// <summary>Depois de montar: o tipo, o olho e a altura (a malha do Blender tem o z para cima, como os especiais).</summary>
        static void ConferirOPrefab(Zumbi z, Modelo m, TipoDeZumbi tipo)
        {
            if (z == null) throw new System.Exception("o prefab não foi salvo.");
            if (z.tipo != tipo) Debug.LogError($"Zumbi novo {m.nome}: o prefab saiu com o tipo {z.tipo}, não {tipo}.");
            if (tipo == TipoDeZumbi.Brutamonte && (z.olho == null || z.palpebraSup == null || z.palpebraInf == null || z.hitboxDoOlho == null))
                Debug.LogError($"Zumbi novo {m.nome}: o prefab saiu sem o olho, as pálpebras ou a hitbox do olho.");
            if (m.altura < 1.7f || m.altura > 2.8f)
                Debug.LogWarning($"Zumbi novo {m.nome}: a altura saiu {m.altura:F2} m (esperava ~2,2 m). O FBX foi exportado como os especiais (o z da malha para cima, sem aplicar a conversão de eixos)?");
        }

        // ------------------------------------------------------------------ a cena de comparação

        const string CenaDeComparacao = "Assets/_Game/Scenes/Exemplos/Comparacao_Zumbis.unity";
        const string PastaDaComparacao = "Assets/_Game/Scenes/Exemplos/Comparacao_Zumbis";
        const float EntreModelos = 2.3f, EntreFileiras = 8f;

        // as fileiras: o nome, o novo e os atuais (o V1 primeiro: fica ao lado do novo, com a régua entre os dois)
        static readonly (string nome, string novo, string[] atuais)[] FileirasDaComparacao =
        {
            ("Brutamonte", "NovoBrutamonte", new[] { "Brutamonte_V1", "Brutamonte_V2", "Brutamonte_V3" }),
            ("Corredor", "NovoCorredor", new[] { "Corredor_V1", "Corredor_V2", "Corredor_V3" }),
        };

        [MenuItem("Zombie City Cleaner/Zumbis/Montar a cena de comparação (atual x novo)")]
        public static void MontarACenaDeComparacao()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a cena de comparação.");
                return;
            }
            MontarCenaDeComparacao();
        }

        /// <summary>
        /// Refaz a Comparacao_Zumbis numa cena à parte (aditiva): as cenas abertas ficam como estão (nada é fechado, trocado
        /// ou salvo nelas, nem com pergunta) e só ela é salva. Ela não abre sozinha: o caminho fica no console e o arquivo
        /// marcado no Project (abrir com dois cliques).
        /// </summary>
        public static void MontarCenaDeComparacao()
        {
            // uma cena sem nome aberta impede a cena à parte (a Unity só deixa uma sem nome): aí não monta, em vez de
            // substituir as abertas
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                Debug.LogWarning("Cena de comparação: não montada, porque há uma cena sem nome aberta (a Unity não abre outra sem " +
                                 "nome ao lado). Salve ou feche essa cena e rode Zumbis > Montar a cena de comparação (atual x novo).");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PastaDaComparacao)) AssetDatabase.CreateFolder(Path.GetDirectoryName(PastaDaComparacao).Replace('\\', '/'), Path.GetFileName(PastaDaComparacao));
            var antes = SceneManager.GetActiveScene();
            var nova = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            // a de antes, se está aberta, sai sem salvar (é refeita inteira)
            var velha = SceneManager.GetSceneByPath(CenaDeComparacao);
            if (velha.IsValid())
            {
                if (antes == velha) antes = nova;
                EditorSceneManager.CloseScene(velha, true);
            }
            int postos;
            bool salva = false;
            try
            {
                SceneManager.SetActiveScene(nova);   // (os objetos novos e o RenderSettings vão para a cena ativa)
                postos = PreencherACenaDeComparacao(nova);
                EditorSceneManager.MarkSceneDirty(nova);
                if (!EditorSceneManager.SaveScene(nova, CenaDeComparacao)) throw new IOException($"Não consegui salvar {CenaDeComparacao}.");
                salva = true;
            }
            finally
            {
                if (antes.IsValid() && antes.isLoaded && antes != nova) SceneManager.SetActiveScene(antes);
                // deu erro no meio: a cena pela metade sai (sem nome, ela travaria a próxima montagem). Se ela era a única
                // aberta (no lugar da comparação velha), a velha volta, como estava no disco
                if (!salva && nova.IsValid())
                {
                    if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(nova, true);
                    else if (File.Exists(CenaDeComparacao)) EditorSceneManager.OpenScene(CenaDeComparacao, OpenSceneMode.Single);
                }
            }
            // aberta só se ela substituiu a única cena aberta (a própria comparação); senão, sai e as abertas ficam como estavam
            bool unica = SceneManager.sceneCount == 1;
            if (!unica) EditorSceneManager.CloseScene(nova, true);
            var arquivo = AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaDeComparacao);
            if (arquivo != null) EditorGUIUtility.PingObject(arquivo);
            Debug.Log($"Cena de comparação montada: {CenaDeComparacao} ({postos} modelos; fora das Build Settings)" +
                      (unica ? "; ela está aberta." : "; abra-a pelo Project (as cenas abertas não foram mexidas).") +
                      " No Play, o painel à esquerda (H esconde) anima todos juntos.");
        }

        static int PreencherACenaDeComparacao(Scene cena)
        {
            // a luz: o sol pela frente e de cima, a luz de trás (o olho do Brutamonte fica nas costas) e o ambiente de três
            // cores, sem céu nem neblina (os modelos lidos como são)
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.5f;
            sol.color = new Color(1f, 0.96f, 0.9f);
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.75f;
            sol.transform.rotation = Quaternion.Euler(45f, 160f, 0f);
            var tras = new GameObject("Luz de trás").AddComponent<Light>();
            tras.type = LightType.Directional;
            tras.intensity = 0.8f;
            tras.color = new Color(0.8f, 0.86f, 1f);
            tras.shadows = LightShadows.None;
            tras.transform.rotation = Quaternion.Euler(25f, -15f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.5f, 0.53f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.36f, 0.37f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.18f);
            RenderSettings.fog = false;

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chão";
            chao.transform.position = new Vector3(EntreModelos, 0f, -EntreFileiras * 0.5f);
            chao.transform.localScale = new Vector3(5f, 1f, 4f);   // 50 × 40 m
            chao.GetComponent<MeshRenderer>().sharedMaterial = MaterialDaComparacao("Chao", new Color(0.2f, 0.2f, 0.21f), 0.05f);

            var camera = new GameObject("Câmera") { tag = "MainCamera" };
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.3f, 6.5f), Quaternion.Euler(4f, 180f, 0f));
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.03f;   // o olho de perto
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.08f, 0.09f);
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.AddComponent<AudioListener>();

            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mRegua = MaterialDaComparacao("Regua", new Color(0.9f, 0.82f, 0.45f), 0.2f);
            var comparacao = new GameObject("Comparação").AddComponent<ComparacaoDeZumbis>();
            comparacao.cameraDaCena = cam;
            var fileiras = new List<ComparacaoDeZumbis.Fileira>();
            int postos = 0;
            for (int f = 0; f < FileirasDaComparacao.Length; f++)
            {
                var (nome, novo, atuais) = FileirasDaComparacao[f];
                var raiz = new GameObject($"Fileira {nome}").transform;
                raiz.position = new Vector3(0f, 0f, -f * EntreFileiras);
                var rotulos = new List<Transform>();
                // o novo à direita da régua (vendo de frente); os atuais à esquerda, o V1 colado nela
                var oNovo = PorNaComparacao(cena, raiz, novo, $"{nome} NOVO", new Vector3(-EntreModelos * 0.6f, 0f, 0f), fonte, rotulos, ref postos);
                var osAtuais = new List<Transform>();
                for (int i = 0; i < atuais.Length; i++)
                {
                    string versao = atuais[i].Substring(atuais[i].LastIndexOf('_') + 1);
                    var t = PorNaComparacao(cena, raiz, atuais[i], $"{nome} atual ({versao})", new Vector3(EntreModelos * (0.6f + i), 0f, 0f), fonte, rotulos, ref postos);
                    if (t != null) osAtuais.Add(t);
                }
                Regua(raiz, Vector3.zero, mRegua, fonte, rotulos);
                fileiras.Add(new ComparacaoDeZumbis.Fileira { nome = nome, raiz = raiz, novo = oNovo, atuais = osAtuais.ToArray(), rotulos = rotulos.ToArray() });
            }
            comparacao.fileiras = fileiras.ToArray();
            return postos;
        }

        /// <summary>
        /// Um zumbi na fileira (o prefab do jogo, com o nome do modelo) e o rótulo dele, acima da cabeça. Sem o prefab: os
        /// atuais são pulados; o novo deixa o lugar vazio, com o rótulo dizendo que falta.
        /// </summary>
        static Transform PorNaComparacao(Scene cena, Transform fileira, string modelo, string titulo, Vector3 lugar, Font fonte,
                                         List<Transform> rotulos, ref int postos)
        {
            string caminho = $"{Prefabs}/Zumbi_{modelo}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            float altura = 2.2f;
            Transform t;
            string texto;
            if (prefab == null)
            {
                Debug.LogWarning($"Cena de comparação: falta {caminho}" +
                                 (EhNovo(modelo) ? "; o lugar dele fica vazio (Zumbis > Montar os zumbis novos (comparação))." : "; ficou de fora."));
                if (!EhNovo(modelo)) return null;
                t = new GameObject($"{modelo} (falta o prefab)").transform;
                t.SetParent(fileira, false);
                t.localPosition = lugar;
                texto = $"{titulo}\n(falta o prefab)";
            }
            else
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                go.name = modelo;
                t = go.transform;
                t.SetParent(fileira, false);
                t.SetLocalPositionAndRotation(lugar, Quaternion.identity);
                var capsula = go.GetComponent<CapsuleCollider>();
                if (capsula != null) altura = capsula.height;
                texto = $"{titulo}\n{altura:0.00} m";
                postos++;
            }
            rotulos.Add(Rotulo(fileira, texto, lugar + Vector3.up * (altura + 0.2f), 0.13f, fonte,
                               EhNovo(modelo) ? new Color(1f, 0.85f, 0.4f) : Color.white));
            return t;
        }

        /// <summary>Um texto 3D (TextMesh, a fonte da Unity) de frente para a câmera da frente; no Play, ele vira para a câmera.</summary>
        static Transform Rotulo(Transform pai, string texto, Vector3 lugar, float letra, Font fonte, Color cor)
        {
            var go = new GameObject("Rótulo");
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(lugar, Quaternion.Euler(0f, 180f, 0f));
            var tm = go.AddComponent<TextMesh>();
            tm.font = fonte;
            tm.fontSize = 64;
            tm.characterSize = letra / 7.4f;   // (1 m para cada 10 pixels da fonte no characterSize 1: a linha fica com ~'letra' m)
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.richText = false;
            tm.color = cor;
            tm.text = texto;
            go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;
            return go.transform;
        }

        /// <summary>A régua: uma barra de 2,5 m com uma marca a cada 25 cm (maiores no meio metro e no metro) e os números.</summary>
        static void Regua(Transform pai, Vector3 lugar, Material material, Font fonte, List<Transform> rotulos)
        {
            var regua = new GameObject("Régua (2,5 m)").transform;
            regua.SetParent(pai, false);
            regua.localPosition = lugar;
            Peca(regua, new Vector3(0f, 1.25f, 0f), new Vector3(0.025f, 2.5f, 0.025f), material);
            for (int i = 1; i <= 10; i++)
            {
                float y = i * 0.25f;
                float largura = i % 4 == 0 ? 0.32f : i % 2 == 0 ? 0.2f : 0.1f;
                Peca(regua, new Vector3(0f, y, 0f), new Vector3(largura, 0.012f, 0.012f), material);
                if (i % 2 == 0) rotulos.Add(Rotulo(regua, $"{y:0.0} m", new Vector3(0f, y + 0.015f, 0f), 0.07f, fonte, new Color(0.95f, 0.88f, 0.55f)));
            }
        }

        static void Peca(Transform pai, Vector3 lugar, Vector3 tamanho, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.name = "Marca";
            go.transform.SetParent(pai, false);
            go.transform.localPosition = lugar;
            go.transform.localScale = tamanho;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material MaterialDaComparacao(string nome, Color cor, float lisura)
        {
            string caminho = $"{PastaDaComparacao}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            bool novo = m == null;
            if (novo) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", lisura);
            // (as cores antes do CreateAsset, que grava na hora; o que já existia é salvo só ele, sem o SaveAssets do projeto)
            if (novo) AssetDatabase.CreateAsset(m, caminho);
            else
            {
                EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssetIfDirty(m);
            }
            return m;
        }
    }
}
