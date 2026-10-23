using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Onde nasce um grupo de zumbis: a quantidade, espalhada num raio em volta do ponto, no NavMesh e na mesma altura
    /// (dá para pôr hordas dentro do prédio, andar por andar). Fica como filho do objeto "Zumbis" da cena.
    /// </summary>
    public class PontoDeHorda : MonoBehaviour
    {
        [Min(1)] public int quantidade = 5;
        [Min(0f)] public float raio = 5f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.75f, 0.15f, 0.15f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, raio);
        }
    }
}
