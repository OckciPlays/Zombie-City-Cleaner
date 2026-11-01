using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O bunker novo levantado a partir da <see cref="PlantaDeBunker"/> (o Plano_Bunker.md da pasta de arte, a parte 2: a
    /// casca), como o <see cref="Predio"/>, em duas partes:
    ///  - <b>a casca</b> (<see cref="MontarCasca"/>): as paredes de fora e as lajes dos 5 andares, o núcleo inteiro (as
    ///    paredes da escada, do poço e do duto, a escada em U do B4 até a casinha no alto do morro e as portas do
    ///    elevador), a casinha, a frente (o vão do portão, a parede de cima dele e as abas de concreto que seguram o
    ///    morro), as torres de ventilação e a antena;
    ///  - <b>um andar por dentro</b> (<see cref="MontarAndar"/>): as paredes de dentro com as portas, as passagens e as
    ///    janelas de dentro, o piso de cada cômodo e as luminárias (e, no B0, as vagas pintadas; no B2, o balcão).
    /// O portão, a portinhola e as portas de aço (a do B4 e a da casinha) são os modelos do Blender (o lote do bunker da
    /// mobília nova), postos por quem monta, como os móveis (<see cref="MoveisDoBunker"/>). As luzes de cada parte ficam em
    /// <see cref="Luzes"/>, para quem monta pôr as lâmpadas. Coordenadas da planta: o meio no centro, a frente em -z, o
    /// chão da garagem (o terreno) em y = 0. O morro de terra, o pátio e a cerca saem de <see cref="MontarMorro"/>, numa obra
    /// de quem põe o bunker no chão.
    /// </summary>
    public sealed class Bunker : Obra
    {
        public readonly Andar[] andares = new Andar[Andares];
        readonly int _parte;

        /// <summary>As luzes da parte: onde (embaixo da luminária), o alcance e a força.</summary>
        public readonly List<(Vector3 pos, float alcance, float forca)> Luzes = new List<(Vector3, float, float)>();

        /// <summary>A casinha da saída de cima: por dentro, o concreto claro.</summary>
        static readonly object Casinha = new object();

        static readonly Cor CorDaLajeDoBunker = Cor.PorNormal("Concreto", "Concreto", "Teto");
        static readonly Cor CorDoPatamar = Cor.PorNormal("Concreto", "ConcretoClaro", "ConcretoClaro");
        /// <summary>O corrimão e o guarda-corpo da escada, amarelos (como na referência).</summary>
        const string CorDoCorrimao = "PlacaAmarela";

        /// <summary>A casca (parte -1) ou o andar 'parte' (0 = B0 ... 4 = B4).</summary>
        public Bunker(Medidas medidas, int parte, int semente = 1)
            : base(semente * 31 + parte, medidas, parte < 0 ? TamanhoDaMalha.Media : TamanhoDaMalha.Pequena)
        {
            _parte = parte;
            for (int f = 0; f < Andares; f++) andares[f] = DoAndar(f);
        }

        // ------------------------------------------------------------ cores pela posição

        protected override object ComodoEm(Vector3 q)
        {
            if (q.y > TopoDaCaixa - 0.05f)
                return Escada.Contains(new Vector2(q.x, q.z)) && q.y < Morro + AlturaDaCasinha ? Casinha : null;
            if (q.x < X0 || q.x > X1 || q.z < Z0 || q.z > Z1) return null;
            return andares[AndarEm(q.y)].Em(q.x, q.z);
        }

        protected override string CorDeFora(Vector3 q) => q.y < -0.05f ? "Concreto" : "ConcretoClaro";
        protected override string CorDeDentro(object k, Vector3 q) => k == Casinha ? "ConcretoClaro" : ((Comodo)k).papel ?? "ConcretoClaro";

        // ------------------------------------------------------------ a casca

        /// <summary>A casca: as paredes de fora, as lajes, o núcleo com a escada, a casinha, a frente e o que vai no morro.</summary>
        public void MontarCasca()
        {
            LinhasDoNucleo();
            for (int f = 0; f < Andares; f++)
            {
                float y0 = Base(f) - GrossuraDaLaje, y1 = f == 0 ? TopoDaCaixa : Teto(f);
                for (int lado = 0; lado < 4; lado++) ParedeExterna(f, lado, y0, y1);
                Laje(X0 + ParedeDeFora / 2, X1 - ParedeDeFora / 2, Z0 + ParedeDeFora / 2, Z1 - ParedeDeFora / 2, Base(f), GrossuraDaLaje,
                     CorDaLajeDoBunker, true, f < Andares - 1 ? new[] { VaoDaEscada } : null);
            }
            // a laje de cima da garagem (a terra fica por cima dela), com o vão da escada que sobe até a casinha
            Laje(X0 + ParedeDeFora / 2, X1 - ParedeDeFora / 2, Z0 + ParedeDeFora / 2, Z1 - ParedeDeFora / 2, TopoDaCaixa, GrossuraDaLaje,
                 CorDaLajeDoBunker, true, new[] { VaoDaSubida });
            NucleoDoBunker();
            EscadaDoBunker();
            CasinhaDoBunker();
            Frente();
            NoMorro();
            Avisos();
        }

        // a parede de fora de um lado (0 frente, 1 direita, 2 fundo, 3 esquerda) no andar f, cortada onde muda o cômodo
        // (a cor de dentro de cada pedaço é a do cômodo dele); na frente da garagem, o vão do portão
        void ParedeExterna(int f, int lado, float y0, float y1)
        {
            bool emX = lado == 0 || lado == 2;
            float linha = lado == 0 ? Z0 : lado == 1 ? X1 : lado == 2 ? Z1 : X0;
            float e = ParedeDeFora / 2;
            // as frentes e os fundos ficam entre as laterais, que vão de quina a quina
            float s0 = emX ? X0 + e : Z0 - e, s1 = emX ? X1 - e : Z1 + e;
            var cortes = new List<float> { s0, s1 };
            foreach (var k in andares[f].comodos)
            {
                bool encosta = lado == 0 ? Mathf.Abs(k.z0 - Z0) < 0.01f : lado == 1 ? Mathf.Abs(k.x1 - X1) < 0.01f
                             : lado == 2 ? Mathf.Abs(k.z1 - Z1) < 0.01f : Mathf.Abs(k.x0 - X0) < 0.01f;
                if (!encosta) continue;
                cortes.Add(emX ? k.x0 : k.z0);
                cortes.Add(emX ? k.x1 : k.z1);
            }
            bool portao = f == 0 && lado == 0;
            if (portao) { cortes.Add(PortaoX0); cortes.Add(PortaoX0 + PortaoLargura); }
            cortes.Sort();
            void Pedaco(float a, float b, float ya, float yb)
            {
                if (b - a < 0.01f || yb - ya < 0.01f) return;
                var c = emX ? new Vector3((a + b) / 2, (ya + yb) / 2, linha) : new Vector3(linha, (ya + yb) / 2, (a + b) / 2);
                var tam = emX ? new Vector3(b - a, yb - ya, ParedeDeFora) : new Vector3(ParedeDeFora, yb - ya, b - a);
                Caixa(c, tam, corDeParede, colide: true);
                // a barra por dentro, nos pedaços que vão do chão do andar para cima
                if (ya > Base(f) + 0.01f) return;
                Vector3 dentro = lado == 0 ? Vector3.forward : lado == 1 ? Vector3.left : lado == 2 ? Vector3.back : Vector3.right;
                var pe = emX ? new Vector3((a + b) / 2, Base(f), linha) : new Vector3(linha, Base(f), (a + b) / 2);
                Barra(pe + dentro * (ParedeDeFora / 2), emX ? Vector3.right : Vector3.forward, dentro, b - a);
            }
            for (int i = 0; i + 1 < cortes.Count; i++)
            {
                float a = Mathf.Max(cortes[i], s0), b = Mathf.Min(cortes[i + 1], s1);
                if (b - a < 0.01f) continue;
                float meio = (a + b) / 2;
                if (portao && meio > PortaoX0 && meio < PortaoX0 + PortaoLargura)
                {
                    Pedaco(a, b, y0, 0f);
                    Pedaco(a, b, PortaoAltura, y1);
                }
                else Pedaco(a, b, y0, y1);
            }
        }

        // as linhas das paredes do núcleo, em todos os andares (perto delas, a face é o batente das portas), e as da casinha
        readonly List<(Vector2 a, Vector2 b, int tipo)> _nucleo = new List<(Vector2, Vector2, int)>();

        void LinhasDoNucleo()
        {
            float x0 = X0 + ParedeDeFora / 2;
            _nucleo.Clear();
            _nucleo.Add((new Vector2(x0, Escada.yMin), new Vector2(Escada.xMax, Escada.yMin), 0));
            _nucleo.Add((new Vector2(x0, Escada.yMax), new Vector2(Escada.xMax, Escada.yMax), 0));
            _nucleo.Add((new Vector2(x0, Poco.yMax), new Vector2(Poco.xMax, Poco.yMax), 0));
            _nucleo.Add((new Vector2(Escada.xMax, Escada.yMin), new Vector2(Escada.xMax, Escada.yMax), 1));
            _nucleo.Add((new Vector2(Poco.xMax, Poco.yMin), new Vector2(Poco.xMax, Poco.yMax), 2));
            _nucleo.Add((new Vector2(Poco.xMin, Poco.yMin), new Vector2(Poco.xMin, Poco.yMax), 0));
            for (int f = 0; f < Andares; f++)
                foreach (var (a, b, _) in _nucleo) RegistrarLinha(a, b, Base(f) - GrossuraDaLaje, f == 0 ? TopoDaCaixa : Base(f) + EntreAndares);
            for (int i = 0; i < 2; i++)
                RegistrarLinha(_nucleo[i].a, _nucleo[i].b, TopoDaCaixa, Morro + AlturaDaCasinha);
            RegistrarLinha(_nucleo[3].a, _nucleo[3].b, TopoDaCaixa, Morro + AlturaDaCasinha);
        }

        // as paredes do núcleo de cada andar, do chão até o chão de cima (passando pela laje, que tem o vão), com a porta da
        // escada (sem folha: a de aço do B4 é à parte) e a do elevador, fechada
        void NucleoDoBunker()
        {
            for (int f = 0; f < Andares; f++)
            {
                float b = Base(f), alto = (f == 0 ? TopoDaCaixa : Base(f) + EntreAndares) - b;
                foreach (var (a, c, tipo) in _nucleo)
                {
                    var aberturas = new List<Abertura>();
                    if (tipo == 1) aberturas.Add(new Abertura(PortaDaEscadaZ - a.y - LarguraDaPortaDaEscada / 2, LarguraDaPortaDaEscada, 0f, m.alturaPorta));
                    if (tipo == 2) aberturas.Add(new Abertura(PortaDoElevadorZ - a.y - 0.55f, 1.1f, 0f, 2.2f));
                    var w = LevantarParede(a, c, aberturas, false, "nucleo", b, alto);
                    BarraNaParede(w);
                    // (a porta da escada do B4 tem o batente de aço do modelo da porta)
                    if (tipo > 0 && !(tipo == 1 && f == Andares - 1)) Molduras(w, tipo == 1 ? "Metal" : "Cromo");
                }
                // o elevador parado: a porta fechada (uma chapa com a fresta do meio)
                Caixa(new Vector3(Poco.xMax, b + 1.1f, PortaDoElevadorZ), new Vector3(0.1f, 2.2f, 1.1f), "Metal", colide: true);
                Caixa(new Vector3(Poco.xMax + 0.055f, b + 1.1f, PortaDoElevadorZ), new Vector3(0.01f, 2.2f, 0.02f), "Grade");
            }
        }

        /// <summary>
        /// A escada em U, do B4 até a casinha: em cada subida, o lance de ida (do patamar do andar, junto da porta, para o
        /// fundo da caixa), o patamar do meio e o lance de volta até o patamar de cima; os corrimãos no vão do meio. Da
        /// garagem para cima, duas subidas até o alto do morro, com um patamar no meio delas (sem andar). Lá em cima, o
        /// guarda-corpo do lado em que nada sobe.
        /// </summary>
        void EscadaDoBunker()
        {
            float xi = EscadaXi, xf = EscadaXf, zi = EscadaZi, zf = EscadaZf, xL = FimDoPatamar;
            float lance = (zf - zi - 0.2f) / 2;
            float zA = zi + lance / 2, zB = zf - lance / 2;
            foreach (var (b, t) in Subidas)
            {
                float meio = (b + t) / 2, r = (meio - b) / Espelhos(meio - b);
                float comp = Lance(new Vector2(xL, zA), new Vector2(-1f, 0f), lance, b, meio, m.pisada, "Concreto", "ConcretoClaro");
                float xM = xL - comp;
                Laje(xi, xM, zi, zf, meio, 0.25f, CorDoPatamar, true);
                Lance(new Vector2(xM, zB), new Vector2(1f, 0f), lance, meio, t, m.pisada, "Concreto", "ConcretoClaro");
                float za = zA + lance / 2 - 0.03f, zb = zB - lance / 2 + 0.03f;
                GuardaCorpo(new Vector3(xL - 0.15f, b + r, za), new Vector3(xM + 0.15f, meio - r, za), 0.9f, CorDoCorrimao, false);
                GuardaCorpo(new Vector3(xM + 0.15f, meio + r, zb), new Vector3(xL - 0.15f, t - r, zb), 0.9f, CorDoCorrimao, false);
                // a luz de cada patamar (na parede da porta, em cima dela)
                Luminaria(new Vector3(xf - 0.05f, b + 2.95f, PortaDaEscadaZ), true);
                Luzes.Add((new Vector3(xL + 0.5f, b + 2.7f, PortaDaEscadaZ), 7f, 2.8f));
            }
            // o patamar do meio da subida até o morro (não tem andar) e o de cima, na casinha
            Laje(xL, xf, zi, zf, Morro / 2, 0.25f, CorDoPatamar, true);
            Laje(xL, xf, zi, zf, Morro, 0.25f, CorDoPatamar, true);
            GuardaCorpo(new Vector3(xL + 0.03f, Morro, zi), new Vector3(xL + 0.03f, Morro, zi + lance + 0.1f), 1.0f, CorDoCorrimao);
            Luminaria(new Vector3(xf - 0.05f, Morro + 2.6f, PortaDaEscadaZ + 1.1f), true);
            Luzes.Add((new Vector3(xL, Morro + 2.3f, PortaDaEscadaZ), 7f, 2.8f));
        }

        // a casinha da saída de cima: as paredes do núcleo continuam da laje da garagem até o teto dela, com a porta para o
        // morro (a folha é à parte), e a laje em cima
        void CasinhaDoBunker()
        {
            float b = TopoDaCaixa, alto = Morro + AlturaDaCasinha - b;
            for (int i = 0; i < 2; i++) LevantarParede(_nucleo[i].a, _nucleo[i].b, new List<Abertura>(), true, "casinha", b, alto);
            var porta = new Abertura(PortaDaEscadaZ - Escada.yMin - LarguraDaPortaDaEscada / 2, LarguraDaPortaDaEscada, Morro - b, Morro - b + m.alturaPorta);
            LevantarParede(_nucleo[3].a, _nucleo[3].b, new List<Abertura> { porta }, true, "casinha", b, alto, "Concreto");
            Caixa(new Vector3(X0, (b + Morro + AlturaDaCasinha) / 2, Escada.center.y), new Vector3(ParedeDeFora, alto, Escada.height + 0.2f), corDeParede, colide: true);
            float topo = Morro + AlturaDaCasinha;
            Laje(X0 - ParedeDeFora / 2, Escada.xMax + 0.15f, Escada.yMin - 0.15f, Escada.yMax + 0.15f, topo + 0.25f, 0.25f,
                 Cor.PorNormal("Concreto", "ConcretoClaro", "ConcretoClaro"), true);
            // a soleira da porta, por fora
            Caixa(new Vector3(Escada.xMax + 0.4f, Morro + 0.05f, PortaDaEscadaZ), new Vector3(0.6f, 0.1f, LarguraDaPortaDaEscada + 0.4f), "Concreto", colide: true);
        }

        // a frente: o portal no corte do morro (a parede em cima do portão, até o alto do morro), as abas de concreto que
        // abrem para fora seguindo o talude e o batente de aço do portão
        void Frente()
        {
            float e = ParedeDeFora / 2;
            Caixa(new Vector3((CorteX0 + CorteX1) / 2, (TopoDaCaixa + Morro + 0.3f) / 2, Z0), new Vector3(CorteX1 - CorteX0 + ParedeDeFora, Morro + 0.3f - TopoDaCaixa, ParedeDeFora),
                  corDeParede, colide: true);
            foreach (int lado in new[] { -1, 1 })
            {
                float x = lado < 0 ? CorteX0 : CorteX1;
                var p0 = new Vector2(x, Z0);
                var p1 = new Vector2(x + lado * AbreCorte, Z0 - Morro * Talude);
                Vector2 d = (p1 - p0).normalized;
                // para fora do corte (para o lado da terra)
                Vector2 n = new Vector2(-d.y, d.x);
                if (Vector2.Dot(n, new Vector2(lado, 0f)) < 0f) n = -n;
                Vector3 P(Vector2 q, float y, float fora) => new Vector3(q.x + n.x * fora, y, q.y + n.y * fora);
                var A = new List<Vector3> { P(p0, -0.3f, 0f), P(p1, -0.3f, 0f), P(p1, 0.3f, 0f), P(p0, Morro + 0.3f, 0f) };
                var B = new List<Vector3> { P(p0, -0.3f, ParedeDeFora), P(p1, -0.3f, ParedeDeFora), P(p1, 0.3f, ParedeDeFora), P(p0, Morro + 0.3f, ParedeDeFora) };
                Solido(A, B, "ConcretoClaro", colide: true);
            }
            // o batente de aço em volta do portão, por fora
            float zf = Z0 - e - 0.06f;
            Caixa(new Vector3(PortaoX0 - 0.1f, PortaoAltura / 2, zf), new Vector3(0.2f, PortaoAltura, 0.12f), "Metal");
            Caixa(new Vector3(PortaoX0 + PortaoLargura + 0.1f, PortaoAltura / 2, zf), new Vector3(0.2f, PortaoAltura, 0.12f), "Metal");
            Caixa(new Vector3(PortaoX0 + PortaoLargura / 2, PortaoAltura + 0.1f, zf), new Vector3(PortaoLargura + 0.4f, 0.2f, 0.12f), "Metal");
            // a placa redonda em cima do portão (como na foto), por enquanto lisa
            vis.Cilindro(T(new Vector3(PortaoX0 + PortaoLargura / 2, PortaoAltura + 0.95f, zf - 0.02f)) * RX(-90f), 0.6f, 0.6f, 0.06f, 16, "PlacaAmarela");
            vis.Cilindro(T(new Vector3(PortaoX0 + PortaoLargura / 2, PortaoAltura + 0.95f, zf - 0.08f)) * RX(-90f), 0.45f, 0.45f, 0.02f, 16, "Poste");
        }

        // em cima do morro: as duas torres de ventilação (o cano em cogumelo, saindo do duto) e a antena do rádio
        void NoMorro()
        {
            foreach (var p in new[] { new Vector2(-2f, -6f), new Vector2(8f, 6f) })
            {
                Caixa(new Vector3(p.x, Morro + 0.25f, p.y), new Vector3(1.1f, 0.5f, 1.1f), "ConcretoClaro", colide: true);
                Cilindro(new Vector3(p.x, Morro + 0.5f, p.y), 0.25f, 0.25f, 1.9f, 10, "Metal");
                Cilindro(new Vector3(p.x, Morro + 2.15f, p.y), 0.3f, 0.3f, 0.35f, 10, "Grade");
                Cilindro(new Vector3(p.x, Morro + 2.5f, p.y), 0.65f, 0.12f, 0.4f, 10, "Metal");
            }
            var mastro = new Vector3(12f, Morro, 9f);
            Caixa(mastro + Vector3.up * 0.2f, new Vector3(0.9f, 0.4f, 0.9f), "ConcretoClaro", colide: true);
            Tubo(mastro, mastro + Vector3.up * 6.5f, 0.06f, "Metal");
            for (int k = 0; k < 4; k++)
            {
                float y = 2f + k * 1.3f, l = 1.2f - k * 0.2f;
                Tubo(mastro + new Vector3(-l / 2, y, 0f), mastro + new Vector3(l / 2, y, 0f), 0.02f, "Metal");
            }
            // a parabólica, presa no mastro, virada para o céu do sul
            var prato = T(mastro + new Vector3(0f, 1.6f, -0.35f)) * RX(-55f);
            vis.Cilindro(prato, 0.65f, 0.65f, 0.06f, 14, "ConcretoClaro");
            vis.Cilindro(prato * T(new Vector3(0f, 0.06f, 0f)), 0.03f, 0.03f, 0.55f, 6, "Metal");
        }

        // ------------------------------------------------------------ o acabamento (a barra, as faixas de aviso e os letreiros)

        /// <summary>
        /// Os cômodos com a barra verde-escura até 1,2 m e o friso amarelo em cima (como no bunker antigo): a circulação e
        /// os de serviço. As salas, os quartos, os banheiros, a cozinha e a enfermaria ficam só com a pintura.
        /// </summary>
        static readonly HashSet<string> ComBarra = new HashSet<string>
        {
            "corredor", "garagem", "oficina", "combustivel", "operacoes", "arsenal", "deposito", "despensa", "lavanderia",
            "rouparia", "esgoto", "pecas", "eletrica", "gerador", "agua", "ar",
        };
        public const float AlturaDaBarra = 1.2f;
        const string CorDaBarra = "ParedeVerdeEscura", CorDoFriso = "Faixa", Preto = "Pneu";

        bool TemBarra(Vector3 q) => ComodoEm(q) is Comodo k && ComBarra.Contains(k.tipo);

        // a barra numa face de parede: 'pe' é o pé da face (no chão do andar), 'u' corre ao longo dela e 'fora' sai dela
        void Barra(Vector3 pe, Vector3 u, Vector3 fora, float comprimento)
        {
            if (comprimento < 0.05f || !TemBarra(pe + fora * 0.1f + Vector3.up * 0.6f)) return;
            var giro = Quaternion.LookRotation(fora, Vector3.up);
            float largura = comprimento - 0.002f;
            CaixaM(Matrix4x4.TRS(pe + fora * 0.006f + Vector3.up * (AlturaDaBarra / 2), giro, Vector3.one), new Vector3(largura, AlturaDaBarra, 0.012f), CorDaBarra);
            CaixaM(Matrix4x4.TRS(pe + fora * 0.008f + Vector3.up * (AlturaDaBarra + 0.03f), giro, Vector3.one), new Vector3(largura, 0.06f, 0.016f), CorDoFriso);
        }

        // a barra nos dois lados de uma parede levantada, nos pedaços que começam no chão (desviando das portas e das janelas)
        void BarraNaParede(Parede w)
        {
            foreach (var pc in w.pecas)
            {
                if (pc.z > 0.01f || pc.w < 0.5f) continue;
                foreach (int lado in new[] { 1, -1 })
                {
                    Vector3 q = w.Ponto((pc.x + pc.y) / 2, 0.6f, lado * (m.esp / 2 + 0.1f));
                    if (!TemBarra(q)) continue;
                    float topo = Mathf.Min(pc.w, AlturaDaBarra);
                    Placa(w, pc.x, pc.y, 0f, topo, lado * (m.esp / 2 + 0.006f), 0.012f, CorDaBarra);
                    if (pc.w >= AlturaDaBarra + 0.06f)
                        Placa(w, pc.x, pc.y, AlturaDaBarra, AlturaDaBarra + 0.06f, lado * (m.esp / 2 + 0.008f), 0.016f, CorDoFriso);
                }
            }
        }

        /// <summary>
        /// A faixa de aviso amarela e preta: 'meio' no meio dela, na face; 'u' corre ao longo dela, 'fora' sai da face; os
        /// blocos alternados, quadrados.
        /// </summary>
        void Listras(Vector3 meio, Vector3 u, Vector3 fora, float comprimento, float largura)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(comprimento / largura));
            float passo = comprimento / n;
            var giro = Quaternion.LookRotation(fora, Vector3.Cross(fora, u));
            for (int i = 0; i < n; i++)
            {
                Vector3 c = meio + u * (-comprimento / 2 + passo * (i + 0.5f)) + fora * 0.007f;
                CaixaM(Matrix4x4.TRS(c, giro, Vector3.one), new Vector3(passo - 0.001f, largura, 0.014f), i % 2 == 0 ? CorDoFriso : Preto);
            }
        }

        // as letras pintadas, em traços numa grade de 2 × 4 (x0, y0, x1, y1 de cada traço): só as que os letreiros usam
        static readonly Dictionary<char, float[]> Letras = new Dictionary<char, float[]>
        {
            ['A'] = new float[] { 0, 0, 0, 3, 0, 3, 1, 4, 1, 4, 2, 3, 2, 3, 2, 0, 0, 2, 2, 2 },
            ['B'] = new float[] { 0, 0, 0, 4, 0, 4, 1.5f, 4, 1.5f, 4, 2, 3.5f, 2, 3.5f, 2, 2.5f, 2, 2.5f, 1.5f, 2, 0, 2, 1.5f, 2, 1.5f, 2, 2, 1.5f, 2, 1.5f, 2, 0.5f, 2, 0.5f, 1.5f, 0, 1.5f, 0, 0, 0 },
            ['C'] = new float[] { 2, 4, 0, 4, 0, 4, 0, 0, 0, 0, 2, 0 },
            ['D'] = new float[] { 0, 0, 0, 4, 0, 4, 1.3f, 4, 1.3f, 4, 2, 3.3f, 2, 3.3f, 2, 0.7f, 2, 0.7f, 1.3f, 0, 1.3f, 0, 0, 0 },
            ['E'] = new float[] { 2, 4, 0, 4, 0, 4, 0, 0, 0, 0, 2, 0, 0, 2, 1.5f, 2 },
            ['G'] = new float[] { 2, 4, 0, 4, 0, 4, 0, 0, 0, 0, 2, 0, 2, 0, 2, 2, 2, 2, 1, 2 },
            ['H'] = new float[] { 0, 0, 0, 4, 2, 0, 2, 4, 0, 2, 2, 2 },
            ['I'] = new float[] { 1, 0, 1, 4, 0.4f, 4, 1.6f, 4, 0.4f, 0, 1.6f, 0 },
            ['K'] = new float[] { 0, 0, 0, 4, 0, 1.6f, 2, 4, 0.7f, 2.4f, 2, 0 },
            ['L'] = new float[] { 0, 4, 0, 0, 0, 0, 2, 0 },
            ['M'] = new float[] { 0, 0, 0, 4, 0, 4, 1, 2, 1, 2, 2, 4, 2, 4, 2, 0 },
            ['N'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 0, 2, 0, 2, 4 },
            ['O'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 4, 2, 4, 2, 0, 2, 0, 0, 0 },
            ['P'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 4, 2, 4, 2, 2, 2, 2, 0, 2 },
            ['Q'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 4, 2, 4, 2, 0, 2, 0, 0, 0, 1.1f, 0.9f, 2.1f, -0.3f },
            ['R'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 4, 2, 4, 2, 2, 2, 2, 0, 2, 0.8f, 2, 2, 0 },
            ['S'] = new float[] { 2, 4, 0, 4, 0, 4, 0, 2, 0, 2, 2, 2, 2, 2, 2, 0, 2, 0, 0, 0 },
            ['T'] = new float[] { 0, 4, 2, 4, 1, 4, 1, 0 },
            ['U'] = new float[] { 0, 4, 0, 0, 0, 0, 2, 0, 2, 0, 2, 4 },
            ['Y'] = new float[] { 0, 4, 1, 2, 2, 4, 1, 2, 1, 2, 1, 0 },
            ['Z'] = new float[] { 0, 4, 2, 4, 2, 4, 0, 0, 0, 0, 2, 0 },
            ['0'] = new float[] { 0, 0, 0, 4, 0, 4, 2, 4, 2, 4, 2, 0, 2, 0, 0, 0, 0, 0.4f, 2, 3.6f },
            ['1'] = new float[] { 1, 0, 1, 4, 1, 4, 0.3f, 3.3f, 0.4f, 0, 1.6f, 0 },
            ['2'] = new float[] { 0, 4, 2, 4, 2, 4, 2, 2, 2, 2, 0, 2, 0, 2, 0, 0, 0, 0, 2, 0 },
            ['3'] = new float[] { 0, 4, 2, 4, 2, 4, 2, 0, 2, 0, 0, 0, 0.5f, 2, 2, 2 },
            ['4'] = new float[] { 0, 4, 0, 2, 0, 2, 2, 2, 2, 4, 2, 0 },
            ['-'] = new float[] { 0.4f, 2, 1.6f, 2 },
        };

        /// <summary>
        /// Um letreiro pintado numa face de parede: o texto (as letras de <see cref="Letras"/>; o resto vira espaço) centrado em
        /// 'meio' (no pé das letras), correndo em 'u' (a direita de quem lê), saindo 'fora' da face; 'h' é a altura das letras.
        /// Com 'fundo', a placa pintada atrás.
        /// </summary>
        void Letreiro(string texto, Vector3 meio, Vector3 u, Vector3 fora, float h, string cor, string fundo = null)
        {
            float e = h / 4f, passo = 2.6f * e, traco = 0.55f * e;
            float total = texto.Length * passo - 0.6f * e;
            Vector3 inicio = meio - u * (total / 2);
            if (fundo != null)
                CaixaM(Matrix4x4.TRS(meio + Vector3.up * (h / 2) + fora * 0.006f, Quaternion.LookRotation(fora, Vector3.up), Vector3.one),
                       new Vector3(total + h * 0.5f, h * 1.6f, 0.012f), fundo);
            for (int i = 0; i < texto.Length; i++)
            {
                if (!Letras.TryGetValue(texto[i], out var tracos)) continue;
                Vector3 o = inicio + u * (i * passo);
                for (int t = 0; t + 3 < tracos.Length; t += 4)
                {
                    Vector3 a = o + u * (tracos[t] * e) + Vector3.up * (tracos[t + 1] * e) + fora * 0.015f;
                    Vector3 b = o + u * (tracos[t + 2] * e) + Vector3.up * (tracos[t + 3] * e) + fora * 0.015f;
                    Vector3 d = b - a;
                    float l = d.magnitude;
                    if (l < 1e-4f) continue;
                    CaixaM(Matrix4x4.TRS((a + b) / 2, Quaternion.LookRotation(fora, d / l), Vector3.one), new Vector3(traco, l + traco, 0.006f), cor);
                }
            }
        }

        /// <summary>Os nomes dos andares (em inglês: o jogo se passa nos Estados Unidos), pintados em cima da porta da escada.</summary>
        public static readonly string[] NomesNaParede = { "B0 GARAGE", "B1 COMMONS", "B2 KITCHEN", "B3 QUARTERS", "B4 MAINTENANCE" };

        // os avisos da casca: o nome de cada andar em cima da porta da escada (do lado do hall e do lado do patamar), as
        // faixas nos batentes dela, o aviso de acesso restrito no B4 e as faixas em volta do portão (por dentro) e no chão
        void Avisos()
        {
            float xHall = Escada.xMax + m.esp / 2, xPatamar = Escada.xMax - m.esp / 2;
            float meiaPorta = LarguraDaPortaDaEscada / 2 + 0.1f + 0.1f;
            for (int f = 0; f < Andares; f++)
            {
                float b = Base(f), z = PortaDaEscadaZ;
                Letreiro(NomesNaParede[f], new Vector3(xHall, b + 2.66f, z), Vector3.forward, Vector3.right, 0.26f, Preto, CorDoFriso);
                Letreiro(NomesNaParede[f], new Vector3(xPatamar, b + (f == Andares - 1 ? 2.92f : 2.66f), z), Vector3.back, Vector3.left, 0.26f, Preto, CorDoFriso);
                foreach (int s in new[] { -1, 1 })
                    Listras(new Vector3(xHall, b + m.alturaPorta / 2, z + s * meiaPorta), Vector3.up, Vector3.right, m.alturaPorta, 0.14f);
            }
            Letreiro("AUTHORIZED PERSONNEL ONLY", new Vector3(xPatamar, Base(Andares - 1) + 2.57f, PortaDaEscadaZ), Vector3.back, Vector3.left, 0.12f,
                     "PlacaVermelha", "PlacaBranca");
            // o portão, por dentro: as faixas nos dois lados e em cima, e a faixa no chão, na frente dele
            float zi = Z0 + ParedeDeFora / 2;
            Listras(new Vector3(PortaoX0 - 0.2f, PortaoAltura / 2, zi), Vector3.up, Vector3.forward, PortaoAltura, 0.3f);
            Listras(new Vector3(PortaoX0 + PortaoLargura + 0.2f, PortaoAltura / 2, zi), Vector3.up, Vector3.forward, PortaoAltura, 0.3f);
            Listras(new Vector3(PortaoX0 + PortaoLargura / 2, PortaoAltura + 0.2f, zi), Vector3.right, Vector3.forward, PortaoLargura + 0.7f, 0.3f);
            Listras(new Vector3(PortaoX0 + PortaoLargura / 2, 0.0f, zi + 0.35f), Vector3.right, Vector3.up, PortaoLargura, 0.3f);
        }

        // uma luminária: no teto, a calha comprida (ao longo de x ou de z); na parede ('naParede'), a arandela
        void Luminaria(Vector3 c, bool naParede, bool emX = true)
        {
            if (naParede)
            {
                Caixa(c, new Vector3(0.1f, 0.14f, 0.36f), "Metal");
                Caixa(c + new Vector3(-0.055f, -0.02f, 0f), new Vector3(0.02f, 0.08f, 0.28f), "Farol");
                return;
            }
            Caixa(c, emX ? new Vector3(1.3f, 0.07f, 0.2f) : new Vector3(0.2f, 0.07f, 1.3f), "Metal");
            Caixa(c - new Vector3(0f, 0.045f, 0f), emX ? new Vector3(1.2f, 0.025f, 0.11f) : new Vector3(0.11f, 0.025f, 1.2f), "Farol");
        }

        // ------------------------------------------------------------ um andar por dentro

        sealed class Interna
        {
            public Vector2 a, b;
            public bool vertical;
            public float lo;
            public Comodo menor, maior;
            public Comodo LadoN => vertical ? maior : menor;
        }

        readonly List<Interna> _internas = new List<Interna>();

        /// <summary>O andar (a parte deste bunker): as paredes de dentro com as portas, os pisos, as luminárias e o resto.</summary>
        public void MontarAndar()
        {
            int f = _parte;
            var an = andares[f];
            float b = Base(f), teto = Teto(f);
            _internas.Clear();
            var ks = an.comodos;
            for (int i = 0; i < ks.Count; i++)
                for (int j = i + 1; j < ks.Count; j++)
                {
                    Comodo A = ks[i], B = ks[j];
                    if (DoNucleo(A) || DoNucleo(B)) continue;
                    Tentar(A.x1, B.x0, A.z0, A.z1, B.z0, B.z1, true, A, B);
                    Tentar(B.x1, A.x0, B.z0, B.z1, A.z0, A.z1, true, B, A);
                    Tentar(A.z1, B.z0, A.x0, A.x1, B.x0, B.x1, false, A, B);
                    Tentar(B.z1, A.z0, B.x0, B.x1, A.x0, A.x1, false, B, A);
                }
            foreach (var w in _internas) RegistrarLinha(w.a, w.b, b, teto);
            foreach (var w in _internas) ParedeInterna(an, w, b, teto - b);
            foreach (var k in ks)
            {
                if (DoNucleo(k)) continue;
                Laje(k.x0 + 0.1f, k.x1 - 0.1f, k.z0 + 0.1f, k.z1 - 0.1f, b + 0.006f, 0.006f, k.piso ?? "Piso", false);
                Lampadas(k, teto, k.tipo == "garagem" || k.tipo == "oficina" ? 5.5f : 4.0f);
            }
            if (f == 0) Vagas();
            if (f == 2) Balcao(an);
            MoveisDoBunker.Caixas(f, col);
        }

        void Tentar(float c1, float c2, float a0, float a1, float b0, float b1, bool vertical, Comodo menor, Comodo maior)
        {
            if (Mathf.Abs(c1 - c2) > 0.01f) return;
            float lo = Mathf.Max(a0, b0), hi = Mathf.Min(a1, b1);
            if (hi - lo < 0.05f) return;
            float min = vertical ? Z0 : X0, max = vertical ? Z1 : X1;
            if (Mathf.Abs(lo - min) < 0.01f) lo += ParedeDeFora / 2;
            if (Mathf.Abs(hi - max) < 0.01f) hi -= ParedeDeFora / 2;
            _internas.Add(new Interna
            {
                vertical = vertical, lo = lo, menor = menor, maior = maior,
                a = vertical ? new Vector2(c1, lo) : new Vector2(lo, c1),
                b = vertical ? new Vector2(c1, hi) : new Vector2(hi, c1),
            });
        }

        static bool Entre(Comodo x, Comodo y, Comodo a, Comodo b) => (x == a && y == b) || (x == b && y == a);

        // a parede entre dois cômodos, com as portas e as passagens deles (uma passagem mais larga que a parede: sem parede)
        // e as janelas de dentro; as folhas abertas para dentro do cômodo que não é corredor (entre dois, o maior)
        void ParedeInterna(Andar an, Interna i, float baseY, float altura)
        {
            float L = (i.b - i.a).magnitude;
            var aberturas = new List<Abertura>();
            var folhas = new List<(Abertura ab, Ligacao l)>();
            foreach (var l in an.ligacoes)
            {
                if (!Entre(l.a, l.b, i.menor, i.maior)) continue;
                float larg = l.largura > 0f ? l.largura : l.passagem ? m.passagem : m.porta;
                if (larg >= L - 0.3f) return;
                float s = Mathf.Clamp(l.pos - i.lo, larg / 2 + 0.15f, L - larg / 2 - 0.15f);
                var ab = new Abertura(s - larg / 2, larg, 0f, l.passagem && larg > 2f ? Mathf.Min(altura - 0.4f, 2.8f) : m.alturaPorta);
                aberturas.Add(ab);
                if (!l.passagem && string.IsNullOrEmpty(l.modelo)) folhas.Add((ab, l));
            }
            foreach (var (a, b, pos, larg, baixo, alto) in an.janelas)
            {
                if (!Entre(a, b, i.menor, i.maior)) continue;
                float s = Mathf.Clamp(pos - i.lo, larg / 2 + 0.15f, L - larg / 2 - 0.15f);
                aberturas.Add(new Abertura(s - larg / 2, larg, baixo, alto));
            }
            var w = LevantarParede(i.a, i.b, aberturas, false, "interna", baseY, altura, "Concreto");
            BarraNaParede(w);
            if (aberturas.Count > 0) Molduras(w, "Batente");
            foreach (var (ab, l) in folhas)
            {
                Comodo A = l.a, B = l.b;
                Comodo alvo = A.tipo == "corredor" ? B : B.tipo == "corredor" ? A
                            : (A.x1 - A.x0) * (A.z1 - A.z0) >= (B.x1 - B.x0) * (B.z1 - B.z0) ? A : B;
                FolhaDePorta(w, ab.ini, ab.larg, ab.alto, alvo == i.LadoN ? 1 : -1, l.deAco ? "Metal" : "Batente", 90f);
            }
        }

        // as calhas de luz de um cômodo, em linhas ao longo do lado comprido, e as luzes delas
        void Lampadas(Comodo k, float teto, float forca)
        {
            float w = k.x1 - k.x0, d = k.z1 - k.z0;
            bool emX = w >= d;
            float comp = emX ? w : d, larg = emX ? d : w;
            int n = Mathf.Max(1, Mathf.RoundToInt(comp / 5.5f)), linhas = Mathf.Max(1, Mathf.RoundToInt(larg / 6.5f));
            float alcance = Mathf.Clamp(Mathf.Max(comp / n, larg / linhas) * 1.25f + 2.5f, 5.5f, 11f);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < linhas; j++)
                {
                    float s = (i + 0.5f) / n, t = (j + 0.5f) / linhas;
                    float x = emX ? Mathf.Lerp(k.x0, k.x1, s) : Mathf.Lerp(k.x0, k.x1, t);
                    float z = emX ? Mathf.Lerp(k.z0, k.z1, t) : Mathf.Lerp(k.z0, k.z1, s);
                    Luminaria(new Vector3(x, teto - 0.035f, z), false, !emX);
                    Luzes.Add((new Vector3(x, teto - 0.35f, z), alcance, forca));
                }
        }

        // as 4 vagas pintadas no fundo da garagem (as linhas amarelas entre elas)
        void Vagas()
        {
            for (int i = 0; i <= 4; i++)
            {
                var r = R(12.4f + i * 3.8f, 17.6f, 12.4f + i * 3.8f, 23.6f);
                Caixa(new Vector3(r.xMin, 0.012f, r.center.y), new Vector3(0.12f, 0.012f, r.height), "Faixa");
            }
            var faixa = R(12.4f, 17.6f, 27.6f, 17.6f);
            Caixa(new Vector3(faixa.center.x, 0.012f, faixa.yMin), new Vector3(faixa.width + 0.12f, 0.012f, 0.12f), "Faixa");
        }

        // o balcão de servir da cozinha para o refeitório: o tampo de inox em cima do peitoril da janela de dentro
        void Balcao(Andar an)
        {
            foreach (var (a, b, pos, larg, baixo, _) in an.janelas)
            {
                bool vertical = Mathf.Abs(a.x1 - b.x0) < 0.01f || Mathf.Abs(b.x1 - a.x0) < 0.01f;
                float linha = vertical ? (Mathf.Abs(a.x1 - b.x0) < 0.01f ? a.x1 : b.x1) : (Mathf.Abs(a.z1 - b.z0) < 0.01f ? a.z1 : b.z1);
                var c = vertical ? new Vector3(linha, Base(an.f) + baixo + 0.03f, pos) : new Vector3(pos, Base(an.f) + baixo + 0.03f, linha);
                Caixa(c, vertical ? new Vector3(0.7f, 0.05f, larg + 0.1f) : new Vector3(larg + 0.1f, 0.05f, 0.7f), "Cromo", colide: true);
            }
        }

        // ------------------------------------------------------------ o morro, o pátio e a cerca (de quem põe o bunker no chão)

        /// <summary>
        /// O morro de terra em cima da caixa (com o buraco da casinha), os taludes para os lados e para o fundo, a frente em
        /// dois pedaços (um de cada lado do corte do portão; as abas são da casca), o pátio de concreto com as juntas e a cerca
        /// de alambrado com o vão da estrada (<see cref="Patio"/>). 'afunda': quanto o pé dos taludes desce abaixo do chão (no
        /// mundo, o terreno fica uns centímetros abaixo do pátio, e o pé entra nele).
        /// </summary>
        public static void MontarMorro(Obra o, float afunda = 0f)
        {
            float h = Morro, pe = Morro * Talude, peFundo = (Morro + afunda) * Talude;
            var casinha = Rect.MinMaxRect(X0 - ParedeDeFora / 2, Escada.yMin - 0.1f, Escada.xMax + 0.1f, Escada.yMax + 0.1f);
            o.Laje(X0, X1, Z0, Z1, h, Terra, Cor.PorNormal("Grama", "Terra"), true, new[] { casinha });
            Vector3 T(float x, float z) => new Vector3(x, h, z);
            // o pé do talude: 'pe' para fora do alto e, afundado, mais um pouco na mesma inclinação
            Vector3 B(float x, float z) => new Vector3(x, -afunda, z);
            float e = peFundo;
            void Encosta(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var p = new List<Vector3> { a, b, c, d };
                o.vis.Face(p, Vector3.up, "Grama");
                o.col.Face(p, Vector3.up, "Grama");
            }
            Encosta(T(X0, Z1), T(X1, Z1), B(X1 + e, Z1 + e), B(X0 - e, Z1 + e));
            Encosta(T(X0, Z0), T(X0, Z1), B(X0 - e, Z1 + e), B(X0 - e, Z0 - e));
            Encosta(T(X1, Z1), T(X1, Z0), B(X1 + e, Z0 - e), B(X1 + e, Z1 + e));
            // a frente, em dois pedaços, um de cada lado do corte
            float abre = AbreCorte * peFundo / pe;
            Encosta(T(X0, Z0), T(CorteX0, Z0), B(CorteX0 - abre, Z0 - e), B(X0 - e, Z0 - e));
            Encosta(T(CorteX1, Z0), T(X1, Z0), B(X1 + e, Z0 - e), B(CorteX1 + abre, Z0 - e));

            // o pátio de concreto (do portão até a cerca), com as juntas
            var patio = Patio;
            o.Laje(patio.xMin, patio.xMax, patio.yMin, patio.yMax, 0f, 0.3f, "Concreto", true);
            for (float x = patio.xMin + 3f; x < patio.xMax; x += 3f)
                o.Caixa(new Vector3(x, 0.002f, (patio.yMin + patio.yMax) / 2), new Vector3(0.03f, 0.004f, patio.height), "Calcada");
            // a cerca de alambrado em volta do pátio (os mourões, os arames e a farpa em cima), com o vão da estrada
            void Cerca(Vector2 a, Vector2 b)
            {
                float L = (b - a).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(L / 2.5f));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    o.Caixa(new Vector3(p.x, 1.2f, p.y), new Vector3(0.08f, 2.4f, 0.08f), "Metal", colide: true);
                }
                foreach (float y in new[] { 0.15f, 0.55f, 0.95f, 1.35f, 1.75f, 2.15f, 2.4f, 2.55f })
                    o.Tubo(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), y > 2.3f ? 0.012f : 0.008f, y > 2.3f ? "Ferrugem" : "Grade");
                o.CaixaEntre(new Vector3(a.x, 1.2f, a.y), new Vector3(b.x, 1.2f, b.y), 0.05f, 2.4f, "Concreto", colide: true, visual: false);
            }
            Cerca(new Vector2(patio.xMin, patio.yMin), new Vector2(VaoDaCercaX0, patio.yMin));
            Cerca(new Vector2(VaoDaCercaX1, patio.yMin), new Vector2(patio.xMax, patio.yMin));
            Cerca(new Vector2(patio.xMin, patio.yMin), new Vector2(patio.xMin, Z0 - pe + 1f));
            Cerca(new Vector2(patio.xMax, patio.yMin), new Vector2(patio.xMax, Z0 - pe + 1f));
        }
    }
}
