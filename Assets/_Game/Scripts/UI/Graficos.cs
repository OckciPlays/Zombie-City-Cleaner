using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

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
    /// jogo como era antes desta tela. A Média e a Baixa também aliviam o SSAO, a sombra suave, as lanternas, os móveis,
    /// a distância de desenho, a hera, a grama e as texturas grandes (veja "a Média e a Baixa", embaixo).</item>
    /// <item>a distância de visão dos zumbis (Curta 40 m, Média 55 m, Longa 70 m, Máxima 85 m; a pedido: "ajustar o spawn
    /// dependendo do gráfico e render distance selecionado do jogador"): até onde os zumbis são desenhados (os de longe,
    /// parados, respirando) e mandados pela rede para este jogador, e a partir de onde os novos nascem perto dele
    /// (<see cref="NascerAPartirDe"/>). Escolher uma qualidade traz a distância dela (Baixa Curta, Média Média, Alta Longa,
    /// Ultra Máxima); depois dá para mudar à parte. A Curta é o jogo como era antes.</item>
    /// <item>a pixelação (a pedido em 07/11/2026, para testar: "como ficaria o jogo com um efeito de pixelação?"): Desligada,
    /// Leve, Média, Média PS1 e Forte PS1. O 3D é desenhado em poucas linhas (270, 180 ou 135: o <c>renderScale</c> do URP
    /// pela altura da tela, refeito quando ela muda) e ampliado sem suavizar (o filtro Point do URP); nas do PS1, também as
    /// cores reduzidas a 5 bits por canal com o pontilhado (o passe "Pixelacao" do PC_Renderer,
    /// Editor/MontagemDaPixelacao, ligado só nelas). A interface (o OnGUI) continua nítida. Com ela, a escala de
    /// renderização não vale.</item>
    /// </list>
    /// Gravados no computador (<c>graficos.escala</c>, <c>graficos.qualidade</c>, <c>graficos.visao</c>, <c>graficos.pixelacao</c>). As opções mudam o asset do URP em uso;
    /// no Editor, ele volta ao original ao sair do Play (senão o projeto ficaria com a escolha de quem testou).
    /// </summary>
    public static class Graficos
    {
        public static readonly string[] NomesDaQualidade = { "Baixa", "Média", "Alta", "Ultra" };
        public static readonly string[] NomesDosModos = { "Tela cheia", "Janela sem borda", "Janela" };
        static readonly FullScreenMode[] Modos = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        const int Alta = 2, Ultra = 3;
        public const float EscalaMinima = 0.5f, EscalaMaxima = 2f;

        /// <summary>A escala de renderização do 3D (1 = a resolução da tela).</summary>
        public static float Escala { get; private set; } = 1f;
        /// <summary>A qualidade: 0 Baixa, 1 Média, 2 Alta (o jogo como era), 3 Ultra.</summary>
        public static int Qualidade { get; private set; } = Alta;

        /// <summary>
        /// Muda a cada vez que as opções são aplicadas: quem segue a qualidade (o sol, as lanternas, os móveis de perto, a
        /// mata) confere a cada quadro e refaz o seu.
        /// </summary>
        public static int Versao { get; private set; }

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

        public static readonly string[] NomesDaPixelacao = { "Desligada", "Leve", "Média", "Média PS1", "Forte PS1" };
        static readonly int[] LinhasDaPixelacao = { 0, 270, 180, 180, 135 };

        /// <summary>A pixelação: 0 desligada, 1 leve (270 linhas), 2 média (180), 3 média com as cores do PS1, 4 forte (135) com elas.</summary>
        public static int Pixelacao { get; private set; }
        /// <summary>As linhas em que o 3D é desenhado (0: sem pixelação).</summary>
        public static int LinhasPixeladas => LinhasDaPixelacao[Pixelacao];
        public static bool CoresDoPs1 => Pixelacao >= 3;

        public static void MudarPixelacao(int pixelacao)
        {
            Pixelacao = Mathf.Clamp(pixelacao, 0, NomesDaPixelacao.Length - 1);
            PlayerPrefs.SetInt("graficos.pixelacao", Pixelacao);
            Aplicar();
        }

        // nas cenas de exemplo (o hospital, o bunker, as variações), que não têm as Configurações: a mesma pixelação escolhida
        // no jogo, e o X passa para a próxima (e grava, como o menu); o resto fica como a cena era (a Alta em 100%). O
        // AndarNoExemplo fica no assembly do mundo, que não enxerga este: o Graficos se registra nele a cada Play (o domínio não
        // recarrega entre eles: a escolha gravada é lida de novo, e a altura zerada faz ela entrar no primeiro quadro)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void NasCenasDeExemplo()
        {
            Pixelacao = Mathf.Clamp(PlayerPrefs.GetInt("graficos.pixelacao", 0), 0, NomesDaPixelacao.Length - 1);
            _alturaPixelada = 0;
            ZombieCityCleaner.Mundo.AndarNoExemplo.TeclasDeFora = k =>
            {
                if (k.xKey.wasPressedThisFrame) MudarPixelacao((Pixelacao + 1) % NomesDaPixelacao.Length);
                // a gravada entra no primeiro quadro; depois, segue o tamanho da janela Game
                if (Pixelacao > 0 && Screen.height != _alturaPixelada) Aplicar();
            };
            ZombieCityCleaner.Mundo.AndarNoExemplo.AjudaDeFora = () => $"X pixelação: {NomesDaPixelacao[Pixelacao]}";
        }

        public static void MudarVisao(int visao)
        {
            Visao = Mathf.Clamp(visao, 0, NomesDaVisao.Length - 1);
            PlayerPrefs.SetInt("graficos.visao", Visao);
        }

        // o asset do URP e a qualidade como vieram (a Alta usa estes; no Editor, voltam ao sair do Play)
        static UniversalRenderPipelineAsset _urp;
        static bool _guardado;
        static float _escala0, _sombra0, _lod0, _desenho0, _borda0;
        static UpscalingFilterSelection _filtro0;
        static int _alturaPixelada;
        // o passe das cores do PS1 (o "Pixelacao" do PC_Renderer) e se ele estava ativo no asset
        static ScriptableRendererFeature _passePs1;
        static bool _passePs1Ativo0;
        static int _msaa0, _mapaDaSombra0, _cascatas0, _texturas0, _aa0, _atlas0;
        static bool _opaca0;
        static readonly int RefracaoId = Shader.PropertyToID("_ZccAguaRefracao");
        static Camera _camera;
        static int _qualidadeNaCamera = -1;

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        // a tabela das sombras do sol da floresta (o Plano_Floresta.md, "As sombras por qualidade" e "A medida das
        // instâncias"): a distância, as cascatas e o mapa da Baixa, da Média, da Alta e da Ultra
        static readonly float[] SombraDaFloresta = { 40f, 70f, 90f, 200f };
        static readonly int[] CascatasDaFloresta = { 2, 3, 3, 4 };
        static readonly int[] MapaDaFloresta = { 1024, 2048, 2048, 4096 };
        static bool _tabelaDaFloresta;

        /// <summary>
        /// A sombra do sol pela tabela da floresta (09/11/2026, a floresta nova no Lakewood: a da cena de floresta, que o
        /// usuário aprovou como a Alta): 40 m em 2 cascatas na Baixa, 70 m em 3 na Média, 90 m em 3 na Alta e 200 m em 4,
        /// com o mapa de 4096, na Ultra, o fim da sombra esmaecendo mais devagar (a borda das cascatas em 0,2) e a sombra
        /// suave do sol pela qualidade (baixa, média, alta e alta). Ligada pela cena do Lakewood (o LakewoodNaCena) enquanto
        /// ela está aberta; no mundo sorteado continua a tabela de antes.
        /// </summary>
        public static bool TabelaDaFloresta
        {
            get => _tabelaDaFloresta;
            set
            {
                if (_tabelaDaFloresta == value) return;
                _tabelaDaFloresta = value;
                if (_guardado) Aplicar();
            }
        }

        /// <summary>Ao abrir o jogo (pelo Opcoes.Carregar): lê as escolhas e aplica.</summary>
        public static void Carregar()
        {
            Guardar();
            Escala = Mathf.Clamp(PlayerPrefs.GetFloat("graficos.escala", 1f), EscalaMinima, EscalaMaxima);
            Qualidade = Mathf.Clamp(PlayerPrefs.GetInt("graficos.qualidade", Alta), 0, NomesDaQualidade.Length - 1);
            Visao = Mathf.Clamp(PlayerPrefs.GetInt("graficos.visao", Qualidade), 0, NomesDaVisao.Length - 1);   // sem escolha: a da qualidade
            Pixelacao = Mathf.Clamp(PlayerPrefs.GetInt("graficos.pixelacao", 0), 0, NomesDaPixelacao.Length - 1);
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
            _filtro0 = _urp.upscalingFilter;
            _passePs1 = null;
            foreach (var dados in _urp.rendererDataList)
                if (dados != null)
                    foreach (var f in dados.rendererFeatures)
                        if (f != null && f.name == "Pixelacao") { _passePs1 = f; _passePs1Ativo0 = f.isActive; }
            _sombra0 = _urp.shadowDistance;
            _msaa0 = _urp.msaaSampleCount;
            _opaca0 = _urp.supportsCameraOpaqueTexture;
            _mapaDaSombra0 = _urp.mainLightShadowmapResolution;
            _cascatas0 = _urp.shadowCascadeCount;
            _borda0 = _urp.cascadeBorder;
            _atlas0 = _urp.additionalLightsShadowmapResolution;
            _lod0 = QualitySettings.lodBias;
            _texturas0 = QualitySettings.globalTextureMipmapLimit;
            _aa0 = QualitySettings.antiAliasing;
            GuardarSsao();
            _comGrupo = TextureMipmapLimitGroups.HasGroup(GrupoDasTexturas);
            if (_comGrupo) _grupo0 = QualitySettings.GetTextureMipmapLimitSettings(GrupoDasTexturas);
#if UNITY_EDITOR
            // ao sair do Play (o Application.quitting não chegava sempre: o asset do URP ficava com o MSAA 4x da Ultra na
            // memória do Editor, e ele gravava o 4x nas configurações de qualidade do projeto)
            Application.quitting += Devolver;
            UnityEditor.EditorApplication.playModeStateChanged += AoMudarOPlay;
#endif
        }

        static void Aplicar()
        {
            Guardar();
            if (_urp != null)
            {
                // a pixelação: o 3D em poucas linhas (pela altura da tela) e ampliado sem suavizar; sem ela, a escala escolhida
                _alturaPixelada = Screen.height;
                if (Pixelacao > 0)
                {
                    _urp.renderScale = Mathf.Clamp(LinhasPixeladas / (float)Mathf.Max(1, Screen.height), 0.1f, 1f);
                    _urp.upscalingFilter = UpscalingFilterSelection.Point;
                }
                else
                {
                    _urp.renderScale = Escala;
                    _urp.upscalingFilter = _filtro0;
                }
                if (_passePs1 != null) _passePs1.SetActive(CoresDoPs1);
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
                // a floresta (o Lakewood): a sombra do sol pela tabela dela, por cima da de cima (o MSAA continua)
                if (_tabelaDaFloresta)
                {
                    int q = Mathf.Clamp(Qualidade, 0, SombraDaFloresta.Length - 1);
                    _urp.shadowDistance = SombraDaFloresta[q];
                    _urp.shadowCascadeCount = CascatasDaFloresta[q];
                    _urp.mainLightShadowmapResolution = MapaDaFloresta[q];
                    _urp.cascadeBorder = 0.2f;
                }
                else _urp.cascadeBorder = _borda0;
                // a Baixa: o mapa das sombras das lanternas menor (lá, só a lanterna de quem joga aqui faz sombra)
                _urp.additionalLightsShadowmapResolution = Qualidade == 0 ? Mathf.Min(_atlas0, 1024) : _atlas0;
                // a água (08/11/2026): a cópia da tela para a refração na Alta e na Ultra; na Média e na Baixa, a água mistura
                // por alfa. O reflexo do cenário no lago (a câmera espelhada, que desenha a mata de novo) só na Ultra
                // (09/11/2026, a pedido, depois da medida da floresta: ele era 40% das instâncias); nas outras, só o céu
                _urp.supportsCameraOpaqueTexture = Qualidade >= Alta;
            }
            Shader.SetGlobalFloat(RefracaoId, Qualidade >= Alta ? 1f : 0f);
            ReflexoDaAgua.Ativo = Qualidade >= Ultra;
            QualitySettings.globalTextureMipmapLimit = Qualidade == 0 ? Mathf.Max(_texturas0, 1) : _texturas0;   // Baixa: texturas pela metade
            QualitySettings.lodBias = _lod0 * (Qualidade == 0 ? 0.6f : Qualidade == 1 ? 0.8f : Qualidade == 3 ? 1.5f : 1f);
            AplicarSsao();
            AplicarTexturas();
            _qualidadeNaCamera = -1;   // o anti-serrilhado da câmera, no próximo quadro
            Versao++;
        }

        // ------------------------------------------------------------------ a Média e a Baixa

        // A pedido (31/10/2026, "os ajustes do Médio e do Baixo", da análise de desempenho): além da sombra do sol, do
        // anti-serrilhado, das texturas e do LOD, a Média e a Baixa aliviam o que mais pesa num computador fraco, perdendo um
        // pouco do visual. A Alta e a Ultra ficam como eram.

        /// <summary>Até onde a câmera do jogador desenha (m), para a da Alta: 90 m na Média e 70 na Baixa.</summary>
        public static float Desenho(float alta) => Qualidade == 1 ? Mathf.Min(alta, 90f) : Qualidade == 0 ? Mathf.Min(alta, 70f) : alta;
        /// <summary>Até onde a hera das paredes aparece (m), para a da Alta: 50 na Média e 35 na Baixa.</summary>
        public static float Hera(float alta) => Qualidade == 1 ? Mathf.Min(alta, 50f) : Qualidade == 0 ? Mathf.Min(alta, 35f) : alta;
        /// <summary>Até onde a grama baixa da mata aparece (m), para a da Alta: 28 na Média e 18 na Baixa.</summary>
        public static float Grama(float alta) => Qualidade == 1 ? Mathf.Min(alta, 28f) : Qualidade == 0 ? Mathf.Min(alta, 18f) : alta;
        /// <summary>
        /// Os móveis de perto fazem a sombra do sol (a que entra pela janela): só na Alta e na Ultra. Os andares dos prédios
        /// continuam fazendo: sem a sombra das lajes, o sol de uma janela de cima iluminaria o andar de baixo.
        /// </summary>
        public static bool SombraDosMoveis => Qualidade >= Alta;

        public enum Luz { Sol, MinhaLanterna, LanternaDosOutros }

        /// <summary>
        /// A sombra de uma luz nesta qualidade ('original': a dela na Alta). Na Média, o sol fica com a sombra suave média, a
        /// lanterna de quem joga aqui com a sombra dura e a dos outros sem sombra; na Baixa, tudo duro, e a dos outros sem.
        /// </summary>
        public static void Sombra(Light luz, Luz tipo, LightShadows original)
        {
            var sombra = original;
            var suavidade = SoftShadowQuality.UsePipelineSettings;
            if (_tabelaDaFloresta && tipo == Luz.Sol && original != LightShadows.None)
                // a floresta: o sol com a sombra suave em todas, o filtro pela qualidade
                suavidade = Qualidade == 0 ? SoftShadowQuality.Low : Qualidade == 1 ? SoftShadowQuality.Medium : SoftShadowQuality.High;
            else if (original != LightShadows.None && Qualidade < Alta)
            {
                if (tipo == Luz.LanternaDosOutros) sombra = LightShadows.None;
                else if (Qualidade == 0 || tipo == Luz.MinhaLanterna) sombra = LightShadows.Hard;
                else suavidade = SoftShadowQuality.Medium;
            }
            if (luz.shadows != sombra) luz.shadows = sombra;
            var dados = luz.GetUniversalAdditionalLightData();
            if (dados != null && dados.softShadowQuality != suavidade) dados.softShadowQuality = suavidade;
        }

        // o SSAO (a sombra nos cantos) dos renderers do URP em uso: na Média em meia resolução, na Baixa desligado. A meia
        // resolução é um campo interno do URP (o Downsample dos ajustes do ScreenSpaceAmbientOcclusion), mudado por reflexão
        // (ele relê os ajustes a cada quadro)
        struct Ssao
        {
            public ScreenSpaceAmbientOcclusion efeito;
            public object ajustes;
            public FieldInfo meia;
            public bool ativo0, meia0;
        }
        static readonly List<Ssao> _ssaos = new List<Ssao>();

        static void GuardarSsao()
        {
            _ssaos.Clear();
            foreach (var dados in _urp.rendererDataList)
            {
                if (dados == null) continue;
                foreach (var f in dados.rendererFeatures)
                {
                    if (!(f is ScreenSpaceAmbientOcclusion efeito)) continue;
                    var s = new Ssao { efeito = efeito, ativo0 = efeito.isActive };
                    s.ajustes = typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(efeito);
                    s.meia = s.ajustes?.GetType().GetField("Downsample", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (s.meia != null) s.meia0 = (bool)s.meia.GetValue(s.ajustes);
                    _ssaos.Add(s);
                }
            }
        }

        static void AplicarSsao()
        {
            foreach (var s in _ssaos)
            {
                if (s.efeito == null) continue;
                s.efeito.SetActive(Qualidade != 0 && s.ativo0);
                if (s.meia != null) s.meia.SetValue(s.ajustes, Qualidade == 1 || s.meia0);
            }
        }

        /// <summary>
        /// O grupo das texturas grandes (2048 px) dos carros dirigíveis e das armas (o mipmap limit group delas, na
        /// importação): 1024 px na Média (a Baixa já corta todas as texturas pela metade).
        /// </summary>
        public const string GrupoDasTexturas = "Carros e armas";
        static bool _comGrupo;
        static TextureMipmapLimitSettings _grupo0;

        static void AplicarTexturas()
        {
            if (!_comGrupo) return;
            var s = _grupo0;
            if (Qualidade == 1)
            {
                s.limitBiasMode = TextureMipmapLimitBiasMode.OffsetGlobalLimit;
                s.limitBias = _grupo0.limitBias + 1;
            }
            QualitySettings.SetTextureMipmapLimitSettings(GrupoDasTexturas, s);
        }

        /// <summary>
        /// A cada quadro (pelo MenuOnline): o anti-serrilhado na câmera do jogador local (ela nasce com o jogador; a Alta e a
        /// Ultra usam o SMAA alto, como o prefab; a Média, o FXAA; a Baixa, nenhum). Fora do vídeo de divulgação e do filme.
        /// </summary>
        public static void NoQuadro()
        {
            // a pixelação segue a altura da tela (a janela mudou de tamanho: as mesmas linhas)
            if (Pixelacao > 0 && Screen.height != _alturaPixelada) Aplicar();
            if (ModoVideo.Ligado) return;
            var eu = JogadorRede.Local;
            var cam = eu != null ? eu.cameraMundo : null;
            if (cam == null) return;
            if (cam != _camera) _desenho0 = cam.farClipPlane;   // a do prefab (a da Alta)
            // a distância de desenho (a Média e a Baixa); pela luneta, a de sempre (a neblina afina, e o alvo pode estar longe);
            // sem a neblina (o N da cena do Lakewood), o condado inteiro (09/11/2026: o far de 150 m do prefab escondia o horizonte)
            float longe = AmbienteDoDia.SemNeblina ? LakewoodNaCena.FarSemNeblina : AmbienteDoDia.NaLuneta ? _desenho0 : Desenho(_desenho0);
            if (cam.farClipPlane != longe) cam.farClipPlane = longe;
            if (cam == _camera && _qualidadeNaCamera == Qualidade) return;
            _camera = cam;
            _qualidadeNaCamera = Qualidade;
            var dados = cam.GetUniversalAdditionalCameraData();
            if (dados == null) return;
            dados.antialiasing = Qualidade == 0 ? AntialiasingMode.None : Qualidade == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dados.antialiasingQuality = AntialiasingQuality.High;
        }

#if UNITY_EDITOR
        static void AoMudarOPlay(UnityEditor.PlayModeStateChange estado)
        {
            if (estado == UnityEditor.PlayModeStateChange.ExitingPlayMode || estado == UnityEditor.PlayModeStateChange.EnteredEditMode) Devolver();
        }

        // no Editor, o asset do URP é o do projeto: volta ao original ao sair do Play
        static void Devolver()
        {
            Application.quitting -= Devolver;
            UnityEditor.EditorApplication.playModeStateChanged -= AoMudarOPlay;
            if (!_guardado) return;
            if (_urp != null)
            {
                _urp.renderScale = _escala0;
                _urp.upscalingFilter = _filtro0;
                _urp.shadowDistance = _sombra0;
                _urp.msaaSampleCount = _msaa0;
                _urp.supportsCameraOpaqueTexture = _opaca0;
                _urp.mainLightShadowmapResolution = _mapaDaSombra0;
                _urp.shadowCascadeCount = _cascatas0;
                _urp.cascadeBorder = _borda0;
                _urp.additionalLightsShadowmapResolution = _atlas0;
            }
            foreach (var s in _ssaos)
            {
                if (s.efeito == null) continue;
                s.efeito.SetActive(s.ativo0);
                if (s.meia != null) s.meia.SetValue(s.ajustes, s.meia0);
            }
            if (_passePs1 != null) _passePs1.SetActive(_passePs1Ativo0);
            if (_comGrupo) QualitySettings.SetTextureMipmapLimitSettings(GrupoDasTexturas, _grupo0);
            QualitySettings.lodBias = _lod0;
            QualitySettings.globalTextureMipmapLimit = _texturas0;
            // o MSAA do asset do URP passa para o anti-serrilhado das configurações de qualidade, e o Editor gravava o 4x da
            // Ultra no ProjectSettings/QualitySettings (visto em 29/10/2026): volta também
            QualitySettings.antiAliasing = _aa0;
            _guardado = false;
            _camera = null;
        }
#endif
    }
}
