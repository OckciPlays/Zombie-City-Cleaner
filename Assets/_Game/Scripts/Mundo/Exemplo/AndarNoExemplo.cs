using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Para andar pelas cenas de exemplo das casas (a rua, a sala de medidas e as variações) em primeira pessoa, com as
    /// medidas do jogador (CharacterController de 1,80 m e raio 0,3; o olho a 1,62 m; andar 2 m/s, correr 4,2 m/s; a
    /// câmera com 70° de campo). Sem rede. WASD anda, Shift corre, Espaço pula, V voa (Espaço e Ctrl sobem e descem), R
    /// gera de novo com uma semente nova. O clique prende o mouse, o Esc solta e o H esconde o texto. A tela mostra o
    /// texto da cena (na sala de medidas, as medidas da casa em que o jogador está; nas variações, a ficha da construção
    /// mais perto).
    /// Nas cenas com as paradas (<see cref="CenaDeExemplo.Paradas"/>): as setas ← e → levam à construção anterior e à
    /// próxima (na calçada, de frente para ela), e o P mostra a planta dela: a câmera de cima, ortográfica, logo abaixo do
    /// teto de um piso (o que fica acima dela não aparece: as paredes saem cortadas e os móveis inteiros), com o nome e o
    /// tamanho de cada cômodo; ↑ e ↓ trocam o piso e a roda aproxima.
    /// O T troca para a terceira pessoa (29/10/2026, a pedido: para ter noção do tamanho das coisas com o personagem): o
    /// corpo do jogo (o <see cref="corpo"/>, com a locomoção dele: parado, andando e correndo pela velocidade) e a câmera
    /// atrás do ombro direito, como a CameraTerceiraPessoa do jogo (0,5 m para o lado, 0,15 para cima e 2,3 para trás da
    /// cabeça, parando nas paredes; colada numa, o corpo some). Na primeira pessoa, o corpo fica só na sombra.
    /// O E abre e fecha a porta ou a gaveta de um móvel (o <see cref="ParteQueAbre"/>, a mobília nova): mirando nela, com
    /// o meio da tela, até <see cref="Alcance"/> da cabeça, a tela mostra "[E] Abrir" ou "[E] Fechar". As teclas da cena
    /// (<see cref="CenaDeExemplo.Teclas"/>) vêm junto.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AndarNoExemplo : MonoBehaviour
    {
        public Transform cabeca;
        public float andar = 2f, correr = 4.2f, voar = 10f, sensibilidade = 0.08f;
        [Tooltip("O corpo do personagem do jogo (o Personagem_PrincipalBloco, com o controlador dele), filho do jogador, nos pés.")]
        public Animator corpo;
        [Tooltip("A câmera da terceira pessoa em relação à cabeça (x para a direita, y para cima, z para a frente), a do jogo.")]
        public Vector3 ombro = new Vector3(0.5f, 0.15f, -2.3f);

        /// <summary>Na planta, a câmera fica tanto acima do piso: acima dos móveis mais altos (2,2 m) e abaixo do teto.</summary>
        const float AlturaDaPlanta = 2.25f;

        CharacterController _cc;
        float _giro, _inclinacao, _vy;
        bool _voando;
        CenaDeExemplo _rua;
        Camera _cam;
        Vector3 _cabecaNoCorpo;
        // a planta: a parada, o piso e o tamanho da vista (o sol sem sombra enquanto ela está aberta: o telhado e as lajes
        // deixariam os cômodos no escuro)
        int _parada = -1, _piso;
        bool _planta, _semTexto;
        float _tamanho = 10f;
        Light _sol;
        LightShadows _sombras;
        // o pós-processamento do dia, desligado na planta (a vinheta escurecia os cantos)
        GameObject _posProcessamento;
        // fora da serialização: a Unity criava os dois vazios (a letra preta e pequena), e eles nunca eram feitos aqui
        [System.NonSerialized] GUIStyle _texto, _rotulo;
        // a terceira pessoa: a distância da câmera atrás do ombro (aproxima na hora, afasta devagar) e o corpo
        float _distancia, _velocidade;
        SkinnedMeshRenderer[] _pele;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        const float RaioDaCamera = 0.2f, PertoDemais = 0.55f;
        /// <summary>Até onde o E alcança, da cabeça (na terceira pessoa também: o raio sai da câmera, mas conta da cabeça).</summary>
        public const float Alcance = 2.5f;
        // a parte de móvel na mira (null: nenhuma) e os acertos do raio
        ParteQueAbre _mirada;
        // o móvel do mostruário na mira (o C troca a cor dele)
        MovelDoMostruario _movel;
        readonly RaycastHit[] _acertos = new RaycastHit[16];
        [System.NonSerialized] GUIStyle _aviso;

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            _giro = transform.eulerAngles.y;
            _rua = FindAnyObjectByType<CenaDeExemplo>();
            _cam = cabeca.GetComponent<Camera>();
            _cabecaNoCorpo = cabeca.localPosition;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { _sol = l; break; }
            if (corpo != null)
            {
                // só o corpo: sem as armas e os itens que vêm presos na mão, e sem a camada dos braços (a das armas): os
                // braços balançam com a locomoção
                foreach (var r in corpo.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                if (corpo.layerCount > 1) corpo.SetLayerWeight(1, 0f);
                corpo.applyRootMotion = false;
                _pele = corpo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                MostrarCorpo(false);
            }
        }

        // o corpo inteiro (na terceira pessoa) ou só a sombra dele
        void MostrarCorpo(bool inteiro)
        {
            if (_pele == null) return;
            foreach (var r in _pele) r.shadowCastingMode = inteiro ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        bool TemParadas => _rua != null && _rua.Paradas.Count > 0;

        /// <summary>O texto da tela escondido (o H; para as fotos).</summary>
        public bool SemTexto { get => _semTexto; set => _semTexto = value; }

        /// <summary>A câmera atrás do ombro, com o corpo à mostra (o T).</summary>
        public bool TerceiraPessoa
        {
            get => _terceira;
            set { _terceira = value; _distancia = 0f; }
        }
        bool _terceira;

        void Update()
        {
            var k = Keyboard.current;
            var mouse = Mouse.current;
            if (k == null || mouse == null) return;
            float dt = Time.deltaTime;
            if (mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (k.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (k.hKey.wasPressedThisFrame) _semTexto = !_semTexto;
            if (k.tKey.wasPressedThisFrame) TerceiraPessoa = !TerceiraPessoa;
            if (_rua != null) _rua.Teclas(k);
            if (k.eKey.wasPressedThisFrame && _mirada != null && !_planta) _mirada.Alternar();
            if (k.cKey.wasPressedThisFrame && _movel != null && !_planta) _movel.Proxima();

            if (k.rKey.wasPressedThisFrame && _rua != null)
            {
                FecharPlanta();
                _rua.semente = Random.Range(1, 100000);
                _rua.Gerar();
                _parada = -1;
            }
            if (TemParadas)
            {
                int n = _rua.Paradas.Count;
                int atual = _parada >= 0 && _parada < n ? _parada : _rua.MaisPerto(transform.position);
                if (k.rightArrowKey.wasPressedThisFrame) IrPara(atual < 0 ? 0 : (atual + 1) % n);
                if (k.leftArrowKey.wasPressedThisFrame) IrPara(atual < 0 ? n - 1 : (atual - 1 + n) % n);
                if (k.pKey.wasPressedThisFrame)
                {
                    if (_planta) FecharPlanta();
                    else AbrirPlanta(atual);
                }
            }
            if (_planta)
            {
                var p = _rua.Paradas[_parada];
                if (k.upArrowKey.wasPressedThisFrame) _piso = Mathf.Min(_piso + 1, p.Pisos.Count - 1);
                if (k.downArrowKey.wasPressedThisFrame) _piso = Mathf.Max(_piso - 1, 0);
                float roda = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(roda) > 0.01f) _tamanho = Mathf.Clamp(_tamanho * (roda > 0 ? 0.88f : 1.14f), 2f, 80f);
                cabeca.SetPositionAndRotation(new Vector3(p.Limites.center.x, p.Pisos[_piso] + AlturaDaPlanta, p.Limites.center.z),
                                              Quaternion.Euler(90f, p.Giro, 0f));
                _cam.orthographicSize = _tamanho;
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * sensibilidade;
                _giro += d.x;
                _inclinacao = Mathf.Clamp(_inclinacao - d.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _giro, 0f);
            cabeca.localRotation = Quaternion.Euler(_inclinacao, 0f, 0f);

            if (k.vKey.wasPressedThisFrame)
            {
                _voando = !_voando;
                _cc.enabled = !_voando;
                _vy = 0f;
            }

            var mv = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            if (_voando)
            {
                Vector3 dirV = cabeca.forward * mv.y + cabeca.right * mv.x;
                if (k.spaceKey.isPressed) dirV += Vector3.up;
                if (k.leftCtrlKey.isPressed) dirV -= Vector3.up;
                transform.position += dirV * voar * (k.leftShiftKey.isPressed ? 3f : 1f) * dt;
                return;
            }
            Vector3 dir = transform.forward * mv.y + transform.right * mv.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            float vel = k.leftShiftKey.isPressed ? correr : andar;
            if (_cc.isGrounded)
            {
                _vy = -1f;
                if (k.spaceKey.wasPressedThisFrame) _vy = 4.5f;
            }
            else _vy -= 9.81f * dt;
            _cc.Move((dir * vel + Vector3.up * _vy) * dt);
            if (transform.position.y < -20f) { _cc.enabled = false; transform.position = new Vector3(transform.position.x, 2f, -2f); _cc.enabled = true; }
        }

        // depois do movimento: a locomoção do corpo pela velocidade no chão e a câmera da terceira pessoa
        void LateUpdate()
        {
            if (corpo != null)
            {
                Vector3 v = _cc.enabled ? _cc.velocity : Vector3.zero;
                v.y = 0f;
                _velocidade = Mathf.Lerp(_velocidade, v.magnitude, 1f - Mathf.Exp(-12f * Time.deltaTime));
                corpo.SetFloat(SpeedId, _velocidade);
            }
            if (_planta) { _mirada = null; _movel = null; return; }
            if (!_terceira)
            {
                cabeca.localPosition = _cabecaNoCorpo;
                MostrarCorpo(false);
                Mirar();
                return;
            }
            // primeiro para o lado (sem passar de uma parede ao lado), depois para trás, até a parede
            Vector3 olho = transform.TransformPoint(_cabecaNoCorpo);
            Quaternion rot = cabeca.rotation;
            Vector3 lado = rot * new Vector3(ombro.x, ombro.y, 0f);
            float l = lado.magnitude;
            Vector3 noOmbro = olho + lado;
            if (l > 0.001f && Physics.SphereCast(olho, RaioDaCamera, lado / l, out var aoLado, l, ~0, QueryTriggerInteraction.Ignore))
                noOmbro = olho + lado / l * Mathf.Max(0f, aoLado.distance - 0.02f);
            Vector3 tras = rot * Vector3.back;
            float longe = Mathf.Max(0f, -ombro.z), d = longe;
            if (longe > 0f && Physics.SphereCast(noOmbro, RaioDaCamera, tras, out var atras, longe, ~0, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0f, atras.distance - 0.02f);
            _distancia = d < _distancia ? d : Mathf.Lerp(_distancia, d, 1f - Mathf.Exp(-4f * Time.deltaTime));
            cabeca.position = noOmbro + tras * _distancia;
            MostrarCorpo(_distancia >= PertoDemais);
            Mirar();
        }

        // a parte de móvel no meio da tela: o primeiro acerto do raio da câmera (fora o próprio jogador), até o alcance
        // contado da cabeça
        void Mirar()
        {
            _mirada = null;
            var raio = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 olho = transform.TransformPoint(_cabecaNoCorpo);
            int n = Physics.RaycastNonAlloc(raio, _acertos, Vector3.Distance(raio.origin, olho) + Alcance, ~0, QueryTriggerInteraction.Ignore);
            int melhor = -1;
            for (int i = 0; i < n; i++)
                if (!_acertos[i].collider.transform.IsChildOf(transform) && (melhor < 0 || _acertos[i].distance < _acertos[melhor].distance)) melhor = i;
            _movel = null;
            if (melhor < 0 || Vector3.Distance(_acertos[melhor].point, olho) > Alcance) return;
            _mirada = _acertos[melhor].collider.GetComponentInParent<ParteQueAbre>();
            _movel = _acertos[melhor].collider.GetComponentInParent<MovelDoMostruario>();
        }

        /// <summary>Na calçada, de frente para a parada i (com a planta aberta, a planta dela).</summary>
        public void IrPara(int i)
        {
            var p = _rua.Paradas[i];
            _parada = i;
            _cc.enabled = false;
            transform.position = p.Chegada;
            _cc.enabled = !_voando;
            _giro = p.Giro;
            _inclinacao = 0f;
            _vy = 0f;
            if (_planta) AbrirPlanta(i);
        }

        /// <summary>A planta da parada i, no piso mais perto dos pés.</summary>
        public void AbrirPlanta(int i)
        {
            if (i < 0) return;
            var p = _rua.Paradas[i];
            _parada = i;
            // o piso mais perto dos pés (da calçada, o térreo)
            _piso = 0;
            for (int f = 1; f < p.Pisos.Count; f++)
                if (Mathf.Abs(p.Pisos[f] - transform.position.y) < Mathf.Abs(p.Pisos[_piso] - transform.position.y)) _piso = f;
            _tamanho = Mathf.Max(p.Limites.extents.x, p.Limites.extents.z) * 1.08f + 0.5f;
            if (!_planta && _sol != null) { _sombras = _sol.shadows; _sol.shadows = LightShadows.None; }
            if (!_planta)
            {
                _posProcessamento = GameObject.Find("Pós-processamento (dia)");
                if (_posProcessamento != null) _posProcessamento.SetActive(false);
            }
            _planta = true;
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.02f;
        }

        public void FecharPlanta()
        {
            if (!_planta) return;
            _planta = false;
            _cam.orthographic = false;
            _cam.nearClipPlane = 0.05f;
            cabeca.localPosition = _cabecaNoCorpo;
            if (_sol != null) _sol.shadows = _sombras;
            if (_posProcessamento != null) _posProcessamento.SetActive(true);
        }

        void OnDisable() => FecharPlanta();

        void OnGUI()
        {
            if (_texto == null)
            {
                // brancos (a cor do skin saía preta no fundo escuro); a sombra dos rótulos é a mesma letra em preto (GUI.color)
                _texto = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = false };
                _texto.normal.textColor = Color.white;
                _rotulo = new GUIStyle(_texto) { fontSize = 12, wordWrap = false, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            string ajuda = "WASD andar · Shift correr · Espaço pular · V voar (Espaço/Ctrl sobem e descem) · T terceira pessoa · E abre e fecha (mirando) · R gerar de novo (semente nova) · H esconde o texto · clique prende o mouse, Esc solta";
            if (TemParadas) ajuda += "\n← → a construção anterior e a próxima · P a planta (↑ ↓ o piso, a roda aproxima)";
            if (_rua != null && _rua.AjudaDasTeclas != null) ajuda += " · " + _rua.AjudaDasTeclas;
            if (_planta)
            {
                var pp = _rua.Paradas[_parada];
                ajuda += $"\nA planta: {pp.NomesDosPisos[_piso]} ({_piso + 1} de {pp.Pisos.Count}, de baixo para cima)";
            }
            float largura = Mathf.Min(760f, Screen.width - 24f);
            string texto = _rua == null || _semTexto ? "" : _planta ? _rua.Paradas[_parada].Ficha : _rua.Texto(transform.position);
            float alto = _texto.CalcHeight(new GUIContent(ajuda + "\n\n" + texto), largura);
            GUI.Box(new Rect(6, 4, largura + 12, alto + 10), GUIContent.none);
            GUI.Box(new Rect(6, 4, largura + 12, alto + 10), GUIContent.none);
            GUI.Label(new Rect(12, 8, largura, alto), ajuda + "\n\n" + texto, _texto);

            if (!_planta)
            {
                // a mira (um ponto no meio da tela, amarelo numa parte que abre) e o aviso do E
                if (_aviso == null) _aviso = new GUIStyle(_texto) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                float mx = Screen.width / 2f, my = Screen.height / 2f;
                var antes = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(mx - 3f, my - 3f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color = _mirada != null || _movel != null ? new Color(1f, 0.85f, 0.3f) : Color.white;
                GUI.DrawTexture(new Rect(mx - 2f, my - 2f, 4f, 4f), Texture2D.whiteTexture);
                GUI.color = antes;
                if (_mirada == null && _movel == null) return;
                // o nome do móvel e a cor dele, o [E] da porta ou da gaveta e o [C] da cor
                string aviso = "";
                if (_movel != null && _movel.Tipo != null)
                {
                    aviso = _movel.Tipo.nome;
                    if (_movel.Quantas > 1) aviso += $" ({_movel.NomeDaVariacao}, {_movel.variacao + 1} de {_movel.Quantas})\n[C] Outra cor";
                }
                if (_mirada != null) aviso += (aviso.Length > 0 ? "   " : "") + $"[E] {(_mirada.Aberta ? "Fechar" : "Abrir")} {_mirada.Nome}";
                var r = new Rect(mx - 320f, my + 18f, 640f, 56f);
                GUI.color = Color.black;
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), aviso, _aviso);
                GUI.color = antes;
                GUI.Label(r, aviso, _aviso);
                return;
            }
            // o nome e o tamanho de cada cômodo do piso, no meio dele
            var p = _rua.Paradas[_parada];
            foreach (var (centro, piso, rotulo) in p.Comodos)
            {
                if (piso != _piso) continue;
                var s = _cam.WorldToScreenPoint(centro);
                if (s.z < 0f) continue;
                var r = new Rect(s.x - 70f, Screen.height - s.y - 18f, 140f, 36f);
                var cor = GUI.color;
                GUI.color = Color.black;
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), rotulo, _rotulo);
                GUI.color = cor;
                GUI.Label(r, rotulo, _rotulo);
            }
        }
    }
}
