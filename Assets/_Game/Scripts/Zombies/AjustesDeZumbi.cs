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
        [Tooltip("À noite, fumando: a brasa do cigarro (03/11/2026, o Plano_Cigarro.md da pasta de arte).")]
        public float visaoComBrasa = 10f;
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

        // a escala de 100 (31/10/2026, a pedido: "mudar o sistema de vida dos zumbis... futuramente... alguns especiais";
        // antes a vida era 6, em tiros de pistola, e qualquer tiro na cabeça matava). Cada tipo de zumbi terá a sua vida
        [Header("Vida (de 100: o Oco)")]
        [Tooltip("A vida do Oco. Cada acerto tira o dano da arma (a pistola, 15) vezes o da parte do corpo.")]
        public float vidaDoCorpo = 100f;
        [Tooltip("Na cabeça, o dano vale tantas vezes (3). A cabeça só explode no acerto que mata.")]
        public float multiplicadorNaCabeca = 3f;
        [Tooltip("Quanto de cada acerto nos braços e nas pernas conta para o corpo.")]
        public float danoNosMembros = 0.5f;
        [Tooltip("A vida de cada braço e de cada perna: acabou, o membro cai (4 tiros de pistola; 1 de sniper; 1 golpe de machado, que corta em dobro).")]
        public float vidaDoMembro = 50f;

        [Header("Sem pernas")]
        [Tooltip("A velocidade mancando (sem uma perna), em proporção da normal.")]
        public float fatorMancando = 0.55f;
        [Tooltip("A velocidade rastejando (sem as duas pernas), em proporção da normal.")]
        public float fatorRastejando = 0.35f;

        // o equilíbrio sem uma perna (31/10/2026, a pedido: "ele vai começar a tombar pros lados, cair no chão, pode começar a
        // se arrastar mesmo com uma perna, pode tentar se levantar e cair novamente..."; Zumbi.Equilibrio.cs)
        [Header("Equilíbrio (sem uma perna)")]
        [Tooltip("Perdendo a perna, a chance de cair na hora; o resto cambaleia.")]
        [Range(0f, 1f)] public float chanceDeCairNaHora = 0.4f;
        [Tooltip("Cambaleando, a chance de cair (o resto se firma e segue pulando).")]
        [Range(0f, 1f)] public float chanceDeCairCambaleando = 0.5f;
        [Tooltip("Quanto tempo ele cambaleia até cair ou se firmar (s).")]
        public Vector2 tempoCambaleando = new Vector2(1.2f, 2.8f);
        [Tooltip("A inclinação máxima cambaleando, antes de cair (graus).")]
        public float inclinacaoDeQueda = 34f;
        [Tooltip("Firme (pulando numa perna), a chance de um acerto fazer ele cambalear de novo.")]
        [Range(0f, 1f)] public float chanceDeCambalearComTiro = 0.45f;
        [Tooltip("A queda com física (o boneco) dura isto (s); depois ele fica deitado um pouco antes de decidir.")]
        public float tempoDaQueda = 2f;
        public Vector2 tempoDeitado = new Vector2(0.6f, 2f);
        [Tooltip("No chão, a chance de tentar levantar (o resto rasteja numa perna).")]
        [Range(0f, 1f)] public float chanceDeTentarLevantar = 0.5f;
        [Tooltip("A chance de cada tentativa de levantar dar certo (falhando, ele cai de novo no meio do caminho).")]
        [Range(0f, 1f)] public float chanceDeLevantar = 0.6f;
        [Tooltip("Depois de tantas tentativas, ele desiste e só rasteja.")]
        public int tentativasDeLevantar = 3;
        [Tooltip("Rastejando numa perna, de tempos em tempos (s) ele pensa em tentar levantar de novo.")]
        public Vector2 rastejandoAteTentar = new Vector2(5f, 10f);
        [Tooltip("Levantar dura isto (s): o clipe do levantar numa perna (LevantarE/D, ~4,5 s no tempo dele) esticado ou encolhido para caber.")]
        public float tempoParaLevantar = 4.45f;

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

        [Header("O fogo (05/11/2026: a tocha e a fogueira acesas; Zumbi.Luz.cs)")]
        [Tooltip("Até onde (m, no plano) eles não chegam: da tocha acesa na mão e da fogueira acesa (a luz que se vê vai a 14 e 16 m).")]
        public float raioDaTocha = 4f;
        public float raioDaFogueira = 8f;
        [Tooltip("A horda que cria coragem: com esta quantidade ou mais rodeando o mesmo fogo por este tempo (s), todos entram.")]
        public int hordaQueEntraNoFogo = 5;
        public float esperaAteACoragem = 30f;
        [Tooltip("Quanto dura a coragem (s): nesse tempo ele não liga para fogo nenhum.")]
        public float tempoDeCoragem = 45f;
        [Tooltip("À noite, numa fogueira, a coragem espera mais (s): o Long da fogueira vem antes (ZombieManager.LongNaFogueira.cs).")]
        public float esperaAteACoragemDeNoiteNaFogueira = 90f;

        [Header("O fogo, etapa 4 (11/11/2026: o lampião, o molotov e a Muralha de fogo; Player/FogoDoJogador.cs)")]
        [Tooltip("Até onde (m) eles não chegam do lampião aceso (na mão ou no chão) e da área de fogo do molotov (que assusta como uma fogueira).")]
        public float raioDoLampiao = 4f;
        public float raioDoMolotov = 8f;
        [Tooltip("A Muralha de fogo: quem está dentro do fogo foge em pânico até esta distância (m) além da beira, nesta velocidade (m/s).")]
        public float distanciaDoPanico = 6f;
        public float velocidadeDoPanico = 4f;

        [Header("O Long da fogueira (05/11/2026; ZombieManager.LongNaFogueira.cs)")]
        [Tooltip("À noite, com pelo menos esta quantidade de zumbis caçando em volta de uma fogueira acesa...")]
        public int zumbisParaOLong = 6;
        [Tooltip("... e um jogador parado dentro dela por este tempo (s), um Long nasce perto e vem apagá-la.")]
        public float paradoParaOLong = 60f;
        [Tooltip("Depois de um Long da fogueira, a mesma fogueira espera este tempo (s) para chamar outro.")]
        public float recargaDoLongDaFogueira = 180f;

        [Header("O sem cabeça (07/11/2026; Zumbi.SemComando.cs)")]
        [Tooltip("No tiro que estoura a cabeça e mata, a chance de ele ainda andar um pouco sem ela.")]
        [Range(0f, 1f)] public float chanceDeAndarSemCabeca = 0.15f;
        [Tooltip("Quanto tempo (s) ele anda sem rumo antes de desligar (a pedido em 07/11/2026: \"uns 3 segundos apenas\").")]
        public Vector2 tempoSemCabeca = new Vector2(2.5f, 3.5f);
        [Tooltip("A chance de ele bater em quem encostar na frente (às cegas), a cada golpe que poderia dar.")]
        [Range(0f, 1f)] public float chanceDoGolpeSemCabeca = 0.35f;

        [Header("Os especiais (01/11/2026; TipoDeZumbi.cs)")]
        public AjustesDoCorredor corredor = new AjustesDoCorredor();
        [Tooltip("O Long (o cuspidor).")]
        public AjustesDoLong cuspidor = new AjustesDoLong();
        public AjustesDoBrutamonte brutamonte = new AjustesDoBrutamonte();
    }
}
