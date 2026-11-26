using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.UI
{
    // O "Novo jogo" (o Iniciar novo jogo) e o "Continuar" da cena Mundo, repaginados em 06/11/2026 (a pedido: "aquele menu
    // está muito bugado, algumas coisas em cima do outro ... algumas coisas nem utilizamos mais"). No estilo da tela de criar
    // o personagem (Player/TelaDeCriarPersonagem) e da das cartas (UI/TelaDosMoradores): os painéis escuros, o destaque
    // vermelho e a Bahnschrift. Tudo é medido num quadro de 1280 × 720 no meio da tela (Quadro, R), que cabe também em 4:3:
    // antes, tudo crescia pela altura da tela, e em 4:3 o painel do personagem saía pela direita.
    //  - Novo jogo: à esquerda, o MUNDO (o nome, o tamanho e a semente) e as REGRAS (salvar o jogo e o fogo amigo); à direita,
    //    o PERSONAGEM e o JOGAR COM AMIGOS (pela internet ou na rede local, e o entrar no mundo de um amigo); embaixo, o voltar,
    //    o erro da conexão e o "Escolher os moradores".
    //  - Continuar: os mundos salvos (clicar escolhe, dois cliques jogam; o apagar pede o segundo clique), o personagem e a
    //    rede; embaixo, o voltar, o erro e o "Jogar".
    public partial class MenuOnline
    {
        // as cores das outras telas (a de criar o personagem e a das cartas)
        static class Cores
        {
            public static readonly Color Escuro = new Color(0f, 0f, 0f, 0.62f);      // por cima do vídeo do menu
            public static readonly Color Painel = new Color(0.105f, 0.1f, 0.098f, 0.94f);
            public static readonly Color PainelSobre = new Color(0.15f, 0.14f, 0.135f, 0.96f);
            public static readonly Color Borda = new Color(0.2f, 0.19f, 0.18f, 1f);
            public static readonly Color Vermelho = new Color(0.72f, 0.14f, 0.15f, 1f);
            public static readonly Color VermelhoSobre = new Color(0.8f, 0.18f, 0.18f, 1f);
            public static readonly Color VermelhoApagado = new Color(0.3f, 0.12f, 0.12f, 1f);
            public static readonly Color BordaVermelha = new Color(0.95f, 0.35f, 0.33f, 1f);
            public static readonly Color Claro = new Color(0.9f, 0.87f, 0.82f, 1f);
            public static readonly Color Medio = new Color(0.78f, 0.75f, 0.7f, 1f);
            public static readonly Color Apagado = new Color(0.55f, 0.53f, 0.5f, 1f);
            public static readonly Color Ruim = new Color(0.9f, 0.42f, 0.38f, 1f);
            public static readonly Color Campo = new Color(0.05f, 0.05f, 0.05f, 1f);
            public static readonly Color Opcao = new Color(0.07f, 0.07f, 0.07f, 1f);
            public static readonly Color OpcaoSobre = new Color(0.1f, 0.095f, 0.09f, 1f);
            public static readonly Color OpcaoEscolhida = new Color(0.17f, 0.11f, 0.1f, 1f);
        }

        static readonly string[] NomesDosTamanhos = { "PEQUENO", "MÉDIO", "GRANDE" };
        static readonly string[] KmDosTamanhos = { "3 km", "5 km", "8 km" };
        static readonly string[] NomesDosModos = { "AUTOMÁTICO", "AO SAIR", "SÓ DORMINDO" };
        static readonly string[] DetalhesDosModos = { "a cada 5 min e ao sair", "e ao dormir", "numa cama" };

        // pela internet (o Relay, com o código) ou na rede local (o IP), no Novo jogo e no Continuar
        bool _redeLocal;
        // o mundo escolhido na lista do Continuar
        string _mundoEscolhido;

        // o quadro de 1280 × 720 no meio da tela: a unidade (_q) e o canto de cima, à esquerda (_o)
        float _q = 1f;
        Vector2 _o;
        // (a fonte é do sistema, criada na hora)
        [System.NonSerialized] Font _fonte;
        GUIStyle _eTitulo, _eCaixa, _eCaixaDireita, _eSub, _eCampo, _eDicaDoCampo, _eOpcao, _eOpcaoEscolhida, _eOpcao2,
                 _eBotao, _eBotaoPequeno, _eBotaoGrande, _eSeta, _eNome, _eInfo, _eInfoApagado, _eDica, _eErro, _eMundo,
                 _eDetalheDoMundo, _eAviso, _eNomeMenor;
        float _qDosEstilos;

        void Quadro()
        {
            _q = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            _o = new Vector2((Screen.width - 1280f * _q) / 2f, (Screen.height - 720f * _q) / 2f);
        }

        // um retângulo do quadro (em unidades de 1280 × 720) na tela
        Rect R(float x, float y, float w, float h) => new Rect(_o.x + x * _q, _o.y + y * _q, w * _q, h * _q);

        Font Fonte() => _fonte != null ? _fonte
            // (só o nome da família: com "Bahnschrift SemiBold Condensed", que não existe, a fonte fica presa num tamanho)
            : (_fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 16));

        void EstilosNovos()
        {
            if (_eTitulo != null && Mathf.Approximately(_qDosEstilos, _q)) return;
            _qDosEstilos = _q;
            GUIStyle E(GUIStyle de, int tamanho, FontStyle jeito, Color cor, TextAnchor ancora, bool quebra = false)
            {
                var s = new GUIStyle(de) { font = Fonte(), fontSize = Mathf.Max(8, Mathf.RoundToInt(tamanho * _q)), fontStyle = jeito, alignment = ancora, wordWrap = quebra };
                Opcoes.Cor(s, cor);
                s.padding = new RectOffset(0, 0, 0, 0);
                s.margin = new RectOffset(0, 0, 0, 0);
                return s;
            }
            var rotulo = GUI.skin.label;
            _eTitulo = E(rotulo, 40, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleLeft);
            _eCaixa = E(rotulo, 19, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleLeft);
            _eCaixaDireita = E(rotulo, 15, FontStyle.Bold, Cores.Apagado, TextAnchor.MiddleRight);
            _eSub = E(rotulo, 14, FontStyle.Bold, Cores.Apagado, TextAnchor.MiddleLeft);
            // o campo de texto sai do estilo de rótulo, sem fundo: o escuro e a borda são desenhados antes (Campo). (Do
            // textField, mesmo sem o background, o cinza da pele padrão continuava, pelas imagens de alta resolução)
            _eCampo = E(rotulo, 21, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleLeft);
            _eCampo.padding = new RectOffset(Mathf.RoundToInt(10 * _q), Mathf.RoundToInt(6 * _q), 0, 0);
            _eCampo.clipping = TextClipping.Clip;
            _eDicaDoCampo = E(rotulo, 17, FontStyle.Normal, Cores.Apagado, TextAnchor.MiddleLeft);
            _eDicaDoCampo.padding = new RectOffset(Mathf.RoundToInt(10 * _q), 0, 0, 0);
            _eOpcao = E(rotulo, 18, FontStyle.Bold, Cores.Medio, TextAnchor.MiddleCenter);
            _eOpcaoEscolhida = E(rotulo, 18, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleCenter);
            _eOpcao2 = E(rotulo, 13, FontStyle.Normal, Cores.Apagado, TextAnchor.MiddleCenter);
            _eBotao = E(rotulo, 17, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleCenter);
            _eBotaoPequeno = E(rotulo, 13, FontStyle.Bold, Cores.Apagado, TextAnchor.MiddleCenter);
            _eBotaoGrande = E(rotulo, 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            _eSeta = E(rotulo, 28, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleCenter);
            _eNome = E(rotulo, 28, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleCenter);
            _eInfo = E(rotulo, 16, FontStyle.Normal, Cores.Claro, TextAnchor.MiddleCenter);
            _eInfoApagado = E(rotulo, 14, FontStyle.Normal, Cores.Apagado, TextAnchor.MiddleCenter);
            _eDica = E(rotulo, 14, FontStyle.Normal, Cores.Apagado, TextAnchor.MiddleLeft, true);
            _eErro = E(rotulo, 15, FontStyle.Normal, Cores.Ruim, TextAnchor.MiddleLeft, true);
            _eMundo = E(rotulo, 20, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleLeft);
            _eMundo.clipping = TextClipping.Clip;
            _eDetalheDoMundo = E(rotulo, 14, FontStyle.Normal, Cores.Apagado, TextAnchor.MiddleLeft);
            _eDetalheDoMundo.clipping = TextClipping.Clip;
            _eAviso = E(rotulo, 13, FontStyle.Normal, Cores.Ruim, TextAnchor.MiddleRight);
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) o nome do personagem que não cabe entre as setas
            // numa letra menor (NomeQueCabe); com os estilos novos, o que foi medido nos de antes é esquecido
            _eNomeMenor = E(rotulo, 21, FontStyle.Bold, Cores.Claro, TextAnchor.MiddleCenter);
            _estiloDoNomeMedido = null;
            _larguraDoAviso = -1f;
            _cortados.Clear();
        }

        // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) os textos da lista dos mundos que não cabem na largura,
        // já com as reticências, guardados: o OnGUI roda várias vezes por quadro, e cortar mede o texto de novo a cada letra
        // tirada
        readonly System.Collections.Generic.Dictionary<(string, GUIStyle), (float largura, string texto)> _cortados =
            new System.Collections.Generic.Dictionary<(string, GUIStyle), (float largura, string texto)>();
        float _larguraDoAviso = -1f;
        const string AvisoDoGerador = "gerador antigo: o mapa pode ter mudado";

        string CaberNaLista(string texto, GUIStyle estilo, float largura)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            var chave = (texto, estilo);
            if (_cortados.TryGetValue(chave, out var c) && Mathf.Approximately(c.largura, largura)) return c.texto;
            string t = Opcoes.Caber(texto, estilo, largura);
            _cortados[chave] = (largura, t);
            return t;
        }

        // o nome do personagem no painel (o que cabe e em que letra), guardado para o nome e a largura em que foi medido
        string _nomeMedido, _nomeQueCabe;
        float _larguraDoNomeMedido;
        [System.NonSerialized] GUIStyle _estiloDoNomeMedido;

        // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) um nome de 16 letras em maiúsculas largas passava da
        // caixa entre as setas (no painel estreito do Continuar) e saía cortado dos dois lados: ele sai numa letra menor e, se
        // nem assim couber, com as reticências
        string NomeQueCabe(string nome, float largura, out GUIStyle estilo)
        {
            if (_estiloDoNomeMedido == null || nome != _nomeMedido || !Mathf.Approximately(largura, _larguraDoNomeMedido))
            {
                _nomeMedido = nome;
                _larguraDoNomeMedido = largura;
                if (_eNome.CalcSize(new GUIContent(nome)).x <= largura)
                {
                    _estiloDoNomeMedido = _eNome;
                    _nomeQueCabe = nome;
                }
                else
                {
                    _estiloDoNomeMedido = _eNomeMenor;
                    _nomeQueCabe = Opcoes.Caber(nome, _eNomeMenor, largura);
                }
            }
            estilo = _estiloDoNomeMedido;
            return _nomeQueCabe;
        }

        // ------------------------------------------------------------------ as telas

        // o Novo jogo (o Iniciar novo jogo do menu principal)
        void TelaDoNovoJogo()
        {
            Quadro();
            EstilosNovos();
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), Cores.Escuro);
            bool conectando = _sessao.Atual == SessaoOnline.Estado.Conectando;
            GUI.enabled = !conectando;
            GUI.Label(R(40, 16, 700, 50), "NOVO JOGO", _eTitulo);

            // à esquerda: o mundo
            const float L = 40, LW = 590, cx = L + 14, cw = LW - 28;
            Caixa(L, 78, LW, 290, "MUNDO");
            Sub(cx, 112, cw, "NOME");
            _nomeDoMundo = Campo("NomeDoMundo", cx, 132, cw, 40, _nomeDoMundo, 30, PedidoDoMundo.Padrao.nome);
            Sub(cx, 184, cw, "TAMANHO DO MAPA");
            float tw = (cw - 20) / 3f;
            for (int i = 0; i < 3; i++)
                if (Escolha(cx + i * (tw + 10), 204, tw, 54, NomesDosTamanhos[i], KmDosTamanhos[i], (int)_tamanho == i)) _tamanho = (TamanhoDoMundo)i;
            Sub(cx, 270, cw, "SEMENTE");
            _semente = Campo("Semente", cx, 290, cw - 160, 40, _semente, 20, "vazia: sorteada");
            if (Botao(cx + cw - 150, 290, 150, 40, "SORTEAR")) _semente = GeradorDoMundo.NovaSemente().ToString();
            GUI.Label(R(cx, 334, cw, 22), "Um número, ou uma palavra (vira sempre o mesmo número).", _eDica);

            // à esquerda: as regras (02/11/2026, o save: as três formas de salvar; 03/11/2026, o fogo amigo)
            Caixa(L, 380, LW, 210, "REGRAS");
            Sub(cx, 414, cw, "SALVAR O JOGO");
            for (int i = 0; i < 3; i++)
                if (Escolha(cx + i * (tw + 10), 434, tw, 54, NomesDosModos[i], DetalhesDosModos[i], (int)_modoNovo == i)) _modoNovo = (ModoDeSalvar)i;
            Sub(cx, 500, cw, "FOGO AMIGO");
            if (Escolha(cx, 520, 140, 44, "DESLIGADO", null, !_fogoAmigoNovo)) _fogoAmigoNovo = false;
            if (Escolha(cx + 150, 520, 140, 44, "LIGADO", null, _fogoAmigoNovo)) _fogoAmigoNovo = true;
            GUI.Label(R(cx + 306, 520, cw - 306, 44), "O tiro e o golpe ferem os outros jogadores.", _eDica);

            // à direita: o personagem e os amigos
            PainelDoPersonagem(650, 78, 590, 206);
            PainelDosAmigos(650, 296, 590, 294, true);

            // embaixo: o voltar, o erro e o criar (o mundo só é criado depois da tela dos moradores, 04/11/2026)
            if (Botao(40, 644, 150, 44, "‹  VOLTAR")) Voltar();
            Erro();
            if (BotaoVermelho(880, 630, 360, 64, conectando ? "CONECTANDO..." : "ESCOLHER OS MORADORES", !conectando))
                AbrirOsMoradores(_redeLocal, null);
            GUI.enabled = true;
        }

        // o Continuar: os mundos salvos, o personagem que vai entrar e a rede
        void TelaDoContinuar()
        {
            Quadro();
            EstilosNovos();
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), Cores.Escuro);
            bool conectando = _sessao.Atual == SessaoOnline.Estado.Conectando;
            GUI.enabled = !conectando;
            GUI.Label(R(40, 16, 700, 50), "CONTINUAR", _eTitulo);

            var mundos = Mundos();
            MundoSalvo escolhido = null;
            foreach (var m in mundos) if (m.id == _mundoEscolhido) escolhido = m;
            if (escolhido == null && mundos.Count > 0) escolhido = mundos[0];
            _mundoEscolhido = escolhido?.id;
            // (25/11/2026, o bug do Continuar) ao escolher um mundo, o personagem passa a ser o que jogou nele por último; sem
            // ninguém dele neste computador, o último jogado. Nunca o "Novo personagem" sozinho: esse só com as setas
            if (escolhido != null && _personagemDoMundoDe != escolhido.id)
            {
                _personagemDoMundoDe = escolhido.id;
                int k = PersonagemDoMundo(escolhido);
                _indiceDoPersonagem = k >= 0 ? k : 0;
                _apagarPersonagem = null;
            }

            // embaixo, antes da lista: um clique ali não pode cair numa linha escondida da lista (os cliques da lista
            // também conferem se o mouse está nela)
            if (Botao(40, 644, 150, 44, "‹  VOLTAR")) Voltar();
            Erro();
            if (BotaoVermelho(880, 630, 360, 64, conectando ? "CONECTANDO..." : "JOGAR", !conectando && escolhido != null))
                Jogar(escolhido.id);

            // à esquerda: a lista
            Caixa(40, 78, 740, 512, "MUNDOS SALVOS", mundos.Count > 1 ? $"{mundos.Count} MUNDOS" : null);
            ListaDosMundos(mundos, R(54, 112, 712, 464));

            // à direita: o personagem e a rede
            PainelDoPersonagem(800, 78, 440, 206);
            PainelDosAmigos(800, 296, 440, 158, false);
            GUI.enabled = true;
        }

        // as linhas dos mundos, com a rolagem (a roda do mouse; sem as barras da pele padrão). Os cliques são lidos à mão
        // e só com o mouse na lista: um botão de uma linha rolada para fora pegaria o clique de quem está embaixo
        void ListaDosMundos(System.Collections.Generic.List<MundoSalvo> mundos, Rect area)
        {
            var e = Event.current;
            if (mundos.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y, area.width, 40 * _q), "Nenhum mundo salvo.", _eInfoApagado);
                return;
            }
            bool naLista = GUI.enabled && area.Contains(e.mousePosition);
            // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a linha ficou mais alta (de 70 para 90): embaixo do
            // nome, o tamanho e o resumo numa linha e quem jogou na outra
            float linha = 90 * _q, vao = 6 * _q, total = mundos.Count * linha - vao;
            bool rola = total > area.height;
            float largura = area.width - (rola ? 12 * _q : 0f);
            _rolagemDosMundos = GUI.BeginScrollView(area, _rolagemDosMundos, new Rect(0, 0, largura, total), false, false, GUIStyle.none, GUIStyle.none);
            string jogar = null, apagar = null;
            for (int i = 0; i < mundos.Count; i++)
            {
                var m = mundos[i];
                var r = new Rect(0, i * linha, largura, linha - vao);
                if (r.yMax < _rolagemDosMundos.y || r.y > _rolagemDosMundos.y + area.height) continue;   // fora da vista
                bool escolhida = m.id == _mundoEscolhido;
                bool sobre = naLista && r.Contains(e.mousePosition);
                Retangulo(r, escolhida ? Cores.OpcaoEscolhida : sobre ? Cores.OpcaoSobre : Cores.Opcao);
                Moldura(r, escolhida ? Cores.Vermelho : Cores.Borda, escolhida ? 1.6f * _q : _q);
                // (25/11/2026: no Lakewood os jogos têm todos o mesmo nome; quem jogou em cada um ajuda a achar o seu)
                string quem = NomesDeQuemJogou(m);
                // (26/11/2026) sem ninguém, o nome e o resumo descem para o meio da linha
                float desce = quem.Length > 0 ? 0f : 11 * _q;
                var nome = new Rect(r.x + 14 * _q, r.y + 8 * _q + desce, r.width - 168 * _q, 28 * _q);
                // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) com o aviso do gerador antigo, o nome fica só no
                // espaço à esquerda dele, com as reticências: um nome comprido passava por baixo do aviso, um texto em cima do outro
                if (m.versaoDoGerador != 0 && m.versaoDoGerador != GeradorDoMundo.Versao)
                {
                    if (_larguraDoAviso < 0f) _larguraDoAviso = _eAviso.CalcSize(new GUIContent(AvisoDoGerador)).x + 12 * _q;
                    GUI.Label(nome, CaberNaLista(m.nome, _eMundo, nome.width - _larguraDoAviso), _eMundo);
                    GUI.Label(nome, AvisoDoGerador, _eAviso);
                }
                else GUI.Label(nome, m.nome, _eMundo);
                // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) o tamanho e o resumo numa linha, e quem jogou na
                // de baixo, com as reticências se os nomes não couberem: numa linha só, com três nomes ou nomes compridos, o fim
                // passava da caixa e saía cortado no meio de uma letra (sumia a data e a hora)
                string tamanho = m.lakewood ? "" : NomesDosTamanhos[Mathf.Clamp(m.tamanho, 0, 2)] + " · ";
                float largTexto = r.width - 168 * _q;
                GUI.Label(new Rect(r.x + 14 * _q, r.y + 36 * _q + desce, largTexto, 20 * _q), tamanho + m.Resumo(), _eDetalheDoMundo);
                if (quem.Length > 0)
                    GUI.Label(new Rect(r.x + 14 * _q, r.y + 58 * _q, largTexto, 20 * _q), CaberNaLista("com " + quem, _eDetalheDoMundo, largTexto), _eDetalheDoMundo);
                // o apagar (o segundo clique confirma)
                bool confirmar = _apagarMundo == m.id;
                var ra = new Rect(r.xMax - 136 * _q, r.y + (r.height - 32 * _q) / 2f, 120 * _q, 32 * _q);
                bool sobreApagar = naLista && ra.Contains(e.mousePosition);
                Retangulo(ra, confirmar ? Cores.VermelhoApagado : sobreApagar ? Cores.PainelSobre : Cores.Painel);
                Moldura(ra, confirmar ? Cores.Vermelho : sobreApagar ? Cores.Apagado : Cores.Borda, _q);
                Rotulo(ra, confirmar ? "CONFIRMAR" : "APAGAR", _eBotaoPequeno, confirmar ? Cores.Claro : Cores.Apagado);
                if (naLista && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    if (ra.Contains(e.mousePosition))
                    {
                        if (confirmar) apagar = m.id;
                        else _apagarMundo = m.id;
                    }
                    else
                    {
                        // clicar escolhe; dois cliques jogam
                        _mundoEscolhido = m.id;
                        _apagarMundo = null;
                        if (e.clickCount >= 2) jogar = m.id;
                    }
                    e.Use();
                }
            }
            GUI.EndScrollView();
            // a barrinha da rolagem (só para ver que há mais)
            if (rola)
            {
                float fracao = area.height / total, topo = _rolagemDosMundos.y / total;
                Retangulo(new Rect(area.xMax - 4 * _q, area.y, 4 * _q, area.height), Cores.Opcao);
                Retangulo(new Rect(area.xMax - 4 * _q, area.y + topo * area.height, 4 * _q, fracao * area.height), Cores.Borda);
            }
            if (apagar != null)
            {
                MundoSalvo.Apagar(apagar);
                _mundos = null;
                _apagarMundo = null;
            }
            if (jogar != null) Jogar(jogar);
        }

        // ------------------------------------------------------------------ quem jogou em cada mundo (25/11/2026)

        // só as posições do arquivo do mundo (o resto do JSON é ignorado): a chave de cada personagem que já esteve nele
        [System.Serializable]
        class SoAsPosicoes
        {
            public System.Collections.Generic.List<PosicaoSalva> posicoes;
        }

        System.Collections.Generic.List<long> QuemJogou(MundoSalvo m)
        {
            if (m == null || string.IsNullOrEmpty(m.id)) return new System.Collections.Generic.List<long>();
            if (_quemJogouNoMundo.TryGetValue(m.id, out var lista)) return lista;
            lista = new System.Collections.Generic.List<long>();
            try
            {
                var arquivo = System.IO.Path.Combine(MundoSalvo.Pasta, m.id + ".json");
                if (System.IO.File.Exists(arquivo))
                {
                    var so = JsonUtility.FromJson<SoAsPosicoes>(System.IO.File.ReadAllText(arquivo));
                    if (so?.posicoes != null) foreach (var p in so.posicoes) lista.Add(p.chave);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] Quem jogou no mundo {m.id}: {e.Message}");
            }
            _quemJogouNoMundo[m.id] = lista;
            return lista;
        }

        /// <summary>O índice (na lista dos personagens) do que jogou no mundo por último; -1 se nenhum dele está neste computador.</summary>
        int PersonagemDoMundo(MundoSalvo m)
        {
            var quem = QuemJogou(m);
            var lista = Personagens();   // (os jogados por último primeiro)
            for (int i = 0; i < lista.Count; i++)
                if (quem.Contains(unchecked((long)lista[i].Chave))) return i;
            return -1;
        }

        // os nomes de quem jogou no mundo e ainda está neste computador ("asd e sergio")
        string NomesDeQuemJogou(MundoSalvo m)
        {
            var quem = QuemJogou(m);
            if (quem.Count == 0) return "";
            var nomes = new System.Collections.Generic.List<string>();
            foreach (var p in Personagens())
                if (quem.Contains(unchecked((long)p.Chave)) && !string.IsNullOrEmpty(p.nome) && !nomes.Contains(p.nome)) nomes.Add(p.nome);
            if (nomes.Count == 0) return "";
            if (nomes.Count > 3) return string.Join(", ", nomes.GetRange(0, 3)) + $" e mais {nomes.Count - 3}";
            return nomes.Count == 1 ? nomes[0] : string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
        }

        /// <summary>
        /// O painel do personagem (no Novo jogo e no Continuar): os salvos neste computador e o "Novo personagem", com as
        /// setas; o salvo mostra o dinheiro e o que leva, e pode ser apagado (com um segundo clique).
        /// </summary>
        void PainelDoPersonagem(float x, float y, float w, float h)
        {
            var lista = Personagens();
            _indiceDoPersonagem = Mathf.Clamp(_indiceDoPersonagem, 0, lista.Count);
            bool novo = _indiceDoPersonagem >= lista.Count;
            var p = novo ? null : lista[_indiceDoPersonagem];
            Caixa(x, y, w, h, "PERSONAGEM", lista.Count > 0 ? $"{_indiceDoPersonagem + 1} DE {lista.Count + 1}" : null);
            float cx = x + 14, cw = w - 28;
            if (lista.Count > 0)
            {
                if (Botao(cx, y + 36, 40, 52, "‹", _eSeta))
                {
                    _indiceDoPersonagem = (_indiceDoPersonagem + lista.Count) % (lista.Count + 1);
                    _apagarPersonagem = null;
                }
                if (Botao(cx + cw - 40, y + 36, 40, 52, "›", _eSeta))
                {
                    _indiceDoPersonagem = (_indiceDoPersonagem + 1) % (lista.Count + 1);
                    _apagarPersonagem = null;
                }
            }
            // (26/11/2026: um nome que não cabe sai numa letra menor: NomeQueCabe)
            var caixaDoNome = R(cx + 50, y + 36, cw - 100, 52);
            string nomeQueCabe = NomeQueCabe(novo ? "NOVO PERSONAGEM" : p.nome, caixaDoNome.width, out var estiloDoNome);
            GUI.Label(caixaDoNome, nomeQueCabe, estiloDoNome);
            // (25/11/2026) no Continuar, o painel diz se esse personagem já jogou no mundo escolhido
            MundoSalvo doMundo = null;
            if (_continuar) foreach (var m in Mundos()) if (m.id == _mundoEscolhido) doMundo = m;
            if (novo)
            {
                GUI.Label(R(cx, y + 100, cw, 24), "Você cria a aparência ao entrar.", _eInfo);
                GUI.Label(R(cx, y + 124, cw, 22), doMundo != null ? "Ele entra no mundo escolhido, que continua de onde parou."
                    : "Num mundo novo, ele começa pelo Dia 0.", _eInfoApagado);
                return;
            }
            GUI.Label(R(cx, y + 100, cw, 24), p.Resumo(), _eInfo);
            if (doMundo != null)
                GUI.Label(R(cx, y + 124, cw, 22), QuemJogou(doMundo).Contains(unchecked((long)p.Chave)) ? "Jogou neste mundo."
                    : "Ainda não jogou neste mundo.", _eInfoApagado);
            else if (!string.IsNullOrEmpty(p.ultimoMundo)) GUI.Label(R(cx, y + 124, cw, 22), "Último mundo: " + p.ultimoMundo, _eInfoApagado);
            bool confirmar = _apagarPersonagem == p.id;
            if (confirmar) GUI.Label(R(cx, y + h - 46, cw - 130, 30), "Clique de novo para apagar.", _eErro);
            if (BotaoPequeno(cx + cw - 120, y + h - 46, 120, 30, confirmar ? "CONFIRMAR" : "APAGAR", confirmar))
            {
                if (!confirmar) _apagarPersonagem = p.id;
                else
                {
                    PersonagemSalvo.Apagar(p.id);
                    _personagens = null;
                    _apagarPersonagem = null;
                }
            }
        }

        // o "jogar com amigos": como os amigos entram no seu mundo (pela internet ou na rede local) e, no Novo jogo, o entrar
        // no mundo de um amigo (o código, ou o IP na rede local; Enter no campo também entra)
        void PainelDosAmigos(float x, float y, float w, float h, bool entrar)
        {
            Caixa(x, y, w, h, "JOGAR COM AMIGOS");
            float cx = x + 14, cw = w - 28, hw = (cw - 10) / 2f;
            Sub(cx, y + 34, cw, "COMO OS AMIGOS ENTRAM NO SEU MUNDO");
            if (Escolha(cx, y + 54, hw, 54, "PELA INTERNET", "com o código", !_redeLocal)) _redeLocal = false;
            if (Escolha(cx + hw + 10, y + 54, hw, 54, "REDE LOCAL", "mesma rede, sem internet", _redeLocal)) _redeLocal = true;
            if (!entrar)
            {
                GUI.Label(R(cx, y + 116, cw, 22), _redeLocal ? "Eles entram pelo IP deste computador." : "O código aparece no ESC, quando o mundo abrir.", _eDica);
                return;
            }
            Retangulo(R(cx, y + 126, cw, 1), Cores.Borda);
            Sub(cx, y + 140, cw, "ENTRAR NO MUNDO DE UM AMIGO");
            const string Nome = "EntrarNoAmigo";
            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                         GUI.GetNameOfFocusedControl() == Nome;
            if (_redeLocal) _ip = Campo(Nome, cx, y + 160, cw - 160, 44, _ip, 40, "IP de quem criou o mundo");
            else _codigo = Campo(Nome, cx, y + 160, cw - 160, 44, _codigo, 12, "código do amigo");
            string alvo = (_redeLocal ? _ip : _codigo).Trim();
            bool pode = GUI.enabled && alvo.Length > 0;
            var antes = GUI.enabled;
            GUI.enabled = pode;
            bool clicou = Botao(cx + cw - 150, y + 160, 150, 44, "ENTRAR");
            GUI.enabled = antes;
            if (clicou || (enter && pode))
            {
                if (enter) e.Use();
                ParaEntrar(true);
                if (_redeLocal) _sessao.EntrarLocal(alvo);
                else _sessao.Entrar(alvo);
            }
            GUI.Label(R(cx, y + 212, cw, 22), "Você entra com o personagem escolhido acima.", _eDica);
            GUI.Label(R(cx, y + 236, cw, 22), _redeLocal
                ? "O IP é o do computador de quem criou o mundo (127.0.0.1: este computador)."
                : "O código aparece no ESC de quem criou o mundo.", _eDica);
        }

        // volta ao menu principal (o "Voltar" e o ESC)
        void Voltar()
        {
            _principal = true;
            _continuar = false;
            _apagarMundo = null;
            _apagarPersonagem = null;
            GUIUtility.keyboardControl = 0;
        }

        // o erro da conexão, embaixo, entre o voltar e o botão vermelho
        // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a caixa cresce com o erro (uma mensagem de 4 linhas
        // saía com a de cima e a de baixo cortadas na de 68), com o mesmo meio de antes, entre 596 (os painéis acabam em 590) e
        // 716; dos lados, o voltar acaba em 190 e o botão vermelho começa em 880
        void Erro()
        {
            if (string.IsNullOrEmpty(_sessao.Erro)) return;
            float altura = Mathf.Clamp(AlturaDe(_sessao.Erro, _eErro, 650 * _q) / _q + 2f, 68f, 120f);
            GUI.Label(R(210, Mathf.Clamp(664f - altura / 2f, 596f, 716f - altura), 650, altura), _sessao.Erro, _eErro);
        }

        /// <summary>
        /// Para os testes e as fotos (o MCP no Play): mostra o "novo" (o Novo jogo), o "continuar" ou o "principal" (o menu
        /// principal), pela internet ou na rede local.
        /// </summary>
        public void MostrarParaTeste(string tela, bool redeLocal = false)
        {
            _redeLocal = redeLocal;
            _opcoes = false;
            _continuar = tela == "continuar";
            _principal = tela == "principal";
            _personagens = null;
            _mundos = null;
        }

        // ------------------------------------------------------------------ as peças

        // um painel com o título (e um texto à direita, como o "2 DE 3")
        void Caixa(float x, float y, float w, float h, string titulo, string direita = null)
        {
            var r = R(x, y, w, h);
            Retangulo(r, Cores.Painel);
            Moldura(r, Cores.Borda, _q);
            GUI.Label(R(x + 14, y + 6, w - 28, 26), titulo, _eCaixa);
            if (direita != null) GUI.Label(R(x + 14, y + 6, w - 28, 26), direita, _eCaixaDireita);
        }

        // o nome de um campo ou de um grupo de opções (pequeno, em maiúsculas, apagado)
        void Sub(float x, float y, float w, string texto) => GUI.Label(R(x, y, w, 18), texto, _eSub);

        // um campo de texto escuro, com a borda vermelha enquanto se escreve e a dica quando vazio
        string Campo(string nome, float x, float y, float w, float h, string texto, int maximo, string dica)
        {
            var r = R(x, y, w, h);
            bool foco = GUI.GetNameOfFocusedControl() == nome;
            Retangulo(r, Cores.Campo);
            Moldura(r, foco ? Cores.Vermelho : Cores.Borda, _q);
            if (string.IsNullOrEmpty(texto) && !foco && dica != null) GUI.Label(r, dica, _eDicaDoCampo);
            GUI.SetNextControlName(nome);
            return GUI.TextField(r, texto ?? "", maximo, _eCampo);
        }

        // uma opção de um grupo (o tamanho, o modo de salvar, o fogo amigo, a rede): o quadrado escuro, o nome e a linha
        // de baixo; a escolhida com a borda vermelha
        bool Escolha(float x, float y, float w, float h, string nome, string embaixo, bool escolhida)
        {
            var r = R(x, y, w, h);
            bool sobre = GUI.enabled && !escolhida && r.Contains(Event.current.mousePosition);
            Retangulo(r, escolhida ? Cores.OpcaoEscolhida : sobre ? Cores.OpcaoSobre : Cores.Opcao);
            var estilo = escolhida || sobre ? _eOpcaoEscolhida : _eOpcao;
            if (embaixo == null) GUI.Label(r, nome, estilo);
            else
            {
                // (26/11/2026, a pedido do Pedro: os textos cortados pela metade) a caixa do nome 4 unidades mais alta, com o
                // mesmo meio: os acentos das maiúsculas da Bahnschrift (MÉDIO, AUTOMÁTICO, SÓ DORMINDO) sobem acima da linha da
                // letra e, na caixa justa, podiam perder a ponta
                GUI.Label(R(x, y + 4, w, h / 2f + 1), nome, estilo);
                GUI.Label(R(x, y + h / 2f + 1, w, h / 2f - 8), embaixo, _eOpcao2);
            }
            Moldura(r, escolhida ? Cores.Vermelho : sobre ? Cores.Apagado : Cores.Borda, escolhida ? 1.6f * _q : _q);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // um botão escuro com a borda (o voltar, o sortear, o entrar, as setas)
        bool Botao(float x, float y, float w, float h, string texto, GUIStyle estilo = null)
        {
            var r = R(x, y, w, h);
            bool sobre = GUI.enabled && r.Contains(Event.current.mousePosition);
            Retangulo(r, sobre ? Cores.PainelSobre : Cores.Painel);
            Moldura(r, sobre ? Cores.Apagado : Cores.Borda, _q);
            return GUI.Button(r, texto, estilo ?? _eBotao);
        }

        // o botão pequeno (o apagar), vermelho no segundo clique
        bool BotaoPequeno(float x, float y, float w, float h, string texto, bool perigo)
        {
            var r = R(x, y, w, h);
            bool sobre = GUI.enabled && r.Contains(Event.current.mousePosition);
            Retangulo(r, perigo ? Cores.VermelhoApagado : sobre ? Cores.PainelSobre : Cores.Painel);
            Moldura(r, perigo ? Cores.Vermelho : sobre ? Cores.Apagado : Cores.Borda, _q);
            Rotulo(r, texto, _eBotaoPequeno, perigo || sobre ? Cores.Claro : Cores.Apagado);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // o botão vermelho, embaixo, à direita (o que leva adiante)
        bool BotaoVermelho(float x, float y, float w, float h, string texto, bool pode)
        {
            var r = R(x, y, w, h);
            bool sobre = pode && GUI.enabled && r.Contains(Event.current.mousePosition);
            Retangulo(r, !pode ? Cores.VermelhoApagado : sobre ? Cores.VermelhoSobre : Cores.Vermelho);
            Moldura(r, pode ? Cores.BordaVermelha : Cores.Borda, _q);
            var antes = GUI.enabled;
            GUI.enabled = antes && pode;
            bool clicou = GUI.Button(r, texto, _eBotaoGrande);
            GUI.enabled = antes;
            return clicou;
        }

        // um rótulo numa cor (o estilo é de vários: a cor volta depois)
        static void Rotulo(Rect r, string texto, GUIStyle estilo, Color cor)
        {
            var antes = estilo.normal.textColor;
            Opcoes.Cor(estilo, cor);
            GUI.Label(r, texto, estilo);
            Opcoes.Cor(estilo, antes);
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
            float g = Mathf.Max(1f, Mathf.Round(grossura));
            Retangulo(new Rect(r.x, r.y, r.width, g), cor);
            Retangulo(new Rect(r.x, r.yMax - g, r.width, g), cor);
            Retangulo(new Rect(r.x, r.y, g, r.height), cor);
            Retangulo(new Rect(r.xMax - g, r.y, g, r.height), cor);
        }
    }
}
