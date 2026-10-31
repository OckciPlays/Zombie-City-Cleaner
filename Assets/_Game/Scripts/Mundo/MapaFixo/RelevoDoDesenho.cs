using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O relevo grosso do desenho (Plano_Mapa_Fixo, Parte F1), para pintar o mapa e, na F2, para começar o relevo do jogo:
    /// uma grade de alturas (20 m entre os pontos) dos 15 km, feita em ordem: as colinas do ruído, as planícies achatadas,
    /// as serras (a cordilheira e os morros das bordas), os picos, as passagens (os desfiladeiros das estradas), as
    /// montanhas da grade (as da imagem de referência), o brejo, as bacias dos lagos com a margem em rampa, as ilhas e, por
    /// último, os rios (a água de cada um, que pode sair do chão, e o vale, o leito e a margem em volta dela). O relevo de verdade (o de 2 m, com as
    /// estradas e as cidades) é da F3. Código puro: pode rodar em outra thread.
    /// </summary>
    public sealed class RelevoDoDesenho
    {
        public readonly float Passo;
        /// <summary>Os pontos de cada lado da grade (de -Metade a +Metade, as duas pontas inclusas).</summary>
        public readonly int N;
        public readonly float[] Alturas;

        readonly float[] _dist, _frac;
        readonly bool[] _dentro;
        // a grade das montanhas amaciada, em cada ponto (null sem a grade): o vale do rio abre mais na serra
        float[] _montanha;
        // os pontos debaixo da água de um lago (fora das ilhas): a margem do rio não sobe ali (fazia um dique no lago); e a
        // água do lago em cada ponto do contorno dele (com as ilhas; NaN fora): o vale de um rio não leva a ilha para
        // baixo dela
        bool[] _noLago;
        float[] _aguaDoLago;

        // a média numa caixa de (2 raio + 1) pontos, em x e depois em z (a borda repete o último ponto)
        void Media(float[] g, int raio)
        {
            var tmp = new float[N * N];
            Parallel.For(0, N, j =>
            {
                float soma = 0f;
                for (int i = -raio; i <= raio; i++) soma += g[j * N + Mathf.Clamp(i, 0, N - 1)];
                for (int i = 0; i < N; i++)
                {
                    tmp[j * N + i] = soma / (2 * raio + 1);
                    soma += g[j * N + Mathf.Min(N - 1, i + raio + 1)] - g[j * N + Mathf.Max(0, i - raio)];
                }
            });
            Parallel.For(0, N, i =>
            {
                float soma = 0f;
                for (int j = -raio; j <= raio; j++) soma += tmp[Mathf.Clamp(j, 0, N - 1) * N + i];
                for (int j = 0; j < N; j++)
                {
                    g[j * N + i] = soma / (2 * raio + 1);
                    soma += tmp[Mathf.Min(N - 1, j + raio + 1) * N + i] - tmp[Mathf.Max(0, j - raio) * N + i];
                }
            });
        }

        public RelevoDoDesenho(DesenhoDoMapa d, float passo = 20f)
        {
            Passo = passo;
            N = Mathf.RoundToInt(DesenhoDoMapa.Lado / passo) + 1;
            Alturas = new float[N * N];
            _dist = new float[N * N];
            _frac = new float[N * N];
            _dentro = new bool[N * N];
            var r = d.relevo;
            var colinas = new Ruido(d.semente, "relevo-do-desenho");
            var cristas = new Ruido(d.semente, "cristas-do-desenho");

            // as colinas
            Parallel.For(0, N, j =>
            {
                for (int i = 0; i < N; i++)
                {
                    float x = X(i), z = X(j);
                    Alturas[j * N + i] = r.baseDoVale + r.amplitude * colinas.Fbm(x / r.escala, z / r.escala, 4);
                }
            });

            // as planícies: o relevo vale o fator dele, com 600 m de passagem da beira para dentro
            foreach (var p in r.planicies)
            {
                if (p.contorno.Count < 3) continue;
                var c = DesenhoDoMapa.Contorno(p.contorno);
                Marcar(c);
                Distancias(c, true, 600f, false);
                Aplicar((k, x, z) =>
                {
                    if (!_dentro[k]) return;
                    float w = _dist[k] == float.MaxValue ? 1f : Geo.Suave(0f, 600f, _dist[k]);
                    Alturas[k] = r.baseDoVale + (Alturas[k] - r.baseDoVale) * Mathf.Lerp(1f, p.fator, w);
                });
            }

            // as serras
            foreach (var s in r.serras)
            {
                if (s.crista.Count < 2) continue;
                Distancias(DesenhoDoMapa.Linha(s.crista), false, s.largura * 2.4f, false);
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    if (dd == float.MaxValue) return;
                    float perfil = Mathf.Exp(-(dd / s.largura) * (dd / s.largura));
                    Alturas[k] += perfil * (s.altura + s.aspereza * Cristas(cristas, x / 1700f, z / 1700f));
                });
            }

            // os picos
            foreach (var p in r.picos)
            {
                float alcance = p.raio * 2.6f;
                Caixa(p.ponto, alcance, (k, x, z) =>
                {
                    float dx = x - p.ponto.x, dz = z - p.ponto.y;
                    Alturas[k] += p.subida * Mathf.Exp(-(dx * dx + dz * dz) / (p.raio * p.raio));
                });
            }

            // as passagens: perto da linha, o relevo desce até a altura dela (o desfiladeiro da estrada na serra)
            foreach (var ps in r.passagens)
            {
                if (ps.pontos.Count < 2) continue;
                Distancias(DesenhoDoMapa.Linha(ps.pontos), false, ps.largura * 2.5f, false);
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    if (dd == float.MaxValue || Alturas[k] <= ps.altura) return;
                    float perfil = Mathf.Exp(-(dd / ps.largura) * (dd / ps.largura));
                    Alturas[k] = Mathf.Lerp(Alturas[k], ps.altura, perfil);
                });
            }

            // as montanhas da grade (as da imagem de referência): a altura pela grade e as cristas por cima
            if (d.TemGrade && r.alturaDasMontanhas > 0f)
            {
                // a grade amaciada (a borda da mancha da imagem virava um paredão): duas passadas de média no raio
                var grade = new float[N * N];
                Parallel.For(0, N, j =>
                {
                    for (int i = 0; i < N; i++) grade[j * N + i] = d.Montanha(X(i), X(j));
                });
                int raio = Mathf.RoundToInt(r.suavidadeDasMontanhas / passo);
                for (int passada = 0; passada < 2 && raio > 0; passada++) Media(grade, raio);
                // o vale de passagem das estradas principais: na serra, a montanha baixa em volta delas (a 120 m, a um
                // oitavo; a 900 m, inteira), como as rodovias de verdade, que vão pelos vales (por cima, a I-90 subia a 800 m)
                foreach (var via in d.estradas)
                {
                    if (via.pontos.Count < 2 || (via.tipo != TipoDeVia.Interestadual && via.tipo != TipoDeVia.Estadual)) continue;
                    Distancias(DesenhoDoMapa.Linha(via.pontos), false, 900f, false);
                    Aplicar((k, x, z) =>
                    {
                        if (_dist[k] != float.MaxValue) grade[k] *= Mathf.Lerp(0.12f, 1f, Geo.Suave(120f, 900f, _dist[k]));
                    });
                }
                _montanha = grade;
                Aplicar((k, x, z) =>
                {
                    float m = grade[k];
                    if (m <= 0f) return;
                    Alturas[k] += r.alturaDasMontanhas * Mathf.Pow(m, 1.15f) + r.asperezaDasMontanhas * m * Cristas(cristas, x / 1500f, z / 1500f);
                });
            }

            // o brejo: o chão na altura da água, em manchas (umas alagadas, outras não)
            NivelDoBrejo = float.NaN;
            if (d.TemGrade)
            {
                var chao = new List<float>();
                for (int j = 0; j < N; j += 4)
                for (int i = 0; i < N; i += 4)
                    if (d.Fracao(Cobertura.Brejo, X(i), X(j)) + d.Fracao(Cobertura.AguaDoBrejo, X(i), X(j)) > 0.75f) chao.Add(Alturas[j * N + i]);
                if (chao.Count > 20)
                {
                    float nivel = NivelDoBrejo = Percentil(chao, 0.15f) - 0.5f;
                    Aplicar((k, x, z) =>
                    {
                        // as manchas de água são as da imagem (a água do brejo); a terra fica logo acima da água
                        float agua = d.Fracao(Cobertura.AguaDoBrejo, x, z), w = d.Fracao(Cobertura.Brejo, x, z) + agua;
                        if (w <= 0f) return;
                        float alvo = nivel + 0.7f - 2.1f * agua + 0.35f * colinas.Fbm(x / 160f + 40f, z / 160f, 2);
                        Alturas[k] = Mathf.Lerp(Alturas[k], alvo, Geo.Suave(0f, 1f, w));
                    });
                }
            }

            // os lagos: a água (a do desenho, ou a do chão em volta: o mais baixo da margem, tirando os 10% mais fundos),
            // a bacia funda no meio, a margem em rampa até o relevo e, onde o chão de fora é mais baixo que a água, a beira
            // levantada (senão a água ficaria em pé na borda)
            NivelDosLagos = new float[d.lagos.Count];
            for (int l = 0; l < d.lagos.Count; l++)
            {
                var lago = d.lagos[l];
                NivelDosLagos[l] = lago.nivel;
                if (lago.contorno.Count < 3) continue;
                var c = DesenhoDoMapa.Contorno(lago.contorno);
                float nivel = lago.nivel;
                if (lago.nivelPeloRelevo)
                {
                    var margem = new List<float>();
                    foreach (var p in Reamostrar(c, 30f, true)) margem.Add(Altura(p.x, p.y));
                    nivel = Percentil(margem, 0.1f) - 1f;
                }
                NivelDosLagos[l] = nivel;
                Marcar(c);
                Distancias(c, true, 900f, false);
                if (_noLago == null)
                {
                    _noLago = new bool[N * N];
                    _aguaDoLago = new float[N * N];
                    Array.Fill(_aguaDoLago, float.NaN);
                }
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    if (_dentro[k])
                    {
                        _noLago[k] = true;
                        _aguaDoLago[k] = float.IsNaN(_aguaDoLago[k]) ? nivel : Mathf.Max(_aguaDoLago[k], nivel);
                        float funda = dd == float.MaxValue ? 1f : Geo.Suave(0f, 900f, dd);
                        Alturas[k] = Mathf.Min(Alturas[k], nivel - 1.5f - lago.fundura * funda);
                    }
                    else if (dd != float.MaxValue && !_noLago[k])   // a margem não sobe dentro de outro lago
                    {
                        float h = Alturas[k];
                        if (h > nivel + 0.5f) h = nivel + 0.5f + (h - nivel - 0.5f) * Geo.Suave(0f, 750f, dd);
                        else if (dd < 400f) h = Mathf.Lerp(nivel + 0.5f, h, Geo.Suave(150f, 400f, dd));
                        Alturas[k] = h;
                    }
                });
            }

            // as ilhas: sobem da água do lago em que ficam
            foreach (var ilha in d.ilhas)
            {
                if (ilha.contorno.Count < 3) continue;
                var c = DesenhoDoMapa.Contorno(ilha.contorno);
                // a água mais alta dos lagos em que ela fica (o meio ou um ponto da beira dentro do lago: numa ilha torta, o
                // meio podia cair noutro lago, mais baixo)
                float agua = float.MinValue;
                var meio = Linhas.Centro(c);
                for (int l = 0; l < d.lagos.Count; l++)
                {
                    if (d.lagos[l].contorno.Count < 3) continue;
                    var lc = DesenhoDoMapa.Contorno(d.lagos[l].contorno);
                    bool nele = Linhas.Dentro(meio, lc);
                    for (int q = 0; q < c.Count && !nele; q += 4) nele = Linhas.Dentro(c[q], lc);
                    if (nele) agua = Mathf.Max(agua, NivelDosLagos[l]);
                }
                if (agua == float.MinValue) agua = 80f;
                Marcar(c);
                Distancias(c, true, 400f, false);
                Aplicar((k, x, z) =>
                {
                    if (!_dentro[k]) return;
                    if (_noLago != null) _noLago[k] = false;
                    float dd = _dist[k] == float.MaxValue ? 400f : _dist[k];
                    Alturas[k] = agua + 0.6f + ilha.altura * Geo.Suave(0f, 350f, dd);
                });
            }

            // os rios: primeiro a água de cada um (da nascente à foz, sempre descendo, chegando na água de onde ele cai:
            // os que caem num lago ou saem do mapa antes, depois os que caem num rio já feito), e então o vale em volta
            // (mais largo nos rios largos e perto da nascente), o leito abaixo da água e a margem seca logo fora dela
            int nr = d.rios.Count;
            LinhasDosRios = new List<Vector2>[nr];
            NiveisDosRios = new float[nr][];
            FracoesDosRios = new float[nr][];
            for (int k = 0; k < nr; k++)
                if (d.rios[k].pontos.Count >= 2) LinhasDosRios[k] = DesenhoDoMapa.Linha(d.rios[k].pontos);
            var feito = new bool[nr];
            for (int volta = 0; volta <= nr; volta++)
            {
                bool algum = false;
                for (int k = 0; k < nr; k++)
                {
                    if (feito[k] || LinhasDosRios[k] == null) continue;
                    var foz = LinhasDosRios[k][LinhasDosRios[k].Count - 1];
                    float alvo = float.NaN;
                    bool pronto = Mathf.Abs(foz.x) > DesenhoDoMapa.Metade - 250f || Mathf.Abs(foz.y) > DesenhoDoMapa.Metade - 250f;
                    for (int l = 0; l < d.lagos.Count && !pronto; l++)
                    {
                        if (d.lagos[l].contorno.Count < 3) continue;
                        var c = DesenhoDoMapa.Contorno(d.lagos[l].contorno);
                        if (Linhas.Dentro(foz, c) || Linhas.Distancia(foz, c, true) < 150f)
                        {
                            alvo = NivelDosLagos[l];
                            pronto = true;
                        }
                    }
                    for (int q = 0; q < nr && !pronto; q++)
                    {
                        if (!feito[q] || q == k) continue;
                        if (Linhas.Distancia(foz, LinhasDosRios[q], out float t) < 150f)
                        {
                            alvo = NivelDoRio(q, t);
                            pronto = true;
                        }
                    }
                    if (!pronto) continue;
                    PerfilDoRio(d.rios[k], k, alvo);
                    feito[k] = true;
                    algum = true;
                }
                if (!algum) break;
            }
            for (int k = 0; k < nr; k++)
            {
                if (LinhasDosRios[k] == null) continue;
                if (!feito[k]) PerfilDoRio(d.rios[k], k, float.NaN);
                var rio = d.rios[k];
                int ki = k;
                float maior = Mathf.Max(rio.larguraNoComeco, rio.larguraNoFim);
                float AberturaEm(float t, float larg) => r.aberturaDosVales * Mathf.Clamp(larg / 50f, 0.35f, 1.2f) * (1f + 0.5f * (1f - t));
                // na serra o vale abre até 5 vezes (pela grade das montanhas: estreito, ele virava um cânion de paredes a 70°)
                float serra = 0f;
                if (_montanha != null)
                    foreach (var q in LinhasDosRios[k]) serra = Mathf.Max(serra, _montanha[Indice(q.x, q.y)]);
                float alcance = AberturaEm(0f, maior) * (1f + 4f * serra) * 2.6f + maior;
                Distancias(LinhasDosRios[k], false, alcance, true);
                Aplicar((kk, x, z) =>
                {
                    float dd = _dist[kk];
                    if (dd == float.MaxValue) return;
                    float t = _frac[kk];
                    float nivel = NivelDoRio(ki, t);
                    float larg = DesenhoDoMapa.LarguraDoRio(rio, t), meia = larg * 0.5f;
                    float h = Alturas[kk];
                    // o piso do vale: a água do rio, ou, numa ilha, logo acima da água do lago dela
                    float piso = nivel;
                    if (_noLago != null && !_noLago[kk] && !float.IsNaN(_aguaDoLago[kk])) piso = Mathf.Max(nivel, _aguaDoLago[kk] + 0.6f);
                    if (h > piso)
                    {
                        // no brejo, só o leito (o vale alagaria o brejo inteiro)
                        float brejo = d.TemGrade ? d.Fracao(Cobertura.Brejo, x, z) + d.Fracao(Cobertura.AguaDoBrejo, x, z) : 0f;
                        float abre = AberturaEm(t, larg) * (1f + 4f * (_montanha != null ? _montanha[kk] : 0f));
                        float fora = Mathf.Max(0f, dd - meia) / abre;
                        h = piso + (h - piso) * Mathf.Lerp(1f - Mathf.Exp(-fora * fora), 1f, Mathf.Clamp01(brejo));
                    }
                    if (dd < meia) h = Mathf.Min(h, nivel - 1.2f - 1.4f * (1f - dd / meia));
                    else if (dd < meia + 14f && h < nivel + 0.4f && (_noLago == null || !_noLago[kk]))
                        h = Mathf.Lerp(nivel + 0.4f, h, Geo.Suave(meia + 3f, meia + 14f, dd));
                    Alturas[kk] = h;
                });
            }
        }

        /// <summary>A água do brejo (NaN sem brejo).</summary>
        public float NivelDoBrejo { get; private set; }

        /// <summary>A água de cada lago (a do desenho ou a calculada pelo chão), na ordem de <see cref="DesenhoDoMapa.lagos"/>.</summary>
        public float[] NivelDosLagos { get; private set; }

        /// <summary>A linha suavizada de cada rio, a água em cada ponto dela e a fração do comprimento de cada ponto.</summary>
        public List<Vector2>[] LinhasDosRios { get; private set; }
        public float[][] NiveisDosRios { get; private set; }
        public float[][] FracoesDosRios { get; private set; }

        /// <summary>A água do rio na fração do comprimento.</summary>
        public float NivelDoRio(int rio, float fracao)
        {
            var f = FracoesDosRios[rio];
            var n = NiveisDosRios[rio];
            if (f == null || f.Length == 0) return 0f;
            if (fracao <= 0f) return n[0];
            if (fracao >= 1f) return n[n.Length - 1];
            int a = 0, b = f.Length - 1;
            while (b - a > 1)
            {
                int m = (a + b) / 2;
                if (f[m] <= fracao) a = m;
                else b = m;
            }
            float t = f[b] > f[a] ? (fracao - f[a]) / (f[b] - f[a]) : 0f;
            return Mathf.Lerp(n[a], n[b], t);
        }

        // a água do rio em cada ponto da linha: pela do desenho, ou pelo chão (o mais baixo até ali, 2 m abaixo dele),
        // chegando no alvo na foz (a água do lago ou do rio de baixo; NaN, livre)
        void PerfilDoRio(RioDoDesenho rio, int k, float alvo)
        {
            var linha = LinhasDosRios[k];
            int n = linha.Count;
            var niveis = new float[n];
            var fracoes = new float[n];
            float total = Mathf.Max(Linhas.Comprimento(linha), 1e-3f), acum = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) acum += Vector2.Distance(linha[i - 1], linha[i]);
                fracoes[i] = acum / total;
            }
            for (int i = 0; i < n; i++)
            {
                if (!rio.nivelPeloRelevo)
                {
                    niveis[i] = DesenhoDoMapa.NivelDoRio(rio, fracoes[i]);
                    continue;
                }
                float h = Altura(linha[i].x, linha[i].y) - 2f;
                niveis[i] = i == 0 ? h : Mathf.Min(niveis[i - 1], h);
            }
            if (rio.nivelPeloRelevo && !float.IsNaN(alvo))
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.Max(niveis[i], alvo);
                    niveis[i] = Mathf.Lerp(v, alvo, Geo.Suave(0.8f, 1f, fracoes[i]));
                }
            NiveisDosRios[k] = niveis;
            FracoesDosRios[k] = fracoes;
        }

        static float Percentil(List<float> valores, float p)
        {
            if (valores.Count == 0) return 0f;
            valores.Sort();
            return valores[Mathf.Clamp(Mathf.RoundToInt(p * (valores.Count - 1)), 0, valores.Count - 1)];
        }

        // os pontos a cada "passo" ao longo da linha
        static List<Vector2> Reamostrar(List<Vector2> pontos, float passo, bool fechada)
        {
            var saida = new List<Vector2>();
            int n = pontos.Count, lados = fechada ? n : n - 1;
            for (int s = 0; s < lados; s++)
            {
                var a = pontos[s];
                var b = pontos[(s + 1) % n];
                int partes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / passo));
                for (int q = 0; q < partes; q++) saida.Add(Vector2.Lerp(a, b, q / (float)partes));
            }
            return saida;
        }

        /// <summary>O x (ou o z) do ponto i da grade.</summary>
        public float X(int i) => -DesenhoDoMapa.Metade + i * Passo;

        // o ponto da grade mais perto
        int Indice(float x, float z) =>
            Mathf.Clamp(Mathf.RoundToInt((z + DesenhoDoMapa.Metade) / Passo), 0, N - 1) * N + Mathf.Clamp(Mathf.RoundToInt((x + DesenhoDoMapa.Metade) / Passo), 0, N - 1);

        /// <summary>A altura em qualquer ponto (entre os 4 pontos da grade em volta; fora do mapa, a da beira).</summary>
        public float Altura(float x, float z)
        {
            float fx = Mathf.Clamp((x + DesenhoDoMapa.Metade) / Passo, 0f, N - 1.001f);
            float fz = Mathf.Clamp((z + DesenhoDoMapa.Metade) / Passo, 0f, N - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            int k = j * N + i;
            float a = Alturas[k] + (Alturas[k + 1] - Alturas[k]) * tx;
            float b = Alturas[k + N] + (Alturas[k + N + 1] - Alturas[k + N]) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>A maior altura num raio em volta do ponto (o topo de um pico, para o mapa).</summary>
        public float Maxima(Vector2 p, float raio)
        {
            float m = float.MinValue;
            Caixa(p, raio, (k, x, z) => m = Mathf.Max(m, Alturas[k]), false);
            return m == float.MinValue ? Altura(p.x, p.y) : m;
        }

        // ------------------------------------------------------------------ as passadas na grade

        void Aplicar(Action<int, float, float> conta)
        {
            Parallel.For(0, N, j =>
            {
                float z = X(j);
                for (int i = 0; i < N; i++) conta(j * N + i, X(i), z);
            });
        }

        // a conta em cada ponto da caixa em volta do ponto (em paralelo por linha, ou não, para quem junta um valor)
        void Caixa(Vector2 p, float alcance, Action<int, float, float> conta, bool paralelo = true)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt((p.x - alcance + DesenhoDoMapa.Metade) / Passo));
            int i1 = Mathf.Min(N - 1, Mathf.CeilToInt((p.x + alcance + DesenhoDoMapa.Metade) / Passo));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((p.y - alcance + DesenhoDoMapa.Metade) / Passo));
            int j1 = Mathf.Min(N - 1, Mathf.CeilToInt((p.y + alcance + DesenhoDoMapa.Metade) / Passo));
            if (i1 < i0 || j1 < j0) return;
            void Linha(int j)
            {
                float z = X(j);
                for (int i = i0; i <= i1; i++) conta(j * N + i, X(i), z);
            }
            if (paralelo) Parallel.For(j0, j1 + 1, Linha);
            else for (int j = j0; j <= j1; j++) Linha(j);
        }

        // _dentro: os pontos da grade dentro do polígono
        void Marcar(List<Vector2> poligono)
        {
            Array.Clear(_dentro, 0, _dentro.Length);
            Linhas.Varrer(poligono, -DesenhoDoMapa.Metade, -DesenhoDoMapa.Metade, Passo, N, N, (j, i0, i1) =>
            {
                for (int i = i0; i <= i1; i++) _dentro[j * N + i] = true;
            });
        }

        // _dist: a distância de cada ponto até a linha, até o alcance (fora dele, MaxValue); _frac: a fração do
        // comprimento no ponto mais perto. Segmento por segmento, só na caixa dele (e cada linha da caixa em paralelo).
        void Distancias(List<Vector2> pontos, bool fechada, float alcance, bool comFracao)
        {
            Array.Fill(_dist, float.MaxValue);
            int n = pontos.Count, lados = fechada ? n : n - 1;
            float total = Mathf.Max(Linhas.Comprimento(pontos, fechada), 1e-3f), acum = 0f;
            for (int s = 0; s < lados; s++)
            {
                var a = pontos[s];
                var b = pontos[(s + 1) % n];
                float l = Vector2.Distance(a, b);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - alcance + DesenhoDoMapa.Metade) / Passo));
                int i1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + alcance + DesenhoDoMapa.Metade) / Passo));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - alcance + DesenhoDoMapa.Metade) / Passo));
                int j1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + alcance + DesenhoDoMapa.Metade) / Passo));
                float antes = acum;
                if (i1 >= i0 && j1 >= j0)
                    Parallel.For(j0, j1 + 1, j =>
                    {
                        float z = X(j);
                        for (int i = i0; i <= i1; i++)
                        {
                            int k = j * N + i;
                            float dd = Linhas.DistanciaDoSegmento(new Vector2(X(i), z), a, b, out float t);
                            if (dd < alcance && dd < _dist[k])
                            {
                                _dist[k] = dd;
                                if (comFracao) _frac[k] = (antes + l * t) / total;
                            }
                        }
                    });
                acum += l;
            }
        }

        // as cristas do ruído (de 0 a 1): o 1 - |ruído| ao quadrado, somado em oitavas
        static float Cristas(Ruido r, float x, float y)
        {
            float v = 0f, amp = 1f, f = 1f, total = 0f;
            for (int o = 0; o < 4; o++)
            {
                float n = 1f - Mathf.Abs(r.Amostra(x * f + o * 31.7f, y * f - o * 13.3f) * 1.4f);
                n = Mathf.Clamp01(n);
                v += n * n * amp;
                total += amp;
                amp *= 0.5f;
                f *= 2.1f;
            }
            return v / total;
        }
    }
}
