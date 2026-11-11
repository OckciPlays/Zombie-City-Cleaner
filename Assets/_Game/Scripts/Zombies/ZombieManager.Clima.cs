using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O clima nos zumbis (a pedido em 02/11/2026; o sol com nuvens fica em ZombieManager.Sol.cs e Zumbi.Sol.cs, e o tiro
    /// abafado pela chuva no <see cref="ZombieManager.Barulho"/>). Aqui, a poça: pisar numa poça da chuva forte faz barulho, e
    /// ele chama só os zumbis mais perto ("atrai os zumbis mais próximos apenas"), que vão ver o que foi.
    /// </summary>
    public partial class ZombieManager
    {
        static readonly List<(float d2, Zumbi z)> _pertoDaPoca = new List<(float, Zumbi)>();

        /// <summary>
        /// No anfitrião (dos passos, Player/PassosJogador): o respingo de um pé numa poça. Os três zumbis mais perto (até 12 m
        /// andando, 18 m correndo; menos com a chuva abafando) vão ver o que foi.
        /// </summary>
        public static void BarulhoDaPoca(Vector3 ponto, JogadorRede autor, bool correndo)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned || !m.IsServer) return;
            float alcance = (correndo ? 18f : 12f) * Mathf.Lerp(1f, Tempo.Ouvido, 0.5f);
            alcance *= 1f - 0.25f * Habilidades.Pontos(autor, Habilidade.PassosLeves);   // (os Passos leves, 06/11/2026: 25% por ponto)
            alcance *= Protecao.FatorDosPassos(autor);   // (o colete e as caneleiras de sucata, 11/11/2026)
            float alcance2 = alcance * alcance;
            _pertoDaPoca.Clear();
            foreach (var z in m._ativos)
            {
                if (z == null || z.Morto || z.Petrificado || z.NivelAtual == Zumbi.Nivel.Longe) continue;
                if (z.Atual == Zumbi.Estado.Perseguindo || z.Atual == Zumbi.Estado.Atacando) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                if (d2 > alcance2 || BunkerRede.Isolado(ponto, z.transform.position)) continue;
                _pertoDaPoca.Add((d2, z));
            }
            _pertoDaPoca.Sort((a, b) => a.d2.CompareTo(b.d2));
            float agora = Time.time;
            for (int i = 0; i < _pertoDaPoca.Count && i < 3; i++)
            {
                var z = _pertoDaPoca[i].z;
                Vector2 erro = Random.insideUnitCircle * 2f;
                var ver = ponto + new Vector3(erro.x, 0f, erro.y);
                if (m.ProtegerDoSol && m.Exposto(ver)) continue;   // de dia, não vai para o sol (o chuvisco ainda tem sol)
                z.Investigar(ver, agora);
            }
        }
    }
}
