using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Chefes
{
    /// <summary>
    /// O painel da vitrine dos chefes (Scenes/Exemplos/Chefes.unity, montada por Chefes > Montar os chefes (vitrine)): a
    /// câmera vai de um chefe ao outro e gira em volta dele, e os botões tocam as animações dele (as do controlador de
    /// cada um: Velocidade = Parado/Andar, um trigger por habilidade, Investida e Frenesi em bool, Reiniciar depois da
    /// morte). Só para olhar os modelos: nada de jogo.
    /// <para>
    /// ← / → (ou A / D): o chefe anterior / o próximo. 1–9: direto. 0: a fila inteira. Arrastar com o mouse (fora do
    /// painel): girar a câmera. Rodinha: aproximar. G: girar sozinho. H: esconder o painel.
    /// </para>
    /// </summary>
    public class VitrineDosChefes : MonoBehaviour
    {
        [System.Serializable]
        public class Item
        {
            public string titulo;
            [TextArea] public string detalhes;
            public Transform raiz;
        }

        public Item[] itens = new Item[0];
        [Tooltip("Vazia: a câmera principal")]
        public Camera cameraDaCena;

        int _foco = -1;   // -1: a fila inteira
        Vector3 _pivo, _pivoAlvo;
        float _yaw = 180f, _yawAlvo = 180f, _pitch = 8f, _pitchAlvo = 8f, _dist = 20f, _distAlvo = 20f;
        bool _girar, _painel = true, _arrastando, _todosAndando;
        Bounds[] _caixas = new Bounds[0];
        Bounds _tudo;
        Animator[] _animadores = new Animator[0];
        GUIStyle _estilo, _titulo, _botao;
        Rect _painelNaTela;
        string _ultima = "";

        static readonly int VelocidadeId = Animator.StringToHash("Velocidade");

        void Start()
        {
            if (cameraDaCena == null) cameraDaCena = Camera.main;
            if (cameraDaCena == null)
            {
                Debug.LogError("Vitrine dos chefes: a cena não tem câmera (monte a vitrine de novo).", this);
                enabled = false;
                return;
            }
            // a neblina de altura do renderizador lê valores globais que o AmbienteDoDia escreve: aqui, sem neblina
            Shader.SetGlobalVector("_NeblinaParams", Vector4.zero);
            _caixas = new Bounds[itens.Length];
            _animadores = new Animator[itens.Length];
            bool primeiro = true;
            for (int i = 0; i < itens.Length; i++)
            {
                var it = itens[i];
                if (it == null || it.raiz == null) continue;
                var a = it.raiz.GetComponentInChildren<Animator>();
                if (a != null && a.runtimeAnimatorController != null)
                {
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    _animadores[i] = a;
                }
                var rs = it.raiz.GetComponentsInChildren<Renderer>();
                var b = new Bounds(it.raiz.position + Vector3.up, Vector3.one);
                bool tem = false;
                foreach (var r in rs)
                {
                    if (r.GetComponent<TextMesh>() != null) continue;   // (os rótulos)
                    if (!tem) b = r.bounds;
                    else b.Encapsulate(r.bounds);
                    tem = true;
                }
                _caixas[i] = b;
                if (primeiro) _tudo = b;
                else _tudo.Encapsulate(b);
                primeiro = false;
            }
            Focar(-1, true);
        }

        void Focar(int i, bool jaLa = false)
        {
            _foco = Mathf.Clamp(i, -1, itens.Length - 1);
            var b = _foco < 0 ? _tudo : _caixas[_foco];
            _pivoAlvo = new Vector3(b.center.x, b.size.y * 0.45f + b.min.y, b.center.z);
            float tamanho = _foco < 0 ? Mathf.Max(b.size.x * 0.55f, b.size.y) : Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z) * 0.75f);
            _distAlvo = tamanho / Mathf.Tan(Mathf.Deg2Rad * cameraDaCena.fieldOfView * 0.5f) * (_foco < 0 ? 0.62f : 0.68f);
            _yawAlvo = _foco < 0 ? 180f : 205f;
            _pitchAlvo = _foco < 0 ? 6f : 10f;
            if (jaLa)
            {
                _pivo = _pivoAlvo;
                _dist = _distAlvo;
                _yaw = _yawAlvo;
                _pitch = _pitchAlvo;
            }
        }

        void Update()
        {
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            if (teclado != null)
            {
                if (teclado.rightArrowKey.wasPressedThisFrame || teclado.dKey.wasPressedThisFrame) Focar(_foco + 1 >= itens.Length ? -1 : _foco + 1);
                if (teclado.leftArrowKey.wasPressedThisFrame || teclado.aKey.wasPressedThisFrame) Focar(_foco - 1 < -1 ? itens.Length - 1 : _foco - 1);
                if (teclado.digit0Key.wasPressedThisFrame || teclado.homeKey.wasPressedThisFrame) Focar(-1);
                for (int k = 1; k <= 9 && k <= itens.Length; k++)
                    if (teclado[(Key)((int)Key.Digit1 + k - 1)].wasPressedThisFrame) Focar(k - 1);
                if (teclado.gKey.wasPressedThisFrame) _girar = !_girar;
                if (teclado.hKey.wasPressedThisFrame) _painel = !_painel;
            }
            if (mouse != null)
            {
                var p = mouse.position.ReadValue();
                bool noPainel = _painel && _painelNaTela.Contains(new Vector2(p.x, Screen.height - p.y));
                if ((mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && !noPainel) _arrastando = true;
                if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed) _arrastando = false;
                if (_arrastando)
                {
                    var d = mouse.delta.ReadValue();
                    _yawAlvo += d.x * 0.25f;
                    _pitchAlvo = Mathf.Clamp(_pitchAlvo - d.y * 0.2f, -10f, 75f);
                }
                float roda = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(roda) > 0.01f && !noPainel) _distAlvo = Mathf.Max(1.5f, _distAlvo * (roda > 0 ? 0.88f : 1.13f));
            }
            if (_girar) _yawAlvo += 20f * Time.deltaTime;
            float s = 1f - Mathf.Exp(-6f * Time.deltaTime);
            _pivo = Vector3.Lerp(_pivo, _pivoAlvo, s);
            _dist = Mathf.Lerp(_dist, _distAlvo, s);
            _yaw = Mathf.LerpAngle(_yaw, _yawAlvo, s);
            _pitch = Mathf.Lerp(_pitch, _pitchAlvo, s);
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            cameraDaCena.transform.SetPositionAndRotation(_pivo - rot * Vector3.forward * _dist, rot);
        }

        void OnGUI()
        {
            if (!_painel) return;
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
                _estilo.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                _titulo = new GUIStyle(_estilo) { fontSize = 26, fontStyle = FontStyle.Bold };
                _titulo.normal.textColor = new Color(1f, 0.85f, 0.4f);
                _botao = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            }
            string nome = _foco < 0 ? "Os chefes" : itens[_foco].titulo;
            string det = _foco < 0 ? $"{itens.Length} modelos lado a lado (o Oco de 1,8 m para a escala)" : itens[_foco].detalhes;
            GUI.Label(new Rect(20, 16, 1000, 36), nome, _titulo);
            GUI.Label(new Rect(20, 52, 1000, 60), det, _estilo);
            GUI.Label(new Rect(20, Screen.height - 34, 1300, 26),
                      "← / → ou A / D: anterior / próximo   ·   1–9: direto   ·   0: a fila   ·   arrastar: girar   ·   rodinha: aproximar   ·   G: girar sozinho   ·   H: esconder",
                      _estilo);
            PainelDeAnimacoes();
        }

        /// <summary>Os botões das animações: do chefe em foco, ou (na fila inteira) andar/parar todos juntos.</summary>
        void PainelDeAnimacoes()
        {
            var botoes = new List<(string rotulo, System.Action acao)>();
            if (_foco < 0)
            {
                botoes.Add((_todosAndando ? "■  Todos parados" : "▶  Todos andando", () =>
                {
                    _todosAndando = !_todosAndando;
                    foreach (var a in _animadores)
                        if (a != null) a.SetFloat(VelocidadeId, _todosAndando ? 1f : 0f);
                }));
                botoes.Add(("↺  Reiniciar todos", () =>
                {
                    foreach (var a in _animadores) Reiniciar(a);
                }));
            }
            else
            {
                var a = _animadores[_foco];
                if (a == null)
                {
                    GUI.Label(new Rect(20, 112, 400, 26), "(sem animações)", _estilo);
                    return;
                }
                bool andando = a.GetFloat(VelocidadeId) > 0.1f;
                botoes.Add((andando ? "■  Parado" : "▶  Andar", () => a.SetFloat(VelocidadeId, andando ? 0f : 1f)));
                foreach (var p in a.parameters)
                {
                    if (p.name == "Velocidade" || p.name == "Reiniciar") continue;
                    if (p.type == AnimatorControllerParameterType.Trigger)
                    {
                        var n = p.name;
                        botoes.Add(("●  " + n, () =>
                        {
                            a.SetFloat(VelocidadeId, 0f);
                            a.SetTrigger(n);
                            _ultima = n;
                        }));
                    }
                    else if (p.type == AnimatorControllerParameterType.Bool)
                    {
                        var n = p.name;
                        bool ligado = a.GetBool(n);
                        botoes.Add(((ligado ? "■  " : "⟳  ") + n + (ligado ? " (parar)" : " (loop)"), () =>
                        {
                            a.SetBool(n, !a.GetBool(n));
                            _ultima = n;
                        }));
                    }
                }
                botoes.Add(("↺  Reiniciar", () => Reiniciar(a)));
            }
            const float x = 20f, y0 = 112f, w = 230f, h = 30f, gap = 4f;
            _painelNaTela = new Rect(x, y0, w, botoes.Count * (h + gap));
            for (int i = 0; i < botoes.Count; i++)
                if (GUI.Button(new Rect(x, y0 + i * (h + gap), w, h), botoes[i].rotulo, _botao)) botoes[i].acao();
        }

        static void Reiniciar(Animator a)
        {
            if (a == null) return;
            a.SetFloat(VelocidadeId, 0f);
            foreach (var p in a.parameters)
                if (p.type == AnimatorControllerParameterType.Bool) a.SetBool(p.name, false);
            a.SetTrigger("Reiniciar");
        }
    }
}
