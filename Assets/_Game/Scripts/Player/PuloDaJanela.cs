using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Pular a janela (28/10/2026, a pedido: "ao chegar perto da janela e apertar espaço ele vai pular por ela, se ela não
    /// estiver bloqueada"). No chão, de frente para uma janela (até 50° de lado) e perto dela, o Espaço pula por ela em vez
    /// do pulo de sempre (o <see cref="MovimentoPrimeiraPessoa"/> pergunta a <see cref="Tentar"/>).
    /// <para>
    /// A janela é achada pela física, em qualquer construção (<see cref="AcharJanela"/>): a parede na altura do joelho, a
    /// abertura na altura do peito, o peitoril (de 0,55 a 1,25 m do chão), o alto da abertura (uma janela tem; um muro
    /// não, e por cima dele não se pula), a passagem livre (uma caixa do tamanho do corpo encolhido atravessa a abertura:
    /// as tábuas, o compensado e o vidro inteiro têm colisão e bloqueiam; o vidro quebrado, não) e o chão do outro lado
    /// (até 1 m mais baixo ou 0,45 m mais alto, com lugar para a cápsula).
    /// </para>
    /// <para>
    /// O pulo é a animação Pular_Janela do corpo (do Blender, bloco_janela.py; o estado do controlador do corpo, pelo
    /// gatilho "Janela"), feita no lugar: a raiz anda para a frente pela curva do SmoothStep, esticada para a mão chegar no
    /// peitoril, o corpo passar por cima dele e os pés pararem do outro lado onde a janela de verdade está (a da animação
    /// fica a 0,40 m), e sobe e desce com o peitoril e com o chão do outro lado. A câmera de primeira pessoa vai com a
    /// cabeça do corpo; os braços de primeira pessoa e o que está na mão somem. Os outros jogadores recebem o pulo pela
    /// rede (<see cref="JanelaRpc"/>) e tocam a mesma animação; a posição vem pelo NetworkTransform.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(50)]   // depois do Animator e do JogadorRede, antes da câmera de terceira pessoa
    public class PuloDaJanela : NetworkBehaviour
    {
        // a janela da animação (o JANELA do bloco_janela.py): a distância da parede no começo, a parede, o peitoril, o
        // quanto anda depois da parede e os quadros (24 por segundo). Ao mudar lá, mude aqui
        const float Distancia = 0.40f, Parede = 0.20f, Peitoril = 0.90f, Depois = 0.55f;
        const float Frente = Distancia + Parede + Depois;
        const float Quadros = 30f;
        /// <summary>O tempo do pulo, em segundos.</summary>
        public const float Duracao = Quadros / 24f;
        // quanto a raiz da animação já andou quando a mão chega no peitoril (no quadro 6)
        static readonly float FrenteNasMaos = Frente * Suave(6f / Quadros);

        const float AnguloMaximo = 50f;       // de lado para a janela, no máximo
        const float DistanciaMaxima = 0.85f;  // da parede, no máximo (o peito encostado nela fica a 0,34)
        const float AlturaMinima = 1.10f;     // da abertura, do peitoril para cima (a cabeça passa a 1,03 dele)

        // o cenário: sem os jogadores, os braços, o retrato, os zumbis e os pedaços deles; a janela, também sem os carros
        // (a do carro não é janela), que só bloqueiam a passagem e o pouso
        static readonly int Mascara = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)
                                        | (1 << JogadorRede.CamadaRetrato) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));
        static readonly int MascaraDaJanela = Mascara & ~(1 << Carro.CamadaVeiculo);
        static readonly int JanelaId = Animator.StringToHash("Janela");

        /// <summary>Uma janela achada: a direção (para dentro dela), a distância e a grossura da parede, e as alturas do
        /// peitoril e do chão do outro lado (desde os pés).</summary>
        public struct Janela
        {
            public Vector3 dir;
            public float distancia, parede, peitoril, desnivel;
        }

        JogadorRede _rede;
        MovimentoPrimeiraPessoa _movimento;
        ArmaJogador _arma;
        CharacterController _cc;

        bool _pulando;
        float _t;
        Janela _j;
        Vector3 _inicio, _cabecaLocal, _olhoNaCabeca;
        Quaternion _giro0, _giro1;

        /// <summary>Pulando a janela agora (nesta máquina: o dono ou a cópia de outro jogador).</summary>
        public bool Pulando => _pulando;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _movimento = GetComponent<MovimentoPrimeiraPessoa>();
            _arma = GetComponent<ArmaJogador>();
            _cc = GetComponent<CharacterController>();
        }

        static float Suave(float t) => Mathf.SmoothStep(0f, 1f, t);

        /// <summary>O dono apertou o pulo, no chão: pula a janela da frente, se tiver uma livre (e devolve true).</summary>
        public bool Tentar()
        {
            if (!IsSpawned || !IsOwner || _pulando) return false;
            if (_rede.Passageiro != null && _rede.Passageiro.NoCarro) return false;
            if (!AcharJanela(out var j)) return false;
            Comecar(j);
            JanelaRpc();
            return true;
        }

        /// <summary>A janela na frente do jogador, livre para pular (veja a classe).</summary>
        public bool AcharJanela(out Janela j)
        {
            j = default;
            const QueryTriggerInteraction semGatilhos = QueryTriggerInteraction.Ignore;
            Vector3 p = transform.position;
            Vector3 frente = transform.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 1e-4f) return false;
            frente.Normalize();
            // a parede na altura do joelho (de pé, e o jogador de frente para ela); depois, de novo, reto para ela
            if (!Physics.Raycast(p + Vector3.up * 0.45f, frente, out var parede, DistanciaMaxima + 0.1f, MascaraDaJanela, semGatilhos)) return false;
            Vector3 n = parede.normal;
            n.y = 0f;
            if (n.sqrMagnitude < 0.5f) return false;
            Vector3 dir = -n.normalized;
            if (Vector3.Angle(dir, frente) > AnguloMaximo) return false;
            if (!Physics.Raycast(p + Vector3.up * 0.45f, dir, out parede, DistanciaMaxima + 0.1f, MascaraDaJanela, semGatilhos)) return false;
            float d0 = parede.distance;
            if (d0 < 0.25f || d0 > DistanciaMaxima) return false;
            // a abertura na altura do peito
            if (Physics.Raycast(p + Vector3.up * 1.35f, dir, d0 + 0.35f, Mascara, semGatilhos)) return false;
            // o peitoril, de cima, logo depois da face da parede
            if (!Physics.Raycast(p + dir * (d0 + 0.05f) + Vector3.up * 1.7f, Vector3.down, out var peitoril, 1.5f, MascaraDaJanela, semGatilhos)) return false;
            float altura = peitoril.point.y - p.y;
            if (peitoril.normal.y < 0.7f || altura < 0.55f || altura > 1.25f) return false;
            // o alto da abertura: uma janela tem, e alto o bastante para a cabeça abaixada
            if (!Physics.Raycast(peitoril.point + Vector3.up * 0.05f, Vector3.up, out var alto, 2f, MascaraDaJanela, semGatilhos)) return false;
            if (alto.distance + 0.05f < AlturaMinima) return false;
            // a grossura da parede, pela face de lá (logo abaixo do peitoril)
            float grossura = Parede;
            if (Physics.Raycast(p + dir * (d0 + 0.7f) + Vector3.up * (altura - 0.15f), -dir, out var la, 0.7f, MascaraDaJanela, semGatilhos))
                grossura = Mathf.Clamp(0.7f - la.distance, 0.05f, 0.45f);
            // a passagem: o corpo encolhido (48 × 64 cm) atravessa a abertura, do jogador até depois da parede (saindo
            // dele: começando encostada nas tábuas pregadas por dentro, a caixa não as via)
            var meio = new Vector3(0.24f, 0.32f, 0.05f);
            Vector3 de = p + Vector3.up * (altura + 0.08f + meio.y);
            if (Physics.BoxCast(de, meio, dir, Quaternion.LookRotation(dir), d0 + grossura + 0.6f, Mascara, semGatilhos)) return false;
            // o chão do outro lado, e lugar para a cápsula
            Vector3 pouso = p + dir * (d0 + grossura + Depois);
            if (!Physics.Raycast(pouso + Vector3.up * (altura + 0.5f), Vector3.down, out var chao, altura + 1.6f, Mascara, semGatilhos)) return false;
            float desnivel = chao.point.y - p.y;
            if (chao.normal.y < 0.7f || desnivel < -1f || desnivel > 0.45f) return false;
            Vector3 pe = new Vector3(pouso.x, chao.point.y, pouso.z);
            if (Physics.CheckCapsule(pe + Vector3.up * 0.4f, pe + Vector3.up * 1.5f, 0.28f, Mascara, semGatilhos)) return false;
            j = new Janela { dir = dir, distancia = d0, parede = grossura, peitoril = altura, desnivel = desnivel };
            return true;
        }

        void Comecar(Janela j)
        {
            _j = j;
            _pulando = true;
            _t = 0f;
            _inicio = transform.position;
            _giro0 = transform.rotation;
            _giro1 = Quaternion.LookRotation(j.dir);
            _cc.enabled = false;
            _movimento.enabled = false;
            _arma.enabled = false;
            _rede.bracos.SetActive(false);
            if (_rede.Mirando.Value) _rede.Mirando.Value = false;
            var cabeca = _movimento.cabeca;
            _cabecaLocal = cabeca.localPosition;
            var osso = _rede.ossoCabeca;
            _olhoNaCabeca = osso != null ? Quaternion.Inverse(osso.rotation) * (cabeca.position - osso.position) : Vector3.zero;
            Visual(true);
        }

        /// <summary>Os outros tocam o pulo (a posição vem pelo NetworkTransform).</summary>
        [Rpc(SendTo.NotMe)]
        void JanelaRpc()
        {
            _pulando = true;
            _t = 0f;
            Visual(true);
        }

        // em todas as máquinas: a animação, o que está na mão (some) e o corpo sem acompanhar o olhar
        void Visual(bool pulando)
        {
            var anim = _rede.animadorCorpo;
            if (pulando) anim.SetTrigger(JanelaId);
            else
            {
                anim.ResetTrigger(JanelaId);
                if (anim.layerCount > 1) anim.SetLayerWeight(1, 1f);
            }
            _rede.EsconderNaMao(pulando);
            _rede.SemOlhar = pulando;
        }

        void Update()
        {
            if (!_pulando) return;
            _t += Time.deltaTime;
            // a camada dos braços (as armas) sai no começo e volta no fim
            var anim = _rede.animadorCorpo;
            if (anim.layerCount > 1) anim.SetLayerWeight(1, Mathf.Clamp01(Mathf.Max(1f - _t / 0.15f, 1f - (Duracao - _t) / 0.25f)));
            if (IsOwner) MoverRaiz(Mathf.Min(_t, Duracao));
            if (_t >= Duracao) Terminar();
        }

        // a raiz pelo caminho da animação, esticado para a janela de verdade (veja a classe)
        void MoverRaiz(float t)
        {
            float k = t / Duracao, f = k * Quadros;
            float n = Frente * Suave(k);
            float meioNominal = Distancia + Parede / 2f, meio = _j.distancia + _j.parede / 2f;
            float nasMaos = FrenteNasMaos + (_j.distancia - Distancia), fim = _j.distancia + _j.parede + Depois;
            float a = n <= FrenteNasMaos ? n * nasMaos / FrenteNasMaos
                : n <= meioNominal ? Mathf.Lerp(nasMaos, meio, (n - FrenteNasMaos) / (meioNominal - FrenteNasMaos))
                : Mathf.Lerp(meio, fim, (n - meioNominal) / (Frente - meioNominal));
            // em cima do peitoril, a diferença dele para o da animação; do outro lado, o chão de lá
            float noPeitoril = Mathf.Min(Suave((f - 2f) / 4f), 1f - Suave((f - 15f) / 6f));
            float sobe = Mathf.Clamp(_j.peitoril - Peitoril, -0.3f, 0.35f) * noPeitoril + _j.desnivel * Suave((f - 13f) / 8f);
            transform.SetPositionAndRotation(_inicio + _j.dir * a + Vector3.up * sobe, Quaternion.Slerp(_giro0, _giro1, Suave(f / 4f)));
        }

        // a câmera de primeira pessoa com a cabeça do corpo (entrando e saindo aos poucos)
        void LateUpdate()
        {
            if (!_pulando || !IsOwner) return;
            var cabeca = _movimento.cabeca;
            var osso = _rede.ossoCabeca;
            if (osso == null) return;
            float w = Mathf.Clamp01(Mathf.Min(_t / 0.12f, (Duracao - _t) / 0.15f));
            Vector3 olho = osso.position + osso.rotation * _olhoNaCabeca;
            cabeca.position = Vector3.Lerp(transform.TransformPoint(_cabecaLocal), olho, w);
        }

        void Terminar()
        {
            _pulando = false;
            Visual(false);
            if (!IsOwner) return;
            _movimento.cabeca.localPosition = _cabecaLocal;
            Vector3 fim = _inicio + _j.dir * (_j.distancia + _j.parede + Depois) + Vector3.up * _j.desnivel;
            _movimento.enabled = true;
            _movimento.Teleportar(fim, _giro1.eulerAngles.y);
            _arma.enabled = true;
            _rede.bracos.SetActive(true);
        }

        public override void OnNetworkDespawn()
        {
            if (_pulando && IsOwner)
            {
                _movimento.cabeca.localPosition = _cabecaLocal;
                _cc.enabled = true;
            }
            _pulando = false;
        }
    }
}
