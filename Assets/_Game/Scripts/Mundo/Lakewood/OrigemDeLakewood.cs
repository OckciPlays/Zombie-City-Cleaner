using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A origem do mundo do Lakewood no meio do mapa (16/11/2026, a pedido: "o personagem principal fica meio tremendo em cima
    /// da moto, e outros objetos ficam meio tremendo também"). O mapa tem a origem no canto sudoeste (0 a 12 km no x, 0 a 10
    /// km no z), e a Woodcreek, a casa da partida e o acampamento ficavam a uns 9,6 km dela, onde o float só anda de 1 em 1
    /// mm: medido na moto, o corpo e o guidão tremiam 10 vezes mais que perto da origem, e na GPU tudo o que fica perto da
    /// câmera (as grades, os zumbis) tremia junto.
    /// <para>
    /// Há dois espaços: <b>o do mapa</b>, o de sempre (os dados do <see cref="MapaDeLakewood"/>, o relevo, o ruído, o chão, as
    /// plantas, as obras, as ferramentas, as imagens e os testes: nada disso muda, e o mapa sai igual), e <b>o do mundo</b>,
    /// o do jogo (os transforms, a física, a rede, o save): o do mapa menos (<see cref="X"/>, <see cref="Z"/>). O que é
    /// montado uma vez fica embaixo de uma raiz deslocada (o <c>LakewoodNaCena</c>), com as posições locais no espaço do
    /// mapa; o que cruza de um espaço para o outro (a câmera, os jogadores, os carros, os bichos, os sistemas do jogo pelas
    /// interfaces) passa por aqui.
    /// </para>
    /// </summary>
    public static class OrigemDeLakewood
    {
        /// <summary>O ponto do mapa que fica na origem do mundo: o meio do condado (números redondos: a conta é exata).</summary>
        public const float X = 6000f, Z = 5000f;

        public static readonly Vector3 Meio = new Vector3(X, 0f, Z);

        /// <summary>Do mapa para o mundo (a altura não muda).</summary>
        public static Vector3 NoMundo(Vector3 noMapa) => new Vector3(noMapa.x - X, noMapa.y, noMapa.z - Z);
        public static Vector2 NoMundo(Vector2 noMapa) => new Vector2(noMapa.x - X, noMapa.y - Z);
        public static Rect NoMundo(Rect noMapa) => new Rect(noMapa.x - X, noMapa.y - Z, noMapa.width, noMapa.height);

        /// <summary>Do mundo para o mapa.</summary>
        public static Vector3 NoMapa(Vector3 noMundo) => new Vector3(noMundo.x + X, noMundo.y, noMundo.z + Z);
        public static Vector2 NoMapa(Vector2 noMundo) => new Vector2(noMundo.x + X, noMundo.y + Z);
        public static Bounds NoMapa(Bounds noMundo) => new Bounds(NoMapa(noMundo.center), noMundo.size);
    }

    /// <summary>
    /// O chão do Lakewood visto do mundo do jogo (16/11/2026, <see cref="OrigemDeLakewood"/>): as mesmas perguntas do
    /// <see cref="ChaoDeLakewood"/>, com os pontos do mundo. Para quem está fora do Lakewood (a fauna, a água, a queda, o F1).
    /// </summary>
    public sealed class ChaoNoMundo
    {
        /// <summary>O chão no espaço do mapa (para os dados: os rios, os lotes, o relevo).</summary>
        public readonly ChaoDeLakewood NoMapa;

        public ChaoNoMundo(ChaoDeLakewood chao) => NoMapa = chao;

        const float X = OrigemDeLakewood.X, Z = OrigemDeLakewood.Z;

        public MapaDeLakewood Mapa => NoMapa.Mapa;
        public float Altura(float x, float z, float passo = 0f) => NoMapa.Altura(x + X, z + Z, passo);
        public float AlturaDaMalha(float x, float z) => NoMapa.AlturaDaMalha(x + X, z + Z);
        public bool Agua(float x, float z, out float nivel, float folga = 0f) => NoMapa.Agua(x + X, z + Z, out nivel, folga);
        public bool NoLago(float x, float z) => NoMapa.NoLago(x + X, z + Z);
        public float Clareira(float x, float z) => NoMapa.Clareira(x + X, z + Z);
        public bool NaCidadeComLotes(float x, float z) => NoMapa.NaCidadeComLotes(x + X, z + Z);
        public ChaoDeLakewood.LoteNoChao LoteEm(float x, float z, float folga = 0f) => NoMapa.LoteEm(x + X, z + Z, folga);
        public Cobertura CoberturaEm(float x, float z) => NoMapa.Mapa.CoberturaEm(x + X, z + Z);
        public float AlturaDoRelevo(float x, float z) => NoMapa.Relevo.Altura(x + X, z + Z);
        public string SetorEm(float x, float z) => NoMapa.Mapa.NomeDoSetorEm(x + X, z + Z);

        public bool EstradaPerto(float x, float z, float alcance, out float dist, out float altura, out bool ponte, out float meia) =>
            NoMapa.EstradaPerto(x + X, z + Z, alcance, out dist, out altura, out ponte, out meia);
    }
}
