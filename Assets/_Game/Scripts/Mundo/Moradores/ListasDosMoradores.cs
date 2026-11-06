using System;
using System.Collections.Generic;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Uma história, de antes do apocalipse ou dos anos dele (a decisão 2 do Plano_NPCs_Bunker.md da pasta de arte).</summary>
    public sealed class HistoriaDoMorador
    {
        public string chave, nomeM, nomeF;
        public int idadeMin, idadeMax;      // a idade de hoje (5 anos depois do começo)
        public int inicio;                  // a idade em que ele começou no ofício (para os anos da frase)
        public string roupa;                // o tipo do sorteio das roupas (só nas de antes)
        public (Habilidade h, int pontos)[] bonus = new (Habilidade, int)[0];
        public Habilidade? paixao;          // a habilidade do ofício: a paixão sai nela mais vezes
        public Incapacidade naoFaz;
        public float chanceDoNaoFaz = 1f;
        public string puxa;                 // o traço que ela puxa (metade das vezes)
        public float peso = 1f;
        public string frase;                // {N} o nome, {o} o ou a, {anos} os anos no ofício, {escola} no colégio ou na faculdade

        public string Nome(bool mulher) => mulher ? nomeF : nomeM;
    }

    /// <summary>Um traço (a decisão 4): o nome de cada sexo, o do par (os dois não vêm juntos), o peso no sorteio e o que ele faz.</summary>
    public sealed class TracoDoMorador
    {
        public string chave, nomeM, nomeF, oposto, efeito;
        public float peso = 1f;
        public Func<int, float> pelaIdade;  // o peso vezes isto, pela idade (null: 1)

        public string Nome(bool mulher) => mulher ? nomeF : nomeM;
        public float Peso(int idade) => peso * (pelaIdade?.Invoke(idade) ?? 1f);
    }

    /// <summary>Uma condição (a decisão 5): uma deficiência ou um problema de saúde, com o peso pela idade e o que ela faz.</summary>
    public sealed class CondicaoDoMorador
    {
        public string chave, nome, efeito;
        public Func<int, float> peso;
    }

    /// <summary>
    /// As listas dos moradores (a parte 2 do Plano_NPCs_Bunker.md da pasta de arte): os nomes (em inglês: o jogo se passa
    /// nos Estados Unidos; os velhos com nomes de velho), os sobrenomes, os apelidos, as histórias de antes e dos anos do
    /// apocalipse, os traços e as condições. As chaves vão no save: não mude uma chave, só os textos.
    /// </summary>
    public static class ListasDosMoradores
    {
        public const int Habilidades = 8;

        public static readonly string[] NomesDasHabilidades =
            { "Combate", "Cozinha", "Medicina", "Social", "Mecânica", "Construção", "Plantio", "Ofício" };

        /// <summary>O "não faz" que tira cada habilidade (a Cozinha de quem não cozinha).</summary>
        public static Incapacidade Bloqueio(Habilidade h) => h switch
        {
            Habilidade.Cozinha => Incapacidade.NaoCozinha,
            Habilidade.Combate => Incapacidade.NaoLuta,
            Habilidade.Medicina => Incapacidade.NaoCuidaDeDoentes,
            _ => Incapacidade.Nenhuma,
        };

        public static readonly (Incapacidade i, string nome)[] NomesDoNaoFaz =
        {
            (Incapacidade.NaoCozinha, "Não cozinha"), (Incapacidade.NadaDeTrabalhoPesado, "Nada de trabalho pesado"),
            (Incapacidade.NaoCuidaDeDoentes, "Não cuida de doentes"), (Incapacidade.NaoLuta, "Não luta"),
        };

        // ------------------------------------------------------------------------------------------------- os nomes
        public static readonly string[] HomensNovos =
        {
            "James", "John", "Michael", "David", "Chris", "Daniel", "Matthew", "Anthony", "Mark", "Josh", "Andrew", "Kevin",
            "Brian", "Ryan", "Jason", "Justin", "Brandon", "Tyler", "Kyle", "Eric", "Adam", "Nathan", "Aaron", "Sean",
            "Travis", "Cody", "Derek", "Luis", "Carlos", "Marcus", "Andre", "Jamal", "Darnell", "Kenji", "Tommy", "Danny",
            "Billy", "Jake", "Luke", "Owen", "Caleb", "Ethan", "Logan", "Mason", "Hunter", "Wyatt", "Cole", "Jesse",
            "Dylan", "Evan", "Gavin", "Miguel", "Diego", "Victor", "Hector", "Omar", "Samuel", "Isaac", "Leon", "Wesley",
            "Troy", "Dustin", "Shane", "Brett", "Grant", "Reggie", "Malik", "Terrence", "Trevor", "Joel",
        };

        public static readonly string[] MulheresNovas =
        {
            "Jennifer", "Jessica", "Ashley", "Sarah", "Amanda", "Emily", "Megan", "Rachel", "Lauren", "Hannah", "Brittany",
            "Samantha", "Nicole", "Stephanie", "Heather", "Amber", "Kayla", "Taylor", "Madison", "Chloe", "Olivia", "Emma",
            "Abigail", "Grace", "Lucy", "Rosa", "Maria", "Carmen", "Lupe", "Ana", "Keisha", "Tasha", "Jasmine", "Aaliyah",
            "Monique", "Mei", "Linh", "Kim", "Tiffany", "Crystal", "Erin", "Kelly", "Holly", "Molly", "Paige", "Brooke",
            "Kaitlyn", "Jenna", "Natalie", "Vanessa", "Alyssa", "Destiny", "Sierra", "Autumn", "Leah", "Faith", "Ruby",
            "Ivy", "Nora", "Claire", "Tessa", "Becky", "Sandy", "Wendy", "Tammy", "Dana", "Gina", "Lisa", "Tracy", "Ellie",
        };

        public static readonly string[] HomensVelhos =
        {
            "Walter", "Harold", "Frank", "George", "Raymond", "Earl", "Eugene", "Ralph", "Howard", "Clarence", "Herbert",
            "Arthur", "Lloyd", "Leonard", "Vernon", "Floyd", "Marvin", "Gerald", "Donald", "Ronald", "Roy", "Carl", "Dale",
            "Glenn", "Wayne", "Larry", "Gary", "Dennis", "Bobby", "Hank", "Bud", "Cecil", "Otis", "Willie", "Irving",
            "Morris", "Stanley", "Norman", "Russell", "Wendell", "Clyde", "Elmer", "Virgil", "Jimmy", "Ernest",
        };

        public static readonly string[] MulheresVelhas =
        {
            "Dorothy", "Martha", "Betty", "Margaret", "Helen", "Ruth", "Shirley", "Barbara", "Patricia", "Linda", "Carol",
            "Joan", "Judy", "Nancy", "Donna", "Peggy", "Gloria", "Phyllis", "Edna", "Mildred", "Ethel", "Agnes", "Irene",
            "Doris", "Marjorie", "Lois", "Beverly", "Darlene", "Loretta", "Rita", "Rose", "June", "Wanda", "Bonnie",
            "Connie", "Sue", "Jean", "Mabel", "Hazel", "Vera", "Opal", "Fern", "Iris", "Pearl", "Delores",
        };

        public static readonly string[] Sobrenomes =
        {
            "Hayes", "Cole", "Pruitt", "Medina", "Tran", "Doyle", "Ellis", "Walker", "Miller", "Johnson", "Smith", "Brown",
            "Davis", "Wilson", "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson",
            "Garcia", "Martinez", "Robinson", "Clark", "Rodriguez", "Lewis", "Lee", "Allen", "Young", "King", "Wright",
            "Scott", "Green", "Baker", "Adams", "Nelson", "Hill", "Campbell", "Mitchell", "Roberts", "Carter", "Phillips",
            "Evans", "Turner", "Torres", "Parker", "Collins", "Edwards", "Stewart", "Flores", "Morris", "Nguyen", "Murphy",
            "Rivera", "Cook", "Rogers", "Morgan", "Peterson", "Cooper", "Reed", "Bailey", "Bell", "Gomez", "Kelly",
            "Howard", "Ward", "Cox", "Diaz", "Richardson", "Wood", "Watson", "Brooks", "Bennett", "Gray", "Reyes", "Cruz",
            "Hughes", "Price", "Myers", "Long", "Foster", "Sanders", "Ross", "Morales", "Powell", "Sullivan", "Russell",
            "Ortiz", "Jenkins", "Perry", "Butler", "Barnes", "Fisher", "Henderson", "Coleman", "Simmons", "Patterson",
            "Jordan", "Reynolds", "Hamilton", "Graham", "Kim", "Alexander", "Ramos", "Wallace", "Griffin", "West",
            "Hawkins", "Dunn", "Harper", "Boone", "McCoy", "Becker", "Schultz", "Novak", "Kowalski", "O'Brien",
            "Gallagher", "Tanaka", "Park", "Chen", "Wade", "Pratt", "Crane", "Holt", "Fowler",
        };

        /// <summary>Os apelidos de cada nome (o Walter vira o "Walt").</summary>
        public static readonly Dictionary<string, string> ApelidosDoNome = new Dictionary<string, string>
        {
            { "Walter", "Walt" }, { "Harold", "Hal" }, { "Raymond", "Ray" }, { "Eugene", "Gene" }, { "Howard", "Howie" },
            { "Herbert", "Herb" }, { "Arthur", "Art" }, { "Leonard", "Lenny" }, { "Donald", "Don" }, { "Ronald", "Ron" },
            { "Stanley", "Stan" }, { "Norman", "Norm" }, { "Russell", "Rusty" }, { "Michael", "Mike" }, { "Daniel", "Dan" },
            { "Matthew", "Matt" }, { "Anthony", "Tony" }, { "Andrew", "Andy" }, { "Samuel", "Sam" }, { "Clarence", "Clay" },
            { "Dorothy", "Dot" }, { "Margaret", "Maggie" }, { "Patricia", "Patty" }, { "Barbara", "Barb" },
            { "Jennifer", "Jen" }, { "Jessica", "Jess" }, { "Samantha", "Sammy" }, { "Stephanie", "Steph" },
            { "Abigail", "Abby" }, { "Natalie", "Nat" }, { "Mildred", "Millie" }, { "Beverly", "Bev" }, { "Gloria", "Glo" },
        };

        /// <summary>Os apelidos do ofício (o médico vira o "Doc").</summary>
        public static readonly Dictionary<string, string> ApelidosDoOficio = new Dictionary<string, string>
        {
            { "Medico", "Doc" }, { "Enfermeiro", "Doc" }, { "Veterinario", "Doc" }, { "Soldado", "Sarge" },
            { "Cozinheiro", "Cookie" }, { "Pastor", "Preacher" }, { "GuiaDeCaca", "Hawk" }, { "ExPresidiario", "Lucky" },
            { "Caminhoneiro", "Diesel" }, { "Professor", "Teach" },
        };

        // ------------------------------------------------------------------------------------------ as histórias
        static HistoriaDoMorador A(string chave, string m, string f, int min, int max, int inicio, string roupa,
                                   Habilidade? paixao, float peso, string frase, string puxa = null,
                                   Incapacidade naoFaz = Incapacidade.Nenhuma, float chanceDoNaoFaz = 1f,
                                   params (Habilidade, int)[] bonus) => new HistoriaDoMorador
        {
            chave = chave, nomeM = m, nomeF = f, idadeMin = Math.Max(min, inicio + SorteioDosMoradores.AnosDoApocalipse + 1),
            idadeMax = max, inicio = inicio, roupa = roupa, paixao = paixao, peso = peso, frase = frase, puxa = puxa,
            naoFaz = naoFaz, chanceDoNaoFaz = chanceDoNaoFaz, bonus = bonus,
        };

        /// <summary>O que ele fazia antes do apocalipse (a primeira parte da história).</summary>
        public static readonly HistoriaDoMorador[] Antes =
        {
            A("Cozinheiro", "Cozinheiro de lanchonete", "Cozinheira de lanchonete", 22, 80, 18, "Civil", Habilidade.Cozinha, 1f,
              "{N} fritou hambúrguer numa lanchonete de beira de estrada por {anos}.", "Guloso",
              bonus: new[] { (Habilidade.Cozinha, 6), (Habilidade.Social, 1) }),
            A("Enfermeiro", "Enfermeiro", "Enfermeira", 24, 75, 22, "Médico", Habilidade.Medicina, 1f,
              "{N} trabalhou como enfermeir{o} no hospital da cidade por {anos}.", "Gentil",
              bonus: new[] { (Habilidade.Medicina, 6), (Habilidade.Social, 2) }),
            A("Medico", "Médico do hospital", "Médica do hospital", 30, 80, 26, "Médico", Habilidade.Medicina, 0.6f,
              "{N} atendeu no pronto-socorro por {anos} e nunca carregou nada mais pesado que uma maleta.",
              naoFaz: Incapacidade.NadaDeTrabalhoPesado, bonus: new[] { (Habilidade.Medicina, 8), (Habilidade.Social, 1) }),
            A("Mecanico", "Mecânico de oficina", "Mecânica de oficina", 20, 80, 18, "Trabalhador", Habilidade.Mecanica, 1.2f,
              "{N} consertou carro e trator numa oficina da cidade por {anos}.", "Trabalhador",
              bonus: new[] { (Habilidade.Mecanica, 7), (Habilidade.Construcao, 2) }),
            A("Pedreiro", "Pedreiro", "Mestre de obras", 20, 75, 18, "Trabalhador", Habilidade.Construcao, 1f,
              "{N} levantou casa e muro em metade do condado por {anos}.",
              bonus: new[] { (Habilidade.Construcao, 7), (Habilidade.Oficio, 1) }),
            A("Fazendeiro", "Fazendeiro", "Fazendeira", 20, 80, 14, "Fazendeiro", Habilidade.Plantio, 1.2f,
              "{N} cuidou da fazenda da família, com milho e gado, desde menin{o}.", "Trabalhador",
              bonus: new[] { (Habilidade.Plantio, 7), (Habilidade.Oficio, 2), (Habilidade.Mecanica, 1) }),
            A("Policial", "Policial", "Policial", 24, 70, 22, "Policial", Habilidade.Combate, 1f,
              "{N} foi policial do condado por {anos} e viu a cidade cair.", "Corajoso",
              bonus: new[] { (Habilidade.Combate, 6), (Habilidade.Social, 2) }),
            A("Soldado", "Soldado", "Soldado", 20, 65, 18, "Militar", Habilidade.Combate, 0.9f,
              "{N} serviu no Exército e estava de licença quando a quarentena fechou as estradas.", "SangueFrio",
              bonus: new[] { (Habilidade.Combate, 8), (Habilidade.Medicina, 1) }),
            A("Professor", "Professor", "Professora", 24, 80, 23, "Civil", Habilidade.Social, 1f,
              "{N} deu aula na escola da cidade por {anos}.", "Gentil",
              bonus: new[] { (Habilidade.Social, 5), (Habilidade.Medicina, 2), (Habilidade.Plantio, 1) }),
            A("Contador", "Contador", "Contadora", 24, 80, 23, "Civil", null, 0.8f,
              "{N} fez as contas de uma loja de móveis por {anos} e odiou cada minuto.", "Arrumado",
              Incapacidade.NadaDeTrabalhoPesado, 0.6f, (Habilidade.Social, 2), (Habilidade.Oficio, 1)),
            A("Caminhoneiro", "Caminhoneiro", "Caminhoneira", 25, 80, 21, "Trabalhador", Habilidade.Mecanica, 1f,
              "{N} dirigiu caminhão pelas rodovias do país por {anos}.", "Calado",
              bonus: new[] { (Habilidade.Mecanica, 4), (Habilidade.Combate, 2) }),
            A("Pastor", "Pastor", "Pastora", 30, 80, 25, "Civil", Habilidade.Social, 0.6f,
              "{N} cuidou de uma igrejinha de madeira na beira da estrada por {anos}.", "Religioso",
              Incapacidade.NaoLuta, 0.5f, (Habilidade.Social, 6), (Habilidade.Medicina, 1)),
            A("Estudante", "Estudante", "Estudante", 16, 26, 0, "Estudante", null, 1f,
              "{N} estava {escola} quando tudo começou."),
            A("Aposentado", "Aposentado", "Aposentada", 62, 80, 0, "Idoso", null, 1.5f,
              "{N} já estava aposentad{o} e passava as tardes na varanda quando tudo começou."),
            A("Eletricista", "Eletricista", "Eletricista", 22, 75, 20, "Trabalhador", Habilidade.Construcao, 0.8f,
              "{N} puxou fio e trocou disjuntor na cidade inteira por {anos}.",
              bonus: new[] { (Habilidade.Construcao, 4), (Habilidade.Mecanica, 4) }),
            A("Bombeiro", "Bombeiro", "Bombeira", 22, 65, 20, "Trabalhador", Habilidade.Medicina, 0.7f,
              "{N} foi bombeir{o} voluntári{o} e tirou muita gente de casa pegando fogo.", "Corajoso",
              bonus: new[] { (Habilidade.Medicina, 3), (Habilidade.Construcao, 3), (Habilidade.Combate, 1) }),
            A("Garcom", "Garçom", "Garçonete", 18, 60, 16, "Civil", Habilidade.Social, 1f,
              "{N} serviu mesa num restaurante de beira de estrada e sabia o nome de meio mundo.", "Falador",
              bonus: new[] { (Habilidade.Social, 4), (Habilidade.Cozinha, 2) }),
            A("Pesquisador", "Pesquisador do laboratório", "Pesquisadora do laboratório", 26, 75, 25, "Médico", Habilidade.Medicina, 0.5f,
              "{N} trabalhava no laboratório murado da cidade e não fala do que viu lá dentro.", "Calado",
              bonus: new[] { (Habilidade.Medicina, 5), (Habilidade.Oficio, 2) }),
            A("GuiaDeCaca", "Guia de caça", "Guia de caça", 22, 75, 20, "Sobrevivente", Habilidade.Combate, 0.8f,
              "{N} levou turista para caçar alce nas montanhas por {anos}.",
              bonus: new[] { (Habilidade.Combate, 5), (Habilidade.Plantio, 2) }),
            A("DonoDeCasa", "Dono de casa", "Dona de casa", 25, 80, 20, "Civil", Habilidade.Cozinha, 1f,
              "{N} cuidava da casa e dos filhos, e a horta dos fundos era o orgulho da rua.",
              bonus: new[] { (Habilidade.Cozinha, 4), (Habilidade.Social, 2), (Habilidade.Plantio, 1) }),
            A("Vendedor", "Vendedor de carros", "Vendedora de carros", 20, 70, 19, "Civil", Habilidade.Social, 0.9f,
              "{N} vendeu carro usado por {anos} e conseguia vender areia no deserto.", "Falador",
              bonus: new[] { (Habilidade.Social, 5), (Habilidade.Mecanica, 1) }),
            A("Marceneiro", "Marceneiro", "Marceneira", 22, 80, 18, "Trabalhador", Habilidade.Oficio, 0.9f,
              "{N} fez móveis numa marcenaria por {anos} e tem as mãos cheias de calo.",
              bonus: new[] { (Habilidade.Oficio, 6), (Habilidade.Construcao, 3) }),
            A("ExPresidiario", "Ex-presidiário", "Ex-presidiária", 20, 65, 18, "Sobrevivente", Habilidade.Combate, 0.5f,
              "{N} tinha acabado de sair da cadeia do condado quando o mundo acabou.", "Grosso",
              bonus: new[] { (Habilidade.Combate, 4), (Habilidade.Oficio, 1), (Habilidade.Social, -2) }),
            A("DonoDeBar", "Dono de bar", "Dona de bar", 25, 75, 25, "Civil", Habilidade.Social, 0.7f,
              "{N} teve um bar na rua principal por {anos}, e meio condado ainda devia uma conta lá.", "Falador",
              bonus: new[] { (Habilidade.Social, 4), (Habilidade.Cozinha, 2) }),
            A("Veterinario", "Veterinário", "Veterinária", 26, 75, 25, "Fazendeiro", Habilidade.Medicina, 0.6f,
              "{N} cuidou de vaca e cavalo nas fazendas da região por {anos}.",
              bonus: new[] { (Habilidade.Medicina, 4), (Habilidade.Plantio, 2) }),
        };

        static HistoriaDoMorador D(string chave, string m, string f, float peso, string frase, string puxa = null,
                                   params (Habilidade, int)[] bonus) => new HistoriaDoMorador
        {
            chave = chave, nomeM = m, nomeF = f, idadeMin = SorteioDosMoradores.IdadeMinima, idadeMax = SorteioDosMoradores.IdadeMaxima,
            peso = peso, frase = frase, puxa = puxa, bonus = bonus,
        };

        public const string ComAFamilia = "ComAFamilia";

        /// <summary>Como ele sobreviveu aos anos do apocalipse e chegou ao bunker (a segunda parte da história).</summary>
        public static readonly HistoriaDoMorador[] Depois =
        {
            D(ComAFamilia, "Entrou no bunker com a família no Dia 0", "Entrou no bunker com a família no Dia 0", 0.6f,
              "{N} desceu para o bunker com a família no Dia 0 e quase não saiu de lá desde então.", null,
              (Habilidade.Cozinha, 2), (Habilidade.Social, 2)),
            D("SozinhoNoMato", "Sobreviveu sozinho no mato", "Sobreviveu sozinha no mato", 1f,
              "{N} sobreviveu sozinh{o} no mato, comendo o que achava, até ver a fumaça do bunker.", "Calado",
              (Habilidade.Combate, 3), (Habilidade.Plantio, 3)),
            D("AcampamentoCaiu", "Veio de um acampamento que caiu", "Veio de um acampamento que caiu", 1f,
              "{N} morava num acampamento perto da rodovia, até a horda passar por lá.", null,
              (Habilidade.Construcao, 3), (Habilidade.Social, 2)),
            D("Saqueador", "Saqueou as cidades", "Saqueou as cidades", 0.7f,
              "{N} viveu de saquear as casas abandonadas e não tem orgulho disso.", "MaoLeve",
              (Habilidade.Combate, 3), (Habilidade.Social, -2)),
            D("Feridos", "Cuidou dos feridos de um abrigo", "Cuidou dos feridos de um abrigo", 0.8f,
              "{N} costurou ferida e baixou febre num abrigo de igreja, quase sem remédio.", "Gentil",
              (Habilidade.Medicina, 4)),
            D("FerroVelho", "Catou ferro-velho", "Catou ferro-velho", 0.8f,
              "{N} se escondeu num ferro-velho e montava o que precisava com a sucata.", null,
              (Habilidade.Mecanica, 3), (Habilidade.Oficio, 2)),
            D("Porao", "Ficou escondido num porão", "Ficou escondida num porão", 0.8f,
              "{N} passou meses trancad{o} num porão, ouvindo os zumbis lá em cima.", "Medroso"),
            D("CacouZumbis", "Caçou zumbis", "Caçou zumbis", 0.7f,
              "{N} limpou casa por casa em troca de comida e perdeu a conta de quantos derrubou.", "SangueFrio",
              (Habilidade.Combate, 4)),
            D("Resgatado", "Foi resgatado pela família", "Foi resgatada pela família", 0.9f,
              "{N} estava pres{o} no telhado de um mercado quando a família passou de carro e {o} tirou de lá.", null,
              (Habilidade.Social, 2)),
            D("AbrigoDoGoverno", "Fugiu do abrigo do governo", "Fugiu do abrigo do governo", 0.8f,
              "{N} ficou num abrigo do governo até os soldados irem embora no meio da noite.", "Pessimista",
              (Habilidade.Social, 1), (Habilidade.Medicina, 1)),
            D("Estrada", "Viveu na estrada", "Viveu na estrada", 0.8f,
              "{N} rodou de carro de cidade em cidade até a gasolina acabar.", null,
              (Habilidade.Mecanica, 2), (Habilidade.Combate, 2)),
            D("Fazenda", "Plantou escondido numa fazenda", "Plantou escondida numa fazenda", 0.8f,
              "{N} plantou escondid{o} numa fazenda isolada até os saqueadores acharem o lugar.", null,
              (Habilidade.Plantio, 4), (Habilidade.Cozinha, 1)),
            // a de quem era da família e ficou de fora do bunker na tela das cartas (04/11/2026): nunca sorteada
            D(SorteioDosMoradores.SeparadoDaFamilia, "Se perdeu da família no Dia 0", "Se perdeu da família no Dia 0", 0f,
              "{N} se perdeu da família no caos do Dia 0 e, desde então, procura por eles.", null,
              (Habilidade.Cozinha, 2), (Habilidade.Social, 2)),
        };

        // ------------------------------------------------------------------------------------------------ os traços
        public const string Fumante = "Fumante";

        static float Velho(int idade, float acima60) => idade >= 60 ? acima60 : 1f;

        static TracoDoMorador T(string chave, string m, string f, string oposto, float peso, string efeito,
                                Func<int, float> pelaIdade = null) =>
            new TracoDoMorador { chave = chave, nomeM = m, nomeF = f, oposto = oposto, peso = peso, efeito = efeito, pelaIdade = pelaIdade };

        /// <summary>Os traços (a decisão 4). O fumante é sorteado à parte (uns 40%), e ocupa uma das vagas.</summary>
        public static readonly TracoDoMorador[] Tracos =
        {
            T(Fumante, "Fumante", "Fumante", null, 0f, "Tem a vontade de fumar e fica triste sem cigarro."),
            T("Trabalhador", "Trabalhador", "Trabalhadora", "Preguicoso", 1f, "A função 20% mais rápida."),
            T("Preguicoso", "Preguiçoso", "Preguiçosa", "Trabalhador", 1f, "A função 20% mais devagar, e ele para mais para descansar."),
            T("Alegre", "Alegre", "Alegre", "Pessimista", 1f, "+10 no humor, sempre."),
            T("Pessimista", "Pessimista", "Pessimista", "Alegre", 1f, "−10 no humor, sempre."),
            T("Gentil", "Gentil", "Gentil", "Grosso", 1f, "A conversa dele dá +3 a quem ouve, e ele nunca insulta."),
            T("Grosso", "Grosso", "Grossa", "Gentil", 0.8f, "Insulta às vezes, e os outros gostam menos dele."),
            T("Corajoso", "Corajoso", "Corajosa", "Medroso", 1f, "Com um zumbi no bunker, fica perto dos outros, sem gritar."),
            T("Medroso", "Medroso", "Medrosa", "Corajoso", 0.8f, "Foge antes de todos, e o portão aberto deixa ele triste."),
            T("Guloso", "Guloso", "Gulosa", "Simples", 0.9f, "Come 50% mais e fica feliz com a fartura."),
            T("Simples", "Simples", "Simples", "Guloso", 0.9f, "Dormir mal e a comida fria pesam a metade."),
            T("Notivago", "Notívago", "Notívaga", null, 0.6f, "Dorme de dia e fica feliz de noite.", i => Velho(i, 0.4f)),
            T("SaudeDeFerro", "Saúde de ferro", "Saúde de ferro", "Fragil", 0.8f, "Adoece a metade.", i => i >= 65 ? 0.5f : 1f),
            T("Fragil", "Frágil", "Frágil", "SaudeDeFerro", 0.6f, "Adoece o dobro.", i => i >= 65 ? 2f : 1f),
            T("Durao", "Durão", "Durona", null, 0.7f, "A vida desce a metade com fome, com sede e doente."),
            T("SangueFrio", "Sangue frio", "Sangue frio", "Sensivel", 0.6f, "O luto pesa 30%."),
            T("Sensivel", "Sensível", "Sensível", "SangueFrio", 0.8f, "O luto pesa 150%."),
            T("Jogador", "Jogador", "Jogadora", null, 0.8f, "O lazer favorito é a sinuca, que vale o dobro."),
            T("Religioso", "Religioso", "Religiosa", null, 0.7f, "Reza de manhã e à noite, e o luto passa mais rápido.", i => i >= 55 ? 1.6f : 1f),
            T("Arrumado", "Arrumado", "Arrumada", null, 0.8f, "Fica triste com itens no chão do bunker e organiza mais rápido."),
            T("Falador", "Falador", "Faladora", "Calado", 0.9f, "Puxa conversa o dobro e sente falta de conversar."),
            T("Calado", "Calado", "Calada", "Falador", 0.9f, "Conversa pouco e não sente falta."),
            T("Rapido", "Rápido", "Rápida", "Lento", 0.7f, "Anda 20% mais rápido.", i => Velho(i, 0.2f)),
            T("Lento", "Lento", "Lenta", "Rapido", 0.6f, "Anda 20% mais devagar.", i => Velho(i, 2.5f)),
            T("Acumulador", "Acumulador", "Acumuladora", null, 0.6f, "A fartura e a escassez valem o dobro."),
            T("MaoLeve", "Mão leve", "Mão leve", null, 0.4f, "Às vezes guarda um item do estoque e não devolve."),
        };

        // ----------------------------------------------------------------------------------------- as condições
        static CondicaoDoMorador C(string chave, string nome, string efeito, float novo, float meio, float velho, int desde = 0) =>
            new CondicaoDoMorador
            {
                chave = chave, nome = nome, efeito = efeito,
                peso = i => i < desde ? 0f : i <= 30 ? novo : i <= 55 ? meio : velho,
            };

        /// <summary>As condições (a decisão 5): o peso de cada uma até 30 anos, de 31 a 55 e acima.</summary>
        public static readonly CondicaoDoMorador[] Condicoes =
        {
            C("ColunaRuim", "Coluna ruim", "4 vagas em vez de 6 e o trabalho pesado 25% mais devagar.", 0.5f, 2f, 3f),
            C("JoelhoRuim", "Joelho ruim", "Anda 25% mais devagar, mancando.", 0.3f, 1f, 3f),
            C("VistaFraca", "Vista fraca", "O trabalho de perto 15% mais devagar e a tacada da sinuca mais torta.", 0.2f, 1f, 3.5f),
            C("Surdez", "Surdez", "Não ouve o portão nem os gritos.", 0.2f, 0.6f, 2.5f),
            C("Asma", "Asma", "Cansa mais rápido, e a gripe vira grave o dobro.", 2f, 1.5f, 1.5f),
            C("Diabetes", "Diabetes", "Precisa de um comprimido por dia, do estoque.", 0.3f, 1.5f, 2f),
            C("CoracaoFraco", "Coração fraco", "Nada de trabalho pesado, e o grave é mais perigoso.", 0f, 0.8f, 2.5f, 40),
            C("Insonia", "Insônia", "Dorme 2 horas a menos, e uma noite em cada três ele dorme mal.", 1.5f, 1.5f, 1.5f),
        };

        /// <summary>A chance de ter uma condição pela idade (a tabela da decisão 5) e a de ter uma segunda.</summary>
        public static (float uma, float segunda) ChanceDeCondicao(int idade) =>
            idade <= 30 ? (0.10f, 0.05f) : idade <= 55 ? (0.25f, 0.05f) : idade <= 70 ? (0.5f, 0.15f) : (0.75f, 0.35f);

        // ------------------------------------------------------------------------------------------------- busca
        public static HistoriaDoMorador DeAntes(string chave) => Array.Find(Antes, h => h.chave == chave);
        public static HistoriaDoMorador DeDepois(string chave) => Array.Find(Depois, h => h.chave == chave);
        public static TracoDoMorador Traco(string chave) => Array.Find(Tracos, t => t.chave == chave);
        public static CondicaoDoMorador Condicao(string chave) => Array.Find(Condicoes, c => c.chave == chave);

        /// <summary>Os tipos de roupa do sorteio (os do <c>Roupas.json</c>).</summary>
        public static readonly string[] TiposDeRoupa =
            { "Sobrevivente", "Civil", "Trabalhador", "Policial", "Militar", "Médico", "Fazendeiro", "Idoso", "Estudante" };
    }
}
