using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os números dos chefes (15/11/2026; o comportamento em Zumbi.Chefes.cs, o nascimento em ZombieManager.Chefes.cs). Os
    /// tempos de cada golpe (quando o dano sai, quando a mão fecha) vêm das animações do Blender (chefe_anim.py) e ficam no
    /// código, junto delas; aqui, o dano, o alcance e a vida.
    /// </summary>
    [System.Serializable]
    public class AjustesDosChefes
    {
        [Header("Vida (o Oco tem 100; o Brutamonte, 500 atrás do casco; 15/11/2026, a pedido: mais vida)")]
        public float vidaDoBlindado = 5000f;
        public float vidaDoAgregado = 1500f;
        public float vidaDoAlongado = 3500f;
        public float vidaDoLongo = 1000f;
        public float vidaDoCondutor = 4500f;

        [Header("Andando atrás do alvo (m/s; o jogador corre a 4,2)")]
        public float velocidadeDoBlindado = 1.7f;
        public float velocidadeDoAgregado = 2.3f;
        public float velocidadeDoAlongado = 1.5f;
        public float velocidadeDoLongo = 3.5f;
        public float velocidadeDoCondutor = 1.4f;

        [Header("Os acertos neles")]
        [Tooltip("Na cabeça (vezes).")]
        public float multiplicadorNaCabeca = 2f;
        [Tooltip("No ponto fraco (vezes): o papo do Longo, o rosto no peito do Condutor.")]
        public float multiplicadorNoPontoFraco = 3f;
        [Tooltip("Nos braços e nas pernas (vezes).")]
        public float danoNosMembros = 0.6f;
        [Tooltip("O Blindado: o casco de calos, fora da cabeça (vezes).")]
        public float cascoDoBlindado = 0.55f;
        [Tooltip("O Blindado absorvendo (agachado, os braços na frente): o que passa (vezes).")]
        public float danoAbsorvendo = 0.12f;
        [Tooltip("O maior acerto de uma vez (o carro rápido, a explosão; o tiro passa inteiro).")]
        public float maiorAcerto = 150f;
        [Tooltip("Segurando alguém, quanto dano ele aguenta antes de soltar (os amigos atirando).")]
        public float danoParaSoltar = 140f;

        [Header("Percepção")]
        [Tooltip("Até onde ele vê alguém (m), com a linha livre.")]
        public float alcanceDaVisao = 45f;
        [Tooltip("Mais perto que isto (m), ele sente, mesmo sem ver.")]
        public float sentePerto = 14f;
        [Tooltip("Mais longe que isto (m), ou sem ver por tempoSemVer (s), ele desiste do alvo.")]
        public float desisteA = 90f;
        public float tempoSemVer = 14f;

        [Header("O Blindado")]
        [Tooltip("O golpe no chão com os dois punhos: o dano no meio e o raio (m; na borda, um terço).")]
        public float danoDoGolpe = 45f;
        public float raioDoGolpe = 6.5f;
        [Tooltip("A investida: a velocidade (m/s), o dano em quem estiver no caminho e até onde ela vai (m).")]
        public float velocidadeDaInvestida = 8.5f;
        public float danoDaInvestida = 50f;
        public float investidaMaxima = 28f;
        [Tooltip("Batendo numa parede na investida, fica tonto (s).")]
        public float tontoDoBlindado = 3f;
        [Tooltip("O agarrão: o dano de cada apertão (são dois) e a velocidade do arremesso no fim (m/s).")]
        public float danoDoAperto = 16f;
        public float arremessoDoBlindado = 11f;
        [Tooltip("Absorve quando leva tanto dano (antes do casco) em 3 s.")]
        public float danoParaAbsorver = 260f;
        [Tooltip("Absorvendo, ele recupera esta fração da vida inteira (aos poucos, enquanto está agachado).")]
        public float curaAbsorvendo = 0.08f;

        [Header("Os Agregados")]
        [Tooltip("A mão de carne que estica: até onde ela pega (m) e o dano quando agarra.")]
        public float alcanceDaMao = 10f;
        public float danoDaMao = 10f;
        [Tooltip("O golpe do braço normal.")]
        public float danoDoGolpeDoAgregado = 24f;

        [Header("O Alongado")]
        [Tooltip("O braço comprido: até onde ele agarra (m).")]
        public float alcanceDoAlongado = 12f;
        [Tooltip("A varrida no chão (o dano) e o arremesso (a velocidade, m/s; o dano chega junto).")]
        public float danoDaVarrida = 30f;
        public float arremessoDoAlongado = 15f;
        public float danoDoArremesso = 22f;

        [Header("O Longo")]
        [Tooltip("O bote: o dano da mordida e até onde ele salta (m).")]
        public float danoDoBote = 32f;
        public float boteMaximo = 7f;
        [Tooltip("O ácido (a gosma do Long, com o raio e o tempo da poça dela): de quanto em quanto tempo (s).")]
        public Vector2 intervaloDoCuspe = new Vector2(4.5f, 7f);
        [Tooltip("O ácido em cheio: o dano na hora (além de prender, como a gosma do Long).")]
        public float danoDoAcido = 20f;
        [Tooltip("O veneno do ácido: quanto tempo dura (s) e quanta vida tira nesse tempo, no total.")]
        public float venenoDoAcido = 45f;
        public float venenoTotalDoAcido = 15f;
        [Tooltip("Na poça de ácido: o dano a cada segundo nas pernas (e o veneno renovado pela metade).")]
        public float queimaNaPoca = 3f;

        [Header("O Condutor")]
        [Tooltip("A varredura da clava de corpos: o dano.")]
        public float danoDaVarredura = 38f;
        [Tooltip("O grito: o raio dos zumbis que já estão por ali e vêm (m) e o intervalo entre gritos (s).")]
        public float raioDoGrito = 70f;
        public float intervaloDoGrito = 28f;
        [Header("A horda do grito (nasce de verdade: Ocos e Corredores, Longs e Brutamontes)")]
        [Tooltip("Ocos e Corredores que nascem a cada grito (sorteado entre os dois).")]
        public Vector2Int comunsNoGrito = new Vector2Int(12, 20);
        [Tooltip("Dos comuns da horda, a fração de Corredores.")]
        public float fracaoDeCorredoresNoGrito = 0.3f;
        [Tooltip("Longs a cada grito (sorteado); o segundo número é também o máximo de Longs vivos no mundo.")]
        public Vector2Int longsNoGrito = new Vector2Int(1, 3);
        [Tooltip("Brutamontes a cada grito (sorteado; eles brotam da terra perto do alvo); o segundo é o máximo vivos no mundo.")]
        public Vector2Int brutamontesNoGrito = new Vector2Int(0, 2);
        [Tooltip("O máximo de comuns das hordas dos gritos vivos ao mesmo tempo (o grito seguinte completa até aqui).")]
        public int hordaMaxima = 40;
        [Tooltip("Chamados pelo grito, eles vão atrás do alvo de longe por este tempo (s), sem desistir pela distância nem por não ver.")]
        public float tempoDoChamado = 45f;
        [Tooltip("O frenesi da horda: os zumbis até este raio (m) ficam mais rápidos (vezes) por este tempo (s).")]
        public float raioDoFrenesi = 30f;
        public float fatorDoFrenesi = 1.35f;
        public float tempoDoFrenesi = 15f;

        [Header("Nascer no mundo (um chefe por vez)")]
        public bool nascerNoMundo = true;
        [Tooltip("A cada intervalo (s), a chance de um chefe aparecer longe de um jogador, fora da vista: de noite e de dia.")]
        public float intervaloDoNascimento = 480f;
        public float chanceDeNoite = 0.3f;
        public float chanceDeDia = 0.04f;
        [Tooltip("O primeiro só depois deste tempo de partida (s).")]
        public float primeiroDepoisDe = 600f;
        [Tooltip("Sem ninguém a menos disto (m) por este tempo (s), ele vai embora (a vida não volta).")]
        public float voltaSemNinguem = 160f;
        public float tempoSemNinguem = 60f;
    }
}
