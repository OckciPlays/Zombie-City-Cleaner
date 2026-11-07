using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O olho do Brutamonte fora do jogo (04/11/2026): o que mexe o olho na cena de comparação
    /// (Scenes/Exemplos/Comparacao_Zumbis, <see cref="ComparacaoDeZumbis"/>), sem ZombieManager e sem rede. No jogo, quem mexe
    /// o olho é o próprio Zumbi, pela fase dele (Zumbi.Brutamonte.cs). Era o OlhoDoParrudo (31/10/2026, o protótipo do olho
    /// nas costas, antes de o Parrudo virar o Brutamonte); o arquivo trocou de nome com o mesmo GUID.
    /// <para>
    /// <see cref="modo"/>: Automático (o ciclo: fechado 4 a 7 s → abre em 0,35 s → aberto 2,5 a 4 s → fecha em 0,25 s), sempre
    /// aberto ou sempre fechado. Aberto, o globo segue o <see cref="alvo"/> (sem alvo, a câmera principal) quando ele está do
    /// lado das costas e, se não, dá olhadas curtas em volta (sacadas), até ±35° para os lados e ±20° para cima e para baixo.
    /// A pálpebra fecha 38° (as bordas das pálpebras do Brutamonte se encontram a ~36°).
    /// </para>
    /// <para>
    /// Os ossos ("olho", "palpebra_sup", "palpebra_inf", filhos do chest) são achados pelo nome embaixo deste objeto (a raiz
    /// do personagem), e o repouso e os eixos saem das bindposes da malha (<see cref="PoseDoOlho"/>), antes de qualquer
    /// animação. Roda no LateUpdate, depois do Animator (os clipes escrevem nos ossos do olho a cada quadro).
    /// Com a mesma <see cref="semente"/>, dois olhos piscam juntos (a comparação lado a lado).
    /// </para>
    /// </summary>
    public class OlhoDoBrutamonte : MonoBehaviour
    {
        public enum ModoDoOlho { Automatico, SempreAberto, SempreFechado }

        /// <summary>Os nomes dos modos, na ordem do enum (os botões do painel).</summary>
        public static readonly string[] NomesDosModos = { "Automático", "Aberto", "Fechado" };

        public ModoDoOlho modo = ModoDoOlho.Automatico;
        [Tooltip("Para onde o olho aberto olha (vazio: a câmera principal)")]
        public Transform alvo;
        [Tooltip("Desligado, o olho só dá as olhadas em volta")]
        public bool seguirAlvo = true;
        [Tooltip("Os ossos (vazios: achados pelo nome embaixo deste objeto)")]
        public Transform olho, palpebraSup, palpebraInf;
        [Tooltip("O colisor do ponto fraco (opcional): ligado só com o olho aberto")]
        public Collider pontoFraco;
        [Tooltip("Graus que cada pálpebra gira para fechar (as bordas do Brutamonte se encontram a ~36°)")]
        public float anguloDeFechar = 38f;
        public Vector2 tempoFechado = new Vector2(4f, 7f);
        public Vector2 tempoAberto = new Vector2(2.5f, 4f);
        public float tempoDeAbrir = 0.35f, tempoDeFechar = 0.25f;
        [Tooltip("Giro máximo do globo para os lados e para cima/baixo (graus)")]
        public float giroMaximo = 35f, inclinacaoMaxima = 20f;
        [Tooltip("Até quantos metros o olho segue o alvo")]
        public float alcanceDoOlhar = 25f;
        [Tooltip("A mesma semente dá o mesmo ciclo (os olhos lado a lado piscam juntos)")]
        public int semente = 1;

        enum Estado { Fechado, Abrindo, Aberto, Fechando }
        Estado _estado = Estado.Fechado;
        float _ate;
        PoseDoOlho _pose;
        float _giro, _inclinacao, _giroAlvo, _inclinacaoAlvo, _proximoOlhar, _velGiro, _velInclinacao;
        System.Random _sorteioDoCiclo, _sorteioDoOlhar;

        /// <summary>0 = fechado, 1 = aberto.</summary>
        public float Abertura { get; private set; }
        /// <summary>O olho está aberto o bastante para ser acertado.</summary>
        public bool Exposto => Abertura > 0.6f;
        /// <summary>Achou os ossos e o repouso (senão, fica desligado).</summary>
        public bool Pronto { get; private set; }

        void Awake() => Preparar();

        /// <summary>Acha os ossos (os vazios, pelo nome) e pega o repouso, das bindposes. Pode ser chamado de novo.</summary>
        public void Preparar()
        {
            if (olho == null) olho = Achar("olho");
            if (palpebraSup == null) palpebraSup = Achar("palpebra_sup");
            if (palpebraInf == null) palpebraInf = Achar("palpebra_inf");
            if (olho == null || palpebraSup == null || palpebraInf == null)
            {
                Debug.LogError($"{name}: sem os ossos do olho do Brutamonte (olho, palpebra_sup, palpebra_inf); o olho fica parado.", this);
                Pronto = false;
                enabled = false;
                return;
            }
            Pronto = PoseDoOlho.Calcular(transform, PoseDoOlho.MalhaDe(GetComponentsInChildren<SkinnedMeshRenderer>(true), olho),
                                         olho, palpebraSup, palpebraInf, out _pose);
            if (Pronto && !_pose.dasBindposes)
                Debug.LogWarning($"{name}: os ossos do olho não estão nas bindposes da malha; o repouso saiu da pose atual.", this);
            // o começo: a pálpebra e o globo parados no ponto de partida (só aqui; o Reiniciar parte de onde o olho está)
            Abertura = modo == ModoDoOlho.SempreAberto ? 1f : 0f;
            _giro = _inclinacao = _velGiro = _velInclinacao = 0f;
            Reiniciar(semente);
        }

        /// <summary>
        /// O ciclo do começo (fechado), com a semente dada: os olhos com a mesma semente piscam juntos. A pálpebra e o globo
        /// partem de onde estão (um olho aberto fecha no tempo de fechar, sem pular).
        /// </summary>
        public void Reiniciar(int novaSemente)
        {
            semente = novaSemente;
            _sorteioDoCiclo = new System.Random(semente);
            _sorteioDoOlhar = new System.Random(semente * 7919 + 13);
            _estado = Estado.Fechado;
            _ate = Time.time + Sorteio(_sorteioDoCiclo, 1f, Mathf.Max(1f, tempoFechado.x));
            _giroAlvo = _inclinacaoAlvo = 0f;
            _proximoOlhar = 0f;
        }

        Transform Achar(string nome)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }

        void LateUpdate()
        {
            if (!Pronto) return;
            float agora = Time.time, dt = Time.deltaTime;
            switch (modo)
            {
                case ModoDoOlho.SempreAberto:
                    Abertura = Mathf.MoveTowards(Abertura, 1f, dt / tempoDeAbrir);
                    _estado = Estado.Aberto;
                    break;
                case ModoDoOlho.SempreFechado:
                    Abertura = Mathf.MoveTowards(Abertura, 0f, dt / tempoDeFechar);
                    _estado = Estado.Fechado;
                    break;
                default:
                    Ciclo(agora, dt);
                    break;
            }
            Olhar(agora, dt);
            _pose.Aplicar(olho, palpebraSup, palpebraInf, anguloDeFechar * (1f - Suave(Abertura)), _giro, _inclinacao);
            if (pontoFraco != null) pontoFraco.enabled = Exposto;
        }

        void Ciclo(float agora, float dt)
        {
            switch (_estado)
            {
                case Estado.Fechado:
                    Abertura = Mathf.MoveTowards(Abertura, 0f, dt / tempoDeFechar);
                    if (agora >= _ate) _estado = Estado.Abrindo;
                    break;
                case Estado.Abrindo:
                    Abertura = Mathf.MoveTowards(Abertura, 1f, dt / tempoDeAbrir);
                    if (Abertura >= 1f) { _estado = Estado.Aberto; _ate = agora + Sorteio(_sorteioDoCiclo, tempoAberto.x, tempoAberto.y); }
                    break;
                case Estado.Aberto:
                    Abertura = 1f;
                    if (agora >= _ate) _estado = Estado.Fechando;
                    break;
                case Estado.Fechando:
                    Abertura = Mathf.MoveTowards(Abertura, 0f, dt / tempoDeFechar);
                    if (Abertura <= 0f) { _estado = Estado.Fechado; _ate = agora + Sorteio(_sorteioDoCiclo, tempoFechado.x, tempoFechado.y); }
                    break;
            }
        }

        // aberto: segue o alvo se ele está do lado das costas (no campo do olho); senão, olhadas curtas em volta
        void Olhar(float agora, float dt)
        {
            bool seguiu = false;
            var quem = !seguirAlvo ? null : alvo != null ? alvo : Camera.main != null ? Camera.main.transform : null;
            if (quem != null && Abertura > 0.2f)
            {
                Vector3 d = quem.position - olho.position;
                if (d.sqrMagnitude < alcanceDoOlhar * alcanceDoOlhar)
                {
                    // no espaço do pai do olho (o chest, como está agora), contra os eixos do repouso
                    Vector3 local = Quaternion.Inverse(olho.parent.rotation) * d;
                    float frente = Vector3.Dot(local, _pose.frente);
                    if (frente > 0.05f)
                    {
                        _giroAlvo = Mathf.Atan2(Vector3.Dot(local, _pose.lado), frente) * Mathf.Rad2Deg;
                        _inclinacaoAlvo = Mathf.Atan2(Vector3.Dot(local, _pose.cima), frente) * Mathf.Rad2Deg;
                        seguiu = true;
                    }
                }
            }
            if (!seguiu && agora >= _proximoOlhar)
            {
                _giroAlvo = Sorteio(_sorteioDoOlhar, -giroMaximo, giroMaximo) * 0.8f;
                _inclinacaoAlvo = Sorteio(_sorteioDoOlhar, -inclinacaoMaxima, inclinacaoMaxima) * 0.7f;
                _proximoOlhar = agora + Sorteio(_sorteioDoOlhar, 0.4f, 1.3f);
            }
            _giroAlvo = Mathf.Clamp(_giroAlvo, -giroMaximo, giroMaximo);
            _inclinacaoAlvo = Mathf.Clamp(_inclinacaoAlvo, -inclinacaoMaxima, inclinacaoMaxima);
            float tempo = seguiu ? 0.12f : 0.05f;   // as olhadas em volta são rápidas (sacadas)
            _giro = Mathf.SmoothDamp(_giro, _giroAlvo, ref _velGiro, tempo, Mathf.Infinity, dt);
            _inclinacao = Mathf.SmoothDamp(_inclinacao, _inclinacaoAlvo, ref _velInclinacao, tempo, Mathf.Infinity, dt);
        }

        static float Sorteio(System.Random r, float de, float ate) => de + (float)r.NextDouble() * (ate - de);
        static float Suave(float t) => t * t * (3f - 2f * t);
    }
}
