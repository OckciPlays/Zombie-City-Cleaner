using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os itens que podem ser achados no mapa (o Plano_Saque.md da pasta de arte), para o sorteio do saque nas threads do
    /// gerador: o número de cada um (o do enum Item do jogo, que fica fora deste assembly), o tamanho dele como fica num
    /// móvel (deitado de lado, as armas; o resto como é; em metros do jogo) e, nas roupas, o lugar no corpo. A tabela
    /// (<c>ItensDoSaque.cs</c>) é escrita pelo Editor a partir das malhas dos itens (Zombie City Cleaner > Itens > Escrever
    /// os itens do saque). O nome é o do enum Item; o das roupas, "Roupa.&lt;peça&gt;".
    /// </summary>
    public static partial class ItensDoSaque
    {
        public struct Dados
        {
            public string nome;
            public byte item;
            public Vector3 tamanho;
            /// <summary>Nas roupas, o lugar no corpo do catálogo ("Tronco", "Cabeca", "Oculos"...); vazio no resto.</summary>
            public string lugar;
        }

        static Dictionary<string, Dados> _porNome;

        static Dictionary<string, Dados> PorNome
        {
            get
            {
                if (_porNome != null) return _porNome;
                var d = new Dictionary<string, Dados>();
                foreach (var (nome, item, tamanho, lugar) in _itens) d[nome] = new Dados { nome = nome, item = item, tamanho = tamanho, lugar = lugar };
                foreach (var (nome, item, tamanho, lugar) in _feitosNoPlay) d[nome] = new Dados { nome = nome, item = item, tamanho = tamanho, lugar = lugar };
                return _porNome = d;
            }
        }

        /// <summary>
        /// Os itens de modelo feito no Play (caixinhas do Loot/ItensDaCaca.cs), que o Editor não acha para a tabela: a panela
        /// das cozinhas (18/11/2026, a etapa 2 da fogueira; o número é o do Item.Panela). O tamanho é o da malha, com o cabo.
        /// </summary>
        static readonly (string nome, byte item, Vector3 tamanho, string lugar)[] _feitosNoPlay =
        {
            ("Panela", 93, new Vector3(0.42f, 0.09f, 0.24f), ""),
            // os instrumentos (a etapa 4 da fogueira; os números dos Item.Violao, Pandeiro, Gaita e Flauta)
            ("Violao", 74, new Vector3(1.1f, 0.11f, 0.39f), ""),
            ("Pandeiro", 75, new Vector3(0.27f, 0.06f, 0.27f), ""),
            ("Gaita", 76, new Vector3(0.104f, 0.028f, 0.028f), ""),
            ("Flauta", 77, new Vector3(0.46f, 0.03f, 0.03f), ""),
        };

        public static bool Tem(string nome) => nome != null && PorNome.ContainsKey(nome);

        public static Dados Do(string nome) => PorNome[nome];

        public static int Quantos => _itens.Length;

        /// <summary>Todos, na ordem do número do item.</summary>
        public static IEnumerable<Dados> Todos
        {
            get
            {
                foreach (var (nome, item, tamanho, lugar) in _itens) yield return new Dados { nome = nome, item = item, tamanho = tamanho, lugar = lugar };
                foreach (var (nome, item, tamanho, lugar) in _feitosNoPlay) yield return new Dados { nome = nome, item = item, tamanho = tamanho, lugar = lugar };
            }
        }
    }
}
