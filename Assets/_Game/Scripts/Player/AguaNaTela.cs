using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// No dono (06/11/2026, a água): com a câmera embaixo da água, a tela fica verde-azulada e escura (mais fundo, mais
    /// escuro); com o fôlego gastando, a barra "FÔLEGO" embaixo da mira (vermelha no fim). O fôlego é do anfitrião
    /// (<see cref="VidaJogador.Folego"/>).
    /// </summary>
    public class AguaNaTela : MonoBehaviour
    {
        JogadorRede _rede;
        VidaJogador _vida;
        GUIStyle _estilo;
        float _u, _embaixo;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rede == null || !_rede.IsOwner || _vida == null || !_vida.IsSpawned) return;
            if (MenuOnline.Cinema || ModoVideo.Ligado) return;
            var cam = _rede.cameraMundo;
            float fundo = 0f;
            if (cam != null && Agua.Nivel(cam.transform.position, out float nivel)) fundo = nivel - cam.transform.position.y;
            _embaixo = Mathf.MoveTowards(_embaixo, fundo > 0.02f ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (_embaixo > 0.01f)
            {
                float escuro = Mathf.Clamp01(fundo / 6f);
                Retangulo(new Rect(0, 0, Screen.width, Screen.height), new Color(0.05f, 0.22f - 0.12f * escuro, 0.26f - 0.12f * escuro, (0.42f + 0.35f * escuro) * _embaixo));
            }
            int folego = _vida.Folego.Value;
            if (folego >= 100 || MenuOnline.Aberto || GravacaoNoMundo.SemHud) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                _estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(12 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            float w = 160 * u, h = 7 * u, x = Screen.width * 0.5f - w / 2f, y = Screen.height * 0.5f + 46 * u;
            Retangulo(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.5f));
            var cor = folego > 30 ? new Color(0.45f, 0.8f, 1f, 0.95f) : new Color(0.95f, 0.3f, 0.25f, 0.95f);
            Retangulo(new Rect(x, y, w * folego / 100f, h), cor);
            _estilo.normal.textColor = cor;
            GUI.Label(new Rect(x, y - 18 * u, w, 16 * u), folego > 0 ? "FÔLEGO" : "SEM AR: SUBA!", _estilo);
        }

        static void Retangulo(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
