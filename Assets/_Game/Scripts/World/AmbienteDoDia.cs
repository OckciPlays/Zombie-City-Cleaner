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
        /// O dia limpo do Dia 0 (de 0 a 1, nesta máquina; o DiaZeroDaPartida liga): o dia vira o <see cref="limpo"/>, e o
        /// pós-processamento do dia dá lugar ao do Dia 0 (o volume da cena dele).
        /// </summary>
        public static float DiaLimpo { get; set; }

        /// <summary>O escuro de uma cena de cinema (a introdução do Dia 0: o corredor do hospital à noite), de 0 a 1.</summary>
        public static float Escurecer { get; set; }

        float _dentro, _dentroVel, _luneta;

        void OnEnable() => NaLuneta = false;

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
            float fora = Mathf.Lerp(1f, sobraNoSubsolo, fundo) * (1f - 0.9f * Escurecer);
            // dentro de casa a neblina de fora fica rala (sem mexer na luz)
            bool dentro = olho != null && AreaInterna.Contem(olho.transform.position);
            _dentro = Mathf.SmoothDamp(_dentro, dentro ? 1f : 0f, ref _dentroVel, trocaDentro);
            // pela luneta a neblina afina (rápido: a luneta já fecha em ~0,2 s)
            _luneta = Mathf.MoveTowards(_luneta, NaLuneta ? 1f : 0f, Time.deltaTime * 8f);
            // e a cena de vídeo afina a neblina por tomada (vista do alto, ela cobria tudo)
            // o Dia 0: o dia do perfil limpo no lugar do pesado
            var dia = Misturar(this.dia, limpo, DiaLimpo);
            float neblinaAqui = (1f - fundo) * Mathf.Lerp(1f, neblinaDentro, _dentro) * Mathf.Lerp(1f, neblinaNaLuneta, _luneta) * ModoVideo.Neblina * (SemNeblina ? 0f : 1f);
            Color neblina = Color.Lerp(dia.neblina, noite.neblina, n);
            CorDaNeblina = neblina;
            RenderSettings.fogColor = neblina;
            RenderSettings.fogDensity = Mathf.Lerp(dia.densidade, noite.densidade, n) * neblinaAqui;
            RenderSettings.ambientSkyColor = Color.Lerp(dia.ceu, noite.ceu, n) * fora;
            RenderSettings.ambientEquatorColor = Color.Lerp(dia.horizonte, noite.horizonte, n) * fora;
            RenderSettings.ambientGroundColor = Color.Lerp(dia.chao, noite.chao, n) * fora;
            if (sol != null)
            {
                sol.color = Color.Lerp(dia.luz, noite.luz, n);
                // no meio da troca (amanhecer e anoitecer) a luz quase some: o sol se põe e a lua nasce
                float transicao = 1f - Mathf.Abs(n - 0.5f) * 2f;
                sol.intensity = Mathf.Lerp(dia.intensidadeLuz, noite.intensidadeLuz, n) * (1f - 0.7f * transicao) * fora;
                sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(dia.alturaLuz, noite.alturaLuz, n), Mathf.Lerp(-30f, -35f, n), 0f);
            }
            if (volumeDia != null) volumeDia.weight = (1f - n) * (1f - DiaLimpo);
            if (volumeNoite != null) volumeNoite.weight = n;
            // o fundo das câmeras do mundo é a neblina (as que desenham numa textura, como o retrato do inventário, não)
            foreach (var cam in Camera.allCameras)
                if (cam.clearFlags == CameraClearFlags.SolidColor && cam.targetTexture == null) cam.backgroundColor = neblina;

            float alturaFixa = Mathf.Lerp(dia.neblinaDeAltura, noite.neblinaDeAltura, n) * neblinaAqui;
            Shader.SetGlobalColor(CorId, new Color(neblina.r * 1.05f, neblina.g * 1.05f, neblina.b * 1.08f, 1f));
            Shader.SetGlobalVector(ParamsId, new Vector4(alturaFixa, alturaBase, queda, 140f));
            Shader.SetGlobalVector(VentoId, new Vector4(vento.x * Time.time, vento.y * Time.time, 0.045f, 0.6f));
        }
    }
}
