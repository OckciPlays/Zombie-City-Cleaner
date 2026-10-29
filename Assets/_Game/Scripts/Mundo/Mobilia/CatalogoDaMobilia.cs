using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O catálogo da mobília nova (o Plano_Mobilia.md da pasta de arte, 29/10/2026): os modelos do Mobilia_Nova.fbx (o
    /// mobilia_nova.py e os lotes de cada cômodo, mobilia_sala.py...), o nome, o cômodo, onde cada um fica no mostruário e
    /// as variações de cor e de material.
    ///
    /// As variações não são modelos: cada vértice do modelo tem, no UV1, o índice da textura (x) e a "troca" (y): 1 na
    /// parte principal (o estofado, o corpo de madeira), 2 na secundária (os pés, a moldura, a cúpula), 0 no resto.
    /// <see cref="Pintar"/> faz a malha de uma variação trocando a textura e a cor (linear) das partes dela. Os tamanhos
    /// (o sofá de 1, 2 e 3 lugares...) são modelos.
    /// </summary>
    public static class CatalogoDaMobilia
    {
        /// <summary>As texturas, na ordem da Texture2DArray (o TEXTURAS do mobilia_texturas.py: ao mudar lá, mude aqui), com
        /// quantas vezes cada uma se repete por metro.</summary>
        public static readonly (string nome, float escala)[] Texturas =
        {
            ("MadeiraClara", 1.2f), ("MadeiraEscura", 1.2f), ("MadeiraPintada", 1.0f), ("Compensado", 1.4f), ("Tecido", 3.0f),
            ("MetalEscovado", 2.0f), ("Esmalte", 1.0f), ("Plastico", 1.0f), ("Borracha", 2.0f), ("Couro", 2.0f),
            ("Cotele", 3.0f), ("Xadrez", 1.5f), ("Colchao", 1.0f), ("Colcha", 1.0f), ("Louca", 1.0f), ("Lombada", 1.0f),
            ("Quadros", 1.0f), ("Tapetes", 1.0f), ("Tijolo", 1.6667f), ("Ferro", 1.5f), ("Terra", 2.0f), ("Granito", 1.5f),
        };

        /// <summary>As que a face pega inteiras (o ajustar do kit): o UV delas não está em metros.</summary>
        static readonly HashSet<string> Ajustadas = new HashSet<string> { "Lombada", "Quadros", "Tapetes" };

        public static int Textura(string nome)
        {
            for (int i = 0; i < Texturas.Length; i++)
                if (Texturas[i].nome == nome) return i;
            throw new System.ArgumentException($"A textura {nome} não está na lista");
        }

        public enum Grupo { Sala, Quarto, Cozinha, Banheiro, Servico }

        public static string NomeDoGrupo(Grupo g) => g switch
        {
            Grupo.Sala => "a sala", Grupo.Quarto => "o quarto", Grupo.Cozinha => "a cozinha e a sala de jantar",
            Grupo.Banheiro => "o banheiro", _ => "o escritório, a garagem e o porão",
        };

        /// <summary>
        /// Onde o móvel fica no mostruário: encostado na parede do fundo, solto no meio, pendurado na parede (a origem a
        /// <see cref="Tipo.altura"/> do chão), em cima de outro (<see cref="Tipo.sobre"/>) ou só em volta da mesa (as
        /// cadeiras).
        /// </summary>
        public enum Lugar { Parede, Meio, Alto, Sobre, ComAMesa }

        /// <summary>A textura e a cor (sRGB, como se escolhe; a malha guarda linear) de uma parte.</summary>
        public sealed class Acabamento
        {
            public readonly int textura;
            public readonly Color cor;
            public Acabamento(int textura, Color cor) { this.textura = textura; this.cor = cor; }
        }

        public sealed class Variacao
        {
            public readonly string nome;
            /// <summary>Null: a parte fica como o modelo saiu do Blender.</summary>
            public readonly Acabamento principal, secundaria;
            public Variacao(string nome, Acabamento principal = null, Acabamento secundaria = null)
            {
                this.nome = nome; this.principal = principal; this.secundaria = secundaria;
            }
        }

        public sealed class Tipo
        {
            public string modelo, nome;
            public Grupo grupo;
            public Lugar lugar;
            /// <summary>Alto: a altura da origem do chão, no tamanho real.</summary>
            public float altura;
            /// <summary>Sobre: os modelos em que ele vai em cima, cada um com a altura do tampo (no tamanho real).</summary>
            public (string modelo, float altura)[] sobre;
            /// <summary>As mesas: a cadeira e quantas vão em volta (na redonda, por igual em volta).</summary>
            public string cadeira;
            public int cadeiras;
            public bool redonda;
            /// <summary>Encostado nos vizinhos que também são "juntos" (os módulos da cozinha formam a bancada).</summary>
            public bool junto;
            /// <summary>
            /// A mesa encostada na parede com a cadeira (a de escritório): o meio do vão da cadeira em x, no modelo (no
            /// tamanho real). A cadeira e o que vai em cima da mesa ficam na frente dele.
            /// </summary>
            public float vao;
            /// <summary>A primeira é o modelo como saiu do Blender.</summary>
            public Variacao[] variacoes;
        }

        // ------------------------------------------------------------------ as cores

        static Acabamento A(string textura, float r = 1f, float g = 1f, float b = 1f) => new Acabamento(Textura(textura), new Color(r, g, b));
        static Variacao V(string nome, Acabamento principal = null, Acabamento secundaria = null) => new Variacao(nome, principal, secundaria);

        /// <summary>A lista com a do Blender primeiro (com o nome dela) e as outras, sem a de mesmo nome.</summary>
        static Variacao[] Comecando(string original, params Variacao[] outras) =>
            new[] { V(original) }.Concat(outras.Where(v => v.nome != original)).ToArray();

        static readonly Variacao[] Madeiras =
        {
            V("madeira escura", A("MadeiraEscura")), V("madeira clara", A("MadeiraClara")),
            V("pintado de branco", A("MadeiraPintada", 0.94f, 0.93f, 0.90f)), V("pintado de verde-água", A("MadeiraPintada", 0.62f, 0.78f, 0.74f)),
            V("pintado de azul", A("MadeiraPintada", 0.50f, 0.60f, 0.74f)),
        };

        static readonly Variacao[] Estofados =
        {
            V("tecido vinho", A("Tecido", 0.55f, 0.24f, 0.24f)), V("tecido cinza", A("Tecido", 0.56f, 0.56f, 0.55f)),
            V("tecido azul", A("Tecido", 0.36f, 0.44f, 0.56f)), V("cotelê mostarda", A("Cotele", 0.74f, 0.58f, 0.30f)),
            V("cotelê marrom", A("Cotele", 0.48f, 0.34f, 0.24f)), V("couro marrom", A("Couro", 0.46f, 0.28f, 0.17f)),
            V("couro preto, pés de metal", A("Couro", 0.17f, 0.16f, 0.16f), A("MetalEscovado")),
            V("xadrez, pés claros", A("Xadrez"), A("MadeiraClara")),
        };

        // as camas: a madeira (a principal) e a roupa de cama (a secundária) juntas
        static readonly Variacao[] Camas =
        {
            V("madeira clara, colcha azul", A("MadeiraClara"), A("Tecido", 0.40f, 0.50f, 0.66f)),
            V("pintada de branco, colcha rosa", A("MadeiraPintada", 0.94f, 0.93f, 0.90f), A("Tecido", 0.82f, 0.62f, 0.64f)),
            V("madeira escura, cobertor xadrez", A("MadeiraEscura"), A("Xadrez")),
            V("madeira escura, colcha verde de cotelê", A("MadeiraEscura"), A("Cotele", 0.52f, 0.60f, 0.46f)),
            V("ferro preto, cobertor cinza", A("Ferro", 0.25f, 0.25f, 0.25f), A("Tecido", 0.62f, 0.62f, 0.62f)),
        };

        // as mesas: as madeiras e o ferro (as do porão e do bunker)
        static readonly Variacao[] Mesas = Madeiras.Concat(new[]
        {
            V("ferro pintado de verde", A("Ferro", 0.45f, 0.55f, 0.45f)), V("ferro preto", A("Ferro", 0.25f, 0.25f, 0.25f)),
        }).ToArray();

        // as cadeiras estofadas: a madeira (a principal) e o pano (a secundária)
        static readonly Variacao[] Estofadas =
        {
            V("madeira escura, tecido vinho", A("MadeiraEscura"), A("Tecido", 0.55f, 0.24f, 0.24f)),
            V("madeira clara, tecido verde", A("MadeiraClara"), A("Tecido", 0.50f, 0.58f, 0.46f)),
            V("madeira escura, couro marrom", A("MadeiraEscura"), A("Couro", 0.46f, 0.28f, 0.17f)),
            V("branca, tecido cinza", A("MadeiraPintada", 0.94f, 0.93f, 0.90f), A("Tecido", 0.62f, 0.62f, 0.62f)),
            V("madeira clara, xadrez", A("MadeiraClara"), A("Xadrez")),
        };

        // os módulos da cozinha: o corpo (a principal) e o tampo (a secundária)
        static readonly Variacao[] Cozinhas =
        {
            V("madeira escura, granito bege", A("MadeiraEscura"), A("Granito", 0.85f, 0.78f, 0.65f)),
            V("branca, fórmica branca", A("MadeiraPintada", 0.94f, 0.93f, 0.90f), A("Plastico", 0.95f, 0.95f, 0.93f)),
            V("verde-água, fórmica amarela", A("MadeiraPintada", 0.62f, 0.78f, 0.74f), A("Plastico", 0.92f, 0.82f, 0.45f)),
            V("azul, tampo de madeira", A("MadeiraPintada", 0.50f, 0.60f, 0.74f), A("MadeiraClara")),
            V("amarelo-mostarda, granito preto", A("MadeiraPintada", 0.84f, 0.68f, 0.32f), A("Granito", 0.25f, 0.25f, 0.25f)),
        };

        // a louça dos banheiros antigos (a principal da privada, da banheira, do box e da pia de coluna)
        static readonly Variacao[] Loucas =
        {
            V("rosa", A("Louca", 0.90f, 0.72f, 0.72f)), V("azul", A("Louca", 0.62f, 0.75f, 0.85f)),
            V("verde-água", A("Louca", 0.68f, 0.84f, 0.78f)), V("amarela", A("Louca", 0.92f, 0.86f, 0.58f)),
        };

        // os gabinetes das pias: a madeira (a principal) e a louça do tampo com as cubas (a secundária)
        static readonly Variacao[] Gabinetes =
        {
            V("madeira escura, louça branca", A("MadeiraEscura"), A("Louca")),
            V("branco, louça rosa", A("MadeiraPintada", 0.94f, 0.93f, 0.90f), A("Louca", 0.90f, 0.72f, 0.72f)),
            V("madeira clara, louça azul", A("MadeiraClara"), A("Louca", 0.62f, 0.75f, 0.85f)),
            V("verde-água, louça branca", A("MadeiraPintada", 0.62f, 0.78f, 0.74f), A("Louca")),
        };

        static readonly Variacao[] Toalhas =
        {
            V("toalha branca", null, A("Tecido", 0.92f, 0.92f, 0.90f)), V("toalha rosa", null, A("Tecido", 0.85f, 0.62f, 0.66f)),
            V("toalha verde", null, A("Tecido", 0.50f, 0.62f, 0.48f)), V("toalha amarela", null, A("Tecido", 0.88f, 0.78f, 0.45f)),
        };

        // o plástico dos computadores
        static readonly Variacao[] Computadores =
        {
            V("cinza", A("Plastico", 0.62f, 0.62f, 0.60f)), V("branco", A("Plastico", 0.92f, 0.91f, 0.88f)), V("preto", A("Plastico", 0.20f, 0.20f, 0.20f)),
        };

        // as cadeiras de rodinhas: o estofado (a principal) e o plástico da base e dos braços (a secundária)
        static readonly Variacao[] CadeirasDeEscritorio =
        {
            V("tecido azul", A("Tecido", 0.30f, 0.38f, 0.52f)), V("tecido vinho", A("Tecido", 0.50f, 0.22f, 0.22f)),
            V("couro preto", A("Couro", 0.17f, 0.16f, 0.16f)), V("couro marrom, base cromada", A("Couro", 0.46f, 0.28f, 0.17f), A("MetalEscovado")),
        };

        // o aço pintado (os arquivos e as estantes)
        static readonly Variacao[] Acos =
        {
            V("bege", A("Esmalte", 0.80f, 0.76f, 0.66f)), V("cinza", A("Esmalte", 0.60f, 0.61f, 0.62f)),
            V("verde-oliva", A("Esmalte", 0.45f, 0.50f, 0.36f)), V("azul", A("Esmalte", 0.36f, 0.46f, 0.60f)), V("preto", A("Esmalte", 0.22f, 0.22f, 0.22f)),
        };

        // a sinuca: a madeira (a principal) e o pano (a secundária)
        static readonly Variacao[] Sinucas =
        {
            V("madeira clara, pano azul", A("MadeiraClara"), A("Tecido", 0.24f, 0.38f, 0.60f)),
            V("madeira escura, pano vinho", A("MadeiraEscura"), A("Tecido", 0.48f, 0.16f, 0.18f)),
            V("preta, pano cinza", A("MadeiraPintada", 0.16f, 0.16f, 0.16f), A("Tecido", 0.48f, 0.50f, 0.52f)),
            V("madeira clara, pano verde", A("MadeiraClara")),
        };

        static readonly Variacao[] Colchonetes =
        {
            V("verde", A("Tecido", 0.42f, 0.52f, 0.36f)), V("laranja", A("Tecido", 0.80f, 0.46f, 0.22f)),
            V("vermelho", A("Tecido", 0.62f, 0.22f, 0.20f)), V("cinza", A("Tecido", 0.50f, 0.50f, 0.50f)),
        };

        // os sacos de dormir: o pano de fora (a principal) e o forro (a secundária)
        static readonly Variacao[] SacosDeDormir =
        {
            V("verde-oliva, forro xadrez", A("Tecido", 0.38f, 0.42f, 0.28f)),
            V("vermelho, forro cinza", A("Tecido", 0.62f, 0.20f, 0.18f), A("Tecido", 0.62f, 0.62f, 0.60f)),
            V("laranja, forro xadrez verde", A("Tecido", 0.80f, 0.45f, 0.20f), A("Xadrez", 0.60f, 0.80f, 0.62f)),
        };

        static readonly Variacao[] Geladeiras =
        {
            V("branca", A("Esmalte", 1.0f, 1.0f, 1.0f)), V("verde-abacate", A("Esmalte", 0.58f, 0.62f, 0.36f)),
            V("amarelo-ouro", A("Esmalte", 0.86f, 0.68f, 0.30f)), V("marrom", A("Esmalte", 0.50f, 0.36f, 0.24f)),
            V("vermelha", A("Esmalte", 0.66f, 0.20f, 0.18f)),
        };

        static readonly Variacao[] TVs =
        {
            V("plástico cinza", A("Plastico", 0.55f, 0.55f, 0.55f)), V("plástico bege", A("Plastico", 0.82f, 0.78f, 0.68f)),
            V("imitando madeira", A("MadeiraEscura")),
        };

        static readonly Variacao[] Molduras =
        {
            V("moldura dourada", null, A("MadeiraClara", 0.95f, 0.78f, 0.45f)), V("moldura preta", null, A("MadeiraPintada", 0.16f, 0.16f, 0.16f)),
            V("moldura branca", null, A("MadeiraPintada", 0.94f, 0.93f, 0.90f)),
        };

        static readonly Variacao[] Vasos =
        {
            V("louça branca", A("Louca")), V("louça azul", A("Louca", 0.36f, 0.48f, 0.62f)), V("louça verde", A("Louca", 0.44f, 0.56f, 0.42f)),
        };

        // ------------------------------------------------------------------ os modelos

        static Tipo T(string modelo, string nome, Grupo grupo, Lugar lugar, Variacao[] variacoes) =>
            new Tipo { modelo = modelo, nome = nome, grupo = grupo, lugar = lugar, variacoes = variacoes };

        // um módulo da cozinha: junto dos vizinhos, com as cores das cozinhas (o de parede, pendurado a 1,40 m)
        static Tipo J(string modelo, string nome, Lugar lugar) =>
            new Tipo { modelo = modelo, nome = nome, grupo = Grupo.Cozinha, lugar = lugar, junto = true, altura = lugar == Lugar.Alto ? 1.40f : 0f,
                       variacoes = Comecando("madeira clara, granito cinza", Cozinhas) };

        /// <summary>Na ordem do mostruário (dentro de cada cômodo, da esquerda para a direita).</summary>
        public static readonly Tipo[] Tipos =
        {
            // a sala
            T("Lareira", "Lareira", Grupo.Sala, Lugar.Parede, new[] { V("tijolo"), V("tijolo escuro, console claro", A("Tijolo", 0.62f, 0.58f, 0.55f), A("MadeiraClara")),
                                                                    V("reboco branco", A("Louca", 0.88f, 0.86f, 0.82f)) }),
            T("Sofa1", "Poltrona", Grupo.Sala, Lugar.Parede, Comecando("tecido verde", Estofados)),
            T("Sofa2", "Sofá de 2 lugares", Grupo.Sala, Lugar.Parede, Comecando("tecido verde", Estofados)),
            T("MesaDeCanto", "Mesa de canto (com gaveta)", Grupo.Sala, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("Sofa3", "Sofá de 3 lugares", Grupo.Sala, Lugar.Parede, Comecando("tecido verde", Estofados)),
            T("AbajurDePe", "Abajur de pé", Grupo.Sala, Lugar.Parede, new[] { V("creme e bronze"), V("verde e preto", A("Tecido", 0.52f, 0.60f, 0.48f), A("MetalEscovado", 0.2f, 0.2f, 0.2f)),
                                                                            V("rosa e dourado", A("Tecido", 0.80f, 0.60f, 0.56f), A("MetalEscovado", 0.85f, 0.72f, 0.45f)),
                                                                            V("branca e cromo", A("Tecido", 0.95f, 0.94f, 0.90f), A("MetalEscovado")) }),
            T("SofaL", "Sofá de canto (em L)", Grupo.Sala, Lugar.Parede, Comecando("tecido verde", Estofados)),
            T("RackSimples", "Rack simples", Grupo.Sala, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("RackArmario", "Rack com armário", Grupo.Sala, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("RackEstante", "Estante da TV", Grupo.Sala, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("TVConsole", "TV de chão (anos 70)", Grupo.Sala, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("EstanteLivros", "Estante de livros alta", Grupo.Sala, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("EstanteBaixa", "Estante de livros baixa", Grupo.Sala, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("VasoPlanta", "Vaso com a planta morta", Grupo.Sala, Lugar.Parede, Comecando("barro", Vasos)),
            new Tipo { modelo = "TVTubo", nome = "TV de tubo", grupo = Grupo.Sala, lugar = Lugar.Sobre, variacoes = Comecando("plástico preto", TVs),
                       sobre = new[] { ("RackSimples", 0.50f), ("RackArmario", 0.60f), ("RackEstante", 0.55f) } },
            new Tipo { modelo = "AbajurMesa", nome = "Abajur de mesa", grupo = Grupo.Sala, lugar = Lugar.Sobre, sobre = new[] { ("MesaDeCanto", 0.58f) },
                       variacoes = new[] { V("bege"), V("verde", A("Louca", 0.48f, 0.60f, 0.50f), A("Tecido", 0.90f, 0.86f, 0.74f)),
                                           V("azul", A("Louca", 0.40f, 0.50f, 0.66f), A("Tecido", 0.86f, 0.82f, 0.70f)),
                                           V("dourado e rosa", A("MetalEscovado", 0.85f, 0.70f, 0.42f), A("Tecido", 0.80f, 0.62f, 0.56f)) } },
            new Tipo { modelo = "VasoMesa", nome = "Vasinho de mesa", grupo = Grupo.Sala, lugar = Lugar.Sobre, sobre = new[] { ("MesaDeCentro", 0.42f) },
                       variacoes = Comecando("barro", Vasos) },
            new Tipo { modelo = "QuadroPaisagem", nome = "Quadro: a paisagem", grupo = Grupo.Sala, lugar = Lugar.Alto, altura = 1.0f, variacoes = Comecando("moldura escura", Molduras) },
            new Tipo { modelo = "QuadroRetrato", nome = "Quadro: o retrato da família", grupo = Grupo.Sala, lugar = Lugar.Alto, altura = 1.0f, variacoes = Comecando("moldura escura", Molduras) },
            new Tipo { modelo = "QuadroBarco", nome = "Quadro: o barco", grupo = Grupo.Sala, lugar = Lugar.Alto, altura = 1.05f, variacoes = Comecando("moldura escura", Molduras) },
            new Tipo { modelo = "QuadroFlores", nome = "Quadro: as flores", grupo = Grupo.Sala, lugar = Lugar.Alto, altura = 1.05f, variacoes = Comecando("moldura escura", Molduras) },
            T("MesaDeCentro", "Mesa de centro", Grupo.Sala, Lugar.Meio, Comecando("madeira escura", Madeiras)),
            T("TapetePersa", "Tapete persa", Grupo.Sala, Lugar.Meio, new[] { V("vermelho"), V("desbotado", A("Tapetes", 0.85f, 0.82f, 0.76f)) }),
            T("TapeteListras", "Tapete de listras", Grupo.Sala, Lugar.Meio, new[] { V("colorido"), V("desbotado", A("Tapetes", 0.85f, 0.82f, 0.76f)) }),
            T("TapeteRedondo", "Tapete redondo trançado", Grupo.Sala, Lugar.Meio, new[] { V("colorido"), V("desbotado", A("Tapetes", 0.85f, 0.82f, 0.76f)) }),
            T("TapeteFelpudo", "Tapete felpudo", Grupo.Sala, Lugar.Meio, new[] { V("branco"), V("cinza", A("Tapetes", 0.60f, 0.60f, 0.60f)),
                                                                                 V("rosa", A("Tapetes", 0.85f, 0.62f, 0.62f)), V("verde", A("Tapetes", 0.58f, 0.66f, 0.50f)),
                                                                                 V("marrom", A("Tapetes", 0.55f, 0.42f, 0.32f)) }),
            T("VasoTerra", "Vaso só com a terra", Grupo.Sala, Lugar.Meio, Comecando("barro", Vasos)),
            T("VasoQuebrado", "Vaso quebrado", Grupo.Sala, Lugar.Meio, Comecando("barro", Vasos)),

            // o quarto
            T("CamaCasal", "Cama de casal", Grupo.Quarto, Lugar.Parede, Comecando("madeira escura, colcha de retalhos", Camas)),
            T("CriadoMudo", "Criado-mudo", Grupo.Quarto, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("CamaSolteiro", "Cama de solteiro", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara, colcha de retalhos", Camas)),
            T("Beliche", "Beliche", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara, colcha de retalhos", Camas)),
            T("Berco", "Berço", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("GuardaRoupa2", "Guarda-roupa de 2 portas", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("GuardaRoupa3", "Guarda-roupa de 3 portas", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("GuardaRoupa4", "Guarda-roupa de 4 portas", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("Comoda3", "Cômoda de 3 gavetas", Grupo.Quarto, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("Comoda6", "Cômoda de 6 gavetas", Grupo.Quarto, Lugar.Parede, Comecando("madeira escura", Madeiras)),
            T("Penteadeira", "Penteadeira (com a banqueta)", Grupo.Quarto, Lugar.Parede, Comecando("madeira clara", Madeiras)),
            T("Bau", "Baú", Grupo.Quarto, Lugar.Meio, Comecando("madeira escura", Madeiras)),
            // (sem a coberta, a pedido: não tem o que trocar)
            T("ColchaoNoChao", "Colchão no chão", Grupo.Quarto, Lugar.Meio, new[] { V("listrado") }),

            // a cozinha e a sala de jantar: os módulos de chão juntos (a bancada) e os de parede juntos em cima deles
            J("ArmarioCanto", "Módulo do canto (0,90 × 0,90)", Lugar.Parede),
            J("ArmarioChao40", "Armário de chão de 0,40", Lugar.Parede),
            J("Pia", "Pia (0,80)", Lugar.Parede),
            J("ArmarioChao60", "Armário de chão de 0,60", Lugar.Parede),
            new Tipo { modelo = "Fogao", nome = "Fogão de 4 bocas", grupo = Grupo.Cozinha, lugar = Lugar.Parede, junto = true, variacoes = Comecando("branca", Geladeiras) },
            J("ArmarioChao80", "Armário de chão de 0,80", Lugar.Parede),
            J("Gaveteiro40", "Gaveteiro de 0,40", Lugar.Parede),
            T("Geladeira", "Geladeira pequena", Grupo.Cozinha, Lugar.Parede, Comecando("creme", Geladeiras)),
            T("GeladeiraGrande", "Geladeira grande", Grupo.Cozinha, Lugar.Parede, Comecando("creme", Geladeiras)),
            J("ArmarioParede40", "Armário de parede de 0,40", Lugar.Alto),
            J("ArmarioParede60", "Armário de parede de 0,60", Lugar.Alto),
            J("ArmarioParedeAberto60", "Armário de parede aberto de 0,60", Lugar.Alto),
            J("ArmarioParede80", "Armário de parede de 0,80", Lugar.Alto),
            J("ArmarioParedeAberto80", "Armário de parede aberto de 0,80", Lugar.Alto),
            T("Ilha", "Ilha", Grupo.Cozinha, Lugar.Meio, Comecando("madeira clara, granito cinza", Cozinhas)),
            new Tipo { modelo = "Mesa4", nome = "Mesa de 4 lugares", grupo = Grupo.Cozinha, lugar = Lugar.Meio, cadeira = "Cadeira", cadeiras = 4,
                       variacoes = Comecando("madeira clara", Mesas) },
            new Tipo { modelo = "Mesa6", nome = "Mesa de 6 lugares", grupo = Grupo.Cozinha, lugar = Lugar.Meio, cadeira = "CadeiraEstofada", cadeiras = 6,
                       variacoes = Comecando("madeira escura", Mesas) },
            new Tipo { modelo = "MesaRedonda", nome = "Mesa redonda", grupo = Grupo.Cozinha, lugar = Lugar.Meio, cadeira = "Cadeira", cadeiras = 4, redonda = true,
                       variacoes = Comecando("madeira clara", Mesas) },
            T("Cadeira", "Cadeira de madeira", Grupo.Cozinha, Lugar.ComAMesa, Comecando("madeira clara", Mesas)),
            T("CadeiraEstofada", "Cadeira estofada", Grupo.Cozinha, Lugar.ComAMesa, Comecando("madeira escura, tecido bege", Estofadas)),

            // o banheiro
            T("Box", "Box (o vidro quebrado)", Grupo.Banheiro, Lugar.Parede, Comecando("branca", Loucas)),
            T("Privada", "Privada", Grupo.Banheiro, Lugar.Parede, Comecando("branca", Loucas)),
            new Tipo { modelo = "Toalheiro", nome = "Toalheiro", grupo = Grupo.Banheiro, lugar = Lugar.Alto, altura = 0.9f, variacoes = Comecando("toalha azul", Toalhas) },
            T("PiaColuna", "Pia de coluna", Grupo.Banheiro, Lugar.Parede, Comecando("branca", Loucas)),
            T("Banheira", "Banheira", Grupo.Banheiro, Lugar.Parede, Comecando("branca", Loucas)),
            T("PiaGabinete", "Pia com gabinete", Grupo.Banheiro, Lugar.Parede, Comecando("madeira clara, louça branca", Gabinetes)),
            new Tipo { modelo = "EspelhoArmario", nome = "Espelho com armário", grupo = Grupo.Banheiro, lugar = Lugar.Alto, altura = 1.25f,
                       variacoes = Comecando("pintado de branco", Madeiras) },
            T("PiaDupla", "Pia dupla com gabinete", Grupo.Banheiro, Lugar.Parede, Comecando("madeira clara, louça branca", Gabinetes)),

            // o escritório, a garagem e o porão (com o acampamento de quem se escondeu lá)
            new Tipo { modelo = "MesaEscritorio", nome = "Mesa de escritório", grupo = Grupo.Servico, lugar = Lugar.Parede, cadeira = "CadeiraEscritorio", cadeiras = 1,
                       vao = 0.20f, variacoes = Comecando("madeira escura", Madeiras) },
            new Tipo { modelo = "Computador", nome = "Computador (anos 90)", grupo = Grupo.Servico, lugar = Lugar.Sobre, sobre = new[] { ("MesaEscritorio", 0.76f) },
                       variacoes = Comecando("bege", Computadores) },
            T("CadeiraEscritorio", "Cadeira de rodinhas", Grupo.Servico, Lugar.ComAMesa, Comecando("tecido cinza-escuro", CadeirasDeEscritorio)),
            T("Arquivo", "Arquivo de aço (4 gavetas)", Grupo.Servico, Lugar.Parede, Comecando("cinza-esverdeado", Acos)),
            T("Bancada", "Bancada de ferramentas", Grupo.Servico, Lugar.Parede, Comecando("madeira crua", Madeiras)),
            T("PrateleiraMadeira90", "Prateleira de madeira de 0,90", Grupo.Servico, Lugar.Parede, Comecando("madeira crua", Madeiras)),
            T("PrateleiraMadeira150", "Prateleira de madeira de 1,50", Grupo.Servico, Lugar.Parede, Comecando("madeira crua", Madeiras)),
            T("EstanteAco90", "Estante de aço de 0,90", Grupo.Servico, Lugar.Parede, Comecando("cinza", Acos)),
            T("EstanteAco120", "Estante de aço de 1,20", Grupo.Servico, Lugar.Parede, Comecando("cinza", Acos)),
            T("FreezerHorizontal", "Freezer horizontal", Grupo.Servico, Lugar.Parede, Comecando("branca", Geladeiras)),
            T("MesaSinuca", "Mesa de sinuca", Grupo.Servico, Lugar.Meio, Comecando("madeira escura, pano verde", Sinucas)),
            T("Colchonete", "Colchonete", Grupo.Servico, Lugar.Meio, Comecando("azul", Colchonetes)),
            T("SacoDeDormir", "Saco de dormir", Grupo.Servico, Lugar.Meio, Comecando("azul, forro xadrez", SacosDeDormir)),
            T("Fogareiro", "Fogareiro de camping", Grupo.Servico, Lugar.Meio, new[] { V("cartucho vermelho") }),
            T("Latas", "Latas vazias", Grupo.Servico, Lugar.Meio, new[] { V("rótulos desbotados") }),
            T("Garrafas", "Garrafas vazias e o galão", Grupo.Servico, Lugar.Meio, new[] { V("plástico azulado") }),
        };

        static Dictionary<string, Tipo> _porModelo;

        /// <summary>O tipo do modelo (o nome do objeto do FBX), ou null.</summary>
        public static Tipo DoModelo(string modelo)
        {
            if (_porModelo == null) _porModelo = Tipos.ToDictionary(t => t.modelo);
            return modelo != null && _porModelo.TryGetValue(modelo, out var t) ? t : null;
        }

        // ------------------------------------------------------------------ pintar

        /// <summary>
        /// A malha da variação: uma cópia da do modelo (legível) com a textura e a cor da parte principal e da secundária
        /// trocadas. Trocando uma textura que se repete por outra, o UV0 (em metros vezes a escala) é esticado para a escala
        /// da nova (o tecido tem 3 por metro, o couro 2). A variação sem nada devolve a própria malha.
        /// </summary>
        public static Mesh Pintar(Mesh origem, Variacao v)
        {
            if (v == null || (v.principal == null && v.secundaria == null)) return origem;
            var m = Object.Instantiate(origem);
            m.name = $"{origem.name} ({v.nome})";
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var cores = new List<Color>();
            m.GetUVs(0, uv0);
            m.GetUVs(1, uv1);
            m.GetColors(cores);
            if (uv1.Count != m.vertexCount || cores.Count != m.vertexCount) return m;
            for (int i = 0; i < uv1.Count; i++)
            {
                int troca = Mathf.RoundToInt(uv1[i].y);
                var a = troca == 1 ? v.principal : troca == 2 ? v.secundaria : null;
                if (a == null) continue;
                int velha = Mathf.Clamp(Mathf.RoundToInt(uv1[i].x), 0, Texturas.Length - 1);
                if (velha != a.textura && !Ajustadas.Contains(Texturas[velha].nome) && !Ajustadas.Contains(Texturas[a.textura].nome))
                    uv0[i] *= Texturas[a.textura].escala / Texturas[velha].escala;
                uv1[i] = new Vector2(a.textura, troca);
                cores[i] = a.cor.linear;
            }
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetColors(cores);
            return m;
        }
    }
}
