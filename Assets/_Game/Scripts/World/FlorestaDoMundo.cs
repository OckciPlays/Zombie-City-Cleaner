using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A floresta na cena (Plano_Mapa, Parte M9a): as plantas de <see cref="Floresta"/> dos blocos até <see cref="raio"/> m
    /// do jogador local, desenhadas em instâncias (Graphics.RenderMeshInstanced, uma chamada por espécie e LOD, sem um
    /// objeto por planta), e a colisão dos troncos. Nada vai pela rede: cada máquina planta a mesma floresta da semente.
    ///  - As plantas de um bloco saem em outra thread (<see cref="Floresta.DoBloco"/>), já com a matriz de cada uma; a grama
    ///    baixa (<see cref="Floresta.GramaDoBloco"/>), à parte, só nos blocos perto do jogador.
    ///  - A cada quadro, os blocos na vista da câmera (e os de perto, para as sombras de trás) entram nas listas da espécie
    ///    e do LOD de cada planta. Cada tipo tem o seu alcance e as suas trocas de LOD, longe do jogador (a pedido: perto,
    ///    a troca aparecia): as árvores até o fim (o _Longe1 a <see cref="lod1"/> m e o _Longe2 a <see cref="lod2"/> m), o chão da mata até <see cref="alcanceDoChao"/> m, o capim
    ///    até <see cref="alcanceDoCapim"/> m e a grama até <see cref="alcanceDaGrama"/> m. O que tem alcance curto encolhe
    ///    até sumir no último quarto dele (o _Some do shader), sem estalar; a grama, o capim e a taboa têm a luz do chão
    ///    (o _Cima). Fazem sombra as árvores de perto e o LOD0 do
    ///    chão da mata (os arbustos, as pedras, os tocos e os troncos); a grama, o capim, as samambaias e a taboa, não.
    ///  - A colisão: nos blocos em que o terreno tem colisão (<see cref="TerrenoDoMundo.ComColisao"/>: perto de todos os
    ///    jogadores e dos carros no anfitrião, do jogador local no cliente), um objeto por bloco com uma cápsula por tronco
    ///    (em pé; deitada no tronco caído, num filho girado), na camada Default: o NavMesh do anfitrião junta. Montada aos
    ///    poucos, com um limite de tempo por quadro.
    ///  - O vento: a direção vira devagar e as rajadas vêm e vão (o global _ZccVento do shader da vegetação).
    /// Fica no objeto "Mundo" da cena Mundo; as malhas e o material vêm da montagem da cena.
    /// </summary>
    public class FlorestaDoMundo : MonoBehaviour
    {
        [Tooltip("O material da vegetação (shader ZombieCityCleaner/Vegetacao, com instâncias).")]
        public Material material;
        [Tooltip("As malhas, 3 por espécie (a de perto, a _Longe1 e a _Longe2), na ordem de Floresta.Nomes.")]
        public Mesh[] malhas;
        [Tooltip("Até onde as árvores aparecem em volta do jogador local (m). A câmera corta a 150 m, na neblina.")]
        public float raio = 165f;
        [Tooltip("Onde as árvores trocam para a _Longe1 e para a _Longe2 (m).")]
        public float lod1 = 55f, lod2 = 100f;
        [Tooltip("Até onde aparecem os arbustos, as samambaias, os tocos, os troncos caídos, a taboa e as pedras pequenas (m).")]
        public float alcanceDoChao = 75f;
        [Tooltip("Até onde aparece o capim alto (m).")]
        public float alcanceDoCapim = 55f;
        [Tooltip("Até onde aparece a grama baixa (m); só os blocos até aí montam a grama.")]
        public float alcanceDaGrama = 38f;
        [Tooltip("Tempo máximo por quadro (ms) para a colisão dos troncos.")]
        [Min(0.2f)] public float orcamentoMs = 1f;
        public float intervaloDeRevisao = 0.25f;
        [Header("Vento")]
        [Tooltip("Quanto o alto das árvores (16 m) anda com o vento (m).")]
        public float forcaDoVento = 0.22f;
        [Tooltip("A direção média do vento no chão (x, z); a mesma da neblina.")]
        public Vector2 direcaoDoVento = new Vector2(0.92f, 0.38f);

        public static FlorestaDoMundo Instancia { get; private set; }
        /// <summary>O maior tempo de um quadro da floresta desde a última leitura (ms; quem mede zera).</summary>
        public static float MaiorQuadroMs;

        public int Blocos => _blocos.Count;
        public int PlantasNaVista { get; private set; }
        public int Troncos { get; private set; }

        sealed class Plantas
        {
            public byte[] Especies;
            public Vector3[] Posicoes;
            public float[] Giros, Escalas;
            public Matrix4x4[] Matrizes;
            public Bounds Caixa;
        }

        sealed class Bloco
        {
            public Vector2Int Chave;
            public Task<Plantas> Montando, MontandoGrama;
            public Plantas Plantas, Grama;
            public bool QuerGrama;
            public GameObject Colisao;
            public int ProximoTronco;
            public bool ColisaoFeita;
            public float QueridoEm;
        }

        static readonly int VentoId = Shader.PropertyToID("_ZccVento");
        static readonly int SomeId = Shader.PropertyToID("_Some");
        static readonly int CimaId = Shader.PropertyToID("_Cima");
        readonly Dictionary<Vector2Int, Bloco> _blocos = new Dictionary<Vector2Int, Bloco>();
        readonly List<Bloco> _prontos = new List<Bloco>();
        readonly List<Bloco> _sobra = new List<Bloco>();
        readonly Plane[] _planos = new Plane[6];
        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();
        Matrix4x4[][] _listas;
        int[] _contagem;
        // por espécie: o alcance, as trocas de LOD (ao quadrado), quais LODs fazem sombra e o bloco do _Some
        float[] _alcance2, _troca1, _troca2;
        bool[,] _sombra;
        MaterialPropertyBlock[] _blocosDoSome;
        DadosDoMundo _dados;
        Transform _raiz;
        float _proximaRevisao;

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            Limpar();
            Shader.SetGlobalVector(VentoId, Vector4.zero);
            if (Instancia == this) Instancia = null;
        }

        void Limpar()
        {
            foreach (var b in _blocos.Values) if (b.Colisao != null) Destroy(b.Colisao);
            _blocos.Clear();
            _prontos.Clear();
            if (_raiz != null) Destroy(_raiz.gameObject);
            _raiz = null;
            _dados = null;
        }

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var dados = mundo != null ? mundo.Dados : null;
            if (dados != _dados)
            {
                Limpar();
                _dados = dados;
                if (dados != null) _raiz = new GameObject("Floresta").transform;
            }
            if (_dados?.Floresta == null || malhas == null || malhas.Length < Floresta.Quantas * 3) return;
            Preparar();
            _relogio.Restart();
            if (Time.unscaledTime >= _proximaRevisao)
            {
                _proximaRevisao = Time.unscaledTime + intervaloDeRevisao;
                Revisar();
            }
            Receber();
            Colidir();
            MaiorQuadroMs = Mathf.Max(MaiorQuadroMs, (float)_relogio.Elapsed.TotalMilliseconds);
            Ventar();
        }

        void LateUpdate()
        {
            if (_dados?.Floresta == null || material == null || _listas == null) return;
            Desenhar();
        }

        void Preparar()
        {
            int n = Floresta.Quantas;
            if (_listas != null && _listas.Length == n * 3) return;
            _listas = new Matrix4x4[n * 3][];
            for (int k = 0; k < _listas.Length; k++) _listas[k] = new Matrix4x4[64];
            _contagem = new int[n * 3];
            _alcance2 = new float[n];
            _troca1 = new float[n];
            _troca2 = new float[n];
            _sombra = new bool[n, 3];
            _blocosDoSome = new MaterialPropertyBlock[n];
            for (int e = 0; e < n; e++)
            {
                var esp = (Especie)e;
                float alcance = raio, t1 = lod1, t2 = lod2;
                bool sombra0 = true, sombra1 = true;
                if (Floresta.EhGrama(esp)) { alcance = alcanceDaGrama; t1 = 18f; t2 = 30f; sombra0 = sombra1 = false; }
                else if (esp == Especie.CapimA || esp == Especie.CapimB || esp == Especie.Taboa || esp == Especie.SamambaiaA || esp == Especie.SamambaiaB)
                {
                    alcance = esp == Especie.CapimA || esp == Especie.CapimB ? alcanceDoCapim : alcanceDoChao;
                    t1 = 25f; t2 = 45f;
                    sombra0 = sombra1 = false;
                }
                else if (!Floresta.EhArvore(esp))
                {
                    // os arbustos, os tocos, os troncos caídos e as pedras (a pedra grande vai até o fim)
                    alcance = esp == Especie.PedraC || esp == Especie.TroncoCaidoA ? raio : alcanceDoChao;
                    t1 = 30f; t2 = 60f;
                    sombra1 = false;
                }
                _alcance2[e] = alcance * alcance;
                _troca1[e] = t1 * t1;
                _troca2[e] = t2 * t2;
                _sombra[e, 0] = sombra0;
                _sombra[e, 1] = sombra1;
                _sombra[e, 2] = false;
                if (alcance < raio)
                {
                    _blocosDoSome[e] = new MaterialPropertyBlock();
                    _blocosDoSome[e].SetVector(SomeId, new Vector4(alcance * 0.75f, alcance, 0f, 0f));
                    // a grama, o capim e a taboa com a luz do chão
                    if (Floresta.EhGrama(esp) || esp == Especie.CapimA || esp == Especie.CapimB || esp == Especie.Taboa)
                        _blocosDoSome[e].SetFloat(CimaId, 0.8f);
                }
            }
        }

        // ------------------------------------------------------------------ os blocos em volta

        void Revisar()
        {
            Vector3 centro;
            var local = JogadorRede.Local;
            if (local != null) centro = local.transform.position;
            else if (Camera.main != null) centro = Camera.main.transform.position;
            else return;
            float agora = Time.unscaledTime;
            foreach (var b in _blocos.Values) b.QuerGrama = false;
            var min = _dados.BlocoDe(centro.x - raio, centro.z - raio);
            var max = _dados.BlocoDe(centro.x + raio, centro.z + raio);
            var floresta = _dados.Floresta;
            var dados = _dados;
            for (int bz = Mathf.Max(0, min.y); bz <= Mathf.Min(_dados.BlocosPorLado - 1, max.y); bz++)
            for (int bx = Mathf.Max(0, min.x); bx <= Mathf.Min(_dados.BlocosPorLado - 1, max.x); bx++)
            {
                var c = _dados.CentroDoBloco(bx, bz);
                float d = Vector2.Distance(c, new Vector2(centro.x, centro.z)) - DadosDoMundo.TamanhoDoBloco * 0.71f;
                if (d > raio) continue;
                var chave = new Vector2Int(bx, bz);
                int x = bx, z = bz;
                if (!_blocos.TryGetValue(chave, out var b))
                {
                    b = new Bloco { Chave = chave };
                    _blocos.Add(chave, b);
                    b.Montando = Task.Run(() => Montar(dados, floresta, x, z, false));
                }
                b.QueridoEm = agora;
                // a grama: os blocos que encostam no alcance dela (com uma folga, para já estar pronta ao chegar)
                if (d < alcanceDaGrama + 24f)
                {
                    b.QuerGrama = true;
                    if (b.Grama == null && b.MontandoGrama == null) b.MontandoGrama = Task.Run(() => Montar(dados, floresta, x, z, true));
                }
            }
            // os que ficaram longe por um tempo saem (a folga evita plantar e arrancar na beira); a grama sai antes
            _sobra.Clear();
            foreach (var b in _blocos.Values)
            {
                if (!b.QuerGrama && b.MontandoGrama == null) b.Grama = null;
                if (agora - b.QueridoEm > 3f && b.Montando == null && b.MontandoGrama == null) _sobra.Add(b);
            }
            foreach (var b in _sobra)
            {
                if (b.Colisao != null) Destroy(b.Colisao);
                _blocos.Remove(b.Chave);
                _prontos.Remove(b);
            }
        }

        // em outra thread: as plantas do bloco (ou a grama dele) e as matrizes delas
        static Plantas Montar(DadosDoMundo dados, Floresta floresta, int bx, int bz, bool grama)
        {
            var lista = new List<PlantaDaFloresta>(grama ? 1600 : 256);
            if (grama)
            {
                floresta.GramaDoBloco(bx, bz, lista);
                floresta.FrestasDoBloco(bx, bz, lista);   // o mato nas frestas das ruas (Parte M9c)
            }
            else floresta.DoBloco(bx, bz, lista);
            int n = lista.Count;
            var p = new Plantas
            {
                Especies = new byte[n], Posicoes = new Vector3[n], Giros = new float[n], Escalas = new float[n], Matrizes = new Matrix4x4[n],
            };
            float y0 = float.MaxValue, y1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var pl = lista[i];
                p.Especies[i] = (byte)pl.Especie;
                p.Posicoes[i] = pl.Posicao;
                p.Giros[i] = pl.Giro;
                p.Escalas[i] = pl.Escala;
                float r = pl.Giro * Mathf.Deg2Rad, c = Mathf.Cos(r) * pl.Escala, s = Mathf.Sin(r) * pl.Escala;
                var m = new Matrix4x4();
                m.m00 = c; m.m02 = s; m.m03 = pl.Posicao.x;
                m.m11 = pl.Escala; m.m13 = pl.Posicao.y;
                m.m20 = -s; m.m22 = c; m.m23 = pl.Posicao.z;
                m.m33 = 1f;
                p.Matrizes[i] = m;
                y0 = Mathf.Min(y0, pl.Posicao.y);
                y1 = Mathf.Max(y1, pl.Posicao.y);
            }
            var canto = dados.CantoDoBloco(bx, bz);
            if (n == 0) y0 = y1 = 0f;
            // a caixa do bloco com a copa mais alta (e mais larga) que sai dele
            p.Caixa = new Bounds();
            p.Caixa.SetMinMax(new Vector3(canto.x - 6f, y0 - 1f, canto.y - 6f),
                              new Vector3(canto.x + DadosDoMundo.TamanhoDoBloco + 6f, y1 + 22f, canto.y + DadosDoMundo.TamanhoDoBloco + 6f));
            return p;
        }

        void Receber()
        {
            foreach (var b in _blocos.Values)
            {
                if (b.Montando != null && b.Montando.IsCompleted)
                {
                    if (b.Montando.IsFaulted) Debug.LogException(b.Montando.Exception);
                    else
                    {
                        b.Plantas = b.Montando.Result;
                        _prontos.Add(b);
                    }
                    b.Montando = null;
                }
                if (b.MontandoGrama != null && b.MontandoGrama.IsCompleted)
                {
                    if (b.MontandoGrama.IsFaulted) Debug.LogException(b.MontandoGrama.Exception);
                    else if (b.QuerGrama) b.Grama = b.MontandoGrama.Result;
                    b.MontandoGrama = null;
                }
            }
        }

        // ------------------------------------------------------------------ a colisão dos troncos

        void Colidir()
        {
            var terreno = TerrenoDoMundo.Instancia;
            int troncos = 0;
            foreach (var b in _prontos)
            {
                bool quer = terreno != null && terreno.ComColisao(b.Chave);
                if (!quer)
                {
                    if (b.Colisao != null)
                    {
                        Destroy(b.Colisao);
                        b.Colisao = null;
                        b.ProximoTronco = 0;
                        b.ColisaoFeita = false;
                    }
                    continue;
                }
                if (!b.ColisaoFeita && _relogio.Elapsed.TotalMilliseconds < orcamentoMs) PorTroncos(b);
                if (b.Colisao != null) troncos += b.ProximoTronco;
            }
            Troncos = troncos;
        }

        // continua as cápsulas do bloco de onde parou, até acabar o tempo do quadro
        void PorTroncos(Bloco b)
        {
            var p = b.Plantas;
            if (b.Colisao == null)
            {
                var canto = _dados.CantoDoBloco(b.Chave.x, b.Chave.y);
                b.Colisao = new GameObject($"Troncos ({b.Chave.x}, {b.Chave.y})");
                b.Colisao.transform.SetParent(_raiz, false);
                b.Colisao.transform.position = new Vector3(canto.x, 0f, canto.y);
            }
            var origem = b.Colisao.transform.position;
            for (; b.ProximoTronco < p.Especies.Length; b.ProximoTronco++)
            {
                if (_relogio.Elapsed.TotalMilliseconds > orcamentoMs) return;
                int e = p.Especies[b.ProximoTronco];
                float r = Floresta.RaioDoTronco[e], h = Floresta.AlturaDoTronco[e];
                if (r <= 0f) continue;
                float k = p.Escalas[b.ProximoTronco];
                var pos = p.Posicoes[b.ProximoTronco];
                if (Floresta.Deitada((Especie)e))
                {
                    var filho = new GameObject("Tronco caído");
                    filho.transform.SetParent(b.Colisao.transform, false);
                    filho.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, p.Giros[b.ProximoTronco], 0f));
                    var deitada = filho.AddComponent<CapsuleCollider>();
                    deitada.direction = 0;
                    deitada.radius = r * k;
                    deitada.height = h * k;
                    deitada.center = new Vector3(0f, r * 0.85f * k, 0f);
                    continue;
                }
                var c = b.Colisao.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = r * k;
                c.height = Mathf.Max(h * k, r * k * 2f);
                c.center = pos - origem + new Vector3(0f, c.height * 0.5f, 0f);
            }
            b.ColisaoFeita = true;
        }

        // ------------------------------------------------------------------ o desenho

        void Desenhar()
        {
            var cam = Camera.main;
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, _planos);
            var c = cam.transform.position;
            const float perto = 48f;
            System.Array.Clear(_contagem, 0, _contagem.Length);
            int vistas = 0;
            foreach (var b in _prontos)
            {
                // fora da vista, só os blocos de perto (as sombras deles podem cair na frente)
                var caixa = b.Plantas.Caixa;
                float dx0 = Mathf.Max(0f, Mathf.Max(caixa.min.x - c.x, c.x - caixa.max.x));
                float dz0 = Mathf.Max(0f, Mathf.Max(caixa.min.z - c.z, c.z - caixa.max.z));
                if (dx0 * dx0 + dz0 * dz0 > perto * perto && !GeometryUtility.TestPlanesAABB(_planos, caixa)) continue;
                vistas += Juntar(b.Plantas, c);
                if (b.Grama != null) vistas += Juntar(b.Grama, c);
            }
            PlantasNaVista = vistas;

            var rp = new RenderParams(material)
            {
                layer = 0,
                receiveShadows = true,
                worldBounds = new Bounds(c, Vector3.one * (raio * 2f + 60f)),
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.BlendProbes,
            };
            for (int k = 0; k < _listas.Length; k++)
            {
                int n = _contagem[k];
                var malha = malhas[k];
                if (n == 0 || malha == null) continue;
                int e = k / 3;
                rp.shadowCastingMode = _sombra[e, k % 3] ? ShadowCastingMode.On : ShadowCastingMode.Off;
                rp.matProps = _blocosDoSome[e];
                for (int ini = 0; ini < n; ini += 1023)
                    Graphics.RenderMeshInstanced(rp, malha, 0, _listas[k], Mathf.Min(1023, n - ini), ini);
            }
        }

        // as plantas de um bloco nas listas da espécie e do LOD de cada uma
        int Juntar(Plantas p, Vector3 c)
        {
            int n = 0;
            for (int i = 0; i < p.Especies.Length; i++)
            {
                var pos = p.Posicoes[i];
                float dx = pos.x - c.x, dz = pos.z - c.z;
                float d2 = dx * dx + dz * dz;
                int e = p.Especies[i];
                if (d2 > _alcance2[e]) continue;
                int k = e * 3 + (d2 < _troca1[e] ? 0 : d2 < _troca2[e] ? 1 : 2);
                var lista = _listas[k];
                if (_contagem[k] == lista.Length)
                {
                    System.Array.Resize(ref lista, lista.Length * 2);
                    _listas[k] = lista;
                }
                lista[_contagem[k]++] = p.Matrizes[i];
                n++;
            }
            return n;
        }

        // a direção vira devagar e as rajadas vêm e vão
        void Ventar()
        {
            float t = Time.time;
            float giro = Mathf.Sin(t * 0.031f) * 35f + Mathf.Sin(t * 0.087f + 1.3f) * 12f;
            var dir = (Vector2)(Quaternion.Euler(0f, 0f, giro) * direcaoDoVento.normalized);
            float rajada = Mathf.Clamp(Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 0.83f + 1.1f) * 0.4f + Mathf.Sin(t * 2.3f) * 0.15f, -1f, 1f);
            Shader.SetGlobalVector(VentoId, new Vector4(dir.x, dir.y, forcaDoVento * (1f + 0.35f * rajada), rajada));
        }
    }
}
