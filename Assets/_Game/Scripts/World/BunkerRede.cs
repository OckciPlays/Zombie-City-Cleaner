using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O bunker na rede: a porta blindada (aberta ou fechada) e as luzes de cada cômodo, acesas e apagadas nos
    /// interruptores. Quem manda é o anfitrião: o jogador pede (E na porta ou num interruptor), ele confere a distância e
    /// muda o estado, que vai para todos.
    /// Com a porta fechada (de verdade, não fechando), o lado de dentro fica isolado: o som não passa de um lado para o
    /// outro (<see cref="Isolado"/>), e os zumbis lá fora não ouvem nem sentem quem está dentro.
    /// O objeto fica nas coordenadas da casa da família (as mesmas da planta do bunker), montado pela montagem da cidade.
    /// </summary>
    public class BunkerRede : NetworkBehaviour
    {
        [Header("Porta")]
        public PortaDoBunker porta;
        [Tooltip("A porta começa a partida aberta?")]
        public bool abertaNoComeco;

        [Header("Luzes (grupos: 0 salão, 1 a 4 quartos, 5 depósito, 6 escada)")]
        public Light[] luzes;
        [Tooltip("A lâmpada de cada luz (na mesma ordem): acesa, ela brilha.")]
        public MeshRenderer[] lampadas;
        public int[] grupoDaLuz;
        public Material lampadaAcesa;
        public Material lampadaApagada;
        public Interruptor[] interruptores;

        [Header("O lado de dentro")]
        [Tooltip("A caixa do lado de dentro da porta (salão, corredores e cômodos), nas coordenadas deste objeto.")]
        public Bounds dentro;
        [Tooltip("Até onde o anfitrião aceita o pedido (m, dos pés do jogador à porta ou ao interruptor).")]
        public float distanciaMaxima = 3f;

        const byte TodasAcesas = 0x7F;

        readonly NetworkVariable<bool> _aberta = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _luzes = new(TodasAcesas, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public static BunkerRede Instancia { get; private set; }

        /// <summary>O estado pedido da porta (a folha pode ainda estar indo).</summary>
        public bool Aberta => IsSpawned ? _aberta.Value : abertaNoComeco;

        public bool Acesa(int grupo) => (((IsSpawned ? _luzes.Value : TodasAcesas) >> grupo) & 1) != 0;

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        void Start()
        {
            if (IsSpawned) return;
            // no menu, antes da partida
            if (porta != null) porta.Ir(abertaNoComeco, true);
            AplicarLuzes(TodasAcesas, 0);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _aberta.Value = abertaNoComeco;
                _luzes.Value = TodasAcesas;
            }
            _aberta.OnValueChanged += AoMudarPorta;
            _luzes.OnValueChanged += AoMudarLuzes;
            if (porta != null) porta.Ir(_aberta.Value, true);
            AplicarLuzes(_luzes.Value, 0);
        }

        public override void OnNetworkDespawn()
        {
            _aberta.OnValueChanged -= AoMudarPorta;
            _luzes.OnValueChanged -= AoMudarLuzes;
            if (porta != null) porta.Ir(abertaNoComeco, true);
            AplicarLuzes(TodasAcesas, 0);
        }

        void AoMudarPorta(bool antes, bool depois)
        {
            if (porta != null) porta.Ir(depois, false);
        }

        void AoMudarLuzes(byte antes, byte depois) => AplicarLuzes(depois, (byte)(antes ^ depois));

        /// <summary>No anfitrião, sem pedido nem distância: a cena de vídeo apaga o bunker, abre a porta e acende os cômodos um por um.</summary>
        public void Definir(byte acesas, bool aberta)
        {
            if (!IsServer) return;
            _luzes.Value = acesas;
            _aberta.Value = aberta;
        }

        /// <summary>O jogador local pede para abrir ou fechar a porta.</summary>
        public void PedirPorta()
        {
            if (IsSpawned) PortaRpc();
        }

        /// <summary>O jogador local virou um interruptor (o índice dele em <see cref="interruptores"/>).</summary>
        public void PedirLuz(int interruptor)
        {
            if (IsSpawned && interruptor >= 0 && interruptor < interruptores.Length) LuzRpc((byte)interruptor);
        }

        [Rpc(SendTo.Server)]
        void PortaRpc(RpcParams parametros = default)
        {
            var jogador = Quem(parametros);
            if (porta == null || jogador == null || Vector3.Distance(jogador.transform.position, porta.transform.position) > distanciaMaxima) return;
            // fechando, ninguém pode estar no caminho da folha (ela passaria por dentro do jogador)
            if (_aberta.Value && !porta.CaminhoLivre()) return;
            _aberta.Value = !_aberta.Value;
        }

        [Rpc(SendTo.Server)]
        void LuzRpc(byte indice, RpcParams parametros = default)
        {
            if (indice >= interruptores.Length) return;
            var jogador = Quem(parametros);
            if (jogador == null || Vector3.Distance(jogador.transform.position, interruptores[indice].transform.position) > distanciaMaxima) return;
            _luzes.Value = (byte)(_luzes.Value ^ (1 << interruptores[indice].grupo));
        }

        JogadorRede Quem(RpcParams parametros)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return null;
            return cliente.PlayerObject.GetComponent<JogadorRede>();
        }

        // acende e apaga as luzes; os interruptores dos grupos que mudaram estalam
        void AplicarLuzes(byte acesas, byte mudaram)
        {
            for (int i = 0; i < luzes.Length; i++)
            {
                bool acesa = ((acesas >> grupoDaLuz[i]) & 1) != 0;
                if (luzes[i] != null) luzes[i].enabled = acesa;
                if (i < lampadas.Length && lampadas[i] != null) lampadas[i].sharedMaterial = acesa ? lampadaAcesa : lampadaApagada;
            }
            foreach (var s in interruptores)
            {
                if (s == null) continue;
                s.Mostrar(((acesas >> s.grupo) & 1) != 0);
                if (((mudaram >> s.grupo) & 1) != 0) Efeitos.Som(Efeitos.Chave, s.transform.position, 0.7f, true, 12f);
            }
        }

        /// <summary>O ponto está do lado de dentro da porta (salão, corredores ou cômodos)?</summary>
        public bool Dentro(Vector3 ponto) => dentro.Contains(transform.InverseTransformPoint(ponto));

        /// <summary>
        /// O som de um ponto não chega no outro: a porta está fechada (a folha já encostou) e só um dos dois está do lado
        /// de dentro. Vale para o som de cada máquina e para o que os zumbis ouvem e sentem.
        /// </summary>
        public static bool Isolado(Vector3 a, Vector3 b)
        {
            var b0 = Instancia;
            if (b0 == null || b0.porta == null || !b0.porta.Fechada) return false;
            return b0.Dentro(a) != b0.Dentro(b);
        }
    }
}
