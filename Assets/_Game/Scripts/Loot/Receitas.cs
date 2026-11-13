using System.Collections.Generic;

namespace ZombieCityCleaner.Loot
{
    /// <summary>Um item e quantos (um ingrediente, ou o que sai de uma receita).</summary>
    public readonly struct Parte
    {
        public readonly Item item;
        public readonly int qtd;
        public Parte(Item item, int qtd)
        {
            this.item = item;
            this.qtd = qtd;
        }
    }

    /// <summary>
    /// Uma receita da fabricação (a aba Criar da tela do inventário): o que sai e quantos, o que gasta, a ferramenta que
    /// precisa ter na mochila (não gasta; Nada, nenhuma) e se precisa estar perto de uma bancada.
    /// </summary>
    public class Receita
    {
        public Item resultado;
        public int qtd = 1;
        public Parte[] gasta;
        public Item ferramenta;
        public bool bancada;
        /// <summary>Só na mesa de oficina (06/11/2026: a do bunker; ela também vale como bancada).</summary>
        public bool oficina;
        /// <summary>Só perto de uma fogueira acesa (07/11/2026: assar o peixe).</summary>
        public bool fogo;
        /// <summary>A habilidade que destrava a receita (o número do <see cref="Player.Habilidade"/>; -1: nenhuma).</summary>
        public int habilidade = -1;
        /// <summary>Quantos pontos da habilidade a receita pede (10/11/2026: o filtro de óleo, a mira de ponto e os canos II e III pedem o 2º e o 3º).</summary>
        public int nivel = 1;
        /// <summary>
        /// Quantos combustíveis gasta também (11/11/2026, a etapa 4 do ramo Fogo; a cerca de fogo da etapa 5a gasta 2): cada
        /// um é 1 álcool ou, sem ele, meio litro de gasolina do galão mais cheio (o Player.FogoDoJogador.LitrosPorCarga; o
        /// InventarioJogador.Fogo.cs confere e gasta). 0: nenhum.
        /// </summary>
        public int combustivel;

        /// <summary>A mesma receita, gastando <paramref name="n"/> combustíveis (11/11/2026: a cerca de fogo).</summary>
        public Receita ComCombustivel(int n)
        {
            combustivel = n;
            return this;
        }
    }

    /// <summary>
    /// O que se desmonta (31/10/2026, a pedido: "eu quero pegar uma roupa e desmanchar ela e virar pano, aí utilizo esse
    /// pano para craftar"): um item da mochila vira os pedaços dele (um de cada vez, numa pilha), com a ferramenta que
    /// precisar (não gasta).
    /// </summary>
    public class Desmontagem
    {
        public Item item;
        public string verbo = "desmontar";
        public Parte[] vira;
        public Item ferramenta;
    }

    /// <summary>
    /// As receitas da fabricação e o que se desmonta (31/10/2026, a pedido: "fazer tocha e alguns itens básicos de
    /// sobrevivência, como fogueira, cama, bandagem, bastão improvisado (prego + madeira), barricada, mochila pequena,
    /// kit de reparo de armas... e mais algumas coisas para sobrevivência"; a bancada não se fabrica: ela existe em
    /// alguns lugares, como o bunker). O anfitrião confere tudo (<see cref="Player.InventarioJogador"/>).
    /// </summary>
    public static class Receitas
    {
        static Parte P(Item i, int n = 1) => new Parte(i, n);

        static Receita R(Item resultado, int qtd, bool bancada, Item ferramenta, params Parte[] gasta) =>
            new Receita { resultado = resultado, qtd = qtd, bancada = bancada, ferramenta = ferramenta, gasta = gasta };

        // assar na fogueira acesa (07/11/2026)
        static Receita Fogo(Item resultado, int qtd, params Parte[] gasta) =>
            new Receita { resultado = resultado, qtd = qtd, fogo = true, ferramenta = Item.Nada, gasta = gasta };

        // a receita da árvore de habilidades (06/11/2026): só com a habilidade, na bancada ou na mesa de oficina
        static Receita H(Player.Habilidade h, Item resultado, int qtd, bool oficina, Item ferramenta, params Parte[] gasta) =>
            new Receita { resultado = resultado, qtd = qtd, bancada = !oficina, oficina = oficina, ferramenta = ferramenta, gasta = gasta, habilidade = (int)h };

