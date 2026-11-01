using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Corpo é a cápsula inteira (nível médio); o resto são as hitboxes presas nos ossos (nível perto). Olho e Papo são dos
    /// especiais: o olho nas costas do Brutamonte (o ponto fraco, só aberto) e o papo do Long (a gosma estoura).
    /// </summary>
    public enum RegiaoDoCorpo : byte { Corpo, Cabeca, Tronco, Braco, Perna, Olho, Papo }

    /// <summary>
    /// O pedaço de um membro (ou nenhum, no tronco e na cabeça). O de cima é o braço ou a coxa, o de baixo é o antebraço
    /// (com a mão) ou a canela (com o pé). Cada lado tem os seus: E é a esquerda do zumbi, D a direita.
    /// </summary>
    public enum Membro : byte { Nenhum, BracoE, AntebracoE, BracoD, AntebracoD, CoxaE, CanelaE, CoxaD, CanelaD }

    /// <summary>
    /// Uma parte do corpo do zumbi que leva tiro, presa a um osso (ou a cápsula inteira, no nível médio).
    /// O tiro manda a região e o membro para o anfitrião: a cabeça explode e mata; o resto tira vida do corpo, e nos
    /// membros também a vida do membro (sem ela, o membro cai).
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Zumbi zumbi;
        public RegiaoDoCorpo regiao;
        public Membro membro;
        public Collider colisor;
    }
}
