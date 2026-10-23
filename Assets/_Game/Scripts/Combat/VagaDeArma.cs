using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Uma vaga do <see cref="ParedeDeArmas"/>: o colisor (gatilho) que a mira do jogador procura e a arma deitada nela.
    /// A origem fica na frente do painel, na altura da prateleira e no meio da vaga.
    /// </summary>
    public class VagaDeArma : MonoBehaviour
    {
        public ParedeDeArmas parede;
        public int indice;
        [Tooltip("A arma da vaga (a malha muda com o que está nela).")]
        public MeshFilter filtro;
    }
}
