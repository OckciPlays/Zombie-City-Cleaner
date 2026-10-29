using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Um dos corpos do jogador ("Corpo_&lt;modelo&gt;", um por personagem, todos no prefab e só o escolhido ligado): o que o
    /// <see cref="JogadorRede"/> usa dele (o Animator, os renderers, os ossos que acompanham o olhar, o que está na mão e
    /// os controladores de cada arma) e as medidas do corpo em repouso para o <see cref="EquipamentoNoCorpo"/> (a mochila,
    /// o coldre e as armas penduradas), tiradas da malha no Blender (personagens.py, "equipamento"). Montado pela
    /// montagem do jogador.
    /// </summary>
    public class CorpoDoPersonagem : MonoBehaviour
    {
        public Animator animador;
        [Tooltip("O corpo e o que está na mão (as armas e os itens dentro do Arma_TP).")]
        public Renderer[] renderers;
        public Transform ossoPeito;
        public Transform ossoCabeca;
        [Tooltip("As armas e os itens dentro do Arma_TP (na ordem de Itens.Modelos).")]
        public GameObject[] naMao;
        [Tooltip("A boca do cano de cada arma (na ordem de TipoDeArma).")]
        public Transform[] bocas;
        [Tooltip("O controlador do corpo com cada arma (os override com as animações deste personagem).")]
        public RuntimeAnimatorController[] controladores;
        public RuntimeAnimatorController controladorDasMaos;
        [Tooltip("O corpo com a lata e com a garrafa na mão (na ordem de Itens.Modelos, a partir de Armas.Quantas); vazio: o dos punhos.")]
        public RuntimeAnimatorController[] controladoresDosItens;
        [Tooltip("O corpo com o taco de sinuca em pé na mão direita.")]
        public RuntimeAnimatorController controladorDoTaco;
        [Tooltip("O taco na mão (o vazio Arma_Taco, no weapon.R: a pegada) e a ponta dele (Boca_Taco); o TacoNaMao põe o taco de cada cor entre os dois.")]
        public Transform taco, pontaDoTaco;

        [Header("Medidas do corpo em repouso (no espaço do corpo, olhando para +Z)")]
        [Tooltip("A superfície das costas na altura do peito (z negativo).")]
        public float costas = -0.115f;
        [Tooltip("Onde fica o topo da mochila: a base do pescoço.")]
        public float topoDaMochila = 1.45f;
        [Tooltip("Onde a arma da alça cruza as costas: o começo do peito.")]
        public float centroDasCostas = 1.15f;
        public float cinto = 0.97f;
        [Tooltip("A largura do quadril na altura do cinto, de um lado.")]
        public float ladoDoQuadril = 0.18f;
        public float topoDaCabeca = 1.81f;
    }
}
