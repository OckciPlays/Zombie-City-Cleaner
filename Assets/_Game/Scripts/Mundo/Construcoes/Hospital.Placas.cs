using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As placas do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 3), em inglês (o jogo se passa nos
    /// Estados Unidos): as penduradas nos corredores (verdes, dos dois lados, com a seta em frente), o diretório e o número do
    /// andar nos lados do poço do elevador, o painel e o mostrador do elevador (parado no 2), o aviso "OUT OF ORDER" no
    /// térreo e o número do andar nos patamares das escadas; por fora, o EMERGENCY em cima dos portões da garagem e o
    /// MAIN ENTRANCE na marquise. As placas de cima das portas e as de saída
    /// estão nos acabamentos (Hospital.Acabamento.cs), e o nome da torre, na Hospital.Fachada.cs.
    /// </summary>
    public sealed partial class Hospital
    {
        /// <summary>
        /// As placas penduradas de cada andar: no corredor (o x e o z do meio), logo fora do saguão, com a face de +x (lida de
        /// quem anda para oeste) e a de -x (de quem anda para leste). O '^' é a seta em frente.
        /// </summary>
        static readonly (int f, float x, float z, string mais, string menos)[] Penduradas =
        {
            (0, -6.5f, -6.5f, "^ AUTOPSY  WORKSHOP", "^ ELEVATOR"),
            (0, 6.5f, -6.5f, "^ ELEVATOR", "^ MORGUE  PUMP ROOM"),
            (0, -6.5f, 6.5f, "^ LAUNDRY", "^ ELEVATOR"),
            (0, 6.5f, 6.5f, "^ ELEVATOR", "^ ELECTRICAL  WASTE"),
            (Terreo, -8.5f, -6.5f, "^ EMERGENCY", "^ MAIN EXIT"),
            (Terreo, 8.5f, -6.5f, "^ MAIN EXIT", "^ LABORATORY"),
            (Terreo, -8.5f, 6.5f, "^ TRAUMA  MEDICATION", "^ RECEPTION"),
            (Terreo, 8.5f, 6.5f, "^ RECEPTION", "^ X-RAY  LAB"),
            (2, -12.5f, -6.5f, "^ MEDICAL SUPPLY", "^ NURSES STATION"),
            (2, 12.5f, -6.5f, "^ NURSES STATION", "^ STAFF LOUNGE"),
            (2, -12.5f, 6.5f, "^ EXAM 3  EXAM 4", "^ NURSES STATION"),
            (2, 12.5f, 6.5f, "^ NURSES STATION", "^ SURGERY"),
            (3, -8.5f, -6.5f, "^ HR  SECURITY", "^ LOBBY"),
            (3, 8.5f, -6.5f, "^ LOBBY", "^ SECRETARY"),
            (3, -8.5f, 6.5f, "^ BREAK ROOM  IT", "^ LOBBY"),
            (3, 8.5f, 6.5f, "^ LOBBY", "^ RECORDS"),
        };

        /// <summary>O número de cada andar nas placas (como nos hospitais americanos: o térreo é o 1, o subsolo é o B).</summary>
        static readonly string[] Numeros = { "B", "1", "2", "3" };
        /// <summary>O que tem em cada andar, no diretório.</summary>
        static readonly string[] Setores = { "MORGUE  SERVICES", "EMERGENCY  X-RAY  LAB", "INPATIENT  ICU  SURGERY", "ADMINISTRATION" };
        static readonly string[] NomesDosNumeros = { "BASEMENT", "EMERGENCY", "INPATIENT", "ADMINISTRATION" };

        /// <summary>As placas do andar: as penduradas, as do elevador e as das escadas.</summary>
        void PlacasDoAndar(Andar an)
        {
            int f = an.f;
            foreach (var (pf, x, z, mais, menos) in Penduradas)
                if (pf == f) Pendurada(x, z, Teto(f), mais, menos);
            NoElevador(f);
            foreach (var k in an.comodos)
                if (k.tipo == "escada") NaEscada(an, k);
        }

        // uma placa pendurada no teto por duas correntes, atravessada no corredor (no plano de z), com o texto dos dois lados
        void Pendurada(float x, float z, float teto, string mais, string menos)
        {
            const float h = 0.1f, alto = 0.3f;
            float larg = Mathf.Max(Letras.Largura(mais, h), Letras.Largura(menos, h)) + 0.3f, y = teto - 0.55f;
            Caixa(new Vector3(x, y, z), new Vector3(0.03f, alto, larg), "PlacaVerde");
            foreach (float s in new[] { -1f, 1f })
                Tubo(new Vector3(x, teto, z + s * (larg / 2f - 0.12f)), new Vector3(x, y + alto / 2f, z + s * (larg / 2f - 0.12f)), 0.006f, "Grade");
            Letreiro(mais, new Vector3(x + 0.015f, y - h / 2f, z), Leitura(Vector3.right), Vector3.right, h, "PlacaBranca", plano: true, sai: 0.002f);
            Letreiro(menos, new Vector3(x - 0.015f, y - h / 2f, z), Leitura(Vector3.left), Vector3.left, h, "PlacaBranca", plano: true, sai: 0.002f);
        }

        /// <summary>
        /// No saguão, em volta do elevador: o painel de chamada ao lado da porta, o mostrador em cima dela (parado no 2), o
        /// diretório na face oeste do poço (o andar em que se está, em amarelo), o número e o nome do andar pintados na face
        /// leste e, no térreo, o papel de "OUT OF ORDER" colado na porta.
        /// </summary>
        void NoElevador(int f)
        {
            var el = Elevador;
            float b = Base(f), cx = el.center.x, zf = el.yMin - m.esp / 2f;
            var frente = new Plano { o = new Vector3(0f, b, zf), u = Vector3.right, n = Vector3.back, emX = true };
            // o painel de chamada e os dois botões (apagados)
            Relevo(frente, cx + 1.0f, cx + 1.12f, 1.0f, 1.24f, 0.02f, "Cromo");
            Relevo(frente, cx + 1.04f, cx + 1.08f, 1.14f, 1.18f, 0.03f, "Lanterna");
            Relevo(frente, cx + 1.04f, cx + 1.08f, 1.06f, 1.1f, 0.03f, "Lanterna");
            // o mostrador
            float y = m.alturaPorta + 0.17f;
            Relevo(frente, cx - 0.22f, cx + 0.22f, y, y + 0.14f, 0.03f, "Pneu");
            Letreiro("^2", P(frente, cx, y + 0.035f, 0f), Leitura(frente.n), frente.n, 0.07f, "Lanterna", plano: true, sai: 0.032f);
            if (f == Terreo)
            {
                // o papel colado na folha da esquerda (a folha fica no meio da parede: a face dela está 2,5 cm para fora)
                float meio = cx - 0.38f, yp = 1.35f;
                var papel = new Plano { o = new Vector3(0f, b, el.yMin), u = Vector3.right, n = Vector3.back, emX = true };
                Pintura(papel, meio - 0.16f, meio + 0.16f, yp, yp + 0.42f, 0.027f, "PlacaBranca");
                Letreiro("OUT OF", P(papel, meio, yp + 0.25f, 0f), Leitura(papel.n), papel.n, 0.05f, "Pneu", plano: true, sai: 0.029f);
                Letreiro("ORDER", P(papel, meio, yp + 0.14f, 0f), Leitura(papel.n), papel.n, 0.05f, "Pneu", plano: true, sai: 0.029f);
            }
            // o diretório, na face oeste do poço (olhando para -x)
            float xo = el.xMin - m.esp / 2f, zc = el.center.y;
            var oeste = new Plano { o = new Vector3(xo, b, 0f), u = Vector3.forward, n = Vector3.left, emX = false };
            Relevo(oeste, zc - 1.2f, zc + 1.2f, 1.25f, 2.35f, 0.02f, "PlacaVerde");
            Vector3 u = Leitura(oeste.n);
            Letreiro("DIRECTORY", P(oeste, zc, 2.16f, 0f), u, oeste.n, 0.09f, "PlacaBranca", plano: true, sai: 0.022f);
            for (int i = 0; i < Andares; i++)
            {
                int g = Andares - 1 - i;
                float yl = 1.92f - i * 0.17f;
                string linha = $"{Numeros[g]}  {Setores[g]}";
                const float hl = 0.07f;
                // alinhada à esquerda de quem lê (o começo em zc + 1.05, andando em u, que aqui é -z)
                Vector3 meio = P(oeste, zc + 1.05f, yl, 0f) + u * (Letras.Largura(linha, hl) / 2f);
                if (g == f) Relevo(oeste, zc - 1.12f, zc + 1.12f, yl - 0.04f, yl + hl + 0.04f, 0.024f, "PlacaAmarela");
                Letreiro(linha, meio, u, oeste.n, hl, g == f ? "Pneu" : "PlacaBranca", plano: true, sai: g == f ? 0.026f : 0.022f);
            }
            // o número e o nome do andar, pintados na face leste
            var leste = new Plano { o = new Vector3(el.xMax + m.esp / 2f, b, 0f), u = Vector3.forward, n = Vector3.right, emX = false };
            Letreiro(Numeros[f], P(leste, zc, 1.62f, 0f), Leitura(leste.n), leste.n, 0.5f, "PlacaVerde", plano: true, sai: 0.004f);
            Letreiro(NomesDosNumeros[f], P(leste, zc, 1.36f, 0f), Leitura(leste.n), leste.n, 0.12f, "PlacaVerde", plano: true, sai: 0.004f);
        }

        // no patamar de cada escada, na parede da porta, por dentro: o número do andar grande e o nome da escada em cima
        void NaEscada(Andar an, Comodo k)
        {
            var p = new Plano { o = new Vector3(0f, Base(an.f), k.z0 + m.esp / 2f), u = Vector3.right, n = Vector3.forward, emX = true };
            float x = k.Cx + 1.2f;
            Letreiro(Numeros[an.f], P(p, x, 1.45f, 0f), Leitura(p.n), p.n, 0.55f, "PlacaVerde", plano: true, sai: 0.004f);
            if (an.placas.TryGetValue(k, out var nome))
                Letreiro(nome, P(p, x, 2.15f, 0f), Leitura(p.n), p.n, 0.075f, "PlacaVerde", plano: true, sai: 0.004f);
        }

        // ------------------------------------------------------------ por fora

        /// <summary>
        /// Os letreiros de fora (o nome e a cruz da torre estão na Hospital.Fachada.cs): o EMERGENCY em vermelho na placa branca
        /// em cima dos portões da garagem (embaixo da faixa de concreto com a cruz) e o MAIN ENTRANCE na frente da marquise.
        /// </summary>
        void LetreirosDeFora()
        {
            var g = GaragemDasAmbulancias;
            float xg = g.xMin - m.esp / 2f;
            Caixa(new Vector3(xg - 0.025f, 3.9f, g.center.y), new Vector3(0.05f, 0.66f, 4.2f), "PlacaBranca");
            Letreiro("EMERGENCY", new Vector3(xg, 3.67f, g.center.y), Vector3.back, Vector3.left, 0.46f, "PlacaVermelha", sai: 0.08f, grossura: 0.05f);
            Letreiro("MAIN ENTRANCE", new Vector3(0f, TopoDaMarquise - 0.45f, Marquise.yMin), Vector3.right, Vector3.back, 0.24f, "Bunker", sai: 0.012f, grossura: 0.02f);
        }
    }
}
