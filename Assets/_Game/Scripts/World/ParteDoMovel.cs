using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma parte que abre de um móvel novo no mundo (a porta, a gaveta, a tampa, o forno): a mira acha o colisor dela (um
    /// gatilho um pouco maior que ela, fora do NavMesh), e o E pede ao anfitrião para abrir ou fechar
    /// (<see cref="MobiliaNovaDoMundo.Alternar"/>): todos veem igual, e quem chega depois vê como ficou.
    /// </summary>
    public class ParteDoMovel : Interagivel
    {
        /// <summary>A chave dela na rede (<see cref="MobiliaNova.ChaveDaParte"/>).</summary>
        public ulong chave;
        public ParteQueAbre parte;

        public override string Dica(ArmaJogador quem) => parte == null ? null : $"[E] {(parte.Aberta ? "Fechar" : "Abrir")} {parte.Nome}";

        public override void Usar(ArmaJogador quem) => MobiliaNovaDoMundo.Alternar(this);
    }
}
