using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O Hospital Principal levantado a partir da <see cref="PlantaDoHospital"/> (o Plano_Hospital.md da pasta de arte: a
    /// parte 2, o blockout, e a parte 3, a arquitetura final), como o <see cref="Bunker"/>, em partes:
    ///  - <b>a casca</b> (<see cref="MontarCasca"/>): as paredes de fora dos 4 andares (com as janelas na grade de 4 m, com a
    ///    moldura de alumínio e o vidro, e as portas de fora), as lajes, a cobertura com a platibanda, o núcleo (as paredes das
    ///    escadas e do poço, as portas corta-fogo e as do elevador), as três escadas em U, os anexos do térreo (a garagem das
    ///    ambulâncias e a entrada), a marquise, o embasamento com a varanda, os degraus e as rampas (Hospital.Fora.cs) e os
    ///    letreiros da fachada (Hospital.Placas.cs);
    ///  - <b>um andar por dentro</b> (<see cref="MontarAndar"/>): as paredes de dentro com as portas, as passagens e as
    ///    janelas de dentro, o piso de cada cômodo, os acabamentos de cada setor, o forro e as luminárias
    ///    (Hospital.Acabamento.cs), as placas (Hospital.Placas.cs), os balcões, as vagas da garagem e o poço do elevador com a
    ///    cabine parada (Hospital.Elevador.cs).
    /// As paredes saem das linhas da planta (<see cref="PlantaDoHospital.Linhas"/>): entre dois cômodos, a de dentro; na
    /// borda em que nenhum encosta, a de fora. Os vãos de cada linha (<see cref="Vao"/>) saem de um lugar só, para a casca e o
    /// andar verem as mesmas portas e janelas. Coordenadas: o meio no centro, a frente em -z, o chão do térreo em y = 0.
    /// </summary>
    public sealed partial class Hospital : Obra
    {
        public readonly Andar[] andares = new Andar[Andares];
        readonly int _parte;

        // O hospital não tem energia (07/11/2026, a pedido: "o hospital não vai ter energia"): as luminárias, as arandelas e a
        // luz da antena ficam, apagadas, e nenhuma luz acende; dentro, só o dia pelas janelas, a lua e a lanterna.

        static readonly Cor CorDaLaje = Cor.PorNormal("Concreto", "Concreto", "Teto");
        static readonly Cor CorDoPatamar = Cor.PorNormal("Concreto", "ConcretoClaro", "ConcretoClaro");
        const string CorDoCorrimao = "Cromo";

        readonly List<Divisa> _divisas = new List<Divisa>();
        readonly List<Borda> _bordas = new List<Borda>();
        readonly List<Vao> _vaos = new List<Vao>();

        /// <summary>A casca (parte -1) ou o andar 'parte' (0 = o subsolo ... 3 = o 3º andar).</summary>
        readonly int _semente;

        public Hospital(Medidas medidas, int parte, int semente = 1)
            : base(semente * 37 + parte, medidas, TamanhoDaMalha.Media)
        {
            _parte = parte;
            _semente = semente;
            for (int f = 0; f < Andares; f++) andares[f] = DoAndar(f);
        }

        // ------------------------------------------------------------ cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.y > TopoDaCobertura + 0.05f || q.y < Base(0) - GrossuraDaLaje - 0.05f) return null;
            // os anexos do térreo, até o teto deles (mais alto que o do térreo, na garagem)
            if (q.y > -GrossuraDaLaje && q.y < TopoDaGaragem)
                foreach (var k in andares[Terreo].comodos)
                    if (Anexo(k) && k.Contem(q.x, q.z)) return q.y < (k.tipo == "garagem" ? TopoDaGaragem : TopoDaEntrada) ? k : null;
            var c = andares[AndarEm(q.y)].Em(q.x, q.z);
            return c != null && Anexo(c) ? null : c;
        }

        // de fora: o tijolo aparente (o shader do abandono desenha os tijolos), o concreto embaixo do chão e na entrada (a de
        // vidro, embaixo da torre); o concreto da fachada (as faixas, as pilastras, a cornija, a torre) vem em relevo por cima
        protected override string CorDeFora(Vector3 q)
        {
            if (q.y < -0.05f) return "Concreto";
            if (q.x > Entrada.xMin - 0.3f && q.x < Entrada.xMax + 0.3f && q.z < Z0 + 0.05f && q.y < TopoDaEntrada + 0.4f) return "ConcretoClaro";
            return "TijoloAparente";
        }
        protected override string CorDeDentro(object k, Vector3 q) => ((Comodo)k).papel ?? "ParedeInterna";

        // ------------------------------------------------------------ a casca

        /// <summary>
        /// A casca: as paredes de fora (com as janelas e as portas), as lajes, o núcleo, as escadas, os anexos, a marquise, a
        /// cobertura, o embasamento (a varanda da entrada, os degraus e as rampas) e os letreiros da fachada.
        /// </summary>
        public void MontarCasca()
        {
            // as linhas primeiro (perto delas, a face é o batente das portas e das janelas)
            for (int f = 0; f < Andares; f++)
            {
                var an = andares[f];
                Linhas(an, _divisas, _bordas);
                foreach (var b in _bordas)
                {
                    var (y0, y1) = AlturaDeFora(f, b.k);
                    RegistrarLinha(Ponta(b, b.lo), Ponta(b, b.hi), y0, y1);
                }
                foreach (var d in _divisas)
                    if (DoNucleo(d.a) || DoNucleo(d.b)) RegistrarLinha(Ponta(d, d.lo), Ponta(d, d.hi), Base(f), AlturaDoNucleo(f, d));
            }
            for (int f = 0; f < Andares; f++)
            {
                var an = andares[f];
                Linhas(an, _divisas, _bordas);
                foreach (var b in _bordas) ParedeDeFora(an, b);
                foreach (var d in _divisas)
                    if (DoNucleo(d.a) || DoNucleo(d.b)) ParedeDeDentro(an, d, AlturaDoNucleo(f, d));
            }
            Lajes();
            EscadasDoHospital();
            MarquiseDaEntrada();
            Fachada();
            EmbasamentoDoHospital();
            LetreirosDeFora();
        }

        static Vector2 Ponta(Divisa d, float s) => d.vertical ? new Vector2(d.c, s) : new Vector2(s, d.c);
        static Vector2 Ponta(Borda b, float s) => b.vertical ? new Vector2(b.c, s) : new Vector2(s, b.c);

        /// <summary>O lado do cômodo em que fica a borda: 0 a frente (z0), 1 o leste (x1), 2 o fundo (z1), 3 o oeste (x0).</summary>
        static int LadoDaBorda(Borda b) => b.vertical ? (b.fora > 0 ? 1 : 3) : (b.fora < 0 ? 0 : 2);

        // as paredes de fora: do fundo da laje do andar até o teto dele (no último andar, até o alto da platibanda); nos
        // anexos, da laje do térreo até o alto deles, com uma mureta
        static (float y0, float y1) AlturaDeFora(int f, Comodo k)
        {
            float y0 = Base(f) - GrossuraDaLaje;
            if (Anexo(k)) return (y0, k.tipo == "garagem" ? TopoDaGaragem + 0.3f : TopoDaEntrada + 0.2f);
            return (y0, f == Andares - 1 ? TopoDaCobertura + Platibanda : Teto(f));
        }

        // as paredes do núcleo passam pela laje de cima (que tem o vão), menos no último andar dele, em que param no teto
        static float AlturaDoNucleo(int f, Divisa d)
        {
            var k = DoNucleo(d.a) ? d.a : d.b;
            return f >= UltimoAndar(k) ? Teto(f) : Base(f + 1);
        }

        float Largura(Porta p) => p.largura > 0f ? p.largura : p.folhas == 2 ? 2.0f : p.folhas == 0 ? m.passagem : m.porta;

        /// <summary>
        /// O ajuste da ponta de uma parede de fora numa quina: nas quinas de fora (convexas), a parede em z vai até a quina
        /// (devolve -e: cresce) e a em x para antes dela (+e: encolhe), para elas não se cobrirem com as faces no mesmo plano;
        /// nas de dentro (côncavas), as duas vão até a linha da outra (o que sobra fica escondido dentro da outra).
        /// </summary>
        float AjusteDaQuina(Borda b, float ponta)
        {
            float e = m.esp / 2f;
            Vector2 P = Ponta(b, ponta);
            foreach (var o in _bordas)
            {
                if (o.vertical == b.vertical) continue;
                if ((Ponta(o, o.lo) - P).sqrMagnitude > 1e-4f && (Ponta(o, o.hi) - P).sqrMagnitude > 1e-4f) continue;
                Borda V = b.vertical ? b : o, H = b.vertical ? o : b;
                float dh = Mathf.Abs(H.lo - V.c) < 0.01f ? 1f : -1f;
                if (V.fora * dh >= 0f) return 0f;
                return b.vertical ? -e : e;
            }
            return 0f;
        }

        // ------------------------------------------------------------ os vãos de cada linha

        /// <summary>
        /// Um vão numa linha de parede, nas coordenadas da linha (x numa linha em x, z numa em z): de 'de' a 'ate' e de
        /// 'baixo' a 'alto' (a partir do chão do andar), com a porta (null numa janela). 'dentro': uma janela de dentro (o
        /// vidro do comando do raio-X, as de observação do isolamento).
        /// </summary>
        struct Vao
        {
            public float de, ate, baixo, alto;
            public Porta porta;
            public bool janela, dentro;
        }

        /// <summary>
        /// Os vãos de uma borda (a parede de fora): as portas de fora e as janelas (as da grade de 4 m, só onde a janela cabe
        /// inteira longe das portas; na entrada, os vidros dos lados e os dos dois lados da porta). Devolve também as pontas
        /// da parede, com o ajuste das quinas.
        /// </summary>
        void VaosDaBorda(Andar an, Borda b, List<Vao> vaos, out float lo, out float hi)
        {
            vaos.Clear();
            var k = b.k;
            lo = b.lo + AjusteDaQuina(b, b.lo);
            hi = b.hi - AjusteDaQuina(b, b.hi);
            int lado = LadoDaBorda(b);
            foreach (var p in an.portas)
            {
                if (p.b != null || p.a != k || p.lado != lado || p.pos < b.lo || p.pos > b.hi) continue;
                float larg = Largura(p), alto = p.alto > 0f ? p.alto : m.alturaPorta;
                vaos.Add(new Vao { de = p.pos - larg / 2f, ate = p.pos + larg / 2f, alto = alto, porta = p });
            }
            int portas = vaos.Count;
            if (an.f == 0 || SemJanela.Contains(k.tipo)) return;
            if (k.tipo == "entrada")
            {
                if (b.vertical) vaos.Add(new Vao { de = lo + 0.5f, ate = hi - 0.5f, baixo = 0.3f, alto = 2.9f, janela = true });
                else
                    for (int i = 0; i < portas; i++)
                    {
                        var v = vaos[i];
                        vaos.Add(new Vao { de = lo + 0.4f, ate = v.de - 0.3f, baixo = 0.3f, alto = 2.9f, janela = true });
                        vaos.Add(new Vao { de = v.ate + 0.3f, ate = hi - 0.4f, baixo = 0.3f, alto = 2.9f, janela = true });
                    }
                return;
            }
            bool pequena = k.tipo == "banheiro";
            float hw = (pequena ? LarguraDaJanelaPequena : LarguraDaJanela) / 2f;
            float origem = b.vertical ? Z0 : X0, fim = b.vertical ? Z1 : X1;
            for (float centro = origem + Modulo / 2f; centro < fim; centro += Modulo)
            {
                if (centro - hw < b.lo + 0.35f || centro + hw > b.hi - 0.35f) continue;
                bool naPorta = false;
                for (int i = 0; i < portas; i++)
                    if (Mathf.Abs(centro - vaos[i].porta.pos) < hw + Largura(vaos[i].porta) / 2f + 0.3f) naPorta = true;
                if (naPorta) continue;
                vaos.Add(new Vao
                {
                    de = centro - hw, ate = centro + hw, janela = true,
                    baixo = pequena ? PeitorilPequeno : Peitoril, alto = pequena ? TopoDaJanelaPequena : TopoDaJanela,
                });
            }
        }

        /// <summary>
        /// Os vãos de uma divisa (a parede entre dois cômodos): as portas e as janelas de dentro; devolve false se os dois são
        /// abertos um para o outro (sem parede).
        /// </summary>
        bool VaosDaDivisa(Andar an, Divisa d, List<Vao> vaos)
        {
            vaos.Clear();
            float L = d.hi - d.lo;
            foreach (var p in PortasEntre(an, d.a, d.b))
            {
                if (p.aberto) return false;
                float larg = Largura(p);
                float s = Mathf.Clamp(p.pos - d.lo, larg / 2f + 0.15f, L - larg / 2f - 0.15f);
                float alto = p.alto > 0f ? p.alto : p.folhas == 0 && larg > 2.2f ? Mathf.Min(PeDireito - 0.4f, 2.8f) : m.alturaPorta;
                vaos.Add(new Vao { de = d.lo + s - larg / 2f, ate = d.lo + s + larg / 2f, alto = alto, porta = p });
            }
            foreach (var j in an.janelas)
            {
                if (!((j.a == d.a && j.b == d.b) || (j.a == d.b && j.b == d.a))) continue;
                float s = Mathf.Clamp(j.pos - d.lo, j.largura / 2f + 0.15f, L - j.largura / 2f - 0.15f);
                vaos.Add(new Vao { de = d.lo + s - j.largura / 2f, ate = d.lo + s + j.largura / 2f, baixo = j.baixo, alto = j.alto, janela = true, dentro = true });
            }
            return true;
        }

        // ------------------------------------------------------------ as paredes

        // uma parede de fora: a faixa da laje embaixo dela e a parede do chão do andar para cima, com as portas de fora (as
        // folhas abrem para fora) e as janelas (a moldura, o vidro e as tábuas: Hospital.Portas.cs)
        void ParedeDeFora(Andar an, Borda b)
        {
            int f = an.f;
            var k = b.k;
            var (y0, y1) = AlturaDeFora(f, k);
            float chao = Base(f);
            VaosDaBorda(an, b, _vaos, out float lo, out float hi);
            // de a para c, com o lado n (o de fora) para fora da borda
            bool sobe = b.vertical ? b.fora > 0 : b.fora < 0;
            Vector2 a = Ponta(b, sobe ? lo : hi), c = Ponta(b, sobe ? hi : lo);
            Abertura NaParede(Vao v) => new Abertura(sobe ? v.de - lo : hi - v.ate, v.ate - v.de, v.baixo, v.alto);

            LevantarParede(a, c, new List<Abertura>(), true, "laje", y0, chao - y0, "ConcretoClaro");
            var aberturas = new List<Abertura>();
            foreach (var v in _vaos) aberturas.Add(NaParede(v));
            var w = LevantarParede(a, c, aberturas, true, "fora", chao, y1 - chao, "ConcretoClaro");
            foreach (var v in _vaos)
            {
                if (v.porta != null) Folhas(an, w, NaParede(v), v.porta, 1);
                else JanelaDeFora(w, NaParede(v), k, f);
            }
        }

        static readonly HashSet<string> DeCirculacao = new HashSet<string> { "corredor", "recepcao", "hall", "posto", "espera", "entrada" };

        // para onde a folha abre: para dentro da escada (a corta-fogo), do cômodo que não é corredor ou, entre dois, do maior
        static Comodo ParaOndeAbre(Comodo A, Comodo B)
        {
            if (A.tipo == "escada") return A;
            if (B.tipo == "escada") return B;
            bool ca = DeCirculacao.Contains(A.tipo), cb = DeCirculacao.Contains(B.tipo);
            if (ca != cb) return ca ? B : A;
            return (A.x1 - A.x0) * (A.z1 - A.z0) >= (B.x1 - B.x0) * (B.z1 - B.z0) ? A : B;
        }

        // a parede entre dois cômodos (do chão até 'topo'), com as portas e as janelas de dentro; sem parede onde os dois
        // ficam abertos um para o outro
        void ParedeDeDentro(Andar an, Divisa d, float topo)
        {
            float baseY = Base(an.f);
            if (!VaosDaDivisa(an, d, _vaos)) return;
            // as pontas que acabam numa quina ou soltas avançam meia parede (Avanco)
            float e0 = Avanco(an, d, false), e1 = Avanco(an, d, true);
            var aberturas = new List<Abertura>();
            foreach (var v in _vaos) aberturas.Add(new Abertura(v.de - d.lo + e0, v.ate - v.de, v.baixo, v.alto));
            var w = LevantarParede(Ponta(d, d.lo - e0), Ponta(d, d.hi + e1), aberturas, false, "interna", baseY, topo - baseY, "Batente");
            // o lado n da parede: numa linha em z (vertical), o +x (o b); numa em x, o -z (o a)
            Comodo ladoN = d.vertical ? d.b : d.a;
            bool daEscada = d.a.tipo == "escada" || d.b.tipo == "escada";
            foreach (var v in _vaos)
            {
                var ab = new Abertura(v.de - d.lo + e0, v.ate - v.de, v.baixo, v.alto);
                if (v.porta != null) Folhas(an, w, ab, v.porta, ParaOndeAbre(d.a, d.b) == ladoN ? 1 : -1, daEscada);
                else JanelaDeDentro(an, w, ab, d);
            }
        }

        /// <summary>Se a linha (em z, se vertical; c, o x ou o z dela) continua com parede depois do ponto, para o lado do sentido.</summary>
        bool ContinuaNaLinha(Andar an, bool vertical, float c, float ponto, int sentido)
        {
            float s = ponto + sentido * 0.05f;
            foreach (var o in _divisas)
            {
                if (o.vertical != vertical || Mathf.Abs(o.c - c) > 0.01f || s < o.lo || s > o.hi) continue;
                bool aberto = false;
                foreach (var p in PortasEntre(an, o.a, o.b)) if (p.aberto) aberto = true;
                if (!aberto) return true;
            }
            foreach (var bd in _bordas)
                if (bd.vertical == vertical && Mathf.Abs(bd.c - c) < 0.01f && s >= bd.lo && s <= bd.hi) return true;
            return false;
        }

        // se o ponto fica numa parede de fora (a ponta de uma parede de dentro encostada nela)
        bool NaParedeDeFora(Vector2 ponto)
        {
            foreach (var bd in _bordas)
            {
                float s = bd.vertical ? ponto.y : ponto.x, c = bd.vertical ? ponto.x : ponto.y;
                if (Mathf.Abs(c - bd.c) < 0.01f && s >= bd.lo - 0.01f && s <= bd.hi + 0.01f) return true;
            }
            return false;
        }

        /// <summary>
        /// Quanto a parede da divisa avança além de uma ponta: meia parede onde ela acaba numa quina ou solta (sem isso, ficava
        /// um vão de 10 × 10 cm na quina, de cima a baixo: as duas paredes paravam no meio uma da outra; o usuário viu na
        /// quina do poço do elevador, na recepção); nada onde a linha continua com outra parede ou onde a ponta encosta numa
        /// parede de fora (a face dela ficaria no mesmo plano da fachada).
        /// Numa quina em L (a outra parede também acaba ali), só a parede em z avança; a em x recua meia parede, até a face
        /// da outra; e no T (a outra passa direto), nenhuma avança. Com as duas avançando, a ponta de uma ficava no mesmo
        /// plano da face da outra, e a faixa de 10 cm piscava de cima a baixo (07/11/2026, a pedido: "a parede perto da
        /// placa LAUNDRY está com bug de sobreposição na quina").
        /// </summary>
        float Avanco(Andar an, Divisa d, bool fim)
        {
            float ponta = fim ? d.hi : d.lo;
            if (ContinuaNaLinha(an, d.vertical, d.c, ponta, fim ? 1 : -1) || NaParedeDeFora(Ponta(d, ponta))) return 0f;
            // a parede que cruza a ponta (a linha do outro sentido, que passa pelo ponto)
            bool antes = ContinuaNaLinha(an, !d.vertical, ponta, d.c, -1), depois = ContinuaNaLinha(an, !d.vertical, ponta, d.c, 1);
            if (antes && depois) return 0f;
            if (antes || depois) return d.vertical ? m.esp / 2f : -m.esp / 2f;
            return m.esp / 2f;
        }

        // ------------------------------------------------------------ as lajes, as escadas e o que fica fora

        // as lajes dos 4 andares (com os vãos das escadas que chegam de baixo e o poço do elevador) e a da cobertura; nos
        // anexos, o chão (até a linha da parede do corpo, embaixo da parede que os separa) e a laje de cima
        void Lajes()
        {
            float e = m.esp / 2f;
            for (int f = 0; f < Andares; f++)
            {
                var vaos = new List<Rect>();
                if (f > 0)
                {
                    foreach (var (_, caixa, de, ate) in Escadas)
                        if (f > de && f <= ate) vaos.Add(VaoDaEscada(caixa));
                    vaos.Add(VaoDoElevador);
                }
                // as placas do forro que caíram no andar de baixo (a parte 5): o furo só no desenho (a colisão fica inteira) e
                // o vão do entreforro em cima de cada uma
                var caidas = f > 0 ? PlacasQueCairam(f - 1) : null;
                if (caidas == null || caidas.Count == 0)
                {
                    Laje(X0 + e, X1 - e, Z0 + e, Z1 - e, Base(f), GrossuraDaLaje, CorDaLaje, true, vaos);
                    continue;
                }
                Laje(X0 + e, X1 - e, Z0 + e, Z1 - e, Base(f), GrossuraDaLaje, CorDaLaje, true, vaos, visual: false);
                var furos = new List<Rect>(vaos);
                foreach (var (placa, _) in caidas) furos.Add(placa);
                Laje(X0 + e, X1 - e, Z0 + e, Z1 - e, Base(f), GrossuraDaLaje, CorDaLaje, false, furos);
                foreach (var (placa, _) in caidas) VaoDoForro(placa, Teto(f - 1));
            }
            Laje(X0 + e, X1 - e, Z0 + e, Z1 - e, TopoDaCobertura, GrossuraDaLaje, CorDaLaje, true);
            var g = GaragemDasAmbulancias;
            Laje(g.xMin + e, X0 + e, g.yMin + e, g.yMax - e, 0f, GrossuraDaLaje, "Concreto", true);
            Laje(g.xMin - e, X0, g.yMin - e, g.yMax + e, TopoDaGaragem, LajeDoAnexo, CorDaLaje, true);
            var en = Entrada;
            Laje(en.xMin + e, en.xMax - e, en.yMin + e, Z0 + e, 0f, GrossuraDaLaje, "Concreto", true);
            Laje(en.xMin - e, en.xMax + e, en.yMin - e, Z0, TopoDaEntrada, LajeDoAnexo, CorDaLaje, true);
        }

        /// <summary>
        /// As três escadas em U (as do canto do fundo, do subsolo ao 3º andar; a de serviço, do subsolo ao térreo): em cada
        /// subida, o lance de ida (do patamar do andar, junto da porta, para o fundo da caixa, do lado oeste), o patamar do
        /// meio e o lance de volta (do lado leste) até o patamar de cima; os corrimãos no vão do meio; no andar de cima, o
        /// guarda-corpo do lado em que nada sobe. A luminária (apagada) de cada patamar.
        /// </summary>
        void EscadasDoHospital()
        {
            foreach (var (_, c, de, ate) in Escadas)
            {
                float xi = Xi(c), xf = Xf(c), zi = Zi(c), zf = Zf(c), zL = ZL(c);
                float lance = (xf - xi - 0.2f) / 2f;
                float xA = xi + lance / 2f, xB = xf - lance / 2f;
                for (int f = de; f < ate; f++)
                {
                    float b = Base(f), t = Base(f + 1), meio = (b + t) / 2f, r = (meio - b) / Espelhos(meio - b);
                    float comp = Lance(new Vector2(xA, zL), new Vector2(0f, 1f), lance, b, meio, m.pisada, "Concreto", "ConcretoClaro");
                    float zM = zL + comp;
                    Laje(xi, xf, zM, zf, meio, 0.25f, CorDoPatamar, true);
                    Lance(new Vector2(xB, zM), new Vector2(0f, -1f), lance, meio, t, m.pisada, "Concreto", "ConcretoClaro");
                    float xa = xA + lance / 2f - 0.03f, xb = xB - lance / 2f + 0.03f;
                    GuardaCorpo(new Vector3(xa, b + r, zL + 0.15f), new Vector3(xa, meio - r, zM - 0.15f), 0.9f, CorDoCorrimao);
                    GuardaCorpo(new Vector3(xb, meio + r, zM - 0.15f), new Vector3(xb, t - r, zL + 0.15f), 0.9f, CorDoCorrimao);
                    LuminariaDoPatamar(c, f);
                }
                float topo = Base(ate);
                GuardaCorpo(new Vector3(xi, topo, zL + 0.03f), new Vector3(xi + lance + 0.1f, topo, zL + 0.03f), 1.0f, CorDoCorrimao);
                LuminariaDoPatamar(c, ate);
            }
        }

        // a luminária de cada patamar (apagada)
        void LuminariaDoPatamar(Rect c, int f)
        {
            var p = new Vector3(c.center.x, Base(f) + 2.95f, Zi(c) + 0.9f);
            Caixa(p + Vector3.up * 0.4f, new Vector3(0.9f, 0.06f, 0.18f), "Metal");
            Caixa(p + Vector3.up * 0.36f, new Vector3(0.8f, 0.02f, 0.1f), "Farol");
        }

        /// <summary>
        /// O pórtico na frente da entrada: a marquise grossa (60 cm, com a faixa na frente e o friso embaixo), presa na entrada
        /// e nos dois pilares quadrados de concreto da frente (com a base e o capitel), e as quatro luminárias embaixo.
        /// </summary>
        void MarquiseDaEntrada()
        {
            var r = Marquise;
            const float grossa = 0.6f, pilar = 0.7f;
            float baixo = TopoDaMarquise - grossa;
            Laje(r.xMin, r.xMax, r.yMin, r.yMax, TopoDaMarquise, grossa, Cor.PorNormal("ConcretoClaro", "ConcretoClaro", "Teto"), true);
            Laje(r.xMin - 0.08f, r.xMax + 0.08f, r.yMin - 0.08f, r.yMax, TopoDaMarquise + 0.08f, 0.14f, "ConcretoClaro");
            Laje(r.xMin + 0.1f, r.xMax - 0.1f, r.yMin + 0.1f, r.yMax, baixo + 0.001f, 0.06f, "Concreto");
            foreach (float s in new[] { -1f, 1f })
            {
                var pe = new Vector3(s * (r.xMax - 0.8f), 0f, r.yMin + 0.8f);
                Caixa(pe + Vector3.up * (baixo / 2f), new Vector3(pilar, baixo, pilar), "ConcretoClaro", colide: true);
                Caixa(pe + Vector3.up * 0.15f, new Vector3(pilar + 0.12f, 0.3f, pilar + 0.12f), "Concreto");
                Caixa(pe + Vector3.up * (baixo - 0.1f), new Vector3(pilar + 0.1f, 0.2f, pilar + 0.1f), "ConcretoClaro");
                foreach (float z in new[] { r.yMin + 2.2f, r.yMax - 2.0f })
                {
                    var c = new Vector3(s * 3.2f, baixo - 0.09f, z);
                    Caixa(c, new Vector3(1.3f, 0.06f, 0.5f), "Metal");
                    Caixa(c - new Vector3(0f, 0.035f, 0f), new Vector3(1.2f, 0.01f, 0.4f), "Farol");
                }
            }
        }

        // ------------------------------------------------------------ um andar por dentro

        /// <summary>
        /// O andar (a parte deste hospital): as paredes de dentro com as portas, os pisos, os acabamentos, o forro, as
        /// luminárias, as placas, os balcões e o poço do elevador.
        /// </summary>
        public void MontarAndar()
        {
            int f = _parte;
            var an = andares[f];
            Linhas(an, _divisas, _bordas);
            foreach (var d in _divisas)
                if (!DoNucleo(d.a) && !DoNucleo(d.b)) RegistrarLinha(Ponta(d, d.lo), Ponta(d, d.hi), Base(f), Teto(f));
            foreach (var d in _divisas)
                if (!DoNucleo(d.a) && !DoNucleo(d.b)) ParedeDeDentro(an, d, Teto(f));
            float b = Base(f);
            foreach (var k in an.comodos)
            {
                if (DoNucleo(k)) continue;
                // o piso de cada cômodo: 10 cm antes das paredes (de cima, na planta, a linha delas aparece) e até a linha onde
                // ele é aberto para o vizinho (sem a emenda no meio do saguão)
                Laje(k.x0 + Recuo(an, k, 3), k.x1 - Recuo(an, k, 1), k.z0 + Recuo(an, k, 0), k.z1 - Recuo(an, k, 2), b + 0.006f, 0.006f, k.piso ?? "Piso", false);
                float teto = !Anexo(k) ? Teto(f) : (k.tipo == "garagem" ? TopoDaGaragem : TopoDaEntrada) - LajeDoAnexo;
                Lampadas(an, k, teto, ComForro(an.AcabamentoDe(k), k.tipo));
            }
            Acabamentos(an);
            PlacasDoAndar(an);
            // o abandono (a parte 5): as marcas e as placas do forro que caíram
            MarcasDoAndar(an);
            PlacasNoChao(f);
            NoPoco(f);
            if (f == Terreo) { BalcaoEmU(R(23f, 12.6f, 33f, 15.2f), "Madeira"); VagasDaGaragem(); }
            if (f == 2) BalcaoEmU(R(22f, 12.5f, 34f, 16f), "ParedeInterna");
            // as caixas de colisão dos móveis (a parte 4: MoveisDoHospital)
            MoveisDoHospital.Caixas(f, col);
        }

        // o recuo do piso num lado do cômodo (0 a frente, z0; 1 o leste, x1; 2 o fundo, z1; 3 o oeste, x0): nenhum se ele é
        // aberto para algum vizinho desse lado, 10 cm se só tem parede
        float Recuo(Andar an, Comodo k, int lado)
        {
            bool vertical = lado == 1 || lado == 3;
            float c = lado == 0 ? k.z0 : lado == 1 ? k.x1 : lado == 2 ? k.z1 : k.x0;
            foreach (var d in _divisas)
            {
                if (d.vertical != vertical || Mathf.Abs(d.c - c) > 0.01f) continue;
                if (!(lado == 0 || lado == 3 ? d.b == k : d.a == k)) continue;
                foreach (var p in PortasEntre(an, d.a, d.b))
                    if (p.aberto) return 0f;
            }
            return 0.1f;
        }

        /// <summary>
        /// As luminárias de um cômodo (apagadas: não há energia), em linhas ao longo do lado comprido: com o forro modular, as
        /// embutidas de 0,6 × 1,2 m no meio de uma placa do forro; sem ele, as calhas penduradas. Na sala vermelha, no
        /// isolamento e no acesso à A-01, a lente vermelha da luz de emergência.
        /// </summary>
        void Lampadas(Andar an, Comodo k, float teto, bool forro)
        {
            bool emX = k.x1 - k.x0 >= k.z1 - k.z0;
            bool vermelha = k.tipo == "vermelha" || k.tipo == "isolamento" || k.tipo == "acesso" || k.tipo == "a01";
            _luminarias.Clear();
            PontosDasLuminarias(k, forro, _luminarias);
            foreach (var l in _luminarias)
            {
                if (forro)
                {
                    Caixa(new Vector3(l.x, teto - 0.02f, l.y), new Vector3(PlacaDoForroX - 0.04f, 0.04f, PlacaDoForroZ - 0.04f), "Metal");
                    Caixa(new Vector3(l.x, teto - 0.041f, l.y), new Vector3(PlacaDoForroX - 0.12f, 0.006f, PlacaDoForroZ - 0.12f), vermelha ? "Lanterna" : "Farol");
                }
                else
                {
                    var c = new Vector3(l.x, teto - 0.035f, l.y);
                    Caixa(c, emX ? new Vector3(1.3f, 0.07f, 0.24f) : new Vector3(0.24f, 0.07f, 1.3f), "Metal");
                    Caixa(c - new Vector3(0f, 0.045f, 0f), emX ? new Vector3(1.2f, 0.025f, 0.14f) : new Vector3(0.14f, 0.025f, 1.2f), vermelha ? "Lanterna" : "Farol");
                }
            }
        }

        readonly List<Vector2> _luminarias = new List<Vector2>();

        // um balcão em U (o tampo a 1,1 m), aberto para o fundo: a frente (do lado de z0) e os dois lados
        void BalcaoEmU(Rect r, string cor)
        {
            const float fundo = 0.7f, h = 1.1f;
            float b = Base(_parte);
            Caixa(new Vector3(r.center.x, b + h / 2f, r.yMin + fundo / 2f), new Vector3(r.width, h, fundo), cor, colide: true);
            foreach (float x in new[] { r.xMin + fundo / 2f, r.xMax - fundo / 2f })
                Caixa(new Vector3(x, b + h / 2f, (r.yMin + fundo + r.yMax) / 2f), new Vector3(fundo, h, r.height - fundo), cor, colide: true);
            Caixa(new Vector3(r.center.x, b + h + 0.02f, r.yMin + fundo / 2f), new Vector3(r.width + 0.06f, 0.04f, fundo + 0.08f), "Cromo");
        }

        // as linhas amarelas entre as 3 vagas da garagem e a faixa da área das macas, junto da porta do recebimento
        void VagasDaGaragem()
        {
            var g = GaragemDasAmbulancias;
            for (int i = 0; i <= Vagas.Length; i++)
            {
                float z = (i < Vagas.Length ? Vagas[i] - 2f : Vagas[Vagas.Length - 1] + 2f) + Z0;
                Caixa(new Vector3(g.xMin + 6.2f, 0.018f, z), new Vector3(8.5f, 0.012f, 0.12f), "Faixa");
            }
            Caixa(new Vector3(X0 - 4.5f, 0.018f, g.center.y), new Vector3(0.12f, 0.012f, g.height - 1f), "Faixa");
        }
    }
}
