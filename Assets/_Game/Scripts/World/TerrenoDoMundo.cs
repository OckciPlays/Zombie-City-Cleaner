using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O terreno do mundo gerado em blocos de 64 m (Plano_Mapa, Parte M2; o WorldStreamer do Guardiões da Floresta). Cada
    /// máquina cria só os blocos em volta de quem precisa:
    ///  - a malha: detalhe máximo perto (passo 1 da grade, 2 m), menos longe (passos 2 e 4); além do raio de desenho, nada
    ///    (a neblina e o corte da câmera a 150 m escondem). As malhas são montadas em outra thread e entregues aos poucos;
    ///  - a colisão: só perto dos jogadores (no anfitrião, de todos e dos carros, porque a física dos carros parados roda
    ///    lá; no cliente, só do jogador local). O cozimento da colisão (Physics.BakeMesh) também roda em outra thread;
    ///  - a água do lago: uma malha por bloco, na camada Water;
    ///  - as estradas (Parte M4, <see cref="MalhasDasEstradas"/>): o asfalto, os pátios dos cruzamentos e o guard-rail (com
    ///    a colisão dele), uma malha por bloco, montada uma vez. Os blocos com estrada ficam no máximo no passo 2 (no 4, o
    ///    relevo grosso cobriria a beira do asfalto nos cortes);
    ///  - sem travadas: o objeto do bloco só nasce quando a malha fica pronta, e cada quadro tem um limite de tempo para
    ///    entregar malhas e colisões.
    /// Outros pedem áreas com <see cref="Pedir"/> (a partida antes de o jogador nascer, o destino do "ir para" do F1) e
    /// perguntam <see cref="Pronto"/> antes de usar a colisão de um lugar. Também ficam aqui o terreno de concreto da
    /// partida e as paredes invisíveis da borda (logo atrás da cerca da quarentena). Nada vai pela rede: cada máquina monta o seu.
    /// Fica no objeto "Mundo" da cena Mundo; quem liga é o <see cref="MundoDaPartida"/>.
    /// </summary>
    public class TerrenoDoMundo : MonoBehaviour
    {
        public Material materialDoChao;
        public Material materialDaAgua;
        [Tooltip("O material da paleta para a laje da partida, as estradas e as cidades: o do abandono (Parte M9c, AbandonoDoMundo).")]
        public Material materialDaCidade;
        [Tooltip("O das casas de verdade (AbandonoDasCasas: a altura sobre o chão na cor do vértice, para a umidade na base). Sem ele, o da cidade.")]
        public Material materialDasCasas;
        [Tooltip("A hera das paredes (o material da vegetação) e as pichações (Pichacao), Parte M9c.")]
        public Material materialDaHera, materialDasPichacoes;
        [Tooltip("Até onde a hera das casas aparece em volta do jogador local (m): as folhas são pequenas, e longe só pesam.")]
        public float raioDaHera = 70f;
        [Tooltip("Até onde os móveis das casas e das lojas aparecem em volta do jogador local (m; a mobília): dentro, no escuro, longe não se vê.")]
        public float raioDaMobilia = 60f;
        [Tooltip("Até onde os blocos aparecem em volta do jogador local (m). A câmera corta a 150 m, na neblina.")]
        public float raioDeDesenho = 180f;
        [Tooltip("Até onde os blocos têm o detalhe máximo (m).")]
        public float raioDeDetalhe = 100f;
        [Tooltip("Até onde os blocos têm colisão em volta de cada jogador (m).")]
        public float raioDeColisao = 90f;
        [Tooltip("Até onde os blocos têm colisão em volta de cada carro, no anfitrião (m).")]
        public float raioDosCarros = 30f;
        [Tooltip("Até onde as casas de verdade (por dentro, com a colisão) aparecem em volta de quem precisa de colisão (m); além, as caixas de longe. Fica dentro do detalhe máximo do terreno (os buracos dos porões só existem nele).")]
        public float raioDasCasas = 95f;
        [Tooltip("Malhas novas entregues por quadro (as montagens rodam em outra thread).")]
        [Min(1)] public int entreguesPorQuadro = 4;
        [Tooltip("Montagens em andamento ao mesmo tempo.")]
        [Min(1)] public int montagensAoMesmoTempo = 8;
        [Tooltip("Tempo máximo por quadro (ms) para entregar malhas e colisões prontas; o resto fica para os próximos.")]
        [Min(0.5f)] public float orcamentoMs = 3f;
        public float intervaloDeRevisao = 0.2f;

        public static TerrenoDoMundo Instancia { get; private set; }
        /// <summary>O maior tempo de um quadro do terreno desde a última leitura (ms; o teste de travessia zera).</summary>
        public static float MaiorQuadroMs;

        public bool Ligado => _dados != null;
        public int Blocos => _blocos.Count;
        public int BlocosComColisao
        {
            get
            {
                int n = 0;
                foreach (var b in _blocos.Values) if (b.ComColisao) n++;
                return n;
            }
        }

        sealed class Bloco
        {
            public Vector2Int Chave;
            public GameObject Objeto;         // null até a primeira malha (ou a água) chegar
            public MeshRenderer Desenho;
            public MeshCollider Colisor;
            public Mesh Malha;
            public int Passo = -1;            // o passo da grade em uso (1, 2 ou 4); -1 = ainda sem malha
            public int PassoQuerido = 4;
            public bool QuerColisao, ComColisao;
            public Task<MalhaDoBloco> MontandoMalha;
            public int PassoMontando;
            public Task<MalhaDoBloco> MontandoAgua;
            public bool AguaFeita;
            public GameObject Agua;
            public Mesh MalhaDaAgua;
            public Task<MalhasDasEstradas.DoBloco> MontandoEstrada;
            public bool EstradaFeita;
            public Mesh MalhaDaEstrada, MalhaDoGuardRail;
            public MeshCollider ColisorDoGuardRail;
            public Task CozimentoDoGuardRail;
            // a cidade (Parte M5): as ruas, as construções e a colisão, uma vez
            public Task<MalhasDaCidade.DoBloco> MontandoCidade;
            public bool CidadeFeita;
            public Mesh MalhaDasRuas, MalhaDasConstrucoes, MalhaDaColisaoDaCidade;
            public MeshCollider ColisorDaCidade;
            public Task CozimentoDaCidade;
            // as casas de longe (as caixas, da cidade) e as de verdade (Parte M6): estas só perto de quem precisa de colisão
            public MeshRenderer CasasDeLonge;
            public Mesh MalhaDasCasasDeLonge;
            public int TemCasas = -1;          // -1 = ainda não sabe; 0 ou 1
            public bool QuerCasas;
            public float CasasQueridasEm;
            public Task<MalhasDasCasas.DoBloco> MontandoCasas;
            public GameObject Casas;
            public Mesh MalhaDasCasas;
            // o abandono das casas (Parte M9c): a hera (só perto; a malha sai depois, FazerHera) e as pichações
            public MalhaDaHera HeraPorFazer;
            public MeshRenderer Hera;
            public Mesh MalhaDaHera, MalhaDasPichacoes;
            // os móveis das casas e das lojas (a mobília): só perto, como a hera (a malha sai depois, FazerDePerto)
            public MalhaDeObra MobiliaPorFazer;
            public MeshRenderer Mobilia;
            public Mesh MalhaDaMobilia;
            public readonly List<(MeshCollider colisor, Mesh malha, Task cozimento)> ColisoesDasCasas = new List<(MeshCollider, Mesh, Task)>();
            // os prédios de verdade (Parte M6, segunda fatia): a casca está nas casas; os andares, em cada um
            public readonly List<AndaresDoPredio> Predios = new List<AndaresDoPredio>();
            public bool CasasProntas;
            public Task Cozimento;
            public float QueridoEm;
        }

        sealed class Pedido
        {
            public Vector3 Ponto;
            public float Raio;
            public bool Colisao;
        }

        static readonly int[] Rodadas = { 0, 1 };
        readonly Dictionary<Vector2Int, Bloco> _blocos = new Dictionary<Vector2Int, Bloco>();
        readonly Dictionary<object, Pedido> _pedidos = new Dictionary<object, Pedido>();
        readonly List<Bloco> _sobra = new List<Bloco>();
        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();
        DadosDoMundo _dados;
        Transform _raiz;
        float _proximaRevisao;

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            Parar();
            if (Instancia == this) Instancia = null;
        }

        /// <summary>Começa a mostrar o mundo gerado: a partida, a borda e os blocos que forem pedidos.</summary>
        public void Comecar(DadosDoMundo dados)
        {
            Parar();
            _dados = dados;
            _proximaRevisao = 0f;
            _raiz = new GameObject("Terreno do mundo").transform;   // na raiz da cena, fora do NetworkObject
            MontarPartida();
            MontarBorda();
        }

        /// <summary>Apaga tudo (a partida acabou).</summary>
        public void Parar()
        {
            foreach (var b in _blocos.Values) Apagar(b);
            _blocos.Clear();
            _pedidos.Clear();
            if (_raiz != null)
            {
                var malhas = new List<Mesh>();
                foreach (var f in _raiz.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null) malhas.Add(f.sharedMesh);
                Destroy(_raiz.gameObject);
                foreach (var m in malhas) Destroy(m);
            }
            _raiz = null;
            _dados = null;
        }

        /// <summary>Pede blocos (e, se quiser, colisão) em volta de um ponto, até <see cref="Esquecer"/>.</summary>
        public void Pedir(object dono, Vector3 ponto, float raio, bool colisao)
        {
            if (!_pedidos.TryGetValue(dono, out var p)) _pedidos[dono] = p = new Pedido();
            p.Ponto = ponto;
            p.Raio = raio;
            p.Colisao = colisao;
        }

        public void Esquecer(object dono) => _pedidos.Remove(dono);

        /// <summary>Todos os blocos que encostam no círculo já têm a malha de detalhe máximo e a colisão (e as casas de verdade, com a colisão delas).</summary>
        public bool Pronto(Vector3 centro, float raio) => Falta(centro, raio, out _) == 0;

        /// <summary>O bloco tem a colisão do chão agora (a floresta põe a dos troncos nos mesmos blocos).</summary>
        public bool ComColisao(Vector2Int chave) => _blocos.TryGetValue(chave, out var b) && b.ComColisao;

        /// <summary>O que falta para <see cref="Pronto"/> (o primeiro bloco), ou null: para a conferência dos prédios.</summary>
        public string QueFalta(Vector3 centro, float raio) => Falta(centro, raio, out var k) switch
        {
            0 => null,
            1 => "sem o mundo",
            2 => $"o bloco ({k.x}, {k.y}) não está montado",
            3 => $"o bloco ({k.x}, {k.y}) não está no detalhe máximo",
            4 => $"o bloco ({k.x}, {k.y}) está sem a colisão",
            _ => $"as casas do bloco ({k.x}, {k.y}) não estão prontas{(_blocos[k].Casas == null ? " (nem montadas)" : "")}",
        };

        // o primeiro bloco que falta no círculo (0: nenhum; 1: sem o mundo; 2: não montado; 3: sem o passo 1; 4: sem a colisão;
        // 5: sem as casas de verdade)
        int Falta(Vector3 centro, float raio, out Vector2Int chave)
        {
            chave = default;
            if (_dados == null) return 1;
            var min = _dados.BlocoDe(centro.x - raio, centro.z - raio);
            var max = _dados.BlocoDe(centro.x + raio, centro.z + raio);
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                if (DistanciaAoBloco(bx, bz, new Vector2(centro.x, centro.z)) > raio) continue;
                chave = new Vector2Int(bx, bz);
                if (!_blocos.TryGetValue(chave, out var b)) return 2;
                if (b.Passo != 1) return 3;
                if (!b.ComColisao) return 4;
                if (b.TemCasas != 0 && !b.CasasProntas && (b.TemCasas == 1 || MalhasDasCasas.TemCasas(_dados, bx, bz))) return 5;
            }
            return 0;
        }

        /// <summary>As casas de verdade (Parte M6) montadas agora, para o F1 e os testes: cada uma com o objeto dela.</summary>
        public int CasasDeVerdade
        {
            get
            {
                int n = 0;
                foreach (var b in _blocos.Values) if (b.Casas != null) n += b.Casas.transform.childCount - 1 - b.Predios.Count;
                return n;
            }
        }

        /// <summary>Os prédios de verdade (Parte M6, segunda fatia) montados agora e os andares carregados neles.</summary>
        public int PrediosDeVerdade
        {
            get
            {
                int n = 0;
                foreach (var b in _blocos.Values) n += b.Predios.Count;
                return n;
            }
        }

        public int AndaresCarregados
        {
            get
            {
                int n = 0;
                foreach (var b in _blocos.Values)
                    foreach (var p in b.Predios) n += p.Carregados;
                return n;
            }
        }

        /// <summary>Os prédios de verdade montados agora (a conferência dos prédios).</summary>
        public void JuntarPredios(List<AndaresDoPredio> lista)
        {
            foreach (var b in _blocos.Values) lista.AddRange(b.Predios);
        }

        void Update()
        {
            if (_dados == null) return;
            _relogio.Restart();
            if (Time.unscaledTime >= _proximaRevisao)
            {
                _proximaRevisao = Time.unscaledTime + intervaloDeRevisao;
                Revisar();
            }
            ComecarMontagens();
            EntregarProntas();
            if (_relogio.Elapsed.TotalMilliseconds < orcamentoMs) FazerDePerto();
            MaiorQuadroMs = Mathf.Max(MaiorQuadroMs, (float)_relogio.Elapsed.TotalMilliseconds);
        }

        // ------------------------------------------------------------------ o que cada bloco precisa

        void Revisar()
        {
            float agora = Time.unscaledTime;
            foreach (var b in _blocos.Values)
            {
                b.QuerColisao = false;
                b.QuerCasas = false;
                b.PassoQuerido = 4;
            }

            var rede = NetworkManager.Singleton;
            bool anfitriao = rede != null && rede.IsServer;
            var local = JogadorRede.Local;
            if (local != null) Querer(local.transform.position, raioDeDesenho, true, agora);
            else if (Camera.main != null && _pedidos.Count == 0) Querer(Camera.main.transform.position, raioDeDesenho, false, agora);
            if (anfitriao)
            {
                // no anfitrião, o chão com colisão em volta de todos os jogadores e dos carros (a física dos parados roda aqui)
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j != local) Querer(j.transform.position, raioDeColisao, true, agora);
                // o carro andando pede o chão de 1,5 s à frente (a 160 km/h, uns 70 m)
                foreach (var c in Carro.Todos)
                {
                    if (c == null) continue;
                    var corpo = c.GetComponent<Rigidbody>();
                    var v = corpo != null && !corpo.isKinematic ? corpo.linearVelocity : Vector3.zero;
                    Querer(c.transform.position + v * 0.75f, raioDosCarros + v.magnitude * 0.75f, true, agora);
                }
            }
            foreach (var p in _pedidos.Values) Querer(p.Ponto, Mathf.Max(p.Raio, 1f), p.Colisao, agora);

            // os que ficaram longe por um tempo saem (a folga evita montar e apagar na beira), mas não no meio de um cozimento
            _sobra.Clear();
            foreach (var b in _blocos.Values)
                if (agora - b.QueridoEm > 3f && !CozinhandoAlgo(b)) _sobra.Add(b);
            foreach (var b in _sobra)
            {
                Apagar(b);
                _blocos.Remove(b.Chave);
            }

            // a colisão que não é mais necessária sai (a malha continua)
            foreach (var b in _blocos.Values)
                if (!b.QuerColisao && b.ComColisao && b.Cozimento == null)
                {
                    b.Colisor.sharedMesh = null;
                    b.Colisor.enabled = false;
                    b.ComColisao = false;
                }
            // as casas de verdade que ficaram longe por um tempo voltam a ser caixas
            foreach (var b in _blocos.Values)
            {
                if (b.QuerCasas) b.CasasQueridasEm = agora;
                else if (b.Casas != null && agora - b.CasasQueridasEm > 3f && !CozinhandoCasas(b)) TirarCasas(b);
            }
            foreach (var b in _blocos.Values)
            {
                if (b.Hera != null) b.Hera.enabled = Perto(b, raioDaHera);
                if (b.Mobilia != null) b.Mobilia.enabled = Perto(b, raioDaMobilia);
            }
        }

        // a malha da hera ou da mobília de um bloco por quadro, e só dos que estão perto: no centro, um bloco tem até ~30
        // mil folhas (130 mil vértices), uns 2 a 4 ms para virar Mesh; junto com a das casas, no mesmo quadro, dava picos
        void FazerDePerto()
        {
            foreach (var b in _blocos.Values)
            {
                if (b.MobiliaPorFazer == null || b.Casas == null || !Perto(b, raioDaMobilia)) continue;
                var m = new GameObject("Mobília");
                m.transform.SetParent(b.Casas.transform, false);
                b.MalhaDaMobilia = b.MobiliaPorFazer.ParaMesh($"Mobília {b.Chave.x}, {b.Chave.y}");
                MalhaDeObra.Devolver(b.MobiliaPorFazer);
                b.MobiliaPorFazer = null;
                m.AddComponent<MeshFilter>().sharedMesh = b.MalhaDaMobilia;
                b.Mobilia = m.AddComponent<MeshRenderer>();
                b.Mobilia.sharedMaterial = materialDasCasas != null ? materialDasCasas : materialDaCidade;
                return;
            }
            foreach (var b in _blocos.Values)
            {
                if (b.HeraPorFazer == null || b.Casas == null || !Perto(b, raioDaHera)) continue;
                var h = new GameObject("Hera");
                h.transform.SetParent(b.Casas.transform, false);
                b.MalhaDaHera = b.HeraPorFazer.ParaMesh($"Hera {b.Chave.x}, {b.Chave.y}");
                MalhaDaHera.Devolver(b.HeraPorFazer);
                b.HeraPorFazer = null;
                h.AddComponent<MeshFilter>().sharedMesh = b.MalhaDaHera;
                b.Hera = h.AddComponent<MeshRenderer>();
                b.Hera.sharedMaterial = materialDaHera;
                b.Hera.shadowCastingMode = ShadowCastingMode.Off;
                return;
            }
        }

        // a hera e a mobília só perto do jogador local (ou da câmera, sem ele)
        bool Perto(Bloco b, float raio)
        {
            var local = JogadorRede.Local;
            var olho = local != null ? local.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            return DistanciaAoBloco(b.Chave.x, b.Chave.y, new Vector2(olho.x, olho.z)) < raio;
        }

        // as colisões ainda cozinhando em outra thread: o bloco (ou as casas dele) espera por elas para sair, e a malha nova
        // espera para entrar. Esperar com o Wait() travava o quadro (na travessia a 160 km/h, um de 305 ms: o cozimento
        // ficava na fila atrás das montagens das casas)
        static bool Cozinhando(Task t) => t != null && !t.IsCompleted;

        static bool CozinhandoCasas(Bloco b)
        {
            foreach (var (_, _, cozimento) in b.ColisoesDasCasas)
                if (Cozinhando(cozimento)) return true;
            foreach (var p in b.Predios)
                if (p != null && p.Cozinhando) return true;
            return false;
        }

        static bool CozinhandoAlgo(Bloco b) =>
            Cozinhando(b.Cozimento) || Cozinhando(b.CozimentoDoGuardRail) || Cozinhando(b.CozimentoDaCidade) || CozinhandoCasas(b);

        float DistanciaAoBloco(int bx, int bz, Vector2 ponto)
        {
            var c = _dados.CentroDoBloco(bx, bz);
            float meio = DadosDoMundo.TamanhoDoBloco * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(ponto.x - c.x) - meio);
            float dz = Mathf.Max(0f, Mathf.Abs(ponto.y - c.y) - meio);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void Querer(Vector3 ponto, float raio, bool colisao, float agora)
        {
            var centro = new Vector2(ponto.x, ponto.z);
            var min = _dados.BlocoDe(centro.x - raio, centro.y - raio);
            var max = _dados.BlocoDe(centro.x + raio, centro.y + raio);
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                float d = DistanciaAoBloco(bx, bz, centro);
                if (d > raio) continue;
                var chave = new Vector2Int(bx, bz);
                if (!_blocos.TryGetValue(chave, out var b))
                {
                    b = new Bloco { Chave = chave };
                    _blocos.Add(chave, b);
                }
                b.QueridoEm = agora;
                int passo = d < raioDeDetalhe ? 1 : d < raio * 0.8f ? 2 : 4;
                if (passo == 4 && (_dados.Rede?.DoBloco(bx, bz) != null || _dados.CidadesDoBloco(bx, bz) != null)) passo = 2;
                if (colisao && d < raioDeColisao)
                {
                    passo = 1;
                    b.QuerColisao = true;
                }
                // as casas de verdade: onde alguém precisa de colisão (e o jogador local, até o raio delas)
                if (colisao && d < Mathf.Min(raio, raioDasCasas)) b.QuerCasas = true;
                if (passo < b.PassoQuerido) b.PassoQuerido = passo;
            }
        }

        // ------------------------------------------------------------------ as montagens em outra thread

        void ComecarMontagens()
        {
            int andando = 0;
            foreach (var b in _blocos.Values)
                if (b.MontandoMalha != null || b.MontandoAgua != null || b.MontandoEstrada != null || b.MontandoCidade != null || b.Cozimento != null ||
                    b.MontandoCasas != null) andando++;
            if (andando >= montagensAoMesmoTempo) return;

            // primeiro o chão onde alguém precisa da colisão (a 160 km/h pela cidade, as casas tiravam a vez dele, e o
            // jogador chegava num bloco ainda sem colisão)
            foreach (var b in _blocos.Values)
            {
                if (andando >= montagensAoMesmoTempo) return;
                if (!b.QuerColisao || b.MontandoMalha != null || b.Passo == b.PassoQuerido) continue;
                ComecarTerreno(b);
                andando++;
            }

            // as casas de verdade de quem pediu (antes dos detalhes do terreno longe: perto, sem elas não se entra em casa)
            foreach (var b in _blocos.Values)
            {
                if (andando >= montagensAoMesmoTempo) return;
                if (!b.QuerCasas || b.Casas != null || b.MontandoCasas != null || b.TemCasas == 0) continue;
                if (b.TemCasas < 0)
                {
                    b.TemCasas = MalhasDasCasas.TemCasas(_dados, b.Chave.x, b.Chave.y) ? 1 : 0;
                    if (b.TemCasas == 0) continue;
                }
                b.MontandoCasas = MontarCasas(_dados, b.Chave);
                andando++;
            }

            // depois, os que ainda não têm nada e então os que mudam de detalhe
            foreach (var rodada in Rodadas)
            foreach (var b in _blocos.Values)
            {
                if (andando >= montagensAoMesmoTempo) return;
                if (b.MontandoMalha != null) continue;
                bool precisa = rodada == 0 ? b.Passo < 0 : b.Passo != b.PassoQuerido;
                if (!precisa) continue;
                ComecarTerreno(b);
                andando++;
            }
        }

        // a malha do chão no detalhe querido e, na primeira vez, a água, a estrada e a cidade do bloco
        void ComecarTerreno(Bloco b)
        {
            b.PassoMontando = b.PassoQuerido;
            b.MontandoMalha = MontarTerreno(_dados, b.Chave, b.PassoQuerido);
            if (!b.AguaFeita && b.MontandoAgua == null) b.MontandoAgua = MontarAgua(_dados, b.Chave);
            if (!b.EstradaFeita && b.MontandoEstrada == null) b.MontandoEstrada = MontarEstrada(_dados, b.Chave);
            if (!b.CidadeFeita && b.MontandoCidade == null) b.MontandoCidade = MontarCidade(_dados, b.Chave);
        }

        // as lambdas ficam em métodos à parte: dentro do laço, o C# criaria a closure a cada bloco percorrido em todo quadro
        static Task<MalhaDoBloco> MontarTerreno(DadosDoMundo d, Vector2Int chave, int passo) =>
            Task.Run(() => MalhasDoTerreno.Terreno(d, chave.x, chave.y, passo));

        static Task<MalhaDoBloco> MontarAgua(DadosDoMundo d, Vector2Int chave) =>
            Task.Run(() => MalhasDoTerreno.Agua(d, chave.x, chave.y));

        static Task<MalhasDasEstradas.DoBloco> MontarEstrada(DadosDoMundo d, Vector2Int chave) =>
            Task.Run(() => MalhasDasEstradas.Bloco(d, chave.x, chave.y));

        static Task<MalhasDaCidade.DoBloco> MontarCidade(DadosDoMundo d, Vector2Int chave) =>
            Task.Run(() => MalhasDaCidade.Bloco(d, chave.x, chave.y));

        static Task<MalhasDasCasas.DoBloco> MontarCasas(DadosDoMundo d, Vector2Int chave) =>
            Task.Run(() => MalhasDasCasas.Bloco(d, chave.x, chave.y));

        static Task Cozinhar(EntityId malha) => Task.Run(() => Physics.BakeMesh(malha, false));

        void EntregarProntas()
        {
            int entregues = 0;
            foreach (var b in _blocos.Values)
            {
                if (entregues >= entreguesPorQuadro || _relogio.Elapsed.TotalMilliseconds > orcamentoMs) break;

                if (b.MontandoMalha != null && b.MontandoMalha.IsCompleted && !Cozinhando(b.Cozimento))
                {
                    var tarefa = b.MontandoMalha;
                    b.MontandoMalha = null;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else
                    {
                        PorMalha(b, tarefa.Result, b.PassoMontando);
                        entregues++;
                    }
                }
                if (b.MontandoAgua != null && b.MontandoAgua.IsCompleted)
                {
                    var tarefa = b.MontandoAgua;
                    b.MontandoAgua = null;
                    b.AguaFeita = true;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else if (!tarefa.Result.Vazia) PorAgua(b, tarefa.Result);
                }
                if (b.CozimentoDoGuardRail != null && b.CozimentoDoGuardRail.IsCompleted)
                {
                    var cozimento = b.CozimentoDoGuardRail;
                    b.CozimentoDoGuardRail = null;
                    if (cozimento.IsFaulted) Debug.LogException(cozimento.Exception);
                    else
                    {
                        b.ColisorDoGuardRail.sharedMesh = b.MalhaDoGuardRail;
                        b.ColisorDoGuardRail.enabled = true;
                    }
                }
                if (b.MontandoEstrada != null && b.MontandoEstrada.IsCompleted)
                {
                    var tarefa = b.MontandoEstrada;
                    b.MontandoEstrada = null;
                    b.EstradaFeita = true;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else
                    {
                        if (!tarefa.Result.Vazio) PorEstrada(b, tarefa.Result);
                        MalhasDasEstradas.Devolver(tarefa.Result);
                    }
                }
                if (b.MontandoCidade != null && b.MontandoCidade.IsCompleted && _relogio.Elapsed.TotalMilliseconds < orcamentoMs)
                {
                    var tarefa = b.MontandoCidade;
                    b.MontandoCidade = null;
                    b.CidadeFeita = true;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else
                    {
                        if (!tarefa.Result.Vazio)
                        {
                            PorCidade(b, tarefa.Result);
                            entregues++;
                        }
                        MalhasDaCidade.Devolver(tarefa.Result);
                    }
                }
                if (b.MontandoCasas != null && b.MontandoCasas.IsCompleted && _relogio.Elapsed.TotalMilliseconds < orcamentoMs)
                {
                    var tarefa = b.MontandoCasas;
                    b.MontandoCasas = null;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else if (b.QuerCasas || Time.unscaledTime - b.CasasQueridasEm < 3f)
                    {
                        PorCasas(b, tarefa.Result);
                        entregues++;
                    }
                    else MalhasDasCasas.Devolver(tarefa.Result);
                }
                if (b.Casas != null && !b.CasasProntas) ConferirAsColisoesDasCasas(b);
                if (b.CozimentoDaCidade != null && b.CozimentoDaCidade.IsCompleted)
                {
                    var cozimento = b.CozimentoDaCidade;
                    b.CozimentoDaCidade = null;
                    if (cozimento.IsFaulted) Debug.LogException(cozimento.Exception);
                    else
                    {
                        b.ColisorDaCidade.sharedMesh = b.MalhaDaColisaoDaCidade;
                        b.ColisorDaCidade.enabled = true;
                    }
                }
                if (b.Cozimento != null && b.Cozimento.IsCompleted)
                {
                    var cozimento = b.Cozimento;
                    b.Cozimento = null;
                    if (cozimento.IsFaulted) Debug.LogException(cozimento.Exception);
                    else if (b.QuerColisao && b.Passo == 1)
                    {
                        b.Colisor.sharedMesh = b.Malha;
                        if (b.Colisor.sharedMesh == b.Malha)
                        {
                            b.Colisor.enabled = true;
                            b.ComColisao = true;
                        }
                        else
                        {
                            // a Unity recusou a malha: nunca deixa um colisor ligado e vazio (o jogador cairia por ele)
                            Debug.LogWarning($"[Mundo] O bloco {b.Chave} não aceitou a malha de colisão; tentando de novo.");
                            b.Colisor.sharedMesh = null;
                            b.Colisor.enabled = false;
                        }
                    }
                }
                // a colisão: só com a malha de detalhe máximo, cozida em outra thread
                if (b.QuerColisao && !b.ComColisao && b.Cozimento == null && b.Passo == 1 && b.MontandoMalha == null)
                    b.Cozimento = Cozinhar(b.Malha.GetEntityId());
            }
        }

        // ------------------------------------------------------------------ os objetos da Unity

        // o objeto (a malha, o desenho e a colisão) só nasce com a primeira malha: pedir muitos blocos de uma vez (um
        // jogador que vai longe) não cria dezenas de objetos num quadro. Os objetos não são reaproveitados entre blocos (no
        // Guardiões, reaproveitados, a Unity às vezes recusava a malha no MeshCollider)
        void CriarObjeto(Bloco b)
        {
            if (b.Objeto != null) return;
            // "Chão": o nome que os passos reconhecem como grama (PassosJogador)
            var go = new GameObject("Chão");
            go.transform.SetParent(_raiz, false);
            var malha = new Mesh { name = $"Terreno {b.Chave.x}, {b.Chave.y}" };
            malha.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var desenho = go.AddComponent<MeshRenderer>();
            desenho.sharedMaterial = materialDoChao;
            desenho.enabled = false;
            var colisor = go.AddComponent<MeshCollider>();
            colisor.enabled = false;
            (b.Objeto, b.Desenho, b.Colisor, b.Malha) = (go, desenho, colisor, malha);
        }

        void PorMalha(Bloco b, MalhaDoBloco m, int passo)
        {
            CriarObjeto(b);
            // a colisão usa a mesma malha: trocar de detalhe refaz a colisão
            if (b.ComColisao)
            {
                b.Colisor.sharedMesh = null;
                b.Colisor.enabled = false;
                b.ComColisao = false;
            }
            if (b.Cozimento != null && !b.Cozimento.IsCompleted) b.Cozimento.Wait();   // não troca a malha no meio do cozimento
            b.Cozimento = null;
            m.Para(b.Malha);
            b.Passo = passo;
            b.Desenho.enabled = true;
        }

        void PorAgua(Bloco b, MalhaDoBloco m)
        {
            CriarObjeto(b);
            var go = new GameObject("Água") { layer = 4 };   // a camada Water
            go.transform.SetParent(b.Objeto.transform, false);
            var malha = new Mesh { name = $"Água {b.Chave.x}, {b.Chave.y}" };
            m.Para(malha);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = materialDaAgua;
            r.shadowCastingMode = ShadowCastingMode.Off;
            b.Agua = go;
            b.MalhaDaAgua = malha;
        }

        // a estrada do bloco: o desenho (a paleta) e a colisão do guard-rail, filhos do bloco
        void PorEstrada(Bloco b, MalhasDasEstradas.DoBloco m)
        {
            CriarObjeto(b);
            var go = new GameObject("Estrada");
            go.transform.SetParent(b.Objeto.transform, false);
            b.MalhaDaEstrada = m.Desenho.ParaMesh($"Estrada {b.Chave.x}, {b.Chave.y}");
            go.AddComponent<MeshFilter>().sharedMesh = b.MalhaDaEstrada;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
            if (m.Colisao != null && m.Colisao.Triangulos > 0)
            {
                b.MalhaDoGuardRail = m.Colisao.ParaMesh($"Guard-rail {b.Chave.x}, {b.Chave.y}");
                var rail = new GameObject("Guard-rail");
                rail.transform.SetParent(go.transform, false);
                // a colisão é cozida em outra thread e entra quando fica pronta
                b.ColisorDoGuardRail = rail.AddComponent<MeshCollider>();
                b.ColisorDoGuardRail.enabled = false;
                b.CozimentoDoGuardRail = Cozinhar(b.MalhaDoGuardRail.GetEntityId());
            }
        }

        // a cidade do bloco: as ruas e as construções (a paleta) e a colisão (cozida em outra thread), filhas do bloco
        void PorCidade(Bloco b, MalhasDaCidade.DoBloco m)
        {
            CriarObjeto(b);
            var go = new GameObject("Cidade");
            go.transform.SetParent(b.Objeto.transform, false);
            b.MalhaDasRuas = m.Ruas.ParaMesh($"Ruas {b.Chave.x}, {b.Chave.y}");
            go.AddComponent<MeshFilter>().sharedMesh = b.MalhaDasRuas;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
            if (m.Construcoes.Triangulos > 0)
            {
                // "Construção": o nome que os passos reconhecem como concreto
                var c = new GameObject("Construção");
                c.transform.SetParent(go.transform, false);
                b.MalhaDasConstrucoes = m.Construcoes.ParaMesh($"Construções {b.Chave.x}, {b.Chave.y}");
                c.AddComponent<MeshFilter>().sharedMesh = b.MalhaDasConstrucoes;
                c.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
            }
            if (m.Colisao.Triangulos > 0)
            {
                b.MalhaDaColisaoDaCidade = m.Colisao.ParaMesh($"Colisão da cidade {b.Chave.x}, {b.Chave.y}");
                var col = new GameObject("Colisão da cidade");
                col.transform.SetParent(go.transform, false);
                b.ColisorDaCidade = col.AddComponent<MeshCollider>();
                b.ColisorDaCidade.enabled = false;
                b.CozimentoDaCidade = Cozinhar(b.MalhaDaColisaoDaCidade.GetEntityId());
            }
            // as casas de longe (Parte M6): somem enquanto as de verdade estão no bloco
            if (m.CasasDeLonge.Triangulos > 0)
            {
                var c = new GameObject("Casas de longe");
                c.transform.SetParent(go.transform, false);
                b.MalhaDasCasasDeLonge = m.CasasDeLonge.ParaMesh($"Casas de longe {b.Chave.x}, {b.Chave.y}");
                c.AddComponent<MeshFilter>().sharedMesh = b.MalhaDasCasasDeLonge;
                b.CasasDeLonge = c.AddComponent<MeshRenderer>();
                b.CasasDeLonge.sharedMaterial = materialDaCidade;
                b.CasasDeLonge.enabled = b.Casas == null;
            }
        }

        // ------------------------------------------------------------------ as casas de verdade (Parte M6)

        // o desenho das casas do bloco (uma malha) e, para cada casa, um objeto "Casa" no lugar dela: a colisão (cozida em
        // outra thread), os pisos dos cômodos (os passos procuram o PisoPorZona no colisor), a caixa de dentro (a neblina) e
        // o eco. As caixas de longe somem
        void PorCasas(Bloco b, MalhasDasCasas.DoBloco m)
        {
            if (m.Vazio)
            {
                b.TemCasas = 0;
                return;
            }
            CriarObjeto(b);
            b.Casas = new GameObject("Casas");
            b.Casas.transform.SetParent(b.Objeto.transform, false);
            var desenho = new GameObject("Desenho das casas");
            desenho.transform.SetParent(b.Casas.transform, false);
            b.MalhaDasCasas = m.Desenho.ParaMesh($"Casas {b.Chave.x}, {b.Chave.y}");
            desenho.AddComponent<MeshFilter>().sharedMesh = b.MalhaDasCasas;
            desenho.AddComponent<MeshRenderer>().sharedMaterial = materialDasCasas != null ? materialDasCasas : materialDaCidade;
            // o abandono (Parte M9c): a hera fica guardada (a malha sai depois, perto: FazerHera), e as pichações
            if (m.Hera != null && m.Hera.Folhas > 0 && materialDaHera != null)
            {
                b.HeraPorFazer = m.Hera;
                m.Hera = null;
            }
            // a mobília: também guardada, e a malha sai perto
            if (m.Mobilia != null && m.Mobilia.Triangulos > 0)
            {
                b.MobiliaPorFazer = m.Mobilia;
                m.Mobilia = null;
            }
            if (m.Pichacoes != null && m.Pichacoes.Quadros > 0 && materialDasPichacoes != null)
            {
                var pich = new GameObject("Pichações");
                pich.transform.SetParent(b.Casas.transform, false);
                b.MalhaDasPichacoes = m.Pichacoes.ParaMesh($"Pichações {b.Chave.x}, {b.Chave.y}");
                pich.AddComponent<MeshFilter>().sharedMesh = b.MalhaDasPichacoes;
                var r = pich.AddComponent<MeshRenderer>();
                r.sharedMaterial = materialDasPichacoes;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var c in m.Casas)
            {
                // (as casas e as lojas: Parte M6, primeira e terceira fatias)
                var go = new GameObject(c.Nome);
                go.transform.SetParent(b.Casas.transform, false);
                go.transform.SetPositionAndRotation(c.Posicao, Quaternion.Euler(0f, c.Giro, 0f));
                MoveisDaConstrucao.Por(go, c.Moveis);
                // a mobília nova (os de longe ficam com ela)
                MobiliaDaConstrucao.Por(go, c.Moveis, c.MobiliaNova, c.Semente, -100);
                c.MobiliaNova = null;
                var malha = c.Colisao.ParaMesh($"{c.Nome} (colisão)");
                var colisor = go.AddComponent<MeshCollider>();
                colisor.enabled = false;
                b.ColisoesDasCasas.Add((colisor, malha, Cozinhar(malha.GetEntityId())));
                var pisos = go.AddComponent<PisoPorZona>();
                pisos.zonas = new Bounds[c.Zonas.Count];
                pisos.superficies = new Superficie[c.Zonas.Count];
                for (int i = 0; i < c.Zonas.Count; i++)
                {
                    pisos.zonas[i] = new Bounds(c.Zonas[i].Centro, c.Zonas[i].Tamanho);
                    pisos.superficies[i] = SomDoPiso(c.Zonas[i].Piso);
                }
                pisos.resto = Superficie.Concreto;
                var dentro = new GameObject("Dentro");
                dentro.transform.SetParent(go.transform, false);
                dentro.transform.localPosition = c.CentroDeDentro;
                dentro.AddComponent<AreaInterna>().tamanho = c.TamanhoDeDentro;
                var eco = new GameObject("Eco").AddComponent<AudioReverbZone>();
                eco.transform.SetParent(go.transform, false);
                eco.transform.localPosition = c.CentroDeDentro;
                eco.reverbPreset = AudioReverbPreset.Room;
                eco.minDistance = Mathf.Min(c.Largura, c.Fundo) * 0.4f;
                eco.maxDistance = Mathf.Max(c.Largura, c.Fundo) * 0.6f;
            }
            // os prédios (Parte M6, segunda fatia): a colisão da casca, os pisos do térreo, a caixa de dentro, as janelas
            // escuras de cada andar e quem carrega os andares por dentro
            foreach (var pr in m.Predios)
            {
                var go = new GameObject(pr.Nome);
                go.transform.SetParent(b.Casas.transform, false);
                go.transform.SetPositionAndRotation(pr.Posicao, Quaternion.Euler(0f, pr.Giro, 0f));
                var malha = pr.Colisao.ParaMesh($"{pr.Nome} (colisão)");
                var colisor = go.AddComponent<MeshCollider>();
                colisor.enabled = false;
                b.ColisoesDasCasas.Add((colisor, malha, Cozinhar(malha.GetEntityId())));
                var pisos = go.AddComponent<PisoPorZona>();
                pisos.zonas = new Bounds[pr.Zonas.Count];
                pisos.superficies = new Superficie[pr.Zonas.Count];
                for (int i = 0; i < pr.Zonas.Count; i++)
                {
                    pisos.zonas[i] = new Bounds(pr.Zonas[i].Centro, pr.Zonas[i].Tamanho);
                    pisos.superficies[i] = SomDoPiso(pr.Zonas[i].Piso);
                }
                pisos.resto = Superficie.Concreto;
                var dentro = new GameObject("Dentro");
                dentro.transform.SetParent(go.transform, false);
                dentro.transform.localPosition = pr.CentroDeDentro;
                dentro.AddComponent<AreaInterna>().tamanho = pr.TamanhoDeDentro;
                var escuros = new MeshRenderer[pr.Escuros.Length];
                for (int f = 0; f < pr.Escuros.Length; f++)
                {
                    if (pr.Escuros[f].Triangulos == 0) continue;
                    var e = new GameObject($"Janelas escuras do andar {f}");
                    e.transform.SetParent(go.transform, false);
                    e.AddComponent<MeshFilter>().sharedMesh = pr.Escuros[f].ParaMesh($"{pr.Nome}: janelas escuras {f}");
                    escuros[f] = e.AddComponent<MeshRenderer>();
                    escuros[f].sharedMaterial = materialDaCidade;
                    escuros[f].shadowCastingMode = ShadowCastingMode.Off;
                }
                var andares = go.AddComponent<AndaresDoPredio>();
                andares.Ligar(pr.Planta, escuros, materialDaCidade);
                b.Predios.Add(andares);
            }
            MalhasDasCasas.Devolver(m);
            if (b.CasasDeLonge != null) b.CasasDeLonge.enabled = false;
        }

        // o som dos passos de cada piso (os nomes das cores da paleta)
        internal static Superficie SomDoPiso(string piso) => piso switch
        {
            "Taco" or "Madeira" or "MadeiraVelha" => Superficie.Madeira,
            "Carpete" => Superficie.Carpete,
            _ => Superficie.Concreto,
        };

        // as colisões das casas cozidas entram; com todas, as casas do bloco ficam prontas
        static void ConferirAsColisoesDasCasas(Bloco b)
        {
            bool todas = true;
            for (int i = 0; i < b.ColisoesDasCasas.Count; i++)
            {
                var (colisor, malha, cozimento) = b.ColisoesDasCasas[i];
                if (cozimento == null) continue;
                if (!cozimento.IsCompleted)
                {
                    todas = false;
                    continue;
                }
                if (cozimento.IsFaulted) Debug.LogException(cozimento.Exception);
                colisor.sharedMesh = malha;
                colisor.enabled = true;
                b.ColisoesDasCasas[i] = (colisor, malha, null);
            }
            b.CasasProntas = todas;
        }

        // as casas de verdade saem (o objeto antes das malhas); as caixas de longe voltam
        static void TirarCasas(Bloco b)
        {
            foreach (var (_, _, cozimento) in b.ColisoesDasCasas)
                if (cozimento != null && !cozimento.IsCompleted) cozimento.Wait();
            if (b.Casas != null) Destroy(b.Casas);   // os prédios soltam os andares e as janelas escuras deles
            if (b.MalhaDasCasas != null) Destroy(b.MalhaDasCasas);
            if (b.MalhaDaHera != null) Destroy(b.MalhaDaHera);
            if (b.MalhaDasPichacoes != null) Destroy(b.MalhaDasPichacoes);
            if (b.MalhaDaMobilia != null) Destroy(b.MalhaDaMobilia);
            b.MalhaDaHera = b.MalhaDasPichacoes = b.MalhaDaMobilia = null;
            b.Hera = b.Mobilia = null;
            MalhaDaHera.Devolver(b.HeraPorFazer);
            b.HeraPorFazer = null;
            MalhaDeObra.Devolver(b.MobiliaPorFazer);
            b.MobiliaPorFazer = null;
            foreach (var (_, malha, _) in b.ColisoesDasCasas) Destroy(malha);
            b.ColisoesDasCasas.Clear();
            b.Predios.Clear();
            b.Casas = null;
            b.MalhaDasCasas = null;
            b.CasasProntas = false;
            if (b.CasasDeLonge != null) b.CasasDeLonge.enabled = true;
        }

        static void Apagar(Bloco b)
        {
            // as tarefas em andamento terminam sozinhas, e o resultado é ignorado; o objeto sai antes das malhas
            if (b.Cozimento != null && !b.Cozimento.IsCompleted) b.Cozimento.Wait();
            if (b.CozimentoDoGuardRail != null && !b.CozimentoDoGuardRail.IsCompleted) b.CozimentoDoGuardRail.Wait();
            if (b.CozimentoDaCidade != null && !b.CozimentoDaCidade.IsCompleted) b.CozimentoDaCidade.Wait();
            TirarCasas(b);
            if (b.MalhaDasCasasDeLonge != null) Destroy(b.MalhaDasCasasDeLonge);
            if (b.Objeto != null) Destroy(b.Objeto);   // a água, a estrada e a cidade são filhas
            if (b.Malha != null) Destroy(b.Malha);
            if (b.MalhaDaAgua != null) Destroy(b.MalhaDaAgua);
            if (b.MalhaDaEstrada != null) Destroy(b.MalhaDaEstrada);
            if (b.MalhaDoGuardRail != null) Destroy(b.MalhaDoGuardRail);
            if (b.MalhaDasRuas != null) Destroy(b.MalhaDasRuas);
            if (b.MalhaDasConstrucoes != null) Destroy(b.MalhaDasConstrucoes);
            if (b.MalhaDaColisaoDaCidade != null) Destroy(b.MalhaDaColisaoDaCidade);
        }

        // o terreno de concreto da partida (com a colisão girada com ele) e as paredes invisíveis da borda; com a casa da
        // família (Parte M6), a partida é nela, e não há laje
        void MontarPartida()
        {
            if (_dados.Plano?.LoteDaFamilia?.Casa != null) return;
            var partida = new GameObject("Partida (terreno da família)");
            partida.transform.SetParent(_raiz, false);
            partida.AddComponent<MeshFilter>().sharedMesh = MalhasDoTerreno.Partida(_dados);
            partida.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
            var colisao = new GameObject("Colisão da partida");
            colisao.transform.SetParent(partida.transform, false);
            colisao.transform.SetPositionAndRotation(_dados.Partida + Vector3.up * (DadosDoMundo.AlturaDaPartida - 0.15f), Quaternion.Euler(0f, _dados.GiroDaPartida, 0f));
            colisao.AddComponent<BoxCollider>().size = new Vector3(DadosDoMundo.LadoDaPartida, 0.3f, DadosDoMundo.LadoDaPartida);
        }

        void MontarBorda()
        {
            // logo atrás da cerca da quarentena (a serra de fora fica só para olhar)
            float m = _dados.Cerca + 2.5f, l = _dados.Cerca * 2f + 10f;
            foreach (var (pos, tam) in new[]
                     {
                         (new Vector3(m + 1f, 400f, 0f), new Vector3(2f, 1200f, l)),
                         (new Vector3(-m - 1f, 400f, 0f), new Vector3(2f, 1200f, l)),
                         (new Vector3(0f, 400f, m + 1f), new Vector3(l, 1200f, 2f)),
                         (new Vector3(0f, 400f, -m - 1f), new Vector3(l, 1200f, 2f)),
                     })
            {
                var parede = new GameObject("Borda do mapa");
                parede.transform.SetParent(_raiz, false);
                parede.transform.localPosition = pos;
                parede.AddComponent<BoxCollider>().size = tam;
            }
        }
    }
}
