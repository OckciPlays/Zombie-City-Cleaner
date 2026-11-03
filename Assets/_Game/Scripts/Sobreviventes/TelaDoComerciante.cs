using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A tela do comércio (02/11/2026, no estilo da tela do caixa do Dia 0): à esquerda, o que o comerciante vende (o
    /// estoque de hoje, o preço e "Comprar"); à direita, o que você tem e ele compra (só a categoria dele, pela metade do
    /// valor, e "Vender"); em cima, o seu dinheiro. Os pedidos vão para o anfitrião, que confere tudo. Fecha com ESC, E ou
    /// TAB, ou longe dele, ou se ele cair, ou se o acampamento ficar hostil.
    /// </summary>
    public class TelaDoComerciante : MonoBehaviour
    {
        static TelaDoComerciante _tela;
        Sobrevivente _quem;
        bool _aberta;
        float _abriuEm, _u;
        Vector2 _rolarA, _rolarB;
        GUIStyle _titulo, _texto, _pequeno, _botao, _preco;

        public static bool Aberta => _tela != null && _tela._aberta;

        public static void Abrir(Sobrevivente quem)
        {
            if (_tela == null) _tela = new GameObject("Tela do comerciante").AddComponent<TelaDoComerciante>();
            _tela._quem = quem;
            _tela._abriuEm = Time.unscaledTime;
            if (_tela._aberta) return;
            _tela._aberta = true;
            MenuOnline.Tela(_tela, true);
            MenuOnline.FecharTelas += _tela.Fechar;
        }

        void Fechar()
        {
            if (!_aberta) return;
            _aberta = false;
            MenuOnline.Tela(this, false);
            MenuOnline.FecharTelas -= Fechar;
        }

        void OnDestroy() => Fechar();

        void Update()
        {
            if (!_aberta) return;
            var j = JogadorRede.Local;
            var m = AcampamentosDoMundo.Instancia;
            if (j == null || m == null || _quem == null || _quem.Morto || !_quem.Comercio(out int a, out _, out _) || m.Hostil(a)
                || (j.transform.position - _quem.transform.position).sqrMagnitude > 5f * 5f)
            {
                Fechar();
                return;
            }
            var k = Keyboard.current;
            if (k != null && Time.unscaledTime - _abriuEm > 0.2f && (k.eKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) Fechar();
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * u), fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * u) };
            _texto.normal.textColor = new Color(0.86f, 0.84f, 0.78f);
            _pequeno = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(12 * u), wordWrap = true };
            _pequeno.normal.textColor = new Color(0.7f, 0.68f, 0.62f);
            _preco = new GUIStyle(_texto) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * u) };
        }

        void OnGUI()
        {
            if (!_aberta) return;
            var j = JogadorRede.Local;
            var m = AcampamentosDoMundo.Instancia;
            if (j == null || j.Inventario == null || m == null || _quem == null || !_quem.Comercio(out int a, out int banca, out var categoria)) return;
            GUI.depth = -12;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            var inv = j.Inventario;
            int dinheiro = inv.Dinheiro.Value;
            float w = 900 * u, h = 560 * u, x0 = (Screen.width - w) / 2f, y0 = (Screen.height - h) / 2f;
            Caixa(new Rect(x0, y0, w, h), new Color(0.07f, 0.07f, 0.065f, 0.96f));
            Caixa(new Rect(x0, y0, w, 3 * u), new Color(0.72f, 0.58f, 0.2f, 0.95f));
            var planta = m.Planta(a);
            GUI.Label(new Rect(x0 + 20 * u, y0 + 12 * u, w * 0.7f, 28 * u), $"COMERCIANTE  ·  {PrecosDosSobreviventes.Nome(categoria).ToUpperInvariant()}", _titulo);
            GUI.Label(new Rect(x0 + 20 * u, y0 + 38 * u, w * 0.7f, 20 * u), planta != null ? planta.Lugar.Nome : "", _pequeno);
            GUI.Label(new Rect(x0 + w - 300 * u, y0 + 14 * u, 280 * u, 28 * u), $"Você tem {PrecosDoDiaZero.Dinheiro(dinheiro)}", _preco);

            float meio = w / 2f, topo = y0 + 70 * u, alto = h - 140 * u;
            // ele vende
            GUI.Label(new Rect(x0 + 20 * u, topo, meio - 30 * u, 22 * u), "ELE VENDE", _titulo);
            var itens = PrecosDosSobreviventes.Itens(categoria);
            var vistaA = new Rect(x0 + 20 * u, topo + 28 * u, meio - 30 * u, alto - 28 * u);
            float linha = 40 * u;
            _rolarA = GUI.BeginScrollView(vistaA, _rolarA, new Rect(0, 0, vistaA.width - 18 * u, Mathf.Max(itens.Length * linha, vistaA.height - 4)));
            for (int i = 0; i < itens.Length; i++)
            {
                float y = i * linha;
                int tem = m.Estoque(a, banca, i);
                int preco = PrecosDosSobreviventes.PrecoDeCompra(itens[i]);
                Linha(new Rect(0, y, vistaA.width - 18 * u, linha - 4 * u), inv, itens[i], tem > 0 ? $"{Primeira(Itens.Nome(itens[i]))}  ·  {tem} {(tem == 1 ? "restante" : "restantes")}" : $"{Primeira(Itens.Nome(itens[i]))}  ·  acabou",
                      PrecoDoDiaZero(preco), tem > 0 && dinheiro >= preco, "Comprar", () => m.PedirComprar(a, banca, i));
            }
            GUI.EndScrollView();

            // você vende
            GUI.Label(new Rect(x0 + meio + 10 * u, topo, meio - 30 * u, 22 * u), "VOCÊ VENDE", _titulo);
            var meus = new List<ItemNaMochila>();
            foreach (var it in inv.Mochila) if (PrecosDosSobreviventes.Compra(categoria, it.item)) meus.Add(it);
            var vistaB = new Rect(x0 + meio + 10 * u, topo + 28 * u, meio - 30 * u, alto - 28 * u);
            _rolarB = GUI.BeginScrollView(vistaB, _rolarB, new Rect(0, 0, vistaB.width - 18 * u, Mathf.Max(meus.Count * linha, vistaB.height - 4)));
            if (meus.Count == 0) GUI.Label(new Rect(0, 0, vistaB.width - 20 * u, 60 * u), $"Nada na sua mochila que ele compre. Ele só compra {PrecosDosSobreviventes.Nome(categoria)}.", _pequeno);
            for (int i = 0; i < meus.Count; i++)
            {
                var it = meus[i];
                ushort id = it.id;
                int preco = PrecosDosSobreviventes.PrecoDeVenda(it);
                Linha(new Rect(0, i * linha, vistaB.width - 18 * u, linha - 4 * u), inv, it.item, Descricao(it), "+" + PrecoDoDiaZero(preco), preco > 0, "Vender",
                      () => m.PedirVender(a, banca, id));
            }
            GUI.EndScrollView();

            float by = y0 + h - 58 * u;
            GUI.Label(new Rect(x0 + 20 * u, by, w - 200 * u, 44 * u),
                      "Só dinheiro. Ele compra só o que vende, pela metade (e menos, se estiver gasto). O estoque volta a cada amanhecer. ESC, E ou TAB fecham.", _pequeno);
            if (GUI.Button(new Rect(x0 + w - 150 * u, by + 4 * u, 130 * u, 36 * u), "Fechar", _botao)) Fechar();
        }

        // uma linha: o ícone, o nome, o preço e o botão
        void Linha(Rect r, InventarioJogador inv, Item item, string nome, string preco, bool pode, string acao, System.Action clique)
        {
            float u = _u;
            Caixa(r, new Color(1f, 1f, 1f, 0.04f));
            var icone = inv.IconeDe(item);
            if (icone != null) GUI.DrawTexture(new Rect(r.x + 4 * u, r.y + 2 * u, r.height - 4 * u, r.height - 4 * u), icone, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x + r.height + 8 * u, r.y, r.width - r.height - 200 * u, r.height), nome, _texto);
            GUI.Label(new Rect(r.x + r.width - 190 * u, r.y, 80 * u, r.height), preco, _preco);
            GUI.enabled = pode;
            if (GUI.Button(new Rect(r.x + r.width - 100 * u, r.y + 4 * u, 94 * u, r.height - 8 * u), acao, _botao)) clique();
            GUI.enabled = true;
        }

        static string PrecoDoDiaZero(int preco) => PrecosDoDiaZero.Dinheiro(preco);

        static string Primeira(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // o nome do item com o estado: as balas da caixa, quantos na pilha, o desgaste, a carga da peça, os litros
        static string Descricao(ItemNaMochila it)
        {
            string nome = Primeira(Itens.Nome(it.item));
            if (Itens.EhMunicao(it.item)) return $"{nome}  ·  {Itens.Quantos(it.item, it.qtd)} balas";
            if (Itens.EmPilha(it.item)) return $"{nome}  ·  {Itens.Quantos(it.item, it.qtd)}";
            if (it.item == Item.Galao) return $"{nome}  ·  {InventarioJogador.LitrosDe(it):0} L";
            if (Itens.ComEstado(it.item)) return $"{nome}  ·  {it.qtd}%";
            if (Itens.ComDesgaste(it.item) && it.desgaste > 0) return $"{nome}  ·  {100 - it.desgaste}%";
            return nome;
        }

        static void Caixa(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
