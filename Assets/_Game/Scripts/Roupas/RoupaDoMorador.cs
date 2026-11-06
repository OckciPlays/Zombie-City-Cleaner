using System.Linq;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// A roupa de um morador do bunker (a parte 2 do Plano_NPCs_Bunker.md da pasta de arte): o sorteio do tipo da roupa da
    /// ficha (<see cref="SorteioDosNPCs"/>), com o corpo, a pele e a cor dos pelos da ficha por cima. O corpo é procurado
    /// pelas sementes vizinhas (o sorteio do tipo decide o sexo pela chance dele); a pele é a da ficha; os pelos (o
    /// cabelo, a barba e as sobrancelhas) são a cor do índice da ficha na lista do tipo, ou na do "Idoso" se ele é
    /// grisalho; e os muito novos ficam sem barba. A mesma ficha dá sempre a mesma roupa.
    /// </summary>
    public static class RoupaDoMorador
    {
        public const string TipoDosGrisalhos = "Idoso";

        public static CatalogoDasRoupas.Npc Npc(CatalogoDasRoupas catalogo, AparenciaDoMorador a, string nome = null)
        {
            string tipo = catalogo.DoTipo(a.tipoDaRoupa) != null ? a.tipoDaRoupa : "Civil";
            string corpo = a.mulher ? SorteioDosNPCs.Mulher : CatalogoDasRoupas.Homem;
            CatalogoDasRoupas.Npc n = null;
            for (int k = 0; k < 64; k++)
            {
                n = SorteioDosNPCs.Sortear(catalogo, tipo, unchecked(a.semente + k * 7919));
                if (n != null && n.corpo == corpo) break;
            }
            if (n == null || n.corpo != corpo)
            {
                // o tipo quase nunca dá esse corpo: o do civil, que dá os dois
                for (int k = 0; k < 64 && (n == null || n.corpo != corpo); k++) n = SorteioDosNPCs.Sortear(catalogo, "Civil", unchecked(a.semente + k * 7919));
            }
            if (n == null) return null;

            n.nome = nome ?? n.nome;
            if (catalogo.peles != null && catalogo.peles.Length > 0)
                n.pele = catalogo.peles[((a.pele % catalogo.peles.Length) + catalogo.peles.Length) % catalogo.peles.Length];
            var lista = (a.grisalho ? catalogo.DoTipo(TipoDosGrisalhos) : catalogo.DoTipo(tipo))?.cabelos;
            if (lista == null || lista.Length == 0) lista = catalogo.DoTipo("Civil")?.cabelos;
            // no grisalho, só os 4 primeiros cinzas do idoso (o quinto, #6E6C68, parecia preto na luz do retrato)
            int cores = lista == null ? 0 : a.grisalho ? Mathf.Min(4, lista.Length) : lista.Length;
            string pelos = cores > 0 ? lista[((a.cabelo % cores) + cores) % cores] : null;
            n.pecas = n.pecas.Where(p => !(a.semBarba && catalogo.DaPeca(p.nome)?.lugar == "Barba")).Select(p =>
            {
                var dados = catalogo.DaPeca(p.nome);
                if (dados == null || !dados.pelos || pelos == null) return p;
                return new CatalogoDasRoupas.PecaVestida { nome = p.nome, cores = SorteioDosNPCs.CoresDoPelo(dados, pelos) };
            }).ToArray();
            return n;
        }

        /// <summary>Veste o corpo como o morador (o corpo, a pele, as peças e as cores).</summary>
        public static bool Vestir(Vestimenta v, AparenciaDoMorador a)
        {
            var n = Npc(v.Catalogo, a);
            if (n == null) return false;
            v.npc = "";
            v.tipo = n.tipo;
            v.semente = a.semente;
            v.corpo = n.corpo;
            v.pele = n.pele;
            v.Vestir(n.pecas);
            return true;
        }
    }
}
