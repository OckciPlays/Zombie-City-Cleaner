using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os modelos da proteção de sucata (11/11/2026, a etapa 3), no <c>Resources/ProtecaoDeSucata.prefab</c> (montado pelo
    /// menu Zombie City Cleaner > Habilidades > Montar a proteção de sucata: o <c>MontagemDaProtecao</c>). As 6 peças do
    /// corpo (do <c>Art/Characters/Jogador/Protecao_Sucata.fbx</c>) estão no espaço do osso delas: filhas do osso, na
    /// posição zero e na escala 1, com o <see cref="giro"/> (as malhas chegam do FBX com o Z para cima do Blender: o giro
    /// de 90° no X as deixa no espaço do osso, o da caixa do Protecao_Sucata.json). Cada uma tem a forma "Mulher" (a blend
    /// shape, a 100 no corpo da mulher). Também
    /// guarda as malhas, os ícones e as fotos dos 4 itens (as cenas e o prefab do jogador ainda não têm). Nada disso muda as
    /// cenas nem o prefab do jogador: tudo é posto no Play.
    /// </summary>
    public class PecasDaProtecao : MonoBehaviour
    {
        [Tooltip("A paleta dos itens (o Materials/Pistola.mat).")]
        public Material material;
        [Tooltip("As 6 peças do corpo, na ordem de Pecas: o capacete, o colete, as braçadeiras (esquerda e direita) e as caneleiras.")]
        public Mesh[] pecas;
        [Tooltip("O giro de cada peça no osso (o da malha do FBX para o espaço do osso).")]
        public Quaternion giro = Quaternion.identity;
        [Tooltip("Os 4 itens (na ordem do LugarDaProtecao): a malha (o chão), o ícone e a foto.")]
        public Mesh[] malhasDosItens;
        public Texture2D[] icones, fotos;

        /// <summary>As 6 peças do corpo: o objeto no FBX, o osso dela e o lugar da proteção.</summary>
        public static readonly (string objeto, string osso, LugarDaProtecao lugar)[] Pecas =
        {
            ("Protecao_Capacete", "head", LugarDaProtecao.Capacete),
            ("Protecao_Colete", "chest", LugarDaProtecao.Colete),
            ("Protecao_Bracadeira.L", "forearm.L", LugarDaProtecao.Bracadeiras),
            ("Protecao_Bracadeira.R", "forearm.R", LugarDaProtecao.Bracadeiras),
            ("Protecao_Caneleira.L", "shin.L", LugarDaProtecao.Caneleiras),
            ("Protecao_Caneleira.R", "shin.R", LugarDaProtecao.Caneleiras),
        };

        /// <summary>O índice das braçadeiras em <see cref="Pecas"/> (a esquerda; a direita é a seguinte).</summary>
        public const int Bracadeiras = 2;

        const string Recurso = "ProtecaoDeSucata";
        static PecasDaProtecao _dados;
        static int _procurouNoQuadro = -1;

        /// <summary>O que o prefab guarda (null, se ainda não foi montado).</summary>
        public static PecasDaProtecao Dados
        {
            get
            {
                if (_dados == null && _procurouNoQuadro != Time.frameCount)
                {
                    _procurouNoQuadro = Time.frameCount;
                    var go = Resources.Load<GameObject>(Recurso);
                    _dados = go != null ? go.GetComponent<PecasDaProtecao>() : null;
                }
                return _dados;
            }
        }

        static T Em<T>(T[] lista, int i) where T : class => lista != null && i >= 0 && i < lista.Length ? lista[i] : null;

        /// <summary>A malha da peça do corpo (o índice em <see cref="Pecas"/>; null, sem o prefab).</summary>
        public static Mesh Peca(int k) => Dados != null ? Em(Dados.pecas, k) : null;
        public static Mesh MalhaDoItem(Item i) => Protecao.EhPeca(i) && Dados != null ? Em(Dados.malhasDosItens, (int)Protecao.LugarDe(i)) : null;
        public static Texture2D IconeDo(Item i) => Protecao.EhPeca(i) && Dados != null ? Em(Dados.icones, (int)Protecao.LugarDe(i)) : null;
        public static Texture2D FotoDo(Item i) => Protecao.EhPeca(i) && Dados != null ? Em(Dados.fotos, (int)Protecao.LugarDe(i)) : null;

        /// <summary>O giro da peça no osso (sem o prefab, nenhum).</summary>
        public static Quaternion Giro => Dados != null ? Dados.giro : Quaternion.identity;

        /// <summary>Põe a peça no osso: a posição zero, o <see cref="Giro"/> e a escala 1.</summary>
        public static void NoOsso(Transform peca, Transform osso)
        {
            peca.SetParent(osso, false);
            peca.localPosition = Vector3.zero;
            peca.localRotation = Giro;
            peca.localScale = Vector3.one;
        }

        /// <summary>
        /// Uma peça presa ao 'osso' (um renderer com a pele sem ossos: só a forma, que o MeshRenderer não faz), na pose do
        /// osso, com a forma 'corpo' a 100 ("Mulher"; o "Homem" deixa todas a 0).
        /// </summary>
        public static SkinnedMeshRenderer Por(Transform osso, Mesh malha, Material mat, string nome, int camada, string corpo)
        {
            var go = new GameObject(nome) { layer = camada };
            NoOsso(go.transform, osso);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = malha;
            r.sharedMaterial = mat;
            r.localBounds = malha.bounds;
            Forma(r, corpo);
            return r;
        }

        /// <summary>A forma do corpo a 100 e as outras a 0.</summary>
        public static void Forma(SkinnedMeshRenderer r, string corpo)
        {
            var m = r != null ? r.sharedMesh : null;
            if (m == null) return;
            for (int f = 0; f < m.blendShapeCount; f++) r.SetBlendShapeWeight(f, m.GetBlendShapeName(f) == corpo ? 100f : 0f);
        }
    }
}
