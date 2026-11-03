using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>Quanto um comerciante tem de um produto (o produto é o índice em <see cref="PrecosDosSobreviventes.Itens"/>).</summary>
    public struct EstoqueDoComerciante : INetworkSerializeByMemcpy, IEquatable<EstoqueDoComerciante>
    {
        public byte acampamento, banca, produto, qtd;

        public bool Equals(EstoqueDoComerciante o) => acampamento == o.acampamento && banca == o.banca && produto == o.produto && qtd == o.qtd;
    }

    /// <summary>
    /// Os acampamentos dos sobreviventes (02/11/2026, a etapa 3 do plano, a pedido: "acampamentos fora da cidade em uma casa
    /// já existente com barricadas, fogueiras, guardas e comerciantes"), no objeto "Mundo":
    ///  - <b>a construção</b> (em cada máquina, perto: AcampamentosDoMundo.Construcao.cs): a casa fortificada, a cerca, as
    ///    bancas, as barracas, as fogueiras e as luzes, pela <see cref="PlantaDoAcampamento"/>;
    ///  - <b>as pessoas</b> (no anfitrião, com alguém perto: AcampamentosDoMundo.Pessoas.cs): os guardas, os comerciantes e
    ///    os moradores, o mesmo <see cref="Sobrevivente"/> dos grupos, com os papéis do acampamento;
    ///  - <b>a zona segura</b>: zumbi não nasce, não vaga nem persegue ninguém lá dentro (<see cref="ZombieManager.ZonasSeguras"/>);
    ///  - <b>a hostilidade</b> (na rede): ferir qualquer um deles deixa o acampamento hostil a todos, para sempre neste mundo;
    ///  - <b>o comércio</b> (AcampamentosDoMundo.Comercio.cs): o estoque de cada comerciante (na rede), a compra e a venda
    ///    conferidas pelo anfitrião, e a reposição a cada amanhecer;
    ///  - <b>o save</b>: os hostis, os mortos (continuam mortos), o estoque e o dia da última reposição.
    /// </summary>
    public partial class AcampamentosDoMundo : NetworkBehaviour
    {
        public static AcampamentosDoMundo Instancia { get; private set; }

        [Tooltip("Até onde a construção do acampamento aparece em volta do jogador local (m, do meio dele).")]
        public float raioDaConstrucao = 230f;
        [Tooltip("As pessoas aparecem com alguém a menos de (o raio do acampamento + isto) m do meio dele.")]
        public float chegarPerto = 35f;
        [Tooltip("E saem com todos a mais de (o raio + isto) m.")]
        public float sairLonge = 110f;

        /// <summary>O grupo das pessoas de um acampamento na rede é este mais o índice dele.</summary>
        public const int GrupoBase = 1000;

        readonly NetworkVariable<int> _hostis = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        NetworkList<EstoqueDoComerciante> _estoque;

        // no anfitrião
        sealed class Estado
        {
            public int mortos;
            public int diaDaReposicao = -1;
            public GrupoDeSobreviventes grupo;
            public float proximaFalaDaMira;
            public readonly Dictionary<JogadorRede, float> mirando = new Dictionary<JogadorRede, float>();
        }

        readonly List<Estado> _estados = new List<Estado>();
        readonly List<ZombieManager.AreaLivre> _zonas = new List<ZombieManager.AreaLivre>();
        bool _comecou, _estoqueFeito;
        float _proximaConferencia, _proximoPensar;

        void Awake()
        {
            Instancia = this;
            _estoque = new NetworkList<EstoqueDoComerciante>();
        }

        public override void OnDestroy()
        {
            ApagarConstrucoes();
            TirarAsZonas();
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Instancia = this;
            _comecou = false;
            _estoqueFeito = false;
            if (IsServer)
            {
                _estoque.Clear();
                _hostis.Value = 0;
            }
        }

        public override void OnNetworkDespawn()
        {
            TirarAsZonas();
            _estados.Clear();
            ApagarConstrucoes();
        }

        static DadosDoMundo Dados
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                return m != null && m.Pronto && !m.NoDiaZero ? m.Dados : null;
            }
        }

        /// <summary>Quantos acampamentos o mundo tem.</summary>
        public int Quantos => Dados?.Plano?.Acampamentos.Count ?? 0;

        /// <summary>O desenho do acampamento (null fora do mundo).</summary>
        public PlantaDoAcampamento Planta(int a)
        {
            var d = Dados;
            if (d?.Plano == null || a < 0 || a >= d.Plano.Acampamentos.Count) return null;
            return PlantaDoAcampamento.Da(d, d.Plano.Acampamentos[a]);
        }

        /// <summary>O acampamento ficou contra os jogadores (alguém feriu um deles).</summary>
        public bool Hostil(int a) => a >= 0 && a < 31 && (_hostis.Value & (1 << a)) != 0;

        /// <summary>O acampamento mais perto do ponto e a distância até o meio dele (-1 sem nenhum).</summary>
        public int MaisPerto(Vector3 ponto, out float distancia)
        {
            distancia = float.MaxValue;
            int melhor = -1;
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                float d = Vector2.Distance(new Vector2(ponto.x, ponto.z), p.Lugar.Centro);
                if (d < distancia)
                {
                    distancia = d;
                    melhor = a;
                }
            }
            return melhor;
        }

        void Update()
        {
            if (!IsSpawned) return;
            AtualizarConstrucoes();
            if (!IsServer || Dados == null) return;
            if (!_comecou) Comecar();
            float agora = Time.time;
            if (agora >= _proximoPensar)
            {
                _proximoPensar = agora + 0.2f;
                for (int a = 0; a < _estados.Count; a++)
                {
                    _estados[a].grupo?.Pensar();
                    VigiarAMira(a, 0.2f);
                }
            }
            if (agora < _proximaConferencia) return;
            _proximaConferencia = agora + 1f;
            ConferirAsPessoas();
            Repor(false);
        }

        // o mundo ficou pronto: os estados, o estoque (se não veio do save) e as zonas seguras
        void Comecar()
        {
            _comecou = true;
            Estados();
            if (!_estoqueFeito) EncherOEstoque();
            PorAsZonas();
            Debug.Log($"[Acampamentos] {Quantos} acampamentos no mundo, {_estoque.Count} produtos nos estoques.");
        }

        void Estados()
        {
            while (_estados.Count < Quantos) _estados.Add(new Estado { diaDaReposicao = CicloDoDia.Dia });
        }

        // ------------------------------------------------------------------ a zona segura

        void PorAsZonas()
        {
            TirarAsZonas();
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                var z = new ZombieManager.AreaLivre { centro = p.Mundo(Vector3.zero), raio = p.Lugar.Raio + 15f };
                _zonas.Add(z);
                ZombieManager.ZonasSeguras.Add(z);
            }
        }

        void TirarAsZonas()
        {
            foreach (var z in _zonas) ZombieManager.ZonasSeguras.Remove(z);
            _zonas.Clear();
        }

        // ------------------------------------------------------------------ a hostilidade

        /// <summary>No anfitrião: alguém feriu uma pessoa do acampamento; ele fica contra todos os jogadores, para sempre.</summary>
        public void TornarHostil(int a, JogadorRede quem)
        {
            if (!IsServer || a < 0 || a >= 31) return;
            bool novo = !Hostil(a);
            _hostis.Value |= 1 << a;
            Estados();
            var g = a < _estados.Count ? _estados[a].grupo : null;
            if (g != null && quem != null) g.Ver(quem, quem.transform.position, null);
            if (!novo) return;
            Debug.Log($"[Acampamentos] O {Planta(a)?.Lugar.Nome} ficou hostil.");
            if (g != null)
                foreach (var m in g.membros)
                    if (m != null && !m.Morto)
                    {
                        m.Falar(Frase.Traidor);
                        break;
                    }
        }

        /// <summary>No anfitrião (o F1): o acampamento volta a ser amigo, ou fica hostil.</summary>
        public void DefinirHostil(int a, bool hostil)
        {
            if (!IsServer || a < 0 || a >= 31) return;
            if (hostil) _hostis.Value |= 1 << a;
            else
            {
                _hostis.Value &= ~(1 << a);
                if (a < _estados.Count && _estados[a].grupo != null)
                {
                    var g = _estados[a].grupo;
                    g.estado = EstadoDoGrupo.Explorando;
                    g.inimigo = null;
                }
            }
        }

        // ------------------------------------------------------------------ o save

        /// <summary>O estado de cada acampamento (o save): o índice, hostil, os mortos, o dia da reposição e o estoque (byte a byte: banca, produto, qtd).</summary>
        public List<(int acampamento, bool hostil, int mortos, int dia, List<int> estoque)> Copia()
        {
            var lista = new List<(int, bool, int, int, List<int>)>();
            Estados();
            for (int a = 0; a < Quantos; a++)
            {
                var estoque = new List<int>();
                foreach (var x in _estoque)
                    if (x.acampamento == a) estoque.Add((x.banca << 16) | (x.produto << 8) | x.qtd);
                var e = a < _estados.Count ? _estados[a] : null;
                lista.Add((a, Hostil(a), e?.mortos ?? 0, e?.diaDaReposicao ?? -1, estoque));
            }
            return lista;
        }

        public void Restaurar(IEnumerable<(int acampamento, bool hostil, int mortos, int dia, List<int> estoque)> lista)
        {
            if (!IsServer) return;
            Estados();
            int hostis = 0;
            bool algum = false;
            var novo = new List<EstoqueDoComerciante>();
            foreach (var (a, hostil, mortos, dia, estoque) in lista)
            {
                if (a < 0 || a >= _estados.Count) continue;
                algum = true;
                if (hostil && a < 31) hostis |= 1 << a;
                _estados[a].mortos = mortos;
                _estados[a].diaDaReposicao = dia;
                if (estoque == null) continue;
                foreach (int v in estoque)
                    novo.Add(new EstoqueDoComerciante { acampamento = (byte)a, banca = (byte)((v >> 16) & 0xFF), produto = (byte)((v >> 8) & 0xFF), qtd = (byte)(v & 0xFF) });
            }
            if (!algum) return;
            _hostis.Value = hostis;
            _estoque.Clear();
            foreach (var x in novo) _estoque.Add(x);
            _estoqueFeito = true;
        }
    }
}
