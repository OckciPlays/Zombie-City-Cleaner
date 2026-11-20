using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O som do fogo aceso (19/11/2026, com o Nature Sound FX, o pacote grátis que o Pedro importou). Toda chama do mundo
    /// feita pelo <see cref="Loot.ConstrucoesNoMundo.Fogo"/> com tamanho de 0,2 para cima ganha o laço de fogo de verdade:
    /// a fogueira, a do acampamento, a tocha no corpo, o molotov, a cerca de fogo e o zumbi queimando.
    /// O som vai pelo tamanho:
    ///  - até 0,5 (a tocha): o fogo médio, baixo, ouvido até 10 m;
    ///  - até 1,3 (a fogueira, a cerca, o zumbi): a fogueira estalando, até 24 m;
    ///  - maior (o molotov, a bola do galão): o fogo grande, roncando, até 36 m.
    /// Para não pesar, só tocam as <see cref="MaisPerto"/> mais perto de quem ouve (as maiores na frente). Uma chama a menos
    /// de <see cref="Junto"/> de outra já escolhida fica quieta (a linha da cerca, as poças do molotov).
    /// As chamas da primeira pessoa (perto da origem, BracosNaOrigem) não têm som: o da tocha do dono vem do corpo.
    /// A parede no meio abafa (<see cref="Abafamento"/>), e a porta do bunker fechada separa.
    /// Os sons ficam em Resources/Fogo. (20/11/2026: os estalos da lenha gerados por código, que tocavam sem eles, saíram;
    /// sem eles, o fogo fica mudo.)
    /// </summary>
    public class SomDaChama : MonoBehaviour
    {
        internal float tamanho, alvo, alvoBase, volume, ordem;
        internal AudioSource fonte;
        internal AudioLowPassFilter filtro;

        void OnEnable() => SomDasChamas.Entrar(this);

        void OnDisable()
        {
            SomDasChamas.Sair(this);
            if (fonte != null) fonte.Stop();
            volume = 0f;
        }
    }

    public static class SomDasChamas
    {
        /// <summary>Quantas chamas tocam ao mesmo tempo, no máximo.</summary>
        public const int MaisPerto = 4;
        /// <summary>A menos disto (m) de uma chama que já toca, a outra fica quieta.</summary>
        public const float Junto = 1.5f;

        static readonly List<SomDaChama> _todas = new List<SomDaChama>(32);
        static readonly List<SomDaChama> _ordem = new List<SomDaChama>(32);
        static readonly List<SomDaChama> _tocando = new List<SomDaChama>(MaisPerto);
        static AudioClip _fogueira, _medio;
        static AudioClip[] _grandes, _labaredas, _labaredasGrandes;
        static bool _carregou;
        static float _proximaEscolha;
        static Motor _motor;
        static AnimationCurve _queda;

        // o Play sem recarregar o domínio (Enter Play Mode Options): começa do zero a cada Play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar()
        {
            _todas.Clear();
            _ordem.Clear();
            _tocando.Clear();
            _carregou = false;
            _proximaEscolha = 0f;
            _motor = null;
        }

        static void Carregar()
        {
            if (_carregou) return;
            _carregou = true;
            _fogueira = Resources.Load<AudioClip>("Fogo/fogueira");
            _medio = Resources.Load<AudioClip>("Fogo/fogo_medio");
            _grandes = Varios("Fogo/fogo_grande_", 2);
            _labaredas = Varios("Fogo/labareda_", 6);
            _labaredasGrandes = Varios("Fogo/labareda_grande_", 2);
        }

        static AudioClip[] Varios(string prefixo, int n)
        {
            var lista = new List<AudioClip>(n);
            for (int i = 1; i <= n; i++)
            {
                var c = Resources.Load<AudioClip>(prefixo + i);
                if (c != null) lista.Add(c);
            }
            return lista.ToArray();
        }

        static AudioClip Sortear(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        /// <summary>Os sons do fogo estão no jogo (Resources/Fogo)?</summary>
        public static bool TemLaco
        {
            get
            {
                Carregar();
                return _fogueira != null;
            }
        }

        /// <summary>Uma labareda curta de verdade, sorteada (null sem os sons).</summary>
        public static AudioClip Labareda()
        {
            Carregar();
            return Sortear(_labaredas);
        }

        /// <summary>Uma labareda grande (o galão estourando), sorteada (null sem os sons).</summary>
        public static AudioClip LabaredaGrande()
        {
            Carregar();
            return Sortear(_labaredasGrandes);
        }

        /// <summary>Quantas chamas com som existem e quantas tocam agora (o F1 e o teste).</summary>
        public static string Resumo()
        {
            int vivas = 0;
            foreach (var s in _todas)
                if (s != null) vivas++;
            var sb = new System.Text.StringBuilder($"chamas com som: {vivas}, tocando: {_tocando.Count}");
            foreach (var s in _tocando)
                if (s != null && s.fonte != null)
                    sb.Append(System.FormattableString.Invariant($" [{s.fonte.clip.name} tam {s.tamanho:F2} vol {s.fonte.volume:F2} corte {s.filtro.cutoffFrequency:F0}]"));
            return sb.ToString();
        }

        /// <summary>Põe o som na chama recém-feita (ConstrucoesNoMundo.Fogo), se ela for grande o bastante.</summary>
        public static void Por(GameObject fogo, float tamanho)
        {
            if (fogo == null || tamanho < 0.2f || !TemLaco) return;
            var s = fogo.AddComponent<SomDaChama>();
            s.tamanho = tamanho;
        }

        internal static void Entrar(SomDaChama s)
        {
            if (!_todas.Contains(s)) _todas.Add(s);
            if (_motor == null)
            {
                var go = new GameObject("Som das chamas");
                Object.DontDestroyOnLoad(go);
                _motor = go.AddComponent<Motor>();
            }
        }

        internal static void Sair(SomDaChama s)
        {
            _todas.Remove(s);
            _tocando.Remove(s);
        }

        // o som de cada tamanho: o clipe, o volume, o alcance (m) e o tom
        static (AudioClip clip, float volume, float alcance, float tom) Tipo(float tamanho)
        {
            if (tamanho < 0.5f) return (_medio != null ? _medio : _fogueira, 0.3f, 10f, 1.1f);
            if (tamanho < 1.3f || _grandes.Length == 0) return (_fogueira, Mathf.Clamp(tamanho, 0.75f, 1.15f), 24f, 1f);
            return (Sortear(_grandes), 0.85f, 36f, 1f);
        }

        static float Alcance(float tamanho) => tamanho < 0.5f ? 10f : tamanho < 1.3f ? 24f : 36f;

        // a queda com a distância (em fração do alcance): cheio até 6%, e chega a zero no alcance
        static AnimationCurve Queda() => _queda ??= new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.06f, 1f),
            new Keyframe(0.2f, 0.45f), new Keyframe(0.45f, 0.15f), new Keyframe(1f, 0f));

        sealed class Motor : MonoBehaviour
        {
            void Update() => Atualizar();
        }

        static void Atualizar()
        {
            if (Time.time >= _proximaEscolha)
            {
                _proximaEscolha = Time.time + 0.25f;
                Escolher(Camera.main);
            }
            float passo = Time.deltaTime * 1.5f;   // sobe e desce em ~0,7 s
            for (int i = _todas.Count - 1; i >= 0; i--)
            {
                var s = _todas[i];
                if (s == null)
                {
                    _todas.RemoveAt(i);
                    continue;
                }
                if (s.fonte == null) continue;
                float alvo = _tocando.Contains(s) ? s.alvo : 0f;
                s.volume = Mathf.MoveTowards(s.volume, alvo, passo);
                s.fonte.volume = s.volume;
                if (alvo <= 0f && s.volume <= 0f && s.fonte.isPlaying) s.fonte.Stop();
            }
        }

        static void Escolher(Camera cam)
        {
            _tocando.Clear();
            if (cam == null) return;
            Vector3 ouvido = cam.transform.position;
            _ordem.Clear();
            foreach (var s in _todas)
            {
                if (s == null || !s.isActiveAndEnabled) continue;
                float a = Alcance(s.tamanho);
                float d2 = (s.transform.position - ouvido).sqrMagnitude;
                if (d2 >= a * a) continue;
                s.ordem = d2 / (a * a);   // a distância pelo alcance: as maiores na frente
                _ordem.Add(s);
            }
            _ordem.Sort((x, y) => x.ordem.CompareTo(y.ordem));
            foreach (var s in _ordem)
            {
                if (_tocando.Count >= MaisPerto) break;
                bool junto = false;
                foreach (var t in _tocando)
                    if ((t.transform.position - s.transform.position).sqrMagnitude < Junto * Junto)
                    {
                        junto = true;
                        break;
                    }
                if (junto) continue;
                _tocando.Add(s);
                Tocar(s, ouvido);
            }
        }

        static void Tocar(SomDaChama s, Vector3 ouvido)
        {
            if (s.fonte == null)
            {
                var (clip, volume, alcance, tom) = Tipo(s.tamanho);
                if (clip == null) return;
                var f = s.gameObject.AddComponent<AudioSource>();
                f.clip = clip;
                f.loop = true;
                f.playOnAwake = false;
                f.spatialBlend = 1f;
                f.dopplerLevel = 0f;
                f.spread = 40f;
                f.priority = 100;
                f.minDistance = 1f;
                f.maxDistance = alcance;
                f.rolloffMode = AudioRolloffMode.Custom;
                f.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Queda());
                f.pitch = tom * Random.Range(0.96f, 1.04f);
                f.volume = 0f;
                s.fonte = f;
                s.filtro = s.gameObject.AddComponent<AudioLowPassFilter>();
                s.alvoBase = volume;
            }
            if (!s.fonte.isPlaying)
            {
                s.fonte.time = Random.Range(0f, s.fonte.clip.length * 0.95f);   // duas fogueiras juntas não tocam iguais
                s.fonte.Play();
            }
            Vector3 p = s.transform.position;
            float tapado = Abafamento.Quanto(ouvido, p);
            s.filtro.cutoffFrequency = Abafamento.Corte(tapado);
            s.alvo = BunkerRede.Isolado(ouvido, p) ? 0f : s.alvoBase * Mathf.Lerp(1f, Abafamento.VolumeTapado, tapado);
        }
    }
}
