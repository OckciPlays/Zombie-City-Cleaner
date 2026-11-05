using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O que o jogador local tem na mão: o item do atalho escolhido do <see cref="InventarioJogador"/> (uma arma, a lata
    /// de comida, a garrafa de água ou nada). Os números de cada arma estão em <see cref="Armas"/>; cada arma da mochila
    /// guarda o pente dela (trocar de arma não recarrega), e o anfitrião guarda uma cópia (largada ou guardada no painel,
    /// ela leva as balas que tinha).
    /// A recarga tira as balas das caixas de munição do calibre da arma que estão na mochila (<see cref="Calibre"/>); sem
    /// caixa, não recarrega ("sem munição 9mm"). O robô de teste não gasta munição.
    /// Com arma: clique atira (raycast do centro da tela; as automáticas atiram com o gatilho apertado e a espingarda solta
    /// vários chumbos), R recarrega (a espingarda põe cartucho a cartucho, e atirar interrompe) e o botão direito mira
    /// (aproxima a câmera e os outros veem o corpo mirando). Sem bala, o gatilho só faz clique.
    /// Com comida ou água: clique come ou bebe (o clipe Usar, com o som); no fim o item acaba e a mão volta para a última
    /// arma. Enquanto isso, nada de trocar, largar ou usar outra coisa.
    /// Com a mão vazia: os punhos em guarda, e o clique soca (<see cref="Socar"/>).
    /// Com uma arma branca (a faca e o machado; 31/10/2026, o sistema básico): o clique golpeia
    /// (<see cref="GolpearComBranca"/>), com os números dela (<see cref="Itens.Branca"/>).
    /// Mirando de perto uma coisa que se usa (<see cref="Interagivel"/>: um item no chão, uma arma do painel, a porta do
    /// bunker, um interruptor), a dica aparece e E usa.
    /// A sniper tem luneta: mirando, a câmera fecha até o campo de visão dela, os braços somem e a tela mostra a luneta
    /// (o preto em volta e o retículo); o mouse fica mais lento na proporção do aumento. Sem a luneta, ela espalha muito.
    /// Acertar um zumbi: o sangue sai na hora aqui, e o acerto (zumbi, região e ponto) vai para o anfitrião, que tira a
    /// vida. O tiro atravessa até dois braços ou mãos e segue para o que estiver atrás. No cenário, faíscas e a marca do
    /// tiro.
    /// </summary>
    public class ArmaJogador : MonoBehaviour
    {
        public JogadorRede rede;
        public MovimentoPrimeiraPessoa movimento;

        [Header("Primeira pessoa (um modelo por coisa na mão, na ordem de Itens.Modelos: as armas, a lata e a garrafa; e os punhos)")]
        public GameObject[] modelos;
        public Animator[] animadores;
        public Transform[] bocas;

        [Header("Mira")]
        public float fovNormal = 70f;
        public float fovMirando = 58f;
        [Tooltip("Até onde a mira pega o que se usa com E: uma arma do painel, a porta, um interruptor (m).")]
        public float alcanceDaMao = 2.2f;

        [Header("Soco (a mão vazia)")]
        [Tooltip("Tempo entre um soco e o próximo (s); o clipe tem 0,42 s.")]
        public float intervaloDoSoco = 0.42f;
        [Tooltip("Até onde o punho chega, do olho (m).")]
        public float alcanceDoSoco = 1.15f;
        [Tooltip("A grossura do golpe (o raio da esfera que procura o alvo, m).")]
        public float raioDoSoco = 0.16f;
        [Tooltip("Dano de um soco (a vida do Oco é 100; a cabeça vale 3 vezes): 6, 17 socos no corpo e 6 na cabeça.")]
        public float danoDoSoco = 6f;

        public int Municao { get; private set; }
        /// <summary>Olhando pela luneta (a câmera já fechou): os braços somem e a tela mostra a luneta.</summary>
        public bool NaLuneta { get; private set; }
        public bool Recarregando { get; private set; }
        /// <summary>A última arma que esteve na mão (a da mão, com arma).</summary>
        public TipoDeArma Atual { get; private set; }
        public DadosDaArma Dados => Armas.Dados(Atual);
        public Item NaMao { get; private set; }

        /// <summary>
        /// A chama da tocha nos braços: some com a tocha apagada pela chuva (05/11/2026) e volta quando ela reacende.
        /// </summary>
        void FogoDaTocha()
        {
            if (NaMao != Item.Tocha || bocas == null) return;
            int m = Itens.ModeloNaMao(Item.Tocha);
            if (m < 0 || m >= bocas.Length || bocas[m] == null) return;
            var fogo = bocas[m].Find("Fogo");
            bool acesa = Inventario == null || !Inventario.TochaDaMaoApagada;
            if (fogo != null && fogo.gameObject.activeSelf != acesa) fogo.gameObject.SetActive(acesa);
        }
        public bool ComArma => Itens.EhArma(NaMao);
        /// <summary>Comendo ou bebendo (o clipe Usar).</summary>
        public bool Usando { get; private set; }
        /// <summary>
        /// Montando uma construção (<see cref="MontadorDeConstrucao"/>): o clique é dele, a arma não atira nem recarrega e a
        /// mochila não troca nem larga. Vale até o quadro seguinte ao que parou (o clique que montou não atira).
        /// </summary>
        public bool Montando
        {
            get => _montando || Time.frameCount <= _montandoAte;
            set
            {
                if (_montando && !value) _montandoAte = Time.frameCount + 1;
                _montando = value;
            }
        }
        bool _montando;
        int _montandoAte = -1;
        public InventarioJogador Inventario { get; private set; }
        /// <summary>As mãos estão ocupadas com um pneu: sem arma, sem soco, sem comer (o E ainda funciona).</summary>
        public bool MaosOcupadas => Inventario != null && Inventario.CarregandoPneu;
        bool _ocupadasAntes;
        /// <summary>O Id da última arma que esteve na mão (a mão volta para ela depois de comer ou de se tratar).</summary>
        public ushort UltimaArma => _ultimaArma;

        /// <summary>
        /// Fazendo um curativo (<see cref="Tratamento"/>, 05/11/2026): nada de atirar, recarregar, golpear, socar, mirar,
        /// usar o E nem trocar de item.
        /// </summary>
        static bool Tratando => Tratamento.Local != null && Tratamento.Local.Ativo;

        /// <summary>O corpo do jogador (o braço quebrado, a mira que treme).</summary>
        EstadoDoCorpo Corpo => rede != null && rede.Vida != null ? rede.Vida.Corpo.Value : default;

        /// <summary>
        /// A arma de duas mãos (as longas, o machado, o bastão e a lança) com um braço quebrado ou inutilizado (05/11/2026):
        /// ela fica na mão, mas não mira, não atira nem golpeia.
        /// </summary>
        bool SemOsDoisBracos => (Itens.EhArma(NaMao) || Itens.EhBranca(NaMao)) && PartesDoCorpo.DuasMaos(NaMao) && Corpo.AlgumBracoImpede;

        float _avisoDoBracoAte;

        // o aviso do braço ruim, no máximo a cada 3 s
        void AvisarDoBraco()
        {
            if (Time.time < _avisoDoBracoAte) return;
            _avisoDoBracoAte = Time.time + 3f;
            InventarioJogador.AvisoLocal($"Com o braço assim, não dá para usar {Itens.NomeComArtigo(NaMao)}");
        }

        Animator Animador
        {
            get
            {
                int m = Itens.ModeloNaMao(NaMao);
                return m >= 0 && m < animadores.Length ? animadores[m] : null;
            }
        }

        Transform Boca => bocas[(int)Atual];

        // os estados da camada "Ações" do Jogador_FP.controller: o código toca cada um do começo (no automático, um
        // tiro recomeça o anterior)
        const int CamadaAcoes = 1;
        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int DryFireId = Animator.StringToHash("DryFire");
        static readonly int ReloadId = Animator.StringToHash("Reload");
        static readonly int SacarId = Animator.StringToHash("Sacar");
        static readonly int VazioId = Animator.StringToHash("Vazio");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        // o peso da camada "Ações": inteiro durante uma ação, e descendo até zero depois (veja PesoDasAcoes)
        float _pesoDasAcoes;
        // o tiro não pega a cápsula do zumbi de perto (camada Ignore Raycast; quem leva o tiro são as hitboxes) nem os
        // corpos caídos
        const int MascaraDoTiro = ~((1 << 2) | (1 << CorpoDeZumbi.Camada));
        static readonly Collider[] _encostados = new Collider[8];

        InputAction _atirar, _recarregar, _mirar, _interagir;
        CharacterController _corpo;
        Coroutine _recarga;
        float _proximoTiro, _marcaAte;
        bool _marcaNaCabeca, _mirandoAntes, _socoDireito = true;
        static Texture2D _texturaDaLuneta;
        Interagivel _alvo;
        string _dica;
        /// <summary>O E tem um alvo agora (a porta, o item, a janela): o curativo no amigo cede a vez (TratarOAmigo).</summary>
        public bool TemAlvoDoE => _alvo != null;
        // o pente de cada arma da mochila, pelo Id dela (a que nunca esteve na mão: o que o anfitrião guardou)
        readonly System.Collections.Generic.Dictionary<ushort, int> _pentes = new System.Collections.Generic.Dictionary<ushort, int>();
        ushort _id, _ultimaArma;
        // o aviso de sem munição, embaixo das balas
        float _semMunicaoAte, _emperrouAte;

        void Awake()
        {
            Municao = Dados.capacidade;
            Inventario = GetComponent<InventarioJogador>();
            _corpo = GetComponent<CharacterController>();
            var acoes = InputSystem.actions;
            _atirar = acoes.FindAction("Player/Attack", true);
            _recarregar = acoes.FindAction("Player/Reload", true);
            _mirar = acoes.FindAction("Player/Aim", true);
            _interagir = acoes.FindAction("Player/Interact", true);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Recarregando = false;
            Usando = false;
            SairDaLuneta();
        }

        void SairDaLuneta()
        {
            NaLuneta = false;
            if (rede != null && rede.IsOwner) AmbienteDoDia.NaLuneta = false;   // a neblina é da máquina de quem joga
            if (rede != null && rede.cameraBracos != null) rede.cameraBracos.enabled = rede.IsOwner;
            if (movimento != null) movimento.MultiplicadorDoOlhar = 1f;
        }

        /// <summary>
        /// O que está na mão mudou (no começo, trocando de atalho, pegando ou largando): mostra o modelo dele, sacando. A
        /// arma que sai guarda o pente; a que entra volta com o pente que tinha (ou cheia, se nunca esteve na mão).
        /// </summary>
        public void Trocar(Item novo, ushort id)
        {
            if (ComArma && _id != 0)
            {
                _pentes[_id] = Municao;
                Inventario.GuardarPente(_id, Municao);   // a cópia do anfitrião (vai junto se ela for largada ou guardada)
            }
            StopAllCoroutines();
            Recarregando = false;
            Usando = false;
            _recarga = null;
            SairDaLuneta();
            NaMao = novo;
            _id = id;
            int m = Itens.ModeloNaMao(novo);   // a mão vazia mostra os punhos
            bool ocupadas = MaosOcupadas;   // com o pneu nas mãos, os braços somem
            for (int i = 0; i < modelos.Length; i++) modelos[i].SetActive(i == m && !ocupadas);
            // os do saque vão com a malha deles no lugar da lata ou da garrafa
            if (m >= 0 && m < modelos.Length) ItemNaMaoProprio.Aplicar(modelos[m], novo);
            // a tocha: a chama na ponta (a boca dos braços dela), só as chamas (a luz é a do corpo)
            // e, no dono, a luz dela ali, na frente dos braços (a do corpo, perto do rosto, estourava os braços de laranja)
            if (novo == Item.Tocha && m >= 0 && m < bocas.Length && bocas[m] != null && bocas[m].Find("Fogo") == null && Loot.ConstrucoesNoMundo.Instancia != null)
            {
                var fogo = Loot.ConstrucoesNoMundo.Fogo(bocas[m], Vector3.zero, Loot.ConstrucoesNoMundo.Instancia.materialDoFogo, 0.28f, 14f, false, JogadorRede.CamadaBracos);   // a luz maior (01/11/2026: era 8 m)
                fogo.GetComponent<Light>().intensity = 2.1f;
            }
            FogoDaTocha();
            if (ComArma)
            {
                Atual = Itens.Arma(novo);
                _ultimaArma = id;
                Municao = PenteGuardado(id, novo);
            }
            if (m >= 0) Acao(SacarId);
            _proximoTiro = Time.time + 0.4f;
        }

        /// <summary>
        /// As balas no pente de uma arma da mochila (a que nunca esteve na mão nesta máquina: o que o anfitrião guardou).
        /// </summary>
        public int MunicaoDe(ushort id, Item arma)
        {
            if (!Itens.EhArma(arma)) return 0;
            return id == _id && ComArma && NaMao == arma ? Municao : PenteGuardado(id, arma);
        }

        // o pente de uma arma que não está na mão: o que esta máquina guardou, ou a cópia do anfitrião
        int PenteGuardado(ushort id, Item arma)
        {
            int cap = Armas.Dados(Itens.Arma(arma)).capacidade;
            if (_pentes.TryGetValue(id, out int pente)) return Mathf.Min(pente, cap);
            return Inventario != null && Inventario.Achar(id, out var it) ? Mathf.Min(it.qtd, cap) : cap;
        }

        /// <summary>As balas do calibre da arma da mão nas caixas da mochila (o robô de teste e o vídeo das armas têm munição sem fim).</summary>
        public int Reserva => SemFim ? 999 : Inventario != null ? Inventario.BalasDe(Dados.calibre) : 0;

        static bool SemFim => MenuOnline.Bot || ModoVideo.MunicaoInfinita;

        // tira as balas das caixas (no anfitrião); o robô não gasta
        void GastarDaReserva(int balas)
        {
            if (!SemFim && Inventario != null) Inventario.Gastar(Dados.calibre, balas);
        }

        void Update()
        {
            bool bot = MenuOnline.Bot;
            bool controla = bot || MenuOnline.Controla;
            // o pneu nas mãos: os braços somem (e voltam sacando quando ele sai)
            bool ocupadas = MaosOcupadas;
            if (ocupadas != _ocupadasAntes)
            {
                _ocupadasAntes = ocupadas;
                if (ocupadas)
                {
                    if (ComArma && _id != 0) _pentes[_id] = Municao;
                    StopAllCoroutines();
                    Recarregando = false;
                    Usando = false;
                    _recarga = null;
                    foreach (var mo in modelos) mo.SetActive(false);
                }
                else Trocar(NaMao, _id);
            }
            bool comArma = ComArma && !ocupadas;
            // o braço quebrado não segura a arma de duas mãos (05/11/2026); fazendo um curativo, as mãos estão nele
            bool semBraco = !bot && SemOsDoisBracos, tratando = !bot && Tratando;
            var dados = Dados;
            bool luneta = comArma && dados.fovLuneta > 0f;
            // recarregando, a sniper sai da luneta (a mão está no carregador). O robô de teste, com a sniper, fica na luneta
            // (testa o nível de detalhe dos zumbis pela luneta, na rede)
            bool mirar = comArma && controla && (bot ? luneta : _mirar.IsPressed()) && !(luneta && Recarregando) && !semBraco && !tratando;
            if (rede.Mirando.Value != mirar) rede.Mirando.Value = mirar;
            var cam = rede.cameraMundo;
            float fovAlvo = mirar ? (luneta ? dados.fovLuneta : fovMirando) : fovNormal;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovAlvo, 1f - Mathf.Exp(-(luneta ? 20f : 12f) * Time.deltaTime));
            if (luneta && mirar != _mirandoAntes) Efeitos.Som(mirar ? Efeitos.LunetaEntra : Efeitos.LunetaSai, cam.transform.position, 0.6f, false);
            _mirandoAntes = mirar;
            // os braços somem quando a câmera já fechou quase toda (antes disso, a arma sobe para o rosto)
            bool naLuneta = luneta && mirar && cam.fieldOfView < dados.fovLuneta + 8f;
            if (naLuneta != NaLuneta)
            {
                NaLuneta = naLuneta;
                rede.cameraBracos.enabled = !naLuneta;
                AmbienteDoDia.NaLuneta = naLuneta;   // a neblina afina para quem olha pela luneta
            }
            if (rede.Luneta.Value != naLuneta) rede.Luneta.Value = naLuneta;
            movimento.MultiplicadorDoOlhar = luneta && mirar
                ? Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fovNormal * 0.5f * Mathf.Deg2Rad) : 1f;
            var animador = Animador;
            if (animador != null)
            {
                animador.SetFloat(SpeedId, rede.Velocidade);
                PesoDasAcoes(animador);
            }
            ProcurarAlvo(rede.Olho);
            // segurando o E (arrombar): cancela se o E soltou ou a mira saiu; no tempo, faz
            if (_segurando != null)
            {
                if (bot || !controla || _alvo != _segurando || !_interagir.IsPressed()) _segurando = null;
                else if (Time.time - _segurarDesde >= _segurarTempo)
                {
                    var s = _segurando;
                    _segurando = null;
                    s.Segurado(this);
                }
            }
            // a tocha acesa na mão queima: o desgaste sobe com o tempo (100: ela se apaga e some); a apagada pela chuva, não
            FogoDaTocha();
            if (NaMao == Item.Tocha && !bot && Inventario != null && !Inventario.TochaDaMaoApagada)
                Inventario.Desgastar(_id, Time.deltaTime / Itens.SegundosDaTochaPorDesgaste);
            if (!controla || Montando) return;
            // fazendo um curativo: nada de atirar, recarregar, golpear, socar nem usar o E (a troca e o largar, no inventário)
            if (tratando)
            {
                _segurando = null;
                return;
            }
            // mirando no que usa o clique (o macaco apoiado), o botão esquerdo é dele: não atira nem soca
            if (_alvo != null && !bot && _alvo.UsaClique(this))
            {
                if (_atirar.WasPressedThisFrame()) _alvo.Clicar(this);
                if (!Usando && _interagir.WasPressedThisFrame()) ApertouE();
                return;
            }
            if (ocupadas)
            {
                if (_alvo != null && !bot && _interagir.WasPressedThisFrame()) ApertouE();
                return;
            }

            if (comArma)
            {
                var d = Dados;
                bool apertou = bot ? Mathf.Repeat(Time.time, 1.5f) < Time.deltaTime : _atirar.WasPressedThisFrame();
                bool atirar = apertou || (!bot && d.automatica && _atirar.IsPressed());
                bool recarregar = bot ? Municao == 0 : _recarregar.WasPressedThisFrame();
                if (recarregar && !Recarregando && Municao < d.capacidade)
                {
                    if (Reserva > 0) _recarga = StartCoroutine(Recarregar());
                    else
                    {
                        _semMunicaoAte = Time.time + 1.8f;
                        Efeitos.Som(Efeitos.Clique, Boca.position, 0.5f, false);
                    }
                }
                // com o braço quebrado, a arma longa não atira (ela fica na mão, e a recarga ainda dá)
                if (semBraco && atirar)
                {
                    if (apertou) AvisarDoBraco();
                    atirar = false;
                }
                // a espingarda para de pôr cartucho para atirar
                if (atirar && Recarregando && d.cartuchoACartucho && Municao > 0) PararRecarga();
                if (atirar && !Recarregando && Time.time >= _proximoTiro)
                {
                    // no automático, o próximo tiro conta do anterior (sem perder a cadência com o tempo do quadro)
                    _proximoTiro = (Time.time - _proximoTiro > Time.deltaTime ? Time.time : _proximoTiro) + d.intervalo;
                    // a arma gasta: quebrada, não atira; muito gasta, emperra às vezes (o clique sem tiro)
                    int desgaste = bot ? 0 : Inventario.DesgasteDe(_id);
                    if (desgaste >= 100 || (Municao > 0 && desgaste >= Itens.DesgasteQueEmperra
                                            && Random.value < (desgaste - Itens.DesgasteQueEmperra) / (100f - Itens.DesgasteQueEmperra) * 0.3f))
                    {
                        Acao(DryFireId);
                        Efeitos.Som(Efeitos.Clique, Boca.position, 0.8f, false);
                        _emperrouAte = Time.time + 1.4f;
                        _proximoTiro = Time.time + Mathf.Max(d.intervalo, 0.35f);
                    }
                    else if (Municao > 0)
                    {
                        Atirar();
                        if (!bot) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));
                    }
                    else if (apertou)
                    {
                        Acao(DryFireId);
                        Efeitos.Som(Efeitos.Clique, Boca.position, 0.8f, false);
                        if (Reserva == 0) _semMunicaoAte = Time.time + 1.8f;
                    }
                }
            }
            // um tratamento do corpo (05/11/2026): o clique começa o curativo com tempo, na parte escolhida sozinha
            else if (PartesDoCorpo.EhTratamento(NaMao) && !Usando && !bot && Time.time >= _proximoTiro && _atirar.WasPressedThisFrame())
            {
                if (Tratamento.Local != null) Tratamento.Local.Comecar(_id, PartesDoCorpo.Sortear);
            }
            else if (Itens.EhConsumivel(NaMao) && !Usando && !bot && Time.time >= _proximoTiro && _atirar.WasPressedThisFrame())
                StartCoroutine(Usar());
            // a mão vazia soca (segurando o botão, um soco atrás do outro)
            else if (NaMao == Item.Nada && !bot && Time.time >= _proximoTiro && _atirar.IsPressed())
                StartCoroutine(Socar());
            // a arma branca de duas mãos, com o braço quebrado, não golpeia
            else if (Itens.EhBranca(NaMao) && semBraco)
            {
                if (_atirar.WasPressedThisFrame()) AvisarDoBraco();
            }
            // a arma branca golpeia (segurando o botão, um golpe atrás do outro)
            else if (Itens.EhBranca(NaMao) && !bot && Time.time >= _proximoTiro && _atirar.IsPressed())
                StartCoroutine(GolpearComBranca());
            if (_alvo != null && !bot && !Usando && _interagir.WasPressedThisFrame()) ApertouE();
        }

        /// <summary>Toca uma ação (sacar, tiro, gatilho sem bala, recarga, comer ou beber) do começo, na camada "Ações".</summary>
        void Acao(int estado)
        {
            var animador = Animador;
            if (animador == null) return;
            _pesoDasAcoes = 1f;
            animador.SetLayerWeight(CamadaAcoes, 1f);
            animador.Play(estado, CamadaAcoes, 0f);
        }

        /// <summary>
        /// A camada "Ações" fica por cima da locomoção (parado, andando, correndo) só durante uma ação. Parada no estado
        /// vazio, com o Write Defaults desligado, ela segurava a última pose da ação para sempre: os braços ficavam
        /// congelados no fim do sacar, e a arma não respirava, não balançava andando nem baixava correndo. Agora, quando
        /// a ação acaba, o peso desce até zero em 0,15 s (sem pulo da pose da ação para a da locomoção).
        /// </summary>
        void PesoDasAcoes(Animator animador)
        {
            bool parada = animador.GetCurrentAnimatorStateInfo(CamadaAcoes).shortNameHash == VazioId && !animador.IsInTransition(CamadaAcoes);
            _pesoDasAcoes = parada ? Mathf.MoveTowards(_pesoDasAcoes, 0f, Time.deltaTime / 0.15f) : 1f;
            animador.SetLayerWeight(CamadaAcoes, _pesoDasAcoes);
        }

        /// <summary>
        /// O soco, com a mão vazia: um punho de cada vez (o direito no estado do tiro, o esquerdo no do gatilho sem bala),
        /// com o vento do braço. No quadro do golpe, uma esfera curta sai do olho, na direção da mira: o primeiro zumbi
        /// que ela pega leva o dano no corpo ou, na cabeça, 3 vezes (sem arrancar membro), com a pancada, um pouco de
        /// sangue e a marca na mira; uma parede faz o barulho dos nós dos dedos. Os outros veem o soco no corpo e ouvem tudo.
        /// </summary>
        IEnumerator Socar()
        {
            _proximoTiro = Time.time + intervaloDoSoco * PartesDoCorpo.LentidaoDoGolpe(Corpo);
            bool direito = _socoDireito;
            _socoDireito = !_socoDireito;
            Acao(direito ? FireId : DryFireId);
            Efeitos.Som(Efeitos.SocoVento, rede.cameraMundo.transform.position, 0.45f, false);
            rede.AnunciarSoco();
            yield return new WaitForSeconds(SomDoSoco.Impacto);
            Golpear(alcanceDoSoco, raioDoSoco, danoDoSoco, false, false, 0f);   // o punho não tem fio: não explode a cabeça
        }

        /// <summary>
        /// O golpe de uma arma branca (o básico, 31/10/2026: "um sistema básico de atacar e segurar, vou arrumar
        /// posteriormente"): o clipe de golpe dos braços dela (a faca: o soco direito com ela no punho; o machado: a
        /// machadada nas duas mãos), o vento do braço e, quando o golpe pega (DadosDaBranca.impacto), a esfera do soco com
        /// o alcance, a grossura e o dano dela; a cabeça vale 3 vezes em todas; o machado e a lança acertam o braço ou a
        /// perna onde pegam (o machado corta em dobro: decepa com um golpe). Os outros veem o soco direito no corpo, com
        /// ela na mão.
        /// </summary>
        IEnumerator GolpearComBranca()
        {
            var b = Itens.Branca(NaMao);
            _proximoTiro = Time.time + b.intervalo * PartesDoCorpo.LentidaoDoGolpe(Corpo);   // (o braço quebrado com a tala: mais lento)
            Acao(FireId);
            Efeitos.Som(Efeitos.SocoVento, rede.cameraMundo.transform.position, 0.55f, false);
            rede.AnunciarSoco();
            yield return new WaitForSeconds(b.impacto);
            Golpear(b.alcance, b.raio, b.dano, true, b.porRegiao, b.corte);
        }

        // o golpe: na cabeça (todas as armas, o soco também), o dano vale 3 vezes no zumbi, e o golpe que mata só explode a
        // cabeça se tiver corte (o soco, o bastão e a tocha, 0, derrubam com ela no lugar); num braço ou numa perna, só as
        // que acertam o membro (porRegiao: o machado e a lança) o levam, com o corte; o resto conta como o tronco
        void Golpear(float alcance, float raio, float dano, bool faca, bool porRegiao, float corte)
        {
            var cam = rede.Olho;
            Physics.SyncTransforms();   // as hitboxes andam com a animação
            int n = Physics.SphereCastNonAlloc(cam.position, raio, cam.forward, _acertos, alcance, MascaraDoTiro, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_acertos, 0, n, PorDistancia);
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                if (hit.collider == _corpo) continue;
                // uma esfera que já começa encostada não tem ponto: o mais perto do olho, no colisor
                Vector3 ponto = hit.distance <= 0f ? hit.collider.ClosestPoint(cam.position) : hit.point;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                // um sobrevivente: o golpe na cabeça vale mais (sem matar na hora), nos membros vale metade
                var parte = hitbox == null ? hit.collider.GetComponent<Sobreviventes.ParteDoSobrevivente>() : null;
                if (hit.collider.gameObject.layer == Sobreviventes.Sobrevivente.Camada && (parte == null || parte.sobrevivente == null || parte.sobrevivente.Morto)) continue;   // os corpos no chão
                if (parte != null && parte.sobrevivente != null)
                {
                    Efeitos.Som(Efeitos.SocoCarne, ponto, faca ? 0.9f : 0.8f, false);
                    Sangue.Respingo(ponto, -cam.forward);
                    _marcaAte = Time.time + 0.18f;
                    _marcaNaCabeca = parte.regiao == Sobreviventes.RegiaoDoSobrevivente.Cabeca;
                    rede.AcertarSobrevivente(parte.sobrevivente, parte.regiao, dano, ponto, false);
                    rede.AnunciarPancada(ponto, true);
                    if (faca) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));
                    return;
                }
                if (hitbox != null && hitbox.zumbi != null)
                {
                    if (hitbox.zumbi.Morto) continue;
                    // a estátua do sol: soco em pedra (o som da parede e poeira, sem sangue)
                    bool pedra = DePedra(hitbox);
                    if (pedra)
                    {
                        Efeitos.Som(Efeitos.SocoParede, ponto, 0.8f, false);
                        Efeitos.Impacto(ponto, -cam.forward);
                    }
                    else
                    {
                        Efeitos.Som(Efeitos.SocoCarne, ponto, faca ? 0.9f : 0.8f, false);
                        Sangue.Respingo(ponto, -cam.forward);
                        if (faca) Sangue.Respingo(ponto, cam.forward);
                    }
                    _marcaAte = Time.time + 0.18f;
                    bool naCabeca = hitbox.regiao == RegiaoDoCorpo.Cabeca;
                    _marcaNaCabeca = naCabeca;
                    if (naCabeca) rede.AcertarZumbi(hitbox.zumbi, RegiaoDoCorpo.Cabeca, Membro.Nenhum, dano, ponto, false, corte);
                    // o olho do Brutamonte e o papo do Long (os especiais) contam como eles mesmos
                    else if (hitbox.regiao == RegiaoDoCorpo.Olho || hitbox.regiao == RegiaoDoCorpo.Papo) rede.AcertarZumbi(hitbox.zumbi, hitbox.regiao, Membro.Nenhum, dano, ponto, false, corte);
                    else if (porRegiao) rede.AcertarZumbi(hitbox.zumbi, hitbox.regiao, hitbox.membro, dano, ponto, false, corte);
                    else rede.AcertarZumbi(hitbox.zumbi, RegiaoDoCorpo.Tronco, Membro.Nenhum, dano, ponto, false);
                    rede.AnunciarPancada(ponto, !pedra);
                    if (faca) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));   // a arma branca gasta a cada golpe que pega
                    return;
                }
                // outro jogador, com o fogo amigo ligado (03/11/2026): o dano da arma, sem a cabeça
                var outro = OutroJogador(hit.collider);
                if (outro != null)
                {
                    Efeitos.Som(Efeitos.SocoCarne, ponto, faca ? 0.9f : 0.8f, false);
                    Sangue.Respingo(ponto, -cam.forward);
                    _marcaAte = Time.time + 0.18f;
                    _marcaNaCabeca = false;
                    // o jeito do golpe (05/11/2026): o que tem fio corta (a faca, o machado, a lança); o resto é pancada
                    // (o soco, o bastão, a tocha); na parte do ponto
                    rede.AcertarJogador(outro, dano, ponto, corte > 0f ? TipoDeGolpe.Corte : TipoDeGolpe.Pancada, PartesDoCorpo.PeloPonto(outro.transform, ponto));
                    rede.AnunciarPancada(ponto, true);
                    if (faca) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));
                    return;
                }
                Efeitos.Som(Efeitos.SocoParede, ponto, 0.6f, false);
                if (faca) Efeitos.Impacto(ponto, -cam.forward);   // a lâmina na parede: faíscas e poeira
                rede.AnunciarPancada(ponto, false);
                if (faca) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));
                return;
            }
        }

        /// <summary>
        /// Comer ou beber: o clipe Usar (o item vem até a boca e deita), com o som de abrir e as mordidas ou os goles. No
        /// fim, o anfitrião tira o item do lugar e enche a fome e a sede; a mão volta para a última arma.
        /// </summary>
        IEnumerator Usar()
        {
            Usando = true;
            var item = NaMao;
            ushort id = _id;
            var dados = Itens.Consumivel(item);
            bool bebe = dados.tipo == TipoDeLoot.Bebida, remedio = dados.tipo == TipoDeLoot.Remedio;
            float inicio = Time.time;
            Acao(FireId);   // o estado do tiro toca o clipe Usar (o override do item)
            Efeitos.Som(bebe || remedio ? Efeitos.AbrirTampa : Efeitos.AbrirLata, rede.cameraMundo.transform.position, 0.45f, false);
            // as mordidas e os goles, nos quadros 14, 20 e 26 do clipe (itens.py); o remédio, sem
            foreach (float quando in new[] { 14 / 24f, 20 / 24f, 26 / 24f })
            {
                yield return new WaitForSeconds(Mathf.Max(0f, quando - (Time.time - inicio)));
                if (!remedio) Efeitos.Som(bebe ? Efeitos.Gole : Efeitos.Mastigar, rede.cameraMundo.transform.position, 0.5f, false);
            }
            yield return new WaitForSeconds(Mathf.Max(0f, Itens.Consumivel(item).tempoDeUso - (Time.time - inicio)));
            Usando = false;
            if (NaMao == item && _id == id) Inventario.Consumir(id, _ultimaArma);
        }

        void Atirar()
        {
            var d = Dados;
            Municao--;
            Acao(FireId);
            // na terceira pessoa, o clarão é o da arma do corpo, na camada de todo mundo
            if (rede.EmTerceiraPessoa) Efeitos.Clarao(rede.bocasCorpo[(int)Atual], 0);
            else Efeitos.Clarao(Boca, JogadorRede.CamadaBracos);
            Efeitos.Som(Efeitos.Tiros[(int)Atual], Boca.position, 1f, false);
            if (Atual == TipoDeArma.Espingarda) StartCoroutine(SomDepois(Efeitos.Bomba, 0.33f, 0.8f));
            if (d.ferrolho) StartCoroutine(SomDepois(Efeitos.Ferrolho, 0.42f, 0.7f));
            movimento.Coice(d.coiceGraus * (Corpo.AlgumBracoRuim ? 1.35f : 1f));   // (o braço quebrado segura mal o coice)

            var cam = rede.Olho;
            _marcaNaCabeca = false;
            // correndo (só acima de 3 m/s: a perna quebrada que "corre" mancando não conta), e o braço machucado ou quebrado
            // (05/11/2026: a mira treme)
            float abertura = (d.fovLuneta > 0f && !NaLuneta ? d.dispersaoSemLuneta : d.dispersao) + (rede.Velocidade > 3f ? d.dispersaoAndando * 2f : 0f)
                             + PartesDoCorpo.DispersaoExtra(Corpo);
            // as hitboxes andam com a animação: sincroniza a física antes do raio, para acertar onde o zumbi está agora
            Physics.SyncTransforms();
            var impactos = new JogadorRede.Impacto[d.chumbos];
            _carne.Clear();
            _gente.Clear();
            _jogadores.Clear();
            for (int k = 0; k < d.chumbos; k++)
            {
                // na espingarda um chumbo sai bem no meio da mira: é a parte que ele pega que conta (veja MandarAcertos)
                _chumboDoCentro = k == 0 && d.chumbos > 1;
                Vector2 desvio = _chumboDoCentro ? Vector2.zero : Random.insideUnitCircle * abertura;
                // (na terceira pessoa a câmera não treme: o tremor do braço e o calafrio entram aqui, no tiro)
                var mira = rede.EmTerceiraPessoa && EfeitosDoCorpo.Local != null ? cam.rotation * EfeitosDoCorpo.Local.DesvioDaMira : cam.rotation;
                Vector3 direcao = mira * (Quaternion.Euler(desvio.y, desvio.x, 0f) * Vector3.forward);
                impactos[k] = Disparar(cam.position, direcao, d);
            }
            _chumboDoCentro = false;
            MandarAcertos(d);
            MandarAcertosNaGente(d);
            MandarAcertosNosJogadores(d);
            rede.AnunciarTiro(Atual, impactos);
        }

        // ------------------------------------------------------------------ o fogo amigo (03/11/2026)

        /// <summary>O fogo amigo está ligado neste mundo (escolhido ao criar: World/MundoDaPartida.FogoAmigo).</summary>
        static bool FogoAmigo => World.MundoDaPartida.Instancia != null && World.MundoDaPartida.Instancia.FogoAmigo;

        /// <summary>Outro jogador que o tiro (ou um chumbo) pegou, com o fogo amigo ligado.</summary>
        readonly System.Collections.Generic.List<(JogadorRede alvo, Vector3 ponto, float distancia)> _jogadores =
            new System.Collections.Generic.List<(JogadorRede, Vector3, float)>();

        // o jogador que o raio pegou (o colisor dele, o CharacterController da raiz), se o fogo amigo estiver ligado
        JogadorRede OutroJogador(Collider c)
        {
            if (!FogoAmigo || c == null || c == _corpo) return null;
            var j = c.GetComponent<JogadorRede>();
            return j != null && j != rede && j.AlvoValido ? j : null;
        }

        /// <summary>
        /// Os acertos nos outros jogadores (o fogo amigo): um por jogador a cada disparo (na espingarda, os chumbos contam
        /// juntos), com o dano da arma na distância mais perto. Sem tiro na cabeça entre jogadores (a pedido do plano).
        /// </summary>
        void MandarAcertosNosJogadores(DadosDaArma d)
        {
            for (int i = 0; i < _jogadores.Count; i++)
            {
                var alvo = _jogadores[i].alvo;
                bool repetido = false;
                for (int k = 0; k < i && !repetido; k++) repetido = _jogadores[k].alvo == alvo;
                if (repetido) continue;
                float perto = _jogadores[i].distancia;
                for (int k = i; k < _jogadores.Count; k++) if (_jogadores[k].alvo == alvo) perto = Mathf.Min(perto, _jogadores[k].distancia);
                // o tiro pega na parte do ponto (05/11/2026: sempre abre uma ferida)
                rede.AcertarJogador(alvo, Armas.Dano(d, perto), _jogadores[i].ponto, TipoDeGolpe.Tiro, PartesDoCorpo.PeloPonto(alvo.transform, _jogadores[i].ponto));
            }
        }

        /// <summary>Uma parte de um sobrevivente que o tiro (ou um chumbo) pegou.</summary>
        struct NaGente
        {
            public Sobreviventes.ParteDoSobrevivente parte;
            public Vector3 ponto;
            public float distancia;
        }

        readonly System.Collections.Generic.List<NaGente> _gente = new System.Collections.Generic.List<NaGente>();

        /// <summary>
        /// Os acertos nos sobreviventes: um por pessoa a cada disparo (na espingarda, os chumbos contam juntos), na melhor
        /// parte que foi pega (a cabeça, depois o tronco, depois um membro), com o dano da distância mais perto.
        /// </summary>
        void MandarAcertosNaGente(DadosDaArma d)
        {
            for (int i = 0; i < _gente.Count; i++)
            {
                var s = _gente[i].parte.sobrevivente;
                bool repetido = false;
                for (int k = 0; k < i && !repetido; k++) repetido = _gente[k].parte.sobrevivente == s;
                if (repetido) continue;
                int melhor = i;
                float perto = _gente[i].distancia;
                for (int k = i; k < _gente.Count; k++)
                {
                    if (_gente[k].parte.sobrevivente != s) continue;
                    perto = Mathf.Min(perto, _gente[k].distancia);
                    if (Peso(_gente[k].parte.regiao) > Peso(_gente[melhor].parte.regiao)) melhor = k;
                }
                // o dano da distância (na espingarda, o do tiro inteiro, como nos zumbis)
                rede.AcertarSobrevivente(s, _gente[melhor].parte.regiao, Armas.Dano(d, perto), _gente[melhor].ponto, true);
            }
        }

        static int Peso(Sobreviventes.RegiaoDoSobrevivente r) =>
            r == Sobreviventes.RegiaoDoSobrevivente.Cabeca ? 2 : r == Sobreviventes.RegiaoDoSobrevivente.Tronco ? 1 : 0;

        /// <summary>Um pedaço de zumbi que o tiro (ou um chumbo) pegou, e a que distância do olho.</summary>
        struct NaCarne
        {
            public Hitbox hitbox;
            public Vector3 ponto;
            public float distancia;
            /// <summary>Do chumbo do meio da mira (na espingarda).</summary>
            public bool centro;
        }

        bool _chumboDoCentro;

        readonly System.Collections.Generic.List<NaCarne> _carne = new System.Collections.Generic.List<NaCarne>();

        /// <summary>
        /// Os acertos do disparo vão para o anfitrião, com o dano da distância (no tronco ou no membro). Na espingarda os
        /// chumbos contam juntos: cada zumbi leva um acerto só, na parte onde se mirou (a que o chumbo do meio pegou). De
        /// perto, um chumbo na cabeça já basta para explodir a cabeça; de longe não (um chumbo perdido matava na média
        /// distância, e lá a espingarda precisa de dois tiros). Se o chumbo do meio não pegou este zumbi, conta a parte que
        /// mais levou chumbo (o tronco, se empatar com um membro; a cabeça, se empatar com qualquer uma).
        /// </summary>
        void MandarAcertos(DadosDaArma d)
        {
            if (!d.chumbosJuntos)
            {
                foreach (var c in _carne) Mandar(c, d);
                return;
            }
            for (int i = 0; i < _carne.Count; i++)
            {
                var zumbi = _carne[i].hitbox.zumbi;
                bool repetido = false;
                for (int k = 0; k < i && !repetido; k++) repetido = _carne[k].hitbox.zumbi == zumbi;
                if (repetido) continue;
                float perto = float.MaxValue;
                for (int k = i; k < _carne.Count; k++)
                    if (_carne[k].hitbox.zumbi == zumbi) perto = Mathf.Min(perto, _carne[k].distancia);
                bool dePerto = perto <= d.arremessoAte;
                int melhor = -1, maisChumbos = 0, doCentro = -1;
                bool naCabeca = false;
                for (int k = i; k < _carne.Count; k++)
                {
                    if (_carne[k].hitbox.zumbi != zumbi) continue;
                    if (_carne[k].centro && doCentro < 0) doCentro = k;
                    naCabeca |= _carne[k].hitbox.regiao == RegiaoDoCorpo.Cabeca || _carne[k].hitbox.regiao == RegiaoDoCorpo.Olho;
                }
                for (int k = i; k < _carne.Count; k++)
                {
                    var h = _carne[k].hitbox;
                    if (h.zumbi != zumbi) continue;
                    int chumbos = 0;
                    for (int j = i; j < _carne.Count; j++)
                        if (_carne[j].hitbox.zumbi == zumbi && _carne[j].hitbox.regiao == h.regiao && _carne[j].hitbox.membro == h.membro) chumbos++;
                    // a conta vale o dobro mais o desempate: cabeça, depois tronco, depois membro
                    if (h.regiao == RegiaoDoCorpo.Cabeca || h.regiao == RegiaoDoCorpo.Olho) chumbos = dePerto ? 1000 : chumbos * 3 + 2;
                    else if (!EhMembro(h.regiao)) chumbos = chumbos * 3 + 1;
                    else chumbos *= 3;
                    if (chumbos > maisChumbos) { maisChumbos = chumbos; melhor = k; }
                }
                if (doCentro >= 0 && !(dePerto && naCabeca)) melhor = doCentro;
                var escolhido = _carne[melhor];
                escolhido.distancia = perto;
                Mandar(escolhido, d);
            }
        }

        void Mandar(in NaCarne c, DadosDaArma d)
        {
            // o dano da distância; a parte do corpo (a cabeça 3 vezes, o membro metade) multiplica no zumbi
            float dano = Armas.Dano(d, c.distancia);
            bool arremesso = d.arremessoAte > 0f && c.distancia <= d.arremessoAte;
            rede.AcertarZumbi(c.hitbox.zumbi, c.hitbox.regiao, c.hitbox.membro, dano, c.ponto, arremesso);
        }

        static bool EhMembro(RegiaoDoCorpo r) => r == RegiaoDoCorpo.Braco || r == RegiaoDoCorpo.Perna;

        /// <summary>Um raio (o tiro, ou um chumbo): o efeito aqui, o acerto ao anfitrião e o que os outros devem ver.</summary>
        JogadorRede.Impacto Disparar(Vector3 origem, Vector3 direcao, DadosDaArma d)
        {
            var impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Nada, normal = Vector3.up };
            int bracos = 0;
            // a bala que atravessa o antebraço e pega o braço logo atrás conta uma vez só naquele membro (senão o braço
            // caía com metade dos tiros: dois de pistola em vez de quatro)
            Zumbi ultimoZumbi = null;
            int ultimoMembro = -1;
            // à queima-roupa, a hitbox colada na câmera leva o tiro primeiro
            var encostada = Encostada(origem, direcao, out Vector3 pontoEncostado);
            if (encostada != null)
            {
                impacto = new JogadorRede.Impacto { tipo = TipoNoZumbi(encostada), ponto = pontoEncostado, normal = -direcao };
                Carne(encostada, pontoEncostado, -direcao, 0f);
                if (!EhBraco(encostada.regiao)) return impacto;
                bracos++;
                ultimoZumbi = encostada.zumbi;
                ultimoMembro = Membros.Limbo(encostada.membro);
            }
            // o tiro atravessa braços e mãos (até dois): numa multidão, as mãos de quem ataca ficam na frente da mira e
            // comiam o tiro que era do zumbi de trás
            int n = Physics.RaycastNonAlloc(origem, direcao, _acertos, d.alcance, MascaraDoTiro, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_acertos, 0, n, PorDistancia);
            // as rodas dos carros não têm colisor: o raio é conferido contra elas à parte
            float naRoda = Vehicles.Carro.TiroNasRodas(origem, direcao, d.alcance, out var carroDaRoda, out int roda, out var normalDaRoda);
            bool parou = false;
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                // a roda antes do que o raio pegou (ou logo atrás da lataria do mesmo carro: a caixa de colisão de baixo
                // passa um pouco da roda)
                if (carroDaRoda != null && (naRoda < hit.distance
                    || (hit.rigidbody != null && hit.rigidbody.gameObject == carroDaRoda.gameObject && naRoda - hit.distance < 0.35f)))
                    return NaRoda(carroDaRoda, roda, origem + direcao * naRoda, normalDaRoda);
                // o olho fica bem no topo da cápsula do próprio jogador: o tiro para baixo (na cabeça de um zumbi perto,
                // por exemplo) batia nela, a distância zero, e soltava faíscas em cima da cabeça
                if (hit.collider == _corpo) continue;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox == encostada) continue;
                // um sobrevivente: o sangue aqui, a marca na mira, e o acerto vai ao anfitrião no fim do disparo
                var parte = hitbox == null ? hit.collider.GetComponent<Sobreviventes.ParteDoSobrevivente>() : null;
                if (hit.collider.gameObject.layer == Sobreviventes.Sobrevivente.Camada && (parte == null || parte.sobrevivente == null || parte.sobrevivente.Morto)) continue;   // os corpos no chão
                if (parte != null && parte.sobrevivente != null)
                {
                    Sangue.Respingo(hit.point, hit.normal);
                    _gente.Add(new NaGente { parte = parte, ponto = hit.point, distancia = hit.distance });
                    _marcaAte = Time.time + 0.18f;
                    _marcaNaCabeca |= parte.regiao == Sobreviventes.RegiaoDoSobrevivente.Cabeca;
                    impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };
                    parou = true;
                    break;
                }
                if (hitbox != null && hitbox.zumbi != null)
                {
                    int membro = Membros.Limbo(hitbox.membro);
                    if (membro >= 0 && hitbox.zumbi == ultimoZumbi && membro == ultimoMembro) continue;
                    ultimoZumbi = hitbox.zumbi;
                    ultimoMembro = membro;
                    Carne(hitbox, hit.point, hit.normal, hit.distance);
                    impacto = new JogadorRede.Impacto { tipo = TipoNoZumbi(hitbox), ponto = hit.point, normal = hit.normal };
                    if (EhBraco(hitbox.regiao) && ++bracos <= 2) continue;
                    parou = true;
                    break;
                }
                // outro jogador, com o fogo amigo ligado: o sangue aqui; o acerto vai ao anfitrião no fim do disparo
                var outro = OutroJogador(hit.collider);
                if (outro != null)
                {
                    Sangue.Respingo(hit.point, hit.normal);
                    _jogadores.Add((outro, hit.point, hit.distance));
                    _marcaAte = Time.time + 0.18f;
                    impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };
                    parou = true;
                    break;
                }
                // furo só no cenário (em gente e em coisas que se mexem, só as faíscas)
                var tipo = hit.collider is CharacterController || hit.rigidbody != null ? JogadorRede.TipoDeAcerto.Outro : JogadorRede.TipoDeAcerto.Cenario;
                impacto = new JogadorRede.Impacto { tipo = tipo, ponto = hit.point, normal = hit.normal };
                Efeitos.Impacto(hit.point, hit.normal);
                if (tipo == JogadorRede.TipoDeAcerto.Cenario) Sangue.FuroDeTiro(hit.point, hit.normal);
                parou = true;
                break;
            }
            if (!parou && carroDaRoda != null) return NaRoda(carroDaRoda, roda, origem + direcao * naRoda, normalDaRoda);
            return impacto;
        }

        // o tiro pegou o pneu: as faíscas (o aro) e o pedido de furar ao anfitrião
        JogadorRede.Impacto NaRoda(Vehicles.Carro carro, int roda, Vector3 ponto, Vector3 normal)
        {
            Efeitos.Impacto(ponto, normal);
            carro.Furar(roda);
            return new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Outro, ponto = ponto, normal = normal };
        }

        // folgado: com o buffer cheio, o Unity não garante que vieram os acertos mais perto (numa multidão, o raio cruza
        // várias hitboxes)
        static readonly RaycastHit[] _acertos = new RaycastHit[64];
        static readonly System.Collections.Generic.IComparer<RaycastHit> PorDistancia =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        static bool EhBraco(RegiaoDoCorpo r) => r == RegiaoDoCorpo.Braco;

        // um acerto num zumbi: sangue aqui na hora e a marca na mira; o acerto vai para o anfitrião no fim do disparo
        // o Oco de pedra (o sol): o tiro solta faíscas e poeira, não sangue (os outros veem um impacto "outro")
        // a pedra do sol, e o casco do Brutamonte (fora do olho): o acerto faz poeira e faíscas, sem sangue
        static bool DePedra(Hitbox hitbox) => hitbox.zumbi != null && (hitbox.zumbi.Pedra >= 0.5f || (hitbox.zumbi.tipo == TipoDeZumbi.Brutamonte && hitbox.regiao != RegiaoDoCorpo.Olho));
        static JogadorRede.TipoDeAcerto TipoNoZumbi(Hitbox hitbox) => DePedra(hitbox) ? JogadorRede.TipoDeAcerto.Outro : JogadorRede.TipoDeAcerto.Carne;

        void Carne(Hitbox hitbox, Vector3 ponto, Vector3 normal, float distancia)
        {
            if (DePedra(hitbox)) Efeitos.Impacto(ponto, normal);
            else Sangue.Respingo(ponto, normal);
            _carne.Add(new NaCarne { hitbox = hitbox, ponto = ponto, distancia = distancia, centro = _chumboDoCentro });
            _marcaAte = Time.time + 0.18f;
            _marcaNaCabeca |= hitbox.regiao == RegiaoDoCorpo.Cabeca || hitbox.regiao == RegiaoDoCorpo.Olho;
        }

        // À queima-roupa a cabeça do zumbi (no bote) pode chegar até a câmera, e um raio que começa dentro de um colisor
        // não o acerta. Os primeiros 35 cm do tiro são conferidos à parte: a hitbox que estiver ali leva o tiro.
        Hitbox Encostada(Vector3 origem, Vector3 direcao, out Vector3 ponto)
        {
            ponto = origem;
            int n = Physics.OverlapCapsuleNonAlloc(origem, origem + direcao * 0.35f, 0.03f, _encostados, 1 << Zumbi.CamadaZumbi, QueryTriggerInteraction.Ignore);
            Hitbox melhor = null;
            float menor = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hitbox = _encostados[i].GetComponent<Hitbox>();
                if (hitbox == null || hitbox.regiao == RegiaoDoCorpo.Corpo) continue;
                Vector3 p = _encostados[i].ClosestPoint(origem);
                float dist = (p - origem).sqrMagnitude;
                if (dist < menor) { menor = dist; melhor = hitbox; ponto = p; }
            }
            return melhor;
        }

        IEnumerator Recarregar()
        {
            Recarregando = true;
            var d = Dados;
            if (d.cartuchoACartucho)
            {
                // um cartucho por vez (cada um sai de uma caixa); o cartucho entra perto do fim da animação (quadro 9 a 10
                // de 13). Acabaram os cartuchos, a recarga para
                while (Municao < d.capacidade && Reserva > 0)
                {
                    Acao(ReloadId);
                    yield return new WaitForSeconds(d.tempoRecarga * 0.72f);
                    if (Reserva <= 0) break;
                    Efeitos.Som(Efeitos.Cartucho, Boca.position, 0.7f, false);
                    Municao++;
                    GastarDaReserva(1);
                    yield return new WaitForSeconds(d.tempoRecarga * 0.28f);
                }
            }
            else
            {
                Acao(ReloadId);
                yield return new WaitForSeconds(d.carregadorSai);
                Efeitos.Som(Efeitos.Carregador, Boca.position, 0.6f, false);
                yield return new WaitForSeconds(d.carregadorEntra - d.carregadorSai);
                Efeitos.Som(Efeitos.Carregador, Boca.position, 0.8f, false);
                yield return new WaitForSeconds(d.tempoRecarga - d.carregadorEntra);
                // o pente é completado com o que houver nas caixas (o que sobrou nele continua)
                int balas = Mathf.Min(d.capacidade - Municao, Reserva);
                Municao += balas;
                GastarDaReserva(balas);
            }
            Recarregando = false;
            _recarga = null;
        }

        void PararRecarga()
        {
            if (_recarga != null) StopCoroutine(_recarga);
            _recarga = null;
            Recarregando = false;
        }

        IEnumerator SomDepois(AudioClip clip, float espera, float volume)
        {
            yield return new WaitForSeconds(espera);
            Efeitos.Som(clip, Boca.position, volume, false);
        }

        // a mira numa coisa que se usa, de perto: a mais perto na linha do olhar que tem o que fazer agora (um gatilho sem
        // dica, como o da roda sem o macaco, deixa a mira passar para a porta atrás dele; gatilhos de outras coisas, como
        // as áreas internas, não contam). Um colisor sólido para a busca.
        void ProcurarAlvo(Transform cam)
        {
            _alvo = null;
            _dica = null;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, _acertos, alcanceDaMao, ~0, QueryTriggerInteraction.Collide);
            // em ordem de distância (o RaycastNonAlloc não ordena)
            for (int i = 1; i < n; i++)
            {
                var h = _acertos[i];
                int j = i - 1;
                while (j >= 0 && _acertos[j].distance > h.distance)
                {
                    _acertos[j + 1] = _acertos[j];
                    j--;
                }
                _acertos[j + 1] = h;
            }
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                if (hit.collider == _corpo) continue;
                var alvo = hit.collider.GetComponent<Interagivel>();
                if (alvo == null)
                {
                    // as partes dos sobreviventes (e os corpos no chão) não tapam a mochila do lado, nem o móvel o item
                    // do saque dentro dele
                    if (hit.collider.isTrigger || hit.collider.gameObject.layer == Sobreviventes.Sobrevivente.Camada || DeDentro(i, n, hit.point, null)) continue;
                    return;
                }
                string dica = alvo.Dica(this);
                if (dica == null)
                {
                    if (hit.collider.isTrigger) continue;
                    return;
                }
                if (DeDentro(i, n, hit.point, alvo)) continue;
                _alvo = alvo;
                _dica = dica;
                return;
            }
        }

        // o E na coisa da mira: aperta (Usar) ou começa a segurar (Segurar mais que 0: arrombar a porta trancada)
        void ApertouE()
        {
            float t = _alvo.Segurar(this);
            if (t <= 0f)
            {
                _alvo.Usar(this);
                return;
            }
            _segurando = _alvo;
            _segurarDesde = Time.time;
            _segurarTempo = t;
        }

        Interagivel _segurando;
        float _segurarDesde, _segurarTempo;

        /// <summary>A coisa em que o jogador está segurando o E agora (null: nenhuma).</summary>
        public Interagivel SegurandoAgora => _segurando;

        // uma coisa mais adiante no raio que ganha da do acerto i (o saque dentro do móvel: veja Interagivel.GanhaDe)
        bool DeDentro(int i, int n, Vector3 ponto, Interagivel antes)
        {
            for (int j = i + 1; j < n; j++)
            {
                var outra = _acertos[j].collider.GetComponent<Interagivel>();
                if (outra != null && outra != antes && outra.GanhaDe(ponto, antes) && outra.Dica(this) != null) return true;
            }
            return false;
        }

        void OnGUI()
        {
            // com o inventário ou o menu do desenvolvedor, sem mira; gravando um vídeo, só no das armas
            if (MenuOnline.Aberto || MenuOnline.ComTela || MenuOnline.Cinema || (ModoVideo.Ligado && !ModoVideo.MostrarMira)) return;
            float u = Screen.height / 720f, cx = Screen.width / 2f, cy = Screen.height / 2f;
            float t = Mathf.Max(1f, Mathf.Round(2 * u));
            if (NaLuneta) Luneta(cx, cy, u);
            else
            {
                // mira: um ponto com quatro tracinhos
                Color cor = new Color(0.95f, 0.93f, 0.85f, 0.85f);
                float g = 6 * u, l = 7 * u;
                Retangulo(cx - t / 2, cy - t / 2, t, t, cor);
                Retangulo(cx - g - l, cy - t / 2, l, t, cor); Retangulo(cx + g, cy - t / 2, l, t, cor);
                Retangulo(cx - t / 2, cy - g - l, t, l, cor); Retangulo(cx - t / 2, cy + g, t, l, cor);
            }
            // acertou carne: quatro tracinhos diagonais (vermelhos na cabeça)
            if (Time.time < _marcaAte)
            {
                var marca = _marcaNaCabeca ? new Color(0.9f, 0.2f, 0.15f, 0.95f) : new Color(0.95f, 0.93f, 0.85f, 0.95f);
                var antes = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
                float m = 9 * u, ml = 6 * u;
                Retangulo(cx - m - ml, cy - t / 2, ml, t, marca); Retangulo(cx + m, cy - t / 2, ml, t, marca);
                Retangulo(cx - t / 2, cy - m - ml, t, ml, marca); Retangulo(cx - t / 2, cy + m, t, ml, marca);
                GUI.matrix = antes;
            }
            if (MaosOcupadas)
            {
                var pesado = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.LowerRight);
                pesado.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
                GUI.Label(new Rect(Screen.width - 420 * u, Screen.height - 50 * u, 400 * u, 30 * u),
                    Inventario.PneuDasMaosFurado
                        ? $"PNEU FURADO NAS MÃOS ({Inventario.EstadoDoPneuNasMaos}%)  ·  R: REMENDAR  ·  G: LARGAR"
                        : Inventario.EstadoDoPneuNasMaos == 0 ? "PNEU RASGADO NAS MÃOS (SEM REMENDO)  ·  G: LARGAR"
                        : $"PNEU NAS MÃOS ({Inventario.EstadoDoPneuNasMaos}%)  ·  PESADO  ·  G: LARGAR", pesado);
            }
            else if (ComArma)
            {
                var d = Dados;
                var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(22 * u), FontStyle.Bold, TextAnchor.LowerRight, true);
                estilo.normal.textColor = Municao == 0 ? new Color(1f, 0.45f, 0.4f) : new Color(0.9f, 0.88f, 0.8f);
                // o pente e, menor, as balas do calibre dela nas caixas da mochila (o texto só é refeito quando muda)
                GUI.Label(new Rect(Screen.width - 260 * u, Screen.height - 50 * u, 240 * u, 36 * u), TextoDaMunicao(d, Mathf.RoundToInt(15 * u)), estilo);
                var nome = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Bold, TextAnchor.LowerRight, true);
                nome.normal.textColor = new Color(0.75f, 0.73f, 0.66f);
                int desgaste = Inventario != null ? Inventario.DesgasteDe(_id) : 0;
                GUI.Label(new Rect(Screen.width - 360 * u, Screen.height - 74 * u, 340 * u, 24 * u), NomeDaArma(d, desgaste), nome);
                if (desgaste >= 100 || Time.time < _emperrouAte)
                {
                    var aviso = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.LowerRight, true);
                    aviso.normal.textColor = new Color(1f, 0.5f, 0.42f);
                    GUI.Label(new Rect(Screen.width - 360 * u, Screen.height - 122 * u, 340 * u, 24 * u),
                        desgaste >= 100 ? "ARMA QUEBRADA: USE UM KIT DE REPARO" : "EMPERROU", aviso);
                }
                if (Time.time < _semMunicaoAte)
                {
                    var aviso = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.LowerRight, true);
                    aviso.normal.textColor = new Color(1f, 0.5f, 0.42f);
                    GUI.Label(new Rect(Screen.width - 360 * u, Screen.height - 98 * u, 340 * u, 24 * u), $"SEM MUNIÇÃO {Itens.NomeDoCalibre(d.calibre).ToUpperInvariant()}", aviso);
                }
            }
            // o que se usa com E (pegar um item ou uma arma do painel, abrir a porta, acender a luz)
            if (_dica != null)
            {
                // com uma sombra embaixo, para ler na frente de coisa clara (a porta na luz da lanterna)
                // (a do saque tem o nome do item na cor da raridade: a sombra vai sem a cor)
                var dica = EstilosDoHud.Rotulo(Mathf.RoundToInt(17 * u), FontStyle.Normal, TextAnchor.UpperCenter, true);
                dica.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                float s = Mathf.Max(1f, Mathf.Round(1.5f * u));
                GUI.Label(new Rect(cx - 300 * u + s, cy + 40 * u + s, 600 * u, 30 * u), SemCor(_dica), dica);
                dica.normal.textColor = new Color(0.95f, 0.93f, 0.85f);
                GUI.Label(new Rect(cx - 300 * u, cy + 40 * u, 600 * u, 30 * u), _dica, dica);
                // segurando o E: a barra do tempo (e o que está fazendo)
                if (_segurando != null && _segurarTempo > 0f)
                {
                    float k = Mathf.Clamp01((Time.time - _segurarDesde) / _segurarTempo), w = 220 * u, h = Mathf.Max(3f, Mathf.Round(6 * u));
                    var barra = new Rect(cx - w / 2, cy + 74 * u, w, h);
                    Retangulo(barra.x - 1, barra.y - 1, barra.width + 2, barra.height + 2, new Color(0f, 0f, 0f, 0.6f));
                    Retangulo(barra.x, barra.y, barra.width * k, barra.height, new Color(0.95f, 0.85f, 0.45f, 0.95f));
                    string texto = _segurando.Segurando(this);
                    if (texto != null)
                    {
                        var pequeno = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Normal, TextAnchor.UpperCenter);
                        pequeno.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
                        GUI.Label(new Rect(cx - 300 * u, cy + 84 * u, 600 * u, 24 * u), texto, pequeno);
                    }
                }
            }
        }

        // o texto sem as marcas de cor do texto rico (<color=...> e </color>)
        static string SemCor(string t)
        {
            if (t.IndexOf('<') < 0) return t;
            var sb = new System.Text.StringBuilder(t.Length);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == '<')
                {
                    int f = t.IndexOf('>', i);
                    if (f > i && (string.CompareOrdinal(t, i, "<color", 0, 6) == 0 || string.CompareOrdinal(t, i, "</color", 0, 7) == 0))
                    {
                        i = f;
                        continue;
                    }
                }
                sb.Append(t[i]);
            }
            return sb.ToString();
        }

        // os textos do HUD da arma, refeitos só quando o que eles mostram muda (eram novos a cada quadro)
        string _textoDaMunicao, _nomeDaArma;
        int _municaoNoTexto = int.MinValue, _reservaNoTexto, _tamanhoNoTexto, _desgasteNoNome = -1;
        object _armaNoNome;

        string TextoDaMunicao(DadosDaArma d, int tamanho)
        {
            if (Recarregando && !d.cartuchoACartucho) return "RECARREGANDO";
            int reserva = SemFim ? -1 : Reserva;
            if (_textoDaMunicao == null || Municao != _municaoNoTexto || reserva != _reservaNoTexto || tamanho != _tamanhoNoTexto)
            {
                _municaoNoTexto = Municao;
                _reservaNoTexto = reserva;
                _tamanhoNoTexto = tamanho;
                _textoDaMunicao = $"{Municao}<size={tamanho}> / {(reserva < 0 ? "∞" : reserva.ToString())}</size>";
            }
            return _textoDaMunicao;
        }

        string NomeDaArma(DadosDaArma d, int desgaste)
        {
            if (_nomeDaArma == null || !ReferenceEquals(d, _armaNoNome) || desgaste != _desgasteNoNome)
            {
                _armaNoNome = d;
                _desgasteNoNome = desgaste;
                _nomeDaArma = $"{d.nome.ToUpperInvariant()}  ·  {Itens.NomeDoCalibre(d.calibre).ToUpperInvariant()}{(desgaste > 0 ? $"  ·  {100 - desgaste}%" : "")}";
            }
            return _nomeDaArma;
        }

        /// <summary>
        /// A luneta na tela: o círculo do tamanho da altura da tela, preto em volta (com a borda escurecendo para dentro)
        /// e o retículo: as linhas grossas vêm da borda e afinam perto do meio, com um ponto vermelho no centro.
        /// </summary>
        static void Luneta(float cx, float cy, float u)
        {
            if (_texturaDaLuneta == null) _texturaDaLuneta = TexturaDaLuneta();
            float lado = Screen.height;
            var r = new Rect(cx - lado / 2f, cy - lado / 2f, lado, lado);
            GUI.DrawTexture(r, _texturaDaLuneta);
            Retangulo(0f, 0f, r.x + 1f, Screen.height, Color.black);
            Retangulo(r.xMax - 1f, 0f, Screen.width - r.xMax + 1f, Screen.height, Color.black);
            var preto = new Color(0f, 0f, 0f, 0.92f);
            float fino = Mathf.Max(1f, Mathf.Round(1.5f * u)), grosso = Mathf.Round(5f * u), raio = lado * 0.47f, vao = lado * 0.15f;
            Retangulo(cx - raio, cy - fino / 2f, raio * 2f, fino, preto);
            Retangulo(cx - fino / 2f, cy - raio, fino, raio * 2f, preto);
            Retangulo(cx - raio, cy - grosso / 2f, raio - vao, grosso, preto);
            Retangulo(cx + vao, cy - grosso / 2f, raio - vao, grosso, preto);
            Retangulo(cx - grosso / 2f, cy + vao, grosso, raio - vao, preto);
            Retangulo(cx - grosso / 2f, cy - raio, grosso, raio - vao, preto);
            float ponto = Mathf.Max(2f, Mathf.Round(3f * u));
            Retangulo(cx - ponto / 2f, cy - ponto / 2f, ponto, ponto, new Color(0.85f, 0.12f, 0.08f, 0.95f));
        }

        // o círculo da luneta: transparente no meio, escurecendo perto da borda e preto fora
        static Texture2D TexturaDaLuneta()
        {
            const int n = 512;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.33f, 0.47f, d)) * 0.7f, Mathf.InverseLerp(0.465f, 0.475f, d));
                    px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Luneta", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
