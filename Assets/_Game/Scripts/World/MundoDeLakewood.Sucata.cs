using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os painéis de metal desmontados no Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a
    /// parte 4): o mesmo do mundo sorteado (<see cref="MundoDaPartida"/>.Sucata.cs): segurar o E no painel pede ao anfitrião,
    /// que confere (o painel existe, ainda está lá e quem pede está perto), dá de 2 a 4 sucatas (mais 1 por ponto do
    /// Sucateiro; o que não cabe fica no chão), o XP e o barulho do metal. A lista dos desmontados vai pela rede (e no save).
    /// Os painéis são os do Lakewood (<see cref="Mundo.PaineisDeSucata.DoLakewood"/>), postos na cena pelo
    /// <see cref="PaineisDoMundo"/>, que fica neste objeto.
    /// </summary>
    public partial class MundoDeLakewood
    {
        NetworkList<int> _paineisDesmontados;

        partial void IniciarAsOutrasListas()
        {
            _paineisDesmontados = new NetworkList<int>();
            _paineisDesmontados.OnListChanged += _ => PaineisDoMundo.DefinirDesmontados(CopiaDosPaineis());
            if (GetComponent<PaineisDoMundo>() == null) gameObject.AddComponent<PaineisDoMundo>();
            IniciarAsListasDasMarcas();
        }

        // as listas das marcas do mapa (a parte 7)
        partial void IniciarAsListasDasMarcas();

        List<int> CopiaDosPaineis()
        {
            var c = new List<int>(_paineisDesmontados.Count);
            foreach (int k in _paineisDesmontados) c.Add(k);
            return c;
        }

        // (no spawn: quem entra recebe a lista)
        void PaineisNoSpawn() => PaineisDoMundo.DefinirDesmontados(CopiaDosPaineis());

        /// <summary>Os painéis desmontados (o save).</summary>
        public List<int> PaineisDesmontados() => _paineisDesmontados != null ? CopiaDosPaineis() : new List<int>();

        /// <summary>No anfitrião: os desmontados de um save.</summary>
        public void RestaurarPaineis(IEnumerable<int> indices)
        {
            if (!IsServer || _paineisDesmontados == null) return;
            _paineisDesmontados.Clear();
            if (indices == null) return;
            var vistos = new HashSet<int>();
            foreach (int i in indices)
                if (i >= 0 && i < MundoDaPartida.PrimeiroPainelDeTeste && vistos.Add(i)) _paineisDesmontados.Add(i);
        }

        // no anfitrião: onde está o painel (e se ainda está lá)
        bool OndeEstaOPainel(int indice, out Vector3 onde)
        {
            onde = default;
            var fonte = BeiraDoMapa.Atual;
            var paineis = fonte?.Paineis;
            if (paineis == null || !paineis.Existe(indice) || _paineisDesmontados.Contains(indice)) return false;
            var p = paineis.Todos[indice];
            onde = new Vector3(p.Posicao.x, fonte.AlturaDoPainel(p), p.Posicao.y);
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
            if (new Vector2(pes.x - onde.x, pes.z - onde.z).magnitude > MundoDaPartida.DistanciaDoPainel) return;
            _paineisDesmontados.Add(indice);
            int n = Random.Range(MundoDaPartida.SucataDoPainelMin, MundoDaPartida.SucataDoPainelMax + 1) + Habilidades.Pontos(j, Habilidade.Sucateiro);
            int sobra = inv.ReceberEmPilha(Item.Sucata, n);
            if (sobra > 0) ItensNoMundo.Instancia?.Criar(Item.Sucata, onde + Vector3.up * 0.05f, Random.Range(0f, 360f), sobra);
            inv.GanharXp(Habilidades.XpDoPainel, "sucata");
            Zombies.ZombieManager.Barulho(onde + Vector3.up, j, MundoDaPartida.RaioDoBarulhoDoPainel);
            DesmontouPainelRpc(indice, onde, quem, (byte)Mathf.Min(255, n), (byte)Mathf.Min(255, n - sobra));
        }

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
