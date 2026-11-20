using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pegada firme das armas de fogo, em teste (19/11/2026, a pedido: "parece ainda que o pulso é quebrado... Rifle, smg,
    /// espingarda e sniper"; depois, vendo a primeira versão: "o restante achei que estão com o braço muito aberto... Precisamos
    /// dar mais pegada e força no pulso e na mão. O ângulo da abertura está muito grande"; a da submetralhadora, que prendeu o
    /// braço à arma, ficou boa). Com a chave do Mixamo no F1 ligada (<see cref="GolpeDoMixamo.Ligado"/>):
    ///  - As mãos ficam onde estão na arma (a pegada é a nossa) e o ombro não se mexe.
    ///  - O cotovelo escolhe, entre as posições possíveis (o círculo com o ombro e o pulso parados), a que junta três coisas:
    ///    o pulso mais reto (o antebraço como no "Rifle Aiming Idle" do Mixamo, onde ele dobra uns 33°), o cotovelo para
    ///    baixo e rente ao corpo (o braço de apoio um pouco para dentro: sem a abertura que machuca o ombro) e perto de onde a
    ///    animação já o põe (a pose de antes era melhor).
    ///  - O antebraço gira metade do giro da mão (sem torcer o pulso como um pano).
    ///  - Os dedos fecham um pouco mais em volta da arma (o polegar também): a mão aperta.
    /// Nos braços de primeira pessoa e no corpo.
    /// </summary>
    public static class PegadaDoMixamo
    {
        // o antebraço (do cotovelo ao pulso) nos eixos da mão (os dedos, o lado do mínimo ao indicador e o dorso), no
        // "Rifle Aiming Idle" do Mixamo (a média dos 94 quadros)
        static readonly Vector3 AntebracoDireito = new Vector3(0.8288f, 0.3031f, -0.4704f);
        static readonly Vector3 AntebracoEsquerdo = new Vector3(0.8485f, 0.0198f, 0.5289f);

        // o cotovelo nunca sai disto do "para baixo e rente" (graus: nada de cotovelo para cima ou aberto, que machuca o ombro)
        const float AberturaMaxima = 50f;
        // o peso de cada coisa na escolha do cotovelo, dentro dessa faixa (por grau): o pulso dobrado além dos 33° do Mixamo,
        // o cotovelo fora do rente e o quanto ele sai de onde a animação pôs; e o castigo por grau de pulso pior do que a
        // animação deixava (com 5° de folga)
        const float PesoDoPulso = 0.6f, PesoDaAbertura = 0.5f, PesoDaMudanca = 0.15f, CastigoDoPior = 3f;
        const float PulsoDoMixamo = 33f, FolgaDoPulso = 5f;
        // quanto os dedos fecham a mais (graus): a primeira e a segunda falange, e o polegar
        const float FechaDedo1 = 10f, FechaDedo2 = 12f, FechaPolegar = 6f, FechaPolegar2 = 8f;

        /// <summary>As armas do teste: as de duas mãos (o rifle, a submetralhadora, a espingarda e a sniper, e as de sucata).</summary>
        public static bool Usa(TipoDeArma t)
        {
            var r = Armas.Real(t);
            return r == TipoDeArma.Rifle || r == TipoDeArma.Submetralhadora || r == TipoDeArma.Espingarda || r == TipoDeArma.Sniper;
        }

        /// <summary>Os dois braços de um corpo (o de primeira pessoa de uma arma, ou o do jogador).</summary>
        public class Bracos
        {
            readonly Braco _direito, _esquerdo;

            public Bracos(Transform raiz)
            {
                _direito = new Braco(raiz, ".R", AntebracoDireito, true);
                _esquerdo = new Braco(raiz, ".L", AntebracoEsquerdo, false);
            }

            /// <summary>
            /// No LateUpdate, depois da animação: os cotovelos e os dedos. <paramref name="corpo"/>: os eixos do corpo (ou da
            /// câmera, na primeira pessoa), para o "para baixo" e o "para dentro".
            /// </summary>
            public void Aplicar(Quaternion corpo)
            {
                if (!GolpeDoMixamo.Ligado) return;
                _direito.Aplicar(corpo);
                _esquerdo.Aplicar(corpo);
            }

            /// <summary>Para o teste: o pulso (graus, do antebraço ao dedo do meio) e a abertura do cotovelo (graus do "para baixo").</summary>
            public string Medida(Quaternion corpo) => $"direita {_direito.Medida(corpo)}; esquerda {_esquerdo.Medida(corpo)}";
        }

        sealed class Braco
        {
            readonly Transform _ombro, _cotovelo, _mao, _medio, _indicador, _minimo;
            readonly Transform[] _dedos1, _dedos2;
            readonly Transform _polegar, _polegar2;
            readonly Vector3 _antebraco;
            readonly bool _direito;

            public Braco(Transform raiz, string lado, Vector3 antebraco, bool direito)
            {
                _antebraco = antebraco;
                _direito = direito;
                _dedos1 = new Transform[4];
                _dedos2 = new Transform[4];
                string[] nomes = { "index", "middle", "ring", "pinky" };
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "upper_arm" + lado) _ombro = t;
                    else if (t.name == "forearm" + lado) _cotovelo = t;
                    else if (t.name == "hand" + lado) _mao = t;
                    else if (t.name == "thumb" + lado) _polegar = t;
                    else if (t.name == "thumb2" + lado) _polegar2 = t;
                    for (int i = 0; i < 4; i++)
                    {
                        if (t.name == nomes[i] + "1" + lado) _dedos1[i] = t;
                        else if (t.name == nomes[i] + "2" + lado) _dedos2[i] = t;
                    }
                }
                _medio = _dedos1[1];
                _indicador = _dedos1[0];
                _minimo = _dedos1[3];
            }

            bool Valido => _ombro != null && _cotovelo != null && _mao != null && _medio != null && _indicador != null && _minimo != null
                           && _ombro.lossyScale.x > 0.01f;   // (o braço esquerdo escondido da primeira pessoa do machado)

            // os eixos da mão: os dedos (do punho ao dedo do meio), o lado (do mínimo ao indicador) e a palma
            void Eixos(out Vector3 dedos, out Vector3 lado, out Vector3 palma)
            {
                dedos = (_medio.position - _mao.position).normalized;
                lado = Vector3.ProjectOnPlane(_indicador.position - _minimo.position, dedos).normalized;
                Vector3 c = Vector3.Cross(lado, dedos);   // (na direita, o dorso; na esquerda, a palma)
                palma = _direito ? -c : c;
            }

            // o "para baixo e rente ao corpo" do cotovelo: para baixo; o de apoio (o esquerdo) um pouco para dentro
            Vector3 Rente(Quaternion corpo) => corpo * (_direito ? new Vector3(0.1f, -1f, 0f) : new Vector3(0.35f, -1f, 0f));

            public void Aplicar(Quaternion corpo)
            {
                if (!Valido) return;
                Vector3 pulso = _mao.position;
                Quaternion pegada = _mao.rotation;
                Vector3 s = _ombro.position;
                float l1 = (_cotovelo.position - s).magnitude, l2 = (pulso - _cotovelo.position).magnitude;
                if (l1 < 1e-4f || l2 < 1e-4f) return;
                Vector3 para = pulso - s;
                float d = para.magnitude;
                if (d < 1e-4f) return;
                Vector3 dir = para / d;
                d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
                float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
                float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
                Vector3 meio = s + dir * x;
                Vector3 agora = Vector3.ProjectOnPlane(_cotovelo.position - meio, dir);
                if (agora.sqrMagnitude < 1e-8f || h < 1e-4f) return;
                agora.Normalize();

                Eixos(out var dedos, out var lado, out _);
                Vector3 dorso = Vector3.Cross(lado, dedos);
                Vector3 quero = (dedos * _antebraco.x + lado * _antebraco.y + dorso * _antebraco.z).normalized;
                Vector3 rente = Vector3.ProjectOnPlane(Rente(corpo), dir);

                // o cotovelo: 24 lugares no círculo, a partir de onde está, só os que ficam para baixo e rente ao corpo (até 50°);
                // entre eles, o do pulso mais reto (o do Mixamo dobra 33°), mais rente e mais perto da animação, castigando o pulso
                // que fique pior do que ela deixava. Sem nenhum na faixa, fica como a animação pôs
                float antes = Torto(_cotovelo.position, pulso);
                Vector3 melhor = agora;
                float menor = float.MaxValue;
                for (int i = -12; i < 12; i++)
                {
                    float giro = i * 15f;
                    Vector3 p = Quaternion.AngleAxis(giro, dir) * agora;
                    float abertura = rente.sqrMagnitude > 1e-8f ? Vector3.Angle(p, rente) : 0f;
                    if (abertura > AberturaMaxima) continue;
                    Vector3 cotovelo = meio + p * h;
                    float torto = Torto(cotovelo, pulso);
                    float custo = PesoDoPulso * Mathf.Max(0f, torto - PulsoDoMixamo) + PesoDaAbertura * abertura + PesoDaMudanca * Mathf.Abs(giro)
                                  + CastigoDoPior * Mathf.Max(0f, torto - antes - FolgaDoPulso);
                    if (custo < menor) { menor = custo; melhor = p; }
                }
                if (menor == float.MaxValue) melhor = agora;
                DoisOssos(_ombro, _cotovelo, _mao, pulso, melhor);
                Torcer(_cotovelo, _mao, pegada);
                _mao.rotation = pegada;
                FecharDedos();
            }

            // o pulso dobrado com o cotovelo ali: do antebraço à mão (do pulso ao dedo do meio), em graus
            float Torto(Vector3 cotovelo, Vector3 pulso) => Vector3.Angle(pulso - cotovelo, _medio.position - pulso);

            // os dedos fecham um pouco mais (em volta da linha dos nós, para a palma), o polegar também
            void FecharDedos()
            {
                Eixos(out var dedos, out _, out var palma);
                Vector3 eixo = Vector3.Cross(dedos, palma);
                if (eixo.sqrMagnitude < 1e-8f) return;
                eixo.Normalize();
                for (int i = 0; i < 4; i++)
                {
                    if (_dedos1[i] != null) _dedos1[i].rotation = Quaternion.AngleAxis(FechaDedo1, eixo) * _dedos1[i].rotation;
                    if (_dedos2[i] != null) _dedos2[i].rotation = Quaternion.AngleAxis(FechaDedo2, eixo) * _dedos2[i].rotation;
                }
                Fechar(_polegar, _polegar2, palma, FechaPolegar);
                Fechar(_polegar2, null, palma, FechaPolegar2);
            }

            static void Fechar(Transform osso, Transform filho, Vector3 palma, float graus)
            {
                if (osso == null) return;
                Vector3 ao = filho != null ? filho.position - osso.position : osso.up;
                Vector3 eixo = Vector3.Cross(ao, palma);
                if (eixo.sqrMagnitude < 1e-8f) return;
                osso.rotation = Quaternion.AngleAxis(graus, eixo.normalized) * osso.rotation;
            }

            public string Medida(Quaternion corpo)
            {
                if (!Valido) return "?";
                float pulsoTorto = Vector3.Angle(_mao.position - _cotovelo.position, _medio.position - _mao.position);
                Vector3 s = _ombro.position, pulso = _mao.position;
                Vector3 dir = (pulso - s).normalized;
                Vector3 p = Vector3.ProjectOnPlane(_cotovelo.position - s, dir);
                Vector3 rente = Vector3.ProjectOnPlane(Rente(corpo), dir);
                float abertura = p.sqrMagnitude > 1e-8f && rente.sqrMagnitude > 1e-8f ? Vector3.Angle(p, rente) : 0f;
                return $"pulso {pulsoTorto:F0}°, cotovelo {abertura:F0}° do rente";
            }

            static void DoisOssos(Transform a, Transform b, Transform c, Vector3 alvo, Vector3 dobra)
            {
                Vector3 pa = a.position;
                float l1 = (b.position - pa).magnitude, l2 = (c.position - b.position).magnitude;
                Vector3 para = alvo - pa;
                float dd = Mathf.Clamp(para.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
                Vector3 dir = para.normalized;
                float x = (l1 * l1 - l2 * l2 + dd * dd) / (2f * dd);
                float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
                Vector3 p = Vector3.ProjectOnPlane(dobra, dir).normalized;
                Vector3 cotovelo = pa + dir * x + p * h;
                a.rotation = Quaternion.FromToRotation(b.position - pa, cotovelo - pa) * a.rotation;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, pa + dir * dd - b.position) * b.rotation;
            }

            // o antebraço gira em volta dele mesmo metade do que falta para a mão (o pulso não fica torcido como um pano)
            static void Torcer(Transform antebraco, Transform mao, Quaternion quero)
            {
                Vector3 eixo = mao.position - antebraco.position;
                if (eixo.sqrMagnitude < 1e-8f) return;
                eixo.Normalize();
                Vector3 agora = Vector3.ProjectOnPlane(mao.rotation * Vector3.forward, eixo);
                Vector3 depois = Vector3.ProjectOnPlane(quero * Vector3.forward, eixo);
                if (agora.sqrMagnitude < 1e-6f || depois.sqrMagnitude < 1e-6f) return;
                float angulo = Mathf.Clamp(Vector3.SignedAngle(agora, depois, eixo) * 0.5f, -80f, 80f);
                antebraco.rotation = Quaternion.AngleAxis(angulo, eixo) * antebraco.rotation;
            }
        }
    }
}
