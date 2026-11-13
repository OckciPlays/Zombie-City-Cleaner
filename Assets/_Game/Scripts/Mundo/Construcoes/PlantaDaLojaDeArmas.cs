using System.Collections.Generic;
using UnityEngine;
using Andar = ZombieCityCleaner.Mundo.PlantaDoHospital.Andar;
using Porta = ZombieCityCleaner.Mundo.PlantaDoHospital.Porta;
using Divisa = ZombieCityCleaner.Mundo.PlantaDoHospital.Divisa;
using Borda = ZombieCityCleaner.Mundo.PlantaDoHospital.Borda;
using TipoDePorta = ZombieCityCleaner.Mundo.PlantaDoHospital.TipoDePorta;
using Zona = ZombieCityCleaner.Mundo.PlantaDoHospital.Zona;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A planta da loja de armas (o Plano_LojaDeArmas.md da pasta de arte, 08/11/2026): o corpo de 24 × 30 m, de um pavimento
    /// (o pé-direito de 3,8 m), com a área de vendas na frente (15,6 × 22 m), o fundo atrás dela (a sala segura, o estoque e
    /// o escritório) e a ala do estande de tiro do lado leste (a antessala, o banheiro e o depósito do estande na frente, e o
    /// estande, de 8,4 × 23 m, com as 5 baias perto da frente e os corredores de tiro indo para o fundo).
    ///
    /// Os cômodos são escritos como no esboço (x de 0 a 24, de oeste para leste; z de 0, a frente, a 30, o fundo) e passados
    /// para as coordenadas da obra (o meio no centro, a frente em -z, o chão em y = 0) por <see cref="R"/>. A planta usa o
    /// <see cref="PlantaDoHospital.Andar"/> (os cômodos, as portas e as linhas das paredes, <see cref="PlantaDoHospital.Linhas"/>),
    /// sem os andares. A variação de cada loja (o estado da sala segura, a barricada, o acampamento, os balcões quebrados...)
    /// sai da semente (<see cref="Sortear"/>).
    /// </summary>
    public static class PlantaDaLojaDeArmas
    {
        // ------------------------------------------------------------ o corpo

        /// <summary>As linhas das paredes de fora: x de -12 a 12, z de -15 (a frente) a 15 (o fundo).</summary>
        public const float X0 = -12f, X1 = 12f, Z0 = -15f, Z1 = 15f;
        /// <summary>O pé-direito (o chão a 0 e o teto a 3,8 m), a laje da cobertura e a platibanda em volta dela.</summary>
        public const float PeDireito = 3.8f, GrossuraDaCobertura = 0.5f, Platibanda = 0.9f;
        public const float TopoDaCobertura = PeDireito + GrossuraDaCobertura;
        /// <summary>A fachada falsa da frente: a parede sobe até 5,9 m em toda a largura e, no meio da área de vendas, até 6,9 m.</summary>
        public const float AltoDaFachada = 5.9f, AltoDoFrontao = 6.9f;
        /// <summary>O chão em volta (o estacionamento, os lados e o beco) fica 15 cm abaixo; a calçada da frente, no nível da porta.</summary>
        public const float ChaoDeFora = -0.15f, Calcada = 2.5f;

        public static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0 + X0, z0 + Z0, x1 + X0, z1 + Z0);
        public static float X(float x) => x + X0;
        public static float Z(float z) => z + Z0;

        public static readonly Rect Vendas = R(0f, 0f, 15.6f, 22f), SalaSegura = R(0f, 22f, 6.4f, 30f), Estoque = R(6.4f, 22f, 11.4f, 30f),
                                    Escritorio = R(11.4f, 22f, 15.6f, 30f), Antessala = R(15.6f, 0f, 20.6f, 7f), Banheiro = R(20.6f, 0f, 24f, 3.6f),
                                    DepositoDoEstande = R(20.6f, 3.6f, 24f, 7f), Estande = R(15.6f, 7f, 24f, 30f);
        public static readonly Rect Pegada = Rect.MinMaxRect(X0, Z0, X1, Z1);

        /// <summary>
        /// O terreno da loja (nas coordenadas da obra): o prédio, a calçada da frente, o estacionamento (12 m na frente), o
        /// beco do lado oeste e o do leste (a saída de emergência do estande) e o beco dos fundos.
        /// </summary>
        public static readonly Rect Terreno = Rect.MinMaxRect(X0 - 4f, Z0 - Calcada - 12f, X1 + 4f, Z1 + 5f);

        // ------------------------------------------------------------ o estande

        /// <summary>
        /// As baias: 5, de 1,40 m, encostadas umas nas outras a partir da parede leste (a face dos painéis acústicos); na
        /// oeste fica a passagem de 1,08 m até os corredores de tiro (com o portãozinho aberto): de parede a parede, as
        /// bancadas fechariam a linha de tiro, e ninguém chegaria ao fundo do estande.
        /// </summary>
        public const int Baias = 5;
        public const float LarguraDaBaia = 1.40f;
        /// <summary>A face dos painéis acústicos das paredes do estande (a linha mais meia parede e o painel).</summary>
        public const float FaceDoPainel = 0.156f;
        /// <summary>O z da linha de tiro (a bancada das baias) e onde os corredores acabam no anteparo do fundo.</summary>
        public static readonly float LinhaDeTiro = Z(10.6f), ComecoDoAnteparo = Z(27.2f);
        /// <summary>O meio da baia i (de 0, a do oeste, a 4) em x.</summary>
        public static float MeioDaBaia(int i) => Estande.xMax - FaceDoPainel - LarguraDaBaia * (Baias - i - 0.5f);
        /// <summary>A passagem da parede oeste até os corredores de tiro: de x a x.</summary>
        public static float ComecoDaPassagem => Estande.xMin + FaceDoPainel;
        public static float FimDaPassagem => MeioDaBaia(0) - LarguraDaBaia / 2f;

        // ------------------------------------------------------------ as portas

        /// <summary>A porta de cofre: no meio do vão da parede entre o estoque e a sala segura (no chão), a folha abrindo para o estoque.</summary>
        public static readonly Vector3 PortaDoCofre = new Vector3(SalaSegura.xMax, 0f, Z(26.4f));
        /// <summary>A largura e a altura do vão da porta de cofre (as da porta do kit, ×1,15: o modelo é feito para elas).</summary>
        public const float VaoDoCofre = 1.265f, AltoDoCofre = 2.415f;
        /// <summary>A entrada principal: a porta dupla de vidro no meio da frente da área de vendas.</summary>
        public static readonly float MeioDaEntrada = X(7.8f);
        public const float LarguraDaEntrada = 2.2f, AltoDaEntrada = 2.6f;

        // ------------------------------------------------------------ as janelas de fora

        /// <summary>Uma janela de fora: o lado da parede (0 a frente, 1 o leste, 2 o fundo, 3 o oeste), de 'de' a 'ate' ao longo dela (x ou z da obra), o peitoril e o alto.</summary>
        public struct JanelaDeFora
        {
            public int lado;
            public float de, ate, baixo, alto;
            /// <summary>A vitrine da frente (com a grade de barras por fora).</summary>
            public bool vitrine;
            public JanelaDeFora(int lado, float de, float ate, float baixo, float alto, bool vitrine = false)
            {
                this.lado = lado; this.de = de; this.ate = ate; this.baixo = baixo; this.alto = alto; this.vitrine = vitrine;
            }
        }

        /// <summary>
        /// As janelas de fora: as 4 vitrines da área de vendas (duas de cada lado da entrada, com as grades), a da antessala
        /// (também com a grade), a janelinha alta do banheiro, as duas altas da parede oeste (em cima das armas longas) e a do
        /// escritório nos fundos. O estande, a sala segura, o estoque e o depósito não têm janela.
        /// </summary>
        public static readonly JanelaDeFora[] Janelas =
        {
            new JanelaDeFora(0, X(0.7f), X(3.5f), 0.75f, 2.9f, true), new JanelaDeFora(0, X(3.7f), X(6.5f), 0.75f, 2.9f, true),
            new JanelaDeFora(0, X(9.1f), X(11.9f), 0.75f, 2.9f, true), new JanelaDeFora(0, X(12.1f), X(14.9f), 0.75f, 2.9f, true),
            new JanelaDeFora(0, X(16.5f), X(19.7f), 0.9f, 2.6f, true),
            new JanelaDeFora(0, X(21.8f), X(22.9f), 1.9f, 2.5f),
            new JanelaDeFora(3, Z(6.5f), Z(8.9f), 2.65f, 3.4f), new JanelaDeFora(3, Z(13.5f), Z(15.9f), 2.65f, 3.4f),
            new JanelaDeFora(2, X(12.7f), X(14.3f), 1.6f, 2.6f),
        };

        // ------------------------------------------------------------ a variação

        /// <summary>O estado da sala segura: A, saqueada (a porta arrombada e aberta); B, parcialmente saqueada (a porta
        /// entreaberta, umas caixas e um cofre fechados); C, intacta (a porta trancada; o dono lá dentro).</summary>
        public enum EstadoDaSala { Saqueada, Parcial, Intacta }

        public static readonly string[] NomesDosEstados = { "A, saqueada", "B, parcialmente saqueada", "C, intacta" };

        /// <summary>O que muda de uma loja para a outra (a mesma construção, outra semente).</summary>
        public struct Variacao
        {
            public int semente;
            public EstadoDaSala sala;
            /// <summary>A barricada velha perto da entrada.</summary>
            public bool barricada;
            /// <summary>O acampamento dos sobreviventes no estande (o colchonete, as latas, as caixas).</summary>
            public bool acampamento;
            /// <summary>Uma das gôndolas derrubada no saque.</summary>
            public bool gondolaTombada;
            /// <summary>A baia de tiro quebrada (de 0 a 4; -1, nenhuma).</summary>
            public int baiaQuebrada;
            /// <summary>Quantos balcões de vidro quebrados (de 9 no L).</summary>
            public int vitrinesQuebradas;
            /// <summary>O quanto de sangue e de bagunça (0 a 1).</summary>
            public float sangue;
            /// <summary>As vitrines da frente com tábuas (0 a 1: a fração).</summary>
            public float tabuas;
        }

        /// <summary>
        /// A variação da semente: a sala segura A em 55% das lojas, B em 30% e C em 15% (ou a pedida), a barricada em 60%, o
        /// acampamento em 50%, a gôndola tombada em 55%, uma baia quebrada em 60%, de 2 a 6 balcões quebrados e o resto.
        /// </summary>
        public static Variacao Sortear(int semente, EstadoDaSala? sala = null)
        {
            var r = new System.Random(semente * 7919 + 101);
            double s = r.NextDouble();
            var v = new Variacao
            {
                semente = semente,
                sala = sala ?? (s < 0.55 ? EstadoDaSala.Saqueada : s < 0.85 ? EstadoDaSala.Parcial : EstadoDaSala.Intacta),
                barricada = r.NextDouble() < 0.6,
                acampamento = r.NextDouble() < 0.5,
                gondolaTombada = r.NextDouble() < 0.55,
                baiaQuebrada = r.NextDouble() < 0.6 ? r.Next(Baias) : -1,
                vitrinesQuebradas = 2 + r.Next(5),
                sangue = 0.4f + 0.6f * (float)r.NextDouble(),
                tabuas = 0.12f + 0.3f * (float)r.NextDouble(),
            };
            return v;
        }

        // ------------------------------------------------------------ os cômodos e as portas

        /// <summary>
        /// A planta (um <see cref="PlantaDoHospital.Andar"/> só, o 0): os 8 cômodos, com o nome, a zona, o papel da parede, o
        /// piso e a placa em inglês, e as portas: a entrada (a dupla de vidro), as de dentro, a porta de cofre (entre o estoque
        /// e a sala segura: o modelo vai no vão), a dos fundos do estoque e a saída de emergência do estande.
        /// </summary>
        public static Andar Planta()
        {
            var a = new Andar { f = 0 };
            Comodo K(string tipo, string nome, Zona zona, Rect r, string papel, string piso, string placa = null)
            {
                var k = new Comodo { tipo = tipo, andar = 0, x0 = r.xMin, z0 = r.yMin, x1 = r.xMax, z1 = r.yMax, papel = papel, piso = piso };
                a.comodos.Add(k);
                a.nomes[k] = nome;
                a.zonas[k] = zona;
                if (placa != null) a.placas[k] = placa;
                return k;
            }
            var vendas = K("vendas", "Área de vendas", Zona.Circulacao, Vendas, "ParedeBege", "Piso");
            var segura = K("segura", "Sala segura", Zona.Restrita, SalaSegura, "Concreto", "Concreto", "VAULT");
            var estoque = K("estoque", "Estoque", Zona.Servico, Estoque, "ConcretoClaro", "Concreto", "EMPLOYEES ONLY");
            var escritorio = K("escritorio", "Escritório", Zona.Administracao, Escritorio, "ParedeBege", "Carpete", "OFFICE");
            var antessala = K("antessala", "Antessala do estande", Zona.Circulacao, Antessala, "ParedeInterna", "Piso", "SHOOTING RANGE");
            var banheiro = K("banheiro", "Banheiro", Zona.Servico, Banheiro, "ParedeInterna", "Azulejo", "RESTROOM");
            var deposito = K("deposito", "Depósito do estande", Zona.Servico, DepositoDoEstande, "ConcretoClaro", "Concreto", "STAFF ONLY");
            var estande = K("estande", "Estande de tiro", Zona.Servico, Estande, "ConcretoClaro", "Concreto");

            Porta P(Comodo x, Comodo y, float pos, TipoDePorta tipo, int folhas = 1, float largura = 0f)
            {
                var p = new Porta { a = x, b = y, pos = pos, folhas = folhas, tipo = tipo, largura = largura };
                a.portas.Add(p);
                return p;
            }
            Porta Fora(Comodo k, int lado, float pos, TipoDePorta tipo, int folhas = 1, float largura = 0f, float alto = 0f)
            {
                var p = new Porta { a = k, lado = lado, pos = pos, folhas = folhas, tipo = tipo, largura = largura, alto = alto };
                a.portas.Add(p);
                return p;
            }
            Fora(vendas, 0, MeioDaEntrada, TipoDePorta.Vidro, 2, LarguraDaEntrada, AltoDaEntrada);
            P(vendas, antessala, Z(4.4f), TipoDePorta.Metal);
            P(antessala, estande, X(18.6f), TipoDePorta.Reforcada);
            P(antessala, banheiro, Z(1.8f), TipoDePorta.Madeira);
            P(antessala, deposito, Z(5.3f), TipoDePorta.Metal);
            P(vendas, estoque, X(8.9f), TipoDePorta.Metal);
            P(vendas, escritorio, X(13.4f), TipoDePorta.Madeira);
            P(estoque, segura, PortaDoCofre.z, TipoDePorta.Reforcada, 1, VaoDoCofre);
            Fora(estoque, 2, X(8.9f), TipoDePorta.Metal);
            Fora(estande, 1, Z(8.7f), TipoDePorta.Metal);
            return a;
        }

        /// <summary>A porta de cofre na planta (a do estoque com a sala segura): a folha é o modelo, não do kit.</summary>
        public static bool EhACofre(Porta p) => p.b != null && (p.a.tipo == "segura" || p.b.tipo == "segura");

        static Andar _planta;
        /// <summary>A planta, feita uma vez (para a tela da cena e para as consultas).</summary>
        public static Andar PlantaPronta => _planta ??= Planta();

        /// <summary>O cômodo de um ponto (null fora da loja ou acima do teto).</summary>
        public static Comodo Onde(Vector3 p)
        {
            if (p.y < -0.6f || p.y > PeDireito + 0.2f) return null;
            return PlantaPronta.Em(p.x, p.z);
        }
    }
}
