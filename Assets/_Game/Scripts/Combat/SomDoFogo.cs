using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os sons do molotov (11/11/2026, a etapa 4 do ramo Fogo) e do galão estourando: o vidro quebrando, a labareda pegando e
    /// o estouro, todos gravados (Resources/Fogo). (20/11/2026: os feitos em código, a reserva dos gravados, saíram; sem a
    /// gravação, o som não toca.)
    /// </summary>
    public static class SomDoFogo
    {
        /// <summary>O galão da cerca de fogo estourando com o tiro (12/11/2026): o gravado (null sem ele).</summary>
        public static AudioClip Estouro => SonsGravados.Um("Fogo", "estouro_");   // (20/11/2026: o gravado)

        /// <summary>O vidro da garrafa quebrando no chão (o gravado, sorteado; null sem ele).</summary>
        public static AudioClip Vidro => SonsGravados.Um("Fogo", "vidro_");   // (19/11/2026: o gravado, sorteado)

        /// <summary>
        /// A labareda pegando (o combustível acendendo de uma vez). Desde 19/11/2026, uma das labaredas de verdade do
        /// Nature Sound FX, sorteada a cada vez (<see cref="SomDasChamas.Labareda"/>; null sem elas).
        /// </summary>
        public static AudioClip Labareda => SomDasChamas.Labareda();

        /// <summary>A garrafa quebrou ali e o fogo pegou.</summary>
        public static void Quebrar(Vector3 onde)
        {
            Efeitos.Som(Vidro, onde, 0.9f, true, 40f);
            Efeitos.Som(Labareda, onde, 1f, true, 50f);
        }

        /// <summary>O galão da cerca estourando com o tiro: o estouro e, por cima, a labareda grande de verdade (19/11/2026).</summary>
        public static void Estourar(Vector3 onde)
        {
            Efeitos.Som(Estouro, onde, 1f, true, 140f);
            var grande = SomDasChamas.LabaredaGrande();
            if (grande != null) Efeitos.Som(grande, onde, 1f, true, 90f);
        }
    }
}
