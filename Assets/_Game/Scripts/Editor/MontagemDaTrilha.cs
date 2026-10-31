using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Music;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A trilha sonora: importa os .ogg de Audio/Trilha (estéreo, Vorbis, comprimidos na memória: são muitos minutos de
    /// música, descomprimidos ocupariam centenas de MB) e refaz o Audio/Resources/TrilhaDoJogo.asset pelo nome de cada
    /// arquivo (neblina_&lt;tipo&gt;_N, noite_&lt;tipo&gt;_N, perigo_&lt;plano&gt;N_c1..c3, perigo_entrada_N, perigo_saida_N,
    /// perigo_pedra_N, vento_dia_N, vento_noite_N, momentos/amanhecer e momentos/morte) e pelo trilha.json (o
    /// comprimento dos blocos). Os arquivos saem do Ferramentas/trilha (blocos.py, compor.py e exportar.py).
    /// </summary>
    public static class MontagemDaTrilha
    {
        const string Pasta = "Assets/_Game/Audio/Trilha";
        const string Asset = "Assets/_Game/Audio/Resources/TrilhaDoJogo.asset";
        const string PastaDoAmbiente = "Assets/_Game/Audio/Ambiente";

        [System.Serializable]
        class Info
        {
            [System.Serializable]
            public class Familia { public float bpm, comprimento; }
            public Familia neblina, noite, perigo;
            public float encaixeDaEntrada, sobreposicaoDoVento;
            public TrilhaDoJogo.Rajadas[] rajadasDeDia, rajadasDeNoite;
        }

        [MenuItem("Zombie City Cleaner/Trilha/Montar a trilha")]
        public static void Montar()
        {
            var info = JsonUtility.FromJson<Info>(File.ReadAllText($"{Pasta}/trilha.json"));
            var arquivos = Directory.GetFiles(Pasta, "*.ogg", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var caminho in arquivos)
                {
                    var imp = (AudioImporter)AssetImporter.GetAtPath(caminho);
                    if (imp == null) continue;
                    imp.forceToMono = false;
                    imp.loadInBackground = false;
                    var s = imp.defaultSampleSettings;
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    s.preloadAudioData = true;
                    imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            // o ambiente: os grilos em estéreo (laço longo, comprimidos na memória); os eventos, mono e curtos
            var doAmbiente = Directory.Exists(PastaDoAmbiente)
                ? Directory.GetFiles(PastaDoAmbiente, "*.wav").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray() : new string[0];
            foreach (var caminho in doAmbiente)
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(caminho);
                if (imp == null) continue;
                bool laco = Path.GetFileName(caminho).StartsWith("Grilos");
                imp.forceToMono = !laco;
                imp.loadInBackground = false;
                var s = imp.defaultSampleSettings;
                s.loadType = laco ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                s.preloadAudioData = true;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            AudioClip[] DoAmbiente(string prefixo) => doAmbiente.Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefixo))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();

            string Nome(string p) => Path.GetFileNameWithoutExtension(p);
            AudioClip Clipe(string p) => AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            AudioClip[] Varios(string prefixo) => arquivos.Where(p => Nome(p).StartsWith(prefixo)).Select(Clipe).Where(c => c != null).ToArray();
            TrilhaDoJogo.Familia Familia(string nome, Info.Familia i) => new TrilhaDoJogo.Familia
            {
                bpm = i.bpm,
                comprimento = i.comprimento,
                blocos = arquivos.Where(p => Nome(p).StartsWith(nome + "_")).Select(p => new TrilhaDoJogo.Bloco
                {
                    tipo = Nome(p).Substring(nome.Length + 1, Nome(p).LastIndexOf('_') - nome.Length - 1),
                    clipe = Clipe(p),
                }).ToArray(),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Asset));
            var trilha = AssetDatabase.LoadAssetAtPath<TrilhaDoJogo>(Asset);
            bool novo = trilha == null;
            if (novo) trilha = ScriptableObject.CreateInstance<TrilhaDoJogo>();
            trilha.neblina = Familia("neblina", info.neblina);
            trilha.noite = Familia("noite", info.noite);
            trilha.bpmDoPerigo = info.perigo.bpm;
            trilha.comprimentoDoPerigo = info.perigo.comprimento;
            // perigo_A0_c1, perigo_A0_c2, perigo_A0_c3: um bloco, três camadas
            trilha.perigo = arquivos.Where(p => Nome(p).StartsWith("perigo_") && Nome(p).EndsWith("_c1")).Select(p =>
            {
                string bloco = Nome(p).Substring(0, Nome(p).Length - 3);
                return new TrilhaDoJogo.BlocoDoPerigo
                {
                    plano = bloco.Substring("perigo_".Length, 1),
                    camadas = new[] { 1, 2, 3 }.Select(c => Clipe($"{Path.GetDirectoryName(p).Replace('\\', '/')}/{bloco}_c{c}.ogg")).ToArray(),
                };
            }).ToArray();
            trilha.entradas = Varios("perigo_entrada_");
            trilha.encaixeDaEntrada = info.encaixeDaEntrada;
            trilha.saidas = Varios("perigo_saida_");
            trilha.saidasPelaPedra = Varios("perigo_pedra_");
            trilha.ventoDeDia = Varios("vento_dia_");
            trilha.ventoDeNoite = Varios("vento_noite_");
            trilha.sobreposicaoDoVento = info.sobreposicaoDoVento;
            trilha.rajadasDoVentoDeDia = info.rajadasDeDia;
            trilha.rajadasDoVentoDeNoite = info.rajadasDeNoite;
            trilha.grilos = DoAmbiente("Grilos_");
            trilha.latidos = DoAmbiente("Latido_");
            trilha.uivos = DoAmbiente("Uivo_");
            trilha.janelas = DoAmbiente("Janela_");
            trilha.portas = DoAmbiente("Porta_");
            trilha.placas = DoAmbiente("Placa_");
            trilha.latas = DoAmbiente("Lata_");
            trilha.madeira = DoAmbiente("Madeira_");
            trilha.corvos = DoAmbiente("Corvos_");
            trilha.amanhecer = Clipe($"{Pasta}/momentos/amanhecer.ogg");
            trilha.morte = Clipe($"{Pasta}/momentos/morte.ogg");
            trilha.menu = Clipe($"{Pasta}/momentos/menu.ogg");
            if (novo) AssetDatabase.CreateAsset(trilha, Asset);
            else EditorUtility.SetDirty(trilha);
            AssetDatabase.SaveAssets();
            Debug.Log($"Trilha: neblina {trilha.neblina.blocos.Length} blocos, noite {trilha.noite.blocos.Length}, perigo {trilha.perigo.Length} (x3 camadas), " +
                      $"entradas {trilha.entradas.Length}, saídas {trilha.saidas.Length} + {trilha.saidasPelaPedra.Length} pela pedra, " +
                      $"vento {trilha.ventoDeDia.Length} + {trilha.ventoDeNoite.Length} (rajadas {trilha.rajadasDoVentoDeDia?.Length ?? 0}), " +
                      $"ambiente {trilha.grilos.Length} grilos, {trilha.latidos.Length + trilha.uivos.Length} cachorros, {trilha.janelas.Length + trilha.portas.Length + trilha.placas.Length + trilha.latas.Length} do vento, " +
                      $"{trilha.madeira.Length} madeira, {trilha.corvos.Length} corvos, amanhecer {(trilha.amanhecer != null ? "sim" : "não")}, morte {(trilha.morte != null ? "sim" : "não")}");
        }
    }
}
