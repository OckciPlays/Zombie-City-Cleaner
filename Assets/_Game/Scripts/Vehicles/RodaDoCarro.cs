using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A troca de roda com o macaco: um gatilho do lado de fora de cada roda (para fora da caixa de colisão de baixo), o
    /// que a mira procura. Com o macaco na mochila e o carro vazio e parado, E apoia o macaco ao lado da roda. Com ele
    /// apoiado, E levanta o carro (mirando no macaco, com o carro já baixado, E pega o macaco de volta). Erguido, E tira a
    /// roda do lado erguido (vai para a mochila, ou para o chão, com o estado do pneu) ou põe o melhor pneu da mochila no
    /// lugar vazio; mirando no macaco, E baixa o carro.
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
            if (carro.MacacoMexendo) return carro.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            bool noMacaco = NoMacaco(quem);
            if (!carro.Erguido)
            {
                if (noMacaco) return "[E] Pegar o macaco";
                return roda == m ? "[E] Levantar o carro" : null;
            }
            if (noMacaco || !carro.NoLadoErguido(roda)) return roda == m || noMacaco ? "[E] Baixar o carro" : null;
            if (pneus.Tem(roda))
            {
                string dica = $"[E] Tirar a roda ({pneus.Estado(roda)}%)";
                return inv.PodeReceber(Item.Pneu, out _) ? dica : dica + " · sem espaço: fica no chão";
            }
            if (PortaMalasDoCarro.Achar(inv, Item.Pneu, out var pneu)) return $"[E] Pôr o pneu ({pneu.qtd}%)";
            return carro.Estepe >= 0 ? "Sem pneu na mochila: o estepe está no porta-malas" : "Sem pneu na mochila";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem)) return;
            var inv = quem.Inventario;
            var pneus = carro.Pneus;
            int m = carro.MacacoNaRoda;
            if (m < 0)
            {
                if (PortaMalasDoCarro.Achar(inv, Item.Macaco, out _) && carro.Vazio) carro.PedirApoiarMacaco(roda);
                return;
            }
            if (carro.MacacoMexendo) return;
            bool noMacaco = NoMacaco(quem);
            if (!carro.Erguido)
            {
                if (noMacaco) carro.PedirTirarMacaco();
                else if (roda == m) carro.PedirErguer(true);
                return;
            }
            if (noMacaco || !carro.NoLadoErguido(roda))
            {
                if (roda == m || noMacaco) carro.PedirErguer(false);
                return;
            }
            if (pneus.Tem(roda)) carro.PedirTirarRoda(roda);
            else if (PortaMalasDoCarro.Achar(inv, Item.Pneu, out var pneu)) carro.PedirPorRoda(roda, pneu.id);
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || quem == null || quem.Inventario == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }

        // a mira passa pela caixa da malha do macaco apoiado
        bool NoMacaco(ArmaJogador quem)
        {
            var rede = quem.GetComponent<JogadorRede>();
            if (rede == null || rede.cameraMundo == null || carro.macacoApoiado == null || !carro.macacoApoiado.enabled) return false;
            var olho = rede.cameraMundo.transform;
            return PortaMalasDoCarro.Na(carro.macacoApoiado, new Ray(olho.position, olho.forward), out _);
        }
    }
}
