using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O chão dos zumbis: onde o NavMesh do mundo (<see cref="NavMeshDoMundo"/>) monta as áreas em volta dos jogadores e
    /// onde o povoamento (ZombieManager.Povoamento.cs) põe os zumbis. É o mundo sorteado (os <see cref="DadosDoMundo"/> e o
    /// <see cref="TerrenoDoMundo"/>) ou o mapa fixo de Lakewood (o <see cref="LakewoodNaCena"/>; Plano_Lakewood, Etapa 6,
    /// a parte 5). <see cref="ChaoDosZumbis.Atual"/> é o da cena aberta.
    /// </summary>
    public interface IChaoDosZumbis
    {
        /// <summary>A semente (o sorteio das células do povoamento e dos andares).</summary>
        int Semente { get; }
        /// <summary>O chão está ligado (a partida começou).</summary>
        bool Ligado { get; }
        /// <summary>A altura do chão (m).</summary>
        float Altura(float x, float z);
        /// <summary>O ponto é de uma cidade (as células da cidade têm mais zumbis).</summary>
        bool NaCidade(float x, float z);
        /// <summary>O Hospital Principal está neste mapa (os zumbis dele nascem por andar).</summary>
        bool TemHospital { get; }
        /// <summary>O chão com a colisão e as construções de perto estão prontos no círculo (o NavMesh pode ser montado).</summary>
        bool Pronto(Vector3 centro, float raio);
        /// <summary>O que falta para <see cref="Pronto"/>, ou null (a conferência do F1).</summary>
        string QueFalta(Vector3 centro, float raio);
        /// <summary>A água pode estar dentro da caixa (senão a busca da água é pulada).</summary>
        bool PodeTerAgua(Bounds caixa);
        /// <summary>A fundura da água no ponto (0 fora dela) e a altura da superfície.</summary>
        float FunduraDaAgua(float x, float z, out float nivel);
    }

    public static class ChaoDosZumbis
    {
        /// <summary>
        /// O chão da cena aberta: o mundo sorteado da partida ou o Lakewood (null sem nenhum dos dois). Desde 12/11/2026, o
        /// mesmo do <see cref="MundoDoJogo.Atual"/> (o mundo do jogo é o chão dos zumbis e mais).
        /// </summary>
        public static IChaoDosZumbis Atual => MundoDoJogo.Atual;
    }
}
