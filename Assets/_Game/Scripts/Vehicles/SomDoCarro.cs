using System;
using System.Threading.Tasks;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// Os sons do carro. Os gravados (Resources/Carro, <see cref="Gravado"/>): a partida (a chave entrando e girando, o
    /// motor de arranque em laço e o motor pegando), o motor desligando, os laços do motor de cada modelo, a buzina, as
    /// portas, o capô fechando, o pneu furando, o ar vazando, o vapor e as batidas. Desde 20/11/2026 (a pedido: "podemos
    /// excluir os sons que já foram substituídos"), eles não têm mais a reserva feita em código; sem a gravação, null.
    /// Continuam gerados por código (como os dos passos) os que não têm gravação: o laço único do motor (o dos carros sem
    /// os laços gravados), o solenoide, o pinhão soltando, a tossida, o macaco, o abastecer e o capô (e a tampa do
    /// porta-malas) abrindo, e a reserva do capô fechando. As amostras são geradas em outra thread no começo da partida
    /// (<see cref="Preparar"/>, na tela de carregamento), e cada clipe sai delas na primeira vez que é pedido: geradas na
    /// hora, o motor e a buzina paravam o jogo por uns 150 ms no primeiro quadro do carro (31/10/2026).
    /// </summary>
    public static class SomDoCarro
    {
        const int Taxa = 44100;
        static AudioClip _motor, _solenoide, _soltar, _abrir, _fechar;

        // as amostras de cada som, pela semente dele (7 a 22: a posição aqui mais 7). Os null (20/11/2026) são os que só
        // têm o gravado: o arranque (8), a buzina (11), o motor pegando (12), a chave girando (15) e entrando (16), o pneu
        // furando (18), o ar vazando (20) e o vapor (21)
        static readonly Func<float[]>[] Receitas =
        {
            () => GerarMotor(7), null, () => GerarAbrir(9), () => GerarFechar(10), null,
            null, () => GerarSolenoide(13), () => GerarSoltar(14), null,
            null, () => GerarTossida(17), null, () => GerarMacaco(19),
            null, null,
            () => GerarAbastecer(22),
        };
        static Task<float[][]> _preparadas;

        /// <summary>Gera as amostras de todos os sons em outra thread (uma vez: elas ficam guardadas).</summary>
        public static void Preparar()
        {
            if (_preparadas != null) return;
            _preparadas = Task.Run(() =>
            {
                var todas = new float[Receitas.Length][];
                for (int i = 0; i < todas.Length; i++) todas[i] = Receitas[i]?.Invoke();
                return todas;
            });
        }

        // as amostras do som desta semente: as preparadas (esperando por elas, se ainda estão sendo geradas) ou geradas agora
        static float[] Dados(int semente) => _preparadas != null && !_preparadas.IsFaulted ? _preparadas.Result[semente - 7] : Receitas[semente - 7]();

        /// <summary>O motor em marcha lenta, em laço (1 s; com pitch 1 são 32 explosões por segundo).</summary>
        public static AudioClip Motor => _motor != null ? _motor : _motor = SomGerado.Clip("Motor do carro", Dados(7));
        // (20/11/2026: os que têm gravação devolvem só ela; os feitos em código que eram a reserva deles saíram)
        /// <summary>O motor de arranque girando o motor, em laço: toca enquanto a chave fica na partida (o gravado).</summary>
        public static AudioClip Arranque => Gravado("arranque");   // (20/11/2026: o gravado do sfxjar, dois giros em laço)
        /// <summary>O motor pegando e caindo para a marcha lenta (o gravado; o laço do motor entra em <see cref="LacoEntra"/>).</summary>
        public static AudioClip Pegar => Gravado("pegar");
        public static AudioClip Solenoide => _solenoide != null ? _solenoide : _solenoide = SomGerado.Clip("Solenoide do arranque", Dados(13));
        public static AudioClip SoltarArranque => _soltar != null ? _soltar : _soltar = SomGerado.Clip("Arranque soltando", Dados(14));
        public static AudioClip CliqueDaChave => Gravado("chave_gira");
        public static AudioClip ChaveEntra => Gravado("chave_entra");
        /// <summary>O motor tossindo (quase pegando): um acerto no mini-game da partida.</summary>
        public static AudioClip Tossida => _tossida != null ? _tossida : _tossida = SomGerado.Clip("Motor tossindo", Dados(17));
        static AudioClip _tossida;
        /// <summary>O pneu furado por um tiro: o estouro e o ar escapando (o gravado).</summary>
        public static AudioClip PneuFurando => Gravado("pneu_furando");
        /// <summary>O macaco sanfona girando (2,2 s, o tempoDoMacaco do carro): a catraca da manivela e o fuso rangendo.</summary>
        public static AudioClip Macaco => _macaco != null ? _macaco : _macaco = SomGerado.Clip("Macaco do carro", Dados(19));
        static AudioClip _macaco;
        public static AudioClip PortaAbrir => Gravado("porta_abrir");
        public static AudioClip PortaFechar => Gravado("porta_fechar");
        /// <summary>A buzina, em laço (o gravado).</summary>
        public static AudioClip Buzina => Gravado("buzina");
        /// <summary>A buzina de cada carro (20/11/2026): o cupê e a caminhonete com a outra (a de carro novo, do sfxjar).</summary>
        public static AudioClip BuzinaDe(Loot.Item chave) =>
            chave == Loot.Item.ChaveDoCupe || chave == Loot.Item.ChaveDaCaminhonete ? Gravado("outra_buzina") ?? Buzina
            : chave == Loot.Item.ChaveDaPerua ? Gravado("perua_buzina") ?? Buzina   // (20/11/2026: a buzina antiga, na perua)
            : Buzina;
        /// <summary>O ar escapando do pneu furado (o furo lento), em laço (o gravado).</summary>
        public static AudioClip ArVazando => Gravado("ar_vazando");
        /// <summary>O vapor do radiador vazando, em laço (o gravado).</summary>
        public static AudioClip Vapor => Gravado("vapor");
        /// <summary>A gasolina caindo do galão no tanque (2,2 s): o gorgolejo e o jorro. 01/11/2026, provisório.</summary>
        public static AudioClip Abastecer => _abastecer != null ? _abastecer : _abastecer = SomGerado.Clip("Abastecendo com o galão", Dados(22));
        static AudioClip _abastecer;

        // ------------------------------------------------------------------ os gravados (19/11/2026)
        // a segunda leva da repaginada: o Vehicle - Essentials, o vapor do Nature Sound FX e as batidas do Regular Impact
        // Sounds, em Resources/Carro (Combat/SonsGravados). Sem eles, null (20/11/2026: os feitos aqui que eram a reserva
        // deles saíram).
        static AudioClip Gravado(string nome) => SonsGravados.Um("Carro", nome);

        /// <summary>O motor desligando (só o gravado; null sem ele).</summary>
        public static AudioClip Desligar => Gravado("desligar");

        /// <summary>
        /// O capô e a tampa do porta-malas abrindo e fechando: o som feito aqui, o de antes (20/11/2026: o Pedro não gostou
        /// do porta-malas gravado e preferiu manter o antigo; as portas continuam com as gravadas).
        /// </summary>
        public static AudioClip CapoAbrir => _abrir != null ? _abrir : _abrir = SomGerado.Clip("Porta do carro abrindo", Dados(9));
        public static AudioClip CapoFechar => Gravado("capo_fechar") ?? (_fechar != null ? _fechar : _fechar = SomGerado.Clip("Porta do carro batendo", Dados(10)));   // (20/11/2026: o gravado; a reserva fica, a do porta-malas)

        /// <summary>A batida do carro pela mudança de velocidade (m/s): a leve (o metal) ou a forte (null sem os gravados).</summary>
        public static AudioClip Batida(float mudanca) => Gravado(mudanca >= 6f ? "batida_forte" : "batida_leve");

        /// <summary>
        /// Os laços gravados do motor de um conjunto, do lento ao rápido (20/11/2026: cada carro com o seu): 'a' a perua,
        /// 'b' o cupê, 'c' a caminhonete (a marcha lenta dela, da van, com os giros do cupê). Sem o conjunto, o da perua;
        /// sem nenhum, null.
        /// </summary>
        public static AudioClip[] LacosDoMotor(char conjunto)
        {
            var lacos = SonsGravados.Varios("Carro", "motor_" + conjunto + "_");
            if (lacos != null && lacos.Length == 1 && conjunto == 'c')
            {
                var b = SonsGravados.Varios("Carro", "motor_b_");
                if (b != null && b.Length >= 3) return new[] { lacos[0], b[1], b[2] };
            }
            if (lacos != null && lacos.Length >= 3) return lacos;
            return conjunto != 'a' ? LacosDoMotor('a') : null;
        }

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

        /// <summary>
        /// A gasolina do galão: o jorro (ruído numa faixa média, que entra e sai) e o gorgolejo do ar entrando no galão, um
        /// "glub" a cada 0,16 a 0,26 s (um tom grave que sobe e some, com um respingo).
        /// </summary>
        static float[] GerarAbastecer(int semente)
        {
            var r = new System.Random(semente);
            const float dura = 2.2f;
            int n = Mathf.RoundToInt(dura * Taxa);
            var d = new float[n];
            var jorro = new SomDePasso.Faixa(1300f, 0.9f);
            var respingo = new SomDePasso.Faixa(2600f, 1.5f);
            var lp = new SomDePasso.PassaBaixa(7000f);
            float proximo = 0.12f, ultimo = -1f, fase = 0f, tom = 200f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Taxa;
                float ruido = (float)(r.NextDouble() * 2 - 1);
                if (t >= proximo && t < dura - 0.3f)
                {
                    ultimo = t;
                    proximo = t + 0.16f + (float)r.NextDouble() * 0.1f;
                    tom = 150f + (float)r.NextDouble() * 120f;
                }
                float desde = ultimo < 0f ? 9f : t - ultimo;
                fase += 2f * Mathf.PI * tom * (1f + desde * 6f) / Taxa;
                float glub = Mathf.Sin(fase) * Mathf.Exp(-desde * 28f) * Mathf.Clamp01(desde / 0.004f);
                float envelope = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((dura - t) / 0.35f);
                float corrente = jorro.Passar(ruido) * (0.55f + 0.2f * Mathf.Sin(t * 23f)) * envelope;
                float gota = respingo.Passar(ruido) * Mathf.Exp(-desde * 40f) * 0.5f;
                d[i] = lp.Passar(corrente * 0.6f + (glub * 0.9f + gota) * envelope);
            }
            return Normalizar(d, 0.8f);
        }

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
