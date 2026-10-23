using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pistola do jogador local: clique atira (raycast do centro da tela), R recarrega e o botão direito mira
    /// (aproxima a câmera e os outros veem o corpo mirando). Sem bala, o gatilho só faz clique.
    /// Acertar um zumbi: o sangue sai na hora aqui, e o acerto (zumbi, região e ponto) vai para o anfitrião, que tira a
    /// vida e derruba o membro. O tiro atravessa até dois braços ou mãos e segue para o que estiver atrás. No cenário,
    /// faíscas e a marca do tiro.
    /// </summary>
    public class ArmaJogador : MonoBehaviour
    {
        public JogadorRede rede;
        public MovimentoPrimeiraPessoa movimento;
        public Transform bocaBracos;

        [Header("Pistola")]
        public int capacidade = 12;
        public float intervalo = 0.18f;
        public float tempoRecarga = 1.67f;
        public float alcance = 80f;
        public float coiceGraus = 1.6f;
        [Tooltip("Dispersão em graus parado e o acréscimo andando.")]
        public float dispersao = 0.3f;
        public float dispersaoAndando = 1.2f;
        [Tooltip("Dano de cada tiro (a vida dos zumbis é contada em tiros de pistola).")]
        public float dano = 1f;

        [Header("Mira")]
        public float fovNormal = 70f;
        public float fovMirando = 58f;

        public int Municao { get; private set; }
        public bool Recarregando { get; private set; }

        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int DryFireId = Animator.StringToHash("DryFire");
        static readonly int ReloadId = Animator.StringToHash("Reload");
        // o tiro não pega a cápsula do zumbi de perto (camada Ignore Raycast; quem leva o tiro são as hitboxes) nem os
        // pedaços caídos
        const int MascaraDoTiro = ~((1 << 2) | (1 << Sangue.CamadaPedacos));
        static readonly Collider[] _encostados = new Collider[8];

        InputAction _atirar, _recarregar, _mirar;
        CharacterController _corpo;
        float _proximoTiro, _marcaAte;
        bool _marcaNaCabeca;

        void Awake()
        {
            Municao = capacidade;
            _corpo = GetComponent<CharacterController>();
            var acoes = InputSystem.actions;
            _atirar = acoes.FindAction("Player/Attack", true);
            _recarregar = acoes.FindAction("Player/Reload", true);
            _mirar = acoes.FindAction("Player/Aim", true);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Recarregando = false;
        }

        void Update()
        {
            bool bot = MenuOnline.Bot;
            bool controla = bot || (!MenuOnline.Aberto && Cursor.lockState == CursorLockMode.Locked);
            bool mirar = controla && !bot && _mirar.IsPressed();
            if (rede.Mirando.Value != mirar) rede.Mirando.Value = mirar;
            var cam = rede.cameraMundo;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, mirar ? fovMirando : fovNormal, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (!controla) return;

            bool atirar = bot ? Mathf.Repeat(Time.time, 1.5f) < Time.deltaTime : _atirar.WasPressedThisFrame();
            bool recarregar = bot ? Municao == 0 : _recarregar.WasPressedThisFrame();
            if (recarregar && !Recarregando && Municao < capacidade) StartCoroutine(Recarregar());
            if (atirar && !Recarregando && Time.time >= _proximoTiro)
            {
                _proximoTiro = Time.time + intervalo;
                if (Municao > 0) Atirar();
                else
                {
                    rede.animadorBracos.SetTrigger(DryFireId);
                    Efeitos.Som(Efeitos.Clique, bocaBracos.position, 0.8f, false);
                }
            }
        }

        void Atirar()
        {
            Municao--;
            rede.animadorBracos.SetTrigger(FireId);
            Efeitos.Clarao(bocaBracos, JogadorRede.CamadaBracos);
            Efeitos.Som(Efeitos.Tiro, bocaBracos.position, 1f, false);
            movimento.Coice(coiceGraus);

            var cam = rede.cameraMundo.transform;
            _marcaNaCabeca = false;
            float abertura = dispersao + (movimento.Correndo ? dispersaoAndando * 2f : 0f);
            Vector3 direcao = Quaternion.Euler(Random.Range(-abertura, abertura), Random.Range(-abertura, abertura), 0f) * cam.forward;
            // as hitboxes andam com a animação: sincroniza a física antes do raio, para acertar onde o zumbi está agora
            Physics.SyncTransforms();
            var tipo = JogadorRede.TipoDeAcerto.Nada;
            Vector3 ponto = Vector3.zero, normal = Vector3.up;
            int bracos = 0;
            bool fim = false;
            // à queima-roupa, a hitbox colada na câmera leva o tiro primeiro
            var encostada = Encostada(cam.position, direcao, out Vector3 pontoEncostado);
            if (encostada != null)
            {
                ponto = pontoEncostado; normal = -direcao;
                Carne(encostada, ponto, normal);
                tipo = JogadorRede.TipoDeAcerto.Carne;
                if (EhBraco(encostada.regiao)) bracos++;
                else fim = true;
            }
            if (!fim)
            {
                // o tiro atravessa braços e mãos (até dois): numa multidão, as mãos de quem ataca ficam na frente da
                // mira e comiam o tiro que era do zumbi de trás
                int n = Physics.RaycastNonAlloc(cam.position, direcao, _acertos, alcance, MascaraDoTiro, QueryTriggerInteraction.Ignore);
                System.Array.Sort(_acertos, 0, n, PorDistancia);
                for (int i = 0; i < n; i++)
                {
                    var hit = _acertos[i];
                    // o olho fica bem no topo da cápsula do próprio jogador: o tiro para baixo (na cabeça de um zumbi
                    // perto, por exemplo) batia nela, a distância zero, e soltava faíscas em cima da cabeça
                    if (hit.collider == _corpo) continue;
                    var hitbox = hit.collider.GetComponent<Hitbox>();
                    if (hitbox != null && hitbox == encostada) continue;
                    ponto = hit.point; normal = hit.normal;
                    if (hitbox != null && hitbox.zumbi != null)
                    {
                        Carne(hitbox, ponto, normal);
                        tipo = JogadorRede.TipoDeAcerto.Carne;
                        if (EhBraco(hitbox.regiao) && ++bracos <= 2) continue;
                        break;
                    }
                    // furo só no cenário (em gente e em coisas que se mexem, só as faíscas)
                    tipo = hit.collider is CharacterController || hit.rigidbody != null ? JogadorRede.TipoDeAcerto.Outro : JogadorRede.TipoDeAcerto.Cenario;
                    Efeitos.Impacto(ponto, normal);
                    if (tipo == JogadorRede.TipoDeAcerto.Cenario) Sangue.FuroDeTiro(ponto, normal);
                    break;
                }
            }
            rede.AnunciarTiro(tipo, ponto, normal);
        }

        // folgado: com o buffer cheio, o Unity não garante que vieram os acertos mais perto (numa multidão, o raio cruza
        // várias hitboxes)
        static readonly RaycastHit[] _acertos = new RaycastHit[64];
        static readonly System.Collections.Generic.IComparer<RaycastHit> PorDistancia =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        static bool EhBraco(RegiaoDoCorpo r) => r == RegiaoDoCorpo.BracoE || r == RegiaoDoCorpo.AntebracoE || r == RegiaoDoCorpo.BracoD || r == RegiaoDoCorpo.AntebracoD;

        // um acerto num zumbi: sangue aqui na hora, a marca na mira e o acerto vai para o anfitrião
        void Carne(Hitbox hitbox, Vector3 ponto, Vector3 normal)
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
                float d = (p - origem).sqrMagnitude;
                if (d < menor) { menor = d; melhor = hitbox; ponto = p; }
            }
            return melhor;
        }

        IEnumerator Recarregar()
        {
            Recarregando = true;
            rede.animadorBracos.SetTrigger(ReloadId);
            Efeitos.Som(Efeitos.Recarga, bocaBracos.position, 0.7f, false);
            yield return new WaitForSeconds(tempoRecarga);
            Municao = capacidade;
            Recarregando = false;
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
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            estilo.normal.textColor = Municao == 0 ? new Color(1f, 0.45f, 0.4f) : new Color(0.9f, 0.88f, 0.8f);
            GUI.Label(new Rect(Screen.width - 260 * u, Screen.height - 50 * u, 240 * u, 36 * u), Recarregando ? "RECARREGANDO" : $"{Municao} / {capacidade}", estilo);
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
