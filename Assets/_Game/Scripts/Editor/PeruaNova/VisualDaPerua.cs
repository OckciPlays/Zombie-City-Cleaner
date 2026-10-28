using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O visual novo da perua (28/10/2026): troca só a malha e os materiais das 11 peças que se veem no prefab do carro
    /// (a carroceria, o capô, a tampa traseira, as 4 portas, as 4 rodas e o volante) pelas do
    /// `Art/Veiculos/PeruaNova/PeruaNova.fbx` (feito pelo `Blender/PeruaNova/perua_nova.py`), com os materiais PBR novos
    /// (URP Lit com as texturas assadas). Nada de física, rede, gatilhos, lugares, câmeras ou luzes muda: a montagem
    /// confere uma "impressão" de todos os componentes (menos a malha e os materiais) antes e depois e não salva se algo
    /// for diferente. Os submeshes das lentes (<see cref="Carro.submeshFarol"/> e <see cref="Carro.submeshLanterna"/>)
    /// apontam para os do modelo novo.
    /// Menus: Zombie City Cleaner > Veículos > Aplicar o visual novo da perua / Voltar ao visual antigo da perua.
    /// Atenção: a "Montar veículos" (<see cref="MontagemDosVeiculos"/>) refaz o prefab com o Carro.fbx antigo; depois
    /// dela, rode "Aplicar o visual novo da perua" de novo.
    /// </summary>
    public static class VisualDaPerua
    {
        const string Pasta = "Assets/_Game/Art/Veiculos/PeruaNova";
        const string Fbx = Pasta + "/PeruaNova.fbx";
        const string PastaTexturas = Pasta + "/Texturas";
        const string PastaMateriais = Pasta + "/Materiais";
        const string Prefab = "Assets/_Game/Prefabs/Veiculos/Carro.prefab";
        // o backup do prefab (fora de Assets, para a Unity não importar)
        const string Backups = "Backups";

        /// <summary>O objeto do prefab (filho direto da raiz, com MeshFilter) e a peça do FBX que vai nele.</summary>
        static readonly (string objeto, string peca)[] Pecas =
        {
            ("Carroceria", "Perua_Carroceria"), ("Capô", "Perua_Capo"), ("Tampa traseira", "Perua_Tampa"), ("Volante", "Perua_Volante"),
            ("Porta DE", "Perua_Porta_DE"), ("Porta DD", "Perua_Porta_DD"), ("Porta TE", "Perua_Porta_TE"), ("Porta TD", "Perua_Porta_TD"),
            ("Roda DE (modelo)", "Perua_Roda"), ("Roda DD (modelo)", "Perua_Roda"), ("Roda TE (modelo)", "Perua_Roda"), ("Roda TD (modelo)", "Perua_Roda"),
        };

        [MenuItem("Zombie City Cleaner/Veículos/Aplicar o visual novo da perua")]
        public static void Aplicar()
        {
            if (!File.Exists(Fbx)) throw new FileNotFoundException("Rode o Blender/PeruaNova/perua_nova.py (exportar) antes.", Fbx);
            Backup();
            Texturas();
            var mats = Materiais();
            Importar(mats);
            TrocarNoPrefab(mats);
        }

        [MenuItem("Zombie City Cleaner/Veículos/Voltar ao visual antigo da perua")]
        public static void VoltarAoAntigo()
        {
            // o backup mais antigo é o do prefab de antes do visual novo
            var pasta = Directory.Exists(Backups) ? Directory.GetDirectories(Backups, "Perua_*").OrderBy(d => d).FirstOrDefault() : null;
            string copia = pasta == null ? null : Path.Combine(pasta, "Prefabs/Veiculos/Carro.prefab");
            if (copia == null || !File.Exists(copia)) { Debug.LogError("Sem backup do prefab em Backups/Perua_*."); return; }
            File.Copy(copia, Prefab, true);
            AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate);
            Debug.Log($"O prefab do carro voltou ao de {copia}.");
        }

        // ------------------------------------------------------------------------------------------ backup
        static void Backup()
        {
            string pasta = Path.Combine(Backups, "Perua_antes_do_visual_novo_2026-10-28");
            if (Directory.Exists(pasta)) return;   // o primeiro backup fica (é o do visual antigo)
            Directory.CreateDirectory(Path.Combine(pasta, "Prefabs/Veiculos"));
            File.Copy(Prefab, Path.Combine(pasta, "Prefabs/Veiculos/Carro.prefab"));
            File.Copy(Prefab + ".meta", Path.Combine(pasta, "Prefabs/Veiculos/Carro.prefab.meta"));
            Debug.Log($"Backup do prefab do carro em {pasta}.");
        }

        // ------------------------------------------------------------------------------------------ texturas e materiais
        static void Texturas()
        {
            foreach (var final in new[] { "Perua_Lataria", "Perua_Interior" })
            {
                Textura($"{PastaTexturas}/{final}_Cor.png", true, false);
                Textura($"{PastaTexturas}/{final}_MS.png", false, false);
                Textura($"{PastaTexturas}/{final}_Normal.png", false, true);
            }
        }

        static void Textura(string caminho, bool srgb, bool normal)
        {
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Textura da perua não encontrada (rode o perua_nova.py: assar e compor).", caminho);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        class Mats
        {
            public Material lataria, interior, vidro, farol, lanterna;
        }

        static Material Material(string nome)
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

        // URP Lit, fluxo metálico: a cor, o MS (R = metal, G = oclusão, A = suavidade) e o normal
        static Material Pbr(string nome, string atlas, float suavidade = 1f, float oclusao = 0.85f)
        {
            var m = Material(nome);
            var cor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Cor.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_MS.png");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/{atlas}_Normal.png");
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_BaseMap", cor);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", suavidade);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_BumpMap", nor);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ms);
            m.SetFloat("_OcclusionStrength", oclusao);
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Transparente(string nome, Color cor, float suavidade, Texture normal)
        {
            var m = Material(nome);
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
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            // sem sombra (como o Vidro.mat do carro antigo: senão o carro por dentro fica no escuro)
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mats Materiais()
        {
            var r = new Mats
            {
                lataria = Pbr("Perua_Lataria", "Perua_Lataria"),
                interior = Pbr("Perua_Interior", "Perua_Interior", 1f, 1f),
                // a lanterna apagada: o vermelho assado na lataria, mais lisa (a Unity troca pelo LanternaAcesa)
                lanterna = Pbr("Perua_Lanterna", "Perua_Lataria", 1f, 0.5f),
                vidro = Transparente("Perua_Vidro", new Color(0.11f, 0.14f, 0.16f, 0.34f), 0.93f, null),
                // o farol apagado: a lente clara e transparente (vê-se o refletor cromado atrás); acesa, vira o FarolAceso
                farol = Transparente("Perua_Farol", new Color(0.92f, 0.94f, 0.96f, 0.16f), 0.96f,
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{PastaTexturas}/Perua_Lataria_Normal.png")),
            };
            AssetDatabase.SaveAssets();
            return r;
        }

        // ------------------------------------------------------------------------------------------ o FBX
        static void Importar(Mats m)
        {
            var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter;
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
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Perua_Lataria"), m.lataria);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Perua_Interior"), m.interior);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Perua_Vidro"), m.vidro);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Perua_Farol"), m.farol);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Perua_Lanterna"), m.lanterna);
            imp.SaveAndReimport();
        }

        static (Mesh malha, Material[] mats) Peca(string nome)
        {
            var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nome);
            if (t == null) throw new System.Exception($"A peça {nome} não está no {Fbx}.");
            return (t.GetComponent<MeshFilter>().sharedMesh, t.GetComponent<MeshRenderer>().sharedMaterials);
        }

        // ------------------------------------------------------------------------------------------ o prefab
        static void TrocarNoPrefab(Mats m)
        {
            var raiz = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                string antes = Impressao(raiz);
                var carro = raiz.GetComponent<Carro>();
                foreach (var (objeto, peca) in Pecas)
                {
                    var t = raiz.transform.Cast<Transform>().FirstOrDefault(x => x.name == objeto && x.GetComponent<MeshFilter>() != null);
                    if (t == null) throw new System.Exception($"O prefab do carro não tem a peça visual \"{objeto}\".");
                    var (malha, mats) = Peca(peca);
                    t.GetComponent<MeshFilter>().sharedMesh = malha;
                    t.GetComponent<MeshRenderer>().sharedMaterials = mats;
                }
                var lentes = carro.carroceria.sharedMaterials;
                carro.submeshFarol = System.Array.IndexOf(lentes, m.farol);
                carro.submeshLanterna = System.Array.IndexOf(lentes, m.lanterna);
                if (carro.submeshFarol < 0 || carro.submeshLanterna < 0)
                    throw new System.Exception("Não achei os submeshes do farol e da lanterna na Perua_Carroceria.");
                string depois = Impressao(raiz);
                if (antes != depois)
                {
                    Debug.LogError("O visual novo mudaria algo além da malha e dos materiais; o prefab NÃO foi salvo.\n" + Diferenca(antes, depois));
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
                Debug.Log($"Visual novo da perua aplicado no {Prefab} (12 peças; farol no submesh {carro.submeshFarol}, lanterna no {carro.submeshLanterna}). A física, a rede e os pontos não mudaram (impressão de {antes.Length} caracteres igual).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        /// <summary>
        /// Tudo o que importa no prefab, menos o que o visual troca (a malha e os materiais das peças, e os dois índices das
        /// lentes no Carro): cada objeto (o caminho, a camada, ativo, a posição, o giro e a escala) e cada componente com
        /// todos os campos serializados (o Rigidbody, os WheelColliders, os colisores, a rede, o Carro, as luzes, os sons...).
        /// </summary>
        public static string Impressao(GameObject raiz)
        {
            var sb = new StringBuilder();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                string caminho = Caminho(t, raiz.transform);
                sb.Append(caminho).Append(" L").Append(t.gameObject.layer).Append(t.gameObject.activeSelf ? " on " : " off ")
                  .Append(t.localPosition.ToString("F5")).Append(t.localRotation.ToString("F5")).Append(t.localScale.ToString("F5")).Append('\n');
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) { sb.Append("  (componente sumido)\n"); continue; }
                    if (c is Transform || c is MeshFilter) { sb.Append("  ").Append(c.GetType().Name).Append('\n'); continue; }
                    sb.Append("  ").Append(c.GetType().FullName).Append('\n');
                    var so = new SerializedObject(c);
                    var it = so.GetIterator();
                    bool entrar = true;
                    while (it.Next(entrar))
                    {
                        entrar = true;
                        if (c is Renderer && it.propertyPath.StartsWith("m_Materials")) { entrar = false; continue; }
                        if (c is Carro && (it.propertyPath == "submeshFarol" || it.propertyPath == "submeshLanterna")) continue;
                        if (it.propertyPath == "m_GameObject" || it.propertyPath == "m_ObjectHideFlags" || it.propertyPath.StartsWith("m_CorrespondingSourceObject") ||
                            it.propertyPath.StartsWith("m_PrefabInstance") || it.propertyPath.StartsWith("m_PrefabAsset")) { entrar = false; continue; }
                        sb.Append("    ").Append(it.propertyPath).Append('=').Append(Valor(it, raiz.transform)).Append('\n');
                    }
                }
            }
            return sb.ToString();
        }

        static string Valor(SerializedProperty p, Transform raiz)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    var o = p.objectReferenceValue;
                    if (o == null) return "null";
                    if (o is Component c) return c.GetType().Name + "@" + Caminho(c.transform, raiz);
                    if (o is GameObject g) return "GO@" + Caminho(g.transform, raiz);
                    return o.GetType().Name + ":" + o.name;
                case SerializedPropertyType.Float: return p.floatValue.ToString("R");
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Enum: return p.enumValueIndex.ToString();
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString("R");
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("R");
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString("R");
                case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString("R");
                case SerializedPropertyType.Color: return p.colorValue.ToString("R");
                case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
                case SerializedPropertyType.Rect: return p.rectValue.ToString();
                case SerializedPropertyType.LayerMask: return p.intValue.ToString();
                case SerializedPropertyType.ArraySize: return p.intValue.ToString();
                case SerializedPropertyType.AnimationCurve: return string.Join(";", p.animationCurveValue.keys.Select(k => $"{k.time:R},{k.value:R}"));
                default: return p.hasChildren ? "" : p.propertyType.ToString();
            }
        }

        static string Caminho(Transform t, Transform raiz)
        {
            var partes = new List<string>();
            for (var x = t; x != null && x != raiz; x = x.parent) partes.Add(x.name);
            partes.Reverse();
            return partes.Count == 0 ? "(raiz)" : string.Join("/", partes);
        }

        static string Diferenca(string a, string b)
        {
            var la = a.Split('\n');
            var lb = b.Split('\n');
            var sb = new StringBuilder();
            int n = Mathf.Max(la.Length, lb.Length), mostradas = 0;
            for (int i = 0; i < n && mostradas < 20; i++)
            {
                string x = i < la.Length ? la[i] : "", y = i < lb.Length ? lb[i] : "";
                if (x == y) continue;
                sb.Append("- ").Append(x).Append("\n+ ").Append(y).Append('\n');
                mostradas++;
            }
            return sb.ToString();
        }
    }
}
