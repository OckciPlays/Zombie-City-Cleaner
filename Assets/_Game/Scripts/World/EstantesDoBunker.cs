using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um item guardado numa estante do bunker: a estante (o índice do móvel em <see cref="MoveisDoBunker.Todos"/>), o Id
    /// (único entre as estantes), o que é, a célula de cima, à esquerda, se está girado e o que vai com ele (as balas, o
    /// desgaste e as cores da roupa), como na mochila.
    /// </summary>
    public struct ItemNaEstante : INetworkSerializeByMemcpy, IEquatable<ItemNaEstante>
    {
        public ushort estante, id;
        public Item item;
        public byte x, y;
        public bool girado;
        public ushort qtd;
        public byte desgaste;
        public ulong cores;

        public Vector2Int Tamanho => InventarioJogador.TamanhoNaGrade(item, girado);

        /// <summary>Como um item da mochila (o desenho da tela; o Id fica 0, para não confundir com os da mochila).</summary>
        public ItemNaMochila ComoDaMochila => new ItemNaMochila { item = item, x = x, y = y, girado = girado, qtd = qtd, desgaste = desgaste, cores = cores };

        public bool Equals(ItemNaEstante o) => estante == o.estante && id == o.id && item == o.item && x == o.x && y == o.y && girado == o.girado
                                              && qtd == o.qtd && desgaste == o.desgaste && cores == o.cores;
    }

    /// <summary>
    /// As estantes de aço e as prateleiras de madeira do bunker da família guardam itens (01/11/2026, a pedido: "ao apertar
    /// E nelas, vai abrir um sistema de colocar itens dentro, mostrando os slots que tem nela e o inventário do jogador,
    /// para eu só arrastar os itens, e aí dependendo da quantidade de itens colocados vai aparecendo na estante ou
    /// prateleira os itens até preencher"). No "Mundo" da cena (com o NetworkObject).
    /// - **A grade de cada uma** (<see cref="Forma"/>): cada tábua é uma faixa de 2 linhas (a de trás e a da frente) com as
    ///   colunas pela largura dela (uns 17 cm por célula, como a mochila); as tábuas vêm de cima para baixo. Um item fica
    ///   numa tábua só (até 2 linhas), com as células dele (<see cref="Itens.Tamanho"/>), deitado ou girado.
    /// - **A rede** (o anfitrião manda): a lista de todos os guardados (<see cref="ItemNaEstante"/>); guardar, pegar e mover
    ///   passam por ele, que confere o jogador perto da estante (<see cref="distanciaMaxima"/>) e o lugar. Por enquanto não é
    ///   salvo no disco (o mundo salvo, a M10).
    /// - **Na estante:** cada item guardado aparece na tábua dele, no lugar das células (<see cref="ItensNaEstante"/>), com a
    ///   malha e o material do chão (<see cref="ItensNoMundo.MalhaDe"/>).
    /// - **A tela** é a do inventário (TAB) com a estante no lugar dos detalhes (<c>TelaDoInventario.Estante.cs</c>): E na
    ///   estante abre (<see cref="EstanteNaMira"/>).
    /// - **O compartimento do caiaque** (07/11/2026, a pedido: "um compartimento pequeno no caiaque") é uma estante a mais,
    ///   de 2 × 2, com o número <see cref="BaseDosCaiaques"/> + o do caiaque: a mesma lista, a mesma tela e o mesmo save; o
    ///   "perto" é o da escotilha de trás dele (<see cref="Vehicles.Caiaque.PontoDoCompartimento"/>). Nada aparece por fora.
    /// </summary>
    public class EstantesDoBunker : NetworkBehaviour
    {
        [Tooltip("Até onde o anfitrião aceita os pedidos (m, dos pés do jogador ao pé da estante, no chão).")]
        public float distanciaMaxima = 3.5f;

        public static EstantesDoBunker Instancia { get; private set; }

        /// <summary>As linhas de cada tábua (a de trás e a da frente).</summary>
        public const int LinhasPorTabua = 2;

        /// <summary>
        /// A forma de uma estante (no tamanho real do modelo, sem a escala dos móveis): o nome, as colunas de cada tábua, a
        /// altura do tampo de cada tábua (de baixo para cima), a largura e o fundo em que os itens ficam, e o vão entre
        /// as tábuas (para os altos não passarem da de cima). As medidas são as do mobilia_servico.py da pasta de arte.
        /// </summary>
        public sealed class Forma
        {
            public string nome;
            public int colunas;
            public float[] alturas;
            public float largura, fundo, vao;
            public int Tabuas => alturas.Length;
            public Vector2Int Grade => new Vector2Int(colunas, Tabuas * LinhasPorTabua);
        }

        static readonly float[] TabuasDeAco = { 0.08f + 0.0015f, 0.50f + 0.0015f, 0.92f + 0.0015f, 1.34f + 0.0015f, 1.82f + 0.0015f };
        static readonly float[] TabuasDeMadeira = { 0.10f + 0.009f, 0.62f + 0.009f, 1.14f + 0.009f, 1.66f + 0.009f };

        static readonly Dictionary<string, Forma> Formas = new Dictionary<string, Forma>
        {
            ["EstanteAco90"] = new Forma { nome = "estante de aço", colunas = 6, alturas = TabuasDeAco, largura = 0.82f, fundo = 0.37f, vao = 0.42f },
            ["EstanteAco120"] = new Forma { nome = "estante de aço", colunas = 8, alturas = TabuasDeAco, largura = 1.12f, fundo = 0.37f, vao = 0.42f },
            ["PrateleiraMadeira90"] = new Forma { nome = "prateleira de madeira", colunas = 6, alturas = TabuasDeMadeira, largura = 0.79f, fundo = 0.37f, vao = 0.52f },
            ["PrateleiraMadeira150"] = new Forma { nome = "prateleira de madeira", colunas = 10, alturas = TabuasDeMadeira, largura = 1.39f, fundo = 0.37f, vao = 0.52f },
        };

        /// <summary>O número da estante do compartimento do primeiro caiaque (os outros, os seguintes).</summary>
        public const int BaseDosCaiaques = 60000;
        const string Compartimento = "CompartimentoDoCaiaque";
        static readonly Forma FormaDoCompartimento = new Forma { nome = "compartimento do caiaque", colunas = 2, alturas = new[] { 0f }, largura = 0.36f, fundo = 0.36f, vao = 0.3f };

        /// <summary>É o compartimento de um caiaque (e não um móvel do bunker)?</summary>
        public static bool EhDoCaiaque(int estante) => estante >= BaseDosCaiaques && estante < BaseDosCaiaques + 16;

        public static bool EhEstante(string modelo) => modelo != null && Formas.ContainsKey(modelo);
        public static Forma FormaDe(string modelo) => modelo != null && Formas.TryGetValue(modelo, out var f) ? f : null;

        /// <summary>O modelo da estante (o móvel do bunker; null se o índice não é de uma estante).</summary>
        public static string ModeloDa(int estante)
        {
            if (EhDoCaiaque(estante)) return Compartimento;   // (mesmo antes de o caiaque nascer: o save dele carrega)
            var todos = MoveisDoBunker.Todos;
            return estante >= 0 && estante < todos.Length && EhEstante(todos[estante].modelo) ? todos[estante].modelo : null;
        }

        public static Forma FormaDa(int estante) => EhDoCaiaque(estante) ? FormaDoCompartimento : FormaDe(ModeloDa(estante));

        NetworkList<ItemNaEstante> _itens;
        ushort _proximoId = 1;

        /// <summary>Todos os itens guardados (escrita pelo anfitrião).</summary>
        public NetworkList<ItemNaEstante> Guardados => _itens;
        /// <summary>Os guardados mudaram (a estante, ou -1: todas).</summary>
        public event Action<int> Mudou;

        void Awake() => _itens = new NetworkList<ItemNaEstante>();

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _itens.Clear();
            _itens.OnListChanged += AoMudar;
            Mudou?.Invoke(-1);
        }

        public override void OnNetworkDespawn()
        {
            _itens.OnListChanged -= AoMudar;
            Mudou?.Invoke(-1);
        }

        void AoMudar(NetworkListEvent<ItemNaEstante> e)
        {
            // a remoção não diz de qual estante era: todas conferem
            if (e.Type == NetworkListEvent<ItemNaEstante>.EventType.Add || e.Type == NetworkListEvent<ItemNaEstante>.EventType.Value)
            {
                Mudou?.Invoke(e.Value.estante);
                if (e.Type == NetworkListEvent<ItemNaEstante>.EventType.Value && e.PreviousValue.estante != e.Value.estante) Mudou?.Invoke(e.PreviousValue.estante);
            }
            else Mudou?.Invoke(-1);
        }

        /// <summary>Só no anfitrião: os itens guardados (o save, Salvar/Salvamento).</summary>
        public List<ItemNaEstante> Copia()
        {
            var lista = new List<ItemNaEstante>(_itens.Count);
            foreach (var it in _itens) lista.Add(it);
            return lista;
        }

        /// <summary>Só no anfitrião: os itens guardados do mundo salvo no lugar dos de agora (os Ids são dados de novo).</summary>
        public void Restaurar(List<ItemNaEstante> itens)
        {
            if (!IsServer || !IsSpawned) return;
            _itens.Clear();
            _proximoId = 1;
            foreach (var it in itens)
            {
                if (it.item == Item.Nada || !Itens.NoJogo(it.item) || FormaDa(it.estante) == null) continue;
                var novo = it;
                novo.id = NovoId();
                _itens.Add(novo);
            }
        }

        // ------------------------------------------------------------------ a grade

        /// <summary>Quantos itens a estante tem.</summary>
        public int Quantos(int estante)
        {
            int n = 0;
            if (IsSpawned) foreach (var it in _itens) if (it.estante == estante) n++;
            return n;
        }

        public bool Achar(int estante, ushort id, out ItemNaEstante item)
        {
            int k = Indice(estante, id);
            item = k >= 0 ? _itens[k] : default;
            return k >= 0;
        }

        int Indice(int estante, ushort id)
        {
            for (int i = 0; i < _itens.Count; i++)
                if (_itens[i].id == id && _itens[i].estante == estante) return i;
            return -1;
        }

        /// <summary>
        /// O item cabe na estante com o canto de cima, à esquerda, nesta célula (numa tábua só), sem contar o item
        /// <paramref name="ignorar"/> (o que se move).
        /// </summary>
        public bool Cabe(int estante, Item item, bool girado, int x, int y, ushort ignorar = 0)
        {
            var f = FormaDa(estante);
            var t = InventarioJogador.TamanhoNaGrade(item, girado);
            if (f == null || t.x <= 0 || x < 0 || y < 0 || x + t.x > f.colunas || y + t.y > f.Grade.y) return false;
            if (y % LinhasPorTabua + t.y > LinhasPorTabua) return false;
            foreach (var o in _itens)
            {
                if (o.estante != estante || o.id == ignorar) continue;
                var s = o.Tamanho;
                if (x < o.x + s.x && o.x < x + t.x && y < o.y + s.y && o.y < y + t.y) return false;
            }
            return true;
        }

        /// <summary>
        /// O primeiro lugar em que o item cabe, deitado (senão, girado): as tábuas pela altura, da mais perto do peito
        /// (<see cref="AlturaBoa"/>, com a escala dos móveis) para as de longe; em cada uma, da linha de trás para a da frente e
        /// da esquerda para a direita.
        /// </summary>
        public bool Espaco(int estante, Item item, out int x, out int y, out bool girado)
        {
            var f = FormaDa(estante);
            var t = Itens.Tamanho(item);
            if (f != null)
            {
                var tabuas = new List<int>();
                for (int k = 0; k < f.Tabuas; k++) tabuas.Add(k);
                tabuas.Sort((a, b) => Mathf.Abs(f.alturas[a] * MedidasDaMobilia.Escala - AlturaBoa).CompareTo(Mathf.Abs(f.alturas[b] * MedidasDaMobilia.Escala - AlturaBoa)));
                for (int g = 0; g < (t.x != t.y ? 2 : 1); g++)
                {
                    girado = g == 1;
                    foreach (int tabua in tabuas)
                    {
                        int faixa = f.Tabuas - 1 - tabua;
                        for (int linha = 0; linha < LinhasPorTabua; linha++)
                            for (x = 0; x < f.colunas; x++)
                            {
                                y = faixa * LinhasPorTabua + linha;
                                if (Cabe(estante, item, girado, x, y)) return true;
                            }
                    }
                }
            }
            x = y = -1;
            girado = false;
            return false;
        }

        /// <summary>A altura (m do chão) da tábua que se enche primeiro: a do peito.</summary>
        const float AlturaBoa = 1.1f;

        /// <summary>
        /// As estantes dos depósitos do bunker, na ordem em que se enchem: as do depósito do B1 e, de sobra, as do B3 (os
        /// índices em <see cref="MoveisDoBunker.Todos"/>).
        /// </summary>
        public static List<int> EstantesDosDepositos()
        {
            var lista = new List<int>();
            var todos = MoveisDoBunker.Todos;
            foreach (int andar in new[] { 1, 3 })
            {
                var planta = PlantaDeBunker.DoAndar(andar);
                for (int i = 0; i < todos.Length; i++)
                {
                    var m = todos[i];
                    if (m.andar != andar || !EhEstante(m.modelo)) continue;
                    var k = planta.Em(m.lugar.x, m.lugar.z);
                    if (k != null && k.tipo == "deposito") lista.Add(i);
                }
            }
            return lista;
        }

        /// <summary>
        /// Só no anfitrião, sem passar por um jogador (as compras do Dia 0): guarda o item na primeira das estantes em que
        /// couber (as pilhas completam antes as do mesmo item que já estão nelas). <paramref name="qtd"/>: as balas, o pente
        /// ou quantos há na pilha (-1, o de sempre). Devolve false se ele (ou o resto da pilha) não coube.
        /// </summary>
        public bool GuardarNasEstantes(IList<int> estantes, Item item, int qtd, int desgaste, ulong cores)
        {
            if (!IsServer || item == Item.Nada || !Itens.NoJogo(item) || Itens.EhEquipamento(item)) return false;
            if (qtd < 0) qtd = Itens.QuantidadePadrao(item);
            if (Itens.EmPilha(item))
            {
                int max = Itens.Pilha(item), resta = Mathf.Max(1, Itens.Quantos(item, qtd));
                for (int i = 0; i < _itens.Count && resta > 0; i++)
                {
                    var o = _itens[i];
                    int tem = Itens.Quantos(o.item, o.qtd);
                    if (o.item != item || tem >= max || !estantes.Contains(o.estante)) continue;
                    int n = Mathf.Min(resta, max - tem);
                    o.qtd = (ushort)(tem + n);
                    _itens[i] = o;
                    resta -= n;
                }
                while (resta > 0)
                {
                    int n = Mathf.Min(resta, max);
                    if (!PorNaPrimeira(estantes, item, n, desgaste, cores)) return false;
                    resta -= n;
                }
                return true;
            }
            return PorNaPrimeira(estantes, item, qtd, desgaste, cores);
        }

        bool PorNaPrimeira(IList<int> estantes, Item item, int qtd, int desgaste, ulong cores)
        {
            foreach (int estante in estantes)
            {
                if (!Espaco(estante, item, out int x, out int y, out bool girado)) continue;
                _itens.Add(new ItemNaEstante { estante = (ushort)estante, id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado,
                                               qtd = (ushort)Mathf.Clamp(qtd, 0, ushort.MaxValue), desgaste = (byte)Mathf.Clamp(desgaste, 0, 100), cores = cores });
                return true;
            }
            return false;
        }

        /// <summary>O ponto está perto da estante (a do bunker, no chão do andar dela).</summary>
        public static bool Perto(int estante, Vector3 ponto, float distancia)
        {
            if (EhDoCaiaque(estante))
            {
                // o compartimento: a escotilha de trás do caiaque (quem está sentado nele também alcança)
                var k = Vehicles.Caiaque.DoCompartimento(estante);
                if (k == null) return false;
                var dc = ponto - k.PontoDoCompartimento;
                if (dc.y < -2.5f || dc.y > 2.5f) return false;
                dc.y = 0f;
                return dc.magnitude <= distancia;
            }
            var bunker = BunkerDoMundo.Instancia;
            if (bunker == null || !bunker.PontoDoMovel(estante, out var pe)) return false;
            var d = ponto - pe;
            if (d.y < -1f || d.y > 2.5f) return false;
            d.y = 0f;
            return d.magnitude <= distancia;
        }

        // ------------------------------------------------------------------ os pedidos (do jogador local)

        /// <summary>
        /// Guarda o item da mochila na estante, nesta célula (x negativo: no primeiro lugar livre, completando antes as
        /// pilhas do mesmo item que já estão nela). A arma leva o pente.
        /// </summary>
        public void Guardar(int estante, InventarioJogador inventario, ArmaJogador arma, ushort id, int x, int y, bool girado)
        {
            var item = inventario.Do(id);
            if (!IsSpawned || item == Item.Nada || (arma != null && arma.Usando && id == inventario.IdNaMao)) return;
            if (Itens.EhArma(item) && arma != null) inventario.GuardarPente(id, arma.MunicaoDe(id, item));
            GuardarRpc((ushort)estante, id, Byte(x), Byte(y), girado);
        }

        /// <summary>Pega o item da estante para a mochila, nesta célula (x negativo: no primeiro lugar livre).</summary>
        public void Pegar(int estante, ushort id, int x, int y, bool girado)
        {
            if (IsSpawned) PegarRpc((ushort)estante, id, Byte(x), Byte(y), girado);
        }

        /// <summary>Move o item para outra célula da mesma estante.</summary>
        public void Mover(int estante, ushort id, int x, int y, bool girado)
        {
            if (IsSpawned && x >= 0 && y >= 0) MoverRpc((ushort)estante, id, (byte)x, (byte)y, girado);
        }

        const byte Livre = 255;
        static byte Byte(int v) => v < 0 || v > 254 ? Livre : (byte)v;

        // ------------------------------------------------------------------ no anfitrião

        bool Quem(RpcParams parametros, int estante, out InventarioJogador inventario)
        {
            inventario = null;
            if (FormaDa(estante) == null) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return false;
            if (!Perto(estante, cliente.PlayerObject.transform.position, distanciaMaxima)) return false;
            inventario = cliente.PlayerObject.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        ushort NovoId()
        {
            ushort id = _proximoId;
            _proximoId = (ushort)(_proximoId == ushort.MaxValue ? 1 : _proximoId + 1);
            return id;
        }

        [Rpc(SendTo.Server)]
        void GuardarRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            if (!Quem(parametros, estante, out var inventario) || !inventario.Achar(id, out var it) || !Itens.NoJogo(it.item)) return;
            int qtd = it.qtd;
            if (x == Livre)
            {
                // as pilhas completam primeiro as do mesmo item que já estão na estante
                if (Itens.EmPilha(it.item))
                {
                    int max = Itens.Pilha(it.item), resta = Itens.Quantos(it.item, it.qtd);
                    for (int i = 0; i < _itens.Count && resta > 0; i++)
                    {
                        var o = _itens[i];
                        int tem = Itens.Quantos(o.item, o.qtd);
                        if (o.estante != estante || o.item != it.item || tem >= max) continue;
                        int n = Mathf.Min(resta, max - tem);
                        o.qtd = (ushort)(tem + n);
                        _itens[i] = o;
                        resta -= n;
                    }
                    if (resta <= 0)
                    {
                        inventario.Tirar(id);
                        return;
                    }
                    qtd = resta;
                }
                if (!Espaco(estante, it.item, out int lx, out int ly, out bool lg))
                {
                    // não coube inteira: o que entrou nas pilhas sai da mochila, e o resto fica nela
                    if (qtd != it.qtd) inventario.AcertarQuantidade(id, qtd);
                    return;
                }
                x = (byte)lx;
                y = (byte)ly;
                girado = lg;
            }
            else if (!Cabe(estante, it.item, girado, x, y)) return;
            inventario.Tirar(id);
            _itens.Add(new ItemNaEstante { estante = estante, id = NovoId(), item = it.item, x = x, y = y, girado = girado, qtd = (ushort)qtd,
                                           desgaste = it.desgaste, cores = it.cores });
        }

        [Rpc(SendTo.Server)]
        void PegarRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(estante, id);
            if (k < 0 || !Quem(parametros, estante, out var inventario)) return;
            var it = _itens[k];
            if (x == Livre)
            {
                if (Itens.EmPilha(it.item))
                {
                    int sobra = inventario.ReceberEmPilha(it.item, Itens.Quantos(it.item, it.qtd));
                    if (sobra <= 0) _itens.RemoveAt(k);
                    else if (sobra != Itens.Quantos(it.item, it.qtd))
                    {
                        it.qtd = (ushort)sobra;
                        _itens[k] = it;
                    }
                    return;
                }
                if (inventario.Receber(it.item, it.qtd, it.desgaste, it.cores)) _itens.RemoveAt(k);
                return;
            }
            if (inventario.PorNaGrade(it.item, x, y, girado, it.qtd, it.desgaste, it.cores)) _itens.RemoveAt(k);
        }

        [Rpc(SendTo.Server)]
        void MoverRpc(ushort estante, ushort id, byte x, byte y, bool girado, RpcParams parametros = default)
        {
            int k = Indice(estante, id);
            if (k < 0 || !Quem(parametros, estante, out _)) return;
            var it = _itens[k];
            if (!Cabe(estante, it.item, girado, x, y, id)) return;
            it.x = x;
            it.y = y;
            it.girado = girado;
            _itens[k] = it;
        }

        // ------------------------------------------------------------------ as estantes da cena

        /// <summary>
        /// Faz do móvel da estante (o prefab da mobília nova, já posto no andar, na escala dele) uma estante que guarda: o
        /// gatilho da mira em volta (E abre a tela) e os itens guardados nas tábuas (<see cref="ItensNaEstante"/>).
        /// </summary>
        public static void Ligar(GameObject movel, int estante, string modelo)
        {
            var forma = FormaDe(modelo);
            if (forma == null) return;
            var filtro = movel.GetComponent<MeshFilter>();
            var b = filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.bounds : new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(forma.largura, 1.9f, 0.4f));
            var mira = new GameObject("Mira") { layer = 2 };
            mira.transform.SetParent(movel.transform, false);
            var gatilho = mira.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = b.center;
            gatilho.size = b.size + new Vector3(0.04f, 0.04f, 0.06f);
            var naMira = mira.AddComponent<EstanteNaMira>();
            naMira.estante = estante;
            var itens = movel.AddComponent<ItensNaEstante>();
            itens.estante = estante;
            itens.forma = forma;
        }
    }

    /// <summary>
    /// Os itens guardados numa estante, na tábua deles (em todas as máquinas): a malha do chão de cada um, no meio das
    /// células dele (as colunas da esquerda para a direita de quem olha de frente, a linha de trás e a da frente), deitado
    /// ou em pé como no chão, virado ao longo do lado mais comprido e, se for maior que o lugar, um pouco menor.
    /// </summary>
    public class ItensNaEstante : MonoBehaviour
    {
        public int estante;
        public EstantesDoBunker.Forma forma;

        EstantesDoBunker _ligada;
        Transform _guardados;
        bool _refazer = true;

        void OnEnable() => _refazer = true;

        void OnDisable() => Desligar();

        void OnDestroy() => Desligar();

        void Desligar()
        {
            if (_ligada != null) _ligada.Mudou -= AoMudar;
            _ligada = null;
        }

        void AoMudar(int qual)
        {
            if (qual < 0 || qual == estante) _refazer = true;
        }

        void Update()
        {
            var e = EstantesDoBunker.Instancia;
            if (e != _ligada)
            {
                Desligar();
                _ligada = e;
                if (e != null) e.Mudou += AoMudar;
                _refazer = true;
            }
            if (!_refazer || forma == null) return;
            _refazer = false;
            Refazer();
        }

        void Refazer()
        {
            if (_guardados != null) Destroy(_guardados.gameObject);
            _guardados = null;
            var e = _ligada;
            var chao = ItensNoMundo.Instancia;
            if (e == null || !e.IsSpawned || chao == null) return;
            float s = transform.localScale.x;
            foreach (var it in e.Guardados)
            {
                if (it.estante != estante) continue;
                if (_guardados == null)
                {
                    // os itens no tamanho deles: o pai deles desfaz a escala dos móveis
                    _guardados = new GameObject("Itens guardados").transform;
                    _guardados.SetParent(transform, false);
                    _guardados.localScale = Vector3.one / Mathf.Max(0.01f, s);
                }
                Por(it, chao, s);
            }
        }

        static readonly Vector3[] _cantos = new Vector3[8];

        void Por(ItemNaEstante it, ItensNoMundo chao, float s)
        {
            var malha = chao.MalhaDe(it.item);
            if (malha == null) return;
            var f = forma;
            var t = it.Tamanho;
            int tabua = f.Tabuas - 1 - it.y / EstantesDoBunker.LinhasPorTabua, linha = it.y % EstantesDoBunker.LinhasPorTabua;
            float cw = f.largura / f.colunas, rd = f.fundo / EstantesDoBunker.LinhasPorTabua;
            // a coluna 0 fica à esquerda de quem olha de frente (a frente é o +Z do móvel, e a esquerda dele, o +X); a linha
            // 0 é a de trás
            float cx = (f.largura / 2f - (it.x + t.x / 2f) * cw) * s, cz = (-f.fundo / 2f + (linha + t.y / 2f) * rd) * s;
            float lx = t.x * cw * s, lz = t.y * rd * s;
            float topo = tabua + 1 < f.Tabuas ? (f.vao - 0.03f) * s : 0.7f;
            // deitada de lado (as armas) ou em pé (o resto), e virada ao longo do lado mais comprido do lugar
            var deitada = Itens.EhArma(it.item) || Itens.EhBranca(it.item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            var b = malha.bounds;
            var tam = Medir(deitada, b, out _, out _);
            bool largo = lx >= lz;
            var giro = (largo && tam.z > tam.x) || (!largo && tam.x > tam.z) ? Quaternion.Euler(0f, 90f, 0f) * deitada : deitada;
            tam = Medir(giro, b, out float baixo, out var meio);
            float k = Mathf.Min(1f, lx * 1.1f / Mathf.Max(0.01f, tam.x), lz * 1.15f / Mathf.Max(0.01f, tam.z), topo / Mathf.Max(0.01f, tam.y));
            k = Mathf.Max(0.35f, k);
            var go = new GameObject(Itens.Nome(it.item));
            go.transform.SetParent(_guardados, false);
            go.transform.localRotation = giro;
            go.transform.localScale = Vector3.one * k;
            go.transform.localPosition = new Vector3(cx - meio.x * k, f.alturas[tabua] * s - baixo * k + 0.002f, cz - meio.z * k);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = chao.MaterialDe(it.item);
            r.shadowCastingMode = UI.Graficos.SombraDosMoveis ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var roupas = Itens.EhRoupa(it.item) ? Roupas.ItensDasRoupas.Carregar() : null;
            if (roupas != null) roupas.Pintar(r, it.item, it.cores);
        }

        // o tamanho da malha girada (em x, y e z), o ponto mais baixo e o meio dela
        static Vector3 Medir(Quaternion giro, Bounds b, out float baixo, out Vector3 meio)
        {
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                var p = giro * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            baixo = min.y;
            meio = (min + max) / 2f;
            return max - min;
        }
    }

    /// <summary>O gatilho em volta da estante: a mira acha ele, e E abre a tela da estante (a do inventário, com ela ao lado).</summary>
    public class EstanteNaMira : Interagivel
    {
        public int estante;

        public override string Dica(ArmaJogador quem)
        {
            var e = EstantesDoBunker.Instancia;
            var f = EstantesDoBunker.FormaDa(estante);
            if (e == null || !e.IsSpawned || f == null || quem == null) return null;
            int n = e.Quantos(estante);
            return $"[E] Abrir a {f.nome}" + (n == 0 ? " (vazia)" : n == 1 ? " (1 item)" : $" ({n} itens)");
        }

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem != null ? quem.GetComponent<TelaDoInventario>() : null;
            if (tela != null) tela.AbrirEstante(estante);
        }
    }
}
