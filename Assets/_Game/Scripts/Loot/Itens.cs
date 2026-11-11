using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O que vai na mochila (e na mão): uma arma (o tipo + 1), uma arma branca (a faca) ou a comida e a água; as caixas de munição (uma por
    /// calibre, só na grade: não vão na mão); e o que se veste (as 3 mochilas), que não vai na grade nem na mão; as peças
    /// do carro; o saque; e a sinuca (a caixa das bolas, cheia ou vazia, só na grade, e os 4 tacos, que vão na mão). Vai
    /// pela rede num byte. O coldre (35) saiu do jogo em 31/10/2026 (a pedido: "eu quero tirar o sistema de coldre"); o
    /// número fica reservado.
    /// </summary>
    public enum Item : byte
    {
        Nada = 0, Pistola, Rifle, Submetralhadora, Espingarda, Sniper,
        // as armas de sucata (09/11/2026, o Armeiro de sucata; a submetralhadora não coube antes da faca: veja Itens.Arma)
        PistolaSucata = 6, RifleSucata = 7,
        // as armas brancas (o bastão, a lança e a tocha saem da fabricação)
        Faca = 8, Machado, Bastao, Lanca, Tocha,
        SubmetralhadoraSucata = 13,
        // o que se come e se bebe (a lata e a garrafa de água são as primeiras; as outras, do saque de 31/10/2026)
        Comida = 16, Agua, Feijao, Carne, Milho, Sopa, Salgadinho, Chocolate, BarraDeEnergia,
        RefrigeranteGarrafa, RefrigeranteLata, Energetico, CopoDeAgua, Leite, Suco, GarrafaTermica,
        MochilaPequena = 32, MochilaMedia, MochilaGrande, Coldre,
        Municao9mm = 48, Municao556, Municao12, Municao762,
        // a munição das armas de sucata (09/11/2026): a de prego e a de parafuso
        MunicaoPrego = 52, MunicaoParafuso = 53,
        Vela = 64, Bateria, Radiador, Pneu, Macaco, ChaveDaPerua, ChaveDoCupe,
        // as chaves das motos (06/11/2026, a pedido: cada moto com a sua chave)
        ChaveDaFatBoy = 71, ChaveDaCross = 72,
        // os remédios
        Remedio = 80, Comprimidos, KitMedico, Atadura, Gaze, Desinfetante, SprayAntisseptico, Antibiotico, Bandagem,
        // a tala (05/11/2026, o sistema de vida por membro: imobiliza o braço ou a perna quebrados)
        Tala = 89,
        // a bandagem antisséptica (05/11/2026, a etapa 2: 1 álcool + 3 bandagens = 3; protege a ferida da infecção)
        BandagemAntisseptica = 90,
        // o calmante (06/11/2026, a mente, a etapa 3: acalma por hora; dois seguidos dão sono, três fazem apagar)
        Calmante = 91,
        // os materiais (a matéria-prima da fabricação)
        Madeira = 96, Toras, Pano, Corda, FitaAdesiva, Ferramenta, PecasDeMetal, Fios, Pregos, Isca, VaraDePesca, KitDeReparo, Roupa,
        // arrombar as portas e as janelas trancadas (02/11/2026, o Plano_Portas.md da pasta de arte)
        Gazua = 109, PeDeCabra,
        // o que se monta no mundo (os kits: na mochila, um item; montados, a construção)
        Fogueira = 112, Cama, Barricada,
        // a fogueira portátil (11/11/2026, a etapa 4 do ramo Fogo: o kit do nó Fogueira portátil; montada, ela é uma construção
        // deste tipo, que queima uma hora e o E recolhe)
        KitFogueira = 115,
        // a luz (01/11/2026, a pedido: a lanterna só funciona achada, e gasta as pilhas)
        Lanterna = 116, Pilhas,
        // a gasolina (01/11/2026, a pedido: o galão é o saque que repõe o tanque do carro)
        Galao = 118,
        // o fumo (03/11/2026, o Plano_Cigarro.md da pasta de arte): o maço é a pilha dos cigarros
        MacoDeCigarros = 119,
        // a pesca (06/11/2026, a árvore de habilidades): os peixes do lago; e a pólvora, a munição do Armeiro
        PeixePequeno = 120, PeixeMedio, PeixeGrande, PeixeDourado, Polvora,
        // o peixe assado na fogueira (07/11/2026: o filé; o cru pode fazer mal)
        PeixeAssado = 125,
        // a sucata (09/11/2026, o Armeiro de sucata): o material das armas e da munição de sucata
        Sucata = 126,
        // as tábuas para barricada (11/11/2026, a etapa 5b: a Barricada de tábuas, pregada numa porta ou numa janela das casas)
        TabuasBarricada = 127,
        // a sinuca (era do 80 ao 85 até o merge com o saque, em 31/10/2026)
        CaixaDeBolas = 128, CaixaDeBolasVazia, TacoMadeira, TacoEscuro, TacoPreto, TacoVermelho,
        // as melhorias das armas de sucata (10/11/2026, a etapa 2 do Armeiro de sucata; a ordem do Combat.PecaDaMelhoria)
        SilenciadorPET = 140, SilenciadorFiltro, MiraDeFerro, MiraDePonto, Luneta, CarregadorLongo, CanoReforcado1, CanoReforcado2, CanoReforcado3,
        // a proteção de sucata (11/11/2026, a etapa 3 da Resistência; a ordem do Player.LugarDaProtecao): vestem-se no grupo PROTEÇÃO
        Capacete = 150, Colete, Bracadeiras, Caneleiras,
        // o fogo (11/11/2026, a etapa 4 do ramo Fogo da Resistência): o lampião e o coquetel molotov vão na mão
        Lampiao = 154, Molotov = 155,
        // as armadilhas (11/11/2026, a etapa 5a do ramo Armadilhas e defesas da Sobrevivência): montam-se no chão, como a
        // barricada (Itens.EhArmadilha; os números no Player/ArmadilhasDoJogador.cs)
        AlarmeLatas = 156, TapetePregos = 157, Estacas = 158, CercaFogo = 159,
        // as roupas (31/10/2026, a parte 4 do Plano_Personagem.md da pasta de arte): uma por peça do catálogo das roupas,
        // daqui em diante (Itens.Roupa; 73 hoje: até o 232)
        PrimeiraRoupa = 160,
    }

    /// <summary>Uma coisa da sinuca (a caixa das bolas e os tacos): o nome do modelo (Sinuca.fbx, Icone_ e Foto_), o tamanho na mochila e o resumo.</summary>
    public class DadosDaSinuca
    {
        public string modelo, nome, nomeComArtigo, resumo;
        public UnityEngine.Vector2Int tamanho;
    }

    /// <summary>A espécie de um item do saque (o fundo dele na mochila e o que se faz com ele).</summary>
    public enum TipoDeLoot : byte { Comida, Bebida, Remedio, Material, Construcao }

    /// <summary>
    /// Uma peça do carro (as do motor, achadas no capô dos carros da cidade, e o pneu): o nome do modelo (Pecas.fbx, Icone_ e Foto_), o tamanho
    /// na mochila e o resumo. O estado dela (0 a 100%) vai no <c>qtd</c> do item.
    /// </summary>
    public class DadosDaPeca
    {
        public string modelo, nome, nomeComArtigo, resumo, funcao;
        public UnityEngine.Vector2Int tamanho;
    }

    /// <summary>Uma caixa de munição: o nome do modelo, o nome do calibre e o resumo.</summary>
    public class DadosDaMunicao
    {
        public string modelo, calibre, resumo;
        /// <summary>O nome do item, quando não é "caixa de &lt;calibre&gt;" (a munição de sucata, que vem numa lata ou numa caixinha).</summary>
        public string nome, nomeComArtigo;
        /// <summary>Quantas balas uma caixa achada costuma ter (o anfitrião sorteia entre os dois).</summary>
        public int achadaMin, achadaMax;
    }

    /// <summary>Um equipamento: o nome do modelo, o resumo e, nas mochilas, o espaço da grade (colunas × linhas).</summary>
    public class DadosDoEquipamento
    {
        public string modelo, nome, nomeComArtigo, resumo;
        public UnityEngine.Vector2Int espaco;
    }

    /// <summary>
    /// Uma arma branca (31/10/2026, o sistema básico): o nome, o dano de um golpe (na escala de 100: a vida do Oco), até
    /// onde ele chega do olho, a grossura dele (o raio da esfera que procura o alvo), o tempo entre um golpe e o próximo,
    /// quando ele pega (s depois do clique: o quadro do impacto no clipe), se ele acerta o braço ou a perna onde pega (o
    /// membro: arranca) ou conta como o tronco, e o corte (quanto ele vale contra a vida do membro: o machado, 2; o que
    /// não tem fio, 0, e também não explode a cabeça no golpe que mata, a pedido em 31/10/2026: "só tiros e cortes"). A
    /// cabeça vale 3 vezes em todas.
    /// </summary>
    public class DadosDaBranca
    {
        public string nome, nomeComArtigo;
        public float dano, alcance, raio, intervalo, impacto;
        public bool porRegiao;
        public float corte = 1f;
    }

    /// <summary>
    /// Um item do saque (31/10/2026: a comida, a bebida, os remédios, os materiais e os kits de montar): o modelo
    /// (Loot.fbx, &lt;modelo&gt;_*; Icone_&lt;modelo&gt; e Foto_&lt;modelo&gt;, feitos pelo Blender/loot.py; vazio: o
    /// de <see cref="Itens.Modelos"/>, a lata e a garrafa de água), os nomes, a espécie, quantos cabem numa célula e, no
    /// que se consome, o verbo e o efeito.
    /// </summary>
    public class DadosDoLoot
    {
        public Item item;
        public string modelo, nome, nomeComArtigo, plural, resumo;
        public TipoDeLoot tipo;
        /// <summary>Quantos numa célula (uma pilha; 1, sem pilha).</summary>
        public int pilha = 1;
        /// <summary>Comer, beber ou usar (vazio: não se consome).</summary>
        public string verbo;
        /// <summary>Quanto sobe a barra da fome e a da sede (de 0 a 100; a sede pode descer: o salgadinho) e a vida.</summary>
        public float fome, sede, vida;
        /// <summary>O clipe Usar inteiro (s): o item só acaba no fim, e o efeito vem junto.</summary>
        public float tempoDeUso;
        /// <summary>
        /// O modelo de mão (o índice em <see cref="Itens.Modelos"/>: os braços da lata ou os da garrafa, com a malha dele
        /// no lugar), ou -1: não vai na mão nem nos atalhos.
        /// </summary>
        public int mao = -1;
    }

    public static partial class Itens
    {
        /// <summary>
        /// Os modelos de tudo o que vai na mão: as armas (na ordem de <see cref="TipoDeArma"/>), a lata, a garrafa e as
        /// armas brancas, a faca e o machado (Jogador_FP_&lt;nome&gt;.fbx, Item_&lt;nome&gt;, Icone_&lt;nome&gt;; as
        /// brancas, de 31/10/2026, saem do Blender/Armas/faca.py e machado.py, com a malha avulsa no Itens_&lt;nome&gt;.fbx).
        /// </summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper", "Sucata_Pistola", "Sucata_Rifle",
                                                    "Sucata_Submetralhadora", "Lata", "Garrafa", "Faca", "Machado", "Bastao", "Lanca", "Tocha" };
        public const int QuantosModelos = Armas.Quantas + 7;

        /// <summary>
        /// As armas brancas, na ordem do enum (de Faca em diante), com o dano na faixa das armas de fogo (31/10/2026, a
        /// pedido: "não quero que ultrapasse muito o dano de armas de fogo"; a faca é a pistola, o machado é o rifle). A
        /// cabeça vale 3 vezes em todas. A faca: o soco direito com ela, rápido e curto, no tronco (7 golpes no corpo, 3 na
        /// cabeça). O machado (as duas mãos, a machadada na diagonal; o golpe pega no quadro 8 do clipe): lento e mais
        /// longe, na parte do corpo onde pega, e corta em dobro (decepa um braço ou uma perna com um golpe; 4 no corpo, 2 na
        /// cabeça).
        /// </summary>
        static readonly DadosDaBranca[] _brancas =
        {
            new DadosDaBranca { nome = "faca", nomeComArtigo = "a faca", dano = 15f, alcance = 1.35f, raio = 0.12f, intervalo = 0.5f, impacto = SomDoSoco.Impacto },
            new DadosDaBranca { nome = "machado", nomeComArtigo = "o machado", dano = 25f, alcance = 1.6f, raio = 0.18f, intervalo = 0.9f, impacto = 7 / 24f, porRegiao = true, corte = 2f },
            // os da fabricação (31/10/2026): o bastão com pregos (a pancada do machado, no tronco: 5 no corpo, 2 na cabeça),
            // a lança (a estocada, mais longe e fina, na parte onde pega) e a tocha (o soco direito com ela: pouco dano; ela
            // ilumina)
            new DadosDaBranca { nome = "bastão com pregos", nomeComArtigo = "o bastão com pregos", dano = 20f, alcance = 1.55f, raio = 0.17f, intervalo = 0.8f, impacto = 7 / 24f, corte = 0f },
            new DadosDaBranca { nome = "lança", nomeComArtigo = "a lança", dano = 20f, alcance = 2.1f, raio = 0.08f, intervalo = 0.85f, impacto = 6 / 24f, porRegiao = true },
            new DadosDaBranca { nome = "tocha", nomeComArtigo = "a tocha", dano = 8f, alcance = 1.4f, raio = 0.14f, intervalo = 0.5f, impacto = SomDoSoco.Impacto, corte = 0f },
        };
        /// <summary>Os números da arma branca (null: não é uma).</summary>
        public static DadosDaBranca Branca(Item i) => EhBranca(i) ? _brancas[i - Item.Faca] : null;

        /// <summary>
        /// Na primeira pessoa há mais dois modelos, depois dos de <see cref="Modelos"/>: os punhos da mão vazia
        /// (Jogador_FP_Maos.fbx, para o soco) e a mão com o taco de sinuca em pé (Jogador_FP_Taco.fbx, sem o taco: o
        /// <c>TacoNaMao</c> põe o de cada cor no osso gun).
        /// </summary>
        public const int Punhos = QuantosModelos;
        public const int TacoFP = QuantosModelos + 1;
        /// <summary>
        /// O fumo (03/11/2026): a mão com o maço (acender) e a mão com o cigarro aceso (tragar e o peteleco), os dois do
        /// Jogador_FP_Cigarro.fbx (o bloco_cigarro.py da pasta de arte), com os clipes de cada um.
        /// </summary>
        public const int MacoFP = QuantosModelos + 2;
        public const int CigarroFP = QuantosModelos + 3;
        public const int QuantosModelosFP = QuantosModelos + 4;
        /// <summary>
        /// O lampião e o molotov (11/11/2026, a etapa 4): dois modelos de primeira pessoa depois dos do prefab do jogador,
        /// postos no Play pelo <see cref="Player.ArmaJogador"/> (do Resources/ItensDoFogo: o prefab não muda).
        /// </summary>
        public const int LampiaoFP = QuantosModelosFP, MolotovFP = QuantosModelosFP + 1;
        public static string ModeloFP(int modelo) => modelo == Punhos ? "Maos" : modelo == TacoFP ? "Taco" : modelo == MacoFP ? "Maco"
            : modelo == CigarroFP ? "Cigarro" : Modelos[modelo];
        /// <summary>
        /// O nome do FBX dos braços (Jogador_FP_&lt;nome&gt;.fbx) e dos objetos dentro dele: o do modelo, ou "Cigarro" no fumo. As
        /// armas de sucata usam os braços da real (com a malha delas no lugar da arma).
        /// </summary>
        public static string ArquivoFP(int modelo) => modelo == MacoFP || modelo == CigarroFP ? "Cigarro"
            : modelo >= 0 && modelo < Armas.Quantas ? Armas.Modelos[(int)Armas.Real((TipoDeArma)modelo)] : ModeloFP(modelo);
        /// <summary>
        /// O modelo de primeira pessoa do que está na mão (os punhos, com a mão vazia; a mão com o taco; com o maço, a mão
        /// com ele: o cigarro aceso troca para o <see cref="CigarroFP"/>, no FumoJogador).
        /// </summary>
        public static int ModeloNaMao(Item i) => i == Item.Nada ? Punhos : EhTaco(i) ? TacoFP : EhFumo(i) ? MacoFP
            : i == Item.Lampiao ? LampiaoFP : i == Item.Molotov ? MolotovFP : MaoDe(i);
        /// <summary>O maço de cigarros (03/11/2026): vai na mão, e o clique acende um cigarro.</summary>
        public static bool EhFumo(Item i) => i == Item.MacoDeCigarros;

        // os modelos de mão dos comestíveis e dos remédios (os clipes Usar: itens.py, 40 e 44 quadros a 24 fps)
        const int MaoDaLata = Armas.Quantas, MaoDaGarrafa = Armas.Quantas + 1;
        const float UsoDaLata = 40 / 24f, UsoDaGarrafa = 44 / 24f;

        static DadosDoLoot Comer(Item i, string modelo, string nome, string artigo, string plural, string resumo, int pilha, float fome, float sede, float vida = 0f) =>
            new DadosDoLoot { item = i, modelo = modelo, nome = nome, nomeComArtigo = artigo, plural = plural, resumo = resumo, tipo = TipoDeLoot.Comida, pilha = pilha,
                              verbo = "comer", fome = fome, sede = sede, vida = vida, tempoDeUso = UsoDaLata, mao = MaoDaLata };
        static DadosDoLoot Beber(Item i, string modelo, string nome, string artigo, string plural, string resumo, int pilha, float fome, float sede, float vida = 0f) =>
            new DadosDoLoot { item = i, modelo = modelo, nome = nome, nomeComArtigo = artigo, plural = plural, resumo = resumo, tipo = TipoDeLoot.Bebida, pilha = pilha,
                              verbo = "beber", fome = fome, sede = sede, vida = vida, tempoDeUso = UsoDaGarrafa, mao = MaoDaGarrafa };
        static DadosDoLoot Remedio(Item i, string modelo, string nome, string artigo, string plural, string resumo, int pilha, float vida) =>
            new DadosDoLoot { item = i, modelo = modelo, nome = nome, nomeComArtigo = artigo, plural = plural, resumo = resumo, tipo = TipoDeLoot.Remedio, pilha = pilha,
                              verbo = "usar", vida = vida, tempoDeUso = UsoDaLata, mao = MaoDaLata };
        // o item que vai na mão no lugar da lata (a vara de pesca, 06/11/2026)
        static DadosDoLoot NaMao(DadosDoLoot d)
        {
            d.mao = MaoDaLata;
            return d;
        }

        static DadosDoLoot Material(Item i, string modelo, string nome, string artigo, string plural, string resumo, int pilha, TipoDeLoot tipo = TipoDeLoot.Material) =>
            new DadosDoLoot { item = i, modelo = modelo, nome = nome, nomeComArtigo = artigo, plural = plural, resumo = resumo, tipo = tipo, pilha = pilha };

        /// <summary>
        /// O saque (31/10/2026, a pedido: "precisamos criar alguns loots ... mais variações de latas de comida e de água,
        /// refrigerantes e algumas matérias-primas para craft"; cada um numa célula, por enquanto). A fome e a sede vão de 0
        /// a 100. Desde 05/11/2026 (a pedido: "a vida não se regenera mais sozinha"), a comida não dá vida, e só o tratamento
        /// cura: a atadura, a bandagem de pano, o kit médico, a tala e os analgésicos vão pelo corpo (os números estão no
        /// Player/VidaJogador.Corpo.cs, não no <c>vida</c> daqui). A gaze e o álcool (o Desinfetante) viraram material das
        /// receitas. Na etapa 2, a bandagem antisséptica, o spray, o antibiótico e o xarope também vão pelo corpo.
        /// </summary>
        static readonly DadosDoLoot[] _loot =
        {
            // o que se come (o clipe da lata)
            Comer(Item.Comida, null, "lata de comida", "a lata de comida", "latas de comida", "Conserva. Mata a fome.", 3, 40f, 5f),
            Comer(Item.Feijao, "Feijao", "lata de feijão", "a lata de feijão", "latas de feijão", "Feijão com caldo. Sustenta.", 3, 45f, 5f),
            Comer(Item.Carne, "Carne", "carne enlatada", "a carne enlatada", "latas de carne", "Carne em conserva. A que mais sustenta.", 3, 50f, 0f),
            Comer(Item.Milho, "Milho", "lata de milho", "a lata de milho", "latas de milho", "Milho verde. Serve de isca também.", 3, 30f, 8f),
            Comer(Item.Sopa, "Sopa", "sopa enlatada", "a sopa enlatada", "latas de sopa", "Sopa pronta. Mata a fome e um pouco da sede.", 3, 30f, 15f),
            Comer(Item.Salgadinho, "Salgadinho", "salgadinho", "o salgadinho", "salgadinhos", "Um pacote. Salgado: dá sede.", 5, 15f, -5f),
            Comer(Item.Chocolate, "Chocolate", "chocolate", "o chocolate", "chocolates", "Uma barra. Um pouco de ânimo.", 5, 12f, 0f),
            Comer(Item.BarraDeEnergia, "BarraDeEnergia", "barra de energia", "a barra de energia", "barras de energia", "Cereal e mel. Leve e sustenta.", 5, 22f, 0f),
            // o que se bebe (o clipe da garrafa)
            Beber(Item.Agua, null, "garrafa de água", "a garrafa de água", "garrafas de água", "Água. Mata a sede.", 2, 0f, 45f),
            Beber(Item.CopoDeAgua, "CopoDeAgua", "copo de água", "o copo de água", "copos de água", "Copinho lacrado. Um gole só.", 5, 0f, 15f),
            Beber(Item.RefrigeranteGarrafa, "RefrigeranteGarrafa", "garrafa de refrigerante", "a garrafa de refrigerante", "garrafas de refrigerante", "Dois litros, quente e sem gás.", 2, 5f, 35f),
            Beber(Item.RefrigeranteLata, "RefrigeranteLata", "lata de refrigerante", "a lata de refrigerante", "latas de refrigerante", "Gelada um dia. Mata a sede.", 3, 3f, 25f),
            Beber(Item.Energetico, "Energetico", "energético", "o energético", "energéticos", "Lata de energético. Acorda.", 3, 2f, 20f),
            Beber(Item.Leite, "Leite", "caixa de leite", "a caixa de leite", "caixas de leite", "Longa vida. Mata a fome e a sede.", 2, 10f, 25f),
            Beber(Item.Suco, "Suco", "caixinha de suco", "a caixinha de suco", "caixinhas de suco", "Suco de caixinha, com canudo.", 3, 5f, 22f),
            Beber(Item.GarrafaTermica, "GarrafaTermica", "garrafa térmica", "a garrafa térmica", "garrafas térmicas", "Café, ainda morno. Esvaziada, vira sucata.", 1, 5f, 30f),
            // os remédios (o clipe da lata). Os tratamentos (05/11/2026) não usam o vida daqui: o que cada um faz no corpo
            // está no VidaJogador.Tratar (e o tempo, no PartesDoCorpo.TempoDe)
            Remedio(Item.Remedio, "Remedio", "xarope", "o xarope", "xaropes", "Tira a febre por umas 4 h. Não cura: só o antibiótico cura a infecção.", 5, 0f),
            Remedio(Item.Comprimidos, "Comprimidos", "cartela de comprimidos", "a cartela de comprimidos", "cartelas de comprimidos", "Analgésico: tira a dor por 6 h.", 10, 0f),
            Remedio(Item.KitMedico, "KitMedico", "kit médico", "o kit médico", "kits médicos", "Trata o corpo todo: estanca e cura muito.", 1, 0f),
            Remedio(Item.Atadura, "Atadura", "atadura", "a atadura", "ataduras", "Estanca uma ferida e cura um pouco.", 5, 0f),
            // a gaze e o álcool (05/11/2026): só material das receitas (não se usam sozinhos, nem vão na mão). O enum e o
            // modelo do álcool continuam "Desinfetante" (os saves e o loot.py); na tela, ele é o álcool (a pedido: "nomear
            // dessa maneira intuitiva")
            Material(Item.Gaze, "Gaze", "gaze", "a gaze", "gazes", "Para montar o kit médico.", 10, TipoDeLoot.Remedio),
            Material(Item.Desinfetante, "Desinfetante", "álcool", "o álcool", "frascos de álcool", "Álcool 70%. Para receitas: o kit médico e a bandagem antisséptica.", 3, TipoDeLoot.Remedio),
            // o spray e o antibiótico (a etapa 2): vão pelo corpo, como os outros tratamentos. O spray não empilha: cada
            // frasco guarda os seus usos no desgaste (Itens.ComUsos)
            Remedio(Item.SprayAntisseptico, "SprayAntisseptico", "spray antisséptico", "o spray antisséptico", "sprays antissépticos", "5 usos. Borrife na ferida antes da bandagem comum: ela fica protegida até o amanhecer.", 1, 0f),
            Remedio(Item.Antibiotico, "Antibiotico", "antibiótico", "o antibiótico", "antibióticos", "Uma dose a cada 6 h. Cura a infecção (nem sempre).", 5, 0f),
            Remedio(Item.Bandagem, "Bandagem", "bandagem", "a bandagem", "bandagens", "Pano em tiras: estanca uma ferida.", 5, 0f),
            // os materiais
            Material(Item.Madeira, "Madeira", "madeira", "a madeira", "madeiras", "Tábuas e ripas. Para quase tudo.", 10),
            Material(Item.Toras, "Toras", "tora", "a tora", "toras", "Lenha grossa. Rachada, vira madeira.", 4),
            Material(Item.Pano, "Pano", "pano", "o pano", "panos", "Retalhos. Bandagem, corda, mochila.", 10),
            Material(Item.Corda, "Corda", "corda", "a corda", "cordas", "Um rolo de corda.", 5),
            Material(Item.FitaAdesiva, "FitaAdesiva", "fita adesiva", "a fita adesiva", "fitas adesivas", "Silver tape. Conserta qualquer coisa.", 5),
            Material(Item.Ferramenta, "Ferramenta", "ferramenta", "a ferramenta", "ferramentas", "Martelo e chave. Não gasta: só precisa ter.", 3),
            Material(Item.PecasDeMetal, "PecasDeMetal", "peças de metal", "as peças de metal", "peças de metal", "Parafusos, molas e chapas.", 10),
            Material(Item.Fios, "Fios", "fios", "os fios", "fios", "Fio de cobre e linha.", 10),
            Material(Item.Pregos, "Pregos", "pregos", "os pregos", "pregos", "Um punhado de pregos.", 50),
            Material(Item.Isca, "Isca", "isca", "a isca", "iscas", "Para pescar: cada peixe (e cada um que escapa) gasta uma.", 20),
            // a vara vai na mão (06/11/2026, a pesca): no lugar da lata, com a malha dela
            NaMao(Material(Item.VaraDePesca, "VaraDePesca", "vara de pesca", "a vara de pesca", "varas de pesca",
                           "Na mão, com isca na mochila: clique mirando a água funda para lançar; quando a boia afundar, clique, e puxe na faixa verde.", 1)),
            Material(Item.KitDeReparo, "KitDeReparo", "kit de reparo", "o kit de reparo", "kits de reparo", "Conserta uma arma ou remenda um pneu furado.", 3),
            Material(Item.Roupa, "Roupa", "roupa", "a roupa", "roupas", "Roupa velha. Desmontada, vira pano.", 5),
            // os kits de montar (a fogueira, a cama e a barricada)
            Material(Item.Fogueira, "Fogueira", "fogueira", "a fogueira", "fogueiras", "Lenha arrumada. Monte no chão: luz, e os zumbis ficam longe.", 1, TipoDeLoot.Construcao),
            Material(Item.Cama, "Cama", "cama improvisada", "a cama improvisada", "camas improvisadas", "Monte no chão: você volta nela quando cair.", 1, TipoDeLoot.Construcao),
            Material(Item.Barricada, "Barricada", "barricada", "a barricada", "barricadas", "Tábuas pregadas. Monte numa porta: segura os zumbis.", 1, TipoDeLoot.Construcao),
            // a luz (01/11/2026): no fim da lista, para os índices dos outros modelos não mudarem
            Material(Item.Lanterna, "Lanterna", "lanterna", "a lanterna", "lanternas", "Vai presa no peito: F liga e desliga. Gasta as pilhas.", 1),
            Material(Item.Pilhas, "Pilhas", "par de pilhas", "o par de pilhas", "pares de pilhas", "Botão direito na lanterna: trocar as pilhas.", 6),
            // a gasolina (01/11/2026)
            Material(Item.Galao, "Galao", "galão de gasolina", "o galão de gasolina", "galões de gasolina", "10 litros. Na tampa do tanque do carro, E abastece.", 1),
            // arrombar (02/11/2026, o Plano_Portas.md da pasta de arte)
            Material(Item.Gazua, "Gazua", "gazua", "a gazua", "gazuas", "Na mochila: segure E numa porta trancada. Abre sem barulho, e às vezes quebra.", 5),
            Material(Item.PeDeCabra, "PeDeCabra", "pé de cabra", "o pé de cabra", "pés de cabra", "Na mochila: segure E numa porta trancada. Arromba rápido, com barulho.", 1),
            // a tala (05/11/2026, o sistema de vida por membro): no fim da lista, para os índices dos outros modelos não
            // mudarem (o modelo "Tala" sai do Blender/loot.py)
            Remedio(Item.Tala, "Tala", "tala", "a tala", "talas", "Imobiliza um braço ou uma perna quebrados, ou o tornozelo torcido. Com analgésico, sara mais rápido.", 3, 0f),
            // a bandagem antisséptica (05/11/2026, a etapa 2): no fim da lista, para os índices dos outros modelos não
            // mudarem (o modelo "BandagemAntisseptica" sai do Blender/loot.py)
            Remedio(Item.BandagemAntisseptica, "BandagemAntisseptica", "bandagem antisséptica", "a bandagem antisséptica", "bandagens antissépticas", "Com álcool: estanca e protege a ferida da infecção até o próximo amanhecer.", 5, 0f),
            // o fumo (03/11/2026, o Plano_Cigarro.md da pasta de arte): o número da pilha é quantos cigarros sobram (até 20)
            Material(Item.MacoDeCigarros, "MacoDeCigarros", "maço de cigarros", "o maço de cigarros", "maços de cigarros",
                     "Blackwater. Na mão, clique: acende um. De noite, a brasa chama os zumbis de mais longe.", 20),
            // o calmante (06/11/2026, a mente, a etapa 3): no fim da lista, para os índices dos outros modelos não mudarem (o
            // modelo "Calmante" sai do Blender/loot.py)
            Remedio(Item.Calmante, "Calmante", "calmante", "o calmante", "calmantes",
                    "Acalma a mente aos poucos, por umas 5 h, e passa a crise de pânico. Dois seguidos dão sono; três, você apaga.", 6, 0f),
            // a pesca (06/11/2026, a árvore de habilidades): os peixes (comem-se assim mesmo) e a pólvora, no fim da lista
            // (os modelos saem do Blender/loot.py)
            Comer(Item.PeixePequeno, "PeixePequeno", "lambari", "o lambari", "lambaris", "Um peixinho do lago. Mata um pouco a fome.", 4, 15f, 2f),
            Comer(Item.PeixeMedio, "PeixeMedio", "tilápia", "a tilápia", "tilápias", "Um peixe do lago. Mata bem a fome.", 3, 30f, 3f),
            Comer(Item.PeixeGrande, "PeixeGrande", "tucunaré", "o tucunaré", "tucunarés", "Um peixe grande do lago: a fome de quase um dia.", 2, 50f, 5f),
            Comer(Item.PeixeDourado, "PeixeDourado", "dourado", "o dourado", "dourados", "Raro, do fundo do lago. Os comerciantes pagam bem.", 1, 45f, 5f),
            Comer(Item.PeixeAssado, "PeixeAssado", "peixe assado", "o peixe assado", "peixes assados", "Assado na fogueira: mata a fome sem fazer mal.", 6, 20f, 0f),
            Material(Item.Polvora, "Polvora", "pólvora", "a pólvora", "latas de pólvora", "Na mesa de oficina, com o Armeiro: vira munição.", 5),
            // a sucata (09/11/2026, o Armeiro de sucata: o modelo é o Item_Sucata do Loot_Sucata.fbx, o Blender/sucata_itens.py)
            Material(Item.Sucata, "Sucata", "sucata", "a sucata", "pedaços de sucata",
                     "Metal velho. Na bancada e na mesa de oficina, vira arma e munição de sucata.", 20),
            // as melhorias das armas de sucata (10/11/2026, a etapa 2: os modelos são os Item_<modelo>_corpo do
            // Loot_Acessorios.fbx, o Blender/Armas/sucata_acessorios.py); põem-se na bancada, no painel MELHORIAS da aba Criar
            Material(Item.SilenciadorPET, "SilenciadorPET", "silenciador de garrafa PET", "o silenciador de garrafa PET", "silenciadores de garrafa PET",
                     "Garrafa e fita na boca do cano. Abafa o tiro, mas dura 15 tiros.", 1),
            Material(Item.SilenciadorFiltro, "SilenciadorFiltro", "silenciador de filtro de óleo", "o silenciador de filtro de óleo", "silenciadores de filtro de óleo",
                     "Um filtro de carro na boca do cano. Abafa o tiro por 80 tiros.", 1),
            Material(Item.MiraDeFerro, "MiraFerro", "mira de ferro ajustada", "a mira de ferro ajustada", "miras de ferro ajustadas",
                     "Alça e massa acertadas na lima. O tiro espalha menos.", 1),
            Material(Item.MiraDePonto, "MiraPonto", "mira de ponto caseira", "a mira de ponto caseira", "miras de ponto caseiras",
                     "Um tubinho com o ponto vermelho. Mirando, o tiro vai no ponto.", 1),
            Material(Item.Luneta, "Luneta", "luneta de cano", "a luneta de cano", "lunetas de cano",
                     "Só no rifle de sucata. Mirando, aproxima duas vezes e meia.", 1),
            Material(Item.CarregadorLongo, "CarregadorLongo", "carregador estendido", "o carregador estendido", "carregadores estendidos",
                     "A pistola de sucata leva 3 tiros; a submetralhadora, 32.", 1),
            Material(Item.CanoReforcado1, "CanoReforcado1", "cano reforçado I", "o cano reforçado I", "canos reforçados I",
                     "Luva de cano e braçadeiras: +8% de dano, e a arma gasta 10% mais.", 1),
            Material(Item.CanoReforcado2, "CanoReforcado2", "cano reforçado II", "o cano reforçado II", "canos reforçados II",
                     "Mais braçadeiras e a tala: +16% de dano, e a arma gasta 20% mais.", 1),
            Material(Item.CanoReforcado3, "CanoReforcado3", "cano reforçado III", "o cano reforçado III", "canos reforçados III",
                     "Com o fio de cobre: +24% de dano, e a arma gasta 30% mais.", 1),
            // a proteção de sucata (11/11/2026, a etapa 3: os modelos são os Item_<modelo>_corpo do Loot_Etapa34.fbx; no corpo,
            // o Protecao_Sucata.fbx); veste-se no grupo PROTEÇÃO da tela (os números no Player/Protecao.cs: os detalhes da tela
            // mostram o que cada uma cobre e corta)
            Material(Item.Capacete, "Capacete", "capacete de panela", "o capacete de panela", "capacetes de panela",
                     "Uma panela forrada, presa no queixo.", 1),
            Material(Item.Colete, "Colete", "colete de chapa", "o colete de chapa", "coletes de chapa",
                     "Chapas sobre a borracha de um pneu.", 1),
            Material(Item.Bracadeiras, "Bracadeiras", "braçadeiras de sucata", "as braçadeiras de sucata", "pares de braçadeiras de sucata",
                     "Latas e fita em volta dos antebraços.", 1),
            Material(Item.Caneleiras, "Caneleiras", "caneleiras de sucata", "as caneleiras de sucata", "pares de caneleiras de sucata",
                     "Chapas de lata presas nas canelas.", 1),
            // o fogo (11/11/2026, a etapa 4: os modelos são os Item_<modelo>_corpo do Loot_Etapa34.fbx; na mão, os braços do
            // Jogador_FP_Lampiao e do _Molotov, e no corpo as malhas do Fogo.fbx, todos do Resources/ItensDoFogo; os números
            // no Player/FogoDoJogador.cs)
            Material(Item.Lampiao, "Lampiao", "lampião", "o lampião", "lampiões",
                     "Combustível e pavio dentro do vidro. Na mão, luz quente, e os zumbis ficam longe; a chuva não apaga.", 1),
            Material(Item.Molotov, "Molotov", "coquetel molotov", "o coquetel molotov", "coquetéis molotov",
                     "Garrafa com combustível e um pano no gargalo. Na mão, o clique arremessa: o fogo pega onde ela quebra.", 1),
            Material(Item.KitFogueira, "KitFogueira", "kit da fogueira portátil", "o kit da fogueira portátil", "kits da fogueira portátil",
                     "Lenha amarrada e combustível. Monte no chão: queima uma hora, e E recolhe.", 1, TipoDeLoot.Construcao),
            // as armadilhas (11/11/2026, a etapa 5a: os modelos são os Item_<modelo>_corpo do Art/Armadilhas/Loot_Armadilhas.fbx,
            // e as montadas, as do Armadilhas.fbx, todas do Resources/ItensDasArmadilhas; o desgaste guarda os usos que já foram)
            Material(Item.AlarmeLatas, "AlarmeLatas", "alarme de latas", "o alarme de latas", "alarmes de latas",
                     "Latas num barbante entre duas estacas. Monte no chão: quem passa em pé faz barulho, e você fica sabendo.", 1, TipoDeLoot.Construcao),
            Material(Item.TapetePregos, "TapetePregos", "tapete de pregos", "o tapete de pregos", "tapetes de pregos",
                     "Uma tábua cheia de pregos virados para cima. Monte no chão: fere quem pisa e fura pneu.", 1, TipoDeLoot.Construcao),
            Material(Item.Estacas, "Estacas", "estacas", "as estacas", "feixes de estacas",
                     "Estacas pontudas amarradas numa tora. Monte no chão, com as pontas para fora: seguram e ferem os zumbis.", 1, TipoDeLoot.Construcao),
            Material(Item.CercaFogo, "CercaFogo", "cerca de fogo", "a cerca de fogo", "cercas de fogo",
                     "Um galão com pavio e combustível para um rastro de 4 m. Monte no chão e acenda: uma linha de fogo que os zumbis não atravessam.", 1, TipoDeLoot.Construcao),
            // as tábuas para barricada (11/11/2026, a etapa 5b: o modelo é o Item_Tabuas_corpo do Loot_Armadilhas.fbx, e as
            // pregadas, as BarricadaPorta e BarricadaJanela do Armadilhas.fbx, todas do Resources/ItensDasArmadilhas; no fim da
            // lista, para os índices dos outros não mudarem)
            Material(Item.TabuasBarricada, "Tabuas", "tábuas para barricada", "as tábuas para barricada", "feixes de tábuas para barricada",
                     "Tábuas cortadas na medida e um punhado de pregos. Mire uma porta ou uma janela de casa e segure E: pregadas, ela não abre.", 1, TipoDeLoot.Construcao),
        };
        static int[] _indiceDoLoot;
        static int[] _comModelo;

        static void PrepararLoot()
        {
            if (_indiceDoLoot != null) return;
            _indiceDoLoot = new int[256];
            for (int k = 0; k < 256; k++) _indiceDoLoot[k] = -1;
            var com = new System.Collections.Generic.List<int>();
            for (int k = 0; k < _loot.Length; k++)
            {
                _indiceDoLoot[(int)_loot[k].item] = k;
                if (_loot[k].modelo != null) com.Add(k);
            }
            _comModelo = com.ToArray();
        }

        /// <summary>Os dados do item do saque (null: não é um; as armas, as munições, as peças e as mochilas não são).</summary>
        public static DadosDoLoot Loot(Item i)
        {
            PrepararLoot();
            int k = _indiceDoLoot[(int)i];
            return k >= 0 ? _loot[k] : null;
        }
        public static bool EhLoot(Item i) => Loot(i) != null;
        /// <summary>Quantos itens do saque têm o modelo do loot.py (os da lata e da garrafa de água, não).</summary>
        public static int QuantosLoots
        {
            get { PrepararLoot(); return _comModelo.Length; }
        }
        /// <summary>O índice do item nos modelos do loot.py (as malhas, os ícones e as fotos), ou -1.</summary>
        public static int IndiceDoLoot(Item i)
        {
            PrepararLoot();
            int k = _indiceDoLoot[(int)i];
            return k >= 0 && _loot[k].modelo != null ? System.Array.IndexOf(_comModelo, k) : -1;
        }
        public static DadosDoLoot DoLoot(int k)
        {
            PrepararLoot();
            return _loot[_comModelo[k]];
        }
        public static string ModeloDoLoot(int k) => DoLoot(k).modelo;
        /// <summary>Todos os itens do saque (a lista do menu do desenvolvedor).</summary>
        public static System.Collections.Generic.IEnumerable<Item> TodosOsLoots
        {
            get { foreach (var l in _loot) yield return l.item; }
        }
        /// <summary>Um kit de montar no mundo (a fogueira, a cama, a barricada).</summary>
        public static bool EhConstrucao(Item i) => (i >= Item.Fogueira && i <= Item.Barricada) || i == Item.KitFogueira || EhArmadilha(i);
        /// <summary>Uma armadilha (11/11/2026, a etapa 5a): o alarme de latas, o tapete de pregos, as estacas e a cerca de fogo.</summary>
        public static bool EhArmadilha(Item i) => i >= Item.AlarmeLatas && i <= Item.CercaFogo;
        /// <summary>Uma fogueira (a de lenha e a portátil, 11/11/2026): acende, apaga na chuva e segura os zumbis.</summary>
        public static bool EhFogueira(Item i) => i == Item.Fogueira || i == Item.KitFogueira;
        /// <summary>O fogo que vai na mão (11/11/2026, a etapa 4): o lampião e o coquetel molotov (os braços deles, do Resources/ItensDoFogo).</summary>
        public static bool EhFogoDeMao(Item i) => i == Item.Lampiao || i == Item.Molotov;
        /// <summary>Um remédio (da espécie: o fundo na mochila e o preço; a gaze e o álcool também, mesmo sem uso sozinhos).</summary>
        public static bool EhRemedio(Item i) => Loot(i)?.tipo == TipoDeLoot.Remedio;

        /// <summary>Quantos cabem numa célula (a pilha): as balas numa caixa de munição, os do saque; o resto, 1.</summary>
        public static int Pilha(Item i) => EhMunicao(i) ? MaxPorCaixa : Loot(i)?.pilha ?? 1;
        /// <summary>O item vai em pilha (o qtd é quantos há nela).</summary>
        public static bool EmPilha(Item i) => Pilha(i) > 1;
        /// <summary>Quantos há na pilha (o qtd; uma pilha sem número é de 1).</summary>
        public static int Quantos(Item i, int qtd) => !EmPilha(i) ? 1 : EhMunicao(i) ? qtd : System.Math.Max(1, qtd);
        /// <summary>O nome com o número ("3 panos", "1 pano"; "1 peça de metal", não "1 peças de metal").</summary>
        public static string ComNumero(Item i, int n)
        {
            var l = Loot(i);
            return n == 1 || l == null ? $"{n} {Um(i)}" : $"{n} {l.plural}";
        }

        /// <summary>
        /// O nome de uma unidade, para contar (10/11/2026): as peças de metal, os fios e os pregos têm o nome no plural (o
        /// item é um punhado, e é assim que ele aparece na mochila, no chão e no comerciante), mas um só se conta no singular.
        /// O resto, o nome.
        /// </summary>
        static string Um(Item i) => i switch
        {
            Item.PecasDeMetal => "peça de metal",
            Item.Fios => "fio",
            Item.Pregos => "prego",
            Item.Estacas => "feixe de estacas",   // (as armadilhas, 11/11/2026: "1 feixe de estacas")
            _ => Nome(i),
        };

        /// <summary>
        /// Os equipamentos, na ordem do enum (de MochilaPequena em diante): Equipamentos.fbx (&lt;modelo&gt;_*),
        /// Icone_&lt;modelo&gt; e Foto_&lt;modelo&gt;, feitos pelo Blender/equipamentos.py.
        /// Sem mochila, o jogador só tem os bolsos (<see cref="Bolsos"/>).
        /// </summary>
        static readonly DadosDoEquipamento[] _equipamentos =
        {
            new DadosDoEquipamento { modelo = "MochilaPequena", nome = "mochila pequena", nomeComArtigo = "a mochila pequena", resumo = "De lona. Pouca coisa, mas já ajuda.", espaco = new(6, 4) },
            new DadosDoEquipamento { modelo = "MochilaMedia", nome = "mochila média", nomeComArtigo = "a mochila média", resumo = "Militar. Cabe bastante coisa.", espaco = new(8, 5) },
            new DadosDoEquipamento { modelo = "MochilaGrande", nome = "mochila grande", nomeComArtigo = "a mochila grande", resumo = "De trilha. A maior que há.", espaco = new(10, 7) },
        };
        public static int QuantosEquipamentos => _equipamentos.Length;

        /// <summary>O máximo de balas numa caixa de munição.</summary>
        public const int MaxPorCaixa = 60;

        /// <summary>
        /// As caixas de munição, na ordem de <see cref="Calibre"/> (Municao.fbx, &lt;modelo&gt;_*; Icone_&lt;modelo&gt; e
        /// Foto_&lt;modelo&gt;, feitos pelo Blender/municao.py).
        /// </summary>
        static readonly DadosDaMunicao[] _municoes =
        {
            new DadosDaMunicao { modelo = "Municao9mm", calibre = "9mm", resumo = "Para a pistola e a submetralhadora.", achadaMin = 12, achadaMax = 40 },
            new DadosDaMunicao { modelo = "Municao556", calibre = "5,56", resumo = "Para o rifle.", achadaMin = 10, achadaMax = 30 },
            new DadosDaMunicao { modelo = "Municao12", calibre = "calibre 12", resumo = "Cartuchos para a espingarda.", achadaMin = 6, achadaMax = 18 },
            new DadosDaMunicao { modelo = "Municao762", calibre = "7,62", resumo = "Para a sniper.", achadaMin = 4, achadaMax = 12 },
            // a das armas de sucata (09/11/2026; os modelos são os do Loot_Sucata.fbx: a lata e a caixinha)
            new DadosDaMunicao { modelo = "MunicaoPrego", calibre = "prego", nome = "munição de prego", nomeComArtigo = "a munição de prego",
                                 resumo = "Pregos com pólvora numa lata. Para a pistola e a submetralhadora de sucata.", achadaMin = 6, achadaMax = 20 },
            new DadosDaMunicao { modelo = "MunicaoParafuso", calibre = "parafuso", nome = "munição de parafuso", nomeComArtigo = "a munição de parafuso",
                                 resumo = "Parafusos com pólvora. Para o rifle de precisão de sucata.", achadaMin = 3, achadaMax = 10 },
        };
        public static int QuantasMunicoes => _municoes.Length;

        /// <summary>
        /// As peças do motor, na ordem do enum (de Vela em diante) e de <see cref="Vehicles.PecasDoMotor"/>: Pecas.fbx
        /// (&lt;modelo&gt;_*), Icone_&lt;modelo&gt; e Foto_&lt;modelo&gt;, feitos pelo Blender/pecas.py.
        /// </summary>
        static readonly DadosDaPeca[] _pecas =
        {
            new DadosDaPeca { modelo = "Vela", nome = "vela de ignição", nomeComArtigo = "a vela de ignição", resumo = "Faz o motor pegar.", funcao = "Motor", tamanho = new(1, 1) },
            new DadosDaPeca { modelo = "Bateria", nome = "bateria", nomeComArtigo = "a bateria", resumo = "Gira o motor de arranque e acende o painel.", funcao = "Bateria", tamanho = new(2, 2) },
            new DadosDaPeca { modelo = "Radiador", nome = "radiador", nomeComArtigo = "o radiador", resumo = "Esfria o motor.", funcao = "Radiador", tamanho = new(3, 2) },
            // a roda inteira (o aro com o pneu): vai numa das 4 rodas do carro, não no capô
            new DadosDaPeca { modelo = "Pneu", nome = "pneu", nomeComArtigo = "o pneu", resumo = "Uma roda com o pneu, para trocar a de um carro.", funcao = "Roda", tamanho = new(3, 3) },
            // a ferramenta (sem estado): fica no porta-malas, com o estepe
            new DadosDaPeca { modelo = "Macaco", nome = "macaco", nomeComArtigo = "o macaco", resumo = "Levanta o carro para trocar a roda.", funcao = "Ferramenta", tamanho = new(3, 1) },
            // a chave da perua (a pedido em 29/10/2026; sem estado): com ela, a partida é direta; sem ela, ligação direta
            new DadosDaPeca { modelo = "ChaveDaPerua", nome = "chave da perua", nomeComArtigo = "a chave da perua", resumo = "Liga a perua direto, sem a ligação direta.", funcao = "Chave", tamanho = new(1, 1) },
            // a chave do cupê (04/11/2026, a pedido): a mesma chave, com o chaveiro vermelho; cada carro pega só com a dele
            new DadosDaPeca { modelo = "ChaveDoCupe", nome = "chave do cupê", nomeComArtigo = "a chave do cupê", resumo = "Liga o cupê direto, sem a ligação direta.", funcao = "Chave", tamanho = new(1, 1) },
            // as chaves das motos (06/11/2026, a pedido): a mesma chave, com o chaveiro de couro preto (a Fat Boy) e o
            // laranja (a cross)
            new DadosDaPeca { modelo = "ChaveDaFatBoy", nome = "chave da Fat Boy", nomeComArtigo = "a chave da Fat Boy", resumo = "Liga a Fat Boy direto, sem a ligação direta.", funcao = "Chave", tamanho = new(1, 1) },
            new DadosDaPeca { modelo = "ChaveDaCross", nome = "chave da moto cross", nomeComArtigo = "a chave da moto cross", resumo = "Liga a moto cross direto, sem a ligação direta.", funcao = "Chave", tamanho = new(1, 1) },
        };
        public static int QuantasPecas => _pecas.Length;

        /// <summary>
        /// A sinuca, na ordem do enum (de CaixaDeBolas em diante): Sinuca.fbx (&lt;modelo&gt;_*), Icone_&lt;modelo&gt; e
        /// Foto_&lt;modelo&gt;, feitos pelo Blender/sinuca.py. A caixa cheia, na mesa de sinuca, põe as bolas nela e fica
        /// vazia; a vazia recolhe as bolas e fica cheia (as duas do mesmo tamanho: a troca não mexe na grade). O taco vai
        /// na mão e é o que joga.
        /// </summary>
        static readonly DadosDaSinuca[] _sinuca =
        {
            new DadosDaSinuca { modelo = "CaixaDeBolas", nome = "caixa de bolas de sinuca", nomeComArtigo = "a caixa de bolas de sinuca", resumo = "As 16 bolas. Na mesa de sinuca, E põe as bolas nela.", tamanho = new(3, 1) },
            new DadosDaSinuca { modelo = "CaixaDeBolasVazia", nome = "caixa de bolas vazia", nomeComArtigo = "a caixa de bolas vazia", resumo = "Sem as bolas. Na mesa, E recolhe as bolas.", tamanho = new(3, 1) },
            new DadosDaSinuca { modelo = "TacoMadeira", nome = "taco de madeira", nomeComArtigo = "o taco de madeira", resumo = "Taco de sinuca. Na mão, E na mesa joga.", tamanho = new(4, 1) },
            new DadosDaSinuca { modelo = "TacoEscuro", nome = "taco escuro", nomeComArtigo = "o taco escuro", resumo = "Taco de sinuca, com os filetes brancos.", tamanho = new(4, 1) },
            new DadosDaSinuca { modelo = "TacoPreto", nome = "taco preto", nomeComArtigo = "o taco preto", resumo = "Taco de sinuca, com os anéis prateados.", tamanho = new(4, 1) },
            new DadosDaSinuca { modelo = "TacoVermelho", nome = "taco vermelho", nomeComArtigo = "o taco vermelho", resumo = "Taco de sinuca.", tamanho = new(4, 1) },
        };
        public static int QuantosDaSinuca => _sinuca.Length;
        /// <summary>Uma coisa da sinuca: a caixa das bolas (cheia ou vazia) ou um taco.</summary>
        public static bool EhSinuca(Item i) => i >= Item.CaixaDeBolas && i <= Item.TacoVermelho;
        public static bool EhTaco(Item i) => i >= Item.TacoMadeira && i <= Item.TacoVermelho;
        public static bool EhCaixaDeBolas(Item i) => i == Item.CaixaDeBolas || i == Item.CaixaDeBolasVazia;
        /// <summary>O índice da coisa da sinuca (0 e 1 as caixas, de 2 a 5 os tacos), ou -1.</summary>
        public static int IndiceDaSinuca(Item i) => EhSinuca(i) ? i - Item.CaixaDeBolas : -1;
        /// <summary>O índice do taco (0 a 3), ou -1.</summary>
        public static int IndiceDoTaco(Item i) => EhTaco(i) ? i - Item.TacoMadeira : -1;
        public static Item DaSinuca(int k) => (Item)((int)Item.CaixaDeBolas + k);
        public static string ModeloDaSinuca(int k) => _sinuca[k].modelo;
        /// <summary>Uma peça do carro (as 3 do motor, o pneu, o macaco e as chaves da perua, do cupê e das motos), feita pelo pecas.py.</summary>
        public static bool EhPeca(Item i) => i >= Item.Vela && i <= Item.ChaveDaCross;
        /// <summary>Uma peça com o estado (0 a 100%) no qtd: todas menos o macaco e as chaves.</summary>
        public static bool ComEstado(Item i) => EhPeca(i) && i != Item.Macaco && !EhChave(i);
        /// <summary>Uma chave de veículo (a da perua, a do cupê e as das motos; cada veículo novo terá a sua, no Carro.chaveDoVeiculo).</summary>
        public static bool EhChave(Item i) => i == Item.ChaveDaPerua || i == Item.ChaveDoCupe || i == Item.ChaveDaFatBoy || i == Item.ChaveDaCross;
        /// <summary>Uma peça do capô (a vela, a bateria e o radiador; o pneu, não).</summary>
        public static bool EhPecaDoMotor(Item i) => i >= Item.Vela && i <= Item.Radiador;
        /// <summary>O índice da peça (0 a 2 as do motor, a ordem de <see cref="Vehicles.PecasDoMotor"/>; 3 o pneu), ou -1.</summary>
        public static int IndiceDaPeca(Item i) => EhPeca(i) ? i - Item.Vela : -1;
        public static Item DaPeca(int k) => (Item)((int)Item.Vela + k);
        public static DadosDaPeca Peca(int k) => _pecas[k];
        public static string ModeloDaPeca(int k) => _pecas[k].modelo;
        /// <summary>
        /// Vai para a mão e para os atalhos? As caixas de munição e as peças, não (só ficam na mochila); o que se veste
        /// também não.
        /// </summary>
        public static bool VaiNaMao(Item i) => i != Item.Nada && !EhRoupa(i) && !EhMunicao(i) && !EhPeca(i) && !EhEquipamento(i) && !EhCaixaDeBolas(i) && NoJogo(i) && (!EhLoot(i) || Loot(i).mao >= 0 || EhFumo(i) || EhFogoDeMao(i));
        public static bool EhMunicao(Item i) => i >= Item.Municao9mm && i <= Item.MunicaoParafuso;
        /// <summary>O índice da caixa (0 a 3, a ordem de <see cref="Calibre"/>), ou -1.</summary>
        public static int IndiceDaMunicao(Item i) => EhMunicao(i) ? i - Item.Municao9mm : -1;
        public static Calibre CalibreDa(Item caixa) => (Calibre)IndiceDaMunicao(caixa);
        public static Item CaixaDe(Calibre c) => (Item)((int)Item.Municao9mm + (int)c);
        public static DadosDaMunicao Municao(Calibre c) => _municoes[(int)c];
        public static string ModeloDaMunicao(int k) => _municoes[k].modelo;
        /// <summary>O nome do calibre ("9mm", "5,56", "calibre 12", "7,62").</summary>
        public static string NomeDoCalibre(Calibre c) => _municoes[(int)c].calibre;
        /// <summary>O nome da munição ("9mm", "munição de prego"), para o aviso de sem munição.</summary>
        public static string NomeDaMunicao(Calibre c) => _municoes[(int)c].nome ?? _municoes[(int)c].calibre;
        /// <summary>O calibre de uma arma.</summary>
        public static Calibre CalibreDaArma(Item arma) => Armas.Dados(Arma(arma)).calibre;
        /// <summary>
        /// As balas que o item leva quando aparece sem dizer quantas: a arma, o pente cheio; a caixa, cheia. Os outros, 0.
        /// </summary>
        public static int QuantidadePadrao(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).capacidade : EhMunicao(i) ? MaxPorCaixa : EhPeca(i) ? 100 : EmPilha(i) ? 1 : 0;
        /// <summary>As armas que usam o calibre ("a pistola e a submetralhadora").</summary>
        public static string ArmasDoCalibre(Calibre c)
        {
            var nomes = new System.Collections.Generic.List<string>();
            for (int k = 0; k < Armas.Quantas; k++)
                if (Armas.Todas[k].calibre == c) nomes.Add(Armas.Todas[k].nome);
            return nomes.Count <= 1 ? (nomes.Count == 1 ? nomes[0] : "") : string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
        }
        /// <summary>Os bolsos: o espaço de quem está sem mochila.</summary>
        public static readonly UnityEngine.Vector2Int Bolsos = new(4, 2);
        /// <summary>O maior espaço (a mochila grande), para a tela reservar o lugar.</summary>
        public static readonly UnityEngine.Vector2Int MaiorEspaco = new(10, 7);

        public static bool EhArma(Item i) => (i >= Item.Pistola && i <= Item.RifleSucata) || i == Item.SubmetralhadoraSucata;
        /// <summary>Uma arma de sucata (09/11/2026).</summary>
        public static bool DeSucata(Item i) => EhArma(i) && Armas.DeSucata(Arma(i));
        /// <summary>
        /// O que gasta com o uso (31/10/2026, com o kit de reparo): as armas de fogo, a cada tiro, e as brancas, a cada golpe
        /// que pega. O desgaste vai de 0 (nova) a 100: a de fogo quebrada não atira (o kit conserta); a branca quebra e some.
        /// </summary>
        public static bool ComDesgaste(Item i) => EhArma(i) || EhBranca(i) || Player.Protecao.EhPeca(i);   // (e a proteção de sucata, 11/11/2026)
        /// <summary>A lanterna gasta as pilhas: o desgaste dela é a carga que já foi (0, pilhas novas; 100, acabaram).</summary>
        public static bool EhLanterna(Item i) => i == Item.Lanterna;
        /// <summary>O galão de gasolina: a gasolina vai no desgaste (0, cheio; 100, vazio), como a carga da lanterna.</summary>
        public static bool EhGalao(Item i) => i == Item.Galao;
        /// <summary>Quantos litros cabem no galão.</summary>
        public const float LitrosDoGalao = 10f;
        /// <summary>Quantos usos tem um spray antisséptico cheio.</summary>
        public const int UsosDoSpray = 5;
        /// <summary>
        /// O que tem usos (05/11/2026, a etapa 2: o spray antisséptico, 5 usos): os usos vão no desgaste, como a carga da
        /// lanterna e a gasolina do galão (0, cheio; cada uso soma <see cref="DesgastePorUsoDoSpray"/>; 100, acabou). O
        /// desgaste já vai pela rede, para o chão, para as estantes e para o save; o item novo sai cheio de todo lugar.
        /// </summary>
        public static bool ComUsos(Item i) => i == Item.SprayAntisseptico;
        /// <summary>Quanto desgaste cada uso do spray soma (20: 5 usos).</summary>
        public static int DesgastePorUsoDoSpray => 100 / UsosDoSpray;
        /// <summary>Quantos usos sobram com este desgaste (0 → 5, 40 → 3, 80 → 1, 100 → 0).</summary>
        public static int UsosQueSobram(int desgaste) =>
            UnityEngine.Mathf.Clamp((100 - desgaste + DesgastePorUsoDoSpray - 1) / DesgastePorUsoDoSpray, 0, UsosDoSpray);
        /// <summary>Quanto um uso gasta: um tiro (as de fogo: 1% a cada <see cref="TirosPorPorcento"/>) ou um golpe que pega (as brancas).</summary>
        public static float DesgastePorUso(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).desgaste / TirosPorPorcento(i) : i == Item.Faca ? 0.8f : i == Item.Machado ? 0.6f
            : i == Item.Bastao ? 1.5f : i == Item.Lanca ? 1.2f : i == Item.Tocha ? 0f : 1f;
        /// <summary>
        /// Quantos tiros gastam 1% da arma de fogo (05/11/2026, a pedido: "precisamos aumentar, principalmente das armas
        /// automáticas"; era 1% a cada 4 tiros em todas, 400 até quebrar). Nas de pente, um pente cheio gasta 1%: a pistola
        /// 12 (1.200 tiros até quebrar), a submetralhadora 25 (2.500) e o rifle 30 (3.000). A espingarda (8, 800 tiros) e a
        /// sniper (10, 1.000) atiram pouco e forçam mais a arma a cada tiro.
        /// </summary>
        public static int TirosPorPorcento(Item i) => i switch
        {
            Item.Pistola => 12, Item.Submetralhadora => 25, Item.Rifle => 30, Item.Espingarda => 8, Item.Sniper => 10,
            // as de sucata: os da real (o desgaste a mais vem do DadosDaArma.desgaste)
            Item.PistolaSucata => 12, Item.SubmetralhadoraSucata => 25, Item.RifleSucata => 10, _ => 4,
        };
        /// <summary>A tocha acesa na mão queima: o desgaste sobe 1 a cada tantos segundos (100: ela se apaga e some).</summary>
        public const float SegundosDaTochaPorDesgaste = 6f;
        /// <summary>
        /// A tocha que a chuva apagou (05/11/2026, a pedido): o qtd dela (a acesa tem 0, o que a tocha não usava). Vai junto
        /// pela rede, para o chão e para o save. Apagada, ela não queima, não ilumina e não afasta os zumbis; reacende numa
        /// fogueira acesa (Loot/ConstrucoesNoMundo.cs). Não é o 1: quem cria um item com "pelo menos 1" no qtd (a mercadoria
        /// do comerciante morto, a mochila do sobrevivente) daria uma tocha apagada.
        /// </summary>
        public const ushort TochaApagada = 1000;
        /// <summary>Daqui para cima, a arma de fogo emperra às vezes (até 30% dos tiros, quase quebrada).</summary>
        public const int DesgasteQueEmperra = 70;
        /// <summary>Uma arma branca (a faca e o machado): vai na mão, o clique golpeia de perto, sem munição.</summary>
        public static bool EhBranca(Item i) => i >= Item.Faca && i <= Item.Tocha;
        /// <summary>O modelo (o índice em <see cref="Modelos"/>) é de uma arma branca.</summary>
        public static bool ModeloBranco(int modelo) => modelo >= Modelo(Item.Faca) && modelo <= Modelo(Item.Tocha);
        /// <summary>Come, bebe ou usa (a comida, a bebida e os remédios).</summary>
        public static bool EhConsumivel(Item i) => Loot(i)?.verbo != null;

        /// <summary>Um peixe do lago, cru (07/11/2026: comido assim, pode fazer mal; assado na fogueira, não).</summary>
        public static bool EhPeixeCru(Item i) => i >= Item.PeixePequeno && i <= Item.PeixeDourado;
        /// <summary>O que se veste: as 3 mochilas (o coldre saiu do jogo).</summary>
        public static bool EhEquipamento(Item i) => EhMochila(i);
        public static bool EhMochila(Item i) => i >= Item.MochilaPequena && i <= Item.MochilaGrande;
        /// <summary>O que existe no jogo (o coldre, não: saiu em 31/10/2026).</summary>
        public static bool NoJogo(Item i) => i != Item.Nada && i != Item.Coldre;
        /// <summary>
        /// O que, num atalho e fora da mão, vai preso no cós da calça, atrás (a pistola e a faca); as outras armas vão nas
        /// costas (o EquipamentoNoCorpo).
        /// </summary>
        public static bool VaiNoCos(Item i) => i == Item.Pistola || i == Item.PistolaSucata || i == Item.Faca;
        /// <summary>O índice do equipamento (0 a 2, na ordem de _equipamentos), ou -1.</summary>
        public static int Equipamento(Item i) => EhEquipamento(i) ? i - Item.MochilaPequena : -1;
        public static Item DoEquipamento(int k) => (Item)((int)Item.MochilaPequena + k);
        public static DadosDoEquipamento DadosDe(Item i) => EhEquipamento(i) ? _equipamentos[Equipamento(i)] : null;
        public static string ModeloDoEquipamento(int k) => _equipamentos[k].modelo;
        /// <summary>O espaço da grade com esta mochila (Nada: os bolsos).</summary>
        public static UnityEngine.Vector2Int EspacoDa(Item mochila) => EhMochila(mochila) ? _equipamentos[Equipamento(mochila)].espaco : Bolsos;
        // (a submetralhadora de sucata é o item 13: o 8 já era a faca)
        public static TipoDeArma Arma(Item i) => i == Item.SubmetralhadoraSucata ? TipoDeArma.SubmetralhadoraSucata : (TipoDeArma)((int)i - 1);
        public static Item DaArma(TipoDeArma t) => t == TipoDeArma.SubmetralhadoraSucata ? Item.SubmetralhadoraSucata : (Item)((int)t + 1);
        public static DadosDoLoot Consumivel(Item i) => Loot(i);

        /// <summary>
        /// O modelo de mão do item (os braços de primeira pessoa e o item na mão do corpo): o dele, ou, nos comestíveis e
        /// nos remédios do saque, o da lata ou o da garrafa (com a malha do item no lugar). -1: não vai na mão.
        /// </summary>
        public static int MaoDe(Item i)
        {
            int m = Modelo(i);
            if (m >= 0) return m;
            // o lampião e o molotov (11/11/2026): no corpo, o lugar da tocha (as poses dela; a malha é trocada no JogadorRede.Fogo)
            if (EhFogoDeMao(i)) return Modelo(Item.Tocha);
            var l = Loot(i);
            return l != null ? l.mao : -1;
        }
        /// <summary>O item vai na mão com a malha dele no lugar da lata ou da garrafa (os do loot.py).</summary>
        public static bool NaMaoComMalhaPropria(Item i) => Modelo(i) < 0 && !EhFogoDeMao(i) && MaoDe(i) >= 0 && IndiceDoLoot(i) >= 0;

        /// <summary>O índice do modelo do item (na mão, no chão e no ícone), ou -1 (nada).</summary>
        public static int Modelo(Item i) => EhArma(i) ? (int)Arma(i) : i == Item.Comida ? Armas.Quantas : i == Item.Agua ? Armas.Quantas + 1
            : EhBranca(i) ? Armas.Quantas + 2 + (i - Item.Faca) : -1;

        static UnityEngine.Material[] _materiaisProprios;

        /// <summary>
        /// O material próprio de um modelo (com a textura dele: hoje só a pistola nova, de 29/10/2026), ou null: o modelo
        /// usa a paleta (o material dos itens no chão, do painel e do equipamento no corpo). Fica em
        /// Resources/MateriaisProprios/&lt;modelo&gt; (o Art/Characters/Jogador/Resources, feito pela montagem da pistola).
        /// </summary>
        public static UnityEngine.Material MaterialProprio(int modelo)
        {
            if (modelo < 0 || modelo >= Modelos.Length) return null;
            if (_materiaisProprios == null)
            {
                _materiaisProprios = new UnityEngine.Material[Modelos.Length];
                for (int k = 0; k < Modelos.Length; k++) _materiaisProprios[k] = UnityEngine.Resources.Load<UnityEngine.Material>("MateriaisProprios/" + Modelos[k]);
            }
            return _materiaisProprios[modelo];
        }

        public static UnityEngine.Material MaterialProprio(Item i) => MaterialProprio(Modelo(i));

        // as células que cada modelo ocupa na mochila (largura × altura), na ordem de Modelos: pelo tamanho da malha, uns
        // 17 cm por célula (o ícone tem esta proporção)
        static readonly UnityEngine.Vector2Int[] _tamanhos =
        {
            new(2, 1), new(5, 2), new(3, 2), new(6, 1), new(7, 2),   // pistola, rifle, submetralhadora, espingarda, sniper
            new(2, 1), new(7, 2), new(3, 2),                         // as de sucata: a pistola, o rifle e a submetralhadora
            new(1, 1), new(1, 2),                                   // lata, garrafa
            new(2, 1), new(4, 1), new(4, 1), new(6, 1), new(3, 1),  // faca, machado, bastão, lança, tocha
        };

        /// <summary>As células que o item ocupa na mochila (largura × altura).</summary>
        public static UnityEngine.Vector2Int Tamanho(Item i)
        {
            if (EhMunicao(i) || EhRoupa(i)) return new UnityEngine.Vector2Int(1, 1);
            if (EhPeca(i)) return _pecas[IndiceDaPeca(i)].tamanho;
            if (i == Item.Galao) return new UnityEngine.Vector2Int(2, 2);   // o galão: maior que o resto do saque
            if (i == Item.PeDeCabra) return new UnityEngine.Vector2Int(2, 1);   // o pé de cabra: comprido
            if (i == Item.PeixeGrande || i == Item.PeixeDourado) return new UnityEngine.Vector2Int(2, 1);   // os peixes grandes
            if (Player.Protecao.EhPeca(i)) return Player.Protecao.Tamanho(i);   // a proteção de sucata (11/11/2026)
            if (i == Item.Lampiao || i == Item.KitFogueira) return new UnityEngine.Vector2Int(2, 2);   // o fogo (11/11/2026)
            if (i == Item.Molotov) return new UnityEngine.Vector2Int(1, 2);
            // as armadilhas (11/11/2026, a etapa 5a)
            if (i == Item.AlarmeLatas || i == Item.CercaFogo) return new UnityEngine.Vector2Int(2, 2);
            if (i == Item.TapetePregos) return new UnityEngine.Vector2Int(3, 1);
            if (i == Item.Estacas) return new UnityEngine.Vector2Int(4, 1);
            if (i == Item.TabuasBarricada) return new UnityEngine.Vector2Int(3, 1);   // as tábuas (11/11/2026, a etapa 5b)
            if (IndiceDoLoot(i) >= 0) return new UnityEngine.Vector2Int(1, 1);   // o saque: uma célula (por enquanto)
            if (EhSinuca(i)) return _sinuca[IndiceDaSinuca(i)].tamanho;
            int m = Modelo(i);
            return m >= 0 ? _tamanhos[m] : UnityEngine.Vector2Int.zero;
        }

        // uma linha sobre cada modelo, para os detalhes da tela do inventário (curta: os números vêm em seguida)
        static readonly string[] _resumos =
        {
            "Semiautomática. Leve e precisa.", "Automático. Bom de longe.", "Automática. Rápida, de perto.",
            "De bomba. Arrasa de perto.", "De ferrolho, com luneta.",
            "Feita de sucata. Um tiro, e recarrega.", "De ferrolho, feito de sucata. Sem luneta, três tiros.",
            "Automática, feita de sucata. Emperra mais que a de fábrica.",
            "Conserva. Mata a fome.", "Água. Mata a sede.",
            "Baioneta. De perto e sem barulho.", "De lenha, nas duas mãos. Lento, mas arranca um braço.",
            "Madeira e pregos. Nas duas mãos, pancada forte.", "Lâmina na ponta de um cabo. Alcança mais longe.",
            "Ilumina em volta. Queima uns 10 minutos.",
        };

        public static string Resumo(Item i)
        {
            if (EhEquipamento(i)) return DadosDe(i).resumo;
            if (EhMunicao(i)) return _municoes[IndiceDaMunicao(i)].resumo;
            if (EhPeca(i)) return _pecas[IndiceDaPeca(i)].resumo;
            if (EhLoot(i)) return Loot(i).resumo;
            if (EhSinuca(i)) return _sinuca[IndiceDaSinuca(i)].resumo;
            if (EhRoupa(i)) return _resumosDasRoupas[(int)Roupa(i).lugar];
            int m = Modelo(i);
            return m >= 0 ? _resumos[m] : "";
        }

        public static string Nome(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nome : EhBranca(i) ? Branca(i).nome : EhLoot(i) ? Loot(i).nome : EhEquipamento(i) ? DadosDe(i).nome
            : EhMunicao(i) ? _municoes[IndiceDaMunicao(i)].nome ?? "caixa de " + NomeDoCalibre(CalibreDa(i)) : EhPeca(i) ? _pecas[IndiceDaPeca(i)].nome : EhSinuca(i) ? _sinuca[IndiceDaSinuca(i)].nome : EhRoupa(i) ? Roupa(i).nome : "";
        public static string NomeComArtigo(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nomeComArtigo : EhBranca(i) ? Branca(i).nomeComArtigo : EhLoot(i) ? Loot(i).nomeComArtigo : EhEquipamento(i) ? DadosDe(i).nomeComArtigo
            : EhMunicao(i) ? _municoes[IndiceDaMunicao(i)].nomeComArtigo ?? "a caixa de " + NomeDoCalibre(CalibreDa(i)) : EhPeca(i) ? _pecas[IndiceDaPeca(i)].nomeComArtigo : EhSinuca(i) ? _sinuca[IndiceDaSinuca(i)].nomeComArtigo : EhRoupa(i) ? Roupa(i).nomeComArtigo : "";
    }
}
