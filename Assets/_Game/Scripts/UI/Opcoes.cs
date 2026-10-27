using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// As opções do jogo (ESC > Opções, ou no menu do começo), gravadas no computador (PlayerPrefs):
    /// - o volume geral (o do ouvido, de 0 a 100%);
    /// - o limite de quadros por segundo: VSync (acompanha o monitor), 30, 60, 120, 144 ou sem limite. Com um número, o
    ///   VSync desliga (senão o limite não vale);
    /// - mostrar o FPS no canto de cima, à direita (a média de meio segundo);
    /// - o menu do desenvolvedor (F1) também no jogo compilado (no Editor e nas builds de desenvolvimento ele sempre está;
    ///   numa partida, vale a opção do anfitrião, que é quem faz os pedidos do F1).
    /// O <see cref="MenuOnline"/> carrega, mede os quadros, desenha o painel e o contador.
    /// </summary>
    public static class Opcoes
    {
        public static readonly string[] NomesDosLimites = { "VSync", "30", "60", "120", "144", "Sem limite" };
        // o targetFrameRate de cada limite (-1 = sem limite; o VSync usa o do monitor)
        static readonly int[] Limites = { -1, 30, 60, 120, 144, -1 };
        const int ComVSync = 0;

        public static float Volume { get; private set; } = 1f;
        public static int Limite { get; private set; }
        public static bool MostrarFps { get; private set; }
        /// <summary>O menu do desenvolvedor (F1) ligado no jogo compilado.</summary>
        public static bool LiberarF1 { get; private set; }

        static int _quadros;
        static float _tempo, _fps, _gravarEm = -1f;

        public static void Carregar()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("opcoes.volume", 1f));
            Limite = Mathf.Clamp(PlayerPrefs.GetInt("opcoes.limite", ComVSync), 0, Limites.Length - 1);
            MostrarFps = PlayerPrefs.GetInt("opcoes.fps", 0) == 1;
            LiberarF1 = PlayerPrefs.GetInt("opcoes.f1", 0) == 1;
            AudioListener.volume = Volume;
            AplicarLimite();
        }

        static void AplicarLimite()
        {
            QualitySettings.vSyncCount = Limite == ComVSync ? 1 : 0;
            Application.targetFrameRate = Limites[Limite];
        }

        static void Gravar()
        {
            PlayerPrefs.SetFloat("opcoes.volume", Volume);
            PlayerPrefs.SetInt("opcoes.limite", Limite);
            PlayerPrefs.SetInt("opcoes.fps", MostrarFps ? 1 : 0);
            PlayerPrefs.SetInt("opcoes.f1", LiberarF1 ? 1 : 0);
            PlayerPrefs.Save();
            _gravarEm = -1f;
        }

        /// <summary>A cada quadro: conta os quadros e grava o volume um pouco depois de soltar o controle.</summary>
        public static void Medir(float dt)
        {
            _quadros++;
            _tempo += dt;
            if (_tempo >= 0.5f)
            {
                _fps = _quadros / _tempo;
                _quadros = 0;
                _tempo = 0f;
            }
            if (_gravarEm > 0f && Time.unscaledTime >= _gravarEm) Gravar();
        }

        /// <summary>O contador de FPS no canto de cima, à direita (se ligado).</summary>
        public static void Contador(float u)
        {
            if (!MostrarFps) return;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            var r = new Rect(Screen.width - 160 * u, 10 * u, 146 * u, 24 * u);
            string texto = $"{Mathf.RoundToInt(_fps)} FPS";
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), texto, estilo);
            estilo.normal.textColor = _fps >= 50f ? new Color(0.75f, 0.9f, 0.65f) : _fps >= 30f ? new Color(0.95f, 0.85f, 0.5f) : new Color(1f, 0.5f, 0.42f);
            GUI.Label(r, texto, estilo);
        }

        static readonly string[] NaoSim = { "Não", "Sim" };

        /// <summary>O painel das opções, no meio da tela a partir de y. Devolve true quando o jogador aperta Voltar.</summary>
        public static bool Painel(float u, float y, GUIStyle titulo, GUIStyle botao)
        {
            float w = 560 * u, x = (Screen.width - w) / 2f;
            var texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u) };
            texto.normal.textColor = new Color(0.85f, 0.83f, 0.76f);
            var direita = new GUIStyle(texto) { alignment = TextAnchor.UpperRight };
            var opcao = new GUIStyle(botao) { fontSize = Mathf.RoundToInt(16 * u) };
            // a opção escolhida em amarelo (o "apertado" da pele padrão quase não aparece)
            opcao.onNormal.textColor = opcao.onHover.textColor = opcao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            opcao.onNormal.background = opcao.active.background;
            GUI.Label(new Rect(0, y - 64 * u, Screen.width, 44 * u), "OPÇÕES", titulo);

            GUI.Label(new Rect(x, y, w, 26 * u), "Volume", texto);
            GUI.Label(new Rect(x, y, w, 26 * u), $"{Mathf.RoundToInt(Volume * 100f)}%", direita);
            var trilho = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = Mathf.Round(10 * u) };
            var cursor = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = Mathf.Round(24 * u), fixedWidth = Mathf.Round(16 * u) };
            float volume = GUI.HorizontalSlider(new Rect(x, y + 34 * u, w, 24 * u), Volume, 0f, 1f, trilho, cursor);
            if (!Mathf.Approximately(volume, Volume))
            {
                Volume = volume;
                AudioListener.volume = volume;
                _gravarEm = Time.unscaledTime + 0.6f;
            }
            y += 78 * u;

            GUI.Label(new Rect(x, y, w, 26 * u), "Limite de FPS", texto);
            int limite = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), Limite, NomesDosLimites, NomesDosLimites.Length, opcao);
            if (limite != Limite)
            {
                Limite = limite;
                AplicarLimite();
                Gravar();
            }
            y += 88 * u;

            // o contador: dois botões, como o limite (a caixinha de marcar do IMGUI fica minúscula na tela grande)
            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Mostrar o FPS na tela", texto);
            int fps = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), MostrarFps ? 1 : 0, NaoSim, 2, opcao);
            if ((fps == 1) != MostrarFps)
            {
                MostrarFps = fps == 1;
                Gravar();
            }
            y += 66 * u;

            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Menu do desenvolvedor (F1)", texto);
            int f1 = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), LiberarF1 ? 1 : 0, NaoSim, 2, opcao);
            if ((f1 == 1) != LiberarF1)
            {
                LiberarF1 = f1 == 1;
                Gravar();
            }
            y += 66 * u;

            bool voltar = GUI.Button(new Rect(x, y, w, 40 * u), "Voltar", botao);
            if (voltar && _gravarEm > 0f) Gravar();
            return voltar;
        }
    }
}
