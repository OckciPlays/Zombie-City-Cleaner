using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As medidas das construções. "Hoje" são as das casas da demo (o `cidade_casas.py`); "DoJogo" é a escala escolhida
    /// na sala de medidas da Parte M0 (26/10/2026): as de hoje × 1,15, as casas maiores por dentro, para a primeira pessoa.
    /// </summary>
    [System.Serializable]
    public struct Medidas
    {
        public string nome;
        /// <summary>
        /// O tamanho em relação às casas de hoje (1). Os moldes (PlantasDeCasa) foram desenhados na escala 1,15 (a do
        /// jogo) e esticam os cômodos por escala / 1,15. O que é do tamanho do corpo não muda: o degrau, o guarda-corpo,
        /// o peitoril da janela.
        /// </summary>
        public float escala;
        /// <summary>Pé-direito do porão.</summary>
        public float alturaPorao;
        /// <summary>Pé-direito: do piso ao teto.</summary>
        public float altura;
        /// <summary>A laje entre um andar e outro, e o forro do último.</summary>
        public float laje, forro;
        /// <summary>Espessura das paredes.</summary>
        public float esp;
        public float porta, alturaPorta, portaFrente, alturaPortaFrente;
        /// <summary>O vão sem porta entre dois cômodos.</summary>
        public float passagem;
        public float janela, peitoril, topoJanela;
        /// <summary>A janela do banheiro.</summary>
        public float janelaPequena, peitorilPequena;
        public float larguraEscada, pisada;

        public static Medidas Hoje => new Medidas
        {
            nome = "Hoje", escala = 1f, altura = 2.8f, laje = 0.3f, forro = 0.12f, esp = 0.2f, alturaPorao = 2.4f,
            porta = 1.1f, alturaPorta = 2.1f, portaFrente = 1.1f, alturaPortaFrente = 2.1f, passagem = 1.8f,
            janela = 1.2f, peitoril = 0.9f, topoJanela = 2.1f, janelaPequena = 0.6f, peitorilPequena = 1.4f,
            larguraEscada = 1.1f, pisada = 0.27f,
        };

        /// <summary>
        /// A escala das construções do jogo, escolhida na sala de medidas (Parte M0, 26/10/2026): as casas de hoje × 1,15
        /// (pé-direito 3,22 m, porta 1,27 × 2,42 m, corredor da térrea 1,80 m, escada 1,27 m).
        /// </summary>
        public static Medidas DoJogo => Escalada(1.15f);

        /// <summary>
        /// As medidas de hoje esticadas por k (a sala de medidas da Parte M0 compara 1, 1,15 e 1,30): o pé-direito, as
        /// portas, as janelas, a escada e os cômodos. O degrau, o peitoril, a laje e a parede ficam iguais.
        /// </summary>
        public static Medidas Escalada(float k) => new Medidas
        {
            nome = $"×{k:0.00}", escala = k, altura = 2.8f * k, laje = 0.3f, forro = 0.12f, esp = 0.2f, alturaPorao = 2.4f * k,
            porta = 1.1f * k, alturaPorta = 2.1f * k, portaFrente = 1.1f * k, alturaPortaFrente = 2.1f * k, passagem = 1.8f * k,
            janela = 1.2f * k, peitoril = 0.9f, topoJanela = 2.1f * k, janelaPequena = 0.6f * k, peitorilPequena = 1.4f + 0.2f * (k - 1f),
            larguraEscada = 1.1f * k, pisada = 0.28f,
        };
    }

    /// <summary>Uma abertura numa parede: início e largura ao longo dela, e de baixo a cima (a partir da base da parede).</summary>
    public struct Abertura
    {
        public float ini, larg, baixo, alto;
        public Abertura(float ini, float larg, float baixo, float alto) { this.ini = ini; this.larg = larg; this.baixo = baixo; this.alto = alto; }
        public bool EhPorta => baixo < 0.01f;
    }

    /// <summary>Uma parede de a até b (x, z), centrada na linha. n é o lado de fora (para as paredes de fora).</summary>
    public sealed class Parede
    {
        public Vector2 a, u, n;
        public float yaw, L, baseY, altura;
        public bool ext;
        public string nome;
        /// <summary>Os pedaços sólidos: (s0, s1, y0, y1).</summary>
        public readonly List<Vector4> pecas = new List<Vector4>();
        public readonly List<Abertura> aberturas = new List<Abertura>();

        public Vector3 Ponto(float s, float y, float fora)
        {
            Vector2 m = a + u * s + n * fora;
            return new Vector3(m.x, baseY + y, m.y);
        }
    }

    /// <summary>
    /// A base das construções, passada do `Obra` do `cidade_casas.py` (pasta de arte, Modelo 3D/Cidade): peças nas
    /// coordenadas da construção (x à direita, y para cima, a frente em -z), laje com buracos, parede com aberturas e
    /// o que se cola nela, escada e guarda-corpo. Gera o visual (vis) e a colisão (col) juntos.
    /// As subclasses dizem os cômodos e as cores de fora e de dentro; as linhas das paredes (para achar os vãos, que
    /// ficam na cor do batente) são registradas antes de levantar as paredes.
    /// </summary>
    public class Obra
    {
        // do estoque: quem junta a obra em outra malha (MalhasDasCasas) devolve as duas depois (ou fica com elas)
        public readonly MalhaDeObra vis, col;
        public readonly Medidas m;
        public readonly List<Parede> paredes = new List<Parede>();
        protected readonly System.Random rnd;

        /// <summary>
        /// O que ocupa o chão, para os pontos dos móveis (<see cref="MoveisDaObra"/>): o pé de cada lance de escada (com o
        /// espaço para chegar nele), os buracos das lajes, as folhas das portas abertas e o que a subclasse põe (os balcões
        /// das lojas). O retângulo em x, z e a altura do chão em que ele fica.
        /// </summary>
        public readonly List<(Rect area, float chao)> ocupado = new List<(Rect, float)>();
        /// <summary>
        /// As fachadas de prédio, que não são paredes levantadas (são faces da casca): no andar, para as janelas dos
        /// cômodos; na casca, para o abandono.
        /// </summary>
        public readonly List<Parede> paredesDeFora = new List<Parede>();
        /// <summary>
        /// As janelas que a obra já tapou com tábuas (a ponta a da parede, a base dela e o início da abertura), para o abandono
        /// (<see cref="AbandonoDaObra.Janelas"/>) não pôr nada em cima.
        /// </summary>
        public readonly List<Vector4> tapadas = new List<Vector4>();

        /// <summary>A abertura ab da parede w já foi tapada pela obra.</summary>
        public bool Tapada(Parede w, Abertura ab)
        {
            foreach (var t in tapadas)
                if (Mathf.Abs(t.x - w.a.x) < 0.01f && Mathf.Abs(t.y - w.a.y) < 0.01f && Mathf.Abs(t.z - w.baseY) < 0.01f && Mathf.Abs(t.w - ab.ini) < 0.01f)
                    return true;
            return false;
        }

        struct Linha { public Vector2 a, b; public float y0, y1; }
        readonly List<Linha> _linhas = new List<Linha>();

        /// <summary>De fora: a cor da construção. De dentro: o papel do cômodo. Nas bordas das aberturas (o vão): o batente.</summary>
        protected readonly RegraDeCor corDeParede;
        /// <summary>Laje do chão: o piso em cima, concreto no resto.</summary>
        protected readonly RegraDeCor corDoPiso;
        /// <summary>Forro: o teto embaixo e em cima, o batente na borda.</summary>
        protected readonly RegraDeCor corDoForro;
        /// <summary>Laje entre andares: o piso em cima, o teto embaixo, o batente na borda.</summary>
        protected readonly RegraDeCor corDaLaje;

        /// <summary>'colisao' é o tamanho da malha de colisão no estoque (a da casca de um prédio e a de uma loja são médias).</summary>
        public Obra(int semente, Medidas medidas, TamanhoDaMalha colisao = TamanhoDaMalha.Pequena)
        {
            vis = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            col = MalhaDeObra.Emprestar(colisao);
            rnd = new System.Random(semente);
            m = medidas;
            corDeParede = CorDeParede;
            corDoPiso = (n, c) => n.y > 0.9f ? "Piso" : "Concreto";
            corDoForro = (n, c) => Mathf.Abs(n.y) < 0.5f ? "Batente" : "Teto";
            corDaLaje = (n, c) => n.y > 0.9f ? "Piso" : (n.y < -0.9f ? "Teto" : "Batente");
        }

        // ------------------------------------------------------------ sorteio e matrizes

        protected float Faixa(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        protected bool Chance(float p) => rnd.NextDouble() < p;
        protected T Um<T>(params T[] opcoes) => opcoes[rnd.Next(opcoes.Length)];

        public static Matrix4x4 T(Vector3 c) => Matrix4x4.Translate(c);
        public static Matrix4x4 RY(float g) => Matrix4x4.Rotate(Quaternion.Euler(0f, g, 0f));
        public static Matrix4x4 RX(float g) => Matrix4x4.Rotate(Quaternion.Euler(g, 0f, 0f));
        public static Matrix4x4 RZ(float g) => Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, g));

        // ------------------------------------------------------------ peças

        public void Caixa(Vector3 c, Vector3 tam, Cor cor, float yaw = 0f, Matrix4x4? rot = null, bool colide = false, bool visual = true)
        {
            CaixaM(T(c) * RY(yaw) * (rot ?? Matrix4x4.identity), tam, cor, colide, visual);
        }

        public void CaixaM(Matrix4x4 M, Vector3 tam, Cor cor, bool colide = false, bool visual = true)
        {
            if (visual) vis.Caixa(M, tam, cor);
            if (colide) col.Caixa(M, tam, "Concreto");
        }

        /// <summary>Uma caixa de a até b (a linha do meio), com a seção largura × altura; acompanha a inclinação.</summary>
        public void CaixaEntre(Vector3 a, Vector3 b, float largura, float altura, Cor cor, bool colide = false, bool visual = true)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var M = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(d, Vector3.up), Vector3.one);
            CaixaM(M, new Vector3(largura, altura, d.magnitude), cor, colide, visual);
        }

        public void Cilindro(Vector3 baseC, float r0, float r1, float h, int lados, Cor cor) => vis.Cilindro(T(baseC), r0, r1, h, lados, cor);
        public void Tubo(Vector3 a, Vector3 b, float r, Cor cor) => vis.Tubo(a, b, r, 6, cor);

        public void Solido(IList<Vector3> A, IList<Vector3> B, Cor cor, bool colide = false, bool visual = true)
        {
            if (visual) vis.Solido(A, B, cor);
            if (colide) col.Solido(A, B, "Concreto");
        }

        // ------------------------------------------------------------ cores pela posição

        /// <summary>O cômodo onde fica o ponto q, ou null se for fora. Quem decide é a subclasse.</summary>
        protected virtual object ComodoEm(Vector3 q) => null;
        protected virtual string CorDeFora(Vector3 q) => "Batente";
        protected virtual string CorDeDentro(object comodo, Vector3 q) => "ParedeInterna";

        /// <summary>Registra a linha de uma parede (antes de levantar as paredes): perto dela, a face é o vão da abertura.</summary>
        public void RegistrarLinha(Vector2 a, Vector2 b, float y0, float y1) => _linhas.Add(new Linha { a = a, b = b, y0 = y0, y1 = y1 });

        static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            return (p - (a + ab * t)).magnitude;
        }

        string CorDeParede(Vector3 n, Vector3 c)
        {
            Vector3 q = c + n * 0.06f;
            var q2 = new Vector2(q.x, q.z);
            foreach (var l in _linhas)
                if (q.y > l.y0 - 0.01f && q.y < l.y1 + 0.01f && DistSegmento(q2, l.a, l.b) < 0.085f) return "Batente";
            var k = ComodoEm(q);
            return k == null ? CorDeFora(q) : CorDeDentro(k, q);
        }

        // ------------------------------------------------------------ laje, parede e o que vai nela

        /// <summary>
        /// Uma laje de x0..x1 × z0..z1, com o topo em 'topo', e os buracos (retângulos em x, z). Com buracos, ela é
        /// cortada numa grade pelas bordas deles, e cada faixa livre vira uma caixa.
        /// </summary>
        public void Laje(float x0, float x1, float z0, float z1, float topo, float esp, Cor cor, bool colide = false,
                         IList<Rect> buracos = null, bool visual = true)
        {
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
            void Bloco(float a0, float a1, float b0, float b1) =>
                Caixa(new Vector3((a0 + a1) / 2, topo - esp / 2, (b0 + b1) / 2), new Vector3(a1 - a0, esp, b1 - b0), cor, colide: colide, visual: visual);
            var dentro = new List<Rect>();
            if (buracos != null)
                foreach (var b in buracos)
                    if (b.xMax > x0 + 0.005f && b.xMin < x1 - 0.005f && b.yMax > z0 + 0.005f && b.yMin < z1 - 0.005f) dentro.Add(b);
            foreach (var b in dentro) ocupado.Add((b, topo));
            if (dentro.Count == 0) { Bloco(x0, x1, z0, z1); return; }
            var xs = new List<float> { x0, x1 };
            var zs = new List<float> { z0, z1 };
            foreach (var b in dentro)
            {
                xs.Add(Mathf.Clamp(b.xMin, x0, x1)); xs.Add(Mathf.Clamp(b.xMax, x0, x1));
                zs.Add(Mathf.Clamp(b.yMin, z0, z1)); zs.Add(Mathf.Clamp(b.yMax, z0, z1));
            }
            xs.Sort(); zs.Sort();
            bool Livre(float x, float z)
            {
                foreach (var b in dentro) if (b.Contains(new Vector2(x, z))) return false;
                return true;
            }
            for (int j = 0; j < zs.Count - 1; j++)
            {
                float za = zs[j], zb = zs[j + 1];
                if (zb - za < 0.005f) continue;
                int i = 0;
                while (i < xs.Count - 1)
                {
                    if (xs[i + 1] - xs[i] < 0.005f || !Livre((xs[i] + xs[i + 1]) / 2, (za + zb) / 2)) { i++; continue; }
                    int k = i;
                    while (k + 1 < xs.Count - 1 && (xs[k + 2] - xs[k + 1] < 0.005f || Livre((xs[k + 1] + xs[k + 2]) / 2, (za + zb) / 2))) k++;
                    Bloco(xs[i], xs[k + 1], za, zb);
                    i = k + 1;
                }
            }
        }

        /// <summary>
        /// Parede de a até b (x, z), centrada na linha, da base até base + altura, com as aberturas (início, largura,
        /// baixo, alto; medidas a partir de a e da base). Peças sólidas no visual e na colisão. Os cortes (medidas a
        /// partir de a) dividem os pedaços onde muda o cômodo: a cor de cada face sai do meio dela, e um pedaço que
        /// passasse de um cômodo para o outro ficava com o papel do vizinho num pedaço.
        /// </summary>
        public Parede LevantarParede(Vector2 a, Vector2 b, List<Abertura> aberturas, bool ext, string nome, float baseY, float altura,
                                     string peitoril = "Batente", IList<float> cortes = null)
        {
            Vector2 d = b - a;
            float L = d.magnitude;
            Vector2 u = d / L;
            var w = new Parede
            {
                a = a, u = u, n = new Vector2(u.y, -u.x), yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg, L = L,
                ext = ext, nome = nome, baseY = baseY, altura = altura,
            };
            aberturas.Sort((p, q) => p.ini.CompareTo(q.ini));
            w.aberturas.AddRange(aberturas);
            float esp = m.esp;

            void Pedaco(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
                if (cortes != null)
                    foreach (float corte in cortes)
                        if (corte > s0 + 0.01f && corte < s1 - 0.01f) { Pedaco(s0, corte, y0, y1); Pedaco(corte, s1, y0, y1); return; }
                Vector2 c = a + u * ((s0 + s1) / 2);
                Caixa(new Vector3(c.x, baseY + (y0 + y1) / 2, c.y), new Vector3(s1 - s0, y1 - y0, esp), corDeParede, w.yaw, colide: true);
                w.pecas.Add(new Vector4(s0, s1, y0, y1));
            }

            float s = 0f;
            foreach (var ab in w.aberturas)
            {
                Pedaco(s, ab.ini, 0f, altura);
                Pedaco(ab.ini, ab.ini + ab.larg, 0f, ab.baixo);
                Pedaco(ab.ini, ab.ini + ab.larg, ab.alto, altura);
                if (ab.baixo > 0.01f)
                {
                    Vector2 c = a + u * (ab.ini + ab.larg / 2);
                    Caixa(new Vector3(c.x, baseY + ab.baixo - 0.03f, c.y), new Vector3(ab.larg, 0.06f, esp + 0.1f), peitoril, w.yaw);
                }
                s = ab.ini + ab.larg;
            }
            Pedaco(s, L, 0f, altura);
            paredes.Add(w);
            return w;
        }

        /// <summary>
        /// Uma placa colada na parede w: de s0 a s1 ao longo dela, de y0 a y1 (acima da base), afastada 'fora' do meio
        /// da parede (positivo = lado n) e com espessura esp. giro inclina no plano da parede.
        /// </summary>
        public void Placa(Parede w, float s0, float s1, float y0, float y1, float fora, float esp, Cor cor, float giro = 0f, bool colide = false)
        {
            Vector2 c = w.a + w.u * ((s0 + s1) / 2) + w.n * fora;
            Caixa(new Vector3(c.x, w.baseY + (y0 + y1) / 2, c.y), new Vector3(s1 - s0, y1 - y0, esp), cor, w.yaw, RZ(giro), colide);
        }

        /// <summary>As duas pontas (no chão) da folha de porta aberta: a da dobradiça e a de fora.</summary>
        public (Vector2 dobradica, Vector2 ponta) PontasDaFolha(Parede w, float ini, float larg, int dentro, float abertura, bool dobradicaNoInicio)
        {
            Vector2 lado = w.n * dentro;
            Vector2 dob = w.a + w.u * (dobradicaNoInicio ? ini + 0.03f : ini + larg - 0.03f) + lado * (m.esp / 2);
            float fi = abertura * Mathf.Deg2Rad;
            Vector2 d = (dobradicaNoInicio ? w.u : -w.u) * Mathf.Cos(fi) + lado * Mathf.Sin(fi);
            return (dob, dob + d * (larg - 0.06f));
        }

        /// <summary>
        /// Folha de porta aberta para o lado 'dentro' (+1 = n, -1 = -n), com maçaneta, presa no fim da abertura (ou no
        /// começo, com dobradicaNoInicio).
        /// </summary>
        public void FolhaDePorta(Parede w, float ini, float larg, float alto, int dentro, string cor, float abertura = 95f, bool colide = true,
                                 bool dobradicaNoInicio = false)
        {
            float folha = larg - 0.06f, h = alto - 0.06f;
            var (dob, ponta) = PontasDaFolha(w, ini, larg, dentro, abertura, dobradicaNoInicio);
            ocupado.Add((Rect.MinMaxRect(Mathf.Min(dob.x, ponta.x) - 0.08f, Mathf.Min(dob.y, ponta.y) - 0.08f, Mathf.Max(dob.x, ponta.x) + 0.08f, Mathf.Max(dob.y, ponta.y) + 0.08f), w.baseY));
            Vector2 d = (ponta - dob) / folha;
            Vector2 c = dob + d * (folha / 2);
            float yaw = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Caixa(new Vector3(c.x, w.baseY + 0.01f + h / 2, c.y), new Vector3(folha, h, 0.045f), cor, yaw, colide: colide);
            Vector2 mc = dob + d * (folha - 0.08f) - new Vector2(d.y, -d.x) * 0.04f;
            Caixa(new Vector3(mc.x, w.baseY + Mathf.Min(1.0f, h * 0.48f), mc.y), new Vector3(0.04f, 0.04f, 0.1f), "Cromo", yaw);
        }

        /// <summary>Molduras nas aberturas da parede w: portas dos dois lados; janelas por fora (ou dos dois lados).</summary>
        public void Molduras(Parede w, string cor = "Batente", bool janelasPorDentro = false)
        {
            foreach (var ab in w.aberturas)
            {
                bool porta = ab.EhPorta;
                int lados = (porta || !w.ext || janelasPorDentro) ? 2 : 1;
                for (int k = 0; k < lados; k++)
                {
                    int lado = k == 0 ? 1 : -1;
                    float fora = lado * (m.esp / 2 + 0.02f);
                    float yLo = porta ? 0f : ab.baixo - 0.05f;
                    Placa(w, ab.ini - 0.1f, ab.ini, yLo, ab.alto, fora, 0.04f, cor);
                    Placa(w, ab.ini + ab.larg, ab.ini + ab.larg + 0.1f, yLo, ab.alto, fora, 0.04f, cor);
                    Placa(w, ab.ini - 0.12f, ab.ini + ab.larg + 0.12f, ab.alto, ab.alto + 0.11f, fora, 0.05f, cor);
                }
            }
        }

        /// <summary>
        /// Janela sem vidro (vidro opaco taparia a vista de dentro): aberta ou com tábuas (pesos: aberta, tábuas).
        /// Os cacos nos cantos, as cortinas e as persianas saíram a pedido em 26/10/2026 (pareciam pedaços pretos).
        /// </summary>
        public bool TratarJanela(Parede w, Abertura ab, int[] pesos, bool tabuasPorDentro = false)
        {
            if (rnd.Next(pesos[0] + pesos[1]) < pesos[0]) return false;
            float h = ab.alto - ab.baixo;
            float fora = (tabuasPorDentro ? -1 : 1) * (m.esp / 2 + 0.05f);
            float[] ys = { 0.25f, h / 2, h - 0.25f };
            string[] cores = { "MadeiraVelha", "Madeira", "BancoMadeira" };
            for (int k = 0; k < 3; k++)
                Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, ab.baixo + ys[k] - 0.08f, ab.baixo + ys[k] + 0.08f, fora, 0.03f, cores[k], Faixa(-9f, 9f));
            tapadas.Add(new Vector4(w.a.x, w.a.y, w.baseY, ab.ini));
            return true;
        }

        /// <summary>Luminária apagada presa no teto (teto = altura da face de baixo da laje).</summary>
        public void Luminaria(float cx, float teto, float cz)
        {
            Cilindro(new Vector3(cx, teto - 0.07f, cz), 0.12f, 0.1f, 0.07f, 8, "Metal");
            vis.Esfera(T(new Vector3(cx, teto - 0.1f, cz)), 0.07f, "Farol");
        }

        /// <summary>Lâmpada pendurada no fio (porão).</summary>
        public void Lampada(float cx, float teto, float cz)
        {
            Tubo(new Vector3(cx, teto, cz), new Vector3(cx, teto - 0.45f, cz), 0.008f, "Grade");
            Cilindro(new Vector3(cx, teto - 0.5f, cz), 0.03f, 0.03f, 0.05f, 6, "Metal");
            vis.Esfera(T(new Vector3(cx, teto - 0.54f, cz)), 0.045f, "Farol");
        }

        // ------------------------------------------------------------ escada e guarda-corpo

        Vector3[] _ladoA, _ladoB;

        /// <summary>Quantos espelhos (a altura de cada degrau) uma subida tem: perto de 18,5 cm cada.</summary>
        public static int Espelhos(float subida) => Mathf.Max(2, Mathf.RoundToInt(subida / 0.185f));

        /// <summary>
        /// Um lance reto: do meio da borda do primeiro degrau (no piso de baixo, baseY), subindo na direção 'dir'
        /// (horizontal) até 'topoY'. Cada degrau é um bloco do degrau até a linha de baixo da escada (por baixo ela é
        /// lisa e inclinada). A colisão é uma rampa (o jogador e o NavMesh sobem sem tropeçar). Devolve o comprimento.
        /// </summary>
        public float Lance(Vector2 inicio, Vector2 dir, float largura, float baseY, float topoY, float pisada,
                           string corDoDegrau, string corDoLado, float grossura = 0.15f)
        {
            float subida = topoY - baseY;
            int n = Espelhos(subida);
            float r = subida / n, comp = (n - 1) * pisada;
            Vector2 lat = new Vector2(dir.y, -dir.x) * (largura / 2);
            // o lance e 0,9 m na frente do primeiro degrau ficam livres de móveis
            Vector2 pe = inicio - dir * 0.9f, fim = inicio + dir * comp;
            ocupado.Add((Rect.MinMaxRect(Mathf.Min(Mathf.Min(pe.x + lat.x, pe.x - lat.x), Mathf.Min(fim.x + lat.x, fim.x - lat.x)),
                                         Mathf.Min(Mathf.Min(pe.y + lat.y, pe.y - lat.y), Mathf.Min(fim.y + lat.y, fim.y - lat.y)),
                                         Mathf.Max(Mathf.Max(pe.x + lat.x, pe.x - lat.x), Mathf.Max(fim.x + lat.x, fim.x - lat.x)),
                                         Mathf.Max(Mathf.Max(pe.y + lat.y, pe.y - lat.y), Mathf.Max(fim.y + lat.y, fim.y - lat.y))), baseY));
            float Baixo(float s) => baseY - grossura + s * r / pisada;
            Vector3 P(float s, float y, int lado) { Vector2 q = inicio + dir * s + lat * lado; return new Vector3(q.x, y, q.y); }
            Cor cor = Cor.Por((nn, c) => nn.y > 0.7f ? corDoDegrau : corDoLado);
            // os quatro cantos de cada lado do degrau, reaproveitados (eram dois vetores novos por degrau)
            var A = _ladoA ??= new Vector3[4];
            var B = _ladoB ??= new Vector3[4];
            for (int k = 1; k < n; k++)
            {
                float s0 = (k - 1) * pisada, s1 = k * pisada, topo = baseY + k * r;
                A[0] = P(s0, Baixo(s0), -1); A[1] = P(s1, Baixo(s1), -1); A[2] = P(s1, topo, -1); A[3] = P(s0, topo, -1);
                B[0] = P(s0, Baixo(s0), 1); B[1] = P(s1, Baixo(s1), 1); B[2] = P(s1, topo, 1); B[3] = P(s0, topo, 1);
                Solido(A, B, cor);
            }
            A[0] = P(0, baseY - 0.3f, -1); A[1] = P(comp, topoY - 0.3f, -1); A[2] = P(comp, topoY, -1); A[3] = P(0, baseY, -1);
            B[0] = P(0, baseY - 0.3f, 1); B[1] = P(comp, topoY - 0.3f, 1); B[2] = P(comp, topoY, 1); B[3] = P(0, baseY, 1);
            Solido(A, B, "Concreto", colide: true, visual: false);
            return comp;
        }

        /// <summary>Guarda-corpo de a até b (os pés; pode ser inclinado): corrimão, travessa de baixo e balaústres.</summary>
        public void GuardaCorpo(Vector3 a, Vector3 b, float altura, string cor, bool colide = true)
        {
            Vector3 up = Vector3.up;
            CaixaEntre(a + up * altura, b + up * altura, 0.06f, 0.05f, cor);
            CaixaEntre(a + up * 0.12f, b + up * 0.12f, 0.04f, 0.04f, cor);
            float L = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            int n = Mathf.Max(2, Mathf.RoundToInt(L / 0.16f));
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                bool poste = i == 0 || i == n;
                float g = poste ? 0.07f : 0.035f;
                Caixa(p + up * (altura / 2), new Vector3(g, altura, g), cor);
            }
            if (colide) CaixaEntre(a + up * (altura / 2), b + up * (altura / 2), 0.05f, altura, "Concreto", colide: true, visual: false);
        }

        /// <summary>
        /// Degraus de uma soleira na altura 'altura' até o chão (y = 0): da borda (o meio da beira da soleira), para
        /// 'fora'. Devolve o ponto do pé da escada.
        /// </summary>
        public Vector2 Degraus(Vector2 borda, Vector2 fora, float largura, float altura, string cor = "Concreto")
        {
            int n = Mathf.CeilToInt(altura / 0.19f - 0.05f);
            float yaw = -Mathf.Atan2(fora.y, fora.x) * Mathf.Rad2Deg + 90f;
            if (n <= 1)
            {
                Vector2 c = borda + fora * 0.18f;
                Caixa(new Vector3(c.x, altura * 0.33f - 0.02f, c.y), new Vector3(largura, altura * 0.66f + 0.04f, 0.36f), cor, yaw, colide: true);
                return borda + fora * 0.36f;
            }
            float r = altura / n, p = 0.3f;
            for (int k = 1; k < n; k++)
            {
                float topo = altura - k * r;
                Vector2 c = borda + fora * ((k - 1) * p + p / 2);
                Caixa(new Vector3(c.x, (topo - 0.05f) / 2, c.y), new Vector3(largura, topo + 0.05f, p), cor, yaw, colide: true);
            }
            return borda + fora * ((n - 1) * p);
        }
    }
}
