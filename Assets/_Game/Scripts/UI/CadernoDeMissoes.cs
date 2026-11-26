using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O caderno das missões na tela (25/11/2026, a pedido do Pedro: "refazer o layout das missões... hoje ela aparece em cima
    /// do mini mapa... quando completamos, aparecer um risco em cima da missão, talvez poderia ser um layout estilo bloco de
    /// anotações onde iriamos riscando e anotando conforme as missões apareciam e faria um barulho de caneta riscando o
    /// caderno"). Uma folhinha de caderno pautada no canto de cima à esquerda (o canto que a linha da partida deixou), um
    /// pouco torta, com a letra de mão (a mesma das folhas do jogo): o título da fase e só a submissão da vez. Depois, no
    /// mesmo dia (a pedido: "ficou muito grande na tela, vamos reduzir um pouco o tamanho do papel, também deixar na tela do
    /// jogador, somente a missão que irá fazer agora... quando riscar uma missão, mostrar ela riscando e depois
    /// desaparece"), a folha ficou menor e a feita é riscada (o traço corre da esquerda para a direita, com o som da caneta),
    /// fica um instante e some; só então a nova aparece escrita letra por letra (com o som da escrita). No fim de uma fase, o
    /// título também é riscado. Embaixo, o que ajuda agora: a distância até onde ir e, no Dia 0, a carteira e o carrinho
    /// (antes, por cima do minimapa). As feitas e a da vez, todas, ficam na aba ANOTAÇÕES da mochila (o TAB ou o P:
    /// <c>CadernoDeMissoes.Anotacoes.cs</c>). Some com qualquer tela aberta (o inventário, o mapa, a conversa, o ESC), no
    /// cinema e na tela de morte. Em cada máquina, pela <see cref="MundoDeLakewood"/>; o anfitrião manda a fase e a submissão.
    /// </summary>
    public partial class CadernoDeMissoes : MonoBehaviour
    {
        const float LetrasPorSegundo = 38f;
        const float SegundosDoRisco = 0.55f;
        // a feita: riscando, um instante riscada e sumindo; só depois a nova é escrita
        const float SegundosRiscada = 0.8f, SegundosSumindo = 0.35f;
        const float SegundosDaSaida = SegundosDoRisco + SegundosRiscada + SegundosSumindo;
        const float Torto = -1.1f;

        FaseDaCampanha _fase = FaseDaCampanha.Nenhuma, _faseSaindo = FaseDaCampanha.Nenhuma;
        int _passo = -1, _passoSaindo = -1;
        bool _fimDaFase, _somDaEscrita;
        float _escritaDesde = -10f, _riscoDesde = -100f;

        static Font _letra;
        static Texture2D _papel, _risco, _furo;
        static AudioClip[] _sonsRiscar, _sonsEscrever, _sonsPagina;
        static bool _sonsLidos;
        GUIStyle _titulo, _linha, _detalhe;
        float _u;

        public static readonly Color Tinta = new Color(0.12f, 0.17f, 0.42f);
        public static readonly Color TintaFeita = new Color(0.32f, 0.35f, 0.48f, 0.85f);
        public static readonly Color TintaDoRisco = new Color(0.1f, 0.12f, 0.32f, 0.92f);
        public static readonly Color Pauta = new Color(0.52f, 0.66f, 0.86f, 0.55f);
        public static readonly Color Margem = new Color(0.86f, 0.36f, 0.36f, 0.6f);

        static bool NaCampanha(FaseDaCampanha f) => f != FaseDaCampanha.Livre && f != FaseDaCampanha.Nenhuma;

        // ------------------------------------------------------------------ o que mudou (o risco e a escrita)

        void Update()
        {
            var m = MundoDeLakewood.Instancia;
            if (m == null || !m.IsSpawned) return;
            float agora = Time.unscaledTime;
            var f = m.Fase;
            int p = m.Passo;
            if (f != _fase || p != _passo)
            {
                // a que estava escrita sai riscada (se a nova ainda nem foi escrita, a que sai continua a mesma)
                bool escrita = NaCampanha(_fase) && _passo >= 0 && agora >= _escritaDesde;
                if (escrita)
                {
                    _faseSaindo = _fase;
                    _passoSaindo = _passo;
                    _fimDaFase = f != _fase;
                    _riscoDesde = agora;
                    Tocar(Sorteado(SonsRiscar));
                }
                else if (NaCampanha(_faseSaindo) && agora - _riscoDesde < SegundosDaSaida) _fimDaFase |= f != _fase;
                bool saindo = NaCampanha(_faseSaindo) && agora - _riscoDesde < SegundosDaSaida;
                _fase = f;
                _passo = p;
                if (NaCampanha(f))
                {
                    _escritaDesde = saindo ? _riscoDesde + SegundosDaSaida : agora;
                    _somDaEscrita = true;
                }
            }
            if (_somDaEscrita && agora >= _escritaDesde)
            {
                _somDaEscrita = false;
                Tocar(Sorteado(SonsEscrever));
            }
        }

        // ------------------------------------------------------------------ a tela

        static TelaDoInventario _mochila;

        // (25/11/2026: o 1º teste mostrou a folha por trás da mochila aberta) some com qualquer tela aberta
        static bool Escondido()
        {
            var m = MundoDeLakewood.Instancia;
            var j = JogadorRede.Local;
            if (j == null) return true;
            if (_mochila == null || _mochila.gameObject != j.gameObject) _mochila = j.GetComponent<TelaDoInventario>();
            var mapa = MapaDoMundo.Instancia;
            var terminal = TerminalDoBunker.Instancia;
            return MenuOnline.Aberto || MenuOnline.Cinema || TelaDeConversa.Aberta || TelaDeMorte.Aberta || ModoVideo.Ligado
                   || GravacaoNoMundo.SemHud || DiaZeroDaPartida.NaPassagem || DiaZeroDaPartida.CarregandoARua
                   || (m != null && (m.Falhou || m.Chegou))
                   || (_mochila != null && _mochila.Aberta) || (mapa != null && mapa.Aberto) || (terminal != null && terminal.Aberto)
                   || TelaDoCaixa.Aberta || Loot.TelaDoSaque.Aberta || Sobreviventes.TelaDoComerciante.Aberta || Documentos.LeituraDePapel.Aberta
                   || MenuDoDesenvolvedor.AbertoAqui;   // (26/11/2026: o F1 e o F3, que ficavam por baixo da folha)
        }

        // (25/11/2026, a revisão: o OnGUI refazia o texto, a quebra das linhas, os detalhes e as medidas em todo evento, umas
        // 100 alocações por quadro com a folha na tela a campanha inteira) as contas da folha ficam guardadas e são refeitas
        // quando a submissão (ou a que sai riscada) muda, quando a escala muda e, na da vez, a cada quarto de segundo (os
        // números, a distância e a carteira mudam sozinhos)
        const float SegundosEntreAsContas = 0.25f;
        readonly List<string> _linhasDaFolha = new List<string>(), _detalhesDaFolha = new List<string>();
        readonly List<float> _largurasDasLinhas = new List<float>();
        FaseDaCampanha _faseDasContas = FaseDaCampanha.Nenhuma;
        int _passoDasContas = -1;
        bool _contasSaindo;
        float _contasEm = -10f, _uDasContas = -1f, _largDoTitulo;

        void RefazerAsContas(MundoDeLakewood m, FaseDaCampanha fase, int passo, bool saindo, float u, float larguraDoTexto, float agora)
        {
            _faseDasContas = fase;
            _passoDasContas = passo;
            _contasSaindo = saindo;
            _uDasContas = u;
            _contasEm = agora;
            // a da vez (ou a que sai riscada) e o que ajuda
            Quebrar(Texto(fase, passo, m, !saindo), larguraDoTexto, _linha, _linhasDaFolha);
            _detalhesDaFolha.Clear();
            if (!saindo) foreach (var d in Detalhes(m, fase, passo)) Quebrar(d, larguraDoTexto, _detalhe, _detalhesDaFolha, false);
            // as larguras: o título (o sublinhado e o risco) e, na que sai, cada linha (o traço por cima)
            _largDoTitulo = Largura(_titulo, Titulo(fase));
            _largurasDasLinhas.Clear();
            if (saindo) foreach (var l in _linhasDaFolha) _largurasDasLinhas.Add(Largura(_linha, l));
        }

        void OnGUI()
        {
            // (25/11/2026, a revisão) a folha só desenha (GUI.Label e DrawTexture, sem clique nem tecla): só no Repaint
            if (Event.current.type != EventType.Repaint) return;
            var m = MundoDeLakewood.Instancia;
            if (m == null || !m.IsSpawned || Escondido()) return;
            float agora = Time.unscaledTime;
            float t = agora - _riscoDesde;
            bool saindo = NaCampanha(_faseSaindo) && t < SegundosDaSaida;
            FaseDaCampanha fase;
            if (saindo) fase = _faseSaindo;
            else
            {
                if (!NaCampanha(_fase) || _fase != m.Fase || _passo != m.Passo) return;   // (o Update ainda não viu)
                fase = _fase;
            }
            int passo = saindo ? _passoSaindo : _passo;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);

            // (a pedido, "ficou muito grande na tela": 300 de largura no 1º, 236 no 2º e 214 agora, com a letra menor)
            float x0 = 12f * u, y0 = 10f * u, w = 214f * u;
            float margem = 22f * u, larguraDoTexto = w - margem - 9f * u;
            float altura = _linha.lineHeight * 1.12f, alturaDoDetalhe = _detalhe.lineHeight * 1.08f;
            float topo = 27f * u;

            if (saindo != _contasSaindo || fase != _faseDasContas || passo != _passoDasContas || u != _uDasContas
                || (!saindo && agora - _contasEm >= SegundosEntreAsContas))
                RefazerAsContas(m, fase, passo, saindo, u, larguraDoTexto, agora);
            var linhas = _linhasDaFolha;
            var detalhes = _detalhesDaFolha;
            float h = topo + linhas.Count * altura + detalhes.Count * alturaDoDetalhe + 9f * u;
            // sumindo: a folha toda fica transparente
            float alfa = saindo ? 1f - Mathf.Clamp01((t - SegundosDoRisco - SegundosRiscada) / SegundosSumindo) : 1f;
            if (alfa <= 0.001f) return;

            var antes = GUI.matrix;
            var cor = GUI.color;
            GUIUtility.RotateAroundPivot(Torto, new Vector2(x0 + w * 0.5f, y0 + h * 0.5f));
            Folha(new Rect(x0, y0, w, h), topo, altura, margem, u, alfa);

            // o título, sublinhado à mão (riscado no fim da fase)
            float riscoT = saindo ? Mathf.Clamp01(t / SegundosDoRisco) : 0f;
            string titulo = Titulo(fase);
            GUI.color = ComAlfa(Tinta, alfa);
            GUI.Label(new Rect(x0 + margem, y0 + 7f * u, w - margem, 20f * u), titulo, _titulo);
            float largTitulo = _largDoTitulo;
            GUI.color = ComAlfa(new Color(Tinta.r, Tinta.g, Tinta.b, 0.8f), alfa);
            GUI.DrawTexture(new Rect(x0 + margem, y0 + 24f * u, largTitulo, 2.6f * u), Risco);
            if (saindo && _fimDaFase) Riscar(new Rect(x0 + margem - 2f * u, y0 + 7f * u, largTitulo + 4f * u, 20f * u), riscoT, alfa, u);

            // a submissão: escrita letra por letra (a da vez) ou riscando (a feita)
            float yl = y0 + topo;
            int letras = saindo ? int.MaxValue : Mathf.FloorToInt(Mathf.Max(0f, agora - _escritaDesde) * LetrasPorSegundo);
            for (int i = 0; i < linhas.Count; i++)
            {
                string l = linhas[i];
                if (!saindo)
                {
                    if (letras <= 0) break;
                    if (l.Length > letras) l = l.Substring(0, letras);
                    letras -= linhas[i].Length;
                }
                GUI.color = ComAlfa(saindo ? TintaFeita : Tinta, alfa);
                if (i == 0) GUI.Label(new Rect(x0 + 8f * u, yl, margem, altura), saindo ? "x" : "•", _linha);
                GUI.Label(new Rect(x0 + margem, yl, w - margem, altura), l, _linha);
                if (saindo)
                {
                    // o traço corre linha por linha
                    float quanto = Mathf.Clamp01(riscoT * linhas.Count - i);
                    float larg = (i < _largurasDasLinhas.Count ? _largurasDasLinhas[i] : Largura(_linha, linhas[i])) + 4f * u;
                    Riscar(new Rect(x0 + margem - 2f * u, yl, larg, altura), quanto, alfa, u);
                }
                yl += altura;
            }
            // o que ajuda, escrito depois da submissão
            foreach (var d in detalhes)
            {
                if (letras <= 0) break;
                string l = d.Length > letras ? d.Substring(0, letras) : d;
                letras -= d.Length;
                GUI.color = new Color(Tinta.r, Tinta.g, Tinta.b, 0.75f);
                // (26/11/2026, a pedido do Pedro: os textos cortados) a caixa da linha (1,08 × a da letra) cortava a ponta do pé
                // do g da letra de mão ("Pague no caixa"): a caixa é mais alta, e o passo entre as linhas continua o mesmo
                GUI.Label(new Rect(x0 + margem, yl, w - margem, alturaDoDetalhe + 4f * u), l, _detalhe);
                yl += alturaDoDetalhe;
            }
            GUI.color = cor;
            GUI.matrix = antes;
        }

        static Color ComAlfa(Color c, float alfa) => new Color(c.r, c.g, c.b, c.a * alfa);

        /// <summary>A folha pautada: a sombra, o papel, as linhas azuis, a margem vermelha e os furos da espiral em cima.</summary>
        public static void Folha(Rect r, float topo, float altura, float margem, float u, float alfa = 1f)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.3f * alfa);
            GUI.DrawTexture(new Rect(r.x + 3f * u, r.y + 4f * u, r.width, r.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alfa);
            GUI.DrawTexture(r, Papel);
            GUI.color = ComAlfa(Pauta, alfa);
            for (float y = r.y + topo + altura - 3f * u; y < r.yMax - 3f * u; y += altura)
                GUI.DrawTexture(new Rect(r.x + 3f * u, y, r.width - 6f * u, Mathf.Max(1f, 1f * u)), Texture2D.whiteTexture);
            GUI.color = ComAlfa(Margem, alfa);
            GUI.DrawTexture(new Rect(r.x + margem - 5f * u, r.y + 3f * u, Mathf.Max(1f, 1.3f * u), r.height - 6f * u), Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.22f, 0.2f, 0.9f * alfa);
            for (float x = r.x + 11f * u; x < r.xMax - 8f * u; x += 18f * u)
                GUI.DrawTexture(new Rect(x, r.y + 4f * u, 5.5f * u, 5.5f * u), Furo);
        }

        /// <summary>O risco da caneta por cima de uma linha (quanto: de 0 a 1, da esquerda para a direita).</summary>
        public static void Riscar(Rect linha, float quanto, float alfa, float u)
        {
            if (quanto <= 0f) return;
            GUI.color = ComAlfa(TintaDoRisco, alfa);
            GUI.DrawTextureWithTexCoords(new Rect(linha.x, linha.y + linha.height * 0.46f, linha.width * quanto, 4f * u), Risco, new Rect(0f, 0f, quanto, 1f));
        }

        /// <summary>
        /// O que ajuda na submissão da vez: a distância até onde ir, a tentativa da fuga, a carteira e o carrinho do Dia 0
        /// (também na aba ANOTAÇÕES).
        /// </summary>
        public static List<string> Detalhes(MundoDeLakewood m, FaseDaCampanha fase, int passo)
        {
            var l = new List<string>();
            var j = JogadorRede.Local;
            var lw = LakewoodNaCena.Instancia;
            if (j == null) return l;
            Vector3? alvo = null;
            string ate = "";
            switch (fase)
            {
                case FaseDaCampanha.DiaZero:
                {
                    var dz = DiaZeroDaPartida.Instancia;
                    if (dz != null && dz.IsSpawned)
                    {
                        ulong eu = dz.NetworkManager.LocalClientId;
                        int compras = dz.Compras(eu);
                        l.Add($"Carteira: {PrecosDoDiaZero.Dinheiro(dz.Dinheiro(eu))}{(compras > 0 ? $"  ·  {compras} {(compras == 1 ? "compra" : "compras")} na caçamba" : "")}");
                        if (Carrinho.Loja != null && Carrinho.Quantos > 0)
                            l.Add($"Carrinho ({Carrinho.Loja.Nome}): {Carrinho.Quantos} {(Carrinho.Quantos == 1 ? "item" : "itens")}, {PrecosDoDiaZero.Dinheiro(Carrinho.Total)}. Pague no caixa.");
                    }
                    break;
                }
                case FaseDaCampanha.Fuga:
                    if (lw != null && lw.PortaoDoBunker(out var portao)) alvo = portao;
                    ate = "até o portão";
                    if (m.Tentativa > 1) l.Add($"tentativa {m.Tentativa}");
                    break;
                case FaseDaCampanha.RetomeAWoodcreek:
                    if (passo == FaleComOCapitao || passo == ConteAoCapitao) { alvo = LakewoodNaCena.PosicaoDoCapitao(); ate = "até o Capitão"; }
                    else if (passo == VolteAoBunker) { if (lw != null && lw.PortaoDoBunker(out var pb)) alvo = pb; ate = "até o portão do bunker"; }
                    else { alvo = LakewoodNaCena.CasaDaFamiliaNoMundo(); ate = "até a casa"; }
                    break;
            }
            if (alvo.HasValue)
            {
                var d = alvo.Value - j.transform.position;
                d.y = 0f;
                if (d.magnitude > 6f) l.Insert(0, d.magnitude >= 1000f ? $"{d.magnitude / 1000f:0.0} km {ate}" : $"{d.magnitude:0} m {ate}");
            }
            return l;
        }

        /// <summary>A quebra das linhas pela largura (as palavras inteiras).</summary>
        public static List<string> Quebrar(string texto, float largura, GUIStyle estilo)
        {
            var saida = new List<string>();
            Quebrar(texto, largura, estilo, saida, false);
            return saida;
        }

        /// <summary>A quebra das linhas numa lista que já existe (limpa antes, se <paramref name="limpar"/>).</summary>
        public static void Quebrar(string texto, float largura, GUIStyle estilo, List<string> saida, bool limpar = true)
        {
            if (limpar) saida.Clear();
            if (string.IsNullOrEmpty(texto)) return;
            var linha = "";
            foreach (var palavra in texto.Split(' '))
            {
                string tenta = linha.Length == 0 ? palavra : linha + " " + palavra;
                if (linha.Length > 0 && Largura(estilo, tenta) > largura)
                {
                    saida.Add(linha);
                    linha = palavra;
                }
                else linha = tenta;
            }
            if (linha.Length > 0) saida.Add(linha);
        }

        // (25/11/2026, a revisão) a medida de um texto sem um GUIContent novo a cada vez (só na thread da tela, no OnGUI)
        static GUIContent _medida;
        static float Largura(GUIStyle estilo, string texto)
        {
            if (_medida == null) _medida = new GUIContent();
            _medida.text = texto;
            return estilo.CalcSize(_medida).x;
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _titulo = EstiloDeMao(15.5f * u);
            _linha = EstiloDeMao(13.5f * u);
            _detalhe = EstiloDeMao(11.5f * u);
            _detalhe.fontStyle = FontStyle.Italic;
        }

        /// <summary>Um estilo com a letra de mão (Ink Free, a das folhas do jogo), branco (a cor vem do GUI.color).</summary>
        public static GUIStyle EstiloDeMao(float tamanho)
        {
            if (_letra == null) _letra = Font.CreateDynamicFontFromOSFont(new[] { "Ink Free", "Segoe Print", "Comic Sans MS", "Segoe Script" }, 32);
            var e = new GUIStyle(GUI.skin.label) { font = _letra, fontSize = Mathf.Max(8, Mathf.RoundToInt(tamanho)), alignment = TextAnchor.UpperLeft, wordWrap = false };
            e.normal.textColor = Color.white;
            return e;
        }

        // ------------------------------------------------------------------ as texturas feitas aqui

        /// <summary>O papel: amarelado, com uma fibra de leve e as bordas um pouco mais escuras.</summary>
        public static Texture2D Papel
        {
            get
            {
                if (_papel != null) return _papel;
                const int n = 128;
                _papel = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Papel do caderno" };
                var px = new Color32[n * n];
                var r = new System.Random(17);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float borda = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y)) / 10f;
                    float escuro = Mathf.Lerp(0.9f, 1f, Mathf.Clamp01(borda));
                    float fibra = 1f + ((float)r.NextDouble() - 0.5f) * 0.035f;
                    px[y * n + x] = new Color(0.975f * escuro * fibra, 0.952f * escuro * fibra, 0.86f * escuro * fibra, 0.97f);
                }
                _papel.SetPixels32(px);
                _papel.Apply();
                return _papel;
            }
        }

        /// <summary>O risco da caneta: um traço um pouco ondulado, com a ponta mais fina.</summary>
        public static Texture2D Risco
        {
            get
            {
                if (_risco != null) return _risco;
                const int w = 256, h = 16;
                _risco = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Risco de caneta" };
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)(w - 1);
                    float meio = h * 0.5f + Mathf.Sin(t * 9.5f) * 1.4f + Mathf.Sin(t * 23f) * 0.5f + (t - 0.5f) * 2f;
                    float grossura = Mathf.Lerp(1.3f, 2.3f, Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI));
                    for (int y = 0; y < h; y++)
                    {
                        float a = Mathf.Clamp01(grossura - Mathf.Abs(y - meio));
                        px[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                _risco.SetPixels32(px);
                _risco.Apply();
                return _risco;
            }
        }

        /// <summary>O furo da espiral: um círculo.</summary>
        public static Texture2D Furo
        {
            get
            {
                if (_furo != null) return _furo;
                const int n = 32;
                _furo = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Furo do caderno" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.42f - d));
                }
                _furo.SetPixels32(px);
                _furo.Apply();
                return _furo;
            }
        }

        // ------------------------------------------------------------------ os sons da caneta e da página
        // (25/11/2026: do sfxjar, CC0, recortados e limpos em Trabalho-Claude/v17_ajustes/sons_scripts; sem o arquivo, mudo)

        static AudioClip[] SonsRiscar { get { LerSons(); return _sonsRiscar; } }
        static AudioClip[] SonsEscrever { get { LerSons(); return _sonsEscrever; } }
        /// <summary>A página virando (a aba ANOTAÇÕES; o caderno do P, depois).</summary>
        public static AudioClip SomDaPagina { get { LerSons(); return Sorteado(_sonsPagina); } }
        /// <summary>A caneta escrevendo (a "Nova receita", 25/11/2026).</summary>
        public static AudioClip SomDeEscrever => Sorteado(SonsEscrever);

        static void LerSons()
        {
            if (_sonsLidos) return;
            _sonsLidos = true;
            _sonsRiscar = Ler("riscar", 2);
            _sonsEscrever = Ler("escrever", 2);
            _sonsPagina = Ler("pagina", 3);
        }

        static AudioClip[] Ler(string nome, int quantos)
        {
            var l = new List<AudioClip>();
            for (int i = 1; i <= quantos; i++)
            {
                var c = Resources.Load<AudioClip>($"Caderno/{nome}_{i}");
                if (c != null) l.Add(c);
            }
            return l.ToArray();
        }

        static AudioClip Sorteado(AudioClip[] sons) => sons == null || sons.Length == 0 ? null : sons[Random.Range(0, sons.Length)];

        static void Tocar(AudioClip clip)
        {
            var cam = Camera.main;
            if (clip == null || cam == null || Escondido()) return;
            Efeitos.Som(clip, cam.transform.position, 0.55f, false, 10f, CanalDoSom.Avisos);
        }

        /// <summary>
        /// Toca um som do caderno perto de quem joga (a página da aba ANOTAÇÕES), mesmo com a mochila aberta. Mudo no modo de
        /// vídeo; na gravação sem o HUD, a caneta da "Nova receita" e do "Anotado no caderno" também (o aviso não aparece).
        /// </summary>
        public static void TocarAqui(AudioClip clip, float volume = 0.5f)
        {
            var cam = Camera.main;
            if (clip == null || cam == null) return;
            // (25/11/2026, a revisão: nos filmes, o jogador do filme e os figurantes começam com o caderno vazio, e o primeiro
            // zumbi de cada tipo rabiscava a caneta no meio da tomada, dentro do WAV; o Efeitos.Som não cala o canal Avisos.)
            // No modo de vídeo, nada do caderno aparece; gravando sem o HUD, a mochila ainda abre (a página pode virar), mas o
            // aviso que a caneta acompanha some (InventarioJogador.OnGUI), então ela fica calada também.
            if (ModoVideo.Ligado) return;
            if (GravacaoNoMundo.SemHud && System.Array.IndexOf(SonsEscrever, clip) >= 0) return;
            Efeitos.Som(clip, cam.transform.position, volume, false, 10f, CanalDoSom.Avisos);
        }
    }
}
