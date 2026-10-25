using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Movimento em primeira pessoa do jogador local: andar, correr (só para a frente), pular e olhar com o mouse ou
    /// o controle. Usa as ações do projeto (Player/Move, Look, Jump, Sprint). Só fica ligado no dono.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MovimentoPrimeiraPessoa : MonoBehaviour
    {
        public JogadorRede rede;
        [Tooltip("Pivô da câmera, na altura dos olhos.")]
        public Transform cabeca;

        [Header("Movimento")]
        public float velocidadeAndando = 2.0f;
        public float velocidadeCorrendo = 4.2f;
        public float velocidadeMirando = 1.3f;
        public float aceleracao = 10f;
        public float alturaPulo = 0.8f;
        public float gravidade = -20f;
        [Tooltip("Abaixo desta altura o jogador volta ao ponto de nascimento.")]
        public float alturaMinima = -20f;

        [Header("Olhar")]
        [Tooltip("Graus por pixel do mouse.")]
        public float sensibilidadeMouse = 0.08f;
        [Tooltip("Graus por segundo com o analógico no máximo.")]
        public float sensibilidadeControle = 160f;
        public float pitchMaximo = 85f;

        /// <summary>Quanto o olhar anda por movimento do mouse (a luneta deixa mais lento, na proporção do aumento).</summary>
        public float MultiplicadorDoOlhar { get; set; } = 1f;

        public bool Correndo { get; private set; }

        CharacterController _controle;
        InputAction _mover, _olhar, _pular, _correr;
        InventarioJogador _inventario;
        Vector3 _velocidade;
        float _yaw, _pitch, _coice;

        void Awake()
        {
            _controle = GetComponent<CharacterController>();
            var acoes = InputSystem.actions;
            _mover = acoes.FindAction("Player/Move", true);
            _olhar = acoes.FindAction("Player/Look", true);
            _pular = acoes.FindAction("Player/Jump", true);
            _correr = acoes.FindAction("Player/Sprint", true);
            _yaw = transform.eulerAngles.y;
        }

        public void Teleportar(Vector3 posicao, float yaw)
        {
            _controle.enabled = false;
            transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, yaw, 0f));
            _controle.enabled = true;
            _yaw = yaw;
            _velocidade = Vector3.zero;
        }

        /// <summary>Coice do tiro: a mira sobe um pouco e volta sozinha.</summary>
        public void Coice(float graus) => _coice += graus;

        // robô: o zumbi vivo mais perto (até 18 m), procurado duas vezes por segundo
        Transform _alvoDoRobo;
        float _proximaBusca;
        Transform AlvoDoRobo()
        {
            if (Time.time < _proximaBusca) return _alvoDoRobo != null && _alvoDoRobo.gameObject.activeInHierarchy ? _alvoDoRobo : null;
            _proximaBusca = Time.time + 0.5f;
            _alvoDoRobo = null;
            float menor = 18f * 18f;
            foreach (var z in FindObjectsByType<Zombies.Zumbi>())
            {
                if (z.Morto) continue;
                float d2 = (z.transform.position - transform.position).sqrMagnitude;
                if (d2 < menor) { menor = d2; _alvoDoRobo = z.transform; }
            }
            return _alvoDoRobo;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool bot = MenuOnline.Bot;
            // quem prende o mouse no jogo é o menu (MenuOnline.Controla)
            bool controla = bot || MenuOnline.Controla;

            Vector2 olhar = Vector2.zero, mover = Vector2.zero;
            bool correr = false, pular = false;
            if (bot)
            {
                var zumbi = AlvoDoRobo();
                if (zumbi != null)
                {
                    // teste automático com zumbi perto: mira no peito dele e recua devagar (a arma atira sozinha)
                    Vector3 d = zumbi.position + Vector3.up * 1.1f - cabeca.position;
                    float yawAlvo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    float pitchAlvo = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    float k = Mathf.Min(1f, 8f * dt);
                    olhar = new Vector2(Mathf.DeltaAngle(_yaw, yawAlvo) * k, -(pitchAlvo - _pitch) * k);
                    mover = new Vector2(0f, -0.4f);
                }
                else
                {
                    // teste automático: anda em círculo e olha em volta
                    mover = new Vector2(0f, 1f);
                    olhar = new Vector2(35f * dt, Mathf.Sin(Time.time * 0.7f) * 8f * dt);
                    correr = Mathf.Repeat(Time.time, 8f) > 6f;
                }
            }
            else if (controla)
            {
                Vector2 bruto = _olhar.ReadValue<Vector2>();
                olhar = _olhar.activeControl != null && _olhar.activeControl.device is Gamepad ? bruto * (sensibilidadeControle * dt) : bruto * sensibilidadeMouse;
                olhar *= MultiplicadorDoOlhar;
                mover = _mover.ReadValue<Vector2>();
                correr = _correr.IsPressed();
                pular = _pular.WasPressedThisFrame();
            }

            _yaw += olhar.x;
            _pitch = Mathf.Clamp(_pitch - olhar.y, -pitchMaximo, pitchMaximo);
            _coice = Mathf.Lerp(_coice, 0f, 1f - Mathf.Exp(-10f * dt));
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            float pitchFinal = Mathf.Clamp(_pitch - _coice, -pitchMaximo, pitchMaximo);
            cabeca.localRotation = Quaternion.Euler(pitchFinal, 0f, 0f);
            if (Mathf.Abs(rede.Pitch.Value - pitchFinal) > 0.5f) rede.Pitch.Value = pitchFinal;

            if (mover.sqrMagnitude > 1f) mover.Normalize();
            // com o pneu nas mãos (pesado): devagar, sem correr nem pular
            if (_inventario == null) _inventario = GetComponent<InventarioJogador>();
            bool pesado = _inventario != null && _inventario.CarregandoPneu;
            if (pesado) correr = pular = false;
            Correndo = correr && mover.y > 0.1f && !rede.Mirando.Value;
            float alvo = rede.Mirando.Value ? velocidadeMirando : Correndo ? velocidadeCorrendo : velocidadeAndando;
            if (pesado) alvo *= InventarioJogador.LentidaoDoPneu;
            Vector3 desejada = (transform.right * mover.x + transform.forward * mover.y) * alvo;
            Vector3 horizontal = new Vector3(_velocidade.x, 0f, _velocidade.z);
            horizontal = Vector3.Lerp(horizontal, desejada, 1f - Mathf.Exp(-(_controle.isGrounded ? aceleracao : aceleracao * 0.2f) * dt));
            if (_controle.isGrounded)
            {
                if (_velocidade.y < 0f) _velocidade.y = -2f;
                if (pular) _velocidade.y = Mathf.Sqrt(2f * alturaPulo * -gravidade);
            }
            _velocidade.y += gravidade * dt;
            _velocidade = new Vector3(horizontal.x, _velocidade.y, horizontal.z);
            _controle.Move(_velocidade * dt);

            // Caiu para fora do mapa: volta ao ponto de nascimento.
            if (transform.position.y < alturaMinima)
            {
                var ponto = Network.PontosDeNascimento.Ponto(rede.OwnerClientId);
                Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
            }
        }
    }
}
