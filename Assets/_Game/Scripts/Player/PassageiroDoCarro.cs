using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jogador dentro de um carro (em todas as cópias). Quem está em cada lugar vem do <see cref="Carro"/> (do
    /// anfitrião); aqui o jogador entra (a porta abre, ele vai até o banco e senta, a porta bate), fica sentado (preso
    /// ao banco, numa pose feita no código: o Animator do corpo fica desligado) e sai (a porta abre, ele desce do lado
    /// dela e a porta bate).
    /// <para>
    /// No dono: o movimento, a arma e os braços de primeira pessoa desligam; o mouse olha em volta de dentro do carro
    /// (ou, com V, a câmera vai para trás do carro); no volante, a partida é com a chave (olhar para a ignição, clicar
    /// para pôr a chave, segurar o clique e arrastar para girar: <see cref="Chave"/>), WASD dirige (só com o motor
    /// ligado), Espaço puxa o freio de mão, L liga os faróis, B buzina e R desvira o carro. F liga a lanterna, como
    /// fora. E sai (com o carro quase parado).
    /// </para>
    /// <para>
    /// Nos outros: a posição recebida pela rede é trocada, a cada quadro, pela do banco no carro desta máquina (o carro
    /// e o jogador chegam com atrasos diferentes, e o corpo sairia do carro andando rápido).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(100)]   // depois do JogadorRede (que gira o peito e a cabeça pelo olhar) e do carro
    public partial class PassageiroDoCarro : MonoBehaviour
    {
        // o olho de quem senta, a partir do quadril (no espaço do carro)
        static readonly Vector3 OlhoNoQuadril = new Vector3(0f, 0.70f, -0.08f);
        // entrando: a porta abre e, depois, ele vai até o banco; saindo, desce do banco até o lado da porta
        const float MoverDe = 0.25f, SairDe = 0.3f;
        const float MoverAte = Carro.FecharEm;

        static readonly int MascaraDoCenario = ~((1 << 2) | (1 << 16) | (1 << Carro.CamadaVeiculo) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
                                                 | (1 << JogadorRede.CamadaRetrato) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));

        static readonly string[] Ossos =
        {
            "hips", "spine", "chest", "neck", "head", "shoulder.L", "shoulder.R", "upper_arm.L", "upper_arm.R", "forearm.L",
            "forearm.R", "hand.L", "hand.R", "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R",
        };

        /// <summary>O carro e o lugar em que ele está sentado (null e -1 fora, e também descendo).</summary>
        public Carro Carro { get; private set; }
        public int Lugar { get; private set; } = -1;
        public bool Sentado => Carro != null;
        /// <summary>Entrando, sentado ou saindo: não anda, não atira, não larga nada.</summary>
        public bool NoCarro => _fase != Fase.Fora;
        /// <summary>Para os testes automáticos no Play (o motorista "aperta" o WASD sem o teclado); null no jogo.</summary>
        public static Vector2? DirecaoDeTeste;
        /// <summary>Para os testes automáticos no Play: o giro do olhar sentado (graus), no lugar do mouse; null no jogo.</summary>
        public static float? GiroDeTeste;

        enum Fase { Fora, Entrando, Dentro, Saindo }
        Fase _fase;
        float _tempo;
        Vector3 _deP;
        Quaternion _deR, _cabecaAntes;
        Vector3 _saida;
        float _yawSaida;
        Carro _carroSaindo, _carroIgnorado;
        int _lugarSaindo;
        float _ignorarAte;

        JogadorRede _rede;
        CharacterController _cc;
        MovimentoPrimeiraPessoa _movimento;
        ArmaJogador _arma;
        PassosJogador _passos;
        VigorDoJogador _vigor;
        bool _ccLigado;
        Transform _cabeca, _camera;
        Vector3 _cabecaLocal;
        float _yaw, _pitch;
        // a câmera de trás do carro (V): a mesma escolha da terceira pessoa a pé
        static bool _terceira
        {
            get => CameraTerceiraPessoa.Ligada;
            set => CameraTerceiraPessoa.Ligada = value;
        }
        float _yaw3, _pitch3 = 12f, _distancia3 = 6f, _mexeuNaCamera;
        string _aviso;
        float _avisoAte;
        // para medir a altura do aviso e da linha da ignição (26/11/2026, a pedido do Pedro: os textos cortados pela metade)
        readonly GUIContent _conteudoDoAviso = new GUIContent();
        // a chave: pega (um clique nela), o mouse gira a chave e não olha; na partida, o mini-game
        bool _segurandoChave, _naPartidaAntes, _fervendoAntes;
        // os avisos do desgaste (01/11/2026): o que estava assim no quadro anterior
        int _murchandoAntes, _furadosAntes = -1;
        bool _vazandoAntes, _reservaAntes, _semGasolinaAntes;
        float _engasgoAvisado = -99f;
        float _semForcaDesde = -1f;
        /// <summary>Graus que a chave gira por pixel do mouse, arrastando para a direita (da ignição para a partida ela pesa: <see cref="Carro.pesoDaPartida"/>).</summary>
        const float GrausPorPixel = 0.35f;
        // o mini-game da partida: um ponteiro dá voltas no anel do mostrador, e o clique tem de pegar ele em cima das
        // marcas (graus; a folga cobre o atraso do clique)
        const float VelocidadeDoPonteiro = 240f, VoltasDaPartida = 3f, LarguraPequena = 9f, LarguraGrande = 28f, Folga = 2f;
        struct Marca { public float centro, largura; public bool pequena, acertada; }
        readonly List<Marca> _marcas = new List<Marca>();
        float _ponteiro, _voltas, _errouEm = -99f;
        // com a chave do veículo, a partida é direta (a pedido em 29/10/2026): o motor de arranque gira este tempo (mais
        // com a vela gasta) e o motor pega, sem o mini-game, que ficou para a ligação direta (sem a chave)
        const float PartidaComChave = 0.8f;
        float _arranqueComChave;
        Texture2D _mostrador;
        bool _mostradorSujo = true, _mostradorNaPartida, _mostradorDireto;
        /// <summary>O quanto o olhar pode passar longe do miolo da ignição (graus) para a mira pegar a chave.</summary>
        const float MiraNaChave = 7f;
        InputAction _mover, _olhar, _interagir, _pular, _desvirar;

        // a pose: os ossos do corpo, o repouso deles (do prefab) e onde fica o quadril em pé, a partir da raiz
        readonly List<(Transform osso, Vector3 pos, Quaternion rot)> _repouso = new List<(Transform, Vector3, Quaternion)>();
        readonly Dictionary<string, Transform> _ossos = new Dictionary<string, Transform>();
        Vector3 _quadrilNaRaiz = new Vector3(0f, 0.888f, 0f);

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _cc = GetComponent<CharacterController>();
            _movimento = GetComponent<MovimentoPrimeiraPessoa>();
            _arma = GetComponent<ArmaJogador>();
            _passos = GetComponent<PassosJogador>();
            var acoes = InputSystem.actions;
            _mover = acoes.FindAction("Player/Move", true);
            _olhar = acoes.FindAction("Player/Look", true);
            _interagir = acoes.FindAction("Player/Interact", true);
            _pular = acoes.FindAction("Player/Jump", true);
            _desvirar = acoes.FindAction("Player/Reload", true);
        }

        void Start()
        {
            _cabeca = _movimento.cabeca;
            _camera = _rede.cameraMundo.transform;
            _cabecaLocal = _cabeca.localPosition;
            LerOssos();
            // o personagem pode chegar pela rede depois do Start (os outros jogadores): o corpo muda e os ossos também
            _rede.CorpoMudou += LerOssos;
        }

        /// <summary>Os ossos do corpo do personagem e o repouso deles (do prefab do jogador, a mesma pose do retrato do inventário).</summary>
        void LerOssos()
        {
            TroncoDeVolta();
            _alturaDaCabeca = -1f;
            _ossos.Clear();
            _repouso.Clear();
            foreach (var t in _rede.animadorCorpo.GetComponentsInChildren<Transform>(true)) _ossos[t.name] = t;
            var prefab = NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null
                ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab.transform : null;
            var corpoDoPrefab = prefab != null ? prefab.Find(_rede.animadorCorpo.name) : null;
            if (corpoDoPrefab == null) return;
            var doPrefab = new Dictionary<string, Transform>();
            foreach (var t in corpoDoPrefab.GetComponentsInChildren<Transform>(true)) doPrefab[t.name] = t;
            foreach (var nome in Ossos)
                if (_ossos.TryGetValue(nome, out var osso) && doPrefab.TryGetValue(nome, out var repouso))
                    _repouso.Add((osso, repouso.localPosition, repouso.localRotation));
            if (doPrefab.TryGetValue("hips", out var quadril)) _quadrilNaRaiz = prefab.InverseTransformPoint(quadril.position);
            // as dobradiças (16/11/2026): no repouso, o joelho aponta para a frente e o cotovelo para trás
            _dobradica.Clear();
            _palma.Clear();
            _empunhaduras.Clear();
            _bracosDoCorpoEncolhidos = false;   // (os ossos novos vêm no tamanho de sempre)
            foreach (var l in new[] { ".L", ".R" })
            {
                GuardarDobradica(doPrefab, "thigh" + l, "shin" + l, "foot" + l, corpoDoPrefab.forward);
                GuardarDobradica(doPrefab, "upper_arm" + l, "forearm" + l, null, -corpoDoPrefab.forward);
                // a palma, no repouso, olha para a coxa (para dentro)
                if (_ossos.TryGetValue("hand" + l, out var mao) && doPrefab.TryGetValue("hand" + l, out var maoDoPrefab))
                    _palma[mao] = Quaternion.Inverse(maoDoPrefab.rotation) * (corpoDoPrefab.right * (l == ".R" ? -1f : 1f));
            }
            // (19/11/2026, quem toca: PassageiroDoCarro.NoChao.cs) o repouso das mãos, dos dedos e dos polegares, e a boca e os
            // eixos do rosto no espaço da cabeça
            _maosNoRepouso.Clear();
            foreach (var l in new[] { ".L", ".R" })
                foreach (var dedo in new[] { "hand", "index1", "index2", "middle1", "middle2", "ring1", "ring2", "pinky1", "pinky2", "thumb", "thumb2" })
                    if (_ossos.TryGetValue(dedo + l, out var osso) && doPrefab.TryGetValue(dedo + l, out var doRepouso)) _maosNoRepouso[osso] = doRepouso.localRotation;
            _temRosto = doPrefab.TryGetValue("head", out var cabecaDoPrefab);
            if (_temRosto)
            {
                var volta = Quaternion.Inverse(cabecaDoPrefab.rotation);
                _bocaNaCabeca = volta * (corpoDoPrefab.up * 0.03f + corpoDoPrefab.forward * 0.11f);
                _frenteNaCabeca = volta * corpoDoPrefab.forward;
                _cimaNaCabeca = volta * corpoDoPrefab.up;
            }
        }

        // ------------------------------------------------------------------------------------------ as juntas
        // (16/11/2026, a pedido: "não é anatômico, não é correto"). O joelho e o cotovelo são dobradiças: a canela e o
        // antebraço só dobram em volta de um eixo, o mesmo da coxa e do braço. Antes cada osso girava pelo caminho mais
        // curto até a direção nova e o joelho torcia (até 98° na perna dobrada da caçamba) e dobrava de lado; a calça e
        // a manga amassavam. Agora cada osso recebe a direção e o eixo da dobradiça (o do repouso, levado junto), o pé
        // fica no plano da perna e a palma vira girando o antebraço no próprio eixo (como o rádio gira em volta da ulna).

        /// <summary>O eixo da dobradiça de cada osso dos membros, no espaço do próprio osso (do repouso do prefab).</summary>
        readonly Dictionary<Transform, Vector3> _dobradica = new Dictionary<Transform, Vector3>();
        /// <summary>Para onde a palma de cada mão olha, no espaço da mão (do repouso: para a coxa).</summary>
        readonly Dictionary<Transform, Vector3> _palma = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// A dobradiça do membro no repouso do prefab: o eixo perpendicular ao membro (do começo até a ponta do segundo osso)
        /// e ao lado para onde a junta do meio aponta (<paramref name="bojo"/>: o joelho para a frente, o cotovelo para
        /// trás), guardado no espaço de cada osso (e no do pé, que dobra no mesmo eixo da canela).
        /// </summary>
        void GuardarDobradica(Dictionary<string, Transform> doPrefab, string de, string meio, string ponta, Vector3 bojo)
        {
            if (!doPrefab.TryGetValue(de, out var a) || !doPrefab.TryGetValue(meio, out var b) || b.childCount == 0) return;
            Vector3 membro = b.GetChild(0).position - a.position;
            Vector3 eixo = Vector3.Cross(Vector3.ProjectOnPlane(bojo, membro), membro);
            if (eixo.sqrMagnitude < 1e-10f) return;
            eixo.Normalize();
            if (_ossos.TryGetValue(de, out var vivoA)) _dobradica[vivoA] = Quaternion.Inverse(a.rotation) * eixo;
            if (_ossos.TryGetValue(meio, out var vivoB)) _dobradica[vivoB] = Quaternion.Inverse(b.rotation) * eixo;
            if (ponta != null && doPrefab.TryGetValue(ponta, out var c) && _ossos.TryGetValue(ponta, out var vivoC))
                _dobradica[vivoC] = Quaternion.Inverse(c.rotation) * eixo;
        }

        // ------------------------------------------------------------------------------------------ entrar e sair

        /// <summary>Senta no lugar do carro (chamado pelo carro, em cada máquina). Animado = acabou de entrar.</summary>
        public void Sentar(Carro carro, int lugar, bool animado)
        {
            if (carro == _carroIgnorado && Time.time < _ignorarAte) return;
            if (Carro == carro && Lugar == lugar) return;
            if (_fase != Fase.Fora) Restaurar();
            Carro = carro;
            Lugar = lugar;
            _fase = animado ? Fase.Entrando : Fase.Dentro;
            _tempo = 0f;
            _deP = transform.position;
            _deR = transform.rotation;
            _ccLigado = _cc.enabled;
            _cc.enabled = false;   // dentro do carro, a cápsula do jogador empurraria o carro
            _passos.enabled = false;
            // o que está na mão some só com quem dirige (08/11/2026: o garupa da moto atira e usa itens, e os outros veem;
            // 10/11/2026: os passageiros do carro também)
            _rede.EsconderNaMao(lugar == 0);
            _maosLivres = false;
            if (!_rede.IsOwner) return;
            _movimento.enabled = false;
            _arma.enabled = false;
            _rede.bracos.SetActive(false);
            _rede.cameraMundo.fieldOfView = _arma.fovNormal;
            _cabecaAntes = _cabeca.localRotation;
            _yaw = 0f;
            _pitch = 6f;
            _yaw3 = 0f;
            ZerarOGiro();
        }

        /// <summary>Sai do carro (chamado pelo carro quando o lugar fica vazio). Animado = acabou de sair.</summary>
        public void Levantar(bool animado)
        {
            if (!Sentado) return;
            _segurandoChave = false;
            _carroSaindo = Carro;
            _lugarSaindo = Lugar;
            Carro = null;
            Lugar = -1;
            if (_rede.IsOwner) PontoDeSaida(_carroSaindo, _lugarSaindo, out _saida, out _yawSaida);
            if (!animado)
            {
                Restaurar();
                if (_rede.IsOwner) _movimento.Teleportar(_saida, _yawSaida);
                return;
            }
            _fase = Fase.Saindo;
            _tempo = 0f;
        }

        /// <summary>Sai na hora, onde estiver, sem porta (caiu; quem chama põe o jogador no lugar dele).</summary>
        public void Soltar()
        {
            if (_fase == Fase.Fora) return;
            _carroIgnorado = Carro != null ? Carro : _carroSaindo;
            _ignorarAte = Time.time + 2f;   // até o lugar vazio chegar pela rede
            Carro = null;
            Lugar = -1;
            Restaurar();
        }

        // volta ao normal: o Animator, a cápsula, os passos, o que está na mão e, no dono, os comandos e a câmera
        void Restaurar()
        {
            _fase = Fase.Fora;
            _segurandoChave = false;
            _carroSaindo = null;
            _maosLivres = false;
            TroncoDeVolta();
            _rede.animadorCorpo.enabled = true;
            if (TemLugar(_rede.animadorCorpo))
            {
                _rede.animadorCorpo.SetInteger(LugarId, 0);
                if (_rede.animadorCorpo.layerCount > 1) _rede.animadorCorpo.SetLayerWeight(1, 1f);
            }
            _cc.enabled = _ccLigado;
            _passos.enabled = true;
            _rede.EsconderNaMao(false);
            if (!_rede.IsOwner)
            {
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                return;
            }
            _movimento.enabled = true;
            _arma.enabled = true;
            _rede.bracos.SetActive(true);
            if (_bracosDoPilotoEscondidos)
            {
                _bracosDoPilotoEscondidos = false;
                if (_rede.cameraBracos != null && !_rede.EmTerceiraPessoa) _rede.cameraBracos.enabled = true;
            }
            EncolherBracosDoCorpo(false);
            _cabeca.localPosition = _cabecaLocal;
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            _rede.CorpoNoVeiculo(false);
            CorpoVisivel(false);
            ZerarOGiro();
        }

        // no dono: o giro do olhar sentado (pela rede) volta para a frente
        void ZerarOGiro()
        {
            if (_rede.IsSpawned && _rede.IsOwner && _rede.GiroSentado.Value != 0f) _rede.GiroSentado.Value = 0f;
        }

        // onde o jogador desce: do lado da porta dele, no chão e sem nada no caminho; senão, pelas outras portas, atrás
        // ou na frente do carro
        static void PontoDeSaida(Carro c, int lugar, out Vector3 ponto, out float yaw)
        {
            yaw = c.transform.eulerAngles.y;
            var candidatos = new List<Vector3>();
            foreach (int l in new[] { lugar, lugar ^ 1, lugar ^ 2, lugar ^ 3 })
                if (c.saidas != null && l < c.saidas.Length && c.saidas[l] != null) candidatos.Add(c.saidas[l].position);
            candidatos.Add(c.transform.TransformPoint(new Vector3(0f, 0f, -3.3f)));
            candidatos.Add(c.transform.TransformPoint(new Vector3(0f, 0f, 3.3f)));
            foreach (var p in candidatos)
            {
                if (!Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out var chao, 3.5f, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                Vector3 pe = chao.point;
                if (Physics.CheckCapsule(pe + Vector3.up * 0.4f, pe + Vector3.up * 1.5f, 0.3f, MascaraDoCenario | (1 << Carro.CamadaVeiculo), QueryTriggerInteraction.Ignore)) continue;
                ponto = pe;
                return;
            }
            ponto = candidatos.Count > 0 ? candidatos[0] : c.transform.position + Vector3.up;
        }

        // ------------------------------------------------------------------------------------------ cada quadro

        void LateUpdate()
        {
            if (_fase == Fase.Fora)
            {
                SentadoNoChao();   // (sentado no chão perto do fogo, 18/11/2026: PassageiroDoCarro.NoChao.cs)
                PoseDoInstrumento();   // (tocando, 18/11/2026: PassageiroDoCarro.Instrumento.cs)
                GolpeDoMixamoNoCorpo();   // (o teste do golpe do Mixamo, 19/11/2026: ArmaJogador.GolpeDoMixamo.cs)
                return;
            }
            float dt = Time.deltaTime;
            _tempo += dt;
            bool dono = _rede.IsOwner;
            if (_fase == Fase.Saindo)
            {
                Saindo(dono, dt);
                return;
            }
            if (Carro == null || Lugar < 0 || Carro.assentos == null || Lugar >= Carro.assentos.Length)
            {
                Restaurar();
                return;
            }
            var banco = Carro.assentos[Lugar];
            Vector3 alvoP = banco.position - banco.rotation * _quadrilNaRaiz;
            float k = _fase == Fase.Entrando ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MoverDe, MoverAte, _tempo)) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(_deP, alvoP, k), Quaternion.Slerp(_deR, banco.rotation, k));
            if (_fase == Fase.Dentro || _tempo >= MoverDe)
            {
                AcertarNaMao();
                Pose(Carro, Lugar);
                if (!Carro.moto && !Carro.NaCacamba(Lugar)) CaberNoTeto(Carro);   // (na caçamba não há teto)
                if (Lugar == 0 && !Carro.moto) MaosNoVolante(Carro);   // (em todas as máquinas: os outros veem também)
                // o passageiro do carro e quem vai na caçamba com algo na mão (10/11/2026: ele atira e golpeia, como o garupa
                // da moto): os braços com a arma, por cima da pose de sentado (na moto, dentro da PoseDaMoto)
                else if (!Carro.moto) BracosComArma(Carro, Lugar);
                CabecaVirada();
            }
            if (_fase == Fase.Entrando && _tempo >= MoverAte) _fase = Fase.Dentro;
            if (!dono) return;
            // o vigor volta sentado como parado (16/11/2026, a pedido: "a estamina não recupera quando estou sentado": ele só
            // voltava no MovimentoPrimeiraPessoa, que fica desligado no veículo; o vigor nasce na primeira vez que ele anda)
            if (_vigor == null && (_vigor = GetComponent<VigorDoJogador>()) == null) _vigor = gameObject.AddComponent<VigorDoJogador>();
            _vigor.Passo(dt, false, false, false, true);
            BracosDoCorpoNaPrimeiraPessoa();
            Comandos();
            if (_fase == Fase.Dentro && _terceira) CameraDeTras(dt);
            else Olhar(k);
            CuidarDasMaos();
        }

        // ------------------------------------------------------------------------------------------ as mãos livres (08/11/2026)

        bool _maosLivres;

        /// <summary>
        /// Sentado com as mãos livres (08/11/2026, a pedido: "poder atirar e usar item de cura na garupa da moto, ou poder usar
        /// itens dentro do carro"; 10/11/2026: os passageiros do carro também atiram e golpeiam): o garupa da moto e os
        /// passageiros do carro. Os braços de primeira pessoa e o
        /// <see cref="ArmaJogador"/> voltam (o que está na mão, sacando). Na câmera de trás (V), não. O motorista, não: o
        /// clique dele é da chave (a ignição), e as mãos dele ficam no volante e no guidão.
        /// </summary>
        public bool MaosLivres => _maosLivres;

        /// <summary>
        /// Com as mãos livres, também atira e golpeia: o garupa da moto e, desde 10/11/2026, os passageiros do carro (a pedido:
        /// atirar de dentro do carro). O tiro e o golpe pulam o próprio veículo (<see cref="ArmaJogador"/>, o MeuVeiculo), e os
        /// outros o veem com a arma na frente do peito, para onde ele olha (<see cref="BracosComArma"/>). O motorista, não.
        /// </summary>
        public bool PodeAtacar => _maosLivres && Carro != null && (Lugar != 0 || Carro.moto);

        bool MaosLivresAgora()
        {
            // (desde 16/11/2026 também na câmera de trás, a pedido: "em terceira pessoa não consigo mirar e nem atirar")
            if (Carro == null || _fase != Fase.Dentro) return false;
            // na moto, a arma grande fica guardada (15/11/2026, a pedido: "o correto para a moto seria poder utilizar somente
            // arma pequena ... o resto fica impedido por ser muito grande")
            if (Carro.moto && GrandeParaAMoto(NaMao)) return false;
            return Lugar != 0 || PilotoArmado;
        }

        /// <summary>
        /// O piloto da moto atira (16/11/2026, a pedido: "motorista com pistola: uma mão permanece no guidão, a outra segura
        /// a pistola"; como em GTA V e Days Gone): com uma pistola ou uma submetralhadora na mão e o motor ligado. A mão
        /// esquerda fica no guidão; a direita sai do guidão quando ele mira ou atira (<see cref="BracosComArma"/>). Com a
        /// arma na mão, o clique é do tiro, não da chave: para desligar, ele guarda a arma.
        /// </summary>
        bool PilotoArmado => Carro != null && Carro.moto && Lugar == 0 && ArmaDoPiloto(NaMao) && Carro.MotorLigado && !_segurandoChave;

        /// <summary>O que o piloto da moto empunha: as armas de fogo pequenas (as pistolas e as submetralhadoras).</summary>
        public static bool ArmaDoPiloto(Loot.Item i) => Loot.Itens.EhArma(i) && !GrandeParaAMoto(i);

        /// <summary>
        /// No dono: a arma que ele vê é a do corpo, não a dos braços de primeira pessoa (o clarão do tiro sai dela): o piloto
        /// armado (o braço do próprio corpo) e, desde 16/11/2026, quem atira na câmera de trás.
        /// </summary>
        public bool ArmaNoCorpo => _maosLivres && Carro != null && (_terceira || (Carro.moto && Lugar == 0));
        /// <summary>No dono: sentado, com a câmera de trás (o V). O tiro sai do meio da tela, a partir da altura do corpo (<see cref="ArmaJogador"/>).</summary>
        public bool NaCameraDeTras => _terceira && _fase == Fase.Dentro && Carro != null;
        /// <summary>Para os testes automáticos no Play: a câmera de trás ligada ou não, no lugar do V; null no jogo.</summary>
        public static bool? CameraDeTrasDeTeste;
        float _ombroDaCamera;
        /// <summary>Com algo na mão, sentado na primeira pessoa: até onde a mira vai para os lados e para baixo (graus).</summary>
        const float GiroArmado = 110f, BaixoArmado = 40f;
        bool _bracosDoPilotoEscondidos;

        Loot.Item NaMao => _rede.Inventario != null ? _rede.Inventario.NaMao : Loot.Item.Nada;

        /// <summary>
        /// O que não cabe na garupa da moto (15/11/2026): as armas de fogo longas (o rifle, a espingarda, a sniper e o rifle de
        /// sucata) e as brancas compridas (o machado, o bastão, a lança, a tocha e o taco). Ficam as pistolas, as submetralhadoras,
        /// a faca e o que não é arma (a comida, os remédios, a lanterna...).
        /// </summary>
        public static bool GrandeParaAMoto(Loot.Item i) =>
            (Loot.Itens.EhArma(i) && i != Loot.Item.Pistola && i != Loot.Item.PistolaSucata && i != Loot.Item.Submetralhadora && i != Loot.Item.SubmetralhadoraSucata)
            || (Loot.Itens.EhBranca(i) && i != Loot.Item.Faca) || Loot.Itens.EhTaco(i);

        bool _grandeNaMotoAvisado;

        // no dono, a cada quadro: liga ou desliga as mãos (o ArmaJogador e os braços)
        void CuidarDasMaos()
        {
            // na garupa com uma arma grande na mão: o aviso, uma vez a cada troca
            bool grande = Carro != null && Carro.moto && Lugar != 0 && _fase == Fase.Dentro && GrandeParaAMoto(NaMao);
            if (grande && !_grandeNaMotoAvisado) Avisar("Na moto, só armas pequenas: as pistolas, as submetralhadoras e a faca", 3f);
            _grandeNaMotoAvisado = grande;
            bool livres = MaosLivresAgora();
            if (livres != _maosLivres)
            {
                _maosLivres = livres;
                _arma.enabled = livres;
                _rede.bracos.SetActive(livres);
                if (livres) _arma.MostrarModeloDaMao(true);
            }
            // o piloto armado (16/11/2026): os braços de primeira pessoa (as duas mãos na arma) não aparecem; na moto ele
            // vê o próprio corpo, com a esquerda no guidão e a direita com a arma (como a câmera de trás, o capô e a sinuca)
            var camera = _rede.cameraBracos;
            if (camera == null) return;
            if (ArmaNoCorpo)
            {
                if (camera.enabled) camera.enabled = false;
                _bracosDoPilotoEscondidos = true;
            }
            else if (_bracosDoPilotoEscondidos)
            {
                _bracosDoPilotoEscondidos = false;
                if (!_rede.EmTerceiraPessoa) camera.enabled = !_arma.NaLuneta;
            }
        }

        void Saindo(bool dono, float dt)
        {
            var c = _carroSaindo;
            if (c == null || _lugarSaindo < 0 || c.assentos == null || _lugarSaindo >= c.assentos.Length)
            {
                Restaurar();
                return;
            }
            var banco = c.assentos[_lugarSaindo];
            Vector3 noBanco = banco.position - banco.rotation * _quadrilNaRaiz;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SairDe, MoverAte, _tempo));
            // o dono desce do banco até o chão; os outros seguem a posição dele pela rede (até ela começar, o banco)
            if (dono) transform.SetPositionAndRotation(Vector3.Lerp(noBanco, _saida, s), Quaternion.Slerp(banco.rotation, Quaternion.Euler(0f, _yawSaida, 0f), s));
            else if (_tempo < SairDe + 0.15f) transform.SetPositionAndRotation(noBanco, banco.rotation);
            if (_tempo < MoverAte)
            {
                Pose(c, _lugarSaindo);
                if (!c.moto && !c.NaCacamba(_lugarSaindo)) CaberNoTeto(c);
            }
            if (dono)
            {
                _camera.localPosition = Vector3.zero;
                _camera.localRotation = Quaternion.identity;
                CorpoVisivel(false);
                Olhar(1f - s);
            }
            if (_tempo < MoverAte) return;
            Restaurar();
            if (dono) _movimento.Teleportar(_saida, _yawSaida);
        }

        // ------------------------------------------------------------------------------------------ o dono

        void Comandos()
        {
            bool controla = MenuOnline.Controla;
            var teclado = Keyboard.current;
            if (Lugar == 0 && Carro.IsOwner)
            {
                // entrando (ou saindo), o motorista ainda não dirige: o carro deixado ligado (05/11/2026) não arranca com a
                // porta aberta; fica no freio de mão até ele sentar
                bool sentado = _fase == Fase.Dentro;
                Vector2 m = !sentado ? Vector2.zero : DirecaoDeTeste ?? (controla ? _mover.ReadValue<Vector2>() : Vector2.zero);
                // o corpo ruim no volante e no pedal (05/11/2026: com a tala, dirige "porém com dificuldades"): o braço
                // quebrado vira menos e o carro puxa devagar para um lado; a perna direita ruim acelera menos
                if (sentado && (controla || DirecaoDeTeste.HasValue) && _rede.Vida != null) m = PartesDoCorpo.NoVolante(_rede.Vida.Corpo.Value, m, Time.time);
                Carro.Comandar(m.y, m.x, !sentado || (controla && _pular.IsPressed()));
                Chave(controla);
                if (Carro.Fervendo && !_fervendoAntes) Avisar("O motor ferveu e apagou: espere esfriar e troque o radiador (capô)", 4f);
                _fervendoAntes = Carro.Fervendo;
                AvisosDoDesgaste();
                if (m.y > 0.3f && !Carro.MotorLigado && !_segurandoChave && Time.time > _avisoAte)
                    Avisar(Carro.Estado == Carro.EstadoDoMotor.SemChave ? "O motor está desligado: ponha a chave na ignição" : "O motor está desligado: gire a chave");
                if (controla && teclado != null && teclado.lKey.wasPressedThisFrame) Carro.AlternarFarois();
                Carro.Buzinar(controla && teclado != null && teclado.bKey.isPressed);
                if (controla && _desvirar.WasPressedThisFrame() && Carro.PodeDesvirar) Carro.Desvirar();
            }
            if (CameraDeTrasDeTeste.HasValue) _terceira = CameraDeTrasDeTeste.Value;
            if (!controla) return;
            if (teclado != null && teclado.vKey.wasPressedThisFrame) _terceira = !_terceira;
            // trocar de lugar (16/11/2026): Ctrl+1 dirige, Ctrl+2 o lado do motorista ou a garupa, Ctrl+3 e Ctrl+4 atrás
            int outro = LugarComCtrl(teclado);
            if (_fase == Fase.Dentro && outro >= 0 && outro != Lugar)
            {
                if (outro >= Mathf.Min(Vehicles.Carro.Lugares, Carro.lugaresUsados)) Avisar(Carro.moto ? "A moto só tem dois lugares" : "Esse lugar não existe aqui");
                else if (Carro.Ocupado(outro)) Avisar("Esse lugar está ocupado");
                else if (Carro.Velocidade > Carro.VelocidadeParaSair) Avisar($"Pare {Carro.OVeiculo} para trocar de lugar");
                else Carro.PedirTrocarDeLugar(outro);
            }
            if (_fase == Fase.Dentro && _interagir.WasPressedThisFrame())
            {
                if (Carro.Velocidade > Carro.VelocidadeParaSair) Avisar($"Pare {Carro.OVeiculo} para sair");
                else Carro.PedirSair();
            }
        }

        /// <summary>O lugar pedido com Ctrl+1 a Ctrl+4 neste quadro (de 0 a 3), ou -1.</summary>
        public static int LugarComCtrl(Keyboard teclado)
        {
            if (teclado == null || !teclado.ctrlKey.isPressed) return -1;
            if (teclado.digit1Key.wasPressedThisFrame || teclado.numpad1Key.wasPressedThisFrame) return 0;
            if (teclado.digit2Key.wasPressedThisFrame || teclado.numpad2Key.wasPressedThisFrame) return 1;
            if (teclado.digit3Key.wasPressedThisFrame || teclado.numpad3Key.wasPressedThisFrame) return 2;
            if (teclado.digit4Key.wasPressedThisFrame || teclado.numpad4Key.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>O olhar do motorista (na primeira pessoa) está no miolo da ignição.</summary>
        bool MirandoAChave()
        {
            if (Lugar != 0 || _fase != Fase.Dentro || _terceira || Carro == null || Carro.ignicao == null) return false;
            Vector3 para = Carro.ignicao.position - _camera.position;
            return para.magnitude < 1.5f && Vector3.Angle(_camera.forward, para) < MiraNaChave;
        }

        /// <summary>
        /// A chave: olhando para a ignição, um clique põe a chave (se o motorista tem a chave do veículo na mochila,
        /// <see cref="Carro.chaveDoVeiculo"/>); outro pega a chave (o mouse para de olhar e aparece o mostrador). Arrastar
        /// gira: para a direita, a ignição e a partida (da ignição para a partida ela pesa e volta sozinha, é preciso
        /// empurrar; chegando, fica presa); para a esquerda, desliga. Presa na partida, o motor de arranque gira um pouco
        /// (<see cref="PartidaComChave"/>, mais com a vela gasta) e o motor pega, sem mini-game.
        /// Sem a chave, o clique faz a ligação direta (os fios juntos: o arranque gira na hora) e começa o mini-game: um
        /// ponteiro dá voltas no anel, e o clique tem de pegar ele em cima das marcas. A pequena (dourada) liga na hora; as
        /// 3 grandes (laranja) ligam quando todas foram acertadas, e errar apaga as já acertadas. Em
        /// <see cref="VoltasDaPartida"/> voltas sem ligar, os fios param na ignição (o clique tenta de novo). Ligado pela
        /// direta, o clique na ignição separa os fios e desliga. Ligou, a chave volta sozinha para a ignição e é solta.
        /// Fora da partida, o clique (ou, sempre, o botão direito) solta a chave; puxar para a esquerda na partida desiste.
        /// </summary>
        void Chave(bool controla)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!_segurandoChave)
            {
                if (!controla || !mouse.leftButton.wasPressedThisFrame || !MirandoAChave()) return;
                // o piloto armado: o clique é do tiro (16/11/2026)
                if (_maosLivres)
                {
                    Avisar("Guarde a arma para mexer na chave");
                    return;
                }
                if (Carro.LigacaoDireta)
                {
                    // os fios juntos: com o motor ligado, o clique separa os fios (desliga); parado, tenta de novo (ou,
                    // com a chave achada depois, separa os fios e põe a chave)
                    if (Carro.MotorLigado)
                    {
                        Carro.DesfazerLigacaoDireta();
                        Avisar("Fios separados: o motor desligou");
                    }
                    else if (TemAChave)
                    {
                        Carro.DesfazerLigacaoDireta();
                        Carro.PorAChave();
                    }
                    else if (SemALigacaoDireta()) { }
                    else
                    {
                        Carro.FazerLigacaoDireta();
                        Segurar();
                    }
                    return;
                }
                if (Carro.Estado == Carro.EstadoDoMotor.SemChave)
                {
                    // com a chave do veículo na mochila, ela entra na ignição; sem ela, a ligação direta (o mini-game)
                    if (TemAChave) Carro.PorAChave();
                    else if (SemALigacaoDireta()) { }
                    else
                    {
                        Carro.FazerLigacaoDireta();
                        Segurar();
                    }
                    return;
                }
                Segurar();
                return;
            }
            if (!controla || _terceira || _fase != Fase.Dentro || mouse.rightButton.wasPressedThisFrame)
            {
                SoltarAChave();
                return;
            }
            // na ligação direta, puxar até o fim separa os fios: não sobra nada para segurar
            if (Carro.Estado == Carro.EstadoDoMotor.SemChave)
            {
                _segurandoChave = false;
                _naPartidaAntes = false;
                Avisar("Fios separados");
                return;
            }
            // a chave na partida sem a bateria ter força: o arranque não gira, e a chave volta para a ignição
            if (Carro.Estado == Carro.EstadoDoMotor.Arranque && !Carro.ArranqueGira)
            {
                if (_semForcaDesde < 0f) _semForcaDesde = Time.time;
                else if (Time.time - _semForcaDesde > 0.6f)
                {
                    _semForcaDesde = -1f;
                    Carro.SoltarChave();
                    Avisar(Carro.Pecas.BateriaAcende ? "A bateria está fraca: o motor de arranque não gira (abra o capô)"
                        : Carro.Pecas.Tem(Vehicles.PecasDoMotor.Bateria) ? "A bateria está morta: nada acende (abra o capô)" : "Sem bateria: nada acende (abra o capô)", 3f);
                }
            }
            else _semForcaDesde = -1f;
            bool partida = Carro.ArranqueGira;
            if (partida != _naPartidaAntes)
            {
                _naPartidaAntes = partida;
                if (partida) NovaPartida();
                _mostradorSujo = true;
            }
            if (partida && !Carro.LigacaoDireta)
            {
                // com a chave: o motor de arranque gira um pouco (mais com a vela gasta) e o motor pega (ou só tosse:
                // a vela queimada, o motor quente; Ligou avisa)
                _arranqueComChave += Time.deltaTime;
                if (_arranqueComChave >= PartidaComChave / Mathf.Max(0.3f, Carro.Pecas.FatorDasMarcas))
                {
                    Ligou();
                    return;
                }
            }
            else if (partida)
            {
                _ponteiro += VelocidadeDoPonteiro * Time.deltaTime;
                if (_ponteiro >= 360f)
                {
                    _ponteiro -= 360f;
                    _voltas++;
                }
                if (_voltas >= VoltasDaPartida)
                {
                    Carro.SoltarChave();
                    Avisar("O motor não pegou: tente de novo");
                    return;
                }
                if (mouse.leftButton.wasPressedThisFrame && Clicar()) return;
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                SoltarAChave();
                return;
            }
            Carro.GirarChave(mouse.delta.ReadValue().x * GrausPorPixel);
        }

        // pega a chave (ou os fios, na ligação direta): o mouse gira e aparece o mostrador
        void Segurar()
        {
            _segurandoChave = true;
            _naPartidaAntes = false;
            _mostradorSujo = true;
        }

        /// <summary>"sem a chave do cupê" (o nome da chave deste veículo), para as dicas da ignição.</summary>
        string SemAChave => Carro != null && Carro.chaveDoVeiculo != Loot.Item.Nada ? "sem " + Loot.Itens.NomeComArtigo(Carro.chaveDoVeiculo) : "sem a chave";

        /// <summary>O motorista (esta máquina) tem a chave deste veículo na mochila.</summary>
        bool TemAChave => Carro != null && _rede != null && _rede.Inventario != null
                          && PortaMalasDoCarro.Achar(_rede.Inventario, Carro.chaveDoVeiculo, out _);

        void SoltarAChave()
        {
            _segurandoChave = false;
            _naPartidaAntes = false;
            if (Carro != null) Carro.SoltarChave();
        }

        // as marcas de uma partida: a pequena e as 3 grandes, em lugares sorteados, longe umas das outras e do começo do
        // ponteiro (em cima)
        void NovaPartida()
        {
            _arranqueComChave = 0f;
            _ponteiro = 0f;
            _voltas = 0f;
            _marcas.Clear();
            for (int i = 0; i < 4; i++)
            {
                // a vela gasta deixa as marcas menores
                float largura = (i == 0 ? LarguraPequena : LarguraGrande) * Carro.Pecas.FatorDasMarcas;
                float centro = 0f;
                for (int tentativa = 0; tentativa < 80; tentativa++)
                {
                    centro = Random.Range(50f + largura / 2f, 350f - largura / 2f);
                    bool livre = true;
                    foreach (var m in _marcas)
                        if (Mathf.Abs(Mathf.DeltaAngle(m.centro, centro)) < (m.largura + largura) / 2f + 14f) { livre = false; break; }
                    if (livre) break;
                }
                _marcas.Add(new Marca { centro = centro, largura = largura, pequena = i == 0 });
            }
        }

        // o clique da partida, com o ponteiro onde está: true se o motor pegou
        bool Clicar()
        {
            for (int i = 0; i < _marcas.Count; i++)
            {
                var m = _marcas[i];
                if (Mathf.Abs(Mathf.DeltaAngle(m.centro, _ponteiro)) > m.largura / 2f + Folga) continue;
                if (m.acertada) return false;   // de novo na mesma: não conta, nem como erro
                if (m.pequena) return Ligou();
                m.acertada = true;
                _marcas[i] = m;
                _mostradorSujo = true;
                foreach (var outra in _marcas)
                    if (!outra.pequena && !outra.acertada)
                    {
                        Carro.Tossir();
                        return false;
                    }
                return Ligou();
            }
            // errou: as grandes já acertadas apagam
            _errouEm = Time.time;
            for (int i = 0; i < _marcas.Count; i++)
            {
                var m = _marcas[i];
                m.acertada = false;
                _marcas[i] = m;
            }
            _mostradorSujo = true;
            return false;
        }

        // a ligação direta é a ponta do Ladrão (06/11/2026, a pedido: "A ligação direta que está hoje passaria a exigir a
        // árvore do ladrão"): sem ela, o aviso, e o carro só liga com a chave
        bool SemALigacaoDireta()
        {
            if (Habilidades.Tem(Habilidade.LigacaoDireta)) return false;
            Avisar("Sem a chave, não liga: a ligação direta é a ponta do Ladrão (K: habilidades)", 3.5f);
            return true;
        }

        bool Ligou()
        {
            _segurandoChave = false;
            _naPartidaAntes = false;
            if (!Carro.PegarAgora(out var motivo))
            {
                // a vela ruim (ou o motor quente): ele só tosse, e a chave volta para a ignição
                Carro.SoltarChave();
                Avisar(motivo, 3f);
                return true;
            }
            Avisar("Motor ligado");
            if (Carro.LigacaoDireta) GetComponent<InventarioJogador>()?.LigouDireto(Carro);   // (o XP, 06/11/2026)
            return true;
        }

        void OnDestroy()
        {
            if (_mostrador != null) Destroy(_mostrador);
            if (_rede != null) _rede.CorpoMudou -= LerOssos;
        }

        Vector2 LerOlhar()
        {
            if (!MenuOnline.Controla) return Vector2.zero;
            Vector2 bruto = _olhar.ReadValue<Vector2>();
            return _olhar.activeControl != null && _olhar.activeControl.device is Gamepad
                ? bruto * (_movimento.sensibilidadeControle * Time.deltaTime) : bruto * _movimento.sensibilidadeMouse;
        }

        // primeira pessoa: o olho no banco; o mouse olha em volta (sem virar para trás de vez). k = o quanto já sentou
        void Olhar(float k)
        {
            var c = Carro != null ? Carro : _carroSaindo;
            // na moto, o corpo sem a cabeça e sem o pescoço (as mãos no guidão: JogadorRede.CorpoNoVeiculo); no carro, só a
            // sombra, como antes (07/11/2026, a pedido: "do carro em primeira pessoa, tira e deixa como estava antes sem
            // nada"); descendo, some
            bool comCorpo = c != null && c.moto && _fase != Fase.Saindo;
            if (comCorpo) _rede.CorpoNoVeiculo(true);
            else
            {
                _rede.CorpoNoVeiculo(false);
                CorpoVisivel(false);
            }
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            if (_fase == Fase.Dentro && !_segurandoChave)
            {
                Vector2 olhar = LerOlhar();
                // com algo na mão (16/11/2026, a pedido: "eu consigo ter um ângulo de mira para dentro do meu próprio corpo,
                // isso não faz sentido"), a mira vai até 110° para os lados e 40° para baixo: virando mais, o olho (que fica na
                // frente do pescoço) via o próprio ombro e as costas por dentro, e olhando mais para baixo, mirava as pernas
                float giroMaximo = _maosLivres ? GiroArmado : 135f, baixoMaximo = _maosLivres ? BaixoArmado : 75f;
                _yaw = GiroDeTeste ?? Mathf.Clamp(_yaw + olhar.x, -giroMaximo, giroMaximo);
                _pitch = Mathf.Clamp(_pitch - olhar.y, -60f, baixoMaximo);
            }
            // o corpo acompanha este olhar (o peito e a cabeça, JogadorRede.LateUpdate): sem isto, ele ficava com o do
            // momento de entrar (olhando para a porta, para baixo), e os outros viam a cabeça baixa o tempo todo
            // (07/11/2026, a pedido: "a cabeça do personagem fica olhando para baixo")
            if (_rede != null && _rede.IsOwner && _fase == Fase.Dentro && Mathf.Abs(_rede.Pitch.Value - _pitch) > 0.5f) _rede.Pitch.Value = _pitch;
            // e para os lados (10/11/2026): o giro vai pela rede como o Pitch, só quando muda mais de 1 grau; os outros veem a
            // arma do garupa e dos passageiros (e a cabeça de quem está sentado) virando para onde ele olha
            if (_rede != null && _rede.IsOwner && _fase == Fase.Dentro && Mathf.Abs(_rede.GiroSentado.Value - _yaw) > 1f) _rede.GiroSentado.Value = _yaw;
            Vector3 olho = _quadrilNaRaiz + OlhoDe(c, Carro != null ? Lugar : _lugarSaindo);
            if (comCorpo) olho = _rede.OlhoForaDoCorpo(olho);   // (o olho nunca atrás nem abaixo do pescoço)
            _cabeca.localPosition = Vector3.Lerp(_cabecaLocal, olho, k);
            _cabeca.localRotation = Quaternion.Slerp(_cabecaAntes, Quaternion.Euler(_pitch, _yaw, 0f), k);
        }

        // a câmera atrás do carro: o mouse gira em volta dele; andando, ela volta sozinha para trás
        void CameraDeTras(float dt)
        {
            _rede.CorpoNoVeiculo(false);
            CorpoVisivel(true);
            // a cabeça reta (08/11/2026, a pedido: "ele continua olhando para baixo ... nos dois carros"): a câmera de trás é a
            // mesma escolha da terceira pessoa a pé, e nela o olhar do corpo (o Pitch da rede) nunca mudava: ficava o do
            // momento de entrar (olhando para a porta, para baixo). O olhar da primeira pessoa volta quando ela volta
            // (com algo na mão, o olhar do corpo é o da mira: MirarPelaCameraDeTras)
            if (!_maosLivres)
            {
                _pitch = 0f;
                if (_rede.IsOwner && Mathf.Abs(_rede.Pitch.Value) > 0.5f) _rede.Pitch.Value = 0f;
                ZerarOGiro();
            }
            Vector2 olhar = LerOlhar();
            if (olhar.sqrMagnitude > 0.0001f) _mexeuNaCamera = Time.time;
            _yaw3 += olhar.x;
            _pitch3 = Mathf.Clamp(_pitch3 - olhar.y, -5f, 50f);
            if (Time.time - _mexeuNaCamera > 1.5f && Carro.Velocidade > 3f)
                _yaw3 = Mathf.LerpAngle(_yaw3, 0f, 1f - Mathf.Exp(-2f * dt));
            Vector3 frente = Vector3.ProjectOnPlane(Carro.transform.forward, Vector3.up);
            float rumo = frente.sqrMagnitude > 0.01f ? Quaternion.LookRotation(frente).eulerAngles.y : Carro.transform.eulerAngles.y;
            var rot = Quaternion.Euler(_pitch3, rumo + _yaw3, 0f);
            // a moto é menor: a câmera fica mais perto e mais baixa
            Vector3 pivo = Carro.transform.position + Vector3.up * (Carro.moto ? 1.3f : 1.4f);
            float distancia = Carro.moto ? 4.2f : 6f;
            // com algo na mão (16/11/2026), a câmera vai para cima do ombro direito de quem senta, mais perto (como a
            // terceira pessoa a pé e as referências: GTA V, Days Gone): atrás do corpo, a mira do meio da tela ficava nas costas
            _ombroDaCamera = Mathf.MoveTowards(_ombroDaCamera, _maosLivres ? 1f : 0f, dt / 0.3f);
            if (_ombroDaCamera > 0f && Lugar >= 0 && Lugar < Carro.assentos.Length && Carro.assentos[Lugar] != null)
            {
                float k = Mathf.SmoothStep(0f, 1f, _ombroDaCamera);
                Vector3 ombro = Carro.assentos[Lugar].position + Vector3.up * 0.75f + rot * Vector3.right * 0.55f;
                pivo = Vector3.Lerp(pivo, ombro, k);
                distancia = Mathf.Lerp(distancia, 2.2f, k);
            }
            if (Physics.SphereCast(pivo, 0.25f, rot * Vector3.back, out var batida, distancia, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                distancia = Mathf.Max(0.8f, batida.distance - 0.1f);
            // aproxima na hora (para não atravessar a parede) e afasta devagar
            _distancia3 = distancia < _distancia3 ? distancia : Mathf.Lerp(_distancia3, distancia, 1f - Mathf.Exp(-3f * dt));
            _camera.SetPositionAndRotation(pivo + rot * Vector3.back * _distancia3, rot);
            if (_maosLivres) MirarPelaCameraDeTras();
        }

        /// <summary>
        /// Na câmera de trás, com algo na mão (16/11/2026, a pedido: "em terceira pessoa não consigo mirar e nem atirar, deveria
        /// dar igual nos prints"; GTA V, GTA IV, Days Gone): o corpo mira para o que está no meio da tela. O giro e o Pitch do
        /// olhar sentado (pela rede, como na primeira pessoa) vão do peito dele até o ponto que a câmera vê; os braços com a
        /// arma seguem (<see cref="BracosComArma"/>).
        /// </summary>
        void MirarPelaCameraDeTras()
        {
            Vector3 de = _camera.position, para = _camera.forward;
            Vector3 alvo = Physics.Raycast(de, para, out var acerto, 80f, MascaraDoCenario, QueryTriggerInteraction.Ignore) ? acerto.point : de + para * 80f;
            var peito = Peito;
            Vector3 origem = peito != null ? peito.position : transform.position + transform.up * 1.2f;
            Vector3 local = Quaternion.Inverse(transform.rotation) * (alvo - origem);
            if (local.sqrMagnitude < 1e-4f) return;
            _yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -135f, 135f);
            _pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -50f, 60f);
            if (!_rede.IsOwner) return;
            if (Mathf.Abs(_rede.Pitch.Value - _pitch) > 0.5f) _rede.Pitch.Value = _pitch;
            if (Mathf.Abs(_rede.GiroSentado.Value - _yaw) > 1f) _rede.GiroSentado.Value = _yaw;
        }

        // o próprio corpo: só sombra na primeira pessoa (como fora do carro), inteiro na câmera de trás (o JogadorRede só
        // muda o que mudou; a terceira pessoa a pé também mexe nele)
        void CorpoVisivel(bool visivel) => _rede.CorpoDoDono(visivel);

        /// <summary>
        /// Os avisos do desgaste para o motorista (Carro.Desgaste.cs), quando a coisa começa: o pneu murchando, o pneu que
        /// estoura ou esvazia, o radiador vazando, a reserva, o fim da gasolina e o motor falhando (a vela ou a gasolina).
        /// </summary>
        void AvisosDoDesgaste()
        {
            var c = Carro;
            int murchando = c.PneusMurchando;
            int novos = murchando & ~_murchandoAntes;
            var p = c.Pneus;
            int furados = 0;
            for (int i = 0; i < Vehicles.PneusDoCarro.Quantas; i++) if (p.Tem(i) && p.Furado(i)) furados |= 1 << i;
            if (novos != 0)
            {
                int r = 0;
                while ((novos & (1 << r)) == 0) r++;
                Avisar($"O pneu {Carro.NomesDasRodas[r]} está murchando: pare para trocar (macaco e estepe)", 4f);
            }
            else if (_furadosAntes >= 0 && (furados & ~_furadosAntes) != 0)
            {
                int f = furados & ~_furadosAntes, r = 0;
                while ((f & (1 << r)) == 0) r++;
                Avisar((_murchandoAntes & (1 << r)) != 0 ? $"O pneu {Carro.NomesDasRodas[r]} esvaziou" : $"O pneu {Carro.NomesDasRodas[r]} estourou!", 4f);
            }
            _murchandoAntes = murchando;
            _furadosAntes = furados;
            if (c.RadiadorVazando && !_vazandoAntes) Avisar("O radiador está vazando: o motor vai esquentar (troque no capô)", 4f);
            _vazandoAntes = c.RadiadorVazando;
            if (c.SemGasolina && !_semGasolinaAntes) Avisar("Acabou a gasolina: abasteça com um galão na tampa do tanque", 4f);
            else if (c.NaReserva && !_reservaAntes && !c.SemGasolina) Avisar("A gasolina está na reserva", 3f);
            _semGasolinaAntes = c.SemGasolina;
            _reservaAntes = c.NaReserva;
            if (c.UltimoEngasgo > _engasgoAvisado + 25f && Time.time - c.UltimoEngasgo < 0.5f)
            {
                _engasgoAvisado = c.UltimoEngasgo;
                Avisar(c.NaReserva ? "O motor está falhando: a gasolina está acabando"
                    : c.moto ? "O motor está falhando: a vela está gasta (agache perto do motor para trocar)"
                    : "O motor está falhando: a vela está gasta (troque no capô)", 3.5f);
            }
        }

        void Avisar(string texto, float tempo = 1.6f)
        {
            _aviso = texto;
            _avisoAte = Time.time + tempo;
        }

        // ------------------------------------------------------------------------------------------ a pose sentada

        /// <summary>
        /// Sentado: o corpo parte do repouso (o Animator fica desligado), o tronco deita um pouco no encosto, as coxas vão
        /// para a frente e as canelas para baixo; o motorista põe as mãos no volante, os outros no colo. Cada osso aponta
        /// para uma direção no espaço do carro (o eixo Y do osso vai ao longo dele), como no retrato do inventário.
        /// </summary>
        void Pose(Carro carro, int lugar)
        {
            bool motorista = lugar == 0;
            var animador = _rede.animadorCorpo;
            // na moto, a pose de quem monta (feita aqui, mesmo com as animações de sentado do carro)
            if (carro != null && carro.moto)
            {
                PoseDaMoto(carro, lugar);
                return;
            }
            // na caçamba da caminhonete, sentado no piso (feita aqui, mesmo com as animações de sentado do carro)
            if (carro != null && carro.NaCacamba(lugar))
            {
                PoseDaCacamba(carro, lugar);
                return;
            }
            // o personagem com as animações de sentado (o principal em blocos, 28/10/2026): o Animator toca a do lugar
            // (o parâmetro Lugar: 1 o motorista, 2 os outros) e a camada dos braços sai; sem elas, a pose feita aqui
            if (TemLugar(animador))
            {
                if (!animador.enabled) animador.enabled = true;
                animador.SetInteger(LugarId, motorista ? 1 : 2);
                if (animador.layerCount > 1) animador.SetLayerWeight(1, 0f);
                return;
            }
            if (_repouso.Count == 0) return;
            if (animador.enabled) animador.enabled = false;
            foreach (var (osso, pos, rot) in _repouso) osso.SetLocalPositionAndRotation(pos, rot);
            if (_ossos.TryGetValue("spine", out var coluna)) coluna.rotation = Quaternion.AngleAxis(-10f, transform.right) * coluna.rotation;
            for (int s = -1; s <= 1; s += 2)
            {
                string l = s < 0 ? ".L" : ".R";
                Apontar("thigh" + l, new Vector3(s * 0.07f, 0.12f, 1f));
                Apontar("shin" + l, motorista ? new Vector3(s * 0.02f, -0.78f, 0.62f) : new Vector3(0f, -0.9f, 0.42f));
                Apontar("foot" + l, new Vector3(0f, -0.35f, 0.94f));
                if (motorista)
                {
                    Apontar("upper_arm" + l, new Vector3(s * 0.12f, -0.5f, 0.86f));
                    Apontar("forearm" + l, new Vector3(-s * 0.32f, 0.22f, 0.92f));
                }
                else
                {
                    Apontar("upper_arm" + l, new Vector3(s * 0.1f, -0.93f, 0.33f));
                    Apontar("forearm" + l, new Vector3(-s * 0.28f, -0.32f, 0.9f));
                }
            }
        }

        static readonly int LugarId = Animator.StringToHash("Lugar");
        RuntimeAnimatorController _controladorVisto;
        bool _temLugar;

        /// <summary>Se o controlador do corpo tem as animações de sentado (o parâmetro Lugar).</summary>
        bool TemLugar(Animator animador)
        {
            if (animador.runtimeAnimatorController != _controladorVisto)
            {
                _controladorVisto = animador.runtimeAnimatorController;
                _temLugar = false;
                foreach (var p in animador.parameters)
                    if (p.nameHash == LugarId) _temLugar = true;
            }
            return _temLugar;
        }

        void Apontar(string nome, Vector3 direcao)
        {
            if (!_ossos.TryGetValue(nome, out var osso)) return;
            Vector3 d = transform.TransformDirection(direcao.normalized);
            osso.rotation = Quaternion.FromToRotation(osso.up, d) * osso.rotation;
        }

        // ------------------------------------------------------------------------------------------ na moto

        /// <summary>
        /// Montado na moto (06/11/2026): o corpo parte do repouso (o Animator fica desligado), o tronco deita para a frente
        /// (o piloto, <see cref="Carro.troncoDoPiloto"/>: a cross mais, a Fat Boy quase nada), as pernas vão do quadril até
        /// as pedaleiras do lugar (os joelhos para a frente e para fora) e as mãos do piloto até os punhos (os cotovelos para
        /// fora e para baixo), que viram com o guidão. O garupa põe as mãos nas coxas. Tudo segue a moto inclinada (o banco
        /// é filho do modelo que inclina).
        /// </summary>
        void PoseDaMoto(Carro c, int lugar)
        {
            var animador = _rede.animadorCorpo;
            if (_repouso.Count == 0) return;
            if (animador.enabled) animador.enabled = false;
            foreach (var (osso, pos, rot) in _repouso) osso.SetLocalPositionAndRotation(pos, rot);
            bool piloto = lugar == 0;
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            if (_ossos.TryGetValue("spine", out var coluna))
                coluna.rotation = Quaternion.AngleAxis(piloto ? c.troncoDoPiloto : 5f, direita) * coluna.rotation;
            // as pernas primeiro: as mãos de quem vai na garupa pousam nas coxas (BracosComArma e NaCoxa leem onde elas ficaram)
            for (int s = 0; s < 2; s++)   // 0 o lado direito, 1 o esquerdo
            {
                string l = s == 0 ? ".R" : ".L";
                float lado = s == 0 ? 1f : -1f;
                int i = lugar * 2 + s;
                var pe = c.pedaleiras != null && i < c.pedaleiras.Length ? c.pedaleiras[i] : null;
                if (pe == null) continue;
                // o tornozelo fica acima e atrás do apoio (a sola em cima da pedaleira)
                Vector3 tornozelo = pe.position + cima * 0.08f - frente * 0.06f;
                // o joelho (16/11/2026, a pedido: "não é anatômico, não é correto"; antes ele abria até 34° na cross, de
                // sapo): para a frente e só por fora do que fica entre as pernas, o tanque (o piloto: a 20 cm do meio da moto,
                // ou três quartos do caminho até o pé, a canela quase em pé vista de frente) ou os quadris do piloto (a
                // garupa: a 23 cm)
                float peFora = Vector3.Dot(tornozelo - transform.position, direita) * lado;
                float joelhoFora = piloto ? Mathf.Max(0.20f, peFora * 0.75f) : 0.23f;
                float quadrilFora = _ossos.TryGetValue("thigh" + l, out var coxaDaPerna)
                    ? Vector3.Dot(coxaDaPerna.position - transform.position, direita) * lado : 0.095f;
                Vector3 dobra = frente * 0.33f + cima * 0.05f + direita * (lado * (joelhoFora - quadrilFora));
                DoisOssos("thigh" + l, "shin" + l, tornozelo, dobra);
                PeNoPlano(l, frente * 0.92f - cima * 0.3f);
            }
            // os braços: com a arma (o garupa; e, desde 16/11/2026, o piloto mirando ou atirando), a pose da arma
            // (BracosComArma, que vira o tronco antes); senão, as mãos no guidão e nas coxas
            bool armado = BracosComArma(c, lugar);
            for (int s = 0; s < 2 && !armado; s++)
            {
                string l = s == 0 ? ".R" : ".L";
                if (piloto && NoGuidao(c, s, out var punho, out var dobraNoGuidao, out var palmaNoGuidao))
                {
                    // as mãos nos punhos, as palmas para baixo (o antebraço gira, não o cotovelo)
                    DoisOssos("upper_arm" + l, "forearm" + l, punho, dobraNoGuidao);
                    Palma(l, palmaNoGuidao);
                }
                else if (NaCoxa(s, out var naCoxa, out var dobraNaCoxa))
                {
                    // o garupa: as mãos nas coxas, perto dos joelhos, as palmas em cima delas
                    DoisOssos("upper_arm" + l, "forearm" + l, naCoxa, dobraNaCoxa);
                    Palma(l, -cima);
                }
            }
            // a mão direita torce o acelerador (07/11/2026; só no dono: o acelerador dele não vai pela rede)
            if (piloto && !armado && c.IsOwner && _ossos.TryGetValue("hand.R", out var maoDireita))
                maoDireita.rotation = Quaternion.AngleAxis(-28f * Mathf.Clamp01(c.AceleradorDoDono), direita) * maoDireita.rotation;
        }

        // o que está na mão do corpo, sentado (em todas as máquinas): some com quem dirige e, na moto, com a arma grande
        // (15/11/2026); e muda quando ele troca de item no banco
        Loot.Item _itemVisto = Loot.Item.Nada;
        bool _naMaoVisto, _naMaoFeito;

        void AcertarNaMao()
        {
            var item = NaMao;
            // o piloto da moto (16/11/2026): a arma aparece quando a mão sai do guidão (mirando ou atirando) e some quando
            // ela volta (a arma guardada)
            bool guardadaNoPiloto = Lugar == 0 && !(Carro.moto && ArmaDoPiloto(item) && _armaLevantada > 0.35f);
            bool esconder = guardadaNoPiloto || (Carro.moto && GrandeParaAMoto(item)) || _bracosDoCorpoEncolhidos;
            if (_naMaoFeito && esconder == _naMaoVisto && item == _itemVisto) return;
            _naMaoFeito = true;
            _naMaoVisto = esconder;
            _itemVisto = item;
            _rede.EsconderNaMao(esconder);
        }

        // ------------------------------------------------------------------------------------------ na caçamba

        /// <summary>
        /// Sentado no piso da caçamba da caminhonete (15/11/2026, a pedido: "na caçamba vão dar para sentar mais duas pessoas
        /// (dentro da caçamba, encostadas ali dentro)"): o corpo parte do repouso (o Animator fica desligado), o tronco deita
        /// para trás, encostado na frente da caçamba (<see cref="Carro.encostoNaCacamba"/>). Depois do primeiro teste ("o
        /// jogador parece um sapo"; "uma perna dobrada e uma perna esticada, acho que ficaria mais interessante"): a perna
        /// direita esticada no piso (<see cref="Carro.pisoDaCacamba"/>, o calcanhar no chão), a esquerda dobrada, o pé perto do
        /// quadril e o joelho para cima; a mão direita em cima da coxa esticada e a esquerda com o antebraço no joelho. Com algo
        /// na mão, a arma descansa em cima da coxa esticada (<see cref="BracosComArma"/>), sem atravessar a perna.
        /// </summary>
        void PoseDaCacamba(Carro c, int lugar)
        {
            var animador = _rede.animadorCorpo;
            if (_repouso.Count == 0) return;
            if (animador.enabled) animador.enabled = false;
            foreach (var (osso, pos, rot) in _repouso) osso.SetLocalPositionAndRotation(pos, rot);
            Vector3 frente = transform.forward, cima = transform.up, direita = transform.right;
            if (_ossos.TryGetValue("spine", out var coluna))
                coluna.rotation = Quaternion.AngleAxis(-c.encostoNaCacamba, direita) * coluna.rotation;
            var banco = c.assentos[lugar];
            float altura = Mathf.Max(0.05f, banco.localPosition.y - c.pisoDaCacamba);   // o quadril acima do piso
            // a direita esticada (quase toda: o joelho dobra um pouco), o calcanhar no piso e a ponta do pé para cima
            Vector3 tornozeloD = banco.position + frente * 0.80f + direita * 0.13f - cima * (altura - 0.06f);
            DoisOssos("thigh.R", "shin.R", tornozeloD, cima + frente * 0.2f);
            PeNoPlano(".R", frente * 0.35f + cima * 0.93f);
            // a esquerda dobrada: o pé no piso, o joelho para cima e um pouco para fora. (16/11/2026: com o pé a 40 cm, o
            // joelho dobrava 126° e a coxa subia até o peito; a 52 cm dobra uns 105°, a coxa a uns 45°)
            Vector3 tornozeloE = banco.position + frente * 0.52f - direita * 0.13f - cima * (altura - 0.07f);
            DoisOssos("thigh.L", "shin.L", tornozeloE, cima * 0.95f - direita * 0.15f);
            PeNoPlano(".L", frente * 0.95f - cima * 0.1f);
            // as mãos (com algo na mão, os braços seguram a arma depois desta pose): a direita em cima da coxa esticada, os
            // cotovelos para trás e para baixo; a esquerda com o antebraço no joelho dobrado, a mão caindo à frente dele
            if (_ossos.TryGetValue("thigh.R", out var coxaD) && _ossos.TryGetValue("shin.R", out var joelhoD))
            {
                DoisOssos("upper_arm.R", "forearm.R", Vector3.Lerp(coxaD.position, joelhoD.position, 0.55f) + cima * 0.08f,
                          -cima * 0.6f - frente * 0.5f + direita * 0.3f);
                Palma(".R", -cima);
            }
            if (_ossos.TryGetValue("shin.L", out var joelhoE))
            {
                DoisOssos("upper_arm.L", "forearm.L", joelhoE.position + frente * 0.08f + cima * 0.02f, -cima * 0.3f - frente * 0.3f - direita * 0.6f);
                Palma(".L", -cima);
            }
        }

        /// <summary>Até onde os braços com a arma acompanham o giro do olhar sentado, para cada lado (graus; o olhar vai a 135).</summary>
        const float GiroDosBracos = 100f;
        /// <summary>Até onde a cabeça de quem está sentado acompanha o giro do olhar, para cada lado (graus).</summary>
        const float GiroDaCabeca = 70f;

        // a arma de quem vai sentado (os braços, a empunhadura e as transições): PassageiroDoCarro.ArmaSentado.cs

        // a cabeça de quem está sentado vira para os lados com o olhar dele (10/11/2026, o giro da rede; o Pitch o JogadorRede
        // já põe), até GiroDaCabeca. No dono, na primeira pessoa, a cabeça não aparece (no carro, nem o corpo)
        void CabecaVirada()
        {
            float giro = Mathf.Clamp(_rede.GiroSentado.Value, -GiroDaCabeca, GiroDaCabeca);
            if (Mathf.Abs(giro) < 0.5f || !_ossos.TryGetValue("head", out var cabeca)) return;
            cabeca.rotation = Quaternion.AngleAxis(giro, transform.up) * cabeca.rotation;
        }

        /// <summary>
        /// As mãos do motorista no volante (07/11/2026, a pedido: as mãos de primeira pessoa no volante; "quando realizarmos
        /// uma curva, os braços não se ultrapassem um ao outro"). Depois da pose sentada: as duas mãos no aro, um pouco acima
        /// do meio (a 62° do alto, de cada lado), acompanhando o volante só até <see cref="MaosSeguemOVolante"/> graus; dali em
        /// diante o aro desliza nas mãos, e elas nunca se cruzam. O braço dobra o cotovelo para baixo e para fora.
        /// O meio e o raio do aro saem das malhas do volante (<see cref="Carro.MedidasDoVolante"/>).
        /// </summary>
        void MaosNoVolante(Carro c)
        {
            var v = c.volante;
            if (v == null) return;
            c.MedidasDoVolante(out var centroLocal, out float raio);
            var repouso = (v.parent != null ? v.parent.rotation : Quaternion.identity) * c.VolanteRepouso;
            Vector3 X = repouso * Vector3.right, Y = repouso * Vector3.up, Z = repouso * Vector3.forward;
            Vector3 centro = v.TransformPoint(centroLocal);
            Vector3 cimaDoCarro = c.transform.up, direitaDoCarro = c.transform.right, frenteDoCarro = c.transform.forward;
            // o alto e a direita do carro, em ângulos no plano do volante (o giro desenhado soma no mesmo sentido)
            Vector3 cima = Vector3.ProjectOnPlane(cimaDoCarro, Z), direita = Vector3.ProjectOnPlane(direitaDoCarro, Z);
            if (cima.sqrMagnitude < 1e-6f || direita.sqrMagnitude < 1e-6f) return;
            float aCima = Mathf.Atan2(Vector3.Dot(cima, Y), Vector3.Dot(cima, X)) * Mathf.Rad2Deg;
            float aDireita = Mathf.Atan2(Vector3.Dot(direita, Y), Vector3.Dot(direita, X)) * Mathf.Rad2Deg;
            float paraADireita = Mathf.Sign(Mathf.DeltaAngle(aCima, aDireita));
            float seguir = Mathf.Clamp(c.VolanteVisto, -MaosSeguemOVolante, MaosSeguemOVolante);
            Vector3 paraMim = Vector3.Dot(Z, -frenteDoCarro) >= 0f ? Z : -Z;
            for (int k = 0; k < 2; k++)
            {
                float lado = k == 0 ? 1f : -1f;
                string l = k == 0 ? ".R" : ".L";
                float a = (aCima + paraADireita * lado * 62f + seguir) * Mathf.Deg2Rad;
                Vector3 radial = X * Mathf.Cos(a) + Y * Mathf.Sin(a);
                Vector3 aro = centro + radial * raio;
                // o pulso na frente do aro e um pouco para fora (a mão abraça o aro)
                Vector3 pulso = aro + paraMim * 0.05f + radial * 0.025f;
                DoisOssos("upper_arm" + l, "forearm" + l, pulso, -cimaDoCarro * 0.8f + direitaDoCarro * (lado * 0.6f) - frenteDoCarro * 0.2f);
                Mirar("hand" + l, aro - paraMim * 0.01f - pulso);
            }
        }

        const float MaosSeguemOVolante = 55f;

        // ------------------------------------------------------------------------------------------ embaixo do teto

        /// <summary>No máximo esta parte do trecho da coluna até o peito encolhe (o resto passaria a parecer um corpo amassado).</summary>
        const float EncolherNoMaximo = 0.45f;
        /// <summary>A folga entre o alto da cabeça e o teto por dentro (m).</summary>
        const float FolgaDoTeto = 0.015f;
        Transform _peitoEncolhido;
        Vector3 _peitoNoLugar, _peitoPosto;
        float _alturaDaCabeca = -1f;

        /// <summary>
        /// No carro (07/11/2026, a pedido: na terceira pessoa "ele ultrapassa um pouco o teto - tem como a gente cortar um
        /// pouco o personagem? encurtar ele dentro do carro talvez?"), em todas as máquinas, depois da pose sentada e antes
        /// das mãos no volante: se o alto da cabeça passa do teto por dentro (<see cref="Carro.TetoPorDentro"/>), o tronco
        /// encurta: o peito desce em direção à coluna, até a cabeça caber (no máximo <see cref="EncolherNoMaximo"/> do trecho).
        /// A cintura, onde o tronco encurta, fica atrás da porta; a cabeça, os ombros e os braços ficam do tamanho de
        /// sempre. A cabeça é medida sem o olhar (o JogadorRede dobra o peito e a cabeça com ele), para o tamanho não mudar
        /// quando ele olha para cima ou para baixo.
        /// </summary>
        void CaberNoTeto(Carro c)
        {
            if (!_ossos.TryGetValue("chest", out var peito) || !_ossos.TryGetValue("head", out var cabeca) || peito.parent == null) return;
            // a posição do peito de agora: a da animação (o Animator escreve a cada quadro) ou, se ele não escreve, a de
            // antes de encolher
            Vector3 atual = peito.localPosition;
            Vector3 noLugar = _peitoEncolhido == peito && atual == _peitoPosto ? _peitoNoLugar : atual;
            peito.localPosition = noLugar;
            float teto = c.TetoPorDentro;
            if (teto >= float.MaxValue * 0.5f)
            {
                TroncoDeVolta();
                return;
            }
            // tirar o olhar: o JogadorRede gira o peito (e tudo acima) e, de novo, a cabeça, cada um com metade do olhar;
            // sem o Animator, a pose parte do repouso e já veio sem ele
            float olhar = TemLugar(_rede.animadorCorpo) ? _rede.Pitch.Value * 0.5f : 0f;
            var semOlhar = Quaternion.AngleAxis(-olhar, transform.right);
            Vector3 aCabeca = peito.position + semOlhar * (cabeca.position - peito.position);
            Vector3 alto = aCabeca + semOlhar * semOlhar * cabeca.rotation * Vector3.up * AlturaDaCabeca(cabeca);
            float passa = c.transform.InverseTransformPoint(alto).y + FolgaDoTeto - teto;
            if (passa <= 0f)
            {
                TroncoDeVolta();
                return;
            }
            // o quanto o trecho da coluna até o peito sobe, no carro: encurtar ele desce o peito, os ombros e a cabeça
            float sobe = Vector3.Dot(peito.parent.TransformVector(noLugar), c.transform.up);
            if (sobe < 0.03f) return;
            float s = Mathf.Clamp(passa / sobe, 0f, EncolherNoMaximo);
            _peitoEncolhido = peito;
            _peitoNoLugar = noLugar;
            _peitoPosto = noLugar * (1f - s);
            peito.localPosition = _peitoPosto;
        }

        // o tronco no tamanho de sempre (saindo do carro)
        void TroncoDeVolta()
        {
            if (_peitoEncolhido != null) _peitoEncolhido.localPosition = _peitoNoLugar;
            _peitoEncolhido = null;
        }

        /// <summary>
        /// Do osso da cabeça até o alto dela (m), medido uma vez nas malhas do corpo (no repouso: o ponto mais alto ao longo
        /// do osso, com o cabelo e o que estiver preso nele, como o chapéu).
        /// </summary>
        float AlturaDaCabeca(Transform cabeca)
        {
            if (_alturaDaCabeca > 0f) return _alturaDaCabeca;
            float maior = 0f;
            foreach (var r in _rede.animadorCorpo.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    var m = smr.sharedMesh;
                    int i = m != null ? System.Array.IndexOf(smr.bones, cabeca) : -1;
                    if (i < 0 || i >= m.bindposes.Length) continue;
                    // a cabeça no espaço da malha, no repouso: o eixo Y do osso, e o alto da malha ao longo dele
                    var osso = m.bindposes[i].inverse;
                    Vector3 pos = osso.GetColumn(3), eixo = ((Vector3)osso.GetColumn(1)).normalized;
                    float escala = Mathf.Abs(smr.transform.lossyScale.y);
                    var b = m.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var canto = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                        maior = Mathf.Max(maior, Vector3.Dot(canto - pos, eixo) * escala);
                    }
                }
                else if (r.transform.IsChildOf(cabeca) && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    var b = mf.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var canto = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                        Vector3 w = r.transform.TransformPoint(canto) - cabeca.position;
                        maior = Mathf.Max(maior, Vector3.Dot(w, cabeca.up));
                    }
                }
            }
            _alturaDaCabeca = maior > 0.08f && maior < 0.6f ? maior : 0.25f;
            return _alturaDaCabeca;
        }

        /// <summary>Dois ossos (o de cima e o do meio) que levam a ponta até o alvo, dobrando para o lado de <paramref name="dobra"/> (no mundo).</summary>
        void DoisOssos(string de, string meio, Vector3 alvo, Vector3 dobra)
        {
            if (!_ossos.TryGetValue(de, out var a) || !_ossos.TryGetValue(meio, out var b)) return;
            var fimDoMeio = b.childCount > 0 ? b.GetChild(0) : null;
            if (fimDoMeio == null) return;
            Vector3 pa = a.position;
            float l1 = (b.position - pa).magnitude, l2 = (fimDoMeio.position - b.position).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            Vector3 para = alvo - pa;
            Vector3 dir = para.sqrMagnitude > 1e-8f ? para.normalized : transform.forward;
            float d = Mathf.Clamp(para.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
            float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            Vector3 p = Vector3.ProjectOnPlane(dobra, dir);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(transform.forward, dir);
            p.Normalize();
            Vector3 dobrado = pa + dir * x + p * h;
            // a dobradiça: perpendicular ao membro e ao lado para onde a junta do meio aponta (a mesma conta do repouso)
            Vector3 eixo = Vector3.Cross(p, dir);
            Orientar(a, dobrado - pa, eixo);
            Orientar(b, pa + dir * d - b.position, eixo);
        }

        /// <summary>
        /// O osso ao longo de <paramref name="aoLongo"/> (no mundo), com o eixo da dobradiça dele em <paramref name="eixo"/>:
        /// sem torcer em volta do próprio comprimento. Sem dobradiça guardada, só aponta (pelo caminho mais curto).
        /// </summary>
        void Orientar(Transform osso, Vector3 aoLongo, Vector3 eixo)
        {
            if (aoLongo.sqrMagnitude < 1e-10f) return;
            Vector3 y = aoLongo.normalized;
            Vector3 h = Vector3.ProjectOnPlane(eixo, y);
            if (!_dobradica.TryGetValue(osso, out var local) || h.sqrMagnitude < 1e-8f)
            {
                Mirar(osso, y);
                return;
            }
            Vector3 hLocal = Vector3.ProjectOnPlane(local, Vector3.up);   // o osso vai ao longo do eixo Y dele
            if (hLocal.sqrMagnitude < 1e-8f)
            {
                Mirar(osso, y);
                return;
            }
            osso.rotation = Quaternion.LookRotation(h.normalized, y) * Quaternion.Inverse(Quaternion.LookRotation(hLocal.normalized, Vector3.up));
        }

        /// <summary>
        /// O pé para <paramref name="direcao"/> (no mundo), dentro do plano da perna: o tornozelo dobra no eixo do joelho
        /// e não torce (a ponta do pé vai para fora ou para dentro com a perna toda, pelo quadril).
        /// </summary>
        void PeNoPlano(string l, Vector3 direcao)
        {
            if (!_ossos.TryGetValue("shin" + l, out var canela) || !_ossos.TryGetValue("foot" + l, out var pe)
                || !_dobradica.TryGetValue(canela, out var local))
            {
                Mirar("foot" + l, direcao);
                return;
            }
            Vector3 eixo = canela.rotation * local;
            Vector3 noPlano = Vector3.ProjectOnPlane(direcao, eixo);
            if (noPlano.sqrMagnitude < 1e-6f) return;
            Orientar(pe, noPlano, eixo);
        }

        /// <summary>
        /// Gira o antebraço no próprio eixo (a pronação e a supinação, que é onde o braço de verdade gira), no máximo
        /// <paramref name="maximo"/> graus, para a palma da mão ficar virada para <paramref name="palmaPara"/> (no mundo).
        /// A mão fica no mesmo lugar (na ponta do antebraço).
        /// </summary>
        void Palma(string l, Vector3 palmaPara, float maximo = 90f)
        {
            if (!_ossos.TryGetValue("forearm" + l, out var antebraco) || !_ossos.TryGetValue("hand" + l, out var mao)
                || !_palma.TryGetValue(mao, out var local)) return;
            Vector3 eixo = antebraco.up;
            Vector3 agora = Vector3.ProjectOnPlane(mao.rotation * local, eixo), quero = Vector3.ProjectOnPlane(palmaPara, eixo);
            if (agora.sqrMagnitude < 1e-6f || quero.sqrMagnitude < 1e-6f) return;
            float giro = Mathf.Clamp(Vector3.SignedAngle(agora, quero, eixo), -maximo, maximo);
            antebraco.rotation = Quaternion.AngleAxis(giro, eixo) * antebraco.rotation;
        }

        void Mirar(string nome, Vector3 direcaoNoMundo)
        {
            if (_ossos.TryGetValue(nome, out var osso)) Mirar(osso, direcaoNoMundo);
        }

        static void Mirar(Transform osso, Vector3 direcaoNoMundo)
        {
            if (direcaoNoMundo.sqrMagnitude < 1e-8f) return;
            osso.rotation = Quaternion.FromToRotation(osso.up, direcaoNoMundo.normalized) * osso.rotation;
        }

        /// <summary>O olho de quem senta, a partir do quadril (no espaço do veículo): na moto, mais para a frente com o tronco deitado.</summary>
        static Vector3 OlhoDe(Carro c, int lugar)
        {
            // na caçamba, o tronco deita para trás (encostado): o olho vai junto (15/11/2026)
            if (c != null && c.NaCacamba(lugar))
            {
                float e = -c.encostoNaCacamba * Mathf.Deg2Rad;
                return new Vector3(0f, 0.70f * Mathf.Cos(e), 0.62f * Mathf.Sin(e) - 0.03f);
            }
            if (c == null || !c.moto) return OlhoNoQuadril;
            float t = (lugar == 0 ? c.troncoDoPiloto : 5f) * Mathf.Deg2Rad;
            return new Vector3(0f, 0.70f * Mathf.Cos(t), 0.62f * Mathf.Sin(t) - 0.03f);
        }

        // ------------------------------------------------------------------------------------------ na tela

        void OnGUI()
        {
            if (_rede == null || !_rede.IsOwner || _fase == Fase.Fora || _fase == Fase.Saindo) return;
            if (MenuOnline.Aberto || MenuOnline.ComTela || Carro == null || Video.GravacaoNoMundo.SemHud) return;
            float u = Screen.height / 720f;
            if (Lugar == 0)
            {
                var vel = EstilosDoHud.Rotulo(Mathf.RoundToInt(30 * u), FontStyle.Bold, TextAnchor.LowerRight, true);
                string texto = $"{Mathf.RoundToInt(Carro.Velocidade * 3.6f)}<size={Mathf.RoundToInt(14 * u)}> km/h</size>";
                Sombra(new Rect(Screen.width - 260 * u, Screen.height - 58 * u, 240 * u, 44 * u), texto, vel, new Color(0.9f, 0.88f, 0.8f));
                var f = EstilosDoHud.Rotulo(Mathf.RoundToInt(13 * u), FontStyle.Normal, TextAnchor.LowerRight, true);
                float ly = Screen.height - 80 * u;
                if (Carro.FaroisLigados && Carro.Pecas.BateriaAcende)
                {
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), Carro.moto ? "FAROL" : "FARÓIS", f, new Color(0.95f, 0.85f, 0.5f));
                    ly -= 20 * u;
                }
                // os avisos das peças: o motor quente (pisca fervendo) e a bateria fraca
                if (Carro.Fervendo || (Carro.MotorLigado && Carro.Temperatura > 0.85f))
                {
                    if (!Carro.Fervendo || Mathf.Repeat(Time.time, 0.8f) < 0.5f)
                        Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), Carro.Fervendo ? "MOTOR FERVENDO" : "MOTOR QUENTE", f, new Color(1f, 0.4f, 0.3f));
                    ly -= 20 * u;
                }
                if (Carro.Pecas.Estado(Vehicles.PecasDoMotor.Bateria) < 20)
                {
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), Carro.Pecas.Tem(Vehicles.PecasDoMotor.Bateria) ? "BATERIA FRACA" : "SEM BATERIA", f, new Color(1f, 0.75f, 0.3f));
                    ly -= 20 * u;
                }
                // a gasolina e o radiador (01/11/2026)
                if (Carro.SemGasolina || Carro.NaReserva)
                {
                    if (Carro.SemGasolina || Mathf.Repeat(Time.time, 1.2f) < 0.8f)
                        Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), Carro.SemGasolina ? "SEM GASOLINA" : "GASOLINA NA RESERVA", f, Carro.SemGasolina ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.75f, 0.3f));
                    ly -= 20 * u;
                }
                if (Carro.RadiadorVazando)
                {
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), "RADIADOR VAZANDO", f, new Color(1f, 0.55f, 0.3f));
                    ly -= 20 * u;
                }
                if (Carro.PneusMurchando != 0)
                {
                    if (Mathf.Repeat(Time.time, 0.8f) < 0.5f)
                        Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), "PNEU MURCHANDO", f, new Color(1f, 0.75f, 0.3f));
                    ly -= 20 * u;
                }
                // as rodas: furada ou faltando
                var pneus = Carro.Pneus;
                if (pneus.Ruins > 0)
                {
                    bool falta = false;
                    for (int i = 0; i < Vehicles.PneusDoCarro.Quantas; i++) falta |= !pneus.Tem(i);
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), falta ? "FALTA UMA RODA" : pneus.Ruins > 1 ? "PNEUS FURADOS" : "PNEU FURADO", f, new Color(1f, 0.75f, 0.3f));
                }
            }
            var dica = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Normal, TextAnchor.LowerCenter);
            string linha = Lugar == 0
                ? (Carro.moto ? "WASD: pilotar   ·   Espaço: freio de trás   ·   L: farol   ·   B: buzina   ·   F: lanterna   ·   V: câmera   ·   "
                              : "WASD: dirigir   ·   Espaço: freio de mão   ·   L: faróis   ·   B: buzina   ·   F: lanterna   ·   V: câmera   ·   ")
                  + (Carro.moto ? (Carro.MotorLigado ? "E: descer (fica ligada)" : "E: descer") : Carro.MotorLigado ? "E: sair (fica ligado)" : "E: sair")
                : Carro.moto ? "F: lanterna   ·   V: câmera   ·   E: descer" : "F: lanterna   ·   V: câmera   ·   E: sair";
            Sombra(new Rect(Screen.width / 2f - 400 * u, Screen.height - 34 * u, 800 * u, 24 * u), linha, dica, new Color(0.8f, 0.78f, 0.72f));
            if (Lugar == 0) TelaDaChave(u, dica);
            string aviso = Lugar == 0 && Carro.PodeDesvirar ? (Carro.moto ? "[R] Levantar a moto" : "[R] Desvirar o carro") : Time.time < _avisoAte ? _aviso : null;
            if (aviso != null)
            {
                var a = EstilosDoHud.Rotulo(Mathf.RoundToInt(18 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
                // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) 800u e a altura do aviso: o do pneu murchando
                // (639u), o da vela gasta (614u) e o da ligação direta (606u) passavam dos 600u e saíam em duas linhas cortadas
                // numa caixa de uma, a de cima no alto e a de baixo embaixo
                float largura = Mathf.Min(800f * u, Screen.width - 32f * u);
                _conteudoDoAviso.text = aviso;
                float alto = Mathf.Max(30f * u, a.CalcHeight(_conteudoDoAviso, largura));
                // com o mostrador da chave aberto, o aviso vai para cima dele (embaixo ficam as instruções), crescendo para cima
                // a partir do fundo de antes (altura/2 − 150u; o mostrador começa em altura/2 − 140u)
                float y = Lugar == 0 && _segurandoChave && !_terceira ? Screen.height / 2f - 150f * u - alto : Screen.height / 2f + 130 * u;
                Sombra(new Rect(Screen.width / 2f - largura / 2f, y, largura, alto), aviso, a, new Color(0.95f, 0.93f, 0.85f));
            }
        }

        /// <summary>
        /// A chave na tela: o ponto no meio (para mirar na ignição) com o motor desligado e a dica de quem olha para ela;
        /// segurando a chave, o mostrador redondo (<see cref="Mostrador"/>).
        /// </summary>
        void TelaDaChave(float u, GUIStyle dica)
        {
            var e = Carro.Estado;
            bool ligado = Carro.MotorLigado;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            var meio = EstilosDoHud.Rotulo(Mathf.RoundToInt(16 * u), FontStyle.Normal, TextAnchor.MiddleCenter);
            if (_terceira)
            {
                if (!ligado) Sombra(new Rect(cx - 300 * u, Screen.height - 62 * u, 600 * u, 24 * u),
                    Carro.moto ? "Para dar a partida, volte para a primeira pessoa (V)" : "Para dar a partida, volte para dentro do carro (V)", dica, new Color(0.95f, 0.85f, 0.55f));
                return;
            }
            if (_segurandoChave)
            {
                Mostrador(u, cx, cy, e);
                return;
            }
            bool mira = MirandoAChave();
            if (!ligado || mira)
            {
                float r = Mathf.Max(2f, 2.5f * u);
                Retangulo(new Rect(cx - r - 1, cy - r - 1, 2 * r + 2, 2 * r + 2), new Color(0f, 0f, 0f, 0.6f));
                Retangulo(new Rect(cx - r, cy - r, 2 * r, 2 * r), mira ? new Color(1f, 0.82f, 0.4f) : new Color(0.9f, 0.9f, 0.85f, 0.8f));
            }
            if (mira)
            {
                string texto = Carro.LigacaoDireta ? (ligado ? "[Clique] Separar os fios (desligar)" : TemAChave ? "[Clique] Pôr a chave" : "[Clique] Tentar a ligação direta de novo")
                    : e == Carro.EstadoDoMotor.SemChave ? (TemAChave ? "[Clique] Pôr a chave" : !Habilidades.Tem(Habilidade.LigacaoDireta) ? $"{SemAChave}: só com a ligação direta (Ladrão)"
                        : $"[Clique] Ligação direta ({SemAChave})") : "[Clique] Pegar a chave";
                Sombra(new Rect(cx - 320 * u, cy + 14 * u, 640 * u, 26 * u), texto, meio, new Color(0.97f, 0.95f, 0.88f));
            }
            else if (!ligado)
            {
                string onde = Carro.ondeFicaAIgnicao;
                string texto = e == Carro.EstadoDoMotor.SemChave
                    ? (TemAChave ? $"Olhe para a ignição, {onde}, e clique para pôr a chave"
                                 : !Habilidades.Tem(Habilidade.LigacaoDireta) ? $"S{SemAChave.Substring(1)}: o carro só liga com a chave (a ligação direta é a ponta do Ladrão, K)"
                                 : $"S{SemAChave.Substring(1)}: olhe para a ignição, {onde}, e clique para fazer a ligação direta")
                    : Carro.LigacaoDireta ? "Olhe para a ignição e clique para tentar a ligação direta de novo"
                    : "Olhe para a chave e clique nela para dar a partida";
                // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a da Fat Boy ("... no tanque, atrás do
                // velocímetro, e clique para fazer a ligação direta", 722u) não cabia nos 720u: quebrava, e a linha de cima saía
                // cortada no alto. Agora 800u, como a linha de baixo, e a altura dela, subindo a partir do fundo de antes
                // (altura − 38u, logo acima da linha dos comandos; em cima, até o aviso do meio da tela, não há nada)
                float largura = Mathf.Min(800f * u, Screen.width - 32f * u);
                _conteudoDoAviso.text = texto;
                float alto = Mathf.Max(24f * u, dica.CalcHeight(_conteudoDoAviso, largura));
                Sombra(new Rect(cx - largura / 2f, Screen.height - 38 * u - alto, largura, alto), texto, dica, new Color(0.95f, 0.85f, 0.55f));
            }
        }

        static readonly Color Dourado = new Color(1f, 0.8f, 0.22f), Laranja = new Color(0.92f, 0.45f, 0.15f), Verde = new Color(0.35f, 0.85f, 0.4f);

        /// <summary>
        /// O mostrador da chave, redondo: a ignição vista de frente (o miolo, a chave girando no meio e as posições DESL,
        /// LIGA e PARTIDA em volta) e, por fora, o anel do mini-game da partida (as marcas e o ponteiro). O desenho parado
        /// é uma textura feita no código, refeita só quando as marcas mudam (<see cref="PintarMostrador"/>).
        /// </summary>
        void Mostrador(float u, float cx, float cy, Carro.EstadoDoMotor e)
        {
            bool partida = e == Carro.EstadoDoMotor.Arranque && _naPartidaAntes;
            bool direta = Carro.LigacaoDireta, miniGame = partida && direta;
            if (_mostrador == null || _mostradorSujo || _mostradorNaPartida != miniGame || _mostradorDireto != direta) PintarMostrador(miniGame, direta);
            float r = 140f * u;
            bool errou = Time.time - _errouEm < 0.8f;
            if (Time.time - _errouEm < 0.25f) cx += Mathf.Sin(Time.time * 90f) * 4f * u;   // errou: treme
            var centro = new Vector2(cx, cy);
            GUI.DrawTexture(new Rect(cx - r, cy - r, 2 * r, 2 * r), _mostrador);

            // as posições da chave (a de agora mais clara); na ligação direta, não há chave
            float angulo = Carro.AnguloDaChave;
            var antes = GUI.matrix;
            if (!direta)
            {
            var rotulo = EstilosDoHud.Rotulo(Mathf.RoundToInt(10 * u), FontStyle.Bold, TextAnchor.MiddleCenter);
            Rotulo(centro, r * 0.66f, 0f, "DESL", rotulo, u, angulo < Carro.ChaveDesliga ? Color.white : new Color(0.7f, 0.7f, 0.68f));
            Rotulo(centro, r * 0.66f, Carro.ChaveLiga, "LIGA", rotulo, u, angulo >= Carro.ChaveDesliga && angulo < Carro.ChavePartida - 8f ? new Color(0.7f, 1f, 0.7f) : new Color(0.5f, 0.75f, 0.5f));
            Rotulo(centro, r * 0.66f, Carro.ChavePartida + 12f, "PARTIDA", rotulo, u, angulo >= Carro.ChavePartida - 8f ? new Color(1f, 0.75f, 0.45f) : new Color(0.85f, 0.5f, 0.25f));

            // a chave, vista de frente: o cabo atravessando o miolo, com a ponta clara mostrando a posição
            GUIUtility.RotateAroundPivot(angulo, centro);
            Retangulo(new Rect(cx - 0.075f * r, cy - 0.44f * r, 0.15f * r, 0.66f * r), new Color(0.03f, 0.03f, 0.03f, 0.95f));
            Retangulo(new Rect(cx - 0.055f * r, cy - 0.42f * r, 0.11f * r, 0.62f * r), new Color(0.13f, 0.13f, 0.14f));
            Retangulo(new Rect(cx - 0.03f * r, cy - 0.42f * r, 0.06f * r, 0.10f * r), new Color(0.95f, 0.9f, 0.75f));
            GUI.matrix = antes;
            }
            else
            {
                // os dois fios, um vermelho e um preto, cruzados no miolo
                var fio = EstilosDoHud.Rotulo(Mathf.RoundToInt(11 * u), FontStyle.Bold, TextAnchor.MiddleCenter);
                GUIUtility.RotateAroundPivot(35f, centro);
                Retangulo(new Rect(cx - 0.42f * r, cy - 0.025f * r, 0.84f * r, 0.05f * r), new Color(0.8f, 0.15f, 0.12f));
                GUI.matrix = antes;
                GUIUtility.RotateAroundPivot(-35f, centro);
                Retangulo(new Rect(cx - 0.42f * r, cy - 0.025f * r, 0.84f * r, 0.05f * r), new Color(0.08f, 0.08f, 0.08f));
                GUI.matrix = antes;
                Sombra(new Rect(cx - 70f * u, cy - 0.7f * r, 140f * u, 20f * u), "LIGAÇÃO DIRETA", fio, new Color(1f, 0.75f, 0.45f));
            }

            // o ponteiro do mini-game (só na ligação direta)
            if (miniGame)
            {
                GUIUtility.RotateAroundPivot(_ponteiro, centro);
                Retangulo(new Rect(cx - 3.5f * u, cy - 1.0f * r, 7f * u, 0.2f * r), new Color(0f, 0f, 0f, 0.8f));
                Retangulo(new Rect(cx - 2f * u, cy - 0.99f * r, 4f * u, 0.18f * r), Color.white);
                GUI.matrix = antes;
            }

            // o que fazer
            var titulo = EstilosDoHud.Rotulo(Mathf.RoundToInt(17 * u), FontStyle.Bold, TextAnchor.UpperCenter, true);
            var linha = EstilosDoHud.Rotulo(Mathf.RoundToInt(13 * u), FontStyle.Normal, TextAnchor.UpperCenter, true);
            string t1, t2;
            Color cor = new Color(0.97f, 0.95f, 0.88f);
            if (miniGame)
            {
                int acertadas = 0;
                foreach (var m in _marcas) if (!m.pequena && m.acertada) acertadas++;
                t1 = errou ? "Errou! As marcas laranja apagaram" : "Ligação direta: clique quando o ponteiro passar nas marcas";
                if (errou) cor = new Color(1f, 0.45f, 0.35f);
                t2 = $"<color=#FFCC38>dourada</color>: liga na hora   ·   <color=#EB7326>laranja</color>: acerte as 3 ({acertadas} de 3)\nPuxe para a esquerda: desistir   ·   botão direito: soltar os fios";
            }
            else if (partida)
            {
                t1 = "Dando a partida...";
                t2 = "Com a chave, o motor pega sozinho\nPuxe para a esquerda: desistir   ·   botão direito: soltar a chave";
            }
            else if (direta)
            {
                t1 = "Os fios da ignição estão juntos";
                t2 = "Arraste para a direita para tentar de novo   ·   para a esquerda: separar os fios\nClique ou botão direito: soltar";
            }
            else if (Carro.MotorLigado)
            {
                t1 = "Motor ligado";
                t2 = "Arraste para a esquerda até DESL para desligar\nClique ou botão direito: soltar a chave";
            }
            else if (e == Carro.EstadoDoMotor.Ignicao)
            {
                t1 = "Empurre para a direita até a PARTIDA";
                t2 = "A chave pesa e volta sozinha: empurre o mouse com força\nClique ou botão direito: soltar a chave";
            }
            else
            {
                t1 = "Arraste para a direita";
                t2 = "LIGA acende o painel; a PARTIDA gira o motor\nClique ou botão direito: soltar a chave";
            }
            Sombra(new Rect(cx - 320 * u, cy + r + 8 * u, 640 * u, 26 * u), t1, titulo, cor);
            Sombra(new Rect(cx - 320 * u, cy + r + 34 * u, 640 * u, 44 * u), t2, linha, new Color(0.85f, 0.83f, 0.78f));
        }

        static void Rotulo(Vector2 centro, float raio, float graus, string texto, GUIStyle estilo, float u, Color cor)
        {
            float a = graus * Mathf.Deg2Rad;
            var p = centro + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * raio;
            Sombra(new Rect(p.x - 40f * u, p.y - 10f * u, 80f * u, 20f * u), texto, estilo, cor);
        }

        // o desenho parado do mostrador (256 × 256, com as bordas suaves): o fundo escuro, o anel do mini-game (com as
        // marcas, na partida), o arco das posições da chave com as 3 marcas brancas (não na ligação direta, sem chave) e o
        // miolo cromado
        void PintarMostrador(bool partida, bool direta)
        {
            const int N = 256;
            if (_mostrador == null)
                _mostrador = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[N * N];
            float px = 2f / N;   // um pixel, com o raio do mostrador = 1
            float[] posicoes = { 0f, Carro.ChaveLiga, Carro.ChavePartida };
            var cinza = new Color(0.4f, 0.4f, 0.38f);
            var verdeEscuro = new Color(0.25f, 0.5f, 0.28f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) * px - 1f, dy = (y + 0.5f) * px - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);   // no sentido do relógio, a partir de cima
                    var c = new Color(0f, 0f, 0f, 0f);
                    c = Por(c, new Color(0.05f, 0.05f, 0.06f, 0.8f), Faixa(d, -1f, 0.995f, px));
                    var anel = new Color(0.2f, 0.2f, 0.2f);
                    if (partida)
                        foreach (var m in _marcas)
                            if (Mathf.Abs(Mathf.DeltaAngle(m.centro, ang)) <= m.largura / 2f) anel = m.acertada ? Verde : m.pequena ? Dourado : Laranja;
                    c = Por(c, anel, Faixa(d, 0.84f, 0.97f, px));
                    if (!direta && ang <= Carro.ChavePartida + 0.4f)
                    {
                        var arco = ang < Carro.ChaveDesliga ? cinza : ang < Carro.ChavePartida - 8f ? verdeEscuro : Laranja;
                        c = Por(c, arco, Faixa(d, 0.50f, 0.56f, px));
                    }
                    if (!direta)
                        foreach (float a in posicoes)
                            if (Mathf.Abs(Mathf.DeltaAngle(a, ang)) * Mathf.Deg2Rad * d < 0.012f) c = Por(c, new Color(0.95f, 0.95f, 0.9f), Faixa(d, 0.46f, 0.60f, px));
                    c = Por(c, new Color(0.3f, 0.3f, 0.29f), Faixa(d, -1f, 0.31f, px));
                    c = Por(c, new Color(0.5f, 0.5f, 0.48f), Faixa(d, -1f, 0.27f, px));
                    pixels[y * N + x] = c;
                }
            _mostrador.SetPixels32(pixels);
            _mostrador.Apply();
            _mostradorSujo = false;
            _mostradorNaPartida = partida;
            _mostradorDireto = direta;
        }

        // quanto do pixel está entre os raios a e b (a borda suave tem um pixel)
        static float Faixa(float d, float a, float b, float px) => Mathf.Clamp01((d - a) / px + 0.5f) * Mathf.Clamp01((b - d) / px + 0.5f);

        static Color Por(Color baixo, Color cima, float k)
        {
            k *= cima.a;
            return new Color(Mathf.Lerp(baixo.r, cima.r, k), Mathf.Lerp(baixo.g, cima.g, k), Mathf.Lerp(baixo.b, cima.b, k), baixo.a + (1f - baixo.a) * k);
        }

        static void Retangulo(Rect r, Color cor)
        {
            var antes = GUI.color;
            GUI.color = cor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        static void Sombra(Rect r, string texto, GUIStyle estilo, Color cor)
        {
            float s = Mathf.Max(1f, Mathf.Round(1.5f * Screen.height / 720f));
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + s, r.y + s, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }
    }
}
