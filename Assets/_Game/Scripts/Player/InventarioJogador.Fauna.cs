namespace ZombieCityCleaner.Player
{
    /// <summary>A fauna (12/11/2026): o aviso ao dono depois de carnear (no anfitrião).</summary>
    public partial class InventarioJogador
    {
        /// <summary>Só no anfitrião: o aviso embaixo da mira do dono ("Carneou o veado: ...").</summary>
        public void AvisarDaFauna(string texto) => AvisoRpc(texto);
    }
}
