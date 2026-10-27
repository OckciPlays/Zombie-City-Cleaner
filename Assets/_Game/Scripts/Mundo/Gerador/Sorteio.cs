namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O sorteio do gerador (SplitMix64, o WorldRandom do Guardiões da Floresta): a mesma semente dá a mesma sequência em
    /// todas as máquinas e em qualquer thread (o UnityEngine.Random é global e só da thread principal; o System.Random
    /// pode mudar entre versões do .NET).
    ///
    /// Cada etapa da geração tem a sua sequência (<c>Sorteio.Da(semente, "estradas")</c>): mudar uma etapa não mexe no
    /// sorteio das outras. Para o que se espalha por célula ou por construção, use <see cref="Hash"/>: não depende da
    /// ordem em que as coisas são geradas.
    /// </summary>
    public sealed class Sorteio
    {
        ulong _estado;

        public Sorteio(ulong semente) => _estado = semente;

        /// <summary>A sequência própria de uma etapa da geração.</summary>
        public static Sorteio Da(int semente, string etapa) => new Sorteio(Misturar((ulong)(uint)semente ^ Fnv(etapa)));

        public ulong Proximo()
        {
            _estado += 0x9E3779B97F4A7C15UL;
            return Misturar(_estado);
        }

        /// <summary>Um número em [0, 1).</summary>
        public float Fracao() => (Proximo() >> 40) * (1f / 16777216f);

        public float Entre(float min, float max) => min + (max - min) * Fracao();

        /// <summary>Um inteiro em [min, maxExclusivo).</summary>
        public int Entre(int min, int maxExclusivo) =>
            maxExclusivo <= min ? min : min + (int)(Proximo() % (ulong)(maxExclusivo - min));

        public bool Chance(float probabilidade) => Fracao() < probabilidade;

        /// <summary>O embaralhador final do SplitMix64.</summary>
        public static ulong Misturar(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>O hash FNV-1a de um texto (separa as sequências por etapa).</summary>
        public static ulong Fnv(string texto)
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in texto)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>O hash de uma célula (x, y) mais um "sal": a base para sortear coisas sem depender da ordem.</summary>
        public static ulong Hash(int semente, int x, int y, int sal = 0) =>
            Misturar(((ulong)(uint)semente << 32) ^ ((ulong)(uint)x * 0x9E3779B1UL) ^ ((ulong)(uint)y * 0x85EBCA77UL << 17)
                     ^ ((ulong)(uint)sal * 0xC2B2AE3DUL << 7));

        /// <summary>Um número em [0, 1) tirado de um hash.</summary>
        public static float FracaoDoHash(ulong hash) => (hash >> 40) * (1f / 16777216f);
    }
}
