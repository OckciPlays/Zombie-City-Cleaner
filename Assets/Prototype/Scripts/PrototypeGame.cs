using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Regras do protótipo do galpão: gera o NavMesh do nível, conta a cota de zumbis, mede o tempo e desenha
    /// o HUD. R reinicia depois de morrer ou limpar a área.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PrototypeGame : MonoBehaviour
    {
        [Header("Referências")]
        public NavMeshSurface levelSurface;
        public Material particleMaterial;
        public PlayerHealth player;
        public WeaponHolder weapons;
        public Flashlight flashlight;

        int _total, _killed;
        float _startTime, _endTime;
        bool _cleared, _started;
        FirstPersonController _controller;

        Gun CurrentGun => weapons != null ? weapons.Current : null;

        void Awake()
        {
            // Roda antes dos zumbis (ordem -500), que criam o NavMeshAgent no Start.
            Effects.ParticleMaterial = particleMaterial;
            if (levelSurface != null) levelSurface.BuildNavMesh();
        }

        void OnEnable() => ZombieController.Died += OnZombieDied;

        void OnDisable()
        {
            ZombieController.Died -= OnZombieDied;
            Time.timeScale = 1f;
        }

        void Start()
        {
            _total = FindObjectsByType<ZombieController>().Length;
            _startTime = Time.time;
            if (player != null) _controller = player.GetComponent<FirstPersonController>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool over = _cleared || (player != null && player.IsDead);
            bool paused = !over && _controller != null && _controller.IsPaused;
            Time.timeScale = paused ? 0f : 1f;
            if (!paused) _started = true;
            if (over && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnZombieDied(ZombieController zombie)
        {
            _killed++;
            if (_killed >= _total && !_cleared)
            {
                _cleared = true;
                _endTime = Time.time;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Hud.DamageFlash(player);

            bool dead = player != null && player.IsDead;
            if (!dead && !_cleared) Hud.Crosshair(CurrentGun);
            float elapsed = (_cleared ? _endTime : Time.time) - _startTime;
            Hud.TopLeft($"COTA  {_killed}/{_total}", $"TEMPO {Hud.FormatTime(elapsed)}");
            Hud.PlayerStatus(player, CurrentGun, flashlight);

            const string goal = "Limpe a área: mate todos os zumbis. Só tiro na cabeça mata.";
            if (dead)
                Hud.Message("VOCÊ FOI DEVORADO", "R para tentar de novo", new Color(0.8f, 0.1f, 0.1f));
            else if (_cleared)
                Hud.Message("ÁREA LIMPA!", $"Tempo: {Hud.FormatTime(elapsed)}   ·   R para jogar de novo", new Color(0.4f, 0.9f, 0.4f));
            else if (_controller != null && _controller.IsPaused)
            {
                Hud.Message(_started ? "PAUSADO" : "ZOMBIE CITY CLEANER", "Clique para jogar", Color.white);
                if (!_started) Hud.Instructions(goal, 1f);
            }
            else if (Time.time - _startTime < 7f)
                Hud.Instructions(goal, Mathf.Clamp01(7f - (Time.time - _startTime)));
        }
    }
}
