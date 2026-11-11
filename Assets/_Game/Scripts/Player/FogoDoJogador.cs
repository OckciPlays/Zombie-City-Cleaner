using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os números do fogo (11/11/2026, a etapa 4 do ramo Fogo da Resistência: do f1 ao f7), para o Pedro ajustar. Os raios
    /// que seguram os zumbis ficam no <see cref="AjustesDeZumbi"/> ("O fogo"): a tocha e o lampião a 4 m, a fogueira e o
    /// molotov a 8 m. Os minutos daqui são de relógio (o tempo jogado), não do relógio do jogo (uma hora do jogo é um minuto).
    ///  - O lampião (f2): na mão, uma luz quente; gasta combustível só aceso na mão (ou largado aceso no chão); a chuva
    ///    não o apaga; o clique só o balança. Reabastece com 1 álcool ou com gasolina do galão.
    ///  - O coquetel molotov (f3): o clique arremessa (a garrafa sai da mão no quadro 8 do soco direito); onde quebra, acende
    ///    uma área de fogo que queima os zumbis, os moradores e os jogadores e assusta os zumbis como uma fogueira.
    ///  - A fogueira portátil (f4): o kit monta uma fogueira que queima uma hora; o E recolhe.
    /// </summary>
    public static class FogoDoJogador
    {
        // ------------------------------------------------------------------ a tocha (f1 e f5)

        /// <summary>A Tocha duradoura (f1): a tocha dura isto a mais por nível (0,5: +50%).</summary>
        public const float TochaDuradouraPorNivel = 0.5f;
        /// <summary>O Fogo que pega (f5): a tocha e o molotov queimam isto a mais por nível (0,3: +30%).</summary>
        public const float FogoQuePegaPorNivel = 0.3f;
        /// <summary>O Fogo que pega (f5): a área do molotov fica isto maior por nível (0,2: +20% no raio).</summary>
        public const float AreaQuePegaPorNivel = 0.2f;

        // ------------------------------------------------------------------ o lampião (f2)

        /// <summary>Quanto dura uma carga do lampião aceso (minutos de relógio): 15.</summary>
        public const float MinutosDoLampiao = 15f;
        /// <summary>O alcance da luz do lampião (m) e a força dela.</summary>
        public const float LuzDoLampiao = 10f, ForcaDaLuzDoLampiao = 2.2f;
        /// <summary>A gasolina de uma carga (L, de um galão), no lugar de 1 álcool: o lampião, o molotov e o kit da fogueira.</summary>
        public const float LitrosPorCarga = 0.5f;
        /// <summary>O lampião largado aceso no chão (o qtd dele: o da mochila é 0; como a <see cref="Itens.TochaApagada"/>).</summary>
        public const ushort LampiaoAcesoNoChao = 1000;

        // ------------------------------------------------------------------ o molotov (f3)

        /// <summary>A velocidade do arremesso (m/s) e quanto a garrafa sobe a mais do que a mira (a mira mais alta vai mais longe).</summary>
        public const float VelocidadeDoArremesso = 15f, SubidaDoArremesso = 0.18f;
        /// <summary>Quando a garrafa sai da mão (s): o quadro 8 do soco direito do Jogador_FP_Molotov (de 1 a 11, a 24 q/s).</summary>
        public const float SoltaAos = 7f / 24f;
        /// <summary>Entre um arremesso e outro (s).</summary>
        public const float IntervaloDoArremesso = 0.9f;
        /// <summary>O raio da área de fogo (m) e quanto ela dura (s), sem o Fogo que pega.</summary>
        public const float RaioDoMolotov = 2.5f, SegundosDoMolotov = 8f;
        /// <summary>O dano por segundo dentro da área: nos zumbis, nos moradores (os sobreviventes) e nos jogadores.</summary>
        public const float DanoNosZumbis = 25f, DanoNosMoradores = 12f, DanoNosJogadores = 8f;
        /// <summary>O zumbi que pegou fogo continua queimando depois de sair da área (s) e o dano por segundo desse tempo.</summary>
        public const float SegundosQueimando = 4f, DanoQueimando = 15f;
        /// <summary>Na chuva, fora de um teto, a área se apaga mais rápido: o tempo corre 1 + isto × a chuva (a chuva, ~2,2 vezes).</summary>
        public const float ChuvaNoMolotov = 2f;

        // ------------------------------------------------------------------ a fogueira portátil (f4)

        /// <summary>Quanto a fogueira portátil queima (minutos de relógio): 60 (a de lenha, 4 por madeira).</summary>
        public const float MinutosDaFogueiraPortatil = 60f;

        // ------------------------------------------------------------------ o calor e a muralha (f6 e f7)

        /// <summary>O Calor do fogo (f6): a menos disto (m) de um fogo aceso, a mente sobe <see cref="CalorPorNivel"/> por hora do jogo por nível.</summary>
        public const float DistanciaDoCalor = 4f, CalorPorNivel = 2f;
        /// <summary>A Muralha de fogo (f7): o fogo do jogador segura os zumbis isto mais longe (1,5: 50%), e eles fogem em pânico.</summary>
        public const float FatorDaMuralha = 1.5f;

        // ------------------------------------------------------------------ as contas

        /// <summary>Os segundos da tocha por 1% de desgaste, com a Tocha duradoura e o Fogo que pega de quem segura.</summary>
        public static float SegundosDaTochaPorDesgaste(JogadorRede j) =>
            Itens.SegundosDaTochaPorDesgaste * (1f + TochaDuradouraPorNivel * Habilidades.Pontos(j, Habilidade.TochaDuradoura))
                                             * (1f + FogoQuePegaPorNivel * Habilidades.Pontos(j, Habilidade.FogoQuePega));

        /// <summary>O raio da área do molotov de quem arremessou (com o Fogo que pega).</summary>
        public static float RaioDoMolotovDe(JogadorRede j) => RaioDoMolotov * (1f + AreaQuePegaPorNivel * Habilidades.Pontos(j, Habilidade.FogoQuePega));

        /// <summary>Quanto a área do molotov dura (s), com o Fogo que pega.</summary>
        public static float SegundosDoMolotovDe(JogadorRede j) => SegundosDoMolotov * (1f + FogoQuePegaPorNivel * Habilidades.Pontos(j, Habilidade.FogoQuePega));

        /// <summary>O zumbi queima depois de sair da área (s), com o Fogo que pega.</summary>
        public static float SegundosQueimandoDe(JogadorRede j) => SegundosQueimando * (1f + FogoQuePegaPorNivel * Habilidades.Pontos(j, Habilidade.FogoQuePega));

        /// <summary>A Muralha de fogo do jogador (1: sem ela).</summary>
        public static float MuralhaDe(JogadorRede j) => j != null && Habilidades.Tem(j, Habilidade.MuralhaDeFogo) ? FatorDaMuralha : 1f;

        /// <summary>A Muralha de fogo do personagem dono (a chave dele: a fogueira e o lampião no chão), se ele estiver na partida.</summary>
        public static float MuralhaDaChave(ulong chave)
        {
            if (chave == 0) return 1f;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && j.Chave.Value == chave) return MuralhaDe(j);
            return 1f;
        }

        /// <summary>Só no anfitrião: há um fogo aceso (a tocha, o lampião, a fogueira, o molotov) a menos de <see cref="DistanciaDoCalor"/> m.</summary>
        public static bool FogoAcesoPerto(Vector3 pes)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null) return false;
            foreach (var f in zm.Fogos)
            {
                Vector3 d = f.centro - pes;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                if (d.sqrMagnitude < DistanciaDoCalor * DistanciaDoCalor) return true;
            }
            return false;
        }
    }
}
