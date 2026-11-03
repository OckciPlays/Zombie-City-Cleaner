using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma porta que abre de uma obra (o Plano_Portas.md da pasta de arte, a parte 2): no mundo, a folha não é desenhada na
    /// malha da casa (<see cref="Obra.portasVivas"/>), e a obra guarda o ponto dela (<see cref="Obra.portas"/>), nas
    /// coordenadas da obra; o jogo põe a folha que abre e fecha pela rede. Fechada, a folha fica na face da parede do lado
    /// para onde ela abre, como a das folhas paradas.
    /// </summary>
    public struct PortaDaObra
    {
        public enum Tipo : byte { Frente, Fora, Porao, Garagem, Dentro }

        /// <summary>A dobradiça, embaixo da folha (no chão), nas coordenadas da obra.</summary>
        public Vector3 dobradica;
        /// <summary>No chão (x, z): para onde a folha vai, fechada (da dobradiça para a ponta), e para onde a ponta vai ao abrir.</summary>
        public Vector2 fechada, abre;
        /// <summary>O tamanho da folha (a largura, da dobradiça à ponta, e a altura).</summary>
        public float largura, altura;
        /// <summary>A cor da folha (o nome na paleta).</summary>
        public string cor;
        /// <summary>O giro do começo (graus, 0 fechada) e até onde ela abre.</summary>
        public float giro, maximo;
        public Tipo tipo;
        /// <summary>O tipo do cômodo para onde ela abre ("quarto", "banheiro"...), nas de dentro; null nas outras.</summary>
        public string comodo;

        /// <summary>A de fora (abre de dentro da casa para dentro dela): as trancas se abrem por dentro.</summary>
        public bool DeFora => tipo != Tipo.Dentro;

        /// <summary>A direção da folha aberta 'graus' (no chão).</summary>
        public Vector2 Direcao(float graus)
        {
            float r = graus * Mathf.Deg2Rad;
            return fechada * Mathf.Cos(r) + abre * Mathf.Sin(r);
        }

        /// <summary>A mesma porta na casa espelhada em x.</summary>
        public PortaDaObra Espelhada()
        {
            var p = this;
            p.dobradica.x = -p.dobradica.x;
            p.fechada.x = -p.fechada.x;
            p.abre.x = -p.abre.x;
            return p;
        }
    }
}
