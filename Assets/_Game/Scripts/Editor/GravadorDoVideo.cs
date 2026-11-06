using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Grava os vídeos com o Unity Recorder, de dois jeitos:
    /// <list type="bullet">
    /// <item>as tomadas da cena de vídeo: com "gravar" ligado no <see cref="DiretorDoVideo"/>, quando a tomada começa, começa
    /// um MP4 em <c>Recordings/&lt;vídeo&gt;/&lt;tomada&gt;_&lt;data&gt;.mp4</c>; quando ela acaba, o arquivo fecha e o Play para;</item>
    /// <item>jogando, na partida de sempre (<see cref="GravacaoNoMundo"/>: o F8 ou o painel VÍDEO do F1): começa e para pelo
    /// pedido do jogo, em <c>Recordings/Mundo/Mundo_&lt;data&gt;.mp4</c>, e o Play continua. Enquanto grava, a aba da Game
    /// view diz "● GRAVANDO" (fora da imagem); no fim, a Game view volta ao tamanho de antes.</item>
    /// </list>
    /// Em pé (1080 × 1920) ou deitado (1920 × 1080), com o som do jogo. O Recorder grava com o tempo fixo: mesmo se o jogo
    /// ficar lento (mil zumbis na tela), o vídeo sai liso; enquanto grava, o jogo anda no tempo do vídeo.
    /// </summary>
    [InitializeOnLoad]
    public static class GravadorDoVideo
    {
        public const int Largura = 1080, Altura = 1920, QuadrosPorSegundo = 60;

        static RecorderController _gravador;
        static string _arquivo;
        static bool _tentou;
        // a gravação de agora é a do mundo (pelo pedido do jogo); o tamanho e o título da Game view de antes dela
        static bool _doMundo;
        static int _tamanhoAntes = -1;
        static GUIContent _tituloAntes;

        static GravadorDoVideo()
        {
            EditorApplication.update += Atualizar;
            EditorApplication.playModeStateChanged += AoMudarPlay;
        }

        static void Atualizar()
        {
            if (!EditorApplication.isPlaying) return;
            // jogando no mundo: o jogo pede (o F8) e desiste
            if (GravacaoNoMundo.Pedido && _gravador == null) ComecarNoMundo();
            else if (!GravacaoNoMundo.Pedido && _doMundo) Parar();
            if (_doMundo) return;

            var diretor = DiretorDoVideo.Instancia;
            if (diretor == null || !diretor.gravar) return;
            if (!_tentou && diretor.Rodando)
            {
                _tentou = true;
                Comecar(Path.Combine("Recordings", diretor.video), diretor.NomeDaTomada, diretor.deitado, QuadrosPorSegundo);
            }
            else if (diretor.Terminou && _tentou)
            {
                Parar();
                EditorApplication.isPlaying = false;
            }
        }

        static void AoMudarPlay(PlayModeStateChange mudanca)
        {
            // parou o Play no meio da tomada (ou da gravação no mundo): o arquivo fecha com o que deu para gravar
            if (mudanca == PlayModeStateChange.ExitingPlayMode) Parar();
            if (mudanca == PlayModeStateChange.EnteredEditMode) _tentou = false;
        }

        static void ComecarNoMundo()
        {
            // a cena de vídeo grava pelo diretor
            var diretor = DiretorDoVideo.Instancia;
            if (diretor != null && diretor.gravar)
            {
                GravacaoNoMundo.Pedido = false;
                return;
            }
            GuardarAGameView();
            Comecar(Path.Combine("Recordings", "Mundo"), "Mundo", !GravacaoNoMundo.EmPe, GravacaoNoMundo.Quadros);
            _doMundo = true;
            GravacaoNoMundo.Gravando = true;
            GravacaoNoMundo.Desde = Time.unscaledTime;
            GravacaoNoMundo.Arquivo = _arquivo;
            TituloDaGameView(true);
        }

        static void Comecar(string pastaNoProjeto, string nome, bool deitado, int quadros)
        {
            string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", pastaNoProjeto));
            Directory.CreateDirectory(pasta);
            var filme = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            filme.name = nome;
            filme.Enabled = true;
            filme.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            filme.CaptureAlpha = false;
            filme.CaptureAudio = true;
            // em pé (o Reels) ou deitado (a tela do jogo)
            filme.ImageInputSettings = deitado
                ? new GameViewInputSettings { OutputWidth = Altura, OutputHeight = Largura }
                : new GameViewInputSettings { OutputWidth = Largura, OutputHeight = Altura };
            filme.OutputFile = Path.Combine(pasta, $"{nome}_{DateTime.Now:yyyyMMdd_HHmmss}");

            var controle = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controle.AddRecorderSettings(filme);
            controle.SetRecordModeToManual();
            controle.FrameRatePlayback = FrameRatePlayback.Constant;
            controle.FrameRate = quadros;
            controle.CapFrameRate = true;

            _gravador = new RecorderController(controle);
            _gravador.PrepareRecording();
            _gravador.StartRecording();
            _arquivo = filme.OutputFile + ".mp4";
            Debug.Log($"[vídeo] gravando {nome} em {_arquivo} ({(deitado ? "1920 × 1080" : "1080 × 1920")}, {quadros} quadros/s)");
        }

        static void Parar()
        {
            if (_gravador != null)
            {
                if (_gravador.IsRecording()) _gravador.StopRecording();
                _gravador = null;
                Debug.Log($"[vídeo] gravado: {_arquivo}");
            }
            if (!_doMundo) return;
            _doMundo = false;
            GravacaoNoMundo.Gravando = false;
            GravacaoNoMundo.Pedido = false;
            VoltarAGameView();
            TituloDaGameView(false);
        }

        // ------------------------------------------------------------------ a Game view (por reflexão: a janela é interna)

        static Type TipoDaGameView => typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        static EditorWindow GameView()
        {
            var tipo = TipoDaGameView;
            if (tipo == null) return null;
            var abertas = Resources.FindObjectsOfTypeAll(tipo);
            return abertas.Length > 0 ? (EditorWindow)abertas[0] : null;
        }

        const BindingFlags Membros = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // o Recorder põe a Game view no tamanho do vídeo e deixa assim: guarda o de antes para voltar no fim
        static void GuardarAGameView()
        {
            _tamanhoAntes = -1;
            try
            {
                var janela = GameView();
                var indice = TipoDaGameView?.GetProperty("selectedSizeIndex", Membros);
                if (janela != null && indice != null) _tamanhoAntes = (int)indice.GetValue(janela);
            }
            catch (Exception) { _tamanhoAntes = -1; }
        }

        static void VoltarAGameView()
        {
            if (_tamanhoAntes < 0) return;
            try
            {
                var janela = GameView();
                if (janela == null) return;
                // o mesmo que escolher o tamanho no menu da janela (ele acerta o zoom); sem ele, só o índice
                var escolher = TipoDaGameView.GetMethod("SizeSelectionCallback", Membros);
                if (escolher != null) escolher.Invoke(janela, new object[] { _tamanhoAntes, null });
                else TipoDaGameView.GetProperty("selectedSizeIndex", Membros)?.SetValue(janela, _tamanhoAntes);
                janela.Repaint();
            }
            catch (Exception e) { Debug.LogWarning($"[vídeo] a Game view não voltou ao tamanho de antes: {e.Message}"); }
            finally { _tamanhoAntes = -1; }
        }

        static void TituloDaGameView(bool gravando)
        {
            var janela = GameView();
            if (janela == null) return;
            if (gravando)
            {
                _tituloAntes = new GUIContent(janela.titleContent);
                janela.titleContent = new GUIContent("● GRAVANDO", janela.titleContent.image);
            }
            else if (_tituloAntes != null)
            {
                janela.titleContent = _tituloAntes;
                _tituloAntes = null;
            }
            janela.Repaint();
        }
    }
}
