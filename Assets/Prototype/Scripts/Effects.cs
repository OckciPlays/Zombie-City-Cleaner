using UnityEngine;

namespace ZombieCityCleaner.Prototype
{
    /// <summary>
    /// Partículas e sons do protótipo. Os sons são gerados por código (o projeto ainda não tem áudio),
    /// com taxa de amostragem baixa para soar como jogo de PS1.
    /// </summary>
    public static class Effects
    {
        public static Material ParticleMaterial;

        const int SampleRate = 22050;
        static AudioClip _gunshot, _dryFire, _click, _flesh, _growl, _ding, _hum, _doorSlide;

        public static AudioClip Gunshot => _gunshot != null ? _gunshot : _gunshot = MakeGunshot();
        public static AudioClip DryFire => _dryFire != null ? _dryFire : _dryFire = MakeClick("DryFire", 0.9f);
        public static AudioClip Click => _click != null ? _click : _click = MakeClick("Click", 0.5f);
        public static AudioClip Flesh => _flesh != null ? _flesh : _flesh = MakeFlesh();
        public static AudioClip Growl => _growl != null ? _growl : _growl = MakeGrowl();
        public static AudioClip Ding => _ding != null ? _ding : _ding = MakeDing();
        public static AudioClip Hum => _hum != null ? _hum : _hum = MakeHum();
        public static AudioClip DoorSlide => _doorSlide != null ? _doorSlide : _doorSlide = MakeDoorSlide();

        // Vozes de zumbi tocando agora (limite para uma horda não virar uma parede de som).
        const int MaxVoices = 6;
        static readonly float[] VoiceEnds = new float[MaxVoices];

        /// <summary>
        /// Toca um trecho [start, start + length] de um clipe em 3D. Retorna false (e não toca)
        /// se já houver vozes demais ao mesmo tempo.
        /// </summary>
        public static bool PlayVoice(AudioClip clip, float start, float length, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            int slot = -1;
            for (int i = 0; i < MaxVoices; i++)
                if (VoiceEnds[i] <= Time.realtimeSinceStartup) { slot = i; break; }
            if (slot < 0 || clip == null) return false;

            float duration = length / Mathf.Max(0.1f, pitch);
            VoiceEnds[slot] = Time.realtimeSinceStartup + duration;   // tempo real: não zera entre sessões de Play
            var go = new GameObject("SFX Voice");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.time = Mathf.Clamp(start, 0f, clip.length - 0.05f);
            source.Play();
            source.SetScheduledEndTime(AudioSettings.dspTime + duration);
            Object.Destroy(go, duration + 0.2f);
            return true;
        }

        public static void Burst(Vector3 position, Vector3 direction, Color color, int count, float speed, float size, float gravity, float lifetime, float spreadAngle = 35f)
        {
            var go = new GameObject("FX Burst");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.sqrMagnitude > 0f ? direction : Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            var dark = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, dark);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = spreadAngle;
            shape.radius = 0.02f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (ParticleMaterial != null) renderer.sharedMaterial = ParticleMaterial;

            ps.Play();
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var go = new GameObject("SFX " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        static AudioClip Build(string name, float seconds, System.Func<float, System.Random, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)SampleRate, random), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        static AudioClip MakeGunshot()
        {
            float low = 0f;
            return Build("Gunshot", 0.45f, (t, r) =>
            {
                low += (Noise(r) - low) * 0.35f;
                float crack = Noise(r) * Mathf.Exp(-t * 60f) * 0.8f;
                float body = low * Mathf.Exp(-t * 11f) * 1.6f;
                float thump = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 16f) * 0.9f;
                float s = crack + body + thump;
                return Mathf.Round(s * 24f) / 24f;   // quantização: som "8 bits"
            });
        }

        static AudioClip MakeClick(string name, float volume)
        {
            return Build(name, 0.05f, (t, r) => Noise(r) * Mathf.Exp(-t * 180f) * volume + Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 250f) * 0.3f * volume);
        }

        static AudioClip MakeFlesh()
        {
            float low = 0f;
            return Build("Flesh", 0.18f, (t, r) =>
            {
                low += (Noise(r) - low) * 0.12f;
                return low * Mathf.Exp(-t * 22f) * 2.2f;
            });
        }

        static AudioClip MakeDing()
        {
            return Build("Ding", 1.4f, (t, r) =>
            {
                float a = Mathf.Sin(2f * Mathf.PI * 1318f * t) * Mathf.Exp(-t * 3f);
                float b = t > 0.35f ? Mathf.Sin(2f * Mathf.PI * 1046f * (t - 0.35f)) * Mathf.Exp(-(t - 0.35f) * 3f) : 0f;
                return (a + b) * 0.35f;
            });
        }

        // Ronco do motor do elevador. Feito para tocar em loop (frequências que fecham ciclos em 2 s).
        static AudioClip MakeHum()
        {
            float low = 0f;
            return Build("Hum", 2f, (t, r) =>
            {
                low += (Noise(r) - low) * 0.02f;
                float motor = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f;
                float rattle = Mathf.Sin(2f * Mathf.PI * 7f * t) * 0.5f + 0.5f;
                return motor * (0.7f + 0.3f * rattle) + low * 0.8f;
            });
        }

        static AudioClip MakeDoorSlide()
        {
            float low = 0f;
            return Build("DoorSlide", 0.9f, (t, r) =>
            {
                low += (Noise(r) - low) * 0.05f;
                float envelope = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((0.9f - t) * 4f);
                float clunk = t > 0.8f ? Noise(r) * Mathf.Exp(-(t - 0.8f) * 60f) * 0.8f : 0f;
                return low * envelope * 1.5f + clunk;
            });
        }

        static AudioClip MakeGrowl()
        {
            float low = 0f, phase = 0f;
            return Build("Growl", 1.1f, (t, r) =>
            {
                float freq = 78f + 14f * Mathf.Sin(t * 9f) + 6f * Mathf.Sin(t * 23f);
                phase += freq / SampleRate;
                float saw = (phase % 1f) * 2f - 1f;
                low += (Noise(r) - low) * 0.08f;
                float envelope = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1.1f - t) * 3f);
                return (saw * 0.45f + low * 1.4f) * envelope * (0.7f + 0.3f * Mathf.Sin(t * 31f));
            });
        }
    }
}
