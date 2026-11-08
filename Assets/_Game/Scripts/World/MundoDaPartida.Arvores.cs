using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As árvores cortadas do mundo (06/11/2026, o machado): a lista da rede (escrita pelo anfitrião, no save do mundo).
    /// <para>
    /// Os cortes dos lados (07/11/2026, a pedido: "para cortar a árvore, é preciso fazer vários cortes aos lados da tora
    /// para poder cair"): o tronco tem 4 lados (de 90 em 90°, a partir do norte), e cada golpe do machado entra no lado
    /// onde pegou (<see cref="PedirGolpeNaArvore"/>). Cada lado vai fundo em <see cref="GolpesPorLado"/> golpes (de 2 a 4,
    /// pelo tamanho da árvore); o lado já fundo não aprofunda mais ("corte do lado oposto"). Com dois lados opostos fundos
    /// (o entalhe e o corte de trás), ela cai, para o lado do primeiro: para longe de quem deu o último golpe. Dois jogadores
    /// podem cortar juntos, um de cada lado. Todos veem os entalhes no tronco (<see cref="FlorestaDoMundo.MarcarEntalhe"/>);
    /// o que está pela metade não vai no save.
    /// </para>
    /// Na queda, o anfitrião confere pela floresta da semente (a árvore existe, está em pé e perto de quem cortou), corta,
    /// larga as toras no pé dela (de 2 a 5, pelo tamanho), dá o XP e faz barulho (a árvore caindo chama os zumbis de
    /// perto). Todos veem a árvore cair e virar toco (<see cref="FlorestaDoMundo"/>.Corte.cs).
    /// </summary>
    public partial class MundoDaPartida
    {
        NetworkList<long> _arvoresCortadas;

        // (no Awake: a lista da rede existe antes do spawn)
        void IniciarAsArvores()
        {
            _arvoresCortadas = new NetworkList<long>();
            _arvoresCortadas.OnListChanged += _ => FlorestaDoMundo.DefinirCortadas(Copia(_arvoresCortadas));
        }

        static List<long> Copia(NetworkList<long> l)
        {
            var c = new List<long>(l.Count);
            foreach (var k in l) c.Add(k);
            return c;
        }

        /// <summary>As árvores cortadas (o save).</summary>
        public List<long> ArvoresCortadas() => _arvoresCortadas != null ? Copia(_arvoresCortadas) : new List<long>();

        /// <summary>No anfitrião: as cortadas de um save.</summary>
        public void RestaurarArvores(IEnumerable<long> chaves)
        {
            if (!IsServer || _arvoresCortadas == null) return;
            _arvoresCortadas.Clear();
            if (chaves != null) foreach (var k in chaves) _arvoresCortadas.Add(k);
        }

        // no anfitrião: o quanto cada lado de cada árvore em pé já foi cortado (os 4 lados)
        readonly Dictionary<long, byte[]> _ladosCortados = new Dictionary<long, byte[]>();

        /// <summary>Os golpes do machado para um lado da árvore ir fundo: de 2 a 4, pelo tamanho dela.</summary>
        public static int GolpesPorLado(float escala) => Mathf.Clamp(Mathf.RoundToInt(1f + escala * 1.6f), 2, 4);

        /// <summary>No dono: um golpe do machado no lado 'lado' (0 a 3, de 90 em 90° a partir do norte) da árvore, a 'altura' m do pé.</summary>
        public void PedirGolpeNaArvore(long chave, byte lado, float altura)
        {
            if (IsSpawned) GolpeNaArvoreRpc(chave, lado, altura);
        }

        [Rpc(SendTo.Server)]
        void GolpeNaArvoreRpc(long chave, byte lado, float altura, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!ArvoreDoGolpe(chave, quem, out var j, out var planta)) return;
            lado &= 3;
            if (!_ladosCortados.TryGetValue(chave, out var lados))
            {
                if (_ladosCortados.Count > 128) _ladosCortados.Clear();
                lados = new byte[4];
                _ladosCortados[chave] = lados;
            }
            int precisa = GolpesPorLado(planta.Escala);
            bool fundo = lados[lado] >= precisa;
            if (!fundo) lados[lado]++;
            byte oposto = lados[(lado + 2) & 3];
            EntalheRpc(chave, lado, lados[lado], oposto, (byte)precisa, Mathf.Clamp(altura, 0.35f, 1.4f), quem, fundo);
            if (lados[lado] < precisa || oposto < precisa) return;
            // os dois lados opostos fundos: ela cai para o lado do primeiro corte, para longe deste (o último)
            _ladosCortados.Remove(chave);
            Vector3 deste = Quaternion.Euler(0f, lado * 90f, 0f) * Vector3.forward;
            Derrubar(chave, planta, j, planta.Posicao + deste * 3f);
        }

        // a árvore em pé desta chave, na floresta da semente, e quem bateu (perto dela)
        bool ArvoreDoGolpe(long chave, ulong quem, out JogadorRede j, out PlantaDaFloresta planta)
        {
            j = null;
            planta = default;
            if (_arvoresCortadas == null || _arvoresCortadas.Contains(chave) || Dados == null || Dados.Floresta == null) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return false;
            j = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (j == null) return false;
            FlorestaDoMundo.Abrir(chave, out int bx, out int bz, out int i);
            var lista = new List<PlantaDaFloresta>(256);
            Dados.Floresta.DoBloco(bx, bz, lista);
            if (i < 0 || i >= lista.Count || !Floresta.EhArvore(lista[i].Especie) || Floresta.Deitada(lista[i].Especie)) return false;
            planta = lista[i];
            var pe = planta.Posicao;
            var de = j.transform.position;
            return new Vector2(de.x - pe.x, de.z - pe.z).magnitude <= 4.5f;
        }

        // anfitrião → todos: o entalhe de um lado cresceu (ou já estava fundo); quem bateu vê o aviso
        [Rpc(SendTo.Everyone)]
        void EntalheRpc(long chave, byte lado, byte fundura, byte oposto, byte precisa, float altura, ulong quem, bool jaFundo)
        {
            FlorestaDoMundo.Instancia?.MarcarEntalhe(chave, lado, fundura / (float)Mathf.Max(1, (int)precisa), altura);
            if (NetworkManager == null || quem != NetworkManager.LocalClientId) return;
            if (fundura >= precisa && oposto >= precisa) return;   // (caiu: o aviso vem com a queda)
            if (jaFundo) InventarioJogador.AvisoLocal("Esse lado já está cortado: corte do lado oposto");
            else if (fundura >= precisa) InventarioJogador.AvisoLocal("Um lado cortado: agora corte o lado oposto (ela cai para o lado do primeiro corte)");
            else InventarioJogador.AvisoLocal($"Cortando a árvore: este lado {fundura} de {precisa}" + (oposto == 0 ? " (corte dois lados opostos para ela cair)" : ""));
        }

        // no anfitrião: a árvore cai (para longe de 'caiLongeDe'), e as toras ficam no pé dela, do lado de quem cortou
        void Derrubar(long chave, PlantaDaFloresta planta, JogadorRede j, Vector3 caiLongeDe)
        {
            var pe = planta.Posicao;
            var de = j.transform.position;
            _arvoresCortadas.Add(chave);
            // as toras no pé dela, do lado de quem cortou
            Vector3 lado = de - pe;
            lado.y = 0f;
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.forward;
            var onde = pe + lado * 1.2f;
            onde.y = Dados.Altura(onde.x, onde.z) + 0.05f;
            int toras = Mathf.Clamp(Mathf.RoundToInt(1.5f + planta.Escala * 1.5f), 2, 5);
            Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.Toras, onde, Random.Range(0f, 360f), toras);
            j.Inventario?.GanharXp(Habilidades.XpDaArvore, "árvore");
            Zombies.ZombieManager.Barulho(pe + Vector3.up * 2f, j, 30f);
            TombouRpc(chave, caiLongeDe, j.OwnerClientId);
        }

        // anfitrião → todos: a árvore cai (para longe de 'deQuem'), e os entalhes somem
        [Rpc(SendTo.Everyone)]
        void TombouRpc(long chave, Vector3 deQuem, ulong quem)
        {
            // (a lista da rede pode chegar depois deste aviso: a cortada entra na conta já)
            var f = FlorestaDoMundo.Instancia;
            f?.TirarEntalhes(chave);
            var cortadas = Copia(_arvoresCortadas);
            if (!cortadas.Contains(chave)) cortadas.Add(chave);
            FlorestaDoMundo.DefinirCortadas(cortadas);
            f?.Tombar(chave, deQuem);
            if (NetworkManager != null && quem == NetworkManager.LocalClientId) InventarioJogador.AvisoLocal("A árvore caiu: as toras ficaram no pé dela");
        }
    }
}
