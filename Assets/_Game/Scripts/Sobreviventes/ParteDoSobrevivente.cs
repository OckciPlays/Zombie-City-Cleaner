using UnityEngine;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// Uma parte do corpo de um sobrevivente que leva tiro (a cabeça, o tronco, os braços e as pernas), num osso, na camada
    /// 12 (<see cref="Sobrevivente.Camada"/>). Posta pela montagem (Editor/MontagemDosSobreviventes); o tiro e o golpe do
    /// jogador a acham pelo colisor (<see cref="Player.ArmaJogador"/>).
    /// </summary>
    public class ParteDoSobrevivente : MonoBehaviour
    {
        public Sobrevivente sobrevivente;
        public RegiaoDoSobrevivente regiao;
        public Collider colisor;
    }
}
