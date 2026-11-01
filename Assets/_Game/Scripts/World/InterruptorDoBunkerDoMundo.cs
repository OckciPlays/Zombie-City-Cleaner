using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O interruptor de um andar do bunker da família (na parede do núcleo, ao lado da porta da escada): E acende ou apaga
    /// as lâmpadas do andar, pelo anfitrião (<see cref="BunkerDoMundo.PedirLuz"/>). A alavanca fica para cima com a luz
    /// acesa e para baixo com ela apagada.
    /// </summary>
    public class InterruptorDoBunkerDoMundo : Interagivel
    {
        public BunkerDoMundo bunker;
        public int andar;
        public Transform alavanca;

        public override string Dica(ArmaJogador quem) =>
            bunker == null ? null : $"{(bunker.Acesa(andar) ? "[E] Apagar" : "[E] Acender")} a luz do {PlantaDeBunker.Codigos[andar]}";

        public override void Usar(ArmaJogador quem) => bunker?.PedirLuz(andar);

        public void Mostrar(bool acesa)
        {
            if (alavanca != null) alavanca.localRotation = Quaternion.Euler(acesa ? -35f : 35f, 0f, 0f);
        }
    }
}
