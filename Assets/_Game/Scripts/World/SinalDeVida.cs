using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Ambientação: o que ficou ligado quando as pessoas fugiram. Uma luz pequena, sem sombra, e (no pisca-alerta) as
    /// lanternas do carro, que aparecem e somem (o renderer liga e desliga). Mudar o brilho por MaterialPropertyBlock
    /// tirava as lanternas do SRP Batcher e custava ~5 SetPass a mais.
    /// - Lâmpada: uma luz quente esquecida acesa num cômodo, que às vezes treme (a rede elétrica falhando).
    /// - TV: a luz fria da tela, mudando de força e de tom o tempo todo, como uma cena trocando.
    /// - Pisca-alerta: laranja, liga e desliga a 1,4 Hz.
    /// É só visual: cada máquina tem o seu (não vai pela rede).
    /// </summary>
    public class SinalDeVida : MonoBehaviour
    {
        public enum Tipo { Lampada, TV, PiscaAlerta }

        public Tipo tipo;
        public Light luz;
        [Tooltip("As lanternas do carro (só no pisca-alerta).")]
        public Renderer[] lanternas;

        float _intensidade, _semente, _proxima, _alvo = 1f, _atual = 1f, _falhaAte;
        Color _cor, _corDaCena;
        bool _acesoAntes;

        void Awake()
        {
            _intensidade = luz.intensity;
            _cor = luz.color;
            _corDaCena = _cor;
            _semente = Random.value * 100f;
            _acesoAntes = true;   // a cena começa com a luz e as lanternas ligadas
        }

        void Update()
        {
            float t = Time.time;
            switch (tipo)
            {
                case Tipo.Lampada:
                    // quase sempre firme; de vez em quando, uma queda rápida
                    if (t >= _proxima)
                    {
                        _falhaAte = t + Random.Range(0.04f, 0.25f);
                        _proxima = _falhaAte + Random.Range(3f, 12f);
                    }
                    luz.intensity = _intensidade * (t < _falhaAte ? 0.35f + 0.4f * Mathf.PerlinNoise(_semente, t * 30f) : 1f);
                    break;

                case Tipo.TV:
                    // cada "corte de cena" muda a força e o tom; entre os cortes, um tremido leve
                    if (t >= _proxima)
                    {
                        _proxima = t + Random.Range(0.12f, 1.4f);
                        _alvo = Random.Range(0.35f, 1.15f);
                        _corDaCena = Color.Lerp(_cor, Random.value < 0.5f ? new Color(0.75f, 0.85f, 1f) : new Color(1f, 0.92f, 0.85f), Random.value * 0.6f);
                    }
                    _atual = Mathf.Lerp(_atual, _alvo, 1f - Mathf.Exp(-30f * Time.deltaTime));
                    luz.intensity = _intensidade * _atual * (0.92f + 0.08f * Mathf.PerlinNoise(_semente, t * 12f));
                    luz.color = _corDaCena;
                    break;

                case Tipo.PiscaAlerta:
                    bool aceso = Mathf.Repeat(t + _semente, 0.72f) < 0.36f;
                    if (aceso == _acesoAntes) break;
                    _acesoAntes = aceso;
                    luz.enabled = aceso;
                    foreach (var r in lanternas) r.enabled = aceso;
                    break;
            }
        }
    }
}
