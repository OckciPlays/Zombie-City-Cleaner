using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A briga com o peixe no círculo (27/11/2026, a pesca nova, a etapa 3, a pedido: "seria mais interessante se fosse
    /// parecido com o do stardew valley, onde cada peixe possui um padrão de pesca ... um circulo pequeno com a barra do peixe
    /// livre nesse circulo, podendo ir para todo o lugar ... um ícone da boia, que representaria a nossa boia e precisamos
    /// encaixar até que recolheria o peixe"). Só a conta, sem tela (a tela é o <see cref="UI.CirculoDaPesca"/>):
    /// <list type="bullet">
    /// <item><b>O peixe</b> anda num disco de raio 1 pelo jeito dele (<see cref="JeitoDoPeixe"/>): um rumo que vira aos poucos,
    /// os arrancos (mais rápido e mudando de lado), as paradas e a curva de volta perto da beira. Ele cansa: perde até 40% da
    /// velocidade em 30 s.</item>
    /// <item><b>A boia</b> vai pelo olhar (o mouse ou o analógico direito: a câmera fica parada na briga), com um pouco de peso,
    /// como na água, e não sai do disco.</item>
    /// <item><b>A barra de captura</b> começa em 30%: enche com a boia em cima do peixe (os dois ícones se tocando) e esvazia
    /// longe dele. Cheia, pegou; vazia, escapou. Em 60 s, escapou.</item>
    /// </list>
    /// </summary>
    public sealed class BrigaDoPeixe
    {
        /// <summary>O raio do ícone da boia (do raio do círculo).</summary>
        public const float RaioDaBoia = 0.11f;
        /// <summary>A barra no começo e o tempo máximo (s).</summary>
        public const float Comeco = 0.3f, Limite = 60f;
        /// <summary>Quanto do disco a boia anda por grau de olhar (o mouse com a sensibilidade do jogo).</summary>
        public const float DiscoPorGrau = 1f / 22f;
        /// <summary>O peso da boia: quão rápido ela chega onde o olhar mandou (1/s).</summary>
        const float PesoDaBoia = 11f;

        readonly JeitoDoPeixe _jeito;
        Vector2 _peixe, _boia, _alvo;
        float _rumo, _giro, _proximaVirada, _velocidade;
        float _arrancoAte = -1f, _paradaAte = -1f;

        /// <summary>O peixe e a boia no disco (raio 1, o centro no meio; y para cima na tela).</summary>
        public Vector2 Peixe => _peixe;
        public Vector2 Boia => _boia;
        /// <summary>Para onde o peixe nada agora (graus, 0 para a direita, contra o relógio).</summary>
        public float Rumo => _rumo * Mathf.Rad2Deg;
        /// <summary>A barra de captura (0 a 1).</summary>
        public float Captura { get; private set; } = Comeco;
        /// <summary>O tempo da briga (s).</summary>
        public float Tempo { get; private set; }
        /// <summary>A boia está em cima do peixe (a barra enchendo).</summary>
        public bool NoPeixe { get; private set; }
        /// <summary>O arranco de agora (0 sem, 1 no meio de um arranco): para a vara e a boia no lago.</summary>
        public float Arranco { get; private set; }
        /// <summary>O quanto o peixe está longe da boia (0 em cima, 1 do outro lado do disco).</summary>
        public float Longe => Mathf.Clamp01((Vector2.Distance(_peixe, _boia) - (RaioDaBoia + _jeito.tamanho)) / 1.5f);
        public bool Pegou => Captura >= 1f;
        public bool Escapou => Captura <= 0f || Tempo >= Limite;
        public bool Acabou => Pegou || Escapou;
        public JeitoDoPeixe Jeito => _jeito;

        public BrigaDoPeixe(JeitoDoPeixe jeito)
        {
            _jeito = jeito;
            // o peixe começa num canto, e a boia no meio
            float a = Random.Range(0f, Mathf.PI * 2f);
            _peixe = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f;
            _rumo = a + Random.Range(-1.2f, 1.2f);
            _boia = _alvo = Vector2.zero;
            _velocidade = jeito.velocidade;
        }

        /// <summary>
        /// Um passo: 'olhar' é o olhar deste quadro em graus (x para a direita, y para cima), que move a boia; 'dt' em s.
        /// </summary>
        public void Passo(Vector2 olhar, float dt)
        {
            if (Acabou || dt <= 0f) return;
            Tempo += dt;
            Peixe_(dt);
            // a boia: o olhar leva o alvo; ela vai atrás com peso, sem sair do disco
            _alvo = DentroDoDisco(_alvo + olhar * DiscoPorGrau, 1f - RaioDaBoia);
            _boia = DentroDoDisco(Vector2.Lerp(_boia, _alvo, 1f - Mathf.Exp(-PesoDaBoia * dt)), 1f - RaioDaBoia);
            NoPeixe = Vector2.Distance(_boia, _peixe) < RaioDaBoia + _jeito.tamanho * 0.85f;
            Captura = Mathf.Clamp01(Captura + (NoPeixe ? _jeito.enche : -_jeito.esvazia) * dt);
        }

        // o peixe: vira aos poucos, às vezes arranca ou para, e volta da beira
        void Peixe_(float dt)
        {
            float agora = Tempo;
            if (agora >= _proximaVirada)
            {
                _giro = Random.Range(-1f, 1f) * _jeito.giro * Mathf.Deg2Rad;
                _proximaVirada = agora + Random.Range(0.5f, 1.5f);
            }
            if (agora >= _arrancoAte && agora >= _paradaAte)
            {
                if (Random.value < _jeito.arrancosPorSegundo * dt)
                {
                    _arrancoAte = agora + Random.Range(0.35f, 0.6f);
                    _rumo += Random.Range(1.0f, 2.6f) * (Random.value < 0.5f ? -1f : 1f);
                }
                else if (Random.value < _jeito.paradasPorSegundo * dt) _paradaAte = agora + Random.Range(0.4f, 1.0f);
            }
            bool arrancando = agora < _arrancoAte, parado = agora < _paradaAte;
            float cansado = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(Tempo / 30f));
            float alvo = parado ? 0f : _jeito.velocidade * cansado * (arrancando ? _jeito.arranco : 1f);
            _velocidade = Mathf.MoveTowards(_velocidade, alvo, (arrancando ? 8f : 2.5f) * dt);
            Arranco = Mathf.MoveTowards(Arranco, arrancando ? 1f : 0f, 5f * dt);
            _rumo += _giro * dt;
            // perto da beira, o rumo vira para dentro
            float beira = 1f - _jeito.tamanho;
            if (_peixe.magnitude > beira * 0.8f)
            {
                float paraDentro = Mathf.Atan2(-_peixe.y, -_peixe.x);
                float k = Mathf.InverseLerp(beira * 0.8f, beira, _peixe.magnitude);
                _rumo = Mathf.LerpAngle(_rumo * Mathf.Rad2Deg, paraDentro * Mathf.Rad2Deg, k * 4f * dt) * Mathf.Deg2Rad;
            }
            _peixe = DentroDoDisco(_peixe + new Vector2(Mathf.Cos(_rumo), Mathf.Sin(_rumo)) * (_velocidade * dt), beira);
        }

        static Vector2 DentroDoDisco(Vector2 p, float raio) => p.sqrMagnitude > raio * raio ? p.normalized * raio : p;
    }
}
