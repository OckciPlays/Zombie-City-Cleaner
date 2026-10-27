using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Um cômodo: um retângulo num andar (-1 = porão), com o papel de parede e o piso.</summary>
    public sealed class Comodo
    {
        public string tipo;
        public int andar;
        public float x0, z0, x1, z1;
        public string papel, piso;
        /// <summary>No prédio, o apartamento do cômodo (o papel e o piso mudam com ele); -1 nos de todos (corredor, portaria, loja).</summary>
        public int apto = -1;

        public float Cx => (x0 + x1) / 2;
        public float Cz => (z0 + z1) / 2;
        public bool Contem(float x, float z) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
    }

    /// <summary>
    /// Uma porta ou passagem entre dois cômodos (b = null: para fora). 'pos' é a coordenada do meio dela ao longo da
    /// linha em comum: x se a linha corre em x, z se corre em z.
    /// </summary>
    public sealed class Ligacao
    {
        public Comodo a, b;
        public float pos;
        public bool passagem;
        /// <summary>Só para fora: o lado da casa (0 frente, 1 direita, 2 fundo, 3 esquerda).</summary>
        public int lado;
        /// <summary>A porta dá na garagem (fica aberta, para dar para chegar no carro por dentro).</summary>
        public bool paraGaragem;
        /// <summary>Nas lojas: a largura do vão (0 = a da porta ou da passagem) e a porta de aço (a do depósito da loja de armas).</summary>
        public float largura;
        public bool deAco;
    }

    /// <summary>
    /// O lugar do carro, de um lado da casa: a garagem anexa (com o portão na frente) ou só a vaga de concreto ao lado.
    /// O carro nasce nele. z0 a z1 é o comprimento (de frente para trás) e a largura conta da parede da casa para fora.
    /// </summary>
    public sealed class Garagem
    {
        /// <summary>1: do lado direito da casa (x1); -1: do esquerdo (x0).</summary>
        public int lado;
        public float z0, z1, largura;
        /// <summary>true: a garagem fechada (paredes, telhado e portão); false: só a vaga ao lado, descoberta.</summary>
        public bool coberta;
        /// <summary>O portão da garagem fechado (abrir é da Parte M7).</summary>
        public bool fechada;
        /// <summary>Uma porta da garagem para dentro da casa.</summary>
        public bool portaParaDentro;
    }

    /// <summary>Um lance de escada ao longo de z, subindo do andar 'de' para o de cima, de zIni (sentido +z).</summary>
    public sealed class LanceDaPlanta
    {
        public int de;
        public float x0, x1, zIni;
        /// <summary>Uma parede debaixo do lance, do lado de x0 (o vão debaixo da escada fica fechado).</summary>
        public bool fechadoEmbaixo;
    }

    /// <summary>
    /// A planta de uma casa: o tamanho, os cômodos de cada andar, as portas e as escadas, e a aparência sorteada.
    /// Os moldes (PlantasDeCasa) desenham a planta; a Casa levanta a construção a partir dela.
    /// Coordenadas da casa: o meio no centro, x de -W/2 a W/2, z de -D/2 (a frente) a D/2 (o fundo).
    /// </summary>
    public sealed class Planta
    {
        public string molde;
        public int semente;
        public float W, D;
        public int andares = 1;
        public bool porao;
        /// <summary>A entrada do porão por fora (a porta inclinada à americana): 0 nenhuma, 1 na direita, -1 na esquerda.</summary>
        public int poraoDeFora;
        public float zPoraoDeFora;
        public readonly List<Comodo> comodos = new List<Comodo>();
        public readonly List<Ligacao> ligacoes = new List<Ligacao>();
        public readonly List<LanceDaPlanta> lances = new List<LanceDaPlanta>();
        public float xPortaFrente;
        /// <summary>A garagem ou a vaga ao lado (toda casa tem uma das duas).</summary>
        public Garagem garagem;

        // aparência
        public string cor = "ParedeVerde", escura = "ParedeVerdeEscura", telhado = "Telhado", telhadoEscuro = "TelhadoEscuro";
        public string corDaPorta = "Madeira";
        public bool varanda, varandaLarga, cumeeiraNoX, espelhar, tabuasPorDentro, portaDaFrenteAberta = true;
        public float fundoDaVaranda = 1.8f;
        /// <summary>-1 esquerda, 0 nenhuma, 1 direita.</summary>
        public int chamine, ar;
        /// <summary>Os pesos das janelas: abertas e com tábuas.</summary>
        public int[] janelas = { 3, 1 };
        public int sangue;

        public float X0 => -W / 2; public float X1 => W / 2; public float Z0 => -D / 2; public float Z1 => D / 2;

        public Comodo Comodo(string tipo, int andar, float x0, float z0, float x1, float z1)
        {
            var c = new Comodo { tipo = tipo, andar = andar, x0 = x0, z0 = z0, x1 = x1, z1 = z1 };
            comodos.Add(c);
            return c;
        }

        public void Porta(Comodo a, Comodo b, float pos) => ligacoes.Add(new Ligacao { a = a, b = b, pos = pos });
        public void Passagem(Comodo a, Comodo b, float pos) => ligacoes.Add(new Ligacao { a = a, b = b, pos = pos, passagem = true });
        public void Fora(Comodo a, int lado, float pos, bool paraGaragem = false) =>
            ligacoes.Add(new Ligacao { a = a, lado = lado, pos = pos, paraGaragem = paraGaragem });
    }

    /// <summary>
    /// Os moldes das casas: plantas desenhadas, com o tamanho e a divisão sorteados dentro de faixas que mantêm a casa
    /// boa de andar. A aparência (cores, telhado, varanda, chaminé, janelas) é sorteada à parte, em Estilo.
    /// </summary>
    public static class PlantasDeCasa
    {
        static float F(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static float Arred(float v) => Mathf.Round(v * 10f) / 10f;
        static T Um<T>(System.Random r, params T[] o) => o[r.Next(o.Length)];

        /// <summary>Uma casa sorteada (térrea ou sobrado), com a aparência sorteada.</summary>
        public static Planta Sortear(System.Random r, Medidas m)
        {
            var p = r.NextDouble() < 0.55 ? Terrea(r, m) : Sobrado(r, m);
            Estilo(r, p);
            return p;
        }

        /// <summary>
        /// Uma casa que cabe no lote (Parte M6): até 'largura' de frente (com a garagem, a chaminé, o ar e o fosso,
        /// <see cref="Casa.Limites"/>) e até 'fundo' da frente dos degraus da frente aos dos fundos
        /// (<see cref="Casa.Profundidade"/>). Sorteia até 10 vezes; sem nenhuma que caiba, um sobrado sem porão com a vaga
        /// (a casa mais estreita); e null se nem ele couber.
        /// </summary>
        public static Planta ParaOLote(System.Random r, Medidas m, float largura, float fundo)
        {
            for (int k = 0; k < 10; k++)
            {
                var p = Sortear(r, m);
                if (Cabe(p, m, largura, fundo)) return p;
            }
            var s = Sobrado(r, m, false);
            s.garagem = NovaVaga(-1, s.Z0);
            Estilo(r, s);
            return Cabe(s, m, largura, fundo) ? s : null;
        }

        static bool Cabe(Planta p, Medidas m, float largura, float fundo)
        {
            Vector2 x = Casa.Limites(p, m), z = Casa.Profundidade(p, m);
            return x.y - x.x <= largura && z.y - z.x <= fundo;
        }

        /// <summary>
        /// A casa da família (Parte M6): uma térrea sem porão (o bunker dela entra depois, Plano_Mapa M10), com o portão da
        /// garagem aberto (é por ele que o carro sai).
        /// </summary>
        public static Planta DaFamilia(System.Random r, Medidas m)
        {
            var p = Terrea(r, m, false);
            Estilo(r, p);
            if (p.garagem != null) p.garagem.fechada = false;
            return p;
        }

        /// <summary>
        /// Quanto os moldes esticam: eles foram desenhados na escala 1,15 (a do jogo), e os cômodos, os corredores e
        /// as posições das portas crescem com a escala das medidas (a sala de medidas da Parte M0 compara 1, 1,15 e 1,30).
        /// </summary>
        static float Esticar(Medidas m) => m.escala > 0f ? m.escala / 1.15f : 1f;

        /// <summary>
        /// Térrea (14 a 17 × 11,6 a 13 m na escala 1,15): na frente, a sala e a cozinha; no meio, o corredor de um lado
        /// ao outro, com a porta do lado; atrás, dois quartos e o banheiro no meio. O porão (metade das casas, ou o que
        /// 'porao' pedir) tem a entrada por fora.
        /// </summary>
        public static Planta Terrea(System.Random r, Medidas m, bool? porao = null)
        {
            float e = Esticar(m);
            var p = new Planta { molde = "Terrea", W = Arred(F(r, 14f, 17f) * e), D = Arred(F(r, 11.6f, 13f) * e) };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, W = p.W, D = p.D;
            float zA = Arred(z0 + Mathf.Max(5f * e, D * 0.42f));
            float zC = zA + Mathf.Max(1.8f * e, m.porta + 0.5f);
            float xS = Arred(x0 + W * F(r, 0.55f, 0.62f));
            float lb = 3.0f * e;
            float xQ = Arred(x0 + (W - lb) * F(r, 0.44f, 0.56f));
            var sala = p.Comodo("sala", 0, x0, z0, xS, zA);
            var coz = p.Comodo("cozinha", 0, xS, z0, x1, zA);
            var cor = p.Comodo("corredor", 0, x0, zA, x1, zC);
            var q1 = p.Comodo("quarto", 0, x0, zC, xQ, z1);
            var ban = p.Comodo("banheiro", 0, xQ, zC, xQ + lb, z1);
            var q2 = p.Comodo("quarto", 0, xQ + lb, zC, x1, z1);
            p.xPortaFrente = Arred(xS - F(r, 1.4f, 2.4f) * e);
            p.Fora(sala, 0, p.xPortaFrente);
            p.Passagem(sala, coz, (z0 + zA) / 2);
            p.Porta(sala, cor, x0 + 1.4f * e);
            p.Porta(coz, cor, Mathf.Min(x1 - 1.4f * e, xS + 1.6f * e));
            p.Porta(cor, q1, xQ - 1.0f * e);
            p.Porta(cor, ban, xQ + lb / 2);
            p.Porta(cor, q2, xQ + lb + 1.0f * e);
            bool sorteio = r.NextDouble() < 0.5;
            if (porao ?? sorteio)
            {
                p.porao = true;
                p.poraoDeFora = 1;
                p.zPoraoDeFora = Arred((zC + z1) / 2);
                p.Comodo("porao", -1, x0, z0, x1, z1);
            }
            // o lugar do carro, do lado do corredor: a garagem (a porta do fim do corredor dá nela, ou ela não tem porta
            // para dentro) até um pouco depois do corredor, mas antes do fosso do porão; ou a vaga ao lado (aí a porta do
            // corredor sai no quintal)
            if (r.NextDouble() < 0.5)
            {
                bool dentro = r.NextDouble() < 0.6;
                float fim = Mathf.Max(z0 + 6.8f, zC + 0.3f);
                if (p.porao) fim = Mathf.Min(fim, p.zPoraoDeFora - Casa.LarguraDoFosso / 2 - 0.7f);
                p.garagem = NovaGaragem(r, m, p, 1, z0 + 0.4f, fim, dentro);
                if (dentro) p.Fora(cor, 1, (zA + zC) / 2, paraGaragem: true);
            }
            else
            {
                p.garagem = NovaVaga(1, z0);
                p.Fora(cor, 1, (zA + zC) / 2);
            }
            return p;
        }

        /// <summary>A largura da vaga do carro dentro da garagem (o carro tem até 1,9 m) e a do caminho em volta.</summary>
        const float VagaDoCarro = 2.6f;

        /// <summary>
        /// Uma garagem de um carro, do lado 'lado', de z0 a z1. Com a porta para dentro da casa, ela fica mais larga: o
        /// patamar e os degraus dessa porta (a casa com porão fica 0,75 m acima do chão) ficam do lado da casa, e o carro
        /// encostado na parede de fora.
        /// </summary>
        static Garagem NovaGaragem(System.Random r, Medidas m, Planta p, int lado, float z0, float z1, bool dentro)
        {
            float piso = p.porao ? 0.75f : 0.15f;
            float degraus = piso < 0.3f ? 1.0f : 1.0f + (Mathf.CeilToInt(piso / 0.19f - 0.05f) - 1) * 0.3f;
            return new Garagem
            {
                lado = lado, coberta = true, z0 = z0, z1 = z1, portaParaDentro = dentro, fechada = r.NextDouble() < 0.5,
                largura = dentro ? m.esp + VagaDoCarro + degraus + 0.4f : 4.0f,
            };
        }

        /// <summary>A vaga de concreto ao lado da casa, descoberta, da frente da casa para trás.</summary>
        static Garagem NovaVaga(int lado, float frente) => new Garagem { lado = lado, coberta = false, z0 = frente - 1.0f, z1 = frente + 4.6f, largura = 3.6f };

        /// <summary>
        /// Sobrado (10,6 a 12,6 × 10,6 a 12,2 m na escala 1,15): o hall com a escada de um lado (a porta da frente e a
        /// dos fundos), a sala na frente e a cozinha atrás; em cima, o corredor ao lado do vão da escada, dois quartos e
        /// o banheiro no fim do corredor. O porão (metade das casas, ou o que 'porao' pedir) desce por baixo da escada.
        /// </summary>
        public static Planta Sobrado(System.Random r, Medidas m, bool? porao = null)
        {
            float e = Esticar(m);
            var p = new Planta { molde = "Sobrado", W = Arred(F(r, 10.6f, 12.6f) * e), D = Arred(F(r, 10.6f, 12.2f) * e), andares = 2 };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, D = p.D;
            float xH = x1 - (m.larguraEscada + 1.6f * e);
            float zM = Arred(z0 + D * F(r, 0.48f, 0.55f));
            var sala = p.Comodo("sala", 0, x0, z0, xH, zM);
            var coz = p.Comodo("cozinha", 0, x0, zM, xH, z1);
            var hall = p.Comodo("corredor", 0, xH, z0, x1, z1);
            // a escada sobe para o fundo, encostada na parede da direita
            float ex1 = x1 - m.esp / 2, ex0 = ex1 - m.larguraEscada;
            float zs = z0 + 1.6f * e;
            float subida = m.altura + m.laje;
            float zTopo = zs + (Obra.Espelhos(subida) - 1) * m.pisada;
            p.lances.Add(new LanceDaPlanta { de = 0, x0 = ex0, x1 = ex1, zIni = zs, fechadoEmbaixo = true });
            p.xPortaFrente = xH + 1.0f * e;
            p.Fora(hall, 0, p.xPortaFrente);
            p.Fora(hall, 2, (xH + x1) / 2);
            p.Passagem(hall, sala, z0 + 2.4f * e);
            p.Porta(hall, coz, Mathf.Min(z1 - 0.9f * e, zTopo + 0.85f * e));
            p.Passagem(sala, coz, (x0 + xH) / 2);
            // em cima
            float zB = zTopo + 1.3f * e;
            float zM2 = Arred(z0 + D * F(r, 0.47f, 0.53f));
            var corr = p.Comodo("corredor", 1, xH, z0, x1, zB);
            var ban = p.Comodo("banheiro", 1, xH, zB, x1, z1);
            var q1 = p.Comodo("quarto", 1, x0, z0, xH, zM2);
            var q2 = p.Comodo("quarto", 1, x0, zM2, xH, z1);
            p.Porta(corr, q1, z0 + 1.3f * e);
            p.Porta(corr, q2, (zM2 + zB) / 2);
            p.Porta(corr, ban, xH + 0.9f * e);
            bool sorteio = r.NextDouble() < 0.5;
            if (porao ?? sorteio)
            {
                // o porão desce por baixo da escada, chegando atrás dela, no hall
                p.porao = true;
                p.Comodo("porao", -1, x0, z0, x1, z1);
                int nb = Obra.Espelhos(m.laje + m.alturaPorao);
                p.lances.Add(new LanceDaPlanta { de = -1, x0 = ex0, x1 = ex1, zIni = zTopo - (nb - 1) * m.pisada });
            }
            // o lugar do carro, do lado da sala e da cozinha (o outro é o da escada): a garagem, que vai até depois do
            // começo da cozinha (a porta para dentro dá nela), ou a vaga ao lado
            if (r.NextDouble() < 0.5)
            {
                bool dentro = r.NextDouble() < 0.6;
                float gz0 = z0 + Mathf.Max(0.4f, zM - z0 + 2.4f - 6.6f);
                p.garagem = NovaGaragem(r, m, p, -1, gz0, gz0 + 6.6f, dentro);
                if (dentro) p.Fora(coz, 3, zM + 1.2f * e, paraGaragem: true);
            }
            else p.garagem = NovaVaga(-1, z0);
            return p;
        }

        /// <summary>
        /// A Casa_1 da demo (o `CASAS[0]` do `cidade_casas.py`): sala na frente, cozinha atrás à esquerda e quarto atrás
        /// à direita, com as mesmas portas e as mesmas cores. Para comparar com o modelo do Blender.
        /// </summary>
        public static Planta Demo()
        {
            var p = new Planta { molde = "Demo", W = 10f, D = 8f, semente = 1 };
            float x0 = p.X0, x1 = p.X1, z0 = p.Z0, z1 = p.Z1, W = p.W, D = p.D;
            float zd = z0 + D * 0.55f;
            var sala = p.Comodo("sala", 0, x0, z0, x1, zd);
            var coz = p.Comodo("cozinha", 0, x0, zd, 0f, z1);
            var qua = p.Comodo("quarto", 0, 0f, zd, x1, z1);
            sala.papel = "PapelVerde"; sala.piso = "Taco";
            coz.papel = "Azulejo"; coz.piso = "ConcretoClaro";
            qua.papel = "ParedeInterna"; qua.piso = "Carpete";
            p.xPortaFrente = x0 + 0.3f * W;
            p.Fora(sala, 0, p.xPortaFrente);
            p.Fora(coz, 2, x0 + 0.3f * W);
            p.Porta(sala, coz, x0 + 0.25f * W);
            p.Porta(sala, qua, x0 + 0.75f * W);
            p.cor = "ParedeVerde"; p.escura = "ParedeVerdeEscura"; p.telhado = "Telhado"; p.telhadoEscuro = "TelhadoEscuro";
            p.varanda = true; p.chamine = -1; p.ar = 1; p.corDaPorta = "Madeira";
            p.janelas = new[] { 7, 2 }; p.sangue = 2;
            return p;
        }

        static readonly int[][] PesosDasJanelas = { new[] { 7, 2 }, new[] { 5, 5 }, new[] { 7, 1 }, new[] { 6, 3 }, new[] { 7, 1 } };

        /// <summary>A aparência: as cores, o telhado, a varanda, a chaminé, o ar-condicionado e os papéis e pisos.</summary>
        public static void Estilo(System.Random r, Planta p)
        {
            int c = r.Next(3);
            p.cor = new[] { "ParedeVerde", "ParedeBege", "ParedeAzul" }[c];
            p.escura = new[] { "ParedeVerdeEscura", "ParedeBegeEscura", "ParedeAzulEscura" }[c];
            bool azul = r.NextDouble() < 0.4;
            p.telhado = azul ? "TelhadoAzul" : "Telhado";
            p.telhadoEscuro = azul ? "TelhadoAzulEscuro" : "TelhadoEscuro";
            p.corDaPorta = Um(r, "Madeira", "Batente", "PersianaVinho", "PersianaVerde");
            p.varanda = r.NextDouble() < 0.65;
            p.varandaLarga = p.varanda && p.molde == "Terrea" && r.NextDouble() < 0.45;
            p.cumeeiraNoX = r.NextDouble() < (p.molde == "Sobrado" ? 0.6 : 0.45);
            p.espelhar = r.NextDouble() < 0.5;
            // por enquanto toda porta da frente fica aberta: fechar, trancar e arrombar é a Parte M7 (sem ela, a casa de
            // porta fechada não tinha como entrar)
            r.NextDouble();
            p.portaDaFrenteAberta = true;
            // a chaminé e o ar ficam do lado sem a garagem (ou a vaga) e sem o fosso do porão
            int ocupado = p.garagem != null ? p.garagem.lado : p.poraoDeFora;
            int ladoLivre = ocupado != 0 ? -ocupado : (r.NextDouble() < 0.5 ? 1 : -1);
            p.chamine = r.NextDouble() < 0.45 ? 0 : ladoLivre;
            p.ar = r.NextDouble() < 0.5 ? ladoLivre : 0;
            if (p.ar != 0 && p.ar == p.chamine) p.chamine = 0;
            p.janelas = PesosDasJanelas[r.Next(PesosDasJanelas.Length)];
            p.sangue = r.Next(0, 5);
            p.semente = r.Next();
            foreach (var k in p.comodos)
            {
                switch (k.tipo)
                {
                    case "sala": k.papel = Um(r, "PapelVerde", "PapelRosa", "ParedeInterna"); k.piso = Um(r, "Taco", "Carpete"); break;
                    case "quarto": k.papel = Um(r, "PapelVerde", "PapelRosa", "ParedeInterna"); k.piso = Um(r, "Taco", "Carpete", "Taco"); break;
                    case "cozinha": k.papel = "Azulejo"; k.piso = "ConcretoClaro"; break;
                    case "banheiro": k.papel = "Azulejo"; k.piso = "Azulejo"; break;
                    case "porao": k.papel = "Concreto"; k.piso = "Concreto"; break;
                    default: k.papel = "ParedeInterna"; k.piso = "Taco"; break;
                }
            }
        }
    }
}
