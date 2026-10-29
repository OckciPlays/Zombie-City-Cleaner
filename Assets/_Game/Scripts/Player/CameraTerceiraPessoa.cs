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

        /// <summary>A escolha do jogador (o V): a terceira pessoa a pé e a câmera de trás no carro.</summary>
        public static bool Ligada { get; set; }

        /// <summary>A câmera está atrás do jogador agora (a pé, fora do capô e da luneta).</summary>
        public bool Ativa { get; private set; }

        /// <summary>O olho do jogo na terceira pessoa: na cabeça, olhando para o que está no meio da tela.</summary>
        public Transform Mira { get; private set; }

        // o que a câmera atravessa: o próprio jogador e os outros, os braços, o retrato, os zumbis e os pedaços deles
        static readonly int MascaraDaCamera = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
                                                | (1 << JogadorRede.CamadaRetrato) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));
        // o que a mira pega: o mesmo que o tiro (as hitboxes dos zumbis também), sem os jogadores
        static readonly int MascaraDaMira = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
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
        }

        void Update()
        {
            if (!rede.IsSpawned || !rede.IsOwner || !MenuOnline.Controla) return;
            // no carro, o V é do carro (a mesma escolha)
            if (rede.Passageiro != null && rede.Passageiro.NoCarro) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.vKey.wasPressedThisFrame) Ligada = !Ligada;
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
            bool querer = Ligada && !(capo != null && capo.Aberta) && !naLuneta && !ModoVideo.Ligado;
            if (!_conhecida || querer != _aplicada) Aplicar(querer);
            if (!querer) return;

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

            Vector3 lado = rot * new Vector3(_ombro.x, _ombro.y, 0f);
            float l = lado.magnitude;
            Vector3 noOmbro = olho + lado;
            if (l > 0.001f && Physics.SphereCast(olho, raio, lado / l, out var aoLado, l, MascaraDaCamera, QueryTriggerInteraction.Ignore))
                noOmbro = olho + lado / l * Mathf.Max(0f, aoLado.distance - 0.02f);

            Vector3 tras = rot * Vector3.back;
            float longe = Mathf.Max(0f, -_ombro.z);
            float d = longe;
            if (longe > 0f && Physics.SphereCast(noOmbro, raio, tras, out var atras, longe, MascaraDaCamera, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0f, atras.distance - 0.02f);
            _distancia = d < _distancia ? d : Mathf.Lerp(_distancia, d, 1f - Mathf.Exp(-4f * dt));
            rede.cameraMundo.transform.SetPositionAndRotation(noOmbro + tras * _distancia, rot);
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
        }
    }
}
