using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aba HABILIDADES da mochila (06/11/2026, a árvore de habilidades; a tecla K): em cima, o nível, os pontos livres e
    /// a barra do XP; as 3 áreas (COMBATE, RESISTÊNCIA, SOBREVIVÊNCIA) em botões; a área escolhida mostra os 3 ramos, cada um
    /// com as 5 habilidades de cima para baixo (a última é a ponta, com a estrela). O clique escolhe; a coluna da direita
    /// mostra o que ela faz, o botão APRENDER e de onde vem o XP. As que ainda não têm o efeito no jogo dizem "em breve"
    /// (dá para aprender: o efeito chega depois, e o caminho até a ponta não trava).
    /// </summary>
    public partial class TelaDoInventario
    {
        static readonly Rect AbaHabilidades = new Rect(670, 14, 160, 36);
        static readonly Rect AreaDasHabilidades = new Rect(GradeX, 58, GradeMaxW, 486);
        static readonly Rect BotaoAprender = new Rect(850, 312, 282, 36);
        const float BotoesDasAreasY = 132f, RamosY = 172f, NoY = 198f, NoH = 56f, NoPasso = 68f, ColunaVao = 8f;

        AreaDaHabilidade _areaEscolhida = AreaDaHabilidade.Combate;
        int _habilidadeEscolhida = -1;
        GUIStyle _nomeDoNo, _estadoDoNo, _textoQuebrado;
        float _uDasHabilidades;

        static readonly Color Aprendida = new Color(0.2f, 0.42f, 0.22f, 0.92f);
        static readonly Color EmBreve = new Color(0.95f, 0.6f, 0.3f);

        // a tecla K: como o H da aba CORPO (fechada abre nela; aberta nela fecha; aberta em outra, troca)
        void TeclaDasHabilidades()
        {
            if (Aberta)
            {
                if (_aba == Aba.Habilidades) Abrir(false);
                else IrParaAbaHabilidades();
            }
            else if (MenuOnline.Controla && rede != null && rede.IsOwner && !MenuOnline.Aberto)
            {
                IrParaAbaHabilidades();
                Abrir(true);
            }
        }

        void IrParaAbaHabilidades()
        {
            _aba = Aba.Habilidades;
            if (_apertou == Origem.Estante)
            {
                _apertou = Origem.Nada;
                _arrastando = false;
            }
            _estante = -1;
            if (_habilidadeEscolhida < 0) _habilidadeEscolhida = (int)ProximaDoRamo(Habilidades.Primeira((RamoDaHabilidade)((int)_areaEscolhida * 3)));
        }

        // a primeira do ramo ainda não aprendida (ou a ponta, com o ramo cheio)
        Habilidade ProximaDoRamo(Habilidade primeira)
        {
            var p = inventario != null && inventario.IsSpawned ? inventario.Progresso.Value : default;
            for (int i = 0; i < Habilidades.PorRamo; i++)
                if (!p.Tem((Habilidade)((int)primeira + i))) return (Habilidade)((int)primeira + i);
            return (Habilidade)((int)primeira + Habilidades.PorRamo - 1);
        }

        // as medidas (a 720p): o botão de cada área e o quadrado de cada habilidade da área escolhida
        static Rect BotaoDaArea(int a)
        {
            float w = (AreaDasHabilidades.width - 12f - 2 * ColunaVao) / 3f;
            return new Rect(AreaDasHabilidades.x + 6f + a * (w + ColunaVao), BotoesDasAreasY, w, 30f);
        }

        static Rect ColunaDoRamo(int k)
        {
            float w = (AreaDasHabilidades.width - 12f - 2 * ColunaVao) / 3f;
            return new Rect(AreaDasHabilidades.x + 6f + k * (w + ColunaVao), RamosY, w, NoY + Habilidades.PorRamo * NoPasso - RamosY);
        }

        static Rect NoDaHabilidade(int k, int ordem)
        {
            var c = ColunaDoRamo(k);
            return new Rect(c.x + 4f, NoY + ordem * NoPasso, c.width - 8f, NoH);
        }

        void EstilosDasHabilidades(float u)
        {
            if (_nomeDoNo != null && Mathf.Approximately(u, _uDasHabilidades)) return;
            _uDasHabilidades = u;
            _nomeDoNo = new GUIStyle(_texto) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true, fontSize = Mathf.RoundToInt(13 * u) };
            _estadoDoNo = new GUIStyle(_pequeno) { alignment = TextAnchor.LowerCenter, wordWrap = false };
            _textoQuebrado = new GUIStyle(_texto) { wordWrap = true };
        }

        // ------------------------------------------------------------------ o desenho

        /// <summary>A aba HABILIDADES, no lugar da grade.</summary>
        void AbaDasHabilidades(Vector2 mouse)
        {
            float u = _u;
            EstilosDasHabilidades(u);
            Ret(Encolher(Em(AreaDasHabilidades), -4 * u), Painel);
            if (inventario == null || !inventario.IsSpawned) return;
            var p = inventario.Progresso.Value;
            int nivel = p.Nivel;

            // o nível, os pontos e a barra do XP
            float x = AreaDasHabilidades.x + 8f, w = AreaDasHabilidades.width - 16f;
            GUI.Label(Em(new Rect(x, 64, 220, 30)), $"NÍVEL {nivel}", _nome);
            var pontos = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleRight };
            PorCor(pontos, p.Pontos > 0 ? Destaque : TextoApagado);
            GUI.Label(Em(new Rect(x + w - 240, 64, 240, 30)), p.Pontos == 1 ? "1 ponto para gastar" : $"{p.Pontos} pontos para gastar", pontos);
            float fracao = 1f;
            string textoDoXp = "nível máximo";
            if (nivel < Habilidades.NivelMaximo)
            {
                int feito = p.xp - Habilidades.XpAte(nivel), precisa = Habilidades.XpDoNivel(nivel);
                fracao = Mathf.Clamp01(feito / (float)precisa);
                textoDoXp = $"{feito} / {precisa} XP para o nível {nivel + 1}";
            }
            var barra = Em(new Rect(x, 98, w, 10));
            Ret(barra, new Color(0f, 0f, 0f, 0.45f));
            Ret(new Rect(barra.x, barra.y, barra.width * fracao, barra.height), Destaque);
            var pequeno = new GUIStyle(_pequeno) { alignment = TextAnchor.MiddleRight };
            GUI.Label(Em(new Rect(x, 108, w, 18)), textoDoXp, pequeno);

            // as 3 áreas
            for (int a = 0; a < 3; a++)
            {
                var r = Em(BotaoDaArea(a));
                bool ativa = (int)_areaEscolhida == a;
                Ret(r, ativa ? new Color(0f, 0f, 0f, 0.35f) : r.Contains(mouse) ? new Color(0f, 0f, 0f, 0.2f) : new Color(0f, 0f, 0f, 0.12f));
                if (ativa) Ret(new Rect(r.x, r.yMax - 3 * u, r.width, 3 * u), Destaque);
                var est = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13 * u) };
                PorCor(est, ativa ? Destaque : TextoApagado);
                GUI.Label(r, Habilidades.Nome((AreaDaHabilidade)a), est);
            }

            // os 3 ramos da área, cada um com as 5 habilidades
            for (int k = 0; k < 3; k++)
            {
                var ramo = (RamoDaHabilidade)((int)_areaEscolhida * 3 + k);
                var coluna = ColunaDoRamo(k);
                var titulo = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(Em(new Rect(coluna.x, RamosY, coluna.width, 22)), Habilidades.Nome(ramo).ToUpperInvariant(), titulo);
                var primeira = Habilidades.Primeira(ramo);
                for (int o = 0; o < Habilidades.PorRamo; o++)
                {
                    var h = (Habilidade)((int)primeira + o);
                    var no = Em(NoDaHabilidade(k, o));
                    // a ligação com a de baixo
                    if (o < Habilidades.PorRamo - 1)
                    {
                        var embaixo = Em(NoDaHabilidade(k, o + 1));
                        bool as2 = p.Tem(h) && p.Tem((Habilidade)((int)h + 1));
                        Ret(new Rect(no.center.x - 2 * u, no.yMax, 4 * u, embaixo.y - no.yMax), as2 ? Bom : new Color(0f, 0f, 0f, 0.35f));
                    }
                    DesenharNo(no, h, p, mouse);
                }
            }
        }

        void DesenharNo(Rect no, Habilidade h, in ProgressoDoPersonagem p, Vector2 mouse)
        {
            float u = _u;
            bool tem = p.Tem(h);
            bool pode = Habilidades.PodeAprender(p, h, out _);
            bool anteriorOk = Habilidades.OrdemNoRamo(h) == 0 || p.Tem((Habilidade)((int)h - 1));
            bool ponta = Habilidades.EhPonta(h);
            bool escolhida = (int)h == _habilidadeEscolhida;
            Color fundo = tem ? Aprendida : anteriorOk ? new Color(0f, 0f, 0f, 0.38f) : new Color(0f, 0f, 0f, 0.2f);
            Ret(no, fundo);
            if (no.Contains(mouse) && !tem) Ret(no, new Color(1f, 1f, 1f, 0.06f));
            Color borda = tem ? Bom : pode ? Destaque : ponta ? new Color(0.6f, 0.5f, 0.25f) : BordaClara;
            Moldura(no, Mathf.Max(1f, Mathf.Round((escolhida ? 3f : ponta ? 2f : 1f) * u)), escolhida ? Color.white : borda);
            PorCor(_nomeDoNo, tem || anteriorOk ? CorDoTexto : TextoApagado);
            var dentro = new Rect(no.x + 4 * u, no.y + 5 * u, no.width - 8 * u, no.height - 8 * u);
            GUI.Label(dentro, (ponta ? "★ " : "") + Habilidades.Nome(h), _nomeDoNo);
            string estado;
            Color cor;
            if (tem) { estado = "aprendida"; cor = Bom; }
            else if (pode) { estado = "1 ponto: aprender"; cor = Destaque; }
            else if (!anteriorOk) { estado = "trancada"; cor = TextoApagado; }
            else { estado = "sem pontos"; cor = TextoApagado; }
            if (!Habilidades.Pronta(h)) { estado += " · em breve"; if (!tem) cor = EmBreve; }
            PorCor(_estadoDoNo, cor);
            GUI.Label(dentro, estado, _estadoDoNo);
            if (no.Contains(mouse)) _dicaTexto = Habilidades.Descricao(h);
        }

        /// <summary>A coluna da direita na aba HABILIDADES: a escolhida, o botão de aprender e de onde vem o XP.</summary>
        void ColunaDasHabilidades(Vector2 mouse)
        {
            float u = _u;
            EstilosDasHabilidades(u);
            var nome = Em(AreaDoNome);
            var resto = Em(new Rect(AreaDaFoto.x, AreaDaFoto.y, AreaDaFoto.width, AreaDosDetalhes.yMax - AreaDaFoto.y));
            Ret(nome, Painel);
            Ret(resto, Painel);
            if (inventario == null || !inventario.IsSpawned) return;
            var p = inventario.Progresso.Value;
            var faixaDoNome = new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height);
            float x = resto.x + 12 * u, w = resto.width - 24 * u, y = resto.y + 8 * u;
            if (_habilidadeEscolhida >= 0 && _habilidadeEscolhida < Habilidades.Quantas)
            {
                var h = (Habilidade)_habilidadeEscolhida;
                var ramo = Habilidades.RamoDe(h);
                var nomeCurto = new GUIStyle(_nome) { fontSize = Mathf.RoundToInt(17 * u) };
                GUI.Label(faixaDoNome, Habilidades.Nome(h).ToUpperInvariant(), nomeCurto);
                if (Habilidades.EhPonta(h))
                {
                    var estrela = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleRight };
                    PorCor(estrela, Destaque);
                    GUI.Label(faixaDoNome, "★ PONTA", estrela);
                }
                Linha($"{Maiuscula(Habilidades.Nome(Habilidades.AreaDe(ramo)).ToLowerInvariant())} · {Habilidades.Nome(ramo)} · {Habilidades.OrdemNoRamo(h) + 1}ª de {Habilidades.PorRamo}", TextoApagado);
                y += 2 * u;
                Linha(Habilidades.Descricao(h), CorDoTexto);
                if (!Habilidades.Pronta(h)) Linha("Em breve: dá para aprender agora, e o efeito chega numa próxima versão.", EmBreve);
                bool pode = Habilidades.PodeAprender(p, h, out string motivo);
                if (p.Tem(h)) Linha("Aprendida.", Bom);
                else if (!pode) Linha(motivo, Ruim);
                // o botão
                var botao = Em(BotaoAprender);
                bool ativo = pode;
                Ret(botao, ativo ? (botao.Contains(mouse) ? new Color(0.36f, 0.5f, 0.22f, 0.95f) : new Color(0.28f, 0.4f, 0.18f, 0.95f)) : new Color(0f, 0f, 0f, 0.25f));
                Moldura(botao, Mathf.Max(1f, Mathf.Round(u)), ativo ? Bom : BordaClara);
                var est = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter };
                PorCor(est, ativo ? CorDoTexto : TextoApagado);
                GUI.Label(botao, p.Tem(h) ? "APRENDIDA" : "APRENDER (1 PONTO)", est);
            }
            else
            {
                PorCor(_nome, TextoApagado);
                GUI.Label(faixaDoNome, "ESCOLHA UMA", _nome);
                PorCor(_nome, CorDoTexto);
            }

            // de onde vem o XP
            float yXp = Em(new Rect(0, BotaoAprender.yMax + 14f, 0, 0)).y;
            GUI.Label(new Rect(x, yXp, w, 22 * u), "COMO GANHAR XP", _titulo);
            yXp += 26 * u;
            var pequeno = new GUIStyle(_pequeno) { wordWrap = true };
            foreach (var l in ComoGanhar)
            {
                var c = new GUIContent(l);
                float hh = pequeno.CalcHeight(c, w);
                if (yXp + hh > resto.yMax - 4 * u) break;
                GUI.Label(new Rect(x, yXp, w, hh), c, pequeno);
                yXp += hh + 2 * u;
            }

            void Linha(string texto, Color cor)
            {
                var c = new GUIContent(texto);
                float hh = _textoQuebrado.CalcHeight(c, w);
                PorCor(_textoQuebrado, cor);
                GUI.Label(new Rect(x, y, w, hh), c, _textoQuebrado);
                y += hh + 4 * u;
            }
        }

        static readonly string[] ComoGanhar =
        {
            $"Zumbi: {Habilidades.XpDoOco} (o Corredor {Habilidades.XpDoCorredor}, o Long {Habilidades.XpDoLong}, o Brutamonte {Habilidades.XpDoBrutamonte})",
            $"Sobrevivente: {Habilidades.XpDoSobrevivente} · militar: {Habilidades.XpDoMilitar}",
            $"Mais um dia vivo: {Habilidades.XpDoDia}",
            $"Curativo: {Habilidades.XpDoCurativo} (no amigo, {Habilidades.XpDoCurativoNoAmigo}) · infecção curada: {Habilidades.XpDaInfeccaoCurada}",
            "Criar um item: 1 a 6 · saque das casas: 1 a 4",
            $"Ligação direta num carro novo: {Habilidades.XpDaLigacaoDireta}",
            $"Pescar: {Habilidades.XpDaPesca} (o dourado, {Habilidades.XpDoDourado}) · cortar uma árvore: {Habilidades.XpDaArvore}",
        };

        // ------------------------------------------------------------------ o clique

        /// <summary>O clique na aba HABILIDADES (a área, a habilidade, o botão de aprender). Devolve se pegou.</summary>
        bool CliqueNaAbaHabilidades(Vector2 m)
        {
            for (int a = 0; a < 3; a++)
                if (Em(BotaoDaArea(a)).Contains(m))
                {
                    _areaEscolhida = (AreaDaHabilidade)a;
                    _habilidadeEscolhida = (int)ProximaDoRamo(Habilidades.Primeira((RamoDaHabilidade)(a * 3)));
                    return true;
                }
            for (int k = 0; k < 3; k++)
                for (int o = 0; o < Habilidades.PorRamo; o++)
                    if (Em(NoDaHabilidade(k, o)).Contains(m))
                    {
                        _habilidadeEscolhida = (int)Habilidades.Primeira((RamoDaHabilidade)((int)_areaEscolhida * 3 + k)) + o;
                        return true;
                    }
            if (Em(BotaoAprender).Contains(m) && _habilidadeEscolhida >= 0 && inventario != null && inventario.IsSpawned)
            {
                if (Habilidades.PodeAprender(inventario.Progresso.Value, (Habilidade)_habilidadeEscolhida, out _))
                    inventario.PedirAprender((Habilidade)_habilidadeEscolhida);
                return true;
            }
            return false;
        }
    }
}
