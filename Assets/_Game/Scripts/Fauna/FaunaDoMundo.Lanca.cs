namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O salmão preso na lança (18/11/2026, a etapa 3 da fogueira; Player/JogadorRede.Salmao.cs): o bicho pelo Id e pela
    /// geração, para o anfitrião ver se o golpe da lança matou um salmão (e então ele some da água e vai para a ponta da lança).
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        /// <summary>O bicho do Id e da geração (null: não existe mais).</summary>
        internal Bicho BichoPorId(ushort id, byte geracao)
        {
            if (id >= MaxBichos) return null;
            var b = _porId[id];
            return b != null && b.Geracao == geracao ? b : null;
        }
    }
}
