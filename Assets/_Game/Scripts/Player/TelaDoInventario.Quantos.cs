using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A escolha de quantos largar (17/11/2026, a pedido: "dropar quantidade de loot"): no botão direito de uma pilha,
    /// "Largar quantos..." (e na caixa de munição, "Largar quantas balas..."), abre um quadro no meio da tela com o número,
    /// a barra (clique ou arraste), os passos (-10, -1, +1, +10, metade e tudo), a roda do mouse e o teclado (os números,
    /// as setas, o Backspace e o Enter). "Largar" manda o pedido ao anfitrião (<see cref="InventarioJogador.LargarQuantos"/>);
    /// o clique fora, o "Cancelar" ou o ESC (que fecha a tela) desistem. Enquanto ele está aberto, o resto da tela não
    /// recebe o mouse nem as teclas.
    /// </summary>
    public partial class TelaDoInventario
    {
        ushort _quantosId;
        int _quantos = 1;
        bool _quantosDigitando, _quantosArrastando;
        GUIStyle _quantosGrande, _quantosBotao;
        float _uDoQuantos = -1f;

        static readonly (string texto, int passo)[] PassosDoQuantos =
        {
            ("-10", -10), ("-1", -1), ("+1", 1), ("+10", 10), ("Metade", 0), ("Tudo", int.MaxValue),
        };

        // as teclas de 0 a 9 (no enum, o Digit0 vem depois do Digit9) e as do teclado numérico
        static readonly Key[] Digitos = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] Teclado = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        bool QuantosAberto => _quantosId != 0;

        void AbrirQuantos(ushort id)
        {
            if (!inventario.Achar(id, out var it) || !Itens.EmPilha(it.item)) return;
            int max = Itens.Quantos(it.item, it.qtd);
            if (max < 2) return;
            _quantosId = id;
            _quantos = Mathf.Max(1, max / 2);
            _quantosDigitando = false;
            _quantosArrastando = false;
        }

        void FecharQuantos()
        {
            _quantosId = 0;
            _quantosArrastando = false;
            _quantosDigitando = false;
        }

        // quantos tem a pilha agora (ela pode mudar com a escolha aberta); 0: o item saiu da mochila
        int MaximoDoQuantos(out ItemNaMochila it)
        {
            if (!inventario.Achar(_quantosId, out it) || !Itens.EmPilha(it.item)) return 0;
            return Itens.Quantos(it.item, it.qtd);
        }

        void ConfirmarQuantos()
        {
            int max = MaximoDoQuantos(out _);
            if (max > 0)
            {
                int n = Mathf.Clamp(_quantos, 1, max);
                inventario.LargarQuantos(_quantosId, n);
                if (n >= max && _escolhido == _quantosId) _escolhido = 0;
            }
            FecharQuantos();
        }

        void PassoDoQuantos(int passo, int max)
        {
            _quantosDigitando = false;
            if (passo == 0) _quantos = Mathf.Max(1, max / 2);
            else if (passo == int.MaxValue) _quantos = max;
            else _quantos = Mathf.Clamp(_quantos + passo, 1, max);
        }

        void QuantosPelaBarra(float x, Rect barra, int max)
        {
            _quantosDigitando = false;
            float t = Mathf.Clamp01((x - barra.x) / Mathf.Max(1f, barra.width));
            _quantos = Mathf.Clamp(1 + Mathf.RoundToInt(t * (max - 1)), 1, max);
        }

        // ------------------------------------------------------------------ as medidas (em pixels da tela)

        Rect PainelDoQuantos
        {
            get
            {
                float u = _u, w = 380f * u, h = 214f * u;
                var j = Janela;
                return new Rect(Mathf.Round(j.center.x - w / 2f), Mathf.Round(j.center.y - h / 2f), w, h);
            }
        }

        Rect BarraDoQuantos(Rect p) => new Rect(p.x + 22f * _u, p.y + 98f * _u, p.width - 44f * _u, 14f * _u);

        Rect BotaoDoPasso(Rect p, int i)
        {
            float u = _u, vao = 6f * u, w = (p.width - 36f * u - vao * (PassosDoQuantos.Length - 1)) / PassosDoQuantos.Length;
            return new Rect(p.x + 18f * u + i * (w + vao), p.y + 124f * u, w, 26f * u);
        }

        Rect BotaoCancelarDoQuantos(Rect p) => new Rect(p.x + 18f * _u, p.yMax - 40f * _u, (p.width - 46f * _u) / 2f, 28f * _u);
        Rect BotaoLargarDoQuantos(Rect p) => new Rect(p.center.x + 5f * _u, p.yMax - 40f * _u, (p.width - 46f * _u) / 2f, 28f * _u);

        // ------------------------------------------------------------------ o mouse e o teclado

        /// <summary>Com a escolha aberta, os eventos do mouse são dela (devolve true: o Processar não segue).</summary>
        bool ProcessarQuantos(Event e)
        {
            if (!QuantosAberto) return false;
            int max = MaximoDoQuantos(out _);
            if (max < 1)
            {
                FecharQuantos();
                return false;
            }
            var p = PainelDoQuantos;
            var m = e.mousePosition;
            var barra = BarraDoQuantos(p);
            var pegaDaBarra = new Rect(barra.x - 6f * _u, barra.y - 8f * _u, barra.width + 12f * _u, barra.height + 16f * _u);
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    if (!p.Contains(m)) FecharQuantos();
                    else if (pegaDaBarra.Contains(m))
                    {
                        _quantosArrastando = true;
                        QuantosPelaBarra(m.x, barra, max);
                    }
                    else if (BotaoLargarDoQuantos(p).Contains(m)) ConfirmarQuantos();
                    else if (BotaoCancelarDoQuantos(p).Contains(m)) FecharQuantos();
                    else
                        for (int i = 0; i < PassosDoQuantos.Length; i++)
                            if (BotaoDoPasso(p, i).Contains(m)) PassoDoQuantos(PassosDoQuantos[i].passo, max);
                    break;
                case EventType.MouseDown when e.button == 1:
                    if (!p.Contains(m)) FecharQuantos();
                    break;
                case EventType.MouseDrag when _quantosArrastando:
                    QuantosPelaBarra(m.x, barra, max);
                    break;
                case EventType.MouseUp:
                    _quantosArrastando = false;
                    break;
                case EventType.ScrollWheel:
                    PassoDoQuantos(e.delta.y > 0f ? -1 : 1, max);
                    break;
            }
            if (e.isMouse || e.isScrollWheel) e.Use();
            return true;
        }

        /// <summary>No Update: com a escolha aberta, as teclas são dela (devolve true: as outras teclas da tela ficam de fora).</summary>
        bool TeclasDoQuantos(Keyboard t)
        {
            if (!QuantosAberto) return false;
            var mouse = Mouse.current;
            if (_quantosArrastando && mouse != null && !mouse.leftButton.isPressed) _quantosArrastando = false;
            int max = MaximoDoQuantos(out _);
            if (max < 1)
            {
                FecharQuantos();
                return false;
            }
            if (t == null) return true;
            if (t.enterKey.wasPressedThisFrame || t.numpadEnterKey.wasPressedThisFrame)
            {
                ConfirmarQuantos();
                return true;
            }
            if (t.upArrowKey.wasPressedThisFrame || t.rightArrowKey.wasPressedThisFrame) PassoDoQuantos(1, max);
            if (t.downArrowKey.wasPressedThisFrame || t.leftArrowKey.wasPressedThisFrame) PassoDoQuantos(-1, max);
            if (t.backspaceKey.wasPressedThisFrame)
            {
                _quantos = _quantosDigitando ? _quantos / 10 : 0;
                _quantosDigitando = true;
            }
            for (int d = 0; d <= 9; d++)
            {
                if (!t[Digitos[d]].wasPressedThisFrame && !t[Teclado[d]].wasPressedThisFrame) continue;
                _quantos = _quantosDigitando ? Mathf.Min(_quantos * 10 + d, max) : d;
                _quantosDigitando = true;
            }
            return true;
        }

        // ------------------------------------------------------------------ o desenho

        void EstilosDoQuantos()
        {
            if (_quantosGrande != null && Mathf.Approximately(_uDoQuantos, _u)) return;
            _uDoQuantos = _u;
            _quantosGrande = new GUIStyle(_nome) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, Mathf.RoundToInt(30f * _u)) };
            PorCor(_quantosGrande, Destaque);
            _quantosBotao = new GUIStyle(_titulo) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
            PorCor(_quantosBotao, CorDoTexto);
        }

        void DesenharQuantos(Vector2 mouse)
        {
            if (!QuantosAberto) return;
            int max = MaximoDoQuantos(out var it);
            if (max < 1) return;
            EstilosDoQuantos();
            float u = _u;
            bool municao = Itens.EhMunicao(it.item);
            Ret(Janela, new Color(0f, 0f, 0f, 0.35f));
            var p = PainelDoQuantos;
            Ret(new Rect(p.x + 5f * u, p.y + 6f * u, p.width, p.height), new Color(0f, 0f, 0f, 0.45f));
            Ret(p, new Color(0.07f, 0.07f, 0.065f, 0.97f));
            Moldura(p, Mathf.Max(1f, Mathf.Round(u)), Friso);
            GUI.Label(new Rect(p.x + 18f * u, p.y + 10f * u, p.width - 36f * u, 22f * u), municao ? "LARGAR QUANTAS BALAS?" : "LARGAR QUANTOS?", _titulo);
            GUI.Label(new Rect(p.x + 18f * u, p.y + 32f * u, p.width - 36f * u, 18f * u), Maiuscula(Itens.Nome(it.item)), _pequeno);
            // o número (digitando, pode ficar 0 até o próximo número)
            GUI.Label(new Rect(p.x, p.y + 50f * u, p.width, 40f * u), $"{_quantos} de {max}", _quantosGrande);
            // a barra
            var barra = BarraDoQuantos(p);
            Ret(barra, new Color(0f, 0f, 0f, 0.55f));
            float t = max > 1 ? (Mathf.Clamp(_quantos, 1, max) - 1f) / (max - 1f) : 1f;
            Ret(new Rect(barra.x, barra.y, barra.width * t, barra.height), new Color(Destaque.r, Destaque.g, Destaque.b, 0.5f));
            Ret(new Rect(Mathf.Round(barra.x + barra.width * t - 4f * u), barra.y - 4f * u, 8f * u, barra.height + 8f * u), Destaque);
            // os passos
            for (int i = 0; i < PassosDoQuantos.Length; i++) BotaoDoQuantos(BotaoDoPasso(p, i), PassosDoQuantos[i].texto, mouse, false);
            GUI.Label(new Rect(p.x, p.y + 156f * u, p.width, 16f * u), "Digite o número  ·  Enter: largar  ·  clique fora: cancela", _centro);
            // cancelar e largar
            int n = Mathf.Clamp(_quantos, 1, max);
            BotaoDoQuantos(BotaoCancelarDoQuantos(p), "Cancelar", mouse, false);
            BotaoDoQuantos(BotaoLargarDoQuantos(p), municao ? $"Largar {n} {(n == 1 ? "bala" : "balas")}" : $"Largar {n}", mouse, true);
        }

        void BotaoDoQuantos(Rect r, string texto, Vector2 mouse, bool principal)
        {
            bool sobre = r.Contains(mouse);
            Ret(r, principal ? new Color(0.36f, 0.29f, 0.1f, 0.96f) : new Color(0.19f, 0.19f, 0.18f, 0.96f));
            if (sobre) Ret(r, new Color(0.98f, 0.82f, 0.36f, 0.22f));
            Moldura(r, Mathf.Max(1f, Mathf.Round(_u)), sobre || principal ? Destaque : BordaClara);
            GUI.Label(r, texto, _quantosBotao);
        }
    }
}
