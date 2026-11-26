using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    // a estante do bunker na tela do inventário (World/EstantesDoBunker; 01/11/2026, a pedido): E numa estante abre a tela
    // com ela na coluna da direita, no lugar dos detalhes: uma faixa por tábua (de cima para baixo), cada uma com a linha
    // de trás e a da frente. Arrastar da mochila para a estante guarda; da estante para a mochila, pega; dentro da
    // estante, move. R gira o que está seguro; o botão direito pega para a mochila (ou guarda, num item da mochila); o Shift +
    // clique passa a pilha inteira de um lado para o outro (08/11/2026). A mochila guardada na caçamba ou no armazém
    // (25/11/2026, a revisão) vai arrastada para a grade ou para o lugar da mochila: é vestida, ou trocada pela vestida
    // quando o que ela leva cabe na nova (EstantesDoBunker.PodeVestir).
    // Longe da estante (ou fechando a tela), ela sai; na fuga, o armazém e a caçamba da família não abrem.
    public partial class TelaDoInventario
    {
        // a coluna da estante (a dos detalhes) e as medidas das faixas
        static readonly Rect AreaDaEstante = new Rect(840, 58, 302, 514);
        const float TituloDaEstanteH = 34f, TabuaH = 7f, VaoEntreTabuas = 9f, RodapeDaEstanteH = 54f;

        int _estante = -1;
        // o tamanho da célula (em pixels) com que o item foi pego: o ponto em que ele foi pego (_pega) está nessa medida
        float _celulaDaPega = 1f;

        /// <summary>A estante aberta na tela (-1, nenhuma).</summary>
        public int EstanteAberta => Aberta ? _estante : -1;

        static EstantesDoBunker Estantes => EstantesDoBunker.Instancia != null && EstantesDoBunker.Instancia.IsSpawned ? EstantesDoBunker.Instancia : null;

        /// <summary>Abre a tela com a estante do bunker ao lado da mochila (E nela).</summary>
        public void AbrirEstante(int estante)
        {
            if (rede == null || !rede.IsOwner || MenuOnline.Aberto || EstantesDoBunker.FormaDa(estante) == null) return;
            // (25/11/2026, a revisão) na fuga, o armazém e a caçamba da família ficam para trás (o anfitrião também recusa)
            if (EstantesDoBunker.FechadaNaFuga(estante))
            {
                InventarioJogador.AvisoLocal(EstantesDoBunker.MotivoNaFuga(estante));
                return;
            }
            _aba = Aba.Mochila;
            if (!Aberta) Abrir(true);
            _estante = estante;
        }

        // longe da estante (ou sem ela), a tela volta a ser só a do inventário
        void ConferirEstante()
        {
            if (_estante < 0) return;
            // (e quando a fuga começa com ela aberta: o armazém e a caçamba da família ficam para trás, 25/11/2026)
            if (!Aberta || Estantes == null || !EstantesDoBunker.Perto(_estante, transform.position, Estantes.distanciaMaxima + 1f)
                || EstantesDoBunker.FechadaNaFuga(_estante))
            {
                if (Aberta && _apertou == Origem.Estante) _apertou = Origem.Nada;
                Abrir(false);
            }
        }

        // ------------------------------------------------------------------ as medidas

        EstantesDoBunker.Forma FormaAberta => EstantesDoBunker.FormaDa(_estante);

        // a célula da estante (na medida de 720p) e onde começa a grade
        float CelulaDaEstante(EstantesDoBunker.Forma f)
        {
            if (f.Especial) return 40f;
            float alturaUtil = AreaDaEstante.height - TituloDaEstanteH - RodapeDaEstanteH - f.Tabuas * TabuaH - (f.Tabuas - 1) * VaoEntreTabuas;
            return Mathf.Min(Mathf.Min(40f, AreaDaEstante.width / f.colunas), alturaUtil / f.Grade.y);   // (sem o array do Min de três)
        }

        Vector2 InicioDaEstante(EstantesDoBunker.Forma f, float c) =>
            new Vector2(AreaDaEstante.x + (AreaDaEstante.width - f.colunas * c) / 2f, AreaDaEstante.y + TituloDaEstanteH);

        // (25/11/2026: as linhas de cada tábua vêm da forma; a caçamba é uma grade só, de 8)
        static float AlturaDaFaixa(EstantesDoBunker.Forma f, float c) => f.linhas * c + TabuaH + VaoEntreTabuas;

        // o topo da linha y da grade (na medida de 720p)
        static float TopoDaLinha(EstantesDoBunker.Forma f, int y, float c) => (y / f.linhas) * AlturaDaFaixa(f, c) + (y % f.linhas) * c;

        Rect RetanguloNaEstante(int x, int y, Vector2Int tamanho)
        {
            var f = FormaAberta;
            if (f.Especial) return Em(RetanguloDaVaga(f, x));
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            return Em(new Rect(o.x + x * c, o.y + TopoDaLinha(f, y, c), tamanho.x * c, tamanho.y * c));
        }

        // a área das faixas (na tela), um pouco maior: soltar ali é soltar na estante
        Rect AreaDasFaixas()
        {
            var f = FormaAberta;
            if (f.Especial)
            {
                var r = RetanguloDaVaga(f, 0);
                for (int k = 1; k < f.colunas; k++)
                {
                    var ov = RetanguloDaVaga(f, k);
                    r = Rect.MinMaxRect(Mathf.Min(r.xMin, ov.xMin), Mathf.Min(r.yMin, ov.yMin), Mathf.Max(r.xMax, ov.xMax), Mathf.Max(r.yMax, ov.yMax));
                }
                return Encolher(Em(r), -10f * _u);
            }
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            float altura = f.Tabuas * AlturaDaFaixa(f, c) - VaoEntreTabuas;
            return Encolher(Em(new Rect(o.x, o.y, f.colunas * c, altura)), -10f * _u);
        }

        bool ItemDaEstanteEm(Vector2 m, out ItemNaEstante item, out Rect r)
        {
            item = default;
            r = default;
            var e = Estantes;
            if (_estante < 0 || e == null || FormaAberta == null) return false;
            foreach (var it in e.Guardados)
            {
                if (it.estante != _estante) continue;
                r = RetanguloNaEstante(it.x, it.y, it.Tamanho);
                if (r.Contains(m))
                {
                    item = it;
                    return true;
                }
            }
            return false;
        }

        bool EstanteApertada(out ItemNaEstante item)
        {
            item = default;
            return _apertou == Origem.Estante && Estantes != null && Estantes.Achar(_estante, _idApertado, out item);
        }

        /// <summary>O ponto em que o item foi pego, na medida de uma grade de células de c pixels.</summary>
        Vector2 PegaPara(float c) => _pega * (c / Mathf.Max(1f, _celulaDaPega));

        // a célula da estante onde o item arrastado cairia (o canto de cima, à esquerda): a linha de começo mais perto
        // em que ele fica numa tábua só
        bool CelulaNaEstante(Vector2 m, Item item, bool girado, out int x, out int y)
        {
            x = y = 0;
            var f = FormaAberta;
            if (_estante < 0 || f == null || !AreaDasFaixas().Contains(m)) return false;
            if (f.Especial)
            {
                // a vaga embaixo do mouse (ou a mais perto dele, dentro da área)
                float melhorD = float.MaxValue;
                for (int k = 0; k < f.colunas; k++)
                {
                    var r = Em(RetanguloDaVaga(f, k));
                    if (r.Contains(m))
                    {
                        x = k;
                        return true;
                    }
                    float d = (r.center - m).sqrMagnitude;
                    if (d < melhorD && f.Aceita(k, item))
                    {
                        melhorD = d;
                        x = k;
                    }
                }
                return melhorD < float.MaxValue;
            }
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            var canto = (m - PegaPara(c * _u) - Em(new Rect(o.x, o.y, 0f, 0f)).position) / _u;
            var t = EstantesDoBunker.TamanhoNaEstante(item, girado);
            x = Mathf.Clamp(Mathf.RoundToInt(canto.x / c), 0, Mathf.Max(0, f.colunas - t.x));
            float melhor = float.MaxValue;
            for (int k = 0; k + t.y <= f.Grade.y; k++)
            {
                if (k % f.linhas + t.y > f.linhas) continue;
                float d = Mathf.Abs(TopoDaLinha(f, k, c) - canto.y);
                if (d < melhor)
                {
                    melhor = d;
                    y = k;
                }
            }
            return melhor < float.MaxValue;
        }

        // ------------------------------------------------------------------ soltar

        // um item da mochila solto na estante: guarda (onde cabe)
        bool SoltarNaEstante(Vector2 m, ItemNaMochila it)
        {
            var e = Estantes;
            if (e == null || !CelulaNaEstante(m, it.item, _girado, out int x, out int y)) return false;
            if (e.Cabe(_estante, it.item, _girado, x, y))
            {
                e.Guardar(_estante, inventario, arma, it.id, x, y, _girado);
                if (_escolhido == it.id) _escolhido = 0;
            }
            return true;
        }

        // um item da estante solto: na mochila, pega; na estante, move
        void SoltarDaEstante(Vector2 m)
        {
            var e = Estantes;
            if (e == null || !EstanteApertada(out var it)) return;
            // (25/11/2026, a revisão: a mochila não tem lugar na grade, o tamanho dela lá é 0 × 0, e o arrasto não fazia nada)
            // a mochila da caçamba ou do armazém solta na grade ou no lugar da mochila: veste (ou troca pela vestida, se o
            // que ela leva couber na nova; o anfitrião confere de novo, EstantesDoBunker.PodeVestir)
            if (Itens.EhMochila(it.item) && (CelulaDoArrasto(m, it.item, _girado, out _, out _) || EquipamentoEm(m, out _)))
            {
                if (e.PodeVestir(_estante, it, inventario, out _, out var motivo)) e.Pegar(_estante, it.id, -1, -1, false);
                else if (motivo != null) InventarioJogador.AvisoLocal(motivo);
                return;
            }
            if (CelulaDoArrasto(m, it.item, _girado, out int x, out int y))
            {
                if (inventario.Cabe(it.item, _girado, x, y)) e.Pegar(_estante, it.id, x, y, _girado);
            }
            else if (CelulaNaEstante(m, it.item, _girado, out int ex, out int ey))
            {
                if ((ex != it.x || ey != it.y || _girado != it.girado) && e.Cabe(_estante, it.item, _girado, ex, ey, it.id)) e.Mover(_estante, it.id, ex, ey, _girado);
            }
        }

        // ------------------------------------------------------------------ o desenho

        // ------------------------------------------------------------------ o suporte dos tacos e a mesinha do fumo

        // as vagas (na medida de 720p): no suporte, a prateleirinha da caixa em cima e os 4 tacos em pé embaixo dela; na
        // mesinha, os 4 maços em dois pares, e o cinzeiro do lado
        const float VaoDaVaga = 10f;

        Rect RetanguloDaVaga(EstantesDoBunker.Forma f, int vaga)
        {
            float c = CelulaDaEstante(f), g = VaoDaVaga;
            float topo = AreaDaEstante.y + TituloDaEstanteH + 24f;
            if (f.especial == EspecialDaEstante.Suporte)
            {
                float largura = 4f * c + 3f * g, x0 = AreaDaEstante.x + (AreaDaEstante.width - largura) / 2f;
                if (vaga >= 4) return new Rect(AreaDaEstante.x + (AreaDaEstante.width - 3f * c) / 2f, topo, 3f * c, c);
                return new Rect(x0 + vaga * (c + g), topo + c + 30f, c, 4f * c);
            }
            // a mesinha: duas fileiras de dois, com o cinzeiro à direita
            float w = 1.6f * c, larguraM = 2f * w + g + 2.2f * c, xm = AreaDaEstante.x + (AreaDaEstante.width - larguraM) / 2f;
            return new Rect(xm + (vaga % 2) * (w + g), topo + 20f + (vaga / 2) * (c * 1.4f + g), w, c * 1.4f);
        }

        void DesenharVagas(EstantesDoBunker.Forma f, EstantesDoBunker e, Vector2 mouse)
        {
            float u = _u, c = CelulaDaEstante(f);
            int n = e.Quantos(_estante);
            bool suporte = f.especial == EspecialDaEstante.Suporte;
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), suporte ? "4 tacos e a caixa de bolas" : "até 4 maços", _texto);
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), n == 0 ? (f.artigo == "o" ? "vazio" : "vazia") : n == 1 ? "1 item" : $"{n} itens", _direita);
            Ret(Encolher(AreaDasFaixas(), 6f * u), Painel);
            var madeira = new Color(0.33f, 0.22f, 0.14f, 0.95f);
            for (int k = 0; k < f.colunas; k++)
            {
                var r = Em(RetanguloDaVaga(f, k));
                Ret(r, CorDaCelula);
                if (suporte && k < 4)
                {
                    // o copo embaixo e a fenda da travessa (o desenho do suporte)
                    Ret(new Rect(r.x - 3f * u, r.yMax, r.width + 6f * u, 6f * u), madeira);
                    Ret(new Rect(r.x - 3f * u, r.y + r.height * 0.18f, 4f * u, 6f * u), madeira);
                    Ret(new Rect(r.xMax - u, r.y + r.height * 0.18f, 4f * u, 6f * u), madeira);
                }
                else if (suporte) Ret(new Rect(r.x - 6f * u, r.yMax, r.width + 12f * u, 6f * u), madeira);
            }
            if (suporte)
            {
                var caixa = Em(RetanguloDaVaga(f, 4));
                GUI.Label(new Rect(caixa.x, caixa.yMax + 6f * u, caixa.width, 18f * u), "a caixa de bolas", _pequeno);
                var t0 = Em(RetanguloDaVaga(f, 0));
                var t3 = Em(RetanguloDaVaga(f, 3));
                GUI.Label(new Rect(t0.x, t0.yMax + 8f * u, t3.xMax - t0.x, 18f * u), "os tacos", _pequeno);
            }
            else
            {
                // o cinzeiro, com as bitucas que ele tem
                var v1 = Em(RetanguloDaVaga(f, 1));
                var r = new Rect(v1.xMax + VaoDaVaga * u * 1.5f, v1.y - c * 0.5f * u, 2f * c * u, 2f * c * u);
                Ret(Encolher(r, 4f * u), new Color(0.55f, 0.32f, 0.12f, 0.85f));
                Ret(Encolher(r, 12f * u), new Color(0.35f, 0.34f, 0.32f, 0.9f));
                int bitucas = BunkerDoMundo.Instancia != null ? BunkerDoMundo.Instancia.Bitucas : 0;
                // (26/11/2026, a pedido do Pedro: os textos cortados) a altura medida: "o cinzeiro (12 bitucas)" quebrava e a 2ª
                // linha saía cortada ao meio (embaixo dele não há nada até as legendas de baixo)
                var conteudoDoCinzeiro = new GUIContent(bitucas == 0 ? "o cinzeiro (limpo)" : bitucas == 1 ? "o cinzeiro (1 bituca)" : $"o cinzeiro ({bitucas} bitucas)");
                var caixaDoCinzeiro = new Rect(r.x - 10f * u, r.yMax + 4f * u, r.width + 20f * u, 18f * u);
                caixaDoCinzeiro.height = Mathf.Max(caixaDoCinzeiro.height, _pequeno.CalcHeight(conteudoDoCinzeiro, caixaDoCinzeiro.width));
                GUI.Label(caixaDoCinzeiro, conteudoDoCinzeiro, _pequeno);
            }
            // onde o item arrastado cairia: verde se cabe, vermelho se não
            if (_arrastando && (_apertou == Origem.Mochila || _apertou == Origem.Estante))
            {
                Item item = Item.Nada;
                ushort ignorar = 0;
                if (_apertou == Origem.Mochila && inventario.Achar(_idApertado, out var daMochila)) item = daMochila.item;
                else if (EstanteApertada(out var daEstante))
                {
                    item = daEstante.item;
                    ignorar = daEstante.id;
                }
                if (item != Item.Nada && CelulaNaEstante(mouse, item, _girado, out int ax, out _))
                    Ret(Em(RetanguloDaVaga(f, ax)), e.Cabe(_estante, item, false, ax, 0, ignorar) ? new Color(0.35f, 0.85f, 0.35f, 0.3f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
            }
            foreach (var it in e.Guardados)
            {
                if (it.estante != _estante) continue;
                var r = Em(RetanguloDaVaga(f, it.x));
                bool arrastado = _arrastando && _apertou == Origem.Estante && _idApertado == it.id;
                // os tacos em pé (o ícone girado) nas vagas compridas
                DesenharItem(it.ComoDaMochila, Encolher(r, 2 * u), suporte && it.x < 4, arrastado ? 0.3f : 1f, false, r.Contains(mouse) && !_arrastando, true);
                if (r.Contains(mouse) && !_arrastando)
                    _dicaTexto = Maiuscula(Itens.Nome(it.item)) + (Itens.EmPilha(it.item) ? $" ({Itens.Quantos(it.item, it.qtd)} de {Itens.Pilha(it.item)})" : "");
            }
            var area = AreaDasFaixas();
            GUI.Label(new Rect(area.x, area.yMax + 30f * u, area.width, 20f * u), "Arraste da mochila para guardar", _pequeno);
            GUI.Label(new Rect(area.x, area.yMax + 48f * u, area.width, 20f * u), "e daqui para a mochila para pegar", _pequeno);
        }

        void DesenharEstante(Vector2 mouse)
        {
            var f = FormaAberta;
            var e = Estantes;
            if (f == null || e == null) return;
            if (f.Especial)
            {
                DesenharVagas(f, e, mouse);
                return;
            }
            float u = _u, c = CelulaDaEstante(f), cu = c * u;
            var o = InicioDaEstante(f, c);
            int n = e.Quantos(_estante);
            // o nome dela fica no cabeçalho da coluna (no lugar de DETALHES); aqui, quantos itens e a medida das tábuas
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), f.Tabuas == 1 ? $"{f.colunas} × {f.linhas}" : $"{f.Tabuas} tábuas de {f.colunas} × {f.linhas}", _texto);
            // (25/11/2026, a revisão: "o armazém da garagem" aparecia "vazia")
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), n == 0 ? (f.artigo == "o" ? "vazio" : "vazia") : n == 1 ? "1 item" : $"{n} itens", _direita);
            bool aco = f.nome.Contains("aço") || f.nome.Contains("armazém");
            var corDaTabua = aco ? new Color(0.62f, 0.64f, 0.64f, 0.95f) : new Color(0.62f, 0.47f, 0.3f, 0.95f);
            Ret(Encolher(AreaDasFaixas(), 6f * u), Painel);
            for (int t = 0; t < f.Tabuas; t++)
            {
                float topo = t * AlturaDaFaixa(f, c);
                for (int linha = 0; linha < f.linhas; linha++)
                    for (int x = 0; x < f.colunas; x++)
                    {
                        var r = Em(new Rect(o.x + x * c, o.y + topo + linha * c, c, c));
                        Ret(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), CorDaCelula);
                    }
                // a tábua embaixo da faixa (o aço, ou a madeira)
                Ret(Em(new Rect(o.x - 4f, o.y + topo + f.linhas * c + 1f, f.colunas * c + 8f, TabuaH - 2f)), corDaTabua);
            }
            // onde o item arrastado cairia: verde se cabe, vermelho se não
            if (_arrastando && (_apertou == Origem.Mochila || _apertou == Origem.Estante))
            {
                Item item = Item.Nada;
                ushort ignorar = 0;
                if (_apertou == Origem.Mochila && inventario.Achar(_idApertado, out var daMochila)) item = daMochila.item;
                else if (EstanteApertada(out var daEstante))
                {
                    item = daEstante.item;
                    ignorar = daEstante.id;
                }
                if (item != Item.Nada && CelulaNaEstante(mouse, item, _girado, out int ax, out int ay))
                {
                    bool cabe = e.Cabe(_estante, item, _girado, ax, ay, ignorar);
                    Ret(RetanguloNaEstante(ax, ay, EstantesDoBunker.TamanhoNaEstante(item, _girado)),
                        cabe ? new Color(0.35f, 0.85f, 0.35f, 0.3f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
                }
            }
            foreach (var it in e.Guardados)
            {
                if (it.estante != _estante) continue;
                var r = RetanguloNaEstante(it.x, it.y, it.Tamanho);
                bool arrastado = _arrastando && _apertou == Origem.Estante && _idApertado == it.id;
                DesenharItem(it.ComoDaMochila, Encolher(r, 2 * u), it.girado, arrastado ? 0.3f : 1f, false, r.Contains(mouse) && !_arrastando, true);
                if (r.Contains(mouse) && !_arrastando)
                    _dicaTexto = Maiuscula(Itens.Nome(it.item)) + (Itens.EhMochila(it.item) ? " (vazia: arraste para vestir)"   // (25/11/2026, a revisão)
                        : Itens.EmPilha(it.item) ? $" ({Itens.Quantos(it.item, it.qtd)} de {Itens.Pilha(it.item)})" : Itens.ComEstado(it.item) ? $" ({it.qtd}%)" : "");
            }
            float rodape = o.y + f.Tabuas * AlturaDaFaixa(f, c) - VaoEntreTabuas + 10f;
            // (25/11/2026: o texto longo quebrava em duas linhas por cima da de baixo)
            GUI.Label(Em(new Rect(AreaDaEstante.x, rodape, AreaDaEstante.width, 20)), "Arraste da mochila para guardar (Shift: a pilha toda)", _pequeno);
            GUI.Label(Em(new Rect(AreaDaEstante.x, rodape + 18, AreaDaEstante.width, 20)), "e daqui para a mochila para pegar", _pequeno);
            // (25/11/2026) a caçamba perto da garagem da casa da família: o botão que passa tudo para o armazém
            var botao = BotaoDeDescarregar();
            if (botao.width > 0f)
            {
                bool sobre = botao.Contains(mouse);
                Ret(botao, sobre ? new Color(0.36f, 0.5f, 0.3f, 0.95f) : new Color(0.26f, 0.36f, 0.22f, 0.92f));
                Moldura(botao, Mathf.Max(1f, Mathf.Round(u)), BordaClara);
                GUI.Label(botao, "Descarregar no armazém da garagem", new GUIStyle(_pequeno) { alignment = TextAnchor.MiddleCenter });
            }
        }

        /// <summary>
        /// O botão "Descarregar no armazém da garagem" (na tela, em pixels), embaixo da grade da caçamba, quando a caminhonete
        /// está na entrada da garagem da casa da família e há o que descarregar; senão, um retângulo vazio.
        /// </summary>
        Rect BotaoDeDescarregar()
        {
            var f = FormaAberta;
            var e = Estantes;
            if (_estante < 0 || f == null || e == null || !EstantesDoBunker.EhDaCacamba(_estante) || e.Quantos(_estante) == 0
                || !EstantesDoBunker.CacambaPertoDoArmazemDaFamilia(_estante) || EstantesDoBunker.FechadaNaFuga(_estante)) return default;
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            float rodape = o.y + f.Tabuas * AlturaDaFaixa(f, c) - VaoEntreTabuas + 10f;
            return Em(new Rect(AreaDaEstante.x + 26f, rodape + 44f, AreaDaEstante.width - 52f, 26f));
        }

        /// <summary>Para os testes: o clique no botão de descarregar a caçamba.</summary>
        public void DescarregarACacamba()
        {
            if (_estante >= 0 && Estantes != null) Estantes.PedirDescarregar(_estante);
        }

        // o item da estante que está sendo arrastado, na medida da grade embaixo do mouse
        void ArrastoDaEstante(Vector2 mouse)
        {
            if (!EstanteApertada(out var it)) return;
            bool naGrade = CelulaDoArrasto(mouse, it.item, _girado, out _, out _);
            float c = (naGrade || FormaAberta == null ? Celula : CelulaDaEstante(FormaAberta)) * _u;
            // (25/11/2026, a revisão) o tamanho dele na estante: o da mochila guardada é o dela lá (o da grade é 0 × 0, e o
            // desenho sumia); nos outros itens, os dois são o mesmo
            var t = EstantesDoBunker.TamanhoNaEstante(it.item, _girado);
            var r = new Rect(mouse - PegaPara(c), new Vector2(t.x * c, t.y * c));
            DesenharItem(it.ComoDaMochila, Encolher(r, 2 * _u), _girado, 0.85f, false, false, true);
            // a mochila em cima da grade ou do lugar da mochila: o lugar dela verde (veste, ou troca pela vestida) ou
            // vermelho, e o que acontece ao soltar
            var e = Estantes;
            if (!Itens.EhMochila(it.item) || e == null || !(naGrade || EquipamentoEm(mouse, out _))) return;
            bool pode = e.PodeVestir(_estante, it, inventario, out bool troca, out string motivo);
            Ret(Em(LugarDaMochila), pode ? new Color(0.35f, 0.85f, 0.35f, 0.35f) : new Color(0.9f, 0.25f, 0.2f, 0.4f));
            // (25/11/2026, a revisão: "trocar pela sua X" dizia o contrário, dar esta e ficar com a sua; o mesmo texto da troca
            // no chão, InventarioJogador.DicaDePegar)
            string texto = !pode ? motivo ?? "Não dá para vestir agora."
                : troca ? $"soltar: trocar a sua {Itens.Nome(inventario.MochilaVestida.Value)} pela {Itens.Nome(it.item)}" : "soltar: vestir";
            float w = _dica.CalcSize(new GUIContent(texto)).x + 8f * _u;
            GUI.Label(new Rect(Mathf.Min(mouse.x + 14 * _u, Screen.width - w), mouse.y - 20 * _u, w, 20 * _u), texto, _dica);
        }
    }
}
