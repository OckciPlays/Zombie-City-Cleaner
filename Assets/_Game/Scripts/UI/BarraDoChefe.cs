using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A barra de vida do Brutamonte (01/11/2026, a pedido: "uma barra de vida para ele no topo"): no alto da tela, o nome e a
    /// vida que sobra, quando ele está a até <see cref="Alcance"/> de quem joga aqui. Some aos poucos quando ele se afasta
    /// ou morre. Criada pelo ZombieManager em cada máquina.
    /// </summary>
    public class BarraDoChefe : MonoBehaviour
    {
        const float Alcance = 60f;
        static readonly Color Fundo = new Color(0.05f, 0.04f, 0.04f, 0.78f);
        static readonly Color Vida = new Color(0.62f, 0.08f, 0.07f, 1f);
        static readonly Color Perdida = new Color(0.95f, 0.75f, 0.35f, 0.9f);

        ZombieManager _m;
        float _alfa, _vidaVista = 1f, _perdidaVista = 1f, _proximaBusca;
        Zumbi _chefe;
        GUIStyle _nome;

        void Awake() => _m = GetComponent<ZombieManager>();

        void Update()
        {
            if (Time.unscaledTime >= _proximaBusca)
            {
                _proximaBusca = Time.unscaledTime + 0.4f;
                _chefe = Procurar();
            }
            bool mostra = _chefe != null && !_chefe.Morto && _chefe.isActiveAndEnabled;
            _alfa = Mathf.MoveTowards(_alfa, mostra ? 1f : 0f, Time.unscaledDeltaTime * (mostra ? 3f : 0.8f));
            if (_chefe != null && _chefe.isActiveAndEnabled)
            {
                float v = _chefe.Morto ? 0f : _chefe.FracaoDaVida;
                _vidaVista = Mathf.MoveTowards(_vidaVista, v, Time.unscaledDeltaTime * 2f);
                // o pedaço que acabou de sair fica claro um instante e desce devagar
                _perdidaVista = _perdidaVista < _vidaVista ? _vidaVista : Mathf.MoveTowards(_perdidaVista, _vidaVista, Time.unscaledDeltaTime * 0.35f);
            }
        }

        Zumbi Procurar()
        {
            if (_m == null) return null;
            var olho = JogadorRede.Local != null ? JogadorRede.Local.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            Zumbi melhor = null;
            float menor = Alcance;
            foreach (var z in _m.Visiveis())
            {
                if (z == null || z.tipo != TipoDeZumbi.Brutamonte || z.Morto) continue;
                float d = Vector3.Distance(olho, z.transform.position);
                if (d < menor)
                {
                    menor = d;
                    melhor = z;
                }
            }
            return melhor ?? (_chefe != null && _chefe.Morto ? _chefe : null);
        }

        void OnGUI()
        {
            if (_alfa <= 0.01f || MenuOnline.Aberto) return;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            if (_nome == null) _nome = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _nome.fontSize = Mathf.RoundToInt(17 * u);
            float largura = 520f * u, altura = 12f * u;
            var barra = new Rect((Screen.width - largura) / 2f, 52f * u, largura, altura);
            var cor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f * _alfa);
            GUI.Label(new Rect(barra.x + 2, barra.y - 26 * u + 2, barra.width, 22 * u), "O BRUTAMONTE", _nome);
            GUI.color = new Color(0.93f, 0.88f, 0.82f, _alfa);
            GUI.Label(new Rect(barra.x, barra.y - 26 * u, barra.width, 22 * u), "O BRUTAMONTE", _nome);
            Ret(new Rect(barra.x - 2 * u, barra.y - 2 * u, barra.width + 4 * u, barra.height + 4 * u), new Color(Fundo.r, Fundo.g, Fundo.b, Fundo.a * _alfa));
            Ret(new Rect(barra.x, barra.y, barra.width * _perdidaVista, barra.height), new Color(Perdida.r, Perdida.g, Perdida.b, Perdida.a * _alfa));
            Ret(new Rect(barra.x, barra.y, barra.width * _vidaVista, barra.height), new Color(Vida.r, Vida.g, Vida.b, _alfa));
            GUI.color = cor;
        }

        static void Ret(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
