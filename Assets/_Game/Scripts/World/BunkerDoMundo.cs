using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O bunker da família no mundo sorteado (o Plano_Bunker.md da pasta de arte, a parte 5), no objeto "Mundo" da cena
    /// Mundo (com o NetworkObject). O lugar, a estrada de terra, o pátio plano e o buraco do núcleo são da geração
    /// (<see cref="DadosDoMundo.TemBunker"/>); aqui fica o que aparece perto de alguém, como os prédios
    /// (<see cref="AndaresDoPredio"/>):
    ///  - até <see cref="raio"/> m de um jogador (no anfitrião, de todos; na tela de carregamento, da partida): a casca (as
    ///    paredes de fora, as lajes, o núcleo com a escada e a casinha), o morro com o pátio e a cerca
    ///    (<see cref="Bunker.MontarMorro"/>), o portão com a portinhola, as portas de aço do B4 e da casinha, o painel de
    ///    armas (a "Parede de armas" da cena vem para cá), os fundadores, a área sem zumbis e a área de dentro;
    ///  - por dentro, os andares perto de alguém: o dele e os vizinhos (de fora, até <see cref="pertoDeFora"/> m, a garagem),
    ///    cada um montado em outra thread, com a colisão cozida em outra thread, os pisos dos passos, o eco, as lâmpadas, o
    ///    interruptor, os móveis (<see cref="MoveisDoBunker"/>, os de perto da mobília nova, com as portas e as gavetas pela
    ///    rede da mobília) e os canos;
    ///  - as portas e as luzes de cada andar pela rede (o anfitrião manda): o E vai ao anfitrião, que confere a distância.
    ///    O portão é lento e barulhento (os zumbis em <see cref="barulhoDoPortao"/> m vêm ver); a portinhola, rápida e quieta.
    /// Dentro, a luz de fora cai (<see cref="AmbienteDoDia.LuzNoBunker"/>: na garagem e na casinha um pouco, embaixo quase
    /// toda) e não há neblina.
    /// </summary>
    public class BunkerDoMundo : NetworkBehaviour
    {
        [Tooltip("O material do bunker (o Cidade.mat: a paleta, sem o abandono; o bunker é usado, mas não abandonado).")]
        public Material material;
        [Tooltip("A malha do painel de armas (Painel_Armas, do Cidade_Moveis.fbx).")]
        public Mesh painelDeArmas;
        [Tooltip("O material da parede dos fundadores (Art/Materials/Fundadores.mat).")]
        public Material fundadores;
        [Tooltip("O interruptor e a alavanca dele (Interruptor e Interruptor_Alavanca, do Cidade_Moveis.fbx).")]
        public Mesh interruptor, alavanca;
        [Tooltip("Até onde a casca, o morro e as portas aparecem (m, do meio da caixa).")]
        public float raio = 300f;
        [Tooltip("De fora, até onde a garagem (o B0) carrega (m, da parede).")]
        public float pertoDeFora = 45f;
        [Tooltip("Até onde o portão se ouve: os zumbis vêm ver (m).")]
        public float barulhoDoPortao = 40f;
        [Tooltip("A luz de fora que sobra na garagem e na casinha (o portão deixa entrar o dia) e nos andares de baixo.")]
        [Range(0f, 1f)] public float luzNaGaragem = 0.35f, luzEmbaixo = 0.08f;
        [Tooltip("Até onde o E abre uma porta ou mexe num interruptor (o anfitrião confere; m).")]
        public float alcance = 5f;

        public static BunkerDoMundo Instancia { get; private set; }

        /// <summary>As portas (os bits da rede).</summary>
        public const int Portao = 0, Portinhola = 1, PortaDoB4 = 2, PortaDaCasinha = 3, CamaraFria = 4;
        const float Folga = 3f;
        const int MontagensAoMesmoTempo = 2;
        const float AfundaOMorro = 0.35f;
        // o tempo de cada quadro para pôr os móveis dos andares (todos de uma vez, o B3 e o B4 travavam o jogo uns 70 ms)
        const double MoveisPorQuadroMs = 3.0;

        readonly NetworkVariable<byte> _portas = new NetworkVariable<byte>(0);
        readonly NetworkVariable<byte> _luzes = new NetworkVariable<byte>(0x1F);
        // fora da rede (o F1 sem partida): o estado daqui
        byte _portasAqui, _luzesAqui = 0x1F;

        sealed class Andar
        {
            public Task<Bunker> Montando;
            public GameObject Objeto;
            public Mesh Desenho, Colisao;
            public MeshCollider Colisor;
            public Task Cozimento;
            public float QueridoEm;
            public bool Pronto;
            // os móveis entrando aos poucos (MoveisPorQuadroMs) e a malha dos canos do andar
            public System.Collections.IEnumerator Pondo;
            public Mesh Canos;
            public readonly List<Light> Luzes = new List<Light>();
            public readonly List<ParteDoMovel> Partes = new List<ParteDoMovel>();
            public InterruptorDoBunkerDoMundo Interruptor;
        }

        sealed class Parte
        {
            public GameObject Objeto;
            public Mesh Desenho, Colisao;
            public MeshCollider Colisor;
            public Task Cozimento;
        }

        DadosDoMundo _d;
        Transform _raiz;
        Task<(Bunker casca, Obra morro)> _montandoCasca;
        GameObject _casca;
        readonly List<Parte> _partesDaCasca = new List<Parte>();
        float _cascaQueridaEm = -100f;
        bool _cascaPronta;
        readonly Andar[] _andares = new Andar[Andares];
        int _montando;
        readonly Dictionary<int, List<ParteQueAbre>> _folhas = new Dictionary<int, List<ParteQueAbre>>();
        readonly Dictionary<(string, Color), Mesh> _pintados = new Dictionary<(string, Color), Mesh>();
        readonly List<ParteDoMovel> _partesDoMovel = new List<ParteDoMovel>();
        readonly List<CombineInstance> _juntar = new List<CombineInstance>();
        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();
        bool _comAreaLivre;
        float _luz = 1f, _neblina = 1f;

        /// <summary>A casca, o morro e a garagem estão com a colisão e os móveis (a partida espera por eles).</summary>
        public bool Pronto => _cascaPronta && _andares[0] != null && _andares[0].Pronto && _andares[0].Pondo == null;

        /// <summary>Quantos andares estão carregados (o F1 mostra).</summary>
        public int Carregados
        {
            get
            {
                int n = 0;
                foreach (var a in _andares) if (a != null && a.Pronto) n++;
                return n;
            }
        }

        void Awake() => Instancia = this;

        public override void OnDestroy()
        {
            Desmontar();
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            _portas.OnValueChanged += AoMudarAsPortas;
            _luzes.OnValueChanged += AoMudarAsLuzes;
            AplicarPortas(false);
            AplicarLuzes();
        }

        public override void OnNetworkDespawn()
        {
            _portas.OnValueChanged -= AoMudarAsPortas;
            _luzes.OnValueChanged -= AoMudarAsLuzes;
        }

        byte Portas => IsSpawned ? _portas.Value : _portasAqui;
        byte Luzes => IsSpawned ? _luzes.Value : _luzesAqui;

        public bool Aberta(int porta) => (Portas & (1 << porta)) != 0;
        public bool Acesa(int andar) => (Luzes & (1 << andar)) != 0;

        /// <summary>
        /// O ponto (mundo) fica na caixa do bunker, na casinha ou embaixo do morro: o chão do terreno passa por cima dele, e
        /// quem está ali não volta para a superfície (<see cref="MundoDaPartida"/>).
        /// </summary>
        public static bool Dentro(Vector3 p)
        {
            var b = Instancia;
            if (b == null || b._raiz == null) return false;
            var q = b._raiz.InverseTransformPoint(p);
            return Mathf.Abs(q.x) < X1 + 1f && Mathf.Abs(q.z) < Z1 + 1f && q.y > Base(Andares - 1) - 3f && q.y < Morro + AlturaDaCasinha + 1f;
        }

        // ------------------------------------------------------------ o que cada quadro quer

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var d = mundo != null ? mundo.Dados : null;
            if (d != _d)
            {
                Desmontar();
                _d = d;
            }
            if (_d == null || !_d.TemBunker) return;
            if (_raiz == null)
            {
                _raiz = new GameObject("Bunker da família").transform;
                _raiz.SetPositionAndRotation(_d.PosicaoDoBunker, Quaternion.Euler(0f, _d.GiroDoBunker, 0f));
            }

            float agora = Time.unscaledTime;
            var local = JogadorRede.Local;
            if (local != null) Querer(local.transform.position, agora);
            if (mundo != null && !mundo.Pronto) Querer(_d.Partida, agora);
            var rede = NetworkManager.Singleton;
            if (rede != null && rede.IsServer)
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j != local) Querer(j.transform.position, agora);

            RevisarACasca(agora);
            if (_casca != null) RevisarOsAndares(agora);
            ALuzDeDentro();
        }

        void Querer(Vector3 mundo, float agora)
        {
            var q = _raiz.InverseTransformPoint(mundo);
            if (new Vector2(q.x, q.z).magnitude < raio) _cascaQueridaEm = agora;
            float dx = Mathf.Max(0f, Mathf.Abs(q.x) - X1), dz = Mathf.Max(0f, Mathf.Abs(q.z) - Z1);
            float fora = Mathf.Sqrt(dx * dx + dz * dz);
            bool naCasinha = q.x > Escada.xMin - 0.5f && q.x < Escada.xMax + 0.5f && q.z > Escada.yMin - 0.5f && q.z < Escada.yMax + 0.5f
                             && q.y >= TopoDaCaixa - 0.5f && q.y < Morro + AlturaDaCasinha + 1f;
            if (fora < 0.5f && q.y > Base(Andares - 1) - 2f && q.y < TopoDaCaixa)
            {
                int f = AndarEm(q.y);
                for (int k = f - 1; k <= f + 1; k++) Marcar(k, agora);
            }
            else if (naCasinha)
            {
                Marcar(0, agora);
                Marcar(1, agora);
            }
            else if (fora < pertoDeFora && q.y < TopoDaCaixa + 3f) Marcar(0, agora);
        }

        void Marcar(int f, float agora)
        {
            if (f < 0 || f >= Andares) return;
            (_andares[f] ??= new Andar()).QueridoEm = agora;
        }

        // ------------------------------------------------------------ a casca, o morro e o que vem com eles

        void RevisarACasca(float agora)
        {
            var mundo = MundoDaPartida.Instancia;
            bool querida = agora - _cascaQueridaEm < Folga || (mundo != null && !mundo.Pronto);
            if (querida && _casca == null && _montandoCasca == null)
            {
                int semente = _d.Semente;
                _montandoCasca = Task.Run(() =>
                {
                    var m = Medidas.DoJogo;
                    var casca = new Bunker(m, -1, semente);
                    casca.MontarCasca();
                    var morro = new Obra(semente, m);
                    Bunker.MontarMorro(morro, AfundaOMorro);
                    return (casca, morro);
                });
            }
            if (_montandoCasca != null && _montandoCasca.IsCompleted)
            {
                var tarefa = _montandoCasca;
                _montandoCasca = null;
                if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                else if (querida) PorACasca(tarefa.Result.casca, tarefa.Result.morro);
                else
                {
                    Devolver(tarefa.Result.casca);
                    Devolver(tarefa.Result.morro);
                }
            }
            bool cozinhando = false;
            foreach (var p in _partesDaCasca)
            {
                if (p.Cozimento == null) continue;
                if (!p.Cozimento.IsCompleted)
                {
                    cozinhando = true;
                    continue;
                }
                p.Cozimento = null;
                p.Colisor.sharedMesh = p.Colisao;
                p.Colisor.enabled = true;
                NavMeshDoMundo.Mudou(CaixaDaCasca());
            }
            if (_casca != null && !cozinhando) _cascaPronta = true;
            if (!querida && _casca != null && !cozinhando && !AlgumAndar()) TirarACasca();
        }

        bool AlgumAndar()
        {
            foreach (var a in _andares) if (a != null && (a.Objeto != null || a.Montando != null)) return true;
            return false;
        }

        static void Devolver(Obra o)
        {
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
        }

        Bounds CaixaDaCasca()
        {
            float pe = Morro * Talude;
            var b = new Bounds(_raiz.position, Vector3.zero);
            foreach (var x in new[] { X0 - pe, X1 + pe })
            foreach (var z in new[] { Patio.yMin, Z1 + pe })
            foreach (var y in new[] { Base(Andares - 1) - 1f, Morro + AlturaDaCasinha })
                b.Encapsulate(_raiz.TransformPoint(new Vector3(x, y, z)));
            return b;
        }

        Bounds CaixaDoAndar(int f)
        {
            var b = new Bounds(_raiz.TransformPoint(new Vector3(0f, Base(f) + 1.5f, 0f)), Vector3.zero);
            foreach (var x in new[] { X0 - 1f, X1 + 1f })
            foreach (var z in new[] { Z0 - 1f, Z1 + 1f })
            foreach (var y in new[] { Base(f) - 0.7f, Teto(f) + 0.7f })
                b.Encapsulate(_raiz.TransformPoint(new Vector3(x, y, z)));
            return b;
        }

        void PorACasca(Bunker casca, Obra morro)
        {
            _casca = new GameObject("Casca e morro");
            _casca.transform.SetParent(_raiz, false);
            var pisoDaCasca = NovaParte("Casca", casca.vis, casca.col).AddComponent<PisoPorZona>();
            pisoDaCasca.zonas = new Bounds[0];
            pisoDaCasca.superficies = new Superficie[0];
            pisoDaCasca.resto = Superficie.Concreto;
            // o morro de grama, com o pátio de concreto
            var pisoDoMorro = NovaParte("Morro", morro.vis, morro.col).AddComponent<PisoPorZona>();
            pisoDoMorro.zonas = new[] { new Bounds(new Vector3(Patio.center.x, 0f, Patio.center.y), new Vector3(Patio.width, 1f, Patio.height)) };
            pisoDoMorro.superficies = new[] { Superficie.Concreto };
            pisoDoMorro.resto = Superficie.Grama;
            // as arandelas da escada (sempre acesas)
            foreach (var (pos, alcance, forca) in casca.Luzes) Lampada(_casca.transform, pos, alcance, forca);
            Devolver(casca);
            Devolver(morro);

            PorAsPortas();
            PorOsFundadores();
            // a área de dentro (a música, o sol) e a área sem zumbis (o povoamento não nasce na garagem nem embaixo)
            var dentro = new GameObject("Dentro (o bunker)");
            dentro.transform.SetParent(_casca.transform, false);
            float alto = TopoDaCaixa - Base(Andares - 1);
            dentro.transform.localPosition = new Vector3(0f, Base(Andares - 1) + alto / 2f, 0f);
            dentro.AddComponent<AreaInterna>().tamanho = new Vector3(X1 - X0, alto, Z1 - Z0);
            var zumbis = ZombieManager.Instancia;
            if (zumbis != null && !_comAreaLivre)
            {
                var areas = new List<ZombieManager.AreaLivre>(zumbis.areasLivres ?? new ZombieManager.AreaLivre[0])
                {
                    new ZombieManager.AreaLivre { centro = _raiz.TransformPoint(new Vector3(0f, -6f, 0f)), raio = 22f },
                };
                zumbis.areasLivres = areas.ToArray();
                _comAreaLivre = true;
            }
            Debug.Log($"[Bunker] A casca e o morro em ({_raiz.position.x:F0}, {_raiz.position.y:F1}, {_raiz.position.z:F0}).");
        }

        GameObject NovaParte(string nome, MalhaDeObra vis, MalhaDeObra col)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(_casca.transform, false);
            var p = new Parte { Objeto = go, Desenho = vis.ParaMesh($"Bunker: {nome}"), Colisao = col.ParaMesh($"Bunker: {nome} (colisão)") };
            go.AddComponent<MeshFilter>().sharedMesh = p.Desenho;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            p.Colisor = go.AddComponent<MeshCollider>();
            p.Colisor.enabled = false;
            p.Cozimento = Cozinhar(p.Colisao.GetEntityId());
            _partesDaCasca.Add(p);
            return go;
        }

        static Task Cozinhar(EntityId malha) => Task.Run(() => Physics.BakeMesh(malha, false));

        void TirarACasca()
        {
            foreach (var p in _partesDaCasca)
            {
                if (p.Desenho != null) Destroy(p.Desenho);
                if (p.Colisao != null) Destroy(p.Colisao);
            }
            _partesDaCasca.Clear();
            Esquecer(Portao);
            Esquecer(Portinhola);
            Esquecer(PortaDoB4);
            Esquecer(PortaDaCasinha);
            if (_casca != null)
            {
                var caixa = CaixaDaCasca();
                Destroy(_casca);
                NavMeshDoMundo.Mudou(caixa);
            }
            _casca = null;
            _cascaPronta = false;
            TirarAAreaLivre();
        }

        void TirarAAreaLivre()
        {
            var zumbis = ZombieManager.Instancia;
            if (!_comAreaLivre || zumbis == null || zumbis.areasLivres == null || _raiz == null)
            {
                _comAreaLivre = false;
                return;
            }
            var centro = _raiz.TransformPoint(new Vector3(0f, -6f, 0f));
            var areas = new List<ZombieManager.AreaLivre>(zumbis.areasLivres);
            areas.RemoveAll(a => (a.centro - centro).sqrMagnitude < 0.01f);
            zumbis.areasLivres = areas.ToArray();
            _comAreaLivre = false;
        }

        // o portão (as duas folhas para dentro, devagar, juntas), a portinhola presa na folha da esquerda e as portas de aço
        // do B4 (na saída da escada) e da casinha (para o morro)
        void PorAsPortas()
        {
            var portao = Novo("Portao", _casca.transform, new Vector3(PortaoX0 + PortaoLargura / 2, 0f, Z0), 180f, 1f);
            if (portao != null)
            {
                Transform f1 = portao.transform.Find("Portao.Folha.1"), f2 = portao.transform.Find("Portao.Folha.2"),
                          portinhola = portao.transform.Find("Portao.Portinhola.1");
                if (f1 != null) Folha(f1, Portao, 6f, "o portão", true, portinhola);
                if (f2 != null) Folha(f2, Portao, 6f, "o portão", true, null);
                if (portinhola != null && f1 != null)
                {
                    portinhola.SetParent(f1, true);
                    Folha(portinhola, Portinhola, 0.6f, "a portinhola", true, null, 0.08f);
                }
            }
            foreach (var (y, porta, rotulo) in new[] { (Base(Andares - 1), PortaDoB4, "a porta de aço (B4, restrito)"), (Morro, PortaDaCasinha, "a porta da casinha") })
            {
                var go = Novo("PortaDeAco", _casca.transform, new Vector3(Escada.xMax, y, PortaDaEscadaZ), 90f, 1f);
                var folha = go != null ? go.GetComponentInChildren<ParteQueAbre>(true) : null;
                if (folha != null) Folha(folha.transform, porta, 0.9f, rotulo, false, null);
            }
        }

        /// <summary>
        /// Uma folha que abre pela rede: a parte que abre (a do prefab ou uma nova), o gatilho da mira (um pouco maior que a
        /// folha) e, num filho, a caixa sólida (na portinhola da folha do portão, a folha fica com o vão dela). 'paraDentro':
        /// abre para o -z do modelo (o portão: para dentro da garagem); senão, como no prefab (para a frente).
        /// </summary>
        void Folha(Transform t, int porta, float duracao, string rotulo, bool paraDentro, Transform portinhola, float folgaDoGatilho = 0.03f)
        {
            var filtro = t.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null) return;
            var b = filtro.sharedMesh.bounds;
            var p = t.GetComponent<ParteQueAbre>();
            if (p == null) p = t.gameObject.AddComponent<ParteQueAbre>();
            p.tipo = ParteQueAbre.Tipo.Porta;
            p.duracao = duracao;
            p.rotulo = rotulo;
            if (paraDentro) p.quanto = b.center.x < 0f ? -90f : 90f;
            var velha = t.GetComponent<ParteDoMovel>();
            if (velha != null) Destroy(velha);
            var gatilho = t.GetComponent<BoxCollider>();
            if (gatilho == null) gatilho = t.gameObject.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = b.center;
            gatilho.size = b.size + Vector3.one * folgaDoGatilho;
            t.gameObject.layer = 2;
            var e = t.gameObject.AddComponent<PortaDoBunkerDoMundo>();
            e.bunker = this;
            e.porta = porta;
            e.parte = p;
            var solido = new GameObject("Sólido");
            solido.transform.SetParent(t, false);
            if (portinhola == null) Caixa(solido, b.center, b.size);
            else
            {
                // o vão da portinhola: de PortinholaDe a PortinholaDe + largura, a partir da dobradiça, até a altura dela
                float s = Mathf.Sign(b.center.x), x0 = PortinholaDe, x1 = PortinholaDe + PortinholaLargura, w = b.size.x;
                void Pedaco(float a0, float a1, float y0, float y1)
                {
                    if (a1 - a0 < 0.01f || y1 - y0 < 0.01f) return;
                    Caixa(solido, new Vector3(s * (a0 + a1) / 2f, (y0 + y1) / 2f, b.center.z), new Vector3(a1 - a0, y1 - y0, b.size.z));
                }
                Pedaco(0f, x0, b.min.y, b.max.y);
                Pedaco(x1, w, b.min.y, b.max.y);
                Pedaco(x0, x1, PortinholaAltura, b.max.y);
            }
            if (!_folhas.TryGetValue(porta, out var lista)) _folhas[porta] = lista = new List<ParteQueAbre>();
            lista.Add(p);
            p.Definir(Aberta(porta), ja: true);
        }

        static void Caixa(GameObject go, Vector3 centro, Vector3 tamanho)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = centro;
            c.size = tamanho;
        }

        void Esquecer(int porta) => _folhas.Remove(porta);

        // o painel de armas no arsenal (com a "Parede de armas" da cena vindo para ele) e os fundadores no fundo da garagem
        void PorOsFundadores()
        {
            var lugarDoPainel = new Vector3(3.4f + X0, 0f, Z1 - ParedeDeFora / 2);
            if (painelDeArmas != null)
            {
                var painel = new GameObject("Painel_Armas");
                painel.transform.SetParent(_casca.transform, false);
                painel.transform.SetLocalPositionAndRotation(lugarDoPainel, Quaternion.Euler(0f, 180f, 0f));
                painel.AddComponent<MeshFilter>().sharedMesh = painelDeArmas;
                painel.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var parede = ParedeDeArmas.Instancia;
            if (parede != null)
                parede.transform.SetPositionAndRotation(_raiz.TransformPoint(lugarDoPainel), _raiz.rotation * Quaternion.Euler(0f, 180f, 0f));
            if (fundadores != null)
            {
                // o quadro de 1 × 1 (a frente para -z, como o Quad da Unity), feito aqui
                var malha = new Mesh { name = "Bunker: o quadro dos fundadores" };
                malha.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
                malha.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                malha.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
                malha.SetTriangles(new[] { 0, 3, 1, 3, 0, 2 }, 0);
                malha.RecalculateBounds();
                var q = new GameObject("Parede dos fundadores");
                q.transform.SetParent(_casca.transform, false);
                q.transform.localPosition = new Vector3(20f + X0, 3.25f, Z1 - ParedeDeFora / 2 - 0.01f);
                q.transform.localScale = new Vector3(4.2f, 3.15f, 1f);
                q.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = q.AddComponent<MeshRenderer>();
                r.sharedMaterial = fundadores;
                r.shadowCastingMode = ShadowCastingMode.Off;
                _partesDaCasca.Add(new Parte { Objeto = q, Desenho = malha });
            }
        }

        Light Lampada(Transform pai, Vector3 pos, float alcance, float forca)
        {
            var go = new GameObject("Lâmpada");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = alcance;
            l.intensity = forca;
            l.color = new Color(1f, 0.92f, 0.78f);
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------ os andares

        void RevisarOsAndares(float agora)
        {
            _relogio.Restart();
            for (int f = 0; f < Andares; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                bool querido = agora - a.QueridoEm < Folga;
                if (a.Montando != null && a.Montando.IsCompleted)
                {
                    var tarefa = a.Montando;
                    a.Montando = null;
                    _montando--;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else if (querido) PorOAndar(f, a, tarefa.Result);
                    else Devolver(tarefa.Result);
                }
                // o NavMesh do andar é refeito com a colisão cozida e os móveis postos (os dois)
                if (a.Cozimento != null && a.Cozimento.IsCompleted)
                {
                    a.Cozimento = null;
                    a.Colisor.sharedMesh = a.Colisao;
                    a.Colisor.enabled = true;
                    a.Pronto = true;
                    if (a.Pondo == null) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
                }
                while (a.Pondo != null && _relogio.Elapsed.TotalMilliseconds < MoveisPorQuadroMs)
                    if (!a.Pondo.MoveNext())
                    {
                        a.Pondo = null;
                        if (a.Pronto) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
                    }
                if (querido && a.Objeto == null && a.Montando == null && _montando < MontagensAoMesmoTempo)
                {
                    int andar = f, semente = _d.Semente;
                    a.Montando = Task.Run(() =>
                    {
                        var b = new Bunker(Medidas.DoJogo, andar, semente);
                        b.MontarAndar();
                        return b;
                    });
                    _montando++;
                }
                if (!querido && a.Objeto != null && a.Cozimento == null) TirarOAndar(f, a);
                if (!querido && a.Objeto == null && a.Montando == null) _andares[f] = null;
            }
        }

        void PorOAndar(int f, Andar a, Bunker b)
        {
            var go = new GameObject($"{Codigos[f]} ({NomesDosAndares[f]})");
            go.transform.SetParent(_raiz, false);
            a.Objeto = go;
            a.Desenho = b.vis.ParaMesh($"Bunker: {Codigos[f]}");
            a.Colisao = b.col.ParaMesh($"Bunker: {Codigos[f]} (colisão)");
            go.AddComponent<MeshFilter>().sharedMesh = a.Desenho;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            a.Colisor = go.AddComponent<MeshCollider>();
            a.Colisor.enabled = false;
            a.Cozimento = Cozinhar(a.Colisao.GetEntityId());
            // os pisos dos passos: o de cada cômodo (o taco, o carpete) e o concreto no resto
            var pisos = go.AddComponent<PisoPorZona>();
            var andar = b.andares[f];
            var zonas = new List<Bounds>();
            var superficies = new List<Superficie>();
            foreach (var k in andar.comodos)
            {
                zonas.Add(new Bounds(new Vector3((k.x0 + k.x1) / 2f, Base(f) + 1f, (k.z0 + k.z1) / 2f), new Vector3(k.x1 - k.x0, 3f, k.z1 - k.z0)));
                superficies.Add(k.piso switch
                {
                    "Taco" or "Madeira" => Superficie.Madeira,
                    "Carpete" => Superficie.Carpete,
                    _ => Superficie.Concreto,
                });
            }
            pisos.zonas = zonas.ToArray();
            pisos.superficies = superficies.ToArray();
            pisos.resto = Superficie.Concreto;
            float h = Teto(f) - Base(f);
            EcoSoDentro.Criar(go.transform, new Bounds(new Vector3(0f, Base(f) + h / 2f, 0f), new Vector3(X1 - X0, h, Z1 - Z0)));
            foreach (var (pos, alcance, forca) in b.Luzes) a.Luzes.Add(Lampada(go.transform, pos, alcance, forca));
            foreach (var l in a.Luzes) l.enabled = Acesa(f);
            Devolver(b);
            PorOInterruptor(f, a);
            a.Pondo = PorOsMoveis(f, a, go.transform);
        }

        void TirarOAndar(int f, Andar a)
        {
            bool tinhaColisao = a.Pronto;
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo != null) foreach (var p in a.Partes) if (p != null) mundo.Esquecer(p);
            a.Partes.Clear();
            a.Pondo = null;
            if (f == 2) Esquecer(CamaraFria);
            Destroy(a.Objeto);
            if (a.Desenho != null) Destroy(a.Desenho);
            if (a.Colisao != null) Destroy(a.Colisao);
            if (a.Canos != null) Destroy(a.Canos);
            a.Objeto = null;
            a.Desenho = a.Colisao = a.Canos = null;
            a.Colisor = null;
            a.Pronto = false;
            a.Luzes.Clear();
            a.Interruptor = null;
            if (tinhaColisao) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
        }

        // o interruptor do andar, na parede do núcleo, do lado do hall, logo depois da porta da escada
        void PorOInterruptor(int f, Andar a)
        {
            if (interruptor == null) return;
            var go = new GameObject($"Interruptor ({Codigos[f]})");
            go.transform.SetParent(a.Objeto.transform, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(Escada.xMax + Medidas.DoJogo.esp / 2f + 0.005f, Base(f) + 1.3f, Escada.yMax - 0.35f),
                Quaternion.Euler(0f, 90f, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = interruptor;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            Transform alav = null;
            if (alavanca != null)
            {
                var al = new GameObject("Alavanca");
                al.transform.SetParent(go.transform, false);
                al.transform.localPosition = new Vector3(0f, 0f, 0.054f);
                al.AddComponent<MeshFilter>().sharedMesh = alavanca;
                al.AddComponent<MeshRenderer>().sharedMaterial = material;
                alav = al.transform;
            }
            var gatilho = go.AddComponent<BoxCollider>();
            gatilho.isTrigger = true;
            gatilho.center = new Vector3(0f, 0f, 0.05f);
            gatilho.size = new Vector3(0.22f, 0.3f, 0.14f);
            go.layer = 2;
            var i = go.AddComponent<InterruptorDoBunkerDoMundo>();
            i.bunker = this;
            i.andar = f;
            i.alavanca = alav;
            i.Mostrar(Acesa(f));
            a.Interruptor = i;
        }

        // os móveis do andar (os de perto da mobília nova, com as partes que abrem pela rede da mobília) e as portas que são
        // modelos (a da câmara fria, pela rede daqui), um por passo (RevisarOsAndares dá MoveisPorQuadroMs a cada quadro), e
        // no fim os canos e as peças do teto
        System.Collections.IEnumerator PorOsMoveis(int f, Andar a, Transform pai)
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo == null) yield break;
            var obra = MobiliaNova.ChaveDaObra(_raiz.position, 100 + f);
            int i = 0;
            foreach (var m in MoveisDoBunker.Todos)
            {
                int ponto = i++;
                if (m.andar != f) continue;
                var go = Novo(m.modelo, pai, m.lugar, m.giro, m.escala);
                if (go == null) continue;
                if (!UI.Graficos.SombraDosMoveis) SemSombra(go);
                go.GetComponentsInChildren(true, _partesDoMovel);
                for (int k = 0; k < _partesDoMovel.Count; k++)
                {
                    var parte = _partesDoMovel[k];
                    parte.chave = MobiliaNova.ChaveDaParte(obra, ponto, 0, k);
                    mundo.Registrar(parte);
                    a.Partes.Add(parte);
                }
                yield return null;
            }
            foreach (var (modelo, lugar, giro) in MoveisDoBunker.PortasDeModelo(f))
            {
                var go = Novo(modelo, pai, lugar, giro, 1f);
                var folha = go != null ? go.GetComponentInChildren<ParteQueAbre>(true) : null;
                if (folha != null && modelo == "PortaCamaraFria") Folha(folha.transform, CamaraFria, 0.9f, "a porta da câmara fria", false, null);
                yield return null;
            }
            PorOsCanos(f, a, pai);
        }

        // os canos, os dutos e as peças do teto do andar numa malha só, com a cor de cada sistema: com um móvel por metro de
        // cano (cada um com o LOD dele), eram de 100 a 180 objetos por andar, e o de longe ficava sem a cor
        void PorOsCanos(int f, Andar a, Transform pai)
        {
            _juntar.Clear();
            foreach (var c in MoveisDoBunker.Canos)
            {
                if (c.andar != f) continue;
                Vector3 ini = new Vector3(c.de.x + X0, Teto(f) - c.abaixoDoTeto, c.de.y + Z0);
                Vector3 fim = new Vector3(c.ate.x + X0, Teto(f) - c.abaixoDoTeto, c.ate.y + Z0);
                Vector3 d = fim - ini;
                int n = Mathf.Max(1, Mathf.FloorToInt(d.magnitude));
                float giro = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0f : 90f;
                for (int k = 0; k < n; k++)
                {
                    var p = ini + d.normalized * (k + 0.5f);
                    Juntar(c.modelo, c.cor, p, giro);
                    if (c.modelo == "CanoReto" && k % 2 == 1) Juntar("SuporteDeCano", default, p + d.normalized * 0.5f, giro);
                }
            }
            foreach (var pc in MoveisDoBunker.Pecas)
                if (pc.andar == f)
                    Juntar(pc.modelo, pc.cor, new Vector3(pc.ponto.x + X0, Teto(f) - pc.abaixoDoTeto, pc.ponto.y + Z0), pc.giro);
            if (_juntar.Count == 0) return;
            a.Canos = new Mesh { name = $"Bunker: os canos do {Codigos[f]}", indexFormat = IndexFormat.UInt32 };
            a.Canos.CombineMeshes(_juntar.ToArray(), true, true);
            _juntar.Clear();
            var go = new GameObject("Canos e dutos");
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = a.Canos;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MobiliaNovaDoMundo.Instancia.Material;
            if (!UI.Graficos.SombraDosMoveis) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Juntar(string modelo, Color cor, Vector3 lugar, float giro)
        {
            var malha = Pintada(modelo, cor);
            if (malha == null) return;
            var m = Matrix4x4.TRS(lugar, Quaternion.Euler(0f, giro, 0f), Vector3.one);
            for (int s = 0; s < malha.subMeshCount; s++) _juntar.Add(new CombineInstance { mesh = malha, subMeshIndex = s, transform = m });
        }

        GameObject Novo(string modelo, Transform pai, Vector3 lugar, float giro, float escala)
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            var go = mundo != null ? mundo.Novo(modelo, 0, pai) : null;
            if (go == null) return null;
            go.transform.SetLocalPositionAndRotation(lugar, Quaternion.Euler(0f, giro, 0f));
            go.transform.localScale = Vector3.one * escala;
            return go;
        }

        static void SemSombra(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // a malha de perto do modelo com a cor do cano (o que ele leva) na parte principal, no esmalte (o duto, no metal), uma
        // por modelo e cor; sem cor (a transparente), a de sempre (a da mobília nova, que não se apaga daqui)
        Mesh Pintada(string modelo, Color cor)
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            var malha = mundo != null ? mundo.Malha(modelo, 0) : null;
            if (malha == null || cor.a <= 0f) return malha;
            if (!_pintados.TryGetValue((modelo, cor), out var pintada))
            {
                var tex = CatalogoDaMobilia.Textura(modelo == "Duto" ? "MetalEscovado" : "Esmalte");
                pintada = CatalogoDaMobilia.Pintar(malha, new CatalogoDaMobilia.Variacao("", new CatalogoDaMobilia.Acabamento(tex, cor)));
                _pintados[(modelo, cor)] = pintada;
            }
            return pintada;
        }

        // ------------------------------------------------------------ as portas e as luzes pela rede

        /// <summary>O E numa porta: pelo anfitrião (na rede) ou direto.</summary>
        public void PedirPorta(int porta)
        {
            if (!IsSpawned)
            {
                _portasAqui ^= (byte)(1 << porta);
                AplicarPortas(true);
                return;
            }
            PortaRpc((byte)porta);
        }

        /// <summary>O E num interruptor: pelo anfitrião (na rede) ou direto.</summary>
        public void PedirLuz(int andar)
        {
            if (!IsSpawned)
            {
                _luzesAqui ^= (byte)(1 << andar);
                AplicarLuzes();
                return;
            }
            LuzRpc((byte)andar);
        }

        [Rpc(SendTo.Server)]
        void PortaRpc(byte porta, RpcParams parametros = default)
        {
            if (porta > CamaraFria || !Perto(parametros.Receive.SenderClientId, PontoDaPorta(porta))) return;
            _portas.Value ^= (byte)(1 << porta);
            var portao = PontoDaPorta(Portao);
            if (porta == Portao && portao.HasValue) ZombieManager.Alarme(portao.Value, barulhoDoPortao);
        }

        [Rpc(SendTo.Server)]
        void LuzRpc(byte andar, RpcParams parametros = default)
        {
            if (andar >= Andares || _raiz == null) return;
            var ponto = _raiz.TransformPoint(new Vector3(Escada.xMax, Base(andar) + 1.3f, Escada.yMax - 0.35f));
            if (!Perto(parametros.Receive.SenderClientId, ponto)) return;
            _luzes.Value ^= (byte)(1 << andar);
        }

        bool Perto(ulong cliente, Vector3? ponto)
        {
            if (!ponto.HasValue || !NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) || c.PlayerObject == null) return false;
            return Vector3.Distance(c.PlayerObject.transform.position, ponto.Value) < alcance + 2f;
        }

        Vector3? PontoDaPorta(int porta)
        {
            if (_folhas.TryGetValue(porta, out var lista))
                foreach (var p in lista) if (p != null) return p.transform.position;
            return null;
        }

        void AoMudarAsPortas(byte antes, byte depois) => AplicarPortas(true);
        void AoMudarAsLuzes(byte antes, byte depois) => AplicarLuzes();

        void AplicarPortas(bool mexeu)
        {
            foreach (var (porta, lista) in _folhas)
            {
                bool aberta = Aberta(porta);
                foreach (var p in lista)
                {
                    if (p == null || p.Aberta == aberta) continue;
                    p.Definir(aberta);
                    // o NavMesh em volta da folha: agora e quando ela acabar de abrir ou fechar
                    if (mexeu) StartCoroutine(AvisarONavMesh(p, p.duracao));
                }
            }
        }

        System.Collections.IEnumerator AvisarONavMesh(ParteQueAbre p, float depois)
        {
            var caixa = new Bounds(p.transform.position, Vector3.one * 8f);
            NavMeshDoMundo.Mudou(caixa);
            yield return new WaitForSeconds(depois + 0.3f);
            NavMeshDoMundo.Mudou(caixa);
        }

        void AplicarLuzes()
        {
            for (int f = 0; f < Andares; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                bool acesa = Acesa(f);
                foreach (var l in a.Luzes) if (l != null) l.enabled = acesa;
                if (a.Interruptor != null) a.Interruptor.Mostrar(acesa);
            }
        }

        // ------------------------------------------------------------ a luz de dentro

        // com a câmera na caixa ou na casinha, a luz de fora cai (na garagem e na casinha um pouco: o portão deixa entrar o
        // dia; embaixo quase toda) e a neblina some
        void ALuzDeDentro()
        {
            float luz = 1f, neblina = 1f;
            var olho = Camera.main;
            if (olho != null && _casca != null)
            {
                var q = _raiz.InverseTransformPoint(olho.transform.position);
                bool naCaixa = Mathf.Abs(q.x) < X1 && Mathf.Abs(q.z) < Z1 && q.y > Base(Andares - 1) - 1f && q.y < TopoDaCaixa;
                bool naCasinha = q.x > Escada.xMin && q.x < Escada.xMax && q.z > Escada.yMin && q.z < Escada.yMax
                                 && q.y >= TopoDaCaixa && q.y < Morro + AlturaDaCasinha;
                if (naCaixa || naCasinha)
                {
                    luz = naCasinha || q.y > -0.3f ? luzNaGaragem : luzEmbaixo;
                    neblina = 0f;
                }
            }
            _luz = Mathf.MoveTowards(_luz, luz, Time.deltaTime * 2f);
            _neblina = Mathf.MoveTowards(_neblina, neblina, Time.deltaTime * 3f);
            AmbienteDoDia.LuzNoBunker = _luz;
            AmbienteDoDia.NeblinaNoBunker = _neblina;
        }

        // ------------------------------------------------------------ o fim

        void Desmontar()
        {
            for (int f = 0; f < Andares; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                if (a.Objeto != null) TirarOAndar(f, a);
                _andares[f] = null;
            }
            _montando = 0;
            if (_casca != null) TirarACasca();
            _montandoCasca = null;
            _cascaQueridaEm = -100f;
            _folhas.Clear();
            foreach (var m in _pintados.Values) if (m != null) Destroy(m);
            _pintados.Clear();
            if (_raiz != null) Destroy(_raiz.gameObject);
            _raiz = null;
            _luz = _neblina = 1f;
            AmbienteDoDia.LuzNoBunker = AmbienteDoDia.NeblinaNoBunker = 1f;
        }
    }
}
