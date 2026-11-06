using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A mão esquerda na arma, na primeira pessoa (03/11/2026, a pedido: "ao correr segurando uma arma na primeira pessoa,
    /// como o rifle, a mão de apoio não acompanha e fica saindo da arma"). Em cada animação dos braços (parado, andando,
    /// correndo, o tiro, o sacar) a mão esquerda fica parada no mesmo lugar da arma (na espingarda, da telha), mas a árvore
    /// do Animator mistura os ossos de duas animações, e andando e correndo a arma muda muito (correndo, ela vai girada e
    /// deitada, atravessada embaixo da tela): no meio da mistura, a mão saía até 12 cm do guarda-mão do rifle (4 cm da
    /// submetralhadora, 1,4 da espingarda; medido no Blender, nas animações exportadas).
    /// Depois do Animator, a mão volta para a pegada (a do Idle, lida uma vez por arma) por um IK de dois ossos (o braço e o
    /// antebraço, com o cotovelo no plano que a animação deu) e o giro dela. Fica de fora na recarga (a mão vai ao
    /// carregador ou ao cartucho), com o peso saindo e voltando aos poucos. Só no dono, com uma arma de fogo na mão e os
    /// braços de primeira pessoa à vista.
    /// </summary>
    public class MaoNaArma : MonoBehaviour
    {
        const int CamadaAcoes = 1;
        static readonly int ReloadId = Animator.StringToHash("Reload");
        // em quanto tempo o IK sai (a recarga começa) e volta (ela acabou: a mão já está de volta na arma)
        const float TempoSaindo = 0.08f, TempoVoltando = 0.2f;

        class Braco
        {
            public Transform braco, antebraco, mao, referencia;
            public Vector3 posicao;      // a pegada, no espaço da referência (a arma, ou a telha da espingarda)
            public Quaternion giro;
        }

        ArmaJogador _arma;
        JogadorRede _rede;
        readonly Dictionary<int, Braco> _bracos = new Dictionary<int, Braco>();
        readonly HashSet<int> _semBraco = new HashSet<int>();
        int _modeloAntes = -1;
        float _peso;

        void Awake()
        {
            _arma = GetComponent<ArmaJogador>();
            _rede = GetComponent<JogadorRede>();
        }

        void LateUpdate()
        {
            if (_arma == null || !_arma.enabled || _arma.modelos == null || _arma.animadores == null) return;
            if (!_arma.ComArma || _arma.MaosOcupadas || (_rede != null && _rede.cameraBracos != null && !_rede.cameraBracos.enabled))
            {
                _modeloAntes = -1;
                return;
            }
            int m = Itens.ModeloNaMao(_arma.NaMao);
            if (m < 0 || m >= _arma.modelos.Length || m >= _arma.animadores.Length || _arma.modelos[m] == null || !_arma.modelos[m].activeInHierarchy) return;
            var animador = _arma.animadores[m];
            if (animador == null || !animador.isActiveAndEnabled) return;
            var b = BracoDe(m, animador);
            if (b == null) return;
            bool recarga = _arma.Recarregando || Tocando(animador, ReloadId);
            float alvo = recarga ? 0f : 1f;
            if (m != _modeloAntes) _peso = alvo;   // sacando, a pegada já vale
            _modeloAntes = m;
            _peso = Mathf.MoveTowards(_peso, alvo, Time.deltaTime / (recarga ? TempoSaindo : TempoVoltando));
            if (_peso <= 0.001f) return;

            Vector3 alvoPos = b.referencia.TransformPoint(b.posicao);
            Quaternion alvoGiro = b.referencia.rotation * b.giro;
            if (_peso < 0.999f)
            {
                alvoPos = Vector3.Lerp(b.mao.position, alvoPos, _peso);
                alvoGiro = Quaternion.Slerp(b.mao.rotation, alvoGiro, _peso);
            }
            DoisOssos(b.braco, b.antebraco, b.mao, alvoPos);
            b.mao.rotation = alvoGiro;
        }

        static bool Tocando(Animator a, int estado)
        {
            if (a.layerCount <= CamadaAcoes) return false;
            if (a.GetCurrentAnimatorStateInfo(CamadaAcoes).shortNameHash == estado) return true;
            return a.IsInTransition(CamadaAcoes) && a.GetNextAnimatorStateInfo(CamadaAcoes).shortNameHash == estado;
        }

        /// <summary>
        /// Os ossos do braço esquerdo de um modelo e a pegada dele: a mão no quadro 0 do Idle, no espaço da arma (o osso
        /// <c>gun</c>; na espingarda, a telha, <c>pump</c>, que a mão leva no tiro). A pose de agora é guardada e volta
        /// depois da amostra.
        /// </summary>
        Braco BracoDe(int m, Animator animador)
        {
            if (_bracos.TryGetValue(m, out var pronto)) return pronto;
            if (_semBraco.Contains(m)) return null;
            var raiz = animador.transform;
            var todos = raiz.GetComponentsInChildren<Transform>(true);
            Transform Osso(string nome)
            {
                foreach (var t in todos) if (t.name == nome) return t;
                return null;
            }
            var b = new Braco
            {
                braco = Osso("upper_arm.L"), antebraco = Osso("forearm.L"), mao = Osso("hand.L"),
                referencia = Osso("pump") ?? Osso("gun"),
            };
            var idle = ClipeIdle(animador);
            if (b.braco == null || b.antebraco == null || b.mao == null || b.referencia == null || idle == null
                || b.antebraco.parent != b.braco || b.mao.parent != b.antebraco)
            {
                _semBraco.Add(m);
                return null;
            }
            var pos = new Vector3[todos.Length];
            var giros = new Quaternion[todos.Length];
            var escalas = new Vector3[todos.Length];
            for (int i = 0; i < todos.Length; i++)
            {
                pos[i] = todos[i].localPosition;
                giros[i] = todos[i].localRotation;
                escalas[i] = todos[i].localScale;
            }
            idle.SampleAnimation(animador.gameObject, 0f);
            b.posicao = b.referencia.InverseTransformPoint(b.mao.position);
            b.giro = Quaternion.Inverse(b.referencia.rotation) * b.mao.rotation;
            for (int i = 0; i < todos.Length; i++)
                todos[i].SetLocalPositionAndRotation(pos[i], giros[i]);
            for (int i = 0; i < todos.Length; i++) todos[i].localScale = escalas[i];
            _bracos[m] = b;
            return b;
        }

        // o clipe "Idle" do controlador (o override da arma, ou o próprio, na pistola)
        static AnimationClip ClipeIdle(Animator animador)
        {
            var c = animador.runtimeAnimatorController;
            if (c == null) return null;
            if (c is AnimatorOverrideController o)
            {
                var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                o.GetOverrides(pares);
                foreach (var p in pares)
                    if (p.Key != null && p.Key.name == "Idle") return p.Value != null ? p.Value : p.Key;
            }
            foreach (var clip in c.animationClips)
                if (clip != null && clip.name == "Idle") return clip;
            return null;
        }

        /// <summary>
        /// O IK de dois ossos (de "Simple Two Joint IK", Daniel Holden): o braço (a), o antebraço (b) e a mão (c). O ângulo
        /// do ombro e o do cotovelo mudam no plano do braço (o cotovelo fica para onde a animação o pôs) até a distância do
        /// ombro à mão ser a do alvo, e o braço todo gira até a mão chegar nele. Fora do alcance, o braço estica na direção dele.
        /// </summary>
        static void DoisOssos(Transform a, Transform b, Transform c, Vector3 alvo)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = Vector3.Distance(pa, pb), lcb = Vector3.Distance(pb, pc);
            if (lab < 1e-4f || lcb < 1e-4f) return;
            float lat = Mathf.Clamp(Vector3.Distance(pa, alvo), 1e-3f, (lab + lcb) * 0.9999f);
            Vector3 ac = (pc - pa).normalized, ab = (pb - pa).normalized, ba = -ab, bc = (pc - pb).normalized, at = (alvo - pa).normalized;
            float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, ab), -1f, 1f));
            float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f));
            float acAt0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, at), -1f, 1f));
            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            Vector3 eixo0 = Vector3.Cross(ac, ab);
            if (eixo0.sqrMagnitude > 1e-10f)
            {
                eixo0.Normalize();
                // o cotovelo primeiro (ele é filho do braço: depois, o giro do braço o leva junto)
                b.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, eixo0) * b.rotation;
                a.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, eixo0) * a.rotation;
            }
            Vector3 eixo1 = Vector3.Cross(ac, at);
            if (eixo1.sqrMagnitude > 1e-10f)
                a.rotation = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, eixo1.normalized) * a.rotation;
        }
    }
}
