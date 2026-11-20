using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Fauna
{
    public sealed partial class FaunaDoMundo
    {
        /// <summary>
        /// Para o som da água (19/11/2026, World/SomDaAgua): o ponto de água está num rio? Sim quando o meio de um rio passa
        /// a menos da metade da largura dele (mais <paramref name="folga"/> m) do ponto, fora dos lagos. As pontes e as
        /// estradas contam (o <see cref="PontoNoRio"/>, o dos peixes, pula essas partes). Só no Lakewood: o mundo gerado não
        /// tem rios.
        /// </summary>
        internal static bool NoRio(Vector3 ponto, float folga = 6f)
        {
            var vista = ChaoDoLakewood;
            if (vista == null) return false;
            var chao = vista.NoMapa;
            var rel = chao.Relevo;
            var linhas = rel.LinhasDosRios;
            if (linhas == null) return false;
            var p = OrigemDeLakewood.NoMapa(ponto);
            for (int r = 0; r < linhas.Length && r < chao.Mapa.rios.Count; r++)
            {
                var L = linhas[r];
                if (L == null) continue;
                var fr = rel.FracoesDosRios[r];
                for (int i = 0; i < L.Count; i++)
                {
                    float dx = L[i].x - p.x, dz = L[i].y - p.z;
                    float w = MapaDeLakewood.LarguraDoRio(chao.Mapa.rios[r], fr[i]) * 0.5f + folga;
                    if (dx * dx + dz * dz > w * w) continue;
                    if (chao.NoLago(L[i].x, L[i].y)) continue;
                    return true;
                }
            }
            return false;
        }

        /// <summary>O teste do som da água: a água parada (fora dos rios) mais perto, em 32 rumos, até 'ate' m.</summary>
        internal static bool AguaParadaMaisPerto(Vector3 de, float ate, out Vector3 ponto)
        {
            ponto = default;
            if (!TemMapa) return false;
            float melhor = float.MaxValue;
            for (int a = 0; a < 32; a++)
            {
                var dir = Quaternion.Euler(0f, a * 11.25f, 0f) * Vector3.forward;
                for (float r = 6f; r < ate && r < melhor; r += 8f)
                {
                    var c = de + dir * r;
                    if (AguaEm(c.x, c.z, out float nivel) < 0.6f || NoRio(c)) continue;
                    melhor = r;
                    ponto = new Vector3(c.x, nivel, c.z);
                    break;
                }
            }
            return melhor < float.MaxValue;
        }
    }
}
