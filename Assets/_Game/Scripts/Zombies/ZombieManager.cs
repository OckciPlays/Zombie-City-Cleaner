using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Comanda todos os zumbis. No anfitrião:
    /// - cria a população aos poucos, nos pontos de horda, reaproveitando os zumbis de um pool;
    /// - mede a distância de cada zumbi até o jogador mais próximo da lista e escolhe o nível de detalhe (LOD de IA),
    ///   com histerese: Perto (IA completa), Médio (só vaga) e Longe (zumbi virtual, parado e sem custo);
    /// - faz os zumbis pensarem em fatias, alguns por quadro, dentro de um orçamento;
    /// - manda para cada cliente só os zumbis perto dele (ZombieManager.Rede.cs).
    /// Nos clientes, mantém as cópias. Em todas as máquinas, cuida dos corpos que caem (ZombieManager.Corpos.cs).
    /// Há vários modelos de zumbi (um prefab e um pool para cada); o modelo sai do Id e da geração, então o anfitrião e
    /// os clientes chegam ao mesmo sem mandar nada a mais. F3 mostra os números na tela.
    /// O sol dos Ocos (a pedra) fica em ZombieManager.Sol.cs e Zumbi.Sol.cs.
    /// </summary>
    public partial class ZombieManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct AreaLivre
        {
            public Vector3 centro;
            public float raio;
        }

        /// <summary>
        /// O que um zumbi precisa saber de cada jogador, montado uma vez por quadro. Olhando pela luneta, o jogador tem um
        /// aumento: na linha dela (o cone do meio ângulo), a distância dos zumbis conta dividida pelo aumento.
        /// </summary>
        internal struct Alvo
        {
            public JogadorRede jogador;
            public Vector3 pes, peito, olho, olhar;
            public bool lanterna, correndo, iluminado;
            /// <summary>
            /// De dia ou amanhecendo, onde o sol bate (ou vai bater): o zumbi só observa (prioriza se esconder), a não ser
            /// atacado.
            /// </summary>
            public bool exposto;
            public float aumento, meioCone;
            /// <summary>
            /// A distância de visão deste jogador (UI/Graficos): até onde os zumbis são desenhados e mandados para ele, e de
            /// onde os novos nascem perto dele (m).
            /// </summary>
            public float visao, nascer;
        }

        /// <summary>O campo de visão normal da câmera do mundo (o aumento da luneta conta a partir dele).</summary>
        const float FovNormal = 70f;

        // o Id vai pela rede em 16 bits; 2.048 dá folga para os 1.000 das hordas e os criados pelo menu do desenvolvedor
        public const int MaxZumbis = 2048;
        // só o cenário (camada Default) tapa a visão; as cápsulas e hitboxes dos zumbis ficam em outras camadas
        const int MascaraDaVisao = 1 << 0;

        public static ZombieManager Instancia { get; private set; }

        [Header("Peças")]
        [Tooltip("Os modelos de zumbi (um prefab para cada), sorteados pelo Id.")]
        public Zumbi[] modelos;
        [Tooltip("O corpo com física de cada modelo, na mesma ordem.")]
        public CorpoDeZumbi[] corpos;
        [Tooltip("A voz do zumbi (SomZombie1), tocada em trechos.")]
        public AudioClip vozDoZumbi;
        public Vector2[] trechosDaVoz = { new Vector2(0.1f, 1f), new Vector2(1.75f, 1f), new Vector2(3.1f, 0.85f), new Vector2(4.35f, 1f) };

        [Header("População")]
        public bool povoarAoComecar = true;
        public int criadosPorQuadro = 6;
        [Tooltip("Onde não nasce zumbi: a casa da família (com o bunker) e a frente dela, onde os jogadores aparecem.")]
        public AreaLivre[] areasLivres;

        [Header("LOD de IA (distância do jogador mais próximo, em m)")]
        public float distanciaPerto = 15f;
        public float distanciaMedia = 40f;
        [Tooltip("Para subir de nível (chegar mais perto) é preciso passar a distância menos isto; para descer, basta passar a distância.")]
        public float histerese = 3f;
        [Tooltip("Intervalo entre decisões no nível Perto e no Médio (s).")]
        public float intervaloPerto = 0.1f;
        public float intervaloMedio = 0.5f;
        [Tooltip("Quantos zumbis pensam por quadro, no máximo.")]
        public int orcamentoPorQuadro = 40;
        // até onde o zumbi é desenhado nesta máquina: a distância de visão de quem joga aqui (UI/Graficos). Era 65 m fixos,
        // mas os de longe (além de 40 m) nem eram desenhados: apareciam do nada a ~37 m, sobretudo de carro
        [Tooltip("Até onde o zumbi usa a malha inteira (LOD0) e a do LOD1; depois, a do LOD2 (os zumbis em blocos).")]
        public float distanciaDoLOD1 = 12f;
        public float distanciaDoLOD2 = 26f;

        public AjustesDeZumbi ajustes = new AjustesDeZumbi();

        static readonly ProfilerMarker MarcaIA = new("Zumbis.IA");
        static readonly ProfilerMarker MarcaLOD = new("Zumbis.LOD");

        internal readonly Alvo[] Jogadores = new Alvo[8];
        internal int QuantosJogadores { get; private set; }
        /// <summary>Benchmark: os zumbis atacam, mas não tiram vida.</summary>
        public bool SemDano { get; set; }
        public bool Povoando => _povoando != null;
        /// <summary>Zumbis na cidade, vivos ou caídos (no anfitrião), ou cópias recebidas (nos clientes).</summary>
        public int Quantos => IsServer ? _ativos.Count : _copias.Count;
        /// <summary>Zumbis mortos nesta partida (no anfitrião).</summary>
        public int Mortos { get; private set; }

        readonly List<Zumbi> _ativos = new List<Zumbi>();
        /// <summary>Os zumbis em jogo agora (só no anfitrião: as barricadas contam os que estão nelas).</summary>
        public IReadOnlyList<Zumbi> Ativos => _ativos;
        Stack<Zumbi>[] _pools;
        readonly Zumbi[] _porId = new Zumbi[MaxZumbis];
        readonly byte[] _geracoes = new byte[MaxZumbis];
        readonly Stack<int> _idsLivres = new Stack<int>();
        int _proximoId, _cursor, _gemidosNoSegundo;
        float _proximaVarredura, _proximoGemido, _segundoDosGemidos;
        Coroutine _povoando;
        Transform _vivos;
        bool _info;

        void OnEnable() => Instancia = this;

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
        }

        public override void OnNetworkSpawn()
        {
            if (_vivos == null) _vivos = new GameObject("Zumbis vivos").transform;
            PrepararCorpos();
            if (GetComponent<UI.BarraDoChefe>() == null) gameObject.AddComponent<UI.BarraDoChefe>();   // a barra do Brutamonte
            StartCoroutine(PreaquecerCortes());
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += AoSairCliente;
                if (povoarAoComecar) RepovoarHordas();
            }
            else NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(NomeDaMensagem, AoReceber);
        }

        public override void OnNetworkDespawn()
        {
            if (_povoando != null)
            {
                StopCoroutine(_povoando);
                _povoando = null;
            }
            if (NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= AoSairCliente;
                NetworkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(NomeDaMensagem);
            }
            Limpar();
            LimparCopias();
            GuardarCorpos();
            GuardarPedacos();
            _niveisPorCliente.Clear();
        }

        void Update()
        {
            if (!IsSpawned) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.f3Key.wasPressedThisFrame) _info = !_info;
            if (IsServer)
            {
                float agora = Time.time, dt = Time.deltaTime;
                AtualizarJogadores();
                AtualizarFachos();
                if (agora >= _proximaVarredura)
                {
                    _proximaVarredura = agora + 0.2f;
                    VarrerNiveis();
                }
                SolNosZumbis(agora, dt);
                if (agora >= _proximoCerco)
                {
                    _proximoCerco = agora + 0.3f;
                    OrganizarCerco();
                }
                PovoarEmVolta(agora);   // o mundo gerado (ZombieManager.Povoamento.cs)
                EspeciaisNoServidor(agora);   // o Long e o Brutamonte aparecem (ZombieManager.Especiais.cs)
                GosmaNoServidor(agora);   // a gosma do Long (ZombieManager.Gosma.cs)
                PensarEmFatias(agora);
                for (int i = 0; i < _ativos.Count; i++)
                    if (_ativos[i].NivelAtual != Zumbi.Nivel.Longe) _ativos[i].QuadroNoServidor(dt);
                DevolverMortos(agora);
                EnviarParaClientes();
            }
            else AtualizarCopias();
            AtualizarCorpos(Time.time, Time.deltaTime);
            AtualizarPedacos(Time.time, Time.deltaTime);
            GemidosAoAcaso();
        }

        // depois da animação: o equilíbrio sem uma perna (o corpo inclinado no pé que sobrou e a pose saindo do boneco da
        // queda; Zumbi.Equilibrio.cs). A conta de quem não está numa fase sai na primeira linha
        void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            var lista = IsServer ? _ativos : _copias;
            for (int i = 0; i < lista.Count; i++) lista[i].DepoisDaAnimacao(dt);
        }

        // ------------------------------------------------------------------ jogadores e percepção

        void AtualizarJogadores()
        {
            int n = 0;
            bool noite = CicloDoDia.Noite > 0.5f;
            foreach (var j in JogadorRede.Todos)
            {
                // quem ainda está criando o personagem (a tela de criar) não aparece para os zumbis
                if (j == null || !j.IsSpawned || !j.Criado || n == Jogadores.Length) continue;
                Jogadores[n] = AlvoDe(j, noite, Quaternion.Euler(j.Pitch.Value, j.transform.eulerAngles.y, 0f) * Vector3.forward);
                Jogadores[n++].exposto = ProtegerDoSol && Exposto(j.transform.position);
            }
            QuantosJogadores = n;
        }

        static Alvo AlvoDe(JogadorRede j, bool noite, Vector3 olhar)
        {
            Vector3 pes = j.transform.position;
            var a = new Alvo
            {
                jogador = j,
                pes = pes,
                peito = pes + Vector3.up * 1.3f,
                olho = pes + Vector3.up * 1.62f,
                olhar = olhar,
                lanterna = j.Lanterna != null && j.Lanterna.Ligada.Value,
                correndo = j.Velocidade > 3f,
                iluminado = noite && PosteDeLuz.Ilumina(pes),
                aumento = 1f,
                visao = Graficos.Metros(j.Visao.Value),
                nascer = Graficos.NascerAPartirDe(j.Visao.Value),
            };
            var dados = Armas.Dados(j.ArmaNaMao);
            if (j.Luneta.Value && dados.fovLuneta > 0f)
            {
                a.aumento = Mathf.Tan(FovNormal * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(dados.fovLuneta * 0.5f * Mathf.Deg2Rad);
                a.meioCone = dados.fovLuneta * 0.5f + 2f;
            }
            return a;
        }

        /// <summary>
        /// A distância de um zumbi a um jogador, para o nível de detalhe. Na linha da luneta (até o alcance da sniper) ela
        /// conta dividida pelo aumento: o zumbi de longe que aparece grande na luneta é tratado como um de perto (é
        /// desenhado, tem hitboxes e vai pela rede).
        /// </summary>
        internal static float Distancia(in Alvo p, Vector3 ponto)
        {
            float d = Vector3.Distance(p.pes, ponto);
            if (p.aumento > 1f && d > 2f && d < AlcanceDaLuneta && Vector3.Angle(p.olhar, ponto + Vector3.up - p.olho) < p.meioCone) d /= p.aumento;
            return d;
        }

        const float AlcanceDaLuneta = 160f;

        /// <summary>O jogador desta máquina como alvo (o olhar é o da câmera, sem esperar a rede).</summary>
        internal Alvo AlvoLocal()
        {
            var j = JogadorRede.Local;
            if (j == null || !j.IsSpawned)
                return new Alvo { pes = PontoDeVista(), aumento = 1f, visao = Graficos.Metros(Graficos.Visao), nascer = Graficos.NascerAPartirDe(Graficos.Visao) };
            var a = AlvoDe(j, false, j.cameraMundo != null ? j.Olho.forward : j.transform.forward);
            a.visao = Graficos.Metros(Graficos.Visao);   // a desta máquina, sem esperar a rede
            return a;
        }

        /// <summary>Até onde um zumbi enxerga este jogador: de dia longe; à noite pouco, a não ser com luz nele.</summary>
        internal float AlcanceDaVisao(in Alvo p)
        {
            float noite = CicloDoDia.Noite;
            float alcance = Mathf.Lerp(ajustes.visaoDeDia, ajustes.visaoDeNoite, noite);
            if (p.iluminado) alcance = Mathf.Max(alcance, ajustes.visaoSobPoste);
            if (p.lanterna) alcance = Mathf.Max(alcance, Mathf.Lerp(ajustes.visaoDeDia, ajustes.visaoComLanterna, noite));
            return alcance;
        }

        internal bool Enxerga(Vector3 olho, Vector3 alvo)
        {
            // para meio metro antes do alvo: o que encosta nele (uma porta, a parede onde ele se apoia) não conta
            Vector3 para = alvo - olho;
            float d = para.magnitude;
            if (d < 0.6f) return true;
            return !Physics.Raycast(olho, para / d, d - 0.5f, MascaraDaVisao, QueryTriggerInteraction.Ignore);
        }

        internal void Engajou(Zumbi z) => z.MudarNivelNoServidor(Zumbi.Nivel.Perto);

        internal void ContarMorte() => Mortos++;

        // o zumbi morto (já invisível: quem aparece é o corpo) continua na lista o tempo do corpo, para a morte chegar
        // aos clientes que passarem perto, e depois volta ao pool
        void DevolverMortos(float agora)
        {
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var z = _ativos[i];
                if (z.Morto && agora - z.mortoEm > tempoDoCorpo + 2.5f) Devolver(z);
            }
        }

        /// <summary>
        /// Um tiro que acertou um zumbi, confirmado aqui no anfitrião. A cópia de quem atirou pode estar um pouco
        /// atrasada, então a conferência é tolerante (é co-op): o zumbi tem que existir (mesmo Id e geração), estar
        /// vivo e perto do ponto do acerto.
        /// </summary>
        public static void Acertar(int id, byte geracao, RegiaoDoCorpo regiao, Membro membro, float dano, Vector3 ponto, bool arremesso, JogadorRede autor, float corte = 1f)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned || !m.IsServer || id < 0 || id >= MaxZumbis) return;
            var z = m._porId[id];
            if (z == null || z.Geracao != geracao || z.Morto) return;
            if ((z.transform.position + Vector3.up * 0.9f - ponto).sqrMagnitude > 3.5f * 3.5f) return;
            // a direção do tiro (do olho de quem atirou ao ponto) empurra o corpo, se ele cair
            Vector3 direcao = autor != null ? ponto - (autor.transform.position + Vector3.up * 1.6f) : -z.transform.forward;
            // o limite (é co-op, mas um número absurdo não passa): o maior acerto é o carro muito rápido (500 a 110 km/h)
            z.LevarTiro(regiao, membro, Mathf.Clamp(dano, 0f, 1000f), ponto, direcao, arremesso, autor, Time.time, Mathf.Clamp(corte, 0f, 4f));
        }

        internal void Golpear(JogadorRede alvo, float dano, Vector3 de)
        {
            if (!SemDano && alvo != null && alvo.Vida != null) alvo.Vida.Dano(dano, de);
        }

        /// <summary>Um tiro, no anfitrião: quem está perto vai atrás de quem atirou; quem está mais longe vai ver o que foi.</summary>
        public static void Barulho(Vector3 ponto, JogadorRede autor)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned || !m.IsServer) return;
            float agora = Time.time;
            // de dia e ao amanhecer, o tiro só tira da sombra quem está bem perto (5 m; à noite, 12 m)
            float direto = m.ProtegerDoSol ? m.ajustes.tiroAmeacaDeDia : m.ajustes.tiroChamaDireto;
            float raio2 = m.ajustes.barulhoDoTiro * m.ajustes.barulhoDoTiro, direto2 = direto * direto;
            foreach (var z in m._ativos)
            {
                if (z.NivelAtual == Zumbi.Nivel.Longe || z.Morto || z.Atual == Zumbi.Estado.Perseguindo || z.Atual == Zumbi.Estado.Atacando) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                if (d2 > raio2 || BunkerRede.Isolado(ponto, z.transform.position)) continue;   // a porta do bunker fechada
                if (d2 < direto2 && autor != null)
                {
                    z.Ameacar(agora);   // um tiro bem perto: sai atrás de quem atirou, mesmo de dia
                    z.ComecarPerseguicao(autor, agora);
                }
                else
                {
                    // vai ver o que foi: cada um num ponto (quanto mais longe, mais espalhados), e não todos na mesma linha
                    Vector2 erro = Random.insideUnitCircle * Mathf.Clamp(Mathf.Sqrt(d2) * 0.2f, 3f, 7f);
                    var ver = ponto + new Vector3(erro.x, 0f, erro.y);
                    if (m.ProtegerDoSol && m.Exposto(ver)) continue;   // de dia, não vai para o sol ver um barulho
                    z.Investigar(ver, agora);
                }
            }
        }

        // ------------------------------------------------------------------ LOD de IA

        /// <summary>O nível para esta distância, com histerese a partir do nível atual.</summary>
        internal Zumbi.Nivel NivelPara(float d, Zumbi.Nivel atual)
        {
            switch (atual)
            {
                case Zumbi.Nivel.Perto:
                    return d > distanciaMedia ? Zumbi.Nivel.Longe : d > distanciaPerto ? Zumbi.Nivel.Medio : Zumbi.Nivel.Perto;
                case Zumbi.Nivel.Medio:
                    return d < distanciaPerto - histerese ? Zumbi.Nivel.Perto : d > distanciaMedia ? Zumbi.Nivel.Longe : Zumbi.Nivel.Medio;
                default:
                    return d < distanciaPerto - histerese ? Zumbi.Nivel.Perto : d < distanciaMedia - histerese ? Zumbi.Nivel.Medio : Zumbi.Nivel.Longe;
            }
        }

        void VarrerNiveis()
        {
            using (MarcaLOD.Auto())
            {
                var local = AlvoLocal();
                // a câmera da cena de vídeo também conta, com o alcance dela: vista do alto, os zumbis aparecem e andam de longe
                var olhoDoVideo = ModoVideo.Olho;
                float alcanceDoVideo = Mathf.Max(1f, ModoVideo.AlcanceDosZumbis);
                for (int i = 0; i < _ativos.Count; i++)
                {
                    var z = _ativos[i];
                    Vector3 p = z.transform.position;
                    float doVideo = olhoDoVideo.HasValue ? Vector3.Distance(olhoDoVideo.Value, p) / alcanceDoVideo : float.MaxValue;
                    float menor = doVideo;
                    for (int k = 0; k < QuantosJogadores; k++) menor = Mathf.Min(menor, Distancia(Jogadores[k], p));
                    var nivel = menor < float.MaxValue ? NivelPara(menor, z.NivelAtual) : Zumbi.Nivel.Longe;
                    if (z.Engajado || z.SempreDePerto(menor)) nivel = Zumbi.Nivel.Perto;
                    z.MudarNivelNoServidor(nivel);
                    float daqui = Mathf.Min(Distancia(local, p), doVideo);
                    z.DefinirVisivel(daqui < local.visao);
                    z.DefinirDetalhe(daqui, distanciaDoLOD1, distanciaDoLOD2);
                }
            }
        }

        void PensarEmFatias(float agora)
        {
            using (MarcaIA.Auto())
            {
                int n = _ativos.Count, feitos = 0;
                for (int k = 0; k < n && feitos < orcamentoPorQuadro; k++)
                {
                    _cursor = (_cursor + 1) % n;
                    var z = _ativos[_cursor];
                    if (z.NivelAtual == Zumbi.Nivel.Longe || z.Morto || agora < z.proximaDecisao) continue;
                    float dt = Mathf.Min(agora - z.ultimaDecisao, 0.6f);
                    z.ultimaDecisao = agora;
                    z.proximaDecisao = agora + (z.NivelAtual == Zumbi.Nivel.Perto ? intervaloPerto : intervaloMedio) * Random.Range(0.85f, 1.15f);
                    z.Pensar(agora, dt);
                    feitos++;
                }
            }
        }

        /// <summary>De onde quem joga nesta máquina está olhando (para desenhar e para os gemidos).</summary>
        Vector3 PontoDeVista()
        {
            if (ModoVideo.Olho is Vector3 olhoDoVideo) return olhoDoVideo;   // a câmera da cena de vídeo (e o ouvido dela)
            if (JogadorRede.Local != null) return JogadorRede.Local.transform.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        public void ContarNiveis(out int perto, out int medio, out int longe)
        {
            perto = medio = longe = 0;
            var lista = IsServer ? _ativos : _copias;
            foreach (var z in lista)
            {
                if (z.NivelAtual == Zumbi.Nivel.Perto) perto++;
                else if (z.NivelAtual == Zumbi.Nivel.Medio) medio++;
                else longe++;
            }
        }

        // ------------------------------------------------------------------ população e pool

        /// <summary>Recria a população nos pontos de horda (filhos deste objeto).</summary>
        public void RepovoarHordas()
        {
            var posicoes = new List<Vector3>();
            foreach (var h in GetComponentsInChildren<PontoDeHorda>())
                for (int i = 0; i < h.quantidade; i++)
                    if (Sortear(h.transform.position, h.raio, out var p)) posicoes.Add(p);
            Povoar(posicoes);
        }

        /// <summary>Troca a população por esta quantidade de zumbis espalhados pela cidade inteira (benchmark).</summary>
        public void RepovoarEspalhado(int quantidade, float raioDaCidade = 56f)
        {
            var posicoes = new List<Vector3>();
            for (int i = 0; i < quantidade * 6 && posicoes.Count < quantidade; i++)
            {
                var c = new Vector3(Random.Range(-raioDaCidade, raioDaCidade), 0.2f, Random.Range(-raioDaCidade, raioDaCidade));
                if (Sortear(c, 0f, out var p)) posicoes.Add(p);
            }
            Povoar(posicoes);
        }

        /// <summary>Zumbis de pé: no anfitrião, os da cidade; nos clientes, as cópias recebidas (só os de perto).</summary>
        public int Vivos
        {
            get
            {
                int n = 0;
                foreach (var z in IsServer ? _ativos : _copias)
                    if (!z.Morto) n++;
                return n;
            }
        }

        /// <summary>Só no anfitrião (o menu do desenvolvedor): todos os zumbis de pé caem mortos, como com um tiro no corpo.</summary>
        public void MatarTodos()
        {
            if (!IsServer || !IsSpawned) return;
            float agora = Time.time;
            foreach (var z in _ativos)
                if (!z.Morto) z.LevarTiro(RegiaoDoCorpo.Tronco, Membro.Nenhum, 1000f, z.transform.position + Vector3.up, -z.transform.forward, false, null, agora);
        }

        /// <summary>
        /// Só no anfitrião (o menu do desenvolvedor): mais zumbis, de 6 a 14 m em volta do ponto, sem tirar os que já
        /// estão (até nas áreas livres, como o bunker). Devolve quantos lugares achou no NavMesh.
        /// </summary>
        public int CriarPerto(Vector3 centro, int quantidade)
        {
            if (!IsServer || !IsSpawned) return 0;
            var posicoes = new List<Vector3>();
            for (int t = 0; t < quantidade * 8 && posicoes.Count < quantidade; t++)
            {
                Vector2 d = Random.insideUnitCircle.normalized * Random.Range(6f, 14f);
                if (Sortear(centro + new Vector3(d.x, 0f, d.y), 1.5f, out var p, false)) posicoes.Add(p);
            }
            StartCoroutine(Acrescentar(posicoes));
            return posicoes.Count;
        }

        IEnumerator Acrescentar(List<Vector3> posicoes)
        {
            for (int i = 0; i < posicoes.Count; i++)
            {
                Nascer(posicoes[i]);
                if ((i + 1) % criadosPorQuadro == 0) yield return null;
            }
        }

        bool Sortear(Vector3 centro, float raio, out Vector3 ponto, bool foraDasAreasLivres = true)
        {
            for (int t = 0; t < 8; t++)
            {
                Vector2 r = Random.insideUnitCircle * raio;
                var tentativa = centro + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(tentativa, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - centro.y) < 1.2f
                    && !(foraDasAreasLivres && EmAreaLivre(hit.position)))
                {
                    ponto = hit.position;
                    return true;
                }
            }
            ponto = default;
            return false;
        }

        bool EmAreaLivre(Vector3 p)
        {
            if (areasLivres != null)
                foreach (var a in areasLivres)
                    if ((p - a.centro).sqrMagnitude < a.raio * a.raio) return true;
            return false;
        }

        void Povoar(List<Vector3> posicoes)
        {
            if (_povoando != null) StopCoroutine(_povoando);
            Limpar();
            _povoando = StartCoroutine(Criar(posicoes));
        }

        IEnumerator Criar(List<Vector3> posicoes)
        {
            for (int i = 0; i < posicoes.Count; i++)
            {
                Nascer(posicoes[i]);
                if ((i + 1) % criadosPorQuadro == 0) yield return null;
            }
            _povoando = null;
        }

        /// <summary>
        /// O modelo de um Oco: sai do Id e da geração. Desde 01/11/2026 o modelo vai pela rede (os especiais são escolhidos,
        /// não sorteados pelo Id): os Ocos são sorteados só entre os modelos comuns.
        /// </summary>
        int ModeloDe(int id, byte geracao)
        {
            var comuns = ModelosDoTipo(TipoDeZumbi.Oco);
            return comuns.Length == 0 ? (id * 7 + geracao * 3) % modelos.Length : comuns[(id * 7 + geracao * 3) % comuns.Length];
        }

        Zumbi Nascer(Vector3 posicao) => Nascer(posicao, -1);

        /// <summary>Um zumbi novo: o modelo dado, ou um Oco (às vezes um Corredor: ZombieManager.Especiais.cs).</summary>
        Zumbi Nascer(Vector3 posicao, int modelo)
        {
            int id = _idsLivres.Count > 0 ? _idsLivres.Pop() : _proximoId++;
            if (id >= MaxZumbis) return null;
            var z = PegarDoPool(modelo >= 0 ? modelo : ModeloParaNascer(id, _geracoes[id]));
            _porId[id] = z;
            _ativos.Add(z);
            z.PrepararNoServidor(id, _geracoes[id], posicao, Random.Range(0f, 360f), this);
            return z;
        }

        void Devolver(Zumbi z)
        {
            _ativos.Remove(z);
            _porId[z.Id] = null;
            _geracoes[z.Id]++;
            _idsLivres.Push(z.Id);
            foreach (var niveis in _niveisPorCliente.Values) niveis[z.Id] = (byte)Zumbi.Nivel.Longe;
            GuardarNoPool(z);
        }

        void Limpar()
        {
            for (int i = _ativos.Count - 1; i >= 0; i--) Devolver(_ativos[i]);
            _cursor = 0;
        }

        Zumbi PegarDoPool(int modelo)
        {
            if (_pools == null)
            {
                _pools = new Stack<Zumbi>[modelos.Length];
                for (int i = 0; i < _pools.Length; i++) _pools[i] = new Stack<Zumbi>();
            }
            var pool = _pools[modelo];
            if (pool.Count > 0)
            {
                var z = pool.Pop();
                z.gameObject.SetActive(true);
                return z;
            }
            if (_vivos == null) _vivos = new GameObject("Zumbis vivos").transform;
            return Instantiate(modelos[modelo], _vivos);
        }

        void GuardarNoPool(Zumbi z)
        {
            z.Zerar();
            z.gameObject.SetActive(false);
            _pools[z.modelo].Push(z);
        }

        // ------------------------------------------------------------------ voz

        void GemidosAoAcaso()
        {
            if ((vozDoZumbi == null && Sons == null) || Time.time < _proximoGemido) return;
            _proximoGemido = Time.time + Random.Range(2.5f, 6f);
            var lista = IsServer ? _ativos : _copias;
            if (lista.Count == 0) return;
            Vector3 ouvinte = PontoDeVista();
            for (int t = 0; t < 12; t++)
            {
                var z = lista[Random.Range(0, lista.Count)];
                if (z.NivelAtual != Zumbi.Nivel.Longe && (z.transform.position - ouvinte).sqrMagnitude < 18f * 18f)
                {
                    // de vez em quando, um chama (e os de perto respondem): eles "conversam" (Zumbi.Voz.cs)
                    if (Sons != null && Random.value < 0.22f) z.Chamar();
                    else z.Gemer(0.9f);
                    return;
                }
            }
        }

        /// <summary>
        /// Só geme quem está perto de quem escuta (e do mesmo lado da porta do bunker, se ela está fechada), e no máximo
        /// três vozes novas por segundo.
        /// </summary>
        internal bool PodeGemer(Vector3 ponto)
        {
            var ouvido = PontoDeVista();
            if ((ponto - ouvido).sqrMagnitude > 25f * 25f || BunkerRede.Isolado(ouvido, ponto)) return false;
            if (Time.time - _segundoDosGemidos > 1f)
            {
                _segundoDosGemidos = Time.time;
                _gemidosNoSegundo = 0;
            }
            return _gemidosNoSegundo++ < 3;
        }

        internal Vector2 TrechoDaVoz() => trechosDaVoz[Random.Range(0, trechosDaVoz.Length)];

        // ------------------------------------------------------------------ números na tela (F3)

        void OnGUI()
        {
            if (!_info || !IsSpawned || MenuOnline.Aberto || ModoVideo.Ligado) return;
            ContarNiveis(out int perto, out int medio, out int longe);
            float u = Screen.height / 720f;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u) };
            estilo.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            string texto = IsServer
                ? $"Zumbis: {_ativos.Count}  ·  perto {perto}  ·  médio {medio}  ·  longe {longe}  ·  mortos {Mortos}  ·  rede {KbPorSegundo:F1} KB/s"
                : $"Zumbis recebidos: {_copias.Count}  ·  perto {perto}  ·  médio {medio}";
            GUI.Label(new Rect(14 * u, 36 * u, 900 * u, 24 * u), texto, estilo);
        }
    }
}
