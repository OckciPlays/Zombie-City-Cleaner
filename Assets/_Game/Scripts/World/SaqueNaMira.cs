using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um item do saque num móvel de perto (o Plano_Saque.md da pasta de arte, a parte 4; <see cref="MobiliaNovaDoMundo"/>):
    /// a mira acha o colisor dele (um gatilho do tamanho do item), e o E pede ao anfitrião para pegar. O de dentro (atrás
    /// de uma porta, numa gaveta, debaixo de uma tampa) só se pega com a parte aberta. A dica tem o nome na cor da raridade.
    /// </summary>
    public class SaqueNaMira : Interagivel
    {
        /// <summary>A chave dele na lista dos pegos (<see cref="MobiliaNovaDoMundo.ChaveDoSaque"/>).</summary>
        public ulong chave;
        /// <summary>Onde ele está: a obra, o ponto do móvel, a peça e o lugar (o anfitrião acha o item por eles).</summary>
        public ulong obra;
        public int ponto, peca, lugar;
        public Item item;
        /// <summary>O número da dica: as balas (da caixa ou do pente), a pilha; -1, nenhum.</summary>
        public int qtd = -1;
        public Raridade raridade;
        /// <summary>A parte que precisa estar aberta (null: à vista).</summary>
        public ParteQueAbre porta;
        /// <summary>O móvel e a caixa dele (nas coordenadas dele, fechado): a mira passa pela colisão dele até o item.</summary>
        public Transform movel;
        public Bounds caixaDoMovel;

        string _dica, _dicaComCor;

        public override string Dica(ArmaJogador quem)
        {
            var inventario = quem.Inventario;
            if (inventario == null || (porta != null && !porta.Aberta) || MobiliaNovaDoMundo.Pego(chave)) return null;
            // o nome do item na cor da raridade (a dica muda com a mochila: só é refeita quando muda)
            var d = inventario.DicaDePegar(item, qtd);
            if (d != _dica)
            {
                _dica = d;
                string nome = Itens.Nome(item);
                int k = d.IndexOf(nome, System.StringComparison.Ordinal);
                _dicaComCor = k < 0 ? d : d.Substring(0, k) + $"<color={CatalogoDoSaque.CorEmHex(raridade)}>{nome}</color>" + d.Substring(k + nome.Length);
            }
            return _dicaComCor;
        }

        public override void Usar(ArmaJogador quem) => MobiliaNovaDoMundo.Instancia?.PedirPegar(this, quem.Inventario);

        // a caixa de colisão do móvel (na obra) e a parte aberta dele não tapam o item
        public override bool GanhaDe(Vector3 ponto, Interagivel antes)
        {
            if (movel == null) return false;
            if (antes != null) return antes is ParteDoMovel && antes.transform.IsChildOf(movel);
            var b = caixaDoMovel;
            b.Expand(0.06f);
            return b.Contains(movel.InverseTransformPoint(ponto));
        }
    }
}
