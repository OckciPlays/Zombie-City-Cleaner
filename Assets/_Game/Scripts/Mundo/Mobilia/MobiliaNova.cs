using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A mobília nova no mundo (Plano_Mobilia.md da pasta de arte, a etapa 4, 29/10/2026): o que vai em cada ponto de móvel
    /// novo (o tipo do ponto é o nome do modelo, <see cref="MoveisDaObra"/>). Um ponto é um conjunto: o móvel e o que vem
    /// com ele, como no mostruário (o que vai em cima, <see cref="CatalogoDaMobilia.Lugar.Sobre"/>: a TV no rack, o abajur
    /// na mesa de canto, o computador na mesa de escritório; as cadeiras em volta da mesa e na frente da de escritório). O
    /// pé do ponto (<see cref="Pe"/>) é o do conjunto inteiro, e o meio dele fica no centro do ponto. Tudo em ×1,30
    /// (<see cref="MedidasDaMobilia.Escala"/>), sem esticar.
    ///  - <see cref="Pecas"/>: os modelos de um ponto no lugar (no espaço da obra), com a variação de cada um (sorteada pela
    ///    semente: a mesma em todas as máquinas; os módulos e os eletrodomésticos de uma cozinha com a mesma);
    ///  - <see cref="Caixas"/>: a colisão (uma caixa por móvel que fica no chão, como a mobília antiga, com o recuo dela);
    ///  - <see cref="Desenho"/>: os de longe de todos os pontos numa malha (<see cref="MalhaDaMobilia"/>), depois de
    ///    <see cref="Registrar"/> (na thread principal, no começo).
    /// Tudo aqui é seguro em várias threads (as obras são montadas em várias).
    /// </summary>
    public static class MobiliaNova
    {
        /// <summary>Um modelo de um ponto no lugar (no espaço da obra).</summary>
        public struct Peca
        {
            public string modelo;
            /// <summary>A origem do modelo, o giro em y (graus) e a variação (o índice nas do catálogo).</summary>
            public Vector3 origem;
            public float giro;
            public int variacao;
            /// <summary>O meio da caixa do modelo (embaixo dela) e o tamanho dela, no jogo.</summary>
            public Vector3 meio, tamanho;
            /// <summary>Encosta no chão (tem a caixa de colisão): não os que vão em cima nem os pendurados nem os tapetes.</summary>
            public bool caixa;
            /// <summary>O índice dele no conjunto do ponto (0 é o móvel do ponto).</summary>
            public int indice;

            public Matrix4x4 Matriz => Matrix4x4.TRS(origem, Quaternion.Euler(0f, giro, 0f), Vector3.one * MedidasDaMobilia.Escala);
        }

        // uma peça do conjunto: o meio da caixa dela no espaço do conjunto (o meio do pé dele na origem), o giro, a altura da
        // origem do modelo (no tamanho real) e se ela usa a variação do móvel do ponto
        struct PecaDoConjunto
        {
            public string modelo;
            public Vector2 meio;
            public float giro, y;
            public bool mesmaVariacao, caixa;
        }

        sealed class Conjunto
        {
            public PecaDoConjunto[] pecas;
            /// <summary>No jogo: largura (x), altura, fundo (z).</summary>
            public Vector3 pe;
        }

        static readonly Dictionary<string, Conjunto> _conjuntos = MontarConjuntos();

        /// <summary>O tipo de ponto é um modelo novo.</summary>
        public static bool EhNovo(string tipo) => tipo != null && _conjuntos.ContainsKey(tipo);

        /// <summary>O pé do conjunto do modelo, no jogo: largura (x), altura (a mais alta das peças), fundo (z).</summary>
        public static Vector3 Pe(string modelo) => _conjuntos[modelo].pe;

        static Dictionary<string, Conjunto> MontarConjuntos()
        {
            // o que vai em cima de cada móvel: o primeiro do catálogo
            var emCima = new Dictionary<string, (string modelo, float altura)>();
            foreach (var t in CatalogoDaMobilia.Tipos)
                if (t.lugar == CatalogoDaMobilia.Lugar.Sobre && t.sobre != null)
                    foreach (var (anfitriao, altura) in t.sobre)
                        if (!emCima.ContainsKey(anfitriao)) emCima[anfitriao] = (t.modelo, altura);

            var r = new Dictionary<string, Conjunto>();
            foreach (var t in CatalogoDaMobilia.Tipos)
            {
                if (!MedidasDaMobilia.Tem(t.modelo) || t.lugar == CatalogoDaMobilia.Lugar.Sobre || t.lugar == CatalogoDaMobilia.Lugar.ComAMesa) continue;
                var pecas = new List<PecaDoConjunto>();
                var b = MedidasDaMobilia.Real(t.modelo);
                bool noChao = t.lugar != CatalogoDaMobilia.Lugar.Alto && !t.modelo.StartsWith("Tapete");
                pecas.Add(new PecaDoConjunto { modelo = t.modelo, mesmaVariacao = true, caixa = noChao });
                // na mesa com o vão da cadeira, o que vai em cima e a cadeira ficam na frente do vão
                float vx = t.vao != 0f ? t.vao - MedidasDaMobilia.Meio(t.modelo).x : 0f;
                if (emCima.TryGetValue(t.modelo, out var s) && MedidasDaMobilia.Tem(s.modelo))
                    pecas.Add(new PecaDoConjunto { modelo = s.modelo, meio = new Vector2(vx, 0f), y = s.altura });
                if (t.cadeira != null && MedidasDaMobilia.Tem(t.cadeira))
                {
                    float cd = MedidasDaMobilia.Real(t.cadeira).z;
                    if (t.lugar == CatalogoDaMobilia.Lugar.Parede)
                        // a de escritório: na frente do vão, um pouco embaixo do tampo e virada de leve
                        pecas.Add(new PecaDoConjunto { modelo = t.cadeira, meio = new Vector2(vx, b.z / 2f + cd / 2f - 0.15f), giro = 172f, mesmaVariacao = true, caixa = true });
                    else
                    {
                        // em volta da mesa (como no mostruário, sem a puxada: ela passava do pé do ponto), a frente para a mesa
                        float z = b.z / 2f + cd / 2f - 0.14f;
                        if (t.redonda)
                            for (int k = 0; k < t.cadeiras; k++)
                            {
                                float a = 360f * k / t.cadeiras;
                                var c = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, -z);
                                pecas.Add(new PecaDoConjunto { modelo = t.cadeira, meio = new Vector2(c.x, c.z), giro = a + (k == 0 ? 12f : 0f), mesmaVariacao = true, caixa = true });
                            }
                        else
                        {
                            int porLado = Mathf.Max(1, t.cadeiras / 2);
                            for (int lado = 0; lado < 2; lado++)
                                for (int k = 0; k < porLado; k++)
                                {
                                    float x = (k + 0.5f) / porLado * b.x - b.x / 2f;
                                    bool torta = lado == 0 && k == porLado - 1;
                                    pecas.Add(new PecaDoConjunto { modelo = t.cadeira, meio = new Vector2(x, lado == 0 ? -z : z), giro = (lado == 0 ? 0f : 180f) + (torta ? -14f : 0f),
                                                                   mesmaVariacao = true, caixa = true });
                                }
                        }
                    }
                }
                // o pé do conjunto (as caixas das peças, giradas) e o meio dele na origem
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue, topo = 0f;
                foreach (var q in pecas)
                {
                    var m = MedidasDaMobilia.Real(q.modelo);
                    float g = q.giro * Mathf.Deg2Rad, cs = Mathf.Abs(Mathf.Cos(g)), sn = Mathf.Abs(Mathf.Sin(g));
                    float ex = (cs * m.x + sn * m.z) / 2f, ez = (sn * m.x + cs * m.z) / 2f;
                    x0 = Mathf.Min(x0, q.meio.x - ex); x1 = Mathf.Max(x1, q.meio.x + ex);
                    z0 = Mathf.Min(z0, q.meio.y - ez); z1 = Mathf.Max(z1, q.meio.y + ez);
                    topo = Mathf.Max(topo, q.y + MedidasDaMobilia.Baixo(q.modelo) + m.y);
                }
                var centro = new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f);
                for (int k = 0; k < pecas.Count; k++)
                {
                    var q = pecas[k];
                    q.meio -= centro;
                    pecas[k] = q;
                }
                r[t.modelo] = new Conjunto
                {
                    pecas = pecas.ToArray(),
                    pe = new Vector3(x1 - x0, topo, z1 - z0) * MedidasDaMobilia.Escala,
                };
            }
            return r;
        }

        /// <summary>
        /// A sorte da variação do ponto i de uma obra: a mesma em todas as máquinas; os módulos e os eletrodomésticos da
        /// cozinha de um andar, com a mesma (a cozinha toda de uma cor).
        /// </summary>
        public static uint Sorte(PontoDeMovel p, int semente, int i)
        {
            var t = CatalogoDaMobilia.DoModelo(p.tipo);
            bool daCozinha = t != null && t.grupo == CatalogoDaMobilia.Grupo.Cozinha && (t.junto || p.tipo.StartsWith("Geladeira"));
            return Mobilia.Sorte(semente, daCozinha ? 1000000 + p.andar : i);
        }

        /// <summary>As peças do ponto (nas coordenadas da obra, como o ponto), em 'saida'.</summary>
        public static void Pecas(PontoDeMovel p, uint sorte, List<Peca> saida)
        {
            if (!_conjuntos.TryGetValue(p.tipo, out var c)) return;
            float e = MedidasDaMobilia.Escala;
            var giroDoPonto = Quaternion.Euler(0f, p.giro, 0f);
            for (int k = 0; k < c.pecas.Length; k++)
            {
                var q = c.pecas[k];
                var t = CatalogoDaMobilia.DoModelo(q.modelo);
                int n = t?.variacoes?.Length ?? 1;
                uint s = q.mesmaVariacao ? sorte : (uint)(Sorteio.Misturar(((ulong)sorte << 8) ^ (ulong)(k + 1)) >> 32);
                var meio = p.centro + giroDoPonto * new Vector3(q.meio.x, 0f, q.meio.y) * e;
                float giro = p.giro + q.giro;
                var mm = MedidasDaMobilia.Meio(q.modelo);
                var origem = meio - Quaternion.Euler(0f, giro, 0f) * new Vector3(mm.x, 0f, mm.y) * e;
                origem.y = p.centro.y + p.altura + q.y * e;
                meio.y = origem.y + MedidasDaMobilia.Baixo(q.modelo) * e;
                saida.Add(new Peca
                {
                    modelo = q.modelo, origem = origem, giro = giro, variacao = (int)(s % (uint)Mathf.Max(1, n)),
                    meio = meio, tamanho = MedidasDaMobilia.Real(q.modelo) * e, caixa = q.caixa, indice = k,
                });
            }
        }

        [System.ThreadStatic] static List<Peca> _pecas;

        /// <summary>
        /// A colisão dos pontos novos (nas coordenadas deles): uma caixa por móvel no chão, com o recuo da mobília
        /// (<see cref="Mobilia.Recuo"/>: o vão entre dois móveis fica largo para o zumbi no NavMesh). A mais alta que o
        /// zumbi vai em camadas (<see cref="Camadas"/>): o NavMesh vê a caixa oca, e dentro da do beliche (2,15 m de alto e
        /// 1 m de largo) ficava um chão sem caminho.
        /// </summary>
        public static void Caixas(List<PontoDeMovel> pontos, MalhaDeObra colisao)
        {
            if (pontos == null || colisao == null) return;
            _pecas ??= new List<Peca>();
            foreach (var p in pontos)
            {
                if (!EhNovo(p.tipo)) continue;
                _pecas.Clear();
                Pecas(p, 0, _pecas);
                foreach (var q in _pecas)
                {
                    if (!q.caixa) continue;
                    int n = Camadas(q.tamanho.y);
                    float h = q.tamanho.y / n;
                    var tam = new Vector3(Mathf.Max(0.1f, q.tamanho.x - 2 * Mobilia.Recuo), h, Mathf.Max(0.1f, q.tamanho.z - 2 * Mobilia.Recuo));
                    for (int c = 0; c < n; c++)
                        colisao.Caixa(Matrix4x4.TRS(q.meio + Vector3.up * (h * (c + 0.5f)), Quaternion.Euler(0f, q.giro, 0f), Vector3.one), tam, "Concreto");
                }
            }
        }

        /// <summary>Em quantas caixas, uma em cima da outra, vai a colisão de um móvel desta altura (cada uma mais baixa que o zumbi, 1,8 m).</summary>
        public static int Camadas(float altura) => altura > 1.7f ? Mathf.CeilToInt(altura / 0.85f) : 1;

        // ------------------------------------------------------------ os de longe

        static volatile Dictionary<string, MalhaDaMobilia.Modelo> _longe = new Dictionary<string, MalhaDaMobilia.Modelo>();

        /// <summary>Os de longe já estão registrados (as obras ganham o desenho dos móveis novos).</summary>
        public static bool Pronta => _longe.Count > 0;

        /// <summary>
        /// Copia as malhas de longe de cada modelo (o nome e a malha legível; sem o de longe, a de perto). Na thread
        /// principal, antes de montar as obras.
        /// </summary>
        public static void Registrar(IEnumerable<(string modelo, Mesh longe)> malhas)
        {
            var novos = new Dictionary<string, MalhaDaMobilia.Modelo>();
            if (malhas != null)
                foreach (var (modelo, m) in malhas)
                    if (m != null && m.isReadable && MedidasDaMobilia.Tem(modelo)) novos[modelo] = MalhaDaMobilia.Modelo.De(m);
            _longe = novos;
        }

        public static void Limpar() => _longe = new Dictionary<string, MalhaDaMobilia.Modelo>();

        /// <summary>
        /// Os de longe dos pontos novos numa malha (nas coordenadas dos pontos), cada um com a variação dele; null se não há
        /// ponto novo ou os modelos não foram registrados.
        /// </summary>
        public static MalhaDaMobilia Desenho(List<PontoDeMovel> pontos, int semente)
        {
            var modelos = _longe;
            if (pontos == null || modelos.Count == 0) return null;
            _pecas ??= new List<Peca>();
            MalhaDaMobilia r = null;
            for (int i = 0; i < pontos.Count; i++)
            {
                var p = pontos[i];
                if (!EhNovo(p.tipo)) continue;
                _pecas.Clear();
                Pecas(p, Sorte(p, semente, i), _pecas);
                foreach (var q in _pecas)
                {
                    if (!modelos.TryGetValue(q.modelo, out var m)) continue;
                    var t = CatalogoDaMobilia.DoModelo(q.modelo);
                    r ??= MalhaDaMobilia.Emprestar();
                    r.Juntar(m, q.Matriz, t?.variacoes != null && t.variacoes.Length > 0 ? t.variacoes[q.variacao] : null);
                }
            }
            return r;
        }

        // ------------------------------------------------------------ as chaves (as partes que abrem, pela rede)

        /// <summary>A chave de uma obra: pelo lugar dela no mundo (o mesmo em todas as máquinas) e o andar.</summary>
        public static ulong ChaveDaObra(Vector3 lugar, int andar)
        {
            ulong x = (ulong)(uint)Mathf.RoundToInt(lugar.x * 10f), z = (ulong)(uint)Mathf.RoundToInt(lugar.z * 10f);
            return Sorteio.Misturar((x << 32) ^ z ^ ((ulong)(uint)(andar + 1000) << 48));
        }

        /// <summary>A chave de uma parte que abre: a obra, o ponto, a peça do ponto e a parte da peça.</summary>
        public static ulong ChaveDaParte(ulong obra, int ponto, int peca, int parte) =>
            Sorteio.Misturar(obra ^ ((ulong)(uint)ponto << 20) ^ ((ulong)(uint)peca << 10) ^ (ulong)(uint)parte);
    }
}
