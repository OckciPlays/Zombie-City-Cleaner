using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>Os números do comportamento dos zumbis, ajustáveis no Inspector do ZombieManager.</summary>
    [System.Serializable]
    public class AjustesDeZumbi
    {
        [Header("Movimento (m/s)")]
        public float velocidadeVagando = 0.55f;
        public float velocidadeInvestigando = 0.8f;
        [Tooltip("Lento: andando (2 m/s) o jogador já se afasta.")]
        public float velocidadePerseguindo = 1.1f;
        [Tooltip("Até onde ele vaga em volta de onde está (m).")]
        public float raioDeVagar = 7f;
        [Tooltip("Pausa entre uma andada e outra (s). Um em cada três zumbis para três vezes mais.")]
        public Vector2 pausa = new Vector2(2f, 8f);

        [Header("Percepção (m)")]
        public float visaoDeDia = 16f;
        public float visaoDeNoite = 6f;
        [Tooltip("À noite, com a lanterna ligada, você é visto de longe.")]
        public float visaoComLanterna = 26f;
        [Tooltip("À noite, embaixo de um poste aceso.")]
        public float visaoSobPoste = 13f;
        [Tooltip("Metade do ângulo de visão, em graus.")]
        public float meioAnguloDaVisao = 70f;
        [Tooltip("Tão perto que ele sente, mesmo de costas.")]
        public float distanciaDeContato = 2.5f;
        [Tooltip("Passos correndo se ouvem até aqui.")]
        public float barulhoDeCorrida = 9f;
        public float barulhoDoTiro = 35f;
        [Tooltip("Perto assim do tiro, ele vai direto em quem atirou; mais longe, vai ver o que foi.")]
        public float tiroChamaDireto = 12f;

        [Header("Perseguição e ataque")]
        public float distanciaDeDesistir = 20f;
        [Tooltip("Sem ver o alvo por este tempo (s), vai até onde o viu por último e desiste.")]
        public float tempoSemVer = 6f;
        public float alcanceDoAtaque = 1.3f;
        public float intervaloDoGolpe = 1.4f;
        [Tooltip("Dano de cada golpe com as duas mãos. Com uma mão sai 65%; sem nenhuma (só a mordida), 30%.")]
        public float danoDoGolpe = 12f;
        [Tooltip("Do começo do golpe até o dano: o momento em que a animação agarra (s).")]
        public float atrasoDoDano = 0.45f;

        [Header("Vida (em tiros de pistola)")]
        public float vidaDoCorpo = 6f;
        [Tooltip("Um tiro na cabeça explode a cabeça.")]
        public float vidaDaCabeca = 1f;
        public float vidaDoBraco = 3f;
        public float vidaDoAntebraco = 2f;
        public float vidaDaCoxa = 3f;
        public float vidaDaCanela = 2f;
        [Tooltip("Quanto de cada tiro num membro também conta para o corpo.")]
        public float membroNoCorpo = 0.35f;

        [Header("Sem perna")]
        [Tooltip("Rastejando, as velocidades caem para esta fração (perseguir: 1,1 → 0,4 m/s).")]
        public float fatorRastejando = 0.36f;
    }
}
