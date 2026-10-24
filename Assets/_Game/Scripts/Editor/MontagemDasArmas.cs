using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As armas (ajustes antes da Parte 7, fatia 3): a importação dos braços de primeira pessoa de cada arma, das
    /// animações de arma longa do corpo e das armas avulsas; as malhas das armas; o Animator dos braços e os override de
    /// cada arma; o prefab do jogador (pela <see cref="MontagemDaRede"/>) e o painel de armas do bunker na cena.
    /// Menu: Zombie City Cleaner > Montar armas. Pode rodar de novo (o painel precisa da cidade montada, com o móvel
    /// "Painel_Armas" no salão do bunker).
    /// </summary>
    public static class MontagemDasArmas
    {
        const string Arte = "Assets/_Game/Art/Characters/Jogador";
        const string Malhas = Arte + "/Malhas";
        const string ControladorFP = Arte + "/Jogador_FP.controller";
        const string ControladorTP = Arte + "/Jogador_TP.controller";
        const string FbxArmas = Arte + "/Armas.fbx";
        const string MaterialArma = Arte + "/Materials/Pistola.mat";
        const string MaterialPersonagem = Arte + "/Materials/Jogador_Variacao0.mat";

        static readonly string[] ClipesFP = { "Idle", "Walk", "Run", "Fire", "DryFire", "Reload", "Sacar" };
        static readonly string[] ClipesTP = { "Idle", "Walk", "Run", "Aim", "Fire" };
        static readonly string[] EmLaco = { "Idle", "Walk", "Run", "Aim" };
        /// <summary>
        /// As vagas do painel de armas (o VAGAS_DO_PAINEL do cidade_moveis.py): a arma de cada uma, o x (do painel, na
        /// Unity) e a altura do apoio, do chão. As longas de cima para baixo, à esquerda; as pistolas numa coluna.
        /// </summary>
        static readonly (TipoDeArma dono, float x, float y)[] VagasDoPainel =
        {
            (TipoDeArma.Sniper, -0.6f, 2.75f), (TipoDeArma.Rifle, -0.6f, 2.27f), (TipoDeArma.Espingarda, -0.6f, 1.85f),
            (TipoDeArma.Submetralhadora, -0.6f, 1.35f), (TipoDeArma.Pistola, 0.85f, 2.7f), (TipoDeArma.Pistola, 0.85f, 2.25f),
            (TipoDeArma.Pistola, 0.85f, 1.8f), (TipoDeArma.Pistola, 0.85f, 1.35f),
        };
        /// <summary>A frente das ripas do painel, a partir da parede (a arma encosta nela).</summary>
        const float FrenteDoPainel = 0.04f;

        public static string FbxFP(int arma) => $"{Arte}/Jogador_FP_{Armas.Modelos[arma]}.fbx";
        public static string MalhaDaArma(int arma) => $"{Malhas}/Arma_{Armas.Modelos[arma]}.asset";
        public static string ControladorFPDa(int arma) => arma == 0 ? ControladorFP : $"{Arte}/Jogador_FP_{Armas.Modelos[arma]}.overrideController";
        public static string ControladorTPDa(int arma) => arma == 0 ? ControladorTP : $"{Arte}/Jogador_TP_{Armas.Modelos[arma]}.overrideController";
        /// <summary>As armas novas trazem as peças avulsas num FBX próprio (Armas_Sniper.fbx), fora do Armas.fbx.</summary>
        static string FbxAvulsaDa(int arma) => $"{Arte}/Armas_{Armas.Modelos[arma]}.fbx";
        /// <summary>A arma cujas animações o corpo usa (a sniper usa as do rifle).</summary>
        static int CorpoDa(int arma) => (int)(Armas.Todas[arma].corpoComo ?? (TipoDeArma)arma);

        [MenuItem("Zombie City Cleaner/Montar armas")]
        public static void Montar()
        {
            Preparar();
            MontagemDaRede.MontarPrefab();
            var cena = EditorSceneManager.GetActiveScene();
            MontarSuporte(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            // o Jogador_FP.controller é apagado e criado de novo: o prefab salvo igual não seria reimportado e ficaria
            // com a referência vazia (veja a montagem dos zumbis)
            AssetDatabase.ImportAsset("Assets/_Game/Prefabs/Jogador.prefab", ImportAssetOptions.ForceUpdate);
            Debug.Log("Armas montadas: braços, animações, malhas, prefab do jogador e o suporte do bunker.");
        }

        /// <summary>Tudo menos o prefab e a cena: importações, malhas e controladores.</summary>
        public static void Preparar()
        {
            Directory.CreateDirectory(Malhas);
            for (int i = 0; i < Armas.Quantas; i++) ImportarFP(FbxFP(i));
            ImportarCorpo();
            ImportarArmas();
            for (int i = 0; i < Armas.Quantas; i++) MontarMalha(i);
            MontarControladorFP();
            for (int i = 1; i < Armas.Quantas; i++)
            {
                MontarOverride(ControladorFP, FbxFP(0), FbxFP(i), ClipesFP, c => c, ControladorFPDa(i));
                string modelo = Armas.Modelos[CorpoDa(i)];
                MontarOverride(ControladorTP, Arte + "/Jogador.fbx", Arte + "/Jogador.fbx", ClipesTP, c => $"{c}_{modelo}", ControladorTPDa(i));
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ importação

        static ModelImporter Importador(string caminho)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            if (imp == null) throw new FileNotFoundException("FBX não encontrado (copie da pasta de arte)", caminho);
            return imp;
        }

        /// <summary>Um clipe por take (o nome da faixa de NLA do Blender), do primeiro ao último quadro.</summary>
        static ModelImporterClipAnimation[] Clipes(ModelImporter imp, System.Func<string, string> nome)
        {
            return imp.importedTakeInfos.Select(t => (take: t, n: nome(t.name))).Where(x => x.n != null).Select(x => new ModelImporterClipAnimation
            {
                name = x.n, takeName = x.take.name,
                firstFrame = Mathf.Round(x.take.startTime * x.take.sampleRate), lastFrame = Mathf.Round(x.take.stopTime * x.take.sampleRate),
                loopTime = EmLaco.Any(l => x.n == l || x.n.StartsWith(l + "_")),
            }).ToArray();
        }

        static void ImportarFP(string caminho)
        {
            var imp = Importador(caminho);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Personagem"), AssetDatabase.LoadAssetAtPath<Material>(MaterialPersonagem));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pistola"), AssetDatabase.LoadAssetAtPath<Material>(MaterialArma));
            imp.clipAnimations = Clipes(imp, t => t);
            imp.SaveAndReimport();
        }

        /// <summary>O corpo: as animações da pistola (TP_Idle → Idle) e as de arma longa (TP_Idle_Rifle → Idle_Rifle).</summary>
        static void ImportarCorpo()
        {
            var imp = Importador(Arte + "/Jogador.fbx");
            imp.clipAnimations = Clipes(imp, t => t.StartsWith("TP_") ? t.Substring(3) : null);
            imp.SaveAndReimport();
        }

        static void ImportarArmas()
        {
            ImportarAvulsas(FbxArmas);
            for (int i = 0; i < Armas.Quantas; i++)
                if (File.Exists(FbxAvulsaDa(i))) ImportarAvulsas(FbxAvulsaDa(i));
        }

        static void ImportarAvulsas(string fbx)
        {
            var imp = Importador(fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.isReadable = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pistola"), AssetDatabase.LoadAssetAtPath<Material>(MaterialArma));
            imp.SaveAndReimport();
        }

        /// <summary>A arma avulsa numa malha só (as peças juntas), com a origem no cabo, o cano em +Z e o topo em +Y.</summary>
        static void MontarMalha(int arma)
        {
            string prefixo = Armas.Modelos[arma] + "_";
            string fbx = File.Exists(FbxAvulsaDa(arma)) ? FbxAvulsaDa(arma) : FbxArmas;
            var pecas = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().Where(m => m.name.StartsWith(prefixo)).ToArray();
            if (pecas.Length == 0) throw new System.Exception($"Nenhuma peça {prefixo}* no {fbx}.");
            var juntas = pecas.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray();
            string caminho = MalhaDaArma(arma);
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = "Arma_" + Armas.Modelos[arma];
            malha.CombineMeshes(juntas, true, false);
            malha.RecalculateBounds();
            if (nova) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
        }

        // ------------------------------------------------------------------ Animator

        static AnimationClip Clipe(string fbx, string nome)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(a => a.name == nome);
            if (c == null) throw new System.Exception($"O clipe {nome} não está no {fbx}.");
            return c;
        }

        /// <summary>
        /// Os braços: a base é uma árvore pelo Speed (parado, andando, correndo); a camada "Ações" tem os estados que o
        /// código toca do começo (Fire, DryFire, Reload e Sacar), e cada um volta sozinho ao estado vazio.
        /// Os clipes são os da pistola; as outras armas usam um override.
        /// </summary>
        static void MontarControladorFP()
        {
            string fbx = FbxFP(0);
            AssetDatabase.DeleteAsset(ControladorFP);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControladorFP);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var baseSm = ctrl.layers[0].stateMachine;
            var arvore = new BlendTree { name = "Locomoção", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arvore, ctrl);
            arvore.AddChild(Clipe(fbx, "Idle"), 0f);
            arvore.AddChild(Clipe(fbx, "Walk"), 2.0f);
            arvore.AddChild(Clipe(fbx, "Run"), 4.2f);
            var loc = baseSm.AddState("Locomoção");
            loc.motion = arvore;
            loc.writeDefaultValues = false;
            baseSm.defaultState = loc;

            ctrl.AddLayer("Ações");
            var camadas = ctrl.layers;
            camadas[1].defaultWeight = 1f;
            camadas[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = camadas;
            var sm = ctrl.layers[1].stateMachine;
            var vazio = sm.AddState("Vazio");
            vazio.writeDefaultValues = false;
            sm.defaultState = vazio;
            foreach (var nome in new[] { "Fire", "DryFire", "Reload", "Sacar" })
            {
                var e = sm.AddState(nome);
                e.motion = Clipe(fbx, nome);
                e.writeDefaultValues = false;
                var volta = e.AddTransition(vazio);
                volta.hasExitTime = true; volta.exitTime = 1f; volta.duration = 0.08f; volta.hasFixedDuration = true;
            }
            EditorUtility.SetDirty(ctrl);
        }

        /// <summary>Um override do controlador: cada clipe da base trocado pelo da arma (de outro FBX, ou com outro nome).</summary>
        static void MontarOverride(string controlador, string fbxBase, string fbxArma, string[] clipes, System.Func<string, string> nome, string caminho)
        {
            var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(caminho);
            if (ov == null)
            {
                ov = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(ov, caminho);
            }
            ov.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controlador);
            var trocas = clipes.Select(c => new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(Clipe(fbxBase, c), Clipe(fbxArma, nome(c)))).ToList();
            ov.ApplyOverrides(trocas);
            EditorUtility.SetDirty(ov);
        }

        // ------------------------------------------------------------------ o suporte na cena

        /// <summary>
        /// "Parede de armas": um NetworkObject da cena no lugar do móvel "Painel_Armas" (posto pela montagem da cidade na
        /// parede do fundo do salão do bunker), com as 8 vagas do painel, cada uma de um tipo de arma, com o colisor que a
        /// mira procura (do tamanho da arma dela) e a arma em pé no apoio.
        /// </summary>
        public static void MontarSuporte(UnityEngine.SceneManagement.Scene cena)
        {
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Parede de armas");
            if (velho != null) Object.DestroyImmediate(velho);
            var movel = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Painel_Armas");
            if (movel == null)
            {
                Debug.LogWarning("O móvel Painel_Armas não está na cena: rode a montagem da cidade antes.");
                return;
            }
            var raiz = new GameObject("Parede de armas");
            raiz.transform.SetPositionAndRotation(movel.position, movel.rotation);
            raiz.AddComponent<NetworkObject>();
            var parede = raiz.AddComponent<ParedeDeArmas>();
            parede.malhas = Enumerable.Range(0, Armas.Quantas).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDaArma(i))).ToArray();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialArma);
            parede.vagas = new VagaDeArma[VagasDoPainel.Length];
            for (int k = 0; k < VagasDoPainel.Length; k++)
            {
                var (dono, x, y) = VagasDoPainel[k];
                var go = new GameObject($"Vaga {k + 1} ({Armas.Dados(dono).nome})");
                go.transform.SetParent(raiz.transform, false);
                go.transform.localPosition = new Vector3(x, y, FrenteDoPainel);
                // o colisor cobre a arma da vaga, em pé no apoio (o comprimento dela ao longo do painel)
                var b = parede.malhas[(int)dono].bounds;
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, b.size.y / 2f, 0.06f);
                col.size = new Vector3(b.size.z + 0.06f, b.size.y + 0.04f, 0.12f);
                var arma = new GameObject("Arma");
                arma.transform.SetParent(go.transform, false);
                var filtro = arma.AddComponent<MeshFilter>();
                var r = arma.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                var vaga = go.AddComponent<VagaDeArma>();
                vaga.parede = parede; vaga.indice = k; vaga.dono = dono; vaga.filtro = filtro;
                parede.vagas[k] = vaga;
            }
        }
    }
}
