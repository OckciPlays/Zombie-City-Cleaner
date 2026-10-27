using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A loja levantada a partir da <see cref="PlantaDeLoja"/> (Plano_Mapa, Parte M6, terceira fatia), inteira de uma vez,
    /// como a casa (são de 1 a 4 andares): a fundação, as lajes (com o vão da escada), as paredes de fora com as portas, as
    /// vitrines (sem vidro, como as janelas das casas) e as janelas, as paredes de dentro com as portas (a de aço do
    /// depósito da loja de armas), a escada em U, os balcões, a fachada (a placa, o toldo e as portas de enrolar
    /// recolhidas), a cobertura com a platibanda e a caixa d'água, os pisos e as luminárias. Coordenadas da planta (o meio
    /// no centro, a frente em -z), com o chão do lote (o mais alto embaixo dela) em y = 0. Espelhar fica para quem junta
    /// as malhas.
    /// </summary>
    public sealed class Loja : Obra
    {
        public readonly PlantaDeLoja p;
        readonly float x0, x1, z0, z1;

        /// <summary>Os pontos dos móveis de todos os cômodos (sem espelhar), feitos no fim do <see cref="Montar"/>.</summary>
        public readonly List<PontoDeMovel> moveis = new List<PontoDeMovel>();

        public Loja(PlantaDeLoja planta, Medidas medidas) : base(planta.semente, medidas, TamanhoDaMalha.Media)
        {
            p = planta;
            x0 = p.X0; x1 = p.X1; z0 = p.Z0; z1 = p.Z1;
        }

        // ------------------------------------------------------------ cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.x < x0 || q.x > x1 || q.z < z0 || q.z > z1) return null;
            if (q.y < PlantaDeLoja.Piso - 0.05f || q.y > p.Cobertura) return null;
            int f = p.AndarEm(q.y);
            if (f >= p.andares) return null;
            foreach (var k in p.comodos) if (k.andar == f && k.Contem(q.x, q.z)) return k;
            return null;
        }

        protected override string CorDeFora(Vector3 q) => q.y < PlantaDeLoja.Piso - 0.05f ? "Concreto" : p.cor;
        protected override string CorDeDentro(object k, Vector3 q) => ((Comodo)k).papel ?? "ParedeInterna";

        Cor CorDaLaje(string topo) => Cor.Por((n, c) => n.y > 0.9f ? topo : (n.y < -0.9f ? "Teto" : p.faixa));

        // ------------------------------------------------------------ a loja

        /// <summary>
        /// A loja inteira. 'chao' dá a altura do chão (nas coordenadas dela) num ponto da planta, para os degraus das portas;
        /// 'chaoMaisBaixo' é o ponto mais baixo do chão em volta (até onde a fundação desce).
        /// </summary>
        public void Montar(Func<float, float, float> chao, float chaoMaisBaixo)
        {
            // as linhas das paredes, antes de levantar (perto delas, a face é o vão: o batente)
            for (int f = 0; f < p.andares; f++)
            {
                for (int lado = 0; lado < 4; lado++) RegistrarLinha(LadoA(lado), LadoB(lado), p.Base(f), p.Base(f) + p.PeDireitoDe(f));
                Internas(f);
            }
            foreach (var i in _internas) RegistrarLinha(i.a, i.b, p.Base(i.andar), p.Base(i.andar) + p.PeDireitoDe(i.andar));
            Lajes(chaoMaisBaixo);
            for (int f = 0; f < p.andares; f++)
                for (int lado = 0; lado < 4; lado++) ParedeDeFora(f, lado, chao);
            foreach (var i in _internas) ParedeInterna(i);
            Portas();
            Escada();
            Balcoes();
            Fachada();
            Cobertura();
            PorDentro();
            // no térreo, a receita do tipo da loja; em cima, a do apartamento
            foreach (var k in p.comodos) MoveisDaObra.Distribuir(this, k, p.Base(k.andar), k.andar == 0 ? p.tipo.ToString() : "casa", p.semente, moveis);
        }

        void Lajes(float chaoMaisBaixo)
        {
            float piso = PlantaDeLoja.Piso;
            // a fundação (até o chão mais baixo em volta) e o chão do térreo; as lojas são coladas (0,3 m entre elas)
            float fundo = Mathf.Min(chaoMaisBaixo, 0f) - 0.4f, topoDaFundacao = piso - m.laje;
            Caixa(new Vector3(0f, (fundo + topoDaFundacao) / 2, 0f), new Vector3(p.W + 0.2f, topoDaFundacao - fundo, p.D + 0.2f), "Concreto", colide: true);
            Laje(x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, piso, m.laje, CorDaLaje("Concreto"), true);
            var vao = p.TemEscada ? new[] { p.VaoDaEscada } : null;
            for (int f = 1; f < p.andares; f++)
                Laje(x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, p.Base(f), m.laje, CorDaLaje("Concreto"), true, vao);
            Laje(x0 - 0.1f, x1 + 0.1f, z0 - 0.1f, z1 + 0.1f, p.Cobertura, m.laje, CorDaLaje("ConcretoClaro"), true);
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

        /// <summary>Um lugar para o meio de uma janela perto de s, entre lo e hi, fora dos intervalos ocupados.</summary>
        static float? Encaixar(float s, float larg, float lo, float hi, List<Vector2> ocupados)
        {
            var livres = new List<Vector2> { new Vector2(lo, hi) };
            foreach (var o in ocupados)
            {
                float a = o.x - larg / 2, b = o.y + larg / 2;
                var novos = new List<Vector2>();
                foreach (var l in livres)
                {
                    if (b <= l.x || a >= l.y) { novos.Add(l); continue; }
                    if (a > l.x) novos.Add(new Vector2(l.x, a));
                    if (b < l.y) novos.Add(new Vector2(b, l.y));
                }
                livres = novos;
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

        enum TipoDeVao : byte { Janela, Alta, PortaDaFrente, PortaDosFundos, Doca, Vitrine }

        struct VaoFeito
        {
            public Parede w;
            public Abertura ab;
            public TipoDeVao tipo;
            public int lado;
        }

        readonly List<VaoFeito> _vaos = new List<VaoFeito>();

        /// <summary>
        /// A parede de fora do andar f no lado 'lado', com as portas, as vitrines (a frente do salão) e as janelas de cada
        /// cômodo. A frente e o fundo passam meia parede dos cantos (os cantos fecham). Os lados não têm janela: as lojas
        /// ficam coladas umas nas outras.
        /// </summary>
        void ParedeDeFora(int f, int lado, Func<float, float, float> chao)
        {
            float e = m.esp / 2, ext = lado == 0 || lado == 2 ? e : 0f, h = p.PeDireitoDe(f);
            Vector2 a = LadoA(lado), b = LadoB(lado), u = (b - a).normalized;
            a -= u * ext;
            b += u * ext;
            var vaos = new List<(Abertura ab, TipoDeVao tipo)>();
            var ocupados = new List<Vector2>();
            foreach (var l in p.ligacoes)
            {
                if (l.b != null || l.a.andar != f || l.lado != lado) continue;
                float larg = l.largura > 0f ? l.largura : m.porta;
                var tipo = lado == 0 ? TipoDeVao.PortaDaFrente : larg >= 3f ? TipoDeVao.Doca : TipoDeVao.PortaDosFundos;
                float alto = Mathf.Min(h - 0.5f, tipo == TipoDeVao.Doca ? 3.4f : tipo == TipoDeVao.PortaDaFrente ? 2.7f : m.alturaPorta);
                float s = S(lado, l.pos) + ext;
                vaos.Add((new Abertura(s - larg / 2, larg, 0f, alto), tipo));
                ocupados.Add(new Vector2(s - larg / 2 - 0.45f, s + larg / 2 + 0.45f));
            }
            if (lado == 0 || lado == 2)
                foreach (var k in p.comodos)
                {
                    if (k.andar != f || !Segmento(k, lado, out float s0, out float s1)) continue;
                    s0 += ext;
                    s1 += ext;
                    if (f == 0 && lado == 0 && k == p.Salao)
                    {
                        Vitrines(s0, s1, h, vaos, ocupados);
                        continue;
                    }
                    float larg = m.janela, baixo = m.peitoril, alto = m.topoJanela;
                    var tipo = TipoDeVao.Janela;
                    int n;
                    float U = s1 - s0 - 1.2f;
                    switch (k.tipo)
                    {
                        case "sala": case "quarto":
                            n = U >= 2 * larg + 2.2f ? 2 : U >= larg ? 1 : 0;
                            break;
                        case "cozinha":
                            n = U >= larg ? 1 : 0;
                            break;
                        case "banheiro":
                            larg = m.janelaPequena; baixo = m.peitorilPequena;
                            n = U >= larg ? 1 : 0;
                            break;
                        case "deposito": case "estoque": case "escritorio":
                            // a janela alta, perto do teto (o basculante dos fundos)
                            larg = m.janela; baixo = h - 1.4f; alto = h - 0.5f; tipo = TipoDeVao.Alta;
                            n = U >= larg ? 1 : 0;
                            break;
                        default:
                            n = 0;
                            break;
                    }
                    for (int j = 0; j < n; j++)
                    {
                        float s = s0 + 0.6f + U * (j + 0.5f) / n;
                        var c = Encaixar(s, larg, s0 + 0.6f + larg / 2, s1 - 0.6f - larg / 2, ocupados);
                        if (c == null) continue;
                        vaos.Add((new Abertura(c.Value - larg / 2, larg, baixo, alto), tipo));
                        ocupados.Add(new Vector2(c.Value - larg / 2 - 0.5f, c.Value + larg / 2 + 0.5f));
                    }
                }
            // a parede é cortada onde muda o cômodo de dentro (a cor de cada pedaço por dentro é a do cômodo dele)
            var cortes = new List<float>();
            foreach (var k in p.comodos)
                if (k.andar == f && Segmento(k, lado, out float c0, out float c1)) { cortes.Add(c0 + ext); cortes.Add(c1 + ext); }
            var aberturas = new List<Abertura>();
            foreach (var (ab, _) in vaos) aberturas.Add(ab);
            string nome = lado switch { 0 => "frente", 1 => "direita", 2 => "fundo", _ => "esquerda" };
            var w = LevantarParede(a, b, aberturas, true, nome, p.Base(f), h, cortes: cortes);
            // cada abertura volta para o tipo dela pelo começo (a parede ordena as aberturas)
            foreach (var ab in w.aberturas)
                foreach (var (v, tipo) in vaos)
                    if (Mathf.Abs(v.ini - ab.ini) < 0.01f) _vaos.Add(new VaoFeito { w = w, ab = ab, tipo = tipo, lado = lado });
            // o que vai nas aberturas: os degraus das portas até o chão, as tábuas, as grades e as portas de enrolar
            float yaw = w.yaw;
            Vector2 n2 = new Vector2(u.y, -u.x);
            foreach (var vf in _vaos)
            {
                if (vf.w != w) continue;
                var ab = vf.ab;
                float meio = ab.ini + ab.larg / 2;
                switch (vf.tipo)
                {
                    case TipoDeVao.PortaDaFrente:
                    case TipoDeVao.Doca:
                        // a porta de enrolar recolhida em cima (a da frente fica aberta; trancar é da Parte M7)
                        Placa(w, ab.ini - 0.1f, ab.ini + ab.larg + 0.1f, ab.alto + 0.02f, ab.alto + 0.47f, e + 0.25f, 0.45f, "Metal");
                        DegrausAteOChao(w.Ponto(meio, 0f, e), n2, ab.larg + 0.4f, vf.tipo == TipoDeVao.PortaDaFrente ? TracadoDaCidade.AlturaDaCalcada : 0f, chao);
                        break;
                    case TipoDeVao.PortaDosFundos:
                        FolhaDePorta(w, ab.ini, ab.larg, ab.alto, 1, "Metal", Faixa(80f, 100f));
                        DegrausAteOChao(w.Ponto(meio, 0f, e), n2, ab.larg + 0.4f, 0f, chao);
                        break;
                    case TipoDeVao.Vitrine:
                        Placa(w, ab.ini - 0.05f, ab.ini + ab.larg + 0.05f, ab.alto + 0.02f, ab.alto + 0.47f, e + 0.25f, 0.45f, "Metal");
                        if (p.tipo == TipoDeLoja.Armas) Grade(w, ab, e + 0.06f);
                        break;
                    case TipoDeVao.Janela:
                        if (p.tipo == TipoDeLoja.Armas && f == 0) Grade(w, ab, e + 0.06f);
                        else TratarJanela(w, ab, new[] { 5, 1 });
                        break;
                    case TipoDeVao.Alta:
                        Grade(w, ab, e + 0.06f);
                        break;
                }
            }
            // o peitoril corrido dos andares de cima, na frente e atrás
            if (f >= 1 && (lado == 0 || lado == 2))
            {
                var c = (a + b) / 2 + n2 * (e + 0.04f);
                Caixa(new Vector3(c.x, p.Base(f) + m.peitoril - 0.04f, c.y), new Vector3((b - a).magnitude + 0.1f, 0.08f, 0.08f), p.faixa, yaw);
            }
        }

        // as vitrines do salão: o que sobra da frente dele, dos lados das portas, em vãos de até 4,6 m
        void Vitrines(float s0, float s1, float h, List<(Abertura ab, TipoDeVao tipo)> vaos, List<Vector2> ocupados)
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
            float alto = Mathf.Min(2.9f, h - 1.4f);
            foreach (var l in livres)
            {
                float comp = l.y - l.x;
                if (comp < 1.5f) continue;
                int n = Mathf.CeilToInt(comp / 4.6f);
                float cada = comp / n;
                for (int i = 0; i < n; i++)
                {
                    float a = l.x + cada * i + 0.3f, b = l.x + cada * (i + 1) - 0.3f;
                    vaos.Add((new Abertura(a, b - a, 0.45f, alto), TipoDeVao.Vitrine));
                }
            }
        }

        // a grade de ferro na abertura (as barras em pé e duas deitadas), por fora
        void Grade(Parede w, Abertura ab, float fora)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(ab.larg / 0.14f));
            for (int i = 1; i < n; i++)
            {
                float s = ab.ini + ab.larg * i / n;
                Placa(w, s - 0.015f, s + 0.015f, ab.baixo, ab.alto, fora, 0.03f, "Metal");
            }
            foreach (float y in new[] { ab.baixo + 0.15f, ab.alto - 0.15f })
                Placa(w, ab.ini, ab.ini + ab.larg, y - 0.02f, y + 0.02f, fora + 0.02f, 0.03f, "Metal");
        }

        /// <summary>
        /// Os degraus de uma porta do térreo (borda: o meio da soleira, por fora) até o chão em frente dela (mais 'acima':
        /// na frente, a calçada): a loja fica no chão mais alto embaixo dela, e na frente o chão pode estar mais baixo.
        /// </summary>
        void DegrausAteOChao(Vector3 borda, Vector2 fora, float largura, float acima, Func<float, float, float> chao)
        {
            var b = new Vector2(borda.x, borda.z);
            var pe = b + fora * 1.2f;
            float ate = chao(pe.x, pe.y) + acima, de = PlantaDeLoja.Piso;
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

        // ------------------------------------------------------------ as paredes de dentro (dos cômodos vizinhos)

        sealed class Interna
        {
            public int andar;
            public Vector2 a, b;
            public bool vertical;
            public float inicio;
            public Comodo menor, maior;
            public Comodo LadoN => vertical ? maior : menor;
        }

        struct PortaFeita { public Parede w; public Abertura ab; public Ligacao l; public Interna i; }

        readonly List<Interna> _internas = new List<Interna>();
        readonly List<PortaFeita> _portas = new List<PortaFeita>();

        void Internas(int f)
        {
            var cs = p.ComodosDo(f);
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                {
                    Comodo A = cs[i], B = cs[j];
                    Tentar(f, A.x1, B.x0, A.z0, A.z1, B.z0, B.z1, true, A, B);
                    Tentar(f, B.x1, A.x0, B.z0, B.z1, A.z0, A.z1, true, B, A);
                    Tentar(f, A.z1, B.z0, A.x0, A.x1, B.x0, B.x1, false, A, B);
                    Tentar(f, B.z1, A.z0, B.x0, B.x1, A.x0, A.x1, false, B, A);
                }
        }

        void Tentar(int f, float c1, float c2, float a0, float a1, float b0, float b1, bool vertical, Comodo menor, Comodo maior)
        {
            if (Mathf.Abs(c1 - c2) > 0.01f) return;
            float lo = Mathf.Max(a0, b0), hi = Mathf.Min(a1, b1);
            if (hi - lo < 0.05f) return;
            float min = vertical ? z0 : x0, max = vertical ? z1 : x1;
            if (Mathf.Abs(lo - min) < 0.01f) lo += m.esp / 2;
            if (Mathf.Abs(hi - max) < 0.01f) hi -= m.esp / 2;
            _internas.Add(new Interna
            {
                andar = f, vertical = vertical, inicio = lo, menor = menor, maior = maior,
                a = vertical ? new Vector2(c1, lo) : new Vector2(lo, c1),
                b = vertical ? new Vector2(c1, hi) : new Vector2(hi, c1),
            });
        }

        float LarguraDa(Ligacao l, float L)
        {
            float larg = l.largura > 0f ? l.largura : l.passagem ? m.passagem : m.porta;
            return L < larg + 0.3f ? Mathf.Max(0.9f, L - 0.3f) : larg;
        }

        void ParedeInterna(Interna i)
        {
            var aberturas = new List<Abertura>();
            var ligs = new List<Ligacao>();
            float L = (i.b - i.a).magnitude;
            foreach (var l in p.ligacoes)
            {
                if (l.b == null || !((l.a == i.menor && l.b == i.maior) || (l.a == i.maior && l.b == i.menor))) continue;
                float larg = LarguraDa(l, L);
                float s = Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f);
                aberturas.Add(new Abertura(s - larg / 2, larg, 0f, Mathf.Min(m.alturaPorta, p.PeDireitoDe(i.andar) - 0.3f)));
                ligs.Add(l);
            }
            var w = LevantarParede(i.a, i.b, aberturas, false, "interna", p.Base(i.andar), p.PeDireitoDe(i.andar));
            // (a parede ordena as aberturas: cada uma volta para a ligação dela pelo meio)
            foreach (var ab in w.aberturas)
            {
                Ligacao achada = null;
                foreach (var l in ligs)
                {
                    float larg = LarguraDa(l, L);
                    if (Mathf.Abs(Mathf.Clamp(l.pos - i.inicio, larg / 2 + 0.15f, L - larg / 2 - 0.15f) - (ab.ini + ab.larg / 2)) < 0.05f) achada = l;
                }
                _portas.Add(new PortaFeita { w = w, ab = ab, l = achada, i = i });
            }
        }

        static bool Privado(Comodo k) => k != null && (k.tipo == "quarto" || k.tipo == "banheiro" || k.tipo == "deposito" || k.tipo == "estoque" || k.tipo == "escritorio" || k.tipo == "acougue");

        // as folhas das portas: para dentro do cômodo dos fundos (do banheiro, do depósito, do escritório) e quase em
        // ângulo reto; a da escada fica sem folha; a de aço é mais grossa, com as barras
        void Portas()
        {
            var comMoldura = new HashSet<Parede>();
            foreach (var pf in _portas)
            {
                if (comMoldura.Add(pf.w)) Molduras(pf.w);
                if (pf.l == null || pf.l.passagem || pf.l.largura >= 2f) continue;
                Comodo a = pf.l.a, b = pf.l.b;
                if (a.tipo == "escada" || b.tipo == "escada") continue;
                Comodo alvo = Privado(b) ? b : Privado(a) ? a : b;
                int dentro = alvo == pf.i.LadoN ? 1 : -1;
                if (pf.l.deAco)
                {
                    float abertura = Faixa(80f, 95f);
                    FolhaDePorta(pf.w, pf.ab.ini, pf.ab.larg, pf.ab.alto, dentro, "Metal", abertura);
                    var (dob, ponta) = PontasDaFolha(pf.w, pf.ab.ini, pf.ab.larg, dentro, abertura, false);
                    // as barras de reforço (só o desenho, por cima da folha)
                    var d = ponta - dob;
                    var c = dob + d * 0.5f;
                    float yaw = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    foreach (float y in new[] { 0.5f, 1.2f, 1.9f })
                        Caixa(new Vector3(c.x, pf.w.baseY + y, c.y), new Vector3(d.magnitude * 0.95f, 0.08f, 0.09f), "Grade", yaw);
                    continue;
                }
                FolhaDePorta(pf.w, pf.ab.ini, pf.ab.larg, pf.ab.alto, dentro, Um("Madeira", "Batente", "MadeiraVelha"), Faixa(85f, 100f));
            }
        }

        // ------------------------------------------------------------ a escada, os balcões, a fachada e a cobertura

        /// <summary>
        /// A escada em U: em cada andar (menos o último), o lance de ida (do patamar do andar, junto do salão, para o
        /// fundo), o patamar do meio, no fundo, e o lance de volta até o patamar do andar de cima; com o corrimão no vão do
        /// meio. No último andar, o guarda-corpo na beira do vão (do lado em que não há lance subindo).
        /// </summary>
        void Escada()
        {
            if (!p.TemEscada) return;
            float xi = p.ex0 + 0.1f, xf = p.ex1 - 0.1f, lance = (xf - xi - 0.2f) / 2;
            float xA = xi + lance / 2, xB = xf - lance / 2;
            float zL = p.zs + 0.1f + PlantaDeLoja.Patamar;
            for (int f = 0; f + 1 < p.andares; f++)
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
            float topo = p.Base(p.andares - 1);
            GuardaCorpo(new Vector3(xi, topo, zL + 0.03f), new Vector3(xi + lance + 0.1f, topo, zL + 0.03f), 1.0f, "Metal");
        }

        // os balcões de alvenaria, de 1 m de altura, com o tampo de madeira
        void Balcoes()
        {
            float piso = PlantaDeLoja.Piso;
            foreach (var r in p.balcoes)
            {
                Caixa(new Vector3(r.center.x, piso + 0.5f, r.center.y), new Vector3(r.width, 1.0f, r.height), Cor.PorNormal("Madeira", "Batente"), colide: true);
                ocupado.Add((r, piso));
                solidos.Add((r, piso));
            }
        }

        /// <summary>
        /// A fachada da frente: a placa em cima das portas e das vitrines (com as letras em blocos e, na farmácia, a cruz
        /// verde) e o toldo; os blocos de ar-condicionado nos andares de cima.
        /// </summary>
        void Fachada()
        {
            float e = m.esp / 2, h = p.PeDireitoDe(0), piso = PlantaDeLoja.Piso;
            float y0 = piso + Mathf.Min(h - 1.25f, 3.45f), y1 = y0 + 0.9f;
            float a = x0 + 0.4f, b = x1 - 0.4f, zf = z0 - e;
            Caixa(new Vector3((a + b) / 2, (y0 + y1) / 2, zf - 0.06f), new Vector3(b - a, y1 - y0, 0.12f), p.placa);
            // as letras (blocos claros, ou escuros na placa branca) e o símbolo
            string letra = p.placa == "PlacaBranca" || p.placa == "PlacaAmarela" ? "Metal" : "PlacaBranca";
            float ini = a + 0.6f, fim = b - 0.6f;
            if (p.tipo == TipoDeLoja.Farmacia)
            {
                float cx = a + 0.75f, cy = (y0 + y1) / 2;
                Caixa(new Vector3(cx, cy, zf - 0.13f), new Vector3(0.55f, 0.18f, 0.03f), p.placa == "PlacaVerde" ? "PlacaBranca" : "PlacaVerde");
                Caixa(new Vector3(cx, cy, zf - 0.13f), new Vector3(0.18f, 0.55f, 0.03f), p.placa == "PlacaVerde" ? "PlacaBranca" : "PlacaVerde");
                ini = a + 1.4f;
            }
            int n = Mathf.Clamp(Mathf.FloorToInt((fim - ini) / 0.42f), 3, 14);
            float largura = Mathf.Min(fim - ini, n * 0.42f), x = (ini + fim) / 2 - largura / 2;
            for (int k = 0; k < n; k++)
                Caixa(new Vector3(x + largura * (k + 0.5f) / n, (y0 + y1) / 2, zf - 0.13f), new Vector3(0.24f, 0.42f, 0.03f), letra);
            if (p.toldo != null)
            {
                var M = T(new Vector3(0f, y0 - 0.25f, zf - 0.6f)) * RX(-12f);
                CaixaM(M, new Vector3(p.W - 0.3f, 0.1f, 1.2f), p.toldo);
            }
            for (int f = 1; f < p.andares; f++)
                if (Chance(0.35f))
                    Caixa(new Vector3(Mathf.Lerp(x0, x1, Faixa(0.2f, 0.8f)), p.Base(f) + 0.6f, zf - 0.28f), new Vector3(0.8f, 0.5f, 0.5f), Um("ConcretoClaro", "Metal"));
        }

        /// <summary>A platibanda em volta da cobertura, com o rufo, a caixa d'água e um ar-condicionado.</summary>
        void Cobertura()
        {
            float cob = p.Cobertura, h = PlantaDeLoja.Platibanda;
            for (int lado = 0; lado < 4; lado++)
            {
                Vector2 a = LadoA(lado), b = LadoB(lado);
                float L = (b - a).magnitude, yaw = -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                var c = (a + b) / 2;
                Caixa(new Vector3(c.x, cob + h / 2, c.y), new Vector3(L + m.esp, h, m.esp), p.cor, yaw, colide: true);
                Caixa(new Vector3(c.x, cob + h + 0.03f, c.y), new Vector3(L + m.esp + 0.06f, 0.06f, m.esp + 0.08f), "Metal", yaw);
            }
            float cx = Mathf.Lerp(x0, x1, Chance(0.5f) ? 0.25f : 0.75f), cz = z1 - 2f;
            Caixa(new Vector3(cx, cob + 0.2f + 0.7f, cz), new Vector3(1.6f, 1.4f, 1.6f), Cor.PorNormal("ConcretoClaro", "ParedeAzul"));
            Caixa(new Vector3(cx, cob + 0.1f, cz), new Vector3(1.8f, 0.2f, 1.8f), "Concreto");
            if (Chance(0.6f)) Caixa(new Vector3(-cx, cob + 0.35f, cz), new Vector3(1.0f, 0.7f, 0.7f), "ConcretoClaro");
        }

        // os pisos de cada cômodo e as luminárias (no salão grande, uma a cada ~5 m); a escada fica sem as duas (os lances e
        // o vão das lajes ocupam ela)
        void PorDentro()
        {
            foreach (var k in p.comodos)
            {
                if (k.tipo == "escada") continue;
                float b = p.Base(k.andar), teto = b + p.PeDireitoDe(k.andar);
                Laje(k.x0 + 0.1f, k.x1 - 0.1f, k.z0 + 0.1f, k.z1 - 0.1f, b + 0.005f, 0.005f, k.piso ?? "Piso", false);
                int nx = Mathf.Max(1, Mathf.RoundToInt((k.x1 - k.x0) / 5f)), nz = Mathf.Max(1, Mathf.RoundToInt((k.z1 - k.z0) / 5f));
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                        Luminaria(Mathf.Lerp(k.x0, k.x1, (i + 0.5f) / nx), teto, Mathf.Lerp(k.z0, k.z1, (j + 0.5f) / nz));
            }
        }
    }
}
