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
        // as armas brancas (o bastão, a lança e a tocha saem da fabricação)
        Faca = 8, Machado, Bastao, Lanca, Tocha,
        // o que se come e se bebe (a lata e a garrafa de água são as primeiras; as outras, do saque de 31/10/2026)
        Comida = 16, Agua, Feijao, Carne, Milho, Sopa, Salgadinho, Chocolate, BarraDeEnergia,
        RefrigeranteGarrafa, RefrigeranteLata, Energetico, CopoDeAgua, Leite, Suco, GarrafaTermica,
        MochilaPequena = 32, MochilaMedia, MochilaGrande, Coldre,
        Municao9mm = 48, Municao556, Municao12, Municao762,
        Vela = 64, Bateria, Radiador, Pneu, Macaco, ChaveDaPerua,
        // os remédios
        Remedio = 80, Comprimidos, KitMedico, Atadura, Gaze, Desinfetante, SprayAntisseptico, Antibiotico, Bandagem,
        // os materiais (a matéria-prima da fabricação)
        Madeira = 96, Toras, Pano, Corda, FitaAdesiva, Ferramenta, PecasDeMetal, Fios, Pregos, Isca, VaraDePesca, KitDeReparo, Roupa,
        // o que se monta no mundo (os kits: na mochila, um item; montados, a construção)
        Fogueira = 112, Cama, Barricada,
        // a sinuca (era do 80 ao 85 até o merge com o saque, em 31/10/2026)
        CaixaDeBolas = 128, CaixaDeBolasVazia, TacoMadeira, TacoEscuro, TacoPreto, TacoVermelho,
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

    public static class Itens
    {
        /// <summary>
        /// Os modelos de tudo o que vai na mão: as armas (na ordem de <see cref="TipoDeArma"/>), a lata, a garrafa e as
        /// armas brancas, a faca e o machado (Jogador_FP_&lt;nome&gt;.fbx, Item_&lt;nome&gt;, Icone_&lt;nome&gt;; as
        /// brancas, de 31/10/2026, saem do Blender/Armas/faca.py e machado.py, com a malha avulsa no Itens_&lt;nome&gt;.fbx).
        /// </summary>
        public static readonly string[] Modelos = { "Pistola", "Rifle", "Submetralhadora", "Espingarda", "Sniper", "Lata", "Garrafa", "Faca", "Machado", "Bastao", "Lanca", "Tocha" };
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
        public const int QuantosModelosFP = QuantosModelos + 2;
        public static string ModeloFP(int modelo) => modelo == Punhos ? "Maos" : modelo == TacoFP ? "Taco" : Modelos[modelo];
        /// <summary>O modelo de primeira pessoa do que está na mão (os punhos, com a mão vazia; a mão com o taco).</summary>
        public static int ModeloNaMao(Item i) => i == Item.Nada ? Punhos : EhTaco(i) ? TacoFP : MaoDe(i);

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
        static DadosDoLoot Material(Item i, string modelo, string nome, string artigo, string plural, string resumo, int pilha, TipoDeLoot tipo = TipoDeLoot.Material) =>
            new DadosDoLoot { item = i, modelo = modelo, nome = nome, nomeComArtigo = artigo, plural = plural, resumo = resumo, tipo = tipo, pilha = pilha };

        /// <summary>
        /// O saque (31/10/2026, a pedido: "precisamos criar alguns loots ... mais variações de latas de comida e de água,
        /// refrigerantes e algumas matérias-primas para craft"; cada um numa célula, por enquanto). A fome, a sede e a vida
        /// vão de 0 a 100 (a vida volta sozinha devagar, de barriga cheia; o remédio é a pressa).
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
            Comer(Item.Chocolate, "Chocolate", "chocolate", "o chocolate", "chocolates", "Uma barra. Um pouco de ânimo.", 5, 12f, 0f, 3f),
            Comer(Item.BarraDeEnergia, "BarraDeEnergia", "barra de energia", "a barra de energia", "barras de energia", "Cereal e mel. Leve e sustenta.", 5, 22f, 0f, 2f),
            // o que se bebe (o clipe da garrafa)
            Beber(Item.Agua, null, "garrafa de água", "a garrafa de água", "garrafas de água", "Água. Mata a sede.", 2, 0f, 45f),
            Beber(Item.CopoDeAgua, "CopoDeAgua", "copo de água", "o copo de água", "copos de água", "Copinho lacrado. Um gole só.", 5, 0f, 15f),
            Beber(Item.RefrigeranteGarrafa, "RefrigeranteGarrafa", "garrafa de refrigerante", "a garrafa de refrigerante", "garrafas de refrigerante", "Dois litros, quente e sem gás.", 2, 5f, 35f),
            Beber(Item.RefrigeranteLata, "RefrigeranteLata", "lata de refrigerante", "a lata de refrigerante", "latas de refrigerante", "Gelada um dia. Mata a sede.", 3, 3f, 25f),
            Beber(Item.Energetico, "Energetico", "energético", "o energético", "energéticos", "Lata de energético. Acorda.", 3, 2f, 20f, 3f),
            Beber(Item.Leite, "Leite", "caixa de leite", "a caixa de leite", "caixas de leite", "Longa vida. Mata a fome e a sede.", 2, 10f, 25f),
            Beber(Item.Suco, "Suco", "caixinha de suco", "a caixinha de suco", "caixinhas de suco", "Suco de caixinha, com canudo.", 3, 5f, 22f),
            Beber(Item.GarrafaTermica, "GarrafaTermica", "garrafa térmica", "a garrafa térmica", "garrafas térmicas", "Café, ainda morno. Esvaziada, vira sucata.", 1, 5f, 30f, 2f),
            // os remédios (o clipe da lata)
            Remedio(Item.Remedio, "Remedio", "remédio", "o remédio", "remédios", "Um vidro de xarope. Alivia a dor.", 5, 15f),
            Remedio(Item.Comprimidos, "Comprimidos", "cartela de comprimidos", "a cartela de comprimidos", "cartelas de comprimidos", "Analgésico. Um pouco de alívio.", 10, 8f),
            Remedio(Item.KitMedico, "KitMedico", "kit médico", "o kit médico", "kits médicos", "Primeiros socorros completos. Cura muito.", 1, 60f),
            Remedio(Item.Atadura, "Atadura", "atadura", "a atadura", "ataduras", "Rolo de atadura. Fecha o corte.", 5, 15f),
            Remedio(Item.Gaze, "Gaze", "gaze", "a gaze", "gazes", "Pacote de gaze. Pouco, mas ajuda.", 10, 8f),
            Remedio(Item.Desinfetante, "Desinfetante", "desinfetante", "o desinfetante", "desinfetantes", "Álcool. Limpa a ferida.", 3, 10f),
            Remedio(Item.SprayAntisseptico, "SprayAntisseptico", "spray antisséptico", "o spray antisséptico", "sprays antissépticos", "Arde, mas limpa.", 3, 12f),
            Remedio(Item.Antibiotico, "Antibiotico", "antibiótico", "o antibiótico", "antibióticos", "Caixa de antibiótico. Cura bem.", 5, 25f),
            Remedio(Item.Bandagem, "Bandagem", "bandagem", "a bandagem", "bandagens", "Pano limpo em tiras. Feita à mão.", 5, 20f),
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
            Material(Item.Isca, "Isca", "isca", "a isca", "iscas", "Para pescar (a pesca ainda não existe).", 20),
            Material(Item.VaraDePesca, "VaraDePesca", "vara de pesca", "a vara de pesca", "varas de pesca", "Com linha e anzol (a pesca ainda não existe).", 1),
            Material(Item.KitDeReparo, "KitDeReparo", "kit de reparo", "o kit de reparo", "kits de reparo", "Conserta uma arma.", 3),
            Material(Item.Roupa, "Roupa", "roupa", "a roupa", "roupas", "Roupa velha. Desmontada, vira pano.", 5),
            // os kits de montar (a fogueira, a cama e a barricada)
            Material(Item.Fogueira, "Fogueira", "fogueira", "a fogueira", "fogueiras", "Lenha arrumada. Monte no chão: luz e calor.", 1, TipoDeLoot.Construcao),
            Material(Item.Cama, "Cama", "cama improvisada", "a cama improvisada", "camas improvisadas", "Monte no chão: você volta nela quando cair.", 1, TipoDeLoot.Construcao),
            Material(Item.Barricada, "Barricada", "barricada", "a barricada", "barricadas", "Tábuas pregadas. Monte numa porta: segura os zumbis.", 1, TipoDeLoot.Construcao),
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
        public static bool EhConstrucao(Item i) => i >= Item.Fogueira && i <= Item.Barricada;
        /// <summary>Um remédio (cura a vida).</summary>
        public static bool EhRemedio(Item i) => Loot(i)?.tipo == TipoDeLoot.Remedio;

        /// <summary>Quantos cabem numa célula (a pilha): as balas numa caixa de munição, os do saque; o resto, 1.</summary>
        public static int Pilha(Item i) => EhMunicao(i) ? MaxPorCaixa : Loot(i)?.pilha ?? 1;
        /// <summary>O item vai em pilha (o qtd é quantos há nela).</summary>
        public static bool EmPilha(Item i) => Pilha(i) > 1;
        /// <summary>Quantos há na pilha (o qtd; uma pilha sem número é de 1).</summary>
        public static int Quantos(Item i, int qtd) => !EmPilha(i) ? 1 : EhMunicao(i) ? qtd : System.Math.Max(1, qtd);
        /// <summary>O nome com o número ("3 panos", "1 pano").</summary>
        public static string ComNumero(Item i, int n)
        {
            var l = Loot(i);
            return n == 1 || l == null ? $"{n} {Nome(i)}" : $"{n} {l.plural}";
        }

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
        /// <summary>Uma peça do carro (as 3 do motor, o pneu, o macaco e a chave da perua), feita pelo pecas.py.</summary>
        public static bool EhPeca(Item i) => i >= Item.Vela && i <= Item.ChaveDaPerua;
        /// <summary>Uma peça com o estado (0 a 100%) no qtd: todas menos o macaco e a chave.</summary>
        public static bool ComEstado(Item i) => EhPeca(i) && i != Item.Macaco && i != Item.ChaveDaPerua;
        /// <summary>Uma chave de veículo (hoje só a da perua; cada veículo novo terá a sua).</summary>
        public static bool EhChave(Item i) => i == Item.ChaveDaPerua;
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
        public static bool VaiNaMao(Item i) => i != Item.Nada && !EhMunicao(i) && !EhPeca(i) && !EhEquipamento(i) && !EhCaixaDeBolas(i) && NoJogo(i) && (!EhLoot(i) || Loot(i).mao >= 0);
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

        public static bool EhArma(Item i) => i >= Item.Pistola && i <= Item.Sniper;
        /// <summary>
        /// O que gasta com o uso (31/10/2026, com o kit de reparo): as armas de fogo, a cada tiro, e as brancas, a cada golpe
        /// que pega. O desgaste vai de 0 (nova) a 100: a de fogo quebrada não atira (o kit conserta); a branca quebra e some.
        /// </summary>
        public static bool ComDesgaste(Item i) => EhArma(i) || EhBranca(i);
        /// <summary>Quanto um uso gasta: um tiro (as de fogo: 400 tiros) ou um golpe que pega (as brancas).</summary>
        public static float DesgastePorUso(Item i) => EhArma(i) ? 0.25f : i == Item.Faca ? 0.8f : i == Item.Machado ? 0.6f
            : i == Item.Bastao ? 1.5f : i == Item.Lanca ? 1.2f : i == Item.Tocha ? 0f : 1f;
        /// <summary>A tocha acesa na mão queima: o desgaste sobe 1 a cada tantos segundos (100: ela se apaga e some).</summary>
        public const float SegundosDaTochaPorDesgaste = 6f;
        /// <summary>Daqui para cima, a arma de fogo emperra às vezes (até 30% dos tiros, quase quebrada).</summary>
        public const int DesgasteQueEmperra = 70;
        /// <summary>Uma arma branca (a faca e o machado): vai na mão, o clique golpeia de perto, sem munição.</summary>
        public static bool EhBranca(Item i) => i >= Item.Faca && i <= Item.Tocha;
        /// <summary>O modelo (o índice em <see cref="Modelos"/>) é de uma arma branca.</summary>
        public static bool ModeloBranco(int modelo) => modelo >= Modelo(Item.Faca) && modelo <= Modelo(Item.Tocha);
        /// <summary>Come, bebe ou usa (a comida, a bebida e os remédios).</summary>
        public static bool EhConsumivel(Item i) => Loot(i)?.verbo != null;
        /// <summary>O que se veste: as 3 mochilas (o coldre saiu do jogo).</summary>
        public static bool EhEquipamento(Item i) => EhMochila(i);
        public static bool EhMochila(Item i) => i >= Item.MochilaPequena && i <= Item.MochilaGrande;
        /// <summary>O que existe no jogo (o coldre, não: saiu em 31/10/2026).</summary>
        public static bool NoJogo(Item i) => i != Item.Nada && i != Item.Coldre;
        /// <summary>
        /// O que, num atalho e fora da mão, vai preso no cós da calça, atrás (a pistola e a faca); as outras armas vão nas
        /// costas (o EquipamentoNoCorpo).
        /// </summary>
        public static bool VaiNoCos(Item i) => i == Item.Pistola || i == Item.Faca;
        /// <summary>O índice do equipamento (0 a 2, na ordem de _equipamentos), ou -1.</summary>
        public static int Equipamento(Item i) => EhEquipamento(i) ? i - Item.MochilaPequena : -1;
        public static Item DoEquipamento(int k) => (Item)((int)Item.MochilaPequena + k);
        public static DadosDoEquipamento DadosDe(Item i) => EhEquipamento(i) ? _equipamentos[Equipamento(i)] : null;
        public static string ModeloDoEquipamento(int k) => _equipamentos[k].modelo;
        /// <summary>O espaço da grade com esta mochila (Nada: os bolsos).</summary>
        public static UnityEngine.Vector2Int EspacoDa(Item mochila) => EhMochila(mochila) ? _equipamentos[Equipamento(mochila)].espaco : Bolsos;
        public static TipoDeArma Arma(Item i) => (TipoDeArma)((int)i - 1);
        public static Item DaArma(TipoDeArma t) => (Item)((int)t + 1);
        public static DadosDoLoot Consumivel(Item i) => Loot(i);

        /// <summary>
        /// O modelo de mão do item (os braços de primeira pessoa e o item na mão do corpo): o dele, ou, nos comestíveis e
        /// nos remédios do saque, o da lata ou o da garrafa (com a malha do item no lugar). -1: não vai na mão.
        /// </summary>
        public static int MaoDe(Item i)
        {
            int m = Modelo(i);
            if (m >= 0) return m;
            var l = Loot(i);
            return l != null ? l.mao : -1;
        }
        /// <summary>O item vai na mão com a malha dele no lugar da lata ou da garrafa (os do loot.py).</summary>
        public static bool NaMaoComMalhaPropria(Item i) => Modelo(i) < 0 && MaoDe(i) >= 0 && IndiceDoLoot(i) >= 0;

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
            new(1, 1), new(1, 2),                                   // lata, garrafa
            new(2, 1), new(4, 1), new(4, 1), new(6, 1), new(3, 1),  // faca, machado, bastão, lança, tocha
        };

        /// <summary>As células que o item ocupa na mochila (largura × altura).</summary>
        public static UnityEngine.Vector2Int Tamanho(Item i)
        {
            if (EhMunicao(i)) return new UnityEngine.Vector2Int(1, 1);
            if (EhPeca(i)) return _pecas[IndiceDaPeca(i)].tamanho;
            if (IndiceDoLoot(i) >= 0) return new UnityEngine.Vector2Int(1, 1);   // o saque: uma célula (por enquanto)
            if (EhSinuca(i)) return _sinuca[IndiceDaSinuca(i)].tamanho;
            int m = Modelo(i);
            return m >= 0 ? _tamanhos[m] : UnityEngine.Vector2Int.zero;
        }

        // uma linha sobre cada modelo, para os detalhes da tela do inventário (curta: os números vêm em seguida)
        static readonly string[] _resumos =
        {
            "Semiautomática. Leve e precisa.", "Automático. Bom de longe.", "Automática. Rápida, de perto.",
            "De bomba. Arrasa de perto.", "De ferrolho, com luneta.", "Conserva. Mata a fome.", "Água. Mata a sede.",
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
            int m = Modelo(i);
            return m >= 0 ? _resumos[m] : "";
        }

        public static string Nome(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nome : EhBranca(i) ? Branca(i).nome : EhLoot(i) ? Loot(i).nome : EhEquipamento(i) ? DadosDe(i).nome
            : EhMunicao(i) ? "caixa de " + NomeDoCalibre(CalibreDa(i)) : EhPeca(i) ? _pecas[IndiceDaPeca(i)].nome : EhSinuca(i) ? _sinuca[IndiceDaSinuca(i)].nome : "";
        public static string NomeComArtigo(Item i) => EhArma(i) ? Armas.Dados(Arma(i)).nomeComArtigo : EhBranca(i) ? Branca(i).nomeComArtigo : EhLoot(i) ? Loot(i).nomeComArtigo : EhEquipamento(i) ? DadosDe(i).nomeComArtigo
            : EhMunicao(i) ? "a caixa de " + NomeDoCalibre(CalibreDa(i)) : EhPeca(i) ? _pecas[IndiceDaPeca(i)].nomeComArtigo : EhSinuca(i) ? _sinuca[IndiceDaSinuca(i)].nomeComArtigo : "";
    }
}
