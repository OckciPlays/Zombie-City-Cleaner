using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A visão de terceira pessoa do dono (28/10/2026, a pedido: "para eu poder alternar entre as duas"): o V troca entre a
    /// primeira pessoa e a câmera atrás do ombro direito. É a mesma escolha da câmera de trás do carro: quem anda em
    /// terceira pessoa entra no carro com a câmera de trás, e o V no carro também troca a pé.
    /// <para>
    /// A cabeça continua sendo o olho do jogo: o tiro, o soco, o E, o largar e a lanterna saem dela (<see cref="Mira"/>, em
    /// <see cref="JogadorRede.Olho"/>), na direção do que está no meio da tela (o primeiro ponto que o raio da câmera pega
    /// depois da cabeça), e a câmera só olha de trás. O corpo do dono aparece inteiro, com o que está na mão, e os braços de
    /// primeira pessoa somem; colada numa parede, perto demais da cabeça, a câmera esconde o corpo de novo.
    /// </para>
    /// Fica na primeira pessoa no carro (que tem a câmera dele), no capô, mirando com a luneta e gravando um vídeo.
    /// </summary>
    [DefaultExecutionOrder(100)]   // depois do movimento (a cabeça), do carro e da arma, antes do quadro ser desenhado
    public class CameraTerceiraPessoa : MonoBehaviour
    {
        public JogadorRede rede;
        public MovimentoPrimeiraPessoa movimento;
        public ArmaJogador arma;
        public TelaDoCapo capo;

        [Tooltip("A câmera em relação à cabeça (x para a direita, y para cima, z para a frente).")]
        public Vector3 ombro = new Vector3(0.5f, 0.15f, -2.3f);
        [Tooltip("A mesma, mirando: mais perto do ombro.")]
        public Vector3 ombroMirando = new Vector3(0.55f, 0.08f, -1.2f);
        [Tooltip("O raio da esfera que procura as paredes entre a cabeça e a câmera.")]
        public float raio = 0.2f;
        [Tooltip("Mais perto que isso da cabeça (uma parede logo atrás), o corpo some, para não tapar a tela.")]
        public float pertoDemais = 0.55f;
        public float alcanceDaMira = 200f;
        [Tooltip("Até esta distância da cabeça (m), o que fica no caminho do tiro puxa a mira para ele (a parede, a proteção);"
                 + " mais longe, não (as árvores no fundo, perto da linha, faziam a mira pular).")]
        public float bloqueioPerto = 6f;

        /// <summary>A escolha do jogador (o V): a terceira pessoa a pé e a câmera de trás no carro.</summary>
        public static bool Ligada { get; set; }

        /// <summary>Encostado numa proteção sem mirar (ProtecaoDoJogador, 18/11/2026): atrás do ombro, mesmo sem o V.</summary>
        public static bool NaProtecao { get; set; }

        /// <summary>No meio da animação do craft (AnimacaoDoCraft, 25/11/2026): atrás do ombro, mesmo sem o V.</summary>
        public static bool NoCraft { get; set; }

        /// <summary>A câmera está atrás do jogador agora (a pé, fora do capô e da luneta).</summary>
        public bool Ativa { get; private set; }

        /// <summary>O olho do jogo na terceira pessoa: na cabeça, olhando para o que está no meio da tela.</summary>
        public Transform Mira { get; private set; }

        /// <summary>
        /// Na terceira pessoa, o que fica entre a cabeça (de onde sai o tiro) e o que está no meio da tela (18/11/2026, a
        /// pedido: "quando isso ocorrer, deveria trocar a mira e a mira começar a apontar no objeto que está impedindo o
        /// trajeto"): o ponto onde o tiro vai bater; a mira é desenhada nele (ArmaJogador.OnGUI). Null: o caminho está livre.
        /// </summary>
        public static Vector3? MiraBloqueada { get; private set; }

        // ao trocar de câmera, o ponto que a mira marcava: a nova câmera é virada para marcar o mesmo (18/11/2026, a pedido:
        // "eu mirando no mesmo local, sem alterar em nada a mira, o tiro sai diferente")
        Vector3? _realinhar;

        // o que a câmera atravessa: o próprio jogador e os outros, os braços, o retrato, os zumbis e os pedaços deles
        static readonly int MascaraDaCamera = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaDaCabeca)
                                                | (1 << JogadorRede.CamadaRetrato) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));
        // o que a mira pega: o mesmo que o tiro (as hitboxes dos zumbis também), sem os jogadores
        static readonly int MascaraDaMira = ~((1 << 2) | (1 << 16) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
                                              | (1 << JogadorRede.CamadaRetrato) | (1 << CorpoDeZumbi.Camada));
        static readonly RaycastHit[] _acertos = new RaycastHit[16];

        Vector3 _ombro;
        float _distancia;
        // o que foi aplicado (a câmera, o corpo e os braços); o carro mexe em tudo, e na volta aplica de novo
        bool _conhecida, _aplicada;

        void Awake()
        {
            if (rede == null) rede = GetComponent<JogadorRede>();
            if (movimento == null) movimento = GetComponent<MovimentoPrimeiraPessoa>();
            if (arma == null) arma = GetComponent<ArmaJogador>();
            if (capo == null) capo = GetComponent<TelaDoCapo>();
            Mira = new GameObject("Mira").transform;
            Mira.SetParent(movimento.cabeca, false);
            _ombro = ombro;
            // (18/11/2026) nos lugares apertados (dentro das casas), a câmera chega mais perto antes de virar a primeira pessoa:
            // com 0,55 m ela parecia não trocar
            pertoDemais = Mathf.Min(pertoDemais, 0.35f);
        }

        void Update()
        {
            if (!rede.IsSpawned || !rede.IsOwner || !MenuOnline.Controla) return;
            // no carro, o V é do carro (a mesma escolha)
            if (rede.Passageiro != null && rede.Passageiro.NoCarro) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.vKey.wasPressedThisFrame)
            {
                _realinhar = PontoMirado();
                Ligada = !Ligada;
            }
        }

        void LateUpdate()
        {
            if (!rede.IsSpawned || !rede.IsOwner) return;
            if (rede.Passageiro != null && rede.Passageiro.NoCarro)
            {
                // o carro cuida da câmera, do corpo e dos braços; na saída, ele volta tudo à primeira pessoa
                Ativa = false;
                _conhecida = false;
                Mira.localRotation = Quaternion.identity;
                return;
            }
            bool naLuneta = arma.ComArma && rede.Mirando.Value && arma.Dados.fovLuneta > 0f;
            bool querer = (Ligada || NaProtecao || NoCraft) && !(capo != null && capo.Aberta) && !naLuneta && !ModoVideo.Ligado;
            if (!_conhecida || querer != _aplicada) Aplicar(querer);
            if (!querer)
            {
                MiraBloqueada = null;
                if (_realinhar.HasValue) Realinhar(false);
                return;
            }
            if (_realinhar.HasValue) Realinhar(true);

            float dt = Time.deltaTime;
            Posicionar(dt);
            rede.CorpoDoDono(_distancia >= pertoDemais);
            if (rede.cameraBracos.enabled) rede.cameraBracos.enabled = false;   // a arma (trocando, saindo da luneta) liga de novo
            Apontar();
        }

        void Aplicar(bool terceira)
        {
            _conhecida = true;
            _aplicada = terceira;
            Ativa = terceira;
            var cam = rede.cameraMundo.transform;
            Mira.localRotation = Quaternion.identity;
            if (terceira)
            {
                // a câmera sai da cabeça para trás, aos poucos
                _distancia = 0f;
                _ombro = rede.Mirando.Value ? ombroMirando : ombro;
                rede.cameraBracos.enabled = false;
                return;
            }
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
            rede.CorpoDoDono(false);
            rede.cameraBracos.enabled = !arma.NaLuneta;
        }

        // a câmera atrás do ombro: primeiro para o lado (sem passar de uma parede ao lado), depois para trás, até a parede;
        // aproxima na hora e afasta devagar
        void Posicionar(float dt)
        {
            var cabeca = movimento.cabeca;
            Quaternion rot = cabeca.rotation;
            Vector3 olho = cabeca.position;
            _ombro = Vector3.Lerp(_ombro, rede.Mirando.Value ? ombroMirando : ombro, 1f - Mathf.Exp(-10f * dt));

            Vector3 noOmbro = NoOmbro(olho, rot);

            Vector3 tras = rot * Vector3.back;
            float longe = Mathf.Max(0f, -_ombro.z);
            float d = longe;
            if (longe > 0f && Physics.SphereCast(noOmbro, raio, tras, out var atras, longe, MascaraDaCamera, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0f, atras.distance - 0.02f);
            _distancia = d < _distancia ? d : Mathf.Lerp(_distancia, d, 1f - Mathf.Exp(-4f * dt));
            rede.cameraMundo.transform.SetPositionAndRotation(noOmbro + tras * _distancia, rot);
        }

        // o ponto ao lado da cabeça de onde a câmera vai para trás: o ombro, mas sem passar de uma parede ao lado (nos lugares
        // apertados, ele chega mais perto da cabeça)
        Vector3 NoOmbro(Vector3 olho, Quaternion rot)
        {
            Vector3 lado = rot * new Vector3(_ombro.x, _ombro.y, 0f);
            float l = lado.magnitude;
            if (l > 0.001f && Physics.SphereCast(olho, raio, lado / l, out var aoLado, l, MascaraDaCamera, QueryTriggerInteraction.Ignore))
                return olho + lado / l * Mathf.Max(0f, aoLado.distance - 0.02f);
            return olho + lado;
        }

        // a mira: da cabeça para o primeiro ponto que o raio do meio da tela pega depois dela (o que está entre a câmera e
        // a cabeça não conta); sem nada, para longe na direção da câmera
        void Apontar()
        {
            var cam = rede.cameraMundo.transform;
            Vector3 olho = movimento.cabeca.position;
            Vector3 frente = cam.forward;
            float comeco = Mathf.Max(0f, Vector3.Dot(olho - cam.position, frente));
            Vector3 alvo = cam.position + frente * alcanceDaMira;
            int n = Physics.RaycastNonAlloc(cam.position, frente, _acertos, alcanceDaMira, MascaraDaMira, QueryTriggerInteraction.Ignore);
            float perto = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _acertos[i];
                if (h.distance <= comeco || h.distance >= perto) continue;
                perto = h.distance;
                alvo = h.point;
            }
            Vector3 para = alvo - olho;
            // um alvo colado na cabeça (ou atrás dela) não dá direção: a da câmera
            Mira.rotation = Vector3.Dot(para, frente) > 0.5f ? Quaternion.LookRotation(para, cam.up) : cam.rotation;
            // o que fica no caminho da cabeça até o alvo (o tiro bate nele): a mira vai para lá, só quando está perto (23/11/2026:
            // só um auxílio; de longe, as árvores perto da linha faziam a mira pular de uma para outra)
            MiraBloqueada = null;
            float ate = para.magnitude;
            if (ate > 0.3f && Physics.Raycast(olho, para / ate, out var bloqueio, Mathf.Min(ate - 0.15f, bloqueioPerto), MascaraDaMira, QueryTriggerInteraction.Ignore))
                MiraBloqueada = bloqueio.point;
        }

        // o ponto que a mira marca agora (o primeiro que o tiro pega): da Mira na terceira pessoa, da cabeça na primeira
        Vector3 PontoMirado()
        {
            var cabeca = movimento.cabeca;
            Vector3 olho = cabeca.position;
            Vector3 dir = Ativa ? Mira.forward : cabeca.forward;
            return Physics.Raycast(olho, dir, out var h, alcanceDaMira, MascaraDaMira, QueryTriggerInteraction.Ignore) ? h.point : olho + dir * 60f;
        }

        // vira o olhar para a câmera de agora marcar o mesmo ponto de antes da troca: na terceira pessoa, a linha do meio da
        // tela sai do ombro (ao lado da cabeça); na primeira, da cabeça
        void Realinhar(bool terceira)
        {
            Vector3 ponto = _realinhar.Value;
            _realinhar = null;
            var cabeca = movimento.cabeca;
            Vector3 olho = cabeca.position;
            Quaternion rot = cabeca.rotation;
            if (terceira) _ombro = rede.Mirando.Value ? ombroMirando : ombro;
            for (int i = 0; i < 3; i++)
            {
                // (o mesmo ombro da câmera: com uma parede ao lado, mais perto da cabeça)
                Vector3 de = terceira ? NoOmbro(olho, rot) : olho;
                Vector3 d = ponto - de;
                if (d.sqrMagnitude < 0.25f) return;
                rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            Vector3 f = rot * Vector3.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;
            movimento.Olhar(yaw, pitch);
        }
    }
}
