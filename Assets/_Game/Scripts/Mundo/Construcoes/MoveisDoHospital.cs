using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os móveis do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 4): a mobília nova, o lote do bunker e o
    /// lote do hospital (o <c>mobilia_hospital.py</c>), pelo nome do modelo, em ×1,30 (<see cref="MedidasDaMobilia.Escala"/>).
    /// O hospital tem 85 cômodos abertos de uns 30 tipos: cada tipo tem a sua receita (<see cref="Receita"/>), que encosta os
    /// móveis nas paredes livres (<see cref="Sala.Parede"/>: primeiro a do fundo, a do lado oposto à porta, depois as dos lados
    /// e por último a da porta) ou põe no meio (as macas da sala vermelha, a mesa cirúrgica, as ilhas do laboratório, as
    /// fileiras da espera, as estantes do arquivo, as baias dos escritórios), pela semente do andar e do cômodo.
    /// Nenhum móvel fica:
    ///  - no giro de uma porta (a faixa da largura dela mais 35 cm de cada lado, da parede até a ponta da folha aberta), nem
    ///    na frente de um vão aberto (o saguão, a espera e os corredores: 1 m para cada lado da linha);
    ///  - na frente de uma janela da fachada, se for alto (mais de 1,25 m no jogo: os armários, as estantes, as máquinas;
    ///    as camas e as macas passam, a haste do soro é fina);
    ///  - em cima de outro (as caixas de cada um, com 4 cm de folga), menos o que vai em cima ou junto de propósito (o
    ///    computador na mesa, o microscópio na bancada, a cadeira na frente da mesa, a privada dentro da cabine).
    /// O giro é o da Unity: 0 olha para +z (as costas numa parede de z menor), 180 para -z, 90 para +x, -90 para -x.
    /// As caixas de colisão de cada andar entram na malha de colisão dele (<see cref="Caixas"/>), como as do bunker.
    /// </summary>
    public static class MoveisDoHospital
    {
        public struct Movel
        {
            public int andar;
            public string modelo;
            public Vector3 lugar;   // nas coordenadas da obra (já com o chão do andar)
            public float giro, escala;
            public bool caixa;
        }

        /// <summary>Os que não têm caixa de colisão (em cima de outro, na parede ou finos).</summary>
        static readonly HashSet<string> SemCaixa = new HashSet<string>
        {
            "Computador", "Microscopio", "Centrifuga", "TapetePersa", "TapeteRedondo", "QuadroPaisagem", "QuadroBranco",
            "QuadroDeAvisos", "TVDeParede", "FocoCirurgico", "Mictorio",
        };

        /// <summary>Os altos que podem ficar na frente de uma janela (a parte alta é fina: a haste do soro, o monitor no pé).</summary>
        static readonly HashSet<string> PassaNaJanela = new HashSet<string>
        {
            "CamaHospital", "MacaEmergencia", "SuporteDeSoro", "MonitorSinais", "Respirador", "CadeiraDeColeta", "MesaDiretor",
            "Biombo", "BancoDeEspera3", "BancoDeEspera4",
        };

        /// <summary>A altura no jogo acima da qual um móvel tapa a janela (o peitoril é de 0,9 m).</summary>
        const float AltoParaAJanela = 1.25f;

        /// <summary>
        /// Os que têm as caixas de colisão deles (no espaço do modelo na Unity, no tamanho real: o centro e o tamanho), e não
        /// a do tamanho inteiro: a cabine do banheiro (as divisórias e os montantes da frente; a porta tem a caixa dela, que
        /// gira com ela) e a baia do escritório (as três divisórias e a mesa em L), por onde se entra (07/11/2026, a pedido:
        /// "não dá para entrar no box do banheiro", "nas mesas da sala office não dá para chegar perto das cadeiras").
        /// </summary>
        static readonly Dictionary<string, (Vector3 centro, Vector3 tamanho)[]> CaixasProprias = new Dictionary<string, (Vector3, Vector3)[]>
        {
            ["CabineDeBanheiro"] = new[]
            {
                (new Vector3(0.4625f, 0.925f, 0f), new Vector3(0.05f, 1.85f, 1.51f)),
                (new Vector3(-0.4625f, 0.925f, 0f), new Vector3(0.05f, 1.85f, 1.51f)),
                (new Vector3(0.42f, 0.925f, 0.7625f), new Vector3(0.11f, 1.85f, 0.05f)),
                (new Vector3(-0.42f, 0.925f, 0.7625f), new Vector3(0.11f, 1.85f, 0.05f)),
            },
            // sem a divisória do lado de +x (o da esquerda de quem olha): é a da direita da cabine de antes
            ["CabineDeBanheiroLado"] = new[]
            {
                (new Vector3(-0.4625f, 0.925f, 0f), new Vector3(0.05f, 1.85f, 1.51f)),
                (new Vector3(0.42f, 0.925f, 0.7625f), new Vector3(0.11f, 1.85f, 0.05f)),
                (new Vector3(-0.42f, 0.925f, 0.7625f), new Vector3(0.11f, 1.85f, 0.05f)),
            },
            // a divisória de trás, a comprida do lado de +x e a curta do lado de -x; a mesa de trás e a do lado de +x (o
            // vão da cadeira fica na frente, do lado de -x)
            ["EstacaoDeTrabalho"] = new[]
            {
                (new Vector3(0f, 0.7f, -0.775f), new Vector3(1.6f, 1.4f, 0.05f)),
                (new Vector3(0.775f, 0.7f, 0.025f), new Vector3(0.05f, 1.4f, 1.55f)),
                (new Vector3(-0.775f, 0.7f, -0.45f), new Vector3(0.05f, 1.4f, 0.6f)),
                (new Vector3(-0.025f, 0.375f, -0.40f), new Vector3(1.5f, 0.75f, 0.70f)),
                (new Vector3(0.425f, 0.375f, 0.325f), new Vector3(0.65f, 0.75f, 0.75f)),
            },
        };

        /// <summary>
        /// Os que têm portas de dobradiça na frente (abrem 100°: a ponta passa um pouco do lado do móvel). Encostados numa
        /// parede, eles ficam a <see cref="FolgaDasPortas"/> das paredes do lado, senão a porta aberta entra nelas (07/11/2026,
        /// a pedido: "as portas desse armário estão entrando na parede quando perto de uma").
        /// </summary>
        static readonly HashSet<string> ComPortas = new HashSet<string>
        {
            "ArmarioCanto", "ArmarioChao40", "ArmarioChao60", "ArmarioChao80", "ArmarioDeAco", "ArmarioDeRemedios",
            "ArmarioParede40", "ArmarioParede60", "ArmarioParede80", "BancadaLaboratorio", "Box", "CriadoHospitalar",
            "EspelhoArmario", "Geladeira", "GeladeiraGrande", "GeladeiraLaboratorio", "GuardaRoupa2", "GuardaRoupa3",
            "GuardaRoupa4", "Ilha", "MacaExame", "MaquinaDeLavar", "Pia", "PiaDupla", "PiaGabinete", "RackArmario",
            "RackEstante", "Secadora", "Vestiario",
        };

        /// <summary>A folga de um móvel com portas até as paredes do lado (no jogo).</summary>
        const float FolgaDasPortas = 0.22f;

        static readonly Dictionary<int, List<Movel>> _andares = new Dictionary<int, List<Movel>>();
        static readonly object _trava = new object();

        /// <summary>Os modelos que as receitas pediram e que a mobília não tem (sem as medidas: o FBX não foi montado).</summary>
        public static readonly HashSet<string> Faltam = new HashSet<string>();

        /// <summary>Os móveis do andar f (montados pelas receitas na primeira vez; a mesma semente dá os mesmos).</summary>
        public static List<Movel> DoAndar(int f, int semente = 1)
        {
            int chave = f * 100000 + semente;
            lock (_trava)
            {
                if (_andares.TryGetValue(chave, out var pronto)) return pronto;
                var lista = new List<Movel>();
                var an = PlantaDoHospital.DoAndar(f);
                var divisas = new List<Divisa>();
                var bordas = new List<Borda>();
                Linhas(an, divisas, bordas);
                for (int i = 0; i < an.comodos.Count; i++)
                {
                    var k = an.comodos[i];
                    if (DoNucleo(k)) continue;
                    var s = new Sala(an, k, bordas, lista, semente * 7919 + f * 131 + i * 17);
                    Receita(s);
                }
                _andares[chave] = lista;
                return lista;
            }
        }

        /// <summary>As caixas de colisão dos móveis do andar f na malha de colisão dele (o tamanho menos o recuo de cada lado,
        /// em camadas de até 0,85 m nos altos), como as do bunker.</summary>
        public static void Caixas(int f, MalhaDeObra colisao, int semente = 1)
        {
            foreach (var m in DoAndar(f, semente))
            {
                if (!m.caixa || !MedidasDaMobilia.Tem(m.modelo)) continue;
                var giro = Quaternion.Euler(0f, m.giro, 0f);
                if (CaixasProprias.TryGetValue(m.modelo, out var proprias))
                {
                    foreach (var (centro, tamanho) in proprias)
                        colisao.Caixa(Matrix4x4.TRS(m.lugar + giro * (centro * m.escala), giro, Vector3.one), tamanho * m.escala, "Concreto");
                    continue;
                }
                Vector3 t = MedidasDaMobilia.Real(m.modelo) * m.escala;
                Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                float baixo = MedidasDaMobilia.Baixo(m.modelo) * m.escala;
                Vector3 c = m.lugar + giro * new Vector3(meio.x, 0f, meio.y);
                var tam = new Vector3(Mathf.Max(0.1f, t.x - 2f * Mobilia.Recuo), t.y, Mathf.Max(0.1f, t.z - 2f * Mobilia.Recuo));
                int camadas = t.y > 1.7f ? Mathf.CeilToInt(t.y / 0.85f) : 1;
                float h = t.y / camadas;
                for (int k = 0; k < camadas; k++)
                    colisao.Caixa(Matrix4x4.TRS(c + Vector3.up * (baixo + h * (k + 0.5f)), giro, Vector3.one), new Vector3(tam.x, h, tam.z), "Concreto");
            }
        }

        /// <summary>A caixa no chão de um modelo em 'lugar' com o giro (a do tamanho inteiro, em x e z da obra).</summary>
        public static Rect Pegada(string m, Vector3 lugar, float giro, float escala)
        {
            Vector3 t = MedidasDaMobilia.Real(m) * escala;
            Vector2 meio = MedidasDaMobilia.Meio(m) * escala;
            var q = Quaternion.Euler(0f, giro, 0f);
            Vector3 c = lugar + q * new Vector3(meio.x, 0f, meio.y);
            Vector3 lado = q * new Vector3(t.x / 2f, 0f, 0f), fundo = q * new Vector3(0f, 0f, t.z / 2f);
            float ex = Mathf.Abs(lado.x) + Mathf.Abs(fundo.x), ez = Mathf.Abs(lado.z) + Mathf.Abs(fundo.z);
            return Rect.MinMaxRect(c.x - ex, c.z - ez, c.x + ex, c.z + ez);
        }

        // ------------------------------------------------------------ a sala (o cômodo, as zonas livres e o que já entrou)

        /// <summary>
        /// Um cômodo sendo mobiliado: as faces das paredes (10 cm para dentro da linha), as zonas que ficam livres (o giro das
        /// portas e a frente dos vãos abertos), as janelas de fora de cada lado e as caixas do que já entrou. Os lados: 0 a
        /// frente (z0), 1 o leste (x1), 2 o fundo (z1), 3 o oeste (x0).
        /// </summary>
        sealed class Sala
        {
            public readonly Andar an;
            public readonly Comodo k;
            public readonly int f;
            public readonly System.Random rnd;
            readonly List<Movel> saida;
            readonly List<Rect> zonas = new List<Rect>();
            readonly List<(int lado, float de, float ate)> janelas = new List<(int, float, float)>();
            readonly List<Rect> ocupados = new List<Rect>();
            readonly int[] portasNoLado = new int[4];
            const float E = 0.1f;

            public float X0 => k.x0 + E;
            public float X1 => k.x1 - E;
            public float Z0 => k.z0 + E;
            public float Z1 => k.z1 - E;
            public float W => X1 - X0;
            public float D => Z1 - Z0;
            public string Nome => an.nomes[k];

            public Sala(Andar an, Comodo k, List<Borda> bordas, List<Movel> saida, int semente)
            {
                this.an = an;
                this.k = k;
                f = an.f;
                this.saida = saida;
                rnd = new System.Random(semente);
                Portas();
                Janelas(bordas);
            }

            // o giro das portas e a frente dos vãos abertos, deste cômodo
            void Portas()
            {
                foreach (var p in an.portas)
                {
                    if (p.a != k && p.b != k) continue;
                    if (p.b == null)
                    {
                        // para fora: o lado dela
                        float w0 = LarguraDa(p);
                        Zona(p.lado, p.pos, w0, ProfundidadeDa(p, w0), 1);
                        continue;
                    }
                    var o = p.a == k ? p.b : p.a;
                    bool vertical = Mathf.Abs(k.x1 - o.x0) < 0.01f || Mathf.Abs(o.x1 - k.x0) < 0.01f;
                    int lado = vertical ? (Mathf.Abs(k.x1 - o.x0) < 0.01f ? 1 : 3) : (Mathf.Abs(k.z1 - o.z0) < 0.01f ? 2 : 0);
                    if (p.aberto)
                    {
                        // a linha toda que é aberta para o vizinho
                        float lo = vertical ? Mathf.Max(k.z0, o.z0) : Mathf.Max(k.x0, o.x0);
                        float hi = vertical ? Mathf.Min(k.z1, o.z1) : Mathf.Min(k.x1, o.x1);
                        Zona(lado, (lo + hi) / 2f, hi - lo, 1.0f, 2);
                        continue;
                    }
                    float w = LarguraDa(p);
                    // a porta para a circulação (o corredor, o saguão) é a de entrar: vale mais na escolha do fundo
                    Zona(lado, p.pos, w, ProfundidadeDa(p, w), an.zonas.TryGetValue(o, out var zo) && zo == PlantaDoHospital.Zona.Circulacao ? 2 : 1);
                }
            }

            static float LarguraDa(Porta p) => p.largura > 0f ? p.largura : p.folhas == 2 ? 2.0f : p.folhas == 0 ? 2.07f : 1.265f;
            static float ProfundidadeDa(Porta p, float w) => p.folhas == 0 ? 0.9f : (p.folhas == 2 ? w / 2f : w) + 0.45f;

            void Zona(int lado, float pos, float largura, float fundo, int peso)
            {
                portasNoLado[lado] += peso;
                float de = pos - largura / 2f - 0.35f, ate = pos + largura / 2f + 0.35f;
                float c = Face(lado);
                zonas.Add(lado switch
                {
                    0 => Rect.MinMaxRect(de, c - 0.2f, ate, c + fundo),
                    2 => Rect.MinMaxRect(de, c - fundo, ate, c + 0.2f),
                    3 => Rect.MinMaxRect(c - 0.2f, de, c + fundo, ate),
                    _ => Rect.MinMaxRect(c - fundo, de, c + 0.2f, ate),
                });
            }

            // as janelas de fora (a grade de 4 m da fachada, como as do Hospital: a que cabe inteira longe das portas)
            void Janelas(List<Borda> bordas)
            {
                if (f == 0 || SemJanela.Contains(k.tipo) || k.tipo == "entrada" || k.tipo == "banheiro") return;
                const float hw = LarguraDaJanela / 2f;
                foreach (var b in bordas)
                {
                    if (b.k != k) continue;
                    int lado = b.vertical ? (b.fora > 0 ? 1 : 3) : (b.fora > 0 ? 2 : 0);
                    float origem = b.vertical ? PlantaDoHospital.Z0 : PlantaDoHospital.X0, fim = b.vertical ? PlantaDoHospital.Z1 : PlantaDoHospital.X1;
                    for (float centro = origem + Modulo / 2f; centro < fim; centro += Modulo)
                    {
                        if (centro - hw < b.lo + 0.35f || centro + hw > b.hi - 0.35f) continue;
                        janelas.Add((lado, centro - hw - 0.15f, centro + hw + 0.15f));
                    }
                }
            }

            /// <summary>O lado da primeira porta (ou vão) do cômodo, e o oposto a ele (o "fundo" da sala).</summary>
            public int LadoDaPorta
            {
                get
                {
                    int melhor = 0;
                    for (int l = 1; l < 4; l++) if (portasNoLado[l] > portasNoLado[melhor]) melhor = l;
                    return melhor;
                }
            }

            public int Fundo => (LadoDaPorta + 2) % 4;

            /// <summary>Os lados na ordem de encostar: o fundo, os dois lados (o de menos portas primeiro) e o da porta.</summary>
            public int[] Ordem()
            {
                int fu = Fundo, a = (fu + 1) % 4, b = (fu + 3) % 4;
                if (portasNoLado[b] < portasNoLado[a] || (portasNoLado[b] == portasNoLado[a] && rnd.NextDouble() < 0.5)) (a, b) = (b, a);
                return new[] { fu, a, b, LadoDaPorta };
            }

            public float Face(int lado) => lado == 0 ? k.z0 + E : lado == 1 ? k.x1 - E : lado == 2 ? k.z1 - E : k.x0 + E;
            public static float GiroDoLado(int lado) => lado == 0 ? 0f : lado == 1 ? -90f : lado == 2 ? 180f : 90f;
            public (float lo, float hi) Ao(int lado) => lado == 0 || lado == 2 ? (X0, X1) : (Z0, Z1);
            public float Comprimento(int lado) => lado == 0 || lado == 2 ? W : D;

            /// <summary>Para onde o +x do modelo encostado aponta ao longo da parede (+1 ou -1 na coordenada da parede).</summary>
            public static float Sentido(int lado) => lado == 0 || lado == 1 ? 1f : -1f;

            /// <summary>Um ponto da sala: a coordenada ao longo da parede 's' e 'dentro' metros para dentro dela.</summary>
            public Vector2 Ponto(int lado, float s, float dentro)
            {
                float c = Face(lado);
                return lado switch
                {
                    0 => new Vector2(s, c + dentro),
                    2 => new Vector2(s, c - dentro),
                    3 => new Vector2(c + dentro, s),
                    _ => new Vector2(c - dentro, s),
                };
            }

            static float Escala => MedidasDaMobilia.Escala;
            public static float Largura(string m) => MedidasDaMobilia.Tem(m) ? MedidasDaMobilia.Real(m).x * Escala : 1f;
            public static float Fundura(string m) => MedidasDaMobilia.Tem(m) ? MedidasDaMobilia.Real(m).z * Escala : 1f;
            public static float Altura(string m) => MedidasDaMobilia.Tem(m) ? MedidasDaMobilia.Real(m).y * Escala : 1f;

            static bool Cruza(Rect a, Rect b, float folga) =>
                a.xMin < b.xMax + folga && a.xMax > b.xMin - folga && a.yMin < b.yMax + folga && a.yMax > b.yMin - folga;

            /// <summary>
            /// Um modelo com a origem em (x, z), o giro e a altura y do chão: confere se cabe (dentro da sala, fora das zonas
            /// das portas, sem tapar uma janela e sem pegar o que já entrou). 'junto' deixa encostar no que já entrou (a
            /// cadeira na mesa, a privada na cabine); acima de 30 cm (em cima de outro ou na parede) não ocupa o chão.
            /// </summary>
            public bool Por(string m, float x, float z, float giro, float y = 0f, bool junto = false)
            {
                if (!MedidasDaMobilia.Tem(m))
                {
                    lock (Faltam) Faltam.Add(m);
                    return false;
                }
                var lugar = new Vector3(x, Base(f) + y, z);
                var r = Pegada(m, lugar, giro, Escala);
                bool pendurado = y > PeDireito - 0.5f;
                if (!pendurado)
                {
                    if (r.xMin < X0 - 0.03f || r.xMax > X1 + 0.03f || r.yMin < Z0 - 0.03f || r.yMax > Z1 + 0.03f) return false;
                    foreach (var zn in zonas)
                        if (Cruza(r, zn, 0f)) return false;
                    if (y >= 0.3f || (Altura(m) > AltoParaAJanela && !PassaNaJanela.Contains(m)))
                        foreach (var (lado, de, ate) in janelas)
                        {
                            float c = Face(lado);
                            var faixa = lado == 0 ? Rect.MinMaxRect(de, c, ate, c + 0.45f) : lado == 2 ? Rect.MinMaxRect(de, c - 0.45f, ate, c)
                                      : lado == 3 ? Rect.MinMaxRect(c, de, c + 0.45f, ate) : Rect.MinMaxRect(c - 0.45f, de, c, ate);
                            if (Cruza(r, faixa, 0f) && (y < 0.3f || y + Altura(m) > Peitoril)) return false;
                        }
                    if (y < 0.3f && !junto)
                        foreach (var o in ocupados)
                            if (Cruza(r, o, 0.04f)) return false;
                }
                if (y < 0.3f) ocupados.Add(r);
                saida.Add(new Movel
                {
                    andar = f, modelo = m, lugar = lugar, giro = giro, escala = Escala, caixa = y < 0.3f && !SemCaixa.Contains(m),
                });
                return true;
            }

            /// <summary>Encostado: as costas na face da parede do lado, o meio em 's' ao longo dela.</summary>
            public bool Enc(int lado, string m, float s, float y = 0f, bool junto = false)
            {
                if (!MedidasDaMobilia.Tem(m))
                {
                    lock (Faltam) Faltam.Add(m);
                    return false;
                }
                if (ComPortas.Contains(m))
                {
                    // as portas abertas não entram nas paredes do lado
                    var (lo, hi) = Ao(lado);
                    float meia = Largura(m) / 2f;
                    if (s - meia < lo + FolgaDasPortas - 0.001f || s + meia > hi - FolgaDasPortas + 0.001f) return false;
                }
                float costas = (MedidasDaMobilia.Meio(m).y - MedidasDaMobilia.Real(m).z / 2f) * Escala;
                // o meio da caixa em x (o modelo pode não estar centrado): 's' é o meio da caixa
                float mx = MedidasDaMobilia.Meio(m).x * Escala;
                var p = Ponto(lado, s - Sentido(lado) * mx, -costas + 0.02f);
                return Por(m, p.x, p.y, GiroDoLado(lado), y, junto);
            }

            /// <summary>Encostado no lado, o mais perto de 's' que couber (até 'alcance' para cada lado). Devolve o 's' usado.</summary>
            public float? EncPerto(int lado, string m, float s, float alcance = 6f, float y = 0f)
            {
                for (float d = 0f; d <= alcance; d += 0.1f)
                {
                    if (Enc(lado, m, s + d, y)) return s + d;
                    if (d > 0f && Enc(lado, m, s - d, y)) return s - d;
                }
                return null;
            }

            /// <summary>
            /// 'n' do modelo encostados nas paredes (<see cref="Ordem"/>), cada parede varrida de uma ponta à outra (a ponta
            /// sorteada), com 'folga' entre um e o seguinte. Devolve quantos entraram.
            /// </summary>
            public int Parede(string m, int n = 1, float folga = 0.12f, params int[] lados)
            {
                if (!MedidasDaMobilia.Tem(m))
                {
                    lock (Faltam) Faltam.Add(m);
                    return 0;
                }
                int feitos = 0;
                float w = Largura(m);
                foreach (int lado in (lados != null && lados.Length > 0 ? lados : Ordem()))
                {
                    var (lo, hi) = Ao(lado);
                    bool doFim = rnd.NextDouble() < 0.5;
                    float s = doFim ? hi - w / 2f : lo + w / 2f;
                    while (feitos < n && s >= lo + w / 2f - 0.001f && s <= hi - w / 2f + 0.001f)
                    {
                        if (Enc(lado, m, s))
                        {
                            feitos++;
                            s += (doFim ? -1f : 1f) * (w + folga);
                        }
                        else s += doFim ? -0.1f : 0.1f;
                    }
                    if (feitos >= n) break;
                }
                return feitos;
            }

            /// <summary>
            /// Encostado de lado: o comprido (a maca, o carrinho de limpeza) paralelo à parede, o lado dele nela (o giro do
            /// lado mais 90°), com o meio em 's'. Nos corredores e nos saguões: de frente, a maca atravessava a passagem.
            /// </summary>
            public bool EncDeLado(int lado, string m, float s)
            {
                if (!MedidasDaMobilia.Tem(m))
                {
                    lock (Faltam) Faltam.Add(m);
                    return false;
                }
                float giro = GiroDoLado(lado) + 90f;
                // o meio da caixa fica a meia largura da parede (a largura do modelo vira a fundura)
                var q = Quaternion.Euler(0f, giro, 0f);
                Vector2 meio = MedidasDaMobilia.Meio(m) * Escala;
                Vector3 desvio = q * new Vector3(meio.x, 0f, meio.y);
                var c = Ponto(lado, s, Largura(m) / 2f + 0.03f);
                return Por(m, c.x - desvio.x, c.y - desvio.z, giro);
            }

            /// <summary>'n' encostados de lado (<see cref="EncDeLado"/>), varrendo as paredes como o <see cref="Parede"/>.</summary>
            public int ParedeDeLado(string m, int n = 1, float folga = 0.3f, params int[] lados)
            {
                if (!MedidasDaMobilia.Tem(m))
                {
                    lock (Faltam) Faltam.Add(m);
                    return 0;
                }
                int feitos = 0;
                float w = Fundura(m);
                foreach (int lado in (lados != null && lados.Length > 0 ? lados : Ordem()))
                {
                    var (lo, hi) = Ao(lado);
                    bool doFim = rnd.NextDouble() < 0.5;
                    float s = doFim ? hi - w / 2f : lo + w / 2f;
                    while (feitos < n && s >= lo + w / 2f - 0.001f && s <= hi - w / 2f + 0.001f)
                    {
                        if (EncDeLado(lado, m, s))
                        {
                            feitos++;
                            s += (doFim ? -1f : 1f) * (w + folga);
                        }
                        else s += doFim ? -0.1f : 0.1f;
                    }
                    if (feitos >= n) break;
                }
                return feitos;
            }

            /// <summary>Na parede, numa altura (o quadro, a TV, o mural), num trecho sem porta e sem janela.</summary>
            public bool NaParede(string m, float y, params int[] lados)
            {
                float w = Largura(m);
                foreach (int lado in (lados != null && lados.Length > 0 ? lados : Ordem()))
                {
                    var (lo, hi) = Ao(lado);
                    float meio = (lo + hi) / 2f;
                    for (float d = 0f; d <= (hi - lo) / 2f - w / 2f; d += 0.2f)
                    {
                        if (Enc(lado, m, meio + d, y) || (d > 0f && Enc(lado, m, meio - d, y))) return true;
                    }
                }
                return false;
            }

            /// <summary>No meio, numa fração da sala (u em x, v em z).</summary>
            public bool Meio(string m, float u, float v, float giro, float y = 0f, bool junto = false) =>
                Por(m, Mathf.Lerp(X0, X1, u), Mathf.Lerp(Z0, Z1, v), giro, y, junto);

            /// <summary>No meio, o mais perto do ponto (u, v) que couber (em volta dele, até 'alcance' metros).</summary>
            public bool MeioPerto(string m, float u, float v, float giro, float alcance = 1.6f)
            {
                float x = Mathf.Lerp(X0, X1, u), z = Mathf.Lerp(Z0, Z1, v);
                if (Por(m, x, z, giro)) return true;
                for (float r = 0.2f; r <= alcance; r += 0.2f)
                    for (int a = 0; a < 8; a++)
                    {
                        float ang = a * Mathf.PI / 4f;
                        if (Por(m, x + r * Mathf.Cos(ang), z + r * Mathf.Sin(ang), giro)) return true;
                    }
                return false;
            }

            /// <summary>Um ponto do modelo já posto (o último daquele nome), no espaço dele (em metros do jogo).</summary>
            public Vector3? Do(string m, Vector3 local)
            {
                for (int i = saida.Count - 1; i >= 0; i--)
                    if (saida[i].modelo == m && saida[i].andar == f && Contem(saida[i].lugar))
                        return saida[i].lugar + Quaternion.Euler(0f, saida[i].giro, 0f) * local;
                return null;
            }

            public float? GiroDo(string m)
            {
                for (int i = saida.Count - 1; i >= 0; i--)
                    if (saida[i].modelo == m && saida[i].andar == f && Contem(saida[i].lugar)) return saida[i].giro;
                return null;
            }

            bool Contem(Vector3 p) => p.x >= k.x0 && p.x <= k.x1 && p.z >= k.z0 && p.z <= k.z1;

            /// <summary>Os móveis daquele modelo já postos neste cômodo (para pôr o que vai em cima deles).</summary>
            public List<Movel> Postos(string m) => saida.Where(x => x.modelo == m && x.andar == f && Contem(x.lugar)).ToList();

            /// <summary>Uma coisa fixa da construção que ocupa o chão (os balcões).</summary>
            public void Ocupar(Rect r) => ocupados.Add(r);

            // -------------------------------------------------------- os conjuntos

            /// <summary>A mesa de escritório numa parede, com o computador em cima (na frente do vão) e a cadeira de rodinhas.</summary>
            public bool Mesa(int? ladoPedido = null, string cadeira = "CadeiraEscritorio")
            {
                foreach (int lado in ladoPedido.HasValue ? new[] { ladoPedido.Value } : Ordem())
                {
                    var (lo, hi) = Ao(lado);
                    float w = Largura("MesaEscritorio");
                    for (float s = lo + w / 2f; s <= hi - w / 2f; s += 0.1f)
                    {
                        if (!Enc(lado, "MesaEscritorio", s)) continue;
                        // o vão da cadeira fica a 0,20 m (real) do meio, para o +x do modelo
                        float sv = s + Sentido(lado) * 0.20f * Escala;
                        Enc(lado, "Computador", sv, 0.76f * Escala, true);
                        var c = Ponto(lado, sv, 0.60f * Escala + 0.15f);
                        Por(cadeira, c.x, c.y, GiroDoLado(lado) + 180f + (float)(rnd.NextDouble() * 40 - 20), 0f, true);
                        return true;
                    }
                }
                return false;
            }

            /// <summary>A cama de hospital com a cabeceira na parede e o criado do lado; com 'uti', o monitor e o respirador.</summary>
            public bool Cama(int lado, float s, bool uti = false)
            {
                if (!Enc(lado, "CamaHospital", s)) return false;
                float wc = Largura("CamaHospital");
                float sc = s + Sentido(lado) * (wc / 2f + Largura("CriadoHospitalar") / 2f + 0.05f);
                if (!Enc(lado, "CriadoHospitalar", sc)) Enc(lado, "CriadoHospitalar", s - Sentido(lado) * (wc / 2f + Largura("CriadoHospitalar") / 2f + 0.05f));
                if (uti)
                {
                    var pm = Ponto(lado, s - Sentido(lado) * (wc / 2f + 0.45f), 0.5f);
                    Por("MonitorSinais", pm.x, pm.y, GiroDoLado(lado) + 25f, 0f, true);
                    var pr = Ponto(lado, s - Sentido(lado) * (wc / 2f + 0.5f), 1.4f);
                    Por("Respirador", pr.x, pr.y, GiroDoLado(lado) + 60f, 0f, true);
                }
                return true;
            }

            /// <summary>Uma mesa solta com as cadeiras em volta (2 de cada lado comprido; com 'pontas', uma em cada ponta).</summary>
            public bool MesaComCadeiras(string mesa, string cadeira, float x, float z, float giro, bool pontas = false, int porLado = 2,
                                        bool junto = false)
            {
                if (!Por(mesa, x, z, giro, 0f, junto)) return false;
                var q = Quaternion.Euler(0f, giro, 0f);
                float lw = Largura(mesa), ld = Fundura(mesa);
                for (int i = 0; i < porLado; i++)
                {
                    float px = porLado == 1 ? 0f : (i - (porLado - 1) / 2f) * Mathf.Min(0.75f, lw / porLado);
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var p = new Vector3(x, 0f, z) + q * new Vector3(px, 0f, sz * (ld / 2f + 0.25f));
                        Por(cadeira, p.x, p.z, giro + (sz < 0 ? 0f : 180f) + (float)(rnd.NextDouble() * 24 - 12), 0f, true);
                    }
                }
                if (pontas)
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var p = new Vector3(x, 0f, z) + q * new Vector3(sx * (lw / 2f + 0.25f), 0f, 0f);
                        Por(cadeira, p.x, p.z, giro + (sx < 0 ? 90f : -90f), 0f, true);
                    }
                return true;
            }
        }

        // ------------------------------------------------------------ as receitas

        static void Receita(Sala s)
        {
            var k = s.k;
            int f = s.f;
            switch (k.tipo)
            {
                // ---------------------------------------------------- o subsolo
                case "lavanderia":
                    s.Parede("MaquinaDeLavar", 3, 0.15f);
                    s.Parede("Secadora", 2, 0.15f);
                    s.Parede("BancadaInox");
                    s.Parede("EstanteAco120");
                    s.Meio("CarrinhoDeRoupa", 0.35f, 0.5f, 15f);
                    s.Meio("CarrinhoDeRoupa", 0.65f, 0.45f, -20f);
                    break;
                case "manutencao":
                    s.Parede("Bancada");
                    s.Parede("EstanteAco120", 2);
                    s.Parede("Galoes");
                    s.Parede("Tambores");
                    s.Parede("CaixasDeSuprimentos");
                    break;
                case "geradores":
                    s.Parede("Gerador", 2, 0.6f);
                    s.Parede("TanqueDiesel");
                    s.Parede("QuadroEletrico", 2);
                    s.Parede("Tambores");
                    break;
                case "eletrica":
                    s.Parede("QuadroEletrico", 4, 0.1f);
                    s.Parede("Baterias", 2);
                    s.Parede("ArmarioDeAco");
                    break;
                case "residuos":
                    s.Parede("ConteinerLixoHospitalar", 7, 0.1f);
                    s.Parede("Tambores", 2);
                    s.Meio("ConteinerLixoHospitalar", 0.5f, 0.5f, 30f);
                    break;
                case "deposito":
                    Deposito(s);
                    break;
                case "autopsia":
                    foreach (float u in new[] { 0.32f, 0.68f })
                        if (s.Meio("MesaAutopsia", u, 0.5f, 0f))
                            s.Meio("FocoCirurgico", u - 0.06f, 0.5f, 0f, PeDireito);
                    s.Parede("BancadaInox", 2);
                    s.Parede("PiaInox");
                    s.Parede("ArmarioDeAco");
                    s.Parede("MesaDeInstrumentos", 2);
                    s.Parede("ConteinerLixoHospitalar");
                    break;
                case "necroterio":
                    s.Parede("GavetasNecroterio", 2, 0.3f, s.Fundo, (s.Fundo + 1) % 4, (s.Fundo + 3) % 4);
                    s.MeioPerto("MacaComCorpo", 0.35f, 0.45f, 90f);
                    s.MeioPerto("MacaComCorpo", 0.62f, 0.55f, 80f);
                    s.MeioPerto("MacaNecroterio", 0.5f, 0.25f, 100f);
                    s.Parede("BancadaInox");
                    s.MeioPerto("SacoDeCorpo", 0.78f, 0.4f, 70f);
                    break;
                case "camaras":
                    s.Parede("GavetasNecroterio", 2, 0.3f, s.Fundo, (s.Fundo + 1) % 4, (s.Fundo + 3) % 4);
                    s.MeioPerto("MacaComCorpo", 0.4f, 0.5f, 85f);
                    s.MeioPerto("MacaComCorpo", 0.65f, 0.4f, 95f);
                    s.MeioPerto("SacoDeCorpo", 0.3f, 0.25f, 5f);
                    s.MeioPerto("SacoDeCorpo", 0.7f, 0.75f, 175f);
                    s.Parede("EstanteAco120");
                    break;
                case "hall":
                    s.ParedeDeLado("MacaNecroterio");
                    s.Parede("BancoDeEspera3");
                    break;
                case "oficina":
                    s.Parede("Bancada", 2, 0.3f);
                    s.Parede("EstanteAco120", 2);
                    s.Parede("Tambores");
                    s.Parede("Galoes");
                    s.Meio("CadeiraDeRodas", 0.5f, 0.5f, 35f);
                    break;
                case "vestiario":
                    s.Parede("Vestiario", 3, 0.05f);
                    s.Meio("BancoRefeitorio", 0.5f, 0.5f, s.Fundo % 2 == 0 ? 0f : 90f);
                    break;
                case "bombas":
                    s.Parede("Bomba", 2, 0.4f);
                    s.Parede("FiltroDeAgua", 2);
                    s.Parede("QuadroEletrico");
                    break;
                case "banheiro":
                    Banheiro(s, s.Nome.Contains("feminino") ? false : true);
                    break;
                case "tecnica":
                    s.Parede("QuadroEletrico", 2);
                    break;
                case "corredor":
                    Corredor(s);
                    break;

                // ---------------------------------------------------- o térreo
                case "vermelha":
                    foreach (float u in new[] { 0.3f, 0.7f })
                    {
                        if (!s.Meio("MacaEmergencia", u, 0.5f, 0f)) continue;
                        s.Meio("MonitorSinais", u + 0.13f, 0.72f, -30f, 0f, true);
                        s.Meio("SuporteDeSoro", u - 0.13f, 0.7f, 10f, 0f, true);
                    }
                    s.Parede("CarrinhoDeParada");
                    s.Parede("ArmarioDeRemedios");
                    s.Parede("Respirador");
                    s.Parede("BancadaInox");
                    break;
                case "medicacao":
                    s.Parede("ArmarioDeRemedios", 2, 0.05f);
                    s.Parede("GeladeiraLaboratorio");
                    s.Parede("BancadaInox");
                    s.Parede("CriadoHospitalar");
                    break;
                case "limpeza":
                    s.Parede("CarrinhoDeLimpeza");
                    s.Parede("EstanteAco90");
                    s.Parede("Galoes");
                    s.Parede("CarrinhoDeRoupa");
                    break;
                case "servico":
                    s.Parede("ConteinerLixoHospitalar", 3, 0.1f);
                    s.Parede("CarrinhoDeRoupa");
                    break;
                case "raiox":
                    s.Meio("AparelhoRaioX", 0.5f, 0.45f, s.Fundo == 0 ? 180f : s.Fundo == 2 ? 0f : s.Fundo == 1 ? 90f : -90f);
                    s.Parede("Biombo");
                    s.Parede("ArmarioDeAco");
                    s.Parede("CadeiraDeRodas");
                    break;
                case "comando":
                    s.Mesa(s.Fundo);
                    s.Parede("Arquivo");
                    break;
                case "recebimento":
                    s.Parede("MacaEmergencia", 2, 0.4f);
                    s.Parede("CadeiraDeRodas", 2);
                    s.Parede("SuporteDeSoro");
                    break;
                case "emergencia":
                    Emergencia(s);
                    break;
                case "triagem":
                    s.Mesa();
                    s.Parede("MacaExame");
                    s.Parede("MonitorSinais");
                    s.Parede("Cadeira");
                    break;
                case "espera":
                    if (f == Terreo) EsperaDoTerreo(s);
                    else
                    {
                        s.Parede("BancoDeEspera3", 2, 0.3f);
                        s.Parede("VasoPlanta", 2);
                        s.Parede("Bebedouro");
                    }
                    break;
                case "recepcao":
                    Balcao(s, R(23f, 12.6f, 33f, 15.2f));
                    s.Parede("BancoDeEspera3");
                    s.Parede("CadeiraDeRodas");
                    break;
                case "consultorio":
                    s.Mesa();
                    s.Parede("MacaExame");
                    s.Parede("Cadeira", 2, 0.2f);
                    s.Parede("Biombo");
                    s.Parede("ArmarioDeRemedios");
                    break;
                case "laboratorio":
                    Laboratorio(s);
                    break;
                case "seguranca":
                    if (s.Parede("MesaMonitoramento", 1, 0.1f, s.Fundo, (s.Fundo + 1) % 4, (s.Fundo + 3) % 4) > 0)
                    {
                        var p = s.Do("MesaMonitoramento", new Vector3(0f, 0f, 0.4f * MedidasDaMobilia.Escala + 0.45f));
                        if (p.HasValue) s.Por("CadeiraEscritorio", p.Value.x, p.Value.z, s.GiroDo("MesaMonitoramento").Value + 180f, 0f, true);
                    }
                    s.Parede("Arquivo", 2, 0.05f);
                    s.Parede("ArmarioDeAco");
                    break;
                case "observacao":
                    Leitos(s, 4, uti: false);
                    s.Parede("SuporteDeSoro", 2);
                    s.Parede("CadeiraDeRodas");
                    break;
                case "coleta":
                    s.Parede("CadeiraDeColeta", 3, 0.5f);
                    s.Parede("BancadaInox");
                    s.Parede("GeladeiraLaboratorio");
                    s.Parede("ConteinerLixoHospitalar");
                    s.Parede("BancoDeEspera3");
                    break;
                case "garagem":
                    s.Parede("EstanteAco120", 2, 0.1f, 2);
                    s.Parede("CilindrosDeOxigenio", 1, 0.1f, 2);
                    s.ParedeDeLado("MacaEmergencia", 1, 0.3f, 2);
                    break;
                case "entrada":
                    s.Parede("CadeiraDeRodas", 2, 0.1f);
                    break;

                // ---------------------------------------------------- o 2º andar
                case "uti":
                    Leitos(s, 5, uti: true);
                    s.Parede("CarrinhoDeParada");
                    s.Parede("ArmarioDeRemedios");
                    s.Parede("SuporteDeSoro", 2);
                    break;
                case "preparo":
                    s.Parede("PiaCirurgica", 1, 0.1f, s.Fundo, (s.Fundo + 1) % 4, (s.Fundo + 3) % 4);
                    s.Parede("ArmarioDeAco");
                    s.Parede("MesaDeInstrumentos");
                    s.Parede("CarrinhoDeRoupa");
                    break;
                case "cirurgia":
                    Cirurgia(s);
                    break;
                case "enfermaria":
                    Leitos(s, 9, uti: false);
                    s.Parede("SuporteDeSoro", 2);
                    s.Parede("CadeiraDeRodas");
                    s.Parede("Biombo", 2);
                    break;
                case "posto":
                    Balcao(s, R(22f, 12.5f, 34f, 16f));
                    s.Parede("CarrinhoDeParada");
                    s.ParedeDeLado("MacaEmergencia");
                    break;
                case "isolamento":
                    // o corredor do isolamento é estreito (2,5 m): nada nele
                    break;
                case "quarto":
                    s.Cama(s.Fundo, (s.Ao(s.Fundo).lo + s.Ao(s.Fundo).hi) / 2f, true);
                    s.Parede("SuporteDeSoro");
                    s.Parede("Cadeira");
                    break;
                case "descanso":
                    Descanso(s);
                    break;

                // ---------------------------------------------------- o 3º andar
                case "ti":
                    s.Parede("RackServidor", 3, 0.05f, s.Fundo, (s.Fundo + 1) % 4, (s.Fundo + 3) % 4);
                    s.Mesa();
                    s.Parede("EstanteAco90");
                    break;
                case "copa":
                    Copa(s);
                    break;
                case "reuniao":
                    Reuniao(s);
                    break;
                case "arquivo":
                    Arquivo(s);
                    break;
                case "escritorio":
                    Escritorio(s);
                    break;
                case "rh":
                    s.Mesa();
                    s.Mesa();
                    s.Parede("Arquivo", 2, 0.05f);
                    s.Parede("Cadeira", 2, 0.2f);
                    break;
                case "planejamento":
                    s.MesaComCadeiras("Mesa6", "CadeiraEstofada", (s.X0 + s.X1) / 2f, (s.Z0 + s.Z1) / 2f, s.W >= s.D ? 0f : 90f, true, 3);
                    s.NaParede("QuadroBranco", 0.9f);
                    s.NaParede("QuadroDeAvisos", 1.2f);
                    s.Parede("EstanteLivros");
                    s.Parede("Arquivo");
                    break;
                case "diretor":
                    Diretor(s);
                    break;
                case "secretaria":
                    s.Mesa();
                    s.Parede("BancoDeEspera3");
                    s.Parede("Arquivo", 2, 0.05f);
                    s.Parede("Copiadora");
                    s.Parede("VasoPlanta");
                    s.NaParede("QuadroDeAvisos", 1.2f);
                    break;
            }
        }

        // ------------------------------------------------------------ os cômodos maiores

        // o depósito: as estantes nas paredes e, se a sala é larga, uma fila de estantes de costas no meio
        static void Deposito(Sala s)
        {
            int n = s.f == 0 ? 6 : 4;
            s.Parede("EstanteAco120", n, 0.05f);
            if (Mathf.Min(s.W, s.D) > 7f)
            {
                bool emX = s.W >= s.D;
                float l = Sala.Largura("EstanteAco120"), fu = Sala.Fundura("EstanteAco120");
                float comp = (emX ? s.W : s.D) - 4.5f;
                int quantas = Mathf.Max(1, Mathf.FloorToInt(comp / (l + 0.02f)));
                for (int i = 0; i < quantas; i++)
                {
                    float t = (i - (quantas - 1) / 2f) * (l + 0.02f);
                    float x = (s.X0 + s.X1) / 2f + (emX ? t : 0f), z = (s.Z0 + s.Z1) / 2f + (emX ? 0f : t);
                    s.Por("EstanteAco120", x + (emX ? 0f : -fu / 2f - 0.01f), z + (emX ? -fu / 2f - 0.01f : 0f), emX ? 180f : -90f, 0f, true);
                    s.Por("EstanteAco120", x + (emX ? 0f : fu / 2f + 0.01f), z + (emX ? fu / 2f + 0.01f : 0f), emX ? 0f : 90f, 0f, true);
                }
            }
            s.Parede("CaixasDeSuprimentos", 2);
            s.Parede("CilindrosDeOxigenio");
        }

        // o banheiro público: as cabines na parede do fundo (a primeira com as duas divisórias, as outras só com a da
        // direita), a privada dentro de cada uma, as pias numa parede do lado e, no masculino, os mictórios na outra
        static void Banheiro(Sala s, bool masculino)
        {
            int fu = s.Fundo;
            var (lo, hi) = s.Ao(fu);
            float w = Sala.Largura("CabineDeBanheiro");
            int cabines = Mathf.Clamp(Mathf.FloorToInt((hi - lo - 0.3f) / w), 1, 4);
            // a fileira vai da esquerda para a direita de quem olha para elas (o -x do modelo): a "Lado" não tem a divisória
            // da esquerda, e usa a da direita da cabine de antes
            float sentido = Sala.Sentido(fu);
            float comeco = sentido > 0 ? hi : lo;
            int feitas = 0;
            for (int i = 0; i < cabines; i++)
            {
                float sc = comeco - sentido * (w * (i + 0.5f) + 0.05f);
                string modelo = i == 0 ? "CabineDeBanheiro" : "CabineDeBanheiroLado";
                if (!s.Enc(fu, modelo, sc, 0f, i > 0)) continue;
                s.Enc(fu, "Privada", sc, 0f, true);
                feitas++;
            }
            int a = (fu + 1) % 4, b = (fu + 3) % 4;
            s.Parede("PiaColuna", 2, 0.25f, a, b);
            if (masculino)
            {
                // na parede, com a boca a uns 60 cm do chão (o modelo tem a origem embaixo dele): de uma ponta, a 0,85 m um do outro
                var (l0, l1) = s.Ao(b);
                int postos = 0;
                for (float t = l0 + 0.5f; t <= l1 - 0.5f && postos < 2; t += 0.1f)
                    if (s.Enc(b, "Mictorio", t, 0.45f)) { postos++; t += 0.75f; }
            }
            s.Parede("ConteinerLixoHospitalar", 1, 0.1f, a, b);
        }

        // um corredor: poucas coisas paradas encostadas nas paredes compridas, espalhadas (uma a cada 7 m), longe das portas
        static void Corredor(Sala s)
        {
            string[] coisas = s.f switch
            {
                0 => new[] { "CarrinhoDeRoupa", "MacaNecroterio", "ConteinerLixoHospitalar", "CarrinhoDeLimpeza", "CilindrosDeOxigenio", "CadeiraDeRodas" },
                1 => new[] { "MacaEmergencia", "CadeiraDeRodas", "BancoDeEspera3", "SuporteDeSoro", "MacaEmergencia", "CarrinhoDeLimpeza" },
                2 => new[] { "MacaEmergencia", "SuporteDeSoro", "CarrinhoDeParada", "CadeiraDeRodas", "CarrinhoDeRoupa", "MonitorSinais" },
                _ => new[] { "BancoDeEspera3", "VasoPlanta", "Arquivo", "CadeiraDeRodas", "Bebedouro", "VasoPlanta" },
            };
            bool emX = s.W >= s.D;
            if (Mathf.Min(s.W, s.D) < 2.7f) return;
            int[] lados = emX ? new[] { 0, 2 } : new[] { 1, 3 };
            var (lo, hi) = s.Ao(lados[0]);
            int n = Mathf.Clamp(Mathf.RoundToInt((hi - lo) / 7f), 1, coisas.Length);
            for (int i = 0; i < n; i++)
            {
                float alvo = Mathf.Lerp(lo, hi, (i + 0.5f) / n) + (float)(s.rnd.NextDouble() * 2 - 1);
                int lado = lados[s.rnd.Next(2)];
                string m = coisas[i];
                bool deLado = Sala.Fundura(m) > 0.9f;
                foreach (int l in new[] { lado, lados[0] == lado ? lados[1] : lados[0] })
                {
                    bool foi = false;
                    for (float d = 0f; d <= 2.5f && !foi; d += 0.1f)
                        foreach (float t in d > 0f ? new[] { alvo + d, alvo - d } : new[] { alvo })
                            if (deLado ? s.EncDeLado(l, m, t) : s.Enc(l, m, t)) { foi = true; break; }
                    if (foi) break;
                }
            }
        }

        // a emergência: as macas com a cabeceira nas paredes dos lados (com o monitor e o soro entre elas), o carrinho de
        // parada e a mesa de instrumentos
        static void Emergencia(Sala s)
        {
            foreach (int lado in s.Ordem().Take(3))
            {
                var (lo, hi) = s.Ao(lado);
                float w = Sala.Largura("MacaEmergencia");
                for (float t = lo + 0.6f + w / 2f; t <= hi - w / 2f - 0.6f; t += 0.1f)
                {
                    if (!s.Enc(lado, "MacaEmergencia", t)) continue;
                    var pm = s.Ponto(lado, t + Sala.Sentido(lado) * (w / 2f + 0.4f), 0.45f);
                    s.Por("MonitorSinais", pm.x, pm.y, Sala.GiroDoLado(lado) - 20f, 0f, true);
                    var ps = s.Ponto(lado, t - Sala.Sentido(lado) * (w / 2f + 0.35f), 0.5f);
                    s.Por("SuporteDeSoro", ps.x, ps.y, 0f, 0f, true);
                    t += w + 1.1f;
                }
            }
            s.Parede("CarrinhoDeParada");
            s.Parede("MesaDeInstrumentos");
            s.Parede("Biombo", 2);
            s.Parede("ArmarioDeRemedios");
        }

        // os leitos (a observação, a UTI, as enfermarias): as camas com a cabeceira na parede, primeiro no fundo e depois
        // nas outras, com o criado do lado (na UTI, o monitor e o respirador)
        static void Leitos(Sala s, int n, bool uti)
        {
            int feitas = 0;
            float w = Sala.Largura("CamaHospital") + Sala.Largura("CriadoHospitalar") + (uti ? 1.3f : 0.6f);
            float wc = Sala.Largura("CamaHospital"), comprimento = Sala.Fundura("CamaHospital");
            var usados = new HashSet<int>();
            foreach (int lado in s.Ordem())
            {
                var (lo, hi) = s.Ao(lado);
                // as pontas da parede: 1 m livre no canto; se a parede vizinha daquela ponta já tem camas, o comprimento
                // de uma cama mais 1 m (as duas fileiras fechavam um bolso no canto, sem caminho para o zumbi)
                int vizLo = lado == 0 || lado == 2 ? 3 : 0, vizHi = lado == 0 || lado == 2 ? 1 : 2;
                float mLo = usados.Contains(vizLo) ? comprimento + 1.0f : 1.0f, mHi = usados.Contains(vizHi) ? comprimento + 1.0f : 1.0f;
                int antes = feitas;
                for (float t = lo + wc / 2f + mLo; t <= hi - wc / 2f - mHi && feitas < n; t += 0.1f)
                {
                    if (!s.Cama(lado, t, uti)) continue;
                    feitas++;
                    t += w - 0.1f;
                }
                if (feitas > antes) usados.Add(lado);
                if (feitas >= n) break;
            }
        }

        // a sala de cirurgia: a mesa no meio, o foco em cima dela, a anestesia na cabeceira, as mesas de instrumentos dos
        // lados, o monitor e o soro; nas paredes, o armário de remédios e o lixo
        static void Cirurgia(Sala s)
        {
            bool emX = s.W >= s.D;
            float giro = emX ? 90f : 0f;
            float cx = (s.X0 + s.X1) / 2f, cz = (s.Z0 + s.Z1) / 2f;
            bool posta = false;
            // no meio ou um pouco para o lado (na sala 2, estreita, a tala do braço pegava o giro da porta)
            foreach (var (dx, dz, g) in new[] { (0f, 0f, giro), (0.4f, 0f, giro), (-0.4f, 0f, giro), (0f, 0.4f, giro), (0f, -0.4f, giro),
                                                (0.6f, 0.3f, giro), (0.6f, -0.3f, giro), (0f, 0f, giro + 180f), (0.5f, 0f, giro + 180f) })
                if (s.Por("MesaCirurgica", cx + dx, cz + dz, g)) { cx += dx; cz += dz; giro = g; posta = true; break; }
            if (posta)
            {
                s.Por("FocoCirurgico", cx, cz, giro, PeDireito);
                var q = Quaternion.Euler(0f, giro, 0f);
                // a cabeceira é o -z do modelo (o +Y do Blender)
                var cab = new Vector3(cx, 0f, cz) + q * new Vector3(0f, 0f, -1.75f);
                s.Por("MaquinaDeAnestesia", cab.x, cab.z, giro, 0f, true);
                var mon = new Vector3(cx, 0f, cz) + q * new Vector3(0.9f, 0f, -1.2f);
                s.Por("MonitorSinais", mon.x, mon.z, giro - 30f, 0f, true);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var mi = new Vector3(cx, 0f, cz) + q * new Vector3(sx * 1.25f, 0f, 0.6f);
                    s.Por("MesaDeInstrumentos", mi.x, mi.z, giro + 90f * sx, 0f, true);
                }
                var so = new Vector3(cx, 0f, cz) + q * new Vector3(-0.85f, 0f, -1.1f);
                s.Por("SuporteDeSoro", so.x, so.z, 0f, 0f, true);
            }
            s.Parede("ArmarioDeRemedios");
            s.Parede("ConteinerLixoHospitalar");
            s.Parede("CarrinhoDeParada");
        }

        // o laboratório: as bancadas nas paredes e uma ilha de duas de costas no meio, o microscópio e a centrífuga em cima,
        // as cadeiras no vão das pernas, as geladeiras e o armário
        static void Laboratorio(Sala s)
        {
            int bancadas = s.Parede("BancadaLaboratorio", 4, 0.1f);
            bool emX = s.W >= s.D;
            float fu = Sala.Fundura("BancadaLaboratorio");
            float cx = (s.X0 + s.X1) / 2f, cz = (s.Z0 + s.Z1) / 2f;
            for (int i = -1; i <= 1; i += 2)
            {
                float t = i * (Sala.Largura("BancadaLaboratorio") / 2f + 0.05f);
                foreach (float lado in new[] { -1f, 1f })
                {
                    float x = cx + (emX ? t : lado * (fu / 2f + 0.01f)), z = cz + (emX ? lado * (fu / 2f + 0.01f) : t);
                    float giro = emX ? (lado < 0 ? 180f : 0f) : (lado < 0 ? -90f : 90f);
                    s.Por("BancadaLaboratorio", x, z, giro, 0f, true);
                }
            }
            s.Parede("GeladeiraLaboratorio", 2, 0.05f);
            s.Parede("ArmarioDeAco");
            // em cima das bancadas: o microscópio e a centrífuga (alternados), e a cadeira no vão das pernas
            float topo = 0.9f * MedidasDaMobilia.Escala;
            var postas = new List<(Vector3 lugar, float giro)>();
            foreach (var m in s.Postos("BancadaLaboratorio")) postas.Add((m.lugar, m.giro));
            for (int i = 0; i < postas.Count; i++)
            {
                var (lugar, giro) = postas[i];
                var q = Quaternion.Euler(0f, giro, 0f);
                var em = lugar + q * new Vector3(0.15f, 0f, 0.05f);
                s.Por(i % 2 == 0 ? "Microscopio" : "Centrifuga", em.x, em.z, giro + (float)(s.rnd.NextDouble() * 30 - 15), topo);
                var c = lugar + q * new Vector3(-0.68f * MedidasDaMobilia.Escala, 0f, 0.75f);
                s.Por("CadeiraEscritorio", c.x, c.z, giro + 180f + (float)(s.rnd.NextDouble() * 50 - 25), 0f, true);
            }
        }

        // a espera do térreo: as fileiras de bancos de 4 de costas no meio, os bancos de 3 nas paredes, a máquina de
        // lanches, o bebedouro, a TV na parede e o mural
        static void EsperaDoTerreo(Sala s)
        {
            float fu = Sala.Fundura("BancoDeEspera4"), l = Sala.Largura("BancoDeEspera4");
            float cz = (s.Z0 + s.Z1) / 2f + 0.3f;
            int fileiras = Mathf.Max(1, Mathf.FloorToInt((s.W - 4f) / (l + 1.4f)));
            for (int i = 0; i < fileiras; i++)
            {
                float x = Mathf.Lerp(s.X0 + 2f + l / 2f, s.X1 - 2f - l / 2f, fileiras == 1 ? 0.5f : i / (float)(fileiras - 1));
                if (s.Por("BancoDeEspera4", x, cz - fu / 2f - 0.05f, 180f))
                    s.Por("BancoDeEspera4", x, cz + fu / 2f + 0.05f, 0f, 0f, true);
            }
            s.Parede("MaquinaDeLanches");
            s.Parede("Bebedouro");
            s.Parede("BancoDeEspera3", 3, 0.6f);
            s.Parede("VasoPlanta", 2);
            s.NaParede("TVDeParede", 1.9f);
            s.NaParede("QuadroDeAvisos", 1.2f);
        }

        // o balcão em U (a recepção, o posto): ele ocupa o chão; as cadeiras de rodinhas dentro dele e o arquivo atrás
        static void Balcao(Sala s, Rect balcao)
        {
            s.Ocupar(balcao);
            float cx = balcao.center.x, cz = balcao.center.y;
            s.Por("CadeiraEscritorio", cx - 1.6f, cz + 0.2f, 180f + (float)(s.rnd.NextDouble() * 40 - 20), 0f, true);
            s.Por("CadeiraEscritorio", cx + 1.4f, cz + 0.1f, 180f + (float)(s.rnd.NextDouble() * 40 - 20), 0f, true);
        }

        // o descanso da equipe: o sofá com as poltronas em volta da mesa de centro, a mesa com as cadeiras, a geladeira, o
        // bebedouro, a máquina de lanches e a TV
        static void Descanso(Sala s)
        {
            float cx = Mathf.Lerp(s.X0, s.X1, 0.3f), cz = (s.Z0 + s.Z1) / 2f;
            if (s.Por("MesaDeCentro", cx, cz, 0f))
            {
                s.Por("Sofa3", cx, cz + 1.25f, 180f, 0f, true);
                s.Por("Sofa1", cx - 1.6f, cz, 90f, 0f, true);
                s.Por("Sofa1", cx + 1.6f, cz, -90f, 0f, true);
            }
            s.MesaComCadeiras("Mesa4", "Cadeira", Mathf.Lerp(s.X0, s.X1, 0.75f), cz, 0f);
            s.Parede("Geladeira");
            s.Parede("Bebedouro");
            s.Parede("MaquinaDeLanches");
            s.Parede("TVConsole");
        }

        // a copa: a pia com um módulo de cada lado, a geladeira, a mesa com as cadeiras e o bebedouro
        static void Copa(Sala s)
        {
            int fu = s.Fundo;
            var (lo, hi) = s.Ao(fu);
            float? sp = s.EncPerto(fu, "Pia", (lo + hi) / 2f, 3f);
            if (sp.HasValue)
            {
                float wp = Sala.Largura("Pia"), wa = Sala.Largura("ArmarioChao60");
                s.Enc(fu, "ArmarioChao60", sp.Value + (wp + wa) / 2f + 0.01f, 0f, true);
                s.Enc(fu, "ArmarioChao60", sp.Value - (wp + wa) / 2f - 0.01f, 0f, true);
            }
            s.Parede("Geladeira");
            s.MesaComCadeiras("Mesa4", "Cadeira", (s.X0 + s.X1) / 2f, (s.Z0 + s.Z1) / 2f, 0f);
            s.Parede("Bebedouro");
        }

        // a sala de reunião: duas mesas de 6 emendadas no meio com as cadeiras de rodinhas, o quadro branco, a TV e o arquivo
        static void Reuniao(Sala s)
        {
            bool emX = s.W >= s.D;
            float giro = emX ? 0f : 90f;
            float l = Sala.Largura("Mesa6");
            float cx = (s.X0 + s.X1) / 2f, cz = (s.Z0 + s.Z1) / 2f;
            foreach (float sx in new[] { -0.5f, 0.5f })
            {
                float x = cx + (emX ? sx * (l + 0.01f) : 0f), z = cz + (emX ? 0f : sx * (l + 0.01f));
                s.MesaComCadeiras("Mesa6", "CadeiraEscritorio", x, z, giro, false, 3, sx > 0f);
            }
            // as cadeiras das pontas
            var q = Quaternion.Euler(0f, giro, 0f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var p = new Vector3(cx, 0f, cz) + q * new Vector3(sx * (l + 0.35f), 0f, 0f);
                s.Por("CadeiraEscritorio", p.x, p.z, giro + (sx < 0 ? 90f : -90f), 0f, true);
            }
            s.NaParede("QuadroBranco", 0.9f, emX ? new[] { 1, 3 } : new[] { 0, 2 });
            s.NaParede("TVDeParede", 1.7f);
            s.Parede("Arquivo");
            s.Parede("VasoPlanta", 2);
        }

        // o arquivo: as fileiras de estantes de costas, do fundo (a 0,8 m da parede, para não tapar as janelas) até o
        // corredor da porta (2,2 m livres), e a mesa com a copiadora perto da porta
        static void Arquivo(Sala s)
        {
            int porta = s.LadoDaPorta;
            bool fileirasEmZ = porta == 0 || porta == 2;
            float l = Sala.Largura("EstanteArquivo"), fu = Sala.Fundura("EstanteArquivo");
            float passo = 2f * fu + 1.3f;
            float a0 = fileirasEmZ ? s.X0 + 1.2f : s.Z0 + 1.2f, a1 = fileirasEmZ ? s.X1 - 1.2f : s.Z1 - 1.2f;
            // do fundo até perto da porta
            float fundoDaSala = s.Face(s.Fundo), perto = s.Face(porta);
            float sentido = Mathf.Sign(perto - fundoDaSala);
            float b0 = fundoDaSala + sentido * 0.8f, b1 = perto - sentido * 2.2f;
            for (float a = a0 + fu; a <= a1 - fu; a += passo)
            {
                for (float b = b0 + sentido * l / 2f; sentido > 0 ? b <= b1 - l / 2f : b >= b1 + l / 2f; b += sentido * (l + 0.02f))
                {
                    foreach (float lado in new[] { -1f, 1f })
                    {
                        float x = fileirasEmZ ? a + lado * (fu / 2f + 0.01f) : b, z = fileirasEmZ ? b : a + lado * (fu / 2f + 0.01f);
                        float giro = fileirasEmZ ? (lado < 0 ? -90f : 90f) : (lado < 0 ? 180f : 0f);
                        s.Por("EstanteArquivo", x, z, giro, 0f, true);
                    }
                }
            }
            s.Mesa(porta);
            s.Parede("Copiadora", 1, 0.1f, porta);
        }

        // os escritórios: as baias em duas fileiras no meio (cada uma com a cadeira e o computador), os arquivos e a
        // copiadora nas paredes, o quadro branco e o vaso
        static void Escritorio(Sala s)
        {
            float b = Sala.Largura("EstacaoDeTrabalho");
            // 1,8 m das paredes: um arquivo ou a copiadora encostados (uns 0,8 m de fundo) ainda deixam 1 m de passagem até
            // as divisórias das baias
            const float margem = 1.8f;
            int colunas = Mathf.Max(1, Mathf.FloorToInt((s.W - 2f * margem) / (b + 0.25f)));
            int linhas = Mathf.Max(1, Mathf.FloorToInt((s.D - 2f * margem) / (b + 1.0f)));
            for (int j = 0; j < linhas; j++)
                for (int i = 0; i < colunas; i++)
                {
                    float x = Mathf.Lerp(s.X0 + margem + b / 2f, s.X1 - margem - b / 2f, colunas == 1 ? 0.5f : i / (float)(colunas - 1));
                    float z = Mathf.Lerp(s.Z0 + margem + b / 2f, s.Z1 - margem - b / 2f, linhas == 1 ? 0.5f : j / (float)(linhas - 1));
                    float giro = j % 2 == 0 ? 0f : 180f;
                    if (!s.Por("EstacaoDeTrabalho", x, z, giro)) continue;
                    var q = Quaternion.Euler(0f, giro, 0f);
                    // o computador na mesa de trás, na frente do vão da cadeira (o canto da frente do lado de -x da baia)
                    var pc = new Vector3(x, 0f, z) + q * new Vector3(-0.2f, 0f, -0.55f);
                    s.Por("Computador", pc.x, pc.z, giro, 0.74f * MedidasDaMobilia.Escala, true);
                    var c = new Vector3(x, 0f, z) + q * new Vector3(-0.43f, 0f, 0.45f);
                    s.Por("CadeiraEscritorio", c.x, c.z, giro + 180f + (float)(s.rnd.NextDouble() * 60 - 30), 0f, true);
                }
            s.Parede("Arquivo", 3, 0.05f);
            s.Parede("Copiadora");
            s.NaParede("QuadroBranco", 0.9f);
            s.Parede("VasoPlanta");
        }

        // a sala do diretor: a mesa grande de frente para a porta (a cadeira dele entre a mesa e a janela), as duas cadeiras
        // das visitas, o canto do sofá com o tapete, as estantes de livros, o quadro, o arquivo e os vasos
        static void Diretor(Sala s)
        {
            int porta = s.LadoDaPorta, fu = s.Fundo;
            var (lo, hi) = s.Ao(fu);
            float meio = Mathf.Lerp(lo, hi, 0.62f);
            var pm = s.Ponto(fu, meio, 1.6f);
            float giroMesa = Sala.GiroDoLado(fu) + 180f;
            if (s.Por("MesaDiretor", pm.x, pm.y, giroMesa))
            {
                var pc = s.Ponto(fu, meio, 0.75f);
                s.Por("CadeiraEscritorio", pc.x, pc.y, Sala.GiroDoLado(fu), 0f, true);
                foreach (float d in new[] { -0.55f, 0.55f })
                {
                    var pv = s.Ponto(fu, meio + d, 2.95f);
                    s.Por("CadeiraEstofada", pv.x, pv.y, giroMesa + (float)(s.rnd.NextDouble() * 20 - 10), 0f, true);
                }
            }
            // o canto do sofá, do outro lado
            var ps = s.Ponto(fu, Mathf.Lerp(lo, hi, 0.2f), 2.3f);
            if (s.Por("TapetePersa", ps.x, ps.y, Sala.GiroDoLado(fu), 0f, true))
            {
                s.Por("MesaDeCentro", ps.x, ps.y, Sala.GiroDoLado(fu), 0f, true);
                var p2 = s.Ponto(fu, Mathf.Lerp(lo, hi, 0.2f), 1.05f);
                s.Por("Sofa2", p2.x, p2.y, Sala.GiroDoLado(fu), 0f, true);
                var p1 = s.Ponto(fu, Mathf.Lerp(lo, hi, 0.2f), 3.5f);
                s.Por("Sofa1", p1.x, p1.y, Sala.GiroDoLado(fu) + 180f, 0f, true);
            }
            s.Parede("EstanteLivros", 2, 0.05f, (fu + 1) % 4, (fu + 3) % 4);
            s.NaParede("QuadroPaisagem", 1.4f, (fu + 1) % 4, (fu + 3) % 4, porta);
            s.Parede("Arquivo");
            s.Parede("VasoPlanta", 2);
        }
    }
}
