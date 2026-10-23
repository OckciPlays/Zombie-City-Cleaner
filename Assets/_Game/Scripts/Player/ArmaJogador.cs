using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A arma do jogador local. Qual arma está na mão é a <see cref="JogadorRede.Arma"/>, que o anfitrião troca no suporte
    /// do bunker (<see cref="ParedeDeArmas"/>); os números de cada uma estão em <see cref="Armas"/>.
    /// Clique atira (raycast do centro da tela; as automáticas atiram com o gatilho apertado e a espingarda solta vários
    /// chumbos), R recarrega (a espingarda põe cartucho a cartucho, e atirar interrompe) e o botão direito mira (aproxima
    /// a câmera e os outros veem o corpo mirando). Sem bala, o gatilho só faz clique. Mirando uma arma do suporte, E troca.
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
        [Tooltip("Até onde a mira pega uma arma do suporte (m).")]
        public float alcanceDaMao = 2.2f;

        public int Municao { get; private set; }
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
        bool _marcaNaCabeca;
        VagaDeArma _vagaMirada;

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
        }

        /// <summary>A arma da mão mudou (no começo, ou trocada no suporte): mostra o modelo dela, cheia, sacando.</summary>
        public void Trocar(TipoDeArma nova)
        {
            StopAllCoroutines();
            Recarregando = false;
            Atual = nova;
            for (int i = 0; i < modelos.Length; i++) modelos[i].SetActive(i == (int)nova);
            Municao = Dados.capacidade;
            Animador.Play(SacarId, CamadaAcoes, 0f);
            _proximoTiro = Time.time + 0.4f;
        }

        void Update()
        {
            bool bot = MenuOnline.Bot;
            bool controla = bot || (!MenuOnline.Aberto && Cursor.lockState == CursorLockMode.Locked);
            bool mirar = controla && !bot && _mirar.IsPressed();
            if (rede.Mirando.Value != mirar) rede.Mirando.Value = mirar;
            var cam = rede.cameraMundo;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, mirar ? fovMirando : fovNormal, 1f - Mathf.Exp(-12f * Time.deltaTime));
            Animador.SetFloat(SpeedId, rede.Velocidade);
            ProcurarVaga(cam.transform);
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
            if (_vagaMirada != null && !bot && _interagir.WasPressedThisFrame())
                _vagaMirada.parede.PedirTroca(_vagaMirada.indice);
        }

        void Atirar()
        {
            var d = Dados;
            Municao--;
            Animador.Play(FireId, CamadaAcoes, 0f);
            Efeitos.Clarao(Boca, JogadorRede.CamadaBracos);
            Efeitos.Som(Efeitos.Tiros[(int)Atual], Boca.position, 1f, false);
            if (Atual == TipoDeArma.Espingarda) StartCoroutine(SomDepois(Efeitos.Bomba, 0.33f, 0.8f));
            movimento.Coice(d.coiceGraus);

            var cam = rede.cameraMundo.transform;
            _marcaNaCabeca = false;
            float abertura = d.dispersao + (movimento.Correndo ? d.dispersaoAndando * 2f : 0f);
            // as hitboxes andam com a animação: sincroniza a física antes do raio, para acertar onde o zumbi está agora
            Physics.SyncTransforms();
            var impactos = new JogadorRede.Impacto[d.chumbos];
            for (int k = 0; k < d.chumbos; k++)
            {
                Vector2 desvio = Random.insideUnitCircle * abertura;
                Vector3 direcao = cam.rotation * (Quaternion.Euler(desvio.y, desvio.x, 0f) * Vector3.forward);
                impactos[k] = Disparar(cam.position, direcao, d);
            }
            rede.AnunciarTiro(impactos);
        }

        /// <summary>Um raio (o tiro, ou um chumbo): o efeito aqui, o acerto ao anfitrião e o que os outros devem ver.</summary>
        JogadorRede.Impacto Disparar(Vector3 origem, Vector3 direcao, DadosDaArma d)
        {
            var impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Nada, normal = Vector3.up };
            int bracos = 0;
            // à queima-roupa, a hitbox colada na câmera leva o tiro primeiro
            var encostada = Encostada(origem, direcao, out Vector3 pontoEncostado);
            if (encostada != null)
            {
                impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = pontoEncostado, normal = -direcao };
                Carne(encostada, pontoEncostado, -direcao, d.dano);
                if (!EhBraco(encostada.regiao)) return impacto;
                bracos++;
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
                    Carne(hitbox, hit.point, hit.normal, d.dano);
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

        // um acerto num zumbi: sangue aqui na hora, a marca na mira e o acerto vai para o anfitrião
        void Carne(Hitbox hitbox, Vector3 ponto, Vector3 normal, float dano)
        {
            Sangue.Respingo(ponto, normal);
            rede.AcertarZumbi(hitbox.zumbi, hitbox.regiao, dano, ponto);
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

        // a mira numa arma do suporte, de perto: o que estiver mais perto na linha do olhar (gatilhos de outras coisas,
        // como as áreas internas, não contam)
        void ProcurarVaga(Transform cam)
        {
            _vagaMirada = null;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, _acertos, alcanceDaMao, ~0, QueryTriggerInteraction.Collide);
            float menor = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                if (hit.collider == _corpo || hit.distance >= menor) continue;
                var vaga = hit.collider.GetComponent<VagaDeArma>();
                if (hit.collider.isTrigger && vaga == null) continue;
                menor = hit.distance;
                _vagaMirada = vaga;
            }
            if (_vagaMirada != null)
            {
                var naVaga = _vagaMirada.parede.Vaga(_vagaMirada.indice);
                if (naVaga == null || naVaga.Value == Atual) _vagaMirada = null;
            }
        }

        void OnGUI()
        {
            if (MenuOnline.Aberto) return;
            float u = Screen.height / 720f, cx = Screen.width / 2f, cy = Screen.height / 2f;
            // mira: um ponto com quatro tracinhos
            Color cor = new Color(0.95f, 0.93f, 0.85f, 0.85f);
            float t = Mathf.Max(1f, Mathf.Round(2 * u)), g = 6 * u, l = 7 * u;
            Retangulo(cx - t / 2, cy - t / 2, t, t, cor);
            Retangulo(cx - g - l, cy - t / 2, l, t, cor); Retangulo(cx + g, cy - t / 2, l, t, cor);
            Retangulo(cx - t / 2, cy - g - l, t, l, cor); Retangulo(cx - t / 2, cy + g, t, l, cor);
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
            // a troca no suporte
            if (_vagaMirada != null)
            {
                var naVaga = _vagaMirada.parede.Vaga(_vagaMirada.indice);
                if (naVaga != null)
                {
                    var dica = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), alignment = TextAnchor.UpperCenter };
                    dica.normal.textColor = new Color(0.95f, 0.93f, 0.85f);
                    GUI.Label(new Rect(cx - 300 * u, cy + 40 * u, 600 * u, 30 * u),
                              $"[E] Pegar {Armas.Dados(naVaga.Value).nomeComArtigo} (deixa {d.nomeComArtigo})", dica);
                }
            }
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
