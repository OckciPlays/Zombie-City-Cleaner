using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A tela de morte (03/11/2026, a pedido: "uma tela de morte melhor, com um botão de renascer depois de uns 5
    /// segundos"), no dono, posta pela <see cref="VidaJogador"/>. Com o jogador <see cref="VidaJogador.Caido"/>: a tela
    /// escurece, "VOCÊ MORREU", do que ele morreu (o nome de quem atirou, no fogo amigo), o que ficou no chão e a contagem;
    /// depois dela, o botão [Renascer] (ou Enter). É uma tela obrigatória do menu (<see cref="MenuOnline.Tela"/>): o mouse
    /// fica solto, o jogador não anda nem atira, e o ESC abre o menu por cima (de onde se sai da partida).
    /// </summary>
    public class TelaDeMorte : MonoBehaviour
    {
        VidaJogador _vida;
        bool _aberta;
        float _abriuEm;
        GUIStyle _titulo, _causa, _texto, _botao;

        /// <summary>A tela de morte está aberta nesta máquina.</summary>
        public static bool Aberta { get; private set; }

        void Awake() => _vida = GetComponent<VidaJogador>();

        void OnDisable() => Fechar();

        void Update()
        {
            bool caido = _vida != null && _vida.IsSpawned && _vida.Caido.Value;
            if (caido && !_aberta)
            {
                _aberta = Aberta = true;
                _abriuEm = Time.unscaledTime;
                MenuOnline.Tela(this, true, true);
            }
            else if (!caido && _aberta) Fechar();
            if (!_aberta || MenuOnline.Pausado) return;
            var teclado = Keyboard.current;
            if (teclado != null && (teclado.enterKey.wasPressedThisFrame || teclado.numpadEnterKey.wasPressedThisFrame)) _vida.PedirRenascer();
        }

        void Fechar()
        {
            if (!_aberta) return;
            _aberta = Aberta = false;
            MenuOnline.Tela(this, false);
        }

        void Estilos(float u)
        {
            int t = Mathf.RoundToInt(58 * u);
            if (_titulo != null && _titulo.fontSize == t) return;
            _titulo = EstilosDoHud.Rotulo(t, FontStyle.Bold, TextAnchor.MiddleCenter);
            _titulo.normal.textColor = new Color(0.78f, 0.16f, 0.12f);
            _causa = EstilosDoHud.Rotulo(Mathf.RoundToInt(22 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
            _causa.normal.textColor = new Color(0.9f, 0.86f, 0.78f);
            _texto = EstilosDoHud.Rotulo(Mathf.RoundToInt(16 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
            _texto.normal.textColor = new Color(0.68f, 0.64f, 0.58f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            // (com o ESC aberto por cima, só o menu: esta tela fica por cima do resto do HUD)
            if (!_aberta || ModoVideo.Ligado || MenuOnline.Pausado) return;
            GUI.depth = -40;
            float u = Screen.height / 720f;
            Estilos(u);
            // a tela escurece em 1,2 s (o vermelho bem escuro por cima do mundo)
            float a = Mathf.Clamp01((Time.unscaledTime - _abriuEm) / 1.2f);
            var antes = GUI.color;
            GUI.color = new Color(0.07f, 0.01f, 0.01f, 0.86f * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            float y = Screen.height * 0.3f;
            GUI.Label(new Rect(0, y, Screen.width, 70 * u), "VOCÊ MORREU", _titulo);
            y += 76 * u;
            GUI.Label(new Rect(0, y, Screen.width, 32 * u), _vida.Causa, _causa);
            y += 40 * u;
            GUI.Label(new Rect(0, y, Screen.width, 26 * u), _vida.PerdeuAsCoisas
                ? "A sua mochila e as suas roupas ficaram onde você caiu."
                : "No Dia 0, você não perde nada.", _texto);
            y += 56 * u;
            float falta = _vida.PodeRenascerEm - Time.time;
            float w = 240 * u, h = 52 * u;
            var botao = new Rect((Screen.width - w) / 2f, y, w, h);
            if (falta > 0f)
            {
                GUI.enabled = false;
                GUI.Button(botao, $"Renascer em {Mathf.CeilToInt(falta)}", _botao);
                GUI.enabled = true;
            }
            else if (GUI.Button(botao, "Renascer", _botao)) _vida.PedirRenascer();
            GUI.color = antes;
        }
    }
}
