using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// A tela do saque (02/11/2026, a pedido: "a mochila que quando abrimos abre um inventário deles que conseguimos
    /// visualizar, também ... trocar mochila caso a mochila do sobrevivente seja maior que a nossa"): a nossa grade à
    /// esquerda e a da mochila no chão à direita. Um clique num item dele o pega; um clique num nosso o guarda lá. Embaixo:
    /// pegar tudo, pegar o dinheiro e trocar de mochila (só quando a dele é maior). Fecha com ESC, E ou TAB, ou longe dela.
    /// </summary>
    public class TelaDoSaque : MonoBehaviour
    {
        static TelaDoSaque _tela;
        ushort _id;
        bool _aberta;
        GUIStyle _titulo, _texto, _botao, _qtd;
        float _u, _abriuEm;
        Vector2 _rolagem;
        string _dica;

        public static bool Aberta => _tela != null && _tela._aberta;

        /// <summary>Abre a tela da mochila no chão (no jogador local).</summary>
        public static void Abrir(ushort id)
        {
            if (_tela == null) _tela = new GameObject("Tela do saque").AddComponent<TelaDoSaque>();
            _tela._id = id;
            _tela._abriuEm = Time.unscaledTime;
            if (!_tela._aberta)
            {
                _tela._aberta = true;
                MenuOnline.Tela(_tela, true);
                MenuOnline.FecharTelas += _tela.Fechar;
            }
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
            var m = MochilasNoChao.Instancia;
            if (j == null || m == null || !m.Achar(_id, out var mochila) ||
                (j.transform.position - mochila.pos).sqrMagnitude > (MochilasNoChao.Alcance + 0.6f) * (MochilasNoChao.Alcance + 0.6f))
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
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * u), fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.92f, 0.88f, 0.8f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), wordWrap = true };
            _texto.normal.textColor = new Color(0.8f, 0.78f, 0.72f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * u) };
            _qtd = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(11 * u), alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
            _qtd.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (!_aberta) return;
            var j = JogadorRede.Local;
            var m = MochilasNoChao.Instancia;
            if (j == null || j.Inventario == null || m == null || !m.Achar(_id, out var mochila)) return;
            GUI.depth = -12;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            var inv = j.Inventario;
            float w = 940 * u, h = 560 * u, x0 = (Screen.width - w) / 2f, y0 = (Screen.height - h) / 2f;
            Caixa(new Rect(x0, y0, w, h), new Color(0.07f, 0.07f, 0.065f, 0.96f));
            Caixa(new Rect(x0, y0, w, 2 * u), new Color(0.72f, 0.58f, 0.2f, 0.9f));
            _dica = null;

            // a nossa
            float meio = w / 2f;
            var minhaGrade = inv.TamanhoDaGrade;
            GUI.Label(new Rect(x0 + 18 * u, y0 + 12 * u, meio - 30 * u, 24 * u), $"SUA {(inv.MochilaVestida.Value != Item.Nada ? Itens.Nome(inv.MochilaVestida.Value).ToUpper() : "ROUPA (OS BOLSOS)")}", _titulo);
            var minhas = new List<ItemNaMochila>();
            foreach (var it in inv.Mochila) minhas.Add(it);
            float celA = Mathf.Min(42f * u, (meio - 40 * u) / Mathf.Max(1, minhaGrade.x));
            var areaA = new Rect(x0 + 18 * u, y0 + 44 * u, celA * minhaGrade.x, celA * minhaGrade.y);
            var clicouA = Grade(areaA, celA, minhaGrade.x, minhaGrade.y, minhas, inv);
            if (clicouA.HasValue)
            {
                if (inv.IdNaMao == clicouA.Value) inv.GuardarPenteDaMao();
                m.PedirGuardar(_id, clicouA.Value);
            }

            // a dele
            string nome = MochilasNoChao.NomeDaMochila(mochila);
            GUI.Label(new Rect(x0 + meio + 12 * u, y0 + 12 * u, meio - 30 * u, 24 * u),
                      $"{nome.ToUpper()}{(mochila.mochila != Item.Nada ? $" · {Itens.Nome(mochila.mochila)}" : "")}", _titulo);
            var deles = m.Itens(_id);
            float celB = Mathf.Min(42f * u, (meio - 40 * u) / Mathf.Max(1, (int)mochila.colunas));
            float alturaB = Mathf.Min(celB * mochila.linhas, 360 * u);
            var vista = new Rect(x0 + meio + 12 * u, y0 + 44 * u, celB * mochila.colunas + 18 * u, alturaB);
            _rolagem = GUI.BeginScrollView(vista, _rolagem, new Rect(0, 0, celB * mochila.colunas, celB * mochila.linhas));
            var clicouB = Grade(new Rect(0, 0, celB * mochila.colunas, celB * mochila.linhas), celB, mochila.colunas, mochila.linhas, deles, inv);
            GUI.EndScrollView();
            if (clicouB.HasValue) m.PedirPegar(_id, clicouB.Value);

            // embaixo: o dinheiro, pegar tudo, trocar de mochila
            float by = y0 + h - 58 * u;
            GUI.Label(new Rect(x0 + 18 * u, by - 40 * u, w - 36 * u, 36 * u),
                      _dica ?? "Clique num item dele para pegar; num seu, para guardar lá. ESC, E ou TAB fecham.", _texto);
            float bw = 200 * u;
            if (GUI.Button(new Rect(x0 + 18 * u, by, bw, 38 * u), "Pegar tudo", _botao)) m.PedirPegarTudo(_id);
            GUI.enabled = mochila.dinheiro > 0;
            if (GUI.Button(new Rect(x0 + 28 * u + bw, by, bw, 38 * u), mochila.dinheiro > 0 ? $"Pegar {PrecosDoDiaZero.Dinheiro(mochila.dinheiro)}" : "Sem dinheiro", _botao))
                m.PedirDinheiro(_id);
            GUI.enabled = MaiorQueAMinha(mochila.mochila, inv.MochilaVestida.Value);
            if (GUI.Button(new Rect(x0 + 38 * u + bw * 2, by, bw + 40 * u, 38 * u), "Trocar de mochila (a dele é maior)", _botao))
                m.PedirTrocarDeMochila(_id);
            GUI.enabled = true;
            if (GUI.Button(new Rect(x0 + w - 18 * u - 120 * u, by, 120 * u, 38 * u), "Fechar", _botao)) Fechar();
        }

        static bool MaiorQueAMinha(Item dele, Item minha)
        {
            if (!Itens.EhMochila(dele)) return false;
            var a = Itens.EspacoDa(dele);
            var b = Itens.EspacoDa(minha);
            return a.x * a.y > b.x * b.y;
        }

        // uma grade com os itens; devolve o Id do item clicado
        ushort? Grade(Rect area, float cel, int colunas, int linhas, List<ItemNaMochila> itens, InventarioJogador inv)
        {
            float u = _u;
            for (int y = 0; y < linhas; y++)
                for (int x = 0; x < colunas; x++)
                    Caixa(new Rect(area.x + x * cel + 1, area.y + y * cel + 1, cel - 2, cel - 2), new Color(1f, 1f, 1f, 0.05f));
            ushort? clicado = null;
            var mouse = Event.current.mousePosition;
            foreach (var it in itens)
            {
                var t = it.Tamanho;
                var r = new Rect(area.x + it.x * cel + 2, area.y + it.y * cel + 2, t.x * cel - 4, t.y * cel - 4);
                bool sobre = r.Contains(mouse);
                Caixa(r, sobre ? new Color(0.72f, 0.58f, 0.2f, 0.35f) : new Color(0.2f, 0.19f, 0.17f, 0.9f));
                var icone = inv.IconeDe(it.item);
                if (icone != null) GUI.DrawTexture(r, icone, ScaleMode.ScaleToFit);
                int n = Itens.EmPilha(it.item) || Itens.EhMunicao(it.item) ? Itens.Quantos(it.item, it.qtd) : Itens.EhArma(it.item) ? it.qtd : 0;
                if (n > 1 || Itens.EhArma(it.item)) GUI.Label(new Rect(r.x, r.y, r.width - 3 * u, r.height - 1 * u), n.ToString(), _qtd);
                if (sobre)
                {
                    _dica = Itens.Nome(it.item) + (Itens.EhArma(it.item) ? $" · {it.qtd} no pente" : n > 1 ? $" · {n}" : "");
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        clicado = it.id;
                        Event.current.Use();
                    }
                }
            }
            return clicado;
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
