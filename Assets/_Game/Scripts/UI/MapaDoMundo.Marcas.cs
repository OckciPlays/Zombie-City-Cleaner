using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// As marcas no mapa grande (03/11/2026, a pedido: "pings, marcações e anotações no mapa"; World/MundoDaPartida.Marcas.cs):
    ///  - o clique direito num ponto abre o menu: o ping (some em 20 s), a marca (o alfinete numa de 5 cores) e a anotação
    ///    (o alfinete com um texto de até 40 letras: escrever antes de escolher a cor);
    ///  - o clique numa marca (sem arrastar) abre a dela: mudar o texto e a cor, ou apagar;
    ///  - passando o mouse numa anotação, o texto aparece.
    /// As marcas são de todos (na rede) e vão no save do mundo; os pings, com o nome de quem marcou.
    /// </summary>
    public partial class MapaDoMundo
    {
        enum MenuDasMarcas { Nenhum, Novo, Editar }

        MenuDasMarcas _menu;
        Vector2 _pontoDoMenu;        // no mundo (x, z)
        Vector2 _ondeAbriu;          // na tela do OnGUI (origem em cima)
        ushort _marcaDoMenu;
        string _textoDoMenu = "";
        Vector2 _cliqueEm;
        bool _arrastou, _segurando;
        GUIStyle _caixa, _pequeno;
        Rect _areaDoMenu;

        /// <summary>O menu das marcas está aberto (o M, o Espaço e o arrastar não mexem no mapa: pode estar escrevendo).</summary>
        bool EscrevendoNasMarcas => _menu != MenuDasMarcas.Nenhum;

        static Vector2 NaTelaDoGui(Vector2 telaDoInput) => new Vector2(telaDoInput.x, Screen.height - telaDoInput.y);

        // no Update, com o mapa aberto: os cliques nas marcas; devolve verdade se o menu está aberto (o mapa não mexe)
        bool AtualizarAsMarcas(Mouse mouse, float escala)
        {
            if (mouse == null) return EscrevendoNasMarcas;
            var tela = mouse.position.ReadValue();
            var gui = NaTelaDoGui(tela);
            if (EscrevendoNasMarcas)
            {
                // um clique fora do menu fecha
                if ((mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && !_areaDoMenu.Contains(gui)) _menu = MenuDasMarcas.Nenhum;
                return true;
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _menu = MenuDasMarcas.Novo;
                _pontoDoMenu = NoMundo(tela, escala);
                _ondeAbriu = gui;
                _textoDoMenu = "";
                return true;
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _cliqueEm = tela;
                _arrastou = false;
                _segurando = true;
            }
            if (_segurando && mouse.leftButton.isPressed && (tela - _cliqueEm).sqrMagnitude > 25f) _arrastou = true;
            if (_segurando && mouse.leftButton.wasReleasedThisFrame)
            {
                _segurando = false;
                if (!_arrastou && MarcaEmbaixo(gui, escala, out var m))
                {
                    _menu = MenuDasMarcas.Editar;
                    _marcaDoMenu = m.id;
                    _pontoDoMenu = new Vector2(m.pos.x, m.pos.z);
                    _ondeAbriu = gui;
                    _textoDoMenu = m.texto.ToString();
                    return true;
                }
            }
            return false;
        }

        // a marca (não o ping) com o alfinete embaixo do mouse
        bool MarcaEmbaixo(Vector2 gui, float escala, out MarcaNoMapa achada)
        {
            achada = default;
            var lista = MarcasDoMapa.Lista;
            if (lista == null) return false;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            float melhor = (14f * u) * (14f * u);
            bool achou = false;
            foreach (var m in lista)
            {
                if (m.tipo == TipoDeMarca.Ping) continue;
                var p = NaTela(new Vector2(m.pos.x, m.pos.z), escala) + new Vector2(0f, -14f * u);
                float d = (p - gui).sqrMagnitude;
                if (d > melhor) continue;
                melhor = d;
                achada = m;
                achou = true;
            }
            return achou;
        }

        // dentro do grupo da janela do mapa: as marcas, os pings e a anotação embaixo do mouse
        void DesenharAsMarcas(Func<Vector2, Vector2> naJanela, float u, Rect janela)
        {
            var lista = MarcasDoMapa.Lista;
            if (lista == null) return;
            EstilosDasMarcas(u);
            var mundo = MundoDaPartida.Instancia;
            double agora = mundo != null ? mundo.Agora : 0.0;
            var mouse = Mouse.current;
            Vector2 doMouse = mouse != null ? NaTelaDoGui(mouse.position.ReadValue()) - janela.position : new Vector2(-999f, -999f);
            string dica = null;
            Vector2 ondeDica = default;
            var antes = GUI.color;
            foreach (var m in lista)
            {
                var p = naJanela(new Vector2(m.pos.x, m.pos.z));
                var cor = MarcasDoMapa.CorDa(m);
                if (m.tipo == TipoDeMarca.Ping)
                {
                    float resta = (float)(m.ate - agora);
                    float s = 20f * u * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f));
                    GUI.color = new Color(cor.r, cor.g, cor.b, Mathf.Clamp01(resta / 3f));
                    GUI.DrawTexture(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), MarcasDoMapa.Losango);
                    GUI.color = antes;
                    Contornado(new Rect(p.x + 12 * u, p.y - 9 * u, 160 * u, 18 * u), m.autor.ToString(), _pequeno, new Color(0f, 0f, 0f, 0.7f));
                    continue;
                }
                float lado = 24f * u;
                var caixa = new Rect(p.x - lado / 2f, p.y - lado * 0.95f, lado, lado);
                GUI.color = cor;
                GUI.DrawTexture(caixa, MarcasDoMapa.Alfinete);
                GUI.color = antes;
                if (m.tipo == TipoDeMarca.Anotacao && (caixa.center - doMouse).sqrMagnitude < (14f * u) * (14f * u))
                {
                    dica = $"{m.texto}\n<size={Mathf.RoundToInt(11 * u)}>— {m.autor}</size>";
                    ondeDica = caixa.center;
                }
            }
            if (dica != null && !EscrevendoNasMarcas)
            {
                var tamanho = _caixa.CalcSize(new GUIContent(dica));
                var r = new Rect(ondeDica.x + 14 * u, ondeDica.y - tamanho.y - 6 * u, Mathf.Max(tamanho.x, 120 * u), tamanho.y);
                GUI.color = new Color(0.08f, 0.08f, 0.07f, 0.92f);
                GUI.DrawTexture(new Rect(r.x - 6 * u, r.y - 4 * u, r.width + 12 * u, r.height + 8 * u), Texture2D.whiteTexture);
                GUI.color = antes;
                GUI.Label(r, dica, _caixa);
            }
        }

        void EstilosDasMarcas(float u)
        {
            int t = Mathf.RoundToInt(14 * u);
            if (_caixa != null && _caixa.fontSize == t) return;
            _caixa = new GUIStyle(GUI.skin.label) { fontSize = t, richText = true, wordWrap = false };
            _caixa.normal.textColor = new Color(0.95f, 0.93f, 0.86f);
            _pequeno = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * u), fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _pequeno.normal.textColor = new Color(1f, 0.85f, 0.6f);
        }

        // por cima do mapa, fora da janela: o menu do clique direito, ou o da marca clicada
        void MenuDasMarcasNaTela(float u)
        {
            if (!EscrevendoNasMarcas) return;
            EstilosDasMarcas(u);
            var local = JogadorRede.Local;
            var lista = MarcasDoMapa.Lista;
            if (local == null || lista == null)
            {
                _menu = MenuDasMarcas.Nenhum;
                return;
            }
            MarcaNoMapa marca = default;
            if (_menu == MenuDasMarcas.Editar)
            {
                bool existe = false;
                foreach (var m in lista) if (m.id == _marcaDoMenu) { marca = m; existe = true; }
                if (!existe)
                {
                    _menu = MenuDasMarcas.Nenhum;
                    return;
                }
            }
            float w = 260 * u, h = (_menu == MenuDasMarcas.Novo ? 196 : 186) * u;
            float x = Mathf.Clamp(_ondeAbriu.x + 10 * u, 6 * u, Screen.width - w - 6 * u);
            float y = Mathf.Clamp(_ondeAbriu.y - 20 * u, 6 * u, Screen.height - h - 6 * u);
            _areaDoMenu = new Rect(x, y, w, h);
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.95f);
            GUI.DrawTexture(_areaDoMenu, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.7f, 0.35f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, w, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;
            float px = x + 10 * u, pw = w - 20 * u, py = y + 8 * u;
            string titulo = _menu == MenuDasMarcas.Novo ? "Marcar aqui"
                : marca.tipo == TipoDeMarca.Anotacao ? $"A anotação de {marca.autor}" : $"A marca de {marca.autor}";
            GUI.Label(new Rect(px, py, pw, 20 * u), titulo, _pequeno);
            py += 24 * u;
            if (_menu == MenuDasMarcas.Novo)
            {
                if (GUI.Button(new Rect(px, py, pw, 26 * u), "Ping (some em 20 s)"))
                {
                    local.PedirPing(PontoNoChao(_pontoDoMenu));
                    _menu = MenuDasMarcas.Nenhum;
                }
                py += 32 * u;
            }
            GUI.Label(new Rect(px, py, pw, 18 * u), "Anotação (opcional, até 40 letras):", _caixa);
            py += 20 * u;
            GUI.SetNextControlName("anotacao");
            _textoDoMenu = GUI.TextField(new Rect(px, py, pw, 24 * u), _textoDoMenu ?? "", MarcasDoMapa.LetrasDaAnotacao);
            py += 30 * u;
            GUI.Label(new Rect(px, py, pw, 18 * u), _menu == MenuDasMarcas.Novo ? "A cor do alfinete (marca):" : "A cor (guarda a mudança):", _caixa);
            py += 20 * u;
            float lado = (pw - 4 * 6 * u) / 5f;
            for (int c = 0; c < MarcasDoMapa.Cores.Length; c++)
            {
                var r = new Rect(px + c * (lado + 6 * u), py, lado, 24 * u);
                GUI.color = MarcasDoMapa.Cores[c];
                bool clicou = GUI.Button(r, Texture2D.whiteTexture);
                GUI.color = antes;
                if (_menu == MenuDasMarcas.Editar && marca.cor == c) GUI.DrawTexture(new Rect(r.x + 3 * u, r.yMax + 2 * u, r.width - 6 * u, 3 * u), Texture2D.whiteTexture);
                if (!clicou) continue;
                if (_menu == MenuDasMarcas.Novo) local.PedirMarca(PontoNoChao(_pontoDoMenu), (byte)c, _textoDoMenu);
                else local.PedirEditarMarca(_marcaDoMenu, (byte)c, _textoDoMenu);
                _menu = MenuDasMarcas.Nenhum;
            }
            py += 36 * u;
            if (_menu == MenuDasMarcas.Editar)
            {
                if (GUI.Button(new Rect(px, py, pw * 0.48f, 26 * u), "Apagar"))
                {
                    local.PedirApagarMarca(_marcaDoMenu);
                    _menu = MenuDasMarcas.Nenhum;
                }
                if (GUI.Button(new Rect(px + pw * 0.52f, py, pw * 0.48f, 26 * u), "Fechar")) _menu = MenuDasMarcas.Nenhum;
            }
            else if (_menu == MenuDasMarcas.Novo && GUI.Button(new Rect(px, py, pw, 22 * u), "Cancelar")) _menu = MenuDasMarcas.Nenhum;
        }

        // o ponto do mapa no chão do mundo (a altura do relevo ali)
        Vector3 PontoNoChao(Vector2 p) => new Vector3(p.x, _dados != null ? _dados.Altura(p.x, p.y) : 0f, p.y);
    }
}
