using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
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
    /// Nos clientes, mantém as cópias. F3 mostra os números na tela.
    /// </summary>
    public partial class ZombieManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct AreaLivre
        {
            public Vector3 centro;
            public float raio;
        }

        /// <summary>O que um zumbi precisa saber de cada jogador, montado uma vez por quadro.</summary>
        internal struct Alvo
        {
            public JogadorRede jogador;
            public Vector3 pes, peito, olhar;
            public bool lanterna, correndo, iluminado;
        }

        public const int MaxZumbis = 1024;
        // só o cenário (camada Default) tapa a visão; as cápsulas e hitboxes dos zumbis ficam em outras camadas
        const int MascaraDaVisao = 1 << 0;

        public static ZombieManager Instancia { get; private set; }

        [Header("Peças")]
        public Zumbi prefab;
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
        [Tooltip("Além disto o anfitrião nem desenha o zumbi: a neblina já esconde.")]
        public float distanciaDeDesenho = 65f;

        public AjustesDeZumbi ajustes = new AjustesDeZumbi();

        static readonly ProfilerMarker MarcaIA = new("Zumbis.IA");
        static readonly ProfilerMarker MarcaLOD = new("Zumbis.LOD");

        internal readonly Alvo[] Jogadores = new Alvo[8];
        internal int QuantosJogadores { get; private set; }
        /// <summary>Benchmark: os zumbis atacam, mas não tiram vida.</summary>
        public bool SemDano { get; set; }
        public bool Povoando => _povoando != null;
        /// <summary>Zumbis vivos (no anfitrião) ou cópias recebidas (nos clientes).</summary>
        public int Quantos => IsServer ? _ativos.Count : _copias.Count;

        readonly List<Zumbi> _ativos = new List<Zumbi>();
        readonly Stack<Zumbi> _pool = new Stack<Zumbi>();
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
                if (agora >= _proximaVarredura)
                {
                    _proximaVarredura = agora + 0.2f;
                    VarrerNiveis();
                }
                PensarEmFatias(agora);
                for (int i = 0; i < _ativos.Count; i++)
                    if (_ativos[i].NivelAtual != Zumbi.Nivel.Longe) _ativos[i].QuadroNoServidor(dt);
                EnviarParaClientes();
            }
            else AtualizarCopias();
            GemidosAoAcaso();
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            var lista = IsServer ? _ativos : _copias;
            for (int i = 0; i < lista.Count; i++)
                if (lista[i].TemBote) lista[i].AplicarBote();
        }

        // ------------------------------------------------------------------ jogadores e percepção

        void AtualizarJogadores()
        {
            int n = 0;
            bool noite = CicloDoDia.Noite > 0.5f;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || n == Jogadores.Length) continue;
                Vector3 pes = j.transform.position;
                Jogadores[n++] = new Alvo
                {
                    jogador = j,
                    pes = pes,
                    peito = pes + Vector3.up * 1.3f,
                    olhar = Quaternion.Euler(j.Pitch.Value, j.transform.eulerAngles.y, 0f) * Vector3.forward,
                    lanterna = j.Lanterna != null && j.Lanterna.Ligada.Value,
                    correndo = j.Velocidade > 3f,
                    iluminado = noite && PosteDeLuz.Ilumina(pes),
                };
            }
            QuantosJogadores = n;
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
            // para meio metro antes: o próprio jogador (CharacterController, na camada Default) não tapa a visão
            Vector3 para = alvo - olho;
            float d = para.magnitude;
            if (d < 0.6f) return true;
            return !Physics.Raycast(olho, para / d, d - 0.5f, MascaraDaVisao, QueryTriggerInteraction.Ignore);
        }

        internal void Engajou(Zumbi z) => z.MudarNivelNoServidor(Zumbi.Nivel.Perto);

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
            float raio2 = m.ajustes.barulhoDoTiro * m.ajustes.barulhoDoTiro, direto2 = m.ajustes.tiroChamaDireto * m.ajustes.tiroChamaDireto;
            foreach (var z in m._ativos)
            {
                if (z.NivelAtual == Zumbi.Nivel.Longe || z.Atual == Zumbi.Estado.Perseguindo || z.Atual == Zumbi.Estado.Atacando) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                if (d2 > raio2) continue;
                if (d2 < direto2 && autor != null) z.ComecarPerseguicao(autor, agora);
                else
                {
                    Vector2 erro = Random.insideUnitCircle * 3f;
                    z.Investigar(ponto + new Vector3(erro.x, 0f, erro.y), agora);
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
                Vector3 local = PontoDeVista();
                float desenho2 = distanciaDeDesenho * distanciaDeDesenho;
                for (int i = 0; i < _ativos.Count; i++)
                {
                    var z = _ativos[i];
                    Vector3 p = z.transform.position;
                    var nivel = Zumbi.Nivel.Longe;
                    if (QuantosJogadores > 0)
                    {
                        float menor = float.MaxValue;
                        for (int k = 0; k < QuantosJogadores; k++) menor = Mathf.Min(menor, (Jogadores[k].pes - p).sqrMagnitude);
                        nivel = NivelPara(Mathf.Sqrt(menor), z.NivelAtual);
                    }
                    if (z.Engajado) nivel = Zumbi.Nivel.Perto;
                    z.MudarNivelNoServidor(nivel);
                    z.DefinirVisivel((local - p).sqrMagnitude < desenho2);
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
                    if (z.NivelAtual == Zumbi.Nivel.Longe || agora < z.proximaDecisao) continue;
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

        bool Sortear(Vector3 centro, float raio, out Vector3 ponto)
        {
            for (int t = 0; t < 8; t++)
            {
                Vector2 r = Random.insideUnitCircle * raio;
                var tentativa = centro + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(tentativa, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - centro.y) < 1.2f && !EmAreaLivre(hit.position))
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

        Zumbi Nascer(Vector3 posicao)
        {
            int id = _idsLivres.Count > 0 ? _idsLivres.Pop() : _proximoId++;
            if (id >= MaxZumbis) return null;
            var z = PegarDoPool();
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

        Zumbi PegarDoPool()
        {
            if (_pool.Count > 0)
            {
                var z = _pool.Pop();
                z.gameObject.SetActive(true);
                return z;
            }
            if (_vivos == null) _vivos = new GameObject("Zumbis vivos").transform;
            return Instantiate(prefab, _vivos);
        }

        void GuardarNoPool(Zumbi z)
        {
            z.Zerar();
            z.gameObject.SetActive(false);
            _pool.Push(z);
        }

        // ------------------------------------------------------------------ voz

        void GemidosAoAcaso()
        {
            if (vozDoZumbi == null || Time.time < _proximoGemido) return;
            _proximoGemido = Time.time + Random.Range(2.5f, 6f);
            var lista = IsServer ? _ativos : _copias;
            if (lista.Count == 0) return;
            Vector3 ouvinte = PontoDeVista();
            for (int t = 0; t < 12; t++)
            {
                var z = lista[Random.Range(0, lista.Count)];
                if (z.NivelAtual != Zumbi.Nivel.Longe && (z.transform.position - ouvinte).sqrMagnitude < 18f * 18f)
                {
                    z.Gemer(0.9f);
                    return;
                }
            }
        }

        /// <summary>Só geme quem está perto de quem escuta, e no máximo três vozes novas por segundo.</summary>
        internal bool PodeGemer(Vector3 ponto)
        {
            if ((ponto - PontoDeVista()).sqrMagnitude > 25f * 25f) return false;
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
            if (!_info || !IsSpawned || MenuOnline.Aberto) return;
            ContarNiveis(out int perto, out int medio, out int longe);
            float u = Screen.height / 720f;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u) };
            estilo.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            string texto = IsServer
                ? $"Zumbis: {_ativos.Count}  ·  perto {perto}  ·  médio {medio}  ·  longe {longe}  ·  rede {KbPorSegundo:F1} KB/s"
                : $"Zumbis recebidos: {_copias.Count}  ·  perto {perto}  ·  médio {medio}";
            GUI.Label(new Rect(14 * u, 36 * u, 900 * u, 24 * u), texto, estilo);
        }
    }
}
