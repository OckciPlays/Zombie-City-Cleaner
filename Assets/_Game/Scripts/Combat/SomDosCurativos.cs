using UnityEngine;
using Faixa = ZombieCityCleaner.Combat.SomDePasso.Faixa;
using PassaBaixa = ZombieCityCleaner.Combat.SomDePasso.PassaBaixa;
using Ressonador = ZombieCityCleaner.Combat.SomDePasso.Ressonador;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do corpo e dos curativos (05/11/2026, o sistema de vida por membro), gerados por código como os da comida
    /// (<see cref="SomDosItens"/>), cada um uma vez só, na primeira vez que toca: o estalo do osso quebrando (seco e
    /// grave), o pano rasgando (a atadura e a bandagem abrindo), o pano roçando (enrolando a faixa), o velcro ou a fita
    /// fechando (o fim do curativo e da tala), o zíper do kit médico e o comprimido saindo da cartela. Tocam por
    /// <see cref="Efeitos.Som(AudioClip, Vector3, float, bool, float)"/> (no dono, na cabeça: não espaciais).
    /// </summary>
    public static class SomDosCurativos
    {
        const int Taxa = 44100;

        static AudioClip _estalo, _rasgo, _enrolar, _fecho, _ziper, _cartela;

        /// <summary>O osso quebrando: um estalo seco e grave (a fratura).</summary>
        public static AudioClip Estalo => _estalo != null ? _estalo : (_estalo = SomGerado.Clip("Osso", GerarEstalo(11), Taxa));
        /// <summary>O pano rasgando (o começo da atadura e da bandagem).</summary>
        public static AudioClip Rasgo => _rasgo != null ? _rasgo : (_rasgo = SomGerado.Clip("Pano rasgando", GerarRasgo(23), Taxa));
        /// <summary>O pano roçando (a faixa dando a volta no braço).</summary>
        public static AudioClip Enrolar => _enrolar != null ? _enrolar : (_enrolar = SomGerado.Clip("Pano enrolando", GerarEnrolar(31), Taxa));
        /// <summary>O velcro ou a fita fechando (o fim do curativo e da tala).</summary>
        public static AudioClip Fecho => _fecho != null ? _fecho : (_fecho = SomGerado.Clip("Velcro", GerarFecho(41), Taxa));
        /// <summary>O zíper do kit médico abrindo.</summary>
        public static AudioClip Ziper => _ziper != null ? _ziper : (_ziper = SomGerado.Clip("Zíper", GerarZiper(53), Taxa));
        /// <summary>O comprimido saindo da cartela (o alumínio estalando e o comprimido batendo na mão).</summary>
        public static AudioClip Cartela => _cartela != null ? _cartela : (_cartela = SomGerado.Clip("Cartela", GerarCartela(67), Taxa));

        // ------------------------------------------------ os sons ------------------------------------------------

        /// <summary>
        /// O osso: um estalo curto (uns estilhaços nos primeiros 30 ms), o corpo do estalo em modos graves que morrem logo
        /// (o osso dentro da carne não soa como madeira solta) e o baque surdo por baixo.
        /// </summary>
        static float[] GerarEstalo(int semente)
        {
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(0.35f * Taxa)];
            var modos = new[] { new Ressonador(160f, 0.05f, 1f), new Ressonador(390f, 0.03f, 0.8f), new Ressonador(870f, 0.015f, 0.5f), new Ressonador(1900f, 0.008f, 0.3f) };
            var abafa = new PassaBaixa(3200f, 2);
            float[] lascas = { 0.004f, 0.011f + (float)r.NextDouble() * 0.006f, 0.022f + (float)r.NextDouble() * 0.01f };
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float golpe = Impulso(t, 0f, 0.0015f) * 1.4f;
                foreach (float l in lascas) golpe += Impulso(t, l, 0.0012f) * 0.6f;
                float x = Ruido(r) * golpe;
                float corpo = 0f;
                foreach (var m in modos) corpo += m.Passar(x);
                // o baque da carne, embaixo de tudo
                float baque = Mathf.Sin(2f * Mathf.PI * 72f * t) * Mathf.Exp(-t / 0.045f) * 0.5f;
                y[i] = abafa.Passar(corpo * 2.2f + x * 0.25f) + baque;
            }
            return Acabar(y, 0.9f);
        }

        /// <summary>
        /// O pano rasgando: os fios estourando em grãos (mais densos no meio do puxão), num ruído de faixa larga que sobe
        /// de ~1,6 kHz para ~2,8 kHz conforme o rasgo corre.
        /// </summary>
        static float[] GerarRasgo(int semente)
        {
            var r = new System.Random(semente);
            float dur = 0.55f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var pano = new Faixa(1600f, 0.9f);
            var fio = new Faixa(3400f, 2.2f);
            var macio = new PassaBaixa(7500f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float k = t / dur;
                pano.Centro(Mathf.Lerp(1600f, 2800f, k));
                // o puxão: sobe rápido, segura e solta; com tremidas (a mão puxando aos trancos)
                float puxao = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((dur - t) / 0.12f) * (0.65f + 0.35f * Mathf.Sin(t * 47f) * Mathf.Sin(t * 13f));
                float graos = r.NextDouble() < 0.02 + 0.06 * puxao ? Ruido(r) * 2.5f : 0f;
                float x = Ruido(r) * puxao * 0.5f;
                y[i] = macio.Passar(pano.Passar(x + graos * 0.4f) + fio.Passar(graos) * 0.6f);
            }
            return Acabar(y, 0.75f);
        }

        /// <summary>O pano roçando: um chiado macio e grave, que incha e esvazia (a faixa dando a volta).</summary>
        static float[] GerarEnrolar(int semente)
        {
            var r = new System.Random(semente);
            float dur = 0.7f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var pano = new Faixa(950f, 0.7f);
            var abafa = new PassaBaixa(2600f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float k = t / dur;
                pano.Centro(Mathf.Lerp(800f, 1300f, Mathf.Sin(k * Mathf.PI)));
                float volta = Mathf.Sin(k * Mathf.PI);
                volta *= volta * (0.8f + 0.2f * Mathf.Sin(t * 31f));
                // umas fibras se agarrando de vez em quando
                float fibra = r.NextDouble() < 0.004 * volta ? Ruido(r) * 1.5f : 0f;
                y[i] = abafa.Passar(pano.Passar(Ruido(r) * volta + fibra));
            }
            return Acabar(y, 0.5f);
        }

        /// <summary>O velcro ou a fita: um crepitar denso e rápido (os ganchinhos soltando), que acaba num toque firme.</summary>
        static float[] GerarFecho(int semente)
        {
            var r = new System.Random(semente);
            float dur = 0.32f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var gancho = new Faixa(2700f, 1.6f);
            var corpo = new Faixa(1100f, 1f);
            var macio = new PassaBaixa(8000f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                // o crepitar entre 0 e 0,22 s, mais forte no meio
                float crepita = t < 0.22f ? Mathf.Sin(t / 0.22f * Mathf.PI) : 0f;
                float grao = r.NextDouble() < 0.12 * crepita ? Ruido(r) * 2f : 0f;
                // a mão apertando a fita no fim
                float aperta = Impulso(t, 0.25f, 0.012f);
                y[i] = macio.Passar(gancho.Passar(grao) + corpo.Passar(Ruido(r) * (crepita * 0.25f + aperta * 1.2f)));
            }
            return Acabar(y, 0.7f);
        }

        /// <summary>
        /// O zíper: um trem de cliques (os dentes), que acelera e depois freia, cada um um toque de metal pequeno, com o
        /// pano da bolsa roçando por baixo.
        /// </summary>
        static float[] GerarZiper(int semente)
        {
            var r = new System.Random(semente);
            float dur = 0.75f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var dente = new[] { new Ressonador(3100f, 0.004f, 1f), new Ressonador(5200f, 0.003f, 0.6f) };
            var pano = new Faixa(1400f, 0.8f);
            var macio = new PassaBaixa(9000f, 2);
            float fase = 0f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float k = t / dur;
                // os dentes por segundo: começa devagar, corre e freia no fim da volta
                float ritmo = Mathf.Lerp(45f, 140f, Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI));
                fase += ritmo / Taxa;
                float x = 0f;
                if (fase >= 1f)
                {
                    fase -= 1f;
                    x = (0.6f + 0.4f * (float)r.NextDouble()) * (r.NextDouble() < 0.5 ? 1f : -1f) * 3f;
                }
                float metal = 0f;
                foreach (var d in dente) metal += d.Passar(x);
                float roca = Mathf.Sin(Mathf.Clamp01(k * 1.1f) * Mathf.PI) * 0.15f;
                y[i] = macio.Passar(metal * 4f + pano.Passar(Ruido(r) * roca));
            }
            return Acabar(y, 0.65f);
        }

        /// <summary>
        /// A cartela: o alumínio amassando (grãos agudos), o estalo do comprimido furando a folha e o toque dele na palma.
        /// </summary>
        static float[] GerarCartela(int semente)
        {
            var r = new System.Random(semente);
            float dur = 0.34f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            var folha = new Faixa(5200f, 1.4f);
            var estalo = new[] { new Ressonador(2300f, 0.01f, 1f), new Ressonador(3900f, 0.006f, 0.6f) };
            var toque = new Ressonador(1500f, 0.012f, 1f);
            var macio = new PassaBaixa(9500f, 2);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                // o dedo apertando o alumínio até ele ceder (0 a 0,13 s)
                float amassa = t < 0.13f ? Mathf.Clamp01(t / 0.13f) : 0f;
                float grao = r.NextDouble() < 0.03 * amassa ? Ruido(r) * 1.4f : 0f;
                float x = Ruido(r) * Impulso(t, 0.135f, 0.0015f) * 2f;
                float pop = 0f;
                foreach (var e in estalo) pop += e.Passar(x);
                float tic = toque.Passar(Ruido(r) * Impulso(t, 0.24f, 0.001f) * 1.5f);
                y[i] = macio.Passar(folha.Passar(grao) + pop * 3f + tic * 2f);
            }
            return Acabar(y, 0.6f);
        }

        // ------------------------------------------------ ajudantes ------------------------------------------------

        // um impulso que nasce em 'em' e morre em 'tau' segundos
        static float Impulso(float t, float em, float tau) => t < em ? 0f : Mathf.Exp(-(t - em) / tau);

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // normaliza no pico e apaga os últimos 20 ms (sem clique no fim)
        static float[] Acabar(float[] dados, float pico)
        {
            float max = 1e-6f;
            foreach (var v in dados) max = Mathf.Max(max, Mathf.Abs(v));
            float k = pico / max;
            int fim = Mathf.Min(dados.Length, Taxa / 50);
            for (int i = 0; i < dados.Length; i++)
            {
                float rampa = i >= dados.Length - fim ? (dados.Length - 1 - i) / (float)fim : 1f;
                dados[i] *= k * rampa;
            }
            return dados;
        }
    }
}
