using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A tampa do tanque do carro (01/11/2026, a pedido: a gasolina e o galão): um gatilho do lado de fora da lataria, o
    /// que a mira procura (o Carro o cria no Awake, em <see cref="Carro.tampaDoTanque"/>). Mostra quanto há no tanque; com
    /// um galão com gasolina na mochila, E despeja o que couber (o galão mais cheio primeiro).
    /// </summary>
    public class TanqueDoCarro : Interagivel
    {
        public Carro carro;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            string tanque = $"tanque: {Litros(carro.Gasolina)} de {carro.Tanque:0} L";
            if (!AcharGalao(quem.Inventario, out _, out float litros))
                return $"Gasolina ({tanque}) · precisa de um galão com gasolina";
            if (carro.Gasolina >= carro.Tanque - 0.05f) return $"O tanque está cheio ({carro.Tanque:0} L)";
            return $"[E] Abastecer com o galão ({Litros(litros)} L) · {tanque}";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem) || !AcharGalao(quem.Inventario, out ushort id, out _)) return;
            if (carro.Gasolina >= carro.Tanque - 0.05f) return;
            carro.PedirAbastecer(id);
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.Virado || quem == null || quem.Inventario == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }

        /// <summary>O galão mais cheio da mochila (com alguma gasolina).</summary>
        static bool AcharGalao(InventarioJogador inv, out ushort id, out float litros)
        {
            id = 0;
            litros = 0f;
            if (inv == null || inv.Mochila == null) return false;
            foreach (var it in inv.Mochila)
            {
                if (it.item != Item.Galao) continue;
                float l = InventarioJogador.LitrosDe(it);
                if (l > litros + 0.001f)
                {
                    litros = l;
                    id = it.id;
                }
            }
            return id != 0 && litros > 0.05f;
        }

        static string Litros(float l) => l < 10f ? l.ToString("0.#") : l.ToString("0");
    }
}
