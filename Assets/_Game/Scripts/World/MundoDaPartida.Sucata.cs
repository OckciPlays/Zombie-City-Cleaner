using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os painéis de metal desmontados (09/11/2026, a sucata, 2ª leva; os painéis são do <see cref="PaineisDeSucata"/>,
    /// postos na cena pelo <see cref="PaineisDoMundo"/>): a lista da rede, escrita pelo anfitrião e guardada no save do
    /// mundo (o desmontado não volta). Segurar o E no painel por <see cref="SegundosDoPainel"/> s pede ao anfitrião, que
    /// confere (o painel existe, ainda está lá e quem pede está perto), dá de <see cref="SucataDoPainelMin"/> a
    /// <see cref="SucataDoPainelMax"/> sucatas (mais 1 por ponto do Sucateiro; o que não cabe fica no chão), o XP e o
    /// barulho do metal, que chama os zumbis até <see cref="RaioDoBarulhoDoPainel"/> m (como o pé de cabra). Todos ouvem o
    /// fim e veem o painel sumir.
    /// <para>
    /// Os do F1 ("Painel na frente"): uma segunda lista da rede, com o lugar de cada um, de número
    /// <see cref="PrimeiroPainelDeTeste"/> em diante; não vão no save.
    /// </para>
    /// </summary>
    public partial class MundoDaPartida
    {
        public const float SegundosDoPainel = 3f, RaioDoBarulhoDoPainel = 25f, DistanciaDoPainel = 4f;
        public const int SucataDoPainelMin = 2, SucataDoPainelMax = 4;
        /// <summary>Os painéis do F1 têm o número daqui em diante (os do mapa ficam bem abaixo).</summary>
        public const int PrimeiroPainelDeTeste = 1 << 24;

        NetworkList<int> _paineisDesmontados;
        NetworkList<Vector4> _paineisDeTeste;

        // (no Awake: as listas da rede existem antes do spawn; o PaineisDoMundo vai no mesmo objeto)
        void IniciarOsPaineis()
        {
            _paineisDesmontados = new NetworkList<int>();
            _paineisDesmontados.OnListChanged += _ => PaineisDoMundo.DefinirDesmontados(Copia(_paineisDesmontados));
            _paineisDeTeste = new NetworkList<Vector4>();
            _paineisDeTeste.OnListChanged += _ => PaineisDoMundo.DefinirDeTeste(Copia(_paineisDeTeste));
            if (GetComponent<PaineisDoMundo>() == null) gameObject.AddComponent<PaineisDoMundo>();
        }

        // (no spawn: o anfitrião começa sem nada; quem entra recebe as listas)
        void PaineisNoSpawn()
        {
            if (IsServer)
            {
                _paineisDesmontados.Clear();
                _paineisDeTeste.Clear();
            }
            PaineisDoMundo.DefinirDesmontados(Copia(_paineisDesmontados));
            PaineisDoMundo.DefinirDeTeste(Copia(_paineisDeTeste));
        }

        static List<int> Copia(NetworkList<int> l)
        {
            var c = new List<int>(l.Count);
            foreach (int k in l) c.Add(k);
            return c;
        }

        static List<Vector4> Copia(NetworkList<Vector4> l)
        {
            var c = new List<Vector4>(l.Count);
            foreach (var k in l) c.Add(k);
            return c;
        }

        /// <summary>Os painéis do mapa desmontados (o save; os do F1 não entram).</summary>
        public List<int> PaineisDesmontados() => _paineisDesmontados != null ? Copia(_paineisDesmontados) : new List<int>();

        /// <summary>No anfitrião: os desmontados de um save (os números que não existem neste mapa ficam de fora).</summary>
        public void RestaurarPaineis(IEnumerable<int> indices)
        {
            if (!IsServer || _paineisDesmontados == null) return;
            _paineisDesmontados.Clear();
            if (indices == null) return;
            var vistos = new HashSet<int>();
            foreach (int i in indices)
                if (i >= 0 && i < PrimeiroPainelDeTeste && vistos.Add(i)) _paineisDesmontados.Add(i);
        }

        /// <summary>No anfitrião (o F1): um painel de teste no chão, neste lugar.</summary>
        public void PorPainelDeTeste(Vector3 onde, float giro, int modelo)
        {
            if (!IsServer || _paineisDeTeste == null) return;
            modelo = Mathf.Clamp(modelo, 0, PaineisDeSucata.Modelos - 1);
            _paineisDeTeste.Add(new Vector4(onde.x, onde.y, onde.z, Mathf.Repeat(giro, 360f) + modelo * 1000f));
        }

        /// <summary>Quantos painéis do mapa já foram desmontados.</summary>
        public int QuantosPaineisDesmontados => _paineisDesmontados != null ? _paineisDesmontados.Count : 0;

        // no anfitrião: onde está o painel (e se ainda está lá)
        bool OndeEstaOPainel(int indice, out Vector3 onde)
        {
            onde = default;
            if (indice >= PrimeiroPainelDeTeste)
            {
                int k = indice - PrimeiroPainelDeTeste;
                if (_paineisDeTeste == null || k < 0 || k >= _paineisDeTeste.Count || _paineisDeTeste[k].w < 0f) return false;
                var v = _paineisDeTeste[k];
                onde = new Vector3(v.x, v.y, v.z);
                return true;
            }
            var paineis = PaineisDeSucata.Do(Dados);
            if (paineis == null || !paineis.Existe(indice) || _paineisDesmontados.Contains(indice)) return false;
            var p = paineis.Todos[indice];
            onde = new Vector3(p.Posicao.x, PaineisDeSucata.AlturaDoChao(Dados, p), p.Posicao.y);
            return true;
        }

        /// <summary>No dono: o E segurado no painel acabou (o anfitrião confere e desmonta).</summary>
        public void PedirDesmontarPainel(int indice)
        {
            if (IsSpawned) DesmontarPainelRpc(indice);
        }

        [Rpc(SendTo.Server)]
        void DesmontarPainelRpc(int indice, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return;
            var j = cliente.PlayerObject.GetComponent<JogadorRede>();
            var inv = cliente.PlayerObject.GetComponent<InventarioJogador>();
            if (j == null || inv == null || !OndeEstaOPainel(indice, out var onde)) return;
            var pes = j.transform.position;
            if (new Vector2(pes.x - onde.x, pes.z - onde.z).magnitude > DistanciaDoPainel) return;
            if (indice >= PrimeiroPainelDeTeste)
            {
                int k = indice - PrimeiroPainelDeTeste;
                var v = _paineisDeTeste[k];
                _paineisDeTeste[k] = new Vector4(v.x, v.y, v.z, -1f);
            }
            else _paineisDesmontados.Add(indice);
            // a sucata: de 2 a 4, mais 1 por ponto do Sucateiro; o que não cabe na mochila fica no chão, no lugar do painel
            int n = Random.Range(SucataDoPainelMin, SucataDoPainelMax + 1) + Habilidades.Pontos(j, Player.Habilidade.Sucateiro);
            int sobra = inv.ReceberEmPilha(Item.Sucata, n);
            if (sobra > 0) ItensNoMundo.Instancia?.Criar(Item.Sucata, onde + Vector3.up * 0.05f, Random.Range(0f, 360f), sobra);
            inv.GanharXp(Habilidades.XpDoPainel, "sucata");
            Zombies.ZombieManager.Barulho(onde + Vector3.up, j, RaioDoBarulhoDoPainel);
            Debug.Log($"[Sucata] O painel {indice} virou {n} sucatas (o jogador {quem}); o barulho chama os zumbis até {RaioDoBarulhoDoPainel:0} m.");
            DesmontouPainelRpc(indice, onde, quem, (byte)Mathf.Min(255, n), (byte)Mathf.Min(255, n - sobra));
        }

        // anfitrião → todos: o painel sumiu (o som do metal soltando); quem desmontou vê o aviso
        [Rpc(SendTo.Everyone)]
        void DesmontouPainelRpc(int indice, Vector3 onde, ulong quem, byte n, byte naMochila)
        {
            PaineisDoMundo.Tirar(indice);
            Combat.Efeitos.Som(Combat.SomDaSucata.Soltando, onde + Vector3.up * 0.1f, 1f, true, 40f);
            if (NetworkManager == null || quem != NetworkManager.LocalClientId) return;
            string pedacos = Itens.ComNumero(Item.Sucata, n);
            InventarioJogador.AvisoLocal(naMochila >= n ? $"Desmontou: {pedacos} na mochila"
                : naMochila == 0 ? $"Desmontou: {pedacos} (sem lugar na mochila: ficaram no chão)"
                : $"Desmontou: {pedacos} ({n - naMochila} ficaram no chão, sem lugar na mochila)");
        }
    }
}
