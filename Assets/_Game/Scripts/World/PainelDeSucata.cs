using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Um painel de metal no chão (09/11/2026, a sucata, 2ª leva; <see cref="PaineisDoMundo"/> põe e tira): segurar o E
    /// por <see cref="MundoDaPartida.SegundosDoPainel"/> s desmonta, com o barulho do metal (o anfitrião confere, dá a
    /// sucata e chama os zumbis de perto: <c>MundoDaPartida.Sucata.cs</c>).
    /// </summary>
    public class PainelDeSucata : Interagivel
    {
        /// <summary>O número do painel (o índice em <see cref="PaineisDeSucata.Todos"/>; os do F1, de <see cref="MundoDaPartida.PrimeiroPainelDeTeste"/> em diante).</summary>
        public int indice;
        public byte modelo;
        /// <summary>Já assentou no chão de verdade (o raio para baixo achou a colisão)?</summary>
        internal bool assentado;
        internal int tentativas;
        public bool Assentado => assentado;

        public Vector3 Meio => transform.position + transform.up * 0.06f;

        public override string Dica(ArmaJogador quem) =>
            PaineisDoMundo.Desmontado(indice) ? null
            : $"Segure [E]: desmontar {PaineisDeSucata.NomeComArtigo(modelo)} (vira sucata · faz barulho)";

        public override void Usar(ArmaJogador quem) { }

        public override float Segurar(ArmaJogador quem) => PaineisDoMundo.Desmontado(indice) ? 0f : MundoDaPartida.SegundosDoPainel;

        public override string Segurando(ArmaJogador quem) => $"Desmontando {PaineisDeSucata.NomeComArtigo(modelo)}";

        public override void Segurado(ArmaJogador quem)
        {
            // (12/11/2026) no Lakewood, a rede é a do MundoDeLakewood (MundoDeLakewood.Sucata.cs)
            if (MundoDaPartida.Instancia != null) MundoDaPartida.Instancia.PedirDesmontarPainel(indice);
            else MundoDeLakewood.Instancia?.PedirDesmontarPainel(indice);
        }
    }
}
