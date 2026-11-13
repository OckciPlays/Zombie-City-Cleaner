using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O chão da cena do Lakewood (Plano_Lakewood, Etapas 1 e 2): o relevo do mapa de perto e o que fica em cima dele. Feito
    /// uma vez a partir do <see cref="MapaDeLakewood"/> e do <see cref="RelevoDeLakewood"/>:
    ///  - a grade da água (12 m: o lago ou o rio e a altura da água em cada célula);
    ///  - o perfil de cada estrada (Etapa 5: os pontos a cada 8 m, a altura suavizada e, por cima da água, a ponte);
    ///  - a altura do chão em qualquer ponto (<see cref="Altura"/>): o relevo, um detalhe pequeno, o leito dos rios finos e
    ///    o chão acomodado às estradas;
    ///  - as malhas: o terreno de um bloco de 64 m (<see cref="Terreno"/>, com os pesos das texturas do chão na cor do
    ///    vértice), o relevo de longe de um setor (<see cref="Longe"/>, em cores da paleta), a água dos lagos e dos rios, a
    ///    barragem e a hidrelétrica (<see cref="UsinaDeLakewood"/>) e as estradas com as pontes;
    ///  - as plantas de um bloco (<see cref="Plantas"/>), pela cobertura da imagem.
    /// Código puro: depois de feito, tudo pode rodar em outra thread.
    /// </summary>
    public sealed partial class ChaoDeLakewood : IChaoDaGrama
    {
        public const float TamanhoDoBloco = 64f;

        const float CelulaDoIndice = 128f;
        const float Acostamento = 3f, FolgaDaPonte = 6f;

        public readonly MapaDeLakewood Mapa;
        public readonly RelevoDeLakewood Relevo;
        /// <summary>A hidrelétrica (Etapa 4): os pátios no chão, a água do canal de fuga, as estradas de acesso e as peças.</summary>
        public readonly UsinaDeLakewood Usina;
        /// <summary>Os blocos de 64 m de cada lado do mapa.</summary>
        public readonly int BlocosX, BlocosZ;
        readonly Ruido _detalhe, _sorte, _clareira;

        /// <summary>
        /// Uma estrada no chão: os pontos (a cada 8 m, mais os dos nós), a altura do asfalto, se o ponto é ponte, os metros
        /// desde o começo e até o nó mais perto (perto dos nós, as linhas pintadas e o guard-rail somem).
        /// </summary>
        public sealed class Estrada
        {
            public ViaDoMapa Via;
            public Vector2[] Pontos;
            public float[] Alturas;
            public bool[] Ponte;
            public float[] AoLongo, AteONo;
            public float MeiaLargura;
            /// <summary>Uma das estradas da hidrelétrica (a crista presa).</summary>
            public bool DaUsina;
        }

        public readonly List<Estrada> Estradas = new List<Estrada>();
        readonly Dictionary<long, List<(int e, int s)>> _estradas = new Dictionary<long, List<(int, int)>>();
        readonly Dictionary<long, List<(int r, int s)>> _rios = new Dictionary<long, List<(int, int)>>();
        readonly List<List<Vector2>> _lagos = new List<List<Vector2>>(), _ilhas = new List<List<Vector2>>();

        public ChaoDeLakewood(MapaDeLakewood d, RelevoDeLakewood relevo)
        {
            Mapa = d;
            Relevo = relevo;
            BlocosX = Mathf.CeilToInt(d.largura / TamanhoDoBloco);
            BlocosZ = Mathf.CeilToInt(d.comprimento / TamanhoDoBloco);
            _detalhe = new Ruido(d.semente, "detalhe-de-lakewood");
            _sorte = new Ruido(d.semente, "plantas-de-lakewood");
            _clareira = new Ruido(d.semente, "clareiras-de-lakewood");
            foreach (var l in d.lagos) _lagos.Add(l.contorno.Count >= 3 ? MapaDeLakewood.Contorno(l.contorno) : null);
            foreach (var i in d.ilhas) _ilhas.Add(i.contorno.Count >= 3 ? MapaDeLakewood.Contorno(i.contorno) : null);
            for (int r = 0; r < d.rios.Count; r++)
            {
                var linha = relevo.LinhasDosRios[r];
                if (linha == null) continue;
                for (int s = 1; s < linha.Count; s++) Indexar(_rios, linha[s - 1], linha[s], MapaDeLakewood.LarguraDoRio(d.rios[r], 1f) * 0.5f + 48f, (r, s));
            }
            GradeDaAgua();
            Usina = new UsinaDeLakewood(d, relevo);
            // as estradas (Etapa 5): as da usina primeiro (as alturas delas são presas na crista), depois as do mapa; os
            // cruzamentos e as alturas deles saem da rede (ChaoDeLakewood.Estradas.cs)
            var vias = new List<ViaDoMapa>(Usina.Vias);
            int daUsina = vias.Count;
            vias.AddRange(d.estradas);
            // as ruas novas das cidades (Etapa 6) entram na mesma rede: os cruzamentos com as estradas viram nós
            vias.AddRange(ViasDasCidades(d));
            MontarARede(vias, daUsina);
            for (int k = 0; k < Estradas.Count; k++)
            {
                var e = Estradas[k];
                for (int s = 1; s < e.Pontos.Length; s++) Indexar(_estradas, e.Pontos[s - 1], e.Pontos[s], e.MeiaLargura + 40f, (k, s));
            }
            // a casa da partida (o terreiro plano na frente dela, na altura da estrada): ChaoDeLakewood.Partida.cs
            Partida = LugarDaPartida.De(this);
            // os lotes das cidades (Etapa 6: o patamar de cada um na altura da rua da frente): ChaoDeLakewood.Lotes.cs
            MontarOsLotes();
        }

        public static float MeiaLarguraDe(TipoDeVia t) => t switch { TipoDeVia.Rodovia => 6.5f, TipoDeVia.Secundaria => 4.2f, TipoDeVia.Rua => 3.5f, _ => 2.6f };

        /// <summary>A meia largura do asfalto de uma via (a dela, se tiver; senão, a do tipo).</summary>
        public static float MeiaLarguraDe(ViaDoMapa via) => via.largura > 0f ? via.largura * 0.5f : MeiaLarguraDe(via.tipo);

        // ------------------------------------------------------------------ o índice (os segmentos de cada célula de 128 m)

        static long Chave(int i, int j) => ((long)i << 32) ^ (uint)j;

        static void Indexar<T>(Dictionary<long, List<T>> indice, Vector2 a, Vector2 b, float folga, T item)
        {
            int i0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - folga) / CelulaDoIndice), i1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + folga) / CelulaDoIndice);
            int j0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - folga) / CelulaDoIndice), j1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + folga) / CelulaDoIndice);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                long k = Chave(i, j);
                if (!indice.TryGetValue(k, out var l)) indice[k] = l = new List<T>();
                l.Add(item);
            }
        }

        static List<T> Celula<T>(Dictionary<long, List<T>> indice, float x, float z) =>
            indice.TryGetValue(Chave(Mathf.FloorToInt(x / CelulaDoIndice), Mathf.FloorToInt(z / CelulaDoIndice)), out var l) ? l : null;

        // ------------------------------------------------------------------ a água

        // a grade da água dos lagos (12 m): 1 lago, e a altura da água de cada célula (os rios são exatos: RioMaisPerto)
        const float PassoDaAgua = 12f;
        int _nAguaX, _nAguaZ;
        byte[] _tipoDaAgua;
        float[] _nivelDaAgua;

        void GradeDaAgua()
        {
            int nx = _nAguaX = Mathf.CeilToInt(Mapa.largura / PassoDaAgua), nz = _nAguaZ = Mathf.CeilToInt(Mapa.comprimento / PassoDaAgua);
            _tipoDaAgua = new byte[nx * nz];
            _nivelDaAgua = new float[nx * nz];
            float x0 = PassoDaAgua * 0.5f;
            // os lagos: o contorno (alargado em duas células, por causa da linha d'água tremida do relevo) sem as ilhas, e
            // a célula é água onde o chão fica abaixo do nível; a jusante da barragem, não
            var marca = new short[nx * nz];
            for (int l = 0; l < _lagos.Count; l++)
            {
                if (_lagos[l] == null) continue;
                short id = (short)(l + 1);
                Linhas.Varrer(_lagos[l], x0, x0, PassoDaAgua, nx, nz, (j, i0, i1) =>
                {
                    for (int dj = -2; dj <= 2; dj++)
                    {
                        int jj = j + dj;
                        if (jj < 0 || jj >= nz) continue;
                        for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(nx - 1, i1 + 2); i++)
                            if (marca[jj * nx + i] == 0) marca[jj * nx + i] = id;
                    }
                });
            }
            // (as ilhas não são tiradas: o relevo delas fica acima da água, e a linha d'água tremida do relevo decide)
            var represa = Mapa.represa;
            var jusante = Relevo.Jusante;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i;
                if (marca[k] == 0) continue;
                float x = x0 + i * PassoDaAgua, z = x0 + j * PassoDaAgua;
                float nivel = Relevo.NivelDosLagos[marca[k] - 1];
                if (Relevo.Altura(x, z) >= nivel + 0.05f) continue;
                if (represa.Tem)
                {
                    var p = new Vector2(x, z);
                    if (Linhas.DistanciaDoSegmento(p, represa.a, represa.b, out _) < 45f && Vector2.Dot(p - represa.a, jusante) > -5f) continue;
                }
                _tipoDaAgua[k] = 1;
                _nivelDaAgua[k] = nivel;
            }
        }

        /// <summary>A água no ponto (o lago ou o rio) e a altura dela; com a folga, também perto dela.</summary>
        public bool Agua(float x, float z, out float nivel, float folga = 0f)
        {
            if (Usina != null && Usina.Agua(x, z, out nivel, folga)) return true;
            // sem folga, a água do lago só vale onde o relevo fica abaixo dela (a célula de 12 m dizia "água" na praia seca)
            if (AguaDoLagoEm(x, z, out nivel, folga) && (folga > 0f || Relevo.Altura(x, z) < nivel + 0.3f)) return true;
            if (RioMaisPerto(x, z, folga + 0.01f, out var rio))
            {
                nivel = rio.Nivel;
                return true;
            }
            nivel = 0f;
            return false;
        }

        /// <summary>A água de um lago no ponto (pela grade de 12 m) e a altura dela; com a folga, também perto dela.</summary>
        public bool AguaDoLagoEm(float x, float z, out float nivel, float folga = 0f)
        {
            int ci = Mathf.FloorToInt(x / PassoDaAgua), cj = Mathf.FloorToInt(z / PassoDaAgua);
            int r = Mathf.CeilToInt(folga / PassoDaAgua);
            for (int dj = -r; dj <= r; dj++)
            for (int di = -r; di <= r; di++)
            {
                int i = ci + di, j = cj + dj;
                if (i < 0 || j < 0 || i >= _nAguaX || j >= _nAguaZ) continue;
                int k = j * _nAguaX + i;
                if (_tipoDaAgua[k] == 0) continue;
                if (r > 0 && (di * di + dj * dj) * PassoDaAgua * PassoDaAgua > (folga + PassoDaAgua) * (folga + PassoDaAgua)) continue;
                nivel = _nivelDaAgua[k];
                return true;
            }
            nivel = 0f;
            return false;
        }

        /// <summary>A distância do ponto até a água de um lago mais perta (pelas células, até o alcance; fora dele, o alcance) e a altura dela.</summary>
        public float DistanciaDaAgua(float x, float z, float alcance, out float nivel)
        {
            int ci = Mathf.FloorToInt(x / PassoDaAgua), cj = Mathf.FloorToInt(z / PassoDaAgua);
            int r = Mathf.CeilToInt(alcance / PassoDaAgua) + 1;
            float melhor = alcance;
            nivel = 0f;
            for (int dj = -r; dj <= r; dj++)
            for (int di = -r; di <= r; di++)
            {
                int i = ci + di, j = cj + dj;
                if (i < 0 || j < 0 || i >= _nAguaX || j >= _nAguaZ) continue;
                int k = j * _nAguaX + i;
                if (_tipoDaAgua[k] == 0) continue;
                // a distância até a célula (um quadrado de 12 m)
                float cx = (i + 0.5f) * PassoDaAgua, cz = (j + 0.5f) * PassoDaAgua;
                float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - PassoDaAgua * 0.5f), dz = Mathf.Max(0f, Mathf.Abs(z - cz) - PassoDaAgua * 0.5f);
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd < melhor)
                {
                    melhor = dd;
                    nivel = _nivelDaAgua[k];
                }
            }
            return melhor;
        }

        /// <summary>O ponto está no lago (não num rio)?</summary>
        public bool NoLago(float x, float z)
        {
            int i = Mathf.FloorToInt(x / PassoDaAgua), j = Mathf.FloorToInt(z / PassoDaAgua);
            return i >= 0 && j >= 0 && i < _nAguaX && j < _nAguaZ && _tipoDaAgua[j * _nAguaX + i] == 1;
        }

        // ------------------------------------------------------------------ os rios (o canal exato)

        /// <summary>
        /// O rio mais perto de um ponto: a distância ao meio dele, a meia largura ali, a água, o barranco (a altura acima
        /// da água e a rampa dele, do pé na linha d'água até o alto), a fundura no meio do leito, a fração do comprimento e
        /// quantos metros desde a nascente (o UV da água que corre).
        /// </summary>
        public struct RioPerto
        {
            public int Rio;
            public float Dist, Meia, Nivel, Margem, Rampa, Fundura, Fracao, AoLongo;
        }

        /// <summary>O rio mais perto do ponto, até "alcance" metros fora da linha d'água dele (pelo índice dos segmentos).</summary>
        public bool RioMaisPerto(float x, float z, float alcance, out RioPerto r)
        {
            r = default;
            r.Dist = float.MaxValue;
            var l = Celula(_rios, x, z);
            if (l == null) return false;
            var p = new Vector2(x, z);
            float melhor = float.MaxValue;
            foreach (var (ri, s) in l)
            {
                var linha = Relevo.LinhasDosRios[ri];
                float dd = Linhas.DistanciaDoSegmento(p, linha[s - 1], linha[s], out float t);
                float f = Mathf.Lerp(Relevo.FracoesDosRios[ri][s - 1], Relevo.FracoesDosRios[ri][s], t);
                float meia = MapaDeLakewood.LarguraDoRio(Mapa.rios[ri], f) * 0.5f;
                if (dd - meia >= melhor) continue;
                melhor = dd - meia;
                r.Rio = ri;
                r.Dist = dd;
                r.Meia = meia;
                r.Fracao = f;
                r.Nivel = Mathf.Lerp(Relevo.NiveisDosRios[ri][s - 1], Relevo.NiveisDosRios[ri][s], t);
                r.AoLongo = f * Relevo.ComprimentosDosRios[ri];
            }
            if (melhor >= alcance) return false;
            // o barranco e o leito mudam ao longo do rio (o ruído pelo comprimento), e o barranco baixa chegando no lago
            float larg = r.Meia * 2f;
            float n1 = _detalhe.Amostra(r.AoLongo / 37f + 5.5f, r.Rio * 3.1f), n2 = _detalhe.Amostra(r.AoLongo / 23f - 2.2f, r.Rio * 5.3f + 7.7f);
            float cai = Relevo.DesembocaNoLago[r.Rio] ? Mathf.Lerp(0.3f, 1f, Geo.Suave(1f, 0.95f, r.Fracao)) : 1f;
            r.Margem = MapaDeLakewood.MargemDoRio(larg) * (1f + 0.25f * n1) * cai;
            r.Rampa = r.Margem * (2.6f + 0.8f * n2);
            r.Fundura = MapaDeLakewood.FunduraDoRio(larg) * (1f + 0.2f * n1);
            return true;
        }

        // ------------------------------------------------------------------ o chão

        /// <summary>
        /// A altura do chão de perto: o relevo, o detalhe, o canal dos rios (o leito e os barrancos, na largura de verdade:
        /// o relevo de 20 m só tem o vale) e o chão das estradas. "passo" é a distância entre os pontos da malha que vai
        /// usar a altura (0, exata): nos terrenos de passo largo, o canal é alargado para pelo menos um ponto cair nele.
        /// </summary>
        public float Altura(float x, float z, float passo = 0f)
        {
            float h = Relevo.Altura(x, z);
            // perto dos rios, sem o detalhe (ele entortava o barranco)
            bool temRio = RioMaisPerto(x, z, 40f, out var rio);
            float perto = temRio ? Geo.Suave(rio.Meia + rio.Rampa, rio.Meia + rio.Rampa + 25f, rio.Dist) : 1f;
            // na beira do lago, sem o detalhe também (ele cavava poças secas abaixo da água, e quem andava nelas "caía na água")
            if (AguaDoLagoEm(x, z, out float nivelDoLago, 60f)) perto *= Geo.Suave(0.6f, 2.5f, Mathf.Abs(h - nivelDoLago));
            h += perto * (1.1f * _detalhe.Fbm(x / 45f, z / 45f, 2) + 0.3f * _detalhe.Amostra(x / 11f, z / 11f));
            if (temRio) h = CanalDoRio(x, z, h, rio, passo);
            // os pátios da usina (os prédios, a subestação, o corte da crista e do canal de fuga); as estradas vêm por cima
            // (o perfil delas já é o do chão com os pátios: perto de um pátio, o asfalto manda)
            if (Usina != null) h = Usina.Chao(x, z, h);
            // o terreiro da casa da partida (plano, na altura da estrada na frente dele)
            if (Partida != null) h = Partida.Chao(x, z, h);
            // os patamares dos lotes das cidades (na altura da rua da frente, com o talude até o chão de fora)
            h = ChaoDosLotes(x, z, h);
            // as estradas: o asfalto, o acostamento e o corte ou o aterro (na ponte, o chão fica como está); e onde duas
            // estradas se encostam (perto dos nós), o chão fica embaixo da mais baixa. Nos terrenos de passo largo (os blocos
            // longe, sem a neblina: 8 e 16 m), nenhum ponto perto da pista fica acima dela: os três cantos de um triângulo que
            // passa por cima da pista ficam embaixo dela (com o talude, o triângulo entre um ponto do talude e um da pista
            // cobria o asfalto)
            float folga = passo > 4f ? passo * 1.42f : 0f;
            if (EstradaEPiso(x, z, Acostamento + 34f, folga, out float de, out float hr, out bool ponte, out float meiaEst, out float piso))
            {
                if (!ponte)
                {
                    float talude = Mathf.Clamp(Mathf.Abs(h - hr) * 1.8f, 3f, 34f);
                    float borda = meiaEst + Acostamento;
                    if (de <= borda) h = hr - 0.08f;
                    else if (de < borda + talude) h = Mathf.Lerp(hr - 0.08f, h, Geo.Suave(borda, borda + talude, de));
                }
                if (piso < float.MaxValue) h = Mathf.Min(h, piso - 0.08f);
            }
            // dentro de um lote ocupado, o patamar manda (os lotes colados na rua pegavam o talude dela na beira da frente;
            // nenhum lote encosta no asfalto)
            return NoPatamar(x, z, h);
        }

        // o canal do rio no ponto: o leito abaixo da água (fundo no meio, em parábola), o barranco subindo do pé (um pouco
        // abaixo da linha d'água) até o alto, e o chão logo depois nunca abaixo da água; dentro de um lago, só o leito
        float CanalDoRio(float x, float z, float h, in RioPerto r, float passo)
        {
            float meia = Mathf.Max(r.Meia, passo * 0.75f), rampa = Mathf.Max(r.Rampa, passo * 0.6f);
            if (r.Dist <= meia)
            {
                float u = r.Dist / meia;
                float fundo = r.Nivel - 0.12f - r.Fundura * (1f - u * u) * (1f + 0.15f * _detalhe.Amostra(x / 6f, z / 6f));
                return Mathf.Min(h, fundo);
            }
            if (NoLago(x, z)) return h;
            float alto = r.Nivel + r.Margem * 0.6f;
            if (r.Dist <= meia + rampa)
            {
                float u = Geo.Suave(meia, meia + rampa, r.Dist);
                return Mathf.Lerp(r.Nivel - 0.25f, Mathf.Max(h, alto), u);
            }
            if (r.Dist <= meia + rampa + 20f) return Mathf.Max(h, Mathf.Lerp(alto, r.Nivel + 0.3f, Geo.Suave(meia + rampa, meia + rampa + 20f, r.Dist)));
            return h;
        }

        // os pesos de cada cobertura (na ordem de Cobertura: a mata, o campo, a lavoura, a rocha, a água e o urbano) nas 4
        // texturas do chão: R a grama seca, G a terra, B o cascalho, A as folhas
        // (09/11/2026: a grama é verde e as folhas são o chão da mata; a classe "água" da imagem, em terra, vale como a mata)
        // (10/11/2026, a pedido: "cobrir o mapa com uma textura mais verde e a terra ficar em volta dos rios e do lago
        // somente": a grama em tudo, a mata com umas manchas de folhas, a rocha com o cascalho; a terra e o cascalho da
        // praia e dos barrancos dos rios, mais embaixo, continuam. A rocha das encostas em pé é a do shader.)
        static readonly float[] PesoR = { 0.62f, 0.90f, 0.82f, 0.55f, 0.62f, 0.82f };
        static readonly float[] PesoG = { 0.03f, 0.04f, 0.10f, 0.08f, 0.03f, 0.08f };
        static readonly float[] PesoB = { 0.00f, 0.00f, 0.00f, 0.37f, 0.00f, 0.10f };
        static readonly float[] PesoA = { 0.35f, 0.06f, 0.08f, 0.00f, 0.35f, 0.00f };

        /// <summary>
        /// Os pesos das texturas do chão (R grama seca, G terra, B cascalho, A folhas): a mistura das coberturas em volta
        /// do ponto deformado pelo ruído (a beira da mata não sai em quadras), e o cascalho da estrada.
        /// </summary>
        public Color32 Pesos(float x, float z, float h)
        {
            var p = MapaDeLakewood.Deformar(_sorte, x, z);
            Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
            Mapa.Fracoes(p.x, p.y, f);
            float r = 0f, g = 0f, b = 0f, a = 0f;
            for (int k = 0; k < f.Length; k++)
            {
                r += f[k] * PesoR[k];
                g += f[k] * PesoG[k];
                b += f[k] * PesoB[k];
                a += f[k] * PesoA[k];
            }
            // as clareiras da mata (a floresta nova, 09/11/2026): o chão aberto com os pesos do campo (a grama), como na cena
            // de floresta; sem isso, a clareira ficava com o chão de agulhas escuro da mata
            float naMata = f[(int)Cobertura.Mata] + f[(int)Cobertura.Agua];
            if (naMata > 0.01f)
            {
                float aberto = naMata * Clareira(x, z);
                int mata = (int)Cobertura.Mata, campo = (int)Cobertura.Campo;
                r += aberto * (PesoR[campo] - PesoR[mata]);
                g += aberto * (PesoG[campo] - PesoG[mata]);
                b += aberto * (PesoB[campo] - PesoB[mata]);
                a += aberto * (PesoA[campo] - PesoA[mata]);
            }
            float n = _sorte.Amostra(x / 60f, z / 60f);
            r += 0.15f * n;
            a -= 0.15f * n;
            // a beira da água: a praia de terra e cascalho numa faixa de uns 6 a 20 m da água (torta pelo ruído), só onde o
            // chão está baixo (não no alto de uma encosta), o cascalho e a pedra nas margens em pé, e o lodo embaixo d'água
            const float AlcanceDaPraia = 26f;
            float dAgua = DistanciaDaAgua(x, z, AlcanceDaPraia, out float nivelDaAgua);
            if (dAgua < AlcanceDaPraia)
            {
                float acima = h - nivelDaAgua;
                float w;
                if (acima < 0f) w = 1f;
                else
                {
                    float faixa = dAgua + 5f * _sorte.Amostra(x / 19f, z / 19f);
                    w = (1f - Geo.Suave(6f, 20f, faixa)) * (1f - Geo.Suave(1.6f, 4f, acima));
                }
                if (w > 0f)
                {
                    float pedra = Geo.Suave(0.22f, 0.45f, Relevo.Inclinacao(x, z));
                    float pg = Mathf.Lerp(0.5f, 0.15f, pedra), pb = Mathf.Lerp(0.42f, 0.85f, pedra);
                    if (acima < 0f) { pg = 0.7f; pb = 0.3f; }
                    r = Mathf.Lerp(r, 0.08f, w);
                    g = Mathf.Lerp(g, pg, w);
                    b = Mathf.Lerp(b, pb, w);
                    a = Mathf.Lerp(a, 0f, w);
                }
            }
            // as margens dos rios: o lodo no leito, a terra e o cascalho no barranco (mais pedra onde ele é em pé) e uma
            // faixa de terra batida no alto dele, torta pelo ruído
            if (RioMaisPerto(x, z, 16f, out var rio))
            {
                float fora = rio.Dist - rio.Meia;
                float w = 1f - Geo.Suave(rio.Rampa + 1f, rio.Rampa + 8f + 5f * _sorte.Amostra01(x / 17f, z / 17f), fora);
                if (w > 0f)
                {
                    float pedra = Geo.Suave(2.9f, 2.0f, rio.Rampa / Mathf.Max(rio.Margem, 0.1f));
                    float pg = fora < 0f ? 0.7f : Mathf.Lerp(0.55f, 0.3f, pedra), pb = fora < 0f ? 0.3f : Mathf.Lerp(0.35f, 0.65f, pedra);
                    r = Mathf.Lerp(r, 0.06f, w);
                    g = Mathf.Lerp(g, pg, w);
                    b = Mathf.Lerp(b, pb, w);
                    a = Mathf.Lerp(a, 0.04f, w);
                }
            }
            // o cascalho e a terra batida dos pátios da usina
            float cas = Usina != null ? Usina.Cascalho(x, z) : 0f;
            if (cas > 0f)
            {
                float n2 = 0.5f + 0.5f * _sorte.Amostra(x / 7f, z / 7f);
                r = Mathf.Lerp(r, 0.12f + 0.2f * n2, cas);
                g = Mathf.Lerp(g, 0.25f, cas);
                b = Mathf.Lerp(b, 0.75f - 0.2f * n2, cas);
                a = Mathf.Lerp(a, 0f, cas);
            }
            // o terreiro da casa da partida: o cascalho batido, com um pouco de terra
            if (Partida != null)
            {
                float dt = Partida.DistanciaDoTerreiro(x, z);
                if (dt < 3f)
                {
                    float w = 1f - Geo.Suave(0.5f, 3f, dt);
                    float n2 = 0.5f + 0.5f * _sorte.Amostra(x / 6f, z / 6f);
                    r = Mathf.Lerp(r, 0.1f + 0.15f * n2, w);
                    g = Mathf.Lerp(g, 0.3f, w);
                    b = Mathf.Lerp(b, 0.7f - 0.15f * n2, w);
                    a = Mathf.Lerp(a, 0f, w);
                }
            }
            // o chão dos lotes das cidades: a grama do quintal com a terra batida em manchas, sem as folhas
            PesosDosLotes(x, z, ref r, ref g, ref b, ref a);
            if (EstradaPerto(x, z, Acostamento + 2f, out float de, out _, out bool ponte, out float meia) && !ponte && de < meia + Acostamento + 1f)
            {
                r = 0.05f; g = 0.1f; b = 0.85f; a = 0f;
            }
            float s = Mathf.Max(0.01f, r + g + b + a);
            return new Color32((byte)(Mathf.Clamp01(r / s) * 255), (byte)(Mathf.Clamp01(g / s) * 255), (byte)(Mathf.Clamp01(b / s) * 255), (byte)(Mathf.Clamp01(a / s) * 255));
        }

        // ------------------------------------------------------------------ o terreno de um bloco

        public static Vector2 CantoDoBloco(int bx, int bz) => new Vector2(bx * TamanhoDoBloco, bz * TamanhoDoBloco);

        /// <summary>
        /// O terreno de um bloco de 64 m, low-poly de faces planas, com os pesos na cor do vértice e as saias de 3 m nas
        /// beiradas (escondem as frestas entre blocos de detalhes diferentes). O passo é a distância entre os pontos (2 a 16 m).
        /// </summary>
        public MalhaDoBloco Terreno(int bx, int bz, float passo)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(TamanhoDoBloco / passo));
            var canto = CantoDoBloco(bx, bz);
            int lado = n + 1;
            var alt = new float[lado * lado];
            var pesos = new Color32[lado * lado];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = canto.x + i * passo, z = canto.y + j * passo;
                float h = Altura(x, z, passo);
                alt[j * lado + i] = h;
                pesos[j * lado + i] = Pesos(x, z, h);
            }
            var v = new List<Vector3>(n * n * 6 + n * 48);
            var c = new List<Color32>(v.Capacity);
            Vector3 P(int i, int j) => new Vector3(canto.x + i * passo, alt[j * lado + i], canto.y + j * passo);
            Color32 C(int i, int j) => pesos[j * lado + i];
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color32 ca, Color32 cb, Color32 cd)
            {
                v.Add(a); v.Add(b); v.Add(d);
                c.Add(ca); c.Add(cb); c.Add(cd);
            }
            // os buracos (o subsolo do hospital), só no terreno de perto: a célula com um canto dentro sai
            bool buracos = passo <= 2.01f && BlocoComBuraco(bx, bz);
            bool Furada(int i, int j)
            {
                for (int dj = 0; dj <= 1; dj++)
                for (int di = 0; di <= 1; di++)
                    if (NoBuraco(canto.x + (i + di) * passo, canto.y + (j + dj) * passo, 0.05f)) return true;
                return false;
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                if (buracos && Furada(i, j)) continue;
                if (((i + j) & 1) == 0)
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j + 1), C(i, j), C(i, j + 1), C(i + 1, j + 1));
                    Tri(P(i, j), P(i + 1, j + 1), P(i + 1, j), C(i, j), C(i + 1, j + 1), C(i + 1, j));
                }
                else
                {
                    Tri(P(i, j), P(i, j + 1), P(i + 1, j), C(i, j), C(i, j + 1), C(i + 1, j));
                    Tri(P(i + 1, j), P(i, j + 1), P(i + 1, j + 1), C(i + 1, j), C(i, j + 1), C(i + 1, j + 1));
                }
            }
            void Saia(int ia, int ja, int ib, int jb)
            {
                Vector3 a = P(ia, ja), b = P(ib, jb), a2 = a + Vector3.down * 3f, b2 = b + Vector3.down * 3f;
                Color32 ca = C(ia, ja), cb = C(ib, jb);
                Tri(a, b, b2, ca, cb, cb);
                Tri(a, b2, a2, ca, cb, ca);
                Tri(a, b2, b, ca, cb, cb);
                Tri(a, a2, b2, ca, ca, cb);
            }
            for (int q = 0; q < n; q++)
            {
                Saia(q, 0, q + 1, 0);
                Saia(q, n, q + 1, n);
                Saia(0, q, 0, q + 1);
                Saia(n, q, n, q + 1);
            }
            return Fechar(v, c, null);
        }

        // as normais das faces planas, os triângulos em ordem e a caixa
        static MalhaDoBloco Fechar(List<Vector3> v, List<Color32> c, List<Vector2> uv)
        {
            var vertices = v.ToArray();
            var normais = new Vector3[vertices.Length];
            var tri = new int[vertices.Length];
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            for (int k = 0; k < vertices.Length; k += 3)
            {
                var nrm = Vector3.Cross(vertices[k + 1] - vertices[k], vertices[k + 2] - vertices[k]).normalized;
                normais[k] = normais[k + 1] = normais[k + 2] = nrm;
            }
            for (int k = 0; k < vertices.Length; k++)
            {
                tri[k] = k;
                min = Vector3.Min(min, vertices[k]);
                max = Vector3.Max(max, vertices[k]);
            }
            var m = new MalhaDoBloco { Vertices = vertices, Normais = normais, Cores = c?.ToArray(), Triangulos = tri };
            m.Uvs = uv?.ToArray();
            m.Limites = vertices.Length > 0 ? new Bounds((min + max) * 0.5f, max - min) : new Bounds();
            return m;
        }

        // ------------------------------------------------------------------ o relevo de longe (um setor)

        /// <summary>
        /// O relevo de longe de um setor (sempre desenhado, por baixo dos blocos de perto): "passo" metros entre os pontos,
        /// com os pesos das texturas do chão na cor do vértice (as mesmas texturas do perto, pelo ChaoDeLonge.shader) e a
        /// copa da mata no UV (a altura e a fração de mata: o shader a levanta e a pinta longe da câmera), tudo afundado
        /// 6 m e, onde o chão de perto desce mais (os cortes das estradas, a usina, os rios), mais fundo
        /// (<see cref="AlturaDeLonge"/>, ChaoDeLakewood.Longe.cs): o terreno de perto fica sempre por cima dele.
        /// </summary>
        public MalhaDoBloco Longe(int sx, int sz, float passo = PassoDeLonge)
        {
            var limites = Mapa.LimitesDoSetor(sx, sz);
            int n = Mathf.Max(1, Mathf.RoundToInt(limites.width / passo)), m = Mathf.Max(1, Mathf.RoundToInt(limites.height / passo));
            float px = limites.width / n, pz = limites.height / m;
            var canto = new Vector2(limites.x, limites.y);
            int lado = n + 1;
            // os vértices são os da grade do mapa todo quando o setor cai nela (sempre, com os setores de 3 × 2,5 km)
            int gi = Mathf.RoundToInt(canto.x / passo), gj = Mathf.RoundToInt(canto.y / passo);
            bool naGrade = Mathf.Abs(px - passo) < 0.01f && Mathf.Abs(pz - passo) < 0.01f
                           && Mathf.Abs(gi * passo - canto.x) < 0.01f && Mathf.Abs(gj * passo - canto.y) < 0.01f;
            var alt = new float[lado * (m + 1)];
            var cor = new Color32[lado * (m + 1)];
            var copa = new Vector2[lado * (m + 1)];
            Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
            for (int j = 0; j <= m; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = canto.x + i * px, z = canto.y + j * pz;
                float h = Relevo.Altura(x, z);
                var pd = MapaDeLakewood.Deformar(_sorte, x, z);
                Mapa.Fracoes(pd.x, pd.y, f);
                bool agua = Agua(x, z, out _, 6f);
                // a malha fica 6 m abaixo do chão (escondida sob os blocos de perto), com os pesos das texturas do chão de
                // perto na cor (as mesmas texturas: sem emenda) e a copa da mata no UV (a altura, até 11 m, e a fração de
                // mata), que o ChaoDeLonge.shader só levanta longe da câmera
                alt[j * lado + i] = naGrade ? VerticeDeLonge(passo, gi + i, gj + j) : h - FundoDoLonge;
                cor[j * lado + i] = Pesos(x, z, h);
                float mata = agua ? 0f : Mathf.Clamp01(f[(int)Cobertura.Mata] + f[(int)Cobertura.Agua]);
                copa[j * lado + i] = new Vector2(mata * 11f / 16f, mata);
            }
            var v = new List<Vector3>(n * m * 6);
            var c = new List<Color32>(n * m * 6);
            var uv = new List<Vector2>(n * m * 6);
            for (int j = 0; j < m; j++)
            for (int i = 0; i < n; i++)
            {
                Vector3 P(int a, int b) => new Vector3(canto.x + a * px, alt[b * lado + a], canto.y + b * pz);
                Color32 C(int a, int b) => cor[b * lado + a];
                Vector2 U(int a, int b) => copa[b * lado + a];
                v.Add(P(i, j)); v.Add(P(i, j + 1)); v.Add(P(i + 1, j + 1));
                c.Add(C(i, j)); c.Add(C(i, j + 1)); c.Add(C(i + 1, j + 1));
                uv.Add(U(i, j)); uv.Add(U(i, j + 1)); uv.Add(U(i + 1, j + 1));
                v.Add(P(i, j)); v.Add(P(i + 1, j + 1)); v.Add(P(i + 1, j));
                c.Add(C(i, j)); c.Add(C(i + 1, j + 1)); c.Add(C(i + 1, j));
                uv.Add(U(i, j)); uv.Add(U(i + 1, j + 1)); uv.Add(U(i + 1, j));
            }
            return Fechar(v, c, uv);
        }

        // a cor do relevo de longe num ponto: a mistura das coberturas (as frações já calculadas), com a textura das copas,
        // as lavouras em retalhos, a rocha pela cobertura e pela altura e a neve nos picos; sai em linear (a cor do vértice)
        Color32 CorDeLonge(float x, float z, float h, ReadOnlySpan<float> f)
        {
            float r1 = _sorte.Fbm(x / 700f, z / 700f, 2);
            float r3 = _sorte.Amostra(x / 55f, z / 55f);
            var mata = Color.Lerp(PintorDeLakewood.MataEscura, PintorDeLakewood.MataClara, Mathf.Clamp01(0.5f + 0.7f * r1)) * (1f + 0.1f * r3);
            var campo = Color.Lerp(PintorDeLakewood.CampoCor, PintorDeLakewood.CampoClaro, Mathf.Clamp01(0.5f + 0.8f * r1)) * (1f + 0.05f * r3);
            ulong hs = Sorteio.Hash(Mapa.semente, Mathf.FloorToInt(x / 300f), Mathf.FloorToInt(z / 220f), 43);
            var lavoura = (Sorteio.FracaoDoHash(hs) > 0.25f ? PintorDeLakewood.Campos[(int)(hs % 4)] : PintorDeLakewood.CampoCor) * (1f + 0.04f * r3);
            var rocha = Color.Lerp(mata, PintorDeLakewood.Rocha, 0.75f) * (1f + 0.06f * r3);
            var margem = Color.Lerp(mata, PintorDeLakewood.MargemRasa, 0.6f);
            var c = mata * f[(int)Cobertura.Mata] + campo * f[(int)Cobertura.Campo] + lavoura * f[(int)Cobertura.Lavoura]
                  + rocha * f[(int)Cobertura.Rocha] + margem * f[(int)Cobertura.Agua] + PintorDeLakewood.Urbano * f[(int)Cobertura.Urbano];
            // a rocha nua pela altura e a neve nos picos
            c = Color.Lerp(c, PintorDeLakewood.Rocha, Geo.Suave(420f, 560f, h + r1 * 60f));
            c = Color.Lerp(c, PintorDeLakewood.Neve, Geo.Suave(615f, 690f, h + 40f * _sorte.Fbm(x / 400f + 50f, z / 400f, 2)));
            c.a = 1f;
            return c.linear;
        }

        // ------------------------------------------------------------------ a água

        /// <summary>
        /// Onde a água do lago para na barragem: a linha, a jusante do eixo da crista (m), por dentro da chapa das
        /// comportas fechadas (a frente dela fica em -1,68 na altura da água); nos blocos de concreto, ela fica dentro
        /// deles (a face do lago em -6, a de jusante em +9,2).
        /// </summary>
        public const float CorteDaAguaNaBarragem = -1.55f;

        /// <summary>
        /// A distância (com o sinal) do ponto até a linha onde a água do lago para na barragem: positiva a jusante, onde
        /// não há água do lago. Só vale na faixa da barragem (do começo ao fim da crista e 150 m além das pontas, até
        /// 400 m a jusante); fora dela, nada é cortado e devolve false.
        /// </summary>
        public bool CorteNaBarragem(float x, float z, out float lado)
        {
            lado = -1f;
            var represa = Mapa.represa;
            if (!represa.Tem) return false;
            var q = new Vector2(x, z) - represa.a;
            float u = Vector2.Dot(q, represa.Direcao), v = Vector2.Dot(q, Relevo.Jusante);
            if (u < -150f || u > represa.Comprimento + 150f || v < -150f || v > 400f) return false;
            lado = v - CorteDaAguaNaBarragem;
            return true;
        }

        // a distância (com o sinal) até a linha do corte, sem a faixa (positiva a jusante)
        float LadoDaBarragem(float x, float z) => Vector2.Dot(new Vector2(x, z) - Mapa.represa.a, Relevo.Jusante) - CorteDaAguaNaBarragem;

        /// <summary>
        /// A água de um lago: uma grade de 24 m na altura da água, nas células dentro do contorno e duas além (a margem fica
        /// acima da água: o chão esconde a beirada), cortada na barragem (a jusante, o chão do vale fica 40 m abaixo da
        /// água e não a esconde). O R da cor é a fundura (de 0 a 3 m).
        /// </summary>
        public MalhaDoBloco AguaDoLago(int lago)
        {
            const float passo = 24f;
            if (_lagos[lago] == null) return null;
            int nx = Mathf.CeilToInt(Mapa.largura / passo), nz = Mathf.CeilToInt(Mapa.comprimento / passo);
            float x0 = passo * 0.5f;
            var marca = new bool[nx * nz];
            float nivel = Relevo.NivelDosLagos[lago];
            Linhas.Varrer(_lagos[lago], x0, x0, passo, nx, nz, (j, i0, i1) =>
            {
                for (int i = i0; i <= i1; i++) marca[j * nx + i] = true;
            });
            var dilatada = new bool[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                if (!marca[j * nx + i]) continue;
                for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a >= 0 && b >= 0 && a < nx && b < nz) dilatada[b * nx + a] = true;
                }
            }
            var v = new List<Vector3>();
            var c = new List<Color32>();
            var uv = new List<Vector2>();
            Color32 Fundura(float x, float z) => new Color32((byte)(Mathf.Clamp01((nivel - Relevo.Altura(x, z)) / 3f) * 255), 0, 0, 255);
            // nas células da faixa da barragem, cada triângulo é cortado na linha dela (fica o lado do lago)
            var poligono = new List<Vector3>(4);
            float Lado(Vector3 p) => LadoDaBarragem(p.x, p.z);
            void Cortado(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                poligono.Clear();
                Vector3 antes = p2;
                float sa = Lado(p2);
                for (int k = 0; k < 3; k++)
                {
                    var p = k == 0 ? p0 : k == 1 ? p1 : p2;
                    float sp = Lado(p);
                    if ((sa <= 0f) != (sp <= 0f)) poligono.Add(Vector3.Lerp(antes, p, sa / (sa - sp)));
                    if (sp <= 0f) poligono.Add(p);
                    antes = p;
                    sa = sp;
                }
                for (int k = 1; k + 1 < poligono.Count; k++)
                {
                    var q0 = poligono[0];
                    var q1 = poligono[k];
                    var q2 = poligono[k + 1];
                    v.Add(q0); v.Add(q1); v.Add(q2);
                    c.Add(Fundura(q0.x, q0.z)); c.Add(Fundura(q1.x, q1.z)); c.Add(Fundura(q2.x, q2.z));
                    uv.Add(new Vector2(q0.x, q0.z)); uv.Add(new Vector2(q1.x, q1.z)); uv.Add(new Vector2(q2.x, q2.z));
                }
            }
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                if (!dilatada[j * nx + i]) continue;
                float xa = i * passo, za = j * passo, xb = xa + passo, zb = za + passo;
                Vector3 a0 = new Vector3(xa, nivel, za), a1 = new Vector3(xb, nivel, za), a2 = new Vector3(xb, nivel, zb), a3 = new Vector3(xa, nivel, zb);
                // a célula encosta na faixa da barragem (com a meia diagonal dela de folga)?
                if (CorteNaBarragem(xa + passo * 0.5f, za + passo * 0.5f, out float lado) && lado > -passo * 0.75f)
                {
                    if (lado > passo * 0.75f) continue;
                    Cortado(a0, a3, a2);
                    Cortado(a0, a2, a1);
                    continue;
                }
                Color32 c0 = Fundura(xa, za), c1 = Fundura(xb, za), c2 = Fundura(xb, zb), c3 = Fundura(xa, zb);
                v.Add(a0); v.Add(a3); v.Add(a2); c.Add(c0); c.Add(c3); c.Add(c2);
                v.Add(a0); v.Add(a2); v.Add(a1); c.Add(c0); c.Add(c2); c.Add(c1);
                uv.Add(new Vector2(xa, za)); uv.Add(new Vector2(xa, zb)); uv.Add(new Vector2(xb, zb));
                uv.Add(new Vector2(xa, za)); uv.Add(new Vector2(xb, zb)); uv.Add(new Vector2(xb, za));
            }
            return v.Count == 0 ? null : Fechar(v, c, uv);
        }

        /// <summary>
        /// A água de um rio: a faixa ao longo da linha (com os meandros), na largura dele mais 1,2 m de cada lado (a sobra
        /// fica escondida sob o barranco), descendo com a água. O UV é (os metros do meio para o lado, os metros desde a
        /// nascente), e o G da cor diz que a água corre (o shader arrasta as ondinhas pelo UV).
        /// </summary>
        public MalhaDoBloco AguaDoRio(int rio)
        {
            var linha = Relevo.LinhasDosRios[rio];
            if (linha == null) return null;
            var f = Relevo.FracoesDosRios[rio];
            var niv = Relevo.NiveisDosRios[rio];
            float comp = Relevo.ComprimentosDosRios[rio];
            var v = new List<Vector3>();
            var c = new List<Color32>();
            var uv = new List<Vector2>();
            const float sobra = 1.2f;
            Vector3 Lado(int i, float sinal, out float meia)
            {
                var dir = (linha[Mathf.Min(i + 1, linha.Count - 1)] - linha[Mathf.Max(i - 1, 0)]).normalized;
                var lado = new Vector2(-dir.y, dir.x);
                meia = MapaDeLakewood.LarguraDoRio(Mapa.rios[rio], f[i]) * 0.5f + sobra;
                var p = linha[i] + lado * meia * sinal;
                return new Vector3(p.x, niv[i], p.y);
            }
            var cor = new Color32(255, 255, 0, 255);
            for (int i = 0; i + 1 < linha.Count; i++)
            {
                // dentro do lago, a água é a dele (as duas na mesma altura brigavam)
                if (NoLago(linha[i].x, linha[i].y) && NoLago(linha[i + 1].x, linha[i + 1].y)) continue;
                Vector3 a = Lado(i, 1f, out float ma), b = Lado(i, -1f, out _), d = Lado(i + 1, 1f, out float md), e = Lado(i + 1, -1f, out _);
                float sa = f[i] * comp, sd = f[i + 1] * comp;
                Vector2 ua = new Vector2(ma, sa), ub = new Vector2(-ma, sa), ud = new Vector2(md, sd), ue = new Vector2(-md, sd);
                v.Add(a); v.Add(d); v.Add(e); c.Add(cor); c.Add(cor); c.Add(cor); uv.Add(ua); uv.Add(ud); uv.Add(ue);
                v.Add(a); v.Add(e); v.Add(b); c.Add(cor); c.Add(cor); c.Add(cor); uv.Add(ua); uv.Add(ue); uv.Add(ub);
            }
            return v.Count == 0 ? null : Fechar(v, c, uv);
        }

        // (a barragem de verdade, com o resto da hidrelétrica, é a UsinaDeLakewood: Etapa 4, 10/11/2026)

        // ------------------------------------------------------------------ a mata (a floresta nova)

        /// <summary>As árvores por 100 m² na mata fechada (a da cena de floresta; a mata de antes tinha umas 2,7).</summary>
        public const float DensidadeDaMata = 7f;

        // as clareiras: o ruído de Perlin em 3 oitavas (manchas de uns 50 m) esticado para a mesma parte do chão aberta da
        // cena de floresta (o ruído de valores de lá espalha mais), e o mesmo degrau suave
        const float EsticarAsClareiras = 1.6f, LimiarDasClareiras = 0.71f;

        /// <summary>
        /// O meio do pátio das bases militares fica sem árvores (12/11/2026, a base dos militares, a parte 5 do "O jogo do
        /// mundo sorteado no Lakewood"): um círculo de 36 m em volta do ponto do lugar (a cerca tem 40 × 40 m).
        /// </summary>
        public const float RaioDoPatioDaBase = 36f;

        Vector2[] _bases;

        /// <summary>O ponto fica no pátio de uma base militar (sem árvores).</summary>
        public bool NaBaseMilitar(float x, float z)
        {
            if (_bases == null)
            {
                var l = new List<Vector2>();
                if (Mapa.lugares != null)
                    foreach (var lugar in Mapa.lugares) if (lugar.tipo == TipoDeLugarDoMapa.BaseMilitar) l.Add(lugar.ponto);
                _bases = l.ToArray();
            }
            foreach (var b in _bases)
                if ((b.x - x) * (b.x - x) + (b.y - z) * (b.y - z) < RaioDoPatioDaBase * RaioDoPatioDaBase) return true;
            return false;
        }

        /// <summary>A clareira (0 mata fechada, 1 clareira aberta) num ponto: as manchas de uns 50 m da cena de floresta.</summary>
        public float Clareira(float x, float z)
        {
            float c = 0.5f + 0.5f * EsticarAsClareiras * _clareira.Fbm(x * 0.018f, z * 0.018f, 3);
            return Geo.Suave(LimiarDasClareiras - 0.12f, LimiarDasClareiras + 0.1f, c);
        }

        /// <summary>
        /// Os pinheiros de um bloco (a floresta nova, 09/11/2026, a pedido: "essa configuração da floresta vai substituir toda
        /// a floresta atual do mapa lakewood"; o Plano_Floresta.md, "A floresta nova no Lakewood"): só os pinheiros leves (o
        /// alto, o médio e o pequeno, 5 variações cada: as espécies <see cref="Especie.PinheiroAlto1"/> em diante, pela ordem
        /// de <see cref="Floresta.PinheiroGerado"/>), numa grade sacudida de <see cref="DensidadeDaMata"/> por 100 m², com as
        /// clareiras e os tamanhos da cena de floresta (os pequenos e os médios na beira das clareiras); no campo, na lavoura e
        /// na cidade, uns soltos, quase todos pequenos e médios; na rocha, mais ralos e, no alto, só os menores. Fora da água,
        /// da praia, dos barrancos dos rios, das estradas (9 m) e dos paredões. A mata de antes (as folhosas, os arbustos, as
        /// samambaias, os tocos, os troncos caídos, as pedras, o capim alto, a taboa e a grama baixa) saiu, a pedido: a grama
        /// é a <see cref="GramaDensa"/> (<see cref="Grama"/>). "chao" ficou só pela assinatura de antes (não muda nada).
        /// </summary>
        public void Plantas(int bx, int bz, List<PlantaDaFloresta> saida, bool chao = false)
        {
            var canto = CantoDoBloco(bx, bz);
            var sorte = new System.Random(unchecked((int)Sorteio.Hash(Mapa.semente, bx, bz, 78)));
            float Entre(float a, float b) => a + (float)sorte.NextDouble() * (b - a);
            int n = Mathf.RoundToInt(TamanhoDoBloco / Mathf.Sqrt(100f / DensidadeDaMata));
            float passo = TamanhoDoBloco / n;
            Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = canto.x + (i + 0.5f + Entre(-0.42f, 0.42f)) * passo, z = canto.y + (j + 0.5f + Entre(-0.42f, 0.42f)) * passo;
                float u = (float)sorte.NextDouble(), r = (float)sorte.NextDouble(), s = (float)sorte.NextDouble(), e01 = (float)sorte.NextDouble();
                int variacao = sorte.Next(5);
                float giro = Entre(0f, 360f);
                // a cobertura do ponto, sorteada pela mistura das células em volta do ponto deformado (a beira da mata fica
                // irregular, com árvores soltas no campo); a "água" lida da imagem em terra firme é mata
                var pd = MapaDeLakewood.Deformar(_sorte, x, z);
                Mapa.Fracoes(pd.x, pd.y, f);
                var cob = MapaDeLakewood.Sortear(f, u);
                if (cob == Cobertura.Agua) cob = Cobertura.Mata;
                float h = Relevo.Altura(x, z);
                float clareira = cob == Cobertura.Mata ? Clareira(x, z) : 0f;
                float chance = cob switch
                {
                    Cobertura.Mata => Mathf.Lerp(1f, 0.05f, clareira),
                    Cobertura.Rocha => h < 520f ? 0.2f : 0.03f,
                    Cobertura.Campo => 0.025f,
                    Cobertura.Urbano => 0.015f,
                    Cobertura.Lavoura => 0.005f,
                    _ => 0f,
                };
                // as cidades (ChaoDeLakewood.Lotes.cs): nada na frente das casas nem entre os lotes; os vazios com a mata fechada,
                // os quintais com umas árvores e o resto da cidade com a mata (ela tomou conta)
                float naCidade = PlantaNaCidade(x, z);
                if (naCidade < 0f) continue;
                if (naCidade > 0f)
                {
                    chance = Mathf.Max(chance, naCidade);
                    cob = Cobertura.Mata;
                }
                if (r > chance) continue;
                if (Agua(x, z, out _)) continue;
                float hf = Altura(x, z);
                if (AguaDoLagoEm(x, z, out float nivelPerto, 14f) && hf - nivelPerto < 2.2f) continue;            // a praia
                if (RioMaisPerto(x, z, 6f, out var rp) && rp.Dist < rp.Meia + rp.Rampa + 1.5f) continue;         // o barranco do rio
                if (EstradaPerto(x, z, 9f, out _, out _, out _, out _)) continue;
                if (Usina.SemArvores(x, z)) continue;                                                              // a usina e as linhas
                if (Partida != null && Partida.DistanciaDoTerreiro(x, z) < 8f) continue;                         // a casa da partida
                if (NaBaseMilitar(x, z)) continue;                                                                 // o pátio das bases
                if (Relevo.Inclinacao(x, z) > 1.1f) continue;                                                      // os paredões
                int tamanho = cob switch
                {
                    Cobertura.Mata => clareira > 0.4f ? (s < 0.5f ? 2 : 1) : (s < 0.52f ? 0 : s < 0.88f ? 1 : 2),
                    Cobertura.Rocha => h < 520f ? (s < 0.3f ? 0 : s < 0.75f ? 1 : 2) : (s < 0.6f ? 2 : 1),
                    _ => s < 0.55f ? 2 : s < 0.9f ? 1 : 0,
                };
                float escala = tamanho == 2 ? 0.8f + 0.5f * e01 : 0.85f + 0.35f * e01;
                saida.Add(new PlantaDaFloresta { Especie = Floresta.PinheiroGerado(tamanho, variacao), Posicao = new Vector3(x, hf - 0.15f, z), Giro = giro, Escala = escala });
            }
        }

        // ------------------------------------------------------------------ a grama densa

        int IChaoDaGrama.Semente => Mapa.semente;
        Rect IChaoDaGrama.Limites => new Rect(0f, 0f, Mapa.largura, Mapa.comprimento);
        bool IChaoDaGrama.SeguroEmThreads => true;
        float IChaoDaGrama.Altura(float x, float z) => Altura(x, z);

        /// <summary>
        /// A densidade da grama densa (0 a 1) num ponto: cheia no campo e nas clareiras, rala sob a mata (o chão de agulhas)
        /// e nas pedras, nada na água, na areia molhada da praia, no canal dos rios, no asfalto e no acostamento das estradas
        /// e nos paredões, e diminuindo na beira de cada um. Seguro em várias threads.
        /// </summary>
        public float Grama(float x, float z)
        {
            if (x < 0f || z < 0f || x > Mapa.largura || z > Mapa.comprimento) return 0f;
            float fator = 1f;
            if (AguaDoLagoEm(x, z, out float nivel, 8f))
            {
                float acima = Altura(x, z) - nivel;
                if (acima < 0.35f) return 0f;
                fator *= Geo.Suave(0.35f, 1.3f, acima);
            }
            if (RioMaisPerto(x, z, 4f, out var rio))
            {
                float borda = rio.Meia + rio.Rampa * 0.4f;
                if (rio.Dist < borda) return 0f;
                fator *= 1f - 0.85f * (1f - Geo.Suave(borda, rio.Meia + rio.Rampa + 3f, rio.Dist));
            }
            if (EstradaPerto(x, z, Acostamento + 1.5f, out float dist, out _, out _, out float meia))
            {
                if (dist < meia + Acostamento) return 0f;
                fator *= Geo.Suave(meia + Acostamento, meia + Acostamento + 1.5f, dist);
            }
            fator *= 1f - Geo.Suave(0.7f, 1.1f, Relevo.Inclinacao(x, z));
            fator *= Usina.FatorDaGrama(x, z);
            // o terreiro da casa da partida (o cascalho; embaixo da casa, os tufos furariam o piso)
            if (Partida != null) fator *= Geo.Suave(1f, 3f, Partida.DistanciaDoTerreiro(x, z));
            fator *= GramaDosLotes(x, z);
            if (fator <= 0f) return 0f;
            Span<float> f = stackalloc float[MapaDeLakewood.Coberturas];
            var pd = MapaDeLakewood.Deformar(_sorte, x, z);
            Mapa.Fracoes(pd.x, pd.y, f);
            float mata = f[(int)Cobertura.Mata] + f[(int)Cobertura.Agua];
            float cheia = mata * Mathf.Lerp(0.35f, 1f, mata > 0f ? Clareira(x, z) : 0f) + f[(int)Cobertura.Campo]
                        + 0.7f * f[(int)Cobertura.Lavoura] + 0.3f * f[(int)Cobertura.Rocha] + 0.5f * f[(int)Cobertura.Urbano];
            return Mathf.Clamp01(cheia) * fator;
        }
    }
}
