using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As árvores cortadas do mundo (06/11/2026, o machado): a lista da rede (escrita pelo anfitrião, no save do mundo). O
    /// dono dá os golpes (5 com o machado) e pede o corte (<see cref="PedirCorte"/>); o anfitrião confere pela floresta da
    /// semente (a árvore existe, está em pé e perto de quem pediu), corta, larga as toras no pé dela (de 2 a 5, pelo
    /// tamanho), dá o XP e faz barulho (a árvore caindo chama os zumbis de perto). Todos veem a árvore cair e virar toco
    /// (<see cref="FlorestaDoMundo"/>.Corte.cs).
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

        /// <summary>No dono: a árvore (pela chave) levou o último golpe.</summary>
        public void PedirCorte(long chave)
        {
            if (IsSpawned) CortarRpc(chave);
        }

        [Rpc(SendTo.Server)]
        void CortarRpc(long chave, RpcParams parametros = default)
        {
            if (_arvoresCortadas == null || _arvoresCortadas.Contains(chave) || Dados == null || Dados.Floresta == null) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            var j = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (j == null) return;
            FlorestaDoMundo.Abrir(chave, out int bx, out int bz, out int i);
            var lista = new List<PlantaDaFloresta>(256);
            Dados.Floresta.DoBloco(bx, bz, lista);
            if (i < 0 || i >= lista.Count || !Floresta.EhArvore(lista[i].Especie) || Floresta.Deitada(lista[i].Especie)) return;
            var pe = lista[i].Posicao;
            var de = j.transform.position;
            if (new Vector2(de.x - pe.x, de.z - pe.z).magnitude > 4.5f) return;
            _arvoresCortadas.Add(chave);
            // as toras no pé dela, do lado de quem cortou
            Vector3 lado = de - pe;
            lado.y = 0f;
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.forward;
            var onde = pe + lado * 1.2f;
            onde.y = Dados.Altura(onde.x, onde.z) + 0.05f;
            int toras = Mathf.Clamp(Mathf.RoundToInt(1.5f + lista[i].Escala * 1.5f), 2, 5);
            Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.Toras, onde, Random.Range(0f, 360f), toras);
            j.Inventario?.GanharXp(Habilidades.XpDaArvore, "árvore");
            Zombies.ZombieManager.Barulho(pe + Vector3.up * 2f, j, 30f);
            TombouRpc(chave, de);
        }

        // anfitrião → todos: a árvore cai (para longe de quem cortou)
        [Rpc(SendTo.Everyone)]
        void TombouRpc(long chave, Vector3 deQuem)
        {
            // (a lista da rede pode chegar depois deste aviso: a cortada entra na conta já)
            var cortadas = Copia(_arvoresCortadas);
            if (!cortadas.Contains(chave)) cortadas.Add(chave);
            FlorestaDoMundo.DefinirCortadas(cortadas);
            FlorestaDoMundo.Instancia?.Tombar(chave, deQuem);
        }
    }
}
