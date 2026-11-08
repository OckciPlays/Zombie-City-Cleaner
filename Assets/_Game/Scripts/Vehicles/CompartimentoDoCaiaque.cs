using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A escotilha de trás do caiaque (07/11/2026, a pedido: "um compartimento pequeno no caiaque"): de fora, o E abre o
    /// inventário com o compartimento ao lado (uma estante de 2 × 2, <see cref="EstantesDoBunker.BaseDosCaiaques"/>).
    /// Sentado nele, o TAB abre o mesmo (o E sai do caiaque). Posta pelo <see cref="Caiaque"/>, numa caixa-gatilho em cima da
    /// escotilha.
    /// </summary>
    public class CompartimentoDoCaiaque : Interagivel
    {
        public Caiaque caiaque;

        public override string Dica(ArmaJogador quem)
        {
            if (caiaque == null || !caiaque.IsSpawned || EstantesDoBunker.Instancia == null) return null;
            if (PassageiroDoCaiaque.Sentado(quem)) return null;
            int n = 0;
            foreach (var it in EstantesDoBunker.Instancia.Guardados) if (it.estante == caiaque.Compartimento) n++;
            return n == 0 ? "[E] Abrir o compartimento (vazio)" : $"[E] Abrir o compartimento ({n} {(n == 1 ? "coisa" : "coisas")})";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (caiaque == null || PassageiroDoCaiaque.Sentado(quem)) return;
            quem.GetComponent<TelaDoInventario>()?.AbrirEstante(caiaque.Compartimento);
        }
    }
}
