using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O comércio dos acampamentos (02/11/2026, a pedido: "comerciantes fixos, cada um vende uma coisa", "só dinheiro",
    /// "vender sempre mais barato que comprar", "o estoque acaba e repõe a cada dia"):
    ///  - o estoque de cada comerciante sai da semente (as armas, uma de cada ou nenhuma; o resto, de 1 ao máximo) e fica na
    ///    rede (<see cref="EstoqueDoComerciante"/>); a cada amanhecer (a partir das 6 h), cada produto volta uma parte;
    ///  - comprar: o anfitrião confere o comerciante (vivo, a até 4 m, o acampamento amigo), o estoque, o dinheiro e o lugar
    ///    na mochila; o item entra como novo (a arma com o pente cheio, a caixa cheia, a peça nova, o galão cheio);
    ///  - vender: só o que é da categoria dele, pela metade do valor (<see cref="PrecosDosSobreviventes.PrecoDeVenda"/>); o
    ///    comerciante tem dinheiro sem fim, e o que ele compra entra no estoque (até o máximo).
    /// </summary>
    public partial class AcampamentosDoMundo
    {
        /// <summary>Quanto o comerciante (o acampamento, a banca) tem do produto.</summary>
        public int Estoque(int a, int banca, int produto)
        {
            int k = IndiceDoEstoque(a, banca, produto);
            return k >= 0 ? _estoque[k].qtd : 0;
        }

        int IndiceDoEstoque(int a, int banca, int produto)
        {
            for (int k = 0; k < _estoque.Count; k++)
            {
                var x = _estoque[k];
                if (x.acampamento == a && x.banca == banca && x.produto == produto) return k;
            }
            return -1;
        }

        void EncherOEstoque()
        {
            _estoqueFeito = true;
            _estoque.Clear();
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                for (int b = 0; b < p.Bancas.Count; b++)
                {
                    var r = new System.Random(unchecked(Dados.Semente * 3571 + a * 911 + b * 37 + 5));
                    var itens = PrecosDosSobreviventes.Itens(p.Bancas[b].categoria);
                    for (int i = 0; i < itens.Length; i++)
                    {
                        int qtd = QtdInicial(itens[i], r);
                        _estoque.Add(new EstoqueDoComerciante { acampamento = (byte)a, banca = (byte)b, produto = (byte)i, qtd = (byte)qtd });
                    }
                }
            }
        }

        // quanto o comerciante tem do produto no começo (a semente: as armas, uma ou nenhuma; o resto, de 1 ao máximo)
        static int QtdInicial(Item item, System.Random r)
        {
            int max = PrecosDosSobreviventes.Maximo(item);
            return Itens.EhArma(item) ? (r.NextDouble() < (item == Item.Sniper ? 0.3 : 0.6) ? 1 : 0) : 1 + r.Next(max);
        }

        /// <summary>
        /// Completa o estoque que veio do save (05/11/2026, a tala nova na banca dos remédios): o produto que entrou no fim
        /// da lista de uma categoria depois do save não tinha linha, e ficava "Acabou" para sempre. Ganha a quantidade que a
        /// semente daria a ele num mundo novo; as linhas que já existem não mudam.
        /// </summary>
        void CompletarOEstoque(List<EstoqueDoComerciante> lista)
        {
            var tem = new HashSet<int>();
            foreach (var x in lista) tem.Add((x.acampamento << 16) | (x.banca << 8) | x.produto);
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                if (p == null) continue;
                for (int b = 0; b < p.Bancas.Count; b++)
                {
                    var r = new System.Random(unchecked(Dados.Semente * 3571 + a * 911 + b * 37 + 5));
                    var itens = PrecosDosSobreviventes.Itens(p.Bancas[b].categoria);
                    for (int i = 0; i < itens.Length; i++)
                    {
                        int qtd = QtdInicial(itens[i], r);
                        if (!tem.Contains((a << 16) | (b << 8) | i))
                            lista.Add(new EstoqueDoComerciante { acampamento = (byte)a, banca = (byte)b, produto = (byte)i, qtd = (byte)qtd });
                    }
                }
            }
        }

        /// <summary>No anfitrião: a reposição do amanhecer (forçada: agora, em todos).</summary>
        public void Repor(bool forcar)
        {
            if (!IsServer) return;
            Estados();
            for (int a = 0; a < _estados.Count; a++)
            {
                var e = _estados[a];
                if (!forcar && (CicloDoDia.Dia <= e.diaDaReposicao || CicloDoDia.Hora < 6f)) continue;
                e.diaDaReposicao = CicloDoDia.Dia;
                var p = Planta(a);
                for (int k = 0; k < _estoque.Count; k++)
                {
                    var x = _estoque[k];
                    if (x.acampamento != a || x.banca >= p.Bancas.Count) continue;
                    var itens = PrecosDosSobreviventes.Itens(p.Bancas[x.banca].categoria);
                    if (x.produto >= itens.Length) continue;
                    int max = PrecosDosSobreviventes.Maximo(itens[x.produto]);
                    if (x.qtd >= max || Random.value > 0.75f) continue;
                    x.qtd = (byte)Mathf.Min(max, x.qtd + Mathf.Max(1, Mathf.CeilToInt(max / 2f)));
                    _estoque[k] = x;
                }
                if (!forcar) Debug.Log($"[Acampamentos] O estoque do {p.Lugar.Nome} foi reposto (dia {CicloDoDia.Dia}).");
            }
        }

        // ------------------------------------------------------------------ os pedidos

        public void PedirComprar(int a, int banca, int produto) => ComprarRpc((byte)a, (byte)banca, (byte)produto);
        public void PedirVender(int a, int banca, ushort id) => VenderRpc((byte)a, (byte)banca, id);

        // quem pede: o inventário dele, se o comerciante está vivo, a até 4 m, e o acampamento é amigo
        InventarioJogador QuemNegocia(RpcParams parametros, int a, int banca, out PlantaDoAcampamento p)
        {
            p = Planta(a);
            if (p == null || banca >= p.Bancas.Count || Hostil(a)) return null;
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var c) || c.PlayerObject == null) return null;
            var g = Grupo(a);
            if (g == null) return null;
            int indice = p.PrimeiroComerciante + banca;
            foreach (var m in g.membros)
                if (m != null && !m.Morto && m.Identidade.Value.indice == indice
                    && (m.transform.position - c.PlayerObject.transform.position).sqrMagnitude < 4.5f * 4.5f)
                    return c.PlayerObject.GetComponent<InventarioJogador>();
            return null;
        }

        [Rpc(SendTo.Server)]
        void ComprarRpc(byte a, byte banca, byte produto, RpcParams parametros = default)
        {
            var inv = QuemNegocia(parametros, a, banca, out var p);
            if (inv == null) return;
            var itens = PrecosDosSobreviventes.Itens(p.Bancas[banca].categoria);
            if (produto >= itens.Length) return;
            int k = IndiceDoEstoque(a, banca, produto);
            if (k < 0 || _estoque[k].qtd == 0)
            {
                inv.AvisarDono("Acabou. Volta amanhã.");
                return;
            }
            var item = itens[produto];
            int preco = PrecosDosSobreviventes.PrecoDeCompra(item);
            if (inv.Dinheiro.Value < preco)
            {
                inv.AvisarDono($"Falta dinheiro: custa {PrecosDoDiaZero.Dinheiro(preco)}.");
                return;
            }
            if (!inv.Receber(item))
            {
                inv.AvisarDono("Não cabe na sua mochila.");
                return;
            }
            inv.Pagar(-preco);
            var x = _estoque[k];
            x.qtd--;
            _estoque[k] = x;
            inv.AvisarDono($"Comprou {Itens.NomeComArtigo(item)} por {PrecosDoDiaZero.Dinheiro(preco)}.");
        }

        [Rpc(SendTo.Server)]
        void VenderRpc(byte a, byte banca, ushort id, RpcParams parametros = default)
        {
            var inv = QuemNegocia(parametros, a, banca, out var p);
            if (inv == null || !inv.Achar(id, out var it)) return;
            var categoria = p.Bancas[banca].categoria;
            if (!PrecosDosSobreviventes.Compra(categoria, it.item))
            {
                inv.AvisarDono("Ele não compra isso.");
                return;
            }
            int preco = PrecosDosSobreviventes.PrecoDeVenda(it);
            if (preco <= 0)
            {
                inv.AvisarDono("Isso não vale nada para ele.");
                return;
            }
            inv.Tirar(id);
            inv.Pagar(preco);
            // o que ele compra vai para a banca (até o máximo)
            int produto = System.Array.IndexOf(PrecosDosSobreviventes.Itens(categoria), it.item);
            int k = IndiceDoEstoque(a, banca, produto);
            if (k >= 0)
            {
                var x = _estoque[k];
                if (x.qtd < PrecosDosSobreviventes.Maximo(it.item))
                {
                    x.qtd++;
                    _estoque[k] = x;
                }
            }
            inv.AvisarDono($"Vendeu {Itens.NomeComArtigo(it.item)} por {PrecosDoDiaZero.Dinheiro(preco)}.");
        }
    }

    /// <summary>O comerciante na mira: E abre o comércio (com o acampamento amigo).</summary>
    public class ComercianteNaMira : Interagivel
    {
        public Sobrevivente sobrevivente;

        public override string Dica(ArmaJogador quem)
        {
            var s = sobrevivente;
            var m = AcampamentosDoMundo.Instancia;
            if (s == null || s.Morto || m == null || !s.Comercio(out int a, out _, out var categoria)) return null;
            if (m.Hostil(a)) return null;
            return $"[E] Negociar ({PrecosDosSobreviventes.Nome(categoria)})";
        }

        public override void Usar(ArmaJogador quem)
        {
            var s = sobrevivente;
            var m = AcampamentosDoMundo.Instancia;
            if (s == null || s.Morto || m == null || !s.Comercio(out int a, out _, out _) || m.Hostil(a)) return;
            TelaDoComerciante.Abrir(s);
        }
    }
}
