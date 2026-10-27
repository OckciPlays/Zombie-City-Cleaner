using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As malhas das estradas de um bloco (Plano_Mapa, Parte M4), em dados (seguras em outra thread; o Mesh sai do
    /// <see cref="MalhaDeObra.ParaMesh"/> na principal), nas cores da paleta:
    ///  - <b>o asfalto</b> de cada segmento cujo meio cai no bloco, em faixas pelo tipo: a rodovia com o acostamento
    ///    asfaltado, as linhas brancas da beira e a amarela tracejada no meio (4 m pintados a cada 12 m); a estrada com as
    ///    linhas mais perto da beira; a de terra numa faixa só, da cor da terra e um pouco mais baixa (onde ela encosta no
    ///    asfalto, o asfalto fica por cima). Saias nas beiradas escondem a espessura. As linhas somem perto dos nós (lá as
    ///    estradas se cobrem);
    ///  - <b>o pátio</b> de cada nó onde estradas se encontram: um disco no plano delas, cobrindo os cantos;
    ///  - <b>o guard-rail</b> (os postes a cada 6 m e a lâmina) onde a <see cref="RedeDeEstradas"/> marcou, menos perto dos
    ///    nós; a lâmina, mais alta e grossa, também vai na malha de colisão.
    /// Como cada ponto da estrada sempre dá a mesma seção, os segmentos de blocos vizinhos se encontram sem fresta.
    /// </summary>
    public static class MalhasDasEstradas
    {
        public sealed class DoBloco
        {
            public MalhaDeObra Desenho, Colisao;
            public bool Vazio => Desenho == null || Desenho.Triangulos == 0;
        }

        /// <summary>Devolve as malhas do bloco ao estoque (depois de virarem Mesh).</summary>
        public static void Devolver(DoBloco r)
        {
            MalhaDeObra.Devolver(r.Desenho);
            MalhaDeObra.Devolver(r.Colisao);
            r.Desenho = r.Colisao = null;
        }

        // as faixas da seção (da esquerda para a direita de quem anda do primeiro ponto para o último): de, até e a cor;
        // a "Faixa" (amarela) é tracejada
        static readonly (float de, float ate, string cor)[] Rodovia =
        {
            (-5f, -3.62f, "Asfalto"), (-3.62f, -3.47f, "ListraBranca"), (-3.47f, -0.08f, "Asfalto"), (-0.08f, 0.08f, "Faixa"),
            (0.08f, 3.47f, "Asfalto"), (3.47f, 3.62f, "ListraBranca"), (3.62f, 5f, "Asfalto"),
        };
        static readonly (float de, float ate, string cor)[] Estrada =
        {
            (-3.75f, -3.6f, "Asfalto"), (-3.6f, -3.48f, "ListraBranca"), (-3.48f, -0.07f, "Asfalto"), (-0.07f, 0.07f, "Faixa"),
            (0.07f, 3.48f, "Asfalto"), (3.48f, 3.6f, "ListraBranca"), (3.6f, 3.75f, "Asfalto"),
        };
        static readonly (float de, float ate, string cor)[] Terra = { (-2.25f, 2.25f, "Terra") };

        /// <summary>Perto dos nós (m, ao longo da estrada) as linhas pintadas somem.</summary>
        const float SemLinhas = 16f;
        /// <summary>Perto dos nós (m) não tem guard-rail (outras estradas chegam ali).</summary>
        const float SemGuardRail = 22f;
        const float Saia = 0.35f;

        static (float de, float ate, string cor)[] Secao(TipoDeEstrada t) => t == TipoDeEstrada.Rodovia ? Rodovia : t == TipoDeEstrada.Estrada ? Estrada : Terra;

        /// <summary>A altura do asfalto acima do chão: a de terra fica mais baixa (onde as duas se cobrem, o asfalto ganha).</summary>
        public static float Acima(TipoDeEstrada t) => t == TipoDeEstrada.Terra ? RedeDeEstradas.AcimaDoChao - 0.03f : RedeDeEstradas.AcimaDoChao;

        public static DoBloco Bloco(DadosDoMundo d, int bx, int bz)
        {
            var r = new DoBloco();
            var rede = d.Rede;
            var lista = rede?.DoBloco(bx, bz);
            if (lista == null) return r;
            r.Desenho = MalhaDeObra.Emprestar(TamanhoDaMalha.Media);
            r.Colisao = MalhaDeObra.Emprestar();
            var estradas = d.Plano.Estradas;
            var pts = new List<Vector3>(4);
            foreach (int ref_ in lista)
            {
                if (ref_ < 0) continue;
                int ei = RedeDeEstradas.EstradaDe(ref_), i = RedeDeEstradas.PontoDe(ref_);
                var e = estradas[ei];
                // o segmento é do bloco onde cai o meio dele
                var meio = (e.Pontos[i] + e.Pontos[i + 1]) * 0.5f;
                var b = d.BlocoDe(meio.x, meio.y);
                if (b.x != bx || b.y != bz) continue;
                Segmento(r, e, i, pts);
            }
            // os pátios dos cruzamentos deste bloco (3 estradas ou mais: lá elas ficam planas)
            foreach (var no in rede.Nos)
            {
                if (no.Trechos.Count < 3) continue;
                var b = d.BlocoDe(no.Posicao.x, no.Posicao.y);
                if (b.x != bx || b.y != bz) continue;
                // dentro das cidades, os cruzamentos são da cidade
                if (d.CidadeEm(no.Posicao.x, no.Posicao.y) != null) continue;
                float raio = 0f;
                bool asfalto = false;
                foreach (int ti in no.Trechos)
                {
                    var tipo = rede.Trechos[ti].Tipo;
                    if (tipo != TipoDeEstrada.Terra) asfalto = true;
                }
                foreach (int ti in no.Trechos)
                {
                    var tipo = rede.Trechos[ti].Tipo;
                    if (asfalto && tipo == TipoDeEstrada.Terra) continue;
                    raio = Mathf.Max(raio, RedeDeEstradas.MeiaLargura(tipo) + 1f);
                }
                var tipoDoPatio = asfalto ? TipoDeEstrada.Estrada : TipoDeEstrada.Terra;
                float y = no.Altura + Acima(tipoDoPatio);
                pts.Clear();
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    pts.Add(new Vector3(no.Posicao.x + Mathf.Cos(a) * raio, y, no.Posicao.y + Mathf.Sin(a) * raio));
                }
                r.Desenho.Face(pts, Vector3.up, asfalto ? "Asfalto" : "Terra");
            }
            return r;
        }

        // a seção no ponto i: o meio (com a altura do asfalto) e a direção para a esquerda (no plano)
        static void Secao(Estrada e, int i, out Vector3 centro, out Vector3 esquerda)
        {
            var p = e.Pontos;
            int a = Mathf.Max(i - 1, 0), b = Mathf.Min(i + 1, p.Count - 1);
            var dir = (p[b] - p[a]).normalized;
            esquerda = new Vector3(-dir.y, 0f, dir.x);
            centro = new Vector3(p[i].x, e.Alturas[i] + Acima(e.Tipo), p[i].y);
        }

        static void Segmento(DoBloco r, Estrada e, int i, List<Vector3> pts)
        {
            Secao(e, i, out var c0, out var l0);
            Secao(e, i + 1, out var c1, out var l1);
            bool linhas = e.AteONo == null || (e.AteONo[i] > SemLinhas && e.AteONo[i + 1] > SemLinhas);
            var secao = Secao(e.Tipo);
            foreach (var (de, ate, corDaFaixa) in secao)
            {
                string cor = corDaFaixa;
                if (!linhas && cor != "Terra") cor = "Asfalto";
                // a seção é simétrica: o "de" e o "até" contam para a esquerda de quem anda
                Vector3 a0 = c0 + l0 * de, b0 = c0 + l0 * ate, a1 = c1 + l1 * de, b1 = c1 + l1 * ate;
                if (cor == "Faixa")
                {
                    // tracejada: o primeiro terço do segmento pintado, o resto asfalto
                    Vector3 a3 = Vector3.Lerp(a0, a1, 1f / 3f), b3 = Vector3.Lerp(b0, b1, 1f / 3f);
                    Quad(r.Desenho, pts, a0, b0, b3, a3, "Faixa");
                    Quad(r.Desenho, pts, a3, b3, b1, a1, "Asfalto");
                }
                else Quad(r.Desenho, pts, a0, b0, b1, a1, cor);
            }
            // as saias das duas beiradas
            float meia = RedeDeEstradas.MeiaLargura(e.Tipo);
            string corDaSaia = e.Tipo == TipoDeEstrada.Terra ? "Terra" : "AsfaltoRemendo";
            foreach (float s in new[] { -meia, meia })
            {
                Vector3 p0 = c0 + l0 * s, p1 = c1 + l1 * s;
                var fora = s > 0 ? l0 : -l0;
                pts.Clear();
                pts.Add(p0); pts.Add(p1); pts.Add(p1 + Vector3.down * Saia); pts.Add(p0 + Vector3.down * Saia);
                r.Desenho.Face(pts, fora, corDaSaia);
            }
            // o guard-rail
            bool longeDoNo = e.AteONo == null || (e.AteONo[i] > SemGuardRail && e.AteONo[i + 1] > SemGuardRail);
            if (!longeDoNo || e.GuardRailEsquerda == null) return;
            if (e.GuardRailEsquerda[i]) GuardRail(r, c0, c1, l0, l1, meia + 0.7f);
            if (e.GuardRailDireita[i]) GuardRail(r, c0, c1, -l0, -l1, meia + 0.7f);
        }

        static void Quad(MalhaDeObra m, List<Vector3> pts, Vector3 a, Vector3 b, Vector3 c, Vector3 d, string cor)
        {
            // dois triângulos (o quadrilátero nem sempre é plano: a estrada sobe e faz curva)
            pts.Clear(); pts.Add(a); pts.Add(b); pts.Add(c);
            m.Face(pts, Vector3.up, cor);
            pts.Clear(); pts.Add(a); pts.Add(c); pts.Add(d);
            m.Face(pts, Vector3.up, cor);
        }

        // um segmento de guard-rail do lado "fora" (unitário, no plano), a "distancia" m do meio da estrada
        static void GuardRail(DoBloco r, Vector3 c0, Vector3 c1, Vector3 fora0, Vector3 fora1, float distancia)
        {
            float chao = -RedeDeEstradas.AcimaDoChao;
            Vector3 p0 = c0 + fora0 * distancia + Vector3.up * chao, p1 = c1 + fora1 * distancia + Vector3.up * chao;
            var dir = p1 - p0;
            float comprimento = dir.magnitude;
            if (comprimento < 0.1f) return;
            var giro = Quaternion.LookRotation(dir / comprimento, Vector3.up);
            var meio = (p0 + p1) * 0.5f;
            // a lâmina (um pouco mais comprida, para emendar na do segmento seguinte) e os postes
            r.Desenho.Caixa(Matrix4x4.TRS(meio + Vector3.up * 0.58f, giro, Vector3.one), new Vector3(0.06f, 0.32f, comprimento + 0.08f), "Cromo");
            foreach (float t in new[] { 0f, 0.5f })
            {
                var p = Vector3.Lerp(p0, p1, t) + (Vector3.Lerp(fora0, fora1, t) * 0.08f);
                r.Desenho.Caixa(Matrix4x4.TRS(p + Vector3.up * 0.38f, Quaternion.LookRotation(Vector3.Lerp(fora0, fora1, t), Vector3.up), Vector3.one), new Vector3(0.1f, 0.76f, 0.1f), "Metal");
            }
            r.Colisao.Caixa(Matrix4x4.TRS(meio + Vector3.up * 0.55f, giro, Vector3.one), new Vector3(0.25f, 1.1f, comprimento + 0.1f), "Metal");
        }
    }
}
