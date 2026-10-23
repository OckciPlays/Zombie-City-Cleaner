using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Atmosphere
{
    /// <summary>
    /// O som da cidade morta: vento contínuo com rajadas e, de vez em quando, algo ao longe (gemidos de zumbi, uma
    /// sirene, uma batida de metal), vindo de uma direção sorteada a 35–60 m de quem escuta. À noite é mais frequente.
    /// O eco dentro das casas e do prédio vem das AudioReverbZones de cada construção.
    /// </summary>
    public class SomAmbiente : MonoBehaviour
    {
        [Tooltip("A gravação de zumbi do usuário; vários gemidos, tocados em trechos e mais graves para parecer longe.")]
        public AudioClip gemidos;
        public Vector2[] trechos = { new Vector2(0.1f, 1f), new Vector2(1.75f, 1f), new Vector2(3.1f, 0.85f), new Vector2(4.35f, 1f) };
        public float volumeVento = 0.22f;
        [Tooltip("Intervalo entre sons distantes, em segundos (de dia; à noite cai pela metade).")]
        public Vector2 intervalo = new Vector2(10f, 26f);

        AudioSource _vento, _distante;
        AudioClip _clipVento, _sirene, _batida;
        float _proximo;

        void Awake()
        {
            _clipVento = Gerar("Vento", 8f, Vento, true);
            _sirene = Gerar("Sirene", 5f, Sirene, false);
            _batida = Gerar("Batida", 1.2f, Batida, false);
            _vento = gameObject.AddComponent<AudioSource>();
            _vento.clip = _clipVento; _vento.loop = true; _vento.spatialBlend = 0f; _vento.volume = volumeVento; _vento.Play();
            var go = new GameObject("Som distante"); go.transform.SetParent(transform, false);
            _distante = go.AddComponent<AudioSource>();
            _distante.spatialBlend = 1f; _distante.rolloffMode = AudioRolloffMode.Linear; _distante.minDistance = 10f; _distante.maxDistance = 90f; _distante.dopplerLevel = 0f;
            _proximo = Time.time + Random.Range(3f, 8f);
        }

        void Update()
        {
            float n = CicloDoDia.Noite;
            float rajada = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * 0.15f, 3.1f);
            _vento.volume = volumeVento * rajada * Mathf.Lerp(0.8f, 1.1f, n);
            if (Time.time < _proximo) return;
            _proximo = Time.time + Random.Range(intervalo.x, intervalo.y) * Mathf.Lerp(1f, 0.5f, n);
            var ouvinte = Camera.main != null ? Camera.main.transform.position : transform.position;
            float ang = Random.value * Mathf.PI * 2f;
            _distante.transform.position = ouvinte + new Vector3(Mathf.Cos(ang), 0.1f, Mathf.Sin(ang)) * Random.Range(35f, 60f);
            float sorteio = Random.value;
            if (gemidos != null && sorteio < 0.6f)
            {
                var t = trechos[Random.Range(0, trechos.Length)];
                _distante.pitch = Random.Range(0.62f, 0.8f);
                _distante.clip = gemidos;
                _distante.volume = 0.9f;
                _distante.Play();
                _distante.time = t.x;
                _distante.SetScheduledEndTime(AudioSettings.dspTime + t.y / _distante.pitch);
            }
            else
            {
                _distante.pitch = Random.Range(0.9f, 1.05f);
                _distante.PlayOneShot(sorteio < 0.8f ? _batida : _sirene, sorteio < 0.8f ? 0.8f : 0.45f);
            }
        }

        // ---- sons gerados por código

        delegate float Onda(float t, System.Random r, ref float estado);

        static AudioClip Gerar(string nome, float duracao, Onda onda, bool laco)
        {
            const int taxa = 22050;
            int n = Mathf.RoundToInt(duracao * taxa);
            var dados = new float[n];
            var r = new System.Random(nome.GetHashCode());
            float estado = 0f;
            for (int i = 0; i < n; i++) dados[i] = onda(i / (float)taxa, r, ref estado);
            if (laco)
            {
                // emenda o fim no começo, para o laço não estalar
                int junta = taxa / 2;
                for (int i = 0; i < junta; i++)
                {
                    float k = i / (float)junta;
                    dados[n - junta + i] = Mathf.Lerp(dados[n - junta + i], dados[i], k);
                }
            }
            var clip = AudioClip.Create(nome, n, 1, taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }

        static float Vento(float t, System.Random r, ref float estado)
        {
            // ruído marrom (grave) com uma variação lenta de volume
            estado = Mathf.Clamp(estado + ((float)r.NextDouble() * 2f - 1f) * 0.02f, -1f, 1f) * 0.998f;
            return estado * (0.6f + 0.4f * Mathf.Sin(t * 0.8f) * Mathf.Sin(t * 0.33f + 1f));
        }

        static float Sirene(float t, System.Random r, ref float estado)
        {
            float freq = 520f + 260f * Mathf.Sin(t * Mathf.PI * 0.8f);
            estado += freq / 22050f;
            float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((5f - t) / 1.2f);
            return Mathf.Sin(estado * Mathf.PI * 2f) * 0.35f * env;
        }

        static float Batida(float t, System.Random r, ref float estado)
        {
            float metal = Mathf.Sin(t * 2f * Mathf.PI * 180f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 413f) * 0.3f;
            float ruido = ((float)r.NextDouble() * 2f - 1f) * 0.4f;
            return (metal + ruido) * Mathf.Exp(-t * 5f) * 0.8f;
        }
    }
}
