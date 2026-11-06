using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do fumo (03/11/2026, o Plano_Cigarro.md da pasta de arte), gerados por código como os da comida: o maço
    /// sacudindo, a pedra do isqueiro raspando com a chama pegando, a chama, a tragada (o chiado da brasa e o ar entrando),
    /// o sopro da baforada e o peteleco. Cada um é feito na primeira vez que toca.
    /// </summary>
    public static class SomDoFumo
    {
        const int Taxa = 44100;
        static AudioClip _sacudir, _pedra, _chama, _tragada, _sopro, _peteleco;

        /// <summary>Duas batidinhas do maço na palma (o cigarro subindo).</summary>
        public static AudioClip Sacudir => _sacudir ??= Fazer("Maço sacudindo", 0.16f, (t, r, _) => Batida(t, r, 0f, 0.55f, 0.012f) + Batida(t, r, 0.07f, 0.4f, 0.010f));
        /// <summary>A pedra raspando na roda, o estalo e a chama pegando (o "fff").</summary>
        public static AudioClip Pedra => _pedra ??= Fazer("Isqueiro", 0.42f, (t, r, f) =>
            Faixa(t, 0f, 0.07f) * Ruido(r) * 0.35f + Batida(t, r, 0.075f, 0.6f, 0.006f) + Faixa(t, 0.08f, 0.42f) * f.Baixa(Ruido(r), 0.18f) * 0.45f);
        /// <summary>A chama acesa (um sopro baixinho, tremendo).</summary>
        public static AudioClip Chama => _chama ??= Fazer("Chama", 1.0f, (t, r, f) =>
            Faixa(t, 0f, 1.0f) * f.Baixa(Ruido(r), 0.08f) * (0.22f + 0.06f * Mathf.Sin(t * 37f)));
        /// <summary>A tragada: o ar entrando pelo filtro e o chiado da brasa (estalinhos soltos).</summary>
        public static AudioClip Tragada => _tragada ??= Fazer("Tragada", 0.95f, (t, r, f) =>
            Faixa(t, 0.02f, 0.9f) * f.Baixa(Ruido(r), 0.10f) * 0.30f + (r.NextDouble() < 0.0016 ? (float)(r.NextDouble() * 2 - 1) * 0.5f : 0f));
        /// <summary>O sopro da baforada (o ar saindo devagar).</summary>
        public static AudioClip Sopro => _sopro ??= Fazer("Sopro", 1.25f, (t, r, f) => Faixa(t, 0f, 1.25f) * f.Baixa(Ruido(r), 0.06f) * 0.55f);
        /// <summary>O peteleco (o estalo do dedo).</summary>
        public static AudioClip Peteleco => _peteleco ??= Fazer("Peteleco", 0.09f, (t, r, _) => Batida(t, r, 0f, 0.8f, 0.004f));

        /// <summary>Um passa-baixa de um polo, que guarda o valor de uma amostra para a outra.</summary>
        sealed class Filtro
        {
            float _v;
            public float Baixa(float x, float k) => _v = Mathf.Lerp(_v, x, k);
        }

        static AudioClip Fazer(string nome, float duracao, System.Func<float, System.Random, Filtro, float> onda)
        {
            int n = Mathf.RoundToInt(duracao * Taxa);
            var dados = new float[n];
            var r = new System.Random(nome.GetHashCode());
            var f = new Filtro();
            for (int i = 0; i < n; i++) dados[i] = Mathf.Clamp(onda(i / (float)Taxa, r, f), -1f, 1f);
            return SomGerado.Clip(nome, dados, Taxa);
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        /// <summary>O envelope de uma faixa de tempo: sobe no começo e desce no fim (seno), zero fora.</summary>
        static float Faixa(float t, float de, float ate) => t < de || t > ate ? 0f : Mathf.Sin(Mathf.PI * (t - de) / (ate - de));

        /// <summary>Uma batida curta (ruído que some em "some" segundos), em "em" segundos.</summary>
        static float Batida(float t, System.Random r, float em, float forca, float some)
        {
            float d = t - em;
            return d < 0f ? 0f : Ruido(r) * Mathf.Exp(-d / some) * forca;
        }
    }
}
