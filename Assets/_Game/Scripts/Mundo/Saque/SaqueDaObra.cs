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
    /// A farmácia e cada andar do hospital (05/11/2026, as unidades de loja: <see cref="PontoDeMovel.Farmacia"/>) têm a
    /// quantidade e as chances deles (<see cref="CatalogoDoSaque.QuantosNaLoja"/>, <see cref="CatalogoDoSaque.ChancesDa"/>) e
    /// o saque em cima dos móveis antigos (<see cref="LugaresDoSaque.DoAntigo"/>, com a <see cref="CatalogoDoSaque.RegraDaLoja"/>).
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
                if (p.unidade != unidade) continue;
                if (!MobiliaNova.EhNovo(p.tipo))
                {
                    // o móvel antigo, só nas unidades de loja (a farmácia, o hospital): a lavanderia das casas tem a
                    // prateleira antiga com saque, e ganhar lugares mudaria o saque das casas dos mundos salvos
                    if (p.saque && p.DeLoja) LugaresDoAntigo(p, i, semente, unidade, r);
                    continue;
                }
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
                        // onde nada cabe (o lugar estreito do criado-mudo, para as roupas), fica de fora
                        if (Preencher(livre)) r.Add(livre);
                    }
                }
            }
            return r;
        }

        // os itens de cada raridade que o lugar aceita e que cabem nele; false se nenhum
        static bool Preencher(Livre livre)
        {
            bool algum = false;
            for (int k = 0; k < 5; k++)
            {
                var lista = livre.cabem[k] = new List<(CatalogoDoSaque.Item, bool)>();
                foreach (var c in CatalogoDoSaque.DaRaridade((Raridade)k))
                    if ((c.especie & livre.aceita) != 0 && Cabe(c.tamanho, livre.tamanho, out bool girado)) lista.Add((c, girado));
                algum |= lista.Count > 0;
            }
            return algum;
        }

        /// <summary>
        /// Os lugares de um móvel antigo de uma unidade de loja (05/11/2026, a etapa 2: a farmácia e o hospital), em 'r': os
        /// da tabela dos antigos (<see cref="LugaresDoSaque.DoAntigo"/>, medidos no Cidade_Mobilia.fbx no tamanho em que o
        /// modelo foi feito, só à vista: os antigos não abrem), na escala do ponto (o tamanho dele sobre o de
        /// <see cref="Mobilia.Feitos"/>, como o <see cref="Mobilia.Por"/> desenha), com o giro dele e a peça 0. O modelo é o
        /// que a obra desenha (com a sorte do ponto: as caixas A e B). No ponto espelhado, o móvel é desenhado espelhado: o
        /// lugar também (o x dele, do outro lado).
        /// </summary>
        static void LugaresDoAntigo(PontoDeMovel p, int i, int semente, int unidade, List<Livre> r)
        {
            string modelo = Mobilia.ModeloDo(p, Mobilia.Sorte(semente, i));
            if (modelo == null || !Mobilia.Feitos.TryGetValue(modelo, out var feito)) return;
            var regra = CatalogoDoSaque.RegraDaLoja(unidade, p.comodo);
            var lugares = LugaresDoSaque.DoAntigo(modelo);
            if (regra == null || lugares.Length == 0) return;
            var esc = new Vector3(p.tamanho.x / feito.x, p.tamanho.y / feito.y, p.tamanho.z / feito.z);
            var giro = Quaternion.Euler(0f, p.giro, 0f);
            for (int j = 0; j < lugares.Length; j++)
            {
                var l = lugares[j];
                var aceita = CatalogoDoSaque.Aceita(regra, l) & ~EspecieDoSaque.Sinuca;
                if (aceita == EspecieDoSaque.Nenhuma) continue;
                var meio = l.meio;
                if (p.espelhado) meio.x = -meio.x;
                var livre = new Livre
                {
                    ponto = i, peca = 0, lugar = j, sala = p.sala, comodo = p.comodo, andar = p.andar, modelo = modelo,
                    l = l, aceita = aceita, peso = regra.peso / lugares.Length,
                    meio = p.centro + Vector3.up * p.altura + giro * Vector3.Scale(meio, esc), giro = p.giro,
                    tamanho = Vector3.Scale(l.tamanho, esc),
                };
                if (Preencher(livre)) r.Add(livre);
            }
        }

        /// <summary>
        /// Uma peça de móvel com saque fora dos pontos dos móveis (o Hospital Principal, 08/11/2026: os móveis dele vêm do
        /// <see cref="MoveisDoHospital"/>): o modelo, onde (nas coordenadas da obra), o giro e a escala dele, o ponto (o número
        /// do móvel no andar: a chave do saque), o cômodo (o tipo e o número dele) e o que ele aceita.
        /// </summary>
        public struct PecaComSaque
        {
            public string modelo, comodo;
            public int ponto, sala;
            public Vector3 origem;
            public float giro, escala;
            public CatalogoDoSaque.Regra regra;
        }

        /// <summary>
        /// O saque de uma lista de peças (o Hospital Principal: um andar), como o de uma moradia: de 'min' a 'max' itens, a
        /// raridade pelas 'chances', o cômodo pelo 'pesoDoComodo' (no máximo <see cref="CatalogoDoSaque.PorComodo"/> no
        /// mesmo), o lugar pelo peso do móvel e o item entre os que o lugar aceita e que cabem nele. A peça é a 0.
        /// </summary>
        public static void SortearPecas(IList<PecaComSaque> pecas, ulong semente, int unidade, int min, int max, float[] chances,
                                        System.Func<string, float> pesoDoComodo, List<ItemDoSaque> saida)
        {
            var livres = new List<Livre>();
            foreach (var p in pecas)
            {
                if (p.regra == null) continue;
                var lugares = LugaresDoSaque.Do(p.modelo);
                var giro = Quaternion.Euler(0f, p.giro, 0f);
                for (int j = 0; j < lugares.Length; j++)
                {
                    var l = lugares[j];
                    var aceita = CatalogoDoSaque.Aceita(p.regra, l) & ~EspecieDoSaque.Sinuca;
                    if (aceita == EspecieDoSaque.Nenhuma) continue;
                    var livre = new Livre
                    {
                        ponto = p.ponto, peca = 0, lugar = j, sala = p.sala, comodo = p.comodo, andar = 0, modelo = p.modelo,
                        l = l, aceita = aceita, peso = p.regra.peso / lugares.Length,
                        meio = p.origem + giro * (l.meio * p.escala), giro = p.giro, tamanho = l.tamanho * p.escala,
                    };
                    if (Preencher(livre)) livres.Add(livre);
                }
            }
            if (livres.Count == 0) return;
            var rng = new Sorteio(semente);
            int n = rng.Entre(min, max + 1);
            var porSala = new Dictionary<int, int>();
            // o sorteio à parte das armas e da munição reais (o da develop, 09/11/2026), do andar
            ulong sal = Sorteio.Misturar(semente ^ ((ulong)(uint)unidade << 8) ^ 0xA2A5EA1500000000UL);
            for (int k = 0; k < n; k++) Colocar(rng, livres, porSala, false, unidade, chances, saida, sal, pesoDoComodo);
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
            // a farmácia e o hospital (as unidades de loja): a quantidade e as chances deles, sem porão
            bool loja = unidade >= PontoDeMovel.Farmacia;
            var rng = new Sorteio(Sorteio.Misturar(((ulong)(uint)semente << 20) ^ (ulong)(uint)unidade ^ 0x5A0E5A9E0000UL));
            var (min, max) = loja ? CatalogoDoSaque.QuantosNaLoja(unidade) : CatalogoDoSaque.Quantos(apartamento, sobrado && !apartamento);
            var chances = CatalogoDoSaque.ChancesDa(unidade);
            int n = rng.Entre(min, max + 1);
            int noPorao = 0;
            if (!loja && !apartamento && porao)
                for (int k = 0; k < CatalogoDoSaque.TentativasNoPorao; k++)
                    if (rng.Chance(CatalogoDoSaque.ChanceDoPorao)) noPorao++;
            var porSala = new Dictionary<int, int>();
            ulong sal = Sorteio.Misturar(((ulong)(uint)semente << 24) ^ (ulong)(uint)unidade ^ 0xA2A5EA1500000000UL);
            for (int k = 0; k < n; k++) Colocar(rng, livres, porSala, false, unidade, chances, saida, sal);
            for (int k = 0; k < noPorao; k++) Colocar(rng, livres, porSala, true, unidade, chances, saida, sal);
        }

        // um item num lugar livre (só no porão, com 'soPorao'): a raridade (pelas chances da unidade), o lugar que aceita um
        // item dela e o item; o lugar usado sai da lista
        static void Colocar(Sorteio rng, List<Livre> livres, Dictionary<int, int> porSala, bool soPorao, int unidade, float[] chances, List<ItemDoSaque> saida,
                            ulong sal, System.Func<string, float> pesoDoComodo = null)
        {
            float x = rng.Fracao();
            int r = 0;
            for (; r < 4; r++)
            {
                x -= chances[r];
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
                    if (!tem) salas.Add((l.sala, pesoDoComodo != null ? pesoDoComodo(l.comodo) : CatalogoDoSaque.PesoDoComodo(l.comodo, unidade)));
                }
                int sala = salas[Escolher(rng, salas.Count, i => salas[i].peso)].sala;
                // o lugar: pelo peso do móvel (dividido pelos lugares dele: um móvel com muitos não ganha mais)
                aceitam.RemoveAll(i => livres[i].sala != sala);
                int k = aceitam[Escolher(rng, aceitam.Count, i => livres[aceitam[i]].peso)];
                var lugar = livres[k];
                livres.RemoveAt(k);
                var it = Item(rng, lugar, lugar.cabem[r], sal);
                it.unidade = unidade;
                saida.Add(it);
                porSala[sala] = porSala.TryGetValue(sala, out int antes) ? antes + 1 : 1;
                return;
            }
        }

        // outro item que cabe no lugar, sem as armas e a munição reais: da raridade de baixo (e das outras de baixo), ou, sem
        // nenhum, da mesma e das de cima. Para baixo, e não na mesma: no lendário, a arma longa virava quase sempre a visão
        // noturna (o único lendário que não é arma)
        static bool OutroNoLugar(Sorteio rng, Livre lugar, int raridade, out (CatalogoDoSaque.Item item, bool girado) outro)
        {
            outro = default;
            var bons = new List<(CatalogoDoSaque.Item, bool)>();
            for (int passo = 0; passo < 5; passo++)
            {
                int r = passo < raridade ? raridade - 1 - passo : passo;
                if (r < 0 || r > 4) continue;
                bons.Clear();
                foreach (var c in lugar.cabem[r]) if (CatalogoDoSaque.FatorDe(c.item) >= 1f) bons.Add(c);
                if (bons.Count == 0) continue;
                outro = bons[rng.Entre(0, bons.Count)];
                return true;
            }
            return false;
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
        static ItemDoSaque Item(Sorteio rng, Livre lugar, List<(CatalogoDoSaque.Item item, bool girado)> cabem, ulong sal)
        {
            var (esc, gir) = cabem[rng.Entre(0, cabem.Count)];
            // as armas de fogo e a munição reais (09/11/2026, a 2ª leva): só ficam com a fração da chance delas; no resto, outro
            // item do mesmo lugar. O sorteio é à parte (do lugar), e o de sempre continua igual: o resto do saque não muda
            float fator = CatalogoDoSaque.FatorDe(esc);
            if (fator < 1f)
            {
                var r2 = new Sorteio(Sorteio.Misturar(sal ^ ((ulong)(uint)lugar.ponto << 20) ^ ((ulong)(uint)lugar.peca << 10) ^ (ulong)(uint)lugar.lugar));
                if (!r2.Chance(fator) && OutroNoLugar(r2, lugar, (int)esc.raridade, out var outro)) (esc, gir) = outro;
            }
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
