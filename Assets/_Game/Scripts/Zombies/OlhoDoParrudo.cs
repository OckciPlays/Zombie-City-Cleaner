using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O olho do Parrudo (a pedido em 31/10/2026): o único olho, no meio das costas, é o ponto fraco do tanque.
    /// Ciclo: fechado (4 a 7 s) → abre (0,35 s) → aberto (2,5 a 4 s) → fecha (0,25 s). Aberto, o olho segue o jogador
    /// quando ele está atrás do Parrudo e, se não, olha em volta (giros curtos e rápidos, como um olho de verdade).
    /// As pálpebras e o globo são ossos próprios do modelo do Blender ("palpebra_sup", "palpebra_inf", "olho"), filhos do
    /// spine_03: o giro é feito no espaço do pai, no LateUpdate (depois da animação Humanoid, que não mexe neles).
    /// <see cref="pontoFraco"/> (o colisor do olho) só fica ligado com o olho aberto (<see cref="Exposto"/>); o dano
    /// maior nele fica para quando o Parrudo virar um zumbi do jogo.
    /// </summary>
    public class OlhoDoParrudo : MonoBehaviour
    {
        public enum ModoDoOlho { Automatico, SempreAberto, SempreFechado }

        /// <summary>O modo de todos os Parrudos (o F1 troca).</summary>
        public static ModoDoOlho Modo = ModoDoOlho.Automatico;
        public static readonly string[] NomesDosModos = { "Automático", "Aberto", "Fechado" };

        public Transform olho, palpebraSup, palpebraInf;
        [Tooltip("O colisor do ponto fraco (no osso do olho): ligado só com o olho aberto")]
        public Collider pontoFraco;
        [Tooltip("Graus que cada pálpebra gira para fechar (a borda vai de 52° até o meio do olho)")]
        public float anguloDeFechar = 53f;
        public Vector2 tempoFechado = new Vector2(4f, 7f);
        public Vector2 tempoAberto = new Vector2(2.5f, 4f);
        public float tempoDeAbrir = 0.35f, tempoDeFechar = 0.25f;
        [Tooltip("Giro máximo do globo para os lados e para cima/baixo")]
        public float giroMaximo = 25f, inclinacaoMaxima = 15f;
        [Tooltip("Até quantos metros o olho segue o jogador")]
        public float alcanceDoOlhar = 25f;

        enum Estado { Fechado, Abrindo, Aberto, Fechando }
        Estado _estado = Estado.Fechado;
        float _ate;
        Quaternion _olhoRepouso, _supRepouso, _infRepouso;
        Vector3 _eixoLado, _eixoCima, _frente;        // no espaço do pai (spine_03), na pose de repouso
        float _giro, _inclinacao, _giroAlvo, _inclinacaoAlvo, _proximoOlhar;
        float _velGiro, _velInclinacao;

        /// <summary>0 = fechado, 1 = aberto.</summary>
        public float Abertura { get; private set; }
        /// <summary>O olho está aberto o bastante para ser acertado.</summary>
        public bool Exposto => Abertura > 0.6f;

        void Awake()
        {
            if (olho == null || palpebraSup == null || palpebraInf == null) { enabled = false; return; }
            _olhoRepouso = olho.localRotation;
            _supRepouso = palpebraSup.localRotation;
            _infRepouso = palpebraInf.localRotation;
            // o olho olha para trás do Parrudo; o "cima" é o do personagem (a pose do prefab é a de repouso)
            var pai = olho.parent;
            Quaternion inv = Quaternion.Inverse(pai.rotation);
            _frente = (inv * -transform.forward).normalized;
            _eixoCima = (inv * transform.up).normalized;
            _eixoLado = Vector3.Cross(_eixoCima, _frente).normalized;   // girar "cima" em torno dele leva à frente
            _ate = Time.time + Random.Range(1f, tempoFechado.y);
        }

        void LateUpdate()
        {
            float agora = Time.time, dt = Time.deltaTime;
            switch (Modo)
            {
                case ModoDoOlho.SempreAberto: Abertura = Mathf.MoveTowards(Abertura, 1f, dt / tempoDeAbrir); _estado = Estado.Aberto; break;
                case ModoDoOlho.SempreFechado: Abertura = Mathf.MoveTowards(Abertura, 0f, dt / tempoDeFechar); _estado = Estado.Fechado; break;
                default: Ciclo(agora, dt); break;
            }
            Olhar(agora, dt);

            // pálpebras: a de cima gira para baixo, a de baixo para cima (fechar = ângulo cheio)
            float fechar = anguloDeFechar * (1f - Suave(Abertura));
            palpebraSup.localRotation = Quaternion.AngleAxis(fechar, _eixoLado) * _supRepouso;
            palpebraInf.localRotation = Quaternion.AngleAxis(fechar, -_eixoLado) * _infRepouso;
            olho.localRotation = Quaternion.AngleAxis(_giro, _eixoCima) * Quaternion.AngleAxis(-_inclinacao, _eixoLado) * _olhoRepouso;
            if (pontoFraco != null) pontoFraco.enabled = Exposto;
        }

        void Ciclo(float agora, float dt)
        {
            switch (_estado)
            {
                case Estado.Fechado:
                    Abertura = 0f;
                    if (agora >= _ate) _estado = Estado.Abrindo;
                    break;
                case Estado.Abrindo:
                    Abertura = Mathf.MoveTowards(Abertura, 1f, dt / tempoDeAbrir);
                    if (Abertura >= 1f) { _estado = Estado.Aberto; _ate = agora + Random.Range(tempoAberto.x, tempoAberto.y); }
                    break;
                case Estado.Aberto:
                    Abertura = 1f;
                    if (agora >= _ate) _estado = Estado.Fechando;
                    break;
                case Estado.Fechando:
                    Abertura = Mathf.MoveTowards(Abertura, 0f, dt / tempoDeFechar);
                    if (Abertura <= 0f) { _estado = Estado.Fechado; _ate = agora + Random.Range(tempoFechado.x, tempoFechado.y); }
                    break;
            }
        }

        // aberto: segue o jogador se ele está atrás (no campo do olho); senão, olhadas curtas em volta
        void Olhar(float agora, float dt)
        {
            bool seguiu = false;
            var jogador = JogadorRede.Local != null ? JogadorRede.Local.transform : null;
            if (jogador != null && Abertura > 0.2f)
            {
                Vector3 d = jogador.position + Vector3.up * 1.5f - olho.position;
                if (d.magnitude < alcanceDoOlhar)
                {
                    Vector3 local = Quaternion.Inverse(olho.parent.rotation) * d;
                    float frente = Vector3.Dot(local, _frente);
                    if (frente > 0.1f)
                    {
                        // girar em torno do "cima" leva a frente para o lado de _eixoLado (cima × frente)
                        _giroAlvo = Mathf.Atan2(Vector3.Dot(local, _eixoLado), frente) * Mathf.Rad2Deg;
                        _inclinacaoAlvo = Mathf.Atan2(Vector3.Dot(local, _eixoCima), frente) * Mathf.Rad2Deg;
                        seguiu = true;
                    }
                }
            }
            if (!seguiu && agora >= _proximoOlhar)
            {
                _giroAlvo = Random.Range(-giroMaximo, giroMaximo) * 0.8f;
                _inclinacaoAlvo = Random.Range(-inclinacaoMaxima, inclinacaoMaxima) * 0.7f;
                _proximoOlhar = agora + Random.Range(0.4f, 1.3f);
            }
            _giroAlvo = Mathf.Clamp(_giroAlvo, -giroMaximo, giroMaximo);
            _inclinacaoAlvo = Mathf.Clamp(_inclinacaoAlvo, -inclinacaoMaxima, inclinacaoMaxima);
            float tempo = seguiu ? 0.12f : 0.05f;   // as olhadas em volta são rápidas (sacadas)
            _giro = Mathf.SmoothDamp(_giro, _giroAlvo, ref _velGiro, tempo, Mathf.Infinity, dt);
            _inclinacao = Mathf.SmoothDamp(_inclinacao, _inclinacaoAlvo, ref _velInclinacao, tempo, Mathf.Infinity, dt);
        }

        static float Suave(float t) => t * t * (3f - 2f * t);
    }
}
