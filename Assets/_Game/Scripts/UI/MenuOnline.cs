using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Menu simples (OnGUI): hospedar, entrar com código ou testar em modo local. Na partida, mostra o código no
    /// canto e ESC abre o menu (o mundo continua rodando, porque é online). Nos dois, as <see cref="Opcoes"/> (volume,
    /// limite de FPS e o contador de FPS, que este menu desenha em cima de tudo).
    /// Também é o dono do mouse: solto com o menu aberto, preso no jogo assim que a partida começa ou o menu fecha.
    /// Linha de comando para testes: -entrar CÓDIGO, -hospedar-local, -entrar-local [ip], -bot (o jogador anda e
    /// atira sozinho) e -personagem 1 a 4; na cena Mundo, também -semente N, -tamanho pequeno|medio|grande e -nome TEXTO
    /// (o mundo de quem hospeda). O personagem é criado ao entrar na partida (a tela de criar, a TelaDeCriarPersonagem do
    /// jogador; o Plano_Personagem.md da pasta de arte): a escolha dos retratos saiu do menu.
    /// Na cena Mundo (com o <see cref="MundoDaPartida"/>), o começo é o menu principal; o "Novo jogo" e o "Continuar" estão
    /// no MenuOnline.NovoJogo.cs (repaginados em 06/11/2026). Desde 12/11/2026 (a pedido: "o lakewood seja o mapa que
    /// aparece no jogo normal"), também na cena Lakewood (com o <see cref="LakewoodNaCena"/>), com o Novo jogo dela (o
    /// MenuOnline.Lakewood.cs: sem a tela das cartas dos moradores, sem o Continuar).
    /// </summary>
    public partial class MenuOnline : MonoBehaviour
    {
        /// <summary>O menu do jogo (o título, o Novo jogo, o fundo): na cena Mundo e, desde 12/11/2026, na cena Lakewood.</summary>
        static bool MenuDoJogo => MundoDaPartida.Instancia != null || LakewoodNaCena.Instancia != null;
        /// <summary>O jogo normal no Lakewood (12/11/2026): a cena dele, sem o mundo sorteado.</summary>
        static bool NoLakewood => MundoDaPartida.Instancia == null && LakewoodNaCena.Instancia != null;

        /// <summary>Menu aberto (ou uma tela obrigatória, a de criar o personagem): o jogador local não recebe comandos.</summary>
        public static bool Aberto { get; private set; } = true;
        /// <summary>O menu da partida (ESC) está aberto: as telas embaixo dele não respondem.</summary>
        public static bool Pausado { get; private set; }
        /// <summary>Teste automático: o jogador local anda em círculo, atira e liga a lanterna sozinho.</summary>
        public static bool Bot { get; private set; }

        /// <summary>
        /// O jogador local recebe comandos: na partida, com o menu e as telas do jogo fechados e o mouse preso no jogo. O
        /// mouse é preso sozinho quando a partida começa e quando o menu ou a tela fecha. Se ele se soltar (a janela perdeu
        /// o foco), um clique o prende de novo, e esse clique não vale como tiro: os comandos só voltam depois de soltar os
        /// botões do mouse. O estado do mouse é lido na hora, para não depender da ordem dos scripts no quadro do clique.
        /// </summary>
        public static bool Controla => !Aberto && _telas.Count == 0 && Cursor.lockState == CursorLockMode.Locked && !_esperarSoltar && !Cinema;

        /// <summary>
        /// Uma cena de cinema (a introdução e os 5 anos depois do Dia 0, 01/11/2026): o jogador local não recebe comandos,
        /// mas o mouse continua preso (sem telas).
        /// </summary>
        public static bool Cinema { get; set; }

        /// <summary>
        /// Uma tela do jogo aberta por cima da partida (o inventário, o menu do desenvolvedor): o mouse fica solto e o
        /// jogador não recebe comandos, mas o HUD continua. ESC fecha as telas (<see cref="FecharTelas"/>) em vez de abrir o menu.
        /// </summary>
        public static bool ComTela => _telas.Count > 0;
        public static event Action FecharTelas;

        /// <summary>
        /// Abre ou fecha uma tela do jogo. Uma obrigatória (a de criar o personagem) não fecha com ESC: ESC abre o menu
        /// por cima dela (de onde se sai da partida), e com ela aberta o menu conta como aberto (sem os atalhos do jogo).
        /// </summary>
        public static void Tela(object dona, bool aberta, bool obrigatoria = false)
        {
            if (aberta)
            {
                _telas.Add(dona);
                if (obrigatoria) _obrigatorias.Add(dona);
            }
            else
            {
                _telas.Remove(dona);
                _obrigatorias.Remove(dona);
            }
        }

        static readonly System.Collections.Generic.HashSet<object> _telas = new System.Collections.Generic.HashSet<object>();
        static readonly System.Collections.Generic.HashSet<object> _obrigatorias = new System.Collections.Generic.HashSet<object>();
        static bool _esperarSoltar;
        SessaoOnline _sessao;
        string _codigo = "", _ip = "127.0.0.1";
        bool _pausa, _opcoes, _estavaAberto = true;
        GUIStyle _titulo, _texto, _botao, _campo, _erro, _grande, _opcaoDoMenu, _detalhe;
        // o menu do jogo (na cena Mundo, a pedido em 29/10/2026): Iniciar novo jogo, Continuar, Configurações e Sair; o
        // "Novo mundo" (o mapa e o multiplayer) fica atrás do Iniciar novo jogo
        bool _principal = true, _estavaNaPartida, _ultimoGravado;
        float _alfa = 1f;   // o menu principal aparecendo (FundoDoMenu.MenuVisivel)
        // o "Novo mundo" da cena Mundo
        string _nomeDoMundo = "Mundo novo", _semente = "";
        TamanhoDoMundo _tamanho = TamanhoDoMundo.Grande;
        // o save (02/11/2026): o personagem escolhido aqui, o modo de salvar do mundo novo e a tela do Continuar (a lista
        // dos mundos salvos)
        bool _continuar;
        System.Collections.Generic.List<PersonagemSalvo> _personagens;
        System.Collections.Generic.List<MundoSalvo> _mundos;
        int _indiceDoPersonagem;
        string _apagarPersonagem, _apagarMundo;
        ModoDeSalvar _modoNovo = ModoDeSalvar.Automatico;
        // o fogo amigo do mundo novo (03/11/2026): desligado de começo
        bool _fogoAmigoNovo;
        Vector2 _rolagemDosMundos;
        // a tela dos moradores do bunker (04/11/2026, o Plano_NPCs_Bunker.md, a parte 3; em cartas desde o mesmo dia): entre
        // o "Novo mundo" e a partida, e no Continuar de um mundo salvo antes deles (o _semMoradores); o _criarLocal diz se o
        // mundo novo é local
        readonly TelaDosMoradores _telaDosMoradores = new TelaDosMoradores();
        bool _criarLocal;
        MundoSalvo _semMoradores;

        void OnEnable() => _esperarSoltar = true;

        void Start()
        {
            _sessao = SessaoOnline.Instancia;
            Opcoes.Carregar();   // antes da linha de comando: o robô de teste fica em 60 FPS
            // o fundo do menu (o vídeo da cidade e a trilha do menu) só nas cenas do jogo (a Mundo e, desde 12/11/2026, a
            // Lakewood)
            if (MenuDoJogo) FundoDoMenu.Criar();
            else _principal = false;
            var args = Environment.GetCommandLineArgs();
            // o mundo da cena Mundo antes de hospedar: -semente N, -tamanho pequeno|medio|grande e -nome TEXTO
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-semente" && int.TryParse(args[i + 1], out int semente)) MundoDaPartida.Pedido.semente = semente;
                else if (args[i] == "-tamanho" && Tamanhos.Ler(args[i + 1], out var tamanho)) MundoDaPartida.Pedido.tamanho = tamanho;
                else if (args[i] == "-nome") MundoDaPartida.Pedido.nome = args[i + 1];
                else if (args[i] == "-personagem" && int.TryParse(args[i + 1], out int personagem)) Player.Personagens.Escolhido = personagem - 1;
                // o robô de teste fotografa a tela dele a cada relatório (o que quem entra enxerga: os zumbis, a chuva)
                else if (args[i] == "-fotos") _pastaDasFotos = args[i + 1];
            }
            // (o pedido fica de um Play para o outro no Editor; sem o nome, o campo de texto quebrava o menu)
            _nomeDoMundo = string.IsNullOrEmpty(MundoDaPartida.Pedido.nome) ? PedidoDoMundo.Padrao.nome : MundoDaPartida.Pedido.nome;
            _tamanho = MundoDaPartida.Pedido.tamanho;
            _semente = MundoDaPartida.Pedido.semente > 0 ? MundoDaPartida.Pedido.semente.ToString() : "";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-bot") { Bot = true; Application.targetFrameRate = 60; }
                else if (args[i] == "-entrar" && i + 1 < args.Length) _sessao.Entrar(args[i + 1]);
                else if (args[i] == "-hospedar-local") _sessao.HospedarLocal();
                else if (args[i] == "-entrar-local") _sessao.EntrarLocal(i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "127.0.0.1");
            }
        }

        void Update()
        {
            Opcoes.Medir(Time.unscaledDeltaTime);
            Graficos.NoQuadro();   // o anti-serrilhado da qualidade escolhida na câmera do jogador
            bool naPartida = _sessao.Atual == SessaoOnline.Estado.Conectado;
            var teclado = Keyboard.current;
            bool esc = teclado != null && teclado.escapeKey.wasPressedThisFrame;
            // ESC nas opções volta para o menu; no Novo jogo e no Continuar, volta ao menu principal (a tela das cartas cuida
            // do ESC dela); com uma tela do jogo aberta, fecha a tela; na partida, abre e fecha o menu
            if (esc && _opcoes) _opcoes = false;
            else if (esc && !naPartida && !_principal && MenuDoJogo && !_telaDosMoradores.Aberta &&
                     _sessao.Atual == SessaoOnline.Estado.Desconectado) Voltar();
            else if (naPartida && esc && _telas.Count > _obrigatorias.Count && !_pausa) FecharTelas?.Invoke();
            else if (naPartida && esc) _pausa = !_pausa;
            if (naPartida && !_pausa) _opcoes = false;
            if (!naPartida) _pausa = false;
            Pausado = naPartida && _pausa;
            Aberto = !naPartida || _pausa || _obrigatorias.Count > 0;
            // fora da partida, o menu do começo (com o fundo, que fica também enquanto conecta); ao sair de uma, volta ao
            // menu principal
            if (_estavaNaPartida && !naPartida)
            {
                _principal = MenuDoJogo;
                _continuar = false;
                _personagens = null;   // a lista do save muda depois de uma partida: lê de novo
                _mundos = null;
            }
            _estavaNaPartida = naPartida;
            if (_telaDosMoradores.Aberta)
            {
                if (_sessao.Atual != SessaoOnline.Estado.Desconectado) _telaDosMoradores.Fechar();
                else _telaDosMoradores.Atualizar();
            }
            FundoDoMenu.Pedido = !naPartida && MenuDoJogo && !ModoVideo.Ligado;
            if (!naPartida) _ultimoGravado = false;
            else if (!_ultimoGravado) GravarOUltimoMundo();
            bool solto = Aberto || ComTela;
            if (solto && !Bot)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!solto && !Bot) PrenderOMouse();
            _estavaAberto = solto;
            if (Bot && naPartida && Time.time >= _proximoRelatorio) Relatorio();
        }

        // no jogo, o mouse fica preso: sozinho quando o menu acabou de fechar (ou a partida começou); com um clique, se
        // ele se soltou (a janela perdeu o foco). Enquanto ele está solto, e até os botões serem soltos, nada de comandos
        // (o clique que prende o mouse não atira nem mira)
        void PrenderOMouse()
        {
            var mouse = Mouse.current;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                _esperarSoltar = true;
                bool clicou = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
                if (Application.isFocused && (_estavaAberto || clicou))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else if (mouse == null || (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)) _esperarSoltar = false;
        }

        // No modo robô, registra no log o que esta cópia enxerga dos outros jogadores (para testes sem tela). Com -fotos
        // PASTA (e a build com janela), também fotografa a tela a cada relatório.
        float _proximoRelatorio;
        static string _pastaDasFotos;
        int _fotos;
        void Relatorio()
        {
            _proximoRelatorio = Time.time + 3f;
            // o clima que chega do anfitrião (World/CicloDoDia.Tempo.cs) e o que esta máquina mostra (World/ClimaNaTela)
            var clima = World.ClimaNaTela.Instancia;
            Debug.Log($"[robô] clima {World.Tempo.NomeDo(World.Tempo.Nivel)}: nuvens {World.Tempo.Nuvens:F2}, chuva {World.Tempo.Chuva:F2}, vento {World.Tempo.Vento:F2}, " +
                      $"poças {World.Tempo.Pocas:F2}, tornado {(World.Tempo.Tornado ? $"a {Vector3.Distance(World.Tempo.PosicaoDoTornado, Player.JogadorRede.Local != null ? Player.JogadorRede.Local.transform.position : Vector3.zero):F0} m, força {World.Tempo.ForcaDoTornado:F2}" : "não")}" +
                      $"{(clima != null ? " · " + clima.Resumo().Replace("\n", " · ") : "")}");
            if (!string.IsNullOrEmpty(_pastaDasFotos))
            {
                System.IO.Directory.CreateDirectory(_pastaDasFotos);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(_pastaDasFotos, $"robo_{++_fotos:000}.png"));
            }
            var zumbis = Zombies.ZombieManager.Instancia;
            int perto = 0, medio = 0;
            if (zumbis != null && zumbis.IsSpawned) zumbis.ContarNiveis(out perto, out medio, out _);
            int semCabeca = 0, mortos = 0, cortados = 0, mancando = 0, rastejando = 0;
            foreach (var z in FindObjectsByType<Zombies.Zumbi>())
            {
                if (!z.isActiveAndEnabled) continue;
                if (z.Morto) mortos++;
                if (z.SemCabeca) semCabeca++;
                if (z.Cortes != 0) cortados++;
                if (z.Pernas == 1 || z.Pernas == 2) mancando++;
                if (z.Pernas >= 3) rastejando++;
            }
            Debug.Log($"[robô] hora {World.CicloDoDia.Relogio()}, zumbis {(zumbis != null && zumbis.IsSpawned ? zumbis.Quantos : 0)} (perto {perto}, médio {medio}; caídos {mortos}, sem cabeça {semCabeca}; " +
                      $"sem membros {cortados}, mancando {mancando}, rastejando {rastejando}), vida {Player.JogadorRede.Local?.Vida?.Vida.Value ?? 0f:F0}");
            foreach (var j in Player.JogadorRede.Todos)
                Debug.Log($"[robô] jogador {j.OwnerClientId}{(j.IsOwner ? " (eu)" : "")}: posição {j.transform.position}, olhar {j.Pitch.Value:F0}°, tiros recebidos {j.TirosRecebidos}, " +
                          $"lanterna {j.GetComponent<Player.LanternaJogador>().Ligada.Value}, na mão {j.NaMao}{(j.Luneta.Value ? " (na luneta)" : "")}");
        }

        void Estilos()
        {
            float u = Screen.height / 720f;
            if (_titulo != null && Mathf.Approximately(_titulo.fontSize, Mathf.Round(38 * u))) return;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(38 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Opcoes.Cor(_titulo, new Color(0.85f, 0.82f, 0.72f));   // a mesma cor com o mouse em cima (Opcoes.Cor)
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Opcoes.Cor(_texto, new Color(0.8f, 0.8f, 0.78f));
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(18 * u) };
            // o escolhido de um grupo (o tamanho do mundo) fica apertado e amarelo
            _botao.onNormal.textColor = _botao.onHover.textColor = _botao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            _botao.onNormal.background = _botao.active.background;
            _campo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.MiddleCenter };
            _erro = new GUIStyle(_texto);
            Opcoes.Cor(_erro, new Color(1f, 0.45f, 0.4f));
            _linhaDaPartida = new GUIStyle(_texto) { alignment = TextAnchor.UpperLeft };
        }

        // a linha da partida (o código, os jogadores e o mundo), refeita só quando muda
        GUIStyle _linhaDaPartida;
        string _textoDaPartida, _codigoNoTexto;
        int _jogadoresNoTexto, _sementeNoTexto;

        void OnGUI()
        {
            if (ModoVideo.Ligado) return;   // gravando um vídeo: nem o menu, nem o código da partida, nem o FPS
            GUI.depth = -18;   // por cima das telas do jogo (a de criar o personagem, o F1, o mapa); embaixo da de carregamento
            Estilos();
            float u = Screen.height / 720f;
            Menu(u);
            // o contador de FPS por cima do menu (no canto de cima, à direita; o minimapa desce para baixo dele quando ele está
            // ligado: UI/MinimapaDoMundo, 08/11/2026)
            if (Event.current.type == EventType.Repaint && !GravacaoNoMundo.SemHud) Opcoes.Contador(u);
        }

        void Menu(float u)
        {
            var estado = _sessao.Atual;
            if (estado == SessaoOnline.Estado.Conectado)
            {
                // com a tela de criar o personagem aberta, só o menu (ESC)
                if (_obrigatorias.Count > 0 && !_pausa) return;
                // na cena Mundo, o nome, a semente e o tamanho do mundo
                var m = MundoDaPartida.Instancia;
                string codigoAgora = _sessao.Local ? null : _sessao.Codigo;
                int semente = m != null ? m.Semente : 0;
                if (_textoDaPartida == null || codigoAgora != _codigoNoTexto || _sessao.Jogadores != _jogadoresNoTexto || semente != _sementeNoTexto)
                {
                    _codigoNoTexto = codigoAgora;
                    _jogadoresNoTexto = _sessao.Jogadores;
                    _sementeNoTexto = semente;
                    string codigo = _sessao.Local ? "partida local" : $"código {_sessao.Codigo}";
                    string mundo = m != null && m.Semente != 0 ? $"  ·  {m.Nome}: semente {m.Semente}, {Tamanhos.Lado(m.Tamanho) / 1000} km" : "";
                    _textoDaPartida = $"{codigo}  ·  {_sessao.Jogadores}/{SessaoOnline.MaxJogadores} jogadores{mundo}  ·  ESC: menu";
                }
                if (!GravacaoNoMundo.SemHud) GUI.Label(new Rect(14 * u, 10 * u, 900 * u, 24 * u), _textoDaPartida, _linhaDaPartida);
                if (_pausa) Pausa(u);
                return;
            }

            bool doJogo = MenuDoJogo;
            if (doJogo && _principal && !_opcoes && estado == SessaoOnline.Estado.Desconectado)
            {
                MenuPrincipal(u);
                return;
            }
            // (no Lakewood, as cartas dos moradores ficam desligadas: 12/11/2026)
            if (doJogo && !NoLakewood && _telaDosMoradores.Aberta && !_opcoes && estado == SessaoOnline.Estado.Desconectado)
            {
                Moradores();
                return;
            }
            // o Novo jogo e o Continuar (repaginados em 06/11/2026: MenuOnline.NovoJogo.cs); também enquanto conecta
            if (doJogo && !_opcoes)
            {
                if (NoLakewood) TelaDoNovoJogoNoLakewood();
                else if (_continuar) TelaDoContinuar();
                else TelaDoNovoJogo();
                return;
            }
            GUI.color = new Color(0, 0, 0, doJogo ? 0.55f : 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_opcoes)
            {
                if (Opcoes.Painel(u, Screen.height * 0.16f, _titulo, _botao)) _opcoes = false;
                return;
            }
            // fora da cena Mundo (a Demo_Cidade): hospedar, entrar com o código e o modo local
            float w = 420 * u, x = (Screen.width - w) / 2f, y = Screen.height * 0.18f;
            GUI.Label(new Rect(0, y, Screen.width, 50 * u), "BUNKER: Outbreak", _titulo);
            GUI.Label(new Rect(0, y + 48 * u, Screen.width, 24 * u), "Demo · até 4 jogadores", _texto);
            y += 110 * u;
            if (estado == SessaoOnline.Estado.Conectando)
            {
                GUI.Label(new Rect(0, y, Screen.width, 30 * u), "Conectando...", _texto);
                return;
            }
            if (GUI.Button(new Rect(x, y, w, 46 * u), "Hospedar partida", _botao)) _sessao.Hospedar();
            y += 70 * u;
            GUI.Label(new Rect(x, y, w, 24 * u), "ou entre com o código de um amigo:", _texto);
            y += 28 * u;
            _codigo = GUI.TextField(new Rect(x, y, w * 0.62f, 40 * u), _codigo, 12, _campo);
            if (GUI.Button(new Rect(x + w * 0.65f, y, w * 0.35f, 40 * u), "Entrar", _botao))
            {
                ParaEntrar(false);
                _sessao.Entrar(_codigo);
            }
            y += 64 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Teste sem internet (mesma rede):", _texto);
            y += 26 * u;
            if (GUI.Button(new Rect(x, y, w * 0.48f, 32 * u), "Hospedar local", _botao)) _sessao.HospedarLocal();
            _ip = GUI.TextField(new Rect(x + w * 0.52f, y, w * 0.26f, 32 * u), _ip, 40, _campo);
            if (GUI.Button(new Rect(x + w * 0.8f, y, w * 0.2f, 32 * u), "Entrar", _botao))
            {
                ParaEntrar(false);
                _sessao.EntrarLocal(_ip);
            }
            y += 50 * u;
            if (!string.IsNullOrEmpty(_sessao.Erro)) GUI.Label(new Rect(x - 40 * u, y, w + 80 * u, 50 * u), _sessao.Erro, _erro);
            if (GUI.Button(new Rect(x + w * 0.3f, Screen.height - 70 * u, w * 0.4f, 34 * u), "Configurações", _botao)) _opcoes = true;
        }

        // ------------------------------------------------------------------ o menu principal (a cena Mundo)

        void EstilosDoPrincipal(float u)
        {
            if (_grande != null && Mathf.Approximately(_grande.fontSize, Mathf.Round(54 * u))) return;
            _grande = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(54 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            Opcoes.Cor(_grande, new Color(0.86f, 0.82f, 0.72f));
            _opcaoDoMenu = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(27 * u), alignment = TextAnchor.MiddleLeft };
            Opcoes.Cor(_opcaoDoMenu, new Color(0.8f, 0.78f, 0.72f));
            _detalhe = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * u), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            Opcoes.Cor(_detalhe, new Color(0.62f, 0.6f, 0.56f));
        }

        // o título e as quatro opções, à esquerda, sobre o vídeo da cidade (UI/FundoDoMenu). Ao abrir o jogo (e ao voltar de
        // uma partida), tudo aparece devagar depois da "TV ligando" (FundoDoMenu.MenuVisivel); antes disso, nada responde
        void MenuPrincipal(float u)
        {
            _alfa = FundoDoMenu.MenuVisivel;
            if (_alfa <= 0.001f) return;
            EstilosDoPrincipal(u);
            GUI.color = new Color(1f, 1f, 1f, _alfa);
            float x = 84 * u, y = Screen.height * 0.17f, w = 520 * u;
            y = TituloDoJogo(x, y, u);
            GUI.color = new Color(0.77f, 0.14f, 0.14f, 0.9f * _alfa);
            GUI.DrawTexture(new Rect(x + 2 * u, y + 6 * u, 300 * u, 2 * u), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, _alfa);
            y += 64 * u;
            if (Opcao(new Rect(x, y, w, 46 * u), "Iniciar novo jogo", true, u))
            {
                _principal = false;
                _personagens = null;
            }
            y += 56 * u;
            // o Continuar abre a lista dos mundos salvos (02/11/2026, o save); no Lakewood, os jogos salvos dele (12/11/2026,
            // a parte 8 do "O jogo do mundo sorteado no Lakewood"; a lista de cada cena só tem os dela)
            bool lakewood = NoLakewood;
            var mundos = Mundos();
            bool podeContinuar = mundos.Count > 0;
            if (Opcao(new Rect(x, y, w, 46 * u), "Continuar", podeContinuar, u) && podeContinuar)
            {
                _principal = false;
                _continuar = true;
                _personagens = null;
            }
            // (12/11/2026: sem mundo, o porquê: o Dia 0 não salva, o mundo entra aqui quando o abrigo abre)
            GUI.Label(new Rect(x + 2 * u, y + 40 * u, w, 20 * u), mundos.Count > 0 ? $"{mundos[0].nome} · {mundos[0].Resumo()}"
                : lakewood ? "nenhum jogo salvo ainda" : "nenhum mundo salvo ainda (o Dia 0 não salva)", _detalhe);
            y += 76 * u;
            if (Opcao(new Rect(x, y, w, 46 * u), "Configurações", true, u)) _opcoes = true;
            y += 56 * u;
            if (Opcao(new Rect(x, y, w, 46 * u), "Sair", true, u)) SairDoJogo();
            if (!string.IsNullOrEmpty(_sessao.Erro)) GUI.Label(new Rect(x, y + 70 * u, w, 50 * u), _sessao.Erro, _erro);
            GUI.color = Color.white;
        }

        // o título do jogo (06/11/2026, a pedido: "BUNKER: Outbreak", com as maiúsculas e as minúsculas do logo do Pedro; o K
        // e o "Out" em vermelho, como no logo, e o "Outbreak" um pouco para a direita). Com a imagem do logo em
        // Resources/Logo_BunkerOutbreak (o fundo transparente), ela fica no lugar do texto. Devolve onde o título acaba
        [System.NonSerialized] Texture2D _logo;
        [System.NonSerialized] bool _logoProcurado;
        GUIStyle _tituloDoJogo;

        float TituloDoJogo(float x, float y, float u)
        {
            if (!_logoProcurado)
            {
                _logoProcurado = true;
                _logo = Resources.Load<Texture2D>("Logo_BunkerOutbreak");
            }
            if (_logo != null)
            {
                float largura = 540 * u, altura = largura * _logo.height / _logo.width;
                GUI.DrawTexture(new Rect(x - 6 * u, y, largura, altura), _logo, ScaleMode.ScaleToFit, true);
                return y + altura;
            }
            int tamanho = Mathf.RoundToInt(72 * u);
            if (_tituloDoJogo == null || _tituloDoJogo.fontSize != tamanho)
            {
                _tituloDoJogo = new GUIStyle(GUI.skin.label) { font = Fonte(), fontSize = tamanho, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };
                _tituloDoJogo.padding = new RectOffset(0, 0, 0, 0);
            }
            Opcoes.Cor(_tituloDoJogo, new Color(0.88f, 0.85f, 0.78f));
            SombreadoRico(new Rect(x, y, Screen.width, 72 * u), "BUNKER:", "BUN<color=#C42424>K</color>ER:", _tituloDoJogo, u);
            SombreadoRico(new Rect(x + 26 * u, y + 64 * u, Screen.width, 72 * u), "Outbreak", "<color=#C42424>Out</color>break", _tituloDoJogo, u);
            return y + 136 * u;
        }

        // o texto com a sombra, com as cores no texto (a sombra sai do texto sem as cores, senão as letras coloridas ficariam
        // sem a sombra escura)
        static void SombreadoRico(Rect r, string semCor, string comCor, GUIStyle estilo, float u)
        {
            var cor = estilo.normal.textColor;
            Opcoes.Cor(estilo, new Color(0f, 0f, 0f, 0.8f));
            GUI.Label(new Rect(r.x + 3 * u, r.y + 3 * u, r.width, r.height), semCor, estilo);
            Opcoes.Cor(estilo, cor);
            GUI.Label(r, comCor, estilo);
        }

        // uma opção do menu principal: o texto que acende sob o mouse, com o tracinho na frente (só responde com o menu já
        // quase todo à vista)
        bool Opcao(Rect r, string texto, bool ativa, float u)
        {
            bool pronta = ativa && _alfa > 0.6f;
            bool sobre = pronta && r.Contains(Event.current.mousePosition);
            var estilo = new GUIStyle(_opcaoDoMenu);
            Opcoes.Cor(estilo, !ativa ? new Color(0.45f, 0.44f, 0.42f) : sobre ? new Color(0.98f, 0.85f, 0.45f) : _opcaoDoMenu.normal.textColor);
            if (sobre)
            {
                GUI.color = new Color(0.98f, 0.85f, 0.45f, 0.9f * _alfa);
                GUI.DrawTexture(new Rect(r.x - 22 * u, r.y + r.height / 2 - 1 * u, 14 * u, 2 * u), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, _alfa);
            }
            Sombreado(new Rect(r.x + (sobre ? 6 * u : 0), r.y, r.width, r.height), texto, estilo, u);
            return pronta && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // o texto com a sombra (uma cópia escura, 2 px abaixo e à direita); a cor vale em todos os estados (Opcoes.Cor)
        static void Sombreado(Rect r, string texto, GUIStyle estilo, float u)
        {
            var cor = estilo.normal.textColor;
            Opcoes.Cor(estilo, new Color(0f, 0f, 0f, 0.8f));
            GUI.Label(new Rect(r.x + 2 * u, r.y + 2 * u, r.width, r.height), texto, estilo);
            Opcoes.Cor(estilo, cor);
            GUI.Label(r, texto, estilo);
        }

        static void SairDoJogo()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // o último mundo criado aqui: o nome, o tamanho e a semente, gravados no computador quando a partida começa como
        // anfitrião (o Continuar agora é o dos mundos salvos: Salvar/MundoSalvo; isto ficou para os testes e o -semente)
        void GravarOUltimoMundo()
        {
            var m = MundoDaPartida.Instancia;
            var rede = Unity.Netcode.NetworkManager.Singleton;
            if (m == null || m.Semente == 0 || rede == null) return;
            _ultimoGravado = true;
            if (!rede.IsServer) return;   // quem entra no mundo de um amigo não grava (o Continuar é de quem hospeda)
            PlayerPrefs.SetInt("mundo.ultimo.semente", m.Semente);
            PlayerPrefs.SetInt("mundo.ultimo.tamanho", (int)m.Tamanho);
            PlayerPrefs.SetString("mundo.ultimo.nome", string.IsNullOrEmpty(m.Nome) ? PedidoDoMundo.Padrao.nome : m.Nome);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ os moradores do bunker (04/11/2026)

        /// <summary>
        /// Abre a tela dos moradores: para o mundo novo (local ou não) ou para um mundo salvo antes deles (<paramref name="semMoradores"/>,
        /// do Continuar). O grupo da última vez fica, até criar um mundo.
        /// </summary>
        void AbrirOsMoradores(bool local, MundoSalvo semMoradores)
        {
            _criarLocal = local;
            _semMoradores = semMoradores;
            _telaDosMoradores.TextoDoBotao = semMoradores != null ? "JOGAR" : local ? "CRIAR LOCAL" : "CRIAR O MUNDO";
            _telaDosMoradores.Abrir();
        }

        void Moradores()
        {
            var saida = _telaDosMoradores.Desenhar();
            if (saida == TelaDosMoradores.Saida.Voltar)
            {
                _telaDosMoradores.Fechar();
                _semMoradores = null;
            }
            else if (saida == TelaDosMoradores.Saida.Criar) Criar();
        }

        // o "Criar o mundo" da tela das cartas: o mundo novo com os 52 (os 5 escolhidos no bunker e os outros no mapa), ou o
        // de antes deles, que ganha os NPCs agora
        void Criar()
        {
            var npcs = _telaDosMoradores.Npcs;
            var (bunker, mapa) = _telaDosMoradores.Separar();
            _telaDosMoradores.Fechar();
            _telaDosMoradores.Esquecer();
            Debug.Log($"[Moradores] {bunker.Count} no bunker e {mapa.Count} no mapa: " +
                      string.Join(", ", bunker.ConvertAll(f => $"{f.NomeCompleto} ({f.idade})")));
            if (_semMoradores != null)
            {
                // o mundo de antes dos moradores ganha os NPCs agora (e grava, para a tela não voltar)
                var m = _semMoradores;
                _semMoradores = null;
                PorOsNpcs(m, npcs, bunker, mapa);
                m.Gravar();
                JogarNoMundo(m);
                return;
            }
            PedirOMundo();
            PorOsNpcs(Salvamento.Atual, npcs, bunker, mapa);
            if (_criarLocal) _sessao.HospedarLocal();
            else _sessao.Hospedar();
        }

        static void PorOsNpcs(MundoSalvo m, NpcsDoMundo npcs, List<FichaDoMorador> bunker, List<FichaDoMorador> mapa)
        {
            m.moradores = bunker;
            m.npcsDoMapa = mapa;
            m.npcsDescobertos = bunker.ConvertAll(f => f.id);
            m.proximoMorador = npcs.proximoId;
        }

        // um mundo da tela de antes das cartas (com os moradores e sem os do mapa) ganha os outros NPCs (e grava)
        static void CompletarOsNpcs(MundoSalvo m)
        {
            var npcs = new NpcsDoMundo { semente = m.semente ^ 0x5A17, proximoId = m.proximoMorador, fichas = new List<FichaDoMorador>(m.moradores) };
            SorteioDosMoradores.CompletarMundo(npcs, npcs.semente);
            var mapa = npcs.fichas.FindAll(f => !m.moradores.Contains(f));
            PorOsNpcs(m, npcs, m.moradores, mapa);
            m.Gravar();
            Debug.Log($"[Moradores] O mundo {m.nome}, de antes das cartas, ganhou {mapa.Count} NPCs no mapa.");
        }

        /// <summary>Para os testes (o MCP no Play): a tela dos moradores.</summary>
        public TelaDosMoradores TelaDeMoradores => _telaDosMoradores;

        /// <summary>Para os testes: o "Criar o mundo" da tela dos moradores aberta (local ou não).</summary>
        public void CriarComOsMoradoresParaTeste(bool local)
        {
            if (!_telaDosMoradores.Aberta) return;
            _criarLocal = local;
            Criar();
        }

        /// <summary>
        /// Para os testes (o MCP no Play): abre a tela dos moradores como o "Escolher os moradores" do Novo mundo, ou, com
        /// <paramref name="fechar"/>, fecha e volta ao menu principal.
        /// </summary>
        public void AbrirOsMoradoresParaTeste(bool fechar = false)
        {
            if (fechar)
            {
                _telaDosMoradores.Fechar();
                _principal = true;
                return;
            }
            _principal = false;
            _continuar = false;
            _opcoes = false;
            AbrirOsMoradores(false, null);
        }

        // o pedido do menu para o MundoDaPartida. Uma palavra vira um número (o mesmo para todos que digitarem ela)
        void PedirOMundo()
        {
            string texto = _semente.Trim();
            int semente = 0;
            if (texto.Length > 0 && (!int.TryParse(texto, out semente) || semente <= 0))
                semente = (int)(Sorteio.Fnv(texto.ToLowerInvariant()) % 999999UL) + 1;
            // o jogo novo começa pelo Dia 0 (01/11/2026), menos para um personagem que já existe (02/11/2026, a pedido: ele
            // pula o Dia 0); o Continuar vai direto para o mundo
            AplicarAEscolha();
            // (o nome vazio vira o padrão: o campo mostra o "Mundo novo" apagado quando vazio)
            string nome = string.IsNullOrWhiteSpace(_nomeDoMundo) ? PedidoDoMundo.Padrao.nome : _nomeDoMundo.Trim();
            MundoDaPartida.Pedido = new PedidoDoMundo { tamanho = _tamanho, semente = semente, nome = nome, diaZero = EscolhaDoPersonagem.Novo, fogoAmigo = _fogoAmigoNovo };
            Salvamento.Atual = MundoSalvo.Novo(MundoDaPartida.Pedido, _modoNovo);
        }

        // ------------------------------------------------------------------ o save (02/11/2026)

        System.Collections.Generic.List<PersonagemSalvo> Personagens() => _personagens ??= PersonagemSalvo.Todos();
        // (12/11/2026: os do Lakewood só na cena dele, e os do mundo sorteado só na dele)
        System.Collections.Generic.List<MundoSalvo> Mundos() => _mundos ??= MundoSalvo.Todos().FindAll(m => m.lakewood == NoLakewood);

        // o personagem escolhido no painel vira o da partida (um novo, se for o "Novo personagem")
        void AplicarAEscolha()
        {
            var lista = Personagens();
            if (_indiceDoPersonagem >= 0 && _indiceDoPersonagem < lista.Count) EscolhaDoPersonagem.Escolher(lista[_indiceDoPersonagem]);
            else EscolhaDoPersonagem.NovoPersonagem();
        }

        // entrando no mundo de um amigo: o personagem escolhido, e nada de mundo salvo nesta máquina (o mundo é dele)
        void ParaEntrar(bool doMundo)
        {
            if (doMundo) AplicarAEscolha();
            Salvamento.Atual = null;
        }

        // abre o mundo salvo: o pedido vem do arquivo (a semente, o tamanho e o nome; sem o Dia 0) e o Salvamento põe o resto
        void Jogar(string id)
        {
            var m = MundoSalvo.Ler(id);
            if (m == null)
            {
                _mundos = null;
                return;
            }
            // um mundo salvo antes dos moradores do bunker abre a tela das cartas na primeira vez; um da tela de antes das
            // cartas (com os moradores e sem os NPCs do mapa) ganha os do mapa
            // (o Lakewood não tem o bunker nem os moradores por enquanto)
            if (!m.lakewood && (m.moradores == null || m.moradores.Count == 0))
            {
                AbrirOsMoradores(_redeLocal, m);
                return;
            }
            if (!m.lakewood && (m.npcsDoMapa == null || m.npcsDoMapa.Count == 0)) CompletarOsNpcs(m);
            JogarNoMundo(m);
        }

        void JogarNoMundo(MundoSalvo m)
        {
            AplicarAEscolha();
            Salvamento.Atual = m;
            if (!m.lakewood)
            {
                MundoDaPartida.Pedido = m.Pedido;
                _nomeDoMundo = m.nome;
                _tamanho = (TamanhoDoMundo)Mathf.Clamp(m.tamanho, 0, 2);
                _semente = m.semente.ToString();
            }
            if (_redeLocal) _sessao.HospedarLocal();
            else _sessao.Hospedar();
        }

        void Pausa(float u)
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_opcoes)
            {
                if (Opcoes.Painel(u, Screen.height * 0.16f, _titulo, _botao)) _opcoes = false;
                return;
            }
            float w = 320 * u, x = (Screen.width - w) / 2f, y = Screen.height * 0.35f;
            GUI.Label(new Rect(0, y - 60 * u, Screen.width, 40 * u), "MENU", _titulo);
            if (!_sessao.Local)
            {
                GUI.Label(new Rect(0, y - 20 * u, Screen.width, 24 * u), $"Código para os amigos: {_sessao.Codigo}", _texto);
                if (GUI.Button(new Rect(x, y + 12 * u, w, 36 * u), "Copiar código", _botao)) GUIUtility.systemCopyBuffer = _sessao.Codigo;
            }
            if (GUI.Button(new Rect(x, y + 56 * u, w, 36 * u), "Continuar", _botao)) _pausa = false;
            if (GUI.Button(new Rect(x, y + 100 * u, w, 36 * u), "Configurações", _botao)) _opcoes = true;
            // (12/11/2026: no Dia 0 nada é salvo, e o botão avisa)
            bool noDiaZero = MundoDaPartida.Instancia != null && MundoDaPartida.Instancia.NoDiaZero;
            if (GUI.Button(new Rect(x, y + 144 * u, w, 36 * u), noDiaZero ? "Sair (o Dia 0 não é salvo)" : "Sair da partida", _botao))
            {
                _pausa = false;
                // nos modos automático e ao sair, salva antes (o anfitrião sai um instante depois, para todos gravarem)
                if (!Salvamento.AoSairDaPartida(() => _sessao.Sair())) _sessao.Sair();
            }
            // como este mundo salva, e quando salvou
            var s = Salvamento.Instancia;
            string texto = s == null || !s.Salva ? "Esta partida não salva."
                : s.Modo == ModoDeSalvar.Dormindo ? "Este mundo salva só dormindo (E na sua cama, ou numa cama da casa da família)."
                : s.Modo == ModoDeSalvar.AoSair ? "Este mundo salva ao sair da partida (e dormindo)."
                : "Este mundo salva sozinho a cada 5 minutos e ao sair.";
            if (s != null && s.Salva && s.SalvouEm >= 0f) texto += $"\nSalvo há {Mathf.FloorToInt((Time.unscaledTime - s.SalvouEm) / 60f)} min.";
            // o fogo amigo do mundo (03/11/2026)
            var mundo = MundoDaPartida.Instancia;
            if (mundo != null && mundo.IsSpawned) texto += mundo.FogoAmigo ? "\nFogo amigo: ligado." : "\nFogo amigo: desligado.";
            GUI.Label(new Rect(0, y + 196 * u, Screen.width, 66 * u), texto, _texto);
        }
    }
}
