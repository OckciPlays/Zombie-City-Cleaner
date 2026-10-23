using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Arma de fogo em primeira pessoa (SCAR, pistola...). Clique atira (segurar, se for automática) e R recarrega.
    /// O tiro é um raycast do centro da tela, com dispersão que cresce ao andar e ao atirar rápido.
    /// A troca de arma é feita pelo WeaponHolder, ligando e desligando este componente.
    /// </summary>
    public class Gun : MonoBehaviour
    {
        [Header("Referências")]
        public Camera aimCamera;
        public FirstPersonController controller;
        public PlayerHealth health;
        public Transform viewModel;
        public Light muzzleLight;
        public Renderer muzzleFlash;

        [Header("Arma")]
        public string weaponName = "PISTOLA";
        public bool automatic;
        public int magazineSize = 12;
        public float fireInterval = 0.16f;
        public float reloadTime = 1.3f;
        public float range = 60f;
        public float recoilDegrees = 2f;
        public float kickBack = 0.06f;
        public float kickRotation = 12f;
        public float soundPitch = 1f;
        public float equipTime = 0.35f;

        [Header("Precisão (graus)")]
        public float baseSpread = 0.1f;
        public float moveSpread = 0.6f;
        public float shotBloom = 0.4f;
        public float maxBloom = 1.5f;

        /// <summary>Avisa os zumbis que um tiro foi disparado nesta posição (eles escutam).</summary>
        public static event Action<Vector3> ShotFired;

        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public float CurrentSpread => baseSpread + moveSpread * controller.Speed01 + _bloom;
        public float LastHitTime { get; private set; } = -999f;
        public bool LastHitWasHead { get; private set; }

        const int IgnoreRaycastMask = 1 << 2;
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        AudioSource _audio;
        float _nextFire, _bloom, _flashUntil, _kickRotation, _reloadDip, _equipStart;
        Vector3 _viewModelBase, _kick, _sway;
        Quaternion _viewModelBaseRotation;

        void Awake()
        {
            Ammo = magazineSize;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _viewModelBase = viewModel.localPosition;
            _viewModelBaseRotation = viewModel.localRotation;
            SetFlash(false);
        }

        void OnEnable()
        {
            _equipStart = Time.time;
            viewModel.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            IsReloading = false;
            SetFlash(false);
            if (viewModel != null) viewModel.gameObject.SetActive(false);
        }

        void Update()
        {
            _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime * 3f);
            if (Time.time > _flashUntil) SetFlash(false);
            if (health != null && health.IsDead) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || controller.IsPaused) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (keyboard.rKey.wasPressedThisFrame && !IsReloading && Ammo < magazineSize) StartCoroutine(Reload());
            bool trigger = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (locked && !controller.JustLockedCursor && trigger) TryFire();
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
            float equip = 1f - Mathf.Clamp01((Time.time - _equipStart) / equipTime);   // a arma sobe ao ser sacada

            viewModel.localPosition = _viewModelBase + _sway + controller.BobOffset * 0.5f + _kick
                                      + new Vector3(0f, -0.12f, 0f) * _reloadDip + new Vector3(0f, -0.25f, 0f) * equip * equip;
            viewModel.localRotation = _viewModelBaseRotation * Quaternion.Euler(-_kickRotation - 35f * _reloadDip + 30f * equip, 0f, 20f * _reloadDip);
        }

        /// <summary>Atira na direção da mira (respeita cadência, munição, recarga e o tempo de sacar).</summary>
        public void TryFire()
        {
            if (IsReloading || Time.time < _nextFire || Time.time - _equipStart < equipTime) return;
            if (Ammo <= 0)
            {
                _audio.PlayOneShot(Effects.DryFire, 0.8f);
                _nextFire = Time.time + 0.25f;
                StartCoroutine(Reload());
                return;
            }

            Ammo--;
            _nextFire = Time.time + fireInterval;
            _audio.pitch = soundPitch * UnityEngine.Random.Range(0.96f, 1.04f);
            _audio.PlayOneShot(Effects.Gunshot, 0.9f);
            controller.AddRecoil(recoilDegrees);
            _kick += new Vector3(0f, 0.01f, -kickBack);
            _kickRotation += kickRotation;
            _flashUntil = Time.time + 0.05f;
            SetFlash(true);
            if (muzzleFlash != null) muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            Vector3 direction = SpreadDirection(aimCamera.transform, CurrentSpread);
            _bloom = Mathf.Min(maxBloom, _bloom + shotBloom);
            ShotFired?.Invoke(aimCamera.transform.position);
            Shoot(aimCamera.transform.position, direction);
        }

        // As hitboxes seguem os ossos animados; sincroniza a física para o raio ver a pose que está na tela.
        // A bala para no primeiro zumbi atingido, mas as partes do próprio zumbi (braço na frente do rosto,
        // pescoço) não protegem a cabeça dele: se o raio atravessa a cabeça desse zumbi, é headshot.
        void Shoot(Vector3 origin, Vector3 direction)
        {
            Physics.SyncTransforms();
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, range, ~IgnoreRaycastMask, QueryTriggerInteraction.Collide);
            Array.Sort(Hits, 0, count, HitDistanceComparer.Instance);

            ZombieController target = null;
            bool head = false;
            RaycastHit impact = default;
            bool anything = false;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox == null)
                {
                    if (!anything) { impact = hit; anything = true; }
                    break;   // parede, móvel: a bala para aqui
                }
                if (hitbox.owner == null || hitbox.owner.IsDead) continue;
                if (target == null) { target = hitbox.owner; impact = hit; anything = true; }
                else if (hitbox.owner != target) break;   // outro zumbi atrás: a bala não atravessa
                if (hitbox.isHead) { head = true; impact = hit; break; }
            }

            if (target != null)
            {
                LastHitTime = Time.time;
                LastHitWasHead = head;
                target.TakeHit(head, impact.point, direction);
            }
            else if (anything)
            {
                // Faísca e poeira no cenário.
                Effects.Burst(impact.point, impact.normal, new Color(1f, 0.85f, 0.4f), 6, 3.5f, 0.03f, 0.6f, 0.25f, 45f);
                Effects.Burst(impact.point, impact.normal, new Color(0.45f, 0.42f, 0.38f), 8, 1.2f, 0.06f, 0.3f, 0.7f, 30f);
            }
        }

        IEnumerator Reload()
        {
            if (IsReloading) yield break;
            IsReloading = true;
            _audio.pitch = 1f;
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

        sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