        // a melhoria das armas de sucata (10/11/2026, a etapa 2): pede 'nivel' pontos da habilidade
        static Receita M(Player.Habilidade h, int nivel, Item resultado, bool oficina, params Parte[] gasta)
        {
            var r = H(h, resultado, 1, oficina, Item.Nada, gasta);
            r.nivel = nivel;
            return r;
        }

        /// <summary>Uma receita do Armeiro de sucata (as armas, as munições e as melhorias): o Mestre armeiro gasta menos sucata nelas.</summary>
        public static bool DoArmeiroDeSucata(Receita r) =>
            r != null && r.habilidade >= 0 && Player.Habilidades.RamoDe((Player.Habilidade)r.habilidade) == Player.RamoDaHabilidade.ArmeiroDeSucata;

        /// <summary>O que a receita gasta, com o Mestre armeiro ('mestre': 25% menos sucata nas do Armeiro de sucata, para cima).</summary>
        public static Parte[] Gasta(Receita r, bool mestre)
        {
            if (!mestre || !DoArmeiroDeSucata(r)) return r.gasta;
            var g = new Parte[r.gasta.Length];
            for (int i = 0; i < g.Length; i++)
                g[i] = r.gasta[i].item == Item.Sucata ? new Parte(Item.Sucata, Combat.Melhorias.SucataComOMestre(r.gasta[i].qtd)) : r.gasta[i];
            return g;
        }

