using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que um zumbi persegue e morde (02/11/2026, a pedido: "não apenas os Ocos deveriam ir atrás, mas os Brutamontes,
    /// os rápidos e os Long"): o jogador (<see cref="Player.JogadorRede"/>) ou um sobrevivente
    /// (<see cref="Sobreviventes.Sobrevivente"/>).
    /// </summary>
    public interface IAlvoDeZumbi
    {
        Transform transform { get; }
        /// <summary>Ainda vale como alvo (na rede, vivo).</summary>
        bool AlvoValido { get; }
        /// <summary>No anfitrião: o golpe de um zumbi (o dano e de onde veio).</summary>
        void LevarGolpe(float dano, Vector3 de);
    }

    /// <summary>
    /// Um alvo que não é jogador (os sobreviventes): ele se põe na lista do <see cref="ZombieManager.OutrosAlvos"/> e os
    /// zumbis o percebem como a um jogador (a luz dele, se está correndo).
    /// </summary>
    public interface IOutroAlvoDeZumbi : IAlvoDeZumbi
    {
        bool LuzLigada { get; }
        float VelocidadeNoChao { get; }
    }
}
