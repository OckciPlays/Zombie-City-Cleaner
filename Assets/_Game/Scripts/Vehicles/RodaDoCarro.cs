using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A troca de roda com o macaco: um gatilho do lado de fora de cada roda, do tamanho dela (para fora da caixa de
    /// colisão de baixo, sem encostar nos das portas), o que a mira procura. Com o macaco na mochila e o carro vazio e
    /// parado, E apoia o macaco ao lado da roda; apoiado, E na roda dele levanta o carro. Erguido, E tira a roda do lado
    /// erguido (vai para a mochila, ou para o chão, com o estado do pneu) ou põe o melhor pneu da mochila no lugar vazio.
    /// Baixar o carro e pegar o macaco de volta é no gatilho do próprio macaco (<see cref="MacacoApoiado"/>).
    /// </summary>
    public class RodaDoCarro : Interagivel
    {
        public Carro carro;
        [Tooltip("A roda deste gatilho, na ordem dos WheelColliders (0 dianteira esquerda, 1 dianteira direita, 2 e 3 atrás).")]
        public int roda;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            var inv = quem.Inventario;
            var pneus = carro.Pneus;
            int m = carro.MacacoNaRoda;
            if (m < 0)
            {
                bool precisa = !pneus.Tem(roda) || pneus.Furado(roda);
                if (!PortaMalasDoCarro.Achar(inv, Item.Macaco, out _))
                    return precisa ? "Para trocar a roda, pegue o macaco no porta-malas" : null;
                if (carro.Virado || carro.Velocidade > Carro.VelocidadeParaOMacaco) return null;
                if (!carro.Vazio) return "Saiam do carro para apoiar o macaco";
                return "[E] Apoiar o macaco";
            }
            if (!carro.NoLadoErguido(roda)) return null;
            if (carro.MacacoMexendo) return carro.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            if (!carro.Erguido) return roda == m ? "[E] Levantar o carro" : null;
            if (pneus.Tem(roda))
            {
                string dica = $"[E] Tirar a roda ({pneus.Estado(roda)}%)";
                return !inv.PodeReceber(Item.Pneu, out _) ? dica + " · sem espaço: fica no chão"
                    : inv.PneuVaiParaAsMaos ? dica + " · nas mãos (pesado)" : dica;
            }
            if (PortaMalasDoCarro.AcharPneu(inv, out int estado, out _)) return $"[E] Pôr o pneu ({estado}%)";
            return carro.Estepe >= 0 ? "Sem pneu na mochila: o estepe está no porta-malas" : "Sem pneu na mochila";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem)) return;
            var inv = quem.Inventario;
            int m = carro.MacacoNaRoda;
            if (m < 0)
            {
                if (PortaMalasDoCarro.Achar(inv, Item.Macaco, out _) && carro.Vazio) carro.PedirApoiarMacaco(roda);
                return;
            }
            if (!carro.NoLadoErguido(roda) || carro.MacacoMexendo) return;
            if (!carro.Erguido)
            {
                if (roda == m) carro.PedirErguer(true);
                return;
            }
            if (carro.Pneus.Tem(roda)) carro.PedirTirarRoda(roda);
            else if (PortaMalasDoCarro.AcharPneu(inv, out _, out ushort id)) carro.PedirPorRoda(roda, id);
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || quem == null || quem.Inventario == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