        /// <summary>As receitas, na ordem da aba Criar (o anfitrião recebe o índice).</summary>
        public static readonly Receita[] Todas =
        {
            // em qualquer lugar
            // a bandagem de pano: 2 panos dão 1 (05/11/2026, sem a vida voltando sozinha: eram 2, e as roupas viravam cura sem fim)
            R(Item.Bandagem, 1, false, Item.Nada, P(Item.Pano, 2)),
            // o peixe assado (07/11/2026, a pedido: "assar na fogueira"; o cru pode fazer mal): cada peixe dá filés pelo tamanho
            Fogo(Item.PeixeAssado, 1, P(Item.PeixePequeno)),
            Fogo(Item.PeixeAssado, 2, P(Item.PeixeMedio)),
            Fogo(Item.PeixeAssado, 3, P(Item.PeixeGrande)),
            Fogo(Item.PeixeAssado, 3, P(Item.PeixeDourado)),
            Fogo(Item.PeixeAssado, 2, P(Item.Bagre)),
            R(Item.KitMedico, 1, false, Item.Nada, P(Item.Atadura), P(Item.Gaze, 2), P(Item.Desinfetante), P(Item.Comprimidos)),
            // a bandagem antisséptica (05/11/2026, a etapa 2): 1 álcool (o Desinfetante) e 3 bandagens de pano, ou 3
            // ataduras, dão 3 (o índice da receita vai pela rede: a VersaoDaRede 2 já cobre esta mudança)
            R(Item.BandagemAntisseptica, 3, false, Item.Nada, P(Item.Desinfetante), P(Item.Bandagem, 3)),
            R(Item.BandagemAntisseptica, 3, false, Item.Nada, P(Item.Desinfetante), P(Item.Atadura, 3)),
            // a tala improvisada (05/11/2026): duas ripas amarradas com o pano
            R(Item.Tala, 1, false, Item.Nada, P(Item.Madeira, 2), P(Item.Pano)),
            R(Item.Corda, 1, false, Item.Nada, P(Item.Pano, 3)),
            R(Item.Fogueira, 1, false, Item.Nada, P(Item.Madeira, 4), P(Item.Pano)),
            R(Item.Cama, 1, false, Item.Nada, P(Item.Pano, 4), P(Item.Madeira, 3), P(Item.Corda)),
            R(Item.VaraDePesca, 1, false, Item.Nada, P(Item.Madeira, 2), P(Item.Fios, 2)),
            R(Item.Isca, 5, false, Item.Nada, P(Item.Milho)),
            R(Item.Tocha, 1, false, Item.Nada, P(Item.Madeira), P(Item.Pano)),
            R(Item.Bastao, 1, false, Item.Nada, P(Item.Madeira, 2), P(Item.Pregos, 6)),
            // na bancada
            R(Item.Barricada, 1, true, Item.Ferramenta, P(Item.Madeira, 4), P(Item.Pregos, 8)),
            R(Item.MochilaPequena, 1, true, Item.Nada, P(Item.Pano, 6), P(Item.Corda, 2), P(Item.FitaAdesiva)),
            R(Item.KitDeReparo, 1, true, Item.Ferramenta, P(Item.PecasDeMetal, 3), P(Item.FitaAdesiva), P(Item.Fios)),
            R(Item.Lanca, 1, true, Item.Nada, P(Item.Madeira, 2), P(Item.PecasDeMetal), P(Item.FitaAdesiva)),
            // a árvore de habilidades (06/11/2026): no fim (o índice vai pela rede). O Faz-tudo, na bancada
            H(Player.Habilidade.FazTudo, Item.Gazua, 2, false, Item.Ferramenta, P(Item.PecasDeMetal), P(Item.Fios)),
            H(Player.Habilidade.FazTudo, Item.Faca, 1, false, Item.Nada, P(Item.PecasDeMetal, 2), P(Item.FitaAdesiva), P(Item.Madeira)),
            H(Player.Habilidade.FazTudo, Item.MochilaMedia, 1, false, Item.Nada, P(Item.Pano, 8), P(Item.Corda, 2), P(Item.FitaAdesiva, 2)),
            // o Artesão, na mesa de oficina
            H(Player.Habilidade.Artesao, Item.Machado, 1, true, Item.Ferramenta, P(Item.PecasDeMetal, 3), P(Item.Madeira, 2), P(Item.FitaAdesiva)),
            H(Player.Habilidade.Artesao, Item.PeDeCabra, 1, true, Item.Ferramenta, P(Item.PecasDeMetal, 4)),
            H(Player.Habilidade.Artesao, Item.MochilaGrande, 1, true, Item.Nada, P(Item.Pano, 12), P(Item.Corda, 3), P(Item.FitaAdesiva, 3)),
            H(Player.Habilidade.Artesao, Item.Lanterna, 1, true, Item.Ferramenta, P(Item.PecasDeMetal, 2), P(Item.Fios, 2), P(Item.FitaAdesiva)),
            // o Armeiro, na mesa de oficina: a munição, com a pólvora
            H(Player.Habilidade.Armeiro, Item.Municao9mm, 15, true, Item.Ferramenta, P(Item.Polvora), P(Item.PecasDeMetal)),
            H(Player.Habilidade.Armeiro, Item.Municao12, 8, true, Item.Ferramenta, P(Item.Polvora), P(Item.PecasDeMetal), P(Item.Pano)),
            H(Player.Habilidade.Armeiro, Item.Municao556, 12, true, Item.Ferramenta, P(Item.Polvora, 2), P(Item.PecasDeMetal)),
            H(Player.Habilidade.Armeiro, Item.Municao762, 10, true, Item.Ferramenta, P(Item.Polvora, 2), P(Item.PecasDeMetal, 2)),
            // o Armeiro de sucata (09/11/2026): a pistola e as munições na bancada; a submetralhadora e o rifle na mesa de oficina
            H(Player.Habilidade.PistolaDeSucata, Item.PistolaSucata, 1, false, Item.Nada, P(Item.Sucata, 6), P(Item.Madeira), P(Item.FitaAdesiva)),
            H(Player.Habilidade.PistolaDeSucata, Item.MunicaoPrego, 10, false, Item.Nada, P(Item.Polvora), P(Item.Sucata)),
            H(Player.Habilidade.RifleDeSucata, Item.MunicaoParafuso, 5, false, Item.Nada, P(Item.Polvora), P(Item.Sucata, 2)),
            H(Player.Habilidade.SubmetralhadoraDeSucata, Item.SubmetralhadoraSucata, 1, true, Item.Nada, P(Item.Sucata, 12), P(Item.Madeira), P(Item.FitaAdesiva, 2)),
            H(Player.Habilidade.RifleDeSucata, Item.RifleSucata, 1, true, Item.Nada, P(Item.Sucata, 10), P(Item.Madeira, 2), P(Item.FitaAdesiva)),
            // as melhorias das armas de sucata (10/11/2026, a etapa 2; os números no Combat/Melhorias.cs): na bancada o que é
            // simples, na mesa de oficina o resto. Não existe garrafa vazia como item: a PET é a garrafa de refrigerante (dois
            // litros); nem filtro de óleo nas peças do carro: ele sai da sucata
            M(Player.Habilidade.SilenciadorCaseiro, 1, Item.SilenciadorPET, false, P(Item.RefrigeranteGarrafa), P(Item.FitaAdesiva)),
            M(Player.Habilidade.SilenciadorCaseiro, 2, Item.SilenciadorFiltro, true, P(Item.Sucata, 4), P(Item.FitaAdesiva)),
            M(Player.Habilidade.MirasCaseiras, 1, Item.MiraDeFerro, false, P(Item.Sucata, 2)),
            M(Player.Habilidade.MirasCaseiras, 2, Item.MiraDePonto, true, P(Item.Sucata, 3), P(Item.FitaAdesiva), P(Item.Pilhas)),
            M(Player.Habilidade.LunetaDeCano, 1, Item.Luneta, true, P(Item.Sucata, 5), P(Item.FitaAdesiva, 2)),
            M(Player.Habilidade.CarregadorEstendido, 1, Item.CarregadorLongo, false, P(Item.Sucata, 4), P(Item.FitaAdesiva)),
            M(Player.Habilidade.CanoReforcado, 1, Item.CanoReforcado1, false, P(Item.Sucata, 4), P(Item.FitaAdesiva)),
            M(Player.Habilidade.CanoReforcado, 2, Item.CanoReforcado2, true, P(Item.Sucata, 6), P(Item.FitaAdesiva, 2)),
            M(Player.Habilidade.CanoReforcado, 3, Item.CanoReforcado3, true, P(Item.Sucata, 8), P(Item.PecasDeMetal, 2)),
            // a proteção de sucata (11/11/2026, a etapa 3; os números no Player/Protecao.cs): o capacete, as braçadeiras e as
            // caneleiras na bancada; o colete na mesa de oficina, com a borracha de um pneu (o mais gasto da mochila: o
            // rasgado, que não tem remendo, vai primeiro)
            H(Player.Habilidade.CapaceteDePanela, Item.Capacete, 1, false, Item.Nada, P(Item.Sucata, 3), P(Item.FitaAdesiva), P(Item.Corda)),
            H(Player.Habilidade.BracadeirasECaneleiras, Item.Bracadeiras, 1, false, Item.Nada, P(Item.Sucata, 4), P(Item.FitaAdesiva, 2)),
            H(Player.Habilidade.BracadeirasECaneleiras, Item.Caneleiras, 1, false, Item.Nada, P(Item.Sucata, 4), P(Item.FitaAdesiva, 2)),
            H(Player.Habilidade.ColeteDeChapa, Item.Colete, 1, true, Item.Nada, P(Item.Sucata, 8), P(Item.FitaAdesiva, 2), P(Item.Pneu)),
            // o fogo (11/11/2026, a etapa 4; os números no Player/FogoDoJogador.cs): o lampião na mesa de oficina, o molotov e o
            // kit da fogueira portátil na bancada; os três com 1 combustível (1 álcool ou meio litro de gasolina de um galão).
            // Não existe vidro como item: a garrafa é a de refrigerante (como no silenciador de garrafa PET)
            F(Player.Habilidade.Lampiao, Item.Lampiao, true, P(Item.Sucata, 3), P(Item.RefrigeranteGarrafa)),
            F(Player.Habilidade.CoquetelMolotov, Item.Molotov, false, P(Item.RefrigeranteGarrafa), P(Item.Pano)),
            F(Player.Habilidade.FogueiraPortatil, Item.KitFogueira, false, P(Item.Madeira, 3), P(Item.Corda)),
            // as armadilhas (11/11/2026, a etapa 5a; os números no Player/ArmadilhasDoJogador.cs): todas na bancada. Não existe
            // lata vazia como item: as latas do alarme saem da sucata. A cerca de fogo gasta 2 combustíveis (o galão e o rastro)
            H(Player.Habilidade.AlarmeDeLatas, Item.AlarmeLatas, 1, false, Item.Nada, P(Item.Sucata, 2), P(Item.Corda), P(Item.Madeira)),
            H(Player.Habilidade.TapeteDePregos, Item.TapetePregos, 1, false, Item.Ferramenta, P(Item.Madeira), P(Item.Pregos, 12)),
            H(Player.Habilidade.Estacas, Item.Estacas, 1, false, Item.Ferramenta, P(Item.Madeira, 4), P(Item.Corda)),
            F(Player.Habilidade.CercaDeFogo, Item.CercaFogo, false, P(Item.Pano)).ComCombustivel(2),
            // as tábuas para barricada (11/11/2026, a etapa 5b; no fim, para os índices das outras não mudarem): pregadas numa
            // porta ou numa janela das casas (os números no Player/ArmadilhasDoJogador.cs)
            H(Player.Habilidade.BarricadaDeTabuas, Item.TabuasBarricada, 1, false, Item.Ferramenta, P(Item.Madeira, 3), P(Item.Pregos, 6)),
            // a carne de caça assada na fogueira acesa (12/11/2026, a fauna; no fim, para os índices das outras não mudarem)
            Fogo(Item.CarneAssada, 1, P(Item.CarneCrua)),
        };

