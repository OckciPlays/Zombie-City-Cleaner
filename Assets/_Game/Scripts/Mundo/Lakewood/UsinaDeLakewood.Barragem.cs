using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public sealed partial class UsinaDeLakewood
    {
        // ------------------------------------------------------------------ o perfil da barragem

        /// <summary>Onde a face de jusante deixa de ser em pé e começa o talude (m), e o talude (horizontal por vertical).</summary>
        public const float TopoDoTalude = 154f, Talude = 0.8f;
        const float FimDoMontanteReto = 128f, InclinacaoDeMontante = 0.05f;
        /// <summary>O comprimento de um bloco da barragem (as juntas), mais ou menos (m).</summary>
        const float Bloco = 18f;

        /// <summary>O v da face de jusante na altura y.</summary>
        public float VDeJusante(float y) => y >= TopoDoTalude ? MeiaCrista : MeiaCrista + Talude * (TopoDoTalude - y);

        /// <summary>O v da face de montante na altura y.</summary>
        public float VDeMontante(float y) => y >= FimDoMontanteReto ? -MeiaCrista : -MeiaCrista - InclinacaoDeMontante * (FimDoMontanteReto - y);

        /// <summary>A altura da face de jusante no v (a crista, até o pé do talude).</summary>
        public float YDeJusante(float v) => v <= MeiaCrista ? Crista : TopoDoTalude - (v - MeiaCrista) / Talude;

        // a calha do vertedouro em (v, y): a soleira em ogiva, o talude e a concha que deita no fundo da bacia
        static readonly Vector2[] PerfilDaCalha =
        {
            new Vector2(-6f, 139f), new Vector2(-3.5f, 140f), new Vector2(-1f, 140.3f), new Vector2(1.5f, 140.1f), new Vector2(4f, 139.3f),
            new Vector2(7f, 137.6f), new Vector2(10f, 135.3f), new Vector2(12.5f, 133f), new Vector2(37.3f, 102f), new Vector2(39.3f, 100.7f),
            new Vector2(41.8f, 100.1f), new Vector2(44f, 100f),
        };

        // a base de um bloco: abaixo do chão mais baixo em volta dele (e nunca mais alta que 150: nas pontas, enterrado)
        float BaseDoBloco(float u0, float u1)
        {
            float b = float.MaxValue;
            foreach (float u in new[] { u0, (u0 + u1) * 0.5f, u1 })
            foreach (float v in new[] { -9f, 0f, 10f, 22f, 34f, 46f, 56f })
            {
                var p = Mundo(u, v);
                b = Mathf.Min(b, Relevo.Altura(p.x, p.y));
            }
            return Mathf.Min(b - 4f, AlturaDoPe - 3f, 150f);
        }

        // ------------------------------------------------------------------ a barragem

        void MontarABarragem(List<PecaDaUsina> saida, System.Random sorte, Func<float, float, float> H)
        {
            var d = new Desenho(L(Comprimento * 0.5f, AlturaDoPe - 2f, 0f));
            float c = Crista, w = MeiaCrista;
            var jusante = new Vector3(0f, Talude, -1f).normalized;
            float ini = InicioDoVertedouro, fim = FimDoVertedouro;

            // os blocos de gravidade, dos dois lados do vertedouro
            void Trecho(float de, float ate, bool capaNoInicio, bool capaNoFim)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt((ate - de) / Bloco));
                for (int k = 0; k < n; k++)
                {
                    float u0 = Mathf.Lerp(de, ate, k / (float)n), u1 = Mathf.Lerp(de, ate, (k + 1) / (float)n);
                    float yb = BaseDoBloco(u0, u1);
                    BlocoDaBarragem(d, sorte, u0, u1, yb, k > 0, capaNoInicio && k == 0, capaNoFim && k == n - 1);
                }
            }
            Trecho(U0, ini, true, true);
            Trecho(fim, U1, true, true);

            // o vertedouro: a soleira e a calha, a face de montante dele, os pilares com o nariz, a ponte e as comportas
            float ybv = Mathf.Min(BaseDoBloco(ini, fim), 100f);
            for (int i = 0; i + 1 < PerfilDaCalha.Length; i++)
            {
                var p = PerfilDaCalha[i];
                var q = PerfilDaCalha[i + 1];
                var n2 = new Vector2(q.y - p.y, -(q.x - p.x));
                var fora = new Vector3(0f, -n2.x, n2.y).normalized;
                if (fora.y < 0f) fora = -fora;
                string cor = i < 7 ? "Bunker" : "Concreto";
                d.Quad(L(ini, p.y, p.x), L(fim, p.y, p.x), L(fim, q.y, q.x), L(ini, q.y, q.x), fora, cor, true);
            }
            d.Quad(L(ini, 139f, -6f), L(fim, 139f, -6f), L(fim, ybv, -7.5f), L(ini, ybv, -7.5f), Vector3.forward, "Concreto");
            d.Colide(L((ini + fim) * 0.5f, (ybv + 133f) * 0.5f, 3f), new Vector3(fim - ini, 133f - ybv, 18f));
            // o limo e a água escura na calha
            for (int k = 0; k < Vaos; k++)
            {
                float um = ini + Pilar + k * (Vao + Pilar) + Vao * 0.5f + (float)(sorte.NextDouble() - 0.5) * 4f;
                float larg = 2f + (float)sorte.NextDouble() * 3f;
                float v0 = 13f, v1 = 30f + (float)sorte.NextDouble() * 7f;
                d.Quad(L(um - larg, 133f - (v0 - 12.5f) / Talude + 0.04f, v0), L(um + larg, 133f - (v0 - 12.5f) / Talude + 0.04f, v0),
                       L(um + larg, 133f - (v1 - 12.5f) / Talude + 0.04f, v1), L(um - larg, 133f - (v1 - 12.5f) / Talude + 0.04f, v1), jusante, k % 2 == 0 ? "Musgo" : "Mofo");
            }
            for (int k = 0; k <= Vaos; k++)
            {
                float up = ini + Pilar * 0.5f + k * (Vao + Pilar);
                // o pilar: em (v, y), do nariz (que sobe do fundo do lago) até o fim, em cima da calha
                // o pilar em duas peças: a caixa de cima (até a ponte) e o pé inclinado de montante (do fundo do lago)
                d.Caixa(L(up, (c - 1.2f + 127f) * 0.5f, 2f), new Vector3(Pilar, c - 1.2f - 127f, 28f), "Concreto", true);
                d.Solido(new[] { L(up - Pilar * 0.5f, 112f, -12f), L(up - Pilar * 0.5f, 127f, -12f), L(up - Pilar * 0.5f, 127f, 16f) },
                         new[] { L(up + Pilar * 0.5f, 112f, -12f), L(up + Pilar * 0.5f, 127f, -12f), L(up + Pilar * 0.5f, 127f, 16f) }, "Concreto", true);
                // o nariz de montante
                d.Solido(new[] { L(up - Pilar * 0.5f, 112f, -12f), L(up + Pilar * 0.5f, 112f, -12f), L(up, 112f, -14.6f) },
                         new[] { L(up - Pilar * 0.5f, c - 1.2f, -12f), L(up + Pilar * 0.5f, c - 1.2f, -12f), L(up, c - 1.2f, -14.6f) }, "Concreto", true);
                // a casa do guincho da comporta, em cima do pilar
                {
                    d.Caixa(L(up, c + 0.6f, -9.2f), new Vector3(Pilar - 0.3f, 3.6f, 5f), Cor.PorNormal("Concreto", "Bunker"), true);
                    d.Caixa(L(up, c + 2.5f, -9.2f), new Vector3(Pilar + 0.2f, 0.25f, 5.5f), "Concreto");
                    d.Caixa(L(up, c + 0.2f, -6.55f), new Vector3(1.1f, 2f, 0.06f), "Ferrugem");
                }
                // as manchas de água escorrida nas laterais do pilar
                d.Quad(L(up + Pilar * 0.5f + 0.02f, c - 1.4f, 4f), L(up + Pilar * 0.5f + 0.02f, c - 1.4f, 8f),
                       L(up + Pilar * 0.5f + 0.02f, 133f, 8f), L(up + Pilar * 0.5f + 0.02f, 133f, 4f), Vector3.right, "Mofo");
            }
            // a ponte da estrada sobre os pilares
            d.Caixa(L((ini + fim) * 0.5f, c - 0.6f, 0f), new Vector3(fim - ini, 1.2f, 2f * w), Cor.PorNormal("Asfalto", "Concreto"), true);
            // as comportas de segmento, fechadas e enferrujadas
            for (int k = 0; k < Vaos; k++)
            {
                float ua = ini + Pilar + k * (Vao + Pilar), ub = ua + Vao;
                Comporta(d, ua, ub);
            }

            // a crista: a estrada, as calçadas, o parapeito, o guarda-corpo e os postes (o asfalto acaba onde as estradas de
            // acesso começam)
            TrechoDaCrista(d, sorte, -20f, 900f);

            // as tomadas d'água da casa de força na face de montante (as grades acima da água) e o pórtico em cima delas
            for (int k = 0; k < 4; k++) TomadaDagua(d, 613f + k * 35f);
            Portico(d, 665f);

            // a torre do elevador (da crista ao pé) e as galerias de inspeção no pé
            TorreDoElevador(d, 440f);
            BocaDaGaleria(d, 300f, "GALLERY 1");
            BocaDaGaleria(d, 770f, "GALLERY 2");

            // o nome na face de jusante
            {
                float y = 124f;
                var meio = L(235f, y, VDeJusante(y));
                var cima = new Vector3(0f, 1f, Talude).normalized;
                d.Letreiro("LAKEWOOD DAM", meio + jusante * 0.04f, Vector3.right, cima, jusante, 9f, "Batente", 0.02f);
            }
            // a placa de bronze no parapeito, no meio da crista
            {
                var meio = L(380f, c + 0.62f, -5.55f);
                d.Caixa(meio + new Vector3(0f, 0.18f, -0.02f), new Vector3(2.3f, 0.75f, 0.04f), "Laranja");
                d.Letreiro("LAKEWOOD DAM", meio + new Vector3(0f, 0.32f, 0f), Vector3.right, Vector3.up, Vector3.back, 0.14f, "FerrugemEscura", 0.06f);
                d.Letreiro("COMPLETED 1962", meio + new Vector3(0f, 0.1f, 0f), Vector3.right, Vector3.up, Vector3.back, 0.09f, "FerrugemEscura", 0.06f);
                d.Letreiro("LAKEWOOD COUNTY WATER AND POWER", meio + new Vector3(0f, -0.08f, 0f), Vector3.right, Vector3.up, Vector3.back, 0.07f, "FerrugemEscura", 0.06f);
            }
            saida.Add(Peca("Barragem: Lakewood Dam", d));
        }

        // um bloco de gravidade de u0 a u1: as faces (a de montante, a de jusante e a de cima), as juntas, as manchas e as
        // bicas, e a colisão nos três sólidos convexos da seção
        void BlocoDaBarragem(Desenho d, System.Random sorte, float u0, float u1, float yb, bool junta, bool capaInicio, bool capaFim)
        {
            float c = Crista, w = MeiaCrista;
            float vT = VDeJusante(yb), vH = VDeMontante(yb);
            float y128 = Mathf.Max(FimDoMontanteReto, yb), v128 = VDeJusante(y128);
            var jusante = new Vector3(0f, Talude, -1f).normalized;
            var montanteInclinado = new Vector3(0f, -InclinacaoDeMontante, 1f).normalized;
            // as faces
            d.Quad(L(u0, c, -w), L(u1, c, -w), L(u1, c, w), L(u0, c, w), Vector3.up, "Asfalto");
            d.Quad(L(u0, c, -w), L(u1, c, -w), L(u1, y128, -w), L(u0, y128, -w), Vector3.forward, "Concreto");
            if (yb < FimDoMontanteReto)
                d.Quad(L(u0, FimDoMontanteReto, -w), L(u1, FimDoMontanteReto, -w), L(u1, yb, vH), L(u0, yb, vH), montanteInclinado, "Concreto");
            d.Quad(L(u0, c, w), L(u1, c, w), L(u1, TopoDoTalude, w), L(u0, TopoDoTalude, w), Vector3.back, "ConcretoClaro");
            d.Quad(L(u0, TopoDoTalude, w), L(u1, TopoDoTalude, w), L(u1, yb, vT), L(u0, yb, vT), jusante, "ConcretoClaro");
            // as tampas das pontas (o vertedouro e a rocha)
            void Tampa(float u, Vector3 fora)
            {
                d.Face(new[] { L(u, c, -w), L(u, c, w), L(u, TopoDoTalude, w), L(u, TopoDoTalude, -w) }, fora, "Concreto");
                d.Face(new[] { L(u, TopoDoTalude, -w), L(u, TopoDoTalude, w), L(u, y128, v128), L(u, y128, -w) }, fora, "Concreto");
                if (yb < FimDoMontanteReto) d.Face(new[] { L(u, FimDoMontanteReto, -w), L(u, FimDoMontanteReto, v128), L(u, yb, vT), L(u, yb, vH) }, fora, "Concreto");
            }
            if (capaInicio) Tampa(u0, Vector3.left);
            if (capaFim) Tampa(u1, Vector3.right);
            // a colisão: os três sólidos convexos da seção (o topo da crista, o meio e o pé)
            d.ColideSolido(new[] { L(u0, c, -w), L(u0, c, w), L(u0, TopoDoTalude, w), L(u0, TopoDoTalude, -w) },
                           new[] { L(u1, c, -w), L(u1, c, w), L(u1, TopoDoTalude, w), L(u1, TopoDoTalude, -w) });
            d.ColideSolido(new[] { L(u0, TopoDoTalude, -w), L(u0, TopoDoTalude, w), L(u0, y128, v128), L(u0, y128, -w) },
                           new[] { L(u1, TopoDoTalude, -w), L(u1, TopoDoTalude, w), L(u1, y128, v128), L(u1, y128, -w) });
            if (yb < FimDoMontanteReto)
                d.ColideSolido(new[] { L(u0, FimDoMontanteReto, -w), L(u0, FimDoMontanteReto, v128), L(u0, yb, vT), L(u0, yb, vH) },
                               new[] { L(u1, FimDoMontanteReto, -w), L(u1, FimDoMontanteReto, v128), L(u1, yb, vT), L(u1, yb, vH) });
            // a junta entre os blocos (uma faixa escura nas duas faces)
            if (junta)
            {
                const float j = 0.14f;
                d.Quad(L(u0 - j, c - 0.05f, w + 0.02f), L(u0 + j, c - 0.05f, w + 0.02f), L(u0 + j, TopoDoTalude, w + 0.02f), L(u0 - j, TopoDoTalude, w + 0.02f), Vector3.back, "Bunker");
                d.Quad(L(u0 - j, TopoDoTalude, w), L(u0 + j, TopoDoTalude, w), L(u0 + j, yb, vT), L(u0 - j, yb, vT), jusante, "Bunker", false);
                d.Quad(L(u0 - j, c - 0.05f, -w - 0.02f), L(u0 + j, c - 0.05f, -w - 0.02f), L(u0 + j, y128, -w - 0.02f), L(u0 - j, y128, -w - 0.02f), Vector3.forward, "Bunker");
            }
            // as bicas da crista e o escorrido escuro embaixo delas (fora do nome)
            for (float u = u0 + 4.5f; u < u1 - 2f; u += 9f)
            {
                d.Caixa(L(u, TopoDoTalude - 0.4f, w + 0.25f), new Vector3(0.25f, 0.25f, 0.5f), "Bunker");
                if (u > 185f && u < 285f) continue;
                if (yb > 140f || sorte.NextDouble() < 0.35) continue;
                float larg = 0.6f + (float)sorte.NextDouble() * 0.9f;
                float y1 = Mathf.Max(yb + 2f, TopoDoTalude - 8f - (float)sorte.NextDouble() * 30f);
                var de = L(u, TopoDoTalude - 0.6f, VDeJusante(TopoDoTalude - 0.6f)) + jusante * 0.03f;
                var ate = L(u, y1, VDeJusante(y1)) + jusante * 0.03f;
                d.Quad(de + Vector3.left * larg * 0.5f, de + Vector3.right * larg * 0.5f, ate + Vector3.right * larg * 0.3f, ate + Vector3.left * larg * 0.3f,
                       jusante, sorte.NextDouble() < 0.6 ? "Mofo" : "Bunker");
            }
            // a umidade no pé (a infiltração): uma faixa escura acima do chão, nos blocos do fundo do vale
            if (yb < AlturaDoPe - 1f && sorte.NextDouble() < 0.6)
            {
                float y0 = AlturaDoPe - 0.4f, y1 = y0 + 1.5f + (float)sorte.NextDouble() * 2.5f;
                float a0 = u0 + 1f + (float)sorte.NextDouble() * 4f, a1 = u1 - 1f - (float)sorte.NextDouble() * 4f;
                d.Quad(L(a0, y1, VDeJusante(y1)) + jusante * 0.03f, L(a1, y1, VDeJusante(y1)) + jusante * 0.03f,
                       L(a1, y0, VDeJusante(y0)) + jusante * 0.03f, L(a0, y0, VDeJusante(y0)) + jusante * 0.03f, jusante, "Musgo");
            }
        }

        // a estrada da crista de u0 a u1: as linhas, as calçadas, o parapeito de montante, o guarda-corpo de jusante e os postes
        void TrechoDaCrista(Desenho d, System.Random sorte, float u0, float u1)
        {
            float c = Crista, w = MeiaCrista;
            const float calcada = 1.75f;
            float meia = w - calcada;
            var corDaCalcada = Cor.Por((n, q) => n.y > 0.7f ? "Calcada" : "MeioFio");
            // as linhas da beira e a do meio (tracejada)
            foreach (float s in new[] { -1f, 1f })
                d.Quad(L(u0, c + 0.012f, s * (meia - 0.45f)), L(u1, c + 0.012f, s * (meia - 0.45f)), L(u1, c + 0.012f, s * (meia - 0.3f)), L(u0, c + 0.012f, s * (meia - 0.3f)), Vector3.up, "ListraBranca");
            for (float s = Mathf.Ceil(u0 / 9f) * 9f; s < u1; s += 9f)
            {
                if ((int)(s / 9f) % 7 == 3) continue;   // um pedaço apagado
                d.Quad(L(s, c + 0.012f, -0.08f), L(Mathf.Min(s + 3f, u1), c + 0.012f, -0.08f), L(Mathf.Min(s + 3f, u1), c + 0.012f, 0.08f), L(s, c + 0.012f, 0.08f), Vector3.up, "Faixa");
            }
            // os remendos e as rachaduras do asfalto
            for (int k = 0; k < 22; k++)
            {
                float u = Mathf.Lerp(u0 + 10f, u1 - 10f, (float)sorte.NextDouble());
                float v = ((float)sorte.NextDouble() - 0.5f) * 2f * (meia - 1f);
                float a = 0.8f + (float)sorte.NextDouble() * 2.5f, b = 0.6f + (float)sorte.NextDouble() * 1.6f;
                d.Quad(L(u - a, c + 0.008f, v - b), L(u + a, c + 0.008f, v - b), L(u + a, c + 0.008f, v + b), L(u - a, c + 0.008f, v + b), Vector3.up, k % 3 == 0 ? "Oleo" : "AsfaltoRemendo");
            }
            // as calçadas e o parapeito, em pedaços com as juntas dos blocos
            for (float a = u0; a < u1 - 0.01f; a += Bloco)
            {
                float b = Mathf.Min(u1, a + Bloco);
                foreach (float s in new[] { -1f, 1f })
                    d.Caixa(L((a + b) * 0.5f, c + 0.1f, s * (meia + calcada * 0.5f)), new Vector3(b - a - 0.03f, 0.2f, calcada), corDaCalcada, true);
                // o parapeito de montante (o lado do lago)
                d.Caixa(L((a + b) * 0.5f, c + 0.72f, -w + 0.22f), new Vector3(b - a - 0.04f, 1.05f, 0.45f), "Barreira", true);
                // o guarda-corpo de jusante: os pilaretes e os tubos (fora da porta da torre do elevador)
                for (float p = a + 1.1f; p < b; p += 2.25f)
                {
                    if (p > 435f && p < 445f) continue;
                    d.Caixa(L(p, c + 0.7f, w - 0.25f), new Vector3(0.22f, 1.0f, 0.22f), "Barreira");
                }
                foreach (var (ra, rb) in Partes(a, b, 435f, 445f))
                {
                    d.Caixa(L((ra + rb) * 0.5f, c + 0.85f, w - 0.25f), new Vector3(rb - ra, 0.08f, 0.1f), "Ferrugem");
                    d.Caixa(L((ra + rb) * 0.5f, c + 1.18f, w - 0.25f), new Vector3(rb - ra, 0.1f, 0.12f), "Metal");
                    d.Colide(L((ra + rb) * 0.5f, c + 0.75f, w - 0.25f), new Vector3(rb - ra, 1.15f, 0.4f));
                }
            }
            // os postes apagados (um torto e um caído)
            int k2 = 0;
            for (float u = u0 + 12f; u < u1 - 4f; u += 36f, k2++)
            {
                if (Mathf.Abs(u - 440f) < 6f || Mathf.Abs(u - 665f) < 7f) continue;
                var pe = L(u, c + 0.2f, w - 0.8f);
                if (k2 == 15)
                {
                    // caído, atravessado na calçada e na pista
                    var cabeca = pe + new Vector3(2.6f, 0.25f, 6.4f);
                    d.Entre(pe + Vector3.up * 0.2f, cabeca, 0.24f, 0.24f, "Poste");
                    d.Caixa(cabeca + new Vector3(0.2f, 0.05f, 0.3f), new Vector3(0.4f, 0.22f, 0.8f), "Metal", false, 20f);
                    d.Caixa(pe + Vector3.up * 0.15f, new Vector3(0.45f, 0.3f, 0.45f), "Concreto");
                    continue;
                }
                float tomba = k2 == 7 ? 17f : 0f;
                var giro = Quaternion.Euler(-tomba, 0f, 0f);
                var topo = pe + giro * Vector3.up * 7.2f;
                d.Entre(pe, topo, 0.22f, 0.22f, "Poste", true);
                var braco = topo + giro * (Vector3.forward * 2.0f + Vector3.up * 0.3f);
                d.Entre(topo, braco, 0.12f, 0.12f, "Poste");
                d.Caixa(braco + Vector3.down * 0.12f, new Vector3(0.38f, 0.2f, 0.8f), "Metal", false, 0f, -tomba);
            }
        }

        // os pedaços de [a, b] fora de [x0, x1]
        static IEnumerable<(float, float)> Partes(float a, float b, float x0, float x1)
        {
            if (b <= x0 || a >= x1) { yield return (a, b); yield break; }
            if (a < x0) yield return (a, x0);
            if (b > x1) yield return (x1, b);
        }

        // uma comporta de segmento (fechada): a chapa curva de frente para o lago, as vigas e os braços até o munhão no pilar
        void Comporta(Desenho d, float ua, float ub)
        {
            const float raio = 12f, vMunhao = 10f, yMunhao = 147f;
            float um = (ua + ub) * 0.5f;
            Vector3 Arco(float graus, float r, float u) => L(u, yMunhao + r * Mathf.Sin(graus * Mathf.Deg2Rad), vMunhao - r * Mathf.Cos(graus * Mathf.Deg2Rad));
            float g0 = Mathf.Asin((SoleiraDoVertedouro - yMunhao) / raio) * Mathf.Rad2Deg, g1 = Mathf.Asin((TopoDasComportas - yMunhao) / raio) * Mathf.Rad2Deg;
            const int partes = 5;
            for (int i = 0; i < partes; i++)
            {
                float a = Mathf.Lerp(g0, g1, i / (float)partes), b = Mathf.Lerp(g0, g1, (i + 1) / (float)partes);
                d.Entre(Arco(a, raio, um), Arco(b, raio, um), ub - ua - 0.1f, 0.22f, i % 2 == 0 ? "Ferrugem" : "FerrugemEscura");
            }
            // as vigas atrás da chapa
            foreach (float g in new[] { g0 + 6f, (g0 + g1) * 0.5f, g1 - 5f })
                d.Entre(Arco(g, raio - 0.55f, ua + 0.15f), Arco(g, raio - 0.55f, ub - 0.15f), 0.8f, 0.45f, "FerrugemEscura");
            // os braços e o munhão de cada lado
            foreach (float u in new[] { ua + 0.45f, ub - 0.45f })
            {
                var munhao = L(u, yMunhao, vMunhao);
                d.Entre(Arco(g0 + 8f, raio - 0.8f, u), munhao, 0.35f, 0.55f, "FerrugemEscura");
                d.Entre(Arco(g1 - 6f, raio - 0.8f, u), munhao, 0.35f, 0.55f, "FerrugemEscura");
                d.Caixa(munhao, new Vector3(0.7f, 1.1f, 1.1f), "Metal");
            }
            d.Colide(L(um, (SoleiraDoVertedouro + TopoDasComportas) * 0.5f, -0.5f), new Vector3(ub - ua, TopoDasComportas - SoleiraDoVertedouro, 1.6f));
        }

        // uma tomada d'água na face de montante: a boca escura e a grade (as barras e as travessas), acima e abaixo da água
        void TomadaDagua(Desenho d, float uc)
        {
            float w = MeiaCrista;
            const float meia = 5f, y0 = 126f, y1 = 156f;
            d.Quad(L(uc - meia, y1, -w - 0.03f), L(uc + meia, y1, -w - 0.03f), L(uc + meia, y0, -w - 0.03f), L(uc - meia, y0, -w - 0.03f), Vector3.forward, "Interior");
            for (float u = uc - meia + 0.25f; u < uc + meia; u += 0.5f)
                d.Caixa(L(u, (y0 + y1) * 0.5f, -w - 0.2f), new Vector3(0.08f, y1 - y0, 0.14f), "Grade");
            foreach (float y in new[] { 131f, 141f, 151f, 155.6f })
                d.Caixa(L(uc, y, -w - 0.3f), new Vector3(2f * meia + 0.3f, 0.18f, 0.2f), y > 155f ? "Ferrugem" : "Grade");
            // a moldura de concreto e as ranhuras da comporta de emergência
            d.Caixa(L(uc, y1 + 0.45f, -w - 0.25f), new Vector3(2f * meia + 1.2f, 0.9f, 0.5f), "Concreto");
            foreach (float s in new[] { -1f, 1f })
                d.Caixa(L(uc + s * (meia + 0.35f), (y0 + y1) * 0.5f, -w - 0.25f), new Vector3(0.7f, y1 - y0, 0.5f), "Concreto");
        }

        // o pórtico rolante sobre a crista: as pernas nas duas calçadas (os carros passam embaixo), as vigas que avançam
        // por cima do lago até as tomadas, o carro do guincho e o gancho
        void Portico(Desenho d, float u)
        {
            float c = Crista, w = MeiaCrista;
            const float altura = 9.2f;
            // os trilhos nas calçadas
            foreach (float s in new[] { -1f, 1f })
                d.Caixa(L(665f, c + 0.26f, s * (w - 0.8f)), new Vector3(140f, 0.12f, 0.16f), "Metal");
            foreach (float s in new[] { -1f, 1f })
            {
                float v = s * (w - 0.8f);
                // o truque (as rodas) e as duas pernas de cada lado
                d.Caixa(L(u, c + 0.65f, v), new Vector3(9f, 0.8f, 0.9f), "Ferrugem", true);
                foreach (float du in new[] { -3.6f, 3.6f })
                    d.Caixa(L(u + du, c + 1f + altura * 0.5f, v), new Vector3(0.55f, altura, 0.6f), "PlacaAmarela", true);
                d.Caixa(L(u, c + 1f + altura - 0.4f, v), new Vector3(8f, 0.8f, 0.7f), "PlacaAmarela");
                d.Entre(L(u - 3.6f, c + 1.3f, v), L(u + 3.6f, c + altura, v), 0.18f, 0.18f, "Ferrugem");
            }
            // as vigas de cima, da calçada de jusante até 9 m por cima do lago
            foreach (float du in new[] { -3f, 3f })
                d.Caixa(L(u + du, c + altura + 1.6f, (-15f + w) * 0.5f), new Vector3(0.9f, 1.2f, w + 15f), "PlacaAmarela");
            d.Caixa(L(u, c + altura + 1.6f, -14.6f), new Vector3(6.9f, 1.0f, 0.6f), "Ferrugem");
            d.Caixa(L(u, c + altura + 1.2f, 0f), new Vector3(6.9f, 0.5f, 0.5f), "Ferrugem");
            // o carro do guincho, os cabos e o gancho (parado em cima de uma das grades)
            d.Caixa(L(u, c + altura + 2.9f, -10.5f), new Vector3(7.4f, 1.4f, 3f), Cor.PorNormal("Ferrugem", "PlacaAmarela"));
            float yGancho = Nivel + 1.6f;
            foreach (float du in new[] { -0.35f, 0.35f })
                d.Caixa(L(u + du, (c + altura + 2.2f + yGancho) * 0.5f, -10.5f), new Vector3(0.05f, c + altura + 2.2f - yGancho, 0.05f), "Grade");
            d.Caixa(L(u, yGancho - 0.4f, -10.5f), new Vector3(1f, 1.1f, 0.6f), "Metal");
            d.Caixa(L(u, yGancho - 1.2f, -10.5f), new Vector3(0.25f, 0.6f, 0.25f), "Ferrugem");
            // a cabine do operador, pendurada do lado de jusante
            d.Caixa(L(u + 3.9f, c + altura - 0.3f, w - 0.8f), new Vector3(2f, 2.2f, 2f), Cor.PorNormal("PlacaAmarela", "PlacaAmarela"), true);
            d.Caixa(L(u + 4.91f, c + altura - 0.1f, w - 0.8f), new Vector3(0.04f, 0.9f, 1.5f), "VidroSujo");
        }

        // a torre do elevador colada na face de jusante: do pé até um pouco acima da crista, com a porta da calçada
        void TorreDoElevador(Desenho d, float u)
        {
            float c = Crista, w = MeiaCrista;
            const float v0 = 6f, v1 = 13f, topo = 4.2f;
            float pe = AlturaDoPe - 1f;
            d.Caixa(L(u, (pe + c + topo) * 0.5f, (v0 + v1) * 0.5f), new Vector3(8f, c + topo - pe, v1 - v0), "Concreto", true);
            d.Caixa(L(u, c + topo + 0.2f, (v0 + v1) * 0.5f), new Vector3(8.6f, 0.4f, v1 - v0 + 0.6f), "Bunker");
            // as janelinhas da escada (escuras; umas sem o vidro)
            for (float y = pe + 6f; y < c - 2f; y += 6f)
                d.Quad(L(u - 0.4f, y + 2.4f, v1 + 0.02f), L(u + 0.4f, y + 2.4f, v1 + 0.02f), L(u + 0.4f, y, v1 + 0.02f), L(u - 0.4f, y, v1 + 0.02f), Vector3.back, (int)y % 3 == 0 ? "Interior" : "VidroSujo");
            // a porta do pé e a da crista
            d.Quad(L(u - 0.75f, pe + 1f + 2.4f, v1 + 0.03f), L(u + 0.75f, pe + 1f + 2.4f, v1 + 0.03f), L(u + 0.75f, pe + 1f, v1 + 0.03f), L(u - 0.75f, pe + 1f, v1 + 0.03f), Vector3.back, "Ferrugem");
            d.Caixa(L(u, pe + 1f + 2.6f, v1 + 0.4f), new Vector3(2.4f, 0.2f, 0.8f), "Concreto");
            d.Quad(L(u - 0.75f, c + 0.2f + 2.4f, v0 - 0.02f), L(u + 0.75f, c + 0.2f + 2.4f, v0 - 0.02f), L(u + 0.75f, c + 0.2f, v0 - 0.02f), L(u - 0.75f, c + 0.2f, v0 - 0.02f), Vector3.forward, "Ferrugem");
            d.Letreiro("ELEVATOR", L(u, c + 2.85f, v0 - 0.02f), Vector3.left, Vector3.up, Vector3.forward, 0.22f, "PlacaBranca", 0.02f);
            // a hera subindo pela torre
            Hera(d, L(u + 4.02f, pe + 1f, (v0 + v1) * 0.5f), Vector3.right, Vector3.forward, 4.5f, 14f, 21);
        }

        // a boca de uma galeria de inspeção no pé da barragem (a porta de aço fechada)
        void BocaDaGaleria(Desenho d, float u, string nome)
        {
            float yb = BaseDoBloco(u - 5f, u + 5f);
            float vT = VDeJusante(Mathf.Max(yb, AlturaDoPe - 0.5f));
            float pe = AlturaDoPe - 0.3f;
            float frente = vT + 3f;
            d.Caixa(L(u, pe + 2.6f, frente - 4f), new Vector3(7f, 5.4f, 8f), Cor.PorNormal("Concreto", "ConcretoClaro"), true);
            d.Quad(L(u - 1.3f, pe + 3f, frente + 0.02f), L(u + 1.3f, pe + 3f, frente + 0.02f), L(u + 1.3f, pe, frente + 0.02f), L(u - 1.3f, pe, frente + 0.02f), Vector3.back, "Ferrugem");
            for (int k = 0; k < 6; k++)
            {
                float x = u - 1.65f + k * 0.66f;
                d.Quad(L(x, pe + 3.45f, frente + 0.03f), L(x + 0.33f, pe + 3.45f, frente + 0.03f), L(x + 0.33f, pe + 3.15f, frente + 0.03f), L(x, pe + 3.15f, frente + 0.03f), Vector3.back, "PlacaAmarela");
            }
            d.Letreiro(nome, L(u, pe + 3.75f, frente), Vector3.right, Vector3.up, Vector3.back, 0.35f, "PlacaBranca", 0.03f);
        }

        // uns pedaços de hera numa parede: as manchas em degraus subindo de 'pe' (na direção 'cima' da parede, 'fora' dela)
        static void Hera(Desenho d, Vector3 pe, Vector3 fora, Vector3 lado, float largura, float altura, int semente)
        {
            var sorte = new System.Random(semente);
            int faixas = Mathf.Max(2, Mathf.RoundToInt(altura / 1.6f));
            float meia = largura * 0.5f;
            for (int k = 0; k < faixas; k++)
            {
                float y0 = k * altura / faixas, y1 = (k + 1) * altura / faixas;
                float t = 1f - k / (float)faixas;
                float a = -meia * t * (0.6f + 0.4f * (float)sorte.NextDouble()), b = meia * t * (0.6f + 0.4f * (float)sorte.NextDouble());
                float sai = 0.03f + 0.01f * k;
                d.Quad(pe + lado * a + Vector3.up * y0 + fora * sai, pe + lado * b + Vector3.up * y0 + fora * sai,
                       pe + lado * b + Vector3.up * y1 + fora * sai, pe + lado * a + Vector3.up * y1 + fora * sai, fora, k % 3 == 2 ? "Musgo" : "Hera");
            }
        }

        // ------------------------------------------------------------------ o canal de fuga

        // a bacia de dissipação e o poço na frente da casa de força: o fundo, os blocos, a soleira, os muros e a água parada
        void MontarOCanalDeFuga(List<PecaDaUsina> saida)
        {
            var d = new Desenho(L(605f, FundoDaFuga, 100f));
            float ini = InicioDoVertedouro, fim = FimDoVertedouro, f = FundoDaFuga, topo = AlturaDoPe + 2f;
            var bacia = Bacia;
            var poco = Poco;
            var corDoMuro = Cor.PorNormal("Barreira", "Concreto");
            // o fundo
            d.Quad(L(ini, f, 44f), L(fim, f, 44f), L(fim, f, 80f), L(ini, f, 80f), Vector3.up, "Bunker", true);
            d.Quad(L(poco.xMin, f, poco.yMin), L(poco.xMax, f, poco.yMin), L(poco.xMax, f, poco.yMax + Muro), L(poco.xMin, f, poco.yMax + Muro), Vector3.up, "Bunker", true);
            // os blocos amortecedores e a soleira do fim da bacia
            for (int fila = 0; fila < 2; fila++)
                for (int k = 0; k < 7; k++)
                {
                    float u = ini + 7f + k * ((fim - ini - 14f) / 6f) + (fila == 1 ? 6f : 0f);
                    if (u > fim - 3f) continue;
                    d.Caixa(L(u, f + 1f, 53f + fila * 7f), new Vector3(2.2f, 2f, 2.2f), "Concreto", true);
                }
            d.Caixa(L((ini + fim) * 0.5f, f + 0.9f, 76f), new Vector3(fim - ini, 1.8f, 4f), "Concreto", true);
            // os muros: os da bacia, o do lado da casa de força e os do poço, com a abertura para o rio
            void Parede(float u0, float u1, float v0, float v1) =>
                d.Caixa(L((u0 + u1) * 0.5f, (f - 2f + topo) * 0.5f, (v0 + v1) * 0.5f), new Vector3(u1 - u0, topo - f + 2f, v1 - v0), corDoMuro, true);
            Parede(ini - UsinaDeLakewood.Muro, ini, bacia.yMin, poco.yMax + UsinaDeLakewood.Muro);
            Parede(fim, fim + UsinaDeLakewood.Muro, bacia.yMin, 80f);
            Parede(fim + UsinaDeLakewood.Muro, CasaDeForca.xMin, 78f, 80f);
            Parede(poco.xMax, poco.xMax + UsinaDeLakewood.Muro, 78f, poco.yMax + UsinaDeLakewood.Muro);
            Parede(ini, SaidaDoRio.x, poco.yMax, poco.yMax + UsinaDeLakewood.Muro);
            Parede(SaidaDoRio.y, poco.xMax, poco.yMax, poco.yMax + UsinaDeLakewood.Muro);
            // o limo na linha d'água dos muros
            foreach (var (u0, u1, v, fora) in new[] { (fim + UsinaDeLakewood.Muro, CasaDeForca.xMin, 80.02f, Vector3.back), (ini, SaidaDoRio.x, poco.yMax - 0.02f, Vector3.forward), (SaidaDoRio.y, poco.xMax, poco.yMax - 0.02f, Vector3.forward) })
                d.Quad(L(u0, NivelDaFuga + 0.7f, v), L(u1, NivelDaFuga + 0.7f, v), L(u1, NivelDaFuga - 0.6f, v), L(u0, NivelDaFuga - 0.6f, v), fora, "Musgo");
            // um guarda-corpo de tubo em cima dos muros do poço
            foreach (var (u0, u1, v) in new[] { (ini - 1f, SaidaDoRio.x, poco.yMax + 1f), (SaidaDoRio.y, poco.xMax + 1f, poco.yMax + 1f) })
            {
                for (float u = u0 + 0.5f; u < u1; u += 3f) d.Caixa(L(u, topo + 0.55f, v), new Vector3(0.08f, 1.1f, 0.08f), "Ferrugem");
                d.Caixa(L((u0 + u1) * 0.5f, topo + 1.05f, v), new Vector3(u1 - u0, 0.07f, 0.07f), "Ferrugem");
                d.Colide(L((u0 + u1) * 0.5f, topo + 0.55f, v), new Vector3(u1 - u0, 1.1f, 0.3f));
            }
            saida.Add(Peca("Canal de fuga", d));

            // a água parada (o material da água do lago: o R é a fundura, o UV o xz do mundo)
            var v3 = new List<Vector3>();
            var cores = new List<Color32>();
            var uv = new List<Vector2>();
            void Quad(Rect r)
            {
                var a = Mundo(r.xMin, r.yMin);
                var b = Mundo(r.xMax, r.yMin);
                var cc = Mundo(r.xMax, r.yMax);
                var dd = Mundo(r.xMin, r.yMax);
                var pts = new[] { a, dd, cc, a, cc, b };
                foreach (var p in pts)
                {
                    v3.Add(new Vector3(p.x, NivelDaFuga, p.y));
                    cores.Add(new Color32(255, 0, 0, 255));
                    uv.Add(p);
                }
            }
            Quad(bacia);
            Quad(poco);
            // o sentido dos triângulos para cima
            for (int i = 0; i < v3.Count; i += 3)
            {
                var n = Vector3.Cross(v3[i + 1] - v3[i], v3[i + 2] - v3[i]);
                if (n.y < 0f)
                {
                    (v3[i + 1], v3[i + 2]) = (v3[i + 2], v3[i + 1]);
                    (uv[i + 1], uv[i + 2]) = (uv[i + 2], uv[i + 1]);
                }
            }
            var normais = new Vector3[v3.Count];
            var tri = new int[v3.Count];
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            for (int i = 0; i < v3.Count; i++)
            {
                normais[i] = Vector3.up;
                tri[i] = i;
                min = Vector3.Min(min, v3[i]);
                max = Vector3.Max(max, v3[i]);
            }
            var agua = new MalhaDoBloco { Vertices = v3.ToArray(), Normais = normais, Cores = cores.ToArray(), Uvs = uv.ToArray(), Triangulos = tri };
            agua.Limites = new Bounds((min + max) * 0.5f, max - min + Vector3.up);
            saida.Add(new PecaDaUsina { Nome = "Água do canal de fuga", Tipo = TipoDaPecaDaUsina.Agua, Agua = agua });
        }
    }
}
