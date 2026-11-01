using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma folha que abre do bunker da família (o portão, a portinhola, as portas de aço do B4 e da casinha, a da câmara
    /// fria): a mira acha o gatilho dela, e o E pede ao anfitrião (<see cref="BunkerDoMundo.PedirPorta"/>), que confere a
    /// distância e abre ou fecha para todos.
    /// </summary>
    public class PortaDoBunkerDoMundo : Interagivel
    {
        public BunkerDoMundo bunker;
        public int porta;
        public ParteQueAbre parte;

        public override string Dica(ArmaJogador quem) =>
            bunker == null || parte == null ? null : $"[E] {(bunker.Aberta(porta) ? "Fechar" : "Abrir")} {parte.Nome}";

        public override void Usar(ArmaJogador quem) => bunker?.PedirPorta(porta);
    }
}
