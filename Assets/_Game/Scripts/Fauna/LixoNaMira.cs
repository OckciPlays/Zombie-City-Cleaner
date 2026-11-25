using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O lixo da cidade na mira (23/11/2026, a pedido: "também daria para lootear os lixos"): a lata da casa, as sacolas, a
    /// caçamba das lojas e o lixo espalhado pelo guaxinim. Segure E para revirar: o anfitrião sorteia o que se acha
    /// (FaunaDoMundo.Lixo.cs) e guarda dentro do lixo (24/11/2026, a pedido: "o correto seria aparecer uma tela de inventário da
    /// lixeira para ver os itens que há dentro"), e a tela do saque abre com a grade do tamanho dele. Depois, o E abre de novo
    /// o que sobrou lá dentro, na hora. Vazio, o lixo fica revirado.
    /// </summary>
    public sealed class LixoNaMira : Interagivel
    {
        /// <summary>O índice do lixo em <see cref="LixoDaCidade.Pontos"/>.</summary>
        public int indice = -1;
        public TipoDeLixo tipo;
        /// <summary>O lixo espalhado pelo chão (um gatilho largo): revirado e vazio, ele some da mira (não tapa o que está atrás).</summary>
        public bool espalhado;

        bool Valido => indice >= 0 && indice < LixoDaCidade.Pontos.Count;

        // o recipiente com o que sobrou dentro deste lixo (0: nenhum)
        ushort Dentro()
        {
            var m = MochilasNoChao.Instancia;
            if (m == null || !Valido) return 0;
            return m.AcharDoLixo(FaunaDoMundo.ChaveDoLixo(LixoDaCidade.Pontos[indice]), out var r) ? r.id : (ushort)0;
        }

        public override string Dica(ArmaJogador quem)
        {
            if (!Valido) return null;
            if (Dentro() != 0) return tipo == TipoDeLixo.Cacamba ? "[E] Abrir a caçamba" : "[E] Ver o que tem no lixo";
            if (FaunaDoMundo.Revirado(indice)) return espalhado ? null : "Lixo já revirado";
            return tipo == TipoDeLixo.Cacamba ? "[Segure E] Revirar a caçamba" : tipo == TipoDeLixo.Sacolas ? "[Segure E] Revirar as sacolas" : "[Segure E] Revirar o lixo";
        }

        public override void Usar(ArmaJogador quem)
        {
            ushort id = Dentro();
            if (id != 0) TelaDoSaque.Abrir(id);
        }

        // a caçamba é funda: demora mais (o que já foi revirado abre na hora)
        public override float Segurar(ArmaJogador quem) =>
            Valido && Dentro() == 0 && !FaunaDoMundo.Revirado(indice) ? (tipo == TipoDeLixo.Cacamba ? 2.2f : 1.6f) : 0f;

        public override string Segurando(ArmaJogador quem) => "Revirando o lixo";

        public override void Segurado(ArmaJogador quem)
        {
            if (!Valido || quem == null || quem.rede == null) return;
            quem.rede.PedirRevirarLixo(indice);
        }
    }
}
