using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os modelos do fogo (11/11/2026, a etapa 4 do ramo Fogo), no <c>Resources/ItensDoFogo.prefab</c> (montado pelo menu
    /// Zombie City Cleaner > Habilidades > Montar o fogo: o <c>MontagemDoFogo</c>). Nada disso muda as cenas nem o prefab do
    /// jogador: tudo é posto no Play.
    ///  - os 3 itens (o lampião, o molotov e o kit da fogueira portátil): a malha do chão (do <c>Loot_Etapa34.fbx</c>), o
    ///    ícone e a foto;
    ///  - os braços de primeira pessoa do lampião e do molotov (os FBX <c>Jogador_FP_Lampiao</c> e <c>_Molotov</c>, com os
    ///    controladores deles): o <see cref="ArmaJogador"/> os põe ao lado dos outros;
    ///  - as malhas do corpo (do <c>Fogo.fbx</c>, a origem onde a mão segura, como a tocha) e onde ficam a chama do lampião e o
    ///    pavio do molotov; a pegada de cada um no osso <c>hand.R</c> (o <c>Fogo_MaoDoCorpo.json</c>);
    ///  - a fogueira montada da portátil e onde nasce o fogo dela.
    /// </summary>
    public class ItensDoFogo : MonoBehaviour
    {
        [Tooltip("A paleta dos itens (o Materials/Pistola.mat).")]
        public Material material;
        [Tooltip("Os 3 itens (na ordem de Itens: o lampião, o molotov e o kit da fogueira): a malha (o chão), o ícone e a foto.")]
        public Mesh[] malhasDosItens;
        public Texture2D[] icones, fotos;

        [Header("A primeira pessoa")]
        public GameObject bracosDoLampiao;
        public GameObject bracosDoMolotov;
        public RuntimeAnimatorController controladorDoLampiao, controladorDoMolotov;
        [Tooltip("Onde nasce a chama nos braços (o osso e o ponto nele): o vazio Boca_FP do FBX é animado e não serve (como na tocha); a MontagemDoFogo acha o vidro do lampião e o gargalo do molotov nas malhas.")]
        public string ossoDaChama, ossoDoPavio;
        public Vector3 chamaNosBracos, pavioNosBracos;

        [Header("O corpo (filhas do hand.R)")]
        public Mesh lampiaoNoCorpo;
        public Mesh molotovNoCorpo;
        [Tooltip("Onde fica a chama do lampião e o pavio do molotov, no espaço da malha (os vazios Chama e Pavio do Fogo.fbx).")]
        public Vector3 chamaDoLampiao, pavioDoMolotov;
        public Vector3 lugarDoLampiao, lugarDoMolotov;
        public Quaternion giroDoLampiao = Quaternion.identity, giroDoMolotov = Quaternion.identity;

        [Header("A fogueira portátil montada")]
        public Mesh fogueira;
        [Tooltip("Onde nasce o fogo dela (o vazio Fogo do Fogo.fbx).")]
        public Vector3 fogoDaFogueira = new Vector3(0f, 0.12f, 0f);

        public static readonly Item[] Itens3 = { Item.Lampiao, Item.Molotov, Item.KitFogueira };

        const string Recurso = "ItensDoFogo";
        static ItensDoFogo _dados;
        static int _procurouNoQuadro = -1;

        /// <summary>O que o prefab guarda (null, se ainda não foi montado).</summary>
        public static ItensDoFogo Dados
        {
            get
            {
                if (_dados == null && _procurouNoQuadro != Time.frameCount)
                {
                    _procurouNoQuadro = Time.frameCount;
                    var go = Resources.Load<GameObject>(Recurso);
                    _dados = go != null ? go.GetComponent<ItensDoFogo>() : null;
                }
                return _dados;
            }
        }

        static int Indice(Item i) => i == Item.Lampiao ? 0 : i == Item.Molotov ? 1 : i == Item.KitFogueira ? 2 : -1;
        static T Em<T>(T[] lista, int i) where T : class => lista != null && i >= 0 && i < lista.Length ? lista[i] : null;

        public static Mesh MalhaDoItem(Item i) => Dados != null ? Em(Dados.malhasDosItens, Indice(i)) : null;
        public static Texture2D IconeDo(Item i) => Dados != null ? Em(Dados.icones, Indice(i)) : null;
        public static Texture2D FotoDo(Item i) => Dados != null ? Em(Dados.fotos, Indice(i)) : null;
        /// <summary>A fogueira portátil montada (null, sem o prefab).</summary>
        public static Mesh Fogueira => Dados != null ? Dados.fogueira : null;
        public static Vector3 FogoDaFogueira => Dados != null ? Dados.fogoDaFogueira : new Vector3(0f, 0.12f, 0f);

        /// <summary>
        /// No corpo (em qualquer máquina): a peça do lampião ou do molotov, filha do lugar da tocha na mão (o
        /// <paramref name="lugarDaTocha"/>, que já está no hand.R com a pegada dela), com a pegada própria; criada na primeira
        /// vez. A chama (o "Fogo") fica na ponta, ligada pelo <see cref="JogadorRede"/>.
        /// </summary>
        public static Transform PecaNoCorpo(Transform lugarDaTocha, Item item, (Vector3 lugar, Quaternion giro) pegadaDaTocha, Material fogo)
        {
            var d = Dados;
            if (d == null || lugarDaTocha == null || !Itens.EhFogoDeMao(item)) return null;
            string nome = item == Item.Lampiao ? "Peca_Lampiao" : "Peca_Molotov";
            var achou = lugarDaTocha.Find(nome);
            if (achou != null) return achou;
            var malha = item == Item.Lampiao ? d.lampiaoNoCorpo : d.molotovNoCorpo;
            if (malha == null) return null;
            var go = new GameObject(nome) { layer = lugarDaTocha.gameObject.layer };
            var t = go.transform;
            t.SetParent(lugarDaTocha, false);
            // a pegada é no hand.R; o lugar da tocha já tem a dela: a peça vai com a diferença
            var inv = Quaternion.Inverse(pegadaDaTocha.giro);
            var lugar = item == Item.Lampiao ? d.lugarDoLampiao : d.lugarDoMolotov;
            var giro = item == Item.Lampiao ? d.giroDoLampiao : d.giroDoMolotov;
            t.localPosition = inv * (lugar - pegadaDaTocha.lugar);
            t.localRotation = inv * giro;
            t.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = d.material;
            if (fogo != null)
            {
                bool lampiao = item == Item.Lampiao;
                var chama = ConstrucoesNoMundo.Fogo(t, lampiao ? d.chamaDoLampiao : d.pavioDoMolotov, fogo, lampiao ? 0.09f : 0.07f,
                                                    lampiao ? FogoDoJogador.LuzDoLampiao : 2.5f, true, go.layer);
                if (chama.TryGetComponent<Light>(out var luz)) luz.intensity = lampiao ? FogoDoJogador.ForcaDaLuzDoLampiao : 0.8f;
            }
            return t;
        }
    }
}
