using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A pistola nova (29/10/2026, a pedido: "um modelo um pouco mais realista do que temos hoje"): o modelo e a textura
    /// vêm do <c>Blender/Pistola/pistola.py</c> (o <c>Armas_Pistola.fbx</c>, com as mesmas três peças da antiga: o corpo, o
    /// ferrolho e o carregador, e as texturas <c>Texturas/Pistola_Cor</c> e <c>Pistola_MS</c>). A antiga continua nos FBX do
    /// Gustavo (os braços de primeira pessoa e o corpo); esta montagem troca ela pela nova onde ela aparece, sem refazer
    /// esses FBX (e sem mexer nas animações):
    ///  - a arma solta (<c>Malhas/Arma_Pistola</c>: o chão, o painel, as costas e o coldre), pelo <c>Armas_Pistola.fbx</c>;
    ///  - os braços (<see cref="NosBracos"/>): uma cópia da malha dos braços com a pistola nova no lugar da antiga, presa aos
    ///    mesmos ossos (o corpo no "gun", o ferrolho no "slide" e o carregador no "mag");
    ///  - o corpo (<see cref="NoCorpo"/>): as peças <c>P_Pistola_*</c> com as malhas novas.
    /// Onde a pistola fica em cada lugar é medido da antiga (o encaixe da arma solta na dos braços e na do corpo), então a
    /// nova cai onde a antiga estava. O material com a textura fica em Resources (<see cref="Loot.Itens.MaterialProprio"/>).
    /// A montagem do prefab (<see cref="MontagemDaRede"/>) chama as duas trocas: rodar a "Montar armas" de novo não volta a
    /// pistola antiga. Menu: Zombie City Cleaner > Armas > Montar a pistola nova.
    /// </summary>
    public static class MontagemDaPistola
    {
        const string Arte = "Assets/_Game/Art/Characters/Jogador";
        public const string Fbx = Arte + "/Armas_Pistola.fbx";
        const string FbxAntigo = Arte + "/Armas.fbx";
        const string Texturas = Arte + "/Texturas";
        const string PastaDoMaterial = Arte + "/Resources/MateriaisProprios";
        public const string CaminhoDoMaterial = PastaDoMaterial + "/Pistola.mat";
        const string MalhaDosBracos = Arte + "/Malhas/FP_Bracos_Pistola.asset";
        const string Jogador = "Assets/_Game/Prefabs/Jogador.prefab";
        /// <summary>As peças e o osso de cada uma (nos braços de primeira pessoa).</summary>
        static readonly (string peca, string osso)[] Pecas = { ("Pistola_corpo", "gun"), ("Pistola_ferrolho", "slide"), ("Pistola_carregador", "mag") };

        public static bool Existe => File.Exists(Fbx);

        [MenuItem("Zombie City Cleaner/Armas/Montar a pistola nova")]
        public static void Montar()
        {
            if (!Existe) throw new FileNotFoundException("Rode o Blender/Pistola/pistola.py antes.", Fbx);
            Preparar();
            MontagemDasArmas.Juntar(Fbx, "Pistola_", MontagemDasArmas.MalhaDo(0));
            // o prefab do jogador como está: só os braços e os corpos da pistola mudam
            var raiz = PrefabUtility.LoadPrefabContents(Jogador);
            try
            {
                foreach (var smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name == "FP_Bracos_Pistola"))
                    NosBracos(smr);
                foreach (Transform filho in raiz.transform)
                    if (filho.name.StartsWith("Corpo_")) NoCorpo(filho);
                PrefabUtility.SaveAsPrefabAsset(raiz, Jogador);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Pistola nova montada: a arma solta, os braços de primeira pessoa e o corpo (no prefab do jogador).");
        }

        /// <summary>As texturas, o material (em Resources) e a importação do FBX.</summary>
        public static void Preparar()
        {
            if (!Existe) return;
            Textura($"{Texturas}/Pistola_Cor.png", true);
            Textura($"{Texturas}/Pistola_MS.png", false);
            var mat = Material();
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.isReadable = true;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pistola_Textura"), mat);
            imp.SaveAndReimport();
        }

        static void Textura(string caminho, bool cor)
        {
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura da pistola não encontrada (rode o pistola.py).", caminho);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = cor;
            imp.alphaSource = cor ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static Material Material()
        {
            Directory.CreateDirectory(PastaDoMaterial);
            var s = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(CaminhoDoMaterial);
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, CaminhoDoMaterial);
            }
            m.shader = s;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/Pistola_Cor.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/Pistola_MS.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetShaderPassEnabled("MotionVectors", false);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------ as malhas

        /// <summary>As malhas de um FBX pelo nome do objeto (a malha de cada filho com MeshFilter ou SkinnedMeshRenderer).</summary>
        static Mesh MalhaDoObjeto(string fbx, string nome)
        {
            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (raiz == null) return null;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != nome) continue;
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null) return mf.sharedMesh;
                var smr = t.GetComponent<SkinnedMeshRenderer>();
                if (smr != null) return smr.sharedMesh;
            }
            return null;
        }

        static Mesh[] PecasNovas() => Pecas.Select(p => MalhaDoObjeto(Fbx, p.peca) ?? throw new System.Exception($"Falta {p.peca} no {Fbx}.")).ToArray();
        static Mesh[] PecasAntigas() => Pecas.Select(p => MalhaDoObjeto(FbxAntigo, p.peca) ?? throw new System.Exception($"Falta {p.peca} no {FbxAntigo}.")).ToArray();

        /// <summary>
        /// O encaixe que leva a arma solta antiga (<paramref name="origem"/>) para onde ela está noutro lugar
        /// (<paramref name="destino"/>): a mesma forma, girada de 90 em 90 graus (cada FBX converte os eixos do seu jeito) e
        /// deslocada. Os eixos saem do tamanho da caixa, o sentido de cada um do lado para onde o miolo da arma pende (o
        /// carregador embaixo e atrás, o cano na frente), e o giro é sem espelho. Confere: a caixa e o miolo batem.
        /// </summary>
        static Matrix4x4 Encaixe(IList<Vector3> destino, IList<Vector3> origem, string onde)
        {
            static (Bounds caixa, Vector3 pende) Medir(IList<Vector3> vs)
            {
                var unicos = vs.Select(v => new Vector3(Mathf.Round(v.x * 1e4f), Mathf.Round(v.y * 1e4f), Mathf.Round(v.z * 1e4f))).Distinct()
                               .Select(v => v * 1e-4f).ToArray();
                var b = new Bounds(unicos[0], Vector3.zero);
                foreach (var v in unicos) b.Encapsulate(v);
                var media = Vector3.zero;
                foreach (var v in unicos) media += v;
                return (b, media / unicos.Length - b.center);
            }
            var (bd, pd) = Medir(destino);
            var (bo, po) = Medir(origem);
            int[][] permutacoes = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            float melhor = float.MaxValue;
            Matrix4x4 escolhido = Matrix4x4.identity;
            foreach (var p in permutacoes)
                for (int sinais = 0; sinais < 8; sinais++)
                {
                    var r = Matrix4x4.zero;
                    for (int j = 0; j < 3; j++) r[p[j], j] = (sinais & (1 << j)) != 0 ? -1f : 1f;
                    r[3, 3] = 1f;
                    if (r.determinant < 0f) continue;   // sem espelho
                    var tam = r.MultiplyVector(bo.size);
                    tam = new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z));
                    float erro = (tam - bd.size).magnitude + (r.MultiplyVector(po) - pd).magnitude;
                    if (erro < melhor)
                    {
                        melhor = erro;
                        escolhido = r;
                    }
                }
            var t = bd.center - escolhido.MultiplyPoint3x4(bo.center);
            var m = Matrix4x4.Translate(t) * escolhido;
            if (melhor > 0.004f) Debug.LogWarning($"Pistola nova ({onde}): o encaixe da antiga não bateu bem (erro {melhor * 1000f:F1} mm).");
            else Debug.Log($"Pistola nova ({onde}): encaixe da antiga com erro de {melhor * 1000f:F2} mm, deslocamento {t.x:F3} {t.y:F3} {t.z:F3}.");
            return m;
        }

        /// <summary>
        /// Os braços de primeira pessoa: a malha deles com a pistola nova no lugar da antiga (o submesh 1), cada peça presa
        /// ao osso dela com peso 1, e o material da pistola no submesh 1. Grava <c>Malhas/FP_Bracos_Pistola</c>.
        /// </summary>
        public static void NosBracos(SkinnedMeshRenderer smr)
        {
            if (!Existe || smr == null) return;
            var original = MalhaDoObjeto(MontagemDasArmas.FbxFP(0), "FP_Bracos_Pistola");
            if (original == null || original.subMeshCount < 2) { Debug.LogWarning("Pistola nova: sem a malha dos braços da pistola."); return; }
            var ossos = smr.bones.Select(b => b != null ? b.name : "").ToList();
            var vs = original.vertices;
            var antigos = original.GetTriangles(1).Distinct().Select(i => vs[i]).ToArray();
            var m = Encaixe(antigos, PecasAntigas().SelectMany(p => p.vertices).ToArray(), "braços");

            // os braços (o submesh 0), com os índices refeitos
            var tri0 = original.GetTriangles(0);
            var usados = tri0.Distinct().OrderBy(i => i).ToArray();
            var novoIndice = new Dictionary<int, int>();
            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var uv = new List<Vector2>();
            var pesos = new List<BoneWeight>();
            var normais = original.normals;
            var uvs = original.uv;
            var pesosOriginais = original.boneWeights;
            foreach (int i in usados)
            {
                novoIndice[i] = pos.Count;
                pos.Add(vs[i]);
                nor.Add(normais.Length > i ? normais[i] : Vector3.up);
                uv.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
                pesos.Add(pesosOriginais[i]);
            }
            var braco = tri0.Select(i => novoIndice[i]).ToArray();

            // a pistola nova, peça por peça, no osso dela
            var arma = new List<int>();
            var novas = PecasNovas();
            for (int k = 0; k < Pecas.Length; k++)
            {
                int osso = ossos.IndexOf(Pecas[k].osso);
                if (osso < 0) throw new System.Exception($"Os braços da pistola não têm o osso {Pecas[k].osso}.");
                var peca = novas[k];
                int primeiro = pos.Count;
                var pv = peca.vertices;
                var pn = peca.normals;
                var pu = peca.uv;
                for (int i = 0; i < pv.Length; i++)
                {
                    pos.Add(m.MultiplyPoint3x4(pv[i]));
                    nor.Add(m.MultiplyVector(pn[i]).normalized);
                    uv.Add(pu[i]);
                    pesos.Add(new BoneWeight { boneIndex0 = osso, weight0 = 1f });
                }
                arma.AddRange(peca.triangles.Select(i => i + primeiro));
            }

            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDosBracos);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = "FP_Bracos_Pistola";
            malha.SetVertices(pos);
            malha.SetNormals(nor);
            malha.SetUVs(0, uv);
            malha.boneWeights = pesos.ToArray();
            malha.bindposes = original.bindposes;
            malha.subMeshCount = 2;
            malha.SetTriangles(braco, 0);
            malha.SetTriangles(arma, 1);
            malha.RecalculateBounds();
            malha.RecalculateTangents();
            if (nova) AssetDatabase.CreateAsset(malha, MalhaDosBracos);
            else EditorUtility.SetDirty(malha);

            smr.sharedMesh = malha;
            var mats = smr.sharedMaterials;
            if (mats.Length > 1) mats[1] = Material();
            smr.sharedMaterials = mats;
            // o carregador comprido passa da caixa da malha antiga
            var caixa = smr.localBounds;
            caixa.Expand(0.1f);
            smr.localBounds = caixa;
        }

        /// <summary>O corpo de um personagem: as peças P_Pistola_* (presas à mão e aos ossos do ferrolho e do carregador) com as
        /// malhas novas, encaixadas onde as antigas estavam, e o material da pistola.</summary>
        public static void NoCorpo(Transform corpo)
        {
            if (!Existe || corpo == null) return;
            var mat = Material();
            var novas = PecasNovas();
            var antigas = PecasAntigas();
            var todos = corpo.GetComponentsInChildren<Transform>(true);
            string fbxDoCorpo = null;
            var fonte = PrefabUtility.GetCorrespondingObjectFromSource(corpo.gameObject);
            if (fonte != null) fbxDoCorpo = AssetDatabase.GetAssetPath(fonte);
            for (int k = 0; k < Pecas.Length; k++)
            {
                string nome = "P_" + Pecas[k].peca;
                var t = todos.FirstOrDefault(x => x.name == nome);
                var mf = t != null ? t.GetComponent<MeshFilter>() : null;
                if (mf == null) continue;
                // a malha antiga desta peça no corpo (a do FBX), para o encaixe
                var antiga = fbxDoCorpo != null ? MalhaDoObjeto(fbxDoCorpo, nome) : null;
                if (antiga == null) antiga = mf.sharedMesh;
                var m = Encaixe(antiga.vertices, antigas[k].vertices, $"corpo, {Pecas[k].peca}");
                mf.sharedMesh = Encaixada(novas[k], m, $"{Arte}/Malhas/TP_{Pecas[k].peca}.asset");
                var r = t.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }

        // a peça nova no encaixe: ela mesma, se o encaixe não muda nada; senão, uma cópia gravada no caminho
        static Mesh Encaixada(Mesh peca, Matrix4x4 m, string caminho)
        {
            bool igual = true;
            for (int i = 0; i < 16; i++) igual &= Mathf.Abs(m[i] - Matrix4x4.identity[i]) < 1e-4f;
            if (igual) return peca;
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = Path.GetFileNameWithoutExtension(caminho);
            malha.SetVertices(peca.vertices.Select(v => m.MultiplyPoint3x4(v)).ToList());
            malha.SetNormals(peca.normals.Select(n => m.MultiplyVector(n).normalized).ToList());
            malha.SetUVs(0, peca.uv.ToList());
            malha.SetTriangles(peca.triangles, 0);
            malha.RecalculateBounds();
            malha.RecalculateTangents();
            if (nova) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
            return malha;
        }
    }
}
