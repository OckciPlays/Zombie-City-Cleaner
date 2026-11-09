using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aba HABILIDADES da mochila (06/11/2026, a árvore de habilidades; a tecla K), refeita em 09/11/2026 para a árvore
    /// nova (a maquete aprovada pelo Pedro): ela ocupa o meio e a coluna da direita. Em cima, "Nível N · pontos livres X de
    /// N" e a barra do XP (o mouse nela mostra como ganhar XP); as 3 áreas em abas, cada uma na cor dela (o Combate laranja,
    /// a Resistência verde, a Sobrevivência roxa), e uma página por área: os ramos em colunas (o nome em cima, o selo "novo"
    /// nos ramos novos), os nós em quadrados ligados por linhas aos pré-requisitos (colorida quando os dois têm ponto), o
    /// "k/máx" no canto, a estrela nas pontas e, à esquerda, os pontos que cada fileira pede. O clique põe um ponto; o botão
    /// direito tira. Com o mouse num nó, um quadro mostra o nome, o ramo, o nível, o efeito do próximo nível e o que falta.
    /// </summary>
    public partial class TelaDoInventario
    {
        static readonly Rect AbaHabilidades = new Rect(670, 14, 160, 36);
        // a árvore: do meio até o fim da coluna da direita (a 720p)
        static readonly Rect AreaDaArvore = new Rect(GradeX, 58, 1142f - GradeX, 516);
        static readonly Rect BarraDoXp = new Rect(760, 68, 374, 9);
        static readonly Rect RodapeDaArvore = new Rect(GradeX + 8, 542, 1142f - GradeX - 16, 26);
        const float TopoDasAreas = 98f, AltoDasAreas = 28f, TopoDosRamos = 132f, AltoDosRamos = 28f;
        const float TopoDosNos = 164f, PassoDaFileira = 76f, AltoDoNo = 58f, MargemDasFileiras = 34f;

        AreaDaHabilidade _areaEscolhida = AreaDaHabilidade.Combate;
        int _noSobMouse = -1, _noNegado = -1;
        float _negadoAte;
        bool _sobreOXp, _avisoDaArvore, _pediuVisto;
        GUIStyle _nomeDoNo, _cantoDoNo, _seloDoNo, _simboloDoNo, _rotuloDaFileira, _cabecalhoDoRamo, _tituloDaDica, _textoDaDica, _pequenoDaDica;
        float _uDasHabilidades;

        static readonly Color EmBreve = new Color(0.95f, 0.6f, 0.3f);
        static readonly Color LinhaApagada = new Color(0.12f, 0.12f, 0.11f, 0.7f);

        /// <summary>A cor da área: o Combate laranja, a Resistência verde, a Sobrevivência roxa.</summary>
        static Color CorDaArea(AreaDaHabilidade a) => a == AreaDaHabilidade.Combate ? new Color(0.96f, 0.56f, 0.22f)
            : a == AreaDaHabilidade.Resistencia ? new Color(0.46f, 0.8f, 0.36f) : new Color(0.72f, 0.52f, 0.96f);

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
        }

        /// <summary>Para os testes: abre a mochila na aba HABILIDADES, na página da área.</summary>
        public void AbrirNaAbaHabilidades(AreaDaHabilidade area)
        {
            _areaEscolhida = area;
            IrParaAbaHabilidades();
            Abrir(true);
        }

        /// <summary>Para os testes (as fotos do quadro do nó): o mouse fingido, em pixels do OnGUI; x negativo, o de verdade.</summary>
        public Vector2 MouseDeTeste { get; set; } = new Vector2(-1f, -1f);

        /// <summary>Para os testes: o meio do nó na tela (em pixels do OnGUI, depois de um quadro aberta); fora da página, (-1, -1).</summary>
        public Vector2 PontoDoNo(Habilidade h) => Aberta && Habilidades.AreaDe(h) == _areaEscolhida ? Em(NoNaArvore(h)).center : new Vector2(-1f, -1f);

        // ------------------------------------------------------------------ as medidas (a 720p)

        static float InicioDasColunas => AreaDaArvore.x + 8f + MargemDasFileiras;
        static float LarguraDaColuna(AreaDaHabilidade a) => (AreaDaArvore.xMax - 8f - InicioDasColunas) / Habilidades.Colunas(a);
        static float TopoDaFileira(int f) => TopoDosNos + (f - 1) * PassoDaFileira;

        static Rect NoNaArvore(Habilidade h)
        {
            var a = Habilidades.AreaDe(h);
            float c = LarguraDaColuna(a), w = Mathf.Min(c - 16f, 118f);
            float cx = InicioDasColunas + (Habilidades.Coluna(h) + 0.5f) * c;
            return new Rect(cx - w / 2f, TopoDaFileira(Habilidades.Fileira(h)), w, AltoDoNo);
        }

        static Rect AbaDaArea(int a)
        {
            float x = AreaDaArvore.x + 8f, w = (AreaDaArvore.width - 16f - 2 * 8f) / 3f;
            return new Rect(x + a * (w + 8f), TopoDasAreas, w, AltoDasAreas);
        }

        static Rect ColunaDoRamo(RamoDaHabilidade r)
        {
            Habilidades.ColunasDoRamo(r, out float de, out float ate);
            float c = LarguraDaColuna(Habilidades.AreaDe(r));
            float x1 = InicioDasColunas + de * c + 3f, x2 = InicioDasColunas + (ate + 1f) * c - 3f;
            return new Rect(x1, TopoDosRamos, x2 - x1, TopoDaFileira(Habilidades.Fileiras) + AltoDoNo + 4f - TopoDosRamos);
        }

        void EstilosDasHabilidades(float u)
        {
            if (_nomeDoNo != null && Mathf.Approximately(u, _uDasHabilidades)) return;
            _uDasHabilidades = u;
            GUIStyle De(GUIStyle baseDe, int tamanho, FontStyle fonte, TextAnchor ancora, bool quebra) =>
                new GUIStyle(baseDe) { fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = fonte, alignment = ancora, wordWrap = quebra, richText = true };
            _nomeDoNo = De(_texto, 11, FontStyle.Bold, TextAnchor.MiddleCenter, true);
            _cantoDoNo = De(_texto, 11, FontStyle.Bold, TextAnchor.LowerRight, false);
            _seloDoNo = De(_texto, 9, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            _simboloDoNo = De(_texto, 13, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _rotuloDaFileira = De(_texto, 12, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _cabecalhoDoRamo = De(_texto, 11, FontStyle.Bold, TextAnchor.MiddleCenter, true);
            _tituloDaDica = De(_texto, 15, FontStyle.Bold, TextAnchor.UpperLeft, true);
            _textoDaDica = De(_texto, 13, FontStyle.Normal, TextAnchor.UpperLeft, true);
            _pequenoDaDica = De(_texto, 12, FontStyle.Normal, TextAnchor.UpperLeft, true);
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ------------------------------------------------------------------ o desenho

        /// <summary>A aba HABILIDADES: a árvore no lugar da grade e da coluna da direita.</summary>
        void AbaDasHabilidades(Vector2 mouse)
        {
            if (MouseDeTeste.x >= 0f) mouse = MouseDeTeste;
            float u = _u;
            EstilosDasHabilidades(u);
            _noSobMouse = -1;
            _sobreOXp = false;
            Ret(Em(AreaDaArvore), Painel);
            if (inventario == null || !inventario.IsSpawned) return;
            var p = inventario.Progresso.Value;
            int nivel = p.Nivel;
            var area = _areaEscolhida;
            var cor = CorDaArea(area);
            // o aviso da árvore nova (uma vez: o anfitrião apaga assim que ele aparece; aqui fica até o clique ou um ponto)
            if (!p.avisoDaArvore) _pediuVisto = false;   // (um save velho de novo, pelo F1, mostra outra vez)
            else if (!_pediuVisto)
            {
                _avisoDaArvore = true;
                _pediuVisto = true;
                inventario.PedirVistoOAviso();
            }

            // o nível, os pontos livres e a barra do XP
            float x = AreaDaArvore.x + 8f;
            var nomeDoNivel = new GUIContent($"NÍVEL {nivel}");
            var r = Em(new Rect(x, 62, 200, 30));
            GUI.Label(r, nomeDoNivel, _nome);
            float larguraDoNivel = _nome.CalcSize(nomeDoNivel).x;
            var pontos = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            PorCor(pontos, p.Pontos > 0 ? Destaque : TextoApagado);
            GUI.Label(new Rect(r.x + larguraDoNivel + 10 * u, r.y, 330 * u, r.height), $"·  pontos livres {p.Pontos} de {nivel}", pontos);
            float fracao = 1f;
            string textoDoXp = "nível máximo";
            if (nivel < Habilidades.NivelMaximo)
            {
                int feito = p.xp - Habilidades.XpAte(nivel), precisa = Habilidades.XpDoNivel(nivel);
                fracao = Mathf.Clamp01(feito / (float)precisa);
                textoDoXp = $"{feito} / {precisa} XP para o nível {nivel + 1}";
            }
            var barra = Em(BarraDoXp);
            Ret(barra, new Color(0f, 0f, 0f, 0.45f));
            Ret(new Rect(barra.x, barra.y, barra.width * fracao, barra.height), Destaque);
            var pequeno = new GUIStyle(_pequeno) { alignment = TextAnchor.MiddleRight };
            var textoDaBarra = Em(new Rect(BarraDoXp.x, BarraDoXp.yMax + 2, BarraDoXp.width, 16));
            GUI.Label(textoDaBarra, textoDoXp + "  ·  como ganhar XP?", pequeno);
            _sobreOXp = new Rect(barra.x, barra.y - 4 * u, barra.width, textoDaBarra.yMax - barra.y + 4 * u).Contains(mouse);

            // as 3 áreas, cada uma na cor dela (com os pontos gastos nela)
            for (int a = 0; a < 3; a++)
            {
                var aba = Em(AbaDaArea(a));
                var essa = (AreaDaHabilidade)a;
                var corDela = CorDaArea(essa);
                bool ativa = essa == area;
                Ret(aba, ativa ? new Color(corDela.r * 0.3f, corDela.g * 0.3f, corDela.b * 0.3f, 0.75f) : aba.Contains(mouse) ? new Color(0f, 0f, 0f, 0.3f) : new Color(0f, 0f, 0f, 0.18f));
                if (ativa) Ret(new Rect(aba.x, aba.yMax - 3 * u, aba.width, 3 * u), corDela);
                var est = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13 * u) };
                PorCor(est, ativa ? corDela : Color.Lerp(corDela, TextoApagado, 0.5f));
                int gastos = Habilidades.GastosAcima(p, essa, Habilidades.Fileiras + 1);
                GUI.Label(aba, gastos > 0 ? $"{Habilidades.Nome(essa)}  ·  {gastos}" : Habilidades.Nome(essa), est);
            }

            // os ramos: a faixa de cada um, o nome em cima e o selo "novo"
            foreach (var ramo in Habilidades.Ramos(area))
            {
                var faixa = Em(ColunaDoRamo(ramo));
                Ret(faixa, new Color(0f, 0f, 0f, 0.12f));
                var cabeca = new Rect(faixa.x + 2 * u, faixa.y, faixa.width - 4 * u, AltoDosRamos * u);
                PorCor(_cabecalhoDoRamo, CorDoTexto);
                string nome = Habilidades.Nome(ramo).ToUpperInvariant();
                if (Habilidades.RamoNovo(ramo)) nome += $"  <color={Hex(cor)}>NOVO</color>";
                GUI.Label(cabeca, nome, _cabecalhoDoRamo);
            }

            // as fileiras: os pontos que cada uma pede (acesos quando a fileira já abriu)
            for (int f = 1; f <= Habilidades.Fileiras; f++)
            {
                var rotulo = Em(new Rect(AreaDaArvore.x + 6f, TopoDaFileira(f), MargemDasFileiras, AltoDoNo));
                bool aberta = Habilidades.FileiraAberta(p, area, f);
                PorCor(_rotuloDaFileira, aberta ? cor : new Color(TextoApagado.r, TextoApagado.g, TextoApagado.b, 0.5f));
                GUI.Label(rotulo, $"{Habilidades.PontosDaFileira[f]}+", _rotuloDaFileira);
            }

            // as ligações aos pré-requisitos (as apagadas primeiro; as coloridas por cima, nos trechos que se juntam)
            for (int vez = 0; vez < 2; vez++)
                for (int i = 0; i < Habilidades.Quantas; i++)
                {
                    var h = (Habilidade)i;
                    if (Habilidades.AreaDe(h) != area) continue;
                    foreach (var q in Habilidades.Pede(h))
                    {
                        if (Habilidades.AreaDe(q) != area) continue;
                        bool acesa = p.Tem(h) && p.Tem(q);
                        if (acesa == (vez == 1)) Ligacao(Em(NoNaArvore(q)), Em(NoNaArvore(h)), acesa ? cor : LinhaApagada, u);
                    }
                }

            // os nós
            for (int i = 0; i < Habilidades.Quantas; i++)
            {
                var h = (Habilidade)i;
                if (Habilidades.AreaDe(h) != area) continue;
                var no = Em(NoNaArvore(h));
                DesenharNo(no, h, p, cor, no.Contains(mouse));
                if (no.Contains(mouse)) _noSobMouse = i;
            }

            // embaixo: o aviso da árvore nova, ou como se usa
            var rodape = Em(RodapeDaArvore);
            if (_avisoDaArvore)
            {
                Ret(rodape, new Color(0.45f, 0.33f, 0.08f, 0.95f));
                Moldura(rodape, Mathf.Max(1f, Mathf.Round(u)), Destaque);
                var est = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                PorCor(est, CorDoTexto);
                GUI.Label(rodape, Habilidades.AvisoDaArvore + "  (clique para fechar)", est);
            }
            else
            {
                var est = new GUIStyle(_pequeno) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                GUI.Label(rodape, "Clique: +1 ponto  ·  botão direito: tira um ponto  ·  à esquerda, os pontos que a fileira pede (gastos nas de cima desta área)", est);
            }
        }

        // a linha do pré-requisito ao nó: na mesma fileira, de lado a lado; de uma fileira à outra, desce, anda no vão e desce
        void Ligacao(Rect de, Rect para, Color cor, float u)
        {
            float e = Mathf.Max(2f, Mathf.Round(3f * u)), m = e / 2f;
            if (Mathf.Abs(de.y - para.y) < 1f)
            {
                float y = de.center.y - m;
                float x1 = de.center.x < para.center.x ? de.xMax : para.xMax, x2 = de.center.x < para.center.x ? para.x : de.x;
                Ret(new Rect(x1, y, x2 - x1, e), cor);
                return;
            }
            float xa = de.center.x, xb = para.center.x, ya = de.yMax, yb = para.y;
            if (Mathf.Abs(xa - xb) < 1f)
            {
                Ret(new Rect(xa - m, ya, e, yb - ya), cor);
                return;
            }
            float meio = Mathf.Round(yb - (PassoDaFileira - AltoDoNo) * 0.5f * u);
            Ret(new Rect(xa - m, ya, e, meio - ya + m), cor);
            Ret(new Rect(Mathf.Min(xa, xb) - m, meio - m, Mathf.Abs(xb - xa) + e, e), cor);
            Ret(new Rect(xb - m, meio - m, e, yb - meio + m), cor);
        }

        void DesenharNo(Rect no, Habilidade h, in ProgressoDoPersonagem p, Color cor, bool sobMouse)
        {
            float u = _u;
            int k = p.PontosEm(h), max = Habilidades.Maximo(h);
            bool pronta = Habilidades.Pronta(h);
            bool pode = Habilidades.PodeAprender(p, h, out _);
            // "dá para aprender" sem contar os pontos livres (a borda fica na cor, mais fraca, até ele subir de nível)
            bool aberta = pronta && k < max && Habilidades.FileiraAberta(p, Habilidades.AreaDe(h), Habilidades.Fileira(h));
            if (aberta) foreach (var q in Habilidades.Pede(h)) aberta &= p.Tem(q);
            bool negado = _noNegado == (int)h && Time.unscaledTime < _negadoAte;
            Color fundo, borda, texto;
            float grossura = 1f;
            if (!pronta)
            {
                fundo = new Color(0f, 0f, 0f, 0.22f);
                borda = new Color(0.36f, 0.36f, 0.34f, 0.9f);
                texto = new Color(TextoApagado.r, TextoApagado.g, TextoApagado.b, 0.6f);
            }
            else if (k > 0)
            {
                fundo = new Color(cor.r * 0.62f, cor.g * 0.62f, cor.b * 0.62f, 0.96f);
                borda = k >= max ? Color.Lerp(cor, Color.white, 0.45f) : cor;
                texto = Color.white;
                grossura = k >= max ? 2f : 1f;
            }
            else if (aberta)
            {
                fundo = new Color(0f, 0f, 0f, 0.4f);
                borda = pode ? cor : new Color(cor.r, cor.g, cor.b, 0.45f);
                texto = CorDoTexto;
                grossura = 2f;
            }
            else
            {
                fundo = new Color(0f, 0f, 0f, 0.2f);
                borda = new Color(0.3f, 0.3f, 0.28f, 0.85f);
                texto = new Color(TextoApagado.r, TextoApagado.g, TextoApagado.b, 0.55f);
            }
            Ret(no, fundo);
            if (sobMouse) Ret(no, new Color(1f, 1f, 1f, 0.07f));
            Moldura(no, Mathf.Max(1f, Mathf.Round((negado ? 2f : grossura) * u)), negado ? Ruim : sobMouse ? Color.Lerp(borda, Color.white, 0.4f) : borda);

            // o ícone do ramo (uma letra ou um símbolo), o selo "em breve" ao lado dele, a estrela da ponta, o nome e o "k/máx"
            var icone = new Rect(no.x + 4 * u, no.y + 4 * u, 18 * u, 18 * u);
            bool acesa = pronta && (k > 0 || aberta);
            Ret(icone, acesa ? (k > 0 ? new Color(0f, 0f, 0f, 0.3f) : new Color(cor.r, cor.g, cor.b, 0.85f)) : new Color(0f, 0f, 0f, 0.25f));
            PorCor(_simboloDoNo, acesa ? (k > 0 ? cor : new Color(0.08f, 0.08f, 0.07f)) : texto);
            GUI.Label(icone, Habilidades.Simbolo(Habilidades.RamoDe(h)), _simboloDoNo);
            if (Habilidades.EhPonta(h))
            {
                PorCor(_simboloDoNo, pronta ? Destaque : texto);
                GUI.Label(new Rect(no.xMax - 22 * u, no.y + 4 * u, 18 * u, 18 * u), "★", _simboloDoNo);
            }
            PorCor(_nomeDoNo, texto);
            GUI.Label(new Rect(no.x + 4 * u, no.y + 21 * u, no.width - 8 * u, 24 * u), Habilidades.Nome(h), _nomeDoNo);
            PorCor(_cantoDoNo, k > 0 ? Color.white : texto);
            GUI.Label(new Rect(no.xMax - 44 * u, no.yMax - 16 * u, 40 * u, 14 * u), $"{k}/{max}", _cantoDoNo);
            if (!pronta)
            {
                PorCor(_seloDoNo, EmBreve);
                GUI.Label(new Rect(icone.xMax + 5 * u, icone.y, 64 * u, icone.height), "EM BREVE", _seloDoNo);
            }
        }

        /// <summary>
        /// A aba HABILIDADES no lugar dos detalhes (a árvore cobre a coluna da direita): o quadro do nó debaixo do mouse (o
        /// nome, o ramo, o nível, o efeito de agora e o do próximo nível, e o que falta), ou de onde vem o XP (o mouse na barra).
        /// </summary>
        void ColunaDasHabilidades(Vector2 mouse)
        {
            if (inventario == null || !inventario.IsSpawned || MenuAberto) return;
            if (MouseDeTeste.x >= 0f) mouse = MouseDeTeste;
            float u = _u;
            EstilosDasHabilidades(u);
            var p = inventario.Progresso.Value;
            float w = 330 * u, x = 12 * u;
            var linhas = new System.Collections.Generic.List<(string texto, GUIStyle estilo, Color cor)>();
            Color corDaBorda = Friso;
            if (_noSobMouse >= 0 && _noSobMouse < Habilidades.Quantas)
            {
                var h = (Habilidade)_noSobMouse;
                var ramo = Habilidades.RamoDe(h);
                var cor = CorDaArea(Habilidades.AreaDe(h));
                corDaBorda = cor;
                int k = p.PontosEm(h), max = Habilidades.Maximo(h), f = Habilidades.Fileira(h);
                bool pronta = Habilidades.Pronta(h);
                linhas.Add((Habilidades.Nome(h).ToUpperInvariant() + (Habilidades.EhPonta(h) ? "   ★ PONTA" : ""), _tituloDaDica, cor));
                linhas.Add(($"{Habilidades.Nome(ramo)} · fileira {f} ({Habilidades.PontosDaFileira[f]}+ pontos nas de cima)", _pequenoDaDica, TextoApagado));
                linhas.Add(($"Nível {k}/{max}", new GUIStyle(_textoDaDica) { fontStyle = FontStyle.Bold }, CorDoTexto));
                if (!pronta) linhas.Add((Habilidades.Efeito(h, 1), _textoDaDica, CorDoTexto));
                else
                {
                    if (k > 0) linhas.Add(($"Agora: {Habilidades.Efeito(h, k)}", _textoDaDica, CorDoTexto));
                    if (k < max) linhas.Add(($"{(k == 0 ? "Com 1 ponto" : $"No nível {k + 1}")}: {Habilidades.Efeito(h, k + 1)}", _textoDaDica, k == 0 ? CorDoTexto : Bom));
                }
                if (!pronta) linhas.Add(("Em breve: chega numa próxima etapa (ainda não dá para aprender).", _pequenoDaDica, EmBreve));
                else if (k < max)
                {
                    if (Habilidades.PodeAprender(p, h, out string falta)) linhas.Add(("Clique: +1 ponto.", _pequenoDaDica, Bom));
                    else linhas.Add((falta, _pequenoDaDica, Ruim));
                }
                if (k > 0)
                {
                    if (Habilidades.PodeTirar(p, h, out string preso)) linhas.Add(("Botão direito: tira um ponto.", _pequenoDaDica, TextoApagado));
                    else linhas.Add(($"Não dá para tirar: {preso}", _pequenoDaDica, TextoApagado));
                }
            }
            else if (_sobreOXp)
            {
                linhas.Add(("COMO GANHAR XP", _tituloDaDica, Destaque));
                foreach (var l in ComoGanhar) linhas.Add((l, _pequenoDaDica, CorDoTexto));
                linhas.Add(($"Cada nível dá 1 ponto (até o nível {Habilidades.NivelMaximo}).", _pequenoDaDica, TextoApagado));
            }
            if (linhas.Count == 0) return;

            float alto = 8 * u;
            foreach (var l in linhas) alto += l.estilo.CalcHeight(new GUIContent(l.texto), w - 2 * x) + 3 * u;
            var r = new Rect(mouse.x + 18 * u, mouse.y + 16 * u, w, alto + 6 * u);
            if (r.xMax > Screen.width - 4) r.x = mouse.x - r.width - 10 * u;
            if (r.yMax > Screen.height - 4) r.y = Mathf.Max(4f, Screen.height - 4 - r.height);
            Ret(r, new Color(0.07f, 0.07f, 0.065f, 0.96f));
            Moldura(r, Mathf.Max(1f, Mathf.Round(u)), corDaBorda);
            float y = r.y + 7 * u;
            foreach (var l in linhas)
            {
                var c = new GUIContent(l.texto);
                float hh = l.estilo.CalcHeight(c, w - 2 * x);
                PorCor(l.estilo, l.cor);
                GUI.Label(new Rect(r.x + x, y, w - 2 * x, hh), c, l.estilo);
                y += hh + 3 * u;
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

        // o nó da página aberta debaixo do mouse (-1: nenhum)
        int NoEm(Vector2 m)
        {
            for (int i = 0; i < Habilidades.Quantas; i++)
                if (Habilidades.AreaDe((Habilidade)i) == _areaEscolhida && Em(NoNaArvore((Habilidade)i)).Contains(m)) return i;
            return -1;
        }

        void Negar(int no)
        {
            _noNegado = no;
            _negadoAte = Time.unscaledTime + 0.45f;
        }

        /// <summary>O clique na aba HABILIDADES (a área, o aviso, um nó: +1 ponto). Devolve se pegou.</summary>
        bool CliqueNaAbaHabilidades(Vector2 m)
        {
            for (int a = 0; a < 3; a++)
                if (Em(AbaDaArea(a)).Contains(m))
                {
                    _areaEscolhida = (AreaDaHabilidade)a;
                    return true;
                }
            if (_avisoDaArvore && Em(RodapeDaArvore).Contains(m))
            {
                _avisoDaArvore = false;
                return true;
            }
            int no = NoEm(m);
            if (no < 0 || inventario == null || !inventario.IsSpawned) return false;
            if (Habilidades.PodeAprender(inventario.Progresso.Value, (Habilidade)no, out _))
            {
                inventario.PedirAprender((Habilidade)no);
                _avisoDaArvore = false;
            }
            else Negar(no);
            return true;
        }

        /// <summary>O botão direito na aba HABILIDADES: tira um ponto do nó (se nada depender dele). Devolve se pegou.</summary>
        bool TirarNaAbaHabilidades(Vector2 m)
        {
            int no = NoEm(m);
            if (no < 0 || inventario == null || !inventario.IsSpawned) return false;
            if (Habilidades.PodeTirar(inventario.Progresso.Value, (Habilidade)no, out _)) inventario.PedirTirar((Habilidade)no);
            else Negar(no);
            return true;
        }
    }
}
