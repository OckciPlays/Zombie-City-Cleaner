using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O gatilho em volta da mesa de sinuca (um pouco maior que ela: a mira acha ele antes do colisor da mesa), e E usa a
    /// mesa (<see cref="MesaDeSinuca.Dica"/> e <see cref="MesaDeSinuca.Usar"/>).
    /// </summary>
    public class MesaDeSinucaNaMira : Interagivel
    {
        public MesaDeSinuca mesa;

        public override string Dica(ArmaJogador quem) => mesa != null ? mesa.Dica(quem) : null;

        public override void Usar(ArmaJogador quem)
        {
            if (mesa != null) mesa.Usar(quem);
        }
    }
}
