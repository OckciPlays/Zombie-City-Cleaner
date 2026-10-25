using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O que vai na mochila (e na mão): uma arma (o tipo + 1) ou a comida e a água; as caixas de munição (uma por
    /// calibre, só na grade: não vão na mão); e o que se veste (os equipamentos: as 3 mochilas e o coldre), que não vai na
    /// grade nem na mão. Vai pela rede num byte.
    /// </summary>
    public enum Item : byte
    {
        Nada = 0, Pistola, Rifle, Submetralhadora, Espingarda, Sniper, Comida = 16, Agua,
        MochilaPequena = 32, MochilaMedia, MochilaGrande, Coldre,
        Municao9mm = 48, Municao556, Municao12, Municao762,
    }

    /// <summary>Uma caixa de munição: o nome do modelo, o nome do calibre e o resumo.</summary>
    public class DadosDaMunicao
    {
        public string modelo, calibre, resumo;
        /// <summary>Quantas balas uma caixa achada costuma ter (o anfitrião sorteia entre os dois).</summary>
        public int achadaMin, achadaMax;
    }

    /// <summary>Um equipamento: o nome do modelo, o resumo e, nas mochilas, o espaço da grade (colunas × linhas).</summary>
    public class DadosDoEquipamento
    {
        public string modelo, nome, nomeComArtigo, resumo;
        public UnityEngine.Vector2Int espaco;
    }

    /// <summary>Os números da comida e da água: quanto cada uma mata da fome e da sede, e o tempo de comer ou beber.</summary>
    public class DadosDoConsumivel
    {
        public string nome, nomeComArtigo, verbo;
        /// <summary>Quanto sobe a barra da fome e a da sede (de 0 a 100).</summary>
        public float fome, sede;
        /// <summary>O clipe Usar inteiro (s): o item só acaba no fim, e o efeito vem junto.</summary>
        public float tempoDeUso;
    }

    public static class Itens
    {
        /// <summary>
        /// Os modelos de tudo o que vai na mão: as armas (na ordem de <see cref="TipoDeArma"/>) e depois a lata e a
        /// garrafa (Jogador_FP_&lt;nome&gt;.fbx, Item_&lt;nome&gt;, Icone_&lt;nome&gt;).
        /// </summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper", "Lata", "Garrafa" };
        public const int QuantosModelos = Armas.Quantas + 2;

        /// <summary>
        /// Na primeira pessoa há mais um modelo, depois dos de <see cref="Modelos"/>: os punhos da mão vazia
        /// (Jogador_FP_Maos.fbx, para o soco).
        /// </summary>
        public const int Punhos = QuantosModelos;
        public const int QuantosModelosFP = QuantosModelos + 1;
        public static string ModeloFP(int modelo) => modelo == Punhos ? "Maos" : Modelos[modelo];
        /// <summary>O modelo de primeira pessoa do que está na mão (os punhos, com a mão vazia).</summary>
        public static int ModeloNaMao(Item i) => i == Item.Nada ? Punhos : Modelo(i);

        static readonly DadosDoConsumivel[] _consumiveis =
        {
            // a conserva mata a fome e um pouco da sede (o caldo); a água, só a sede. Os tempos são os do clipe Usar
            // (itens.py: 40 e 44 quadros a 24 fps)
            new DadosDoConsumivel { nome = "lata de comida", nomeComArtigo = "a lata de comida", verbo = "comer", fome = 40f, sede = 5f, tempoDeUso = 40 / 24f },
            new DadosDoConsumivel { nome = "garrafa de água", nomeComArtigo = "a garrafa de água", verbo = "beber", fome = 0f, sede = 45f, tempoDeUso = 44 / 24f },
        };

        /// <summary>
        /// Os equipamentos, na ordem do enum (de MochilaPequena em diante): Equipamentos.fbx (&lt;modelo&gt;_*),
        /// Icone_&lt;modelo&gt; e Foto_&lt;modelo&gt;, feitos pelo Blender/equipamentos.py.
        /// Sem mochila, o jogador só tem os bolsos (<see cref="Bolsos"/>).
        /// </summary>
        static readonly DadosDoEquipamento[] _equipamentos =
        {
            new DadosDoEquipamento { modelo = "MochilaPequena", nome = "mochila pequena", nomeComArtigo = "a mochila pequena", resumo = "De lona. Leva uma arma nas costas.", espaco = new(6, 4) },
            new DadosDoEquipamento { modelo = "MochilaMedia", nome = "mochila média", nomeComArtigo = "a mochila média", resumo = "Militar. Leva uma arma nas costas.", espaco = new(8, 5) },
            new DadosDoEquipamento { modelo = "MochilaGrande", nome = "mochila grande", nomeComArtigo = "a mochila grande", resumo = "De trilha. Leva uma arma nas costas.", espaco = new(10, 7) },
            new DadosDoEquipamento { modelo = "Coldre", nome = "coldre", nomeComArtigo = "o coldre", resumo = "De couro. Leva uma arma curta na cintura." },
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
        };
        public static int QuantasMunicoes => _municoes.Length;
        public static bool EhMunicao(Item i) => i >= Item.Municao9mm && i <= Item.Municao762;
        /// <summary>O índice da caixa (0 a 3, a ordem de <see cref="Calibre"/>), ou -1.</summary>
        public static int IndiceDaMunicao(Item i) => EhMunicao(i) ? i - Item.Municao9mm : -1;
        public static Calibre CalibreDa(Item caixa) => (Calibre)IndiceDaMunicao(caixa);
        public static Item CaixaDe(Calibre c) => (Item)((int)Item.Municao9mm + (int)c);
        public static DadosDaMunicao Municao(Calibre c) => _municoes[(int)c];
        public static string ModeloDaMunicao(int k) => _municoes[k].modelo;
        /// <summary>O nome do calibre ("9mm", "5,56", "calibre 12", "7,62").</summary>
        public static string NomeDoCalibre(Calibre c) => _municoes[(int)c].calibre;
        /// <summary>O calibre de uma arma.</summary>
        public static Calibre CalibreDaArma(Item arma) => Armas.Dados(Arma(arma)).calibre;
        /// <summary>
        /// As balas que o item leva quando aparece sem dizer quantas: a arma, o pente cheio; a caixa, cheia. Os outros, 0.
        /// </summary>
        public static int QuantidadePadrao(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).capacidade : EhMunicao(i) ? MaxPorCaixa : 0;
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

        public static bool EhArma(Item i) => i >= Item.Pistola && i <= Item.Sniper;
        public static bool EhConsumivel(Item i) => i == Item.Comida || i == Item.Agua;
        public static bool EhEquipamento(Item i) => i >= Item.MochilaPequena && i <= Item.Coldre;
        public static bool EhMochila(Item i) => i >= Item.MochilaPequena && i <= Item.MochilaGrande;
        /// <summary>As armas curtas: as que cabem no coldre (a pistola e a submetralhadora).</summary>
        public static bool EhCurta(Item i) => i == Item.Pistola || i == Item.Submetralhadora;
        /// <summary>O índice do equipamento (0 a 3, na ordem de _equipamentos), ou -1.</summary>
        public static int Equipamento(Item i) => EhEquipamento(i) ? i - Item.MochilaPequena : -1;
        public static Item DoEquipamento(int k) => (Item)((int)Item.MochilaPequena + k);
        public static DadosDoEquipamento DadosDe(Item i) => EhEquipamento(i) ? _equipamentos[Equipamento(i)] : null;
        public static string ModeloDoEquipamento(int k) => _equipamentos[k].modelo;
        /// <summary>O espaço da grade com esta mochila (Nada: os bolsos).</summary>
        public static UnityEngine.Vector2Int EspacoDa(Item mochila) => EhMochila(mochila) ? _equipamentos[Equipamento(mochila)].espaco : Bolsos;
        public static TipoDeArma Arma(Item i) => (TipoDeArma)((int)i - 1);
        public static Item DaArma(TipoDeArma t) => (Item)((int)t + 1);
        public static DadosDoConsumivel Consumivel(Item i) => _consumiveis[i == Item.Agua ? 1 : 0];

        /// <summary>O índice do modelo do item (na mão, no chão e no ícone), ou -1 (nada).</summary>
        public static int Modelo(Item i) => EhArma(i) ? (int)Arma(i) : i == Item.Comida ? Armas.Quantas : i == Item.Agua ? Armas.Quantas + 1 : -1;

        // as células que cada modelo ocupa na mochila (largura × altura), na ordem de Modelos: pelo tamanho da malha, uns
        // 17 cm por célula (o ícone tem esta proporção)
        static readonly UnityEngine.Vector2Int[] _tamanhos =
        {
            new(2, 1), new(5, 2), new(3, 2), new(6, 1), new(7, 2),   // pistola, rifle, submetralhadora, espingarda, sniper
            new(1, 1), new(1, 2),                                   // lata, garrafa
        };

        /// <summary>As células que o item ocupa na mochila (largura × altura).</summary>
        public static UnityEngine.Vector2Int Tamanho(Item i)
        {
            if (EhMunicao(i)) return new UnityEngine.Vector2Int(1, 1);
            int m = Modelo(i);
            return m >= 0 ? _tamanhos[m] : UnityEngine.Vector2Int.zero;
        }

        // uma linha sobre cada modelo, para os detalhes da tela do inventário (curta: os números vêm em seguida)
        static readonly string[] _resumos =
        {
            "Semiautomática. Leve e precisa.", "Automático. Bom de longe.", "Automática. Rápida, de perto.",
            "De bomba. Arrasa de perto.", "De ferrolho, com luneta.", "Conserva. Mata a fome.", "Água. Mata a sede.",
        };

        public static string Resumo(Item i)
        {
            if (EhEquipamento(i)) return DadosDe(i).resumo;
            if (EhMunicao(i)) return _municoes[IndiceDaMunicao(i)].resumo;
            int m = Modelo(i);
            return m >= 0 ? _resumos[m] : "";
        }

        public static string Nome(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nome : EhConsumivel(i) ? Consumivel(i).nome : EhEquipamento(i) ? DadosDe(i).nome
            : EhMunicao(i) ? "caixa de " + NomeDoCalibre(CalibreDa(i)) : "";
        public static string NomeComArtigo(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nomeComArtigo : EhConsumivel(i) ? Consumivel(i).nomeComArtigo : EhEquipamento(i) ? DadosDe(i).nomeComArtigo
            : EhMunicao(i) ? "a caixa de " + NomeDoCalibre(CalibreDa(i)) : "";
    }
}
