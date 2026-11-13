using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os vaga-lumes (a etapa 4, 12/11/2026), em cada máquina (só visuais, nada vai pela rede): ao anoitecer e à noite, no
    /// campo, na rebrota e na beira da mata, até <see cref="Quantos"/> pontinhos em volta da câmera (de 4 a 40 m), boiando
    /// devagar perto do chão (de 0,3 a 2 m). Cada um pisca no seu ritmo (o brilho dura uns 0,3 s; o apagado, de 1 a 4 s).
    /// A chuva apaga todos (eles somem com ela). O sapo perto come o vaga-lume que passa (a língua: Bicho.Lingua).
    /// Desenhados por um ParticleSystem (o material de partícula sem luz, a cor com o brilho alto: o bloom acende).
    /// </summary>
    public sealed class VagaLumes : MonoBehaviour
    {
        public const int Quantos = 70;
        const float Perto = 4f, Longe = 40f;

        struct VagaLume
        {
            public Vector3 pos, rumo;
            public float fase, ritmo, aceso, vivo;
        }

        readonly VagaLume[] _todos = new VagaLume[Quantos];
        ParticleSystem.Particle[] _particulas;
        ParticleSystem _ps;
        float _forca;
        static Texture2D _ponto;

        void Awake()
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = Quantos;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1e6f;
            var emissao = _ps.emission;
            emissao.enabled = false;
            var forma = _ps.shape;
            forma.enabled = false;
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            var m = new Material(shader) { name = "Vaga-lumes" };
            m.SetTexture("_BaseMap", Ponto());
            m.SetColor("_BaseColor", Color.white);
            // aditivo: o brilho soma com o escuro
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3100;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            r.sharedMaterial = m;
            _particulas = new ParticleSystem.Particle[Quantos];
            for (int i = 0; i < Quantos; i++) _todos[i].vivo = 0f;
            _ps.Play();
        }

        // o pontinho: o meio claro e a borda suave
        static Texture2D Ponto()
        {
            if (_ponto != null) return _ponto;
            const int n = 32;
            _ponto = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vaga-lume" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (0.4f + 0.6f * Mathf.Clamp01(1f - d * 3f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            _ponto.SetPixels32(px);
            _ponto.Apply(false, true);
            return _ponto;
        }

        void Update()
        {
            var cam = Camera.main;
            float dt = Time.deltaTime, t = Time.time;
            // a força: do anoitecer à noite; a chuva apaga
            float noite = Mathf.InverseLerp(0.35f, 0.7f, CicloDoDia.Noite);
            float alvo = noite * (1f - Mathf.Clamp01(Tempo.Chuva * 3f));
            _forca = Mathf.MoveTowards(_forca, alvo, dt * 0.2f);
            if (cam == null || _forca < 0.01f)
            {
                if (_ps.particleCount > 0) _ps.SetParticles(_particulas, 0);
                return;
            }
            Vector3 c = cam.transform.position;
            bool temMapa = FaunaDoMundo.TemMapa;
            int vivos = Mathf.RoundToInt(Quantos * _forca);
            int n = 0;
            for (int i = 0; i < Quantos; i++)
            {
                ref var v = ref _todos[i];
                if (i >= vivos) { v.vivo = 0f; continue; }
                // longe demais (a câmera andou): renasce em volta
                if (v.vivo <= 0f || (v.pos - c).sqrMagnitude > Longe * Longe * 1.3f)
                {
                    if (!Nascer(ref v, c, temMapa)) continue;
                }
                // boia: o rumo muda devagar, perto do chão
                v.rumo = Vector3.Lerp(v.rumo, Random.insideUnitSphere * 0.5f, dt * 0.3f);
                v.pos += v.rumo * dt;
                float chao = temMapa ? FaunaDoMundo.AlturaDoMapa(v.pos.x, v.pos.z) : c.y - 1.6f;
                v.pos.y = Mathf.Clamp(v.pos.y, chao + 0.3f, chao + 2f);
                // o pisca: aceso por ~0,3 s a cada ciclo
                v.fase += dt / v.ritmo;
                if (v.fase > 1f) v.fase -= 1f;
                float brilho = v.fase < 0.3f / v.ritmo ? Mathf.Sin(v.fase * v.ritmo / 0.3f * Mathf.PI) : 0f;
                v.aceso = brilho;
                if (brilho <= 0.01f) continue;
                var p = new ParticleSystem.Particle
                {
                    position = v.pos,
                    startSize = 0.12f,
                    startColor = new Color(0.75f, 1f, 0.3f, brilho * _forca),
                    startLifetime = 1e6f,
                    remainingLifetime = 1e6f,
                };
                _particulas[n++] = p;
            }
            _ps.SetParticles(_particulas, n);
            Sapos();
        }

        bool Nascer(ref VagaLume v, Vector3 c, bool temMapa)
        {
            for (int k = 0; k < 4; k++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(Perto, Longe);
                var p = new Vector3(c.x + r.x, 0f, c.z + r.y);
                if (temMapa)
                {
                    if (FaunaDoMundo.AguaEm(p.x, p.z, out _) > 0.05f) continue;
                    var m = FaunaDoMundo.ManchaEm(p.x, p.z);
                    if (m != Mancha.Campo && m != Mancha.Rebrota && m != Mancha.Beira && m != Mancha.Mista && m != Mancha.Lavoura && m != Mancha.Outono) continue;
                    p.y = FaunaDoMundo.AlturaDoMapa(p.x, p.z) + Random.Range(0.3f, 2f);
                }
                else p.y = c.y + Random.Range(-1f, 0.5f);
                v.pos = p;
                v.rumo = Random.insideUnitSphere * 0.4f;
                v.ritmo = Random.Range(1.3f, 4.3f);
                v.fase = Random.value;
                v.vivo = 1f;
                return true;
            }
            v.vivo = 0f;
            return false;
        }

        // o sapo come o vaga-lume que passa perto da boca (às vezes)
        float _proximoSapo;
        void Sapos()
        {
            if (Time.time < _proximoSapo) return;
            _proximoSapo = Time.time + 0.5f;
            var f = FaunaDoMundo.Instancia;
            if (f == null) return;
            foreach (var b in f.Ativos)
            {
                if (b == null || b.Morto || b.Especie != Especie.Sapo) continue;
                Vector3 boca = b.transform.position + Vector3.up * 0.1f;
                for (int i = 0; i < Quantos; i++)
                {
                    ref var v = ref _todos[i];
                    if (v.vivo <= 0f || (v.pos - boca).sqrMagnitude > 1.3f * 1.3f || Random.value > 0.3f) continue;
                    b.Lingua(v.pos);
                    v.vivo = 0f;
                    break;
                }
            }
        }
    }
}
