using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As estradas do chão do Lakewood (Plano_Lakewood, Etapa 5): a rede, o perfil e as malhas.
    ///  - <b>A rede:</b> as pontas que encostam numa outra estrada (até <see cref="PertoDoNo"/> m) vão para um ponto exato
    ///    dela (o T), as pontas juntas viram um nó só (a continuação, o Y), e onde duas estradas se cruzam nasce um nó nas
    ///    duas (o X). Os nós são <see cref="Nos"/>.
    ///  - <b>O perfil:</b> o chão (com os pátios da usina) suavizado ao longo da estrada, a rampa limitada (a média do corte
    ///    por cima com o aterro por baixo), as alturas presas da usina (a crista), as pontes (acima da água, a folga pelo
    ///    vão) e, por último, os nós: a estrada mais importante passa primeiro (a da usina, a rodovia, a secundária) e dá a
    ///    altura do nó; as outras chegam nela. Em volta de cada nó, todas ficam planas na altura dele (<see cref="RaioDoPatio"/>)
    ///    e voltam ao chão pela rampa delas: no cruzamento, os asfaltos ficam no mesmo plano.
    ///  - <b>As malhas</b> (<see cref="Pedaco"/> e <see cref="Patios"/>): o asfalto com as linhas (somem perto dos nós), as
    ///    saias das beiradas, o guard-rail onde o aterro é alto, a ponte com as muretas e os pilares, e o pátio redondo de
    ///    cada nó, cobrindo os cantos.
    /// </summary>
    public sealed partial class ChaoDeLakewood
    {
        /// <summary>Um nó da rede: o ponto, a altura (a da estrada mais importante dele), o raio do pátio e os pontos de cada estrada nele.</summary>
        public sealed class NoDaRede
        {
            public Vector2 Posicao;
            public float Altura = float.NaN;
            public float Raio;
            public readonly List<(int estrada, int ponto)> Pontos = new List<(int, int)>();
        }

        public readonly List<NoDaRede> Nos = new List<NoDaRede>();

        /// <summary>Em volta de um nó, as estradas ficam planas até aqui (m, ao longo delas).</summary>
        public const float RaioDoPatio = 9f;
        /// <summary>Uma ponta a menos disso de outra estrada (ou de outra ponta) vai para ela (m).</summary>
        public const float PertoDoNo = 14f;
        const float SemLinhas = 14f, SemGuardRail = 22f, Saia = 0.4f;

        // ------------------------------------------------------------------ a rede

        void MontarARede(List<ViaDoMapa> vias, int daUsina)
        {
            var validas = new List<ViaDoMapa>();
            var usina = new List<bool>();
            for (int i = 0; i < vias.Count; i++)
            {
                if (vias[i].pontos.Count < 2) continue;
                validas.Add(vias[i]);
                usina.Add(i < daUsina);
            }
            var linhas = new List<List<Vector2>>();
            foreach (var v in validas) linhas.Add(PontosDaVia(v));
            int n = linhas.Count;
            var nos = new List<Vector2>();
            int NoEm(Vector2 p)
            {
                for (int k = 0; k < nos.Count; k++) if ((nos[k] - p).sqrMagnitude < 0.25f) return k;
                nos.Add(p);
                return nos.Count - 1;
            }

            // 1) as pontas juntas: um nó só (uma ponta da usina manda no lugar; senão, a média)
            var pontas = new List<(int e, bool fim)>();
            for (int e = 0; e < n; e++) { pontas.Add((e, false)); pontas.Add((e, true)); }
            var usada = new bool[pontas.Count];
            Vector2 Ponta((int e, bool fim) p) => p.fim ? linhas[p.e][linhas[p.e].Count - 1] : linhas[p.e][0];
            void Mover((int e, bool fim) p, Vector2 q)
            {
                var l = linhas[p.e];
                if (p.fim) l[l.Count - 1] = q; else l[0] = q;
            }
            for (int a = 0; a < pontas.Count; a++)
            {
                if (usada[a]) continue;
                var grupo = new List<int> { a };
                for (int b = a + 1; b < pontas.Count; b++)
                    if (!usada[b] && pontas[b].e != pontas[a].e && (Ponta(pontas[b]) - Ponta(pontas[a])).sqrMagnitude < PertoDoNo * PertoDoNo)
                        grupo.Add(b);
                if (grupo.Count < 2) continue;
                Vector2 lugar = Vector2.zero;
                bool fixo = false;
                foreach (int g in grupo)
                    if (usina[pontas[g].e]) { lugar = Ponta(pontas[g]); fixo = true; }
                if (!fixo)
                {
                    foreach (int g in grupo) lugar += Ponta(pontas[g]);
                    lugar /= grupo.Count;
                }
                foreach (int g in grupo)
                {
                    usada[g] = true;
                    Mover(pontas[g], lugar);
                }
                NoEm(lugar);
            }

            // 2) as pontas que encostam no meio de outra estrada (o T): a ponta vai para o ponto mais perto dela, que entra nela
            var inserir = new List<(int e, int seg, float t, Vector2 p)>();
            for (int a = 0; a < pontas.Count; a++)
            {
                if (usada[a]) continue;
                var p = Ponta(pontas[a]);
                float melhor = PertoDoNo;
                int qual = -1, seg = -1;
                float tt = 0f;
                for (int e = 0; e < n; e++)
                {
                    if (e == pontas[a].e) continue;
                    var l = linhas[e];
                    for (int s = 1; s < l.Count; s++)
                    {
                        float d = Linhas.DistanciaDoSegmento(p, l[s - 1], l[s], out float t);
                        if (d < melhor) { melhor = d; qual = e; seg = s; tt = t; }
                    }
                }
                if (qual < 0) continue;
                var lq = linhas[qual];
                var q = Vector2.Lerp(lq[seg - 1], lq[seg], tt);
                // perto da ponta da outra, é a ponta dela (o nó das pontas, se houver)
                if ((q - lq[0]).sqrMagnitude < PertoDoNo * PertoDoNo) q = lq[0];
                else if ((q - lq[lq.Count - 1]).sqrMagnitude < PertoDoNo * PertoDoNo) q = lq[lq.Count - 1];
                else inserir.Add((qual, seg, tt, q));
                usada[a] = true;
                Mover(pontas[a], q);
                NoEm(q);
            }
            Inserir(linhas, inserir);

            // 3) os cruzamentos (o X): onde duas estradas se cortam longe das pontas e dos nós de antes (pelos segmentos de
            // cada quadrado de 64 m: comparar todos com todos levava quase 2 s)
            inserir.Clear();
            const float Quadrado = 64f;
            var quadrados = new Dictionary<long, List<(int e, int s)>>();
            for (int e = 0; e < n; e++)
            {
                var l = linhas[e];
                for (int s = 1; s < l.Count; s++)
                {
                    var mn = Vector2.Min(l[s - 1], l[s]);
                    var mx = Vector2.Max(l[s - 1], l[s]);
                    for (int qj = Mathf.FloorToInt(mn.y / Quadrado); qj <= Mathf.FloorToInt(mx.y / Quadrado); qj++)
                    for (int qi = Mathf.FloorToInt(mn.x / Quadrado); qi <= Mathf.FloorToInt(mx.x / Quadrado); qi++)
                    {
                        long k = Chave(qi, qj);
                        if (!quadrados.TryGetValue(k, out var lista)) quadrados[k] = lista = new List<(int, int)>();
                        lista.Add((e, s));
                    }
                }
            }
            var vistos = new HashSet<(int, int, int, int)>();
            foreach (var lista in quadrados.Values)
                for (int p = 0; p < lista.Count; p++)
                for (int q = p + 1; q < lista.Count; q++)
                {
                    var (a, i) = lista[p];
                    var (b, j) = lista[q];
                    if (a == b) continue;
                    if (a > b) { (a, b) = (b, a); (i, j) = (j, i); }
                    if (!vistos.Add((a, i, b, j))) continue;
                    var la = linhas[a];
                    var lb = linhas[b];
                    if (!Linhas.Cruzam(la[i - 1], la[i], lb[j - 1], lb[j], out var x, out float ta, out float tb)) continue;
                    bool pertoDeUmNo = false;
                    foreach (var no in nos) pertoDeUmNo |= (no - x).sqrMagnitude < 30f * 30f;
                    if (pertoDeUmNo) continue;
                    inserir.Add((a, i, ta, x));
                    inserir.Add((b, j, tb, x));
                    NoEm(x);
                }
            Inserir(linhas, inserir);

            // as estradas, com os pontos de cada nó
            Estradas.Clear();
            Nos.Clear();
            foreach (var p in nos) Nos.Add(new NoDaRede { Posicao = p });
            for (int e = 0; e < n; e++)
            {
                var l = linhas[e];
                var est = new Estrada { Via = validas[e], Pontos = l.ToArray(), MeiaLargura = MeiaLarguraDe(validas[e]), DaUsina = usina[e] };
                est.AoLongo = new float[l.Count];
                for (int i = 1; i < l.Count; i++) est.AoLongo[i] = est.AoLongo[i - 1] + Vector2.Distance(l[i - 1], l[i]);
                Estradas.Add(est);
                for (int k = 0; k < Nos.Count; k++)
                    for (int i = 0; i < l.Count; i++)
                        if ((l[i] - Nos[k].Posicao).sqrMagnitude < 0.01f) { Nos[k].Pontos.Add((e, i)); break; }
            }
            // os nós de uma estrada só (o desenho tinha uma ponta solta perto de outra ponta da mesma) não contam
            Nos.RemoveAll(no => no.Pontos.Count < 2);
            foreach (var no in Nos)
            {
                float raio = 0f;
                foreach (var (e, _) in no.Pontos) raio = Mathf.Max(raio, Estradas[e].MeiaLargura);
                no.Raio = raio + 1.5f;
            }

            // as alturas: a da usina primeiro, depois as rodovias e as secundárias; quem passa primeiro dá a altura dos nós
            var ordem = new List<int>();
            for (int e = 0; e < n; e++) ordem.Add(e);
            int Peso(int e) => Estradas[e].DaUsina ? 0 : Estradas[e].Via.tipo == TipoDeVia.Rodovia ? 1 : Estradas[e].Via.tipo == TipoDeVia.Secundaria ? 2 : 3;
            ordem.Sort((x, y) => Peso(x) != Peso(y) ? Peso(x).CompareTo(Peso(y)) : x.CompareTo(y));
            foreach (int e in ordem)
            {
                var est = Estradas[e];
                Perfil(est, PinosDe(e));
                bool novos = false;
                foreach (var no in Nos)
                    foreach (var (ee, i) in no.Pontos)
                        if (ee == e && float.IsNaN(no.Altura))
                        {
                            no.Altura = est.Alturas[i];
                            novos = true;
                        }
                if (novos) Perfil(est, PinosDe(e));
            }
            // os metros até o nó mais perto
            foreach (var est in Estradas)
            {
                est.AteONo = new float[est.Pontos.Length];
                for (int i = 0; i < est.AteONo.Length; i++) est.AteONo[i] = float.MaxValue;
            }
            foreach (var no in Nos)
                foreach (var (e, i) in no.Pontos)
                {
                    var est = Estradas[e];
                    for (int k = 0; k < est.Pontos.Length; k++)
                        est.AteONo[k] = Mathf.Min(est.AteONo[k], Mathf.Abs(est.AoLongo[k] - est.AoLongo[i]));
                }
        }

        List<(int i, float h)> PinosDe(int e)
        {
            var l = new List<(int, float)>();
            foreach (var no in Nos)
                foreach (var (ee, i) in no.Pontos)
                    if (ee == e && !float.IsNaN(no.Altura)) l.Add((i, no.Altura));
            return l;
        }

        // põe os pontos pedidos nas linhas (de trás para a frente, para os índices não mudarem)
        static void Inserir(List<List<Vector2>> linhas, List<(int e, int seg, float t, Vector2 p)> pedidos)
        {
            pedidos.Sort((x, y) => x.e != y.e ? x.e.CompareTo(y.e) : x.seg != y.seg ? y.seg.CompareTo(x.seg) : y.t.CompareTo(x.t));
            foreach (var (e, seg, t, p) in pedidos)
            {
                var l = linhas[e];
                if ((l[seg - 1] - p).sqrMagnitude < 0.01f || (l[seg] - p).sqrMagnitude < 0.01f)
                {
                    // já é um ponto da linha: só acerta
                    if ((l[seg - 1] - p).sqrMagnitude < 0.01f) l[seg - 1] = p; else l[seg] = p;
                    continue;
                }
                l.Insert(seg, p);
            }
        }

        // os pontos da via a cada 8 m ao longo da linha arredondada
        static List<Vector2> PontosDaVia(ViaDoMapa via)
        {
            // as ruas das cidades são retas entre os pontos (os lotes foram postos na linha sem arredondar)
            var linha = via.tipo == TipoDeVia.Rua ? new List<Vector2>(via.pontos) : MapaDeLakewood.Linha(via.pontos);
            var pts = new List<Vector2>();
            for (int s = 1; s < linha.Count; s++)
            {
                var a = linha[s - 1];
                var b = linha[s];
                int partes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 8f));
                for (int q = 0; q < partes; q++) pts.Add(Vector2.Lerp(a, b, q / (float)partes));
            }
            pts.Add(linha[linha.Count - 1]);
            // sem pontos repetidos (os cantos do Chaikin bem perto)
            for (int i = pts.Count - 2; i >= 1; i--)
                if ((pts[i] - pts[i + 1]).sqrMagnitude < 1f) pts.RemoveAt(i);
            return pts;
        }

        // ------------------------------------------------------------------ o perfil

        /// <summary>A rampa da via (a do desenho; senão, 6% na rodovia e 9% nas outras).</summary>
        public static float RampaDe(ViaDoMapa via) => via.rampaMaxima > 0f ? via.rampaMaxima : via.tipo == TipoDeVia.Rodovia ? 0.06f : 0.09f;

        void Perfil(Estrada est, List<(int i, float h)> pinos)
        {
            var via = est.Via;
            var pts = est.Pontos;
            var s = est.AoLongo;
            int n = pts.Length;
            var chao = new float[n];
            var agua = new float[n];
            var ponte = new bool[n];
            for (int i = 0; i < n; i++)
            {
                chao[i] = Usina.Chao(pts[i].x, pts[i].y, Relevo.Altura(pts[i].x, pts[i].y));
                ponte[i] = AguaDaPonte(pts[i].x, pts[i].y, est.MeiaLargura, out agua[i]);
            }
            // o chão suavizado ao longo da estrada (uns 100 m para cada lado)
            var h = (float[])chao.Clone();
            for (int passada = 0; passada < 3; passada++)
            {
                var novo = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float soma = 0f, peso = 0f;
                    for (int k = -12; k <= 12; k++)
                    {
                        int j = Mathf.Clamp(i + k, 0, n - 1);
                        float w = 1f - Mathf.Abs(k) / 13f;
                        soma += h[j] * w;
                        peso += w;
                    }
                    novo[i] = soma / peso;
                }
                h = novo;
            }
            // a rampa limitada: a média do perfil cortado por cima com o aterrado por baixo
            float rampa = RampaDe(via);
            var cima = (float[])h.Clone();
            var baixo = (float[])h.Clone();
            for (int i = 1; i < n; i++)
            {
                float ds = (s[i] - s[i - 1]) * rampa;
                cima[i] = Mathf.Min(cima[i], cima[i - 1] + ds);
                baixo[i] = Mathf.Max(baixo[i], baixo[i - 1] - ds);
            }
            for (int i = n - 2; i >= 0; i--)
            {
                float ds = (s[i + 1] - s[i]) * rampa;
                cima[i] = Mathf.Min(cima[i], cima[i + 1] + ds);
                baixo[i] = Mathf.Max(baixo[i], baixo[i + 1] - ds);
            }
            for (int i = 0; i < n; i++) h[i] = (cima[i] + baixo[i]) * 0.5f;
            // a altura presa nas pontas (a crista da barragem), e a rampa limitada a partir dela
            if (via.alturaNoComeco > 0f)
            {
                h[0] = via.alturaNoComeco;
                for (int i = 1; i < n; i++)
                {
                    float ds = (s[i] - s[i - 1]) * rampa;
                    h[i] = Mathf.Clamp(h[i], h[i - 1] - ds, via.soDesce ? h[i - 1] : h[i - 1] + ds);
                }
            }
            if (via.alturaNoFim > 0f)
            {
                h[n - 1] = via.alturaNoFim;
                for (int i = n - 2; i >= 0; i--)
                {
                    float ds = (s[i + 1] - s[i]) * rampa;
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - ds, h[i + 1] + ds);
                }
            }
            // por cima da água, a ponte: a folga pelo vão (o riacho de 15 m, uns 2,6 m; o lago e o rio largo, 6 m), com
            // as rampas de 5% (ou a da estrada, se menor)
            var minimo = new float[n];
            for (int i = 0; i < n; i++) minimo[i] = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (!ponte[i]) continue;
                int j = i;
                while (j + 1 < n && ponte[j + 1]) j++;
                float vao = s[j] - s[i];
                float folga = Mathf.Clamp(2f + 0.04f * vao, 2.5f, FolgaDaPonte);
                for (int k = i; k <= j; k++) minimo[k] = agua[k] + folga;
                i = j;
            }
            float rampaDaPonte = Mathf.Min(0.05f, rampa);
            for (int i = 1; i < n; i++) minimo[i] = Mathf.Max(minimo[i], minimo[i - 1] - rampaDaPonte * (s[i] - s[i - 1]));
            for (int i = n - 2; i >= 0; i--) minimo[i] = Mathf.Max(minimo[i], minimo[i + 1] - rampaDaPonte * (s[i + 1] - s[i]));
            for (int i = 0; i < n; i++) h[i] = Mathf.Max(h[i], minimo[i]);
            // os nós: plano em volta de cada um, na altura dele, e a rampa da estrada a partir dali (o envelope dos pinos)
            if (pinos.Count > 0)
                for (int i = 0; i < n; i++)
                {
                    float teto = float.MaxValue, piso = float.MinValue;
                    foreach (var (k, alt) in pinos)
                    {
                        float d = Mathf.Max(0f, Mathf.Abs(s[i] - s[k]) - RaioDoPatio);
                        teto = Mathf.Min(teto, alt + rampa * d);
                        piso = Mathf.Max(piso, alt - rampa * d);
                    }
                    h[i] = teto < piso ? (teto + piso) * 0.5f : Mathf.Clamp(h[i], piso, teto);
                }
            for (int i = 0; i < n; i++) ponte[i] |= h[i] - chao[i] > 7f;
            est.Alturas = h;
            est.Ponte = ponte;
        }

        // a água embaixo do ponto (a ponte): o lago só no ponto (a beira do lago não levanta a estrada), o rio com a folga
        // da meia largura (a ponte passa um pouco do barranco)
        bool AguaDaPonte(float x, float z, float meia, out float nivel)
        {
            if (Usina != null && Usina.Agua(x, z, out nivel, 4f)) return true;
            if (AguaDoLagoEm(x, z, out nivel) && Relevo.Altura(x, z) < nivel + 0.5f) return true;
            if (RioMaisPerto(x, z, meia + 4f, out var rio))
            {
                nivel = rio.Nivel;
                return true;
            }
            nivel = 0f;
            return false;
        }

        /// <summary>A estrada mais perto (até o alcance): a distância, a altura do asfalto ali e se é ponte.</summary>
        public bool EstradaPerto(float x, float z, float alcance, out float dist, out float altura, out bool ponte, out float meia)
        {
            dist = float.MaxValue;
            altura = 0f;
            ponte = false;
            meia = 0f;
            var l = Celula(_estradas, x, z);
            if (l == null) return false;
            var p = new Vector2(x, z);
            foreach (var (e, s) in l)
            {
                var est = Estradas[e];
                float dd = Linhas.DistanciaDoSegmento(p, est.Pontos[s - 1], est.Pontos[s], out float t);
                if (dd - est.MeiaLargura >= dist - meia) continue;
                dist = dd;
                meia = est.MeiaLargura;
                altura = Mathf.Lerp(est.Alturas[s - 1], est.Alturas[s], t);
                ponte = t < 0.5f ? est.Ponte[s - 1] : est.Ponte[s];
            }
            return dist < meia + alcance;
        }

        /// <summary>
        /// O asfalto mais baixo que cobre o ponto (a pista e o acostamento de todas as estradas, mais a folga, fora das
        /// pontes). Perto de um nó, a estrada que sobe até ele passa por dentro do acostamento da outra, e o chão da mais
        /// perto (a mais alta) cobria a pista dela. Sem folga, cada estrada conta pelo trecho dela mais perto do ponto; com
        /// folga (o canto de um triângulo dos blocos longe), qualquer trecho ao alcance conta, também o da mesma estrada mais
        /// abaixo na rampa: o triângulo que passa por cima da pista numa curva em rampa pegava o canto mais alto.
        /// </summary>
        public bool PisoMaisBaixo(float x, float z, float folga, out float altura)
        {
            EstradaEPiso(x, z, 0f, folga, out _, out _, out _, out _, out altura);
            return altura < float.MaxValue;
        }

        // a estrada mais perto (a do EstradaPerto) e o asfalto mais baixo ao alcance (o do PisoMaisBaixo, float.MaxValue
        // sem nenhum), numa passada só pelos trechos da célula (perto da represa, uns 150: a consulta mais cara do chão)
        bool EstradaEPiso(float x, float z, float alcance, float folga, out float dist, out float altura, out bool ponte, out float meia, out float piso)
        {
            dist = float.MaxValue;
            altura = 0f;
            ponte = false;
            meia = 0f;
            piso = float.MaxValue;
            var l = Celula(_estradas, x, z);
            if (l == null) return false;
            var p = new Vector2(x, z);
            // sem folga, cada estrada pelo trecho dela mais perto do ponto (os trechos vizinhos da mesma estrada, numa rampa,
            // ficam mais baixos ou mais altos e não contam)
            Span<int> quais = stackalloc int[8];
            Span<float> distDe = stackalloc float[8], altDe = stackalloc float[8];
            Span<bool> ponteDe = stackalloc bool[8];
            int n = 0;
            foreach (var (e, s) in l)
            {
                var est = Estradas[e];
                float dd = Linhas.DistanciaDoSegmento(p, est.Pontos[s - 1], est.Pontos[s], out float t);
                if (dd - est.MeiaLargura < dist - meia)
                {
                    dist = dd;
                    meia = est.MeiaLargura;
                    altura = Mathf.Lerp(est.Alturas[s - 1], est.Alturas[s], t);
                    ponte = t < 0.5f ? est.Ponte[s - 1] : est.Ponte[s];
                }
                if (dd > est.MeiaLargura + Acostamento + folga) continue;
                bool ehPonte = t < 0.5f ? est.Ponte[s - 1] : est.Ponte[s];
                if (folga > 0f)
                {
                    if (!ehPonte) piso = Mathf.Min(piso, Mathf.Lerp(est.Alturas[s - 1], est.Alturas[s], t));
                    continue;
                }
                int k = 0;
                while (k < n && quais[k] != e) k++;
                if (k == n)
                {
                    if (n == quais.Length) continue;
                    quais[n] = e;
                    distDe[n] = float.MaxValue;
                    n++;
                }
                if (dd >= distDe[k]) continue;
                distDe[k] = dd;
                altDe[k] = Mathf.Lerp(est.Alturas[s - 1], est.Alturas[s], t);
                ponteDe[k] = ehPonte;
            }
            for (int k = 0; k < n; k++)
                if (!ponteDe[k]) piso = Mathf.Min(piso, altDe[k]);
            return dist < meia + alcance;
        }

        // ------------------------------------------------------------------ as malhas

        /// <summary>
        /// Um pedaço (de "ini" a "fim", nos pontos) de uma estrada: o asfalto com as linhas brancas da beira e a amarela do
        /// meio (dupla na rodovia, tracejada na secundária; perto dos nós, nenhuma), as saias das beiradas, o guard-rail onde
        /// o aterro passa de 2,5 m e, onde é ponte, o tabuleiro grosso, as muretas e os pilares até o chão.
        /// </summary>
        public MalhaDeObra Pedaco(int estrada, int ini, int fim, MalhaDeObra colisao = null)
        {
            var e = Estradas[estrada];
            var m = new MalhaDeObra();
            bool rodovia = e.Via.tipo == TipoDeVia.Rodovia;
            Vector2 Lado(int i)
            {
                var dir = (e.Pontos[Mathf.Min(i + 1, e.Pontos.Length - 1)] - e.Pontos[Mathf.Max(i - 1, 0)]).normalized;
                return new Vector2(-dir.y, dir.x);
            }
            Vector3 P(int i, float off, float y)
            {
                var p = e.Pontos[i] + Lado(i) * off;
                return new Vector3(p.x, e.Alturas[i] + y, p.y);
            }
            void Faixa(int i, float o0, float o1, float y, string cor, float de = 0f, float ate = 1f)
            {
                var a0 = P(i, o0, y);
                var a1 = P(i, o1, y);
                var b0 = P(i + 1, o0, y);
                var b1 = P(i + 1, o1, y);
                m.Face(new[] { Vector3.Lerp(a0, b0, de), Vector3.Lerp(a1, b1, de), Vector3.Lerp(a1, b1, ate), Vector3.Lerp(a0, b0, ate) }, Vector3.up, cor);
            }
            float meia = e.MeiaLargura;
            bool terra = e.Via.tipo == TipoDeVia.Terra;
            for (int i = ini; i < fim && i + 1 < e.Pontos.Length; i++)
            {
                bool naPonte = e.Ponte[i] || e.Ponte[i + 1];
                Faixa(i, -meia, meia, 0.05f, terra ? "Terra" : "Asfalto");
                // a colisão: só o asfalto (as linhas e as saias ficam no desenho)
                colisao?.Face(new[] { P(i, -meia, 0.05f), P(i, meia, 0.05f), P(i + 1, meia, 0.05f), P(i + 1, -meia, 0.05f) }, Vector3.up, "Asfalto");
                bool linhas = !terra && e.AteONo[i] > SemLinhas && e.AteONo[i + 1] > SemLinhas;
                if (linhas)
                {
                    // as ruas das cidades: só a amarela do meio, tracejada
                    float beira = rodovia ? meia - 1.6f : meia - 0.35f;
                    if (e.Via.tipo != TipoDeVia.Rua)
                    {
                        Faixa(i, -beira - 0.14f, -beira, 0.06f, "ListraBranca");
                        Faixa(i, beira, beira + 0.14f, 0.06f, "ListraBranca");
                    }
                    if (rodovia)
                    {
                        Faixa(i, -0.2f, -0.08f, 0.06f, "Faixa");
                        Faixa(i, 0.08f, 0.2f, 0.06f, "Faixa");
                    }
                    else
                    {
                        // tracejada: 3 m pintados a cada 12 m (pelos metros desde o começo)
                        float s0 = e.AoLongo[i], s1 = e.AoLongo[i + 1], l = Mathf.Max(s1 - s0, 1e-3f);
                        for (float c = Mathf.Floor(s0 / 12f) * 12f; c < s1; c += 12f)
                        {
                            float de = Mathf.Clamp01((c - s0) / l), ate = Mathf.Clamp01((c + 3f - s0) / l);
                            if (ate > de) Faixa(i, -0.07f, 0.07f, 0.06f, "Faixa", de, ate);
                        }
                    }
                }
                if (!naPonte)
                {
                    // as saias das beiradas (o asfalto fica 13 cm acima do acostamento)
                    foreach (float sl in new[] { -1f, 1f })
                    {
                        var a = P(i, sl * meia, 0.05f);
                        var b = P(i + 1, sl * meia, 0.05f);
                        var fora = new Vector3(Lado(i).x * sl, 0f, Lado(i).y * sl);
                        m.Face(new[] { a, b, b + Vector3.down * Saia, a + Vector3.down * Saia }, fora, terra ? "Terra" : "AsfaltoRemendo");
                    }
                    // o guard-rail: onde o chão de verdade, logo além do acostamento, fica bem abaixo do asfalto
                    if (!terra && e.AteONo[i] > SemGuardRail && e.AteONo[i + 1] > SemGuardRail)
                        foreach (float sl in new[] { -1f, 1f })
                        {
                            var fora0 = e.Pontos[i] + Lado(i) * sl * (meia + Acostamento + 2f);
                            var fora1 = e.Pontos[i + 1] + Lado(i + 1) * sl * (meia + Acostamento + 2f);
                            if (e.Alturas[i] - Relevo.Altura(fora0.x, fora0.y) < 2.5f || e.Alturas[i + 1] - Relevo.Altura(fora1.x, fora1.y) < 2.5f) continue;
                            GuardRail(m, colisao, P(i, sl * (meia + 0.6f), 0f), P(i + 1, sl * (meia + 0.6f), 0f), new Vector3(Lado(i).x * sl, 0f, Lado(i).y * sl));
                        }
                    continue;
                }
                foreach (float sl in new[] { -1f, 1f })
                {
                    var lado = new[] { P(i, sl * (meia + 0.6f), 0.05f), P(i + 1, sl * (meia + 0.6f), 0.05f), P(i + 1, sl * (meia + 0.6f), -1.2f), P(i, sl * (meia + 0.6f), -1.2f) };
                    m.Face(lado, new Vector3(Lado(i).x * sl, 0f, Lado(i).y * sl), "Concreto");
                    // a faixa de concreto entre o asfalto e a mureta
                    m.Face(new[] { P(i, sl * meia, 0.05f), P(i, sl * (meia + 0.6f), 0.05f), P(i + 1, sl * (meia + 0.6f), 0.05f), P(i + 1, sl * meia, 0.05f) }, Vector3.up, "Concreto");
                    var a = P(i, sl * (meia + 0.35f), 0.5f);
                    var b = P(i + 1, sl * (meia + 0.35f), 0.5f);
                    var dir = b - a;
                    var mureta = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward), Vector3.one);
                    m.Caixa(mureta, new Vector3(0.4f, 0.9f, dir.magnitude + 0.05f), "Concreto");
                    colisao?.Caixa(mureta, new Vector3(0.4f, 0.9f, dir.magnitude + 0.05f), "Concreto");
                }
                var fundo = new[] { P(i, -meia - 0.6f, -1.2f), P(i + 1, -meia - 0.6f, -1.2f), P(i + 1, meia + 0.6f, -1.2f), P(i, meia + 0.6f, -1.2f) };
                m.Face(fundo, Vector3.down, "Concreto");
                if (i % 3 == 0)
                {
                    var p = e.Pontos[i];
                    float chao = Relevo.Altura(p.x, p.y) - 2f;
                    if (AguaDoLagoEm(p.x, p.y, out float nivelDoLago)) chao = Mathf.Min(chao, nivelDoLago - 2f);
                    float topo = e.Alturas[i] - 1.2f;
                    if (topo - chao > 1f)
                    {
                        var dir = e.Pontos[Mathf.Min(i + 1, e.Pontos.Length - 1)] - e.Pontos[Mathf.Max(i - 1, 0)];
                        float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        var pilar = Matrix4x4.TRS(new Vector3(p.x, (topo + chao) * 0.5f, p.y), Quaternion.Euler(0f, giro, 0f), Vector3.one);
                        m.Caixa(pilar, new Vector3(meia * 1.6f, topo - chao, 1.2f), "Concreto");
                        colisao?.Caixa(pilar, new Vector3(meia * 1.6f, topo - chao, 1.2f), "Concreto");
                    }
                }
            }
            return m;
        }

        // um trecho de guard-rail de a até b (no chão do asfalto), "fora" para o lado de fora: a lâmina e um poste (na
        // colisão, a lâmina mais alta e grossa)
        static void GuardRail(MalhaDeObra m, MalhaDeObra colisao, Vector3 a, Vector3 b, Vector3 fora)
        {
            var dir = b - a;
            float comprimento = dir.magnitude;
            if (comprimento < 0.1f) return;
            var giro = Quaternion.LookRotation(dir / comprimento, Vector3.up);
            var meio = (a + b) * 0.5f;
            m.Caixa(Matrix4x4.TRS(meio + Vector3.up * 0.62f, giro, Vector3.one), new Vector3(0.06f, 0.32f, comprimento + 0.08f), "Cromo");
            m.Caixa(Matrix4x4.TRS(a + fora * 0.08f + Vector3.up * 0.4f, Quaternion.LookRotation(fora, Vector3.up), Vector3.one), new Vector3(0.1f, 0.8f, 0.1f), "Metal");
            colisao?.Caixa(Matrix4x4.TRS(meio + Vector3.up * 0.55f, giro, Vector3.one), new Vector3(0.25f, 1.1f, comprimento + 0.1f), "Metal");
        }

        /// <summary>
        /// Os pátios dos nós (os cruzamentos e as emendas): um disco de asfalto na altura do nó, do raio da estrada mais larga
        /// dele mais 1,5 m, cobrindo os cantos onde as estradas se encontram; com a saia em volta.
        /// </summary>
        public MalhaDeObra Patios()
        {
            var m = new MalhaDeObra();
            const int lados = 20;
            var disco = new Vector3[lados];
            foreach (var no in Nos)
            {
                if (float.IsNaN(no.Altura)) continue;
                float y = no.Altura + 0.05f;
                for (int k = 0; k < lados; k++)
                {
                    float a = -k * Mathf.PI * 2f / lados;
                    disco[k] = new Vector3(no.Posicao.x + Mathf.Cos(a) * no.Raio, y, no.Posicao.y + Mathf.Sin(a) * no.Raio);
                }
                m.Face(disco, Vector3.up, "Asfalto");
                for (int k = 0; k < lados; k++)
                {
                    var a = disco[k];
                    var b = disco[(k + 1) % lados];
                    var fora = ((a + b) * 0.5f - new Vector3(no.Posicao.x, y, no.Posicao.y)).normalized;
                    m.Face(new[] { b, a, a + Vector3.down * Saia, b + Vector3.down * Saia }, fora, "AsfaltoRemendo");
                }
            }
            return m;
        }
    }
}
