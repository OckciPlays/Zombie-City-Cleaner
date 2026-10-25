using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>O chão embaixo do pé, pelo som que faz.</summary>
    public enum Superficie : byte { Concreto, Madeira, Grama, Carpete }

    /// <summary>
    /// Os passos, gerados por código. Cada passo é o calcanhar batendo e, um pouco depois, a ponta do pé (mais perto um
    /// do outro correndo), e o que cada chão faz com isso:
    /// - concreto (ruas, calçadas, a cozinha das casas, o prédio e o bunker): um estalo seco da sola (ruído curto numa
    ///   faixa média), o baque grave do pé e a areia raspando entre as duas batidas (grãos de estalo bem agudos);
    /// - madeira (o taco das casas e dos quartos do bunker, e o deck da varanda): o baque abafado das tábuas (ruído grave,
    ///   sem tom), o "toc" curto da madeira e o estalo do salto;
    /// - grama: sem estalo: o capim amassado (ruído agudo que sobe e desce) com grãos de folha seca quebrando, e um baque
    ///   macio na terra;
    /// - carpete: quase nada: um baque surdo e curto, e o pano roçando bem baixinho.
    /// Quatro variações de cada, andando e correndo.
    /// </summary>
    public static class SomDePasso
    {
        const int Taxa = 44100;
        const int Variacoes = 4;

        /// <summary>Os clipes por superfície ([superfície][correndo ? 1 : 0][variação]).</summary>
        public static AudioClip[][][] Gerar()
        {
            int n = System.Enum.GetValues(typeof(Superficie)).Length;
            var todos = new AudioClip[n][][];
            for (int s = 0; s < n; s++)
            {
                todos[s] = new AudioClip[2][];
                for (int c = 0; c < 2; c++)
                {
                    todos[s][c] = new AudioClip[Variacoes];
                    for (int v = 0; v < Variacoes; v++)
                    {
                        var dados = Passo((Superficie)s, c == 1, 100 * s + 10 * c + v);
                        todos[s][c][v] = SomGerado.Clip($"Passo {(Superficie)s} {(c == 1 ? "correndo" : "andando")} {v + 1}", dados, Taxa);
                    }
                }
            }
            return todos;
        }

        /// <summary>As amostras de um passo.</summary>
        public static float[] Passo(Superficie chao, bool correndo, int semente)
        {
            var r = new System.Random(semente);
            float dur = chao == Superficie.Grama ? 0.34f : 0.28f;
            var y = new float[Mathf.RoundToInt(dur * Taxa)];
            float calcanhar = 0.004f + (float)r.NextDouble() * 0.006f;
            float ponta = calcanhar + (correndo ? 0.032f : 0.058f) + ((float)r.NextDouble() - 0.5f) * 0.016f;
            float forcaPonta = 0.45f + (float)r.NextDouble() * 0.25f;
            switch (chao)
            {
                case Superficie.Concreto: Concreto(y, r, calcanhar, ponta, forcaPonta, correndo); break;
                case Superficie.Madeira: Madeira(y, r, calcanhar, ponta, forcaPonta, correndo); break;
                case Superficie.Carpete: Carpete(y, r, calcanhar, ponta, forcaPonta, correndo); break;
                default: Grama(y, r, calcanhar, ponta, forcaPonta, correndo); break;
            }
            float pico = chao == Superficie.Grama ? 0.5f : chao == Superficie.Carpete ? 0.45f : 0.8f;
            Normalizar(y, pico);
            return y;
        }

        static void Concreto(float[] y, System.Random r, float t0, float t1, float ponta, bool correndo)
        {
            // a sola e o salto no chão duro: um estalo médio (o "toc") com o corpo grave do salto por baixo
            var sola = new Faixa(1300f + (float)r.NextDouble() * 500f, 0.7f);
            var salto = new Faixa(520f, 1.2f);
            var areia = new Faixa(5200f, 1.4f);
            var raspa = new Faixa(3200f, 0.7f);
            // um passa-baixa suave em cima de tudo: sem ele, o ruído deixava o passo chiado (mais da metade da energia
            // acima de 4 kHz)
            float baque = 0f, macio = 0f, a = 1f - Mathf.Exp(-2f * Mathf.PI * 4200f / Taxa);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float d0 = t - t0, d1 = t - t1;
                // o estalo da sola: ruído que morre em poucos milissegundos, na batida do calcanhar e na da ponta
                float golpe = (d0 >= 0f ? Mathf.Exp(-d0 / 0.006f) : 0f) + (d1 >= 0f ? ponta * Mathf.Exp(-d1 / 0.005f) : 0f);
                float x = Ruido(r) * golpe;
                float s = sola.Passar(x) * 1.6f + salto.Passar(x) * 0.9f;
                // o baque do pé (o peso do corpo): baixo, só para dar corpo (forte, o passo virava um "bum")
                if (d0 >= 0f) baque = Mathf.Sin(2f * Mathf.PI * 82f * d0) * Mathf.Exp(-d0 / 0.022f) * (correndo ? 0.14f : 0.1f);
                if (d1 >= 0f) baque += Mathf.Sin(2f * Mathf.PI * 118f * d1) * Mathf.Exp(-d1 / 0.016f) * 0.06f * ponta;
                // a areia: grãos soltos entre o calcanhar e um pouco depois da ponta
                float janela = t > t0 && t < t1 + 0.05f ? Mathf.Sin(Mathf.PI * (t - t0) / (t1 + 0.05f - t0)) : 0f;
                float grao = r.NextDouble() < 0.0016 * janela ? Ruido(r) * 1.6f : 0f;
                float raspado = raspa.Passar(Ruido(r) * janela * 0.08f);
                macio += a * (s + areia.Passar(grao) + raspado - macio);
                y[i] = macio + baque;
            }
        }

        // o "toc" da madeira: poucos modos, bem curtos (Hz, duração em s, peso), desafinados a cada passo. Longos (de 12 a
        // 50 ms, como eram), eles davam um tom, e o passo soava como bater num bongô. O peso é o da energia de cada um
        // (veja o ganho em Madeira)
        static readonly (float f, float d, float g)[] Tabuas =
        {
            (210f, 0.010f, 0.5f), (360f, 0.010f, 0.9f), (600f, 0.008f, 0.8f), (950f, 0.006f, 0.6f), (1450f, 0.004f, 0.4f),
        };

        static void Madeira(float[] y, System.Random r, float t0, float t1, float ponta, bool correndo)
        {
            var modos = new Ressonador[Tabuas.Length];
            for (int k = 0; k < modos.Length; k++)
            {
                var (f, d, g) = Tabuas[k];
                f *= 0.85f + 0.3f * (float)r.NextDouble();
                // com o ganho do Ressonador, a energia de um modo cai com f² e com a duração: sem compensar, só o mais
                // grave aparecia, e o passo virava um "bum" abafado (70% da energia entre 125 e 250 Hz)
                modos[k] = new Ressonador(f, d, g * (f / 210f) * Mathf.Sqrt(0.01f / d));
            }
            var tabua = new PassaBaixa(260f, 2);   // o baque das tábuas: ruído grave, sem tom
            var salto = new Faixa(2600f, 1f);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float d0 = t - t0, d1 = t - t1;
                // o pé descendo: sobe em 2,5 ms (o salto amassa) e some em ~12 ms
                float golpe = (d0 >= 0f ? Mathf.Clamp01(d0 / 0.0025f) * Mathf.Exp(-d0 / 0.012f) : 0f)
                              + (d1 >= 0f ? ponta * Mathf.Clamp01(d1 / 0.002f) * Mathf.Exp(-d1 / 0.009f) : 0f);
                float x = Ruido(r) * golpe * (correndo ? 1f : 0.8f);
                float madeira = 0f;
                foreach (var m in modos) madeira += m.Passar(x);
                float clique = salto.Passar(Ruido(r) * (d0 >= 0f ? Mathf.Exp(-d0 / 0.0015f) : 0f));
                y[i] = tabua.Passar(x) * 2.2f + madeira * 2.6f + clique * 0.7f;
            }
        }

        static void Carpete(float[] y, System.Random r, float t0, float t1, float ponta, bool correndo)
        {
            var baque = new PassaBaixa(240f, 2);
            var pano = new Faixa(3000f, 0.7f);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float d0 = t - t0, d1 = t - t1;
                // o pé afunda no carpete: sobe devagar (4 ms) e some em 20 ms
                float golpe = (d0 >= 0f ? Mathf.Clamp01(d0 / 0.004f) * Mathf.Exp(-d0 / 0.02f) : 0f)
                              + (d1 >= 0f ? 0.6f * ponta * Mathf.Clamp01(d1 / 0.004f) * Mathf.Exp(-d1 / 0.016f) : 0f);
                float x = Ruido(r) * golpe * (correndo ? 1f : 0.8f);
                y[i] = baque.Passar(x) * 4f + pano.Passar(x) * 0.05f;
            }
        }

        static void Grama(float[] y, System.Random r, float t0, float t1, float ponta, bool correndo)
        {
            var capim = new Faixa(4300f, 0.6f);
            var folhas = new Faixa(3000f + (float)r.NextDouble() * 2000f, 2f);
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)Taxa;
                float d0 = t - t0, d1 = t - t1;
                // o capim amassando: sobe em ~10 ms e some devagar (mais longo correndo)
                float sobe0 = Mathf.Clamp01(d0 / 0.012f), sobe1 = Mathf.Clamp01(d1 / 0.01f);
                float env = (d0 >= 0f ? sobe0 * Mathf.Exp(-d0 / (correndo ? 0.075f : 0.06f)) : 0f)
                            + (d1 >= 0f ? ponta * sobe1 * Mathf.Exp(-d1 / 0.05f) : 0f);
                float x = capim.Passar(Ruido(r) * env * 0.5f);
                // folhas secas quebrando: grãos que aparecem mais onde o capim está mais amassado
                float grao = r.NextDouble() < 0.005 * env ? Ruido(r) * 3f : 0f;
                float baque = d0 >= 0f ? Mathf.Sin(2f * Mathf.PI * 68f * d0) * Mathf.Exp(-d0 / 0.035f) * 0.12f : 0f;
                y[i] = x + folhas.Passar(grao) + baque;
            }
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        static void Normalizar(float[] dados, float pico)
        {
            float max = 1e-6f;
            foreach (var v in dados) max = Mathf.Max(max, Mathf.Abs(v));
            float k = pico / max;
            for (int i = 0; i < dados.Length; i++) dados[i] *= k;
            // sem estalo no fim: os últimos 20 ms descem até zero
            int fim = Mathf.Min(dados.Length, Taxa / 50);
            for (int i = 0; i < fim; i++) dados[dados.Length - 1 - i] *= i / (float)fim;
        }

        /// <summary>
        /// Um passa-faixa de estado variável (centro em Hz e Q). Ele só é estável com f² + 2·f·q &lt; 4 (f é o coeficiente
        /// da frequência e q o do amortecimento): no agudo e com Q baixo, ele explodia. O "ar" do som de abrir a lata
        /// (6,5 kHz, Q 0,5) saía inteiro NaN e calava o jogo. Agora a frequência é limitada a 90% do limite.
        /// </summary>
        internal class Faixa
        {
            readonly float _q;
            float _f, _baixa, _faixa;

            public Faixa(float centro, float q)
            {
                _q = 1f / Mathf.Max(0.3f, q);
                Centro(centro);
            }

            /// <summary>Muda o centro (o filtro continua de onde estava).</summary>
            public void Centro(float centro)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centro, Taxa / 6f) / Taxa);
                _f = Mathf.Min(f, 0.9f * (Mathf.Sqrt(_q * _q + 4f) - _q));
            }

            public float Passar(float x)
            {
                _baixa += _f * _faixa;
                float alta = x - _baixa - _q * _faixa;
                _faixa += _f * alta;
                return _faixa;
            }
        }

        /// <summary>Um passa-baixa de um polo, repetido 'ordem' vezes (cada vez, 6 dB por oitava a mais de corte).</summary>
        internal class PassaBaixa
        {
            readonly float _a;
            readonly float[] _y;

            public PassaBaixa(float corte, int ordem = 1)
            {
                _a = 1f - Mathf.Exp(-2f * Mathf.PI * corte / Taxa);
                _y = new float[Mathf.Max(1, ordem)];
            }

            public float Passar(float x)
            {
                for (int k = 0; k < _y.Length; k++)
                {
                    _y[k] += _a * (x - _y[k]);
                    x = _y[k];
                }
                return x;
            }
        }

        /// <summary>Um modo de vibração: um ressonador de dois polos que soa na frequência e morre na duração dada.</summary>
        internal class Ressonador
        {
            readonly float _a1, _a2, _g;
            float _y1, _y2;

            public Ressonador(float frequencia, float duracao, float peso)
            {
                float raio = Mathf.Exp(-1f / (duracao * Taxa));
                _a1 = 2f * raio * Mathf.Cos(2f * Mathf.PI * frequencia / Taxa);
                _a2 = -raio * raio;
                _g = peso * (1f - raio);
            }

            public float Passar(float x)
            {
                float y = _a1 * _y1 + _a2 * _y2 + x * _g;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }
}
