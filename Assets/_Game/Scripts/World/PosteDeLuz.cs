using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Poste de rua com luz de sódio. Acende ao anoitecer (cada um com um pequeno atraso), apaga de dia e mostra o cone
    /// de luz na neblina. Alguns falham (piscam) e outros estão quebrados.
    /// </summary>
    public class PosteDeLuz : MonoBehaviour
    {
        public Light luz;
        public Renderer cone;
        public Renderer lampada;
        public bool pisca;
        public bool quebrado;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _bloco;
        float _intensidade, _atraso, _semente, _falhaAte, _proximaFalha;
        Color _corCone, _corLampada;

        void Awake()
        {
            _bloco = new MaterialPropertyBlock();
            _intensidade = luz.intensity;
            _atraso = Random.value * 0.15f;
            _semente = Random.value * 100f;
            _corCone = cone.sharedMaterial.GetColor(BaseColorId);
            _corLampada = lampada.sharedMaterial.GetColor(EmissionId);
        }

        void Update()
        {
            float acesa = quebrado ? 0f : Mathf.Clamp01((CicloDoDia.Noite - 0.3f - _atraso) * 4f);
            if (pisca && acesa > 0f)
            {
                if (Time.time >= _proximaFalha)
                {
                    _falhaAte = Time.time + Random.Range(0.05f, 0.6f);
                    _proximaFalha = _falhaAte + Random.Range(0.3f, 4f);
                }
                if (Time.time < _falhaAte) acesa *= Mathf.PerlinNoise(_semente, Time.time * 25f) > 0.5f ? 0.15f : 0.7f;
            }
            luz.enabled = acesa > 0.01f;
            luz.intensity = _intensidade * acesa;
            cone.enabled = acesa > 0.01f;
            _bloco.SetColor(BaseColorId, new Color(_corCone.r, _corCone.g, _corCone.b, _corCone.a * acesa));
            cone.SetPropertyBlock(_bloco);
            _bloco.Clear();
            _bloco.SetColor(EmissionId, _corLampada * acesa);
            lampada.SetPropertyBlock(_bloco);
            _bloco.Clear();
        }
    }
}
