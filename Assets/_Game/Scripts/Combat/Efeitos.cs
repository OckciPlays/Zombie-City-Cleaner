using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Efeitos de tiro compartilhados: clarão do cano (luz rápida + brilho), faíscas no impacto e sons gerados por
    /// código (tiro, clique sem bala, carregador, bomba e cartucho da espingarda, lanterna). O tiro de cada arma vem do
    /// <see cref="SomDeTiro"/>, em variações.
    /// Fica num objeto da cena, com os materiais das partículas.
    /// </summary>
    public class Efeitos : MonoBehaviour
    {
        public Material materialParticulas;
        public Material materialClarao;

        static Efeitos _instancia;
        /// <summary>As variações do tiro de cada arma (na ordem de <see cref="TipoDeArma"/>).</summary>
        public static AudioClip[][] Tiros;
        public static AudioClip Clique, Recarga, Carregador, Bomba, Cartucho, Lanterna;

        readonly Queue<AudioSource> _fontes = new Queue<AudioSource>();
        ParticleSystem _faiscas, _poeira;

        void Awake()
        {
            _instancia = this;
            Tiros = new AudioClip[Armas.Quantas][];
            for (int i = 0; i < Armas.Quantas; i++) Tiros[i] = SomDeTiro.Gerar("Tiro d" + Armas.Todas[i].nomeComArtigo, Armas.Todas[i].som);
            Clique = Gerar("Clique", 0.06f, (t, r) => (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 160f) * 0.7f);
            Recarga = Gerar("Recarga", 0.5f, (t, r) => (float)(r.NextDouble() * 2 - 1) * (Mathf.Exp(-t * 120f) + Mathf.Exp(-Mathf.Max(0, t - 0.28f) * 140f) * (t > 0.28f ? 1 : 0)) * 0.6f);
            Lanterna = Gerar("Lanterna", 0.04f, (t, r) => (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 220f) * 0.5f);
            // o carregador saindo ou entrando: um estalo seco e o metal batendo logo depois
            Carregador = Gerar("Carregador", 0.12f, (t, r) => (float)(r.NextDouble() * 2 - 1) * (Mathf.Exp(-t * 180f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, t - 0.035f) * 150f) * (t > 0.035f ? 1 : 0)) * 0.6f
                                                         + Mathf.Sin(2f * Mathf.PI * 2100f * t) * Mathf.Exp(-t * 90f) * 0.15f);
            // a bomba da espingarda: para trás (no começo) e para a frente (0,3 s depois), dois golpes de metal
            Bomba = Gerar("Bomba", 0.45f, (t, r) => Golpe(t, r, 0f, 1f) + Golpe(t, r, 0.3f, 1.2f));
            // o cartucho entrando no tubo: um clique curto
            Cartucho = Gerar("Cartucho", 0.08f, (t, r) => Golpe(t, r, 0f, 0.6f));
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Som");
                go.transform.SetParent(transform);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = 60f;
                go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
                _fontes.Enqueue(s);
            }
            _faiscas = Particulas("Faíscas", new Color(1f, 0.8f, 0.4f), 0.03f, 0.25f, 6f);
            _poeira = Particulas("Poeira", new Color(0.55f, 0.52f, 0.48f), 0.07f, 0.6f, 1.2f);
        }

        static AudioClip Gerar(string nome, float duracao, System.Func<float, System.Random, float> onda)
        {
            const int taxa = 44100;
            int n = Mathf.RoundToInt(duracao * taxa);
            var dados = new float[n];
            var r = new System.Random(nome.GetHashCode());
            float filtro = 0f;
            for (int i = 0; i < n; i++)
            {
                filtro = Mathf.Lerp(filtro, onda(i / (float)taxa, r), 0.35f);   // passa-baixa simples: som mais grave
                dados[i] = filtro;
            }
            var clip = AudioClip.Create(nome, n, 1, taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }

        // um golpe de metal: ruído que some rápido e um tilintar
        static float Golpe(float t, System.Random r, float em, float forca)
        {
            float d = t - em;
            if (d < 0f) return 0f;
            return ((float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-d * 120f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1700f * d) * Mathf.Exp(-d * 60f) * 0.25f) * forca;
        }

        ParticleSystem Particulas(string nome, Color cor, float tamanho, float vida, float velocidade)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.startLifetime = vida; main.startSpeed = velocidade;
            main.startSize = tamanho; main.startColor = cor; main.gravityModifier = 0.6f; main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emissao = ps.emission; emissao.enabled = false;
            var forma = ps.shape; forma.shapeType = ParticleSystemShapeType.Cone; forma.angle = 35f; forma.radius = 0.01f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = materialParticulas;
            return ps;
        }

        /// <summary>
        /// Toca um som. Espacial = 3D no ponto, ouvido até o alcance e mais abafado quanto mais longe; senão, sai
        /// "dentro da cabeça" (o próprio tiro).
        /// </summary>
        public static void Som(AudioClip clip, Vector3 posicao, float volume = 1f, bool espacial = true, float alcance = 60f)
        {
            if (_instancia == null || clip == null) return;
            var s = _instancia._fontes.Dequeue(); _instancia._fontes.Enqueue(s);
            s.transform.position = posicao;
            s.spatialBlend = espacial ? 1f : 0f;
            s.maxDistance = alcance;
            var cam = Camera.main;
            float longe = espacial && cam != null ? Mathf.Clamp01(Vector3.Distance(cam.transform.position, posicao) / alcance) : 0f;
            s.GetComponent<AudioLowPassFilter>().cutoffFrequency = 22000f * Mathf.Pow(0.08f, longe);
            s.pitch = Random.Range(0.94f, 1.06f);
            s.PlayOneShot(clip, volume);
        }

        /// <summary>Toca uma das variações, sorteada.</summary>
        public static void Som(AudioClip[] clips, Vector3 posicao, float volume = 1f, bool espacial = true, float alcance = 60f)
        {
            if (clips == null || clips.Length == 0) return;
            Som(clips[Random.Range(0, clips.Length)], posicao, volume, espacial, alcance);
        }

        /// <summary>Clarão do cano: uma luz de um instante e um brilho na boca da arma (na camada informada).</summary>
        public static void Clarao(Transform boca, int camada)
        {
            if (_instancia == null || boca == null) return;
            _instancia.StartCoroutine(_instancia.ClaraoRotina(boca, camada));
        }

        System.Collections.IEnumerator ClaraoRotina(Transform boca, int camada)
        {
            var go = new GameObject("Clarão");
            go.layer = camada;
            go.transform.SetPositionAndRotation(boca.position, boca.rotation);
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point; luz.color = new Color(1f, 0.75f, 0.4f); luz.intensity = 5f; luz.range = 6f; luz.shadows = LightShadows.None;
            var brilho = GameObject.CreatePrimitive(PrimitiveType.Quad);
            // na hora: com Destroy o colisor só sumiria no fim do quadro, e o raio do tiro, feito logo depois, podia batê-lo
            DestroyImmediate(brilho.GetComponent<Collider>());
            brilho.layer = camada;
            brilho.transform.SetParent(go.transform, false);
            brilho.transform.localScale = Vector3.one * 0.12f;
            var r = brilho.GetComponent<MeshRenderer>();
            r.sharedMaterial = materialClarao; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 3; i++)
            {
                if (boca != null) go.transform.position = boca.position;
                var cam = Camera.main;
                if (cam != null) brilho.transform.rotation = Quaternion.LookRotation(brilho.transform.position - cam.transform.position) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                luz.intensity *= 0.6f;
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Marca de tiro no cenário: faíscas e poeira saindo da superfície.</summary>
        public static void Impacto(Vector3 ponto, Vector3 normal)
        {
            if (_instancia == null) return;
            foreach (var (ps, n) in new[] { (_instancia._faiscas, 8), (_instancia._poeira, 10) })
            {
                ps.transform.SetPositionAndRotation(ponto + normal * 0.02f, Quaternion.LookRotation(normal));
                ps.Emit(n);
            }
        }
    }
}
