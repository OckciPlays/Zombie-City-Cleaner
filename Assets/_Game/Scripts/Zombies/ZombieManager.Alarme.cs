using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O alarme de um carro abandonado tocando (Vehicles/PecasDosCarrosDoMundo.Alarme.cs, a pedido em 29/10/2026) chama os
    /// zumbis em volta, a cada poucos segundos enquanto toca. À noite, quem está até o raio vai ver o que foi (cada um num
    /// ponto em volta do carro). De dia e ao amanhecer, ninguém sai da sombra (o Pedro: o tiro de dia já chamava demais):
    /// quem está na sombra vai até a beira dela, do lado do carro, e fica olhando. O alarme não tem dono: ninguém sai
    /// caçando por causa dele, só investigando.
    /// </summary>
    public partial class ZombieManager
    {
        /// <summary>No anfitrião: o alarme tocando neste ponto chama os zumbis até <paramref name="raio"/> m.</summary>
        public static void Alarme(Vector3 ponto, float raio)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned || !m.IsServer) return;
            float agora = Time.time, raio2 = raio * raio;
            bool dia = m.ProtegerDoSol;
            foreach (var z in m._ativos)
            {
                if (z.NivelAtual == Zumbi.Nivel.Longe || z.Morto || z.Petrificado) continue;
                switch (z.Atual)
                {
                    case Zumbi.Estado.Perseguindo:
                    case Zumbi.Estado.Atacando:
                    case Zumbi.Estado.Esquivando:
                    case Zumbi.Estado.Fugindo:
                    case Zumbi.Estado.Abrigando:
                        continue;
                }
                Vector3 p = z.transform.position;
                float d2 = (p - ponto).sqrMagnitude;
                if (d2 > raio2 || BunkerRede.Isolado(ponto, p)) continue;
                Vector2 erro = Random.insideUnitCircle * Mathf.Clamp(Mathf.Sqrt(d2) * 0.2f, 3f, 7f);
                var ver = ponto + new Vector3(erro.x, 0f, erro.y);
                if (dia)
                {
                    if (m.Exposto(p)) continue;   // no sol, ele já está fugindo para a sombra
                    ver = m.BeiraDaSombra(p, ver);
                }
                z.Investigar(ver, agora);
            }
        }

        // de dia: da sombra em direção ao ponto, de 1,5 em 1,5 m (até 40 m), o último passo que ainda é sombra; se o
        // primeiro passo já é sol, ele fica onde está (e olha em volta)
        Vector3 BeiraDaSombra(Vector3 de, Vector3 para)
        {
            Vector3 dir = para - de;
            dir.y = 0f;
            float total = Mathf.Min(dir.magnitude, 40f);
            if (total < 0.5f) return de;
            dir.Normalize();
            Vector3 ultimo = de;
            for (float s = 1.5f; s <= total; s += 1.5f)
            {
                var q = de + dir * s;
                if (!NavMesh.SamplePosition(q, out var hit, 2f, NavMesh.AllAreas)) break;
                if (Exposto(hit.position)) break;
                ultimo = hit.position;
            }
            return ultimo;
        }
    }
}
