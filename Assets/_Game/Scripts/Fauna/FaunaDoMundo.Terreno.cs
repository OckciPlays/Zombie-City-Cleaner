using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O mapa para os bichos (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 2): a altura, a
    /// água (a fundura e a altura dela no ponto: no sorteado, o lago só; no Lakewood, o reservatório, os rios e o canal da
    /// usina, cada um na sua altura), a borda e a mancha de cada ponto (o habitat das espécies). No mundo sorteado a mancha é
    /// a da floresta da semente (<see cref="Floresta.ManchaEm"/>); no Lakewood ela sai do mapa dele
    /// (<see cref="ManchaDoLakewood"/>): os quintais e os baldios das cidades, a beira da água, a mata (de coníferas quase
    /// toda, com umas manchas mistas e a rebrota nas clareiras), o campo, a lavoura e a serra.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        static IMundoDoJogo Mapa => MundoDoJogo.Atual;

        static ChaoDeLakewood ChaoDoLakewood => DadosDoMapa == null && LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.Chao : null;

        /// <summary>O mundo da cena tem o mapa para os bichos (o sorteado com a floresta, ou o Lakewood).</summary>
        internal static bool TemMapa => DadosDoMapa?.Floresta != null || ChaoDoLakewood != null;

        /// <summary>A altura do chão no ponto (sem mundo: 'senao').</summary>
        internal static float AlturaDoMapa(float x, float z, float senao = 0f)
        {
            var m = Mapa;
            return m != null ? m.Altura(x, z) : senao;
        }

        /// <summary>A fundura da água no ponto (0 em terra) e a altura da superfície dela.</summary>
        internal static float AguaEm(float x, float z, out float nivel)
        {
            var m = Mapa;
            if (m == null)
            {
                nivel = 0f;
                return 0f;
            }
            return m.FunduraDaAgua(x, z, out nivel);
        }

        /// <summary>A superfície no ponto: a água, se houver, senão o chão dado.</summary>
        internal static float SuperficieEm(float x, float z, float chao)
        {
            float f = AguaEm(x, z, out float nivel);
            return f > 0.01f ? Mathf.Max(chao, nivel) : chao;
        }

        /// <summary>A altura da água no ponto (ou o chão, em terra).</summary>
        internal static float NivelEm(float x, float z)
        {
            float f = AguaEm(x, z, out float nivel);
            return f > 0.01f ? nivel : AlturaDoMapa(x, z);
        }

        /// <summary>O ponto fica dentro do mapa, a 'margem' m da borda (sem mundo, sempre).</summary>
        internal static bool DentroDoMapa(float x, float z, float margem)
        {
            var m = Mapa;
            return m == null || m.Dentro(x, z, margem);
        }

        /// <summary>A mancha do ponto (o habitat). Sem mapa, o campo.</summary>
        internal static Mancha ManchaEm(float x, float z)
        {
            var d = DadosDoMapa;
            if (d?.Floresta != null) return d.Floresta.ManchaEm(x, z, out _);
            var chao = ChaoDoLakewood;
            return chao != null ? ManchaDoLakewood(chao, x, z) : Mancha.Campo;
        }

        /// <summary>
        /// A mancha de um ponto do Lakewood: o lote (o quintal da casa, ou o baldio do lote vazio) e o resto das cidades
        /// (baldio); a beira da água (a até 35 m do lago, dos rios e do canal); e, pela cobertura do mapa, a mata (as
        /// coníferas, com as manchas de uns 250 m da mata mista e a rebrota nas clareiras; acima de 480 m, a serra), o campo,
        /// a lavoura, a rocha (a serra) e a área urbana de fora dos lotes (o baldio).
        /// </summary>
        internal static Mancha ManchaDoLakewood(ChaoDeLakewood chao, float x, float z)
        {
            if (chao.Agua(x, z, out _)) return Mancha.Nada;
            var lote = chao.LoteEm(x, z, 2f);
            if (lote != null) return lote.Plano != null ? Mancha.Quintal : Mancha.Baldio;
            if (chao.NaCidadeComLotes(x, z)) return Mancha.Baldio;
            if (chao.Agua(x, z, out _, 35f)) return Mancha.Beira;
            switch (chao.Mapa.CoberturaEm(x, z))
            {
                case Cobertura.Campo: return Mancha.Campo;
                case Cobertura.Lavoura: return Mancha.Lavoura;
                case Cobertura.Rocha: return Mancha.Serra;
                case Cobertura.Urbano: return Mancha.Baldio;
                case Cobertura.Agua: return Mancha.Beira;
            }
            if (chao.Clareira(x, z) > 0.45f) return Mancha.Rebrota;
            if (chao.Relevo.Altura(x, z) > 480f) return Mancha.Serra;
            float mista = Mathf.PerlinNoise(x * 0.004f + 31.7f, z * 0.004f + 12.3f);
            return mista > 0.58f ? Mancha.Mista : Mancha.Coniferas;
        }
    }
}
