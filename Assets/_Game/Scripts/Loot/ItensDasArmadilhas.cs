using UnityEngine;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// Os modelos das armadilhas (11/11/2026, a etapa 5a do ramo Armadilhas e defesas), no
    /// <c>Resources/ItensDasArmadilhas.prefab</c> (montado pelo menu Zombie City Cleaner > Habilidades > Montar as armadilhas:
    /// o <c>MontagemDasArmadilhas</c>). Nada disso muda as cenas: tudo é posto no Play, como o fogo da etapa 4.
    ///  - os 4 itens (o alarme de latas, o tapete de pregos, as estacas e a cerca de fogo): a malha do chão (do
    ///    <c>Art/Armadilhas/Loot_Armadilhas.fbx</c>), o ícone e a foto;
    ///  - as montadas (do <c>Art/Armadilhas/Armadilhas.fbx</c>): o alarme inteiro (a sombra de montar) e separado em duas (o
    ///    barbante com as estacas e as latas, que balançam em volta do vazio <c>Latas</c>), o tapete, as estacas (o vazio
    ///    <c>Ponta</c>) e a cerca de fogo (o vazio <c>Pavio</c>, de onde o rastro de 4 m sai para +Z);
    ///  - o material do rastro de combustível da cerca (uma faixa escura no chão);
    ///  - a etapa 5b: as barricadas de tábuas pregadas numa porta e numa janela, inteiras e danificadas, e o item das tábuas
    ///    (a malha, o ícone e a foto).
    /// </summary>
    public class ItensDasArmadilhas : MonoBehaviour
    {
        [Tooltip("A paleta (o Materials/Pistola.mat).")]
        public Material material;
        [Tooltip("O rastro de combustível da cerca de fogo no chão (escuro, transparente).")]
        public Material materialDoRastro;
        [Tooltip("Os 4 itens (na ordem de Itens4): a malha (o chão), o ícone e a foto.")]
        public Mesh[] malhasDosItens;
        public Texture2D[] icones, fotos;

        [Header("As montadas (o Armadilhas.fbx)")]
        public Mesh alarme;
        [Tooltip("O alarme sem as latas (as estacas e o barbante) e as latas sozinhas, com a origem no vazio Latas.")]
        public Mesh alarmeSemLatas, latas;
        public Mesh tapete, estacas, cerca;
        [Tooltip("Os vazios, no espaço da montada: Latas (onde as latas penduram), Ponta (a ponta das estacas) e Pavio (o começo do rastro).")]
        public Vector3 vazioDasLatas = new Vector3(0f, 0.525f, 0f), pontaDasEstacas = new Vector3(0f, 0.89f, 0.84f), pavioDaCerca = new Vector3(0f, 0.01f, 0.465f);

        [Header("A etapa 5b: a barricada de tábuas")]
        [Tooltip("Pregadas: a origem no meio da largura do vão, no chão (a porta) ou no peitoril (a janela), na face da parede de quem prega; as tábuas para +Z.")]
        public Mesh barricadaPorta;
        public Mesh barricadaPortaDanificada, barricadaJanela, barricadaJanelaDanificada;
        [Tooltip("O item das tábuas (Item_Tabuas_corpo do Loot_Armadilhas.fbx), o ícone e a foto.")]
        public Mesh tabuasDoItem;
        public Texture2D iconeDasTabuas, fotoDasTabuas;

        public static readonly Item[] Itens4 = { Item.AlarmeLatas, Item.TapetePregos, Item.Estacas, Item.CercaFogo };

        const string Recurso = "ItensDasArmadilhas";
        static ItensDasArmadilhas _dados;
        static int _procurouNoQuadro = -1;

        /// <summary>O que o prefab guarda (null, se ainda não foi montado).</summary>
        public static ItensDasArmadilhas Dados
        {
            get
            {
                if (_dados == null && _procurouNoQuadro != Time.frameCount)
                {
                    _procurouNoQuadro = Time.frameCount;
                    var go = Resources.Load<GameObject>(Recurso);
                    _dados = go != null ? go.GetComponent<ItensDasArmadilhas>() : null;
                }
                return _dados;
            }
        }

        static int Indice(Item i) => Itens.EhArmadilha(i) ? i - Item.AlarmeLatas : -1;
        static T Em<T>(T[] lista, int i) where T : class => lista != null && i >= 0 && i < lista.Length ? lista[i] : null;

        public static Mesh MalhaDoItem(Item i) => Dados == null ? null : i == Item.TabuasBarricada ? Dados.tabuasDoItem : Em(Dados.malhasDosItens, Indice(i));
        public static Texture2D IconeDo(Item i) => Dados == null ? null : i == Item.TabuasBarricada ? Dados.iconeDasTabuas : Em(Dados.icones, Indice(i));
        public static Texture2D FotoDo(Item i) => Dados == null ? null : i == Item.TabuasBarricada ? Dados.fotoDasTabuas : Em(Dados.fotos, Indice(i));

        /// <summary>A barricada de tábuas pregada (a porta ou a janela, inteira ou danificada; null, sem o prefab).</summary>
        public static Mesh Barricada(bool janela, bool danificada)
        {
            var d = Dados;
            if (d == null) return null;
            var inteira = janela ? d.barricadaJanela : d.barricadaPorta;
            var gasta = janela ? d.barricadaJanelaDanificada : d.barricadaPortaDanificada;
            return danificada && gasta != null ? gasta : inteira;
        }

        /// <summary>A armadilha montada (a malha inteira: a sombra de montar, o colisor e o encaixe; null, sem o prefab).</summary>
        public static Mesh Montada(Item i)
        {
            var d = Dados;
            if (d == null) return null;
            return i switch
            {
                Item.AlarmeLatas => d.alarme,
                Item.TapetePregos => d.tapete,
                Item.Estacas => d.estacas,
                Item.CercaFogo => d.cerca,
                _ => null,
            };
        }

        public static Vector3 VazioDasLatas => Dados != null ? Dados.vazioDasLatas : new Vector3(0f, 0.525f, 0f);
        public static Vector3 PavioDaCerca => Dados != null ? Dados.pavioDaCerca : new Vector3(0f, 0.01f, 0.465f);
    }
}
