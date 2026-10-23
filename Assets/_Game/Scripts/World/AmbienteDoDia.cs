using UnityEngine;
using UnityEngine.Rendering;

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
        [Tooltip("Quanto da luz ambiente, do sol e da neblina sobra lá embaixo.")]
        [Range(0f, 1f)] public float sobraNoSubsolo = 0.15f;

        static readonly int CorId = Shader.PropertyToID("_NeblinaCor");
        static readonly int ParamsId = Shader.PropertyToID("_NeblinaParams");
        static readonly int VentoId = Shader.PropertyToID("_NeblinaVento");

        /// <summary>Cor da neblina agora (as câmeras usam no fundo).</summary>
        public static Color CorDaNeblina { get; private set; }

        void LateUpdate()
        {
            float n = CicloDoDia.Noite;
            // no bunker a luz do dia e a neblina de fora não chegam: só a lâmpada e a lanterna iluminam
            var olho = Camera.main;
            float fundo = olho != null ? Mathf.InverseLerp(subsolo.x, subsolo.y, olho.transform.position.y) : 0f;
            float fora = Mathf.Lerp(1f, sobraNoSubsolo, fundo);
            Color neblina = Color.Lerp(dia.neblina, noite.neblina, n);
            CorDaNeblina = neblina;
            RenderSettings.fogColor = neblina;
            RenderSettings.fogDensity = Mathf.Lerp(dia.densidade, noite.densidade, n) * fora;
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
            if (volumeDia != null) volumeDia.weight = 1f - n;
            if (volumeNoite != null) volumeNoite.weight = n;
            foreach (var cam in Camera.allCameras)
                if (cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = neblina;

            float alturaFixa = Mathf.Lerp(dia.neblinaDeAltura, noite.neblinaDeAltura, n) * fora;
            Shader.SetGlobalColor(CorId, new Color(neblina.r * 1.05f, neblina.g * 1.05f, neblina.b * 1.08f, 1f));
            Shader.SetGlobalVector(ParamsId, new Vector4(alturaFixa, alturaBase, queda, 140f));
            Shader.SetGlobalVector(VentoId, new Vector4(vento.x * Time.time, vento.y * Time.time, 0.045f, 0.6f));
        }
    }
}
