using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>Um carro do mundo que alguém já mexeu: o número dele (o índice em <c>Objetos.Todos</c>), as peças, as rodas e o macaco.</summary>
    public struct CarroMexido : INetworkSerializeByMemcpy, IEquatable<CarroMexido>
    {
        public int id;
        public PecasDoMotor pecas;
        public PneusDoCarro pneus;
        /// <summary><see cref="PecasDosCarrosDoMundo.SemMacaco"/>, ou a roda do macaco com o bit 7 erguido.</summary>
        public byte macaco;

        public bool Equals(CarroMexido o) => id == o.id && pecas.Equals(o.pecas) && pneus.Equals(o.pneus) && macaco == o.macaco;
    }

    /// <summary>
    /// As peças, as rodas e o macaco dos carros abandonados do mundo gerado (no NetworkObject "Mundo" da cena Mundo).
    /// Os carros nascem e somem com os blocos (<see cref="ObjetosDoMundo"/>), e são centenas: por isso nada vai pela rede
    /// até alguém mexer.
    ///  - O que sobrou em cada carro sai da semente do mundo e do número dele (<see cref="Padrao"/>): igual em todas as
    ///    máquinas, sem mandar nada.
    ///  - O carro mexido (uma peça tirada, uma roda trocada, o macaco apoiado) entra na lista da rede
    ///    (<see cref="_mexidos"/>), escrita pelo anfitrião, e passa a valer no lugar do sorteio.
    ///  - Quem olha o motor de cada carro também vai pela rede (<see cref="_abertos"/>): o capô fica aberto para todos.
    /// O anfitrião confere a distância pelos dados do mundo (<see cref="ObjetosDoMundo.PoseDoCarro"/>), não pelo objeto do
    /// carro: ele só cria os carros em volta dele mesmo, e o pedido pode vir de um cliente longe.
    /// </summary>
    public class PecasDosCarrosDoMundo : FonteDasPecas
    {
        public static PecasDosCarrosDoMundo Instancia { get; private set; }

        [Tooltip("A chance de cada peça (a vela, a bateria e o radiador) ainda estar no carro.")]
        public float[] chance = { 0.55f, 0.5f, 0.45f };
        [Tooltip("O estado sorteado de cada peça (%): x o mínimo, y o máximo. A bateria parada perde a carga.")]
        public Vector2Int[] faixas = { new(15, 100), new(5, 70), new(20, 100) };
        [Tooltip("A chance de cada roda ainda estar no carro e, se está, de o pneu estar furado.")]
        public float chanceDaRoda = 0.9f;
        public float chanceDeFurado = 0.3f;
        [Tooltip("O estado sorteado do pneu que não está furado (%).")]
        public Vector2Int faixaDoPneu = new(15, 85);
        [Tooltip("Até onde o anfitrião aceita o pedido no capô (m, dos pés do jogador ao meio do capô).")]
        public float distanciaMaxima = 3.5f;

        public const byte SemMacaco = 255, Erguido = 0x80;

        NetworkList<CarroMexido> _mexidos;
        NetworkList<int> _abertos;
        // o lugar de cada carro mexido na lista (refeito quando ela muda) e o sorteio dos que ninguém mexeu
        readonly Dictionary<int, int> _lugar = new Dictionary<int, int>();
        readonly Dictionary<int, CarroMexido> _padroes = new Dictionary<int, CarroMexido>();
        bool _lugarVelho = true;
        int _sementeDosPadroes = int.MinValue;
        // no anfitrião: quando o macaco de cada carro começou a subir ou a descer, e quem olha cada motor
        readonly Dictionary<int, float> _macacoMudouEm = new Dictionary<int, float>();
        readonly Dictionary<int, HashSet<ulong>> _olhando = new Dictionary<int, HashSet<ulong>>();
        readonly List<int> _vazios = new List<int>();
        float _conferirEm;

        void Awake()
        {
            Instancia = this;
            _mexidos = new NetworkList<CarroMexido>();
            _abertos = new NetworkList<int>();
            _mexidos.OnListChanged += _ => _lugarVelho = true;
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            _lugarVelho = true;
            _padroes.Clear();
            if (!IsServer) return;
            _mexidos.Clear();
            _abertos.Clear();
            _macacoMudouEm.Clear();
            _olhando.Clear();
        }

        // ------------------------------------------------------------------ o estado de cada carro

        /// <summary>O estado do carro: o da lista da rede, se alguém mexeu nele; senão, o sorteio da semente.</summary>
        CarroMexido Estado(int carro)
        {
            if (_lugarVelho)
            {
                _lugarVelho = false;
                _lugar.Clear();
                for (int i = 0; i < _mexidos.Count; i++) _lugar[_mexidos[i].id] = i;
            }
            return _lugar.TryGetValue(carro, out int i2) && i2 < _mexidos.Count ? _mexidos[i2] : Padrao(carro);
        }

        /// <summary>O que sobrou no carro que ninguém mexeu: sorteado pela semente do mundo e pelo número do carro.</summary>
        CarroMexido Padrao(int carro)
        {
            var dados = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            int semente = dados != null ? dados.Semente : 0;
            if (semente != _sementeDosPadroes)
            {
                _sementeDosPadroes = semente;
                _padroes.Clear();
            }
            if (_padroes.TryGetValue(carro, out var pronto)) return pronto;
            var s = new Sorteio(semente, carro);
            var e = new CarroMexido { id = carro, macaco = SemMacaco };
            for (int k = 0; k < PecasDoMotor.Quantas; k++)
                e.pecas[k] = s.Valor() < chance[k] ? (byte)s.Entre(faixas[k].x, faixas[k].y) : PecasDoMotor.Vazio;
            for (int k = 0; k < PneusDoCarro.Quantas; k++)
                e.pneus[k] = s.Valor() >= chanceDaRoda ? PneusDoCarro.Vazio
                    : s.Valor() < chanceDeFurado ? (byte)s.Entre(0, PneusDoCarro.Minimo - 1) : (byte)s.Entre(faixaDoPneu.x, faixaDoPneu.y);
            _padroes[carro] = e;
            return e;
        }

        // no anfitrião: grava o carro mexido na lista da rede
        void Gravar(CarroMexido e)
        {
            Estado(e.id);
            if (_lugar.TryGetValue(e.id, out int i) && i < _mexidos.Count) _mexidos[i] = e;
            else _mexidos.Add(e);
            _lugarVelho = true;
        }

        public override PneusDoCarro PneusDo(int carro) => Estado(carro).pneus;
        public override int MacacoDo(int carro)
        {
            byte m = Estado(carro).macaco;
            return m == SemMacaco ? -1 : m & 3;
        }
        public override bool ErguidoDo(int carro)
        {
            byte m = Estado(carro).macaco;
            return m != SemMacaco && (m & Erguido) != 0;
        }
        public override PecasDoMotor Do(int carro) => Estado(carro).pecas;
        public override bool EstaAberto(int carro) => IsSpawned && _abertos.Contains(carro);

        // ------------------------------------------------------------------ onde o carro está (pelos dados do mundo)

        bool Ponto(int carro, out Vector3 capo)
        {
            capo = default;
            var objetos = ObjetosDoMundo.Instancia;
            return objetos != null && objetos.PontoDoCapo(carro, out capo);
        }

        bool Roda(int carro, int roda, out Vector3 ponto, out Vector3 fora)
        {
            ponto = fora = default;
            var objetos = ObjetosDoMundo.Instancia;
            return objetos != null && objetos.PontoDaRoda(carro, roda, out ponto, out fora);
        }

        bool Jogador(ulong cliente, out NetworkObject jogador)
        {
            jogador = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) || c.PlayerObject == null) return false;
            jogador = c.PlayerObject;
            return true;
        }

        bool Perto(int carro, ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            if (!Ponto(carro, out var capo) || !Jogador(cliente, out var j)) return false;
            if ((j.transform.position - capo).sqrMagnitude > distanciaMaxima * distanciaMaxima) return false;
            inventario = j.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        bool PertoDaRoda(int carro, int roda, ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            if (roda < 0 || roda >= PneusDoCarro.Quantas || !Roda(carro, roda, out var ponto, out _) || !Jogador(cliente, out var j)) return false;
            var p = j.GetComponent<PassageiroDoCarro>();
            if (p != null && p.Sentado) return false;
            if ((j.transform.position - ponto).sqrMagnitude > 3f * 3f) return false;
            inventario = j.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        // ------------------------------------------------------------------ olhar o motor

        public override void Olhar(int carro, bool olhando)
        {
            if (IsSpawned && carro >= 0) OlharRpc(carro, olhando);
        }

        [Rpc(SendTo.Server)]
        void OlharRpc(int carro, bool olhando, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (!_olhando.TryGetValue(carro, out var quem)) _olhando[carro] = quem = new HashSet<ulong>();
            if (olhando && Perto(carro, cliente, out _))
            {
                quem.Add(cliente);
                Susto(carro, cliente);
            }
            else quem.Remove(cliente);
            AtualizarAbertos();
        }

        // no anfitrião: quem se afastou (com folga) ou saiu deixa de olhar
        void Update()
        {
            if (!IsServer || !IsSpawned || _olhando.Count == 0 || Time.time < _conferirEm) return;
            _conferirEm = Time.time + 0.5f;
            foreach (var par in _olhando)
                par.Value.RemoveWhere(c => !Jogador(c, out var j) || !Ponto(par.Key, out var capo)
                    || (j.transform.position - capo).sqrMagnitude > 4.5f * 4.5f);
            AtualizarAbertos();
        }

        void AtualizarAbertos()
        {
            _vazios.Clear();
            foreach (var par in _olhando)
                if (par.Value.Count == 0) _vazios.Add(par.Key);
            foreach (int k in _vazios) _olhando.Remove(k);
            // a lista da rede só muda no que mudou
            for (int i = _abertos.Count - 1; i >= 0; i--)
                if (!_olhando.ContainsKey(_abertos[i])) _abertos.RemoveAt(i);
            foreach (int k in _olhando.Keys)
                if (!_abertos.Contains(k)) _abertos.Add(k);
        }

        // ------------------------------------------------------------------ as peças do motor

        public override void PedirTirar(int carro, int peca)
        {
            if (IsSpawned) TirarRpc(carro, (byte)peca);
        }

        public override void PedirPor(int carro, int peca, ushort idNaMochila)
        {
            if (IsSpawned) PorRpc(carro, (byte)peca, idNaMochila);
        }

        [Rpc(SendTo.Server)]
        void TirarRpc(int carro, byte peca, RpcParams parametros = default)
        {
            if (!Perto(carro, parametros.Receive.SenderClientId, out var inventario)) return;
            var e = Estado(carro);
            if (!e.pecas.Tirar(peca, inventario)) return;
            Gravar(e);
        }

        [Rpc(SendTo.Server)]
        void PorRpc(int carro, byte peca, ushort id, RpcParams parametros = default)
        {
            if (!Perto(carro, parametros.Receive.SenderClientId, out var inventario)) return;
            var e = Estado(carro);
            if (!e.pecas.Por(peca, inventario, id)) return;
            Gravar(e);
        }

        // ------------------------------------------------------------------ as rodas e o macaco

        public override void PedirApoiarMacaco(int carro, int roda)
        {
            if (IsSpawned) ApoiarMacacoRpc(carro, (byte)roda);
        }

        public override void PedirErguer(int carro, bool erguer)
        {
            if (IsSpawned) ErguerRpc(carro, erguer);
        }

        public override void PedirTirarMacaco(int carro)
        {
            if (IsSpawned) TirarMacacoRpc(carro);
        }

        public override void PedirTirarRoda(int carro, int roda)
        {
            if (IsSpawned) TirarRodaRpc(carro, (byte)roda);
        }

        public override void PedirPorRoda(int carro, int roda, ushort idNaMochila)
        {
            if (IsSpawned) PorRodaRpc(carro, (byte)roda, idNaMochila);
        }

        // no anfitrião: o carro já acabou de subir (ou de descer)
        bool Parado(int carro) => !_macacoMudouEm.TryGetValue(carro, out float t) || Time.time - t >= PecasDosCarros.TempoDoMacaco * 0.98f;

        // só a roda do macaco sai ou entra (o macaco levanta aquele canto)
        bool PodeTrocar(int carro, int roda) => ErguidoDo(carro) && Parado(carro) && MacacoDo(carro) == roda;

        [Rpc(SendTo.Server)]
        void ApoiarMacacoRpc(int carro, byte roda, RpcParams parametros = default)
        {
            if (MacacoDo(carro) >= 0 || !PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario)) return;
            if (!PortaMalasDoCarro.Achar(inventario, Loot.Item.Macaco, out var macaco)) return;
            inventario.Tirar(macaco.id);
            var e = Estado(carro);
            e.macaco = roda;
            Gravar(e);
            _macacoMudouEm.Remove(carro);
            Som(0, carro, roda);
            Susto(carro, parametros.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        void ErguerRpc(int carro, bool erguer, RpcParams parametros = default)
        {
            int m = MacacoDo(carro);
            if (m < 0 || ErguidoDo(carro) == erguer || !Parado(carro) || !PertoDaRoda(carro, m, parametros.Receive.SenderClientId, out _)) return;
            var e = Estado(carro);
            e.macaco = (byte)(m | (erguer ? Erguido : 0));
            Gravar(e);
            _macacoMudouEm[carro] = Time.time;
            Som(1, carro, m);
            if (erguer) Susto(carro, parametros.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server)]
        void TirarMacacoRpc(int carro, RpcParams parametros = default)
        {
            int m = MacacoDo(carro);
            if (m < 0 || ErguidoDo(carro) || !Parado(carro) || !PertoDaRoda(carro, m, parametros.Receive.SenderClientId, out var inventario)) return;
            if (!inventario.Receber(Loot.Item.Macaco, 1)) NoChao(Loot.Item.Macaco, carro, m, 1);
            var e = Estado(carro);
            e.macaco = SemMacaco;
            Gravar(e);
            _macacoMudouEm.Remove(carro);
            Som(0, carro, m);
        }

        [Rpc(SendTo.Server)]
        void TirarRodaRpc(int carro, byte roda, RpcParams parametros = default)
        {
            if (!PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario) || !PodeTrocar(carro, roda)) return;
            var e = Estado(carro);
            if (!e.pneus.Tem(roda)) return;
            int estado = e.pneus.Estado(roda);
            // sem espaço (e com as mãos ocupadas), a roda fica no chão, ao lado
            if (!inventario.Receber(Loot.Item.Pneu, estado)) NoChao(Loot.Item.Pneu, carro, roda, estado);
            e.pneus[roda] = PneusDoCarro.Vazio;
            Gravar(e);
            Som(2, carro, roda);
            Susto(carro, parametros.Receive.SenderClientId);
        }

        // um em cada 10 carros tem um rastejante embaixo: quem mexe no carro (o capô, o macaco, a roda) leva o susto
        // (a pedido em 28/10/2026; Zombies/ZombieManager.Povoamento.cs)
        void Susto(int carro, ulong cliente)
        {
            var zumbis = Zombies.ZombieManager.Instancia;
            if (zumbis != null && Jogador(cliente, out var j)) zumbis.MexeramNoCarro(carro, j.GetComponent<Player.JogadorRede>());
        }

        [Rpc(SendTo.Server)]
        void PorRodaRpc(int carro, byte roda, ushort id, RpcParams parametros = default)
        {
            if (!PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario) || !PodeTrocar(carro, roda)) return;
            var e = Estado(carro);
            if (e.pneus.Tem(roda)) return;
            int estado;
            // o Id 0: o pneu das mãos
            if (id == 0)
            {
                if (!inventario.TirarPneuDasMaos(out estado)) return;
            }
            else
            {
                if (!inventario.Achar(id, out var it) || it.item != Loot.Item.Pneu) return;
                inventario.Tirar(id);
                estado = it.qtd;
            }
            e.pneus[roda] = (byte)Mathf.Clamp(estado, 0, 100);
            Gravar(e);
            Som(2, carro, roda);
        }

        // no anfitrião: o que não coube fica no chão, para fora da roda
        void NoChao(Loot.Item item, int carro, int roda, int qtd)
        {
            var mundo = Loot.ItensNoMundo.Instancia;
            if (mundo == null || !Roda(carro, roda, out var ponto, out var fora)) return;
            ponto += fora * 0.8f;
            if (Physics.Raycast(ponto + Vector3.up, Vector3.down, out var chao, 3f, 1, QueryTriggerInteraction.Ignore)) ponto = chao.point;
            mundo.Criar(item, ponto, Quaternion.LookRotation(fora).eulerAngles.y, qtd);
        }

        void Som(byte qual, int carro, int roda)
        {
            if (Roda(carro, roda, out var ponto, out _)) SomRpc(qual, ponto);
        }

        // 0: o macaco no chão (apoiar e tirar); 1: a manivela (subir e descer); 2: a roda saindo ou entrando
        [Rpc(SendTo.Everyone)]
        void SomRpc(byte qual, Vector3 ponto)
        {
            if (qual == 1) Combat.Efeitos.Som(SomDoCarro.Macaco, ponto, 0.75f, true, 18f);
            else Combat.Efeitos.Som(Combat.Efeitos.Largar, ponto, qual == 2 ? 1f : 0.8f, true, 15f);
        }

        /// <summary>Um sorteio que só depende da semente do mundo e do número do carro (igual em todas as máquinas).</summary>
        struct Sorteio
        {
            uint _s;

            public Sorteio(int semente, int carro)
            {
                _s = (uint)semente * 0x9E3779B1u ^ ((uint)carro + 0x7F4A7C15u) * 0x85EBCA77u;
                if (_s == 0) _s = 0x6C8E9CF5u;
                Proximo();
                Proximo();
            }

            uint Proximo()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return _s;
            }

            /// <summary>De 0 (inclusive) a 1 (exclusive).</summary>
            public float Valor() => (Proximo() >> 8) * (1f / 16777216f);
            /// <summary>De <paramref name="min"/> a <paramref name="max"/>, os dois inclusive.</summary>
            public int Entre(int min, int max) => min + (int)(Valor() * (max - min + 1));
        }
    }
}
