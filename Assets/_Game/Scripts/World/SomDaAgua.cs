using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Music;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A água em volta (19/11/2026, com o Nature Sound FX): perto do lago, as ondinhas na margem; perto de um rio, a
    /// correnteza. É um som 3D no ponto de água mais perto de quem ouve (até <see cref="Raio"/> m), que anda com ele.
    /// O volume é o do ambiente (Configurações > Sons de ambiente). Dentro de casa ele fica abafado, e a porta do bunker
    /// fechada separa. Os sons ficam em Resources/Agua (Agua_Lago, Agua_Rio_1 e Agua_Rio_2); sem eles, nada toca.
    /// </summary>
    public class SomDaAgua : MonoBehaviour
    {
        /// <summary>Até onde se procura a água (m).</summary>
        public const float Raio = 40f;
        const float VolumeDoLago = 0.8f, VolumeDoRio = 0.75f;

        static SomDaAgua _instancia;
        /// <summary>Qual dos rios toca (o F1 troca: 0 ou 1).</summary>
        public static int QualRio;

        Agua _lago, _rio;
        float _proxima;

        sealed class Agua
        {
            public AudioSource fonte;
            public AudioLowPassFilter filtro;
            public Vector3 ponto;
            public float alvo, volume;
            public bool temPonto;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Criar()
        {
            if (_instancia != null || Resources.Load<AudioClip>("Agua/Agua_Lago") == null) return;
            var go = new GameObject("Som da água");
            DontDestroyOnLoad(go);
            _instancia = go.AddComponent<SomDaAgua>();
        }

        /// <summary>O teste: a água parada (um lago, fora dos rios) mais perto, até 'ate' m.</summary>
        public static bool LagoParaTeste(Vector3 de, float ate, out Vector3 ponto) => FaunaDoMundo.AguaParadaMaisPerto(de, ate, out ponto);

        /// <summary>O que toca agora (o F1 e o teste).</summary>
        public static string Resumo()
        {
            var a = _instancia;
            if (a == null) return "água: sem os sons";
            string Um(string nome, Agua x) => x == null || x.fonte == null ? $"{nome} parado" :
                System.FormattableString.Invariant($"{nome} {x.fonte.clip.name} vol {x.fonte.volume:F2} a {(Camera.main != null ? Vector3.Distance(Camera.main.transform.position, x.ponto) : -1f):F0} m");
            return $"água: {Um("lago", a._lago)}; {Um("rio", a._rio)}";
        }

        void Update()
        {
            var cam = Camera.main;
            if (Time.time >= _proxima)
            {
                _proxima = Time.time + 0.5f;
                Procurar(cam);
            }
            float ambiente = MaestroDaTrilha.VolumeDoAmbiente;
            Andar(_lago, ambiente);
            Andar(_rio, ambiente);
        }

        void Procurar(Camera cam)
        {
            if (_lago != null) _lago.alvo = 0f;
            if (_rio != null) _rio.alvo = 0f;
            if (cam == null || !FaunaDoMundo.TemMapa) return;
            Vector3 ouvido = cam.transform.position;
            if (!FaunaDoMundo.AguaMaisPerto(ouvido, 0.15f, Raio, out var p)) return;
            // é rio quando o meio de um rio passa perto da água achada (no lago, um rio que chega nele não conta)
            bool rio = FaunaDoMundo.TemRios && FaunaDoMundo.NoRio(p);
            var a = rio ? (_rio ??= Nova("Rio", ClipeDoRio(), VolumeDoRio)) : (_lago ??= Nova("Lago", Resources.Load<AudioClip>("Agua/Agua_Lago"), VolumeDoLago));
            if (a == null || a.fonte == null) return;
            if (rio && a.fonte.clip != ClipeDoRio() && ClipeDoRio() != null)
            {
                a.fonte.clip = ClipeDoRio();
                a.fonte.Play();
            }
            Vector3 ponto = p + Vector3.up * 0.2f;
            if (!a.temPonto || (a.ponto - ponto).sqrMagnitude > 60f * 60f) a.fonte.transform.position = ponto;   // longe: pula
            a.ponto = ponto;
            a.temPonto = true;
            float tapado = Abafamento.Quanto(ouvido, ponto);
            a.filtro.cutoffFrequency = Abafamento.Corte(tapado);
            float cheio = rio ? VolumeDoRio : VolumeDoLago;
            a.alvo = BunkerRede.Isolado(ouvido, ponto) ? 0f : cheio * Mathf.Lerp(1f, Abafamento.VolumeTapado, tapado);
        }

        static AudioClip ClipeDoRio() => Resources.Load<AudioClip>(QualRio == 1 ? "Agua/Agua_Rio_2" : "Agua/Agua_Rio_1");

        Agua Nova(string nome, AudioClip clip, float volume)
        {
            if (clip == null) return null;
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var f = go.AddComponent<AudioSource>();
            f.clip = clip;
            f.loop = true;
            f.playOnAwake = false;
            f.spatialBlend = 1f;
            f.dopplerLevel = 0f;
            f.spread = 90f;   // a água é larga: não sai de um ponto só
            f.priority = 110;
            f.minDistance = 3f;
            f.maxDistance = Raio + 10f;
            f.rolloffMode = AudioRolloffMode.Custom;
            f.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 1f),
                new Keyframe(0.35f, 0.4f), new Keyframe(0.7f, 0.1f), new Keyframe(1f, 0f)));
            f.volume = 0f;
            f.time = Random.Range(0f, clip.length * 0.9f);
            f.Play();
            return new Agua { fonte = f, filtro = go.AddComponent<AudioLowPassFilter>() };
        }

        void Andar(Agua a, float ambiente)
        {
            if (a == null || a.fonte == null) return;
            // o ponto anda devagar (a margem muda enquanto se anda), e o volume sobe e desce em ~1,5 s
            a.fonte.transform.position = Vector3.MoveTowards(a.fonte.transform.position, a.ponto, Time.deltaTime * 8f);
            a.volume = Mathf.MoveTowards(a.volume, a.alvo, Time.deltaTime * 0.7f);
            a.fonte.volume = a.volume * ambiente * MixerDeSom.Volume(CanalDoSom.Agua);   // (20/11/2026: o canal Água do mixer)
            if (a.volume <= 0f && a.fonte.isPlaying) a.fonte.Pause();
            else if (a.volume > 0f && !a.fonte.isPlaying) a.fonte.UnPause();
        }
    }
}
