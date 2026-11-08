using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As marcas na água (07/11/2026, a natação): a ondinha que abre em volta de quem nada (um anel deitado na água, que
    /// cresce e some) e o respingo (as gotas que sobem e caem). Em cada máquina, só visual. Os sistemas de partículas são
    /// feitos uma vez, com o material das partículas do <see cref="Efeitos"/> da cena (sem ele, nada aparece).
    /// </summary>
    public static class AguaNaSuperficie
    {
        static ParticleSystem _ondas, _respingo;

        /// <summary>Uma ondinha no ponto (na altura da água); 'forca' 1 é a de uma braçada (o anel chega a ~2,2 m).</summary>
        public static void Onda(Vector3 ponto, float forca)
        {
            if (!Preparar()) return;
            var p = new ParticleSystem.EmitParams
            {
                position = ponto,
                startSize = 2.2f * forca,
                startLifetime = Random.Range(1.2f, 1.6f),
                startColor = Cor(0.55f),
                rotation = Random.Range(0f, 360f),
            };
            _ondas.Emit(p, 1);
        }

        /// <summary>As gotas de um respingo no ponto: 'quantas', subindo com a 'forca' (1, um tchibum).</summary>
        public static void Respingo(Vector3 ponto, int quantas, float forca)
        {
            if (!Preparar() || quantas <= 0) return;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < quantas; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                p.position = ponto + new Vector3(r.x, 0f, r.y) * 0.35f;
                p.velocity = new Vector3(r.x * 1.6f, Random.Range(1.6f, 3.6f), r.y * 1.6f) * forca;
                p.startSize = Random.Range(0.035f, 0.085f) * Mathf.Lerp(1f, 1.4f, forca - 0.5f);
                p.startLifetime = Random.Range(0.45f, 0.85f);
                p.startColor = Cor(0.85f);
                _respingo.Emit(p, 1);
            }
        }

        // a água clara de dia; de noite, mais escura (o material é sem luz e brilharia no escuro)
        static Color Cor(float alfa)
        {
            var c = Color.Lerp(new Color(0.86f, 0.93f, 0.97f), new Color(0.32f, 0.38f, 0.44f), CicloDoDia.Noite);
            c.a = alfa;
            return c;
        }

        static bool Preparar()
        {
            if (_ondas != null && _respingo != null) return true;
            var efeitos = Object.FindAnyObjectByType<Efeitos>();
            if (efeitos == null || efeitos.materialParticulas == null) return false;
            var pai = new GameObject("Água: ondas e respingos").transform;
            pai.SetParent(efeitos.transform, false);

            // as ondas: um anel deitado (HorizontalBillboard) que cresce de 25% a 100% do tamanho e some
            _ondas = Sistema(pai, "Ondas", 120, 0f);
            var cresce = _ondas.sizeOverLifetime;
            cresce.enabled = true;
            cresce.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 2.2f), new Keyframe(1f, 1f, 0.2f, 0f)));
            var some = _ondas.colorOverLifetime;
            some.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            some.color = g;
            var desenhoDasOndas = _ondas.GetComponent<ParticleSystemRenderer>();
            desenhoDasOndas.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var anel = new Material(efeitos.materialParticulas) { name = "Onda na água" };
            var textura = TexturaDoAnel();
            anel.SetTexture("_BaseMap", textura);
            anel.mainTexture = textura;
            desenhoDasOndas.sharedMaterial = anel;

            // o respingo: gotas que caem com a gravidade
            _respingo = Sistema(pai, "Respingo", 300, 1.5f);
            var desenhoDoRespingo = _respingo.GetComponent<ParticleSystemRenderer>();
            desenhoDoRespingo.sharedMaterial = efeitos.materialParticulas;
            return true;
        }

        static ParticleSystem Sistema(Transform pai, string nome, int maximo, float gravidade)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startSpeed = 0f;
            main.gravityModifier = gravidade;
            main.maxParticles = maximo;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emissao = ps.emission;
            emissao.enabled = false;
            var forma = ps.shape;
            forma.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // um anel macio (64 px): a borda clara entre 70% e 95% do raio, sumindo para dentro e para fora
        static Texture2D TexturaDoAnel()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Anel da onda" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.84f) / 0.12f);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
