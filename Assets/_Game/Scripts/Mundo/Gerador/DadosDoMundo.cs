using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>O tamanho do mapa: o lado do quadrado (Plano_Mapa, "Os tamanhos e o que cada mapa tem").</summary>
    public enum TamanhoDoMundo : byte { Pequeno, Medio, Grande }

    public static class Tamanhos
    {
        public static readonly TamanhoDoMundo[] Todos = { TamanhoDoMundo.Pequeno, TamanhoDoMundo.Medio, TamanhoDoMundo.Grande };

        /// <summary>O lado do mapa, em metros.</summary>
        public static int Lado(TamanhoDoMundo t) => t switch
        {
            TamanhoDoMundo.Pequeno => 3000,
            TamanhoDoMundo.Medio => 5000,
            _ => 8000,
        };

        public static string Nome(TamanhoDoMundo t) => t switch
        {
            TamanhoDoMundo.Pequeno => "Pequeno",
            TamanhoDoMundo.Medio => "Médio",
            _ => "Grande",
        };

        public static string NomeComLado(TamanhoDoMundo t) => $"{Nome(t)} ({Lado(t) / 1000} km)";

        /// <summary>Lê o tamanho da linha de comando: "pequeno", "medio" (ou "médio"), "grande", a inicial ou os km.</summary>
        public static bool Ler(string texto, out TamanhoDoMundo tamanho)
        {
            switch ((texto ?? "").Trim().ToLowerInvariant())
            {
                case "pequeno": case "p": case "3": tamanho = TamanhoDoMundo.Pequeno; return true;
                case "medio": case "médio": case "m": case "5": tamanho = TamanhoDoMundo.Medio; return true;
                case "grande": case "g": case "8": tamanho = TamanhoDoMundo.Grande; return true;
                default: tamanho = TamanhoDoMundo.Grande; return false;
            }
        }
    }

    /// <summary>O que o anfitrião escolhe no menu: o tamanho, a semente (0 = sortear) e o nome (para mostrar e, depois, salvar).</summary>
    public struct PedidoDoMundo
    {
        public TamanhoDoMundo tamanho;
        public int semente;
        public string nome;

        public static PedidoDoMundo Padrao => new PedidoDoMundo { tamanho = TamanhoDoMundo.Grande, nome = "Mundo novo" };
    }

    /// <summary>
    /// O lago do mapa (só um, com a casa no meio, na ilha; Plano_Mapa, decidido em 26/10/2026). A margem é irregular:
    /// <see cref="Q"/> dá a distância relativa até ela (1 na margem).
    /// </summary>
    public sealed class Lago
    {
        public Vector2 Centro;
        /// <summary>O raio médio da margem (m).</summary>
        public float Raio;
        /// <summary>A altura da água.</summary>
        public float Nivel;
        /// <summary>A fundura no meio (m).</summary>
        public float Fundura;
        public float RaioDaIlha;
        /// <summary>A fase do contorno da margem.</summary>
        public float Fase;
        internal Ruido Contorno;

        /// <summary>A distância relativa até a margem: 0 no meio, 1 na margem, mais que 1 fora.</summary>
        public float Q(float x, float z)
        {
            float dx = x - Centro.x, dz = z - Centro.y;
            float m = Mathf.Sqrt(dx * dx + dz * dz);
            if (m < 1e-3f) return 0f;
            return m / RaioNaDirecao(dx / m, dz / m);
        }

        /// <summary>O raio da margem na direção (x, z) unitária.</summary>
        public float RaioNaDirecao(float x, float z) => Raio * (1f + 0.22f * Contorno.Amostra(x * 1.3f + Fase, z * 1.3f - Fase));
    }

    /// <summary>
    /// O mundo gerado, em dados (<see cref="GeradorDoMundo"/>): o mesmo em todas as máquinas, da mesma semente, tamanho e
    /// versão. O que aparece na tela (o terreno em blocos, a água, a prévia do Editor, o mapa) sai daqui.
    ///
    /// As coordenadas são as da Unity: o meio do mapa em (0, 0), x para o leste e z para o norte. A grade de alturas
    /// (um ponto a cada <see cref="Celula"/> m) começa no canto sudoeste (<see cref="MetadeDaGrade"/>) e passa da borda do
    /// mapa por <see cref="Margem"/> m: as serras continuam do lado de fora da parede invisível. Os blocos de 64 m
    /// (<see cref="CelulasPorBloco"/> células) cobrem a grade inteira. Índice da grade: k = j × <see cref="Vertices"/> + i.
    /// </summary>
    public sealed class DadosDoMundo
    {
        public const float TamanhoDoBloco = 64f;
        public const float Celula = 2f;
        public const int CelulasPorBloco = 32;
        /// <summary>Quanto a grade passa da borda do mapa (m).</summary>
        public const float Margem = 192f;
        public const int Jogadores = 4;
        /// <summary>O lado do terreno de concreto onde a família começa (até a casa e o bunker entrarem, na Parte M5).</summary>
        public const float LadoDaPartida = 14f;
        public const float AlturaDaPartida = 0.12f;
        /// <summary>O chão em volta da partida é nivelado até este raio e volta ao relevo até <see cref="FimDoPlato"/>.</summary>
        public const float RaioDoPlato = 17f, FimDoPlato = 44f;

        public readonly int Semente;
        public readonly TamanhoDoMundo Tamanho;
        public readonly int Versao;
        /// <summary>O lado e a metade do mapa (m): a parede invisível fica na metade.</summary>
        public readonly float Lado, Metade;
        /// <summary>Blocos por lado da grade (o mapa e a margem).</summary>
        public readonly int BlocosPorLado;
        public readonly float MetadeDaGrade;
        /// <summary>Células e vértices por lado da grade.</summary>
        public readonly int Celulas, Vertices;
        /// <summary>As serras: a largura da faixa e a altura (m), pelo tamanho do mapa.</summary>
        public readonly float FaixaDaSerra, AlturaDaSerra;
        /// <summary>
        /// A cerca da quarentena (Parte M4): um quadrado na subida da serra, a metade da faixa para dentro da borda
        /// (|x| ou |z| = Cerca). Ninguém passa dela: as saídas acabam nela, na barreira, e a parede invisível fica logo atrás.
        /// </summary>
        public readonly float Cerca;

        /// <summary>A altura de cada vértice da grade.</summary>
        public readonly float[] Alturas;
        public Lago Lago;
        /// <summary>O desenho do mapa: as cidades, os lugares, as estradas, o metrô e os nomes (<see cref="PlanejadorDoMapa"/>).</summary>
        public PlanoDoMapa Plano;
        /// <summary>As estradas no chão: os nós, os trechos, a altura do asfalto e o terreno acomodado (<see cref="RedeDeEstradas"/>).</summary>
        public RedeDeEstradas Rede;
        /// <summary>O que fica na beira das estradas: as placas, os carros abandonados, as blitz, as barreiras e os postos (<see cref="ObjetosDaEstrada"/>).</summary>
        public ObjetosDaEstrada Objetos;
        /// <summary>A floresta entre as cidades: as manchas e as plantas de cada bloco (<see cref="Mundo.Floresta"/>, Parte M9a).</summary>
        public Floresta Floresta;

        /// <summary>Onde a família começa e para onde ela olha (o giro em y, em graus).</summary>
        public Vector3 Partida;
        public float GiroDaPartida;
        /// <summary>O chão mais baixo embaixo da laje da partida (a laje desce até ele).</summary>
        public float FundoDaPartida;
        /// <summary>
        /// O chão em volta da partida é nivelado (o platô). Só fora das cidades: no lote da casa da família (Parte M5) a
        /// laje desce até o chão, e as ruas em volta seguem o chão da cidade.
        /// </summary>
        public bool ComPlato = true;
        /// <summary>Os lugares dos 4 jogadores (na sala da casa da família, ou no terreno da partida), olhando para o mesmo lado.</summary>
        public readonly Vector3[] Nascimentos = new Vector3[Jogadores];
        public readonly float[] GirosDosNascimentos = new float[Jogadores];
        /// <summary>
        /// O lugar do carro da família (a garagem ou a vaga da casa dela, no chão; Parte M6) e o giro dele (a frente para a
        /// rua); null sem a casa (o carro fica ao lado da laje da partida).
        /// </summary>
        public Vector3? LugarDoCarro;
        public float GiroDoCarro;

        /// <summary>O resumo dos números (checksum): igual em todas as máquinas se a geração bateu.</summary>
        public ulong Resumo;

        /// <summary>Os ruídos do relevo e do chão (cada um com a sua tabela, todos da mesma semente; só leitura, seguros em threads).</summary>
        public readonly Ruido Colinas, Vales, Detalhe, Serra, Manchas, Folhas;

        // as células que o terreno pula (porões, bocas do metrô, escadas dos bunkers): marcadas na geração
        readonly HashSet<int> _buracos = new HashSet<int>();

        public DadosDoMundo(int semente, TamanhoDoMundo tamanho)
        {
            Semente = semente;
            Tamanho = tamanho;
            Versao = GeradorDoMundo.Versao;
            Lado = Tamanhos.Lado(tamanho);
            Metade = Lado / 2f;
            BlocosPorLado = Mathf.CeilToInt((Lado + 2f * Margem) / TamanhoDoBloco);
            MetadeDaGrade = BlocosPorLado * TamanhoDoBloco / 2f;
            Celulas = BlocosPorLado * CelulasPorBloco;
            Vertices = Celulas + 1;
            FaixaDaSerra = Mathf.Clamp(Lado * 0.085f, 260f, 620f);
            AlturaDaSerra = Mathf.Clamp(Lado * 0.017f, 60f, 136f);
            Cerca = Metade - FaixaDaSerra * 0.5f;
            Alturas = new float[Vertices * Vertices];
            Colinas = new Ruido(semente, "colinas");
            Vales = new Ruido(semente, "vales");
            Detalhe = new Ruido(semente, "detalhe-do-relevo");
            Serra = new Ruido(semente, "serras");
            Manchas = new Ruido(semente, "manchas-de-terra");
            Folhas = new Ruido(semente, "folhas-mortas");
        }

        /// <summary>A posição (x, z) do vértice (i, j) da grade.</summary>
        public Vector2 PosicaoDoVertice(int i, int j) => new Vector2(-MetadeDaGrade + i * Celula, -MetadeDaGrade + j * Celula);

        public float AlturaDoVertice(int i, int j) => Alturas[j * Vertices + i];

        /// <summary>
        /// A altura EXATA do terreno no ponto: a mesma diagonal dos triângulos da malha de detalhe máximo
        /// (<see cref="MalhasDoTerreno.Terreno"/> com passo 1). Use para encostar coisas no chão.
        /// </summary>
        public float Altura(float x, float z)
        {
            float fx = (x + MetadeDaGrade) / Celula, fz = (z + MetadeDaGrade) / Celula;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, Celulas - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, Celulas - 1);
            float u = Mathf.Clamp01(fx - i), v = Mathf.Clamp01(fz - j);
            float ha = Alturas[j * Vertices + i], hb = Alturas[j * Vertices + i + 1];
            float hc = Alturas[(j + 1) * Vertices + i + 1], hd = Alturas[(j + 1) * Vertices + i];
            if (((i + j) & 1) == 0) // a diagonal a-c
                return v > u ? ha + v * (hd - ha) + u * (hc - hd) : ha + u * (hb - ha) + v * (hc - hb);
            // a diagonal b-d
            return u + v <= 1f ? ha + u * (hb - ha) + v * (hd - ha) : hc + (1f - u) * (hd - hc) + (1f - v) * (hb - hc);
        }

        /// <summary>O ponto está dentro do mapa (com uma margem para dentro).</summary>
        public bool Dentro(float x, float z, float margem = 0f) => Mathf.Abs(x) <= Metade - margem && Mathf.Abs(z) <= Metade - margem;

        /// <summary>O ponto está dentro da cerca da quarentena (com uma margem para dentro; negativa, para fora).</summary>
        public bool DentroDaCerca(float x, float z, float margem = 0f) => Mathf.Abs(x) <= Cerca - margem && Mathf.Abs(z) <= Cerca - margem;

        /// <summary>O bloco (bx, bz) que contém o ponto.</summary>
        public Vector2Int BlocoDe(float x, float z) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((x + MetadeDaGrade) / TamanhoDoBloco), 0, BlocosPorLado - 1),
            Mathf.Clamp(Mathf.FloorToInt((z + MetadeDaGrade) / TamanhoDoBloco), 0, BlocosPorLado - 1));

        /// <summary>O canto sudoeste do bloco (bx, bz), no plano (x, z).</summary>
        public Vector2 CantoDoBloco(int bx, int bz) => new Vector2(-MetadeDaGrade + bx * TamanhoDoBloco, -MetadeDaGrade + bz * TamanhoDoBloco);

        public Vector2 CentroDoBloco(int bx, int bz) => CantoDoBloco(bx, bz) + Vector2.one * (TamanhoDoBloco * 0.5f);

        /// <summary>O quanto o ponto está na serra da borda (0 no meio do mapa, 1 na borda), com a beirada irregular.</summary>
        public float Borda(float x, float z)
        {
            float fora = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float torto = Serra.Amostra(x * 0.0021f + 31f, z * 0.0021f - 17f) * FaixaDaSerra * 0.3f;
            return Geo.Suave(Metade - FaixaDaSerra + torto, Metade + 80f, fora);
        }

        /// <summary>A fundura da água no ponto (0 em terra e fora do lago).</summary>
        public float FunduraDaAgua(float x, float z)
        {
            if (Lago == null || Lago.Q(x, z) > 1.8f) return 0f;
            return Mathf.Max(0f, Lago.Nivel - Altura(x, z));
        }

        // ------------------------------------------------------------------ as cidades (Parte M5)

        // os traçados que mexem no chão de cada bloco (o índice em _tracados)
        int[][] _cidadesDoBloco;
        List<TracadoDaCidade> _tracados;

        /// <summary>Guarda, por bloco, as cidades que mexem no chão dele (no desenho do mapa, logo depois das cidades).</summary>
        public void IndexarCidades(List<TracadoDaCidade> tracados)
        {
            _tracados = tracados;
            var listas = new List<int>[BlocosPorLado * BlocosPorLado];
            for (int t = 0; t < tracados.Count; t++)
            {
                var tr = tracados[t];
                float a = tr.Alcance;
                var min = BlocoDe(tr.Centro.x - a, tr.Centro.y - a);
                var max = BlocoDe(tr.Centro.x + a, tr.Centro.y + a);
                for (int bz = min.y; bz <= max.y; bz++)
                for (int bx = min.x; bx <= max.x; bx++)
                    (listas[bz * BlocosPorLado + bx] ??= new List<int>()).Add(t);
            }
            _cidadesDoBloco = new int[listas.Length][];
            for (int k = 0; k < listas.Length; k++) _cidadesDoBloco[k] = listas[k]?.ToArray();
        }

        /// <summary>Os traçados das cidades que encostam no bloco (null = nenhum).</summary>
        public int[] CidadesDoBloco(int bx, int bz) => _cidadesDoBloco?[bz * BlocosPorLado + bx];

        /// <summary>O traçado da cidade que mais manda no chão do ponto e o peso dela (0 a 1), ou null.</summary>
        public TracadoDaCidade CidadeNoChao(float x, float z, out float peso)
        {
            peso = 0f;
            if (_cidadesDoBloco == null) return null;
            var b = BlocoDe(x, z);
            var lista = _cidadesDoBloco[b.y * BlocosPorLado + b.x];
            if (lista == null) return null;
            TracadoDaCidade melhor = null;
            foreach (int t in lista)
            {
                var tr = _tracados[t];
                float w = tr.PesoDoChao(x, z);
                if (w > peso)
                {
                    peso = w;
                    melhor = tr;
                }
            }
            return melhor;
        }

        /// <summary>O ponto cai numa rua, calçada, construção ou lote ocupado de alguma cidade (<see cref="TracadoDaCidade.Ocupado"/>).</summary>
        public bool OcupadoPelaCidade(Vector2 p, float folga = 0.5f)
        {
            var t = CidadeNoChao(p.x, p.y, out _);
            return t != null && t.Ocupado(p, folga);
        }

        /// <summary>O traçado da cidade (ou vila) que contém o ponto (até onde vão as ruas dela), ou null.</summary>
        public TracadoDaCidade CidadeEm(float x, float z)
        {
            var t = CidadeNoChao(x, z, out float peso);
            return t != null && t.Cidade.Q(x, z) <= t.LimiteDasRuas ? t : null;
        }

        // ------------------------------------------------------------------ os buracos

        /// <summary>Marca como buraco as células cujo meio cai no retângulo (x0, z0)–(x1, z1): o terreno não passa por elas.</summary>
        public void MarcarBuraco(float x0, float z0, float x1, float z1)
        {
            int i0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(x0, x1) + MetadeDaGrade) / Celula - 0.5f));
            int i1 = Mathf.Min(Celulas - 1, Mathf.FloorToInt((Mathf.Max(x0, x1) + MetadeDaGrade) / Celula - 0.5f));
            int j0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(z0, z1) + MetadeDaGrade) / Celula - 0.5f));
            int j1 = Mathf.Min(Celulas - 1, Mathf.FloorToInt((Mathf.Max(z0, z1) + MetadeDaGrade) / Celula - 0.5f));
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                _buracos.Add(j * Celulas + i);
        }

        /// <summary>
        /// Marca como buraco as células que encostam no quadrilátero convexo (os 4 cantos em volta, no plano x, z): o porão
        /// de uma casa girada (Parte M6). Encostar é ter mais de 1 cm em comum.
        /// </summary>
        public void MarcarBuracoEm(Vector2[] cantos)
        {
            const float folga = 0.01f;
            float xa = float.MaxValue, xb = float.MinValue, za = float.MaxValue, zb = float.MinValue;
            foreach (var c in cantos)
            {
                xa = Mathf.Min(xa, c.x); xb = Mathf.Max(xb, c.x);
                za = Mathf.Min(za, c.y); zb = Mathf.Max(zb, c.y);
            }
            int i0 = Mathf.Max(0, Mathf.FloorToInt((xa + folga + MetadeDaGrade) / Celula));
            int i1 = Mathf.Min(Celulas - 1, Mathf.FloorToInt((xb - folga + MetadeDaGrade) / Celula));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((za + folga + MetadeDaGrade) / Celula));
            int j1 = Mathf.Min(Celulas - 1, Mathf.FloorToInt((zb - folga + MetadeDaGrade) / Celula));
            var celula = new Vector2[4];
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x0 = -MetadeDaGrade + i * Celula, z0 = -MetadeDaGrade + j * Celula;
                celula[0] = new Vector2(x0, z0); celula[1] = new Vector2(x0 + Celula, z0);
                celula[2] = new Vector2(x0 + Celula, z0 + Celula); celula[3] = new Vector2(x0, z0 + Celula);
                // separados por alguma aresta do quadrilátero (os eixos x e z já se encostam pela faixa de i e j)
                bool separados = false;
                for (int k = 0; k < cantos.Length && !separados; k++)
                {
                    Vector2 a = cantos[k], b = cantos[(k + 1) % cantos.Length];
                    var n = new Vector2(b.y - a.y, a.x - b.x).normalized;
                    float qa = float.MaxValue, qb = float.MinValue, ca = float.MaxValue, cb = float.MinValue;
                    foreach (var c in cantos) { float d = Vector2.Dot(c, n); qa = Mathf.Min(qa, d); qb = Mathf.Max(qb, d); }
                    foreach (var c in celula) { float d = Vector2.Dot(c, n); ca = Mathf.Min(ca, d); cb = Mathf.Max(cb, d); }
                    separados = cb < qa + folga || ca > qb - folga;
                }
                if (!separados) _buracos.Add(j * Celulas + i);
            }
        }

        /// <summary>A célula (i, j) é buraco (o terreno não passa por ela).</summary>
        public bool Buraco(int i, int j) => _buracos.Count > 0 && _buracos.Contains(j * Celulas + i);

        public int QuantosBuracos => _buracos.Count;

        // ------------------------------------------------------------------ o chão

        /// <summary>
        /// Os pesos das texturas do chão no vértice (i, j), para o shader do chão: R grama seca, G terra, B cascalho e
        /// A folhas mortas (a rocha o shader põe sozinho nas encostas). Calculados na hora, a partir da posição e da altura
        /// (depois de o relevo ficar pronto): manchas de terra e de folhas, o cascalho subindo a serra, a terra na beira e no
        /// fundo do lago e o cascalho na laje da partida.
        /// </summary>
        public Color32 PesosDoChao(int i, int j)
        {
            var p = PosicaoDoVertice(i, j);
            float x = p.x, z = p.y;
            float terra = Geo.Suave(-0.16f, -0.4f, Manchas.Fbm(x / 90f, z / 90f, 3));
            float folhas = Geo.Suave(0.26f, 0.48f, Folhas.Fbm(x / 70f + 5.3f, z / 70f - 3.1f, 3));
            // as folhas que caem da mata e dos plantios (Parte M9a)
            if (Floresta != null) folhas = Mathf.Max(folhas, Floresta.Folhagem(x, z) * (0.55f + 0.45f * Folhas.Amostra01(x / 23f, z / 23f)));
            float cascalho = Geo.Suave(0.2f, 0.65f, Borda(x, z)) * (0.55f + 0.45f * Detalhe.Amostra01(x * 0.03f, z * 0.03f));
            if (Lago != null && Lago.Q(x, z) < 2.2f)
            {
                // a terra molhada na beira da água (pela altura sobre a água, com a beirada irregular) e no fundo
                float acima = AlturaDoVertice(i, j) - Lago.Nivel;
                float beira = 0.9f + 1.1f * Manchas.Amostra01(x * 0.05f, z * 0.05f);
                terra = Mathf.Max(terra, 1f - Geo.Suave(0.25f, beira, acima));
                folhas *= Geo.Suave(0.2f, 1.4f, acima);
            }
            // (com a casa da família, Parte M6, a partida é nela: sem a laje, sem o cascalho em volta)
            float dx = x - Partida.x, dz = z - Partida.z;
            float dp = Mathf.Sqrt(dx * dx + dz * dz);
            if (dp < 26f && !LugarDoCarro.HasValue)
            {
                cascalho = Mathf.Max(cascalho, 1f - Geo.Suave(11f, 24f, dp));
                folhas *= Geo.Suave(12f, 26f, dp);
            }
            // o acostamento das estradas (cascalho; terra nas de terra) e o pátio dos postos
            if (Rede != null)
            {
                float acostado = Rede.Acostado(x, z, out bool deTerra);
                if (acostado > 0f)
                {
                    if (deTerra) terra = Mathf.Max(terra, acostado);
                    else cascalho = Mathf.Max(cascalho, acostado);
                    folhas *= 1f - acostado * 0.85f;
                }
            }
            float outros = Mathf.Max(terra, Mathf.Max(cascalho, folhas));
            float grama = Mathf.Max(0.04f, 1f - outros);
            return new Color32(Byte(grama), Byte(terra), Byte(cascalho), Byte(folhas));
        }

        static byte Byte(float f) => (byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 255f);
    }

    /// <summary>Contas pequenas da geração (seguras fora da thread principal).</summary>
    public static class Geo
    {
        /// <summary>0 até a, 1 a partir de b, suave no meio (como o smoothstep; a pode ser maior que b).</summary>
        public static float Suave(float a, float b, float x)
        {
            float t = (x - a) / (b - a);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}
