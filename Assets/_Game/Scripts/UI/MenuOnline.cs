using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Menu simples (OnGUI): hospedar, entrar com código ou testar em modo local. Na partida, mostra o código no
    /// canto e ESC abre o menu (o mundo continua rodando, porque é online). Nos dois, as <see cref="Opcoes"/> (volume,
    /// limite de FPS e o contador de FPS, que este menu desenha em cima de tudo).
    /// Também é o dono do mouse: solto com o menu aberto, preso no jogo assim que a partida começa ou o menu fecha.
    /// Linha de comando para testes: -entrar CÓDIGO, -hospedar-local, -entrar-local [ip] e -bot (o jogador anda e
    /// atira sozinho); na cena Mundo, também -semente N, -tamanho pequeno|medio|grande e -nome TEXTO (o mundo de quem hospeda).
    /// Na cena Mundo (com o <see cref="MundoDaPartida"/>), o começo é o "Novo mundo": o nome, o tamanho e a semente.
    /// </summary>
    public class MenuOnline : MonoBehaviour
    {
        /// <summary>Menu aberto: o jogador local não recebe comandos.</summary>
        public static bool Aberto { get; private set; } = true;
        /// <summary>Teste automático: o jogador local anda em círculo, atira e liga a lanterna sozinho.</summary>
        public static bool Bot { get; private set; }

        /// <summary>
        /// O jogador local recebe comandos: na partida, com o menu e as telas do jogo fechados e o mouse preso no jogo. O
        /// mouse é preso sozinho quando a partida começa e quando o menu ou a tela fecha. Se ele se soltar (a janela perdeu
        /// o foco), um clique o prende de novo, e esse clique não vale como tiro: os comandos só voltam depois de soltar os
        /// botões do mouse. O estado do mouse é lido na hora, para não depender da ordem dos scripts no quadro do clique.
        /// </summary>
        public static bool Controla => !Aberto && _telas.Count == 0 && Cursor.lockState == CursorLockMode.Locked && !_esperarSoltar;

        /// <summary>
        /// Uma tela do jogo aberta por cima da partida (o inventário, o menu do desenvolvedor): o mouse fica solto e o
        /// jogador não recebe comandos, mas o HUD continua. ESC fecha as telas (<see cref="FecharTelas"/>) em vez de abrir o menu.
        /// </summary>
        public static bool ComTela => _telas.Count > 0;
        public static event Action FecharTelas;

        public static void Tela(object dona, bool aberta)
        {
            if (aberta) _telas.Add(dona);
            else _telas.Remove(dona);
        }

        static readonly System.Collections.Generic.HashSet<object> _telas = new System.Collections.Generic.HashSet<object>();
        static bool _esperarSoltar;
        SessaoOnline _sessao;
        string _codigo = "", _ip = "127.0.0.1";
        bool _pausa, _opcoes, _estavaAberto = true;
        GUIStyle _titulo, _texto, _botao, _campo, _erro;
        // o "Novo mundo" da cena Mundo
        string _nomeDoMundo = "Mundo novo", _semente = "";
        TamanhoDoMundo _tamanho = TamanhoDoMundo.Grande;
        static readonly string[] NomesDosTamanhos = { "Pequeno (3 km)", "Médio (5 km)", "Grande (8 km)" };

        void OnEnable() => _esperarSoltar = true;

        void Start()
        {
            _sessao = SessaoOnline.Instancia;
            Opcoes.Carregar();   // antes da linha de comando: o robô de teste fica em 60 FPS
            var args = Environment.GetCommandLineArgs();
            // o mundo da cena Mundo antes de hospedar: -semente N, -tamanho pequeno|medio|grande e -nome TEXTO
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-semente" && int.TryParse(args[i + 1], out int semente)) MundoDaPartida.Pedido.semente = semente;
                else if (args[i] == "-tamanho" && Tamanhos.Ler(args[i + 1], out var tamanho)) MundoDaPartida.Pedido.tamanho = tamanho;
                else if (args[i] == "-nome") MundoDaPartida.Pedido.nome = args[i + 1];
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
            bool naPartida = _sessao.Atual == SessaoOnline.Estado.Conectado;
            var teclado = Keyboard.current;
            bool esc = teclado != null && teclado.escapeKey.wasPressedThisFrame;
            // ESC nas opções volta para o menu; com uma tela do jogo aberta, fecha a tela; na partida, abre e fecha o menu
            if (esc && _opcoes) _opcoes = false;
            else if (naPartida && esc && ComTela) FecharTelas?.Invoke();
            else if (naPartida && esc) _pausa = !_pausa;
            if (naPartida && !_pausa) _opcoes = false;
            if (!naPartida) _pausa = false;
            Aberto = !naPartida || _pausa;
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

        // No modo robô, registra no log o que esta cópia enxerga dos outros jogadores (para testes sem tela).
        float _proximoRelatorio;
        void Relatorio()
        {
            _proximoRelatorio = Time.time + 3f;
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
            _titulo.normal.textColor = new Color(0.85f, 0.82f, 0.72f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _texto.normal.textColor = new Color(0.8f, 0.8f, 0.78f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(18 * u) };
            // o escolhido de um grupo (o tamanho do mundo) fica apertado e amarelo
            _botao.onNormal.textColor = _botao.onHover.textColor = _botao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            _botao.onNormal.background = _botao.active.background;
            _campo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.MiddleCenter };
            _erro = new GUIStyle(_texto);
            _erro.normal.textColor = new Color(1f, 0.45f, 0.4f);
        }

        void OnGUI()
        {
            if (ModoVideo.Ligado) return;   // gravando um vídeo: nem o menu, nem o código da partida, nem o FPS
            Estilos();
            float u = Screen.height / 720f;
            Menu(u);
            // o contador de FPS por cima do menu (o OnGUI do jogador não usa o canto de cima, à direita)
            if (Event.current.type == EventType.Repaint) Opcoes.Contador(u);
        }

        void Menu(float u)
        {
            var estado = _sessao.Atual;
            if (estado == SessaoOnline.Estado.Conectado)
            {
                string codigo = _sessao.Local ? "partida local" : $"código {_sessao.Codigo}";
                // na cena Mundo, o nome, a semente e o tamanho do mundo
                var m = MundoDaPartida.Instancia;
                string mundo = m != null && m.Semente != 0 ? $"  ·  {m.Nome}: semente {m.Semente}, {Tamanhos.Lado(m.Tamanho) / 1000} km" : "";
                GUI.Label(new Rect(14 * u, 10 * u, 900 * u, 24 * u), $"{codigo}  ·  {_sessao.Jogadores}/{SessaoOnline.MaxJogadores} jogadores{mundo}  ·  ESC: menu", new GUIStyle(_texto) { alignment = TextAnchor.UpperLeft });
                if (_pausa) Pausa(u);
                return;
            }

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_opcoes)
            {
                if (Opcoes.Painel(u, Screen.height * 0.3f, _titulo, _botao)) _opcoes = false;
                return;
            }
            // na cena Mundo, o "Novo mundo" (o nome, o tamanho e a semente) no lugar do "Hospedar partida"
            bool doMundo = MundoDaPartida.Instancia != null;
            float w = 420 * u, x = (Screen.width - w) / 2f, y = Screen.height * (doMundo ? 0.07f : 0.18f);
            GUI.Label(new Rect(0, y, Screen.width, 50 * u), "ZOMBIE CITY CLEANER", _titulo);
            GUI.Label(new Rect(0, y + 48 * u, Screen.width, 24 * u), doMundo ? "Novo mundo · até 4 jogadores" : "Demo · até 4 jogadores", _texto);
            y += (doMundo ? 84 : 110) * u;
            if (estado == SessaoOnline.Estado.Conectando)
            {
                GUI.Label(new Rect(0, y, Screen.width, 30 * u), "Conectando...", _texto);
                return;
            }
            if (doMundo) y = NovoMundo(u, x, y, w);
            else
            {
                if (GUI.Button(new Rect(x, y, w, 46 * u), "Hospedar partida", _botao)) _sessao.Hospedar();
                y += 70 * u;
            }
            GUI.Label(new Rect(x, y, w, 24 * u), "ou entre com o código de um amigo:", _texto);
            y += 28 * u;
            _codigo = GUI.TextField(new Rect(x, y, w * 0.62f, 40 * u), _codigo, 12, _campo);
            if (GUI.Button(new Rect(x + w * 0.65f, y, w * 0.35f, 40 * u), "Entrar", _botao)) _sessao.Entrar(_codigo);
            y += 64 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Teste sem internet (mesma rede):", _texto);
            y += 26 * u;
            if (GUI.Button(new Rect(x, y, w * 0.48f, 32 * u), doMundo ? "Criar local" : "Hospedar local", _botao))
            {
                if (doMundo) PedirOMundo();
                _sessao.HospedarLocal();
            }
            _ip = GUI.TextField(new Rect(x + w * 0.52f, y, w * 0.26f, 32 * u), _ip, 40, _campo);
            if (GUI.Button(new Rect(x + w * 0.8f, y, w * 0.2f, 32 * u), "Entrar", _botao)) _sessao.EntrarLocal(_ip);
            y += 50 * u;
            if (!string.IsNullOrEmpty(_sessao.Erro)) GUI.Label(new Rect(x - 40 * u, y, w + 80 * u, 50 * u), _sessao.Erro, _erro);
            if (GUI.Button(new Rect(x + w * 0.3f, Screen.height - 70 * u, w * 0.4f, 34 * u), "Opções", _botao)) _opcoes = true;
        }

        // o "Novo mundo": o nome, o tamanho e a semente, e o botão de criar (hospedar); devolve onde o menu continua
        float NovoMundo(float u, float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22 * u), "Nome do mundo:", _texto);
            y += 24 * u;
            _nomeDoMundo = GUI.TextField(new Rect(x, y, w, 36 * u), _nomeDoMundo, 30, _campo);
            y += 46 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Tamanho do mapa:", _texto);
            y += 24 * u;
            _tamanho = (TamanhoDoMundo)GUI.SelectionGrid(new Rect(x, y, w, 34 * u), (int)_tamanho, NomesDosTamanhos, 3, _botao);
            y += 44 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Semente (um número ou uma palavra; vazia, sorteada):", _texto);
            y += 24 * u;
            _semente = GUI.TextField(new Rect(x, y, w * 0.62f, 34 * u), _semente, 20, _campo);
            if (GUI.Button(new Rect(x + w * 0.65f, y, w * 0.35f, 34 * u), "Sortear", _botao)) _semente = GeradorDoMundo.NovaSemente().ToString();
            y += 46 * u;
            if (GUI.Button(new Rect(x, y, w, 46 * u), "Criar o mundo", _botao))
            {
                PedirOMundo();
                _sessao.Hospedar();
            }
            return y + 66 * u;
        }

        // o pedido do menu para o MundoDaPartida. Uma palavra vira um número (o mesmo para todos que digitarem ela)
        void PedirOMundo()
        {
            string texto = _semente.Trim();
            int semente = 0;
            if (texto.Length > 0 && (!int.TryParse(texto, out semente) || semente <= 0))
                semente = (int)(Sorteio.Fnv(texto.ToLowerInvariant()) % 999999UL) + 1;
            MundoDaPartida.Pedido = new PedidoDoMundo { tamanho = _tamanho, semente = semente, nome = _nomeDoMundo };
        }

        void Pausa(float u)
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_opcoes)
            {
                if (Opcoes.Painel(u, Screen.height * 0.3f, _titulo, _botao)) _opcoes = false;
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
            if (GUI.Button(new Rect(x, y + 100 * u, w, 36 * u), "Opções", _botao)) _opcoes = true;
            if (GUI.Button(new Rect(x, y + 144 * u, w, 36 * u), "Sair da partida", _botao)) { _pausa = false; _sessao.Sair(); }
        }
    }
}
