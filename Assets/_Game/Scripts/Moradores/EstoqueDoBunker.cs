using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>Os tipos dos lugares do estoque (a decisão 12 do Plano_NPCs_Bunker.md da pasta de arte).</summary>
    public enum TipoDoEstoque : byte { Deposito, Despensa, Agua, Suporte, Fumo, Enfermaria }

    /// <summary>
    /// O estoque do bunker para os moradores (a parte 6, a segunda fatia): o que está nas estantes e nas bancadas que guardam
    /// (<see cref="EstantesDoBunker"/>; o que está no chão não conta). Cada uma tem um tipo, pelo cômodo: a despensa (a do B2 e
    /// a estante da cozinha: a comida, meta 12), a bancada da água (a bancada de inox do refeitório: a bebida, meta 6) e o
    /// depósito (o resto: qualquer coisa, sem meta). O suporte dos tacos e a mesinha do fumo vieram com o lazer (a parte 8),
    /// e o armário da enfermaria vem com a saúde. O morador pega no lugar mais perto que tenha o que ele quer, de qualquer tipo (<see cref="Procurar"/>).
    /// No começo, o bunker ganha uma reserva de 7 dias para o grupo (<see cref="PorAReserva"/>), na despensa.
    /// </summary>
    public static class EstoqueDoBunker
    {
        /// <summary>O caminho de um andar ao outro pela escada, para a distância da procura (m).</summary>
        const float PorAndar = 15f;

        public static string Nome(TipoDoEstoque t) => t switch
        {
            TipoDoEstoque.Despensa => "Despensa",
            TipoDoEstoque.Agua => "Bancada da água",
            TipoDoEstoque.Suporte => "Suporte dos tacos",
            TipoDoEstoque.Fumo => "Mesinha do fumo",
            TipoDoEstoque.Enfermaria => "Armário da enfermaria",
            _ => "Depósito",
        };

        /// <summary>Os tipos de lugar (quantos há em <see cref="TipoDoEstoque"/>).</summary>
        public const int Tipos = 6;
        /// <summary>A meta mais alta que o computador aceita.</summary>
        public const int MetaMaxima = 60;

        static readonly int[] _metas = MetasDoComeco();

        static int[] MetasDoComeco()
        {
            var m = new int[Tipos];
            for (int i = 0; i < Tipos; i++) m[i] = MetaDoComeco((TipoDoEstoque)i);
            return m;
        }

        /// <summary>A meta de cada lugar (quanto ele quer ter; -1: sem meta). O organizador (a parte 9) mantém; o computador muda.</summary>
        public static int Meta(TipoDoEstoque t) => (int)t < Tipos ? _metas[(int)t] : -1;

        /// <summary>As metas de todos os tipos (na ordem do <see cref="TipoDoEstoque"/>), para o terminal e o save.</summary>
        public static int[] Metas => (int[])_metas.Clone();

        /// <summary>No anfitrião: as metas do mundo salvo (vazia ou de outro tamanho: as do começo).</summary>
        public static void CarregarAsMetas(List<int> salvas)
        {
            for (int i = 0; i < Tipos; i++)
            {
                int m = MetaDoComeco((TipoDoEstoque)i);
                if (m >= 0 && salvas != null && i < salvas.Count && salvas[i] >= 0) m = Mathf.Min(salvas[i], MetaMaxima);
                _metas[i] = m;
            }
        }

        /// <summary>No anfitrião: muda a meta de um lugar que tem meta (o computador). Devolve se mudou.</summary>
        public static bool MudarMeta(TipoDoEstoque t, int meta)
        {
            int i = (int)t;
            if (i >= Tipos || _metas[i] < 0) return false;
            meta = Mathf.Clamp(meta, 0, MetaMaxima);
            if (_metas[i] == meta) return false;
            _metas[i] = meta;
            return true;
        }

        /// <summary>A meta do começo de cada lugar (a tabela da decisão 12).</summary>
        public static int MetaDoComeco(TipoDoEstoque t) => t switch
        {
            TipoDoEstoque.Despensa => 12,
            TipoDoEstoque.Agua => 6,
            TipoDoEstoque.Suporte => 3,
            TipoDoEstoque.Fumo => 2,
            TipoDoEstoque.Enfermaria => 5,
            _ => -1,
        };

        /// <summary>O tipo do lugar da estante (o índice do móvel em <see cref="MoveisDoBunker.Todos"/>), pelo cômodo dela.</summary>
        public static TipoDoEstoque TipoDe(int estante)
        {
            var todos = MoveisDoBunker.Todos;
            if (estante < 0 || estante >= todos.Length) return TipoDoEstoque.Deposito;
            var m = todos[estante];
            // o suporte dos tacos e a mesinha do fumo (a parte 8: os modelos deles)
            if (m.modelo == "SuporteDeTacos") return TipoDoEstoque.Suporte;
            if (m.modelo == "MesaDeCanto") return TipoDoEstoque.Fumo;
            var k = PlantaDeBunker.DoAndar(m.andar).Em(m.lugar.x, m.lugar.z);
            if (k == null) return TipoDoEstoque.Deposito;
            if (m.andar == 2 && m.modelo == "BancadaInox" && k.tipo == "refeitorio") return TipoDoEstoque.Agua;
            if (m.andar == 2 && (k.tipo == "despensa" || (k.tipo == "cozinha" && m.modelo != "BancadaInox"))) return TipoDoEstoque.Despensa;
            return TipoDoEstoque.Deposito;
        }

        /// <summary>Se o item é o que o lugar guarda (para a meta: a comida na despensa, a bebida na bancada da água).</summary>
        public static bool DoTipo(TipoDoEstoque t, Item i) => t switch
        {
            TipoDoEstoque.Despensa => EhComida(i),
            TipoDoEstoque.Agua => EhBebida(i),
            TipoDoEstoque.Fumo => Itens.EhFumo(i),
            TipoDoEstoque.Suporte => Itens.EhSinuca(i),
            TipoDoEstoque.Enfermaria => Itens.Loot(i)?.tipo == TipoDeLoot.Remedio,
            _ => true,
        };

        /// <summary>As estantes de um tipo (os índices dos móveis).</summary>
        public static List<int> Estantes(TipoDoEstoque t)
        {
            var lista = new List<int>();
            var todos = MoveisDoBunker.Todos;
            for (int i = 0; i < todos.Length; i++)
                if (todos[i].andar < PlantaDeBunker.Andares - 1 && EstantesDoBunker.EhEstante(todos[i].modelo) && TipoDe(i) == t) lista.Add(i);
            return lista;
        }

        public static bool EhComida(Item i)
        {
            var l = Itens.Loot(i);
            return l != null && l.tipo == TipoDeLoot.Comida && l.fome > 0f;
        }

        public static bool EhBebida(Item i)
        {
            var l = Itens.Loot(i);
            return l != null && l.tipo == TipoDeLoot.Bebida && l.sede > 0f;
        }

        /// <summary>O morador alcança o item (a tábua até 2 m; a de cima das estantes de aço, não).</summary>
        public static bool Alcanca(ItemNaEstante it) => EstantesDoBunker.AlturaDe(it) <= AcoesDoCorpo.AlcanceNaEstante;

        /// <summary>
        /// No anfitrião: o item que o morador quer (<paramref name="quer"/>) mais perto dele (o andar e o ponto, nas
        /// coordenadas do bunker), com um lugar livre na frente da estante (<paramref name="livre"/>) no lado do item.
        /// </summary>
        public static bool Procurar(Func<Item, bool> quer, int andar, Vector3 pos, Func<int, bool> livre, out ItemNaEstante achado, out int lugar)
        {
            achado = default;
            lugar = -1;
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned) return false;
            var todos = LugaresDoBunker.Todos;
            float melhor = float.MaxValue;
            foreach (var it in e.Guardados)
            {
                if (!quer(it.item) || !Alcanca(it)) continue;
                float lado = EstantesDoBunker.LadoDe(it);
                int l = -1;
                float dl = float.MaxValue;
                for (int k = 0; k < todos.Length; k++)
                {
                    if (todos[k].tipo != TipoDoLugar.Estante || todos[k].movel != it.estante || !livre(k)) continue;
                    float d = Mathf.Abs(todos[k].lado - lado);
                    if (d < dl)
                    {
                        dl = d;
                        l = k;
                    }
                }
                if (l < 0) continue;
                var p = todos[l].raiz - pos;
                float dist = new Vector2(p.x, p.z).magnitude + Mathf.Abs(todos[l].andar - andar) * PorAndar + dl;
                if (dist >= melhor) continue;
                melhor = dist;
                achado = it;
                lugar = l;
            }
            return lugar >= 0;
        }

        // ------------------------------------------------------------------ a conta

        /// <summary>Quanto há (só o que eles alcançam): as comidas e a fome que elas dão, as bebidas e a sede.</summary>
        public static (int comidas, float fome, int bebidas, float sede) Contar()
        {
            int comidas = 0, bebidas = 0;
            float fome = 0f, sede = 0f;
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned) return (0, 0f, 0, 0f);
            foreach (var it in e.Guardados)
            {
                var l = Itens.Loot(it.item);
                if (l == null || !Alcanca(it)) continue;
                int n = Itens.Quantos(it.item, it.qtd);
                if (EhComida(it.item)) comidas += n;
                if (EhBebida(it.item)) bebidas += n;
                fome += l.fome * n;
                sede += l.sede * n;
            }
            return (comidas, Mathf.Max(0f, fome), bebidas, Mathf.Max(0f, sede));
        }

        /// <summary>Quantos dias o estoque dura para tantos moradores (a fome e a sede que eles perdem por dia).</summary>
        public static float Dias(int moradores)
        {
            if (moradores <= 0) return 0f;
            var (_, fome, _, sede) = Contar();
            float porDia = 24f * moradores;
            return Mathf.Min(fome / (AgendaDoMorador.FomePorHora * porDia), sede / (AgendaDoMorador.SedePorHora * porDia));
        }

        /// <summary>Quantos itens do tipo o lugar tem (para a meta).</summary>
        public static int Tem(TipoDoEstoque t)
        {
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned) return 0;
            int n = 0;
            foreach (var it in e.Guardados)
                if (TipoDe(it.estante) == t && DoTipo(t, it.item)) n += Itens.Quantos(it.item, it.qtd);
            return n;
        }

        // ------------------------------------------------------------------ a reserva

        // as comidas e as bebidas da reserva, com o peso de cada uma
        static readonly (Item item, int peso)[] ComidasDaReserva =
        {
            (Item.Comida, 8), (Item.Feijao, 6), (Item.Carne, 5), (Item.Milho, 4), (Item.Sopa, 4),
        };
        static readonly (Item item, int peso)[] BebidasDaReserva = { (Item.Agua, 9), (Item.Leite, 1) };

        /// <summary>
        /// No anfitrião: a reserva de <paramref name="dias"/> dias para tantos moradores (a decisão 11: por dia, cada um gasta
        /// três quartos de uma lata e uma garrafa), na despensa (e, se não couber, no depósito). Devolve quantos itens couberam.
        /// </summary>
        public static int PorAReserva(int moradores, int dias)
        {
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsServer || moradores <= 0) return 0;
            var despensa = Estantes(TipoDoEstoque.Despensa);
            var deposito = Estantes(TipoDoEstoque.Deposito);
            int comidas = Mathf.CeilToInt(0.75f * moradores * dias), bebidas = moradores * dias, postos = 0;
            postos += Repartir(e, ComidasDaReserva, comidas, despensa, deposito);
            // a água: a meta da bancada da água nela, e o resto na despensa
            int naBancada = Mathf.Min(Meta(TipoDoEstoque.Agua), bebidas);
            postos += Repartir(e, new[] { (Item.Agua, 1) }, naBancada, Estantes(TipoDoEstoque.Agua), despensa);
            postos += Repartir(e, BebidasDaReserva, bebidas - naBancada, despensa, deposito);
            return postos;
        }

        static int Repartir(EstantesDoBunker e, (Item item, int peso)[] tipos, int total, List<int> primeiro, List<int> sobra)
        {
            int pesos = 0;
            foreach (var (_, p) in tipos) pesos += p;
            int postos = 0, resta = total;
            for (int k = 0; k < tipos.Length; k++)
            {
                int n = k == tipos.Length - 1 ? resta : Mathf.RoundToInt((float)total * tipos[k].peso / pesos);
                n = Mathf.Min(n, resta);
                resta -= n;
                for (int j = 0; j < n; j++)
                    if (e.GuardarNasEstantes(primeiro, tipos[k].item, 1, 0, 0) || e.GuardarNasEstantes(sobra, tipos[k].item, 1, 0, 0)) postos++;
            }
            return postos;
        }
    }
}
