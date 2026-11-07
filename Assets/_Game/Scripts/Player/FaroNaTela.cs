using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O Faro (06/11/2026, a ponta do Sortudo), no dono: os itens raros (raro, épico, lendário) dos móveis a até
    /// <see cref="Alcance"/> m aparecem marcados na tela, na cor da raridade, até dentro das gavetas fechadas e atrás das
    /// paredes (é o faro, não a vista). Só quem tem a habilidade vê; nada vai pela rede (o saque é o mesmo em todas as
    /// máquinas).
    /// </summary>
    public class FaroNaTela : MonoBehaviour
    {
        public const float Alcance = 15f;

        JogadorRede _rede;
        GUIStyle _estilo;
        float _u;
        Texture2D _losango;

        void Awake() => _rede = GetComponent<JogadorRede>();

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rede == null || !_rede.IsOwner) return;
            if (!Habilidades.Tem(_rede, Habilidade.Faro)) return;
            if (MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || GravacaoNoMundo.SemHud) return;
            var cam = _rede.cameraMundo;
            if (cam == null) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                _estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(12 * u), FontStyle.Bold, TextAnchor.UpperCenter));
            }
            if (_losango == null) _losango = Losango();
            Vector3 olho = cam.transform.position;
            foreach (var s in MobiliaNovaDoMundo.SaqueAVista)
            {
                if (!s.gameObject.activeInHierarchy || s.raridade < Raridade.Raro) continue;
                Vector3 p = s.transform.position;
                float d = Vector3.Distance(olho, p);
                if (d > Alcance) continue;
                var tela = cam.WorldToScreenPoint(p);
                if (tela.z <= 0f) continue;
                float x = tela.x, y = Screen.height - tela.y;
                float a = Mathf.Clamp01(1.2f - d / Alcance);
                var cor = CorDa(s.raridade);
                float lado = 12 * u * (0.8f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f));
                var antes = GUI.color;
                GUI.color = new Color(cor.r, cor.g, cor.b, 0.85f * a);
                GUI.DrawTexture(new Rect(x - lado / 2f, y - lado / 2f, lado, lado), _losango);
                GUI.color = antes;
                _estilo.normal.textColor = new Color(cor.r, cor.g, cor.b, a);
                GUI.Label(new Rect(x - 80 * u, y + 8 * u, 160 * u, 18 * u), $"{Itens.Nome(s.item)} · {d:0} m", _estilo);
            }
        }

        static Color CorDa(Raridade r) => ColorUtility.TryParseHtmlString(CatalogoDoSaque.CorEmHex(r), out var c) ? c : Color.yellow;

        static Texture2D Losango()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - n / 2f) + Mathf.Abs(y + 0.5f - n / 2f);
                    float a = Mathf.Clamp01((n / 2f - d) / 1.5f);
                    float borda = Mathf.Clamp01((d - (n / 2f - 5f)) / 1.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * Mathf.Lerp(0.45f, 1f, borda) * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
