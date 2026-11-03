using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os sons das portas e das janelas das casas (o Plano_Portas.md da pasta de arte), gerados por código como os outros do
    /// jogo (<see cref="SomGerado"/>): abrir (o rangido da dobradiça velha), fechar (a batida da folha no batente e o
    /// estalo do trinco), a trancada (a maçaneta girando e o trinco batendo na tranca, três vezes), a gazua (os cliques
    /// dos pinos) e o pé de cabra (a madeira rachando e a fechadura arrebentando).
    /// </summary>
    public static class SomDasPortas
    {
        static AudioClip _abrir, _fechar, _trancada, _gazua, _peDeCabra, _forcando;

        public static AudioClip Abrir => _abrir ??= SomGerado.Clip("Porta: abrir", Rangido(41));
        public static AudioClip Fechar => _fechar ??= SomGerado.Clip("Porta: fechar", Batida(42));
        public static AudioClip Trancada => _trancada ??= SomGerado.Clip("Porta: trancada", Chacoalhar(43));
        public static AudioClip Gazua => _gazua ??= SomGerado.Clip("Porta: gazua", Pinos(44));
        public static AudioClip PeDeCabra => _peDeCabra ??= SomGerado.Clip("Porta: pé de cabra", Rachar(45));
        /// <summary>O pé de cabra forçando (enquanto o E é segurado): a madeira rangendo e um estalo.</summary>
        public static AudioClip Forcando => _forcando ??= SomGerado.Clip("Porta: forçando", Forcar(46));

        const int Taxa = 44100;

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // o rangido: um tom áspero (os pulsos da dobradiça raspando) subindo e descendo devagar, com o chiado
        static float[] Rangido(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.55f * Taxa);
            var d = new float[n];
            float fase = 0f, filtro = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa, k = t / 0.55f;
                float f = 380f + 160f * Mathf.Sin(k * Mathf.PI * 1.3f) + 25f * Mathf.Sin(t * 37f);
                fase += f / Taxa;
                // pulsos estreitos (a dobradiça agarra e solta) em vez do seno liso
                float pulso = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(fase * 2f * Mathf.PI)), 6f) * 2f - 0.35f;
                filtro += (Ruido(r) - filtro) * 0.2f;
                float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 9f));
                d[i] = (pulso * 0.35f + filtro * 0.12f) * env * 0.6f;
            }
            return d;
        }

        // a batida: o baque grave da folha (dois modos da madeira) e, logo depois, o estalo do trinco
        static float[] Batida(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.35f * Taxa);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float baque = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 18f) * 0.7f
                            + Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 30f) * 0.35f
                            + Ruido(r) * Mathf.Exp(-t * 120f) * 0.4f;
                float t2 = t - 0.06f;
                float trinco = t2 > 0f ? (Mathf.Sin(2f * Mathf.PI * 2600f * t2) * 0.4f + Ruido(r) * 0.3f) * Mathf.Exp(-t2 * 160f) : 0f;
                d[i] = (baque + trinco) * 0.8f;
            }
            return d;
        }

        // um clique metálico (a lingueta, os pinos), em 'tom' e com 'forca'
        static void Clique(float[] d, System.Random r, float inicio, float tom, float forca, float decai = 140f)
        {
            int i0 = Mathf.RoundToInt(inicio * Taxa);
            int n = Mathf.Min(d.Length - i0, Mathf.RoundToInt(0.08f * Taxa));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i0 + i] += (Mathf.Sin(2f * Mathf.PI * tom * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * tom * 1.73f * t) * 0.3f + Ruido(r) * 0.4f)
                             * Mathf.Exp(-t * decai) * forca;
            }
        }

        // um baque de madeira (a folha batendo na tranca)
        static void Baque(float[] d, System.Random r, float inicio, float forca)
        {
            int i0 = Mathf.RoundToInt(inicio * Taxa);
            int n = Mathf.Min(d.Length - i0, Mathf.RoundToInt(0.12f * Taxa));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                d[i0 + i] += (Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.7f + Ruido(r) * Mathf.Exp(-t * 200f) * 0.4f) * Mathf.Exp(-t * 35f) * forca;
            }
        }

        // a trancada: a maçaneta girando (o atrito) e, três vezes, a folha batendo na tranca com o trinco
        static float[] Chacoalhar(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(0.55f * Taxa)];
            float filtro = 0f;
            for (int i = 0; i < Mathf.RoundToInt(0.12f * Taxa); i++)
            {
                float t = i / (float)Taxa;
                filtro += (Ruido(r) - filtro) * 0.35f;
                d[i] += filtro * Mathf.Sin(t / 0.12f * Mathf.PI) * 0.25f;
            }
            for (int k = 0; k < 3; k++)
            {
                float t0 = 0.1f + k * 0.13f + (float)r.NextDouble() * 0.02f;
                Baque(d, r, t0, 0.65f - k * 0.12f);
                Clique(d, r, t0 + 0.004f, 1900f + k * 140f, 0.5f - k * 0.08f);
            }
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i] * 0.85f, -1f, 1f);
            return d;
        }

        // a gazua: os cliques pequenos dos pinos, num ritmo irregular, por 1,5 s (o jogo repete enquanto arromba)
        static float[] Pinos(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(1.5f * Taxa)];
            float t = 0.05f;
            while (t < 1.42f)
            {
                Clique(d, r, t, 3200f + (float)r.NextDouble() * 1400f, 0.18f + (float)r.NextDouble() * 0.15f, 260f);
                t += 0.08f + (float)r.NextDouble() * 0.22f;
            }
            return d;
        }

        // forçando: um rangido grave e curto da madeira e um estalinho de fibra
        static float[] Forcar(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(0.6f * Taxa)];
            float fase = 0f, filtro = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Taxa, k = t / 0.6f;
                fase += (110f + 40f * k) / Taxa;
                float pulso = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(fase * 2f * Mathf.PI)), 5f) * 2f - 0.3f;
                filtro += (Ruido(r) - filtro) * 0.1f;
                d[i] = (pulso * 0.4f + filtro * 0.5f) * Mathf.Sin(k * Mathf.PI) * 0.6f;
            }
            Baque(d, r, 0.38f, 0.35f);
            Clique(d, r, 0.38f, 1300f, 0.25f, 90f);
            return d;
        }

        // o pé de cabra: o estalo da madeira rachando (o ruído grave em rajadas), o metal raspando e a fechadura arrebentando
        static float[] Rachar(int semente)
        {
            var r = new System.Random(semente);
            var d = new float[Mathf.RoundToInt(0.9f * Taxa)];
            float filtro = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Taxa;
                filtro += (Ruido(r) - filtro) * 0.12f;
                // as fibras estalando: rajadas de 15 a 40 ms
                float rajada = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 47f + Mathf.Sin(t * 13f) * 2f)), 8f);
                d[i] += filtro * rajada * Mathf.Exp(-t * 2.5f) * 1.4f;
            }
            Baque(d, r, 0.02f, 0.9f);
            Baque(d, r, 0.42f, 1.1f);
            Clique(d, r, 0.42f, 1500f, 0.9f, 60f);
            Clique(d, r, 0.47f, 2300f, 0.6f, 90f);
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i] * 0.8f, -1f, 1f);
            return d;
        }
    }
}
