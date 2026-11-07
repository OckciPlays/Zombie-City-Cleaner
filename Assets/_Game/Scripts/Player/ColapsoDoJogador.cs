using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O colapso no dono (06/11/2026, a sanidade, a etapa 2: "fuga em pânico por alguns segundos (max 6s), desmaio"):
    /// <para>A fuga (<see cref="Fugir"/>): o jogador perde o controle e corre para a direção dada (a de costas para o que ele
    /// via), virando quando bate numa parede; o movimento e a arma ficam desligados, e no fim o olhar fica onde a fuga
    /// parou. Outros jogadores veem ele correndo (o NetworkTransform).</para>
    /// <para>O desmaio (o <see cref="VidaJogador.Desmaiado"/>, do anfitrião): o baque e o zumbido (o
    /// <see cref="SomDaMente.Desmaio"/>), a tela apaga em 0,8 s, o ouvido tampa (um passa-baixa no ouvido do jogador) e o
    /// controle sai; acordando, a tela clareia e o ouvido abre devagar.</para>
    /// </summary>
    public class ColapsoDoJogador : MonoBehaviour
    {
        public const float FugaMaxima = 6f;

        public static ColapsoDoJogador Local { get; private set; }

        /// <summary>Correndo em pânico agora.</summary>
        public bool Fugindo => _fugindo;
        /// <summary>Sem o controle agora (fugindo ou desmaiado).</summary>
        public bool SemControle => _semControle;

        VidaJogador _vida;
        JogadorRede _rede;
        CharacterController _cc;
        MovimentoPrimeiraPessoa _mov;
        ArmaJogador _arma;
        AudioLowPassFilter _ouvido;
        bool _fugindo, _semControle, _estavaDesmaiado, _movEstava, _armaEstava, _estavaDormindo;
        float _proximaPiscada, _piscada;
        UnityEngine.InputSystem.InputAction _interagir;
        float _fugaAte, _quedaY, _escuro, _proximaVirada;
        Vector3 _direcao;

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
            _cc = GetComponent<CharacterController>();
            _mov = GetComponent<MovimentoPrimeiraPessoa>();
            _arma = GetComponent<ArmaJogador>();
            _interagir = UnityEngine.InputSystem.InputSystem.actions != null ? UnityEngine.InputSystem.InputSystem.actions.FindAction("Player/Interact", false) : null;
        }

        void OnEnable() => Local = this;

        void OnDisable()
        {
            if (Local == this) Local = null;
            DevolverOControle();
            if (_ouvido != null) _ouvido.cutoffFrequency = 22000f;
        }

        bool NoCarro => _rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro;

        /// <summary>Foge em pânico para esta direção (no máximo <see cref="FugaMaxima"/> s). False se não dá agora.</summary>
        public bool Fugir(Vector3 direcao, float segundos)
        {
            if (_fugindo || _vida == null || !_vida.IsSpawned || _vida.Caido.Value || _vida.Desmaiado.Value || NoCarro) return false;
            // (sem o controle por outro motivo, como no meio do pulo da janela: não foge)
            if (_mov == null || _cc == null || !_cc.enabled || !_mov.enabled) return false;
            direcao.y = 0f;
            if (direcao.sqrMagnitude < 0.01f) direcao = -transform.forward;
            _direcao = direcao.normalized;
            _fugaAte = Time.time + Mathf.Clamp(segundos, 1f, FugaMaxima);
            _fugindo = true;
            _quedaY = 0f;
            TirarOControle();
            Tratamento.Local?.Cancelar("O pânico interrompeu o tratamento");
            return true;
        }

        void TirarOControle()
        {
            if (_semControle) return;
            _semControle = true;
            // o que estava ligado volta ligado no fim (o que outro desligou fica com ele)
            _movEstava = _mov != null && _mov.enabled;
            _armaEstava = _arma != null && _arma.enabled;
            if (_mov != null) _mov.enabled = false;
            if (_arma != null) _arma.enabled = false;
        }

        void DevolverOControle()
        {
            if (!_semControle) return;
            _semControle = false;
            if (_mov != null && _movEstava)
            {
                _mov.enabled = true;
                // o olhar continua de onde a fuga deixou (o giro do corpo) e na altura de antes
                float pitch = _rede != null && _rede.IsSpawned ? _rede.Pitch.Value : 0f;
                _mov.Olhar(transform.eulerAngles.y, pitch);
            }
            if (_arma != null && _armaEstava) _arma.enabled = true;
        }

        void Update()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            float dt = Time.deltaTime;
            bool desmaiado = _vida.Desmaiado.Value;
            if (desmaiado != _estavaDesmaiado)
            {
                _estavaDesmaiado = desmaiado;
                if (desmaiado)
                {
                    Efeitos.Som(SomDaMente.Desmaio, transform.position + Vector3.up * 1.6f, 0.8f, false);
                    _fugindo = false;
                    TirarOControle();
                    Tratamento.Local?.Cancelar();
                }
                else if (!_fugindo) DevolverOControle();
            }
            // a etapa 3: dormindo (a cama, ou o calmante), também sem o controle; o E levanta
            bool dormindo = _vida.Dormindo.Value;
            if (dormindo != _estavaDormindo)
            {
                _estavaDormindo = dormindo;
                if (dormindo)
                {
                    if (_vida.TemEfeito(VidaJogador.EfeitoSonoForcado)) Efeitos.Som(SomDaMente.Desmaio, transform.position + Vector3.up * 1.6f, 0.7f, false);
                    _fugindo = false;
                    TirarOControle();
                    Tratamento.Local?.Cancelar();
                }
                else if (!_fugindo && !desmaiado) DevolverOControle();
            }
            if (dormindo && _escuro > 0.9f && _interagir != null && _interagir.WasPressedThisFrame()) _vida.PedirAcordar();
            // exausto: a vista pisca (fecha um instante) de vez em quando
            if (!dormindo && !desmaiado && _vida.TemEfeito(VidaJogador.EfeitoExausto) && Time.time >= _proximaPiscada)
            {
                _proximaPiscada = Time.time + Random.Range(6f, 12f);
                _piscada = 1f;
            }
            _piscada = Mathf.MoveTowards(_piscada, 0f, dt * 3.2f);
            if (_fugindo) Correr(dt);
            // caído (a tela de morte cuida) ou no carro: nada daqui
            if (_vida.Caido.Value && _semControle && !desmaiado && !dormindo)
            {
                _fugindo = false;
                DevolverOControle();
            }
            // a tela e o ouvido
            bool apagado = desmaiado || dormindo;
            _escuro = Mathf.MoveTowards(_escuro, apagado ? 1f : 0f, dt * (desmaiado ? 1.25f : apagado ? 0.6f : 0.45f));
            if (_ouvido == null && _rede != null && _rede.ouvido != null)
            {
                _ouvido = _rede.ouvido.GetComponent<AudioLowPassFilter>();
                if (_ouvido == null) _ouvido = _rede.ouvido.gameObject.AddComponent<AudioLowPassFilter>();
                _ouvido.cutoffFrequency = 22000f;
            }
            if (_ouvido != null)
            {
                // (dormindo, o ouvido não tampa tanto: dá para ouvir o zumbi chegando)
                float corte = Mathf.Lerp(22000f, dormindo ? 1400f : 420f, Mathf.SmoothStep(0f, 1f, _escuro));
                _ouvido.cutoffFrequency = corte;
                _ouvido.enabled = _escuro > 0.001f;
            }
        }

        void Correr(float dt)
        {
            if (Time.time >= _fugaAte || _vida.Caido.Value || NoCarro || !_cc.enabled)
            {
                _fugindo = false;
                if (!_vida.Desmaiado.Value && !_vida.Dormindo.Value) DevolverOControle();
                return;
            }
            // vira para a direção da fuga e corre (com a gravidade)
            float alvo = Mathf.Atan2(_direcao.x, _direcao.z) * Mathf.Rad2Deg;
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, alvo, 300f * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _quedaY = _cc.isGrounded ? -2f : _quedaY - 20f * dt;
            float velocidade = Mathf.Max(_mov.velocidadeAndando, _mov.velocidadeCorrendo) * 0.95f;
            var bateu = _cc.Move((transform.forward * velocidade + Vector3.up * _quedaY) * dt);
            // bateu de lado (a parede, o carro): vira para um lado e segue
            if ((bateu & CollisionFlags.Sides) != 0 && Time.time >= _proximaVirada)
            {
                _proximaVirada = Time.time + 0.4f;
                _direcao = Quaternion.Euler(0f, Random.Range(80f, 140f) * (Random.value < 0.5f ? 1f : -1f), 0f) * _direcao;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || ModoVideo.Ligado) return;
            float escuro = Mathf.Max(Mathf.SmoothStep(0f, 0.97f, _escuro), Mathf.Sin(_piscada * Mathf.PI) * 0.92f);
            if (escuro < 0.005f) return;
            var antes = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, escuro);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = antes;
            if (_vida == null || _escuro < 0.6f) return;
            bool dormindo = _vida.Dormindo.Value;
            if (!_vida.Desmaiado.Value && !dormindo) return;
            float u = Screen.height / 720f, a = (_escuro - 0.6f) / 0.4f;
            string titulo, linha;
            if (dormindo)
            {
                bool forcado = _vida.TemEfeito(VidaJogador.EfeitoSonoForcado);
                titulo = forcado ? "Apagou…" : "Dormindo…";
                string relogio = World.CicloDoDia.Relogio();
                linha = World.CicloDoDia.Pressa > 1.01f ? $"{relogio}  ·  todos dormindo: a noite passa"
                      : forcado ? $"{relogio}  ·  um amigo pode te acordar"
                      : $"{relogio}  ·  [E] levantar  ·  com todos dormindo, a noite passa";
            }
            else
            {
                titulo = "Desmaiado…";
                linha = "um amigo pode te acordar";
            }
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Bold, TextAnchor.MiddleCenter);
            estilo.normal.textColor = new Color(0.7f, 0.66f, 0.66f, a * 0.8f);
            GUI.Label(new Rect(0f, Screen.height * 0.5f - 12f * u, Screen.width, 24f * u), titulo, estilo);
            var menor = EstilosDoHud.Rotulo(Mathf.RoundToInt(11 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
            menor.normal.textColor = new Color(0.55f, 0.52f, 0.52f, a * 0.7f);
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 12f * u, Screen.width, 20f * u), linha, menor);
        }
    }
}
