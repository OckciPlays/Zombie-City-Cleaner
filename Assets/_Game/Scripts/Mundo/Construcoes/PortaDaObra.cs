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

        // ------------------------------------------------------------ as do Hospital Principal (08/11/2026)

        /// <summary>
        /// O começo vem da obra (o <see cref="giro"/>: 0 fechada, e a <see cref="trancada"/>), sem o sorteio das casas: as
        /// portas do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 7), que a obra decide pela semente.
        /// </summary>
        public bool doKit;
        /// <summary>Com <see cref="doKit"/>: começa trancada (só fechada).</summary>
        public bool trancada;
        /// <summary>
        /// A folha abre para fora da obra (as saídas do hospital): "por dentro", para destrancar sem ferramenta, é o lado
        /// contrário ao que ela abre. Nas casas, as de fora abrem para dentro.
        /// </summary>
        public bool abreParaFora;
        /// <summary>
        /// A outra folha da porta dupla: o índice dela na lista da obra mais 1 (0: sem; o padrão das casas). As duas abrem,
        /// fecham e trancam juntas.
        /// </summary>
        public int parMaisUm;

        /// <summary>O índice da outra folha da porta dupla (-1: sem).</summary>
        public int Par => parMaisUm - 1;
        /// <summary>O giro da folha aberta por alguém (graus; 0: 90°, ou o máximo): a corta-fogo da escada encosta na parede, a 172°.</summary>
        public float abertaEm;
        /// <summary>
        /// A malha da folha, quando a obra desenha a dela (a de vai-e-vem com o visor, a corta-fogo, a reforçada com o
        /// aviso): no espaço da folha (a origem na dobradiça, embaixo; a folha ao longo do +X, o +Y para cima). O
        /// <see cref="modelo"/> é a chave dela (as folhas iguais usam a mesma malha). Null: a folha das casas.
        /// </summary>
        public MalhaDeObra folha;
        public string modelo;

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

        /// <summary>
        /// O ponto (nas coordenadas da obra) está do lado de dentro da obra, onde uma de fora destranca sem ferramenta: o
        /// lado para onde ela abre (nas casas) ou o outro (com <see cref="abreParaFora"/>).
        /// </summary>
        public bool PorDentro(Vector3 q)
        {
            bool doLadoQueAbre = (q.x - dobradica.x) * abre.x + (q.z - dobradica.z) * abre.y > 0f;
            return DeFora && doLadoQueAbre != abreParaFora;
        }
    }
}
