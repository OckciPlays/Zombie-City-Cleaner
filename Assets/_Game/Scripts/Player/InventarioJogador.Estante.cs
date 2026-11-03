using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    // o que as estantes do bunker (World/EstantesDoBunker) pedem à mochila, no anfitrião
    public partial class InventarioJogador
    {
        /// <summary>
        /// Só no anfitrião: o item entra nesta célula da grade (girado ou não), com o que vai com ele (as balas, o desgaste
        /// e as cores da roupa): o que vem de uma estante. Falso se não cabe ali.
        /// </summary>
        public bool PorNaGrade(Item item, int x, int y, bool girado, int qtd, int desgaste, ulong cores)
        {
            if (!IsServer || item == Item.Nada || !Itens.NoJogo(item) || Itens.EhEquipamento(item) || !Cabe(item, girado, x, y)) return false;
            Mochila.Add(new ItemNaMochila { id = NovoId(), item = item, x = (byte)x, y = (byte)y, girado = girado, qtd = (ushort)Mathf.Max(0, qtd),
                                            desgaste = (byte)Mathf.Clamp(desgaste, 0, 100), cores = cores });
            return true;
        }

        /// <summary>Só no anfitrião: a pilha fica com esta quantidade (parte dela foi para uma estante).</summary>
        public void AcertarQuantidade(ushort id, int qtd)
        {
            int k = IsServer ? Indice(id) : -1;
            if (k < 0) return;
            if (qtd <= 0)
            {
                Mochila.RemoveAt(k);
                return;
            }
            var it = Mochila[k];
            it.qtd = (ushort)qtd;
            Mochila[k] = it;
        }
    }
}
