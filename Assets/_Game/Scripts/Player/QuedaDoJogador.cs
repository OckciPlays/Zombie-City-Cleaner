using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A queda do jogador (05/11/2026, a pedido: "entorse de tornozelo, fratura e fratura exposta, a depender da altura";
    /// o sistema de vida por membro, a etapa 1). Só no dono, que é quem move o corpo (o CharacterController do cliente não
    /// se move no anfitrião): mede a queda no pouso e manda a altura que valeu ao anfitrião
    /// (<see cref="VidaJogador.PedirQueda"/>), que confere e sorteia a lesão (<see cref="VidaJogador.Cair"/>).
    /// <para>
    /// A medida, no LateUpdate (depois do movimento, do pulo da janela, da câmera de terceira pessoa e do carro): no ar,
    /// o ponto mais alto e a velocidade de descida dos 3 últimos quadros (o quadro do pouso vem cortado pelo Move); no
    /// pouso, a altura pela velocidade (v² / 2g) e a de verdade (do ponto mais alto até o chão), a menor das duas (com
    /// meio metro de folga na de verdade): escorregar numa encosta conta menos, e um teleporte para baixo que escapar não
    /// conta. A água amortece (o jogador pousa no fundo: com 1,5 m de água, até 20 m não machucam). De 3 m para cima, o
    /// tranco na câmera, o baque e o pedido ao anfitrião.
    /// </para>
    /// <para>
    /// Só arma depois de 0,4 s seguidos no chão firme, e desarma com o movimento desligado (o carro, a janela, o vídeo), o
    /// voo, caído, o cinema, o Dia 0 (<see cref="VidaJogador.CorpoAnda"/>) e o teleporte (o salto de posição que não bate
    /// com a velocidade: nascer, renascer na cama, sair do carro, o save, o F1). Assim a queda que começa num teleporte
    /// (para o ar, para um andar ainda sem colisão, saindo do voo) nunca conta. O teste do F1 que teleporta para o alto
    /// arma na hora (<see cref="ArmarJa"/>).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class QuedaDoJogador : MonoBehaviour
    {
        /// <summary>O detector do jogador desta máquina.</summary>
        public static QuedaDoJogador Local { get; private set; }

        /// <summary>A partir desta altura (m), a queda machuca.</summary>
        public const float AlturaQueMachuca = 3f;

        const float TempoParaArmar = 0.4f;     // no chão firme, seguidos (s)
        const float TempoMinimoNoAr = 0.15f;   // menos que isso não é queda (os degraus da escada)
        const float SaltoDeTeleporte = 1f;     // a diferença entre o que andou e o que a velocidade explica (m)

        VidaJogador _vida;
        MovimentoPrimeiraPessoa _mov;
        PuloDaJanela _janela;
        PassageiroDoCarro _passageiro;
        CharacterController _cc;

        bool _armado, _noAr, _temPosicao;
        float _noChaoDesde = -1f, _arDesde, _picoY;
        // a maior subida desta fase no ar (m/s): bem acima do pulo (5,7), foi o tornado que jogou para cima
        float _subida;
        const float SubidaDoArremesso = 8f, AlturaDoArremesso = 4.4f;
        // as velocidades de descida dos 3 últimos quadros no ar
        readonly float[] _descida = new float[3];
        int _quadroDaDescida;
        Vector3 _posAnterior;
        // o ArmarJa: os quadros em que o salto de posição não desarma (o teleporte do próprio teste)
        int _semConferirSalto;

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _mov = GetComponent<MovimentoPrimeiraPessoa>();
            _janela = GetComponent<PuloDaJanela>();
            _passageiro = GetComponent<PassageiroDoCarro>();
            _cc = GetComponent<CharacterController>();
            Local = this;
        }

        void OnDestroy()
        {
            if (Local == this) Local = null;
        }

        /// <summary>
        /// Arma o detector agora, mesmo no ar (o teste do F1: teleporta para o alto e solta). A queda conta do ponto onde ele
        /// estiver no próximo quadro.
        /// </summary>
        public void ArmarJa()
        {
            _armado = true;
            _noAr = false;
            _noChaoDesde = -1f;
            LimparDescida();
            _posAnterior = transform.position;
            _temPosicao = true;
            _semConferirSalto = 2;
        }

        void Desarmar()
        {
            _armado = false;
            _noAr = false;
            _noChaoDesde = -1f;
            LimparDescida();
        }

        void LimparDescida()
        {
            for (int i = 0; i < _descida.Length; i++) _descida[i] = 0f;
            _quadroDaDescida = 0;
        }

        void LateUpdate()
        {
            if (_vida == null || _mov == null || _cc == null || !_vida.IsSpawned || !_vida.IsOwner) return;
            Vector3 p = transform.position;
            float dt = Time.deltaTime;
            if (!_temPosicao)
            {
                _posAnterior = p;
                _temPosicao = true;
            }

            // 1. o que não conta: desarma (e esquece o voo)
            bool ignora = !_mov.enabled || !_cc.enabled || _mov.Voando
                          || (_janela != null && _janela.Pulando)
                          || (_passageiro != null && _passageiro.NoCarro)
                          || _vida.Caido.Value || MenuOnline.Cinema || !VidaJogador.CorpoAnda;
            if (ignora)
            {
                Desarmar();
                _posAnterior = p;
                return;
            }

            // 2. o teleporte: andou bem mais do que a velocidade explica
            if (_semConferirSalto > 0) _semConferirSalto--;
            else if ((p - _posAnterior - _cc.velocity * dt).magnitude > SaltoDeTeleporte) Desarmar();
            _posAnterior = p;

            if (_cc.isGrounded)
            {
                // 3. no chão: vinha do ar, armado e por tempo bastante, é um pouso
                if (_noAr)
                {
                    _noAr = false;
                    if (_armado && Time.time - _arDesde >= TempoMinimoNoAr) Pousou(p);
                    LimparDescida();
                }
                if (_noChaoDesde < 0f) _noChaoDesde = Time.time;
                if (!_armado && Time.time - _noChaoDesde >= TempoParaArmar) _armado = true;
                return;
            }

            // 4. no ar: o ponto mais alto e as velocidades de descida
            _noChaoDesde = -1f;
            if (!_noAr)
            {
                _noAr = true;
                _arDesde = Time.time;
                _picoY = p.y;
                _subida = 0f;
                LimparDescida();
            }
            else _picoY = Mathf.Max(_picoY, p.y);
            _subida = Mathf.Max(_subida, _cc.velocity.y);
            _descida[_quadroDaDescida] = Mathf.Max(0f, -_cc.velocity.y);
            _quadroDaDescida = (_quadroDaDescida + 1) % _descida.Length;
        }

        // o pouso: a altura que valeu (veja a classe) e, de 3 m para cima, o tranco, o baque e o pedido ao anfitrião
        void Pousou(Vector3 p)
        {
            float v = 0f;
            for (int i = 0; i < _descida.Length; i++) v = Mathf.Max(v, _descida[i]);
            float g = Mathf.Abs(_mov.gravidade);
            if (g < 0.1f) g = 20f;
            float hVel = v * v / (2f * g);
            float hGeo = Mathf.Max(0f, _picoY - p.y);
            float h = Mathf.Min(hVel, hGeo + 0.5f);
            // a água amortece (sem colisão: ele pousa no fundo)
            float fundura = FunduraDaAgua(p);
            if (fundura > 0f) h = Mathf.Max(h * (1f - Mathf.Clamp01(fundura / 1.5f)), h - 20f);
            // o arremesso do tornado: no máximo a faixa da entorse (ele já tira vida a cada 2,5 s)
            if (_subida > SubidaDoArremesso) h = Mathf.Min(h, AlturaDoArremesso);
            if (float.IsNaN(h) || h < AlturaQueMachuca) return;
            _mov.Coice(-Mathf.Min(8f, h));   // a cabeça bate para baixo
            // (21/11/2026, a pedido: o baque gravado da queda tinha ruído e saiu; até achar outro, a queda fica só com o gemido.
            // Um Corpo/baque_ novo volta a tocar aqui sozinho)
            Efeitos.Som(SonsGravados.Um("Corpo", "baque_"), p + Vector3.up * 0.3f, Mathf.Clamp(0.55f + h * 0.04f, 0.55f, 1f), false);
            GetComponent<JogadorRede>()?.Voz(TipoDeVoz.Queda, 0.85f);   // (19/11/2026: o gemido da queda; a fratura, a dor)
            _vida.PedirQueda(h);
        }

        /// <summary>
        /// A água acima dos pés, no ponto (0 em terra): o lago do mundo sorteado, ou o lago e os rios do Lakewood.
        /// </summary>
        static float FunduraDaAgua(Vector3 p)
        {
            var mundo = MundoDaPartida.Instancia;
            if (mundo != null && mundo.Dados != null)
            {
                var dados = mundo.Dados;
                float f = dados.FunduraDaAgua(p.x, p.z);
                if (f <= 0f) return 0f;
                // o nível da água (o fundo mais a fundura) acima dos pés: em cima de alguma coisa, a água fica embaixo
                return Mathf.Clamp(dados.Altura(p.x, p.z) + f - p.y, 0f, f);
            }
            var mapa = LakewoodNaCena.Instancia;
            if (mapa != null && mapa.ChaoNoMundo != null && mapa.ChaoNoMundo.Agua(p.x, p.z, out float nivel)) return Mathf.Max(0f, nivel - p.y);
            return 0f;
        }
    }
}
