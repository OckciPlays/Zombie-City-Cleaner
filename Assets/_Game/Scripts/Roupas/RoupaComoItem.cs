using UnityEngine;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// Uma peça de roupa como item (o lote 6 do Plano_Roupas.md da pasta de arte, 31/10/2026): a malha parada do
    /// <c>Roupas_Itens.fbx</c> (as roupas deitadas, as luvas juntas, o resto como fica no corpo), com o material das
    /// roupas e as três cores dela (o <see cref="MaterialPropertyBlock"/> do shader, como a <see cref="Vestimenta"/>). Os
    /// prefabs (<c>Prefabs/Roupas/Itens</c>) são montados por Editor/MontagemDasRoupas; por enquanto eles só aparecem nas
    /// mesas da cena das variações (para ver a quantidade de itens), e o jogo ainda não os pega.
    /// </summary>
    [ExecuteAlways]
    public class RoupaComoItem : MonoBehaviour
    {
        [Tooltip("O nome da peça no catálogo das roupas (o Roupas.json).")]
        public string peca;
        [Tooltip("O lugar dela no corpo (a cabeça, os óculos, a máscara, o tronco...).")]
        public string lugar;
        [Tooltip("As três cores (hex): as do catálogo, ou outras.")]
        public string[] cores = new string[0];

        MaterialPropertyBlock _bloco;

        void OnEnable() => Pintar();

        void OnValidate() => Pintar();

        /// <summary>Pinta a peça com as cores dela (ou as dadas).</summary>
        public void Pintar(string[] outras = null)
        {
            if (outras != null) cores = outras;
            var r = GetComponent<MeshRenderer>();
            if (r == null || cores == null || cores.Length == 0) return;
            _bloco ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_bloco);
            for (int k = 0; k < 3; k++)
                _bloco.SetColor(k == 0 ? "_Cor1" : k == 1 ? "_Cor2" : "_Cor3", CatalogoDasRoupas.Cor(cores[Mathf.Min(k, cores.Length - 1)]));
            r.SetPropertyBlock(_bloco);
        }
    }
}
