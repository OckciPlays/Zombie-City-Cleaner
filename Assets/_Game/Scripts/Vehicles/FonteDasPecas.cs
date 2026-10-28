using Unity.Netcode;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// Quem guarda as peças, as rodas e o macaco dos carros abandonados, carro por carro (pelo número dele): o
    /// <see cref="PecasDosCarros"/> na Demo_Cidade (os carros montados na cena) e o <see cref="PecasDosCarrosDoMundo"/>
    /// no mundo gerado (os carros que nascem com os blocos). O <see cref="CapoDaCidade"/>, a <see cref="RodaDaCidade"/> e o
    /// <see cref="MacacoDaCidade"/> só falam com esta base.
    /// </summary>
    public abstract class FonteDasPecas : NetworkBehaviour
    {
        /// <summary>As 4 rodas do carro.</summary>
        public abstract PneusDoCarro PneusDo(int carro);
        /// <summary>A roda ao lado da qual o macaco está apoiado neste carro, ou -1.</summary>
        public abstract int MacacoDo(int carro);
        /// <summary>O macaco deste carro está levantado.</summary>
        public abstract bool ErguidoDo(int carro);
        /// <summary>As peças do motor do carro.</summary>
        public abstract PecasDoMotor Do(int carro);
        /// <summary>Alguém está olhando o motor deste carro (o capô fica aberto).</summary>
        public abstract bool EstaAberto(int carro);

        /// <summary>O jogador local começou ou parou de olhar o motor do carro.</summary>
        public abstract void Olhar(int carro, bool olhando);
        public abstract void PedirTirar(int carro, int peca);
        public abstract void PedirPor(int carro, int peca, ushort idNaMochila);
        public abstract void PedirApoiarMacaco(int carro, int roda);
        public abstract void PedirErguer(int carro, bool erguer);
        public abstract void PedirTirarMacaco(int carro);
        public abstract void PedirTirarRoda(int carro, int roda);
        /// <summary>Um pneu da mochila (pelo Id; 0, o das mãos) vai para a roda vazia do lado erguido.</summary>
        public abstract void PedirPorRoda(int carro, int roda, ushort idNaMochila);
    }
}
