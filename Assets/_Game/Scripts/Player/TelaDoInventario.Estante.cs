using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    // a estante do bunker na tela do inventário (World/EstantesDoBunker; 01/11/2026, a pedido): E numa estante abre a tela
    // com ela na coluna da direita, no lugar dos detalhes: uma faixa por tábua (de cima para baixo), cada uma com a linha
    // de trás e a da frente. Arrastar da mochila para a estante guarda; da estante para a mochila, pega; dentro da
    // estante, move. R gira o que está seguro; o botão direito pega para a mochila (ou guarda, num item da mochila).
    // Longe da estante (ou fechando a tela), ela sai.
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
            _aba = Aba.Mochila;
            if (!Aberta) Abrir(true);
            _estante = estante;
        }

        // longe da estante (ou sem ela), a tela volta a ser só a do inventário
        void ConferirEstante()
        {
            if (_estante < 0) return;
            if (!Aberta || Estantes == null || !EstantesDoBunker.Perto(_estante, transform.position, Estantes.distanciaMaxima + 1f))
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
            float alturaUtil = AreaDaEstante.height - TituloDaEstanteH - RodapeDaEstanteH - f.Tabuas * TabuaH - (f.Tabuas - 1) * VaoEntreTabuas;
            return Mathf.Min(40f, AreaDaEstante.width / f.colunas, alturaUtil / f.Grade.y);
        }

        Vector2 InicioDaEstante(EstantesDoBunker.Forma f, float c) =>
            new Vector2(AreaDaEstante.x + (AreaDaEstante.width - f.colunas * c) / 2f, AreaDaEstante.y + TituloDaEstanteH);

        float AlturaDaFaixa(float c) => EstantesDoBunker.LinhasPorTabua * c + TabuaH + VaoEntreTabuas;

        // o topo da linha y da grade (na medida de 720p)
        float TopoDaLinha(int y, float c) => (y / EstantesDoBunker.LinhasPorTabua) * AlturaDaFaixa(c) + (y % EstantesDoBunker.LinhasPorTabua) * c;

        Rect RetanguloNaEstante(int x, int y, Vector2Int tamanho)
        {
            var f = FormaAberta;
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            return Em(new Rect(o.x + x * c, o.y + TopoDaLinha(y, c), tamanho.x * c, tamanho.y * c));
        }

        // a área das faixas (na tela), um pouco maior: soltar ali é soltar na estante
        Rect AreaDasFaixas()
        {
            var f = FormaAberta;
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            float altura = f.Tabuas * AlturaDaFaixa(c) - VaoEntreTabuas;
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
            float c = CelulaDaEstante(f);
            var o = InicioDaEstante(f, c);
            var canto = (m - PegaPara(c * _u) - Em(new Rect(o.x, o.y, 0f, 0f)).position) / _u;
            var t = InventarioJogador.TamanhoNaGrade(item, girado);
            x = Mathf.Clamp(Mathf.RoundToInt(canto.x / c), 0, Mathf.Max(0, f.colunas - t.x));
            float melhor = float.MaxValue;
            for (int k = 0; k + t.y <= f.Grade.y; k++)
            {
                if (k % EstantesDoBunker.LinhasPorTabua + t.y > EstantesDoBunker.LinhasPorTabua) continue;
                float d = Mathf.Abs(TopoDaLinha(k, c) - canto.y);
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

        void DesenharEstante(Vector2 mouse)
        {
            var f = FormaAberta;
            var e = Estantes;
            if (f == null || e == null) return;
            float u = _u, c = CelulaDaEstante(f), cu = c * u;
            var o = InicioDaEstante(f, c);
            int n = e.Quantos(_estante);
            // o nome dela fica no cabeçalho da coluna (no lugar de DETALHES); aqui, quantos itens e a medida das tábuas
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), $"{f.Tabuas} tábuas de {f.colunas} × {EstantesDoBunker.LinhasPorTabua}", _texto);
            GUI.Label(Em(new Rect(AreaDaEstante.x, AreaDaEstante.y + 2, AreaDaEstante.width, 22)), n == 0 ? "vazia" : n == 1 ? "1 item" : $"{n} itens", _direita);
            bool aco = f.nome.Contains("aço");
            var corDaTabua = aco ? new Color(0.62f, 0.64f, 0.64f, 0.95f) : new Color(0.62f, 0.47f, 0.3f, 0.95f);
            Ret(Encolher(AreaDasFaixas(), 6f * u), Painel);
            for (int t = 0; t < f.Tabuas; t++)
            {
                float topo = t * AlturaDaFaixa(c);
                for (int linha = 0; linha < EstantesDoBunker.LinhasPorTabua; linha++)
                    for (int x = 0; x < f.colunas; x++)
                    {
                        var r = Em(new Rect(o.x + x * c, o.y + topo + linha * c, c, c));
                        Ret(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), CorDaCelula);
                    }
                // a tábua embaixo da faixa (o aço, ou a madeira)
                Ret(Em(new Rect(o.x - 4f, o.y + topo + EstantesDoBunker.LinhasPorTabua * c + 1f, f.colunas * c + 8f, TabuaH - 2f)), corDaTabua);
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
                    Ret(RetanguloNaEstante(ax, ay, InventarioJogador.TamanhoNaGrade(item, _girado)),
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
                    _dicaTexto = Maiuscula(Itens.Nome(it.item)) + (Itens.EmPilha(it.item) ? $" ({Itens.Quantos(it.item, it.qtd)} de {Itens.Pilha(it.item)})" : Itens.ComEstado(it.item) ? $" ({it.qtd}%)" : "");
            }
            float rodape = o.y + f.Tabuas * AlturaDaFaixa(c) - VaoEntreTabuas + 10f;
            GUI.Label(Em(new Rect(AreaDaEstante.x, rodape, AreaDaEstante.width, 20)), "Arraste da mochila para guardar", _pequeno);
            GUI.Label(Em(new Rect(AreaDaEstante.x, rodape + 18, AreaDaEstante.width, 20)), "e daqui para a mochila para pegar", _pequeno);
        }

        // o item da estante que está sendo arrastado, na medida da grade embaixo do mouse
        void ArrastoDaEstante(Vector2 mouse)
        {
            if (!EstanteApertada(out var it)) return;
            float c = (CelulaDoArrasto(mouse, it.item, _girado, out _, out _) || FormaAberta == null ? Celula : CelulaDaEstante(FormaAberta)) * _u;
            var t = InventarioJogador.TamanhoNaGrade(it.item, _girado);
            var r = new Rect(mouse - PegaPara(c), new Vector2(t.x * c, t.y * c));
            DesenharItem(it.ComoDaMochila, Encolher(r, 2 * _u), _girado, 0.85f, false, false, true);
        }
    }
}
