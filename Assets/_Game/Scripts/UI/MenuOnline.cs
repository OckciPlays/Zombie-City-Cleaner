using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Network;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// Menu simples (OnGUI): hospedar, entrar com código ou testar em modo local. Na partida, mostra o código no
    /// canto e ESC abre o menu (o mundo continua rodando, porque é online).
    /// Linha de comando para testes: -entrar CÓDIGO, -hospedar-local, -entrar-local [ip] e -bot (o jogador anda e
    /// atira sozinho).
    /// </summary>
    public class MenuOnline : MonoBehaviour
    {
        /// <summary>Menu aberto: o jogador local não recebe comandos.</summary>
        public static bool Aberto { get; private set; } = true;
        /// <summary>Teste automático: o jogador local anda em círculo, atira e liga a lanterna sozinho.</summary>
        public static bool Bot { get; private set; }

        SessaoOnline _sessao;
        string _codigo = "", _ip = "127.0.0.1";
        bool _pausa;
        GUIStyle _titulo, _texto, _botao, _campo, _erro;

        void Start()
        {
            _sessao = SessaoOnline.Instancia;
            var args = Environment.GetCommandLineArgs();
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
            bool naPartida = _sessao.Atual == SessaoOnline.Estado.Conectado;
            var teclado = Keyboard.current;
            if (naPartida && teclado != null && teclado.escapeKey.wasPressedThisFrame) _pausa = !_pausa;
            if (!naPartida) _pausa = false;
            Aberto = !naPartida || _pausa;
            if (Aberto && !Bot)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Bot && naPartida && Time.time >= _proximoRelatorio) Relatorio();
        }

        // No modo robô, registra no log o que esta cópia enxerga dos outros jogadores (para testes sem tela).
        float _proximoRelatorio;
        void Relatorio()
        {
            _proximoRelatorio = Time.time + 3f;
            var zumbis = Zombies.ZombieManager.Instancia;
            int perto = 0, medio = 0;
            if (zumbis != null && zumbis.IsSpawned) zumbis.ContarNiveis(out perto, out medio, out _);
            int semCabeca = 0, mortos = 0;
            foreach (var z in FindObjectsByType<Zombies.Zumbi>())
            {
                if (z.Morto) mortos++;
                if (z.SemCabeca) semCabeca++;
            }
            Debug.Log($"[robô] hora {World.CicloDoDia.Relogio()}, zumbis {(zumbis != null && zumbis.IsSpawned ? zumbis.Quantos : 0)} (perto {perto}, médio {medio}; caídos {mortos}, sem cabeça {semCabeca}), vida {Player.JogadorRede.Local?.Vida?.Vida.Value ?? 0f:F0}");
            foreach (var j in Player.JogadorRede.Todos)
                Debug.Log($"[robô] jogador {j.OwnerClientId}{(j.IsOwner ? " (eu)" : "")}: posição {j.transform.position}, olhar {j.Pitch.Value:F0}°, tiros recebidos {j.TirosRecebidos}, lanterna {j.GetComponent<Player.LanternaJogador>().Ligada.Value}");
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
            _campo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * u), alignment = TextAnchor.MiddleCenter };
            _erro = new GUIStyle(_texto);
            _erro.normal.textColor = new Color(1f, 0.45f, 0.4f);
        }

        void OnGUI()
        {
            Estilos();
            float u = Screen.height / 720f;
            var estado = _sessao.Atual;
            if (estado == SessaoOnline.Estado.Conectado)
            {
                string codigo = _sessao.Local ? "partida local" : $"código {_sessao.Codigo}";
                GUI.Label(new Rect(14 * u, 10 * u, 600 * u, 24 * u), $"{codigo}  ·  {_sessao.Jogadores}/{SessaoOnline.MaxJogadores} jogadores  ·  ESC: menu", new GUIStyle(_texto) { alignment = TextAnchor.UpperLeft });
                if (_pausa) Pausa(u);
                return;
            }

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = 420 * u, x = (Screen.width - w) / 2f, y = Screen.height * 0.18f;
            GUI.Label(new Rect(0, y, Screen.width, 50 * u), "ZOMBIE CITY CLEANER", _titulo);
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
            if (GUI.Button(new Rect(x + w * 0.65f, y, w * 0.35f, 40 * u), "Entrar", _botao)) _sessao.Entrar(_codigo);
            y += 64 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "Teste sem internet (mesma rede):", _texto);
            y += 26 * u;
            if (GUI.Button(new Rect(x, y, w * 0.48f, 32 * u), "Hospedar local", _botao)) _sessao.HospedarLocal();
            _ip = GUI.TextField(new Rect(x + w * 0.52f, y, w * 0.26f, 32 * u), _ip, 40, _campo);
            if (GUI.Button(new Rect(x + w * 0.8f, y, w * 0.2f, 32 * u), "Entrar", _botao)) _sessao.EntrarLocal(_ip);
            y += 50 * u;
            if (!string.IsNullOrEmpty(_sessao.Erro)) GUI.Label(new Rect(x - 40 * u, y, w + 80 * u, 50 * u), _sessao.Erro, _erro);
        }

        void Pausa(float u)
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = 320 * u, x = (Screen.width - w) / 2f, y = Screen.height * 0.35f;
            GUI.Label(new Rect(0, y - 60 * u, Screen.width, 40 * u), "MENU", _titulo);
            if (!_sessao.Local)
            {
                GUI.Label(new Rect(0, y - 20 * u, Screen.width, 24 * u), $"Código para os amigos: {_sessao.Codigo}", _texto);
                if (GUI.Button(new Rect(x, y + 12 * u, w, 36 * u), "Copiar código", _botao)) GUIUtility.systemCopyBuffer = _sessao.Codigo;
            }
            if (GUI.Button(new Rect(x, y + 56 * u, w, 36 * u), "Continuar", _botao)) _pausa = false;
            if (GUI.Button(new Rect(x, y + 100 * u, w, 36 * u), "Sair da partida", _botao)) { _pausa = false; _sessao.Sair(); }
        }
    }
}
