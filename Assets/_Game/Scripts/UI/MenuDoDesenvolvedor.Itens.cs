using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O F3 (31/10/2026, a pedido: "como o F1 tá bem grande, vamos colocar no F3 a parte de roupas e demais itens"): o painel
    /// dos itens, no lugar do F1 (um ou outro aberto), com as espécies de antes (as armas, a comida, os remédios, os
    /// materiais e os outros) e as roupas, por lugar no corpo, com as cores do catálogo ou sorteadas (as receitas dos tipos
    /// do sorteio e as dos NPCs prontos). Na mochila (sem espaço, no chão) ou no chão à frente. Os pedidos vão para o
    /// anfitrião (<see cref="CriarItemRpc"/>), como os do F1.
    /// </summary>
    public partial class MenuDoDesenvolvedor
    {
        bool _abertoItens;
        // a dona do F3 nas telas do menu (o F1 é o próprio componente)
        readonly object _telaDosItens = new object();
        int _lugarDaRoupa, _coresDaRoupa;
        // (feita na primeira vez: a ordem de iniciar os campos estáticos de dois arquivos da mesma classe não é garantida, e
        // a lista do F1, ItensDoMenu, ainda podia estar vazia)
        static string[] _especiesDoF3;
        static string[] EspeciesDoF3 => _especiesDoF3 ??= Especies();
        static readonly string[] LugaresDasRoupas = Lugares();
        static readonly string[] ModosDeCor = { "Cores do catálogo", "Cores sorteadas" };

        static string[] Especies()
        {
            var nomes = new List<string>(System.Array.ConvertAll(ItensDoMenu, e => e.nome)) { "Roupas" };
            return nomes.ToArray();
        }

        static string[] Lugares()
        {
            var nomes = new string[Itens.QuantosLugaresDeRoupa];
            for (int k = 0; k < nomes.Length; k++) nomes[k] = Itens.NomeDoLugar((LugarDaRoupa)k);
            return nomes;
        }

        void AbrirItens(bool abrir)
        {
            _abertoItens = abrir;
            MenuOnline.Tela(_telaDosItens, abrir);
            if (!abrir && !_aberto) Area = default;
        }

        // o painel do F3, no canto de cima, à esquerda (no lugar do F1)
        void PainelDosItens(float u)
        {
            var inventario = GetComponent<InventarioJogador>();
            bool roupas = _especie >= ItensDoMenu.Length;
            int maior = 0;
            foreach (var e in ItensDoMenu) maior = Mathf.Max(maior, e.itens.Length);
            // a altura da maior lista (a janela não pula ao trocar): a dos óculos (12, com os lugares e as cores em cima) ou
            // a maior espécie
            float altura = 8 + 30 + 34 + 64 + Mathf.Max(92 + 6 * 32, (maior + 1) / 2 * 32) + 44;
            var area = new Rect(12 * u, 40 * u, 380 * u, altura * u);
            Area = area;
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "ITENS E ROUPAS  (F3)", _titulo);
            y += 30 * u;
            _destino = GUI.SelectionGrid(new Rect(x, y, w, 26 * u), _destino, Destinos, 2, _botao);
            y += 34 * u;
            _especie = GUI.SelectionGrid(new Rect(x, y, w, 58 * u), _especie, EspeciesDoF3, 3, _botao);
            // o clique em "Roupas" vale já neste quadro (antes a lista de antes lia fora do ItensDoMenu)
            roupas = _especie >= ItensDoMenu.Length;
            y += 64 * u;
            float bw = (w - 8 * u) / 2f;
            if (roupas)
            {
                _lugarDaRoupa = GUI.SelectionGrid(new Rect(x, y, w, 58 * u), _lugarDaRoupa, LugaresDasRoupas, 5, _botao);
                y += 64 * u;
                _coresDaRoupa = GUI.SelectionGrid(new Rect(x, y, w, 24 * u), _coresDaRoupa, ModosDeCor, 2, _botao);
                y += 28 * u;
                int i = 0;
                for (int k = 0; k < Itens.QuantasRoupas; k++)
                {
                    if ((int)Itens.Roupa(k).lugar != _lugarDaRoupa) continue;
                    var item = Itens.DaRoupa(k);
                    var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                    i++;
                    if (GUI.Button(r, TelaDoInventario.Maiuscula(Itens.Nome(item)), _botao)) PedirItem(item, inventario, CoresParaARoupa(item));
                }
            }
            else
            {
                var lista = ItensDoMenu[_especie].itens;
                for (int i = 0; i < lista.Length; i++)
                {
                    var item = lista[i];
                    var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                    if (GUI.Button(r, TelaDoInventario.Maiuscula(Itens.Nome(item)), _botao)) PedirItem(item, inventario, 0);
                }
            }
            if (_aviso != null && Time.time < _avisoAte) GUI.Label(new Rect(x, area.yMax - 40 * u, w, 36 * u), _aviso, _texto);
        }

        void PedirItem(Item item, InventarioJogador inventario, ulong cores)
        {
            bool noChao = _destino == 1;
            if (!noChao && inventario != null && !inventario.PodeReceber(item, out var motivo))
            {
                noChao = true;
                Avisar($"{motivo}: {Itens.NomeComArtigo(item)} foi para o chão.");
            }
            CriarItemRpc(item, noChao, cores);
        }

        /// <summary>
        /// As cores da roupa: as do catálogo (0) ou uma receita sorteada (as dos tipos do sorteio e as dos NPCs prontos
        /// que vestem a peça; sem nenhuma, as do catálogo).
        /// </summary>
        ulong CoresParaARoupa(Item item)
        {
            if (_coresDaRoupa == 0) return 0;
            var catalogo = GetComponent<JogadorRede>()?.Vestimenta?.Catalogo;
            var dados = Itens.Roupa(item);
            if (catalogo == null || dados == null) return 0;
            var receitas = new List<string[]>();
            foreach (var t in catalogo.tipos ?? new Roupas.CatalogoDasRoupas.Tipo[0])
                foreach (var l in t.lugares ?? new Roupas.CatalogoDasRoupas.LugarDoTipo[0])
                    foreach (var o in l.opcoes ?? new Roupas.CatalogoDasRoupas.Opcao[0])
                        if (o.peca == dados.peca && o.cores != null)
                            foreach (var rc in o.cores) receitas.Add(rc.Split(' '));
            foreach (var n in catalogo.npcs ?? new Roupas.CatalogoDasRoupas.Npc[0])
                foreach (var p in n.pecas ?? new Roupas.CatalogoDasRoupas.PecaVestida[0])
                    if (p.nome == dados.peca && p.cores != null && p.cores.Length > 0) receitas.Add(p.cores);
            return receitas.Count == 0 ? 0 : Itens.Cores(receitas[Random.Range(0, receitas.Count)]);
        }
    }
}
