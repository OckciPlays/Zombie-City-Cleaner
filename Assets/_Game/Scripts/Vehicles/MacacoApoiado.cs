using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O macaco apoiado embaixo do carro: um gatilho em volta dele, que passa para fora da lateral (o que a mira procura;
    /// o <see cref="Carro"/> o liga só com o macaco apoiado e o estica com ele). O clique esquerdo sobe e desce o carro;
    /// com o carro baixado, E pega o macaco de volta (E não mexe no carro: assim ninguém o levanta sem querer).
    /// </summary>
    public class MacacoApoiado : Interagivel
    {
        public Carro carro;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (carro.MacacoMexendo) return carro.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            return carro.Erguido ? "[Clique] Baixar o carro" : "[Clique] Levantar o carro  ·  [E] Pegar o macaco";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem) || carro.MacacoMexendo || carro.Erguido) return;
            carro.PedirTirarMacaco();
        }

        public override bool UsaClique(ArmaJogador quem) => Pode(quem);

        public override void Clicar(ArmaJogador quem)
        {
            if (!Pode(quem) || carro.MacacoMexendo) return;
            carro.PedirErguer(!carro.Erguido);
        }

        bool Pode(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.MacacoNaRoda < 0 || quem == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
