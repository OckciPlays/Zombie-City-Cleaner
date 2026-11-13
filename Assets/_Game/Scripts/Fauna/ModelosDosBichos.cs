using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O corpo de um bicho na cena: as juntas que a animação mexe (<see cref="AnimacaoDoBicho"/>) e os colisores que o tiro
    /// acha (<see cref="ParteDoBicho"/>). Sai do modelo pronto (o Resources/Fauna/&lt;Especie&gt;_&lt;Variante&gt;, hoje só o
    /// veado, do Blender/Fauna/familia.py) ou das caixas (os modelos básicos de todos os bichos, a pedido do Pedro em
    /// 12/11/2026: "pode ser feito em quadrados bem básicos, apenas para diferenciar um pouco ... depois eu vou trocar todos
    /// por outros modelos").
    /// </summary>
    public sealed class ModeloDoBicho
    {
        /// <summary>Uma perna: a junta de cima (o ombro, o quadril), a de baixo (o joelho, o jarrete) e se é de trás.</summary>
        public struct Perna
        {
            public Transform cima, baixo;
            public bool traseira;
        }

        public Transform raiz, corpo, pescoco, cabeca;
        public readonly List<Perna> pernas = new List<Perna>();
        public readonly List<Transform> cauda = new List<Transform>();
        public readonly List<Transform> orelhas = new List<Transform>();
        public readonly List<Transform> asas = new List<Transform>();
        /// <summary>Os gomos da cobra (e o corpo do peixe), do primeiro ao último.</summary>
        public readonly List<Transform> gomos = new List<Transform>();
        /// <summary>A altura do lombo (m), para a mira e os sentidos.</summary>
        public float altura;
        /// <summary>O comprimento (m).</summary>
        public float comprimento;
    }

    /// <summary>Um colisor do bicho: o tiro e o golpe acham o bicho por ele (a cabeça vale mais).</summary>
    public sealed class ParteDoBicho : MonoBehaviour
    {
        public Bicho bicho;
        public bool cabeca;
    }

    public static class ModelosDosBichos
    {
        /// <summary>A camada dos bichos (ProjectSettings/TagManager): o tiro acha, o NavMesh e a vista dos zumbis não.</summary>
        public const int Camada = 14;

        static Mesh _cubo;
        static readonly Dictionary<Color32, Material> _materiais = new Dictionary<Color32, Material>();
        static readonly Dictionary<string, GameObject> _prontos = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar()
        {
            _materiais.Clear();
            _prontos.Clear();
            _cubo = null;
        }

        /// <summary>Monta o corpo do bicho como filho de <paramref name="pai"/> (na origem dele, olhando para +z).</summary>
        /// <summary>
        /// Uma caixa solta, sem colisor (a etapa 3: o galho na boca do castor, o peixe nas garras da águia, a toca, o toco
        /// roído e o ninho): o meio e o tamanho no espaço do pai, a cor e o giro.
        /// </summary>
        public static Transform CaixaSolta(Transform pai, Vector3 meio, Vector3 tamanho, Color cor, Vector3 giro = default)
        {
            var go = new GameObject("Caixa");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = meio;
            go.transform.localRotation = Quaternion.Euler(giro);
            go.transform.localScale = tamanho;
            go.AddComponent<MeshFilter>().sharedMesh = Cubo();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material(cor);
            r.shadowCastingMode = tamanho.magnitude < 0.04f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        public static ModeloDoBicho Montar(Especie e, Variante v, Transform pai, int semente)
        {
            var m = DoModeloPronto(e, v, pai) ?? DasCaixas(e, v, pai, semente);
            foreach (var t in m.raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Camada;
            return m;
        }

        // ------------------------------------------------------------------ o modelo pronto (o veado)

        static ModeloDoBicho DoModeloPronto(Especie e, Variante v, Transform pai)
        {
            string nome = NomeDoPronto(e, v);
            if (nome == null) return null;
            if (!_prontos.TryGetValue(nome, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Fauna/" + nome);
                _prontos[nome] = prefab;
            }
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pai, false);
            go.name = nome;
            var m = new ModeloDoBicho { raiz = go.transform };
            var ossos = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) ossos[t.name] = t;
            Transform O(string n) => ossos.TryGetValue(n, out var t) ? t : null;
            m.corpo = O("Quadril");
            m.pescoco = O("Pescoco_1");
            m.cabeca = O("Cabeca");
            foreach (var (cima, baixo, tras) in new[] { ("Braco.L", "Antebraco.L", false), ("Braco.R", "Antebraco.R", false),
                                                         ("Coxa.L", "Perna.L", true), ("Coxa.R", "Perna.R", true) })
                if (O(cima) != null) m.pernas.Add(new ModeloDoBicho.Perna { cima = O(cima), baixo = O(baixo), traseira = tras });
            foreach (var n in new[] { "Cauda_1", "Cauda_2" }) if (O(n) != null) m.cauda.Add(O(n));
            foreach (var n in new[] { "Orelha.L", "Orelha.R" }) if (O(n) != null) m.orelhas.Add(O(n));
            if (O("Pescoco_2") != null) m.gomos.Add(O("Pescoco_2"));   // o pescoço de cima acompanha o de baixo
            var d = Especies.De(e);
            float k = Especies.Escala(e, v);
            m.altura = d.altura * k;
            m.comprimento = d.comprimento * k;
            // os colisores: o corpo (na raiz) e a cabeça (presa no osso dela)
            var corpo = Caixote(go.transform, new Vector3(0.36f, 0.48f, 1.2f) * k, new Vector3(0f, 0.78f * k, -0.02f * k));
            corpo.gameObject.AddComponent<ParteDoBicho>();
            if (m.cabeca != null)
            {
                var cab = Caixote(m.cabeca, new Vector3(0.18f, 0.2f, 0.32f) * k, Vector3.zero);
                cab.transform.position = m.cabeca.position + go.transform.forward * 0.1f * k;
                cab.transform.rotation = go.transform.rotation;
                cab.gameObject.AddComponent<ParteDoBicho>().cabeca = true;
            }
            return m;
        }

        static string NomeDoPronto(Especie e, Variante v) => e switch
        {
            Especie.Veado => v switch { Variante.Femea => "Cervo_Femea", Variante.Filhote => "Cervo_Filhote", _ => "Cervo_Macho" },
            _ => null,
        };

        static BoxCollider Caixote(Transform pai, Vector3 tamanho, Vector3 local)
        {
            var go = new GameObject("Colisor");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = local;
            var c = go.AddComponent<BoxCollider>();
            c.size = tamanho;
            return c;
        }

        // ------------------------------------------------------------------ as caixas

        sealed class Obra
        {
            public ModeloDoBicho m;
            public System.Random sorte;
            public Color tom;

            public Transform Junta(string nome, Transform pai, Vector3 local, Vector3 giro = default)
            {
                var t = new GameObject(nome).transform;
                t.SetParent(pai, false);
                t.localPosition = local;
                t.localRotation = Quaternion.Euler(giro);
                return t;
            }

            /// <summary>Uma caixa (o meio e o tamanho no espaço da junta), da cor (com o tom do bicho).</summary>
            public Transform Caixa(Transform pai, Vector3 meio, Vector3 tamanho, Color cor, Vector3 giro = default, bool tingir = true)
            {
                var go = new GameObject("Caixa");
                go.transform.SetParent(pai, false);
                go.transform.localPosition = meio;
                go.transform.localRotation = Quaternion.Euler(giro);
                go.transform.localScale = tamanho;
                go.AddComponent<MeshFilter>().sharedMesh = Cubo();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Material(tingir ? cor * tom : cor);
                r.shadowCastingMode = tamanho.magnitude < 0.04f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                return go.transform;
            }

            public BoxCollider Colisor(Transform pai, Vector3 meio, Vector3 tamanho, bool cabeca)
            {
                var go = new GameObject(cabeca ? "Colisor da cabeça" : "Colisor");
                go.transform.SetParent(pai, false);
                go.transform.localPosition = meio;
                var c = go.AddComponent<BoxCollider>();
                c.size = tamanho;
                go.AddComponent<ParteDoBicho>().cabeca = cabeca;
                return c;
            }
        }

        static ModeloDoBicho DasCaixas(Especie e, Variante v, Transform pai, int semente)
        {
            var d = Especies.De(e);
            var raiz = new GameObject(d.nome + " (caixas)").transform;
            raiz.SetParent(pai, false);
            var o = new Obra { m = new ModeloDoBicho { raiz = raiz }, sorte = new System.Random(semente) };
            float brilho = 0.92f + 0.16f * (float)o.sorte.NextDouble();
            o.tom = new Color(brilho, brilho * (0.97f + 0.06f * (float)o.sorte.NextDouble()), brilho, 1f);
            switch (e)
            {
                case Especie.Veado: Quadrupede(o, Q.Veado(v)); break;
                case Especie.Alce: Quadrupede(o, Q.Alce(v)); break;
                case Especie.Lobo: Quadrupede(o, Q.Lobo()); break;
                case Especie.Coiote: Quadrupede(o, Q.Coiote()); break;
                case Especie.UrsoNegro: Quadrupede(o, Q.Urso(false)); break;
                case Especie.UrsoPardo: Quadrupede(o, Q.Urso(true)); break;
                case Especie.Coelho: Quadrupede(o, Q.Coelho()); break;
                case Especie.Castor: Quadrupede(o, Q.Castor()); break;
                case Especie.Guaxinim: Quadrupede(o, Q.Guaxinim()); break;
                case Especie.Gamba: Quadrupede(o, Q.Gamba()); break;
                case Especie.Esquilo: Quadrupede(o, Q.Esquilo()); break;
                case Especie.Pato: Ave(o, A.Pato()); break;
                case Especie.Ganso: Ave(o, A.Ganso()); break;
                case Especie.Aguia: Ave(o, A.Aguia()); break;
                case Especie.Urubu: Ave(o, A.Urubu()); break;
                case Especie.Peru: Ave(o, A.Peru()); break;
                case Especie.Sapo: Sapo(o); break;
                case Especie.Cascavel: Cobra(o); break;
                case Especie.Salmao: Peixe(o); break;
            }
            // o filhote e a fêmea menores (a raiz em escala: as juntas e os colisores acompanham)
            float k = Especies.Escala(e, v);
            raiz.localScale = Vector3.one * k;
            o.m.altura *= k;
            o.m.comprimento *= k;
            return o.m;
        }

        // ------------------------------------------------------------------ os quadrúpedes

        /// <summary>As medidas de um quadrúpede de caixas (m) e as cores.</summary>
        sealed class Q
        {
            public float comp, alt, larg, lombo, perna = 0.06f;
            public float pescocoComp, pescocoGrosso, pescocoAngulo = 40f;
            public float cabecaComp, cabecaAlt, cabecaLarg, cabecaAngulo = -30f;
            public float focinhoComp, focinhoAlt, focinhoLarg;
            public float orelhaComp, orelhaLarg, orelhaAbre = 50f, orelhaSobe = 20f;
            public float caudaComp, caudaGrossa = 0.05f, caudaLarga, caudaAngulo = 60f;
            public float corcova;          // o alce e o urso-pardo: o cupim sobre os ombros
            public float trasMaior = 1f;   // o coelho: as pernas de trás maiores
            public Color corpo, barriga, pernas, cabeca, focinho, nariz = new Color(0.08f, 0.06f, 0.06f), orelha, orelhaDentro,
                         cauda, pata = new Color(0.12f, 0.09f, 0.07f);
            public System.Action<Obra, Transform, Transform, Transform> extra;   // (obra, corpo, cabeça, cauda)

            static Color C(float r, float g, float b) => new Color(r, g, b);

            public static Q Veado(Variante v)
            {
                var q = new Q
                {
                    comp = 1.05f, alt = 0.42f, larg = 0.32f, lombo = 0.97f, perna = 0.06f,
                    pescocoComp = 0.48f, pescocoGrosso = 0.16f, pescocoAngulo = 55f,
                    cabecaComp = 0.30f, cabecaAlt = 0.14f, cabecaLarg = 0.13f, cabecaAngulo = -45f,
                    focinhoComp = 0.14f, focinhoAlt = 0.09f, focinhoLarg = 0.08f,
                    orelhaComp = 0.17f, orelhaLarg = 0.09f, caudaComp = 0.2f, caudaLarga = 0.08f, caudaAngulo = 70f,
                    corpo = C(0.55f, 0.32f, 0.17f), barriga = C(0.88f, 0.80f, 0.66f), pernas = C(0.50f, 0.30f, 0.16f),
                    cabeca = C(0.52f, 0.30f, 0.16f), focinho = C(0.80f, 0.64f, 0.45f), orelha = C(0.50f, 0.30f, 0.17f),
                    orelhaDentro = C(0.82f, 0.68f, 0.52f), cauda = C(0.90f, 0.86f, 0.78f),
                };
                if (v == Variante.Filhote)
                {
                    q.corpo = C(0.66f, 0.40f, 0.21f);
                    q.extra = (o, corpo, cab, cau) =>
                    {
                        for (int i = 0; i < 14; i++)   // as pintas brancas
                        {
                            float z = -0.42f + 0.065f * i, lado = (i % 2 == 0 ? 1 : -1) * 0.165f;
                            o.Caixa(corpo, new Vector3(lado, 0.12f + 0.04f * (i % 3), z), new Vector3(0.01f, 0.04f, 0.04f), C(0.94f, 0.9f, 0.82f), tingir: false);
                        }
                    };
                }
                else if (v != Variante.Femea)
                    q.extra = (o, corpo, cab, cau) => Galhada(o, cab, 0.30f, C(0.68f, 0.56f, 0.40f), false);
                return q;
            }

            public static Q Alce(Variante v)
            {
                var q = new Q
                {
                    comp = 1.7f, alt = 0.85f, larg = 0.7f, lombo = 1.85f, perna = 0.1f, corcova = 0.2f,
                    pescocoComp = 0.45f, pescocoGrosso = 0.34f, pescocoAngulo = 25f,
                    cabecaComp = 0.55f, cabecaAlt = 0.26f, cabecaLarg = 0.24f, cabecaAngulo = -50f,
                    focinhoComp = 0.30f, focinhoAlt = 0.24f, focinhoLarg = 0.22f,
                    orelhaComp = 0.22f, orelhaLarg = 0.11f, caudaComp = 0.12f, caudaLarga = 0.08f, caudaAngulo = 80f,
                    corpo = C(0.22f, 0.15f, 0.10f), barriga = C(0.18f, 0.13f, 0.09f), pernas = C(0.52f, 0.46f, 0.38f),
                    cabeca = C(0.26f, 0.18f, 0.12f), focinho = C(0.30f, 0.22f, 0.15f), orelha = C(0.25f, 0.18f, 0.12f),
                    orelhaDentro = C(0.45f, 0.36f, 0.28f), cauda = C(0.2f, 0.14f, 0.1f),
                };
                q.extra = (o, corpo, cab, cau) =>
                {
                    // a barbela embaixo do pescoço
                    o.Caixa(cab, new Vector3(0f, -0.2f, 0.05f), new Vector3(0.06f, 0.22f, 0.08f), C(0.2f, 0.14f, 0.1f));
                    if (v == Variante.Macho || v == Variante.Adulto) Galhada(o, cab, 0.42f, C(0.62f, 0.52f, 0.38f), true);
                };
                return q;
            }

            public static Q Lobo() => new Q
            {
                comp = 0.95f, alt = 0.34f, larg = 0.28f, lombo = 0.80f, perna = 0.06f,
                pescocoComp = 0.30f, pescocoGrosso = 0.20f, pescocoAngulo = 30f,
                cabecaComp = 0.24f, cabecaAlt = 0.15f, cabecaLarg = 0.16f, cabecaAngulo = -25f,
                focinhoComp = 0.14f, focinhoAlt = 0.08f, focinhoLarg = 0.08f,
                orelhaComp = 0.09f, orelhaLarg = 0.06f, orelhaAbre = 15f, orelhaSobe = 70f,
                caudaComp = 0.40f, caudaGrossa = 0.09f, caudaAngulo = 35f,
                corpo = C(0.46f, 0.44f, 0.41f), barriga = C(0.74f, 0.71f, 0.66f), pernas = C(0.56f, 0.53f, 0.48f),
                cabeca = C(0.48f, 0.46f, 0.42f), focinho = C(0.70f, 0.67f, 0.62f), orelha = C(0.38f, 0.36f, 0.33f),
                orelhaDentro = C(0.75f, 0.70f, 0.64f), cauda = C(0.40f, 0.38f, 0.35f),
            };

            public static Q Coiote()
            {
                var q = Lobo();
                q.comp = 0.80f; q.alt = 0.28f; q.larg = 0.22f; q.lombo = 0.60f; q.perna = 0.045f;
                q.pescocoGrosso = 0.15f; q.cabecaComp = 0.21f; q.cabecaAlt = 0.12f; q.cabecaLarg = 0.13f;
                q.orelhaComp = 0.10f; q.caudaComp = 0.33f; q.caudaGrossa = 0.08f; q.caudaAngulo = 55f;
                q.corpo = C(0.60f, 0.48f, 0.34f); q.pernas = C(0.66f, 0.54f, 0.38f); q.cabeca = C(0.62f, 0.50f, 0.36f);
                q.barriga = C(0.82f, 0.76f, 0.66f); q.cauda = C(0.5f, 0.42f, 0.32f);
                return q;
            }

            public static Q Urso(bool pardo)
            {
                float k = pardo ? 1.22f : 1f;
                var q = new Q
                {
                    comp = 1.15f * k, alt = 0.62f * k, larg = 0.6f * k, lombo = 0.95f * k, perna = 0.13f * k, corcova = pardo ? 0.14f : 0f,
                    pescocoComp = 0.22f * k, pescocoGrosso = 0.36f * k, pescocoAngulo = 10f,
                    cabecaComp = 0.30f * k, cabecaAlt = 0.25f * k, cabecaLarg = 0.28f * k, cabecaAngulo = -15f,
                    focinhoComp = 0.13f * k, focinhoAlt = 0.11f * k, focinhoLarg = 0.12f * k,
                    orelhaComp = 0.07f * k, orelhaLarg = 0.07f * k, orelhaAbre = 30f, orelhaSobe = 60f,
                    caudaComp = 0.08f, caudaGrossa = 0.07f, caudaAngulo = 40f,
                    corpo = pardo ? C(0.34f, 0.23f, 0.13f) : C(0.08f, 0.07f, 0.07f),
                    barriga = pardo ? C(0.30f, 0.20f, 0.12f) : C(0.07f, 0.06f, 0.06f),
                    pernas = pardo ? C(0.28f, 0.19f, 0.11f) : C(0.06f, 0.05f, 0.05f),
                    cabeca = pardo ? C(0.38f, 0.27f, 0.16f) : C(0.09f, 0.08f, 0.08f),
                    focinho = pardo ? C(0.46f, 0.34f, 0.22f) : C(0.50f, 0.38f, 0.26f),
                    orelha = pardo ? C(0.30f, 0.2f, 0.12f) : C(0.07f, 0.06f, 0.06f), orelhaDentro = C(0.2f, 0.15f, 0.12f),
                    cauda = pardo ? C(0.30f, 0.2f, 0.12f) : C(0.07f, 0.06f, 0.06f), pata = C(0.06f, 0.05f, 0.05f),
                };
                return q;
            }

            public static Q Coelho()
            {
                var q = new Q
                {
                    comp = 0.28f, alt = 0.17f, larg = 0.15f, lombo = 0.25f, perna = 0.035f, trasMaior = 1.6f,
                    pescocoComp = 0.05f, pescocoGrosso = 0.09f, pescocoAngulo = 40f,
                    cabecaComp = 0.11f, cabecaAlt = 0.09f, cabecaLarg = 0.09f, cabecaAngulo = -10f,
                    focinhoComp = 0.04f, focinhoAlt = 0.05f, focinhoLarg = 0.05f,
                    orelhaComp = 0.11f, orelhaLarg = 0.035f, orelhaAbre = 10f, orelhaSobe = 80f,
                    caudaComp = 0.04f, caudaGrossa = 0.05f, caudaAngulo = -20f,
                    corpo = C(0.55f, 0.47f, 0.38f), barriga = C(0.85f, 0.82f, 0.76f), pernas = C(0.52f, 0.45f, 0.36f),
                    cabeca = C(0.56f, 0.48f, 0.39f), focinho = C(0.75f, 0.70f, 0.62f), orelha = C(0.50f, 0.43f, 0.35f),
                    orelhaDentro = C(0.85f, 0.70f, 0.66f), cauda = C(0.95f, 0.94f, 0.92f), pata = C(0.6f, 0.53f, 0.44f),
                };
                return q;
            }

            public static Q Castor()
            {
                var q = new Q
                {
                    comp = 0.55f, alt = 0.25f, larg = 0.28f, lombo = 0.30f, perna = 0.05f,
                    pescocoComp = 0.06f, pescocoGrosso = 0.17f, pescocoAngulo = 10f,
                    cabecaComp = 0.15f, cabecaAlt = 0.12f, cabecaLarg = 0.13f, cabecaAngulo = -10f,
                    focinhoComp = 0.05f, focinhoAlt = 0.07f, focinhoLarg = 0.08f,
                    orelhaComp = 0.025f, orelhaLarg = 0.025f, caudaComp = 0.30f, caudaGrossa = 0.03f, caudaLarga = 0.14f, caudaAngulo = 15f,
                    corpo = C(0.36f, 0.22f, 0.12f), barriga = C(0.32f, 0.2f, 0.11f), pernas = C(0.2f, 0.13f, 0.08f),
                    cabeca = C(0.38f, 0.24f, 0.13f), focinho = C(0.3f, 0.2f, 0.12f), orelha = C(0.3f, 0.2f, 0.12f),
                    orelhaDentro = C(0.3f, 0.2f, 0.12f), cauda = C(0.12f, 0.11f, 0.11f), pata = C(0.15f, 0.12f, 0.1f),
                };
                q.extra = (o, corpo, cab, cau) =>   // os dentes da frente, laranja
                    o.Caixa(cab, new Vector3(0f, -0.05f, 0.21f), new Vector3(0.03f, 0.03f, 0.012f), C(0.85f, 0.45f, 0.1f), tingir: false);
                return q;
            }

            public static Q Guaxinim()
            {
                var q = new Q
                {
                    comp = 0.45f, alt = 0.24f, larg = 0.22f, lombo = 0.32f, perna = 0.04f,
                    pescocoComp = 0.06f, pescocoGrosso = 0.13f, pescocoAngulo = 20f,
                    cabecaComp = 0.13f, cabecaAlt = 0.10f, cabecaLarg = 0.13f, cabecaAngulo = -15f,
                    focinhoComp = 0.06f, focinhoAlt = 0.05f, focinhoLarg = 0.05f,
                    orelhaComp = 0.045f, orelhaLarg = 0.04f, orelhaAbre = 30f, orelhaSobe = 60f,
                    caudaComp = 0.26f, caudaGrossa = 0.07f, caudaAngulo = 25f,
                    corpo = C(0.45f, 0.43f, 0.40f), barriga = C(0.55f, 0.53f, 0.50f), pernas = C(0.20f, 0.19f, 0.18f),
                    cabeca = C(0.62f, 0.60f, 0.57f), focinho = C(0.85f, 0.84f, 0.82f), orelha = C(0.3f, 0.29f, 0.28f),
                    orelhaDentro = C(0.85f, 0.84f, 0.82f), cauda = C(0.5f, 0.48f, 0.45f), pata = C(0.12f, 0.11f, 0.1f),
                };
                q.extra = (o, corpo, cab, cau) =>
                {
                    o.Caixa(cab, new Vector3(0f, 0.015f, 0.10f), new Vector3(0.135f, 0.035f, 0.05f), C(0.08f, 0.08f, 0.08f), tingir: false);   // a máscara
                    for (int i = 0; i < 4; i++)   // os anéis da cauda
                        o.Caixa(cau, new Vector3(0f, 0f, -0.05f - 0.06f * i), new Vector3(0.075f, 0.075f, 0.025f), C(0.12f, 0.11f, 0.1f), tingir: false);
                };
                return q;
            }

            public static Q Gamba()
            {
                var q = new Q
                {
                    comp = 0.38f, alt = 0.2f, larg = 0.2f, lombo = 0.25f, perna = 0.035f,
                    pescocoComp = 0.05f, pescocoGrosso = 0.11f, pescocoAngulo = 15f,
                    cabecaComp = 0.11f, cabecaAlt = 0.08f, cabecaLarg = 0.09f, cabecaAngulo = -20f,
                    focinhoComp = 0.05f, focinhoAlt = 0.04f, focinhoLarg = 0.04f,
                    orelhaComp = 0.025f, orelhaLarg = 0.03f, caudaComp = 0.3f, caudaGrossa = 0.12f, caudaAngulo = -50f,
                    corpo = C(0.06f, 0.06f, 0.06f), barriga = C(0.05f, 0.05f, 0.05f), pernas = C(0.05f, 0.05f, 0.05f),
                    cabeca = C(0.07f, 0.07f, 0.07f), focinho = C(0.07f, 0.07f, 0.07f), orelha = C(0.06f, 0.06f, 0.06f),
                    orelhaDentro = C(0.06f, 0.06f, 0.06f), cauda = C(0.07f, 0.07f, 0.07f),
                };
                q.extra = (o, corpo, cab, cau) =>
                {
                    o.Caixa(corpo, new Vector3(0f, 0.1f, 0f), new Vector3(0.08f, 0.012f, 0.40f), C(0.95f, 0.95f, 0.93f), tingir: false);   // a listra
                    o.Caixa(cab, new Vector3(0f, 0.04f, 0.06f), new Vector3(0.02f, 0.01f, 0.1f), C(0.95f, 0.95f, 0.93f), tingir: false);
                    o.Caixa(cau, new Vector3(0f, 0.055f, -0.15f), new Vector3(0.05f, 0.02f, 0.26f), C(0.92f, 0.92f, 0.9f), tingir: false);
                };
                return q;
            }

            public static Q Esquilo()
            {
                var q = new Q
                {
                    comp = 0.14f, alt = 0.08f, larg = 0.07f, lombo = 0.13f, perna = 0.018f, trasMaior = 1.3f,
                    pescocoComp = 0.02f, pescocoGrosso = 0.05f, pescocoAngulo = 40f,
                    cabecaComp = 0.06f, cabecaAlt = 0.045f, cabecaLarg = 0.045f, cabecaAngulo = -10f,
                    focinhoComp = 0.02f, focinhoAlt = 0.025f, focinhoLarg = 0.025f,
                    orelhaComp = 0.02f, orelhaLarg = 0.012f, orelhaAbre = 15f, orelhaSobe = 75f,
                    caudaComp = 0.17f, caudaGrossa = 0.06f, caudaAngulo = -70f,
                    corpo = C(0.52f, 0.32f, 0.18f), barriga = C(0.85f, 0.82f, 0.74f), pernas = C(0.48f, 0.3f, 0.17f),
                    cabeca = C(0.54f, 0.34f, 0.19f), focinho = C(0.6f, 0.4f, 0.24f), orelha = C(0.5f, 0.3f, 0.17f),
                    orelhaDentro = C(0.6f, 0.45f, 0.35f), cauda = C(0.55f, 0.34f, 0.19f), pata = C(0.35f, 0.22f, 0.13f),
                };
                return q;
            }
        }

        /// <summary>A galhada de caixas: a haste para cima e para fora, com as pontas (o veado) ou a pá larga (o alce).</summary>
        static void Galhada(Obra o, Transform cab, float tamanho, Color cor, bool pa)
        {
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var haste = o.Junta("Galhada", cab, new Vector3(0.05f * lado * tamanho / 0.3f, 0.07f, -0.02f), new Vector3(-20f, 0f, -35f * lado));
                o.Caixa(haste, new Vector3(0f, tamanho * 0.5f, 0f), new Vector3(0.035f, tamanho, 0.035f) * (pa ? 1.3f : 1f), cor, tingir: false);
                if (pa)
                {
                    o.Caixa(haste, new Vector3(0.06f * lado, tamanho * 0.9f, 0.05f), new Vector3(0.24f, 0.03f, 0.32f), cor, new Vector3(0f, 0f, 20f * lado), tingir: false);
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                        o.Caixa(haste, new Vector3(0f, tamanho * (0.4f + 0.25f * k), 0.04f), new Vector3(0.022f, tamanho * 0.35f, 0.022f), cor,
                                new Vector3(35f, 0f, 0f), tingir: false);
                }
            }
        }

        static void Quadrupede(Obra o, Q q)
        {
            var m = o.m;
            m.altura = q.lombo;
            m.comprimento = q.comp + q.pescocoComp * 0.6f + q.cabecaComp;
            float yMeio = q.lombo - q.alt * 0.5f;
            float baixo = yMeio - q.alt * 0.5f;          // a barriga
            float pernaLen = baixo + q.alt * 0.12f;      // do ombro (um pouco dentro do corpo) ao chão
            m.corpo = o.Junta("Corpo", m.raiz, new Vector3(0f, yMeio, 0f));
            o.Caixa(m.corpo, Vector3.zero, new Vector3(q.larg, q.alt, q.comp), q.corpo);
            o.Caixa(m.corpo, new Vector3(0f, -q.alt * 0.32f, 0f), new Vector3(q.larg * 0.86f, q.alt * 0.42f, q.comp * 0.78f), q.barriga);
            if (q.corcova > 0f) o.Caixa(m.corpo, new Vector3(0f, q.alt * 0.5f, q.comp * 0.28f), new Vector3(q.larg * 0.6f, q.corcova, q.comp * 0.32f), q.corpo);
            o.Colisor(m.corpo, Vector3.zero, new Vector3(q.larg, q.alt, q.comp) * 1.05f, false);

            // o pescoço e a cabeça
            m.pescoco = o.Junta("Pescoco", m.corpo, new Vector3(0f, q.alt * 0.25f, q.comp * 0.44f), new Vector3(-q.pescocoAngulo, 0f, 0f));
            o.Caixa(m.pescoco, new Vector3(0f, 0f, q.pescocoComp * 0.5f), new Vector3(q.pescocoGrosso * 0.85f, q.pescocoGrosso, q.pescocoComp + q.pescocoGrosso * 0.4f), q.corpo);
            m.cabeca = o.Junta("Cabeca", m.pescoco, new Vector3(0f, 0f, q.pescocoComp), new Vector3(q.pescocoAngulo + q.cabecaAngulo * 0.4f, 0f, 0f));
            o.Caixa(m.cabeca, new Vector3(0f, 0f, q.cabecaComp * 0.25f), new Vector3(q.cabecaLarg, q.cabecaAlt, q.cabecaComp * 0.75f), q.cabeca);
            o.Caixa(m.cabeca, new Vector3(0f, -q.cabecaAlt * 0.12f, q.cabecaComp * 0.62f + q.focinhoComp * 0.3f),
                    new Vector3(q.focinhoLarg, q.focinhoAlt, q.focinhoComp), q.focinho);
            o.Caixa(m.cabeca, new Vector3(0f, -q.cabecaAlt * 0.05f, q.cabecaComp * 0.62f + q.focinhoComp * 0.82f),
                    new Vector3(q.focinhoLarg * 0.7f, q.focinhoAlt * 0.55f, q.focinhoComp * 0.12f), q.nariz, tingir: false);
            for (int lado = -1; lado <= 1; lado += 2)
                o.Caixa(m.cabeca, new Vector3(q.cabecaLarg * 0.5f * lado, q.cabecaAlt * 0.18f, q.cabecaComp * 0.3f),
                        new Vector3(0.012f, q.cabecaAlt * 0.22f, q.cabecaAlt * 0.3f), new Color(0.03f, 0.02f, 0.02f), tingir: false);
            o.Colisor(m.cabeca, new Vector3(0f, 0f, q.cabecaComp * 0.4f), new Vector3(q.cabecaLarg, q.cabecaAlt, q.cabecaComp + q.focinhoComp) * 1.1f, true);
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var orelha = o.Junta(lado < 0 ? "Orelha.R" : "Orelha.L", m.cabeca, new Vector3(q.cabecaLarg * 0.38f * lado, q.cabecaAlt * 0.45f, -q.cabecaComp * 0.05f),
                                     new Vector3(-q.orelhaSobe * 0.3f, 0f, -lado * (90f - q.orelhaSobe)));
                o.Caixa(orelha, new Vector3(0f, q.orelhaComp * 0.5f, 0f), new Vector3(q.orelhaLarg, q.orelhaComp, q.orelhaLarg * 0.25f), q.orelha);
                o.Caixa(orelha, new Vector3(0f, q.orelhaComp * 0.5f, q.orelhaLarg * 0.1f), new Vector3(q.orelhaLarg * 0.7f, q.orelhaComp * 0.8f, q.orelhaLarg * 0.08f), q.orelhaDentro);
                m.orelhas.Add(orelha);
            }

            // a cauda
            var cauda = o.Junta("Cauda", m.corpo, new Vector3(0f, q.alt * 0.3f, -q.comp * 0.5f), new Vector3(q.caudaAngulo, 0f, 0f));
            float largura = q.caudaLarga > 0f ? q.caudaLarga : q.caudaGrossa;
            o.Caixa(cauda, new Vector3(0f, 0f, -q.caudaComp * 0.5f), new Vector3(largura, q.caudaGrossa, q.caudaComp), q.cauda);
            m.cauda.Add(cauda);

            // as pernas: o ombro e o quadril, o joelho e a pata
            for (int k = 0; k < 4; k++)
            {
                bool tras = k >= 2;
                int lado = k % 2 == 0 ? 1 : -1;
                float g = q.perna * (tras ? Mathf.Lerp(1f, 1.35f, q.trasMaior - 1f) : 1f);
                float len = pernaLen;
                var cima = o.Junta((tras ? "PernaT." : "PernaD.") + (lado > 0 ? "L" : "R"), m.corpo,
                                   new Vector3(q.larg * 0.32f * lado, -q.alt * 0.5f + q.alt * 0.12f, (tras ? -1f : 1f) * q.comp * 0.36f));
                float cimaLen = len * 0.5f, baixoLen = len * 0.5f;
                o.Caixa(cima, new Vector3(0f, -cimaLen * 0.5f + g * 0.3f, 0f), new Vector3(g * 1.05f, cimaLen + g * 0.6f, g * (tras ? 1.7f : 1.25f) * q.trasMaior), q.pernas);
                var joelho = o.Junta("Joelho", cima, new Vector3(0f, -cimaLen, 0f));
                o.Caixa(joelho, new Vector3(0f, -baixoLen * 0.5f, 0f), new Vector3(g * 0.75f, baixoLen, g * 0.8f), q.pernas);
                o.Caixa(joelho, new Vector3(0f, -baixoLen + g * 0.35f, g * 0.25f), new Vector3(g * 0.9f, g * 0.7f, g * 1.3f), q.pata, tingir: false);
                m.pernas.Add(new ModeloDoBicho.Perna { cima = cima, baixo = joelho, traseira = tras });
            }
            q.extra?.Invoke(o, m.corpo, m.cabeca, cauda);
        }

        // ------------------------------------------------------------------ as aves

        sealed class A
        {
            public float comp, alt, larg, pernas, pescocoComp, pescoco, cabeca, bico, asa, caudaComp, caudaLarga;
            public Color corpo, peito, asaCor, cabecaCor, pescocoCor, bicoCor, pernaCor, caudaCor;
            public System.Action<Obra, Transform, Transform> extra;   // (obra, corpo, cabeça)

            static Color C(float r, float g, float b) => new Color(r, g, b);

            public static A Pato() => new A
            {
                comp = 0.32f, alt = 0.16f, larg = 0.17f, pernas = 0.07f, pescocoComp = 0.10f, pescoco = 0.06f, cabeca = 0.07f, bico = 0.06f,
                asa = 0.28f, caudaComp = 0.07f, caudaLarga = 0.1f,
                corpo = C(0.45f, 0.38f, 0.30f), peito = C(0.42f, 0.25f, 0.18f), asaCor = C(0.40f, 0.36f, 0.32f),
                cabecaCor = C(0.10f, 0.35f, 0.18f), pescocoCor = C(0.10f, 0.32f, 0.17f), bicoCor = C(0.85f, 0.75f, 0.2f),
                pernaCor = C(0.9f, 0.5f, 0.1f), caudaCor = C(0.2f, 0.18f, 0.17f),
            };

            public static A Ganso() => new A
            {
                comp = 0.5f, alt = 0.24f, larg = 0.25f, pernas = 0.12f, pescocoComp = 0.3f, pescoco = 0.06f, cabeca = 0.08f, bico = 0.06f,
                asa = 0.45f, caudaComp = 0.1f, caudaLarga = 0.14f,
                corpo = C(0.48f, 0.43f, 0.38f), peito = C(0.80f, 0.77f, 0.72f), asaCor = C(0.40f, 0.36f, 0.32f),
                cabecaCor = C(0.06f, 0.06f, 0.06f), pescocoCor = C(0.06f, 0.06f, 0.06f), bicoCor = C(0.08f, 0.08f, 0.08f),
                pernaCor = C(0.1f, 0.1f, 0.1f), caudaCor = C(0.1f, 0.1f, 0.1f),
                extra = (o, corpo, cab) => o.Caixa(cab, new Vector3(0f, -0.005f, -0.005f), new Vector3(0.085f, 0.04f, 0.05f), new Color(0.95f, 0.95f, 0.93f), tingir: false),
            };

            public static A Aguia() => new A
            {
                comp = 0.55f, alt = 0.24f, larg = 0.24f, pernas = 0.12f, pescocoComp = 0.08f, pescoco = 0.1f, cabeca = 0.1f, bico = 0.06f,
                asa = 0.95f, caudaComp = 0.18f, caudaLarga = 0.16f,
                corpo = C(0.22f, 0.15f, 0.10f), peito = C(0.20f, 0.14f, 0.09f), asaCor = C(0.18f, 0.12f, 0.08f),
                cabecaCor = C(0.95f, 0.94f, 0.9f), pescocoCor = C(0.95f, 0.94f, 0.9f), bicoCor = C(0.95f, 0.75f, 0.15f),
                pernaCor = C(0.95f, 0.75f, 0.15f), caudaCor = C(0.95f, 0.94f, 0.9f),
            };

            public static A Urubu() => new A
            {
                comp = 0.45f, alt = 0.2f, larg = 0.22f, pernas = 0.1f, pescocoComp = 0.1f, pescoco = 0.05f, cabeca = 0.06f, bico = 0.04f,
                asa = 0.8f, caudaComp = 0.14f, caudaLarga = 0.14f,
                corpo = C(0.07f, 0.07f, 0.07f), peito = C(0.06f, 0.06f, 0.06f), asaCor = C(0.09f, 0.09f, 0.09f),
                cabecaCor = C(0.6f, 0.2f, 0.18f), pescocoCor = C(0.55f, 0.2f, 0.18f), bicoCor = C(0.85f, 0.82f, 0.75f),
                pernaCor = C(0.5f, 0.45f, 0.42f), caudaCor = C(0.07f, 0.07f, 0.07f),
            };

            public static A Peru() => new A
            {
                comp = 0.55f, alt = 0.38f, larg = 0.32f, pernas = 0.38f, pescocoComp = 0.22f, pescoco = 0.06f, cabeca = 0.07f, bico = 0.04f,
                asa = 0.5f, caudaComp = 0.3f, caudaLarga = 0.45f,
                corpo = C(0.22f, 0.17f, 0.12f), peito = C(0.18f, 0.14f, 0.1f), asaCor = C(0.30f, 0.25f, 0.2f),
                cabecaCor = C(0.55f, 0.65f, 0.75f), pescocoCor = C(0.75f, 0.2f, 0.18f), bicoCor = C(0.75f, 0.7f, 0.6f),
                pernaCor = C(0.6f, 0.45f, 0.4f), caudaCor = C(0.3f, 0.22f, 0.15f),
                extra = (o, corpo, cab) => o.Caixa(cab, new Vector3(0f, -0.06f, 0.03f), new Vector3(0.025f, 0.07f, 0.025f), new Color(0.8f, 0.15f, 0.12f), tingir: false),
            };
        }

        static void Ave(Obra o, A a)
        {
            var m = o.m;
            m.altura = a.pernas + a.alt;
            m.comprimento = a.comp + a.caudaComp;
            m.corpo = o.Junta("Corpo", m.raiz, new Vector3(0f, a.pernas + a.alt * 0.5f, 0f));
            o.Caixa(m.corpo, Vector3.zero, new Vector3(a.larg, a.alt, a.comp), a.corpo);
            o.Caixa(m.corpo, new Vector3(0f, -a.alt * 0.1f, a.comp * 0.3f), new Vector3(a.larg * 0.9f, a.alt * 0.8f, a.comp * 0.4f), a.peito);
            o.Colisor(m.corpo, Vector3.zero, new Vector3(a.larg, a.alt, a.comp) * 1.1f, false);
            m.pescoco = o.Junta("Pescoco", m.corpo, new Vector3(0f, a.alt * 0.3f, a.comp * 0.42f), new Vector3(-65f, 0f, 0f));
            o.Caixa(m.pescoco, new Vector3(0f, 0f, a.pescocoComp * 0.5f), new Vector3(a.pescoco, a.pescoco, a.pescocoComp + a.pescoco), a.pescocoCor);
            m.cabeca = o.Junta("Cabeca", m.pescoco, new Vector3(0f, 0f, a.pescocoComp), new Vector3(65f, 0f, 0f));
            o.Caixa(m.cabeca, new Vector3(0f, 0f, a.cabeca * 0.2f), new Vector3(a.cabeca * 0.85f, a.cabeca, a.cabeca * 1.2f), a.cabecaCor);
            o.Caixa(m.cabeca, new Vector3(0f, -a.cabeca * 0.1f, a.cabeca * 0.8f + a.bico * 0.5f), new Vector3(a.cabeca * 0.45f, a.cabeca * 0.3f, a.bico), a.bicoCor, tingir: false);
            o.Colisor(m.cabeca, new Vector3(0f, 0f, a.cabeca * 0.4f), Vector3.one * a.cabeca * 1.4f, true);
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var asa = o.Junta(lado > 0 ? "Asa.L" : "Asa.R", m.corpo, new Vector3(a.larg * 0.5f * lado, a.alt * 0.3f, a.comp * 0.15f));
                o.Caixa(asa, new Vector3(a.asa * 0.25f * lado, 0f, -a.comp * 0.25f), new Vector3(a.asa * 0.5f, 0.02f, a.comp * 0.7f), a.asaCor);
                m.asas.Add(asa);
                var perna = o.Junta(lado > 0 ? "PernaT.L" : "PernaT.R", m.corpo, new Vector3(a.larg * 0.2f * lado, -a.alt * 0.45f, -a.comp * 0.05f));
                o.Caixa(perna, new Vector3(0f, -a.pernas * 0.5f, 0f), new Vector3(0.015f, a.pernas, 0.015f), a.pernaCor, tingir: false);
                var pe = o.Junta("Pe", perna, new Vector3(0f, -a.pernas, 0f));
                o.Caixa(pe, new Vector3(0f, 0.005f, 0.025f), new Vector3(0.05f, 0.01f, 0.06f), a.pernaCor, tingir: false);
                m.pernas.Add(new ModeloDoBicho.Perna { cima = perna, baixo = pe, traseira = true });
            }
            var cauda = o.Junta("Cauda", m.corpo, new Vector3(0f, a.alt * 0.2f, -a.comp * 0.5f), new Vector3(15f, 0f, 0f));
            o.Caixa(cauda, new Vector3(0f, 0f, -a.caudaComp * 0.5f), new Vector3(a.caudaLarga, 0.02f, a.caudaComp), a.caudaCor);
            m.cauda.Add(cauda);
            a.extra?.Invoke(o, m.corpo, m.cabeca);
        }

        // ------------------------------------------------------------------ o sapo, a cobra e o peixe

        static void Sapo(Obra o)
        {
            var m = o.m;
            m.altura = 0.06f;
            m.comprimento = 0.12f;
            var verde = new Color(0.28f, 0.36f, 0.18f);
            m.corpo = o.Junta("Corpo", m.raiz, new Vector3(0f, 0.035f, 0f), new Vector3(-12f, 0f, 0f));
            o.Caixa(m.corpo, Vector3.zero, new Vector3(0.07f, 0.04f, 0.09f), verde);
            o.Caixa(m.corpo, new Vector3(0f, -0.012f, 0.01f), new Vector3(0.06f, 0.02f, 0.07f), new Color(0.75f, 0.72f, 0.55f));
            m.cabeca = o.Junta("Cabeca", m.corpo, new Vector3(0f, 0.005f, 0.045f));
            o.Caixa(m.cabeca, new Vector3(0f, 0f, 0.015f), new Vector3(0.065f, 0.03f, 0.04f), verde);
            for (int lado = -1; lado <= 1; lado += 2)
                o.Caixa(m.cabeca, new Vector3(0.025f * lado, 0.018f, 0.015f), new Vector3(0.015f, 0.015f, 0.015f), new Color(0.75f, 0.6f, 0.15f), tingir: false);
            o.Colisor(m.corpo, Vector3.zero, new Vector3(0.08f, 0.05f, 0.12f), false);
            for (int k = 0; k < 4; k++)
            {
                bool tras = k >= 2;
                int lado = k % 2 == 0 ? 1 : -1;
                var cima = o.Junta("Perna", m.corpo, new Vector3(0.035f * lado, -0.01f, tras ? -0.035f : 0.03f), new Vector3(0f, 0f, -30f * lado));
                o.Caixa(cima, new Vector3(0f, -0.015f, tras ? -0.01f : 0f), new Vector3(0.015f, 0.03f, tras ? 0.05f : 0.02f), verde);
                var baixo = o.Junta("Pe", cima, new Vector3(0f, -0.03f, 0f));
                o.Caixa(baixo, new Vector3(0f, 0f, 0.01f), new Vector3(0.025f, 0.006f, 0.03f), verde);
                m.pernas.Add(new ModeloDoBicho.Perna { cima = cima, baixo = baixo, traseira = tras });
            }
        }

        static void Cobra(Obra o)
        {
            var m = o.m;
            m.altura = 0.05f;
            m.comprimento = 1.1f;
            var claro = new Color(0.62f, 0.52f, 0.36f);
            var escuro = new Color(0.30f, 0.23f, 0.15f);
            m.corpo = o.Junta("Corpo", m.raiz, new Vector3(0f, 0.03f, 0.45f));
            Transform pai = m.corpo;
            const int gomos = 12;
            for (int i = 0; i < gomos; i++)
            {
                float t = i / (float)(gomos - 1);
                float grosso = Mathf.Lerp(0.05f, 0.022f, t * t);
                var g = o.Junta("Gomo", pai, i == 0 ? Vector3.zero : new Vector3(0f, 0f, -0.085f));
                o.Caixa(g, new Vector3(0f, 0f, -0.04f), new Vector3(grosso, grosso * 0.8f, 0.09f), i % 2 == 0 ? claro : escuro);
                if (i < gomos - 2) o.Colisor(g, new Vector3(0f, 0f, -0.04f), new Vector3(grosso + 0.03f, grosso + 0.03f, 0.09f), false);
                m.gomos.Add(g);
                pai = g;
            }
            o.Caixa(pai, new Vector3(0f, 0.005f, -0.1f), new Vector3(0.025f, 0.02f, 0.05f), new Color(0.78f, 0.72f, 0.58f));   // o chocalho
            m.cabeca = o.Junta("Cabeca", m.corpo, new Vector3(0f, 0.005f, 0.0f));
            o.Caixa(m.cabeca, new Vector3(0f, 0f, 0.035f), new Vector3(0.055f, 0.03f, 0.07f), escuro);
            o.Colisor(m.cabeca, new Vector3(0f, 0f, 0.035f), new Vector3(0.07f, 0.05f, 0.08f), true);
        }

        static void Peixe(Obra o)
        {
            var m = o.m;
            m.altura = 0.15f;
            m.comprimento = 0.7f;
            m.corpo = o.Junta("Corpo", m.raiz, new Vector3(0f, 0.08f, 0f));
            o.Caixa(m.corpo, new Vector3(0f, 0f, 0.05f), new Vector3(0.10f, 0.15f, 0.42f), new Color(0.55f, 0.58f, 0.6f));
            o.Caixa(m.corpo, new Vector3(0f, -0.03f, 0.05f), new Vector3(0.105f, 0.06f, 0.38f), new Color(0.75f, 0.35f, 0.28f));
            m.cabeca = o.Junta("Cabeca", m.corpo, new Vector3(0f, 0f, 0.26f));
            o.Caixa(m.cabeca, new Vector3(0f, -0.01f, 0.04f), new Vector3(0.085f, 0.11f, 0.1f), new Color(0.45f, 0.48f, 0.5f));
            o.Colisor(m.corpo, new Vector3(0f, 0f, 0.05f), new Vector3(0.12f, 0.17f, 0.6f), false);
            var cauda = o.Junta("Cauda", m.corpo, new Vector3(0f, 0f, -0.16f));
            o.Caixa(cauda, new Vector3(0f, 0f, -0.08f), new Vector3(0.05f, 0.08f, 0.16f), new Color(0.5f, 0.52f, 0.55f));
            o.Caixa(cauda, new Vector3(0f, 0f, -0.18f), new Vector3(0.02f, 0.16f, 0.06f), new Color(0.45f, 0.47f, 0.5f));
            m.cauda.Add(cauda);
        }

        // ------------------------------------------------------------------ a malha e os materiais

        static Mesh Cubo()
        {
            if (_cubo != null) return _cubo;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Face(Vector3 normal, Vector3 a, Vector3 b)
            {
                Vector3 c = normal * 0.5f;
                int i = v.Count;
                v.Add(c - a * 0.5f - b * 0.5f);
                v.Add(c + a * 0.5f - b * 0.5f);
                v.Add(c + a * 0.5f + b * 0.5f);
                v.Add(c - a * 0.5f + b * 0.5f);
                for (int k = 0; k < 4; k++) n.Add(normal);
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            // (a × b = a normal em todas: o triângulo 0-1-2 tem (v1 - v0) × (v2 - v0) = a normal, o horário visto de fora, a
            // frente na Unity; conferido com o RecalculateNormals em 12/11/2026: o 0-2-1 de antes deixava as caixas do avesso)
            Face(Vector3.up, Vector3.forward, Vector3.right);
            Face(Vector3.down, Vector3.right, Vector3.forward);
            Face(Vector3.right, Vector3.back, Vector3.up);
            Face(Vector3.left, Vector3.forward, Vector3.up);
            Face(Vector3.forward, Vector3.right, Vector3.up);
            Face(Vector3.back, Vector3.left, Vector3.up);
            _cubo = new Mesh { name = "Fauna_Cubo" };
            _cubo.SetVertices(v);
            _cubo.SetNormals(n);
            _cubo.SetTriangles(t, 0);
            _cubo.RecalculateBounds();
            return _cubo;
        }

        static Material Material(Color cor)
        {
            Color32 chave = cor;
            if (_materiais.TryGetValue(chave, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            m = new Material(shader) { name = "Fauna_" + ColorUtility.ToHtmlStringRGB(cor) };
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            _materiais[chave] = m;
            return m;
        }
    }
}
