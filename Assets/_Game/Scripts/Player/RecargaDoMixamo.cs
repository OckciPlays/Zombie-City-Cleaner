using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A recarga do Mixamo, em teste (19/11/2026, a pedido: "essa animação é apenas em 3ª pessoa? tudo o que formos fazer
    /// precisa ser em primeira e terceira também, para comparar. Na primeira o principal é seguir o padrão da pegada das
    /// armas"): com a chave do Mixamo no F1 ligada (<see cref="GolpeDoMixamo.Ligado"/>), a recarga do rifle e da
    /// submetralhadora segue o "Reloading Rifle While Standing" do Mixamo (os dados: RecargaDoMixamo.Dados.cs), nos braços de
    /// primeira pessoa e no corpo do dono (para os outros, a recarga ainda não vai pela rede). Desligada, a de sempre.
    ///  - A mão direita (com a arma: a pegada é a nossa) anda o que a do Mixamo anda desde o começo, vezes a
    ///    <see cref="Amplitude"/>, e gira o que ela gira; na primeira pessoa, a arma (o osso gun, que não é filho da mão)
    ///    vai junto, presa onde estava na mão no começo.
    ///  - A esquerda anda em relação à direita (à arma) o que a do Mixamo anda: sai do guarda-mão, vai ao pente, tira, desce,
    ///    põe de volta, puxa o ferrolho em cima e volta; o pente vai preso a ela do quadro 15 (pega) ao 51 (encaixa).
    ///  - O tempo: o pente saindo, entrando e o fim do Mixamo nos da arma (DadosDaArma.carregadorSai, carregadorEntra e
    ///    tempoRecarga), com a Recarga rápida.
    /// </summary>
    public static partial class RecargaDoMixamo
    {
        /// <summary>Quanto do caminho da mão direita do Mixamo (a esquerda anda inteira, para chegar ao pente).</summary>
        public static float Amplitude = 0.8f;

        // os quadros do Mixamo: o pente sai (a mão já puxou), entra (encaixou) e o fim (de volta à mira); a esquerda pega e
        // solta o pente
        const float QuadroSai = 21f, QuadroEntra = 48f, QuadroFim = 78f, PegaOPente = 15f, SoltaOPente = 51f;
        const int Numeros = 21;
        static int Quadros => Dados.Length / Numeros;

        /// <summary>As armas do teste: o rifle e a submetralhadora (e as de sucata delas).</summary>
        public static bool Usa(TipoDeArma t) => Armas.Real(t) == TipoDeArma.Rifle || Armas.Real(t) == TipoDeArma.Submetralhadora;

        static float Quadro(float t, DadosDaArma d, float rapido)
        {
            float sai = Mathf.Max(0.05f, d.carregadorSai * rapido), entra = Mathf.Max(sai + 0.05f, d.carregadorEntra * rapido);
            float fim = Mathf.Max(entra + 0.05f, d.tempoRecarga * rapido);
            if (t <= sai) return Mathf.Lerp(0f, QuadroSai, t / sai);
            if (t <= entra) return Mathf.Lerp(QuadroSai, QuadroEntra, (t - sai) / (entra - sai));
            return Mathf.Lerp(QuadroEntra, QuadroFim, Mathf.Clamp01((t - entra) / (fim - entra)));
        }

        static Vector3 Ler(float quadro, int k)
        {
            int n = Quadros;
            quadro = Mathf.Clamp(quadro, 0f, n - 1);
            int a = Mathf.FloorToInt(quadro), b = Mathf.Min(a + 1, n - 1);
            float f = quadro - a;
            var va = new Vector3(Dados[a * Numeros + k], Dados[a * Numeros + k + 1], Dados[a * Numeros + k + 2]);
            var vb = new Vector3(Dados[b * Numeros + k], Dados[b * Numeros + k + 1], Dados[b * Numeros + k + 2]);
            return Vector3.Lerp(va, vb, f);
        }

        // dos eixos da mão do Mixamo (os dedos, o lado, o dorso) para os de LookRotation(dedos, lado) (x o dorso, y o lado,
        // z os dedos)
        static Vector3 NaMao(Vector3 v) => new Vector3(v.z, v.y, v.x);

        static Quaternion Forma(Vector3 dedos, Vector3 lado) => Quaternion.LookRotation(dedos, Vector3.ProjectOnPlane(lado, dedos));

        /// <summary>Os braços de um corpo (o de primeira pessoa de uma arma, ou o do jogador) e a arma e o pente nele.</summary>
        public class Bracos
        {
            readonly Transform _ombro, _cotovelo, _mao, _ombroE, _cotoveloE, _maoE, _arma, _pente, _ferrolho;
            readonly Quaternion _formaD;          // a forma da mão direita (dedos, lado) no espaço do osso dela
            bool _ativo, _segurando;
            float _inicio;
            Vector3 _pulsoNaGuarda;               // do ombro, em braços, no espaço do corpo
            Quaternion _maoNaGuarda;              // no espaço do corpo
            Vector3 _esquerdaNaDireita;           // o pulso esquerdo nos eixos da forma da mão direita (m)
            Quaternion _giroDaEsquerda;           // a mão esquerda nos eixos da forma da direita
            Vector3 _armaNaMao;                   // o osso da arma na mão direita (na primeira pessoa)
            Quaternion _giroDaArma;
            Vector3 _penteLocal, _ferrolhoLocal;  // o pente e o ferrolho no lugar (o começo)
            Quaternion _penteGiro, _ferrolhoGiro;
            Vector3 _penteNaEsquerda;             // o pente preso na mão esquerda
            Quaternion _penteGiroNaEsquerda;

            public bool Valido => _ombro != null && _cotovelo != null && _mao != null && _ombroE != null && _cotoveloE != null && _maoE != null;

            public Bracos(Transform raiz)
            {
                Transform medio = null, indicador = null, minimo = null;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        case "upper_arm.R": _ombro = t; break;
                        case "forearm.R": _cotovelo = t; break;
                        case "hand.R": _mao = t; break;
                        case "upper_arm.L": _ombroE = t; break;
                        case "forearm.L": _cotoveloE = t; break;
                        case "hand.L": _maoE = t; break;
                        case "middle1.R": medio = t; break;
                        case "index1.R": indicador = t; break;
                        case "pinky1.R": minimo = t; break;
                        case "gun": _arma = t; break;
                        case "mag": _pente = t; break;
                        case "slide": _ferrolho = t; break;
                    }
                if (!Valido || medio == null || indicador == null || minimo == null)
                {
                    _ombro = null;
                    return;
                }
                // (no corpo, a arma é filha da mão: o osso gun não existe)
                Vector3 dedos = _mao.InverseTransformPoint(medio.position).normalized;
                Vector3 lado = _mao.InverseTransformPoint(indicador.position) - _mao.InverseTransformPoint(minimo.position);
                _formaD = Forma(dedos, lado);
            }

            /// <summary>No LateUpdate, depois da animação: recarregando uma das armas do teste, os braços no caminho do Mixamo.</summary>
            public void Aplicar(Quaternion corpo, bool recarregando, DadosDaArma d, float rapido)
            {
                if (!GolpeDoMixamo.Ligado || !Valido || !recarregando || d == null)
                {
                    _ativo = false;
                    return;
                }
                float braco = (_cotovelo.position - _ombro.position).magnitude + (_mao.position - _cotovelo.position).magnitude;
                if (braco < 0.05f) return;
                if (!_ativo)
                {
                    // a recarga começou: a pegada de agora
                    _ativo = true;
                    _segurando = false;
                    _inicio = Time.time;
                    var volta = Quaternion.Inverse(corpo);
                    _pulsoNaGuarda = volta * (_mao.position - _ombro.position) / braco;
                    _maoNaGuarda = volta * _mao.rotation;
                    var formaNoComeco = _mao.rotation * _formaD;
                    _esquerdaNaDireita = Quaternion.Inverse(formaNoComeco) * (_maoE.position - _mao.position);
                    _giroDaEsquerda = Quaternion.Inverse(formaNoComeco) * _maoE.rotation;
                    if (_arma != null)
                    {
                        _armaNaMao = Quaternion.Inverse(_mao.rotation) * (_arma.position - _mao.position);
                        _giroDaArma = Quaternion.Inverse(_mao.rotation) * _arma.rotation;
                    }
                    if (_pente != null) { _penteLocal = _pente.localPosition; _penteGiro = _pente.localRotation; }
                    if (_ferrolho != null) { _ferrolhoLocal = _ferrolho.localPosition; _ferrolhoGiro = _ferrolho.localRotation; }
                }
                float fim = d.tempoRecarga * rapido, t = Time.time - _inicio;
                if (t > fim) return;
                float q = Quadro(t, d, rapido);
                float peso = Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (t - (fim - 0.15f)) / 0.15f));

                // a direita (com a arma): anda e gira o que a do Mixamo anda e gira desde o começo
                Vector3 andou = (Ler(q, 0) - Ler(0f, 0)) * Amplitude;
                Vector3 pulso = _ombro.position + corpo * ((_pulsoNaGuarda + andou) * braco);
                Quaternion giro = Forma(Ler(q, 3), Ler(q, 6)) * Quaternion.Inverse(Forma(Ler(0f, 3), Ler(0f, 6)));
                Quaternion maoQuero = corpo * Quaternion.Slerp(Quaternion.identity, giro, Amplitude) * _maoNaGuarda;
                Vector3 armaAgora = _arma != null ? _arma.position : Vector3.zero;
                Quaternion armaGiroAgora = _arma != null ? _arma.rotation : Quaternion.identity;
                Vector3 pulsoEAgora = _maoE.position;
                Quaternion maoEAgora = _maoE.rotation;
                Vector3 dobra = _cotovelo.position - (_ombro.position + _mao.position) * 0.5f;
                Quaternion maoAgora = _mao.rotation;
                DoisOssos(_ombro, _cotovelo, _mao, Vector3.Lerp(_mao.position, pulso, peso), dobra);
                _mao.rotation = Quaternion.Slerp(maoAgora, maoQuero, peso);
                if (_arma != null)
                    _arma.SetPositionAndRotation(Vector3.Lerp(armaAgora, _mao.position + _mao.rotation * _armaNaMao, peso),
                                                 Quaternion.Slerp(armaGiroAgora, _mao.rotation * _giroDaArma, peso));

                // a esquerda: em relação à direita, o que a do Mixamo anda em relação à dele
                var forma = _mao.rotation * _formaD;
                Vector3 relativo = NaMao(Ler(q, 9) - Ler(0f, 9)) * braco;
                Vector3 pulsoE = _mao.position + forma * (_esquerdaNaDireita + relativo);
                Quaternion giroE = Forma(NaMao(Ler(q, 12)), NaMao(Ler(q, 15))) * Quaternion.Inverse(Forma(NaMao(Ler(0f, 12)), NaMao(Ler(0f, 15))));
                Quaternion maoEQuero = forma * giroE * _giroDaEsquerda;
                DoisOssos(_ombroE, _cotoveloE, _maoE, Vector3.Lerp(pulsoEAgora, pulsoE, peso), corpo * Ler(q, 18));
                _maoE.rotation = Quaternion.Slerp(maoEAgora, maoEQuero, peso);

                // o pente: preso na esquerda do quadro 15 ao 51; antes e depois, no lugar (o da animação de sempre não vale)
                if (_pente != null)
                {
                    bool segura = q >= PegaOPente && q <= SoltaOPente;
                    if (segura && !_segurando)
                    {
                        _pente.localPosition = _penteLocal;
                        _pente.localRotation = _penteGiro;
                        _penteNaEsquerda = Quaternion.Inverse(_maoE.rotation) * (_pente.position - _maoE.position);
                        _penteGiroNaEsquerda = Quaternion.Inverse(_maoE.rotation) * _pente.rotation;
                    }
                    _segurando = segura;
                    if (segura) _pente.SetPositionAndRotation(_maoE.position + _maoE.rotation * _penteNaEsquerda, _maoE.rotation * _penteGiroNaEsquerda);
                    else
                    {
                        _pente.localPosition = _penteLocal;
                        _pente.localRotation = _penteGiro;
                    }
                }
                if (_ferrolho != null)
                {
                    _ferrolho.localPosition = _ferrolhoLocal;
                    _ferrolho.localRotation = _ferrolhoGiro;
                }
            }

            // o braço de dois ossos até o pulso, o cotovelo para o lado da dobra (pelo caminho mais curto de cada osso)
            static void DoisOssos(Transform a, Transform b, Transform c, Vector3 alvo, Vector3 dobra)
            {
                Vector3 pa = a.position;
                float l1 = (b.position - pa).magnitude, l2 = (c.position - b.position).magnitude;
                if (l1 < 1e-4f || l2 < 1e-4f) return;
                Vector3 para = alvo - pa;
                float dd = Mathf.Clamp(para.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
                Vector3 dir = para.sqrMagnitude > 1e-8f ? para.normalized : a.forward;
                float x = (l1 * l1 - l2 * l2 + dd * dd) / (2f * dd);
                float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
                Vector3 p = Vector3.ProjectOnPlane(dobra, dir);
                if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(Vector3.down, dir);
                p.Normalize();
                Vector3 cotovelo = pa + dir * x + p * h;
                a.rotation = Quaternion.FromToRotation(b.position - pa, cotovelo - pa) * a.rotation;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, pa + dir * dd - b.position) * b.rotation;
            }
        }
    }
}
