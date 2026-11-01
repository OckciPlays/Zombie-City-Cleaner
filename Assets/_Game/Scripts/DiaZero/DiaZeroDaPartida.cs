using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>O dinheiro de um jogador no Dia 0 (o anfitrião escreve).</summary>
    public struct CarteiraDoDia : INetworkSerializeByMemcpy, IEquatable<CarteiraDoDia>
    {
        public ulong cliente;
        public int dinheiro, gasto;
        public bool Equals(CarteiraDoDia o) => cliente == o.cliente && dinheiro == o.dinheiro && gasto == o.gasto;
    }

    /// <summary>Uma compra do Dia 0: quem comprou, o item, a quantidade (0: a de sempre), o desgaste e o lugar dela na casa.</summary>
    public struct CompraDoDia : INetworkSerializeByMemcpy, IEquatable<CompraDoDia>
    {
        public ushort id;
        public ulong cliente;
        public Item item;
        public ushort qtd;
        public byte desgaste;
        public ushort lugar;
        public bool Equals(CompraDoDia o) => id == o.id && cliente == o.cliente && item == o.item && qtd == o.qtd && desgaste == o.desgaste && lugar == o.lugar;
    }

    /// <summary>O pedido do caixa: a loja, os produtos (o índice na tabela dela) e as roupas (o lugar e a peça, em pares).</summary>
    public struct PedidoDeCompra : INetworkSerializable
    {
        public byte loja;
        public byte[] produtos;
        public byte[] roupas;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref loja);
            Lista(s, ref produtos);
            Lista(s, ref roupas);
        }

        static void Lista<T>(BufferSerializer<T> s, ref byte[] lista) where T : IReaderWriter
        {
            int n = lista?.Length ?? 0;
            s.SerializeValue(ref n);
            if (s.IsReader) lista = new byte[Mathf.Clamp(n, 0, 512)];
            for (int i = 0; i < lista.Length; i++) s.SerializeValue(ref lista[i]);
        }
    }

    /// <summary>
    /// O Dia 0 da partida (01/11/2026, a pedido: o começo do jogo no mundo normal), no objeto "Mundo" da cena do mundo (com o
    /// <see cref="MundoDaPartida"/>, que segura a geração do mundo enquanto ele dura: <see cref="MundoDaPartida.NoDiaZero"/>):
    ///  - cada máquina carrega a cena <c>DiaZero</c> por cima da do mundo (a rua, <see cref="RuaDoDiaZero"/>); com ela
    ///    pronta, o anfitrião cria os jogadores na sala da nossa casa (os clientes pedem, <see cref="ProntoNaRuaRpc"/>), sem
    ///    a pistola, com 1.000 dólares cada (<see cref="PrecosDoDiaZero.Economias"/>);
    ///  - o caixa manda o carrinho ao anfitrião (<see cref="PagarRpc"/>), que confere o dinheiro pela tabela e guarda as
    ///    compras (aparecem no chão da casa, para todos);
    ///  - a porta do abrigo (<see cref="FecharAPortaRpc"/>, ou o F1): quem está fora de casa perde as compras; o mundo
    ///    começa a ser gerado, e a tela dos "5 anos depois" cobre a geração. Com o mundo pronto, cada um vai para a sala
    ///    da casa da família (a mesma casa, abandonada), e o anfitrião põe as compras no mesmo lugar delas, no chão
    ///    (<see cref="ItensNoMundo"/>). O dinheiro que sobrou fica guardado (para os acampamentos, depois).
    /// </summary>
    public partial class DiaZeroDaPartida : NetworkBehaviour
    {
        public const string Cena = "DiaZero";
        /// <summary>A hora em que o jogo começa no mundo, 5 anos depois (a saída do abrigo, de manhã).</summary>
        public const float HoraDaSaida = 8f;

        public static DiaZeroDaPartida Instancia { get; private set; }

        /// <summary>Nesta máquina, o Dia 0 está acontecendo e a rua está pronta.</summary>
        public static bool NoDia => Instancia != null && Instancia.IsSpawned && Instancia._mundo != null && Instancia._mundo.NoDiaZero &&
                                    Instancia._ruaPronta && RuaDoDiaZero.Instancia != null && RuaDoDiaZero.Instancia.Pronta;

        /// <summary>A tela dos 5 anos depois está na frente (a porta fechou e o mundo ainda não apareceu).</summary>
        public static bool NaPassagem => Instancia != null && Instancia._passagem;

        NetworkList<CarteiraDoDia> _carteiras;
        NetworkList<CompraDoDia> _compras;
        ushort _proximoId = 1, _proximoLugar;
        MundoDaPartida _mundo;

        // a cena (em cada máquina)
        AsyncOperation _carregando;
        bool _ruaPronta, _pediuOJogador, _estavaNoDia;
        readonly List<ulong> _esperandoARua = new List<ulong>();
        // a passagem dos 5 anos
        bool _passagem;
        float _passagemDesde, _prontoDesde = -1f;
        string _quemFicouFora = "";
        // no anfitrião: o que vai para a casa do mundo, e onde (nas coordenadas da casa)
        readonly List<CompraDoDia> _levar = new List<CompraDoDia>();
        readonly List<Vector3> _lugares = new List<Vector3>();
        // as compras à mostra na casa do Dia 0
        readonly Dictionary<ushort, GameObject> _vistas = new Dictionary<ushort, GameObject>();
        readonly HashSet<ushort> _vistos = new HashSet<ushort>();
        readonly List<ushort> _sobrando = new List<ushort>();

        /// <summary>A mensagem curta do Dia 0 (o pagamento, a porta) e até quando.</summary>
        string _mensagem;
        float _mensagemAte;

        void Awake()
        {
            _carteiras = new NetworkList<CarteiraDoDia>();
            _compras = new NetworkList<CompraDoDia>();
            _mundo = GetComponent<MundoDaPartida>();
            Instancia = this;
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _carteiras.Clear();
                _compras.Clear();
                _proximoId = 1;
                _proximoLugar = 0;
                _levar.Clear();
                ComecarOLobby();
                NetworkManager.OnClientDisconnectCallback += AoSair;
            }
            _compras.OnListChanged += AoMudarAsCompras;
            if (_mundo != null) _mundo.FicouPronto += AoFicarProntoOMundo;
            _estavaNoDia = false;
            _ruaPronta = _pediuOJogador = _passagem = false;
            IntroDoDiaZero.JaViu = false;
            _prontoDesde = -1f;
            _temUltimoDentro = false;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= AoSair;
            _compras.OnListChanged -= AoMudarAsCompras;
            if (_mundo != null) _mundo.FicouPronto -= AoFicarProntoOMundo;
            Carrinho.Esvaziar(false);
            TelaDoCaixa.Fechar();
            MenuOnline.Cinema = false;
            AmbienteDoDia.DiaLimpo = 0f;
            _passagem = false;
            _esperandoARua.Clear();
            Descarregar();
        }

        // ------------------------------------------------------------------ a cena e os jogadores

        void Update()
        {
            if (!IsSpawned || _mundo == null) return;
            // o dia limpo (o sol e as cores do mundo normal) no Dia 0 e na tela dos 5 anos, até o mundo aparecer
            AmbienteDoDia.DiaLimpo = _mundo.NoDiaZero || (_passagem && _prontoDesde < 0f) ? 1f : 0f;
            if (_mundo.NoDiaZero)
            {
                _estavaNoDia = true;
                CarregarARua();
                if (_ruaPronta) NaRua();
            }
            else if (_estavaNoDia && !_passagem && _prontoDesde < 0f) ComecarAPassagem();
            if (_passagem) Passagem();
        }

        bool _semACena;

        void CarregarARua()
        {
            if (_ruaPronta || _semACena) return;
            if (_carregando == null && !SceneManager.GetSceneByName(Cena).isLoaded)
            {
                _carregando = SceneManager.LoadSceneAsync(Cena, LoadSceneMode.Additive);
                if (_carregando == null)
                {
                    _semACena = true;
                    Debug.LogError($"[Dia 0] A cena {Cena} não está nas Build Settings (Zombie City Cleaner > Dia 0 > Montar a cena): o jogo vai direto para o mundo.");
                    _estavaNoDia = false;
                    if (IsServer) _mundo.AcabarODiaZero();
                }
                return;
            }
            if (_carregando != null && !_carregando.isDone) return;
            _carregando = null;
            var rua = RuaDoDiaZero.Instancia;
            if (rua == null || !rua.Pronta) return;
            _ruaPronta = true;
            PontosDeNascimento.Definir(rua.Nascimentos, rua.GirosDosNascimentos);
            _lugares.Clear();
            _lugares.AddRange(rua.LugaresDasCompras);
            Debug.Log($"[Dia 0] A rua está pronta: {rua.Lojas.Count} lojas, {rua.Npcs.Count} NPCs, {rua.LugaresDasCompras.Count} lugares para as compras.");
            AoMudarAsCompras(default);
            if (IsServer)
            {
                // o dia, parado (sem pressa: "quero desfrutar do momento")
                var ciclo = CicloDoDia.Instancia;
                if (ciclo != null)
                {
                    ciclo.SempreDeDia(true);
                    CicloDoDia.MudarHora(10.5f);
                    ciclo.HoraRede.Value = 10.5f;
                }
                CriarNaRua(NetworkManager.LocalClientId);
                foreach (var c in _esperandoARua) CriarNaRua(c);
                _esperandoARua.Clear();
            }
            else if (!_pediuOJogador)
            {
                _pediuOJogador = true;
                ProntoNaRuaRpc();
            }
        }

        /// <summary>Cliente → anfitrião: a rua está pronta aqui (pode criar o meu jogador).</summary>
        [Rpc(SendTo.Server)]
        void ProntoNaRuaRpc(RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (!_mundo.NoDiaZero) return;
            if (!_ruaPronta) { if (!_esperandoARua.Contains(cliente)) _esperandoARua.Add(cliente); return; }
            CriarNaRua(cliente);
        }

        void CriarNaRua(ulong cliente)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(cliente, out var c)) return;
            if (Carteira(cliente) < 0) _carteiras.Add(new CarteiraDoDia { cliente = cliente, dinheiro = PrecosDoDiaZero.Economias });
            if (c.PlayerObject != null) return;
            var ponto = PontosDeNascimento.Ponto(cliente);
            var jogador = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab, ponto.position, ponto.rotation);
            jogador.GetComponent<NetworkObject>().SpawnAsPlayerObject(cliente, true);
            Debug.Log($"[Dia 0] O jogador {cliente} nasceu na sala da nossa casa.");
        }

        void AoSair(ulong cliente)
        {
            _esperandoARua.Remove(cliente);
            _viram.Remove(cliente);
            ConferirATv();
        }

        void Descarregar()
        {
            _vistas.Clear();
            _ruaPronta = false;
            _carregando = null;
            var cena = SceneManager.GetSceneByName(Cena);
            if (cena.isLoaded) SceneManager.UnloadSceneAsync(cena);
        }

        // no Dia 0, a cada quadro: quem sai da loja com o carrinho cheio devolve tudo
        void NaRua()
        {
            if (IsServer && Fase == FaseDoDia.Tv) ConferirATv();
            var j = JogadorRede.Local;
            if (j == null) return;
            NoLobby(j);
            if (Carrinho.Loja != null && !Carrinho.Loja.Dentro(j.transform.position, 0.8f))
            {
                bool tinha = Carrinho.Quantos > 0;
                string loja = Carrinho.Loja.Nome;
                Carrinho.Esvaziar(true);
                TelaDoCaixa.Fechar();
                if (tinha) Avisar($"Você saiu da {loja.ToLowerInvariant()} sem pagar: o carrinho ficou lá (e as roupas provadas também).");
            }
        }

        public void Avisar(string texto, float segundos = 5f)
        {
            _mensagem = texto;
            _mensagemAte = Time.unscaledTime + segundos;
        }

        // ------------------------------------------------------------------ o dinheiro e as compras

        int Carteira(ulong cliente)
        {
            for (int i = 0; i < _carteiras.Count; i++) if (_carteiras[i].cliente == cliente) return i;
            return -1;
        }

        /// <summary>O dinheiro do jogador (nesta máquina, pela rede).</summary>
        public int Dinheiro(ulong cliente)
        {
            int k = Carteira(cliente);
            return k < 0 ? PrecosDoDiaZero.Economias : _carteiras[k].dinheiro;
        }

        /// <summary>O que o jogador já gastou.</summary>
        public int Gasto(ulong cliente)
        {
            int k = Carteira(cliente);
            return k < 0 ? 0 : _carteiras[k].gasto;
        }

        /// <summary>Quantas compras o jogador tem na casa.</summary>
        public int Compras(ulong cliente)
        {
            int n = 0;
            foreach (var c in _compras) if (c.cliente == cliente) n += Mathf.Max(1, Itens.EmPilha(c.item) && !Itens.EhMunicao(c.item) ? c.qtd : 1);
            return n;
        }

        bool _pagando;
        /// <summary>Esperando a resposta do anfitrião (a tela do caixa mostra).</summary>
        public bool Pagando => _pagando;

        /// <summary>O jogador local paga o carrinho (a tela do caixa).</summary>
        public void Pagar()
        {
            if (_pagando || Carrinho.Loja == null || Carrinho.Quantos == 0) return;
            var roupas = Carrinho.Roupas();
            var pedido = new PedidoDeCompra
            {
                loja = (byte)Carrinho.Loja.Tipo,
                produtos = new byte[Carrinho.Produtos.Count],
                roupas = new byte[roupas.Count * 2],
            };
            for (int i = 0; i < Carrinho.Produtos.Count; i++) pedido.produtos[i] = (byte)Carrinho.Produtos[i];
            for (int i = 0; i < roupas.Count; i++)
            {
                pedido.roupas[i * 2] = (byte)roupas[i].lugar;
                pedido.roupas[i * 2 + 1] = roupas[i].valor;
            }
            _pagando = true;
            PagarRpc(pedido);
        }

        /// <summary>Quem joga → anfitrião: pagar o carrinho (o anfitrião confere pela tabela).</summary>
        [Rpc(SendTo.Server)]
        void PagarRpc(PedidoDeCompra pedido, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            var alvo = RpcTarget.Single(cliente, RpcTargetUse.Temp);
            if (!_mundo.NoDiaZero || pedido.produtos == null || pedido.roupas == null)
            {
                ResultadoRpc(false, 0, 0, alvo);
                return;
            }
            var loja = (LojaDoDia)pedido.loja;
            int total = 0;
            foreach (byte i in pedido.produtos) total += PrecosDoDiaZero.DaLoja(loja, i)?.preco ?? 0;
            for (int i = 0; i + 1 < pedido.roupas.Length; i += 2) total += PrecosDoDiaZero.PrecoDaRoupa(pedido.roupas[i], pedido.roupas[i + 1]);
            int k = Carteira(cliente);
            if (k < 0)
            {
                _carteiras.Add(new CarteiraDoDia { cliente = cliente, dinheiro = PrecosDoDiaZero.Economias });
                k = _carteiras.Count - 1;
            }
            var carteira = _carteiras[k];
            if (total > carteira.dinheiro)
            {
                ResultadoRpc(false, total, carteira.dinheiro, alvo);
                return;
            }
            carteira.dinheiro -= total;
            carteira.gasto += total;
            _carteiras[k] = carteira;
            foreach (byte i in pedido.produtos)
            {
                var p = PrecosDoDiaZero.DaLoja(loja, i);
                if (p.HasValue) Guardar(cliente, p.Value);
            }
            Debug.Log($"[Dia 0] O jogador {cliente} pagou {PrecosDoDiaZero.Dinheiro(total)} na {PrecosDoDiaZero.Nome(loja)} ({pedido.produtos.Length} produtos, {pedido.roupas.Length / 2} roupas); sobraram {PrecosDoDiaZero.Dinheiro(carteira.dinheiro)}.");
            ResultadoRpc(true, total, carteira.dinheiro, alvo);
        }

        // a compra vai para a casa: a pilha (a comida, os remédios) completa a anterior do mesmo item; o resto, um lugar cada
        void Guardar(ulong cliente, Produto p)
        {
            bool pilha = Itens.EmPilha(p.item) && !Itens.EhMunicao(p.item);
            if (pilha)
                for (int i = 0; i < _compras.Count; i++)
                {
                    var c = _compras[i];
                    if (c.cliente != cliente || c.item != p.item || c.desgaste != p.desgaste || c.qtd >= Itens.Pilha(p.item)) continue;
                    c.qtd++;
                    _compras[i] = c;
                    return;
                }
            _compras.Add(new CompraDoDia
            {
                id = _proximoId++, cliente = cliente, item = p.item, qtd = (ushort)(pilha ? 1 : 0), desgaste = p.desgaste, lugar = _proximoLugar++,
            });
        }

        /// <summary>Anfitrião → quem pagou: deu certo (ou faltou dinheiro).</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void ResultadoRpc(bool pago, int total, int sobrou, RpcParams parametros = default)
        {
            _pagando = false;
            if (pago)
            {
                Carrinho.Pago();
                TelaDoCaixa.Fechar();
                Avisar($"Pago: {PrecosDoDiaZero.Dinheiro(total)}. As compras foram para a sua casa (sobraram {PrecosDoDiaZero.Dinheiro(sobrou)}).");
            }
            else Avisar(total > 0 ? $"Não deu: o total é {PrecosDoDiaZero.Dinheiro(total)} e você tem {PrecosDoDiaZero.Dinheiro(sobrou)}. Tire alguma coisa do carrinho."
                                  : "Não deu para pagar agora.");
        }

        // as compras à mostra no chão da casa do Dia 0 (em todas as máquinas; só o desenho)
        void AoMudarAsCompras(NetworkListEvent<CompraDoDia> e)
        {
            var rua = RuaDoDiaZero.Instancia;
            var mundo = ItensNoMundo.Instancia;
            if (!_ruaPronta || rua == null || rua.NossaCasa == null || mundo == null || rua.LugaresDasCompras.Count == 0) return;
            _vistos.Clear();
            foreach (var c in _compras)
            {
                _vistos.Add(c.id);
                if (_vistas.TryGetValue(c.id, out var v) && v != null) continue;
                var local = rua.LugaresDasCompras[c.lugar % rua.LugaresDasCompras.Count];
                _vistas[c.id] = ModeloNoChao(rua.NossaCasa, c.item, local, (c.id * 47) % 360);
            }
            _sobrando.Clear();
            foreach (var id in _vistas.Keys) if (!_vistos.Contains(id)) _sobrando.Add(id);
            foreach (var id in _sobrando)
            {
                if (_vistas[id] != null) Destroy(_vistas[id]);
                _vistas.Remove(id);
            }
        }

        // o desenho de um item no chão (como o ItensNoMundo: as armas deitadas de lado, o apoio embaixo do meio)
        static GameObject ModeloNoChao(Transform pai, Item item, Vector3 apoio, float giro)
        {
            var mundo = ItensNoMundo.Instancia;
            var malha = mundo.MalhaDe(item);
            var go = new GameObject($"Compra ({Itens.Nome(item)})");
            go.transform.SetParent(pai, false);
            if (malha == null) return go;
            var b = malha.bounds;
            var rot = Quaternion.Euler(0f, giro, 0f) * (Itens.EhArma(item) || Itens.EhBranca(item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            float baixo = float.MaxValue;
            var meio = Vector3.zero;
            for (int c = 0; c < 8; c++)
            {
                var p = rot * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                baixo = Mathf.Min(baixo, p.y);
                meio += p / 8f;
            }
            go.transform.localPosition = apoio + new Vector3(-meio.x, -baixo + 0.002f, -meio.z);
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = mundo.MaterialDe(item);
            return go;
        }

        // ------------------------------------------------------------------ a porta do abrigo e os 5 anos

        /// <summary>Os nomes de quem está fora de casa agora (nesta máquina: a dica da porta).</summary>
        public static string QuemEstaFora()
        {
            var rua = RuaDoDiaZero.Instancia;
            if (rua == null) return "";
            var nomes = new List<string>();
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Criado && !rua.DentroDaCasa(j.transform.position)) nomes.Add(j.Aparencia.Value.nome.ToString());
            return string.Join(", ", nomes);
        }

        /// <summary>O jogador local fecha a porta do abrigo (de dentro de casa).</summary>
        public void PedirFecharAPorta()
        {
            if (IsSpawned && _mundo.NoDiaZero) FecharAPortaRpc(false);
        }

        /// <summary>O F1: pular o Dia 0 (todos levam as compras, onde estiverem).</summary>
        public void PedirPular()
        {
            if (IsSpawned && _mundo.NoDiaZero) FecharAPortaRpc(true);
        }

        /// <summary>O F1: mais 1.000 dólares para quem pediu.</summary>
        public void PedirMaisDinheiro()
        {
            if (IsSpawned && _mundo.NoDiaZero) MaisDinheiroRpc();
        }

        [Rpc(SendTo.Server)]
        void MaisDinheiroRpc(RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            int k = Carteira(cliente);
            if (k < 0) { _carteiras.Add(new CarteiraDoDia { cliente = cliente, dinheiro = PrecosDoDiaZero.Economias * 2 }); return; }
            var c = _carteiras[k];
            c.dinheiro += PrecosDoDiaZero.Economias;
            _carteiras[k] = c;
        }

        [Rpc(SendTo.Server)]
        void FecharAPortaRpc(bool pular, RpcParams parametros = default)
        {
            if (!_mundo.NoDiaZero) return;
            var rua = RuaDoDiaZero.Instancia;
            ulong quem = parametros.Receive.SenderClientId;
            if (!pular && rua != null && NetworkManager.ConnectedClients.TryGetValue(quem, out var c) && c.PlayerObject != null &&
                !rua.DentroDaCasa(c.PlayerObject.transform.position)) return;
            var fora = new List<string>();
            var deFora = new HashSet<ulong>();
            if (!pular && rua != null)
                foreach (var kv in NetworkManager.ConnectedClients)
                {
                    var po = kv.Value.PlayerObject;
                    if (po == null || rua.DentroDaCasa(po.transform.position)) continue;
                    deFora.Add(kv.Key);
                    var jr = po.GetComponent<JogadorRede>();
                    fora.Add(jr != null ? jr.Aparencia.Value.nome.ToString() : $"Jogador {kv.Key + 1}");
                }
            _levar.Clear();
            foreach (var compra in _compras) if (!deFora.Contains(compra.cliente)) _levar.Add(compra);
            Debug.Log($"[Dia 0] A porta do abrigo fechou{(pular ? " (pulado no F1)" : "")}: {_levar.Count} compras vão para a casa" +
                      (fora.Count > 0 ? $"; ficaram de fora: {string.Join(", ", fora)}." : "."));
            PortaFechouRpc(string.Join(", ", fora));
            // 5 anos depois, a saída do abrigo é de manhã (o relógio volta a andar)
            var ciclo = CicloDoDia.Instancia;
            if (ciclo != null)
            {
                ciclo.SempreDeDia(false);
                CicloDoDia.MudarHora(HoraDaSaida);
                ciclo.HoraRede.Value = HoraDaSaida;
            }
            _mundo.AcabarODiaZero();
        }

        /// <summary>Anfitrião → todos: a porta fechou (com quem ficou de fora).</summary>
        [Rpc(SendTo.Everyone)]
        void PortaFechouRpc(string ficaramFora) => _quemFicouFora = ficaramFora ?? "";

        // a porta fechou (o Dia 0 acabou nesta máquina): a tela escurece, a rua sai e o jogador local fica parado no ar
        void ComecarAPassagem()
        {
            _passagem = true;
            _passagemDesde = Time.unscaledTime;
            _prontoDesde = -1f;
            MenuOnline.Cinema = true;
            TelaDoCaixa.Fechar();
            Carrinho.Esvaziar(true);
            var j = JogadorRede.Local;
            if (j != null) j.GetComponent<MovimentoPrimeiraPessoa>().Voando = true;
        }

        void Passagem()
        {
            float t = Time.unscaledTime - _passagemDesde;
            // a rua sai com a tela já preta
            if (t > 1.3f && (_ruaPronta || SceneManager.GetSceneByName(Cena).isLoaded)) Descarregar();
            if (_prontoDesde >= 0f && Time.unscaledTime - _prontoDesde > 4.5f)
            {
                _passagem = false;
                _estavaNoDia = false;
                MenuOnline.Cinema = false;
            }
        }

        // o mundo ficou pronto nesta máquina: cada um vai para a sala da casa da família; o anfitrião põe as compras
        void AoFicarProntoOMundo(DadosDoMundo dados)
        {
            if (!_estavaNoDia) return;
            _prontoDesde = Time.unscaledTime;
            var j = JogadorRede.Local;
            if (j != null)
            {
                var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
                var pose = PontosDeNascimento.Ponto(NetworkManager.LocalClientId);
                mov.Voando = false;
                mov.Teleportar(pose.position, pose.rotation.eulerAngles.y);
            }
            if (IsServer) Entregar(dados);
        }

        // as compras no chão da casa da família, no mesmo lugar delas na casa do Dia 0 (é a mesma casa)
        void Entregar(DadosDoMundo d)
        {
            var lote = d.Plano?.LoteDaFamilia;
            var tracado = d.Plano?.CidadeDaFamilia?.Tracado;
            var itens = ItensNoMundo.Instancia;
            if (lote == null || tracado == null || itens == null || _lugares.Count == 0)
            {
                if (_levar.Count > 0) Debug.LogWarning($"[Dia 0] Sem a casa da família no mundo: {_levar.Count} compras não foram entregues.");
                return;
            }
            var M = tracado.MatrizDaCasa(lote);
            foreach (var c in _levar)
            {
                var local = _lugares[c.lugar % _lugares.Count];
                var ponto = M.MultiplyPoint3x4(local) + Vector3.up * 0.01f;
                itens.Criar(c.item, ponto, (c.id * 47) % 360, c.qtd > 0 ? c.qtd : -1, c.desgaste);
            }
            Debug.Log($"[Dia 0] {_levar.Count} compras entregues na casa da família.");
            _levar.Clear();
        }
    }
}
