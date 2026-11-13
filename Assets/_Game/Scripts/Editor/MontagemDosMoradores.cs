using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Moradores;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A montagem dos moradores do bunker (o Plano_NPCs_Bunker.md da pasta de arte).
    ///  - A tela (a parte 3): o <c>Settings/Resources/RecursosDosMoradores.asset</c>, com o NPC vestível das roupas, que a
    ///    <see cref="TelaDosMoradores"/> carrega antes de haver partida. Menu: Zombie City Cleaner > Moradores > Montar a tela.
    ///  - O corpo (a parte 6, 06/11/2026): o controlador dos moradores (a locomoção pelo Speed e pelo Mancando, com o passo
    ///    na rapidez de cada um, e as ações da parte 5: sentar e levantar, deitar, comer, as bancadas, a estante e a
    ///    sinuca; a camada "Cima", do peito para cima, com a caixa, a conversa, o choro e a tosse) e o prefab
    ///    <c>Prefabs/Moradores/Morador.prefab</c> (o NetworkObject com o NetworkTransform do anfitrião, o NavMeshAgent, a
    ///    cápsula e o <see cref="CorpoDoMorador"/>; o "Corpo" é a cópia do do <c>Jogador.prefab</c>, como o dos sobreviventes,
    ///    com o prato, a comida, a colher e o bocado do <c>Loot.fbx</c> nos vazios <c>Morador_*</c>), na lista dos prefabs da
    ///    rede e nos recursos. Menu: Zombie City Cleaner > Moradores > Montar o corpo.
    /// </summary>
    public static class MontagemDosMoradores
    {
        public const string Pasta = "Assets/_Game/Settings/Resources";
        public const string Recursos = Pasta + "/" + RecursosDosMoradores.NoResources + ".asset";
        const string Npc = "Assets/_Game/Prefabs/Roupas/NPC.prefab";
        public const string PastaDoCorpo = "Assets/_Game/Prefabs/Moradores";
        public const string Prefab = PastaDoCorpo + "/Morador.prefab";
        public const string Controlador = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco_Morador.controller";
        const string Mascara = MontagemDasArmas.ArtePersonagens + "/Personagem_PrincipalBloco_Tronco.mask";
        const string ArteDoLivro = "Assets/_Game/Art/Moradores";
        /// <summary>A página do livro aberto (o LIVRO do bloco_morador.py: 0,15 × 0,22 m em ×1,30).</summary>
        static readonly Vector2 Pagina = new Vector2(0.15f, 0.22f) * 1.3f;

        /// <summary>
        /// O giro das malhas do Loot.fbx dentro dos vazios Morador_* do corpo (os vazios ficam no espaço da peça do loot.py;
        /// o Loot.fbx sai com os eixos da Unity, e o vazio fica com o giro de -90° em X da conversão): conferido no Comer, com o
        /// prato deitado na mesa (a base em 0,99 m) e a comida em cima, e o bocado da colher chegando na boca.
        /// </summary>
        public static readonly Quaternion GiroDasPecas = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>As peças nos vazios: o vazio e as malhas do Loot.fbx (o prato fundo e a comida; a colher; o bocado).</summary>
        static readonly (string vazio, string[] malhas)[] Pecas =
        {
            ("Morador_Prato", new[] { "PratoFundo_corpo", "PratoComida_corpo" }),
            ("Morador_Colher", new[] { "Colher_corpo" }),
            ("Morador_Bocado", new[] { "ColherBocado_corpo" }),
        };

        [MenuItem("Zombie City Cleaner/Moradores/Montar a tela")]
        public static void Montar()
        {
            var r = OsRecursos();
            r.npc = AssetDatabase.LoadAssetAtPath<GameObject>(Npc);
            EditorUtility.SetDirty(r);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Moradores] {Recursos}: o NPC {(r.npc != null ? r.npc.name : "não achado")}.");
        }

        static RecursosDosMoradores OsRecursos()
        {
            Directory.CreateDirectory(Pasta);
            var r = AssetDatabase.LoadAssetAtPath<RecursosDosMoradores>(Recursos);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<RecursosDosMoradores>();
                AssetDatabase.CreateAsset(r, Recursos);
            }
            return r;
        }

        [MenuItem("Zombie City Cleaner/Moradores/Montar o corpo")]
        public static void MontarOCorpo()
        {
            var ctrl = MontarControlador();
            var prefab = MontarPrefab(ctrl);
            PorNaRede(prefab);
            var r = OsRecursos();
            if (r.npc == null) r.npc = AssetDatabase.LoadAssetAtPath<GameObject>(Npc);
            r.corpo = prefab;
            EditorUtility.SetDirty(r);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Moradores] O corpo dos moradores: {Prefab}, com o controlador {Controlador}.");
        }

        // ------------------------------------------------------------------ o controlador

        public static AnimationClip Clipe(string nome)
        {
            var clipe = AssetDatabase.LoadAllAssetsAtPath(MontagemDasArmas.FbxDoPersonagem(0)).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == nome && !c.name.StartsWith("__preview__"));
            if (clipe == null) Debug.LogWarning($"[Moradores] O FBX do personagem não tem o clipe \"{nome}\" (exporte o bloco_morador.py).");
            return clipe;
        }

        /// <summary>As ações da base: o clipe, para onde ele vai sozinho no fim (nulo: fica, é laço).</summary>
        static readonly (string clipe, string depois)[] Acoes =
        {
            ("Sentar_Cadeira", "Sentado_Cadeira"), ("Sentado_Cadeira", null), ("Levantar_Cadeira", "Locomocao"),
            ("Sentado_Lendo", null), ("Sentado_Rezando", null), ("Sentado_Triste", null),
            ("Sentar_Sofa", "Sentado_Sofa"), ("Sentado_Sofa", null), ("Levantar_Sofa", "Locomocao"),
            ("Sentar_Banco", "Sentado_Banco"), ("Sentado_Banco", null), ("Levantar_Banco", "Locomocao"), ("Comer", null),
            ("Deitar", "Dormindo"), ("Dormindo", null), ("Levantar_Cama", "Locomocao"),
            ("Cozinhar", null), ("Lavar_Louca", null), ("Consertar", null),
            ("Estante_Alto", "Locomocao"), ("Estante_Meio", "Locomocao"), ("Estante_Baixo", "Locomocao"), ("Estante_Chao", "Locomocao"),
            ("Sinuca_Tacada", "Locomocao"),
            // a parte 8 (o lazer): dobrar a roupa na mesa da lavanderia
            ("Dobrar_Roupa", null),
        };

        /// <summary>
        /// Os da camada de cima (do peito para cima): a caixa, a conversa, o choro e a tosse (a parte 5) e, na parte 8, o taco
        /// em pé e o fumo (o maço, o acender, o cigarro, a tragada e o peteleco, os clipes do jogador). Com o depois: para onde
        /// ele vai sozinho no fim.
        /// </summary>
        static readonly (string clipe, string depois)[] DeCima =
        {
            ("Caixa_Carregar", null), ("Conversa", null), ("Chorar", null), ("Tossir", "Vazio"),
            ("Taco_Segurar", null), ("Maco_Segurar", null), ("Maco_Acender", "Cigarro_Segurar"), ("Cigarro_Segurar", null),
            ("Cigarro_Tragar", "Cigarro_Segurar"), ("Cigarro_Peteleco", "Vazio"),
        };

        public static AnimatorController MontarControlador()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador) != null) AssetDatabase.DeleteAsset(Controlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Mancando", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Passo", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var base_ = ctrl.layers[0].stateMachine;
            // a locomoção: parado e andando (normal e mancando), o passo na rapidez dele (o CorpoDoMorador põe o Passo)
            var loc = base_.AddState("Locomocao", new Vector3(300f, 0f, 0f));
            var arvore = new BlendTree
            {
                name = "Locomocao", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "Speed", blendParameterY = "Mancando",
                useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(arvore, ctrl);
            arvore.AddChild(Clipe("Parado"), new Vector2(0f, 0f));
            arvore.AddChild(Clipe("Andar"), new Vector2(1f, 0f));
            arvore.AddChild(Clipe("Parado"), new Vector2(0f, 1f));
            arvore.AddChild(Clipe("Andar_Mancando"), new Vector2(1f, 1f));
            loc.motion = arvore;
            loc.speedParameterActive = true;
            loc.speedParameter = "Passo";
            base_.defaultState = loc;

            var estados = new System.Collections.Generic.Dictionary<string, AnimatorState> { ["Locomocao"] = loc };
            int i = 0;
            foreach (var (clipe, _) in Acoes)
            {
                var s = base_.AddState(clipe, new Vector3(600f + (i / 8) * 260f, (i % 8) * 60f - 200f, 0f));
                s.motion = Clipe(clipe);
                estados[clipe] = s;
                i++;
            }
            foreach (var (clipe, depois) in Acoes)
            {
                if (depois == null) continue;
                var t = estados[clipe].AddTransition(estados[depois]);
                t.hasExitTime = true;
                t.exitTime = 0.97f;
                t.hasFixedDuration = true;
                t.duration = depois == "Locomocao" ? 0.15f : 0.12f;
            }

            // do peito para cima, por cima de tudo (o peso 0 até usar)
            var mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara);
            ctrl.AddLayer("Cima");
            var camadas = ctrl.layers;
            camadas[1].avatarMask = mascara;
            camadas[1].defaultWeight = 0f;
            camadas[1].blendingMode = AnimatorLayerBlendingMode.Override;
            var cima = camadas[1].stateMachine;
            var vazio = cima.AddState("Vazio", new Vector3(300f, 0f, 0f));
            cima.defaultState = vazio;
            var deCima = new System.Collections.Generic.Dictionary<string, AnimatorState> { ["Vazio"] = vazio };
            int k = 0;
            foreach (var (c, _) in DeCima)
            {
                var s = cima.AddState(c, new Vector3(600f, k++ * 60f, 0f));
                s.motion = Clipe(c);
                deCima[c] = s;
            }
            foreach (var (c, depois) in DeCima)
            {
                if (depois == null) continue;
                var t = deCima[c].AddTransition(deCima[depois]);
                t.hasExitTime = true;
                t.exitTime = 0.95f;
                t.hasFixedDuration = true;
                t.duration = depois == "Vazio" ? 0.2f : 0.12f;
            }
            ctrl.layers = camadas;
            if (mascara == null) Debug.LogWarning($"[Moradores] Falta a máscara do tronco ({Mascara}): rode a montagem dos personagens.");
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ------------------------------------------------------------------ o prefab

        public static GameObject MontarPrefab(RuntimeAnimatorController ctrl)
        {
            Directory.CreateDirectory(PastaDoCorpo);
            var jogador = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab");
            var corpoDoJogador = jogador != null ? jogador.transform.Find("Corpo_PrincipalBloco") : null;
            if (corpoDoJogador == null) throw new FileNotFoundException("O corpo do jogador (Corpo_PrincipalBloco) não está no Jogador.prefab.");

            var raiz = new GameObject("Morador") { layer = 2 };
            raiz.AddComponent<NetworkObject>();
            var nt = raiz.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            var agente = raiz.AddComponent<NavMeshAgent>();
            agente.radius = 0.28f;
            agente.height = 1.8f;
            agente.baseOffset = 0f;
            agente.speed = 1.3f;
            agente.angularSpeed = 360f;
            agente.acceleration = 8f;
            agente.stoppingDistance = 0.15f;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agente.enabled = false;
            var capsula = raiz.AddComponent<CapsuleCollider>();
            capsula.center = new Vector3(0f, 0.9f, 0f);
            capsula.height = 1.8f;
            capsula.radius = 0.28f;
            var morador = raiz.AddComponent<CorpoDoMorador>();

            var corpoGo = Object.Instantiate(corpoDoJogador.gameObject, raiz.transform, false);
            corpoGo.name = "Corpo";
            corpoGo.SetActive(true);
            corpoGo.transform.localPosition = Vector3.zero;
            corpoGo.transform.localRotation = Quaternion.identity;
            var corpo = corpoGo.GetComponent<CorpoDoPersonagem>();
            corpo.animador.runtimeAnimatorController = ctrl;
            corpo.animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            corpo.animador.applyRootMotion = false;
            foreach (var r in corpoGo.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            if (corpo.naMao != null) foreach (var a in corpo.naMao) if (a != null) a.SetActive(false);
            // as peças das armas que ficam nos ossos delas (o ferrolho e o carregador: P_<arma>_<peça>), como no estúdio
            foreach (var t in corpoGo.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("P_")) t.gameObject.SetActive(false);

            // o prato, a comida, a colher e o bocado do Loot.fbx nos vazios do corpo (desligados; o CorpoDoMorador liga)
            var malhas = AssetDatabase.LoadAllAssetsAtPath(MontagemDasArmas.FbxLoot).OfType<Mesh>().ToArray();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var todos = corpoGo.GetComponentsInChildren<Transform>(true);
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var (vazio, nomes) in Pecas)
            {
                var v = todos.FirstOrDefault(t => t.name == vazio);
                if (v == null)
                {
                    Debug.LogWarning($"[Moradores] O corpo não tem o vazio {vazio} (exporte o bloco_morador.py).");
                    continue;
                }
                foreach (var nome in nomes)
                {
                    var malha = malhas.FirstOrDefault(m => m.name == nome);
                    if (malha == null)
                    {
                        Debug.LogWarning($"[Moradores] O Loot.fbx não tem a malha {nome} (exporte o loot.py).");
                        continue;
                    }
                    var go = new GameObject(nome);
                    go.transform.SetParent(v, false);
                    go.transform.localRotation = GiroDasPecas;
                    go.AddComponent<MeshFilter>().sharedMesh = malha;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.enabled = false;
                    renderers.Add(r);
                }
            }

            // o livro aberto (a prévia do bloco_morador.py, que não tem modelo): no vazio do livro, só no ler
            var vazioDoLivro = todos.FirstOrDefault(t => t.name == "Morador_Livro");
            Renderer livro = null;
            if (vazioDoLivro != null)
            {
                var go = new GameObject("Livro_Aberto");
                go.transform.SetParent(vazioDoLivro, false);
                go.transform.localRotation = GiroDasPecas;
                go.AddComponent<MeshFilter>().sharedMesh = MalhaDoLivro();
                livro = go.AddComponent<MeshRenderer>();
                livro.sharedMaterials = new[] { MaterialDoLivro("M_Livro_Capa", new Color(0.42f, 0.09f, 0.07f)), MaterialDoLivro("M_Livro_Folhas", new Color(0.88f, 0.84f, 0.72f)) };
                livro.shadowCastingMode = ShadowCastingMode.Off;
                livro.enabled = false;
            }

            // a roupa dobrando (a parte 8): a peça aberta no vazio do livro, a dobrada ao meio no da caixa e a dobrada em
            // quatro no do prato (a camiseta do bloco_morador.py: 40 × 46 cm), só no Dobrar_Roupa
            var roupas = new System.Collections.Generic.List<Renderer>();
            var corDaRoupa = MaterialDoLivro("M_Roupa_Dobrando", new Color(0.24f, 0.33f, 0.46f));
            foreach (var (vazio, nome, tamanho) in new[]
            {
                ("Morador_Livro", "Roupa_Aberta", new Vector3(0.40f, 0.006f, 0.46f)),
                ("Morador_Caixa", "Roupa_Ao_Meio", new Vector3(0.40f, 0.014f, 0.23f)),
                ("Morador_Prato", "Roupa_Em_Quatro", new Vector3(0.20f, 0.026f, 0.23f)),
            })
            {
                var v = todos.FirstOrDefault(t => t.name == vazio);
                if (v == null) continue;
                var go = new GameObject(nome);
                go.transform.SetParent(v, false);
                go.transform.localRotation = GiroDasPecas;
                go.AddComponent<MeshFilter>().sharedMesh = MalhaDaRoupa(nome, tamanho);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = corDaRoupa;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.enabled = false;
                roupas.Add(r);
            }
            // a fumaça e a bituca do cigarro: as do jogador
            var fumoDoJogador = jogador.GetComponent<FumoJogador>();

            morador.corpo = corpo;
            morador.agente = agente;
            morador.controlador = ctrl;
            morador.pratoEColher = renderers.ToArray();
            morador.livro = livro;
            morador.roupaDobrando = roupas.ToArray();
            if (fumoDoJogador != null)
            {
                morador.materialDaFumaca = fumoDoJogador.materialDaFumaca;
                morador.materialDaBituca = fumoDoJogador.materialDaBituca;
                morador.malhaDaBituca = fumoDoJogador.malhaDaBituca;
            }
            else Debug.LogWarning("[Moradores] O Jogador.prefab não tem o FumoJogador: o fumo dos moradores fica sem a fumaça.");

            var salvo = PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Object.DestroyImmediate(raiz);
            return salvo;
        }

        // o livro aberto, como a prévia do bloco_morador.py (nos eixos das malhas do Loot.fbx: a largura em x, o alto em z e
        // as páginas para +y): a capa e o bloco de páginas de cada lado, um pouco em V; o submesh 0 é a capa, o 1 as folhas
        static Mesh MalhaDoLivro()
        {
            Directory.CreateDirectory(ArteDoLivro);
            string caminho = ArteDoLivro + "/Livro_Aberto.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (m == null)
            {
                m = new Mesh { name = "Livro_Aberto" };
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.Clear();
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var capa = new System.Collections.Generic.List<int>();
            var folhas = new System.Collections.Generic.List<int>();
            float lw = Pagina.x, lh = Pagina.y;
            for (int s = -1; s <= 1; s += 2)
            {
                var giro = Quaternion.Euler(0f, 0f, 8f * s);
                Caixa(v, n, capa, giro, new Vector3(s * lw / 2f, -0.006f, 0f), new Vector3(lw, 0.005f, lh + 0.01f));
                Caixa(v, n, folhas, giro, new Vector3(s * (lw / 2f - 0.004f), 0.006f, 0f), new Vector3(lw - 0.012f, 0.018f, lh - 0.006f));
            }
            m.SetVertices(v);
            m.SetNormals(n);
            m.subMeshCount = 2;
            m.SetTriangles(capa, 0);
            m.SetTriangles(folhas, 1);
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        // uma peça de roupa dobrada: uma caixa achatada (nos eixos das malhas do Loot.fbx: a largura em x, a grossura em y)
        static Mesh MalhaDaRoupa(string nome, Vector3 tamanho)
        {
            Directory.CreateDirectory(ArteDoLivro);
            string caminho = $"{ArteDoLivro}/{nome}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (m == null)
            {
                m = new Mesh { name = nome };
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.Clear();
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var tri = new System.Collections.Generic.List<int>();
            Caixa(v, n, tri, Quaternion.identity, Vector3.zero, tamanho);
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Caixa(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<int> tri,
                          Quaternion giro, Vector3 centro, Vector3 tamanho)
        {
            var h = tamanho / 2f;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 a = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up, b = Vector3.Cross(d, a);
                int i0 = v.Count;
                foreach (var (sa, sb) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    var p = Vector3.Scale(d + a * sa + b * sb, h);
                    v.Add(giro * (centro + p));
                    n.Add(giro * d);
                }
                tri.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
        }

        static Material MaterialDoLivro(string nome, Color cor)
        {
            Directory.CreateDirectory(ArteDoLivro);
            string caminho = $"{ArteDoLivro}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void PorNaRede(GameObject prefab)
        {
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista == null || lista.PrefabList.Any(p => p.Prefab == prefab)) return;
            lista.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(lista);
        }
    }
}
