using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A aba ANOTAÇÕES da mochila (25/11/2026, a pedido do Pedro: "No tab por exemplo, quando vamos em anotações, tudo bem ter
    /// as missões disponíveis ainda e as missões que já realizamos, as riscadas"). Um caderno aberto, com a espiral no meio e,
    /// desde a etapa 9 (o caderno do P), as abas de papel em cima: Missões, Zumbis, Animais, Construções e Receitas
    /// (CadernoDeMissoes.Fichas.cs). Nas Missões: na folha da esquerda, cada fase com o título e as submissões (as feitas
    /// riscadas e a da vez com o ponto); na da direita, a da vez com o que ajuda e quantas já foram feitas. As fases que não
    /// aconteceram neste mundo não aparecem (o Dia 0 e a fuga, num mundo que começou depois deles; o tutorial do Gus, num
    /// mundo de antes do caderno, que foi direto para o Capitão), nem as que ainda vêm. Com
    /// muitas, as fases mais antigas fecham no título riscado. Desenhado pela <see cref="Player.TelaDoInventario"/> (o TAB ou
    /// o P).
    /// </summary>
    public partial class CadernoDeMissoes
    {
        static GUIStyle _tituloDaPagina, _faseDaPagina, _linhaDaPagina, _detalheDaPagina, _nomeDaAba, _nomeDeTras, _numeroDaPagina;
        static float _uDaPagina;

        /// <summary>
        /// (26/11/2026, a pedido do Pedro: os textos cortados) a folga, à direita, das caixas das linhas em itálico (os
        /// detalhes): o itálico da letra de mão inclina o alto da última letra para fora da medida da quebra, e a borda da
        /// caixa cortava a ponta dela. A quebra continua na largura de antes; só a caixa é mais larga (em unidades da escala).
        /// </summary>
        const float FolgaDoItalico = 6f;

        // tipo: 0 o título de uma fase feita, 1 o da fase de agora, 2 uma submissão feita, 3 a da vez
        struct Anotacao
        {
            public string texto;
            public int tipo;
            public Anotacao(string texto, int tipo) { this.texto = texto; this.tipo = tipo; }
        }

        /// <summary>As fases deste mundo até a de agora, cada uma com as submissões feitas e a da vez.</summary>
        static List<List<Anotacao>> AsFases(MundoDeLakewood m, out int feitas)
        {
            feitas = 0;
            var fases = new List<List<Anotacao>>();
            if (m == null || !m.IsSpawned) return fases;
            var atual = m.Fase;
            if (atual == FaseDaCampanha.Nenhuma) return fases;
            foreach (var f in new[] { FaseDaCampanha.DiaZero, FaseDaCampanha.Fuga, FaseDaCampanha.OBunker, FaseDaCampanha.RetomeAWoodcreek })
            {
                if ((f == FaseDaCampanha.DiaZero || f == FaseDaCampanha.Fuga) && !m.ComDiaZero) continue;
                // (25/11/2026, a revisão: num save de antes do caderno, as 11 do Gus apareciam riscadas e somavam nas "feitas")
                // o tutorial do Gus só aparece se aconteceu neste mundo (a fase de agora aparece sempre)
                if (f == FaseDaCampanha.OBunker && f != atual && !m.ComOBunker) continue;
                if (PrimeiraPronta(f) < 0) continue;
                if (atual != FaseDaCampanha.Livre && f > atual) break;
                bool feita = atual == FaseDaCampanha.Livre || f < atual;
                var l = new List<Anotacao> { new Anotacao(Titulo(f), feita ? 0 : 1) };
                for (int p = 0; p < Passos(f); p++)
                {
                    if (!Pronta(f, p)) continue;
                    if (feita || p < m.Passo)
                    {
                        l.Add(new Anotacao(Texto(f, p, m, false), 2));
                        feitas++;
                    }
                    else if (p == m.Passo)
                    {
                        l.Add(new Anotacao(Texto(f, p, m, true), 3));
                        break;
                    }
                }
                fases.Add(l);
            }
            return fases;
        }

        /// <summary>
        /// O caderno aberto na área (em pixels da tela; u, a escala da mochila): as abas de papel em cima e as duas folhas da
        /// aba escolhida. <paramref name="inventario"/>: o do jogador (as receitas, os zumbis vistos e os bichos caçados).
        /// </summary>
        public static void DesenharAsAnotacoes(Rect area, float u, Player.InventarioJogador inventario = null)
        {
            EstilosDaPagina(u);
            var cor = GUI.color;
            // (a etapa 9) a faixa das abas em cima; o caderno desce um pouco (a mesma conta do clique: CadernoDeMissoes.Fichas.cs)
            Geometria(area, u, out var faixa, out area, out var esq, out var dir);
            float vao = dir.x - esq.xMax;
            float larg = esq.width;
            float altura = _linhaDaPagina.lineHeight * 1.2f;
            float topo = 48f * u, margem = 34f * u;

            // as abas de trás, a capa (o papelão por trás), as duas folhas, a espiral no meio e a aba da frente
            AbasDeTras(faixa, u);
            GUI.color = new Color(0.2f, 0.16f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(area.x - 6f * u, area.y - 5f * u, area.width + 12f * u, area.height + 10f * u), Texture2D.whiteTexture);
            Pagina(esq, topo, altura, margem, u);
            Pagina(dir, topo, altura, margem, u);
            Espiral(new Rect(esq.xMax, area.y, vao, area.height), u);
            AbaDaFrente(faixa, area, u);

            switch (_abaDoCaderno)
            {
                case AbaDoCaderno.Zumbis: OsZumbis(esq, dir, topo, margem, altura, inventario, u); break;
                case AbaDoCaderno.Animais: OsAnimais(esq, dir, topo, margem, altura, inventario, u); break;
                case AbaDoCaderno.Construcoes: AsReceitas(esq, dir, topo, margem, altura, inventario, true, u); break;
                case AbaDoCaderno.Receitas: AsReceitas(esq, dir, topo, margem, altura, inventario, false, u); break;
                default:
                    _folhasDaAba = 1;
                    AsMissoes(esq, dir, topo, margem, altura, larg, u);
                    break;
            }
            NumerosDasPaginas(esq, dir, u);
            GUI.color = cor;
        }

        // a aba Missões: na folha da esquerda, as fases; na da direita, a da vez
        static void AsMissoes(Rect esq, Rect dir, float topo, float margem, float altura, float larg, float u)
        {
            var m = MundoDeLakewood.Instancia;
            float alturaDaFase = _faseDaPagina.lineHeight * 1.15f;
            Cabecalho(esq, margem, "Missões", u);
            float largura = larg - margem - 16f * u;
            var fases = AsFases(m, out int feitas);
            float y = esq.y + topo;
            float fim = esq.yMax - 30f * u;   // (embaixo, o número da página)
            if (fases.Count == 0)
            {
                GUI.color = ComAlfa(Tinta, 0.8f);
                GUI.Label(new Rect(esq.x + margem, y, largura, altura), "Nenhuma missão neste mundo.", _linhaDaPagina);
            }
            else
            {
                // com muitas, as fases mais antigas fecham no título riscado
                var linhas = Linhas(fases, largura, out var fechadas);
                float cabe = fim - y;
                for (int i = 0; i < fases.Count - 1 && Altura(linhas, altura, alturaDaFase) > cabe; i++)
                {
                    fechadas[i] = true;
                    linhas = Linhas(fases, largura, out _, fechadas);
                }
                foreach (var (texto, tipo, primeira, extra) in linhas)
                {
                    if (y > fim) break;
                    if (tipo < 0) { y += altura * 0.45f; continue; }   // o espaço entre as fases
                    bool fase = tipo <= 1;
                    var estilo = fase ? _faseDaPagina : _linhaDaPagina;
                    float h = fase ? alturaDaFase : altura;
                    bool riscada = tipo == 0 || tipo == 2;
                    GUI.color = riscada ? TintaFeita : Tinta;
                    float x = esq.x + margem + (fase ? 0f : 4f * u);
                    if (!fase && primeira) GUI.Label(new Rect(esq.x + 12f * u, y, margem, h), riscada ? "x" : "•", estilo);
                    GUI.Label(new Rect(x, y, largura, h), texto, estilo);
                    float largDoTexto = estilo.CalcSize(new GUIContent(texto)).x;
                    if (extra.Length > 0)
                    {
                        GUI.color = ComAlfa(Tinta, 0.6f);
                        GUI.Label(new Rect(x + largDoTexto + 8f * u, y + 3f * u, largura, h), extra, _detalheDaPagina);
                    }
                    if (riscada) Riscar(new Rect(x - 2f * u, y, largDoTexto + 4f * u, h), 1f, 1f, u);
                    else if (fase)
                    {
                        GUI.color = ComAlfa(Tinta, 0.75f);
                        GUI.DrawTexture(new Rect(x, y + h - 5f * u, largDoTexto, 3f * u), Risco);
                    }
                    y += h;
                }
            }

            // a folha da direita: a da vez, o que ajuda e quantas já foram
            Cabecalho(dir, margem, "Agora", u);
            float yd = dir.y + topo;
            if (m != null && m.IsSpawned && NaCampanha(m.Fase))
            {
                GUI.color = ComAlfa(Tinta, 0.7f);
                GUI.Label(new Rect(dir.x + margem, yd, largura, alturaDaFase), Titulo(m.Fase), _detalheDaPagina);
                yd += altura;
                bool primeira = true;
                foreach (var l in Quebrar(Texto(m.Fase, m.Passo, m, true), largura, _linhaDaPagina))
                {
                    GUI.color = Tinta;
                    if (primeira) GUI.Label(new Rect(dir.x + 12f * u, yd, margem, altura), "•", _linhaDaPagina);
                    primeira = false;
                    GUI.Label(new Rect(dir.x + margem + 4f * u, yd, largura, altura), l, _linhaDaPagina);
                    yd += altura;
                }
                foreach (var d in Detalhes(m, m.Fase, m.Passo))
                foreach (var l in Quebrar(d, largura, _detalheDaPagina))
                {
                    GUI.color = ComAlfa(Tinta, 0.75f);
                    // (26/11/2026, a pedido do Pedro: os textos cortados) a caixa com a folga da inclinação do itálico
                    GUI.Label(new Rect(dir.x + margem + 4f * u, yd + 2f * u, largura + FolgaDoItalico * u, altura), l, _detalheDaPagina);
                    yd += altura;
                }
            }
            else if (m != null && m.IsSpawned && m.Fase == FaseDaCampanha.Livre)
            {
                foreach (var l in Quebrar("Nenhuma missão agora. A Woodcreek é nossa de novo: explore, junte o que faltar no bunker e volte inteiro.", largura, _linhaDaPagina))
                {
                    GUI.color = Tinta;
                    GUI.Label(new Rect(dir.x + margem, yd, largura, altura), l, _linhaDaPagina);
                    yd += altura;
                }
            }
            if (feitas > 0)
            {
                yd += altura;
                GUI.color = ComAlfa(Tinta, 0.7f);
                GUI.Label(new Rect(dir.x + margem, yd, largura, altura), feitas == 1 ? "1 missão feita até aqui." : $"{feitas} missões feitas até aqui.", _detalheDaPagina);
            }
        }

        // as linhas quebradas pela largura: (texto, tipo, a primeira da anotação, o "N feitas" de uma fase fechada); tipo -1, o espaço
        static List<(string, int, bool, string)> Linhas(List<List<Anotacao>> fases, float largura, out bool[] fechadas, bool[] jaFechadas = null)
        {
            fechadas = jaFechadas ?? new bool[fases.Count];
            var saida = new List<(string, int, bool, string)>();
            for (int i = 0; i < fases.Count; i++)
            {
                if (i > 0) saida.Add(("", -1, false, ""));
                var f = fases[i];
                if (fechadas[i])
                {
                    int n = f.Count - 1;
                    saida.Add((f[0].texto, f[0].tipo, true, n == 1 ? "(1 feita)" : $"({n} feitas)"));
                    continue;
                }
                foreach (var a in f)
                {
                    bool primeira = true;
                    foreach (var l in Quebrar(a.texto, largura - 4f, a.tipo <= 1 ? _faseDaPagina : _linhaDaPagina))
                    {
                        saida.Add((l, a.tipo, primeira, ""));
                        primeira = false;
                    }
                }
            }
            return saida;
        }

        static float Altura(List<(string, int, bool, string)> linhas, float altura, float alturaDaFase)
        {
            float h = 0f;
            foreach (var (_, tipo, _, _) in linhas) h += tipo < 0 ? altura * 0.45f : tipo <= 1 ? alturaDaFase : altura;
            return h;
        }

        // uma folha do caderno aberto: o papel, a pauta e a margem (sem os furos de cima)
        static void Pagina(Rect r, float topo, float altura, float margem, float u)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(r, Papel);
            GUI.color = Pauta;
            for (float y = r.y + topo + altura - 3f * u; y < r.yMax - 4f * u; y += altura)
                GUI.DrawTexture(new Rect(r.x + 4f * u, y, r.width - 8f * u, Mathf.Max(1f, 1.1f * u)), Texture2D.whiteTexture);
            GUI.color = Margem;
            GUI.DrawTexture(new Rect(r.x + margem - 7f * u, r.y + 4f * u, Mathf.Max(1f, 1.4f * u), r.height - 8f * u), Texture2D.whiteTexture);
        }

        // a espiral entre as folhas: os furos nas duas e o arame passando
        static void Espiral(Rect r, float u)
        {
            for (float y = r.y + 12f * u; y < r.yMax - 8f * u; y += 19f * u)
            {
                GUI.color = new Color(0.22f, 0.2f, 0.18f, 0.95f);
                GUI.DrawTexture(new Rect(r.x - 9f * u, y, 6f * u, 6f * u), Furo);
                GUI.DrawTexture(new Rect(r.xMax + 3f * u, y, 6f * u, 6f * u), Furo);
                GUI.color = new Color(0.62f, 0.63f, 0.64f, 1f);
                GUI.DrawTexture(new Rect(r.x - 6f * u, y + 1.5f * u, r.width + 12f * u, 3f * u), Texture2D.whiteTexture);
                GUI.color = new Color(0.85f, 0.86f, 0.87f, 1f);
                GUI.DrawTexture(new Rect(r.x - 6f * u, y + 1.5f * u, r.width + 12f * u, 1f * u), Texture2D.whiteTexture);
            }
        }

        static void Cabecalho(Rect pagina, float margem, string texto, float u)
        {
            GUI.color = Tinta;
            // (26/11/2026, a pedido do Pedro: os textos cortados) a caixa do título tinha a altura justa da letra de mão, e o pé
            // do g ("Agora") encostava na borda de baixo: 36u (o traço continua em 38u e a primeira linha em 48u)
            GUI.Label(new Rect(pagina.x + margem, pagina.y + 10f * u, pagina.width - margem, 36f * u), texto, _tituloDaPagina);
            float l = _tituloDaPagina.CalcSize(new GUIContent(texto)).x;
            GUI.color = ComAlfa(Tinta, 0.8f);
            GUI.DrawTexture(new Rect(pagina.x + margem, pagina.y + 38f * u, l, 4f * u), Risco);
        }

        static void EstilosDaPagina(float u)
        {
            if (_tituloDaPagina != null && Mathf.Approximately(u, _uDaPagina)) return;
            _uDaPagina = u;
            _tituloDaPagina = EstiloDeMao(25f * u);
            _faseDaPagina = EstiloDeMao(19f * u);
            _linhaDaPagina = EstiloDeMao(15.5f * u);
            _detalheDaPagina = EstiloDeMao(13.5f * u);
            _detalheDaPagina.fontStyle = FontStyle.Italic;
            _nomeDaAba = EstiloDeMao(17f * u);
            _nomeDaAba.alignment = TextAnchor.MiddleCenter;
            _nomeDeTras = EstiloDeMao(15f * u);
            _nomeDeTras.alignment = TextAnchor.MiddleCenter;
            _numeroDaPagina = EstiloDeMao(14f * u);
            _numeroDaPagina.alignment = TextAnchor.MiddleCenter;
        }
    }
}
