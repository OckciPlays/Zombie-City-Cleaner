using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta o carro que se dirige: a importação do `Art/Veiculos/Carro.fbx` (feito pelo `Blender/carro.py`), os
    /// materiais (o vidro, os faróis e as lanternas acesos), o prefab `Prefabs/Veiculos/Carro.prefab` (a física, as
    /// rodas, as portas, os lugares, os faróis e o motor), a camada "Veiculo" e o carro na cena (a raiz "Carro", na rua
    /// em frente à casa da família). Também põe o capô nos carros da cidade (<see cref="CapoDaCidade"/> e o "Peças dos
    /// carros") e refaz o prefab do jogador, que ganha o <see cref="PassageiroDoCarro"/> e a <see cref="TelaDoCapo"/>.
    /// Menu: Zombie City Cleaner > Montar veículos. Pode rodar de novo: apaga e refaz a raiz "Carro".
    /// </summary>
    public static partial class MontagemDosVeiculos
    {
        const string Fbx = "Assets/_Game/Art/Veiculos/Carro.fbx";
        const string PastaMateriais = "Assets/_Game/Art/Veiculos/Materials";
        const string PastaMalhas = "Assets/_Game/Art/Veiculos/Malhas";
        const string TexturaDoPainel = "Assets/_Game/Art/Veiculos/Painel.png";
        const string PastaPrefabs = "Assets/_Game/Prefabs/Veiculos";
        const string PrefabCarro = PastaPrefabs + "/Carro.prefab";
        const string MaterialCidade = "Assets/_Game/Art/Materials/Cidade.mat";

        // as medidas do carro (as mesmas do carro.py, nos eixos da Unity)
        const float MeiaLargura = 0.86f, Eixo = 1.36f, XRoda = 0.735f, RaioRoda = 0.30f;
        const float Suspensao = 0.18f, AlvoDaSuspensao = 0.5f;
        static readonly float[] DobradicaZ = { 0.92f, 0.92f, -0.16f, -0.16f };
        static readonly float[] FimDaPortaZ = { -0.10f, -0.10f, -0.94f, -0.94f };
        static readonly Vector3[] Lugares =
        {
            new Vector3(-0.37f, 0.58f, 0.06f), new Vector3(0.37f, 0.58f, 0.06f),
            new Vector3(-0.38f, 0.60f, -0.68f), new Vector3(0.38f, 0.60f, -0.68f),
        };
        static readonly string[] NomesDasPortas = { "DE", "DD", "TE", "TD" };

        // onde o carro fica na cidade: na rua em frente à casa da família, encostado na calçada, virado para o oeste
        static readonly Vector3 NaCidade = new Vector3(-24f, 0f, -37.6f);
        const float YawNaCidade = 270f;

        [MenuItem("Zombie City Cleaner/Montar veículos")]
        public static void Montar()
        {
            Directory.CreateDirectory(PastaMateriais);
            Directory.CreateDirectory(PastaMalhas);
            Directory.CreateDirectory(PastaPrefabs);
            Camada();
            var mats = Materiais();
            Importar(mats);
            ImportarCofre();
            var prefab = MontarPrefab(mats);
            PorNaCena(prefab);
            CaposDaCidade(mats);
            // o jogador ganha o PassageiroDoCarro (o prefab é todo refeito pela montagem da rede)
            MontagemDaRede.MontarPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("Carro montado (Prefabs/Veiculos/Carro.prefab) e posto na rua em frente à casa da família.");
        }

        // ------------------------------------------------------------------------------------------ a camada

        /// <summary>
        /// A camada "Veiculo": o carro não bate nos zumbis (o atropelamento é por código: sem isso, cada zumbi seria um
        /// muro), nem na cápsula de perto deles (Ignore Raycast), nem nos braços de primeira pessoa ou no retrato.
        /// </summary>
        static void Camada()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var item = tags.FindProperty("layers").GetArrayElementAtIndex(Carro.CamadaVeiculo);
            if (string.IsNullOrEmpty(item.stringValue))
            {
                item.stringValue = "Veiculo";
                tags.ApplyModifiedProperties();
            }
            else if (item.stringValue != "Veiculo") Debug.LogWarning($"A camada {Carro.CamadaVeiculo} já se chama {item.stringValue}; o carro usa ela.");
            foreach (int outra in new[] { 2, Zumbi.CamadaZumbi, JogadorRede.CamadaBracos, JogadorRede.CamadaRetrato })
                Physics.IgnoreLayerCollision(Carro.CamadaVeiculo, outra, true);
        }

        // ------------------------------------------------------------------------------------------ materiais

        class Mats
        {
            public Material cidade, vidro, marcaFarol, marcaLanterna, farolAceso, lanternaAcesa;
            public Material painelApagado, painelAceso, ponteiroApagado, ponteiroAceso, indicador, tetoApagado, tetoAceso;
            public Material chaveMetal, chavePlastico, chaveiro, miolo, anelApagado, anelAceso, marca;
            public Mesh quadro, icone, ponteiroVel, ponteiroGas;
            public Material fantasma;
        }

        static Material Material(string nome, string shader)
        {
            string caminho = $"{PastaMateriais}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            var s = Shader.Find(shader);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, caminho); }
            m.shader = s;
            return m;
        }

        static Mats Materiais()
        {
            var r = new Mats { cidade = AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade) };
            if (r.cidade == null) throw new FileNotFoundException("Rode a montagem da cidade antes (o material da cidade).", MaterialCidade);

            // o vidro: escuro, transparente e liso, sem sombra (senão o carro por dentro fica no escuro)
            var v = Material("Vidro", "Universal Render Pipeline/Lit");
            v.shaderKeywords = new string[0];
            v.SetFloat("_Surface", 1f);
            v.SetFloat("_Blend", 0f);
            v.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            v.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            v.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            v.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            v.SetFloat("_ZWrite", 0f);
            v.SetFloat("_Cull", (float)CullMode.Back);
            v.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            v.SetOverrideTag("RenderType", "Transparent");
            v.renderQueue = (int)RenderQueue.Transparent;
            v.SetColor("_BaseColor", new Color(0.10f, 0.13f, 0.15f, 0.38f));
            v.SetFloat("_Smoothness", 0.92f);
            v.SetFloat("_Metallic", 0f);
            v.SetShaderPassEnabled("ShadowCaster", false);
            v.SetShaderPassEnabled("DepthOnly", false);
            v.SetShaderPassEnabled("DepthNormals", false);
            v.enableInstancing = true;
            EditorUtility.SetDirty(v);
            r.vidro = v;

            // as lentes apagadas: iguais ao material da cidade (a cor da paleta). São materiais à parte só para a
            // montagem achar o submesh delas; no carro, voltam a ser o da cidade
            r.marcaFarol = Copia("Marca_Farol", r.cidade);
            r.marcaLanterna = Copia("Marca_Lanterna", r.cidade);

            // acesas: Unlit com a cor em HDR (brilham no bloom), como a lâmpada do bunker
            r.farolAceso = Material("FarolAceso", "Universal Render Pipeline/Unlit");
            r.farolAceso.shaderKeywords = new string[0];
            r.farolAceso.SetColor("_BaseColor", new Color(1f, 0.93f, 0.78f) * 4f);
            r.farolAceso.enableInstancing = true;
            EditorUtility.SetDirty(r.farolAceso);
            r.lanternaAcesa = Material("LanternaAcesa", "Universal Render Pipeline/Unlit");
            r.lanternaAcesa.shaderKeywords = new string[0];
            r.lanternaAcesa.SetColor("_BaseColor", new Color(1f, 0.08f, 0.05f) * 2.2f);
            r.lanternaAcesa.enableInstancing = true;
            EditorUtility.SetDirty(r.lanternaAcesa);
            r.fantasma = Fantasma();

            // o painel: o quadro dos mostradores aceso (Unlit, a textura com um pouco de brilho) e apagado (Lit, para a
            // lanterna clarear), os ponteiros laranja, o ícone verde do farol e a lente da luz de teto
            var tex = PainelDoCarro.Textura(TexturaDoPainel);
            r.painelAceso = Unlit("PainelAceso", new Color(1f, 1f, 1f) * 1.6f, tex);
            r.painelApagado = Lit("PainelApagado", new Color(0.6f, 0.6f, 0.6f), 0.55f, tex);
            r.ponteiroAceso = Unlit("PonteiroAceso", new Color(1f, 0.32f, 0.08f) * 3f, null);
            r.ponteiroApagado = Lit("PonteiroApagado", new Color(0.7f, 0.22f, 0.08f), 0.4f, null);
            r.indicador = Unlit("PainelIndicador", Color.white * 2.4f, tex);
            r.tetoAceso = Unlit("TetoAceso", new Color(1f, 0.88f, 0.66f) * 3f, null);
            r.tetoApagado = Lit("TetoApagado", new Color(0.78f, 0.75f, 0.68f), 0.3f, null);
            // a ignição e a chave
            r.chaveMetal = Lit("ChaveMetal", new Color(0.78f, 0.76f, 0.70f), 0.7f, null);
            r.chaveMetal.SetFloat("_Metallic", 0.85f);
            r.chavePlastico = Lit("ChavePlastico", new Color(0.07f, 0.07f, 0.075f), 0.35f, null);
            r.chaveiro = Lit("Chaveiro", new Color(0.62f, 0.10f, 0.07f), 0.45f, null);
            r.miolo = Lit("IgnicaoMiolo", new Color(0.55f, 0.55f, 0.53f), 0.6f, null);
            r.miolo.SetFloat("_Metallic", 0.8f);
            r.anelApagado = Lit("IgnicaoAnel", new Color(0.12f, 0.12f, 0.12f), 0.4f, null);
            r.anelAceso = Unlit("IgnicaoAnelAceso", new Color(1f, 0.55f, 0.18f) * 2.2f, null);
            r.marca = Lit("IgnicaoMarca", new Color(0.85f, 0.85f, 0.8f), 0.3f, null);
            foreach (var mat in new[] { r.chaveMetal, r.miolo }) EditorUtility.SetDirty(mat);
            r.quadro = PainelDoCarro.Salvar(PainelDoCarro.MalhaDoQuadro(), PastaMalhas + "/Painel_Quadro.asset");
            r.icone = PainelDoCarro.Salvar(PainelDoCarro.MalhaDoIcone(), PastaMalhas + "/Painel_Icone.asset");
            r.ponteiroVel = PainelDoCarro.Salvar(PainelDoCarro.MalhaDoPonteiro("Painel_PonteiroVelocidade", 0.0375f, 0.0032f), PastaMalhas + "/Painel_PonteiroVelocidade.asset");
            r.ponteiroGas = PainelDoCarro.Salvar(PainelDoCarro.MalhaDoPonteiro("Painel_PonteiroGasolina", 0.025f, 0.0028f), PastaMalhas + "/Painel_PonteiroGasolina.asset");
            AssetDatabase.SaveAssets();
            return r;
        }

        static Material Unlit(string nome, Color cor, Texture textura)
        {
            var m = Material(nome, "Universal Render Pipeline/Unlit");
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", cor);
            m.SetTexture("_BaseMap", textura);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string nome, Color cor, float suavidade, Texture textura)
        {
            var m = Material(nome, "Universal Render Pipeline/Lit");
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", cor);
            m.SetTexture("_BaseMap", textura);
            m.SetFloat("_Smoothness", suavidade);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Copia(string nome, Material de)
        {
            string caminho = $"{PastaMateriais}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(de); AssetDatabase.CreateAsset(m, caminho); }
            else m.CopyPropertiesFromMaterial(de);
            m.shader = de.shader;
            m.shaderKeywords = de.shaderKeywords;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------ o FBX

        static void Importar(Mats m)
        {
            var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
            if (imp == null) throw new FileNotFoundException("Carro.fbx não encontrado (rode o Blender/carro.py).", Fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Carro"), m.cidade);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Vidro"), m.vidro);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Farol"), m.marcaFarol);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Lanterna"), m.marcaLanterna);
            imp.SaveAndReimport();
        }

        // a malha de uma peça do FBX e os materiais dela (na ordem dos submeshes)
        static (Mesh malha, Material[] mats) Peca(string nome)
        {
            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nome);
            if (t == null) throw new System.Exception($"A peça {nome} não está no {Fbx}.");
            return (t.GetComponent<MeshFilter>().sharedMesh, t.GetComponent<MeshRenderer>().sharedMaterials);
        }

        static GameObject Visual(Transform pai, string nome, string peca, Vector3 pos, Quaternion rot, bool sombra = true)
        {
            var (malha, mats) = Peca(peca);
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------------------------------ o prefab

        static GameObject MontarPrefab(Mats m)
        {
            var raiz = new GameObject("Carro") { layer = Carro.CamadaVeiculo };
            var corpo = raiz.AddComponent<Rigidbody>();
            corpo.mass = 1150f;
            corpo.linearDamping = 0.03f;
            corpo.angularDamping = 0.4f;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var no = raiz.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;   // o motorista sai da partida: o carro fica (volta para o anfitrião)
            var nt = raiz.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.UseQuaternionSynchronization = true;
            nt.Interpolate = true;
            raiz.AddComponent<NetworkRigidbody>();
            var carro = raiz.AddComponent<Carro>();

            // os zumbis desviam do carro (e o NavMesh assado não o conta: ele anda)
            var obstaculo = raiz.AddComponent<NavMeshObstacle>();
            obstaculo.shape = NavMeshObstacleShape.Box;
            obstaculo.center = new Vector3(0f, 0.75f, 0f);
            obstaculo.size = new Vector3(1.75f, 1.5f, 4.4f);
            obstaculo.carving = true;
            obstaculo.carveOnlyStationary = true;
            raiz.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            // o que se vê: a carroceria (com as lentes que acendem), o volante, as portas e as rodas
            var carroceria = Visual(raiz.transform, "Carroceria", "Carro_Carroceria", Vector3.zero, Quaternion.identity);
            var rc = carroceria.GetComponent<MeshRenderer>();
            var matsCarroceria = rc.sharedMaterials;
            carro.submeshFarol = System.Array.IndexOf(matsCarroceria, m.marcaFarol);
            carro.submeshLanterna = System.Array.IndexOf(matsCarroceria, m.marcaLanterna);
            if (carro.submeshFarol < 0 || carro.submeshLanterna < 0) Debug.LogWarning("Não achei os submeshes dos faróis e das lanternas no Carro.fbx.");
            for (int i = 0; i < matsCarroceria.Length; i++)
                if (matsCarroceria[i] == m.marcaFarol || matsCarroceria[i] == m.marcaLanterna) matsCarroceria[i] = m.cidade;
            rc.sharedMaterials = matsCarroceria;
            carro.carroceria = rc;
            carro.materialApagado = m.cidade;
            carro.farolAceso = m.farolAceso;
            carro.lanternaAceso = m.lanternaAcesa;
            carro.volante = Visual(raiz.transform, "Volante", "Carro_Volante", new Vector3(-0.37f, 0.86f, 0.50f), Quaternion.Euler(25f, 0f, 0f), false).transform;

            Painel(raiz.transform, carro, m);

            // o capô (a origem na dobradiça, atrás, em cima) e o gatilho em cima dele, que a mira procura. Ele fica acima da
            // caixa de colisão de baixo (o topo dela passa um pouco do capô), para a mira achar o gatilho antes
            carro.capo = Visual(raiz.transform, "Capô", "Carro_Capo", new Vector3(0f, AlturaDoCapo(ZCapo), ZCapo), Quaternion.identity).transform;
            var gatilhoDoCapo = new GameObject("Capô (mira)") { layer = Carro.CamadaVeiculo };
            gatilhoDoCapo.transform.SetParent(raiz.transform, false);
            var bcc = gatilhoDoCapo.AddComponent<BoxCollider>();
            bcc.isTrigger = true;
            bcc.center = new Vector3(0f, 0.98f, 1.62f);
            bcc.size = new Vector3(1.3f, 0.12f, 0.95f);
            gatilhoDoCapo.AddComponent<CapoDoCarro>().carro = carro;
            // o cofre do motor em 3D (o motor e as peças), o que se vê olhando dentro do capô
            carro.cofre = CofreDaFamilia(raiz.transform, carro, m);

            // o porta-malas: a tampa traseira (a origem na dobradiça, embaixo da borda do teto), o macaco e o estepe deitados
            // no piso e o gatilho na frente da tampa (para fora da caixa de colisão de baixo, que tapava o que fica dentro)
            carro.tampaTraseira = Visual(raiz.transform, "Tampa traseira", "Carro_Tampa", PivoDaTampa, Quaternion.identity).transform;
            carro.estepeNoPortaMalas = NoPortaMalas(raiz.transform, "Estepe", 3, new Vector3(0f, PisoDasMalas, -1.50f), m);
            carro.macacoNoPortaMalas = NoPortaMalas(raiz.transform, "Macaco", 4, new Vector3(0f, PisoDasMalas, -1.97f), m);
            var gatilhoDasMalas = new GameObject("Porta-malas (mira)") { layer = Carro.CamadaVeiculo };
            gatilhoDasMalas.transform.SetParent(raiz.transform, false);
            var bcm = gatilhoDasMalas.AddComponent<BoxCollider>();
            bcm.isTrigger = true;
            bcm.center = new Vector3(0f, 1.03f, -2.27f);
            bcm.size = new Vector3(1.22f, 0.86f, 0.16f);
            gatilhoDasMalas.AddComponent<PortaMalasDoCarro>().carro = carro;

            // o macaco: a peça que aparece apoiada embaixo do carro (o Carro a põe no lugar e a estica) e um gatilho do lado
            // de fora de cada roda (fora da caixa de colisão de baixo; embaixo do das portas), com o RodaDoCarro
            carro.macacoApoiado = NoPortaMalas(raiz.transform, "Macaco (apoiado)", 4, Vector3.zero, m);
            carro.macacoApoiado.enabled = false;
            var gatilhosDasRodas = new GameObject("Rodas (mira)") { layer = Carro.CamadaVeiculo }.transform;
            gatilhosDasRodas.SetParent(raiz.transform, false);
            string[] nomesDasRodas = { "DE", "DD", "TE", "TD" };
            for (int i = 0; i < 4; i++)
            {
                float lado = i % 2 == 0 ? -1f : 1f, frente = i < 2 ? 1f : -1f;
                var g = new GameObject("Roda " + nomesDasRodas[i] + " (mira)") { layer = Carro.CamadaVeiculo };
                g.transform.SetParent(gatilhosDasRodas, false);
                var bc = g.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = new Vector3(lado * 0.95f, 0.27f, frente * 1.30f);
                bc.size = new Vector3(0.16f, 0.54f, 1.40f);
                var r = g.AddComponent<RodaDoCarro>();
                r.carro = carro;
                r.roda = i;
            }

            carro.portas = new PortaDoCarro[4];
            carro.assentos = new Transform[4];
            carro.saidas = new Transform[4];
            var gatilhos = new GameObject("Portas (mira)") { layer = Carro.CamadaVeiculo }.transform;
            gatilhos.SetParent(raiz.transform, false);
            var lugares = new GameObject("Lugares") { layer = Carro.CamadaVeiculo }.transform;
            lugares.SetParent(raiz.transform, false);
            for (int i = 0; i < 4; i++)
            {
                int lado = i % 2 == 0 ? -1 : 1;
                var folha = Visual(raiz.transform, "Porta " + NomesDasPortas[i], "Carro_Porta_" + NomesDasPortas[i],
                    new Vector3(lado * MeiaLargura, 0f, DobradicaZ[i]), Quaternion.identity).transform;
                // o gatilho do lado de fora da porta fechada: é o que a mira procura
                float meio = (DobradicaZ[i] + FimDaPortaZ[i]) / 2f, comp = DobradicaZ[i] - FimDaPortaZ[i];
                var g = new GameObject("Porta " + NomesDasPortas[i]) { layer = Carro.CamadaVeiculo };
                g.transform.SetParent(gatilhos, false);
                var bc = g.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = new Vector3(lado * 0.92f, 0.78f, meio);
                bc.size = new Vector3(0.14f, 0.95f, comp - 0.04f);
                var porta = g.AddComponent<PortaDoCarro>();
                porta.carro = carro; porta.lugar = i; porta.folha = folha; porta.lado = lado;
                carro.portas[i] = porta;
                var assento = new GameObject("Lugar " + i) { layer = Carro.CamadaVeiculo }.transform;
                assento.SetParent(lugares, false);
                assento.localPosition = Lugares[i];
                carro.assentos[i] = assento;
                var saida = new GameObject("Saída " + i) { layer = Carro.CamadaVeiculo }.transform;
                saida.SetParent(lugares, false);
                saida.localPosition = new Vector3(lado * 1.55f, 0f, meio);
                carro.saidas[i] = saida;
            }

            // as rodas: a física (WheelCollider) e o que se vê (a da esquerda virada, com a calota para fora)
            var fisica = new GameObject("Rodas (física)") { layer = Carro.CamadaVeiculo }.transform;
            fisica.SetParent(raiz.transform, false);
            carro.rodas = new WheelCollider[4];
            carro.rodasVisuais = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                int lado = i % 2 == 0 ? -1 : 1;
                float z = i < 2 ? Eixo : -Eixo;
                var w = new GameObject("Roda " + NomesDasPortas[i]) { layer = Carro.CamadaVeiculo };
                w.transform.SetParent(fisica, false);
                // a roda pendura da posição do WheelCollider: em repouso (na metade do curso), ela fica no lugar do modelo
                w.transform.localPosition = new Vector3(lado * XRoda, RaioRoda + Suspensao * (1f - AlvoDaSuspensao), z);
                var wc = w.AddComponent<WheelCollider>();
                wc.radius = RaioRoda;
                wc.mass = 20f;
                wc.wheelDampingRate = 0.4f;
                wc.suspensionDistance = Suspensao;
                wc.forceAppPointDistance = 0.12f;
                wc.suspensionSpring = new JointSpring { spring = 32000f, damper = 3800f, targetPosition = AlvoDaSuspensao };
                wc.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.6f };
                wc.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.25f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 2f };
                carro.rodas[i] = wc;
                carro.rodasVisuais[i] = Visual(raiz.transform, "Roda " + NomesDasPortas[i] + " (modelo)", "Carro_Roda",
                    new Vector3(lado * XRoda, RaioRoda, z), Quaternion.Euler(0f, lado < 0 ? 180f : 0f, 0f)).transform;
            }

            // a colisão: a parte de baixo, a cabine e o para-brisa inclinado (não uma caixa do tamanho do carro todo)
            var colisao = new GameObject("Colisão") { layer = Carro.CamadaVeiculo }.transform;
            colisao.SetParent(raiz.transform, false);
            Caixa(colisao, "Baixo", new Vector3(0f, 0.58f, 0f), new Vector3(1.72f, 0.70f, 4.36f), Quaternion.identity);
            Caixa(colisao, "Cabine", new Vector3(0f, 1.19f, -0.82f), new Vector3(1.60f, 0.52f, 2.30f), Quaternion.identity);
            Caixa(colisao, "Para-brisa", new Vector3(0f, 1.18f, 0.65f), new Vector3(1.58f, 0.10f, 0.85f), Quaternion.Euler(33.9f, 0f, 0f));

            // os faróis: duas luzes quentes, sem sombra (desligados no começo)
            carro.farois = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                int lado = i == 0 ? -1 : 1;
                var f = new GameObject(lado < 0 ? "Farol E" : "Farol D") { layer = Carro.CamadaVeiculo };
                f.transform.SetParent(raiz.transform, false);
                f.transform.SetLocalPositionAndRotation(new Vector3(lado * 0.57f, 0.65f, 2.2f), Quaternion.Euler(5f, lado * 3f, 0f));
                var luz = f.AddComponent<Light>();
                luz.type = LightType.Spot;
                luz.color = new Color(1f, 0.92f, 0.78f);
                luz.intensity = 60f;
                luz.range = 34f;
                luz.spotAngle = 68f;
                luz.innerSpotAngle = 36f;
                luz.shadows = LightShadows.None;
                luz.enabled = false;
                carro.farois[i] = luz;
            }

            // a buzina (na grade da frente)
            var buz = new GameObject("Buzina") { layer = Carro.CamadaVeiculo };
            buz.transform.SetParent(raiz.transform, false);
            buz.transform.localPosition = new Vector3(0f, 0.55f, 2.05f);
            var somBuzina = buz.AddComponent<AudioSource>();
            somBuzina.playOnAwake = false;
            somBuzina.loop = true;
            somBuzina.spatialBlend = 1f;
            somBuzina.rolloffMode = AudioRolloffMode.Linear;
            somBuzina.minDistance = 4f;
            somBuzina.maxDistance = 70f;
            somBuzina.dopplerLevel = 0.3f;
            somBuzina.volume = 0f;
            carro.buzina = somBuzina;

            // o motor
            var motor = new GameObject("Motor") { layer = Carro.CamadaVeiculo };
            motor.transform.SetParent(raiz.transform, false);
            motor.transform.localPosition = new Vector3(0f, 0.6f, 1.6f);
            var som = motor.AddComponent<AudioSource>();
            som.playOnAwake = false;
            som.loop = true;
            som.spatialBlend = 1f;
            som.rolloffMode = AudioRolloffMode.Linear;
            som.minDistance = 3f;
            som.maxDistance = 45f;
            som.dopplerLevel = 0.3f;
            som.volume = 0f;
            carro.motor = som;

            // o motor de arranque (em laço, enquanto a chave fica na partida)
            var arr = new GameObject("Arranque") { layer = Carro.CamadaVeiculo };
            arr.transform.SetParent(raiz.transform, false);
            arr.transform.localPosition = new Vector3(0.25f, 0.45f, 1.45f);
            var somArranque = arr.AddComponent<AudioSource>();
            somArranque.playOnAwake = false;
            somArranque.loop = true;
            somArranque.spatialBlend = 1f;
            somArranque.rolloffMode = AudioRolloffMode.Linear;
            somArranque.minDistance = 3f;
            somArranque.maxDistance = 30f;
            somArranque.dopplerLevel = 0.3f;
            somArranque.volume = 0f;
            carro.arranque = somArranque;

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, PrefabCarro);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        /// <summary>
        /// O painel e as luzes de dentro: o quadro dos mostradores na frente do quadro de instrumentos do modelo (que olha
        /// para o motorista), os ponteiros (o eixo no centro de cada mostrador), o ícone do farol, a luz fraca do painel
        /// (perto do retrovisor, para clarear o volante e as mãos pela frente) e a luz de teto com a lente.
        /// </summary>
        static void Painel(Transform raiz, Carro carro, Mats m)
        {
            var quadro = Peca(raiz, "Painel (mostradores)", m.quadro, m.painelApagado, new Vector3(-0.37f, 0.85f, 0.626f));
            carro.painel = quadro;
            carro.painelApagado = m.painelApagado;
            carro.painelAceso = m.painelAceso;
            var vel = Peca(quadro.transform, "Ponteiro (velocidade)", m.ponteiroVel, m.ponteiroApagado, PainelDoCarro.CentroDoVelocimetro + new Vector3(0f, 0f, -0.002f));
            var gas = Peca(quadro.transform, "Ponteiro (gasolina)", m.ponteiroGas, m.ponteiroApagado, PainelDoCarro.CentroDaGasolina + new Vector3(0f, 0f, -0.002f));
            vel.transform.localRotation = Quaternion.Euler(0f, 0f, -Carro.AnguloDoVelocimetro(0f));
            gas.transform.localRotation = Quaternion.Euler(0f, 0f, -Carro.AnguloDoCombustivel(0f));
            carro.ponteiroVelocidade = vel.transform;
            carro.ponteiroCombustivel = gas.transform;
            carro.ponteiros = new[] { vel, gas };
            carro.ponteiroApagado = m.ponteiroApagado;
            carro.ponteiroAceso = m.ponteiroAceso;
            var icone = Peca(quadro.transform, "Luz do farol", m.icone, m.indicador, PainelDoCarro.CentroDoIcone + new Vector3(0f, 0f, -0.001f));
            icone.enabled = false;
            carro.indicadorFarol = icone;

            var luzPainel = new GameObject("Luz do painel") { layer = Carro.CamadaVeiculo };
            luzPainel.transform.SetParent(raiz, false);
            luzPainel.transform.localPosition = new Vector3(-0.25f, 1.30f, 0.36f);
            carro.luzDoPainel = LuzDeDentro(luzPainel, new Color(1f, 0.72f, 0.45f), 1.4f);

            // a luz de teto, no meio do forro
            var lente = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lente.name = "Lente da luz de teto";
            lente.layer = Carro.CamadaVeiculo;
            Object.DestroyImmediate(lente.GetComponent<Collider>());
            lente.transform.SetParent(raiz, false);
            lente.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.394f, -0.30f), Quaternion.identity);
            lente.transform.localScale = new Vector3(0.16f, 0.012f, 0.08f);
            var rl = lente.GetComponent<MeshRenderer>();
            rl.sharedMaterial = m.tetoApagado;
            rl.shadowCastingMode = ShadowCastingMode.Off;
            carro.lenteDoTeto = rl;
            carro.tetoApagado = m.tetoApagado;
            carro.tetoAceso = m.tetoAceso;
            var luzTeto = new GameObject("Luz de teto") { layer = Carro.CamadaVeiculo };
            luzTeto.transform.SetParent(raiz, false);
            luzTeto.transform.localPosition = new Vector3(0f, 1.36f, -0.30f);
            carro.luzDoTeto = LuzDeDentro(luzTeto, new Color(1f, 0.86f, 0.62f), 2.6f);

            Ignicao(raiz, carro, m);
        }

        /// <summary>Onde fica o miolo da ignição: no painel (a face de z = 0,62), à direita da coluna da direção, embaixo
        /// dos mostradores; do banco do motorista, ele aparece pelo vão de baixo, à direita, do volante.</summary>
        static readonly Vector3 LugarDaIgnicao = new Vector3(-0.23f, 0.655f, 0.62f);

        /// <summary>
        /// A ignição: o anel (que acende para achar a chave no escuro), o miolo cromado com as marcas das posições
        /// (desligada, ignição e partida) e a chave (o cabo de plástico preto, a haste de metal, a argola e o chaveiro
        /// vermelho). O grupo olha para dentro do painel (+Z); a chave gira em volta do Z dele.
        /// </summary>
        static void Ignicao(Transform raiz, Carro carro, Mats m)
        {
            var grupo = new GameObject("Ignição") { layer = Carro.CamadaVeiculo }.transform;
            grupo.SetParent(raiz, false);
            grupo.localPosition = LugarDaIgnicao;
            carro.ignicao = grupo;
            // o cilindro primitivo tem o eixo em Y, 1 de diâmetro e 2 de altura: deitado (90° em X), o eixo vai para Z
            var deitado = Quaternion.Euler(90f, 0f, 0f);
            carro.anelDaIgnicao = Primitiva(grupo, "Anel", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.002f), deitado, new Vector3(0.05f, 0.002f, 0.05f), m.anelApagado);
            carro.anelApagado = m.anelApagado;
            carro.anelAceso = m.anelAceso;
            Primitiva(grupo, "Miolo", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.007f), deitado, new Vector3(0.036f, 0.005f, 0.036f), m.miolo);
            // as marcas das posições, em volta do miolo (no sentido do relógio a partir de cima)
            foreach (float graus in new[] { 0f, Carro.ChaveLiga, Carro.ChavePartida })
            {
                var giro = Quaternion.Euler(0f, 0f, -graus);
                Primitiva(grupo, "Marca " + graus, PrimitiveType.Cube, giro * new Vector3(0f, 0.0225f, -0.0043f), giro, new Vector3(0.0018f, 0.004f, 0.001f), m.marca);
            }
            var chave = new GameObject("Chave") { layer = Carro.CamadaVeiculo }.transform;
            chave.SetParent(grupo, false);
            chave.localPosition = Vector3.zero;
            carro.chave = chave;
            var pecas = new List<MeshRenderer>
            {
                Primitiva(chave, "Haste", PrimitiveType.Cube, new Vector3(0f, 0f, -0.016f), Quaternion.identity, new Vector3(0.0035f, 0.009f, 0.010f), m.chaveMetal),
                Primitiva(chave, "Cabo", PrimitiveType.Cube, new Vector3(0f, 0.002f, -0.031f), Quaternion.identity, new Vector3(0.007f, 0.030f, 0.022f), m.chavePlastico),
                Primitiva(chave, "Argola", PrimitiveType.Cylinder, new Vector3(0f, -0.012f, -0.046f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.016f, 0.0012f, 0.016f), m.chaveMetal),
                Primitiva(chave, "Chaveiro", PrimitiveType.Cube, new Vector3(0f, -0.034f, -0.047f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.005f, 0.028f, 0.016f), m.chaveiro),
            };
            foreach (var p in pecas) p.enabled = false;   // sem motorista, sem chave
            carro.pecasDaChave = pecas.ToArray();
        }

        static MeshRenderer Primitiva(Transform pai, string nome, PrimitiveType tipo, Vector3 pos, Quaternion rot, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(tipo);
            go.name = nome;
            go.layer = Carro.CamadaVeiculo;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = escala;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        static Light LuzDeDentro(GameObject go, Color cor, float alcance)
        {
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = cor;
            luz.range = alcance;
            luz.intensity = 0f;
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            return luz;
        }

        static MeshRenderer Peca(Transform pai, string nome, Mesh malha, Material mat, Vector3 pos)
        {
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        static void Caixa(Transform pai, string nome, Vector3 centro, Vector3 tamanho, Quaternion rot)
        {
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(centro, rot);
            go.AddComponent<BoxCollider>().size = tamanho;
        }

        /// <summary>A borda de trás do capô (a dobradiça) e a altura de cima dele em z: as do carro.py (Z_CAPO, _y_capo).</summary>
        const float ZCapo = 1.145f;
        static float AlturaDoCapo(float z) => 0.915f + (z - 1.10f) * (0.84f - 0.915f) / (2.02f - 1.10f);

        /// <summary>A dobradiça da tampa traseira e o piso do porta-malas (em cima do carpete): os do carro.py (PIVO_TAMPA, Y_PISO_MALAS).</summary>
        static readonly Vector3 PivoDaTampa = new Vector3(0f, 1.445f, -1.91f);
        const float PisoDasMalas = 0.67f;

        // uma peça do pecas.py (a malha da montagem das armas) deitada no porta-malas, sem colisor
        static MeshRenderer NoPortaMalas(Transform pai, string nome, int peca, Vector3 pos, Mats m)
        {
            string caminho = MontagemDasArmas.MalhaDaPeca(peca);
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (malha == null) throw new FileNotFoundException("Rode a montagem das armas antes (as malhas das peças).", caminho);
            var go = new GameObject(nome) { layer = Carro.CamadaVeiculo };
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m.cidade;
            return r;
        }

        // ------------------------------------------------------------------------------------------ a cena

        static void PorNaCena(GameObject prefab)
        {
            // na raiz da cena (um NetworkObject da cena, como o "Bunker (rede)")
            var cena = EditorSceneManager.GetActiveScene();
            foreach (var velho in cena.GetRootGameObjects().Where(g => g.name == "Veículos" || g.name == "Carro").ToArray())
                Object.DestroyImmediate(velho);
            var carro = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
            carro.name = "Carro";
            carro.transform.SetLocalPositionAndRotation(NaCidade, Quaternion.Euler(0f, YawNaCidade, 0f));
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        /// <summary>
        /// O capô dos carros da cidade (os "Carro" dentro da raiz "Cidade", de pé e dentro da barreira): um gatilho em
        /// volta da frente de cada um (na camada Ignore Raycast, fora do NavMesh), com o <see cref="CapoDaCidade"/>, o
        /// "Capô aberto" (o capô recortado da malha e o cofre por dentro, <see cref="AbrirCarroDaCidade"/>) e o
        /// NetworkObject "Peças dos carros" (<see cref="PecasDosCarros"/>), com o que sobrou em cada um. A montagem da
        /// cidade apaga os gatilhos (refaz os carros): depois dela, rode esta de novo.
        /// </summary>
        static void CaposDaCidade(Mats m)
        {
            _recortes.Clear();
            var cena = EditorSceneManager.GetActiveScene();
            foreach (var velho in cena.GetRootGameObjects().Where(g => g.name == "Peças dos carros").ToArray())
                Object.DestroyImmediate(velho);
            var cidade = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Cidade");
            if (cidade == null)
            {
                Debug.LogWarning("Sem a raiz \"Cidade\": os carros da cidade ficaram sem capô.");
                return;
            }
            var raiz = new GameObject("Peças dos carros");
            raiz.AddComponent<NetworkObject>();
            var pecas = raiz.AddComponent<PecasDosCarros>();
            var capos = new List<CapoDaCidade>();
            foreach (var mf in cidade.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name == "Carro").ToArray())
            {
                foreach (var velho in mf.GetComponentsInChildren<CapoDaCidade>(true)) Object.DestroyImmediate(velho.gameObject);
                foreach (Transform velho in mf.transform.Cast<Transform>().Where(x => x.name == "Capô aberto").ToArray()) Object.DestroyImmediate(velho.gameObject);
                var fechado = mf.GetComponent<MeshRenderer>();
                if (fechado != null) fechado.enabled = true;
                var t = mf.transform;
                // o capotado não abre; os de fora da barreira (os das barricadas) não se alcançam
                if (t.up.y < 0.5f || new Vector2(t.position.x, t.position.z).magnitude > 60f) continue;
                var b = mf.sharedMesh.bounds;
                // a altura do capô: um raio de cima na malha (um colisor temporário)
                var medidor = new GameObject("Medidor temporário").AddComponent<MeshCollider>();
                medidor.sharedMesh = mf.sharedMesh;
                float zCapo = b.max.z - 0.6f;
                float yCapo = medidor.Raycast(new Ray(new Vector3(b.center.x, b.max.y + 1f, zCapo), Vector3.down), out var acerto, 5f)
                    ? acerto.point.y : b.center.y;
                Object.DestroyImmediate(medidor.gameObject);
                // o gatilho envolve a frente do carro (um pouco maior que a caixa de colisão dele, para a mira achar o
                // gatilho antes dela); o ponto dele é o meio do capô
                var g = new GameObject("Capô") { layer = 2 };
                g.transform.SetParent(t, false);
                g.transform.localPosition = new Vector3(b.center.x, yCapo, zCapo);
                g.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                var bc = g.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                float z0 = b.max.z - 1.25f, z1 = b.max.z + 0.08f;
                bc.center = new Vector3(0f, (b.min.y + b.max.y + 0.08f) / 2f - yCapo, (z0 + z1) / 2f - zCapo);
                bc.size = new Vector3(b.size.x + 0.1f, b.size.y + 0.08f, z1 - z0);
                var capo = g.AddComponent<CapoDaCidade>();
                capo.pecas = pecas;
                capo.indice = capos.Count;
                capo.nome = NomeDoCarro(mf.sharedMesh.name);
                if (!AbrirCarroDaCidade(mf, capo, m))
                {
                    Object.DestroyImmediate(g);
                    continue;
                }
                capos.Add(capo);
            }
            pecas.capos = capos.ToArray();
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Capô em {capos.Count} carros da cidade.");
        }

        // "Carro_Sedan_Cinza" vira "Sedã cinza"; "Carro_Pickup_Branco", "Picape branca"
        static string NomeDoCarro(string malha)
        {
            var partes = malha.Split('_');
            if (partes.Length < 3) return "Carro";
            bool picape = partes[1] == "Pickup";
            string cor = partes[2].ToLowerInvariant();
            if (picape && cor == "branco") cor = "branca";
            return (picape ? "Picape " : partes[1] == "Sedan" ? "Sedã " : partes[1] + " ") + cor;
        }
    }
}
