using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A tela do inventário (TAB, só no dono; fecha com TAB, ESC ou o X do canto). OnGUI, como o resto do HUD: uma janela
    /// de 1160 × 590 (a 720p) com o fundo do concreto do chão do bunker e a borda escura com rebites.
    /// - À esquerda, o retrato do jogador (<see cref="RetratoDoJogador"/>; gira arrastando), os lugares da armadura (ainda
    ///   vazios), o da mochila (a vestida; arrastar para fora, ou G, tira e larga no chão) e, embaixo dos pés, a barra de
    ///   atalhos.
    /// - No meio, as abas Mochila (a grade do <see cref="InventarioJogador"/>, do tamanho dos bolsos ou da mochila vestida;
    ///   desde 31/10/2026 sem a alça: as armas vão na grade, quantas couberem) e Criar (as receitas de
    ///   <see cref="Receitas"/>: o que gasta, o que tem e o botão de criar; as da bancada só perto de uma).
    /// - À direita, o item escolhido (ou a receita): o nome, a foto e os detalhes.
    /// Clique escolhe um item. Arrastar move na grade, põe num atalho (o item continua na mochila), junta uma pilha em
    /// outra do mesmo item ou, para fora da janela, larga no chão; segurando o item, R gira ele 90° (a largura e a altura
    /// trocam). G larga o escolhido; 1, 2 e 3 com o mouse em cima de um item põem ele no atalho. Um atalho arrastado para
    /// outro troca os dois; para fora da barra, sai (o item fica na mochila). O botão direito abre o menu do item (31/10/2026,
    /// a pedido: "apertar o botão direito nos itens e ter a opção de jogar o item fora do inventário, comer, beber (se for
    /// consumível), usar no atalho (1, 2 e 3)"): comer, beber ou usar, pôr ou tirar de um atalho, desmontar, largar (um ou
    /// todos). Passando o mouse, o nome aparece ao lado. Com a tela aberta, o jogador não anda, não olha nem atira
    /// (<see cref="MenuOnline.Tela"/>).
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
        // as dicas e o aviso do que foi feito, embaixo da grade
        static readonly Rect AreaDoRodape = new Rect(GradeX, 448, GradeMaxW, 112);
        // a lista das receitas (a aba Criar), no lugar da grade
        static readonly Rect AreaDasReceitas = new Rect(GradeX, 58, GradeMaxW, 486);
        const float LinhaDaReceita = 48f;
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
        // o que se veste e já existe: a mochila (o coldre saiu em 31/10/2026)
        static readonly Rect LugarDaMochila = new Rect(28, 256, 54, 54);

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
        static readonly Color FundoDeRemedio = new Color(0.62f, 0.45f, 0.45f, 0.95f);
        static readonly Color FundoDeMaterial = new Color(0.5f, 0.47f, 0.42f, 0.95f);
        static readonly Color FundoDeConstrucao = new Color(0.47f, 0.5f, 0.42f, 0.95f);
        static readonly Color Bom = new Color(0.55f, 0.9f, 0.5f), Ruim = new Color(1f, 0.5f, 0.42f);
        // a sinuca: o verde do pano, claro (o estojo e a base dos tacos são escuros)
        static readonly Color FundoDaSinuca = new Color(0.44f, 0.54f, 0.46f, 0.95f);
        static readonly Color CorDoTexto = new Color(0.94f, 0.92f, 0.86f);
        static readonly Color TextoApagado = new Color(0.8f, 0.78f, 0.72f, 0.85f);
        static readonly Color Destaque = new Color(0.98f, 0.82f, 0.36f);
        static readonly CultureInfo Br = new CultureInfo("pt-BR");

        enum Aba { Mochila, Criar }
        enum Origem { Nada, Mochila, Atalho, Retrato, Equipamento }

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
        // a aba Criar: a receita escolhida e a primeira linha mostrada (a roda rola)
        int _receita = -1, _rolagem;
        // o aviso do que foi feito (uma receita, uma desmontagem)
        string _aviso;
        float _avisoAte;
        Vector2 _origem;
        GUIStyle _titulo, _nome, _texto, _pequeno, _direita, _centro, _numero, _dica, _fechar;
        static Texture2D _circulo;

        void OnEnable()
        {
            MenuOnline.FecharTelas += Fechar;
            if (inventario != null) inventario.Feito += Avisar;
        }

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (inventario != null) inventario.Feito -= Avisar;
            Abrir(false);
        }

        void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.unscaledTime + 3f;
        }

        /// <summary>Abre a tela na aba Criar (a bancada).</summary>
        public void AbrirNaAbaCriar()
        {
            if (rede == null || !rede.IsOwner || MenuOnline.Aberto) return;
            _aba = Aba.Criar;
            Abrir(true);
        }

        void Fechar() => Abrir(false);

        void Abrir(bool abrir)
        {
            if (abrir == Aberta) return;
            Aberta = abrir;
            MenuOnline.Tela(this, abrir);
            _apertou = Origem.Nada;
            _arrastando = false;
            _opcoes.Clear();
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
            if (teclado.rKey.wasPressedThisFrame && _apertou == Origem.Mochila) Girar();
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
        bool Vestido(Item e) => Itens.EhMochila(e) && inventario.MochilaVestida.Value == e;

        /// <summary>O lugar do equipamento embaixo do mouse, e o que está vestido nele (Nada, vazio).</summary>
        bool EquipamentoEm(Vector2 m, out Item vestido)
        {
            vestido = Item.Nada;
            if (!Em(LugarDaMochila).Contains(m)) return false;
            vestido = inventario.MochilaVestida.Value;
            return true;
        }

        bool ItemEm(Vector2 m, out ItemNaMochila item, out Rect r)
        {
            var lista = inventario.Mochila;
            for (int i = 0; i < lista.Count; i++)
            {
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
            if (!Aberta || MenuOnline.Aberto || ModoVideo.Ligado) return;
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
                case EventType.MouseDown when e.button == 1:
                    // o menu do botão direito: o do item, o do atalho ou o da mochila vestida
                    _opcoes.Clear();
                    _apertou = Origem.Nada;
                    _arrastando = false;
                    if (_aba == Aba.Mochila && ItemEm(m, out var doMenu, out _))
                    {
                        Escolher(doMenu.id);
                        MenuDoItem(doMenu, -1);
                    }
                    else if (AtalhoEm(m, out int atalhoDoMenu) && inventario.Achar(inventario.Atalho(atalhoDoMenu), out var noAtalho))
                    {
                        Escolher(noAtalho.id);
                        MenuDoItem(noAtalho, atalhoDoMenu);
                    }
                    else if (EquipamentoEm(m, out var vestidoDoMenu) && vestidoDoMenu != Item.Nada)
                    {
                        _equipEscolhido = vestidoDoMenu;
                        _escolhido = 0;
                        Opcao("Tirar e largar no chão", () => inventario.TirarEquipamento(vestidoDoMenu));
                    }
                    _menuEm = m;
                    e.Use();
                    break;
                case EventType.ScrollWheel when _aba == Aba.Criar:
                    _rolagem = Mathf.Clamp(_rolagem + (e.delta.y > 0f ? 1 : -1), 0, Mathf.Max(0, Receitas.Todas.Length - ReceitasNaTela));
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 0 && MenuAberto:
                    {
                        // com o menu aberto, o clique é dele: uma opção (apagada não faz nada) ou fora (fecha)
                        var menu = RetanguloDoMenu();
                        System.Action acao = null;
                        bool naOpcao = false;
                        for (int i = 0; i < _opcoes.Count; i++)
                            if (LinhaDoMenu(menu, i).Contains(m))
                            {
                                naOpcao = true;
                                acao = _opcoes[i].acao;
                            }
                        if (!naOpcao || acao != null) _opcoes.Clear();
                        acao?.Invoke();
                        _apertou = Origem.Nada;
                        e.Use();
                        break;
                    }
                case EventType.MouseDown when e.button == 0:
                    _inicio = m;
                    _arrastando = false;
                    _apertou = Origem.Nada;
                    if (Em(BotaoFechar).Contains(m)) Abrir(false);
                    else if (Em(AbaMochila).Contains(m)) _aba = Aba.Mochila;
                    else if (Em(AbaCriar).Contains(m)) _aba = Aba.Criar;
                    else if (_aba == Aba.Criar && ReceitaEm(m, out int receita, out bool noBotao))
                    {
                        _receita = receita;
                        _escolhido = 0;
                        _equipEscolhido = Item.Nada;
                        if (noBotao) inventario.Criar(receita);
                    }
                    else if (_aba == Aba.Mochila && ItemEm(m, out var it, out var r))
                    {
                        _apertou = Origem.Mochila;
                        _idApertado = it.id;
                        _pega = m - r.position;
                        _girado = Posicao(it).girado;
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
                    else if (_apertou == Origem.Mochila) Escolher(_idApertado);
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
            _receita = -1;
        }

        // ------------------------------------------------------------------ o menu do botão direito

        struct Opcao_
        {
            public string texto;
            /// <summary>O que a opção faz (null: apagada; o texto diz por quê).</summary>
            public System.Action acao;
        }

        readonly List<Opcao_> _opcoes = new List<Opcao_>();
        Vector2 _menuEm;
        const float MenuLinha = 26f;
        bool MenuAberto => _opcoes.Count > 0;

        void Opcao(string texto, System.Action acao) => _opcoes.Add(new Opcao_ { texto = texto, acao = acao });

        /// <summary>
        /// As opções de um item da mochila (ou do atalho <paramref name="atalho"/>, -1 da grade): comer, beber ou usar;
        /// pôr ou tirar dos atalhos; desmontar; largar (um, ou todos da pilha).
        /// </summary>
        void MenuDoItem(ItemNaMochila it, int atalho)
        {
            var item = it.item;
            ushort id = it.id;
            int n = Itens.Quantos(item, it.qtd);
            bool ocupado = arma != null && arma.Usando;
            if (Itens.EhConsumivel(item))
                Opcao(Maiuscula(Itens.Consumivel(item).verbo) + (ocupado ? " (ocupado)" : ""), ocupado ? null : () => inventario.UsarDaMochila(id));
            if (Itens.EhConstrucao(item))
                Opcao("Montar no chão", ocupado ? null : () =>
                {
                    Abrir(false);
                    var montador = GetComponent<MontadorDeConstrucao>();
                    if (montador == null) montador = gameObject.AddComponent<MontadorDeConstrucao>();
                    montador.Comecar(id);
                });
            if (Itens.VaiNaMao(item))
            {
                for (int k = 0; k < InventarioJogador.Atalhos; k++)
                {
                    int lugar = k;
                    if (inventario.Atalho(k) == id) Opcao($"Tirar do atalho {k + 1}", ocupado ? null : () => inventario.TirarDoAtalho(lugar));
                    else if (atalho < 0) Opcao($"Pôr no atalho {k + 1}", ocupado ? null : () => inventario.PorNoAtalho(lugar, id));
                }
            }
            if (Itens.ComDesgaste(item) && it.desgaste > 0)
                Opcao(inventario.PodeReparar(id) ? $"Reparar ({100 - it.desgaste}% → 100%, gasta 1 kit de reparo)" : "Reparar (precisa de um kit de reparo)",
                      inventario.PodeReparar(id) ? () => inventario.Reparar(id) : (System.Action)null);
            if (!inventario.PodeDesmontar(id, out var d, out var falta) && d != null)
                Opcao($"{Maiuscula(d.verbo)}: {Receitas.Lista(d.vira)} ({falta})", null);
            else if (d != null)
                Opcao($"{Maiuscula(d.verbo)}: {Receitas.Lista(d.vira)}", () => inventario.Desmontar(id));
            if (Itens.EmPilha(item) && !Itens.EhMunicao(item) && n > 1)
            {
                Opcao("Largar um", () => inventario.LargarUm(id));
                Opcao($"Largar todos ({n})", () => LargarDoMenu(id));
            }
            else Opcao("Largar", () => LargarDoMenu(id));
        }

        void LargarDoMenu(ushort id)
        {
            inventario.Largar(id);
            if (_escolhido == id) _escolhido = 0;
        }

        Rect RetanguloDoMenu()
        {
            float u = _u, w = 160f * u;
            foreach (var o in _opcoes) w = Mathf.Max(w, _texto.CalcSize(new GUIContent(o.texto)).x + 28f * u);
            var r = new Rect(_menuEm.x + 2f * u, _menuEm.y + 2f * u, w, (_opcoes.Count * MenuLinha + 8f) * u);
            if (r.xMax > Screen.width) r.x = Screen.width - r.width;
            if (r.yMax > Screen.height) r.y = Screen.height - r.height;
            return r;
        }

        Rect LinhaDoMenu(Rect menu, int i) => new Rect(menu.x + 4f * _u, menu.y + (4f + i * MenuLinha) * _u, menu.width - 8f * _u, MenuLinha * _u);

        void DesenharMenu(Vector2 mouse)
        {
            if (!MenuAberto) return;
            float u = _u;
            var menu = RetanguloDoMenu();
            Ret(new Rect(menu.x + 4 * u, menu.y + 5 * u, menu.width, menu.height), new Color(0f, 0f, 0f, 0.4f));
            Ret(menu, new Color(0.07f, 0.07f, 0.065f, 0.96f));
            Moldura(menu, Mathf.Max(1f, Mathf.Round(u)), Friso);
            var estilo = new GUIStyle(_texto) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            for (int i = 0; i < _opcoes.Count; i++)
            {
                var r = LinhaDoMenu(menu, i);
                bool ativa = _opcoes[i].acao != null;
                if (ativa && r.Contains(mouse)) Ret(r, new Color(0.98f, 0.82f, 0.36f, 0.25f));
                PorCor(estilo, ativa ? CorDoTexto : new Color(0.6f, 0.58f, 0.54f, 0.8f));
                GUI.Label(new Rect(r.x + 10 * u, r.y, r.width - 12 * u, r.height), _opcoes[i].texto, estilo);
            }
        }

        void Soltar(Vector2 m)
        {
            if (_apertou == Origem.Mochila)
            {
                if (!inventario.Achar(_idApertado, out var it)) return;
                if (AtalhoEm(m, out int k)) inventario.PorNoAtalho(k, it.id);
                // uma pilha solta em cima de outra do mesmo item (a caixa de munição, do mesmo calibre): passa para ela
                else if (_aba == Aba.Mochila && Itens.EmPilha(it.item) && ItemEm(m, out var outra, out _) && outra.id != it.id && outra.item == it.item)
                    inventario.Juntar(it.id, outra.id);
                else if (CelulaDoArrasto(m, it.item, _girado, out int x, out int y))
                {
                    if (inventario.PodeMover(it.id, x, y, _girado) && (x != it.x || y != it.y || _girado != it.girado))
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
                Rodape();
            }
            else Criar(mouse);
            Detalhes();
            Arrasto(mouse);
            if (MenuAberto) _dicaTexto = null;
            Dica(mouse);
            DesenharMenu(mouse);
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
                PorCor(_titulo, ativa ? Destaque : TextoApagado);
                GUI.Label(a, nome, new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter });
                PorCor(_titulo, CorDoTexto);
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
            // a mochila: o ícone da vestida (arrastar para fora tira e larga no chão)
            foreach (var (r, nome, vestido) in new[] { (LugarDaMochila, "Mochila", inventario.MochilaVestida.Value) })
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
                    _dicaTexto = vestido != Item.Nada ? Maiuscula(Itens.Nome(vestido)) : "Sem mochila: só os bolsos";
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
                PorCor(_numero, naMao ? Destaque : TextoApagado);
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
            if (_arrastando && _apertou == Origem.Mochila && inventario.Achar(_idApertado, out var caixa) && Itens.EmPilha(caixa.item)
                && ItemEm(mouse, out var outra, out var ro) && outra.id != caixa.id && outra.item == caixa.item)
            {
                // juntar duas pilhas do mesmo item (as caixas, do mesmo calibre)
                Ret(ro, inventario.PodeJuntar(caixa.id, outra.id) ? new Color(0.35f, 0.85f, 0.35f, 0.4f) : new Color(0.9f, 0.25f, 0.2f, 0.35f));
            }
            else if (_arrastando && _apertou == Origem.Mochila && inventario.Achar(_idApertado, out var arrastado)
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
                var p = Posicao(it);
                var r = RetanguloNaGrade(p.x, p.y, InventarioJogador.TamanhoNaGrade(it.item, p.girado));
                bool sendoArrastado = _arrastando && _apertou == Origem.Mochila && _idApertado == it.id;
                DesenharItem(it, Encolher(r, 2 * u), p.girado, sendoArrastado ? 0.3f : 1f, it.id == _escolhido, r.Contains(mouse) && !_arrastando);
                if (r.Contains(mouse) && !_arrastando)
                {
                    _sobMouse = it.id;
                    _dicaTexto = Maiuscula(Itens.Nome(it.item)) + (Itens.EmPilha(it.item) ? $" ({Itens.Quantos(it.item, it.qtd)} de {Itens.Pilha(it.item)})" : Itens.ComEstado(it.item) ? $" ({it.qtd}%)" : "");
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
        /// Embaixo da grade: as dicas (o botão direito, G e R; as armas dos atalhos vão no corpo) e o aviso do que acabou
        /// de ser feito.
        /// </summary>
        void Rodape()
        {
            var a = AreaDoRodape;
            GUI.Label(Em(new Rect(a.x + 2, a.y, a.width, 20)), "Botão direito: opções   ·   G: largar   ·   R: girar", _texto);
            GUI.Label(Em(new Rect(a.x + 2, a.y + 24, a.width, 20)), "As armas dos atalhos 1, 2 e 3 ficam penduradas no corpo.", _pequeno);
            Aviso(new Rect(a.x + 2, a.y + 50, a.width, 22));
        }

        // o aviso do que foi feito (uma receita, uma desmontagem), por uns segundos
        void Aviso(Rect r)
        {
            if (_aviso == null || Time.unscaledTime > _avisoAte) return;
            var antes = _texto.normal.textColor;
            PorCor(_texto, Destaque);
            GUI.Label(Em(r), _aviso, _texto);
            PorCor(_texto, antes);
        }

        // um item: o fundo pela espécie (arma, comida, água), o ícone (girado 90° no sentido do relógio, se o item está
        // girado), o atalho em que ele está e o pente
        void DesenharItem(ItemNaMochila it, Rect r, bool girado, float alfa, bool escolhido, bool sobMouse)
        {
            float u = _u;
            var fundo = FundoDo(it.item);
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
                PorCor(_numero, Destaque);
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
            else if (Itens.EmPilha(it.item))
            {
                // quantos há na pilha, embaixo
                int n = Itens.Quantos(it.item, it.qtd);
                if (n > 1)
                {
                    var t = new Rect(r.xMax - 22 * u, r.yMax - 15 * u, 22 * u, 15 * u);
                    Ret(t, new Color(0f, 0f, 0f, 0.5f));
                    GUI.Label(new Rect(r.x, r.yMax - 17 * u, r.width - 4 * u, 16 * u), n.ToString(), _direita);
                }
            }
            else if (Itens.ComEstado(it.item))
            {
                // o estado da peça embaixo, na cor dele
                Ret(new Rect(r.x, r.yMax - 15 * u, r.width, 15 * u), new Color(0f, 0f, 0f, 0.45f));
                var antes = _direita.normal.textColor;
                PorCor(_direita, Vehicles.PecasDoMotor.CorDoEstado(it.qtd));
                GUI.Label(new Rect(r.x, r.yMax - 17 * u, r.width - 4 * u, 16 * u), it.qtd + "%", _direita);
                PorCor(_direita, antes);
            }
            // o estado da arma gasta: a barra fina em cima (verde, amarela, vermelha)
            if (Itens.ComDesgaste(it.item) && it.desgaste > 0)
            {
                float f = (100 - it.desgaste) / 100f;
                Ret(new Rect(r.x + 3 * u, r.y + 3 * u, (r.width - 24 * u), 4 * u), new Color(0f, 0f, 0f, 0.5f));
                Ret(new Rect(r.x + 3 * u, r.y + 3 * u, (r.width - 24 * u) * f, 4 * u),
                    f > 0.5f ? new Color(0.45f, 0.85f, 0.4f) : f > 0.3f ? new Color(0.95f, 0.8f, 0.3f) : new Color(1f, 0.38f, 0.3f));
            }
            if (escolhido) Moldura(r, Mathf.Max(1f, Mathf.Round(2 * u)), Destaque);
        }

        // o fundo de cada espécie de item: as armas, a munição, as peças e as do saque
        static Color FundoDo(Item item)
        {
            if (Itens.EhArma(item) || Itens.EhBranca(item)) return FundoDeArma;
            if (Itens.EhMunicao(item)) return FundoDeMunicao;
            if (Itens.EhPeca(item)) return FundoDePeca;
            if (Itens.EhSinuca(item)) return FundoDaSinuca;
            var l = Itens.Loot(item);
            if (l == null) return FundoDeEquipamento;
            return l.tipo == TipoDeLoot.Comida ? FundoDeComida : l.tipo == TipoDeLoot.Bebida ? FundoDeAgua : l.tipo == TipoDeLoot.Remedio ? FundoDeRemedio
                : l.tipo == TipoDeLoot.Construcao ? FundoDeConstrucao : FundoDeMaterial;
        }

        int ReceitasNaTela => Mathf.FloorToInt(AreaDasReceitas.height / LinhaDaReceita);

        Rect LinhaDaReceitaNaTela(int i) => Em(new Rect(AreaDasReceitas.x, AreaDasReceitas.y + i * LinhaDaReceita, AreaDasReceitas.width, LinhaDaReceita - 4));

        Rect BotaoDeCriar(Rect linha) => new Rect(linha.xMax - 84 * _u, linha.y + (linha.height - 30 * _u) / 2f, 76 * _u, 30 * _u);

        // a receita embaixo do mouse (e se no botão de criar)
        bool ReceitaEm(Vector2 m, out int receita, out bool noBotao)
        {
            receita = -1;
            noBotao = false;
            for (int i = 0; i < ReceitasNaTela && i + _rolagem < Receitas.Todas.Length; i++)
            {
                var l = LinhaDaReceitaNaTela(i);
                if (!l.Contains(m)) continue;
                receita = i + _rolagem;
                noBotao = BotaoDeCriar(l).Contains(m);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A aba Criar: uma linha por receita (o ícone do que sai, o nome, o que gasta, verde o que tem e vermelho o que
        /// falta, a ferramenta e a bancada) e o botão de criar, aceso quando dá. Embaixo, se há uma bancada perto.
        /// </summary>
        void Criar(Vector2 mouse)
        {
            float u = _u;
            Ret(Encolher(Em(AreaDasReceitas), -4 * u), Painel);
            var todas = Receitas.Todas;
            _rolagem = Mathf.Clamp(_rolagem, 0, Mathf.Max(0, todas.Length - ReceitasNaTela));
            var linha = new GUIStyle(_pequeno) { wordWrap = false };
            for (int i = 0; i < ReceitasNaTela && i + _rolagem < todas.Length; i++)
            {
                int k = i + _rolagem;
                var rec = todas[k];
                var l = LinhaDaReceitaNaTela(i);
                bool pode = inventario.PodeCriar(rec, out _);
                Ret(l, k == _receita ? new Color(0f, 0f, 0f, 0.32f) : l.Contains(mouse) ? new Color(0f, 0f, 0f, 0.22f) : new Color(0f, 0f, 0f, 0.14f));
                if (k == _receita) Moldura(l, Mathf.Max(1f, Mathf.Round(2 * u)), Destaque);
                var caixa = new Rect(l.x + 4 * u, l.y + 2 * u, l.height - 4 * u, l.height - 4 * u);
                Ret(caixa, FundoDo(rec.resultado));
                var icone = inventario.IconeDe(rec.resultado);
                if (icone != null) GUI.DrawTexture(Encolher(caixa, 2 * u), icone, ScaleMode.ScaleToFit);
                float x = caixa.xMax + 8 * u;
                GUI.Label(new Rect(x, l.y + 2 * u, l.width, 20 * u), Maiuscula(Itens.Nome(rec.resultado)) + (rec.qtd > 1 ? $" ×{rec.qtd}" : ""), new GUIStyle(_texto) { fontStyle = FontStyle.Bold, wordWrap = false });
                // o que gasta (verde o que já tem, vermelho o que falta; quanto tem, nos detalhes), a ferramenta e a
                // bancada, até o botão (o que não couber vira "...")
                var b = BotaoDeCriar(l);
                float y = l.y + 23 * u, limite = b.x - 8 * u;
                var partes = new List<(string texto, bool ok)>();
                foreach (var g in rec.gasta) partes.Add((Itens.ComNumero(g.item, g.qtd), inventario.Quantos(g.item) >= g.qtd));
                if (rec.ferramenta != Item.Nada) partes.Add(("+ " + Itens.Nome(rec.ferramenta), inventario.Quantos(rec.ferramenta) > 0));
                if (rec.bancada) partes.Add(("na bancada", Bancada.Perto(inventario.transform.position)));
                float reticencias = linha.CalcSize(new GUIContent("...")).x;
                for (int p = 0; p < partes.Count; p++)
                {
                    float largura = linha.CalcSize(new GUIContent(partes[p].texto)).x;
                    bool ultima = p == partes.Count - 1;
                    // a última precisa caber; as outras, com lugar para as reticências depois
                    if (x + largura + (ultima ? 0f : 10 * u + reticencias) > limite && !(ultima && x + largura <= limite))
                    {
                        PorCor(linha, TextoApagado);
                        GUI.Label(new Rect(x, y, reticencias + 4 * u, 18 * u), "...", linha);
                        break;
                    }
                    PorCor(linha, partes[p].ok ? Bom : Ruim);
                    GUI.Label(new Rect(x, y, largura + 2 * u, 18 * u), partes[p].texto, linha);
                    x += largura + 10 * u;
                }
                // o botão
                Ret(b, pode ? (b.Contains(mouse) ? new Color(0.98f, 0.82f, 0.36f, 0.9f) : new Color(0.78f, 0.62f, 0.2f, 0.85f)) : new Color(0f, 0f, 0f, 0.3f));
                Moldura(b, Mathf.Max(1f, Mathf.Round(u)), new Color(1f, 1f, 1f, pode ? 0.3f : 0.08f));
                var estilo = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(13 * u) };
                PorCor(estilo, pode ? new Color(0.1f, 0.09f, 0.07f) : TextoApagado);
                GUI.Label(b, "CRIAR", estilo);
            }
            // embaixo: a bancada (à esquerda) e, se há mais receitas que linhas, a dica de rolar (à direita)
            if (todas.Length > ReceitasNaTela)
            {
                string rolar = _rolagem + ReceitasNaTela < todas.Length ? "role para ver mais ▼" : "▲ role para voltar";
                GUI.Label(Em(new Rect(AreaDasReceitas.x, AreaDasReceitas.yMax + 6, AreaDasReceitas.width - 2, 18)), rolar,
                    new GUIStyle(_pequeno) { alignment = TextAnchor.UpperRight });
            }
            bool perto = Bancada.Perto(inventario.transform.position);
            var aviso = new GUIStyle(_pequeno) { wordWrap = false };
            PorCor(aviso, perto ? Bom : TextoApagado);
            GUI.Label(Em(new Rect(AreaDasReceitas.x + 2, AreaDasReceitas.yMax + 6, AreaDasReceitas.width - 140, 18)),
                perto ? "Bancada perto: todas as receitas valem." : "Longe de uma bancada (há uma no bunker).", aviso);
            Aviso(new Rect(AreaDasReceitas.x + 2, AreaDasReceitas.yMax + 26, AreaDasReceitas.width, 20));
        }

        // os detalhes da receita escolhida: o que sai, o resumo, o que gasta (e quanto tem), a ferramenta e onde
        void DetalhesDaReceita(Receita rec, Rect nome, Rect foto, Rect det)
        {
            float u = _u;
            GUI.Label(new Rect(nome.x + 10 * u, nome.y, nome.width - 20 * u, nome.height), Maiuscula(Itens.Nome(rec.resultado)) + (rec.qtd > 1 ? $" ×{rec.qtd}" : ""), _nome);
            var imagem = inventario.FotoDe(rec.resultado);
            if (imagem == null) imagem = inventario.IconeDe(rec.resultado);
            if (imagem != null) GUI.DrawTexture(Encolher(foto, 10 * u), imagem, ScaleMode.ScaleToFit);
            float x = det.x + 12 * u, w = det.width - 24 * u, y = det.y + 10 * u, linha = 22 * u;
            GUI.Label(new Rect(x, y, w, 20 * u), Itens.Resumo(rec.resultado), _texto);
            y += 30 * u;
            foreach (var g in rec.gasta)
            {
                int tem = inventario.Quantos(g.item);
                GUI.Label(new Rect(x, y, w, linha), $"Gasta {Itens.ComNumero(g.item, g.qtd)}", _texto);
                var v = new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
                PorCor(v, tem >= g.qtd ? Bom : Ruim);
                GUI.Label(new Rect(x, y, w, linha), $"tem {tem}", v);
                y += linha;
            }
            if (rec.ferramenta != Item.Nada)
            {
                GUI.Label(new Rect(x, y, w, linha), $"Ferramenta (não gasta): {Itens.Nome(rec.ferramenta)}", _texto);
                y += linha;
            }
            GUI.Label(new Rect(x, y, w, linha), rec.bancada ? "Só perto de uma bancada" : "Em qualquer lugar", _texto);
            y += linha + 4 * u;
            if (!inventario.PodeCriar(rec, out var falta))
            {
                var f = new GUIStyle(_texto);
                PorCor(f, Ruim);
                GUI.Label(new Rect(x, y, w, 40 * u), falta, f);
            }
        }

        // as receitas que gastam o item ("bandagem, corda e cama"; vazio: nenhuma)
        static string ServePara(Item item)
        {
            var nomes = new List<string>();
            foreach (var r in Receitas.Todas)
                foreach (var g in r.gasta)
                    if (g.item == item && !nomes.Contains(Itens.Nome(r.resultado))) nomes.Add(Itens.Nome(r.resultado));
            return nomes.Count <= 1 ? (nomes.Count == 1 ? nomes[0] : "") : string.Join(", ", nomes.GetRange(0, nomes.Count - 1)) + " e " + nomes[nomes.Count - 1];
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
            if (_aba == Aba.Criar && _receita >= 0 && _receita < Receitas.Todas.Length)
            {
                DetalhesDaReceita(Receitas.Todas[_receita], nome, foto, det);
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
                var negrito = new GUIStyle(_texto) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold, wordWrap = false };
                float cabe = w - _texto.CalcSize(new GUIContent(rotulo)).x - 12 * u;
                if (negrito.CalcSize(new GUIContent(valor)).x > cabe)
                {
                    // o valor comprido vai embaixo do rótulo, quebrando as linhas
                    var embaixo = new GUIStyle(negrito) { alignment = TextAnchor.UpperLeft, wordWrap = true };
                    float altura = embaixo.CalcHeight(new GUIContent(valor), w);
                    GUI.Label(new Rect(x, y + linha - 2 * u, w, altura), valor, embaixo);
                    y += altura;
                }
                else GUI.Label(new Rect(x, y, w, linha), valor, negrito);
                Ret(new Rect(x, y + linha - 3 * u, w, 1), new Color(1f, 1f, 1f, 0.07f));
                y += linha;
            }
            var t = Itens.Tamanho(item);
            if (Itens.EhArma(item))
            {
                var d = Armas.Dados(Itens.Arma(item));
                Linha("Pente", $"{arma.MunicaoDe(it.id, item)} / {d.capacidade}");
                Linha("Munição", $"{Itens.NomeDoCalibre(d.calibre)}  ·  {inventario.BalasDe(d.calibre)} nas caixas");
                Linha("Dano", d.faixas != null && d.faixas.Length > 0 ? $"{Numero(d.faixas[0].dano)} de perto" : Numero(d.dano));
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
                else if (Itens.EhChave(item))
                {
                    Linha("Serve para", "dar a partida na perua");
                    Linha("Sem ela", "só com a ligação direta");
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
            else if (Itens.EhCaixaDeBolas(item))
            {
                Linha("Bolas", item == Item.CaixaDeBolas ? "as 16" : "nenhuma (estão numa mesa)");
                Linha("Na mesa de sinuca", item == Item.CaixaDeBolas ? "E: pôr as bolas" : "E: recolher as bolas");
            }
            else if (Itens.EhTaco(item))
            {
                Linha("Serve para", "jogar sinuca");
                Linha("Na mão, na mesa", "E: jogar");
            }
            else if (Itens.EhLoot(item))
            {
                var l = Itens.Loot(item);
                if (l.pilha > 1) Linha("Na pilha", $"{Itens.Quantos(item, it.qtd)} de {l.pilha}");
                if (l.fome != 0f) Linha("Fome", (l.fome > 0f ? "+" : "") + Numero(l.fome));
                if (l.sede != 0f) Linha("Sede", (l.sede > 0f ? "+" : "") + Numero(l.sede));
                if (l.vida > 0f) Linha("Vida", "+" + Numero(l.vida));
                if (l.verbo != null) Linha(Maiuscula(l.verbo), l.mao >= 0 ? $"{Numero(l.tempoDeUso)} s na mão" : "pelo menu");
                string serve = ServePara(item);
                if (serve.Length > 0) Linha("Serve para", serve);
            }
            if (Itens.ComDesgaste(item))
                Linha("Estado", $"{100 - it.desgaste}%" + (it.desgaste >= 100 ? " (quebrada)" : it.desgaste >= Itens.DesgasteQueEmperra && Itens.EhArma(item) ? " (emperra)" : ""));
            var desmonta = Receitas.DesmontagemDe(item);
            if (desmonta != null) Linha(Maiuscula(desmonta.verbo), Receitas.Lista(desmonta.vira));
            if (!Itens.EhLoot(item)) Linha("Tamanho", $"{t.x} × {t.y}");
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
            var e = Itens.EspacoDa(item);
            Linha("Espaço", $"{e.x} × {e.y}  (bolsos: {Itens.Bolsos.x} × {Itens.Bolsos.y})");
            y += 6 * u;
            GUI.Label(new Rect(x, y, w, 40 * u), "Para tirar: G, ou arraste para fora da janela.", _pequeno);
        }

        void Arrasto(Vector2 mouse)
        {
            if (!_arrastando) return;
            float u = _u;
            if (_apertou == Origem.Mochila && inventario.Achar(_idApertado, out var it))
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

        // a cor do texto em todos os estados do estilo: o rótulo debaixo do mouse usa o "hover", que ficava com a cor
        // padrão da Unity (o texto das receitas mudava de cor ao passar o mouse)
        internal static void PorCor(GUIStyle s, Color c)
        {
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
        }

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
                PorCor(s, cor);
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
