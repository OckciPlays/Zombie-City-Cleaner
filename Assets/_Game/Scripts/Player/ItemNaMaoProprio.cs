using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os comestíveis, as bebidas e os remédios do saque (31/10/2026) vão na mão com os braços da lata ou da garrafa, com a
    /// malha deles no lugar. A montagem do prefab (MontagemDaRede) põe este componente nos braços de primeira pessoa da lata
    /// e da garrafa e no item do corpo (Arma_Lata, Arma_Garrafa), com o lugar do item (<see cref="lugar"/>: a origem e o giro
    /// da lata, preso ao osso que a segura) e, nos braços, a malha sem a lata. Aqui, a malha do item entra e a lata sai (ou
    /// volta, com a lata ou a garrafa de água).
    /// </summary>
    public class ItemNaMaoProprio : MonoBehaviour
    {
        [Tooltip("Primeira pessoa: os braços (a malha com a lata) e a malha deles sem ela.")]
        public SkinnedMeshRenderer bracos;
        public Mesh comLata, semLata;
        [Tooltip("O índice da lata nas partes da malha dos braços (e o material dela).")]
        public int indiceDaLata = -1;
        public Material materialDaLata;
        [Tooltip("No corpo: a malha da lata (some com o item do saque na mão).")]
        public MeshFilter lata;
        public Mesh malhaDaLata;
        [Tooltip("O lugar do item, na pose da lata (desligado com a lata na mão).")]
        public GameObject lugar;

        bool _semLata;

        /// <summary>Mostra na mão o item: a lata (ou a garrafa) dos braços, ou a malha dele no lugar dela.</summary>
        public static void Aplicar(GameObject modelo, Item item)
        {
            var p = modelo != null ? modelo.GetComponent<ItemNaMaoProprio>() : null;
            if (p == null) return;
            var mundo = ItensNoMundo.Instancia;
            p.Mostrar(Itens.NaMaoComMalhaPropria(item) && mundo != null ? mundo.MalhaDoLoot(item) : null);
        }

        /// <summary>A malha do item no lugar da lata (null: a lata de volta).</summary>
        public void Mostrar(Mesh malha)
        {
            bool proprio = malha != null;
            if (lugar != null)
            {
                var filtro = lugar.GetComponent<MeshFilter>();
                if (filtro != null) filtro.sharedMesh = malha;
                lugar.SetActive(proprio);
            }
            if (lata != null) lata.sharedMesh = proprio ? null : malhaDaLata;
            if (bracos != null && semLata != null && indiceDaLata >= 0 && proprio != _semLata)
            {
                // a pele dos braços (o material 0) pode ter sido pintada (a cor do personagem): só a lata sai ou volta
                var mats = new List<Material>(bracos.sharedMaterials);
                if (proprio && indiceDaLata < mats.Count) mats.RemoveAt(indiceDaLata);
                else if (!proprio) mats.Insert(Mathf.Min(indiceDaLata, mats.Count), materialDaLata);
                bracos.sharedMesh = proprio ? semLata : comLata;
                bracos.sharedMaterials = mats.ToArray();
                _semLata = proprio;
            }
        }
    }
}
