using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Grava as tomadas da cena de vídeo com o Unity Recorder. Com "gravar" ligado no <see cref="DiretorDoVideo"/>,
    /// quando a tomada começa, começa um MP4 vertical (1080 × 1920, 60 quadros por segundo constantes, com o som do jogo)
    /// em <c>Recordings/&lt;vídeo&gt;/&lt;tomada&gt;_&lt;data&gt;.mp4</c>; quando ela acaba, o arquivo fecha e o Play para.
    /// O Recorder grava com o tempo fixo: mesmo se o jogo ficar lento (mil zumbis na tela), o vídeo sai liso.
    /// </summary>
    [InitializeOnLoad]
    public static class GravadorDoVideo
    {
        public const int Largura = 1080, Altura = 1920, QuadrosPorSegundo = 60;

        static RecorderController _gravador;
        static string _arquivo;
        static bool _tentou;

        static GravadorDoVideo()
        {
            EditorApplication.update += Atualizar;
            EditorApplication.playModeStateChanged += AoMudarPlay;
        }

        static void Atualizar()
        {
            if (!EditorApplication.isPlaying) return;
            var diretor = DiretorDoVideo.Instancia;
            if (diretor == null || !diretor.gravar) return;
            if (!_tentou && diretor.Rodando) Comecar(diretor);
            else if (diretor.Terminou && _tentou)
            {
                Parar();
                EditorApplication.isPlaying = false;
            }
        }

        static void AoMudarPlay(PlayModeStateChange mudanca)
        {
            // parou o Play no meio da tomada: o arquivo fecha com o que deu para gravar
            if (mudanca == PlayModeStateChange.ExitingPlayMode) Parar();
            if (mudanca == PlayModeStateChange.EnteredEditMode) _tentou = false;
        }

        static void Comecar(DiretorDoVideo diretor)
        {
            _tentou = true;
            string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", diretor.video));
            Directory.CreateDirectory(pasta);
            var filme = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            filme.name = diretor.NomeDaTomada;
            filme.Enabled = true;
            filme.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            filme.CaptureAlpha = false;
            filme.CaptureAudio = true;
            // em pé (o Reels) ou deitado (a tela do jogo)
            filme.ImageInputSettings = diretor.deitado
                ? new GameViewInputSettings { OutputWidth = Altura, OutputHeight = Largura }
                : new GameViewInputSettings { OutputWidth = Largura, OutputHeight = Altura };
            filme.OutputFile = Path.Combine(pasta, $"{diretor.NomeDaTomada}_{DateTime.Now:yyyyMMdd_HHmmss}");

            var controle = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controle.AddRecorderSettings(filme);
            controle.SetRecordModeToManual();
            controle.FrameRatePlayback = FrameRatePlayback.Constant;
            controle.FrameRate = QuadrosPorSegundo;
            controle.CapFrameRate = true;

            _gravador = new RecorderController(controle);
            _gravador.PrepareRecording();
            _gravador.StartRecording();
            _arquivo = filme.OutputFile + ".mp4";
            Debug.Log($"[vídeo] gravando a tomada {diretor.NomeDaTomada} em {_arquivo}");
        }

        static void Parar()
        {
            if (_gravador == null) return;
            if (_gravador.IsRecording()) _gravador.StopRecording();
            _gravador = null;
            Debug.Log($"[vídeo] gravado: {_arquivo}");
        }
    }
}
