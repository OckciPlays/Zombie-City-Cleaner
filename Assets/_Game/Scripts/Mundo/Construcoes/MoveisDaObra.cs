using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Um ponto de móvel (Parte M6, o fim dela): onde um móvel vai entrar depois, com o tamanho dele e se guarda saque.
    /// Nas coordenadas da construção (as da malha dela, já espelhadas quando a construção é espelhada): o centro do pé do
    /// móvel no chão, o giro em y (graus; a frente do móvel olha para +z quando é 0) e o tamanho (largura em x, altura,
    /// fundo em z, no espaço do móvel).
    /// </summary>
    public struct PontoDeMovel
    {
        public string tipo;
        public Vector3 centro;
        public float giro;
        public Vector3 tamanho;
        public bool saque;
        /// <summary>O tipo do cômodo e o andar dele (-1 = porão).</summary>
        public string comodo;
        public int andar;
        /// <summary>Pendurado na parede (a mobília nova: os quadros, o espelho, os armários de cima da cozinha): a altura da base sobre o chão; 0 no chão.</summary>
        public float altura;

        /// <summary>Ocupa o chão (os pendurados e os tapetes, não: não mexem na passagem nem têm caixa).</summary>
        public bool OcupaOChao => altura <= 0f && (tipo == null || !tipo.StartsWith("Tapete"));

        /// <summary>O mesmo ponto espelhado em x (como as malhas das construções espelhadas).</summary>
        public PontoDeMovel Espelhado()
        {
            var p = this;
            p.centro.x = -p.centro.x;
            p.giro = -p.giro;
            return p;
        }

        /// <summary>O retângulo que o pé do móvel ocupa no chão (x, z), sem espelhar.</summary>
        public Rect Pe
        {
            get
            {
                bool deLado = Mathf.Abs(Mathf.Sin(giro * Mathf.Deg2Rad)) > 0.5f;
                float lx = deLado ? tamanho.z : tamanho.x, lz = deLado ? tamanho.x : tamanho.z;
                return new Rect(centro.x - lx / 2, centro.z - lz / 2, lx, lz);
            }
        }
    }

    /// <summary>
    /// Os pontos dos móveis de um cômodo (Parte M6, "em todas as fatias": os interiores vazios, mas com os pontos dos
    /// móveis): uma receita por tipo de cômodo (e pela construção: casa, apartamento, loja, hospital), com cada móvel
    /// encostado numa parede, de costas para ela, ou em fileiras no meio do salão (as gôndolas). Um móvel não fica:
    ///  - fora do cômodo, em cima de outro ou do que ocupa o chão (o pé da escada, os buracos das lajes, as folhas das
    ///    portas, os balcões: <see cref="Obra.ocupado"/>);
    ///  - na frente de uma porta (1 m livre para dentro do cômodo) nem alto na frente de uma janela;
    ///  - onde ele cortaria a passagem: numa grade de 15 cm, com a folga de quem anda (0,3 m), as portas do cômodo
    ///    continuam ligadas entre si, a frente de cada móvel continua alcançável de uma porta e o chão livre do cômodo
    ///    continua ligado (um móvel não deixa chão isolado que o cômodo vazio não tinha: uma cama e um guarda-roupa
    ///    cortavam ao meio um quarto estreito). O que é sólido no chão (<see cref="Obra.solidos"/>: as folhas das portas
    ///    bem abertas e os balcões das lojas) tem a folga também (com os móveis de verdade, um vaso junto da folha da porta
    ///    do banheiro fechava o resto dele para o zumbi). E onde o NavMesh pode ter chão (a folga do agente, contada das
    ///    caixas de colisão dos móveis, menores que o pé) fica perto do chão alcançado: um canto entre uma poltrona e um
    ///    rack, fechado para a grade, ficava com um pedaço de NavMesh sem caminho. A conferência é o PassagemTestes: o
    ///    NavMesh de cada construção, com as caixas dos móveis.
    /// As listas de rascunho são de cada thread (as construções são montadas em várias).
    /// </summary>
    public static partial class MoveisDaObra
    {
        enum Regra : byte { Parede, Fileira, Bancada, Meio, FrenteDe, AoLado, Alto, Tapete }

        sealed partial class Item
        {
            public string tipo;
            public float w, d, h;
            public int min, max;
            public bool saque;
            public Regra regra;
            /// <summary>Se este não couber: o outro (a cama de solteiro no lugar da de casal).</summary>
            public Item senao;
        }

        static Item I(string tipo, float w, float d, float h, int min, int max, bool saque = false, Item senao = null, Regra regra = Regra.Parede) =>
            new Item { tipo = tipo, w = w, d = d, h = h, min = min, max = max, saque = saque, senao = senao, regra = regra };

        // ------------------------------------------------------------ o catálogo

        static readonly Item[] Nada = new Item[0];
        static readonly Item[] Sala = { I("sofa", 2.0f, 0.9f, 0.85f, 1, 1), I("rack", 1.6f, 0.45f, 0.6f, 1, 1, true), I("poltrona", 0.85f, 0.85f, 0.9f, 0, 2), I("estante", 1.0f, 0.4f, 1.9f, 0, 1, true) };
        // na cozinha, o fogão e a pia primeiro (baixos, cabem embaixo da janela; nas cozinhas de apartamento, cheias de
        // portas, a geladeira posta antes tirava o lugar do fogão), e a geladeira, ou o frigobar, depois
        static readonly Item[] Cozinha = { I("fogao", 0.75f, 0.65f, 0.9f, 1, 1), I("pia", 1.4f, 0.6f, 0.9f, 1, 1, true), I("geladeira", 0.75f, 0.7f, 1.8f, 1, 1, true, senao: I("frigobar", 0.5f, 0.5f, 0.85f, 1, 1, true)), I("armario", 1.0f, 0.45f, 1.9f, 0, 1, true), I("mesa", 1.2f, 0.8f, 0.75f, 0, 1) };
        static readonly Item[] Quarto =
        {
            I("cama", 1.6f, 2.05f, 0.55f, 1, 1, senao: I("cama de solteiro", 0.95f, 2.0f, 0.55f, 1, 1)),
            I("guarda-roupa", 1.6f, 0.6f, 2.1f, 1, 1, true, senao: I("guarda-roupa", 1.0f, 0.6f, 2.1f, 1, 1, true)),
            I("criado-mudo", 0.45f, 0.45f, 0.55f, 0, 2, true), I("comoda", 1.0f, 0.5f, 0.9f, 0, 1, true),
        };
        static readonly Item[] Banheiro = { I("vaso", 0.4f, 0.7f, 0.8f, 1, 1), I("pia de banheiro", 0.6f, 0.45f, 0.85f, 1, 1, true), I("chuveiro", 0.9f, 0.9f, 2.1f, 0, 1) };
        static readonly Item[] Lavanderia = { I("tanque", 0.6f, 0.55f, 0.9f, 1, 1), I("maquina de lavar", 0.6f, 0.6f, 0.85f, 0, 1), I("prateleira", 0.9f, 0.4f, 1.8f, 0, 1, true) };
        static readonly Item[] Porao = { I("prateleira", 1.2f, 0.45f, 1.8f, 1, 3, true), I("bancada", 1.5f, 0.6f, 0.9f, 0, 1, true), I("caixas", 0.6f, 0.6f, 0.6f, 0, 3, true) };
        static readonly Item[] Deposito = { I("prateleira", 1.2f, 0.45f, 1.8f, 1, 4, true), I("caixas", 0.6f, 0.6f, 0.6f, 0, 3, true) };
        static readonly Item[] Portaria = { I("balcao", 1.8f, 0.6f, 1.1f, 1, 1, true), I("poltrona", 0.85f, 0.85f, 0.9f, 0, 2) };
        static readonly Item[] LojaDoPredio = { I("balcao", 1.6f, 0.6f, 1.0f, 1, 1, true), I("prateleira", 1.5f, 0.45f, 1.9f, 1, 6, true) };

        // as lojas (o salão pelo tipo; os fundos)
        static readonly Item Gondola = I("gondola", 0.9f, 2.7f, 1.6f, 0, 12, true, regra: Regra.Fileira);
        static readonly Item[] SalaoComum = { I("prateleira", 1.5f, 0.45f, 1.9f, 2, 8, true), I("balcao", 1.6f, 0.6f, 1.0f, 0, 1, true) };
        static readonly Item[] SalaoMercado = { I("prateleira", 1.5f, 0.5f, 1.9f, 4, 12, true), I("freezer", 1.6f, 0.8f, 0.9f, 0, 3, true), Gondola };
        static readonly Item[] SalaoFarmacia = { I("prateleira", 1.2f, 0.4f, 1.9f, 3, 10, true), Gondola };
        static readonly Item[] SalaoConveniencia = { I("expositor refrigerado", 1.2f, 0.7f, 2.0f, 1, 3, true), I("prateleira", 1.2f, 0.45f, 1.9f, 2, 6, true), Gondola };
        static readonly Item[] SalaoArmas = { I("expositor de armas", 1.5f, 0.35f, 2.0f, 2, 6, true), I("vitrine", 1.4f, 0.6f, 1.0f, 0, 3, true) };
        static readonly Item[] DepositoDaLoja = { I("estante de aco", 1.8f, 0.6f, 2.2f, 1, 6, true), I("caixas", 0.6f, 0.6f, 0.6f, 0, 4, true) };
        static readonly Item[] DepositoDeArmas = { I("armario de armas", 1.0f, 0.5f, 2.0f, 2, 4, true), I("caixas de municao", 0.6f, 0.4f, 0.4f, 0, 3, true) };
        static readonly Item[] Acougue = { I("freezer", 1.6f, 0.8f, 0.9f, 1, 2, true), I("mesa de corte", 1.6f, 0.7f, 0.9f, 1, 1) };
        static readonly Item[] Escritorio = { I("mesa de escritorio", 1.4f, 0.7f, 0.75f, 1, 1, true), I("arquivo", 0.5f, 0.6f, 1.3f, 0, 2, true) };
        static readonly Item[] EscritorioDeArmas = { I("mesa de escritorio", 1.4f, 0.7f, 0.75f, 1, 1, true), I("cofre", 0.6f, 0.6f, 1.0f, 1, 1, true), I("arquivo", 0.5f, 0.6f, 1.3f, 0, 1, true) };

        // o hospital
        static readonly Item Maca = I("maca", 0.8f, 2.0f, 0.9f, 0, 4);
        static readonly Item ArmarioDeRemedios = I("armario de remedios", 1.0f, 0.45f, 1.9f, 0, 2, true);
        static readonly Item[] Recepcao = { I("balcao", 2.4f, 0.7f, 1.1f, 1, 1, true), I("banco de espera", 1.8f, 0.6f, 0.85f, 0, 3) };
        static readonly Item[] Espera = { I("banco de espera", 1.8f, 0.6f, 0.85f, 2, 6) };
        static readonly Item[] Emergencia = { I("maca", 0.8f, 2.0f, 0.9f, 2, 4), I("armario de remedios", 1.0f, 0.45f, 1.9f, 1, 2, true) };
        static readonly Item[] Triagem = { I("maca", 0.8f, 2.0f, 0.9f, 1, 1), I("mesa de escritorio", 1.2f, 0.6f, 0.75f, 1, 1, true), ArmarioDeRemedios };
        static readonly Item[] Consultorio = { I("mesa de escritorio", 1.2f, 0.6f, 0.75f, 1, 1, true), Maca, ArmarioDeRemedios };
        static readonly Item[] RaioX = { I("aparelho de raio-x", 1.2f, 2.2f, 1.8f, 1, 1), ArmarioDeRemedios };
        static readonly Item[] Enfermaria = { I("cama de hospital", 1.0f, 2.1f, 0.8f, 2, 6), I("criado-mudo", 0.45f, 0.45f, 0.55f, 0, 6, true) };
        static readonly Item[] QuartoDeHospital = { I("cama de hospital", 1.0f, 2.1f, 0.8f, 1, 2), I("criado-mudo", 0.45f, 0.45f, 0.55f, 0, 2, true), I("poltrona", 0.85f, 0.85f, 0.9f, 0, 1) };
        static readonly Item[] PostoDeEnfermagem = { I("balcao", 1.8f, 0.6f, 1.1f, 1, 1, true), I("armario de remedios", 1.0f, 0.45f, 1.9f, 1, 3, true) };
        static readonly Item[] Rouparia = { I("prateleira", 1.2f, 0.45f, 1.9f, 2, 5, true) };

        /// <summary>
        /// A receita de um cômodo: 'contexto' é a construção ("casa" para as casas e os apartamentos, "predio" para os
        /// cômodos de todos do prédio, "hospital", ou o tipo da loja, <see cref="TipoDeLoja"/>).
        /// </summary>
        static Item[] Receita(string contexto, string tipo)
        {
            // as casas e os apartamentos (e os depósitos dos prédios): a mobília nova
            var nova = ReceitaNova(contexto, tipo);
            if (nova != null) return nova;
            if (contexto == "hospital")
                switch (tipo)
                {
                    case "recepcao": return Recepcao;
                    case "espera": return Espera;
                    case "emergencia": return Emergencia;
                    case "triagem": return Triagem;
                    case "consultorio": return Consultorio;
                    case "raiox": return RaioX;
                    case "enfermaria": return Enfermaria;
                    case "quarto": return QuartoDeHospital;
                    case "posto": return PostoDeEnfermagem;
                    case "rouparia": return Rouparia;
                    case "banheiro": return Banheiro;
                    case "deposito": return Deposito;
                    default: return Nada;
                }
            bool loja = contexto != "casa" && contexto != "predio";
            switch (tipo)
            {
                case "sala": return Sala;
                case "cozinha": return Cozinha;
                case "quarto": return Quarto;
                case "banheiro": return Banheiro;
                case "lavanderia": return Lavanderia;
                case "porao": return Porao;
                case "portaria": return Portaria;
                case "loja": return LojaDoPredio;
                case "acougue": return Acougue;
                case "escritorio": return contexto == nameof(TipoDeLoja.Armas) ? EscritorioDeArmas : Escritorio;
                case "deposito":
                case "estoque":
                    return !loja ? Deposito : contexto == nameof(TipoDeLoja.Armas) ? DepositoDeArmas : DepositoDaLoja;
                case "salao":
                    switch (contexto)
                    {
                        case nameof(TipoDeLoja.Mercado): return SalaoMercado;
                        case nameof(TipoDeLoja.Farmacia): return SalaoFarmacia;
                        case nameof(TipoDeLoja.VinteQuatroHoras): case nameof(TipoDeLoja.Conveniencia): return SalaoConveniencia;
                        case nameof(TipoDeLoja.Armas): return SalaoArmas;
                        default: return SalaoComum;
                    }
                default: return Nada;   // o corredor, a escada, a garagem: livres
            }
        }

        // ------------------------------------------------------------ a distribuição

        // a folga de quem anda vale para o quadrado inteiro da célula (com a folga só no meio dela, uma fresta de 4 cm
        // entre dois móveis passava quando o meio de uma célula caía nela): entre dois móveis, passa-se com 0,75 m. Com a
        // mobília, a caixa de colisão de cada móvel é 15 cm menor de cada lado (Mobilia.Recuo): para o NavMesh, 1,05 m,
        // o bastante para o zumbi (o agente tem 0,35 m de raio, e o NavMesh, com as células de 12 cm, pode perder uma de
        // cada lado: um vão de 0,9 m num canto fechava); com a folga do jogador (0,25 m), os móveis de verdade fechavam
        // umas portas de banheiro e de quarto para ele, e com a do zumbi inteira (0,38 m), metade dos móveis não cabia. A
        // folha da porta e o balcão (que não encolhem) têm a folga mais 10 cm (FolgaDoSolido)
        const float Celula = 0.15f, Folga = 0.3f, FolgaDaGrade = Folga + Celula / 2, FrenteDaPorta = 1.0f, Vao = 0.05f;
        const float FolgaDoSolido = FolgaDaGrade + 0.1f;
        // onde o NavMesh pode ter chão: a 0,35 m (o raio do agente) das paredes, do que é sólido e da caixa de colisão de cada
        // móvel (o pé encolhido de Mobilia.Recuo), um pouco menos para não deixar de ver um canto; esse chão fica a no máximo
        // duas células do que a grade alcança (a folga dela é 0,175 m maior)
        const float FolgaDoNavMesh = 0.3f, FolgaDoNavMeshNoMovel = 0.35f - Mobilia.Recuo;
        const int Perto = 2;

        struct Furo { public float a, b; public bool porta; public float peitoril; }

        // um lado do cômodo: 0 = z0 (de frente para +z), 1 = x1 (para -x), 2 = z1 (para -z), 3 = x0 (para +x)
        [System.ThreadStatic] static List<Furo>[] _furos;
        [System.ThreadStatic] static List<Rect> _bloqueios, _colocados, _frentes, _bocas, _solidos;
        [System.ThreadStatic] static int _soltasAntes, _longeAntes;
        [System.ThreadStatic] static List<int> _ordem;

        /// <summary>
        /// Os pontos dos móveis do cômodo k, com o chão em 'chao' (nas coordenadas da obra, sem espelhar), em 'saida'. A
        /// obra já está montada (as paredes, as portas e o que ocupa o chão).
        /// </summary>
        public static void Distribuir(Obra obra, Comodo k, float chao, string contexto, int semente, List<PontoDeMovel> saida)
        {
            var receita = Receita(contexto, k.tipo);
            if (receita.Length == 0) return;
            float e = obra.m.esp / 2;
            var r = Rect.MinMaxRect(k.x0 + e, k.z0 + e, k.x1 - e, k.z1 - e);
            if (r.width < 0.8f || r.height < 0.8f) return;
            if (_furos == null)
            {
                _furos = new[] { new List<Furo>(), new List<Furo>(), new List<Furo>(), new List<Furo>() };
                _bloqueios = new List<Rect>(); _colocados = new List<Rect>(); _frentes = new List<Rect>(); _bocas = new List<Rect>();
                _solidos = new List<Rect>();
                _ordem = new List<int>();
            }
            AcharFuros(obra, k, chao);
            _bloqueios.Clear(); _colocados.Clear(); _frentes.Clear(); _bocas.Clear(); _solidos.Clear();
            foreach (var (area, y) in obra.ocupado)
                if (Mathf.Abs(y - chao) < 0.2f && area.Overlaps(r)) _bloqueios.Add(area);
            foreach (var (area, y) in obra.solidos)
                if (Mathf.Abs(y - chao) < 0.2f && area.Overlaps(r))
                    _solidos.Add(area);
            // a frente de cada porta fica livre, e a boca dela (logo para dentro) é o que a passagem liga; no vão largo sem
            // porta (a passagem da sala para a cozinha), só o meio dele, da largura de uma porta (com a mobília nova, em
            // ×1,30, os cantos dele eram o lugar da geladeira nas cozinhas pequenas)
            for (int lado = 0; lado < 4; lado++)
                foreach (var f in _furos[lado])
                    if (f.porta)
                    {
                        var (a, b) = FrenteLivre(f, obra.m);
                        _bloqueios.Add(Faixa(r, lado, a, b, 0f, FrenteDaPorta));
                        _bocas.Add(Faixa(r, lado, f.a + 0.1f, f.b - 0.1f, 0f, 0.4f));
                    }
            PrepararGrade(r);
            // o chão que o cômodo vazio já deixa isolado (atrás de uma folha de porta, num canto)
            bool comeca = Buscar();
            _soltasAntes = comeca ? Soltas() : 0;
            _longeAntes = comeca ? Longe() : 0;
            var rnd = new System.Random(semente * 7919 + Mathf.RoundToInt(k.x0 * 13f + k.z0 * 7f + chao * 3f));
            _altos.Clear();
            int inicio = saida.Count;
            foreach (var item in receita)
            {
                // (só os itens novos têm a chance: as receitas antigas sorteiam como antes)
                if (item.chance < 1f && rnd.NextDouble() > item.chance) continue;
                int quantos = item.min + rnd.Next(item.max - item.min + 1);
                for (int n = 0; n < quantos; n++)
                {
                    bool feito = false;
                    for (var x = item; x != null && !feito; x = x.senao)
                    {
                        var escolhido = x.Escolher(rnd);
                        feito = escolhido != null && Por(escolhido, r, k, chao, rnd, saida, inicio);
                    }
                    if (!feito) break;
                }
            }
        }

        static bool Por(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida, int inicio)
        {
            switch (it.regra)
            {
                case Regra.Fileira: return Fileira(it, r, k, chao, rnd, saida);
                case Regra.Bancada: return Bancada(it, r, k, chao, rnd, saida, inicio);
                case Regra.Meio: return NoMeio(it, r, k, chao, rnd, saida);
                case Regra.FrenteDe: return NaFrenteDe(it, r, k, chao, saida, inicio);
                case Regra.AoLado: return AoLadoDe(it, r, k, chao, rnd, saida, inicio);
                case Regra.Alto: return NaParedeAlto(it, r, k, chao, rnd, saida, inicio);
                case Regra.Tapete: return NoChao(it, r, k, chao, saida, inicio);
                default:
                    if (it.unico != null && Tem(saida, inicio, it.unico)) return true;
                    return NaParede(it, r, k, chao, rnd, saida, inicio);
            }
        }

        /// <summary>
        /// Ao longo da porta (de a até b, na parede), onde a frente dela fica livre: a largura dela e mais 15 cm de cada lado;
        /// num vão mais largo que <see cref="VaoLargo"/> (a passagem sem porta), só o meio, da largura de uma porta.
        /// </summary>
        public static (float a, float b) FrenteLivre(float a, float b, Medidas m)
        {
            if (b - a <= VaoLargo) return (a - 0.15f, b + 0.15f);
            float meio = (a + b) / 2;
            return (meio - m.porta / 2, meio + m.porta / 2);
        }

        public const float VaoLargo = 1.6f;

        static (float a, float b) FrenteLivre(Furo f, Medidas m) => FrenteLivre(f.a, f.b, m);

        // as portas e as janelas de cada lado do cômodo, das paredes desta obra e das de fora (a fachada do prédio)
        static void AcharFuros(Obra obra, Comodo k, float chao)
        {
            for (int lado = 0; lado < 4; lado++) _furos[lado].Clear();
            void Ver(Parede w)
            {
                if (Mathf.Abs(w.baseY - chao) > 0.2f || w.aberturas.Count == 0) return;
                bool emX = Mathf.Abs(w.u.y) < 0.01f, emZ = Mathf.Abs(w.u.x) < 0.01f;
                if (!emX && !emZ) return;
                for (int lado = 0; lado < 4; lado++)
                {
                    bool horizontal = lado == 0 || lado == 2;
                    if (horizontal != emX) continue;
                    float linha = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
                    if (Mathf.Abs((horizontal ? w.a.y : w.a.x) - linha) > 0.12f) continue;
                    float lo = horizontal ? k.x0 : k.z0, hi = horizontal ? k.x1 : k.z1;
                    foreach (var ab in w.aberturas)
                    {
                        Vector2 p0 = w.a + w.u * ab.ini, p1 = w.a + w.u * (ab.ini + ab.larg);
                        float a = horizontal ? Mathf.Min(p0.x, p1.x) : Mathf.Min(p0.y, p1.y), b = horizontal ? Mathf.Max(p0.x, p1.x) : Mathf.Max(p0.y, p1.y);
                        if (b <= lo + 0.05f || a >= hi - 0.05f) continue;
                        _furos[lado].Add(new Furo { a = Mathf.Max(a, lo), b = Mathf.Min(b, hi), porta = ab.EhPorta, peitoril = ab.baixo });
                    }
                }
            }
            foreach (var w in obra.paredes) Ver(w);
            foreach (var w in obra.paredesDeFora) Ver(w);
        }

        // a faixa junto do lado 'lado' do retângulo r: de a a b ao longo dele e de 'de' a 'ate' para dentro
        static Rect Faixa(Rect r, int lado, float a, float b, float de, float ate)
        {
            switch (lado)
            {
                case 0: return Rect.MinMaxRect(a, r.yMin + de, b, r.yMin + ate);
                case 1: return Rect.MinMaxRect(r.xMax - ate, a, r.xMax - de, b);
                case 2: return Rect.MinMaxRect(a, r.yMax - ate, b, r.yMax - de);
                default: return Rect.MinMaxRect(r.xMin + de, a, r.xMin + ate, b);
            }
        }

        static readonly float[] Giros = { 0f, 270f, 180f, 90f };
        /// <summary>
        /// De quanto em quanto o móvel anda ao longo da parede, e quantas vezes no máximo um móvel testa a passagem (os que o
        /// cômodo precisa ter, como a cama e o fogão, tentam mais).
        /// </summary>
        const float Passo = 0.2f;
        const int Tentativas = 10, TentativasDoPrecisa = 40;

        // um móvel de costas para uma parede: os lugares de 20 em 20 cm em volta do cômodo, sorteados (o que fica de frente
        // para outro, como o rack para o sofá, tenta primeiro a parede da frente dele). De lado (a cama de solteiro e o
        // colchão num quarto estreito), o comprido dele encosta na parede, e chega-se a ele pelo lado comprido
        static bool NaParede(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida, int inicio = 0)
        {
            float w = it.deLado ? it.d : it.w, d = it.deLado ? it.w : it.d;
            _ordem.Clear();
            for (int lado = 0; lado < 4; lado++)
            {
                float comprimento = lado == 0 || lado == 2 ? r.width : r.height;
                // (o que não cabe de fundo nem entra na conta: a cama de 2,67 m num quarto de 2,5 m gastava as tentativas)
                if (d > (lado == 0 || lado == 2 ? r.height : r.width)) continue;
                int n = Mathf.FloorToInt((comprimento - w) / Passo);
                for (int i = 0; i <= n; i++) _ordem.Add(lado * 1000 + i);
            }
            for (int i = _ordem.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (_ordem[i], _ordem[j]) = (_ordem[j], _ordem[i]);
            }
            if (it.oposto != null && Achar(saida, inicio, it.oposto, out var anfitriao))
            {
                int frente = (LadoDoGiro(anfitriao.giro) + 2) % 4;
                // a parede da frente primeiro (na ordem sorteada)
                int primeiros = 0;
                for (int i = 0; i < _ordem.Count; i++)
                    if (_ordem[i] / 1000 == frente)
                    {
                        int c = _ordem[i];
                        _ordem.RemoveAt(i);
                        _ordem.Insert(primeiros++, c);
                    }
            }
            int tentativas = 0, maximo = it.min > 0 ? TentativasDoPrecisa : Tentativas;
            foreach (int codigo in _ordem)
            {
                int lado = codigo / 1000;
                float ini = (lado == 0 || lado == 2 ? r.xMin : r.yMin) + (codigo % 1000) * Passo;
                var pe = Faixa(r, lado, ini, ini + w, 0f, d);
                if (!Livre(pe) || TapaJanela(it, pe, r)) continue;
                var frente = Faixa(r, lado, ini + w / 2 - 0.2f, ini + w / 2 + 0.2f, d + 0.05f, d + 0.45f);
                // (de lado, o pé para um lado ou para o outro ao longo da parede)
                float giro = Giros[lado] + (!it.deLado ? 0f : rnd.Next(2) == 0 ? 90f : -90f);
                if (Colocar(pe, frente)) { Guardar(it, pe, giro, k, chao, saida); return true; }
                if (++tentativas >= maximo) return false;
            }
            return false;
        }

        // mais alto que o peitoril na frente de uma janela (de qualquer lado do cômodo: num canto, o móvel encosta na parede
        // do lado), não; da altura dele, sim (a pia e o fogão embaixo da janela da cozinha)
        static bool TapaJanela(Item it, Rect pe, Rect r)
        {
            for (int lado = 0; lado < 4; lado++)
                foreach (var f in _furos[lado])
                    if (!f.porta && it.h > f.peitoril + it.tolerancia && pe.Overlaps(Faixa(r, lado, f.a - 0.1f, f.b + 0.1f, 0f, 0.35f))) return true;
            return false;
        }

        // as gôndolas: no meio do salão, compridas de frente para o fundo, em colunas com corredores entre elas, longe da
        // frente da loja (as portas, as vitrines e os caixas)
        static bool Fileira(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida)
        {
            float x0 = r.xMin + 1.5f, x1 = r.xMax - 1.5f, z0 = r.yMin + 3.4f, z1 = r.yMax - 1.5f;
            int tentativas = 0;
            for (float x = x0; x + it.w <= x1; x += it.w + 1.4f)
                for (float z = z0; z + it.d <= z1; z += it.d + 1.3f)
                {
                    var pe = Rect.MinMaxRect(x, z, x + it.w, z + it.d);
                    if (!Livre(pe) || TapaJanela(it, pe, r)) continue;
                    var frente = Rect.MinMaxRect(x + it.w + 0.05f, z + it.d / 2 - 0.2f, x + it.w + 0.45f, z + it.d / 2 + 0.2f);
                    if (Colocar(pe, frente)) { Guardar(it, pe, 0f, k, chao, saida); return true; }
                    if (++tentativas >= Tentativas) return false;
                }
            return false;
        }

        static bool Livre(Rect pe)
        {
            var maior = Rect.MinMaxRect(pe.xMin - Vao, pe.yMin - Vao, pe.xMax + Vao, pe.yMax + Vao);
            foreach (var b in _bloqueios) if (b.Overlaps(pe)) return false;
            foreach (var c in _colocados) if (c.Overlaps(maior)) return false;
            return true;
        }

        // ------------------------------------------------------------ a passagem (a grade do cômodo)

        // a grade: 0 livre, 1 parede ou o que ocupa o chão, 2 um móvel posto, 3 o móvel em teste (de cada thread); a do
        // NavMesh, o mesmo com a folga dele
        [System.ThreadStatic] static byte[] _grade, _doNavMesh;
        [System.ThreadStatic] static bool[] _visto, _perto;
        [System.ThreadStatic] static int[] _fila;
        [System.ThreadStatic] static Rect _r;
        [System.ThreadStatic] static int _nx, _nz;
        [System.ThreadStatic] static float _sx, _sz;

        // a grade do cômodo, uma vez por cômodo: junto das paredes não se anda (a não ser na boca das portas), nem em cima
        // do que ocupa o chão (a frente das portas é livre para quem anda)
        static void PrepararGrade(Rect r)
        {
            _r = r;
            _nx = Mathf.Max(1, Mathf.FloorToInt(r.width / Celula));
            _nz = Mathf.Max(1, Mathf.FloorToInt(r.height / Celula));
            int n = _nx * _nz;
            if (_grade == null || _grade.Length < n)
            {
                _grade = new byte[n * 2]; _doNavMesh = new byte[n * 2];
                _visto = new bool[n * 2]; _perto = new bool[n * 2]; _fila = new int[n * 2];
            }
            _sx = r.width / _nx;
            _sz = r.height / _nz;
            for (int j = 0; j < _nz; j++)
                for (int i = 0; i < _nx; i++)
                {
                    float x = r.xMin + (i + 0.5f) * _sx, z = r.yMin + (j + 0.5f) * _sz;
                    _grade[j * _nx + i] = Anda(r, x, z, FolgaDaGrade, FolgaDoSolido) ? (byte)0 : (byte)1;
                    _doNavMesh[j * _nx + i] = Anda(r, x, z, FolgaDoNavMesh, FolgaDoNavMesh) ? (byte)0 : (byte)1;
                }
        }

        // se dá para andar no ponto, com a folga das paredes e a do que é sólido
        static bool Anda(Rect r, float x, float z, float folga, float doSolido)
        {
            bool livre = (x - r.xMin >= folga && r.xMax - x >= folga && z - r.yMin >= folga && r.yMax - z >= folga) || NaBoca(x, z);
            if (livre)
                foreach (var b in _bloqueios)
                    if (b.Contains(new Vector2(x, z)) && !EhFrenteDePorta(b)) return false;
            // o que é sólido (com a folga), mesmo encostado na boca
            if (livre)
                foreach (var f in _solidos)
                    if (x > f.xMin - doSolido && x < f.xMax + doSolido && z > f.yMin - doSolido && z < f.yMax + doSolido) return false;
            return livre;
        }

        // as células com o meio dentro de um retângulo (alargado de 'folga')
        static void Celulas(Rect a, float folga, out int i0, out int i1, out int j0, out int j1)
        {
            i0 = Mathf.Max(0, Mathf.CeilToInt((a.xMin - folga - _r.xMin) / _sx - 0.5f));
            i1 = Mathf.Min(_nx - 1, Mathf.FloorToInt((a.xMax + folga - _r.xMin) / _sx - 0.5f));
            j0 = Mathf.Max(0, Mathf.CeilToInt((a.yMin - folga - _r.yMin) / _sz - 0.5f));
            j1 = Mathf.Min(_nz - 1, Mathf.FloorToInt((a.yMax + folga - _r.yMin) / _sz - 0.5f));
        }

        static void Marcar(Rect pe, byte de, byte para)
        {
            Marcar(_grade, pe, FolgaDaGrade, de, para);
            Marcar(_doNavMesh, pe, FolgaDoNavMeshNoMovel, de, para);
        }

        static void Marcar(byte[] grade, Rect pe, float folga, byte de, byte para)
        {
            Celulas(pe, folga, out int i0, out int i1, out int j0, out int j1);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (grade[j * _nx + i] == de) grade[j * _nx + i] = para;
        }

        // põe o móvel se a passagem continua: as portas ligadas entre si e a frente de cada móvel alcançável
        static bool Colocar(Rect pe, Rect frente)
        {
            _umaFrente ??= new List<Rect>(1);
            _umaFrente.Clear();
            _umaFrente.Add(frente);
            return Colocar(pe, _umaFrente);
        }

        [System.ThreadStatic] static List<Rect> _umaFrente;

        // (com mais de uma frente: a bancada da cozinha, a de cada módulo)
        static bool Colocar(Rect pe, List<Rect> frentes)
        {
            Marcar(pe, 0, 3);
            int antes = _frentes.Count;
            _frentes.AddRange(frentes);
            if (Passa())
            {
                Marcar(pe, 3, 2);
                _colocados.Add(pe);
                return true;
            }
            Marcar(pe, 3, 0);
            _frentes.RemoveRange(antes, _frentes.Count - antes);
            return false;
        }

        static bool Passa()
        {
            if (!Buscar()) return false;
            for (int b = 1; b < _bocas.Count; b++) if (!Alcanca(_bocas[b])) return false;
            foreach (var f in _frentes) if (!Alcanca(f)) return false;
            // nenhum chão isolado que o cômodo vazio não tinha: a grade vê o bolsão menor que o NavMesh (a folga dela é maior),
            // e um de 6 células (a folha de uma porta e a pia ao lado) era um canto de 0,4 m² sem caminho para o zumbi
            return Soltas() <= _soltasAntes && Longe() <= _longeAntes;
        }

        // as células onde o NavMesh pode ter chão a mais de Perto células (em x e em z) de uma que a busca alcançou
        static int Longe()
        {
            for (int j = 0; j < _nz; j++)
                for (int i = 0; i < _nx; i++)
                {
                    bool p = false;
                    for (int a = Mathf.Max(0, i - Perto), b = Mathf.Min(_nx - 1, i + Perto); a <= b && !p; a++) p = _visto[j * _nx + a];
                    _perto[j * _nx + i] = p;
                }
            int longe = 0;
            for (int j = 0; j < _nz; j++)
                for (int i = 0; i < _nx; i++)
                {
                    if (_doNavMesh[j * _nx + i] != 0) continue;
                    bool p = false;
                    for (int a = Mathf.Max(0, j - Perto), b = Mathf.Min(_nz - 1, j + Perto); a <= b && !p; a++) p = _perto[a * _nx + i];
                    if (!p) longe++;
                }
            return longe;
        }

        // as células livres que a busca não alcançou
        static int Soltas()
        {
            int n = _nx * _nz, soltas = 0;
            for (int c = 0; c < n; c++) if (_grade[c] == 0 && !_visto[c]) soltas++;
            return soltas;
        }

        // a busca pelas células livres, da boca da primeira porta (ou, sem porta, do primeiro lugar livre)
        static bool Buscar()
        {
            int n = _nx * _nz, cabeca = 0, cauda = 0;
            System.Array.Clear(_visto, 0, n);
            // a busca sai da boca da primeira porta (ou, sem porta, do primeiro lugar livre)
            if (_bocas.Count > 0)
            {
                Celulas(_bocas[0], 0f, out int i0, out int i1, out int j0, out int j1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int c = j * _nx + i;
                        if (_grade[c] != 0 || _visto[c]) continue;
                        _visto[c] = true;
                        _fila[cauda++] = c;
                    }
            }
            else for (int c = 0; c < n && cauda == 0; c++) if (_grade[c] == 0) { _visto[c] = true; _fila[cauda++] = c; }
            if (cauda == 0) return false;
            while (cabeca < cauda)
            {
                int c = _fila[cabeca++], i = c % _nx, j = c / _nx;
                if (i > 0 && _grade[c - 1] == 0 && !_visto[c - 1]) { _visto[c - 1] = true; _fila[cauda++] = c - 1; }
                if (i < _nx - 1 && _grade[c + 1] == 0 && !_visto[c + 1]) { _visto[c + 1] = true; _fila[cauda++] = c + 1; }
                if (j > 0 && _grade[c - _nx] == 0 && !_visto[c - _nx]) { _visto[c - _nx] = true; _fila[cauda++] = c - _nx; }
                if (j < _nz - 1 && _grade[c + _nx] == 0 && !_visto[c + _nx]) { _visto[c + _nx] = true; _fila[cauda++] = c + _nx; }
            }
            return true;
        }

        // alguma célula que encosta no alvo foi alcançada (um alvo menor que uma célula também conta)
        static bool Alcanca(Rect alvo)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt((alvo.xMin - _r.xMin) / _sx)), i1 = Mathf.Min(_nx - 1, Mathf.FloorToInt((alvo.xMax - _r.xMin) / _sx));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((alvo.yMin - _r.yMin) / _sz)), j1 = Mathf.Min(_nz - 1, Mathf.FloorToInt((alvo.yMax - _r.yMin) / _sz));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (_visto[j * _nx + i]) return true;
            return false;
        }

        static bool NaBoca(float x, float z)
        {
            foreach (var boca in _bocas) if (boca.Contains(new Vector2(x, z))) return true;
            return false;
        }

        // a frente das portas bloqueia móveis, não quem anda
        static bool EhFrenteDePorta(Rect b)
        {
            foreach (var boca in _bocas)
                if (b.Overlaps(boca)) return true;
            return false;
        }

        static void Guardar(Item it, Rect pe, float giro, Comodo k, float chao, List<PontoDeMovel> saida, float altura = 0f)
        {
            saida.Add(new PontoDeMovel
            {
                tipo = it.tipo,
                centro = new Vector3(pe.center.x, chao, pe.center.y),
                giro = giro,
                tamanho = new Vector3(it.w, it.h, it.d),
                saque = it.saque,
                comodo = k.tipo,
                andar = k.andar,
                altura = altura,
            });
        }
    }
}
