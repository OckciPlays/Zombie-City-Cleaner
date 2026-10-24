using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A arma do jogador local. Qual arma está na mão é a <see cref="JogadorRede.Arma"/>, que o anfitrião troca no suporte
    /// do bunker (<see cref="ParedeDeArmas"/>); os números de cada uma estão em <see cref="Armas"/>.
    /// Clique atira (raycast do centro da tela; as automáticas atiram com o gatilho apertado e a espingarda solta vários
    /// chumbos), R recarrega (a espingarda põe cartucho a cartucho, e atirar interrompe) e o botão direito mira (aproxima
    /// a câmera e os outros veem o corpo mirando). Sem bala, o gatilho só faz clique. Mirando de perto uma coisa que se usa
    /// (<see cref="Interagivel"/>: uma arma do painel, a porta do bunker, um interruptor), a dica aparece e E usa.
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

        [Header("Primeira pessoa (um modelo por arma, na ordem de TipoDeArma)")]
        public GameObject[] modelos;
        public Animator[] animadores;
        public Transform[] bocas;

        [Header("Mira")]
        public float fovNormal = 70f;
        public float fovMirando = 58f;
        [Tooltip("Até onde a mira pega o que se usa com E: uma arma do painel, a porta, um interruptor (m).")]
        public float alcanceDaMao = 2.2f;

        public int Municao { get; private set; }
        /// <summary>Olhando pela luneta (a câmera já fechou): os braços somem e a tela mostra a luneta.</summary>
        public bool NaLuneta { get; private set; }
        public bool Recarregando { get; private set; }
        public TipoDeArma Atual { get; private set; }
        public DadosDaArma Dados => Armas.Dados(Atual);

        Animator Animador => animadores[(int)Atual];
        Transform Boca => bocas[(int)Atual];

        // os estados da camada "Ações" do Jogador_FP.controller: o código toca cada um do começo (no automático, um
        // tiro recomeça o anterior)
        const int CamadaAcoes = 1;
        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int DryFireId = Animator.StringToHash("DryFire");
        static readonly int ReloadId = Animator.StringToHash("Reload");
        static readonly int SacarId = Animator.StringToHash("Sacar");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        // o tiro não pega a cápsula do zumbi de perto (camada Ignore Raycast; quem leva o tiro são as hitboxes) nem os
        // corpos caídos
        const int MascaraDoTiro = ~((1 << 2) | (1 << CorpoDeZumbi.Camada));
        static readonly Collider[] _encostados = new Collider[8];

        InputAction _atirar, _recarregar, _mirar, _interagir;
        CharacterController _corpo;
        Coroutine _recarga;
        float _proximoTiro, _marcaAte;
        bool _marcaNaCabeca, _mirandoAntes;
        static Texture2D _texturaDaLuneta;
        Interagivel _alvo;
        string _dica;

        void Awake()
        {
            Municao = Dados.capacidade;
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
            SairDaLuneta();
        }

        void SairDaLuneta()
        {
            NaLuneta = false;
            if (rede != null && rede.IsOwner) AmbienteDoDia.NaLuneta = false;   // a neblina é da máquina de quem joga
            if (rede != null && rede.cameraBracos != null) rede.cameraBracos.enabled = rede.IsOwner;
            if (movimento != null) movimento.MultiplicadorDoOlhar = 1f;
        }

        /// <summary>A arma da mão mudou (no começo, ou trocada no suporte): mostra o modelo dela, cheia, sacando.</summary>
        public void Trocar(TipoDeArma nova)
        {
            StopAllCoroutines();
            Recarregando = false;
            Atual = nova;
            SairDaLuneta();
            for (int i = 0; i < modelos.Length; i++) modelos[i].SetActive(i == (int)nova);
            Municao = Dados.capacidade;
            Animador.Play(SacarId, CamadaAcoes, 0f);
            _proximoTiro = Time.time + 0.4f;
        }

        void Update()
        {
            bool bot = MenuOnline.Bot;
            bool controla = bot || MenuOnline.Controla;
            var dados = Dados;
            bool luneta = dados.fovLuneta > 0f;
            // recarregando, a sniper sai da luneta (a mão está no carregador). O robô de teste, com a sniper, fica na luneta
            // (testa o nível de detalhe dos zumbis pela luneta, na rede)
            bool mirar = controla && (bot ? luneta : _mirar.IsPressed()) && !(luneta && Recarregando);
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
            Animador.SetFloat(SpeedId, rede.Velocidade);
            ProcurarAlvo(cam.transform);
            if (!controla) return;

            var d = Dados;
            bool apertou = bot ? Mathf.Repeat(Time.time, 1.5f) < Time.deltaTime : _atirar.WasPressedThisFrame();
            bool atirar = apertou || (!bot && d.automatica && _atirar.IsPressed());
            bool recarregar = bot ? Municao == 0 : _recarregar.WasPressedThisFrame();
            if (recarregar && !Recarregando && Municao < d.capacidade) _recarga = StartCoroutine(Recarregar());
            // a espingarda para de pôr cartucho para atirar
            if (atirar && Recarregando && d.cartuchoACartucho && Municao > 0) PararRecarga();
            if (atirar && !Recarregando && Time.time >= _proximoTiro)
            {
                // no automático, o próximo tiro conta do anterior (sem perder a cadência com o tempo do quadro)
                _proximoTiro = (Time.time - _proximoTiro > Time.deltaTime ? Time.time : _proximoTiro) + d.intervalo;
                if (Municao > 0) Atirar();
                else if (apertou)
                {
                    Animador.Play(DryFireId, CamadaAcoes, 0f);
                    Efeitos.Som(Efeitos.Clique, Boca.position, 0.8f, false);
                }
            }
            if (_alvo != null && !bot && _interagir.WasPressedThisFrame()) _alvo.Usar(this);
        }

        void Atirar()
        {
            var d = Dados;
            Municao--;
            Animador.Play(FireId, CamadaAcoes, 0f);
            Efeitos.Clarao(Boca, JogadorRede.CamadaBracos);
            Efeitos.Som(Efeitos.Tiros[(int)Atual], Boca.position, 1f, false);
            if (Atual == TipoDeArma.Espingarda) StartCoroutine(SomDepois(Efeitos.Bomba, 0.33f, 0.8f));
            if (d.ferrolho) StartCoroutine(SomDepois(Efeitos.Ferrolho, 0.42f, 0.7f));
            movimento.Coice(d.coiceGraus);

            var cam = rede.cameraMundo.transform;
            _marcaNaCabeca = false;
            float abertura = (d.fovLuneta > 0f && !NaLuneta ? d.dispersaoSemLuneta : d.dispersao) + (movimento.Correndo ? d.dispersaoAndando * 2f : 0f);
            // as hitboxes andam com a animação: sincroniza a física antes do raio, para acertar onde o zumbi está agora
            Physics.SyncTransforms();
            var impactos = new JogadorRede.Impacto[d.chumbos];
            _carne.Clear();
            for (int k = 0; k < d.chumbos; k++)
            {
                // na espingarda um chumbo sai bem no meio da mira: é a parte que ele pega que conta (veja MandarAcertos)
                _chumboDoCentro = k == 0 && d.chumbos > 1;
                Vector2 desvio = _chumboDoCentro ? Vector2.zero : Random.insideUnitCircle * abertura;
                Vector3 direcao = cam.rotation * (Quaternion.Euler(desvio.y, desvio.x, 0f) * Vector3.forward);
                impactos[k] = Disparar(cam.position, direcao, d);
            }
            _chumboDoCentro = false;
            MandarAcertos(d);
            rede.AnunciarTiro(impactos);
        }

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
                    naCabeca |= _carne[k].hitbox.regiao == RegiaoDoCorpo.Cabeca;
                }
                for (int k = i; k < _carne.Count; k++)
                {
                    var h = _carne[k].hitbox;
                    if (h.zumbi != zumbi) continue;
                    int chumbos = 0;
                    for (int j = i; j < _carne.Count; j++)
                        if (_carne[j].hitbox.zumbi == zumbi && _carne[j].hitbox.regiao == h.regiao && _carne[j].hitbox.membro == h.membro) chumbos++;
                    // a conta vale o dobro mais o desempate: cabeça, depois tronco, depois membro
                    if (h.regiao == RegiaoDoCorpo.Cabeca) chumbos = dePerto ? 1000 : chumbos * 3 + 2;
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
            var faixa = Armas.Dano(d, c.distancia);
            float dano = EhMembro(c.hitbox.regiao) ? faixa.membro : faixa.corpo;
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
            // caía com dois tiros em vez de três)
            Zumbi ultimoZumbi = null;
            int ultimoMembro = -1;
            // à queima-roupa, a hitbox colada na câmera leva o tiro primeiro
            var encostada = Encostada(origem, direcao, out Vector3 pontoEncostado);
            if (encostada != null)
            {
                impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = pontoEncostado, normal = -direcao };
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
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                // o olho fica bem no topo da cápsula do próprio jogador: o tiro para baixo (na cabeça de um zumbi perto,
                // por exemplo) batia nela, a distância zero, e soltava faíscas em cima da cabeça
                if (hit.collider == _corpo) continue;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox == encostada) continue;
                if (hitbox != null && hitbox.zumbi != null)
                {
                    int membro = Membros.Limbo(hitbox.membro);
                    if (membro >= 0 && hitbox.zumbi == ultimoZumbi && membro == ultimoMembro) continue;
                    ultimoZumbi = hitbox.zumbi;
                    ultimoMembro = membro;
                    Carne(hitbox, hit.point, hit.normal, hit.distance);
                    impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };
                    if (EhBraco(hitbox.regiao) && ++bracos <= 2) continue;
                    break;
                }
                // furo só no cenário (em gente e em coisas que se mexem, só as faíscas)
                var tipo = hit.collider is CharacterController || hit.rigidbody != null ? JogadorRede.TipoDeAcerto.Outro : JogadorRede.TipoDeAcerto.Cenario;
                impacto = new JogadorRede.Impacto { tipo = tipo, ponto = hit.point, normal = hit.normal };
                Efeitos.Impacto(hit.point, hit.normal);
                if (tipo == JogadorRede.TipoDeAcerto.Cenario) Sangue.FuroDeTiro(hit.point, hit.normal);
                break;
            }
            return impacto;
        }

        // folgado: com o buffer cheio, o Unity não garante que vieram os acertos mais perto (numa multidão, o raio cruza
        // várias hitboxes)
        static readonly RaycastHit[] _acertos = new RaycastHit[64];
        static readonly System.Collections.Generic.IComparer<RaycastHit> PorDistancia =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        static bool EhBraco(RegiaoDoCorpo r) => r == RegiaoDoCorpo.Braco;

        // um acerto num zumbi: sangue aqui na hora e a marca na mira; o acerto vai para o anfitrião no fim do disparo
        void Carne(Hitbox hitbox, Vector3 ponto, Vector3 normal, float distancia)
        {
            Sangue.Respingo(ponto, normal);
            _carne.Add(new NaCarne { hitbox = hitbox, ponto = ponto, distancia = distancia, centro = _chumboDoCentro });
            _marcaAte = Time.time + 0.18f;
            _marcaNaCabeca |= hitbox.regiao == RegiaoDoCorpo.Cabeca;
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
                // um cartucho por vez; o cartucho entra perto do fim da animação (quadro 9 a 10 de 13)
                while (Municao < d.capacidade)
                {
                    Animador.Play(ReloadId, CamadaAcoes, 0f);
                    yield return new WaitForSeconds(d.tempoRecarga * 0.72f);
                    Efeitos.Som(Efeitos.Cartucho, Boca.position, 0.7f, false);
                    Municao++;
                    yield return new WaitForSeconds(d.tempoRecarga * 0.28f);
                }
            }
            else
            {
                Animador.Play(ReloadId, CamadaAcoes, 0f);
                yield return new WaitForSeconds(d.carregadorSai);
                Efeitos.Som(Efeitos.Carregador, Boca.position, 0.6f, false);
                yield return new WaitForSeconds(d.carregadorEntra - d.carregadorSai);
                Efeitos.Som(Efeitos.Carregador, Boca.position, 0.8f, false);
                yield return new WaitForSeconds(d.tempoRecarga - d.carregadorEntra);
                Municao = d.capacidade;
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

        // a mira numa coisa que se usa, de perto: o que estiver mais perto na linha do olhar (gatilhos de outras coisas,
        // como as áreas internas, não contam)
        void ProcurarAlvo(Transform cam)
        {
            _alvo = null;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, _acertos, alcanceDaMao, ~0, QueryTriggerInteraction.Collide);
            float menor = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                if (hit.collider == _corpo || hit.distance >= menor) continue;
                var alvo = hit.collider.GetComponent<Interagivel>();
                if (hit.collider.isTrigger && alvo == null) continue;
                menor = hit.distance;
                _alvo = alvo;
            }
            _dica = _alvo != null ? _alvo.Dica(this) : null;
            if (_dica == null) _alvo = null;
        }

        void OnGUI()
        {
            if (MenuOnline.Aberto) return;
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
            var d = Dados;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            estilo.normal.textColor = Municao == 0 ? new Color(1f, 0.45f, 0.4f) : new Color(0.9f, 0.88f, 0.8f);
            string texto = Recarregando && !d.cartuchoACartucho ? "RECARREGANDO" : $"{Municao} / {d.capacidade}";
            GUI.Label(new Rect(Screen.width - 260 * u, Screen.height - 50 * u, 240 * u, 36 * u), texto, estilo);
            var nome = new GUIStyle(estilo) { fontSize = Mathf.RoundToInt(14 * u) };
            nome.normal.textColor = new Color(0.75f, 0.73f, 0.66f);
            GUI.Label(new Rect(Screen.width - 260 * u, Screen.height - 74 * u, 240 * u, 24 * u), d.nome.ToUpperInvariant(), nome);
            // o que se usa com E (pegar uma arma do painel, abrir a porta, acender a luz)
            if (_dica != null)
            {
                // com uma sombra embaixo, para ler na frente de coisa clara (a porta na luz da lanterna)
                var dica = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), alignment = TextAnchor.UpperCenter };
                dica.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                float s = Mathf.Max(1f, Mathf.Round(1.5f * u));
                GUI.Label(new Rect(cx - 300 * u + s, cy + 40 * u + s, 600 * u, 30 * u), _dica, dica);
                dica.normal.textColor = new Color(0.95f, 0.93f, 0.85f);
                GUI.Label(new Rect(cx - 300 * u, cy + 40 * u, 600 * u, 30 * u), _dica, dica);
            }
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
