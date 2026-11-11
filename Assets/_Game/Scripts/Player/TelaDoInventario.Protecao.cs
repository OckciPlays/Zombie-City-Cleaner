using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O grupo PROTEÇÃO da tela do inventário (11/11/2026, a proteção de sucata, a etapa 3): os 4 lugares (o capacete, o
    /// colete, as braçadeiras e as caneleiras) numa fileira embaixo do retrato, acima dos atalhos (o retrato e as roupas em
    /// volta dele ficaram um pouco menores para caber). Cada lugar mostra o ícone da peça vestida, a barra do desgaste e,
    /// quebrada, "quebrado". Como as roupas:
    /// - Vestir: arrastar a peça da grade para o lugar dela (ou para o retrato), ou "Vestir" no menu do botão direito. A
    ///   que estava no lugar volta para a mochila; o capacete manda o chapéu para a mochila.
    /// - Tirar: arrastar do lugar para a grade, ou "Tirar" no menu; para fora da janela, ou G com ela escolhida, larga no
    ///   chão. "Consertar" (no menu) gasta um kit de reparo.
    /// - Clique escolhe: os detalhes mostram a peça, o que ela cobre e o estado.
    /// </summary>
    public partial class TelaDoInventario
    {
        // as medidas, a 720p: o rótulo, a fileira dos 4 lugares (com o nome embaixo) entre o retrato e os atalhos
        const float RotuloDaProtecaoY = 400f, LinhaDaProtecao = 420f, LadoDaProtecao = 44f;
        static readonly Color FundoDeProtecao = new Color(0.47f, 0.49f, 0.5f, 0.95f);

        // a peça escolhida (o lugar; -1, nenhuma) e a apertada
        int _protecaoEscolhida = -1;
        LugarDaProtecao _protecaoApertada;

        /// <summary>O lugar da peça na tela (a 720p): os 4 lado a lado, na largura do retrato.</summary>
        static Rect LugarDaProtecaoNaTela(LugarDaProtecao l)
        {
            float passo = AreaDoRetrato.width / Protecao.Quantos;
            return new Rect(AreaDoRetrato.x + (int)l * passo + (passo - LadoDaProtecao) * 0.5f, LinhaDaProtecao, LadoDaProtecao, LadoDaProtecao);
        }

        /// <summary>Abre a tela na aba Mochila, com a peça vestida do lugar escolhida (o teste pelo MCP e o F1; -1, nenhuma).</summary>
        public void AbrirNaMochila(int protecao = -1)
        {
            if (rede == null || !rede.IsOwner || MenuOnline.Aberto) return;
            _aba = Aba.Mochila;
            Abrir(true);
            if (protecao >= 0) Escolher(0);
            _protecaoEscolhida = protecao;
        }

        bool ProtecaoEm(Vector2 m, out LugarDaProtecao l)
        {
            for (int k = 0; k < Protecao.Quantos; k++)
                if (Em(LugarDaProtecaoNaTela((LugarDaProtecao)k)).Contains(m))
                {
                    l = (LugarDaProtecao)k;
                    return true;
                }
            l = default;
            return false;
        }

        bool ProtecaoEscolhida(out LugarDaProtecao l)
        {
            l = (LugarDaProtecao)Mathf.Clamp(_protecaoEscolhida, 0, Protecao.Quantos - 1);
            return _protecaoEscolhida >= 0 && inventario.Protecao.Value.Peca(l) != Item.Nada;
        }

        void DesenharProtecao(Vector2 mouse)
        {
            float u = _u;
            var p = inventario.Protecao.Value;
            Rotulo(Em(new Rect(AreaDoRetrato.x + 4, RotuloDaProtecaoY, 300, 20)), "PROTEÇÃO", _pequeno);
            // arrastando uma peça da grade: o lugar dela acende
            int doArrasto = -1;
            if (_arrastando && _apertou == Origem.Mochila && inventario.Achar(_idApertado, out var arrastada) && Protecao.EhPeca(arrastada.item))
                doArrasto = (int)Protecao.LugarDe(arrastada.item);
            for (int k = 0; k < Protecao.Quantos; k++)
            {
                var l = (LugarDaProtecao)k;
                var a = Em(LugarDaProtecaoNaTela(l));
                var item = p.Peca(l);
                bool vestida = item != Item.Nada;
                int desgaste = p.Desgaste(l);
                bool escolhida = vestida && _protecaoEscolhida == k;
                bool arrastadaDaqui = _arrastando && _apertou == Origem.Protecao && _protecaoApertada == l;
                Ret(a, vestida ? FundoDeProtecao : new Color(0f, 0f, 0f, 0.28f));
                if (k == doArrasto) Ret(a, new Color(0.35f, 0.85f, 0.35f, a.Contains(mouse) ? 0.5f : 0.25f));
                else if (a.Contains(mouse) && !_arrastando && vestida) Ret(a, new Color(1f, 1f, 1f, 0.07f));
                Moldura(a, Mathf.Max(1f, Mathf.Round((escolhida ? 2 : 1) * u)), escolhida ? Destaque : new Color(1f, 1f, 1f, vestida ? 0.16f : 0.08f));
                var icone = vestida ? inventario.IconeDe(item) : null;
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, arrastadaDaqui ? 0.3f : desgaste >= 100 ? 0.45f : 1f);
                    GUI.DrawTexture(Encolher(a, 3 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                if (vestida && desgaste > 0)
                {
                    // o estado: a barra fina embaixo (verde, amarela, vermelha) ou, quebrada, "quebrado"
                    float f = (100 - desgaste) / 100f;
                    if (desgaste < 100)
                    {
                        Ret(new Rect(a.x + 3 * u, a.yMax - 6 * u, a.width - 6 * u, 3 * u), new Color(0f, 0f, 0f, 0.5f));
                        Ret(new Rect(a.x + 3 * u, a.yMax - 6 * u, (a.width - 6 * u) * f, 3 * u),
                            f > 0.5f ? new Color(0.45f, 0.85f, 0.4f) : f > 0.3f ? new Color(0.95f, 0.8f, 0.3f) : new Color(1f, 0.38f, 0.3f));
                    }
                    else
                    {
                        var quebrado = new GUIStyle(_centro) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                        PorCor(quebrado, new Color(1f, 0.38f, 0.3f));
                        GUI.Label(a, "quebrado", quebrado);
                    }
                }
                GUI.Label(new Rect(a.x - 14 * u, a.yMax + 1 * u, a.width + 28 * u, 16 * u), Protecao.NomeDoLugar(l), _centro);
                if (a.Contains(mouse) && !_arrastando)
                    _dicaTexto = vestida ? $"{Maiuscula(Itens.Nome(item))}: {Protecao.Estado(item, desgaste)}"
                        : $"{Protecao.NomeDoLugar(l)}: vazio (protege {Protecao.QueCobre(l)})";
            }
        }

        // ------------------------------------------------------------------ o mouse

        /// <summary>O menu do botão direito de uma peça vestida: tirar para a mochila, largar no chão ou consertar.</summary>
        void MenuDaProtecao(LugarDaProtecao l)
        {
            _protecaoEscolhida = (int)l;
            _roupaEscolhida = -1;
            _escolhido = 0;
            _equipEscolhido = Item.Nada;
            var p = inventario.Protecao.Value;
            var item = p.Peca(l);
            if (inventario.TemEspaco(item)) Opcao("Tirar (para a mochila)", () => inventario.TirarProtecao(l));
            else Opcao($"Tirar (sem espaço {(inventario.MochilaVestida.Value != Item.Nada ? "na mochila" : "nos bolsos")})", null);
            Opcao("Tirar e largar no chão", () =>
            {
                inventario.LargarProtecao(l);
                _protecaoEscolhida = -1;
            });
            if (p.Desgaste(l) > 0)
            {
                bool pode = inventario.PodeConsertarProtecao(l, out var motivo);
                Opcao(pode ? $"Consertar ({Protecao.Estado(item, p.Desgaste(l))} → 100%, gasta 1 kit de reparo)" : $"Consertar ({motivo})",
                      pode ? () => inventario.ConsertarProtecao(l) : (System.Action)null);
            }
        }

        /// <summary>No menu de uma peça da grade: vestir (trocando pela que está no lugar dela).</summary>
        void OpcaoDeVestirProtecao(ItemNaMochila it)
        {
            var l = Protecao.LugarDe(it.item);
            var vestida = inventario.Protecao.Value.Peca(l);
            ushort id = it.id;
            string chapeu = l == LugarDaProtecao.Capacete && inventario.Roupas.Value.Peca(LugarDaRoupa.Chapeu) != Item.Nada ? " (o chapéu vai para a mochila)" : "";
            Opcao((vestida == Item.Nada ? "Vestir" : $"Vestir (no lugar d{Itens.NomeComArtigo(vestida)})") + chapeu, () =>
            {
                inventario.VestirProtecao(id);
                if (_escolhido == id) _escolhido = 0;
            });
        }

        /// <summary>A peça da grade solta em cima do corpo: no lugar dela ou no retrato, veste; noutro lugar, nada.</summary>
        bool SoltarProtecaoNoCorpo(Vector2 m, ItemNaMochila it)
        {
            if (ProtecaoEm(m, out var l))
            {
                if (l == Protecao.LugarDe(it.item)) VestirProtecao(it.id);
                return true;
            }
            if (!Em(AreaDoRetrato).Contains(m) || EquipamentoEm(m, out _) || RoupaEm(m, out _)) return false;
            VestirProtecao(it.id);
            return true;
        }

        void VestirProtecao(ushort id)
        {
            inventario.VestirProtecao(id);
            if (_escolhido == id) _escolhido = 0;
        }

        /// <summary>A peça vestida solta: na grade, guarda (na célula, se cabe); fora da janela, larga no chão.</summary>
        void SoltarProtecaoVestida(Vector2 m)
        {
            var item = inventario.Protecao.Value.Peca(_protecaoApertada);
            if (item == Item.Nada) return;
            if (CelulaDoArrasto(m, item, false, out int x, out int y))
            {
                if (inventario.Cabe(item, false, x, y)) inventario.TirarProtecao(_protecaoApertada, x, y);
            }
            else if (!Janela.Contains(m))
            {
                inventario.LargarProtecao(_protecaoApertada);
                if (_protecaoEscolhida == (int)_protecaoApertada) _protecaoEscolhida = -1;
            }
        }

        // ------------------------------------------------------------------ o desenho do arrasto e os detalhes

        // as células onde a peça vestida arrastada cairia: verde se cabe
        void CelulaDaProtecaoArrastada(Vector2 mouse)
        {
            var item = inventario.Protecao.Value.Peca(_protecaoApertada);
            if (item != Item.Nada && CelulaDoArrasto(mouse, item, false, out int x, out int y))
                Ret(RetanguloNaGrade(x, y, Itens.Tamanho(item)), inventario.Cabe(item, false, x, y) ? CorQueCabe : CorQueNaoCabe);
        }

        void ArrastoDaProtecao(Vector2 mouse)
        {
            float u = _u;
            var icone = inventario.IconeDe(inventario.Protecao.Value.Peca(_protecaoApertada));
            if (icone != null) GUI.DrawTexture(new Rect(mouse - _pega, new Vector2(LadoDaProtecao * u, LadoDaProtecao * u)), icone, ScaleMode.ScaleToFit);
            string texto = !Janela.Contains(mouse) ? "soltar: largar no chão"
                : _aba == Aba.Mochila && Em(AreaDaGrade).Contains(mouse) ? "soltar: guardar na mochila" : "solte na mochila, ou fora da janela para largar";
            GUI.Label(new Rect(mouse.x + 14 * u, mouse.y - 20 * u, 260 * u, 20 * u), texto, _dica);
        }

        // a peça vestida escolhida: o nome, a foto, o que ela cobre, o estado e como tirar
        void DetalhesDaProtecaoVestida(LugarDaProtecao l, Rect nome, Rect foto, Rect det)
        {
            float u = _u;
            var p = inventario.Protecao.Value;
            var item = p.Peca(l);
            GUI.Label(new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height), Maiuscula(Itens.Nome(item)), _nome);
            var imagem = inventario.FotoDe(item);
            if (imagem != null) GUI.DrawTexture(Encolher(foto, 10 * u), imagem, ScaleMode.ScaleToFit);
            float x = det.x + 12 * u, w = det.width - 24 * u, y = det.y + 10 * u, linha = 22 * u;
            var resumo = new GUIContent(Itens.Resumo(item));
            float alturaDoResumo = Mathf.Max(20 * u, _texto.CalcHeight(resumo, w));
            GUI.Label(new Rect(x, y, w, alturaDoResumo), resumo, _texto);
            y += alturaDoResumo + 10 * u;
            var direita = new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            void Linha(string rotulo, string valor)
            {
                GUI.Label(new Rect(x, y, w, linha), rotulo, _texto);
                GUI.Label(new Rect(x, y, w, linha), valor, direita);
                Ret(new Rect(x, y + linha - 3 * u, w, 1), new Color(1f, 1f, 1f, 0.07f));
                y += linha;
            }
            Linha("Vestida", Protecao.NomeDoLugar(l));
            Linha("Cobre", $"{Protecao.QueCobre(l)}: −{Protecao.Corte(l) * 100f:0}% do golpe");
            Linha("Estado", Protecao.Estado(item, p.Desgaste(l)));
            y += 6 * u;
            GUI.Label(new Rect(x, y, w, 60 * u), "Para tirar: arraste para a mochila. Para largar no chão: G, ou arraste para fora da janela. Gasta: conserte pelo botão direito.", _pequeno);
        }
    }
}
