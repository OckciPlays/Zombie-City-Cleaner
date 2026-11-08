using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os músculos do corpo que cai (07/11/2026, a pedido: os zumbis morriam "muito mole", "parece que não possuem ossos",
    /// "caem e despencam igual uma maria mole"). Medido na prévia, havia duas causas:
    ///  - <b>o tranco:</b> as juntas da montagem têm os limites contados da pose de repouso (de braços abertos), e as poses
    ///    do jogo passam deles (o antebraço andando, 50 a 60°; correndo ou atacando, 90 a 166°). No instante da morte a
    ///    física puxava tudo de volta de uma vez: os braços abriam e o corpo desmontava. Agora, em cada queda, as juntas
    ///    tomam a pose de agora como o zero delas (<see cref="LimitesNaPose"/>) e os limites da montagem andam junto (o
    ///    quanto a pose já tinha girado de cada lado), alargados só o que falta para a pose caber;
    ///  - <b>nada segurava o corpo:</b> as juntas não tinham força nenhuma. Agora cada junta puxa de volta para a pose da
    ///    morte, com uma força que some em ~0,5 a 1 s (o corpo cai com peso e forma, e os joelhos cedem primeiro), e um
    ///    atrito entre as partes até ele assentar (os braços não balançam como borracha).
    /// O jeito depende da morte (<see cref="Morte"/>): o tiro na cabeça cai de joelhos e depois bate o peito no chão; o que
    /// corria mergulha para a frente; o arremesso (a espingarda de perto, o carro) desliga quase na hora, como antes; o
    /// "corte de energia" (o sem cabeça que ainda andou), mole de uma vez. Custa só nos ~3,5 s depois da queda, e só nos
    /// corpos caindo.
    /// </summary>
    public partial class CorpoDeZumbi
    {
        /// <summary>Como ele morreu: o jeito de cair.</summary>
        public enum Morte : byte { Comum, Cabeca, Correndo, Arremesso, Desligou }

        const float TempoDoAtrito = 3.5f;   // o atrito entre as partes, até perto de assentar
        const float Atrito = 6f;            // (1/s: tira da diferença de giro entre as partes)

        // a força de cada parte (o quanto ela segura a pose, por parte filha da junta)
        static readonly float[] Rigidez = { 0f, 420f, 120f, 90f, 60f, 90f, 60f, 460f, 520f, 460f, 520f };
        static PhysicsMaterial _atritoDoCorpo;

        // a junta de cada parte: as partes do filho e do pai, o giro entre eles no repouso e os alvos desta queda
        int[] _filho, _pai, _juntaDaParte;
        Quaternion[] _repousoRel, _alvo, _alvoJoelhos, _alvoPeito;
        Vector3 _altoNoQuadril, _frenteNoQuadril, _direitaNoQuadril;
        Transform _peE, _peD;
        Morte _jeito;
        int _passos;
        float _segura, _ate;   // na morte comum: quanto tempo a força fica cheia e quando ela acaba
        bool _comMusculos, _empurrouOPeito;

        /// <summary>
        /// O sentido do giro da física é o contrário do eixo da junta (medido na prévia: o joelho girado +60° em volta do eixo
        /// dele fica a -60° para a física).
        /// </summary>
        internal static float SinalDoGiro = -1f;

        void ConhecerOsMusculos()
        {
            int n = _juntas.Length;
            _filho = new int[n];
            _pai = new int[n];
            _repousoRel = new Quaternion[n];
            _alvo = new Quaternion[n];
            _alvoJoelhos = new Quaternion[n];
            _alvoPeito = new Quaternion[n];
            _juntaDaParte = new int[partes.Length];
            for (int i = 0; i < _juntaDaParte.Length; i++) _juntaDaParte[i] = -1;
            for (int i = 0; i < n; i++)
            {
                _filho[i] = System.Array.IndexOf(partes, _juntas[i].GetComponent<Rigidbody>());
                _pai[i] = System.Array.IndexOf(partes, _juntas[i].connectedBody);
                if (_filho[i] >= 0) _juntaDaParte[_filho[i]] = i;
                // (os ossos estão no repouso agora)
                if (_filho[i] >= 0 && _pai[i] >= 0)
                    _repousoRel[i] = Quaternion.Inverse(partes[_pai[i]].transform.rotation) * partes[_filho[i]].transform.rotation;
            }
            var quadril = partes[0].transform;
            _altoNoQuadril = Quaternion.Inverse(quadril.rotation) * transform.up;
            _frenteNoQuadril = Quaternion.Inverse(quadril.rotation) * transform.forward;
            _direitaNoQuadril = Quaternion.Inverse(quadril.rotation) * transform.right;
            foreach (var t in esqueleto)
            {
                if (t.name == "foot.L") _peE = t;
                else if (t.name == "foot.R") _peD = t;
            }
            if (_atritoDoCorpo == null)
                _atritoDoCorpo = new PhysicsMaterial("Corpo")
                {
                    dynamicFriction = 0.9f, staticFriction = 1f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            foreach (var p in partes)
                foreach (var c in p.GetComponents<Collider>()) c.sharedMaterial = _atritoDoCorpo;
        }

        // (aqui, antes de a física rodar, o giro vem do osso: o do corpo rígido ainda é o de antes)

        /// <summary>
        /// As juntas tomam a pose de agora como o zero delas (sem o tranco da morte), e os limites da montagem andam junto:
        /// o giro em volta do eixo da junta que a pose já tinha sai dos dois lados; o balanço alarga o que falta para caber.
        /// </summary>
        void LimitesNaPose()
        {
            for (int i = 0; i < _juntas.Length; i++)
            {
                if (_filho[i] < 0 || _pai[i] < 0) continue;
                var j = _juntas[i];
                var agora = Quaternion.Inverse(partes[_pai[i]].transform.rotation) * partes[_filho[i]].transform.rotation;
                var delta = Quaternion.Inverse(_repousoRel[i]) * agora;   // do repouso até agora, no espaço do filho
                Vector3 eixo = j.axis.normalized;
                var v = new Vector3(delta.x, delta.y, delta.z);
                Vector3 p = eixo * Vector3.Dot(v, eixo);
                var giro = new Quaternion(p.x, p.y, p.z, delta.w);
                float norma = Mathf.Sqrt(giro.x * giro.x + giro.y * giro.y + giro.z * giro.z + giro.w * giro.w);
                float tw = 0f;
                if (norma > 1e-6f)
                {
                    giro = new Quaternion(giro.x / norma, giro.y / norma, giro.z / norma, giro.w / norma);
                    giro.ToAngleAxis(out tw, out Vector3 ax);
                    if (tw > 180f) tw -= 360f;
                    if (Vector3.Dot(ax, eixo) < 0f) tw = -tw;
                }
                float balanco = Quaternion.Angle(delta * Quaternion.Inverse(giro), Quaternion.identity);
                var (baixo, alto, b1, b2) = _limites[i];
                float lo = Mathf.Clamp(Mathf.Min(baixo.limit - SinalDoGiro * tw, -2f), -177f, -2f);
                float hi = Mathf.Clamp(Mathf.Max(alto.limit - SinalDoGiro * tw, 2f), 2f, 177f);
                j.lowTwistLimit = new SoftJointLimit { limit = lo };
                j.highTwistLimit = new SoftJointLimit { limit = hi };
                j.swing1Limit = new SoftJointLimit { limit = Mathf.Min(170f, Mathf.Max(b1.limit, balanco + 8f)) };
                j.swing2Limit = new SoftJointLimit { limit = Mathf.Min(170f, Mathf.Max(b2.limit, balanco + 8f)) };
            }
            Reancorar();
        }

        /// <summary>Os músculos da queda: os alvos (a pose de agora e, no tiro na cabeça, os joelhos e o peito) e o tempo.</summary>
        void Musculos(Morte jeito)
        {
            _jeito = jeito;
            _passos = 0;
            _empurrouOPeito = false;
            _comMusculos = true;
            // cada corpo segura um pouco diferente (pelo lugar e a hora da queda: igual em todas as máquinas não importa aqui)
            Vector3 onde = transform.position;
            float sorte = Mathf.Abs(Mathf.Sin(onde.x * 12.9898f + onde.z * 4.1414f + caiuEm * 78.233f));
            _segura = Mathf.Lerp(0.12f, 0.25f, sorte);
            _ate = _segura + Mathf.Lerp(0.35f, 0.5f, 1f - sorte);
            for (int i = 0; i < _juntas.Length; i++)
                if (_filho[i] >= 0 && _pai[i] >= 0)
                    _alvo[i] = _alvoJoelhos[i] = _alvoPeito[i] = Quaternion.Inverse(partes[_pai[i]].transform.rotation) * partes[_filho[i]].transform.rotation;
            if (jeito == Morte.Cabeca) AlvosDeJoelhos();
        }

        /// <summary>
        /// O tiro na cabeça: primeiro de joelhos (as coxas retas embaixo do quadril, as canelas para trás, o peito em pé);
        /// depois o quadril dobra e o peito vai para a frente, até o chão.
        /// </summary>
        void AlvosDeJoelhos()
        {
            var quadril = partes[0].transform;
            Vector3 alto = quadril.rotation * _altoNoQuadril, frente = quadril.rotation * _frenteNoQuadril;
            Quaternion inv = Quaternion.Inverse(quadril.rotation);
            void Perna(int coxa, int canela, Transform pe)
            {
                int jc = _juntaDaParte[coxa], jk = _juntaDaParte[canela];
                if (jc < 0 || jk < 0 || pe == null) return;
                Transform th = partes[coxa].transform, sh = partes[canela].transform;
                Vector3 dirCoxa = (sh.position - th.position).normalized, dirCanela = (pe.position - sh.position).normalized;
                // de joelhos: a coxa para baixo, a canela para trás (o joelho dobrado ~95°)
                Vector3 querCoxa = -alto;
                Vector3 querCanela = (-alto * Mathf.Cos(95f * Mathf.Deg2Rad) - frente * Mathf.Sin(95f * Mathf.Deg2Rad)).normalized;
                var giroCoxa = Quaternion.FromToRotation(dirCoxa, querCoxa);
                var coxaA = giroCoxa * th.rotation;
                var canelaA = Quaternion.FromToRotation(giroCoxa * dirCanela, querCanela) * (giroCoxa * sh.rotation);
                _alvoJoelhos[jc] = inv * coxaA;
                _alvoJoelhos[jk] = Quaternion.Inverse(coxaA) * canelaA;
                // o peito no chão: o quadril dobra 80° (a coxa vai para a frente do quadril), o joelho continua
                Vector3 querCoxaB = (-alto * Mathf.Cos(80f * Mathf.Deg2Rad) + frente * Mathf.Sin(80f * Mathf.Deg2Rad)).normalized;
                var giroB = Quaternion.FromToRotation(dirCoxa, querCoxaB);
                var coxaB = giroB * th.rotation;
                Vector3 querCanelaB = Quaternion.FromToRotation(querCoxa, querCoxaB) * querCanela;
                var canelaB = Quaternion.FromToRotation(giroB * dirCanela, querCanelaB) * (giroB * sh.rotation);
                _alvoPeito[jc] = inv * coxaB;
                _alvoPeito[jk] = Quaternion.Inverse(coxaB) * canelaB;
            }
            Perna(7, 8, _peE);
            Perna(9, 10, _peD);
            int jp = _juntaDaParte[1];
            if (jp >= 0 && pescoco != null)
            {
                var peito = partes[1].transform;
                Vector3 dir = (pescoco.position - peito.position).normalized;
                _alvoJoelhos[jp] = inv * (Quaternion.FromToRotation(dir, alto) * peito.rotation);
                Vector3 frenteB = (alto * Mathf.Cos(25f * Mathf.Deg2Rad) + frente * Mathf.Sin(25f * Mathf.Deg2Rad)).normalized;
                _alvoPeito[jp] = inv * (Quaternion.FromToRotation(dir, frenteB) * peito.rotation);
            }
        }

        /// <summary>A força da junta agora (0 a 1, vezes a rigidez da parte) e para onde ela puxa.</summary>
        float Forca(int i, float t, out Quaternion alvo)
        {
            int parte = _filho[i];
            bool joelho = parte == 8 || parte == 10, braco = parte >= 3 && parte <= 6;
            alvo = _alvo[i];
            switch (_jeito)
            {
                case Morte.Arremesso:
                    return 1f - Mathf.Clamp01(t / 0.15f);
                case Morte.Desligou:
                    return 0f;
                case Morte.Correndo:
                    if (joelho) return 1f - Mathf.Clamp01((t - 0.03f) / 0.17f);
                    return 1f - Mathf.Clamp01((t - 0.08f) / 0.37f);
                case Morte.Cabeca:
                    if (t < 0.4f)
                    {
                        alvo = _alvoJoelhos[i];
                        return braco ? 0.25f : parte == 2 ? 0.2f : 1.3f;
                    }
                    alvo = _alvoPeito[i];
                    if (t < 0.85f) return braco ? 0.15f : parte == 2 ? 0.1f : 1.1f;
                    return (braco ? 0.15f : 0.8f) * (1f - Mathf.Clamp01((t - 0.85f) / 0.4f));
                default:   // Comum: segura a forma e tomba para o lado do tiro, e solta antes de chegar ao chão
                    if (joelho) return 1f - Mathf.Clamp01((t - _segura * 0.8f) / (_ate - _segura));
                    if (braco) return 1f - Mathf.Clamp01((t - _segura) / 0.25f);
                    return 1f - Mathf.Clamp01((t - _segura) / (_ate - _segura));
            }
        }

        static float Inercia(Rigidbody rb, Vector3 eixo)
        {
            var local = Quaternion.Inverse(rb.rotation * rb.inertiaTensorRotation) * eixo;
            var I = rb.inertiaTensor;
            return local.x * local.x * I.x + local.y * local.y * I.y + local.z * local.z * I.z;
        }

        /// <summary>Um passo de física dos músculos e do atrito entre as partes (do FixedUpdate; a prévia chama direto).</summary>
        internal void PassoDosMusculos()
        {
            if (!_comMusculos) return;
            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            float t = _passos * dt;
            _passos++;
            if (t > TempoDoAtrito)
            {
                _comMusculos = false;
                return;
            }
            if (_jeito == Morte.Cabeca && !_empurrouOPeito && t >= 0.4f)
            {
                // o peito vai para a frente (para cair de cara, e não sentado nem de lado)
                _empurrouOPeito = true;
                var q = partes[0].transform;
                Vector3 frente = q.rotation * _frenteNoQuadril;
                frente.y = 0f;
                frente = frente.sqrMagnitude > 1e-4f ? frente.normalized : transform.forward;
                // e gira para a frente (em volta da direita do corpo: o alto vai para a frente)
                Vector3 direita = Vector3.Cross(Vector3.up, frente).normalized;
                if (!partes[1].isKinematic)
                {
                    partes[1].AddForce(frente * 1.8f, ForceMode.VelocityChange);
                    partes[1].AddTorque(direita * 5f, ForceMode.VelocityChange);
                }
                if (!partes[2].isKinematic) partes[2].AddForce(frente * 2f, ForceMode.VelocityChange);
                if (!partes[0].isKinematic) partes[0].AddTorque(direita * 3f, ForceMode.VelocityChange);
            }
            for (int i = 0; i < _juntas.Length; i++)
            {
                if (_filho[i] < 0 || _pai[i] < 0) continue;
                var c = partes[_filho[i]];
                var p = partes[_pai[i]];
                if (c.isKinematic || p.isKinematic) continue;
                float f = Forca(i, t, out var alvo);
                Vector3 relativo = c.angularVelocity - p.angularVelocity;
                Vector3 acc = -relativo * Atrito;
                if (f > 0f)
                {
                    float k = Rigidez[Mathf.Clamp(_filho[i], 0, Rigidez.Length - 1)] * f;
                    float d = 1.5f * Mathf.Sqrt(k);
                    var erro = p.rotation * alvo * Quaternion.Inverse(c.rotation);
                    erro.ToAngleAxis(out float graus, out Vector3 eixo);
                    if (graus > 180f) graus -= 360f;
                    if (!float.IsNaN(eixo.x) && Mathf.Abs(graus) > 0.01f)
                        acc += eixo * (k * Mathf.Clamp(graus * Mathf.Deg2Rad, -1.6f, 1.6f));
                    acc -= relativo * d;
                }
                float tamanho = acc.magnitude;
                if (tamanho < 1e-4f) continue;
                Vector3 u = acc / tamanho;
                float ic = Inercia(c, u), ip = Inercia(p, u);
                if (ic <= 0f || ip <= 0f) continue;
                Vector3 torque = acc * (ic * ip / (ic + ip));
                c.AddTorque(torque, ForceMode.Force);
                p.AddTorque(-torque, ForceMode.Force);
            }
        }
    }
}
