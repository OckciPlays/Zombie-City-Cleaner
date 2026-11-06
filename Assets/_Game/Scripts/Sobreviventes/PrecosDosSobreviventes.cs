using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>O que cada comerciante do acampamento vende (e compra): uma categoria só (02/11/2026, a pedido).</summary>
    public enum CategoriaDoComercio : byte { Alimentos, Armas, Municao, Sobrevivencia, PecasDeCarro, Materiais, Roupas }

    /// <summary>
    /// Os preços dos acampamentos (02/11/2026, a pedido: "só dinheiro", "compra por 20, vende por 10", e o dinheiro que
    /// sobrou do Dia 0 serve aqui). A base é o preço do Dia 0 (<see cref="PrecosDoDiaZero"/>; o que não tinha lá, como a
    /// madeira e o pano, tem o seu aqui), vezes o fator do pós-apocalipse de cada categoria: a comida, a munição e os remédios
    /// ficaram bem mais caros. Vender rende sempre a metade do preço de compra, vezes o estado do item (o desgaste da arma,
    /// as balas na caixa, a carga da peça, os litros do galão). Os mesmos em todas as máquinas: o anfitrião confere tudo
    /// por esta tabela.
    /// </summary>
    public static class PrecosDosSobreviventes
    {
        public const int Categorias = 7;
        /// <summary>Vender rende esta fração do preço de compra.</summary>
        public const float FracaoDaVenda = 0.5f;

        public static string Nome(CategoriaDoComercio c) => c switch
        {
            CategoriaDoComercio.Alimentos => "comida e bebida",
            CategoriaDoComercio.Armas => "armas",
            CategoriaDoComercio.Municao => "munição",
            CategoriaDoComercio.Sobrevivencia => "remédios e sobrevivência",
            CategoriaDoComercio.PecasDeCarro => "peças de carro",
            CategoriaDoComercio.Materiais => "materiais",
            _ => "roupas e mochilas",
        };

        /// <summary>O que vai escrito na placa da banca.</summary>
        public static string Placa(CategoriaDoComercio c) => c switch
        {
            CategoriaDoComercio.Alimentos => "COMIDA",
            CategoriaDoComercio.Armas => "ARMAS",
            CategoriaDoComercio.Municao => "MUNIÇÃO",
            CategoriaDoComercio.Sobrevivencia => "REMÉDIOS",
            CategoriaDoComercio.PecasDeCarro => "PEÇAS",
            CategoriaDoComercio.Materiais => "MATERIAIS",
            _ => "ROUPAS",
        };

        static readonly Item[][] _itens =
        {
            new[] { Item.Comida, Item.Feijao, Item.Milho, Item.Sopa, Item.Carne, Item.Salgadinho, Item.Chocolate, Item.BarraDeEnergia,
                    Item.Agua, Item.CopoDeAgua, Item.Leite, Item.Suco, Item.RefrigeranteLata, Item.RefrigeranteGarrafa, Item.Energetico, Item.GarrafaTermica },
            new[] { Item.Pistola, Item.Espingarda, Item.Submetralhadora, Item.Rifle, Item.Sniper, Item.Faca, Item.Machado, Item.Bastao, Item.Lanca },
            new[] { Item.Municao9mm, Item.Municao12, Item.Municao556, Item.Municao762 },
            new[] { Item.Remedio, Item.Comprimidos, Item.Antibiotico, Item.KitMedico, Item.Atadura, Item.Bandagem, Item.Gaze, Item.Desinfetante,
                    Item.SprayAntisseptico, Item.Lanterna, Item.Pilhas, Item.Tocha, Item.Fogueira, Item.Cama, Item.Barricada,
                    // (05/11/2026) no fim: o save guarda o estoque pelo índice do produto (a tala; a bandagem antisséptica,
                    // da etapa 2)
                    Item.Tala, Item.BandagemAntisseptica },
            new[] { Item.Pneu, Item.Bateria, Item.Vela, Item.Radiador, Item.Macaco, Item.Galao, Item.KitDeReparo, Item.Ferramenta },
            new[] { Item.Madeira, Item.Toras, Item.Pano, Item.Corda, Item.FitaAdesiva, Item.PecasDeMetal, Item.Fios, Item.Pregos, Item.Isca, Item.VaraDePesca },
            new[] { Item.MochilaPequena, Item.MochilaMedia, Item.MochilaGrande, Item.Coldre, Item.Roupa, Item.Pano },
        };

        static readonly Dictionary<CategoriaDoComercio, Item[]> _noJogo = new Dictionary<CategoriaDoComercio, Item[]>();

        /// <summary>O que o comerciante da categoria vende (e compra), só o que existe no jogo; o índice é o do estoque.</summary>
        public static Item[] Itens(CategoriaDoComercio c)
        {
            if (_noJogo.TryGetValue(c, out var l)) return l;
            var lista = new List<Item>();
            foreach (var i in _itens[(int)c]) if (Loot.Itens.NoJogo(i)) lista.Add(i);
            l = lista.ToArray();
            _noJogo[c] = l;
            return l;
        }

        /// <summary>O comerciante da categoria compra este item.</summary>
        public static bool Compra(CategoriaDoComercio c, Item item) => System.Array.IndexOf(Itens(c), item) >= 0;

        // os preços que o Dia 0 não tinha
        static readonly Dictionary<Item, int> _semPrecoNoDia = new Dictionary<Item, int>
        {
            [Item.Madeira] = 4, [Item.Toras] = 6, [Item.Pano] = 3, [Item.PecasDeMetal] = 6, [Item.Fios] = 4, [Item.Roupa] = 5,
            [Item.Bastao] = 15, [Item.Lanca] = 25, [Item.Tocha] = 10, [Item.CopoDeAgua] = 1, [Item.Fogueira] = 12, [Item.Cama] = 30,
            [Item.Barricada] = 25,
        };

        static Dictionary<Item, int> _base;

        // o preço do Dia 0 (o menor, se aparece em mais de uma loja; o galão, o cheio)
        static int Base(Item item)
        {
            if (_base == null)
            {
                _base = new Dictionary<Item, int>(_semPrecoNoDia);
                foreach (LojaDoDia l in System.Enum.GetValues(typeof(LojaDoDia)))
                    foreach (var p in PrecosDoDiaZero.Produtos(l))
                    {
                        if (p.item == Item.Galao && p.desgaste > 0) continue;   // o galão vazio
                        if (!_base.TryGetValue(p.item, out int antes) || p.preco < antes) _base[p.item] = p.preco;
                    }
            }
            return _base.TryGetValue(item, out int preco) ? preco : 10;
        }

        /// <summary>O fator do pós-apocalipse de cada categoria.</summary>
        static float Fator(Item item)
        {
            if (Loot.Itens.EhMunicao(item)) return 2.5f;
            if (Loot.Itens.EhRemedio(item)) return 2.5f;
            if (Loot.Itens.EhArma(item)) return 1.2f;
            if (Loot.Itens.EhPeca(item)) return 1.3f;
            var loot = Loot.Itens.Loot(item);
            if (loot != null && (loot.tipo == TipoDeLoot.Comida || loot.tipo == TipoDeLoot.Bebida)) return 2f;
            if (loot != null && loot.tipo == TipoDeLoot.Material) return 1.5f;
            return 1.2f;
        }

        /// <summary>O antibiótico no acampamento (05/11/2026, a etapa 2): $100 fixo, o remédio mais caro (cura a infecção).</summary>
        public const int PrecoDoAntibiotico = 100;

        /// <summary>
        /// O preço de compra de um (a caixa de munição cheia, a peça nova, o galão cheio, a arma com o pente cheio, o spray
        /// com os 5 usos). O antibiótico é fixo (<see cref="PrecoDoAntibiotico"/>).
        /// </summary>
        public static int PrecoDeCompra(Item item) => item == Item.Antibiotico ? PrecoDoAntibiotico
            : Mathf.Max(1, Mathf.RoundToInt(Base(item) * Fator(item)));

        /// <summary>Quanto o comerciante guarda de cada item (o máximo do estoque).</summary>
        public static int Maximo(Item item)
        {
            if (Loot.Itens.EhArma(item)) return item == Item.Sniper ? 1 : 2;
            if (Loot.Itens.EhMochila(item)) return 1;
            if (Loot.Itens.EhMunicao(item)) return 5;
            if (Loot.Itens.EhPeca(item)) return 2;
            // os remédios (05/11/2026, sem a vida voltando sozinha): mais do que estanca e do analgésico, menos do kit, da tala
            // e do antibiótico (a etapa 2: no máximo 2)
            if (item == Item.Atadura || item == Item.Bandagem || item == Item.BandagemAntisseptica || item == Item.Comprimidos) return 6;
            if (item == Item.KitMedico || item == Item.Tala || item == Item.Antibiotico) return 2;
            if (Loot.Itens.EhRemedio(item)) return 3;
            int p = PrecoDeCompra(item);
            return p >= 60 ? 2 : p >= 20 ? 3 : 6;
        }

        /// <summary>
        /// O quanto vale este item (o preço de compra vezes o estado dele): a arma e a lanterna pelo desgaste; a caixa de
        /// munição pelas balas; as pilhas do saque pela quantidade; a peça pela carga; o galão pelos litros; o spray pelos
        /// usos que sobram.
        /// </summary>
        public static float Valor(ItemNaMochila it)
        {
            float preco = PrecoDeCompra(it.item);
            if (Loot.Itens.ComUsos(it.item)) return preco * Loot.Itens.UsosQueSobram(it.desgaste) / (float)Loot.Itens.UsosDoSpray;
            if (Loot.Itens.EhMunicao(it.item)) return preco * Loot.Itens.Quantos(it.item, it.qtd) / (float)Loot.Itens.MaxPorCaixa;
            if (Loot.Itens.EmPilha(it.item)) return preco * Loot.Itens.Quantos(it.item, it.qtd);
            if (it.item == Item.Galao) return preco * Mathf.Lerp(0.45f, 1f, InventarioJogador.LitrosDe(it) / 10f);
            if (Loot.Itens.ComEstado(it.item)) return preco * Mathf.Clamp01(it.qtd / 100f);
            if (Loot.Itens.ComDesgaste(it.item) || it.item == Item.Lanterna) return preco * Mathf.Lerp(1f, 0.25f, it.desgaste / 100f);
            return preco;
        }

        /// <summary>Quanto o comerciante paga por este item (a metade do valor; pelo menos 1, se valer alguma coisa).</summary>
        public static int PrecoDeVenda(ItemNaMochila it)
        {
            float v = Valor(it) * FracaoDaVenda;
            return v <= 0.01f ? 0 : Mathf.Max(1, Mathf.FloorToInt(v));
        }
    }
}
