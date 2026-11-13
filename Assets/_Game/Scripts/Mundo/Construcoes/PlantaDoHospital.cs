using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A planta do Hospital Principal (o Plano_Hospital.md da pasta de arte, 07/11/2026): o corpo de 56 × 32 m com 4 andares
    /// (o subsolo, o térreo, o 2º e o 3º), o núcleo no mesmo lugar em todos (a escada oeste e a leste nos cantos do fundo, o
    /// elevador no meio, de frente para o saguão; a escada de serviço só do subsolo ao térreo) e, só no térreo, a garagem das
    /// ambulâncias (do lado oeste) e a entrada principal (na frente, no meio). As quatro plantas conceituais do usuário foram
    /// juntadas numa só grade: em cada andar, a faixa do fundo (8 m), o corredor de cima (3 m), a faixa do meio (10 m, com o
    /// saguão em volta do elevador), o corredor de baixo (3 m) e a faixa da frente (8 m).
    ///
    /// Os cômodos são escritos como no esboço (x de 0 a 56, de oeste para leste; z de 0, a frente, a 32, o fundo) e passados
    /// para as coordenadas da obra (o meio no centro, a frente em -z, o chão do térreo em y = 0) por <see cref="R"/>. As
    /// linhas das paredes são as bordas dos cômodos: entre dois cômodos vizinhos, a parede de dentro; onde nenhum encosta, a
    /// de fora.
    /// </summary>
    public static class PlantaDoHospital
    {
        // ------------------------------------------------------------ o corpo e os andares

        /// <summary>As linhas das paredes de fora do corpo: x de -28 a 28, z de -16 (a frente) a 16 (o fundo).</summary>
        public const float X0 = -28f, X1 = 28f, Z0 = -16f, Z1 = 16f;
        /// <summary>0 = o subsolo, 1 = o térreo, 2 = o 2º andar, 3 = o 3º andar.</summary>
        public const int Andares = 4, Terreo = 1;
        /// <summary>4 m de um chão ao outro: a laje (com o forro e os canos) de 0,6 m e o pé-direito de 3,4 m.</summary>
        public const float EntreAndares = 4f, GrossuraDaLaje = 0.6f, PeDireito = 3.4f;
        /// <summary>A mureta em volta da cobertura.</summary>
        public const float Platibanda = 1.1f;
        /// <summary>
        /// O chão em volta do prédio fica 45 cm abaixo do térreo (a parte 3: o embasamento): a entrada sobe pelos degraus e
        /// pelas rampas da varanda, as ambulâncias pela rampa da garagem, e as saídas têm os degraus.
        /// </summary>
        public const float Embasamento = 0.45f;
        public const float ChaoDeFora = -Embasamento;

        public static float Base(int f) => (f - Terreo) * EntreAndares;
        public static float Teto(int f) => Base(f) + PeDireito;
        /// <summary>O topo da laje da cobertura.</summary>
        public static float TopoDaCobertura => Base(Andares);
        /// <summary>O andar de uma altura: o chão dele, a laje embaixo e as paredes até a laje de cima.</summary>
        public static int AndarEm(float y) => Mathf.Clamp(Mathf.FloorToInt((y - Base(0) + GrossuraDaLaje) / EntreAndares), 0, Andares - 1);

        public static readonly string[] Codigos = { "SS", "T", "2º", "3º" };
        public static readonly string[] NomesDosAndares =
        {
            "Subsolo · infraestrutura e necrotério", "Térreo · atendimento e emergência",
            "2º andar · internação e tratamento", "3º andar · administração e direção",
        };
        /// <summary>Os nomes dos andares em inglês (o jogo se passa nos Estados Unidos), para as placas (a parte 4).</summary>
        public static readonly string[] NomesNaParede = { "B BASEMENT", "1 EMERGENCY", "2 INPATIENT", "3 ADMINISTRATION" };

        /// <summary>Um retângulo do esboço (x de 0 a 56, z de 0 a 32) nas coordenadas da obra.</summary>
        public static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0 + X0, z0 + Z0, x1 + X0, z1 + Z0);

        // ------------------------------------------------------------ o núcleo

        public static readonly Rect EscadaOeste = R(0f, 24f, 4f, 32f), EscadaLeste = R(52f, 24f, 56f, 32f), EscadaDeServico = R(18f, 24f, 22f, 32f);
        /// <summary>O poço do elevador (de maca), com a porta para o sul, de frente para o saguão de cada andar.</summary>
        public static readonly Rect Elevador = R(26.5f, 17.5f, 29.5f, 21f);
        /// <summary>As escadas: o nome, a caixa (a porta no lado de z0, para o corredor do fundo) e de que andar a que andar.</summary>
        public static readonly (string nome, Rect caixa, int de, int ate)[] Escadas =
        {
            ("Escada oeste", EscadaOeste, 0, Andares - 1), ("Escada leste", EscadaLeste, 0, Andares - 1), ("Escada de serviço", EscadaDeServico, 0, Terreo),
        };
        /// <summary>O patamar de cada andar, do lado da porta (a escada sobe em U para o fundo e volta).</summary>
        public const float Patamar = 1.8f, LarguraDaPortaDaEscada = 1.2f, LarguraDaPortaDoElevador = 1.4f;

        // as medidas da escada dentro da caixa (as paredes de 20 cm)
        public static float Xi(Rect c) => c.xMin + 0.1f;
        public static float Xf(Rect c) => c.xMax - 0.1f;
        public static float Zi(Rect c) => c.yMin + 0.1f;
        public static float Zf(Rect c) => c.yMax - 0.1f;
        /// <summary>Onde o patamar do andar acaba e o lance começa.</summary>
        public static float ZL(Rect c) => Zi(c) + Patamar;
        /// <summary>O vão da escada na laje de cima (tudo menos o patamar), 5 cm para dentro das paredes em volta.</summary>
        public static Rect VaoDaEscada(Rect c) => Rect.MinMaxRect(Xi(c) - 0.05f, ZL(c), Xf(c) + 0.05f, Zf(c) + 0.05f);
        /// <summary>O vão do poço do elevador nas lajes, 5 cm para dentro das paredes.</summary>
        public static Rect VaoDoElevador => Rect.MinMaxRect(Elevador.xMin + 0.05f, Elevador.yMin + 0.05f, Elevador.xMax - 0.05f, Elevador.yMax - 0.05f);

        public static bool DoNucleo(Comodo k) => k.tipo == "escada" || k.tipo == "elevador";

        /// <summary>O último andar do núcleo: as paredes dele param no teto (nos outros, passam pela laje de cima, que tem o vão).</summary>
        public static int UltimoAndar(Comodo k)
        {
            if (k.tipo == "escada")
                foreach (var e in Escadas)
                    if (Mathf.Abs(e.caixa.xMin - k.x0) < 0.01f && Mathf.Abs(e.caixa.yMin - k.z0) < 0.01f) return e.ate;
            return Andares - 1;
        }

        // ------------------------------------------------------------ os anexos do térreo

        /// <summary>A garagem das ambulâncias (do lado oeste, colada no recebimento) e a entrada principal (na frente, no meio).</summary>
        public static readonly Rect GaragemDasAmbulancias = R(-15f, 11f, 0f, 24f), Entrada = R(24f, -4f, 32f, 0f);
        /// <summary>O topo da laje de cima de cada anexo (a garagem tem 4,2 m livres; a entrada, o pé-direito do térreo).</summary>
        public const float TopoDaGaragem = 4.6f, TopoDaEntrada = 3.8f, LajeDoAnexo = 0.4f;
        /// <summary>As 3 vagas (o meio de cada uma em z, no esboço) e os portões, um por vaga, na parede oeste da garagem.</summary>
        public static readonly float[] Vagas = { 13.5f, 17.5f, 21.5f };
        public const float LarguraDoPortao = 3.2f, AlturaDoPortao = 3.4f;
        public static bool Anexo(Comodo k) => k.tipo == "garagem" || k.tipo == "entrada";

        /// <summary>A marquise na frente da entrada (nas coordenadas da obra) e a altura do topo dela.</summary>
        public static readonly Rect Marquise = Rect.MinMaxRect(-7f, -27f, 7f, -20f);
        public const float TopoDaMarquise = 3.9f;

        /// <summary>O corpo do prédio (as paredes de fora), nas coordenadas da obra: o subsolo fica embaixo dele inteiro.</summary>
        public static readonly Rect Pegada = Rect.MinMaxRect(X0, Z0, X1, Z1);

        /// <summary>
        /// O terreno do hospital no mundo sorteado (a parte 7 do plano, 08/11/2026), nas coordenadas da obra: o prédio, a
        /// garagem com a rampa das ambulâncias (até x = -49,4, o pé dela, na calçada da rua do lado), a varanda com os
        /// degraus (até z = -28,9), as saídas do leste e do fundo com os degraus e uma beira de 1 m. O gerador procura um
        /// quarteirão inteiro onde ele caiba (TracadoDaCidade), e o chão fica plano nele.
        /// </summary>
        public static readonly Rect TerrenoNoMundo = Rect.MinMaxRect(-50.5f, -30f, 32f, 21.5f);

        // ------------------------------------------------------------ as janelas da fachada

        /// <summary>
        /// As janelas de fora ficam numa grade de 4 m (o meio de cada módulo), igual em todos os andares: a fachada sai
        /// alinhada. Um módulo só ganha a janela se ela cabe inteira num cômodo que aceita janela, longe das portas.
        /// </summary>
        public const float Modulo = 4f, LarguraDaJanela = 1.6f, Peitoril = 0.9f, TopoDaJanela = 2.6f;
        public const float LarguraDaJanelaPequena = 1.0f, PeitorilPequeno = 1.7f, TopoDaJanelaPequena = 2.4f;
        /// <summary>Os cômodos sem janela de fora (o subsolo inteiro também não tem).</summary>
        public static readonly HashSet<string> SemJanela = new HashSet<string>
        {
            "escada", "elevador", "raiox", "comando", "garagem", "camaras", "a01", "tecnica", "acesso",
        };

        // ------------------------------------------------------------ as zonas (as cores do blockout)

        /// <summary>As zonas das plantas do usuário: a circulação, a área médica, o serviço e o apoio, a administração e a área restrita.</summary>
        public enum Zona { Circulacao, Medica, Servico, Administracao, Restrita, Nucleo }

        /// <summary>O piso de cada zona no blockout (as cores das legendas das plantas).</summary>
        public static string PisoDaZona(Zona z) => z switch
        {
            Zona.Circulacao => "Piso",
            Zona.Medica => "ParedeAzul",
            Zona.Servico => "Concreto",
            Zona.Administracao => "ParedeBege",
            Zona.Restrita => "PlacaVermelha",
            _ => "ConcretoClaro",
        };

        /// <summary>A parede de dentro de cada zona no blockout.</summary>
        public static string ParedeDaZona(Zona z) => z switch
        {
            Zona.Servico => "Concreto",
            Zona.Restrita => "Concreto",
            Zona.Administracao => "ParedeBege",
            Zona.Nucleo => "ConcretoClaro",
            _ => "ParedeInterna",
        };

        // ------------------------------------------------------------ os acabamentos (a parte 3)

        /// <summary>
        /// O acabamento de cada cômodo (a parte 3, a arquitetura final): a circulação (o verde até 1 m com o bate-maca e o
        /// piso vinílico claro, o forro modular), a área médica (o verde-claro, a barra verde até 1,2 m e o piso vinílico), o
        /// azulejado (os banheiros, o centro cirúrgico, a autópsia, o necrotério, a copa e a lavanderia: o azulejo até 2,1 m
        /// com o rejunte, o piso de cerâmica), a administração (o bege, o lambri e o rodameio, o carpete ou o taco), o serviço
        /// (o concreto, a barra verde-escura com o friso amarelo, como no bunker), o isolamento (o branco com a faixa
        /// vermelha), a área restrita (o concreto com as faixas de aviso) e as escadas (a barra no patamar).
        /// </summary>
        public enum Acabamento { Circulacao, Medico, Azulejo, Administracao, Servico, Isolamento, Restrito, Escada }

        public static Acabamento AcabamentoDe(string tipo, Zona zona, int f)
        {
            switch (tipo)
            {
                case "escada": case "elevador": return Acabamento.Escada;
                case "banheiro": case "cirurgia": case "preparo": case "autopsia": case "necroterio": case "camaras":
                case "copa": case "limpeza": case "vestiario": case "lavanderia": case "residuos": return Acabamento.Azulejo;
                case "isolamento": case "quarto": return Acabamento.Isolamento;
                case "acesso": case "a01": case "tecnica": return Acabamento.Restrito;
                case "corredor": case "hall": return f == 0 ? Acabamento.Servico : Acabamento.Circulacao;
                case "recepcao": case "posto": case "espera": case "entrada": return Acabamento.Circulacao;
                case "descanso": return Acabamento.Administracao;
            }
            return zona switch
            {
                Zona.Medica => Acabamento.Medico,
                Zona.Administracao => Acabamento.Administracao,
                Zona.Restrita => Acabamento.Restrito,
                Zona.Circulacao => Acabamento.Circulacao,
                _ => Acabamento.Servico,
            };
        }

        /// <summary>A cor da parede de dentro, acima das barras.</summary>
        public static string PapelDe(Acabamento a, string tipo, int f) => a switch
        {
            Acabamento.Medico => "PapelVerde",
            Acabamento.Administracao or Acabamento.Isolamento => "Batente",
            Acabamento.Restrito => "Concreto",
            Acabamento.Escada => "ConcretoClaro",
            Acabamento.Servico => f == 0 || tipo == "garagem" ? "ConcretoClaro" : "ParedeInterna",
            _ => "ParedeInterna",
        };

        /// <summary>O piso de cada cômodo.</summary>
        public static string PisoDe(Acabamento a, string tipo) => tipo switch
        {
            "diretor" or "reuniao" or "secretaria" => "Taco",
            "escritorio" or "rh" or "planejamento" or "descanso" => "Carpete",
            "ti" => "PlacaBranca",
            _ => a switch
            {
                // (cores "de dentro" no abandono: o concreto do chão ganhava as folhas e as poças da calçada)
                Acabamento.Circulacao => "Teto",
                Acabamento.Azulejo => "Azulejo",
                Acabamento.Medico or Acabamento.Isolamento => "Azulejo",
                Acabamento.Administracao => "Piso",
                _ => "Concreto",
            },
        };

        /// <summary>Os cômodos com o forro modular (as placas de 0,6 × 1,2 m e as luminárias embutidas); nos outros, o teto liso com as calhas.</summary>
        public static bool ComForro(Acabamento a, string tipo) =>
            (a == Acabamento.Circulacao || a == Acabamento.Medico || a == Acabamento.Administracao || a == Acabamento.Isolamento) && tipo != "garagem";

        // ------------------------------------------------------------ as portas

        /// <summary>As portas: a de madeira, a de metal (a corta-fogo das escadas e as de serviço), a de vidro, a reforçada (o
        /// isolamento, o raio-X, a A-01), a do elevador, o portão da garagem e a de vai-e-vem (a dupla com o visor e a chapa de
        /// inox embaixo: a emergência, o centro cirúrgico, as enfermarias, a UTI, o necrotério).</summary>
        public enum TipoDePorta { Madeira, Metal, Vidro, Reforcada, Elevador, Portao, VaiEVem }
        public enum EstadoDaPorta { Aberta, Fechada, Bloqueada }

        /// <summary>
        /// Uma porta ou passagem: entre dois cômodos (b) ou para fora (b = null, com o lado). 'pos' é o meio dela ao longo da
        /// linha, nas coordenadas da obra. 'folhas': 0 é a passagem sem folha, 1 ou 2. 'aberto': sem parede nenhuma entre os
        /// dois (o saguão e os corredores).
        /// </summary>
        public sealed class Porta
        {
            public Comodo a, b;
            public float pos, largura, alto;
            public int folhas = 1;
            public bool aberto;
            public TipoDePorta tipo;
            public EstadoDaPorta estado;
            /// <summary>Só para fora: 0 a frente (-z), 1 o leste (+x), 2 o fundo (+z), 3 o oeste (-x).</summary>
            public int lado;
            /// <summary>A porta do elevador forçada: as duas folhas afastadas tanto (a do 2º andar, pela fresta se vê a cabine).</summary>
            public float fresta;

            public bool Fechada => estado != EstadoDaPorta.Aberta;
        }

        /// <summary>Uma janela de dentro (o vidro do comando do raio-X, as de observação do isolamento), como as portas.</summary>
        public sealed class JanelaDeDentro
        {
            public Comodo a, b;
            public float pos, largura, baixo, alto;
        }

        // ------------------------------------------------------------ os andares

        /// <summary>
        /// Um andar: os cômodos (com o núcleo e, no térreo, os anexos), o nome e a zona de cada um, os que ficam fechados de
        /// propósito (sem caminho: a área restrita A-01, as salas técnicas dela e o quarto lacrado do isolamento), as portas e
        /// as janelas de dentro.
        /// </summary>
        public sealed class Andar
        {
            public int f;
            public readonly List<Comodo> comodos = new List<Comodo>();
            public readonly Dictionary<Comodo, string> nomes = new Dictionary<Comodo, string>();
            public readonly Dictionary<Comodo, Zona> zonas = new Dictionary<Comodo, Zona>();
            public readonly HashSet<Comodo> fechados = new HashSet<Comodo>();
            /// <summary>Os pedaços do saguão em volta do elevador: na planta, sem o nome.</summary>
            public readonly HashSet<Comodo> semRotulo = new HashSet<Comodo>();
            public readonly List<Porta> portas = new List<Porta>();
            public readonly List<JanelaDeDentro> janelas = new List<JanelaDeDentro>();
            /// <summary>O nome de cada cômodo na placa em cima da porta (em inglês: o jogo se passa nos Estados Unidos).</summary>
            public readonly Dictionary<Comodo, string> placas = new Dictionary<Comodo, string>();

            public Comodo Em(float x, float z)
            {
                foreach (var k in comodos) if (k.Contem(x, z)) return k;
                return null;
            }

            public Acabamento AcabamentoDe(Comodo k) => PlantaDoHospital.AcabamentoDe(k.tipo, zonas[k], f);

            internal Comodo K(string tipo, string nome, Zona zona, float x0, float z0, float x1, float z1, bool rotulo = true, string placa = null)
            {
                var r = R(x0, z0, x1, z1);
                var ac = PlantaDoHospital.AcabamentoDe(tipo, zona, f);
                var k = new Comodo
                {
                    tipo = tipo, andar = f, x0 = r.xMin, z0 = r.yMin, x1 = r.xMax, z1 = r.yMax, papel = PapelDe(ac, tipo, f), piso = PisoDe(ac, tipo),
                };
                comodos.Add(k);
                nomes[k] = nome;
                zonas[k] = zona;
                if (!rotulo) semRotulo.Add(k);
                if (placa != null) placas[k] = placa;
                return k;
            }

            // a linha em comum de a e b corre em z (x constante) ou em x; pos vem do esboço
            internal static bool Vertical(Comodo a, Comodo b) => Mathf.Abs(a.x1 - b.x0) < 0.01f || Mathf.Abs(b.x1 - a.x0) < 0.01f;

            internal Porta P(Comodo a, Comodo b, float pos, int folhas = 1, TipoDePorta tipo = TipoDePorta.Madeira,
                             EstadoDaPorta estado = EstadoDaPorta.Aberta, float largura = 0f)
            {
                var p = new Porta { a = a, b = b, pos = pos + (Vertical(a, b) ? Z0 : X0), folhas = folhas, tipo = tipo, estado = estado, largura = largura };
                portas.Add(p);
                return p;
            }

            /// <summary>Sem parede entre os dois (o saguão, o posto e os corredores formam um espaço só).</summary>
            internal void Aberto(Comodo a, Comodo b) => portas.Add(new Porta { a = a, b = b, aberto = true, folhas = 0 });

            /// <summary>Uma porta para fora, no lado 'lado' do cômodo k (pos do esboço).</summary>
            internal Porta Fora(Comodo k, int lado, float pos, int folhas = 2, TipoDePorta tipo = TipoDePorta.Metal,
                                EstadoDaPorta estado = EstadoDaPorta.Aberta, float largura = 0f, float alto = 0f)
            {
                var p = new Porta
                {
                    a = k, lado = lado, pos = pos + (lado == 1 || lado == 3 ? Z0 : X0), folhas = folhas, tipo = tipo, estado = estado,
                    largura = largura, alto = alto,
                };
                portas.Add(p);
                return p;
            }

            internal void Janela(Comodo a, Comodo b, float de, float ate, float baixo, float alto) =>
                janelas.Add(new JanelaDeDentro { a = a, b = b, pos = (de + ate) / 2f + (Vertical(a, b) ? Z0 : X0), largura = ate - de, baixo = baixo, alto = alto });
        }

        // o núcleo de um andar: as escadas dos cantos do fundo, a de serviço (no subsolo e no térreo) e o elevador, com as
        // portas para o corredor do fundo (as das escadas, corta-fogo, de metal) e para o saguão (a do elevador, fechada:
        // ele está parado desde a evacuação)
        static void Nucleo(Andar a, Comodo corredor, Comodo saguao)
        {
            var oeste = a.K("escada", "Escada oeste", Zona.Nucleo, 0f, 24f, 4f, 32f, placa: "STAIR A");
            var leste = a.K("escada", "Escada leste", Zona.Nucleo, 52f, 24f, 56f, 32f, placa: "STAIR B");
            a.P(oeste, corredor, 2f, 1, TipoDePorta.Metal, largura: LarguraDaPortaDaEscada);
            a.P(leste, corredor, 54f, 1, TipoDePorta.Metal, largura: LarguraDaPortaDaEscada);
            if (a.f <= Terreo)
            {
                var servico = a.K("escada", "Escada de serviço", Zona.Nucleo, 18f, 24f, 22f, 32f, placa: "SERVICE STAIR");
                a.P(servico, corredor, 20f, 1, TipoDePorta.Metal, largura: LarguraDaPortaDaEscada);
            }
            var elevador = a.K("elevador", "Elevador", Zona.Nucleo, 26.5f, 17.5f, 29.5f, 21f);
            var porta = a.P(elevador, saguao, 28f, 2, TipoDePorta.Elevador, EstadoDaPorta.Fechada, LarguraDaPortaDoElevador);
            // no 2º andar, alguém forçou as folhas: pela fresta de 35 cm se vê a cabine, parada entre o 2º e o 3º
            if (a.f == 2) porta.fresta = 0.35f;
        }

        // o saguão em volta do elevador: a parte da frente (o nome que vai na planta) e os dois pedaços dos lados do poço,
        // abertos para ela e para os dois corredores
        static (Comodo frente, Comodo oeste, Comodo leste) Saguao(Andar a, string tipo, string nome, float x0, float x1, Comodo norte, Comodo sul)
        {
            var frente = a.K(tipo, nome, Zona.Circulacao, x0, 11f, x1, 17.5f);
            var oeste = a.K(tipo, nome, Zona.Circulacao, x0, 17.5f, 26.5f, 21f, rotulo: false);
            var leste = a.K(tipo, nome, Zona.Circulacao, 29.5f, 17.5f, x1, 21f, rotulo: false);
            a.Aberto(frente, oeste);
            a.Aberto(frente, leste);
            a.Aberto(oeste, norte);
            a.Aberto(leste, norte);
            a.Aberto(frente, sul);
            return (frente, oeste, leste);
        }

        /// <summary>O andar f, montado de novo (os cômodos são objetos novos a cada chamada).</summary>
        public static Andar DoAndar(int f)
        {
            var a = new Andar { f = f };
            var cn = a.K("corredor", "Corredor", Zona.Circulacao, 0f, 21f, 56f, 24f);
            var cs = a.K("corredor", f == 0 ? "Corredor de serviço" : "Corredor", Zona.Circulacao, 0f, 8f, 56f, 11f);
            switch (f)
            {
                case 0: Subsolo(a, cn, cs); break;
                case 1: Atendimento(a, cn, cs); break;
                case 2: Internacao(a, cn, cs); break;
                case 3: Administracao(a, cn, cs); break;
            }
            return a;
        }

        // o subsolo: a infraestrutura no fundo (a lavanderia, a manutenção, os geradores, a elétrica e os resíduos), o
        // necrotério no meio (a autópsia, o hall do elevador, o necrotério e as câmaras frias) e, na frente, a oficina, o
        // vestiário, a área restrita A-01 (o corredor curto até a porta reforçada, fechada, e as salas técnicas com as portas
        // bloqueadas: o que fica atrás é para depois, e pode crescer para baixo do pátio da frente), a casa de bombas e o
        // banheiro técnico
        static void Subsolo(Andar a, Comodo cn, Comodo cs)
        {
            var lavanderia = a.K("lavanderia", "Lavanderia", Zona.Servico, 4f, 24f, 12f, 32f, placa: "LAUNDRY");
            var manutencao = a.K("manutencao", "Manutenção", Zona.Servico, 12f, 24f, 18f, 32f, placa: "MAINTENANCE");
            var geradores = a.K("geradores", "Sala de geradores", Zona.Servico, 22f, 24f, 34f, 32f, placa: "GENERATORS");
            var eletrica = a.K("eletrica", "Sala elétrica", Zona.Servico, 34f, 24f, 42f, 32f, placa: "ELECTRICAL");
            var residuos = a.K("residuos", "Resíduos biológicos", Zona.Servico, 42f, 24f, 52f, 32f, placa: "BIOHAZARD WASTE");
            var deposito = a.K("deposito", "Depósito geral", Zona.Servico, 0f, 11f, 10f, 21f, placa: "CENTRAL STORAGE");
            var autopsia = a.K("autopsia", "Sala de autópsia", Zona.Medica, 10f, 11f, 22f, 21f, placa: "AUTOPSY");
            var hall = Saguao(a, "hall", "Hall do necrotério", 22f, 34f, cn, cs).frente;
            var necroterio = a.K("necroterio", "Necrotério", Zona.Medica, 34f, 11f, 46f, 21f, placa: "MORGUE");
            var camaras = a.K("camaras", "Câmaras frias", Zona.Medica, 46f, 11f, 56f, 21f, placa: "COLD STORAGE");
            var oficina = a.K("oficina", "Oficina", Zona.Servico, 0f, 0f, 12f, 8f, placa: "WORKSHOP");
            var vestiario = a.K("vestiario", "Vestiário", Zona.Servico, 12f, 0f, 18f, 8f, placa: "LOCKER ROOM");
            var tecnicaO = a.K("tecnica", "Sala técnica A-01", Zona.Restrita, 18f, 4f, 26.5f, 8f, placa: "TECHNICAL");
            var acesso = a.K("acesso", "Acesso A-01", Zona.Restrita, 26.5f, 4f, 29.5f, 8f);
            var tecnicaL = a.K("tecnica", "Sala técnica A-01", Zona.Restrita, 29.5f, 4f, 38f, 8f, placa: "TECHNICAL");
            var a01 = a.K("a01", "Área restrita A-01", Zona.Restrita, 18f, 0f, 38f, 4f, placa: "A-01  RESTRICTED AREA");
            var bombas = a.K("bombas", "Casa de bombas", Zona.Servico, 38f, 0f, 48f, 8f, placa: "PUMP ROOM");
            var banheiro = a.K("banheiro", "Banheiro técnico", Zona.Servico, 48f, 0f, 56f, 8f, placa: "RESTROOM");
            Nucleo(a, cn, hall);
            a.fechados.Add(tecnicaO);
            a.fechados.Add(tecnicaL);
            a.fechados.Add(a01);

            a.P(lavanderia, cn, 8f, 2, TipoDePorta.VaiEVem);
            a.P(manutencao, cn, 15f);
            a.P(geradores, cn, 28f, 2, TipoDePorta.Metal);
            a.P(eletrica, cn, 38f, 1, TipoDePorta.Metal);
            a.P(residuos, cn, 47f, 2, TipoDePorta.Metal);
            a.P(deposito, cn, 5f, 2, TipoDePorta.Metal);
            a.P(deposito, cs, 5f, 2, TipoDePorta.Metal);
            a.P(autopsia, cs, 16f, 2, TipoDePorta.VaiEVem);
            a.P(autopsia, hall, 14.25f, 2, TipoDePorta.VaiEVem);
            a.P(necroterio, hall, 14.25f, 2, TipoDePorta.VaiEVem);
            a.P(necroterio, camaras, 16f, 2, TipoDePorta.Metal);
            a.P(necroterio, cs, 40f);
            a.P(oficina, cs, 6f, 2, TipoDePorta.Metal);
            a.P(vestiario, cs, 15f);
            a.P(acesso, cs, 28f, 0, largura: 2.4f);
            a.P(tecnicaO, acesso, 6f, 1, TipoDePorta.Metal, EstadoDaPorta.Bloqueada);
            a.P(tecnicaL, acesso, 6f, 1, TipoDePorta.Metal, EstadoDaPorta.Bloqueada);
            a.P(a01, acesso, 28f, 2, TipoDePorta.Reforcada, EstadoDaPorta.Fechada, 2.0f);
            a.P(bombas, cs, 43f, 1, TipoDePorta.Metal);
            a.P(banheiro, cs, 52f);
        }

        // o térreo: a emergência a oeste (o recebimento das ambulâncias, a emergência, as triagens entre ela e a recepção, a
        // sala vermelha e a medicação do outro lado do corredor do fundo, a observação do outro lado do corredor da frente),
        // a recepção e a sala de espera no meio, os consultórios, o raio-X e o laboratório a leste, o serviço no fundo (o
        // depósito, a limpeza, a escada de serviço para o subsolo e a saída de serviço) e os banheiros na frente, a leste
        static void Atendimento(Andar a, Comodo cn, Comodo cs)
        {
            var vermelha = a.K("vermelha", "Sala Vermelha", Zona.Medica, 4f, 24f, 13f, 32f, placa: "TRAUMA");
            var medicacao = a.K("medicacao", "Medicação", Zona.Medica, 13f, 24f, 18f, 32f, placa: "MEDICATION");
            var deposito = a.K("deposito", "Depósito de suprimentos", Zona.Servico, 22f, 24f, 30f, 32f, placa: "SUPPLY");
            var limpeza = a.K("limpeza", "Limpeza", Zona.Servico, 30f, 24f, 34f, 32f, placa: "JANITOR");
            var servico = a.K("servico", "Saída de serviço", Zona.Servico, 34f, 24f, 38f, 32f);
            var raiox = a.K("raiox", "Raio-X", Zona.Medica, 38f, 24f, 48f, 32f, placa: "X-RAY");
            var comando = a.K("comando", "Comando do raio-X", Zona.Medica, 48f, 24f, 52f, 32f, placa: "X-RAY CONTROL");
            var recebimento = a.K("recebimento", "Recebimento de ambulâncias", Zona.Medica, 0f, 11f, 5f, 21f, placa: "EMERGENCY");
            var emergencia = a.K("emergencia", "Emergência", Zona.Medica, 5f, 11f, 16f, 21f, placa: "EMERGENCY");
            var triagem1 = a.K("triagem", "Triagem 1", Zona.Medica, 16f, 16f, 20f, 21f, placa: "TRIAGE 1");
            var triagem2 = a.K("triagem", "Triagem 2", Zona.Medica, 16f, 11f, 20f, 16f, placa: "TRIAGE 2");
            var espera = a.K("espera", "Sala de espera", Zona.Circulacao, 18f, 0f, 38f, 8f);
            var (recepcao, saguaoO, saguaoL) = Saguao(a, "recepcao", "Recepção", 20f, 36f, cn, cs);
            var consultorio1 = a.K("consultorio", "Consultório 1", Zona.Medica, 36f, 16f, 41f, 21f, placa: "EXAM 1");
            var consultorio2 = a.K("consultorio", "Consultório 2", Zona.Medica, 36f, 11f, 41f, 16f, placa: "EXAM 2");
            var laboratorio = a.K("laboratorio", "Laboratório", Zona.Medica, 41f, 11f, 56f, 21f, placa: "LABORATORY");
            var seguranca = a.K("seguranca", "Segurança", Zona.Administracao, 0f, 0f, 7f, 8f, placa: "SECURITY");
            var observacao = a.K("observacao", "Observação", Zona.Medica, 7f, 0f, 18f, 8f, placa: "OBSERVATION");
            var coleta = a.K("coleta", "Coleta", Zona.Medica, 38f, 0f, 46f, 8f, placa: "BLOOD DRAW");
            var banheiroF = a.K("banheiro", "Banheiro feminino", Zona.Servico, 46f, 0f, 51f, 8f, placa: "WOMEN");
            var banheiroM = a.K("banheiro", "Banheiro masculino", Zona.Servico, 51f, 0f, 56f, 8f, placa: "MEN");
            var garagem = a.K("garagem", "Garagem de ambulâncias", Zona.Servico, -15f, 11f, 0f, 24f, placa: "AMBULANCE BAY");
            var entrada = a.K("entrada", "Entrada principal", Zona.Circulacao, 24f, -4f, 32f, 0f);
            Nucleo(a, cn, recepcao);

            a.P(vermelha, cn, 8.5f, 2, TipoDePorta.VaiEVem);
            a.P(medicacao, cn, 15.5f);
            a.P(deposito, cn, 26f, 2, TipoDePorta.Metal);
            a.P(limpeza, cn, 32f);
            a.P(servico, cn, 36f, 0, largura: 2.0f);
            a.P(raiox, cn, 42f, 2, TipoDePorta.Reforcada);
            a.P(comando, cn, 50f);
            a.Janela(raiox, comando, 25.5f, 30.5f, 1.0f, 2.1f);
            a.P(recebimento, garagem, 16f, 2, TipoDePorta.VaiEVem, largura: 2.4f);
            a.P(recebimento, emergencia, 16f, 0, largura: 2.6f);
            a.P(emergencia, cn, 10.5f, 2, TipoDePorta.VaiEVem);
            a.P(emergencia, cs, 10.5f, 2, TipoDePorta.VaiEVem);
            a.P(emergencia, triagem1, 18.5f);
            a.P(emergencia, triagem2, 13.5f);
            a.P(triagem1, saguaoO, 19.25f);
            a.P(triagem2, recepcao, 13.5f);
            a.Aberto(espera, cs);
            a.P(consultorio1, saguaoL, 19.25f);
            a.P(consultorio2, recepcao, 13.5f);
            a.P(laboratorio, cn, 48f, 2, TipoDePorta.VaiEVem);
            a.P(laboratorio, cs, 44f);
            a.P(seguranca, cs, 3.5f, 1, TipoDePorta.Metal);
            a.P(observacao, cs, 10.5f, 2, TipoDePorta.VaiEVem);
            a.P(coleta, cs, 42f);
            a.P(banheiroF, cs, 48.5f);
            a.P(banheiroM, cs, 53.5f);
            a.P(entrada, espera, 28f, 2, TipoDePorta.Vidro, largura: 3.0f);

            // as portas de fora: a da frente (de vidro), as duas saídas de emergência na ponta do corredor da frente (a oeste,
            // barricada), a de serviço no fundo e os portões da garagem (um aberto)
            a.Fora(entrada, 0, 28f, 2, TipoDePorta.Vidro, largura: 3.0f);
            a.Fora(cs, 3, 9.5f, 2, TipoDePorta.Metal, EstadoDaPorta.Bloqueada, 1.8f);
            a.Fora(cs, 1, 9.5f, 2, TipoDePorta.Metal, largura: 1.8f);
            a.Fora(servico, 2, 36f, 2, TipoDePorta.Metal, largura: 2.0f);
            for (int i = 0; i < Vagas.Length; i++)
                a.Fora(garagem, 3, Vagas[i], 0, TipoDePorta.Portao, i == 0 ? EstadoDaPorta.Aberta : EstadoDaPorta.Fechada, LarguraDoPortao, AlturaDoPortao);
        }

        // o 2º andar: no fundo, os consultórios, a UTI e o centro cirúrgico (o preparo, a sala principal e a menor); no meio,
        // as enfermarias A e B dos lados do posto de enfermagem (no saguão, de frente para o elevador); na frente, o depósito
        // médico, a ala de isolamento (o corredor de dentro, atrás da barreira reforçada, e os 4 quartos; a outra porta da ala
        // bloqueada e o quarto 4 lacrado), o descanso da equipe e os banheiros
        static void Internacao(Andar a, Comodo cn, Comodo cs)
        {
            var consultorio3 = a.K("consultorio", "Consultório 3", Zona.Medica, 4f, 24f, 9f, 32f, placa: "EXAM 3");
            var consultorio4 = a.K("consultorio", "Consultório 4", Zona.Medica, 9f, 24f, 14f, 32f, placa: "EXAM 4");
            var uti = a.K("uti", "UTI", Zona.Medica, 14f, 24f, 30f, 32f, placa: "ICU");
            var preparo = a.K("preparo", "Preparo cirúrgico", Zona.Medica, 30f, 24f, 35f, 32f, placa: "PRE-OP");
            var cirurgia1 = a.K("cirurgia", "Sala de cirurgia 1", Zona.Medica, 35f, 24f, 47f, 32f, placa: "OR 1");
            var cirurgia2 = a.K("cirurgia", "Sala de cirurgia 2", Zona.Medica, 47f, 24f, 52f, 32f, placa: "OR 2");
            var enfermariaA = a.K("enfermaria", "Enfermaria A", Zona.Medica, 0f, 11f, 16f, 21f, placa: "WARD A");
            var posto = Saguao(a, "posto", "Posto de enfermagem", 16f, 40f, cn, cs).frente;
            var enfermariaB = a.K("enfermaria", "Enfermaria B", Zona.Medica, 40f, 11f, 56f, 21f, placa: "WARD B");
            var depositoMedico = a.K("deposito", "Depósito médico", Zona.Servico, 0f, 0f, 8f, 8f, placa: "MEDICAL SUPPLY");
            var isolamento = a.K("isolamento", "Corredor do isolamento", Zona.Restrita, 8f, 5.5f, 36f, 8f, placa: "ISOLATION");
            var quartos = new Comodo[4];
            for (int i = 0; i < 4; i++)
                quartos[i] = a.K("quarto", $"Isolamento {i + 1}", Zona.Restrita, 8f + 7f * i, 0f, 15f + 7f * i, 5.5f, placa: $"ISO {i + 1}");
            var descanso = a.K("descanso", "Descanso da equipe", Zona.Servico, 36f, 0f, 46f, 8f, placa: "STAFF LOUNGE");
            var banheiroF = a.K("banheiro", "Banheiro feminino", Zona.Servico, 46f, 0f, 51f, 8f, placa: "WOMEN");
            var banheiroM = a.K("banheiro", "Banheiro masculino", Zona.Servico, 51f, 0f, 56f, 8f, placa: "MEN");
            Nucleo(a, cn, posto);
            a.fechados.Add(quartos[3]);

            a.P(consultorio3, cn, 6.5f);
            a.P(consultorio4, cn, 11.5f);
            a.P(uti, cn, 18f, 2, TipoDePorta.VaiEVem);
            a.P(uti, cn, 26f, 2, TipoDePorta.VaiEVem);
            a.P(preparo, cn, 32.5f);
            a.P(preparo, cirurgia1, 28f, 1, TipoDePorta.VaiEVem);
            a.P(cirurgia1, cn, 41f, 2, TipoDePorta.VaiEVem);
            a.P(cirurgia1, cirurgia2, 28f, 1, TipoDePorta.VaiEVem);
            a.P(cirurgia2, cn, 49.5f, 2, TipoDePorta.VaiEVem);
            a.P(enfermariaA, cn, 8f, 2, TipoDePorta.VaiEVem);
            a.P(enfermariaA, posto, 14.25f, 2, TipoDePorta.VaiEVem);
            a.P(enfermariaB, cn, 48f, 2, TipoDePorta.VaiEVem);
            a.P(enfermariaB, posto, 14.25f, 2, TipoDePorta.VaiEVem);
            a.P(depositoMedico, cs, 4f, 2);
            a.P(isolamento, cs, 22f, 2, TipoDePorta.Reforcada);
            a.P(isolamento, cs, 33f, 2, TipoDePorta.Reforcada, EstadoDaPorta.Bloqueada);
            for (int i = 0; i < 4; i++)
            {
                float meio = 11.5f + 7f * i;
                a.P(quartos[i], isolamento, meio - 1.6f, 1, TipoDePorta.Reforcada, i == 3 ? EstadoDaPorta.Fechada : EstadoDaPorta.Aberta);
                a.Janela(isolamento, quartos[i], meio + 0.2f, meio + 1.6f, 1.0f, 1.9f);
            }
            a.P(descanso, cs, 41f);
            a.P(banheiroF, cs, 48.5f);
            a.P(banheiroM, cs, 53.5f);
        }

        // o 3º andar: no fundo, a TI, a copa, a sala de reunião e o arquivo; no meio, os escritórios administrativos A e B dos
        // lados do hall de espera; na frente, a segurança, o RH, o planejamento, a sala do diretor (no meio da fachada, com a
        // porta dupla para o corredor e a porta da secretaria), a secretaria e os banheiros
        static void Administracao(Andar a, Comodo cn, Comodo cs)
        {
            var ti = a.K("ti", "TI e servidores", Zona.Administracao, 4f, 24f, 10f, 32f, placa: "IT");
            var copa = a.K("copa", "Copa", Zona.Administracao, 10f, 24f, 18f, 32f, placa: "BREAK ROOM");
            var reuniao = a.K("reuniao", "Sala de reunião", Zona.Administracao, 18f, 24f, 32f, 32f, placa: "CONFERENCE");
            var arquivo = a.K("arquivo", "Arquivo", Zona.Administracao, 32f, 24f, 52f, 32f, placa: "RECORDS");
            var escritorioA = a.K("escritorio", "Escritório A", Zona.Administracao, 0f, 11f, 20f, 21f, placa: "OFFICES A");
            var espera = Saguao(a, "espera", "Hall de espera", 20f, 36f, cn, cs).frente;
            var escritorioB = a.K("escritorio", "Escritório B", Zona.Administracao, 36f, 11f, 56f, 21f, placa: "OFFICES B");
            var seguranca = a.K("seguranca", "Segurança e monitoramento", Zona.Administracao, 0f, 0f, 8f, 8f, placa: "SECURITY");
            var rh = a.K("rh", "RH", Zona.Administracao, 8f, 0f, 14f, 8f, placa: "HUMAN RESOURCES");
            var planejamento = a.K("planejamento", "Sala de planejamento", Zona.Administracao, 14f, 0f, 22f, 8f, placa: "PLANNING");
            var diretor = a.K("diretor", "Sala do diretor", Zona.Administracao, 22f, 0f, 34f, 8f, placa: "DIRECTOR");
            var secretaria = a.K("secretaria", "Secretaria", Zona.Administracao, 34f, 0f, 46f, 8f, placa: "SECRETARY");
            var banheiroF = a.K("banheiro", "Banheiro feminino", Zona.Servico, 46f, 0f, 51f, 8f, placa: "WOMEN");
            var banheiroM = a.K("banheiro", "Banheiro masculino", Zona.Servico, 51f, 0f, 56f, 8f, placa: "MEN");
            Nucleo(a, cn, espera);

            a.P(ti, cn, 7f, 1, TipoDePorta.Metal);
            a.P(copa, cn, 14f);
            a.P(reuniao, cn, 25f, 2);
            a.P(arquivo, cn, 36f);
            a.P(arquivo, cn, 48f);
            a.P(escritorioA, cn, 10f);
            a.P(escritorioA, cs, 10f);
            a.P(escritorioA, espera, 14.25f, 2, TipoDePorta.Vidro);
            a.P(escritorioB, cn, 46f);
            a.P(escritorioB, cs, 46f);
            a.P(escritorioB, espera, 14.25f, 2, TipoDePorta.Vidro);
            a.P(seguranca, cs, 4f, 1, TipoDePorta.Metal);
            a.P(rh, cs, 11f);
            a.P(planejamento, cs, 18f);
            a.P(diretor, cs, 28f, 2, TipoDePorta.Madeira);
            a.P(diretor, secretaria, 4f);
            a.P(secretaria, cs, 40f, 2, TipoDePorta.Vidro);
            a.P(banheiroF, cs, 48.5f);
            a.P(banheiroM, cs, 53.5f);
        }

        // ------------------------------------------------------------ as linhas das paredes

        /// <summary>A linha em comum de dois cômodos vizinhos: 'a' é o de -x (linha em z) ou o de -z (linha em x).</summary>
        public struct Divisa
        {
            public bool vertical;
            public float c, lo, hi;
            public Comodo a, b;
        }

        /// <summary>Um pedaço da borda de um cômodo em que nenhum outro encosta: a parede de fora. 'fora' é +1 ou -1 (o lado de fora em x ou z).</summary>
        public struct Borda
        {
            public bool vertical;
            public float c, lo, hi;
            public int fora;
            public Comodo k;
        }

        /// <summary>As divisas (entre dois cômodos) e as bordas (as de fora) de um andar.</summary>
        public static void Linhas(Andar an, List<Divisa> divisas, List<Borda> bordas)
        {
            divisas.Clear();
            bordas.Clear();
            var ks = an.comodos;
            void Tentar(Comodo A, Comodo B)
            {
                if (Mathf.Abs(A.x1 - B.x0) < 0.01f)
                {
                    float lo = Mathf.Max(A.z0, B.z0), hi = Mathf.Min(A.z1, B.z1);
                    if (hi - lo > 0.05f) divisas.Add(new Divisa { vertical = true, c = A.x1, lo = lo, hi = hi, a = A, b = B });
                }
                if (Mathf.Abs(A.z1 - B.z0) < 0.01f)
                {
                    float lo = Mathf.Max(A.x0, B.x0), hi = Mathf.Min(A.x1, B.x1);
                    if (hi - lo > 0.05f) divisas.Add(new Divisa { vertical = false, c = A.z1, lo = lo, hi = hi, a = A, b = B });
                }
            }
            for (int i = 0; i < ks.Count; i++)
                for (int j = 0; j < ks.Count; j++)
                    if (i != j) Tentar(ks[i], ks[j]);

            var livres = new List<Vector2>();
            foreach (var k in ks)
            {
                for (int lado = 0; lado < 4; lado++)
                {
                    bool vertical = lado == 1 || lado == 3;
                    float c = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
                    int fora = lado == 0 || lado == 3 ? -1 : 1;
                    livres.Clear();
                    livres.Add(vertical ? new Vector2(k.z0, k.z1) : new Vector2(k.x0, k.x1));
                    foreach (var d in divisas)
                    {
                        if (d.vertical != vertical || Mathf.Abs(d.c - c) > 0.01f) continue;
                        // o vizinho fica do lado de fora desta borda
                        if (!(fora < 0 ? d.b == k : d.a == k)) continue;
                        for (int t = livres.Count - 1; t >= 0; t--)
                        {
                            var v = livres[t];
                            if (d.hi <= v.x + 0.001f || d.lo >= v.y - 0.001f) continue;
                            livres.RemoveAt(t);
                            if (d.lo > v.x + 0.05f) livres.Add(new Vector2(v.x, d.lo));
                            if (d.hi < v.y - 0.05f) livres.Add(new Vector2(d.hi, v.y));
                        }
                    }
                    foreach (var v in livres)
                        if (v.y - v.x > 0.05f) bordas.Add(new Borda { vertical = vertical, c = c, lo = v.x, hi = v.y, fora = fora, k = k });
                }
            }
        }

        /// <summary>A porta (ou o vão aberto) entre dois cômodos, se houver.</summary>
        public static IEnumerable<Porta> PortasEntre(Andar an, Comodo x, Comodo y)
        {
            foreach (var p in an.portas)
                if (p.b != null && ((p.a == x && p.b == y) || (p.a == y && p.b == x))) yield return p;
        }
    }
}
