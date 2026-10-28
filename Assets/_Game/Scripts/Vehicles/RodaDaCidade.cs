using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A roda de um carro da cidade: um gatilho do lado de fora dela (para fora da caixa de colisão do carro), o que a
    /// mira procura. Como na perua (<see cref="RodaDoCarro"/>): com o macaco na mochila, E apoia o macaco ao lado da
    /// roda; apoiado, o clique esquerdo na roda dele levanta aquele canto (e baixa); erguido, E tira a roda do macaco (vai
    /// para a mochila ou para as mãos, com o estado do pneu) ou põe um pneu na roda vazia. Pegar o macaco é no gatilho
    /// dele (<see cref="MacacoDaCidade"/>).
    /// O que muda fica no <see cref="PecasDosCarros"/>, pelo carro (<see cref="CapoDaCidade"/>).
    /// </summary>
    public class RodaDaCidade : Interagivel
    {
        public CapoDaCidade capo;
        [Tooltip("A roda deste gatilho: 0 dianteira esquerda, 1 dianteira direita, 2 e 3 atrás.")]
        public int roda;

        FonteDasPecas Pecas => capo != null ? capo.pecas : null;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            var inv = quem.Inventario;
            var pneus = capo.Pneus;
            int m = capo.MacacoNaRoda;
            if (m < 0)
            {
                if (!PortaMalasDoCarro.Achar(inv, Item.Macaco, out _))
                    return pneus.Tem(roda) ? "Para tirar a roda, você precisa de um macaco" : null;
                return "[E] Apoiar o macaco";
            }
            if (!capo.NaRodaDoMacaco(roda)) return null;
            if (capo.MacacoMexendo) return capo.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            if (!capo.Erguido) return "[Clique] Levantar o carro";
            if (pneus.Tem(roda))
            {
                string dica = $"[E] Tirar a roda ({pneus.Estado(roda)}%)  ·  [Clique] Baixar";
                return !inv.PodeReceber(Item.Pneu, out _) ? dica + " · sem espaço: fica no chão"
                    : inv.PneuVaiParaAsMaos ? dica + " · nas mãos (pesado)" : dica;
            }
            if (PortaMalasDoCarro.AcharPneu(inv, out int estado, out _)) return $"[E] Pôr o pneu ({estado}%)  ·  [Clique] Baixar";
            return "Sem pneu na mochila  ·  [Clique] Baixar";
        }

        public override bool UsaClique(ArmaJogador quem) => Pode(quem) && capo.NaRodaDoMacaco(roda);

        public override void Clicar(ArmaJogador quem)
        {
            if (!Pode(quem) || !capo.NaRodaDoMacaco(roda) || capo.MacacoMexendo) return;
            Pecas.PedirErguer(capo.indice, !capo.Erguido);
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem)) return;
            var inv = quem.Inventario;
            int m = capo.MacacoNaRoda;
            if (m < 0)
            {
                if (PortaMalasDoCarro.Achar(inv, Item.Macaco, out _)) Pecas.PedirApoiarMacaco(capo.indice, roda);
                return;
            }
            if (!capo.NaRodaDoMacaco(roda) || capo.MacacoMexendo || !capo.Erguido) return;
            if (capo.Pneus.Tem(roda)) Pecas.PedirTirarRoda(capo.indice, roda);
            else if (PortaMalasDoCarro.AcharPneu(inv, out _, out ushort id)) Pecas.PedirPorRoda(capo.indice, roda, id);
        }

        bool Pode(ArmaJogador quem)
        {
            if (capo == null || Pecas == null || !Pecas.IsSpawned || quem == null || quem.Inventario == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
