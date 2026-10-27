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
    public class PassageiroDoCarro : MonoBehaviour
    {
        // o olho de quem senta, a partir do quadril (no espaço do carro)
        static readonly Vector3 OlhoNoQuadril = new Vector3(0f, 0.70f, -0.08f);
        // entrando: a porta abre e, depois, ele vai até o banco; saindo, desce do banco até o lado da porta
        const float MoverDe = 0.25f, SairDe = 0.3f;
        const float MoverAte = Carro.FecharEm;

        static readonly int MascaraDoCenario = ~((1 << 2) | (1 << Carro.CamadaVeiculo) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
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
        bool _ccLigado;
        Transform _cabeca, _camera;
        Vector3 _cabecaLocal;
        float _yaw, _pitch;
        // a câmera de trás do carro (V)
        static bool _terceira;
        bool _corpoVisivel;
        float _yaw3, _pitch3 = 12f, _distancia3 = 6f, _mexeuNaCamera;
        string _aviso;
        float _avisoAte;
        // a chave: pega (um clique nela), o mouse gira a chave e não olha; na partida, o mini-game
        bool _segurandoChave, _naPartidaAntes, _fervendoAntes;
        float _semForcaDesde = -1f;
        /// <summary>Graus que a chave gira por pixel do mouse, arrastando para a direita (da ignição para a partida ela pesa: <see cref="Carro.pesoDaPartida"/>).</summary>
        const float GrausPorPixel = 0.35f;
        // o mini-game da partida: um ponteiro dá voltas no anel do mostrador, e o clique tem de pegar ele em cima das
        // marcas (graus; a folga cobre o atraso do clique)
        const float VelocidadeDoPonteiro = 240f, VoltasDaPartida = 3f, LarguraPequena = 9f, LarguraGrande = 28f, Folga = 2f;
        struct Marca { public float centro, largura; public bool pequena, acertada; }
        readonly List<Marca> _marcas = new List<Marca>();
        float _ponteiro, _voltas, _errouEm = -99f;
        Texture2D _mostrador;
        bool _mostradorSujo = true, _mostradorNaPartida;
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
            _rede.EsconderNaMao(true);
            if (!_rede.IsOwner) return;
            _movimento.enabled = false;
            _arma.enabled = false;
            _rede.bracos.SetActive(false);
            _rede.cameraMundo.fieldOfView = _arma.fovNormal;
            _cabecaAntes = _cabeca.localRotation;
            _yaw = 0f;
            _pitch = 6f;
            _yaw3 = 0f;
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
            _rede.animadorCorpo.enabled = true;
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
            _cabeca.localPosition = _cabecaLocal;
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            CorpoVisivel(false);
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
            if (_fase == Fase.Fora) return;
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
            if (_fase == Fase.Dentro || _tempo >= MoverDe) Pose(Lugar == 0);
            if (_fase == Fase.Entrando && _tempo >= MoverAte) _fase = Fase.Dentro;
            if (!dono) return;
            Comandos();
            if (_fase == Fase.Dentro && _terceira) CameraDeTras(dt);
            else Olhar(k);
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
            if (_tempo < MoverAte) Pose(_lugarSaindo == 0);
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
                Vector2 m = controla ? _mover.ReadValue<Vector2>() : Vector2.zero;
                Carro.Comandar(m.y, m.x, controla && _pular.IsPressed());
                Chave(controla);
                if (Carro.Fervendo && !_fervendoAntes) Avisar("O motor ferveu e apagou: espere esfriar e troque o radiador (capô)", 4f);
                _fervendoAntes = Carro.Fervendo;
                if (m.y > 0.3f && !Carro.MotorLigado && !_segurandoChave && Time.time > _avisoAte)
                    Avisar(Carro.Estado == Carro.EstadoDoMotor.SemChave ? "O motor está desligado: ponha a chave na ignição" : "O motor está desligado: gire a chave");
                if (controla && teclado != null && teclado.lKey.wasPressedThisFrame) Carro.AlternarFarois();
                Carro.Buzinar(controla && teclado != null && teclado.bKey.isPressed);
                if (controla && _desvirar.WasPressedThisFrame() && Carro.PodeDesvirar) Carro.Desvirar();
            }
            if (!controla) return;
            if (teclado != null && teclado.vKey.wasPressedThisFrame) _terceira = !_terceira;
            if (_fase == Fase.Dentro && _interagir.WasPressedThisFrame())
            {
                if (Carro.Velocidade > Carro.VelocidadeParaSair) Avisar("Pare o carro para sair");
                else Carro.PedirSair();
            }
        }

        /// <summary>O olhar do motorista (na primeira pessoa) está no miolo da ignição.</summary>
        bool MirandoAChave()
        {
            if (Lugar != 0 || _fase != Fase.Dentro || _terceira || Carro == null || Carro.ignicao == null) return false;
            Vector3 para = Carro.ignicao.position - _camera.position;
            return para.magnitude < 1.5f && Vector3.Angle(_camera.forward, para) < MiraNaChave;
        }

        /// <summary>
        /// A chave: olhando para a ignição, um clique põe a chave; outro pega a chave (o mouse para de olhar e aparece o
        /// mostrador). Arrastar gira: para a direita, a ignição e a partida (da ignição para a partida ela pesa e volta
        /// sozinha, é preciso empurrar; chegando, fica presa); para a esquerda, desliga. Presa na partida, o motor de
        /// arranque gira e o mini-game começa: um ponteiro dá voltas no anel, e o clique tem de pegar ele em cima das
        /// marcas. A pequena (dourada) liga na hora; as 3 grandes (laranja) ligam quando todas foram acertadas, e errar
        /// apaga as já acertadas. Em <see cref="VoltasDaPartida"/> voltas sem ligar, a chave volta para a ignição. Ligou,
        /// a chave volta sozinha para a ignição e é solta. Fora da partida, o clique (ou, sempre, o botão direito) solta a
        /// chave; puxar para a esquerda na partida desiste.
        /// </summary>
        void Chave(bool controla)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!_segurandoChave)
            {
                if (!controla || !mouse.leftButton.wasPressedThisFrame || !MirandoAChave()) return;
                if (Carro.Estado == Carro.EstadoDoMotor.SemChave) Carro.PorAChave();
                else
                {
                    _segurandoChave = true;
                    _naPartidaAntes = false;
                    _mostradorSujo = true;
                }
                return;
            }
            if (!controla || _terceira || _fase != Fase.Dentro || mouse.rightButton.wasPressedThisFrame)
            {
                SoltarAChave();
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
            if (partida)
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
            CorpoVisivel(false);
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
            if (_fase == Fase.Dentro && !_segurandoChave)
            {
                Vector2 olhar = LerOlhar();
                _yaw = Mathf.Clamp(_yaw + olhar.x, -135f, 135f);
                _pitch = Mathf.Clamp(_pitch - olhar.y, -60f, 75f);
            }
            Vector3 olho = _quadrilNaRaiz + OlhoNoQuadril;
            _cabeca.localPosition = Vector3.Lerp(_cabecaLocal, olho, k);
            _cabeca.localRotation = Quaternion.Slerp(_cabecaAntes, Quaternion.Euler(_pitch, _yaw, 0f), k);
        }

        // a câmera atrás do carro: o mouse gira em volta dele; andando, ela volta sozinha para trás
        void CameraDeTras(float dt)
        {
            CorpoVisivel(true);
            Vector2 olhar = LerOlhar();
            if (olhar.sqrMagnitude > 0.0001f) _mexeuNaCamera = Time.time;
            _yaw3 += olhar.x;
            _pitch3 = Mathf.Clamp(_pitch3 - olhar.y, -5f, 50f);
            if (Time.time - _mexeuNaCamera > 1.5f && Carro.Velocidade > 3f)
                _yaw3 = Mathf.LerpAngle(_yaw3, 0f, 1f - Mathf.Exp(-2f * dt));
            Vector3 frente = Vector3.ProjectOnPlane(Carro.transform.forward, Vector3.up);
            float rumo = frente.sqrMagnitude > 0.01f ? Quaternion.LookRotation(frente).eulerAngles.y : Carro.transform.eulerAngles.y;
            var rot = Quaternion.Euler(_pitch3, rumo + _yaw3, 0f);
            Vector3 pivo = Carro.transform.position + Vector3.up * 1.4f;
            float distancia = 6f;
            if (Physics.SphereCast(pivo, 0.25f, rot * Vector3.back, out var batida, distancia, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                distancia = Mathf.Max(0.8f, batida.distance - 0.1f);
            // aproxima na hora (para não atravessar a parede) e afasta devagar
            _distancia3 = distancia < _distancia3 ? distancia : Mathf.Lerp(_distancia3, distancia, 1f - Mathf.Exp(-3f * dt));
            _camera.SetPositionAndRotation(pivo + rot * Vector3.back * _distancia3, rot);
        }

        // o próprio corpo: só sombra na primeira pessoa (como fora do carro), inteiro na câmera de trás
        void CorpoVisivel(bool visivel)
        {
            if (_corpoVisivel == visivel) return;
            _corpoVisivel = visivel;
            _rede.CorpoDoDono(visivel);
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
        void Pose(bool motorista)
        {
            if (_repouso.Count == 0) return;
            var animador = _rede.animadorCorpo;
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

        void Apontar(string nome, Vector3 direcao)
        {
            if (!_ossos.TryGetValue(nome, out var osso)) return;
            Vector3 d = transform.TransformDirection(direcao.normalized);
            osso.rotation = Quaternion.FromToRotation(osso.up, d) * osso.rotation;
        }

        // ------------------------------------------------------------------------------------------ na tela

        void OnGUI()
        {
            if (_rede == null || !_rede.IsOwner || _fase == Fase.Fora || _fase == Fase.Saindo) return;
            if (MenuOnline.Aberto || MenuOnline.ComTela || Carro == null) return;
            float u = Screen.height / 720f;
            if (Lugar == 0)
            {
                var vel = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight, richText = true };
                string texto = $"{Mathf.RoundToInt(Carro.Velocidade * 3.6f)}<size={Mathf.RoundToInt(14 * u)}> km/h</size>";
                Sombra(new Rect(Screen.width - 260 * u, Screen.height - 58 * u, 240 * u, 44 * u), texto, vel, new Color(0.9f, 0.88f, 0.8f));
                var f = new GUIStyle(vel) { fontSize = Mathf.RoundToInt(13 * u), fontStyle = FontStyle.Normal };
                float ly = Screen.height - 80 * u;
                if (Carro.FaroisLigados && Carro.Pecas.BateriaAcende)
                {
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), "FARÓIS", f, new Color(0.95f, 0.85f, 0.5f));
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
                // as rodas: furada ou faltando
                var pneus = Carro.Pneus;
                if (pneus.Ruins > 0)
                {
                    bool falta = false;
                    for (int i = 0; i < Vehicles.PneusDoCarro.Quantas; i++) falta |= !pneus.Tem(i);
                    Sombra(new Rect(Screen.width - 260 * u, ly, 240 * u, 22 * u), falta ? "FALTA UMA RODA" : pneus.Ruins > 1 ? "PNEUS FURADOS" : "PNEU FURADO", f, new Color(1f, 0.75f, 0.3f));
                }
            }
            var dica = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * u), alignment = TextAnchor.LowerCenter };
            string linha = Lugar == 0 ? "WASD: dirigir   ·   Espaço: freio de mão   ·   L: faróis   ·   B: buzina   ·   F: lanterna   ·   V: câmera   ·   E: sair"
                : "F: lanterna   ·   V: câmera   ·   E: sair";
            Sombra(new Rect(Screen.width / 2f - 400 * u, Screen.height - 34 * u, 800 * u, 24 * u), linha, dica, new Color(0.8f, 0.78f, 0.72f));
            if (Lugar == 0) TelaDaChave(u, dica);
            string aviso = Lugar == 0 && Carro.PodeDesvirar ? "[R] Desvirar o carro" : Time.time < _avisoAte ? _aviso : null;
            if (aviso != null)
            {
                var a = new GUIStyle(dica) { fontSize = Mathf.RoundToInt(18 * u), alignment = TextAnchor.MiddleCenter };
                Sombra(new Rect(Screen.width / 2f - 300 * u, Screen.height / 2f + 130 * u, 600 * u, 30 * u), aviso, a, new Color(0.95f, 0.93f, 0.85f));
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
            var meio = new GUIStyle(dica) { fontSize = Mathf.RoundToInt(16 * u), alignment = TextAnchor.MiddleCenter };
            if (_terceira)
            {
                if (!ligado) Sombra(new Rect(cx - 300 * u, Screen.height - 62 * u, 600 * u, 24 * u), "Para dar a partida, volte para dentro do carro (V)", dica, new Color(0.95f, 0.85f, 0.55f));
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
                string texto = e == Carro.EstadoDoMotor.SemChave ? "[Clique] Pôr a chave" : "[Clique] Pegar a chave";
                Sombra(new Rect(cx - 320 * u, cy + 14 * u, 640 * u, 26 * u), texto, meio, new Color(0.97f, 0.95f, 0.88f));
            }
            else if (!ligado)
            {
                string texto = e == Carro.EstadoDoMotor.SemChave ? "Olhe para a ignição, à direita do volante, e clique para pôr a chave"
                    : "Olhe para a chave e clique nela para dar a partida";
                Sombra(new Rect(cx - 360 * u, Screen.height - 62 * u, 720 * u, 24 * u), texto, dica, new Color(0.95f, 0.85f, 0.55f));
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
            if (_mostrador == null || _mostradorSujo || _mostradorNaPartida != partida) PintarMostrador(partida);
            float r = 140f * u;
            bool errou = Time.time - _errouEm < 0.8f;
            if (Time.time - _errouEm < 0.25f) cx += Mathf.Sin(Time.time * 90f) * 4f * u;   // errou: treme
            var centro = new Vector2(cx, cy);
            GUI.DrawTexture(new Rect(cx - r, cy - r, 2 * r, 2 * r), _mostrador);

            // as posições da chave (a de agora mais clara)
            float angulo = Carro.AnguloDaChave;
            var rotulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(10 * u), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Rotulo(centro, r * 0.66f, 0f, "DESL", rotulo, u, angulo < Carro.ChaveDesliga ? Color.white : new Color(0.7f, 0.7f, 0.68f));
            Rotulo(centro, r * 0.66f, Carro.ChaveLiga, "LIGA", rotulo, u, angulo >= Carro.ChaveDesliga && angulo < Carro.ChavePartida - 8f ? new Color(0.7f, 1f, 0.7f) : new Color(0.5f, 0.75f, 0.5f));
            Rotulo(centro, r * 0.66f, Carro.ChavePartida + 12f, "PARTIDA", rotulo, u, angulo >= Carro.ChavePartida - 8f ? new Color(1f, 0.75f, 0.45f) : new Color(0.85f, 0.5f, 0.25f));

            // a chave, vista de frente: o cabo atravessando o miolo, com a ponta clara mostrando a posição
            var antes = GUI.matrix;
            GUIUtility.RotateAroundPivot(angulo, centro);
            Retangulo(new Rect(cx - 0.075f * r, cy - 0.44f * r, 0.15f * r, 0.66f * r), new Color(0.03f, 0.03f, 0.03f, 0.95f));
            Retangulo(new Rect(cx - 0.055f * r, cy - 0.42f * r, 0.11f * r, 0.62f * r), new Color(0.13f, 0.13f, 0.14f));
            Retangulo(new Rect(cx - 0.03f * r, cy - 0.42f * r, 0.06f * r, 0.10f * r), new Color(0.95f, 0.9f, 0.75f));
            GUI.matrix = antes;

            // o ponteiro do mini-game
            if (partida)
            {
                GUIUtility.RotateAroundPivot(_ponteiro, centro);
                Retangulo(new Rect(cx - 3.5f * u, cy - 1.0f * r, 7f * u, 0.2f * r), new Color(0f, 0f, 0f, 0.8f));
                Retangulo(new Rect(cx - 2f * u, cy - 0.99f * r, 4f * u, 0.18f * r), Color.white);
                GUI.matrix = antes;
            }

            // o que fazer
            var titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, richText = true };
            var linha = new GUIStyle(titulo) { fontSize = Mathf.RoundToInt(13 * u), fontStyle = FontStyle.Normal };
            string t1, t2;
            Color cor = new Color(0.97f, 0.95f, 0.88f);
            if (partida)
            {
                int acertadas = 0;
                foreach (var m in _marcas) if (!m.pequena && m.acertada) acertadas++;
                t1 = errou ? "Errou! As marcas laranja apagaram" : "Clique quando o ponteiro passar nas marcas";
                if (errou) cor = new Color(1f, 0.45f, 0.35f);
                t2 = $"<color=#FFCC38>dourada</color>: liga na hora   ·   <color=#EB7326>laranja</color>: acerte as 3 ({acertadas} de 3)\nPuxe para a esquerda: desistir   ·   botão direito: soltar a chave";
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
        // marcas, na partida), o arco das posições da chave com as 3 marcas brancas e o miolo cromado
        void PintarMostrador(bool partida)
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
                    if (ang <= Carro.ChavePartida + 0.4f)
                    {
                        var arco = ang < Carro.ChaveDesliga ? cinza : ang < Carro.ChavePartida - 8f ? verdeEscuro : Laranja;
                        c = Por(c, arco, Faixa(d, 0.50f, 0.56f, px));
                    }
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
