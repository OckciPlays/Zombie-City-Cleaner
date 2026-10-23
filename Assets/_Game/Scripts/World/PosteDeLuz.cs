using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Poste de rua com luz de sódio. Acende ao anoitecer (cada um com um pequeno atraso), apaga de dia e mostra o cone
    /// de luz na neblina. Alguns falham (piscam) e outros estão quebrados.
    /// Quem fica na poça de luz de um poste aceso é visto de mais longe pelos zumbis (Ilumina).
    /// </summary>
    public class PosteDeLuz : MonoBehaviour
    {
        static readonly List<PosteDeLuz> Todos = new List<PosteDeLuz>();

        public Light luz;
        public Renderer cone;
        public Renderer lampada;
        public bool pisca;
        public bool quebrado;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _bloco;
        float _intensidade, _atraso, _semente, _falhaAte, _proximaFalha, _acesa, _raio;
        Color _corCone, _corLampada;

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        /// <summary>Se um ponto do chão está na poça de luz de algum poste aceso.</summary>
        public static bool Ilumina(Vector3 ponto)
        {
            foreach (var p in Todos)
            {
                if (p._acesa < 0.5f) continue;
                Vector3 d = p.luz.transform.position - ponto;
                if (d.y > 0f && d.x * d.x + d.z * d.z < p._raio * p._raio) return true;
            }
            return false;
        }

        void Awake()
        {
            _bloco = new MaterialPropertyBlock();
            _intensidade = luz.intensity;
            // raio da poça no chão: a luz aponta para baixo, com o ângulo do spot (a borda é fraca, por isso 0,75)
            _raio = luz.transform.position.y * Mathf.Tan(luz.spotAngle * 0.5f * Mathf.Deg2Rad) * 0.75f;
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
            _acesa = acesa;
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
