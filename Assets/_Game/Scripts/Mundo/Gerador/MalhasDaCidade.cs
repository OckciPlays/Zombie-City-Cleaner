using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As malhas das cidades num bloco de 64 m (Plano_Mapa, Parte M5, "primeiro caixas"), em dados (seguras em outra
    /// thread; os Mesh saem de <see cref="MalhaDeObra.ParaMesh"/> na principal), nas cores da paleta:
    ///  - <b>as ruas</b> (<see cref="DoBloco.Ruas"/>): o asfalto 5 cm acima do chão, com a faixa amarela tracejada no meio
    ///    das ruas, a dupla e as brancas das pistas nas avenidas e as faixas de pedestre nas pontas delas; o meio-fio de
    ///    15 cm; as calçadas; e, em cada cruzamento, o asfalto do meio, as quatro esquinas e a calçada que fecha os braços
    ///    sem rua (menos o do portão, por onde a rodovia continua). A beira de fora das calçadas desce até o chão;
    ///  - <b>as construções</b> (<see cref="DoBloco.Construcoes"/>), caixas do tamanho certo: a fundação de concreto
    ///    descendo até o chão mais baixo embaixo dela, as paredes e, pela zona, a casa com o telhado de duas águas, a porta
    ///    e as janelas; a loja com a vitrine, o toldo e as janelas de cima; o prédio com o saguão de vidro, as faixas de
    ///    janelas de cada andar e a casa de máquinas no teto; o laboratório murado; o estacionamento e a praça;
    ///  - <b>a colisão</b> (<see cref="DoBloco.Colisao"/>): as caixas das construções, os muros, as calçadas e o meio-fio
    ///    (o asfalto fica no chão do terreno, 5 cm abaixo).
    /// Cada pedaço de rua, cruzamento e lote é do bloco onde cai o meio dele (<see cref="TracadoDaCidade.Bloco"/>).
    /// </summary>
    public static class MalhasDaCidade
    {
        public sealed class DoBloco
        {
            public MalhaDeObra Ruas, Construcoes, Colisao;
            /// <summary>
            /// As casas de longe (Parte M6): a caixa do corpo de cada casa, com o telhado e as janelas escuras, sem colisão.
            /// Perto dos jogadores, as casas de verdade (<see cref="MalhasDasCasas"/>) tomam o lugar delas.
            /// </summary>
            public MalhaDeObra CasasDeLonge;
            public bool Vazio => Ruas == null || (Ruas.Triangulos == 0 && Construcoes.Triangulos == 0 && CasasDeLonge.Triangulos == 0);
        }

        /// <summary>Devolve as malhas do bloco ao estoque (depois de virarem Mesh).</summary>
        public static void Devolver(DoBloco r)
        {
            MalhaDeObra.Devolver(r.Ruas);
            MalhaDeObra.Devolver(r.Construcoes);
            MalhaDeObra.Devolver(r.Colisao);
            MalhaDeObra.Devolver(r.CasasDeLonge);
            r.Ruas = r.Construcoes = r.Colisao = r.CasasDeLonge = null;
        }

        /// <summary>As faixas que seguem o chão são cortadas a cada isto (m).</summary>
        const float Passo = 6f;
        const float Asfalto = TracadoDaCidade.AlturaDoAsfalto, Calcada = TracadoDaCidade.AlturaDaCalcada;
        /// <summary>Até onde a beira das calçadas desce (m, abaixo do chão).</summary>
        const float Fundo = -0.25f;

        static readonly string[] Toldos = { "PlacaVermelha", "PlacaVerde", "Laranja", "PlacaAmarela", "CarroAzul" };

        public static DoBloco Bloco(DadosDoMundo d, int bx, int bz)
        {
            var r = new DoBloco();
            var lista = d.CidadesDoBloco(bx, bz);
            if (lista == null || d.Plano == null) return r;
            r.Ruas = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            r.Construcoes = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            r.Colisao = MalhaDeObra.Emprestar();
            r.CasasDeLonge = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            foreach (int ti in lista)
            {
                var t = d.Plano.Tracados[ti];
                var doBloco = t.Bloco(bx, bz);
                if (doBloco == null) continue;
                var m = new Montagem(d, t, r);
                foreach (int k in doBloco.Pecas) m.Peca(t.Pecas[k]);
                foreach (int k in doBloco.Cruzamentos) m.Cruzamento(t.Cruzamentos[k]);
                foreach (int k in doBloco.Lotes) m.Lote(t.Lotes[k]);
            }
            return r;
        }

        sealed class Montagem
        {
            readonly DadosDoMundo _d;
            readonly TracadoDaCidade _t;
            readonly DoBloco _r;
            readonly List<Vector3> _p = new List<Vector3>(4);
            // onde vai a construção de agora: as construções, ou as casas de longe
            MalhaDeObra _m;

            public Montagem(DadosDoMundo d, TracadoDaCidade t, DoBloco r)
            {
                _d = d;
                _t = t;
                _r = r;
            }

            // ------------------------------------------------------------------ as peças que seguem o chão

            // o ponto local (u, v) no mapa, "acima" m acima do chão
            Vector3 P(float u, float v, float acima)
            {
                var w = _t.Mundo(u, v);
                return new Vector3(w.x, _d.Altura(w.x, w.y) + acima, w.y);
            }

            Vector3 P(Vector2 l, float acima) => P(l.x, l.y, acima);

            // a direção local "dir" no mapa (horizontal), no ponto local "l"
            Vector3 Dir(Vector2 l, Vector2 dir)
            {
                var w = (_t.Mundo(l + dir) - _t.Mundo(l)).normalized;
                return new Vector3(w.x, 0f, w.y);
            }

            void Tri(MalhaDeObra m, Vector3 a, Vector3 b, Vector3 c, string cor)
            {
                _p.Clear(); _p.Add(a); _p.Add(b); _p.Add(c);
                m.Face(_p, Vector3.up, cor);
            }

            // um retângulo local [u0, u1] × [v0, v1] seguindo o chão, "acima" m acima dele, cortado a cada Passo
            void Chao(MalhaDeObra m, float u0, float u1, float v0, float v1, float acima, string cor)
            {
                if (u1 - u0 < 0.01f || v1 - v0 < 0.01f) return;
                int nu = Mathf.Max(1, Mathf.CeilToInt((u1 - u0) / Passo)), nv = Mathf.Max(1, Mathf.CeilToInt((v1 - v0) / Passo));
                for (int a = 0; a < nu; a++)
                for (int b = 0; b < nv; b++)
                {
                    float ua = Mathf.Lerp(u0, u1, a / (float)nu), ub = Mathf.Lerp(u0, u1, (a + 1) / (float)nu);
                    float va = Mathf.Lerp(v0, v1, b / (float)nv), vb = Mathf.Lerp(v0, v1, (b + 1) / (float)nv);
                    Vector3 p00 = P(ua, va, acima), p10 = P(ub, va, acima), p11 = P(ub, vb, acima), p01 = P(ua, vb, acima);
                    Tri(m, p00, p10, p11, cor);
                    Tri(m, p00, p11, p01, cor);
                }
            }

            // a face em pé ao longo do segmento local a-b, de "de" até "ate" acima do chão, olhando para "fora" (local)
            void Degrau(MalhaDeObra m, Vector2 a, Vector2 b, float de, float ate, Vector2 fora, string cor)
            {
                float comprimento = Vector2.Distance(a, b);
                if (comprimento < 0.01f) return;
                int n = Mathf.Max(1, Mathf.CeilToInt(comprimento / Passo));
                for (int k = 0; k < n; k++)
                {
                    var pa = Vector2.Lerp(a, b, k / (float)n);
                    var pb = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    _p.Clear(); _p.Add(P(pa, de)); _p.Add(P(pb, de)); _p.Add(P(pb, ate)); _p.Add(P(pa, ate));
                    m.Face(_p, Dir((pa + pb) * 0.5f, fora), cor);
                }
            }

            // a calçada (em cima, no desenho e na colisão)
            void ChaoDaCalcada(float u0, float u1, float v0, float v1)
            {
                Chao(_r.Ruas, u0, u1, v0, v1, Calcada, "Calcada");
                Chao(_r.Colisao, u0, u1, v0, v1, Calcada, "Calcada");
            }

            void MeioFio(Vector2 a, Vector2 b, Vector2 fora)
            {
                Degrau(_r.Ruas, a, b, Asfalto - 0.02f, Calcada, fora, "MeioFio");
                Degrau(_r.Colisao, a, b, Asfalto - 0.02f, Calcada, fora, "MeioFio");
            }

            void BeiraDaCalcada(Vector2 a, Vector2 b, Vector2 fora) => Degrau(_r.Ruas, a, b, Fundo, Calcada, fora, "Calcada");

            // ------------------------------------------------------------------ as ruas

            public void Peca(TracadoDaCidade.Peca p)
            {
                bool lu = p.DaLinhaU;
                var tipo = lu ? _t.TipoU[p.Linha] : _t.TipoV[p.Linha];
                float a = TracadoDaCidade.MeiaPista(tipo), s = TracadoDaCidade.Calcada(tipo);
                float eixo = lu ? _t.Us[p.Linha] : _t.Vs[p.Linha];
                Vector2 L(float aoLongo, float atravessado) => lu ? new Vector2(eixo + atravessado, aoLongo) : new Vector2(aoLongo, eixo + atravessado);
                void Ret(MalhaDeObra m, float l0, float l1, float c0, float c1, float acima, string cor)
                {
                    if (lu) Chao(m, eixo + c0, eixo + c1, l0, l1, acima, cor);
                    else Chao(m, l0, l1, eixo + c0, eixo + c1, acima, cor);
                }
                var lado = lu ? new Vector2(1f, 0f) : new Vector2(0f, 1f);

                // o asfalto, em faixas (as pintadas tracejadas: 3 m pintados, 3 m não)
                var faixas = tipo == TipoDeRua.Avenida
                    ? new (float de, float ate, string cor, bool tracejada)[]
                    {
                        (-a, -3.56f, "Asfalto", false), (-3.56f, -3.44f, "ListraBranca", true), (-3.44f, -0.25f, "Asfalto", false),
                        (-0.25f, -0.13f, "Faixa", false), (-0.13f, 0.13f, "Asfalto", false), (0.13f, 0.25f, "Faixa", false),
                        (0.25f, 3.44f, "Asfalto", false), (3.44f, 3.56f, "ListraBranca", true), (3.56f, a, "Asfalto", false),
                    }
                    : new (float, float, string, bool)[] { (-a, -0.08f, "Asfalto", false), (-0.08f, 0.08f, "Faixa", true), (0.08f, a, "Asfalto", false) };
                foreach (var (de, ate, cor, tracejada) in faixas)
                {
                    if (!tracejada)
                    {
                        Ret(_r.Ruas, p.De, p.Ate, de, ate, Asfalto, cor);
                        continue;
                    }
                    float x = p.De;
                    while (x < p.Ate - 0.01f)
                    {
                        float proximo = Mathf.Min(p.Ate, (Mathf.Floor(x / 3f) + 1f) * 3f);
                        bool pintada = ((int)Mathf.Floor(x / 3f + 0.001f) & 1) == 0;
                        Ret(_r.Ruas, x, proximo, de, ate, Asfalto, pintada ? cor : "Asfalto");
                        x = proximo;
                    }
                }

                // as calçadas e o meio-fio, sem as esquinas (dos cruzamentos)
                var linhas = lu ? _t.Vs : _t.Us;
                float meio = (p.De + p.Ate) * 0.5f;
                int j = 0;
                while (j + 1 < linhas.Length && !(meio >= linhas[j] && meio <= linhas[j + 1])) j++;
                float esquina0 = lu ? linhas[j] + _t.AV(j) + _t.SV(j) : linhas[j] + _t.AU(j) + _t.SU(j);
                float esquina1 = lu ? linhas[j + 1] - _t.AV(j + 1) - _t.SV(j + 1) : linhas[j + 1] - _t.AU(j + 1) - _t.SU(j + 1);
                float c0 = Mathf.Max(p.De, esquina0), c1 = Mathf.Min(p.Ate, esquina1);
                if (c1 > c0)
                    foreach (float sinal in new[] { 1f, -1f })
                    {
                        float dentro = sinal * a, fora = sinal * (a + s);
                        if (lu) ChaoDaCalcada(eixo + Mathf.Min(dentro, fora), eixo + Mathf.Max(dentro, fora), c0, c1);
                        else ChaoDaCalcada(c0, c1, eixo + Mathf.Min(dentro, fora), eixo + Mathf.Max(dentro, fora));
                        MeioFio(L(c0, dentro), L(c1, dentro), -lado * sinal);
                        BeiraDaCalcada(L(c0, fora), L(c1, fora), lado * sinal);
                        Lixo(p, c0, c1, a, s, sinal);
                    }

                // as faixas de pedestre nas pontas das avenidas: listras de 0,5 m, ao longo da rua, 3 m de comprido
                if (tipo == TipoDeRua.Avenida)
                {
                    float borda0 = lu ? linhas[j] + _t.AV(j) : linhas[j] + _t.AU(j);
                    float borda1 = lu ? linhas[j + 1] - _t.AV(j + 1) : linhas[j + 1] - _t.AU(j + 1);
                    foreach (var (l0, l1) in new[] { (borda0 + 1f, borda0 + 4f), (borda1 - 4f, borda1 - 1f) })
                    {
                        float x0 = Mathf.Max(l0, p.De), x1 = Mathf.Min(l1, p.Ate);
                        if (x1 - x0 < 0.2f) continue;
                        for (float c = -a + 0.6f; c < a - 0.6f; c += 1f)
                            Ret(_r.Ruas, x0, x1, c, c + 0.5f, Asfalto + 0.006f, "ListraBranca");
                    }
                }
            }

            // o lixo na calçada e na sarjeta de um lado do pedaço (Parte M9c): os tijolos soltos, as caixas de papelão e os
            // sacos de lixo encostados, uns a cada 7 m; só o desenho (pequenos, não atrapalham o andar)
            void Lixo(TracadoDaCidade.Peca p, float c0, float c1, float a, float s, float sinal)
            {
                var sorte = new Sorteio(Sorteio.Misturar(((ulong)(uint)_d.Semente << 32) ^ ((ulong)(uint)p.Linha << 20) ^ (p.DaLinhaU ? 0x77UL : 0x11UL) ^
                                                         ((ulong)(uint)Mathf.RoundToInt(p.De * 10f) << 4) ^ (sinal > 0f ? 0xA5A5000000UL : 0x5A5A000000UL)));
                int n = Mathf.FloorToInt((c1 - c0) / 7f * sorte.Entre(0.6f, 1.4f));
                if (n <= 0 || c1 - c0 < 1.5f) return;
                var tremido = new System.Random((int)(sorte.Proximo() & 0x7fffffff));
                for (int i = 0; i < n; i++)
                {
                    float x = sorte.Entre(c0 + 0.6f, c1 - 0.6f);
                    float tipo = sorte.Fracao();
                    bool naSarjeta = sorte.Fracao() < 0.35f || s < 0.9f;
                    float atravessado = sinal * (naSarjeta ? a - sorte.Entre(0.15f, 0.45f) : a + sorte.Entre(0.35f, s - 0.35f));
                    var c = P(_t.NaPeca(p, x, atravessado), naSarjeta ? Asfalto : Calcada);
                    var gira = Quaternion.Euler(0f, sorte.Entre(0f, 360f), 0f);
                    if (tipo < 0.45f)
                    {
                        // os tijolos soltos
                        int k = 2 + (int)(sorte.Fracao() * 3);
                        for (int b = 0; b < k; b++)
                        {
                            var o = new Vector3(sorte.Entre(-0.35f, 0.35f), 0.037f, sorte.Entre(-0.35f, 0.35f));
                            _r.Ruas.Caixa(Matrix4x4.TRS(c + o, Quaternion.Euler(0f, sorte.Entre(0f, 360f), 0f), Vector3.one), new Vector3(0.23f, 0.075f, 0.11f),
                                          sorte.Fracao() < 0.5f ? "Tijolo" : "TijoloEscuro");
                        }
                    }
                    else if (tipo < 0.75f || naSarjeta)
                    {
                        // a caixa de papelão, um pouco torta
                        float t = sorte.Entre(0.3f, 0.6f);
                        var torta = gira * Quaternion.Euler(sorte.Entre(-8f, 8f), 0f, sorte.Entre(-8f, 8f));
                        _r.Ruas.Caixa(Matrix4x4.TRS(c + Vector3.up * (t * 0.28f), torta, Vector3.one), new Vector3(t, t * 0.6f, t * 0.8f), "Papelao");
                    }
                    else
                    {
                        // os sacos de lixo encostados
                        int k = 1 + (int)(sorte.Fracao() * 3);
                        for (int b = 0; b < k; b++)
                        {
                            var o = gira * new Vector3(sorte.Entre(-0.4f, 0.4f), 0f, sorte.Entre(-0.25f, 0.25f));
                            float tam = sorte.Entre(0.85f, 1.15f);
                            _r.Ruas.Esfera(Matrix4x4.TRS(c + o + Vector3.up * (0.15f * tam), gira, new Vector3(0.44f, 0.34f, 0.4f) * tam), 0.5f, "Saco", 0.035f, tremido);
                        }
                    }
                }
            }

            public void Cruzamento(Vector2Int c)
            {
                int i = c.x, j = c.y;
                float u = _t.Us[i], v = _t.Vs[j], au = _t.AU(i), su = _t.SU(i), av = _t.AV(j), sv = _t.SV(j);
                byte bracos = _t.Bracos(i, j), abertos = _t.Abertos[i, j];
                byte comAsfalto = (byte)(bracos | abertos);
                // o asfalto do meio
                Chao(_r.Ruas, u - au, u + au, v - av, v + av, Asfalto, "Asfalto");
                // as quatro esquinas: o meio-fio para os braços com asfalto; a beira desce onde não continua calçada
                foreach (float eu in new[] { 1f, -1f })
                foreach (float ev in new[] { 1f, -1f })
                {
                    byte bitU = eu > 0f ? TracadoDaCidade.MaisU : TracadoDaCidade.MenosU;
                    byte bitV = ev > 0f ? TracadoDaCidade.MaisV : TracadoDaCidade.MenosV;
                    float cu0 = u + eu * au, cu1 = u + eu * (au + su), cv0 = v + ev * av, cv1 = v + ev * (av + sv);
                    ChaoDaCalcada(Mathf.Min(cu0, cu1), Mathf.Max(cu0, cu1), Mathf.Min(cv0, cv1), Mathf.Max(cv0, cv1));
                    if ((comAsfalto & bitV) != 0) MeioFio(new Vector2(cu0, cv0), new Vector2(cu0, cv1), new Vector2(-eu, 0f));
                    if ((comAsfalto & bitU) != 0) MeioFio(new Vector2(cu0, cv0), new Vector2(cu1, cv0), new Vector2(0f, -ev));
                    if ((bracos & bitU) == 0) BeiraDaCalcada(new Vector2(cu1, cv0), new Vector2(cu1, cv1), new Vector2(eu, 0f));
                    if ((bracos & bitV) == 0) BeiraDaCalcada(new Vector2(cu0, cv1), new Vector2(cu1, cv1), new Vector2(0f, ev));
                }
                // a calçada que fecha os braços sem rua (menos o do portão)
                foreach (float e in new[] { 1f, -1f })
                {
                    byte bitU = e > 0f ? TracadoDaCidade.MaisU : TracadoDaCidade.MenosU;
                    if ((comAsfalto & bitU) == 0)
                    {
                        float a0 = u + e * au, a1 = u + e * (au + su);
                        ChaoDaCalcada(Mathf.Min(a0, a1), Mathf.Max(a0, a1), v - av, v + av);
                        MeioFio(new Vector2(a0, v - av), new Vector2(a0, v + av), new Vector2(-e, 0f));
                        BeiraDaCalcada(new Vector2(a1, v - av), new Vector2(a1, v + av), new Vector2(e, 0f));
                    }
                    byte bitV = e > 0f ? TracadoDaCidade.MaisV : TracadoDaCidade.MenosV;
                    if ((comAsfalto & bitV) == 0)
                    {
                        float b0 = v + e * av, b1 = v + e * (av + sv);
                        ChaoDaCalcada(u - au, u + au, Mathf.Min(b0, b1), Mathf.Max(b0, b1));
                        MeioFio(new Vector2(u - au, b0), new Vector2(u + au, b0), new Vector2(0f, -e));
                        BeiraDaCalcada(new Vector2(u - au, b1), new Vector2(u + au, b1), new Vector2(0f, e));
                    }
                }
            }

            // ------------------------------------------------------------------ os lotes

            public void Lote(Lote l)
            {
                switch (l.Tipo)
                {
                    case TipoDeLote.Estacionamento: Estacionamento(l); return;
                    case TipoDeLote.Praca: Praca(l); return;
                    case TipoDeLote.Laboratorio: Muro(l); break;
                }
                if (l.TemConstrucao) Construcao(l);
            }

            // a construção: a fundação até o chão mais baixo, as paredes até o topo e os detalhes da zona
            void Construcao(Lote l)
            {
                var q = new[] { new Vector2(l.BU0, l.BV0), new Vector2(l.BU1, l.BV0), new Vector2(l.BU1, l.BV1), new Vector2(l.BU0, l.BV1) };
                var w = new Vector2[4];
                float baixo = float.MaxValue, alto = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    w[k] = _t.Mundo(q[k]);
                    float h = _d.Altura(w[k].x, w[k].y);
                    baixo = Mathf.Min(baixo, h);
                    alto = Mathf.Max(alto, h);
                }
                var centro = _t.Mundo((q[0] + q[2]) * 0.5f);
                alto = Mathf.Max(alto, _d.Altura(centro.x, centro.y));
                var rnd = new System.Random((int)(l.Semente & 0x7fffffff));
                // as faces: a de índice k vai de w[k] a w[k + 1]; para fora, no plano local: (0,-1), (1,0), (0,1), (-1,0)
                int frente = l.Frente.y < 0f ? 0 : l.Frente.x > 0f ? 1 : l.Frente.y > 0f ? 2 : 3;
                if (l.Casa != null)
                {
                    // a casa de longe (Parte M6): no patamar dela, sem colisão (perto, é a casa de verdade que colide)
                    _m = _r.CasasDeLonge;
                    float chao = l.ChaoDaCasa, pisoDaCasa = chao + ZombieCityCleaner.Mundo.Casa.PisoDe(l.Casa);
                    Prisma(_m, w, Mathf.Min(baixo, chao) - 0.4f, pisoDaCasa, "Concreto");
                    Prisma(_m, w, pisoDaCasa, pisoDaCasa + l.Altura, Cor.PorNormal(l.Cor, l.Cor));
                    Casa(l, w, q, pisoDaCasa, pisoDaCasa + l.Altura, frente, rnd);
                    return;
                }
                if (l.Predio != null)
                {
                    // o prédio de longe (Parte M6, segunda fatia): no chão dele, sem colisão (perto, é o
                    // prédio de verdade)
                    _m = _r.CasasDeLonge;
                    float chao = l.ChaoDaCasa, pisoDoPredio = chao + PlantaDePredio.Piso, cobertura = chao + l.Predio.Cobertura;
                    Prisma(_m, w, Mathf.Min(baixo, chao) - 0.4f, pisoDoPredio, "Concreto");
                    Prisma(_m, w, pisoDoPredio, cobertura + PlantaDePredio.Platibanda, Cor.PorNormal("ConcretoClaro", l.Cor));
                    Predio(l, w, q, pisoDoPredio, cobertura, frente, rnd);
                    return;
                }
                if (l.Loja != null)
                {
                    // a loja de longe (Parte M6, terceira fatia): no chão dela, sem colisão (perto, é a
                    // loja de verdade que colide)
                    _m = _r.CasasDeLonge;
                    float chao = l.ChaoDaCasa, pisoDaLoja = chao + PlantaDeLoja.Piso;
                    Prisma(_m, w, Mathf.Min(baixo, chao) - 0.4f, pisoDaLoja, "Concreto");
                    Prisma(_m, w, pisoDaLoja, pisoDaLoja + l.Altura, Cor.PorNormal("ConcretoClaro", l.Cor));
                    Loja(l, w, pisoDaLoja, pisoDaLoja + l.Altura, frente, rnd);
                    return;
                }
                _m = _r.Construcoes;
                float fundacao = baixo - 0.4f, piso = alto + 0.45f, topo = alto + 0.15f + l.Altura;
                Prisma(_m, w, fundacao, piso, "Concreto");
                Prisma(_m, w, piso, topo, Cor.PorNormal("Concreto", l.Cor));
                Prisma(_r.Colisao, w, fundacao, topo, "Concreto");
                if (l.Tipo == TipoDeLote.Loja) Loja(l, w, piso, topo, frente, rnd);
                else Predio(l, w, q, piso, topo, frente, rnd);
            }

            // a caixa de base w (4 pontos do mapa, em volta) de y0 a y1
            static void Prisma(MalhaDeObra m, Vector2[] w, float y0, float y1, Cor cor)
            {
                var a = new List<Vector3>(4);
                var b = new List<Vector3>(4);
                for (int k = 0; k < 4; k++)
                {
                    a.Add(new Vector3(w[k].x, y0, w[k].y));
                    b.Add(new Vector3(w[k].x, y1, w[k].y));
                }
                m.Solido(a, b, cor);
            }

            // a normal para fora da face k (no mapa)
            static Vector2 Normal(Vector2[] w, int k)
            {
                var a = w[k];
                var b = w[(k + 1) % 4];
                var n = new Vector2(b.y - a.y, a.x - b.x).normalized;
                var centro = (w[0] + w[1] + w[2] + w[3]) * 0.25f;
                return Vector2.Dot(n, (a + b) * 0.5f - centro) < 0f ? -n : n;
            }

            static float Comprimento(Vector2[] w, int k) => Vector2.Distance(w[k], w[(k + 1) % 4]);

            // um retângulo na face k, de f0 a f1 (frações da face) e de y0 a y1, um pouco para fora dela
            void Abertura(MalhaDeObra m, Vector2[] w, int k, float f0, float f1, float y0, float y1, string cor, float afastado = 0.025f)
            {
                var n = Normal(w, k);
                var a = Vector2.Lerp(w[k], w[(k + 1) % 4], f0) + n * afastado;
                var b = Vector2.Lerp(w[k], w[(k + 1) % 4], f1) + n * afastado;
                _p.Clear();
                _p.Add(new Vector3(a.x, y0, a.y)); _p.Add(new Vector3(b.x, y0, b.y));
                _p.Add(new Vector3(b.x, y1, b.y)); _p.Add(new Vector3(a.x, y1, a.y));
                m.Face(_p, new Vector3(n.x, 0f, n.y), cor);
            }

            // uma janela (o vidro escuro; sem energia, nenhuma acende de noite)
            void Janela(Vector2[] w, int k, float meio, float largura, float y0, float y1)
            {
                float comprimento = Comprimento(w, k);
                float f = largura / Mathf.Max(comprimento, 0.1f) * 0.5f;
                if (meio - f < 0.02f || meio + f > 0.98f) return;
                Abertura(_m, w, k, meio - f, meio + f, y0, y1, "Vidro");
            }

            void Casa(Lote l, Vector2[] w, Vector2[] q, float piso, float topo, int frente, System.Random rnd)
            {
                // a porta na frente, as janelas em cada andar (na frente, duas; nos lados e atrás, uma ou duas)
                float largura = Comprimento(w, frente);
                float porta = 0.38f + (float)rnd.NextDouble() * 0.2f;
                float fp = 1.27f / Mathf.Max(largura, 1f) * 0.5f;
                Abertura(_m, w, frente, porta - fp, porta + fp, piso, piso + 2.42f, "Madeira");
                for (int andar = 0; andar < l.Andares; andar++)
                {
                    float y0 = piso + andar * 3.22f + 0.95f, y1 = y0 + 1.4f;
                    for (int k = 0; k < 4; k++)
                    {
                        if (k == frente)
                        {
                            if (andar == 0) { Janela(w, k, porta > 0.5f ? 0.2f : 0.8f, 1.4f, y0, y1); }
                            else { Janela(w, k, 0.25f, 1.4f, y0, y1); Janela(w, k, 0.75f, 1.4f, y0, y1); }
                            continue;
                        }
                        int quantas = Comprimento(w, k) > 10f ? 2 : 1;
                        for (int n = 0; n < quantas; n++) Janela(w, k, (n + 1f) / (quantas + 1f), 1.4f, y0, y1);
                    }
                }
                Telhado(l, q, topo);
            }

            // o telhado de duas águas, com a cumeeira no lado comprido e o beiral de 45 cm
            void Telhado(Lote l, Vector2[] q, float topo)
            {
                const float beiral = 0.45f;
                bool cumeeiraEmU = l.BU1 - l.BU0 >= l.BV1 - l.BV0;
                float meiaCurta = (cumeeiraEmU ? l.BV1 - l.BV0 : l.BU1 - l.BU0) * 0.5f;
                float subida = (meiaCurta + beiral) * 0.55f;
                float u0 = l.BU0 - beiral, u1 = l.BU1 + beiral, v0 = l.BV0 - beiral, v1 = l.BV1 + beiral;
                float beira = topo - beiral * 0.55f, cume = topo + meiaCurta * 0.55f;
                string telhado = l.CorDoTelhado ?? "Telhado";
                Vector3 W(float u, float v, float y)
                {
                    var p = _t.Mundo(u, v);
                    return new Vector3(p.x, y, p.y);
                }
                if (cumeeiraEmU)
                {
                    float vm = (l.BV0 + l.BV1) * 0.5f;
                    // as duas águas (por cima e por baixo, o beiral se vê da rua)
                    Agua(W(u0, v0, beira), W(u1, v0, beira), W(u1, vm, cume), W(u0, vm, cume), telhado);
                    Agua(W(u1, v1, beira), W(u0, v1, beira), W(u0, vm, cume), W(u1, vm, cume), telhado);
                    // os oitões (na cor da parede)
                    Oitao(W(l.BU0, l.BV0, topo), W(l.BU0, l.BV1, topo), W(l.BU0, vm, cume - 0.05f), new Vector2(-1f, 0f), l.Cor, (q[0] + q[3]) * 0.5f);
                    Oitao(W(l.BU1, l.BV1, topo), W(l.BU1, l.BV0, topo), W(l.BU1, vm, cume - 0.05f), new Vector2(1f, 0f), l.Cor, (q[1] + q[2]) * 0.5f);
                }
                else
                {
                    float um = (l.BU0 + l.BU1) * 0.5f;
                    Agua(W(u0, v1, beira), W(u0, v0, beira), W(um, v0, cume), W(um, v1, cume), telhado);
                    Agua(W(u1, v0, beira), W(u1, v1, beira), W(um, v1, cume), W(um, v0, cume), telhado);
                    Oitao(W(l.BU1, l.BV0, topo), W(l.BU0, l.BV0, topo), W(um, l.BV0, cume - 0.05f), new Vector2(0f, -1f), l.Cor, (q[0] + q[1]) * 0.5f);
                    Oitao(W(l.BU0, l.BV1, topo), W(l.BU1, l.BV1, topo), W(um, l.BV1, cume - 0.05f), new Vector2(0f, 1f), l.Cor, (q[2] + q[3]) * 0.5f);
                }
            }

            void Agua(Vector3 a, Vector3 b, Vector3 c, Vector3 d, string cor)
            {
                _p.Clear(); _p.Add(a); _p.Add(b); _p.Add(c); _p.Add(d);
                _m.Face(_p, Vector3.up, cor);
                _m.Face(_p, Vector3.down, "Madeira");
            }

            void Oitao(Vector3 a, Vector3 b, Vector3 c, Vector2 foraLocal, string cor, Vector2 noLocal)
            {
                _p.Clear(); _p.Add(a); _p.Add(b); _p.Add(c);
                _m.Face(_p, Dir(noLocal, foraLocal), cor);
            }

            void Loja(Lote l, Vector2[] w, float piso, float topo, int frente, System.Random rnd)
            {
                // a vitrine e o toldo (na loja de verdade, o da planta; no mercado, um pouco mais alta)
                var planta = l.Loja;
                float BaseDoAndar(int andar) => planta != null ? piso - PlantaDeLoja.Piso + planta.Base(andar) : piso + 4.5f + (andar - 1) * 3.4f;
                Abertura(_m, w, frente, 0.08f, 0.92f, piso + 0.15f, piso + (planta != null && planta.tipo == TipoDeLoja.Mercado ? 3.9f : 3.3f), "Vidro");
                if (planta != null ? planta.toldo != null : rnd.NextDouble() < 0.7)
                {
                    var n = Normal(w, frente);
                    var a = Vector2.Lerp(w[frente], w[(frente + 1) % 4], 0.06f);
                    var b = Vector2.Lerp(w[frente], w[(frente + 1) % 4], 0.94f);
                    var meio = (a + b) * 0.5f + n * 0.55f;
                    float comprimento = Vector2.Distance(a, b);
                    var giro = Quaternion.LookRotation(new Vector3(n.x, 0f, n.y), Vector3.up);
                    // (inclinado para baixo, para fora)
                    _m.Caixa(Matrix4x4.TRS(new Vector3(meio.x, piso + 3.55f, meio.y), giro * Quaternion.Euler(12f, 0f, 0f), Vector3.one),
                        new Vector3(comprimento, 0.12f, 1.1f), planta != null ? planta.toldo : Toldos[rnd.Next(Toldos.Length)]);
                }
                // as janelas dos andares de cima, na frente e atrás
                for (int andar = 1; andar < l.Andares; andar++)
                {
                    float y0 = BaseDoAndar(andar) + 0.9f, y1 = y0 + 1.4f;
                    foreach (int k in new[] { frente, (frente + 2) % 4 })
                    {
                        int quantas = Mathf.Max(1, Mathf.FloorToInt(Comprimento(w, k) / 3.2f));
                        for (int n = 0; n < quantas; n++) Janela(w, k, (n + 0.5f) / quantas, 1.5f, y0, y1);
                    }
                }
            }

            // (o laboratório, em caixa, e o prédio de longe, na malha das casas de longe: os andares e a cobertura do de verdade)
            void Predio(Lote l, Vector2[] w, Vector2[] q, float piso, float topo, int frente, System.Random rnd)
            {
                var planta = l.Predio;
                float BaseDoAndar(int andar) => planta != null ? piso - PlantaDePredio.Piso + planta.Base(andar) : piso + 4.5f + (andar - 1) * 3.6f;
                // o saguão de vidro na frente
                Abertura(_m, w, frente, 0.14f, 0.86f, piso + 0.1f, piso + 3.6f, "Vidro");
                // as faixas de janelas de cada andar, nos quatro lados
                for (int andar = 1; andar < l.Andares; andar++)
                {
                    float y0 = BaseDoAndar(andar) + 0.9f, y1 = y0 + 1.6f;
                    for (int k = 0; k < 4; k++) Abertura(_m, w, k, 0.05f, 0.95f, y0, y1, "Vidro");
                }
                // a casa de máquinas e, às vezes, a caixa d'água em cima
                var c0 = Vector2.Lerp(q[0], q[2], 0.35f);
                var c1 = Vector2.Lerp(q[0], q[2], 0.65f);
                var casinha = new[] { _t.Mundo(c0.x, c0.y), _t.Mundo(c1.x, c0.y), _t.Mundo(c1.x, c1.y), _t.Mundo(c0.x, c1.y) };
                Prisma(_m, casinha, topo - 0.2f, topo + 2.8f, Cor.PorNormal("Concreto", "ConcretoClaro"));
                if (planta != null || rnd.NextDouble() < 0.5)
                {
                    var meio = _t.Mundo((c0 + c1) * 0.5f);
                    _m.Caixa(Matrix4x4.TRS(new Vector3(meio.x, topo + 3.8f, meio.y), Quaternion.identity, Vector3.one), new Vector3(2.4f, 2f, 2.4f), planta != null ? "ConcretoClaro" : "MadeiraVelha");
                }
            }

            // o muro do terreno do laboratório (2,8 m), com o portão aberto na frente
            void Muro(Lote l)
            {
                const float alto = 2.8f, grosso = 0.3f;
                var cantos = new[] { new Vector2(l.U0, l.V0), new Vector2(l.U1, l.V0), new Vector2(l.U1, l.V1), new Vector2(l.U0, l.V1) };
                for (int k = 0; k < 4; k++)
                {
                    var a = cantos[k];
                    var b = cantos[(k + 1) % 4];
                    bool daFrente = k == 0;   // o lado de v baixo
                    float comprimento = Vector2.Distance(a, b);
                    int n = Mathf.Max(1, Mathf.CeilToInt(comprimento / 8f));
                    for (int s = 0; s < n; s++)
                    {
                        float f0 = s / (float)n, f1 = (s + 1) / (float)n;
                        // o portão: 6 m abertos no meio da frente
                        if (daFrente && Mathf.Abs((f0 + f1) * 0.5f - 0.5f) * comprimento < 4f) continue;
                        var pa = _t.Mundo(Vector2.Lerp(a, b, f0));
                        var pb = _t.Mundo(Vector2.Lerp(a, b, f1));
                        var meio = (pa + pb) * 0.5f;
                        float chao = Mathf.Min(_d.Altura(pa.x, pa.y), _d.Altura(pb.x, pb.y));
                        var dir = pb - pa;
                        var giro = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
                        var M = Matrix4x4.TRS(new Vector3(meio.x, chao - 0.3f + (alto + 0.3f) * 0.5f, meio.y), giro, Vector3.one);
                        var tam = new Vector3(grosso, alto + 0.3f, dir.magnitude + 0.05f);
                        _r.Construcoes.Caixa(M, tam, Cor.PorNormal("ConcretoClaro", "Concreto"));
                        _r.Colisao.Caixa(M, tam, "Concreto");
                    }
                }
            }

            // o estacionamento: o asfalto do lote e as vagas pintadas, de frente para a rua
            void Estacionamento(Lote l)
            {
                Chao(_r.Ruas, l.U0 + 0.3f, l.U1 - 0.3f, l.V0 + 0.3f, l.V1 - 0.3f, 0.03f, "AsfaltoRemendo");
                bool aoLongoDeU = l.Frente.y != 0f;
                float de = aoLongoDeU ? l.U0 + 1f : l.V0 + 1f, ate = aoLongoDeU ? l.U1 - 1f : l.V1 - 1f;
                // as linhas das vagas no fundo do lote (5 m de comprido), a cada 2,6 m
                for (float x = de; x <= ate; x += 2.6f)
                {
                    if (aoLongoDeU)
                    {
                        float fundo = l.Frente.y < 0f ? l.V1 - 1f : l.V0 + 1f, ponta = l.Frente.y < 0f ? fundo - 5f : fundo + 5f;
                        Chao(_r.Ruas, x - 0.06f, x + 0.06f, Mathf.Min(fundo, ponta), Mathf.Max(fundo, ponta), 0.035f, "ListraBranca");
                    }
                    else
                    {
                        float fundo = l.Frente.x < 0f ? l.U1 - 1f : l.U0 + 1f, ponta = l.Frente.x < 0f ? fundo - 5f : fundo + 5f;
                        Chao(_r.Ruas, Mathf.Min(fundo, ponta), Mathf.Max(fundo, ponta), x - 0.06f, x + 0.06f, 0.035f, "ListraBranca");
                    }
                }
            }

            // a praça: o miolo calçado (onde vai o coreto, depois) e os dois caminhos em cruz
            void Praca(Lote l)
            {
                var meio = l.Meio;
                float raio = Mathf.Min(l.U1 - l.U0, l.V1 - l.V0) * 0.18f;
                Chao(_r.Ruas, meio.x - raio, meio.x + raio, meio.y - raio, meio.y + raio, 0.08f, "ConcretoClaro");
                Chao(_r.Ruas, l.U0, l.U1, meio.y - 1.5f, meio.y + 1.5f, 0.06f, "Calcada");
                Chao(_r.Ruas, meio.x - 1.5f, meio.x + 1.5f, l.V0, l.V1, 0.06f, "Calcada");
            }
        }
    }
}
