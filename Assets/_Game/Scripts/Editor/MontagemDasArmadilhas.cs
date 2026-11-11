using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As armadilhas (11/11/2026, a etapa 5a do ramo Armadilhas e defesas). Menu: Zombie City Cleaner > Habilidades > Montar as
    /// armadilhas. Pode rodar de novo à vontade; não mexe nas cenas nem no prefab do jogador:
    ///  - importa o <c>Art/Armadilhas/Armadilhas.fbx</c> (as montadas, já com o Y para cima e a frente em +Z) e o
    ///    <c>Loot_Armadilhas.fbx</c> (os itens), na paleta (o material Pistola);
    ///  - separa as latas do alarme do resto (as ilhas da malha penduradas no meio do barbante: <c>Malhas/Alarme_Latas</c>, com
    ///    a origem no vazio Latas, e <c>Malhas/Alarme_SemLatas</c>);
    ///  - junta os 4 itens (<c>Characters/Jogador/Malhas/Loot_&lt;modelo&gt;</c>, os ícones e as fotos) e o das tábuas para
    ///    barricada (<c>Loot_Tabuas</c>, o ícone e a foto: a etapa 5b; só ele, sem refazer o resto: Montar as tábuas da barricada);
    ///  - faz o material do rastro da cerca (<c>Art/Armadilhas/Rastro.mat</c>) e o <c>Resources/ItensDasArmadilhas.prefab</c>
    ///    (o <see cref="ItensDasArmadilhas"/>), com as malhas das barricadas da 5b (porta e janela, inteira e danificada).
    /// </summary>
    public static class MontagemDasArmadilhas
    {
        const string Arte = "Assets/_Game/Art/Armadilhas";
        internal const string FbxMontadas = Arte + "/Armadilhas.fbx";
        internal const string FbxItens = Arte + "/Loot_Armadilhas.fbx";
        const string PastaDasMalhas = Arte + "/Malhas";
        const string MaterialDoRastro = Arte + "/Rastro.mat";
        const string Prefab = "Assets/_Game/Resources/ItensDasArmadilhas.prefab";
        const string MalhaDasTabuas = "Assets/_Game/Art/Characters/Jogador/Malhas/Loot_Tabuas.asset";

        [MenuItem("Zombie City Cleaner/Habilidades/Montar as armadilhas")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as armadilhas.");
                return;
            }
            foreach (var f in new[] { FbxMontadas, FbxItens })
                if (!File.Exists(f)) throw new FileNotFoundException("Falta um modelo das armadilhas.", f);
            MontagemDasArmas.ImportarAvulsas(FbxMontadas);
            MontagemDasArmas.ImportarAvulsas(FbxItens);
            Directory.CreateDirectory(PastaDasMalhas);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var go = new GameObject("ItensDasArmadilhas");
            int qualidade = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);   // (as fotos com a qualidade Mobile, como as do fogo)
            try
            {
                var d = go.AddComponent<ItensDasArmadilhas>();
                d.material = material;
                d.materialDoRastro = Rastro();
                var relatorio = new List<string>();
                // as montadas: pelo nome exato (o BarricadaPorta é o começo do BarricadaPorta_Danificada)
                var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxMontadas).OfType<Mesh>().ToList();
                Mesh Malha(string nome)
                {
                    var m = malhas.FirstOrDefault(x => x.name == nome);
                    if (m == null) Debug.LogWarning($"[Armadilhas] Falta a {nome} no {FbxMontadas}.");
                    return m;
                }
                var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(FbxMontadas).transform;
                Vector3 Vazio(string pai, string nome, Vector3 senao)
                {
                    var p = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == pai);
                    var v = p != null ? p.Find(nome) : null;
                    if (v == null) Debug.LogWarning($"[Armadilhas] Falta o vazio {pai}/{nome} no {FbxMontadas}.");
                    return v != null ? v.localPosition : senao;
                }
                d.alarme = Malha("Armadilha_AlarmeLatas_corpo");
                d.tapete = Malha("Armadilha_TapetePregos_corpo");
                d.estacas = Malha("Armadilha_Estacas_corpo");
                d.cerca = Malha("Armadilha_CercaFogo_corpo");
                d.barricadaPorta = Malha("Armadilha_BarricadaPorta_corpo");
                d.barricadaPortaDanificada = Malha("Armadilha_BarricadaPorta_Danificada_corpo");
                d.barricadaJanela = Malha("Armadilha_BarricadaJanela_corpo");
                d.barricadaJanelaDanificada = Malha("Armadilha_BarricadaJanela_Danificada_corpo");
                d.vazioDasLatas = Vazio("Armadilha_AlarmeLatas_corpo", "Latas", new Vector3(0f, 0.525f, 0f));
                d.pontaDasEstacas = Vazio("Armadilha_Estacas_corpo", "Ponta", new Vector3(0f, 0.89f, 0.84f));
                d.pavioDaCerca = Vazio("Armadilha_CercaFogo_corpo", "Pavio", new Vector3(0f, 0.01f, 0.465f));
                foreach (var m in new[] { d.alarme, d.tapete, d.estacas, d.cerca, d.barricadaPorta, d.barricadaPortaDanificada, d.barricadaJanela, d.barricadaJanelaDanificada })
                    if (m != null) relatorio.Add($"{m.name}: de {m.bounds.min.ToString("F3")} a {m.bounds.max.ToString("F3")}");
                relatorio.Add($"Vazios: Latas {d.vazioDasLatas.ToString("F3")}, Ponta {d.pontaDasEstacas.ToString("F3")}, Pavio {d.pavioDaCerca.ToString("F3")}");
                // as latas soltas do alarme
                if (d.alarme != null)
                {
                    SepararAsLatas(d.alarme, d.vazioDasLatas, out var semLatas, out var latas, out int ilhas, out int ilhasDasLatas);
                    d.alarmeSemLatas = Gravar(semLatas, $"{PastaDasMalhas}/Alarme_SemLatas.asset");
                    d.latas = Gravar(latas, $"{PastaDasMalhas}/Alarme_Latas.asset");
                    relatorio.Add($"O alarme: {ilhas} ilhas, {ilhasDasLatas} nas latas ({d.latas.vertexCount} vértices, de {d.latas.bounds.min.ToString("F3")} a {d.latas.bounds.max.ToString("F3")} do vazio); o resto, {d.alarmeSemLatas.vertexCount} vértices");
                }
                // os 4 itens: a malha do chão, o ícone e a foto
                int n = ItensDasArmadilhas.Itens4.Length;
                d.malhasDosItens = new Mesh[n];
                d.icones = new Texture2D[n];
                d.fotos = new Texture2D[n];
                for (int k = 0; k < n; k++)
                {
                    int i = Itens.IndiceDoLoot(ItensDasArmadilhas.Itens4[k]);
                    string modelo = Itens.ModeloDoLoot(i);
                    MontagemDasArmas.Juntar(FbxItens, $"Item_{modelo}_", MontagemDasArmas.MalhaDoLoot(i));
                    AssetDatabase.SaveAssets();
                    var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDoLoot(i));
                    d.malhasDosItens[k] = malha;
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.IconeDoLoot(i), 128, 128, false, true);
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.FotoDoLoot(i), 600, 500, false, false);
                    relatorio.Add($"Item {modelo}: {malha.bounds.size.ToString("F3")}");
                }
                // as tábuas para barricada (a etapa 5b: o item, o ícone e a foto)
                FotografarAsTabuas(material, relatorio);
                AssetDatabase.Refresh();
                for (int k = 0; k < n; k++)
                {
                    int i = Itens.IndiceDoLoot(ItensDasArmadilhas.Itens4[k]);
                    d.icones[k] = Imagem(MontagemDasArmas.IconeDoLoot(i));
                    d.fotos[k] = Imagem(MontagemDasArmas.FotoDoLoot(i));
                }
                PorAsTabuas(d);
                Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
                PrefabUtility.SaveAsPrefabAsset(go, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Armadilhas] {Prefab}:\n{string.Join("\n", relatorio)}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                QualitySettings.SetQualityLevel(qualidade, false);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Só as tábuas para barricada (11/11/2026, a etapa 5b): o item (a malha, o ícone e a foto) no prefab que já existe,
        /// sem refazer o resto. Menu: Zombie City Cleaner > Habilidades > Montar as tábuas da barricada.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Habilidades/Montar as tábuas da barricada")]
        public static void MontarAsTabuas()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as tábuas.");
                return;
            }
            if (!File.Exists(Prefab))
            {
                Montar();
                return;
            }
            MontagemDasArmas.ImportarAvulsas(FbxItens);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            int qualidade = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);
            var relatorio = new List<string>();
            try
            {
                FotografarAsTabuas(material, relatorio);
                AssetDatabase.Refresh();
            }
            finally
            {
                QualitySettings.SetQualityLevel(qualidade, false);
            }
            var raiz = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                PorAsTabuas(raiz.GetComponent<ItensDasArmadilhas>());
                PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Armadilhas] As tábuas no {Prefab}:\n{string.Join("\n", relatorio)}");
        }

        // o item das tábuas: a malha do Loot_Armadilhas.fbx (o Item_Tabuas_), o ícone e a foto (como os 4 itens da 5a)
        static void FotografarAsTabuas(Material material, List<string> relatorio)
        {
            int i = Itens.IndiceDoLoot(Item.TabuasBarricada);
            MontagemDasArmas.Juntar(FbxItens, "Item_Tabuas_", MalhaDasTabuas);
            AssetDatabase.SaveAssets();
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDasTabuas);
            if (malha == null || i < 0)
            {
                Debug.LogWarning($"[Armadilhas] Faltam as tábuas ({MalhaDasTabuas}, o índice do saque {i}).");
                return;
            }
            MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.IconeDoLoot(i), 128, 128, false, true);
            MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.FotoDoLoot(i), 600, 500, false, false);
            relatorio.Add($"Tábuas para barricada: {malha.bounds.size.ToString("F3")} ({MontagemDasArmas.IconeDoLoot(i)})");
        }

        // as tábuas no componente do prefab (depois do Refresh: as imagens já importadas)
        static void PorAsTabuas(ItensDasArmadilhas d)
        {
            if (d == null) return;
            int i = Itens.IndiceDoLoot(Item.TabuasBarricada);
            d.tabuasDoItem = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDasTabuas);
            if (i < 0) return;
            d.iconeDasTabuas = Imagem(MontagemDasArmas.IconeDoLoot(i));
            d.fotoDasTabuas = Imagem(MontagemDasArmas.FotoDoLoot(i));
        }

        /// <summary>
        /// Separa as latas do alarme: as ilhas da malha (os pedaços ligados, com os vértices iguais soldados) que ficam no meio
        /// do barbante, abaixo dele (as latas, os fios delas e a etiqueta), viram uma malha com a origem no vazio Latas; o resto
        /// (as estacas e o barbante) fica na outra.
        /// </summary>
        static void SepararAsLatas(Mesh alarme, Vector3 vazio, out Mesh semLatas, out Mesh latas, out int ilhas, out int ilhasDasLatas)
        {
            var v = alarme.vertices;
            var tri = alarme.triangles;
            // as ilhas: um por vértice, unidos pelos triângulos e pelas posições iguais
            var pai = Enumerable.Range(0, v.Length).ToArray();
            int Raiz(int a)
            {
                while (pai[a] != a) a = pai[a] = pai[pai[a]];
                return a;
            }
            void Unir(int a, int b)
            {
                a = Raiz(a);
                b = Raiz(b);
                if (a != b) pai[a] = b;
            }
            var mesmos = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var chave = Vector3Int.RoundToInt(v[i] * 10000f);
                if (mesmos.TryGetValue(chave, out int j)) Unir(i, j);
                else mesmos[chave] = i;
            }
            for (int t = 0; t < tri.Length; t += 3)
            {
                Unir(tri[t], tri[t + 1]);
                Unir(tri[t], tri[t + 2]);
            }
            var caixas = new Dictionary<int, Bounds>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Raiz(i);
                if (caixas.TryGetValue(r, out var b))
                {
                    b.Encapsulate(v[i]);
                    caixas[r] = b;
                }
                else caixas[r] = new Bounds(v[i], Vector3.zero);
            }
            ilhas = caixas.Count;
            // a lata: no meio (longe das estacas), abaixo do barbante e pequena
            var dasLatas = new HashSet<int>(caixas.Where(kv => Mathf.Abs(kv.Value.center.x) < 0.5f && kv.Value.size.x < 0.45f
                                                               && kv.Value.max.y < vazio.y + 0.08f && kv.Value.min.y > 0.1f).Select(kv => kv.Key));
            ilhasDasLatas = dasLatas.Count;
            var triLatas = new List<int>();
            var triResto = new List<int>();
            for (int t = 0; t < tri.Length; t += 3)
                (dasLatas.Contains(Raiz(tri[t])) ? triLatas : triResto).AddRange(new[] { tri[t], tri[t + 1], tri[t + 2] });
            semLatas = Parte(alarme, triResto, Vector3.zero, "Alarme_SemLatas");
            latas = Parte(alarme, triLatas, vazio, "Alarme_Latas");
        }

        // uma parte da malha (só os vértices desses triângulos), deslocada pela origem nova
        static Mesh Parte(Mesh m, List<int> tri, Vector3 origem, string nome)
        {
            var v = m.vertices;
            var nrm = m.normals;
            var uv = m.uv;
            var cores = m.colors;
            var novo = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nuv = new List<Vector2>();
            var ncor = new List<Color>();
            var nt = new int[tri.Count];
            for (int i = 0; i < tri.Count; i++)
            {
                int a = tri[i];
                if (!novo.TryGetValue(a, out int b))
                {
                    b = novo[a] = nv.Count;
                    nv.Add(v[a] - origem);
                    if (nrm.Length == v.Length) nn.Add(nrm[a]);
                    if (uv.Length == v.Length) nuv.Add(uv[a]);
                    if (cores.Length == v.Length) ncor.Add(cores[a]);
                }
                nt[i] = b;
            }
            var r = new Mesh { name = nome };
            r.SetVertices(nv);
            if (nn.Count == nv.Count) r.SetNormals(nn);
            if (nuv.Count == nv.Count) r.SetUVs(0, nuv);
            if (ncor.Count == nv.Count) r.SetColors(ncor);
            r.SetTriangles(nt, 0);
            if (nn.Count != nv.Count) r.RecalculateNormals();
            r.RecalculateBounds();
            return r;
        }

        // grava a malha (por cima da que já existe, sem trocar o arquivo: as referências ficam)
        static Mesh Gravar(Mesh m, string caminho)
        {
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha == null)
            {
                AssetDatabase.CreateAsset(m, caminho);
                return m;
            }
            velha.Clear();
            velha.SetVertices(m.vertices);
            velha.SetNormals(m.normals);
            velha.SetUVs(0, m.uv);
            if (m.colors.Length == m.vertexCount) velha.SetColors(m.colors);
            velha.SetTriangles(m.triangles, 0);
            velha.RecalculateBounds();
            EditorUtility.SetDirty(velha);
            Object.DestroyImmediate(m);
            return velha;
        }

        // o material do rastro de combustível: escuro, molhado e meio transparente (o URP Lit, para escurecer com a noite)
        static Material Rastro()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDoRastro);
            bool novo = m == null;
            if (novo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Rastro" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Smoothness", 0.25f);   // (0,75 brilhava branco com o céu de dia: o rastro parecia uma faixa clara)
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.03f, 0.6f));
            m.SetShaderPassEnabled("ShadowCaster", false);
            if (novo) AssetDatabase.CreateAsset(m, MaterialDoRastro);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // a imagem com a importação dos ícones (a transparência, sem mipmaps, sem compressão)
        static Texture2D Imagem(string caminho)
        {
            if (!(AssetImporter.GetAtPath(caminho) is TextureImporter imp)) return null;
            if (!(imp.alphaIsTransparency && !imp.mipmapEnabled && imp.textureCompression == TextureImporterCompression.Uncompressed && imp.npotScale == TextureImporterNPOTScale.None))
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }
    }
}
