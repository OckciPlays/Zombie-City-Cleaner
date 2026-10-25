using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O capô do carro que se dirige: o colisor (um gatilho em cima do capô fechado) é o que a mira procura. E leva a
    /// cabeça para dentro do capô (<see cref="TelaDoCapo"/>), com as peças do motor do <see cref="Carro"/> em 3D; o capô
    /// sobe enquanto alguém olha.
    /// </summary>
    public class CapoDoCarro : Interagivel
    {
        public Carro carro;

        public override string Dica(ArmaJogador quem)
        {
            if (carro == null || !carro.Aberto) return null;
            var p = quem.GetComponent<PassageiroDoCarro>();
            if (p != null && p.NoCarro) return null;
            return "[E] Abrir o capô";
        }

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem.GetComponent<TelaDoCapo>();
            if (tela != null && carro != null) tela.Abrir(carro);
        }
    }
}
