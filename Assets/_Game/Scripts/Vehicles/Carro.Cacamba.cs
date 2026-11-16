using UnityEngine;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A caçamba da caminhonete (15/11/2026, a pedido: "na cabine da frente vão ser apenas dois lugares que dá para sentar
    /// e na caçamba vão dar para sentar mais duas pessoas (dentro da caçamba, encostadas ali dentro)"; "pode fazer a caçamba
    /// aberta, sem a lona"):
    ///  - <b>o porta-malas é a caçamba aberta</b>: fica sempre aberta (não há tampa para abrir; a tampa de trás da
    ///    caminhonete é só desenho), e o estepe (de pé, do lado direito, logo atrás da cabine) e o macaco (deitado no piso) se pegam e se
    ///    guardam direto nela (<see cref="PortaMalasDoCarro"/>, sem o "abrir" e o "fechar");
    ///  - <b>os lugares da caçamba</b> (de <see cref="primeiroLugarNaCacamba"/> em diante): quem senta fica no piso,
    ///    encostado na frente da caçamba, olhando para trás, com os joelhos para cima (a pose do
    ///    <see cref="Player.PassageiroDoCarro"/>), e o teto da cabine não conta para ele. Sobe pela lateral da caçamba, atrás
    ///    da roda ("[E] Subir na caçamba", <see cref="PortaDoCarro.cacamba"/>), e atira e golpeia como os passageiros.
    /// </summary>
    public partial class Carro
    {
        [Header("A caçamba (a caminhonete, 15/11/2026)")]
        [Tooltip("O porta-malas é a caçamba aberta: sempre aberta, o estepe e o macaco se pegam direto dela.")]
        public bool cacamba;
        [Tooltip("Os lugares deste em diante ficam na caçamba (sentados no piso, encostados na frente dela).")]
        public int primeiroLugarNaCacamba = 2;
        [Tooltip("A altura do piso da caçamba (no espaço do carro): os pés de quem senta lá ficam nele.")]
        public float pisoDaCacamba = 0.84f;
        [Tooltip("Quanto o tronco de quem senta na caçamba deita para trás, encostado (graus).")]
        public float encostoNaCacamba = 14f;

        /// <summary>O lugar fica na caçamba (sem o teto, com a pose de sentado no piso).</summary>
        public bool NaCacamba(int lugar) => cacamba && lugar >= primeiroLugarNaCacamba && lugar < Lugares;
    }
}
