using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma coisa de fora deste assembly que se usa com o E nas cenas de exemplo (o papel do hospital, que se lê): o
    /// <see cref="AndarNoExemplo"/> acha pelo gatilho dela, no meio da tela, até o alcance, e mostra a dica embaixo da mira.
    /// No jogo, a mesma coisa é um <c>Interagivel</c> (o assembly do jogo).
    /// </summary>
    public interface IUsavelNoExemplo
    {
        /// <summary>O texto da mira ("[E] Ler"), ou null se agora não dá para usar.</summary>
        string DicaNoExemplo { get; }

        void UsarNoExemplo();

        /// <summary>O raio bateu num colisor sólido em 'ponto' antes de chegar nesta coisa: ela ganha dele (a caixa de
        /// colisão do móvel embaixo do papel, que é do tamanho do móvel inteiro).</summary>
        bool GanhaNoExemplo(Vector3 ponto);
    }
}
