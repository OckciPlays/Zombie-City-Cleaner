using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os símbolos dos estados do HUD (12/11/2026): os PNG de 256 x 256 com alfa do <c>Blender/icones_estados.py</c>
    /// (<c>Estado_&lt;nome do enum EstadoDoJogador&gt;.png</c>: o rosto do personagem num disco escuro) entram em
    /// <see cref="Pasta"/>, de onde o <see cref="EstadosDoJogador.Icone"/> carrega pelo Resources (nenhuma cena nem prefab
    /// muda). A importação: com mipmap (nítido de 40 a 80 px), o alfa do PNG e BC7 no PC.
    /// Para refazer: rode o Blender com <c>--saida</c> numa pasta e o menu Zombie City Cleaner/Estados/Importar os ícones
    /// (ou <see cref="Importar"/> com a pasta).
    /// </summary>
    public static class MontagemDosIconesDosEstados
    {
        public const string Pasta = "Assets/_Game/Art/UI/Estados/Resources/Estados";

        public static string Caminho(EstadoDoJogador e) => $"{Pasta}/Estado_{e}.png";

        [MenuItem("Zombie City Cleaner/Estados/Importar os ícones (da pasta do Blender)")]
        static void ImportarPeloMenu()
        {
            var pasta = EditorUtility.OpenFolderPanel("A pasta dos Estado_<nome>.png (a --saida do Blender/icones_estados.py)", "", "");
            if (!string.IsNullOrEmpty(pasta)) Debug.Log($"Ícones dos estados: {Importar(pasta)} de {System.Enum.GetValues(typeof(EstadoDoJogador)).Length}.");
        }

        /// <summary>Copia os Estado_&lt;nome&gt;.png da pasta do Blender e importa. Devolve quantos.</summary>
        public static int Importar(string pastaDoBlender)
        {
            Directory.CreateDirectory(Pasta);
            int feitos = 0;
            foreach (EstadoDoJogador e in System.Enum.GetValues(typeof(EstadoDoJogador)))
            {
                var origem = Path.Combine(pastaDoBlender, $"Estado_{e}.png");
                if (!File.Exists(origem))
                {
                    Debug.LogWarning($"Falta o ícone {origem}.");
                    continue;
                }
                File.WriteAllBytes(Caminho(e), File.ReadAllBytes(origem));
                feitos++;
            }
            AssetDatabase.Refresh();
            AjustarAImportacao();
            return feitos;
        }

        /// <summary>A importação (fica nos .meta): 256 x 256 com mipmap (Kaiser, viés -0,5), o alfa do PNG e BC7 no PC.</summary>
        public static int AjustarAImportacao()
        {
            int mudados = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (EstadoDoJogador e in System.Enum.GetValues(typeof(EstadoDoJogador)))
                {
                    if (!(AssetImporter.GetAtPath(Caminho(e)) is TextureImporter imp)) continue;
                    imp.textureType = TextureImporterType.Default;
                    imp.textureShape = TextureImporterShape.Texture2D;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.FromInput;
                    imp.alphaIsTransparency = true;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.isReadable = false;
                    imp.mipmapEnabled = true;
                    imp.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    imp.mipMapBias = -0.5f;
                    imp.ignoreMipmapLimit = true;
                    imp.streamingMipmaps = false;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.filterMode = FilterMode.Bilinear;
                    imp.maxTextureSize = 256;
                    var pc = imp.GetPlatformTextureSettings("Standalone");
                    pc.overridden = true;
                    pc.maxTextureSize = 256;
                    pc.format = TextureImporterFormat.BC7;
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
    }
}
