using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O que fica na rua de uma cidade do Lakewood (Plano_Lakewood, Etapa 6, a parte 2), feito pelo kit, numa malha só (o
    /// desenho e a colisão): a calçada com o meio-fio nos dois lados da rodovia onde ela passa pelo comércio, a placa verde
    /// com o nome das duas ruas em cada cruzamento de uma rua nova, a placa de madeira "WELCOME TO" nas entradas da rodovia
    /// (com a população riscada) e os postes de madeira apagados ao longo das ruas. As ruas em si são da rede das estradas
    /// (<see cref="ChaoDeLakewood.ViasDasCidades"/>). Código puro.
    /// </summary>
    public static class RuasDaCidade
    {
        /// <summary>A largura da calçada (a do acostamento) e a altura dela acima do asfalto (m).</summary>
        public const float LarguraDaCalcada = 3f, AlturaDaCalcada = 0.18f;
        /// <summary>A distância entre os postes (m).</summary>
        public const float EntrePostes = 38f;

        /// <summary>Os números da montagem, para o log.</summary>
        public struct Resumo
        {
            public int Placas, Entradas, Postes;
            public float Calcada;
            public override string ToString() => $"{Calcada:F0} m de calçada, {Placas} placas de rua, {Entradas} placas de entrada e {Postes} postes";
        }

        /// <summary>O nome curto de uma rua para a placa ("Pine Street" vira "PINE ST").</summary>
        public static string NaPlaca(string nome, IDictionary<string, string> apelidos)
        {
            if (apelidos != null && apelidos.TryGetValue(nome, out var ap)) nome = ap;
            var s = nome.ToUpperInvariant();
            s = s.Replace(" STREET", " ST").Replace(" LANE", " LN").Replace(" ROAD", " RD").Replace(" HIGHWAY", " HWY").Replace(" AVENUE", " AVE");
            return s;
        }

        /// <summary>Monta as peças da cidade no desenho e na colisão (nas coordenadas do mundo).</summary>
        public static Resumo Montar(ChaoDeLakewood chao, string cidade, string populacao, MalhaDeObra vis, MalhaDeObra col, IDictionary<string, string> apelidos = null)
        {
            var r = new Resumo();
            var lotes = chao.Lotes.FindAll(l => l.Lote.cidade == cidade);
            if (lotes.Count == 0) return r;
            r.Calcada = Calcadas(chao, lotes, vis, col);
            r.Placas = PlacasDasEsquinas(chao, cidade, vis, col, apelidos);
            r.Entradas = Entradas(chao, cidade, lotes, populacao, vis, col);
            r.Postes = Postes(chao, cidade, lotes, vis, col);
            return r;
        }

        // ------------------------------------------------------------------ a ajuda

        static Vector2 Tangente(ChaoDeLakewood.Estrada e, int i)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(e.Pontos.Length - 1, i + 1);
            return (e.Pontos[b] - e.Pontos[a]).normalized;
        }

        static Vector3 V(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        // a posição ao longo da estrada (os metros desde o começo) do ponto mais perto de p, e a distância até ela
        static float AoLongo(ChaoDeLakewood.Estrada e, Vector2 p, out float dist)
        {
            dist = float.MaxValue;
            float s = 0f;
            for (int i = 1; i < e.Pontos.Length; i++)
            {
                float d = Linhas.DistanciaDoSegmento(p, e.Pontos[i - 1], e.Pontos[i], out float t);
                if (d >= dist) continue;
                dist = d;
                s = Mathf.Lerp(e.AoLongo[i - 1], e.AoLongo[i], t);
            }
            return s;
        }

        // o índice do ponto da estrada a "s" metros do começo (o de antes) e a fração até o próximo
        static int PontoEm(ChaoDeLakewood.Estrada e, float s, out float t)
        {
            int n = e.Pontos.Length;
            for (int i = 1; i < n; i++)
                if (e.AoLongo[i] >= s)
                {
                    t = Mathf.InverseLerp(e.AoLongo[i - 1], e.AoLongo[i], s);
                    return i - 1;
                }
            t = 1f;
            return n - 2;
        }

        /// <summary>
        /// Um letreiro chapado na fonte das construções, centrado em "meio" (no pé das letras), correndo em "direita" e
        /// subindo em "cima", saindo um pouco da face (para "fora").
        /// </summary>
        public static void Letreiro(MalhaDeObra m, string texto, Vector3 meio, Vector3 direita, Vector3 cima, Vector3 fora, float h, Cor cor, float sai = 0.008f)
        {
            float total = Letras.Largura(texto, h);
            var inicio = meio - direita * (total * 0.5f);
            float x = 0f;
            var pts = new List<Vector3>(8);
            for (int i = 0; i < texto.Length; i++)
            {
                var g = Letras.De(texto[i]);
                if (g == null) { x += Letras.Espaco + Letras.Entre; continue; }
                foreach (var parte in g.partes)
                {
                    pts.Clear();
                    foreach (var q in parte) pts.Add(inicio + direita * ((x + q.x) * h) + cima * (q.y * h) + fora * sai);
                    m.Face(pts, fora, cor);
                }
                x += g.largura + Letras.Entre;
            }
        }

        // ------------------------------------------------------------------ a calçada do comércio

        // nas estradas que têm lojas de frente para elas, do primeiro ao último metro das lojas (mais 4 m), nos dois lados:
        // a laje de concreto do asfalto até o fim do acostamento, com o meio-fio, longe dos nós (as esquinas ficam livres)
        static float Calcadas(ChaoDeLakewood chao, List<ChaoDeLakewood.LoteNoChao> lotes, MalhaDeObra vis, MalhaDeObra col)
        {
            float total = 0f;
            foreach (var e in chao.Estradas)
            {
                if (e.Via.tipo == TipoDeVia.Rua) continue;
                float s0 = float.MaxValue, s1 = float.MinValue;
                foreach (var l in lotes)
                {
                    if (l.Lote.tipo != UsoDoLote.Loja || l.Lote.rua != e.Via.nome) continue;
                    float s = AoLongo(e, l.Lote.MeioDaFrente, out float d);
                    if (d > 30f) continue;
                    s0 = Mathf.Min(s0, s - l.Lote.largura * 0.5f - 4f);
                    s1 = Mathf.Max(s1, s + l.Lote.largura * 0.5f + 4f);
                }
                if (s0 >= s1) continue;
                float meia = e.MeiaLargura;
                for (int i = 0; i + 1 < e.Pontos.Length; i++)
                {
                    if (e.AoLongo[i + 1] < s0 || e.AoLongo[i] > s1) continue;
                    if (e.Ponte[i] || e.Ponte[i + 1]) continue;
                    if (e.AteONo[i] < 12f || e.AteONo[i + 1] < 12f) continue;
                    var ta = Tangente(e, i);
                    var tb = Tangente(e, i + 1);
                    var na = new Vector2(-ta.y, ta.x);
                    var nb = new Vector2(-tb.y, tb.x);
                    float ha = e.Alturas[i], hb = e.Alturas[i + 1];
                    bool comeco = i == 0 || e.AoLongo[i] - 0.01f <= s0 || e.AteONo[i - 1] < 12f;
                    bool fim = i + 2 >= e.Pontos.Length || e.AoLongo[i + 1] + 0.01f >= s1 || e.AteONo[i + 2] < 12f;
                    foreach (float lado in new[] { -1f, 1f })
                    {
                        Vector3 P(Vector2 p, Vector2 n, float off, float y) => V(p + n * (lado * off), y);
                        var a0 = P(e.Pontos[i], na, meia, ha + AlturaDaCalcada);
                        var a1 = P(e.Pontos[i], na, meia + LarguraDaCalcada, ha + AlturaDaCalcada);
                        var b0 = P(e.Pontos[i + 1], nb, meia, hb + AlturaDaCalcada);
                        var b1 = P(e.Pontos[i + 1], nb, meia + LarguraDaCalcada, hb + AlturaDaCalcada);
                        var topo = lado > 0 ? new[] { a0, b0, b1, a1 } : new[] { a0, a1, b1, b0 };
                        vis.Face(topo, Vector3.up, "Calcada");
                        col.Face(topo, Vector3.up, "Calcada");
                        // o meio-fio (do lado do asfalto) e o lado de trás (até o chão do lote)
                        var dentro = V(-na * lado, 0f);
                        var meioFio = new[] { a0, b0, b0 + Vector3.down * 0.3f, a0 + Vector3.down * 0.3f };
                        vis.Face(meioFio, dentro, "MeioFio");
                        col.Face(meioFio, dentro, "MeioFio");
                        vis.Face(new[] { a1, b1, b1 + Vector3.down * 0.8f, a1 + Vector3.down * 0.8f }, -dentro, "Concreto");
                        if (comeco) vis.Face(new[] { a0, a1, a1 + Vector3.down * 0.8f, a0 + Vector3.down * 0.3f }, V(-ta, 0f), "Concreto");
                        if (fim) vis.Face(new[] { b0, b1, b1 + Vector3.down * 0.8f, b0 + Vector3.down * 0.3f }, V(tb, 0f), "Concreto");
                        // as juntas das placas de concreto, a cada 2 m
                        float sa = e.AoLongo[i], sb = e.AoLongo[i + 1];
                        for (float c = Mathf.Ceil(sa / 2f) * 2f; c < sb; c += 2f)
                        {
                            float t = Mathf.InverseLerp(sa, sb, c);
                            var p = Vector2.Lerp(e.Pontos[i], e.Pontos[i + 1], t);
                            var n = Vector2.Lerp(na, nb, t).normalized;
                            var tt = Vector2.Lerp(ta, tb, t).normalized;
                            float y = Mathf.Lerp(ha, hb, t) + AlturaDaCalcada + 0.004f;
                            var j0 = V(p + n * (lado * meia) - tt * 0.015f, y);
                            var j1 = V(p + n * (lado * (meia + LarguraDaCalcada)) - tt * 0.015f, y);
                            var j2 = j1 + V(tt * 0.03f, 0f);
                            var j3 = j0 + V(tt * 0.03f, 0f);
                            vis.Face(lado > 0 ? new[] { j0, j3, j2, j1 } : new[] { j0, j1, j2, j3 }, Vector3.up, "Concreto");
                        }
                    }
                    total += 2f * (e.AoLongo[i + 1] - e.AoLongo[i]);
                }
            }
            return total;
        }

        // ------------------------------------------------------------------ as placas das esquinas

        // em cada nó com uma rua nova: o poste verde na esquina, com uma placa ao longo de cada rua (o nome dela nas duas
        // faces), a de cima ao longo da rua nova
        static int PlacasDasEsquinas(ChaoDeLakewood chao, string cidade, MalhaDeObra vis, MalhaDeObra col, IDictionary<string, string> apelidos)
        {
            var nomes = new HashSet<string>();
            foreach (var r in chao.Mapa.ruas) if (r.cidade == cidade) nomes.Add(r.nome);
            int placas = 0;
            foreach (var no in chao.Nos)
            {
                int rua = -1, ponto = -1, outra = -1, pontoDaOutra = -1;
                foreach (var (e, i) in no.Pontos)
                {
                    var est = chao.Estradas[e];
                    if (rua < 0 && est.Via.tipo == TipoDeVia.Rua && nomes.Contains(est.Via.nome)) { rua = e; ponto = i; }
                }
                if (rua < 0) continue;
                foreach (var (e, i) in no.Pontos)
                    if (e != rua && chao.Estradas[e].Via.nome != chao.Estradas[rua].Via.nome) { outra = e; pontoDaOutra = i; break; }
                if (outra < 0) continue;
                var er = chao.Estradas[rua];
                var eo = chao.Estradas[outra];
                // para onde a rua nova sai do nó, e a direção da outra
                Vector2 d = ponto < er.Pontos.Length - 1 ? (er.Pontos[ponto + 1] - er.Pontos[ponto]).normalized : (er.Pontos[ponto - 1] - er.Pontos[ponto]).normalized;
                var t = Tangente(eo, pontoDaOutra);
                var lado = new Vector2(d.y, -d.x);
                var canto = no.Posicao + d * (eo.MeiaLargura + 2.4f) + lado * (er.MeiaLargura + 1.7f);
                float h = chao.Altura(canto.x, canto.y);
                Poste(vis, col, canto, h);
                Placa(vis, V(canto, h + 2.55f), d, NaPlaca(er.Via.nome, apelidos));
                Placa(vis, V(canto, h + 2.28f), t, NaPlaca(eo.Via.nome, apelidos));
                placas++;
            }
            return placas;
        }

        static void Poste(MalhaDeObra vis, MalhaDeObra col, Vector2 p, float h)
        {
            vis.Cilindro(Matrix4x4.Translate(V(p, h)), 0.04f, 0.035f, 2.72f, 6, "PlacaVerde");
            col.Caixa(Matrix4x4.Translate(V(p, h + 1.3f)), new Vector3(0.12f, 2.6f, 0.12f), "Metal");
        }

        // uma placa de rua (1,1 × 0,22 m) ao longo de "dir", o meio no poste, com o nome nas duas faces
        static void Placa(MalhaDeObra vis, Vector3 meio, Vector2 dir, string texto)
        {
            var dx = new Vector3(dir.x, 0f, dir.y);
            var fora = new Vector3(-dir.y, 0f, dir.x);
            float larg = Mathf.Max(1.0f, Letras.Largura(texto, 0.12f) + 0.16f);
            var M = Matrix4x4.TRS(meio, Quaternion.LookRotation(fora, Vector3.up), Vector3.one);
            vis.Caixa(M, new Vector3(larg, 0.22f, 0.025f), "PlacaVerde");
            Letreiro(vis, texto, meio + fora * 0.0125f + Vector3.down * 0.06f, -dx, Vector3.up, fora, 0.12f, "PlacaBranca");
            Letreiro(vis, texto, meio - fora * 0.0125f + Vector3.down * 0.06f, dx, Vector3.up, -fora, 0.12f, "PlacaBranca");
        }

        // ------------------------------------------------------------------ as placas de entrada

        // nas rodovias com lotes da cidade: 70 m antes do primeiro e depois do último, do lado direito de quem chega, de
        // frente para ele
        static int Entradas(ChaoDeLakewood chao, string cidade, List<ChaoDeLakewood.LoteNoChao> lotes, string populacao, MalhaDeObra vis, MalhaDeObra col)
        {
            int n = 0;
            foreach (var e in chao.Estradas)
            {
                if (e.Via.tipo != TipoDeVia.Rodovia) continue;
                float s0 = float.MaxValue, s1 = float.MinValue;
                foreach (var l in lotes)
                {
                    if (l.Lote.rua != e.Via.nome) continue;
                    float s = AoLongo(e, l.Lote.MeioDaFrente, out float d);
                    if (d > 30f) continue;
                    s0 = Mathf.Min(s0, s);
                    s1 = Mathf.Max(s1, s);
                }
                if (s0 >= s1) continue;
                float total = e.AoLongo[e.AoLongo.Length - 1];
                foreach (var (s, sentido) in new[] { (s0 - 70f, 1f), (s1 + 70f, -1f) })
                {
                    if (s < 20f || s > total - 20f) continue;
                    int i = PontoEm(e, s, out float t);
                    var p = Vector2.Lerp(e.Pontos[i], e.Pontos[i + 1], t);
                    if (e.AteONo[i] < 40f) continue;
                    var tan = Tangente(e, i) * sentido;
                    var direita = new Vector2(tan.y, -tan.x);
                    var onde = p + direita * (e.MeiaLargura + 3f + 2.5f);
                    float h = chao.Altura(onde.x, onde.y);
                    PlacaDeEntrada(vis, col, V(onde, h), -tan, cidade, populacao);
                    n++;
                }
            }
            return n;
        }

        // a placa de madeira de 3,4 × 1,7 m em dois postes, de frente para "frente": WELCOME TO, o nome, a fundação e a
        // população riscada de vermelho
        static void PlacaDeEntrada(MalhaDeObra vis, MalhaDeObra col, Vector3 pe, Vector2 frente, string cidade, string populacao)
        {
            var f = new Vector3(frente.x, 0f, frente.y);
            var dir = new Vector3(-frente.y, 0f, frente.x);   // à direita de quem lê (olhando para -frente)
            var giro = Quaternion.LookRotation(f, Vector3.up);
            foreach (float x in new[] { -1.35f, 1.35f })
            {
                var poste = pe + dir * x;
                vis.Caixa(Matrix4x4.TRS(poste + Vector3.up * 1.4f, giro, Vector3.one), new Vector3(0.16f, 2.8f, 0.16f), "MadeiraVelha");
                col.Caixa(Matrix4x4.TRS(poste + Vector3.up * 1.4f, giro, Vector3.one), new Vector3(0.2f, 2.8f, 0.2f), "Madeira");
            }
            var meio = pe + Vector3.up * 1.95f;
            vis.Caixa(Matrix4x4.TRS(meio, giro, Vector3.one), new Vector3(3.4f, 1.7f, 0.1f), "Madeira");
            // a moldura
            vis.Caixa(Matrix4x4.TRS(meio + f * 0.055f + Vector3.up * 0.8f, giro, Vector3.one), new Vector3(3.4f, 0.1f, 0.02f), "MadeiraVelha");
            vis.Caixa(Matrix4x4.TRS(meio + f * 0.055f + Vector3.down * 0.8f, giro, Vector3.one), new Vector3(3.4f, 0.1f, 0.02f), "MadeiraVelha");
            var face = meio + f * 0.066f;
            Letreiro(vis, "WELCOME TO", face + Vector3.up * 0.45f, dir, Vector3.up, f, 0.2f, "PlacaBranca");
            Letreiro(vis, cidade.ToUpperInvariant(), face - Vector3.up * 0.1f, dir, Vector3.up, f, 0.42f, "PlacaBranca");
            Letreiro(vis, "EST. 1891", face - Vector3.up * 0.33f, dir, Vector3.up, f, 0.12f, "PlacaBranca");
            string pop = "POP. " + populacao;
            Letreiro(vis, pop, face - Vector3.up * 0.62f, dir, Vector3.up, f, 0.15f, "PlacaBranca");
            // o risco vermelho por cima da população (e o zero de spray do lado)
            float larg = Letras.Largura(pop, 0.15f) + 0.15f;
            var risco = face + f * 0.006f - Vector3.up * 0.545f;
            vis.Caixa(Matrix4x4.TRS(risco, giro * Quaternion.Euler(0f, 0f, 8f), Vector3.one), new Vector3(larg, 0.05f, 0.004f), "PlacaVermelha");
            Letreiro(vis, "0", face + f * 0.004f + dir * (larg * 0.5f + 0.25f) - Vector3.up * 0.7f, dir, Vector3.up, f, 0.3f, "PlacaVermelha");
        }

        // ------------------------------------------------------------------ os postes

        // os postes de madeira apagados: a cada 38 m, de um lado das ruas novas e da rodovia no trecho dos lotes, fora dos
        // nós e dos lotes
        static int Postes(ChaoDeLakewood chao, string cidade, List<ChaoDeLakewood.LoteNoChao> lotes, MalhaDeObra vis, MalhaDeObra col)
        {
            var nomes = new HashSet<string>();
            foreach (var r in chao.Mapa.ruas) if (r.cidade == cidade) nomes.Add(r.nome);
            int n = 0;
            foreach (var e in chao.Estradas)
            {
                float s0 = 0f, s1 = e.AoLongo[e.AoLongo.Length - 1];
                if (e.Via.tipo != TipoDeVia.Rua || !nomes.Contains(e.Via.nome))
                {
                    // a rodovia e as estradas que passam pela cidade: só no trecho dos lotes de frente para elas
                    s0 = float.MaxValue;
                    s1 = float.MinValue;
                    foreach (var l in lotes)
                    {
                        if (l.Lote.rua != e.Via.nome) continue;
                        float s = AoLongo(e, l.Lote.MeioDaFrente, out float d);
                        if (d > 30f) continue;
                        s0 = Mathf.Min(s0, s - 20f);
                        s1 = Mathf.Max(s1, s + 20f);
                    }
                    if (s0 >= s1) continue;
                }
                float lado = e.Via.tipo == TipoDeVia.Rua ? 1f : -1f;
                for (float s = s0 + 10f; s < s1; s += EntrePostes)
                {
                    int i = PontoEm(e, s, out float t);
                    if (e.AteONo[i] < 10f || e.AteONo[i + 1] < 10f || e.Ponte[i]) continue;
                    var p = Vector2.Lerp(e.Pontos[i], e.Pontos[i + 1], t);
                    var tan = Tangente(e, i);
                    var n2 = new Vector2(-tan.y, tan.x) * lado;
                    float off = e.MeiaLargura + (e.Via.tipo == TipoDeVia.Rua ? 1.6f : LarguraDaCalcada + 0.8f);
                    var onde = p + n2 * off;
                    if (chao.LoteEm(onde.x, onde.y, 0.5f) != null) continue;
                    float h = chao.Altura(onde.x, onde.y);
                    PosteDeMadeira(vis, col, V(onde, h), new Vector3(-n2.x, 0f, -n2.y));
                    n++;
                }
            }
            return n;
        }

        // um poste de madeira de 8,4 m com a travessa, os isoladores e o braço da luz (apagada) virado para a rua
        static void PosteDeMadeira(MalhaDeObra vis, MalhaDeObra col, Vector3 pe, Vector3 paraARua)
        {
            vis.Cilindro(Matrix4x4.Translate(pe + Vector3.down * 0.3f), 0.15f, 0.11f, 8.7f, 6, "MadeiraVelha");
            col.Caixa(Matrix4x4.Translate(pe + Vector3.up * 3f), new Vector3(0.3f, 6f, 0.3f), "Madeira");
            var lado = Vector3.Cross(Vector3.up, paraARua).normalized;
            var giro = Quaternion.LookRotation(paraARua, Vector3.up);
            vis.Caixa(Matrix4x4.TRS(pe + Vector3.up * 7.75f, giro, Vector3.one), new Vector3(1.8f, 0.1f, 0.1f), "MadeiraVelha");
            foreach (float x in new[] { -0.8f, -0.3f, 0.3f, 0.8f })
                vis.Cilindro(Matrix4x4.Translate(pe + lado * x + Vector3.up * 7.8f), 0.035f, 0.025f, 0.12f, 4, "Cromo");
            // o braço e a luminária (a lente apagada embaixo)
            var braco0 = pe + Vector3.up * 6.6f;
            var braco1 = braco0 + paraARua * 1.5f + Vector3.up * 0.25f;
            vis.Tubo(braco0, braco1, 0.035f, 4, "Metal");
            var cabeca = Matrix4x4.TRS(braco1 + paraARua * 0.2f, giro, Vector3.one);
            vis.Caixa(cabeca, new Vector3(0.28f, 0.12f, 0.5f), "Poste");
            vis.Face(new[]
            {
                braco1 + paraARua * 0.02f + lado * 0.1f + Vector3.down * 0.061f, braco1 + paraARua * 0.38f + lado * 0.1f + Vector3.down * 0.061f,
                braco1 + paraARua * 0.38f - lado * 0.1f + Vector3.down * 0.061f, braco1 + paraARua * 0.02f - lado * 0.1f + Vector3.down * 0.061f,
            }, Vector3.down, "Lente");
        }
    }
}
