using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O macaco apoiado embaixo do carro: um gatilho em volta dele, que passa para fora da lateral (o que a mira procura;
    /// o <see cref="Carro"/> o liga só com o macaco apoiado e o estica com ele). Com o carro baixado, E pega o macaco de
    /// volta; erguido, E baixa o carro.
    /// </summary>
    public class MacacoApoiado : Interagivel
    {
        public Carro carro;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (carro.MacacoMexendo) return carro.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            return carro.Erguido ? "[E] Baixar o carro" : "[E] Pegar o macaco";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem) || carro.MacacoMexendo) return;
            if (carro.Erguido) carro.PedirErguer(false);
            else carro.PedirTirarMacaco();
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.MacacoNaRoda < 0 || quem == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
