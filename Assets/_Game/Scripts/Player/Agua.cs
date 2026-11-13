using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A água do mundo (06/11/2026, a pedido: nadar, o fôlego e o afogamento; o caiaque e a pesca): o nível da água num
    /// ponto, no lago do mundo sorteado (<c>DadosDoMundo.FunduraDaAgua</c>) ou no lago e nos rios do Lakewood
    /// (<c>ChaoDeLakewood.Agua</c>). Serve em qualquer máquina (o mundo é o mesmo em todas).
    /// </summary>
    public static class Agua
    {
        /// <summary>Os olhos acima dos pés (m).</summary>
        public const float AlturaDosOlhos = 1.62f;

        /// <summary>Há água no ponto (x, z)? E o nível dela (a altura da superfície).</summary>
        public static bool Nivel(Vector3 p, out float nivel)
        {
            var mundo = MundoDaPartida.Instancia;
            if (mundo != null && mundo.Dados != null)
            {
                var dados = mundo.Dados;
                float f = dados.FunduraDaAgua(p.x, p.z);
                if (f <= 0.01f)
                {
                    nivel = 0f;
                    return false;
                }
                nivel = dados.Altura(p.x, p.z) + f;
                return true;
            }
            var mapa = LakewoodNaCena.Instancia;
            if (mapa != null && mapa.Chao != null && mapa.Chao.Agua(p.x, p.z, out nivel)) return true;
            nivel = 0f;
            return false;
        }

        /// <summary>A fundura da água no ponto (do fundo à superfície; 0 em terra).</summary>
        public static float Fundura(Vector3 p)
        {
            var mundo = MundoDaPartida.Instancia;
            if (mundo != null && mundo.Dados != null) return Mathf.Max(0f, mundo.Dados.FunduraDaAgua(p.x, p.z));
            if (!Nivel(p, out float nivel)) return 0f;
            var mapa = LakewoodNaCena.Instancia;
            return mapa != null && mapa.Chao != null ? Mathf.Max(0f, nivel - mapa.Chao.Altura(p.x, p.z)) : 1f;
        }

        /// <summary>Os olhos de quem está com os pés em 'pes' estão embaixo da água?</summary>
        public static bool Submerso(Vector3 pes) => Nivel(pes, out float nivel) && pes.y + AlturaDosOlhos < nivel - 0.05f;
    }
}
