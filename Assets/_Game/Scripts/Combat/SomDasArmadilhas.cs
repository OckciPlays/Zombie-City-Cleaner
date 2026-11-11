using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons das armadilhas (11/11/2026, a etapa 5a), gerados por código como os do molotov (nada de voz gravada): as latas
    /// chacoalhando no barbante, o barbante arrebentando, os pregos furando e a madeira das estacas quebrando. Cada um é
    /// feito na primeira vez.
    /// </summary>
    public static class SomDasArmadilhas
    {
        const int Taxa = 44100;
        static AudioClip _latas, _barbante, _pregos, _madeira, _martelo, _arrancar;

        /// <summary>As latas batendo umas nas outras e no barbante (um chacoalhar de lata vazia, ~1 s).</summary>
        public static AudioClip Latas => _latas ??= Fazer("Latas chacoalhando", 1.1f, (t, r, f) =>
        {
            float s = 0f;
            // as batidas: lata em lata, cada uma com os seus tons (a lata fina tem parciais desafinadas, altas)
            for (int k = 0; k < 11; k++)
            {
                float em = 0.01f + k * 0.075f + 0.03f * Mathf.Sin(k * 2.7f) + (k > 6 ? 0.04f * (k - 6) : 0f);
                float d = t - em;
                if (d < 0f || d > 0.25f) continue;
                float forca = (k < 3 ? 0.9f : 0.75f) * Mathf.Pow(0.9f, k);
                float baseHz = 820f + 230f * ((k * 7) % 5);
                s += forca * Mathf.Exp(-d / 0.035f) * (Mathf.Sin(2f * Mathf.PI * baseHz * d) * 0.5f
                                                      + Mathf.Sin(2f * Mathf.PI * baseHz * 2.76f * d) * 0.35f
                                                      + Mathf.Sin(2f * Mathf.PI * baseHz * 5.4f * d) * 0.2f);
                // o "tlec" do metal fino: um estalo de ruído bem no começo
                s += forca * Ruido(r) * Mathf.Exp(-d / 0.004f) * 0.5f;
            }
            return s * 0.8f;
        });

        /// <summary>O barbante arrebentando (um estalo seco com um "tuim" que some).</summary>
        public static AudioClip Barbante => _barbante ??= Fazer("Barbante arrebentando", 0.35f, (t, r, f) =>
            Ruido(r) * Mathf.Exp(-t / 0.006f) * 0.9f + Mathf.Sin(2f * Mathf.PI * (380f - 300f * t) * t) * Mathf.Exp(-t / 0.08f) * 0.35f);

        /// <summary>Os pregos furando (o pé ou o pneu): um esmagar curto com o raspar do metal.</summary>
        public static AudioClip Pregos => _pregos ??= Fazer("Pregos", 0.45f, (t, r, f) =>
        {
            float esmaga = f.Baixa(Ruido(r), 0.25f) * (t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) / 0.09f)) * 1.1f;
            float raspa = Mathf.Sin(2f * Mathf.PI * 3100f * t + 3f * Mathf.Sin(2f * Mathf.PI * 47f * t)) * Mathf.Exp(-t / 0.06f) * 0.18f;
            return esmaga + raspa;
        });

        /// <summary>A madeira das estacas quebrando (os estalos de tora rachando).</summary>
        public static AudioClip Madeira => _madeira ??= Fazer("Estacas quebrando", 0.8f, (t, r, f) =>
        {
            float s = 0f;
            for (int k = 0; k < 5; k++)
            {
                float d = t - (0.005f + k * 0.07f + 0.02f * (k % 2));
                if (d < 0f) continue;
                s += f.Baixa(Ruido(r), 0.35f) * Mathf.Exp(-d / (0.025f + 0.01f * k)) * (1f - 0.12f * k);
                s += Mathf.Sin(2f * Mathf.PI * (180f + 40f * k) * d) * Mathf.Exp(-d / 0.05f) * 0.3f;
            }
            return s;
        });

        /// <summary>
        /// O martelo pregando (11/11/2026, a etapa 5b: a barricada de tábuas, pregar e consertar): três batidas no prego, cada uma
        /// com o baque da tábua (grave, abafado) e o tinido do prego (agudo, curto); a última mais forte (~1 s).
        /// </summary>
        public static AudioClip Martelo => _martelo ??= Fazer("Martelo pregando", 1.0f, (t, r, f) =>
        {
            float s = 0f;
            for (int k = 0; k < 3; k++)
            {
                float d = t - (0.02f + k * 0.31f + 0.015f * (k % 2));
                if (d < 0f || d > 0.3f) continue;
                float forca = k == 2 ? 1f : 0.78f;
                // o baque: o ruído grave da madeira e um tom de tábua que some depressa
                s += forca * f.Baixa(Ruido(r), 0.18f) * Mathf.Exp(-d / 0.018f) * 1.6f;
                s += forca * Mathf.Sin(2f * Mathf.PI * (150f + 25f * k) * d) * Mathf.Exp(-d / 0.035f) * 0.45f;
                // o tinido do prego (o metal batido): parciais altas e desafinadas
                float hz = 2350f + 180f * k;
                s += forca * Mathf.Exp(-d / 0.022f) * (Mathf.Sin(2f * Mathf.PI * hz * d) * 0.22f + Mathf.Sin(2f * Mathf.PI * hz * 1.83f * d) * 0.12f);
            }
            return s * 0.85f;
        });

        /// <summary>As tábuas arrancadas (desmontar, ou a barricada caindo): o rangido dos pregos saindo e a madeira estalando (~0,7 s).</summary>
        public static AudioClip Arrancar => _arrancar ??= Fazer("Tábuas arrancadas", 0.7f, (t, r, f) =>
        {
            // o prego saindo: um rangido que sobe (um tom com a aspereza do ruído)
            float rangido = Mathf.Sin(2f * Mathf.PI * (420f + 900f * t) * t + 2f * Mathf.Sin(2f * Mathf.PI * 31f * t))
                            * (0.25f + 0.75f * Mathf.Abs(f.Baixa(Ruido(r), 0.08f))) * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t / 0.35f) * 0.4f;
            // o estalo da tábua soltando, no fim
            float d = t - 0.42f;
            float estalo = d >= 0f ? Ruido(r) * Mathf.Exp(-d / 0.02f) * 0.8f + Mathf.Sin(2f * Mathf.PI * 210f * d) * Mathf.Exp(-d / 0.05f) * 0.3f : 0f;
            return rangido + estalo;
        });

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
            // um respiro no fim (sem estalo ao cortar)
            int fade = Mathf.Min(n, Taxa / 100);
            for (int i = 0; i < fade; i++) dados[n - 1 - i] *= i / (float)fade;
            return SomGerado.Clip(nome, dados, Taxa);
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);
    }
}
