using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Lanterna que segue a câmera com um leve atraso (dá peso à mão) e liga/desliga com F.
    /// De vez em quando ela pisca sozinha, para dar tensão.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        public Light spotLight;
        public Transform follow;
        [Tooltip("Posição em relação à câmera. Fica à frente do cano da arma para não estourar ela de luz.")]
        public Vector3 offset = new Vector3(0.09f, -0.12f, 0.95f);
        public float lag = 12f;
        public bool startOn = true;

        [Header("Falhas")]
        public float flickerChancePerSecond = 0.06f;

        public bool IsOn { get; private set; }

        const int IgnoreRaycastMask = 1 << 2;
        const float WallMargin = 0.15f;

        float _baseIntensity;
        float _flickerUntil;

        void Start()
        {
            _baseIntensity = spotLight.intensity;
            IsOn = startOn;
            spotLight.enabled = IsOn;
            transform.SetPositionAndRotation(OffsetPosition(), follow.rotation);
        }

        // Encostado numa parede, a luz recua junto com ela (senão iluminaria o outro lado).
        Vector3 OffsetPosition()
        {
            Vector3 target = follow.TransformPoint(offset);
            Vector3 toTarget = target - follow.position;
            float distance = toTarget.magnitude;
            if (Physics.Raycast(follow.position, toTarget / distance, out RaycastHit hit, distance + WallMargin, ~IgnoreRaycastMask, QueryTriggerInteraction.Ignore))
                return follow.position + toTarget * (Mathf.Max(0f, hit.distance - WallMargin) / distance);
            return target;
        }

        void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                IsOn = !IsOn;
                spotLight.enabled = IsOn;
                Effects.PlayAt(Effects.Click, transform.position, 0.6f, 1.4f);
            }

            transform.position = OffsetPosition();
            transform.rotation = Quaternion.Slerp(transform.rotation, follow.rotation, 1f - Mathf.Exp(-lag * Time.deltaTime));

            if (!IsOn) return;
            if (Time.time > _flickerUntil && Random.value < flickerChancePerSecond * Time.deltaTime)
                _flickerUntil = Time.time + Random.Range(0.15f, 0.5f);
            spotLight.intensity = Time.time < _flickerUntil
                ? _baseIntensity * (Random.value < 0.5f ? 0.15f : 0.8f)
                : _baseIntensity;
        }
    }
}
