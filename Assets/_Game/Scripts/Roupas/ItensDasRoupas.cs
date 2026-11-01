using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// O que as roupas como itens precisam no jogo (a parte 4 do Plano_Personagem.md da pasta de arte, 31/10/2026): as
    /// malhas paradas do <c>Roupas_Itens.fbx</c> (as roupas deitadas, as luvas juntas, o resto como fica no corpo), na
    /// ordem de <see cref="Itens.Roupa(int)"/>, o material das roupas e o catálogo (as cores do começo de cada peça). O chão
    /// (<see cref="ItensNoMundo"/>) e os ícones (<see cref="IconesDasRoupas"/>) usam. Fica em Resources (o
    /// <c>Art/Characters/Roupas/Resources/ItensDasRoupas.asset</c>), montado por Editor/MontagemDasRoupas: assim as cenas não
    /// precisam ser remontadas.
    /// </summary>
    public class ItensDasRoupas : ScriptableObject
    {
        public const string Nome = "ItensDasRoupas";

        [Tooltip("A malha de cada roupa, na ordem de Itens.Roupa (a base na origem).")]
        public Mesh[] malhas;
        [Tooltip("O material das roupas (as Texture2DArray; as cores vão no MaterialPropertyBlock).")]
        public Material material;
        public TextAsset catalogo;
        [Tooltip("O shader que não desenha nada (a pele dos braços de primeira pessoa, debaixo das roupas: Player/RoupasNosBracos).")]
        public Shader invisivel;

        static ItensDasRoupas _carregado;
        [System.NonSerialized] CatalogoDasRoupas _catalogo;
        static MaterialPropertyBlock _bloco;

        /// <summary>O de Resources (null: ainda não montado).</summary>
        public static ItensDasRoupas Carregar()
        {
            if (_carregado == null) _carregado = Resources.Load<ItensDasRoupas>(Nome);
            return _carregado;
        }

        public CatalogoDasRoupas Catalogo => _catalogo ??= catalogo != null ? CatalogoDasRoupas.Ler(catalogo) : null;

        /// <summary>A malha da roupa (null: não é uma, ou falta).</summary>
        public Mesh Malha(Item item)
        {
            int k = Itens.IndiceDaRoupa(item);
            return malhas != null && k >= 0 && k < malhas.Length ? malhas[k] : null;
        }

        /// <summary>As três cores (hex) da roupa: as dela, ou as do catálogo (<paramref name="cores"/> 0).</summary>
        public string[] CoresDe(Item item, ulong cores)
        {
            var hex = Itens.CoresEmHex(cores);
            if (hex != null) return hex;
            var dados = Itens.Roupa(item);
            var peca = dados != null ? Catalogo?.DaPeca(dados.peca) : null;
            return peca?.cores != null && peca.cores.Length > 0 ? peca.cores : new[] { "#808080" };
        }

        /// <summary>Pinta o renderer (o item no chão, o do ícone) com as cores da roupa.</summary>
        public void Pintar(Renderer r, Item item, ulong cores)
        {
            var hex = CoresDe(item, cores);
            _bloco ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_bloco);
            for (int k = 0; k < 3; k++)
                _bloco.SetColor(k == 0 ? "_Cor1" : k == 1 ? "_Cor2" : "_Cor3", CatalogoDasRoupas.Cor(hex[Mathf.Min(k, hex.Length - 1)]));
            r.SetPropertyBlock(_bloco);
        }
    }
}
