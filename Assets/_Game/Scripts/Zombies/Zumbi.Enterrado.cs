namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Brutamonte que saiu da terra perto de alguém (ZombieManager.Enterrado.cs, 05/11/2026): ao acabar de sair, se o olho
    /// (nas costas, virado para quem o acordou) vê alguém, ele trava nele na hora (o rugido, o preparo e o avanço). Sem isso,
    /// de perto, ele andava até o ponto visto, virava a frente para lá e o olho perdia o jogador antes de travar: ficava
    /// parado ao lado dele.
    /// </summary>
    public partial class Zumbi
    {
        /// <summary>No anfitrião: o Brutamonte já saiu da terra e procura (ainda não travou em ninguém).</summary>
        internal bool BrutamonteProcurando => tipo == TipoDeZumbi.Brutamonte && !Morto && FaseBruto == FaseDoBruto.Procurando;

        /// <summary>No anfitrião: se o olho vê alguém agora, trava nele. Devolve se travou.</summary>
        internal bool TravarSeOlhoVe()
        {
            var visto = JogadorNoOlho(A.brutamonte);
            if (visto == null) return false;
            Travar(visto);
            return true;
        }
    }
}
