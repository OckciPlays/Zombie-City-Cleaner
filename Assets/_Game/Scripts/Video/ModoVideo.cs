using UnityEngine;

namespace ZombieCityCleaner.Video
{
    /// <summary>
    /// O que muda no jogo quando a cena de vídeo (<c>Scenes/Video.unity</c>) roda: quem liga e desliga é o
    /// <see cref="DiretorDoVideo"/>. Fora dela, tudo fica como no jogo.
    /// </summary>
    public static class ModoVideo
    {
        /// <summary>Gravando um vídeo: nada de HUD (o menu, a mira, as barras, os atalhos, a dica, o F3 e o FPS).</summary>
        public static bool Ligado { get; set; }
        /// <summary>O jogador nasce de mãos vazias (sem a pistola do começo).</summary>
        public static bool SemArma { get; set; }
        /// <summary>Quanto das duas neblinas sobra (1 = a do jogo). Vista do alto, a neblina do jogo cobre tudo.</summary>
        public static float Neblina { get; set; } = 1f;
        /// <summary>
        /// De onde a câmera do vídeo olha, ou null quando quem filma é a câmera do jogador. Os zumbis contam a distância
        /// até ela, dividida por <see cref="AlcanceDosZumbis"/>, para o nível de detalhe e para aparecer na tela.
        /// </summary>
        public static Vector3? Olho { get; set; }
        /// <summary>Quantas vezes mais longe da câmera do vídeo os zumbis continuam aparecendo e andando.</summary>
        public static float AlcanceDosZumbis { get; set; } = 1f;
        /// <summary>
        /// A luz de debaixo da terra mesmo com a câmera no alto (0 a 1, como descer a escada do bunker): o corte de raio-X
        /// do bunker olha de fora, mas a luz tem que ser a de dentro.
        /// </summary>
        public static float Subsolo { get; set; }
        /// <summary>A mira (com a munição, a dica e a luneta da sniper) aparece mesmo gravando: o vídeo das armas.</summary>
        public static bool MostrarMira { get; set; }
        /// <summary>A recarga não gasta as caixas, e o HUD mostra "∞" (como no robô de teste).</summary>
        public static bool MunicaoInfinita { get; set; }
        /// <summary>Quantas armas couberem na mochila, sem o limite das costas e do coldre.</summary>
        public static bool ArmasSemLimite { get; set; }

        // sem Domain Reload, o estado estático sobrevive ao Play: tudo volta ao do jogo no começo de cada um
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Zerar()
        {
            Ligado = false;
            SemArma = false;
            Neblina = 1f;
            Olho = null;
            AlcanceDosZumbis = 1f;
            Subsolo = 0f;
            MostrarMira = false;
            MunicaoInfinita = false;
            ArmasSemLimite = false;
        }
    }
}
