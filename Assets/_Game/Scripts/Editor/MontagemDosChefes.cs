using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os chefes do BUNKER: Outbreak (14/11/2026), feitos no Blender em "D:\teste blender\Chefes" (fora do repositório):
    /// O Blindado (que se divide nos Agregados Médios A e B), O Alongado (que vira O Longo) e O Condutor. Cada um é uma
    /// fusão de vários Ocos, low-poly facetado, com uma malha só, um atlas 4096 (cor, normal e rugosidade) e um esqueleto
    /// próprio (não o do Oco: coluna, pescoço, braços em segmentos que esticam, dedos, mandíbula, e os ossos de cada Oco
    /// fundido), então eles entram como Generic, sem as animações dos zumbis.
    /// <para>
    /// Chegam em Art/Characters/Chefes/&lt;Nome&gt;/: &lt;Nome&gt;.fbx e Texturas/&lt;Nome&gt;_BaseColor, _Normal e _Roughness.png
    /// (o _MetalLiso, o mapa de metal e lisura da URP, é feito aqui da rugosidade).
    /// </para>
    /// <para>
    /// Menu Zombie City Cleaner > Chefes > Montar os chefes (vitrine): para cada um, a importação (Generic, as normais do
    /// Blender para o sombreamento plano), o material (URP Lit) e o prefab Prefabs/Chefes/Chefe_&lt;Nome&gt; (o modelo com o
    /// Animator, o avatar Generic e o controlador das animações dele, &lt;Nome&gt;.controller, e sem nada de jogo: nem IA, nem
    /// vida, nem colisor). As animações vêm no FBX (as ações que o chefe_anim.py grava no Blender): Parado, Andar, Dano,
    /// a morte (Morte ou, no Blindado, Dividir) e as habilidades de cada um (Velocidade = Parado/Andar; um trigger por
    /// habilidade; Investida e Frenesi são bools; Reiniciar volta ao Parado). No fim, a vitrine
    /// Scenes/Exemplos/Chefes.unity (fora das Build Settings) é refeita: os seis lado a lado, com um Oco de 1,8 m para a
    /// escala e os rótulos. Nada entra no jogo (nem nas listas de zumbis, nem no ZombieManager). Só os assets desta montagem
    /// são salvos (as mudanças em aberto de outro trabalho ficam como estão), e as cenas abertas não são mexidas.
    /// </para>
    /// </summary>
    public static partial class MontagemDosChefes
    {
        const string Arte = "Assets/_Game/Art/Characters/Chefes";
        const string PastaDosPrefabs = "Assets/_Game/Prefabs/Chefes";
        const string Cena = "Assets/_Game/Scenes/Exemplos/Chefes.unity";
        const string PastaDaCena = "Assets/_Game/Scenes/Exemplos/Chefes";
        const string OcoDaEscala = "Assets/_Game/Prefabs/Zumbis/Zumbi_Oco_Original.prefab";
        const float EntreChefes = 2.5f;

        // o nome (o do FBX, das texturas e da pasta) e o título do rótulo, na ordem da vitrine
        static readonly (string nome, string titulo)[] Chefes =
        {
            ("O_Blindado", "O Blindado"), ("Agregado_Medio_A", "Agregado Médio A"), ("Agregado_Medio_B", "Agregado Médio B"),
            ("O_Alongado", "O Alongado"), ("O_Longo", "O Longo"), ("O_Condutor", "O Condutor"),
        };

        // a linha de cada um no painel da vitrine
        static readonly Dictionary<string, string> Descricao = new Dictionary<string, string>
        {
            ["O_Blindado"] = "Fusão de vários Ocos: ombros assimétricos, a mão de agarrão, placas de calo. Divide-se nos Agregados A e B.",
            ["Agregado_Medio_A"] = "Metade do Blindado: curvado, um segundo tronco no ombro, a mão de carne que estica (direita).",
            ["Agregado_Medio_B"] = "A outra metade: alto e ereto, a mão de carne que estica (esquerda).",
            ["O_Alongado"] = "Braços humanos esticados (o esquerdo com um cotovelo a mais), membros extras nas costas.",
            ["O_Longo"] = "O que sobra do Alongado: agachado, pescoço em S, a boca de ácido; fundido com outros Ocos e Longs.",
            ["O_Condutor"] = "A torre de corpos, dezenas de braços e o único rosto humano (no peito, gritando).",
        };

        [MenuItem("Zombie City Cleaner/Chefes/Montar os chefes (vitrine)")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os chefes.");
                return;
            }
            var feitos = new List<string>();
            foreach (var (nome, titulo) in Chefes)
            {
                try
                {
                    if (MontarUm(nome)) feitos.Add(nome);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Chefe {nome}: a montagem parou: {e.Message}");
                    Debug.LogException(e);
                }
            }
            // recarregar os prefabs: se o conteúdo saiu igual, a Unity não regrava o arquivo e a cópia carregada podia ficar
            // com a referência velha (o Animator sem controlador)
            foreach (var n in feitos)
                AssetDatabase.ImportAsset($"{PastaDosPrefabs}/Chefe_{n}.prefab", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Chefes montados: {feitos.Count} de {Chefes.Length} ({string.Join(", ", feitos)}) → {PastaDosPrefabs}/Chefe_<Nome>.prefab. Refazendo a vitrine.");
            if (feitos.Count > 0) MontarAVitrine();
        }

        [MenuItem("Zombie City Cleaner/Chefes/Montar a vitrine dos chefes")]
        public static void MontarSoAVitrine()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a vitrine dos chefes.");
                return;
            }
            MontarAVitrine();
        }

        // ------------------------------------------------------------------ um chefe

        static bool MontarUm(string nome)
        {
            string pasta = $"{Arte}/{nome}";
            string fbx = $"{pasta}/{nome}.fbx";
            string tex = $"{pasta}/Texturas/{nome}";
            var faltas = new[] { fbx, tex + "_BaseColor.png", tex + "_Normal.png", tex + "_Roughness.png" }.Where(f => !File.Exists(f)).ToList();
            if (faltas.Count > 0)
            {
                Debug.LogError($"Chefe {nome}: faltam arquivos (exportados do Blender): {string.Join(", ", faltas)}. Ele não foi montado.");
                return false;
            }
            var avatar = ImportarModelo(fbx);
            var controlador = MontarControlador(pasta, nome, fbx, out var nomesDosClipes);
            var mat = MontarMaterial(pasta, nome);
            var prefab = MontarPrefab(nome, fbx, avatar, mat, controlador, out float altura, out int tris, out int ossos);
            Debug.Log($"Chefe montado: {nome} ({altura:F2} m, {tris} triângulos, {ossos} ossos, {nomesDosClipes.Count} animações: " +
                      $"{string.Join(", ", nomesDosClipes)}) → {AssetDatabase.GetAssetPath(prefab)}");
            return true;
        }

        // ------------------------------------------------------------------ as animações

        // os clipes em loop; os outros tocam uma vez e voltam ao Parado (as mortes ficam no fim)
        static readonly HashSet<string> EmLoop = new HashSet<string> { "Parado", "Andar", "Investida", "Frenesi" };
        static readonly HashSet<string> Mortes = new HashSet<string> { "Morte", "Dividir" };
        // os loops especiais (não o Parado e o Andar) entram e saem por um bool com o nome deles
        static bool EhLoopEspecial(string c) => EmLoop.Contains(c) && c != "Parado" && c != "Andar";

        /// <summary>
        /// Os clipes do FBX (as ações do Blender, "&lt;Nome&gt;_Rig|&lt;Clipe&gt;") com o nome curto e o loop certo.
        /// </summary>
        static void ClipesDoFbx(ModelImporter imp)
        {
            var clipes = imp.defaultClipAnimations;
            foreach (var c in clipes)
            {
                string curto = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.name = curto;
                c.loopTime = EmLoop.Contains(curto);
                c.loopPose = false;
                c.lockRootRotation = false;
                c.lockRootHeightY = false;
                c.lockRootPositionXZ = false;
            }
            imp.clipAnimations = clipes;
        }

        /// <summary>
        /// O Animator Controller do chefe (refeito do zero em &lt;Nome&gt;.controller, ao lado do FBX):
        ///  - "Velocidade" (float): Parado ↔ Andar;
        ///  - cada ação de uma vez (Golpe, Agarrar, Cuspir, ...) é um trigger com o nome dela, de qualquer estado, e volta ao
        ///    Parado no fim; as mortes (Morte, Dividir) ficam paradas no último quadro;
        ///  - os loops especiais (Investida, Frenesi) são bools com o nome deles;
        ///  - "Reiniciar" (trigger) volta ao Parado de qualquer estado (depois da morte).
        /// </summary>
        static UnityEditor.Animations.AnimatorController MontarControlador(string pasta, string nome, string fbx, out List<string> nomes)
        {
            var clipes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
                                      .ToDictionary(c => c.name, c => c);
            nomes = clipes.Keys.OrderBy(n => n == "Parado" ? 0 : n == "Andar" ? 1 : Mortes.Contains(n) ? 3 : 2).ThenBy(n => n).ToList();
            string caminho = $"{pasta}/{nome}.controller";
            // refeito no mesmo asset (apagar e criar de novo deixava o prefab carregado apontando para o controlador morto)
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(caminho);
            if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(caminho);
            ctrl.parameters = new AnimatorControllerParameter[0];
            var smVelha = ctrl.layers[0].stateMachine;
            foreach (var t in smVelha.anyStateTransitions) smVelha.RemoveAnyStateTransition(t);
            foreach (var s in smVelha.states) smVelha.RemoveState(s.state);
            ctrl.AddParameter("Velocidade", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Reiniciar", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            if (!clipes.TryGetValue("Parado", out var cParado))
                throw new System.Exception("o FBX não tem o clipe Parado (rode o chefe_anim.py no Blender).");
            var parado = sm.AddState("Parado", new Vector3(300, 0, 0));
            parado.motion = cParado;
            sm.defaultState = parado;
            var r = sm.AddAnyStateTransition(parado);
            r.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Reiniciar");
            r.duration = 0.2f;
            r.canTransitionToSelf = false;
            if (clipes.TryGetValue("Andar", out var cAndar))
            {
                var andar = sm.AddState("Andar", new Vector3(300, 80, 0));
                andar.motion = cAndar;
                var ida = parado.AddTransition(andar);
                ida.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, "Velocidade");
                ida.hasExitTime = false;
                ida.duration = 0.25f;
                var volta = andar.AddTransition(parado);
                volta.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, "Velocidade");
                volta.hasExitTime = false;
                volta.duration = 0.25f;
            }
            int i = 0;
            foreach (var n in nomes)
            {
                if (n == "Parado" || n == "Andar") continue;
                var st = sm.AddState(n, new Vector3(600, 80 * i++, 0));
                st.motion = clipes[n];
                if (EhLoopEspecial(n))
                {
                    ctrl.AddParameter(n, AnimatorControllerParameterType.Bool);
                    var ida = sm.AddAnyStateTransition(st);
                    ida.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, n);
                    ida.canTransitionToSelf = false;
                    ida.duration = 0.25f;
                    var volta = st.AddTransition(parado);
                    volta.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, n);
                    volta.hasExitTime = false;
                    volta.duration = 0.3f;
                }
                else
                {
                    ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);
                    var ida = sm.AddAnyStateTransition(st);
                    ida.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, n);
                    ida.canTransitionToSelf = false;
                    ida.duration = 0.12f;
                    if (!Mortes.Contains(n))
                    {
                        var volta = st.AddTransition(parado);
                        volta.hasExitTime = true;
                        volta.exitTime = 0.92f;
                        volta.duration = 0.2f;
                    }
                }
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssetIfDirty(ctrl);
            return ctrl;
        }

        static Avatar ImportarModelo(string caminho)
        {
            if (AssetImporter.GetAtPath(caminho) == null) AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceSynchronousImport);
            var imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
            imp.animationType = ModelImporterAnimationType.Generic;   // o esqueleto é dele (não o do Oco)
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;                               // as ações do Blender (chefe_anim.py)
            imp.animationCompression = ModelImporterAnimationCompression.Optimal;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importNormals = ModelImporterNormals.Import;            // o sombreamento plano vem do Blender
            imp.importTangents = ModelImporterTangents.CalculateMikk;  // o mapa de normal
            imp.bakeAxisConversion = true;
            imp.optimizeGameObjects = false;                          // os ossos ficam (para animar)
            imp.isReadable = false;
            imp.clipAnimations = new ModelImporterClipAnimation[0];   // (as tomadas do FBX novo, as ações de agora)
            imp.SaveAndReimport();
            // só com a animação importada a Unity conhece as tomadas: aí os clipes ganham o nome curto e o loop
            imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
            ClipesDoFbx(imp);
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid) Debug.LogWarning($"Chefe: o avatar Generic de {caminho} não ficou válido (o prefab sai sem ele).");
            return avatar;
        }

        static Material MontarMaterial(string pasta, string nome)
        {
            string tex = $"{pasta}/Texturas/{nome}";
            string cor = tex + "_BaseColor.png", normal = tex + "_Normal.png", rug = tex + "_Roughness.png", liso = tex + "_MetalLiso.png";
            if (!File.Exists(liso) || File.GetLastWriteTimeUtc(rug) > File.GetLastWriteTimeUtc(liso)) LisuraDaRugosidade(rug, liso);
            Textura(cor, TextureImporterType.Default, true);
            Textura(normal, TextureImporterType.NormalMap, false);
            Textura(liso, TextureImporterType.Default, false);
            Textura(rug, TextureImporterType.Default, false, 256);   // (só a fonte do _MetalLiso)
            Directory.CreateDirectory($"{pasta}/Materiais");
            string caminho = $"{pasta}/Materiais/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            bool novo = m == null;
            if (novo) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cor));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(liso));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.75f);                     // (a lisura = o alfa do _MetalLiso × isto: carne úmida, não plástico)
            m.SetFloat("_Cull", (float)CullMode.Off);              // bordas abertas onde os corpos fundidos somem na carne
            m.doubleSidedGI = true;
            if (novo) AssetDatabase.CreateAsset(m, caminho);
            else
            {
                EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssetIfDirty(m);
            }
            return m;
        }

        static void Textura(string caminho, TextureImporterType tipo, bool srgb, int tamanho = 2048)
        {
            if (AssetImporter.GetAtPath(caminho) == null) AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (ti == null) return;
            if (ti.textureType == tipo && ti.sRGBTexture == srgb && ti.maxTextureSize == tamanho) return;
            ti.textureType = tipo;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = tamanho;   // 2048 como os zumbis (o atlas do Blender tem 4096: dá para subir no importador)
            ti.SaveAndReimport();
        }

        // o mapa de metal e lisura da URP: R = 0 (nada de metal), A = 1 - rugosidade
        static void LisuraDaRugosidade(string rugosidade, string saida)
        {
            var origem = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            origem.LoadImage(File.ReadAllBytes(rugosidade));
            var px = origem.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, (byte)(255 - px[i].r));
            var mapa = new Texture2D(origem.width, origem.height, TextureFormat.RGBA32, false, true);
            mapa.SetPixels32(px);
            File.WriteAllBytes(saida, mapa.EncodeToPNG());
            Object.DestroyImmediate(origem);
            Object.DestroyImmediate(mapa);
            AssetDatabase.ImportAsset(saida, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// O prefab: a raiz (no chão, olhando para +Z) e o Modelo (o FBX desempacotado), com o Animator (avatar Generic, sem
        /// controlador). A malha do Blender olha para -Y; se ela chegar olhando para -Z, o Modelo gira 180°.
        /// </summary>
        static GameObject MontarPrefab(string nome, string fbx, Avatar avatar, Material mat, RuntimeAnimatorController controlador,
                                       out float altura, out int tris, out int ossos)
        {
            if (!AssetDatabase.IsValidFolder(PastaDosPrefabs)) AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Chefes");
            var raiz = new GameObject($"Chefe_{nome}");
            try
            {
                var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                modelo.name = "Modelo";
                modelo.transform.SetParent(raiz.transform, false);
                var malhas = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (malhas.Length == 0) throw new System.Exception("o FBX não tem malha com esqueleto.");
                tris = 0;
                ossos = 0;
                foreach (var r in malhas)
                {
                    r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMesh.subMeshCount)).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.updateWhenOffscreen = false;
                    r.skinnedMotionVectors = false;
                    for (int s = 0; s < r.sharedMesh.subMeshCount; s++) tris += (int)r.sharedMesh.GetIndexCount(s) / 3;
                    ossos = Mathf.Max(ossos, r.bones.Length);
                }
                // a malha na pose de repouso, no espaço da raiz: a altura, o chão e para onde ela olha
                var (min, max, frente) = Medir(raiz.transform, malhas);
                float frenteOssos = FrentePelosOssos(raiz.transform, modelo.transform);
                if ((Mathf.Abs(frenteOssos) > 0.05f ? frenteOssos : frente) < 0f)
                {
                    modelo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * modelo.transform.localRotation;
                    (min, max, frente) = Medir(raiz.transform, malhas);
                }
                modelo.transform.localPosition += new Vector3(0f, -min.y, 0f);   // os pés no chão (y = 0 da raiz)
                altura = max.y - min.y;
                foreach (var r in malhas)   // a caixa de recorte com folga (os braços e as mãos animados saem da pose)
                {
                    var b = r.localBounds;
                    b.Expand(b.size.magnitude * 0.25f);
                    r.localBounds = b;
                }
                var animador = modelo.GetComponent<Animator>();
                if (animador == null) animador = modelo.AddComponent<Animator>();
                animador.avatar = avatar;
                animador.runtimeAnimatorController = controlador;   // as animações dele (o esqueleto é próprio de cada chefe)
                animador.applyRootMotion = false;
                animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                string caminho = $"{PastaDosPrefabs}/Chefe_{nome}.prefab";
                var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, caminho, out bool ok);
                if (!ok || salvo == null) throw new IOException($"não consegui salvar {caminho}.");
                return salvo;
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        /// <summary>
        /// Para onde o chefe olha pelos ossos: o z da mandíbula (o rosto do Condutor, a boca do Longo) ou da cabeça menos o
        /// da pelve, no espaço da raiz (a cabeça de todos fica à frente da pelve). 0 = sem os ossos.
        /// </summary>
        static float FrentePelosOssos(Transform raiz, Transform modelo)
        {
            var ossos = modelo.GetComponentsInChildren<Transform>(true);
            var cabeca = ossos.FirstOrDefault(t => t.name == "mandibula") ?? ossos.FirstOrDefault(t => t.name == "cabeca");
            var pelve = ossos.FirstOrDefault(t => t.name == "pelvis");
            if (cabeca == null || pelve == null) return 0f;
            return raiz.InverseTransformPoint(cabeca.position).z - raiz.InverseTransformPoint(pelve.position).z;
        }

        /// <summary>
        /// A caixa da malha (na pose de repouso) no espaço de 'raiz' e para onde ela olha: o sinal do z do topo (a cabeça
        /// fica um pouco à frente do meio do corpo, e a do Longo bem à frente) — positivo = olhando para +Z.
        /// </summary>
        static (Vector3 min, Vector3 max, float frente) Medir(Transform raiz, SkinnedMeshRenderer[] malhas)
        {
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            var pontos = new List<Vector3>();
            var temp = new UnityEngine.Mesh();
            foreach (var r in malhas)
            {
                r.BakeMesh(temp, true);
                var m = raiz.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (var v in temp.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    pontos.Add(p);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            Object.DestroyImmediate(temp);
            float corte = Mathf.Lerp(min.y, max.y, 0.85f);
            var topo = pontos.Where(p => p.y >= corte).ToList();
            float zTopo = topo.Count > 0 ? topo.Average(p => p.z) : 0f;
            float zMeio = (min.z + max.z) * 0.5f;
            return (min, max, zTopo - zMeio);
        }

        // ------------------------------------------------------------------ a vitrine

        /// <summary>
        /// Refaz Scenes/Exemplos/Chefes.unity numa cena à parte (aditiva), como a de comparação dos zumbis: as cenas abertas
        /// ficam como estão (nada é fechado, trocado ou salvo nelas) e só ela é salva. Não abre sozinha (fica marcada no Project).
        /// </summary>
        static void MontarAVitrine()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                Debug.LogWarning("Vitrine dos chefes: não montada, porque há uma cena sem nome aberta (a Unity não abre outra sem nome " +
                                 "ao lado). Salve ou feche essa cena e rode Chefes > Montar a vitrine dos chefes.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PastaDaCena)) AssetDatabase.CreateFolder("Assets/_Game/Scenes/Exemplos", "Chefes");
            var antes = SceneManager.GetActiveScene();
            var velha = SceneManager.GetSceneByPath(Cena);
            var nova = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (velha.IsValid())
            {
                if (antes == velha) antes = nova;
                EditorSceneManager.CloseScene(velha, true);
            }
            int postos = 0;
            bool salva = false;
            try
            {
                SceneManager.SetActiveScene(nova);
                postos = PreencherAVitrine(nova);
                EditorSceneManager.MarkSceneDirty(nova);
                if (!EditorSceneManager.SaveScene(nova, Cena)) throw new IOException($"Não consegui salvar {Cena}.");
                salva = true;
            }
            finally
            {
                if (antes.IsValid() && antes.isLoaded && antes != nova) SceneManager.SetActiveScene(antes);
                if (!salva && nova.IsValid())
                {
                    if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(nova, true);
                    else if (File.Exists(Cena)) EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
                }
            }
            bool unica = SceneManager.sceneCount == 1;
            if (!unica) EditorSceneManager.CloseScene(nova, true);
            var arquivo = AssetDatabase.LoadAssetAtPath<SceneAsset>(Cena);
            if (arquivo != null) EditorGUIUtility.PingObject(arquivo);
            Debug.Log($"Vitrine dos chefes montada: {Cena} ({postos} chefes; fora das Build Settings)" +
                      (unica ? "; ela está aberta." : "; abra-a pelo Project (as cenas abertas não foram mexidas)."));
        }

        static int PreencherAVitrine(Scene cena)
        {
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.4f;
            sol.color = new Color(1f, 0.95f, 0.88f);
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.8f;
            sol.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            var tras = new GameObject("Luz de trás").AddComponent<Light>();
            tras.type = LightType.Directional;
            tras.intensity = 0.7f;
            tras.color = new Color(0.78f, 0.84f, 1f);
            tras.shadows = LightShadows.None;
            tras.transform.rotation = Quaternion.Euler(25f, -20f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.46f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.32f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.17f, 0.16f);
            RenderSettings.fog = false;

            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var raiz = new GameObject("Chefes").transform;
            // a fila anda para -X: vista da câmera (em +Z, olhando para -Z), ela se lê da esquerda para a direita
            raiz.localScale = Vector3.one;
            float x = 0f;
            int postos = 0;
            float maisAlto = 2f;
            var itens = new List<ZombieCityCleaner.Chefes.VitrineDosChefes.Item>();
            // o Oco de 1,8 m primeiro, para a escala (o prefab do jogo sem nada de jogo: só o modelo e o Animator)
            var oco = AssetDatabase.LoadAssetAtPath<GameObject>(OcoDaEscala);
            if (oco != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(oco, cena);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                SoOModelo(go);
                go.name = "Oco (escala, 1,8 m)";
                go.transform.SetParent(raiz, false);
                go.transform.localPosition = new Vector3(x, 0f, 0f);
                Rotulo(raiz, "O Oco\n1,8 m", new Vector3(x, 2.15f, 0f), 0.16f, fonte, Color.white);
                itens.Add(new ZombieCityCleaner.Chefes.VitrineDosChefes.Item { titulo = "O Oco (escala)", detalhes = "O zumbi comum, 1,8 m.", raiz = go.transform });
                x -= 0.5f + EntreChefes;
            }
            else Debug.LogWarning($"Vitrine dos chefes: falta {OcoDaEscala} (o Oco da escala ficou de fora).");

            foreach (var (nome, titulo) in Chefes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosPrefabs}/Chefe_{nome}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"Vitrine dos chefes: falta o prefab Chefe_{nome} (Chefes > Montar os chefes).");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                go.transform.SetParent(raiz, false);
                var malhas = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var (min, max, _) = Medir(go.transform, malhas);
                float altura = max.y - min.y;
                float px = x - max.x;   // encosta a caixa dele no fim da fila (que anda para -X)
                go.transform.localPosition = new Vector3(px, 0f, 0f);
                int tris = malhas.Sum(r => Enumerable.Range(0, r.sharedMesh.subMeshCount).Sum(s => (int)r.sharedMesh.GetIndexCount(s) / 3));
                int ossos = malhas.Max(r => r.bones.Length);
                Rotulo(raiz, $"{titulo}\n{altura:0.0} m  ·  {tris:N0} tris", new Vector3(px + (min.x + max.x) * 0.5f, altura + 0.5f, 0f),
                       Mathf.Clamp(altura * 0.05f, 0.18f, 0.4f), fonte, new Color(1f, 0.85f, 0.4f));
                itens.Add(new ZombieCityCleaner.Chefes.VitrineDosChefes.Item
                {
                    titulo = titulo,
                    detalhes = $"{altura:0.0} m  ·  {tris:N0} triângulos  ·  {ossos} ossos\n{(Descricao.TryGetValue(nome, out var d) ? d : "")}",
                    raiz = go.transform,
                });
                x = px + min.x - EntreChefes;
                maisAlto = Mathf.Max(maisAlto, altura);
                postos++;
            }

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chão";
            float meio = x * 0.5f;
            chao.transform.position = new Vector3(meio, 0f, 0f);
            chao.transform.localScale = new Vector3(Mathf.Abs(x) / 10f + 8f, 1f, 8f);
            chao.GetComponent<MeshRenderer>().sharedMaterial = MaterialDaVitrine("Chao", new Color(0.19f, 0.19f, 0.2f), 0.05f);

            // a câmera de frente para a fila inteira (os chefes olham para +Z: a câmera fica em +Z olhando para -Z)
            var camera = new GameObject("Câmera") { tag = "MainCamera" };
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.075f, 0.085f);
            float distancia = Mathf.Max(Mathf.Abs(x) * 0.5f / Mathf.Tan(Mathf.Deg2Rad * cam.fieldOfView * 0.5f * 1.6f), maisAlto * 2.2f);
            camera.transform.SetPositionAndRotation(new Vector3(meio, maisAlto * 0.55f, distancia), Quaternion.Euler(6f, 180f, 0f));
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var vitrine = new GameObject("Vitrine").AddComponent<ZombieCityCleaner.Chefes.VitrineDosChefes>();
            vitrine.cameraDaCena = cam;
            vitrine.itens = itens.ToArray();
            camera.AddComponent<AudioListener>();
            return postos;
        }

        /// <summary>Tira do prefab do zumbi tudo o que é de jogo (IA, vida, física, navegação), deixando o modelo e o Animator.</summary>
        static void SoOModelo(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
        }

        static Transform Rotulo(Transform pai, string texto, Vector3 lugar, float letra, Font fonte, Color cor)
        {
            var go = new GameObject("Rótulo");
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(lugar, Quaternion.Euler(0f, 180f, 0f));
            var tm = go.AddComponent<TextMesh>();
            tm.font = fonte;
            tm.fontSize = 64;
            tm.characterSize = letra / 7.4f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.richText = false;
            tm.color = cor;
            tm.text = texto;
            go.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;
            return go.transform;
        }

        static Material MaterialDaVitrine(string nome, Color cor, float lisura)
        {
            string caminho = $"{PastaDaCena}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            bool novo = m == null;
            if (novo) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", lisura);
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
