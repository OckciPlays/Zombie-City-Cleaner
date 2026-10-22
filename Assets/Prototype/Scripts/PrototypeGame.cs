using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Regras do protótipo: gera o NavMesh do nível, conta a cota de zumbis, mede o tempo e desenha o HUD
    /// (mira, vida, munição, cota e mensagens). R reinicia depois de morrer ou limpar a área.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PrototypeGame : MonoBehaviour
    {
        [Header("Referências")]
        public NavMeshSurface levelSurface;
        public Material particleMaterial;
        public PlayerHealth player;
        public Pistol pistol;
        public Flashlight flashlight;

        int _total, _killed;
        float _startTime, _endTime;
        bool _cleared, _started;
        Texture2D _white;
        FirstPersonController _controller;

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

        // ---------------------------------------------------------------- HUD

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_white == null) _white = Texture2D.whiteTexture;
            float unit = Screen.height / 360f;   // 1 "pixel de PS1" na tela

            if (player != null && player.DamageFlash > 0f)
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.6f, 0f, 0f, player.DamageFlash * 0.35f));

            bool dead = player != null && player.IsDead;
            if (!dead && !_cleared) DrawCrosshair(unit);
            DrawStats(unit);

            float elapsed = (_cleared ? _endTime : Time.time) - _startTime;
            if (dead)
                Message(unit, "VOCÊ FOI DEVORADO", "R para tentar de novo", new Color(0.8f, 0.1f, 0.1f));
            else if (_cleared)
                Message(unit, "ÁREA LIMPA!", $"Tempo: {FormatTime(elapsed)}   ·   R para jogar de novo", new Color(0.4f, 0.9f, 0.4f));
            else if (_controller != null && _controller.IsPaused)
            {
                Message(unit, _started ? "PAUSADO" : "ZOMBIE CITY CLEANER", "Clique para jogar", Color.white);
                if (!_started) Instructions(unit, 1f);
            }
            else if (Time.time - _startTime < 7f)
                Instructions(unit, Mathf.Clamp01(7f - (Time.time - _startTime)));
        }

        void Instructions(float unit, float alpha)
        {
            Text(new Rect(0, Screen.height * 0.72f, Screen.width, 20 * unit), "Limpe a área: mate todos os zumbis. Só tiro na cabeça mata.", 9 * unit, TextAnchor.MiddleCenter, new Color(1, 1, 1, alpha));
            Text(new Rect(0, Screen.height * 0.72f + 16 * unit, Screen.width, 20 * unit), "WASD mover · SHIFT correr · ESPAÇO pular · CLIQUE atirar · R recarregar · F lanterna · ESC pausar", 7 * unit, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f, alpha));
        }

        void DrawCrosshair(float unit)
        {
            var cam = pistol != null ? pistol.aimCamera : Camera.main;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float spread = pistol != null ? pistol.CurrentSpread : 0.5f;
            float fov = cam != null ? cam.fieldOfView : 70f;
            float gap = Mathf.Tan(spread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f + 3f * unit;
            float length = 6f * unit, thickness = Mathf.Max(2f, Mathf.Round(unit));
            Color shadow = new Color(0, 0, 0, 0.8f), color = new Color(0.95f, 0.95f, 0.85f, 0.95f);

            for (int pass = 0; pass < 2; pass++)
            {
                float o = pass == 0 ? 1f : 0f;
                Color c = pass == 0 ? shadow : color;
                Fill(new Rect(cx - gap - length - o, cy - thickness * 0.5f - o, length + 2 * o, thickness + 2 * o), c);
                Fill(new Rect(cx + gap - o, cy - thickness * 0.5f - o, length + 2 * o, thickness + 2 * o), c);
                Fill(new Rect(cx - thickness * 0.5f - o, cy - gap - length - o, thickness + 2 * o, length + 2 * o), c);
                Fill(new Rect(cx - thickness * 0.5f - o, cy + gap - o, thickness + 2 * o, length + 2 * o), c);
                Fill(new Rect(cx - thickness * 0.5f - o, cy - thickness * 0.5f - o, thickness + 2 * o, thickness + 2 * o), c);
            }

            // Hitmarker: X branco no corpo, vermelho na cabeça.
            if (pistol != null && Time.time - pistol.LastHitTime < 0.25f)
            {
                Color hitColor = pistol.LastHitWasHead ? new Color(1f, 0.15f, 0.1f) : Color.white;
                float size = (pistol.LastHitWasHead ? 9f : 6f) * unit, off = 4f * unit;
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
                Fill(new Rect(cx - off - size, cy - thickness * 0.5f, size, thickness), hitColor);
                Fill(new Rect(cx + off, cy - thickness * 0.5f, size, thickness), hitColor);
                Fill(new Rect(cx - thickness * 0.5f, cy - off - size, thickness, size), hitColor);
                Fill(new Rect(cx - thickness * 0.5f, cy + off, thickness, size), hitColor);
                GUI.matrix = previous;
                if (pistol.LastHitWasHead)
                    Text(new Rect(0, cy + 14 * unit, Screen.width, 16 * unit), "HEADSHOT", 9 * unit, TextAnchor.MiddleCenter, hitColor);
            }
        }

        void DrawStats(float unit)
        {
            float margin = 12 * unit;
            float elapsed = (_cleared ? _endTime : Time.time) - _startTime;
            Text(new Rect(margin, margin, 300 * unit, 14 * unit), $"COTA  {_killed}/{_total}", 10 * unit, TextAnchor.UpperLeft, new Color(0.9f, 0.85f, 0.4f));
            Text(new Rect(margin, margin + 13 * unit, 300 * unit, 14 * unit), $"TEMPO {FormatTime(elapsed)}", 8 * unit, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.8f));

            if (player != null)
            {
                float barWidth = 70 * unit, barHeight = 6 * unit, y = Screen.height - margin - barHeight;
                Fill(new Rect(margin - unit, y - unit, barWidth + 2 * unit, barHeight + 2 * unit), new Color(0, 0, 0, 0.7f));
                Fill(new Rect(margin, y, barWidth * player.Health / player.maxHealth, barHeight), new Color(0.75f, 0.1f, 0.1f));
                Text(new Rect(margin, y - 13 * unit, 200 * unit, 12 * unit), "VIDA", 8 * unit, TextAnchor.LowerLeft, Color.white);
            }

            if (pistol != null)
            {
                string ammo = pistol.IsReloading ? "RECARREGANDO" : $"{pistol.Ammo} / {pistol.magazineSize}";
                Color ammoColor = !pistol.IsReloading && pistol.Ammo <= 3 ? new Color(1f, 0.35f, 0.25f) : Color.white;
                Text(new Rect(Screen.width - margin - 200 * unit, Screen.height - margin - 16 * unit, 200 * unit, 16 * unit), ammo, 11 * unit, TextAnchor.LowerRight, ammoColor);
            }
            if (flashlight != null && !flashlight.IsOn)
                Text(new Rect(Screen.width - margin - 200 * unit, Screen.height - margin - 30 * unit, 200 * unit, 12 * unit), "LANTERNA DESLIGADA (F)", 7 * unit, TextAnchor.LowerRight, new Color(0.7f, 0.7f, 0.7f));
        }

        void Message(float unit, string title, string subtitle, Color color)
        {
            Fill(new Rect(0, Screen.height * 0.38f, Screen.width, 46 * unit), new Color(0, 0, 0, 0.55f));
            Text(new Rect(0, Screen.height * 0.38f + 4 * unit, Screen.width, 24 * unit), title, 18 * unit, TextAnchor.MiddleCenter, color);
            Text(new Rect(0, Screen.height * 0.38f + 28 * unit, Screen.width, 14 * unit), subtitle, 8 * unit, TextAnchor.MiddleCenter, Color.white);
        }

        void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        static readonly GUIStyle Style = new GUIStyle();

        static void Text(Rect rect, string text, float size, TextAnchor anchor, Color color)
        {
            Style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            Style.fontStyle = FontStyle.Bold;
            Style.alignment = anchor;
            Style.normal.textColor = new Color(0, 0, 0, color.a * 0.85f);
            float shadow = Mathf.Max(1f, size / 8f);
            GUI.Label(new Rect(rect.x + shadow, rect.y + shadow, rect.width, rect.height), text, Style);
            Style.normal.textColor = color;
            GUI.Label(rect, text, Style);
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
