using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>O fogo na tela da mochila (11/11/2026, a etapa 4 do ramo Fogo): reabastecer o lampião e as linhas dos detalhes.</summary>
    public partial class TelaDoInventario
    {
        // o menu do botão direito: o lampião gasto reabastece com 1 combustível
        void OpcoesDoFogo(ItemNaMochila it)
        {
            if (it.item != Item.Lampiao || it.desgaste <= 0) return;
            ushort id = it.id;
            bool pode = inventario.PodeReabastecer(id);
            Opcao(pode ? $"Reabastecer ({100 - it.desgaste}% → 100%, gasta {InventarioJogador.NomeDoCombustivel})"
                       : "Reabastecer (precisa de álcool ou de gasolina num galão)",
                  pode ? () => inventario.Reabastecer(id) : (System.Action)null);
        }

        // os detalhes: o combustível do lampião e a lenha do kit da fogueira portátil
        static void LinhasDoFogo(ItemNaMochila it, System.Action<string, string> Linha)
        {
            if (it.item == Item.Lampiao)
            {
                Linha("Combustível", it.desgaste >= 100 ? "acabou (reabasteça)" : $"{100 - it.desgaste}% ({Mathf.CeilToInt(FogoDoJogador.MinutosDoLampiao * (100 - it.desgaste) / 100f)} min aceso)");
                Linha("Luz", $"{FogoDoJogador.LuzDoLampiao:0} m, só aceso na mão (a chuva não apaga)");
            }
            else if (it.item == Item.KitFogueira)
                Linha("Lenha", $"{Mathf.CeilToInt(FogoDoJogador.MinutosDaFogueiraPortatil * (100 - it.desgaste) / 100f)} min de fogo");
            else if (it.item == Item.Molotov)
                Linha("Fogo", $"{FogoDoJogador.RaioDoMolotov:0.#} m de raio por {FogoDoJogador.SegundosDoMolotov:0} s (o Fogo que pega aumenta)");
        }
    }
}
