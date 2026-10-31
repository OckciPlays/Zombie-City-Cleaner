using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os sons do Oco (a etapa 7, a pedido em 28/10/2026), em Audio/Resources/SonsDoOco.asset (o ZombieManager carrega pelo
    /// nome). Os arquivos (Audio/Oco/*.wav) saem do Ferramentas/sons_do_oco.py, e o asset é refeito pelo menu Zombie City
    /// Cleaner > Zumbis > Montar os sons do Oco.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Sons do Oco", fileName = "SonsDoOco")]
    public class SonsDoOco : ScriptableObject
    {
        [Tooltip("O volume de todas as vozes do Oco: a fala, o golpe, o chamado, a resposta, a agonia, a dor e o grito ao morrer. Na madrugada de 29/10/2026 desceu para 60%; na tarde do mesmo dia voltou a 100%, a pedido, e quem quer mais baixo usa Configurações > Zumbis (ZombieManager.VolumeDosZumbis).")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("O som padrão: o gemido rouco e oco, com a inspiração chocalhada no fim.")]
        public AudioClip[] voz;
        [Tooltip("Ao atacar (o rosnado curto).")]
        public AudioClip[] golpe;
        [Tooltip("O chamado entre eles: estalos da garganta e um lamento oco que sobe e cai (achou alguém, ou só chamando).")]
        public AudioClip[] chamado;
        [Tooltip("A resposta ao chamado (os de perto respondem).")]
        public AudioClip[] resposta;
        [Tooltip("A agonia no sol: o grito esganiçado, com soluços e a pele chiando.")]
        public AudioClip[] agonia;
        [Tooltip("A dor ao levar um tiro (e o grito ao morrer).")]
        public AudioClip[] tiro;
        [Tooltip("A crosta estalando enquanto a pedra sobe (em laço).")]
        public AudioClip petrificando;
        [Tooltip("A pedra fechando: virou estátua.")]
        public AudioClip petrificou;
        [Tooltip("À noite, a casca quebrando quando ele se solta.")]
        public AudioClip soltando;
    }
}
