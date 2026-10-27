using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Para andar pelas cenas de exemplo das casas (a rua e a sala de medidas) em primeira pessoa, com as medidas do
    /// jogador (CharacterController de 1,80 m e raio 0,3; o olho a 1,62 m; andar 2 m/s, correr 4,2 m/s; a câmera com 70°
    /// de campo). Sem rede. WASD anda, Shift corre, Espaço pula, V voa (Espaço e Ctrl sobem e descem), R gera de novo
    /// com uma semente nova. O clique prende o mouse e o Esc solta. A tela mostra o texto da cena (na sala de medidas,
    /// as medidas da casa em que o jogador está).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AndarNoExemplo : MonoBehaviour
    {
        public Transform cabeca;
        public float andar = 2f, correr = 4.2f, voar = 10f, sensibilidade = 0.08f;

        CharacterController _cc;
        float _giro, _inclinacao, _vy;
        bool _voando;
        CenaDeExemplo _rua;

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            _giro = transform.eulerAngles.y;
            _rua = FindAnyObjectByType<CenaDeExemplo>();
        }

        void Update()
        {
            var k = Keyboard.current;
            var mouse = Mouse.current;
            if (k == null || mouse == null) return;
            float dt = Time.deltaTime;
            if (mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (k.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * sensibilidade;
                _giro += d.x;
                _inclinacao = Mathf.Clamp(_inclinacao - d.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _giro, 0f);
            cabeca.localRotation = Quaternion.Euler(_inclinacao, 0f, 0f);

            if (k.rKey.wasPressedThisFrame && _rua != null)
            {
                _rua.semente = Random.Range(1, 100000);
                _rua.Gerar();
            }
            if (k.vKey.wasPressedThisFrame)
            {
                _voando = !_voando;
                _cc.enabled = !_voando;
                _vy = 0f;
            }

            var mv = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            if (_voando)
            {
                Vector3 dirV = cabeca.forward * mv.y + cabeca.right * mv.x;
                if (k.spaceKey.isPressed) dirV += Vector3.up;
                if (k.leftCtrlKey.isPressed) dirV -= Vector3.up;
                transform.position += dirV * voar * (k.leftShiftKey.isPressed ? 3f : 1f) * dt;
                return;
            }
            Vector3 dir = transform.forward * mv.y + transform.right * mv.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            float vel = k.leftShiftKey.isPressed ? correr : andar;
            if (_cc.isGrounded)
            {
                _vy = -1f;
                if (k.spaceKey.wasPressedThisFrame) _vy = 4.5f;
            }
            else _vy -= 9.81f * dt;
            _cc.Move((dir * vel + Vector3.up * _vy) * dt);
            if (transform.position.y < -20f) { _cc.enabled = false; transform.position = new Vector3(transform.position.x, 2f, -2f); _cc.enabled = true; }
        }

        void OnGUI()
        {
            const string ajuda = "WASD andar · Shift correr · Espaço pular · V voar (Espaço/Ctrl sobem e descem) · R gerar de novo (semente nova) · clique prende o mouse, Esc solta";
            GUI.Label(new Rect(12, 8, 1400, 22), ajuda);
            if (_rua != null) GUI.Label(new Rect(12, 28, 1200, 400), _rua.Texto(transform.position));
        }
    }
}
