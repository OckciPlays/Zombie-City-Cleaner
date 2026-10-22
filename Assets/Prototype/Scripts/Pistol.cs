using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Pistola semiautomática: clique para atirar, R para recarregar. O tiro é um raycast do centro
    /// da tela, com uma dispersão pequena que cresce ao andar e ao atirar rápido.
    /// </summary>
    public class Pistol : MonoBehaviour
    {
        [Header("Referências")]
        public Camera aimCamera;
        public FirstPersonController controller;
        public PlayerHealth health;
        public Transform viewModel;
        public Light muzzleLight;
        public Renderer muzzleFlash;

        [Header("Arma")]
        public int magazineSize = 12;
        public float fireInterval = 0.16f;
        public float reloadTime = 1.3f;
        public float range = 60f;
        public float recoilDegrees = 2.2f;

        [Header("Precisão (graus)")]
        public float baseSpread = 0.15f;
        public float moveSpread = 1.4f;
        public float shotBloom = 0.9f;
        public float maxBloom = 2.5f;

        /// <summary>Avisa os zumbis que um tiro foi disparado nesta posição (eles escutam).</summary>
        public static event Action<Vector3> ShotFired;

        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public float CurrentSpread => baseSpread + moveSpread * controller.Speed01 + _bloom;
        public float LastHitTime { get; private set; } = -999f;
        public bool LastHitWasHead { get; private set; }

        const int IgnoreRaycastMask = 1 << 2;

        AudioSource _audio;
        float _nextFire, _bloom, _flashUntil, _kickRotation, _reloadDip;
        Vector3 _viewModelBase, _kick, _sway;
        Quaternion _viewModelBaseRotation;

        void Start()
        {
            Ammo = magazineSize;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _viewModelBase = viewModel.localPosition;
            _viewModelBaseRotation = viewModel.localRotation;
            SetFlash(false);
        }

        void Update()
        {
            _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime * 3f);
            if (Time.time > _flashUntil) SetFlash(false);
            if (health != null && health.IsDead) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (keyboard.rKey.wasPressedThisFrame && !IsReloading && Ammo < magazineSize) StartCoroutine(Reload());
            if (locked && !controller.JustLockedCursor && mouse.leftButton.wasPressedThisFrame) TryFire();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _kick = Vector3.Lerp(_kick, Vector3.zero, 1f - Mathf.Exp(-16f * dt));
            _kickRotation = Mathf.Lerp(_kickRotation, 0f, 1f - Mathf.Exp(-14f * dt));
            Vector3 swayTarget = new Vector3(-controller.LookDelta.x, -controller.LookDelta.y, 0f) * 0.004f;
            swayTarget = Vector3.ClampMagnitude(swayTarget, 0.04f);
            _sway = Vector3.Lerp(_sway, swayTarget, 1f - Mathf.Exp(-10f * dt));
            _reloadDip = Mathf.MoveTowards(_reloadDip, IsReloading ? 1f : 0f, dt * 5f);

            viewModel.localPosition = _viewModelBase + _sway + controller.BobOffset * 0.5f + _kick + new Vector3(0f, -0.12f, 0f) * _reloadDip;
            viewModel.localRotation = _viewModelBaseRotation * Quaternion.Euler(-_kickRotation - 35f * _reloadDip, 0f, 20f * _reloadDip);
        }

        /// <summary>Atira na direção da mira (respeita cadência, munição e recarga).</summary>
        public void TryFire()
        {
            if (IsReloading || Time.time < _nextFire) return;
            if (Ammo <= 0)
            {
                _audio.PlayOneShot(Effects.DryFire, 0.8f);
                _nextFire = Time.time + 0.25f;
                StartCoroutine(Reload());
                return;
            }

            Ammo--;
            _nextFire = Time.time + fireInterval;
            _audio.PlayOneShot(Effects.Gunshot, 0.9f);
            controller.AddRecoil(recoilDegrees);
            _kick += new Vector3(0f, 0.01f, -0.06f);
            _kickRotation += 12f;
            _flashUntil = Time.time + 0.05f;
            SetFlash(true);
            if (muzzleFlash != null) muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            Vector3 direction = SpreadDirection(aimCamera.transform, CurrentSpread);
            _bloom = Mathf.Min(maxBloom, _bloom + shotBloom);
            ShotFired?.Invoke(aimCamera.transform.position);

            if (!Physics.Raycast(aimCamera.transform.position, direction, out RaycastHit hit, range, ~IgnoreRaycastMask, QueryTriggerInteraction.Collide))
                return;

            var hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null && hitbox.owner != null && !hitbox.owner.IsDead)
            {
                LastHitTime = Time.time;
                LastHitWasHead = hitbox.isHead;
                hitbox.owner.TakeHit(hitbox.isHead, hit.point, direction);
            }
            else if (hitbox == null)
            {
                // Faísca e poeira no cenário.
                Effects.Burst(hit.point, hit.normal, new Color(1f, 0.85f, 0.4f), 6, 3.5f, 0.03f, 0.6f, 0.25f, 45f);
                Effects.Burst(hit.point, hit.normal, new Color(0.45f, 0.42f, 0.38f), 8, 1.2f, 0.06f, 0.3f, 0.7f, 30f);
            }
        }

        IEnumerator Reload()
        {
            if (IsReloading) yield break;
            IsReloading = true;
            _audio.PlayOneShot(Effects.Click, 0.7f);
            yield return new WaitForSeconds(reloadTime * 0.6f);
            _audio.PlayOneShot(Effects.Click, 0.9f);
            yield return new WaitForSeconds(reloadTime * 0.4f);
            Ammo = magazineSize;
            IsReloading = false;
        }

        static Vector3 SpreadDirection(Transform aim, float spreadDegrees)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            return (aim.forward + aim.right * offset.x + aim.up * offset.y).normalized;
        }

        void SetFlash(bool on)
        {
            if (muzzleLight != null) muzzleLight.enabled = on;
            if (muzzleFlash != null) muzzleFlash.enabled = on;
        }
    }
}
