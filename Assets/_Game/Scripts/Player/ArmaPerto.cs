using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A arma perto do rosto na primeira pessoa, em teste (19/11/2026, a pedido, com imagens de outros jogos: "as armas estão
    /// muito distantes do corpo na primeira pessoa... se a gente trouxer as armas mais para perto do rosto, colocando apenas
    /// a mão e o pulso esquerdo vindo de baixo para cima e realizando a pegada com os dedos na arma... isso não pode alterar
    /// a terceira pessoa... também não quero que dê apenas um zoom"). Com a chave do F1 (a aba Fauna) ligada, no rifle, na
    /// submetralhadora, na espingarda e na sniper, depois da animação dos braços de primeira pessoa (que só o dono vê: os
    /// outros veem o corpo, outro esqueleto):
    ///  - A arma (o osso gun) chega mais perto do olho (<see cref="Puxar"/>, a fração do caminho até a câmera), desce e vai
    ///    para o lado (<see cref="Baixar"/>, <see cref="Lado"/>); a animação (respirar, andar, o coice) continua por cima.
    ///  - A mão esquerda pega a arma por baixo, montada pelo formato dela (a malha da arma, medida uma vez: a largura e o fundo
    ///    a cada centímetro do cano): no ponto do cano em que a animação a põe (ela acompanha a bomba da espingarda), a palma
    ///    para cima, embaixo do guarda-mão, os dedos atravessados (<see cref="MaoAtravessada"/>) com os nós na beira direita,
    ///    fechando pela lateral, e o antebraço vindo de baixo (o pulso dobrado para trás: <see cref="PulsoDeBaixo"/>).
    ///  - A mão direita fica no cabo, como a animação a põe, e o antebraço dela não deixa o pulso dobrar mais do que
    ///    <see cref="PulsoDireito"/>.
    ///  - Os antebraços vêm primeiro (da mão para trás); o braço vai do cotovelo até o ombro, que escorrega para onde precisar
    ///    (ninguém vê o ombro na primeira pessoa), vindo de mais baixo (<see cref="Ombro"/>): nada força o pulso.
    ///  - Mirando (a mira de ponto, a luneta), tudo volta aos poucos ao de sempre (a mira continua alinhada); recarregando, a
    ///    mão esquerda volta à animação (ela vai ao pente).
    /// O campo de visão não muda (sem zoom).
    /// </summary>
    public static class ArmaPerto
    {
        /// <summary>A chave do teste (o F1): desligada, os braços de sempre.</summary>
        public static bool Ligado;
        /// <summary>A fração do caminho da arma até a câmera (0: onde está).</summary>
        public static float Puxar = 0.25f;
        /// <summary>Quanto a arma desce e vai para a direita (m, nos eixos da câmera).</summary>
        public static float Baixar = 0.03f, Lado = 0f;
        /// <summary>O ângulo dos dedos da mão esquerda com o cano (graus): 0, ao longo dele; 90, atravessados para a direita.</summary>
        public static float MaoAtravessada = 60f;
        /// <summary>O quanto o pulso esquerdo dobra para trás (graus): com ele, o antebraço vem de baixo.</summary>
        public static float PulsoDeBaixo = 40f;
        /// <summary>O quanto a palma esquerda vira para o lado esquerdo do guarda-mão (graus em volta do cano; 0: bem embaixo).</summary>
        public static float PalmaDeLado = 0f;
        /// <summary>Do meio da mão esquerda (o osso) ao fundo da arma (m): a espessura da palma.</summary>
        public static float FolgaDaPalma = 0.02f;
        /// <summary>O quanto os dedos da esquerda fecham a mais (graus em cada junta).</summary>
        public static float DedosFechando = 15f;
        /// <summary>O pulso direito (o do gatilho) não dobra mais do que isto (graus, do antebraço à mão).</summary>
        public static float PulsoDireito = 20f;
        /// <summary>De quanto mais baixo os braços vêm (m; e de trás, metade disso).</summary>
        public static float Ombro = 0.18f;

        // a mão direita aperta um pouco mais (graus nas duas juntas dos dedos)
        const float FechaDireita1 = 8f, FechaDireita2 = 10f;

        /// <summary>As armas do teste: as de duas mãos (o rifle, a submetralhadora, a espingarda e a sniper, e as de sucata).</summary>
        public static bool Usa(TipoDeArma t)
        {
            var r = Armas.Real(t);
            return r == TipoDeArma.Rifle || r == TipoDeArma.Submetralhadora || r == TipoDeArma.Espingarda || r == TipoDeArma.Sniper;
        }

        /// <summary>Os braços de primeira pessoa de uma arma: a arma, os dois braços, a boca (o cano) e o formato da arma.</summary>
        public class Bracos
        {
            readonly Transform _raiz, _arma, _boca;
            readonly Braco _direito, _esquerdo;
            float _peso, _pesoDaPegada;
            readonly Posicao _posDaArma = new Posicao();
            Perfil _perfil;
            bool _semPerfil;
            string _ultimaFatia = "";

            public bool Valido => _arma != null && _direito.Valido && _esquerdo.Valido;

            /// <summary>Para o teste: o formato medido e a última fatia em que a mão esquerda pegou.</summary>
            public string Resumo => (_perfil != null ? _perfil.Resumo : _semPerfil ? "sem o formato da arma" : "formato ainda não medido") + "; " + _ultimaFatia;

            public Bracos(Transform raiz)
            {
                _raiz = raiz;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "gun") _arma = t;
                    else if (t.name.StartsWith("Boca")) _boca = t;
                }
                _direito = new Braco(raiz, ".R");
                _esquerdo = new Braco(raiz, ".L");
            }

            /// <summary>
            /// No LateUpdate, depois da animação. <paramref name="camera"/>: a câmera dos braços; <paramref name="mirando"/>:
            /// mirando (volta ao de sempre); <paramref name="recarregando"/>: a esquerda fica com a animação (ela vai ao pente).
            /// </summary>
            public void Aplicar(Transform camera, bool mirando, bool recarregando)
            {
                float dt = Time.deltaTime;
                _peso = Mathf.MoveTowards(_peso, Ligado && !mirando ? 1f : 0f, dt / 0.15f);
                _pesoDaPegada = Mathf.MoveTowards(_pesoDaPegada, recarregando ? 0f : 1f, dt / 0.15f);
                if (_peso <= 0f || !Valido) return;
                float peso = Mathf.SmoothStep(0f, 1f, _peso);

                // a pose da animação (sem o que este código pôs no quadro anterior, se ela não regravar)
                var arma = _arma;
                _posDaArma.DaAnimacao(arma);
                _direito.DaAnimacao();
                _esquerdo.DaAnimacao();

                // o formato da arma: os pontos uma vez; o "para baixo" e as fatias quando ela está apontada para a frente
                if (!_semPerfil && (_perfil == null || !_perfil.Pronto))
                {
                    _perfil ??= Perfil.Da(_raiz, arma, _boca);
                    if (_perfil == null) _semPerfil = true;
                    else if (!recarregando) _perfil.Medir(arma, camera);
                }

                // as mãos na arma, como a animação as pôs, e os antebraços e os ombros
                Quaternion volta = Quaternion.Inverse(arma.rotation);
                Vector3 direitaNaArma = volta * (_direito.Mao.position - arma.position);
                Quaternion giroDireitaNaArma = volta * _direito.Mao.rotation;
                Vector3 esquerdaNaArma = volta * (_esquerdo.Mao.position - arma.position);
                Quaternion giroEsquerdaNaArma = volta * _esquerdo.Mao.rotation;
                Vector3 meioDaEsquerdaNaArma = esquerdaNaArma + giroEsquerdaNaArma * _esquerdo.MeioL;
                Vector3 antebracoDireito = _direito.Antebraco, antebracoEsquerdo = _esquerdo.Antebraco;
                Vector3 baixaOmbro = (camera.up * -Ombro + camera.forward * (-Ombro * 0.5f)) * peso;
                Vector3 ombroDireito = _direito.Ombro + baixaOmbro * 0.5f, ombroEsquerdo = _esquerdo.Ombro + baixaOmbro;

                // a arma no lugar novo: mais perto do olho, mais baixa e para o lado
                Vector3 desloca = (camera.position - arma.position) * Puxar + camera.up * -Baixar + camera.right * Lado;
                arma.position += desloca * peso;
                _posDaArma.Posta(arma);

                // a direita: no cabo, como a animação a pôs; o antebraço na linha da mão até sobrar o pulso de PulsoDireito
                Vector3 pulsoD = arma.position + arma.rotation * direitaNaArma;
                Quaternion giroD = arma.rotation * giroDireitaNaArma;
                Vector3 eixoD = _direito.EixoDaMao(giroD);
                float dobra = Vector3.Angle(antebracoDireito, eixoD);
                Vector3 retoD = dobra > PulsoDireito ? Vector3.Slerp(antebracoDireito, eixoD, (dobra - PulsoDireito) / dobra) : antebracoDireito;
                _direito.Por(pulsoD, giroD, Vector3.Slerp(antebracoDireito, retoD, peso), ombroDireito, FechaDireita1 * peso, FechaDireita2 * peso);

                // a esquerda: por baixo do guarda-mão (fora da recarga), montada pelo formato da arma
                Vector3 pulsoE = arma.position + arma.rotation * esquerdaNaArma;
                Quaternion giroE = arma.rotation * giroEsquerdaNaArma;
                Vector3 antebracoE = antebracoEsquerdo;
                float pegada = peso * Mathf.SmoothStep(0f, 1f, _pesoDaPegada);
                if (pegada > 0f && _perfil != null && _perfil.Pronto && PorBaixo(meioDaEsquerdaNaArma, out var pulso, out var giro, out var antebraco))
                {
                    pulsoE = Vector3.Lerp(pulsoE, pulso, pegada);
                    giroE = Quaternion.Slerp(giroE, giro, pegada);
                    antebracoE = Vector3.Slerp(antebracoE, antebraco, pegada);
                }
                _esquerdo.Por(pulsoE, giroE, antebracoE, ombroEsquerdo, DedosFechando * pegada, DedosFechando * pegada);
            }

            // a mão esquerda por baixo da arma, na fatia do cano em que está o meio da palma (no espaço da arma): o pulso, o
            // giro da mão e o antebraço (do cotovelo ao pulso), no mundo
            bool PorBaixo(Vector3 meioNaArma, out Vector3 pulso, out Quaternion giro, out Vector3 antebraco)
            {
                pulso = antebraco = Vector3.zero;
                giro = Quaternion.identity;
                var p = _perfil;
                float t = Vector3.Dot(meioNaArma, p.eixo);
                if (!p.Fatia(t, out float latMin, out float latMax, out float fundo)) { _ultimaFatia = $"fora da arma ({t * 100f:F0} cm)"; return false; }
                float a = MaoAtravessada * Mathf.Deg2Rad, b = PulsoDeBaixo * Mathf.Deg2Rad, f = PalmaDeLado * Mathf.Deg2Rad;
                // o guarda-mão como se fosse redondo (o meio acima do fundo pela metade da largura); a palma embaixo dele,
                // virada PalmaDeLado para a esquerda, olhando para o meio; os dedos atravessados para o lado de baixo da volta
                // (por baixo dele, para a direita)
                float meiaLargura = (latMax - latMin) * 0.5f;
                Vector3 centro = p.eixo * t + p.direita * ((latMin + latMax) * 0.5f) + p.baixo * (fundo - meiaLargura);
                Vector3 fora = p.baixo * Mathf.Cos(f) - p.direita * Mathf.Sin(f);
                Vector3 volta = p.direita * Mathf.Cos(f) + p.baixo * Mathf.Sin(f);
                Vector3 palma = -fora;
                Vector3 dedos = p.eixo * Mathf.Cos(a) + volta * Mathf.Sin(a);
                Vector3 meio = centro + fora * (meiaLargura + FolgaDaPalma);
                var q = _arma.rotation;
                giro = _esquerdo.Giro(q * dedos, q * palma);
                pulso = _arma.position + q * meio - giro * _esquerdo.MeioL;
                antebraco = (q * (dedos * Mathf.Cos(b) + palma * Mathf.Sin(b))).normalized;
                _ultimaFatia = $"a mão a {t * 100f:F0} cm, a arma ali com {(latMax - latMin) * 100f:F1} cm de largura";
                return true;
            }
        }

        /// <summary>
        /// O formato da arma (os pontos da malha que vão com o osso gun, a bomba e o ferrolho, no espaço da arma: o giro dela,
        /// na escala do mundo), o eixo comprido dela (para a boca) e, medidas com ela apontada para a frente, as fatias a cada
        /// centímetro do eixo: de onde a onde ela vai para os lados e até onde desce.
        /// </summary>
        sealed class Perfil
        {
            const float Passo = 0.01f, MeiaFatia = 0.015f;
            readonly List<Vector3> _pontos;
            public readonly Vector3 eixo;
            public Vector3 baixo, direita;
            public bool Pronto { get; private set; }
            public string Resumo { get; private set; }
            float _inicio;
            float[] _latMin, _latMax, _fundo;

            Perfil(List<Vector3> pontos, Vector3 eixo)
            {
                _pontos = pontos;
                this.eixo = eixo;
                Resumo = $"{pontos.Count} pontos da arma, sem medir";
            }

            public static Perfil Da(Transform raiz, Transform arma, Transform boca)
            {
                SkinnedMeshRenderer smr = null;
                foreach (var s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var malhaDele = s.sharedMesh;
                    if (malhaDele == null || !malhaDele.isReadable || s.bones == null) continue;
                    if (System.Array.IndexOf(s.bones, arma) >= 0) { smr = s; break; }
                }
                if (smr == null) return null;
                var malha = smr.sharedMesh;
                var ossos = smr.bones;
                var liga = malha.bindposes;
                if (liga.Length != ossos.Length) return null;
                var volta = Matrix4x4.TRS(arma.position, arma.rotation, Vector3.one).inverse;
                var paraArma = new Matrix4x4[ossos.Length];
                var usa = new bool[ossos.Length];
                for (int i = 0; i < ossos.Length; i++)
                {
                    var o = ossos[i];
                    usa[i] = o != null && (o == arma || o.name == "pump" || o.name == "slide");
                    if (usa[i]) paraArma[i] = volta * o.localToWorldMatrix * liga[i];
                }
                var vs = malha.vertices;
                var pesos = malha.boneWeights;
                if (pesos.Length != vs.Length) return null;
                var pontos = new List<Vector3>();
                Vector3 soma = Vector3.zero;
                for (int v = 0; v < vs.Length; v++)
                {
                    var w = pesos[v];
                    int b = w.boneIndex0;
                    if (w.weight0 < 0.5f || b < 0 || b >= usa.Length || !usa[b]) continue;
                    var p = paraArma[b].MultiplyPoint3x4(vs[v]);
                    pontos.Add(p);
                    soma += p;
                }
                if (pontos.Count < 30) return null;
                Vector3 c = soma / pontos.Count;
                // o eixo comprido (o maior dos eixos principais, pela potência da covariância), para o lado da boca
                double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
                foreach (var p in pontos)
                {
                    Vector3 d = p - c;
                    xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                    yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
                }
                Vector3 bocaNaArma = boca != null ? volta.MultiplyPoint3x4(boca.position) : c + Vector3.forward;
                Vector3 e = bocaNaArma - c;
                if (e.sqrMagnitude < 1e-8f) e = Vector3.forward;
                e.Normalize();
                for (int k = 0; k < 60; k++)
                {
                    var n = new Vector3((float)(xx * e.x + xy * e.y + xz * e.z), (float)(xy * e.x + yy * e.y + yz * e.z), (float)(xz * e.x + yz * e.y + zz * e.z));
                    if (n.sqrMagnitude < 1e-12f) break;
                    e = n.normalized;
                }
                if (Vector3.Dot(bocaNaArma - c, e) < 0f) e = -e;
                return new Perfil(pontos, e);
            }

            /// <summary>
            /// O "para baixo" e o "para a direita" da arma (os da câmera, com ela apontada para a frente: senão, fica para outro
            /// quadro) e as fatias.
            /// </summary>
            public void Medir(Transform arma, Transform camera)
            {
                Vector3 eixoNoMundo = arma.rotation * eixo;
                if (Vector3.Angle(eixoNoMundo, camera.forward) > 35f) return;   // (entrando na tela, ou de lado)
                var volta = Quaternion.Inverse(arma.rotation);
                Vector3 b = Vector3.ProjectOnPlane(-camera.up, eixoNoMundo);
                if (b.sqrMagnitude < 1e-6f) return;
                baixo = Vector3.ProjectOnPlane(volta * b, eixo).normalized;
                direita = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(volta * camera.right, eixo), baixo).normalized;
                if (direita.sqrMagnitude < 0.5f) return;
                float tMin = float.MaxValue, tMax = float.MinValue;
                foreach (var p in _pontos)
                {
                    float t = Vector3.Dot(p, eixo);
                    if (t < tMin) tMin = t;
                    if (t > tMax) tMax = t;
                }
                int n = Mathf.Clamp(Mathf.CeilToInt((tMax - tMin) / Passo) + 1, 1, 1000);
                _latMin = new float[n];
                _latMax = new float[n];
                _fundo = new float[n];
                for (int i = 0; i < n; i++) { _latMin[i] = float.MaxValue; _latMax[i] = float.MinValue; _fundo[i] = float.MinValue; }
                foreach (var p in _pontos)
                {
                    int i = Mathf.Clamp((int)((Vector3.Dot(p, eixo) - tMin) / Passo), 0, n - 1);
                    float lat = Vector3.Dot(p, direita), desce = Vector3.Dot(p, baixo);
                    if (lat < _latMin[i]) _latMin[i] = lat;
                    if (lat > _latMax[i]) _latMax[i] = lat;
                    if (desce > _fundo[i]) _fundo[i] = desce;
                }
                _inicio = tMin;
                Pronto = true;
                Resumo = $"{_pontos.Count} pontos da arma, {(tMax - tMin) * 100f:F0} cm de comprimento";
            }

            /// <summary>A fatia em volta de <paramref name="t"/> (no eixo): de onde a onde vai para os lados e até onde desce.</summary>
            public bool Fatia(float t, out float latMin, out float latMax, out float fundo)
            {
                latMin = float.MaxValue; latMax = float.MinValue; fundo = float.MinValue;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((t - MeiaFatia - _inicio) / Passo));
                int i1 = Mathf.Min(_fundo.Length - 1, Mathf.FloorToInt((t + MeiaFatia - _inicio) / Passo));
                for (int i = i0; i <= i1; i++)
                {
                    if (_latMax[i] < _latMin[i]) continue;
                    latMin = Mathf.Min(latMin, _latMin[i]);
                    latMax = Mathf.Max(latMax, _latMax[i]);
                    fundo = Mathf.Max(fundo, _fundo[i]);
                }
                return latMax >= latMin;
            }
        }

        /// <summary>
        /// A posição local que a animação deu a um osso: se ela não a regravar no quadro (a curva constante), o osso ainda
        /// está onde este código o pôs, e ela volta à da animação antes de mudar de novo (o deslocamento não se soma).
        /// </summary>
        sealed class Posicao
        {
            Vector3 _daAnimacao, _posta;
            bool _tem;

            public void DaAnimacao(Transform t)
            {
                Vector3 agora = t.localPosition;
                if (_tem && (agora - _posta).sqrMagnitude < 1e-12f) t.localPosition = _daAnimacao;
                else _daAnimacao = agora;
            }

            public void Posta(Transform t)
            {
                _posta = t.localPosition;
                _tem = true;
            }
        }

        sealed class Braco
        {
            readonly Transform _ombro, _cotovelo;
            readonly Posicao _posDoOmbro = new Posicao();
            readonly Transform[] _dedos1 = new Transform[4], _dedos2 = new Transform[4];
            readonly bool _direito, _temForma;
            // a forma da mão, no espaço dela: para onde vão os dedos (do pulso ao nó do dedo do meio) e para onde olha a palma
            readonly Vector3 _dedosL, _palmaL;
            public Transform Mao { get; }
            /// <summary>Do pulso ao meio da palma (0,6 do caminho até o nó do dedo do meio), no espaço da mão.</summary>
            public Vector3 MeioL { get; }
            /// <summary>Do pulso ao nó do dedo do meio (m).</summary>
            public float AteOsNos { get; }
            public bool Valido => _ombro != null && _cotovelo != null && Mao != null && _temForma;
            /// <summary>Do cotovelo ao pulso (a direção), como está.</summary>
            public Vector3 Antebraco => (Mao.position - _cotovelo.position).normalized;
            public Vector3 Ombro => _ombro.position;

            public Braco(Transform raiz, string lado)
            {
                _direito = lado == ".R";
                string[] nomes = { "index", "middle", "ring", "pinky" };
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "upper_arm" + lado) _ombro = t;
                    else if (t.name == "forearm" + lado) _cotovelo = t;
                    else if (t.name == "hand" + lado) Mao = t;
                    for (int i = 0; i < 4; i++)
                    {
                        if (t.name == nomes[i] + "1" + lado) _dedos1[i] = t;
                        else if (t.name == nomes[i] + "2" + lado) _dedos2[i] = t;
                    }
                }
                if (Mao == null || _dedos1[0] == null || _dedos1[1] == null || _dedos1[3] == null) return;
                Vector3 dedos = _dedos1[1].position - Mao.position;
                AteOsNos = dedos.magnitude;
                if (AteOsNos < 1e-4f) return;
                dedos /= AteOsNos;
                Vector3 ladoDaMao = Vector3.ProjectOnPlane(_dedos1[0].position - _dedos1[3].position, dedos);
                if (ladoDaMao.sqrMagnitude < 1e-10f) return;
                Vector3 c = Vector3.Cross(ladoDaMao.normalized, dedos);
                var volta = Quaternion.Inverse(Mao.rotation);
                _dedosL = volta * dedos;
                _palmaL = volta * (_direito ? -c : c);
                MeioL = volta * (dedos * (AteOsNos * 0.6f));
                _temForma = true;
            }

            /// <summary>O ombro volta à posição da animação, se ela não a regravou (antes de ler a pose do quadro).</summary>
            public void DaAnimacao() => _posDoOmbro.DaAnimacao(_ombro);

            /// <summary>Para onde vão os dedos com a mão neste giro.</summary>
            public Vector3 EixoDaMao(Quaternion giro) => giro * _dedosL;

            /// <summary>O giro da mão com os dedos para <paramref name="dedos"/> e a palma para <paramref name="palma"/> (no mundo).</summary>
            public Quaternion Giro(Vector3 dedos, Vector3 palma)
            {
                dedos = Vector3.ProjectOnPlane(dedos, palma);
                if (dedos.sqrMagnitude < 1e-8f || palma.sqrMagnitude < 1e-8f) return Mao.rotation;
                return Quaternion.LookRotation(dedos.normalized, palma.normalized) * Quaternion.Inverse(Quaternion.LookRotation(_dedosL, _palmaL));
            }

            /// <summary>
            /// O braço da mão para trás: o pulso em <paramref name="pulso"/> com o giro <paramref name="giro"/>, o antebraço na
            /// direção <paramref name="antebraco"/> (do cotovelo ao pulso) e o braço do cotovelo para o lado de
            /// <paramref name="ombroDeOnde"/>, com o ombro onde o comprimento do braço mandar; e os dedos fecham um pouco.
            /// </summary>
            public void Por(Vector3 pulso, Quaternion giro, Vector3 antebraco, Vector3 ombroDeOnde, float fecha1, float fecha2)
            {
                if (_ombro.lossyScale.x < 0.01f) return;   // (o braço escondido)
                float l1 = (_cotovelo.position - _ombro.position).magnitude, l2 = (Mao.position - _cotovelo.position).magnitude;
                if (l1 < 1e-4f || l2 < 1e-4f || antebraco.sqrMagnitude < 1e-8f) return;
                Vector3 cotovelo = pulso - antebraco.normalized * l2;
                Vector3 braco = cotovelo - ombroDeOnde;
                if (braco.sqrMagnitude < 1e-8f) braco = _cotovelo.position - _ombro.position;
                Vector3 ombro = cotovelo - braco.normalized * l1;
                _ombro.position = ombro;
                _posDoOmbro.Posta(_ombro);
                _ombro.rotation = Quaternion.FromToRotation(_cotovelo.position - ombro, cotovelo - ombro) * _ombro.rotation;
                _cotovelo.rotation = Quaternion.FromToRotation(Mao.position - _cotovelo.position, pulso - _cotovelo.position) * _cotovelo.rotation;
                Torcer(giro);
                Mao.rotation = giro;
                FecharDedos(fecha1, fecha2);
            }

            // o antebraço gira em volta dele mesmo metade do que falta para a mão (o pulso não fica torcido como um pano)
            void Torcer(Quaternion quero)
            {
                Vector3 eixo = Mao.position - _cotovelo.position;
                if (eixo.sqrMagnitude < 1e-8f) return;
                eixo.Normalize();
                Vector3 agora = Vector3.ProjectOnPlane(Mao.rotation * Vector3.forward, eixo);
                Vector3 depois = Vector3.ProjectOnPlane(quero * Vector3.forward, eixo);
                if (agora.sqrMagnitude < 1e-6f || depois.sqrMagnitude < 1e-6f) return;
                float angulo = Mathf.Clamp(Vector3.SignedAngle(agora, depois, eixo) * 0.5f, -80f, 80f);
                _cotovelo.rotation = Quaternion.AngleAxis(angulo, eixo) * _cotovelo.rotation;
            }

            // os dedos fecham mais (em volta da linha dos nós, para a palma): a1 na primeira junta, a2 na segunda
            void FecharDedos(float a1, float a2)
            {
                if ((a1 == 0f && a2 == 0f) || _dedos1[1] == null || _dedos1[0] == null || _dedos1[3] == null) return;
                Vector3 dedos = (_dedos1[1].position - Mao.position).normalized;
                Vector3 lado = Vector3.ProjectOnPlane(_dedos1[0].position - _dedos1[3].position, dedos).normalized;
                Vector3 c = Vector3.Cross(lado, dedos);
                Vector3 palma = _direito ? -c : c;
                Vector3 eixo = Vector3.Cross(dedos, palma);
                if (eixo.sqrMagnitude < 1e-8f) return;
                eixo.Normalize();
                for (int i = 0; i < 4; i++)
                {
                    if (_dedos1[i] != null) _dedos1[i].rotation = Quaternion.AngleAxis(a1, eixo) * _dedos1[i].rotation;
                    if (_dedos2[i] != null) _dedos2[i].rotation = Quaternion.AngleAxis(a2, eixo) * _dedos2[i].rotation;
                }
            }
        }
    }
}
