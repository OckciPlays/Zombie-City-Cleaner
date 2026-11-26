using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// As opções do jogo (a tela "Configurações": ESC > Configurações, ou no menu do começo), gravadas no computador
    /// (PlayerPrefs):
    /// - o volume geral (o do ouvido, de 0 a 100%);
    /// - por baixo do geral, o volume de cada grupo, de 0 a 100%: a música (a trilha do menu e a do jogo,
    ///   Music/MaestroDaTrilha.Volume), os zumbis (Zombies/ZombieManager.VolumeDosZumbis) e os sons de ambiente
    ///   (MaestroDaTrilha.VolumeDoAmbiente); desde 20/11/2026 todos na aba Som, com os canais novos do mixer (os animais, as
    ///   armas, os golpes, os passos, o corpo, o clima, a água, o fogo, os veículos, as vozes, os objetos e os avisos:
    ///   Combat/MixerDeSom), cada um com o botão de uma amostra;
    /// - o limite de quadros por segundo: VSync (acompanha o monitor), 30, 60, 120, 144 ou sem limite. Com um número, o
    ///   VSync desliga (senão o limite não vale);
    /// - mostrar o FPS no canto de cima, à direita (a média de meio segundo);
    /// - o menu do desenvolvedor (F1) também no jogo compilado (no Editor e nas builds de desenvolvimento ele sempre está;
    ///   numa partida, vale a opção do anfitrião, que é quem faz os pedidos do F1);
    /// - menos balanço da câmera (05/11/2026, para quem enjoa): o mancar, o tremor da mira e o calafrio da febre a 35%,
    ///   e a febre sem a visão turva (o Player/EfeitosDoCorpo lê).
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
        /// <summary>Menos balanço da câmera: o mancar, o tremor da mira e o calafrio a 35%, sem a visão turva da febre.</summary>
        public static bool CameraCalma { get; private set; }

        static int _quadros;
        static float _tempo, _fps, _gravarEm = -1f;

        public static void Carregar()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("opcoes.volume", 1f));
            Limite = Mathf.Clamp(PlayerPrefs.GetInt("opcoes.limite", ComVSync), 0, Limites.Length - 1);
            MostrarFps = PlayerPrefs.GetInt("opcoes.fps", 0) == 1;
            LiberarF1 = PlayerPrefs.GetInt("opcoes.f1", 0) == 1;
            CameraCalma = PlayerPrefs.GetInt("opcoes.calma", 0) == 1;
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
            PlayerPrefs.SetInt("opcoes.calma", CameraCalma ? 1 : 0);
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

        /// <summary>
        /// (26/11/2026, a pedido do Pedro: os textos cortados pela metade) O texto que cabe numa linha da largura dada, no estilo
        /// dado: o que não cabe termina em reticências, em vez de sair cortado no meio de uma letra na borda da caixa (ou de
        /// quebrar numa segunda linha que não cabe nela). A largura é a da caixa (o CalcSize já conta o padding). Mede o texto
        /// de novo a cada letra tirada: quem chama a cada quadro guarda o resultado.
        /// </summary>
        public static string Caber(string texto, GUIStyle estilo, float largura)
        {
            if (string.IsNullOrEmpty(texto) || estilo.CalcSize(new GUIContent(texto)).x <= largura) return texto;
            for (int n = texto.Length - 1; n > 1; n--)
            {
                string t = texto.Substring(0, n).TrimEnd(' ', ',', '·') + "…";
                if (estilo.CalcSize(new GUIContent(t)).x <= largura) return t;
            }
            return "…";
        }

        static string _textoDoFps;
        static int _fpsNoTexto = -1;

        /// <summary>O contador de FPS no canto de cima, à direita (se ligado).</summary>
        public static void Contador(float u)
        {
            if (!MostrarFps) return;
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.UpperRight);
            var r = new Rect(Screen.width - 160 * u, 10 * u, 146 * u, 24 * u);
            int fps = Mathf.RoundToInt(_fps);
            if (fps != _fpsNoTexto || _textoDoFps == null) { _fpsNoTexto = fps; _textoDoFps = $"{fps} FPS"; }
            string texto = _textoDoFps;
            Cor(estilo, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), texto, estilo);
            Cor(estilo, _fps >= 50f ? new Color(0.75f, 0.9f, 0.65f) : _fps >= 30f ? new Color(0.95f, 0.85f, 0.5f) : new Color(1f, 0.5f, 0.42f));
            GUI.Label(r, texto, estilo);
        }

        static readonly string[] NaoSim = { "Não", "Sim" };
        static readonly string[] Abas = { "Geral", "Som", "Gráficos" };   // (20/11/2026: a aba Som, o mixer detalhado)
        static readonly string[] SobreAQualidade =
        {
            "Sombras duras e só perto, sem a sombra nos cantos (SSAO) nem anti-serrilhado, texturas pela metade e o mundo desenhado até 70 m. Para computadores fracos.",
            "Sombras até 30 m e menos suaves, a sombra nos cantos (SSAO) mais simples, anti-serrilhado leve (FXAA) e o mundo desenhado até 90 m.",
            "O jogo como foi feito: sombras até 40 m e anti-serrilhado SMAA.",
            "Sombras mais longe e mais definidas, anti-serrilhado SMAA com MSAA 4x e mais detalhes ao longe. Pesado.",
        };
        static int _aba;

        /// <summary>Para os testes: a aba aberta (0 Geral, 1 Som, 2 Gráficos).</summary>
        public static int AbaParaTeste { get => _aba; set => _aba = Mathf.Clamp(value, 0, Abas.Length - 1); }

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
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a caixa do título mais alta que a linha da letra (na
            // de 44u, para a letra de 38u, o til do Õ e a cedilha do Ç ficavam rentes às bordas e podiam perder a ponta), com o
            // mesmo meio de antes (y − 42u); acaba em y − 14u, antes das abas
            GUI.Label(new Rect(0, y - 70 * u, Screen.width, 56 * u), "CONFIGURAÇÕES", titulo);
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

            if (_aba == 2)
            {
                AbaDosGraficos(u, x, w, ref y, texto, centro, detalhe, opcao, Barra);
                return Voltar(u, x, w, y, botao);
            }
            if (_aba == 1)
            {
                AbaDoSom(u, ref y, detalhe);
                return Voltar(u, x, w, y, botao);
            }

            // os volumes (desde 20/11/2026) ficam todos na aba Som: os de antes (o geral, a música, os zumbis e o ambiente)
            // e os canais novos do mixer
            GUI.Label(new Rect(x, y, w, 26 * u), "O volume geral e os de cada som ficam na aba Som.", detalhe);
            y += 40 * u;

            GUI.Label(new Rect(x, y, w, 26 * u), "Limite de FPS", texto);
            int limite = GUI.SelectionGrid(new Rect(x, y + 32 * u, w, 36 * u), Limite, NomesDosLimites, NomesDosLimites.Length, QueCabe(opcao, NomesDosLimites, w));
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
            y += 50 * u;

            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Menu do desenvolvedor (F1)", texto);
            int f1 = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), LiberarF1 ? 1 : 0, NaoSim, 2, opcao);
            if ((f1 == 1) != LiberarF1)
            {
                LiberarF1 = f1 == 1;
                Gravar();
            }
            y += 50 * u;

            // para quem enjoa (05/11/2026): o mancar, o tremor da mira e o calafrio da febre bem menores, sem a visão turva
            GUI.Label(new Rect(x, y + 6 * u, w, 26 * u), "Menos balanço da câmera", texto);
            int calma = GUI.SelectionGrid(new Rect(x + w - 200 * u, y, 200 * u, 36 * u), CameraCalma ? 1 : 0, NaoSim, 2, opcao);
            if ((calma == 1) != CameraCalma)
            {
                CameraCalma = calma == 1;
                Gravar();
            }
            y += 50 * u;
            return Voltar(u, x, w, y, botao);
        }

        // ------------------------------------------------------------------ a aba Som (o mixer detalhado)

        /// <summary>Uma barra do mixer: o nome, do que ela cuida, o volume (ler e mudar) e a amostra do botão ▶.</summary>
        class LinhaDoMixer
        {
            public string nome, sobre;
            public System.Func<float> ler;
            public System.Action<float> mudar;
            public System.Func<AudioClip[]> amostra;
            public float intervalo, ate = 2.5f;
            public int vezes = 1;
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) o "do que ela cuida" que cabe na linha (com as
            // reticências), guardado para a largura e a letra em que foi medido
            public float larguraDoSobre = -1f;
            public int letraDoSobre;
            public string sobreQueCabe;
        }

        static (string grupo, LinhaDoMixer[] linhas)[][] _colunas;

        static LinhaDoMixer Canal(string nome, string sobre, Combat.CanalDoSom c, System.Func<AudioClip[]> amostra, int vezes = 1, float intervalo = 0f) =>
            new LinhaDoMixer
            {
                nome = nome, sobre = sobre, ler = () => Combat.MixerDeSom.Volume(c), mudar = v => Combat.MixerDeSom.Mudar(c, v),
                amostra = amostra, vezes = vezes, intervalo = intervalo,
            };

        static AudioClip[] Um(AudioClip c) => c != null ? new[] { c } : null;

        // as colunas da aba: os volumes de antes (o geral, a música, o ambiente e os zumbis) e os canais novos
        static (string grupo, LinhaDoMixer[] linhas)[][] Colunas() => _colunas ??= new[]
        {
            new (string grupo, LinhaDoMixer[] linhas)[]
            {
                ("GERAL", new[]
                {
                    new LinhaDoMixer { nome = "Volume geral", sobre = "tudo o que o jogo toca", ler = () => Volume,
                        mudar = v => { Volume = v; AudioListener.volume = v; } },
                    new LinhaDoMixer { nome = "Música", sobre = "a trilha do menu e a do jogo, com o vento dela",
                        ler = () => Music.MaestroDaTrilha.Volume, mudar = v => Music.MaestroDaTrilha.Volume = v },
                }),
                ("O MUNDO", new[]
                {
                    new LinhaDoMixer { nome = "Sons de ambiente", sobre = "pássaros, corvos, coruja, grilos, galhos",
                        ler = () => Music.MaestroDaTrilha.VolumeDoAmbiente, mudar = v => Music.MaestroDaTrilha.VolumeDoAmbiente = v,
                        amostra = () => Combat.SonsGravados.Pasta("Ambiente/Passaros") },
                    Canal("Clima", "chuva, trovões, poças, o vento do temporal", Combat.CanalDoSom.Clima,
                        () => Combat.SonsGravados.Varios("Clima", "trovao_medio_")),
                    Canal("Água", "o lago, os rios, nadar, o caiaque", Combat.CanalDoSom.Agua,
                        () => Combat.SonsGravados.Varios("Agua", "Agua_Entrar")),
                    Canal("Fogo", "fogueiras, tochas, o galão, o isqueiro", Combat.CanalDoSom.Fogo,
                        () => Um(Resources.Load<AudioClip>("Fogo/fogueira"))),
                    Canal("Animais", "lobos, coiotes, ursos, veados, aves e os outros", Combat.CanalDoSom.Animais,
                        () => Combat.SonsGravados.Pasta("Bichos/Uivo")),
                }),
            },
            new (string grupo, LinhaDoMixer[] linhas)[]
            {
                ("O PERIGO", new[]
                {
                    new LinhaDoMixer { nome = "Zumbis", sobre = "as vozes do Oco e dos outros, a pedra",
                        ler = () => Zombies.ZombieManager.VolumeDosZumbis, mudar = v => Zombies.ZombieManager.VolumeDosZumbis = v },
                    Canal("Armas", "tiros, ecos, recarga, cápsulas", Combat.CanalDoSom.Armas,
                        () => Combat.SonsGravados.Tiros(Combat.TipoDeArma.Pistola)),
                    Canal("Golpes e lutas", "socos, facas, bastões, quedas", Combat.CanalDoSom.Golpes,
                        () => Combat.SonsGravados.Golpes("SocoCarne"), 2, 0.45f),
                }),
                ("VOCÊ", new[]
                {
                    Canal("Passos", "os seus e os dos outros, até na água rasa", Combat.CanalDoSom.Passos,
                        () => Combat.SonsGravados.Passos(Combat.Superficie.Concreto, false), 4, 0.48f),
                    Canal("Corpo e mente", "coração, respiração, comer e beber, curativos", Combat.CanalDoSom.Corpo,
                        () => Combat.SonsGravados.Varios("Corpo", "coracao_"), 3, 0.8f),
                    Canal("Vozes", "a sua e as dos sobreviventes", Combat.CanalDoSom.Vozes,
                        () => Resources.LoadAll<AudioClip>("Vozes/Homem1/Susto")),
                }),
                ("AS COISAS", new[]
                {
                    Canal("Veículos", "motores, buzinas, portas do carro, alarmes, motos", Combat.CanalDoSom.Veiculos,
                        () => Combat.SonsGravados.Varios("Carro", "buzina")),
                    Canal("Objetos e casa", "portas, móveis, armadilhas, machado, pesca, TV", Combat.CanalDoSom.Objetos,
                        () => Combat.SonsGravados.Varios("Portas", "abrir_")),
                    Canal("Avisos e bipes", "o ping do mapa e as falas", Combat.CanalDoSom.Avisos,
                        () => Um(MarcasDoMapa.Bipe), 2, 0.5f),
                }),
            },
        };

        /// <summary>
        /// A aba Som (20/11/2026, a pedido: "Quero um mixer de som bem legal e detalhado ... deixe os que existem hoje também,
        /// apenas adicione mais"): duas colunas com os grupos; cada barra tem o nome, do que ela cuida, a porcentagem e o ▶
        /// (uma amostra no volume escolhido). Os volumes são deste computador (PlayerPrefs), gravados ao soltar.
        /// </summary>
        static void AbaDoSom(float u, ref float y, GUIStyle detalhe)
        {
            float W = Mathf.Min(980 * u, Screen.width - 24 * u), X = (Screen.width - W) / 2f, vao = 30 * u, cw = (W - vao) / 2f;
            var nome = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            Cor(nome, new Color(0.9f, 0.88f, 0.8f));
            var sobre = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * u), clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Cor(sobre, new Color(0.62f, 0.6f, 0.55f));
            var pct = new GUIStyle(nome) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Normal };
            var grupo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), fontStyle = FontStyle.Bold };
            Cor(grupo, new Color(0.98f, 0.85f, 0.45f));
            var tocar = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(11 * u), padding = new RectOffset(0, 0, 0, 0) };
            var trilho = new GUIStyle { fixedHeight = Mathf.Round(16 * u) };
            var cursor = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = Mathf.Round(16 * u), fixedWidth = Mathf.Round(12 * u) };

            GUI.Label(new Rect(X, y - 6 * u, W, 22 * u), "▶ toca uma amostra no volume escolhido. Os volumes ficam só neste computador.", detalhe);
            float topo = y + 20 * u, fim = topo;
            var colunas = Colunas();
            for (int k = 0; k < colunas.Length; k++)
            {
                float cx = X + k * (cw + vao), cy = topo;
                foreach (var (titulo, linhas) in colunas[k])
                {
                    GUI.Label(new Rect(cx, cy, cw, 20 * u), titulo, grupo);
                    GUI.color = new Color(0.98f, 0.85f, 0.45f, 0.35f);
                    GUI.DrawTexture(new Rect(cx, cy + 19 * u, cw, 1 * u), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    cy += 24 * u;
                    foreach (var l in linhas)
                    {
                        float valor = l.ler();
                        // a linha de cima: o nome, do que cuida (cinza), a porcentagem e o ▶
                        float largNome = nome.CalcSize(new GUIContent(l.nome)).x + 8 * u;
                        float direita = l.amostra != null ? 30 * u : 0f;
                        GUI.Label(new Rect(cx, cy, largNome, 22 * u), l.nome, nome);
                        // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) do que ela cuida numa linha só: o que
                        // não cabe termina em reticências (com a quebra, numa tela mais estreita que 16:9, o "portas, móveis,
                        // armadilhas..." virava duas linhas na caixa de uma, e as duas saíam cortadas pela metade); refeito só
                        // quando a largura ou a letra mudam
                        float largSobre = cw - largNome - 52 * u - direita;
                        if (!Mathf.Approximately(l.larguraDoSobre, largSobre) || l.letraDoSobre != sobre.fontSize || l.sobreQueCabe == null)
                        {
                            l.larguraDoSobre = largSobre;
                            l.letraDoSobre = sobre.fontSize;
                            l.sobreQueCabe = Caber(l.sobre, sobre, largSobre) ?? "";
                        }
                        GUI.Label(new Rect(cx + largNome, cy + 1 * u, largSobre, 20 * u), l.sobreQueCabe, sobre);
                        GUI.Label(new Rect(cx + cw - 50 * u - direita, cy, 48 * u, 22 * u), $"{Mathf.RoundToInt(valor * 100f)}%", pct);
                        if (l.amostra != null && GUI.Button(new Rect(cx + cw - 26 * u, cy + 1 * u, 26 * u, 20 * u), "▶", tocar))
                            AmostraDoMixer.Tocar(l.amostra(), l.ler, l.vezes, l.intervalo, l.ate);
                        // o trilho: cinza, e amarelo até o cursor
                        var area = new Rect(cx, cy + 21 * u, cw, 16 * u);
                        float meio = area.y + area.height / 2f, ate = cursor.fixedWidth / 2f + (cw - cursor.fixedWidth) * valor;
                        GUI.color = new Color(1f, 1f, 1f, 0.16f);
                        GUI.DrawTexture(new Rect(cx, meio - 2 * u, cw, 4 * u), Texture2D.whiteTexture);
                        GUI.color = new Color(0.98f, 0.85f, 0.45f, 0.8f);
                        GUI.DrawTexture(new Rect(cx, meio - 2 * u, ate, 4 * u), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        float novo = GUI.HorizontalSlider(area, valor, 0f, 1f, trilho, cursor);
                        if (!Mathf.Approximately(novo, valor))
                        {
                            l.mudar(novo);
                            _gravarEm = Time.unscaledTime + 0.6f;
                        }
                        cy += 42 * u;   // (as 9 barras da coluna da direita cabem nas 720 unidades da tela)
                    }
                    cy += 4 * u;
                }
                fim = Mathf.Max(fim, cy);
            }
            y = fim + 6 * u;
        }

        static bool Voltar(float u, float x, float w, float y, GUIStyle botao)
        {
            bool voltar = GUI.Button(new Rect(x, y, w, 40 * u), "Voltar", botao);
            if (voltar && _gravarEm > 0f) Gravar();
            return voltar;
        }

        delegate bool BarraDoPainel(string nome, float valor, out float novo, float min = 0f, float max = 1f);

        // a aba Gráficos: a resolução, o modo da tela, a escala de renderização e a qualidade (UI/Graficos)
        // o nome à esquerda e as escolhas na mesma linha
        static int NaLinha(float u, float x, float w, ref float y, GUIStyle texto, GUIStyle opcao, string nome, float rotulo, int atual, string[] nomes)
        {
            GUI.Label(new Rect(x, y + 6 * u, rotulo, 26 * u), nome, texto);
            int novo = GUI.SelectionGrid(new Rect(x + rotulo, y, w - rotulo, 36 * u), atual, nomes, nomes.Length, QueCabe(opcao, nomes, w - rotulo));
            y += 42 * u;
            return novo;
        }

        // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a letra das escolhas de um SelectionGrid desce até o
        // nome mais comprido caber na célula: o padding e o vão dos botões não crescem com a tela, e a 1280 × 720 o "Média PS1"
        // passava da célula da pixelação (79 pixels em 75) e saía cortado dos dois lados; o "Sem limite" do FPS ficava a 3
        // pixels disso. Na tela em que tudo cabe, o estilo é o mesmo de antes
        static GUIStyle QueCabe(GUIStyle opcao, string[] nomes, float largura)
        {
            if (nomes == null || nomes.Length == 0) return opcao;
            float vao = Mathf.Max(opcao.margin.left, opcao.margin.right);
            float celula = (largura - vao * (nomes.Length - 1)) / nomes.Length - opcao.padding.horizontal;
            float maior = 0f;
            foreach (var n in nomes) maior = Mathf.Max(maior, opcao.CalcSize(new GUIContent(n)).x - opcao.padding.horizontal);
            if (maior <= celula || celula <= 0f) return opcao;
            return new GUIStyle(opcao) { fontSize = Mathf.Max(8, Mathf.FloorToInt(opcao.fontSize * celula / maior)) };
        }

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

            // as escolhas curtas na mesma linha do nome (com a pixelação, o painel passava da altura da tela)
            int qualidade = NaLinha(u, x, w, ref y, texto, opcao, "Qualidade gráfica", 170 * u, UI.Graficos.Qualidade, UI.Graficos.NomesDaQualidade);
            if (qualidade != UI.Graficos.Qualidade) UI.Graficos.MudarQualidade(qualidade);
            GUI.Label(new Rect(x, y, w, 44 * u), SobreAQualidade[UI.Graficos.Qualidade], detalhe);
            y += 50 * u;

            // a distância de visão dos zumbis (a qualidade traz a dela; dá para mudar à parte)
            int visao = UI.Graficos.Visao;
            int novaVisao = NaLinha(u, x, w, ref y, texto, opcao, "Visão (zumbis)", 170 * u, visao, UI.Graficos.NomesDaVisao);
            if (novaVisao != visao)
            {
                UI.Graficos.MudarVisao(novaVisao);
                _gravarEm = Time.unscaledTime + 0.6f;
            }
            GUI.Label(new Rect(x, y, w, 22 * u), $"Até {UI.Graficos.Metros(UI.Graficos.Visao):0} m. Mais longe: os zumbis aparecem antes e nascem mais longe.", detalhe);
            y += 30 * u;

            // a pixelação (para testar o visual; UI/Graficos)
            int pixelacao = NaLinha(u, x, w, ref y, texto, opcao, "Pixelação", 110 * u, UI.Graficos.Pixelacao, UI.Graficos.NomesDaPixelacao);
            if (pixelacao != UI.Graficos.Pixelacao) UI.Graficos.MudarPixelacao(pixelacao);
            GUI.Label(new Rect(x, y, w, 22 * u), UI.Graficos.Pixelacao == 0
                ? "O 3D em poucas linhas, ampliado sem suavizar; nas PS1, também as cores."
                : $"O 3D em {UI.Graficos.LinhasPixeladas} linhas{(UI.Graficos.CoresDoPs1 ? ", com as cores do PS1" : "")}. A escala de renderização não vale.", detalhe);
            y += 30 * u;
        }
    }
    /// <summary>
    /// A amostra do ▶ da aba Som (20/11/2026): toca o clipe (ou alguns, um depois do outro) numa fonte própria, sem posição,
    /// no volume do canal na hora de cada um; um laço para em poucos segundos, sumindo.
    /// </summary>
    public class AmostraDoMixer : MonoBehaviour
    {
        static AmostraDoMixer _instancia;
        AudioSource _fonte;
        AudioClip[] _clips;
        System.Func<float> _volume;
        int _faltam;
        float _intervalo, _proxima, _paraEm;

        public static void Tocar(AudioClip[] clips, System.Func<float> volume, int vezes, float intervalo, float ate)
        {
            if (clips == null || clips.Length == 0) return;
            if (_instancia == null)
            {
                var go = new GameObject("Amostra do mixer");
                DontDestroyOnLoad(go);
                _instancia = go.AddComponent<AmostraDoMixer>();
                _instancia._fonte = go.AddComponent<AudioSource>();
                _instancia._fonte.playOnAwake = false;
                _instancia._fonte.spatialBlend = 0f;
                _instancia._fonte.ignoreListenerPause = true;
            }
            var a = _instancia;
            a._fonte.Stop();
            a._clips = clips;
            a._volume = volume;
            a._faltam = Mathf.Max(1, vezes);
            a._intervalo = intervalo;
            a._proxima = Time.unscaledTime;
            a._paraEm = Time.unscaledTime + Mathf.Max(ate, intervalo * vezes + 0.5f);
        }

        void Update()
        {
            if (_clips == null) return;
            float agora = Time.unscaledTime;
            if (_faltam > 0 && agora >= _proxima)
            {
                _faltam--;
                _proxima = agora + _intervalo;
                var clip = _clips[Random.Range(0, _clips.Length)];
                if (clip != null)
                {
                    _fonte.clip = clip;
                    _fonte.volume = 0.8f * (_volume != null ? _volume() : 1f);
                    _fonte.Play();
                }
            }
            // o laço (a fogueira) e o que passar do tempo: some no último meio segundo
            float resta = _paraEm - agora;
            if (_fonte.isPlaying && resta < 0.5f) _fonte.volume = Mathf.Max(0f, 0.8f * (_volume != null ? _volume() : 1f) * resta / 0.5f);
            if (resta <= 0f)
            {
                _fonte.Stop();
                _clips = null;
            }
        }
    }
}
