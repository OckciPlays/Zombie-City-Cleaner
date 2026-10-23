using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    public enum RegiaoDoCorpo : byte { Corpo, Cabeca, Tronco, BracoE, AntebracoE, BracoD, AntebracoD, PernaE, PernaD }

    /// <summary>
    /// Uma parte do corpo do zumbi que leva tiro, presa a um osso (ou a cápsula inteira, no nível médio).
    /// A Parte 6 usa a região para o dano de cada membro e o desmembramento.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Zumbi zumbi;
        public RegiaoDoCorpo regiao;
        public Collider colisor;
    }
}
