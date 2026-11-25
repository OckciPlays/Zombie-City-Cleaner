using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A marina da cidade de Lakewood (23/11/2026, "Siga: marina e galpões"; a loja e o estacionamento são lotes da cidade, no
    /// fim da Marina Road): o píer de madeira saindo da beira da baía, com as pontes laterais das vagas, os barcos amarrados
    /// (uns afundados, cinco anos depois), a rampa dos barcos ao lado e o posto de combustível na ponta. Tudo pelo chão de
    /// verdade (a beira d'água e o fundo do lago), numa malha só no espaço do mapa, com a colisão. Código puro.
    /// </summary>
    public static class MarinaDeLakewood
    {
        /// <summary>O que foi montado (para o log).</summary>
        public struct Resumo
        {
            public float Pier;
            public int Pontes, Barcos, Afundados;
            public Vector2 Beira, Direcao;
            public override string ToString() =>
                $"o píer de {Pier:F0} m saindo de ({Beira.x:F0}; {Beira.y:F0}) para ({Direcao.x:F2}; {Direcao.y:F2}), {Pontes} pontes, {Barcos} barcos ({Afundados} afundados)";
        }

        const float Largura = 3f, Comprimento = 46f, Altura = 0.85f, EntrePontes = 9f, PonteComprida = 9f, PonteLarga = 1.4f;

        static Vector3 V(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>
        /// Monta a marina perto de "lugar" (o ponto do mapa), saindo de "terra" (o fim da rua): o píer começa na beira d'água
        /// mais perto do lugar e segue para a água. False sem água perto.
        /// </summary>
        public static bool Montar(ChaoDeLakewood chao, Vector2 lugar, Vector2 terra, int semente, MalhaDeObra vis, MalhaDeObra col, out Resumo resumo)
        {
            resumo = default;
            // a beira: o primeiro ponto com água em cada direção a partir do lugar; a mais perto
            float melhor = float.MaxValue;
            Vector2 beira = default, dir = default;
            for (float a = 0f; a < 360f; a += 3f)
            {
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                for (float s = 0f; s < 250f; s += 1f)
                {
                    var q = lugar + d * s;
                    if (!chao.Agua(q.x, q.y, out float nv) || nv <= chao.Altura(q.x, q.y)) continue;
                    if (s < melhor) { melhor = s; beira = q; dir = d; }
                    break;
                }
            }
            if (melhor == float.MaxValue) return false;
            chao.Agua(beira.x, beira.y, out float nivel);
            // o píer segue para onde a água continua (a direção que mais tempo fica na água em 50 m)
            float melhorAgua = -1f;
            Vector2 eixo = dir;
            for (float a = -60f; a <= 60f; a += 5f)
            {
                var d = (Vector2)(Quaternion.Euler(0f, 0f, a) * dir);
                int n = 0;
                for (float s = 2f; s <= Comprimento + 4f; s += 2f)
                {
                    var q = beira + d * s;
                    if (chao.Agua(q.x, q.y, out float nv) && nv > chao.Altura(q.x, q.y) + 0.6f) n++;
                }
                if (n > melhorAgua) { melhorAgua = n; eixo = d; }
            }
            var lado = new Vector2(-eixo.y, eixo.x);
            float y = nivel + Altura;
            var sorte = new System.Random(semente);
            // o começo: 6 m para dentro da terra, a passarela descendo do chão até o píer
            var ini = beira - eixo * 6f;
            float yTerra = Mathf.Max(chao.Altura(ini.x, ini.y) + 0.15f, y);
            Deck(chao, vis, col, ini, beira, Largura, yTerra, y, nivel);
            Deck(chao, vis, col, beira, beira + eixo * Comprimento, Largura, y, y, nivel);
            // as pontes laterais e as vagas entre elas
            int pontes = 0, barcos = 0, afundados = 0;
            for (float s = EntrePontes; s <= Comprimento - 3f; s += EntrePontes)
            {
                foreach (int l in new[] { -1, 1 })
                {
                    var p0 = beira + eixo * s + lado * (l * Largura * 0.5f);
                    var p1 = p0 + lado * (l * PonteComprida);
                    if (!chao.Agua(p1.x, p1.y, out _)) continue;
                    Deck(chao, vis, col, p0, p1, PonteLarga, y, y, nivel);
                    pontes++;
                    // o barco na vaga depois da ponte (perto do fim do píer, quase todas ocupadas)
                    if (s + EntrePontes * 0.5f > Comprimento || sorte.NextDouble() < 0.25) continue;
                    var meio = beira + eixo * (s + EntrePontes * 0.5f) + lado * (l * (Largura * 0.5f + PonteComprida * 0.55f));
                    if (!chao.Agua(meio.x, meio.y, out _) || nivel < chao.Altura(meio.x, meio.y) + 0.8f) continue;
                    bool afundado = sorte.NextDouble() < 0.3;
                    Barco(vis, col, meio, lado * l, nivel, afundado, sorte);
                    barcos++;
                    if (afundado) afundados++;
                }
            }
            // o posto de combustível na ponta: a bomba e o cano
            var ponta = beira + eixo * (Comprimento - 1.5f) + lado * (Largura * 0.25f);
            vis.Caixa(Matrix4x4.TRS(V(ponta, y + 0.75f), Quaternion.LookRotation(V(eixo, 0f)), Vector3.one), new Vector3(0.7f, 1.5f, 0.5f), "PlacaVermelha");
            col.Caixa(Matrix4x4.TRS(V(ponta, y + 0.75f), Quaternion.LookRotation(V(eixo, 0f)), Vector3.one), new Vector3(0.7f, 1.5f, 0.5f), "Metal");
            vis.Caixa(Matrix4x4.TRS(V(ponta + lado * 0.5f, y + 1.1f), Quaternion.LookRotation(V(eixo, 0f)), Vector3.one), new Vector3(0.12f, 0.12f, 0.9f), "Pneu");
            // a rampa dos barcos, ao lado do píer: o concreto descendo do chão para dentro da água
            Rampa(chao, vis, col, beira + lado * -9f - eixo * 10f, eixo, nivel);
            resumo = new Resumo { Pier = Comprimento + 6f, Pontes = pontes, Barcos = barcos, Afundados = afundados, Beira = beira, Direcao = eixo };
            return true;
        }

        // um trecho de deque de madeira de "a" a "b" (a altura de cada ponta), com as tábuas, a viga da beira e os postes até o
        // fundo (onde ele passa por cima da água) ou até o chão
        static void Deck(ChaoDeLakewood chao, MalhaDeObra vis, MalhaDeObra col, Vector2 a, Vector2 b, float largura, float ya, float yb, float nivel)
        {
            float comp = Vector2.Distance(a, b);
            if (comp < 0.2f) return;
            var t = (b - a) / comp;
            var n = new Vector2(-t.y, t.x);
            var meio = (a + b) * 0.5f;
            float ym = (ya + yb) * 0.5f;
            float inclina = Mathf.Atan2(ya - yb, comp) * Mathf.Rad2Deg;
            var giro = Quaternion.LookRotation(V(t, 0f)) * Quaternion.Euler(inclina, 0f, 0f);
            var M = Matrix4x4.TRS(V(meio, ym - 0.06f), giro, Vector3.one);
            vis.Caixa(M, new Vector3(largura, 0.12f, comp), "MadeiraVelha");
            col.Caixa(M, new Vector3(largura, 0.12f, comp), "Madeira");
            // as juntas das tábuas (a cada 0,9 m, uma fresta escura)
            for (float s = 0.45f; s < comp - 0.2f; s += 0.9f)
            {
                var p = a + t * s;
                float yy = Mathf.Lerp(ya, yb, s / comp) + 0.002f;
                vis.Caixa(Matrix4x4.TRS(V(p, yy), giro, Vector3.one), new Vector3(largura * 0.98f, 0.004f, 0.03f), "Oleo");
            }
            // as vigas das beiras e os postes, a cada 3 m dos dois lados
            foreach (int l in new[] { -1, 1 })
            {
                var Mv = Matrix4x4.TRS(V(meio + n * (l * (largura * 0.5f - 0.08f)), ym - 0.22f), giro, Vector3.one);
                vis.Caixa(Mv, new Vector3(0.16f, 0.22f, comp), "Madeira");
                for (float s = 0.3f; s <= comp; s += 3f)
                {
                    var p = a + t * s + n * (l * (largura * 0.5f - 0.1f));
                    float topo = Mathf.Lerp(ya, yb, s / comp) - 0.1f;
                    float fundo = Mathf.Min(chao.Altura(p.x, p.y), nivel) - 0.6f;
                    if (topo - fundo < 0.2f) continue;
                    vis.Cilindro(Matrix4x4.Translate(V(p, fundo)), 0.13f, 0.12f, topo - fundo + 0.25f, 6, "MadeiraVelha");
                }
            }
        }

        // um barco de passeio na vaga (a proa para fora do píer): o casco afinando na proa, o convés, a cabine com o para-brisa
        // e o motor de popa; o afundado vem inclinado e meio dentro da água
        static void Barco(MalhaDeObra vis, MalhaDeObra col, Vector2 meio, Vector2 proa, float nivel, bool afundado, System.Random sorte)
        {
            float comp = 6.4f + (float)sorte.NextDouble() * 1.8f, larg = 2.3f + (float)sorte.NextDouble() * 0.5f;
            string[] cores = { "CarroBranco", "CarroAzul", "CarroBege", "CarroVerde", "CarroVinho" };
            string casco = cores[sorte.Next(cores.Length)];
            var giro = Quaternion.LookRotation(V(proa, 0f));
            float y0 = nivel - 0.45f;
            if (afundado)
            {
                giro *= Quaternion.Euler(-14f - (float)sorte.NextDouble() * 10f, 0f, (sorte.NextDouble() < 0.5 ? -1f : 1f) * (10f + (float)sorte.NextDouble() * 15f));
                y0 = nivel - 1.3f;
            }
            var M = Matrix4x4.TRS(V(meio, y0), giro, Vector3.one);
            // o casco: a planta (a popa reta, a proa em ponta), do fundo (estreito) ao convés
            var baixo = new List<Vector3>();
            var cima = new List<Vector3>();
            float meiaL = larg * 0.5f, meiaC = comp * 0.5f;
            Vector2[] planta = { new Vector2(-meiaL, -meiaC), new Vector2(meiaL, -meiaC), new Vector2(meiaL, meiaC * 0.35f), new Vector2(0f, meiaC), new Vector2(-meiaL, meiaC * 0.35f) };
            foreach (var q in planta)
            {
                baixo.Add(M.MultiplyPoint3x4(new Vector3(q.x * 0.55f, 0f, q.y * 0.92f)));
                cima.Add(M.MultiplyPoint3x4(new Vector3(q.x, 1.05f, q.y)));
            }
            vis.Solido(baixo, cima, casco);
            col.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.55f, 0f)), new Vector3(larg * 0.9f, 1.1f, comp * 0.9f), "Metal");
            // a faixa do convés e a cabine com o para-brisa
            vis.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 1.08f, -meiaC * 0.15f)), new Vector3(larg * 0.92f, 0.06f, comp * 0.62f), "Madeira");
            vis.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 1.45f, meiaC * 0.05f)), new Vector3(larg * 0.75f, 0.7f, comp * 0.28f), casco);
            vis.Caixa(M * Matrix4x4.TRS(new Vector3(0f, 1.95f, meiaC * 0.22f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one), new Vector3(larg * 0.7f, 0.5f, 0.05f), "VidroCarro");
            // o motor de popa
            vis.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.9f, -meiaC - 0.25f)), new Vector3(0.45f, 0.7f, 0.4f), "Pneu");
            vis.Caixa(M * Matrix4x4.Translate(new Vector3(0f, 0.25f, -meiaC - 0.3f)), new Vector3(0.12f, 0.8f, 0.15f), "Metal");
        }

        // a rampa dos barcos: uma faixa de concreto de 5 m, do chão (6 m antes da beira) até 12 m dentro da água, rente ao relevo
        static void Rampa(ChaoDeLakewood chao, MalhaDeObra vis, MalhaDeObra col, Vector2 de, Vector2 eixo, float nivel)
        {
            var n = new Vector2(-eixo.y, eixo.x) * 2.5f;
            for (float s = 0f; s < 22f; s += 2f)
            {
                var a = de + eixo * s;
                var b = de + eixo * (s + 2f);
                float ha = chao.Altura(a.x, a.y) + 0.06f, hb = chao.Altura(b.x, b.y) + 0.06f;
                var quad = new[] { V(a - n, ha), V(b - n, hb), V(b + n, hb), V(a + n, ha) };
                vis.Face(quad, Vector3.up, "Concreto");
                if (ha > nivel - 0.5f) col.Face(quad, Vector3.up, "Concreto");
            }
        }
    }
}
