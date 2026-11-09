using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>A raridade de um item achado (o Plano_Saque.md da pasta de arte, a decisão 1): muda a chance de ele aparecer.</summary>
    public enum Raridade : byte { Normal, Incomum, Raro, Epico, Lendario }

    /// <summary>
    /// O que um item é para o saque: decide em que móveis e cômodos ele aparece (o Plano_Saque.md, "As categorias"). Um item
    /// pode ter mais de uma (o salgadinho é comida e lanche: vai na cozinha e na sala).
    /// </summary>
    [System.Flags]
    public enum EspecieDoSaque
    {
        Nenhuma = 0,
        /// <summary>As roupas; as pequenas (o chapéu, os óculos, a máscara, as luvas e o cinto) também vão no criado-mudo.</summary>
        Roupa = 1 << 0, RoupaPequena = 1 << 1,
        Mochila = 1 << 2,
        Comida = 1 << 3, Bebida = 1 << 4,
        /// <summary>O que fica na sala: o salgadinho, o chocolate, a barra, o refrigerante e a água.</summary>
        Lanche = 1 << 5,
        Remedio = 1 << 6,
        /// <summary>O que fica na cabeceira: o remédio e os comprimidos.</summary>
        Cabeceira = 1 << 7,
        Pano = 1 << 8,
        /// <summary>A oficina: as ferramentas, os pregos, a fita, os fios, as peças de metal, a corda, a madeira e o kit de reparo.</summary>
        Ferramenta = 1 << 9,
        /// <summary>A mesa de escritório e a gaveta da cozinha: a fita adesiva e os fios.</summary>
        Escritorio = 1 << 10,
        Pesca = 1 << 11,
        /// <summary>A lareira: a madeira e as toras.</summary>
        Lenha = 1 << 12,
        /// <summary>A lanterna e as pilhas.</summary>
        Luz = 1 << 13,
        Galao = 1 << 14,
        Faca = 1 << 15, Machado = 1 << 16,
        ArmaCurta = 1 << 17, ArmaLonga = 1 << 18, Municao = 1 << 19,
        /// <summary>A caixa de bolas e os tacos: só no cômodo com a mesa de sinuca.</summary>
        Sinuca = 1 << 20,
        /// <summary>O que vai nas gavetas da cozinha: a faca, a fita adesiva, a lanterna, as pilhas, a atadura e os comprimidos.</summary>
        GavetaDaCozinha = 1 << 21,
        /// <summary>
        /// O que aparece nos móveis da farmácia e do hospital (05/11/2026, a etapa 2 do sistema de vida): os remédios, os
        /// curativos e o kit; o antibiótico incomum só tem esta (nas casas, ele continua raro). As casas não aceitam esta.
        /// </summary>
        Farmacia = 1 << 22,
        /// <summary>O que fica atrás das portas da cozinha, com os produtos de limpeza: o álcool (o Desinfetante).</summary>
        Limpeza = 1 << 23,
    }

    /// <summary>
    /// O catálogo do saque pelo mapa (o Plano_Saque.md da pasta de arte, a parte 3, com as 8 decisões aprovadas pela
    /// recomendação): a raridade, a espécie, a quantidade e o estado de cada item, o que cada móvel aceita em cima, atrás das
    /// portas e nas gavetas, o peso de cada cômodo e de cada móvel no sorteio e quantos itens cada moradia tem. Os itens são
    /// chamados pelo nome (<see cref="ItensDoSaque"/>). Só lê: seguro em várias threads.
    /// </summary>
    public static class CatalogoDoSaque
    {
        // ------------------------------------------------------------------ as raridades (a decisão 1)

        /// <summary>
        /// De cada 100 itens sorteados: normal 40, incomum 30, raro 15, épico 10 e lendário 5 (02/11/2026, a pedido; na
        /// decisão 1 eram 60, 25, 10, 4 e 1).
        /// </summary>
        public static readonly float[] Chances = { 0.40f, 0.30f, 0.15f, 0.10f, 0.05f };

        public static readonly string[] NomesDasRaridades = { "normal", "incomum", "raro", "épico", "lendário" };

        /// <summary>
        /// As armas de fogo reais e a munição real nas casas, nos prédios e nas lojas (09/11/2026, a 2ª leva da etapa 1b; a
        /// decisão do Pedro: "as armas reais ficam super raras", só com os militares e "raríssimas" no saque): quando o
        /// sorteio cai numa delas, só fica com esta fração da chance; no resto, vira outro item do mesmo lugar (da raridade de
        /// baixo; <see cref="SaqueDaObra"/>). Os militares e a base militar não passam por aqui.
        /// </summary>
        public static float FatorDasArmasReais = 0.1f, FatorDaMunicaoReal = 0.35f;

        /// <summary>A fração da chance do item (1: como sempre; as armas de fogo e a munição reais, menos).</summary>
        public static float FatorDe(Item c)
        {
            if ((c.especie & (EspecieDoSaque.ArmaCurta | EspecieDoSaque.ArmaLonga)) != 0) return FatorDasArmasReais;
            if ((c.especie & EspecieDoSaque.Municao) != 0 && c.nome.StartsWith("Municao")) return FatorDaMunicaoReal;
            return 1f;
        }

        /// <summary>A cor de cada raridade (o cinza, o verde, o azul, o roxo e o dourado; sRGB).</summary>
        public static readonly Color[] Cores =
        {
            new Color(0.78f, 0.78f, 0.76f), new Color(0.35f, 0.80f, 0.32f), new Color(0.25f, 0.55f, 0.95f),
            new Color(0.68f, 0.35f, 0.92f), new Color(1.00f, 0.74f, 0.15f),
        };

        // ------------------------------------------------------------------ os itens (a decisão 2)

        /// <summary>Um item do catálogo: o nome (o de <see cref="ItensDoSaque"/>), a raridade, a espécie e a pilha achada.</summary>
        public sealed class Item
        {
            public string nome;
            public Raridade raridade;
            public EspecieDoSaque especie;
            /// <summary>Quantos numa pilha achada (as balas, numa caixa de munição), entre os dois.</summary>
            public int min = 1, max = 1;
            /// <summary>O número do item e o tamanho dele no móvel (de <see cref="ItensDoSaque"/>).</summary>
            public byte numero;
            public Vector3 tamanho;
        }

        const Raridade N = Raridade.Normal, I = Raridade.Incomum, R = Raridade.Raro, E = Raridade.Epico, L = Raridade.Lendario;

        static Item C(string nome, Raridade r, EspecieDoSaque e, int min = 1, int max = 1) => new Item { nome = nome, raridade = r, especie = e, min = min, max = max };

        // o que não está aqui não aparece nas casas (os kits de montar, o bastão, a lança e a tocha da fabricação, a "roupa" de
        // desfiar, o copo de água, a caixa de bolas vazia, o coldre, as peças do carro e a chave da perua)
        static readonly Item[] Lista =
        {
            // a comida
            C("Comida", N, EspecieDoSaque.Comida), C("Feijao", N, EspecieDoSaque.Comida), C("Milho", N, EspecieDoSaque.Comida), C("Sopa", N, EspecieDoSaque.Comida),
            C("Salgadinho", N, EspecieDoSaque.Comida | EspecieDoSaque.Lanche), C("Carne", I, EspecieDoSaque.Comida),
            C("Chocolate", I, EspecieDoSaque.Comida | EspecieDoSaque.Lanche),
            // (incomum, e não rara: rara, ela era o único raro da cozinha e da sala, e o raro mais achado do mapa)
            C("BarraDeEnergia", I, EspecieDoSaque.Comida | EspecieDoSaque.Lanche),
            // a bebida
            C("Agua", N, EspecieDoSaque.Bebida | EspecieDoSaque.Lanche), C("RefrigeranteGarrafa", N, EspecieDoSaque.Bebida | EspecieDoSaque.Lanche),
            C("RefrigeranteLata", N, EspecieDoSaque.Bebida | EspecieDoSaque.Lanche), C("Suco", N, EspecieDoSaque.Bebida),
            C("Leite", I, EspecieDoSaque.Bebida), C("Energetico", I, EspecieDoSaque.Bebida | EspecieDoSaque.Lanche), C("GarrafaTermica", I, EspecieDoSaque.Bebida),
            // os remédios (05/11/2026, sem a vida voltando sozinha: a atadura, a bandagem e a gaze vêm de 1 a 2, os
            // comprimidos de 1 a 3; a pilha achada não muda o lugar de nada no mundo salvo)
            // (05/11/2026, a etapa 2: todos com a espécie Farmacia, a da farmácia e do hospital, e o Desinfetante, o "álcool",
            // normal, e não mais incomum, também atrás das portas da cozinha, com a limpeza)
            C("Comprimidos", N, EspecieDoSaque.Remedio | EspecieDoSaque.Cabeceira | EspecieDoSaque.GavetaDaCozinha | EspecieDoSaque.Farmacia, 1, 3),
            C("Atadura", N, EspecieDoSaque.Remedio | EspecieDoSaque.GavetaDaCozinha | EspecieDoSaque.Farmacia, 1, 2),
            C("Gaze", N, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia, 1, 2), C("Remedio", I, EspecieDoSaque.Remedio | EspecieDoSaque.Cabeceira | EspecieDoSaque.Farmacia),
            C("Desinfetante", N, EspecieDoSaque.Remedio | EspecieDoSaque.Limpeza | EspecieDoSaque.Farmacia),
            C("SprayAntisseptico", I, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia), C("Bandagem", I, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia, 1, 2),
            C("KitMedico", R, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia),
            // o antibiótico: raro nas casas (só a espécie Remedio: a farmácia e o hospital não aceitam esta entrada) e
            // incomum na farmácia e no hospital (a segunda entrada, só Farmacia). A raridade da mochila é a da primeira
            // (RaridadeDo): a rara
            C("Antibiotico", R, EspecieDoSaque.Remedio),
            C("Antibiotico", I, EspecieDoSaque.Farmacia),
            // a tala (05/11/2026): no banheiro e no acampamento. Só entra no saque depois do modelo no Loot.fbx e do "Itens >
            // Escrever os itens do saque" (antes disso, fica no Faltando)
            C("Tala", I, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia),
            // a bandagem antisséptica (05/11/2026, a etapa 2): no banheiro, no acampamento, na farmácia e no hospital. Como a
            // tala, só entra no saque depois do modelo no Loot.fbx e do "Itens > Escrever os itens do saque"
            C("BandagemAntisseptica", I, EspecieDoSaque.Remedio | EspecieDoSaque.Farmacia, 1, 2),
            // o calmante (06/11/2026, a mente): raro nas casas (o banheiro, o acampamento) e incomum na farmácia e no hospital,
            // como o antibiótico. Só entra no saque depois do modelo no Loot.fbx e do "Itens > Escrever os itens do saque"
            C("Calmante", R, EspecieDoSaque.Remedio, 1, 2),
            C("Calmante", I, EspecieDoSaque.Farmacia, 1, 3),
            // os materiais
            C("Pano", N, EspecieDoSaque.Pano, 1, 3), C("Madeira", N, EspecieDoSaque.Ferramenta | EspecieDoSaque.Lenha, 1, 3), C("Pregos", N, EspecieDoSaque.Ferramenta, 3, 12),
            C("FitaAdesiva", N, EspecieDoSaque.Ferramenta | EspecieDoSaque.Escritorio | EspecieDoSaque.GavetaDaCozinha), C("Fios", N, EspecieDoSaque.Ferramenta | EspecieDoSaque.Escritorio),
            C("Isca", N, EspecieDoSaque.Pesca, 2, 6), C("Corda", I, EspecieDoSaque.Ferramenta), C("Toras", I, EspecieDoSaque.Lenha, 1, 2),
            C("PecasDeMetal", I, EspecieDoSaque.Ferramenta, 1, 3), C("Ferramenta", I, EspecieDoSaque.Ferramenta), C("VaraDePesca", I, EspecieDoSaque.Pesca),
            C("KitDeReparo", R, EspecieDoSaque.Ferramenta),
            // a pólvora (06/11/2026, o Armeiro): incomum, com a munição e na oficina (só entra depois do modelo no Loot.fbx e
            // do "Itens > Escrever os itens do saque")
            C("Polvora", I, EspecieDoSaque.Municao | EspecieDoSaque.Ferramenta, 1, 2),
            // arrombar (02/11/2026, o Plano_Portas.md da pasta de arte): a gazua nas gavetas (do quarto, da cozinha, do
            // escritório e da oficina), o pé de cabra na oficina e no porão
            C("Gazua", I, EspecieDoSaque.Ferramenta | EspecieDoSaque.Escritorio | EspecieDoSaque.GavetaDaCozinha | EspecieDoSaque.Cabeceira, 1, 3),
            C("PeDeCabra", I, EspecieDoSaque.Ferramenta),
            // a sucata (09/11/2026, a 2ª leva da etapa 1b): às vezes, com as ferramentas (a bancada, as prateleiras e as estantes
            // do porão, da garagem e do depósito), de 1 a 3. A outra fonte são os painéis de metal da rua e o desmontar
            C("Sucata", I, EspecieDoSaque.Ferramenta, 1, 3),
            // o fumo (03/11/2026, o Plano_Cigarro.md da pasta de arte): o maço, normal, na cabeceira, na sala (a mesa de
            // centro e o rack), nas gavetas da cozinha e do escritório e na oficina, com 2 a 20 cigarros
            C("MacoDeCigarros", N, EspecieDoSaque.Cabeceira | EspecieDoSaque.Lanche | EspecieDoSaque.GavetaDaCozinha | EspecieDoSaque.Escritorio
                                   | EspecieDoSaque.Ferramenta, 2, 20),
            // a luz e o galão
            C("Pilhas", N, EspecieDoSaque.Luz | EspecieDoSaque.GavetaDaCozinha, 1, 4), C("Lanterna", I, EspecieDoSaque.Luz | EspecieDoSaque.GavetaDaCozinha), C("Galao", I, EspecieDoSaque.Galao),
            // as armas brancas e as de fogo
            C("Faca", I, EspecieDoSaque.Faca | EspecieDoSaque.GavetaDaCozinha), C("Machado", R, EspecieDoSaque.Machado),
            C("Pistola", R, EspecieDoSaque.ArmaCurta), C("Espingarda", E, EspecieDoSaque.ArmaLonga), C("Submetralhadora", E, EspecieDoSaque.ArmaLonga),
            C("Rifle", L, EspecieDoSaque.ArmaLonga), C("Sniper", L, EspecieDoSaque.ArmaLonga),
            // a munição (as balas da caixa: as da tabela do jogo, DadosDaMunicao.achadaMin e achadaMax)
            C("Municao9mm", I, EspecieDoSaque.Municao, 12, 40), C("Municao12", R, EspecieDoSaque.Municao, 6, 18),
            C("Municao556", R, EspecieDoSaque.Municao, 10, 30), C("Municao762", E, EspecieDoSaque.Municao, 4, 12),
            // as mochilas
            C("MochilaPequena", I, EspecieDoSaque.Mochila), C("MochilaMedia", R, EspecieDoSaque.Mochila), C("MochilaGrande", E, EspecieDoSaque.Mochila),
            // a sinuca
            C("CaixaDeBolas", I, EspecieDoSaque.Sinuca), C("TacoMadeira", I, EspecieDoSaque.Sinuca), C("TacoEscuro", I, EspecieDoSaque.Sinuca),
            C("TacoPreto", I, EspecieDoSaque.Sinuca), C("TacoVermelho", I, EspecieDoSaque.Sinuca),
        };

        /// <summary>As roupas, por raridade (as peças do catálogo das roupas; as que não estão aqui são normais).</summary>
        static readonly Dictionary<string, Raridade> RaridadeDasRoupas = Roupas(
            (I, new[] { "Jaqueta_Capuz", "Jaqueta_Lona", "Jaqueta_Esportiva", "Calca_Cargo", "Calca_Trabalho", "Jardineira", "Jeans_Suspensorio",
                        "Bota_Trabalho", "Bota_Couro", "Bota_Cauboi", "Chapeu_Cauboi", "Capacete", "Colete_Refletivo", "Cinto_Ferramentas",
                        "Luva_Sem_Dedos", "Oculos_Sol", "Oculos_Aviador", "Oculos_Sol_Redondo", "Oculos_Esportivo", "Bandana_Rosto",
                        "Gola_Camuflada", "Gola_Caveira", "Bone_Militar" }),
            (R, new[] { "Jaqueta_Couro", "Camisa_Uniforme", "Camisa_Uniforme_Longa", "Calca_Uniforme", "Bone_Policia", "Quepe", "Cinto_Policia",
                        "Jaleco", "Camisa_Militar", "Calca_Militar", "Luva_Tatica", "Oculos_Protecao", "Respirador", "Viseira", "Tapa_Olho" }),
            (E, new[] { "Colete_Tatico", "Cinto_Tatico", "Coturno", "Capacete_Militar", "Oculos_Tatico", "Mascara_Gas", "Balaclava" }),
            (L, new[] { "Visao_Noturna" }));

        static Dictionary<string, Raridade> Roupas(params (Raridade r, string[] pecas)[] grupos)
        {
            var d = new Dictionary<string, Raridade>();
            foreach (var (r, pecas) in grupos) foreach (var p in pecas) d[p] = r;
            return d;
        }

        /// <summary>Os lugares das roupas pequenas no corpo (vão também no criado-mudo e no tampo da cômoda).</summary>
        static readonly HashSet<string> LugaresPequenos = new HashSet<string> { "Cabeca", "Oculos", "Mascara", "Maos", "Cintura" };

        static List<Item> _itens;
        static List<Item>[] _porRaridade;
        static List<string> _faltando;

        /// <summary>Todos os itens do catálogo que existem no jogo (com o número e o tamanho), as roupas com eles.</summary>
        public static IReadOnlyList<Item> Itens { get { Preparar(); return _itens; } }

        /// <summary>Os de uma raridade.</summary>
        public static IReadOnlyList<Item> DaRaridade(Raridade r) { Preparar(); return _porRaridade[(int)r]; }

        /// <summary>Os nomes do catálogo que não estão na tabela dos itens (o teste confere que não há nenhum).</summary>
        public static IReadOnlyList<string> Faltando { get { Preparar(); return _faltando; } }

        static Dictionary<int, Raridade> _raridadePorNumero;

        /// <summary>A raridade do item pelo número dele (o do enum Item); false se ele não aparece no saque (os kits, as peças do carro).</summary>
        public static bool RaridadeDo(int numero, out Raridade r)
        {
            Preparar();
            if (_raridadePorNumero == null)
            {
                var d = new Dictionary<int, Raridade>();
                // (com duas entradas, vale a primeira: o antibiótico fica raro na mochila, mesmo o achado na farmácia)
                foreach (var it in _itens) if (!d.ContainsKey(it.numero)) d[it.numero] = it.raridade;
                _raridadePorNumero = d;
            }
            return _raridadePorNumero.TryGetValue(numero, out r);
        }

        /// <summary>A cor da raridade para o texto rico do OnGUI ("#RRGGBB").</summary>
        public static string CorEmHex(Raridade r) => "#" + ColorUtility.ToHtmlStringRGB(Cores[(int)r]);

        static readonly object _trava = new object();

        static void Preparar()
        {
            if (_itens != null) return;
            lock (_trava)
            {
                if (_itens != null) return;
                var itens = new List<Item>();
                var faltando = new List<string>();
                foreach (var c in Lista)
                {
                    if (!ItensDoSaque.Tem(c.nome)) { faltando.Add(c.nome); continue; }
                    var d = ItensDoSaque.Do(c.nome);
                    c.numero = d.item;
                    c.tamanho = d.tamanho;
                    itens.Add(c);
                }
                foreach (var d in ItensDoSaque.Todos)
                {
                    if (!d.nome.StartsWith("Roupa.")) continue;
                    string peca = d.nome.Substring(6);
                    itens.Add(new Item
                    {
                        nome = d.nome, numero = d.item, tamanho = d.tamanho,
                        raridade = RaridadeDasRoupas.TryGetValue(peca, out var r) ? r : N,
                        especie = EspecieDoSaque.Roupa | (LugaresPequenos.Contains(d.lugar) ? EspecieDoSaque.RoupaPequena : EspecieDoSaque.Nenhuma),
                    });
                }
                var por = new List<Item>[5];
                for (int k = 0; k < 5; k++) por[k] = new List<Item>();
                foreach (var it in itens) por[(int)it.raridade].Add(it);
                _faltando = faltando;
                _porRaridade = por;
                _itens = itens;
            }
        }

        // ------------------------------------------------------------------ os móveis (o que cada um aceita)

        /// <summary>
        /// O que um móvel aceita em cima (à vista), atrás das portas e das tampas e nas gavetas, e o peso dele no sorteio do
        /// cômodo (o guarda-roupa e o acampamento valem mais que um módulo da cozinha: a cozinha de 8 módulos não engole o saque).
        /// </summary>
        public sealed class Regra
        {
            public EspecieDoSaque cima, dentro, gavetas;
            public float peso = 1f;
        }

        static Regra G(EspecieDoSaque cima, EspecieDoSaque dentro, EspecieDoSaque gavetas, float peso) => new Regra { cima = cima, dentro = dentro, gavetas = gavetas, peso = peso };

        const EspecieDoSaque Armas = EspecieDoSaque.ArmaCurta | EspecieDoSaque.ArmaLonga | EspecieDoSaque.Municao;

        static readonly Dictionary<string, Regra> Regras = Montar(
            // o quarto: as roupas em cima da cama, no guarda-roupa, na cômoda e no baú; a arma longa largada em cima da cama
            // (com 1,2 m, a espingarda e a sniper só cabem nela e nas estantes grandes do porão)
            (G(EspecieDoSaque.Roupa | EspecieDoSaque.ArmaLonga, 0, 0, 2f), new[] { "CamaCasal", "CamaSolteiro", "Beliche", "ColchaoNoChao" }),
            (G(0, EspecieDoSaque.Roupa | EspecieDoSaque.Mochila | EspecieDoSaque.Pano | Armas, 0, 3f), new[] { "GuardaRoupa2", "GuardaRoupa3", "GuardaRoupa4" }),
            (G(EspecieDoSaque.RoupaPequena | EspecieDoSaque.Cabeceira, 0, EspecieDoSaque.Roupa, 2f), new[] { "Comoda3", "Comoda6", "Penteadeira" }),
            (G(EspecieDoSaque.RoupaPequena | EspecieDoSaque.Cabeceira | EspecieDoSaque.Luz, 0, EspecieDoSaque.RoupaPequena | EspecieDoSaque.Cabeceira | EspecieDoSaque.Luz | EspecieDoSaque.ArmaCurta | EspecieDoSaque.Municao, 2f),
             new[] { "CriadoMudo" }),
            (G(0, EspecieDoSaque.Roupa | EspecieDoSaque.Pano | EspecieDoSaque.Mochila | Armas, 0, 2f), new[] { "Bau" }),
            (G(EspecieDoSaque.Escritorio | EspecieDoSaque.Luz, 0, EspecieDoSaque.Escritorio | EspecieDoSaque.Luz | EspecieDoSaque.ArmaCurta | EspecieDoSaque.Municao, 1f), new[] { "MesaEscritorio" }),
            // a sala: o lanche e a luz; a lenha na lareira
            (G(EspecieDoSaque.Lanche | EspecieDoSaque.Luz, EspecieDoSaque.Lanche | EspecieDoSaque.Luz, 0, 1f), new[] { "RackSimples", "RackArmario", "RackEstante", "EstanteLivros", "EstanteBaixa" }),
            (G(EspecieDoSaque.Lanche, 0, 0, 1f), new[] { "MesaDeCentro" }),
            (G(EspecieDoSaque.Lanche | EspecieDoSaque.Luz, 0, EspecieDoSaque.Luz | EspecieDoSaque.Cabeceira | EspecieDoSaque.ArmaCurta | EspecieDoSaque.Municao, 1f), new[] { "MesaDeCanto" }),
            (G(EspecieDoSaque.Lenha | EspecieDoSaque.Machado, 0, 0, 1f), new[] { "Lareira" }),
            // a cozinha: a comida e a bebida; atrás das portas, também o álcool (a limpeza: o frasco tem 17 cm, e atrás das
            // portas há de 27 a 32; nas gavetas, de 14, ele não cabe); nas gavetas, o que fica numa gaveta da cozinha
            (G(EspecieDoSaque.Comida | EspecieDoSaque.Bebida, EspecieDoSaque.Comida | EspecieDoSaque.Bebida | EspecieDoSaque.Limpeza, EspecieDoSaque.GavetaDaCozinha, 1f),
             new[] { "ArmarioChao40", "ArmarioChao60", "ArmarioChao80", "Gaveteiro40", "ArmarioCanto", "Ilha", "Pia",
                     "ArmarioParede40", "ArmarioParede60", "ArmarioParede80", "ArmarioParedeAberto60", "ArmarioParedeAberto80" }),
            (G(0, EspecieDoSaque.Bebida, 0, 2f), new[] { "Geladeira", "GeladeiraGrande" }),
            (G(0, EspecieDoSaque.Comida, EspecieDoSaque.GavetaDaCozinha, 1f), new[] { "Fogao" }),
            (G(EspecieDoSaque.Comida | EspecieDoSaque.Bebida | EspecieDoSaque.Lanche, 0, 0, 1f), new[] { "Mesa4", "Mesa6", "MesaRedonda" }),
            // o banheiro: os remédios
            (G(0, EspecieDoSaque.Remedio | EspecieDoSaque.Pano, EspecieDoSaque.Remedio, 2f), new[] { "PiaGabinete", "PiaDupla" }),
            (G(0, EspecieDoSaque.Remedio, 0, 3f), new[] { "EspelhoArmario" }),
            // o porão e o depósito: os materiais, a luz, o galão, as armas, as conservas e a água
            (G(EspecieDoSaque.Ferramenta | EspecieDoSaque.Pesca | EspecieDoSaque.Luz | EspecieDoSaque.Machado | EspecieDoSaque.Municao, 0, 0, 2f), new[] { "Bancada" }),
            (G(EspecieDoSaque.Ferramenta | EspecieDoSaque.Pesca | EspecieDoSaque.Luz | EspecieDoSaque.Galao | EspecieDoSaque.Comida | EspecieDoSaque.Bebida | EspecieDoSaque.Mochila | EspecieDoSaque.Machado | Armas | EspecieDoSaque.Sinuca, 0, 0, 2f),
             new[] { "PrateleiraMadeira90", "PrateleiraMadeira150", "EstanteAco90", "EstanteAco120" }),
            (G(0, EspecieDoSaque.Comida | EspecieDoSaque.Bebida, 0, 1f), new[] { "FreezerHorizontal" }),
            (G(0, 0, EspecieDoSaque.Escritorio, 1f), new[] { "Arquivo" }),
            // o acampamento: o saque de quem se escondeu ali
            (G(EspecieDoSaque.Comida | EspecieDoSaque.Bebida | EspecieDoSaque.Remedio | EspecieDoSaque.Faca | EspecieDoSaque.Luz | EspecieDoSaque.Municao | EspecieDoSaque.Mochila, 0, 0, 3f),
             new[] { "Colchonete", "SacoDeDormir", "Latas", "Fogareiro", "Garrafas" }));

        static Dictionary<string, Regra> Montar(params (Regra r, string[] modelos)[] grupos)
        {
            var d = new Dictionary<string, Regra>();
            foreach (var (r, modelos) in grupos) foreach (var m in modelos) d[m] = r;
            return d;
        }

        /// <summary>A regra do modelo (null: ele não guarda saque).</summary>
        public static Regra DoModelo(string modelo) => modelo != null && Regras.TryGetValue(modelo, out var r) ? r : null;

        public static IEnumerable<string> ModelosComRegra => Regras.Keys;

        // a farmácia e o hospital (05/11/2026): os remédios, os curativos e o kit em cima dos móveis antigos; na rouparia,
        // também o pano
        static readonly Regra RegraDaFarmacia = G(EspecieDoSaque.Farmacia, EspecieDoSaque.Farmacia, EspecieDoSaque.Farmacia, 1f);
        static readonly Regra RegraDaRouparia = G(EspecieDoSaque.Farmacia | EspecieDoSaque.Pano, EspecieDoSaque.Farmacia | EspecieDoSaque.Pano, EspecieDoSaque.Farmacia | EspecieDoSaque.Pano, 1f);

        /// <summary>
        /// A regra dos móveis antigos de uma unidade de loja (<see cref="PontoDeMovel.Farmacia"/>, <see cref="PontoDeMovel.Hospital"/>;
        /// 05/11/2026): pela unidade e pelo cômodo, não pelo modelo (o ModelosComRegra fica só com os da mobília nova). Aceita
        /// só a espécie Farmacia (o antibiótico raro das casas fica de fora: aqui ele é incomum), e o pano na rouparia. Null
        /// fora de uma unidade de loja.
        /// </summary>
        public static Regra RegraDaLoja(int unidade, string comodo) =>
            unidade < PontoDeMovel.Farmacia ? null : comodo == "rouparia" ? RegraDaRouparia : RegraDaFarmacia;

        /// <summary>O que o lugar do móvel aceita: em cima, atrás de uma porta ou tampa, ou numa gaveta.</summary>
        public static EspecieDoSaque Aceita(Regra r, LugarDoSaque l) =>
            l.parte == null ? r.cima : l.parte.StartsWith("Gaveta") ? (r.gavetas != 0 ? r.gavetas : r.dentro) : r.dentro;

        // ------------------------------------------------------------------ os cômodos e as moradias (a decisão 3)

        /// <summary>O peso de cada cômodo no sorteio (o quarto e a cozinha mais; a sala menos).</summary>
        public static float PesoDoComodo(string comodo) => comodo switch
        {
            "quarto" => 3f, "cozinha" => 3f, "porao" => 2f, "deposito" => 2f, "banheiro" => 2f, "sala" => 1f,
            // a farmácia (o salão e o estoque) e o hospital (05/11/2026)
            "salao" => 3f, "estoque" => 2f, "posto" => 3f, "emergencia" => 3f, "enfermaria" => 2f,
            _ => 1f,
        };

        /// <summary>
        /// O peso do cômodo na unidade: na farmácia e no hospital, o banheiro vale 1 (o remédio fica no salão, no estoque e
        /// nos armários, não na pia); nas moradias, o de <see cref="PesoDoComodo(string)"/>.
        /// </summary>
        public static float PesoDoComodo(string comodo, int unidade) =>
            unidade >= PontoDeMovel.Farmacia && comodo == "banheiro" ? 1f : PesoDoComodo(comodo);

        /// <summary>Num cômodo, no máximo tantos itens (se a moradia tiver outros cômodos com lugar).</summary>
        public const int PorComodo = 6;

        /// <summary>
        /// Quantos itens uma moradia tem (02/11/2026, a pedido: três vezes os da decisão 3, "achei poucos itens"): a casa
        /// térrea de 3 a 9, o sobrado de 6 a 12, o apartamento de 3 a 6; com porão, mais 0 a 3, no porão
        /// (<see cref="TentativasNoPorao"/> vezes a <see cref="ChanceDoPorao"/>). Sem lugar para todos, ficam os que couberam.
        /// </summary>
        public static (int min, int max) Quantos(bool apartamento, bool sobrado) => apartamento ? (3, 6) : sobrado ? (6, 12) : (3, 9);

        public const float ChanceDoPorao = 0.5f;
        public const int TentativasNoPorao = 3;

        // ------------------------------------------------------------------ a farmácia e o hospital (05/11/2026)

        /// <summary>
        /// Quantos itens uma unidade de loja tem: a farmácia de 8 a 14 (mais ou menos o dobro de uma casa), cada andar do
        /// hospital de 5 a 10 (cada andar é uma obra, com o seu sorteio). Sem porão.
        /// </summary>
        public static (int min, int max) QuantosNaLoja(int unidade) => unidade == PontoDeMovel.Hospital ? (5, 10) : (8, 14);

        /// <summary>
        /// As raridades na farmácia (normal 50, incomum 38, raro 12) e no hospital (45, 40 e 15). Sem épico nem lendário:
        /// com as chances das casas, eles desceriam para o raro, e uns 30% dos itens seriam kits médicos.
        /// </summary>
        public static readonly float[] ChancesDaFarmacia = { 0.50f, 0.38f, 0.12f, 0f, 0f };
        public static readonly float[] ChancesDoHospital = { 0.45f, 0.40f, 0.15f, 0f, 0f };

        /// <summary>As chances da unidade: as da farmácia, as do hospital, ou as de todo o mapa (<see cref="Chances"/>).</summary>
        public static float[] ChancesDa(int unidade) =>
            unidade == PontoDeMovel.Farmacia ? ChancesDaFarmacia : unidade == PontoDeMovel.Hospital ? ChancesDoHospital : Chances;

        // ------------------------------------------------------------------ o estado do que se acha (a decisão 6)

        /// <summary>O desgaste das armas de fogo achadas (de 0 a 100): as épicas e as lendárias, menos.</summary>
        public static (int min, int max) DesgasteDaArma(Raridade r) => r >= Raridade.Epico ? (0, 30) : (20, 70);

        /// <summary>O desgaste das armas brancas achadas.</summary>
        public static readonly (int min, int max) DesgasteDaBranca = (10, 60);

        /// <summary>O pente das armas achadas: vazio até a metade (a fração da capacidade).</summary>
        public const float PenteMaximo = 0.5f;
    }
}
