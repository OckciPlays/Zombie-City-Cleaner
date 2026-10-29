using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O alarme dos carros: importa as sirenes de Audio/Carro (Alarme_*.wav, do Ferramentas/alarmes.py: mono, Vorbis,
    /// comprimidas na memória) e refaz o Audio/Resources/SonsDoAlarme.asset com elas e o material do pisca-alerta.
    /// </summary>
    public static class MontagemDoAlarme
    {
        const string Pasta = "Assets/_Game/Audio/Carro";
        const string Asset = "Assets/_Game/Audio/Resources/SonsDoAlarme.asset";
        const string Luz = "Assets/_Game/Art/Materials/Blocos/PiscaAlerta.mat";

        [MenuItem("Zombie City Cleaner/Veículos/Montar o alarme dos carros")]
        public static void Montar()
        {
            var arquivos = Directory.GetFiles(Pasta, "Alarme_*.wav").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            foreach (var caminho in arquivos)
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(caminho);
                if (imp == null) continue;
                imp.forceToMono = true;
                imp.loadInBackground = false;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                s.preloadAudioData = true;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Asset));
            var sons = AssetDatabase.LoadAssetAtPath<SonsDoAlarme>(Asset);
            bool novo = sons == null;
            if (novo) sons = ScriptableObject.CreateInstance<SonsDoAlarme>();
            sons.sirenes = arquivos.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            sons.luz = AssetDatabase.LoadAssetAtPath<Material>(Luz);
            if (novo) AssetDatabase.CreateAsset(sons, Asset);
            else EditorUtility.SetDirty(sons);
            AssetDatabase.SaveAssets();
            Debug.Log($"Alarme dos carros: {sons.sirenes.Length} sirenes, luz {(sons.luz != null ? sons.luz.name : "FALTANDO")}");
        }
    }
}
