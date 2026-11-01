using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A tela de carregamento da cena Mundo (OnGUI, por cima de tudo): o nome do mundo, o tamanho, a semente, a etapa e a
    /// barra, da entrada na partida até o jogador local nascer. Se a geração falhar, mostra o erro e o botão de voltar ao
    /// menu. Depois de nascer, mostra por 12 s o aviso de mundo diferente do anfitrião, se houver.
    /// </summary>
    public class TelaDeCarregamento : MonoBehaviour
    {
        GUIStyle _titulo, _texto, _etapa, _aviso, _botao;
        float _uDosEstilos;
        float _avisoAte;
        string _avisoMostrado;

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _titulo.normal.textColor = new Color(0.85f, 0.82f, 0.72f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _texto.normal.textColor = new Color(0.72f, 0.72f, 0.7f);
            _etapa = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(18 * u) };
            _etapa.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            _aviso = new GUIStyle(_texto);
            _aviso.normal.textColor = new Color(1f, 0.55f, 0.45f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(18 * u) };
        }

        void OnGUI()
        {
            var mundo = MundoDaPartida.Instancia;
            var sessao = SessaoOnline.Instancia;
            if (mundo == null || sessao == null || sessao.Atual != SessaoOnline.Estado.Conectado || ModoVideo.Ligado) return;
            // no Dia 0 (e na passagem dos 5 anos), o DiaZeroDaPartida desenha as telas dele
            if (mundo.NoDiaZero || DiaZero.DiaZeroDaPartida.NaPassagem) return;
            GUI.depth = -20;   // por cima do menu e do HUD
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            if (JogadorRede.Local != null)
            {
                Aviso(mundo, u);
                return;
            }

            GUI.color = new Color(0.035f, 0.035f, 0.035f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float y = Screen.height * 0.36f, w = 520 * u, x = (Screen.width - w) / 2f;
            string nome = mundo.Semente != 0 ? mundo.Nome : "Entrando no mundo";
            GUI.Label(new Rect(0, y, Screen.width, 44 * u), nome.ToUpperInvariant(), _titulo);
            y += 46 * u;
            if (mundo.Semente != 0)
                GUI.Label(new Rect(0, y, Screen.width, 24 * u), $"{Tamanhos.NomeComLado(mundo.Tamanho)}  ·  semente {mundo.Semente}", _texto);
            y += 52 * u;

            if (!string.IsNullOrEmpty(mundo.Erro))
            {
                GUI.Label(new Rect(x - 60 * u, y, w + 120 * u, 50 * u), mundo.Erro, _aviso);
                if (GUI.Button(new Rect(x + w * 0.25f, y + 64 * u, w * 0.5f, 40 * u), "Voltar ao menu", _botao)) sessao.Sair();
                return;
            }
            GUI.Label(new Rect(0, y, Screen.width, 28 * u), mundo.Etapa + "…", _etapa);
            y += 38 * u;
            // a barra
            GUI.color = new Color(0.2f, 0.2f, 0.19f);
            GUI.DrawTexture(new Rect(x, y, w, 8 * u), Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f);
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(mundo.Progresso), 8 * u), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 28 * u;
            if (mundo.Pronto && !mundo.IsServer)
                GUI.Label(new Rect(0, y, Screen.width, 24 * u), "O mundo está pronto aqui; o anfitrião vai colocar você nele.", _texto);
        }

        // o aviso do mundo diferente, por 12 s depois de nascer
        void Aviso(MundoDaPartida mundo, float u)
        {
            if (string.IsNullOrEmpty(mundo.Aviso)) return;
            if (_avisoMostrado != mundo.Aviso)
            {
                _avisoMostrado = mundo.Aviso;
                _avisoAte = Time.time + 12f;
            }
            if (Time.time > _avisoAte) return;
            var r = new Rect(Screen.width * 0.2f, 44 * u, Screen.width * 0.6f, 50 * u);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, mundo.Aviso, _aviso);
        }
    }
}
