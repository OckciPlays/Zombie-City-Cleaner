using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Um lugar do saque num móvel da mobília nova (o Plano_Saque.md da pasta de arte, a parte 2): o retângulo de uma
    /// superfície onde um item pode ficar, em cima (à vista) ou dentro (atrás de uma porta, numa gaveta, embaixo de uma
    /// tampa). Tudo no espaço do modelo, com as partes fechadas, no tamanho real (o jogo põe a escala, ×1,30).
    /// </summary>
    public struct LugarDoSaque
    {
        /// <summary>O meio do retângulo, em cima da superfície (o y é o dela).</summary>
        public Vector3 meio;
        /// <summary>A largura (x), a altura livre em cima dele e o fundo (z).</summary>
        public Vector3 tamanho;
        /// <summary>
        /// A parte que precisa estar aberta para ver e pegar o que está nele (o nome dela no prefab, sem o do móvel:
        /// "Gaveta.2", "Porta.1", "Tampa.1"); null: à vista.
        /// </summary>
        public string parte;
        /// <summary>O lugar é da parte (o chão da gaveta, a prateleira da porta da geladeira): vai com ela quando abre.</summary>
        public bool naParte;

        public bool Dentro => parte != null;
    }

    /// <summary>
    /// Os lugares do saque de cada modelo da mobília nova (a tabela escrita pela montagem dela, a partir das malhas dos
    /// prefabs: <c>LugaresDoSaque.cs</c>; Zombie City Cleaner > Mobília nova > Escrever os lugares do saque). Só lê: serve
    /// às threads do gerador do mapa e aos testes do Editor, sem os prefabs.
    /// </summary>
    public static partial class LugaresDoSaque
    {
        static readonly LugarDoSaque[] Nenhum = new LugarDoSaque[0];

        /// <summary>Os lugares do modelo (nenhum, se ele não guarda saque).</summary>
        public static LugarDoSaque[] Do(string modelo) => modelo != null && _lugares.TryGetValue(modelo, out var l) ? l : Nenhum;

        public static bool Tem(string modelo) => modelo != null && _lugares.ContainsKey(modelo);

        public static int Modelos => _lugares.Count;

        public static System.Collections.Generic.IEnumerable<string> Nomes => _lugares.Keys;

        static LugarDoSaque L(float x, float y, float z, float w, float h, float d, string parte = null, bool naParte = false) =>
            new LugarDoSaque { meio = new Vector3(x, y, z), tamanho = new Vector3(w, h, d), parte = parte, naParte = naParte };
    }
}
