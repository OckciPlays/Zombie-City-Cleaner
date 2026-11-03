using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma janela que abre de uma obra (o Plano_Portas.md da pasta de arte, a parte 5): no mundo, das janelas de fora que o
    /// abandono deixaria com o vidro inteiro (<see cref="AbandonoDaObra.Janelas"/>), a obra guarda o ponto
    /// (<see cref="Obra.janelas"/>), e o jogo põe as duas folhas de vidro sujo que abrem para fora, como portas.
    /// </summary>
    public struct JanelaDaObra
    {
        /// <summary>O meio do peitoril (embaixo da abertura, no meio da parede), nas coordenadas da obra.</summary>
        public Vector3 peitoril;
        /// <summary>No chão (x, z): ao longo da parede e para fora dela.</summary>
        public Vector2 u, fora;
        /// <summary>O vão (a largura e a altura) e a grossura da parede.</summary>
        public float largura, altura, parede;

        /// <summary>A mesma janela na obra espelhada em x.</summary>
        public JanelaDaObra Espelhada()
        {
            var j = this;
            j.peitoril.x = -j.peitoril.x;
            j.u.x = -j.u.x;
            j.fora.x = -j.fora.x;
            return j;
        }
    }
}
