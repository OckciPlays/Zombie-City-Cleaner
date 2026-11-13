using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena de floresta andável (o Plano_Floresta.md, "A cena de floresta", 09/11/2026): um pedaço de mata fechada para
    /// testar a floresta de verdade, onde se anda (o AndarNoExemplo). Gera, e não salva (HideFlags.DontSave, como as cenas
    /// de exemplo), o terreno (as colinas, com o material do chão do jogo e os pesos das texturas na cor do vértice: o
    /// chão da mata, a grama nas clareiras, a terra e o cascalho na beira do rio), o riacho (o canal cavado no terreno e a
    /// água do jogo) e os pinheiros realistas (os prefabs com os LODs), densos, com as clareiras. R sorteia outra, F liga
    /// e desliga a neblina. O vento e o céu de dia nas globais dos shaders, a cada quadro.
    /// </summary>
    [ExecuteAlways]
    public class FlorestaDeTeste : MonoBehaviour, IChaoDaGrama
    {
        public int semente = 11;
        [Tooltip("O lado do quadrado (m).")] public float lado = 320f;
        [Tooltip("Árvores por 100 m² na mata fechada.")] public float densidade = 7f;
        [Tooltip("Os prefabs dos pinheiros realistas (os 15: 5 altos, 5 médios e 5 pequenos).")] public GameObject[] pinheiros;
        public Material chao, agua;
        public bool rio = true;
        [Tooltip("Quanto da mata vira clareira (0 nada, 1 muito).")] [Range(0f, 1f)] public float clareiras = 0.35f;
        [Header("Vento")]
        public float forcaDoVento = 0.25f;
        public Vector2 direcaoDoVento = new Vector2(0.92f, 0.38f);
        [Header("Neblina de altura")]
        [Tooltip("A densidade da neblina de altura do jogo no chão (0 desligada; no jogo, 0,003 de dia e 0,07 a 0,1 de noite e na madrugada).")]
        [Range(0f, 0.1f)] public float neblinaDeAltura = 0f;

        const string NomeDaRaiz = "Gerado (não é salvo)";
        const float Celula = 2f, Pedaco = 40f;
        /// <summary>O nível da água do riacho (plano).</summary>
        public const float NivelDaAgua = -0.35f;
        const float MeiaLargura = 3.2f, Rampa = 2.2f, Margem = 0.55f;

        public int Arvores { get; private set; }
        public string Resumo { get; private set; } = "";
        Transform _raiz;
        string _geradoCom;
        Light _sol;
        [System.NonSerialized] GUIStyle _estilo;
        static readonly int VentoId = Shader.PropertyToID("_ZccVento");
        static readonly int ReflexoPlanoId = Shader.PropertyToID("_ZccReflexoPlano");
        static readonly int NeblinaCorId = Shader.PropertyToID("_NeblinaCor");
        static readonly int NeblinaParamsId = Shader.PropertyToID("_NeblinaParams");
        static readonly int NeblinaVentoId = Shader.PropertyToID("_NeblinaVento");

        string Chave => $"{semente}|{lado}|{densidade}|{rio}|{clareiras}|{(pinheiros == null ? 0 : pinheiros.Length)}";

        void OnEnable()
        {
            if (!Application.isPlaying) Shader.SetGlobalFloat(ReflexoPlanoId, -1e9f);   // no Editor, sem o reflexo da água
            if (transform.Find(NomeDaRaiz) == null) Gerar();
        }

        void OnDisable() => Apagar();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && _geradoCom != Chave) Gerar();
            };
        }
