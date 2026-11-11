using System;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os shaders aquecidos no carregamento (10/11/2026, a pedido: "quando entro no bunker, os primeiros segundos o fps cai
    /// para 7", só no executável). No jogo compilado, a placa monta cada shader na primeira vez que ele aparece (na build do
    /// Pedro de 08/11/2026, 458 ms ao todo, até 204 ms num só); no Editor, isso não acontece. Pela
    /// <see cref="GraphicsStateCollection"/> da Unity:
    /// <list type="bullet">
    /// <item><b>gravar</b>: com <c>-gravarshaders</c> na linha de comando (o <c>Gravar_Shaders.bat</c> da pasta da build), o
    /// executável anota, desde o começo, cada shader e cada estado gráfico que desenhou; ao sair do jogo (o ESC &gt; Sair ou
    /// o fechar da janela, pelo <c>Application.quitting</c>), grava tudo em <c>gravacao_shaders.graphicsstate</c>, ao lado
    /// do executável, e escreve no <c>Player.log</c> onde gravou. Gravando, ele não aquece nada;</item>
    /// <item><b>aquecer</b>: se houver uma gravação, em <c>Assets/StreamingAssets/</c> (a pasta especial da Unity, que vai
    /// inteira para o jogo) ou ao lado do executável, o jogo monta esses shaders na tela de carregamento do mundo
    /// (<see cref="MundoDaPartida"/>) e na tela preta de carregar a rua do Dia 0, aos poucos (<see cref="Andar"/>, uma
    /// leva por quadro), com "Preparando os gráficos (N%)" se ela for a última coisa a esperar. Uma vez por execução; até
    /// 60 s, por segurança. Sem gravação, no Editor ou sem placa (o <c>-nographics</c> dos testes), nada muda.</item>
    /// </list>
    /// No Direct3D 11 (o primeiro da lista do Player Settings, o que a build do Pedro usa), a Unity aquece pelo caminho antigo
    /// (o mesmo do <c>ShaderVariantCollection.WarmUp</c>, um desenho escondido de cada variante): cada leva roda nesta hora e
    /// o tamanho dela se ajusta para o quadro ficar perto de 30 ms. No Direct3D 12 e no Vulkan, as levas vão para os jobs.
    /// </summary>
    public static class AquecimentoDosShaders
    {
        /// <summary>O argumento da linha de comando que liga a gravação.</summary>
        public const string Argumento = "-gravarshaders";
        /// <summary>O nome do arquivo da gravação (ao lado do executável e no StreamingAssets).</summary>
        public const string Arquivo = "gravacao_shaders.graphicsstate";
        const float LimiteSegundos = 60f;
        const float QuadroAlvoMs = 30f;
        const int LevaMaxima = 64;
        // as levas seguidas sem nenhum shader novo pronto até desistir (o caminho antigo não diz quando acaba)
        const int LevasParadasMaximas = 12;

        static GraphicsStateCollection _gravacao, _aquecer;
        static bool _carregou, _pronto, _temJob, _porEstados;
        static JobHandle _job;
        static float _desde = -1f, _comecoDaLeva;
        // _total: os estados (pelos jobs) ou as variantes (pelo caminho antigo); _pedidos: quantos já foram pedidos
        static int _leva = 1, _total, _pedidos, _ultimaContagem = -1, _paradas;

        /// <summary>O executável está gravando os shaders (o <c>-gravarshaders</c>).</summary>
        public static bool Gravando => _gravacao != null;

        /// <summary>Não há mais nada a aquecer (acabou, não havia gravação, deu o tempo, ou é o Editor).</summary>
        public static bool Pronto => _carregou && _pronto;

        /// <summary>De 0 a 1, o quanto do aquecimento já foi.</summary>
        public static float Progresso => _pronto ? 1f : _aquecer == null || _total <= 0 ? 0f :
            Mathf.Clamp01((_porEstados ? _aquecer.completedWarmupCount : Mathf.Max(_aquecer.completedWarmupCount, _pedidos)) / (float)_total);

        /// <summary>Onde a gravação fica no executável: a pasta dele (o <c>Application.dataPath</c> é a pasta <c>_Data</c>).</summary>
        public static string CaminhoAoLadoDoJogo => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Arquivo);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Iniciar()
        {
            // o Editor sem recarregar o domínio guarda os campos estáticos de uma vez para a outra
            _gravacao = _aquecer = null;
            _carregou = _pronto = _temJob = _porEstados = false;
            _desde = -1f;
            _leva = 1;
            _total = _pedidos = 0;
            _ultimaContagem = -1;
            _paradas = 0;
            if (Application.isEditor || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Argumento) < 0) return;
            var gravacao = new GraphicsStateCollection();
            if (!gravacao.BeginTrace())
            {
                Debug.LogWarning($"[Shaders] O {Argumento} veio, mas a Unity não começou a gravar ({SystemInfo.graphicsDeviceType}).");
                return;
            }
            _gravacao = gravacao;
            Application.quitting += AoSair;
            Debug.Log($"[Shaders] Gravando os shaders e os estados gráficos ({SystemInfo.graphicsDeviceType}, qualidade " +
                      $"{QualitySettings.names[QualitySettings.GetQualityLevel()]}): ao sair do jogo, vão para \"{CaminhoAoLadoDoJogo}\".");
        }

        static void AoSair()
        {
            Application.quitting -= AoSair;
            var gravacao = _gravacao;
            _gravacao = null;
            if (gravacao == null) return;
            gravacao.EndTrace();
            string caminho = CaminhoAoLadoDoJogo;
            bool salvou;
            try { salvou = gravacao.SaveToFile(caminho); }
            catch (Exception e)
            {
                Debug.LogException(e);
                salvou = false;
            }
            if (salvou)
                Debug.Log($"[Shaders] A gravação foi salva em \"{caminho}\": {gravacao.variantCount} variantes, " +
                          $"{gravacao.totalGraphicsStateCount} estados gráficos ({gravacao.graphicsDeviceType}, qualidade {gravacao.qualityLevelName}).");
            else
                Debug.LogError($"[Shaders] Não deu para salvar a gravação em \"{caminho}\" ({gravacao.variantCount} variantes).");
        }

        /// <summary>
        /// Uma leva do aquecimento, para chamar uma vez por quadro numa tela de carregamento. Devolve true quando não há mais
        /// nada a esperar.
        /// </summary>
        public static bool Andar()
        {
            if (!_carregou) Carregar();
            if (_pronto) return true;
            float agora = Time.realtimeSinceStartup;
            if (_temJob)
            {
                if (!_job.IsCompleted && agora - _desde < LimiteSegundos) return false;
                _job.Complete();
                _temJob = false;
                // o tamanho da próxima leva, pelo tempo desta (no Direct3D 11, ela roda aqui mesmo)
                float ms = (agora - _comecoDaLeva) * 1000f;
                if (ms < QuadroAlvoMs * 0.4f) _leva = Mathf.Min(_leva * 2, LevaMaxima);
                else if (ms > QuadroAlvoMs * 1.5f) _leva = Mathf.Max(1, _leva / 2);
            }
            int prontos = _aquecer.completedWarmupCount;
            // pelo caminho antigo, cada leva continua de onde a outra parou: pedidas todas, acabou
            if (_aquecer.isWarmedUp || prontos >= _total || (!_porEstados && _pedidos >= _total)) return Acabar("pronto");
            if (agora - _desde >= LimiteSegundos) return Acabar($"passou de {LimiteSegundos:0} s");
            if (prontos == _ultimaContagem)
            {
                if (++_paradas >= LevasParadasMaximas) return Acabar("parou de andar");
            }
            else _paradas = 0;
            _ultimaContagem = prontos;
            _comecoDaLeva = Time.realtimeSinceStartup;
            _job = _aquecer.WarmUpProgressively(_leva);
            _pedidos += _leva;
            _temJob = true;
            return false;
        }

        static void Carregar()
        {
            _carregou = true;
            _pronto = true;
            if (Application.isEditor || Gravando || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // a do jogo (a que vai junto, no StreamingAssets) e a deste computador (a gravada ao lado do executável)
            var colecao = Ler(Path.Combine(Application.streamingAssetsPath, Arquivo));
            var aoLado = Ler(CaminhoAoLadoDoJogo);
            if (colecao == null) colecao = aoLado;
            else if (aoLado != null && !colecao.Append(aoLado))
                Debug.LogWarning("[Shaders] A gravação ao lado do executável não juntou com a do jogo: fica só a do jogo.");
            if (colecao == null) return;
            bool porEstados = _porEstados = SystemInfo.supportsParallelPSOCreation;
            _total = porEstados ? colecao.totalGraphicsStateCount : colecao.variantCount;
            if (_total <= 0) return;
            _aquecer = colecao;
            _pronto = false;
            _desde = Time.realtimeSinceStartup;
            Debug.Log($"[Shaders] Aquecendo {colecao.variantCount} variantes e {colecao.totalGraphicsStateCount} estados gráficos " +
                      $"({(porEstados ? "pelos estados, em jobs" : "pelo caminho antigo, uma variante de cada vez")}; {SystemInfo.graphicsDeviceType}).");
        }

        static GraphicsStateCollection Ler(string caminho)
        {
            if (!File.Exists(caminho)) return null;
            var colecao = new GraphicsStateCollection();
            bool leu;
            try { leu = colecao.LoadFromFile(caminho); }
            catch (Exception e)
            {
                Debug.LogException(e);
                leu = false;
            }
            if (!leu)
            {
                Debug.LogWarning($"[Shaders] Não deu para ler a gravação \"{caminho}\".");
                return null;
            }
            if (colecao.graphicsDeviceType != SystemInfo.graphicsDeviceType)
            {
                Debug.Log($"[Shaders] A gravação \"{caminho}\" é do {colecao.graphicsDeviceType}, e o jogo está no " +
                          $"{SystemInfo.graphicsDeviceType}: ela fica de fora.");
                return null;
            }
            Debug.Log($"[Shaders] Gravação lida: \"{caminho}\" ({colecao.variantCount} variantes, {colecao.totalGraphicsStateCount} estados gráficos, " +
                      $"qualidade {colecao.qualityLevelName}).");
            return colecao;
        }

        static bool Acabar(string porque)
        {
            if (_temJob)
            {
                _job.Complete();
                _temJob = false;
            }
            Debug.Log($"[Shaders] Aquecimento {porque}: {_aquecer.completedWarmupCount} de {_total} ({_pedidos} pedidos) em " +
                      $"{(Time.realtimeSinceStartup - _desde) * 1000f:F0} ms.");
            _pronto = true;
            _aquecer = null;
            return true;
        }
    }
}
