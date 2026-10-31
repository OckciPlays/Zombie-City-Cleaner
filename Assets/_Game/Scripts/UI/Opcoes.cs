using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// As opções do jogo (a tela "Configurações": ESC > Configurações, ou no menu do começo), gravadas no computador
    /// (PlayerPrefs):
    /// - o volume geral (o do ouvido, de 0 a 100%);
    /// - por baixo do geral, o volume de cada grupo, de 0 a 100%: a música (a trilha do menu e a do jogo,
    ///   Music/MaestroDaTrilha.Volume), os zumbis (Zombies/ZombieManager.VolumeDosZumbis) e os sons de ambiente
    ///   (MaestroDaTrilha.VolumeDoAmbiente);
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
            Graficos.Carregar();   // a escala de renderização e a qualidade (a resolução, a Unity guarda sozinha)
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

        /// <summary>
        /// A cor de um texto em todos os estados. O rótulo da pele padrão tem outra cor com o mouse em cima: sem isto, o texto
        /// muda de cor sob o mouse, e a sombra (uma cópia escura, deslocada) clareia e vira uma imagem duplicada.
        /// </summary>
        public static void Cor(GUIStyle estilo, Color cor)
        {
            estilo.normal.textColor = estilo.hover.textColor = estilo.active.textColor = estilo.focused.textColor = cor;
        }

        /// <summary>O contador de FPS no canto de cima, à direita (se ligado).</summary>
        public static void Contador(float u)
        {
            if (!MostrarFps) return;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            var r = new Rect(Screen.width - 160 * u, 10 * u, 146 * u, 24 * u);
            string texto = $"{Mathf.RoundToInt(_fps)} FPS";
            Cor(estilo, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), texto, estilo);
            Cor(estilo, _fps >= 50f ? new Color(0.75f, 0.9f, 0.65f) : _fps >= 30f ? new Color(0.95f, 0.85f, 0.5f) : new Color(1f, 0.5f, 0.42f));
            GUI.Label(r, texto, estilo);
        }

        static readonly string[] NaoSim = { "Não", "Sim" };
        static readonly string[] Abas = { "Geral", "Gráficos" };
        static readonly string[] SobreAQualidade =
        {
            "Sombras só perto e simples, sem anti-serrilhado, texturas pela metade e menos detalhes ao longe. Para computadores fracos.",
            "Sombras até 30 m, anti-serrilhado leve (FXAA) e os detalhes sumindo um pouco mais perto.",
            "O jogo como foi feito: sombras até 40 m e anti-serrilhado SMAA.",
            "Sombras mais longe e mais definidas, anti-serrilhado SMAA com MSAA 4x e mais detalhes ao longe. Pesado.",
        };
        static int _aba;

        /// <summary>
        /// O painel das opções, no meio da tela a partir de y (o título fica acima): as abas Geral (o som, o FPS, o F1) e
        /// Gráficos (UI/Graficos). Devolve true quando o jogador aperta Voltar.
        /// </summary>
        public static bool Painel(float u, float y, GUIStyle titulo, GUIStyle botao)
        {
            float w = 560 * u, x = (Screen.width - w) / 2f;
            var texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u) };
            Cor(texto, new Color(0.85f, 0.83f, 0.76f));
            var direita = new GUIStyle(texto) { alignment = TextAnchor.UpperRight };
            var centro = new GUIStyle(texto) { alignment = TextAnchor.UpperCenter };
            var detalhe = new GUIStyle(texto) { fontSize = Mathf.RoundToInt(14 * u), wordWrap = true };
            Cor(detalhe, new Color(0.65f, 0.63f, 0.58f));
            var opcao = new GUIStyle(botao) { fontSize = Mathf.RoundToInt(16 * u) };
            // a opção escolhida em amarelo (o "apertado" da pele padrão quase não aparece)
            opcao.onNormal.textColor = opcao.onHover.textColor = opcao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            opcao.onNormal.background = opcao.active.background;
            GUI.Label(new Rect(0, y - 64 * u, Screen.width, 44 * u), "CONFIGURAÇÕES", titulo);
            _aba = GUI.SelectionGrid(new Rect(x, y, w, 36 * u), _aba, Abas, Abas.Length, opcao);
            y += 54 * u;

            // uma barra (o volume, a escala): o nome, a porcentagem e o trilho à vista (o da pele padrão some no fundo
            // escuro): cinza, e amarelo até o cursor. Gravada 0,6 s depois de soltar
            var trilho = new GUIStyle { fixedHeight = Mathf.Round(24 * u) };   // vazio: o trilho é desenhado abaixo
            var cursor = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = Mathf.Round(24 * u), fixedWidth = Mathf.Round(16 * u) };
            bool Barra(string nome, float valor, out float novo, float min = 0f, float max = 1f)
            {
                GUI.Label(new Rect(x, y, w, 26 * u), nome, texto);
                GUI.Label(new Rect(x, y, w, 26 * u), $"{Mathf.RoundToInt(valor * 100f)}%", direita);
                var area = new Rect(x, y + 32 * u, w, 24 * u);
                float meio = area.y + area.height / 2f, ate = cursor.fixedWidth / 2f + (w - cursor.fixedWidth) * Mathf.InverseLerp(min, max, valor);
                GUI.color = new Color(1f, 1f, 1f, 0.16f);
                GUI.DrawTexture(new Rect(x, meio - 2 * u, w, 4 * u), Texture2D.whiteTexture);
                GUI.color = new Color(0.98f, 0.85f, 0.45f, 0.8f);
                GUI.DrawTexture(new Rect(x, meio - 2 * u, ate, 4 * u), Texture2D.whiteTexture);
                GUI.color = Color.white;
                novo = GUI.HorizontalSlider(area, valor, min, max, trilho, cursor);
                y += 66 * u;
                if (Mathf.Approximately(novo, valor)) return false;
                _gravarEm = Time.unscaledTime + 0.6f;
                return true;
            }

            if (_aba == 1)
            {
                AbaDosGraficos(u, x, w, ref y, texto, centro, detalhe, opcao, Barra);
                return Voltar(u, x, w, y, botao);
            }

            // os volumes: o geral (o do ouvido) e, por baixo dele, cada grupo separado (a pedido em 29/10/2026): a música
            // (a trilha do menu e a do jogo, com o vento dela), os zumbis (as vozes e a pedra) e o ambiente (os grilos, os
            // cachorros, o que o vento mexe, os corvos)
            if (Barra("Volume geral", Volume, out float volume))
            {
                Volume = volume;
                AudioListener.volume = volume;
            }
            if (Barra("Música", Music.MaestroDaTrilha.Volume, out float musica)) Music.MaestroDaTrilha.Volume = musica;
            if (Barra("Zumbis", Zombies.ZombieManager.VolumeDosZumbis, out float zumbis)) Zombies.ZombieManager.VolumeDosZumbis = zumbis;
            if (Barra("Sons de ambiente", Music.MaestroDaTrilha.VolumeDoAmbiente, out float ambiente)) Music.MaestroDaTrilha.VolumeDoAmbiente = ambiente;
            y += 6 * u;

            GUI.Label(new Rect(x, y, w, 26 * u), "Limite de FPS", texto);
            int limite = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), Limite, NomesDosLimites, NomesDosLimites.Length, opcao);
            if (limite != Limite)
            {
                Limite = limite;
                AplicarLimite();
                Gravar();
            }
            y += 82 * u;

            // o contador: dois botões, como o limite (a caixinha de marcar do IMGUI fica minúscula na tela grande)
            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Mostrar o FPS na tela", texto);
            int fps = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), MostrarFps ? 1 : 0, NaoSim, 2, opcao);
            if ((fps == 1) != MostrarFps)
            {
                MostrarFps = fps == 1;
                Gravar();
            }
            y += 58 * u;

            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Menu do desenvolvedor (F1)", texto);
            int f1 = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), LiberarF1 ? 1 : 0, NaoSim, 2, opcao);
            if ((f1 == 1) != LiberarF1)
            {
                LiberarF1 = f1 == 1;
                Gravar();
            }
            y += 58 * u;
            return Voltar(u, x, w, y, botao);
        }

        static bool Voltar(float u, float x, float w, float y, GUIStyle botao)
        {
            bool voltar = GUI.Button(new Rect(x, y, w, 40 * u), "Voltar", botao);
            if (voltar && _gravarEm > 0f) Gravar();
            return voltar;
        }

        delegate bool BarraDoPainel(string nome, float valor, out float novo, float min = 0f, float max = 1f);

        // a aba Gráficos: a resolução, o modo da tela, a escala de renderização e a qualidade (UI/Graficos)
        static void AbaDosGraficos(float u, float x, float w, ref float y, GUIStyle texto, GUIStyle centro, GUIStyle detalhe, GUIStyle opcao, BarraDoPainel barra)
        {
            // a resolução: < 2560 × 1440 >, da lista do monitor
            var lista = UI.Graficos.Resolucoes();
            var atual = new Vector2Int(Screen.width, Screen.height);
            int i = Mathf.Max(0, lista.IndexOf(atual));
            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Resolução", texto);
            float bx = x + w - 300 * u;
            if (GUI.Button(new Rect(bx, y, 44 * u, 36 * u), "<", opcao) && i > 0) UI.Graficos.MudarResolucao(lista[i - 1]);
            GUI.Label(new Rect(bx + 44 * u, y + 6 * u, 212 * u, 26 * u), $"{atual.x} × {atual.y}", centro);
            if (GUI.Button(new Rect(bx + 256 * u, y, 44 * u, 36 * u), ">", opcao) && i < lista.Count - 1) UI.Graficos.MudarResolucao(lista[i + 1]);
            y += 44 * u;
            if (Application.isEditor)
            {
                GUI.Label(new Rect(x, y, w, 22 * u), "No Editor, a resolução e o modo são os da janela Game; valem no jogo compilado.", detalhe);
                y += 26 * u;
            }
            y += 8 * u;

            GUI.Label(new Rect(x, y, w, 26 * u), "Modo da tela", texto);
            int modo = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), UI.Graficos.Modo, UI.Graficos.NomesDosModos, UI.Graficos.NomesDosModos.Length, opcao);
            if (modo != UI.Graficos.Modo) UI.Graficos.MudarModo(modo);
            y += 86 * u;

            // a escala de renderização ("aumentar a renderização do jogador")
            if (barra("Escala de renderização", UI.Graficos.Escala, out float escala, UI.Graficos.EscalaMinima, UI.Graficos.EscalaMaxima))
                UI.Graficos.MudarEscala(escala);
            GUI.Label(new Rect(x, y - 8 * u, w, 22 * u), "Acima de 100%: o 3D mais nítido e mais pesado. Abaixo: mais leve e borrado.", detalhe);
            y += 26 * u;

            GUI.Label(new Rect(x, y, w, 26 * u), "Qualidade gráfica", texto);
            int qualidade = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), UI.Graficos.Qualidade, UI.Graficos.NomesDaQualidade, UI.Graficos.NomesDaQualidade.Length, opcao);
            if (qualidade != UI.Graficos.Qualidade) UI.Graficos.MudarQualidade(qualidade);
            y += 76 * u;
            GUI.Label(new Rect(x, y, w, 44 * u), SobreAQualidade[UI.Graficos.Qualidade], detalhe);
            y += 50 * u;

            // a distância de visão dos zumbis (a qualidade traz a dela; dá para mudar à parte)
            int visao = UI.Graficos.Visao;
            GUI.Label(new Rect(x, y, w, 26 * u), "Distância de visão (zumbis)", texto);
            GUI.Label(new Rect(x, y, w, 26 * u), $"até {UI.Graficos.Metros(visao):0} m", new GUIStyle(texto) { alignment = TextAnchor.UpperRight });
            int novaVisao = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), visao, UI.Graficos.NomesDaVisao, UI.Graficos.NomesDaVisao.Length, opcao);
            if (novaVisao != visao)
            {
                UI.Graficos.MudarVisao(novaVisao);
                _gravarEm = Time.unscaledTime + 0.6f;
            }
            y += 74 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Mais longe: os zumbis aparecem antes, na neblina, e nascem mais longe de você.", detalhe);
            y += 30 * u;
        }
    }
}
