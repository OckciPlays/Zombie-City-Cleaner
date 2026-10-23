using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>Corpo é a cápsula inteira (nível médio); o resto são as hitboxes presas nos ossos (nível perto).</summary>
    public enum RegiaoDoCorpo : byte { Corpo, Cabeca, Tronco, Braco, Perna }

    /// <summary>
    /// Uma parte do corpo do zumbi que leva tiro, presa a um osso (ou a cápsula inteira, no nível médio).
    /// O tiro manda a região para o anfitrião: a cabeça explode e mata; o resto tira vida do corpo.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Zumbi zumbi;
        public RegiaoDoCorpo regiao;
        public Collider colisor;
    }
}
