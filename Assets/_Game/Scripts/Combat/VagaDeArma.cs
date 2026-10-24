using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Uma vaga do <see cref="ParedeDeArmas"/>: o colisor (gatilho) que a mira do jogador procura e a arma em pé nela.
    /// A origem fica na frente do painel, na altura do apoio e no meio da vaga. Cada vaga é de um tipo de arma (o
    /// lugar dela no painel): a arma que o jogador larga volta para uma vaga livre do tipo dela.
    /// </summary>
    public class VagaDeArma : Interagivel
    {
        public ParedeDeArmas parede;
        public int indice;
        [Tooltip("A arma desta vaga (o lugar dela no painel).")]
        public TipoDeArma dono;
        [Tooltip("A arma na vaga (a malha muda com o que está nela).")]
        public MeshFilter filtro;

        public override string Dica(ArmaJogador quem)
        {
            var inventario = quem.Inventario;
            if (inventario == null) return null;
            var naVaga = parede.Vaga(indice);
            var naMao = inventario.NaMao;
            if (naVaga == null)
                return Itens.EhArma(naMao) && Itens.Arma(naMao) == dono ? $"[E] Guardar {Itens.NomeComArtigo(naMao)}" : null;
            var nova = Itens.DaArma(naVaga.Value);
            if (inventario.LugarParaReceber() >= 0) return $"[E] Pegar {Itens.NomeComArtigo(nova)}";
            if (Itens.EhArma(naMao)) return $"[E] Trocar {Itens.NomeComArtigo(naMao)} {Itens.Pelo(nova)}";
            return $"Sem lugar para {Itens.NomeComArtigo(nova)} (G larga {Itens.NomeComArtigo(naMao)})";
        }

        public override void Usar(ArmaJogador quem)
        {
            if (quem.Inventario != null) parede.PedirVaga(indice, quem.Inventario);
        }
    }
}