#endif

        // ------------------------------------------------------------------ o relevo

        // o smoothstep do GLSL (o Mathf.SmoothStep interpola de a até b)
        static float Passo(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Hash(int x, int y)
        {
            uint v = (uint)(x * 374761393 + y * 668265263 + 1013904223);
            v = (v ^ (v >> 13)) * 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }

        static float Ruido(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx), fy);
        }

        /// <summary>O ruído fractal de -1 a 1.</summary>
        static float Fbm(float x, float y)
        {
            float s = Ruido(x, y) * 0.5f + Ruido(x * 2.03f + 17f, y * 2.03f + 9f) * 0.25f + Ruido(x * 4.1f + 31f, y * 4.1f + 3f) * 0.125f;
            return s / 0.875f * 2f - 1f;
        }

        /// <summary>Onde o riacho passa (x) numa altura z: uma curva suave, de ponta a ponta.</summary>
        public float RioX(float z) => Mathf.Sin(z * 0.0118f + 0.6f) * lado * 0.2f + 9f * Fbm(z * 0.03f + 2f, 17f);

        /// <summary>A altura do terreno: as colinas e, perto do riacho, a margem e o canal.</summary>
        public float Altura(float x, float z)
        {
            float h = 4.5f * Fbm(x * 0.011f + 3.1f, z * 0.011f + 7.7f) + 1.1f * Fbm(x * 0.045f + 11f, z * 0.045f) + 0.22f * Fbm(x * 0.2f, z * 0.2f + 5f) + 2.2f;
            if (!rio) return h;
            float d = Mathf.Abs(x - RioX(z));
            float margem = NivelDaAgua + Margem + 0.35f + 0.25f * Fbm(x * 0.08f, z * 0.08f);
            float perto = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - MeiaLargura - Rampa) / 14f));
            h = Mathf.Lerp(h, margem, perto);
            if (d < MeiaLargura + Rampa)
            {
                float u = Mathf.Clamp01((d - MeiaLargura) / Rampa);
                float banco = Mathf.Lerp(NivelDaAgua + 0.2f, margem, u * u);
                if (d < MeiaLargura)
                {
                    float q = d / MeiaLargura;
                    banco = NivelDaAgua + 0.2f - 1.0f * (1f - q * q);
                }
                h = banco;
            }
            return h;
        }

        Vector3 Normal(float x, float z)
        {
            const float e = 0.5f;
            float dx = Altura(x + e, z) - Altura(x - e, z);
            float dz = Altura(x, z + e) - Altura(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        int IChaoDaGrama.Semente => semente;
        Rect IChaoDaGrama.Limites => new Rect(-lado * 0.5f, -lado * 0.5f, lado, lado);
        bool IChaoDaGrama.SeguroEmThreads => false;

        /// <summary>A clareira (0 mata fechada, 1 clareira aberta) num ponto.</summary>
        public float Clareira(float x, float z)
        {
            float c = Fbm(x * 0.018f + 31f + semente * 0.37f, z * 0.018f + 5f) * 0.5f + 0.5f;
            float limiar = Mathf.Lerp(0.85f, 0.45f, clareiras);
            return Passo(limiar - 0.12f, limiar + 0.1f, c);
        }

        /// <summary>
        /// A densidade da grama densa (0 a 1) num ponto: cheia nas clareiras, rala sob a mata (o chão de agulhas), nada no
        /// canal do riacho e pouca na beira de terra e cascalho.
        /// </summary>
        public float Grama(float x, float z)
        {
            float clareira = Clareira(x, z);
            float d = rio ? Mathf.Abs(x - RioX(z)) : 1000f;
            if (d < MeiaLargura + Rampa * 0.4f) return 0f;
            float beira = 1f - Passo(MeiaLargura + Rampa * 0.4f, MeiaLargura + Rampa + 3f, d);
            return Mathf.Lerp(0.35f, 1f, clareira) * (1f - 0.85f * beira);
        }

        Color32 Pesos(float x, float z)
        {
            float grama = Clareira(x, z);
            float d = rio ? Mathf.Abs(x - RioX(z)) : 1000f;
            float beira = 1f - Passo(3.5f, 7.5f, d);
            float r = grama * (1f - beira);
            float g = 0.3f * beira + 0.08f * (1f - grama) * (1f - beira);
            float b = 0.6f * beira;
            float a = (1f - grama) * (1f - beira) * 0.92f;
            float soma = Mathf.Max(r + g + b + a, 1e-3f);
            return new Color32((byte)(255f * r / soma), (byte)(255f * g / soma), (byte)(255f * b / soma), (byte)(255f * a / soma));
        }

        // ------------------------------------------------------------------ gerar

        public void Gerar()
        {
            Apagar();
            _geradoCom = Chave;
            _raiz = Novo(NomeDaRaiz, transform).transform;
            int triangulos = Terreno();
            if (rio) Agua();
            Arvores = Pinheiros();
            Resumo = $"Floresta de teste: {lado:0} × {lado:0} m, {Arvores} pinheiros, semente {semente}, o terreno com {triangulos} triângulos";
        }

        public void Apagar()
        {
            Transform velho;
            while ((velho = transform.Find(NomeDaRaiz)) != null)
            {
                foreach (var f in velho.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && (f.sharedMesh.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(f.sharedMesh);
                foreach (var c in velho.GetComponentsInChildren<MeshCollider>(true))
                    if (c.sharedMesh != null && (c.sharedMesh.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(c.sharedMesh);
                DestroyImmediate(velho.gameObject);
            }
            _raiz = null;
        }

        /// <summary>
        /// No Editor, o que é gerado não é salvo (DontSave). No Play, nada: o GPU Resident Drawer ignora os objetos com
        /// DontSave (nem o FindObjectsByType os acha), e sem ele cada árvore era um draw call (18 a 25 mil por quadro).
        /// </summary>
        static HideFlags FlagsDoGerado => Application.isPlaying ? HideFlags.None : HideFlags.DontSave;

        static GameObject Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome) { hideFlags = FlagsDoGerado };
            go.transform.SetParent(pai, false);
            return go;
        }

        int Terreno()
        {
            int pedacos = Mathf.Max(1, Mathf.CeilToInt(lado / Pedaco));
            int n = Mathf.RoundToInt(Pedaco / Celula);
            int triangulos = 0;
            var pai = Novo("Terreno", _raiz).transform;
            for (int pj = 0; pj < pedacos; pj++)
            for (int pi = 0; pi < pedacos; pi++)
            {
                float x0 = -lado * 0.5f + pi * Pedaco, z0 = -lado * 0.5f + pj * Pedaco;
                var v = new Vector3[(n + 1) * (n + 1)];
                var nr = new Vector3[v.Length];
                var c = new Color32[v.Length];
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = x0 + i * Celula, z = z0 + j * Celula;
                    int k = j * (n + 1) + i;
                    v[k] = new Vector3(x, Altura(x, z), z);
                    nr[k] = Normal(x, z);
                    c[k] = Pesos(x, z);
                }
                var tri = new int[n * n * 6];
                int t = 0;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, d = a + n + 1, e = d + 1;
                    tri[t++] = a; tri[t++] = d; tri[t++] = e;
                    tri[t++] = a; tri[t++] = e; tri[t++] = b;
                }
                var malha = new Mesh { name = $"Terreno {pi},{pj}", hideFlags = HideFlags.DontSave };
                malha.SetVertices(v);
                malha.SetNormals(nr);
                malha.SetColors(c);
                malha.SetTriangles(tri, 0, true);
                malha.RecalculateBounds();
                var go = Novo($"Terreno {pi},{pj}", pai);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = chao;
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.AddComponent<MeshCollider>().sharedMesh = malha;
                triangulos += n * n * 2;
            }
            return triangulos;
        }

        void Agua()
        {
            const float sobra = 0.8f;
            var v = new List<Vector3>();
            var c = new List<Color32>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            var cor = new Color32(255, 255, 0, 255);
            float z0 = -lado * 0.5f - 4f, z1 = lado * 0.5f + 4f;
            int segmentos = Mathf.CeilToInt((z1 - z0) / 2f);
            for (int i = 0; i <= segmentos; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)segmentos);
                float xs = RioX(z);
                float meia = MeiaLargura + sobra;
                v.Add(new Vector3(xs - meia, NivelDaAgua, z)); uv.Add(new Vector2(-meia, z)); c.Add(cor);
                v.Add(new Vector3(xs + meia, NivelDaAgua, z)); uv.Add(new Vector2(meia, z)); c.Add(cor);
                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    tri.Add(a); tri.Add(a + 2); tri.Add(a + 3);
                    tri.Add(a); tri.Add(a + 3); tri.Add(a + 1);
                }
            }
            var malha = new Mesh { name = "Água do riacho", hideFlags = HideFlags.DontSave };
            malha.SetVertices(v);
            malha.SetUVs(0, uv);
            malha.SetColors(c);
            malha.SetTriangles(tri, 0, true);
            malha.RecalculateNormals();
            malha.RecalculateBounds();
            var go = Novo("Água do riacho", _raiz);
            go.layer = 4;   // Water: fora do reflexo
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = agua;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        int Pinheiros()
        {
            if (pinheiros == null || pinheiros.Length == 0) return 0;
            var pai = Novo("Pinheiros", _raiz).transform;
            var rnd = new System.Random(semente);
            float Entre(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float celula = Mathf.Sqrt(100f / Mathf.Max(densidade, 0.1f));
            int n = Mathf.FloorToInt(lado / celula);
            int total = 0;
            int porTamanho = Mathf.Max(1, pinheiros.Length / 3);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = -lado * 0.5f + (i + 0.5f + Entre(-0.42f, 0.42f)) * celula;
                float z = -lado * 0.5f + (j + 0.5f + Entre(-0.42f, 0.42f)) * celula;
                float clareira = Clareira(x, z);
                float manter = Mathf.Lerp(1f, 0.05f, clareira);
                if (rnd.NextDouble() > manter) continue;
                if (rio && Mathf.Abs(x - RioX(z)) < MeiaLargura + Rampa + Entre(0f, 2.5f)) continue;
                double s = rnd.NextDouble();
                int tamanho = clareira > 0.4f ? (s < 0.5 ? 2 : 1) : (s < 0.52 ? 0 : s < 0.88 ? 1 : 2);
                int variacao = rnd.Next(porTamanho);
                int indice = Mathf.Min(tamanho * porTamanho + variacao, pinheiros.Length - 1);
                var prefab = pinheiros[indice];
                if (prefab == null) continue;
                var pos = new Vector3(x, Altura(x, z) - 0.15f, z);
                var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Entre(0f, 360f), 0f), pai);
                if (!Application.isPlaying) foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.DontSave;
                go.name = prefab.name;
                go.transform.localScale = Vector3.one * (tamanho == 2 ? Entre(0.8f, 1.3f) : Entre(0.85f, 1.2f));
                total++;
            }
            return total;
        }

        // ------------------------------------------------------------------ no Play

        void Update()
        {
            // o céu de dia e o vento, nas globais (no jogo, o AmbienteDoDia e o FlorestaDoMundo fazem isso)
            if (_sol == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _sol = l; break; }
            var direcao = _sol != null ? -_sol.transform.forward : new Vector3(0.4f, 0.7f, 0.5f).normalized;
            Shader.SetGlobalColor("_ZccCeuTopo", new Color(0.4f, 0.52f, 0.7f));
            Shader.SetGlobalColor("_ZccCeuHorizonte", new Color(0.6f, 0.64f, 0.68f));
            Shader.SetGlobalColor("_ZccCeuNeblina", new Color(0.66f, 0.7f, 0.72f));
            Shader.SetGlobalVector("_ZccSol", new Vector4(direcao.x, direcao.y, direcao.z, 1f));
            Shader.SetGlobalColor("_ZccSolCor", new Color(1.3f, 1.25f, 1.15f));
            Shader.SetGlobalVector("_ZccLua", new Vector4(direcao.x, direcao.y, direcao.z, 0f));
            Shader.SetGlobalFloat("_ZccNuvens", 0.15f);
            Shader.SetGlobalFloat("_ZccEstrelas", 0f);
            float t = Application.isPlaying ? Time.time : (float)UnityEditor_Tempo();
            float rajada = Mathf.Sin(t * 0.23f) * 0.5f + Mathf.Sin(t * 0.071f + 1f) * 0.5f;
            var dir = direcaoDoVento.normalized;
            Shader.SetGlobalVector(VentoId, new Vector4(dir.x, dir.y, forcaDoVento * (1f + 0.35f * rajada), rajada));
            // a neblina de altura do jogo (o passe NeblinaDeAltura do renderizador, sempre ativo): as globais dela são do
            // AmbienteDoDia do mundo e ficam na memória do Editor ao trocar de cena, então a cena manda nas suas (a pedido,
            // 09/11/2026: depois de uma ida ao mundo normal, a neblina de lá apareceu aqui, tapando os troncos de perto)
            Shader.SetGlobalColor(NeblinaCorId, RenderSettings.fogColor);
            Shader.SetGlobalVector(NeblinaParamsId, new Vector4(neblinaDeAltura, NivelDaAgua, 0.3f, 140f));
            Shader.SetGlobalVector(NeblinaVentoId, new Vector4(dir.x * t * 0.5f, dir.y * t * 0.5f, 0.045f, 0.6f));

            if (!Application.isPlaying) return;
            var k = Keyboard.current;
            if (k == null) return;
            if (k.rKey.wasPressedThisFrame) { semente = Random.Range(1, 100000); Gerar(); }
            if (k.fKey.wasPressedThisFrame) RenderSettings.fog = !RenderSettings.fog;
        }

        static double UnityEditor_Tempo()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return 0;
#endif
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (_estilo == null) _estilo = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperRight, richText = true };
            _estilo.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 620, 8, 610, 60), $"{Resumo}\nR sorteia outra · F neblina ({(RenderSettings.fog ? "ligada" : "desligada")})", _estilo);
        }
    }
}
