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
    ///  - onde ele cortaria a passagem: numa grade de 15 cm, com a folga de quem anda (0,28 m), as portas do cômodo
    ///    continuam ligadas entre si e a frente de cada móvel continua alcançável de uma porta.
    /// As listas de rascunho são de cada thread (as construções são montadas em várias).
    /// </summary>
    public static class MoveisDaObra
    {
        enum Regra : byte { Parede, Fileira }

        sealed class Item
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
        // entre dois móveis passava quando o meio de uma célula caía nela): entre dois móveis, passa-se com 0,65 m
        const float Celula = 0.15f, Folga = 0.25f, FolgaDaGrade = Folga + Celula / 2, FrenteDaPorta = 1.0f, Vao = 0.05f;

        struct Furo { public float a, b; public bool porta; public float peitoril; }

        // um lado do cômodo: 0 = z0 (de frente para +z), 1 = x1 (para -x), 2 = z1 (para -z), 3 = x0 (para +x)
        [System.ThreadStatic] static List<Furo>[] _furos;
        [System.ThreadStatic] static List<Rect> _bloqueios, _colocados, _frentes, _bocas;
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
                _ordem = new List<int>();
            }
            AcharFuros(obra, k, chao);
            _bloqueios.Clear(); _colocados.Clear(); _frentes.Clear(); _bocas.Clear();
            foreach (var (area, y) in obra.ocupado)
                if (Mathf.Abs(y - chao) < 0.2f && area.Overlaps(r)) _bloqueios.Add(area);
            // a frente de cada porta fica livre, e a boca dela (logo para dentro) é o que a passagem liga
            for (int lado = 0; lado < 4; lado++)
                foreach (var f in _furos[lado])
                    if (f.porta)
                    {
                        _bloqueios.Add(Faixa(r, lado, f.a - 0.15f, f.b + 0.15f, 0f, FrenteDaPorta));
                        _bocas.Add(Faixa(r, lado, f.a + 0.1f, f.b - 0.1f, 0f, 0.4f));
                    }
            PrepararGrade(r);
            var rnd = new System.Random(semente * 7919 + Mathf.RoundToInt(k.x0 * 13f + k.z0 * 7f + chao * 3f));
            foreach (var item in receita)
            {
                int quantos = item.min + rnd.Next(item.max - item.min + 1);
                for (int n = 0; n < quantos; n++)
                {
                    bool feito = item.regra == Regra.Fileira ? Fileira(item, r, k, chao, rnd, saida) : NaParede(item, r, k, chao, rnd, saida);
                    if (!feito && item.senao != null) feito = NaParede(item.senao, r, k, chao, rnd, saida);
                    if (!feito) break;
                }
            }
        }

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

        // um móvel de costas para uma parede: os lugares de 20 em 20 cm em volta do cômodo, sorteados
        static bool NaParede(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida)
        {
            _ordem.Clear();
            for (int lado = 0; lado < 4; lado++)
            {
                float comprimento = lado == 0 || lado == 2 ? r.width : r.height;
                int n = Mathf.FloorToInt((comprimento - it.w) / Passo);
                for (int i = 0; i <= n; i++) _ordem.Add(lado * 1000 + i);
            }
            for (int i = _ordem.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (_ordem[i], _ordem[j]) = (_ordem[j], _ordem[i]);
            }
            int tentativas = 0, maximo = it.min > 0 ? TentativasDoPrecisa : Tentativas;
            foreach (int codigo in _ordem)
            {
                int lado = codigo / 1000;
                float ini = (lado == 0 || lado == 2 ? r.xMin : r.yMin) + (codigo % 1000) * Passo;
                var pe = Faixa(r, lado, ini, ini + it.w, 0f, it.d);
                if (!Livre(pe) || TapaJanela(it, pe, r)) continue;
                var frente = Faixa(r, lado, ini + it.w / 2 - 0.2f, ini + it.w / 2 + 0.2f, it.d + 0.05f, it.d + 0.45f);
                if (Colocar(pe, frente)) { Guardar(it, pe, Giros[lado], k, chao, saida); return true; }
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
                    if (!f.porta && it.h > f.peitoril + 0.01f && pe.Overlaps(Faixa(r, lado, f.a - 0.1f, f.b + 0.1f, 0f, 0.35f))) return true;
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

        // a grade: 0 livre, 1 parede ou o que ocupa o chão, 2 um móvel posto, 3 o móvel em teste (de cada thread)
        [System.ThreadStatic] static byte[] _grade;
        [System.ThreadStatic] static bool[] _visto;
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
            if (_grade == null || _grade.Length < n) { _grade = new byte[n * 2]; _visto = new bool[n * 2]; _fila = new int[n * 2]; }
            _sx = r.width / _nx;
            _sz = r.height / _nz;
            for (int j = 0; j < _nz; j++)
                for (int i = 0; i < _nx; i++)
                {
                    float x = r.xMin + (i + 0.5f) * _sx, z = r.yMin + (j + 0.5f) * _sz;
                    bool livre = (x - r.xMin >= FolgaDaGrade && r.xMax - x >= FolgaDaGrade && z - r.yMin >= FolgaDaGrade && r.yMax - z >= FolgaDaGrade) || NaBoca(x, z);
                    if (livre)
                        foreach (var b in _bloqueios)
                            if (b.Contains(new Vector2(x, z)) && !EhFrenteDePorta(b)) { livre = false; break; }
                    _grade[j * _nx + i] = livre ? (byte)0 : (byte)1;
                }
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
            Celulas(pe, FolgaDaGrade, out int i0, out int i1, out int j0, out int j1);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (_grade[j * _nx + i] == de) _grade[j * _nx + i] = para;
        }

        // põe o móvel se a passagem continua: as portas ligadas entre si e a frente de cada móvel alcançável
        static bool Colocar(Rect pe, Rect frente)
        {
            Marcar(pe, 0, 3);
            _frentes.Add(frente);
            if (Passa())
            {
                Marcar(pe, 3, 2);
                _colocados.Add(pe);
                return true;
            }
            Marcar(pe, 3, 0);
            _frentes.RemoveAt(_frentes.Count - 1);
            return false;
        }

        static bool Passa()
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
            for (int b = 1; b < _bocas.Count; b++) if (!Alcanca(_bocas[b])) return false;
            foreach (var f in _frentes) if (!Alcanca(f)) return false;
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

        static void Guardar(Item it, Rect pe, float giro, Comodo k, float chao, List<PontoDeMovel> saida)
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
            });
        }
    }
}
