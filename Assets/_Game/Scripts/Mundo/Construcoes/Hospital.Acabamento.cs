using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os acabamentos do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3: a arquitetura final), por
    /// setor (<see cref="PlantaDoHospital.Acabamento"/>), na face de dentro de cada parede de cada cômodo, cortados nas portas
    /// e nas janelas: o rodapé, as barras pintadas, o bate-maca dos corredores, o lambri e o rodameio da administração, o
    /// azulejo com o rejunte e o listelo, as faixas do isolamento e as de aviso da A-01; as molduras das portas e das
    /// janelas, as persianas, as placas em cima das portas e as de saída; no chão, o rejunte dos azulejados, o piso elevado da
    /// TI, os ralos e as linhas coloridas do térreo; no teto, a grade do forro modular.
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>As placas do forro modular: 0,6 m em x e 1,2 m em z, na mesma grade em todos os cômodos (a partir de X0 e Z0).</summary>
        const float PlacaDoForroX = 0.6f, PlacaDoForroZ = 1.2f;
        /// <summary>O azulejo das paredes (0,3 m) e a lajota do chão dos azulejados (0,4 m).</summary>
        const float LadoDoAzulejo = 0.3f, LadoDaLajota = 0.4f;

        /// <summary>Um trecho de parede num lado de um cômodo (de lo a hi, na linha), com os vãos e o cômodo do outro lado (null: fora).</summary>
        sealed class Trecho
        {
            public float lo, hi;
            public List<Vao> vaos;
            public Comodo outro;
        }

        /// <summary>
        /// A face de dentro de um lado de um cômodo: 'o' é o ponto da face com 0 na coordenada da linha (assim, s é o x ou o z
        /// de verdade), 'u' corre ao longo dela e 'n' sai dela para dentro do cômodo.
        /// </summary>
        struct Plano
        {
            public Vector3 o, u, n;
            public bool emX;
        }

        readonly Dictionary<(Comodo, int), List<Trecho>> _trechos = new Dictionary<(Comodo, int), List<Trecho>>();
        readonly Dictionary<Comodo, int> _portasDe = new Dictionary<Comodo, int>();
        readonly List<Vector3> _q = new List<Vector3>(4);
        readonly List<float> _xs = new List<float>(), _ys = new List<float>();
        readonly List<Rect> _furos = new List<Rect>(), _livres = new List<Rect>();

        static Vector3 P(Plano p, float s, float y, float off) => p.o + p.u * s + Vector3.up * y + p.n * off;

        /// <summary>A direção em que se lê um texto numa face que olha para 'n' (a direita de quem olha para ela).</summary>
        static Vector3 Leitura(Vector3 n) => Vector3.Cross(n, Vector3.up);

        Plano PlanoDe(Comodo k, int lado)
        {
            float e = m.esp / 2f, b = Base(_parte);
            switch (lado)
            {
                case 0: return new Plano { o = new Vector3(0f, b, k.z0 + e), u = Vector3.right, n = Vector3.forward, emX = true };
                case 1: return new Plano { o = new Vector3(k.x1 - e, b, 0f), u = Vector3.forward, n = Vector3.left, emX = false };
                case 2: return new Plano { o = new Vector3(0f, b, k.z1 - e), u = Vector3.right, n = Vector3.back, emX = true };
                default: return new Plano { o = new Vector3(k.x0 + e, b, 0f), u = Vector3.forward, n = Vector3.right, emX = false };
            }
        }

        // ------------------------------------------------------------ os trechos de parede do andar

        // de cada lado de cada cômodo, os trechos de parede (as divisas que têm parede e as bordas de fora), com os vãos; e
        // quantas portas cada cômodo tem (para a placa: veja LadoDaPlaca)
        void TrechosDoAndar(Andar an)
        {
            _trechos.Clear();
            void Por(Comodo k, int lado, Trecho t)
            {
                if (!_trechos.TryGetValue((k, lado), out var l)) _trechos[(k, lado)] = l = new List<Trecho>();
                l.Add(t);
            }
            foreach (var d in _divisas)
            {
                var vaos = new List<Vao>();
                if (!VaosDaDivisa(an, d, vaos)) continue;
                // numa linha em z, o 'a' fica a oeste (é o lado leste dele); numa em x, o 'a' fica na frente (o lado do fundo dele)
                Por(d.a, d.vertical ? 1 : 2, new Trecho { lo = d.lo, hi = d.hi, vaos = vaos, outro = d.b });
                Por(d.b, d.vertical ? 3 : 0, new Trecho { lo = d.lo, hi = d.hi, vaos = vaos, outro = d.a });
            }
            foreach (var b in _bordas)
            {
                var vaos = new List<Vao>();
                VaosDaBorda(an, b, vaos, out _, out _);
                Por(b.k, LadoDaBorda(b), new Trecho { lo = b.lo, hi = b.hi, vaos = vaos });
            }
            _portasDe.Clear();
            foreach (var p in an.portas)
            {
                if (p.aberto) continue;
                _portasDe[p.a] = (_portasDe.TryGetValue(p.a, out int na) ? na : 0) + 1;
                if (p.b != null) _portasDe[p.b] = (_portasDe.TryGetValue(p.b, out int nb) ? nb : 0) + 1;
            }
        }

        // se o cômodo k tem parede no canto do começo (ou do fim) do lado 'lado': o lado vizinho tem um trecho ali
        bool ParedeNoCanto(Comodo k, int lado, bool inicio)
        {
            int outro;
            float s;
            switch (lado)
            {
                case 0: outro = inicio ? 3 : 1; s = k.z0 + 0.05f; break;
                case 2: outro = inicio ? 3 : 1; s = k.z1 - 0.05f; break;
                case 1: outro = inicio ? 0 : 2; s = k.x1 - 0.05f; break;
                default: outro = inicio ? 0 : 2; s = k.x0 + 0.05f; break;
            }
            if (!_trechos.TryGetValue((k, outro), out var l)) return false;
            foreach (var t in l)
                if (s >= t.lo - 0.001f && s <= t.hi + 0.001f) return true;
            return false;
        }

        // ------------------------------------------------------------ os acabamentos do andar

        /// <summary>Os acabamentos de todos os cômodos do andar (as paredes, o chão e o teto) e as linhas coloridas do térreo.</summary>
        void Acabamentos(Andar an)
        {
            TrechosDoAndar(an);
            foreach (var k in an.comodos)
            {
                if (k.tipo == "elevador") continue;
                var ac = an.AcabamentoDe(k);
                for (int lado = 0; lado < 4; lado++)
                {
                    if (!_trechos.TryGetValue((k, lado), out var lista)) continue;
                    // na escada, só em volta do patamar do andar (atrás dos lances, a barra ficava embaixo dos degraus)
                    if (k.tipo == "escada" && lado == 2) continue;
                    lista.Sort((x, y) => x.lo.CompareTo(y.lo));
                    var p = PlanoDe(k, lado);
                    bool emX = lado == 0 || lado == 2;
                    float kmin = emX ? k.x0 : k.z0, kmax = emX ? k.x1 : k.z1;
                    int i = 0;
                    while (i < lista.Count)
                    {
                        // os trechos seguidos viram um só (a parede continua de um vizinho para o outro)
                        int j = i;
                        while (j + 1 < lista.Count && Mathf.Abs(lista[j + 1].lo - lista[j].hi) < 0.01f) j++;
                        float r0 = lista[i].lo, r1 = lista[j].hi;
                        // nos cantos com parede, a face acaba 10 cm antes (na face da outra); onde a parede acaba solta ou
                        // numa quina de fora, ela avançou meia parede (Avanco), e as faixas vão junto, dobrando a quina
                        float c = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
                        bool solto0 = false, solto1 = false;
                        if (Mathf.Abs(r0 - kmin) < 0.01f && ParedeNoCanto(k, lado, true)) r0 += m.esp / 2f;
                        else if (!ContinuaNaLinha(an, !emX, c, r0, -1)) { r0 -= m.esp / 2f; solto0 = true; }
                        if (Mathf.Abs(r1 - kmax) < 0.01f && ParedeNoCanto(k, lado, false)) r1 -= m.esp / 2f;
                        else if (!ContinuaNaLinha(an, !emX, c, r1, 1)) { r1 += m.esp / 2f; solto1 = true; }
                        if (k.tipo == "escada" && !emX) { r1 = Mathf.Min(r1, k.z0 + m.esp / 2f + Patamar); solto1 = false; }
                        _furos.Clear();
                        for (int t = i; t <= j; t++)
                            foreach (var v in lista[t].vaos) Furo(v);
                        _solto0 = solto0;
                        _solto1 = solto1;
                        if (r1 - r0 > 0.05f) Revestir(k, ac, p, r0, r1);
                        _solto0 = _solto1 = false;
                        for (int t = i; t <= j; t++) NosVaos(an, k, p, lista[t]);
                        i = j + 1;
                    }
                }
                PisoEForro(an, k, ac);
            }
            if (_parte == Terreo) LinhasNoChao();
        }

        // o furo de um vão nas faixas (com a moldura em volta das portas e das janelas)
        void Furo(Vao v) => _furos.Add(FuroDe(v));

        static Rect FuroDe(Vao v)
        {
            if (v.janela) return Rect.MinMaxRect(v.de - 0.06f, v.baixo - 0.07f, v.ate + 0.06f, v.alto + 0.06f);
            bool moldura = v.porta.folhas > 0 || v.porta.tipo == TipoDePorta.Elevador;
            float e = moldura ? (v.porta.tipo == TipoDePorta.Elevador ? 0.1f : 0.08f) : 0f;
            return Rect.MinMaxRect(v.de - e, -1f, v.ate + e, v.alto + e);
        }

        /// <summary>As faixas de um trecho de parede (de r0 a r1 na face de dentro do cômodo k), pelo acabamento.</summary>
        void Revestir(Comodo k, Acabamento ac, Plano p, float r0, float r1)
        {
            switch (ac)
            {
                case Acabamento.Circulacao:
                    Barra(p, r0, r1, 0f, 0.12f, 0.015f, "Rodape");
                    Barra(p, r0, r1, 0.12f, 1.0f, 0f, "ParedeVerde");
                    // o bate-maca: a régua de proteção na altura das macas e das cadeiras de rodas
                    Barra(p, r0, r1, 0.82f, 1.0f, 0.04f, "Batente");
                    break;
                case Acabamento.Medico:
                    Barra(p, r0, r1, 0f, 0.12f, 0.015f, "ParedeVerdeEscura");
                    Barra(p, r0, r1, 0.12f, 1.2f, 0f, "ParedeVerde");
                    Barra(p, r0, r1, 1.2f, 1.24f, 0.01f, "Batente");
                    break;
                case Acabamento.Azulejo:
                    Azulejar(p, r0, r1, k.tipo == "copa" || k.tipo == "limpeza" || k.tipo == "lavanderia" || k.tipo == "residuos" ? 1.5f : 2.1f);
                    break;
                case Acabamento.Administracao:
                    bool diretor = k.tipo == "diretor";
                    Barra(p, r0, r1, 0f, 0.1f, 0.015f, "Rodape");
                    if (diretor) Barra(p, r0, r1, 0.1f, 1.0f, 0.012f, "Madeira");
                    else Barra(p, r0, r1, 0.1f, 0.9f, 0f, "ParedeBege");
                    Barra(p, r0, r1, diretor ? 1.0f : 0.9f, diretor ? 1.06f : 0.95f, 0.025f, "Madeira");
                    break;
                case Acabamento.Isolamento:
                    Barra(p, r0, r1, 0f, 0.12f, 0.015f, "ParedeVerdeEscura");
                    Barra(p, r0, r1, 1.05f, 1.2f, 0f, "PlacaVermelha");
                    break;
                case Acabamento.Restrito:
                    Barra(p, r0, r1, 0f, 0.95f, 0f, "Bunker");
                    Listras(p, r0, r1, 0.95f, 1.15f);
                    break;
                case Acabamento.Escada:
                    Barra(p, r0, r1, 0f, 1.2f, 0f, "ParedeVerdeEscura");
                    Barra(p, r0, r1, 1.2f, 1.25f, 0.008f, "Batente");
                    break;
                default: // o serviço: a barra verde-escura com o friso amarelo, como no bunker
                    Barra(p, r0, r1, 0f, 1.2f, 0f, "ParedeVerdeEscura");
                    Barra(p, r0, r1, 1.2f, 1.26f, 0.008f, "Faixa");
                    break;
            }
        }

        /// <summary>
        /// Uma faixa de y0 a y1 ao longo do trecho, menos os furos: pintada (sai 0: a 'off' da face) ou em relevo (sai &gt; 0:
        /// a caixa da face até 'sai').
        /// </summary>
        void Barra(Plano p, float r0, float r1, float y0, float y1, float sai, Cor cor, float off = 0.003f)
        {
            Cobrir(r0, r1, y0, y1, _livres);
            foreach (var r in _livres)
            {
                if (sai <= 0f) { Pintura(p, r.xMin, r.xMax, r.yMin, r.yMax, off, cor); continue; }
                // o relevo, na ponta solta, passa da quina o quanto ele sai (encontra o do outro lado)
                float a = r.xMin - (_solto0 && r.xMin <= r0 + 0.001f ? sai : 0f), b = r.xMax + (_solto1 && r.xMax >= r1 - 0.001f ? sai : 0f);
                Relevo(p, a, b, r.yMin, r.yMax, sai, cor);
            }
        }

        // se as pontas do trecho que está sendo revestido são soltas (as faixas em relevo dobram a quina)
        bool _solto0, _solto1;

        // as faixas de aviso, amarelas e pretas, em blocos de 20 cm na grade de cada linha
        void Listras(Plano p, float r0, float r1, float y0, float y1)
        {
            const float bloco = 0.2f;
            Cobrir(r0, r1, y0, y1, _livres);
            foreach (var r in _livres)
                for (float s = Mathf.Floor(r.xMin / bloco) * bloco; s < r.xMax; s += bloco)
                {
                    float a = Mathf.Max(s, r.xMin), b = Mathf.Min(s + bloco, r.xMax);
                    if (b - a < 0.005f) continue;
                    Pintura(p, a, b, r.yMin, r.yMax, 0.004f, (Mathf.RoundToInt(s / bloco) & 1) == 0 ? "Faixa" : "Pneu");
                }
        }

        // o azulejo até 'alto' (com o rejunte a cada 30 cm, nas linhas de verdade de x, z e y), o listelo verde a 1,2 m e o
        // arremate de cima
        void Azulejar(Plano p, float r0, float r1, float alto)
        {
            Cobrir(r0, r1, 0f, alto, _livres);
            foreach (var r in _livres)
            {
                Pintura(p, r.xMin, r.xMax, r.yMin, r.yMax, 0.003f, "Azulejo");
                for (float s = Mathf.Ceil(r.xMin / LadoDoAzulejo) * LadoDoAzulejo; s < r.xMax - 0.01f; s += LadoDoAzulejo)
                    if (s > r.xMin + 0.01f) Pintura(p, s - 0.003f, s + 0.003f, r.yMin, r.yMax, 0.0045f, "ConcretoClaro");
                for (float y = Mathf.Ceil(r.yMin / LadoDoAzulejo) * LadoDoAzulejo; y < r.yMax - 0.01f; y += LadoDoAzulejo)
                    if (y > r.yMin + 0.01f) Pintura(p, r.xMin, r.xMax, y - 0.003f, y + 0.003f, 0.0045f, "ConcretoClaro");
            }
            Barra(p, r0, r1, 1.205f, 1.295f, 0f, "ParedeVerdeEscura", 0.0038f);
            Barra(p, r0, r1, alto, alto + 0.03f, 0.012f, "Batente");
        }

        bool Livre(float x, float y)
        {
            foreach (var f in _furos)
                if (f.Contains(new Vector2(x, y))) return false;
            return true;
        }

        /// <summary>
        /// O retângulo [s0, s1] × [y0, y1] da face menos os furos (as portas, as janelas e as molduras), em retângulos: as
        /// linhas cortadas nas bordas dos furos e os pedaços livres de cada linha juntados. Os pedaços com menos de 3 cm de
        /// largura ou de 5 cm de altura ficam de fora: embaixo de uma janela, o bate-maca não vira uma lasca.
        /// </summary>
        void Cobrir(float s0, float s1, float y0, float y1, List<Rect> saida)
        {
            saida.Clear();
            if (s1 - s0 < 0.03f || y1 - y0 < 0.004f) return;
            _xs.Clear();
            _ys.Clear();
            _xs.Add(s0); _xs.Add(s1); _ys.Add(y0); _ys.Add(y1);
            foreach (var f in _furos)
            {
                if (f.xMax <= s0 || f.xMin >= s1 || f.yMax <= y0 || f.yMin >= y1) continue;
                if (f.xMin > s0) _xs.Add(f.xMin);
                if (f.xMax < s1) _xs.Add(f.xMax);
                if (f.yMin > y0) _ys.Add(f.yMin);
                if (f.yMax < y1) _ys.Add(f.yMax);
            }
            _xs.Sort();
            _ys.Sort();
            float minimo = Mathf.Min(0.05f, y1 - y0 - 0.001f);
            for (int j = 0; j + 1 < _ys.Count; j++)
            {
                float ya = _ys[j], yb = _ys[j + 1];
                if (yb - ya < minimo) continue;
                float cy = (ya + yb) / 2f;
                int i = 0;
                while (i + 1 < _xs.Count)
                {
                    int k = i;
                    while (k + 1 < _xs.Count && Livre((_xs[k] + _xs[k + 1]) / 2f, cy)) k++;
                    if (k > i && _xs[k] - _xs[i] >= 0.03f) saida.Add(Rect.MinMaxRect(_xs[i], ya, _xs[k], yb));
                    i = Mathf.Max(k, i + 1);
                }
            }
        }

        // um retângulo pintado na face, a 'off' dela
        void Pintura(Plano p, float s0, float s1, float y0, float y1, float off, Cor cor)
        {
            _q.Clear();
            _q.Add(P(p, s0, y0, off)); _q.Add(P(p, s1, y0, off)); _q.Add(P(p, s1, y1, off)); _q.Add(P(p, s0, y1, off));
            vis.Face(_q, p.n, cor);
        }

        // uma caixa da face até 'sai' dela (com o fundo 1 mm para dentro da parede)
        void Relevo(Plano p, float s0, float s1, float y0, float y1, float sai, Cor cor)
        {
            Caixa(P(p, (s0 + s1) / 2f, (y0 + y1) / 2f, sai / 2f - 0.001f), new Vector3(s1 - s0, y1 - y0, sai + 0.002f), cor, p.emX ? 0f : -90f);
        }

        // ------------------------------------------------------------ as molduras, as persianas e as placas

        /// <summary>Os cômodos com persiana nas janelas de fora.</summary>
        static readonly HashSet<string> ComPersiana = new HashSet<string>
        {
            "escritorio", "diretor", "secretaria", "rh", "planejamento", "reuniao", "seguranca", "ti", "arquivo", "copa", "descanso",
            "consultorio", "enfermaria", "uti", "observacao", "coleta",
        };

        // o que vai em volta de cada vão do trecho, do lado do cômodo k: a moldura, a persiana, a placa e a de saída
        void NosVaos(Andar an, Comodo k, Plano p, Trecho t)
        {
            foreach (var v in t.vaos)
            {
                if (v.janela)
                {
                    Moldura(p, v, v.dentro ? "Metal" : "Batente", 0.06f, 0.015f, true);
                    if (!v.dentro && ComPersiana.Contains(k.tipo) && Chance(0.85f)) Persiana(p, v.de, v.ate, v.baixo, v.alto);
                    continue;
                }
                var porta = v.porta;
                float meio = (v.de + v.ate) / 2f;
                if (porta.tipo == TipoDePorta.Elevador) Moldura(p, v, "Cromo", 0.1f, 0.02f, false);
                else if (porta.folhas > 0) Moldura(p, v, porta.tipo == TipoDePorta.Madeira ? "Madeira" : "Metal", 0.08f, 0.02f, false);
                if (porta.b != null && LadoDaPlaca(an, porta) == k) PlacaDaPorta(p, meio, v.alto, an.placas[t.outro]);
                // as placas de saída: em cima da porta da escada, do lado do corredor, e em cima das saídas para fora
                if (t.outro != null && t.outro.tipo == "escada" && DeCirculacao.Contains(k.tipo)) Saida(p, meio, v.alto + 0.45f);
                if (t.outro == null && porta.tipo != TipoDePorta.Portao && (k.tipo == "corredor" || k.tipo == "servico")) Saida(p, meio, v.alto + 0.18f);
            }
        }

        // a moldura de um vão: os dois batentes e a verga (nas janelas, do peitoril para cima)
        void Moldura(Plano p, Vao v, Cor cor, float largura, float sai, bool janela)
        {
            float y0 = janela ? v.baixo : 0f;
            Relevo(p, v.de - largura, v.de, y0, v.alto + largura, sai, cor);
            Relevo(p, v.ate, v.ate + largura, y0, v.alto + largura, sai, cor);
            Relevo(p, v.de, v.ate, v.alto, v.alto + largura, sai, cor);
        }

        // a persiana de lâminas por dentro da janela: a caixa de cima, as lâminas (baixadas até uma altura sorteada, umas
        // faltando e, em três de cada dez, tortas: um cordão arrebentou e um lado caiu) e o cordão
        void Persiana(Plano p, float s0, float s1, float y0, float y1)
        {
            float topo = y1 + 0.02f;
            Relevo(p, s0 - 0.04f, s1 + 0.04f, topo, topo + 0.06f, 0.08f, "PlacaBranca");
            float baixa = Mathf.Lerp(y1 - 0.2f, y0 + 0.04f, (float)rnd.NextDouble());
            float torto = Chance(0.3f) ? Faixa(0.08f, 0.4f) : 0f;
            bool esquerda = Chance(0.5f);
            float alto = Mathf.Max(0.05f, topo - baixa);
            for (float y = topo - 0.05f; y > baixa; y -= 0.05f)
            {
                if (Chance(0.05f)) continue;
                float q = torto * (topo - y) / alto;
                float ya = y - (esquerda ? q : 0f), yb = y - (esquerda ? 0f : q);
                _q.Clear();
                _q.Add(P(p, s0 + 0.01f, ya - 0.012f, 0.035f)); _q.Add(P(p, s1 - 0.01f, yb - 0.012f, 0.035f));
                _q.Add(P(p, s1 - 0.01f, yb + 0.012f, 0.06f)); _q.Add(P(p, s0 + 0.01f, ya + 0.012f, 0.06f));
                vis.Face(_q, p.n + Vector3.up, "PlacaBranca");
                vis.Face(_q, -p.n - Vector3.up, "PlacaBranca");
            }
            if (torto <= 0f)
                Caixa(P(p, (s0 + s1) / 2f, baixa - 0.01f, 0.05f), new Vector3(s1 - s0 - 0.02f, 0.025f, 0.03f), "PlacaBranca", p.emX ? 0f : -90f);
            Tubo(P(p, s1 - 0.06f, topo, 0.07f), P(p, s1 - 0.06f, Mathf.Max(y0 + 0.2f, baixa + 0.3f), 0.07f), 0.004f, "PlacaBranca");
        }

        static float Area(Comodo k) => (k.x1 - k.x0) * (k.z1 - k.z0);

        /// <summary>
        /// O cômodo do lado em que vai a placa de uma porta (a placa diz o nome do outro), ou null: do lado da circulação; entre
        /// dois cômodos que não são, do lado do que tem mais portas (por onde se chega) ou, empatados, do maior. Sem placa nas
        /// passagens, nas portas do elevador, entre dois de circulação e se o outro não tem nome.
        /// </summary>
        Comodo LadoDaPlaca(Andar an, Porta p)
        {
            if (p.b == null || p.aberto || p.folhas == 0 || p.tipo == TipoDePorta.Elevador) return null;
            bool ca = DeCirculacao.Contains(p.a.tipo), cb = DeCirculacao.Contains(p.b.tipo);
            if (ca && cb) return null;
            Comodo s;
            if (ca != cb) s = ca ? p.a : p.b;
            else
            {
                int na = _portasDe.TryGetValue(p.a, out int x) ? x : 0, nb = _portasDe.TryGetValue(p.b, out int y) ? y : 0;
                s = na != nb ? (na > nb ? p.a : p.b) : (Area(p.a) >= Area(p.b) ? p.a : p.b);
            }
            var t = s == p.a ? p.b : p.a;
            return an.placas.ContainsKey(t) ? s : null;
        }

        // a placa em cima da porta: verde-escura, com as letras brancas
        void PlacaDaPorta(Plano p, float meio, float alto, string texto)
        {
            const float h = 0.085f, altura = 0.22f;
            float larg = Letras.Largura(texto, h) + 0.2f, y0 = alto + 0.13f;
            Relevo(p, meio - larg / 2f, meio + larg / 2f, y0, y0 + altura, 0.014f, "PlacaVerde");
            Letreiro(texto, P(p, meio, y0 + (altura - h) / 2f, 0f), Leitura(p.n), p.n, h, "PlacaBranca", plano: true, sai: 0.016f);
        }

        // a placa de saída: a caixinha branca com o EXIT vermelho (apagada: não há luz)
        void Saida(Plano p, float meio, float y)
        {
            Relevo(p, meio - 0.2f, meio + 0.2f, y, y + 0.18f, 0.06f, "PlacaBranca");
            Letreiro("EXIT", P(p, meio, y + 0.04f, 0f), Leitura(p.n), p.n, 0.1f, "PlacaVermelha", plano: true, sai: 0.062f);
        }

        // ------------------------------------------------------------ o chão e o teto de cada cômodo

        // no chão: o rejunte dos azulejados, a grade do piso elevado da TI e os ralos; no teto, a grade do forro modular
        void PisoEForro(Andar an, Comodo k, Acabamento ac)
        {
            if (k.tipo == "escada") return;
            float b = Base(_parte);
            float x0 = k.x0 + Recuo(an, k, 3), x1 = k.x1 - Recuo(an, k, 1), z0 = k.z0 + Recuo(an, k, 0), z1 = k.z1 - Recuo(an, k, 2);
            float passo = ac == Acabamento.Azulejo ? LadoDaLajota : k.tipo == "ti" ? 0.6f : 0f;
            if (passo > 0f)
            {
                string cor = k.tipo == "ti" ? "Metal" : "Concreto";
                float y = b + 0.0075f;
                for (float x = X0 + Mathf.Ceil((x0 - X0) / passo) * passo; x < x1 - 0.01f; x += passo)
                    if (x > x0 + 0.01f) NoChao(x - 0.004f, z0, x + 0.004f, z1, y, cor);
                for (float z = Z0 + Mathf.Ceil((z0 - Z0) / passo) * passo; z < z1 - 0.01f; z += passo)
                    if (z > z0 + 0.01f) NoChao(x0, z - 0.004f, x1, z + 0.004f, y, cor);
            }
            if (k.tipo == "autopsia" || k.tipo == "necroterio" || k.tipo == "banheiro" || k.tipo == "camaras" || k.tipo == "limpeza" || k.tipo == "lavanderia")
            {
                // o ralo no meio, numa lajota
                float cx = X0 + (Mathf.Floor((k.Cx - X0) / LadoDaLajota) + 0.5f) * LadoDaLajota, cz = Z0 + (Mathf.Floor((k.Cz - Z0) / LadoDaLajota) + 0.5f) * LadoDaLajota;
                NoChao(cx - 0.13f, cz - 0.13f, cx + 0.13f, cz + 0.13f, b + 0.008f, "Metal");
                for (float d = -0.09f; d < 0.1f; d += 0.045f) NoChao(cx - 0.1f, cz + d - 0.008f, cx + 0.1f, cz + d + 0.008f, b + 0.0085f, "Pneu");
            }
            if (!ComForro(ac, k.tipo)) return;
            float teto = !Anexo(k) ? Teto(_parte) : TopoDaEntrada - LajeDoAnexo;
            for (float x = X0 + Mathf.Ceil((x0 - X0) / PlacaDoForroX) * PlacaDoForroX; x < x1 - 0.01f; x += PlacaDoForroX)
                if (x > x0 + 0.01f) NoTeto(x - 0.012f, z0, x + 0.012f, z1, teto - 0.003f);
            for (float z = Z0 + Mathf.Ceil((z0 - Z0) / PlacaDoForroZ) * PlacaDoForroZ; z < z1 - 0.01f; z += PlacaDoForroZ)
                if (z > z0 + 0.01f) NoTeto(x0, z - 0.012f, x1, z + 0.012f, teto - 0.004f);
        }

        void NoChao(float x0, float z0, float x1, float z1, float y, Cor cor)
        {
            _q.Clear();
            _q.Add(new Vector3(x0, y, z0)); _q.Add(new Vector3(x1, y, z0)); _q.Add(new Vector3(x1, y, z1)); _q.Add(new Vector3(x0, y, z1));
            vis.Face(_q, Vector3.up, cor);
        }

        void NoTeto(float x0, float z0, float x1, float z1, float y)
        {
            _q.Clear();
            _q.Add(new Vector3(x0, y, z0)); _q.Add(new Vector3(x1, y, z0)); _q.Add(new Vector3(x1, y, z1)); _q.Add(new Vector3(x0, y, z1));
            vis.Face(_q, Vector3.down, "Batente");
        }

        /// <summary>
        /// As linhas coloridas no chão do térreo, da entrada: a vermelha até a emergência e a azul até o laboratório (as
        /// pontas, nas coordenadas da obra; cada uma passa pela sala de espera e pelo corredor da frente).
        /// </summary>
        static readonly (string cor, Vector2[] pontos)[] LinhasDoTerreo =
        {
            ("PlacaVermelha", new[] { new Vector2(-0.25f, -19.4f), new Vector2(-0.25f, -6.2f), new Vector2(-17.5f, -6.2f), new Vector2(-17.5f, -5.35f) }),
            ("ParedeAzul", new[] { new Vector2(0.25f, -19.4f), new Vector2(0.25f, -6.8f), new Vector2(16f, -6.8f), new Vector2(16f, -5.35f) }),
        };

        void LinhasNoChao()
        {
            const float meia = 0.05f;
            foreach (var (cor, pontos) in LinhasDoTerreo)
                for (int i = 0; i + 1 < pontos.Length; i++)
                {
                    Vector2 a = pontos[i], c = pontos[i + 1];
                    NoChao(Mathf.Min(a.x, c.x) - meia, Mathf.Min(a.y, c.y) - meia, Mathf.Max(a.x, c.x) + meia, Mathf.Max(a.y, c.y) + meia, 0.0088f + 0.0004f * i, cor);
                }
        }
    }
}
