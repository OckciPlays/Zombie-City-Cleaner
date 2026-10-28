using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Uma coisa que o jogador usa com E, olhando para ela de perto: uma arma do painel, a porta do bunker, um
    /// interruptor. A mira (<see cref="ArmaJogador"/>) procura o colisor dela; a dica aparece embaixo da mira.
    /// </summary>
    public abstract class Interagivel : MonoBehaviour
    {
        /// <summary>O texto embaixo da mira ("[E] ..."), ou null se agora não dá para usar.</summary>
        public abstract string Dica(ArmaJogador quem);

        /// <summary>O jogador local apertou E olhando para isto.</summary>
        public abstract void Usar(ArmaJogador quem);

        /// <summary>
        /// O clique esquerdo, olhando para isto, é desta coisa (e não da arma ou do soco): o macaco apoiado sobe e desce o
        /// carro no clique. Falso na maioria.
        /// </summary>
        public virtual bool UsaClique(ArmaJogador quem) => false;

        /// <summary>O jogador local clicou (botão esquerdo) olhando para isto (só se <see cref="UsaClique"/>).</summary>
        public virtual void Clicar(ArmaJogador quem) { }
    }
}
