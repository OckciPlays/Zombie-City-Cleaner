using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os ajustes do jogo na cena de floresta andável (o Plano_Floresta.md, 09/11/2026), que a FlorestaDeTeste (do assembly
    /// do mundo) não alcança: a sombra do sol e a grama densa **pela qualidade dos gráficos** (a Baixa, a Média, a Alta e a
    /// Ultra: a escolha gravada no jogo entra ao abrir a cena, e o Q troca aqui, sem gravar) e o reflexo da água no riacho
    /// (só na Ultra, como no jogo). O O liga e desliga a oclusão na GPU do GPU Resident Drawer (desligada: medida nesta
    /// mata, ela cortava 37% dos triângulos mas deixava o quadro mais lento; fica pronta para o teste no Lakewood). O Z liga
    /// e desliga o depth priming do renderizador principal (a passada de cor ilumina só o pixel da frente: a profundidade
    /// já sai antes, na passada do SSAO, e a cor desenha com o teste "igual", sem as cartas de trás). Desligado: medido em
    /// 09/11/2026, ele baixava a GPU de 2,9–3,1 para 1,9 ms, mas com o GPU Resident Drawer umas árvores somem (as de trás
    /// da crista do morro, na vista da clareira); sem o GRD elas aparecem, mas a cena volta a uns 3.250 draw calls. Fica só
    /// para o teste, até a Unity consertar.
    /// As cenas de exemplo não passam pelo Graficos.Carregar (lá só a pixelação entra), por isso a cena aplica o dela; e o
    /// Graficos.Aplicar (a pixelação, no X) devolve o asset do URP ao que era: a cena reaplica quando a Versao muda.
    /// Devolve tudo ao sair do Play.
    /// </summary>
    public class AjustesDaFlorestaDeTeste : MonoBehaviour
    {
        [Tooltip("Até onde o sol faz sombra (m), na Baixa, na Média, na Alta e na Ultra.")] public float[] sombraAte = { 40f, 70f, 90f, 200f };
        [Tooltip("As cascatas de sombra, por qualidade.")] public int[] cascatas = { 2, 3, 3, 4 };
        [Tooltip("O tamanho do mapa das sombras do sol, por qualidade.")] public int[] mapaDaSombra = { 1024, 2048, 2048, 4096 };
        [Tooltip("Até onde a grama densa aparece (m), por qualidade (0: o alcance do componente).")] public float[] gramaAte = { 18f, 28f, 0f, 0f };
        [Tooltip("Até onde o reflexo da água desenha (m).")] public float reflexoAte = 1000f;
        [Tooltip("A oclusão na GPU do GPU Resident Drawer (as árvores escondidas atrás de outras e dos morros não são "
                 + "desenhadas). O O troca no Play.")] public bool oclusaoNaGpu = false;
        [Tooltip("O depth priming do renderizador principal (a cor ilumina só o pixel da frente; sem o MSAA). Só para o teste: "
                 + "com o GPU Resident Drawer, umas árvores somem. O Z troca no Play.")] public bool depthPriming = false;

        /// <summary>A qualidade em uso na cena: 0 Baixa, 1 Média, 2 Alta, 3 Ultra.</summary>
        public int Qualidade { get; private set; } = 2;
        public string Resumo { get; private set; } = "";

        UniversalRenderPipelineAsset _urp;
        float _sombra0, _borda0, _grama0 = -1f;
        bool _reflexo0 = true, _oclusao0;
        UniversalRendererData _renderizador;
        DepthPrimingMode _priming0;
        int _cascatas0, _mapa0, _versao = -1;
        GramaDensa _grama;
        UniversalAdditionalLightData _sol;
        SoftShadowQuality _suavidade0;
        [System.NonSerialized] GUIStyle _estilo;

        void OnEnable()
        {
            _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_urp != null)
            {
                _sombra0 = _urp.shadowDistance;
                _cascatas0 = _urp.shadowCascadeCount;
                _mapa0 = _urp.mainLightShadowmapResolution;
                _borda0 = _urp.cascadeBorder;
                _oclusao0 = _urp.gpuResidentDrawerEnableOcclusionCullingInCameras;
            }
            // o renderizador principal (o primeiro da lista do asset: o da câmera do jogo; o dos braços é outro)
            _renderizador = _urp != null && _urp.rendererDataList.Length > 0 ? _urp.rendererDataList[0] as UniversalRendererData : null;
            if (_renderizador != null) _priming0 = _renderizador.depthPrimingMode;
            _reflexo0 = ReflexoDaAgua.Ativo;
            _grama = GetComponent<GramaDensa>();
            if (_grama != null) _grama0 = _grama.alcance;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.TryGetComponent(out _sol)) { _suavidade0 = _sol.softShadowQuality; break; }
            Qualidade = Mathf.Clamp(PlayerPrefs.GetInt("graficos.qualidade", 2), 0, 3);
            Aplicar();
            ReflexoDaAgua.Alcance = reflexoAte;
            ReflexoDaAgua.Instalar(FlorestaDeTeste.NivelDaAgua);
        }

        void OnDisable()
        {
            if (_urp != null)
            {
                _urp.shadowDistance = _sombra0;
                _urp.shadowCascadeCount = _cascatas0;
                _urp.mainLightShadowmapResolution = _mapa0;
                _urp.cascadeBorder = _borda0;
                _urp.gpuResidentDrawerEnableOcclusionCullingInCameras = _oclusao0;
            }
            if (_renderizador != null && _renderizador.depthPrimingMode != _priming0) _renderizador.depthPrimingMode = _priming0;
            ReflexoDaAgua.Ativo = _reflexo0;
            if (_grama != null && _grama0 > 0f) _grama.alcance = _grama0;
            if (_sol != null) _sol.softShadowQuality = _suavidade0;
            ReflexoDaAgua.Alcance = float.PositiveInfinity;
        }

        /// <summary>Troca a qualidade na cena (0 Baixa a 3 Ultra), sem gravar a escolha do jogo.</summary>
        public void Mudar(int qualidade)
        {
            Qualidade = Mathf.Clamp(qualidade, 0, 3);
            Aplicar();
        }

        /// <summary>A sombra do sol e o alcance da grama da qualidade em uso.</summary>
        public void Aplicar()
        {
            int q = Mathf.Clamp(Qualidade, 0, 3);
            if (_urp != null)
            {
                _urp.shadowDistance = Em(sombraAte, q, 120f);
                _urp.shadowCascadeCount = Mathf.Clamp(Em(cascatas, q, 4), 1, 4);
                _urp.mainLightShadowmapResolution = Em(mapaDaSombra, q, 2048);
                _urp.cascadeBorder = 0.2f;   // o fim da sombra esmaecendo mais devagar (o padrão, 0,1, cortava seco)
                _urp.gpuResidentDrawerEnableOcclusionCullingInCameras = oclusaoNaGpu;
            }
            // o depth priming (trocar o modo refaz o renderizador: só quando muda)
            if (_renderizador != null)
            {
                var modo = depthPriming ? DepthPrimingMode.Forced : DepthPrimingMode.Disabled;
                if (_renderizador.depthPrimingMode != modo) _renderizador.depthPrimingMode = modo;
            }
            // o reflexo do cenário no riacho só na Ultra, como no jogo (o Graficos)
            ReflexoDaAgua.Ativo = q == 3;
            if (_grama != null && _grama0 > 0f)
            {
                float g = Em(gramaAte, q, 0f);
                _grama.alcance = g > 0f ? Mathf.Min(_grama0, g) : _grama0;
            }
            // a sombra suave do sol (o filtro): baixa na Baixa, média na Média, alta na Alta e na Ultra (a borda das cascatas
            // e a troca de resolução entre elas ficam menos visíveis)
            if (_sol != null) _sol.softShadowQuality = q == 0 ? SoftShadowQuality.Low : q == 1 ? SoftShadowQuality.Medium : SoftShadowQuality.High;
            _versao = Graficos.Versao;
            Resumo = $"Gráficos: {Graficos.NomesDaQualidade[q]} · a sombra do sol até {(_urp != null ? _urp.shadowDistance : 0f):0} m em {(_urp != null ? _urp.shadowCascadeCount : 0)} cascatas ({(_urp != null ? _urp.mainLightShadowmapResolution : 0)} px)" +
                     (_grama != null ? $" · a grama até {_grama.alcance:0} m" : "") + (q == 3 ? " · com o reflexo" : "") +
                     $" · Q troca a qualidade · O oclusão na GPU ({(oclusaoNaGpu ? "ligada" : "desligada")})" +
                     $" · Z depth priming ({(depthPriming ? "ligado" : "desligado")})";
        }

        static float Em(float[] lista, int i, float padrao) => lista != null && i < lista.Length ? lista[i] : padrao;
        static int Em(int[] lista, int i, int padrao) => lista != null && i < lista.Length ? lista[i] : padrao;

        void Update()
        {
            if (!Application.isPlaying) return;
            // o Graficos.Aplicar (a pixelação no X) devolve o asset ao que era: reaplicar
            if (Graficos.Versao != _versao) Aplicar();
            var k = Keyboard.current;
            if (k != null && k.qKey.wasPressedThisFrame) Mudar((Qualidade + 1) % 4);
            if (k != null && k.oKey.wasPressedThisFrame) { oclusaoNaGpu = !oclusaoNaGpu; Aplicar(); }
            if (k != null && k.zKey.wasPressedThisFrame) { depthPriming = !depthPriming; Aplicar(); }
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (_estilo == null) _estilo = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperRight };
            _estilo.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 1220, 68, 1210, 20), Resumo, _estilo);
        }
    }
}
