using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As medidas da mobília nova (a caixa de cada modelo, com as partes fechadas, a tabela escrita pela montagem dela a
    /// partir dos prefabs: <c>MedidasDaMobilia.cs</c>), no tamanho do jogo. Só lê: serve às threads do gerador do mapa e
    /// aos testes do Editor, sem os prefabs.
    /// </summary>
    public static partial class MedidasDaMobilia
    {
        /// <summary>
        /// A escala dos móveis no jogo (a escolha do usuário no mostruário, 29/10/2026: "O piloto ficou bom", em ×1,30); as
        /// construções ficam em ×1,15. Os modelos são feitos no tamanho real.
        /// </summary>
        public const float Escala = 1.30f;

        public static bool Tem(string modelo) => modelo != null && _medidas.ContainsKey(modelo);

        /// <summary>O tamanho no tamanho real: largura (x), altura, fundo (z).</summary>
        public static Vector3 Real(string modelo) => _medidas[modelo].tamanho;

        /// <summary>O meio da caixa em x e z (no espaço do modelo, no tamanho real): a origem nem sempre fica no meio.</summary>
        public static Vector2 Meio(string modelo) => _medidas[modelo].meio;

        /// <summary>O y de baixo da caixa (no espaço do modelo, no tamanho real).</summary>
        public static float Baixo(string modelo) => _medidas[modelo].baixo;

        public static int Quantos => _medidas.Count;
        public static System.Collections.Generic.IEnumerable<string> Modelos => _medidas.Keys;
    }
}
