namespace ZombieCityCleaner.Zombies
{
    /// <summary>O Long que ainda pode cuspir (o papo inteiro), para a regra do Long da fogueira (ZombieManager.LongNaFogueira.cs).</summary>
    public partial class Zumbi
    {
        /// <summary>No anfitrião: um Long vivo com o papo inteiro (sem ele, não cospe mais e não apaga fogo nenhum).</summary>
        internal bool LongQueCospe => tipo == TipoDeZumbi.Long && !Morto && !_semPapo;
    }
}
