using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As armas novas (29/10/2026, a pedido: a reformulação visual das armas): os modelos vêm do <c>Blender/Armas</c> (a
    /// partir dos modelos prontos que o Pedro mandou, adaptados ao jogo), um FBX por arma em <c>Art/Armas/Modelos</c>, com
    /// as mesmas peças da arma antiga (o corpo, o ferrolho, o carregador...) no mesmo espaço dela, e as texturas PBR em
    /// <c>Art/Armas/Texturas</c> (a cor, o MS: o metal no R, a oclusão no G e a lisura no A, e o normal).
    /// Aqui: a importação, o material (URP Lit, em <c>Art/Armas/Materiais</c>) e a prévia em primeira pessoa (uma cena de
    /// prévia, sem mexer em cena nem prefab).
    /// </summary>
    public static class ArmasNovas
    {
        public const string Arte = "Assets/_Game/Art/Armas";
        const string Modelos = Arte + "/Modelos";
        const string Texturas = Arte + "/Texturas";
        /// <summary>Os materiais ficam em Resources (o <see cref="Loot.Itens.MaterialProprio"/> carrega pelo nome do modelo).</summary>
        const string Materiais = Arte + "/Resources/MateriaisProprios";
        const string MateriaisAntes = Arte + "/Materiais";
        const string Jogador = "Assets/_Game/Art/Characters/Jogador";

        public class Arma
        {
            public string nome;
            /// <summary>O nome dela nos FBX do jogador (Jogador_FP_&lt;modelo&gt;, &lt;modelo&gt;_corpo no Armas.fbx).</summary>
            public string modelo;
            /// <summary>Onde está a arma solta antiga (para medir o encaixe).</summary>
            public string fbxAntigo;
            /// <summary>As peças e o osso de cada uma nos braços de primeira pessoa.</summary>
            public (string peca, string osso)[] pecas;
            public int tamanho = 2048;
            /// <summary>
            /// As estilizadas (v2, 30/10/2026): cores chapadas da paleta compartilhada (<c>Texturas/Armas_Paleta_Cor</c> e
            /// <c>_MS</c>, sem filtro), sem normal map.
            /// </summary>
            public bool paleta;
        }

        public static readonly Arma Pistola = new Arma
        {
            nome = "Pistola", modelo = "Pistola", fbxAntigo = Jogador + "/Armas.fbx",
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        public static readonly Arma Rifle = new Arma
        {
            nome = "Rifle", modelo = "Rifle", fbxAntigo = Jogador + "/Armas.fbx",
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        public static readonly Arma Espingarda = new Arma
        {
            nome = "Espingarda", modelo = "Espingarda", fbxAntigo = Jogador + "/Armas.fbx",
            pecas = new[] { ("corpo", "gun"), ("telha", "pump"), ("cartucho", "shell") },
        };

        public static readonly Arma Sniper = new Arma
        {
            nome = "Sniper", modelo = "Sniper", fbxAntigo = Jogador + "/Armas_Sniper.fbx",
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        // as estilizadas (v2, 30/10/2026, a pedido: "uma melhoria das anteriores", low poly como as antigas)
        public static readonly Arma Rifle2 = new Arma
        {
            nome = "Rifle2", modelo = "Rifle", fbxAntigo = Jogador + "/Armas.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        // o rifle v3 (30/10/2026, a pedido: no conceito da sniper v3, a M4A1 da imagem de referência, com ~3 mil
        // triângulos): o AR-15 fonte com a alça de transporte, o guarda-mão canelado e a massa de mira em "A"
        // (Blender/Armas/v3_m4.py, 3.352 triângulos)
        public static readonly Arma Rifle3 = new Arma
        {
            nome = "Rifle3", modelo = "Rifle", fbxAntigo = Jogador + "/Armas.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        public static readonly Arma Espingarda2 = new Arma
        {
            nome = "Espingarda2", modelo = "Espingarda", fbxAntigo = Jogador + "/Armas.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("telha", "pump"), ("cartucho", "shell") },
        };

        public static readonly Arma Sniper2 = new Arma
        {
            nome = "Sniper2", modelo = "Sniper", fbxAntigo = Jogador + "/Armas_Sniper.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        // a sniper v3 (30/10/2026, a pedido: a Mosin fiel, com a luneta da v2 e sem o apoio; a mão segura o punho dela,
        // com os braços de primeira pessoa ajustados pelo Blender/Armas/maos_sniper.py)
        public static readonly Arma Sniper3 = new Arma
        {
            nome = "Sniper3", modelo = "Sniper", fbxAntigo = Jogador + "/Armas_Sniper.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        // a sniper v3 leve (30/10/2026, a pedido: "8500 triângulos está um pouco pesada demais"): a mesma Mosin com a
        // luneta "scout", com 4.219 triângulos em vez de 8.548 (Blender/Armas/v3_mosin_leve.py)
        public static readonly Arma Sniper3Leve = new Arma
        {
            nome = "Sniper3Leve", modelo = "Sniper", fbxAntigo = Jogador + "/Armas_Sniper.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        // a submetralhadora v3 (30/10/2026, a pedido: a MP5K da imagem de referência, de coronha dobrável e empunhadura
        // vertical): toda modelada (Blender/Armas/v3_mp5.py, sem modelo fonte); a mão esquerda dos braços de primeira
        // pessoa vai para a empunhadura (Blender/Armas/maos_smg.py)
        public static readonly Arma Submetralhadora3 = new Arma
        {
            nome = "Submetralhadora3", modelo = "Submetralhadora", fbxAntigo = Jogador + "/Armas.fbx", paleta = true,
            pecas = new[] { ("corpo", "gun"), ("ferrolho", "slide"), ("carregador", "mag") },
        };

        /// <summary>Todas as versões (as que não estão no jogo ficam no projeto).</summary>
        public static readonly Arma[] Todas = { Pistola, Rifle, Espingarda, Sniper, Rifle2, Espingarda2, Sniper2, Sniper3, Sniper3Leve, Rifle3, Submetralhadora3 };
        /// <summary>As que estão no jogo: a pistola (v1, aprovada), o rifle v3 (a M4A1), a espingarda estilizada, a sniper v3 leve e a submetralhadora v3 (a MP5K).</summary>
        public static readonly Arma[] Ativas = { Pistola, Rifle3, Espingarda2, Sniper3Leve, Submetralhadora3 };

        public static string Fbx(Arma a) => $"{Modelos}/{a.nome}.fbx";
        /// <summary>O material do modelo (pelo nome do modelo, que o Itens.MaterialProprio carrega): o da arma ativa.</summary>
        public static string CaminhoDoMaterial(Arma a) => $"{Materiais}/{a.modelo}.mat";

        // ------------------------------------------------------------------------------------------ importar
        /// <summary>As texturas, o material e a importação do FBX (o material dele trocado pelo nosso).</summary>
        public static Material Preparar(Arma a)
        {
            if (!File.Exists(Fbx(a))) throw new FileNotFoundException("Rode o Blender/Armas/rodar.py antes.", Fbx(a));
            if (a.paleta)
            {
                TexturaDaPaleta($"{Texturas}/Armas_Paleta_Cor.png", true);
                TexturaDaPaleta($"{Texturas}/Armas_Paleta_MS.png", false);
            }
            else
            {
                Textura($"{Texturas}/{a.nome}_Cor.png", TextureImporterType.Default, true, a.tamanho);
                Textura($"{Texturas}/{a.nome}_MS.png", TextureImporterType.Default, false, a.tamanho);
                Textura($"{Texturas}/{a.nome}_Normal.png", TextureImporterType.NormalMap, false, a.tamanho);
            }
            var mat = Material(a);
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx(a));
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.isReadable = true;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.optimizeMeshVertices = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), a.paleta ? $"{a.nome}_Paleta" : $"{a.nome}_Textura"), mat);
            imp.SaveAndReimport();
            return mat;
        }

        /// <summary>A paleta (4 x 4 cores): sem filtro e sem mipmaps (uma cor não vaza na outra), sem compressão.</summary>
        static void TexturaDaPaleta(string caminho, bool cor)
        {
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("A paleta das armas não foi gravada (rode o Blender/Armas/rodar_v2.py).", caminho);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = cor;
            imp.alphaSource = cor ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static void Textura(string caminho, TextureImporterType tipo, bool cor, int tamanho)
        {
            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura da arma não encontrada (rode o Blender/Armas/rodar.py).", caminho);
            imp.textureType = tipo;
            imp.sRGBTexture = cor;
            imp.alphaSource = tipo == TextureImporterType.Default && !cor ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = tamanho;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        /// <summary>URP Lit, fluxo metálico: a cor, o MS (R = metal, G = oclusão, A = lisura) e o normal.</summary>
        static Material Material(Arma a)
        {
            if (!AssetDatabase.IsValidFolder(Materiais))
            {
                Directory.CreateDirectory(Materiais);
                AssetDatabase.Refresh();
            }
            // o material de outra versão do mesmo modelo (por exemplo, o rifle v1) sai de Resources para Art/Armas/Materiais,
            // com o mesmo GUID (o FBX dela continua apontando para ele)
            string esperado = a.paleta ? "Armas_Paleta_Cor" : $"{a.nome}_Cor";
            var existente = AssetDatabase.LoadAssetAtPath<Material>(CaminhoDoMaterial(a));
            var base0 = existente != null && existente.HasProperty("_BaseMap") ? existente.GetTexture("_BaseMap") : null;
            if (base0 != null && base0.name != esperado)
            {
                if (!AssetDatabase.IsValidFolder(MateriaisAntes)) AssetDatabase.CreateFolder(Arte, "Materiais");
                string destino = AssetDatabase.GenerateUniqueAssetPath($"{MateriaisAntes}/{base0.name.Replace("_Cor", "")}.mat");
                string erro = AssetDatabase.MoveAsset(CaminhoDoMaterial(a), destino);
                if (!string.IsNullOrEmpty(erro)) Debug.LogWarning(erro);
            }
            var s = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(CaminhoDoMaterial(a));
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, CaminhoDoMaterial(a));
            }
            m.shader = s;
            m.shaderKeywords = new string[0];
            if (a.paleta)
            {
                // a paleta: a cor e o metal/lisura chapados de cada face, sem normal map e sem oclusão
                var msp = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/Armas_Paleta_MS.png");
                m.SetFloat("_WorkflowMode", 1f);
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/Armas_Paleta_Cor.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_MetallicGlossMap", msp);
                m.SetFloat("_Metallic", 1f);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_BumpMap", null);
                m.SetTexture("_OcclusionMap", null);
                m.SetFloat("_SpecularHighlights", 1f);
                m.SetFloat("_EnvironmentReflections", 1f);
                m.SetShaderPassEnabled("MotionVectors", false);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssets();
                return m;
            }
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/{a.nome}_MS.png");
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/{a.nome}_Cor.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Texturas}/{a.nome}_Normal.png"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ms);
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetShaderPassEnabled("MotionVectors", false);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ------------------------------------------------------------------------------------------ as malhas
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

        public static Mesh[] PecasNovas(Arma a) =>
            a.pecas.Select(p => MalhaDoObjeto(Fbx(a), $"{a.nome}_{p.peca}") ?? throw new System.Exception($"Falta {a.nome}_{p.peca} no {Fbx(a)}.")).ToArray();

        /// <summary>Os vértices da arma solta antiga (todas as peças &lt;modelo&gt;_* do FBX dela, cada uma no espaço dela).</summary>
        static Vector3[] VerticesAntigos(Arma a) =>
            AssetDatabase.LoadAllAssetsAtPath(a.fbxAntigo).OfType<Mesh>().Where(m => m.name.StartsWith(a.modelo + "_")).SelectMany(m => m.vertices).ToArray();

        /// <summary>
        /// O encaixe que leva a arma solta antiga para onde ela está noutro lugar (a mesma forma, girada de 90 em 90 graus
        /// e deslocada), pela caixa e pelo miolo, como a <see cref="MontagemDaPistola"/>.
        /// </summary>
        public static Matrix4x4 Encaixe(IList<Vector3> destino, IList<Vector3> origem, out float erro)
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
            var escolhido = Matrix4x4.identity;
            foreach (var p in permutacoes)
                for (int sinais = 0; sinais < 8; sinais++)
                {
                    var r = Matrix4x4.zero;
                    for (int j = 0; j < 3; j++) r[p[j], j] = (sinais & (1 << j)) != 0 ? -1f : 1f;
                    r[3, 3] = 1f;
                    if (r.determinant < 0f) continue;
                    var tam = r.MultiplyVector(bo.size);
                    tam = new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z));
                    float e = (tam - bd.size).magnitude + (r.MultiplyVector(po) - pd).magnitude;
                    if (e < melhor) { melhor = e; escolhido = r; }
                }
            erro = melhor;
            return Matrix4x4.Translate(bd.center - escolhido.MultiplyPoint3x4(bo.center)) * escolhido;
        }

        /// <summary>
        /// A malha dos braços de primeira pessoa com a arma nova no lugar da antiga (o submesh 1): cada peça presa ao osso
        /// dela com peso 1. Não grava nada (a malha fica na memória).
        /// </summary>
        public static Mesh BracosComArmaNova(Arma a, SkinnedMeshRenderer smr, Mesh original, out float erro, Mesh destino = null)
        {
            var ossos = smr.bones.Select(b => b != null ? b.name : "").ToList();
            var vs = original.vertices;
            // o cartucho da espingarda (osso "shell") não está na arma solta antiga: fica fora da medida
            var pesosDaMalha = original.boneWeights;
            var antigos = original.GetTriangles(1).Distinct().Where(i => ossos[pesosDaMalha[i].boneIndex0] != "shell").Select(i => vs[i]).ToArray();
            var m = Encaixe(antigos, VerticesAntigos(a), out erro);
            var tri0 = original.GetTriangles(0);
            var usados = tri0.Distinct().OrderBy(i => i).ToArray();
            var novoIndice = new Dictionary<int, int>();
            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var tan = new List<Vector4>();
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
                tan.Add(new Vector4(1, 0, 0, 1));
                uv.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
                pesos.Add(pesosOriginais[i]);
            }
            var braco = tri0.Select(i => novoIndice[i]).ToArray();
            var arma = new List<int>();
            var novas = PecasNovas(a);
            for (int k = 0; k < a.pecas.Length; k++)
            {
                int osso = ossos.IndexOf(a.pecas[k].osso);
                if (osso < 0) throw new System.Exception($"Os braços não têm o osso {a.pecas[k].osso}.");
                var peca = novas[k];
                int primeiro = pos.Count;
                var pv = peca.vertices;
                var pn = peca.normals;
                var pt = peca.tangents;
                var pu = peca.uv;
                for (int i = 0; i < pv.Length; i++)
                {
                    pos.Add(m.MultiplyPoint3x4(pv[i]));
                    nor.Add(m.MultiplyVector(pn[i]).normalized);
                    var t = m.MultiplyVector(pt.Length > i ? (Vector3)pt[i] : Vector3.right).normalized;
                    tan.Add(new Vector4(t.x, t.y, t.z, pt.Length > i ? pt[i].w : 1f));
                    uv.Add(pu[i]);
                    pesos.Add(new BoneWeight { boneIndex0 = osso, weight0 = 1f });
                }
                arma.AddRange(peca.triangles.Select(i => i + primeiro));
            }
            // a malha gravada é preenchida pela API (o CopySerialized de uma malha com pesos embaralhava os índices)
            var malha = destino != null ? destino : new Mesh();
            malha.Clear();
            malha.name = $"FP_Bracos_{a.modelo}";
            malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            malha.SetVertices(pos);
            malha.SetNormals(nor);
            malha.SetTangents(tan);
            malha.SetUVs(0, uv);
            malha.boneWeights = pesos.ToArray();
            malha.bindposes = original.bindposes;
            malha.subMeshCount = 2;
            malha.SetTriangles(braco, 0);
            malha.SetTriangles(arma, 1);
            malha.RecalculateBounds();
            return malha;
        }

        // ------------------------------------------------------------------------------------------ a integração
        public static Arma DoModelo(string modelo) => Ativas.FirstOrDefault(a => a.modelo == modelo && File.Exists(Fbx(a)));

        /// <summary>
        /// A malha solta (<c>Malhas/Arma_&lt;modelo&gt;</c>: o chão, o painel, as costas e o coldre) do FBX novo: as peças juntas,
        /// menos o cartucho da espingarda (ele só aparece na recarga). Devolve false se o modelo não tem arma nova.
        /// </summary>
        public static bool MalhaSolta(int modelo)
        {
            if (modelo < 0 || modelo >= Combat.Armas.Quantas) return false;
            var a = DoModelo(Combat.Armas.Modelos[modelo]);
            if (a == null) return false;
            var pecas = a.pecas.Where(p => p.peca != "cartucho").Select(p => MalhaDoObjeto(Fbx(a), $"{a.nome}_{p.peca}")).Where(m => m != null).ToArray();
            string caminho = MontagemDasArmas.MalhaDo(modelo);
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = Path.GetFileNameWithoutExtension(caminho);
            malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            malha.CombineMeshes(pecas.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), true, false);
            malha.RecalculateBounds();
            if (nova) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
            return true;
        }

        /// <summary>As texturas, os materiais, a importação dos FBX e as malhas soltas das armas novas que existem.</summary>
        public static void PrepararTodas()
        {
            foreach (var a in Ativas)
            {
                if (!File.Exists(Fbx(a))) continue;
                Preparar(a);
                MalhaSolta(System.Array.IndexOf(Combat.Armas.Modelos, a.modelo));
            }
        }

        /// <summary>
        /// Os braços de primeira pessoa de um modelo: a malha deles (a do FBX) com a arma nova no lugar da antiga, gravada em
        /// <c>Malhas/FP_Bracos_&lt;modelo&gt;</c>, e o material dela no submesh 1. Sem arma nova para o modelo, não mexe.
        /// </summary>
        public static void NosBracos(string modelo, SkinnedMeshRenderer smr)
        {
            var a = DoModelo(modelo);
            if (a == null || smr == null) return;
            var original = MalhaDoObjeto($"{Jogador}/Jogador_FP_{a.modelo}.fbx", $"FP_Bracos_{a.modelo}");
            if (original == null || original.subMeshCount < 2) { Debug.LogWarning($"Arma nova: sem a malha dos braços de {modelo}."); return; }
            string caminho = $"{Jogador}/Malhas/FP_Bracos_{a.modelo}.asset";
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool criar = malha == null;
            malha = BracosComArmaNova(a, smr, original, out float erro, malha);
            if (erro > 0.006f) Debug.LogWarning($"Arma nova ({modelo}, braços): o encaixe da antiga não bateu bem ({erro * 1000f:F1} mm).");
            if (criar) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
            smr.sharedMesh = malha;
            var mats = smr.sharedMaterials;
            if (mats.Length > 1) mats[1] = Material(a);
            smr.sharedMaterials = mats;
            var caixa = smr.localBounds;
            caixa.Expand(0.1f);
            smr.localBounds = caixa;
        }

        /// <summary>
        /// A arma antiga solta, peça por peça (por osso), medida nos braços: os vértices de cada osso no submesh 1 levados de
        /// volta para o espaço da arma solta. Serve para o corpo, onde cada peça está noutro espaço (o osso dela), e para as
        /// peças que a arma solta antiga não tem (o cartucho da espingarda).
        /// </summary>
        static Dictionary<string, Vector3[]> AntigaPorOsso(Arma a)
        {
            string fbxFP = $"{Jogador}/Jogador_FP_{a.modelo}.fbx";
            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(fbxFP);
            var smr = raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == $"FP_Bracos_{a.modelo}");
            var original = smr.sharedMesh;
            var ossos = smr.bones.Select(b => b != null ? b.name : "").ToList();
            var vs = original.vertices;
            var pesos = original.boneWeights;
            var daArma = original.GetTriangles(1).Distinct().ToArray();
            var m = Encaixe(daArma.Where(i => ossos[pesos[i].boneIndex0] != "shell").Select(i => vs[i]).ToArray(), VerticesAntigos(a), out _);
            var volta = m.inverse;
            return daArma.GroupBy(i => ossos[pesos[i].boneIndex0]).ToDictionary(g => g.Key, g => g.Select(i => volta.MultiplyPoint3x4(vs[i])).ToArray());
        }

        /// <summary>
        /// O corpo de um personagem (terceira pessoa): as peças P_&lt;modelo&gt;_&lt;peça&gt; de cada arma nova com as malhas novas,
        /// encaixadas onde as antigas estavam (cada peça no espaço do osso dela), e o material da arma.
        /// </summary>
        public static void NoCorpo(Transform corpo)
        {
            if (corpo == null) return;
            var todos = corpo.GetComponentsInChildren<Transform>(true);
            var fonte = PrefabUtility.GetCorrespondingObjectFromSource(corpo.gameObject);
            string fbxDoCorpo = fonte != null ? AssetDatabase.GetAssetPath(fonte) : null;
            foreach (var a in Ativas)
            {
                if (!File.Exists(Fbx(a))) continue;
                var mat = Material(a);
                var antiga = AntigaPorOsso(a);
                var novas = PecasNovas(a);
                for (int k = 0; k < a.pecas.Length; k++)
                {
                    string nome = $"P_{a.modelo}_{a.pecas[k].peca}";
                    var t = todos.FirstOrDefault(x => x.name == nome);
                    var mf = t != null ? t.GetComponent<MeshFilter>() : null;
                    if (mf == null || !antiga.TryGetValue(a.pecas[k].osso, out var vsAntigos)) continue;
                    var daPeca = (fbxDoCorpo != null ? MalhaDoObjeto(fbxDoCorpo, nome) : null) ?? mf.sharedMesh;
                    var m = Encaixe(daPeca.vertices, vsAntigos, out float erro);
                    if (erro > 0.006f) Debug.LogWarning($"Arma nova ({nome}, corpo): o encaixe da antiga não bateu bem ({erro * 1000f:F1} mm).");
                    mf.sharedMesh = Encaixada(novas[k], m, $"{Jogador}/Malhas/TP_{a.modelo}_{a.pecas[k].peca}.asset");
                    var r = t.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = mat;
                }
            }
        }

        static Mesh Encaixada(Mesh peca, Matrix4x4 m, string caminho)
        {
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = Path.GetFileNameWithoutExtension(caminho);
            malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            malha.SetVertices(peca.vertices.Select(v => m.MultiplyPoint3x4(v)).ToList());
            malha.SetNormals(peca.normals.Select(n => m.MultiplyVector(n).normalized).ToList());
            malha.SetTangents(peca.tangents.Select(t =>
            {
                var d = m.MultiplyVector(t).normalized;
                return new Vector4(d.x, d.y, d.z, t.w);
            }).ToList());
            malha.SetUVs(0, peca.uv.ToList());
            malha.SetTriangles(peca.triangles, 0);
            malha.RecalculateBounds();
            if (nova) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
            return malha;
        }

        const string PrefabDoJogador = "Assets/_Game/Prefabs/Jogador.prefab";

        /// <summary>
        /// Põe as armas novas no jogo sem refazer o prefab do jogador: as texturas, os materiais, as malhas soltas e, no
        /// prefab, os braços de primeira pessoa e as peças das armas nos corpos. A montagem do prefab (MontagemDaRede) e a
        /// "Montar armas" chamam o mesmo: refazer não volta as antigas.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Armas/Pôr as armas novas")]
        public static void Integrar()
        {
            PrepararTodas();
            var raiz = PrefabUtility.LoadPrefabContents(PrefabDoJogador);
            try
            {
                foreach (var smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.name.StartsWith("FP_Bracos_")) NosBracos(smr.name.Substring("FP_Bracos_".Length), smr);
                foreach (Transform filho in raiz.transform)
                    if (filho.name.StartsWith("Corpo_")) NoCorpo(filho);
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDoJogador);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Armas novas no jogo: " + string.Join(", ", Ativas.Where(a => File.Exists(Fbx(a))).Select(a => a.nome)) + ".");
        }

        // ------------------------------------------------------------------------------------------ a prévia
        /// <summary>
        /// A prévia em primeira pessoa: os braços na pose de um clipe (parado e mirando), com a arma nova, vistos pela câmera
        /// dos braços (no olho, 55°), e a arma solta de três quartos, numa cena de prévia com luz de estúdio. Grava os PNG em
        /// <paramref name="pasta"/> e devolve o resumo.
        /// </summary>
        public static string Previa(Arma a, string pasta, int largura = 1600, int altura = 900, bool antiga = false,
                                    (string clipe, float fracao)[] poses = null, bool deLado = false)
        {
            var mat = antiga ? null : Preparar(a);
            Directory.CreateDirectory(pasta);
            string fbxFP = $"{Jogador}/Jogador_FP_{a.modelo}.fbx";
            var log = new System.Text.StringBuilder();
            var cena = EditorSceneManager.NewPreviewScene();
            var criados = new List<Object>();
            try
            {
                var bracos = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbxFP));
                SceneManager.MoveGameObjectToScene(bracos, cena);
                var smr = bracos.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == $"FP_Bracos_{a.modelo}");
                var pele = AssetDatabase.FindAssets("Personagem_PrincipalBloco t:Material").Select(AssetDatabase.GUIDToAssetPath)
                                        .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(x => x != null && x.name == "Personagem_PrincipalBloco");
                if (!antiga)
                {
                    var malha = BracosComArmaNova(a, smr, smr.sharedMesh, out float erro);
                    criados.Add(malha);
                    log.AppendLine($"encaixe da antiga: erro {erro * 1000f:F2} mm");
                    smr.sharedMesh = malha;
                    smr.sharedMaterials = new[] { pele != null ? pele : smr.sharedMaterials[0], mat };
                }
                else
                {
                    var paleta = AssetDatabase.LoadAssetAtPath<Material>($"{Jogador}/Materials/Pistola.mat");
                    smr.sharedMaterials = new[] { pele != null ? pele : smr.sharedMaterials[0], paleta };
                }
                smr.updateWhenOffscreen = true;

                // a luz de estúdio e a câmera dos braços
                Luz(cena, new Vector3(35f, -30f, 0f), 1.3f, new Color(1f, 0.97f, 0.92f));
                Luz(cena, new Vector3(20f, 150f, 0f), 0.7f, new Color(0.85f, 0.9f, 1f));
                Luz(cena, new Vector3(-60f, 60f, 0f), 0.35f, Color.white);
                var camGo = new GameObject("Câmera da prévia");
                SceneManager.MoveGameObjectToScene(camGo, cena);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = cena;
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.16f, 0.17f);
                var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                criados.Add(rt);
                cam.targetTexture = rt;

                var clipes = AssetDatabase.LoadAllAssetsAtPath(fbxFP).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
                log.AppendLine("clipes: " + string.Join(", ", clipes.Select(c => c.name)));
                // a pose de cada clipe pelo AnimationMode (o SampleAnimation não mexe nos clipes Generic)
                // e a malha deformada de cada pose num MeshRenderer (a cena de prévia não refaz a deformação sozinha)
                var pose = new GameObject("Pose");
                SceneManager.MoveGameObjectToScene(pose, cena);
                var malhaDaPose = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                criados.Add(malhaDaPose);
                pose.AddComponent<MeshFilter>().sharedMesh = malhaDaPose;
                pose.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                smr.enabled = false;
                bool ligou = !AnimationMode.InAnimationMode();
                if (ligou) AnimationMode.StartAnimationMode();
                try
                {
                    // a primeira amostra não pega: uma de aquecimento antes
                    var aquecer = clipes.FirstOrDefault(x => x.name == "Idle");
                    if (aquecer != null)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(bracos, aquecer, 0f);
                        AnimationMode.EndSampling();
                        smr.BakeMesh(malhaDaPose, true);
                    }
                    string sufixo = antiga ? "_antiga" : "";
                    foreach (var (clipe, fracao) in poses ?? new[] { ("Idle", 0.25f), ("Fire", 0.2f), ("Reload", 0.45f) })
                    {
                        var c = clipes.FirstOrDefault(x => x.name == clipe);
                        if (c == null) continue;
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(bracos, c, c.length * fracao);
                        AnimationMode.EndSampling();
                        smr.BakeMesh(malhaDaPose, true);
                        pose.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                        camGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        string nomeFoto = $"{a.nome.ToLower()}_fp_{clipe.ToLower()}{(poses != null ? "_" + Mathf.RoundToInt(fracao * 100) : "")}{sufixo}";
                        Gravar(cam, rt, Path.Combine(pasta, nomeFoto + ".png"));
                        if (deLado)
                        {
                            // de lado (pela direita), para ver onde a mão pega
                            var b = pose.GetComponent<MeshRenderer>().bounds;
                            camGo.transform.position = b.center + Vector3.right * 1.1f + Vector3.up * 0.1f;
                            camGo.transform.LookAt(b.center);
                            Gravar(cam, rt, Path.Combine(pasta, nomeFoto + "_lado.png"));
                        }
                        var slide = bracos.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "slide");
                        var mag = bracos.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "mag");
                        log.AppendLine($"foto: {clipe} (t {c.length * fracao:F2} s; slide {slide?.localPosition}, mag {mag?.localPosition})");
                    }
                }
                finally
                {
                    if (ligou) AnimationMode.StopAnimationMode();
                }

                if (antiga) { cam.targetTexture = null; return log.ToString(); }
                // a arma solta, de três quartos (as peças na mesma origem)
                bracos.SetActive(false);
                pose.SetActive(false);
                var solta = new GameObject("Arma solta");
                SceneManager.MoveGameObjectToScene(solta, cena);
                foreach (var peca in PecasNovas(a))
                {
                    var p = new GameObject(peca.name);
                    p.transform.SetParent(solta.transform, false);
                    p.AddComponent<MeshFilter>().sharedMesh = peca;
                    p.AddComponent<MeshRenderer>().sharedMaterial = mat;
                }
                var caixa = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var r in solta.GetComponentsInChildren<MeshRenderer>()) caixa.Encapsulate(r.bounds);
                float dist = caixa.extents.magnitude / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
                foreach (var (vista, dir) in new[] { ("34", new Vector3(-0.85f, 0.45f, 0.75f)), ("lado", new Vector3(-1f, 0.05f, 0f)) })
                {
                    var d = dir.normalized;
                    camGo.transform.position = caixa.center + d * dist;
                    camGo.transform.rotation = Quaternion.LookRotation(-d, Vector3.up);
                    Gravar(cam, rt, Path.Combine(pasta, $"{a.nome.ToLower()}_unity_{vista}.png"));
                }
                cam.targetTexture = null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(cena);
                foreach (var o in criados) if (o != null) Object.DestroyImmediate(o);
            }
            return log.ToString();
        }

        static void Luz(Scene cena, Vector3 giro, float intensidade, Color cor)
        {
            var go = new GameObject("Luz");
            SceneManager.MoveGameObjectToScene(go, cena);
            go.transform.rotation = Quaternion.Euler(giro);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensidade;
            l.color = cor;
            l.shadows = LightShadows.Soft;
        }

        static void Gravar(Camera cam, RenderTexture rt, string caminho)
        {
            cam.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
