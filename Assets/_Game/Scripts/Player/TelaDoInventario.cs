using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A tela do inventário (TAB, só no dono; fecha com TAB, ESC ou o X do canto). OnGUI, como o resto do HUD: uma janela
    /// de 1160 × 590 (a 720p) com o fundo do concreto do chão do bunker e a borda escura com rebites.
    /// - À esquerda, o retrato do jogador (<see cref="RetratoDoJogador"/>; gira arrastando), os lugares da armadura (ainda
    ///   vazios), os da mochila e do coldre (o que está vestido; arrastar para fora, ou G, tira e larga no chão) e,
    ///   embaixo dos pés, a barra de atalhos.
    /// - No meio, as abas Mochila (a grade do <see cref="InventarioJogador"/>, do tamanho dos bolsos ou da mochila vestida,
    ///   e embaixo dela a alça: a primeira arma, fora da grade; uma arma arrastada para lá vai para a alça) e Criar (ainda
    ///   vazia).
    /// - À direita, o item escolhido: o nome, a foto e os detalhes.
    /// Clique escolhe um item. Arrastar move na grade, põe num atalho (o item continua na mochila) ou, para fora da
    /// janela, larga no chão; segurando o item, R gira ele 90° (a largura e a altura trocam). G larga o escolhido; 1, 2
    /// e 3 com o mouse em cima de um item põem ele no atalho. Um atalho
    /// arrastado para outro troca os dois; para fora da barra, sai (o item fica na mochila). Passando o mouse, o nome
    /// aparece ao lado. Com a tela aberta, o jogador não anda, não olha nem atira (<see cref="MenuOnline.Tela"/>).
    /// </summary>
    public class TelaDoInventario : MonoBehaviour
    {
        public JogadorRede rede;
        public InventarioJogador inventario;
        public ArmaJogador arma;
        [Tooltip("O material do retrato do jogador (shader ZombieCityCleaner/Retrato).")]
        public Material materialDoRetrato;

        public bool Aberta { get; private set; }

        // as medidas, a 720p (vezes u)
        const float W = 1160f, H = 590f;
        // a grade muda com a mochila (os bolsos 4 × 2 até a grande 10 × 7): as células têm até 54 e cabem neste espaço
        const float GradeX = 336f, GradeY = 58f, GradeMaxW = 486f, GradeMaxH = 336f;
        int Colunas => inventario.Colunas;
        int Linhas => inventario.Linhas;
        float Celula => Mathf.Min(54f, GradeMaxW / Colunas, GradeMaxH / Linhas);
        Rect AreaDaGrade => new Rect(GradeX, GradeY, Celula * Colunas, Celula * Linhas);
        // a alça (a primeira arma, fora da grade), embaixo da grade
        static readonly Rect AreaDaAlca = new Rect(GradeX, 448, 300, 58);
        static readonly Rect AreaDoRetrato = new Rect(18, 58, 300, 392);
        static readonly Rect AreaDoNome = new Rect(840, 58, 302, 40);
        static readonly Rect AreaDaFoto = new Rect(840, 106, 302, 250);
        static readonly Rect AreaDosDetalhes = new Rect(840, 364, 302, 208);
        static readonly Rect AbaMochila = new Rect(336, 14, 150, 36), AbaCriar = new Rect(492, 14, 150, 36);
        static readonly Rect BotaoFechar = new Rect(W - 52, 12, 36, 36);
        const float AtalhoY = 478f, AtalhoW = 92f, AtalhoH = 60f, AtalhoVao = 12f;
        // os lugares da armadura, em volta do retrato (ainda sem armadura no jogo)
        static readonly (Rect r, string nome)[] Armadura =
        {
            (new Rect(28, 80, 54, 54), "Cabeça"), (new Rect(28, 168, 54, 54), "Colete"),
            (new Rect(254, 80, 54, 54), "Calças"), (new Rect(254, 168, 54, 54), "Botas"),
        };
        // o que se veste e já existe: a mochila e o coldre
        static readonly Rect LugarDaMochila = new Rect(28, 256, 54, 54), LugarDoColdre = new Rect(254, 256, 54, 54);

        // as cores: o concreto do chão do bunker (Concreto, #6A6A64), a borda escura e o amarelo das paredes de lá (na
        // borda da dica)
        static readonly Color Fundo = new Color32(0x6A, 0x6A, 0x64, 0xFF);
        static readonly Color Borda = new Color32(0x2A, 0x2A, 0x27, 0xFF);
        static readonly Color BordaClara = new Color32(0x4A, 0x4A, 0x45, 0xFF);
        static readonly Color Friso = new Color32(0xC2, 0x9B, 0x2E, 0xE6);
        static readonly Color Painel = new Color(0f, 0f, 0f, 0.2f);
        static readonly Color CorDaCelula = new Color(0.09f, 0.09f, 0.085f, 0.55f);
        // o fundo de cada espécie de item: claro, porque as armas são escuras
        static readonly Color FundoDeArma = new Color(0.5f, 0.52f, 0.53f, 0.95f);
        static readonly Color FundoDeComida = new Color(0.58f, 0.5f, 0.37f, 0.95f);
        static readonly Color FundoDeAgua = new Color(0.4f, 0.51f, 0.58f, 0.95f);
        static readonly Color FundoDoAtalho = new Color(0.4f, 0.41f, 0.41f, 0.9f);
        static readonly Color FundoDeEquipamento = new Color(0.52f, 0.5f, 0.44f, 0.95f);
        static readonly Color FundoDeMunicao = new Color(0.47f, 0.45f, 0.38f, 0.95f);
        static readonly Color FundoDePeca = new Color(0.55f, 0.53f, 0.5f, 0.95f);
        static readonly Color CorDoTexto = new Color(0.94f, 0.92f, 0.86f);
        static readonly Color TextoApagado = new Color(0.8f, 0.78f, 0.72f, 0.85f);
        static readonly Color Destaque = new Color(0.98f, 0.82f, 0.36f);
        static readonly CultureInfo Br = new CultureInfo("pt-BR");

        enum Aba { Mochila, Criar }
        enum Origem { Nada, Mochila, Atalho, Retrato, Alca, Equipamento }

        Aba _aba;
        RetratoDoJogador _retrato;
        ushort _escolhido, _sobMouse;
        // o equipamento apertado e o escolhido (os detalhes mostram ele no lugar de um item da mochila)
        Item _equipApertado, _equipEscolhido;
        Origem _apertou;
        ushort _idApertado;
        int _atalhoApertado, _quadrosSolto;
        Vector2 _inicio, _pega, _ultimoMouse;
        // arrastando, e se o item segurado está girado (R)
        bool _arrastando, _girado;
        // as posições pedidas ao anfitrião que ainda não voltaram (para o item não pular de volta enquanto isso)
        readonly Dictionary<ushort, (int x, int y, bool girado, float ate)> _pendentes = new Dictionary<ushort, (int, int, bool, float)>();
        float _u = 1f, _uDosEstilos;
        Vector2 _origem;
        GUIStyle _titulo, _nome, _texto, _pequeno, _direita, _centro, _numero, _dica, _fechar;
        static Texture2D _circulo;

        void OnEnable() => MenuOnline.FecharTelas += Fechar;

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            Abrir(false);
        }

        void Fechar() => Abrir(false);

        void Abrir(bool abrir)
        {
            if (abrir == Aberta) return;
            Aberta = abrir;
            MenuOnline.Tela(this, abrir);
            _apertou = Origem.Nada;
            _arrastando = false;
            if (abrir)
            {
                _u = Escala();
                if (materialDoRetrato != null)
                    _retrato = new RetratoDoJogador(rede, materialDoRetrato, Mathf.RoundToInt(AreaDoRetrato.width * _u), Mathf.RoundToInt(AreaDoRetrato.height * _u));
                if (inventario.Do(_escolhido) == Item.Nada) _escolhido = inventario.IdNaMao;
            }
            else
            {
                _retrato?.Destruir();
                _retrato = null;
            }
        }

        static float Escala() => Mathf.Min(Screen.height / 720f, Screen.width / 1280f);

        void Update()
        {
            if (rede == null || !rede.IsSpawned || !rede.IsOwner || MenuOnline.Bot) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.tabKey.wasPressedThisFrame && !MenuOnline.Aberto) Abrir(!Aberta);
            if (!Aberta) return;
            _retrato?.Atualizar();
            // arrastando, o botão solto fora da janela do jogo não chega ao OnGUI: solto há uns quadros, solta onde o
            // mouse estava (antes disso, o soltar de verdade ainda pode chegar)
            var mouse = Mouse.current;
            if (_arrastando && mouse != null && !mouse.leftButton.isPressed)
            {
                if (++_quadrosSolto >= 3)
                {
                    Soltar(_ultimoMouse);
                    _apertou = Origem.Nada;
                    _arrastando = false;
                }
            }
            else _quadrosSolto = 0;
            if (teclado == null || MenuOnline.Aberto) return;
            if (teclado.gKey.wasPressedThisFrame)
            {
                if (Vestido(_equipEscolhido))
                {
                    inventario.TirarEquipamento(_equipEscolhido);
                    _equipEscolhido = Item.Nada;
                }
                else if (inventario.Do(_escolhido) != Item.Nada) inventario.Largar(_escolhido);
            }
            if (teclado.rKey.wasPressedThisFrame && (_apertou == Origem.Mochila || _apertou == Origem.Alca)) Girar();
            if (_sobMouse != 0)
            {
                if (teclado.digit1Key.wasPressedThisFrame) inventario.PorNoAtalho(0, _sobMouse);
                else if (teclado.digit2Key.wasPressedThisFrame) inventario.PorNoAtalho(1, _sobMouse);
                else if (teclado.digit3Key.wasPressedThisFrame) inventario.PorNoAtalho(2, _sobMouse);
            }
        }

        // ------------------------------------------------------------------ medidas

        Rect Em(Rect r) => new Rect(_origem.x + r.x * _u, _origem.y + r.y * _u, r.width * _u, r.height * _u);
        Rect Janela => new Rect(_origem.x, _origem.y, W * _u, H * _u);
        Rect Atalho(int k) => Em(new Rect(AreaDoRetrato.x + k * (AtalhoW + AtalhoVao), AtalhoY, AtalhoW, AtalhoH));

        // a posição e o giro de um item na grade (os pedidos ao anfitrião, enquanto a resposta não chega)
        (int x, int y, bool girado) Posicao(ItemNaMochila it)
        {
            if (_pendentes.TryGetValue(it.id, out var p))
            {
                if (Time.unscaledTime < p.ate && (p.x != it.x || p.y != it.y || p.girado != it.girado)) return (p.x, p.y, p.girado);
                _pendentes.Remove(it.id);
            }
            return (it.x, it.y, it.girado);
        }

        /// <summary>
        /// Segurando um item da mochila, R gira ele 90° (a largura e a altura trocam), em volta do mouse: o ponto do item
        /// embaixo dele continua embaixo dele. Ele passa a acompanhar o mouse, se ainda não acompanhava. Os quadrados
        /// (a lata) não giram.
        /// </summary>
        void Girar()
        {
            if (!inventario.Achar(_idApertado, out var it)) return;
            var t = InventarioJogador.TamanhoNaGrade(it.item, _girado);
            if (t.x == t.y) return;
            float c = Celula * _u;
            // deitado para girado, no sentido do relógio; de volta, ao contrário
            _pega = _girado ? new Vector2(_pega.y, t.x * c - _pega.x) : new Vector2(t.y * c - _pega.y, _pega.x);
            _girado = !_girado;
            _arrastando = true;
        }

        Rect RetanguloNaGrade(int x, int y, Vector2Int tamanho)
        {
            var g = Em(AreaDaGrade);
            float c = Celula * _u;
            return new Rect(g.x + x * c, g.y + y * c, tamanho.x * c, tamanho.y * c);
        }

        /// <summary>O equipamento está vestido agora.</summary>
        bool Vestido(Item e) => e == Item.Coldre ? inventario.ComColdre.Value : Itens.EhMochila(e) && inventario.MochilaVestida.Value == e;

        /// <summary>O lugar do equipamento embaixo do mouse, e o que está vestido nele (Nada, vazio).</summary>
        bool EquipamentoEm(Vector2 m, out Item vestido)
        {
            vestido = Item.Nada;
            if (Em(LugarDaMochila).Contains(m))
            {
                vestido = inventario.MochilaVestida.Value;
                return true;
            }
            if (Em(LugarDoColdre).Contains(m))
            {
                if (inventario.ComColdre.Value) vestido = Item.Coldre;
                return true;
            }
            return false;
        }

        bool ItemEm(Vector2 m, out ItemNaMochila item, out Rect r)
        {
            var lista = inventario.Mochila;
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i].naAlca) continue;
                var p = Posicao(lista[i]);
                r = RetanguloNaGrade(p.x, p.y, InventarioJogador.TamanhoNaGrade(lista[i].item, p.girado));
                if (r.Contains(m))
                {
                    item = lista[i];
                    return true;
                }
            }
            item = default;
            r = default;
            return false;
        }

        bool AtalhoEm(Vector2 m, out int k)
        {
            for (k = 0; k < InventarioJogador.Atalhos; k++)
                if (Atalho(k).Contains(m)) return true;
            k = -1;
            return false;
        }

        // a célula onde o item arrastado cairia (o canto de cima, à esquerda, do item que acompanha o mouse, com o giro)
        bool CelulaDoArrasto(Vector2 m, Item item, bool girado, out int x, out int y)
        {
            var g = Em(AreaDaGrade);
            float c = Celula * _u;
            var canto = m - _pega - g.position;
            x = Mathf.RoundToInt(canto.x / c);
            y = Mathf.RoundToInt(canto.y / c);
            var t = InventarioJogador.TamanhoNaGrade(item, girado);
            x = Mathf.Clamp(x, 0, Colunas - t.x);
            y = Mathf.Clamp(y, 0, Linhas - t.y);
            return _aba == Aba.Mochila && g.Contains(m);
        }

        // ------------------------------------------------------------------ o mouse

        void OnGUI()
        {
            if (!Aberta || MenuOnline.Aberto) return;
            GUI.depth = 0;
            _u = Escala();
            Estilos();
            _origem = new Vector2(Mathf.Round((Screen.width - W * _u) / 2f), Mathf.Round((Screen.height - H * _u) / 2f));
            var e = Event.current;
            _ultimoMouse = e.mousePosition;
            if (!MenuDoDesenvolvedor.Area.Contains(e.mousePosition)) Processar(e);
            if (e.type == EventType.Repaint) Desenhar(e.mousePosition);
        }

        /// <summary>
        /// Um evento do mouse (em pixels da tela, de cima para baixo): apertar, arrastar e soltar. O OnGUI manda os de
        /// verdade; um teste pode mandar os dele (com a tela aberta e já desenhada uma vez).
        /// </summary>
        public void Processar(Event e)
        {
            var m = e.mousePosition;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    _inicio = m;
                    _arrastando = false;
                    _apertou = Origem.Nada;
                    if (Em(BotaoFechar).Contains(m)) Abrir(false);
                    else if (Em(AbaMochila).Contains(m)) _aba = Aba.Mochila;
                    else if (Em(AbaCriar).Contains(m)) _aba = Aba.Criar;
                    else if (_aba == Aba.Mochila && ItemEm(m, out var it, out var r))
                    {
                        _apertou = Origem.Mochila;
                        _idApertado = it.id;
                        _pega = m - r.position;
                        _girado = Posicao(it).girado;
                    }
                    else if (_aba == Aba.Mochila && Em(AreaDaAlca).Contains(m) && inventario.NaAlca(out var alca))
                    {
                        // a arma da alça, arrastada, fica do tamanho dela na grade, presa pela primeira célula
                        _apertou = Origem.Alca;
                        _idApertado = alca.id;
                        _pega = Vector2.one * (Celula * _u * 0.5f);
                        _girado = false;
                    }
                    else if (EquipamentoEm(m, out var vestido) && vestido != Item.Nada)
                    {
                        _apertou = Origem.Equipamento;
                        _equipApertado = vestido;
                        _pega = Vector2.one * (27f * _u);
                    }
                    else if (AtalhoEm(m, out int k))
                    {
                        _apertou = Origem.Atalho;
                        _atalhoApertado = k;
                        _pega = m - Atalho(k).position;
                    }
                    else if (Em(AreaDoRetrato).Contains(m)) _apertou = Origem.Retrato;
                    e.Use();
                    break;
                case EventType.MouseDrag when e.button == 0:
                    if (_apertou == Origem.Retrato && _retrato != null) _retrato.Giro -= e.delta.x * 0.6f / _u;
                    else if (_apertou != Origem.Nada && !_arrastando && (m - _inicio).magnitude > 5f * _u) _arrastando = true;
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0:
                    if (_arrastando) Soltar(m);
                    else if (_apertou == Origem.Mochila || _apertou == Origem.Alca) Escolher(_idApertado);
                    else if (_apertou == Origem.Atalho && inventario[_atalhoApertado] != Item.Nada) Escolher(inventario.Atalho(_atalhoApertado));
                    else if (_apertou == Origem.Equipamento)
                    {
                        _equipEscolhido = _equipApertado;
                        _escolhido = 0;
                    }
                    _apertou = Origem.Nada;
                    _arrastando = false;
                    e.Use();
                    break;
            }
        }

        void Escolher(ushort id)
        {
            _escolhido = id;
            _equipEscolhido = Item.Nada;
        }

        void Soltar(Vector2 m)
        {
            if (_apertou == Origem.Mochila || _apertou == Origem.Alca)
            {
                if (!inventario.Achar(_idApertado, out var it)) return;
                if (AtalhoEm(m, out int k)) inventario.PorNoAtalho(k, it.id);
                else if (_apertou == Origem.Mochila && _aba == Aba.Mochila && Em(AreaDaAlca).Contains(m)) inventario.PorNaAlca(it.id);
                // uma caixa de munição solta em cima de outra do mesmo calibre: as balas passam para ela
                else if (_aba == Aba.Mochila && Itens.EhMunicao(it.item) && ItemEm(m, out var outra, out _) && outra.id != it.id && outra.item == it.item)
                    inventario.Juntar(it.id, outra.id);
                else if (CelulaDoArrasto(m, it.item, _girado, out int x, out int y))
                {
                    if (inventario.PodeMover(it.id, x, y, _girado) && (it.naAlca || x != it.x || y != it.y || _girado != it.girado))
                    {
                        inventario.Mover(it.id, x, y, _girado);
                        _pendentes[it.id] = (x, y, _girado, Time.unscaledTime + 0.8f);
                    }
                }
                else if (!Janela.Contains(m))
                {
                    inventario.Largar(it.id);
                    if (_escolhido == it.id) _escolhido = 0;
                }
            }
            else if (_apertou == Origem.Equipamento)
            {
                if (!Janela.Contains(m) && Vestido(_equipApertado))
                {
                    inventario.TirarEquipamento(_equipApertado);
                    if (_equipEscolhido == _equipApertado) _equipEscolhido = Item.Nada;
                }
            }
            else if (_apertou == Origem.Atalho)
            {
                if (AtalhoEm(m, out int k)) inventario.TrocarAtalhos(_atalhoApertado, k);
                else inventario.TirarDoAtalho(_atalhoApertado);
            }
        }

        // ------------------------------------------------------------------ o desenho

        void Desenhar(Vector2 mouse)
        {
            var janela = Janela;
            float u = _u;
            // a sombra, a borda escura, o fundo, o friso amarelo e os rebites
            Ret(new Rect(janela.x + 8 * u, janela.y + 10 * u, janela.width, janela.height), new Color(0f, 0f, 0f, 0.45f));
            Ret(janela, Borda);
            var dentro = Encolher(janela, 8 * u);
            Ret(dentro, Fundo);
            Moldura(Encolher(janela, 3 * u), Mathf.Max(1f, Mathf.Round(u)), BordaClara);
            Rebites(janela, u);

            _sobMouse = 0;
            Cabecalho(mouse);
            LadoEsquerdo(mouse);
            if (_aba == Aba.Mochila)
            {
                Grade(mouse);
                Alca(mouse);
            }
            else Criar();
            Detalhes();
            Arrasto(mouse);
            Dica(mouse);
        }

        void Cabecalho(Vector2 mouse)
        {
            float u = _u;
            Rotulo(Em(new Rect(AreaDoRetrato.x + 4, 18, 300, 28)), "EQUIPAMENTO", _titulo);
            foreach (var (r, nome, aba) in new[] { (AbaMochila, "MOCHILA", Aba.Mochila), (AbaCriar, "CRIAR", Aba.Criar) })
            {
                var a = Em(r);
                bool ativa = _aba == aba;
                Ret(a, ativa ? new Color(0f, 0f, 0f, 0.3f) : (a.Contains(mouse) ? new Color(0f, 0f, 0f, 0.18f) : new Color(0f, 0f, 0f, 0.1f)));
                if (ativa) Ret(new Rect(a.x, a.yMax - 3 * u, a.width, 3 * u), Destaque);
                _titulo.normal.textColor = ativa ? Destaque : TextoApagado;
                GUI.Label(a, nome, new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter });
                _titulo.normal.textColor = CorDoTexto;
            }
            Rotulo(Em(new Rect(AreaDoNome.x + 4, 18, 300, 28)), "DETALHES", _titulo);
            var x = Em(BotaoFechar);
            Ret(x, x.Contains(mouse) ? new Color(0.62f, 0.16f, 0.12f, 0.95f) : new Color(0f, 0f, 0f, 0.35f));
            Moldura(x, Mathf.Max(1f, Mathf.Round(u)), BordaClara);
            GUI.Label(x, "X", _fechar);
        }

        void LadoEsquerdo(Vector2 mouse)
        {
            float u = _u;
            var area = Em(AreaDoRetrato);
            Ret(area, Painel);
            if (_retrato != null && _retrato.Imagem != null)
            {
                // a sombra no chão, embaixo dos pés
                var pes = _retrato.Pes;
                var p = new Vector2(area.x + pes.x * area.width, area.y + pes.y * area.height);
                var antes = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(new Rect(p.x - 52 * u, p.y - 9 * u, 104 * u, 18 * u), Circulo());
                GUI.color = antes;
                GUI.DrawTexture(area, _retrato.Imagem, ScaleMode.StretchToFill, true);
            }
            // a armadura: ainda não há (os lugares ficam apagados)
            foreach (var (r, nome) in Armadura)
            {
                var a = Em(r);
                Ret(a, new Color(0f, 0f, 0f, 0.28f));
                Moldura(a, Mathf.Max(1f, Mathf.Round(u)), new Color(1f, 1f, 1f, 0.08f));
                GUI.Label(new Rect(a.x - 10 * u, a.yMax + 1 * u, a.width + 20 * u, 16 * u), nome, _centro);
                if (a.Contains(mouse) && !_arrastando) _dicaTexto = nome + " (em breve)";
            }
            // a mochila e o coldre: o ícone do que está vestido (arrastar para fora tira e larga no chão)
            foreach (var (r, nome, vestido) in new[]
                     {
                         (LugarDaMochila, "Mochila", inventario.MochilaVestida.Value),
                         (LugarDoColdre, "Coldre", inventario.ComColdre.Value ? Item.Coldre : Item.Nada),
                     })
            {
                var a = Em(r);
                bool escolhido = vestido != Item.Nada && vestido == _equipEscolhido;
                bool arrastado = _arrastando && _apertou == Origem.Equipamento && _equipApertado == vestido;
                Ret(a, vestido != Item.Nada ? FundoDeEquipamento : new Color(0f, 0f, 0f, 0.28f));
                if (a.Contains(mouse) && !_arrastando && vestido != Item.Nada) Ret(a, new Color(1f, 1f, 1f, 0.07f));
                Moldura(a, Mathf.Max(1f, Mathf.Round((escolhido ? 2 : 1) * u)), escolhido ? Destaque : new Color(1f, 1f, 1f, vestido != Item.Nada ? 0.16f : 0.08f));
                var icone = inventario.IconeDe(vestido);
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, arrastado ? 0.3f : 1f);
                    GUI.DrawTexture(Encolher(a, 3 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                GUI.Label(new Rect(a.x - 10 * u, a.yMax + 1 * u, a.width + 20 * u, 16 * u), nome, _centro);
                if (a.Contains(mouse) && !_arrastando)
                    _dicaTexto = vestido != Item.Nada ? Maiuscula(Itens.Nome(vestido)) : nome == "Mochila" ? "Sem mochila: só os bolsos" : "Sem coldre";
            }
            // a barra de atalhos, embaixo dos pés
            Rotulo(Em(new Rect(AreaDoRetrato.x + 4, AtalhoY - 22, 300, 20)), "ATALHOS", _pequeno);
            for (int k = 0; k < InventarioJogador.Atalhos; k++)
            {
                var a = Atalho(k);
                bool naMao = k == inventario.Escolhido.Value;
                bool alvo = _arrastando && a.Contains(mouse);
                Ret(a, FundoDoAtalho);
                if (alvo) Ret(a, new Color(0.98f, 0.82f, 0.36f, 0.3f));
                Moldura(a, Mathf.Max(1f, Mathf.Round((naMao ? 2 : 1) * u)), naMao ? Destaque : new Color(1f, 1f, 1f, 0.1f));
                ushort id = inventario.Atalho(k);
                var item = inventario.Do(id);
                bool arrastado = _arrastando && _apertou == Origem.Atalho && _atalhoApertado == k;
                var icone = inventario.IconeDe(item);
                if (icone != null)
                {
                    var antes = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, arrastado ? 0.3f : 1f);
                    GUI.DrawTexture(Encolher(a, 6 * u), icone, ScaleMode.ScaleToFit);
                    GUI.color = antes;
                }
                _numero.normal.textColor = naMao ? Destaque : TextoApagado;
                GUI.Label(new Rect(a.x + 4 * u, a.y + 1 * u, 20 * u, 18 * u), (k + 1).ToString(), _numero);
                if (a.Contains(mouse) && item != Item.Nada && !_arrastando) _dicaTexto = Maiuscula(Itens.Nome(item));
            }
        }

        void Grade(Vector2 mouse)
        {
            float u = _u, c = Celula * u;
            var g = Em(AreaDaGrade);
            Ret(Encolher(g, -4 * u), Painel);
            for (int y = 0; y < Linhas; y++)
                for (int x = 0; x < Colunas; x++)
                    Ret(new Rect(g.x + x * c + 1, g.y + y * c + 1, c - 2, c - 2), CorDaCelula);
            // onde o item arrastado cairia: verde se cabe, vermelho se não (a arma que sai da alça também precisa de
            // lugar no corpo)
            if (_arrastando && _apertou == Origem.Mochila && inventario.Achar(_idApertado, out var caixa) && Itens.EhMunicao(caixa.item)
                && ItemEm(mouse, out var outra, out var ro) && outra.id != caixa.id && outra.item == caixa.item)
            {
                // juntar duas caixas do mesmo calibre
                Ret(ro, inventario.PodeJuntar(caixa.id, outra.id) ? new Color(0.35f, 0.85f, 0.35f, 0.4f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
            }
            else if (_arrastando && (_apertou == Origem.Mochila || _apertou == Origem.Alca) && inventario.Achar(_idApertado, out var arrastado)
                && CelulaDoArrasto(mouse, arrastado.item, _girado, out int ax, out int ay))
            {
                bool cabe = inventario.PodeMover(arrastado.id, ax, ay, _girado);
                Ret(RetanguloNaGrade(ax, ay, InventarioJogador.TamanhoNaGrade(arrastado.item, _girado)),
                    cabe ? new Color(0.35f, 0.85f, 0.35f, 0.3f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
            }
            var lista = inventario.Mochila;
            for (int i = 0; i < lista.Count; i++)
            {
                var it = lista[i];
                if (it.naAlca) continue;
                var p = Posicao(it);
                var r = RetanguloNaGrade(p.x, p.y, InventarioJogador.TamanhoNaGrade(it.item, p.girado));
                bool sendoArrastado = _arrastando && _apertou == Origem.Mochila && _idApertado == it.id;
                DesenharItem(it, Encolher(r, 2 * u), p.girado, sendoArrastado ? 0.3f : 1f, it.id == _escolhido, r.Contains(mouse) && !_arrastando);
                if (r.Contains(mouse) && !_arrastando)
                {
                    _sobMouse = it.id;
                    _dicaTexto = Maiuscula(Itens.Nome(it.item)) + (Itens.EhMunicao(it.item) ? $" ({it.qtd} de {Itens.MaxPorCaixa})" : Itens.ComEstado(it.item) ? $" ({it.qtd}%)" : "");
                }
            }
            // o tamanho da grade: os bolsos ou a mochila vestida
            var mochila = inventario.MochilaVestida.Value;
            string legenda = mochila == Item.Nada
                ? $"Bolsos ({Colunas} × {Linhas}). Ache uma mochila para levar mais."
                : $"{Maiuscula(Itens.Nome(mochila))} ({Colunas} × {Linhas})";
            GUI.Label(Em(new Rect(AreaDaGrade.x, AreaDaGrade.yMax + 6, GradeMaxW, 20)), legenda, _pequeno);
        }

        /// <summary>
        /// A alça: a primeira arma, pendurada no ombro, fora da grade (de qualquer tamanho). Embaixo, quantas armas ele
        /// leva e onde cabem as outras (as costas da mochila e o coldre).
        /// </summary>
        void Alca(Vector2 mouse)
        {
            float u = _u;
            Rotulo(Em(new Rect(AreaDaAlca.x + 2, AreaDaAlca.y - 20, 300, 18)), "ALÇA (a 1ª arma, fora da grade)", _pequeno);
            var a = Em(AreaDaAlca);
            Ret(a, new Color(0f, 0f, 0f, 0.28f));
            // arrastando uma arma da grade para cá: verde se a alça está livre, vermelho se não
            if (_arrastando && _apertou == Origem.Mochila && a.Contains(mouse))
                Ret(a, inventario.PodeIrParaAlca(_idApertado) ? new Color(0.35f, 0.85f, 0.35f, 0.3f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
            if (inventario.NaAlca(out var arma))
            {
                bool arrastado = _arrastando && _apertou == Origem.Alca && _idApertado == arma.id;
                DesenharItem(arma, Encolher(a, 2 * u), false, arrastado ? 0.3f : 1f, arma.id == _escolhido && _equipEscolhido == Item.Nada,
                    a.Contains(mouse) && !_arrastando);
                if (a.Contains(mouse) && !_arrastando)
                {
                    _sobMouse = arma.id;
                    _dicaTexto = Maiuscula(Itens.Nome(arma.item)) + " (na alça)";
                }
            }
            else
            {
                Moldura(a, Mathf.Max(1f, Mathf.Round(u)), new Color(1f, 1f, 1f, 0.08f));
                GUI.Label(a, "vazia", new GUIStyle(_centro) { alignment = TextAnchor.MiddleCenter });
            }
            // quantas armas: a da alça, mais uma nas costas (com mochila) e uma curta no coldre
            bool mochila = inventario.MochilaVestida.Value != Item.Nada, coldre = inventario.ComColdre.Value;
            string onde = "alça" + (mochila ? ", costas" : "") + (coldre ? ", coldre" : "");
            string falta = !mochila && !coldre ? " · mochila ou coldre levam mais" : !mochila ? " · mochila leva mais uma" : !coldre ? " · coldre leva mais uma curta" : "";
            GUI.Label(Em(new Rect(AreaDaAlca.x + 2, AreaDaAlca.yMax + 6, GradeMaxW, 20)),
                $"Armas: {inventario.ArmasLevadas} de {inventario.VagasDeArma} ({onde}){falta}", _pequeno);
            var direita = new GUIStyle(_texto) { alignment = TextAnchor.UpperRight };
            GUI.Label(Em(new Rect(GradeX, AreaDaAlca.y + 6, GradeMaxW, 20)), "G: largar o escolhido", direita);
            GUI.Label(Em(new Rect(GradeX, AreaDaAlca.y + 28, GradeMaxW, 20)), "R: girar item", direita);
        }

        // um item: o fundo pela espécie (arma, comida, água), o ícone (girado 90° no sentido do relógio, se o item está
        // girado), o atalho em que ele está e o pente
        void DesenharItem(ItemNaMochila it, Rect r, bool girado, float alfa, bool escolhido, bool sobMouse)
        {
            float u = _u;
            var fundo = Itens.EhArma(it.item) ? FundoDeArma : Itens.EhMunicao(it.item) ? FundoDeMunicao : Itens.EhPeca(it.item) ? FundoDePeca
                : it.item == Item.Comida ? FundoDeComida : FundoDeAgua;
            fundo.a *= alfa;
            Ret(r, fundo);
            if (sobMouse) Ret(r, new Color(1f, 1f, 1f, 0.07f));
            Moldura(r, Mathf.Max(1f, Mathf.Round(u)), new Color(1f, 1f, 1f, 0.12f * alfa));
            var icone = inventario.IconeDe(it.item);
            if (icone != null)
            {
                var antes = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                var area = Encolher(r, 3 * u);
                if (girado)
                {
                    // o ícone deitado, no retângulo de lados trocados, girado em volta do meio
                    var matriz = GUI.matrix;
                    GUIUtility.RotateAroundPivot(90f, area.center);
                    GUI.DrawTexture(new Rect(area.center.x - area.height / 2f, area.center.y - area.width / 2f, area.height, area.width), icone, ScaleMode.ScaleToFit);
                    GUI.matrix = matriz;
                }
                else GUI.DrawTexture(area, icone, ScaleMode.ScaleToFit);
                GUI.color = antes;
            }
            if (alfa < 1f) return;
            int atalho = inventario.LugarDoAtalho(it.id);
            if (atalho >= 0)
            {
                var b = new Rect(r.xMax - 16 * u, r.y + 2 * u, 14 * u, 14 * u);
                Ret(b, new Color(0f, 0f, 0f, 0.55f));
                _numero.normal.textColor = Destaque;
                GUI.Label(b, (atalho + 1).ToString(), new GUIStyle(_numero) { alignment = TextAnchor.MiddleCenter });
            }
            if (Itens.EhArma(it.item))
            {
                int cap = Armas.Dados(Itens.Arma(it.item)).capacidade;
                GUI.Label(new Rect(r.x, r.yMax - 17 * u, r.width - 4 * u, 16 * u), $"{arma.MunicaoDe(it.id, it.item)}/{cap}", _direita);
            }
            else if (Itens.EhMunicao(it.item))
            {
                // o calibre em cima (as caixas são parecidas) e as balas embaixo, com uma faixa escura para ler
                var cal = Itens.NomeDoCalibre(Itens.CalibreDa(it.item));
                Ret(new Rect(r.x, r.y, r.width, 14 * u), new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(new Rect(r.x + 3 * u, r.y - 1 * u, r.width, 15 * u), cal == "calibre 12" ? "cal. 12" : cal, _pequeno);
                Ret(new Rect(r.x, r.yMax - 15 * u, r.width, 15 * u), new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(new Rect(r.x, r.yMax - 17 * u, r.width - 4 * u, 16 * u), it.qtd.ToString(), _direita);
            }
            else if (Itens.ComEstado(it.item))
            {
                // o estado da peça embaixo, na cor dele
                Ret(new Rect(r.x, r.yMax - 15 * u, r.width, 15 * u), new Color(0f, 0f, 0f, 0.45f));
                var antes = _direita.normal.textColor;
                _direita.normal.textColor = Vehicles.PecasDoMotor.CorDoEstado(it.qtd);
                GUI.Label(new Rect(r.x, r.yMax - 17 * u, r.width - 4 * u, 16 * u), it.qtd + "%", _direita);
                _direita.normal.textColor = antes;
            }
            if (escolhido) Moldura(r, Mathf.Max(1f, Mathf.Round(2 * u)), Destaque);
        }

        void Criar()
        {
            float u = _u;
            var g = Em(AreaDaGrade);
            Ret(Encolher(g, -4 * u), Painel);
            GUI.Label(new Rect(g.x, g.y + g.height * 0.38f, g.width, 30 * u), "Nada para criar ainda.", new GUIStyle(_nome) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(g.x, g.y + g.height * 0.38f + 30 * u, g.width, 24 * u), "As receitas vêm com o saque.", _centro);
        }

        void Detalhes()
        {
            float u = _u;
            var nome = Em(AreaDoNome);
            var foto = Em(AreaDaFoto);
            var det = Em(AreaDosDetalhes);
            Ret(nome, Painel);
            Ret(foto, Painel);
            Ret(det, Painel);
            if (Vestido(_equipEscolhido))
            {
                DetalhesDoEquipamento(_equipEscolhido, nome, foto, det);
                return;
            }
            if (!inventario.Achar(_escolhido, out var it))
            {
                GUI.Label(Encolher(nome, 10 * u), "—", _nome);
                GUI.Label(new Rect(foto.x, foto.center.y - 12 * u, foto.width, 24 * u), "Escolha um item da mochila", _centro);
                return;
            }
            var item = it.item;
            GUI.Label(new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height), Maiuscula(Itens.Nome(item)), _nome);
            var imagem = inventario.FotoDe(item);
            if (imagem == null) imagem = inventario.IconeDe(item);
            if (imagem != null) GUI.DrawTexture(Encolher(foto, 10 * u), imagem, ScaleMode.ScaleToFit);

            float x = det.x + 12 * u, w = det.width - 24 * u, y = det.y + 10 * u, linha = 22 * u;
            GUI.Label(new Rect(x, y, w, 20 * u), Itens.Resumo(item), _texto);
            y += 30 * u;
            void Linha(string rotulo, string valor)
            {
                GUI.Label(new Rect(x, y, w, linha), rotulo, _texto);
                GUI.Label(new Rect(x, y, w, linha), valor, new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold });
                Ret(new Rect(x, y + linha - 3 * u, w, 1), new Color(1f, 1f, 1f, 0.07f));
                y += linha;
            }
            var t = Itens.Tamanho(item);
            if (Itens.EhArma(item))
            {
                var d = Armas.Dados(Itens.Arma(item));
                Linha("Pente", $"{arma.MunicaoDe(it.id, item)} / {d.capacidade}");
                Linha("Munição", $"{Itens.NomeDoCalibre(d.calibre)}  ·  {inventario.BalasDe(d.calibre)} nas caixas");
                Linha("Dano", d.faixas != null && d.faixas.Length > 0 ? $"{Numero(d.faixas[0].corpo)} de perto" : Numero(d.dano));
                Linha("Tiros por minuto", Mathf.RoundToInt(60f / d.intervalo).ToString());
                Linha("Recarga", d.cartuchoACartucho ? $"{Numero(d.tempoRecarga)} s por cartucho" : $"{Numero(d.tempoRecarga)} s");
            }
            else if (Itens.EhMunicao(item))
            {
                var cal = Itens.CalibreDa(item);
                Linha("Balas", $"{it.qtd} de {Itens.MaxPorCaixa}");
                Linha("Calibre", Itens.NomeDoCalibre(cal));
                Linha("Serve em", Itens.ArmasDoCalibre(cal));
                Linha("Total na mochila", inventario.BalasDe(cal).ToString());
            }
            else if (Itens.EhPeca(item))
            {
                int k = Itens.IndiceDaPeca(item);
                if (item == Item.Macaco)
                {
                    Linha("Serve para", "levantar o carro");
                    Linha("Fica", "no porta-malas, com o estepe");
                }
                else if (item == Item.Pneu)
                {
                    Linha("Estado", $"{it.qtd}%");
                    Linha("Vai em", "uma roda do carro");
                    Linha("Efeito", Vehicles.PneusDoCarro.Efeito(it.qtd));
                }
                else
                {
                    Linha("Estado", $"{it.qtd}%");
                    Linha("Vai no capô", Vehicles.PecasDoMotor.Lugares[k]);
                    Linha("Efeito", Vehicles.PecasDoMotor.Efeito(k, it.qtd));
                }
            }
            else if (Itens.EhConsumivel(item))
            {
                var c = Itens.Consumivel(item);
                if (c.fome > 0f) Linha("Fome", "+" + Numero(c.fome));
                if (c.sede > 0f) Linha("Sede", "+" + Numero(c.sede));
                Linha(Maiuscula(c.verbo), $"{Numero(c.tempoDeUso)} s");
            }
            Linha("Tamanho", $"{t.x} × {t.y}");
            if (it.naAlca) Linha("Onde", "na alça (fora da grade)");
        }

        // a mochila ou o coldre vestido: o nome, a foto, o espaço que dá e quantas armas leva
        void DetalhesDoEquipamento(Item item, Rect nome, Rect foto, Rect det)
        {
            float u = _u;
            GUI.Label(new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height), Maiuscula(Itens.Nome(item)), _nome);
            var imagem = inventario.FotoDe(item);
            if (imagem == null) imagem = inventario.IconeDe(item);
            if (imagem != null) GUI.DrawTexture(Encolher(foto, 10 * u), imagem, ScaleMode.ScaleToFit);
            float x = det.x + 12 * u, w = det.width - 24 * u, y = det.y + 10 * u, linha = 22 * u;
            GUI.Label(new Rect(x, y, w, 20 * u), Itens.Resumo(item), _texto);
            y += 30 * u;
            void Linha(string rotulo, string valor)
            {
                GUI.Label(new Rect(x, y, w, linha), rotulo, _texto);
                GUI.Label(new Rect(x, y, w, linha), valor, new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold });
                Ret(new Rect(x, y + linha - 3 * u, w, 1), new Color(1f, 1f, 1f, 0.07f));
                y += linha;
            }
            if (Itens.EhMochila(item))
            {
                var e = Itens.EspacoDa(item);
                Linha("Espaço", $"{e.x} × {e.y}  (bolsos: {Itens.Bolsos.x} × {Itens.Bolsos.y})");
                Linha("Armas", "+1 nas costas");
            }
            else Linha("Armas", "+1 curta (pistola ou submetralhadora)");
            y += 6 * u;
            GUI.Label(new Rect(x, y, w, 40 * u), "Para tirar: G, ou arraste para fora da janela.", _pequeno);
        }

        void Arrasto(Vector2 mouse)
        {
            if (!_arrastando) return;
            float u = _u;
            if ((_apertou == Origem.Mochila || _apertou == Origem.Alca) && inventario.Achar(_idApertado, out var it))
            {
                var t = InventarioJogador.TamanhoNaGrade(it.item, _girado);
                var r = new Rect(mouse - _pega, new Vector2(t.x * Celula * u, t.y * Celula * u));
                DesenharItem(it, Encolher(r, 2 * u), _girado, 0.85f, false, false);
                if (!Janela.Contains(mouse)) GUI.Label(new Rect(mouse.x + 14 * u, mouse.y - 20 * u, 160 * u, 20 * u), "soltar: largar no chão", _dica);
            }
            else if (_apertou == Origem.Equipamento)
            {
                var icone = inventario.IconeDe(_equipApertado);
                if (icone != null) GUI.DrawTexture(new Rect(mouse - _pega, new Vector2(54 * u, 54 * u)), icone, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(mouse.x + 14 * u, mouse.y - 20 * u, 200 * u, 20 * u),
                    Janela.Contains(mouse) ? "solte fora da janela para tirar" : "soltar: tirar e largar no chão", _dica);
            }
            else if (_apertou == Origem.Atalho)
            {
                var icone = inventario.IconeDe(inventario[_atalhoApertado]);
                if (icone != null) GUI.DrawTexture(new Rect(mouse - _pega, new Vector2(AtalhoW * u, AtalhoH * u)), icone, ScaleMode.ScaleToFit);
                if (!AtalhoEm(mouse, out _)) GUI.Label(new Rect(mouse.x + 14 * u, mouse.y - 20 * u, 160 * u, 20 * u), "soltar: tirar do atalho", _dica);
            }
        }

        string _dicaTexto;

        // o nome do que está embaixo do mouse, ao lado dele
        void Dica(Vector2 mouse)
        {
            if (string.IsNullOrEmpty(_dicaTexto) || _arrastando)
            {
                _dicaTexto = null;
                return;
            }
            float u = _u;
            var tamanho = _dica.CalcSize(new GUIContent(_dicaTexto));
            var r = new Rect(mouse.x + 16 * u, mouse.y + 18 * u, tamanho.x + 14 * u, tamanho.y + 6 * u);
            if (r.xMax > Screen.width) r.x = mouse.x - r.width - 6 * u;
            Ret(r, new Color(0.07f, 0.07f, 0.065f, 0.94f));
            Moldura(r, Mathf.Max(1f, Mathf.Round(u)), Friso);
            GUI.Label(r, _dicaTexto, _dica);
            _dicaTexto = null;
        }

        // ------------------------------------------------------------------ ajudantes

        internal static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
        static string Numero(float v) => v.ToString("0.#", Br);

        internal static Rect Encolher(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - 2 * d, r.height - 2 * d);

        internal static void Ret(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        internal static void Moldura(Rect r, float b, Color c)
        {
            Ret(new Rect(r.x, r.y, r.width, b), c);
            Ret(new Rect(r.x, r.yMax - b, r.width, b), c);
            Ret(new Rect(r.x, r.y, b, r.height), c);
            Ret(new Rect(r.xMax - b, r.y, b, r.height), c);
        }

        void Rotulo(Rect r, string texto, GUIStyle estilo) => GUI.Label(r, texto, estilo);

        // os rebites da borda: um em cada canto e outros ao longo dela, com um brilho em cima
        internal static void Rebites(Rect janela, float u)
        {
            var circulo = Circulo();
            float raio = 3.2f * u, d = 4f * u;
            void Rebite(float x, float y)
            {
                var antes = GUI.color;
                GUI.color = new Color(0.16f, 0.16f, 0.15f);
                GUI.DrawTexture(new Rect(x - raio, y - raio + 0.8f * u, 2 * raio, 2 * raio), circulo);
                GUI.color = new Color(0.42f, 0.42f, 0.39f);
                GUI.DrawTexture(new Rect(x - raio, y - raio, 2 * raio, 2 * raio), circulo);
                GUI.color = new Color(0.62f, 0.62f, 0.58f);
                GUI.DrawTexture(new Rect(x - raio * 0.45f, y - raio * 0.6f, raio * 0.8f, raio * 0.8f), circulo);
                GUI.color = antes;
            }
            int nx = Mathf.Max(2, Mathf.RoundToInt(janela.width / (110f * u)));
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(janela.x + d, janela.xMax - d, i / (float)nx);
                Rebite(x, janela.y + d);
                Rebite(x, janela.yMax - d);
            }
            int ny = Mathf.Max(2, Mathf.RoundToInt(janela.height / (110f * u)));
            for (int i = 1; i < ny; i++)
            {
                float y = Mathf.Lerp(janela.y + d, janela.yMax - d, i / (float)ny);
                Rebite(janela.x + d, y);
                Rebite(janela.xMax - d, y);
            }
        }

        internal static Texture2D Circulo()
        {
            if (_circulo != null) return _circulo;
            const int n = 32;
            _circulo = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Círculo", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    _circulo.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - r)));
                }
            _circulo.Apply();
            return _circulo;
        }

        void Estilos()
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, _u)) return;
            _uDosEstilos = _u;
            float u = _u;
            GUIStyle Estilo(int tamanho, FontStyle fonte, TextAnchor ancora, Color cor)
            {
                var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = fonte, alignment = ancora, wordWrap = true, clipping = TextClipping.Clip };
                s.normal.textColor = cor;
                s.padding = new RectOffset(0, 0, 0, 0);
                return s;
            }
            _titulo = Estilo(15, FontStyle.Bold, TextAnchor.MiddleLeft, CorDoTexto);
            _nome = Estilo(19, FontStyle.Bold, TextAnchor.MiddleLeft, CorDoTexto);
            _texto = Estilo(14, FontStyle.Normal, TextAnchor.UpperLeft, CorDoTexto);
            _pequeno = Estilo(12, FontStyle.Normal, TextAnchor.UpperLeft, TextoApagado);
            _centro = Estilo(12, FontStyle.Normal, TextAnchor.UpperCenter, TextoApagado);
            _direita = Estilo(11, FontStyle.Bold, TextAnchor.LowerRight, new Color(0.92f, 0.9f, 0.82f, 0.9f));
            _numero = Estilo(12, FontStyle.Bold, TextAnchor.UpperLeft, TextoApagado);
            _dica = Estilo(13, FontStyle.Bold, TextAnchor.MiddleCenter, CorDoTexto);
            _dica.wordWrap = false;
            _fechar = Estilo(18, FontStyle.Bold, TextAnchor.MiddleCenter, CorDoTexto);
        }
    }
}
