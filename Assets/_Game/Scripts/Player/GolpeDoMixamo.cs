using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os golpes do Mixamo, em teste (19/11/2026, a pedido: "vamos tentar e fazer um teste... se tiver como dar uma diminuída
    /// na amplitude do ataque, acho que seria melhor. Vamos tentar isso com algumas armas... não remova nada do que temos
    /// hoje"; depois: a faca, a lança e o bastão com animações próprias, "tudo o que formos fazer precisa ser em primeira e
    /// terceira, para comparar; na primeira o principal é seguir o padrão da pegada das armas"): com a chave do F1 (a aba
    /// Fauna) ligada, o golpe das armas brancas segue o braço direito de uma animação do Mixamo (os dados:
    /// GolpeDoMixamo.Dados.cs), nos braços de primeira pessoa e no corpo. Desligada (o normal), nada muda: o golpe de sempre.
    ///  - O machado e a tocha: o "Standing Melee Attack Downward" (a machadada de uma mão, o Pro Melee Axe Pack); a faca: o
    ///    "Stabbing" (a estocada com a mão de trás e a lâmina para a frente); a lança: o "Bayonet Stab" (a estocada de
    ///    baioneta ou lança); o bastão: o primeiro golpe do "Two Hand Club Combo" (a clava com as duas mãos).
    ///  - O pulso: a partir de onde a mão está quando o golpe começa (a guarda de cada arma: a pegada é a nossa), anda o que o
    ///    pulso do Mixamo anda desde o começo dele, vezes a amplitude da arma (<see cref="AmplitudeDo"/>), em braços (o
    ///    comprimento do braço de cada um), no espaço do corpo (ou da câmera, na primeira pessoa).
    ///  - A mão: vira como a do Mixamo pela forma dela (a direção dos dedos e a do lado da mão), e não pelos eixos dos
    ///    ossos (os do Mixamo e os nossos são outros); entra aos poucos na preparação e sai na volta.
    ///  - As de duas mãos (a lança e o bastão): a esquerda fica no cabo, onde estava em relação à direita no começo do golpe.
    ///  - O tempo: a preparação, o golpe e a volta do Mixamo apertados no tempo da arma: o acerto dele no impacto dela.
    /// O resto (o dano, o alcance, o impacto) é o de sempre.
    /// </summary>
    public static partial class GolpeDoMixamo
    {
        /// <summary>A chave do teste (o F1): desligada, o golpe de sempre.</summary>
        public static bool Ligado;

        /// <summary>
        /// Quanto do caminho do pulso do Mixamo (1: o dele, inteiro), por arma (19/11/2026, a pedido: "machado amplitude de
        /// 80% e tocha amplitude de 60%"; a faca, a lança e o bastão começam em 100, 80 e 70%).
        /// </summary>
        static readonly float[] _amplitudes = { 1f, 0.8f, 0.7f, 0.8f, 0.6f };   // faca, machado, bastão, lança, tocha

        public static float AmplitudeDo(Item i) => Itens.EhBranca(i) ? _amplitudes[i - Item.Faca] : 1f;

        /// <summary>Gira a amplitude da arma (40, 60, 80, 100%), para o botão do F1.</summary>
        public static void GirarAmplitude(Item i)
        {
            if (!Itens.EhBranca(i)) return;
            float a = _amplitudes[i - Item.Faca];
            _amplitudes[i - Item.Faca] = a >= 0.99f ? 0.4f : Mathf.Round((a + 0.2f) * 5f) / 5f;
        }

        // uma animação: os dados (12 números por quadro) e os quadros que marcam o golpe (de 0): o começo (na guarda), o alto
        // da preparação, o acerto e o fim (de volta à guarda)
        sealed class Clipe
        {
            public float[] dados;
            public float comeco, alto, acerto, fim;
            // a mão gira só o tanto que a do Mixamo gira desde o começo (a pegada dele é outra: a baioneta é segurada como um
            // rifle), em vez de copiar a forma dela; e quanto do caminho para cima vale (a estocada da baioneta sobe muito)
            public bool giroRelativo;
            public float subida = 1f;
            public int Quadros => dados.Length / 12;
        }

        static readonly Clipe Machadada = new Clipe { dados = DadosDoMachado, comeco = 3f, alto = 21f, acerto = 29f, fim = 66f };
        // (a faca: a mão sai da guarda e vai reta para a frente no quadro 12, fica esticada até o 27, recolhe e volta no 54)
        static readonly Clipe Estocada = new Clipe { dados = DadosDaFaca, comeco = 3f, alto = 9f, acerto = 27f, fim = 54f };
        // (a lança: recua até o 18, estoca para a frente e para cima até o 33 e volta no 60)
        static readonly Clipe Baioneta = new Clipe { dados = DadosDaLanca, comeco = 6f, alto = 18f, acerto = 33f, fim = 60f, giroRelativo = true, subida = 0.5f };
        // (o bastão: sobe por cima da cabeça até o 33, desce e acerta no 48, com ele deitado para a frente, e termina do lado
        // no 56; o resto é o 2º golpe)
        static readonly Clipe Clava = new Clipe { dados = DadosDoBastao, comeco = 2f, alto = 33f, acerto = 48f, fim = 56f };

        static Clipe ClipeDa(Item i) => i switch
        {
            Item.Machado or Item.Tocha => Machadada,
            Item.Faca => Estocada,
            Item.Lanca => Baioneta,
            Item.Bastao => Clava,
            _ => null,
        };

        /// <summary>As armas que usam um golpe do Mixamo no teste: todas as brancas.</summary>
        public static bool Usa(Item i) => ClipeDa(i) != null;

        /// <summary>O tempo do golpe inteiro na arma (s): 90% do intervalo entre um golpe e outro, até 0,8 s.</summary>
        static float Duracao(DadosDaBranca b) => Mathf.Min(0.8f, b.intervalo * 0.9f);

        // o quadro do Mixamo no tempo t do golpe: a preparação até 0,1 s antes do impacto, o golpe até o impacto, e a volta
        static float Quadro(Clipe c, float t, float impacto, float duracao)
        {
            float alto = Mathf.Max(0.06f, impacto - 0.1f);
            if (impacto <= alto + 0.02f) impacto = alto + 0.02f;
            if (t <= alto) return Mathf.Lerp(c.comeco, c.alto, t / alto);
            if (t <= impacto) return Mathf.Lerp(c.alto, c.acerto, (t - alto) / (impacto - alto));
            return Mathf.Lerp(c.acerto, c.fim, Mathf.Clamp01((t - impacto) / Mathf.Max(0.05f, duracao - impacto)));
        }

        static Vector3 Ler(Clipe c, float quadro, int k)
        {
            int n = c.Quadros;
            quadro = Mathf.Clamp(quadro, 0f, n - 1);
            int a = Mathf.FloorToInt(quadro), b = Mathf.Min(a + 1, n - 1);
            float f = quadro - a;
            var d = c.dados;
            var va = new Vector3(d[a * 12 + k], d[a * 12 + k + 1], d[a * 12 + k + 2]);
            var vb = new Vector3(d[b * 12 + k], d[b * 12 + k + 1], d[b * 12 + k + 2]);
            return Vector3.Lerp(va, vb, f);
        }

        /// <summary>
        /// Os braços (o do corpo ou o de primeira pessoa de uma arma): guarda a guarda quando o golpe começa e, até o fim dele,
        /// põe o braço direito no caminho do Mixamo; nas armas de duas mãos, a esquerda segue no cabo.
        /// </summary>
        public class Braco
        {
            readonly Transform _ombro, _cotovelo, _mao, _ombroE, _cotoveloE, _maoE;
            readonly Quaternion _formaNaMao;   // a forma da mão (dedos, lado) no espaço do osso da mão
            float _inicio = -99f;
            Item _item;
            Vector3 _pulsoNaGuarda;            // do ombro, em braços, no espaço do corpo
            Quaternion _maoNaGuarda;           // no espaço do corpo
            bool _duasMaos;                    // a esquerda no cabo (perto da direita) no começo do golpe
            Vector3 _esquerdaNaDireita;        // o pulso esquerdo no espaço da mão direita
            Quaternion _giroDaEsquerda;        // a mão esquerda no espaço da direita

            public bool Valido => _ombro != null && _cotovelo != null && _mao != null;

            public Braco(Transform raiz)
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
                    }
                if (!Valido || medio == null || indicador == null || minimo == null)
                {
                    _ombro = null;
                    return;
                }
                Vector3 dedos = _mao.InverseTransformPoint(medio.position).normalized;
                Vector3 lado = _mao.InverseTransformPoint(indicador.position) - _mao.InverseTransformPoint(minimo.position);
                _formaNaMao = Quaternion.LookRotation(dedos, Vector3.ProjectOnPlane(lado, dedos));
            }

            /// <summary>
            /// No LateUpdate, depois da animação: com o golpe de uma das armas do teste andando (o último ataque há menos que
            /// a duração dele), os braços no caminho do Mixamo. <paramref name="corpo"/>: os eixos do corpo (ou da câmera).
            /// </summary>
            public void Aplicar(Quaternion corpo, float ultimoAtaque, Item naMao)
            {
                if (!Ligado || !Valido) return;
                var b = Itens.Branca(naMao);
                var c = ClipeDa(naMao);
                if (b == null || c == null) { _inicio = -99f; return; }
                float braco = (_cotovelo.position - _ombro.position).magnitude + (_mao.position - _cotovelo.position).magnitude;
                if (braco < 0.05f) return;
                // um golpe novo: a guarda de agora (a pose da animação no começo dele)
                if (ultimoAtaque != _inicio || naMao != _item)
                {
                    if (Time.time - ultimoAtaque > 0.1f) return;   // (um ataque velho: ao trocar de arma, nada)
                    _inicio = ultimoAtaque;
                    _item = naMao;
                    var volta = Quaternion.Inverse(corpo);
                    _pulsoNaGuarda = volta * (_mao.position - _ombro.position) / braco;
                    _maoNaGuarda = volta * _mao.rotation;
                    // a esquerda no cabo: perto da direita e visível (na primeira pessoa do machado, o braço esquerdo some)
                    _duasMaos = _maoE != null && _ombroE != null && _cotoveloE != null && _ombroE.lossyScale.x > 0.01f
                                && (_maoE.position - _mao.position).magnitude < 0.5f * braco / 0.53f;
                    if (_duasMaos)
                    {
                        _esquerdaNaDireita = Quaternion.Inverse(_mao.rotation) * (_maoE.position - _mao.position);
                        _giroDaEsquerda = Quaternion.Inverse(_mao.rotation) * _maoE.rotation;
                    }
                }
                float duracao = Duracao(b), t = Time.time - _inicio;
                if (t < 0f || t > duracao) return;
                float q = Quadro(c, t, b.impacto, duracao);
                // o peso: inteiro no golpe, saindo nos últimos 0,12 s; a mão vira para a do Mixamo na preparação
                float peso = 1f - Mathf.SmoothStep(0f, 1f, (t - (duracao - 0.12f)) / 0.12f);
                float alto = Mathf.Max(0.06f, b.impacto - 0.1f);
                float giro = Mathf.SmoothStep(0f, 1f, t / alto) * (1f - Mathf.SmoothStep(0f, 1f, (t - b.impacto - 0.05f) / Mathf.Max(0.05f, duracao - b.impacto - 0.05f)));

                float amplitude = AmplitudeDo(naMao);
                Vector3 andou = (Ler(c, q, 0) - Ler(c, c.comeco, 0)) * amplitude;
                andou.y *= c.subida;
                Vector3 pulso = _ombro.position + corpo * ((_pulsoNaGuarda + andou) * braco);
                Vector3 dobra = corpo * Ler(c, q, 3);
                Vector3 dedos = corpo * Ler(c, q, 6), lado = corpo * Ler(c, q, 9);
                Quaternion doMixamo = Quaternion.LookRotation(dedos, Vector3.ProjectOnPlane(lado, dedos)) * Quaternion.Inverse(_formaNaMao);
                if (c.giroRelativo)
                {
                    // o giro da mão do Mixamo desde o começo, na guarda nossa
                    Vector3 dedos0 = corpo * Ler(c, c.comeco, 6), lado0 = corpo * Ler(c, c.comeco, 9);
                    Quaternion agora = Quaternion.LookRotation(dedos, Vector3.ProjectOnPlane(lado, dedos));
                    Quaternion antes = Quaternion.LookRotation(dedos0, Vector3.ProjectOnPlane(lado0, dedos0));
                    doMixamo = agora * Quaternion.Inverse(antes) * (corpo * _maoNaGuarda);
                }
                Quaternion mao = Quaternion.Slerp(corpo * _maoNaGuarda, doMixamo, giro * Mathf.Lerp(0.7f, 1f, amplitude));

                Vector3 pulsoAgora = _mao.position;
                Quaternion maoAgora = _mao.rotation;
                Vector3 pulsoEAgora = _duasMaos ? _maoE.position : Vector3.zero;
                Quaternion maoEAgora = _duasMaos ? _maoE.rotation : Quaternion.identity;
                DoisOssos(_ombro, _cotovelo, _mao, Vector3.Lerp(pulsoAgora, pulso, peso), dobra);
                Quaternion quero = Quaternion.Slerp(maoAgora, mao, peso);
                Torcer(_cotovelo, _mao, quero);
                _mao.rotation = quero;

                // a esquerda no cabo, onde estava em relação à direita no começo
                if (_duasMaos)
                {
                    Vector3 alvoE = _mao.position + _mao.rotation * _esquerdaNaDireita;
                    Quaternion giroE = _mao.rotation * _giroDaEsquerda;
                    Vector3 dobraE = _cotoveloE.position - (_ombroE.position + pulsoEAgora) * 0.5f;
                    DoisOssos(_ombroE, _cotoveloE, _maoE, Vector3.Lerp(pulsoEAgora, alvoE, peso), dobraE);
                    Quaternion queroE = Quaternion.Slerp(maoEAgora, giroE, peso);
                    Torcer(_cotoveloE, _maoE, queroE);
                    _maoE.rotation = queroE;
                }
            }

            // o braço de dois ossos até o pulso, o cotovelo para o lado da dobra (pelo caminho mais curto de cada osso)
            static void DoisOssos(Transform a, Transform b, Transform c, Vector3 alvo, Vector3 dobra)
            {
                Vector3 pa = a.position;
                float l1 = (b.position - pa).magnitude, l2 = (c.position - b.position).magnitude;
                if (l1 < 1e-4f || l2 < 1e-4f) return;
                Vector3 para = alvo - pa;
                float d = Mathf.Clamp(para.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
                Vector3 dir = para.sqrMagnitude > 1e-8f ? para.normalized : a.forward;
                float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
                float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
                Vector3 p = Vector3.ProjectOnPlane(dobra, dir);
                if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(Vector3.down, dir);
                p.Normalize();
                Vector3 cotovelo = pa + dir * x + p * h;
                a.rotation = Quaternion.FromToRotation(b.position - pa, cotovelo - pa) * a.rotation;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, pa + dir * d - b.position) * b.rotation;
            }

            // o antebraço gira em volta dele mesmo metade do que a mão vai girar (o pulso não fica torcido como um pano)
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