        // a receita do fogo (11/11/2026): a da habilidade, com 1 combustível
        static Receita F(Player.Habilidade h, Item resultado, bool oficina, params Parte[] gasta)
        {
            var r = H(h, resultado, 1, oficina, Item.Nada, gasta);
            r.combustivel = 1;
            return r;
        }

        static Desmontagem D(Item item, string verbo, Item ferramenta, params Parte[] vira) =>
            new Desmontagem { item = item, verbo = verbo, ferramenta = ferramenta, vira = vira };

        /// <summary>O que se desmonta (um item de cada vez).</summary>
        public static readonly Desmontagem[] Desmontagens =
        {
            D(Item.Roupa, "desfiar", Item.Nada, P(Item.Pano, 2)),
            D(Item.Toras, "rachar", Item.Machado, P(Item.Madeira, 3)),
            D(Item.KitMedico, "desmontar", Item.Nada, P(Item.Atadura), P(Item.Gaze, 2), P(Item.Desinfetante)),
            D(Item.Bandagem, "desfazer", Item.Nada, P(Item.Pano)),
            D(Item.Tala, "desfazer", Item.Nada, P(Item.Madeira)),
            D(Item.Corda, "desfazer", Item.Nada, P(Item.Pano, 2)),
            D(Item.VaraDePesca, "desmontar", Item.Nada, P(Item.Madeira), P(Item.Fios, 2)),
            // (09/11/2026, a sucata, 2ª leva: a garrafa térmica vira sucata, como o resumo dela já dizia)
            D(Item.GarrafaTermica, "desmontar", Item.Nada, P(Item.Sucata, 2)),
            D(Item.KitDeReparo, "desmontar", Item.Nada, P(Item.PecasDeMetal, 2), P(Item.FitaAdesiva)),
            D(Item.Fogueira, "desmontar", Item.Nada, P(Item.Madeira, 3)),
            D(Item.Cama, "desmontar", Item.Nada, P(Item.Pano, 3), P(Item.Madeira, 2)),
            D(Item.Barricada, "desmontar", Item.Ferramenta, P(Item.Madeira, 3), P(Item.Pregos, 4)),
            // as armas viram peças de metal (a matéria do kit de reparo) e, desde 09/11/2026, sucata (a 2ª leva: "desconstruindo
            // algum material que possui metal em sua composição"); a quebrada também (a de fogo fica, esperando o kit)
            D(Item.Pistola, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal), P(Item.Sucata, 3)),
            D(Item.Submetralhadora, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal), P(Item.Sucata, 5)),
            D(Item.Rifle, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 2), P(Item.Sucata, 5)),
            D(Item.Espingarda, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal), P(Item.Sucata, 4), P(Item.Madeira)),
            D(Item.Sniper, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 2), P(Item.Sucata, 5), P(Item.Madeira)),
            D(Item.Faca, "desmontar", Item.Nada, P(Item.Sucata)),
            D(Item.Machado, "desmontar", Item.Nada, P(Item.Sucata, 2), P(Item.Madeira)),
            D(Item.Bastao, "desmontar", Item.Nada, P(Item.Madeira), P(Item.Pregos, 3)),
            D(Item.Lanca, "desmontar", Item.Nada, P(Item.Madeira), P(Item.Sucata)),
            // (09/11/2026, a sucata, 2ª leva) as armas de sucata voltam a ser sucata (menos que a receita: perde-se um pouco)
            D(Item.PistolaSucata, "desmontar", Item.Nada, P(Item.Sucata, 4)),
            D(Item.SubmetralhadoraSucata, "desmontar", Item.Ferramenta, P(Item.Sucata, 8)),
            D(Item.RifleSucata, "desmontar", Item.Ferramenta, P(Item.Sucata, 7), P(Item.Madeira)),
            // as ferramentas e o metal solto: o martelo e a chave, as peças de metal e o pé de cabra
            D(Item.Ferramenta, "desmontar", Item.Nada, P(Item.Sucata, 2)),
            D(Item.PecasDeMetal, "desmontar", Item.Nada, P(Item.Sucata, 2)),
            D(Item.PeDeCabra, "desmontar", Item.Nada, P(Item.Sucata, 2)),
            // as peças de carro (a vela, a bateria, o radiador e o macaco; o pneu não: é borracha)
            D(Item.Vela, "desmontar", Item.Nada, P(Item.Sucata)),
            D(Item.Bateria, "desmontar", Item.Ferramenta, P(Item.Sucata, 2)),
            D(Item.Radiador, "desmontar", Item.Ferramenta, P(Item.Sucata, 3)),
            D(Item.Macaco, "desmontar", Item.Nada, P(Item.Sucata, 3)),
        };

        /// <summary>
        /// O desmontar que dá sucata (09/11/2026, a 2ª leva) leva uns segundos, com a barra na mochila (fechar a mochila
        /// interrompe): <see cref="SegundosDoMetal"/> nas coisas pequenas, <see cref="SegundosDoMetalGrande"/> nas armas e nas
        /// peças grandes (3 sucatas ou mais). Os outros (desfiar a roupa, rachar a tora) continuam na hora.
        /// </summary>
        public const float SegundosDoMetal = 2f, SegundosDoMetalGrande = 3.5f;

        /// <summary>Quantas sucatas o desmontar dá (sem o Sucateiro).</summary>
        public static int SucataDe(Desmontagem d)
        {
            int n = 0;
            if (d?.vira != null) foreach (var p in d.vira) if (p.item == Item.Sucata) n += p.qtd;
            return n;
        }

        /// <summary>As peças com 'bonus' sucatas a mais (o Sucateiro), na sucata que já sai (sem sucata, as mesmas).</summary>
        public static Parte[] ComBonus(Parte[] vira, int bonus)
        {
            if (vira == null || bonus <= 0) return vira;
            var c = new Parte[vira.Length];
            bool deu = false;
            for (int i = 0; i < vira.Length; i++)
            {
                bool esta = !deu && vira[i].item == Item.Sucata;
                c[i] = esta ? new Parte(Item.Sucata, vira[i].qtd + bonus) : vira[i];
                deu |= esta;
            }
            return c;
        }

        /// <summary>Quanto tempo o desmontar leva (0: na hora).</summary>
        public static float SegundosPara(Desmontagem d)
        {
            int n = SucataDe(d);
            if (n <= 0) return 0f;
            return n >= 3 || Itens.EhArma(d.item) ? SegundosDoMetalGrande : SegundosDoMetal;
        }

        /// <summary>Como o item se desmonta (null: não se desmonta).</summary>
        public static Desmontagem DesmontagemDe(Item item)
        {
            foreach (var d in Desmontagens)
                if (d.item == item) return d;
            // as roupas de verdade também se desfiam em 2 panos, como a "roupa" (o Plano_Saque.md da pasta de arte, a decisão 7)
            if (Itens.EhRoupa(item))
            {
                if (!_dasRoupas.TryGetValue(item, out var r)) _dasRoupas[item] = r = D(item, "desfiar", Item.Nada, P(Item.Pano, 2));
                return r;
            }
            return null;
        }

        static readonly Dictionary<Item, Desmontagem> _dasRoupas = new Dictionary<Item, Desmontagem>();

        /// <summary>A lista do que sai ("2 panos, 1 corda").</summary>
        public static string Lista(IEnumerable<Parte> partes)
        {
            var nomes = new List<string>();
            foreach (var p in partes) nomes.Add(Itens.ComNumero(p.item, p.qtd));
            return nomes.Count <= 1 ? (nomes.Count == 1 ? nomes[0] : "") : string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
        }
    }
}
