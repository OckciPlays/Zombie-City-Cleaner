using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// A tela das cartas (04/11/2026, a ideia do amigo do usuário, no lugar da tela como a do RimWorld; o
    /// Plano_NPCs_Bunker.md da pasta de arte): o mundo sorteia 52 NPCs (<see cref="NpcsDoMundo"/>), e o anfitrião escolhe
    /// os 5 que começam no bunker numa rodada de cartas por vez: 5 cartas no meio da tela, ele escolhe uma, saem mais 5, até
    /// completar os 5. O parente da família está sempre na primeira mão (com a fita da família; se ficar de fora, vai para
    /// o mapa). Quem não for escolhido fica no mapa, para ser descoberto depois.
    /// Nas rodadas, a tela é limpa (05/11/2026, a pedido: "somente as cartas aparecendo mesmo na tela, sem o resto"):
    ///  - no meio, as cartas: o retrato (o <see cref="EstudioDoMorador"/>; a escolhida gira devagar), a fita da família, o
    ///    laço que importa (com quem já está no bunker, nesta mão ou lá fora), o nome, a idade e o ofício, as 3 melhores
    ///    habilidades com as chamas, os traços e as condições;
    ///  - embaixo, no meio, o botão de escolher e as 5 marquinhas das rodadas.
    /// A ficha (as histórias, as 8 habilidades, os traços e as condições com o efeito no mouse, o que ele não faz e os laços,
    /// com onde o outro está e a opinião) só aparece na revisão do fim, embaixo dos 5 escolhidos, com o mudar o nome e o
    /// botão de criar o mundo no meio.
    /// Teclas: de 1 a 5 escolhem a carta, Enter confirma (clicar duas vezes também), ESC ou Backspace desfazem a última
    /// escolha (sem nenhuma, o ESC volta para o menu). OnGUI, no estilo da tela de criar o personagem (os painéis escuros, o
    /// destaque vermelho e a Bahnschrift). Quem a desenha é o <see cref="MenuOnline"/>.
    /// </summary>
    public class TelaDosMoradores
    {
        public enum Saida { Nada, Voltar, Criar }

        static readonly Color Fundo = new Color(0.075f, 0.07f, 0.068f);
        static readonly Color Painel = new Color(0.105f, 0.1f, 0.098f, 0.94f);
        static readonly Color Borda = new Color(0.2f, 0.19f, 0.18f, 1f);
        static readonly Color Vermelho = new Color(0.72f, 0.14f, 0.15f, 1f);
        static readonly Color Ambar = new Color(0.9f, 0.62f, 0.2f, 1f);
        static readonly Color Claro = new Color(0.9f, 0.87f, 0.82f, 1f);
        static readonly Color Apagado = new Color(0.55f, 0.53f, 0.5f, 1f);
        static readonly Color Barra = new Color(0.52f, 0.68f, 0.42f, 1f);
        static readonly Color FundoDaBarra = new Color(0.2f, 0.2f, 0.19f, 1f);
        static readonly Color Bom = new Color(0.55f, 0.78f, 0.45f, 1f);
        static readonly Color Ruim = new Color(0.9f, 0.42f, 0.38f, 1f);
        static readonly Color ChipDeTraco = new Color(0.16f, 0.15f, 0.14f, 1f);
        static readonly Color ChipDeCondicao = new Color(0.2f, 0.12f, 0.11f, 1f);
        static readonly Color FundoDaCarta = new Color(0.12f, 0.115f, 0.11f, 1f);
        static readonly Color FundoDoRetrato = new Color(0.17f, 0.16f, 0.15f, 1f);

        // as medidas, em unidades de 1280 × 720 (a tela é desenhada no meio, pela unidade): nas rodadas, as cartas e o botão;
        // na revisão, as cartas mais em cima, a ficha e o botão
        const float LarguraDaFileira = 1130f, CartaL = 210f, CartaA = 346f, RetratoA = 188f,
                    TopoDasCartas = 130f, TopoDoBotao = 520f,
                    TopoDasCartasNaRevisao = 76f, TopoDaFicha = 432f, AlturaDaFicha = 194f, TopoDoBotaoNaRevisao = 640f,
                    LarguraDoBotao = 340f, AlturaDoBotao = 56f;

        /// <summary>Os NPCs do mundo na tela (ficam entre uma abertura e outra, até <see cref="Esquecer"/>).</summary>
        public NpcsDoMundo Npcs { get; private set; }
        public bool Aberta { get; private set; }
        /// <summary>O texto do botão de seguir ("CRIAR O MUNDO", "CRIAR LOCAL" ou "JOGAR").</summary>
        public string TextoDoBotao = "CRIAR O MUNDO";
        /// <summary>Os ids escolhidos, na ordem (os do bunker).</summary>
        public IReadOnlyList<int> Escolhidos => _escolhidos;
        /// <summary>Os 5 já foram escolhidos (a revisão).</summary>
        public bool Completo => Npcs != null && _escolhidos.Count >= SorteioDosMoradores.MoradoresNoComeco;
        /// <summary>Os retratos da fileira de agora já foram tirados (para os testes).</summary>
        public bool RetratosProntos => Fileira().TrueForAll(f => _prontos.Contains(f.id));

        readonly System.Random _acaso = new System.Random();
        readonly List<int> _escolhidos = new List<int>();
        readonly Dictionary<int, RenderTexture> _retratos = new Dictionary<int, RenderTexture>();
        readonly HashSet<int> _prontos = new HashSet<int>();
        EstudioDoMorador _estudio;
        FichaDoMorador _tirando;        // o retrato pedido no quadro de antes (fica pronto no fim dele)
        List<FichaDoMorador> _fileira;
        int _fileiraDe = -1;            // de quantos escolhidos a fileira é (a rodada, ou 5 na revisão)
        int _selecionada = -1;          // a carta escolhida da fileira (-1: nenhuma)
        float _quandoAFileiraMudou;
        bool _editando, _voltar;        // _voltar: o ESC sem nenhuma escolha (o Desenhar devolve o Voltar)
        string _nomeAntes, _dica;
        [System.NonSerialized] Texture2D _brilho, _chama;
        [System.NonSerialized] Font _fonte;
        GUIStyle _titulo, _rotulo, _nome, _nomeGrande, _texto, _frase, _pequeno, _botao, _botaoGrande, _chip, _campo, _nomeDaCarta, _numero;
        float _uDosEstilos;

        /// <summary>Abre a tela com os NPCs da última vez ou com os de um mundo sorteado agora.</summary>
        public void Abrir()
        {
            if (Npcs == null)
            {
                Npcs = SorteioDosMoradores.SortearMundo(_acaso.Next(1, int.MaxValue));
                _escolhidos.Clear();
            }
            _editando = false;
            _voltar = false;
            _fileiraDe = -1;
            var recursos = RecursosDosMoradores.Carregar();
            if (recursos == null || recursos.npc == null)
                Debug.LogWarning("[Moradores] Sem o NPC das roupas (Settings/Resources/RecursosDosMoradores): rode Zombie City Cleaner > Moradores > Montar a tela.");
            _estudio = new EstudioDoMorador(recursos != null ? recursos.npc : null);
            Aberta = true;
        }

        /// <summary>Fecha a tela (os NPCs e os escolhidos ficam, para a volta).</summary>
        public void Fechar()
        {
            Aberta = false;
            _editando = false;
            _estudio?.Destruir();
            _estudio = null;
            _tirando = null;
            foreach (var rt in _retratos.Values)
                if (rt != null)
                {
                    rt.Release();
                    Object.Destroy(rt);
                }
            _retratos.Clear();
            _prontos.Clear();
        }

        /// <summary>Esquece os NPCs: a próxima abertura sorteia outro mundo.</summary>
        public void Esquecer()
        {
            Npcs = null;
            _escolhidos.Clear();
        }

        /// <summary>Os do bunker (os escolhidos, na ordem) e os do mapa (o resto).</summary>
        public (List<FichaDoMorador> bunker, List<FichaDoMorador> mapa) Separar() => SorteioDosMoradores.Separar(Npcs, _escolhidos);

        // a fileira de cartas de agora: a mão da rodada, ou os 5 escolhidos na revisão
        List<FichaDoMorador> Fileira()
        {
            if (Npcs == null) return new List<FichaDoMorador>();
            int de = _escolhidos.Count;
            if (_fileira == null || _fileiraDe != de)
            {
                _fileira = Completo ? _escolhidos.Select(Npcs.DoId).Where(f => f != null).ToList() : SorteioDosMoradores.Mao(Npcs, de);
                _fileiraDe = de;
                _quandoAFileiraMudou = Time.realtimeSinceStartup;
                if (_selecionada >= _fileira.Count) _selecionada = -1;
            }
            return _fileira;
        }

        FichaDoMorador Selecionada
        {
            get
            {
                var fileira = Fileira();
                return _selecionada >= 0 && _selecionada < fileira.Count ? fileira[_selecionada] : null;
            }
        }

        // ------------------------------------------------------------------ escolher

        /// <summary>Escolhe a carta selecionada (ela vai para o bunker, e sai a próxima mão).</summary>
        public void Escolher()
        {
            var f = Selecionada;
            if (f == null || Completo || _escolhidos.Contains(f.id)) return;
            _escolhidos.Add(f.id);
            _selecionada = Completo ? _escolhidos.Count - 1 : -1;
            _editando = false;
        }

        /// <summary>Desfaz a última escolha (volta para a mão dela, com a carta desfeita selecionada).</summary>
        public void Desfazer()
        {
            if (_escolhidos.Count == 0) return;
            int id = _escolhidos[_escolhidos.Count - 1];
            _escolhidos.RemoveAt(_escolhidos.Count - 1);
            _editando = false;
            _selecionada = Fileira().FindIndex(f => f.id == id);
        }

        /// <summary>Para os testes (o MCP): seleciona a carta (de 0 a 4) da fileira.</summary>
        public void SelecionarParaTeste(int carta, bool editando = false)
        {
            _selecionada = Mathf.Clamp(carta, 0, Fileira().Count - 1);
            _editando = editando && Completo;
            if (_editando) _nomeAntes = Selecionada.nome;
        }

        // ------------------------------------------------------------------ a cada quadro

        /// <summary>A cada quadro (do menu), com a tela aberta: os retratos (um por quadro), a escolhida girando e as teclas.</summary>
        public void Atualizar()
        {
            if (!Aberta || _estudio == null || Npcs == null) return;
            var fileira = Fileira();
            // o retrato pedido no quadro de antes já foi tirado
            if (_tirando != null)
            {
                _prontos.Add(_tirando.id);
                _tirando = null;
            }
            // o próximo que falta: os da fileira, depois os do bunker (as miniaturas em cima)
            var falta = fileira.Concat(_escolhidos.Select(Npcs.DoId)).FirstOrDefault(f => f != null && !_prontos.Contains(f.id));
            if (falta != null)
            {
                _estudio.Retratar(falta.aparencia, Retrato(falta.id), -18f);
                _tirando = falta;
            }
            else if (Selecionada != null)
                _estudio.Retratar(Selecionada.aparencia, Retrato(Selecionada.id), -18f + 26f * Mathf.Sin(Time.realtimeSinceStartup * 0.9f));
            else _estudio.Parar();

            var teclado = Keyboard.current;
            if (teclado == null) return;
            if (_editando)
            {
                if (teclado.escapeKey.wasPressedThisFrame) TerminarONome(Selecionada);
                return;
            }
            // o ESC e o Backspace desfazem a última escolha; sem nenhuma, o ESC volta para o menu
            if (teclado.escapeKey.wasPressedThisFrame || teclado.backspaceKey.wasPressedThisFrame)
            {
                if (_escolhidos.Count > 0) Desfazer();
                else if (teclado.escapeKey.wasPressedThisFrame) _voltar = true;
                return;
            }
            for (int k = 0; k < fileira.Count && k < 5; k++)
                if (Tecla(teclado, k).wasPressedThisFrame) _selecionada = k;
            if ((teclado.enterKey.wasPressedThisFrame || teclado.numpadEnterKey.wasPressedThisFrame) && !Completo) Escolher();
        }

        // o fim da edição do nome (o OK ou o ESC): sem o nome, volta o de antes
        void TerminarONome(FichaDoMorador f)
        {
            _editando = false;
            if (f == null) return;
            if (string.IsNullOrWhiteSpace(f.nome)) f.nome = _nomeAntes;
            f.nome = f.nome.Trim();
            f.apelido = (f.apelido ?? "").Trim();
            f.sobrenome = (f.sobrenome ?? "").Trim();
        }

        static UnityEngine.InputSystem.Controls.KeyControl Tecla(Keyboard t, int k) => k switch
        {
            0 => t.digit1Key, 1 => t.digit2Key, 2 => t.digit3Key, 3 => t.digit4Key, _ => t.digit5Key,
        };

        RenderTexture Retrato(int id)
        {
            if (!_retratos.TryGetValue(id, out var rt) || rt == null) _retratos[id] = rt = EstudioDoMorador.NovaImagem();
            return rt;
        }

        static float Unidade() => Mathf.Min(Screen.height / 720f, Screen.width / 1280f);

        // ------------------------------------------------------------------ o desenho

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            if (_fonte == null) _fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 16);
            GUIStyle Estilo(GUIStyle de, int tamanho, FontStyle jeito, Color cor, TextAnchor ancora, bool quebra = false)
            {
                var s = new GUIStyle(de) { font = _fonte, fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = jeito, alignment = ancora, wordWrap = quebra };
                s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = cor;
                return s;
            }
            _titulo = Estilo(GUI.skin.label, 34, FontStyle.Bold, Claro, TextAnchor.MiddleLeft);
            _rotulo = Estilo(GUI.skin.label, 13, FontStyle.Bold, Claro, TextAnchor.UpperLeft);
            _nome = Estilo(GUI.skin.label, 14, FontStyle.Bold, Claro, TextAnchor.UpperLeft);
            _nomeGrande = Estilo(GUI.skin.label, 21, FontStyle.Bold, Claro, TextAnchor.MiddleLeft);
            _nomeDaCarta = Estilo(GUI.skin.label, 15, FontStyle.Bold, Claro, TextAnchor.UpperLeft, true);
            _texto = Estilo(GUI.skin.label, 13, FontStyle.Normal, Apagado, TextAnchor.UpperLeft);
            _frase = Estilo(GUI.skin.label, 12, FontStyle.Normal, Claro, TextAnchor.UpperLeft, true);
            _pequeno = Estilo(GUI.skin.label, 12, FontStyle.Normal, Apagado, TextAnchor.UpperLeft, true);
            _botao = Estilo(GUI.skin.label, 15, FontStyle.Bold, Claro, TextAnchor.MiddleCenter);
            _botaoGrande = Estilo(GUI.skin.label, 21, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            _chip = Estilo(GUI.skin.label, 12, FontStyle.Bold, Claro, TextAnchor.MiddleCenter);
            _numero = Estilo(GUI.skin.label, 26, FontStyle.Bold, new Color(0.32f, 0.3f, 0.28f), TextAnchor.MiddleCenter);
            _campo = Estilo(GUI.skin.textField, 15, FontStyle.Bold, Claro, TextAnchor.MiddleLeft);
            _campo.normal.background = _campo.focused.background = _campo.hover.background = Texture2D.blackTexture;
            _campo.padding = new RectOffset(Mathf.RoundToInt(6 * u), 2, 2, 2);
        }

        /// <summary>Desenha a tela (no OnGUI do menu); devolve se o jogador voltou ou mandou criar.</summary>
        public Saida Desenhar()
        {
            if (!Aberta || Npcs == null) return Saida.Nada;
            float u = Unidade();
            Estilos(u);
            var saida = Saida.Nada;
            if (_voltar)
            {
                _voltar = false;
                saida = Saida.Voltar;
            }
            // a tela de 1280 × 720 no meio (numa tela mais alta que 16:9, sobra em cima e embaixo)
            float y0 = (Screen.height - 720f * u) / 2f;
            float x0 = Screen.width / 2f - LarguraDaFileira / 2f * u;
            float topo = y0 + (Completo ? TopoDasCartasNaRevisao : TopoDasCartas) * u;
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), Fundo);
            _brilho ??= Brilho();
            _chama ??= Chama();
            GUI.color = new Color(1f, 0.82f, 0.6f, 0.16f);
            float lb = Screen.width * 0.8f;
            GUI.DrawTexture(new Rect((Screen.width - lb) / 2f, topo + CartaA / 2f * u - lb / 2f, lb, lb), _brilho);
            GUI.color = Color.white;
            _dica = null;

            var fileira = Fileira();
            if (Completo)
            {
                // a revisão: os 5 do bunker, a ficha do escolhido e o botão de criar
                GUI.Label(new Rect(x0, y0 + 10 * u, 700 * u, 40 * u), "O BUNKER ESTÁ COMPLETO", _titulo);
                Cartas(x0, topo, fileira, u);
                Ficha(new Rect(x0, y0 + TopoDaFicha * u, LarguraDaFileira * u, AlturaDaFicha * u), u);
            }
            else Cartas(x0, topo, fileira, u);

            // embaixo, no meio: escolher (ou criar) e, nas rodadas, as marquinhas
            float yb = y0 + (Completo ? TopoDoBotaoNaRevisao : TopoDoBotao) * u;
            var botao = new Rect(Screen.width / 2f - LarguraDoBotao / 2f * u, yb, LarguraDoBotao * u, AlturaDoBotao * u);
            var sel = Selecionada;
            bool pode = Completo ? !_editando && _escolhidos.TrueForAll(id => !string.IsNullOrWhiteSpace(Npcs.DoId(id)?.nome)) : sel != null;
            string texto = Completo ? TextoDoBotao : sel != null ? $"ESCOLHER {Maiusculo(sel.nome)}" : "ESCOLHA UMA CARTA";
            var estiloDoBotao = _botaoGrande;
            if (estiloDoBotao.CalcSize(new GUIContent(texto)).x > botao.width - 24 * u)
                estiloDoBotao = new GUIStyle(_botaoGrande) { fontSize = Mathf.RoundToInt(17 * u) };
            Retangulo(botao, pode ? Vermelho : new Color(0.3f, 0.12f, 0.12f, 1f));
            Moldura(botao, pode ? new Color(0.95f, 0.35f, 0.33f) : Borda, u);
            var antes = GUI.enabled;
            GUI.enabled = antes && pode;
            if (GUI.Button(botao, Caber(texto, estiloDoBotao, botao.width - 24 * u), estiloDoBotao))
            {
                if (Completo) saida = Saida.Criar;
                else Escolher();
            }
            GUI.enabled = antes;
            if (!Completo) Rodadas(Screen.width / 2f, botao.yMax + 18 * u, u);

            Dica(u);
            return saida;
        }

        // as 5 marquinhas das rodadas: as já escolhidas cheias, a de agora com a borda clara, as que faltam apagadas
        void Rodadas(float meio, float y, float u)
        {
            const float lado = 9f, vao = 9f;
            int n = SorteioDosMoradores.MoradoresNoComeco;
            float largura = (n * lado + (n - 1) * vao) * u;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(meio - largura / 2f + i * (lado + vao) * u, y, lado * u, lado * u);
                if (i < _escolhidos.Count) Retangulo(r, Vermelho);
                else Moldura(r, i == _escolhidos.Count ? Claro : Borda, i == _escolhidos.Count ? 1.5f * u : u);
            }
        }

        static string Maiusculo(string s) => (s ?? "").ToUpperInvariant();

        // o texto que cabe na largura (cortado com as reticências)
        static string Caber(string texto, GUIStyle estilo, float largura)
        {
            if (string.IsNullOrEmpty(texto) || estilo.CalcSize(new GUIContent(texto)).x <= largura) return texto;
            for (int n = texto.Length - 1; n > 1; n--)
            {
                string t = texto.Substring(0, n).TrimEnd() + "…";
                if (estilo.CalcSize(new GUIContent(t)).x <= largura) return t;
            }
            return texto.Substring(0, 1) + "…";
        }

        // ------------------------------------------------------------------ no meio: as cartas

        void Cartas(float x0, float topo, List<FichaDoMorador> fileira, float u)
        {
            float vao = (LarguraDaFileira - fileira.Count * CartaL) / Mathf.Max(1, fileira.Count - 1);
            if (fileira.Count < 5) vao = 16f;
            float largura = fileira.Count * CartaL + (fileira.Count - 1) * vao;
            float xi = x0 + (LarguraDaFileira - largura) / 2f * u;
            var e = Event.current;
            for (int k = 0; k < fileira.Count; k++)
            {
                // a mão chegando: as cartas sobem uma depois da outra
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - _quandoAFileiraMudou - k * 0.06f) / 0.3f);
                if (t <= 0f) continue;
                float sobe = (1f - t) * (1f - t) * 50f;
                var r = new Rect(xi + k * (CartaL + vao) * u, topo + sobe * u, CartaL * u, CartaA * u);
                bool sobre = r.Contains(e.mousePosition), selecionada = _selecionada == k;
                if (selecionada) r.y -= 10 * u;
                else if (sobre) r.y -= 4 * u;
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    _selecionada = k;
                    _editando = false;
                    if (e.clickCount == 2 && !Completo) Escolher();
                    e.Use();
                }
                Carta(r, fileira[k], selecionada, sobre, u);
            }
        }

        void Carta(Rect r, FichaDoMorador f, bool selecionada, bool sobre, float u)
        {
            bool familia = f.parente != ParenteDaFamilia.Nenhum;
            Retangulo(new Rect(r.x + 5 * u, r.y + 7 * u, r.width, r.height), new Color(0f, 0f, 0f, 0.4f));
            Retangulo(r, FundoDaCarta);
            // o retrato, com a luz atrás
            var retrato = new Rect(r.x + 6 * u, r.y + 6 * u, r.width - 12 * u, RetratoA * u - 6 * u);
            Retangulo(retrato, FundoDoRetrato);
            GUI.color = familia ? new Color(1f, 0.75f, 0.35f, 0.32f) : new Color(1f, 0.9f, 0.8f, 0.14f);
            GUI.DrawTexture(new Rect(retrato.center.x - retrato.width * 0.6f, retrato.y - retrato.height * 0.1f, retrato.width * 1.2f, retrato.height * 1.2f), _brilho);
            GUI.color = Color.white;
            if (_prontos.Contains(f.id) && _retratos.TryGetValue(f.id, out var rt) && rt != null) GUI.DrawTexture(retrato, rt, ScaleMode.ScaleAndCrop, true);
            else GUI.Label(retrato, "...", new GUIStyle(_numero) { fontSize = Mathf.RoundToInt(18 * u) });

            // a fita da família e o laço que importa, em cima do retrato
            if (familia)
            {
                var fita = new Rect(retrato.x, retrato.y + 8 * u, 86 * u, 20 * u);
                Retangulo(fita, Ambar);
                GUI.Label(fita, "FAMÍLIA", new GUIStyle(_chip) { normal = { textColor = new Color(0.12f, 0.08f, 0.04f) } });
                if (fita.Contains(Event.current.mousePosition))
                    _dica = $"{SorteioDosMoradores.TextoDoParente(f)}. Se ficar de fora, vai para o mapa: a família vai ter que encontrá-l{(f.mulher ? "a" : "o")}.";
            }
            var (laco, cor) = LacoDaCarta(f);
            if (laco != null)
            {
                var faixa = new Rect(retrato.x, retrato.yMax - 22 * u, retrato.width, 22 * u);
                Retangulo(faixa, new Color(0.04f, 0.04f, 0.04f, 0.82f));
                GUI.Label(faixa, laco, new GUIStyle(_chip) { fontSize = Mathf.RoundToInt(11 * u), normal = { textColor = cor } });
            }

            // o nome, a idade e o ofício
            float x = r.x + 10 * u, w = r.width - 20 * u, y = r.y + RetratoA * u + 4 * u;
            GUI.Label(new Rect(x, y, w, 36 * u), f.NomeCompleto, _nomeDaCarta);
            y += 36 * u;
            var estiloDaIdade = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(12 * u) };
            GUI.Label(new Rect(x, y, w, 17 * u), Caber($"{f.idade} anos · {SorteioDosMoradores.NomeDeAntes(f)}", estiloDaIdade, w), estiloDaIdade);
            y += 20 * u;

            // as 3 melhores habilidades
            var melhores = Enumerable.Range(0, ListasDosMoradores.Habilidades).Where(h => !f.Incapaz((Habilidade)h))
                .OrderByDescending(h => f.habilidades[h] + (f.paixoes[h] == Paixao.Apaixonado ? 0.6f : f.paixoes[h] == Paixao.Interessado ? 0.3f : 0f))
                .Take(3).ToList();
            var claroPequeno = new GUIStyle(_texto) { fontSize = Mathf.RoundToInt(12 * u), normal = { textColor = Claro } };
            foreach (int h in melhores)
            {
                GUI.Label(new Rect(x, y, 74 * u, 16 * u), ListasDosMoradores.NomesDasHabilidades[h], claroPequeno);
                float bx = x + 74 * u, bw = 62 * u;
                Retangulo(new Rect(bx, y + 5 * u, bw, 7 * u), FundoDaBarra);
                if (f.habilidades[h] > 0) Retangulo(new Rect(bx, y + 5 * u, bw * f.habilidades[h] / 20f, 7 * u), Barra);
                GUI.Label(new Rect(bx + bw + 5 * u, y, 22 * u, 16 * u), f.habilidades[h].ToString(), new GUIStyle(_nome) { fontSize = Mathf.RoundToInt(12 * u) });
                int chamas = f.paixoes[h] == Paixao.Apaixonado ? 2 : f.paixoes[h] == Paixao.Interessado ? 1 : 0;
                for (int c = 0; c < chamas; c++) GUI.DrawTexture(new Rect(bx + bw + 26 * u + c * 10 * u, y + 2 * u, 9 * u, 12 * u), _chama);
                y += 16 * u;
            }

            // os traços e as condições, numa linha que quebra
            var partes = f.tracos.Select(t => ListasDosMoradores.Traco(t)?.Nome(f.mulher) ?? t).ToList();
            string tracos = string.Join(" · ", partes);
            string condicoes = string.Join(" · ", f.condicoes.Select(c => ListasDosMoradores.Condicao(c)?.nome ?? c));
            string linha = condicoes.Length > 0 ? $"{tracos}  <color=#E66B61>{condicoes}</color>" : tracos;
            GUI.Label(new Rect(x, y + 2 * u, w, r.yMax - y - 6 * u), linha,
                      new GUIStyle(_pequeno) { richText = true, fontSize = Mathf.RoundToInt(11 * u), clipping = TextClipping.Clip });

            // a borda: a escolhida em vermelho, a da família em âmbar
            Moldura(r, selecionada ? Vermelho : familia ? Ambar : sobre ? Apagado : Borda, selecionada ? 3f * u : familia ? 2f * u : u);
        }

        // o laço que importa na carta: com quem já está no bunker (verde), com alguém desta mão (âmbar) ou com alguém lá fora
        (string, Color) LacoDaCarta(FichaDoMorador f)
        {
            string melhor = null;
            Color cor = Apagado;
            int nivel = 0;
            var fileira = Fileira();
            foreach (var l in f.lacos)
            {
                var o = Npcs.DoId(l.outro);
                if (o == null) continue;
                string palavra = SorteioDosMoradores.PalavraDoLaco(l.tipo, f.mulher);
                if (_escolhidos.Contains(o.id) && !Completo)
                {
                    if (nivel < 3) { melhor = $"{palavra} {(o.mulher ? "da" : "do")} {o.nome}, no bunker"; cor = Bom; nivel = 3; }
                }
                else if (Completo && _escolhidos.Contains(o.id))
                {
                    if (nivel < 3) { melhor = $"{palavra} {(o.mulher ? "da" : "do")} {o.nome}"; cor = Bom; nivel = 3; }
                }
                else if (fileira.Contains(o))
                {
                    if (nivel < 2) { melhor = $"{palavra} {(o.mulher ? "da" : "do")} {o.nome}, nesta mão"; cor = Ambar; nivel = 2; }
                }
                else if (nivel < 1)
                {
                    melhor = $"tem {Artigo(l.tipo, o.mulher)} {PalavraDoOutro(l.tipo, o.mulher)} lá fora";
                    cor = Apagado;
                    nivel = 1;
                }
            }
            return (melhor, cor);
        }

        // o que o outro é para este ("irmã", "pai"), pelo laço (o que o outro é) e pelo sexo do outro
        static string PalavraDoOutro(string tipo, bool mulher) => SorteioDosMoradores.PalavraDoLaco(SorteioDosMoradores.Inverso(tipo), mulher);
        static string Artigo(string tipo, bool mulher) => tipo == LacoDoMorador.Ex ? (mulher ? "uma" : "um") : mulher ? "uma" : "um";

        // ------------------------------------------------------------------ embaixo: a ficha da carta escolhida

        void Ficha(Rect painel, float u)
        {
            Retangulo(painel, Painel);
            Moldura(painel, Borda, u);
            var f = Selecionada;
            if (f == null)
            {
                GUI.Label(painel, Completo ? "Clique numa carta para ver a ficha." : "Clique numa carta para ver a ficha dela (ou passe o mouse nas fichinhas).",
                          new GUIStyle(_texto) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            float pad = 14 * u, y0 = painel.y + 10 * u;
            float xa = painel.x + pad, wa = 390 * u;
            float xb = xa + wa + 16 * u, wb = 290 * u;
            float xc = xb + wb + 16 * u, wc = painel.xMax - pad - xc;

            // a coluna do nome e das histórias
            float y = y0;
            if (_editando)
            {
                float c1 = wa * 0.34f, c2 = wa * 0.24f, c3 = wa - c1 - c2 - 62 * u;
                GUI.Label(new Rect(xa, y, c1, 16 * u), "NOME", _pequeno);
                GUI.Label(new Rect(xa + c1 + 4 * u, y, c2, 16 * u), "APELIDO", _pequeno);
                GUI.Label(new Rect(xa + c1 + c2 + 8 * u, y, c3, 16 * u), "SOBRENOME", _pequeno);
                f.nome = Campo(new Rect(xa, y + 16 * u, c1, 26 * u), f.nome, 14, u);
                f.apelido = Campo(new Rect(xa + c1 + 4 * u, y + 16 * u, c2, 26 * u), f.apelido, 12, u);
                f.sobrenome = Campo(new Rect(xa + c1 + c2 + 8 * u, y + 16 * u, c3, 26 * u), f.sobrenome, 16, u);
                if (Botao(new Rect(xa + wa - 50 * u, y + 16 * u, 50 * u, 26 * u), "OK", u)) TerminarONome(f);
                y += 46 * u;
            }
            else
            {
                GUI.Label(new Rect(xa, y, wa - (Completo ? 112 * u : 0), 30 * u), f.NomeCompleto, _nomeGrande);
                if (Completo && Botao(new Rect(xa + wa - 108 * u, y + 3 * u, 108 * u, 24 * u), "MUDAR O NOME", u, null, _pequeno))
                {
                    _editando = true;
                    _nomeAntes = f.nome;
                }
                y += 30 * u;
            }
            GUI.Label(new Rect(xa, y, wa, 18 * u), SorteioDosMoradores.TextoDaIdade(f), _texto);
            y += 22 * u;
            y = Historia(xa, y, wa, $"ANTES · {SorteioDosMoradores.NomeDeAntes(f)}", SorteioDosMoradores.FraseDeAntes(f), u);
            Historia(xa, y, wa, $"NOS ANOS DO APOCALIPSE · {SorteioDosMoradores.NomeDeDepois(f)}", SorteioDosMoradores.FraseDeDepois(f), u);

            // as habilidades: o nome, a barra (de 0 a 20), o valor e as chamas
            y = y0 + 2 * u;
            float linha = 21 * u, barraX = xb + 92 * u, barraW = 140 * u;
            for (int k = 0; k < ListasDosMoradores.Habilidades; k++)
            {
                var hab = (Habilidade)k;
                bool incapaz = f.Incapaz(hab);
                var estilo = new GUIStyle(_texto) { normal = { textColor = incapaz ? new Color(0.4f, 0.38f, 0.36f) : Claro } };
                GUI.Label(new Rect(xb, y, 90 * u, linha), ListasDosMoradores.NomesDasHabilidades[k], estilo);
                Retangulo(new Rect(barraX, y + 5 * u, barraW, 9 * u), FundoDaBarra);
                if (!incapaz && f.habilidades[k] > 0) Retangulo(new Rect(barraX, y + 5 * u, barraW * f.habilidades[k] / 20f, 9 * u), Barra);
                GUI.Label(new Rect(barraX + barraW + 6 * u, y, 26 * u, linha), incapaz ? "—" : f.habilidades[k].ToString(), new GUIStyle(_nome) { fontSize = Mathf.RoundToInt(13 * u) });
                int chamas = f.paixoes[k] == Paixao.Apaixonado ? 2 : f.paixoes[k] == Paixao.Interessado ? 1 : 0;
                for (int c = 0; c < chamas; c++) GUI.DrawTexture(new Rect(barraX + barraW + 32 * u + c * 12 * u, y + 2 * u, 10 * u, 14 * u), _chama);
                if (new Rect(xb, y, wb, linha).Contains(Event.current.mousePosition))
                    _dica = incapaz ? "Não faz isso (veja o NÃO FAZ)."
                          : chamas == 2 ? "Apaixonado: aprende uma vez e meia mais rápido e fica feliz fazendo isso (+8)."
                          : chamas == 1 ? "Interessado: aprende no ritmo normal e fica feliz fazendo isso (+4)."
                          : "Sem paixão: aprende devagar (um terço do ritmo).";
                y += linha;
            }

            // os traços, as condições, o que ele não faz e os laços
            y = y0;
            var tracos = f.tracos.Select(t => { var d = ListasDosMoradores.Traco(t); return (d != null ? d.Nome(f.mulher) : t, d?.efeito); }).ToList();
            y = Fichinhas(xc, y, wc, "TRAÇOS", tracos, ChipDeTraco, u);
            var condicoes = f.condicoes.Select(c => { var d = ListasDosMoradores.Condicao(c); return (d != null ? d.nome : c, d?.efeito); }).ToList();
            y = Fichinhas(xc, y, wc, "CONDIÇÕES", condicoes, ChipDeCondicao, u);
            var claro = new GUIStyle(_texto) { normal = { textColor = Claro } };
            GUI.Label(new Rect(xc, y, 80 * u, 18 * u), "NÃO FAZ", _rotulo);
            GUI.Label(new Rect(xc + 80 * u, y, wc - 80 * u, 18 * u), SorteioDosMoradores.TextoDoNaoFaz(f), claro);
            y += 22 * u;
            GUI.Label(new Rect(xc, y, 80 * u, 18 * u), "LAÇOS", _rotulo);
            float lx = xc + 80 * u, lw = wc - 80 * u;
            bool algum = false;
            foreach (var t in SorteioDosMoradores.TextosDosLacos(f, null))
            {
                GUI.Label(new Rect(lx, y, lw, 18 * u), t, claro);
                y += 17 * u;
                algum = true;
            }
            var pequenoClaro = new GUIStyle(claro) { fontSize = Mathf.RoundToInt(12 * u), richText = true };
            foreach (var l in f.lacos)
            {
                if (y > painel.yMax - 20 * u) break;
                var o = Npcs.DoId(l.outro);
                if (o == null) continue;
                int op = SorteioDosMoradores.OpiniaoDoComeco(f, o);
                string onde = _escolhidos.Contains(o.id) ? "<color=#8CC772>no bunker</color>"
                            : Fileira().Contains(o) ? "<color=#E69E33>nesta mão</color>" : "<color=#8C8780>lá fora</color>";
                GUI.Label(new Rect(lx, y, lw - 36 * u, 18 * u),
                          $"{SorteioDosMoradores.PalavraDoLaco(l.tipo, f.mulher)} {(o.mulher ? "da" : "do")} {o.nome} {o.sobrenome} · {onde}", pequenoClaro);
                var corDaOpiniao = op >= 20 ? Bom : op < 0 ? Ruim : Apagado;
                GUI.Label(new Rect(xc + wc - 36 * u, y, 36 * u, 18 * u), $"{(op >= 0 ? "+" : "")}{op}",
                          new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontSize = Mathf.RoundToInt(12 * u), normal = { textColor = corDaOpiniao } });
                if (new Rect(lx, y, lw, 18 * u).Contains(Event.current.mousePosition))
                    _dica = $"A opinião de {f.nome} sobre {o.nome}, de −100 a 100: o laço, os traços e um pouco de acaso.";
                y += 17 * u;
                algum = true;
            }
            if (!algum) GUI.Label(new Rect(lx, y, lw, 18 * u), "—", claro);
        }

        float Historia(float x, float y, float w, string titulo, string frase, float u)
        {
            var estilo = new GUIStyle(_rotulo) { fontSize = Mathf.RoundToInt(12 * u), wordWrap = true };
            float altoDoTitulo = estilo.CalcHeight(new GUIContent(titulo), w);
            GUI.Label(new Rect(x, y, w, altoDoTitulo), titulo, estilo);
            y += altoDoTitulo;
            float alto = _frase.CalcHeight(new GUIContent(frase), w);
            GUI.Label(new Rect(x, y, w, alto), frase, _frase);
            return y + alto + 5 * u;
        }

        // as fichinhas de uma linha (o rótulo à esquerda), quebrando quando não cabem
        float Fichinhas(float x, float y, float w, string titulo, List<(string nome, string efeito)> itens, Color cor, float u)
        {
            GUI.Label(new Rect(x, y + 3 * u, 80 * u, 18 * u), titulo, _rotulo);
            float cx = x + 80 * u, cy = y, alto = 22 * u;
            if (itens.Count == 0)
            {
                GUI.Label(new Rect(cx, y + 3 * u, w - 80 * u, 18 * u), "—", new GUIStyle(_texto) { normal = { textColor = Claro } });
                return y + alto + 4 * u;
            }
            foreach (var (nome, efeito) in itens)
            {
                float largo = _chip.CalcSize(new GUIContent(nome)).x + 16 * u;
                if (cx + largo > x + w && cx > x + 80 * u)
                {
                    cx = x + 80 * u;
                    cy += alto + 3 * u;
                }
                var r = new Rect(cx, cy, largo, alto);
                Retangulo(r, cor);
                Moldura(r, Borda, u);
                GUI.Label(r, nome, _chip);
                if (r.Contains(Event.current.mousePosition) && !string.IsNullOrEmpty(efeito)) _dica = $"{nome}: {efeito}";
                cx += largo + 5 * u;
            }
            return cy + alto + 5 * u;
        }

        // a dica do que está embaixo do mouse, numa caixa ao lado dele
        void Dica(float u)
        {
            if (string.IsNullOrEmpty(_dica)) return;
            var estilo = new GUIStyle(_frase) { fontSize = Mathf.RoundToInt(13 * u) };
            float w = 320 * u, h = estilo.CalcHeight(new GUIContent(_dica), w - 16 * u) + 12 * u;
            var m = Event.current.mousePosition;
            var r = new Rect(m.x + 16 * u, m.y + 18 * u, w, h);
            if (r.xMax > Screen.width - 8 * u) r.x = m.x - w - 12 * u;
            if (r.yMax > Screen.height - 8 * u) r.y = m.y - h - 12 * u;
            Retangulo(r, new Color(0.03f, 0.03f, 0.03f, 0.95f));
            Moldura(r, Borda, u);
            GUI.Label(new Rect(r.x + 8 * u, r.y + 6 * u, r.width - 16 * u, r.height - 12 * u), _dica, estilo);
        }

        string Campo(Rect r, string valor, int maximo, float u)
        {
            Retangulo(r, new Color(0.05f, 0.05f, 0.05f, 1f));
            Moldura(r, Vermelho, u);
            return GUI.TextField(r, valor ?? "", maximo, _campo);
        }

        // um botão no estilo da tela (o painel escuro, a borda e o texto; com a cor, a borda e o texto nela)
        bool Botao(Rect r, string texto, float u, Color? cor = null, GUIStyle estilo = null)
        {
            bool sobre = r.Contains(Event.current.mousePosition);
            Retangulo(r, sobre ? new Color(0.16f, 0.15f, 0.14f, 1f) : Painel);
            Moldura(r, cor ?? (sobre ? Apagado : Borda), u);
            var s = new GUIStyle(estilo ?? _botao) { alignment = TextAnchor.MiddleCenter };
            if (cor.HasValue) s.normal.textColor = s.hover.textColor = cor.Value;
            return GUI.Button(r, texto, s);
        }

        static void Retangulo(Rect r, Color cor)
        {
            var antes = GUI.color;
            GUI.color = cor;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        static void Moldura(Rect r, Color cor, float grossura)
        {
            float g = Mathf.Max(1f, grossura);
            Retangulo(new Rect(r.x, r.y, r.width, g), cor);
            Retangulo(new Rect(r.x, r.yMax - g, r.width, g), cor);
            Retangulo(new Rect(r.x, r.y, g, r.height), cor);
            Retangulo(new Rect(r.xMax - g, r.y, g, r.height), cor);
        }

        // um círculo de luz (o alfa cai do meio para a borda)
        static Texture2D Brilho()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Brilho dos moradores" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // a chama da paixão: uma gota laranja, amarela no meio (larga embaixo, em ponta em cima)
        static Texture2D Chama()
        {
            const int w = 22, h = 30;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Chama da paixão" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (y + 0.5f) / h;                       // 0 embaixo, 1 em cima
                    float meia = v < 0.38f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.38f - v) / 0.38f, 2f))) * 0.48f
                                           : 0.48f * Mathf.Pow(1f - (v - 0.38f) / 0.62f, 1.3f);
                    float dx = Mathf.Abs((x + 0.5f) / w - 0.5f);
                    float borda = Mathf.Clamp01((meia - dx) * w * 0.9f);
                    float dentro = Mathf.Clamp01(1f - dx / Mathf.Max(0.01f, meia));
                    var cor = Color.Lerp(new Color(0.95f, 0.38f, 0.1f), new Color(1f, 0.85f, 0.35f), dentro * (1f - v) * 1.2f);
                    cor.a = borda;
                    px[y * w + x] = cor;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
