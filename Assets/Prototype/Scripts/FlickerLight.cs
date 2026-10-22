using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>Luz de teto com mau contato: oscila e às vezes apaga. Também apaga o "bulbo" emissivo junto.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [Range(0f, 1f)] public float brokenness = 0.3f;
        public Renderer bulb;
        public Color bulbColor = new Color(1f, 0.85f, 0.6f);

        Light _light;
        float _base, _offTimer, _seed;
        MaterialPropertyBlock _block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Start() => Init();

        void Init()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 100f;
            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (_block == null) Init();   // campos não serializados zeram se os scripts recompilarem durante o Play
            float buzz = 0.85f + 0.15f * Mathf.PerlinNoise(_seed, Time.time * 8f);
            if (_offTimer > 0f) _offTimer -= Time.deltaTime;
            else if (Random.value < brokenness * Time.deltaTime * 3f) _offTimer = Random.Range(0.05f, 0.25f + brokenness);
            float k = _offTimer > 0f ? (Random.value < 0.3f ? 0.3f : 0f) : buzz;
            _light.intensity = _base * k;

            if (bulb != null)
            {
                bulb.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, bulbColor * (k * 2f));
                bulb.SetPropertyBlock(_block);
            }
        }
    }
}
