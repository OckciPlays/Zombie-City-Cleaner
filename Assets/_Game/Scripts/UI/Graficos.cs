using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Os gráficos (Configurações > Gráficos, a pedido em 29/10/2026: "um menu de opções lá em configuração para gráfico,
    /// onde podemos aumentar a renderização do jogador, resolução e qualidade gráfica também"):
    /// <list type="bullet">
    /// <item>a resolução e o modo da tela (tela cheia, janela sem borda, janela), pelo <c>Screen.SetResolution</c>; a Unity
    /// guarda sozinha para a próxima vez (no Editor, vale o tamanho da janela Game);</item>
    /// <item>a escala de renderização (50 a 200%): o 3D é desenhado nesse tanto da resolução (o <c>renderScale</c> do URP).
    /// Acima de 100%, desenhado maior e reduzido: mais nítido (sem serrilhado nas bordas finas) e mais pesado;</item>
    /// <item>a qualidade (Baixa, Média, Alta, Ultra): as sombras (a distância, o tamanho do mapa, as cascatas), o
    /// anti-serrilhado (o da câmera do jogador e o MSAA), as texturas e a distância em que os detalhes somem. A Alta é o
    /// jogo como era antes desta tela.</item>
    /// <item>a distância de visão dos zumbis (Curta 40 m, Média 55 m, Longa 70 m, Máxima 85 m; a pedido: "ajustar o spawn
    /// dependendo do gráfico e render distance selecionado do jogador"): até onde os zumbis são desenhados (os de longe,
    /// parados, respirando) e mandados pela rede para este jogador, e a partir de onde os novos nascem perto dele
    /// (<see cref="NascerAPartirDe"/>). Escolher uma qualidade traz a distância dela (Baixa Curta, Média Média, Alta Longa,
    /// Ultra Máxima); depois dá para mudar à parte. A Curta é o jogo como era antes.</item>
    /// </list>
    /// Gravados no computador (<c>graficos.escala</c>, <c>graficos.qualidade</c>, <c>graficos.visao</c>). As opções mudam o asset do URP em uso;
    /// no Editor, ele volta ao original ao sair do Play (senão o projeto ficaria com a escolha de quem testou).
    /// </summary>
    public static class Graficos
    {
        public static readonly string[] NomesDaQualidade = { "Baixa", "Média", "Alta", "Ultra" };
        public static readonly string[] NomesDosModos = { "Tela cheia", "Janela sem borda", "Janela" };
        static readonly FullScreenMode[] Modos = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        const int Alta = 2;
        public const float EscalaMinima = 0.5f, EscalaMaxima = 2f;

        /// <summary>A escala de renderização do 3D (1 = a resolução da tela).</summary>
        public static float Escala { get; private set; } = 1f;
        /// <summary>A qualidade: 0 Baixa, 1 Média, 2 Alta (o jogo como era), 3 Ultra.</summary>
        public static int Qualidade { get; private set; } = Alta;

        public static readonly string[] NomesDaVisao = { "Curta", "Média", "Longa", "Máxima" };
        static readonly float[] MetrosDaVisao = { 40f, 55f, 70f, 85f };
        // de onde os zumbis novos nascem, para quem escolheu cada distância (m): mais longe que isso o NavMesh (uns 60 m em
        // volta de cada jogador) quase não chega, e a célula fica sem lugar
        static readonly float[] NascerDe = { 22f, 32f, 42f, 46f };

        /// <summary>A distância de visão dos zumbis: 0 Curta (40 m, como era), 1 Média, 2 Longa, 3 Máxima.</summary>
        public static int Visao { get; private set; } = Alta;
        /// <summary>Até onde os zumbis são desenhados e mandados, para uma distância de visão (m).</summary>
        public static float Metros(int visao) => MetrosDaVisao[Mathf.Clamp(visao, 0, MetrosDaVisao.Length - 1)];
        /// <summary>Mais perto que isto de um jogador com esta distância de visão, zumbi novo não nasce (m).</summary>
        public static float NascerAPartirDe(int visao) => NascerDe[Mathf.Clamp(visao, 0, NascerDe.Length - 1)];

        public static void MudarVisao(int visao)
        {
            Visao = Mathf.Clamp(visao, 0, NomesDaVisao.Length - 1);
            PlayerPrefs.SetInt("graficos.visao", Visao);
        }

        // o asset do URP e a qualidade como vieram (a Alta usa estes; no Editor, voltam ao sair do Play)
        static UniversalRenderPipelineAsset _urp;
        static bool _guardado;
        static float _escala0, _sombra0, _lod0;
        static int _msaa0, _mapaDaSombra0, _cascatas0, _texturas0;
        static Camera _camera;
        static int _qualidadeNaCamera = -1;

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>Ao abrir o jogo (pelo Opcoes.Carregar): lê as escolhas e aplica.</summary>
        public static void Carregar()
        {
            Guardar();
            Escala = Mathf.Clamp(PlayerPrefs.GetFloat("graficos.escala", 1f), EscalaMinima, EscalaMaxima);
            Qualidade = Mathf.Clamp(PlayerPrefs.GetInt("graficos.qualidade", Alta), 0, NomesDaQualidade.Length - 1);
            Visao = Mathf.Clamp(PlayerPrefs.GetInt("graficos.visao", Qualidade), 0, NomesDaVisao.Length - 1);   // sem escolha: a da qualidade
            Aplicar();
        }

        public static void MudarEscala(float escala)
        {
            Escala = Mathf.Clamp(Mathf.Round(escala * 20f) / 20f, EscalaMinima, EscalaMaxima);   // de 5 em 5%
            PlayerPrefs.SetFloat("graficos.escala", Escala);
            Aplicar();
        }

        public static void MudarQualidade(int qualidade)
        {
            Qualidade = Mathf.Clamp(qualidade, 0, NomesDaQualidade.Length - 1);
            PlayerPrefs.SetInt("graficos.qualidade", Qualidade);
            MudarVisao(Qualidade);   // a qualidade traz a distância dela (Baixa Curta ... Ultra Máxima)
            Aplicar();
        }

        // ------------------------------------------------------------------ a resolução e o modo da tela

        /// <summary>As resoluções do monitor (sem repetir pela frequência), da menor para a maior.</summary>
        public static List<Vector2Int> Resolucoes()
        {
            var lista = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!lista.Contains(v)) lista.Add(v);
            }
            var atual = new Vector2Int(Screen.width, Screen.height);
            if (!lista.Contains(atual)) lista.Add(atual);
            lista.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return lista;
        }

        public static void MudarResolucao(Vector2Int r) => Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);

        /// <summary>O modo da tela de agora (0 tela cheia, 1 janela sem borda, 2 janela).</summary>
        public static int Modo
        {
            get
            {
                int i = System.Array.IndexOf(Modos, Screen.fullScreenMode);
                return i < 0 ? 1 : i;   // o MaximizedWindow (Mac) conta como janela sem borda
            }
        }

        public static void MudarModo(int i) => Screen.SetResolution(Screen.width, Screen.height, Modos[Mathf.Clamp(i, 0, Modos.Length - 1)]);

        // ------------------------------------------------------------------ aplicar

        static void Guardar()
        {
            if (_guardado || Urp == null) return;
            _guardado = true;
            _urp = Urp;
            _escala0 = _urp.renderScale;
            _sombra0 = _urp.shadowDistance;
            _msaa0 = _urp.msaaSampleCount;
            _mapaDaSombra0 = _urp.mainLightShadowmapResolution;
            _cascatas0 = _urp.shadowCascadeCount;
            _lod0 = QualitySettings.lodBias;
            _texturas0 = QualitySettings.globalTextureMipmapLimit;
#if UNITY_EDITOR
            Application.quitting += Devolver;
#endif
        }

        static void Aplicar()
        {
            Guardar();
            if (_urp != null)
            {
                _urp.renderScale = Escala;
                switch (Qualidade)
                {
                    case 0:   // Baixa: a sombra só perto, pequena e sem cascatas
                        _urp.shadowDistance = Mathf.Min(_sombra0, 20f);
                        _urp.mainLightShadowmapResolution = 1024;
                        _urp.shadowCascadeCount = 1;
                        _urp.msaaSampleCount = 1;
                        break;
                    case 1:   // Média
                        _urp.shadowDistance = Mathf.Min(_sombra0, 30f);
                        _urp.mainLightShadowmapResolution = 1024;
                        _urp.shadowCascadeCount = Mathf.Min(_cascatas0, 2);
                        _urp.msaaSampleCount = 1;
                        break;
                    case 3:   // Ultra: a sombra mais longe e mais definida, 4 cascatas, MSAA 4x
                        _urp.shadowDistance = Mathf.Max(_sombra0 * 1.75f, 70f);
                        _urp.mainLightShadowmapResolution = 4096;
                        _urp.shadowCascadeCount = 4;
                        _urp.msaaSampleCount = 4;
                        break;
                    default:  // Alta: como o jogo veio
                        _urp.shadowDistance = _sombra0;
                        _urp.mainLightShadowmapResolution = _mapaDaSombra0;
                        _urp.shadowCascadeCount = _cascatas0;
                        _urp.msaaSampleCount = _msaa0;
                        break;
                }
            }
            QualitySettings.globalTextureMipmapLimit = Qualidade == 0 ? Mathf.Max(_texturas0, 1) : _texturas0;   // Baixa: texturas pela metade
            QualitySettings.lodBias = _lod0 * (Qualidade == 0 ? 0.6f : Qualidade == 1 ? 0.8f : Qualidade == 3 ? 1.5f : 1f);
            _qualidadeNaCamera = -1;   // o anti-serrilhado da câmera, no próximo quadro
        }

        /// <summary>
        /// A cada quadro (pelo MenuOnline): o anti-serrilhado na câmera do jogador local (ela nasce com o jogador; a Alta e a
        /// Ultra usam o SMAA alto, como o prefab; a Média, o FXAA; a Baixa, nenhum). Fora do vídeo de divulgação e do filme.
        /// </summary>
        public static void NoQuadro()
        {
            if (ModoVideo.Ligado) return;
            var eu = JogadorRede.Local;
            var cam = eu != null ? eu.cameraMundo : null;
            if (cam == null || (cam == _camera && _qualidadeNaCamera == Qualidade)) return;
            _camera = cam;
            _qualidadeNaCamera = Qualidade;
            var dados = cam.GetUniversalAdditionalCameraData();
            if (dados == null) return;
            dados.antialiasing = Qualidade == 0 ? AntialiasingMode.None : Qualidade == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dados.antialiasingQuality = AntialiasingQuality.High;
        }

#if UNITY_EDITOR
        // no Editor, o asset do URP é o do projeto: volta ao original ao sair do Play
        static void Devolver()
        {
            Application.quitting -= Devolver;
            if (_urp != null)
            {
                _urp.renderScale = _escala0;
                _urp.shadowDistance = _sombra0;
                _urp.msaaSampleCount = _msaa0;
                _urp.mainLightShadowmapResolution = _mapaDaSombra0;
                _urp.shadowCascadeCount = _cascatas0;
            }
            QualitySettings.lodBias = _lod0;
            QualitySettings.globalTextureMipmapLimit = _texturas0;
            _guardado = false;
            _camera = null;
        }
#endif
    }
}
