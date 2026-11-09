using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jogador na rede. O dono controla o próprio movimento (NetworkTransform com autoridade do dono) e joga em
    /// primeira pessoa: a câmera do mundo, a câmera dos braços por cima (camada própria, não atravessa paredes) e o
    /// próprio corpo só como sombra. Os outros veem o corpo animado, para onde ele olha, o que está na mão (do
    /// <see cref="InventarioJogador"/>), a lanterna e os tiros.
    /// </summary>
    public partial class JogadorRede : NetworkBehaviour, IAlvoDeZumbi
    {
        /// <summary>
        /// Para os zumbis (02/11/2026: eles também perseguem os sobreviventes): o jogador na rede. Caído (03/11/2026, a tela
        /// de morte), ninguém vai atrás dele.
        /// </summary>
        public bool AlvoValido => this != null && IsSpawned && (Vida == null || !Vida.Caido.Value);

        /// <summary>No anfitrião: a mordida de um zumbi.</summary>
        public void LevarGolpe(float dano, Vector3 de)
        {
            // (o golpe dos zumbis passa pelo ZombieManager.Ferimentos, que escolhe a parte; este é o de reserva)
            if (Vida != null) Vida.Dano(dano, de, "Mordido pelos zumbis.", TipoDeGolpe.Mordida);
        }

        public const int CamadaBracos = 6;
        /// <summary>A raiz do jogador (o CharacterController): os pedaços dos zumbis não param em cima de ninguém.</summary>
        public const int CamadaJogador = 9;
        /// <summary>O retrato do jogador na tela do inventário (<see cref="RetratoDoJogador"/>): só a câmera dele desenha.</summary>
        public const int CamadaRetrato = 10;
        /// <summary>
        /// A camada de render (rendering layer, não a camada do GameObject) do corpo do dono, só na máquina dele: a lanterna
        /// dele não faz sombra dela (as mãos e os braços do corpo, invisíveis na primeira pessoa, faziam sombra no facho),
        /// e a lua faz (a sombra dele no chão continua). Os assets de URP precisam das camadas de render ligadas
        /// (<c>MontagemDoClima.CamadasDeRender</c>).
        /// </summary>
        public const int CamadaDeRenderDoDono = 1;
        public const uint MascaraDoDono = 1u << CamadaDeRenderDoDono;

        /// <summary>
        /// A camada de render da arma (ou do item) na mão dos outros jogadores, nesta máquina (31/10/2026): a lanterna de
        /// cada um fica no peito, colada na coronha das armas longas, e estourava a arma dele de branco e fazia a sombra do
        /// cano no facho. A lanterna dos outros não ilumina nem faz sombra desta camada (a do dono não ilumina; a sombra
        /// dela é de outra luz, longe); o sol e a lua continuam. A do dono, na terceira pessoa, fica na
        /// <see cref="CamadaDeRenderDoDono"/>.
        /// </summary>
        public const int CamadaDaArmaDosOutros = 2;
        public const uint MascaraDaArmaDosOutros = 1u << CamadaDaArmaDosOutros;

        [Header("Primeira pessoa (só o dono)")]
        public Camera cameraMundo;
        public Camera cameraBracos;
        public AudioListener ouvido;
        public GameObject bracos;

        [Header("Os corpos (um por personagem, na ordem de Personagens.Modelos; só o escolhido fica ligado)")]
        public CorpoDoPersonagem[] corpos;

        [Header("Corpo (o que os outros veem): o do personagem escolhido, copiado do CorpoDoPersonagem dele")]
        public Animator animadorCorpo;
        public Renderer[] renderersCorpo;
        public Transform ossoPeito;
        public Transform ossoCabeca;

        [Header("O que está na mão, no corpo (na ordem de Itens.Modelos: as armas, a lata e a garrafa)")]
        [Tooltip("As armas e os itens avulsos dentro do Arma_TP (filho do weapon.R): só o da mão fica ligado.")]
        public GameObject[] naMaoCorpo;
        [Tooltip("A boca do cano de cada arma (na ordem de TipoDeArma).")]
        public Transform[] bocasCorpo;
        [Tooltip("O controlador do corpo com cada arma: o da pistola e os override com as animações de arma longa.")]
        public RuntimeAnimatorController[] controladoresCorpo;
        [Tooltip("O corpo com a mão vazia (os punhos em guarda e o soco) ou com comida e água na mão.")]
        public RuntimeAnimatorController controladorDasMaos;
        [Tooltip("O corpo com a lata e com a garrafa na mão (na ordem de Itens.Modelos, a partir de Armas.Quantas); vazio: o dos punhos.")]
        public RuntimeAnimatorController[] controladoresDosItens;
        [Tooltip("O corpo com o taco de sinuca em pé na mão direita, e onde fica o taco (a pegada e a ponta: o TacoNaMao).")]
        public RuntimeAnimatorController controladorDoTaco;
        public Transform tacoCorpo, pontaDoTacoCorpo;
        [Tooltip("O corpo com o maço de cigarros (segurar e acender) e com o cigarro aceso (segurar, tragar e o peteleco), e os vazios das peças do fumo (Fumo_<Peça>: o FumoJogador).")]
        public RuntimeAnimatorController controladorDoMaco, controladorDoCigarro;
        public Transform[] fumoCorpo;
        [Tooltip("As malhas das peças do fumo no corpo (como o que está na mão: no dono, só aparecem com o corpo à vista).")]
        public Renderer[] renderersDoFumo;

        [Header("A pele dos braços de primeira pessoa")]
        [Tooltip("Jogador_Variacao0..3: a linha k da paleta tem a pele do personagem k (as mangas e as luvas também).")]
        public Material[] variacoes;
        [Tooltip("Renderers que trocam de cor (o material 0 de cada um): os braços de primeira pessoa.")]
        public Renderer[] pintaveis;

        /// <summary>Para onde a cabeça aponta na vertical, em graus (positivo = olhando para baixo).</summary>
        public readonly NetworkVariable<float> Pitch = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Mirando = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>
        /// Olhando pela luneta (a sniper). O anfitrião usa para os zumbis na linha da luneta: de longe eles contam como
        /// se estivessem perto (aparecem, têm hitboxes e vão pela rede), na proporção do aumento.
        /// </summary>
        public readonly NetworkVariable<bool> Luneta = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>
        /// O personagem (o índice em <see cref="Personagens.Modelos"/>), escolhido no menu antes de entrar e escrito pelo
        /// dono quando ele nasce. Dois jogadores podem ter o mesmo.
        /// </summary>
        public readonly NetworkVariable<byte> Personagem = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>
        /// A aparência (o Plano_Personagem.md da pasta de arte): o nome, o sexo, a pele, o cabelo, a barba, a cor deles, a
        /// camiseta e a calça, escrita pelo dono quando ele cria o personagem, na tela de criar (<see cref="TelaDeCriarPersonagem"/>,
        /// que abre quando ele entra; o robô de teste sai sorteado). Quem entra depois recebe a dos outros. Antes de criar
        /// (<see cref="Criado"/>), o corpo e o que está na mão ficam escondidos, e os zumbis não o veem.
        /// </summary>
        public readonly NetworkVariable<AparenciaDoJogador> Aparencia = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>O personagem foi criado (a aparência chegou): antes, ele não aparece para ninguém.</summary>
        public bool Criado => Aparencia.Value.definida != 0;

        /// <summary>
        /// A chave do personagem salvo (02/11/2026, o save: <see cref="Salvar.PersonagemSalvo.Chave"/>), escrita pelo dono
        /// quando nasce: o anfitrião guarda por ela onde cada um estava no mundo e de quem é cada cama.
        /// </summary>
        public readonly NetworkVariable<ulong> Chave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Abaixado (03/11/2026, a pedido: o C; <see cref="MovimentoPrimeiraPessoa.Agachado"/>), escrito pelo dono: o corpo
        /// de todos toca o agachado (o parâmetro "Agachado" da árvore da locomoção, de 0 a 1, aos poucos).
        /// </summary>
        public readonly NetworkVariable<bool> Agachado = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// O dono cria o personagem (a tela de criar, ou o personagem salvo): a aparência vai pela rede, e o que ele leva
        /// (o salvo, ou o começo de um novo) vai para o anfitrião.
        /// </summary>
        public void Criar(AparenciaDoJogador a)
        {
            if (!IsOwner) return;
            a.definida = 1;
            Aparencia.Value = a;
            Inventario?.ComecarPersonagem();
        }

        /// <summary>
        /// Um ponto dos braços de primeira pessoa (a boca da arma) no lugar dele no mundo: no dono, os braços ficam perto da
        /// origem (<see cref="BracosNaOrigem"/>); nos outros, ele mesmo.
        /// </summary>
        public Vector3 NoMundo(Vector3 pontoDosBracos)
        {
            if (_bracosNaOrigem == null) _bracosNaOrigem = GetComponent<BracosNaOrigem>();
            return _bracosNaOrigem != null ? _bracosNaOrigem.NoMundo(pontoDosBracos) : pontoDosBracos;
        }
        BracosNaOrigem _bracosNaOrigem;

        /// <summary>As roupas do corpo (a aparência e, depois, as roupas achadas).</summary>
        public ZombieCityCleaner.Roupas.Vestimenta Vestimenta { get; private set; }
        /// <summary>
        /// A distância de visão que este jogador escolheu (Configurações > Gráficos, <see cref="Graficos.Visao"/>: 0 Curta a 3
        /// Máxima), escrita pelo dono: o anfitrião manda os zumbis até ela e faz os novos nascerem mais longe dele.
        /// </summary>
        public readonly NetworkVariable<byte> Visao = new(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>
        /// Na chuva, o dono está debaixo de um teto (05/11/2026, o fogo na chuva; o dono confere a cada meio segundo): o
        /// anfitrião não monta o posto e as casas dos acampamentos longe do jogador dele, e a tocha de quem está lá dentro
        /// apagaria (Loot/ConstrucoesNoMundo.Chuva.cs).
        /// </summary>
        public readonly NetworkVariable<bool> DebaixoDeTeto = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        float _proximoTeto;

        /// <summary>O corpo mudou (o personagem chegou pela rede): quem guarda os ossos dele lê de novo.</summary>
        public event System.Action CorpoMudou;

        /// <summary>Quantos tiros deste jogador chegaram pela rede (para testes).</summary>
        public int TirosRecebidos { get; private set; }
        /// <summary>Velocidade no chão (m/s): a real no dono, a medida pela posição recebida nos outros.</summary>
        public float Velocidade => _velocidade;
        public LanternaJogador Lanterna { get; private set; }
        public VidaJogador Vida { get; private set; }
        /// <summary>O fumo (o maço e o cigarro aceso, 03/11/2026).</summary>
        public FumoJogador Fumo { get; private set; }

        /// <summary>Só no anfitrião: a gosma do Long prendeu este jogador (na máquina dele, ele se debate com E).</summary>
        public void PrenderNaGosma(float tempoParaSoltar)
        {
            if (IsServer) PresoNaGosmaRpc(tempoParaSoltar);
        }

        [Rpc(SendTo.Owner)]
        void PresoNaGosmaRpc(float tempoParaSoltar)
        {
            var m = GetComponent<MovimentoPrimeiraPessoa>();
            if (m != null) m.PrenderNaGosma(tempoParaSoltar);
            InventarioJogador.AvisoLocal("A gosma te prendeu no chão!");
        }
        public InventarioJogador Inventario { get; private set; }
        /// <summary>O jogador num carro (entrando, sentado ou saindo).</summary>
        public PassageiroDoCarro Passageiro { get; private set; }
        /// <summary>O que está na mão (o item do atalho escolhido do inventário).</summary>
        public Item NaMao => Inventario != null ? Inventario.NaMao : Item.Nada;
        /// <summary>
        /// A tocha acesa na mão, à vista (não a apagada pela chuva; no carro ela fica escondida): os zumbis ficam longe dela
        /// (ZombieManager.Luz.cs) e o Long cospe nela.
        /// </summary>
        public bool TochaAcesa => Inventario != null && Inventario.TochaAcesaNaMao && !_naMaoEscondida && (Passageiro == null || !Passageiro.NoCarro);
        /// <summary>A arma da mão; sem arma, a pistola (para quem pergunta pela luneta, por exemplo).</summary>
        public TipoDeArma ArmaNaMao => Itens.EhArma(NaMao) ? Itens.Arma(NaMao) : TipoDeArma.Pistola;

        public static JogadorRede Local { get; private set; }
        public static readonly List<JogadorRede> Todos = new List<JogadorRede>();

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AimingId = Animator.StringToHash("Aiming");
        static readonly int FireId = Animator.StringToHash("Fire");

        CharacterController _corpoFisico;
        Vector3 _posJanela;
        float _velocidade, _medidaRemota, _janela;
        Item _naMaoAplicado = (Item)255;
        ushort _idAplicado;
        bool _naMaoEscondida;
        /// <summary>O que está na mão do corpo está escondido (sentado num carro).</summary>
        public bool NaMaoEscondida => _naMaoEscondida;

        /// <summary>A câmera de terceira pessoa do dono (o V).</summary>
        public CameraTerceiraPessoa TerceiraPessoa => _terceira != null ? _terceira : _terceira = GetComponent<CameraTerceiraPessoa>();
        CameraTerceiraPessoa _terceira;

        /// <summary>A câmera do dono está atrás dele (a terceira pessoa a pé).</summary>
        public bool EmTerceiraPessoa => TerceiraPessoa != null && TerceiraPessoa.Ativa;

        /// <summary>
        /// De onde o dono olha, e para onde: o tiro, o soco, o E, o largar e a lanterna. Na primeira pessoa, a câmera; na
        /// terceira, a cabeça, olhando para o que está no meio da tela (<see cref="CameraTerceiraPessoa.Mira"/>).
        /// </summary>
        public Transform Olho => EmTerceiraPessoa ? TerceiraPessoa.Mira : cameraMundo.transform;

        /// <summary>O corpo do dono está visível na máquina dele (a terceira pessoa ou a câmera de trás do carro).</summary>
        public bool CorpoVisivelNoDono { get; private set; }
        bool _corpoDoDonoAplicado;

        /// <summary>
        /// O corpo do dono, na máquina dele: só sombra (a primeira pessoa), na <see cref="CamadaDeRenderDoDono"/> (fora da
        /// sombra da própria lanterna), ou visível e na camada normal (a terceira pessoa e a câmera de trás do carro). O
        /// que está na mão do corpo fica desligado na primeira pessoa, nem sombra: a ponta das armas longas passa dos 60 cm
        /// em que a sombra da lanterna começa (shadowNearPlane), e a sombra do cano aparecia no facho, na frente da arma de
        /// primeira pessoa. Na terceira pessoa, a arma da mão aparece, mas fica na camada do dono (31/10/2026, a pedido: o
        /// bug da lanterna voltava na terceira pessoa com a sniper, o rifle e a espingarda): a lanterna dele, no peito,
        /// fica colada na coronha das armas longas; ela estourava a arma de branco na mão e o cano fazia sombra no facho.
        /// Nessa camada a lanterna do dono nem a ilumina nem faz a sombra dela; o sol e a lua continuam (eles iluminam e
        /// fazem sombra de todas as camadas). Só muda o que mudou (a câmera de terceira pessoa chama a cada quadro).
        /// </summary>
        /// <summary>
        /// (07/11/2026, a pedido: no caiaque, "em primeira pessoa aparece apenas o remo... seria interessante ter ali a mão
        /// do personagem"; e as mãos no guidão) Na moto e no caiaque com o remo, na primeira pessoa, o dono vê o próprio
        /// corpo sem a cabeça e sem o pescoço (o osso do pescoço fica minúsculo, depois da animação; na moto, "a gente está
        /// vendo o pescoço do personagem e uma parte da cabeça"), e o peito não dobra com o olhar (no caiaque, olhando para
        /// baixo, o peito vinha para a frente, por cima da câmera: "essa parte do corpo um pouco bugada quando olho pra
        /// baixo"; os outros continuam vendo ele dobrar). As mãos no remo e no guidão são as do corpo, as mesmas que os
        /// outros veem. Quem senta chama a cada quadro. No carro, não: na primeira pessoa ele fica como antes, só a sombra
        /// ("do carro em primeira pessoa, tira e deixa como estava antes sem nada").
        /// </summary>
        public bool CorpoSemCabeca { get; private set; }
        Vector3 _escalaDaCabeca = Vector3.one;
        Transform _ossoEncolhido;
        bool _cabecaEncolhida;
        /// <summary>O olho da primeira pessoa com o corpo à mostra fica pelo menos isto acima e na frente do pescoço (m).</summary>
        const float OlhoAcimaDoPescoco = 0.15f, OlhoNaFrenteDoPescoco = 0.07f;

        public void CorpoNoVeiculo(bool sim)
        {
            if (sim)
            {
                CorpoSemCabeca = true;
                CorpoDoDono(true);
                return;
            }
            if (!CorpoSemCabeca) return;
            CorpoSemCabeca = false;
            CabecaDeVolta();
            CorpoDoDono(EmTerceiraPessoa);
        }

        void CabecaDeVolta()
        {
            if (_cabecaEncolhida && _ossoEncolhido != null) _ossoEncolhido.localScale = _escalaDaCabeca;
            _cabecaEncolhida = false;
            _ossoEncolhido = null;
        }

        /// <summary>O pescoço (o pai da cabeça), se o corpo tiver um; senão, null.</summary>
        Transform OssoDoPescoco => ossoCabeca != null && ossoCabeca.parent != null && ossoCabeca.parent != ossoPeito ? ossoCabeca.parent : null;

        /// <summary>
        /// O olho da primeira pessoa com o corpo à mostra (<see cref="CorpoSemCabeca"/>), no espaço da raiz: nunca abaixo nem
        /// atrás do pescoço. Assim a gola fica embaixo e atrás da câmera, e o olhar para baixo vê o peito e os braços por
        /// fora, sem entrar no corpo (o tronco mexe com a animação: o remar, a moto inclinada).
        /// </summary>
        public Vector3 OlhoForaDoCorpo(Vector3 olhoNaRaiz)
        {
            var pescoco = OssoDoPescoco;
            if (pescoco == null) pescoco = ossoCabeca;
            if (pescoco == null) return olhoNaRaiz;
            Vector3 p = transform.InverseTransformPoint(pescoco.position);
            olhoNaRaiz.y = Mathf.Max(olhoNaRaiz.y, p.y + OlhoAcimaDoPescoco);
            olhoNaRaiz.z = Mathf.Max(olhoNaRaiz.z, p.z + OlhoNaFrenteDoPescoco);
            return olhoNaRaiz;
        }

        public void CorpoDoDono(bool visivel)
        {
            if (_corpoDoDonoAplicado && CorpoVisivelNoDono == visivel) return;
            _corpoDoDonoAplicado = true;
            CorpoVisivelNoDono = visivel;
            foreach (var r in renderersCorpo)
            {
                if (r == null) continue;
                r.shadowCastingMode = visivel ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                r.renderingLayerMask = visivel ? 1u : MascaraDoDono;
            }
            if (naMaoCorpo != null)
                foreach (var a in naMaoCorpo)
                    if (a != null)
                        foreach (var r in PecasDaArma.Renderers(a))
                        {
                            r.enabled = visivel;
                            r.renderingLayerMask = MascaraDoDono;
                        }
            // as peças do fumo, como o que está na mão (o cigarro do corpo fica na frente do rosto)
            if (renderersDoFumo != null)
                foreach (var r in renderersDoFumo)
                    if (r != null)
                    {
                        r.enabled = visivel;
                        r.renderingLayerMask = MascaraDoDono;
                    }
            var pneu = Inventario != null ? Inventario.PneuNoCorpo : null;
            if (pneu != null)
            {
                pneu.shadowCastingMode = visivel ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                pneu.renderingLayerMask = visivel ? 1u : MascaraDoDono;
            }
        }

        public override void OnNetworkSpawn()
        {
            Todos.Add(this);
            _corpoFisico = GetComponent<CharacterController>();
            Lanterna = GetComponent<LanternaJogador>();
            Vida = GetComponent<VidaJogador>();
            Fumo = GetComponent<FumoJogador>();
            Inventario = GetComponent<InventarioJogador>();
            Passageiro = GetComponent<PassageiroDoCarro>();

            bool dono = IsOwner;
            if (dono) Personagem.Value = (byte)Personagens.Escolhido;
            Personagem.OnValueChanged += AoMudarPersonagem;
            Aparencia.OnValueChanged += AoMudarAparencia;
            cameraMundo.enabled = dono;
            cameraBracos.enabled = dono;
            ouvido.enabled = dono;
            bracos.SetActive(dono);
            GetComponent<MovimentoPrimeiraPessoa>().enabled = dono;
            GetComponent<ArmaJogador>().enabled = dono;
            Inventario.Mudou += AplicarNaMao;
            Inventario.RoupasMudaram += AplicarAparencia;
            if (GetComponent<TacoNaMao>() == null) gameObject.AddComponent<TacoNaMao>();
            if (dono)
            {
                // o nome em cima da cabeça dos outros e as roupas nos braços de primeira pessoa (as partes 3 e 5 do
                // Plano_Personagem.md da pasta de arte)
                if (GetComponent<NomesDosJogadores>() == null) gameObject.AddComponent<NomesDosJogadores>();
                if (GetComponent<RoupasNosBracos>() == null) gameObject.AddComponent<RoupasNosBracos>();
                // a mão esquerda na arma de fogo, por cima da animação dos braços, e os braços desenhados perto da origem
                // (sem o tremor da precisão longe do meio do mapa) (03/11/2026)
                if (GetComponent<MaoNaArma>() == null) gameObject.AddComponent<MaoNaArma>();
                if (GetComponent<BracosNaOrigem>() == null) gameObject.AddComponent<BracosNaOrigem>();
            }
            AplicarPersonagem(Personagem.Value);
            if (dono)
            {
                // a chave do personagem escolhido no menu (sem um, a do jogador nesta partida)
                var escolhido = Salvar.EscolhaDoPersonagem.Atual;
                Chave.Value = escolhido != null ? escolhido.Chave : unchecked((OwnerClientId + 1) * 0x9E3779B97F4A7C15UL) | 1UL;
                // o personagem: o salvo entra direto; um novo passa pela tela de criar (o robô de teste sai sorteado)
                if (MenuOnline.Bot)
                {
                    var semente = new System.Random(unchecked((int)OwnerClientId * 7919 + System.Environment.TickCount));
                    Criar(AparenciaDoJogador.Sortear(semente, $"Robô {OwnerClientId + 1}"));
                }
                else if (escolhido != null && !Salvar.EscolhaDoPersonagem.Novo) Criar(escolhido.Aparencia());
                else if (!Criado && GetComponent<TelaDeCriarPersonagem>() != null) GetComponent<TelaDeCriarPersonagem>().Abrir();
                Local = this;
                CameraDoMenu.Ligar(false);
                var ponto = PontosDeNascimento.Ponto(OwnerClientId);
                GetComponent<MovimentoPrimeiraPessoa>().Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
            }
            _posJanela = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            Todos.Remove(this);
            // quem sai da partida leva as coisas (o personagem é dele: 02/11/2026, o save); o mundo lembra onde ele estava
            if (IsServer) Salvar.Salvamento.LembrarPosicao(this);
            Personagem.OnValueChanged -= AoMudarPersonagem;
            Aparencia.OnValueChanged -= AoMudarAparencia;
            Inventario.Mudou -= AplicarNaMao;
            Inventario.RoupasMudaram -= AplicarAparencia;
            if (Local == this)
            {
                Local = null;
                CameraDoMenu.Ligar(true);
            }
        }

        void AoMudarPersonagem(byte antes, byte depois) => AplicarPersonagem(depois);

        void AoMudarAparencia(AparenciaDoJogador antes, AparenciaDoJogador depois)
        {
            // criado o personagem, o anfitrião veste as roupas do começo no inventário (uma vez por partida)
            if (IsServer && depois.definida != 0 && Inventario != null) Inventario.VestirOComeco(depois);
            AplicarAparencia();
        }

        /// <summary>
        /// Veste o corpo pela aparência (o corpo, a pele, o cabelo e a barba) e pelas roupas vestidas do inventário e, no
        /// dono, pinta os braços de primeira pessoa com a pele dela. Sem aparência ainda (antes de o dono escrever), o
        /// corpo fica como está.
        /// </summary>
        void AplicarAparencia()
        {
            var a = Aparencia.Value;
            if (Vestimenta == null) return;
            // antes de criar o personagem, nada aparece (nem o corpo, nem o que está na mão)
            var roupas = Vestimenta.transform.Find("Roupas");
            if (roupas != null) roupas.gameObject.SetActive(a.definida != 0);
            if (a.definida == 0)
            {
                EsconderNaMao(true);
                return;
            }
            if (_naMaoEscondida && (Passageiro == null || !Passageiro.NoCarro)) EsconderNaMao(false);
            var catalogo = Vestimenta.Catalogo;
            Vestimenta.corpo = a.Corpo;
            Vestimenta.pele = a.Pele(catalogo);
            Vestimenta.Vestir(a.Pecas(catalogo, Inventario != null && Inventario.IsSpawned ? Inventario.Roupas.Value : default));
            if (IsOwner) PintarBracos(Vestimenta.pele);
        }

        /// <summary>A cor da pele do corpo do personagem principal (a da textura dele, que os braços de primeira pessoa usam).</summary>
        public const string PeleDaTextura = "#D08A63";

        /// <summary>
        /// Os braços de primeira pessoa com a pele escolhida: eles são só pele (a textura do personagem principal), então a
        /// cor base do material deles vira a pele pedida dividida pela da textura (na luz linear).
        /// </summary>
        void PintarBracos(string pele)
        {
            var pedida = ZombieCityCleaner.Roupas.CatalogoDasRoupas.Cor(pele).linear;
            var original = ZombieCityCleaner.Roupas.CatalogoDasRoupas.Cor(PeleDaTextura).linear;
            var tinta = new Color(pedida.r / Mathf.Max(original.r, 1e-4f), pedida.g / Mathf.Max(original.g, 1e-4f),
                                  pedida.b / Mathf.Max(original.b, 1e-4f), 1f).gamma;
            var bloco = new MaterialPropertyBlock();
            foreach (var r in pintaveis)
            {
                if (r == null) continue;
                r.GetPropertyBlock(bloco, 0);
                bloco.SetColor("_BaseColor", tinta);
                r.SetPropertyBlock(bloco, 0);
            }
        }

        /// <summary>
        /// Liga o corpo do personagem (e desliga os outros), passa a usar o Animator, os ossos, o que está na mão e os
        /// controladores dele, e pinta os braços de primeira pessoa com a pele dele.
        /// </summary>
        void AplicarPersonagem(int indice)
        {
            if (corpos != null && corpos.Length > 0)
            {
                indice = Mathf.Clamp(indice, 0, corpos.Length - 1);
                var c = corpos[indice];
                for (int k = 0; k < corpos.Length; k++) corpos[k].gameObject.SetActive(k == indice);
                animadorCorpo = c.animador;
                renderersCorpo = c.renderers;
                ossoPeito = c.ossoPeito;
                ossoCabeca = c.ossoCabeca;
                naMaoCorpo = c.naMao;
                bocasCorpo = c.bocas;
                controladoresCorpo = c.controladores;
                controladorDasMaos = c.controladorDasMaos;
                controladoresDosItens = c.controladoresDosItens;
                controladorDoTaco = c.controladorDoTaco;
                tacoCorpo = c.taco;
                pontaDoTacoCorpo = c.pontaDoTaco;
                controladorDoMaco = c.controladorDoMaco;
                controladorDoCigarro = c.controladorDoCigarro;
                fumoCorpo = c.fumo;
                renderersDoFumo = c.renderersDoFumo;
                Vestimenta = c.vestimenta;
            }
            if (IsOwner)
            {
                // o corpo novo começa na primeira pessoa (a câmera de terceira pessoa mostra de novo no próximo quadro)
                _corpoDoDonoAplicado = false;
                CorpoDoDono(false);
            }
            else
            {
                foreach (var r in renderersCorpo) r.shadowCastingMode = ShadowCastingMode.On;
                // a arma da mão dele fora da luz e da sombra da lanterna dele (veja MascaraDaArmaDosOutros)
                if (naMaoCorpo != null)
                    foreach (var a in naMaoCorpo)
                        if (a != null)
                            foreach (var r in PecasDaArma.Renderers(a)) r.renderingLayerMask = MascaraDaArmaDosOutros;
            }
            AplicarCor(indice);
            AplicarAparencia();
            _naMaoAplicado = (Item)255;   // o corpo novo pega a arma da mão e o controlador dela
            if (Inventario != null) AplicarNaMao();
            CorpoMudou?.Invoke();
        }

        void AplicarCor(int indice)
        {
            if (variacoes == null || variacoes.Length == 0) return;
            var material = variacoes[Mathf.Clamp(indice, 0, variacoes.Length - 1)];
            foreach (var r in pintaveis)
            {
                var mats = r.sharedMaterials;
                mats[0] = material;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// O que está na mão: o modelo no corpo, as animações do corpo (as da pistola, as de arma longa ou, sem arma, as
        /// dos punhos) e, no dono, o de primeira pessoa (que também muda quando a mão passa para outra arma do mesmo
        /// tipo: cada arma da mochila tem o seu pente).
        /// </summary>
        /// <summary>A chama e a luz da tocha no corpo: somem com a tocha apagada pela chuva (05/11/2026).</summary>
        void FogoDaTochaNoCorpo()
        {
            if (Inventario == null || naMaoCorpo == null) return;
            int m = Itens.MaoDe(Item.Tocha);
            if (m < 0 || m >= naMaoCorpo.Length || naMaoCorpo[m] == null) return;
            var peca = naMaoCorpo[m].GetComponentInChildren<MeshFilter>(true);
            var fogo = peca != null ? peca.transform.Find("Fogo") : null;
            if (fogo == null) return;
            bool acesa = !Inventario.TochaDaMaoApagada;
            if (fogo.gameObject.activeSelf != acesa) fogo.gameObject.SetActive(acesa);
        }

        void AplicarNaMao()
        {
            var item = Inventario.NaMao;
            ushort id = Inventario.IdNaMao;
            FogoDaTochaNoCorpo();   // a tocha apagada pela chuva (ou reacesa) muda o item sem mudar o que está na mão
            if (item == _naMaoAplicado && (id == _idAplicado || !IsOwner)) return;
            _naMaoAplicado = item;
            _idAplicado = id;
            int m = Itens.MaoDe(item);
            for (int i = 0; i < naMaoCorpo.Length; i++) naMaoCorpo[i].SetActive(i == m && !_naMaoEscondida);
            // os do saque vão com a malha deles no lugar da lata ou da garrafa
            if (m >= 0 && m < naMaoCorpo.Length) ItemNaMaoProprio.Aplicar(naMaoCorpo[m], item);
            // a tocha: a chama e a luz na ponta (a malha dela tem a ponta em +Z)
            if (item == Item.Tocha && m >= 0 && m < naMaoCorpo.Length && Loot.ConstrucoesNoMundo.Instancia != null)
            {
                var peca = naMaoCorpo[m].GetComponentInChildren<MeshFilter>(true);
                if (peca != null && peca.sharedMesh != null && peca.transform.Find("Fogo") == null)
                {
                    var b = peca.sharedMesh.bounds;
                    // a luz no mundo é esta, também no dono (03/11/2026: os braços de primeira pessoa ficam perto da origem,
                    // BracosNaOrigem, e a luz da tocha deles só os ilumina; antes, no dono, era ela que iluminava em volta)
                    var fogo = Loot.ConstrucoesNoMundo.Fogo(peca.transform, new Vector3(b.center.x, b.center.y, b.max.z - 0.03f),
                        Loot.ConstrucoesNoMundo.Instancia.materialDoFogo, 0.3f, 14f, true, naMaoCorpo[m].layer);   // a luz maior (01/11/2026: era 8 m)
                    if (fogo.TryGetComponent<Light>(out var luz)) luz.intensity = 2.4f;
                }
                FogoDaTochaNoCorpo();
            }
            AtualizarControladorDoCorpo();
            if (IsOwner) GetComponent<ArmaJogador>().Trocar(item, id);
        }

        /// <summary>
        /// O controlador do corpo para o que está na mão: o da arma, o do taco, o do maço (ou, com o cigarro aceso, o do
        /// cigarro: o FumoJogador chama de novo quando acende e quando joga fora), o do item ou o dos punhos.
        /// </summary>
        public void AtualizarControladorDoCorpo()
        {
            if (animadorCorpo == null || Inventario == null) return;
            var item = Inventario.NaMao;
            int k = Itens.MaoDe(item) - Armas.Quantas;
            var controlador = Itens.EhArma(item) ? controladoresCorpo[(int)Itens.Arma(item)]
                : Itens.EhTaco(item) && controladorDoTaco != null ? controladorDoTaco
                : Itens.EhFumo(item) && controladorDoMaco != null ? (Fumo != null && Fumo.ComOCigarroNaMao && controladorDoCigarro != null ? controladorDoCigarro : controladorDoMaco)
                : k >= 0 && controladoresDosItens != null && k < controladoresDosItens.Length && controladoresDosItens[k] != null ? controladoresDosItens[k]
                : controladorDasMaos != null ? controladorDasMaos : controladoresCorpo[0];
            if (animadorCorpo.runtimeAnimatorController != controlador) animadorCorpo.runtimeAnimatorController = controlador;
        }

        /// <summary>Esconde (sentado num carro) ou mostra de novo o que está na mão do corpo.</summary>
        public void EsconderNaMao(bool esconder)
        {
            _naMaoEscondida = esconder;
            int m = Itens.MaoDe(Inventario != null ? Inventario.NaMao : Item.Nada);
            for (int i = 0; i < naMaoCorpo.Length; i++) naMaoCorpo[i].SetActive(!esconder && i == m);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner && Visao.Value != Graficos.Visao) Visao.Value = (byte)Graficos.Visao;   // a escolha dos Gráficos, para o anfitrião
            if (IsOwner && Time.time >= _proximoTeto)
            {
                _proximoTeto = Time.time + 0.5f;
                bool teto = ZombieCityCleaner.World.Tempo.Chuva > 0.01f && ZombieCityCleaner.World.Tempo.SobTeto(transform.position);
                if (DebaixoDeTeto.Value != teto) DebaixoDeTeto.Value = teto;
            }
            // Velocidade no chão: o dono usa a do próprio controle; os outros medem a posição recebida a cada 0,1 s
            // (quadro a quadro a medida oscila, porque a posição chega em degraus).
            float medida;
            if (IsOwner && _corpoFisico != null)
                medida = new Vector3(_corpoFisico.velocity.x, 0f, _corpoFisico.velocity.z).magnitude;
            else
            {
                _janela += Time.deltaTime;
                if (_janela >= 0.1f)
                {
                    Vector3 delta = transform.position - _posJanela;
                    delta.y = 0f;
                    _medidaRemota = Mathf.Min(delta.magnitude / _janela, 6f);
                    _posJanela = transform.position;
                    _janela = 0f;
                }
                medida = _medidaRemota;
            }
            if (Passageiro != null && Passageiro.NoCarro) medida = 0f;   // no carro, ele não anda nem corre (a fome, os zumbis)
            _velocidade = Mathf.Lerp(_velocidade, medida, 1f - Mathf.Exp(-12f * Time.deltaTime));
            animadorCorpo.SetFloat(SpeedId, _velocidade);
            animadorCorpo.SetBool(AimingId, Mirando.Value);
            // abaixado: o corpo desce e sobe em ~0,25 s (só no controlador que tem o parâmetro)
            _agachado = Mathf.MoveTowards(_agachado, Agachado.Value ? 1f : 0f, Time.deltaTime / 0.25f);
            if (TemAgachado(animadorCorpo)) animadorCorpo.SetFloat(AgachadoId, _agachado);
        }

        static readonly int AgachadoId = Animator.StringToHash("Agachado");
        float _agachado;
        Animator _animadorVisto;
        RuntimeAnimatorController _controladorVisto;
        bool _temAgachado;

        // o controlador do corpo tem o parâmetro "Agachado"? (lido de novo quando o Animator ou o controlador mudam; sem
        // ele, o SetFloat avisaria no Console a cada quadro)
        bool TemAgachado(Animator a)
        {
            if (a == null || !a.isInitialized) return false;
            if (a == _animadorVisto && a.runtimeAnimatorController == _controladorVisto) return _temAgachado;
            _animadorVisto = a;
            _controladorVisto = a.runtimeAnimatorController;
            _temAgachado = false;
            foreach (var p in a.parameters)
                if (p.nameHash == AgachadoId && p.type == AnimatorControllerParameterType.Float) _temAgachado = true;
            return _temAgachado;
        }

        /// <summary>O corpo não acompanha o olhar (pulando a janela: a animação é do corpo todo).</summary>
        public bool SemOlhar { get; set; }

        void LateUpdate()
        {
            if (!IsSpawned || SemOlhar) return;
            // O corpo acompanha o olhar: metade no peito, metade na cabeça, por cima da animação.
            float pitch = Pitch.Value;
            Vector3 eixo = transform.right;
            // na moto e no caiaque, na primeira pessoa: o corpo sem a cabeça e sem o pescoço, e o peito sem dobrar com o
            // olhar, só no dono (CorpoNoVeiculo)
            bool semCabeca = CorpoSemCabeca && IsOwner && ossoCabeca != null && !EmTerceiraPessoa;
            if (ossoPeito != null && !semCabeca) ossoPeito.rotation = Quaternion.AngleAxis(pitch * 0.5f, eixo) * ossoPeito.rotation;
            if (ossoCabeca != null) ossoCabeca.rotation = Quaternion.AngleAxis(pitch * 0.5f, eixo) * ossoCabeca.rotation;
            if (semCabeca)
            {
                var osso = OssoDoPescoco != null ? OssoDoPescoco : ossoCabeca;
                if (_cabecaEncolhida && _ossoEncolhido != osso) CabecaDeVolta();   // (o corpo mudou)
                if (!_cabecaEncolhida)
                {
                    _ossoEncolhido = osso;
                    _escalaDaCabeca = osso.localScale;
                    _cabecaEncolhida = true;
                }
                osso.localScale = Vector3.one * 0.001f;
            }
            else if (_cabecaEncolhida) CabecaDeVolta();
        }

        /// <summary>O que o tiro acertou, para os outros mostrarem o efeito certo.</summary>
        public enum TipoDeAcerto : byte { Nada, Cenario, Carne, Outro }

        /// <summary>Onde um tiro (ou um chumbo da espingarda) parou, e no quê.</summary>
        public struct Impacto : INetworkSerializable
        {
            public TipoDeAcerto tipo;
            public Vector3 ponto, normal;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref tipo);
                s.SerializeValue(ref ponto);
                s.SerializeValue(ref normal);
            }
        }

        // o Tiro abafado (06/11/2026, o Assassino): o barulho do tiro chega 15% menos longe por ponto (-1: o normal)
        // o raio do barulho do meu tiro (-1: o do jogo): o Tiro abafado diminui; as armas de sucata (09/11/2026) fazem mais
        float RaioDoMeuTiro(TipoDeArma arma)
        {
            float vezes = Armas.Dados(arma).barulho;
            bool abafado = Habilidades.Tem(this, Habilidade.TiroAbafado);
            if ((!abafado && vezes == 1f) || ZombieManager.Instancia == null) return -1f;
            return ZombieManager.Instancia.ajustes.barulhoDoTiro * vezes * (abafado ? 1f - 0.15f * Habilidades.Pontos(this, Habilidade.TiroAbafado) : 1f);
        }

        /// <summary>Chamado pelo dono ao atirar: o corpo dele (a sombra) e o dos outros mostram o tiro (e de que arma).</summary>
        public void AnunciarTiro(TipoDeArma arma, Impacto[] impactos)
        {
            animadorCorpo.SetTrigger(FireId);
            if (IsServer)
            {
                ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this, RaioDoMeuTiro(arma));
                Vehicles.PecasDosCarrosDoMundo.Instancia?.TiroNosCarros(impactos);   // o tiro num carro com alarme dispara
                Sobreviventes.SobreviventesDoMundo.OuvirTiro(transform.position, this);   // o grupo de sobreviventes perto ouve
            }
            TiroRpc((byte)arma, impactos);
        }

        /// <summary>
        /// O dono acertou um sobrevivente (02/11/2026): o anfitrião confere (ele existe e está perto do ponto) e tira a vida
        /// (a parte do corpo decide: o tiro na cabeça mata).
        /// </summary>
        public void AcertarSobrevivente(Sobreviventes.Sobrevivente s, Sobreviventes.RegiaoDoSobrevivente regiao, float dano, Vector3 ponto, bool tiro)
        {
            if (s == null) return;
            if (IsServer) s.LevarTiro(regiao, dano, ponto, this, tiro);
            else AcertoNoSobreviventeRpc(s.NetworkObject, (byte)regiao, dano, ponto, tiro);
        }

        [Rpc(SendTo.Server)]
        void AcertoNoSobreviventeRpc(NetworkObjectReference quem, byte regiao, float dano, Vector3 ponto, bool tiro, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !quem.TryGet(out var no)) return;
            var s = no.GetComponent<Sobreviventes.Sobrevivente>();
            if (s == null || (s.transform.position + Vector3.up * 1f - ponto).sqrMagnitude > 2.5f * 2.5f) return;
            s.LevarTiro((Sobreviventes.RegiaoDoSobrevivente)Mathf.Min(regiao, (byte)2), Mathf.Clamp(dano, 0f, 500f), ponto, this, tiro);
        }

        /// <summary>
        /// O dono acertou outro jogador (03/11/2026, o fogo amigo, escolhido ao criar o mundo): o anfitrião confere (o fogo
        /// amigo ligado, o outro vivo e perto do ponto) e tira a vida, com o nome de quem atirou na tela de morte.
        /// </summary>
        public void AcertarJogador(JogadorRede alvo, float dano, Vector3 ponto, TipoDeGolpe tipo = TipoDeGolpe.Tiro, ParteDoCorpo parte = PartesDoCorpo.Sortear)
        {
            if (alvo == null) return;
            if (IsServer) FerirJogador(alvo, dano, ponto, tipo, parte);
            else AcertoNoJogadorRpc(alvo.NetworkObject, dano, ponto, (byte)tipo, (byte)parte);
        }

        [Rpc(SendTo.Server)]
        void AcertoNoJogadorRpc(NetworkObjectReference quem, float dano, Vector3 ponto, byte tipo, byte parte, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !quem.TryGet(out var no)) return;
            FerirJogador(no.GetComponent<JogadorRede>(), dano, ponto, (TipoDeGolpe)tipo, (ParteDoCorpo)parte);
        }

        // no anfitrião: o fogo amigo (05/11/2026: o tiro, o corte e a pancada abrem feridas também entre jogadores)
        void FerirJogador(JogadorRede alvo, float dano, Vector3 ponto, TipoDeGolpe tipo, ParteDoCorpo parte)
        {
            if (tipo != TipoDeGolpe.Tiro && tipo != TipoDeGolpe.Corte && tipo != TipoDeGolpe.Pancada) tipo = TipoDeGolpe.Pancada;
            if (!PartesDoCorpo.Valida(parte) && alvo != null) parte = PartesDoCorpo.PeloPonto(alvo.transform, ponto);
            var m = World.MundoDaPartida.Instancia;
            if (alvo == null || alvo == this || !alvo.AlvoValido || alvo.Vida == null || m == null || !m.FogoAmigo || !AlvoValido) return;
            if ((alvo.transform.position + Vector3.up * 1f - ponto).sqrMagnitude > 2.5f * 2.5f) return;
            string nome = Aparencia.Value.nome.ToString();
            alvo.Vida.Dano(Mathf.Clamp(dano, 0f, 200f), transform.position + Vector3.up * 1.5f, $"Morto por {(string.IsNullOrEmpty(nome) ? "outro jogador" : nome)}.", tipo, parte);
        }

        [Rpc(SendTo.NotMe)]
        void TiroRpc(byte qual, Impacto[] impactos)
        {
            TirosRecebidos++;
            // no anfitrião, o tiro de um amigo chama os zumbis em volta dele (e dispara o alarme do carro em que pegou)
            var arma = (TipoDeArma)Mathf.Min(qual, Armas.Quantas - 1);
            if (IsServer)
            {
                ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this, RaioDoMeuTiro(arma));
                Vehicles.PecasDosCarrosDoMundo.Instancia?.TiroNosCarros(impactos);
                Sobreviventes.SobreviventesDoMundo.OuvirTiro(transform.position, this);
            }
            animadorCorpo.SetTrigger(FireId);
            var boca = bocasCorpo[(int)arma];
            Efeitos.Clarao(boca, 0);
            Efeitos.Som(Efeitos.Tiros[(int)arma], boca.position, 1f, true, 150f);   // tiro se ouve longe
            if (arma == TipoDeArma.Espingarda) StartCoroutine(SomDepois(boca, Efeitos.Bomba, 0.33f, 0.8f));
            if (Armas.Dados(arma).ferrolho) StartCoroutine(SomDepois(boca, Efeitos.Ferrolho, 0.42f, 0.7f));
            foreach (var i in impactos)
            {
                if (i.tipo == TipoDeAcerto.Carne) Sangue.Respingo(i.ponto, i.normal);
                else if (i.tipo != TipoDeAcerto.Nada) Efeitos.Impacto(i.ponto, i.normal);
                if (i.tipo == TipoDeAcerto.Cenario) Sangue.FuroDeTiro(i.ponto, i.normal);
            }
        }

        /// <summary>Chamado pelo dono ao socar: o corpo dele (a sombra) e o dos outros mostram o soco, e os outros ouvem o vento.</summary>
        public void AnunciarSoco()
        {
            animadorCorpo.SetTrigger(FireId);
            SocoRpc();
        }

        [Rpc(SendTo.NotMe)]
        void SocoRpc()
        {
            animadorCorpo.SetTrigger(FireId);
            Efeitos.Som(Efeitos.SocoVento, transform.position + Vector3.up * 1.4f, 0.5f, true, 15f);
        }

        /// <summary>Chamado pelo dono quando o soco pega: os outros ouvem a pancada (na carne, com um pouco de sangue, ou na parede).</summary>
        public void AnunciarPancada(Vector3 ponto, bool carne) => PancadaRpc(ponto, carne);

        [Rpc(SendTo.NotMe)]
        void PancadaRpc(Vector3 ponto, bool carne)
        {
            if (carne)
            {
                Efeitos.Som(Efeitos.SocoCarne, ponto, 0.8f, true, 20f);
                Sangue.Respingo(ponto, (transform.position + Vector3.up * 1.5f - ponto).normalized);
            }
            else Efeitos.Som(Efeitos.SocoParede, ponto, 0.6f, true, 15f);
        }

        IEnumerator SomDepois(Transform boca, AudioClip clip, float espera, float volume)
        {
            yield return new WaitForSeconds(espera);
            if (boca != null) Efeitos.Som(clip, boca.position, volume, true, 25f);
        }

        /// <summary>
        /// O dono acertou um zumbi (a cópia dele, ou o zumbi de verdade no anfitrião). Quem decide o dano é o anfitrião:
        /// o acerto vai para ele com o Id, a geração, a região, o membro, o dano, o ponto e se o corpo deve ser
        /// arremessado (a espingarda de perto). A região, o membro e o arremesso vão num byte só. O corte: quanto o acerto
        /// vale contra a vida de um braço ou de uma perna (o machado, 2: decepa com um golpe; o resto, 1).
        /// </summary>
        public void AcertarZumbi(Zumbi zumbi, RegiaoDoCorpo regiao, Membro membro, float dano, Vector3 ponto, bool arremesso, float corte = 1f)
        {
            if (IsServer) ZombieManager.Acertar(zumbi.Id, zumbi.Geracao, regiao, membro, dano, ponto, arremesso, this, corte);
            else AcertoRpc((ushort)zumbi.Id, zumbi.Geracao, (byte)((byte)regiao | ((byte)membro << 3) | (arremesso ? 128 : 0)), dano, ponto,
                           (byte)Mathf.Clamp(Mathf.RoundToInt(corte * 10f), 0, 255));
        }

        [Rpc(SendTo.Server)]
        void AcertoRpc(ushort id, byte geracao, byte parte, float dano, Vector3 ponto, byte corte, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;   // só o dono atira com este jogador
            var regiao = (RegiaoDoCorpo)(parte & 7);
            var membro = (Membro)Mathf.Min((parte >> 3) & 15, (int)Membro.CanelaD);
            ZombieManager.Acertar(id, geracao, regiao, membro, dano, ponto, (parte & 128) != 0, this, corte / 10f);
        }
    }
}
