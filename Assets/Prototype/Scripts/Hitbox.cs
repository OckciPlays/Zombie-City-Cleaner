using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>Parte do corpo de um zumbi que pode levar tiro. A cabeça mata na hora.</summary>
    public class Hitbox : MonoBehaviour
    {
        public ZombieController owner;
        public bool isHead;
    }
}
