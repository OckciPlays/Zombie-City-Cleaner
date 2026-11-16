using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O MeshCollider novo, vazio (16/11/2026, o teste de desempenho: "às vezes dá um pouco de lag"). O
    /// <c>AddComponent&lt;MeshCollider&gt;()</c> num objeto que já tem um MeshFilter põe a malha dele (a do desenho) no colisor
    /// e a cozinha na hora, no quadro principal, antes de quem chamou trocar pela de colisão. As obras, os andares, o bunker
    /// e o hospital cozinham a colisão numa thread (Physics.BakeMesh), mas a malha do desenho era cozida antes, à toa: medido
    /// andando de carro pelo Lakewood, até 57 ms num quadro (uma casa) e 16 ms (um andar do hospital).
    /// </summary>
    public static class Colisores
    {
        /// <summary>Um MeshCollider sem malha nenhuma (o MeshFilter do objeto sai e volta em volta do AddComponent).</summary>
        public static MeshCollider NovoVazio(GameObject go)
        {
            var filtro = go.GetComponent<MeshFilter>();
            var malha = filtro != null ? filtro.sharedMesh : null;
            if (malha != null) filtro.sharedMesh = null;
            var colisor = go.AddComponent<MeshCollider>();
            colisor.sharedMesh = null;
            if (malha != null) filtro.sharedMesh = malha;
            return colisor;
        }
    }
}
