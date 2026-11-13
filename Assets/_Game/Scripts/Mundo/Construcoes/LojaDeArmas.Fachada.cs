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
    /// A casca da loja de armas por fora (o Plano_LojaDeArmas.md): as portas de fora (a entrada de vidro com a grade de
    /// enrolar arrombada, a dos fundos e a saída de emergência do estande), as janelas (o caixilho, o vidro sujo inteiro,
    /// quebrado ou sem vidro, as tábuas e as grades das vitrines), a fachada (as pilastras de tijolo, a cornija, a faixa das
    /// letras, o letreiro com o fuzil em cima, o frontão, as luminárias pescoço de ganso, a bandeira e o cervo pintados), o
    /// telhado (o capeamento, os aparelhos de ar, os respiros e o alçapão) e os lados e os fundos (as faixas vermelhas, os
    /// condutores, os degraus, o relógio de luz e o cartaz do alvo).
    /// </summary>
    public sealed partial class LojaDeArmas
    {
        /// <summary>A cor das letras dos letreiros de fora (o creme desbotado) e a do fundo da faixa.</summary>
        const string Creme = "Palha", FundoDaFaixa = "ParedeAzulEscura";

        // ------------------------------------------------------------ as folhas das portas

        /// <summary>
        /// Uma folha de porta: fechada (abertura 0), no meio do vão; aberta, presa na dobradiça e girada para o lado 'dentro'
        /// (+1 = o lado n da parede). O tipo dá o desenho: a de madeira (a maçaneta), a de metal (a barra antipânico e o chute),
        /// a reforçada (escura, o visor e a faixa amarela) e a de vidro (o caixilho de alumínio, o vidro sujo ou só os cacos).
        /// </summary>
        void Folha(Parede w, float ini, float larg, float alto, int dentro, float abertura, bool dobradicaNoInicio, TipoDePorta tipo,
                   bool quebrada = false)
        {
            float L = larg - 0.04f, h = alto - 0.03f;
            Matrix4x4 M;
            if (abertura <= 0.01f)
            {
                Vector3 c = w.Ponto(ini + larg / 2f, 0.01f + h / 2f, 0f);
                M = T(c) * RY(w.yaw);
            }
            else
            {
                var (dob, ponta) = PontasDaFolha(w, ini, larg, dentro, abertura, dobradicaNoInicio);
                Vector2 d = (ponta - dob).normalized;
                Vector2 meio = dob + d * (L / 2f);
                M = T(new Vector3(meio.x, w.baseY + 0.01f + h / 2f, meio.y)) * RY(-Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                // a folha bem aberta ocupa o chão (os pontos dos móveis desviam)
                var area = Rect.MinMaxRect(Mathf.Min(dob.x, ponta.x) - 0.08f, Mathf.Min(dob.y, ponta.y) - 0.08f, Mathf.Max(dob.x, ponta.x) + 0.08f, Mathf.Max(dob.y, ponta.y) + 0.08f);
                ocupado.Add((area, w.baseY));
                if (abertura >= 60f) solidos.Add((area, w.baseY));
            }
            // o lado da maçaneta (a ponta da folha, longe da dobradiça), no espaço da folha
            float mx = (abertura <= 0.01f ? (dobradicaNoInicio ? 1f : -1f) : 1f) * (L / 2f - 0.09f);
            switch (tipo)
            {
                case TipoDePorta.Vidro:
                    {
                        const float e = 0.07f;
                        CaixaM(M * T(new Vector3(0f, h / 2f - e / 2f, 0f)), new Vector3(L, e, 0.05f), "Metal", colide: true);
                        CaixaM(M * T(new Vector3(0f, -h / 2f + 0.09f, 0f)), new Vector3(L, 0.18f, 0.05f), "Metal", colide: true);
                        CaixaM(M * T(new Vector3(-L / 2f + e / 2f, 0f, 0f)), new Vector3(e, h, 0.05f), "Metal", colide: true);
                        CaixaM(M * T(new Vector3(L / 2f - e / 2f, 0f, 0f)), new Vector3(e, h, 0.05f), "Metal", colide: true);
                        if (!quebrada)
                        {
                            CaixaM(M * T(new Vector3(0f, 0.045f, 0f)), new Vector3(L - 2 * e, h - e - 0.18f, 0.012f), "VidroSujo", colide: true);
                            CaixaM(M * T(new Vector3(-L * 0.18f, 0.3f, -0.008f)) * RZ(32f), new Vector3(0.06f, h * 0.5f, 0.002f), "Batente");
                        }
                        else
                            // os cacos que ficaram no caixilho
                            for (int i = 0; i < 5; i++)
                            {
                                float x = Faixa(-L / 2f + e + 0.08f, L / 2f - e - 0.08f);
                                bool emCima = Chance(0.5f);
                                float y = emCima ? h / 2f - e - 0.07f : -h / 2f + 0.18f + 0.07f;
                                CaixaM(M * T(new Vector3(x, y, 0f)) * RZ(Faixa(-30f, 30f)), new Vector3(Faixa(0.06f, 0.16f), 0.14f, 0.01f), "VidroSujo");
                            }
                        // a barra de empurrar dos dois lados
                        foreach (float s in new[] { -1f, 1f })
                            CaixaM(M * T(new Vector3(0f, -0.05f, s * 0.05f)), new Vector3(L - 0.25f, 0.04f, 0.03f), "Cromo");
                        break;
                    }
                case TipoDePorta.Reforcada:
                    CaixaM(M, new Vector3(L, h, 0.06f), "Grade", colide: true);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        CaixaM(M * T(new Vector3(0f, 0.35f, s * 0.031f)), new Vector3(0.24f, 0.36f, 0.006f), "Vidro");
                        CaixaM(M * T(new Vector3(0f, 0.35f, s * 0.033f)), new Vector3(0.30f, 0.42f, 0.004f), "Metal");
                        CaixaM(M * T(new Vector3(0f, -h / 2f + 0.15f, s * 0.031f)), new Vector3(L - 0.06f, 0.08f, 0.006f), "Faixa");
                        CaixaM(M * T(new Vector3(mx, -0.12f, s * 0.05f)), new Vector3(0.18f, 0.03f, 0.04f), "Cromo");
                    }
                    break;
                case TipoDePorta.Metal:
                    CaixaM(M, new Vector3(L, h, 0.05f), "Metal", colide: true);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        CaixaM(M * T(new Vector3(0f, -h / 2f + 0.12f, s * 0.027f)), new Vector3(L - 0.04f, 0.22f, 0.004f), "Cromo");
                        CaixaM(M * T(new Vector3(mx, -0.12f, s * 0.045f)), new Vector3(0.16f, 0.03f, 0.04f), "Cromo");
                    }
                    break;
                default:
                    CaixaM(M, new Vector3(L, h, 0.045f), "Madeira", colide: true);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        CaixaM(M * T(new Vector3(0f, h * 0.22f, s * 0.025f)), new Vector3(L - 0.26f, h * 0.36f, 0.008f), "MadeiraVelha");
                        CaixaM(M * T(new Vector3(0f, -h * 0.22f, s * 0.025f)), new Vector3(L - 0.26f, h * 0.36f, 0.008f), "MadeiraVelha");
                        CaixaM(M * T(new Vector3(mx, -0.12f, s * 0.045f)), new Vector3(0.05f, 0.05f, 0.06f), "Cromo");
                    }
                    break;
            }
        }

        /// <summary>A moldura de uma porta ou janela, dos dois lados (ou só de um, 'lado').</summary>
        void Moldura(Parede w, Abertura ab, string cor, int lado = 0, float largura = 0.08f)
        {
            foreach (int s in new[] { 1, -1 })
            {
                if (lado != 0 && s != lado) continue;
                float fora = s * (m.esp / 2f + 0.015f);
                float y0 = ab.EhPorta ? 0f : ab.baixo - 0.04f;
                Placa(w, ab.ini - largura, ab.ini, y0, ab.alto, fora, 0.03f, cor);
                Placa(w, ab.ini + ab.larg, ab.ini + ab.larg + largura, y0, ab.alto, fora, 0.03f, cor);
                Placa(w, ab.ini - largura, ab.ini + ab.larg + largura, ab.alto, ab.alto + largura, fora, 0.03f, cor);
            }
        }

        // ------------------------------------------------------------ as portas de fora

        void PortaDeFora(Parede w, Abertura ab, Porta p)
        {
            Moldura(w, ab, "Metal");
            if (p.tipo == TipoDePorta.Vidro)
            {
                // a entrada: a folha da esquerda aberta para dentro, sem o vidro (quebrado no saque); a da direita fechada,
                // com o vidro sujo. Por cima, a bandeira de vidro tapada com o compensado; por fora, a grade de enrolar
                float meia = ab.larg / 2f;
                Folha(w, ab.ini, meia, ab.alto, -1, 78f, true, TipoDePorta.Vidro, quebrada: true);
                Folha(w, ab.ini + meia, meia, ab.alto, -1, 0f, false, TipoDePorta.Vidro);
                GradeDeEnrolar(w, ab);
                Placa(w, ab.ini - 0.08f, ab.ini + ab.larg + 0.08f, ab.alto + 0.08f, ab.alto + 0.62f, m.esp / 2f + 0.03f, 0.03f, "Compensado");
                return;
            }
            // a dos fundos e a saída de emergência: fechadas
            Folha(w, ab.ini, ab.larg, ab.alto, 1, 0f, true, p.tipo);
            bool saida = p.a.tipo == "estande";
            // a placa por fora e, na saída, a barra antipânico por dentro
            Vector3 meio = w.Ponto(ab.ini + ab.larg / 2f, 1.65f, m.esp / 2f + 0.03f);
            var fora = new Vector3(w.n.x, 0f, w.n.y);
            var u = new Vector3(w.u.x, 0f, w.u.y);
            PlacaComTexto(meio, fora, 0.9f, 0.32f, saida ? "PlacaVermelha" : "PlacaBranca", saida ? "EMERGENCY EXIT" : "DELIVERIES", 0.11f, saida ? "PlacaBranca" : "Pneu");
            if (saida)
                Placa(w, ab.ini + 0.15f, ab.ini + ab.larg - 0.15f, 1.0f, 1.07f, -(m.esp / 2f + 0.06f), 0.06f, "Cromo");
        }

        /// <summary>
        /// A grade de enrolar da entrada (a sanfona de aço por fora da porta), arrombada: as duas metades recolhidas nos lados,
        /// a da esquerda torta (a puxaram com um carro), os trilhos de cima e de baixo e o cadeado arrebentado no chão.
        /// </summary>
        void GradeDeEnrolar(Parede w, Abertura ab)
        {
            float fora = m.esp / 2f + 0.09f;
            Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, ab.alto + 0.02f, ab.alto + 0.08f, fora, 0.06f, "Grade");
            Placa(w, ab.ini - 0.15f, ab.ini + ab.larg + 0.15f, 0.0f, 0.03f, fora, 0.05f, "Grade");
            for (int lado = 0; lado < 2; lado++)
            {
                float s0 = lado == 0 ? ab.ini - 0.1f : ab.ini + ab.larg - 0.22f;
                for (int i = 0; i < 7; i++)
                {
                    float s = s0 + i * 0.05f;
                    float giro = lado == 0 ? Faixa(4f, 11f) * (i % 2 == 0 ? 1f : 0.6f) : 0f;
                    Placa(w, s - 0.012f, s + 0.012f, 0.03f, ab.alto + 0.02f, fora + (lado == 0 ? 0.04f * i / 7f : 0f), 0.02f, "FerrugemEscura", giro);
                }
                // os losangos da sanfona (as barras em X)
                for (float y = 0.3f; y < ab.alto - 0.2f; y += 0.45f)
                    Placa(w, s0, s0 + 0.32f, y, y + 0.03f, fora + 0.012f, 0.012f, "FerrugemEscura", lado == 0 ? 18f : -14f);
            }
            // o cadeado arrebentado no chão, na calçada
            Vector3 c = w.Ponto(ab.ini + ab.larg * 0.7f, 0.03f, m.esp / 2f + 0.6f);
            Caixa(c, new Vector3(0.07f, 0.06f, 0.03f), "Cromo", 25f);
        }

        // ------------------------------------------------------------ as janelas de fora

        void JanelaDeFora(Parede w, Abertura ab, JanelaDeFora j)
        {
            const float e = 0.06f, t = 0.08f;
            float s0 = ab.ini, s1 = ab.ini + ab.larg, y0 = ab.baixo, y1 = ab.alto;
            // o caixilho (e, nas vitrines, o montante do meio), a pingadeira por fora e a moldura por dentro
            Placa(w, s0, s0 + e, y0, y1, 0f, t, "Metal");
            Placa(w, s1 - e, s1, y0, y1, 0f, t, "Metal");
            Placa(w, s0 + e, s1 - e, y1 - e, y1, 0f, t, "Metal");
            Placa(w, s0 + e, s1 - e, y0, y0 + e, 0f, t, "Metal");
            Placa(w, s0 - 0.08f, s1 + 0.08f, y0 - 0.06f, y0, m.esp / 2f + 0.04f, 0.1f, "ConcretoClaro");
            Moldura(w, ab, "Batente", -1, 0.06f);
            bool larga = ab.larg > 2f;
            float meio = (s0 + s1) / 2f;
            if (larga) Placa(w, meio - e / 2f, meio + e / 2f, y0 + e, y1 - e, 0f, t, "Metal");
            // os vidros (um ou dois): inteiro e sujo, quebrado com o buraco ou sem vidro (os cacos no caixilho), ou tapado com as
            // tábuas (por fora, atrás da grade)
            int n = larga ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? s0 + e : (i == 0 ? s0 + e : meio + e / 2f), b = n == 1 ? s1 - e : (i == 0 ? meio - e / 2f : s1 - e);
                var vidro = new Abertura(a, b - a, y0 + e, y1 - e);
                bool tabuas = j.vitrine && Chance(v.tabuas);
                // nas vitrines, cinco anos de saque: quase todas sem o vidro (só os cacos no caixilho), umas com o buraco e
                // poucas inteiras; o sol entra por elas e risca o chão com a sombra das grades
                double r = rnd.NextDouble();
                float inteira = j.vitrine ? 0.12f : 0.35f, furada = j.vitrine ? 0.4f : 0.8f;
                if (r < inteira) AbandonoDaObra.Vidro(this, w, vidro, rnd, false, "VidroSujo", -0.012f);
                else if (r < furada) AbandonoDaObra.Vidro(this, w, vidro, rnd, true, "VidroSujo", -0.012f);
                else
                    for (int k = 0; k < 4; k++)
                    {
                        float s = Faixa(a + 0.05f, b - 0.05f);
                        bool emCima = Chance(0.5f);
                        Placa(w, s - 0.06f, s + 0.06f, emCima ? vidro.alto - 0.15f : vidro.baixo, emCima ? vidro.alto : vidro.baixo + 0.15f, 0f, 0.01f, "VidroSujo", Faixa(-25f, 25f));
                    }
                if (tabuas) Tabuas(w, vidro);
            }
            if (j.vitrine || j.baixo > 1.5f && j.baixo < 2.0f || j.lado == 2) Grade(w, ab, j.vitrine);
        }

        // as tábuas pregadas por fora: em X com uma deitada, ou de 3 a 4 deitadas, ou a chapa de compensado
        void Tabuas(Parede w, Abertura ab)
        {
            float face = m.esp / 2f;
            float s0 = ab.ini - 0.05f, s1 = ab.ini + ab.larg + 0.05f, y0 = ab.baixo, y1 = ab.alto;
            float h = y1 - y0, cs = (s0 + s1) / 2f, cy = (y0 + y1) / 2f;
            string Madeira() => Um("MadeiraVelha", "Madeira", "BancoMadeira");
            double v = rnd.NextDouble();
            if (v < 0.25)
            {
                Placa(w, s0, s1, y0, y1, face + 0.03f, 0.02f, "Compensado", 0f, true);
                tapadas.Add(new Vector4(w.a.x, w.a.y, w.baseY, ab.ini));
                return;
            }
            if (v < 0.65)
            {
                float diag = Mathf.Sqrt((s1 - s0) * (s1 - s0) + h * h) - 0.2f, ang = Mathf.Atan2(h, s1 - s0) * Mathf.Rad2Deg;
                Placa(w, cs - diag / 2f, cs + diag / 2f, cy - 0.08f, cy + 0.08f, face + 0.03f, 0.025f, Madeira(), ang, true);
                Placa(w, cs - diag / 2f, cs + diag / 2f, cy - 0.08f, cy + 0.08f, face + 0.055f, 0.025f, Madeira(), -ang, true);
                return;
            }
            int k = rnd.Next(3, 5);
            for (int i = 0; i < k; i++)
            {
                float y = y0 + (i + 0.5f) * h / k;
                Placa(w, s0, s1, y - 0.08f, y + 0.08f, face + 0.03f, 0.025f, Madeira(), Faixa(-5f, 5f), true);
            }
        }

        /// <summary>
        /// A grade de barras por fora da janela (as vitrines, o banheiro e o escritório): o quadro, as barras de pé a cada 12 cm
        /// e as travessas; numa das vitrines (pela semente), umas barras arrancadas e tortas. A colisão fecha a janela.
        /// </summary>
        void Grade(Parede w, Abertura ab, bool vitrine)
        {
            // o ferro enferrujado (a cor da ferrugem não leva as manchas do metal no material do abandono: com a do metal, a
            // grade densa virava uma massa escura com manchas laranja)
            const string Ferro = "FerrugemEscura";
            float fora = m.esp / 2f + 0.09f;
            float s0 = ab.ini - 0.06f, s1 = ab.ini + ab.larg + 0.06f, y0 = ab.baixo - 0.06f, y1 = ab.alto + 0.06f;
            Placa(w, s0, s1, y1 - 0.04f, y1, fora, 0.025f, Ferro);
            Placa(w, s0, s1, y0, y0 + 0.04f, fora, 0.025f, Ferro);
            Placa(w, s0, s0 + 0.04f, y0, y1, fora, 0.025f, Ferro);
            Placa(w, s1 - 0.04f, s1, y0, y1, fora, 0.025f, Ferro);
            bool arrancada = vitrine && Chance(0.3f);
            float a0 = arrancada ? Faixa(s0 + 0.4f, s1 - 1.2f) : -1f;
            for (float s = s0 + 0.16f; s < s1 - 0.1f; s += 0.16f)
            {
                if (arrancada && s > a0 && s < a0 + 0.5f)
                {
                    if (Chance(0.5f)) Placa(w, s - 0.009f, s + 0.009f, y0 + 0.04f, (y0 + y1) / 2f, fora + 0.08f, 0.018f, "Ferrugem", Faixa(20f, 40f));
                    continue;
                }
                Placa(w, s - 0.009f, s + 0.009f, y0 + 0.04f, y1 - 0.04f, fora, 0.018f, Ferro);
            }
            for (float y = y0 + 0.7f; y < y1 - 0.3f; y += 0.95f) Placa(w, s0, s1, y, y + 0.035f, fora + 0.014f, 0.01f, Ferro);
            // a colisão: a grade fecha a janela (não se pula por ela)
            Vector2 meio = w.a + w.u * ((s0 + s1) / 2f) + w.n * fora;
            Caixa(new Vector3(meio.x, w.baseY + (y0 + y1) / 2f, meio.y), new Vector3(s1 - s0, y1 - y0, 0.05f), Ferro, w.yaw, colide: true, visual: false);
        }

        // ------------------------------------------------------------ a fachada

        /// <summary>Uma placa com um texto (as letras chatas do kit, no meio dela): o meio da face da frente e o lado de fora.</summary>
        void PlacaComTexto(Vector3 meio, Vector3 fora, float largura, float altura, string fundo, string texto, float h, string corDaLetra,
                           float grossura = 0.02f)
        {
            // a direita de quem lê (de frente para a placa)
            var u = Vector3.Cross(Vector3.up, -fora).normalized;
            var giro = Quaternion.LookRotation(-fora, Vector3.up);
            CaixaM(Matrix4x4.TRS(meio - fora * (grossura / 2f), giro, Vector3.one), new Vector3(largura, altura, grossura), fundo);
            Letreiro(texto, meio - Vector3.up * (h / 2f), u, fora, h, corDaLetra, plano: true, sai: 0.002f);
        }

        void Fachada()
        {
            float zf = Z0 - m.esp / 2f;       // a face de fora da frente
            var fora = Vector3.back;
            var u = Vector3.right;
            // as pilastras de tijolo (as quinas, o encontro da loja com a ala do estande e a do banheiro) e o embasamento
            foreach (float x in new[] { X(0.32f), X(15.28f), X(20.75f), X(23.68f) })
                Caixa(new Vector3(x, 2.05f, zf - 0.06f), new Vector3(0.62f, 4.1f, 0.12f), "TijoloAparente");
            Caixa(new Vector3(0f, 0.12f, zf - 0.03f), new Vector3(X1 - X0 + 0.2f, 0.24f, 0.06f), "Concreto");
            // a cornija de concreto e a faixa das letras
            Caixa(new Vector3(0f, 4.13f, zf - 0.08f), new Vector3(X1 - X0 + 0.36f, 0.18f, 0.16f), "ConcretoClaro");
            PlacaComTexto(new Vector3(0f, 4.62f, zf - 0.07f), fora, X1 - X0 - 0.6f, 0.62f, FundoDaFaixa, "FIREARMS  *  AMMUNITION  *  SHOOTING RANGE", 0.34f, Creme, 0.05f);
            for (float x = X0 + 0.5f; x < X1 - 0.4f; x += 0.6f)
                if (Chance(0.12f)) Caixa(new Vector3(x, Faixa(4.4f, 4.85f), zf - 0.096f), new Vector3(Faixa(0.08f, 0.3f), Faixa(0.04f, 0.12f), 0.004f), "Concreto");
            // o frontão do meio (a parede subindo atrás do letreiro), com o capeamento
            float xa = X(1.8f), xb = X(13.8f);
            Caixa(new Vector3((xa + xb) / 2f, (AltoDaFachada + AltoDoFrontao) / 2f, Z0), new Vector3(xb - xa, AltoDoFrontao - AltoDaFachada, m.esp), "ConcretoClaro", colide: true);
            Caixa(new Vector3((xa + xb) / 2f, AltoDoFrontao + 0.04f, Z0), new Vector3(xb - xa + 0.12f, 0.08f, m.esp + 0.12f), "ConcretoClaro");
            Caixa(new Vector3(0f, AltoDaFachada + 0.04f, Z0), new Vector3(X1 - X0 + 0.24f, 0.08f, m.esp + 0.12f), "ConcretoClaro");
            LetreiroDaFrente();
            Luminarias(zf);
            Bandeira(new Vector3(X(15.9f), 5.15f, zf - 0.004f));
            CervoPintado(new Vector3(X(0.95f), 5.15f, zf - 0.004f));
        }

        // o letreiro vermelho "GUNS & AMMO" (as letras creme em relevo, as estrelas dos lados e a moldura) e o fuzil em cima
        void LetreiroDaFrente()
        {
            float zf = Z0 - m.esp / 2f;
            float xa = X(2.9f), xb = X(12.7f), ya = 5.05f, yb = 6.9f, cx = (xa + xb) / 2f;
            Caixa(new Vector3(cx, (ya + yb) / 2f, zf - 0.05f), new Vector3(xb - xa + 0.16f, yb - ya + 0.16f, 0.06f), Creme);
            Caixa(new Vector3(cx, (ya + yb) / 2f, zf - 0.07f), new Vector3(xb - xa, yb - ya, 0.06f), "PlacaVermelha");
            // a tinta descascando: umas manchas claras e os pregos
            for (int i = 0; i < 14; i++)
                Caixa(new Vector3(Faixa(xa + 0.2f, xb - 0.2f), Faixa(ya + 0.15f, yb - 0.15f), zf - 0.101f), new Vector3(Faixa(0.1f, 0.5f), Faixa(0.05f, 0.25f), 0.002f), "Batente");
            Letreiro("GUNS & AMMO", new Vector3(cx, ya + 0.22f, zf - 0.1f), Vector3.right, Vector3.back, 0.86f, Creme, sai: 0.02f, grossura: 0.04f);
            foreach (float s in new[] { -1f, 1f })
                Letreiro("*", new Vector3(cx + s * 4.1f, ya + 0.38f, zf - 0.1f), Vector3.right, Vector3.back, 0.55f, Creme, sai: 0.02f, grossura: 0.03f);
            // o fuzil (o perfil de um AR-15 em placas), em cima das letras
            var M = Matrix4x4.Translate(new Vector3(cx - 0.1f, ya + 1.45f, zf - 0.115f));
            void P(float x, float y, float w, float h, float giro = 0f) =>
                CaixaM(M * Matrix4x4.Translate(new Vector3(x, y, 0f)) * RZ(giro), new Vector3(w, h, 0.03f), Creme);
            P(-0.86f, 0.0f, 0.44f, 0.16f, 3f);    // a coronha
            P(-0.56f, 0.02f, 0.24f, 0.08f);       // o tubo
            P(-0.24f, 0.03f, 0.46f, 0.15f);       // a caixa da culatra
            P(-0.22f, 0.16f, 0.26f, 0.05f);       // a alça de transporte
            P(-0.20f, -0.14f, 0.07f, 0.20f, -18f);// o punho
            P(-0.02f, -0.18f, 0.09f, 0.26f, 8f);  // o carregador
            P(0.32f, 0.03f, 0.46f, 0.11f);        // o guarda-mão
            P(0.62f, 0.10f, 0.03f, 0.16f);        // a massa de mira
            P(0.82f, 0.04f, 0.50f, 0.035f);       // o cano
        }

        // as luminárias pescoço de ganso (apagadas): quatro em cima do letreiro e as de baixo da faixa, em cima das vitrines e da porta
        void Luminarias(float zf)
        {
            void Uma(float x, float y, float sai, bool deCima)
            {
                var b = new Vector3(x, y, zf);
                Caixa(b + new Vector3(0f, 0f, -0.02f), new Vector3(0.12f, 0.12f, 0.04f), "Pneu");
                var cotovelo = b + new Vector3(0f, deCima ? 0.28f : 0.14f, -sai * 0.55f);
                var ponta = b + new Vector3(0f, deCima ? 0.18f : 0.02f, -sai);
                Tubo(b + new Vector3(0f, 0f, -0.03f), cotovelo, 0.018f, "Pneu");
                Tubo(cotovelo, ponta, 0.018f, "Pneu");
                Cilindro(ponta + new Vector3(0f, -0.13f, 0f), 0.2f, 0.06f, 0.13f, 10, "ParedeVerdeEscura");
                Cilindro(ponta + new Vector3(0f, -0.135f, 0f), 0.08f, 0.08f, 0.02f, 8, "Farol");
            }
            foreach (float x in new[] { X(4.4f), X(6.8f), X(9.2f), X(11.6f) }) Uma(x, 7.0f, 0.75f, true);
            foreach (float x in new[] { X(2.1f), X(5.1f), X(7.8f), X(10.5f), X(13.5f), X(18.1f) }) Uma(x, 3.9f, 0.5f, false);
        }

        // a bandeira pintada na parede (desbotada): as 13 listras e o cantão azul com as estrelas (pontos), do canto de baixo à esquerda
        void Bandeira(Vector3 c)
        {
            const float W = 2.3f, H = 1.21f;
            float faixa = H / 13f;
            for (int i = 0; i < 13; i++)
            {
                float y = c.y + H - (i + 0.5f) * faixa;
                Caixa(new Vector3(c.x + W / 2f, y, c.z), new Vector3(W, faixa, 0.004f), i % 2 == 0 ? "PlacaVermelha" : "PlacaBranca");
            }
            float cw = W * 0.4f, ch = faixa * 7f;
            Caixa(new Vector3(c.x + cw / 2f, c.y + H - ch / 2f, c.z - 0.002f), new Vector3(cw, ch, 0.004f), "CarroAzul");
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 4; j++)
                    Caixa(new Vector3(c.x + 0.09f + i * (cw - 0.18f) / 4f, c.y + H - 0.09f - j * (ch - 0.18f) / 3f, c.z - 0.0045f), new Vector3(0.035f, 0.035f, 0.002f), "PlacaBranca");
            // o desbotado: umas faixas de reboco aparecendo
            for (int i = 0; i < 6; i++)
                Caixa(new Vector3(c.x + Faixa(0.1f, W - 0.1f), c.y + Faixa(0.1f, H - 0.1f), c.z - 0.005f), new Vector3(Faixa(0.08f, 0.35f), Faixa(0.03f, 0.1f), 0.002f), "ConcretoClaro");
        }

        // o cervo pintado (a silhueta da cabeça com a galhada, marrom), do lado esquerdo da fachada
        void CervoPintado(Vector3 c)
        {
            void P(float x, float y, float w, float h, float giro = 0f) =>
                CaixaM(Matrix4x4.Translate(c + new Vector3(x, y, 0f)) * RZ(giro), new Vector3(w, h, 0.004f), "Madeira");
            P(0f, 0.18f, 0.22f, 0.36f);           // o pescoço
            P(0f, 0.42f, 0.26f, 0.22f);           // a cabeça
            P(0.0f, 0.24f, 0.13f, 0.20f);         // o focinho, para baixo
            foreach (float s in new[] { -1f, 1f })
            {
                P(s * 0.17f, 0.5f, 0.14f, 0.06f, s * 25f);              // a orelha
                P(s * 0.16f, 0.66f, 0.04f, 0.28f, s * -25f);            // a galhada
                P(s * 0.27f, 0.82f, 0.04f, 0.22f, s * -50f);
                P(s * 0.12f, 0.86f, 0.035f, 0.18f, s * 10f);
                P(s * 0.34f, 0.98f, 0.03f, 0.14f, s * -20f);
            }
        }

        // ------------------------------------------------------------ o telhado, os lados e os fundos

        void Telhado()
        {
            float topo = TopoDaCobertura;
            // o capeamento de metal em cima da platibanda (os lados e os fundos)
            float e = m.esp / 2f + 0.04f;
            Caixa(new Vector3(X0, topo + Platibanda + 0.02f, 0f), new Vector3(2f * e, 0.04f, Z1 - Z0 + 2f * e), "Metal");
            Caixa(new Vector3(X1, topo + Platibanda + 0.02f, 0f), new Vector3(2f * e, 0.04f, Z1 - Z0 + 2f * e), "Metal");
            Caixa(new Vector3(0f, topo + Platibanda + 0.02f, Z1), new Vector3(X1 - X0 + 2f * e, 0.04f, 2f * e), "Metal");
            // os aparelhos de ar (com a hélice e a grade) em cima da área de vendas e do estande, o respiro e o alçapão
            foreach (var (x, z) in new[] { (X(5f), Z(12f)), (X(10.5f), Z(16f)), (X(19.8f), Z(18f)) })
            {
                Caixa(new Vector3(x, topo + 0.06f, z), new Vector3(1.5f, 0.12f, 1.2f), "Concreto");
                Caixa(new Vector3(x, topo + 0.55f, z), new Vector3(1.3f, 0.86f, 1.0f), "Metal", colide: true);
                Cilindro(new Vector3(x, topo + 0.98f, z), 0.4f, 0.4f, 0.03f, 12, "Pneu");
                Caixa(new Vector3(x - 0.66f, topo + 0.55f, z), new Vector3(0.02f, 0.7f, 0.8f), "Grade");
            }
            Caixa(new Vector3(X(9f), topo + 0.25f, Z(27f)), new Vector3(1.0f, 0.5f, 1.0f), "Metal", colide: true);
            Cilindro(new Vector3(X(19f), topo, Z(26f)), 0.12f, 0.12f, 0.9f, 8, "Metal");
            Cilindro(new Vector3(X(19f), topo + 0.9f, Z(26f)), 0.2f, 0.18f, 0.12f, 8, "Metal");
            Cilindro(new Vector3(X(3f), topo, Z(26f)), 0.06f, 0.06f, 0.6f, 6, "Metal");
        }

        void LadosEFundos()
        {
            float e = m.esp / 2f;
            // as faixas vermelhas dos lados (a de cima mais fina), cortadas na saída de emergência
            foreach (float x in new[] { X0 - e - 0.003f, X1 + e + 0.003f })
            {
                bool leste = x > 0f;
                float saida = Z(8.7f);
                void Listra(float z0, float z1, float y0, float y1) =>
                    Caixa(new Vector3(x, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(0.006f, y1 - y0, z1 - z0), "PlacaVermelha");
                foreach (var (y0, y1) in new[] { (1.85f, 2.07f), (2.22f, 2.29f) })
                {
                    if (!leste) { Listra(Z0, Z1, y0, y1); continue; }
                    Listra(Z0, saida - 0.8f, y0, y1);
                    Listra(saida + 0.8f, Z1, y0, y1);
                }
            }
            // os condutores da chuva (os fundos e o leste) e as bocas de saída na platibanda
            foreach (var (x, z) in new[] { (X0 + 0.4f, Z1 + e + 0.08f), (X1 - 0.4f, Z1 + e + 0.08f), (X1 + e + 0.08f, Z(19f)), (X0 - e - 0.08f, Z(19f)) })
            {
                Tubo(new Vector3(x, ChaoDeFora + 0.1f, z), new Vector3(x, TopoDaCobertura + 0.15f, z), 0.05f, "Metal");
                Caixa(new Vector3(x, TopoDaCobertura + 0.25f, z), new Vector3(0.22f, 0.22f, 0.22f), "Metal");
            }
            // os degraus da porta dos fundos e da saída de emergência (o chão de fora 15 cm abaixo)
            Caixa(new Vector3(X(8.9f), ChaoDeFora / 2f - 0.01f, Z1 + e + 0.55f), new Vector3(1.8f, -ChaoDeFora + 0.02f, 1.1f), "Concreto", colide: true);
            Caixa(new Vector3(X1 + e + 0.55f, ChaoDeFora / 2f - 0.01f, Z(8.7f)), new Vector3(1.1f, -ChaoDeFora + 0.02f, 1.8f), "Concreto", colide: true);
            // o relógio de luz e o quadro, no lado oeste perto dos fundos
            Caixa(new Vector3(X0 - e - 0.08f, 1.5f, Z(26f)), new Vector3(0.16f, 0.5f, 0.4f), "Metal");
            Cilindro(new Vector3(X0 - e - 0.17f, 1.55f, Z(26f)), 0.08f, 0.08f, 0.02f, 10, "Vidro");
            Tubo(new Vector3(X0 - e - 0.06f, 1.25f, Z(26f)), new Vector3(X0 - e - 0.06f, 0f, Z(26f)), 0.025f, "Metal");
            // o cartaz do alvo na quina da frente do lado leste (o papel branco com a silhueta preta)
            var cartaz = new Vector3(X1 + e + 0.004f, 1.6f, Z(1.4f));
            Caixa(cartaz, new Vector3(0.006f, 0.9f, 0.6f), "PlacaBranca");
            Caixa(cartaz + new Vector3(0.004f, -0.12f, 0f), new Vector3(0.006f, 0.52f, 0.38f), "Pneu");
            Caixa(cartaz + new Vector3(0.004f, 0.25f, 0f), new Vector3(0.006f, 0.2f, 0.17f), "Pneu");
        }
    }
}
