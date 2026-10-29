using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O prédio levantado a partir da <see cref="PlantaDePredio"/> (Plano_Mapa, Parte M6, segunda fatia), em duas partes:
    ///  - <b>a casca</b> (<see cref="MontarCasca"/>), que existe sempre que o prédio é de verdade (perto de alguém): a
    ///    fachada de todos os andares (as paredes de fora com as janelas; por dentro, no papel de cada cômodo), as lajes
    ///    (com a colisão só a do térreo, a da cobertura e os patamares da escada), o núcleo inteiro com a colisão (a
    ///    escada, as paredes dela e o poço do elevador), a cobertura (a platibanda, a casinha da escada, a casa de
    ///    máquinas e a caixa d'água), a entrada (as portas, os degraus, a marquise, as lojas) e a escada de incêndio.
    ///    Mais as janelas escuras de cada andar (<see cref="Escuros"/>), que tapam a vista enquanto o andar não carrega;
    ///  - <b>um andar por dentro</b> (<see cref="MontarAndar"/>), que só carrega perto de alguém: a laje com a colisão,
    ///    o piso de cada cômodo, as paredes de dentro com as portas e as luminárias.
    /// A fachada é feita de faces (as duas da parede e os batentes das aberturas), e não de caixas: um prédio de 15 andares
    /// tem centenas de janelas. Coordenadas da planta (o meio no centro, a frente em -z), com o chão do lote (o mais alto
    /// embaixo do prédio) em y = 0. Espelhar fica para quem junta as malhas.
    /// </summary>
    public sealed class Predio : Obra
    {
        public readonly PlantaDePredio p;
        readonly float x0, x1, z0, z1;
        readonly int _parte;

        /// <summary>Na casca: as janelas escuras de cada andar (do estoque; quem usa devolve).</summary>
        public readonly List<MalhaDeObra> Escuros = new List<MalhaDeObra>();
        /// <summary>No andar: os cômodos dele (com o papel e o piso do andar).</summary>
        public readonly List<Comodo> Comodos = new List<Comodo>();
        /// <summary>No andar: os pontos dos móveis dos cômodos dele (sem espelhar).</summary>
        public readonly List<PontoDeMovel> Moveis = new List<PontoDeMovel>();
        readonly List<Ligacao> _ligacoes = new List<Ligacao>();
        readonly List<Vector3> _q = new List<Vector3>(4);

        /// <summary>O núcleo (a escada e o poço): por dentro, as paredes de concreto pintado.</summary>
        static readonly object Nucleo = new object();

        /// <summary>A casca (parte -1) ou o andar 'parte' do prédio.</summary>
        public Predio(PlantaDePredio planta, Medidas medidas, int parte)
            : base(planta.semente * 31 + parte, medidas, parte < 0 ? TamanhoDaMalha.Media : TamanhoDaMalha.Pequena)
        {
            p = planta;
            _parte = parte;
            x0 = p.X0; x1 = p.X1; z0 = p.Z0; z1 = p.Z1;
        }

        // ------------------------------------------------------------ cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.x < x0 || q.x > x1 || q.z < z0 || q.z > z1) return null;
            if (q.z > p.zc1 && ((q.x > p.ex0 && q.x < p.ex1) || (q.x > p.px0 && q.x < p.px1 && q.z < p.FimDoPoco))) return Nucleo;
            if (q.y < PlantaDePredio.Piso - 0.05f || q.y > p.Cobertura) return null;
            int f = p.AndarEm(q.y);
            if (f >= p.andares) return null;
            foreach (var k in p.ComodosDo(f)) if (k.Contem(q.x, q.z)) return k;
            return null;
        }

        protected override string CorDeFora(Vector3 q) => q.y < PlantaDePredio.Piso - 0.05f ? "Concreto" : p.cor;
        protected override string CorDeDentro(object k, Vector3 q) => k == Nucleo ? "ParedeInterna" : p.PapelDe(p.AndarEm(q.y), (Comodo)k);

        void Quad(MalhaDeObra malha, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 fora, Cor cor)
        {
            _q.Clear(); _q.Add(a); _q.Add(b); _q.Add(c); _q.Add(d);
            malha.Face(_q, fora, cor);
        }

        // ------------------------------------------------------------ os lados de fora

        Vector2 LadoA(int lado) => lado switch { 0 => new Vector2(x0, z0), 1 => new Vector2(x1, z0), 2 => new Vector2(x1, z1), _ => new Vector2(x0, z1) };
        Vector2 LadoB(int lado) => lado switch { 0 => new Vector2(x1, z0), 1 => new Vector2(x1, z1), 2 => new Vector2(x0, z1), _ => new Vector2(x0, z0) };
        float S(int lado, float pos) => lado switch { 0 => pos - x0, 1 => pos - z0, 2 => x1 - pos, _ => z1 - pos };

        bool Segmento(Comodo k, int lado, out float s0, out float s1)
        {
            s0 = s1 = 0f;
            switch (lado)
            {
                case 0: if (Mathf.Abs(k.z0 - z0) > 0.01f) return false; s0 = k.x0 - x0; s1 = k.x1 - x0; return true;
                case 1: if (Mathf.Abs(k.x1 - x1) > 0.01f) return false; s0 = k.z0 - z0; s1 = k.z1 - z0; return true;
                case 2: if (Mathf.Abs(k.z1 - z1) > 0.01f) return false; s0 = x1 - k.x1; s1 = x1 - k.x0; return true;
                default: if (Mathf.Abs(k.x0 - x0) > 0.01f) return false; s0 = z1 - k.z1; s1 = z1 - k.z0; return true;
            }
        }

        // as listas de rascunho do Encaixar, uma por thread (as montagens rodam em várias): eram duas listas novas por
        // janela de cada andar, o maior número de alocações da casca
        [System.ThreadStatic] static List<Vector2> _livres, _novos;

        /// <summary>Um lugar para o meio de uma janela perto de s, entre lo e hi, fora dos intervalos ocupados.</summary>
        static float? Encaixar(float s, float larg, float lo, float hi, List<Vector2> ocupados)
        {
            var livres = _livres ??= new List<Vector2>();
            var novos = _novos ??= new List<Vector2>();
            livres.Clear();
            livres.Add(new Vector2(lo, hi));
            foreach (var o in ocupados)
            {
                float a = o.x - larg / 2, b = o.y + larg / 2;
                novos.Clear();
                foreach (var l in livres)
                {
                    if (b <= l.x || a >= l.y) { novos.Add(l); continue; }
                    if (a > l.x) novos.Add(new Vector2(l.x, a));
                    if (b < l.y) novos.Add(new Vector2(b, l.y));
                }
                (livres, novos) = (novos, livres);
            }
            float? melhor = null;
            float dist = float.MaxValue;
            foreach (var l in livres)
            {
                if (l.y < l.x) continue;
                float c = Mathf.Clamp(s, l.x, l.y);
                if (Mathf.Abs(c - s) < dist) { dist = Mathf.Abs(c - s); melhor = c; }
            }
            return melhor;
        }

        enum TipoDeVao : byte { Janela, PortaDaFrente, PortaDeLoja, Vitrine }

        struct Vao
        {
            public Abertura ab;
            public TipoDeVao tipo;
        }

        /// <summary>
        /// As aberturas do lado 'lado' no andar f: no térreo, na frente, as portas (a da portaria e as das lojas) e as
        /// vitrines; nos cômodos, as janelas pelo tamanho e pelo tipo de cada um, desviando das portas e das paredes; nas
        /// pontas do corredor, uma janela. Os cortes são onde muda o cômodo de dentro (para o papel de cada um).
        /// </summary>
        void VaosDo(int f, int lado, List<Vao> vaos, List<float> cortes)
        {
            vaos.Clear();
            cortes.Clear();
            var ocupados = new List<Vector2>();
            float topo = f == 0 ? m.topoJanela + 0.3f : m.topoJanela;
            if (f == 0 && lado == 0)
                foreach (var l in p.ligacoesDoTerreo)
                {
                    if (l.b != null) continue;
                    float s = S(0, l.pos);
                    bool loja = l.a.tipo == "loja";
                    float larg = loja ? 3.0f : 2.4f, alto = loja ? 2.9f : 2.7f;
                    vaos.Add(new Vao { ab = new Abertura(s - larg / 2, larg, 0f, alto), tipo = loja ? TipoDeVao.PortaDeLoja : TipoDeVao.PortaDaFrente });
                    ocupados.Add(new Vector2(s - larg / 2 - 0.5f, s + larg / 2 + 0.5f));
                }
            // o núcleo encostado neste lado: sem janela, e a cor de dentro muda nas bordas dele
            foreach (float xe in new[] { p.ex0, p.ex1, p.px0, p.px1 })
                if (lado == 0 || lado == 2) cortes.Add(S(lado, xe));
            if (lado == 1 || lado == 3) { cortes.Add(S(lado, p.zc1)); cortes.Add(S(lado, p.FimDoPoco)); }
            foreach (var k in p.ComodosDo(f))
            {
                if (!Segmento(k, lado, out float s0, out float s1)) continue;
                cortes.Add(s0);
                cortes.Add(s1);
                if (k.tipo == "loja" && f == 0 && lado == 0)
                {
                    Vitrines(k, s0, s1, vaos, ocupados);
                    continue;
                }
                float larg = m.janela, baixo = m.peitoril, alto = topo;
                int n;
                float U = s1 - s0 - 1.2f;
                switch (k.tipo)
                {
                    case "sala": case "quarto": case "loja": case "portaria":
                    case "enfermaria": case "espera": case "emergencia": case "recepcao":
                        n = U >= 2 * larg + 2.2f ? 2 : U >= larg ? 1 : 0;
                        break;
                    case "cozinha": case "deposito": case "consultorio": case "triagem": case "posto":
                        n = U >= larg ? 1 : 0;
                        break;
                    case "banheiro": case "lavanderia": case "rouparia":
                        larg = m.janelaPequena; baixo = m.peitorilPequena;
                        n = U >= larg ? 1 : 0;
                        break;
                    case "corredor":
                        larg = Mathf.Min(m.janela, s1 - s0 - 1.0f);
                        n = (lado == 1 || lado == 3) && larg > 0.6f ? 1 : 0;
                        U = s1 - s0 - 1.0f;
                        break;
                    default:
                        n = 0;
                        break;
                }
                float margem = k.tipo == "corredor" ? 0.5f : 0.6f;
                for (int j = 0; j < n; j++)
                {
                    float s = s0 + margem + U * (j + 0.5f) / n;
                    var c = Encaixar(s, larg, s0 + margem + larg / 2, s1 - margem - larg / 2, ocupados);
                    if (c == null) continue;
                    vaos.Add(new Vao { ab = new Abertura(c.Value - larg / 2, larg, baixo, alto), tipo = TipoDeVao.Janela });
                    ocupados.Add(new Vector2(c.Value - larg / 2 - 0.5f, c.Value + larg / 2 + 0.5f));
                }
            }
            vaos.Sort((a, b) => a.ab.ini.CompareTo(b.ab.ini));
            cortes.Sort();
        }

        // as vitrines da loja: o que sobra da frente dela, dos lados da porta, em vãos de até 4 m
        void Vitrines(Comodo k, float s0, float s1, List<Vao> vaos, List<Vector2> ocupados)
        {
            var livres = new List<Vector2> { new Vector2(s0 + 0.5f, s1 - 0.5f) };
            foreach (var o in ocupados)
            {
                var novos = new List<Vector2>();
                foreach (var l in livres)
                {
                    if (o.y <= l.x || o.x >= l.y) { novos.Add(l); continue; }
                    if (o.x > l.x) novos.Add(new Vector2(l.x, o.x));
                    if (o.y < l.y) novos.Add(new Vector2(o.y, l.y));
                }
                livres = novos;
            }
            foreach (var l in livres)
            {
                float comp = l.y - l.x;
                if (comp < 1.5f) continue;
                int n = Mathf.CeilToInt(comp / 4.6f);
                float cada = comp / n;
                for (int i = 0; i < n; i++)
                {
                    float a = l.x + cada * i + 0.3f, b = l.x + cada * (i + 1) - 0.3f;
                    vaos.Add(new Vao { ab = new Abertura(a, b - a, 0.45f, 2.9f), tipo = TipoDeVao.Vitrine });
                }
            }
        }

        // ------------------------------------------------------------ a casca

        /// <summary>
        /// A casca. 'chao' dá a altura do chão (nas coordenadas do prédio) num ponto da planta, para os degraus das portas;
        /// 'chaoMaisBaixo' é o ponto mais baixo do chão em volta (até onde a fundação desce).
        /// </summary>
        public void MontarCasca(Func<float, float, float> chao, float chaoMaisBaixo)
        {
            for (int f = 0; f < p.andares; f++) Escuros.Add(MalhaDeObra.Emprestar());
            LinhasDoNucleo();
            Lajes(chaoMaisBaixo);
            var vaos = new List<Vao>();
            var cortes = new List<float>();
            for (int f = 0; f < p.andares; f++)
                for (int lado = 0; lado < 4; lado++)
                {
                    VaosDo(f, lado, vaos, cortes);
                    Fachada(f, lado, vaos, cortes, chao);
                    paredesDeFora.Add(ParedeDaFachada(f, lado, vaos));
                }
            Quinas();
            NucleoDoPredio();
            Escada();
            Elevador();
            Cobertura();
            EscadaDeIncendio();
        }

        Cor CorDaLaje(string topo) => Cor.Por((n, c) => n.y > 0.9f ? topo : (n.y < -0.9f ? "Teto" : p.faixa));

        void Lajes(float chaoMaisBaixo)
        {
            float piso = PlantaDePredio.Piso;
            // a fundação (até o chão mais baixo em volta) e o chão do térreo
            float fundo = Mathf.Min(chaoMaisBaixo, 0f) - 0.4f, topoDaFundacao = piso - m.laje;
            Caixa(new Vector3(0f, (fundo + topoDaFundacao) / 2, 0f), new Vector3(p.W + 0.4f, topoDaFundacao - fundo, p.D + 0.4f), "Concreto", colide: true);
            Laje(x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, piso, m.laje, CorDaLaje("Concreto"), true, new[] { p.VaoDoPoco });
            // as lajes de cima (o desenho; a colisão é do andar, que carrega perto) e o patamar de cada andar na escada
            for (int f = 1; f < p.andares; f++)
            {
                Laje(x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, p.Base(f), m.laje, CorDaLaje("Concreto"), false, new[] { p.VaoDaEscada, p.VaoDoPoco });
                Laje(p.ex0 - 0.1f, p.ex1 + 0.1f, p.zc1 - 0.1f, p.zc1 + 0.1f + PlantaDePredio.Patamar, p.Base(f), m.laje, "Concreto", true, visual: false);
            }
            Laje(x0 - 0.2f, x1 + 0.2f, z0 - 0.2f, z1 + 0.2f, p.Cobertura, m.laje, CorDaLaje("ConcretoClaro"), true, new[] { p.VaoDaEscada, p.VaoDoPoco });
        }

        /// <summary>
        /// A parede de fora do andar f no lado 'lado', com as aberturas: as duas faces em retângulos entre as aberturas (a
        /// de dentro cortada onde muda o cômodo), os batentes, a colisão em caixas e a janela escura de cada abertura.
        /// Mais o que vai nela: o peitoril corrido, as tábuas, o ar-condicionado, as portas e a entrada.
        /// </summary>
        void Fachada(int f, int lado, List<Vao> vaos, List<float> cortes, Func<float, float, float> chao)
        {
            Vector2 a = LadoA(lado), b = LadoB(lado);
            float L = (b - a).magnitude;
            Vector2 u = (b - a) / L, n = new Vector2(u.y, -u.x);
            Vector3 u3 = new Vector3(u.x, 0f, u.y), n3 = new Vector3(n.x, 0f, n.y);
            float e = m.esp / 2, y0 = p.Base(f), h = p.PeDireitoDe(f), yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
            Vector3 P(float s, float y, float fora) { var q = a + u * s + n * fora; return new Vector3(q.x, y0 + y, q.y); }

            void Pedaco(float s0, float s1, float ya, float yb)
            {
                if (s1 - s0 < 0.005f || yb - ya < 0.005f) return;
                float c0 = s0 <= 0.001f ? -e : s0, c1 = s1 >= L - 0.001f ? L + e : s1;
                var cm = a + u * ((c0 + c1) / 2);
                Caixa(new Vector3(cm.x, y0 + (ya + yb) / 2, cm.y), new Vector3(c1 - c0, yb - ya, m.esp), "Concreto", yaw, colide: true, visual: false);
                Quad(vis, P(c0, ya, e), P(c1, ya, e), P(c1, yb, e), P(c0, yb, e), n3, p.cor);
                float i0 = Mathf.Max(s0, e), i1 = Mathf.Min(s1, L - e), ini = i0;
                if (i1 - i0 < 0.005f) return;
                foreach (float corte in cortes)
                {
                    if (corte <= ini + 0.005f || corte >= i1 - 0.005f) continue;
                    Quad(vis, P(ini, ya, -e), P(corte, ya, -e), P(corte, yb, -e), P(ini, yb, -e), -n3, corDeParede);
                    ini = corte;
                }
                Quad(vis, P(ini, ya, -e), P(i1, ya, -e), P(i1, yb, -e), P(ini, yb, -e), -n3, corDeParede);
            }

            float sAte = 0f;
            foreach (var v in vaos)
            {
                var ab = v.ab;
                Pedaco(sAte, ab.ini, 0f, h);
                Pedaco(ab.ini, ab.ini + ab.larg, 0f, ab.baixo);
                Pedaco(ab.ini, ab.ini + ab.larg, ab.alto, h);
                sAte = ab.ini + ab.larg;
                float s0 = ab.ini, s1 = ab.ini + ab.larg;
                string batente = v.tipo == TipoDeVao.PortaDeLoja ? "Metal" : "Batente";
                Quad(vis, P(s0, ab.baixo, -e), P(s0, ab.baixo, e), P(s0, ab.alto, e), P(s0, ab.alto, -e), u3, batente);
                Quad(vis, P(s1, ab.baixo, -e), P(s1, ab.baixo, e), P(s1, ab.alto, e), P(s1, ab.alto, -e), -u3, batente);
                if (ab.baixo > 0.01f) Quad(vis, P(s0, ab.baixo, -e), P(s1, ab.baixo, -e), P(s1, ab.baixo, e), P(s0, ab.baixo, e), Vector3.up, batente);
                Quad(vis, P(s0, ab.alto, -e), P(s1, ab.alto, -e), P(s1, ab.alto, e), P(s0, ab.alto, e), Vector3.down, batente);
                // a janela escura (enquanto o andar não carrega)
                Quad(Escuros[f], P(s0, ab.baixo, -e - 0.01f), P(s1, ab.baixo, -e - 0.01f), P(s1, ab.alto, -e - 0.01f), P(s0, ab.alto, -e - 0.01f), n3, "Vidro");
                float meio = (s0 + s1) / 2;
                switch (v.tipo)
                {
                    case TipoDeVao.Janela:
                        if (Chance(f <= 1 ? 0.25f : 0.07f))
                        {
                            // tábuas pregadas por fora
                            tapadas.Add(new Vector4(a.x, a.y, y0, ab.ini));
                            float alt = ab.alto - ab.baixo;
                            string[] cores = { "MadeiraVelha", "Madeira", "BancoMadeira" };
                            float[] ys = { 0.25f, alt / 2, alt - 0.25f };
                            for (int k = 0; k < 3; k++)
                                Caixa(P(meio, ab.baixo + ys[k], e + 0.03f), new Vector3(ab.larg + 0.3f, 0.16f, 0.03f), cores[k], yaw, RZ(Faixa(-9f, 9f)), colide: true);
                        }
                        else if (f >= 1 && Chance(0.06f))
                            Caixa(P(meio, ab.baixo - 0.4f, e + 0.28f), new Vector3(0.8f, 0.5f, 0.5f), Um("ConcretoClaro", "Metal"), yaw, colide: false);
                        break;
                    case TipoDeVao.PortaDaFrente:
                        Entrada(lado, ab, P, yaw, e, u, n, chao);
                        break;
                    case TipoDeVao.PortaDeLoja:
                        // a porta de enrolar recolhida em cima, e os degraus até a calçada
                        Caixa(P(meio, ab.alto + 0.25f, e + 0.25f), new Vector3(ab.larg + 0.2f, 0.45f, 0.45f), "Metal", yaw);
                        DegrausAteOChao(P(meio, 0f, e), n, ab.larg, chao);
                        break;
                    case TipoDeVao.Vitrine:
                        if (Chance(0.5f)) Caixa(P(meio, ab.alto + 0.45f, e + 0.6f), new Vector3(ab.larg + 0.3f, 0.08f, 1.2f), Um("PersianaVerde", "PersianaVinho", "PlacaAmarela"), yaw, RX(-12f));
                        break;
                }
            }
            Pedaco(sAte, L, 0f, h);
            // o peitoril corrido dos andares de cima
            if (f >= 1)
                Caixa(P(L / 2, m.peitoril - 0.04f, e + 0.04f), new Vector3(L + 2 * e + 0.1f, 0.08f, 0.08f), p.faixa, yaw);
        }

        // a fachada do andar f no lado 'lado' como uma parede, sem montar nada: para as janelas dos cômodos (no andar) e
        // para o abandono (na casca)
        Parede ParedeDaFachada(int f, int lado, List<Vao> vaos)
        {
            Vector2 a = LadoA(lado), fim = LadoB(lado);
            float L = (fim - a).magnitude;
            Vector2 u = (fim - a) / L;
            var w = new Parede
            {
                a = a, u = u, n = new Vector2(u.y, -u.x), yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg, L = L,
                baseY = p.Base(f), altura = p.PeDireitoDe(f), ext = true, nome = "fachada",
            };
            foreach (var v in vaos) w.aberturas.Add(v.ab);
            return w;
        }

        // a porta da frente: as duas folhas abertas para dentro, a marquise, a placa e os degraus até a calçada
        void Entrada(int lado, Abertura ab, Func<float, float, float, Vector3> P, float yaw, float e, Vector2 u, Vector2 n, Func<float, float, float> chao)
        {
            float meio = ab.ini + ab.larg / 2, folha = ab.larg / 2 - 0.03f;
            foreach (int sentido in new[] { 1, -1 })
            {
                float sDob = sentido > 0 ? ab.ini + 0.03f : ab.ini + ab.larg - 0.03f;
                var q = P(sDob, 0f, -e);
                var dob = new Vector2(q.x, q.z);
                float fi = 95f * Mathf.Deg2Rad;
                var d = u * sentido * Mathf.Cos(fi) - n * Mathf.Sin(fi);
                var c = dob + d * (folha / 2);
                Caixa(new Vector3(c.x, PlantaDePredio.Piso + 0.01f + (ab.alto - 0.06f) / 2, c.y), new Vector3(folha, ab.alto - 0.06f, 0.05f),
                      "Madeira", -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, colide: true);
            }
            Caixa(P(meio, ab.alto + 0.4f, e + 0.9f), new Vector3(ab.larg + 2.2f, 0.15f, 1.8f), "Concreto", yaw);
            Caixa(P(meio, ab.alto + 0.85f, e + 0.03f), new Vector3(2.6f, 0.45f, 0.05f), p.hospital ? "PlacaBranca" : "Metal", yaw);
            for (int k = 0; k < 6; k++)
                Caixa(P(meio - 1.0f + k * 0.4f, ab.alto + 0.85f, e + 0.06f), new Vector3(0.22f, 0.26f, 0.02f), p.hospital ? "PlacaVermelha" : "PlacaBranca", yaw);
            if (p.hospital)
            {
                // a cruz vermelha ao lado da placa
                Caixa(P(meio + 1.75f, ab.alto + 0.85f, e + 0.06f), new Vector3(0.7f, 0.22f, 0.08f), "PlacaVermelha", yaw);
                Caixa(P(meio + 1.75f, ab.alto + 0.85f, e + 0.06f), new Vector3(0.22f, 0.7f, 0.08f), "PlacaVermelha", yaw);
            }
            DegrausAteOChao(P(meio, 0f, e), n, ab.larg + 0.6f, chao);
        }

        /// <summary>
        /// Os degraus de uma porta do térreo (borda: o meio da soleira, por fora) até o chão em frente dela: o prédio fica
        /// no chão mais alto embaixo dele, e na frente o chão pode estar até ~1,5 m mais baixo.
        /// </summary>
        void DegrausAteOChao(Vector3 borda, Vector2 fora, float largura, Func<float, float, float> chao)
        {
            var b = new Vector2(borda.x, borda.z);
            var pe = b + fora * 1.2f;
            float ate = chao(pe.x, pe.y), de = PlantaDePredio.Piso;
            float altura = de - ate;
            if (altura < 0.2f) return;
            int n = Mathf.CeilToInt(altura / 0.19f - 0.05f);
            float r = altura / n, passo = 0.3f, yaw = -Mathf.Atan2(fora.y, fora.x) * Mathf.Rad2Deg + 90f;
            for (int k = 1; k < n; k++)
            {
                float topo = de - k * r, baixo = ate - 0.3f;
                var c = b + fora * ((k - 1) * passo + passo / 2 + 0.02f);
                Caixa(new Vector3(c.x, (topo + baixo) / 2, c.y), new Vector3(largura, topo - baixo, passo), "Concreto", yaw, colide: true);
            }
        }

        // as quinas, de cima a baixo
        void Quinas()
        {
            float de = PlantaDePredio.Piso - 0.15f, ate = p.Cobertura + PlantaDePredio.Platibanda;
            foreach (float cx in new[] { x0, x1 })
                foreach (float cz in new[] { z0, z1 })
                    Caixa(new Vector3(cx, (de + ate) / 2, cz), new Vector3(0.36f, ate - de, 0.36f), p.quina);
        }

        // ------------------------------------------------------------ o núcleo

        bool PocoADireita => p.px0 >= p.ex1 - 0.01f;

        // as paredes do núcleo: a frente da escada (1, com a porta) e a do poço (2, com a do elevador), os lados da escada
        // e o fundo e o lado de fora do poço (0); cada uma registrada em todos os andares (os vãos ficam na cor do batente)
        readonly List<(Vector2 a, Vector2 b, int tipo)> _linhasDoNucleo = new List<(Vector2, Vector2, int)>();

        void LinhasDoNucleo()
        {
            var L = _linhasDoNucleo;
            L.Clear();
            L.Add((new Vector2(p.ex0, p.zc1), new Vector2(p.ex1, p.zc1), 1));
            L.Add((new Vector2(p.px0, p.zc1), new Vector2(p.px1, p.zc1), 2));
            if (p.ex0 > x0 + 0.01f) L.Add((new Vector2(p.ex0, p.zc1), new Vector2(p.ex0, z1), 0));
            if (p.ex1 < x1 - 0.01f) L.Add((new Vector2(p.ex1, p.zc1), new Vector2(p.ex1, z1), 0));
            L.Add((new Vector2(p.px0, p.FimDoPoco), new Vector2(p.px1, p.FimDoPoco), 0));
            float xFora = PocoADireita ? p.px1 : p.px0;
            if (xFora > x0 + 0.01f && xFora < x1 - 0.01f) L.Add((new Vector2(xFora, p.zc1), new Vector2(xFora, p.FimDoPoco), 0));
            for (int f = 0; f < p.andares; f++)
                foreach (var (a, b, _) in L) RegistrarLinha(a, b, p.Base(f), p.Base(f + 1));
            // a casinha da escada e a casa de máquinas, na cobertura
            foreach (var (a, b) in LinhasDaCobertura()) RegistrarLinha(a, b, p.Cobertura, p.Cobertura + PlantaDePredio.CasinhaDaEscada);
        }

        // na cobertura: as quatro paredes da casinha da escada e as três da casa de máquinas (a quarta é a da casinha)
        List<(Vector2 a, Vector2 b)> LinhasDaCobertura()
        {
            var r = new List<(Vector2, Vector2)>
            {
                (new Vector2(p.ex0, p.zc1), new Vector2(p.ex1, p.zc1)),
                (new Vector2(p.ex1, p.zc1), new Vector2(p.ex1, z1)),
                (new Vector2(p.ex1, z1), new Vector2(p.ex0, z1)),
                (new Vector2(p.ex0, z1), new Vector2(p.ex0, p.zc1)),
                (new Vector2(p.px0, p.zc1), new Vector2(p.px1, p.zc1)),
                (new Vector2(p.px0, p.FimDoPoco), new Vector2(p.px1, p.FimDoPoco)),
            };
            float xFora = PocoADireita ? p.px1 : p.px0;
            r.Add((new Vector2(xFora, p.zc1), new Vector2(xFora, p.FimDoPoco)));
            return r;
        }

        void NucleoDoPredio()
        {
            for (int f = 0; f < p.andares; f++)
            {
                // do piso até o piso de cima (as lajes têm o vão da escada: a parede não pode parar embaixo delas)
                float b = p.Base(f), alto = p.Base(f + 1) - b;
                foreach (var (a, c, tipo) in _linhasDoNucleo)
                {
                    float L = (c - a).magnitude;
                    var aberturas = new List<Abertura>();
                    if (tipo == 1) aberturas.Add(new Abertura(L / 2 - m.porta / 2, m.porta, 0f, m.alturaPorta));
                    if (tipo == 2) aberturas.Add(new Abertura(L / 2 - 0.55f, 1.1f, 0f, 2.2f));
                    LevantarParede(a, c, aberturas, false, "nucleo", b, alto);
                }
            }
            // na cobertura: a casinha da escada (com a porta para a cobertura) e a casa de máquinas, com as lajes de cima
            float cob = p.Cobertura;
            var linhas = LinhasDaCobertura();
            for (int i = 0; i < linhas.Count; i++)
            {
                var (a, c) = linhas[i];
                float L = (c - a).magnitude;
                bool casinha = i < 4;
                var aberturas = new List<Abertura>();
                if (i == 0) aberturas.Add(new Abertura(L / 2 - m.porta / 2, m.porta, 0f, m.alturaPorta));
                LevantarParede(a, c, aberturas, true, "cobertura", cob, casinha ? PlantaDePredio.CasinhaDaEscada : PlantaDePredio.CasinhaDaEscada - 0.2f);
            }
            float tc = cob + PlantaDePredio.CasinhaDaEscada;
            Laje(p.ex0 - 0.2f, p.ex1 + 0.2f, p.zc1 - 0.2f, z1 + 0.2f, tc + 0.2f, 0.2f, CorDaLaje("ConcretoClaro"), true);
            Laje(p.px0 - 0.2f, p.px1 + 0.2f, p.zc1 - 0.2f, p.FimDoPoco + 0.2f, tc, 0.2f, CorDaLaje("ConcretoClaro"), true);
            // a caixa d'água em cima da casinha
            Caixa(new Vector3((p.ex0 + p.ex1) / 2, tc + 0.2f + 1.05f, (p.zc1 + z1) / 2), new Vector3(LarguraUtil(p.ex1 - p.ex0, 3.2f), 2.1f, Mathf.Min(z1 - p.zc1 - 0.6f, 3.2f)),
                  Cor.PorNormal("ConcretoClaro", "Concreto"));
        }

        static float LarguraUtil(float vao, float max) => Mathf.Min(vao - 0.4f, max);

        /// <summary>
        /// A escada em U: em cada andar, o lance de ida (do patamar do andar, junto do corredor, para o fundo), o patamar
        /// do meio, no fundo, e o lance de volta até o patamar do andar de cima; com o corrimão no vão do meio. Na
        /// cobertura, o guarda-corpo na beira do vão (do lado em que não há lance subindo).
        /// </summary>
        void Escada()
        {
            float xi = p.ex0 + 0.1f, xf = p.ex1 - 0.1f, lance = (xf - xi - 0.2f) / 2;
            float xA = xi + lance / 2, xB = xf - lance / 2;
            float zL = p.zc1 + 0.1f + PlantaDePredio.Patamar;
            for (int f = 0; f < p.andares; f++)
            {
                float b = p.Base(f), t = p.Base(f + 1), meio = (b + t) / 2;
                float r = (meio - b) / Espelhos(meio - b);
                float compA = Lance(new Vector2(xA, zL), new Vector2(0f, 1f), lance, b, meio, m.pisada, "Concreto", "ConcretoClaro");
                float zM = zL + compA;
                Laje(xi, xf, zM, z1 - 0.1f, meio, 0.2f, CorDaLaje("Concreto"), true);
                Lance(new Vector2(xB, zM), new Vector2(0f, -1f), lance, meio, t, m.pisada, "Concreto", "ConcretoClaro");
                float xa = xA + lance / 2 - 0.03f, xb = xB - lance / 2 + 0.03f;
                GuardaCorpo(new Vector3(xa, b + r, zL + 0.15f), new Vector3(xa, meio - r, zM - 0.15f), 0.9f, "Metal", false);
                GuardaCorpo(new Vector3(xb, meio + r, zM - 0.15f), new Vector3(xb, t - r, zL + 0.15f), 0.9f, "Metal", false);
            }
            float cob = p.Cobertura;
            GuardaCorpo(new Vector3(xi, cob, zL + 0.03f), new Vector3(xi + lance + 0.1f, cob, zL + 0.03f), 1.0f, "Metal");
        }

        /// <summary>
        /// O elevador, parado: as portas de cada andar fechadas (uma chapa), menos no andar do poço aberto e no térreo; o
        /// fundo do poço é a fundação, 30 cm abaixo do térreo (a laje dele tem o vão); com o poço aberto, a cabine fica
        /// parada no último andar.
        /// </summary>
        void Elevador()
        {
            float xm = (p.px0 + p.px1) / 2;
            for (int f = 0; f < p.andares; f++)
            {
                if (p.PocoAbertoNo(f)) continue;
                float b = p.Base(f);
                Caixa(new Vector3(xm, b + 1.1f, p.zc1), new Vector3(1.1f, 2.2f, 0.1f), "Metal", colide: true);
                Caixa(new Vector3(xm, b + 1.1f, p.zc1 - 0.055f), new Vector3(0.02f, 2.2f, 0.01f), "Grade");
            }
            var v = p.VaoDoPoco;
            if (p.andarDoPocoAberto >= 0)
            {
                float bc = p.Base(p.andares - 1);
                Caixa(new Vector3(xm, bc + 1.2f, (v.yMin + v.yMax) / 2), new Vector3(v.width - 0.1f, 2.4f, v.height - 0.1f), Cor.PorNormal("Metal", "Cromo"), colide: true);
                Caixa(new Vector3(xm, (bc + 2.4f + p.Cobertura + 2.6f) / 2, (v.yMin + v.yMax) / 2), new Vector3(0.05f, p.Cobertura + 2.6f - bc - 2.4f, 0.05f), "Grade");
            }
        }

        /// <summary>A platibanda em volta da cobertura, com o rufo, e a antena.</summary>
        void Cobertura()
        {
            float cob = p.Cobertura, h = PlantaDePredio.Platibanda;
            for (int lado = 0; lado < 4; lado++)
            {
                Vector2 a = LadoA(lado), b = LadoB(lado);
                float L = (b - a).magnitude, yaw = -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                var c = (a + b) / 2;
                Caixa(new Vector3(c.x, cob + h / 2, c.y), new Vector3(L + m.esp, h, m.esp), p.cor, yaw, colide: true);
                Caixa(new Vector3(c.x, cob + h + 0.03f, c.y), new Vector3(L + m.esp + 0.1f, 0.06f, m.esp + 0.12f), "Metal", yaw);
            }
            float ax = Mathf.Lerp(x0, x1, Chance(0.5f) ? 0.15f : 0.85f), az = Mathf.Lerp(z0, z1, 0.7f);
            Tubo(new Vector3(ax, cob, az), new Vector3(ax, cob + 4.5f, az), 0.04f, "Metal");
            Tubo(new Vector3(ax - 0.6f, cob + 3.8f, az), new Vector3(ax + 0.6f, cob + 3.8f, az), 0.02f, "Metal");
        }

        /// <summary>
        /// A escada de incêndio de ferro, por fora, num dos lados (na ponta do corredor), nos andares de baixo (do 1 ao 4):
        /// as plataformas nas janelas do corredor, os lances entre elas e a escada de mão recolhida. Só o desenho.
        /// </summary>
        void EscadaDeIncendio()
        {
            int ate = Mathf.Min(4, p.andares - 1);
            if (ate < 1) return;
            int lado = Chance(0.5f) ? 1 : 3;
            float xParede = lado == 1 ? x1 : x0, fora = lado == 1 ? 1f : -1f;
            float zm = (p.zc0 + p.zc1) / 2, larg = 2.6f, prof = 1.2f;
            float xp = xParede + fora * (m.esp / 2 + prof / 2);
            for (int f = 1; f <= ate; f++)
            {
                float y = p.Base(f) - 0.05f;
                Caixa(new Vector3(xp, y, zm), new Vector3(prof, 0.06f, larg), "Grade");
                Caixa(new Vector3(xp + fora * prof / 2, y + 0.5f, zm), new Vector3(0.04f, 1.0f, larg), "Metal");
                Caixa(new Vector3(xp, y + 1.0f, zm - larg / 2), new Vector3(prof, 0.04f, 0.04f), "Metal");
                Caixa(new Vector3(xp, y + 1.0f, zm + larg / 2), new Vector3(prof, 0.04f, 0.04f), "Metal");
                // o lance até a plataforma de baixo (o de baixo de todas é a escada de mão recolhida)
                float yb = f == 1 ? y - 1.4f : p.Base(f - 1) - 0.05f;
                var deCima = new Vector3(xp, y, zm + (f % 2 == 0 ? -1f : 1f) * (larg / 2 - 0.3f));
                var deBaixo = new Vector3(xp, yb, zm + (f % 2 == 0 ? 1f : -1f) * (larg / 2 - 0.3f));
                if (f == 1) deBaixo = new Vector3(xp + fora * 0.3f, yb, deCima.z);
                CaixaEntre(deBaixo, deCima, 0.6f, 0.05f, "Metal");
            }
        }

        // ------------------------------------------------------------ um andar por dentro

        sealed class Interna
        {
            public Vector2 a, b;
            public bool vertical;
            public float inicio;
            public Comodo menor, maior;
            public Comodo LadoN => vertical ? maior : menor;
        }

        struct PortaFeita { public Parede w; public Abertura ab; public Ligacao l; public Interna i; }

        readonly List<Interna> _internas = new List<Interna>();
        readonly List<PortaFeita> _portas = new List<PortaFeita>();

        /// <summary>O andar (a parte deste prédio): a laje, os pisos, as paredes de dentro com as portas e as luminárias.</summary>
        public void MontarAndar()
        {
            int f = _parte;
            float b = p.Base(f), h = p.PeDireitoDe(f);
            p.Andar(f, Comodos, _ligacoes);
            for (int i = 0; i < Comodos.Count; i++)
                for (int j = i + 1; j < Comodos.Count; j++)
                {
                    Comodo A = Comodos[i], B = Comodos[j];
                    Tentar(A.x1, B.x0, A.z0, A.z1, B.z0, B.z1, true, A, B);
                    Tentar(B.x1, A.x0, B.z0, B.z1, A.z0, A.z1, true, B, A);
                    Tentar(A.z1, B.z0, A.x0, A.x1, B.x0, B.x1, false, A, B);
                    Tentar(B.z1, A.z0, B.x0, B.x1, A.x0, A.x1, false, B, A);
                }
            foreach (var i in _internas) RegistrarLinha(i.a, i.b, b, b + h);
            if (f >= 1) Laje(x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, b, m.laje, "Concreto", true, new[] { p.VaoDaEscada, p.VaoDoPoco }, visual: false);
            foreach (var k in Comodos) Laje(k.x0 + 0.1f, k.x1 - 0.1f, k.z0 + 0.1f, k.z1 - 0.1f, b + 0.005f, 0.005f, k.piso ?? "Piso", false);
            foreach (var i in _internas) ParedeInterna(i, b, h);
            Portas();
            foreach (var k in Comodos)
            {
                if (k.tipo != "corredor") { Luminaria(k.Cx, b + h, k.Cz); continue; }
                int n = Mathf.Max(1, Mathf.RoundToInt((k.x1 - k.x0) / 6f));
                for (int j = 0; j < n; j++) Luminaria(k.x0 + (k.x1 - k.x0) * (j + 0.5f) / n, b + h, k.Cz);
            }
            // os pontos dos móveis: as janelas dos cômodos são da fachada (da casca), refeitas aqui sem montar nada
            var vaos = new List<Vao>();
            var cortes = new List<float>();
            for (int lado = 0; lado < 4; lado++)
            {
                VaosDo(f, lado, vaos, cortes);
                paredesDeFora.Add(ParedeDaFachada(f, lado, vaos));
            }
            foreach (var k in Comodos)
                MoveisDaObra.Distribuir(this, k, b, p.hospital ? "hospital" : k.apto >= 0 ? "casa" : "predio", p.semente * 31 + f, Moveis);
        }

        void Tentar(float c1, float c2, float a0, float a1, float b0, float b1, bool vertical, Comodo menor, Comodo maior)
        {
            if (Mathf.Abs(c1 - c2) > 0.01f) return;
            float lo = Mathf.Max(a0, b0), hi = Mathf.Min(a1, b1);
            if (hi - lo < 0.05f) return;
            float min = vertical ? z0 : x0, max = vertical ? z1 : x1;
            if (Mathf.Abs(lo - min) < 0.01f) lo += m.esp / 2;
            if (Mathf.Abs(hi - max) < 0.01f) hi -= m.esp / 2;
            _internas.Add(new Interna
            {
                vertical = vertical, inicio = lo, menor = menor, maior = maior,
                a = vertical ? new Vector2(c1, lo) : new Vector2(lo, c1),
                b = vertical ? new Vector2(c1, hi) : new Vector2(hi, c1),
            });
        }

        void ParedeInterna(Interna i, float baseY, float altura)
        {
            var aberturas = new List<Abertura>();
            var ligs = new List<Ligacao>();
            float L = (i.b - i.a).magnitude;
            foreach (var l in _ligacoes)
            {
                if (l.b == null || !((l.a == i.menor && l.b == i.maior) || (l.a == i.maior && l.b == i.menor))) continue;
                float larg = l.passagem ? m.passagem : m.porta;
                if (L < larg + 0.3f) larg = Mathf.Max(0.9f, L - 0.3f);
                float s = Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f);
                aberturas.Add(new Abertura(s - larg / 2, larg, 0f, m.alturaPorta));
                ligs.Add(l);
            }
            var w = LevantarParede(i.a, i.b, aberturas, false, "interna", baseY, altura);
            // (a parede ordena as aberturas: cada uma volta para a ligação dela pelo meio)
            foreach (var ab in w.aberturas)
            {
                Ligacao achada = null;
                foreach (var l in ligs)
                {
                    float larg = l.passagem ? m.passagem : m.porta;
                    if (L < larg + 0.3f) larg = Mathf.Max(0.9f, L - 0.3f);
                    if (Mathf.Abs(Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f) - (ab.ini + ab.larg / 2)) < 0.05f) achada = l;
                }
                _portas.Add(new PortaFeita { w = w, ab = ab, l = achada, i = i });
            }
        }

        static bool Privado(Comodo k) => k != null && (k.tipo == "quarto" || k.tipo == "banheiro" || k.tipo == "lavanderia" || k.tipo == "deposito" ||
                                                       k.tipo == "consultorio" || k.tipo == "triagem" || k.tipo == "raiox" || k.tipo == "rouparia" || k.tipo == "enfermaria");

        // as folhas das portas: para dentro do quarto, do banheiro, da lavanderia e do depósito; a de entrada do
        // apartamento, para dentro dele (nunca para o corredor); entre dois cômodos comuns (a sala e a cozinha), para o
        // maior (para dentro da cozinha pequena, a folha tirava o lugar do fogão e da geladeira: a mobília desvia dela);
        // quase em ângulo reto
        void Portas()
        {
            var comMoldura = new HashSet<Parede>();
            foreach (var pf in _portas)
            {
                if (comMoldura.Add(pf.w)) Molduras(pf.w);
                if (pf.l == null || pf.l.passagem) continue;
                Comodo a = pf.l.a, b = pf.l.b;
                Comodo alvo = Privado(b) ? b : Privado(a) ? a : a.tipo == "corredor" ? b : b.tipo == "corredor" ? a
                            : (a.x1 - a.x0) * (a.z1 - a.z0) >= (b.x1 - b.x0) * (b.z1 - b.z0) ? a : b;
                FolhaDePorta(pf.w, pf.ab.ini, pf.ab.larg, pf.ab.alto, alvo == pf.i.LadoN ? 1 : -1,
                             a.tipo == "corredor" || b.tipo == "corredor" ? Um("Madeira", "Batente", "MadeiraVelha") : Um("Batente", "Madeira", "Batente"),
                             Faixa(85f, 100f));
            }
        }
    }
}
