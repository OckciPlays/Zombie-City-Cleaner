using UnityEngine;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O que a tela dos moradores (<see cref="TelaDosMoradores"/>) carrega antes de haver partida: o NPC vestível das
    /// roupas (<c>Prefabs/Roupas/NPC</c>, com o parado do mostruário). Fica em <c>Settings/Resources/RecursosDosMoradores</c>,
    /// feito pela Zombie City Cleaner > Moradores > Montar a tela.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Recursos dos moradores")]
    public class RecursosDosMoradores : ScriptableObject
    {
        public const string NoResources = "RecursosDosMoradores";

        [Tooltip("O NPC vestível das roupas (Prefabs/Roupas/NPC).")]
        public GameObject npc;
        [Tooltip("O corpo dos moradores no bunker (Prefabs/Moradores/Morador: o NetworkObject com o corpo do jogador; a parte 6).")]
        public GameObject corpo;

        static RecursosDosMoradores _carregados;
        public static RecursosDosMoradores Carregar() => _carregados != null ? _carregados : _carregados = Resources.Load<RecursosDosMoradores>(NoResources);
    }
}
