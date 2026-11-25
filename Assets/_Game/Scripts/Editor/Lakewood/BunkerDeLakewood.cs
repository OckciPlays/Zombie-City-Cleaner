using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O bunker da comunidade no Lakewood (22/11/2026, a etapa 2 do Docs/23-Campanha-e-Mapa.md; aprovado: "o bunker na
    /// encosta do lago, ao lado do lote 114, onde se nasce hoje"): acha o lugar da pegada do bunker do mundo sorteado
    /// (<see cref="PlantaDeBunker.Pegada"/>, uns 60 × 68 m) a até 260 m da casa da partida, com o portão na beira de uma
    /// estrada (o pátio na altura dela), fora da água, dos lotes, do terreiro e das outras estradas, com o menor corte e
    /// aterro (a encosta subindo atrás do morro até 14 m) e perto da partida, e grava em <see cref="MapaDeLakewood.bunker"/>.
    /// </summary>
    public static class BunkerDeLakewood
    {
        const float Busca = 260f, Passo = 6f, PassoDoGiro = 15f, DesnivelMaximo = 14f;

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: o bunker da comunidade (na encosta da casa da partida)", false, 32)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de pôr o bunker.");
                return;
            }
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || mapa.estradas.Count == 0 || mapa.casaDaPartida == null || !mapa.casaDaPartida.tem)
            {
                Debug.LogWarning("Falta o mapa com as estradas e a casa da partida.");
                return;
            }
            Undo.RecordObject(mapa, "O bunker");
            // o de antes sai (o chão é feito sem ele, para a busca não contar com ele)
            mapa.bunker.tem = false;
            var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
            var relatorio = new StringBuilder();
            if (!Achar(mapa, chao, relatorio, out var novo))
            {
                Debug.LogWarning("[Lakewood] O bunker: nenhum lugar serve. " + relatorio);
                return;
            }
            mapa.bunker = novo;
            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lakewood] O bunker da comunidade: " + relatorio);
        }

        /// <summary>O lugar do bunker perto da casa da partida (a nota: o corte e o aterro, a estrada e a distância).</summary>
        public static bool Achar(MapaDeLakewood mapa, ChaoDeLakewood chao, StringBuilder relatorio, out BunkerDoMapa bunker)
        {
            bunker = null;
            var p0 = mapa.casaDaPartida.naEstrada;
            var peg = PlantaDeBunker.Pegada;
            float melhorNota = float.MaxValue;
            int vistos = 0;
            for (float cx = p0.x - Busca; cx <= p0.x + Busca; cx += Passo)
            for (float cz = p0.y - Busca; cz <= p0.y + Busca; cz += Passo)
            for (float giro = 0f; giro < 360f; giro += PassoDoGiro)
            {
                var rot = Quaternion.Euler(0f, giro, 0f);
                var vx = rot * Vector3.right;
                var vz = rot * Vector3.forward;
                Vector2 W(float lx, float lz) => new Vector2(cx + vx.x * lx + vz.x * lz, cz + vx.z * lx + vz.z * lz);
                // o portão: a frente do pátio no meio do vão da cerca, na beira de uma estrada (fora de uma ponte)
                var frente = W((PlantaDeBunker.VaoDaCercaX0 + PlantaDeBunker.VaoDaCercaX1) * 0.5f, peg.yMin);
                if (!chao.EstradaPerto(frente.x, frente.y, 30f, out float de, out float hEstrada, out bool ponte, out _) || ponte) continue;
                float altura = hEstrada - 0.08f;
                bool serve = true;
                float soma = 0f, maior = 0f;
                int n = 0;
                for (float lx = peg.xMin; lx <= peg.xMax && serve; lx += 5f)
                for (float lz = peg.yMin; lz <= peg.yMax && serve; lz += lz < peg.yMin + 4f ? 2f : 5f)
                {
                    var q = W(lx, lz);
                    // (a beira da frente fica fora do asfalto e do acostamento, 3 m da pista; o resto, mais longe)
                    float folgaDaEstrada = lz < peg.yMin + 4f ? 3.5f : 6f;
                    if (chao.Agua(q.x, q.y, out _, 4f) || chao.LoteEm(q.x, q.y, 3f) != null
                        || (chao.Partida != null && chao.Partida.DistanciaDoTerreiro(q.x, q.y) < 4f)
                        || (chao.EstradaPerto(q.x, q.y, 20f, out float dq, out _, out _, out float mq) && dq < mq + folgaDaEstrada))
                    {
                        serve = false;
                        break;
                    }
                    float d = Mathf.Abs(chao.Relevo.Altura(q.x, q.y) - altura);
                    maior = Mathf.Max(maior, d);
                    soma += d;
                    n++;
                }
                if (!serve || n == 0 || maior > DesnivelMaximo) continue;
                vistos++;
                float nota = soma / n * 10f + de * 3f + Vector2.Distance(new Vector2(cx, cz), p0) * 0.3f;
                if (nota >= melhorNota) continue;
                melhorNota = nota;
                chao.EstradaPerto(frente.x, frente.y, 30f, out _, out _, out _, out _);
                bunker = new BunkerDoMapa { tem = true, meio = new Vector2(cx, cz), giro = giro, altura = altura, estrada = EstradaDe(mapa, chao, frente) };
                relatorio.Clear();
                relatorio.Append($"o meio da caixa em ({cx:F0}; {cz:F0}) do mapa, setor {mapa.NomeDoSetorEm(cx, cz)}, o giro {giro:F0}°, ")
                         .Append($"o pátio a {altura:F1} m (o portão a {de:F1} m da {bunker.estrada}), o desnível até {maior:F1} m, ")
                         .Append($"a {Vector2.Distance(new Vector2(cx, cz), p0):F0} m da casa da partida");
            }
            if (bunker != null) relatorio.Append($"; {vistos} lugares serviam.");
            return bunker != null;
        }

        // o nome da via mais perto do ponto (para o relatório)
        static string EstradaDe(MapaDeLakewood mapa, ChaoDeLakewood chao, Vector2 p)
        {
            string nome = "estrada";
            float menor = float.MaxValue;
            foreach (var e in chao.Estradas)
            {
                if (e.Via == null) continue;
                for (int i = 1; i < e.Pontos.Length; i++)
                {
                    float d = Linhas.DistanciaDoSegmento(p, e.Pontos[i - 1], e.Pontos[i], out _);
                    if (d < menor) { menor = d; nome = e.Via.nome; }
                }
            }
            return nome;
        }
    }
}
