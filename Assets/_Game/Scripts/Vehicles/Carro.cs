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
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Carro : NetworkBehaviour
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

        [Header("Direção")]
        [Tooltip("Torque em cada roda da frente (N·m), com o acelerador no fundo e o carro parado.")]
        public float torque = 700f;
        public float velocidadeMaxima = 22f;
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
        [Tooltip("Quantas vezes o volante gira mais que as rodas.")]
        public float voltasDoVolante = 12f;

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
        /// <summary>O giro das rodas da frente (-127 a 127 = todo para a esquerda ou a direita), para os outros verem.</summary>
        readonly NetworkVariable<sbyte> _direcao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>A rotação do motor (0 a 255), para o som.</summary>
        readonly NetworkVariable<byte> _rotacao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> _buzina = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>A chave e o motor (<see cref="EstadoDoMotor"/>), escritos pelo dono (o motorista).</summary>
        readonly NetworkVariable<byte> _motorRede = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
        float _giroDasRodas;
        readonly float[] _mudouEm = { -99f, -99f, -99f, -99f };
        readonly Dictionary<int, float> _atropelados = new Dictionary<int, float>();
        static readonly Collider[] _encostados = new Collider[32];
        Material[] _matsApagados, _matsAcesos;
        bool _faroisAplicados, _buzinaLocal, _painelAceso, _tetoAceso, _anelAceso = true;
        float _volumeDoMotor, _rotacaoLocal, _enviouRotacao, _viradoDesde = -1f;
        // a chave e o motor: o estado e o ângulo da chave no dono; o que cada máquina já tocou e mostrou
        EstadoDoMotor _estadoLocal, _estadoOuvido;
        float _anguloDaChave, _anguloVisto, _chaveEntrouEm = -99f, _pegouEm = -99f, _puxando;
        bool _presaNaPartida;
        float _brilhoPainel, _brilhoTeto, _tetoAte = -99f, _ponteiroVel, _ponteiroComb;
        Quaternion _volanteRepouso;

        /// <summary>A velocidade (m/s): a da física no dono, a medida pela posição nos outros.</summary>
        public float Velocidade => Simula ? _corpo.linearVelocity.magnitude : _velocidadeMedida.magnitude;
        /// <summary>A velocidade para a frente (negativa de ré).</summary>
        public float VelocidadeParaFrente => Vector3.Dot(Simula ? _corpo.linearVelocity : _velocidadeMedida, transform.forward);
        /// <summary>De lado ou de rodas para cima.</summary>
        public bool Virado => transform.up.y < 0.35f;
        /// <summary>Virado há mais de 1,5 s e quase parado: o motorista pode desvirar.</summary>
        public bool PodeDesvirar => Virado && _viradoDesde >= 0f && Time.time - _viradoDesde > 1.5f && Velocidade < 2f;
        public bool FaroisLigados => IsSpawned && _farois.Value;
        bool Simula => !IsSpawned || IsOwner;
        /// <summary>A chave e o motor: o do dono na hora, o da rede nos outros.</summary>
        public EstadoDoMotor Estado => !IsSpawned ? EstadoDoMotor.SemChave : IsOwner ? _estadoLocal : (EstadoDoMotor)_motorRede.Value;
        /// <summary>O motor está ligado (o carro anda).</summary>
        public bool MotorLigado => Estado >= EstadoDoMotor.Ligado;
        /// <summary>O ângulo da chave (graus no sentido do relógio): o do dono, ou o da posição do estado nos outros.</summary>
        public float AnguloDaChave => IsSpawned && IsOwner ? _anguloDaChave : AnguloDe(Estado);
        /// <summary>Quando o motor pegou (Time.time; -99 se ele já estava ligado quando esta máquina viu).</summary>
        public float PegouEm => _pegouEm;

        void Awake()
        {
            _corpo = GetComponent<Rigidbody>();
            _corpo.centerOfMass = new Vector3(0f, 0.38f, 0.12f);
            if (rodas != null && rodas.Length > 0) rodas[0].ConfigureVehicleSubsteps(5f, 12, 15);
            if (volante != null) _volanteRepouso = volante.localRotation;
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
            if (lugar >= Lugares) return;
            var o = _ocupantes.Value;
            if (o[lugar] != 0) return;
            var jogador = Jogador(cliente);
            if (jogador == null || jogador.Passageiro == null || jogador.Passageiro.Sentado) return;
            if (jogador.Vida != null && jogador.Vida.Vida.Value <= 0f) return;
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

        /// <summary>O mini-game da partida deu certo: o motor pega na hora, e a chave volta para a ignição.</summary>
        public void PegarAgora()
        {
            if (!IsSpawned || !IsOwner || _estadoLocal != EstadoDoMotor.Arranque) return;
            _presaNaPartida = false;
            _anguloDaChave = ChaveLiga;
            MudarEstado(EstadoDoMotor.Ligado);
        }

        /// <summary>Um acerto no mini-game da partida: o motor tosse (quase pega). Só nesta máquina.</summary>
        public void Tossir()
        {
            Efeitos.Som(SomDoCarro.Tossida, motor != null ? motor.transform.position : transform.position, 0.6f, true, 30f);
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
                if (e != EstadoDoMotor.SemChave) MudarEstado(EstadoDoMotor.SemChave);
                return;
            }
            if (e == EstadoDoMotor.SemChave) return;
            if (!_presaNaPartida && _anguloDaChave > ChaveLiga)
                _anguloDaChave = Mathf.MoveTowards(_anguloDaChave, ChaveLiga, molaDaPartida * dt);
            EstadoDoMotor novo;
            if (_anguloDaChave < ChaveDesliga) novo = EstadoDoMotor.Desligado;
            else if (e == EstadoDoMotor.Ligado) novo = EstadoDoMotor.Ligado;
            else if (_presaNaPartida) novo = EstadoDoMotor.Arranque;
            else novo = EstadoDoMotor.Ignicao;
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
                bool girava = antes == EstadoDoMotor.Arranque;
                bool gira = e == EstadoDoMotor.Arranque;
                if (antes == EstadoDoMotor.SemChave)
                {
                    _chaveEntrouEm = Time.time;
                    Efeitos.Som(SomDoCarro.ChaveEntra, naChave, 0.55f, true, 6f);
                }
                else if (e != EstadoDoMotor.SemChave)
                    Efeitos.Som(SomDoCarro.CliqueDaChave, naChave, 0.5f, true, 6f);
                if (gira && !girava) Efeitos.Som(SomDoCarro.Solenoide, noMotor, 0.6f, true, 30f);
                if (girava && !gira) Efeitos.Som(SomDoCarro.SoltarArranque, noMotor, 0.6f, true, 30f);
                if (antes == EstadoDoMotor.Arranque && e >= EstadoDoMotor.Ligado)
                {
                    _pegouEm = Time.time;
                    Efeitos.Som(SomDoCarro.Pegar, noMotor, 0.6f, true, 35f);
                }
            }

            // o arranque em laço, enquanto a chave fica na partida
            if (arranque != null)
            {
                if (arranque.clip == null) arranque.clip = SomDoCarro.Arranque;
                bool toca = e == EstadoDoMotor.Arranque;
                arranque.volume = toca ? 0.6f : 0f;
                if (toca && !arranque.isPlaying) arranque.Play();
                else if (!toca && arranque.isPlaying) arranque.Stop();
            }

            // a chave: some sem motorista, entra pelo miolo e gira (nos outros, devagar até a posição do estado)
            if (chave == null) return;
            bool comChave = e != EstadoDoMotor.SemChave;
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
            // freio de mão: a traseira escorrega
            for (int i = 2; i < 4; i++)
            {
                var lado = rodas[i].sidewaysFriction;
                float rigidez = mao > 0f && Mathf.Abs(v) > 3f ? 0.9f : 2f;
                if (!Mathf.Approximately(lado.stiffness, rigidez))
                {
                    lado.stiffness = rigidez;
                    rodas[i].sidewaysFriction = lado;
                }
            }

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

        // a rotação do motor (0 a 1): 4 marchas imaginárias; parado, a marcha lenta sobe com o acelerador
        static float Rotacao(float v, float acelerar)
        {
            if (v < 0.6f) return 0.12f + acelerar * 0.3f;
            float[] marchas = { 0f, 5f, 10f, 15.5f, 23f };
            int m = 0;
            while (m < marchas.Length - 2 && v > marchas[m + 1]) m++;
            float dentro = Mathf.InverseLerp(marchas[m], marchas[m + 1], v);
            return Mathf.Lerp(m == 0 ? 0.2f : 0.38f, 0.92f, dentro) + acelerar * 0.06f;
        }

        // os zumbis na frente do carro (ou atrás, de ré) levam o acerto, como um tiro no tronco do motorista
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
                float dano = Mathf.Clamp((rapidez - 2.5f) * 1.6f, 1f, 10f);
                Vector3 ponto = h.zumbi.transform.position + Vector3.up * 1.0f;
                motorista.AcertarZumbi(h.zumbi, RegiaoDoCorpo.Tronco, Membro.Nenhum, dano, ponto, rapidez > 8f);
                Efeitos.Som(Efeitos.SocoCarne, ponto, 1f, true, 30f);
                Sangue.Respingo(ponto, transform.forward * Mathf.Sign(v));
                _corpo.linearVelocity *= dano >= 6f ? 0.9f : 0.96f;
            }
            if (_atropelados.Count > 64) _atropelados.Clear();
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

            Rodas(dt);
            if (_faroisAplicados != _farois.Value) AplicarFarois(_farois.Value);
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
            if (volante != null) volante.localRotation = _volanteRepouso * Quaternion.Euler(0f, 0f, -giro * voltasDoVolante);
            if (rodasVisuais == null) return;
            _giroDasRodas += VelocidadeParaFrente * dt / 0.3f * Mathf.Rad2Deg;
            for (int i = 0; i < rodasVisuais.Length && i < rodas.Length; i++)
            {
                var visual = rodasVisuais[i];
                bool esquerda = i % 2 == 0;
                var virar = Quaternion.Euler(0f, esquerda ? 180f : 0f, 0f);
                if (IsOwner)
                {
                    rodas[i].GetWorldPose(out var pos, out var rot);
                    visual.SetPositionAndRotation(pos, rot * virar);
                    continue;
                }
                // nos outros: a roda desce até o chão (dentro do curso da suspensão), vira e gira pela velocidade
                var w = rodas[i];
                Vector3 topo = w.transform.TransformPoint(w.center);
                float desce = w.suspensionDistance;
                if (Physics.Raycast(topo, -transform.up, out var chao, w.radius + w.suspensionDistance, ~(1 << CamadaVeiculo), QueryTriggerInteraction.Ignore))
                    desce = Mathf.Clamp(chao.distance - w.radius, 0f, w.suspensionDistance);
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
            bool tocando = IsOwner ? _buzinaLocal : _buzina.Value;
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
            bool ignicao = e >= EstadoDoMotor.Ignicao;
            float alvo = !ignicao ? 0f : e == EstadoDoMotor.Arranque ? 0.6f : 1f;
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
            if (indicadorFarol != null) indicadorFarol.enabled = _farois.Value;

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
