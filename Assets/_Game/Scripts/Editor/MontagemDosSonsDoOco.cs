using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os sons do Oco (a etapa 7): importa os .wav de Audio/Oco (mono, Vorbis, descomprimidos ao carregar: são curtos) e
    /// refaz o Audio/Resources/SonsDoOco.asset com eles, pelo começo do nome (Oco_Voz_*, Oco_Golpe_*, Oco_Chamado_*,
    /// Oco_Resposta_*, Oco_Agonia_*, Oco_Tiro_*, Oco_Petrificando, Oco_Petrificou, Oco_Soltando). Os .wav saem do
    /// Ferramentas/sons_do_oco.py (python sons_do_oco.py voz_original.wav pasta), a partir da voz do SomZombie1.
    /// </summary>
    public static class MontagemDosSonsDoOco
    {
        const string Pasta = "Assets/_Game/Audio/Oco";
        const string Asset = "Assets/_Game/Audio/Resources/SonsDoOco.asset";

        [MenuItem("Zombie City Cleaner/Zumbis/Montar os sons do Oco")]
        public static void Montar()
        {
            var arquivos = Directory.GetFiles(Pasta, "*.wav").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            foreach (var caminho in arquivos)
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(caminho);
                if (imp == null) continue;
                imp.forceToMono = true;
                imp.loadInBackground = false;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = true;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            AudioClip[] Varios(string prefixo) => arquivos.Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefixo))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            AudioClip Um(string nome) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Pasta}/{nome}.wav");

            Directory.CreateDirectory(Path.GetDirectoryName(Asset));
            var sons = AssetDatabase.LoadAssetAtPath<SonsDoOco>(Asset);
            bool novo = sons == null;
            if (novo) sons = ScriptableObject.CreateInstance<SonsDoOco>();
            sons.voz = Varios("Oco_Voz_");
            sons.golpe = Varios("Oco_Golpe_");
            sons.chamado = Varios("Oco_Chamado_");
            sons.resposta = Varios("Oco_Resposta_");
            sons.agonia = Varios("Oco_Agonia_");
            sons.tiro = Varios("Oco_Tiro_");
            sons.petrificando = Um("Oco_Petrificando");
            sons.petrificou = Um("Oco_Petrificou");
            sons.soltando = Um("Oco_Soltando");
            if (novo) AssetDatabase.CreateAsset(sons, Asset);
            else EditorUtility.SetDirty(sons);
            AssetDatabase.SaveAssets();
            Debug.Log($"Sons do Oco: {sons.voz.Length} vozes, {sons.golpe.Length} golpes, {sons.chamado.Length} chamados, {sons.resposta.Length} respostas, " +
                      $"{sons.agonia.Length} agonias, {sons.tiro.Length} tiros, pedra {(sons.petrificando != null ? "sim" : "não")}");
        }
    }
}
