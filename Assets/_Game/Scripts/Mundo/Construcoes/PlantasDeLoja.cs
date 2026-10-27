using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Os tipos de loja (Plano_Mapa, Parte M6, terceira fatia). A conveniência é a loja do posto da estrada.</summary>
    public enum TipoDeLoja : byte { Comum, Mercado, Farmacia, VinteQuatroHoras, Armas, Conveniencia }

    /// <summary>
    /// A planta de uma loja (Plano_Mapa, Parte M6, terceira fatia): o salão na frente, com as portas e as vitrines para a
    /// rua, e os fundos atrás da linha <see cref="zs"/> (o depósito, o estoque, o banheiro; no mercado, o açougue e a doca;
    /// na loja de armas, o escritório e o depósito atrás da porta de aço). Com mais de um andar, a escada em U num lado dos
    /// fundos (a porta para o salão) e, em cada andar de cima, um apartamento: a sala (e o quarto) na frente, a cozinha e o
    /// banheiro atrás. Os balcões de alvenaria ficam no salão.
    /// Coordenadas: o meio no centro, x de -W/2 a W/2, z de -D/2 (a frente, para a rua) a D/2 (o fundo); as linhas das
    /// paredes passam nas bordas dos cômodos (como na Planta das casas). Espelhar espelha a malha em x no fim.
    /// </summary>
    public sealed class PlantaDeLoja
    {
        public TipoDeLoja tipo;
        public int semente;
        public float W, D;
        public int andares;
        public bool espelhar;
        /// <summary>A linha entre o salão (na frente) e os fundos.</summary>
        public float zs;
        /// <summary>A escada (com mais de um andar): de ex0 a ex1, de zs até o fundo.</summary>
        public float ex0, ex1;
        /// <summary>As cores: a fachada, as faixas (a borda das lajes), a placa e o toldo (null: sem toldo).</summary>
        public string cor, faixa, placa, toldo;
        public float peDireitoDoTerreo, peDireito, laje;
        public readonly List<Comodo> comodos = new List<Comodo>();
        public readonly List<Ligacao> ligacoes = new List<Ligacao>();
        /// <summary>Os balcões de alvenaria do térreo (x, z): o da farmácia, o da loja de armas, o caixa da 24 horas e os do mercado.</summary>
        public readonly List<Rect> balcoes = new List<Rect>();

        /// <summary>O piso do térreo acima do chão (um pouco acima da calçada, que fica a 0,2 m).</summary>
        public const float Piso = 0.25f, Platibanda = 1.0f;
        /// <summary>A escada em U (entre as linhas das paredes, dois lances de 1,2 m) e o patamar de cada andar, junto do salão.</summary>
        public const float LarguraDaEscada = 2.8f, Patamar = 1.3f;
        /// <summary>O fundo dos fundos com a escada: os lances do térreo (4,8 m de subida) e o patamar do meio.</summary>
        public const float FundoDaEscada = 6.2f;
        /// <summary>A largura do banheiro dos fundos.</summary>
        public const float Banheiro = 2.2f;

        public float X0 => -W / 2; public float X1 => W / 2; public float Z0 => -D / 2; public float Z1 => D / 2;
        public bool TemEscada => andares > 1;

        /// <summary>A altura do piso do andar f (o topo da laje); a do andar 'andares' é a cobertura.</summary>
        public float Base(int f) => Piso + (f <= 0 ? 0f : peDireitoDoTerreo + laje + (f - 1) * (peDireito + laje));
        public float PeDireitoDe(int f) => f == 0 ? peDireitoDoTerreo : peDireito;
        public float Cobertura => Base(andares);
        /// <summary>A altura da caixa de longe: do piso do térreo ao topo da platibanda.</summary>
        public float AlturaDaCaixa => Cobertura + Platibanda - Piso;

        /// <summary>O andar de uma altura (nas coordenadas da loja): de 0 a 'andares' (a cobertura).</summary>
        public int AndarEm(float y)
        {
            if (andares <= 1 || y < Base(1) - 0.05f) return y > Cobertura - 0.05f ? andares : 0;
            return Mathf.Clamp(1 + Mathf.FloorToInt((y - Base(1) + 0.05f) / (peDireito + laje)), 1, andares);
        }

        /// <summary>O vão da escada nas lajes de cima (fora o patamar de cada andar, junto do salão).</summary>
        public Rect VaoDaEscada => Rect.MinMaxRect(ex0 + 0.1f, zs + 0.1f + Patamar, ex1 - 0.1f, Z1 - 0.1f);

        /// <summary>O salão (o primeiro cômodo do térreo).</summary>
        public Comodo Salao => comodos[0];

        public List<Comodo> ComodosDo(int f) => comodos.FindAll(k => k.andar == f);

        /// <summary>O nome da loja para o relatório e para o objeto na cena.</summary>
        public static string Nome(TipoDeLoja t) => t switch
        {
            TipoDeLoja.Mercado => "Mercado",
            TipoDeLoja.Farmacia => "Farmácia",
            TipoDeLoja.VinteQuatroHoras => "Loja 24 horas",
            TipoDeLoja.Armas => "Loja de armas",
            TipoDeLoja.Conveniencia => "Loja de conveniência",
            _ => "Loja",
        };
    }

    /// <summary>
    /// Os moldes das lojas: o salão do tamanho que sobra na frente; os fundos de 4 a 7 m (com a escada, os 6,2 m dela),
    /// divididos pelo tipo; os andares de cima com um apartamento cada. Os balcões deixam ao menos 1,1 m de passagem.
    /// </summary>
    public static class PlantasDeLoja
    {
        static float F(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static float Arred(float v) => Mathf.Round(v * 10f) / 10f;
        static T Um<T>(System.Random r, params T[] o) => o[r.Next(o.Length)];

        static readonly string[] Papeis = { "PapelVerde", "PapelRosa", "ParedeBege", "ParedeAzul", "ParedeVerde", "ParedeInterna", "ParedeBegeEscura" };
        static readonly string[] Pisos = { "Taco", "Carpete", "Taco", "Piso" };

        /// <summary>
        /// A loja de um lote do comércio: até 'largura' de frente e 'fundo' de fundo, com 'andares' andares (o mercado tem
        /// um só, alto) e a fachada na cor dada. Com o fundo curto para a escada, ela fica de um andar.
        /// </summary>
        public static PlantaDeLoja ParaOLote(System.Random r, Medidas m, TipoDeLoja tipo, float largura, float fundo, int andares, string cor)
        {
            var p = new PlantaDeLoja { tipo = tipo, semente = r.Next(), cor = cor, laje = m.laje, peDireito = m.altura };
            p.W = Arred(Mathf.Max(largura, 8f));
            p.D = Arred(Mathf.Max(fundo, 10f));
            bool deUmAndar = tipo == TipoDeLoja.Mercado || tipo == TipoDeLoja.Conveniencia || p.D < PlantaDeLoja.FundoDaEscada + 5f || p.W < 9f;
            p.andares = deUmAndar ? 1 : Mathf.Clamp(andares, 1, 4);
            p.peDireitoDoTerreo = tipo == TipoDeLoja.Mercado ? 5.5f : tipo == TipoDeLoja.Conveniencia ? 3.8f : 4.5f;
            p.espelhar = r.NextDouble() < 0.5;
            p.faixa = Um(r, "Concreto", "ConcretoClaro");
            p.placa = tipo switch
            {
                TipoDeLoja.Mercado => Um(r, "PlacaVermelha", "Laranja", "PlacaAmarela"),
                TipoDeLoja.Farmacia => Um(r, "PlacaVerde", "PlacaBranca"),
                TipoDeLoja.VinteQuatroHoras or TipoDeLoja.Conveniencia => Um(r, "PlacaVermelha", "Laranja", "CarroAzul"),
                TipoDeLoja.Armas => Um(r, "Metal", "PlacaVermelha"),
                _ => Um(r, "PlacaVermelha", "PlacaVerde", "PlacaAmarela", "PlacaBranca", "Laranja", "CarroAzul"),
            };
            p.toldo = tipo == TipoDeLoja.Mercado || tipo == TipoDeLoja.Armas || tipo == TipoDeLoja.Conveniencia || r.NextDouble() < 0.3
                ? null : Um(r, "PlacaVermelha", "PlacaVerde", "Laranja", "PlacaAmarela", "CarroAzul", "PersianaVinho", "PersianaVerde");

            // os fundos: com a escada, o fundo dela; senão, pelo tipo (o salão fica com ao menos 5 m)
            float fundos = p.TemEscada ? PlantaDeLoja.FundoDaEscada + (p.D > 20f ? F(r, 0f, 1.2f) : 0f)
                         : tipo == TipoDeLoja.Mercado ? F(r, 5.5f, 7f) : tipo == TipoDeLoja.Conveniencia ? 3.4f : F(r, 4f, 5.5f);
            fundos = Mathf.Min(fundos, p.D - 5f);
            p.zs = Arred(p.Z1 - fundos);
            // a faixa livre dos fundos (fora a escada, num dos lados)
            float la = p.X0, lb = p.X1;
            if (p.TemEscada)
            {
                bool naEsquerda = r.NextDouble() < 0.5;
                p.ex0 = naEsquerda ? p.X0 : p.X1 - PlantaDeLoja.LarguraDaEscada;
                p.ex1 = p.ex0 + PlantaDeLoja.LarguraDaEscada;
                if (naEsquerda) la = p.ex1; else lb = p.ex0;
            }
            Terreo(p, r, la, lb);
            for (int f = 1; f < p.andares; f++) Apartamento(p, r, f, la, lb);
            return p;
        }

        /// <summary>A loja de conveniência do posto (15 × 9 m, de frente para as bombas).</summary>
        public static PlantaDeLoja DoPosto(System.Random r, Medidas m) =>
            ParaOLote(r, m, TipoDeLoja.Conveniencia, 15f, 9f, 1, "ParedeBege");

        static Comodo Novo(PlantaDeLoja p, string tipo, int andar, float x0, float z0, float x1, float z1, string papel, string piso)
        {
            var c = new Comodo { tipo = tipo, andar = andar, x0 = x0, z0 = z0, x1 = x1, z1 = z1, papel = papel, piso = piso };
            p.comodos.Add(c);
            return c;
        }

        static void Porta(PlantaDeLoja p, Comodo a, Comodo b, float pos, bool passagem = false, float largura = 0f, bool deAco = false) =>
            p.ligacoes.Add(new Ligacao { a = a, b = b, pos = pos, passagem = passagem, largura = largura, deAco = deAco });

        static void Fora(PlantaDeLoja p, Comodo a, int lado, float pos, float largura = 0f) =>
            p.ligacoes.Add(new Ligacao { a = a, lado = lado, pos = pos, largura = largura });

        // o térreo: o salão, as portas da frente, a escada, os fundos pelo tipo e os balcões
        static void Terreo(PlantaDeLoja p, System.Random r, float la, float lb)
        {
            string papel = p.tipo switch
            {
                TipoDeLoja.Mercado or TipoDeLoja.Farmacia => Um(r, "ParedeInterna", "Azulejo"),
                TipoDeLoja.Armas => Um(r, "ParedeVerdeEscura", "TijoloEscuro", "ParedeBegeEscura"),
                _ => Um(r, "ParedeInterna", "ParedeBege", "ParedeAzul", "ParedeVerde", "Tijolo"),
            };
            string piso = p.tipo == TipoDeLoja.Armas ? "Concreto" : Um(r, "Piso", "Piso", "Azulejo");
            var salao = Novo(p, "salao", 0, p.X0, p.Z0, p.X1, p.zs, papel, piso);

            // as portas da frente: duas no mercado (a entrada e a saída), uma nas outras
            if (p.tipo == TipoDeLoja.Mercado)
            {
                Fora(p, salao, 0, Arred(p.X0 + p.W * 0.2f), 2.4f);
                Fora(p, salao, 0, Arred(p.X1 - p.W * 0.2f), 2.4f);
            }
            else Fora(p, salao, 0, Arred(Mathf.Clamp(F(r, -0.25f, 0.25f) * p.W, p.X0 + 1.8f, p.X1 - 1.8f)), 2.0f);

            if (p.TemEscada)
            {
                var esc = Novo(p, "escada", 0, p.ex0, p.zs, p.ex1, p.Z1, "ParedeInterna", "Concreto");
                Porta(p, salao, esc, (p.ex0 + p.ex1) / 2);
            }

            // os fundos, de la a lb (a escada fica de fora), divididos pelo tipo
            float L = lb - la, zm = (p.zs + p.Z1) / 2;
            Comodo Fundo(string tipo, float a, float b, string pa, string pi) => Novo(p, tipo, 0, a, p.zs, b, p.Z1, pa, pi);
            switch (p.tipo)
            {
                case TipoDeLoja.Mercado:
                {
                    // o açougue, o depósito com a doca para os fundos, o escritório e o banheiro
                    float xa = Arred(la + Mathf.Clamp(L * 0.25f, 3.5f, 4.5f)), xc = lb - PlantaDeLoja.Banheiro, xb = Arred(xc - 3f);
                    bool escritorio = xb - xa >= 4.5f;
                    if (!escritorio) xb = xc;
                    var acougue = Fundo("acougue", la, xa, "Azulejo", "Azulejo");
                    var deposito = Fundo("deposito", xa, xb, "Concreto", "Concreto");
                    var banheiro = Fundo("banheiro", xc, lb, "Azulejo", "Azulejo");
                    Porta(p, salao, acougue, (la + xa) / 2);
                    Porta(p, salao, deposito, (xa + xb) / 2, largura: 2.4f);
                    if (escritorio)
                    {
                        var esc = Fundo("escritorio", xb, xc, "ParedeBege", "Piso");
                        Porta(p, deposito, esc, zm);
                        Porta(p, esc, banheiro, zm);
                    }
                    else Porta(p, deposito, banheiro, zm);
                    Fora(p, deposito, 2, (xa + xb) / 2, Mathf.Min(3.2f, xb - xa - 0.8f));
                    break;
                }
                case TipoDeLoja.Armas:
                {
                    // o banheiro, o escritório e, atrás da porta de aço, o depósito (estreito, sem o banheiro)
                    bool comBanheiro = L >= 7.5f;
                    float xa = comBanheiro ? la + PlantaDeLoja.Banheiro - 0.2f : la, xb = Arred(xa + Mathf.Clamp(L * 0.35f, 2.6f, 3.6f));
                    var escritorio = Fundo("escritorio", xa, xb, "ParedeBegeEscura", "Taco");
                    var deposito = Fundo("deposito", xb, lb, "Concreto", "Concreto");
                    Porta(p, salao, escritorio, (xa + xb) / 2);
                    if (comBanheiro) Porta(p, escritorio, Fundo("banheiro", la, xa, "Azulejo", "Azulejo"), zm);
                    Porta(p, escritorio, deposito, zm, deAco: true);
                    break;
                }
                case TipoDeLoja.Farmacia:
                case TipoDeLoja.VinteQuatroHoras:
                case TipoDeLoja.Conveniencia:
                case TipoDeLoja.Comum:
                default:
                {
                    // o estoque (o depósito) e o banheiro; às vezes, a porta dos fundos
                    bool comBanheiro = L >= 6f;
                    float xa = comBanheiro ? lb - PlantaDeLoja.Banheiro : lb;
                    var estoque = Fundo(p.tipo == TipoDeLoja.Comum ? "deposito" : "estoque", la, xa, "Concreto", "Concreto");
                    Porta(p, salao, estoque, Arred((la + xa) / 2));
                    if (comBanheiro) Porta(p, estoque, Fundo("banheiro", xa, lb, "Azulejo", "Azulejo"), zm);
                    if (r.NextDouble() < 0.5) Fora(p, estoque, 2, Arred((la + xa) / 2));
                    break;
                }
            }
            Balcoes(p, r);
        }

        // os balcões de alvenaria: o da farmácia e o da loja de armas, de um lado ao outro do salão (com a passagem numa
        // ponta); o caixa da 24 horas e da conveniência, ao lado da porta; os caixas do mercado, perto das portas
        static void Balcoes(PlantaDeLoja p, System.Random r)
        {
            float fundoDoSalao = p.zs - p.Z0;
            switch (p.tipo)
            {
                case TipoDeLoja.Farmacia:
                case TipoDeLoja.Armas:
                {
                    // atrás do balcão, uma faixa de ~2 m até a parede dos fundos (as portas dos fundos dão nela)
                    float atras = Mathf.Min(p.tipo == TipoDeLoja.Armas ? 2.4f : 2.1f, fundoDoSalao * 0.4f);
                    float z1 = p.zs - atras, z0 = z1 - 0.7f;
                    bool passagemNaEsquerda = r.NextDouble() < 0.5;
                    float a = passagemNaEsquerda ? p.X0 + 1.4f : p.X0 + 0.1f, b = passagemNaEsquerda ? p.X1 - 0.1f : p.X1 - 1.4f;
                    p.balcoes.Add(Rect.MinMaxRect(a, z0, b, z1));
                    break;
                }
                case TipoDeLoja.VinteQuatroHoras:
                case TipoDeLoja.Conveniencia:
                {
                    float xp = p.ligacoes.Find(l => l.b == null && l.lado == 0).pos;
                    float lado = xp + 2.2f + 0.7f < p.X1 - 0.6f ? 1f : -1f;
                    float a = xp + lado * 1.9f, b = a + lado * 0.7f;
                    float z0 = p.Z0 + 1.0f, z1 = Mathf.Min(p.Z0 + 3.4f, p.zs - 1.6f);
                    if (z1 - z0 > 1f) p.balcoes.Add(Rect.MinMaxRect(Mathf.Min(a, b), z0, Mathf.Max(a, b), z1));
                    break;
                }
                case TipoDeLoja.Mercado:
                {
                    // os caixas: na faixa entre as duas portas, de 2,6 em 2,6 m (sobram ao menos 1,8 m entre eles); num
                    // salão raso, nenhum (atrás deles, a passagem até as portas dos fundos ficava estreita)
                    float xa = p.X0 + p.W * 0.2f + 2.2f, xb = p.X1 - p.W * 0.2f - 2.2f;
                    int n = fundoDoSalao < 7.5f || xb < xa ? 0 : Mathf.Clamp(Mathf.FloorToInt((xb - xa) / 2.6f) + 1, 0, 5);
                    float z0 = p.Z0 + 2.4f, z1 = z0 + 1.8f;
                    for (int i = 0; i < n; i++)
                    {
                        float x = n == 1 ? (xa + xb) / 2 : Mathf.Lerp(xa, xb, i / (float)(n - 1));
                        p.balcoes.Add(Rect.MinMaxRect(x - 0.4f, z0, x + 0.4f, z1));
                    }
                    break;
                }
            }
        }

        // um andar de cima: o apartamento (a sala no lado da escada e o quarto na frente; a cozinha e o banheiro atrás)
        static void Apartamento(PlantaDeLoja p, System.Random r, int f, float la, float lb)
        {
            bool escadaNaEsquerda = p.ex0 <= p.X0 + 0.01f;
            string papel = Um(r, Papeis), papelDoQuarto = Um(r, Papeis), piso = Um(r, Pisos);
            var esc = Novo(p, "escada", f, p.ex0, p.zs, p.ex1, p.Z1, "ParedeInterna", "Concreto");
            Comodo sala;
            if (p.W >= 10f)
            {
                float xq = Arred(escadaNaEsquerda ? p.X0 + p.W * F(r, 0.52f, 0.62f) : p.X1 - p.W * F(r, 0.52f, 0.62f));
                sala = escadaNaEsquerda ? Novo(p, "sala", f, p.X0, p.Z0, xq, p.zs, papel, piso) : Novo(p, "sala", f, xq, p.Z0, p.X1, p.zs, papel, piso);
                var quarto = escadaNaEsquerda ? Novo(p, "quarto", f, xq, p.Z0, p.X1, p.zs, papelDoQuarto, piso) : Novo(p, "quarto", f, p.X0, p.Z0, xq, p.zs, papelDoQuarto, piso);
                Porta(p, sala, quarto, (p.Z0 + p.zs) / 2);
            }
            else sala = Novo(p, "sala", f, p.X0, p.Z0, p.X1, p.zs, papel, piso);
            Porta(p, sala, esc, (p.ex0 + p.ex1) / 2);
            // a cozinha encosta na escada; o banheiro fica na outra ponta
            float xb = escadaNaEsquerda ? lb - PlantaDeLoja.Banheiro : la + PlantaDeLoja.Banheiro;
            var cozinha = escadaNaEsquerda ? Novo(p, "cozinha", f, la, p.zs, xb, p.Z1, "Azulejo", "Piso") : Novo(p, "cozinha", f, xb, p.zs, lb, p.Z1, "Azulejo", "Piso");
            var banheiro = escadaNaEsquerda ? Novo(p, "banheiro", f, xb, p.zs, lb, p.Z1, "Azulejo", "Azulejo") : Novo(p, "banheiro", f, la, p.zs, xb, p.Z1, "Azulejo", "Azulejo");
            float sa = Mathf.Max(sala.x0, cozinha.x0), sb = Mathf.Min(sala.x1, cozinha.x1);
            Porta(p, sala, cozinha, Arred((sa + sb) / 2), passagem: true);
            Porta(p, cozinha, banheiro, (p.zs + p.Z1) / 2);
        }
    }
}
