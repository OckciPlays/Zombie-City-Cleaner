using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A pose na proteção (18/11/2026, a pedido: "eles ficam muito expostos, o que fica fácil para atirar"): agachado atrás
    /// da proteção baixa (o "Agachado" do corpo, o mesmo do jogador, com os braços segurando a arma) e o tronco inclinado
    /// para espiar pela quina (os ossos da coluna e do peito girados depois da animação: a cabeça, o ombro e a arma saem da
    /// proteção, e o resto fica atrás). As partes que levam tiro ficam presas nos ossos, então vão junto. O anfitrião decide;
    /// as duas NetworkVariables levam a pose para todos.
    /// </summary>
    public partial class Sobrevivente
    {
        /// <summary>A inclinação do tronco, de -100 (para a esquerda) a 100 (para a direita).</summary>
        readonly NetworkVariable<sbyte> _inclina = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O quanto ele está agachado, de 0 (em pé) a 100.</summary>
        readonly NetworkVariable<byte> _agacha = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>O ângulo de cada lado na inclinação toda (graus: a coluna e o peito juntos).</summary>
        const float InclinacaoMaxima = 38f;
        /// <summary>
        /// Da cintura até o olho (m), na conta da inclinação: 0,48 (e não os 0,6 do corpo) porque a coluna e o peito giram em
        /// dois pedaços, e a cabeça sai uns 30 cm para o lado na inclinação toda.
        /// </summary>
        const float CinturaAoOlho = 0.48f;

        // no anfitrião, a cada quadro: o que a cabeça quer (sai na NetworkVariable no fim do Pensar)
        int _querInclinar, _querAgachar;
        // em todas as máquinas: a pose suave
        float _inclinaSuave, _agachaSuave;
        Transform _coluna, _peito;
        static readonly int AgachadoNoCorpoId = Animator.StringToHash("Agachado");
        bool? _temAgachadoNoCorpo;
        RuntimeAnimatorController _controladorVisto;

        /// <summary>No anfitrião, no fim do Pensar: a pose que a cabeça quis neste quadro.</summary>
        void AplicarAPose()
        {
            sbyte i = (sbyte)Mathf.Clamp(_querInclinar, -100, 100);
            byte a = (byte)Mathf.Clamp(_querAgachar, 0, 100);
            if (_inclina.Value != i) _inclina.Value = i;
            if (_agacha.Value != a) _agacha.Value = a;
        }

        /// <summary>Em todas as máquinas (do Update): o agachado no Animator, suave.</summary>
        void PoseNoAnimador()
        {
            float alvo = _caido ? 0f : _agacha.Value / 100f;
            _agachaSuave = Mathf.MoveTowards(_agachaSuave, alvo, Time.deltaTime / 0.25f);
            var an = corpo.animador;
            if (_controladorVisto != an.runtimeAnimatorController)
            {
                _controladorVisto = an.runtimeAnimatorController;
                _temAgachadoNoCorpo = null;
            }
            if (_temAgachadoNoCorpo == null)
            {
                _temAgachadoNoCorpo = false;
                foreach (var p in an.parameters)
                    if (p.nameHash == AgachadoNoCorpoId && p.type == AnimatorControllerParameterType.Float) _temAgachadoNoCorpo = true;
            }
            if (_temAgachadoNoCorpo == true) an.SetFloat(AgachadoNoCorpoId, _agachaSuave);
        }

        /// <summary>Em todas as máquinas (do LateUpdate, depois da animação): a coluna e o peito inclinados.</summary>
        void InclinarOCorpo()
        {
            float alvo = _caido ? 0f : _inclina.Value / 100f;
            _inclinaSuave = Mathf.MoveTowards(_inclinaSuave, alvo, Time.deltaTime / 0.22f);
            if (Mathf.Abs(_inclinaSuave) < 0.001f) return;
            if (_coluna == null || _peito == null)
                foreach (var t in corpo.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "spine") _coluna = t;
                    else if (t.name == "chest") _peito = t;
                }
            if (_coluna == null || _peito == null) return;
            // (para a direita, o giro negativo em volta da frente)
            float graus = -_inclinaSuave * InclinacaoMaxima;
            var frente = transform.forward;
            _coluna.rotation = Quaternion.AngleAxis(graus * 0.45f, frente) * _coluna.rotation;
            _peito.rotation = Quaternion.AngleAxis(graus * 0.55f, frente) * _peito.rotation;
        }

        /// <summary>
        /// O olho na pose (para ver e atirar): a altura do agachado e o deslocamento da inclinação para o lado. 'inclina' de
        /// -1 a 1 e 'agacha' de 0 a 1 (sem os valores: a pose de agora).
        /// </summary>
        Vector3 OlhoNaPose(float inclina, float agacha)
        {
            float rad = Mathf.Abs(inclina) * InclinacaoMaxima * Mathf.Deg2Rad;
            float alto = Mathf.Lerp(1.6f, 1.15f, agacha) - CinturaAoOlho * (1f - Mathf.Cos(rad));
            return transform.position + Vector3.up * alto + transform.right * (Mathf.Sign(inclina) * CinturaAoOlho * Mathf.Sin(rad));
        }

        Transform _ossoDaCabeca;

        /// <summary>
        /// O olho agora: o osso da cabeça (depois da animação e da inclinação; o Animator dele anima sempre), um pouco acima e à
        /// frente. Assim ele só vê (e atira) quando a cabeça saiu mesmo da proteção, e o jogador pode acertá-la. Sem o osso,
        /// a conta da pose.
        /// </summary>
        Vector3 OlhoAgora
        {
            get
            {
                if (_ossoDaCabeca == null && corpo != null)
                    foreach (var t in corpo.GetComponentsInChildren<Transform>(true))
                        if (t.name == "head") { _ossoDaCabeca = t; break; }
                if (_ossoDaCabeca != null && !_caido) return _ossoDaCabeca.position + Vector3.up * 0.07f + transform.forward * 0.1f;
                return OlhoNaPose(_inclina.Value / 100f, _agacha.Value / 100f);
            }
        }

        // para o F1 e os testes: a pose na rede (de -100 a 100 e de 0 a 100) e a de agora, suave
        public int Inclinacao => _inclina.Value;
        public int Agachamento => _agacha.Value;
        public float InclinacaoSuave => _inclinaSuave;
        public float AgachamentoSuave => _agachaSuave;
    }
}
