using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O inventário do jogador: 3 lugares, cada um com uma arma, uma lata de comida ou uma garrafa de água. O do lugar
    /// escolhido fica na mão. A roda do mouse passa pelos lugares ocupados e pela mão vazia, se houver um lugar vazio
    /// (para baixo, o próximo), e 1, 2 e 3 escolhem um lugar (um vazio deixa as mãos vazias). G larga o que está na mão: no tampo para onde a mira aponta, se estiver
    /// perto (a mesa do depósito do bunker, por exemplo), senão no chão à frente.
    /// Pegar (E, num item do chão ou numa arma do painel) põe o item na mão, se ela estiver vazia, ou no primeiro lugar
    /// livre; com tudo cheio, ele troca de lugar com o da mão. A arma pega vai para a mão.
    /// O conteúdo é do anfitrião (pegar, largar, comer e o painel passam por ele); o lugar escolhido é do dono.
    /// Na tela: os 3 lugares embaixo, no meio, com o ícone de cada coisa.
    /// </summary>
    public class InventarioJogador : NetworkBehaviour
    {
        public const int Lugares = 3;

        [Tooltip("Os ícones, na ordem de Itens.Modelos (as armas, a lata e a garrafa).")]
        public Texture2D[] icones;

        /// <summary>Os 3 lugares, um byte cada (<see cref="Item"/>). O jogador nasce com a pistola no primeiro.</summary>
        public readonly NetworkVariable<int> Conteudo = new((int)Item.Pistola, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O lugar da mão (0 a 2).</summary>
        public readonly NetworkVariable<byte> Escolhido = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Item this[int lugar] => Ler(Conteudo.Value, lugar);
        public Item NaMao => this[Escolhido.Value];

        /// <summary>O conteúdo ou o lugar da mão mudou (em todas as cópias).</summary>
        public event System.Action Mudou;

        // o raio de largar não vê o próprio jogador, os braços, os zumbis, os corpos e o Ignore Raycast
        const int MascaraDoCenario = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        ArmaJogador _arma;
        JogadorRede _rede;
        float _pegouAte, _nomeAte;
        int _consumindo = -1, _voltarPara = -1;

        public override void OnNetworkSpawn()
        {
            _arma = GetComponent<ArmaJogador>();
            _rede = GetComponent<JogadorRede>();
            Conteudo.OnValueChanged += AoMudarConteudo;
            Escolhido.OnValueChanged += AoMudarEscolhido;
        }

        public override void OnNetworkDespawn()
        {
            Conteudo.OnValueChanged -= AoMudarConteudo;
            Escolhido.OnValueChanged -= AoMudarEscolhido;
        }

        static Item Ler(int conteudo, int lugar) => (Item)((conteudo >> (8 * lugar)) & 0xFF);
        static int Gravar(int conteudo, int lugar, Item item) => (conteudo & ~(0xFF << (8 * lugar))) | ((int)item << (8 * lugar));

        void AoMudarConteudo(int antes, int depois)
        {
            if (IsOwner)
            {
                int ir = -1;
                for (int i = 0; i < Lugares; i++)
                {
                    Item a = Ler(antes, i), d = Ler(depois, i);
                    if (a == d) continue;
                    // o que o jogador acabou de pegar vai para a mão se for uma arma, ou se a mão estava vazia
                    if (a == Item.Nada && d != Item.Nada && Time.time < _pegouAte && (Itens.EhArma(d) || Ler(antes, Escolhido.Value) == Item.Nada))
                        ir = i;
                    // acabou de comer ou beber: a mão volta para a última arma
                    if (i == _consumindo && d == Item.Nada)
                    {
                        if (Escolhido.Value == i && _voltarPara >= 0 && Itens.EhArma(Ler(depois, _voltarPara))) ir = _voltarPara;
                        _consumindo = -1;
                    }
                }
                if (ir >= 0 && ir != Escolhido.Value) Escolhido.Value = (byte)ir;   // avisa (Mudou) já com o lugar novo
            }
            Mudou?.Invoke();
        }

        void AoMudarEscolhido(byte antes, byte depois)
        {
            _nomeAte = Time.time + 1.6f;
            Mudou?.Invoke();
        }

        /// <summary>O dono vai pedir para pegar algo: o que chegar logo depois pode ir para a mão.</summary>
        public void AvisarQuePegou() => _pegouAte = Time.time + 2f;

        /// <summary>
        /// O lugar onde entra o que o jogador pegar: o da mão, se estiver vazio; senão, o primeiro livre; -1 com tudo
        /// cheio.
        /// </summary>
        public int LugarParaReceber()
        {
            if (NaMao == Item.Nada) return Escolhido.Value;
            for (int i = 0; i < Lugares; i++)
                if (this[i] == Item.Nada) return i;
            return -1;
        }

        /// <summary>Só no anfitrião: põe um item num lugar (Nada esvazia).</summary>
        public void Por(int lugar, Item item)
        {
            if (IsServer && lugar >= 0 && lugar < Lugares) Conteudo.Value = Gravar(Conteudo.Value, lugar, item);
        }

        /// <summary>
        /// Só no anfitrião, quando o jogador sai da partida: as armas voltam para o painel do bunker (se houver vaga do
        /// tipo delas) e a comida e a água ficam no chão, onde ele estava.
        /// </summary>
        public void Esvaziar()
        {
            for (int i = 0; i < Lugares; i++)
            {
                var item = this[i];
                if (Itens.EhArma(item)) ParedeDeArmas.Instancia?.Devolver(Itens.Arma(item));
                else if (Itens.EhConsumivel(item) && ItensNoMundo.Instancia != null)
                    ItensNoMundo.Instancia.Criar(item, transform.position + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.3f, i * 90f);
            }
        }

        /// <summary>
        /// O dono terminou de comer ou beber o item do lugar: ele acaba, e a fome e a sede caem. Depois, a mão volta para
        /// o lugar da última arma (se ela ainda estiver lá).
        /// </summary>
        public void Consumir(int lugar, int voltarPara)
        {
            _consumindo = lugar;
            _voltarPara = voltarPara;
            ConsumirRpc((byte)lugar);
        }

        [Rpc(SendTo.Server)]
        void ConsumirRpc(byte lugar, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || lugar >= Lugares) return;
            var item = this[lugar];
            if (!Itens.EhConsumivel(item)) return;
            Por(lugar, Item.Nada);
            GetComponent<FomeESede>().Consumir(Itens.Consumivel(item));
        }

        [Rpc(SendTo.Server)]
        void LargarRpc(byte lugar, Vector3 ponto, float giro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || lugar >= Lugares) return;
            var item = this[lugar];
            if (item == Item.Nada || ItensNoMundo.Instancia == null) return;
            if ((ponto - transform.position).sqrMagnitude > 4f * 4f) ponto = transform.position;
            Por(lugar, Item.Nada);
            ItensNoMundo.Instancia.Criar(item, ponto, giro);
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
            if (teclado != null && teclado.gKey.wasPressedThisFrame) Largar();
        }

        // o próximo lugar naquele sentido (-1 se não há outro): os ocupados e um vazio só, a mão vazia (o da mão, se ele
        // estiver vazio, senão o primeiro vazio; com tudo cheio, não tem)
        int Proximo(int sentido)
        {
            int vazio = NaMao == Item.Nada ? Escolhido.Value : -1;
            for (int i = 0; i < Lugares && vazio < 0; i++)
                if (this[i] == Item.Nada) vazio = i;
            for (int k = 1; k < Lugares; k++)
            {
                int i = ((Escolhido.Value + sentido * k) % Lugares + Lugares) % Lugares;
                if (this[i] != Item.Nada || i == vazio) return i;
            }
            return -1;
        }

        void Largar()
        {
            if (NaMao == Item.Nada) return;
            LargarRpc(Escolhido.Value, PontoParaLargar(_rede.cameraMundo.transform), transform.eulerAngles.y + Random.Range(-50f, 50f));
            Efeitos.Som(Efeitos.Largar, transform.position + Vector3.up * 0.8f, 0.5f, false);
        }

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

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto) return;
            float u = Screen.height / 720f;
            float w = 112 * u, h = 56 * u, vao = 8 * u;
            float x0 = (Screen.width - (w * Lugares + vao * (Lugares - 1))) / 2f, y = Screen.height - h - 16 * u;
            var numero = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * u), fontStyle = FontStyle.Bold };
            for (int i = 0; i < Lugares; i++)
            {
                float x = x0 + i * (w + vao);
                bool escolhido = i == Escolhido.Value;
                Retangulo(x, y, w, h, escolhido ? new Color(0.17f, 0.16f, 0.14f, 0.78f) : new Color(0f, 0f, 0f, 0.45f));
                if (escolhido)
                {
                    float b = Mathf.Max(1f, Mathf.Round(2 * u));
                    var cor = new Color(0.9f, 0.86f, 0.72f, 0.9f);
                    Retangulo(x, y, w, b, cor); Retangulo(x, y + h - b, w, b, cor);
                    Retangulo(x, y, b, h, cor); Retangulo(x + w - b, y, b, h, cor);
                }
                int m = Itens.Modelo(this[i]);
                if (m >= 0 && icones != null && m < icones.Length && icones[m] != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, escolhido ? 1f : 0.55f);
                    GUI.DrawTexture(new Rect(x + 6 * u, y + 5 * u, w - 12 * u, h - 10 * u), icones[m], ScaleMode.ScaleToFit);
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
