using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aba HABILIDADES da mochila (06/11/2026, a árvore de habilidades; a tecla K), refeita em 09/11/2026 para a árvore
    /// nova (a maquete aprovada pelo Pedro): ela ocupa o meio e a coluna da direita. Em cima, "Nível N · pontos livres X de
    /// N" e a barra do XP (o mouse nela mostra como ganhar XP); as 3 áreas em abas, cada uma na cor dela (o Combate laranja,
    /// a Resistência verde, a Sobrevivência roxa), e uma página por área: os ramos em colunas (o nome em cima, o selo "novo"
    /// nos ramos novos), os nós e, à esquerda, os pontos que cada fileira pede. O clique põe um ponto; o botão direito tira.
    /// Com o mouse num nó, um quadro mostra o ícone, o nome, o ramo, o nível, o efeito do próximo nível e o que falta.
    /// Desde 10/11/2026 (a referência do Pedro, o "Dungeons &amp; Developers"), cada nó é o ícone dele num quadrado (um PNG
    /// pintado no Blender, pelo Resources: <see cref="IconeDaHabilidade"/>), com o nome embaixo: o aprendido tem a moldura
    /// dourada e grossa, com um brilho em volta; o que dá para aprender, a moldura na cor da área; o trancado e o "em
    /// breve" ficam cinza e escuros (o "em breve" com o selo por cima); o "k/máx" no canto de baixo e a estrela das pontas
    /// no de cima. As ligações são grossas, com a seta no nó que pede: apagadas, e douradas quando os dois lados estão
    /// aprendidos (também as de um ramo para outro).
    /// </summary>
    public partial class TelaDoInventario
    {
        static readonly Rect AbaHabilidades = new Rect(670, 14, 160, 36);
        // a árvore: do meio até o fim da coluna da direita (a 720p)
        static readonly Rect AreaDaArvore = new Rect(GradeX, 58, 1142f - GradeX, 516);
        static readonly Rect BarraDoXp = new Rect(760, 68, 374, 9);
        static readonly Rect RodapeDaArvore = new Rect(GradeX + 8, 551, 1142f - GradeX - 16, 18);
        const float TopoDasAreas = 98f, AltoDasAreas = 28f, TopoDosRamos = 130f, AltoDosRamos = 26f;
        // os nós (10/11/2026, os ícones): o quadrado do ícone, o nome embaixo (uma linha) e, no vão de 16, as setas
        const float TopoDosNos = 158f, PassoDaFileira = 81f, LadoDoNo = 50f, AltoDoNome = 13f, MargemDasFileiras = 34f;

        AreaDaHabilidade _areaEscolhida = AreaDaHabilidade.Combate;
        int _noSobMouse = -1, _noNegado = -1;
        float _negadoAte;
        bool _sobreOXp, _avisoDaArvore, _pediuVisto;
        GUIStyle _nomeDoNo, _cantoDoNo, _seloDoNo, _simboloDoNo, _estrelaDoNo, _rotuloDaFileira, _cabecalhoDoRamo, _tituloDaDica, _textoDaDica, _pequenoDaDica;
        GUIStyle[] _nomesDoNo;   // o nome do nó em 10, 9 e 8 (o longo diminui até caber numa linha)
        float _uDasHabilidades;

        static readonly Color EmBreve = new Color(0.95f, 0.6f, 0.3f);
        static readonly Color LinhaApagada = new Color(0.16f, 0.155f, 0.13f, 1f);
        static readonly Color Ouro = new Color(1f, 0.8f, 0.3f), OuroClaro = new Color(1f, 0.93f, 0.66f);
        static readonly Color MolduraApagada = new Color(0.3f, 0.3f, 0.28f, 0.95f);

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

        // o quadrado do ícone do nó (o nome fica embaixo, em NomeNaArvore)
        static Rect NoNaArvore(Habilidade h)
        {
            float cx = InicioDasColunas + (Habilidades.Coluna(h) + 0.5f) * LarguraDaColuna(Habilidades.AreaDe(h));
            return new Rect(cx - LadoDoNo / 2f, TopoDaFileira(Habilidades.Fileira(h)), LadoDoNo, LadoDoNo);
        }

        // a linha do nome, embaixo do ícone, da largura da coluna
        static Rect NomeNaArvore(Habilidade h)
        {
            var no = NoNaArvore(h);
            float w = LarguraDaColuna(Habilidades.AreaDe(h)) - 4f;
            return new Rect(no.center.x - w / 2f, no.yMax + 2f, w, AltoDoNome);
        }

        // onde o mouse pega o nó: o quadrado e o nome embaixo
        static Rect AlvoDoNo(Habilidade h)
        {
            var no = NoNaArvore(h);
            return new Rect(no.x - 12f, no.y - 3f, no.width + 24f, LadoDoNo + 2f + AltoDoNome + 4f);
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
            return new Rect(x1, TopoDosRamos, x2 - x1, TopoDaFileira(Habilidades.Fileiras) + LadoDoNo + 2f + AltoDoNome + 2f - TopoDosRamos);
        }

        void EstilosDasHabilidades(float u)
        {
            if (_nomeDoNo != null && Mathf.Approximately(u, _uDasHabilidades)) return;
            _uDasHabilidades = u;
            GUIStyle De(GUIStyle baseDe, int tamanho, FontStyle fonte, TextAnchor ancora, bool quebra) =>
                new GUIStyle(baseDe) { fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = fonte, alignment = ancora, wordWrap = quebra, richText = true };
            _nomeDoNo = De(_texto, 10, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _nomesDoNo = new[] { _nomeDoNo, De(_texto, 9, FontStyle.Bold, TextAnchor.MiddleCenter, false), De(_texto, 8, FontStyle.Bold, TextAnchor.MiddleCenter, false) };
            foreach (var e in _nomesDoNo) e.clipping = TextClipping.Overflow;
            _cantoDoNo = De(_texto, 9, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _seloDoNo = De(_texto, 8, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _simboloDoNo = De(_texto, 13, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _estrelaDoNo = De(_texto, 11, FontStyle.Bold, TextAnchor.MiddleCenter, false);
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
                var rotulo = Em(new Rect(AreaDaArvore.x + 6f, TopoDaFileira(f), MargemDasFileiras, LadoDoNo));
                bool aberta = Habilidades.FileiraAberta(p, area, f);
                PorCor(_rotuloDaFileira, aberta ? cor : new Color(TextoApagado.r, TextoApagado.g, TextoApagado.b, 0.5f));
                GUI.Label(rotulo, $"{Habilidades.PontosDaFileira[f]}+", _rotuloDaFileira);
            }

            // as ligações aos pré-requisitos (também as de um ramo para outro): as apagadas primeiro; as douradas por cima
            // (o brilho de todas, depois as linhas), nos trechos que se juntam
            for (int vez = 0; vez < 3; vez++)
                for (int i = 0; i < Habilidades.Quantas; i++)
                {
                    var h = (Habilidade)i;
                    if (Habilidades.AreaDe(h) != area) continue;
                    foreach (var q in Habilidades.Pede(h))
                    {
                        if (Habilidades.AreaDe(q) != area) continue;
                        bool acesa = p.Tem(h) && p.Tem(q);
                        if (acesa != (vez > 0)) continue;
                        if (vez == 1) Ligacao(Em(NoNaArvore(q)), Em(NoNaArvore(h)), new Color(Ouro.r, Ouro.g, Ouro.b, 0.22f), u, 2f * u);
                        else Ligacao(Em(NoNaArvore(q)), Em(NoNaArvore(h)), acesa ? Ouro : LinhaApagada, u, 0f);
                    }
                }

            // os nós: o ícone, a moldura, os selos e o nome embaixo
            for (int i = 0; i < Habilidades.Quantas; i++)
            {
                var h = (Habilidade)i;
                if (Habilidades.AreaDe(h) != area) continue;
                bool sob = Em(AlvoDoNo(h)).Contains(mouse);
                DesenharNo(Em(NoNaArvore(h)), Em(NomeNaArvore(h)), h, p, cor, sob);
                if (sob) _noSobMouse = i;
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

        // ------------------------------------------------------------------ as ligações e os nós (10/11/2026: os ícones)

        // a ligação do pré-requisito (de) ao nó que o pede (para), entre os quadrados dos ícones: na mesma fileira, de lado a
        // lado; de uma fileira para outra, sai debaixo do nome, desce, anda no vão de cima do nó e desce até ele. A seta
        // aponta para o nó que pede. alem: a mais de cada lado (o brilho das douradas, sem a seta)
        void Ligacao(Rect de, Rect para, Color cor, float u, float alem)
        {
            float e = Mathf.Max(2f, Mathf.Round(4f * u)) + 2f * alem;
            float ponta = Mathf.Round(6.5f * u), boca = Mathf.Round(12f * u), folga = Mathf.Max(1f, Mathf.Round(u));
            bool seta = alem <= 0f;
            if (Mathf.Abs(de.y - para.y) < 1f)
            {
                float y = Mathf.Round(de.center.y);
                bool direita = de.center.x < para.center.x;
                float x1 = direita ? de.xMax : de.x, xp = direita ? para.x - folga : para.xMax + folga;   // xp: onde a seta encosta
                float xb = direita ? xp - ponta : xp + ponta;
                Trecho(x1, y, seta ? xb : xp, y, e, cor);
                if (seta) Triangulo(new Rect(direita ? xb : xp, y - boca / 2f, ponta, boca), direita ? 1 : 2, cor);
                return;
            }
            float xa = Mathf.Round(de.center.x), xc = Mathf.Round(para.center.x);
            float ya = de.yMax + (2f + AltoDoNome) * u;     // debaixo do nome
            float yp = para.y - folga, yb = yp - ponta;      // a ponta e a base da seta
            if (Mathf.Abs(xa - xc) < 1f) Trecho(xa, ya + e / 2f, xa, seta ? yb : yp, e, cor);
            else
            {
                float meio = Mathf.Round(yb - 4.5f * u);
                Trecho(xa, ya + e / 2f, xa, meio, e, cor);
                Trecho(xa, meio, xc, meio, e, cor);
                Trecho(xc, meio, xc, seta ? yb : yp, e, cor);
            }
            if (seta) Triangulo(new Rect(xc - boca / 2f, yb, boca, ponta), 0, cor);
        }

        // um trecho reto da ligação, com meia grossura além de cada ponta (as dobras fecham)
        static void Trecho(float x1, float y1, float x2, float y2, float e, Color cor)
        {
            float m = e / 2f;
            Ret(Rect.MinMaxRect(Mathf.Round(Mathf.Min(x1, x2) - m), Mathf.Round(Mathf.Min(y1, y2) - m),
                Mathf.Round(Mathf.Max(x1, x2) + m), Mathf.Round(Mathf.Max(y1, y2) + m)), cor);
        }

        static void Triangulo(Rect r, int lado, Color cor)
        {
            var antes = GUI.color;
            GUI.color = cor;
            GUI.DrawTexture(r, Seta(lado));
            GUI.color = antes;
        }

        static Texture2D[] _setas;

        // a ponta da seta (0 para baixo, 1 para a direita, 2 para a esquerda): um triângulo branco, com a borda suave
        static Texture2D Seta(int lado)
        {
            _setas ??= new Texture2D[3];
            if (_setas[lado] != null) return _setas[lado];
            const int n = 32, s = 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Seta", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dentro = 0;
                    for (int sy = 0; sy < s; sy++)
                        for (int sx = 0; sx < s; sx++)
                        {
                            // px da esquerda, py de cima (a linha 0 da textura é a de baixo na tela)
                            float px = (x + (sx + 0.5f) / s) / n, py = 1f - (y + (sy + 0.5f) / s) / n;
                            float ao = lado == 0 ? py : lado == 1 ? px : 1f - px, at = lado == 0 ? px : py;   // ao longo e através
                            if (Mathf.Abs(at - 0.5f) <= 0.5f * (1f - ao)) dentro++;
                        }
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, dentro / (float)(s * s)));
                }
            t.Apply();
            return _setas[lado] = t;
        }

        const float BrilhoEmVolta = 0.2f;   // o brilho do aprendido passa do quadrado (a fração do lado, em cada lado)
        static Texture2D _brilho;

        // o brilho em volta do aprendido: o quadrado no meio e o alfa caindo até a borda (esticado sobre o nó)
        static Texture2D Brilho()
        {
            if (_brilho != null) return _brilho;
            const int n = 64;
            float m = n * BrilhoEmVolta / (1f + 2f * BrilhoEmVolta);
            _brilho = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Brilho do nó", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(m - (x + 0.5f), x + 0.5f - (n - m)));
                    float dy = Mathf.Max(0f, Mathf.Max(m - (y + 0.5f), y + 0.5f - (n - m)));
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / m);
                    _brilho.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _brilho.Apply();
            return _brilho;
        }

        // os estados do nó: aprendido (a moldura dourada e o brilho), dá para aprender (a moldura na cor da área),
        // trancado (o ícone cinza e escuro, a moldura apagada) e em breve (como o trancado, com o selo)
        enum EstadoDoNo : byte { Aprendido, Aberto, Trancado, EmBreve }

        static EstadoDoNo EstadoDe(Habilidade h, in ProgressoDoPersonagem p)
        {
            if (!Habilidades.Pronta(h)) return EstadoDoNo.EmBreve;
            if (p.PontosEm(h) > 0) return EstadoDoNo.Aprendido;
            // "dá para aprender" sem contar os pontos livres (a moldura fica na cor, mais fraca, até ele subir de nível)
            if (!Habilidades.FileiraAberta(p, Habilidades.AreaDe(h), Habilidades.Fileira(h))) return EstadoDoNo.Trancado;
            foreach (var q in Habilidades.Pede(h))
                if (!p.Tem(q)) return EstadoDoNo.Trancado;
            return EstadoDoNo.Aberto;
        }

        // o aprendido com todos os pontos brilha mais; o que dá para aprender, com ponto livre, tem a moldura cheia
        static bool Forte(Habilidade h, in ProgressoDoPersonagem p, EstadoDoNo estado) =>
            estado == EstadoDoNo.Aprendido ? p.PontosEm(h) >= Habilidades.Maximo(h) : Habilidades.PodeAprender(p, h, out _);

        static Texture2D[] _icones, _iconesCinza;

        /// <summary>
        /// O ícone do nó (10/11/2026): <c>Art/UI/Habilidades/Resources/Hab_&lt;Habilidade&gt;.png</c>, pelo Resources, como
        /// as roupas (nenhuma cena muda); cinza: a cópia sem cor, <c>Hab_&lt;Habilidade&gt;_Cinza.png</c>, a dos trancados.
        /// Pintados pelo Blender/icones_habilidades.py e trazidos pelo Editor/MontagemDosIconesDasHabilidades. null: falta
        /// (o nó mostra a letra do ramo).
        /// </summary>
        internal static Texture2D IconeDaHabilidade(Habilidade h, bool cinza = false)
        {
            if (_icones == null)
            {
                _icones = new Texture2D[Habilidades.Quantas];
                _iconesCinza = new Texture2D[Habilidades.Quantas];
                for (int i = 0; i < Habilidades.Quantas; i++)
                {
                    _icones[i] = Resources.Load<Texture2D>($"Hab_{(Habilidade)i}");
                    _iconesCinza[i] = Resources.Load<Texture2D>($"Hab_{(Habilidade)i}_Cinza");
                }
            }
            int k = (int)h;
            if (k < 0 || k >= Habilidades.Quantas) return null;
            return cinza && _iconesCinza[k] != null ? _iconesCinza[k] : _icones[k];
        }

        // o ícone no quadrado, com a moldura do estado (e, no aprendido, o brilho em volta): o nó e o quadro do mouse usam
        void IconeNoQuadrado(Rect r, Habilidade h, EstadoDoNo estado, Color cor, bool forte, bool brilho, bool sobMouse, bool negado, float u)
        {
            float px = Mathf.Max(1f, Mathf.Round(u));
            bool apagado = estado >= EstadoDoNo.Trancado;
            var antes = GUI.color;
            if (brilho && estado == EstadoDoNo.Aprendido)
            {
                GUI.color = new Color(Ouro.r, Ouro.g, Ouro.b, forte ? 0.8f : 0.6f);
                GUI.DrawTexture(Encolher(r, -r.width * BrilhoEmVolta), Brilho());
            }
            GUI.color = antes;
            Ret(Encolher(r, -px), new Color(0f, 0f, 0f, 0.7f));   // o contorno escuro, por fora da moldura
            var icone = IconeDaHabilidade(h, apagado);
            if (icone != null)
            {
                float luz = estado == EstadoDoNo.EmBreve ? 0.45f : estado == EstadoDoNo.Trancado ? 0.6f : 1f;
                GUI.color = new Color(luz, luz, luz, 1f);
                GUI.DrawTexture(r, icone, ScaleMode.StretchToFill);
                GUI.color = antes;
            }
            else
            {
                Ret(r, new Color(0.06f, 0.06f, 0.055f));
                PorCor(_simboloDoNo, apagado ? TextoApagado : cor);
                GUI.Label(r, Habilidades.Simbolo(Habilidades.RamoDe(h)), _simboloDoNo);
            }
            if (sobMouse) Ret(r, new Color(1f, 1f, 1f, 0.08f));
            Color borda;
            float grossura;
            if (estado == EstadoDoNo.Aprendido)
            {
                borda = Ouro;
                grossura = 3f;
            }
            else if (estado == EstadoDoNo.Aberto)
            {
                borda = forte ? cor : new Color(cor.r, cor.g, cor.b, 0.5f);
                grossura = 2.5f;
            }
            else
            {
                borda = MolduraApagada;
                grossura = 2f;
            }
            if (negado)
            {
                borda = Ruim;
                grossura = 3f;
            }
            else if (sobMouse) borda = Color.Lerp(borda, Color.white, 0.35f);
            float g = Mathf.Max(1f, Mathf.Round(grossura * u));
            Moldura(r, g, borda);
            if (estado == EstadoDoNo.Aprendido && !negado) Moldura(Encolher(r, g), px, new Color(OuroClaro.r, OuroClaro.g, OuroClaro.b, 0.5f));
        }

        void DesenharNo(Rect no, Rect nome, Habilidade h, in ProgressoDoPersonagem p, Color cor, bool sobMouse)
        {
            float u = _u, px = Mathf.Max(1f, Mathf.Round(u));
            int k = p.PontosEm(h), max = Habilidades.Maximo(h);
            var estado = EstadoDe(h, p);
            bool negado = _noNegado == (int)h && Time.unscaledTime < _negadoAte;
            IconeNoQuadrado(no, h, estado, cor, Forte(h, p, estado), true, sobMouse, negado, u);
            var antes = GUI.color;

            // o selo "em breve", numa faixa no meio do ícone
            if (estado == EstadoDoNo.EmBreve)
            {
                var faixa = new Rect(no.x + 3 * px, Mathf.Round(no.center.y - 7 * u), no.width - 6 * px, Mathf.Round(14 * u));
                Ret(faixa, new Color(0.05f, 0.04f, 0.03f, 0.88f));
                var linha = new Color(EmBreve.r, EmBreve.g, EmBreve.b, 0.75f);
                Ret(new Rect(faixa.x, faixa.y, faixa.width, px), linha);
                Ret(new Rect(faixa.x, faixa.yMax - px, faixa.width, px), linha);
                PorCor(_seloDoNo, EmBreve);
                GUI.Label(faixa, "EM BREVE", _seloDoNo);
            }

            // a estrela das pontas, no canto de cima
            if (Habilidades.EhPonta(h))
            {
                var canto = new Rect(Mathf.Round(no.xMax - 8 * u), Mathf.Round(no.y - 6 * u), Mathf.Round(14 * u), Mathf.Round(14 * u));
                GUI.color = new Color(0.05f, 0.05f, 0.045f, 0.95f);
                GUI.DrawTexture(canto, Circulo());
                GUI.color = antes;
                PorCor(_estrelaDoNo, estado == EstadoDoNo.Aprendido ? Ouro : estado == EstadoDoNo.Aberto ? Destaque : TextoApagado);
                GUI.Label(canto, "★", _estrelaDoNo);
            }

            // o "k/máx", numa placa no canto de baixo
            var texto = new GUIContent($"{k}/{max}");
            float lw = Mathf.Round(_cantoDoNo.CalcSize(texto).x + 6 * u), lh = Mathf.Round(12 * u);
            var placa = new Rect(Mathf.Round(no.xMax - lw + 3 * u), Mathf.Round(no.yMax - lh + 2 * u), lw, lh);
            Ret(placa, new Color(0.04f, 0.04f, 0.035f, 0.95f));
            Moldura(placa, px, estado == EstadoDoNo.Aprendido ? Ouro : estado == EstadoDoNo.Aberto ? new Color(cor.r, cor.g, cor.b, 0.8f) : MolduraApagada);
            PorCor(_cantoDoNo, estado == EstadoDoNo.Aprendido ? Color.white : estado == EstadoDoNo.Aberto ? CorDoTexto : TextoApagado);
            GUI.Label(placa, texto, _cantoDoNo);

            // o nome, embaixo (a letra diminui até caber numa linha)
            var conteudo = new GUIContent(Habilidades.Nome(h));
            GUIStyle est = _nomeDoNo;
            foreach (var e in _nomesDoNo)
            {
                est = e;
                if (e.CalcSize(conteudo).x <= nome.width) break;
            }
            PorCor(est, estado == EstadoDoNo.Aprendido ? OuroClaro : estado == EstadoDoNo.Aberto ? CorDoTexto
                : new Color(TextoApagado.r, TextoApagado.g, TextoApagado.b, 0.6f));
            GUI.Label(nome, conteudo, est);
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

            // o ícone do nó (10/11/2026) à esquerda das duas primeiras linhas (o nome e o ramo)
            bool comIcone = _noSobMouse >= 0 && _noSobMouse < Habilidades.Quantas;
            float lado = comIcone ? 46 * u : 0f, recuo = comIcone ? lado + 10 * u : 0f;
            float topo = 0f, alto = 8 * u;
            for (int i = 0; i < linhas.Count; i++)
            {
                float hh = linhas[i].estilo.CalcHeight(new GUIContent(linhas[i].texto), w - 2 * x - (i < 2 ? recuo : 0f)) + 3 * u;
                if (i < 2) topo += hh;
                else alto += hh;
            }
            topo = Mathf.Max(topo, comIcone ? lado + 6 * u : 0f);
            alto += topo;
            var r = new Rect(mouse.x + 18 * u, mouse.y + 16 * u, w, alto + 6 * u);
            if (r.xMax > Screen.width - 4) r.x = mouse.x - r.width - 10 * u;
            if (r.yMax > Screen.height - 4) r.y = Mathf.Max(4f, Screen.height - 4 - r.height);
            Ret(r, new Color(0.07f, 0.07f, 0.065f, 0.96f));
            Moldura(r, Mathf.Max(1f, Mathf.Round(u)), corDaBorda);
            float y = r.y + 7 * u;
            if (comIcone)
            {
                var h = (Habilidade)_noSobMouse;
                var estado = EstadoDe(h, p);
                IconeNoQuadrado(new Rect(Mathf.Round(r.x + x), Mathf.Round(y + 1 * u), Mathf.Round(lado), Mathf.Round(lado)), h, estado,
                    CorDaArea(Habilidades.AreaDe(h)), Forte(h, p, estado), false, false, false, u);
            }
            for (int i = 0; i < linhas.Count; i++)
            {
                var l = linhas[i];
                float rec = i < 2 ? recuo : 0f;
                var c = new GUIContent(l.texto);
                float hh = l.estilo.CalcHeight(c, w - 2 * x - rec);
                PorCor(l.estilo, l.cor);
                GUI.Label(new Rect(r.x + x + rec, y, w - 2 * x - rec, hh), c, l.estilo);
                y += hh + 3 * u;
                if (i == 1) y = r.y + 7 * u + topo;
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
                if (Habilidades.AreaDe((Habilidade)i) == _areaEscolhida && Em(AlvoDoNo((Habilidade)i)).Contains(m)) return i;
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
