using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;
using static ZombieCityCleaner.Player.TelaDoInventario;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Olhar dentro do capô (só no dono; abre com E no capô de um carro, <see cref="CapoDoCarro"/> ou
    /// <see cref="CapoDaCidade"/>; fecha com E, ESC, abrindo o inventário, entrando num carro ou se o jogador se afasta).
    /// A câmera do jogador sai do olho e vai até em cima do cofre do motor (o <see cref="CofreDoMotor.olhar"/>), como se
    /// ele enfiasse a cabeça embaixo do capô, e o mouse fica solto (<see cref="MenuOnline.Tela"/>). A cabeça segue um
    /// pouco o mouse.
    /// - As peças estão no carro, em 3D (o <see cref="CofreDoMotor"/>): a vela no motor, a bateria e o radiador. O lugar
    ///   vazio mostra a peça apagada. Cada lugar tem uma etiqueta com o nome e o estado; passando o mouse, a peça acende
    ///   e a etiqueta mostra o que ela faz.
    /// - Clique numa peça: ela sai do carro e vai para a mochila. Clique num lugar vazio: a melhor peça daquele tipo que
    ///   o jogador leva vai para lá.
    /// - À direita, as peças que o jogador leva. Clique numa: ela vai para o lugar dela, e a que estava lá vai para a
    ///   mochila (ou para o chão, sem espaço).
    /// Com o motor ligado, nada sai nem entra (<see cref="ICapo.Impedimento"/>). Os pedidos vão para o anfitrião.
    /// </summary>
    [DefaultExecutionOrder(200)]   // a câmera é posta depois de tudo o que mexe nela (a arma muda o campo de visão)
    public class TelaDoCapo : MonoBehaviour
    {
        public JogadorRede rede;
        public InventarioJogador inventario;
        public TelaDoInventario telaDoInventario;
        [Tooltip("O campo de visão olhando o motor (graus).")]
        public float campoDeVisao = 56f;
        [Tooltip("Quanto tempo (s) a cabeça leva para entrar no capô e para sair.")]
        public float tempoEntrar = 0.65f, tempoSair = 0.45f;
        [Tooltip("Quanto a cabeça segue o mouse (graus para o lado e para cima, com o mouse na borda da tela).")]
        public Vector2 seguirMouse = new Vector2(7f, 4.5f);

        /// <summary>Olhando um motor (ou a cabeça ainda saindo do capô).</summary>
        public bool Aberta => _capo != null;

        // a coluna das peças da mochila (a 720p, vezes u), à direita
        const float LarguraDaColuna = 300f, AlturaDaLinha = 62f;

        static readonly Color CorDoTexto = new Color(0.94f, 0.92f, 0.86f);
        static readonly Color TextoApagado = new Color(0.8f, 0.78f, 0.72f, 0.9f);
        static readonly Color Destaque = new Color(0.98f, 0.82f, 0.36f);
        static readonly Color FundoEscuro = new Color(0.07f, 0.07f, 0.065f, 0.86f);
        static readonly Color Borda = new Color32(0x4A, 0x4A, 0x45, 0xFF);

        ICapo _capo;
        CofreDoMotor _cofre;
        ArmaJogador _arma;
        bool _saindo;
        float _k, _fovAntes;
        int _abriuNoQuadro, _fechouNoQuadro = -1;
        int _sobre = -1;                 // o lugar do cofre embaixo do mouse
        int _linhaSobre = -1;            // a peça da mochila embaixo do mouse
        bool _mouseNaColuna;
        float _u = 1f, _uDosEstilos;
        string _aviso;
        float _avisoAte;
        GUIStyle _titulo, _texto, _pequeno, _nome, _centro;
        readonly RaycastHit[] _acertos = new RaycastHit[32];
        readonly List<ItemNaMochila> _naMochila = new List<ItemNaMochila>();

        void Awake() => _arma = GetComponent<ArmaJogador>();

        void OnEnable() => MenuOnline.FecharTelas += Fechar;

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            Terminar();
        }

        /// <summary>Leva a cabeça para dentro deste capô.</summary>
        public void Abrir(ICapo capo)
        {
            // o E que fechou não abre de novo no mesmo quadro; saindo de um capô, espera a cabeça voltar
            if (capo == null || capo.Cofre == null || rede == null || !rede.IsOwner || rede.cameraMundo == null) return;
            if (_capo != null || Time.frameCount == _fechouNoQuadro) return;
            if (telaDoInventario != null && telaDoInventario.Aberta) return;
            _capo = capo;
            _cofre = capo.Cofre;
            _saindo = false;
            _k = 0f;
            _fovAntes = rede.cameraMundo.fieldOfView;
            _abriuNoQuadro = Time.frameCount;
            _aviso = null;
            _sobre = _linhaSobre = -1;
            _cofre.Inspecionando = true;
            MenuOnline.Tela(this, true);
            capo.Olhando(true);
        }

        /// <summary>A cabeça sai do capô (a tela só fecha quando ela volta para o olho).</summary>
        public void Fechar()
        {
            if (_capo == null || _saindo) return;
            _saindo = true;
            _fechouNoQuadro = Time.frameCount;
            _sobre = _linhaSobre = -1;
            if (_cofre != null) _cofre.Destaque = -1;
        }

        // a cabeça voltou: tudo como antes
        void Terminar()
        {
            if (_capo == null) return;
            var capo = _capo;
            var cofre = _cofre;
            _capo = null;
            _cofre = null;
            _saindo = false;
            _fechouNoQuadro = Time.frameCount;
            if (cofre != null)
            {
                cofre.Inspecionando = false;
                cofre.Destaque = -1;
            }
            if (rede != null && rede.cameraMundo != null)
            {
                var cam = rede.cameraMundo;
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.fieldOfView = _fovAntes;
                if (rede.cameraBracos != null) rede.cameraBracos.enabled = _arma == null || !_arma.NaLuneta;
            }
            MenuOnline.Tela(this, false);
            if (!(capo is Object o) || o != null) capo.Olhando(false);
        }

        void Update()
        {
            if (_capo == null || _saindo) return;
            // o carro sumiu, andou ou ficou longe; o jogador entrou num carro ou abriu o inventário
            bool sumiu = (_capo is Object o && o == null) || _cofre == null;
            var passageiro = rede != null ? rede.Passageiro : null;
            if (sumiu || rede == null || !rede.IsSpawned || !_capo.Aberto || (rede.transform.position - _capo.Ponto).sqrMagnitude > 3.6f * 3.6f
                || (passageiro != null && passageiro.NoCarro) || (telaDoInventario != null && telaDoInventario.Aberta))
            {
                if (sumiu) Terminar();
                else Fechar();
                return;
            }
            var teclado = Keyboard.current;
            if (teclado != null && Time.frameCount > _abriuNoQuadro && !MenuOnline.Aberto && teclado.eKey.wasPressedThisFrame) Fechar();
        }

        // ------------------------------------------------------------------ a câmera e o mouse

        void LateUpdate()
        {
            if (_capo == null) return;
            if (_cofre == null || _cofre.olhar == null || rede == null || rede.cameraMundo == null)
            {
                Terminar();
                return;
            }
            float dt = Time.deltaTime;
            _k = Mathf.MoveTowards(_k, _saindo ? 0f : 1f, dt / (_saindo ? tempoSair : tempoEntrar));
            if (_saindo && _k <= 0f)
            {
                Terminar();
                return;
            }
            var cam = rede.cameraMundo;
            var olho = cam.transform.parent;
            float s = Mathf.SmoothStep(0f, 1f, _k);

            // a cabeça segue um pouco o mouse (de -1 a 1, da borda à borda da tela)
            Vector2 m = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                var p = mouse.position.ReadValue();
                m = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            var olhar = _cofre.olhar;
            var alvo = olhar.rotation * Quaternion.Euler(-m.y * seguirMouse.y, m.x * seguirMouse.x, 0f);
            // do olho até em cima do motor, num arco (a cabeça sobe um pouco no meio do caminho, por cima da lataria)
            var de = olho != null ? olho.position : cam.transform.position;
            var deGiro = olho != null ? olho.rotation : cam.transform.rotation;
            var pos = Vector3.Lerp(de, olhar.position, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.12f);
            cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(deGiro, alvo, s));
            cam.fieldOfView = Mathf.Lerp(_fovAntes, campoDeVisao, s);
            if (rede.cameraBracos != null && rede.cameraBracos.enabled) rede.cameraBracos.enabled = false;

            // o lugar embaixo do mouse (os gatilhos do cofre; o resto não tapa)
            _sobre = -1;
            if (!_saindo && _k > 0.9f && mouse != null && !_mouseNaColuna && !MenuOnline.Aberto)
            {
                var raio = cam.ScreenPointToRay(mouse.position.ReadValue());
                int n = Physics.RaycastNonAlloc(raio, _acertos, 4f, ~0, QueryTriggerInteraction.Collide);
                float perto = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    int k = _cofre.Lugar(_acertos[i].collider);
                    if (k >= 0 && _acertos[i].distance < perto)
                    {
                        perto = _acertos[i].distance;
                        _sobre = k;
                    }
                }
            }
            // a peça da mochila embaixo do mouse acende o lugar dela
            int destaque = _sobre;
            if (_linhaSobre >= 0 && _linhaSobre < _naMochila.Count) destaque = Itens.IndiceDaPeca(_naMochila[_linhaSobre].item);
            _cofre.Destaque = _saindo ? -1 : destaque;
        }

        // ------------------------------------------------------------------ o clique

        void PecasNaMochila()
        {
            _naMochila.Clear();
            if (inventario == null) return;
            foreach (var it in inventario.Mochila)
                if (Itens.EhPecaDoMotor(it.item)) _naMochila.Add(it);
            // por tipo (a vela, a bateria e o radiador), as melhores primeiro
            _naMochila.Sort((a, b) => a.item != b.item ? a.item.CompareTo(b.item) : b.qtd.CompareTo(a.qtd));
        }

        // a melhor peça deste lugar que o jogador leva
        bool Melhor(int k, out ItemNaMochila melhor)
        {
            melhor = default;
            bool achou = false;
            foreach (var it in _naMochila)
                if (Itens.IndiceDaPeca(it.item) == k && (!achou || it.qtd > melhor.qtd))
                {
                    melhor = it;
                    achou = true;
                }
            return achou;
        }

        void Clicar()
        {
            if (_capo == null || _saindo || _k < 0.9f) return;
            var pecas = _capo.Pecas;
            // uma peça da mochila: vai para o lugar dela
            if (_linhaSobre >= 0 && _linhaSobre < _naMochila.Count)
            {
                var it = _naMochila[_linhaSobre];
                Por(Itens.IndiceDaPeca(it.item), it);
                return;
            }
            if (_sobre < 0) return;
            int k = _sobre;
            if (pecas.Tem(k))
            {
                // a peça do carro vai para a mochila
                var item = Itens.DaPeca(k);
                if (_capo.Impedimento != null) Avisar(_capo.Impedimento);
                else if (!inventario.PodeReceber(item, out var motivo)) Avisar(motivo);
                else
                {
                    _capo.PedirTirar(k);
                    Tinido();
                }
            }
            else if (Melhor(k, out var melhor)) Por(k, melhor);
            else Avisar($"Você não tem {Itens.NomeComArtigo(Itens.DaPeca(k))}: procure no capô dos carros abandonados");
        }

        void Por(int k, ItemNaMochila it)
        {
            if (_capo.Impedimento != null)
            {
                Avisar(_capo.Impedimento);
                return;
            }
            _capo.PedirPor(k, it.id);
            Tinido();
        }

        // o tinido da peça saindo ou entrando (o estalo da chave serve)
        void Tinido()
        {
            var cam = rede.cameraMundo;
            Combat.Efeitos.Som(Combat.Efeitos.Chave, cam != null ? cam.transform.position : rede.transform.position + Vector3.up, 0.5f, false);
        }

        void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.time + 2.4f;
        }

        // ------------------------------------------------------------------ o desenho

        static float Escala() => Mathf.Min(Screen.height / 720f, Screen.width / 1280f);

        Rect Coluna => new Rect(Screen.width - (LarguraDaColuna + 24f) * _u, 96f * _u, LarguraDaColuna * _u,
            (44f + Mathf.Max(1, _naMochila.Count) * AlturaDaLinha + 10f) * _u);

        Rect Linha(int i)
        {
            var c = Coluna;
            return new Rect(c.x + 8 * _u, c.y + 38 * _u + i * AlturaDaLinha * _u, c.width - 16 * _u, (AlturaDaLinha - 6) * _u);
        }

        void OnGUI()
        {
            if (_capo == null || MenuOnline.Aberto) return;
            GUI.depth = 0;
            _u = Escala();
            Estilos();
            PecasNaMochila();
            var e = Event.current;
            var mouse = e.mousePosition;
            if (e.type == EventType.Repaint || e.type == EventType.MouseMove || e.type == EventType.MouseDown)
            {
                _mouseNaColuna = _naMochila.Count > 0 && Coluna.Contains(mouse);
                _linhaSobre = -1;
                if (!_saindo)
                    for (int i = 0; i < _naMochila.Count; i++)
                        if (Linha(i).Contains(mouse)) _linhaSobre = i;
            }
            if (e.type == EventType.MouseDown && e.button == 0 && !MenuDoDesenvolvedor.Area.Contains(mouse))
            {
                Clicar();
                e.Use();
            }
            if (e.type != EventType.Repaint) return;

            // tudo aparece quando a cabeça chega, e some quando ela sai
            float a = Mathf.Clamp01((_k - 0.6f) / 0.4f);
            if (a <= 0f) return;
            var antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            Titulo();
            Etiquetas();
            ColunaDaMochila();
            Rodape();
            GUI.color = antes;
        }

        // em cima, à esquerda: o carro e a situação (a carga e a temperatura)
        void Titulo()
        {
            float u = _u;
            var r = new Rect(24 * u, 24 * u, 470 * u, 66 * u);
            Ret(r, FundoEscuro);
            Moldura(r, Mathf.Max(1f, Mathf.Round(u)), Borda);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 8 * u, r.width - 28 * u, 26 * u), "CAPÔ   ·   " + _capo.NomeDoCarro.ToUpperInvariant(), _titulo);
            GUI.Label(new Rect(r.x + 14 * u, r.y + 36 * u, r.width - 28 * u, 22 * u), _capo.Situacao ?? "", _texto);
        }

        // a etiqueta de cada lugar, presa em cima da peça: o nome e o estado; a do mouse, maior, com o que faz e o clique
        void Etiquetas()
        {
            var cam = rede.cameraMundo;
            if (cam == null || _cofre == null) return;
            var pecas = _capo.Pecas;
            float u = _u;
            int destaque = _cofre.Destaque;
            for (int k = 0; k < PecasDoMotor.Quantas; k++)
            {
                var tela = cam.WorldToScreenPoint(_cofre.Topo(k) + Vector3.up * 0.03f);
                if (tela.z <= 0f) continue;
                var ponto = new Vector2(tela.x, Screen.height - tela.y);
                bool tem = pecas.Tem(k);
                int estado = pecas.Estado(k);
                string nome = Itens.Nome(Itens.DaPeca(k)).ToUpperInvariant();
                bool grande = k == destaque;
                float w = (grande ? 300f : 150f) * u, h = (grande ? 104f : 46f) * u;
                var r = new Rect(ponto.x - w / 2f, ponto.y - h - 14 * u, w, h);
                r.x = Mathf.Clamp(r.x, 8 * u, Screen.width - w - 8 * u);
                r.y = Mathf.Clamp(r.y, 8 * u, Screen.height - h - 8 * u);
                // o fio da etiqueta até a peça
                Ret(new Rect(ponto.x - u, r.yMax, 2 * u, Mathf.Max(0f, ponto.y - r.yMax)), new Color(0f, 0f, 0f, 0.55f));
                Ret(r, FundoEscuro);
                Moldura(r, Mathf.Max(1f, Mathf.Round((grande ? 2 : 1) * u)), grande ? Destaque : Borda);
                var cor = tem ? PecasDoMotor.CorDoEstado(estado) : TextoApagado;
                GUI.Label(new Rect(r.x + 10 * u, r.y + 5 * u, r.width - 20 * u, 20 * u), nome, _nome);
                var num = new GUIStyle(_nome) { alignment = TextAnchor.UpperRight };
                num.normal.textColor = cor;
                GUI.Label(new Rect(r.x + 10 * u, r.y + 5 * u, r.width - 20 * u, 20 * u), tem ? $"{estado}%" : "falta", num);
                var barra = new Rect(r.x + 10 * u, r.y + 28 * u, r.width - 20 * u, 8 * u);
                Ret(barra, new Color(0f, 0f, 0f, 0.6f));
                if (tem) Ret(new Rect(barra.x, barra.y, barra.width * estado / 100f, barra.height), cor);
                if (!grande) continue;
                GUI.Label(new Rect(r.x + 10 * u, r.y + 42 * u, r.width - 20 * u, 36 * u), tem ? PecasDoMotor.Efeito(k, estado) : PecasDoMotor.Faltando(k), _pequeno);
                string clique;
                if (tem) clique = "Clique: tirar e levar";
                else if (Melhor(k, out var melhor)) clique = $"Clique: pôr a sua ({melhor.qtd}%)";
                else clique = "Ache uma no capô de outro carro";
                var c = new GUIStyle(_pequeno);
                c.normal.textColor = Destaque;
                GUI.Label(new Rect(r.x + 10 * u, r.yMax - 22 * u, r.width - 20 * u, 18 * u), clique, c);
            }
        }

        // à direita: as peças que o jogador leva (o ícone, o nome e o estado)
        void ColunaDaMochila()
        {
            float u = _u;
            var area = Coluna;
            Ret(area, FundoEscuro);
            Moldura(area, Mathf.Max(1f, Mathf.Round(u)), Borda);
            GUI.Label(new Rect(area.x + 12 * u, area.y + 8 * u, area.width - 24 * u, 24 * u), "PEÇAS NA MOCHILA", _titulo);
            if (_naMochila.Count == 0)
            {
                GUI.Label(new Rect(area.x + 12 * u, area.y + 40 * u, area.width - 24 * u, 60 * u),
                    "Nenhuma. Vasculhe o capô dos carros abandonados.", _pequeno);
                return;
            }
            var pecas = _capo.Pecas;
            for (int i = 0; i < _naMochila.Count; i++)
            {
                var it = _naMochila[i];
                int k = Itens.IndiceDaPeca(it.item);
                var r = Linha(i);
                if (r.yMax > Screen.height - 60 * u) break;
                bool sobre = i == _linhaSobre;
                Ret(r, sobre ? new Color(0.62f, 0.6f, 0.56f, 0.95f) : new Color(0.5f, 0.48f, 0.45f, 0.9f));
                Moldura(r, Mathf.Max(1f, Mathf.Round((sobre ? 2 : 1) * u)), sobre ? Destaque : new Color(1f, 1f, 1f, 0.12f));
                var icone = inventario.IconeDe(it.item);
                if (icone != null) GUI.DrawTexture(new Rect(r.x + 4 * u, r.y + 4 * u, 72 * u, r.height - 8 * u), icone, ScaleMode.ScaleToFit);
                float tx = r.x + 84 * u, tw = r.width - 92 * u;
                GUI.Label(new Rect(tx, r.y + 6 * u, tw, 22 * u), Maiuscula(Itens.Nome(it.item)), _nome);
                var est = new GUIStyle(_nome) { alignment = TextAnchor.UpperRight };
                est.normal.textColor = PecasDoMotor.CorDoEstado(it.qtd);
                GUI.Label(new Rect(tx, r.y + 6 * u, tw, 22 * u), $"{it.qtd}%", est);
                GUI.Label(new Rect(tx, r.y + 30 * u, tw, 20 * u),
                    sobre ? (pecas.Tem(k) ? $"Clique: trocar pela de {pecas.Estado(k)}%" : "Clique: pôr no carro") : PecasDoMotor.Efeito(k, it.qtd), _pequeno);
            }
        }

        // embaixo, no meio: o que fazer, ou o aviso
        void Rodape()
        {
            float u = _u;
            string texto = _capo.Impedimento;
            Color cor = new Color(1f, 0.55f, 0.4f);
            if (Time.time < _avisoAte && _aviso != null)
            {
                texto = _aviso;
                cor = Destaque;
            }
            if (texto == null)
            {
                texto = "Clique numa peça para tirar   ·   num lugar vazio ou numa peça da mochila para pôr   ·   E ou ESC: sair";
                cor = TextoApagado;
            }
            var estilo = new GUIStyle(_centro);
            estilo.normal.textColor = cor;
            var tamanho = estilo.CalcSize(new GUIContent(texto));
            var r = new Rect((Screen.width - tamanho.x) / 2f - 16 * u, Screen.height - 64 * u, tamanho.x + 32 * u, 34 * u);
            Ret(r, FundoEscuro);
            GUI.Label(r, texto, estilo);
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
            _titulo = Estilo(16, FontStyle.Bold, TextAnchor.MiddleLeft, CorDoTexto);
            _texto = Estilo(14, FontStyle.Normal, TextAnchor.UpperLeft, CorDoTexto);
            _pequeno = Estilo(12, FontStyle.Normal, TextAnchor.UpperLeft, TextoApagado);
            _nome = Estilo(14, FontStyle.Bold, TextAnchor.UpperLeft, CorDoTexto);
            _centro = Estilo(14, FontStyle.Bold, TextAnchor.MiddleCenter, CorDoTexto);
            _centro.wordWrap = false;
        }
    }
}
