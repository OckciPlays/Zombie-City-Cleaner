using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A cidade de Lakewood, a principal do condado (23/11/2026, a parte C do Docs/23-Campanha-e-Mapa: "reconstruir o
    /// mapa", começando por ela; o pedido do Pedro de 22/11: "manter o padrão das cidades como era no mapa antigo, porém
    /// agora as cidades estão maiores e precisamos deixar desse tamanho (corresponder com o mapa e mini-mapa)").
    ///
    /// O padrão do mapa sorteado (<see cref="TracadoDaCidade"/>): a grade de ruas retas, os quarteirões de 100 × 75 m, as
    /// avenidas mais largas a cada 5 linhas de norte a sul e a cada 6 de leste a oeste, a calçada em todas, e as zonas pela
    /// distância do meio: os prédios no centro, as lojas coladas na calçada no comércio, as casas recuadas nos bairros.
    /// A grade fica nos quarteirões planos e secos dentro do contorno (a encosta da baía do noroeste e a beira do contorno
    /// ficam de fora). As rodovias da Etapa 5 que passam pela cidade continuam: o Lakewood Boulevard (a Airport Road e a
    /// East Shore Highway, de norte a sul) e a Central Avenue (a Eastern Highway, para o leste; para o oeste, uma avenida
    /// nova na mesma linha); a grade se cruza com as diagonais delas.
    ///
    /// O desenho é fixo: rodar de novo troca as ruas e os lotes da Lakewood pelos desta receita.
    /// </summary>
    public static class CidadePrincipalDeLakewood
    {
        public const string Cidade = "Lakewood";

        /// <summary>O meio da cidade: o nó das quatro rodovias.</summary>
        public static readonly Vector2 Centro = new Vector2(9820f, 3500f);

        // a grade: as linhas de norte a sul em x = X0 + DX·i (a X0 é a Airport Road) e as de leste a oeste em z = Z0 + DZ·j
        // (a Z0 é a Eastern Highway; a South Shore Highway, em z 2892, cai na j = -8)
        const float X0 = 9852f, DX = 100f, Z0 = 3492f, DZ = 75f;
        const int IMin = -18, IMax = 17, JMin = -20, JMax = 18;
        const float Calcada = RuasDaCidade.LarguraDaCalcada;
        const float LarguraDaRua = 8f, LarguraDaAvenida = 12f;
        /// <summary>A marina da cidade (o lugar do mapa), a rua até ela e onde ela para antes do lugar (m).</summary>
        public const string NomeDaMarina = "Lakewood Marina", RuaDaMarina = "Marina Road";
        const float FimDaRuaDaMarina = 22f;

        /// <summary>Meia distância entre dois lotes vizinhos (m) e o recuo a mais das pontas das fileiras (m).</summary>
        const float Folga = 0.3f, Ponta = 2f;
        /// <summary>A distância mínima de uma rua da grade até a beira do asfalto de uma rodovia (m).</summary>
        const float LongeDaRodovia = 12f;

        /// <summary>Até onde vão os prédios, as lojas e as lojas na beira das avenidas e das rodovias (m do meio).</summary>
        public const float RaioDoCentro = 340f, RaioDoComercio = 540f, RaioDasFaixas = 1000f;

        const int Semente = 20261123;

        /// <summary>Os nomes das rodovias dentro da cidade (o plano e as placas).</summary>
        public static readonly Dictionary<string, string> Apelidos = new Dictionary<string, string>
        {
            { "Airport Road", "Lakewood Boulevard" },
            { "East Shore Highway", "Lakewood Boulevard" },
            { "Eastern Highway", "Central Avenue" },
        };

        static readonly string[] NomesDoOeste =
        {
            "Maple", "Oak", "Elm", "Walnut", "Chestnut", "Birch", "Hickory", "Sycamore", "Willow", "Poplar",
            "Ash", "Aspen", "Cherry", "Magnolia", "Laurel", "Juniper", "Locust", "Linden", "Hawthorn", "Alder",
        };
        static readonly string[] NomesDoLeste =
        {
            "Washington", "Jefferson", "Lincoln", "Adams", "Madison", "Monroe", "Jackson", "Grant", "Wilson", "Franklin",
            "Harrison", "Hamilton", "Lafayette", "Kennedy", "Roosevelt", "Truman", "Garfield", "Hayes",
        };

        static float X(int i) => X0 + DX * i;
        static float Z(int j) => Z0 + DZ * j;
        static bool AvenidaX(int i) => i % 5 == 0;
        static bool AvenidaZ(int j) => j % 6 == 0;
        static float LarguraX(int i) => AvenidaX(i) ? LarguraDaAvenida : LarguraDaRua;
        static float LarguraZ(int j) => AvenidaZ(j) ? LarguraDaAvenida : LarguraDaRua;

        static string Ordinal(int n)
        {
            string s = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + s;
        }

        /// <summary>O nome da linha de norte a sul i (as árvores a oeste do boulevard, os presidentes a leste).</summary>
        public static string NomeX(int i)
        {
            if (i == 0) return "Lakewood Boulevard";
            string b = i < 0 ? NomesDoOeste[(-i - 1) % NomesDoOeste.Length] : NomesDoLeste[(i - 1) % NomesDoLeste.Length];
            return b + (AvenidaX(i) ? " Avenue" : " Street");
        }

        /// <summary>O nome da linha de leste a oeste j (numeradas a partir da Central Avenue).</summary>
        public static string NomeZ(int j)
        {
            if (j == 0) return "Central Avenue";
            return $"{(j > 0 ? "North" : "South")} {Ordinal(Mathf.Abs(j))} {(AvenidaZ(j) ? "Avenue" : "Street")}";
        }

        // ------------------------------------------------------------------ o menu

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: a cidade de Lakewood (a grade, as ruas e os lotes)", false, 8)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de desenhar a cidade de Lakewood.");
                return;
            }
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || mapa.estradas.Count == 0)
            {
                Debug.LogWarning("Falta o mapa com as estradas (Etapa 5: traçar as estradas).");
                return;
            }
            float t0 = Time.realtimeSinceStartup;
            try
            {
                EditorUtility.DisplayProgressBar("Lakewood", "A cidade de Lakewood: a grade e os lotes", 0.2f);
                Undo.RecordObject(mapa, "A cidade de Lakewood");
                var desenho = Desenhar(mapa, out string relatorio);
                desenho.Gravar();
                EditorUtility.SetDirty(mapa);
                AssetDatabase.SaveAssets();
                float tDesenho = Time.realtimeSinceStartup - t0;
                EditorUtility.DisplayProgressBar("Lakewood", "A cidade de Lakewood: o plano", 0.6f);
                var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
                float tChao = Time.realtimeSinceStartup - t0 - tDesenho;
                string imagem = ImagemDaCidade.Gravar(mapa, chao, Cidade, Apelidos, 2f);
                Debug.Log($"[Lakewood] A cidade de Lakewood (o desenho {tDesenho:F1} s, o chão com ela {tChao:F1} s, tudo {Time.realtimeSinceStartup - t0:F0} s): " +
                          $"{WoodcreekDeLakewood.ResumoDe(mapa, Cidade)}{relatorio}{desenho.Relatorio}\n  O plano: {imagem}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: a imagem da cidade de Lakewood", false, 23)]
        public static void SoAImagem()
        {
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || !mapa.TemLotes(Cidade))
            {
                Debug.LogWarning("A cidade de Lakewood ainda não tem os lotes (Etapa 6: a cidade de Lakewood).");
                return;
            }
            try
            {
                EditorUtility.DisplayProgressBar("Lakewood", "A cidade de Lakewood: o plano", 0.4f);
                var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
                Debug.Log($"[Lakewood] O plano da cidade de Lakewood: {ImagemDaCidade.Gravar(mapa, chao, Cidade, Apelidos, 2f)}. {WoodcreekDeLakewood.ResumoDe(mapa, Cidade)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ as rodovias (o índice)

        sealed class Rodovias
        {
            public readonly List<(string nome, List<Vector2> pts, float meia)> Lista = new List<(string, List<Vector2>, float)>();
            readonly Dictionary<long, List<(int r, int s)>> _celulas = new Dictionary<long, List<(int, int)>>();
            const float Celula = 32f, Folga = 40f;

            static long Chave(int i, int j) => ((long)i << 32) ^ (uint)j;

            public Rodovias(ChaoDeLakewood chao, Rect caixa)
            {
                foreach (var e in chao.Estradas)
                {
                    if (e.Via.tipo == TipoDeVia.Rua) continue;
                    var pts = new List<Vector2>();
                    foreach (var p in e.Pontos) if (caixa.Contains(p)) pts.Add(p);
                    if (pts.Count < 2) continue;
                    // (os pontos fora da caixa saem; uma estrada que sai e volta ficaria com um segmento atravessando: a caixa
                    // é grande o bastante para as rodovias da Lakewood entrarem e saírem uma vez só)
                    int r = Lista.Count;
                    Lista.Add((e.Via.nome, pts, e.MeiaLargura));
                    for (int s = 1; s < pts.Count; s++)
                    {
                        var mn = Vector2.Min(pts[s - 1], pts[s]) - Vector2.one * Folga;
                        var mx = Vector2.Max(pts[s - 1], pts[s]) + Vector2.one * Folga;
                        for (int j = Mathf.FloorToInt(mn.y / Celula); j <= Mathf.FloorToInt(mx.y / Celula); j++)
                        for (int i = Mathf.FloorToInt(mn.x / Celula); i <= Mathf.FloorToInt(mx.x / Celula); i++)
                        {
                            long k = Chave(i, j);
                            if (!_celulas.TryGetValue(k, out var l)) _celulas[k] = l = new List<(int, int)>();
                            l.Add((r, s));
                        }
                    }
                }
            }

            /// <summary>
            /// A distância do ponto até a beira do asfalto da rodovia mais perto (até 40 m da linha do meio; senão, o máximo),
            /// qual é ela e a direção dela ali.
            /// </summary>
            public float Beira(Vector2 p, out int qual, out Vector2 dir)
            {
                qual = -1;
                dir = Vector2.zero;
                float melhor = float.MaxValue;
                if (!_celulas.TryGetValue(Chave(Mathf.FloorToInt(p.x / Celula), Mathf.FloorToInt(p.y / Celula)), out var lista)) return melhor;
                foreach (var (r, s) in lista)
                {
                    var (_, pts, meia) = Lista[r];
                    float d = Linhas.DistanciaDoSegmento(p, pts[s - 1], pts[s], out _) - meia;
                    if (d >= melhor) continue;
                    melhor = d;
                    qual = r;
                    dir = (pts[s] - pts[s - 1]).normalized;
                }
                return melhor;
            }

            /// <summary>O ponto da linha do meio da rodovia mais perto (até 40 m) e qual é ela.</summary>
            public bool NoMeio(Vector2 p, out Vector2 q, out int qual)
            {
                qual = -1;
                q = p;
                float melhor = float.MaxValue;
                if (!_celulas.TryGetValue(Chave(Mathf.FloorToInt(p.x / Celula), Mathf.FloorToInt(p.y / Celula)), out var lista)) return false;
                foreach (var (r, s) in lista)
                {
                    var pts = Lista[r].pts;
                    float d = Linhas.DistanciaDoSegmento(p, pts[s - 1], pts[s], out float t);
                    if (d >= melhor) continue;
                    melhor = d;
                    qual = r;
                    q = Vector2.Lerp(pts[s - 1], pts[s], t);
                }
                return qual >= 0;
            }

            /// <summary>Onde o segmento a-b cruza a linha do meio de uma rodovia (o cruzamento mais perto de a).</summary>
            public bool Cruza(Vector2 a, Vector2 b, out Vector2 x, out int qual)
            {
                x = default;
                qual = -1;
                float melhor = float.MaxValue;
                for (int r = 0; r < Lista.Count; r++)
                {
                    var pts = Lista[r].pts;
                    for (int s = 1; s < pts.Count; s++)
                    {
                        if (!Linhas.Cruzam(a, b, pts[s - 1], pts[s], out var c, out float ta, out _)) continue;
                        if (ta >= melhor) continue;
                        melhor = ta;
                        x = c;
                        qual = r;
                    }
                }
                return qual >= 0;
            }
        }

        // ------------------------------------------------------------------ a receita

        enum Zona { Centro, Comercio, Bairro, Industrial }

        // uma fileira de lotes: o começo da frente, a direção ao longo dela, para onde a frente olha, o comprimento, o fundo, a
        // rua e se ela é uma avenida ou uma rodovia
        struct Fileira
        {
            public Vector2 A, T, Frente;
            public float L, Fundo;
            public string Rua;
            public bool Larga, Principal;
        }

        /// <summary>
        /// A receita: a grade nos quarteirões bons, as ruas (de quarteirão em quarteirão, juntando as seguidas numa rua só),
        /// os especiais de quarteirão inteiro e as fileiras de lotes. Não grava.
        /// </summary>
        public static DesenhoDaCidade Desenhar(MapaDeLakewood mapa, out string relatorio)
        {
            var chao = DesenhoDaCidade.ChaoSemAsCidades(mapa);
            var d = new DesenhoDaCidade(mapa, chao, Cidade);
            var cid = mapa.cidades.Find(c => c.nome == Cidade);
            var contorno = MapaDeLakewood.Contorno(cid.contorno);
            if (cid.populacao <= 0) cid.populacao = 38450;
            var rodovias = new Rodovias(chao, Rect.MinMaxRect(X(IMin) - 300f, Z(JMin) - 300f, X(IMax) + 300f, Z(JMax) + 300f));
            var sb = new StringBuilder();

            // 1) os quarteirões bons: dentro do contorno, secos e quase planos (o relevo do quarteirão inteiro varia até 5 m)
            int ni = IMax - IMin, nj = JMax - JMin;
            var bom = new bool[ni, nj];
            int bons = 0;
            for (int bi = 0; bi < ni; bi++)
            for (int bj = 0; bj < nj; bj++)
            {
                int i = bi + IMin, j = bj + JMin;
                float hmin = float.MaxValue, hmax = float.MinValue;
                bool ok = true;
                for (int a = 0; a <= 4 && ok; a++)
                for (int b = 0; b <= 4 && ok; b++)
                {
                    var p = new Vector2(Mathf.Lerp(X(i), X(i + 1), a / 4f), Mathf.Lerp(Z(j), Z(j + 1), b / 4f));
                    if (!Linhas.Dentro(p, contorno)) { ok = false; break; }
                    float h = chao.Relevo.Altura(p.x, p.y);
                    if ((chao.Agua(p.x, p.y, out float nivel, 6f) && nivel > h - 3f) || chao.RioMaisPerto(p.x, p.y, LotesDeLakewood.LongeDosRios, out _)) ok = false;
                    hmin = Mathf.Min(hmin, h);
                    hmax = Mathf.Max(hmax, h);
                }
                if (ok && hmax - hmin <= 5f)
                {
                    bom[bi, bj] = true;
                    bons++;
                }
            }
            bool Bom(int i, int j) => i >= IMin && i < IMax && j >= JMin && j < JMax && bom[i - IMin, j - JMin];

            // 2) as beiras dos quarteirões bons, de três tipos: a rua (1); a beira que corre junto de uma rodovia, que é a rua
            // dela (2); e a que passa a menos de 12 m da beira do asfalto de uma rodovia ou a cruza (3): sai (as diagonais das
            // rodovias cortam a grade; sem isso, os cruzamentos ficavam colados uns nos outros e uma rua andava por cima da
            // rodovia), e no lugar dela entra o pedaço até a rodovia, quando é comprido o bastante (o T)
            int Tipo(Vector2 a, Vector2 b, out string qual)
            {
                qual = null;
                var t = (b - a).normalized;
                int junto = 0;
                float perto = float.MaxValue;
                for (int k = 0; k <= 8; k++)
                {
                    var q = Vector2.Lerp(a, b, k / 8f);
                    float bd = rodovias.Beira(q, out int r, out var dir);
                    if (bd < 20f && Mathf.Abs(Vector2.Dot(dir, t)) > 0.9f)
                    {
                        junto++;
                        qual = rodovias.Lista[r].nome;
                    }
                    perto = Mathf.Min(perto, bd);
                }
                if (junto >= 5) return 2;
                if (perto < LongeDaRodovia || rodovias.Cruza(a, b, out _, out _)) return 3;
                return 1;
            }
            var bH = new Dictionary<(int, int), int>();
            var bV = new Dictionary<(int, int), int>();
            var ruaDaBeira = new Dictionary<(bool, int, int), string>();
            for (int i = IMin; i < IMax; i++)
            for (int j = JMin; j <= JMax; j++)
            {
                if (!Bom(i, j - 1) && !Bom(i, j)) continue;
                int k = Tipo(new Vector2(X(i), Z(j)), new Vector2(X(i + 1), Z(j)), out string qual);
                bH[(i, j)] = k;
                ruaDaBeira[(true, i, j)] = k == 2 ? qual : NomeZ(j);
            }
            for (int i = IMin; i <= IMax; i++)
            for (int j = JMin; j < JMax; j++)
            {
                if (!Bom(i - 1, j) && !Bom(i, j)) continue;
                int k = Tipo(new Vector2(X(i), Z(j)), new Vector2(X(i), Z(j + 1)), out string qual);
                bV[(i, j)] = k;
                ruaDaBeira[(false, i, j)] = k == 2 ? qual : NomeX(i);
            }
            int TipoDa(bool deH, int i, int j) => (deH ? bH : bV).TryGetValue((i, j), out int k) ? k : 0;

            // 3) as ruas: as beiras seguidas de uma linha viram uma rua só
            int ruas = 0;
            float metros = 0f;
            for (int j = JMin; j <= JMax; j++)
            {
                int ini = int.MinValue;
                for (int i = IMin; i <= IMax; i++)
                {
                    bool tem = i < IMax && TipoDa(true, i, j) == 1;
                    if (tem && ini == int.MinValue) ini = i;
                    if (tem || ini == int.MinValue) continue;
                    d.Rua(NomeZ(j), LarguraZ(j), X(ini), Z(j), X(i), Z(j));
                    metros += X(i) - X(ini);
                    ruas++;
                    ini = int.MinValue;
                }
            }
            for (int i = IMin; i <= IMax; i++)
            {
                int ini = int.MinValue;
                for (int j = JMin; j <= JMax; j++)
                {
                    bool tem = j < JMax && TipoDa(false, i, j) == 1;
                    if (tem && ini == int.MinValue) ini = j;
                    if (tem || ini == int.MinValue) continue;
                    d.Rua(NomeX(i), LarguraX(i), X(i), Z(ini), X(i), Z(j));
                    metros += Z(j) - Z(ini);
                    ruas++;
                    ini = int.MinValue;
                }
            }
            // os pedaços até as rodovias (os T): de um nó da grade com uma rua até onde a beira que saiu cruza a rodovia, com
            // ao menos 20 m; os pontos na rodovia a 35 m uns dos outros (os dois lados da mesma beira fazem um X) e a 40 m dos
            // nós das rodovias
            var pedacos = new List<(Vector2 de, Vector2 ate, string nome, float largura, float comp)>();
            bool TemRua(int i, int j)
            {
                return TipoDa(true, i - 1, j) == 1 || TipoDa(true, i, j) == 1 || TipoDa(false, i, j - 1) == 1 || TipoDa(false, i, j) == 1;
            }
            void Pedacos(bool deH, int i, int j)
            {
                if (TipoDa(deH, i, j) != 3) return;
                var a = new Vector2(X(i), Z(j));
                var b = deH ? new Vector2(X(i + 1), Z(j)) : new Vector2(X(i), Z(j + 1));
                if (!rodovias.Cruza(a, b, out var x, out _)) return;
                foreach (var no in chao.Nos) if (Vector2.Distance(no.Posicao, x) < 40f) return;
                string nome = deH ? NomeZ(j) : NomeX(i);
                float largura = deH ? LarguraZ(j) : LarguraX(i);
                if (Vector2.Distance(a, x) >= 20f && TemRua(i, j)) pedacos.Add((a, x, nome, largura, Vector2.Distance(a, x)));
                int bi = deH ? i + 1 : i, bj = deH ? j : j + 1;
                if (Vector2.Distance(b, x) >= 20f && TemRua(bi, bj)) pedacos.Add((b, x, nome, largura, Vector2.Distance(b, x)));
            }
            foreach (var kv in bH) Pedacos(true, kv.Key.Item1, kv.Key.Item2);
            foreach (var kv in bV) Pedacos(false, kv.Key.Item1, kv.Key.Item2);
            pedacos.Sort((x, y) => y.comp.CompareTo(x.comp));
            var pontosNaRodovia = new List<Vector2>();
            int ts = 0;
            foreach (var pd in pedacos)
            {
                bool longe = true;
                foreach (var q in pontosNaRodovia)
                {
                    float dd = Vector2.Distance(q, pd.ate);
                    if (dd > 0.5f && dd < 35f) { longe = false; break; }
                }
                if (!longe) continue;
                pontosNaRodovia.Add(pd.ate);
                d.Rua(pd.nome, pd.largura, pd.de.x, pd.de.y, pd.ate.x, pd.ate.y);
                metros += pd.comp;
                ts++;
            }
            sb.Append($"\n  A grade: {bons} quarteirões bons, {ruas} ruas e {ts} pedaços até as rodovias ({metros / 1000f:F1} km).");

            // a marina (23/11/2026, "Siga: marina e galpões"): a Marina Road do nó da grade mais perto dela até a península na
            // beira da baía; a loja da marina (a de pesca) e o estacionamento no fim dela. O píer, os barcos e a rampa são do
            // MarinaDeLakewood (pelo chão de verdade, na cena)
            var marina = mapa.lugares.Find(x => x.nome == NomeDaMarina);
            if (marina != null)
            {
                (int, int) no = default;
                float melhorNo = float.MaxValue;
                for (int i = IMin; i <= IMax; i++)
                for (int j = JMin; j <= JMax; j++)
                {
                    if (!TemRua(i, j)) continue;
                    float dd = Vector2.Distance(new Vector2(X(i), Z(j)), marina.ponto);
                    if (dd < melhorNo) { melhorNo = dd; no = (i, j); }
                }
                var de = new Vector2(X(no.Item1), Z(no.Item2));
                var dir = (marina.ponto - de).normalized;
                var ate = marina.ponto - dir * FimDaRuaDaMarina;
                d.Rua(RuaDaMarina, LarguraDaRua, de.x, de.y, Vector2.Lerp(de, ate, 0.5f).x, Vector2.Lerp(de, ate, 0.5f).y, ate.x, ate.y);
                metros += Vector2.Distance(de, ate);
                // a loja à esquerda de quem desce e o estacionamento à direita, no trecho mais plano dos últimos 120 m
                var antes = ate - dir * 120f;
                d.EspecialMaisPlano(UsoDoLote.Loja, "marina", RuaDaMarina, antes, ate, 1, 22f, 24f, Calcada);
                d.EspecialMaisPlano(UsoDoLote.Estacionamento, "marina", RuaDaMarina, antes, ate, -1, 26f, 24f, Calcada);
                sb.Append($"\n  A marina: a {RuaDaMarina}, {Vector2.Distance(de, ate):F0} m do nó ({de.x:F0}; {de.y:F0}) até ({ate.x:F0}; {ate.y:F0}).");
            }

            // 4) as zonas
            Zona ZonaEm(Vector2 p)
            {
                float n = Mathf.PerlinNoise(p.x * 0.0035f + 13.1f, p.y * 0.0035f + 7.7f);
                float dist = Vector2.Distance(p, Centro) * (0.85f + 0.3f * n);
                if (dist < RaioDoCentro) return Zona.Centro;
                if (dist < RaioDoComercio) return Zona.Comercio;
                if (p.x > X0 + 100f && p.y < 2760f) return Zona.Industrial;
                return Zona.Bairro;
            }
            float Distancia(Vector2 p) => Vector2.Distance(p, Centro) * (0.85f + 0.3f * Mathf.PerlinNoise(p.x * 0.0035f + 13.1f, p.y * 0.0035f + 7.7f));

            // a terra de um quarteirão (dentro das calçadas): a beira de cada lado é a calçada da rua; junto de uma rodovia, a
            // calçada da beira do asfalto dela (o pior ponto ao longo do lado)
            float Limite(bool deH, int i, int j, int lado)
            {
                int k = TipoDa(deH, i, j);
                float linha = deH ? Z(j) : X(i);
                if (k == 2)
                {
                    var a = new Vector2(X(i), Z(j));
                    var b = deH ? new Vector2(X(i + 1), Z(j)) : new Vector2(X(i), Z(j + 1));
                    float lim = lado > 0 ? float.MinValue : float.MaxValue;
                    for (int s = 0; s <= 8; s++)
                    {
                        if (!rodovias.NoMeio(Vector2.Lerp(a, b, s / 8f), out var q, out int r)) continue;
                        float c = deH ? q.y : q.x;
                        float meia = rodovias.Lista[r].meia;
                        lim = lado > 0 ? Mathf.Max(lim, c + meia + Calcada) : Mathf.Min(lim, c - meia - Calcada);
                    }
                    if (lim > float.MinValue && lim < float.MaxValue) return lim;
                    return linha + lado * (6.5f + Calcada);
                }
                float meiaDaRua = k == 1 ? (deH ? LarguraZ(j) : LarguraX(i)) * 0.5f : LarguraDaRua * 0.5f;
                return linha + lado * (meiaDaRua + Calcada);
            }
            Rect Terra(int i, int j) => Rect.MinMaxRect(Limite(false, i, j, 1), Limite(true, i, j, 1), Limite(false, i + 1, j, -1), Limite(true, i, j + 1, -1));
            // se dá para ter lotes de frente para a beira (a rua ou a rodovia dela)
            bool DeFrente(bool deH, int i, int j) => TipoDa(deH, i, j) is 1 or 2;
            bool Larga(bool deH, int i, int j) => TipoDa(deH, i, j) == 2 || (TipoDa(deH, i, j) == 1 && (deH ? AvenidaZ(j) : AvenidaX(i)));
            // as principais: as rodovias e as avenidas do meio (a Central Avenue e o Lakewood Boulevard), com as lojas na
            // beira até o RaioDasFaixas
            bool Principal(bool deH, int i, int j) => TipoDa(deH, i, j) == 2 || (TipoDa(deH, i, j) == 1 && (deH ? j == 0 : i == 0));

            // 5) os especiais de quarteirão inteiro: o quarteirão bom mais perto de cada alvo
            var especiais = new Dictionary<(int, int), (UsoDoLote tipo, string nome)>();
            void Especial(UsoDoLote tipo, string nome, float x, float z)
            {
                (int, int) melhor = default;
                float md = float.MaxValue;
                for (int i = IMin; i < IMax; i++)
                for (int j = JMin; j < JMax; j++)
                {
                    if (!Bom(i, j) || especiais.ContainsKey((i, j)) || !DeFrente(true, i, j)) continue;
                    var m = new Vector2((X(i) + X(i + 1)) * 0.5f, (Z(j) + Z(j + 1)) * 0.5f);
                    float dd = Vector2.Distance(m, new Vector2(x, z));
                    if (dd < md) { md = dd; melhor = (i, j); }
                }
                if (md < 400f) especiais[melhor] = (tipo, nome);
                else sb.Append($"\n  {nome ?? tipo.ToString()}: sem quarteirão perto de ({x:F0}; {z:F0})");
            }
            // a praça do meio (no quarteirão a nordeste do nó das rodovias, como no mapa sorteado) e as dos bairros
            Especial(UsoDoLote.Praca, null, 9902f, 3530f);
            Especial(UsoDoLote.Praca, null, 9300f, 3250f);
            Especial(UsoDoLote.Praca, null, 10700f, 4000f);
            Especial(UsoDoLote.Praca, null, 9650f, 4350f);
            Especial(UsoDoLote.Praca, null, 10450f, 2950f);
            Especial(UsoDoLote.Especial, "escola", 9400f, 2600f);
            Especial(UsoDoLote.Especial, "escola", 10500f, 4250f);
            Especial(UsoDoLote.Especial, "igreja", 10300f, 3800f);
            Especial(UsoDoLote.Especial, "igreja", 9050f, 3120f);
            Especial(UsoDoLote.Especial, "igreja", 10950f, 3250f);
            Especial(UsoDoLote.Especial, "bombeiros", 10150f, 3150f);

            // os postos: na fileira de frente para uma rodovia mais perto de cada alvo
            var postos = new List<Vector2> { new Vector2(8700f, 2892f), new Vector2(10500f, 3492f), new Vector2(9852f, 2250f), new Vector2(9828f, 3950f) };

            // 6) os lotes
            var sorte = new System.Random(Semente);
            int semLugar = 0, recuados = 0;
            var contagem = new Dictionary<UsoDoLote, int>();

            // a beira do asfalto das rodovias longe o bastante (a calçada) de cada ponto do lote
            bool Livre(LoteDoMapa l)
            {
                for (int a = 0; a <= 2; a++)
                for (int b = 0; b <= 2; b++)
                    if (rodovias.Beira(l.NoMapa((a / 2f - 0.5f) * l.largura, (b / 2f - 0.5f) * l.fundo), out _, out _) < Calcada + 0.3f) return false;
                foreach (var c in l.Cantos())
                    if (!Linhas.Dentro(c, contorno)) return false;
                return true;
            }

            bool Por(LoteDoMapa l, float minimo, float desnivel)
            {
                l.cidade = Cidade;
                // encostado numa rodovia: a frente vai para trás, de metro em metro (o fundo diminui)
                int recuo = 0;
                while (!Livre(l))
                {
                    if (++recuo > 6 || l.fundo - 1f < minimo) { semLugar++; return false; }
                    l.meio -= l.Frente * 0.5f;
                    l.fundo -= 1f;
                }
                if (recuo > 0) recuados++;
                // de frente para uma rodovia: perto da beira dela e olhando para ela (onde ela faz a curva e se afasta, não)
                if (rodovias.Lista.Exists(r => r.nome == l.rua))
                {
                    var frente = l.MeioDaFrente;
                    if (rodovias.Beira(frente, out _, out _) > 9f || !rodovias.NoMeio(frente, out var q, out _) ||
                        Vector2.Dot((q - frente).normalized, l.Frente) < 0.75f) { semLugar++; return false; }
                }
                if (LotesDeLakewood.Desnivel(chao, l, out bool agua) > desnivel || agua) { semLugar++; return false; }
                d.Lotes.Add(l);
                contagem[l.tipo] = (contagem.TryGetValue(l.tipo, out int c) ? c : 0) + 1;
                return true;
            }

            List<float> Larguras(float L, float min, float max)
            {
                var ws = new List<float>();
                float resto = L;
                while (resto > 0.01f)
                {
                    if (resto <= max)
                    {
                        if (resto >= min || ws.Count == 0) ws.Add(resto);
                        else ws[ws.Count - 1] += resto;
                        break;
                    }
                    float w = Mathf.Lerp(min, max, (float)sorte.NextDouble());
                    if (resto - w < min) w = Mathf.Max(min, resto - min);
                    ws.Add(w);
                    resto -= w;
                }
                return ws;
            }

            void Encher(Fileira f)
            {
                var meio = f.A + f.T * (f.L * 0.5f);
                var zona = ZonaEm(meio);
                bool faixa = zona == Zona.Bairro && f.Principal && Distancia(meio) < RaioDasFaixas;
                float giro = LoteDoMapa.GiroPara(f.Frente);
                float x = 0f;
                LoteDoMapa Novo(UsoDoLote tipo, float w) => new LoteDoMapa
                {
                    tipo = tipo, rua = f.Rua, giro = giro, largura = w - 2f * Folga, fundo = f.Fundo - Folga,
                    meio = f.A + f.T * (x + w * 0.5f) - f.Frente * ((f.Fundo - Folga) * 0.5f),
                };
                // o posto, no começo de uma fileira de frente para a rodovia perto de um dos alvos
                if (f.Larga && f.L >= 44f && f.Fundo >= 28f && ZonaEm(meio) != Zona.Centro)
                    for (int p = 0; p < postos.Count; p++)
                    {
                        if (Vector2.Distance(postos[p], meio) > 160f || !rodovias.Lista.Exists(r => r.nome == f.Rua)) continue;
                        if (Por(Novo(UsoDoLote.Posto, 44f), 28f, LotesDeLakewood.DesnivelMaximo))
                        {
                            postos.RemoveAt(p);
                            x = 44f;
                        }
                        break;
                    }
                float L = f.L - x;
                if (L < 9f) return;
                switch (zona)
                {
                    case Zona.Centro:
                        foreach (float w in Larguras(L, 24f, 40f))
                        {
                            var tipo = sorte.NextDouble() < 0.1 ? UsoDoLote.Estacionamento : UsoDoLote.Predio;
                            if (w >= 15f) Por(Novo(tipo, w), 19f, LotesDeLakewood.DesnivelMaximo);
                            x += w;
                        }
                        break;
                    case Zona.Comercio:
                        foreach (float w in Larguras(L, 12f, 22f))
                        {
                            double s = sorte.NextDouble();
                            var tipo = s < 0.06 ? UsoDoLote.Estacionamento : s < 0.1 ? UsoDoLote.Vazio : UsoDoLote.Loja;
                            Por(Novo(tipo, w), 14f, LotesDeLakewood.DesnivelMaximo);
                            x += w;
                        }
                        break;
                    case Zona.Industrial:
                        foreach (float w in Larguras(L, 34f, 60f))
                        {
                            Por(Novo(sorte.NextDouble() < 0.12 ? UsoDoLote.Vazio : UsoDoLote.Galpao, w), 20f, LotesDeLakewood.DesnivelMaximo);
                            x += w;
                        }
                        break;
                    default:
                        if (faixa)
                        {
                            foreach (float w in Larguras(L, 13f, 22f))
                            {
                                Por(Novo(sorte.NextDouble() < 0.08 ? UsoDoLote.Vazio : UsoDoLote.Loja, w), 14f, LotesDeLakewood.DesnivelMaximo);
                                x += w;
                            }
                            break;
                        }
                        int n = Mathf.Max(1, Mathf.FloorToInt(L / 24f));   // (lotes de 24 m ou mais, como na Woodcreek: com menos, metade das plantas do kit não cabia)
                        float wc = L / n;
                        for (int k = 0; k < n; k++)
                        {
                            Por(Novo(sorte.NextDouble() < 0.07 ? UsoDoLote.Vazio : UsoDoLote.Casa, wc), 22f, LotesDeLakewood.DesnivelMaximo);
                            x += wc;
                        }
                        break;
                }
            }

            for (int j = JMin; j < JMax; j++)
            for (int i = IMin; i < IMax; i++)
            {
                if (!Bom(i, j)) continue;
                var t = Terra(i, j);
                if (t.width < 20f || t.height < 30f) continue;
                if (especiais.TryGetValue((i, j), out var esp))
                {
                    // o quarteirão inteiro, de frente para a rua mais baixa em volta (o patamar fica na altura da rua da frente:
                    // de frente para uma mais alta, ele ficava acima das outras, e o terreno de longe cobria a beira delas)
                    (bool deH, int a, int b, Vector2 frente, Vector2 meioDoLado) melhorLado = (true, i, j, Vector2.down, new Vector2(t.center.x, Z(j)));
                    float maisBaixo = float.MaxValue;
                    foreach (var lado in new[]
                    {
                        (true, i, j, Vector2.down, new Vector2(t.center.x, Z(j))),
                        (true, i, j + 1, Vector2.up, new Vector2(t.center.x, Z(j + 1))),
                        (false, i, j, Vector2.left, new Vector2(X(i), t.center.y)),
                        (false, i + 1, j, Vector2.right, new Vector2(X(i + 1), t.center.y)),
                    })
                    {
                        if (!DeFrente(lado.Item1, lado.Item2, lado.Item3)) continue;
                        float hl = chao.Relevo.Altura(lado.Item5.x, lado.Item5.y);
                        if (hl < maisBaixo) { maisBaixo = hl; melhorLado = lado; }
                    }
                    bool deFrenteH = melhorLado.deH;
                    var l = new LoteDoMapa
                    {
                        tipo = esp.tipo, nome = esp.nome,
                        rua = ruaDaBeira.TryGetValue((melhorLado.deH, melhorLado.a, melhorLado.b), out var rs) ? rs : (melhorLado.deH ? NomeZ(melhorLado.b) : NomeX(melhorLado.a)),
                        giro = LoteDoMapa.GiroPara(melhorLado.frente),
                        largura = deFrenteH ? t.width : t.height, fundo = deFrenteH ? t.height : t.width, meio = t.center,
                    };
                    if (!Por(l, 30f, LotesDeLakewood.DesnivelMaximoDosGrandes))
                        sb.Append($"\n  {l.Descricao}: o quarteirão ({t.center.x:F0}; {t.center.y:F0}) NÃO SERVIU");
                    continue;
                }
                // as fileiras: de frente para as ruas de leste a oeste (os lados compridos); de frente para as de norte a sul só
                // quando uma delas é larga (o boulevard, uma avenida) e as outras não
                bool largaH = Larga(true, i, j) || Larga(true, i, j + 1);
                bool largaV = Larga(false, i, j) || Larga(false, i + 1, j);
                string Rua(bool deH, int a, int b) => ruaDaBeira.TryGetValue((deH, a, b), out var r) ? r : (deH ? NomeZ(b) : NomeX(a));
                // (as pontas das fileiras ficam a 2 m a mais das ruas dos lados: o patamar segue a rua da frente, e a do lado
                // pode estar mais baixa; de longe, o triângulo do terreno entre o lote e ela cobria a beira do asfalto)
                if (largaV && !largaH && (DeFrente(false, i, j) || DeFrente(false, i + 1, j)))
                {
                    float fundo = t.width * 0.5f;
                    var tc = Rect.MinMaxRect(t.xMin, t.yMin + Ponta, t.xMax, t.yMax - Ponta);
                    if (DeFrente(false, i, j))
                        Encher(new Fileira { A = new Vector2(tc.xMin, tc.yMin), T = Vector2.up, Frente = Vector2.left, L = tc.height, Fundo = fundo, Rua = Rua(false, i, j), Larga = Larga(false, i, j), Principal = Principal(false, i, j) });
                    if (DeFrente(false, i + 1, j))
                        Encher(new Fileira { A = new Vector2(tc.xMax, tc.yMax), T = Vector2.down, Frente = Vector2.right, L = tc.height, Fundo = fundo, Rua = Rua(false, i + 1, j), Larga = Larga(false, i + 1, j), Principal = Principal(false, i + 1, j) });
                }
                else
                {
                    float fundo = t.height * 0.5f;
                    var tr = Rect.MinMaxRect(t.xMin + Ponta, t.yMin, t.xMax - Ponta, t.yMax);
                    if (DeFrente(true, i, j))
                        Encher(new Fileira { A = new Vector2(tr.xMax, tr.yMin), T = Vector2.left, Frente = Vector2.down, L = tr.width, Fundo = fundo, Rua = Rua(true, i, j), Larga = Larga(true, i, j), Principal = Principal(true, i, j) });
                    if (DeFrente(true, i, j + 1))
                        Encher(new Fileira { A = new Vector2(tr.xMin, tr.yMax), T = Vector2.right, Frente = Vector2.up, L = tr.width, Fundo = fundo, Rua = Rua(true, i, j + 1), Larga = Larga(true, i, j + 1), Principal = Principal(true, i, j + 1) });
                }
            }

            sb.Append($"\n  Os lotes: {d.Lotes.Count} ({semLugar} sem lugar, {recuados} recuados da rodovia):");
            foreach (var kv in contagem) sb.Append($" {kv.Key} {kv.Value};");
            if (postos.Count > 0) sb.Append($"\n  {postos.Count} postos sem lugar.");
            relatorio = sb.ToString();
            return d;
        }
    }
}
