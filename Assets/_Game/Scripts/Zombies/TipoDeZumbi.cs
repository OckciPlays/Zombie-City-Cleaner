using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O tipo do zumbi (01/11/2026, a pedido: os especiais). O Oco é o de sempre; os especiais têm um comportamento próprio
    /// por cima (Zumbi.Corredor.cs, Zumbi.Long.cs, Zumbi.Brutamonte.cs) e os números deles em <see cref="AjustesDeZumbi"/>.
    /// </summary>
    public enum TipoDeZumbi : byte { Oco, Corredor, Long, Brutamonte }

    /// <summary>O Corredor: um Oco contaminado há mais tempo; frágil e muito rápido.</summary>
    [System.Serializable]
    public class AjustesDoCorredor
    {
        [Tooltip("A vida do corpo (o Oco tem 100).")]
        public float vida = 60f;
        [Tooltip("A vida de cada braço e de cada perna.")]
        public float vidaDoMembro = 30f;
        [Tooltip("Correndo atrás do jogador (m/s; o jogador corre a 4,2).")]
        public float velocidadePerseguindo = 3.8f;
        [Tooltip("O bote nos últimos metros (m/s).")]
        public float velocidadeDoBote = 4.6f;
        [Tooltip("A partir de quantos metros do alvo ele dá o bote.")]
        public float distanciaDoBote = 4f;
        [Tooltip("Vagando e investigando, mais rápido que o Oco (vezes).")]
        public float fatorAndando = 1.6f;
        [Tooltip("No sol, a pedra sobe neste tempo (s): o corpo dele é mais frágil que o do Oco.")]
        public float tempoParaPetrificar = 5f;
        [Tooltip("Dos zumbis que nascem, a fração de Corredores: de dia (poucos) e de noite (com as hordas).")]
        public float fracaoDeDia = 0.02f;
        public float fracaoDeNoite = 0.08f;
    }

    /// <summary>O Long (o cuspidor): não ataca, ajuda as hordas; odeia a luz.</summary>
    [System.Serializable]
    public class AjustesDoLong
    {
        public float vida = 150f;
        public float vidaDoMembro = 60f;
        [Tooltip("A distância que ele mantém dos jogadores (m): mais perto que o mínimo, ele se afasta.")]
        public Vector2 distancia = new Vector2(14f, 30f);
        public float velocidade = 1.5f;
        [Tooltip("Até onde ele cospe (m) e o intervalo entre as cusparadas (s).")]
        public float alcanceDoCuspe = 26f;
        public Vector2 intervaloDoCuspe = new Vector2(6f, 9f);
        [Tooltip("A velocidade da gosma no ar (m/s).")]
        public float velocidadeDaGosma = 16f;
        [Tooltip("O raio da poça (m) e quanto tempo ela fica (s).")]
        public float raioDaPoca = 2.2f;
        public float tempoDaPoca = 20f;
        [Tooltip("Acertado em cheio: preso no chão; segurando E por este tempo (s) ele se solta (um colega ajudando, a metade).")]
        public float tempoParaSoltar = 2f;
        [Tooltip("O grito (quando leva um tiro e não morre): o raio dos zumbis que vêm (m) e o intervalo entre gritos (s).")]
        public float raioDoGrito = 60f;
        public float intervaloDoGrito = 20f;
        [Tooltip("Quantos Ocos novos o grito chama (nascem fora da vista, de 30 a 50 m).")]
        public Vector2Int chamadosPeloGrito = new Vector2Int(5, 9);
        [Tooltip("O ponto fraco: a lanterna nele, de perto (m), por este tempo (s), o petrifica (e ele morre).")]
        public float alcanceDaLanterna = 7f;
        public float tempoNaLuz = 2f;
        [Tooltip("O nascimento (só de noite): a chance a cada verificação, o intervalo (s) e quantos ao mesmo tempo, no máximo.")]
        public float chance = 0.35f;
        public float intervaloDoNascimento = 120f;
        public int maximo = 2;
    }

    /// <summary>O Brutamonte: o mini-chefe, um tanque cego e surdo; o olho nas costas acha os jogadores.</summary>
    [System.Serializable]
    public class AjustesDoBrutamonte
    {
        public float vida = 500f;
        [Tooltip("O casco: o dano em qualquer lugar fora do olho (vezes).")]
        public float danoNoCasco = 0.3f;
        [Tooltip("O olho aberto (vezes; o mesmo da cabeça dos outros).")]
        public float danoNoOlho = 3f;
        [Tooltip("O olho procura: até onde ele vê (m) e quanto tempo precisa ver para travar (s).")]
        public float alcanceDoOlho = 35f;
        public float tempoParaTravar = 1f;
        [Tooltip("Firmando os pés antes do avanço (s): a deixa para sair da frente.")]
        public float preparo = 0.6f;
        [Tooltip("O avanço: a velocidade (m/s), o dano no caminho e até onde vai, no máximo (m).")]
        public float velocidadeDoAvanco = 9f;
        public float danoDoAvanco = 50f;
        public float avancoMaximo = 30f;
        [Tooltip("Batendo numa parede no avanço, fica tonto (s).")]
        public float tonto = 4f;
        [Tooltip("A fúria: quanto dura (s), o raio dos braços (m), o dano de cada braçada e o intervalo entre elas (s).")]
        public float tempoDaFuria = 3f;
        public float raioDaFuria = 2.6f;
        public float danoDaFuria = 35f;
        public float intervaloDaFuria = 0.6f;
        [Tooltip("Cansado depois da fúria (s): o olho abre devagar.")]
        public float cansado = 2f;
        [Tooltip("Andando atrás de quem o olho viu (m/s), entre os avanços.")]
        public float velocidade = 1.3f;
        [Tooltip("O nascimento: um por mundo; a chance de aparecer a cada verificação, de noite e de dia, e o intervalo (s).")]
        public float chanceDeNoite = 0.3f;
        public float chanceDeDia = 0.05f;
        public float intervaloDoNascimento = 180f;
        [Tooltip("Sem ninguém a menos disto (m) por este tempo (s), ele volta para a terra (guardando a vida).")]
        public float voltaParaATerra = 120f;
        public float tempoSemNinguem = 60f;
    }
}
