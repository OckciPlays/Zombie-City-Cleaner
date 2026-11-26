using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// No dono (06/11/2026, a água): com a câmera embaixo da água, a tela fica verde-azulada e escura (mais fundo, mais
    /// escuro); com o fôlego gastando, a barra "FÔLEGO" embaixo da mira (vermelha no fim). O fôlego é do anfitrião
    /// (<see cref="VidaJogador.Folego"/>). Desde 07/11/2026, embaixo da água o som fica abafado (um passa-baixa no ouvido
    /// do jogador: tudo o que ele ouve).
    /// </summary>
    public class AguaNaTela : MonoBehaviour
    {
        JogadorRede _rede;
        VidaJogador _vida;
        GUIStyle _estilo;
        float _u, _embaixo, _fundo;
        AudioLowPassFilter _abafado;
        bool _procurouOOuvido;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
        }

        void Update()
        {
            if (_rede == null || !_rede.IsOwner) return;
            var cam = _rede.cameraMundo;
            _fundo = 0f;
            if (cam != null && Agua.Nivel(cam.transform.position, out float nivel)) _fundo = nivel - cam.transform.position.y;
            _embaixo = Mathf.MoveTowards(_embaixo, _fundo > 0.02f ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            Abafar();
        }

        // o ouvido embaixo da água: o passa-baixa desce de 22 kHz a ~650 Hz (e sai de vez fora dela)
        void Abafar()
        {
            if (_abafado == null && !_procurouOOuvido)
            {
                _procurouOOuvido = true;
                var ouvido = _rede.cameraMundo != null ? _rede.cameraMundo.GetComponentInChildren<AudioListener>(true) : null;
                if (ouvido == null) ouvido = FindAnyObjectByType<AudioListener>();
                if (ouvido != null)
                {
                    _abafado = ouvido.GetComponent<AudioLowPassFilter>();
                    if (_abafado == null) _abafado = ouvido.gameObject.AddComponent<AudioLowPassFilter>();
                    _abafado.lowpassResonanceQ = 1f;
                    _abafado.enabled = false;
                }
            }
            if (_abafado == null) return;
            bool ligado = _embaixo > 0.01f;
            if (_abafado.enabled != ligado) _abafado.enabled = ligado;
            if (ligado) _abafado.cutoffFrequency = Mathf.Lerp(22000f, 650f, Mathf.Pow(_embaixo, 0.5f));
        }

        void OnDisable()
        {
            if (_abafado != null) _abafado.enabled = false;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rede == null || !_rede.IsOwner || _vida == null || !_vida.IsSpawned) return;
            if (MenuOnline.Cinema || ModoVideo.Ligado) return;
            float fundo = _fundo;
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
                // (26/11/2026, a revisão dos textos cortados: o padding de cima e de baixo do GUI.skin.label é em pixels e não
                // cresce com a tela; a 1280 × 720 a caixa de 18u deixava ~12 px para a linha de ~13,8 px, e o Ô de FÔLEGO
                // ainda saía cortado. Sem ele, a linha cabe nos 18u em qualquer tela, com o mesmo meio: é uma cópia, só daqui)
                _estilo.padding = new RectOffset(_estilo.padding.left, _estilo.padding.right, 0, 0);
            }
            float w = 160 * u, h = 7 * u, x = Screen.width * 0.5f - w / 2f, y = Screen.height * 0.5f + 46 * u;
            Retangulo(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.5f));
            var cor = folego > 30 ? new Color(0.45f, 0.8f, 1f, 0.95f) : new Color(0.95f, 0.3f, 0.25f, 0.95f);
            Retangulo(new Rect(x, y, w * folego / 100f, h), cor);
            _estilo.normal.textColor = cor;
            // (na tona, o fôlego cai só no nado rápido: sem ele, é só parar de correr)
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) 18u, com o fundo 1u acima da barra: a letra de 12
            // precisa de ~16,8u, e a ponta do circunflexo do Ô de FÔLEGO passava do alto da caixa de 16u
            GUI.Label(new Rect(x, y - 19 * u, w, 18 * u), folego > 0 || _fundo <= 0.02f ? "FÔLEGO" : "SEM AR: SUBA!", _estilo);
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
