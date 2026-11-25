using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O lixo da cidade (23/11/2026, a pedido: "refazer as latas de lixo, acho que estão muito simples, precisa dar alguns
    /// detalhes a mais e uma melhorada nos gráficos, parece que não se encaixa no jogo"). Antes eram os cilindros e os cubos
    /// da Unity, de cor lisa; agora são malhas da <see cref="MalhaDeObra"/> com as cores da <see cref="Paleta"/>, para o
    /// material da cidade (o abandono por cima: o plástico desbota, escorre e racha; o metal enferruja):
    ///  - <b>o carrinho</b> de plástico das casas (o de rodas dos EUA): o corpo mais largo em cima, a borda grossa, as
    ///    nervuras dos lados, o painel em relevo e a etiqueta do número da casa na frente, a barra do caminhão, o pé, a caixa
    ///    do eixo com as rodas atrás, a alça e a tampa na dobradiça (fechada, entreaberta com a sacola saindo ou aberta para
    ///    trás);
    ///  - <b>a lata de metal</b> (a galvanizada, em umas casas): o aro do fundo, os anéis, as alças dos lados e a tampa de
    ///    cone com a alça em arco (no lugar ou encostada na lata, com a sacola saindo);
    ///  - <b>a sacola</b>: o saco cheio e torto, o pescoço torcido e as pontas do nó (ou rasgada e murcha);
    ///  - <b>a caçamba</b> das lojas: o corpo com a frente baixa, a borda, as duas tampas de plástico na dobradiça (fechadas,
    ///    uma entreaberta ou uma virada para trás), os bolsos do garfo, os reforços, os patins, a placa da firma e o aviso;
    ///  - <b>os restos</b> do lixo revirado pelo guaxinim: os papéis, as latas, a garrafa, a caixa de pizza e a comida.
    /// Cada variante é feita uma vez e guardada (uma malha para todas as iguais). As medidas em metros, com a origem no chão
    /// e a frente (o lado da rua, onde a tampa abre) em -z.
    /// </summary>
    public static class ModelosDoLixo
    {
        public static readonly string[] CoresDoCarrinho = { "PlasticoVerde", "PlasticoPreto", "PlasticoAzul", "PlasticoCinza" };
        public static readonly string[] CoresDaSacola = { "Saco", "Saco", "SacoVerde", "SacoBranco" };
        public static readonly string[] CoresDaCacamba = { "CarroVerde", "Cacamba", "PlacaVerde" };
        public const int FormasDaSacola = 6, VariantesDosRestos = 6;

        /// <summary>O carrinho: a largura (x), o fundo (z) e a altura com a tampa (m).</summary>
        public static readonly Vector3 TamanhoDoCarrinho = new Vector3(0.66f, 1.05f, 0.8f);
        /// <summary>A lata de metal: o raio e a altura com a tampa (m).</summary>
        public const float RaioDaLata = 0.28f, AlturaDaLata = 0.83f;
        /// <summary>A caçamba: com os bolsos do garfo (x), a altura atrás (y) e o fundo (z) (m).</summary>
        public static readonly Vector3 TamanhoDaCacamba = new Vector3(2.14f, 1.3f, 1.16f);

        /// <summary>Qual é a lata de uma casa: o carrinho (a cor) ou a lata de metal, e a tampa (0 fechada, 1 entreaberta, 2 aberta).</summary>
        public struct Lata
        {
            public bool carrinho;
            public int cor, tampa;
        }

        /// <summary>A lata de uma casa pela semente do ponto (a mesma em todas as máquinas, e a mesma caída no lixo revirado).</summary>
        public static Lata QualLata(int semente)
        {
            var r = new System.Random(semente ^ 0x2c1b3c6d);
            var l = new Lata { carrinho = r.NextDouble() < 0.8 };
            double c = r.NextDouble();
            l.cor = c < 0.38 ? 0 : c < 0.68 ? 1 : c < 0.88 ? 2 : 3;
            double t = r.NextDouble();
            l.tampa = t < 0.68 ? 0 : t < 0.94 ? 1 : 2;
            return l;
        }

        /// <summary>A malha da lata de pé.</summary>
        public static Mesh DaLata(Lata l) => l.carrinho ? Carrinho(l.cor, l.tampa) : LataDeMetal(l.tampa == 0 ? 0 : 1);

        static readonly Dictionary<int, Mesh> _malhas = new Dictionary<int, Mesh>();

        static Mesh Guardada(int chave, string nome, System.Action<MalhaDeObra> fazer)
        {
            if (_malhas.TryGetValue(chave, out var m) && m != null) return m;
            var o = new MalhaDeObra(2048);
            fazer(o);
            m = o.ParaMesh(nome);
            _malhas[chave] = m;
            return m;
        }

        /// <summary>O carrinho de plástico na cor (<see cref="CoresDoCarrinho"/>) e com a tampa (0 fechada, 1 entreaberta com a sacola, 2 aberta para trás).</summary>
        public static Mesh Carrinho(int cor, int tampa)
        {
            cor = Mathf.Abs(cor) % CoresDoCarrinho.Length;
            tampa = Mathf.Clamp(tampa, 0, 2);
            return Guardada(1000 + cor * 10 + tampa, $"Lixo: o carrinho ({CoresDoCarrinho[cor]}, tampa {tampa})", m => FazerCarrinho(m, CoresDoCarrinho[cor], tampa));
        }

        /// <summary>A lata de metal: 0 com a tampa, 1 com a tampa encostada e a sacola saindo, 2 sem a tampa (a caída).</summary>
        public static Mesh LataDeMetal(int variante)
        {
            variante = Mathf.Clamp(variante, 0, 2);
            return Guardada(2000 + variante, $"Lixo: a lata de metal ({variante})", m => FazerLataDeMetal(m, variante));
        }

        /// <summary>A tampa da lata de metal sozinha (a do lixo revirado), com a origem no meio do aro.</summary>
        public static Mesh TampaDaLata() => Guardada(2100, "Lixo: a tampa da lata", m => FazerTampa(m, Matrix4x4.identity));

        /// <summary>Uma sacola de lixo (a forma de 0 a <see cref="FormasDaSacola"/>, a cor de <see cref="CoresDaSacola"/>), cheia ou rasgada.</summary>
        public static Mesh Sacola(int forma, int cor, bool rasgada = false)
        {
            forma = Mathf.Abs(forma) % FormasDaSacola;
            cor = Mathf.Abs(cor) % CoresDaSacola.Length;
            return Guardada(3000 + forma * 20 + cor * 2 + (rasgada ? 1 : 0), $"Lixo: a sacola ({forma}, {CoresDaSacola[cor]}{(rasgada ? ", rasgada" : "")})",
                m => FazerSacola(m, Matrix4x4.identity, forma, CoresDaSacola[cor], rasgada));
        }

        /// <summary>A caçamba na cor (<see cref="CoresDaCacamba"/>) e com as tampas (0 fechadas, 1 uma entreaberta, 2 uma virada para trás).</summary>
        public static Mesh Cacamba(int cor, int tampa)
        {
            cor = Mathf.Abs(cor) % CoresDaCacamba.Length;
            tampa = Mathf.Clamp(tampa, 0, 2);
            return Guardada(4000 + cor * 10 + tampa, $"Lixo: a caçamba ({CoresDaCacamba[cor]}, tampas {tampa})", m => FazerCacamba(m, CoresDaCacamba[cor], tampa));
        }

        /// <summary>Os restos espalhados do lixo revirado (até uns 1,7 m do meio).</summary>
        public static Mesh Restos(int variante)
        {
            variante = Mathf.Abs(variante) % VariantesDosRestos;
            return Guardada(5000 + variante, $"Lixo: os restos ({variante})", m => FazerRestos(m, variante));
        }

        // ------------------------------------------------------------------ o carrinho

        // o corpo: embaixo (Y0) e em cima (Y1), a meia largura e o meio fundo
        const float W0 = 0.25f, D0 = 0.29f, W1 = 0.30f, D1 = 0.355f, Y0 = 0.04f, Y1 = 0.95f;
        static float MeiaLargura(float y) => Mathf.Lerp(W0, W1, (y - Y0) / (Y1 - Y0));
        static float MeioFundo(float y) => Mathf.Lerp(D0, D1, (y - Y0) / (Y1 - Y0));

        // um retângulo de cantos chanfrados (8 pontos) na altura y
        static List<Vector3> Anel(float hx, float hz, float ch, float y, float cz = 0f) => new List<Vector3>
        {
            new Vector3(-hx + ch, y, cz - hz), new Vector3(hx - ch, y, cz - hz), new Vector3(hx, y, cz - hz + ch), new Vector3(hx, y, cz + hz - ch),
            new Vector3(hx - ch, y, cz + hz), new Vector3(-hx + ch, y, cz + hz), new Vector3(-hx, y, cz + hz - ch), new Vector3(-hx, y, cz - hz + ch),
        };

        static Matrix4x4 Em(float x, float y, float z) => Matrix4x4.Translate(new Vector3(x, y, z));
        static Matrix4x4 Em(Vector3 p, Quaternion q) => Matrix4x4.TRS(p, q, Vector3.one);

        static void FazerCarrinho(MalhaDeObra m, string cor, int tampa)
        {
            // o corpo, mais largo em cima, e a borda grossa
            m.Solido(Anel(W0, D0, 0.06f, Y0), Anel(W1, D1, 0.07f, Y1), cor);
            m.Solido(Anel(W1 + 0.015f, D1 + 0.015f, 0.075f, 0.925f), Anel(W1 + 0.015f, D1 + 0.015f, 0.075f, 0.985f), cor);
            // o fundo escuro da boca (aparece com a tampa aberta)
            m.Face(Anel(W1 - 0.03f, D1 - 0.03f, 0.05f, 0.987f), Vector3.up, "Pneu");
            // as nervuras dos lados, seguindo o corpo
            foreach (int s in new[] { -1, 1 })
                foreach (float z in new[] { -0.13f, 0.13f })
                {
                    List<Vector3> Nervura(float y)
                    {
                        float x = MeiaLargura(y);
                        return new List<Vector3>
                        {
                            new Vector3(s * (x - 0.005f), y, z - 0.025f), new Vector3(s * (x + 0.018f), y, z - 0.025f),
                            new Vector3(s * (x + 0.018f), y, z + 0.025f), new Vector3(s * (x - 0.005f), y, z + 0.025f),
                        };
                    }
                    m.Solido(Nervura(0.12f), Nervura(0.88f), cor);
                }
            // na frente: o painel em relevo, a etiqueta do número da casa, a barra do caminhão e o pé
            float inc = Mathf.Atan2(D1 - D0, Y1 - Y0) * Mathf.Rad2Deg;
            var frente = Quaternion.Euler(-inc, 0f, 0f);
            m.Caixa(Em(new Vector3(0f, 0.5f, -MeioFundo(0.5f) - 0.002f), frente), new Vector3(0.32f, 0.4f, 0.024f), cor);
            m.Caixa(Em(new Vector3(0.1f, 0.77f, -MeioFundo(0.77f) - 0.004f), frente), new Vector3(0.12f, 0.07f, 0.012f), "ListraBranca");
            float yb = 0.87f, zb = -MeioFundo(yb) - 0.035f;
            m.Tubo(new Vector3(-0.2f, yb, zb), new Vector3(0.2f, yb, zb), 0.014f, 6, "Metal");
            foreach (float x in new[] { -0.17f, 0.17f })
                m.Caixa(Em(x, yb, zb + 0.022f), new Vector3(0.03f, 0.04f, 0.05f), cor);
            m.Caixa(Em(0f, 0.025f, -D0 + 0.05f), new Vector3(0.34f, 0.05f, 0.1f), cor);
            // atrás: a caixa do eixo, o eixo e as rodas (o pneu e o miolo)
            float yr = 0.125f, zr = D0 + 0.03f, rr = 0.125f;
            m.Caixa(Em(0f, 0.17f, D0 - 0.02f), new Vector3(0.4f, 0.17f, 0.12f), cor);
            m.Tubo(new Vector3(-0.3f, yr, zr), new Vector3(0.3f, yr, zr), 0.016f, 6, "Metal");
            var deLado = Quaternion.Euler(0f, 0f, -90f);   // (o eixo do cilindro em +x)
            foreach (float xi in new[] { -0.3f, 0.225f })
            {
                m.Cilindro(Em(new Vector3(xi, yr, zr), deLado), rr, rr, 0.075f, 12, "Pneu");
                m.Cilindro(Em(new Vector3(xi - 0.006f, yr, zr), deLado), 0.06f, 0.06f, 0.087f, 8, "PlasticoCinza");
            }
            // a alça de trás, na altura da borda, e as dobradiças da tampa
            float yh = 0.955f, zh = D1 + 0.085f;
            m.Tubo(new Vector3(-0.25f, yh, zh), new Vector3(0.25f, yh, zh), 0.022f, 8, cor);
            foreach (float x in new[] { -0.24f, 0.24f })
                m.Caixa(Em(x, yh, D1 + 0.045f), new Vector3(0.05f, 0.05f, 0.08f), cor);
            float zd = D1 + 0.02f;
            m.Tubo(new Vector3(-0.29f, 0.99f, zd), new Vector3(-0.17f, 0.99f, zd), 0.018f, 6, cor);
            m.Tubo(new Vector3(0.17f, 0.99f, zd), new Vector3(0.29f, 0.99f, zd), 0.018f, 6, cor);
            // a tampa, na dobradiça: a placa, o meio mais alto e a aba da frente (onde se pega)
            float ang = tampa == 1 ? 16f : tampa == 2 ? 108f : 0f;
            var L = Matrix4x4.TRS(new Vector3(0f, 0.985f, zd), Quaternion.Euler(ang, 0f, 0f), Vector3.one);
            float comp = 2f * D1 + 0.06f;
            m.Caixa(L * Em(0f, 0.0175f, -comp * 0.5f), new Vector3(0.65f, 0.035f, comp), cor);
            m.Caixa(L * Em(0f, 0.045f, -comp * 0.5f - 0.02f), new Vector3(0.52f, 0.022f, comp - 0.16f), cor);
            m.Caixa(L * Em(0f, -0.012f, -comp + 0.015f), new Vector3(0.44f, 0.06f, 0.03f), cor);
            // a sacola saindo pela boca
            if (tampa >= 1)
            {
                var r = new System.Random(11 + tampa);
                Saco(m, Em(new Vector3(0.03f, 0.78f, -0.08f), Quaternion.Euler(0f, 25f, 0f)), 0.27f, 0.31f, "Saco", r, false);
                if (tampa == 2) Saco(m, Em(new Vector3(-0.1f, 0.86f, 0.1f), Quaternion.Euler(0f, 70f, 0f)), 0.17f, 0.26f, "SacoBranco", r, true);
            }
        }

        // as pontas do nó de uma sacola: as duas orelhas, para cima e para fora
        static void Pontas(MalhaDeObra m, Vector3 topo, Quaternion q, float s, string cor)
        {
            foreach (int lado in new[] { -1, 1 })
                m.Esfera(Matrix4x4.TRS(topo + q * new Vector3(lado * 0.022f, 0.045f, 0f) * s, q * Quaternion.Euler(0f, 0f, -lado * 32f), new Vector3(0.045f, 0.11f, 0.022f) * s), 0.5f, cor);
        }

        /// <summary>
        /// O saco (23/11/2026): anéis tortos de 9 lados, do fundo achatado à boca franzida (o plástico em pregas, sem a
        /// cara de pedra da esfera), com o pescoço torcido e as pontas do nó se 'amarrado'. 'M' leva o fundo do saco;
        /// 'raio' e 'alto' em metros.
        /// </summary>
        static void Saco(MalhaDeObra m, Matrix4x4 M, float raio, float alto, string cor, System.Random r, bool amarrado)
        {
            // (a altura e o raio de cada anel, em frações)
            float[] ys = { 0f, 0.1f, 0.34f, 0.66f, 0.88f, 1f };
            float[] rs = { 0.72f, 0.96f, 1f, 0.9f, 0.56f, 0.2f };
            const int lados = 9;
            var aneis = new List<List<Vector3>>();
            var meios = new List<Vector3>();
            float tortoX = 0f, tortoZ = 0f, giro0 = (float)r.NextDouble() * 6.283f;
            for (int k = 0; k < ys.Length; k++)
            {
                if (k > 1) { tortoX += ((float)r.NextDouble() - 0.5f) * 0.05f * raio * 4f; tortoZ += ((float)r.NextDouble() - 0.5f) * 0.05f * raio * 4f; }
                var c = new Vector3(tortoX, ys[k] * alto, tortoZ);
                var anel = new List<Vector3>(lados);
                float g = giro0 + k * 0.35f;
                for (int i = 0; i < lados; i++)
                {
                    float a = g + i * Mathf.PI * 2f / lados;
                    float rr = raio * rs[k] * (k == ys.Length - 1 ? 1f : 0.88f + 0.24f * (float)r.NextDouble());
                    float dy = k == 0 || k == ys.Length - 1 ? 0f : ((float)r.NextDouble() - 0.5f) * 0.06f * alto;
                    anel.Add(M.MultiplyPoint3x4(c + new Vector3(Mathf.Cos(a) * rr, dy, Mathf.Sin(a) * rr * 0.9f)));
                }
                aneis.Add(anel);
                meios.Add(M.MultiplyPoint3x4(c));
            }
            var quad = new List<Vector3>(4);
            for (int k = 0; k + 1 < aneis.Count; k++)
                for (int i = 0; i < lados; i++)
                {
                    int j = (i + 1) % lados;
                    quad.Clear();
                    quad.Add(aneis[k][i]); quad.Add(aneis[k][j]); quad.Add(aneis[k + 1][j]); quad.Add(aneis[k + 1][i]);
                    var centro = (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
                    m.Face(quad, centro - (meios[k] + meios[k + 1]) * 0.5f, cor);
                }
            m.Face(aneis[0], M.MultiplyVector(Vector3.down), cor);
            m.Face(aneis[aneis.Count - 1], M.MultiplyVector(Vector3.up), cor);
            if (!amarrado) return;
            // o pescoço torcido e as pontas do nó
            var inc = Quaternion.Euler((float)r.NextDouble() * 24f - 12f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 24f - 12f);
            var topoDoSaco = new Vector3(tortoX, alto * 0.97f, tortoZ);
            var N = M * Em(topoDoSaco, inc);
            float e = raio / 0.22f;
            m.Cilindro(N, 0.05f * e, 0.022f * e, 0.07f * e, 6, cor);
            Pontas(m, N.MultiplyPoint3x4(new Vector3(0f, 0.065f * e, 0f)), M.rotation * inc, e, cor);
        }

        // ------------------------------------------------------------------ a lata de metal

        static void FazerLataDeMetal(MalhaDeObra m, int variante)
        {
            const string chapa = "Cromo", ferro = "Metal";
            m.Cilindro(Matrix4x4.identity, 0.245f, 0.25f, 0.03f, 16, ferro);
            m.Cilindro(Em(0f, 0.02f, 0f), 0.25f, 0.27f, 0.65f, 16, chapa);
            // os anéis da chapa
            foreach (float y in new[] { 0.17f, 0.37f, 0.55f })
            {
                float r = Mathf.Lerp(0.25f, 0.27f, (y - 0.02f) / 0.65f) + 0.008f;
                m.Cilindro(Em(0f, y, 0f), r, r, 0.035f, 16, chapa);
            }
            m.Cilindro(Em(0f, 0.655f, 0f), 0.28f, 0.28f, 0.03f, 16, ferro);
            // as alças dos lados
            foreach (int s in new[] { -1, 1 })
            {
                m.Caixa(Em(s * 0.272f, 0.56f, 0f), new Vector3(0.02f, 0.07f, 0.16f), ferro);
                m.Tubo(new Vector3(s * 0.3f, 0.53f, -0.07f), new Vector3(s * 0.3f, 0.53f, 0.07f), 0.011f, 6, ferro);
                foreach (float z in new[] { -0.07f, 0.07f })
                    m.Tubo(new Vector3(s * 0.278f, 0.56f, z), new Vector3(s * 0.3f, 0.53f, z), 0.01f, 5, ferro);
            }
            if (variante == 0) FazerTampa(m, Em(0f, 0.685f, 0f));
            else if (variante == 1)
            {
                // a tampa encostada na lata, no chão, e a sacola saindo
                FazerTampa(m, Em(new Vector3(0.36f, 0.29f, 0.05f), Quaternion.Euler(0f, 0f, -75f)));
                var r = new System.Random(41);
                Saco(m, Em(new Vector3(0f, 0.55f, 0f), Quaternion.Euler(0f, 40f, 0f)), 0.25f, 0.32f, "Saco", r, true);
            }
            else m.Face(Anel(0.24f, 0.24f, 0.07f, 0.686f), Vector3.up, "Pneu");   // (a boca escura, sem a tampa)
        }

        // a tampa de cone: o aro, o cone, o topo e a alça em arco, com a origem no meio do aro
        static void FazerTampa(MalhaDeObra m, Matrix4x4 T)
        {
            m.Cilindro(T, 0.295f, 0.295f, 0.025f, 16, "Metal");
            m.Cilindro(T * Em(0f, 0.025f, 0f), 0.29f, 0.11f, 0.065f, 16, "Cromo");
            m.Cilindro(T * Em(0f, 0.09f, 0f), 0.11f, 0.11f, 0.008f, 12, "Cromo");
            Vector3 P(float x, float y) => T.MultiplyPoint3x4(new Vector3(x, y, 0f));
            m.Tubo(P(-0.075f, 0.095f), P(-0.06f, 0.14f), 0.011f, 6, "Metal");
            m.Tubo(P(-0.06f, 0.14f), P(0.06f, 0.14f), 0.011f, 6, "Metal");
            m.Tubo(P(0.06f, 0.14f), P(0.075f, 0.095f), 0.011f, 6, "Metal");
        }

        // ------------------------------------------------------------------ a sacola

        static void FazerSacola(MalhaDeObra m, Matrix4x4 M, int forma, string cor, bool rasgada)
        {
            var r = new System.Random(977 + forma * 131);
            float e = 0.88f + 0.08f * (forma % 3);
            if (!rasgada)
            {
                Saco(m, M, 0.22f * e, 0.42f * e, cor, r, true);
                return;
            }
            // rasgada: murcha, sem o nó, com o rasgo e uns papéis saindo
            Saco(m, M, 0.25f * e, 0.17f * e, cor, r, false);
            m.Esfera(M * Matrix4x4.TRS(new Vector3(-0.2f * e, 0.05f, 0.02f), Quaternion.Euler(0f, 20f, 0f), new Vector3(0.14f, 0.05f, 0.11f) * e), 0.5f, "Pneu");
            for (int i = 0; i < 3; i++)
                m.Caixa(M * Em(new Vector3(-0.3f * e - i * 0.09f, 0.008f, (float)r.NextDouble() * 0.2f - 0.1f), Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f)),
                    new Vector3(0.1f, 0.006f, 0.08f), i % 2 == 0 ? "ListraBranca" : "Papelao");
        }

        // ------------------------------------------------------------------ a caçamba

        static void FazerCacamba(MalhaDeObra m, string cor, int tampa)
        {
            const float W = 1.86f, hw = W * 0.5f;
            // o corpo: o perfil (de lado) puxado em x, com a frente baixa e o pé da frente recolhido
            List<Vector3> Perfil(float x) => new List<Vector3>
            {
                new Vector3(x, 0.12f, 0.56f), new Vector3(x, 1.26f, 0.56f), new Vector3(x, 1.06f, -0.56f), new Vector3(x, 0.52f, -0.56f), new Vector3(x, 0.12f, -0.36f),
            };
            m.Solido(Perfil(-hw), Perfil(hw), cor);
            float inc = Mathf.Atan2(0.2f, 1.12f) * Mathf.Rad2Deg;
            var rampa = Quaternion.Euler(-inc, 0f, 0f);
            // a borda de cima e o fundo escuro da boca (aparece com a tampa aberta)
            m.Caixa(Em(0f, 1.06f, -0.575f), new Vector3(W + 0.04f, 0.07f, 0.05f), cor);
            m.Caixa(Em(0f, 1.26f, 0.575f), new Vector3(W + 0.04f, 0.07f, 0.05f), cor);
            foreach (int s in new[] { -1, 1 })
                m.Caixa(Em(new Vector3(s * (hw + 0.012f), 1.16f, 0f), rampa), new Vector3(0.035f, 0.07f, 1.19f), cor);
            m.Caixa(Em(new Vector3(0f, 1.16f, 0f), rampa), new Vector3(W - 0.1f, 0.01f, 1.05f), "Pneu");
            // as tampas de plástico na dobradiça: a placa, as nervuras e a aba da frente
            m.Tubo(new Vector3(-hw, 1.32f, 0.585f), new Vector3(hw, 1.32f, 0.585f), 0.02f, 6, "Metal");
            for (int k = 0; k < 2; k++)
            {
                float xm = (k == 0 ? -1f : 1f) * W * 0.25f;
                float alfa = tampa == 1 && k == 1 ? 24f : tampa == 2 && k == 0 ? 275f : 0f;
                var L = Matrix4x4.TRS(new Vector3(xm, 1.32f, 0.585f), Quaternion.Euler(-inc + alfa, 0f, 0f), Vector3.one);
                float lw = W * 0.5f - 0.02f, lc = 1.22f;
                m.Caixa(L * Em(0f, -0.005f, -lc * 0.5f), new Vector3(lw, 0.04f, lc), "PlasticoPreto");
                foreach (float zz in new[] { -0.35f, -0.85f })
                    m.Caixa(L * Em(0f, 0.025f, zz), new Vector3(lw - 0.06f, 0.02f, 0.05f), "PlasticoPreto");
                m.Caixa(L * Em(0f, -0.04f, -lc + 0.015f), new Vector3(lw, 0.07f, 0.03f), "PlasticoPreto");
            }
            // as sacolas aparecendo pela tampa aberta
            if (tampa >= 1)
            {
                var r = new System.Random(23 + tampa);
                float x = tampa == 1 ? 0.45f : -0.45f;
                Saco(m, Em(new Vector3(x, 0.95f, 0f), Quaternion.Euler(0f, 20f, 0f)), 0.32f, 0.33f, "Saco", r, true);
                if (tampa == 2)
                {
                    Saco(m, Em(new Vector3(x - 0.28f, 0.95f, 0.25f), Quaternion.Euler(0f, 70f, 0f)), 0.25f, 0.32f, "SacoVerde", r, true);
                    m.Caixa(Em(new Vector3(x + 0.22f, 1.2f, -0.3f), Quaternion.Euler(-30f, 10f, 0f)), new Vector3(0.36f, 0.3f, 0.02f), "Papelao");
                }
            }
            // os bolsos do garfo dos lados, com a boca escura
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * (hw + 0.07f);
                m.Caixa(Em(x, 0.8f, 0f), new Vector3(0.14f, 0.15f, 1f), "Metal");
                m.Caixa(Em(x, 0.8f, -0.502f), new Vector3(0.1f, 0.1f, 0.006f), "Pneu");
                m.Caixa(Em(x, 0.8f, 0.502f), new Vector3(0.1f, 0.1f, 0.006f), "Pneu");
            }
            // os reforços de pé na frente e atrás, e a faixa dos lados
            foreach (float x in new[] { -0.72f, 0f, 0.72f })
            {
                m.Caixa(Em(x, 0.79f, -0.575f), new Vector3(0.06f, 0.52f, 0.03f), cor);
                m.Caixa(Em(x, 0.69f, 0.575f), new Vector3(0.06f, 1.12f, 0.03f), cor);
            }
            foreach (int s in new[] { -1, 1 })
                m.Caixa(Em(s * (hw + 0.01f), 0.42f, 0.05f), new Vector3(0.02f, 0.06f, 0.9f), cor);
            // os patins
            foreach (float z in new[] { -0.26f, 0.38f })
                m.Caixa(Em(0f, 0.06f, z), new Vector3(W - 0.1f, 0.12f, 0.12f), "Metal");
            // a placa da firma e o aviso
            m.Caixa(Em(-0.33f, 0.88f, -0.567f), new Vector3(0.56f, 0.16f, 0.012f), "ListraBranca");
            m.Caixa(Em(0.36f, 0.88f, -0.567f), new Vector3(0.2f, 0.14f, 0.012f), "PlacaAmarela");
        }

        // ------------------------------------------------------------------ os restos do lixo revirado

        static void FazerRestos(MalhaDeObra m, int variante)
        {
            var r = new System.Random(5309 + variante * 71);
            float R() => (float)r.NextDouble();
            Vector2 Onde(float raio)
            {
                float a = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * raio;
                return new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
            }
            // os papéis
            string[] papeis = { "ListraBranca", "ListraBranca", "Papelao", "PapelVerde", "PapelRosa", "Palha" };
            for (int i = 0; i < 10; i++)
            {
                var o = Onde(1.7f);
                m.Caixa(Em(new Vector3(o.x, 0.005f + i * 0.0015f, o.y), Quaternion.Euler(R() * 6f - 3f, R() * 360f, R() * 6f - 3f)),
                    new Vector3(0.12f + R() * 0.14f, 0.006f, 0.09f + R() * 0.12f), papeis[r.Next(papeis.Length)]);
            }
            // as latas, deitadas
            string[] latas = { "Cromo", "PlacaVermelha", "CarroAzul", "PlacaVerde" };
            for (int i = 0; i < 3; i++)
            {
                var o = Onde(1.5f);
                m.Cilindro(Em(new Vector3(o.x, 0.033f, o.y), Quaternion.Euler(0f, R() * 360f, 90f)), 0.033f, 0.03f, 0.12f, 8, latas[r.Next(latas.Length)]);
            }
            // a garrafa
            {
                var o = Onde(1.4f);
                var G = Em(new Vector3(o.x, 0.04f, o.y), Quaternion.Euler(0f, R() * 360f, 90f));
                m.Cilindro(G, 0.04f, 0.04f, 0.17f, 8, "VidroSujo");
                m.Cilindro(G * Em(0f, 0.17f, 0f), 0.04f, 0.015f, 0.08f, 8, "VidroSujo");
            }
            // a caixa de pizza, com a tampa meio aberta
            {
                var o = Onde(1.3f);
                var q = Quaternion.Euler(0f, R() * 360f, 0f);
                var C = Em(new Vector3(o.x, 0f, o.y), q);
                m.Caixa(C * Em(0f, 0.02f, 0f), new Vector3(0.34f, 0.035f, 0.34f), "Papelao");
                m.Caixa(C * Em(new Vector3(0f, 0.19f, 0.09f), Quaternion.Euler(62f, 0f, 0f)), new Vector3(0.34f, 0.006f, 0.34f), "Papelao");
            }
            // os restos de comida
            string[] comida = { "FolhaMorta", "Laranja", "Palha", "Terra" };
            for (int i = 0; i < 5; i++)
            {
                var o = Onde(1.2f);
                m.Esfera(Matrix4x4.TRS(new Vector3(o.x, 0.015f, o.y), Quaternion.Euler(0f, R() * 360f, 0f), new Vector3(0.09f + R() * 0.06f, 0.04f, 0.07f + R() * 0.05f)),
                    0.5f, comida[r.Next(comida.Length)], 0.08f, r);
            }
        }
    }
}
