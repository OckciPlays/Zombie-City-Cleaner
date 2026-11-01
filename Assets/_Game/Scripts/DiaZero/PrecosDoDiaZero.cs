using System.Collections.Generic;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>As lojas da rua do Dia 0 (01/11/2026, a pedido).</summary>
    public enum LojaDoDia : byte { Mercado, Farmacia, Armas, Ferragens, Roupas, Posto }

    /// <summary>Um produto à venda: o item, o preço em dólares e o desgaste dele (o galão vazio é 100; o cheio, 0).</summary>
    public readonly struct Produto
    {
        public readonly Item item;
        public readonly int preco;
        public readonly byte desgaste;
        public readonly string nome;   // vazio: o nome do item

        public Produto(Item item, int preco, byte desgaste = 0, string nome = null)
        {
            this.item = item;
            this.preco = preco;
            this.desgaste = desgaste;
            this.nome = nome;
        }

        public string Nome => string.IsNullOrEmpty(nome) ? Itens.Nome(item) : nome;
    }

    /// <summary>
    /// Os preços do Dia 0 (a pedido em 01/11/2026: "1000 dólares, que são suas economias"; todos os itens do saque, menos a
    /// matéria-prima que se acha depois, como a madeira e o pano). Os mesmos em todas as máquinas: o anfitrião confere a
    /// compra por esta tabela, e não pelo que o cliente mandou. As roupas da loja de roupas têm o preço por peça
    /// (<see cref="PrecoDaRoupa"/>).
    /// </summary>
    public static class PrecosDoDiaZero
    {
        /// <summary>As economias de cada jogador.</summary>
        public const int Economias = 1000;

        public static string Nome(LojaDoDia l) => l switch
        {
            LojaDoDia.Mercado => "Mercado",
            LojaDoDia.Farmacia => "Farmácia",
            LojaDoDia.Armas => "Loja de armas",
            LojaDoDia.Ferragens => "Ferragens e autopeças",
            LojaDoDia.Roupas => "Loja de roupas",
            _ => "Loja do posto",
        };

        /// <summary>O que vai escrito na placa da loja.</summary>
        public static string Placa(LojaDoDia l) => l switch
        {
            LojaDoDia.Mercado => "MERCADO",
            LojaDoDia.Farmacia => "FARMÁCIA",
            LojaDoDia.Armas => "ARMAS",
            LojaDoDia.Ferragens => "FERRAGENS",
            LojaDoDia.Roupas => "ROUPAS",
            _ => "CONVENIÊNCIA",
        };

        static readonly Dictionary<LojaDoDia, Produto[]> _produtos = new Dictionary<LojaDoDia, Produto[]>
        {
            [LojaDoDia.Mercado] = new[]
            {
                new Produto(Item.Comida, 3), new Produto(Item.Feijao, 2), new Produto(Item.Milho, 2), new Produto(Item.Sopa, 3),
                new Produto(Item.Carne, 6), new Produto(Item.Agua, 2), new Produto(Item.Leite, 4), new Produto(Item.Suco, 4),
                new Produto(Item.RefrigeranteGarrafa, 3), new Produto(Item.RefrigeranteLata, 1), new Produto(Item.Energetico, 3),
                new Produto(Item.Salgadinho, 3), new Produto(Item.Chocolate, 2), new Produto(Item.BarraDeEnergia, 2),
                new Produto(Item.GarrafaTermica, 20), new Produto(Item.Pilhas, 8), new Produto(Item.Lanterna, 25),
            },
            [LojaDoDia.Farmacia] = new[]
            {
                new Produto(Item.Remedio, 12), new Produto(Item.Comprimidos, 9), new Produto(Item.Antibiotico, 25),
                new Produto(Item.KitMedico, 45), new Produto(Item.Atadura, 5), new Produto(Item.Bandagem, 5), new Produto(Item.Gaze, 4),
                new Produto(Item.Desinfetante, 6), new Produto(Item.SprayAntisseptico, 8), new Produto(Item.Agua, 2),
                new Produto(Item.BarraDeEnergia, 2),
            },
            [LojaDoDia.Armas] = new[]
            {
                new Produto(Item.Pistola, 450), new Produto(Item.Espingarda, 400), new Produto(Item.Submetralhadora, 650),
                new Produto(Item.Rifle, 850), new Produto(Item.Sniper, 1000),
                new Produto(Item.Municao9mm, 20), new Produto(Item.Municao12, 25), new Produto(Item.Municao556, 25), new Produto(Item.Municao762, 30),
                new Produto(Item.Faca, 40), new Produto(Item.Machado, 45), new Produto(Item.Coldre, 35, 0, "coldre da pistola"),
            },
            [LojaDoDia.Ferragens] = new[]
            {
                new Produto(Item.KitDeReparo, 30), new Produto(Item.Macaco, 50), new Produto(Item.Pneu, 90), new Produto(Item.Bateria, 120),
                new Produto(Item.Vela, 10), new Produto(Item.Radiador, 150), new Produto(Item.Galao, 15, 100, "galão de gasolina (vazio)"),
                new Produto(Item.Corda, 12), new Produto(Item.FitaAdesiva, 6), new Produto(Item.Ferramenta, 25), new Produto(Item.Pregos, 5),
                new Produto(Item.VaraDePesca, 35), new Produto(Item.Isca, 5), new Produto(Item.Lanterna, 25), new Produto(Item.Pilhas, 8),
                new Produto(Item.Machado, 45), new Produto(Item.Faca, 40),
                new Produto(Item.Agua, 2), new Produto(Item.RefrigeranteLata, 1), new Produto(Item.Energetico, 3),
            },
            [LojaDoDia.Roupas] = new[]
            {
                new Produto(Item.MochilaPequena, 30), new Produto(Item.MochilaMedia, 60), new Produto(Item.MochilaGrande, 100),
            },
            [LojaDoDia.Posto] = new[]
            {
                new Produto(Item.Galao, 32, 0, "galão com 10 L de gasolina"), new Produto(Item.Galao, 15, 100, "galão de gasolina (vazio)"),
                new Produto(Item.Agua, 2), new Produto(Item.RefrigeranteLata, 1), new Produto(Item.Energetico, 3), new Produto(Item.Salgadinho, 3),
                new Produto(Item.Chocolate, 2), new Produto(Item.BarraDeEnergia, 2), new Produto(Item.Pilhas, 8), new Produto(Item.Lanterna, 25),
            },
        };

        public static Produto[] Produtos(LojaDoDia l) => _produtos.TryGetValue(l, out var p) ? p : new Produto[0];

        /// <summary>O produto 'indice' da loja (o que o cliente manda), ou null.</summary>
        public static Produto? DaLoja(LojaDoDia l, int indice)
        {
            var p = Produtos(l);
            return indice >= 0 && indice < p.Length ? p[indice] : (Produto?)null;
        }

        static readonly Dictionary<string, int> _roupas = new Dictionary<string, int>
        {
            // a cabeça
            ["Bone"] = 15, ["Bone_Militar"] = 25, ["Gorro"] = 12, ["Capacete"] = 35, ["Chapeu_Cauboi"] = 40, ["Capacete_Militar"] = 90, ["Bandana"] = 8,
            // os óculos
            ["Oculos_Sol"] = 20, ["Oculos_Aviador"] = 35, ["Oculos_Protecao"] = 15, ["Oculos_Esportivo"] = 30, ["Oculos_Tatico"] = 45, ["Visao_Noturna"] = 350,
            // o rosto
            ["Mascara_Cirurgica"] = 3, ["Mascara_Pano"] = 8, ["Bandana_Rosto"] = 8, ["Gola_Tubular"] = 12, ["Respirador"] = 30, ["Mascara_Gas"] = 120, ["Balaclava"] = 15,
            // o tronco e por cima
            ["Camiseta_V"] = 15, ["Camisa_Xadrez"] = 30, ["Camisa_Social"] = 35, ["Regata"] = 10, ["Camisa_Militar"] = 40,
            ["Jaqueta_Capuz"] = 55, ["Jaqueta_Lona"] = 70, ["Jaqueta_Couro"] = 150, ["Jaqueta_Esportiva"] = 60, ["Moletom"] = 40, ["Moletom_Ziper"] = 45,
            ["Colete_Refletivo"] = 20, ["Colete_Tatico"] = 180, ["Cinto_Ferramentas"] = 25, ["Cinto_Tatico"] = 40,
            // as pernas, os pés e as mãos
            ["Calca_Cargo"] = 45, ["Calca_Militar"] = 55, ["Calca_Trabalho"] = 50, ["Jardineira"] = 60, ["Jeans_Rasgado"] = 45,
            ["Bota_Trabalho"] = 80, ["Bota_Couro"] = 95, ["Coturno"] = 110, ["Tenis_Baixo"] = 50, ["Bota_Cauboi"] = 90,
            ["Luva_Tatica"] = 35, ["Luva_Sem_Dedos"] = 20,
        };

        /// <summary>O preço de uma peça da loja de roupas (0: não está à venda).</summary>
        public static int PrecoDaRoupa(string peca) => peca != null && _roupas.TryGetValue(peca, out int p) ? p : 0;

        /// <summary>A peça da loja no lugar 'lugar' (o de <see cref="AparenciaDoJogador.LugaresDaLoja"/>) e o índice dela + 1.</summary>
        public static int PrecoDaRoupa(int lugar, int valor)
        {
            var l = AparenciaDoJogador.LugaresDaLoja;
            if (lugar < 0 || lugar >= l.Length || valor <= 0 || valor > l[lugar].pecas.Length) return 0;
            return PrecoDaRoupa(l[lugar].pecas[valor - 1]);
        }

        /// <summary>"$ 1.000" (o ponto nos milhares, como no Brasil).</summary>
        public static string Dinheiro(int valor) => "$ " + valor.ToString("N0", new System.Globalization.CultureInfo("pt-BR"));
    }
}
