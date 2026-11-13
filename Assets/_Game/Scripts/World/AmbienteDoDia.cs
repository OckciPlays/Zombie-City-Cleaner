using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Aplica a hora do dia no visual: o sol de dia vira o luar à noite, a neblina e a luz ambiente mudam de cor e de
    /// densidade, o pós-processamento troca entre o perfil do dia e o da noite, e a neblina de altura (passe de tela
    /// cheia) recebe os mesmos tons. Dia: neblina pesada e clara. Noite: escura e azulada, com os postes acesos.
    /// </summary>
    public class AmbienteDoDia : MonoBehaviour
    {
        [System.Serializable]
        public struct Tons
        {
            public Color neblina, ceu, horizonte, chao, luz;
            public float densidade, intensidadeLuz, alturaLuz, neblinaDeAltura;
        }

        public Light sol;

        // o céu (09/11/2026; o Ceu.shader): o skybox em Resources/Ceu.mat, carregado uma vez; as câmeras do mundo passam a
        // limpar com ele, e as globais do sol, da lua, das cores e das nuvens vão a cada quadro
        Material _ceu;
        bool _ceuProcurado;
        static readonly int CeuTopoId = Shader.PropertyToID("_ZccCeuTopo"), CeuHorizonteId = Shader.PropertyToID("_ZccCeuHorizonte");
        static readonly int CeuNeblinaId = Shader.PropertyToID("_ZccCeuNeblina"), SolId = Shader.PropertyToID("_ZccSol"), SolCorId = Shader.PropertyToID("_ZccSolCor");
        static readonly int LuaId = Shader.PropertyToID("_ZccLua"), NuvensId = Shader.PropertyToID("_ZccNuvens"), EstrelasId = Shader.PropertyToID("_ZccEstrelas");
        Camera[] _cameras = new Camera[8];
        public Volume volumeDia;
        public Volume volumeNoite;
        public Tons dia = new Tons
        {
            neblina = new Color(0.60f, 0.63f, 0.63f), ceu = new Color(0.55f, 0.57f, 0.59f), horizonte = new Color(0.43f, 0.45f, 0.46f), chao = new Color(0.29f, 0.29f, 0.27f),
            luz = new Color(0.78f, 0.77f, 0.72f), densidade = 0.032f, intensidadeLuz = 0.9f, alturaLuz = 55f, neblinaDeAltura = 0.07f,
        };
        // a noite precisa de neblina escura: clara, ela apaga as poças de luz dos postes e vira um nevoeiro azul-claro
        public Tons noite = new Tons
        {
            neblina = new Color(0.085f, 0.10f, 0.12f), ceu = new Color(0.12f, 0.15f, 0.19f), horizonte = new Color(0.08f, 0.10f, 0.12f), chao = new Color(0.04f, 0.045f, 0.055f),
            luz = new Color(0.56f, 0.65f, 0.78f), densidade = 0.042f, intensidadeLuz = 0.4f, alturaLuz = 38f, neblinaDeAltura = 0.05f,
        };
        // o Dia 0 (01/11/2026): o dia limpo de antes do apocalipse, com o sol, pouca neblina e as cores vivas
        public Tons limpo = new Tons
        {
            neblina = new Color(0.70f, 0.78f, 0.86f), ceu = new Color(0.60f, 0.68f, 0.78f), horizonte = new Color(0.56f, 0.58f, 0.56f), chao = new Color(0.36f, 0.34f, 0.30f),
            luz = new Color(1.0f, 0.95f, 0.84f), densidade = 0.0045f, intensidadeLuz = 1.35f, alturaLuz = 47f, neblinaDeAltura = 0.003f,
        };
        [Header("O tempo fechado (a chuva e a tempestade: World/CicloDoDia.Tempo.cs)")]
        [Tooltip("O dia com o céu fechado (pelas nuvens do Tempo): mais escuro, cinza-azulado, a neblina mais densa e o sol sem sombra.")]
        public Tons tempestadeDia = new Tons
        {
            neblina = new Color(0.40f, 0.43f, 0.45f), ceu = new Color(0.30f, 0.32f, 0.35f), horizonte = new Color(0.26f, 0.28f, 0.30f), chao = new Color(0.17f, 0.17f, 0.17f),
            luz = new Color(0.62f, 0.66f, 0.72f), densidade = 0.05f, intensidadeLuz = 0.25f, alturaLuz = 55f, neblinaDeAltura = 0.1f,
        };
        [Tooltip("A noite com o céu fechado: ainda mais escura (sem a lua), a neblina mais densa.")]
        public Tons tempestadeNoite = new Tons
        {
            neblina = new Color(0.05f, 0.06f, 0.07f), ceu = new Color(0.07f, 0.085f, 0.10f), horizonte = new Color(0.05f, 0.06f, 0.07f), chao = new Color(0.025f, 0.028f, 0.032f),
            luz = new Color(0.4f, 0.46f, 0.56f), densidade = 0.05f, intensidadeLuz = 0.12f, alturaLuz = 38f, neblinaDeAltura = 0.07f,
        };
        [Tooltip("A cor do clarão do raio (Tempo.Clarao) na luz ambiente e na neblina.")]
        public Color corDoClarao = new Color(0.75f, 0.8f, 1f);

        [Header("Neblina de altura")]
        [Tooltip("Altura (m) em que a densidade é a do chão.")]
        public float alturaBase = 0.2f;
        [Tooltip("Quanto a densidade cai por metro de altura.")]
        public float queda = 0.32f;
        [Tooltip("Velocidade do vento que arrasta o ruído da neblina de altura (m/s).")]
        public Vector2 vento = new Vector2(0.6f, 0.25f);
        [Header("Debaixo da terra")]
        [Tooltip("Altura da câmera (m) em que o subsolo começa e em que ele fica completo (descendo a escada do bunker).")]
        public Vector2 subsolo = new Vector2(-0.3f, -1.5f);
        [Tooltip("Quanto da luz ambiente e do sol sobra lá embaixo. A neblina some toda: no bunker não tem neblina.")]
        [Range(0f, 1f)] public float sobraNoSubsolo = 0.15f;
        [Header("Dentro das casas e do prédio (AreaInterna)")]
        [Tooltip("Quanto da neblina sobra lá dentro (a luz não muda: ela entra pelas janelas).")]
        [Range(0f, 1f)] public float neblinaDentro = 0.4f;
        [Tooltip("Tempo (s) para a neblina afinar ou voltar ao entrar e sair.")]
        public float trocaDentro = 0.6f;

        [Header("Luneta")]
        [Tooltip("Quanto das neblinas sobra olhando pela luneta da sniper (só na máquina de quem mira): sem isso, a neblina " +
                 "escondia tudo a mais de 40 m, e a luneta não servia.")]
        [Range(0f, 1f)] public float neblinaNaLuneta = 0.35f;

        /// <summary>Quem joga nesta máquina está olhando pela luneta (a ArmaJogador liga e desliga).</summary>
        public static bool NaLuneta { get; set; }

        /// <summary>
        /// Sem neblina nenhuma (a de distância e a de altura), nesta máquina: a opção da cena do mapa fixo, para ver o
        /// relevo de longe e medir as coisas (o F1, "Neblina", ou o N).
        /// </summary>
        public static bool SemNeblina { get; set; }

        /// <summary>
        /// Dentro do bunker da família (<see cref="BunkerDoMundo"/>): o quanto da luz de fora chega (1 fora; na garagem
        /// e na casinha um pouco, embaixo quase nada) e o quanto da neblina fica (0 dentro).
        /// </summary>
        public static float LuzNoBunker { get; set; } = 1f;
        public static float NeblinaNoBunker { get; set; } = 1f;

        /// <summary>
        /// Dentro do Hospital Principal (<see cref="HospitalDoMundo"/>, 08/11/2026): o mesmo, à parte do bunker (cada um
        /// escreve o seu a cada quadro; vale o menor). No subsolo, sem janela, sobra pouca luz e quase nenhuma neblina.
        /// </summary>
        public static float LuzNoHospital { get; set; } = 1f;
        public static float NeblinaNoHospital { get; set; } = 1f;

        /// <summary>
        /// O dia limpo do Dia 0 (de 0 a 1, nesta máquina; o DiaZeroDaPartida liga): o dia vira o <see cref="limpo"/>, e o
        /// pós-processamento do dia dá lugar ao do Dia 0 (o volume da cena dele).
        /// </summary>
        public static float DiaLimpo { get; set; }

        /// <summary>O escuro de uma cena de cinema (a introdução do Dia 0: o corredor do hospital à noite), de 0 a 1.</summary>
        public static float Escurecer { get; set; }

        float _dentro, _dentroVel, _luneta, _sombraDoSol = -1f;
        // a sombra do sol pela qualidade (UI/Graficos): a dele como veio, e a versão das opções já aplicada
        LightShadows _sombraDoSol0;
        int _versaoDosGraficos = -1;

        void OnEnable() => NaLuneta = false;

        // ao sair da cena (ou do Play): a neblina de altura desligada. As globais ficam na memória do Editor entre as cenas,
        // e as cenas de exemplo (sem o AmbienteDoDia) herdavam a neblina do mundo (09/11/2026, a cena de floresta leve).
        void OnDisable() => Shader.SetGlobalVector(ParamsId, Vector4.zero);

        static readonly int CorId = Shader.PropertyToID("_NeblinaCor");
        static readonly int ParamsId = Shader.PropertyToID("_NeblinaParams");
        static readonly int VentoId = Shader.PropertyToID("_NeblinaVento");

        /// <summary>Cor da neblina agora (as câmeras usam no fundo).</summary>
        public static Color CorDaNeblina { get; private set; }

        static Tons Misturar(Tons a, Tons b, float t)
        {
            if (t <= 0f) return a;
            return new Tons
            {
                neblina = Color.Lerp(a.neblina, b.neblina, t), ceu = Color.Lerp(a.ceu, b.ceu, t), horizonte = Color.Lerp(a.horizonte, b.horizonte, t),
                chao = Color.Lerp(a.chao, b.chao, t), luz = Color.Lerp(a.luz, b.luz, t), densidade = Mathf.Lerp(a.densidade, b.densidade, t),
                intensidadeLuz = Mathf.Lerp(a.intensidadeLuz, b.intensidadeLuz, t), alturaLuz = Mathf.Lerp(a.alturaLuz, b.alturaLuz, t),
                neblinaDeAltura = Mathf.Lerp(a.neblinaDeAltura, b.neblinaDeAltura, t),
            };
        }

        void LateUpdate()
        {
            float n = CicloDoDia.Noite;
            // no bunker a luz do dia e a neblina de fora não chegam: só as lâmpadas e a lanterna iluminam, e não há
            // neblina nenhuma
            var olho = Camera.main;
            float fundo = olho != null ? Mathf.InverseLerp(subsolo.x, subsolo.y, olho.transform.position.y) : 0f;
            fundo = Mathf.Max(fundo, ModoVideo.Subsolo);   // o corte do bunker na cena de vídeo olha do alto, com a luz de dentro
            float fora = Mathf.Min(Mathf.Lerp(1f, sobraNoSubsolo, fundo), Mathf.Min(LuzNoBunker, LuzNoHospital)) * (1f - 0.9f * Escurecer);
            // dentro de casa a neblina de fora fica rala (sem mexer na luz)
            bool dentro = olho != null && AreaInterna.Contem(olho.transform.position);
            _dentro = Mathf.SmoothDamp(_dentro, dentro ? 1f : 0f, ref _dentroVel, trocaDentro);
            // pela luneta a neblina afina (rápido: a luneta já fecha em ~0,2 s)
            _luneta = Mathf.MoveTowards(_luneta, NaLuneta ? 1f : 0f, Time.deltaTime * 8f);
            // e a cena de vídeo afina a neblina por tomada (vista do alto, ela cobria tudo)
            // o Dia 0: o dia do perfil limpo no lugar do pesado
            var dia = Misturar(this.dia, limpo, DiaLimpo);
            // o céu fechado (a chuva, a tempestade): o dia e a noite ficam mais escuros e a neblina, mais densa
            float nuvens = Tempo.Nuvens;
            dia = Misturar(dia, tempestadeDia, nuvens);
            var noite = Misturar(this.noite, tempestadeNoite, nuvens);
            float neblinaAqui = (1f - fundo) * Mathf.Lerp(1f, neblinaDentro, _dentro) * Mathf.Lerp(1f, neblinaNaLuneta, _luneta) * ModoVideo.Neblina * (SemNeblina ? 0f : 1f)
                                * Mathf.Min(NeblinaNoBunker, NeblinaNoHospital);
            Color neblina = Color.Lerp(dia.neblina, noite.neblina, n);
            CorDaNeblina = neblina;
            RenderSettings.fogColor = neblina;
            RenderSettings.fogDensity = Mathf.Lerp(dia.densidade, noite.densidade, n) * neblinaAqui;
            RenderSettings.ambientSkyColor = Color.Lerp(dia.ceu, noite.ceu, n) * fora;
            RenderSettings.ambientEquatorColor = Color.Lerp(dia.horizonte, noite.horizonte, n) * fora;
            RenderSettings.ambientGroundColor = Color.Lerp(dia.chao, noite.chao, n) * fora;
            // o clarão do raio acende o céu, a neblina e tudo em volta por um instante (no bunker, quase nada)
            float clarao = Tempo.Clarao * fora * (1f - fundo);
            if (clarao > 0.001f)
            {
                RenderSettings.ambientSkyColor += corDoClarao * (clarao * 1.3f);
                RenderSettings.ambientEquatorColor += corDoClarao * (clarao * 1.0f);
                RenderSettings.ambientGroundColor += corDoClarao * (clarao * 0.5f);
                neblina += corDoClarao * (clarao * 0.35f);
                RenderSettings.fogColor = neblina;
                CorDaNeblina = neblina;
            }
            // o reflexo do céu acompanha a luz ambiente (a noite, o céu fechado, o subsolo e o bunker): ele é o do dia, fixo, e
            // no escuro as faces lisas, como o azulejo, ficavam com um brilho cinza (07/11/2026, a pedido)
            float ceuDoDia = dia.ceu.grayscale;
            RenderSettings.reflectionIntensity = ceuDoDia > 0.001f ? Mathf.Clamp01(RenderSettings.ambientSkyColor.grayscale / ceuDoDia) : 1f;
            if (sol != null)
            {
                if (_versaoDosGraficos != UI.Graficos.Versao)
                {
                    if (_versaoDosGraficos < 0) _sombraDoSol0 = sol.shadows;
                    _versaoDosGraficos = UI.Graficos.Versao;
                    UI.Graficos.Sombra(sol, UI.Graficos.Luz.Sol, _sombraDoSol0);
                }
                sol.color = Color.Lerp(dia.luz, noite.luz, n);
                // no meio da troca (amanhecer e anoitecer) a luz quase some: o sol se põe e a lua nasce
                float transicao = 1f - Mathf.Abs(n - 0.5f) * 2f;
                sol.intensity = Mathf.Lerp(dia.intensidadeLuz, noite.intensidadeLuz, n) * (1f - 0.7f * transicao) * fora;
                sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(dia.alturaLuz, noite.alturaLuz, n), Mathf.Lerp(-30f, -35f, n), 0f);
                // com o céu fechado, a luz fica difusa: quase sem sombra
                if (_sombraDoSol < 0f) _sombraDoSol = sol.shadowStrength;
                sol.shadowStrength = Mathf.Lerp(_sombraDoSol, 0.25f, nuvens);
            }
            if (volumeDia != null) volumeDia.weight = (1f - n) * (1f - DiaLimpo);
            if (volumeNoite != null) volumeNoite.weight = n;
            // o fundo das câmeras do mundo é a neblina (as que desenham numa textura, como o retrato do inventário, não)
            if (_cameras.Length < Camera.allCamerasCount) _cameras = new Camera[Camera.allCamerasCount + 4];
            int quantas = Camera.GetAllCameras(_cameras);   // (o Camera.allCameras fazia uma lista nova a cada quadro)
            if (!_ceuProcurado)
            {
                _ceuProcurado = true;
                _ceu = Resources.Load<Material>("Ceu");
                if (_ceu != null) RenderSettings.skybox = _ceu;
            }
            for (int i = 0; i < quantas; i++)
            {
                var cam = _cameras[i];
                if (cam.targetTexture != null) continue;
                if (_ceu != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.clearFlags = CameraClearFlags.Skybox;
                if (cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = neblina;
            }
            if (_ceu != null)
            {
                // o céu: de dia um azul acinzentado (mais cinza com as nuvens), de noite o escuro da noite; o horizonte com a
                // cor do horizonte, e a névoa da neblina embaixo
                var azul = new Color(0.3f, 0.46f, 0.74f);
                var topoDia = Color.Lerp(dia.ceu * 0.95f, azul, 0.6f * (1f - nuvens));
                var topo = Color.Lerp(topoDia, noite.ceu * 0.9f, n) * fora;
                var horizonte = Color.Lerp(dia.horizonte * 1.15f, noite.horizonte * 1.1f, n) * fora;
                Shader.SetGlobalColor(CeuTopoId, topo);
                Shader.SetGlobalColor(CeuHorizonteId, horizonte);
                Shader.SetGlobalColor(CeuNeblinaId, neblina);
                var direcao = sol != null ? -sol.transform.forward : Vector3.up;
                float transicao = 1f - Mathf.Abs(n - 0.5f) * 2f;
                Shader.SetGlobalVector(SolId, new Vector4(direcao.x, direcao.y, direcao.z, (1f - n) * (1f - 0.6f * transicao) * fora));
                Shader.SetGlobalColor(SolCorId, dia.luz * 1.6f);
                Shader.SetGlobalVector(LuaId, new Vector4(direcao.x, direcao.y, direcao.z, n * (1f - 0.6f * transicao) * fora));
                Shader.SetGlobalFloat(NuvensId, nuvens * 0.6f);
                Shader.SetGlobalFloat(EstrelasId, n * (1f - nuvens) * fora);
            }

            float alturaFixa = Mathf.Lerp(dia.neblinaDeAltura, noite.neblinaDeAltura, n) * neblinaAqui;
            Shader.SetGlobalColor(CorId, new Color(neblina.r * 1.05f, neblina.g * 1.05f, neblina.b * 1.08f, 1f));
            Shader.SetGlobalVector(ParamsId, new Vector4(alturaFixa, alturaBase, queda, 140f));
            Shader.SetGlobalVector(VentoId, new Vector4(vento.x * Time.time, vento.y * Time.time, 0.045f, 0.6f));
        }
    }
}
