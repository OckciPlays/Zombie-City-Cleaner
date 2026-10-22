using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Zumbi do protótipo. Fica parado (Idle) até perceber o jogador (visão, proximidade ou barulho de tiro),
    /// então corre atrás dele pelo NavMesh e ataca de perto. Tiro na cabeça mata na hora; no corpo, ele só cambaleia.
    /// As hitboxes são criadas nos ossos ao iniciar, a partir da pose de bind do modelo.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ZombieController : MonoBehaviour
    {
        [Header("Percepção")]
        public float sightRange = 14f;
        public float sightAngle = 140f;
        public float closeSenseRange = 3.5f;
        public float hearingRange = 12f;
        public float alertNeighboursRange = 7f;

        [Header("Movimento")]
        public float runSpeed = 3.2f;
        [Tooltip("Velocidade (m/s) em que a animação de corrida combina com o deslocamento, com velocidade 1 no Animator.")]
        public float animationRunSpeed = 1.8f;

        [Header("Ataque")]
        public float attackRange = 1.5f;
        public float attackDamage = 10f;
        public float attackInterval = 1.2f;

        [Header("Vida")]
        [Tooltip("Quantos tiros no corpo matam. 0 = só morre com tiro na cabeça.")]
        public int bodyHitsToKill = 0;

        public bool IsDead { get; private set; }
        public bool IsAlerted { get; private set; }

        public static event Action<ZombieController> Died;
        static readonly List<ZombieController> All = new List<ZombieController>();
        static readonly int SpeedId = Animator.StringToHash("Speed");
        const int IgnoreRaycastMask = 1 << 2;

        // Ossos com cápsula: osso, osso onde a cápsula termina, raio (em unidades do modelo, que mede 1 m).
        static readonly (string bone, string end, float radius)[] BodyParts =
        {
            ("hips", "spine", 0.09f), ("spine", "chest", 0.10f), ("chest", "neck", 0.11f), ("neck", "head", 0.045f),
            ("upper_arm.L", "forearm.L", 0.045f), ("forearm.L", "hand.L", 0.04f),
            ("upper_arm.R", "forearm.R", 0.045f), ("forearm.R", "hand.R", 0.04f),
            ("thigh.L", "shin.L", 0.065f), ("shin.L", "foot.L", 0.055f),
            ("thigh.R", "shin.R", 0.065f), ("shin.R", "foot.R", 0.055f),
        };
        // Centro e raio da cabeça no espaço do modelo (pose de bind).
        static readonly Vector3 HeadCenterModel = new Vector3(0f, 0.885f, 0.03f);
        const float HeadRadiusModel = 0.085f;

        Animator _animator;
        NavMeshAgent _agent;
        PlayerHealth _target;
        Transform _targetEye;
        Transform _head;
        readonly List<Collider> _colliders = new List<Collider>();
        float _nextSense, _nextAttack, _staggerUntil, _nextRepath;
        int _bodyHits;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            BuildColliders();
        }

        void OnEnable()
        {
            All.Add(this);
            Pistol.ShotFired += OnShotHeard;
        }

        void OnDisable()
        {
            All.Remove(this);
            Pistol.ShotFired -= OnShotHeard;
        }

        void Start()
        {
            _target = FindAnyObjectByType<PlayerHealth>();
            if (Camera.main != null) _targetEye = Camera.main.transform;

            float scale = transform.lossyScale.y;
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = runSpeed;
            _agent.acceleration = 24f;
            _agent.angularSpeed = 720f;
            _agent.radius = 0.35f / scale;   // o raio e a altura do agente são multiplicados pela escala
            _agent.height = 1.8f / scale;
            _agent.stoppingDistance = attackRange * 0.8f;
            _agent.autoBraking = true;
            _agent.Warp(transform.position);
            _agent.isStopped = true;

            // Cada zumbi começa o idle num ponto diferente, para a horda não ficar sincronizada.
            _animator.Play("Idle", 0, UnityEngine.Random.value);
            _animator.SetFloat(SpeedId, 0f);
        }

        void Update()
        {
            if (IsDead || _target == null || _agent == null || !_agent.isOnNavMesh) return;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (!IsAlerted && Time.time >= _nextSense)
            {
                _nextSense = Time.time + 0.2f;
                if (CanSensePlayer(toTarget, distance)) Alert();
            }

            if (!IsAlerted || _target.IsDead)
            {
                _agent.isStopped = true;
                SetAnimation(0f, 1f);
                return;
            }

            if (Time.time < _staggerUntil)
            {
                _agent.isStopped = true;
                return;
            }

            if (distance <= attackRange)
            {
                // Ataque: para, vira para o jogador e "agarra" (a animação de corrida tem os braços esticados).
                _agent.isStopped = true;
                if (toTarget.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), 1f - Mathf.Exp(-10f * Time.deltaTime));
                SetAnimation(1f, 1.3f);
                if (Time.time >= _nextAttack)
                {
                    _nextAttack = Time.time + attackInterval;
                    _target.TakeDamage(attackDamage);
                    Effects.PlayAt(Effects.Flesh, _target.transform.position + Vector3.up, 0.8f, 0.8f);
                }
                return;
            }

            _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.35f);
            _agent.isStopped = false;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + 0.25f;
                _agent.SetDestination(_target.transform.position);
            }
            float velocity = _agent.velocity.magnitude;
            SetAnimation(velocity, velocity > 0.1f ? Mathf.Clamp(velocity / animationRunSpeed, 0.6f, 2.2f) : 1f);
        }

        void SetAnimation(float speedParameter, float playbackSpeed)
        {
            _animator.SetFloat(SpeedId, speedParameter);
            _animator.speed = playbackSpeed;
        }

        bool CanSensePlayer(Vector3 toTarget, float distance)
        {
            if (_target.IsDead) return false;
            if (distance <= closeSenseRange) return true;
            if (distance > sightRange) return false;
            if (Vector3.Angle(transform.forward, toTarget) > sightAngle * 0.5f) return false;
            return HasLineOfSight();
        }

        bool HasLineOfSight()
        {
            if (_targetEye == null) return true;
            Vector3 from = _head != null ? _head.position : transform.position + Vector3.up * 1.6f;
            if (!Physics.Linecast(from, _targetEye.position, out RaycastHit hit, ~IgnoreRaycastMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<PlayerHealth>() != null;
        }

        public void Alert(bool spread = true)
        {
            if (IsDead || IsAlerted) return;
            IsAlerted = true;
            Effects.PlayAt(Effects.Growl, transform.position + Vector3.up * 1.5f, 0.9f, UnityEngine.Random.Range(0.85f, 1.15f));
            if (!spread) return;
            foreach (var other in All)
                if (other != this && (other.transform.position - transform.position).sqrMagnitude < alertNeighboursRange * alertNeighboursRange)
                    other.Alert(false);
        }

        void OnShotHeard(Vector3 position)
        {
            if ((position - transform.position).sqrMagnitude < hearingRange * hearingRange) Alert(false);
        }

        public void TakeHit(bool isHead, Vector3 point, Vector3 direction)
        {
            if (IsDead) return;
            Alert();
            Vector3 spray = Vector3.Lerp(-direction, Vector3.up, 0.3f);
            Effects.Burst(point, spray, new Color(0.55f, 0.02f, 0.02f), isHead ? 30 : 14, isHead ? 3.5f : 2.2f, 0.07f, 1.2f, 0.8f);
            Effects.PlayAt(Effects.Flesh, point, 1f, isHead ? 0.8f : 1.1f);

            if (isHead)
            {
                Die(direction, true);
                return;
            }
            _bodyHits++;
            if (bodyHitsToKill > 0 && _bodyHits >= bodyHitsToKill)
            {
                Die(direction, false);
                return;
            }
            // Cambaleia para trás.
            _staggerUntil = Time.time + 0.35f;
            Vector3 push = new Vector3(direction.x, 0f, direction.z).normalized * 0.25f;
            if (_agent != null && _agent.isOnNavMesh) _agent.Move(push);
        }

        void Die(Vector3 direction, bool headshot)
        {
            IsDead = true;
            if (_agent != null) _agent.enabled = false;
            _animator.enabled = false;
            foreach (var c in _colliders) c.enabled = false;

            if (headshot && _head != null)
            {
                // A cabeça "explode": encolhe o osso e espirra sangue.
                Vector3 headPosition = transform.TransformPoint(HeadCenterModel);
                _head.localScale = Vector3.zero;
                Effects.Burst(headPosition, Vector3.up + direction * 0.5f, new Color(0.6f, 0.03f, 0.03f), 45, 4.5f, 0.09f, 1.5f, 1.1f, 60f);
                Effects.Burst(headPosition, direction, new Color(0.85f, 0.75f, 0.65f), 10, 3f, 0.05f, 1.5f, 0.9f, 40f);
            }

            StartCoroutine(Fall(direction));
            Died?.Invoke(this);
        }

        IEnumerator Fall(Vector3 direction)
        {
            Vector3 fallDirection = new Vector3(direction.x, 0f, direction.z);
            if (fallDirection.sqrMagnitude < 0.001f) fallDirection = -transform.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, fallDirection.normalized);
            Vector3 pivot = transform.position;
            float angle = 0f, speed = 0f;
            while (angle < 86f)
            {
                speed += 900f * Time.deltaTime;   // acelera como uma queda
                float step = Mathf.Min(speed * Time.deltaTime, 86f - angle);
                transform.RotateAround(pivot, axis, step);
                angle += step;
                yield return null;
            }
            transform.position += Vector3.down * 0.05f;
        }

        void BuildColliders()
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            float scale = transform.lossyScale.y;

            foreach (var (boneName, endName, radius) in BodyParts)
            {
                if (!bones.TryGetValue(boneName, out var bone) || !bones.TryGetValue(endName, out var end)) continue;
                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                Vector3 localEnd = bone.InverseTransformPoint(end.position);
                float boneScale = MaxAbs(bone.lossyScale);
                float localRadius = radius * scale / boneScale;
                int axis = DominantAxis(localEnd);
                capsule.direction = axis;
                capsule.center = localEnd * 0.5f;
                capsule.radius = localRadius;
                capsule.height = Mathf.Max(Mathf.Abs(localEnd[axis]), localRadius * 2f);
                capsule.isTrigger = true;
                AddHitbox(capsule, false);
            }

            if (bones.TryGetValue("head", out _head))
            {
                var sphere = _head.gameObject.AddComponent<SphereCollider>();
                sphere.center = _head.InverseTransformPoint(transform.TransformPoint(HeadCenterModel));
                sphere.radius = HeadRadiusModel * scale / MaxAbs(_head.lossyScale);
                sphere.isTrigger = true;
                AddHitbox(sphere, true);
            }

            // Corpo sólido (na layer Ignore Raycast, para não bloquear os tiros) para o jogador não atravessar o zumbi.
            gameObject.layer = 2;
            var body = gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.5f, 0f);
            body.height = 1f;
            body.radius = 0.17f;
            _colliders.Add(body);
        }

        void AddHitbox(Collider collider, bool isHead)
        {
            var hitbox = collider.gameObject.AddComponent<Hitbox>();
            hitbox.owner = this;
            hitbox.isHead = isHead;
            _colliders.Add(collider);
        }

        static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        static int DominantAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
        }
    }
}
