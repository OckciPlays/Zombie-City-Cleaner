using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Modo prédio: o jogador começa no elevador, que sobe andar por andar. Cada andar tem uma cota de zumbis;
    /// com o andar limpo, o elevador libera a subida (E dentro da cabine). Guarda o recorde de andar.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TowerGame : MonoBehaviour
    {
        [Header("Referências")]
        public Elevator elevator;
        public FloorGenerator floors;
        public PlayerHealth player;
        public WeaponHolder weapons;
        public Flashlight flashlight;
        public Material particleMaterial;

        const string BestFloorKey = "ZombieCityCleaner.MelhorAndar";

        int _floor, _total, _killed, _best;
        bool _cleared, _started;
        float _runStart, _arrivedAt = -99f, _clearedAt = -99f;
        FirstPersonController _controller;

        public int Floor => _floor;
        public int Remaining => Mathf.Max(0, _total - _killed);
        public bool FloorCleared => _cleared;
        Gun CurrentGun => weapons != null ? weapons.Current : null;

        void Awake()
        {
            Effects.ParticleMaterial = particleMaterial;
            elevator.CanLeave = () => _floor == 0 || _cleared;
        }

        void OnEnable()
        {
            ZombieController.Died += OnZombieDied;
            elevator.Arrived += OnArrived;
        }

        void OnDisable()
        {
            ZombieController.Died -= OnZombieDied;
            elevator.Arrived -= OnArrived;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Start()
        {
            _controller = player.GetComponent<FirstPersonController>();
            _best = PlayerPrefs.GetInt(BestFloorKey, 0);
            _runStart = Time.time;
            elevator.BeginRun();
        }

        void Update()
        {
            bool dead = player != null && player.IsDead;
            bool paused = !dead && _controller != null && _controller.IsPaused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (!paused) _started = true;

            var keyboard = Keyboard.current;
            if (dead && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnArrived(int floor, int zombies)
        {
            _floor = floor;
            _total = zombies;
            _killed = 0;
            _cleared = zombies == 0;
            _arrivedAt = Time.time;
        }

        void OnZombieDied(ZombieController zombie)
        {
            if (!floors.Zombies.Contains(zombie)) return;
            _killed++;
            if (_killed < _total || _cleared) return;
            _cleared = true;
            _clearedAt = Time.time;
            if (_floor > _best)
            {
                _best = _floor;
                PlayerPrefs.SetInt(BestFloorKey, _best);
                PlayerPrefs.Save();
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Hud.DamageFlash(player);

            bool dead = player != null && player.IsDead;
            if (!dead) Hud.Crosshair(CurrentGun);
            string floorLine = _floor == 0 ? "TÉRREO" : $"ANDAR {_floor}";
            string status = elevator.IsMoving ? "SUBINDO..." : _cleared ? "ANDAR LIMPO" : $"ZUMBIS {Remaining}/{_total}";
            Hud.TopLeft(floorLine, $"{status}   ·   TEMPO {Hud.FormatTime(Time.time - _runStart)}");
            Hud.PlayerStatus(player, CurrentGun, flashlight);

            const string goal = "Mate todos os zumbis do andar e volte ao elevador para subir. Só tiro na cabeça mata.";
            if (dead)
            {
                Hud.Message("VOCÊ FOI DEVORADO", $"Chegou ao andar {_floor}   ·   Recorde: andar {_best}   ·   R para tentar de novo", new Color(0.8f, 0.1f, 0.1f));
                return;
            }
            if (_controller != null && _controller.IsPaused)
            {
                Hud.Message(_started ? "PAUSADO" : "ZOMBIE CITY CLEANER", _best > 0 ? $"Clique para jogar   ·   Recorde: andar {_best}" : "Clique para jogar", Color.white);
                if (!_started) Hud.Instructions(goal, 1f);
                return;
            }

            if (Time.time - _arrivedAt < 2.5f)
                Hud.Message($"ANDAR {_floor}", $"{_total} zumbis neste andar", new Color(0.9f, 0.85f, 0.4f));
            else if (_cleared && Time.time - _clearedAt < 4f)
                Hud.Message("ANDAR LIMPO!", "Volte ao elevador e aperte E para subir", new Color(0.4f, 0.9f, 0.4f));
            else if (Time.time - _runStart < 9f)
                Hud.Instructions(goal, Mathf.Clamp01(9f - (Time.time - _runStart)));

            if (elevator.PlayerInside && !elevator.IsMoving && _floor > 0)
            {
                if (_cleared) Hud.Prompt("Aperte E para subir", new Color(0.4f, 1f, 0.4f));
                else Hud.Prompt($"O elevador só sobe com o andar limpo ({Remaining} zumbis)", new Color(1f, 0.45f, 0.35f));
            }
        }
    }
}
