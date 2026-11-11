using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do molotov (11/11/2026, a etapa 4 do ramo Fogo), gerados por código como os do fumo: o vidro quebrando
    /// (estalos agudos que tilintam) e a labareda pegando (o "fuuum" do combustível). Cada um é feito na primeira vez.
    /// </summary>
    public static class SomDoFogo
    {
        const int Taxa = 44100;
        static AudioClip _vidro, _labareda;

        /// <summary>O vidro da garrafa quebrando no chão.</summary>
        public static AudioClip Vidro => _vidro ??= Fazer("Vidro quebrando", 0.55f, (t, r, f) =>
        {
            float s = Batida(t, r, 0f, 0.9f, 0.012f);
            // os cacos: tons altos que somem rápido, um atrás do outro
            for (int k = 0; k < 7; k++)
            {
                float em = 0.004f + k * 0.035f + 0.01f * (k % 3), d = t - em;
                if (d < 0f) continue;
                s += Mathf.Sin(2f * Mathf.PI * (2600f + 650f * k) * d) * Mathf.Exp(-d / (0.03f + 0.01f * (k % 2))) * 0.22f;
            }
            return s;
        });

        /// <summary>A labareda pegando (o combustível acendendo de uma vez).</summary>
        public static AudioClip Labareda => _labareda ??= Fazer("Labareda", 1.6f, (t, r, f) =>
            (t < 0.12f ? t / 0.12f : Mathf.Exp(-(t - 0.12f) / 0.6f)) * f.Baixa(Ruido(r), 0.07f) * 0.9f);

        /// <summary>A garrafa quebrou ali e o fogo pegou.</summary>
        public static void Quebrar(Vector3 onde)
        {
            Efeitos.Som(Vidro, onde, 0.9f, true, 40f);
            Efeitos.Som(Labareda, onde, 1f, true, 50f);
        }

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

        static float Batida(float t, System.Random r, float em, float forca, float some)
        {
            float d = t - em;
            return d < 0f ? 0f : Ruido(r) * Mathf.Exp(-d / some) * forca;
        }
    }
}
