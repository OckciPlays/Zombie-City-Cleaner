using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os ícones da aba HABILIDADES (10/11/2026, como no Dungeons &amp; Developers): os PNG de 256 x 256 do
    /// <c>Blender/icones_habilidades.py</c> (<c>Hab_&lt;id&gt;.png</c>; o id da lista NOS é a letra do ramo e o lugar do nó
    /// nele: g1, a10, q5...) entram em <see cref="Pasta"/> como <c>Hab_&lt;Habilidade&gt;.png</c> (o nome do enum), com a
    /// cópia sem cor ao lado (<c>Hab_&lt;Habilidade&gt;_Cinza.png</c>, a dos nós trancados e "em breve": o OnGUI não
    /// tira a cor de uma textura). A tela carrega pelo Resources (<c>TelaDoInventario.IconeDaHabilidade</c>): nenhuma cena
    /// nem prefab muda. A importação: <see cref="AjustarAImportacao"/>.
    /// Para refazer: rode o Blender com <c>--saida</c> numa pasta e o menu Zombie City Cleaner/Habilidades/Importar os
    /// ícones (ou <see cref="Importar"/> com a pasta).
    /// </summary>
    public static class MontagemDosIconesDasHabilidades
    {
        public const string Pasta = "Assets/_Game/Art/UI/Habilidades/Resources";

        // a letra de cada ramo no id do Blender (na ordem de RamoDaHabilidade)
        static readonly string[] Letras = { "g", "a", "s", "l", "f", "h", "n", "u", "p", "k", "t", "o", "q", "v" };   // (o "v": o Atleta, 12/11/2026)

        /// <summary>O id do nó no Blender (g1, a10, q5...): a letra do ramo e o lugar dele no ramo (na ordem do enum).</summary>
        public static string Id(Habilidade h)
        {
            var ramo = Habilidades.RamoDe(h);
            int n = 1;
            for (int i = 0; i < (int)h; i++)
                if (Habilidades.RamoDe((Habilidade)i) == ramo) n++;
            return Letras[(int)ramo] + n;
        }

        public static string Caminho(Habilidade h, bool cinza = false) => $"{Pasta}/Hab_{h}{(cinza ? "_Cinza" : "")}.png";

        [MenuItem("Zombie City Cleaner/Habilidades/Importar os ícones (da pasta do Blender)")]
        static void ImportarPeloMenu()
        {
            var pasta = EditorUtility.OpenFolderPanel("A pasta dos Hab_<id>.png (a --saida do Blender/icones_habilidades.py)", "", "");
            if (!string.IsNullOrEmpty(pasta)) Debug.Log($"Ícones das habilidades: {Importar(pasta)} de {Habilidades.Quantas}.");
        }

        /// <summary>Copia os Hab_&lt;id&gt;.png da pasta do Blender com o nome do enum, faz os cinza e importa. Devolve quantos.</summary>
        public static int Importar(string pastaDoBlender)
        {
            Directory.CreateDirectory(Pasta);
            int feitos = 0;
            for (int i = 0; i < Habilidades.Quantas; i++)
            {
                var h = (Habilidade)i;
                var origem = Path.Combine(pastaDoBlender, $"Hab_{Id(h)}.png");
                if (!File.Exists(origem))
                {
                    Debug.LogWarning($"Falta o ícone {origem} ({h}).");
                    continue;
                }
                var png = File.ReadAllBytes(origem);
                File.WriteAllBytes(Caminho(h), png);
                File.WriteAllBytes(Caminho(h, true), Cinza(png));
                feitos++;
            }
            AssetDatabase.Refresh();
            AjustarAImportacao();
            return feitos;
        }

        [MenuItem("Zombie City Cleaner/Habilidades/Refazer os ícones cinza")]
        public static void RefazerOsCinza()
        {
            for (int i = 0; i < Habilidades.Quantas; i++)
            {
                var h = (Habilidade)i;
                if (File.Exists(Caminho(h))) File.WriteAllBytes(Caminho(h, true), Cinza(File.ReadAllBytes(Caminho(h))));
            }
            AssetDatabase.Refresh();
            AjustarAImportacao();
        }

        /// <summary>
        /// A importação dos ícones (fica nos .meta): 256 x 256 com mipmap (o filtro Kaiser e o viés -0,5 deixam nítido de
        /// 75 px, a 1080p, a 150 px, a 4K), sem o limite de mipmap da qualidade, e BC7 no PC (as cores e o fundo escuro
        /// sem as faixas do DXT1). Sem AssetPostprocessor de propósito: um novo reimporta todas as texturas do projeto.
        /// </summary>
        public static int AjustarAImportacao()
        {
            int mudados = 0;
            AssetDatabase.StartAssetEditing();   // as reimportações juntas, no fim
            try
            {
                for (int i = 0; i < Habilidades.Quantas * 2; i++)
                {
                    var caminho = Caminho((Habilidade)(i / 2), i % 2 == 1);
                    if (!(AssetImporter.GetAtPath(caminho) is TextureImporter imp)) continue;
                    var pc = imp.GetPlatformTextureSettings("Standalone");
                    if (imp.mipmapEnabled && imp.mipmapFilter == TextureImporterMipFilter.KaiserFilter && Mathf.Approximately(imp.mipMapBias, -0.5f)
                        && imp.ignoreMipmapLimit && imp.maxTextureSize == 256 && imp.alphaSource == TextureImporterAlphaSource.None
                        && imp.wrapMode == TextureWrapMode.Clamp && pc.overridden && pc.format == TextureImporterFormat.BC7) continue;
                    imp.textureType = TextureImporterType.Default;
                    imp.textureShape = TextureImporterShape.Texture2D;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.None;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.isReadable = false;
                    imp.mipmapEnabled = true;
                    imp.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    imp.mipMapBias = -0.5f;
                    imp.ignoreMipmapLimit = true;
                    imp.streamingMipmaps = false;
                    imp.filterMode = FilterMode.Trilinear;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.maxTextureSize = 256;
                    imp.textureCompression = TextureImporterCompression.CompressedHQ;
                    pc.overridden = true;
                    pc.maxTextureSize = 256;
                    pc.format = TextureImporterFormat.BC7;
                    pc.textureCompression = TextureImporterCompression.CompressedHQ;
                    imp.SetPlatformTextureSettings(pc);
                    imp.SaveAndReimport();
                    mudados++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return mudados;
        }

        // a cópia sem cor: a luminância (o escurecido fica na tela, que escolhe quanto)
        static byte[] Cinza(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(png);
            var px = t.GetPixels32();
            for (int k = 0; k < px.Length; k++)
            {
                var c = px[k];
                byte l = (byte)Mathf.Clamp(Mathf.RoundToInt(0.299f * c.r + 0.587f * c.g + 0.114f * c.b), 0, 255);
                px[k] = new Color32(l, l, l, 255);
            }
            t.SetPixels32(px);
            var saida = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            return saida;
        }
    }
}
