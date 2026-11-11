using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A proteção de sucata (11/11/2026, a etapa 3 do ramo Proteção de sucata). Menu: Zombie City Cleaner > Habilidades >
    /// Montar a proteção de sucata. Pode rodar de novo à vontade; não mexe nas cenas nem no prefab do jogador:
    ///  - os modelos (do Blender, a etapa 3 e 4): <c>Art/Characters/Jogador/Protecao_Sucata.fbx</c> (as 6 peças do corpo,
    ///    <c>Protecao_*</c>, cada uma no espaço do osso dela, com a forma "Mulher") e
    ///    <c>Art/Characters/Jogador/Loot_Etapa34.fbx</c> (os itens soltos, <c>Item_&lt;modelo&gt;_corpo</c>: as malhas
    ///    <c>Malhas/Loot_&lt;modelo&gt;</c>; os da etapa 4, o lampião, o molotov e o kit da fogueira, ficam para ela);
    ///  - os ícones e as fotos dos 4 itens (<c>Art/UI/Icones</c>);
    ///  - o <c>Resources/ProtecaoDeSucata.prefab</c> (o <see cref="PecasDaProtecao"/>), que o jogo usa no Play.
    /// O relatório no Console mede cada peça (a caixa no espaço do osso, para conferir com o Protecao_Sucata.json).
    /// </summary>
    public static class MontagemDaProtecao
    {
        internal const string FbxCorpo = "Assets/_Game/Art/Characters/Jogador/Protecao_Sucata.fbx";
        internal const string FbxItens = "Assets/_Game/Art/Characters/Jogador/Loot_Etapa34.fbx";
        const string Prefab = "Assets/_Game/Resources/ProtecaoDeSucata.prefab";

        [MenuItem("Zombie City Cleaner/Habilidades/Montar a proteção de sucata")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a proteção de sucata.");
                return;
            }
            if (!File.Exists(FbxCorpo) || !File.Exists(FbxItens))
                throw new FileNotFoundException("Faltam os modelos da proteção (o Protecao_Sucata.fbx e o Loot_Etapa34.fbx).", FbxCorpo);
            Importar();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var go = new GameObject("ProtecaoDeSucata");
            // as fotos com a qualidade Mobile (como as das melhorias: na PC, a transparência da câmera de prévia se perde)
            int qualidade = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);
            try
            {
                var d = go.AddComponent<PecasDaProtecao>();
                d.material = material;
                // as malhas chegam com o Z para cima do Blender (11/11/2026: a caixa do json é a da malha com (x, -z, y)); o giro
                // de 90° no X as leva para o espaço do osso (o relatório mostra a caixa já girada, para conferir com o json)
                d.giro = Quaternion.Euler(90f, 0f, 0f);
                var relatorio = new List<string>();
                var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxCorpo).OfType<Mesh>().ToList();
                d.pecas = new Mesh[PecasDaProtecao.Pecas.Length];
                for (int k = 0; k < d.pecas.Length; k++)
                {
                    string objeto = PecasDaProtecao.Pecas[k].objeto;
                    var m = malhas.FirstOrDefault(x => x.name == objeto);
                    if (m == null)
                    {
                        Debug.LogWarning($"[Proteção] Falta a {objeto} no {FbxCorpo}.");
                        continue;
                    }
                    d.pecas[k] = m;
                    var min = Vector3.one * 99f;
                    var max = -min;
                    foreach (var v in m.vertices)
                    {
                        var g = d.giro * v;
                        min = Vector3.Min(min, g);
                        max = Vector3.Max(max, g);
                    }
                    relatorio.Add($"{objeto}: {m.vertexCount} vértices, a caixa no osso {min.ToString("F3")} a {max.ToString("F3")}, as formas [{string.Join(", ", Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName))}]");
                }
                d.malhasDosItens = new Mesh[Protecao.Quantos];
                d.icones = new Texture2D[Protecao.Quantos];
                d.fotos = new Texture2D[Protecao.Quantos];
                for (int k = 0; k < Protecao.Quantos; k++)
                {
                    int i = Itens.IndiceDoLoot(Protecao.ItemDo((LugarDaProtecao)k));
                    string modelo = Itens.ModeloDoLoot(i);
                    MontagemDasArmas.Juntar(FbxItens, $"Item_{modelo}_", MontagemDasArmas.MalhaDoLoot(i));
                    AssetDatabase.SaveAssets();
                    var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDoLoot(i));
                    d.malhasDosItens[k] = malha;
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.IconeDoLoot(i), 128, 128, false, true);
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.FotoDoLoot(i), 600, 500, false, false);
                    relatorio.Add($"Item {modelo}: {malha.bounds.size.ToString("F3")}");
                }
                AssetDatabase.Refresh();
                for (int k = 0; k < Protecao.Quantos; k++)
                {
                    int i = Itens.IndiceDoLoot(Protecao.ItemDo((LugarDaProtecao)k));
                    d.icones[k] = Imagem(MontagemDasArmas.IconeDoLoot(i));
                    d.fotos[k] = Imagem(MontagemDasArmas.FotoDoLoot(i));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
                PrefabUtility.SaveAsPrefabAsset(go, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Proteção] {Prefab}:\n{string.Join("\n", relatorio)}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                QualitySettings.SetQualityLevel(qualidade, false);
                AssetDatabase.SaveAssets();   // (o ProjectSettings volta com a qualidade de antes)
            }
        }

        static void Importar()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxCorpo);
            if (imp != null)
            {
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
                imp.isReadable = true;
                imp.importBlendShapes = true;
                imp.importNormals = ModelImporterNormals.Import;
                imp.importBlendShapeNormals = ModelImporterNormals.None;
                imp.meshCompression = ModelImporterMeshCompression.Off;
                imp.optimizeMeshVertices = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
            }
            MontagemDasArmas.ImportarAvulsas(FbxItens);
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
