using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os números das armadilhas (11/11/2026, a etapa 5a da árvore nova: o ramo Armadilhas e defesas da Sobrevivência; o
    /// mundo é o <c>Loot/ConstrucoesNoMundo.Armadilhas.cs</c> e o <c>.CercaDeFogo.cs</c>). Aqui, para o Pedro ajustar.
    /// O anfitrião decide tudo (os toques, o dano, os usos); o Armadilheiro vale pelo nível que o dono tinha quando montou
    /// (guardado na construção: o <c>Construcao.nivel</c>).
    /// </summary>
    public static class ArmadilhasDoJogador
    {
        // ------------------------------------------------------------------ o alarme de latas (t1)

        /// <summary>Quantos toques o barbante aguenta (o último o arrebenta: sobra 1 sucata).</summary>
        public const int ToquesDoAlarme = 12;
        /// <summary>Quem passa a menos disto do barbante (m, para os lados) o toca; o barbante vai de -MeioDoBarbante a +MeioDoBarbante no X.</summary>
        public const float PertoDoBarbante = 0.35f, MeioDoBarbante = 0.8f;
        /// <summary>Depois de tocar, ele espera isto (s) para tocar de novo.</summary>
        public const float RearmeDoAlarme = 4f;
        /// <summary>Até onde se ouvem as latas (m), até onde o dono recebe o aviso (m) e até onde os zumbis vêm ver (m).</summary>
        public const float OuvidoDasLatas = 35f, AvisoDoAlarme = 60f, BarulhoDoAlarme = 12f;

        // ------------------------------------------------------------------ o tapete de pregos (t3)

        /// <summary>Quantas vezes ele fere (cada zumbi, morador ou pneu gasta uma; o jogador não gasta).</summary>
        public const int UsosDoTapete = 8;
        /// <summary>O dano no zumbi e no morador (uma vez por passagem) e no jogador (na perna, andando ou correndo).</summary>
        public const float DanoDoTapete = 20f, DanoDoTapeteNoJogador = 6f;
        /// <summary>O zumbi que pisa anda a esta fração da velocidade por estes segundos.</summary>
        public const float LentidaoDosPregos = 0.5f, SegundosLento = 4f;
        /// <summary>Gasto, ele some e sobram estes pregos no chão.</summary>
        public const int PregosQueSobram = 4;

        // ------------------------------------------------------------------ as estacas (t4)

        /// <summary>A vida das estacas (como a barricada: os zumbis encostados tiram, na metade do ritmo dela).</summary>
        public const float VidaDasEstacas = 100f, RitmoDoDesgaste = 0.5f;
        /// <summary>Cada zumbi engajado encostado na frente (o lado +Z, o das pontas) leva isto por segundo.</summary>
        public const float DanoDasEstacas = 12f;
        /// <summary>O Brutamonte as quebra, mas leva isto.</summary>
        public const float DanoNoBrutamonte = 60f;
        /// <summary>Recolhidas com pelo menos isto da vida (%), voltam as estacas; senão, 2 madeiras.</summary>
        public const float EstacasVoltamCom = 60f;

        // ------------------------------------------------------------------ a cerca de fogo (t6)

        /// <summary>Acesa, ela queima isto (s; na chuva, menos, como a área do molotov).</summary>
        public const float SegundosDaCerca = 90f;
        /// <summary>O rastro de combustível (m, do pavio para +Z) e o tempo (s) que o fogo leva para correr do pavio até a ponta.</summary>
        public const float ComprimentoDaCerca = 4f, CorridaDoFogo = 1f;
        /// <summary>Quem está a menos disto da linha acesa queima (m).</summary>
        public const float LarguraDaQueima = 0.6f;
        /// <summary>Os zumbis ficam a esta distância de cada ponto de fogo da linha (m; a Muralha de fogo aumenta, como na área do molotov).</summary>
        public const float RaioQueSegura = 1.3f;
        /// <summary>Os pontos de fogo que seguram os zumbis ao longo da linha.</summary>
        public const int PontosQueSeguram = 5;

        // ------------------------------------------------------------------ o Armadilheiro (t5)

        /// <summary>Por nível do Armadilheiro: os usos (e a vida, e o tempo da cerca) e o dano das armadilhas do dono.</summary>
        public const float UsosPorNivel = 0.5f, DanoPorNivel = 0.25f;

        public static float FatorDeUsos(int nivel) => 1f + UsosPorNivel * Mathf.Clamp(nivel, 0, 2);
        public static float FatorDeDano(int nivel) => 1f + DanoPorNivel * Mathf.Clamp(nivel, 0, 2);
        /// <summary>No nível 2, o tapete de pregos do dono não fere jogadores.</summary>
        public static bool TapeteSemJogadores(int nivel) => nivel >= 2;

        public static int ToquesDe(int nivel) => Mathf.RoundToInt(ToquesDoAlarme * FatorDeUsos(nivel));
        public static int UsosDoTapeteDe(int nivel) => Mathf.RoundToInt(UsosDoTapete * FatorDeUsos(nivel));
        public static float VidaDasEstacasDe(int nivel) => VidaDasEstacas * FatorDeUsos(nivel);
        public static float SegundosDaCercaDe(int nivel) => SegundosDaCerca * FatorDeUsos(nivel);

        /// <summary>O nível do Armadilheiro do jogador (0 a 2).</summary>
        public static int NivelDe(JogadorRede j) => j != null ? Habilidades.Pontos(j, Habilidade.Armadilheiro) : 0;

        // ------------------------------------------------------------------ as barricadas (t2) e a Fortaleza (t7), a etapa 5b

        /// <summary>
        /// A vida da barricada de tábuas pregada numa porta e numa janela, e a da barricada solta (o kit de antes, sem nó). Os
        /// zumbis engajados encostados tiram <c>ConstrucoesNoMundo.DanoPorZumbi</c> (2,5) por segundo cada, nas três.
        /// </summary>
        public const float VidaNaPorta = 150f, VidaNaJanela = 100f, VidaDaBarricada = 100f;
        /// <summary>O nível 2 da Barricada de tábuas: a vida a mais (fração) e o conserto (a fração da vida máxima que volta).</summary>
        public const float ReforcoDoNivel2 = 0.5f, Conserto = 0.35f;
        /// <summary>O que o conserto gasta (com a ferramenta na mochila).</summary>
        public const int MadeiraDoConserto = 1, PregosDoConserto = 2;
        /// <summary>Quanto tempo se segura (s): o E para pregar e para desmontar; o clique para consertar.</summary>
        public const float SegundosDePregar = 3f, SegundosDeDesmontar = 2f, SegundosDeConsertar = 2.5f;
        /// <summary>O barulho do martelo (pregar e consertar): os zumbis até esta distância (m) vêm ver.</summary>
        public const float BarulhoDoMartelo = 15f;
        /// <summary>Desmontada com pelo menos isto da vida (%), voltam as tábuas (e a barricada solta); senão, 2 madeiras.</summary>
        public const float TabuasVoltamCom = 60f;
        /// <summary>O golpe de arma branca de um jogador numa barricada de tábuas (dos dois lados) tira esta fração do dano da arma.</summary>
        public const float GolpeNaBarricada = 0.5f;
        /// <summary>Abaixo desta fração da vida, a barricada de tábuas aparece danificada (a outra malha).</summary>
        public const float Danificada = 0.5f;

        /// <summary>A Fortaleza: as defesas do dono (as barricadas de tábuas, a solta e as estacas) com a vida máxima vezes isto (na hora de montar).</summary>
        public const float FatorDaFortaleza = 2f;
        /// <summary>
        /// A Fortaleza: elas se consertam sozinhas, esta fração da vida máxima por hora do jogo (o <c>CicloDoDia</c>: 24 min
        /// por dia, uma hora em 60 s), só com o dono a até <see cref="DonoPerto"/> m e nenhum zumbi batendo nelas há
        /// <see cref="SossegoDoConserto"/> s.
        /// </summary>
        public const float ConsertoPorHora = 0.05f, DonoPerto = 30f, SossegoDoConserto = 5f;

        /// <summary>O fator de vida do nível 2 da Barricada de tábuas.</summary>
        public static float FatorDoReforco(bool reforcada) => reforcada ? 1f + ReforcoDoNivel2 : 1f;
        /// <summary>A vida máxima de uma barricada de tábuas: a base (porta ou janela), o nível 2, o Armadilheiro e a Fortaleza do dono.</summary>
        public static float VidaDaBarricadaDeTabuas(bool janela, bool reforcada, int armadilheiro, bool fortaleza) =>
            (janela ? VidaNaJanela : VidaNaPorta) * FatorDoReforco(reforcada) * FatorDeUsos(armadilheiro) * (fortaleza ? FatorDaFortaleza : 1f);
        /// <summary>A vida máxima da barricada solta: o Armadilheiro e a Fortaleza do dono.</summary>
        public static float VidaDaBarricadaSolta(int armadilheiro, bool fortaleza) =>
            VidaDaBarricada * FatorDeUsos(armadilheiro) * (fortaleza ? FatorDaFortaleza : 1f);
        /// <summary>O nível da Barricada de tábuas do jogador (0 a 2) e se ele tem a Fortaleza.</summary>
        public static int BarricadaDe(JogadorRede j) => j != null ? Habilidades.Pontos(j, Habilidade.BarricadaDeTabuas) : 0;
        public static bool FortalezaDe(JogadorRede j) => j != null && Habilidades.Tem(j, Habilidade.Fortaleza);
    }
}
