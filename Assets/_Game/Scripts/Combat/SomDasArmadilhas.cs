using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons das armadilhas (11/11/2026, a etapa 5a): as latas chacoalhando no barbante, o barbante arrebentando, os
    /// pregos furando, a madeira das estacas quebrando, o martelo e as tábuas arrancadas. As latas, as estacas e o martelo
    /// são os gravados (Resources/Armadilhas; 20/11/2026: sem a reserva feita em código, que saiu); o barbante, os pregos e
    /// as tábuas arrancadas continuam gerados por código como os do molotov, cada um feito na primeira vez.
    /// </summary>
    public static class SomDasArmadilhas
    {
        const int Taxa = 44100;
        static AudioClip _barbante, _pregos, _arrancar;

        /// <summary>As latas batendo umas nas outras e no barbante (os gravados, sorteados; null sem eles).</summary>
        public static AudioClip Latas => SonsGravados.Um("Armadilhas", "latas_");   // (20/11/2026: os gravados, sorteados)

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

        /// <summary>A madeira das estacas quebrando (os gravados; null sem eles).</summary>
        public static AudioClip Madeira => SonsGravados.Um("Armadilhas", "estacas_");   // (20/11/2026: só o gravado)

        /// <summary>
        /// O martelo pregando (11/11/2026, a etapa 5b: a barricada de tábuas, pregar e consertar): os gravados, sorteados
        /// (20/11/2026: só eles; null sem eles).
        /// </summary>
        public static AudioClip Martelo => SonsGravados.Um("Armadilhas", "martelo_");

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
