using UnityEngine;
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
            var naVaga = parede.Vaga(indice);
            if (naVaga == null || naVaga.Value == quem.Atual) return null;
            return $"[E] Pegar {Armas.Dados(naVaga.Value).nomeComArtigo} (deixa {quem.Dados.nomeComArtigo})";
        }

        public override void Usar(ArmaJogador quem) => parede.PedirTroca(indice);
    }
}
