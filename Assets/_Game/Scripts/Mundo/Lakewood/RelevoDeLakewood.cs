using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O relevo grosso do Condado de Lakewood (Plano_Lakewood, Etapa 2): uma grade de alturas (20 m entre os pontos) dos
    /// 12 × 10 km, feita em ordem: as colinas do ruído da base, as zonas de relevo (a planície do oeste, a serra do norte,
    /// as montanhas Crowley, as colinas do leste, os morros do sul, a garganta da represa e o pátio da usina), as
    /// montanhas da imagem de referência (a grade da rocha, amaciada, com as cristas por cima), os picos, as passagens,
    /// o aplainamento das cidades e das instalações, a bacia do reservatório (a água na cota fixa, funda longe da margem e
    /// junto da crista da barragem, a margem oeste em rampa comprida e a leste curta, a garganta em pé), as ilhas, os rios
    /// (a água de cada um descendo até o lago, o rio ou a borda; o vale, o leito e a margem) e o pé da barragem, do lado de
    /// fora. O relevo de perto (o detalhe e as estradas) é do <see cref="ChaoDeLakewood"/>. Código puro: pode rodar em
    /// outra thread.
    /// </summary>
    public sealed class RelevoDeLakewood
    {
        public readonly MapaDeLakewood Mapa;
        public readonly float Passo;
        /// <summary>Os pontos de cada lado da grade (de 0 a largura e de 0 a comprimento, as pontas inclusas).</summary>
        public readonly int NX, NZ;
        public readonly float[] Alturas;

        readonly float[] _dist, _frac;
        readonly bool[] _dentro;
        // a grade das montanhas amaciada, em cada ponto (null sem a grade): o vale do rio abre mais na serra
        float[] _montanha;
        // a média do relevo no raio do aplainamento (as cidades e as instalações se acomodam nela)
        float[] _borrado;
        // os pontos debaixo da água de um lago (fora das ilhas): a margem do rio não sobe ali; e a água do lago em cada
        // ponto dentro dele (NaN fora): o vale de um rio não leva a ilha para baixo dela
        bool[] _noLago;
        float[] _aguaDoLago;

        /// <summary>A direção perpendicular à crista da barragem, para fora do lago (a jusante).</summary>
        public Vector2 Jusante { get; private set; } = Vector2.down;

        /// <summary>A água de cada lago (a do desenho ou a calculada pelo chão), na ordem de <see cref="MapaDeLakewood.lagos"/>.</summary>
        public float[] NivelDosLagos { get; private set; }

        /// <summary>A linha suavizada de cada rio, a água em cada ponto dela e a fração do comprimento de cada ponto.</summary>
        public List<Vector2>[] LinhasDosRios { get; private set; }
        public float[][] NiveisDosRios { get; private set; }
        public float[][] FracoesDosRios { get; private set; }
        /// <summary>O comprimento da linha de cada rio (m) e se ele desemboca num lago (o barranco baixa na foz).</summary>
        public float[] ComprimentosDosRios { get; private set; }
        public bool[] DesembocaNoLago { get; private set; }

        public RelevoDeLakewood(MapaDeLakewood d, float passo = 20f)
        {
            Mapa = d;
            Passo = passo;
            NX = Mathf.RoundToInt(d.largura / passo) + 1;
            NZ = Mathf.RoundToInt(d.comprimento / passo) + 1;
            int n = NX * NZ;
            Alturas = new float[n];
            _dist = new float[n];
            _frac = new float[n];
            _dentro = new bool[n];
            var r = d.relevo;
            var colinas = new Ruido(d.semente, "colinas-de-lakewood");
            var cristas = new Ruido(d.semente, "cristas-de-lakewood");
            var fator = new float[n];
            Array.Fill(fator, 1f);

            // a base: as colinas do ruído
            Aplicar((k, x, z) => Alturas[k] = r.altitudeBase + r.amplitude * colinas.Fbm(x / r.escala, z / r.escala, 4));

            // as zonas: dentro, a altitude e as colinas delas; na beira, a passagem (metade para cada lado)
            foreach (var zona in r.zonas)
            {
                if (zona.contorno.Count < 3) continue;
                var c = MapaDeLakewood.Contorno(zona.contorno);
                float meia = Mathf.Max(zona.borda * 0.5f, passo);
                Marcar(c);
                Distancias(c, true, meia, false);
                var z0 = zona;
                Aplicar((k, x, z) =>
                {
                    float w;
                    if (_dentro[k]) w = _dist[k] == float.MaxValue ? 1f : 0.5f + 0.5f * Geo.Suave(0f, meia, _dist[k]);
                    else
                    {
                        if (_dist[k] == float.MaxValue) return;
                        w = 0.5f * (1f - Geo.Suave(0f, meia, _dist[k]));
                    }
                    float h = z0.altitude + z0.amplitude * colinas.Fbm(x / z0.escala + 11.3f, z / z0.escala - 7.9f, 4);
                    if (z0.aspereza > 0f) h += z0.aspereza * Cristas(cristas, x / 1500f, z / 1500f);
                    Alturas[k] = Mathf.Lerp(Alturas[k], h, w);
                    fator[k] = Mathf.Lerp(fator[k], z0.fatorDasMontanhas, w);
                });
            }

            // as montanhas da imagem de referência: a grade da rocha (vezes o fator da zona), amaciada em duas passadas
            // de média, com a altura pela grade e as cristas por cima
            if (d.TemGrade && r.alturaDasMontanhas > 0f)
            {
                var grade = new float[n];
                Aplicar((k, x, z) => grade[k] = d.Montanha(x, z) * fator[k]);
                int raio = Mathf.RoundToInt(r.suavidadeDasMontanhas / passo);
                for (int passada = 0; passada < 2 && raio > 0; passada++) Media(grade, raio);
                // (o vale de passagem das rodovias, do mapa de antes, saiu na Etapa 5, 10/11/2026: as estradas são traçadas
                // pelo relevo e sobem a serra em zigue-zague; baixar a montanha em volta delas mudava o mapa inteiro)
                _montanha = grade;
                Aplicar((k, x, z) =>
                {
                    float m = grade[k];
                    if (m <= 0f) return;
                    Alturas[k] += r.alturaDasMontanhas * Mathf.Pow(m, 1.15f) + r.asperezaDasMontanhas * m * Cristas(cristas, x / 1500f, z / 1500f);
                });
            }

            // os picos
            foreach (var p in r.picos)
            {
                var p0 = p;
                Caixa(p.ponto, p.raio * 2.6f, (k, x, z) =>
                {
                    float dx = x - p0.ponto.x, dz = z - p0.ponto.y;
                    Alturas[k] += p0.subida * Mathf.Exp(-(dx * dx + dz * dz) / (p0.raio * p0.raio));
                });
            }

            // as passagens: perto da linha, o relevo desce até a altura dela
            foreach (var ps in r.passagens)
            {
                if (ps.pontos.Count < 2) continue;
                Distancias(MapaDeLakewood.Linha(ps.pontos), false, ps.largura * 2.5f, false);
                var ps0 = ps;
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    if (dd == float.MaxValue || Alturas[k] <= ps0.altura) return;
                    float perfil = Mathf.Exp(-(dd / ps0.largura) * (dd / ps0.largura));
                    Alturas[k] = Mathf.Lerp(Alturas[k], ps0.altura, perfil);
                });
            }

            // o aplainamento das cidades (dentro do contorno) e das instalações (no raio): o chão vai para a média do
            // relevo em volta (ou para a cota pedida), com a passagem na beira
            _borrado = (float[])Alturas.Clone();
            Media(_borrado, Mathf.Max(1, Mathf.RoundToInt(r.raioDoAplainamento / passo)));
            foreach (var cidade in d.cidades)
            {
                if (cidade.contorno.Count < 3 || cidade.aplainar <= 0f) continue;
                var c = MapaDeLakewood.Contorno(cidade.contorno);
                float meia = Mathf.Max(cidade.borda * 0.5f, passo);
                Marcar(c);
                Distancias(c, true, meia, false);
                var c0 = cidade;
                Aplicar((k, x, z) =>
                {
                    float w;
                    if (_dentro[k]) w = _dist[k] == float.MaxValue ? 1f : 0.5f + 0.5f * Geo.Suave(0f, meia, _dist[k]);
                    else
                    {
                        if (_dist[k] == float.MaxValue) return;
                        w = 0.5f * (1f - Geo.Suave(0f, meia, _dist[k]));
                    }
                    float alvo = c0.altitude > 0f ? c0.altitude : _borrado[k];
                    Alturas[k] = Mathf.Lerp(Alturas[k], alvo, w * c0.aplainar);
                });
            }
            foreach (var lugar in d.lugares)
            {
                if (lugar.aplainar <= 0f || lugar.raio <= 0f) continue;
                float hc = AlturaBorrada(lugar.ponto.x, lugar.ponto.y);
                var l0 = lugar;
                Caixa(lugar.ponto, lugar.raio, (k, x, z) =>
                {
                    float dd = Vector2.Distance(new Vector2(x, z), l0.ponto);
                    if (dd > l0.raio) return;
                    float w = l0.aplainar * (1f - Geo.Suave(l0.raio * 0.55f, l0.raio, dd));
                    Alturas[k] = Mathf.Lerp(Alturas[k], hc, w);
                });
            }

            // a barragem: para que lado fica a jusante (o lado oposto ao meio do reservatório)
            var represa = d.represa;
            bool temRepresa = represa.Tem;
            int principal = d.LagoPrincipal();
            if (temRepresa)
            {
                var dir = represa.Direcao;
                var perp = new Vector2(-dir.y, dir.x);
                var meioDoLago = principal >= 0 ? Linhas.Centro(MapaDeLakewood.Contorno(d.lagos[principal].contorno)) : d.Meio;
                Jusante = Vector2.Dot(perp, meioDoLago - represa.Meio) > 0f ? -perp : perp;
            }
            float DistanciaDaRepresa(float x, float z) => temRepresa ? Linhas.DistanciaDoSegmento(new Vector2(x, z), represa.a, represa.b, out _) : float.MaxValue;

            // os lagos: a água (a do desenho, ou a do chão em volta), a bacia funda longe da margem (a crista da barragem não
            // conta como margem: junto dela o lago é fundo), a margem em rampa até o relevo (comprida a oeste, curta a leste,
            // em pé na garganta da represa) e, onde o chão de fora é mais baixo que a água, a beira levantada; a jusante da
            // barragem, nada disso: ali fica o vale
            NivelDosLagos = new float[d.lagos.Count];
            for (int l = 0; l < d.lagos.Count; l++)
            {
                var lago = d.lagos[l];
                NivelDosLagos[l] = lago.nivel;
                if (lago.contorno.Count < 3) continue;
                var c = MapaDeLakewood.Contorno(lago.contorno);
                float nivel = lago.nivel;
                if (lago.nivelPeloRelevo)
                {
                    var margem = new List<float>();
                    foreach (var p in Reamostrar(c, 30f, true)) margem.Add(Altura(p.x, p.y));
                    nivel = Percentil(margem, 0.1f) - 1f;
                }
                NivelDosLagos[l] = nivel;
                float xMin = float.MaxValue, xMax = float.MinValue;
                foreach (var p in c)
                {
                    xMin = Mathf.Min(xMin, p.x);
                    xMax = Mathf.Max(xMax, p.x);
                }
                bool esteTemRepresa = temRepresa && l == principal;
                // o vale afogado (só no reservatório): a distância até a linha do canal e a fração dela (0 no começo, 1 na barragem)
                float[] distCanal = null, fracCanal = null;
                if (l == principal && d.canalDoReservatorio.Count >= 2 && d.larguraDoCanal > 0f)
                {
                    Distancias(MapaDeLakewood.Linha(d.canalDoReservatorio), false, d.larguraDoCanal * 2.5f, true);
                    distCanal = (float[])_dist.Clone();
                    fracCanal = (float[])_frac.Clone();
                }
                // as ilhas deste lago contam como margem: o fundo sobe até elas (sem isso, a beira delas era um paredão
                // embaixo d'água, do raso da ilha direto para o fundo da bacia)
                float[] distIlhas = null;
                foreach (var ilha in d.ilhas)
                {
                    if (ilha.contorno.Count < 3) continue;
                    var ci = MapaDeLakewood.Contorno(ilha.contorno);
                    if (!Linhas.Dentro(Linhas.Centro(ci), c)) continue;
                    Distancias(ci, true, 700f, false);
                    if (distIlhas == null) distIlhas = (float[])_dist.Clone();
                    else for (int k = 0; k < n; k++) distIlhas[k] = Mathf.Min(distIlhas[k], _dist[k]);
                }
                // as cidades e os lugares da beira: ali a margem é sempre deitada (sem a encosta em pé)
                var cidades = new List<List<Vector2>>();
                foreach (var cid in d.cidades) if (cid.contorno.Count >= 3) cidades.Add(MapaDeLakewood.Contorno(cid.contorno));
                bool MargemDeitada(Vector2 p)
                {
                    foreach (var cc in cidades) if (Linhas.Dentro(p, cc) || Linhas.Distancia(p, cc, true) < 200f) return true;
                    foreach (var lg in d.lugares) if (lg.raio > 0f && Vector2.Distance(p, lg.ponto) < lg.raio + 120f) return true;
                    return false;
                }
                Marcar(c);
                Distancias(c, true, 1000f, false,
                    pular: (p, q) => esteTemRepresa && Linhas.DistanciaDoSegmento(p, represa.a, represa.b, out _) < 60f && Linhas.DistanciaDoSegmento(q, represa.a, represa.b, out _) < 60f);
                if (_noLago == null)
                {
                    _noLago = new bool[n];
                    _aguaDoLago = new float[n];
                    Array.Fill(_aguaDoLago, float.NaN);
                }
                var lg = lago;
                float larguraDoCanal = Mathf.Max(1f, d.larguraDoCanal), funduraDoCanal = d.funduraDoCanal;
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    // a linha d'água com reentrâncias pequenas: a distância até a margem, com sinal (para dentro negativa),
                    // tremida por um ruído de uns 14 m em manchas de 110 m (as pontas e as enseadas pequenas)
                    float tremor = 14f * colinas.Fbm(x / 110f + 3.1f, z / 110f - 7.7f, 2);
                    float sinal = dd == float.MaxValue ? (_dentro[k] ? -1000f : 1000f) : (_dentro[k] ? -dd : dd);
                    if (dd != float.MaxValue) sinal += tremor;
                    if (sinal < 0f)
                    {
                        _noLago[k] = true;
                        _aguaDoLago[k] = float.IsNaN(_aguaDoLago[k]) ? nivel : Mathf.Max(_aguaDoLago[k], nivel);
                        float ddp = -sinal;
                        if (distIlhas != null) ddp = Mathf.Min(ddp, distIlhas[k]);
                        float funda = Geo.Suave(0f, 700f, ddp);
                        // o fundo: a fundura do lago longe da margem, com ondulações, e o vale afogado por baixo (mais fundo
                        // para a barragem), que não cava a beira
                        float prof = 1.5f + lg.fundura * funda + 2.5f * funda * (1f + colinas.Fbm(x / 320f + 11f, z / 320f + 5f, 2));
                        if (distCanal != null && distCanal[k] != float.MaxValue)
                        {
                            float dc = distCanal[k] / larguraDoCanal;
                            prof += funduraDoCanal * Mathf.Lerp(0.3f, 1f, fracCanal[k]) * Mathf.Exp(-dc * dc) * Geo.Suave(0f, 120f, ddp);
                        }
                        Alturas[k] = Mathf.Min(Alturas[k], nivel - prof);
                    }
                    else if (dd != float.MaxValue && !_noLago[k])   // a margem não sobe dentro de outro lago
                    {
                        float dRep = esteTemRepresa ? DistanciaDaRepresa(x, z) : float.MaxValue;
                        if (dRep < 400f) return;   // a jusante da barragem fica o vale
                        float t = xMax > xMin ? Mathf.Clamp01((x - xMin) / (xMax - xMin)) : 0.5f;
                        float rampa = Mathf.Lerp(lg.rampaDaMargemOeste, lg.rampaDaMargemLeste, t);
                        // umas margens mais em pé e outras mais deitadas, em trechos de uns 600 m
                        rampa *= 0.65f + 0.7f * colinas.Amostra01(x / 600f + 23f, z / 600f - 41f);
                        rampa = Mathf.Lerp(120f, rampa, Geo.Suave(250f, 700f, dRep));
                        float ddp = sinal;
                        float h = Alturas[k];
                        // as costas rochosas: em trechos (mais a leste), a encosta sobe em pé logo depois de uma faixa de
                        // uns 5 m de cascalho (o relevo chega em 30% da rampa); nas cidades e nos lugares da beira, não
                        float emPe = Geo.Suave(0.55f, 0.9f, colinas.Amostra01(x / 480f + 77f, z / 480f + 5f)) * Mathf.Lerp(0.25f, 1f, t);
                        if (emPe > 0f && MargemDeitada(new Vector2(x, z))) emPe = 0f;
                        float perfil = Mathf.Lerp(Geo.Suave(0f, rampa, ddp), Geo.Suave(5f, Mathf.Max(70f, rampa * 0.3f), ddp), emPe);
                        if (h > nivel + 0.5f) h = nivel + 0.5f + (h - nivel - 0.5f) * perfil;
                        else if (ddp < 400f) h = Mathf.Lerp(nivel + 0.5f, h, Geo.Suave(150f, 400f, ddp));
                        Alturas[k] = h;
                    }
                });
            }

            // as ilhas: sobem da água do lago em que ficam
            foreach (var ilha in d.ilhas)
            {
                if (ilha.contorno.Count < 3) continue;
                var c = MapaDeLakewood.Contorno(ilha.contorno);
                float agua = float.MinValue;
                var meio = Linhas.Centro(c);
                for (int l = 0; l < d.lagos.Count; l++)
                {
                    if (d.lagos[l].contorno.Count < 3) continue;
                    var lc = MapaDeLakewood.Contorno(d.lagos[l].contorno);
                    bool nele = Linhas.Dentro(meio, lc);
                    for (int q = 0; q < c.Count && !nele; q += 4) nele = Linhas.Dentro(c[q], lc);
                    if (nele) agua = Mathf.Max(agua, NivelDosLagos[l]);
                }
                if (agua == float.MinValue) agua = 150f;
                Marcar(c);
                Distancias(c, true, 400f, false);
                var il = ilha;
                var ruidoDaIlha = new Ruido(d.semente, "colinas-de-lakewood");
                Aplicar((k, x, z) =>
                {
                    float dd = _dist[k];
                    bool dentro = _dentro[k];
                    if (!dentro && dd == float.MaxValue) return;
                    // a linha d'água da ilha tremida como a do lago (o mesmo ruído): para fora, a ilha avança sobre o
                    // raso; para dentro, o lago entra (o leito fica, e a água do chão segue o relevo)
                    float sinal = dd == float.MaxValue ? 400f : (dentro ? dd : -dd);
                    if (dd != float.MaxValue) sinal += 14f * ruidoDaIlha.Fbm(x / 110f + 3.1f, z / 110f - 7.7f, 2);
                    if (sinal <= 0f) return;
                    if (_noLago != null) _noLago[k] = false;
                    Alturas[k] = agua + 0.6f + il.altura * Geo.Suave(0f, 350f, sinal);
                });
            }

            // os rios: a linha de cada um com os meandros (menores na serra), depois a água (da nascente à foz, sempre
            // descendo, chegando na água de onde ele cai: os que caem num lago ou saem do mapa antes, depois os que caem
            // num rio já feito), e então o vale em volta (mais largo nos rios largos e perto da nascente), com o piso no
            // alto do barranco (a água mais a margem). O canal de verdade (o leito e os barrancos, na largura exata) é
            // cavado na hora pelo ChaoDeLakewood.Altura: a grade de 20 m não o representa.
            int nr = d.rios.Count;
            LinhasDosRios = new List<Vector2>[nr];
            NiveisDosRios = new float[nr][];
            FracoesDosRios = new float[nr][];
            ComprimentosDosRios = new float[nr];
            DesembocaNoLago = new bool[nr];
            for (int k = 0; k < nr; k++)
            {
                LinhasDosRios[k] = MapaDeLakewood.LinhaDoRio(d.rios[k], d.semente, k, p => Montanha(p.x, p.y));
                if (LinhasDosRios[k] != null) ComprimentosDosRios[k] = Linhas.Comprimento(LinhasDosRios[k]);
            }
            var feito = new bool[nr];
            for (int volta = 0; volta <= nr; volta++)
            {
                bool algum = false;
                for (int k = 0; k < nr; k++)
                {
                    if (feito[k] || LinhasDosRios[k] == null) continue;
                    var foz = LinhasDosRios[k][LinhasDosRios[k].Count - 1];
                    float alvo = float.NaN;
                    bool pronto = NaBorda(foz, 250f);
                    for (int l = 0; l < d.lagos.Count && !pronto; l++)
                    {
                        if (d.lagos[l].contorno.Count < 3) continue;
                        var c = MapaDeLakewood.Contorno(d.lagos[l].contorno);
                        if (Linhas.Dentro(foz, c) || Linhas.Distancia(foz, c, true) < 150f)
                        {
                            alvo = NivelDosLagos[l];
                            DesembocaNoLago[k] = true;
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
                // na serra o vale abre até 5 vezes (pela grade das montanhas: estreito, ele virava um cânion)
                float serra = 0f;
                if (_montanha != null)
                    foreach (var q in LinhasDosRios[k]) serra = Mathf.Max(serra, _montanha[Indice(q.x, q.y)]);
                float alcance = AberturaEm(0f, maior) * (1f + 4f * serra) * 2.6f + maior;
                bool desemboca = DesembocaNoLago[k];
                Distancias(LinhasDosRios[k], false, alcance, true);
                Aplicar((kk, x, z) =>
                {
                    float dd = _dist[kk];
                    if (dd == float.MaxValue) return;
                    float t = _frac[kk];
                    float nivel = NivelDoRio(ki, t);
                    float larg = MapaDeLakewood.LarguraDoRio(rio, t), meia = larg * 0.5f;
                    // o barranco (mais baixo chegando no lago) e até onde vai o alto dele
                    float margem = MapaDeLakewood.MargemDoRio(larg) * (desemboca ? Mathf.Lerp(0.3f, 1f, Geo.Suave(1f, 0.95f, t)) : 1f);
                    float beira = meia + margem * 2.6f;
                    float h = Alturas[kk];
                    bool noLago = _noLago != null && _noLago[kk];
                    // o piso do vale: o alto do barranco, ou, numa ilha, logo acima da água do lago dela
                    float piso = nivel + margem;
                    if (_noLago != null && !noLago && !float.IsNaN(_aguaDoLago[kk])) piso = Mathf.Max(piso, _aguaDoLago[kk] + 0.6f + margem);
                    if (h > piso)
                    {
                        float abre = AberturaEm(t, larg) * (1f + 4f * (_montanha != null ? _montanha[kk] : 0f));
                        float fora = Mathf.Max(0f, dd - beira) / abre;
                        h = piso + (h - piso) * (1f - Mathf.Exp(-fora * fora));
                    }
                    else if (!noLago && dd < beira + 30f)
                        // o chão baixo junto do rio sobe até o alto do barranco (as margens ficam acima da água)
                        h = Mathf.Lerp(piso, h, Geo.Suave(beira + 6f, beira + 30f, dd));
                    Alturas[kk] = h;
                });
            }

            // o pé da barragem: entre as cabeceiras, do lado de fora, o chão fica na altura do pé (a parede sobe dele), e
            // volta ao relevo em 200 m
            if (temRepresa)
            {
                var a = represa.a;
                var dir = represa.Direcao;
                float comp = represa.Comprimento;
                var jus = Jusante;
                float pe = represa.alturaDoPe;
                Caixa(represa.Meio, comp * 0.5f + 260f, (k, x, z) =>
                {
                    if (_noLago != null && _noLago[k]) return;
                    var p = new Vector2(x, z);
                    float t = Vector2.Dot(p - a, dir) / comp;
                    // nas pontas, a passagem para o relevo em uns 70 m (10/11/2026: o corte seco deixava um paredão reto do
                    // lado de jusante, nas duas cabeceiras)
                    float dentro = Mathf.Min(Geo.Suave(0f, 0.08f, t), Geo.Suave(1f, 0.92f, t));
                    if (dentro <= 0f) return;
                    float dn = Vector2.Dot(p - a, jus);
                    if (dn < -12f) return;
                    float teto = dn <= 60f ? pe : pe + (dn - 60f) * 0.45f;
                    Alturas[k] = Mathf.Lerp(Alturas[k], Mathf.Min(Alturas[k], teto), dentro);
                });
            }
        }

        // ------------------------------------------------------------------ as consultas

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
        void PerfilDoRio(RioDoMapa rio, int k, float alvo)
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
                    niveis[i] = MapaDeLakewood.NivelDoRio(rio, fracoes[i]);
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

        /// <summary>O ponto está a menos de "folga" da borda do mapa?</summary>
        public bool NaBorda(Vector2 p, float folga) => p.x <= folga || p.y <= folga || p.x >= Mapa.largura - folga || p.y >= Mapa.comprimento - folga;

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

        /// <summary>O x do ponto i da grade.</summary>
        public float X(int i) => i * Passo;

        /// <summary>O z do ponto j da grade.</summary>
        public float Z(int j) => j * Passo;

        // o ponto da grade mais perto
        int Indice(float x, float z) =>
            Mathf.Clamp(Mathf.RoundToInt(z / Passo), 0, NZ - 1) * NX + Mathf.Clamp(Mathf.RoundToInt(x / Passo), 0, NX - 1);

        float Bilinear(float[] g, float x, float z)
        {
            float fx = Mathf.Clamp(x / Passo, 0f, NX - 1.001f);
            float fz = Mathf.Clamp(z / Passo, 0f, NZ - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            int k = j * NX + i;
            float a = g[k] + (g[k + 1] - g[k]) * tx;
            float b = g[k + NX] + (g[k + NX + 1] - g[k + NX]) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>A altura em qualquer ponto (entre os 4 pontos da grade em volta; fora do mapa, a da beira).</summary>
        public float Altura(float x, float z) => Bilinear(Alturas, x, z);

        /// <summary>A média do relevo no raio do aplainamento (o chão das cidades vai para ela).</summary>
        public float AlturaBorrada(float x, float z) => _borrado != null ? Bilinear(_borrado, x, z) : Altura(x, z);

        /// <summary>O quanto o ponto é montanha (a grade da imagem amaciada; 0 sem a grade).</summary>
        public float Montanha(float x, float z) => _montanha != null ? Bilinear(_montanha, x, z) : 0f;

        /// <summary>A maior altura num raio em volta do ponto (o topo de um pico, para o mapa).</summary>
        public float Maxima(Vector2 p, float raio)
        {
            float m = float.MinValue;
            Caixa(p, raio, (k, x, z) => m = Mathf.Max(m, Alturas[k]), false);
            return m == float.MinValue ? Altura(p.x, p.y) : m;
        }

        /// <summary>A inclinação do chão no ponto (a subida por metro), pela grade.</summary>
        public float Inclinacao(float x, float z)
        {
            float hx = (Altura(x + Passo, z) - Altura(x - Passo, z)) / (2f * Passo);
            float hz = (Altura(x, z + Passo) - Altura(x, z - Passo)) / (2f * Passo);
            return Mathf.Sqrt(hx * hx + hz * hz);
        }

        // ------------------------------------------------------------------ as passadas na grade

        // a média numa caixa de (2 raio + 1) pontos, em x e depois em z (a borda repete o último ponto)
        void Media(float[] g, int raio)
        {
            var tmp = new float[NX * NZ];
            Parallel.For(0, NZ, j =>
            {
                float soma = 0f;
                for (int i = -raio; i <= raio; i++) soma += g[j * NX + Mathf.Clamp(i, 0, NX - 1)];
                for (int i = 0; i < NX; i++)
                {
                    tmp[j * NX + i] = soma / (2 * raio + 1);
                    soma += g[j * NX + Mathf.Min(NX - 1, i + raio + 1)] - g[j * NX + Mathf.Max(0, i - raio)];
                }
            });
            Parallel.For(0, NX, i =>
            {
                float soma = 0f;
                for (int j = -raio; j <= raio; j++) soma += tmp[Mathf.Clamp(j, 0, NZ - 1) * NX + i];
                for (int j = 0; j < NZ; j++)
                {
                    g[j * NX + i] = soma / (2 * raio + 1);
                    soma += tmp[Mathf.Min(NZ - 1, j + raio + 1) * NX + i] - tmp[Mathf.Max(0, j - raio) * NX + i];
                }
            });
        }

        void Aplicar(Action<int, float, float> conta)
        {
            Parallel.For(0, NZ, j =>
            {
                float z = Z(j);
                for (int i = 0; i < NX; i++) conta(j * NX + i, X(i), z);
            });
        }

        // a conta em cada ponto da caixa em volta do ponto (em paralelo por linha, ou não, para quem junta um valor)
        void Caixa(Vector2 p, float alcance, Action<int, float, float> conta, bool paralelo = true)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt((p.x - alcance) / Passo));
            int i1 = Mathf.Min(NX - 1, Mathf.CeilToInt((p.x + alcance) / Passo));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((p.y - alcance) / Passo));
            int j1 = Mathf.Min(NZ - 1, Mathf.CeilToInt((p.y + alcance) / Passo));
            if (i1 < i0 || j1 < j0) return;
            void Linha(int j)
            {
                float z = Z(j);
                for (int i = i0; i <= i1; i++) conta(j * NX + i, X(i), z);
            }
            if (paralelo) Parallel.For(j0, j1 + 1, Linha);
            else for (int j = j0; j <= j1; j++) Linha(j);
        }

        // _dentro: os pontos da grade dentro do polígono
        void Marcar(List<Vector2> poligono)
        {
            Array.Clear(_dentro, 0, _dentro.Length);
            Linhas.Varrer(poligono, 0f, 0f, Passo, NX, NZ, (j, i0, i1) =>
            {
                for (int i = i0; i <= i1; i++) _dentro[j * NX + i] = true;
            });
        }

        // _dist: a distância de cada ponto até a linha, até o alcance (fora dele, MaxValue); _frac: a fração do
        // comprimento no ponto mais perto. Segmento por segmento, só na caixa dele (e cada linha da caixa em paralelo).
        // "pular" diz os lados que não contam (a crista da barragem no contorno do lago).
        void Distancias(List<Vector2> pontos, bool fechada, float alcance, bool comFracao, Func<Vector2, Vector2, bool> pular = null)
        {
            Array.Fill(_dist, float.MaxValue);
            int n = pontos.Count, lados = fechada ? n : n - 1;
            float total = Mathf.Max(Linhas.Comprimento(pontos, fechada), 1e-3f), acum = 0f;
            for (int s = 0; s < lados; s++)
            {
                var a = pontos[s];
                var b = pontos[(s + 1) % n];
                float l = Vector2.Distance(a, b);
                float antes = acum;
                acum += l;
                if (pular != null && pular(a, b)) continue;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - alcance) / Passo));
                int i1 = Mathf.Min(NX - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + alcance) / Passo));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - alcance) / Passo));
                int j1 = Mathf.Min(NZ - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + alcance) / Passo));
                if (i1 < i0 || j1 < j0) continue;
                Parallel.For(j0, j1 + 1, j =>
                {
                    float z = Z(j);
                    for (int i = i0; i <= i1; i++)
                    {
                        int k = j * NX + i;
                        float dd = Linhas.DistanciaDoSegmento(new Vector2(X(i), z), a, b, out float t);
                        if (dd < alcance && dd < _dist[k])
                        {
                            _dist[k] = dd;
                            if (comFracao) _frac[k] = (antes + l * t) / total;
                        }
                    }
                });
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
