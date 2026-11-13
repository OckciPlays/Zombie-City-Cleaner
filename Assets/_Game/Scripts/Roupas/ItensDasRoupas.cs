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
        [Tooltip("O material do vidro (o shader RoupasVidro: só os pixels de vidro, transparentes de verdade), que vai junto do das roupas nas peças com vidro.")]
        public Material vidro;
        [Tooltip("As peças com vidro (o alfa da cor abaixo de 1 em algum lugar: a lente dos óculos de proteção, a viseira), achadas pela montagem.")]
        public string[] comVidro;

        static ItensDasRoupas _carregado;
        [System.NonSerialized] CatalogoDasRoupas _catalogo;
        [System.NonSerialized] System.Collections.Generic.HashSet<string> _comVidro;
        [System.NonSerialized] Material[] _soARoupa, _comOVidro;
        static MaterialPropertyBlock _bloco;

        /// <summary>A peça (o nome do catálogo) tem vidro.</summary>
        public bool TemVidro(string peca)
        {
            if (vidro == null || string.IsNullOrEmpty(peca)) return false;
            _comVidro ??= new System.Collections.Generic.HashSet<string>(comVidro ?? new string[0]);
            return _comVidro.Contains(peca);
        }

        /// <summary>A roupa (o item) tem vidro.</summary>
        public bool TemVidro(Item item) => Itens.Roupa(item) is { } dados && TemVidro(dados.peca);

        /// <summary>
        /// Os materiais do item da roupa: o das roupas e, com vidro, o do vidro depois dele (a mesma malha desenhada duas
        /// vezes: as roupas sem o vidro, e o vidro transparente). Antes, o vidro era um pontilhado no material das roupas, e
        /// de perto se viam as bolinhas brancas no lugar do vidro (07/11/2026).
        /// </summary>
        public Material[] Materiais(Item item) => TemVidro(item) ? (_comOVidro ??= new[] { material, vidro }) : (_soARoupa ??= new[] { material });

        /// <summary>Põe (ou tira) o vidro no renderer de uma peça vestida: o primeiro material fica o que estava.</summary>
        public void PorOVidro(Renderer r, string peca)
        {
            var mats = r.sharedMaterials;
            if (mats.Length == 0) return;
            bool quer = TemVidro(peca);
            var ultimo = mats[mats.Length - 1];
            // (a cópia do retrato tem a cópia do vidro: o mesmo shader)
            bool tem = mats.Length > 1 && ultimo != null && vidro != null && ultimo.shader == vidro.shader;
            if (quer == tem) return;
            r.sharedMaterials = quer ? new[] { mats[0], vidro } : new[] { mats[0] };
        }

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
