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
        [Tooltip("De dia e ao amanhecer, o tiro só tira da sombra quem está até aqui (m); mais longe, nem vai ver se o ponto está no sol.")]
        public float tiroAmeacaDeDia = 5f;

        [Header("Perseguição e ataque")]
        public float distanciaDeDesistir = 20f;
        [Tooltip("Sem ver o alvo por este tempo (s), vai até onde o viu por último e desiste.")]
        public float tempoSemVer = 6f;
        public float alcanceDoAtaque = 1.3f;
        public float intervaloDoGolpe = 1.4f;
        public float danoDoGolpe = 12f;
        [Tooltip("Do começo do golpe até o dano: o momento em que a animação agarra (s).")]
        public float atrasoDoDano = 0.45f;

        [Header("Vida (em tiros de pistola)")]
        [Tooltip("Um tiro na cabeça explode a cabeça e mata; no tronco, cada tiro tira 1.")]
        public float vidaDoCorpo = 6f;
        [Tooltip("Quanto de cada tiro nos braços e nas pernas conta para o corpo.")]
        public float danoNosMembros = 0.5f;
        [Tooltip("A vida de cada braço e de cada perna: acabou, o membro cai (3 tiros de pistola; 1 de sniper).")]
        public float vidaDoMembro = 3f;

        [Header("Sem pernas")]
        [Tooltip("A velocidade mancando (sem uma perna), em proporção da normal.")]
        public float fatorMancando = 0.55f;
        [Tooltip("A velocidade rastejando (sem as duas pernas), em proporção da normal.")]
        public float fatorRastejando = 0.35f;

        [Header("O sol (os Ocos são fotossensíveis)")]
        [Tooltip("No sol, a pedra sobe dos pés à cabeça neste tempo (s): aí ele fica petrificado até a noite.")]
        public float tempoParaPetrificar = 10f;
        [Tooltip("Na sombra, a pedra que já subiu recua neste tempo (s, da cabeça aos pés).")]
        public float tempoParaRecuar = 20f;
        [Tooltip("Com a pedra subindo, a velocidade e a animação caem até esta fração (a pedra prende o corpo).")]
        public float lentidaoNaPedra = 0.1f;
        [Tooltip("Fugindo do sol (desesperado), procurando sombra (m/s).")]
        public float velocidadeDeFuga = 1.7f;
        [Tooltip("Procura sombra até esta distância (m).")]
        public float raioDaSombra = 18f;
        [Tooltip("Ao amanhecer, procura abrigo (de preferência com teto) até esta distância (m).")]
        public float raioDoAbrigo = 40f;
        [Tooltip("De pedra, o tiro tira só esta fração (a casca protege); a cabeça já não explode depois desta pedra.")]
        public float danoNaPedra = 0.25f;
        public float pedraQueProtegeACabeca = 0.8f;
        [Tooltip("À noite, cada um se solta da pedra num momento sorteado neste intervalo (s) depois de escurecer.")]
        public Vector2 soltarANoite = new Vector2(30f, 60f);
        [Tooltip("Quebrando a casca ao se soltar (s).")]
        public float tempoDeQuebrar = 1.6f;

        [Header("A luz (a lanterna e o farol só afastam)")]
        [Tooltip("O miolo do facho da lanterna que o faz se esquivar: até onde (m) e a metade do ângulo (graus).")]
        public float alcanceDaLanterna = 14f;
        public float meioAnguloDaLanterna = 15f;
        [Tooltip("O mesmo para o farol do carro.")]
        public float alcanceDoFarol = 22f;
        public float meioAnguloDoFarol = 22f;
        [Tooltip("O pulo para fora do facho: quanto (m, de lado) e a que velocidade (m/s).")]
        public float distanciaDaEsquiva = 2.2f;
        public float velocidadeDaEsquiva = 3.2f;
        [Tooltip("Entre uma esquiva e outra (s): com a luz parada nele, ele pula de novo.")]
        public float intervaloDaEsquiva = 1f;
    }
}
