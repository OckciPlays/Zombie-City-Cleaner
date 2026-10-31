using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Roupas;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A tela de criar o personagem (o Plano_Personagem.md da pasta de arte, a parte 2; a imagem do usuário,
    /// Imgs/Personagem_Criacao_Referencia.webp): aparece para o dono quando ele entra na partida, uma vez (outra partida,
    /// outro personagem), antes de ele aparecer no mundo. OnGUI, como as outras telas do jogo, no estilo da imagem (os
    /// painéis escuros, o destaque vermelho e as letras condensadas da Bahnschrift, a do Windows; sem ela, a Arial).
    ///  - à esquerda: o nome, o sexo, a cor da pele, o cabelo, a cor do cabelo e da barba e a barba (só no homem);
    ///  - à direita: a camiseta e a calça;
    ///  - no meio, o personagem (<see cref="EstudioDoPersonagem"/>), que gira arrastando o mouse;
    ///  - embaixo: R (ou o botão) sorteia tudo, e Confirmar (com o nome escrito) cria o personagem: a aparência vai pela
    ///    rede (<see cref="JogadorRede.Criar"/>) e a tela fecha.
    /// Com ela aberta, o jogador não recebe comandos, e ESC abre o menu (de onde se sai da partida): ela não fecha sem
    /// criar (<see cref="MenuOnline.Tela"/>, obrigatória).
    /// </summary>
    public class TelaDeCriarPersonagem : MonoBehaviour
    {
        [Tooltip("O parado do corpo no estúdio (o controlador do mostruário das roupas).")]
        public RuntimeAnimatorController controladorDoEstudio;

        /// <summary>A tela está aberta (no dono).</summary>
        public static bool Aberta { get; private set; }

        // as cores da imagem do usuário
        static readonly Color Fundo = new Color(0.075f, 0.07f, 0.068f);
        static readonly Color Painel = new Color(0.105f, 0.1f, 0.098f, 0.94f);
        static readonly Color Borda = new Color(0.2f, 0.19f, 0.18f, 1f);
        static readonly Color Vermelho = new Color(0.72f, 0.14f, 0.15f, 1f);
        static readonly Color Claro = new Color(0.9f, 0.87f, 0.82f, 1f);
        static readonly Color Apagado = new Color(0.55f, 0.53f, 0.5f, 1f);

        JogadorRede _rede;
        EstudioDoPersonagem _estudio;
        AparenciaDoJogador _escolha;
        string _nome = "";
        bool _digitando, _miniaturasSujas, _arrastando;
        int _paginaDoCabelo, _paginaDaBarba;
        Texture2D[] _cabelos, _barbas, _camisetas, _calcas;
        AparenciaDoJogador _dasMiniaturas;
        // (nada disso é guardado pela Unity: a fonte é do sistema, criada na hora)
        [System.NonSerialized] Texture2D _brilho;
        [System.NonSerialized] Font _fonte;
        [System.NonSerialized] GUIStyle _titulo, _rotulo, _texto, _campo, _botao, _botaoGrande, _pequeno;
        float _uDosEstilos;

        /// <summary>Abre a tela (no dono, ao entrar na partida), com uma aparência ao acaso para começar.</summary>
        public void Abrir()
        {
            if (Aberta) return;
            _rede = GetComponent<JogadorRede>();
            int tons = _rede.Vestimenta != null && _rede.Vestimenta.Catalogo.peles != null ? _rede.Vestimenta.Catalogo.peles.Length : 6;
            _escolha = AparenciaDoJogador.Sortear(new System.Random(), "", tons);
            _nome = "";
            float u = Unidade();
            _estudio = new EstudioDoPersonagem(_rede, controladorDoEstudio, Mathf.RoundToInt(AreaDoCorpo(u).width), Mathf.RoundToInt(AreaDoCorpo(u).height));
            _estudio.Vestir(_escolha);
            _miniaturasSujas = true;
            Paginas();
            Aberta = true;
            MenuOnline.Tela(this, true, true);
        }

        /// <summary>Cria o personagem com o que está escolhido (o nome precisa estar escrito).</summary>
        public void Confirmar()
        {
            string nome = AparenciaDoJogador.Cortar(_nome);
            if (!Aberta || nome.Length == 0) return;
            _escolha.nome = new Unity.Collections.FixedString32Bytes(nome);
            _rede.Criar(_escolha);
            Fechar();
        }

        /// <summary>Para testes: escolhe o nome e a aparência sem a tela (os índices das listas da AparenciaDoJogador).</summary>
        public void Escolher(string nome, AparenciaDoJogador a)
        {
            _nome = nome;
            _escolha = a;
            if (_estudio != null) _estudio.Vestir(_escolha);
            _miniaturasSujas = true;
            Paginas();
        }

        void Fechar()
        {
            Aberta = false;
            MenuOnline.Tela(this, false);
            _estudio?.Destruir();
            _estudio = null;
            foreach (var lista in new[] { _cabelos, _barbas, _camisetas, _calcas })
                if (lista != null) foreach (var t in lista) if (t != null) Destroy(t);
            _cabelos = _barbas = _camisetas = _calcas = null;
        }

        void OnDestroy()
        {
            if (Aberta) Fechar();
            if (_brilho != null) Destroy(_brilho);
        }

        static float Unidade() => Mathf.Min(Screen.height / 720f, Screen.width / 1280f);

        // onde o corpo fica (entre as duas colunas)
        static Rect AreaDoCorpo(float u)
        {
            float x0 = 400f * u, x1 = Screen.width - 420f * u;
            return new Rect(x0, 70f * u, Mathf.Max(100f, x1 - x0), Screen.height - 160f * u);
        }

        void Update()
        {
            if (!Aberta || _estudio == null) return;
            _estudio.Atualizar();
            var teclado = Keyboard.current;
            if (teclado != null && teclado.rKey.wasPressedThisFrame && !_digitando && !MenuOnline.Pausado) Aleatorio();
            if (_miniaturasSujas) Miniaturas();
        }

        void Aleatorio()
        {
            int tons = _estudio.Catalogo?.peles?.Length ?? 6;
            _escolha = AparenciaDoJogador.Sortear(new System.Random(), "", tons);
            _estudio.Vestir(_escolha);
            _miniaturasSujas = true;
            Paginas();
        }

        // os carrosséis mostram a página da escolhida
        void Paginas()
        {
            _paginaDoCabelo = _escolha.cabelo / 4;
            _paginaDaBarba = _escolha.barba / 4;
        }

        void Mudou(AparenciaDoJogador nova)
        {
            if (nova.Equals(_escolha)) return;
            if (nova.EhMulher) nova.barba = 0;
            _escolha = nova;
            _estudio.Vestir(_escolha);
            // as miniaturas da cabeça mudam com a pele, a cor dos pelos e o corpo; as da roupa, com o corpo
            if (nova.mulher != _dasMiniaturas.mulher || nova.pele != _dasMiniaturas.pele || nova.corDosPelos != _dasMiniaturas.corDosPelos)
                _miniaturasSujas = true;
        }

        // as miniaturas das opções (fora do OnGUI: as câmeras desenham aqui), e o corpo volta a vestir a escolha
        void Miniaturas()
        {
            _miniaturasSujas = false;
            var catalogo = _estudio.Catalogo;
            if (catalogo == null) return;
            var a = _escolha;
            _dasMiniaturas = a;
            List<CatalogoDasRoupas.PecaVestida> Da(AparenciaDoJogador x, bool cabelo, bool barba, bool camiseta, bool calca)
            {
                var todas = x.Pecas(catalogo);
                return todas.FindAll(p =>
                    (cabelo && System.Array.IndexOf(AparenciaDoJogador.Cabelos, p.nome) > 0) || (cabelo && p.nome == "Cilios") ||
                    (barba && System.Array.IndexOf(AparenciaDoJogador.Barbas, p.nome) > 0) ||
                    (camiseta && p.nome == "Camiseta") ||
                    (calca && System.Array.Exists(AparenciaDoJogador.Calcas, c => c.peca == p.nome)));
            }
            _cabelos ??= new Texture2D[AparenciaDoJogador.Cabelos.Length];
            for (int i = 0; i < _cabelos.Length; i++)
            {
                var x = a; x.cabelo = (byte)i; x.barba = 0;
                _cabelos[i] = _estudio.Miniatura(x, Da(x, true, false, false, false), EstudioDoPersonagem.Parte.Cabeca, _cabelos[i]);
            }
            _barbas ??= new Texture2D[AparenciaDoJogador.Barbas.Length];
            for (int i = 0; i < _barbas.Length; i++)
            {
                var x = a; x.mulher = 0; x.barba = (byte)i;
                _barbas[i] = _estudio.Miniatura(x, Da(x, true, true, false, false), EstudioDoPersonagem.Parte.Cabeca, _barbas[i]);
            }
            _camisetas ??= new Texture2D[AparenciaDoJogador.Camisetas.Length];
            for (int i = 0; i < _camisetas.Length; i++)
            {
                var x = a; x.camiseta = (byte)i;
                _camisetas[i] = _estudio.Miniatura(x, Da(x, false, false, true, false), EstudioDoPersonagem.Parte.Tronco, _camisetas[i]);
            }
            _calcas ??= new Texture2D[AparenciaDoJogador.Calcas.Length];
            for (int i = 0; i < _calcas.Length; i++)
            {
                var x = a; x.calca = (byte)i;
                _calcas[i] = _estudio.Miniatura(x, Da(x, false, false, false, true), EstudioDoPersonagem.Parte.Pernas, _calcas[i]);
            }
            _estudio.Vestir(_escolha);
        }

        // ------------------------------------------------------------------ o desenho

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            if (_fonte == null)
                // (só o nome da família: com "Bahnschrift SemiBold Condensed", que não existe como fonte, a Unity não carrega
                // a face e a fonte fica presa num tamanho só, desenhada no canto de cada caixa)
                _fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 16);
            GUIStyle Estilo(GUIStyle de, int tamanho, FontStyle jeito, Color cor, TextAnchor ancora)
            {
                var s = new GUIStyle(de) { font = _fonte, fontSize = Mathf.RoundToInt(tamanho * u), fontStyle = jeito, alignment = ancora };
                s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = cor;
                return s;
            }
            _titulo = Estilo(GUI.skin.label, 40, FontStyle.Bold, Claro, TextAnchor.MiddleLeft);
            _rotulo = Estilo(GUI.skin.label, 19, FontStyle.Bold, Claro, TextAnchor.UpperLeft);
            _texto = Estilo(GUI.skin.label, 16, FontStyle.Normal, Apagado, TextAnchor.MiddleLeft);
            _campo = Estilo(GUI.skin.textField, 22, FontStyle.Bold, Claro, TextAnchor.MiddleLeft);
            _campo.normal.background = _campo.focused.background = _campo.hover.background = Texture2D.blackTexture;
            _campo.padding = new RectOffset(Mathf.RoundToInt(10 * u), 4, 2, 2);
            _botao = Estilo(GUI.skin.label, 18, FontStyle.Bold, Claro, TextAnchor.MiddleCenter);
            _botaoGrande = Estilo(GUI.skin.label, 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            _pequeno = Estilo(GUI.skin.label, 13, FontStyle.Bold, Claro, TextAnchor.MiddleCenter);
            _pequeno.wordWrap = false;
            _pequeno.clipping = TextClipping.Overflow;
        }

        void OnGUI()
        {
            if (!Aberta || _estudio == null) return;
            GUI.depth = -15;   // por cima do HUD, do F1 e do mapa; embaixo do menu (ESC) e da tela de carregamento
            float u = Unidade();
            Estilos(u);
            GUI.enabled = !MenuOnline.Pausado;
            Retangulo(new Rect(0, 0, Screen.width, Screen.height), Fundo);
            // a luz de cima, quente, como a lâmpada da imagem
            _brilho ??= Brilho();
            GUI.color = new Color(1f, 0.82f, 0.6f, 0.22f);
            float lb = Screen.width * 0.7f;
            GUI.DrawTexture(new Rect((Screen.width - lb) / 2f, -lb * 0.42f, lb, lb), _brilho);
            GUI.color = Color.white;

            GUI.Label(new Rect(40 * u, 16 * u, 700 * u, 50 * u), "CRIAR PERSONAGEM", _titulo);

            // o personagem no meio, que gira arrastando
            var area = AreaDoCorpo(u);
            var img = _estudio.Imagem;
            if (img != null)
            {
                float alto = area.height, largo = alto * img.width / img.height;
                if (largo > area.width) { largo = area.width; alto = largo * img.height / img.width; }
                var r = new Rect(area.center.x - largo / 2f, area.center.y - alto / 2f, largo, alto);
                // a sombra no chão
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(r.center.x - 110 * u, r.yMax - 26 * u, 220 * u, 30 * u), _brilho);
                GUI.color = Color.white;
                GUI.DrawTexture(r, img, ScaleMode.StretchToFill, true);
            }
            var e = Event.current;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { _arrastando = true; e.Use(); }
            else if (_arrastando && e.type == EventType.MouseDrag) { _estudio.Giro -= e.delta.x * 0.6f / u; e.Use(); }
            else if (e.rawType == EventType.MouseUp) _arrastando = false;

            var nova = _escolha;
            ColunaDaEsquerda(u, ref nova);
            ColunaDaDireita(u, ref nova);
            if (GUI.enabled) Mudou(nova);

            // embaixo: o girar, o aleatório e o confirmar
            float yb = Screen.height - 70 * u;
            GUI.Label(new Rect(area.x, yb, 300 * u, 40 * u), "ARRASTE: GIRAR", _texto);
            var aleatorio = new Rect(area.x + 170 * u, yb + 2 * u, 170 * u, 36 * u);
            Retangulo(aleatorio, Painel);
            Moldura(aleatorio, Borda, u);
            if (GUI.Button(aleatorio, "R  ALEATÓRIO", _botao)) Aleatorio();
            bool podeCriar = AparenciaDoJogador.Cortar(_nome).Length > 0;
            var confirmar = new Rect(Screen.width - 300 * u, Screen.height - 82 * u, 260 * u, 58 * u);
            Retangulo(confirmar, podeCriar ? Vermelho : new Color(0.3f, 0.12f, 0.12f, 1f));
            Moldura(confirmar, podeCriar ? new Color(0.95f, 0.35f, 0.33f) : Borda, u);
            var antes = GUI.enabled;
            GUI.enabled = antes && podeCriar;
            if (GUI.Button(confirmar, "CONFIRMAR", _botaoGrande)) Confirmar();
            GUI.enabled = antes;
            if (!podeCriar) GUI.Label(new Rect(confirmar.x - 40 * u, confirmar.y - 28 * u, 300 * u, 24 * u), "Escreva o nome do personagem", _texto);
            GUI.enabled = true;
        }

        void ColunaDaEsquerda(float u, ref AparenciaDoJogador a)
        {
            float x = 40 * u, w = 330 * u, y = 78 * u;

            // o nome
            var p = Caixa(x, y, w, 76 * u, "NOME", u);
            GUI.SetNextControlName("NomeDoPersonagem");
            var campo = new Rect(p.x, p.y, p.width, 36 * u);
            Retangulo(campo, new Color(0.05f, 0.05f, 0.05f, 1f));
            Moldura(campo, _digitando ? Vermelho : Borda, u);
            _nome = GUI.TextField(campo, _nome, AparenciaDoJogador.LetrasDoNome, _campo);
            _digitando = GUI.GetNameOfFocusedControl() == "NomeDoPersonagem";
            y += 84 * u;

            // o sexo
            p = Caixa(x, y, w, 80 * u, "SEXO", u);
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(p.x + i * 150 * u, p.y, 140 * u, 40 * u);
                bool escolhido = (a.mulher != 0) == (i == 1);
                if (Opcao(r, escolhido, u, null, i == 0 ? "HOMEM" : "MULHER")) a.mulher = (byte)i;
            }
            y += 88 * u;

            // a cor da pele
            var peles = _estudio.Catalogo?.peles ?? new string[0];
            p = Caixa(x, y, w, 76 * u, "COR DA PELE", u);
            for (int i = 0; i < peles.Length; i++)
                if (Amostra(new Rect(p.x + i * 46 * u, p.y, 38 * u, 38 * u), CatalogoDasRoupas.Cor(peles[i]), a.pele == i, u)) a.pele = (byte)i;
            y += 84 * u;

            // o cabelo (8, de 4 em 4)
            p = Caixa(x, y, w, 110 * u, "CABELO", u);
            a.cabelo = (byte)Carrossel(p, _cabelos, AparenciaDoJogador.Cabelos.Length, a.cabelo, ref _paginaDoCabelo, u, "CARECA");
            y += 118 * u;

            // a cor do cabelo e da barba
            p = Caixa(x, y, w, 68 * u, "COR DO CABELO E DA BARBA", u);
            for (int i = 0; i < AparenciaDoJogador.CoresDosPelos.Length; i++)
                if (Amostra(new Rect(p.x + i * 39 * u, p.y, 32 * u, 32 * u), CatalogoDasRoupas.Cor(AparenciaDoJogador.CoresDosPelos[i].cor), a.corDosPelos == i, u))
                    a.corDosPelos = (byte)i;
            y += 76 * u;

            // a barba (só no homem)
            p = Caixa(x, y, w, 110 * u, "BARBA", u);
            if (a.mulher != 0) GUI.Label(new Rect(p.x, p.y, p.width, 64 * u), "SÓ NO HOMEM", _texto);
            else a.barba = (byte)Carrossel(p, _barbas, AparenciaDoJogador.Barbas.Length, a.barba, ref _paginaDaBarba, u, "SEM");
        }

        void ColunaDaDireita(float u, ref AparenciaDoJogador a)
        {
            float w = 380 * u, x = Screen.width - 40 * u - w, y = 78 * u;
            var p = Caixa(x, y, w, 222 * u, "CAMISETA", u);
            a.camiseta = (byte)Grade(p, _camisetas, AparenciaDoJogador.Camisetas.Length, a.camiseta, u, i => AparenciaDoJogador.Camisetas[i].nome);
            y += 232 * u;
            p = Caixa(x, y, w, 222 * u, "CALÇA", u);
            a.calca = (byte)Grade(p, _calcas, AparenciaDoJogador.Calcas.Length, a.calca, u, i => AparenciaDoJogador.Calcas[i].nome);
        }

        // um painel com o título; devolve onde o conteúdo vai
        Rect Caixa(float x, float y, float w, float h, string titulo, float u)
        {
            var r = new Rect(x, y, w, h);
            Retangulo(r, Painel);
            Moldura(r, Borda, u);
            GUI.Label(new Rect(x + 14 * u, y + 6 * u, w - 28 * u, 26 * u), titulo, _rotulo);
            return new Rect(x + 14 * u, y + 34 * u, w - 28 * u, h - 42 * u);
        }

        // as opções em fila, 4 por vez, com as setas dos lados (o cabelo e a barba)
        int Carrossel(Rect p, Texture2D[] miniaturas, int quantas, int escolhida, ref int pagina, float u, string vazia)
        {
            const int PorVez = 4;
            float lado = 62 * u, vao = 8 * u, seta = 22 * u;
            int paginas = (quantas + PorVez - 1) / PorVez;
            pagina = Mathf.Clamp(pagina, 0, paginas - 1);
            if (GUI.Button(new Rect(p.x, p.y + lado / 2f - 16 * u, seta, 32 * u), "‹", _botaoGrande)) pagina = (pagina + paginas - 1) % paginas;
            if (GUI.Button(new Rect(p.x + seta + PorVez * (lado + vao), p.y + lado / 2f - 16 * u, seta, 32 * u), "›", _botaoGrande)) pagina = (pagina + 1) % paginas;
            for (int k = 0; k < PorVez; k++)
            {
                int i = pagina * PorVez + k;
                if (i >= quantas) break;
                var r = new Rect(p.x + seta + k * (lado + vao), p.y, lado, lado);
                var t = miniaturas != null && i < miniaturas.Length ? miniaturas[i] : null;
                if (Opcao(r, escolhida == i, u, t, i == 0 ? vazia : null)) escolhida = i;
            }
            return escolhida;
        }

        // as opções em grade, 4 por linha (a camiseta e a calça), com o nome da escolhida embaixo
        int Grade(Rect p, Texture2D[] miniaturas, int quantas, int escolhida, float u, System.Func<int, string> nome)
        {
            float lado = 76 * u, vao = (p.width - 4 * lado) / 3f;
            for (int i = 0; i < quantas; i++)
            {
                var r = new Rect(p.x + (i % 4) * (lado + vao), p.y + (i / 4) * (lado + 6 * u), lado, lado);
                var t = miniaturas != null && i < miniaturas.Length ? miniaturas[i] : null;
                if (Opcao(r, escolhida == i, u, t, null)) escolhida = i;
            }
            GUI.Label(new Rect(p.x, p.yMax - 18 * u, p.width, 22 * u), nome(escolhida).ToUpperInvariant(), _texto);
            return escolhida;
        }

        // uma opção: o quadrado escuro, a miniatura (ou o texto) e, na escolhida, a borda vermelha
        bool Opcao(Rect r, bool escolhida, float u, Texture2D miniatura, string texto)
        {
            Retangulo(r, escolhida ? new Color(0.17f, 0.11f, 0.1f, 1f) : new Color(0.07f, 0.07f, 0.07f, 1f));
            if (miniatura != null && texto == null) GUI.DrawTexture(new Rect(r.x + 2 * u, r.y + 2 * u, r.width - 4 * u, r.height - 4 * u), miniatura, ScaleMode.ScaleToFit, true);
            else if (miniatura != null) { GUI.color = new Color(1f, 1f, 1f, 0.55f); GUI.DrawTexture(r, miniatura, ScaleMode.ScaleToFit, true); GUI.color = Color.white; }
            if (texto != null) GUI.Label(r, texto, _pequeno);
            Moldura(r, escolhida ? Vermelho : Borda, escolhida ? u * 1.6f : u);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        bool Amostra(Rect r, Color cor, bool escolhida, float u)
        {
            Retangulo(r, cor);
            Moldura(r, escolhida ? Vermelho : new Color(0f, 0f, 0f, 0.6f), escolhida ? u * 2.2f : u);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
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
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Brilho da tela" };
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
    }
}
