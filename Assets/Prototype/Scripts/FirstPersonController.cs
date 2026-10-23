using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Movimento em primeira pessoa: WASD, Shift para correr, Espaço para pular e mouse para olhar.
    /// Também cuida do balanço da câmera, do coice (recoil) e do tremor ao levar dano.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Câmera")]
        public Transform cameraPivot;
        [Tooltip("Graus por pixel de movimento do mouse.")]
        public float mouseSensitivity = 0.08f;
        public float maxPitch = 85f;

        [Header("Movimento")]
        public float walkSpeed = 3.8f;
        public float sprintSpeed = 6.2f;
        public float acceleration = 12f;
        public float jumpHeight = 0.9f;
        public float gravity = -22f;

        [Header("Balanço da câmera")]
        public float headBobAmplitude = 0.035f;
        public float headBobFrequency = 1.7f;

        [Header("Segurança")]
        [Tooltip("Se cair mais que isto abaixo do último chão firme (ex.: empurrado através do piso), volta para ele.")]
        public float fallRecoveryDepth = 5f;

        public bool InputEnabled { get; set; } = true;
        /// <summary>Velocidade horizontal de 0 (parado) a 1 (correndo).</summary>
        public float Speed01 { get; private set; }
        public bool IsGrounded => _controller.isGrounded;
        public Vector2 LookDelta { get; private set; }
        public Vector3 BobOffset { get; private set; }
        /// <summary>True no quadro em que o clique só serviu para prender o cursor (não deve atirar).</summary>
        public bool JustLockedCursor { get; private set; }
        /// <summary>Pausado (começa pausado até o primeiro clique; ESC pausa de novo). O PrototypeGame congela o tempo.</summary>
        public bool IsPaused { get; private set; } = true;

        CharacterController _controller;
        Vector3 _velocity;
        float _yaw, _pitch, _recoil, _bobTime, _shake;
        Vector3 _pivotBase, _lastSafePosition;
        bool _hasSafePosition, _dead;
        float _deadTime;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            _pivotBase = cameraPivot.localPosition;
            SetCursorLocked(false);
        }

        public void Pause()
        {
            IsPaused = true;
            SetCursorLocked(false);
        }

        public void Resume()
        {
            IsPaused = false;
            SetCursorLocked(true);
        }

        /// <summary>Coloca o jogador numa posição (o CharacterController precisa ser desligado para isso).</summary>
        public void Teleport(Vector3 position)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            _controller.enabled = false;
            transform.position = position;
            _velocity = Vector3.zero;
            _controller.enabled = true;
            _lastSafePosition = position;
            _hasSafePosition = true;
        }

        /// <summary>Define para onde o jogador olha (yaw no corpo, pitch na câmera).</summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(_pitch - _recoil, 0f, 0f);
        }

        public void AddRecoil(float degrees) => _recoil += degrees;
        public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

        public void Die()
        {
            _dead = true;
            _deadTime = Time.time;
            InputEnabled = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.deltaTime;
            JustLockedCursor = false;
            if (keyboard == null || mouse == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame && !_dead) Pause();
            else if (mouse.leftButton.wasPressedThisFrame && (IsPaused || Cursor.lockState != CursorLockMode.Locked) && !_dead)
            {
                Resume();
                JustLockedCursor = true;
            }
            if (IsPaused) return;

            bool canLook = InputEnabled && Cursor.lockState == CursorLockMode.Locked;
            LookDelta = canLook ? mouse.delta.ReadValue() * mouseSensitivity : Vector2.zero;
            _yaw += LookDelta.x;
            _pitch = Mathf.Clamp(_pitch - LookDelta.y, -maxPitch, maxPitch);
            _recoil = Mathf.Lerp(_recoil, 0f, 1f - Mathf.Exp(-9f * dt));
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            Vector2 input = Vector2.zero;
            if (InputEnabled)
            {
                input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                if (input.sqrMagnitude > 1f) input.Normalize();
            }
            bool sprinting = InputEnabled && keyboard.leftShiftKey.isPressed && input.y > 0.1f;
            float targetSpeed = sprinting ? sprintSpeed : walkSpeed;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            float accel = _controller.isGrounded ? acceleration : acceleration * 0.25f;
            horizontal = Vector3.Lerp(horizontal, wish, 1f - Mathf.Exp(-accel * dt));

            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f) _velocity.y = -2f;
                if (InputEnabled && keyboard.spaceKey.wasPressedThisFrame) _velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            _velocity.y += gravity * dt;
            _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);
            _controller.Move(_velocity * dt);

            if (_controller.isGrounded)
            {
                _lastSafePosition = transform.position;
                _hasSafePosition = true;
            }
            else if (_hasSafePosition && transform.position.y < _lastSafePosition.y - fallRecoveryDepth)
                Teleport(_lastSafePosition);

            float speed = horizontal.magnitude;
            Speed01 = Mathf.Clamp01(speed / sprintSpeed);
            UpdateCamera(speed, dt);
        }

        void UpdateCamera(float speed, float dt)
        {
            if (_controller.isGrounded && speed > 0.3f) _bobTime += dt * speed * headBobFrequency;
            float strength = Mathf.Clamp01(speed / walkSpeed);
            BobOffset = new Vector3(Mathf.Cos(_bobTime) * headBobAmplitude * 0.6f, Mathf.Abs(Mathf.Sin(_bobTime)) * headBobAmplitude, 0f) * strength;

            _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f);
            Vector3 shake = Random.insideUnitSphere * (_shake * 0.05f);

            Vector3 targetPosition = _pivotBase + BobOffset + shake;
            float roll = 0f;
            if (_dead)
            {
                // Ao morrer, a câmera cai até o chão e tomba de lado.
                float t = Mathf.Clamp01((Time.time - _deadTime) / 0.8f);
                targetPosition = Vector3.Lerp(_pivotBase, new Vector3(0f, 0.25f, 0f), t * t);
                roll = 70f * t;
            }
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetPosition, 1f - Mathf.Exp(-14f * dt));
            cameraPivot.localRotation = Quaternion.Euler(_pitch - _recoil, 0f, roll);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
