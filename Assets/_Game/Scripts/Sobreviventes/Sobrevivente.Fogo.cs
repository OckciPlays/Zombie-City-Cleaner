using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O morador no fogo do molotov (11/11/2026, a etapa 4 do ramo Fogo; no anfitrião): antes de qualquer outra decisão, quem
    /// está numa área de fogo (ou na beira dela) corre para fora, para o lado contrário do meio. O dano é o da área (o
    /// ConstrucoesNoMundo.Molotov.cs).
    /// </summary>
    public partial class Sobrevivente
    {
        float _saindoDoFogoAte;

        // true: está saindo do fogo (não pensa em mais nada agora)
        bool SairDoFogo(float agora)
        {
            var c = ConstrucoesNoMundo.Instancia;
            if (c == null || !c.AreaDeFogoEm(transform.position, 0.6f, out var centro, out float raio))
                return agora < _saindoDoFogoAte && agente.isOnNavMesh && agente.hasPath && agente.remainingDistance > 0.5f;
            Vector3 fora = transform.position - centro;
            fora.y = 0f;
            if (fora.sqrMagnitude < 0.01f) fora = -transform.forward;
            Vector3 alvo = centro + fora.normalized * (raio + 2.5f);
            if (!NavMesh.SamplePosition(alvo, out var hit, 2f, NavMesh.AllAreas)) return false;
            Ir(hit.position, 4f, 0.3f);
            _saindoDoFogoAte = agora + 2f;
            return true;
        }
    }
}
