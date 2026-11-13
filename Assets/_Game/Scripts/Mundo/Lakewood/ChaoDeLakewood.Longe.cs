using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As alturas do relevo de longe (10/11/2026, a pedido: "no meio da pista o terreno tá cobrindo ela, mas dá pra
    /// passar"): a malha de longe é o relevo 6 m abaixo, numa grade de 25 m, e fica sempre desenhada por baixo dos blocos de
    /// perto. Onde o chão de perto desce mais que isso (as estradas cortando o morro, os pátios e a crista da usina, o canal
    /// dos rios e os buracos do relevo entre dois pontos da grade), ela aparecia por cima, sem colisão, até a câmera passar
    /// por baixo dela. Agora a grade do mapa todo é feita uma vez e cada vértice é abaixado só o que precisa para a malha
    /// ficar <see cref="FolgaDoLonge"/> abaixo do chão de perto nos pontos de conferência: a grade de 10 m do relevo em
    /// todo o mapa (o relevo menos o detalhe) e, perto das estradas, dos rios e da usina, a de 5 m com a altura de verdade
    /// (a mais baixa entre a exata e a dos blocos de 16 m).
    /// </summary>
    public sealed partial class ChaoDeLakewood
    {
        /// <summary>A distância entre os pontos do relevo de longe (os setores de 3 × 2,5 km têm 120 × 100 quadrados).</summary>
        public const float PassoDeLonge = 25f;
        /// <summary>Quanto o relevo de longe fica abaixo do relevo (os blocos de perto ficam por cima dele).</summary>
        public const float FundoDoLonge = 6f;
        /// <summary>O mínimo que o relevo de longe fica abaixo do chão de perto nos pontos de conferência.</summary>
        public const float FolgaDoLonge = 2f;

        // o detalhe do chão de perto (o Fbm de 1,1 m e a amostra de 0,3 m): o chão fica no máximo isto abaixo do relevo
        const float DetalheMaximo = 1.4f;
        const float PassoDaConferencia = 5f;

        sealed class GradeDeLonge
        {
            public float Passo;
            public int Nx, Nz;
            public float[] Alturas;
        }

        readonly Dictionary<float, GradeDeLonge> _gradesDeLonge = new Dictionary<float, GradeDeLonge>();

        /// <summary>Quantos pontos de conferência e vértices abaixados a última grade de longe teve (para o relatório).</summary>
        public (int pontos, int abaixados, float maisFundo, float segundos) ResumoDoLonge { get; private set; }

        GradeDeLonge Grade(float passo)
        {
            lock (_gradesDeLonge)
            {
                if (_gradesDeLonge.TryGetValue(passo, out var g)) return g;
                g = MontarAGrade(passo);
                _gradesDeLonge[passo] = g;
                return g;
            }
        }

        /// <summary>A altura do relevo de longe no ponto (o mesmo triângulo da malha de <see cref="Longe"/>).</summary>
        public float AlturaDeLonge(float x, float z, float passo = PassoDeLonge)
        {
            var g = Grade(passo);
            float fx = Mathf.Clamp(x / g.Passo, 0f, g.Nx - 1e-4f), fz = Mathf.Clamp(z / g.Passo, 0f, g.Nz - 1e-4f);
            int i = (int)fx, j = (int)fz;
            fx -= i;
            fz -= j;
            int lado = g.Nx + 1;
            float h00 = g.Alturas[j * lado + i], h01 = g.Alturas[(j + 1) * lado + i], h10 = g.Alturas[j * lado + i + 1], h11 = g.Alturas[(j + 1) * lado + i + 1];
            return fz >= fx ? h00 * (1f - fz) + h01 * (fz - fx) + h11 * fx : h00 * (1f - fx) + h10 * (fx - fz) + h11 * fz;
        }

        // a altura de um vértice da grade de longe (i, j: os índices no mapa todo)
        float VerticeDeLonge(float passo, int i, int j)
        {
            var g = Grade(passo);
            i = Mathf.Clamp(i, 0, g.Nx);
            j = Mathf.Clamp(j, 0, g.Nz);
            return g.Alturas[j * (g.Nx + 1) + i];
        }

        GradeDeLonge MontarAGrade(float passo)
        {
            var t0 = DateTime.Now;
            var g = new GradeDeLonge { Passo = passo, Nx = Mathf.Max(1, Mathf.CeilToInt(Mapa.largura / passo - 1e-3f)), Nz = Mathf.Max(1, Mathf.CeilToInt(Mapa.comprimento / passo - 1e-3f)) };
            int lado = g.Nx + 1;
            var alt = g.Alturas = new float[lado * (g.Nz + 1)];
            for (int j = 0; j <= g.Nz; j++)
            for (int i = 0; i <= g.Nx; i++)
                alt[j * lado + i] = Relevo.Altura(i * passo, j * passo) - FundoDoLonge;

            // os pontos de conferência perto do que mexe no chão (a grade de 5 m, num mapa de bits): as estradas (a pista, o
            // acostamento e o talude, até 34 m), os rios (o canal, o barranco e a faixa de 20 m depois) e a caixa da usina
            int mx = Mathf.CeilToInt(Mapa.largura / PassoDaConferencia) + 1, mz = Mathf.CeilToInt(Mapa.comprimento / PassoDaConferencia) + 1;
            var marca = new bool[mx * mz];
            int marcados = 0;
            void Marcar(Vector2 c, float raio)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((c.x - raio) / PassoDaConferencia)), i1 = Mathf.Min(mx - 1, Mathf.CeilToInt((c.x + raio) / PassoDaConferencia));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((c.y - raio) / PassoDaConferencia)), j1 = Mathf.Min(mz - 1, Mathf.CeilToInt((c.y + raio) / PassoDaConferencia));
                float r2 = raio * raio;
                for (int j = j0; j <= j1; j++)
                {
                    float dz = j * PassoDaConferencia - c.y;
                    for (int i = i0; i <= i1; i++)
                    {
                        float dx = i * PassoDaConferencia - c.x;
                        if (dx * dx + dz * dz > r2 || marca[j * mx + i]) continue;
                        marca[j * mx + i] = true;
                        marcados++;
                    }
                }
            }
            foreach (var e in Estradas)
            {
                float raio = e.MeiaLargura + Acostamento + 34f + 3f;
                for (int k = 0; k < e.Pontos.Length; k++)
                    if (!e.Ponte[k] || (k > 0 && !e.Ponte[k - 1]) || (k + 1 < e.Pontos.Length && !e.Ponte[k + 1])) Marcar(e.Pontos[k], raio);
            }
            for (int r = 0; r < Mapa.rios.Count; r++)
            {
                var linha = Relevo.LinhasDosRios[r];
                if (linha == null) continue;
                float meia = Mathf.Max(Mapa.rios[r].larguraNoComeco, Mapa.rios[r].larguraNoFim) * 0.5f;
                for (int s = 0; s < linha.Count; s++)
                {
                    Marcar(linha[s], meia + 45f);
                    // os pontos da linha podem ficar longe uns dos outros: o meio também
                    if (s > 0 && (linha[s] - linha[s - 1]).sqrMagnitude > 400f)
                    {
                        int partes = Mathf.CeilToInt(Vector2.Distance(linha[s], linha[s - 1]) / 20f);
                        for (int q = 1; q < partes; q++) Marcar(Vector2.Lerp(linha[s - 1], linha[s], q / (float)partes), meia + 45f);
                    }
                }
            }
            // o terreiro da casa da partida, com o talude
            if (Partida != null) Marcar(Partida.Meio, Partida.Raio + 3f);
            // os patamares dos lotes das cidades, com o talude
            foreach (var l in Lotes)
                if (l.Ocupado) Marcar(l.Lote.meio, 0.5f * Mathf.Sqrt(l.Lote.largura * l.Lote.largura + l.Lote.fundo * l.Lote.fundo) + l.Talude + 3f);
            if (Usina != null && Usina.Tem)
            {
                var caixa = Usina.CaixaNoMapa;
                int i0 = Mathf.Max(0, Mathf.FloorToInt(caixa.xMin / PassoDaConferencia)), i1 = Mathf.Min(mx - 1, Mathf.CeilToInt(caixa.xMax / PassoDaConferencia));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(caixa.yMin / PassoDaConferencia)), j1 = Mathf.Min(mz - 1, Mathf.CeilToInt(caixa.yMax / PassoDaConferencia));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (!marca[j * mx + i] && Usina.NaCaixa(i * PassoDaConferencia, j * PassoDaConferencia))
                    {
                        marca[j * mx + i] = true;
                        marcados++;
                    }
            }
            var pontos = new Vector2[marcados];
            var chao = new float[marcados];
            int np = 0;
            for (int j = 0; j < mz; j++)
            for (int i = 0; i < mx; i++)
                if (marca[j * mx + i]) pontos[np++] = new Vector2(i * PassoDaConferencia, j * PassoDaConferencia);
            // a altura de verdade em cada um (em várias threads: o chão é código puro; sem pegar todos os núcleos, porque o
            // resto do Lakewood monta ao mesmo tempo na thread principal)
            var opcoes = new ParallelOptions { MaxDegreeOfParallelism = Mathf.Max(1, Environment.ProcessorCount - 2) };
            Parallel.For(0, (pontos.Length + 1023) / 1024, opcoes, bloco =>
            {
                int fim = Mathf.Min(pontos.Length, (bloco + 1) * 1024);
                // o chão de perto mais baixo: o exato ou o dos blocos de 16 m (que alarga o canal dos rios e baixa a beira
                // da pista), para o relevo de longe ficar embaixo dos dois
                for (int k = bloco * 1024; k < fim; k++) chao[k] = Mathf.Min(Altura(pontos[k].x, pontos[k].y), Altura(pontos[k].x, pontos[k].y, 16f));
            });

            // abaixa os vértices: em cada ponto acima do limite, os três do triângulo, cada um pelo peso dele (o menor
            // abaixamento que põe o ponto no limite); só abaixar não estraga um ponto já conferido, então uma passada basta
            void Conferir(float x, float z, float limite)
            {
                if (x < 0f || z < 0f || x > Mapa.largura || z > Mapa.comprimento) return;
                float fx = Mathf.Clamp(x / passo, 0f, g.Nx - 1e-4f), fz = Mathf.Clamp(z / passo, 0f, g.Nz - 1e-4f);
                int i = (int)fx, j = (int)fz;
                fx -= i;
                fz -= j;
                int a = j * lado + i, b, c;
                float wa, wb, wc;
                if (fz >= fx) { b = (j + 1) * lado + i; c = (j + 1) * lado + i + 1; wa = 1f - fz; wb = fz - fx; wc = fx; }
                else { b = j * lado + i + 1; c = (j + 1) * lado + i + 1; wa = 1f - fx; wb = fx - fz; wc = fz; }
                float delta = wa * alt[a] + wb * alt[b] + wc * alt[c] - limite;
                if (delta <= 0f) return;
                float s = delta / (wa * wa + wb * wb + wc * wc);
                alt[a] -= s * wa;
                alt[b] -= s * wb;
                alt[c] -= s * wc;
            }
            // o relevo em todo o mapa (os buracos e os pés dos penhascos entre dois vértices): nos pontos da grade do relevo
            // e no meio deles (o relevo é bilinear entre os pontos: o mais baixo de cada quadrado fica num canto)
            int nr = 0;
            float pr = Relevo.Passo * 0.5f;
            for (float z = 0f; z <= Mapa.comprimento + 0.01f; z += pr)
            for (float x = 0f; x <= Mapa.largura + 0.01f; x += pr)
            {
                Conferir(x, z, Relevo.Altura(x, z) - DetalheMaximo - FolgaDoLonge);
                nr++;
            }
            // e o chão de verdade perto do que mexe nele
            for (int k = 0; k < pontos.Length; k++) Conferir(pontos[k].x, pontos[k].y, chao[k] - FolgaDoLonge);
            int abaixados = 0;
            float maisFundo = 0f;
            for (int j = 0; j <= g.Nz; j++)
            for (int i = 0; i <= g.Nx; i++)
            {
                float d = Relevo.Altura(i * passo, j * passo) - FundoDoLonge - alt[j * lado + i];
                if (d > 0.01f) abaixados++;
                maisFundo = Mathf.Max(maisFundo, d);
            }
            ResumoDoLonge = (nr + pontos.Length, abaixados, maisFundo, (float)(DateTime.Now - t0).TotalSeconds);
            return g;
        }
    }
}
