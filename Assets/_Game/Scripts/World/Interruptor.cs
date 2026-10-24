using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um interruptor de luz do bunker: E acende ou apaga as luzes do grupo dele (o cômodo), pelo
    /// <see cref="BunkerRede"/>. A alavanca fica para cima com a luz acesa e para baixo com ela apagada.
    /// </summary>
    public class Interruptor : Interagivel
    {
        public BunkerRede bunker;
        [Tooltip("O lugar dele na lista de interruptores do BunkerRede.")]
        public int indice;
        [Tooltip("O grupo de luzes (0 salão, 1 a 4 quartos, 5 depósito, 6 escada).")]
        public int grupo;
        [Tooltip("O que ele acende, para a dica (\"a luz do salão\").")]
        public string oQue = "a luz";
        [Tooltip("A alavanca: gira em X, para cima (acesa) ou para baixo (apagada).")]
        public Transform alavanca;

        public override string Dica(ArmaJogador quem) =>
            bunker == null ? null : (bunker.Acesa(grupo) ? "[E] Apagar " : "[E] Acender ") + oQue;

        public override void Usar(ArmaJogador quem)
        {
            if (bunker != null) bunker.PedirLuz(indice);
        }

        public void Mostrar(bool acesa)
        {
            if (alavanca != null) alavanca.localRotation = Quaternion.Euler(acesa ? -35f : 35f, 0f, 0f);
        }
    }
}
