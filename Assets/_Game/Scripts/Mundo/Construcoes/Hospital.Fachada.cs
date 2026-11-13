using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A fachada do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3, depois das imagens de referência do
    /// usuário: o prédio menos quadrado, de tijolo e concreto): as paredes de fora no tijolo aparente (a cor
    /// "TijoloAparente": o shader do abandono desenha os tijolos e o rejunte), com o concreto por cima, em relevo: o rodapé,
    /// as faixas na linha de cada laje, as pilastras nas quinas e a cada 8 m, a cornija com o capeamento e as molduras das
    /// janelas (Hospital.Portas.cs); a torre da entrada, na frente do saguão, mais alta que o prédio, com as janelas em faixa
    /// e o nome e a cruz em cima; o pórtico (a marquise grossa nos pilares quadrados, com as luminárias); a ala da
    /// emergência (a faixa de concreto em cima da garagem, a placa e a cruz); e, na cobertura, as casas das escadas, a casa
    /// de máquinas, os aparelhos de ar e a antena com a luz vermelha; os condutores da chuva e as arandelas das saídas.
    /// O relevo não tem colisão (fica rente à parede); a torre, os pilares e o que fica na cobertura, têm.
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>O nome do hospital na torre da entrada, em três linhas.</summary>
        public static readonly string[] NomeNaTorre = { "BLACKWATER", "GENERAL", "HOSPITAL" };

        /// <summary>O nome na torre desta obra (null: o <see cref="NomeNaTorre"/>; o do Lakewood é o LAKEWOOD COUNTY HOSPITAL).</summary>
        public string[] nomeNaTorre;

        /// <summary>A torre da entrada: de -TorreX a TorreX, a frente dela saindo da fachada e o topo, acima da cobertura.</summary>
        public const float TorreX = 5f, TorreSai = 0.45f, AlturaDaTorre = 4.6f, FundoDaTorre = -12.5f;
        public static float FrenteDaTorre => Z0 - 0.1f - TorreSai;
        public static float TopoDaTorre => TopoDaCobertura + AlturaDaTorre;

        const string Concreto = "ConcretoClaro", Tijolo = "TijoloAparente";
        const float AltoDoRodape = 0.12f, BaixoDaCornija = 12.45f, TopoDaCornija = 13.12f;

        // a face de fora de um lado do corpo (0 a frente, -z; 1 o leste, +x; 2 o fundo, +z; 3 o oeste, -x): o ponto com 0 ao
        // longo dela, o sentido ao longo e o de fora
        static (Vector3 o, Vector3 u, Vector3 n) FaceDeFora(int lado) => lado switch
        {
            0 => (new Vector3(0f, 0f, Z0 - 0.1f), Vector3.right, Vector3.back),
            1 => (new Vector3(X1 + 0.1f, 0f, 0f), Vector3.forward, Vector3.right),
            2 => (new Vector3(0f, 0f, Z1 + 0.1f), Vector3.right, Vector3.forward),
            _ => (new Vector3(X0 - 0.1f, 0f, 0f), Vector3.forward, Vector3.left),
        };

        // uma caixa em relevo na face de fora do lado: de s0 a s1 ao longo dela, de y0 a y1, saindo 'sai'
        void NaFaceDeFora(int lado, float s0, float s1, float y0, float y1, float sai, Cor cor, bool colide = false)
        {
            var (o, u, n) = FaceDeFora(lado);
            Caixa(o + u * ((s0 + s1) / 2f) + Vector3.up * ((y0 + y1) / 2f) + n * (sai / 2f - 0.002f),
                  new Vector3(s1 - s0, y1 - y0, sai + 0.004f), cor, u == Vector3.right ? 0f : -90f, colide: colide);
        }

        // o comprimento de um lado (com a volta da quina: 'sai' a mais em cada ponta, para os relevos se encontrarem)
        static (float s0, float s1) Extensao(int lado, float sai) =>
            lado == 0 || lado == 2 ? (X0 - 0.1f - sai, X1 + 0.1f + sai) : (Z0 - 0.1f - sai, Z1 + 0.1f + sai);

        readonly List<Vector2> _livresNaFace = new List<Vector2>();

        // os trechos livres de um lado entre y0 e y1: sem a torre (na frente), a garagem (no oeste, até o alto dela), a
        // entrada (na frente, até o alto dela) e, embaixo, as portas de fora e os patamares delas
        List<Vector2> Livres(int lado, float y0, float y1, float sai)
        {
            var (a, b) = Extensao(lado, sai);
            _livresNaFace.Clear();
            _livresNaFace.Add(new Vector2(a, b));
            void Tirar(float de, float ate)
            {
                for (int i = _livresNaFace.Count - 1; i >= 0; i--)
                {
                    var v = _livresNaFace[i];
                    if (ate <= v.x || de >= v.y) continue;
                    _livresNaFace.RemoveAt(i);
                    if (de > v.x + 0.02f) _livresNaFace.Add(new Vector2(v.x, de));
                    if (ate < v.y - 0.02f) _livresNaFace.Add(new Vector2(ate, v.y));
                }
            }
            var g = GaragemDasAmbulancias;
            if (lado == 0)
            {
                if (y0 < TopoDaTorre) Tirar(-TorreX - 0.02f, TorreX + 0.02f);
                if (y0 < TopoDaEntrada + 0.25f) Tirar(Entrada.xMin - 0.1f, Entrada.xMax + 0.1f);
            }
            if (lado == 3 && y0 < TopoDaGaragem + 0.4f) Tirar(g.yMin - 0.1f - sai, g.yMax + 0.1f + sai);
            if (y0 < m.alturaPorta + 0.3f)
            {
                if (lado == 1) Tirar(-8.2f, -4.8f);
                if (lado == 3) Tirar(-8.2f, -5.15f);
                if (lado == 2) Tirar(5.4f, 10.6f);
            }
            _livresNaFace.Sort((p, q) => p.x.CompareTo(q.x));
            return _livresNaFace;
        }

        /// <summary>A fachada: os relevos de concreto do corpo, a torre, a ala da emergência, a cobertura e os detalhes.</summary>
        void Fachada()
        {
            for (int lado = 0; lado < 4; lado++)
            {
                // o rodapé de concreto, do chão de fora até 12 cm acima do térreo
                foreach (var v in Livres(lado, ChaoDeFora, AltoDoRodape, 0.07f))
                    NaFaceDeFora(lado, v.x, v.y, ChaoDeFora - 0.04f, AltoDoRodape, 0.07f, "Concreto");
                // as faixas na linha das lajes do 2º e do 3º andar
                for (int f = 2; f < Andares; f++)
                    foreach (var v in Livres(lado, Base(f) - GrossuraDaLaje - 0.02f, Base(f) + 0.05f, 0.09f))
                        NaFaceDeFora(lado, v.x, v.y, Base(f) - GrossuraDaLaje - 0.02f, Base(f) + 0.05f, 0.09f, Concreto);
                // a cornija (dois degraus) e o capeamento da platibanda
                foreach (var v in Livres(lado, BaixoDaCornija, TopoDaCornija, 0.16f))
                    NaFaceDeFora(lado, v.x, v.y, BaixoDaCornija, TopoDaCornija, 0.16f, Concreto);
                foreach (var v in Livres(lado, BaixoDaCornija - 0.2f, BaixoDaCornija, 0.08f))
                    NaFaceDeFora(lado, v.x, v.y, BaixoDaCornija - 0.2f, BaixoDaCornija, 0.08f, Concreto);
                foreach (var v in Livres(lado, TopoDaCornija, TopoDaCornija + 0.1f, 0.24f))
                    NaFaceDeFora(lado, v.x, v.y, TopoDaCornija, TopoDaCornija + 0.1f, 0.24f, Concreto);
                // o capeamento por cima da platibanda, até a face de dentro
                var (o, u, n) = FaceDeFora(lado);
                var (a, b) = Extensao(lado, 0.24f);
                Caixa(o + u * ((a + b) / 2f) + Vector3.up * (TopoDaCornija + 0.05f) - n * 0.12f, new Vector3(b - a, 0.1f, 0.26f), Concreto, u == Vector3.right ? 0f : -90f);
                // as pilastras: nas quinas e a cada 8 m (entre as janelas), do rodapé até a cornija
                var (e0, e1) = Extensao(lado, 0f);
                var posicoes = new List<(float s0, float s1)> { (e0 - 0.11f, e0 + 0.6f), (e1 - 0.6f, e1 + 0.11f) };
                float[] meio = lado == 0 ? new[] { -24f, -16f, -8f, 8f, 16f, 24f } : lado == 2 ? new[] { -24f, -16f, -8f, 0f, 16f, 24f } : new[] { -8f, 0f, 8f };
                foreach (float s in meio) posicoes.Add((s - 0.3f, s + 0.3f));
                foreach (var (s0, s1) in posicoes)
                {
                    // começa acima do que estiver embaixo dela (a garagem, uma porta), cortada pelos trechos livres
                    float y0 = AltoDoRodape;
                    foreach (float yt in new[] { AltoDoRodape, m.alturaPorta + 0.35f, TopoDaGaragem + 0.45f, TopoDaTorre })
                    {
                        bool livre = false;
                        foreach (var v in Livres(lado, yt, yt + 0.1f, 0.11f))
                            if (v.x <= s0 + 0.01f && v.y >= s1 - 0.01f) livre = true;
                        if (livre) { y0 = yt; break; }
                        y0 = float.MaxValue;
                    }
                    if (y0 < BaixoDaCornija - 0.2f) NaFaceDeFora(lado, s0, s1, y0, BaixoDaCornija - 0.2f, 0.11f, Concreto);
                }
            }
            TorreDaEntrada();
            AlaDaEmergencia();
            NoTelhado();
            Condutores();
            Arandelas();
        }

        // ------------------------------------------------------------ a torre da entrada

        /// <summary>
        /// A torre da entrada, na frente do saguão (de concreto, saindo 45 cm da fachada e subindo 4,6 m acima da cobertura): os
        /// pilares dos lados da entrada, as faixas de janela do 2º e do 3º andar (por elas se veem as janelas do prédio, 45 cm
        /// para dentro), o corpo em cima da cobertura, o capeamento, o nome em três linhas e a cruz vermelha.
        /// </summary>
        void TorreDaEntrada()
        {
            float zf = FrenteDaTorre, zt = Z0 - 0.1f, topo = TopoDaTorre, xe = Entrada.xMax + 0.1f;
            void Bloco(float x0, float x1, float y0, float y1, float z0, float z1) =>
                Caixa(new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, y1 - y0, z1 - z0), Concreto, colide: true);
            const float faixa = 3.0f;
            foreach (float s in new[] { -1f, 1f })
            {
                // o pilar de fora (até o chão) e o de dentro (de cima da entrada para cima)
                float xa = s * TorreX, xb = s * xe, xc = s * faixa;
                Bloco(Mathf.Min(xa, xb), Mathf.Max(xa, xb), ChaoDeFora - 0.05f, topo, zf, zt);
                Bloco(Mathf.Min(xb, xc), Mathf.Max(xb, xc), TopoDaEntrada, topo, zf, zt);
            }
            // no meio, entre as faixas de janela do 2º e do 3º andar
            float j2 = Base(2) + Peitoril, t2 = Base(2) + TopoDaJanela, j3 = Base(3) + Peitoril, t3 = Base(3) + TopoDaJanela;
            Bloco(-faixa, faixa, TopoDaEntrada, j2, zf, zt);
            Bloco(-faixa, faixa, t2, j3, zf, zt);
            Bloco(-faixa, faixa, t3, topo, zf, zt);
            // o peitoril de cada faixa, saindo da torre
            foreach (float y in new[] { j2, j3 })
                Caixa(new Vector3(0f, y - 0.05f, zf - 0.05f), new Vector3(2f * faixa + 0.2f, 0.1f, 0.12f), Concreto);
            // o corpo da torre em cima da cobertura e o capeamento
            Bloco(-TorreX, TorreX, TopoDaCobertura, topo, zt - 0.01f, FundoDaTorre);
            Caixa(new Vector3(0f, topo + 0.12f, (zf + FundoDaTorre) / 2f), new Vector3(2f * TorreX + 0.4f, 0.24f, FundoDaTorre - zf + 0.4f), Concreto);
            // as juntas das placas de concreto na frente (os frisos)
            foreach (float y in new[] { Base(3) - 0.3f, TopoDaCobertura - 0.2f })
                Caixa(new Vector3(0f, y, zf - 0.004f), new Vector3(2f * TorreX - 0.02f, 0.025f, 0.01f), "Concreto");
            // o nome e a cruz, em relevo
            float yn = TopoDaCobertura + 2.2f;
            var nome = nomeNaTorre ?? NomeNaTorre;
            for (int i = 0; i < nome.Length; i++)
                Letreiro(nome[i], new Vector3(0f, yn - i * 0.9f, zf), Vector3.right, Vector3.back, 0.62f, "Bunker", sai: 0.05f, grossura: 0.08f);
            float yc = topo - 0.95f;
            Caixa(new Vector3(0f, yc, zf - 0.05f), new Vector3(1.2f, 0.38f, 0.1f), "PlacaVermelha");
            Caixa(new Vector3(0f, yc, zf - 0.05f), new Vector3(0.38f, 1.2f, 0.1f), "PlacaVermelha");
        }

        // ------------------------------------------------------------ a ala da emergência

        // a garagem das ambulâncias: o rodapé, a faixa de concreto em cima (com o capeamento), a placa branca do EMERGENCY
        // (o texto em LetreirosDeFora) e a cruz vermelha em cima dela
        void AlaDaEmergencia()
        {
            var g = GaragemDasAmbulancias;
            float xo = g.xMin - 0.1f, zs = g.yMin - 0.1f, zn = g.yMax + 0.1f, xc = X0 - 0.1f;
            float y0 = TopoDaGaragem - 0.25f, y1 = TopoDaGaragem + 0.3f;
            // a faixa: no oeste (a frente dos portões), no sul e no norte
            Caixa(new Vector3(xo - 0.06f, (y0 + y1) / 2f, g.center.y), new Vector3(0.12f, y1 - y0, zn - zs + 0.24f), Concreto);
            foreach (float z in new[] { zs - 0.06f, zn + 0.06f })
            {
                Caixa(new Vector3((xo + xc) / 2f, (y0 + y1) / 2f, z), new Vector3(xc - xo + 0.12f, y1 - y0, 0.12f), Concreto);
                Caixa(new Vector3((xo + xc) / 2f, (ChaoDeFora + AltoDoRodape) / 2f - 0.02f, z), new Vector3(xc - xo, AltoDoRodape - ChaoDeFora, 0.12f), "Concreto");
            }
            Caixa(new Vector3((xo + xc) / 2f - 0.05f, y1 + 0.05f, g.center.y), new Vector3(xc - xo + 0.3f, 0.1f, zn - zs + 0.36f), Concreto);
            float yc = TopoDaGaragem;
            Caixa(new Vector3(xo - 0.14f, yc, g.center.y), new Vector3(0.05f, 0.42f, 0.13f), "PlacaVermelha");
            Caixa(new Vector3(xo - 0.14f, yc, g.center.y), new Vector3(0.05f, 0.13f, 0.42f), "PlacaVermelha");
        }

        // ------------------------------------------------------------ a cobertura

        /// <summary>
        /// Na cobertura: as casas das escadas dos cantos do fundo (de tijolo, com a porta de metal e o capeamento), a casa de
        /// máquinas do elevador (de tijolo), os aparelhos de ar (com as hélices e um duto), e a antena com a luz vermelha.
        /// </summary>
        void NoTelhado()
        {
            float y = TopoDaCobertura;
            void Casa(Rect r, float alto, int portaLado)
            {
                Caixa(new Vector3(r.center.x, y + alto / 2f, r.center.y), new Vector3(r.width, alto, r.height), Tijolo, colide: true);
                Caixa(new Vector3(r.center.x, y + alto + 0.08f, r.center.y), new Vector3(r.width + 0.3f, 0.16f, r.height + 0.3f), Concreto);
                Caixa(new Vector3(r.center.x, y + alto - 0.25f, r.center.y), new Vector3(r.width + 0.12f, 0.12f, r.height + 0.12f), Concreto);
                if (portaLado != 0)
                    Caixa(new Vector3(portaLado > 0 ? r.xMax + 0.02f : r.xMin - 0.02f, y + 1.05f, r.center.y), new Vector3(0.06f, 2.1f, 1.0f), "Metal");
            }
            var eo = EscadaOeste;
            Casa(Rect.MinMaxRect(eo.xMin - 0.1f, eo.yMin - 0.1f, eo.xMax + 0.1f, eo.yMax + 0.1f), 2.9f, 1);
            var el = EscadaLeste;
            Casa(Rect.MinMaxRect(el.xMin - 0.1f, el.yMin - 0.1f, el.xMax + 0.1f, el.yMax + 0.1f), 2.9f, -1);
            var ev = Elevador;
            Casa(Rect.MinMaxRect(ev.xMin - 0.6f, ev.yMin - 0.6f, ev.xMax + 0.6f, ev.yMax + 0.6f), 3.2f, 0);
            // os aparelhos de ar
            foreach (var (p, giro) in new[] { (new Vector2(-14f, 4f), 0f), (new Vector2(-10f, 4f), 0f), (new Vector2(12f, -6f), 90f),
                                              (new Vector2(-18f, -8f), 0f), (new Vector2(16f, 6f), 0f), (new Vector2(9f, 10f), 90f) })
            {
                Caixa(new Vector3(p.x, y + 0.1f, p.y), new Vector3(2.5f, 0.2f, 1.9f), "Concreto", giro);
                Caixa(new Vector3(p.x, y + 0.95f, p.y), new Vector3(2.2f, 1.5f, 1.6f), "Metal", giro, colide: true);
                Caixa(new Vector3(p.x, y + 0.95f, p.y), new Vector3(2.24f, 0.08f, 1.64f), "Grade", giro);
                Cilindro(new Vector3(p.x, y + 1.7f, p.y), 0.55f, 0.55f, 0.1f, 12, "Grade");
                Cilindro(new Vector3(p.x, y + 1.72f, p.y), 0.12f, 0.12f, 0.1f, 6, "Metal");
            }
            Caixa(new Vector3(-12f, y + 0.45f, -0.5f), new Vector3(0.5f, 0.5f, 7.6f), "Metal");
            // a antena, com a luz vermelha em cima (apagada: não há energia)
            var a = new Vector3(20f, y, 11f);
            Caixa(a + Vector3.up * 0.1f, new Vector3(0.8f, 0.2f, 0.8f), "Concreto");
            Cilindro(a, 0.06f, 0.035f, 6.2f, 6, "Metal");
            foreach (float h in new[] { 2f, 3.6f, 5f })
                Caixa(a + Vector3.up * h, new Vector3(0.9f - h * 0.08f, 0.04f, 0.04f), "Metal");
            vis.Esfera(T(a + Vector3.up * 6.3f), 0.1f, "Lanterna");
        }

        // os condutores da chuva: do alto (embaixo da cornija) até o chão, com a curva no pé
        void Condutores()
        {
            foreach (var (lado, s) in new[] { (2, -20f), (2, -4f), (2, 20f), (1, 12f), (3, -12f), (1, -12f), (0, -20f), (0, 20f) })
            {
                var (o, u, n) = FaceDeFora(lado);
                var p = o + u * s + n * 0.18f;
                Cilindro(new Vector3(p.x, ChaoDeFora + 0.25f, p.z), 0.06f, 0.06f, BaixoDaCornija - ChaoDeFora - 0.25f, 6, "Metal");
                Tubo(new Vector3(p.x, ChaoDeFora + 0.3f, p.z), new Vector3(p.x, ChaoDeFora + 0.1f, p.z) + n * 0.25f, 0.06f, "Metal");
                foreach (float y in new[] { 2f, 6f, 10f })
                    Caixa(new Vector3(p.x, y, p.z) - n * 0.09f, new Vector3(0.16f, 0.04f, 0.16f), "Metal");
            }
        }

        // as arandelas em cima das portas de fora (a do leste, a do oeste e a de serviço) e a cobertura da saída de serviço
        void Arandelas()
        {
            float y = m.alturaPorta + 0.35f;
            foreach (var (lado, s) in new[] { (1, -6.5f), (3, -6.65f), (2, 8f) })
            {
                var (o, u, n) = FaceDeFora(lado);
                var p = o + u * s + Vector3.up * y;
                Caixa(p + n * 0.09f, lado == 2 ? new Vector3(0.36f, 0.16f, 0.18f) : new Vector3(0.18f, 0.16f, 0.36f), "Metal");
                Caixa(p + n * 0.09f - Vector3.up * 0.085f, lado == 2 ? new Vector3(0.28f, 0.01f, 0.12f) : new Vector3(0.12f, 0.01f, 0.28f), "Farol");
            }
            Caixa(new Vector3(8f, m.alturaPorta + 0.62f, Z1 + 0.1f + 0.55f), new Vector3(3.2f, 0.16f, 1.1f), Concreto);
        }
    }
}
