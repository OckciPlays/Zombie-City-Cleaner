using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pistola do jogador local: clique atira (raycast do centro da tela), R recarrega e o botão direito mira
    /// (aproxima a câmera e os outros veem o corpo mirando). Sem bala, o gatilho só faz clique.
    /// Os acertos em zumbis entram na Parte 6; por enquanto o tiro marca o cenário.
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

        [Header("Mira")]
        public float fovNormal = 70f;
        public float fovMirando = 58f;

        public int Municao { get; private set; }
        public bool Recarregando { get; private set; }

        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int DryFireId = Animator.StringToHash("DryFire");
        static readonly int ReloadId = Animator.StringToHash("Reload");
        const int IgnorarRaycast = 1 << 2;

        InputAction _atirar, _recarregar, _mirar;
        float _proximoTiro;

        void Awake()
        {
            Municao = capacidade;
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
            float abertura = dispersao + (movimento.Correndo ? dispersaoAndando * 2f : 0f);
            Vector3 direcao = Quaternion.Euler(Random.Range(-abertura, abertura), Random.Range(-abertura, abertura), 0f) * cam.forward;
            bool acertou = Physics.Raycast(cam.position, direcao, out RaycastHit hit, alcance, ~IgnorarRaycast, QueryTriggerInteraction.Ignore);
            if (acertou) Efeitos.Impacto(hit.point, hit.normal);
            rede.AnunciarTiro(acertou, acertou ? hit.point : Vector3.zero, acertou ? hit.normal : Vector3.up);
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
