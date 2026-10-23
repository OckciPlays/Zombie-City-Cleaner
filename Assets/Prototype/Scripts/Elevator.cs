using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Elevador "infinito": ele fica sempre no mesmo lugar. Durante a viagem (portas fechadas, motor roncando e
    /// a cabine tremendo) o andar anterior é apagado e o próximo é gerado em volta dele pelo FloorGenerator.
    /// Só sobe quando o andar está limpo: dentro da cabine, aperte E.
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        [Header("Referências")]
        public FloorGenerator floors;
        public Transform doorLeft, doorRight;
        public Renderer indicator;
        public FirstPersonController player;

        [Header("Cabine (espaço local)")]
        public Vector3 interiorCenter = new Vector3(-1.4f, 1.2f, 0f);
        public Vector3 interiorSize = new Vector3(2.5f, 2.4f, 2.5f);

        [Header("Viagem")]
        public float doorSlide = 0.72f;
        public float doorTime = 0.9f;
        public float travelTime = 3f;

        public int CurrentFloor { get; private set; }
        public bool IsMoving { get; private set; }
        public bool DoorsOpen { get; private set; }
        /// <summary>Quem decide se pode subir (o TowerGame liga isso à cota do andar).</summary>
        public Func<bool> CanLeave = () => true;

        public event Action<int, int> Arrived;   // andar, zumbis gerados

        AudioSource _motor, _sfx;
        Vector3 _leftClosed, _rightClosed;
        MaterialPropertyBlock _block;
        int _seed;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _leftClosed = doorLeft.localPosition;
            _rightClosed = doorRight.localPosition;
            _motor = gameObject.AddComponent<AudioSource>();
            _motor.clip = Effects.Hum;
            _motor.loop = true;
            _motor.spatialBlend = 0f;
            _motor.volume = 0.5f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f;
            _block = new MaterialPropertyBlock();
            _seed = UnityEngine.Random.Range(0, 100000);
        }

        public bool PlayerInside => player != null && Contains(player.transform.position + Vector3.up);

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - interiorCenter;
            Vector3 half = interiorSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>Começa a partida: a cabine sai com as portas fechadas em direção ao andar 1.</summary>
        public void BeginRun() => StartCoroutine(Travel());

        public bool TryGoUp()
        {
            if (IsMoving || !PlayerInside || !CanLeave()) return false;
            StartCoroutine(Travel());
            return true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && player != null && !player.IsPaused) TryGoUp();
            SetIndicator(IsMoving ? new Color(1f, 0.6f, 0.1f) : CanLeave() ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.1f, 0.05f));
        }

        IEnumerator Travel()
        {
            IsMoving = true;
            if (DoorsOpen) yield return MoveDoors(false);

            _motor.Play();
            float start = Time.time;
            yield return null;   // um quadro com a cabine fechada antes do trabalho pesado

            // Gera o próximo andar enquanto as portas estão fechadas (esconde a pequena travada da geração).
            int next = CurrentFloor + 1;
            int zombies = floors != null ? floors.Generate(next, _seed + next * 7919) : 0;
            CurrentFloor = next;

            while (Time.time - start < travelTime)
            {
                if (player != null) player.AddShake(0.25f);
                yield return null;
            }
            _motor.Stop();
            _sfx.PlayOneShot(Effects.Ding, 0.8f);
            yield return new WaitForSeconds(0.4f);
            yield return MoveDoors(true);
            IsMoving = false;
            Arrived?.Invoke(CurrentFloor, zombies);
        }

        IEnumerator MoveDoors(bool open)
        {
            _sfx.PlayOneShot(Effects.DoorSlide, 0.7f);
            Vector3 leftTarget = _leftClosed + (open ? Vector3.forward * doorSlide : Vector3.zero);
            Vector3 rightTarget = _rightClosed + (open ? Vector3.back * doorSlide : Vector3.zero);
            Vector3 leftFrom = doorLeft.localPosition, rightFrom = doorRight.localPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime / doorTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                doorLeft.localPosition = Vector3.Lerp(leftFrom, leftTarget, k);
                doorRight.localPosition = Vector3.Lerp(rightFrom, rightTarget, k);
                yield return null;
            }
            doorLeft.localPosition = leftTarget;
            doorRight.localPosition = rightTarget;
            DoorsOpen = open;
        }

        void SetIndicator(Color color)
        {
            if (indicator == null) return;
            indicator.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, color * 2f);
            indicator.SetPropertyBlock(_block);
        }
    }
}
