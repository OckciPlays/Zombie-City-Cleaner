using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As portas e as janelas do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3): as folhas de cada
    /// tipo, com os detalhes (a de vai-e-vem com o visor, a chapa de inox embaixo e a de empurrar; a corta-fogo da escada com
    /// a barra antipânico e o visor estreito; a de madeira com a maçaneta e, na área médica, o visor; a reforçada com o visor
    /// pequeno e o aviso amarelo; a de vidro no caixilho de alumínio), as portas de correr da entrada (uma aberta, a outra
    /// emperrada no meio, sem o vidro), as do elevador (a do 2º andar forçada), os portões da garagem e as janelas: a de
    /// fora com o caixilho, o montante, a bandeira e o vidro sujo (inteiro, quebrado com o buraco, sem vidro ou tapado com as
    /// tábuas e o compensado, mais no térreo, e o isolamento todo tapado) e a de dentro (o vidro do raio-X e os de observação
    /// do isolamento, o do quarto 4 tapado do lado do corredor).
    /// As folhas abertas ficam entre 74° e 104° (as de vai-e-vem duplas, de 74° a 98°): mais fechadas, o zumbi não passava.
    /// </summary>
    public sealed partial class Hospital
    {
        readonly List<Abertura> _vidros = new List<Abertura>();
        static readonly int[] DosDoisLados = { 1, -1 };

        /// <summary>As folhas de uma porta (aberta, fechada ou bloqueada com as tábuas), pelo tipo. 'dentro' (+1 = o lado n da parede) é para onde ela abre.</summary>
        void Folhas(Andar an, Parede w, Abertura ab, Porta p, int dentro, bool daEscada = false)
        {
            switch (p.tipo)
            {
                case TipoDePorta.Elevador: PortaDoElevador(w, ab, p); return;
                case TipoDePorta.Portao: Portao(w, ab, p); return;
            }
            if (p.folhas == 0) return;
            if (p.tipo == TipoDePorta.Vidro && (p.a.tipo == "entrada" || (p.b != null && p.b.tipo == "entrada")))
            {
                PortaDeCorrer(w, ab, p.b == null ? -1 : dentro);
                return;
            }
            bool aberta = p.estado == EstadoDaPorta.Aberta;
            bool medica = (an.zonas.TryGetValue(p.a, out var za) && za == Zona.Medica) || (p.b != null && an.zonas.TryGetValue(p.b, out var zb) && zb == Zona.Medica);
            // a soleira de metal da corta-fogo, como a do elevador: as paredes da caixa da escada sobem até a laje de cima, e
            // no vão da porta a face de cima da parede de baixo ficava no plano do chão e piscava (07/11/2026, a pedido: "a porta
            // de entrada para a escada está com bug de sobreposição na parte de baixo")
            if (daEscada) Placa(w, ab.ini, ab.ini + ab.larg, 0f, 0.01f, 0f, m.esp + 0.04f, "Metal");
            // no mundo, as que abrem viram pontos (o jogo põe a folha que abre, fecha e tranca); os giros são sorteados do
            // mesmo jeito (o rnd segue igual, e as marcas e as janelas saem as mesmas da cena de teste)
            bool viva = portasVivas && QueAbre(an, p);
            bool fechada = false, trancada = false;
            if (viva) ComecoDaPorta(sementeDasPortas, an, p, out fechada, out trancada);
            if (p.folhas == 1)
            {
                float giro = !aberta ? 0f : daEscada ? 172f : Faixa(84f, 104f);
                if (viva) FolhaViva(w, ab.ini, ab.larg, ab.alto, dentro, p, an, giro, daEscada, medica, daEscada, fechada, trancada, 0);
                else Folha(w, ab.ini, ab.larg, ab.alto, dentro, p, giro, daEscada, medica, daEscada);
            }
            else
            {
                float g1 = !aberta ? 0f : Faixa(74f, 98f);
                float g2 = !aberta ? 0f : Faixa(74f, 98f);
                if (viva)
                {
                    int primeira = portas.Count;
                    FolhaViva(w, ab.ini, ab.larg / 2f, ab.alto, dentro, p, an, g1, true, medica, false, fechada, trancada, primeira + 2);
                    FolhaViva(w, ab.ini + ab.larg / 2f, ab.larg / 2f, ab.alto, dentro, p, an, g2, false, medica, false, fechada, trancada, primeira + 1);
                }
                else
                {
                    Folha(w, ab.ini, ab.larg / 2f, ab.alto, dentro, p, g1, true, medica, false);
                    Folha(w, ab.ini + ab.larg / 2f, ab.larg / 2f, ab.alto, dentro, p, g2, false, medica, false);
                }
            }
            if (p.estado == EstadoDaPorta.Bloqueada)
                foreach (int lado in DosDoisLados)
                {
                    float fora = lado * (m.esp / 2f + 0.07f);
                    Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, 0.62f, 0.78f, fora, 0.04f, "MadeiraVelha", 9f);
                    Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, 1.32f, 1.48f, fora, 0.04f, "Madeira", -7f);
                    Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, 1.92f, 2.08f, fora, 0.04f, "BancoMadeira", 5f);
                }
        }

        static string AvisoDaPorta(Porta p)
        {
            foreach (var k in new[] { p.a, p.b })
                if (k != null)
                    switch (k.tipo)
                    {
                        case "isolamento": case "quarto": return "ISOLATION";
                        case "raiox": return "X-RAY";
                        case "a01": return "A-01";
                    }
            return "RESTRICTED";
        }

        /// <summary>
        /// Uma folha de porta, aberta 'abertura' graus para o lado 'dentro', com a dobradiça no começo ou no fim do vão.
        /// Aberta, a dobradiça fica na face da parede (a corta-fogo da escada, a 172°, encosta na parede); fechada, a folha fica
        /// dentro do vão, com 1,2 cm de folga dos lados. A folha tem a colisão; os detalhes, não.
        /// </summary>
        void Folha(Parede w, float ini, float larg, float alto, int dentro, Porta p, float abertura, bool dobradicaNoInicio, bool medica, bool escada)
        {
            bool fechada = abertura < 1f;
            float folga = fechada ? 0.012f : 0.03f, L = larg - 2f * folga, h = alto - 0.02f;
            Vector2 lado = w.n * dentro;
            Vector2 dob = w.a + w.u * (dobradicaNoInicio ? ini + folga : ini + larg - folga) + lado * (fechada ? m.esp / 2f - 0.035f : m.esp / 2f);
            float fi = abertura * Mathf.Deg2Rad;
            Vector2 d = (dobradicaNoInicio ? w.u : -w.u) * Mathf.Cos(fi) + lado * Mathf.Sin(fi);
            float yaw = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            // o espaço da folha: x da dobradiça para a ponta, y para cima, z para os lados
            var F = Matrix4x4.TRS(new Vector3(dob.x, w.baseY + 0.01f, dob.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            DesenharFolha(F, L, h, p, medica, escada);
        }

        // ------------------------------------------------------------ as portas que abrem, no mundo (08/11/2026)

        /// <summary>
        /// A semente do começo das portas que abrem (a do mundo: em cada mundo, umas fechadas e umas trancadas diferentes); a
        /// obra continua com a dela (as marcas, as janelas e a hera saem iguais às aprovadas).
        /// </summary>
        public int sementeDasPortas = 1;

        // as folhas desenhadas de cada modelo (a malha delas, no espaço da folha), uma vez por obra
        readonly Dictionary<string, MalhaDeObra> _folhas = new Dictionary<string, MalhaDeObra>();

        /// <summary>
        /// A porta abre e fecha no mundo (o Plano_Hospital.md, a parte 7): as de folha (a de madeira, a de metal, a corta-fogo,
        /// a de vidro de dentro, a reforçada e a de vai-e-vem) que a planta deixa abertas. Ficam paradas: as fechadas e as
        /// bloqueadas de propósito (a A-01, as salas técnicas, o quarto 4 e a segunda do isolamento, a saída oeste), as de
        /// correr da entrada, as do elevador e os portões da garagem.
        /// </summary>
        public static bool QueAbre(Andar an, Porta p)
        {
            if (p.aberto || p.folhas == 0 || p.estado != EstadoDaPorta.Aberta) return false;
            if (p.tipo == TipoDePorta.Elevador || p.tipo == TipoDePorta.Portao) return false;
            if (p.tipo == TipoDePorta.Vidro && (p.a.tipo == "entrada" || (p.b != null && p.b.tipo == "entrada"))) return false;
            return !an.fechados.Contains(p.a) && (p.b == null || !an.fechados.Contains(p.b));
        }

        /// <summary>
        /// Como uma porta que abre começa no mundo (pela semente do mundo, o andar e o número da porta na planta): fechada e
        /// trancada, pelo lugar dela (<see cref="ChancesDaPorta"/>). A mesma conta serve para os zumbis das salas fechadas.
        /// </summary>
        public static void ComecoDaPorta(int semente, Andar an, Porta p, out bool fechada, out bool trancada)
        {
            int i = an.portas.IndexOf(p);
            float s1 = Sorteio.FracaoDoHash(Sorteio.Hash(semente, an.f, i, 0x504F)), s2 = Sorteio.FracaoDoHash(Sorteio.Hash(semente, an.f, i, 0x5452));
            var (fecha, tranca) = ChancesDaPorta(p);
            fechada = s1 < fecha;
            trancada = fechada && s2 < tranca;
        }

        /// <summary>
        /// A chance de a porta começar fechada e a de, fechada, estar trancada: as saídas para fora quase sempre fechadas e
        /// trancadas (abrem por dentro); a corta-fogo da escada e a de vai-e-vem fechadas às vezes, nunca trancadas; a
        /// reforçada (o raio-X, os quartos do isolamento) metade fechada; as das salas que guardam alguma coisa (a medicação,
        /// os depósitos, o arquivo, a TI, a direção, a segurança, as salas de máquinas) quase sempre fechadas, metade
        /// trancadas; as dos escritórios metade fechadas; as dos banheiros e do vestiário pouco trancadas; o resto (os
        /// consultórios, as enfermarias, a copa...) aberto quase sempre.
        /// </summary>
        public static (float fecha, float tranca) ChancesDaPorta(Porta p)
        {
            if (p.b == null) return (0.8f, 0.6f);
            if (p.a.tipo == "escada" || p.b.tipo == "escada") return (0.35f, 0f);
            if (p.tipo == TipoDePorta.VaiEVem) return (0.4f, 0f);
            if (p.tipo == TipoDePorta.Reforcada) return (0.5f, 0.3f);
            var alvo = ParaOndeAbre(p.a, p.b);
            var outro = alvo == p.a ? p.b : p.a;
            string k = DeCirculacao.Contains(alvo.tipo) ? outro.tipo : alvo.tipo;
            switch (k)
            {
                case "medicacao": case "deposito": case "arquivo": case "ti": case "diretor": case "seguranca":
                case "eletrica": case "bombas": case "geradores": case "camaras": case "residuos":
                    return (0.7f, 0.5f);
                case "escritorio": case "rh": case "secretaria": case "reuniao": case "planejamento": case "comando": case "raiox":
                    return (0.5f, 0.3f);
                case "banheiro": case "vestiario":
                    return (0.4f, 0.1f);
                default:
                    return (0.3f, 0.15f);
            }
        }

        /// <summary>
        /// Uma folha que abre (no mundo): no lugar da folha desenhada, o ponto dela (<see cref="Obra.portas"/>), com a
        /// dobradiça na face da parede, a folha do tamanho da aberta e a malha dela à parte (a mesma da folha parada, no
        /// espaço da folha). 'giro' é o da folha aberta; 'parMaisUm', a outra folha da dupla.
        /// </summary>
        void FolhaViva(Parede w, float ini, float larg, float alto, int dentro, Porta p, Andar an, float giro, bool dobradicaNoInicio,
                       bool medica, bool escada, bool fechada, bool trancada, int parMaisUm)
        {
            const float folga = 0.03f;
            float L = larg - 2f * folga, h = alto - 0.02f;
            Vector2 lado = w.n * dentro;
            Vector2 dob = w.a + w.u * (dobradicaNoInicio ? ini + folga : ini + larg - folga) + lado * (m.esp / 2f);
            string aviso = p.tipo == TipoDePorta.Reforcada ? AvisoDaPorta(p) : "";
            string modelo = $"Hospital: {p.tipo} {L:0.000} × {h:0.000}{(medica ? " (médica)" : "")}{(escada ? " (escada)" : "")} {aviso}";
            if (!_folhas.TryGetValue(modelo, out var malha))
            {
                malha = new MalhaDeObra(1024);
                DesenharEm(malha, () => DesenharFolha(Matrix4x4.identity, L, h, p, medica, escada));
                _folhas[modelo] = malha;
            }
            var alvo = p.b == null ? null : ParaOndeAbre(p.a, p.b);
            if (giro < 1f) giro = 90f;
            portas.Add(new PortaDaObra
            {
                dobradica = new Vector3(dob.x, w.baseY + 0.01f, dob.y),
                fechada = dobradicaNoInicio ? w.u : -w.u,
                abre = lado,
                largura = L,
                altura = h,
                cor = "Metal",
                giro = fechada ? 0f : giro,
                maximo = Mathf.Max(giro, AberturaMaxima),
                abertaEm = giro,
                tipo = p.b == null ? PortaDaObra.Tipo.Fora : PortaDaObra.Tipo.Dentro,
                comodo = alvo?.tipo,
                doKit = true,
                trancada = trancada,
                abreParaFora = p.b == null,
                parMaisUm = parMaisUm,
                folha = malha,
                modelo = modelo,
            });
        }

        // a folha de cada tipo no espaço F (x da dobradiça para a ponta, y para cima, z para os lados), com L × h
        void DesenharFolha(Matrix4x4 F, float L, float h, Porta p, bool medica, bool escada)
        {
            void C(float x, float y, float sx, float sy, float sz, Cor cor, bool colide = false) =>
                CaixaM(F * Matrix4x4.Translate(new Vector3(x, y, 0f)), new Vector3(sx, sy, sz), cor, colide);
            // o vidro do visor, nas duas faces (a 't' do meio da folha)
            void Vidro(float x, float y, float sx, float sy, float t, Cor cor)
            {
                foreach (int s in DosDoisLados)
                {
                    _q.Clear();
                    _q.Add(F.MultiplyPoint3x4(new Vector3(x - sx / 2f, y - sy / 2f, s * t)));
                    _q.Add(F.MultiplyPoint3x4(new Vector3(x + sx / 2f, y - sy / 2f, s * t)));
                    _q.Add(F.MultiplyPoint3x4(new Vector3(x + sx / 2f, y + sy / 2f, s * t)));
                    _q.Add(F.MultiplyPoint3x4(new Vector3(x - sx / 2f, y + sy / 2f, s * t)));
                    vis.Face(_q, F.MultiplyVector(new Vector3(0f, 0f, s)), cor);
                }
            }
            switch (p.tipo)
            {
                case TipoDePorta.VaiEVem:
                    C(L / 2f, h / 2f, L, h, 0.045f, "PlacaBranca", true);
                    C(L / 2f, 0.17f, L - 0.02f, 0.3f, 0.051f, "Cromo");
                    C(L / 2f, 1.5f, 0.34f, 0.5f, 0.053f, "Cromo");
                    Vidro(L / 2f, 1.5f, 0.26f, 0.42f, 0.0272f, "VidroSujo");
                    C(L - 0.13f, 1.12f, 0.1f, 0.32f, 0.051f, "Cromo");
                    break;
                case TipoDePorta.Metal:
                    C(L / 2f, h / 2f, L, h, 0.045f, "Metal", true);
                    if (escada)
                    {
                        // a corta-fogo: a barra antipânico e o visor estreito de vidro aramado
                        C(L - 0.3f, 1.5f, 0.16f, 0.74f, 0.053f, "Cromo");
                        Vidro(L - 0.3f, 1.5f, 0.1f, 0.66f, 0.0272f, "VidroSujo");
                        C(L / 2f, 1.0f, L - 0.16f, 0.06f, 0.1f, "Cromo");
                    }
                    else
                    {
                        // as de serviço: a veneziana embaixo e a maçaneta
                        C(L / 2f, 0.36f, Mathf.Min(0.5f, L - 0.3f), 0.3f, 0.049f, "Grade");
                        C(L - 0.09f, 1.0f, 0.13f, 0.022f, 0.1f, "Cromo");
                    }
                    break;
                case TipoDePorta.Reforcada:
                    C(L / 2f, h / 2f, L, h, 0.07f, "Poste", true);
                    C(L / 2f, 1.6f, 0.32f, 0.32f, 0.08f, "Cromo");
                    Vidro(L / 2f, 1.6f, 0.24f, 0.24f, 0.0405f, "Vidro");
                    C(L - 0.12f, 1.05f, 0.05f, 0.42f, 0.13f, "Cromo");
                    C(L / 2f, 1.18f, 0.44f, 0.26f, 0.076f, "PlacaAmarela");
                    string aviso = AvisoDaPorta(p);
                    foreach (int s in DosDoisLados)
                        Letreiro(aviso, F.MultiplyPoint3x4(new Vector3(L / 2f, 1.15f, s * 0.038f)), F.MultiplyVector(new Vector3(-s, 0f, 0f)),
                                 F.MultiplyVector(new Vector3(0f, 0f, s)), 0.055f, "Pneu", plano: true, sai: 0.0015f);
                    break;
                case TipoDePorta.Vidro:
                    // o caixilho de alumínio com o vidro sujo no meio (a colisão é a folha inteira)
                    C(0.035f, h / 2f, 0.07f, h, 0.05f, "Metal");
                    C(L - 0.035f, h / 2f, 0.07f, h, 0.05f, "Metal");
                    C(L / 2f, h - 0.05f, L - 0.14f, 0.1f, 0.05f, "Metal");
                    C(L / 2f, 0.1f, L - 0.14f, 0.2f, 0.05f, "Metal");
                    Vidro(L / 2f, (0.2f + h - 0.1f) / 2f, L - 0.14f, h - 0.3f, 0.002f, "VidroSujo");
                    CaixaM(F * Matrix4x4.Translate(new Vector3(L / 2f, h / 2f, 0f)), new Vector3(L, h, 0.04f), "Metal", colide: true, visual: false);
                    C(L - 0.14f, 1.05f, 0.03f, 0.45f, 0.11f, "Cromo");
                    break;
                default:
                    C(L / 2f, h / 2f, L, h, 0.045f, "Madeira", true);
                    C(L - 0.09f, 1.0f, 0.13f, 0.022f, 0.1f, "Cromo");
                    C(L - 0.06f, 1.0f, 0.05f, 0.05f, 0.055f, "Cromo");
                    if (medica)
                    {
                        C(L - 0.32f, 1.55f, 0.16f, 0.66f, 0.053f, "Metal");
                        Vidro(L - 0.32f, 1.55f, 0.1f, 0.6f, 0.0272f, "VidroSujo");
                    }
                    break;
            }
        }

        // as duas folhas do elevador, de inox, no meio da parede (fechadas, com a emenda escura; na do 2º andar, afastadas
        // pela fresta: a ponta de cada uma entra na parede), e a soleira
        void PortaDoElevador(Parede w, Abertura ab, Porta p)
        {
            float g = p.fresta / 2f, meio = ab.ini + ab.larg / 2f;
            Placa(w, ab.ini - g, meio - g, 0.01f, ab.alto, 0f, 0.05f, "Cromo", colide: true);
            Placa(w, meio + g, ab.ini + ab.larg + g, 0.01f, ab.alto, 0f, 0.05f, "Cromo", colide: true);
            if (g <= 0f)
                foreach (int lado in DosDoisLados) Placa(w, meio - 0.004f, meio + 0.004f, 0.01f, ab.alto, lado * 0.026f, 0.003f, "Pneu");
            Placa(w, ab.ini, ab.ini + ab.larg, 0f, 0.012f, 0f, m.esp + 0.04f, "Metal");
        }

        // o portão de enrolar da garagem: a caixa do rolo por dentro, em cima, e, fechado, a cortina de chapa com os gomos
        void Portao(Parede w, Abertura ab, Porta p)
        {
            Placa(w, ab.ini - 0.1f, ab.ini + ab.larg + 0.1f, ab.alto + 0.05f, ab.alto + 0.5f, -(m.esp / 2f + 0.25f), 0.45f, "Metal");
            if (p.estado == EstadoDaPorta.Aberta) return;
            Placa(w, ab.ini + 0.01f, ab.ini + ab.larg - 0.01f, 0.01f, ab.alto, 0f, 0.07f, "Metal", colide: true);
            for (float y = 0.4f; y < ab.alto; y += 0.4f)
                foreach (int lado in DosDoisLados)
                    Placa(w, ab.ini + 0.01f, ab.ini + ab.larg - 0.01f, y - 0.015f, y + 0.015f, lado * 0.037f, 0.004f, "Grade");
        }

        // as portas de correr da entrada, por dentro (lado = +1 o lado n da parede): o trilho em cima, a folha da esquerda
        // aberta até o fim (na frente do vidro fixo) e a da direita emperrada no meio, sem o vidro (sobraram os cacos dos cantos)
        void PortaDeCorrer(Parede w, Abertura ab, int lado)
        {
            float fora = lado * (m.esp / 2f + 0.05f), meia = ab.larg / 2f + 0.03f;
            Placa(w, ab.ini - meia, ab.ini + ab.larg + meia, ab.alto, ab.alto + 0.16f, fora, 0.1f, "Metal");
            FolhaDeCorrer(w, ab.ini - meia + 0.2f, meia, ab.alto, fora, true);
            FolhaDeCorrer(w, ab.ini + ab.larg / 2f + 0.75f, meia, ab.alto, fora, false);
        }

        void FolhaDeCorrer(Parede w, float s0, float larg, float alto, float fora, bool vidro)
        {
            const float e = 0.06f;
            float s1 = s0 + larg, a = s0 + e, b = s1 - e, y0 = 0.12f, y1 = alto - 0.08f;
            Placa(w, s0, a, 0.01f, alto, fora, 0.05f, "Metal", colide: true);
            Placa(w, b, s1, 0.01f, alto, fora, 0.05f, "Metal", colide: true);
            Placa(w, a, b, y1, alto, fora, 0.05f, "Metal");
            Placa(w, a, b, 0.01f, y0, fora, 0.05f, "Metal", colide: true);
            var n = new Vector3(w.n.x, 0f, w.n.y);
            if (vidro)
            {
                Placa(w, a, b, y0, y1, fora, 0.012f, "VidroSujo", colide: true);
                return;
            }
            // os cacos que ficaram nos cantos
            foreach (var (ca, cb, cc) in new[]
            {
                (new Vector2(a, y0), new Vector2(a + 0.32f, y0), new Vector2(a, y0 + 0.5f)),
                (new Vector2(b, y1), new Vector2(b - 0.25f, y1), new Vector2(b, y1 - 0.62f)),
                (new Vector2(b, y0), new Vector2(b, y0 + 0.2f), new Vector2(b - 0.14f, y0)),
            })
                foreach (int s in DosDoisLados)
                {
                    _q.Clear();
                    _q.Add(w.Ponto(ca.x, ca.y, fora)); _q.Add(w.Ponto(cb.x, cb.y, fora)); _q.Add(w.Ponto(cc.x, cc.y, fora));
                    vis.Face(_q, n * s, "VidroSujo");
                }
        }

        /// <summary>
        /// Uma janela de fora: o caixilho de alumínio no meio da parede, o montante e a travessa da bandeira (na entrada, os
        /// montantes a cada 1,25 m da vitrine; na pequena dos banheiros, só o caixilho) e o estado do vidro, sorteado: tapada
        /// (por fora, as tábuas ou a chapa de compensado, com o vidro atrás), com um ou dois vidros quebrados (o buraco torto),
        /// sem vidro nenhum ou inteira, suja. No térreo, a evacuação tapou quatro em cada dez; os quartos do isolamento, todas.
        /// </summary>
        void JanelaDeFora(Parede w, Abertura ab, Comodo k, int f)
        {
            const float e = 0.05f, t = 0.07f;
            float s0 = ab.ini, s1 = ab.ini + ab.larg, y0 = ab.baixo, y1 = ab.alto;
            // a moldura de concreto por fora (a de vidro da entrada não tem): o peitoril saindo 10 cm, a verga e os batentes
            if (k.tipo != "entrada")
            {
                float face = m.esp / 2f;
                Placa(w, s0 - 0.13f, s1 + 0.13f, y0 - 0.14f, y0 - 0.02f, face + 0.05f, 0.1f, "ConcretoClaro");
                Placa(w, s0 - 0.11f, s1 + 0.11f, y1, y1 + 0.22f, face + 0.04f, 0.08f, "ConcretoClaro");
                Placa(w, s0 - 0.09f, s0, y0 - 0.02f, y1, face + 0.03f, 0.06f, "ConcretoClaro");
                Placa(w, s1, s1 + 0.09f, y0 - 0.02f, y1, face + 0.03f, 0.06f, "ConcretoClaro");
            }
            Placa(w, s0, s0 + e, y0, y1, 0f, t, "Metal");
            Placa(w, s1 - e, s1, y0, y1, 0f, t, "Metal");
            Placa(w, s0 + e, s1 - e, y1 - e, y1, 0f, t, "Metal");
            Placa(w, s0 + e, s1 - e, y0, y0 + e, 0f, t, "Metal");
            // cada vidro: de a a b ao longo da parede e de ya a yb (a Abertura: o início, a largura, o baixo e o alto)
            _vidros.Clear();
            void UmVidro(float a, float b, float ya, float yb) => _vidros.Add(new Abertura(a, b - a, ya, yb));
            if (k.tipo == "entrada")
            {
                int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 1.25f));
                float passo = (s1 - s0 - 2f * e) / n;
                for (int i = 0; i < n; i++)
                {
                    float a = s0 + e + passo * i, b = a + passo;
                    if (i > 0) Placa(w, a - e / 2f, a + e / 2f, y0 + e, y1 - e, 0f, t, "Metal");
                    UmVidro(i > 0 ? a + e / 2f : a, i < n - 1 ? b - e / 2f : b, y0 + e, y1 - e);
                }
            }
            else if (y1 - y0 < 1.0f)
                UmVidro(s0 + e, s1 - e, y0 + e, y1 - e);
            else
            {
                // o montante, a bandeira em cima e a travessa no meio dos vidros de baixo (os quadros das referências)
                float meio = (s0 + s1) / 2f, yt = y1 - 0.55f, ym = (y0 + yt) / 2f;
                Placa(w, meio - e / 2f, meio + e / 2f, y0 + e, yt - e / 2f, 0f, t, "Metal");
                Placa(w, s0 + e, s1 - e, yt - e / 2f, yt + e / 2f, 0f, t, "Metal");
                Placa(w, s0 + e, s1 - e, ym - 0.02f, ym + 0.02f, 0f, 0.05f, "Metal");
                UmVidro(s0 + e, meio - e / 2f, y0 + e, ym - 0.02f);
                UmVidro(meio + e / 2f, s1 - e, y0 + e, ym - 0.02f);
                UmVidro(s0 + e, meio - e / 2f, ym + 0.02f, yt - e / 2f);
                UmVidro(meio + e / 2f, s1 - e, ym + 0.02f, yt - e / 2f);
                UmVidro(s0 + e, s1 - e, yt + e / 2f, y1 - e);
            }
            // o vidro escuro (o "Vidro") enche cada quadro e entra 1,2 cm no caixilho: com a margem do abandono, ficava uma
            // fresta clara em volta de cada vidro, que parecia uma segunda moldura de metal
            const string vidro = "Vidro";
            const float margem = -0.012f;
            double r = rnd.NextDouble();
            float tapada = k.tipo == "quarto" ? 1f : k.tipo == "entrada" ? 0.15f : f == Terreo ? 0.4f : 0.08f;
            float quebrada = f == Terreo ? 0.25f : 0.3f, semVidro = 0.04f;
            if (r < tapada)
            {
                foreach (var v in _vidros) AbandonoDaObra.Vidro(this, w, v, rnd, false, vidro, margem);
                if (k.tipo == "entrada") AbandonoDaObra.Tabuas(this, w, ab, rnd, 1f, null);
                else TabuasDaJanela(w, ab);
            }
            else if (r < tapada + quebrada)
            {
                int qual = rnd.Next(_vidros.Count);
                for (int i = 0; i < _vidros.Count; i++) AbandonoDaObra.Vidro(this, w, _vidros[i], rnd, i == qual || Chance(0.2f), vidro, margem);
            }
            else if (r >= tapada + quebrada + semVidro)
                foreach (var v in _vidros) AbandonoDaObra.Vidro(this, w, v, rnd, false, vidro, margem);
        }

        /// <summary>As madeiras das tábuas (marrons: as cores da madeira das casas ficam cinza no material do abandono e, nas
        /// janelas do hospital, pareciam uma grade de metal).</summary>
        static readonly string[] MadeirasDasTabuas = { "Lente", "Feno", "Terra" };

        /// <summary>
        /// As tábuas pregadas por fora, na frente da moldura de concreto (sem atravessar o peitoril, a verga e os batentes): em
        /// X com uma deitada no meio, de três a quatro deitadas ou a chapa de compensado inteira (clara). Com a colisão.
        /// </summary>
        void TabuasDaJanela(Parede w, Abertura ab)
        {
            float face = m.esp / 2f;
            float s0 = ab.ini - 0.07f, s1 = ab.ini + ab.larg + 0.07f, y0 = ab.baixo + 0.02f, y1 = ab.alto - 0.02f;
            float h = y1 - y0, cs = (s0 + s1) / 2f, cy = (y0 + y1) / 2f;
            string Madeira() => MadeirasDasTabuas[rnd.Next(MadeirasDasTabuas.Length)];
            double v = rnd.NextDouble();
            if (v < 0.3)
            {
                Placa(w, s0, s1, y0, y1, face + 0.075f, 0.02f, "Palha", 0f, true);
                for (int i = 0; i < 3; i++)
                    Placa(w, cs - 0.01f + (i - 1) * 0.5f, cs + 0.01f + (i - 1) * 0.5f, y0 + 0.05f, y1 - 0.05f, face + 0.087f, 0.004f, "Terra");
                return;
            }
            if (v < 0.7)
            {
                float diag = Mathf.Sqrt((s1 - s0) * (s1 - s0) + h * h) - 0.32f, ang = Mathf.Atan2(h, s1 - s0) * Mathf.Rad2Deg;
                Placa(w, cs - diag / 2f, cs + diag / 2f, cy - 0.08f, cy + 0.08f, face + 0.095f, 0.025f, Madeira(), ang, true);
                Placa(w, cs - diag / 2f, cs + diag / 2f, cy - 0.08f, cy + 0.08f, face + 0.12f, 0.025f, Madeira(), -ang, true);
                Placa(w, s0, s1, cy - 0.08f, cy + 0.08f, face + 0.145f, 0.025f, Madeira(), Faixa(-4f, 4f), true);
                return;
            }
            int n = rnd.Next(3, 5);
            for (int i = 0; i < n; i++)
            {
                float y = y0 + (i + 0.5f) * h / n;
                Placa(w, s0, s1, y - 0.08f, y + 0.08f, face + 0.095f, 0.025f, Madeira(), Faixa(-4f, 4f), true);
            }
        }

        /// <summary>
        /// Uma janela de dentro (o vidro do comando do raio-X, as de observação do isolamento): o caixilho de metal, mais
        /// largo que a parede, e o vidro inteiro, com a colisão; a do quarto lacrado, tapada com o compensado do lado de fora.
        /// </summary>
        void JanelaDeDentro(Andar an, Parede w, Abertura ab, Divisa d)
        {
            const float e = 0.05f;
            float s0 = ab.ini, s1 = ab.ini + ab.larg, y0 = ab.baixo, y1 = ab.alto, grossura = m.esp + 0.03f;
            Placa(w, s0, s0 + e, y0, y1, 0f, grossura, "Metal");
            Placa(w, s1 - e, s1, y0, y1, 0f, grossura, "Metal");
            Placa(w, s0 + e, s1 - e, y1 - e, y1, 0f, grossura, "Metal");
            Placa(w, s0 + e, s1 - e, y0, y0 + e, 0f, grossura, "Metal");
            Placa(w, s0 + e, s1 - e, y0 + e, y1 - e, 0f, 0.02f, "Vidro", colide: true);
            bool aLacrado = an.fechados.Contains(d.a), bLacrado = an.fechados.Contains(d.b);
            if (aLacrado == bLacrado) return;
            // o lado n da parede é o b numa linha em z e o a numa linha em x; o compensado vai do lado de quem não está lacrado
            Comodo ladoN = d.vertical ? d.b : d.a, aberto = aLacrado ? d.b : d.a;
            float fora = (aberto == ladoN ? 1f : -1f) * (grossura / 2f + 0.015f);
            Placa(w, s0 - 0.06f, s1 + 0.06f, y0 - 0.06f, y1 + 0.06f, fora, 0.025f, "Compensado", 0f, true);
        }
    }
}
