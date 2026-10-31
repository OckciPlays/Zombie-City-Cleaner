using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A mobília nova (o Plano_Mobilia.md da pasta de arte, 29/10/2026), do Mobilia_Nova.fbx (o mobilia_nova.py do
    /// Mobilia.blend e os lotes de cada cômodo) e da tira das texturas do mobilia_texturas.py (Art/Mobilia/T_Mobilia.png):
    ///  - a Texture2DArray das texturas: a própria tira, importada como array (uma camada por textura, a quantidade do
    ///    CatalogoDaMobilia.Texturas), em sRGB, comprimida, com os mipmaps;
    ///  - o material da mobília (Art/Mobilia/M_Mobilia.mat, o shader ZombieCityCleaner/Mobilia);
    ///  - um prefab por móvel (Prefabs/MobiliaNova/&lt;nome&gt;.prefab): o corpo, com a MeshCollider e o LODGroup; as partes
    ///    que abrem (&lt;nome&gt;.Porta.&lt;n&gt; e &lt;nome&gt;.Gaveta.&lt;n&gt;, filhas dele, com a origem na dobradiça ou na frente da gaveta),
    ///    com a BoxCollider e o ParteQueAbre; e o de longe (&lt;nome&gt;_Longe), o LOD 1.
    /// As malhas do FBX vêm com um submesh por textura (um material por textura no Blender): aqui viram um submesh só, um
    /// material para todos os móveis, em Art/Mobilia/Mobilia_Nova_Malhas.asset.
    /// Menu Zombie City Cleaner > Mobília nova > Montar; depois, Exemplos > Variações das construções (o mostruário).
    /// </summary>
    public static class MontagemDaMobiliaNova
    {
        const string Pasta = "Assets/_Game/Art/Mobilia";
        public const string Fbx = Pasta + "/Mobilia_Nova.fbx";
        /// <summary>A tira das texturas (o Texturas_Mobilia/Mobilia_Texturas.png do mobilia_texturas.py).</summary>
        const string ArquivoDaArray = Pasta + "/T_Mobilia.png";
        const string ArquivoDoMaterial = Pasta + "/M_Mobilia.mat";
        const string ArquivoDasMalhas = Pasta + "/Mobilia_Nova_Malhas.asset";
        public const string PastaDosPrefabs = "Assets/_Game/Prefabs/MobiliaNova";
        /// <summary>Até onde vai o de perto e o de longe (a altura na tela, como no LODGroup).</summary>
        const float AtePerto = 0.08f, AteLonge = 0.012f;

        /// <summary>A tabela das medidas de cada móvel, escrita a partir dos prefabs (o gerador do mapa a lê em outras threads).</summary>
        const string ArquivoDasMedidas = "Assets/_Game/Scripts/Mundo/Mobilia/MedidasDaMobilia.cs";

        [MenuItem("Zombie City Cleaner/Mobília nova/Montar (texturas, material e prefabs)")]
        public static void Montar() => Debug.Log(MontarTudo());

        /// <summary>O Montar, devolvendo o relatório (o tamanho, os triângulos e as partes de cada móvel).</summary>
        public static string MontarTudo()
        {
            Importacao();
            var array = Texturas();
            var material = Material(array);
            var log = new StringBuilder($"[Mobília nova] {array.depth} texturas de {array.width} px, o material {ArquivoDoMaterial}\n");
            foreach (var p in Prefabs(material, log)) log.AppendLine($"  {AssetDatabase.GetAssetPath(p)}");
            AssetDatabase.SaveAssets();
            log.AppendLine(EscreverMedidas());
            return log.ToString();
        }

        /// <summary>
        /// O MedidasDaMobilia.cs: de cada prefab, a caixa do corpo com as partes fechadas (sem o de longe), no tamanho real:
        /// o tamanho e onde fica o meio dela (em x e z) e o pé (o y de baixo). Se mudou, a Unity recompila.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Mobília nova/Escrever as medidas")]
        public static string EscreverMedidas()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", ci) + "f";
            var s = new StringBuilder();
            s.AppendLine("// Escrito pela Zombie City Cleaner > Mobília nova > Escrever as medidas (MontagemDaMobiliaNova): não mude à mão.");
            s.AppendLine("using System.Collections.Generic;");
            s.AppendLine("using UnityEngine;");
            s.AppendLine();
            s.AppendLine("namespace ZombieCityCleaner.Mundo");
            s.AppendLine("{");
            s.AppendLine("    public static partial class MedidasDaMobilia");
            s.AppendLine("    {");
            s.AppendLine("        // o modelo: o tamanho (largura em x, altura, fundo em z), o meio da caixa em x e z e o y de baixo, no tamanho real");
            s.AppendLine("        static readonly Dictionary<string, (Vector3 tamanho, Vector2 meio, float baixo)> _medidas = new Dictionary<string, (Vector3, Vector2, float)>");
            s.AppendLine("        {");
            int n = 0;
            foreach (var p in Carregar())
            {
                var caixa = Caixa(p);
                s.AppendLine($"            [\"{p.name}\"] = (new Vector3({F(caixa.size.x)}, {F(caixa.size.y)}, {F(caixa.size.z)}), new Vector2({F(caixa.center.x)}, {F(caixa.center.z)}), {F(caixa.min.y)}),");
                n++;
            }
            s.AppendLine("        };");
            s.AppendLine("    }");
            s.AppendLine("}");
            string texto = s.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            string velho = File.Exists(ArquivoDasMedidas) ? File.ReadAllText(ArquivoDasMedidas) : null;
            if (velho == texto) return $"[Mobília nova] as medidas de {n} móveis não mudaram";
            File.WriteAllText(ArquivoDasMedidas, texto, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(ArquivoDasMedidas);
            return $"[Mobília nova] as medidas de {n} móveis em {ArquivoDasMedidas}";
        }

        /// <summary>A caixa do móvel (o corpo e as partes, fechadas; sem o de longe), no espaço dele.</summary>
        public static Bounds Caixa(GameObject prefab)
        {
            var b = new Bounds();
            bool tem = false;
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.name.EndsWith("_Longe")) continue;
                var m = f.transform == prefab.transform ? Matrix4x4.identity : prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = m.MultiplyPoint3x4(c);
                    if (!tem) { b = new Bounds(w, Vector3.zero); tem = true; }
                    else b.Encapsulate(w);
                }
            }
            return b;
        }

        /// <summary>Os prefabs feitos (para a cena das variações), na ordem do nome.</summary>
        public static GameObject[] Carregar() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { PastaDosPrefabs })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToArray();

        // o FBX legível (as malhas são copiadas daqui), sem os materiais (o material é o da mobília)
        static void Importacao()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp.isReadable && imp.materialImportMode == ModelImporterMaterialImportMode.None && !imp.importAnimation) return;
            imp.isReadable = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SaveAndReimport();
        }

        // a tira das texturas (uma embaixo da outra, a 0 em cima) importada como a Texture2DArray, comprimida, com os
        // mipmaps de cada uma; a quantidade é a do catálogo
        static Texture2DArray Texturas()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ArquivoDaArray);
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.textureShape = TextureImporterShape.Texture2DArray;
            s.flipbookRows = CatalogoDaMobilia.Texturas.Length;
            s.flipbookColumns = 1;
            s.sRGBTexture = true;
            s.mipmapEnabled = true;
            s.wrapMode = TextureWrapMode.Repeat;
            s.filterMode = FilterMode.Trilinear;
            s.aniso = 4;
            s.alphaSource = TextureImporterAlphaSource.FromInput;
            s.alphaIsTransparency = false;
            imp.SetTextureSettings(s);
            imp.maxTextureSize = 16384;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2DArray>(ArquivoDaArray);
        }

        static Material Material(Texture2DArray array)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ArquivoDoMaterial);
            if (mat == null)
            {
                mat = new Material(Shader.Find("ZombieCityCleaner/Mobilia")) { name = "M_Mobilia", enableInstancing = true };
                AssetDatabase.CreateAsset(mat, ArquivoDoMaterial);
            }
            mat.SetTexture("_Texturas", array);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static List<GameObject> Prefabs(Material material, StringBuilder log)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            Directory.CreateDirectory(PastaDosPrefabs);
            AssetDatabase.DeleteAsset(ArquivoDasMalhas);
            // o principal do arquivo é uma malha vazia (a Unity dá ao principal o nome do arquivo, e os móveis ficam com o deles)
            AssetDatabase.CreateAsset(new Mesh(), ArquivoDasMalhas);
            void Guardar(Mesh m) => AssetDatabase.AddObjectToAsset(m, ArquivoDasMalhas);

            var feitos = new List<GameObject>();
            foreach (Transform t in modelo.transform)
            {
                if (t.name.EndsWith("_Longe")) continue;
                var filtro = t.GetComponent<MeshFilter>();
                if (filtro == null) continue;
                if (t.localRotation != Quaternion.identity || t.localScale != Vector3.one)
                    log.AppendLine($"  ATENÇÃO: {t.name} tem o giro {t.localEulerAngles} e a escala {t.localScale} no FBX (a malha vai sem eles)");

                var raiz = new GameObject(t.name);
                var corpo = Juntar(filtro.sharedMesh, t.name);
                Guardar(corpo);
                var perto = new List<Renderer> { Desenhar(raiz, corpo, material) };
                raiz.AddComponent<MeshCollider>().sharedMesh = corpo;
                int triPerto = Triangulos(corpo), portas = 0, gavetas = 0;
                var (indiceMin, indiceMax) = Indices(corpo);

                foreach (Transform p in t)
                {
                    var f = p.GetComponent<MeshFilter>();
                    if (f == null) continue;
                    if (p.childCount > 0) log.AppendLine($"  ATENÇÃO: {p.name} tem filhos no FBX (ficaram de fora)");
                    var go = new GameObject(p.name);
                    go.transform.SetParent(raiz.transform, false);
                    go.transform.SetLocalPositionAndRotation(p.localPosition, p.localRotation);
                    go.transform.localScale = p.localScale;
                    var malha = Juntar(f.sharedMesh, p.name);
                    Guardar(malha);
                    perto.Add(Desenhar(go, malha, material));
                    var caixa = go.AddComponent<BoxCollider>();
                    caixa.center = malha.bounds.center;
                    caixa.size = malha.bounds.size;
                    var parte = ParteQueAbre.Configurar(go, malha);
                    if (parte != null && parte.tipo == ParteQueAbre.Tipo.Gaveta) gavetas++;
                    else if (parte != null) portas++;
                    triPerto += Triangulos(malha);
                    var (a, b) = Indices(malha);
                    indiceMin = Mathf.Min(indiceMin, a);
                    indiceMax = Mathf.Max(indiceMax, b);
                }

                var lods = new List<LOD> { new LOD(AtePerto, perto.ToArray()) };
                int triLonge = 0;
                var longe = modelo.transform.Find(t.name + "_Longe");
                if (longe != null && longe.GetComponent<MeshFilter>() != null)
                {
                    var go = new GameObject(longe.name);
                    go.transform.SetParent(raiz.transform, false);
                    var malha = Juntar(longe.GetComponent<MeshFilter>().sharedMesh, longe.name);
                    Guardar(malha);
                    lods.Add(new LOD(AteLonge, new[] { Desenhar(go, malha, material) }));
                    triLonge = Triangulos(malha);
                }
                else lods[0] = new LOD(AteLonge, perto.ToArray());
                var grupo = raiz.AddComponent<LODGroup>();
                grupo.SetLODs(lods.ToArray());
                grupo.RecalculateBounds();

                var tam = corpo.bounds.size;
                log.AppendLine($"  {t.name}: {tam.x:0.00} × {tam.z:0.00} × {tam.y:0.00} m (o corpo), {triPerto} triângulos de perto e {triLonge} de longe, " +
                               $"{portas} portas e {gavetas} gavetas, as texturas {indiceMin:0.##} a {indiceMax:0.##}");
                var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, $"{PastaDosPrefabs}/{t.name}.prefab");
                Object.DestroyImmediate(raiz);
                feitos.Add(prefab);
            }
            return feitos;
        }

        static Renderer Desenhar(GameObject go, Mesh malha, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return r;
        }

        // a malha do FBX com os submeshes (um por textura) num só
        static Mesh Juntar(Mesh m, string nome)
        {
            var r = new Mesh { name = nome, indexFormat = m.vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            r.SetVertices(m.vertices);
            r.SetNormals(m.normals);
            var uv = new List<Vector2>();
            m.GetUVs(0, uv);
            r.SetUVs(0, uv);
            m.GetUVs(1, uv);
            r.SetUVs(1, uv);
            r.SetColors(m.colors);
            var tris = new List<int>();
            for (int s = 0; s < m.subMeshCount; s++) tris.AddRange(m.GetTriangles(s));
            r.SetTriangles(tris, 0);
            r.RecalculateBounds();
            return r;
        }

        static int Triangulos(Mesh m) => (int)(m.GetIndexCount(0) / 3);

        // o menor e o maior índice de textura (o UV1.x), para conferir a ordem dos UVs do FBX
        static (float, float) Indices(Mesh m)
        {
            var uv = new List<Vector2>();
            m.GetUVs(1, uv);
            if (uv.Count == 0) return (float.NaN, float.NaN);
            return (uv.Min(u => u.x), uv.Max(u => u.x));
        }
    }
}
