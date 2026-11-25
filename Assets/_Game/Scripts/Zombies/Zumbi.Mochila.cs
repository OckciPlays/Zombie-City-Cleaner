using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis de mochila (24/11/2026, a pedido: "também seria interessante colocar alguns zumbis com mochilas, como se
    /// fossem sobreviventes que viraram zumbi"): uns 8% dos Ocos levam uma mochila nas costas (a pequena, a média ou, rara, a
    /// grande), sorteada pelo Id e pela geração (igual em todas as máquinas, sem nada a mais na rede). Ela fica presa no peito
    /// (<see cref="EquipamentoNoCorpo"/>, como a dos jogadores), aparece e some com o corpo do zumbi, e, quando ele morre,
    /// cai do lado do corpo, cheia (o recipiente do corpo: <see cref="SaqueDosZumbis"/>).
    /// </summary>
    public partial class Zumbi
    {
        public const float ChanceDaMochila = 0.08f;

        /// <summary>A mochila nas costas deste zumbi (Nada: sem mochila).</summary>
        public Item MochilaNasCostas { get; private set; }

        EquipamentoNoCorpo _equipamento;

        /// <summary>O teste do F1: os próximos N Ocos do anfitrião nascem com a mochila média (só no anfitrião: a cópia não sabe).</summary>
        public static int ForcarMochilas;

        Item SortearMochila()
        {
            if (tipo != TipoDeZumbi.Oco) return Item.Nada;
            if (!Copia && ForcarMochilas > 0)
            {
                ForcarMochilas--;
                return Item.MochilaMedia;
            }
            uint h = Misturar((uint)Id, Geracao, (uint)modelo);
            if ((h & 0xFFFF) / 65535f >= ChanceDaMochila) return Item.Nada;
            float qual = ((h >> 16) & 0xFFFF) / 65535f;
            return qual < 0.6f ? Item.MochilaPequena : qual < 0.9f ? Item.MochilaMedia : Item.MochilaGrande;
        }

        static uint Misturar(uint a, uint b, uint c)
        {
            uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u ^ (c + 0x165667B1u) * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        // nasceu (no anfitrião ou na cópia): a mochila do sorteio, presa no peito
        void VestirAMochila()
        {
            MochilaNasCostas = SortearMochila();
            if (MochilaNasCostas == Item.Nada)
            {
                if (_equipamento != null) _equipamento.Limpar();
                return;
            }
            // o corpo em repouso: o do modelo deste zumbi (o prefab, sem animação)
            var molde = _m != null && _m.modelos != null && modelo >= 0 && modelo < _m.modelos.Length ? _m.modelos[modelo] : null;
            var referencia = molde != null ? molde.raizDoModelo : null;
            if (referencia == null || raizDoModelo == null) return;
            if (_equipamento == null) _equipamento = raizDoModelo.gameObject.AddComponent<EquipamentoNoCorpo>();
            _equipamento.Vestir(referencia, MochilaNasCostas, null);
            AtualizarRenderers();
        }

        // de volta ao pool
        void TirarAMochila()
        {
            MochilaNasCostas = Item.Nada;
            if (_equipamento != null) _equipamento.Limpar();
        }

        // a mochila aparece e some com o corpo (AtualizarRenderers)
        void MostrarAMochila(bool ligado)
        {
            if (_equipamento == null) return;
            foreach (var p in _equipamento.Pecas)
                if (p != null && p.TryGetComponent<Renderer>(out var r)) r.enabled = ligado;
        }
    }
}
