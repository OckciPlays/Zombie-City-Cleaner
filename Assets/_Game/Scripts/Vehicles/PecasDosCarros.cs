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
    public class PecasDosCarros : NetworkBehaviour
    {
        [Tooltip("O capô de cada carro (CapoDaCidade), na ordem da lista.")]
        public CapoDaCidade[] capos;
        [Tooltip("A chance de cada peça (a vela, a bateria e o radiador) ainda estar no carro.")]
        public float[] chance = { 0.55f, 0.5f, 0.45f };
        [Tooltip("O estado sorteado de cada peça (%): x o mínimo, y o máximo. A bateria parada perde a carga.")]
        public Vector2Int[] faixas = { new(15, 100), new(5, 70), new(20, 100) };
        [Tooltip("Até onde o anfitrião aceita o pedido (m, dos pés do jogador ao capô).")]
        public float distanciaMaxima = 3.5f;

        NetworkList<PecasDoMotor> _pecas;
        // um bit por carro: o capô aberto (alguém olhando o motor)
        readonly NetworkVariable<ulong> _abertos = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly Dictionary<int, HashSet<ulong>> _olhando = new Dictionary<int, HashSet<ulong>>();
        readonly List<int> _vazios = new List<int>();
        float _conferirEm;

        void Awake() => _pecas = new NetworkList<PecasDoMotor>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _olhando.Clear();
            _abertos.Value = 0;
            _pecas.Clear();
            for (int i = 0; capos != null && i < capos.Length; i++)
            {
                var p = new PecasDoMotor();
                for (int k = 0; k < PecasDoMotor.Quantas; k++)
                    p[k] = Random.value < chance[k] ? (byte)Random.Range(faixas[k].x, faixas[k].y + 1) : PecasDoMotor.Vazio;
                _pecas.Add(p);
            }
        }

        /// <summary>As peças do carro (antes da lista chegar, nenhuma).</summary>
        public PecasDoMotor Do(int carro) =>
            IsSpawned && carro >= 0 && carro < _pecas.Count ? _pecas[carro] : PecasDoMotor.Com(-1, -1, -1);

        /// <summary>Alguém está olhando o motor deste carro (o capô fica aberto).</summary>
        public bool EstaAberto(int carro) => IsSpawned && carro >= 0 && carro < 64 && (_abertos.Value & (1UL << carro)) != 0;

        /// <summary>O jogador local começou ou parou de olhar o motor do carro.</summary>
        public void Olhar(int carro, bool olhando)
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

        public void PedirTirar(int carro, int peca)
        {
            if (IsSpawned) TirarRpc((byte)carro, (byte)peca);
        }

        public void PedirPor(int carro, int peca, ushort id)
        {
            if (IsSpawned) PorRpc((byte)carro, (byte)peca, id);
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
    }
}
