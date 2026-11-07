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
        /// <summary>A habilidade que destrava a receita (o número do <see cref="Player.Habilidade"/>; -1: nenhuma).</summary>
        public int habilidade = -1;
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

        // a receita da árvore de habilidades (06/11/2026): só com a habilidade, na bancada ou na mesa de oficina
        static Receita H(Player.Habilidade h, Item resultado, int qtd, bool oficina, Item ferramenta, params Parte[] gasta) =>
            new Receita { resultado = resultado, qtd = qtd, bancada = !oficina, oficina = oficina, ferramenta = ferramenta, gasta = gasta, habilidade = (int)h };

        /// <summary>As receitas, na ordem da aba Criar (o anfitrião recebe o índice).</summary>
        public static readonly Receita[] Todas =
        {
            // em qualquer lugar
            // a bandagem de pano: 2 panos dão 1 (05/11/2026, sem a vida voltando sozinha: eram 2, e as roupas viravam cura sem fim)
            R(Item.Bandagem, 1, false, Item.Nada, P(Item.Pano, 2)),
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
        };

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
            D(Item.GarrafaTermica, "desmontar", Item.Nada, P(Item.PecasDeMetal)),
            D(Item.KitDeReparo, "desmontar", Item.Nada, P(Item.PecasDeMetal, 2), P(Item.FitaAdesiva)),
            D(Item.Fogueira, "desmontar", Item.Nada, P(Item.Madeira, 3)),
            D(Item.Cama, "desmontar", Item.Nada, P(Item.Pano, 3), P(Item.Madeira, 2)),
            D(Item.Barricada, "desmontar", Item.Ferramenta, P(Item.Madeira, 3), P(Item.Pregos, 4)),
            // as armas viram sucata (a matéria do kit de reparo)
            D(Item.Pistola, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 2)),
            D(Item.Submetralhadora, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 3)),
            D(Item.Rifle, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 4)),
            D(Item.Espingarda, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 3), P(Item.Madeira)),
            D(Item.Sniper, "desmontar", Item.Ferramenta, P(Item.PecasDeMetal, 4), P(Item.Madeira)),
            D(Item.Faca, "desmontar", Item.Nada, P(Item.PecasDeMetal)),
            D(Item.Machado, "desmontar", Item.Nada, P(Item.PecasDeMetal), P(Item.Madeira)),
            D(Item.Bastao, "desmontar", Item.Nada, P(Item.Madeira), P(Item.Pregos, 3)),
            D(Item.Lanca, "desmontar", Item.Nada, P(Item.Madeira), P(Item.PecasDeMetal)),
        };

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
