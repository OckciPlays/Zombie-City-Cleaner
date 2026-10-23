using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Desenho do HUD com OnGUI, compartilhado pelos modos de jogo: mira com hitmarker, vida, munição,
    /// mensagens e texto com sombra. Tudo é medido em "pixels de PS1" (a tela tem 360 de altura).
    /// </summary>
    public static class Hud
    {
        static readonly GUIStyle Style = new GUIStyle();

        public static float Unit => Screen.height / 360f;

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void Text(Rect rect, string text, float size, TextAnchor anchor, Color color)
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

        public static void Message(string title, string subtitle, Color color)
        {
            float unit = Unit;
            Fill(new Rect(0, Screen.height * 0.38f, Screen.width, 46 * unit), new Color(0, 0, 0, 0.55f));
            Text(new Rect(0, Screen.height * 0.38f + 4 * unit, Screen.width, 24 * unit), title, 18 * unit, TextAnchor.MiddleCenter, color);
            Text(new Rect(0, Screen.height * 0.38f + 28 * unit, Screen.width, 14 * unit), subtitle, 8 * unit, TextAnchor.MiddleCenter, Color.white);
        }

        /// <summary>Texto de aviso logo abaixo da mira (ex.: "Aperte E para subir").</summary>
        public static void Prompt(string text, Color color)
        {
            float unit = Unit;
            Text(new Rect(0, Screen.height * 0.62f, Screen.width, 16 * unit), text, 9 * unit, TextAnchor.MiddleCenter, color);
        }

        public static void DamageFlash(PlayerHealth player)
        {
            if (player != null && player.DamageFlash > 0f)
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.6f, 0f, 0f, player.DamageFlash * 0.35f));
        }

        public static void Crosshair(Gun gun)
        {
            float unit = Unit;
            var cam = gun != null ? gun.aimCamera : Camera.main;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float spread = gun != null ? gun.CurrentSpread : 0.5f;
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
            if (gun != null && Time.time - gun.LastHitTime < 0.25f)
            {
                Color hitColor = gun.LastHitWasHead ? new Color(1f, 0.15f, 0.1f) : Color.white;
                float size = (gun.LastHitWasHead ? 9f : 6f) * unit, off = 4f * unit;
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
                Fill(new Rect(cx - off - size, cy - thickness * 0.5f, size, thickness), hitColor);
                Fill(new Rect(cx + off, cy - thickness * 0.5f, size, thickness), hitColor);
                Fill(new Rect(cx - thickness * 0.5f, cy - off - size, thickness, size), hitColor);
                Fill(new Rect(cx - thickness * 0.5f, cy + off, thickness, size), hitColor);
                GUI.matrix = previous;
                if (gun.LastHitWasHead)
                    Text(new Rect(0, cy + 14 * unit, Screen.width, 16 * unit), "HEADSHOT", 9 * unit, TextAnchor.MiddleCenter, hitColor);
            }
        }

        /// <summary>Vida (canto inferior esquerdo), munição e aviso de lanterna (canto inferior direito).</summary>
        public static void PlayerStatus(PlayerHealth player, Gun gun, Flashlight flashlight)
        {
            float unit = Unit, margin = 12 * unit;
            if (player != null)
            {
                float barWidth = 70 * unit, barHeight = 6 * unit, y = Screen.height - margin - barHeight;
                Fill(new Rect(margin - unit, y - unit, barWidth + 2 * unit, barHeight + 2 * unit), new Color(0, 0, 0, 0.7f));
                Fill(new Rect(margin, y, barWidth * player.Health / player.maxHealth, barHeight), new Color(0.75f, 0.1f, 0.1f));
                Text(new Rect(margin, y - 13 * unit, 200 * unit, 12 * unit), "VIDA", 8 * unit, TextAnchor.LowerLeft, Color.white);
            }
            if (gun != null)
            {
                string ammo = gun.IsReloading ? "RECARREGANDO" : $"{gun.Ammo} / {gun.magazineSize}";
                Color ammoColor = !gun.IsReloading && gun.Ammo <= Mathf.Max(3, gun.magazineSize / 5) ? new Color(1f, 0.35f, 0.25f) : Color.white;
                Text(new Rect(Screen.width - margin - 200 * unit, Screen.height - margin - 16 * unit, 200 * unit, 16 * unit), ammo, 11 * unit, TextAnchor.LowerRight, ammoColor);
                Text(new Rect(Screen.width - margin - 200 * unit, Screen.height - margin - 29 * unit, 200 * unit, 12 * unit), gun.weaponName, 8 * unit, TextAnchor.LowerRight, new Color(0.9f, 0.85f, 0.4f));
            }
            if (flashlight != null && !flashlight.IsOn)
                Text(new Rect(Screen.width - margin - 200 * unit, Screen.height - margin - 42 * unit, 200 * unit, 12 * unit), "LANTERNA DESLIGADA (F)", 7 * unit, TextAnchor.LowerRight, new Color(0.7f, 0.7f, 0.7f));
        }

        /// <summary>Duas linhas no canto superior esquerdo (ex.: cota e tempo).</summary>
        public static void TopLeft(string line1, string line2)
        {
            float unit = Unit, margin = 12 * unit;
            Text(new Rect(margin, margin, 300 * unit, 14 * unit), line1, 10 * unit, TextAnchor.UpperLeft, new Color(0.9f, 0.85f, 0.4f));
            if (!string.IsNullOrEmpty(line2))
                Text(new Rect(margin, margin + 13 * unit, 300 * unit, 14 * unit), line2, 8 * unit, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.8f));
        }

        public static void Instructions(string goal, float alpha)
        {
            float unit = Unit;
            Text(new Rect(0, Screen.height * 0.72f, Screen.width, 20 * unit), goal, 9 * unit, TextAnchor.MiddleCenter, new Color(1, 1, 1, alpha));
            Text(new Rect(0, Screen.height * 0.72f + 16 * unit, Screen.width, 20 * unit), "WASD mover · SHIFT correr · ESPAÇO pular · CLIQUE atirar · R recarregar · 1/2 trocar arma · F lanterna · E elevador · ESC pausar", 7 * unit, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f, alpha));
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
