using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O sobrevivente da emboscada (18/11/2026, a etapa 3, <see cref="EmboscadasDoMundo"/>): antes do primeiro tiro, ele
    /// espera no posto (ao lado de uma janela por dentro, ou atrás da mureta do telhado), agachado e quieto; no tiroteio,
    /// luta do posto: a proteção fica a até 4 m dele (a janela, a mureta), e ele não avança, não cerca e não anda de lado
    /// (a emboscada é de onde ele está).
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>No anfitrião: ele é de uma emboscada (o posto é o <see cref="posto"/>).</summary>
        internal bool naEmboscada;

        /// <summary>Até onde do posto vale a proteção dele no tiroteio (m).</summary>
        const float RaioDoPosto = 4f;

        /// <summary>No anfitrião (os testes): ele está numa emboscada.</summary>
        public bool NaEmboscada => naEmboscada;

        /// <summary>A janela do posto, já sabida (a emboscada a escolhe ao pôr ele lá).</summary>
        internal void JanelaDaEmboscada(JanelaDaCasa j) => _janelaDoPosto = j;

        /// <summary>Antes do primeiro tiro: parado no posto, agachado, virado para a janela ou para a beira do telhado.</summary>
        void Aguardar(float agora)
        {
            _temProtecao = false;
            if ((transform.position - posto).sqrMagnitude > 1.2f * 1.2f)
            {
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.6f;
                    Ir(posto, 2.4f, 0.3f);
                }
                return;
            }
            Parar();
            _querAgachar = 100;
            var giro = Quaternion.Euler(0f, giroDoPosto, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, giro, 180f * Time.deltaTime);
        }
    }
}
