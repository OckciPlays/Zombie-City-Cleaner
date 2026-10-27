using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As armas e os itens (a comida e a água, da Parte 7): a importação dos braços de primeira pessoa de cada um, das
    /// animações de arma longa do corpo, das armas e dos itens avulsos e dos ícones do inventário; as malhas; o Animator
    /// dos braços e os override de cada arma e item; o prefab do jogador (pela <see cref="MontagemDaRede"/>); e, na
    /// cena, o painel de armas do bunker e o "Itens no mundo".
    /// Menu: Zombie City Cleaner > Montar armas. Pode rodar de novo (o painel e os itens precisam da cidade montada, com
    /// o móvel "Painel_Armas" no salão do bunker e as marcas "Lugar de item").
    /// </summary>
    public static class MontagemDasArmas
    {
        const string Arte = "Assets/_Game/Art/Characters/Jogador";
        const string Malhas = Arte + "/Malhas";
        const string ControladorFP = Arte + "/Jogador_FP.controller";
        const string ControladorTP = Arte + "/Jogador_TP.controller";
        const string FbxArmas = Arte + "/Armas.fbx";
        const string FbxItens = Arte + "/Itens.fbx";
        /// <summary>As 3 mochilas e o coldre (Blender/equipamentos.py, no repositório).</summary>
        const string FbxEquipamentos = Arte + "/Equipamentos.fbx";
        /// <summary>As caixas de munição, uma por calibre (Blender/municao.py, no repositório).</summary>
        const string FbxMunicao = Arte + "/Municao.fbx";
        /// <summary>As peças do motor: a vela, a bateria e o radiador (Blender/pecas.py, no repositório).</summary>
        const string FbxPecas = Arte + "/Pecas.fbx";
        const string PastaIcones = "Assets/_Game/Art/UI/Icones";
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

        /// <summary>
        /// Os braços de primeira pessoa de um modelo (na ordem de Itens.Modelos: as armas, a lata e a garrafa; depois, os
        /// punhos da mão vazia, Itens.Punhos).
        /// </summary>
        public static string FbxFP(int modelo) => $"{Arte}/Jogador_FP_{Itens.ModeloFP(modelo)}.fbx";
        /// <summary>A malha avulsa de um modelo: Arma_&lt;nome&gt; ou Item_&lt;nome&gt; (origem onde a mão segura, topo em +Y).</summary>
        public static string MalhaDo(int modelo) => $"{Malhas}/{(modelo < Armas.Quantas ? "Arma" : "Item")}_{Itens.Modelos[modelo]}.asset";
        public static string ControladorFPDa(int modelo) => modelo == 0 ? ControladorFP : $"{Arte}/Jogador_FP_{Itens.ModeloFP(modelo)}.overrideController";
        public static string ControladorTPDa(int arma) => arma == 0 ? ControladorTP : $"{Arte}/Jogador_TP_{Armas.Modelos[arma]}.overrideController";
        /// <summary>O corpo com a mão vazia (a guarda e o soco) ou com comida e água na mão.</summary>
        public static string ControladorTPDasMaos => $"{Arte}/Jogador_TP_Maos.overrideController";
        /// <summary>O ícone do inventário, colorido, na proporção das células do item (renderizado no Blender pelo itens.py).</summary>
        public static string IconeDo(int modelo) => $"{PastaIcones}/Icone_{Itens.Modelos[modelo]}.png";
        /// <summary>A foto do item, para os detalhes da tela do inventário (itens.py).</summary>
        public static string FotoDo(int modelo) => $"{PastaIcones}/Foto_{Itens.Modelos[modelo]}.png";
        /// <summary>A malha de um equipamento (as mochilas e o coldre, na ordem do enum Item): Equip_&lt;modelo&gt;.</summary>
        public static string MalhaDoEquipamento(int k) => $"{Malhas}/Equip_{Itens.ModeloDoEquipamento(k)}.asset";
        public static string IconeDoEquipamento(int k) => $"{PastaIcones}/Icone_{Itens.ModeloDoEquipamento(k)}.png";
        public static string FotoDoEquipamento(int k) => $"{PastaIcones}/Foto_{Itens.ModeloDoEquipamento(k)}.png";
        /// <summary>A malha de uma caixa de munição (na ordem de Calibre): Mun_&lt;modelo&gt;.</summary>
        public static string MalhaDaMunicao(int k) => $"{Malhas}/Mun_{Itens.ModeloDaMunicao(k)}.asset";
        public static string IconeDaMunicao(int k) => $"{PastaIcones}/Icone_{Itens.ModeloDaMunicao(k)}.png";
        public static string FotoDaMunicao(int k) => $"{PastaIcones}/Foto_{Itens.ModeloDaMunicao(k)}.png";
        /// <summary>A malha de uma peça do motor (na ordem do enum Item): Peca_&lt;modelo&gt;.</summary>
        public static string MalhaDaPeca(int k) => $"{Malhas}/Peca_{Itens.ModeloDaPeca(k)}.asset";
        public static string IconeDaPeca(int k) => $"{PastaIcones}/Icone_{Itens.ModeloDaPeca(k)}.png";
        public static string FotoDaPeca(int k) => $"{PastaIcones}/Foto_{Itens.ModeloDaPeca(k)}.png";
        /// <summary>As armas novas trazem as peças avulsas num FBX próprio (Armas_Sniper.fbx), fora do Armas.fbx.</summary>
        static string FbxAvulsaDa(int arma) => $"{Arte}/Armas_{Armas.Modelos[arma]}.fbx";
        /// <summary>A arma cujas animações o corpo usa (a sniper usa as do rifle).</summary>
        static int CorpoDa(int arma) => (int)(Armas.Todas[arma].corpoComo ?? (TipoDeArma)arma);

        /// <summary>
        /// Os personagens (27/10/2026; Modelo 3D/Personagens na pasta de arte, o personagens.py): Personagem_&lt;modelo&gt;.fbx
        /// (o esqueleto do jogador com os 5 dedos, o corpo e as 25 animações, com os nomes do Jogador.fbx) e a textura
        /// Personagem_&lt;modelo&gt;.jpg. Os retratos da tela de escolha ficam em Art/UI/Personagens/Resources.
        /// </summary>
        public const string ArtePersonagens = "Assets/_Game/Art/Characters/Personagens";
        public static string FbxDoPersonagem(int p) => $"{ArtePersonagens}/Personagem_{Personagens.Modelos[p]}.fbx";
        /// <summary>O controlador do corpo do personagem com a arma (o override do Jogador_TP com as animações dele).</summary>
        public static string ControladorTPDoPersonagem(int p, int arma) => $"{ArtePersonagens}/Personagem_{Personagens.Modelos[p]}_{Armas.Modelos[arma]}.overrideController";
        /// <summary>O corpo do personagem com a mão vazia (a guarda e o soco) ou com comida e água na mão.</summary>
        public static string ControladorTPDasMaosDoPersonagem(int p) => $"{ArtePersonagens}/Personagem_{Personagens.Modelos[p]}_Maos.overrideController";
        static string MaterialDoPersonagem(int p) => $"{ArtePersonagens}/Materials/Personagem_{Personagens.Modelos[p]}.mat";

        [MenuItem("Zombie City Cleaner/Montar armas")]
        public static void Montar()
        {
            Preparar();
            MontagemDaRede.MontarPrefab();
            var cena = EditorSceneManager.GetActiveScene();
            MontarSuporte(cena);
            MontarItensNoMundo(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            // o Jogador_FP.controller é apagado e criado de novo: o prefab salvo igual não seria reimportado e ficaria
            // com a referência vazia (veja a montagem dos zumbis)
            AssetDatabase.ImportAsset("Assets/_Game/Prefabs/Jogador.prefab", ImportAssetOptions.ForceUpdate);
            Debug.Log("Armas montadas: braços, animações, malhas, ícones, prefab do jogador, o suporte do bunker e os itens no mundo.");
        }

        /// <summary>Tudo menos o prefab e a cena: importações, malhas, ícones e controladores.</summary>
        public static void Preparar()
        {
            Directory.CreateDirectory(Malhas);
            for (int i = 0; i < Itens.QuantosModelosFP; i++) ImportarFP(FbxFP(i));
            ImportarCorpo();
            ImportarArmas();
            for (int i = 0; i < Itens.QuantosModelos; i++) MontarMalha(i);
            for (int k = 0; k < Itens.QuantosEquipamentos; k++) Juntar(FbxEquipamentos, Itens.ModeloDoEquipamento(k) + "_", MalhaDoEquipamento(k));
            for (int k = 0; k < Itens.QuantasMunicoes; k++) Juntar(FbxMunicao, Itens.ModeloDaMunicao(k) + "_", MalhaDaMunicao(k));
            for (int k = 0; k < Itens.QuantasPecas; k++) Juntar(FbxPecas, Itens.ModeloDaPeca(k) + "_", MalhaDaPeca(k));
            ImportarIcones();
            MontarControladorFP();
            // a mão vazia: o soco direito no lugar do tiro e o esquerdo no do gatilho sem bala (o código alterna); no
            // corpo, a guarda e o soco (as animações TP_<clipe>_Maos)
            MontarOverride(ControladorFP, FbxFP(0), FbxFP(Itens.Punhos), ClipesFP,
                           c => c == "Fire" ? "SocoDireito" : c == "DryFire" ? "SocoEsquerdo" : c == "Reload" ? "Idle" : c, ControladorFPDa(Itens.Punhos));
            MontarOverride(ControladorTP, Arte + "/Jogador.fbx", Arte + "/Jogador.fbx", ClipesTP, c => $"{c}_Maos", ControladorTPDasMaos);
            PrepararPersonagens();
            for (int i = 1; i < Itens.QuantosModelos; i++)
            {
                if (i >= Armas.Quantas)
                {
                    // os itens: o Usar (comer ou beber) no lugar do tiro; o gatilho sem bala e a recarga nunca tocam
                    MontarOverride(ControladorFP, FbxFP(0), FbxFP(i), ClipesFP, c => c == "Fire" ? "Usar" : c == "DryFire" || c == "Reload" ? "Idle" : c, ControladorFPDa(i));
                    continue;
                }
                MontarOverride(ControladorFP, FbxFP(0), FbxFP(i), ClipesFP, c => c, ControladorFPDa(i));
                string modelo = Armas.Modelos[CorpoDa(i)];
                MontarOverride(ControladorTP, Arte + "/Jogador.fbx", Arte + "/Jogador.fbx", ClipesTP, c => $"{c}_{modelo}", ControladorTPDa(i));
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Os personagens e o prefab do jogador, sem mexer na cena. Menu: Zombie City Cleaner > Montar personagens.</summary>
        [MenuItem("Zombie City Cleaner/Montar personagens")]
        public static void MontarPersonagens()
        {
            PrepararPersonagens();
            MontagemDaRede.MontarPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset("Assets/_Game/Prefabs/Jogador.prefab", ImportAssetOptions.ForceUpdate);
            Debug.Log("Personagens montados: as importações, os materiais, os controladores e o prefab do jogador.");
        }

        /// <summary>
        /// Os personagens: a importação (as animações TP_* sem o prefixo, como as do Jogador.fbx), o material com a textura
        /// e os override do Jogador_TP de cada arma (a pistola também) e dos punhos, com as animações de cada um.
        /// </summary>
        public static void PrepararPersonagens()
        {
            Directory.CreateDirectory(ArtePersonagens + "/Materials");
            for (int p = 0; p < Personagens.Quantos; p++)
            {
                ImportarPersonagem(p, MaterialDoCorpo(p));
                string fbx = FbxDoPersonagem(p);
                MontarOverride(ControladorTP, Arte + "/Jogador.fbx", fbx, ClipesTP, c => $"{c}_Maos", ControladorTPDasMaosDoPersonagem(p));
                for (int i = 0; i < Armas.Quantas; i++)
                {
                    int corpo = CorpoDa(i);
                    string sufixo = corpo == 0 ? "" : "_" + Armas.Modelos[corpo];
                    MontarOverride(ControladorTP, Arte + "/Jogador.fbx", fbx, ClipesTP, c => c + sufixo, ControladorTPDoPersonagem(p, i));
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>O material do corpo: o URP Lit com a textura do Tripo, fosco, sem o passe de motion vectors (como o dos zumbis).</summary>
        static Material MaterialDoCorpo(int p)
        {
            string caminho = MaterialDoPersonagem(p);
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtePersonagens}/Personagem_{Personagens.Modelos[p]}.jpg");
            if (textura == null) Debug.LogWarning($"A textura do personagem {Personagens.Modelos[p]} não está em {ArtePersonagens} (copie de Modelo 3D/Personagens).");
            m.SetTexture("_BaseMap", textura);
            m.mainTexture = textura;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            m.SetShaderPassEnabled("MotionVectors", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>O corpo do personagem: rig Generic com avatar do próprio modelo, sem "Bake Axis Conversion" (como o
        /// Jogador.fbx), o material dele e um clipe por faixa de NLA (TP_Idle → Idle, TP_Idle_Rifle → Idle_Rifle).</summary>
        static void ImportarPersonagem(int p, Material material)
        {
            var imp = Importador(FbxDoPersonagem(p));
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Personagem_" + Personagens.Modelos[p]), material);
            imp.SaveAndReimport();
            imp.clipAnimations = Clipes(imp, t => t.StartsWith("TP_") ? t.Substring(3) : null);
            imp.SaveAndReimport();
        }

        /// <summary>Os ícones e as fotos: transparentes, sem mipmap e sem compressão (vão direto para a tela).</summary>
        static void ImportarIcones()
        {
            foreach (var caminho in Enumerable.Range(0, Itens.QuantosModelos).SelectMany(i => new[] { IconeDo(i), FotoDo(i) })
                         .Concat(Enumerable.Range(0, Itens.QuantosEquipamentos).SelectMany(k => new[] { IconeDoEquipamento(k), FotoDoEquipamento(k) }))
                         .Concat(Enumerable.Range(0, Itens.QuantasMunicoes).SelectMany(k => new[] { IconeDaMunicao(k), FotoDaMunicao(k) }))
                         .Concat(Enumerable.Range(0, Itens.QuantasPecas).SelectMany(k => new[] { IconeDaPeca(k), FotoDaPeca(k) })))
            {
                if (!(AssetImporter.GetAtPath(caminho) is TextureImporter imp))
                {
                    Debug.LogWarning($"A imagem {caminho} não existe (copie de Modelo 3D/Jogador/Icones).");
                    continue;
                }
                if (imp.alphaIsTransparency && !imp.mipmapEnabled && imp.textureCompression == TextureImporterCompression.Uncompressed
                    && imp.npotScale == TextureImporterNPOTScale.None) continue;
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;   // o tamanho de cada um é a proporção das células do item
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
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
            ImportarAvulsas(FbxItens);
            ImportarAvulsas(FbxEquipamentos);
            ImportarAvulsas(FbxMunicao);
            ImportarAvulsas(FbxPecas);
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

        /// <summary>
        /// A arma ou o item avulso numa malha só (as peças juntas), com a origem onde a mão segura, o cano (a frente) em +Z
        /// e o topo em +Y.
        /// </summary>
        static void MontarMalha(int modelo)
        {
            string fbx = modelo >= Armas.Quantas ? FbxItens : File.Exists(FbxAvulsaDa(modelo)) ? FbxAvulsaDa(modelo) : FbxArmas;
            Juntar(fbx, Itens.Modelos[modelo] + "_", MalhaDo(modelo));
        }

        /// <summary>As peças &lt;prefixo&gt;* do FBX numa malha só, gravada no caminho.</summary>
        static void Juntar(string fbx, string prefixo, string caminho)
        {
            var pecas = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().Where(m => m.name.StartsWith(prefixo)).ToArray();
            if (pecas.Length == 0) throw new System.Exception($"Nenhuma peça {prefixo}* no {fbx}.");
            var juntas = pecas.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray();
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = Path.GetFileNameWithoutExtension(caminho);
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
            parede.malhas = Enumerable.Range(0, Armas.Quantas).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDo(i))).ToArray();
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

        /// <summary>
        /// "Itens no mundo": um NetworkObject da cena com o <see cref="ItensNoMundo"/>, as malhas de tudo o que pode ficar
        /// no chão (as armas largadas, a lata, a garrafa, as mochilas, o coldre e as caixas de munição) e os itens do começo,
        /// um em cada marca "Lugar de item (...)" que a montagem da cidade põe nos móveis das casas, dos apartamentos e do
        /// depósito do bunker.
        /// </summary>
        public static void MontarItensNoMundo(UnityEngine.SceneManagement.Scene cena)
        {
            var malhas = Enumerable.Range(0, Itens.QuantosModelos).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDo(i))).ToArray();
            if (malhas.Any(m => m == null))
            {
                Debug.LogWarning("As malhas dos itens ainda não existem: rode a montagem das armas.");
                return;
            }
            var velho = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Itens no mundo");
            if (velho != null) Object.DestroyImmediate(velho);
            var marcas = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Lugar de item")).ToList();
            var go = new GameObject("Itens no mundo");
            go.AddComponent<NetworkObject>();
            var mundo = go.AddComponent<ItensNoMundo>();
            mundo.malhas = malhas;
            mundo.malhasDeEquipamento = Enumerable.Range(0, Itens.QuantosEquipamentos).Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDoEquipamento(k))).ToArray();
            mundo.malhasDeMunicao = Enumerable.Range(0, Itens.QuantasMunicoes).Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDaMunicao(k))).ToArray();
            mundo.malhasDePeca = Enumerable.Range(0, Itens.QuantasPecas).Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDaPeca(k))).ToArray();
            mundo.material = AssetDatabase.LoadAssetAtPath<Material>(MaterialArma);
            mundo.lugares = marcas.Select(t => t.position).ToArray();
            // o item vem no nome da marca: "Lugar de item (Comida)", "(Agua)", "(Municao9mm)"...
            mundo.itensDosLugares = marcas.Select(t => System.Enum.TryParse<Item>(t.name.Substring(t.name.IndexOf('(') + 1).TrimEnd(')'), out var item) ? item : Item.Comida).ToArray();
            mundo.girosDosLugares = marcas.Select(t => t.eulerAngles.y).ToArray();
            int municao = mundo.itensDosLugares.Count(Itens.EhMunicao);
            Debug.Log($"Itens no mundo: {marcas.Count - municao} lugares de comida e água e {municao} de munição.");
        }
    }
}
