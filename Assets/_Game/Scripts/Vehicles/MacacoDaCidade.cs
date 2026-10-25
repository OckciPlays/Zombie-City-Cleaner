using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O macaco apoiado embaixo de um carro da cidade: o gatilho dele (o <see cref="CapoDaCidade"/> o liga só com o
    /// macaco apoiado e o estica com ele). Como na perua (<see cref="MacacoApoiado"/>): com o carro baixado, E pega o
    /// macaco de volta; erguido, E baixa o carro.
    /// </summary>
    public class MacacoDaCidade : Interagivel
    {
        public CapoDaCidade capo;

        public override string Dica(ArmaJogador quem)
        {
            if (!Pode(quem)) return null;
            if (capo.MacacoMexendo) return capo.Erguido ? "Levantando o carro..." : "Baixando o carro...";
            return capo.Erguido ? "[E] Baixar o carro" : "[E] Pegar o macaco";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (!Pode(quem) || capo.MacacoMexendo) return;
            if (capo.Erguido) capo.pecas.PedirErguer(capo.indice, false);
            else capo.pecas.PedirTirarMacaco(capo.indice);
        }

        bool Pode(ArmaJogador quem)
        {
            if (capo == null || capo.pecas == null || !capo.pecas.IsSpawned || capo.MacacoNaRoda < 0 || quem == null) return false;
            var p = quem.GetComponent<PassageiroDoCarro>();
            return p == null || !p.NoCarro;
        }
    }
}
