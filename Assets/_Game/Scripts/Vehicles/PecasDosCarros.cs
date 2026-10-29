using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// As peças do motor dos carros da cidade (no NetworkObject "Peças dos carros", montado pela montagem dos veículos):
    /// uma lista na rede, um item por carro (<see cref="capos"/>, na mesma ordem), escrita pelo anfitrião. No começo da
    /// partida ele sorteia o que sobrou em cada carro: cada peça está lá ou não (<see cref="chance"/>) e, se está, com um
    /// estado sorteado (<see cref="faixas"/>). Tirar e pôr vão por RPC; o anfitrião confere a distância.
    /// Também guarda quem está olhando o motor de cada carro (um bit por carro, <see cref="EstaAberto"/>): com alguém
    /// olhando, o capô dele fica aberto em todas as máquinas. Quem se afasta mais de 4,5 m ou sai deixa de contar.
    /// </summary>
    public class PecasDosCarros : FonteDasPecas
    {
        [Tooltip("O capô de cada carro (CapoDaCidade), na ordem da lista.")]
        public CapoDaCidade[] capos;
        [Tooltip("A chance de cada peça (a vela, a bateria e o radiador) ainda estar no carro.")]
        public float[] chance = { 0.55f, 0.5f, 0.45f };
        [Tooltip("O estado sorteado de cada peça (%): x o mínimo, y o máximo. A bateria parada perde a carga.")]
        public Vector2Int[] faixas = { new(15, 100), new(5, 70), new(20, 100) };
        [Tooltip("Até onde o anfitrião aceita o pedido (m, dos pés do jogador ao capô).")]
        public float distanciaMaxima = 3.5f;

        [Header("As rodas")]
        [Tooltip("A chance de cada roda ainda estar no carro e, se está, de o pneu estar furado.")]
        public float chanceDaRoda = 0.9f;
        public float chanceDeFurado = 0.3f;
        [Tooltip("O estado sorteado do pneu que não está furado (%).")]
        public Vector2Int faixaDoPneu = new(15, 85);

        /// <summary>Quanto tempo o macaco leva para levantar (ou baixar) o carro (s), o mesmo da perua.</summary>
        public const float TempoDoMacaco = 2.2f;
        public const byte SemMacaco = 255, Erguido = 0x80;

        NetworkList<PecasDoMotor> _pecas;
        // as 4 rodas de cada carro, e o macaco (255, sem ele; senão a roda dele, com o bit 7 erguido)
        NetworkList<PneusDoCarro> _pneus;
        NetworkList<byte> _macacos;
        // no anfitrião: quando o macaco de cada carro começou a subir ou a descer
        readonly Dictionary<int, float> _macacoMudouEm = new Dictionary<int, float>();
        // um bit por carro: o capô aberto (alguém olhando o motor)
        readonly NetworkVariable<ulong> _abertos = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly Dictionary<int, HashSet<ulong>> _olhando = new Dictionary<int, HashSet<ulong>>();
        readonly List<int> _vazios = new List<int>();
        float _conferirEm;

        void Awake()
        {
            _pecas = new NetworkList<PecasDoMotor>();
            _pneus = new NetworkList<PneusDoCarro>();
            _macacos = new NetworkList<byte>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _olhando.Clear();
            _abertos.Value = 0;
            _pecas.Clear();
            _pneus.Clear();
            _macacos.Clear();
            _macacoMudouEm.Clear();
            for (int i = 0; capos != null && i < capos.Length; i++)
            {
                var p = new PecasDoMotor();
                for (int k = 0; k < PecasDoMotor.Quantas; k++)
                    p[k] = Random.value < chance[k] ? (byte)Random.Range(faixas[k].x, faixas[k].y + 1) : PecasDoMotor.Vazio;
                _pecas.Add(p);
                // as rodas: algumas já foram levadas, e muitos pneus estão furados
                var r = new PneusDoCarro();
                for (int k = 0; k < PneusDoCarro.Quantas; k++)
                    r[k] = Random.value >= chanceDaRoda ? PneusDoCarro.Vazio
                        : Random.value < chanceDeFurado ? (byte)Random.Range(0, PneusDoCarro.Minimo) : (byte)Random.Range(faixaDoPneu.x, faixaDoPneu.y + 1);
                _pneus.Add(r);
                _macacos.Add(SemMacaco);
            }
        }

        /// <summary>As 4 rodas do carro (antes da lista chegar, todas, como no modelo).</summary>
        public override PneusDoCarro PneusDo(int carro) =>
            IsSpawned && carro >= 0 && carro < _pneus.Count ? _pneus[carro] : PneusDoCarro.Com(new[] { 100, 100, 100, 100 });

        /// <summary>A roda ao lado da qual o macaco está apoiado neste carro, ou -1.</summary>
        public override int MacacoDo(int carro) =>
            IsSpawned && carro >= 0 && carro < _macacos.Count && _macacos[carro] != SemMacaco ? _macacos[carro] & 3 : -1;

        /// <summary>O macaco deste carro está levantado.</summary>
        public override bool ErguidoDo(int carro) => MacacoDo(carro) >= 0 && (_macacos[carro] & Erguido) != 0;

        /// <summary>As peças do carro (antes da lista chegar, nenhuma).</summary>
        public override PecasDoMotor Do(int carro) =>
            IsSpawned && carro >= 0 && carro < _pecas.Count ? _pecas[carro] : PecasDoMotor.Com(-1, -1, -1);

        /// <summary>Alguém está olhando o motor deste carro (o capô fica aberto).</summary>
        public override bool EstaAberto(int carro) => IsSpawned && carro >= 0 && carro < 64 && (_abertos.Value & (1UL << carro)) != 0;

        /// <summary>O jogador local começou ou parou de olhar o motor do carro.</summary>
        public override void Olhar(int carro, bool olhando)
        {
            if (IsSpawned && carro >= 0 && carro < 64) OlharRpc((byte)carro, olhando);
        }

        [Rpc(SendTo.Server)]
        void OlharRpc(byte carro, bool olhando, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (!_olhando.TryGetValue(carro, out var quem)) _olhando[carro] = quem = new HashSet<ulong>();
            if (olhando && Perto(carro, cliente, out _)) quem.Add(cliente);
            else quem.Remove(cliente);
            AtualizarAbertos();
        }

        // no anfitrião: quem se afastou (com folga) ou saiu deixa de olhar
        void Update()
        {
            if (!IsServer || !IsSpawned || _olhando.Count == 0 || Time.time < _conferirEm) return;
            _conferirEm = Time.time + 0.5f;
            foreach (var par in _olhando)
                par.Value.RemoveWhere(c =>
                {
                    if (!NetworkManager.ConnectedClients.TryGetValue(c, out var cl) || cl.PlayerObject == null) return true;
                    return capos == null || par.Key >= capos.Length || capos[par.Key] == null
                        || (cl.PlayerObject.transform.position - capos[par.Key].Ponto).sqrMagnitude > 4.5f * 4.5f;
                });
            AtualizarAbertos();
        }

        void AtualizarAbertos()
        {
            ulong bits = 0;
            _vazios.Clear();
            foreach (var par in _olhando)
            {
                if (par.Value.Count > 0) bits |= 1UL << par.Key;
                else _vazios.Add(par.Key);
            }
            foreach (int k in _vazios) _olhando.Remove(k);
            if (_abertos.Value != bits) _abertos.Value = bits;
        }

        public override void PedirTirar(int carro, int peca)
        {
            if (IsSpawned) TirarRpc((byte)carro, (byte)peca);
        }

        public override void PedirPor(int carro, int peca, ushort idNaMochila)
        {
            if (IsSpawned) PorRpc((byte)carro, (byte)peca, idNaMochila);
        }

        bool Perto(int carro, ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            if (capos == null || carro >= capos.Length || carro >= _pecas.Count || capos[carro] == null) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) || c.PlayerObject == null) return false;
            if ((c.PlayerObject.transform.position - capos[carro].Ponto).sqrMagnitude > distanciaMaxima * distanciaMaxima) return false;
            inventario = c.PlayerObject.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        [Rpc(SendTo.Server)]
        void TirarRpc(byte carro, byte peca, RpcParams parametros = default)
        {
            if (!Perto(carro, parametros.Receive.SenderClientId, out var inventario)) return;
            var p = _pecas[carro];
            if (p.Tirar(peca, inventario)) _pecas[carro] = p;
        }

        [Rpc(SendTo.Server)]
        void PorRpc(byte carro, byte peca, ushort id, RpcParams parametros = default)
        {
            if (!Perto(carro, parametros.Receive.SenderClientId, out var inventario)) return;
            var p = _pecas[carro];
            if (p.Por(peca, inventario, id)) _pecas[carro] = p;
        }

        // ------------------------------------------------------------------ as rodas e o macaco (como na perua)

        public override void PedirApoiarMacaco(int carro, int roda)
        {
            if (IsSpawned) ApoiarMacacoRpc((byte)carro, (byte)roda);
        }

        public override void PedirErguer(int carro, bool erguer)
        {
            if (IsSpawned) ErguerRpc((byte)carro, erguer);
        }

        public override void PedirTirarMacaco(int carro)
        {
            if (IsSpawned) TirarMacacoRpc((byte)carro);
        }

        public override void PedirTirarRoda(int carro, int roda)
        {
            if (IsSpawned) TirarRodaRpc((byte)carro, (byte)roda);
        }

        /// <summary>Um pneu da mochila (pelo Id; 0, o das mãos) vai para a roda vazia do lado erguido.</summary>
        public override void PedirPorRoda(int carro, int roda, ushort idNaMochila)
        {
            if (IsSpawned) PorRodaRpc((byte)carro, (byte)roda, idNaMochila);
        }

        bool PertoDaRoda(int carro, int roda, ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            if (capos == null || carro >= capos.Length || carro >= _pneus.Count || capos[carro] == null || roda < 0 || roda >= PneusDoCarro.Quantas) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) || c.PlayerObject == null) return false;
            var p = c.PlayerObject.GetComponent<PassageiroDoCarro>();
            if (p != null && p.Sentado) return false;
            if ((c.PlayerObject.transform.position - capos[carro].PosicaoDaRoda(roda)).sqrMagnitude > 3f * 3f) return false;
            inventario = c.PlayerObject.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        // no anfitrião: o carro já acabou de subir (ou de descer)
        bool Parado(int carro) => !_macacoMudouEm.TryGetValue(carro, out float t) || Time.time - t >= TempoDoMacaco * 0.98f;

        [Rpc(SendTo.Server)]
        void ApoiarMacacoRpc(byte carro, byte roda, RpcParams parametros = default)
        {
            if (MacacoDo(carro) >= 0 || !PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario)) return;
            if (!PortaMalasDoCarro.Achar(inventario, Loot.Item.Macaco, out var macaco)) return;
            inventario.Tirar(macaco.id);
            _macacos[carro] = roda;
            _macacoMudouEm.Remove(carro);
            SomRpc(0, capos[carro].PosicaoDaRoda(roda));
        }

        [Rpc(SendTo.Server)]
        void ErguerRpc(byte carro, bool erguer, RpcParams parametros = default)
        {
            int m = MacacoDo(carro);
            if (m < 0 || ErguidoDo(carro) == erguer || !PertoDaRoda(carro, m, parametros.Receive.SenderClientId, out _)) return;
            _macacos[carro] = (byte)(m | (erguer ? Erguido : 0));
            _macacoMudouEm[carro] = Time.time;
            SomRpc(1, capos[carro].PosicaoDaRoda(m));
        }

        [Rpc(SendTo.Server)]
        void TirarMacacoRpc(byte carro, RpcParams parametros = default)
        {
            int m = MacacoDo(carro);
            if (m < 0 || ErguidoDo(carro) || !Parado(carro) || !PertoDaRoda(carro, m, parametros.Receive.SenderClientId, out var inventario)) return;
            if (!inventario.Receber(Loot.Item.Macaco, 1)) NoChao(Loot.Item.Macaco, carro, m, 1);
            _macacos[carro] = SemMacaco;
            _macacoMudouEm.Remove(carro);
            SomRpc(0, capos[carro].PosicaoDaRoda(m));
        }

        // só a roda do macaco sai ou entra (o macaco levanta aquele canto)
        bool PodeTrocar(int carro, int roda) => ErguidoDo(carro) && Parado(carro) && MacacoDo(carro) == roda;

        [Rpc(SendTo.Server)]
        void TirarRodaRpc(byte carro, byte roda, RpcParams parametros = default)
        {
            if (!PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario) || !PodeTrocar(carro, roda)) return;
            var p = _pneus[carro];
            if (!p.Tem(roda)) return;
            int estado = p.Estado(roda);
            // sem espaço (e com as mãos ocupadas), a roda fica no chão, ao lado
            if (!inventario.Receber(Loot.Item.Pneu, estado)) NoChao(Loot.Item.Pneu, carro, roda, estado);
            p[roda] = PneusDoCarro.Vazio;
            _pneus[carro] = p;
            SomRpc(2, capos[carro].PosicaoDaRoda(roda));
        }

        [Rpc(SendTo.Server)]
        void PorRodaRpc(byte carro, byte roda, ushort id, RpcParams parametros = default)
        {
            if (!PertoDaRoda(carro, roda, parametros.Receive.SenderClientId, out var inventario) || !PodeTrocar(carro, roda)) return;
            var p = _pneus[carro];
            if (p.Tem(roda)) return;
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
            p[roda] = (byte)Mathf.Clamp(estado, 0, 100);
            _pneus[carro] = p;
            SomRpc(2, capos[carro].PosicaoDaRoda(roda));
        }

        // no anfitrião: o que não coube fica no chão, para fora da roda
        void NoChao(Loot.Item item, int carro, int roda, int qtd)
        {
            var mundo = Loot.ItensNoMundo.Instancia;
            if (mundo == null) return;
            var capo = capos[carro];
            var t = capo.carroFechado != null ? capo.carroFechado.transform : capo.transform;
            Vector3 ponto = capo.PosicaoDaRoda(roda) + t.right * Mathf.Sign(capo.centrosDasRodas[roda].x) * 0.8f;
            ponto.y = t.position.y;
            mundo.Criar(item, ponto, t.eulerAngles.y, qtd);
        }

        // 0: o macaco no chão (apoiar e tirar); 1: a manivela (subir e descer); 2: a roda saindo ou entrando
        [Rpc(SendTo.Everyone)]
        void SomRpc(byte qual, Vector3 ponto)
        {
            if (qual == 1) Combat.Efeitos.Som(SomDoCarro.Macaco, ponto, 0.75f, true, 18f);
            else Combat.Efeitos.Som(Combat.Efeitos.Largar, ponto, qual == 2 ? 1f : 0.8f, true, 15f);
        }
    }
}
