using UnityEngine;
using ZombieCityCleaner.Loot;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;

namespace ZombieCityCleaner.Player
{
    /// <summary>As armadilhas na tela da mochila (11/11/2026, a etapa 5a): as linhas dos detalhes (o que resta e o que fazem).</summary>
    public partial class TelaDoInventario
    {
        // os detalhes: o que resta do kit (o desgaste guarda o que já foi) no nível do Armadilheiro de quem olha
        void LinhasDasArmadilhas(ItemNaMochila it, System.Action<string, string> Linha)
        {
            int nivel = A.NivelDe(GetComponent<JogadorRede>());
            // as tábuas para barricada (11/11/2026, a etapa 5b): a vida pregada (o nível 2, o Armadilheiro e a Fortaleza de quem olha)
            if (it.item == Item.TabuasBarricada)
            {
                var j = GetComponent<JogadorRede>();
                bool reforcada = A.BarricadaDe(j) >= 2, fortaleza = A.FortalezaDe(j);
                Linha("Vida", $"a porta {A.VidaDaBarricadaDeTabuas(false, reforcada, nivel, fortaleza):0}, a janela {A.VidaDaBarricadaDeTabuas(true, reforcada, nivel, fortaleza):0}");
                Linha("Pregar", $"mire uma porta ou uma janela de casa e segure E ({A.SegundosDePregar:0} s; o martelo chama os zumbis a {A.BarulhoDoMartelo:0} m)");
                return;
            }
            if (!Itens.EhArmadilha(it.item)) return;
            float resto = (100 - Mathf.Clamp(it.desgaste, 0, 99)) / 100f;
            switch (it.item)
            {
                case Item.AlarmeLatas:
                    Linha("Toques", $"{Mathf.Max(1, Mathf.RoundToInt(A.ToquesDe(nivel) * resto))} (rearma em {A.RearmeDoAlarme:0} s)");
                    Linha("Barulho", $"os zumbis a {A.BarulhoDoAlarme:0} m vêm ver; o aviso chega a {A.AvisoDoAlarme:0} m");
                    break;
                case Item.TapetePregos:
                    Linha("Usos", $"{Mathf.Max(1, Mathf.RoundToInt(A.UsosDoTapeteDe(nivel) * resto))} (cada zumbi, morador ou pneu)");
                    Linha("Dano", $"{A.DanoDoTapete * A.FatorDeDano(nivel):0.#} (o jogador: {A.DanoDoTapeteNoJogador:0} na perna)");
                    break;
                case Item.Estacas:
                    Linha("Vida", $"{A.VidaDasEstacasDe(nivel):0}");
                    Linha("Dano", $"{A.DanoDasEstacas * A.FatorDeDano(nivel):0.#} por segundo, na frente");
                    break;
                case Item.CercaFogo:
                    Linha("Fogo", $"{A.ComprimentoDaCerca:0} m por {A.SegundosDaCercaDe(nivel):0} s (acende com a tocha ou o lampião)");
                    break;
            }
        }
    }
}
