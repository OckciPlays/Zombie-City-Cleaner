using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O painel MELHORIAS da aba Criar (10/11/2026, a etapa 2 do Armeiro de sucata): em cima da lista, a barra RECEITAS |
    /// MELHORIAS. Nas melhorias: as armas de sucata da mochila (a da mão primeiro), e, da escolhida, os encaixes (o cano,
    /// a mira, o carregador e o reforço), com o que está posto (o clique tira e devolve para a mochila) e o que dá para
    /// pôr da mochila (o clique põe e gasta a peça; a que estava no lugar volta). Só perto de uma bancada (a mesa de
    /// oficina vale). Os detalhes, à direita, mostram a arma escolhida, com a foto das peças postas.
    /// </summary>
    public partial class TelaDoInventario
    {
        /// <summary>A barra RECEITAS | MELHORIAS, em cima da lista da aba Criar.</summary>
        const float BarraDaCriar = 32f;
        bool _melhorias;
        ushort _armaDasMelhorias;
        // o que se clica no painel (os retângulos do último desenho, já na tela)
        readonly List<(Rect r, System.Action acao)> _cliquesDasMelhorias = new List<(Rect, System.Action)>();

        Rect BotaoDasReceitas => Em(new Rect(AreaDasReceitas.x, AreaDasReceitas.y, 150, BarraDaCriar - 6));
        Rect BotaoDasMelhorias => Em(new Rect(AreaDasReceitas.x + 156, AreaDasReceitas.y, 210, BarraDaCriar - 6));

        /// <summary>Abre a aba Criar direto nas melhorias (o F1 e o teste).</summary>
        public void AbrirNasMelhorias(ushort arma = 0)
        {
            AbrirNaAbaCriar();
            _melhorias = true;
            _receita = -1;
            if (arma != 0) _armaDasMelhorias = arma;
        }

        /// <summary>Fecha a tela (o teste pelo MCP: o TAB não chega com o Editor sem o foco).</summary>
        public void FecharTela() => Abrir(false);

        // a barra de cima (as duas abas da aba Criar)
        void BarraDasReceitas(Vector2 mouse)
        {
            float u = _u;
            foreach (var (r, nome, ativa) in new[] { (BotaoDasReceitas, "RECEITAS", !_melhorias), (BotaoDasMelhorias, "MELHORIAS DAS ARMAS", _melhorias) })
            {
                Ret(r, ativa ? new Color(0f, 0f, 0f, 0.32f) : r.Contains(mouse) ? new Color(0f, 0f, 0f, 0.2f) : new Color(0f, 0f, 0f, 0.1f));
                if (ativa) Ret(new Rect(r.x, r.yMax - 2 * u, r.width, 2 * u), Destaque);
                var e = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13 * u) };
                PorCor(e, ativa ? Destaque : TextoApagado);
                GUI.Label(r, nome, e);
            }
        }

        // o clique na barra e no painel (true: era dele)
        bool CliqueNaCriar(Vector2 m)
        {
            if (BotaoDasReceitas.Contains(m))
            {
                _melhorias = false;
                return true;
            }
            if (BotaoDasMelhorias.Contains(m))
            {
                _melhorias = true;
                _receita = -1;
                return true;
            }
            if (!_melhorias) return false;
            foreach (var (r, acao) in _cliquesDasMelhorias)
                if (r.Contains(m))
                {
                    acao?.Invoke();
                    return true;
                }
            return Em(AreaDasReceitas).Contains(m);
        }

        // as armas de sucata da mochila (a da mão primeiro)
        List<ItemNaMochila> ArmasDeSucata()
        {
            var l = new List<ItemNaMochila>();
            ushort mao = inventario.IdNaMao;
            foreach (var it in inventario.Mochila)
                if (Itens.DeSucata(it.item)) l.Add(it);
            l.Sort((a, b) => (b.id == mao).CompareTo(a.id == mao));
            return l;
        }

        void PainelDasMelhorias(Vector2 mouse)
        {
            float u = _u;
            _cliquesDasMelhorias.Clear();
            var area = Em(new Rect(AreaDasReceitas.x, AreaDasReceitas.y + BarraDaCriar, AreaDasReceitas.width, AreaDasReceitas.height - BarraDaCriar));
            var armas = ArmasDeSucata();
            bool perto = Bancada.Perto(inventario.transform.position);
            var pequeno = new GUIStyle(_pequeno) { wordWrap = false };
            // embaixo: a bancada e o aviso do que foi feito
            PorCor(pequeno, perto ? Bom : Ruim);
            GUI.Label(Em(new Rect(AreaDasReceitas.x + 2, AreaDasReceitas.yMax + 6, AreaDasReceitas.width, 18)),
                perto ? "Bancada perto: clique numa peça para pôr ou tirar." : "Longe de uma bancada: aqui só dá para ver (há uma no bunker).", pequeno);
            Aviso(new Rect(AreaDasReceitas.x + 2, AreaDasReceitas.yMax + 26, AreaDasReceitas.width, 20));
            if (armas.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y + 40 * u, area.width, 60 * u),
                    "Nenhuma arma de sucata na mochila.\nAs melhorias vão na pistola, no rifle e na submetralhadora de sucata.", _centro);
                return;
            }
            if (!armas.Exists(a => a.id == _armaDasMelhorias)) _armaDasMelhorias = armas[0].id;
            // a arma escolhida aparece nos detalhes (com as peças postas na foto)
            _escolhido = _armaDasMelhorias;
            _receita = -1;
            // as armas: uma ficha por arma
            float x = area.x + 4 * u, y = area.y + 2 * u, fichaW = 116 * u, fichaH = 46 * u;
            foreach (var a in armas)
            {
                if (x + fichaW > area.xMax) break;
                var f = new Rect(x, y, fichaW, fichaH);
                bool esta = a.id == _armaDasMelhorias;
                Ret(f, esta ? new Color(0f, 0f, 0f, 0.34f) : f.Contains(mouse) ? new Color(0f, 0f, 0f, 0.24f) : new Color(0f, 0f, 0f, 0.14f));
                if (esta) Moldura(f, Mathf.Max(1f, Mathf.Round(2 * u)), Destaque);
                var icone = inventario.IconeDe(a);
                if (icone != null) GUI.DrawTexture(new Rect(f.x + 4 * u, f.y + 3 * u, f.width - 8 * u, f.height - 18 * u), icone, ScaleMode.ScaleToFit);
                var e = new GUIStyle(_pequeno) { alignment = TextAnchor.LowerCenter, wordWrap = false, fontSize = Mathf.RoundToInt(11 * u) };
                int n = Melhorias.Postas(a.cores).Count;
                GUI.Label(new Rect(f.x, f.y, f.width, f.height - 2 * u), (a.id == inventario.IdNaMao ? "na mão · " : "") + (n == 0 ? "sem melhorias" : n == 1 ? "1 melhoria" : $"{n} melhorias"), e);
                ushort id = a.id;
                _cliquesDasMelhorias.Add((f, () => _armaDasMelhorias = id));
                x += fichaW + 6 * u;
            }
            y += fichaH + 8 * u;
            if (!inventario.Achar(_armaDasMelhorias, out var arma)) return;
            var tipo = Itens.Arma(arma.item);
            GUI.Label(new Rect(area.x + 6 * u, y, area.width, 20 * u), Maiuscula(Itens.Nome(arma.item)), new GUIStyle(_texto) { fontStyle = FontStyle.Bold, wordWrap = false });
            y += 22 * u;
            // as peças soltas da mochila (uma por item: a primeira; o silenciador mais gasto primeiro, para gastar ele)
            var soltas = new Dictionary<Item, ItemNaMochila>();
            var quantas = new Dictionary<Item, int>();
            foreach (var it in inventario.Mochila)
            {
                if (!Melhorias.EhPeca(it.item)) continue;
                quantas[it.item] = (quantas.TryGetValue(it.item, out int q) ? q : 0) + 1;
                if (!soltas.TryGetValue(it.item, out var antes) || it.desgaste > antes.desgaste) soltas[it.item] = it;
            }
            float colW = (area.width - 112 * u) / 2f, linhaH = 34 * u;
            for (int e = 0; e < 4; e++)
            {
                var encaixe = (EncaixeDaMelhoria)e;
                var postas = new List<PecaDaMelhoria>();
                var podem = new List<PecaDaMelhoria>();
                for (int i = 0; i < Melhorias.Quantas; i++)
                {
                    var p = (PecaDaMelhoria)i;
                    if (Melhorias.EncaixeDa(p) != encaixe || !Melhorias.Serve(p, tipo)) continue;
                    if (Melhorias.Posta(arma.cores, p)) postas.Add(p);
                    else if (soltas.ContainsKey(Melhorias.ItemDa(p))) podem.Add(p);
                }
                int linhas = Mathf.Max(1, Mathf.Max(postas.Count, podem.Count));
                var bloco = new Rect(area.x + 2 * u, y, area.width - 4 * u, linhas * linhaH + 22 * u);
                Ret(bloco, new Color(0f, 0f, 0f, 0.12f));
                var rotulo = new GUIStyle(_titulo) { fontSize = Mathf.RoundToInt(13 * u), wordWrap = false };
                GUI.Label(new Rect(bloco.x + 8 * u, bloco.y + 4 * u, 104 * u, 22 * u), Melhorias.NomeDo(encaixe).ToUpperInvariant(), rotulo);
                var cab = new GUIStyle(_pequeno) { wordWrap = false, fontSize = Mathf.RoundToInt(11 * u) };
                PorCor(cab, TextoApagado);
                float cx = bloco.x + 108 * u;
                GUI.Label(new Rect(cx, bloco.y + 3 * u, colW, 16 * u), "POSTO (clique: tirar)", cab);
                GUI.Label(new Rect(cx + colW + 4 * u, bloco.y + 3 * u, colW, 16 * u), "NA MOCHILA (clique: pôr)", cab);
                bool nenhumaServe = true;
                for (int i = 0; i < Melhorias.Quantas; i++)
                    if (Melhorias.EncaixeDa((PecaDaMelhoria)i) == encaixe && Melhorias.Serve((PecaDaMelhoria)i, tipo)) nenhumaServe = false;
                float ly = bloco.y + 20 * u;
                if (nenhumaServe)
                {
                    GUI.Label(new Rect(cx, ly + 8 * u, colW * 2, 18 * u), $"não vai n{(Itens.NomeComArtigo(arma.item).StartsWith("o ") ? "o" : "a")} {Itens.Nome(arma.item)}", cab);
                }
                else
                {
                    if (postas.Count == 0) GUI.Label(new Rect(cx, ly + 8 * u, colW, 18 * u), "vazio", cab);
                    if (podem.Count == 0) GUI.Label(new Rect(cx + colW + 4 * u, ly + 8 * u, colW, 18 * u), "nenhuma peça", cab);
                }
                for (int i = 0; i < postas.Count; i++)
                {
                    var p = postas[i];
                    string extra = Melhorias.Duracao(p) > 0 ? $"{Melhorias.Duracao(p) - Melhorias.TirosDoSilenciador(arma.cores)} tiros" : Melhorias.Efeito(p);
                    var r = new Rect(cx, ly + i * linhaH, colW - 2 * u, linhaH - 3 * u);
                    Ficha(r, Melhorias.ItemDa(p), extra, mouse, true);
                    ushort a = arma.id;
                    _cliquesDasMelhorias.Add((r, () => inventario.TirarMelhoria(a, p)));
                }
                for (int i = 0; i < podem.Count; i++)
                {
                    var p = podem[i];
                    var it = soltas[Melhorias.ItemDa(p)];
                    int n = quantas[it.item];
                    bool troca = Melhorias.NoLugarDe(arma.cores, p).HasValue;
                    string extra = (n > 1 ? $"×{n} · " : "") + (troca ? "troca a posta" : Melhorias.Efeito(p));
                    var r = new Rect(cx + colW + 4 * u, ly + i * linhaH, colW - 2 * u, linhaH - 3 * u);
                    Ficha(r, it.item, extra, mouse, false);
                    ushort a = arma.id, pid = it.id;
                    _cliquesDasMelhorias.Add((r, () => inventario.PorMelhoria(a, pid)));
                }
                y = bloco.yMax + 5 * u;
            }
        }

        // uma peça (o ícone, o nome e o que ela faz), clicável
        void Ficha(Rect r, Item item, string extra, Vector2 mouse, bool posta)
        {
            float u = _u;
            bool sob = r.Contains(mouse);
            Ret(r, sob ? (posta ? new Color(0.6f, 0.2f, 0.15f, 0.45f) : new Color(0.98f, 0.82f, 0.36f, 0.35f)) : new Color(0f, 0f, 0f, 0.2f));
            if (posta) Ret(new Rect(r.x, r.y, 3 * u, r.height), Destaque);
            var caixa = new Rect(r.x + 4 * u, r.y + 2 * u, r.height - 4 * u, r.height - 4 * u);
            Ret(caixa, FundoDeMaterial);
            var icone = inventario.IconeDe(item);
            if (icone != null) GUI.DrawTexture(Encolher(caixa, 1 * u), icone, ScaleMode.ScaleToFit);
            var nome = new GUIStyle(_pequeno) { wordWrap = false, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(11 * u) };
            var resto = new GUIStyle(_pequeno) { wordWrap = false, fontSize = Mathf.RoundToInt(10 * u) };
            PorCor(resto, TextoApagado);
            float tx = caixa.xMax + 5 * u, tw = r.xMax - tx - 2 * u;
            GUI.Label(new Rect(tx, r.y + 1 * u, tw, 16 * u), Maiuscula(Itens.Nome(item)), nome);
            GUI.Label(new Rect(tx, r.y + 15 * u, tw, 15 * u), extra, resto);
            if (sob) _dicaTexto = posta ? $"Tirar {Itens.NomeComArtigo(item)} (volta para a mochila)" : $"Pôr {Itens.NomeComArtigo(item)}";
        }
    }
}
