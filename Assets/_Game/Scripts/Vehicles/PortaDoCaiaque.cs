using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O E no casco do caiaque (06/11/2026): entra no lugar livre (o de remar primeiro; o da frente, para pescar). Sentado,
    /// o E sai (<see cref="PassageiroDoCaiaque"/>), e o casco não oferece nada.
    /// </summary>
    public class PortaDoCaiaque : Interagivel
    {
        public Caiaque caiaque;

        public override string Dica(ArmaJogador quem)
        {
            if (caiaque == null || !caiaque.IsSpawned) return null;
            var p = quem.GetComponent<PassageiroDoCaiaque>();
            if (p != null && p.NoCaiaque) return null;
            int l = caiaque.LugarLivre();
            if (l < 0) return "O caiaque está cheio";
            return l == 0 ? "[E] Entrar no caiaque (atrás: remar)" : "[E] Entrar no caiaque (na frente: pescar)";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (caiaque == null) return;
            var p = quem.GetComponent<PassageiroDoCaiaque>();
            if (p != null && p.NoCaiaque) return;
            int l = caiaque.LugarLivre();
            if (l >= 0) caiaque.PedirEntrar(l);
        }
    }
}
