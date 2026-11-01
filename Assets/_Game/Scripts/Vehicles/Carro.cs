using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// Um carro que se dirige, com 4 lugares (0 o motorista, 1 o do lado, 2 e 3 atrás), cada um com a sua porta.
    /// <para>
    /// Rede: quem está no volante é o dono do carro (o anfitrião passa a posse quando ele senta) e simula a física
    /// (Rigidbody e WheelColliders); nos outros, o corpo fica cinemático e a posição vem do NetworkTransform. Sem
    /// motorista, o dono volta a ser o anfitrião, e o carro fica freado. Quem está em cada lugar é do anfitrião
    /// (<see cref="_ocupantes"/>): o jogador pede para entrar (E na porta) ou sair (E dentro), ele confere e muda.
    /// Cada máquina, ao ver um lugar mudar, toca a porta (abre, a pessoa entra ou sai, fecha) e põe o jogador sentado
    /// ou de pé (<see cref="PassageiroDoCarro"/>).
    /// </para>
    /// <para>
    /// Atropelar: o dono, acima de 3 m/s, procura zumbis na frente (ou atrás, de ré) e manda o acerto ao anfitrião pelo
    /// motorista, como um tiro no tronco (o dano cresce com a velocidade; rápido, o corpo voa).
    /// </para>
    /// <para>
    /// O capô (<see cref="ICapo"/>, aberto pela <see cref="CapoDoCarro"/>): as 3 peças do motor (<see cref="PecasDoMotor"/>,
    /// do anfitrião). Sem uma vela boa o motor não pega; a bateria (a carga) acende o painel e gira o arranque, e gasta na
    /// partida (o motor ligado carrega); o radiador ruim deixa o motor ferver, e ele apaga até esfriar. O capô sobe
    /// enquanto alguém olha o motor.
    /// </para>
    /// <para>
    /// O porta-malas (<see cref="PortaMalasDoCarro"/>, do anfitrião): a tampa traseira abre e fecha, e dentro ficam o
    /// macaco e o estepe, que se pegam e se guardam.
    /// </para>
    /// <para>
    /// O macaco (<see cref="RodaDoCarro"/>, do anfitrião): apoiado ao lado de uma roda, com o carro vazio e parado, ele
    /// levanta aquele lado (o carro fica cinemático e gira em volta das rodas do outro lado), e as rodas do lado erguido
    /// saem e entram.
    /// </para>
    /// <para>
    /// As rodas (<see cref="PneusDoCarro"/>, do anfitrião): o pneu furado baixa a roda, segura menos e arrasta (o carro
    /// puxa para aquele lado); o gasto escorrega mais; sem a roda, o cubo arrasta no chão.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Carro : NetworkBehaviour, ICapo
    {
        public const int Lugares = 4;
        /// <summary>A camada do carro: não bate nos zumbis (eles são atropelados por código) nem na cápsula de perto deles.</summary>
        public const int CamadaVeiculo = 11;
        /// <summary>A porta: abre em <see cref="AbrirPorta"/> s, espera e começa a fechar em <see cref="FecharEm"/> s.</summary>
        public const float AbrirPorta = 0.4f, FecharEm = 0.85f, FecharPorta = 0.3f;
        /// <summary>Até que velocidade (m/s) dá para entrar e sair.</summary>
        public const float VelocidadeParaEntrar = 2.5f, VelocidadeParaSair = 3f;

        public static readonly List<Carro> Todos = new List<Carro>();

        [Header("Peças (na ordem: dianteira esquerda, dianteira direita, traseira esquerda, traseira direita)")]
        public WheelCollider[] rodas;
        public Transform[] rodasVisuais;
        public Transform volante;
        [Tooltip("A porta de cada lugar.")]
        public PortaDoCarro[] portas;
        [Tooltip("O quadril de quem senta em cada lugar (com a rotação do carro).")]
        public Transform[] assentos;
        [Tooltip("Onde quem sai fica de pé, no chão, do lado da porta de cada lugar.")]
        public Transform[] saidas;
        [Tooltip("Quantos lugares este carro usa: os primeiros (0 o motorista, 1 o do lado, 2 e 3 atrás). A perua usa os 4; o " +
                 "cupê, só os 2 da frente (a pedido em 29/10/2026). Os outros ficam no prefab, mas ninguém entra neles.")]
        [Range(1, Lugares)] public int lugaresUsados = Lugares;

        [Header("Luzes")]
        public Light[] farois;
        public MeshRenderer carroceria;
        public int submeshFarol = -1, submeshLanterna = -1;
        public Material materialApagado, farolAceso, lanternaAceso;

        [Header("Som")]
        public AudioSource motor;
        public AudioSource buzina;
        [Tooltip("O motor de arranque, em laço (enquanto a chave fica na partida).")]
        public AudioSource arranque;

        [Header("A ignição (à direita da coluna da direção)")]
        [Tooltip("O miolo da ignição: +Z para dentro do painel. É para ele que o motorista olha para pôr e girar a chave.")]
        public Transform ignicao;
        [Tooltip("A chave: gira em volta do Z do miolo (no sentido do relógio, o giro negativo) e entra pelo -Z.")]
        public Transform chave;
        public MeshRenderer[] pecasDaChave;
        [Tooltip("O anel da ignição: aceso com o motorista sentado e o motor desligado (para achar a chave no escuro).")]
        public MeshRenderer anelDaIgnicao;
        public Material anelApagado, anelAceso;
        [Tooltip("Da ignição para a partida a chave pesa: o quanto do giro do mouse passa (0 a 1).")]
        [Range(0.05f, 1f)] public float pesoDaPartida = 0.3f;
        [Tooltip("A mola da partida: graus por segundo que a chave volta para a ignição se não for empurrada.")]
        public float molaDaPartida = 25f;

        [Header("O painel e as luzes de dentro")]
        [Tooltip("O quadro dos mostradores: aceso (Unlit) com a ignição ligada, apagado (Lit) sem ela.")]
        public MeshRenderer painel;
        public Material painelApagado, painelAceso;
        public Transform ponteiroVelocidade, ponteiroCombustivel;
        public MeshRenderer[] ponteiros;
        public Material ponteiroApagado, ponteiroAceso;
        [Tooltip("A luz verde do farol no painel.")]
        public MeshRenderer indicadorFarol;
        [Tooltip("A luz fraca do painel, que clareia o volante e as mãos (perto do retrovisor).")]
        public Light luzDoPainel;
        public float intensidadeDoPainel = 0.22f;
        [Tooltip("A luz de teto: acende quando uma porta abre e apaga devagar depois que ela fecha.")]
        public Light luzDoTeto;
        public MeshRenderer lenteDoTeto;
        public Material tetoApagado, tetoAceso;
        public float intensidadeDoTeto = 0.5f;
        [Tooltip("Quanto o ponteiro da gasolina mostra (ainda não gasta: o jogo não tem combustível).")]
        [Range(0f, 1f)] public float combustivel = 0.68f;

        [Header("O capô e as peças do motor")]
        [Tooltip("O capô (a origem na dobradiça, atrás): abre girando em X.")]
        public Transform capo;
        public float aberturaDoCapo = 62f;
        [Tooltip("O cofre do motor em 3D (o motor e as peças nos lugares delas), o que se vê com o capô aberto.")]
        public CofreDoMotor cofre;
        [Tooltip("Como as peças começam a partida (%; -1, sem a peça). A família achou o carro com a vela queimada.")]
        public int velaInicial = 0, bateriaInicial = 30, radiadorInicial = 25;
        [Tooltip("Quanto a partida gasta da bateria (% por segundo de arranque) e quanto o motor ligado carrega.")]
        public float gastoDaPartida = 1.5f, cargaDoMotor = 0.4f;
        [Tooltip("Quanto tempo (s) a temperatura leva para chegar perto do alvo, ligado, e para esfriar, desligado.")]
        public float esquentar = 40f, esfriar = 60f;

        [Header("As rodas")]
        [Tooltip("Como os pneus começam a partida (%, na ordem das rodas; -1, sem a roda). A família achou o carro com o da frente esquerda furado.")]
        public int[] pneusIniciais = { 0, 80, 70, 75 };

        [Header("O porta-malas")]
        [Tooltip("A tampa traseira (a origem na dobradiça, em cima): abre girando em X (a borda de baixo sobe).")]
        public Transform tampaTraseira;
        public float aberturaDaTampa = 78f;
        [Tooltip("O macaco e o estepe deitados no piso do porta-malas: aparecem quando estão lá.")]
        public MeshRenderer macacoNoPortaMalas, estepeNoPortaMalas;
        [Tooltip("Como o porta-malas começa a partida: com o macaco, e o estepe (%; -1, sem ele).")]
        public bool macacoInicial = true;
        public int estepeInicial = 85;

        [Header("O macaco")]
        [Tooltip("O macaco apoiado embaixo do carro (a malha do pecas.py): aparece com ele apoiado, esticado do chão até a lataria.")]
        public MeshRenderer macacoApoiado;
        [Tooltip("Quanto o lado do macaco sobe (m, na roda) e em quanto tempo (s).")]
        public float alturaDoMacaco = 0.25f;
        public float tempoDoMacaco = 2.2f;

        [Header("Direção")]
        [Tooltip("Torque em cada roda da frente (N·m), com o acelerador no fundo e o carro parado.")]
        public float torque = 700f;
        [Tooltip("A força do motor cai a zero nesta velocidade (m/s); com o atrito, o carro para um pouco antes (o cupê, com " +
                 "42 e 1.000 N·m, chega a ~110 km/h).")]
        public float velocidadeMaxima = 22f;
        [Tooltip("Onde acaba a 4ª marcha imaginária do som do motor (m/s): perto da velocidade que o carro alcança. A perua: 23.")]
        public float fimDasMarchas = 23f;
        public float velocidadeDeRe = 7f;
        public float freio = 2400f;
        public float freioDeMao = 4500f;
        [Tooltip("O quanto as rodas viram parado (graus); rápido, vira menos.")]
        public float giroMaximo = 32f;
        public float giroNaVelocidadeMaxima = 12f;
        [Tooltip("Graus por segundo que as rodas viram com o teclado.")]
        public float velocidadeDoVolante = 110f;
        [Tooltip("A barra estabilizadora (N): segura o carro nas curvas, sem capotar.")]
        public float antiCapotar = 7000f;
        // o volante desenhado (a pedido em 29/10/2026: "gira muito rápido na visão em primeira pessoa... só o visual"): gira
        // menos (era 12 vezes as rodas: 384° no máximo) e segue as rodas com um pouco de atraso, no máximo a tantos graus
        // por segundo (as rodas viram na mesma hora: a direção do carro não mudou)
        [Tooltip("Quantas vezes o volante desenhado gira mais que as rodas (só o visual).")]
        public float relacaoDoVolante = 7f;
        [Tooltip("O máximo que o volante desenhado gira por segundo (graus; só o visual).")]
        public float giroMaximoDoVolanteVisto = 320f;
        [Tooltip("A chave deste veículo (o item que dá a partida direta; sem ela, só a ligação direta).")]
        public Loot.Item chaveDoVeiculo = Loot.Item.ChaveDaPerua;

        /// <summary>Quem está em cada lugar: o id do cliente + 1 (0 = vazio).</summary>
        public struct Ocupantes : INetworkSerializeByMemcpy
        {
            public ulong a, b, c, d;

            public ulong this[int i]
            {
                get => i == 0 ? a : i == 1 ? b : i == 2 ? c : d;
                set { if (i == 0) a = value; else if (i == 1) b = value; else if (i == 2) c = value; else d = value; }
            }
        }

        readonly NetworkVariable<Ocupantes> _ocupantes = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _farois = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // a gosma do Long nos faróis (01/11/2026): até esta hora (no relógio do servidor), eles não acendem
        readonly NetworkVariable<float> _faroisSujosAte = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O giro das rodas da frente (-127 a 127 = todo para a esquerda ou a direita), para os outros verem.</summary>
        readonly NetworkVariable<sbyte> _direcao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>A rotação do motor (0 a 255), para o som.</summary>
        readonly NetworkVariable<byte> _rotacao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> _buzina = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>A chave e o motor (<see cref="EstadoDoMotor"/>), escritos pelo dono (o motorista).</summary>
        readonly NetworkVariable<byte> _motorRede = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Ligado sem a chave (a ligação direta): a chave não aparece na ignição. Escrito pelo dono (o motorista).</summary>
        readonly NetworkVariable<bool> _direta = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>As peças do motor (a vela, a carga da bateria e o radiador), escritas pelo anfitrião.</summary>
        readonly NetworkVariable<PecasDoMotor> _pecas = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>A temperatura do motor (0 a 1,25, vezes 200) e se ele ferveu (até esfriar), do anfitrião.</summary>
        readonly NetworkVariable<byte> _calor = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _fervendo = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Alguém está com a tela do capô aberta: o capô fica levantado.</summary>
        readonly NetworkVariable<bool> _capoAberto = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O porta-malas: a tampa aberta, o macaco lá dentro e o estado do estepe (255, sem ele), do anfitrião.</summary>
        readonly NetworkVariable<bool> _malasAberto = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _macaco = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _estepe = new(PneusDoCarro.Vazio, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O macaco: a roda ao lado da qual ele está apoiado (255, sem ele) e se o carro está erguido, do anfitrião.</summary>
        readonly NetworkVariable<byte> _macacoEm = new(PneusDoCarro.Vazio, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _erguido = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Os 4 pneus (o estado, ou sem a roda), escritos pelo anfitrião.</summary>
        readonly NetworkVariable<PneusDoCarro> _pneus = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// A chave e o motor: sem a chave (ninguém no volante), a chave na ignição desligada, a ignição ligada (o painel
        /// acende), o motor de arranque girando (a chave presa na partida, durante o mini-game do motorista) e o motor
        /// ligado. Só anda com o motor ligado.
        /// </summary>
        public enum EstadoDoMotor : byte { SemChave, Desligado, Ignicao, Arranque, Ligado }

        /// <summary>
        /// As posições da chave (graus no sentido do relógio): desligada em 0, a ignição ligada em <see cref="ChaveLiga"/>
        /// e a partida em <see cref="ChavePartida"/> (da ignição para a partida ela pesa e volta sozinha; chegando, fica
        /// presa até o motor pegar ou o motorista desistir). Abaixo de <see cref="ChaveDesliga"/> o motor desliga.
        /// </summary>
        public const float ChaveLiga = 60f, ChavePartida = 92f, ChaveDesliga = 30f;
        /// <summary>Quanto tempo (s) a chave leva para entrar na ignição.</summary>
        public const float ChaveEntrando = 0.3f;

        /// <summary>O ângulo do ponteiro do velocímetro (graus, no sentido do relógio a partir de cima): 0 a 160 km/h.</summary>
        public static float AnguloDoVelocimetro(float kmh) => -120f + Mathf.Clamp(kmh, 0f, 160f) / 160f * 240f;
        /// <summary>O ângulo do ponteiro da gasolina (graus, no sentido do relógio a partir de cima): vazio a cheio.</summary>
        public static float AnguloDoCombustivel(float nivel) => -50f + Mathf.Clamp01(nivel) * 100f;

        Rigidbody _corpo;
        // os comandos do motorista (só no dono)
        float _acelerar, _virar, _maoPuxada, _giro;
        // a velocidade medida pela posição (nos que não simulam)
        Vector3 _posAntes;
        Vector3 _velocidadeMedida;
        float _giroDasRodas, _volanteVisto;
        readonly float[] _mudouEm = { -99f, -99f, -99f, -99f };
        readonly Dictionary<int, float> _atropelados = new Dictionary<int, float>();
        static readonly Collider[] _encostados = new Collider[32];
        Material[] _matsApagados, _matsAcesos;
        bool _faroisAplicados, _buzinaLocal, _painelAceso, _tetoAceso, _anelAceso = true;
        float _volumeDoMotor, _rotacaoLocal, _enviouRotacao, _viradoDesde = -1f;
        // a chave e o motor: o estado e o ângulo da chave no dono; o que cada máquina já tocou e mostrou
        EstadoDoMotor _estadoLocal, _estadoOuvido;
        float _anguloDaChave, _anguloVisto, _chaveEntrouEm = -99f, _pegouEm = -99f, _puxando;
        bool _presaNaPartida, _arranqueGirando;
        float _brilhoPainel, _brilhoTeto, _tetoAte = -99f, _ponteiroVel, _ponteiroComb;
        Quaternion _volanteRepouso, _capoRepouso;
        // no anfitrião: a carga da bateria e a temperatura (as da rede são arredondadas) e quem está com o capô aberto
        float _carga, _temperatura;
        readonly HashSet<ulong> _olhandoOCapo = new HashSet<ulong>();
        float _capoVisto;
        // os pneus já postos nos WheelColliders (o raio e a aderência de cada roda)
        PneusDoCarro _pneusAplicados;
        bool _pneusFeitos;
        readonly float[] _aderencia = { 1f, 1f, 1f, 1f };
        Quaternion _tampaRepouso;
        float _tampaVista;
        // o macaco: no anfitrião, a pose do carro quando ele foi apoiado e quanto o carro já subiu (0 a 1); em todas, quando
        // o carro começou a subir ou a descer (para as dicas) e a altura da malha do macaco fechado
        Vector3 _poseBase;
        Quaternion _giroBase;
        float _ergueu, _erguidoMudouEm = -99f, _alturaDoMacacoFechado = 0.118f;
        bool _erguidoVisto;
        BoxCollider _miraDoMacaco;

        /// <summary>A velocidade (m/s): a da física no dono, a medida pela posição nos outros.</summary>
        public float Velocidade => Simula ? _corpo.linearVelocity.magnitude : _velocidadeMedida.magnitude;
        /// <summary>A velocidade para a frente (negativa de ré).</summary>
        public float VelocidadeParaFrente => Vector3.Dot(Simula ? _corpo.linearVelocity : _velocidadeMedida, transform.forward);
        /// <summary>De lado ou de rodas para cima.</summary>
        public bool Virado => transform.up.y < 0.35f;
        /// <summary>Virado há mais de 1,5 s e quase parado: o motorista pode desvirar.</summary>
        public bool PodeDesvirar => Virado && _viradoDesde >= 0f && Time.time - _viradoDesde > 1.5f && Velocidade < 2f;
        public bool FaroisLigados => IsSpawned && _farois.Value && !FaroisSujos;
        /// <summary>A gosma do Long cobriu os faróis (eles não acendem até ela escorrer).</summary>
        public bool FaroisSujos => IsSpawned && NetworkManager.ServerTime.TimeAsFloat < _faroisSujosAte.Value;

        /// <summary>Só no anfitrião: a gosma cobre os faróis por este tempo (s).</summary>
        public void SujarFarois(float segundos)
        {
            if (IsServer) _faroisSujosAte.Value = NetworkManager.ServerTime.TimeAsFloat + segundos;
        }
        bool Simula => !IsSpawned || IsOwner;
        /// <summary>A chave e o motor: o do dono na hora, o da rede nos outros.</summary>
        public EstadoDoMotor Estado => !IsSpawned ? EstadoDoMotor.SemChave : IsOwner ? _estadoLocal : (EstadoDoMotor)_motorRede.Value;
        /// <summary>O motor está ligado (o carro anda).</summary>
        public bool MotorLigado => Estado >= EstadoDoMotor.Ligado;
        /// <summary>O ângulo da chave (graus no sentido do relógio): o do dono, ou o da posição do estado nos outros.</summary>
        public float AnguloDaChave => IsSpawned && IsOwner ? _anguloDaChave : AnguloDe(Estado);
        /// <summary>Quando o motor pegou (Time.time; -99 se ele já estava ligado quando esta máquina viu).</summary>
        public float PegouEm => _pegouEm;
        /// <summary>As peças do motor agora.</summary>
        public PecasDoMotor Pecas => _pecas.Value;
        /// <summary>As 4 rodas agora.</summary>
        public PneusDoCarro Pneus => _pneus.Value;
        public bool PortaMalasAberto => IsSpawned && _malasAberto.Value;
        public bool TemMacaco => IsSpawned && _macaco.Value;
        /// <summary>O estado do estepe no porta-malas (%), ou -1 sem ele.</summary>
        public int Estepe => IsSpawned && _estepe.Value != PneusDoCarro.Vazio ? _estepe.Value : -1;
        /// <summary>A roda ao lado da qual o macaco está apoiado, ou -1 sem ele.</summary>
        public int MacacoNaRoda => IsSpawned && _macacoEm.Value != PneusDoCarro.Vazio ? _macacoEm.Value : -1;
        /// <summary>O macaco está levantado (o carro sobe, ou já subiu).</summary>
        public bool Erguido => IsSpawned && _erguido.Value;
        /// <summary>O carro ainda está subindo ou descendo no macaco.</summary>
        public bool MacacoMexendo => MacacoNaRoda >= 0 && Time.time - _erguidoMudouEm < tempoDoMacaco;
        /// <summary>A roda é a do macaco (o macaco levanta aquele canto: só ela sai do chão e pode ser trocada).</summary>
        public bool NaRodaDoMacaco(int roda)
        {
            int m = MacacoNaRoda;
            return m >= 0 && roda == m;
        }
        /// <summary>Ninguém sentado no carro.</summary>
        public bool Vazio
        {
            get
            {
                if (!IsSpawned) return true;
                var o = _ocupantes.Value;
                for (int i = 0; i < Lugares; i++) if (o[i] != 0) return false;
                return true;
            }
        }
        /// <summary>Onde o macaco encosta na lataria, ao lado da roda (embaixo da soleira, para o meio do carro): no espaço do carro.</summary>
        public Vector3 PontoDoMacaco(int roda)
        {
            var w = rodas[roda].transform.localPosition;
            return new Vector3(Mathf.Sign(w.x) * MacacoX, BaseDaLataria, w.z - Mathf.Sign(w.z) * 0.55f);
        }
        /// <summary>O macaco fica embaixo da soleira (x) e encosta embaixo da carroceria (y; o Y_BASE do carro.py).</summary>
        public const float MacacoX = 0.78f, BaseDaLataria = 0.22f;
        /// <summary>Acima disto (m/s) o macaco não se apoia.</summary>
        public const float VelocidadeParaOMacaco = 0.4f;
        /// <summary>Atrás do carro, na altura da tampa: os pedidos do porta-malas contam a distância daqui.</summary>
        public Vector3 PontoDoPortaMalas => transform.TransformPoint(0f, 0.9f, -2.3f);
        /// <summary>A temperatura do motor (0 frio, ~0,45 normal, 1 fervendo).</summary>
        public float Temperatura => _calor.Value / 200f;
        /// <summary>O motor ferveu e ainda não esfriou: apaga e não pega.</summary>
        public bool Fervendo => IsSpawned && _fervendo.Value;
        /// <summary>O motor de arranque está girando (a chave na partida e a bateria com força).</summary>
        public bool ArranqueGira => Estado == EstadoDoMotor.Arranque && Pecas.BateriaGira;
        /// <summary>A ignição acende o painel (a chave na ignição ou além, e a bateria viva).</summary>
        public bool IgnicaoLigada => Estado >= EstadoDoMotor.Ignicao && Pecas.BateriaAcende;

        void Awake()
        {
            _corpo = GetComponent<Rigidbody>();
            _corpo.centerOfMass = new Vector3(0f, 0.38f, 0.12f);
            if (rodas != null && rodas.Length > 0) rodas[0].ConfigureVehicleSubsteps(5f, 12, 15);
            if (volante != null) _volanteRepouso = volante.localRotation;
            if (capo != null) _capoRepouso = capo.localRotation;
            if (tampaTraseira != null) _tampaRepouso = tampaTraseira.localRotation;
            if (macacoApoiado != null)
            {
                var mf = macacoApoiado.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) _alturaDoMacacoFechado = Mathf.Max(0.02f, mf.sharedMesh.bounds.size.y);
                _miraDoMacaco = macacoApoiado.GetComponent<BoxCollider>();
            }
            if (carroceria != null)
            {
                _matsApagados = carroceria.sharedMaterials;
                _matsAcesos = (Material[])_matsApagados.Clone();
                if (submeshFarol >= 0) _matsAcesos[submeshFarol] = farolAceso;
                if (submeshLanterna >= 0) _matsAcesos[submeshLanterna] = lanternaAceso;
            }
            AplicarFarois(false);
            _posAntes = transform.position;
        }

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        public override void OnNetworkSpawn()
        {
            _ocupantes.OnValueChanged += AoMudarOcupantes;
            if (IsServer)
            {
                _pecas.Value = PecasDoMotor.Com(velaInicial, bateriaInicial, radiadorInicial);
                _carga = _pecas.Value.Estado(PecasDoMotor.Bateria);
                _temperatura = 0f;
                _olhandoOCapo.Clear();
                _capoAberto.Value = false;
                _pneus.Value = PneusDoCarro.Com(pneusIniciais);
                _malasAberto.Value = false;
                _macaco.Value = macacoInicial;
                _estepe.Value = estepeInicial < 0 ? PneusDoCarro.Vazio : (byte)Mathf.Clamp(estepeInicial, 0, 100);
                _macacoEm.Value = PneusDoCarro.Vazio;
                _erguido.Value = false;
                _ergueu = 0f;
            }
            _erguidoVisto = _erguido.Value;
            _erguidoMudouEm = -99f;
            _tampaVista = _malasAberto.Value ? 1f : 0f;
            _pneusFeitos = false;
            _capoVisto = _capoAberto.Value ? 1f : 0f;
            // quem chega com o motor já ligado não ouve a partida
            _estadoOuvido = Estado;
            _anguloVisto = AnguloDe(_estadoOuvido);
            if (_estadoOuvido != EstadoDoMotor.SemChave) _chaveEntrouEm = -99f;
        }

        // o novo dono (o motorista que sentou, ou o anfitrião quando ele sai) parte do que a rede tem
        public override void OnGainedOwnership()
        {
            _estadoLocal = (EstadoDoMotor)_motorRede.Value;
            _anguloDaChave = AnguloDe(_estadoLocal);
            _presaNaPartida = false;
        }

        public override void OnNetworkDespawn()
        {
            _ocupantes.OnValueChanged -= AoMudarOcupantes;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Passageiro != null && j.Passageiro.Carro == this) j.Passageiro.Soltar();
        }

        void AoMudarOcupantes(Ocupantes antes, Ocupantes depois)
        {
            for (int i = 0; i < Lugares; i++)
            {
                if (antes[i] == depois[i]) continue;
                _mudouEm[i] = Time.time;
                _tetoAte = Time.time + FecharEm + FecharPorta + 2.5f;   // a luz de teto acende com a porta
                if (portas != null && i < portas.Length && portas[i] != null) portas[i].Ciclo();
            }
        }

        /// <summary>O jogador (pelo id do cliente) sentado no lugar, ou null.</summary>
        public JogadorRede Ocupante(int lugar)
        {
            if (!IsSpawned) return null;
            ulong c = _ocupantes.Value[lugar];
            return c == 0 ? null : Jogador(c - 1);
        }

        public bool Ocupado(int lugar) => IsSpawned && _ocupantes.Value[lugar] != 0;

        static JogadorRede Jogador(ulong cliente)
        {
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.OwnerClientId == cliente) return j;
            return null;
        }

        // ------------------------------------------------------------------------------------------ entrar e sair

        /// <summary>O jogador local apertou E na porta do lugar.</summary>
        public void PedirEntrar(int lugar) => EntrarRpc((byte)lugar);

        /// <summary>O jogador local, sentado, apertou E.</summary>
        public void PedirSair() => SairRpc();

        [Rpc(SendTo.Server)]
        void EntrarRpc(byte lugar, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (lugar >= Mathf.Min(Lugares, lugaresUsados) || MacacoNaRoda >= 0) return;   // no macaco, ninguém entra
            var o = _ocupantes.Value;
            if (o[lugar] != 0) return;
            var jogador = Jogador(cliente);
            if (jogador == null || jogador.Passageiro == null || jogador.Passageiro.Sentado) return;
            if (jogador.Vida != null && jogador.Vida.Vida.Value <= 0f) return;
            var mochila = jogador.GetComponent<InventarioJogador>();
            if (mochila != null && mochila.CarregandoPneu) return;   // com o pneu nas mãos, não entra
            if (Velocidade > VelocidadeParaEntrar) return;
            if (Vector3.Distance(jogador.transform.position, portas[lugar].transform.position) > 3.5f) return;
            foreach (var outro in Todos)
                for (int i = 0; i < Lugares; i++)
                    if (outro.IsSpawned && outro._ocupantes.Value[i] == cliente + 1) return;
            o[lugar] = cliente + 1;
            _ocupantes.Value = o;
            if (lugar == 0 && OwnerClientId != cliente) NetworkObject.ChangeOwnership(cliente);
        }

        [Rpc(SendTo.Server)]
        void SairRpc(RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            var o = _ocupantes.Value;
            for (int i = 0; i < Lugares; i++)
            {
                if (o[i] != cliente + 1) continue;
                if (Velocidade > VelocidadeParaSair + 1f) return;   // folga para a medida atrasada
                Desocupar(i);
                return;
            }
        }

        /// <summary>No anfitrião: tira o jogador do carro na hora (caiu, saiu da partida).</summary>
        public static void TirarDeTodos(ulong cliente)
        {
            foreach (var c in Todos)
            {
                if (!c.IsSpawned || !c.IsServer) continue;
                for (int i = 0; i < Lugares; i++)
                    if (c._ocupantes.Value[i] == cliente + 1) c.Desocupar(i);
            }
        }

        void Desocupar(int lugar)
        {
            var o = _ocupantes.Value;
            o[lugar] = 0;
            _ocupantes.Value = o;
            if (lugar == 0 && OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
        }

        /// <summary>
        /// Cada quadro, cada jogador fica de acordo com os lugares: quem está num lugar senta (se acabou de entrar, com a
        /// porta e tudo; quem chegou depois na partida já o vê sentado); quem saiu, levanta. No anfitrião, um lugar de
        /// quem não está mais na partida fica vazio.
        /// </summary>
        void Acertar()
        {
            var o = _ocupantes.Value;
            for (int i = 0; i < Lugares; i++)
            {
                if (o[i] == 0) continue;
                var j = Jogador(o[i] - 1);
                if (j == null)
                {
                    if (IsServer) Desocupar(i);
                    continue;
                }
                var p = j.Passageiro;
                if (p != null && (p.Carro != this || p.Lugar != i)) p.Sentar(this, i, Time.time - _mudouEm[i] < 0.5f);
            }
            foreach (var j in JogadorRede.Todos)
            {
                var p = j != null ? j.Passageiro : null;
                if (p == null || p.Carro != this) continue;
                if (o[p.Lugar] != j.OwnerClientId + 1) p.Levantar(Time.time - _mudouEm[p.Lugar] < 0.5f);
            }
        }

        // ------------------------------------------------------------------------------------------ dirigir

        /// <summary>Os comandos do motorista (só valem no dono): acelerar (-1 ré/freio a 1), virar (-1 a 1), freio de mão.</summary>
        public void Comandar(float acelerar, float virar, bool freioDeMao)
        {
            _acelerar = Mathf.Clamp(acelerar, -1f, 1f);
            _virar = Mathf.Clamp(virar, -1f, 1f);
            _maoPuxada = freioDeMao ? 1f : 0f;
        }

        public void AlternarFarois()
        {
            if (IsSpawned && IsOwner) _farois.Value = !_farois.Value;
        }

        /// <summary>O motorista segurando (ou soltando) a buzina; quem toca é cada máquina, pela rede.</summary>
        public void Buzinar(bool apertada)
        {
            if (!IsSpawned || !IsOwner) return;
            _buzinaLocal = apertada;
            if (_buzina.Value != apertada) _buzina.Value = apertada;
        }

        // ------------------------------------------------------------------------------------------ a chave

        static float AnguloDe(EstadoDoMotor e) => e switch
        {
            EstadoDoMotor.Ignicao or EstadoDoMotor.Ligado => ChaveLiga,
            EstadoDoMotor.Arranque => ChavePartida,
            _ => 0f,
        };

        /// <summary>O motorista põe a chave na ignição (ela entra desligada).</summary>
        public void PorAChave()
        {
            if (!IsSpawned || !IsOwner || _estadoLocal != EstadoDoMotor.SemChave) return;
            _anguloDaChave = 0f;
            MudarEstado(EstadoDoMotor.Desligado);
        }

        /// <summary>
        /// O motorista, segurando a chave, gira ela (graus; positivo no sentido do relógio). Da ignição para a partida ela
        /// pesa (<see cref="pesoDaPartida"/>); chegando na partida, fica presa e só sai puxando para a esquerda. Com o
        /// motor ligado, ela não passa da ignição.
        /// </summary>
        public void GirarChave(float graus)
        {
            if (!IsSpawned || !IsOwner || _estadoLocal == EstadoDoMotor.SemChave) return;
            if (Time.time - _chaveEntrouEm < ChaveEntrando) return;   // ainda entrando
            if (_presaNaPartida)
            {
                _puxando = graus < 0f ? _puxando + graus : 0f;
                if (_puxando > -10f) return;
                _presaNaPartida = false;
                _anguloDaChave = ChavePartida - 10f;
                return;
            }
            if (graus > 0f && _anguloDaChave >= ChaveLiga) graus *= pesoDaPartida;
            float max = _estadoLocal == EstadoDoMotor.Ligado ? ChaveLiga : ChavePartida;
            _anguloDaChave = Mathf.Clamp(_anguloDaChave + graus, 0f, max);
            if (_anguloDaChave >= ChavePartida - 0.5f)
            {
                _anguloDaChave = ChavePartida;
                _presaNaPartida = true;
                _puxando = 0f;
            }
        }

        /// <summary>O motorista soltou a chave (ou desistiu da partida): ela cai na posição mais perto (desligada ou a ignição).</summary>
        public void SoltarChave()
        {
            if (!IsSpawned || !IsOwner) return;
            _presaNaPartida = false;
            _anguloDaChave = _anguloDaChave < ChaveDesliga ? 0f : ChaveLiga;
        }

        /// <summary>
        /// O mini-game da partida deu certo: o motor pega na hora, e a chave volta para a ignição. Com a vela ruim (ou
        /// sem ela), ou com o motor fervendo, ele só tosse: devolve false e o motivo.
        /// </summary>
        public bool PegarAgora(out string motivo)
        {
            motivo = null;
            if (!IsSpawned || !IsOwner || _estadoLocal != EstadoDoMotor.Arranque) return false;
            var p = Pecas;
            if (!p.VelaBoa || Fervendo)
            {
                Tossir();
                motivo = Fervendo ? "O motor está quente demais: espere esfriar"
                    : p.Tem(PecasDoMotor.Vela) ? "O motor não pega: a vela está queimada (abra o capô)" : "O motor não pega: falta a vela (abra o capô)";
                return false;
            }
            _presaNaPartida = false;
            _anguloDaChave = ChaveLiga;
            MudarEstado(EstadoDoMotor.Ligado);
            return true;
        }

        /// <summary>Um acerto no mini-game da partida: o motor tosse (quase pega). Só nesta máquina.</summary>
        public void Tossir()
        {
            Efeitos.Som(SomDoCarro.Tossida, motor != null ? motor.transform.position : transform.position, 0.6f, true, 30f);
        }

        /// <summary>Ligado (ou tentando ligar) pela ligação direta, sem a chave: a chave não aparece na ignição.</summary>
        public bool LigacaoDireta => IsSpawned && _direta.Value && Estado != EstadoDoMotor.SemChave;

        /// <summary>
        /// A ligação direta (a pedido em 29/10/2026: sem a chave do veículo, "esse mini-game como se fosse um ligar direto,
        /// pra roubar o carro"): os fios juntos, o painel acende e o motor de arranque gira na hora, como a chave presa na
        /// partida; o mini-game do motorista decide se pega (<see cref="PegarAgora"/>). Com os fios juntos e o motor
        /// parado (a ignição), tenta de novo. Puxar para a esquerda desiste, como com a chave.
        /// </summary>
        public void FazerLigacaoDireta()
        {
            if (!IsSpawned || !IsOwner || _estadoLocal == EstadoDoMotor.Ligado) return;
            if (_estadoLocal != EstadoDoMotor.SemChave && !_direta.Value) return;   // com a chave posta, é pela chave
            _direta.Value = true;
            _anguloDaChave = ChavePartida;
            _presaNaPartida = true;
            _puxando = 0f;
            MudarEstado(EstadoDoMotor.Arranque);
        }

        /// <summary>Desfaz a ligação direta (separa os fios): o motor apaga, e não fica chave nenhuma.</summary>
        public void DesfazerLigacaoDireta()
        {
            if (!IsSpawned || !IsOwner || !_direta.Value) return;
            _anguloDaChave = 0f;
            _presaNaPartida = false;
            _direta.Value = false;
            MudarEstado(EstadoDoMotor.SemChave);
        }

        void MudarEstado(EstadoDoMotor e)
        {
            _estadoLocal = e;
            if (_motorRede.Value != (byte)e) _motorRede.Value = (byte)e;
        }

        /// <summary>
        /// No dono: o estado vem da chave. Presa na partida, o motor de arranque gira (até o mini-game do motorista dar
        /// certo, <see cref="PegarAgora"/>); entre a ignição e a partida, a mola puxa a chave de volta. Abaixo de
        /// <see cref="ChaveDesliga"/>, o motor desliga. Sem motorista, a chave sai.
        /// </summary>
        void Ignicao(float dt)
        {
            if (!IsOwner) return;
            var e = _estadoLocal;
            if (_ocupantes.Value[0] == 0)
            {
                _anguloDaChave = 0f;
                _presaNaPartida = false;
                if (_direta.Value) _direta.Value = false;
                if (e != EstadoDoMotor.SemChave) MudarEstado(EstadoDoMotor.SemChave);
                return;
            }
            if (e == EstadoDoMotor.SemChave) return;
            if (!_presaNaPartida && _anguloDaChave > ChaveLiga)
                _anguloDaChave = Mathf.MoveTowards(_anguloDaChave, ChaveLiga, molaDaPartida * dt);
            EstadoDoMotor novo;
            if (_anguloDaChave < ChaveDesliga) novo = EstadoDoMotor.Desligado;
            // ferveu (ou tiraram a vela): o motor apaga, e a chave fica na ignição
            else if (e == EstadoDoMotor.Ligado) novo = Fervendo || !Pecas.VelaBoa ? EstadoDoMotor.Ignicao : EstadoDoMotor.Ligado;
            else if (_presaNaPartida) novo = EstadoDoMotor.Arranque;
            else novo = EstadoDoMotor.Ignicao;
            // na ligação direta não há "desligado com a chave": puxada até o fim, os fios se soltam
            if (novo == EstadoDoMotor.Desligado && _direta.Value)
            {
                _direta.Value = false;
                novo = EstadoDoMotor.SemChave;
            }
            if (novo != e) MudarEstado(novo);
        }

        /// <summary>
        /// Em cada máquina: os sons das mudanças da chave e do motor (a chave entrando e estalando, o solenoide, o arranque
        /// em laço, o pinhão soltando, o motor pegando) e a chave na ignição (entrando, girando, escondida sem motorista).
        /// </summary>
        void Chave(float dt)
        {
            var e = Estado;
            var antes = _estadoOuvido;
            if (e != antes)
            {
                _estadoOuvido = e;
                Vector3 naChave = ignicao != null ? ignicao.position : transform.position;
                Vector3 noMotor = motor != null ? motor.transform.position : transform.position;
                // o arranque só gira com a bateria forte; fraca, o solenoide estala sozinho; morta, nem isso
                bool forca = Pecas.BateriaGira, viva = Pecas.BateriaAcende;
                bool girava = antes == EstadoDoMotor.Arranque && _arranqueGirando;
                bool gira = e == EstadoDoMotor.Arranque && forca;
                if (antes == EstadoDoMotor.SemChave)
                {
                    _chaveEntrouEm = Time.time;
                    if (!_direta.Value) Efeitos.Som(SomDoCarro.ChaveEntra, naChave, 0.55f, true, 6f);   // na direta, não há chave
                }
                else if (e != EstadoDoMotor.SemChave)
                    Efeitos.Som(SomDoCarro.CliqueDaChave, naChave, 0.5f, true, 6f);
                if (e == EstadoDoMotor.Arranque && antes != EstadoDoMotor.Arranque && viva) Efeitos.Som(SomDoCarro.Solenoide, noMotor, 0.6f, true, 30f);
                if (girava && !gira) Efeitos.Som(SomDoCarro.SoltarArranque, noMotor, 0.6f, true, 30f);
                _arranqueGirando = gira;
                if (antes == EstadoDoMotor.Arranque && e >= EstadoDoMotor.Ligado)
                {
                    _pegouEm = Time.time;
                    Efeitos.Som(SomDoCarro.Pegar, noMotor, 0.6f, true, 35f);
                }
            }

            // a bateria acabou no meio da partida: o arranque para
            if (_arranqueGirando && !ArranqueGira)
            {
                _arranqueGirando = false;
                Efeitos.Som(SomDoCarro.SoltarArranque, motor != null ? motor.transform.position : transform.position, 0.6f, true, 30f);
            }

            // o arranque em laço, enquanto a chave fica na partida (mais lento com a bateria fraca)
            if (arranque != null)
            {
                if (arranque.clip == null) arranque.clip = SomDoCarro.Arranque;
                bool toca = ArranqueGira;
                arranque.pitch = Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(PecasDoMotor.BateriaArranque, 40f, Pecas.Estado(PecasDoMotor.Bateria)));
                arranque.volume = toca ? 0.6f : 0f;
                if (toca && !arranque.isPlaying) arranque.Play();
                else if (!toca && arranque.isPlaying) arranque.Stop();
            }

            // a chave: some sem motorista, entra pelo miolo e gira (nos outros, devagar até a posição do estado)
            if (chave == null) return;
            bool comChave = e != EstadoDoMotor.SemChave && !_direta.Value;   // na ligação direta, sem chave na ignição
            if (pecasDaChave != null)
                foreach (var p in pecasDaChave)
                    if (p != null && p.enabled != comChave) p.enabled = comChave;
            float alvo = AnguloDaChave;
            _anguloVisto = IsOwner ? alvo : Mathf.MoveTowards(_anguloVisto, alvo, dt * 500f);
            float entra = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _chaveEntrouEm) / ChaveEntrando));
            chave.localPosition = new Vector3(0f, 0f, -0.035f * (1f - entra));
            chave.localRotation = Quaternion.Euler(0f, 0f, -_anguloVisto);

            bool anel = _ocupantes.Value[0] != 0 && e < EstadoDoMotor.Ligado;
            if (anel != _anelAceso && anelDaIgnicao != null)
            {
                _anelAceso = anel;
                anelDaIgnicao.sharedMaterial = anel ? anelAceso : anelApagado;
            }
        }

        /// <summary>Põe o carro de pé de novo (o motorista, com ele virado e parado).</summary>
        public void Desvirar()
        {
            if (!Simula || !PodeDesvirar) return;
            float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).sqrMagnitude > 0.01f
                ? Vector3.ProjectOnPlane(transform.forward, Vector3.up) : Vector3.forward).eulerAngles.y;
            _corpo.linearVelocity = Vector3.zero;
            _corpo.angularVelocity = Vector3.zero;
            _corpo.position = transform.position + Vector3.up * 1.2f;
            _corpo.rotation = Quaternion.Euler(0f, yaw, 0f);
            _viradoDesde = -1f;
        }

        void FixedUpdate()
        {
            if (IsSpawned && IsServer && IsOwner && MacacoNaRoda >= 0 && rodas != null && rodas.Length >= 4)
            {
                NoMacaco(Time.fixedDeltaTime);
                return;
            }
            if (!Simula || rodas == null || rodas.Length < 4) return;
            bool comMotorista = IsSpawned && _ocupantes.Value[0] != 0;
            float acelerar = comMotorista ? _acelerar : 0f, virar = comMotorista ? _virar : 0f, mao = comMotorista ? _maoPuxada : 1f;
            float v = Vector3.Dot(_corpo.linearVelocity, transform.forward);
            float dt = Time.fixedDeltaTime;

            // direção: vira menos quanto mais rápido
            float max = Mathf.Lerp(giroMaximo, giroNaVelocidadeMaxima, Mathf.InverseLerp(0f, velocidadeMaxima, Mathf.Abs(v)));
            float alvo = virar * max;
            // soltando o volante, ele volta mais rápido
            float rapidez = Mathf.Abs(virar) < 0.05f ? velocidadeDoVolante * 1.6f : velocidadeDoVolante;
            _giro = Mathf.MoveTowards(_giro, alvo, rapidez * dt);
            rodas[0].steerAngle = rodas[1].steerAngle = _giro;

            // acelerador, freio e ré (tração na frente); com o motor desligado, o acelerador não faz nada e não há ré (o
            // freio funciona, e o carro rola devagar até parar)
            bool ligado = MotorLigado;
            float forca = 0f, frear = 0f;
            // a vela gasta tira força do motor
            float torque = this.torque * Pecas.FatorDeForca;
            if (acelerar > 0.05f)
            {
                if (v < -0.8f) frear = freio * acelerar;
                else if (ligado) forca = torque * acelerar * Mathf.Clamp01(1f - v / velocidadeMaxima);
                else frear = Mathf.Abs(v) < 0.4f ? 600f : 90f;
            }
            else if (acelerar < -0.05f)
            {
                if (v > 0.8f || !ligado) frear = freio * -acelerar;
                else forca = -torque * 0.6f * -acelerar * Mathf.Clamp01(1f + v / velocidadeDeRe);
            }
            else if (ligado) frear = Mathf.Abs(v) < 0.4f ? 400f : 70f;   // o freio-motor; quase parado, segura na ladeira
            else frear = Mathf.Abs(v) < 0.4f ? 600f : 90f;   // desligado e engatado: segura parado
            for (int i = 0; i < 4; i++)
            {
                rodas[i].motorTorque = i < 2 ? forca : 0f;
                rodas[i].brakeTorque = frear + (i >= 2 ? mao * freioDeMao : 0f);
            }
            // freio de mão: a traseira escorrega. O pneu gasto ou furado segura menos (de lado e para a frente)
            for (int i = 0; i < 4; i++)
            {
                var lado = rodas[i].sidewaysFriction;
                float rigidez = (i >= 2 && mao > 0f && Mathf.Abs(v) > 3f ? 0.9f : 2f) * _aderencia[i];
                if (!Mathf.Approximately(lado.stiffness, rigidez))
                {
                    lado.stiffness = rigidez;
                    rodas[i].sidewaysFriction = lado;
                }
            }
            Arrastar();

            Barra(rodas[0], rodas[1]);
            Barra(rodas[2], rodas[3]);
            _corpo.AddForce(-transform.up * Mathf.Abs(v) * 25f);   // um pouco de pressão para baixo, rápido

            // a rede: o giro das rodas e a rotação do motor (para o som dos outros)
            if (IsSpawned)
            {
                var direcao = (sbyte)Mathf.RoundToInt(Mathf.Clamp(_giro / giroMaximo, -1f, 1f) * 127f);
                if (direcao != _direcao.Value) _direcao.Value = direcao;
                _rotacaoLocal = ligado ? Rotacao(Mathf.Abs(v), Mathf.Abs(acelerar)) : 0f;
                byte rot = (byte)Mathf.RoundToInt(_rotacaoLocal * 255f);
                if (Mathf.Abs(rot - _rotacao.Value) > 4 && Time.time - _enviouRotacao > 0.1f)
                {
                    _rotacao.Value = rot;
                    _enviouRotacao = Time.time;
                }
            }

            if (comMotorista) Atropelar(v);
        }

        // o pneu furado e o cubo sem a roda arrastam no chão: uma força contra o movimento daquele canto (o carro puxa
        // para o lado dele e perde velocidade)
        void Arrastar()
        {
            var p = Pneus;
            for (int i = 0; i < 4; i++)
            {
                float forca = p.Arrasto(i);
                if (forca <= 0f || !rodas[i].GetGroundHit(out var chao)) continue;
                var vp = Vector3.ProjectOnPlane(_corpo.GetPointVelocity(chao.point), chao.normal);
                float rapidez = vp.magnitude;
                if (rapidez < 0.05f) continue;
                _corpo.AddForceAtPosition(-vp / rapidez * forca * Mathf.Clamp01(rapidez), chao.point);
            }
        }

        // ------------------------------------------------------------------------------------------ o porta-malas

        public void PedirPortaMalas(bool abrir)
        {
            if (IsSpawned) PortaMalasRpc(abrir);
        }

        /// <summary>Pega o macaco (<see cref="PortaMalasDoCarro.Macaco"/>) ou o estepe do porta-malas para a mochila.</summary>
        public void PedirDoPortaMalas(int qual)
        {
            if (IsSpawned) PegarDoPortaMalasRpc((byte)qual);
        }

        /// <summary>Guarda no porta-malas o macaco ou um pneu da mochila (pelo Id), no lugar vazio dele.</summary>
        public void PedirGuardar(int qual, ushort idNaMochila)
        {
            if (IsSpawned) GuardarNoPortaMalasRpc((byte)qual, idNaMochila);
        }

        bool PertoDoPortaMalas(ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            var j = Jogador(cliente);
            if (j == null || Virado || (j.transform.position - PontoDoPortaMalas).sqrMagnitude > 3f * 3f) return false;
            inventario = j.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        [Rpc(SendTo.Server)]
        void PortaMalasRpc(bool abrir, RpcParams parametros = default)
        {
            if (!PertoDoPortaMalas(parametros.Receive.SenderClientId, out _) || (abrir && Velocidade > 1.5f)) return;
            _malasAberto.Value = abrir;
        }

        [Rpc(SendTo.Server)]
        void PegarDoPortaMalasRpc(byte qual, RpcParams parametros = default)
        {
            if (!_malasAberto.Value || !PertoDoPortaMalas(parametros.Receive.SenderClientId, out var inventario)) return;
            if (qual == PortaMalasDoCarro.Macaco)
            {
                if (_macaco.Value && inventario.Receber(Loot.Item.Macaco, 1)) _macaco.Value = false;
            }
            else if (_estepe.Value != PneusDoCarro.Vazio && inventario.Receber(Loot.Item.Pneu, _estepe.Value))
                _estepe.Value = PneusDoCarro.Vazio;
        }

        [Rpc(SendTo.Server)]
        void GuardarNoPortaMalasRpc(byte qual, ushort id, RpcParams parametros = default)
        {
            if (!_malasAberto.Value || !PertoDoPortaMalas(parametros.Receive.SenderClientId, out var inventario)) return;
            // o Id 0: o pneu das mãos
            if (id == 0)
            {
                if (qual == PortaMalasDoCarro.Estepe && _estepe.Value == PneusDoCarro.Vazio && inventario.TirarPneuDasMaos(out int estado))
                    _estepe.Value = (byte)Mathf.Clamp(estado, 0, 100);
                return;
            }
            if (!inventario.Achar(id, out var it)) return;
            if (qual == PortaMalasDoCarro.Macaco && !_macaco.Value && it.item == Loot.Item.Macaco)
            {
                inventario.Tirar(id);
                _macaco.Value = true;
            }
            else if (qual == PortaMalasDoCarro.Estepe && _estepe.Value == PneusDoCarro.Vazio && it.item == Loot.Item.Pneu)
            {
                inventario.Tirar(id);
                _estepe.Value = (byte)Mathf.Clamp(it.qtd, 0, 100);
            }
        }

        // a tampa sobe e desce (a mesma conta do capô, girando para o outro lado), e o que está lá dentro aparece
        void PortaMalas(float dt)
        {
            CofreDoMotor.AnimarCapo(tampaTraseira, _tampaRepouso, -aberturaDaTampa, ref _tampaVista, _malasAberto.Value, dt);
            if (macacoNoPortaMalas != null && macacoNoPortaMalas.enabled != _macaco.Value) macacoNoPortaMalas.enabled = _macaco.Value;
            bool estepe = _estepe.Value != PneusDoCarro.Vazio;
            if (estepeNoPortaMalas != null && estepeNoPortaMalas.enabled != estepe) estepeNoPortaMalas.enabled = estepe;
        }

        // ------------------------------------------------------------------------------------------ o macaco

        /// <summary>Apoia o macaco da mochila ao lado da roda (o carro vazio e parado).</summary>
        public void PedirApoiarMacaco(int roda)
        {
            if (IsSpawned) ApoiarMacacoRpc((byte)roda);
        }

        /// <summary>Levanta (ou baixa) o lado do macaco.</summary>
        public void PedirErguer(bool erguer)
        {
            if (IsSpawned) ErguerRpc(erguer);
        }

        /// <summary>O macaco volta para a mochila (o carro já baixado).</summary>
        public void PedirTirarMacaco()
        {
            if (IsSpawned) TirarMacacoRpc();
        }

        /// <summary>A roda do lado erguido vai para a mochila, como um pneu com o estado dela.</summary>
        public void PedirTirarRoda(int roda)
        {
            if (IsSpawned) TirarRodaRpc((byte)roda);
        }

        /// <summary>Um pneu da mochila (pelo Id) vai para a roda vazia do lado erguido.</summary>
        public void PedirPorRoda(int roda, ushort idNaMochila)
        {
            if (IsSpawned) PorRodaRpc((byte)roda, idNaMochila);
        }

        bool PertoDaRoda(ulong cliente, int roda, out InventarioJogador inventario)
        {
            inventario = null;
            if (roda < 0 || roda >= PneusDoCarro.Quantas || rodas == null || roda >= rodas.Length) return false;
            var j = Jogador(cliente);
            if (j == null || (j.Passageiro != null && j.Passageiro.Sentado)) return false;
            if ((j.transform.position - rodas[roda].transform.position).sqrMagnitude > 3f * 3f) return false;
            inventario = j.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        [Rpc(SendTo.Server)]
        void ApoiarMacacoRpc(byte roda, RpcParams parametros = default)
        {
            if (!IsOwner || MacacoNaRoda >= 0 || !Vazio || Virado || Velocidade > VelocidadeParaOMacaco) return;
            if (!PertoDaRoda(parametros.Receive.SenderClientId, roda, out var inventario)) return;
            if (!PortaMalasDoCarro.Achar(inventario, Loot.Item.Macaco, out var macaco)) return;
            inventario.Tirar(macaco.id);
            _corpo.linearVelocity = Vector3.zero;
            _corpo.angularVelocity = Vector3.zero;
            _poseBase = _corpo.position;
            _giroBase = _corpo.rotation;
            _ergueu = 0f;
            _corpo.isKinematic = true;
            _erguido.Value = false;
            _macacoEm.Value = roda;
            SomDoMacacoRpc(0, transform.TransformPoint(PontoDoMacaco(roda)));
        }

        [Rpc(SendTo.Server)]
        void ErguerRpc(bool erguer, RpcParams parametros = default)
        {
            int m = MacacoNaRoda;
            if (m < 0 || _erguido.Value == erguer || !PertoDaRoda(parametros.Receive.SenderClientId, m, out _)) return;
            _erguido.Value = erguer;
            SomDoMacacoRpc(1, transform.TransformPoint(PontoDoMacaco(m)));
        }

        [Rpc(SendTo.Server)]
        void TirarMacacoRpc(RpcParams parametros = default)
        {
            int m = MacacoNaRoda;
            if (m < 0 || _erguido.Value || _ergueu > 0.001f || !PertoDaRoda(parametros.Receive.SenderClientId, m, out var inventario)) return;
            if (!inventario.Receber(Loot.Item.Macaco, 1)) NoChao(Loot.Item.Macaco, m, 1);
            SomDoMacacoRpc(0, transform.TransformPoint(PontoDoMacaco(m)));
            _macacoEm.Value = PneusDoCarro.Vazio;
            _ergueu = 0f;
            _corpo.isKinematic = false;
            _corpo.WakeUp();
        }

        [Rpc(SendTo.Server)]
        void TirarRodaRpc(byte roda, RpcParams parametros = default)
        {
            if (!_erguido.Value || _ergueu < 0.98f || !NaRodaDoMacaco(roda) || !PertoDaRoda(parametros.Receive.SenderClientId, roda, out var inventario)) return;
            var p = _pneus.Value;
            if (!p.Tem(roda)) return;
            int estado = p.Estado(roda);
            // sem espaço na mochila, a roda fica no chão, ao lado
            if (!inventario.Receber(Loot.Item.Pneu, estado)) NoChao(Loot.Item.Pneu, roda, estado);
            p[roda] = PneusDoCarro.Vazio;
            _pneus.Value = p;
            SomDoMacacoRpc(2, rodas[roda].transform.position);
        }

        [Rpc(SendTo.Server)]
        void PorRodaRpc(byte roda, ushort id, RpcParams parametros = default)
        {
            if (!_erguido.Value || _ergueu < 0.98f || !NaRodaDoMacaco(roda) || !PertoDaRoda(parametros.Receive.SenderClientId, roda, out var inventario)) return;
            var p = _pneus.Value;
            if (p.Tem(roda)) return;
            int estado;
            // o Id 0: o pneu das mãos
            if (id == 0)
            {
                if (!inventario.TirarPneuDasMaos(out estado)) return;
            }
            else
            {
                if (!inventario.Achar(id, out var it) || it.item != Loot.Item.Pneu) return;
                inventario.Tirar(id);
                estado = it.qtd;
            }
            p[roda] = (byte)Mathf.Clamp(estado, 0, 100);
            _pneus.Value = p;
            SomDoMacacoRpc(2, rodas[roda].transform.position);
        }

        // no anfitrião: um item no chão, para fora da roda (o que não coube na mochila)
        void NoChao(Loot.Item item, int roda, int qtd)
        {
            var mundo = Loot.ItensNoMundo.Instancia;
            if (mundo == null) return;
            var w = rodas[roda].transform;
            Vector3 ponto = w.position + transform.right * Mathf.Sign(w.localPosition.x) * 0.8f;
            if (Physics.Raycast(ponto + Vector3.up, Vector3.down, out var chao, 3f, 1, QueryTriggerInteraction.Ignore)) ponto = chao.point;
            else ponto.y = transform.position.y;
            mundo.Criar(item, ponto, transform.eulerAngles.y, qtd);
        }

        // 0: o macaco no chão (apoiar e tirar); 1: a manivela girando (subir e descer); 2: a roda saindo ou entrando
        [Rpc(SendTo.Everyone)]
        void SomDoMacacoRpc(byte qual, Vector3 ponto)
        {
            if (qual == 1) Efeitos.Som(SomDoCarro.Macaco, ponto, 0.75f, true, 18f);
            else Efeitos.Som(Efeitos.Largar, ponto, qual == 2 ? 1f : 0.8f, true, 15f);
        }

        // no anfitrião (o dono do carro vazio): o carro fica cinemático e gira em volta do canto da diagonal (o do outro lado e
        // do outro eixo), até o canto do macaco subir alturaDoMacaco. Os dois cantos vizinhos sobem a metade, e as rodas
        // deles continuam no chão (a suspensão estica, em Rodas); a do macaco sai do chão.
        void NoMacaco(float dt)
        {
            if (!_corpo.isKinematic) _corpo.isKinematic = true;
            _ergueu = Mathf.MoveTowards(_ergueu, _erguido.Value ? 1f : 0f, dt / Mathf.Max(0.1f, tempoDoMacaco));
            int m = MacacoNaRoda;
            // o retângulo das rodas (no espaço do carro): o meio, a bitola e o entre-eixos
            Vector3 meioDasRodas = Vector3.zero;
            for (int i = 0; i < rodas.Length; i++) meioDasRodas += rodas[i].transform.localPosition;
            meioDasRodas /= Mathf.Max(1, rodas.Length);
            meioDasRodas.y = 0f;
            float bitola = Mathf.Max(0.5f, Mathf.Abs(rodas[1].transform.localPosition.x - rodas[0].transform.localPosition.x));
            float entre = Mathf.Max(0.5f, Mathf.Abs(rodas[0].transform.localPosition.z - rodas[2].transform.localPosition.z));
            // o canto do macaco sobe h, os vizinhos h/2, a diagonal nada: um plano, que sobe h/2 no meio e inclina
            float h = alturaDoMacaco * _ergueu;
            var cm = rodas[m].transform.localPosition - meioDasRodas;
            float lado = Mathf.Sign(cm.x) * h / 2f / bitola;       // a direita mais alta
            float frente = Mathf.Sign(cm.z) * h / 2f / entre;      // a frente mais alta
            var giro = _giroBase * Quaternion.Euler(-Mathf.Atan(frente) * Mathf.Rad2Deg, 0f, Mathf.Atan(lado) * Mathf.Rad2Deg);
            var pivo = meioDasRodas;
            _corpo.MovePosition(_poseBase + _giroBase * (pivo + Vector3.up * (h / 2f)) - giro * pivo);
            _corpo.MoveRotation(giro);
        }

        // em todas as máquinas: o macaco apoiado aparece, esticado do chão até a lataria (a malha cresce na altura)
        void MacacoApoiado()
        {
            if (_erguidoVisto != _erguido.Value)
            {
                _erguidoVisto = _erguido.Value;
                _erguidoMudouEm = Time.time;
            }
            if (macacoApoiado == null) return;
            int roda = MacacoNaRoda;
            bool ver = roda >= 0 && rodas != null && roda < rodas.Length;
            if (macacoApoiado.enabled != ver) macacoApoiado.enabled = ver;
            if (_miraDoMacaco != null && _miraDoMacaco.enabled != ver) _miraDoMacaco.enabled = ver;
            if (!ver) return;
            Vector3 topo = transform.TransformPoint(PontoDoMacaco(roda));
            Vector3 chao = topo - Vector3.up * BaseDaLataria;
            float altura = BaseDaLataria;
            if (Physics.Raycast(topo + Vector3.up * 0.05f, Vector3.down, out var hit, 1.5f, 1, QueryTriggerInteraction.Ignore))
            {
                chao = hit.point;
                altura = Mathf.Max(0.04f, topo.y - hit.point.y);
            }
            var frente = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
            // a malha é comprida em X: fica ao longo do carro
            var t = macacoApoiado.transform;
            t.SetPositionAndRotation(chao, Quaternion.LookRotation(frente.normalized) * Quaternion.Euler(0f, -90f, 0f));
            t.localScale = new Vector3(1f, altura / _alturaDoMacacoFechado, 1f);
            // o gatilho do macaco (MacacoApoiado) passa para fora da lateral, onde a mira chega: o z da malha é o lado
            // esquerdo do carro
            if (_miraDoMacaco != null)
            {
                float lado = Mathf.Sign(rodas[roda].transform.localPosition.x);
                _miraDoMacaco.center = new Vector3(0f, _alturaDoMacacoFechado * 0.5f, -lado * 0.09f);
                _miraDoMacaco.size = new Vector3(0.44f, _alturaDoMacacoFechado * 1.1f, 0.34f);
            }
        }

        // ------------------------------------------------------------------------------------------ o tiro no pneu

        /// <summary>
        /// A roda mais perto que um raio (o tiro) cruza, entre todos os carros: a distância (ou infinito), o carro, a roda
        /// e a normal. A roda é um cilindro (o modelo dela, que gira e vira), sem colisor: um colisor preso ao carro
        /// entraria na física dele.
        /// </summary>
        public static float TiroNasRodas(Vector3 origem, Vector3 direcao, float alcance, out Carro carro, out int roda, out Vector3 normal)
        {
            float melhor = float.PositiveInfinity;
            carro = null;
            roda = -1;
            normal = -direcao;
            foreach (var c in Todos)
            {
                if (c == null || !c.IsSpawned || c.rodasVisuais == null) continue;
                // o raio passa longe do carro (mais de 3 m do meio dele): nem olha as rodas
                Vector3 ate = c.transform.position - origem;
                float ao = Vector3.Dot(ate, direcao);
                if (ao < -3f || ao > alcance + 3f || (ate - ao * direcao).sqrMagnitude > 9f) continue;
                var p = c.Pneus;
                for (int i = 0; i < c.rodasVisuais.Length && i < PneusDoCarro.Quantas; i++)
                {
                    var v = c.rodasVisuais[i];
                    if (v == null || !p.Tem(i)) continue;
                    if (Cilindro(origem, direcao, v.position, v.right, PneusDoCarro.RaioDoModelo, PneusDoCarro.MeiaLargura, out float t, out var n)
                        && t < melhor && t <= alcance)
                    {
                        melhor = t;
                        carro = c;
                        roda = i;
                        normal = n;
                    }
                }
            }
            return melhor;
        }

        // um raio contra um cilindro fechado (o centro, o eixo, o raio e a metade da altura): a primeira entrada
        static bool Cilindro(Vector3 o, Vector3 d, Vector3 centro, Vector3 eixo, float raio, float meia, out float t, out Vector3 normal)
        {
            t = float.PositiveInfinity;
            normal = Vector3.zero;
            Vector3 rel = o - centro;
            float oa = Vector3.Dot(rel, eixo), da = Vector3.Dot(d, eixo);
            Vector3 op = rel - oa * eixo, dp = d - da * eixo;
            // a banda de rodagem
            float a = dp.sqrMagnitude, b = 2f * Vector3.Dot(op, dp), c = op.sqrMagnitude - raio * raio;
            if (a > 1e-8f)
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float t1 = (-b - Mathf.Sqrt(disc)) / (2f * a);
                    if (t1 >= 0f && Mathf.Abs(oa + t1 * da) <= meia)
                    {
                        t = t1;
                        normal = (op + t1 * dp).normalized;
                    }
                }
            }
            // as duas faces (a calota e o lado de dentro)
            if (Mathf.Abs(da) > 1e-6f)
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float tf = (lado * meia - oa) / da;
                    if (tf < 0f || tf >= t) continue;
                    if ((op + tf * dp).sqrMagnitude > raio * raio) continue;
                    t = tf;
                    normal = eixo * lado;
                }
            return !float.IsPositiveInfinity(t);
        }

        /// <summary>Um tiro pegou esta roda (quem atirou manda; o anfitrião fura, e todos ouvem o estouro e o ar).</summary>
        public void Furar(int roda)
        {
            if (IsSpawned && roda >= 0 && roda < PneusDoCarro.Quantas) FurarRpc((byte)roda);
        }

        [Rpc(SendTo.Server)]
        void FurarRpc(byte roda, RpcParams parametros = default)
        {
            var p = _pneus.Value;
            if (roda >= PneusDoCarro.Quantas || !p.Tem(roda) || p.Furado(roda) || Jogador(parametros.Receive.SenderClientId) == null) return;
            p[roda] = 0;
            _pneus.Value = p;
            FurouRpc(roda);
        }

        [Rpc(SendTo.Everyone)]
        void FurouRpc(byte roda)
        {
            if (rodasVisuais == null || roda >= rodasVisuais.Length || rodasVisuais[roda] == null) return;
            Efeitos.Som(SomDoCarro.PneuFurando, rodasVisuais[roda].position, 1f, true, 60f);
        }

        // as rodas de acordo com os pneus: o raio (furada, ela baixa; sem ela, o cubo), a aderência e o modelo (sem a roda,
        // some). Em todas as máquinas (o raio também conta para a roda dos outros descer até o chão)
        void AplicarPneus()
        {
            var p = Pneus;
            if (_pneusFeitos && p.Equals(_pneusAplicados)) return;
            _pneusFeitos = true;
            _pneusAplicados = p;
            if (rodas == null) return;
            for (int i = 0; i < rodas.Length && i < PneusDoCarro.Quantas; i++)
            {
                if (rodas[i] == null) continue;
                rodas[i].radius = p.Raio(i);
                _aderencia[i] = p.Aderencia(i);
                var frente = rodas[i].forwardFriction;
                frente.stiffness = 1.6f * _aderencia[i];
                rodas[i].forwardFriction = frente;
                if (rodasVisuais != null && i < rodasVisuais.Length && rodasVisuais[i] != null)
                    rodasVisuais[i].gameObject.SetActive(p.Tem(i));
            }
        }

        // a barra estabilizadora de um eixo: puxa para baixo o lado que a suspensão esticou
        void Barra(WheelCollider e, WheelCollider d)
        {
            float ce = 1f, cd = 1f;
            bool noChaoE = e.GetGroundHit(out var he), noChaoD = d.GetGroundHit(out var hd);
            if (noChaoE) ce = (-e.transform.InverseTransformPoint(he.point).y - e.radius) / e.suspensionDistance;
            if (noChaoD) cd = (-d.transform.InverseTransformPoint(hd.point).y - d.radius) / d.suspensionDistance;
            float f = (ce - cd) * antiCapotar;
            if (noChaoE) _corpo.AddForceAtPosition(e.transform.up * -f, e.transform.position);
            if (noChaoD) _corpo.AddForceAtPosition(d.transform.up * f, d.transform.position);
        }

        // a rotação do motor (0 a 1): 4 marchas imaginárias; parado, a marcha lenta sobe com o acelerador. As marchas são
        // as da perua (a 4ª acaba em 23 m/s) esticadas até o fimDasMarchas deste carro (o cupê, mais rápido)
        float Rotacao(float v, float acelerar)
        {
            if (v < 0.6f) return 0.12f + acelerar * 0.3f;
            v *= 23f / Mathf.Max(1f, fimDasMarchas);
            float[] marchas = { 0f, 5f, 10f, 15.5f, 23f };
            int m = 0;
            while (m < marchas.Length - 2 && v > marchas[m + 1]) m++;
            float dentro = Mathf.InverseLerp(marchas[m], marchas[m + 1], v);
            return Mathf.Lerp(m == 0 ? 0.2f : 0.38f, 0.92f, dentro) + acelerar * 0.06f;
        }

        /// <summary>Quanto o carro tira de vida (na escala de 100) por km/h acima de <see cref="KmhSemDano"/>.</summary>
        public const float DanoPorKmh = 5f, KmhSemDano = 10f;

        // os zumbis na frente do carro (ou atrás, de ré) levam o acerto, como um tiro no tronco do motorista. O dano vem da
        // velocidade, sem teto (31/10/2026, a pedido: o atropelamento no sistema de vida de 100, pensando nos zumbis
        // especiais): (km/h - 10) x 5; o Oco (100) morre a partir de 30 km/h, e um especial de 300, a partir de 70
        void Atropelar(float v)
        {
            if (Mathf.Abs(v) < 3f) return;
            var motorista = Ocupante(0);
            if (motorista == null || !motorista.IsOwner) return;
            Vector3 centro = transform.TransformPoint(new Vector3(0f, 0.85f, v > 0f ? 1.75f : -1.75f));
            int n = Physics.OverlapBoxNonAlloc(centro, new Vector3(1.0f, 0.65f, 0.75f), _encostados, transform.rotation, 1 << Zumbi.CamadaZumbi, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = _encostados[i].GetComponent<Hitbox>();
                if (h == null || h.zumbi == null || h.zumbi.Morto) continue;
                int chave = h.zumbi.Id * 256 + h.zumbi.Geracao;
                if (_atropelados.TryGetValue(chave, out float ate) && Time.time < ate) continue;
                _atropelados[chave] = Time.time + 0.7f;
                float rapidez = Mathf.Abs(v);
                float dano = Mathf.Max(1f, (rapidez * 3.6f - KmhSemDano) * DanoPorKmh);
                Vector3 ponto = h.zumbi.transform.position + Vector3.up * 1.0f;
                // o zumbi de pedra (o sol) quebra com a batida (o "arremesso" quebra a estátua), e pesa mais
                bool pedra = h.zumbi.Petrificado;
                motorista.AcertarZumbi(h.zumbi, RegiaoDoCorpo.Tronco, Membro.Nenhum, dano, ponto, rapidez > 8f || pedra);
                if (pedra)
                {
                    Efeitos.Impacto(ponto, transform.forward * Mathf.Sign(v));
                    Efeitos.Som(Efeitos.SocoParede, ponto, 1f, true, 40f);
                }
                else
                {
                    Efeitos.Som(Efeitos.SocoCarne, ponto, 1f, true, 30f);
                    Sangue.Respingo(ponto, transform.forward * Mathf.Sign(v));
                }
                _corpo.linearVelocity *= pedra ? 0.8f : dano >= 100f ? 0.9f : 0.96f;
            }
            if (_atropelados.Count > 64) _atropelados.Clear();
        }

        // ------------------------------------------------------------------------------------------ o capô e as peças

        public string NomeDoCarro => "Perua da família";
        public CofreDoMotor Cofre => cofre;
        public Vector3 Ponto => transform.TransformPoint(0f, 0.9f, 1.6f);
        public bool Aberto => IsSpawned && !Virado && Velocidade < 1.5f;
        public string Impedimento => MotorLigado ? "Desligue o motor para mexer nas peças" : null;

        public string Situacao
        {
            get
            {
                float t = Temperatura;
                string motor = Fervendo ? "fervendo" : t < 0.3f ? "frio" : t < 0.75f ? "normal" : "quente";
                return Pecas.Tem(PecasDoMotor.Bateria)
                    ? $"Carga da bateria: {Pecas.Estado(PecasDoMotor.Bateria)}%   ·   Motor: {motor}"
                    : $"Sem bateria   ·   Motor: {motor}";
            }
        }

        public void PedirTirar(int peca) => TirarPecaRpc((byte)peca);
        public void PedirPor(int peca, ushort idNaMochila) => PorPecaRpc((byte)peca, idNaMochila);
        public void Olhando(bool olhando)
        {
            if (IsSpawned) CapoRpc(olhando);
        }

        // quem pediu está perto do capô, e o carro está parado
        bool PertoDoCapo(ulong cliente, out InventarioJogador inventario)
        {
            inventario = null;
            var j = Jogador(cliente);
            if (j == null || !Aberto || (j.transform.position - Ponto).sqrMagnitude > 3.5f * 3.5f) return false;
            inventario = j.GetComponent<InventarioJogador>();
            return inventario != null;
        }

        [Rpc(SendTo.Server)]
        void TirarPecaRpc(byte peca, RpcParams parametros = default)
        {
            if (MotorLigado || !PertoDoCapo(parametros.Receive.SenderClientId, out var inventario)) return;
            var p = _pecas.Value;
            if (!p.Tirar(peca, inventario)) return;
            _pecas.Value = p;
            if (peca == PecasDoMotor.Bateria) _carga = 0f;
        }

        [Rpc(SendTo.Server)]
        void PorPecaRpc(byte peca, ushort id, RpcParams parametros = default)
        {
            if (MotorLigado || !PertoDoCapo(parametros.Receive.SenderClientId, out var inventario)) return;
            var p = _pecas.Value;
            if (!p.Por(peca, inventario, id)) return;
            _pecas.Value = p;
            if (peca == PecasDoMotor.Bateria) _carga = p.Estado(PecasDoMotor.Bateria);
        }

        [Rpc(SendTo.Server)]
        void CapoRpc(bool olhando, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (olhando && PertoDoCapo(cliente, out _)) _olhandoOCapo.Add(cliente);
            else _olhandoOCapo.Remove(cliente);
        }

        /// <summary>
        /// No anfitrião: a carga da bateria (a partida gasta; o motor ligado carrega; a ignição e os faróis com o motor
        /// desligado gastam devagar) e a temperatura do motor (ligado, vai até o alvo do radiador; desligado, esfria).
        /// Também fecha o capô de quem se afastou, saiu da partida ou com o carro andando.
        /// </summary>
        void Desgaste(float dt)
        {
            var p = _pecas.Value;
            var e = Estado;
            if (p.Tem(PecasDoMotor.Bateria))
            {
                float gasto = e == EstadoDoMotor.Arranque && p.BateriaGira ? gastoDaPartida
                    : e == EstadoDoMotor.Ligado ? -cargaDoMotor
                    : (e == EstadoDoMotor.Ignicao ? 0.05f : 0f) + (_farois.Value ? 0.15f : 0f);
                _carga = Mathf.Clamp(_carga - gasto * dt, 0f, 100f);
                int nova = Mathf.RoundToInt(_carga);
                if (nova != p.bateria)
                {
                    p.bateria = (byte)nova;
                    _pecas.Value = p;
                }
            }
            bool ligado = e == EstadoDoMotor.Ligado;
            _temperatura = ligado
                ? Mathf.MoveTowards(_temperatura, p.TemperaturaAlvo, Mathf.Abs(p.TemperaturaAlvo - _temperatura) * dt / esquentar + 0.0005f * dt)
                : Mathf.MoveTowards(_temperatura, 0f, _temperatura * dt / esfriar + 0.0005f * dt);
            byte calor = (byte)Mathf.Clamp(Mathf.RoundToInt(_temperatura * 200f), 0, 255);
            if (Mathf.Abs(calor - _calor.Value) >= 2) _calor.Value = calor;
            if (!_fervendo.Value && _temperatura >= 1f) _fervendo.Value = true;
            else if (_fervendo.Value && _temperatura < 0.7f) _fervendo.Value = false;

            // quem se afastou (com folga), saiu da partida, ou o carro andou: o capô fecha
            if (_olhandoOCapo.Count > 0)
                _olhandoOCapo.RemoveWhere(c =>
                {
                    var j = Jogador(c);
                    return j == null || !Aberto || (j.transform.position - Ponto).sqrMagnitude > 4.5f * 4.5f;
                });
            bool aberto = _olhandoOCapo.Count > 0;
            if (_capoAberto.Value != aberto) _capoAberto.Value = aberto;
        }

        // o capô sobe enquanto alguém olha o motor (quem olha desta máquina já vê subir, sem esperar a rede), e desce batendo
        void Capo(float dt)
        {
            bool aberto = _capoAberto.Value || (cofre != null && cofre.Inspecionando);
            CofreDoMotor.AnimarCapo(capo, _capoRepouso, aberturaDoCapo, ref _capoVisto, aberto, dt);
        }

        // ------------------------------------------------------------------------------------------ o que se vê e ouve

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                var medida = (transform.position - _posAntes) / dt;
                _velocidadeMedida = Vector3.Lerp(_velocidadeMedida, medida, 1f - Mathf.Exp(-10f * dt));
            }
            _posAntes = transform.position;
            if (!IsSpawned) return;
            Acertar();

            if (Virado) { if (_viradoDesde < 0f) _viradoDesde = Time.time; }
            else _viradoDesde = -1f;

            AplicarPneus();
            Rodas(dt);
            // os faróis precisam da bateria
            bool farois = _farois.Value && Pecas.BateriaAcende && !FaroisSujos;
            if (_faroisAplicados != farois) AplicarFarois(farois);
            if (IsServer) Desgaste(dt);
            Capo(dt);
            PortaMalas(dt);
            MacacoApoiado();
            Ignicao(dt);
            Chave(dt);
            Motor(dt);
            // sem motorista, a buzina para (quem saiu não solta mais a tecla)
            if (IsOwner && _ocupantes.Value[0] == 0 && (_buzinaLocal || _buzina.Value)) Buzinar(false);
            Buzina(dt);
            Painel(dt);
        }

        void Rodas(float dt)
        {
            float giro = IsOwner ? _giro : _direcao.Value / 127f * giroMaximo;
            // o volante desenhado: menos voltas que antes e um pouco atrás das rodas (só o visual)
            _volanteVisto = Mathf.MoveTowards(_volanteVisto, -giro * relacaoDoVolante, giroMaximoDoVolanteVisto * dt);
            if (volante != null) volante.localRotation = _volanteRepouso * Quaternion.Euler(0f, 0f, _volanteVisto);
            if (rodasVisuais == null) return;
            _giroDasRodas += VelocidadeParaFrente * dt / 0.3f * Mathf.Rad2Deg;
            for (int i = 0; i < rodasVisuais.Length && i < rodas.Length; i++)
            {
                var visual = rodasVisuais[i];
                bool esquerda = i % 2 == 0;
                var virar = Quaternion.Euler(0f, esquerda ? 180f : 0f, 0f);
                if (IsOwner && MacacoNaRoda < 0)
                {
                    rodas[i].GetWorldPose(out var pos, out var rot);
                    visual.SetPositionAndRotation(pos, rot * virar);
                    continue;
                }
                // nos outros (e no macaco): a roda desce até o chão (dentro do curso da suspensão), vira e gira pela velocidade
                var w = rodas[i];
                Vector3 topo = w.transform.TransformPoint(w.center);
                // a roda do macaco, com o carro erguido, quase não estica: ela sai do chão
                float curso = i == MacacoNaRoda && Erguido ? Mathf.Min(0.05f, w.suspensionDistance) : w.suspensionDistance;
                float desce = curso;
                if (Physics.Raycast(topo, -transform.up, out var chao, w.radius + curso, ~(1 << CamadaVeiculo), QueryTriggerInteraction.Ignore))
                    desce = Mathf.Clamp(chao.distance - w.radius, 0f, curso);
                visual.position = topo - transform.up * desce;
                float angulo = i < 2 ? giro : 0f;
                visual.rotation = transform.rotation * Quaternion.Euler(0f, angulo, 0f) * Quaternion.Euler(_giroDasRodas, 0f, 0f) * virar;
            }
        }

        void AplicarFarois(bool ligados)
        {
            _faroisAplicados = ligados;
            if (farois != null) foreach (var f in farois) if (f != null) f.enabled = ligados;
            if (carroceria != null && _matsApagados != null) carroceria.sharedMaterials = ligados ? _matsAcesos : _matsApagados;
        }

        // o laço do motor: entra quando o som de pegar já assentou na marcha lenta; desligado, morre (o tom cai e some)
        void Motor(float dt)
        {
            if (motor == null) return;
            bool ligado = MotorLigado;
            float rot = IsOwner ? _rotacaoLocal : _rotacao.Value / 255f;
            float alvo = ligado && Time.time > _pegouEm + SomDoCarro.LacoEntra ? 0.35f + rot * 0.45f : 0f;
            _volumeDoMotor = Mathf.MoveTowards(_volumeDoMotor, alvo, dt * (alvo > 0f ? 1.5f : 1.1f));
            if (motor.clip == null) motor.clip = SomDoCarro.Motor;
            motor.volume = _volumeDoMotor;
            motor.pitch = Mathf.Lerp(motor.pitch, ligado ? 0.75f + rot * 1.7f : 0.4f, 1f - Mathf.Exp(-(ligado ? 6f : 3f) * dt));
            if (_volumeDoMotor > 0.001f && !motor.isPlaying) motor.Play();
            else if (_volumeDoMotor <= 0.001f && motor.isPlaying) motor.Stop();
        }

        // a buzina toca enquanto o motorista segura B (no dono, sem esperar a rede)
        void Buzina(float dt)
        {
            if (buzina == null) return;
            if (buzina.clip == null) buzina.clip = SomDoCarro.Buzina;
            bool tocando = (IsOwner ? _buzinaLocal : _buzina.Value) && Pecas.BateriaAcende;
            buzina.volume = Mathf.MoveTowards(buzina.volume, tocando ? 0.9f : 0f, dt * 25f);
            if (buzina.volume > 0.001f && !buzina.isPlaying) buzina.Play();
            else if (buzina.volume <= 0.001f && buzina.isPlaying) buzina.Stop();
        }

        /// <summary>
        /// O painel acende com a ignição (a chave na primeira posição) e fica mais fraco enquanto o motor de arranque
        /// gira; os ponteiros mostram a velocidade e a gasolina (sem ignição, os dois caem para o zero). A luz de teto
        /// acende quando uma porta abre e apaga devagar uns segundos depois de ela fechar.
        /// </summary>
        void Painel(float dt)
        {
            var e = Estado;
            bool ignicao = IgnicaoLigada;
            // a bateria fraca acende o painel fraco; o arranque girando puxa a luz
            float alvo = !ignicao ? 0f : ArranqueGira ? 0.6f : Pecas.BateriaGira || MotorLigado ? 1f : 0.35f;
            _brilhoPainel = Mathf.MoveTowards(_brilhoPainel, alvo, dt * 6f);
            bool aceso = _brilhoPainel > 0.05f;
            if (aceso != _painelAceso)
            {
                _painelAceso = aceso;
                if (painel != null) painel.sharedMaterial = aceso ? painelAceso : painelApagado;
                if (ponteiros != null) foreach (var p in ponteiros) if (p != null) p.sharedMaterial = aceso ? ponteiroAceso : ponteiroApagado;
            }
            if (luzDoPainel != null)
            {
                luzDoPainel.enabled = aceso;
                luzDoPainel.intensity = intensidadeDoPainel * _brilhoPainel;
            }

            float kmh = ignicao ? Mathf.Abs(VelocidadeParaFrente) * 3.6f : 0f;
            _ponteiroVel = Mathf.Lerp(_ponteiroVel, kmh, 1f - Mathf.Exp(-6f * dt));
            _ponteiroComb = Mathf.MoveTowards(_ponteiroComb, ignicao ? combustivel : 0f, dt * 0.5f);
            // o ponteiro aponta para +Y; o painel olha para o motorista (-Z): no sentido do relógio é o giro negativo em Z
            if (ponteiroVelocidade != null) ponteiroVelocidade.localRotation = Quaternion.Euler(0f, 0f, -AnguloDoVelocimetro(_ponteiroVel));
            if (ponteiroCombustivel != null) ponteiroCombustivel.localRotation = Quaternion.Euler(0f, 0f, -AnguloDoCombustivel(_ponteiroComb));
            if (indicadorFarol != null) indicadorFarol.enabled = _farois.Value && Pecas.BateriaAcende;

            float teto = Mathf.Clamp01((_tetoAte - Time.time) / 1.2f);
            _brilhoTeto = Mathf.MoveTowards(_brilhoTeto, teto, dt * 5f);
            bool tetoLigado = _brilhoTeto > 0.05f;
            if (tetoLigado != _tetoAceso)
            {
                _tetoAceso = tetoLigado;
                if (lenteDoTeto != null) lenteDoTeto.sharedMaterial = tetoLigado ? tetoAceso : tetoApagado;
            }
            if (luzDoTeto != null)
            {
                luzDoTeto.enabled = tetoLigado;
                luzDoTeto.intensity = intensidadeDoTeto * _brilhoTeto;
            }
        }
    }
}
