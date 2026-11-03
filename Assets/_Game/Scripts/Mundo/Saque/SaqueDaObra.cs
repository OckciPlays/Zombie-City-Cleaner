using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>Um item sorteado num lugar de um móvel de uma obra (<see cref="SaqueDaObra"/>).</summary>
    public struct ItemDoSaque
    {
        /// <summary>O item: o número (o do enum Item do jogo), o nome (o do catálogo) e a raridade.</summary>
        public byte item;
        public string nome;
        public Raridade raridade;
        /// <summary>Onde: o ponto do móvel na obra, a peça do conjunto dele e o lugar da peça (a chave na rede vem deles).</summary>
        public int ponto, peca, lugar;
        /// <summary>
        /// Nas coordenadas da obra (as dos pontos), com as partes fechadas: o meio de baixo do item (em cima da superfície),
        /// o giro dele em y (graus) e o tamanho dele (como em <see cref="ItensDoSaque"/>, antes do giro).
        /// </summary>
        public Vector3 apoio;
        public float giro;
        public Vector3 tamanho;
        /// <summary>A parte que precisa abrir para ver e pegar (null: à vista) e se o lugar vai com ela (a gaveta).</summary>
        public string parte;
        public bool naParte;
        /// <summary>A pilha (as balas, na caixa de munição), o desgaste (as armas, de 0 a 100) e o pente (a fração, nas de fogo).</summary>
        public int qtd, desgaste;
        public float pente;
        /// <summary>O cômodo (o tipo) e a moradia (<see cref="PontoDeMovel.unidade"/>).</summary>
        public string comodo;
        public int unidade;
    }

    /// <summary>
    /// O sorteio do saque de uma obra (o Plano_Saque.md da pasta de arte, a parte 3): para cada moradia dela (a casa, ou cada
    /// apartamento; <see cref="PontoDeMovel.unidade"/>), pela semente da obra (a mesma em todas as máquinas, como os móveis):
    ///  - quantos itens (<see cref="CatalogoDoSaque.Quantos"/>: a térrea de 3 a 9, o sobrado de 6 a 12, o apartamento de 3 a 6;
    ///    com porão, mais 0 a 3, no porão);
    ///  - para cada um, a raridade (<see cref="CatalogoDoSaque.Chances"/>) e, entre os lugares livres que aceitam um item dela
    ///    (<see cref="CatalogoDoSaque.Aceita"/>) que cabe neles, deitado ou girado 90°: o cômodo (pelo peso dele; no máximo 6
    ///    no mesmo, se a moradia tiver outros com lugar), o móvel (pelo peso dele) e o lugar (<see cref="LugaresDoSaque"/>, nas
    ///    peças do conjunto, no lugar e no giro de cada uma, como o <see cref="MobiliaNova"/> as põe); sem nenhum lugar para a
    ///    raridade na moradia (a cozinha não tem nada épico), a de baixo. Escolher a raridade antes do lugar deixa as chances
    ///    perto das do plano: com o lugar antes, quase tudo descia (o épico ficava em 1,5% e o lendário em 0,3%);
    ///  - o item, entre os da raridade que o lugar aceita e que cabem nele, e a pilha e o estado dele (as balas, o desgaste
    ///    e o pente das armas).
    /// Seguro em várias threads.
    /// </summary>
    public static class SaqueDaObra
    {
        /// <summary>A folga do item no lugar, de cada lado (m, no jogo).</summary>
        const float Folga = 0.01f;

        // um lugar livre de uma moradia, com o que ele aceita e onde fica (nas coordenadas da obra)
        sealed class Livre
        {
            public int ponto, peca, lugar, sala;
            public string comodo, modelo;
            public int andar;
            public LugarDoSaque l;
            public EspecieDoSaque aceita;
            public float peso;
            public Vector3 meio;          // em cima da superfície, na obra
            public float giro;            // o da peça
            public Vector3 tamanho;       // o do lugar, no jogo
            // os itens de cada raridade que ele aceita e que cabem nele (e se vão girados)
            public readonly List<(CatalogoDoSaque.Item item, bool girado)>[] cabem = new List<(CatalogoDoSaque.Item, bool)>[5];
        }

        /// <summary>O saque de todas as moradias da obra (os pontos e a semente dela, como no MobiliaDaConstrucao), em 'saida'.</summary>
        public static void Sortear(IList<PontoDeMovel> pontos, int semente, List<ItemDoSaque> saida)
        {
            if (pontos == null) return;
            var unidades = new List<int>();
            foreach (var p in pontos)
                if (p.unidade > 0 && !unidades.Contains(p.unidade)) unidades.Add(p.unidade);
            unidades.Sort();
            foreach (int u in unidades) SortearMorada(pontos, semente, u, saida);
        }

        /// <summary>Os lugares de uma moradia que guardam saque (para os testes e o F1).</summary>
        public static int Lugares(IList<PontoDeMovel> pontos, int semente, int unidade) => LugaresDa(pontos, semente, unidade).Count;

        static List<Livre> LugaresDa(IList<PontoDeMovel> pontos, int semente, int unidade)
        {
            var r = new List<Livre>();
            var pecas = new List<MobiliaNova.Peca>();
            float e = MedidasDaMobilia.Escala;
            // as salas com a mesa de sinuca (a caixa de bolas e os tacos só vão nelas)
            var comSinuca = new HashSet<int>();
            for (int i = 0; i < pontos.Count; i++)
                if (pontos[i].unidade == unidade && pontos[i].tipo == "MesaSinuca") comSinuca.Add(pontos[i].sala);
            for (int i = 0; i < pontos.Count; i++)
            {
                var p = pontos[i];
                if (p.unidade != unidade || !MobiliaNova.EhNovo(p.tipo)) continue;
                pecas.Clear();
                MobiliaNova.Pecas(p, MobiliaNova.Sorte(p, semente, i), pecas);
                foreach (var q in pecas)
                {
                    var regra = CatalogoDoSaque.DoModelo(q.modelo);
                    if (regra == null) continue;
                    var lugares = LugaresDoSaque.Do(q.modelo);
                    var giro = Quaternion.Euler(0f, q.giro, 0f);
                    for (int j = 0; j < lugares.Length; j++)
                    {
                        var l = lugares[j];
                        var aceita = CatalogoDoSaque.Aceita(regra, l);
                        if (!comSinuca.Contains(p.sala)) aceita &= ~EspecieDoSaque.Sinuca;
                        if (aceita == EspecieDoSaque.Nenhuma) continue;
                        var livre = new Livre
                        {
                            ponto = i, peca = q.indice, lugar = j, sala = p.sala, comodo = p.comodo, andar = p.andar, modelo = q.modelo,
                            l = l, aceita = aceita, peso = regra.peso / lugares.Length,
                            meio = q.origem + giro * (l.meio * e), giro = q.giro, tamanho = l.tamanho * e,
                        };
                        bool algum = false;
                        for (int k = 0; k < 5; k++)
                        {
                            var lista = livre.cabem[k] = new List<(CatalogoDoSaque.Item, bool)>();
                            foreach (var c in CatalogoDoSaque.DaRaridade((Raridade)k))
                                if ((c.especie & aceita) != 0 && Cabe(c.tamanho, livre.tamanho, out bool girado)) lista.Add((c, girado));
                            algum |= lista.Count > 0;
                        }
                        // onde nada cabe (o lugar estreito do criado-mudo, para as roupas), fica de fora
                        if (algum) r.Add(livre);
                    }
                }
            }
            return r;
        }

        static void SortearMorada(IList<PontoDeMovel> pontos, int semente, int unidade, List<ItemDoSaque> saida)
        {
            var livres = LugaresDa(pontos, semente, unidade);
            if (livres.Count == 0) return;
            bool apartamento = false, sobrado = false, porao = false;
            foreach (var p in pontos)
            {
                if (p.unidade != unidade) continue;
                apartamento |= p.apartamento;
                sobrado |= p.andar >= 1;
            }
            foreach (var l in livres) porao |= l.andar < 0;
            var rng = new Sorteio(Sorteio.Misturar(((ulong)(uint)semente << 20) ^ (ulong)(uint)unidade ^ 0x5A0E5A9E0000UL));
            var (min, max) = CatalogoDoSaque.Quantos(apartamento, sobrado && !apartamento);
            int n = rng.Entre(min, max + 1);
            int noPorao = 0;
            if (!apartamento && porao)
                for (int k = 0; k < CatalogoDoSaque.TentativasNoPorao; k++)
                    if (rng.Chance(CatalogoDoSaque.ChanceDoPorao)) noPorao++;
            var porSala = new Dictionary<int, int>();
            for (int k = 0; k < n; k++) Colocar(rng, livres, porSala, false, unidade, saida);
            for (int k = 0; k < noPorao; k++) Colocar(rng, livres, porSala, true, unidade, saida);
        }

        // um item num lugar livre (só no porão, com 'soPorao'): a raridade, o lugar que aceita um item dela e o item; o
        // lugar usado sai da lista
        static void Colocar(Sorteio rng, List<Livre> livres, Dictionary<int, int> porSala, bool soPorao, int unidade, List<ItemDoSaque> saida)
        {
            float x = rng.Fracao();
            int r = 0;
            for (; r < 4; r++)
            {
                x -= CatalogoDoSaque.Chances[r];
                if (x < 0f) break;
            }
            var aceitam = new List<int>();
            var salas = new List<(int sala, float peso)>();
            for (; r >= 0; r--)
            {
                aceitam.Clear();
                for (int i = 0; i < livres.Count; i++)
                    if ((!soPorao || livres[i].andar < 0) && livres[i].cabem[r].Count > 0) aceitam.Add(i);
                if (aceitam.Count == 0) continue;
                // o cômodo: pelo peso, entre os que têm lugar e ainda não estão cheios (se houver outros)
                bool temLivre = false;
                foreach (int i in aceitam)
                {
                    porSala.TryGetValue(livres[i].sala, out int ja);
                    temLivre |= ja < CatalogoDoSaque.PorComodo;
                }
                salas.Clear();
                foreach (int i in aceitam)
                {
                    var l = livres[i];
                    porSala.TryGetValue(l.sala, out int ja);
                    if (temLivre && ja >= CatalogoDoSaque.PorComodo) continue;
                    bool tem = false;
                    foreach (var s in salas) tem |= s.sala == l.sala;
                    if (!tem) salas.Add((l.sala, CatalogoDoSaque.PesoDoComodo(l.comodo)));
                }
                int sala = salas[Escolher(rng, salas.Count, i => salas[i].peso)].sala;
                // o lugar: pelo peso do móvel (dividido pelos lugares dele: um móvel com muitos não ganha mais)
                aceitam.RemoveAll(i => livres[i].sala != sala);
                int k = aceitam[Escolher(rng, aceitam.Count, i => livres[aceitam[i]].peso)];
                var lugar = livres[k];
                livres.RemoveAt(k);
                var it = Item(rng, lugar, lugar.cabem[r]);
                it.unidade = unidade;
                saida.Add(it);
                porSala[sala] = porSala.TryGetValue(sala, out int antes) ? antes + 1 : 1;
                return;
            }
        }

        static int Escolher(Sorteio rng, int n, System.Func<int, float> peso)
        {
            float total = 0f;
            for (int i = 0; i < n; i++) total += peso(i);
            float x = rng.Fracao() * total;
            for (int i = 0; i < n; i++)
            {
                x -= peso(i);
                if (x < 0f) return i;
            }
            return n - 1;
        }

        // um dos itens que cabem no lugar (os da raridade), com a pilha e o estado
        static ItemDoSaque Item(Sorteio rng, Livre lugar, List<(CatalogoDoSaque.Item item, bool girado)> cabem)
        {
            var (esc, gir) = cabem[rng.Entre(0, cabem.Count)];
            // no lugar: um pouco fora do meio, onde sobrar espaço, e virado para um lado ou para o outro
            float w = gir ? esc.tamanho.z : esc.tamanho.x, d = gir ? esc.tamanho.x : esc.tamanho.z;
            float sx = Mathf.Max(0f, lugar.tamanho.x - w) / 2f * 0.6f, sz = Mathf.Max(0f, lugar.tamanho.z - d) / 2f * 0.6f;
            var desvio = new Vector3(rng.Entre(-sx, sx), 0f, rng.Entre(-sz, sz));
            float giro = lugar.giro + (gir ? 90f : 0f) + (rng.Chance(0.5f) ? 180f : 0f) + rng.Entre(-8f, 8f);
            var it = new ItemDoSaque
            {
                item = esc.numero, nome = esc.nome, raridade = esc.raridade,
                ponto = lugar.ponto, peca = lugar.peca, lugar = lugar.lugar,
                apoio = lugar.meio + Quaternion.Euler(0f, lugar.giro, 0f) * desvio, giro = giro, tamanho = esc.tamanho,
                parte = lugar.l.parte, naParte = lugar.l.naParte,
                qtd = rng.Entre(esc.min, esc.max + 1), comodo = lugar.comodo,
            };
            if ((esc.especie & (EspecieDoSaque.ArmaCurta | EspecieDoSaque.ArmaLonga)) != 0)
            {
                var (a, b) = CatalogoDoSaque.DesgasteDaArma(esc.raridade);
                it.desgaste = rng.Entre(a, b + 1);
                it.pente = rng.Entre(0f, CatalogoDoSaque.PenteMaximo);
            }
            else if ((esc.especie & (EspecieDoSaque.Faca | EspecieDoSaque.Machado)) != 0)
                it.desgaste = rng.Entre(CatalogoDoSaque.DesgasteDaBranca.min, CatalogoDoSaque.DesgasteDaBranca.max + 1);
            return it;
        }

        /// <summary>O item (o tamanho dele) cabe no lugar (o tamanho dele no jogo), deitado ou girado 90° (girado: true).</summary>
        public static bool Cabe(Vector3 item, Vector3 lugar, out bool girado)
        {
            float w = lugar.x - 2f * Folga, d = lugar.z - 2f * Folga;
            girado = false;
            if (item.y > lugar.y) return false;
            if (item.x <= w && item.z <= d) return true;
            girado = item.z <= w && item.x <= d;
            return girado;
        }
    }
}
