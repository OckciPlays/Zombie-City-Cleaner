using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Atmosphere
{
    /// <summary>
    /// Cinza e poeira flutuando em volta de quem está olhando (a câmera ativa). As partículas ficam no mundo: o jogador
    /// anda e passa por elas. De dia são cinzas claras caindo devagar; à noite, poeira escura que só aparece na luz.
    /// Embaixo da terra (a escada e o bunker) elas somem e param.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticulasNoAr : MonoBehaviour
    {
        public Color corDia = new Color(0.82f, 0.82f, 0.8f, 0.55f);
        public Color corNoite = new Color(0.45f, 0.47f, 0.5f, 0.35f);
        [Tooltip("Abaixo desta altura da câmera (m) não há partículas: a escada do bunker e o bunker.")]
        public float alturaDoSubsolo = -0.3f;

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        bool _embaixo;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            var main = _ps.main;
            main.loop = true; main.playOnAwake = true; main.startLifetime = 9f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 900;
            var emissao = _ps.emission; emissao.rateOverTime = 90f;
            var forma = _ps.shape; forma.shapeType = ParticleSystemShapeType.Box; forma.scale = new Vector3(26f, 12f, 26f);
            var velocidade = _ps.velocityOverLifetime; velocidade.enabled = true; velocidade.space = ParticleSystemSimulationSpace.World;
            velocidade.x = new ParticleSystem.MinMaxCurve(0.1f, 0.35f); velocidade.y = new ParticleSystem.MinMaxCurve(-0.25f, -0.05f); velocidade.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
            var ruido = _ps.noise; ruido.enabled = true; ruido.strength = 0.25f; ruido.frequency = 0.3f; ruido.scrollSpeed = 0.2f;
            var cor = _ps.colorOverLifetime; cor.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            cor.color = g;
            _renderer = GetComponent<ParticleSystemRenderer>(); _renderer.renderMode = ParticleSystemRenderMode.Billboard;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _renderer.receiveShadows = false;
            _ps.Play();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position;
            bool embaixo = cam != null && cam.transform.position.y < alturaDoSubsolo;
            if (embaixo != _embaixo)
            {
                _embaixo = embaixo;
                _renderer.enabled = !embaixo;
                if (embaixo) _ps.Pause();
                else _ps.Play();
            }
            var main = _ps.main;
            main.startColor = Color.Lerp(corDia, corNoite, CicloDoDia.Noite);
        }
    }
}
