using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O macaco apoiado embaixo de um carro da cidade: o gatilho dele (o <see cref="CapoDaCidade"/> o liga só com o
    /// macaco apoiado e o estica com ele). Como na perua (<see cref="MacacoApoiado"/>): o clique esquerdo sobe e desce o
    /// carro; com o carro baixado, E pega o macaco de volta (E não mexe no carro: assim ninguém o levanta sem querer).
    /// O mundo gerado usa o mesmo (os carros com o kit).
    /// </summary>
    public class MacacoDaCidade : Interagivel
    {
        public CapoDaCidade capo;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (capo.MacacoMexendo) return capo.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            return capo.Erguido ? "[Clique] Baixar o carro" : "[Clique] Levantar o carro  ·  [E] Pegar o macaco";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem) || capo.MacacoMexendo || capo.Erguido) return;
            capo.pecas.PedirTirarMacaco(capo.indice);
        }

        public override bool UsaClique(ArmaJogador quem) => Pode(quem);

        public override void Clicar(ArmaJogador quem)
        {
            if (!Pode(quem) || capo.MacacoMexendo) return;
            capo.pecas.PedirErguer(capo.indice, !capo.Erguido);
        }

        bool Pode(ArmaJogador quem)
        {
            if (capo == null || capo.pecas == null || !capo.pecas.IsSpawned || capo.MacacoNaRoda < 0 || quem == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
