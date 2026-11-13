using System.Collections.Generic;
using UnityEngine;
using Andar = ZombieCityCleaner.Mundo.PlantaDoHospital.Andar;
using Porta = ZombieCityCleaner.Mundo.PlantaDoHospital.Porta;
using Divisa = ZombieCityCleaner.Mundo.PlantaDoHospital.Divisa;
using Borda = ZombieCityCleaner.Mundo.PlantaDoHospital.Borda;
using TipoDePorta = ZombieCityCleaner.Mundo.PlantaDoHospital.TipoDePorta;
using Zona = ZombieCityCleaner.Mundo.PlantaDoHospital.Zona;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O lado de dentro da loja de armas (o Plano_LojaDeArmas.md): as portas de dentro, os acabamentos de cada cômodo (o
    /// rodapé, o lambri e o friso na loja, na antessala e no escritório; o azulejo do banheiro; a chapa de aço e os painéis
    /// acústicos do estande; o concreto grosso da sala segura), o forro rebaixado com a grade, as luminárias apagadas e as
    /// placas que caíram, o estande (a faixa da linha de tiro, os trilhos e os motores dos carros dos alvos, os defletores
    /// do teto, os números das baias, as marcas das jardas, o anteparo do fundo, a luz de cessar-fogo e o duto) e as placas em
    /// inglês.
    /// </summary>
    public sealed partial class LojaDeArmas
    {
        /// <summary>A altura do forro rebaixado (as placas de 0,6 × 1,2 m; em cima, o vão até a laje).</summary>
        public const float Forro = 3.5f;

        /// <summary>Os cômodos com o forro rebaixado; nos outros, a laje à mostra.</summary>
        static bool ComForro(string tipo) => tipo is "vendas" or "escritorio" or "antessala" or "banheiro";

        // as portas de dentro já feitas (para as placas em cima delas): a parede, o vão, a porta e os cômodos dos lados n e -n
        readonly List<(Parede w, Abertura ab, Porta p, Comodo ladoN, Comodo outro)> _feitas = new List<(Parede, Abertura, Porta, Comodo, Comodo)>();

        // ------------------------------------------------------------ as portas de dentro

        void PortaDeDentro(Parede w, Abertura ab, Porta p, int dentro)
        {
            Moldura(w, ab, p.tipo == TipoDePorta.Madeira ? "Batente" : "Metal");
            // abertas, entre 80° e 100° (mais fechadas, fechavam a passagem); a do estande e a do depósito um pouco menos
            float abertura = p.tipo == TipoDePorta.Reforcada ? Faixa(82f, 92f) : Faixa(80f, 100f);
            Folha(w, ab.ini, ab.larg, ab.alto, dentro, abertura, Chance(0.5f), p.tipo);
            var pts = w.Ponto(ab.ini + ab.larg / 2f, 1f, 0.3f);
            var k1 = an.Em(pts.x, pts.z);
            var pts2 = w.Ponto(ab.ini + ab.larg / 2f, 1f, -0.3f);
            var k2 = an.Em(pts2.x, pts2.z);
            _feitas.Add((w, ab, p, k1, k2));
        }

        // o vão da porta de cofre: o batente é do modelo (a PortaDoCofre); aqui, só a soleira de aço
        void MolduraDoCofre(Parede w, Abertura ab)
        {
            Placa(w, ab.ini, ab.ini + ab.larg, 0f, 0.012f, 0f, m.esp + 0.3f, "Metal");
            _feitas.Add((w, ab, null, null, null));
        }

        // ------------------------------------------------------------ os acabamentos

        /// <summary>
        /// Os acabamentos nas faces de dentro das paredes de cada cômodo (as de fora e as de dentro), nos pedaços sólidos de
        /// cada uma (as faixas param nas portas e nas janelas), cortados no comprimento do cômodo.
        /// </summary>
        void Acabamentos()
        {
            // as paredes de fora não são desta parte (são da casca): o desenho delas vem do mesmo kit, com os mesmos vãos
            var deFora = new List<Parede>();
            foreach (var b in _bordas)
            {
                VaosDaBorda(b, out float lo, out float hi);
                bool sobe = b.vertical ? b.fora > 0 : b.fora < 0;
                Vector2 a = Ponta(b, sobe ? lo : hi), c = Ponta(b, sobe ? hi : lo);
                var w = ParedeSoDeConta(a, c, LadoDaBorda(b) == 0 ? AltoDaFachada : TopoDaCobertura + Platibanda, sobe, lo, hi);
                deFora.Add(w);
            }
            foreach (var w in deFora) Revestir(w, -1);
            foreach (var w in paredes)
            {
                if (w.nome != "dentro") continue;
                Revestir(w, 1);
                Revestir(w, -1);
            }
        }

        // uma parede de fora só para as contas (os pedaços sólidos, com as janelas e as portas de fora), sem desenho nem colisão
        Parede ParedeSoDeConta(Vector2 a, Vector2 c, float altura, bool sobe, float lo, float hi)
        {
            Vector2 d = c - a;
            float L = d.magnitude;
            Vector2 u = d / L;
            var w = new Parede { a = a, u = u, n = new Vector2(u.y, -u.x), yaw = -Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg, L = L, ext = true, nome = "conta", baseY = 0f, altura = altura };
            var abs = new List<Abertura>();
            foreach (var x in _vaos) abs.Add(new Abertura(sobe ? x.de - lo : hi - x.ate, x.ate - x.de, x.baixo, x.alto));
            abs.Sort((p, q) => p.ini.CompareTo(q.ini));
            float s = 0f;
            void Pedaco(float s0, float s1, float y0, float y1)
            {
                if (s1 - s0 > 0.01f && y1 - y0 > 0.01f) w.pecas.Add(new Vector4(s0, s1, y0, y1));
            }
            foreach (var ab in abs)
            {
                Pedaco(s, ab.ini, 0f, altura);
                Pedaco(ab.ini, ab.ini + ab.larg, 0f, ab.baixo);
                Pedaco(ab.ini, ab.ini + ab.larg, ab.alto, altura);
                s = ab.ini + ab.larg;
            }
            Pedaco(s, L, 0f, altura);
            w.aberturas.AddRange(abs);
            return w;
        }

        void Revestir(Parede w, int lado)
        {
            var q = w.Ponto(w.L / 2f, 1f, lado * 0.35f);
            var k = an.Em(q.x, q.z);
            if (k == null) return;
            // o trecho do cômodo ao longo da parede (sem a meia parede das pontas)
            float s0 = float.MaxValue, s1 = float.MinValue;
            foreach (var p in new[] { new Vector2(k.x0, k.z0), new Vector2(k.x1, k.z0), new Vector2(k.x0, k.z1), new Vector2(k.x1, k.z1) })
            {
                float s = Vector2.Dot(p - w.a, w.u);
                s0 = Mathf.Min(s0, s);
                s1 = Mathf.Max(s1, s);
            }
            s0 = Mathf.Max(s0 + m.esp / 2f, 0f);
            s1 = Mathf.Min(s1 - m.esp / 2f, w.L);
            if (s1 - s0 < 0.1f) return;
            float face = m.esp / 2f;
            void Tira(float y0, float y1, float esp, string cor, float modulo = 0f)
            {
                foreach (var pc in w.pecas)
                {
                    float a = Mathf.Max(pc.x, s0), b = Mathf.Min(pc.y, s1), ya = Mathf.Max(pc.z, y0), yb = Mathf.Min(pc.w, y1);
                    if (b - a < 0.04f || yb - ya < 0.04f) continue;
                    if (modulo <= 0f) { Placa(w, a, b, ya, yb, lado * (face + esp / 2f), esp, cor); continue; }
                    // em placas (os painéis acústicos): cortadas a cada 'modulo' contado do começo do cômodo, com a fresta
                    for (float m0 = s0 + Mathf.Floor((a - s0) / modulo) * modulo; m0 < b; m0 += modulo)
                    {
                        float pa = Mathf.Max(a, m0 + 0.015f), pb = Mathf.Min(b, m0 + modulo - 0.015f);
                        if (pb - pa > 0.05f) Placa(w, pa, pb, ya + 0.015f, yb - 0.015f, lado * (face + esp / 2f), esp, cor);
                    }
                }
            }
            switch (k.tipo)
            {
                case "vendas":
                case "antessala":
                case "escritorio":
                    Tira(0f, 0.12f, 0.018f, "Rodape");
                    Tira(0.12f, k.tipo == "escritorio" ? 0.9f : 1.05f, 0.012f, "Madeira");
                    Tira(k.tipo == "escritorio" ? 0.9f : 1.05f, k.tipo == "escritorio" ? 0.95f : 1.11f, 0.03f, "MadeiraVelha");
                    break;
                case "banheiro":
                    Tira(0f, 1.6f, 0.012f, "Azulejo");
                    Tira(1.6f, 1.66f, 0.018f, "ParedeVerdeEscura");
                    break;
                case "estande":
                    Tira(0f, 0.55f, 0.02f, "Metal");
                    Tira(0.6f, 3.3f, 0.05f, "Interior", 1.2f);
                    break;
                case "segura":
                    // o concreto grosso da sala segura (15 cm a mais por dentro, até a laje)
                    Tira(0f, PeDireito, 0.15f, "Concreto");
                    break;
                default:
                    Tira(0f, 0.1f, 0.015f, "Concreto");
                    break;
            }
        }

        // ------------------------------------------------------------ o forro e as luminárias

        void OForro()
        {
            foreach (var k in an.comodos)
            {
                float x0 = k.x0 + 0.1f, x1 = k.x1 - 0.1f, z0 = k.z0 + 0.1f, z1 = k.z1 - 0.1f;
                if (!ComForro(k.tipo))
                {
                    // a laje à mostra: as calhas penduradas (apagadas), uma a cada uns 5 m
                    if (k.tipo == "estande") continue;
                    for (float x = x0 + 2.2f; x < x1 - 1f; x += 4.5f)
                        for (float z = z0 + 2f; z < z1 - 1f; z += 4.5f)
                        {
                            Caixa(new Vector3(x, PeDireito - 0.3f, z), new Vector3(0.24f, 0.07f, 1.3f), "Metal");
                            Caixa(new Vector3(x, PeDireito - 0.345f, z), new Vector3(0.14f, 0.02f, 1.2f), "Farol");
                            Tubo(new Vector3(x, PeDireito, z - 0.5f), new Vector3(x, PeDireito - 0.27f, z - 0.5f), 0.006f, "Grade");
                            Tubo(new Vector3(x, PeDireito, z + 0.5f), new Vector3(x, PeDireito - 0.27f, z + 0.5f), 0.006f, "Grade");
                        }
                    continue;
                }
                // a grade do forro (0,6 m em x, 1,2 m em z, a partir da quina do cômodo), as placas que caíram e as luminárias
                var caidas = new List<Rect>();
                int nCaidas = k.tipo == "vendas" ? 6 : k.tipo == "banheiro" ? 1 : 2;
                int ix = Mathf.FloorToInt((x1 - x0) / 0.6f), iz = Mathf.FloorToInt((z1 - z0) / 1.2f);
                for (int i = 0; i < nCaidas * 3 && caidas.Count < nCaidas; i++)
                {
                    int a = rnd.Next(1, Mathf.Max(2, ix - 1)), b = rnd.Next(1, Mathf.Max(2, iz - 1));
                    if (a % 4 == 2 && b % 3 == 1) continue;   // não onde vai uma luminária
                    var r = Rect.MinMaxRect(x0 + a * 0.6f + 0.01f, z0 + b * 1.2f + 0.01f, x0 + (a + 1) * 0.6f - 0.01f, z0 + (b + 1) * 1.2f - 0.01f);
                    if (r.xMax > x1 || r.yMax > z1) continue;
                    caidas.Add(r);
                }
                Laje(x0, x1, z0, z1, Forro + 0.02f, 0.02f, "Teto", false, caidas);
                for (float x = x0 + 0.6f; x < x1 - 0.05f; x += 0.6f) Caixa(new Vector3(x, Forro - 0.008f, (z0 + z1) / 2f), new Vector3(0.025f, 0.016f, z1 - z0), "Metal");
                for (float z = z0 + 1.2f; z < z1 - 0.05f; z += 1.2f) Caixa(new Vector3((x0 + x1) / 2f, Forro - 0.008f, z), new Vector3(x1 - x0, 0.016f, 0.025f), "Metal");
                for (int a = 2; a < ix - 1; a += 4)
                    for (int b = 1; b < iz - 1; b += 3)
                    {
                        var c = new Vector3(x0 + (a + 0.5f) * 0.6f, Forro - 0.01f, z0 + (b + 0.5f) * 1.2f);
                        Caixa(c, new Vector3(0.56f, 0.03f, 1.16f), "Metal");
                        Caixa(c - new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.008f, 1.04f), "Farol");
                    }
                // as placas caídas no chão (inteiras ou quebradas) e uma pendurada pela grade
                for (int i = 0; i < caidas.Count; i++)
                {
                    var r = caidas[i];
                    if (i == 0 && caidas.Count > 2)
                    {
                        CaixaM(T(new Vector3(r.center.x, Forro - 0.35f, r.yMin + 0.1f)) * RX(-68f), new Vector3(0.58f, 0.02f, 1.18f), "Teto");
                        continue;
                    }
                    var p = new Vector3(Mathf.Clamp(r.center.x + Faixa(-0.4f, 0.4f), x0 + 0.5f, x1 - 0.5f), 0.02f, Mathf.Clamp(r.center.y + Faixa(-0.5f, 0.5f), z0 + 0.7f, z1 - 0.7f));
                    if (Chance(0.5f)) Caixa(p, new Vector3(0.58f, 0.02f, 1.18f), "Teto", Faixa(0f, 180f), RX(Faixa(-4f, 4f)));
                    else
                    {
                        Caixa(p, new Vector3(0.58f, 0.02f, 0.55f), "Teto", Faixa(0f, 180f));
                        Caixa(p + new Vector3(0.3f, 0.01f, 0.4f), new Vector3(0.5f, 0.02f, 0.5f), "Teto", Faixa(0f, 180f), RX(6f));
                    }
                }
            }
        }

        // ------------------------------------------------------------ o estande

        void OEstande()
        {
            var r = Estande;
            float x0 = r.xMin + m.esp / 2f, x1 = r.xMax - m.esp / 2f, z0 = r.yMin + m.esp / 2f, z1 = r.yMax - m.esp / 2f;
            float L = x1 - x0;
            // a faixa amarela da linha de tiro (do lado do alvo das bancadas) e a do fim da área de espera
            Caixa(new Vector3((x0 + x1) / 2f, 0.014f, LinhaDeTiro + 0.42f), new Vector3(L, 0.006f, 0.12f), "Faixa");
            Caixa(new Vector3((x0 + x1) / 2f, 0.014f, LinhaDeTiro - 1.45f), new Vector3(L, 0.006f, 0.06f), "Faixa");
            // os trilhos dos carros dos alvos (um por baia, no teto, da linha ao anteparo), os motores e os tirantes
            for (int i = 0; i < Baias; i++)
            {
                float x = MeioDaBaia(i);
                float za = LinhaDeTiro - 0.3f, zb = ComecoDoAnteparo - 0.25f;
                Caixa(new Vector3(x, PeDireito - 0.03f, (za + zb) / 2f), new Vector3(0.07f, 0.06f, zb - za), "Metal");
                Caixa(new Vector3(x, PeDireito - 0.12f, za + 0.2f), new Vector3(0.22f, 0.16f, 0.34f), "Grade");
                // o número da baia: a placa branca pendurada em cima da bancada
                var c = new Vector3(x, 2.85f, LinhaDeTiro - 0.25f);
                Tubo(c + Vector3.up * 0.15f, new Vector3(x, PeDireito, c.z), 0.006f, "Grade");
                PlacaComTexto(c, Vector3.back, 0.36f, 0.3f, "PlacaBranca", (i + 1).ToString(), 0.2f, "Pneu");
                PlacaComTexto(c + new Vector3(0f, 0f, 0.021f), Vector3.forward, 0.36f, 0.3f, "PlacaBranca", (i + 1).ToString(), 0.2f, "Pneu");
            }
            // os defletores do teto: as placas inclinadas de compensado com a chapa por trás, de parede a parede
            for (float z = LinhaDeTiro + 2.4f; z < ComecoDoAnteparo - 1.2f; z += 3.3f)
            {
                var c = new Vector3((x0 + x1) / 2f, PeDireito - 0.42f, z);
                Caixa(c, new Vector3(L, 0.04f, 0.95f), "MadeiraVelha", 0f, RX(-32f));
                Caixa(c + new Vector3(0f, 0.04f, 0.02f), new Vector3(L, 0.012f, 0.95f), "Metal", 0f, RX(-32f));
                foreach (float x in new[] { x0 + 0.6f, (x0 + x1) / 2f, x1 - 0.6f })
                    Tubo(new Vector3(x, PeDireito, z - 0.3f), new Vector3(x, PeDireito - 0.2f, z - 0.3f), 0.008f, "Grade");
            }
            // as marcas das jardas nas paredes dos lados (5, 10 e 15 jardas da linha)
            foreach (var (jardas, z) in new[] { (5, LinhaDeTiro + 4.57f), (10, LinhaDeTiro + 9.14f), (15, LinhaDeTiro + 13.72f) })
            {
                if (z > ComecoDoAnteparo - 0.5f) continue;
                PlacaComTexto(new Vector3(x0 + 0.105f, 2.1f, z), Vector3.right, 0.7f, 0.3f, "PlacaAmarela", $"{jardas} YD", 0.17f, "Pneu");
                PlacaComTexto(new Vector3(x1 - 0.105f, 2.1f, z), Vector3.left, 0.7f, 0.3f, "PlacaAmarela", $"{jardas} YD", 0.17f, "Pneu");
            }
            // o anteparo do fundo: a saia de aço, a rampa de baixo e a chapa de cima convergindo para a câmara (a caixa
            // escura), a chapa até o teto, as faixas de aviso e a colisão (não se entra nele)
            float za0 = ComecoDoAnteparo, zf = z1 - 0.6f;
            Caixa(new Vector3((x0 + x1) / 2f, 0.225f, za0 + 0.02f), new Vector3(L, 0.45f, 0.04f), "Ferrugem");
            Caixa(new Vector3((x0 + x1) / 2f, 0.38f, za0 - 0.002f), new Vector3(L, 0.08f, 0.006f), "Faixa");
            CaixaEntre(new Vector3((x0 + x1) / 2f, 0.45f, za0 + 0.04f), new Vector3((x0 + x1) / 2f, 1.55f, zf), L, 0.04f, "Ferrugem");
            CaixaEntre(new Vector3((x0 + x1) / 2f, 3.45f, za0 + 0.04f), new Vector3((x0 + x1) / 2f, 1.95f, zf), L, 0.04f, "Ferrugem");
            Caixa(new Vector3((x0 + x1) / 2f, 3.62f, za0 + 0.02f), new Vector3(L, 0.36f, 0.04f), "Metal");
            Caixa(new Vector3((x0 + x1) / 2f, 1.75f, (zf + z1) / 2f), new Vector3(L, 0.9f, z1 - zf), "Pneu");
            for (int i = 0; i <= Baias; i++)
            {
                float x = MeioDaBaia(0) - LarguraDaBaia / 2f + i * LarguraDaBaia;
                CaixaEntre(new Vector3(x, 0.45f, za0 + 0.04f), new Vector3(x, 1.55f, zf), 0.06f, 0.08f, "Metal");
            }
            // a passagem da parede oeste até os corredores: o portãozinho de grade aberto (encostado na baia 1) e a placa
            float pa = ComecoDaPassagem, pb = FimDaPassagem;
            Caixa(new Vector3(pb - 0.03f, 0.55f, LinhaDeTiro - 0.02f - (pb - pa) * 0.42f), new Vector3(0.04f, 1.0f, (pb - pa) * 0.84f), "Grade");
            Caixa(new Vector3(pb - 0.03f, 1.0f, LinhaDeTiro - 0.02f - (pb - pa) * 0.42f), new Vector3(0.05f, 0.05f, (pb - pa) * 0.86f), "Faixa");
            Caixa(new Vector3(pa + 0.05f, 0.6f, LinhaDeTiro), new Vector3(0.08f, 1.2f, 0.08f), "Metal");
            PlacaComTexto(new Vector3(pa + 0.105f - 0.1f + 0.002f + 0.05f, 1.75f, LinhaDeTiro + 0.6f), Vector3.right, 0.8f, 0.24f, "PlacaAmarela", "STAFF ONLY", 0.1f, "Pneu");
            Caixa(new Vector3((x0 + x1) / 2f, 0.8f, (za0 + z1) / 2f), new Vector3(L, 1.6f, z1 - za0), "Concreto", colide: true, visual: false);
            // a luz de cessar-fogo (apagada) na parede do leste, perto da linha, e o duto da ventilação no alto do oeste
            Caixa(new Vector3(x1 - 0.06f, 2.6f, LinhaDeTiro - 0.6f), new Vector3(0.1f, 0.22f, 0.22f), "Metal");
            Caixa(new Vector3(x1 - 0.115f, 2.6f, LinhaDeTiro - 0.6f), new Vector3(0.012f, 0.16f, 0.16f), "Lanterna");
            PlacaComTexto(new Vector3(x1 - 0.105f, 2.3f, LinhaDeTiro - 0.6f), Vector3.left, 0.9f, 0.18f, "PlacaVermelha", "CEASE FIRE", 0.1f, "PlacaBranca");
            Caixa(new Vector3(x0 + 0.35f, PeDireito - 0.3f, (z0 + za0) / 2f), new Vector3(0.5f, 0.4f, za0 - z0 - 0.4f), "Metal");
            for (float z = z0 + 2f; z < za0 - 1f; z += 4f) Caixa(new Vector3(x0 + 0.6f, PeDireito - 0.3f, z), new Vector3(0.01f, 0.28f, 0.5f), "Grade");
            // a laje à mostra: duas calhas apagadas na área de espera
            foreach (float x in new[] { x0 + 2f, x1 - 2f })
            {
                Caixa(new Vector3(x, PeDireito - 0.05f, z0 + 1.1f), new Vector3(1.3f, 0.07f, 0.24f), "Metal");
                Caixa(new Vector3(x, PeDireito - 0.095f, z0 + 1.1f), new Vector3(1.2f, 0.02f, 0.14f), "Farol");
            }
        }

        // ------------------------------------------------------------ as placas de dentro

        /// <summary>Uma placa de parede de dentro: na parede w, no lado 'lado', o meio em 's' e na altura 'y'.</summary>
        void PlacaNaParede(Parede w, int lado, float s, float y, float largura, float altura, string fundo, string texto, float h, string letra)
        {
            var fora = new Vector3(w.n.x, 0f, w.n.y) * lado;
            PlacaComTexto(w.Ponto(s, y, lado * (m.esp / 2f + 0.022f)), fora, largura, altura, fundo, texto, h, letra);
        }

        void PlacasDeDentro()
        {
            // em cima de cada porta, do lado de quem chega (a área de vendas, a antessala, o estoque), o nome do cômodo do outro lado
            foreach (var (w, ab, p, kn, ko) in _feitas)
            {
                float s = ab.ini + ab.larg / 2f, y = ab.alto + 0.24f;
                if (p == null)
                {
                    // a porta de cofre: o aviso amarelo do lado do estoque (o lado n da parede, para +x)
                    PlacaNaParede(w, 1, s + ab.larg / 2f + 0.6f, 1.55f, 0.75f, 0.42f, "PlacaAmarela", "AUTHORIZED", 0.09f, "Pneu");
                    PlacaNaParede(w, 1, s + ab.larg / 2f + 0.6f, 1.43f, 0.75f, 0.001f, "PlacaAmarela", "PERSONNEL ONLY", 0.08f, "Pneu");
                    continue;
                }
                foreach (var (k, lado) in new[] { (kn, -1), (ko, 1) })
                {
                    // a placa vai do lado do outro cômodo (o que se vê ao chegar)
                    if (k == null || !an.placas.TryGetValue(k, out var nome)) continue;
                    var outro = lado == -1 ? ko : kn;
                    if (outro == null || outro.tipo == "estande") continue;
                    float larg = Mathf.Max(0.9f, Letras.Largura(nome, 0.11f) + 0.2f);
                    PlacaNaParede(w, lado, s, y, larg, 0.24f, k.tipo == "segura" ? "PlacaAmarela" : "PlacaVerde", nome, 0.11f, k.tipo == "segura" ? "Pneu" : "PlacaBranca");
                }
                // em cima da porta do estande, do lado da antessala, o aviso dos abafadores
                if (p.tipo == TipoDePorta.Reforcada)
                {
                    int lado = kn != null && kn.tipo == "antessala" ? 1 : -1;
                    PlacaNaParede(w, lado, s, y + 0.02f, 2.5f, 0.3f, "PlacaAmarela", "EYE & EAR PROTECTION REQUIRED", 0.11f, "Pneu");
                    PlacaNaParede(w, -lado, s, y, 0.7f, 0.22f, "PlacaBranca", "EXIT", 0.13f, "PlacaVermelha");
                }
            }
            float e = m.esp / 2f + 0.022f;
            // a área de vendas: as seções em cima dos expositores, o EXIT em cima da entrada e o aviso da verificação
            PlacaComTexto(new Vector3(X(3.6f), 3.1f, Z(22f) - e), Vector3.back, 3.2f, 0.36f, "PlacaVermelha", "FIREARMS", 0.2f, "PlacaBranca");
            PlacaComTexto(new Vector3(X(0f) + e, 3.1f, Z(12.3f)), Vector3.right, 3.2f, 0.36f, "PlacaVermelha", "RIFLES & SHOTGUNS", 0.17f, "PlacaBranca");
            PlacaComTexto(new Vector3(X(0f) + e, 3.1f, Z(3.1f)), Vector3.right, 3.0f, 0.36f, "Laranja", "AMMUNITION", 0.2f, "Pneu");
            PlacaComTexto(new Vector3(X(15.6f) - e, 3.1f, Z(12.2f)), Vector3.left, 3.2f, 0.36f, "PlacaVermelha", "ACCESSORIES", 0.2f, "PlacaBranca");
            PlacaComTexto(new Vector3(MeioDaEntrada, 3.12f, Z0 + e), Vector3.forward, 0.7f, 0.24f, "PlacaBranca", "EXIT", 0.14f, "PlacaVermelha");
            PlacaComTexto(new Vector3(X(10.6f), 2.95f, Z(22f) - e), Vector3.back, 2.3f, 0.5f, "PlacaBranca", "ID REQUIRED", 0.13f, "Pneu");
            PlacaComTexto(new Vector3(X(10.6f), 2.79f, Z(22f) - e - 0.003f), Vector3.back, 2.3f, 0.001f, "PlacaBranca", "FOR ALL PURCHASES", 0.08f, "Pneu");
            // a antessala: o quadro das regras do estande (o título e as linhas)
            var regras = new Vector3(X(20.0f), 1.75f, Z(7f) - e);
            PlacaComTexto(regras, Vector3.back, 0.95f, 1.25f, "PlacaBranca", "", 0.1f, "Pneu");
            Letreiro("RANGE RULES", regras + new Vector3(0f, 0.42f, 0f) + Vector3.back * 0.003f, Vector3.right, Vector3.back, 0.09f, "PlacaVermelha", plano: true, sai: 0.002f);
            for (int i = 0; i < 9; i++)
            {
                float w = Faixa(0.45f, 0.75f);
                Caixa(regras + new Vector3(-0.38f + w / 2f, 0.27f - i * 0.1f, -0.003f), new Vector3(w, 0.022f, 0.002f), "Pneu");
            }
            // o estande: o aviso do tiro rápido na área de espera
            PlacaComTexto(new Vector3(X(15.6f) + e, 2.0f, Z(8.2f)), Vector3.right, 1.5f, 0.3f, "PlacaVermelha", "NO RAPID FIRE", 0.13f, "PlacaBranca");
        }
    }
}
