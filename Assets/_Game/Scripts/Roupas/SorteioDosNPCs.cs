using System.Collections.Generic;
using System.Linq;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// O sorteio dos NPCs por tipo (a etapa 4 do Plano_Roupas.md da pasta de arte, 31/10/2026): um NPC novo de um tipo
    /// do catálogo (o "Policial", o "Militar"...), com as peças e as cores que combinam nele. A mesma semente dá o mesmo
    /// NPC (para os NPCs do mundo saírem iguais em todos os jogadores, só a semente vai pela rede).
    /// A ordem: o corpo (homem ou mulher, pela chance do tipo), o tom de pele, a cor dos pelos (uma só: o cabelo, a barba
    /// e as sobrancelhas), o esquema de cor (o verde, o deserto e o preto do militar) e cada lugar do tipo, na ordem dele:
    /// pela chance, uma das opções que servem (no corpo, no esquema e com os lugares do "sem" vazios), pelo peso, e uma
    /// das receitas de cor dela (nenhuma: as do começo da peça; num pelo, com a cor dos pelos no lugar da do começo).
    /// </summary>
    public static class SorteioDosNPCs
    {
        public const string Mulher = "Mulher";

        /// <summary>Um NPC sorteado do tipo (null se o tipo não está no catálogo), com o nome "tipo semente".</summary>
        public static CatalogoDasRoupas.Npc Sortear(CatalogoDasRoupas catalogo, string tipo, int semente)
        {
            var t = catalogo.DoTipo(tipo);
            if (t == null) return null;
            var r = new System.Random(Misturar(tipo, semente));
            bool mulher = r.NextDouble() < t.mulher && catalogo.formas != null && catalogo.formas.Contains(Mulher);
            string corpo = mulher ? Mulher : CatalogoDasRoupas.Homem;
            var peles = t.peles != null && t.peles.Length > 0 ? t.peles : Enumerable.Range(0, catalogo.peles.Length).ToArray();
            string pele = catalogo.peles[peles[r.Next(peles.Length)] % catalogo.peles.Length];
            string pelos = t.cabelos != null && t.cabelos.Length > 0 ? t.cabelos[r.Next(t.cabelos.Length)] : null;
            string esquema = "";
            if (t.esquemas != null && t.esquemas.Length > 0)
                esquema = Pesado(r, t.esquemas, e => e.peso)?.nome ?? "";

            var cheios = new HashSet<string>();
            var pecas = new List<CatalogoDasRoupas.PecaVestida>();
            foreach (var lugar in t.lugares ?? new CatalogoDasRoupas.LugarDoTipo[0])
            {
                // a chance é sorteada sempre (mesmo sem opção que sirva), para o resto não mudar com o corpo
                bool tem = r.NextDouble() < lugar.chance;
                var servem = (lugar.opcoes ?? new CatalogoDasRoupas.Opcao[0]).Where(o =>
                    (string.IsNullOrEmpty(o.corpo) || o.corpo == corpo) &&
                    (string.IsNullOrEmpty(o.esquema) || o.esquema == esquema) &&
                    (o.sem == null || !o.sem.Any(cheios.Contains)) &&
                    (string.IsNullOrEmpty(o.peca) || catalogo.DaPeca(o.peca) != null)).ToList();
                var escolha = Pesado(r, servem, o => o.peso);
                int receita = r.Next(1000);
                if (!tem || escolha == null || string.IsNullOrEmpty(escolha.peca)) continue;
                var dados = catalogo.DaPeca(escolha.peca);
                string[] cores;
                if (escolha.cores != null && escolha.cores.Length > 0)
                    cores = escolha.cores[receita % escolha.cores.Length].Split(' ');
                else cores = CoresDoPelo(dados, pelos);
                pecas.Add(new CatalogoDasRoupas.PecaVestida { nome = escolha.peca, cores = cores });
                cheios.Add(dados.lugar);
                if (!string.IsNullOrEmpty(lugar.lugar)) cheios.Add(lugar.lugar);
            }
            return new CatalogoDasRoupas.Npc
            {
                nome = $"{tipo} {semente}", tipo = tipo, corpo = corpo, pele = pele, pecas = pecas.ToArray(),
            };
        }

        /// <summary>
        /// As cores do começo de uma peça; num pelo (o cabelo, a barba), com a cor dos pelos onde a peça tem a cor 1 dela
        /// (o laço do rabo de cavalo fica).
        /// </summary>
        public static string[] CoresDoPelo(CatalogoDasRoupas.Peca dados, string pelos)
        {
            var cores = (string[])dados.cores.Clone();
            if (dados.pelos && !string.IsNullOrEmpty(pelos))
                for (int k = 0; k < cores.Length; k++)
                    if (cores[k] == dados.cores[0]) cores[k] = pelos;
            return cores;
        }

        /// <summary>
        /// A semente do gerador: o nome do tipo (FNV-1a) com a semente, embaralhados (o final do SplitMix). Com a semente
        /// direto, as vizinhas davam NPCs parecidos (o System.Random começa quase igual com 1, 2 e 3), e a mesma semente,
        /// o mesmo corpo, a mesma pele e o mesmo cabelo em todos os tipos.
        /// </summary>
        static int Misturar(string tipo, int semente)
        {
            uint h = 2166136261;
            foreach (char c in tipo) h = (h ^ c) * 16777619;
            ulong z = h ^ ((ulong)(uint)semente * 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z & 0x7FFFFFFF);
        }

        static T Pesado<T>(System.Random r, IList<T> lista, System.Func<T, float> peso) where T : class
        {
            float total = 0f;
            foreach (var x in lista) total += System.Math.Max(0f, peso(x));
            double sorteio = r.NextDouble() * total;
            if (total <= 0f) return null;
            foreach (var x in lista)
            {
                sorteio -= System.Math.Max(0f, peso(x));
                if (sorteio < 0) return x;
            }
            return lista[lista.Count - 1];
        }
    }
}
