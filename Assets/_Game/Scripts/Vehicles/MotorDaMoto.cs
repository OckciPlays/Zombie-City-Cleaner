using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O motor da moto (06/11/2026, a pedido: "para arrumar o motor a gente precisa agachar e ir em direção ao motor. Na
    /// moto, apenas a vela estará comprometida"). O colisor (um gatilho em volta do motor, embaixo do tanque) é o que a
    /// mira procura. Em pé, a dica manda agachar (C); agachado, E leva a cabeça até o lado do motor
    /// (<see cref="TelaDoCapo"/>, no cofre da moto, que só tem o lugar da vela), com a moto parada e ninguém nela.
    /// </summary>
    public class MotorDaMoto : Interagivel
    {
        public Carro carro;

        public override string Dica(ArmaJogador quem)
        {
            if (carro == null || !carro.IsSpawned || carro.Virado || carro.Velocidade > 1.5f) return null;
            var p = quem.GetComponent<PassageiroDoCarro>();
            if (p != null && p.NoCarro) return null;
            if (!carro.Vazio) return null;
            if (!Agachado(quem)) return "Agache (C) para mexer no motor";
            return "[E] Ver o motor (a vela)";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (carro == null || !Agachado(quem) || !carro.Aberto) return;
            var tela = quem.GetComponent<TelaDoCapo>();
            if (tela != null) tela.Abrir(carro);
        }

        static bool Agachado(ArmaJogador quem)
        {
            var m = quem.GetComponent<MovimentoPrimeiraPessoa>();
            return m != null && m.Agachado;
        }
    }
}
