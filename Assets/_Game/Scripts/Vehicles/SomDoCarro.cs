using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// Os sons do carro, gerados por código (como os do tiro e dos passos), feitos na primeira vez que são pedidos:
    /// o motor (um laço de 1 s, tocado com o pitch da rotação), a partida (a chave entrando e girando, o solenoide, o
    /// motor de arranque em laço, o pinhão soltando e o motor pegando), a buzina, a porta abrindo e a porta batendo.
    /// </summary>
    public static class SomDoCarro
    {
        const int Taxa = 44100;
        static AudioClip _motor, _arranque, _pegar, _solenoide, _soltar, _clique, _entra, _abrir, _fechar, _buzina;

        /// <summary>O motor em marcha lenta, em laço (1 s; com pitch 1 são 32 explosões por segundo).</summary>
        public static AudioClip Motor => _motor != null ? _motor : _motor = SomGerado.Clip("Motor do carro", GerarMotor(7));
        /// <summary>O motor de arranque girando o motor, em laço (1 s): toca enquanto a chave fica na partida.</summary>
        public static AudioClip Arranque => _arranque != null ? _arranque : _arranque = SomGerado.Clip("Arranque do carro", GerarArranque(8));
        /// <summary>O motor pegando e caindo para a marcha lenta (o laço do motor entra em <see cref="LacoEntra"/>).</summary>
        public static AudioClip Pegar => _pegar != null ? _pegar : _pegar = SomGerado.Clip("Motor pegando", GerarPegar(12));
        public static AudioClip Solenoide => _solenoide != null ? _solenoide : _solenoide = SomGerado.Clip("Solenoide do arranque", GerarSolenoide(13));
        public static AudioClip SoltarArranque => _soltar != null ? _soltar : _soltar = SomGerado.Clip("Arranque soltando", GerarSoltar(14));
        public static AudioClip CliqueDaChave => _clique != null ? _clique : _clique = SomGerado.Clip("Chave girando", GerarCliqueDaChave(15));
        public static AudioClip ChaveEntra => _entra != null ? _entra : _entra = SomGerado.Clip("Chave entrando", GerarChaveEntra(16));
        /// <summary>O motor tossindo (quase pegando): um acerto no mini-game da partida.</summary>
        public static AudioClip Tossida => _tossida != null ? _tossida : _tossida = SomGerado.Clip("Motor tossindo", GerarTossida(17));
        static AudioClip _tossida;
        /// <summary>O pneu furado por um tiro: o estouro e o ar escapando.</summary>
        public static AudioClip PneuFurando => _furando != null ? _furando : _furando = SomGerado.Clip("Pneu furando", GerarPneuFurando(18));
        static AudioClip _furando;
        /// <summary>O macaco sanfona girando (2,2 s, o tempoDoMacaco do carro): a catraca da manivela e o fuso rangendo.</summary>
        public static AudioClip Macaco => _macaco != null ? _macaco : _macaco = SomGerado.Clip("Macaco do carro", GerarMacaco(19));
        static AudioClip _macaco;
        public static AudioClip PortaAbrir => _abrir != null ? _abrir : _abrir = SomGerado.Clip("Porta do carro abrindo", GerarAbrir(9));
        public static AudioClip PortaFechar => _fechar != null ? _fechar : _fechar = SomGerado.Clip("Porta do carro batendo", GerarFechar(10));
        /// <summary>A buzina, em laço (1 s).</summary>
        public static AudioClip Buzina => _buzina != null ? _buzina : _buzina = SomGerado.Clip("Buzina do carro", GerarBuzina(11));

        /// <summary>
        /// Um motor de 4 cilindros: os harmônicos das explosões (32 por segundo, todos inteiros em 1 s, para o laço não ter
        /// emenda), a aspereza de meio ciclo e de um ciclo inteiro (16 e 8 Hz) e o ronco do escape (ruído grave que pulsa
        /// com cada explosão). O ruído é gerado por 2 s e só o segundo é usado (o filtro já está assentado).
        /// </summary>
        static float[] GerarMotor(int semente)
        {
            var r = new System.Random(semente);
            const float f0 = 32f;
            const int harmonicos = 14;
            var fase = new float[harmonicos + 2];
            for (int k = 0; k < fase.Length; k++) fase[k] = (float)(r.NextDouble() * Mathf.PI * 2);
            var ronco = new SomDePasso.PassaBaixa(650f, 2);
            var corpo = new SomDePasso.Ressonador(96f, 0.035f, 1f);
            int n = Taxa;
            var d = new float[n];
            for (int i = 0; i < 2 * n; i++)
            {
                float t = (i % n) / (float)Taxa;
                float faseDaExplosao = (t * f0) % 1f;
                float pulso = Mathf.Exp(-faseDaExplosao * 7f);
                float ruido = ronco.Passar((float)(r.NextDouble() * 2 - 1)) * pulso;
                float baque = corpo.Passar(faseDaExplosao < 1f / 64f ? (float)(r.NextDouble() * 2 - 1) : 0f);
                if (i < n) continue;
                float h = 0f;
                for (int k = 1; k <= harmonicos; k++)
                    h += Mathf.Sin(2f * Mathf.PI * k * f0 * t + fase[k]) / Mathf.Pow(k, 0.9f) * (k % 2 == 0 ? 0.75f : 1f);
                h += 0.35f * Mathf.Sin(2f * Mathf.PI * 16f * t + fase[0]);
                h += 0.15f * Mathf.Sin(2f * Mathf.PI * 8f * t + fase[harmonicos + 1]);
                d[i - n] = h * 0.35f + ruido * 2.2f + baque * 0.8f;
            }
            return Normalizar(d, 0.8f);
        }

        /// <summary>
        /// Quando o laço do motor entra (s, do começo do <see cref="Pegar"/>): o motor que pegou já assentou na marcha
        /// lenta, e o som de pegar começa a sumir.
        /// </summary>
        public const float LacoEntra = 0.69f;

        /// <summary>
        /// O motor de arranque girando o motor (um laço de 1 s), como o de um 4 cilindros dos anos 90: o zunido da
        /// engrenagem no volante do motor (~370 Hz e os harmônicos, com o chiado das escovas), que perde força a cada
        /// compressão (5 por segundo), e o sopro grave do ar de cada cilindro soltando. A rotação do arranque é periódica
        /// em 1 s, e as frequências são acertadas para dar um número inteiro de voltas nesse segundo: o laço não tem
        /// emenda. O ruído é gerado por 2 s, e só o segundo é usado (os filtros já assentaram).
        /// </summary>
        static float[] GerarArranque(int semente)
        {
            var r = new System.Random(semente);
            int n = Taxa;
            var d = new float[n];
            var escovas = new SomDePasso.Faixa(3200f, 1.5f);
            var dentes = new SomDePasso.Faixa(1150f, 4f);
            var sopro = new SomDePasso.PassaBaixa(190f, 2);
            var bloco = new SomDePasso.Ressonador(85f, 0.06f, 1f);
            var lp = new SomDePasso.PassaBaixa(5500f, 2);
            double voltas = 0;
            for (int i = 0; i < n; i++) voltas += RotacaoDoArranque(i / (double)Taxa) / Taxa;
            double fDentes = System.Math.Round(370 * voltas) / voltas, fArmadura = System.Math.Round(58 * voltas) / voltas;
            double faseDentes = 0, faseArmadura = 0;
            for (int i = 0; i < 2 * n; i++)
            {
                double t = i / (double)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float naCompressao = (float)(t * 5.0 % 1.0);
                float comp = Compressao(naCompressao);
                float w = RotacaoDoArranque(t);
                faseDentes += 2 * System.Math.PI * fDentes * w / Taxa;
                faseArmadura += 2 * System.Math.PI * fArmadura * w / Taxa;
                float fd = (float)faseDentes, fa = (float)faseArmadura;
                float zunido = Mathf.Sin(fd) * 0.5f + Mathf.Sin(2f * fd + 0.7f) * 0.3f + Mathf.Sin(3f * fd + 1.3f) * 0.15f;
                float armadura = Mathf.Sin(fa) * 0.3f + Mathf.Sin(2f * fa + 0.4f) * 0.12f;
                float chiado = escovas.Passar(ruido) * 0.25f + dentes.Passar(ruido) * (0.4f + 0.6f * w) * 0.5f;
                float carga = 0.65f + 0.5f * comp;   // na compressão, ele força (mais alto)
                float giro = (zunido * 0.35f + armadura + chiado) * w * carga;
                // o ar de cada cilindro soltando, logo depois da compressão
                float dx = Mathf.Repeat(naCompressao - 0.8f + 0.5f, 1f) - 0.5f;
                float ar = sopro.Passar(ruido) * Mathf.Exp(-dx * dx / (0.07f * 0.07f)) * 4f;
                float s = lp.Passar(giro + ar * 1.5f + bloco.Passar(ar) * 2f);
                if (i >= n) d[i - n] = s;
            }
            return Normalizar(d, 0.7f);
        }

        // a força da compressão (0 a 1) na fração do ciclo de um cilindro: o pico em 0,62, contado em volta (sem emenda)
        static float Compressao(float naCompressao)
        {
            float dx = Mathf.Repeat(naCompressao - 0.62f + 0.5f, 1f) - 0.5f;
            return Mathf.Exp(-dx * dx / (0.16f * 0.16f));
        }

        // a rotação do arranque (1 = solto): cai 30% em cada compressão
        static float RotacaoDoArranque(double t) => 1f - 0.3f * Compressao((float)(t * 5.0 % 1.0));

        /// <summary>
        /// O motor pegando (1,25 s): as primeiras explosões falham, ele acelera até ~1.700 rpm (58 explosões por segundo)
        /// e cai devagar até a marcha lenta (30 por segundo, a do laço, feita com a mesma conta). De 0,74 a 1,14 s ele
        /// some, e o laço (<see cref="Motor"/>) entra em <see cref="LacoEntra"/>. O motor de arranque é outro som
        /// (<see cref="Arranque"/>): ele continua girando enquanto a chave fica na partida.
        /// </summary>
        static float[] GerarPegar(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(1.25f * Taxa);
            var motor = new float[n];
            const int harmonicos = 14;
            var fase = new float[harmonicos + 2];
            for (int k = 0; k < fase.Length; k++) fase[k] = (float)(r.NextDouble() * Mathf.PI * 2);
            var escape = new SomDePasso.PassaBaixa(650f, 2);
            var corpo = new SomDePasso.Ressonador(96f, 0.035f, 1f);
            const float Pega = 0.02f, Sobe = Pega + 0.32f;
            double faseMotor = 0;
            int ciclo = -1;
            float ganhoDoCiclo = 0f;
            float picoNaLenta = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float f;
                if (t < Pega) f = 0f;
                else if (t < Sobe) f = Mathf.Lerp(8f, 58f, Mathf.SmoothStep(0f, 1f, (t - Pega) / (Sobe - Pega)));
                else f = 30f + 28f * Mathf.Exp(-(t - Sobe) * 5f);
                faseMotor += f / Taxa;
                float p = (float)(faseMotor % 1.0);
                int c = (int)faseMotor;
                if (t >= Pega && c != ciclo)
                {
                    ciclo = c;
                    // as primeiras falham de vez em quando
                    ganhoDoCiclo = c < 7 && r.NextDouble() < 0.35 ? 0.25f : 1f;
                }
                float pulso = Mathf.Exp(-p * 7f) * ganhoDoCiclo;
                float explosao = escape.Passar(ruido) * pulso;
                float baque = corpo.Passar(t >= Pega && p < 1f / 64f ? ruido * ganhoDoCiclo : 0f);
                float h = 0f;
                double ang = 2.0 * System.Math.PI * faseMotor;
                for (int k = 1; k <= harmonicos; k++)
                    h += (float)System.Math.Sin(k * ang + fase[k]) / Mathf.Pow(k, 0.9f) * (k % 2 == 0 ? 0.75f : 1f);
                h += 0.35f * (float)System.Math.Sin(0.5 * ang + fase[0]);
                h += 0.15f * (float)System.Math.Sin(0.25 * ang + fase[harmonicos + 1]);
                float tom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Pega + 0.02f, Pega + 0.22f, t));   // o ronco firma quando ele pega
                float m = h * 0.35f * tom + explosao * 2.2f + baque * 0.8f;
                if (t > Pega + 0.52f && t < Pega + 0.67f) picoNaLenta = Mathf.Max(picoNaLenta, Mathf.Abs(m));
                float acelera = 1f + 0.45f * Mathf.Exp(-Mathf.Pow((t - Sobe) / 0.18f, 2f));
                float some = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Pega + 0.72f, Pega + 1.12f, t));
                motor[i] = m * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Pega - 0.01f, Pega + 0.05f, t)) * acelera * some;
            }
            // na marcha lenta, no nível do laço (o carro toca este som a 0,6 e o laço a ~0,4)
            float k2 = 0.8f * 0.40f / 0.6f / picoNaLenta;
            float max = 0f;
            for (int i = 0; i < n; i++) { motor[i] *= k2; max = Mathf.Max(max, Mathf.Abs(motor[i])); }
            return max > 0.95f ? Normalizar(motor, 0.95f) : motor;
        }

        /// <summary>O solenoide do arranque estalando (a chave chegou na partida).</summary>
        static float[] GerarSolenoide(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.14f * Taxa);
            var d = new float[n];
            var sol1 = new SomDePasso.Ressonador(1250f, 0.012f, 1f);
            var sol2 = new SomDePasso.Ressonador(2700f, 0.008f, 1f);
            var corpo = new SomDePasso.Ressonador(150f, 0.03f, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float clique = (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 700f);
                d[i] = sol1.Passar(clique) * 2f + sol2.Passar(clique) * 1.2f + corpo.Passar(clique) * 3f;
            }
            return Normalizar(d, 0.7f);
        }

        /// <summary>
        /// A chave saindo da partida: o pinhão do arranque volta (um estalo seco) e o motor de arranque, solto, desacelera
        /// (0,4 s).
        /// </summary>
        static float[] GerarSoltar(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.4f * Taxa);
            var d = new float[n];
            var sol1 = new SomDePasso.Ressonador(1250f, 0.012f, 1f);
            var sol2 = new SomDePasso.Ressonador(2700f, 0.008f, 1f);
            var corpo = new SomDePasso.Ressonador(150f, 0.03f, 1f);
            var dentes = new SomDePasso.Faixa(1150f, 4f);
            var lp = new SomDePasso.PassaBaixa(5500f, 2);
            float faseDentes = 0f, faseArmadura = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float clique = ruido * Mathf.Exp(-t * 900f) * 0.55f;
                float estalo = sol1.Passar(clique) * 2f + sol2.Passar(clique) * 1.2f + corpo.Passar(clique) * 3f;
                float w = Mathf.Exp(-t * 14f);
                faseDentes += 2f * Mathf.PI * 370f * w / Taxa;
                faseArmadura += 2f * Mathf.PI * 58f * w / Taxa;
                float zunido = Mathf.Sin(faseDentes) * 0.5f + Mathf.Sin(2f * faseDentes + 0.7f) * 0.3f + Mathf.Sin(3f * faseDentes + 1.3f) * 0.15f;
                float armadura = Mathf.Sin(faseArmadura) * 0.3f + Mathf.Sin(2f * faseArmadura + 0.4f) * 0.12f;
                float giro = (zunido * 0.35f + armadura + dentes.Passar(ruido) * (0.4f + 0.6f * w) * 0.5f) * w * 0.65f;
                d[i] = lp.Passar(giro) + estalo;
            }
            // o arranque no mesmo nível do laço dele (0,7)
            float max = 0f;
            foreach (var x in d) max = Mathf.Max(max, Mathf.Abs(x));
            return max > 0.85f ? Normalizar(d, 0.85f) : d;
        }

        /// <summary>A chave passando de uma posição para outra: o estalo do miolo (dois toques metálicos curtos).</summary>
        static float[] GerarCliqueDaChave(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.06f * Taxa);
            var d = new float[n];
            var a = new SomDePasso.Ressonador(3600f, 0.004f, 1f);
            var b = new SomDePasso.Ressonador(6100f, 0.003f, 1f);
            var c = new SomDePasso.Ressonador(1400f, 0.006f, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float x = ruido * (Mathf.Exp(-t * 2500f) + 0.5f * Degrau(t - 0.009f) * Mathf.Exp(-Mathf.Max(0f, t - 0.009f) * 2500f));
                d[i] = a.Passar(x) + b.Passar(x) * 0.6f + c.Passar(x) * 0.8f;
            }
            return Normalizar(d, 0.5f);
        }

        /// <summary>
        /// O motor tossindo (0,35 s): duas explosões soltas, a segunda mais fraca (o ronco do escape pulsando, o baque do
        /// bloco e um pouco do tom do motor), como as primeiras do <see cref="Pegar"/>, sem ele firmar.
        /// </summary>
        static float[] GerarTossida(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.35f * Taxa);
            var d = new float[n];
            var escape = new SomDePasso.PassaBaixa(650f, 2);
            var corpo = new SomDePasso.Ressonador(96f, 0.035f, 1f);
            var bloco = new SomDePasso.Ressonador(180f, 0.02f, 1f);
            float[] quando = { 0f, 0.075f }, forca = { 1f, 0.55f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float pulso = 0f, golpe = 0f;
                for (int k = 0; k < quando.Length; k++)
                {
                    float tk = t - quando[k];
                    if (tk < 0f) continue;
                    pulso += Mathf.Exp(-tk * 28f) * forca[k] * Mathf.SmoothStep(0f, 1f, tk / 0.003f);
                    if (tk < 0.0025f) golpe += ruido * forca[k];
                }
                d[i] = escape.Passar(ruido) * pulso * 2.4f + corpo.Passar(golpe) * 1.2f + bloco.Passar(golpe) * 0.6f;
            }
            return Normalizar(d, 0.75f);
        }

        /// <summary>A chave entrando na ignição: os pinos do miolo estalando um a um (cada vez mais agudos), o metal raspando e o encaixe no fim.</summary>
        static float[] GerarChaveEntra(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.32f * Taxa);
            var d = new float[n];
            var raspa = new SomDePasso.Faixa(4200f, 2f);
            var pinos = new SomDePasso.Ressonador[5];
            for (int k = 0; k < pinos.Length; k++) pinos[k] = new SomDePasso.Ressonador(3000f + 450f * k, 0.004f, 1f);
            var encaixe = new SomDePasso.Ressonador(1700f, 0.01f, 1f);
            var corpo = new SomDePasso.Ressonador(420f, 0.012f, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float s = raspa.Passar(ruido) * 0.06f * Mathf.SmoothStep(0f, 1f, t / 0.02f) * (t < 0.22f ? 1f : Mathf.Exp(-(t - 0.22f) * 80f));
                for (int k = 0; k < pinos.Length; k++)
                {
                    float tk = t - (0.03f + k * 0.038f);
                    s += pinos[k].Passar(Degrau(tk) * ruido * Mathf.Exp(-Mathf.Max(0f, tk) * 3000f));
                }
                float te = t - 0.235f;
                float x = Degrau(te) * ruido * Mathf.Exp(-Mathf.Max(0f, te) * 1500f);
                s += encaixe.Passar(x) * 1.6f + corpo.Passar(x) * 1.2f;
                d[i] = s;
            }
            return Normalizar(d, 0.5f);
        }

        /// <summary>
        /// A buzina (um laço de 1 s): duas cornetas eletromagnéticas em terça maior (412 e 518 Hz, inteiros para o laço
        /// não ter emenda), cada uma com os harmônicos do diafragma, reforçados pela boca da corneta (~850 Hz e ~2,4 kHz),
        /// e uma saturação que dá a aspereza.
        /// </summary>
        static float[] GerarBuzina(int semente)
        {
            var r = new System.Random(semente);
            int n = Taxa;
            var d = new float[n];
            int[] tons = { 412, 518 };
            var freqs = new System.Collections.Generic.List<float>();
            var ganhos = new System.Collections.Generic.List<float>();
            var fases = new System.Collections.Generic.List<float>();
            foreach (int f0 in tons)
                for (int k = 1; k * f0 < 9000; k++)
                {
                    float fk = k * f0;
                    float g = 1f / Mathf.Pow(k, 0.6f) * (0.35f + 1.6f * Mathf.Exp(-Mathf.Pow((fk - 2400f) / 900f, 2f)) + 0.8f * Mathf.Exp(-Mathf.Pow((fk - 850f) / 350f, 2f)));
                    freqs.Add(fk);
                    ganhos.Add(g);
                    fases.Add((float)(r.NextDouble() * Mathf.PI * 2));
                }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float s = 0f;
                for (int j = 0; j < freqs.Count; j++) s += ganhos[j] * Mathf.Sin(2f * Mathf.PI * freqs[j] * t + fases[j]);
                s *= 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 6f * t);   // o diafragma tremendo um pouco
                d[i] = (float)System.Math.Tanh(s * 0.35f);
            }
            return Normalizar(d, 0.8f);
        }

        /// <summary>A porta abrindo: o trinco soltando (dois estalos de metal) e a borracha da porta descolando.</summary>
        static float[] GerarAbrir(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.28f * Taxa);
            var d = new float[n];
            var trinco = new SomDePasso.Faixa(2800f, 2f);
            var metal1 = new SomDePasso.Ressonador(1900f, 0.03f, 1f);
            var metal2 = new SomDePasso.Ressonador(3300f, 0.02f, 1f);
            var borracha = new SomDePasso.Ressonador(180f, 0.05f, 1f);
            var lp = new SomDePasso.PassaBaixa(5000f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                float estalo = Mathf.Exp(-t * 900f) + 0.45f * Degrau(t - 0.022f) * Mathf.Exp(-Mathf.Max(0f, t - 0.022f) * 1100f);
                float x = ruido * estalo;
                float s = trinco.Passar(x) * 0.6f + metal1.Passar(x) * 3f + metal2.Passar(x) * 2f;
                float desc = Degrau(t - 0.045f) * Mathf.Exp(-Mathf.Max(0f, t - 0.045f) * 60f) * ruido;
                s += borracha.Passar(desc) * 2.5f;
                d[i] = lp.Passar(s);
            }
            return Normalizar(d, 0.7f);
        }

        /// <summary>
        /// A porta batendo: o baque grave da porta no batente (um seno que cai de 120 para 55 Hz), as chapas vibrando
        /// (240, 410 e 690 Hz), o trinco fechando por cima e um chacoalhar curto logo depois.
        /// </summary>
        static float[] GerarFechar(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(0.45f * Taxa);
            var d = new float[n];
            var chapa1 = new SomDePasso.Ressonador(240f, 0.07f, 1f);
            var chapa2 = new SomDePasso.Ressonador(410f, 0.05f, 1f);
            var chapa3 = new SomDePasso.Ressonador(690f, 0.035f, 1f);
            var trinco = new SomDePasso.Faixa(3000f, 2f);
            var metal = new SomDePasso.Ressonador(2300f, 0.02f, 1f);
            var lp = new SomDePasso.PassaBaixa(4000f);
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                fase += 2f * Mathf.PI * Mathf.Lerp(55f, 120f, Mathf.Exp(-t * 30f)) / Taxa;
                float baque = Mathf.Sin(fase) * Mathf.Exp(-t * 28f) * Mathf.Clamp01(t / 0.002f);
                float golpe = ruido * Mathf.Exp(-t * 200f);
                float chapas = chapa1.Passar(golpe) * 2.2f + chapa2.Passar(golpe) * 1.6f + chapa3.Passar(golpe) * 1.1f;
                float tt = t - 0.004f;
                float clique = Degrau(tt) * ruido * Mathf.Exp(-Mathf.Max(0f, tt) * 1200f);
                float trava = trinco.Passar(clique) * 0.5f + metal.Passar(clique) * 2f;
                float tc = t - 0.07f;
                float chacoalho = Degrau(tc) * ruido * Mathf.Exp(-Mathf.Max(0f, tc) * 700f) * 0.15f;
                d[i] = lp.Passar(baque * 1.3f + chapas + trava + metal.Passar(chacoalho));
            }
            return Normalizar(d, 0.9f);
        }

        /// <summary>
        /// O pneu furando: o estouro (um baque grave da borracha, um seno que cai de 140 para 60 Hz, e o estalo) e o ar
        /// escapando, um chiado forte que perde força e escurece (de ~4,2 para ~1,8 kHz) em 1,8 s.
        /// </summary>
        static float[] GerarPneuFurando(int semente)
        {
            var r = new System.Random(semente);
            int n = Mathf.RoundToInt(1.9f * Taxa);
            var d = new float[n];
            var ar = new SomDePasso.Faixa(4200f, 1.2f);
            var estalo = new SomDePasso.Faixa(1800f, 1.5f);
            var lp = new SomDePasso.PassaBaixa(9000f);
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                fase += 2f * Mathf.PI * Mathf.Lerp(60f, 140f, Mathf.Exp(-t * 35f)) / Taxa;
                float baque = Mathf.Sin(fase) * Mathf.Exp(-t * 22f) * Mathf.Clamp01(t / 0.0015f);
                float golpe = estalo.Passar(ruido * Mathf.Exp(-t * 160f)) * 1.4f;
                if (i % 64 == 0) ar.Centro(Mathf.Lerp(1800f, 4200f, Mathf.Exp(-t * 1.2f)));
                float sopro = Mathf.Clamp01((t - 0.01f) / 0.04f) * Mathf.Exp(-t * 1.6f) * (0.85f + 0.15f * Mathf.Sin(t * 37f));
                d[i] = lp.Passar(baque * 1.2f + golpe + ar.Passar(ruido) * sopro * 0.9f);
            }
            return Normalizar(d, 0.85f);
        }

        /// <summary>
        /// O macaco: um estalo de catraca a cada meia volta da manivela (~0,17 s, desafinado), o metal tocando em dois modos
        /// (1,87 e 3,15 kHz) e o fuso rangendo baixo, que sobe e desce com a volta.
        /// </summary>
        static float[] GerarMacaco(int semente)
        {
            var r = new System.Random(semente);
            const float dura = 2.2f;
            int n = Mathf.RoundToInt(dura * Taxa);
            var d = new float[n];
            var metal = new SomDePasso.Faixa(2600f, 3f);
            var a = new SomDePasso.Ressonador(1870f, 0.02f, 1f);
            var b = new SomDePasso.Ressonador(3150f, 0.012f, 1f);
            var fuso = new SomDePasso.Faixa(700f, 2f);
            float proximo = 0.03f, ultimo = -1f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                if (t >= proximo)
                {
                    ultimo = t;
                    proximo = t + 0.15f + (float)r.NextDouble() * 0.05f;
                }
                float desde = ultimo < 0f ? 1f : t - ultimo;
                float golpe = ruido * Mathf.Exp(-desde * 900f);
                float estalo = metal.Passar(golpe) + a.Passar(golpe) * 40f + b.Passar(golpe) * 30f;
                float volta = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 0.34f);
                float rangido = fuso.Passar(ruido) * 0.08f * volta;
                float bordas = Mathf.Clamp01((dura - t) / 0.15f) * Mathf.Clamp01(t / 0.02f);
                d[i] = (estalo + rangido) * bordas;
            }
            return Normalizar(d, 0.8f);
        }

        static float Degrau(float x) => x >= 0f ? 1f : 0f;

        static float[] Normalizar(float[] d, float pico)
        {
            float max = 1e-6f;
            foreach (var x in d) max = Mathf.Max(max, Mathf.Abs(x));
            float k = pico / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
            return d;
        }
    }
}
