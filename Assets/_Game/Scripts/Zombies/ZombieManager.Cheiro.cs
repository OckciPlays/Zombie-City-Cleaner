using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O cheiro (12/11/2026, a etapa 4 da fauna: o espirro do gambá). Como um barulho que não se ouve: os zumbis a até
    /// 'raio' m vão ver o ponto (cada um num lugar um pouco diferente), mas nada assusta os bichos e ninguém é caçado na
    /// hora. A chuva não abafa o cheiro; o sol de dia ainda segura quem está na sombra.
    /// </summary>
    public partial class ZombieManager
    {
        public static void Cheiro(Vector3 ponto, float raio)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned || !m.IsServer) return;
            float agora = Time.time, raio2 = raio * raio;
            foreach (var z in m._ativos)
            {
                if (z.NivelAtual == Zumbi.Nivel.Longe || z.Morto || z.Atual == Zumbi.Estado.Perseguindo || z.Atual == Zumbi.Estado.Atacando) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                if (d2 > raio2 || BunkerRede.Isolado(ponto, z.transform.position)) continue;
                Vector2 erro = Random.insideUnitCircle * Mathf.Clamp(Mathf.Sqrt(d2) * 0.15f, 2f, 6f);
                var ver = ponto + new Vector3(erro.x, 0f, erro.y);
                if (m.ProtegerDoSol && m.Exposto(ver)) continue;
                z.Investigar(ver, agora);
            }
        }
    }
}
