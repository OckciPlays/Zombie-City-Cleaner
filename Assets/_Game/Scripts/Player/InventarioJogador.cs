using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Um item na mochila: o Id (único na mochila), o que é, a célula de cima, à esquerda, que ele ocupa e se está girado
    /// (90°: a largura e a altura trocam).
    /// </summary>
    public struct ItemNaMochila : INetworkSerializeByMemcpy, IEquatable<ItemNaMochila>
    {
        public ushort id;
        public Item item;
        public byte x, y;
        public bool girado;

        /// <summary>As células que ele ocupa, com o giro.</summary>
        public Vector2Int Tamanho => InventarioJogador.TamanhoNaGrade(item, girado);

        public bool Equals(ItemNaMochila o) => id == o.id && item == o.item && x == o.x && y == o.y && girado == o.girado;
    }

    /// <summary>
    /// O inventário do jogador:
    /// - a mochila: uma grade de <see cref="Colunas"/> × <see cref="Linhas"/> células, e cada item ocupa as dele
    ///   (<see cref="Itens.Tamanho"/>: a sniper 7 × 2, a lata 1), deitado ou girado 90° (a tela gira com R). Sem pilhas.
    ///   O que é pego entra na primeira posição em que couber (de cima para baixo, da esquerda para a direita; se não
    ///   couber deitado, girado); sem espaço, não entra;
    /// - a barra de atalhos: 3 lugares, cada um apontando para um item da mochila (o item continua nela). O do lugar
    ///   escolhido fica na mão; um lugar vazio deixa a mão vazia (os punhos). A roda passa pelos 3, vazios ou não, 1, 2 e
    ///   3 escolhem, G larga o da mão.
    /// A arma pega vai para um atalho livre e para a mão; o que é pego com a mão vazia vai para a mão.
    /// A mochila é do anfitrião (pegar, largar, comer, mover e o painel de armas passam por ele); os atalhos e o lugar
    /// escolhido são do dono. A tela da mochila (TAB) é a <see cref="TelaDoInventario"/>; aqui fica a barra do HUD.
    /// </summary>
    public class InventarioJogador : NetworkBehaviour
    {
        public const int Atalhos = 3, Colunas = 9, Linhas = 6;

        [Tooltip("Os ícones coloridos, na ordem de Itens.Modelos (as armas, a lata e a garrafa).")]
        public Texture2D[] icones;
        [Tooltip("A foto de cada modelo, na mesma ordem (os detalhes da tela do inventário).")]
        public Texture2D[] fotos;

        /// <summary>A mochila (escrita pelo anfitrião). O jogador nasce com a pistola, no primeiro atalho.</summary>
        public NetworkList<ItemNaMochila> Mochila { get; private set; }
        // os 3 atalhos, 16 bits cada (o Id do item na mochila; 0 = vazio), escritos pelo dono
        readonly NetworkVariable<long> _atalhos = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>O atalho da mão (0 a 2).</summary>
        public readonly NetworkVariable<byte> Escolhido = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>O Id do item de um atalho (0, vazio; pode apontar para um item que acabou de sair da mochila).</summary>
        public ushort Atalho(int lugar) => Ler(_atalhos.Value, lugar);
        /// <summary>O item de um atalho (Nada, vazio).</summary>
        public Item this[int lugar] => Do(Atalho(lugar));
        public Item NaMao => Do(Atalho(Escolhido.Value));
        /// <summary>O Id do item da mão (0, a mão vazia).</summary>
        public ushort IdNaMao => NaMao == Item.Nada ? (ushort)0 : Atalho(Escolhido.Value);

        /// <summary>A mochila, os atalhos ou o atalho da mão mudaram (em todas as cópias).</summary>
        public event Action Mudou;

        // o raio de largar não vê o próprio jogador, os braços, os zumbis, os corpos e o Ignore Raycast
        const int MascaraDoCenario = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        ArmaJogador _arma;
        JogadorRede _rede;
        TelaDoInventario _tela;
        float _pegouAte, _nomeAte;
        ushort _proximoId = 1, _consumindo, _voltarPara;

        void Awake() => Mochila = new NetworkList<ItemNaMochila>();

        public override void OnNetworkSpawn()
        {
            _arma = GetComponent<ArmaJogador>();
            _rede = GetComponent<JogadorRede>();
            _tela = GetComponent<TelaDoInventario>();
            Mochila.OnListChanged += AoMudarMochila;
            _atalhos.OnValueChanged += AoMudarAtalhos;
            Escolhido.OnValueChanged += AoMudarEscolhido;
            if (IsServer)
            {
                Mochila.Clear();
                Receber(Item.Pistola);
            }
            // a pistola do começo no primeiro atalho
            if (IsOwner && _atalhos.Value == 0 && Mochila.Count > 0) _atalhos.Value = Mochila[0].id;
            Mudou?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Mochila.OnListChanged -= AoMudarMochila;
            _atalhos.OnValueChanged -= AoMudarAtalhos;
            Escolhido.OnValueChanged -= AoMudarEscolhido;
        }

        static ushort Ler(long atalhos, int lugar) => (ushort)((atalhos >> (16 * lugar)) & 0xFFFF);
        static long Gravar(long atalhos, int lugar, ushort id) => (atalhos & ~(0xFFFFL << (16 * lugar))) | ((long)id << (16 * lugar));

        /// <summary>O item da mochila com este Id (Nada, se não está nela).</summary>
        public Item Do(ushort id) => id != 0 && Achar(id, out var it) ? it.item : Item.Nada;

        public bool Achar(ushort id, out ItemNaMochila item)
        {
            int k = Indice(id);
            item = k >= 0 ? Mochila[k] : default;
            return k >= 0;
        }

        int Indice(ushort id)
        {
            for (int i = 0; i < Mochila.Count; i++)
                if (Mochila[i].id == id) return i;
            return -1;
        }

        /// <summary>O atalho que aponta para o item (-1, nenhum).</summary>
        public int LugarDoAtalho(ushort id)
        {
            for (int i = 0; id != 0 && i < Atalhos; i++)
                if (Atalho(i) == id) return i;
            return -1;
        }

        // ------------------------------------------------------------------ a grade

        /// <summary>As células que o item ocupa na grade: as dele, ou trocadas, girado.</summary>
        public static Vector2Int TamanhoNaGrade(Item item, bool girado)
        {
            var t = Itens.Tamanho(item);
            return girado ? new Vector2Int(t.y, t.x) : t;
        }

        /// <summary>
        /// O item cabe (girado ou não) com o canto de cima, à esquerda, nesta célula, sem contar o item 'ignorar' (o que
        /// se move).
        /// </summary>
        public bool Cabe(Item item, bool girado, int x, int y, ushort ignorar = 0)
        {
            var t = TamanhoNaGrade(item, girado);
            if (t.x <= 0 || x < 0 || y < 0 || x + t.x > Colunas || y + t.y > Linhas) return false;
            for (int i = 0; i < Mochila.Count; i++)
            {
                var o = Mochila[i];
                if (o.id == ignorar) continue;
                var s = o.Tamanho;
                if (x < o.x + s.x && o.x < x + t.x && y < o.y + s.y && o.y < y + t.y) return false;
            }
            return true;
        }

        /// <summary>
        /// A primeira posição em que o item cabe (de cima para baixo, da esquerda para a direita), deitado; se não houver,
        /// girado.
        /// </summary>
        public bool Espaco(Item item, out int x, out int y, out bool girado)
        {
            var t = Itens.Tamanho(item);
            for (int g = 0; g < (t.x != t.y ? 2 : 1); g++)
            {
                girado = g == 1;
                for (y = 0; y < Linhas; y++)
                    for (x = 0; x < Colunas; x++)
                        if (Cabe(item, girado, x, y)) return true;
            }
            x = y = -1;
            girado = false;
            return false;
        }

        public bool TemEspaco(Item item) => Espaco(item, out _, out _, out _);

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>Só no anfitrião: o item entra na primeira posição em que cabe (deitado, ou girado). Sem espaço, devolve false.</summary>
        public bool Receber(Item item)
        {
            if (!IsServer || item == Item.Nada || !Espaco(item, out int x, out int y, out bool girado)) return false;
            Mochila.Add(new ItemNaMochila { id = _proximoId, item = item, x = (byte)x, y = (byte)y, girado = girado });
            _proximoId = (ushort)(_proximoId == ushort.MaxValue ? 1 : _proximoId + 1);
            return true;
        }

        /// <summary>Só no anfitrião: tira o item da mochila e devolve o que ele era (Nada, se não estava).</summary>
        public Item Tirar(ushort id)
        {
            int k = IsServer ? Indice(id) : -1;
            if (k < 0) return Item.Nada;
            var item = Mochila[k].item;
            Mochila.RemoveAt(k);
            return item;
        }

        /// <summary>
        /// Só no anfitrião, quando o jogador sai da partida: as armas voltam para o painel do bunker (se houver vaga do
        /// tipo delas) e a comida e a água ficam no chão, onde ele estava.
        /// </summary>
        public void Esvaziar()
        {
            for (int i = 0; i < Mochila.Count; i++)
            {
                var item = Mochila[i].item;
                if (Itens.EhArma(item)) ParedeDeArmas.Instancia?.Devolver(Itens.Arma(item));
                else if (Itens.EhConsumivel(item) && ItensNoMundo.Instancia != null)
                    ItensNoMundo.Instancia.Criar(item, transform.position + Quaternion.Euler(0f, i * 75f, 0f) * Vector3.forward * 0.35f, i * 90f);
            }
        }

        [Rpc(SendTo.Server)]
        void MoverRpc(ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(id);
            if (parametros.Receive.SenderClientId != OwnerClientId || k < 0) return;
            var it = Mochila[k];
            if ((it.x == x && it.y == y && it.girado == girado) || !Cabe(it.item, girado, x, y, id)) return;
            it.x = x;
            it.y = y;
            it.girado = girado;
            Mochila[k] = it;
        }

        [Rpc(SendTo.Server)]
        void ConsumirRpc(ushort id, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !Itens.EhConsumivel(Do(id))) return;
            var item = Tirar(id);
            GetComponent<FomeESede>().Consumir(Itens.Consumivel(item));
        }

        [Rpc(SendTo.Server)]
        void LargarRpc(ushort id, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            var item = Tirar(id);
            if (item != Item.Nada) ItensNoMundo.Instancia.Criar(item, ponto, giro);
        }

        // ------------------------------------------------------------------ no dono

        /// <summary>O dono vai pedir para pegar algo: o que chegar logo depois pode ir para a mão.</summary>
        public void AvisarQuePegou() => _pegouAte = Time.time + 2f;

        /// <summary>Move o item para outra posição da grade, girado ou não (o anfitrião confere se cabe).</summary>
        public void Mover(ushort id, int x, int y, bool girado)
        {
            if (x >= 0 && y >= 0 && x < Colunas && y < Linhas) MoverRpc(id, (byte)x, (byte)y, girado);
        }

        /// <summary>Põe o item num atalho (se ele já estava em outro, sai de lá).</summary>
        public void PorNoAtalho(int lugar, ushort id)
        {
            if (lugar < 0 || lugar >= Atalhos || Do(id) == Item.Nada || _arma.Usando) return;
            long a = _atalhos.Value;
            for (int i = 0; i < Atalhos; i++)
                if (Ler(a, i) == id) a = Gravar(a, i, 0);
            _atalhos.Value = Gravar(a, lugar, id);
        }

        public void TirarDoAtalho(int lugar)
        {
            if (lugar >= 0 && lugar < Atalhos && !_arma.Usando) _atalhos.Value = Gravar(_atalhos.Value, lugar, 0);
        }

        public void TrocarAtalhos(int de, int para)
        {
            if (de == para || de < 0 || para < 0 || de >= Atalhos || para >= Atalhos || _arma.Usando) return;
            long a = _atalhos.Value;
            ushort x = Ler(a, de), y = Ler(a, para);
            _atalhos.Value = Gravar(Gravar(a, de, y), para, x);
        }

        /// <summary>Larga o item: no tampo para onde a mira aponta, se estiver perto, senão no chão à frente.</summary>
        public void Largar(ushort id)
        {
            if (Do(id) == Item.Nada || (_arma.Usando && id == IdNaMao)) return;
            LargarRpc(id, PontoParaLargar(_rede.cameraMundo.transform), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

        /// <summary>
        /// O dono terminou de comer ou beber o item: ele acaba, e a fome e a sede caem. Depois, a mão volta para o
        /// atalho da última arma (se ela ainda estiver num).
        /// </summary>
        public void Consumir(ushort id, ushort voltarPara)
        {
            _consumindo = id;
            _voltarPara = voltarPara;
            ConsumirRpc(id);
        }

        void AoMudarMochila(NetworkListEvent<ItemNaMochila> e)
        {
            if (IsOwner)
            {
                long a = _atalhos.Value;
                int escolher = -1;
                // o atalho de um item que saiu da mochila fica vazio; se era o que acabou de ser comido, a mão volta
                // para a última arma
                for (int i = 0; i < Atalhos; i++)
                {
                    ushort id = Ler(a, i);
                    if (id == 0 || Do(id) != Item.Nada) continue;
                    a = Gravar(a, i, 0);
                    if (id == _consumindo && i == Escolhido.Value)
                    {
                        for (int k = 0; k < Atalhos; k++)
                            if (_voltarPara != 0 && Ler(a, k) == _voltarPara) escolher = k;
                        _consumindo = 0;
                    }
                }
                // o que o jogador acabou de pegar: com a mão vazia, vai para a mão; uma arma vai para um atalho livre
                // (e para a mão)
                if (e.Type == NetworkListEvent<ItemNaMochila>.EventType.Add && Time.time < _pegouAte)
                {
                    _pegouAte = 0f;
                    int lugar = -1;
                    if (Ler(a, Escolhido.Value) == 0) lugar = Escolhido.Value;
                    else if (Itens.EhArma(e.Value.item))
                        for (int i = Atalhos - 1; i >= 0; i--)
                            if (Ler(a, i) == 0) lugar = i;
                    if (lugar >= 0)
                    {
                        a = Gravar(a, lugar, e.Value.id);
                        escolher = lugar;
                    }
                }
                if (a != _atalhos.Value) _atalhos.Value = a;
                if (escolher >= 0 && escolher != Escolhido.Value) Escolhido.Value = (byte)escolher;
            }
            Mudou?.Invoke();
        }

        void AoMudarAtalhos(long antes, long depois) => Mudou?.Invoke();

        void AoMudarEscolhido(byte antes, byte depois)
        {
            _nomeAte = Time.time + 1.6f;
            Mudou?.Invoke();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Bot || !MenuOnline.Controla) return;
            if (_arma.Usando) return;   // comendo ou bebendo: nem troca nem larga
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            int escolher = -1;
            if (teclado != null)
            {
                if (teclado.digit1Key.wasPressedThisFrame) escolher = 0;
                else if (teclado.digit2Key.wasPressedThisFrame) escolher = 1;
                else if (teclado.digit3Key.wasPressedThisFrame) escolher = 2;
            }
            if (mouse != null)
            {
                float roda = mouse.scroll.ReadValue().y;
                if (roda != 0f) escolher = Proximo(roda < 0f ? 1 : -1);
            }
            if (escolher >= 0 && escolher != Escolhido.Value) Escolhido.Value = (byte)escolher;
            if (teclado != null && teclado.gKey.wasPressedThisFrame) Largar(IdNaMao);
        }

        // o próximo atalho naquele sentido, vazio ou não (um vazio deixa a mão vazia)
        int Proximo(int sentido) => ((Escolhido.Value + sentido) % Atalhos + Atalhos) % Atalhos;

        // olhando para um tampo perto (uma mesa, um balcão, o chão logo ali): ali mesmo. Senão, no chão à frente, sem
        // atravessar a parede
        Vector3 PontoParaLargar(Transform cam)
        {
            if (Physics.Raycast(cam.position, cam.forward, out var acerto, 2.2f, MascaraDoCenario, QueryTriggerInteraction.Ignore) && acerto.normal.y > 0.7f)
                return acerto.point;
            Vector3 peito = transform.position + Vector3.up;
            float d = Physics.Raycast(peito, transform.forward, out var parede, 0.85f, MascaraDoCenario, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(0f, parede.distance - 0.25f) : 0.6f;
            if (Physics.Raycast(peito + transform.forward * d, Vector3.down, out var chao, 3f, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                return chao.point;
            return transform.position;
        }

        /// <summary>O ícone colorido do item (null, nada).</summary>
        public Texture2D IconeDe(Item item)
        {
            int m = Itens.Modelo(item);
            return m >= 0 && icones != null && m < icones.Length ? icones[m] : null;
        }

        public Texture2D FotoDe(Item item)
        {
            int m = Itens.Modelo(item);
            return m >= 0 && fotos != null && m < fotos.Length ? fotos[m] : null;
        }

        // a barra de atalhos, embaixo, no meio (com a tela do inventário aberta, ela fica lá dentro)
        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || (_tela != null && _tela.Aberta)) return;
            float u = Screen.height / 720f;
            float w = 112 * u, h = 56 * u, vao = 8 * u;
            float x0 = (Screen.width - (w * Atalhos + vao * (Atalhos - 1))) / 2f, y = Screen.height - h - 16 * u;
            var numero = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * u), fontStyle = FontStyle.Bold };
            for (int i = 0; i < Atalhos; i++)
            {
                float x = x0 + i * (w + vao);
                bool escolhido = i == Escolhido.Value;
                // o fundo claro: as armas do ícone são escuras
                Retangulo(x, y, w, h, escolhido ? new Color(0.5f, 0.5f, 0.47f, 0.82f) : new Color(0.36f, 0.36f, 0.34f, 0.6f));
                if (escolhido)
                {
                    float b = Mathf.Max(1f, Mathf.Round(2 * u));
                    var cor = new Color(0.9f, 0.86f, 0.72f, 0.9f);
                    Retangulo(x, y, w, b, cor); Retangulo(x, y + h - b, w, b, cor);
                    Retangulo(x, y, b, h, cor); Retangulo(x + w - b, y, b, h, cor);
                }
                var icone = IconeDe(this[i]);
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, escolhido ? 1f : 0.8f);
                    GUI.DrawTexture(new Rect(x + 6 * u, y + 5 * u, w - 12 * u, h - 10 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                numero.normal.textColor = escolhido ? new Color(0.95f, 0.92f, 0.8f) : new Color(0.7f, 0.68f, 0.62f, 0.8f);
                GUI.Label(new Rect(x + 5 * u, y + 2 * u, 20 * u, 18 * u), (i + 1).ToString(), numero);
            }
            // o nome do que está na mão, logo depois de trocar; e, com comida ou água, como usar
            var texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.LowerCenter };
            var naMao = NaMao;
            string linha = null;
            float alfa = 1f;
            if (Itens.EhConsumivel(naMao)) linha = $"{Itens.Nome(naMao)}   ·   clique: {Itens.Consumivel(naMao).verbo}   ·   G: largar";
            else if (Time.time < _nomeAte)
            {
                linha = naMao == Item.Nada ? "mãos vazias   ·   clique: soco" : Itens.Nome(naMao);
                alfa = Mathf.Clamp01((_nomeAte - Time.time) / 0.4f);
            }
            if (linha != null)
            {
                var r = new Rect(0f, y - 30 * u, Screen.width, 26 * u);
                texto.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alfa);
                GUI.Label(new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height), linha, texto);
                texto.normal.textColor = new Color(0.92f, 0.9f, 0.82f, alfa);
                GUI.Label(r, linha, texto);
            }
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
