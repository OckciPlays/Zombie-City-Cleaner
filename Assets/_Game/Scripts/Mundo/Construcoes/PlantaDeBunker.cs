using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A planta do bunker novo (o Plano_Bunker.md da pasta de arte, 01/11/2026): a caixa de 32 × 24 m com 5 andares, a
    /// garagem (B0) rente ao terreno e os outros embaixo dela (B1 a sala comum, B2 a cozinha e o refeitório, B3 os
    /// dormitórios, B4 a manutenção), o núcleo no mesmo lugar em todos (a escada em U, o poço do elevador e o duto) e a
    /// escada subindo da garagem até a casinha no alto do morro. As medidas são as do esboço
    /// (`Imgs/Bunker/Bunker_Esboco.png`): os cômodos são escritos como lá (x de 0 a 32 da esquerda para a direita de quem
    /// olha o portão de fora, z de 0, a frente, a 24, o fundo) e passados para as coordenadas da obra (o meio no centro, a
    /// frente em -z, o chão da garagem em y = 0) por <see cref="R"/>. As linhas das paredes são as bordas dos cômodos.
    /// </summary>
    public static class PlantaDeBunker
    {
        // ------------------------------------------------------------ a caixa

        /// <summary>As linhas das paredes de fora (o meio delas): x de -16 a 16, z de -12 (a frente, o portão) a 12.</summary>
        public const float X0 = -16f, X1 = 16f, Z0 = -12f, Z1 = 12f;
        public const float ParedeDeFora = 0.4f;
        public const int Andares = 5;
        /// <summary>A garagem (B0) tem 5 m de pé-direito; os outros andares, 3,4 m; a laje, 0,6 m (4 m de um chão ao outro).</summary>
        public const float PeDireitoDaGaragem = 5f, PeDireito = 3.4f, GrossuraDaLaje = 0.6f, EntreAndares = 4f;
        /// <summary>A terra em cima da laje da garagem, e quanto o morro anda para fora a cada metro que desce.</summary>
        public const float Terra = 1.5f, Talude = 2f;

        /// <summary>O chão do andar f (0 = B0, a garagem, rente ao terreno).</summary>
        public static float Base(int f) => -f * EntreAndares;
        /// <summary>O teto do andar f (a face de baixo da laje de cima).</summary>
        public static float Teto(int f) => Base(f) + (f == 0 ? PeDireitoDaGaragem : PeDireito);
        /// <summary>O topo da laje da garagem e o alto do morro (a terra por cima dela).</summary>
        public const float TopoDaCaixa = PeDireitoDaGaragem + GrossuraDaLaje, Morro = TopoDaCaixa + Terra;

        /// <summary>O andar de uma altura: o chão dele, a laje embaixo e as paredes até a laje de cima.</summary>
        public static int AndarEm(float y) => Mathf.Clamp(Mathf.CeilToInt((-y - GrossuraDaLaje + 0.001f) / EntreAndares), 0, Andares - 1);

        public static readonly string[] Codigos = { "B0", "B1", "B2", "B3", "B4" };
        public static readonly string[] NomesDosAndares = { "Garagem e operações", "Sala comum", "Cozinha e refeitório", "Dormitórios", "Manutenção" };

        /// <summary>Um retângulo do esboço (x de 0 a 32, z de 0 a 24) nas coordenadas da obra.</summary>
        public static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0 + X0, z0 + Z0, x1 + X0, z1 + Z0);

        // ------------------------------------------------------------ o portão, a frente e o morro

        /// <summary>O portão, na frente da garagem: de x0 a x0 + largura, 4,2 m de altura, de duas folhas.</summary>
        public const float PortaoX0 = 1.25f, PortaoLargura = 5.5f, PortaoAltura = 4.2f;
        /// <summary>A portinhola, na folha da esquerda: a partir da dobradiça dela, a largura e a altura.</summary>
        public const float PortinholaDe = 1.0f, PortinholaLargura = 1.0f, PortinholaAltura = 2.15f;
        /// <summary>
        /// O corte do morro na frente do portão (o pátio entre as abas de concreto): as abas saem da frente da caixa em x0 e
        /// x1 e abrem para fora até o pé do morro (<see cref="Morro"/> × <see cref="Talude"/> à frente).
        /// </summary>
        public const float CorteX0 = -1f, CorteX1 = 9f, AbreCorte = 5f;
        /// <summary>O pátio de concreto na frente do portão, até a cerca, e o vão da estrada de terra na cerca (em x).</summary>
        public static readonly Rect Patio = Rect.MinMaxRect(-14f, -42f, 22f, Z0 - ParedeDeFora / 2f);
        public const float VaoDaCercaX0 = 1.5f, VaoDaCercaX1 = 8.5f;
        /// <summary>O meio do vão da cerca, onde a estrada de terra chega (na frente do pátio).</summary>
        public static Vector2 ChegadaDaEstrada => new Vector2((VaoDaCercaX0 + VaoDaCercaX1) / 2f, Patio.yMin);
        /// <summary>O chão que o bunker ocupa: o pé do morro (a caixa e o talude) e o pátio; no mundo, ele fica plano.</summary>
        public static Rect Pegada => Rect.MinMaxRect(X0 - Morro * Talude, Patio.yMin, X1 + Morro * Talude, Z1 + Morro * Talude);
        /// <summary>
        /// A partida no bunker (nas coordenadas do esboço): os 4 lugares na sala de operações (fora dos móveis), a mesa de
        /// reunião para onde eles olham, o meio da sala, e as vagas da perua e do cupê (a 2 e a 3 das 4 do fundo da garagem).
        /// </summary>
        public static readonly Vector2[] Nascimentos = { new Vector2(2.4f, 3.6f), new Vector2(2.4f, 5.6f), new Vector2(9.5f, 4.6f), new Vector2(4.2f, 7.4f) };
        public static readonly Vector2 MesaDeReuniao = new Vector2(6.8f, 4.8f), MeioDasOperacoes = new Vector2(6f, 4.3f);
        public static readonly Vector2 VagaDaPerua = new Vector2(18.1f, 20.6f), VagaDoCupe = new Vector2(21.9f, 20.6f);
        /// <summary>A casinha da saída de cima, em cima do núcleo: do alto do morro até o teto dela.</summary>
        public const float AlturaDaCasinha = 3.0f;

        // ------------------------------------------------------------ o núcleo

        /// <summary>A escada em U: a caixa (de x0 a x1, o patamar de cada andar do lado de x1, onde fica a porta).</summary>
        public static readonly Rect Escada = R(0f, 8.6f, 6f, 12.2f);
        public static readonly Rect Poco = R(3.4f, 12.2f, 6f, 15f), Duto = R(0f, 12.2f, 3.4f, 15f);
        /// <summary>O patamar de cada andar, do lado da porta.</summary>
        public const float Patamar = 1.5f;
        /// <summary>Os lances (de chão a chão), do B4 até a casinha: os do meio de 4 m, os de cima de metade da subida até o morro.</summary>
        public static readonly (float de, float ate)[] Subidas =
        {
            (Base(4), Base(3)), (Base(3), Base(2)), (Base(2), Base(1)), (Base(1), Base(0)), (0f, Morro / 2f), (Morro / 2f, Morro),
        };
        /// <summary>O chão da escada (por dentro das paredes) e o vão nas lajes (tudo menos o patamar dos andares).</summary>
        public static float EscadaXi => Escada.xMin + ParedeDeFora / 2f;
        public static float EscadaXf => Escada.xMax - 0.1f;
        public static float EscadaZi => Escada.yMin + 0.1f;
        public static float EscadaZf => Escada.yMax - 0.1f;
        public static float FimDoPatamar => EscadaXf - Patamar;
        public static Rect VaoDaEscada => Rect.MinMaxRect(EscadaXi, EscadaZi, FimDoPatamar, EscadaZf);
        /// <summary>O vão na laje de cima da garagem: a caixa da escada inteira (o patamar do meio da subida até o morro fica embaixo dela).</summary>
        public static Rect VaoDaSubida => Rect.MinMaxRect(EscadaXi, EscadaZi, EscadaXf, EscadaZf);
        /// <summary>A porta da escada (na parede de x1) e a do elevador, pelo meio em z.</summary>
        public static float PortaDaEscadaZ => Escada.center.y;
        public static float PortaDoElevadorZ => Poco.center.y;
        public const float LarguraDaPortaDaEscada = 1.4f;

        public static bool DoNucleo(Comodo k) => k.tipo == "escada" || k.tipo == "poco" || k.tipo == "duto";

        // ------------------------------------------------------------ os andares

        /// <summary>
        /// Um andar: os cômodos (com o núcleo), o nome de cada um, as portas e passagens entre eles (<see cref="Ligacao"/>:
        /// pos na linha em comum; passagem = sem folha; largura 0 = a da porta, maior que a parede = sem parede) e as janelas
        /// de dentro (o vidro da sala de operações, o balcão da cozinha).
        /// </summary>
        public sealed class Andar
        {
            public int f;
            public readonly List<Comodo> comodos = new List<Comodo>();
            public readonly Dictionary<Comodo, string> nomes = new Dictionary<Comodo, string>();
            public readonly List<Ligacao> ligacoes = new List<Ligacao>();
            public readonly List<(Comodo a, Comodo b, float pos, float largura, float baixo, float alto)> janelas =
                new List<(Comodo, Comodo, float, float, float, float)>();

            public Comodo Em(float x, float z)
            {
                foreach (var k in comodos) if (k.Contem(x, z)) return k;
                return null;
            }

            internal Comodo K(string tipo, string nome, float x0, float z0, float x1, float z1, string papel, string piso)
            {
                var r = R(x0, z0, x1, z1);
                var k = new Comodo { tipo = tipo, andar = f, x0 = r.xMin, z0 = r.yMin, x1 = r.xMax, z1 = r.yMax, papel = papel, piso = piso };
                comodos.Add(k);
                nomes[k] = nome;
                return k;
            }

            // a linha em comum de a e b corre em z (x constante) ou em x; pos vem do esboço
            static bool Vertical(Comodo a, Comodo b) => Mathf.Abs(a.x1 - b.x0) < 0.01f || Mathf.Abs(b.x1 - a.x0) < 0.01f;

            internal void Porta(Comodo a, Comodo b, float pos, bool passagem = false, float largura = 0f, bool deAco = false, string modelo = null) =>
                ligacoes.Add(new Ligacao { a = a, b = b, pos = pos + (Vertical(a, b) ? Z0 : X0), passagem = passagem, largura = largura, deAco = deAco, modelo = modelo });

            internal void Janela(Comodo a, Comodo b, float de, float ate, float baixo, float alto)
            {
                float o = Vertical(a, b) ? Z0 : X0;
                janelas.Add((a, b, (de + ate) / 2f + o, ate - de, baixo, alto));
            }
        }

        /// <summary>O andar f, montado de novo (os cômodos são objetos novos a cada chamada).</summary>
        public static Andar DoAndar(int f)
        {
            var a = new Andar { f = f };
            // o núcleo, igual em todos (as paredes dele são da casca)
            a.K("escada", "Escada", 0f, 8.6f, 6f, 12.2f, "ConcretoClaro", null);
            a.K("poco", "Elevador", 3.4f, 12.2f, 6f, 15f, "Concreto", null);
            a.K("duto", "Dutos", 0f, 12.2f, 3.4f, 15f, "Concreto", null);
            switch (f)
            {
                case 0:
                {
                    var op = a.K("operacoes", "Operações", 0f, 0f, 12f, 8.6f, "ConcretoClaro", "Piso");
                    var hall = a.K("corredor", "Hall", 6f, 8.6f, 12f, 15f, "ConcretoClaro", "Piso");
                    var arsenal = a.K("arsenal", "Arsenal e fabricação", 0f, 15f, 12f, 24f, "ConcretoClaro", "Piso");
                    var garagem = a.K("garagem", "Garagem", 12f, 0f, 28f, 24f, "Concreto", "Concreto");
                    var combustivel = a.K("combustivel", "Combustível", 28f, 0f, 32f, 6f, "Concreto", "Concreto");
                    var oficina = a.K("oficina", "Oficina", 28f, 6f, 32f, 24f, "Concreto", "Concreto");
                    a.Porta(hall, garagem, 11.8f);
                    a.Porta(op, hall, 9f);
                    a.Porta(hall, arsenal, 9f);
                    a.Porta(garagem, combustivel, 3f, deAco: true);
                    a.Porta(garagem, oficina, 15f, passagem: true, largura: 99f);
                    a.Janela(op, garagem, 1f, 7.6f, 1.0f, 2.4f);
                    break;
                }
                case 1:
                {
                    var deposito = a.K("deposito", "Depósito", 0f, 0f, 9f, 8.6f, "ConcretoClaro", "Piso");
                    var hall = a.K("corredor", "Hall", 6f, 8.6f, 9f, 15f, "ConcretoClaro", "Piso");
                    var banheiros = a.K("banheiro", "Banheiros", 0f, 15f, 9f, 24f, "Azulejo", "Azulejo");
                    var estar = a.K("sala", "Sala de estar", 9f, 0f, 22f, 12f, "ParedeBege", "Carpete");
                    var jogos = a.K("jogos", "Sala de jogos", 9f, 12f, 27f, 24f, "ParedeBege", "Taco");
                    var enfermaria = a.K("enfermaria", "Enfermaria", 22f, 0f, 32f, 12f, "ParedeInterna", "Azulejo");
                    var isolamento = a.K("isolamento", "Isolamento", 27f, 12f, 32f, 24f, "ParedeInterna", "Azulejo");
                    a.Porta(hall, estar, 10.2f);
                    a.Porta(hall, jogos, 13.8f);
                    a.Porta(hall, deposito, 7.5f);
                    a.Porta(hall, banheiros, 7.5f);
                    a.Porta(estar, jogos, 15.5f, passagem: true, largura: 9f);
                    a.Porta(estar, enfermaria, 5.7f);
                    a.Porta(enfermaria, isolamento, 29.5f, deAco: true);
                    break;
                }
                case 2:
                {
                    var lavagem = a.K("lavagem", "Lavagem e lixo", 0f, 0f, 9f, 8.6f, "Azulejo", "Azulejo");
                    var hall = a.K("corredor", "Hall", 6f, 8.6f, 9f, 15f, "ConcretoClaro", "Piso");
                    var camara = a.K("camara", "Câmara fria", 0f, 15f, 9f, 24f, "Azulejo", "Azulejo");
                    var refeitorio = a.K("refeitorio", "Refeitório", 9f, 0f, 32f, 12f, "ParedeBege", "Piso");
                    var cozinha = a.K("cozinha", "Cozinha", 9f, 12f, 21f, 24f, "Azulejo", "Azulejo");
                    var despensa = a.K("despensa", "Despensa", 21f, 12f, 32f, 24f, "ConcretoClaro", "Piso");
                    a.Porta(hall, refeitorio, 10.2f);
                    a.Porta(hall, cozinha, 13.8f);
                    a.Porta(refeitorio, lavagem, 3.7f);
                    a.Porta(cozinha, camara, 19.7f, deAco: true, modelo: "PortaCamaraFria");
                    a.Porta(cozinha, despensa, 17.7f);
                    a.Porta(refeitorio, cozinha, 20.2f);
                    a.Janela(refeitorio, cozinha, 11.5f, 18.5f, 0.95f, 2.1f);
                    break;
                }
                case 3:
                {
                    var corredor = a.K("corredor", "Corredor", 6f, 0f, 9f, 24f, "ConcretoClaro", "Piso");
                    var q5 = a.K("quarto", "Quarto 5", 0f, 0f, 6f, 8.6f, "ParedeBege", "Taco");
                    var q6 = a.K("quarto", "Quarto 6", 0f, 15f, 6f, 24f, "ParedeBege", "Taco");
                    var corredor2 = a.K("corredor", "Corredor", 9f, 11f, 32f, 13f, "ConcretoClaro", "Piso");
                    var dormitorio = a.K("dormitorio", "Dormitório", 9f, 0f, 21f, 11f, "ParedeVerde", "Taco");
                    var lavanderia = a.K("lavanderia", "Lavanderia", 21f, 0f, 27f, 11f, "ParedeInterna", "Azulejo");
                    var rouparia = a.K("rouparia", "Rouparia", 27f, 0f, 32f, 11f, "ParedeInterna", "Piso");
                    var q1 = a.K("quarto", "Quarto 1", 9f, 13f, 14f, 19.5f, "ParedeBege", "Taco");
                    var q2 = a.K("quarto", "Quarto 2", 14f, 13f, 19f, 19.5f, "ParedeBege", "Taco");
                    var q3 = a.K("quarto", "Quarto 3", 19f, 13f, 24f, 19.5f, "ParedeBege", "Taco");
                    var q4 = a.K("quarto", "Quarto 4", 24f, 13f, 29f, 19.5f, "ParedeBege", "Taco");
                    var deposito = a.K("deposito", "Depósito", 29f, 13f, 32f, 19.5f, "ConcretoClaro", "Piso");
                    var banheiros = a.K("banheiro", "Banheiros e chuveiros", 9f, 19.5f, 32f, 24f, "Azulejo", "Azulejo");
                    a.Porta(corredor, q5, 4.3f);
                    a.Porta(corredor, q6, 19.5f);
                    a.Porta(corredor, corredor2, 12f, passagem: true, largura: 99f);
                    a.Porta(corredor, dormitorio, 5.5f);
                    a.Porta(corredor, banheiros, 21.8f);
                    a.Porta(corredor2, dormitorio, 15f);
                    a.Porta(corredor2, lavanderia, 24f);
                    a.Porta(corredor2, rouparia, 29.5f);
                    a.Porta(corredor2, q1, 11.5f);
                    a.Porta(corredor2, q2, 16.5f);
                    a.Porta(corredor2, q3, 21.5f);
                    a.Porta(corredor2, q4, 26.5f);
                    a.Porta(corredor2, deposito, 30.5f);
                    break;
                }
                case 4:
                {
                    var esgoto = a.K("esgoto", "Esgoto", 0f, 0f, 9f, 8.6f, "Concreto", "Concreto");
                    var hall = a.K("corredor", "Hall", 6f, 8.6f, 9f, 15f, "Concreto", "Concreto");
                    var pecas = a.K("pecas", "Peças", 0f, 15f, 9f, 24f, "Concreto", "Concreto");
                    var eletrica = a.K("eletrica", "Elétrica", 9f, 0f, 20f, 12f, "Concreto", "Concreto");
                    var gerador = a.K("gerador", "Gerador", 9f, 12f, 20f, 24f, "Concreto", "Concreto");
                    var agua = a.K("agua", "Água", 20f, 0f, 32f, 12f, "Concreto", "Concreto");
                    var ar = a.K("ar", "Ar", 20f, 12f, 32f, 24f, "Concreto", "Concreto");
                    a.Porta(hall, eletrica, 10.2f);
                    a.Porta(hall, gerador, 13.8f);
                    a.Porta(hall, esgoto, 7.5f, deAco: true);
                    a.Porta(hall, pecas, 7.5f);
                    a.Porta(eletrica, agua, 5.7f, deAco: true);
                    a.Porta(gerador, ar, 17.7f, deAco: true);
                    a.Porta(eletrica, gerador, 14.7f);
                    break;
                }
            }
            return a;
        }
    }
}
