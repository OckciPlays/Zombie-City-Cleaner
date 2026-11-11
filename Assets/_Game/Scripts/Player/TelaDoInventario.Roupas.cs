using UnityEngine;
using ZombieCityCleaner.Loot;
using Icones = ZombieCityCleaner.Roupas.IconesDasRoupas;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os lugares das roupas na tela do inventário (a parte 4 do Plano_Personagem.md da pasta de arte, 31/10/2026: "na
    /// imagem do jogador no inventário", 5 de cada lado): à esquerda o chapéu, os óculos, a máscara, a camiseta e as luvas;
    /// à direita a jaqueta ou o colete, o cinto ou o coldre, a calça, o sapato e, embaixo, a mochila. Cada um mostra o
    /// ícone da roupa vestida, com as cores dela.
    /// - Vestir: arrastar a roupa da grade para o lugar dela (ou para o retrato), ou "Vestir" no menu do botão direito. A
    ///   que estava no lugar vai para a célula da vestida.
    /// - Tirar: arrastar do lugar para a grade (para a célula solta, se livre) ou "Tirar" no menu; para fora da janela,
    ///   ou G com ela escolhida, larga no chão.
    /// - Clique escolhe: os detalhes mostram a roupa.
    /// </summary>
    public partial class TelaDoInventario
    {
        // as medidas, a 720p: as duas colunas dentro da área do retrato e as 5 linhas (com o nome embaixo de cada lugar; o passo
        // era 74 e o lado 54 até o grupo PROTEÇÃO, 11/11/2026)
        const float ColunaDaEsquerda = 24f, ColunaDaDireita = 258f, LinhaDasRoupas = 64f, PassoDasRoupas = 66f, LadoDaRoupa = 48f;
        static readonly Color FundoDeRoupa = new Color(0.5f, 0.49f, 0.46f, 0.95f);
        static readonly Color CorQueCabe = new Color(0.35f, 0.85f, 0.35f, 0.3f), CorQueNaoCabe = new Color(0.9f, 0.25f, 0.2f, 0.35f);

        // a roupa escolhida (o lugar; -1, nenhuma) e a apertada
        int _roupaEscolhida = -1;
        LugarDaRoupa _lugarApertado;

        /// <summary>O lugar da roupa na tela (a 720p): os 5 primeiros à esquerda, os outros à direita.</summary>
        static Rect LugarNaTela(LugarDaRoupa l)
        {
            int k = (int)l;
            bool esquerda = k < 5;
            int linha = esquerda ? k : k - 5;
            return new Rect(esquerda ? ColunaDaEsquerda : ColunaDaDireita, LinhaDasRoupas + linha * PassoDasRoupas, LadoDaRoupa, LadoDaRoupa);
        }

        bool RoupaEm(Vector2 m, out LugarDaRoupa l)
        {
            for (int k = 0; k < Itens.QuantosLugaresDeRoupa; k++)
                if (Em(LugarNaTela((LugarDaRoupa)k)).Contains(m))
                {
                    l = (LugarDaRoupa)k;
                    return true;
                }
            l = default;
            return false;
        }

        bool RoupaEscolhida(out LugarDaRoupa l)
        {
            l = (LugarDaRoupa)Mathf.Max(0, _roupaEscolhida);
            return _roupaEscolhida >= 0 && inventario.Roupas.Value.Peca(l) != Item.Nada;
        }

        void DesenharRoupas(Vector2 mouse)
        {
            float u = _u;
            var roupas = inventario.Roupas.Value;
            // arrastando uma roupa da grade: o lugar dela acende
            var lugarDoArrasto = (LugarDaRoupa)255;
            if (_arrastando && _apertou == Origem.Mochila && inventario.Achar(_idApertado, out var arrastada) && Itens.Roupa(arrastada.item) is DadosDaRoupa d)
                lugarDoArrasto = d.lugar;
            for (int k = 0; k < Itens.QuantosLugaresDeRoupa; k++)
            {
                var l = (LugarDaRoupa)k;
                var a = Em(LugarNaTela(l));
                var item = roupas.Peca(l);
                bool vestida = item != Item.Nada;
                bool escolhida = vestida && _roupaEscolhida == k;
                bool arrastadaDaqui = _arrastando && _apertou == Origem.Roupa && _lugarApertado == l;
                Ret(a, vestida ? FundoDeRoupa : new Color(0f, 0f, 0f, 0.28f));
                if (l == lugarDoArrasto) Ret(a, new Color(0.35f, 0.85f, 0.35f, a.Contains(mouse) ? 0.5f : 0.25f));
                else if (a.Contains(mouse) && !_arrastando && vestida) Ret(a, new Color(1f, 1f, 1f, 0.07f));
                Moldura(a, Mathf.Max(1f, Mathf.Round((escolhida ? 2 : 1) * u)), escolhida ? Destaque : new Color(1f, 1f, 1f, vestida ? 0.16f : 0.08f));
                var icone = vestida ? Icones.Icone(item, roupas.Cores(l)) : null;
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, arrastadaDaqui ? 0.3f : 1f);
                    GUI.DrawTexture(Encolher(a, 3 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                GUI.Label(new Rect(a.x - 10 * u, a.yMax + 1 * u, a.width + 20 * u, 16 * u), Itens.NomeDoLugar(l), _centro);
                if (a.Contains(mouse) && !_arrastando) _dicaTexto = vestida ? Maiuscula(Itens.Nome(item)) : Itens.DicaDoLugar(l) + ": vazio";
            }
        }

        // ------------------------------------------------------------------ o mouse

        /// <summary>O menu do botão direito de uma roupa vestida: tirar para a mochila ou largar no chão.</summary>
        void MenuDaRoupa(LugarDaRoupa l)
        {
            _roupaEscolhida = (int)l;
            _protecaoEscolhida = -1;
            _escolhido = 0;
            _equipEscolhido = Item.Nada;
            if (inventario.CabeUmaRoupa) Opcao("Tirar (para a mochila)", () => inventario.TirarRoupa(l));
            else Opcao($"Tirar (sem espaço {(inventario.MochilaVestida.Value != Item.Nada ? "na mochila" : "nos bolsos")})", null);
            Opcao("Tirar e largar no chão", () =>
            {
                inventario.LargarRoupa(l);
                _roupaEscolhida = -1;
            });
        }

        /// <summary>No menu de uma roupa da grade: vestir (trocando pela que está no lugar dela).</summary>
        void OpcaoDeVestir(ItemNaMochila it)
        {
            var dados = Itens.Roupa(it.item);
            var vestida = inventario.Roupas.Value.Peca(dados.lugar);
            ushort id = it.id;
            Opcao(vestida == Item.Nada ? "Vestir" : $"Vestir (no lugar d{Itens.NomeComArtigo(vestida)})", () =>
            {
                inventario.Vestir(id);
                if (_escolhido == id) _escolhido = 0;
            });
        }

        /// <summary>A roupa da grade solta em cima do corpo: no lugar dela ou no retrato, veste; noutro lugar, nada.</summary>
        bool SoltarRoupaNoCorpo(Vector2 m, ItemNaMochila it)
        {
            var dados = Itens.Roupa(it.item);
            if (RoupaEm(m, out var l))
            {
                if (l == dados.lugar) Vestir(it.id);
                return true;
            }
            if (!Em(AreaDoRetrato).Contains(m) || EquipamentoEm(m, out _) || AtalhoEm(m, out _)) return false;
            Vestir(it.id);
            return true;
        }

        void Vestir(ushort id)
        {
            inventario.Vestir(id);
            if (_escolhido == id) _escolhido = 0;
        }

        /// <summary>A roupa vestida solta: na grade, guarda (na célula, se livre); fora da janela, larga no chão.</summary>
        void SoltarRoupaVestida(Vector2 m)
        {
            var item = inventario.Roupas.Value.Peca(_lugarApertado);
            if (item == Item.Nada) return;
            if (CelulaDoArrasto(m, item, false, out int x, out int y))
            {
                if (inventario.Cabe(item, false, x, y)) inventario.TirarRoupa(_lugarApertado, x, y);
            }
            else if (!Janela.Contains(m))
            {
                inventario.LargarRoupa(_lugarApertado);
                if (_roupaEscolhida == (int)_lugarApertado) _roupaEscolhida = -1;
            }
        }

        // ------------------------------------------------------------------ o desenho do arrasto e os detalhes

        // a célula onde a roupa vestida arrastada cairia: verde se está livre
        void CelulaDaRoupaArrastada(Vector2 mouse)
        {
            var item = inventario.Roupas.Value.Peca(_lugarApertado);
            if (item != Item.Nada && CelulaDoArrasto(mouse, item, false, out int x, out int y))
                Ret(RetanguloNaGrade(x, y, Vector2Int.one), inventario.Cabe(item, false, x, y) ? CorQueCabe : CorQueNaoCabe);
        }

        void ArrastoDaRoupa(Vector2 mouse)
        {
            float u = _u;
            var roupas = inventario.Roupas.Value;
            var icone = Icones.Icone(roupas.Peca(_lugarApertado), roupas.Cores(_lugarApertado));
            if (icone != null) GUI.DrawTexture(new Rect(mouse - _pega, new Vector2(LadoDaRoupa * u, LadoDaRoupa * u)), icone, ScaleMode.ScaleToFit);
            string texto = !Janela.Contains(mouse) ? "soltar: largar no chão"
                : _aba == Aba.Mochila && Em(AreaDaGrade).Contains(mouse) ? "soltar: guardar na mochila" : "solte na mochila, ou fora da janela para largar";
            GUI.Label(new Rect(mouse.x + 14 * u, mouse.y - 20 * u, 260 * u, 20 * u), texto, _dica);
        }

        // a roupa vestida escolhida: o nome, a foto (com as cores dela), o lugar e como tirar
        void DetalhesDaRoupaVestida(LugarDaRoupa l, Rect nome, Rect foto, Rect det)
        {
            float u = _u;
            var roupas = inventario.Roupas.Value;
            var item = roupas.Peca(l);
            GUI.Label(new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height), Maiuscula(Itens.Nome(item)), _nome);
            var imagem = Icones.Icone(item, roupas.Cores(l), true);
            if (imagem != null) GUI.DrawTexture(Encolher(foto, 10 * u), imagem, ScaleMode.ScaleToFit);
            float x = det.x + 12 * u, w = det.width - 24 * u, y = det.y + 10 * u, linha = 22 * u;
            GUI.Label(new Rect(x, y, w, 20 * u), Itens.Resumo(item), _texto);
            y += 30 * u;
            GUI.Label(new Rect(x, y, w, linha), "Vestida", _texto);
            GUI.Label(new Rect(x, y, w, linha), Itens.DicaDoLugar(l), new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold });
            Ret(new Rect(x, y + linha - 3 * u, w, 1), new Color(1f, 1f, 1f, 0.07f));
            y += linha + 6 * u;
            GUI.Label(new Rect(x, y, w, 60 * u), "Para tirar: arraste para a mochila. Para largar no chão: G, ou arraste para fora da janela.", _pequeno);
        }
    }
}
