using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O menu do desenvolvedor (F1; só no Editor e nas builds de desenvolvimento): criar itens (as armas, a lata e a
    /// garrafa) na mochila ou no chão à frente, ver quantos zumbis há, matar todos e criar mais em volta do jogador. Fica
    /// no jogador (só o dono abre); os pedidos vão para o anfitrião, que faz.
    /// </summary>
    public class MenuDoDesenvolvedor : NetworkBehaviour
    {
        /// <summary>Só no Editor e nas builds de desenvolvimento.</summary>
        public static bool Disponivel => Application.isEditor || Debug.isDebugBuild;
        /// <summary>A janela na tela (vazia, fechada): a tela do inventário não pega cliques nela.</summary>
        public static Rect Area { get; private set; }

        static readonly Item[] ItensDoMenu = { Item.Pistola, Item.Rifle, Item.Submetralhadora, Item.Espingarda, Item.Sniper, Item.Comida, Item.Agua };
        static readonly string[] Destinos = { "Na mochila", "No chão" };

        bool _aberto;
        int _destino;
        string _quantos = "10", _aviso;
        float _avisoAte;
        GUIStyle _titulo, _texto, _botao, _campo;
        float _uDosEstilos;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) MenuOnline.FecharTelas += Fechar;
        }

        public override void OnNetworkDespawn()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (_aberto) Abrir(false);
        }

        void Fechar() => Abrir(false);

        void Abrir(bool abrir)
        {
            _aberto = abrir;
            MenuOnline.Tela(this, abrir);
            if (!abrir) Area = default;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || !Disponivel || MenuOnline.Bot) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.f1Key.wasPressedThisFrame && !MenuOnline.Aberto) Abrir(!_aberto);
        }

        void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.time + 3f;
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.98f, 0.82f, 0.4f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), wordWrap = true };
            _texto.normal.textColor = new Color(0.88f, 0.86f, 0.8f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(13 * u) };
            _botao.onNormal.textColor = _botao.onHover.textColor = _botao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            _botao.onNormal.background = _botao.active.background;
            _campo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(14 * u), alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (!_aberto || !IsSpawned || !IsOwner || MenuOnline.Aberto || ModoVideo.Ligado) return;
            GUI.depth = -10;   // por cima da tela do inventário
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            var area = new Rect(12 * u, 40 * u, 300 * u, 404 * u);
            Area = area;
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "DESENVOLVEDOR  (F1)", _titulo);
            y += 30 * u;

            // os zumbis
            var zm = ZombieManager.Instancia;
            string contagem = "Zumbis: —";
            if (zm != null && zm.IsSpawned)
            {
                zm.ContarNiveis(out int perto, out int medio, out int longe);
                contagem = zm.IsServer
                    ? $"Zumbis vivos: {zm.Vivos}   (mortos {zm.Mortos})\nperto {perto} · médio {medio} · longe {longe}"
                    : $"Zumbis vivos perto de você: {zm.Vivos}\nperto {perto} · médio {medio}";
            }
            GUI.Label(new Rect(x, y, w, 36 * u), contagem, _texto);
            y += 42 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), "Matar todos os zumbis", _botao))
            {
                MatarZumbisRpc();
                Avisar("Todos os zumbis morreram.");
            }
            y += 36 * u;
            GUI.Label(new Rect(x, y + 4 * u, 46 * u, 22 * u), "Criar", _texto);
            _quantos = GUI.TextField(new Rect(x + 44 * u, y, 60 * u, 28 * u), _quantos, 4, _campo);
            if (GUI.Button(new Rect(x + 112 * u, y, w - 112 * u, 28 * u), "zumbis perto de mim", _botao))
            {
                if (int.TryParse(_quantos, out int n) && n > 0)
                {
                    n = Mathf.Min(n, 300);
                    CriarZumbisRpc((ushort)n);
                    Avisar($"Criando {n} zumbis de 6 a 14 m daqui.");
                }
                else Avisar("Escreva um número.");
            }
            y += 44 * u;

            // os itens
            GUI.Label(new Rect(x, y, w, 22 * u), "Itens", _titulo);
            y += 26 * u;
            _destino = GUI.SelectionGrid(new Rect(x, y, w, 26 * u), _destino, Destinos, 2, _botao);
            y += 34 * u;
            float bw = (w - 8 * u) / 2f;
            var inventario = GetComponent<InventarioJogador>();
            for (int i = 0; i < ItensDoMenu.Length; i++)
            {
                var item = ItensDoMenu[i];
                var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                string nome = Itens.Nome(item);
                if (!GUI.Button(r, char.ToUpper(nome[0]) + nome.Substring(1), _botao)) continue;
                bool noChao = _destino == 1;
                if (!noChao && inventario != null && !inventario.TemEspaco(item))
                {
                    noChao = true;
                    Avisar($"Sem espaço na mochila: {Itens.NomeComArtigo(item)} foi para o chão.");
                }
                CriarItemRpc(item, noChao);
            }
            y += 4 * 32 * u + 4 * u;
            if (_aviso != null && Time.time < _avisoAte) GUI.Label(new Rect(x, y, w, 36 * u), _aviso, _texto);
        }

        // ------------------------------------------------------------------ no anfitrião

        bool Aceita(RpcParams parametros) => Disponivel && parametros.Receive.SenderClientId == OwnerClientId;

        [Rpc(SendTo.Server)]
        void CriarItemRpc(Item item, bool noChao, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || item == Item.Nada) return;
            var inventario = GetComponent<InventarioJogador>();
            if (!noChao && inventario != null && inventario.Receber(item)) return;
            if (ItensNoMundo.Instancia == null) return;
            // no chão, 1,2 m à frente (onde um raio de cima bater)
            Vector3 frente = transform.position + transform.forward * 1.2f;
            var ponto = Physics.Raycast(frente + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore)
                ? chao.point : transform.position;
            ItensNoMundo.Instancia.Criar(item, ponto, transform.eulerAngles.y + 90f);
        }

        [Rpc(SendTo.Server)]
        void MatarZumbisRpc(RpcParams parametros = default)
        {
            if (Aceita(parametros)) ZombieManager.Instancia?.MatarTodos();
        }

        [Rpc(SendTo.Server)]
        void CriarZumbisRpc(ushort quantos, RpcParams parametros = default)
        {
            if (Aceita(parametros)) ZombieManager.Instancia?.CriarPerto(transform.position, Mathf.Min((int)quantos, 300));
        }
    }
}
