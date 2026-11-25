using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>Uma pergunta da conversa: o texto do botão, as respostas (uma sorteada), o que ela faz e se fecha a conversa.</summary>
    public sealed class Pergunta
    {
        public string texto;
        public string[] respostas;
        public System.Action acao;
        public bool fecha;

        public Pergunta(string texto, params string[] respostas)
        {
            this.texto = texto;
            this.respostas = respostas;
        }
    }

    /// <summary>Quem conversa (os NPCs do Dia 0, o Capitão): o nome, a saudação, as perguntas de agora e onde está.</summary>
    public interface IConversavel
    {
        string NomeNaConversa { get; }
        string Saudacao();
        List<Pergunta> Perguntas();
        Vector3 Onde { get; }
        bool PodeConversar { get; }
        /// <summary>A conversa começou (true) ou acabou (false): o NPC para e olha para quem fala, e depois volta.</summary>
        void AoConversar(bool comecou);
    }

    /// <summary>
    /// O E num NPC que conversa (24/11/2026, o Pedro: "ter algumas interações com os npcs (tipo um chat de conversa com algumas
    /// perguntas que daria para gente fazer aos vizinhos, aos atendentes e alguns outros npcs)"): o colisor da mira (um
    /// gatilho, para não esbarrar) e a dica.
    /// </summary>
    public class ConversaComNpc : Interagivel
    {
        public IConversavel quem;

        /// <summary>Põe a conversa no objeto (com o gatilho da mira em volta do corpo).</summary>
        public static ConversaComNpc Por(GameObject go, IConversavel quem)
        {
            var c = go.AddComponent<ConversaComNpc>();
            c.quem = quem;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.center = new Vector3(0f, 0.95f, 0f);
            cap.height = 1.8f;
            cap.radius = 0.35f;
            return c;
        }

        public override string Dica(ArmaJogador quemUsa) =>
            quem == null || !quem.PodeConversar || TelaDeConversa.Aberta ? null : $"[E] Conversar com {quem.NomeNaConversa}";

        public override void Usar(ArmaJogador quemUsa)
        {
            if (quem != null && quem.PodeConversar) TelaDeConversa.Abrir(quem);
        }
    }

    /// <summary>
    /// A tela da conversa (24/11/2026): embaixo, como as falas do cinema, o nome de quem fala, a fala dele e as perguntas
    /// numeradas (o clique ou as teclas 1 a 9). Fecha com ESC, E ou TAB, com a última pergunta ("Tchau"), longe dele (4 m)
    /// ou quando ele não pode mais conversar. Só nesta máquina: as conversas não vão pela rede (o que elas fazem, sim, pelo
    /// próprio NPC: o Capitão).
    /// </summary>
    public class TelaDeConversa : MonoBehaviour
    {
        static TelaDeConversa _tela;
        IConversavel _quem;
        string _fala;
        List<Pergunta> _perguntas = new List<Pergunta>();
        bool _aberta;
        float _abriuEm, _u;
        GUIStyle _nome, _texto, _botao;

        public static bool Aberta => _tela != null && _tela._aberta;

        public static void Abrir(IConversavel quem)
        {
            if (quem == null) return;
            if (_tela == null) _tela = new GameObject("Tela da conversa").AddComponent<TelaDeConversa>();
            if (_tela._aberta) _tela.Fechar();
            _tela._quem = quem;
            _tela._abriuEm = Time.unscaledTime;
            _tela._fala = quem.Saudacao();
            _tela._perguntas = quem.Perguntas() ?? new List<Pergunta>();
            _tela._aberta = true;
            quem.AoConversar(true);
            MenuOnline.Tela(_tela, true);
            MenuOnline.FecharTelas += _tela.Fechar;
        }

        void Fechar()
        {
            if (!_aberta) return;
            _aberta = false;
            MenuOnline.Tela(this, false);
            MenuOnline.FecharTelas -= Fechar;
            _quem?.AoConversar(false);
            _quem = null;
        }

        void OnDestroy() => Fechar();

        void Update()
        {
            if (!_aberta) return;
            var j = JogadorRede.Local;
            if (j == null || _quem == null || !_quem.PodeConversar || (j.transform.position - _quem.Onde).sqrMagnitude > 4.5f * 4.5f)
            {
                Fechar();
                return;
            }
            var k = Keyboard.current;
            if (k == null || Time.unscaledTime - _abriuEm < 0.2f) return;
            if (k.eKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)
            {
                Fechar();
                return;
            }
            Key[] teclas = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < teclas.Length && i < _perguntas.Count; i++)
                if (k[teclas[i]].wasPressedThisFrame)
                {
                    Escolher(i);
                    return;
                }
        }

        void Escolher(int i)
        {
            if (i < 0 || i >= _perguntas.Count) return;
            var p = _perguntas[i];
            if (p.respostas != null && p.respostas.Length > 0) _fala = p.respostas[Random.Range(0, p.respostas.Length)];
            p.acao?.Invoke();
            if (p.fecha)
            {
                Fechar();
                return;
            }
            if (_quem != null) _perguntas = _quem.Perguntas() ?? new List<Pergunta>();
        }

        void Estilos(float u)
        {
            if (_nome != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _nome = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), fontStyle = FontStyle.Bold };
            _nome.normal.textColor = new Color(0.95f, 0.83f, 0.45f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * u), wordWrap = true };
            _texto.normal.textColor = new Color(0.95f, 0.94f, 0.9f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _botao.padding = new RectOffset(Mathf.RoundToInt(12 * u), Mathf.RoundToInt(8 * u), 4, 4);
        }

        void OnGUI()
        {
            if (!_aberta || _quem == null) return;
            GUI.depth = -12;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            float w = 820 * u, x0 = (Screen.width - w) / 2f;
            float alturaDaFala = _texto.CalcHeight(new GUIContent(_fala ?? ""), w - 40 * u);
            float linha = 38 * u;
            float h = 24 * u + 30 * u + alturaDaFala + 16 * u + _perguntas.Count * (linha + 6 * u) + 30 * u;
            float y0 = Screen.height - h - 40 * u;
            Caixa(new Rect(x0, y0, w, h), new Color(0.05f, 0.05f, 0.05f, 0.88f));
            Caixa(new Rect(x0, y0, w, 3 * u), new Color(0.72f, 0.58f, 0.2f, 0.95f));
            float y = y0 + 14 * u;
            GUI.Label(new Rect(x0 + 20 * u, y, w - 40 * u, 26 * u), _quem.NomeNaConversa, _nome);
            y += 30 * u;
            GUI.Label(new Rect(x0 + 20 * u, y, w - 40 * u, alturaDaFala), _fala ?? "", _texto);
            y += alturaDaFala + 16 * u;
            for (int i = 0; i < _perguntas.Count; i++)
            {
                if (GUI.Button(new Rect(x0 + 20 * u, y, w - 40 * u, linha), $"{i + 1}.  {_perguntas[i].texto}", _botao))
                {
                    Escolher(i);
                    return;
                }
                y += linha + 6 * u;
            }
            var dica = new GUIStyle(_botao) { fontSize = Mathf.RoundToInt(12 * u), alignment = TextAnchor.MiddleRight };
            dica.normal.background = null;
            dica.normal.textColor = new Color(0.65f, 0.63f, 0.58f);
            GUI.Label(new Rect(x0 + 20 * u, y0 + h - 28 * u, w - 40 * u, 22 * u), "1 a 9 ou o clique escolhem  ·  E, TAB ou ESC fecham", dica);
        }

        static Texture2D _branco;
        static void Caixa(Rect r, Color cor)
        {
            if (_branco == null) _branco = Texture2D.whiteTexture;
            var antes = GUI.color;
            GUI.color = cor;
            GUI.DrawTexture(r, _branco);
            GUI.color = antes;
        }
    }
}
