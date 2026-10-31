using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O cupê (29/10/2026): o segundo carro que se dirige, um cupê fastback dos anos 70, preto com as faixas cinza
    /// (`Art/Veiculos/Cupe/Cupe.fbx`, feito pelo `Blender/Cupe/cupe.py`). O prefab `Prefabs/Veiculos/Cupe.prefab` é uma
    /// cópia do da perua (`Carro.prefab`: a mesma física, rede, motor e chave, peças, pneus, macaco, capô e porta-malas,
    /// sons e luzes) com o visual do cupê e os pontos que dependem da forma dele:
    ///  - o chassi é o mesmo (as rodas e os WheelColliders não mudam); o lugar do motorista (o volante, os mostradores, a
    ///    ignição) é o da perua 8 cm mais baixo; os lugares, as saídas, a colisão (com a queda do fastback), os faróis, a
    ///    luz de teto, o cofre do motor (3 cm mais baixo, embaixo do capô mais baixo) e o porta-malas (a tampa deitada,
    ///    o estepe e o macaco no piso) seguem o cupê;
    ///  - <b>as 2 portas e 2 lugares</b> (os da frente; `Carro.lugaresUsados` = 2): a mira na porta comprida (um gatilho
    ///    só, de ponta a ponta) leva ao banco da frente daquele lado. Os bancos de trás continuam desenhados, sem ninguém;
    ///  - é o carro rápido: chega a 110 km/h (<see cref="VelocidadeMaxima"/>, <see cref="Torque"/>).
    /// Também põe o cupê na lista de prefabs da rede, no `MundoDaPartida.segundoCarro` da cena Mundo (o anfitrião o cria
    /// ao lado da laje da partida) e, na Demo_Cidade, atrás da perua. A perua não muda.
    /// Menu: Zombie City Cleaner > Veículos > Montar o cupê. Depois de um "Montar veículos", rode de novo.
    /// </summary>
    public static class MontagemDoCupe
    {
        const string Pasta = "Assets/_Game/Art/Veiculos/Cupe";
        const string Fbx = Pasta + "/Cupe.fbx";
        const string PastaTexturas = Pasta + "/Texturas";
        const string PastaMateriais = Pasta + "/Materiais";
        const string PrefabPerua = "Assets/_Game/Prefabs/Veiculos/Carro.prefab";
        public const string Prefab = "Assets/_Game/Prefabs/Veiculos/Cupe.prefab";

        // as medidas do cupê (as do cupe.py, nos eixos da Unity)
        const float MeiaLargura = 0.89f, Descer = -0.08f;
        static readonly Vector3 PivoDoCapo = new Vector3(0f, 0.8968f, 1.145f);
        static readonly Vector3 PivoDaTampa = new Vector3(0f, 0.968f, -1.76f);
        const float PisoDasMalas = 0.52f;
        static readonly Vector3[] Lugares =
        {
            new Vector3(-0.37f, 0.50f, 0.06f), new Vector3(0.37f, 0.50f, 0.06f),
            new Vector3(-0.36f, 0.48f, -0.64f), new Vector3(0.36f, 0.48f, -0.64f),
        };
        // a mira (29/10/2026, a pedido: "quando eu miro para entrar no banco do motorista, algumas vezes estou indo para o
        // banco de trás"; depois, "esse carro preto só vai ter apenas dois lugares"): o gatilho da porta cobre a porta
        // comprida inteira e leva ao banco da frente; os bancos de trás não são usados (`Carro.lugaresUsados` = 2) e os
        // gatilhos deles ficam desligados
        const float MeioDaPorta = 0.31f, CompDaPorta = 1.18f;
        // o ponto de saída de cada lugar, do lado de fora (a metade da frente da porta e a de trás)
        static readonly float[] ZDaSaida = { 0.61f, 0.61f, -0.04f, -0.04f };
        // o cupê é o carro rápido (a pedido em 29/10/2026: "ele precisa chegar a 110 km de velocidade"); a perua fica em
        // 22 m/s e 700 N·m. A força cai a zero na velocidadeMaxima e o atrito segura antes: com 33 e 950 ele parou em
        // 93 km/h (medido numa pista reta, pneus bons); com 42 e 1.000, a conta dá ~112 km/h. O som das marchas vai até 32
        public const float VelocidadeMaxima = 42f, Torque = 1000f, FimDasMarchas = 32f;

        [MenuItem("Zombie City Cleaner/Veículos/Montar o cupê")]
        public static void Montar()
        {
            if (!File.Exists(Fbx)) throw new FileNotFoundException("Rode o Blender/Cupe/rodar.py (compor_exportar) antes.", Fbx);
            var mats = Materiais();
            Importar(mats);
            var prefab = MontarPrefab(mats);
            NaRede(prefab);
            NoMundo(prefab);
            NaDemo(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cupê montado ({Prefab}): na lista da rede, no MundoDaPartida da cena Mundo e na Demo_Cidade.");
        }

        // ------------------------------------------------------------------------------------------ materiais e FBX
        class Mats { public Material lataria, interior, vidro, farol, lanterna; }

        static Mats Materiais()
        {
            foreach (var atlas in new[] { "Cupe_Lataria", "Cupe_Interior" })
            {
                Textura($"{PastaTexturas}/{atlas}_Cor.png", true, false);
                Textura($"{PastaTexturas}/{atlas}_MS.png", false, false);
                Textura($"{PastaTexturas}/{atlas}_Normal.png", false, true);
            }
            var r = new Mats
            {
                lataria = Pbr("Cupe_Lataria", "Cupe_Lataria", 0.85f),
                interior = Pbr("Cupe_Interior", "Cupe_Interior", 1f),
                lanterna = Pbr("Cupe_Lanterna", "Cupe_Lataria", 0.5f),
                vidro = Transparente("Cupe_Vidro", new Color(0.09f, 0.11f, 0.13f, 0.36f), 0.93f, null),
                farol = Transparente("Cupe_Farol", new Color(0.92f, 0.94f, 0.96f, 0.14f), 0.96f,
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/Cupe_Lataria_Normal.png")),
            };
            AssetDatabase.SaveAssets();
            return r;
        }

        static void Importar(Mats m)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.isReadable = false;
            imp.generateSecondaryUV = false;
            imp.bakeAxisConversion = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var (nome, mat) in new[] { ("Cupe_Lataria", m.lataria), ("Cupe_Interior", m.interior), ("Cupe_Vidro", m.vidro), ("Cupe_Farol", m.farol), ("Cupe_Lanterna", m.lanterna) })
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nome), mat);
            imp.SaveAndReimport();
        }

        static (Mesh malha, Material[] mats) Peca(string nome)
        {
            var mf = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx).GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.name == nome);
            if (mf == null) throw new System.Exception($"A peça {nome} não está no {Fbx}.");
            return (mf.sharedMesh, mf.GetComponent<MeshRenderer>().sharedMaterials);
        }

        // ------------------------------------------------------------------------------------------ o prefab
        static GameObject MontarPrefab(Mats m)
        {
            var raiz = PrefabUtility.LoadPrefabContents(PrefabPerua);
            try
            {
                raiz.name = "Cupe";
                var carro = raiz.GetComponent<Carro>();
                Transform T(string nome) => raiz.transform.Find(nome) ?? throw new System.Exception("Falta " + nome + " no prefab da perua.");

                // o visual
                Trocar(T("Carroceria"), "Cupe_Carroceria", Vector3.zero, Quaternion.identity);
                Trocar(T("Capô"), "Cupe_Capo", PivoDoCapo, Quaternion.identity);
                Trocar(T("Tampa traseira"), "Cupe_Tampa", PivoDaTampa, Quaternion.identity);
                Trocar(T("Volante"), "Cupe_Volante", new Vector3(-0.37f, 0.86f + Descer, 0.50f), Quaternion.Euler(25f, 0f, 0f));
                Trocar(T("Porta DE"), "Cupe_Porta_E", new Vector3(-MeiaLargura, 0f, 0.92f), Quaternion.identity);
                Trocar(T("Porta DD"), "Cupe_Porta_D", new Vector3(MeiaLargura, 0f, 0.92f), Quaternion.identity);
                foreach (var n in new[] { "Roda DE (modelo)", "Roda DD (modelo)", "Roda TE (modelo)", "Roda TD (modelo)" })
                {
                    var t = T(n);
                    Trocar(t, "Cupe_Roda", t.localPosition, t.localRotation);
                }
                // as portas de trás da perua não existem no cupê
                Object.DestroyImmediate(T("Porta TE").gameObject);
                Object.DestroyImmediate(T("Porta TD").gameObject);
                var lentes = carro.carroceria.sharedMaterials;
                carro.submeshFarol = System.Array.IndexOf(lentes, m.farol);
                carro.submeshLanterna = System.Array.IndexOf(lentes, m.lanterna);
                if (carro.submeshFarol < 0 || carro.submeshLanterna < 0) throw new System.Exception("Sem os submeshes das lentes na Cupe_Carroceria.");

                // as portas (os 4 lugares, 2 folhas), os lugares e as saídas
                var folhas = new[] { T("Porta DE"), T("Porta DD") };
                var gatilhos = T("Portas (mira)");
                string[] nomes = { "Porta E (frente)", "Porta D (frente)", "Porta E (banco de trás)", "Porta D (banco de trás)" };
                for (int i = 0; i < 4; i++)
                {
                    var porta = carro.portas[i];
                    int lado = i % 2 == 0 ? -1 : 1;
                    porta.folha = folhas[i % 2];
                    porta.lado = lado;
                    porta.name = nomes[i];
                    var bc = porta.GetComponent<BoxCollider>();
                    bc.center = new Vector3(lado * 0.95f, 0.66f, MeioDaPorta);
                    bc.size = new Vector3(0.14f, 0.80f, CompDaPorta);
                    // só os 2 lugares da frente (a pedido em 29/10/2026: "esse carro preto só vai ter apenas dois lugares"):
                    // os de trás ficam no prefab (os vetores do carro têm 4), sem mira e sem ninguém
                    bool frente = i < 2;
                    bc.enabled = frente;
                    porta.lugarDeTras = -1;
                    carro.assentos[i].localPosition = Lugares[i];
                    carro.saidas[i].localPosition = new Vector3(lado * 1.55f, 0f, ZDaSaida[i]);
                }

                // a colisão: a parte de baixo, a cabine (o teto reto), o para-brisa e o vidro de trás inclinados
                var colisao = T("Colisão");
                Caixa(colisao, "Baixo", new Vector3(0f, 0.55f, -0.06f), new Vector3(1.78f, 0.66f, 4.46f), Quaternion.identity);
                Caixa(colisao, "Cabine", new Vector3(0f, 1.115f, -0.25f), new Vector3(1.60f, 0.45f, 1.10f), Quaternion.identity);
                Caixa(colisao, "Para-brisa", new Vector3(0f, 1.105f, 0.66f), new Vector3(1.56f, 0.10f, 0.85f), Quaternion.Euler(32f, 0f, 0f));
                Caixa(colisao, "Vidro de trás", new Vector3(0f, 1.15f, -1.27f), new Vector3(1.50f, 0.10f, 1.01f), Quaternion.Euler(-21.7f, 0f, 0f));
                var obst = raiz.GetComponent<NavMeshObstacle>();
                obst.center = new Vector3(0f, 0.68f, -0.06f);
                obst.size = new Vector3(1.80f, 1.36f, 4.60f);

                // os gatilhos do capô, do porta-malas e das rodas
                var capo = T("Capô (mira)").GetComponent<BoxCollider>();
                capo.center = new Vector3(0f, 0.94f, 1.62f);
                capo.size = new Vector3(1.3f, 0.12f, 0.95f);
                var malas = T("Porta-malas (mira)").GetComponent<BoxCollider>();
                malas.center = new Vector3(0f, 1.02f, -2.0f);
                malas.size = new Vector3(1.22f, 0.12f, 0.50f);
                foreach (Transform g in T("Rodas (mira)"))
                {
                    var bc = g.GetComponent<BoxCollider>();
                    bc.center = new Vector3(Mathf.Sign(bc.center.x) * 0.97f, bc.center.y, bc.center.z);
                }
                // a tampa deitada abre menos (a borda de trás sobe; o mesmo sentido da tampa da perua)
                carro.aberturaDaTampa = 60f;
                carro.velocidadeMaxima = VelocidadeMaxima;
                carro.torque = Torque;
                carro.fimDasMarchas = FimDasMarchas;
                carro.lugaresUsados = 2;
                // o cupê não tem chave (só a ligação direta): a chave da perua não serve nele. (Pela propriedade
                // serializada: o campo é novo no Carro e pode não existir numa versão sem a chave.)
                var so = new SerializedObject(carro);
                var chave = so.FindProperty("chaveDoVeiculo");
                if (chave != null)
                {
                    chave.intValue = 0;   // Item.Nada
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                T("Estepe").localPosition = new Vector3(0f, PisoDasMalas, -1.78f);
                T("Macaco").localPosition = new Vector3(0f, PisoDasMalas, -2.15f);

                // o lugar do motorista (8 cm mais baixo), as luzes de dentro, os faróis e o cofre
                T("Painel (mostradores)").localPosition = new Vector3(-0.37f, 0.85f + Descer, 0.626f);
                T("Ignição").localPosition = new Vector3(-0.23f, 0.655f + Descer, 0.62f);
                T("Luz do painel").localPosition = new Vector3(-0.25f, 1.22f, 0.36f);
                T("Lente da luz de teto").localPosition = new Vector3(0f, 1.284f, -0.30f);
                T("Luz de teto").localPosition = new Vector3(0f, 1.25f, -0.30f);
                T("Farol E").localPosition = new Vector3(-0.66f, 0.655f, 2.21f);
                T("Farol D").localPosition = new Vector3(0.66f, 0.655f, 2.21f);
                var cofre = T("Cofre do motor");
                cofre.localPosition = cofre.localPosition + new Vector3(0f, -0.03f, 0f);

                var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
                AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate);
                return AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        static void Trocar(Transform t, string peca, Vector3 pos, Quaternion rot)
        {
            var (malha, mats) = Peca(peca);
            t.SetLocalPositionAndRotation(pos, rot);
            t.GetComponent<MeshFilter>().sharedMesh = malha;
            t.GetComponent<MeshRenderer>().sharedMaterials = mats;
        }

        static void Caixa(Transform pai, string nome, Vector3 centro, Vector3 tamanho, Quaternion rot)
        {
            var t = pai.Find(nome);
            if (t == null)
            {
                t = new GameObject(nome) { layer = Carro.CamadaVeiculo }.transform;
                t.SetParent(pai, false);
                t.gameObject.AddComponent<BoxCollider>();
            }
            t.SetLocalPositionAndRotation(centro, rot);
            var bc = t.GetComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = tamanho;
        }

        // ------------------------------------------------------------------------------------------ a rede e as cenas
        static void NaRede(GameObject prefab)
        {
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista != null && !lista.PrefabList.Any(p => p.Prefab == prefab))
            {
                lista.Add(new NetworkPrefab { Prefab = prefab });
                EditorUtility.SetDirty(lista);
            }
        }

        /// <summary>No `MundoDaPartida` da cena Mundo: o anfitrião cria o cupê ao lado da laje da partida.</summary>
        static void NoMundo(GameObject prefab)
        {
            var cena = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Mundo.unity", OpenSceneMode.Single);
            var partida = cena.GetRootGameObjects().Select(g => g.GetComponentInChildren<MundoDaPartida>(true)).FirstOrDefault(p => p != null);
            if (partida == null) { Debug.LogWarning("Sem o MundoDaPartida na cena Mundo: o cupê não foi posto no mundo."); return; }
            partida.segundoCarro = prefab;
            EditorUtility.SetDirty(partida);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        /// <summary>Na Demo_Cidade: o cupê na rua, atrás da perua (virado para o mesmo lado).</summary>
        static void NaDemo(GameObject prefab)
        {
            var cena = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Demo_Cidade.unity", OpenSceneMode.Single);
            foreach (var velho in cena.GetRootGameObjects().Where(g => g.name == "Cupe").ToArray()) Object.DestroyImmediate(velho);
            var perua = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Carro");
            if (perua == null) { Debug.LogWarning("Sem a perua na Demo_Cidade: o cupê não foi posto lá."); return; }
            var cupe = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
            cupe.name = "Cupe";
            cupe.transform.SetPositionAndRotation(perua.transform.position - perua.transform.forward * 6.5f, perua.transform.rotation);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        // ------------------------------------------------------------------------------------------ texturas e materiais
        static void Textura(string caminho, bool srgb, bool normal)
        {
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura do cupê não encontrada.", caminho);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static Material Novo(string nome)
        {
            Directory.CreateDirectory(PastaMateriais);
            string caminho = $"{PastaMateriais}/{nome}.mat";
            var s = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, caminho); }
            m.shader = s;
            m.shaderKeywords = new string[0];
            m.enableInstancing = true;
            return m;
        }

        static Material Pbr(string nome, string atlas, float oclusao)
        {
            var m = Novo(nome);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Cor.png"));
            m.SetColor("_BaseColor", Color.white);
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_MS.png");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Normal.png"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ms);
            m.SetFloat("_OcclusionStrength", oclusao);
            m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Transparente(string nome, Color cor, float suavidade, Texture normal)
        {
            var m = Novo(nome);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", suavidade);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
