using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O que o tempo (<see cref="Tempo"/>, decidido pelo anfitrião em CicloDoDia.Tempo.cs) mostra e toca nesta máquina; a
    /// pedido em 02/11/2026. Fica no objeto do <see cref="CicloDoDia"/> (ele mesmo põe).
    /// <list type="bullet">
    /// <item>A chuva: as gotas caem em volta da câmera, tortas com o vento, e morrem no que batem (o telhado, o carro, o chão),
    /// com um respingo; brilham no facho da lanterna e dos postes.</item>
    /// <item>Os sons (a pedido: "o som do cair da chuva e raios fica abafado" dentro das construções e do carro): a chuva fina,
    /// a média e a forte, o vento forte e o tornado, todos abafados dentro de uma casa ou de um prédio (sem os agudos, mais
    /// baixos), mais ainda no carro e quase sumindo no bunker; dentro, a chuva no telhado; no carro, a chuva na lata.</item>
    /// <item>Os raios: o clarão na hora e o trovão atrasado pela distância (343 m/s): longe, o ronco grave e baixo; no meio, o
    /// rolar; perto, o estalo seco, o estrondo e o zumbido no ouvido (o resto do som abaixa por uns segundos). O risco do raio
    /// aparece nos de perto, com uma luz forte no chão e faíscas. Dentro, o trovão também chega abafado.</item>
    /// <item>O tornado: o funil (umas cascas girando, ClimaFunil.shader), a poeira em volta do pé e os destroços voando. Perto
    /// dele, o vento puxa o jogador (leve: correndo dá para fugir); no miolo, ele joga o jogador longe (o dano é do anfitrião).
    /// A casa protege em parte e o porão e o bunker, inteiro.</item>
    /// <item>As poças da chuva forte: um sorteio fixo por células de 7 m (o mesmo em todas as máquinas), só no chão plano das
    /// ruas e das calçadas, fora das construções. Pisar numa poça cheia faz o respingo (<see cref="NaPoca"/>, nos passos).</item>
    /// </list>
    /// </summary>
    public class ClimaNaTela : MonoBehaviour
    {
        public static ClimaNaTela Instancia { get; private set; }

        /// <summary>De 0 a 1: o tornado perto de quem joga nesta máquina (para a música).</summary>
        public static float PertoDoTornado { get; private set; }
        /// <summary>Quando quem joga aqui avistou o tornado (Time.time; negativo se ainda não).</summary>
        public static float AvistouEm { get; private set; } = -999f;
        /// <summary>Quem joga aqui avistou o tornado agora (a música toca o golpe).</summary>
        public static event System.Action AoAvistarOTornado;

        /// <summary>De 0 (fora) a 1 (no bunker): o quanto a chuva e os trovões chegam abafados aqui (o F1 mostra).</summary>
        public float Abafado => _abafo;
        public string Onde { get; private set; } = "fora";

        static readonly int ChuvaId = Shader.PropertyToID("_ZccChuva");
        static readonly int PocasId = Shader.PropertyToID("_ZccPocas");
        static readonly int ClaraoId = Shader.PropertyToID("_ZccClarao");
        static readonly int VentoId = Shader.PropertyToID("_ZccVento");

        // o que a chuva acerta: o cenário e os carros
        const int MascaraDaChuva = Abafamento.Mascara | (1 << Vehicles.Carro.CamadaVeiculo);

        // ------------------------------------------------------------------ os sons
        AudioClip _somFina, _somChuva, _somForte, _somTelhado, _somCarro, _somVento, _somTornado, _somZumbido;
        AudioClip[] _trovoesLonge, _trovoesMedio, _trovoesPerto, _estalos;
        static AudioClip[] _sonsDaPoca;

        AudioSource _fina, _media, _forte, _telhado, _carro, _vento, _tornadoSom, _zumbido;
        AudioLowPassFilter _filtroFina, _filtroMedia, _filtroForte, _filtroVento, _filtroTornado;
        readonly List<AudioSource> _fontesDeTrovao = new List<AudioSource>();

        struct Trovao
        {
            public double quando;
            public AudioClip clip;
            public float volume, corte;
            public Vector3 ponto;
            public float zumbido;
        }

        readonly List<Trovao> _trovoes = new List<Trovao>();

        // ------------------------------------------------------------------ onde quem joga está
        float _abafo, _alvoAbafo, _noTelhado, _alvoTelhado, _noCarro, _alvoCarro, _proximaOlhada, _surdo = 1f;

        // ------------------------------------------------------------------ a chuva e os raios
        ParticleSystem _chuva, _respingo, _faiscas;
        Material _materialDaChuva, _materialDoRespingo, _materialDoRaio, _materialDasFaiscas, _materialDaPoeira, _materialDosDestrocos;
        Vector3 _cameraAntes, _cameraVel;
        readonly List<LineRenderer> _riscos = new List<LineRenderer>();
        Light _luzDoRaio;

        struct Clarao
        {
            public float inicio, forca;
            public int padrao;
            public List<LineRenderer> riscos;
            public float luz;
            public Vector3 ponto;
        }

        readonly List<Clarao> _claroes = new List<Clarao>();

        // ------------------------------------------------------------------ o tornado
        GameObject _tornado;
        readonly List<Material> _cascas = new List<Material>();
        ParticleSystem _poeira, _destrocos;
        MovimentoPrimeiraPessoa _movimento;
        float _protecao, _proximaProtecao, _proximoArremesso;
        bool _avistado;
        // a pé, girando no miolo (07/11/2026): até quando, a altura e o chão de onde subiu, e quando pode pegar de novo
        float _girandoAte = -1f, _alturaDoGiro, _chaoDoGiro, _proximoGiro;
        /// <summary>A pé: até onde (m) o tornado puxa, a força no meio (m/s), e o miolo, onde ele pega e gira.</summary>
        const float PuxaAPe = 90f, ForcaAPe = 6.5f, MioloAPe = 13f;

        // ------------------------------------------------------------------ as poças
        struct Poca
        {
            public bool existe;
            public Vector3 centro;
            public float raio, giro;
        }

        const float Celula = 7f;
        static readonly Dictionary<long, Poca> _pocas = new Dictionary<long, Poca>();
        readonly Dictionary<long, GameObject> _pocasNaTela = new Dictionary<long, GameObject>();
        readonly List<GameObject> _pocasLivres = new List<GameObject>();
        readonly List<long> _pocasSaindo = new List<long>();
        Material _materialDaPoca;
        Mesh _malhaDaPoca;
        float _proximasPocas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar()
        {
            _pocas.Clear();
            AvistouEm = -999f;
            PertoDoTornado = 0f;
        }

        void Awake()
        {
            Instancia = this;
            _somFina = Carregar("chuva_fina");
            _somChuva = Carregar("chuva");
            _somForte = Carregar("chuva_forte");
            _somTelhado = Carregar("chuva_telhado");
            _somCarro = Carregar("chuva_carro");
            _somVento = Carregar("vento_forte");
            _somTornado = Carregar("tornado");
            _somZumbido = Carregar("zumbido");
            _trovoesLonge = Varios("trovao_longe_", 4);
            _trovoesMedio = Varios("trovao_medio_", 3);
            _trovoesPerto = Varios("trovao_perto_", 3);
            _estalos = Varios("estalo_", 2);

            _fina = Fonte("Chuva fina", _somFina, out _filtroFina);
            _media = Fonte("Chuva", _somChuva, out _filtroMedia);
            _forte = Fonte("Chuva forte", _somForte, out _filtroForte);
            _telhado = Fonte("Chuva no telhado", _somTelhado, out _);
            _carro = Fonte("Chuva no carro", _somCarro, out _);
            _vento = Fonte("Vento forte", _somVento, out _filtroVento);
            _tornadoSom = Fonte("Tornado", _somTornado, out _filtroTornado);
            _tornadoSom.spatialBlend = 0.75f;
            _tornadoSom.rolloffMode = AudioRolloffMode.Linear;
            _tornadoSom.minDistance = 30f;
            _tornadoSom.maxDistance = 900f;
            _zumbido = Fonte("Zumbido", _somZumbido, out _);
            _zumbido.loop = false;
            for (int i = 0; i < 4; i++)
            {
                var f = Fonte("Trovão " + (i + 1), null, out _);
                f.loop = false;
                f.priority = 5;
                _fontesDeTrovao.Add(f);
            }

            var particula = Shader.Find("ZombieCityCleaner/ClimaParticula");
            if (particula == null) particula = Resources.Load<Shader>("ClimaParticula");
            _materialDaChuva = NovoMaterial(particula, new Color(0.72f, 0.76f, 0.8f, 0.3f), 0, 1f, 1f, false);
            _materialDoRespingo = NovoMaterial(particula, new Color(0.8f, 0.83f, 0.86f, 0.45f), 2, 1f, 1f, false);
            // a gota e o respingo somem debaixo dos tetos e dentro do carro (o mapa dos tetos, embaixo)
            if (_materialDaChuva != null) _materialDaChuva.SetFloat("_Teto", 1f);
            if (_materialDoRespingo != null) _materialDoRespingo.SetFloat("_Teto", 1f);
            _materialDoRaio = NovoMaterial(particula, new Color(7f, 7.5f, 9f, 1f), 3, 0f, 0.25f, true);
            _materialDasFaiscas = NovoMaterial(particula, new Color(5f, 3f, 1.2f, 1f), 0, 0f, 0.3f, true);
            _materialDaPoeira = NovoMaterial(particula, new Color(0.36f, 0.33f, 0.29f, 0.5f), 0, 1f, 0.4f, false);
            _materialDosDestrocos = NovoMaterial(particula, new Color(0.13f, 0.12f, 0.11f, 1f), 1, 1f, 0.45f, false);
            var poca = Shader.Find("ZombieCityCleaner/ClimaPoca");
            if (poca == null) poca = Resources.Load<Shader>("ClimaPoca");
            if (poca != null) _materialDaPoca = new Material(poca) { name = "Poça" };
            _malhaDaPoca = MalhaDaPoca();
            CriarChuva();
            CriarRaio();
            _tetos = new Texture2D(LadoDoTeto, LadoDoTeto, TextureFormat.RFloat, false, true)
            {
                name = "Tetos da chuva", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _raiosDoTeto = new NativeArray<RaycastCommand>(LadoDoTeto * LadoDoTeto, Allocator.Persistent);
            _acertosDoTeto = new NativeArray<RaycastHit>(LadoDoTeto * LadoDoTeto, Allocator.Persistent);
            Shader.SetGlobalTexture(TetoId, _tetos);
            Shader.SetGlobalVector(TetoAreaId, Vector4.zero);
            Shader.SetGlobalVector(CarroMeioId, Vector4.zero);
        }

        void OnEnable() => Tempo.AoCairUmRaio += AoCairUmRaio;

        void OnDisable()
        {
            Tempo.AoCairUmRaio -= AoCairUmRaio;
            if (_movimento != null) _movimento.Vento = Vector3.zero;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (_tetoRodando) _jobDoTeto.Complete();
            if (_raiosDoTeto.IsCreated) _raiosDoTeto.Dispose();
            if (_acertosDoTeto.IsCreated) _acertosDoTeto.Dispose();
            Shader.SetGlobalVector(TetoAreaId, Vector4.zero);
            Shader.SetGlobalVector(CarroMeioId, Vector4.zero);
            Shader.SetGlobalFloat(ChuvaId, 0f);
            Shader.SetGlobalFloat(PocasId, 0f);
            Shader.SetGlobalFloat(ClaraoId, 0f);
        }

        static AudioClip Carregar(string nome) => Resources.Load<AudioClip>("Clima/" + nome);

        static AudioClip[] Varios(string prefixo, int n)
        {
            var lista = new List<AudioClip>();
            for (int i = 1; i <= n; i++)
            {
                var c = Carregar(prefixo + i);
                if (c != null) lista.Add(c);
            }
            return lista.ToArray();
        }

        /// <summary>Os respingos de pisar numa poça (os passos tocam).</summary>
        public static AudioClip[] SonsDaPoca => _sonsDaPoca ??= Varios("poca_", 5);

        AudioSource Fonte(string nome, AudioClip clip, out AudioLowPassFilter filtro)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 20;
            s.dopplerLevel = 0f;
            s.bypassReverbZones = true;
            filtro = go.AddComponent<AudioLowPassFilter>();
            filtro.cutoffFrequency = 22000f;
            return s;
        }

        static Material NovoMaterial(Shader shader, Color cor, int forma, float luz, float neblina, bool soma)
        {
            if (shader == null) return null;
            var m = new Material(shader);
            m.SetColor("_Cor", cor);
            m.SetFloat("_Forma", forma);
            m.SetFloat("_Luz", luz);
            m.SetFloat("_Neblina", neblina);
            m.SetFloat("_Origem", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_Destino", soma ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        // ================================================================== a cada quadro

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            Shader.SetGlobalFloat(ChuvaId, Tempo.Chuva);
            Shader.SetGlobalFloat(PocasId, Tempo.Pocas);
            if (cam == null)
            {
                Shader.SetGlobalFloat(ClaraoId, 0f);
                return;
            }
            var ouvido = cam.transform.position;
            if (Time.unscaledTime >= _proximaOlhada)
            {
                _proximaOlhada = Time.unscaledTime + 0.25f;
                OlharOndeEsta(ouvido);
            }
            _abafo = Mathf.MoveTowards(_abafo, _alvoAbafo, dt / 0.6f);
            _noTelhado = Mathf.MoveTowards(_noTelhado, _alvoTelhado, dt / 0.6f);
            _noCarro = Mathf.MoveTowards(_noCarro, _alvoCarro, dt / 0.4f);
            _surdo = Mathf.MoveTowards(_surdo, 1f, dt / 4f);

            Claroes(cam);
            Tetos(cam);
            CaixaDoCarro();
            Chuva(cam, dt);
            Sons(cam, dt);
            Trovoes(cam);
            Tornado(cam, dt);
            Pocas(cam);
        }

        // dentro de uma casa ou de um prédio (ou com um teto em cima), no carro, debaixo da terra (o bunker, o porão)
        void OlharOndeEsta(Vector3 ouvido)
        {
            var local = JogadorRede.Local;
            bool carro = local != null && local.Passageiro != null && local.Passageiro.Sentado;
            bool area = AreaInterna.Contem(ouvido);
            bool teto = Physics.Raycast(ouvido, Vector3.up, out var h, 40f, Abafamento.Mascara, QueryTriggerInteraction.Ignore);
            var mundo = MundoDoJogo.Atual;
            float chao = mundo != null ? mundo.Altura(ouvido.x, ouvido.z) : float.NegativeInfinity;
            bool fundo = (BunkerRede.Instancia != null && BunkerRede.Instancia.Dentro(ouvido)) || (teto && ouvido.y < chao - 1.2f);
            if (fundo)
            {
                _alvoAbafo = 1f;
                Onde = "debaixo da terra";
            }
            else if (area || teto)
            {
                _alvoAbafo = 0.8f;
                Onde = area ? "dentro" : "debaixo de um teto";
            }
            else if (carro)
            {
                _alvoAbafo = 0.62f;
                Onde = "no carro";
            }
            else
            {
                _alvoAbafo = 0f;
                Onde = "fora";
            }
            // a chuva batendo no telhado: com o telhado perto (a casa, o último andar), não no térreo de um prédio alto
            _alvoTelhado = !fundo && (area || teto) && teto && h.distance < 9f ? 1f : (!fundo && (area || teto)) ? 0.35f : 0f;
            _alvoCarro = carro ? 1f : 0f;
        }

        // o quanto o som de fora chega (o volume) e o corte dos agudos
        float SomDeFora => Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(_abafo / 0.8f)) * Mathf.Lerp(1f, 0.08f, Mathf.Clamp01((_abafo - 0.8f) / 0.2f));
        float CorteDeFora => Abafamento.Corte(_abafo);

        // ------------------------------------------------------------------ os sons em laço
        void Sons(Camera cam, float dt)
        {
            float c = Tempo.Chuva;
            float wFina = c < 0.3f ? c / 0.3f : Mathf.Max(0f, 1f - (c - 0.3f) / 0.3f);
            float wMedia = Mathf.Clamp01((c - 0.25f) / 0.3f) * (c < 0.78f ? 1f : Mathf.Max(0f, 1f - (c - 0.78f) / 0.22f));
            float wForte = Mathf.Clamp01((c - 0.68f) / 0.3f);
            float fora = SomDeFora * _surdo;
            float corte = CorteDeFora;
            Laco(_fina, _filtroFina, 0.42f * wFina * fora, corte);
            Laco(_media, _filtroMedia, 0.55f * wMedia * fora, corte);
            Laco(_forte, _filtroForte, 0.7f * wForte * fora, corte);
            Laco(_telhado, null, 0.6f * Mathf.Clamp01(c * 1.4f) * _noTelhado * _surdo, 22000f);
            Laco(_carro, null, 0.65f * Mathf.Clamp01(c * 1.3f) * _noCarro * _surdo, 22000f);
            float v = Tempo.Vento;
            Laco(_vento, _filtroVento, 0.55f * Mathf.Pow(Mathf.Clamp01((v - 0.15f) / 0.85f), 1.3f) * fora, corte);
            if (_vento.isPlaying) _vento.pitch = 0.9f + 0.2f * v;
        }

        static void Laco(AudioSource s, AudioLowPassFilter filtro, float volume, float corte)
        {
            if (s == null || s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, volume, Time.deltaTime * 0.5f);
            if (filtro != null) filtro.cutoffFrequency = Mathf.Lerp(filtro.cutoffFrequency, corte, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            if (s.volume > 0.002f)
            {
                if (!s.isPlaying)
                {
                    s.time = Random.Range(0f, s.clip.length * 0.9f);
                    s.Play();
                }
            }
            else if (s.isPlaying) s.Stop();
        }

        // ------------------------------------------------------------------ a chuva
        void CriarChuva()
        {
            var go = new GameObject("Chuva");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // a caixa solta as gotas para baixo (o +Z dela)
            _chuva = go.AddComponent<ParticleSystem>();
            _chuva.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _chuva.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 1.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(11f, 13.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = Color.white;
            main.maxParticles = 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // sempre simulando: a caixa das gotas fica acima da câmera, fora da vista, e pausada ela nunca começava
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emissao = _chuva.emission;
            emissao.rateOverTime = 0f;
            var forma = _chuva.shape;
            forma.shapeType = ParticleSystemShapeType.Box;
            forma.scale = new Vector3(40f, 40f, 1f);
            var vento = _chuva.velocityOverLifetime;
            vento.enabled = true;
            vento.space = ParticleSystemSimulationSpace.World;
            vento.x = new ParticleSystem.MinMaxCurve(0f);
            vento.y = new ParticleSystem.MinMaxCurve(0f);
            vento.z = new ParticleSystem.MinMaxCurve(0f);
            var colisao = _chuva.collision;
            colisao.enabled = true;
            colisao.type = ParticleSystemCollisionType.World;
            colisao.mode = ParticleSystemCollisionMode.Collision3D;
            colisao.quality = ParticleSystemCollisionQuality.Medium;
            colisao.voxelSize = 0.5f;
            colisao.collidesWith = MascaraDaChuva;
            colisao.lifetimeLoss = 1f;
            colisao.enableDynamicColliders = false;
            colisao.maxCollisionShapes = 128;
            colisao.radiusScale = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = _materialDaChuva;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 2f;

            // o respingo: um anel deitado no chão onde a gota bate (um em cada três)
            var rg = new GameObject("Respingo");
            rg.transform.SetParent(go.transform, false);
            _respingo = rg.AddComponent<ParticleSystem>();
            _respingo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = _respingo.main;
            m2.playOnAwake = false;
            m2.loop = true;
            m2.startLifetime = 0.25f;
            m2.startSpeed = 0f;
            m2.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            m2.maxParticles = 800;
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            m2.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var e2 = _respingo.emission;
            e2.rateOverTime = 0f;
            var f2 = _respingo.shape;
            f2.enabled = false;
            var tam = _respingo.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1.6f));
            var cor = _respingo.colorOverLifetime;
            cor.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            cor.color = g;
            var r2 = rg.GetComponent<ParticleSystemRenderer>();
            r2.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r2.sharedMaterial = _materialDoRespingo;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
            var sub = _chuva.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(_respingo, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, 0.33f);
        }

        void Chuva(Camera cam, float dt)
        {
            if (_chuva == null) return;
            float c = Tempo.Chuva;
            bool liga = c > 0.02f && _abafo < 0.95f;
            // a câmera andando (no carro, a 100 km/h): as gotas nascem mais à frente
            var pos = cam.transform.position;
            if (dt > 0f) _cameraVel = Vector3.Lerp(_cameraVel, (pos - _cameraAntes) / dt, 1f - Mathf.Exp(-dt * 3f));
            if ((pos - _cameraAntes).sqrMagnitude > 2500f) _cameraVel = Vector3.zero;   // teleporte
            _cameraAntes = pos;
            var adiante = new Vector3(_cameraVel.x, 0f, _cameraVel.z) * 0.6f;
            _chuva.transform.position = pos + Vector3.up * 14f + Vector3.ClampMagnitude(adiante, 20f);
            var emissao = _chuva.emission;
            emissao.rateOverTime = liga ? 5500f * c : 0f;
            var main = _chuva.main;
            // finas demais, as gotas sumiam na tela (menos de um pixel a 5 m): no chuvisco, 1,5 a 2,5 cm; na tempestade, 3 a 4,5
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.015f, 0.03f, c), Mathf.Lerp(0.025f, 0.045f, c));
            _materialDaChuva.SetColor("_Cor", new Color(0.85f, 0.88f, 0.92f, Mathf.Lerp(0.28f, 0.45f, c)));
            var r = _chuva.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = Mathf.Lerp(0.025f, 0.055f, c);
            // o vento entorta as gotas (a mesma direção do vento da vegetação)
            var v = Shader.GetGlobalVector(VentoId);
            var dir = new Vector2(v.x, v.y);
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(1f, 0.3f);
            dir.Normalize();
            float forca = 1f + 6f * Tempo.Vento;
            var vento = _chuva.velocityOverLifetime;
            vento.x = new ParticleSystem.MinMaxCurve(dir.x * forca * 0.7f, dir.x * forca * 1.2f);
            vento.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vento.z = new ParticleSystem.MinMaxCurve(dir.y * forca * 0.7f, dir.y * forca * 1.2f);
            if (liga && !_chuva.isPlaying) _chuva.Play(true);
            else if (!liga && _chuva.isPlaying && _chuva.particleCount == 0) _chuva.Stop(true);
        }

        // ------------------------------------------------------------------ os raios
        void CriarRaio()
        {
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Raio " + (i + 1));
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.sharedMaterial = _materialDoRaio;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.alignment = LineAlignment.View;
                lr.enabled = false;
                _riscos.Add(lr);
            }
            var luz = new GameObject("Luz do raio");
            luz.transform.SetParent(transform, false);
            _luzDoRaio = luz.AddComponent<Light>();
            _luzDoRaio.type = LightType.Point;
            _luzDoRaio.range = 160f;
            _luzDoRaio.color = new Color(0.82f, 0.86f, 1f);
            _luzDoRaio.shadows = LightShadows.None;
            _luzDoRaio.intensity = 0f;
            _luzDoRaio.enabled = false;

            var fg = new GameObject("Faíscas do raio");
            fg.transform.SetParent(transform, false);
            _faiscas = fg.AddComponent<ParticleSystem>();
            _faiscas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _faiscas.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.gravityModifier = 1f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var e = _faiscas.emission;
            e.enabled = false;
            var f = _faiscas.shape;
            f.shapeType = ParticleSystemShapeType.Hemisphere;
            f.radius = 0.3f;
            f.rotation = new Vector3(-90f, 0f, 0f);
            var r = fg.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.sharedMaterial = _materialDasFaiscas;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void AoCairUmRaio(Raio raio)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ouvido = cam.transform.position;
            float dist = Vector3.Distance(ouvido, raio.ponto);
            // os três raios (a pedido em 02/11/2026): o que é só o clarão (muito longe, além de 2,8 km: o trovão não chega),
            // o clarão com o trovão longe e o que cai perto (o risco, a luz no chão, as faíscas, o estalo e o estrondo).
            // O clarão: forte perto, fraco longe; dentro, ele entra pelas janelas (mais fraco); no bunker, quase nada
            bool soClarao = raio.tipo == 0 && dist > 2800f;
            float forca = raio.tipo >= 1 ? Mathf.Lerp(1.5f, 0.55f, Mathf.Clamp01(dist / 500f))
                : soClarao ? Random.Range(0.12f, 0.3f) : Mathf.Lerp(0.6f, 0.15f, Mathf.Clamp01((dist - 300f) / 2200f));
            forca *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(_abafo / 0.8f)) * Mathf.Lerp(1f, 0.05f, Mathf.Clamp01((_abafo - 0.8f) / 0.2f));
            var clarao = new Clarao { inicio = Time.time, forca = forca, padrao = Random.Range(0, 3), ponto = raio.ponto };
            if ((raio.tipo >= 1 || dist < 900f) && _abafo < 0.95f)
            {
                clarao.riscos = Riscar(raio.ponto, dist, ouvido);
                clarao.luz = dist < 450f ? Mathf.Lerp(28f, 6f, dist / 450f) : 0f;
                if (dist < 250f && _faiscas != null)
                {
                    _faiscas.transform.position = raio.ponto + Vector3.up * 0.2f;
                    _faiscas.Emit(raio.tipo == 2 ? 120 : 60);
                }
            }
            _claroes.Add(clarao);
            if (soClarao) return;
            // o trovão, atrasado pela distância
            double agora = Time.timeAsDouble;
            double atraso = dist / 343.0;
            if (dist < 150f)
            {
                _trovoes.Add(new Trovao { quando = agora + atraso, clip = Sortear(_estalos), volume = 1f, corte = 22000f, ponto = raio.ponto, zumbido = dist < 50f ? 1f - dist / 50f : 0f });
                _trovoes.Add(new Trovao { quando = agora + atraso + 0.06, clip = Sortear(_trovoesPerto), volume = 0.95f, corte = 22000f, ponto = raio.ponto });
            }
            else if (dist < 750f)
            {
                float t = (dist - 150f) / 600f;
                _trovoes.Add(new Trovao { quando = agora + atraso, clip = Sortear(_trovoesMedio), volume = Mathf.Lerp(1f, 0.75f, t), corte = Mathf.Lerp(14000f, 5000f, t), ponto = raio.ponto });
            }
            else
            {
                // o longe chega com o ronco nos médios (o som de referência do Pedro): sem cortar demais, senão só sobra o
                // grave que a caixa de som pequena não toca
                float t = Mathf.Clamp01((dist - 750f) / 2050f);
                _trovoes.Add(new Trovao { quando = agora + atraso, clip = Sortear(_trovoesLonge), volume = Mathf.Lerp(0.85f, 0.45f, t), corte = Mathf.Lerp(4000f, 1500f, t), ponto = raio.ponto });
            }
        }

        static AudioClip Sortear(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        void Trovoes(Camera cam)
        {
            double agora = Time.timeAsDouble;
            for (int i = _trovoes.Count - 1; i >= 0; i--)
            {
                var t = _trovoes[i];
                if (agora < t.quando) continue;
                _trovoes.RemoveAt(i);
                if (t.clip == null) continue;
                AudioSource fonte = null;
                foreach (var f in _fontesDeTrovao)
                    if (!f.isPlaying) { fonte = f; break; }
                if (fonte == null) fonte = _fontesDeTrovao[Random.Range(0, _fontesDeTrovao.Count)];
                // de que lado ele vem (meio de lado: o trovão enche o céu)
                var d = t.ponto - cam.transform.position;
                d.y = 0f;
                fonte.panStereo = d.sqrMagnitude > 1f ? Vector3.Dot(d.normalized, cam.transform.right) * 0.5f : 0f;
                fonte.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Min(t.corte, Abafamento.Corte(_abafo * 0.9f));
                fonte.pitch = Random.Range(0.92f, 1.06f);
                fonte.clip = t.clip;
                fonte.volume = t.volume * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(_abafo / 0.8f)) * Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((_abafo - 0.8f) / 0.2f));
                fonte.Play();
                // o zumbido no ouvido (o raio bem perto): o resto abaixa e volta aos poucos
                if (t.zumbido > 0f && _zumbido.clip != null && _abafo < 0.9f)
                {
                    _zumbido.volume = 0.5f * t.zumbido * (1f - 0.6f * _abafo);
                    _zumbido.Play();
                    _surdo = Mathf.Min(_surdo, Mathf.Lerp(1f, 0.2f, t.zumbido));
                }
            }
        }

        // o risco do raio: do céu até o ponto, quebrado, com uns galhos
        List<LineRenderer> Riscar(Vector3 ponto, float dist, Vector3 olho)
        {
            var usados = new List<LineRenderer>(3);
            LineRenderer principal = null;
            int livres = 0;
            foreach (var lr in _riscos)
            {
                if (lr.enabled) continue;
                var topo = ponto + Vector3.up * Random.Range(180f, 260f) + new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
                float largura = Mathf.Max(0.35f, dist * 0.004f);
                if (principal == null)
                {
                    Quebrar(lr, topo, ponto, 6, 0.22f);
                    lr.widthMultiplier = largura;
                    lr.enabled = true;
                    principal = lr;
                    usados.Add(lr);
                    continue;
                }
                // um galho: de um ponto alto do risco principal, descendo para o lado
                int n = principal.positionCount;
                var de = principal.GetPosition(Random.Range(n / 8, n / 2));
                var para = de + new Vector3(Random.Range(-60f, 60f), -Random.Range(40f, 90f), Random.Range(-60f, 60f));
                Quebrar(lr, de, para, 4, 0.3f);
                lr.widthMultiplier = largura * 0.45f;
                lr.enabled = true;
                usados.Add(lr);
                if (++livres >= 2) break;
            }
            // a câmera só desenha até 150 m: o raio mais longe é desenhado trazido para 130 m, no mesmo tamanho na tela
            if (dist > 130f)
            {
                float k = 130f / dist;
                foreach (var lr in usados)
                {
                    for (int i = 0; i < lr.positionCount; i++) lr.SetPosition(i, olho + (lr.GetPosition(i) - olho) * k);
                    lr.widthMultiplier *= k;
                }
            }
            return usados;
        }

        static readonly List<Vector3> _pontos = new List<Vector3>();

        static void Quebrar(LineRenderer lr, Vector3 de, Vector3 ate, int niveis, float desvio)
        {
            _pontos.Clear();
            _pontos.Add(de);
            _pontos.Add(ate);
            for (int nivel = 0; nivel < niveis; nivel++)
            {
                for (int i = _pontos.Count - 1; i > 0; i--)
                {
                    var a = _pontos[i - 1];
                    var b = _pontos[i];
                    float comp = Vector3.Distance(a, b);
                    var meio = (a + b) * 0.5f + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)) * comp * desvio;
                    _pontos.Insert(i, meio);
                }
            }
            lr.positionCount = _pontos.Count;
            for (int i = 0; i < _pontos.Count; i++) lr.SetPosition(i, _pontos[i]);
        }

        // as piscadas de um clarão: o primeiro forte e mais um ou dois, e o resto morrendo
        static float Piscar(float t, int padrao)
        {
            if (t < 0f || t > 1.2f) return 0f;
            float a = Pulso(t, 0f, 0.05f) + 0.55f * Pulso(t, 0.12f, 0.05f);
            if (padrao >= 1) a += 0.85f * Pulso(t, 0.23f + 0.04f * padrao, 0.07f);
            if (padrao >= 2) a += 0.4f * Pulso(t, 0.45f, 0.06f);
            return Mathf.Clamp01(a + 0.15f * Mathf.Exp(-t * 4f));
        }

        static float Pulso(float t, float em, float dur) => t >= em ? Mathf.Exp(-(t - em) / dur) : 0f;

        void Claroes(Camera cam)
        {
            float total = 0f, luz = 0f;
            Vector3 pontoDaLuz = Vector3.zero;
            for (int i = _claroes.Count - 1; i >= 0; i--)
            {
                var c = _claroes[i];
                float t = Time.time - c.inicio;
                float p = Piscar(t, c.padrao);
                if (t > 1.2f)
                {
                    if (c.riscos != null) foreach (var lr in c.riscos) lr.enabled = false;
                    _claroes.RemoveAt(i);
                    continue;
                }
                total = Mathf.Max(total, p * c.forca);
                if (c.riscos != null)
                {
                    // o risco some quando a piscada apaga
                    float a = p > 0.12f ? Mathf.Clamp01(p * 1.4f) : 0f;
                    foreach (var lr in c.riscos) lr.startColor = lr.endColor = new Color(1f, 1f, 1f, a);
                }
                if (c.luz > 0f && p * c.luz > luz)
                {
                    luz = p * c.luz;
                    pontoDaLuz = c.ponto;
                }
            }
            Tempo.Clarao = total;
            Shader.SetGlobalFloat(ClaraoId, total);
            if (_luzDoRaio != null)
            {
                _luzDoRaio.enabled = luz > 0.05f;
                _luzDoRaio.intensity = luz;
                if (luz > 0.05f) _luzDoRaio.transform.position = pontoDaLuz + Vector3.up * 18f;
            }
        }

        // ------------------------------------------------------------------ o tornado
        void CriarTornado()
        {
            _tornado = new GameObject("Tornado");
            _tornado.transform.SetParent(transform, false);
            var funil = Shader.Find("ZombieCityCleaner/ClimaFunil");
            if (funil == null) funil = Resources.Load<Shader>("ClimaFunil");
            var malha = MalhaDoFunil();
            var cascas = new[]
            {
                (escala: 1f, giro: 0.32f, cor: new Color(0.22f, 0.21f, 0.2f, 0.85f)),
                (escala: 1.22f, giro: 0.2f, cor: new Color(0.27f, 0.26f, 0.25f, 0.45f)),
                (escala: 0.78f, giro: 0.5f, cor: new Color(0.17f, 0.16f, 0.15f, 0.7f)),
            };
            _cascas.Clear();
            for (int i = 0; i < cascas.Length && funil != null; i++)
            {
                var go = new GameObject("Casca " + (i + 1));
                go.transform.SetParent(_tornado.transform, false);
                go.transform.localScale = new Vector3(cascas[i].escala, 1f, cascas[i].escala);
                go.transform.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = go.AddComponent<MeshRenderer>();
                var m = new Material(funil);
                m.SetColor("_Cor", cascas[i].cor);
                m.SetFloat("_Giro", cascas[i].giro);
                m.SetFloat("_Semente", i * 3.7f);
                m.renderQueue = 2995 - i;
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _cascas.Add(m);
            }
            _poeira = ParticulasDoTornado("Poeira", _materialDaPoeira, ParticleSystemRenderMode.Billboard, 80f, new Vector2(3f, 6f),
                new Vector2(4f, 12f), new Vector2(7f, 18f), 1.1f, 5f, 13f);
            _destrocos = ParticulasDoTornado("Destroços", _materialDosDestrocos, ParticleSystemRenderMode.Mesh, 22f, new Vector2(4f, 8f),
                new Vector2(8f, 20f), new Vector2(0.15f, 0.9f), 1.6f, 9f, 6f);
        }

        ParticleSystem ParticulasDoTornado(string nome, Material material, ParticleSystemRenderMode modo, float taxa, Vector2 vida,
            Vector2 velocidade, Vector2 tamanho, float giro, float sobe, float raio)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(_tornado.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(vida.x, vida.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(velocidade.x, velocidade.y);
            main.startSize = new ParticleSystem.MinMaxCurve(tamanho.x, tamanho.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var e = ps.emission;
            e.rateOverTime = taxa;
            var f = ps.shape;
            f.shapeType = ParticleSystemShapeType.Cone;
            f.angle = 60f;
            f.radius = raio;
            f.rotation = new Vector3(-90f, 0f, 0f);
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.orbitalY = new ParticleSystem.MinMaxCurve(giro);
            v.radial = new ParticleSystem.MinMaxCurve(-1.5f);
            v.y = new ParticleSystem.MinMaxCurve(sobe);
            v.x = new ParticleSystem.MinMaxCurve(0f);
            v.z = new ParticleSystem.MinMaxCurve(0f);
            var cor = ps.colorOverLifetime;
            cor.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            cor.color = g;
            if (modo == ParticleSystemRenderMode.Mesh)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
                main.startRotation3D = true;
            }
            else
            {
                var tam = ps.sizeOverLifetime;
                tam.enabled = true;
                tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = modo;
            if (modo == ParticleSystemRenderMode.Mesh) r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 5f;
            return ps;
        }

        // as cascas do funil: fino no pé, abrindo para cima até a nuvem (300 m), o UV dando a volta (x) e subindo (y)
        static Mesh MalhaDoFunil()
        {
            const int Aneis = 26, Lados = 28;
            const float Altura = 300f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var cor = new List<Color>();
            var tri = new List<int>();
            for (int i = 0; i <= Aneis; i++)
            {
                float h = i / (float)Aneis;
                // um cone que abre para cima (o funil fino parecia um cano) e se espalha na base da nuvem
                float raio = 8f + 26f * h + 170f * Mathf.Pow(h, 2.4f);
                float abre = (26f + 170f * 2.4f * Mathf.Pow(h, 1.4f)) / Altura;   // quanto a parede abre por metro subido
                float alfa = Mathf.SmoothStep(0f, 1f, h / 0.04f) * (1f - Mathf.SmoothStep(0f, 1f, (h - 0.78f) / 0.22f));
                for (int j = 0; j <= Lados; j++)
                {
                    float a = j / (float)Lados * Mathf.PI * 2f;
                    var fora = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(fora * raio + Vector3.up * (h * Altura));
                    n.Add((fora - Vector3.up * abre).normalized);
                    uv.Add(new Vector2(j / (float)Lados, h));
                    cor.Add(new Color(1f, 1f, 1f, alfa));
                }
            }
            for (int i = 0; i < Aneis; i++)
                for (int j = 0; j < Lados; j++)
                {
                    int a = i * (Lados + 1) + j, b = a + Lados + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = new Mesh { name = "Funil do tornado" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetColors(cor);
            m.SetTriangles(tri, 0);
            // enorme de propósito: além dos 150 m da câmera, o funil fica preso no fundo (ClimaFunil.shader), e a caixa não
            // pode deixar a câmera descartar ele antes
            m.bounds = new Bounds(Vector3.up * Altura * 0.5f, new Vector3(4000f, 4000f, 4000f));
            return m;
        }

        void Tornado(Camera cam, float dt)
        {
            bool tem = Tempo.Tornado && Tempo.ForcaDoTornado > 0.001f;
            var local = JogadorRede.Local;
            if (_movimento == null && local != null) _movimento = local.GetComponent<MovimentoPrimeiraPessoa>();
            if (!tem)
            {
                if (_tornado != null && _tornado.activeSelf)
                {
                    _tornado.SetActive(false);
                    _avistado = false;
                }
                PertoDoTornado = Mathf.MoveTowards(PertoDoTornado, 0f, dt * 0.2f);
                Laco(_tornadoSom, _filtroTornado, 0f, 22000f);
                if (_movimento != null) _movimento.Vento = Vector3.zero;
                return;
            }
            if (_tornado == null) CriarTornado();
            if (!_tornado.activeSelf)
            {
                _tornado.SetActive(true);
                _poeira.Play(true);
                _destrocos.Play(true);
            }
            var p = Tempo.PosicaoDoTornado;
            float forca = Tempo.ForcaDoTornado;
            _tornado.transform.position = p;
            foreach (var m in _cascas) m.SetFloat("_Desce", forca);
            var e = _poeira.emission;
            e.rateOverTime = 80f * Mathf.Clamp01((forca - 0.4f) / 0.6f);
            var e2 = _destrocos.emission;
            e2.rateOverTime = 22f * Mathf.Clamp01((forca - 0.6f) / 0.4f);

            // o som: o rugido, mais grave de longe e dentro
            var ouvido = cam.transform.position;
            float dist = Vector3.Distance(new Vector3(ouvido.x, 0f, ouvido.z), new Vector3(p.x, 0f, p.z));
            _tornadoSom.transform.position = p + Vector3.up * 20f;
            float corte = Mathf.Min(22000f * Mathf.Pow(0.05f, Mathf.Clamp01(dist / 700f)), Abafamento.Corte(_abafo * 0.85f));
            Laco(_tornadoSom, _filtroTornado, Mathf.Clamp01(forca * 1.2f) * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(_abafo / 0.8f)) * Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((_abafo - 0.8f) / 0.2f)), corte);
            PertoDoTornado = Mathf.MoveTowards(PertoDoTornado, Mathf.Clamp01(1f - dist / 500f) * forca, dt * 0.3f);
            // avistado: na frente da câmera, a menos de 400 m (a música dá o golpe; uma vez por aparição)
            if (!_avistado && forca > 0.5f && dist < 400f)
            {
                var alvo = p + Vector3.up * 40f - ouvido;
                if (Vector3.Angle(cam.transform.forward, alvo) < 35f && _abafo < 0.9f)
                {
                    _avistado = true;
                    AvistouEm = Time.time;
                    AoAvistarOTornado?.Invoke();
                }
            }

            // o vento puxando quem joga aqui, e o giro no miolo (o dano é do anfitrião). Sentado num carro, numa moto ou no
            // caiaque, quem leva é o veículo (Vehicles/Carro.Tornado.cs, Caiaque)
            if (_movimento == null || local == null) return;
            bool sentado = (local.Passageiro != null && local.Passageiro.Sentado) || PassageiroDoCaiaque.Sentado(local);
            bool vivo = local.Vida == null || local.Vida.Vida.Value > 0f;
            var pes = local.transform.position;
            var d = p - pes;
            d.y = 0f;
            float distPes = d.magnitude;
            if (sentado || !vivo || distPes > PuxaAPe || forca < 0.2f)
            {
                _movimento.Vento = Vector3.zero;
                _girandoAte = -1f;
                return;
            }
            if (Time.time >= _proximaProtecao && CicloDoDia.Instancia != null)
            {
                _proximaProtecao = Time.time + 0.25f;
                _protecao = CicloDoDia.Instancia.ProtecaoContraOTornado(pes);
            }
            // (07/11/2026, a pedido: "quando passar perto, ele vai puxar com mais força ... se eu estiver a pé, a força dele
            // é mais forte ainda") girando no miolo: ele sobe e roda em volta do tornado (a velocidade é a do giro) por um ou
            // dois segundos, e no fim é jogado para fora; depois, por uns segundos, só puxa
            if (_girandoAte > 0f)
            {
                if (Time.time < _girandoAte && _protecao < 0.5f && forca > 0.3f)
                {
                    _movimento.Vento = Vector3.zero;
                    _movimento.Arremessar(Redemoinho.Giro(pes, _chaoDoGiro, 4.5f, 11f, _alturaDoGiro));
                    return;
                }
                _girandoAte = -1f;
                _proximoGiro = Time.time + 5f;
                _proximoArremesso = Time.time + 2.5f;
                _movimento.Arremessar(Redemoinho.Arremesso(pes, 13f, 6f, 7f));
            }
            // o puxão: até 90 m, e forte perto (a uns 20 m, mais que o passo correndo)
            _movimento.Vento = Redemoinho.Puxao(pes, PuxaAPe, ForcaAPe) * (1f - _protecao);
            if (distPes >= MioloAPe || forca <= 0.5f || _protecao >= 1f) return;
            if (_protecao < 0.5f && Time.time >= _proximoGiro)
            {
                // no aberto: pega e gira
                _girandoAte = Time.time + Random.Range(1.6f, 2.6f);
                _chaoDoGiro = pes.y;
                _alturaDoGiro = Random.Range(4f, 7f);
                _movimento.Arremessar(Vector3.up * 6f);
            }
            else if (_protecao >= 0.5f && Time.time >= _proximoArremesso)
            {
                // dentro de casa: só o tranco de antes, mais fraco
                var para = distPes > 0.1f ? d / distPes : Vector3.forward;
                var gira = Vector3.Cross(Vector3.up, para);
                _proximoArremesso = Time.time + 2.5f;
                float leve = 1f - _protecao * 0.6f;
                _movimento.Arremessar((Vector3.up * Random.Range(11f, 14f) + gira * 11f - para * 5f) * leve);
            }
        }

        // ------------------------------------------------------------------ as poças
        static Mesh MalhaDaPoca()
        {
            var m = new Mesh { name = "Poça" };
            m.SetVertices(new List<Vector3> { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f) });
            m.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        static uint Misturar(int x, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ 0x5bd1e995u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;
                return h;
            }
        }

        static long Chave(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        // a poça daquela célula (o sorteio é fixo: igual em todas as máquinas); a que ainda não tem chão embaixo (o terreno
        // carregando) não fica guardada
        static Poca PocaDa(int cx, int cz)
        {
            long chave = Chave(cx, cz);
            if (_pocas.TryGetValue(chave, out var p)) return p;
            uint h = Misturar(cx, cz);
            p = new Poca();
            if ((h & 1023u) / 1023f > 0.24f)
            {
                _pocas[chave] = p;
                return p;
            }
            float ox = ((h >> 10) & 255u) / 255f, oz = ((h >> 18) & 255u) / 255f;
            float raio = Mathf.Lerp(0.9f, 2.1f, ((h >> 26) & 63u) / 63f);
            var c = new Vector3((cx + 0.2f + 0.6f * ox) * Celula, 0f, (cz + 0.2f + 0.6f * oz) * Celula);
            // (12/11/2026: o mundo do jogo; no Lakewood o chão passa de 500 m, e de y = 300 o raio nascia dentro do morro)
            var mundo = MundoDoJogo.Atual;
            bool temChao = mundo != null;
            float topo = temChao ? mundo.Altura(c.x, c.z) + 3f : 300f;
            if (!Physics.Raycast(new Vector3(c.x, topo, c.z), Vector3.down, out var chao, temChao ? 8f : 600f, Abafamento.Mascara, QueryTriggerInteraction.Ignore))
                return p;   // sem chão ainda: não guarda
            if (_pocas.Count > 40000) _pocas.Clear();
            _pocas[chave] = p;
            if (chao.normal.y < 0.985f || AreaInterna.Contem(chao.point + Vector3.up)) return p;
            // no terreno, só nas ruas (o asfalto fica 5 cm acima do terreno, sem colisor); no gramado, a água entra no chão
            float acima = 0.02f;
            if (PassosJogador.EhTerreno(chao.collider))
            {
                if (!PassosJogador.NaEstrada(chao.point)) return p;
                acima = 0.075f;
            }
            // plano em volta (a poça não pode boiar num degrau nem entrar num barranco)
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                var q = chao.point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * raio * 0.8f;
                if (!Physics.Raycast(q + Vector3.up * 1f, Vector3.down, out var h2, 2f, Abafamento.Mascara, QueryTriggerInteraction.Ignore)) return p;
                if (Mathf.Abs(h2.point.y - chao.point.y) > 0.06f) return p;
            }
            p.existe = true;
            p.centro = chao.point + Vector3.up * acima;
            p.raio = raio;
            p.giro = ((h >> 4) & 63u) / 63f * 360f;
            _pocas[chave] = p;
            return p;
        }

        /// <summary>
        /// Esse ponto (um pé) está numa poça cheia? Os passos usam (Player/PassosJogador): o respingo no lugar do passo e, no
        /// anfitrião, o barulho que chama os zumbis mais perto.
        /// </summary>
        public static bool NaPoca(Vector3 p)
        {
            float cheia = Tempo.Pocas;
            if (cheia < 0.35f) return false;
            int cx = Mathf.FloorToInt(p.x / Celula), cz = Mathf.FloorToInt(p.z / Celula);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var poca = PocaDa(cx + dx, cz + dz);
                    if (!poca.existe) continue;
                    var d = p - poca.centro;
                    if (Mathf.Abs(d.y) > 0.5f) continue;
                    d.y = 0f;
                    if (d.magnitude < poca.raio * cheia * 0.55f) return true;
                }
            return false;
        }

        void Pocas(Camera cam)
        {
            if (Time.unscaledTime < _proximasPocas || _materialDaPoca == null) return;
            _proximasPocas = Time.unscaledTime + 0.3f;
            _pocasSaindo.Clear();
            if (Tempo.Pocas < 0.02f)
            {
                foreach (var kv in _pocasNaTela) Devolver(kv.Value);
                _pocasNaTela.Clear();
                return;
            }
            var pos = cam.transform.position;
            int cx = Mathf.FloorToInt(pos.x / Celula), cz = Mathf.FloorToInt(pos.z / Celula);
            const int Alcance = 7;   // 7 células para cada lado: uns 50 m
            foreach (var kv in _pocasNaTela)
            {
                int x = (int)(kv.Key >> 32), z = (int)(uint)kv.Key;
                if (Mathf.Abs(x - cx) > Alcance + 1 || Mathf.Abs(z - cz) > Alcance + 1) _pocasSaindo.Add(kv.Key);
            }
            foreach (var k in _pocasSaindo)
            {
                Devolver(_pocasNaTela[k]);
                _pocasNaTela.Remove(k);
            }
            int novas = 0;
            for (int z = cz - Alcance; z <= cz + Alcance; z++)
                for (int x = cx - Alcance; x <= cx + Alcance; x++)
                {
                    long chave = Chave(x, z);
                    if (_pocasNaTela.ContainsKey(chave)) continue;
                    if (!_pocas.ContainsKey(chave) && ++novas > 30) continue;   // poucas contas por vez
                    var p = PocaDa(x, z);
                    if (!p.existe) continue;
                    var go = Pegar();
                    go.transform.SetPositionAndRotation(p.centro, Quaternion.Euler(0f, p.giro, 0f));
                    go.transform.localScale = new Vector3(p.raio, 1f, p.raio);
                    go.SetActive(true);
                    _pocasNaTela[chave] = go;
                }
        }

        GameObject Pegar()
        {
            if (_pocasLivres.Count > 0)
            {
                var g = _pocasLivres[_pocasLivres.Count - 1];
                _pocasLivres.RemoveAt(_pocasLivres.Count - 1);
                return g;
            }
            var go = new GameObject("Poça");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _malhaDaPoca;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _materialDaPoca;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void Devolver(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            _pocasLivres.Add(go);
        }

        // ------------------------------------------------------------------ os tetos (a chuva não entra nas casas nem no carro)
        // A colisão das gotas (a das partículas) deixava passar muita gota pelos telhados finos e pelo teto do carro (a pedido
        // em 02/11/2026: "a chuva também ultrapassa construções como dentro de casas e dentro do carro"). Agora, a cada 0,25 s,
        // 64 x 64 raios de cima para baixo (em paralelo, fora do quadro) acham o que cobre cada célula de 0,75 m em volta da
        // câmera (48 m de lado): o telhado, a laje, o teto do carro, o chão. O shader das partículas (ClimaParticula, com _Teto)
        // apaga a gota e o respingo que estiverem abaixo dele. O carro em que se está entra também como uma caixa (ele anda
        // rápido demais para o mapa acompanhar).
        const int LadoDoTeto = 64;
        const float CelulaDoTeto = 0.75f;
        static readonly int TetoId = Shader.PropertyToID("_ZccTeto");
        static readonly int TetoAreaId = Shader.PropertyToID("_ZccTetoArea");
        static readonly int CarroId = Shader.PropertyToID("_ZccCarro");
        static readonly int CarroMeioId = Shader.PropertyToID("_ZccCarroMeio");
        Texture2D _tetos;
        NativeArray<RaycastCommand> _raiosDoTeto;
        NativeArray<RaycastHit> _acertosDoTeto;
        JobHandle _jobDoTeto;
        bool _tetoRodando;
        Vector2 _cantoDoTeto;
        float _proximoTeto;
        readonly float[] _alturasDosTetos = new float[LadoDoTeto * LadoDoTeto];
        readonly Dictionary<Vehicles.Carro, Bounds> _caixasDosCarros = new Dictionary<Vehicles.Carro, Bounds>();

        void Tetos(Camera cam)
        {
            if (_tetoRodando)
            {
                _jobDoTeto.Complete();
                _tetoRodando = false;
                for (int i = 0; i < _alturasDosTetos.Length; i++)
                {
                    var h = _acertosDoTeto[i];
                    _alturasDosTetos[i] = h.distance > 0f ? h.point.y : -10000f;
                }
                _tetos.SetPixelData(_alturasDosTetos, 0);
                _tetos.Apply(false, false);
                Shader.SetGlobalVector(TetoAreaId, new Vector4(_cantoDoTeto.x, _cantoDoTeto.y, 1f / (LadoDoTeto * CelulaDoTeto), 1f));
            }
            if (Tempo.Chuva < 0.01f || Time.unscaledTime < _proximoTeto) return;
            _proximoTeto = Time.unscaledTime + 0.25f;
            var pos = cam.transform.position;
            float metade = LadoDoTeto * CelulaDoTeto * 0.5f;
            _cantoDoTeto = new Vector2(Mathf.Floor((pos.x - metade) / CelulaDoTeto) * CelulaDoTeto, Mathf.Floor((pos.z - metade) / CelulaDoTeto) * CelulaDoTeto);
            float alto = pos.y + 100f;
            var consulta = new QueryParameters(MascaraDaChuva, false, QueryTriggerInteraction.Ignore, false);
            for (int z = 0; z < LadoDoTeto; z++)
                for (int x = 0; x < LadoDoTeto; x++)
                {
                    var de = new Vector3(_cantoDoTeto.x + (x + 0.5f) * CelulaDoTeto, alto, _cantoDoTeto.y + (z + 0.5f) * CelulaDoTeto);
                    _raiosDoTeto[z * LadoDoTeto + x] = new RaycastCommand(de, Vector3.down, consulta, 220f);
                }
            _jobDoTeto = RaycastCommand.ScheduleBatch(_raiosDoTeto, _acertosDoTeto, 64);
            _tetoRodando = true;
        }

        // o carro em que quem joga aqui está: a caixa dele (do mundo para dentro dela) para o shader apagar a chuva lá dentro
        void CaixaDoCarro()
        {
            var local = JogadorRede.Local;
            var carro = local != null && local.Passageiro != null ? local.Passageiro.Carro : null;
            if (carro == null)
            {
                Shader.SetGlobalVector(CarroMeioId, Vector4.zero);
                return;
            }
            if (!_caixasDosCarros.TryGetValue(carro, out var caixa))
            {
                caixa = CaixaDe(carro.transform);
                _caixasDosCarros[carro] = caixa;
            }
            var t = carro.transform;
            var meio = Vector3.Scale(caixa.extents, t.lossyScale);
            var paraDentro = Matrix4x4.TRS(t.TransformPoint(caixa.center), t.rotation, Vector3.one).inverse;
            Shader.SetGlobalMatrix(CarroId, paraDentro);
            Shader.SetGlobalVector(CarroMeioId, new Vector4(meio.x, meio.y, meio.z, 1f));
        }

        // a caixa do carro, no espaço dele: as malhas da lataria (sem as que passam de 7 m, como os fachos dos faróis)
        static Bounds CaixaDe(Transform carro)
        {
            bool tem = false;
            var caixa = new Bounds();
            foreach (var mf in carro.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled) continue;
                if (r.bounds.size.x > 7f || r.bounds.size.y > 7f || r.bounds.size.z > 7f) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var lado = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    var local = carro.InverseTransformPoint(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, lado)));
                    if (!tem)
                    {
                        caixa = new Bounds(local, Vector3.zero);
                        tem = true;
                    }
                    else caixa.Encapsulate(local);
                }
            }
            if (!tem) caixa = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 1.6f, 4.6f));
            return caixa;
        }

        /// <summary>Para o F1: onde quem joga está, o abafado e o tornado.</summary>
        public string Resumo()
        {
            var s = $"Aqui: {Onde} (abafado {_abafo:0.00})\n{(_chuva != null ? _chuva.particleCount : 0)} gotas · {_pocasNaTela.Count} poças";
            if (Tempo.Tornado) s += $" · tornado {PertoDoTornado:0.00}{(_avistado ? ", avistado" : "")}";
            return s;
        }
    }
}
