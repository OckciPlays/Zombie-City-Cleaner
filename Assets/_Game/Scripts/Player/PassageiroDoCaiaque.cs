using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jogador no caiaque (06/11/2026), em todas as máquinas (posto pelo VidaJogador): o caiaque diz o lugar
    /// (<see cref="Caiaque"/>), e aqui o corpo fica preso no banco, sentado, sem a cápsula e sem os passos. No dono: o andar
    /// para; o mouse olha em volta do banco; o E sai; o TAB abre o inventário com o compartimento. Quem vai na frente (o
    /// lugar 1) fica com as mãos livres: a vara de pesca e a arma funcionam. Saindo, ele desce do lado: na terra, se houver;
    /// senão, na água (nadando).
    /// <para>
    /// Quem rema (o lugar 0; 07/11/2026, a pedido: "o certo é pescar quando paramos, mesmo se estivermos sozinho"): com o
    /// remo na mão, rema (os braços remando de primeira pessoa, o <c>Resources/BracosRemando</c>; o corpo, o Remar); parado
    /// <see cref="LargarDepois"/> s, larga o remo no casco e fica com as mãos livres, como o da frente; o W, o S, o A ou o D
    /// pegam o remo de novo. O estado vai pela rede no caiaque (<see cref="Caiaque.RemoNaMao"/>, <see cref="Caiaque.Remando"/>).
    /// </para>
    /// <para>
    /// O corpo: com o parâmetro "Remando" no controlador (o MontagemDaAgua; 1 sentado no caiaque, o Remar_Parado; 2 remando,
    /// o Remar), as pernas esticadas debaixo do convés e o quadril no banco; a camada dos braços sai com o remo na mão. Sem o
    /// parâmetro, o "Lugar" 2 (o sentado do carro), como antes.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class PassageiroDoCaiaque : MonoBehaviour
    {
        public Caiaque Caiaque { get; private set; }
        public int Lugar { get; private set; } = -1;
        public bool NoCaiaque => Caiaque != null;

        /// <summary>Parado assim (s), quem rema larga o remo e fica com as mãos livres.</summary>
        public const float LargarDepois = 1.2f;

        /// <summary>O jogador (o componente qualquer dele) está sentado num caiaque?</summary>
        public static bool Sentado(Component c)
        {
            var p = c != null ? c.GetComponent<PassageiroDoCaiaque>() : null;
            return p != null && p.NoCaiaque;
        }

        static readonly int LugarId = Animator.StringToHash("Lugar");
        static readonly int RemandoId = Animator.StringToHash("Remando");
        const int MascaraDoChao = ~((1 << 2) | (1 << 4) | (1 << 6) | (1 << 10) | (1 << 11) | (1 << 12));
        static readonly int SpeedId = Animator.StringToHash("Speed");

        JogadorRede _rede;
        CharacterController _cc;
        MovimentoPrimeiraPessoa _movimento;
        ArmaJogador _arma;
        PassosJogador _passos;
        Transform _cabeca, _camera;
        Vector3 _cabecaLocal;
        Vector3 _quadrilNaRaiz = new Vector3(0f, 0.888f, 0f);
        InputAction _olhar, _interagir, _mover;
        bool _ccLigado = true, _semMaos, _remoLocal, _avisouDoRemo;
        float _yaw, _pitch, _sentouEm, _paradoDesde;
        int _temLugar = -1, _temRemando = -1;
        Animator _animadorVisto;
        GameObject _bracosRemando;
        Animator _animadorRemando;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _cc = GetComponent<CharacterController>();
            _movimento = GetComponent<MovimentoPrimeiraPessoa>();
            _arma = GetComponent<ArmaJogador>();
            _passos = GetComponent<PassosJogador>();
            var acoes = InputSystem.actions;
            _olhar = acoes.FindAction("Player/Look", true);
            _interagir = acoes.FindAction("Player/Interact", true);
            _mover = acoes.FindAction("Player/Move", true);
        }

        void Start()
        {
            if (_movimento != null) _cabeca = _movimento.cabeca;
            if (_rede != null && _rede.cameraMundo != null) _camera = _rede.cameraMundo.transform;
            if (_cabeca != null) _cabecaLocal = _cabeca.localPosition;
            // o quadril em pé, a partir da raiz (do corpo de agora)
            var animador = _rede != null ? _rede.animadorCorpo : null;
            if (animador != null)
                foreach (var t in animador.GetComponentsInChildren<Transform>(true))
                    if (t.name == "hips")
                    {
                        var q = transform.InverseTransformPoint(t.position);
                        if (q.y > 0.5f && q.y < 1.3f) _quadrilNaRaiz = q;
                        break;
                    }
        }

        /// <summary>Senta no lugar do caiaque (chamado pelo caiaque, em cada máquina, a cada quadro).</summary>
        public void Sentar(Caiaque c, int lugar)
        {
            if (Caiaque == c && Lugar == lugar) return;
            if (Caiaque != null) Restaurar();
            Caiaque = c;
            Lugar = lugar;
            _sentouEm = Time.time;
            _paradoDesde = Time.time;
            _ccLigado = _cc == null || _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            if (_passos != null) _passos.enabled = false;
            // quem senta para remar já pega o remo
            _remoLocal = lugar == 0;
            _avisouDoRemo = false;
            _semMaos = false;
            Maos(lugar == 0);
            if (!_rede.IsOwner) return;
            if (_movimento != null) _movimento.enabled = false;
            _yaw = 0f;
            _pitch = 8f;
        }

        /// <summary>Sai do caiaque (o lugar ficou vazio): no dono, desce do lado.</summary>
        public void Levantar()
        {
            if (Caiaque == null) return;
            var c = Caiaque;
            int l = Lugar;
            Caiaque = null;
            Lugar = -1;
            Restaurar();
            if (_rede.IsOwner && _movimento != null && c != null)
            {
                Saida(c, l, out var ponto, out float yaw);
                _movimento.Teleportar(ponto, yaw);
            }
        }

        // as mãos de quem está sentado: com o remo (sem a arma, os braços remando) ou livres (a vara, a arma)
        void Maos(bool comORemo)
        {
            if (comORemo == _semMaos) return;
            _semMaos = comORemo;
            _rede.EsconderNaMao(comORemo);
            if (!_rede.IsOwner) return;
            if (comORemo)
            {
                if (_arma != null)
                {
                    _arma.enabled = false;
                    _arma.EsconderAMao();
                }
                // (07/11/2026) na primeira pessoa, o próprio corpo sem a cabeça segura o remo (o mesmo que os outros veem:
                // JogadorRede.CorpoNoVeiculo, a cada quadro no LateUpdate); os braços de primeira pessoa somem
                if (_rede.bracos != null) _rede.bracos.SetActive(false);
            }
            else
            {
                if (_bracosRemando != null) _bracosRemando.SetActive(false);
                _rede.CorpoNoVeiculo(false);
                if (_rede.bracos != null) _rede.bracos.SetActive(true);
                if (_arma != null)
                {
                    _arma.enabled = true;
                    _arma.MostrarModeloDaMao(true);
                }
            }
        }

        void Restaurar()
        {
            var a = _rede != null ? _rede.animadorCorpo : null;
            if (a != null)
            {
                if (TemLugar(a)) a.SetInteger(LugarId, 0);
                if (TemRemando(a)) a.SetInteger(RemandoId, 0);
                if (a.layerCount > 1) a.SetLayerWeight(1, 1f);
            }
            if (_cc != null) _cc.enabled = _ccLigado;
            if (_passos != null) _passos.enabled = true;
            Maos(false);
            if (!_rede.IsOwner)
            {
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                return;
            }
            if (_movimento != null) _movimento.enabled = true;
            if (_arma != null) _arma.enabled = true;
            if (_rede.bracos != null) _rede.bracos.SetActive(true);
            if (_cabeca != null) _cabeca.localPosition = _cabecaLocal;
            if (_camera != null) _camera.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        bool TemLugar(Animator a)
        {
            Conferir(a);
            return _temLugar == 1;
        }

        bool TemRemando(Animator a)
        {
            Conferir(a);
            return _temRemando == 1;
        }

        // os parâmetros do controlador do corpo (lidos de novo quando o corpo muda)
        void Conferir(Animator a)
        {
            if (a == _animadorVisto && _temLugar >= 0) return;
            _animadorVisto = a;
            _temLugar = _temRemando = 0;
            foreach (var p in a.parameters)
            {
                if (p.nameHash == LugarId) _temLugar = 1;
                if (p.nameHash == RemandoId) _temRemando = 1;
            }
        }

        // desce do lado do lugar: a terra seca mais perto (dos lados, atrás, na frente); sem terra, na água, boiando
        static void Saida(Caiaque c, int lugar, out Vector3 ponto, out float yaw)
        {
            var t = c.transform;
            yaw = t.eulerAngles.y;
            float z = c.assentos != null && lugar < c.assentos.Length && c.assentos[lugar] != null ? t.InverseTransformPoint(c.assentos[lugar].position).z : 0f;
            Vector3[] candidatos =
            {
                t.TransformPoint(new Vector3(-1.1f, 0f, z)), t.TransformPoint(new Vector3(1.1f, 0f, z)),
                t.TransformPoint(new Vector3(0f, 0f, -c.meioComprimento - 0.8f)), t.TransformPoint(new Vector3(0f, 0f, c.meioComprimento + 0.8f)),
            };
            foreach (var p in candidatos)
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var chao, 6f, MascaraDoChao, QueryTriggerInteraction.Ignore)
                    && Agua.Fundura(chao.point) < 0.6f)
                {
                    ponto = chao.point + Vector3.up * 0.05f;
                    return;
                }
            ponto = candidatos[0];
            ponto.y = Agua.Nivel(ponto, out float nivel) ? nivel - 1.3f : t.position.y;
        }

        void LateUpdate()
        {
            if (Caiaque == null) return;
            if (!Caiaque.IsSpawned || Caiaque.assentos == null || Lugar < 0 || Lugar >= Caiaque.assentos.Length || Caiaque.assentos[Lugar] == null)
            {
                Caiaque = null;
                Lugar = -1;
                Restaurar();
                return;
            }
            // quem rema: no dono, o remo segue o que ele faz (pega com o W, o S, o A ou o D; parado, larga); nos outros, o
            // que vem pela rede
            bool remando = false;
            if (Lugar == 0)
            {
                if (_rede.IsOwner)
                {
                    Vector2 m = MenuOnline.Controla && _mover != null ? _mover.ReadValue<Vector2>() : Vector2.zero;
                    remando = m.sqrMagnitude > 0.01f;
                    if (remando)
                    {
                        _remoLocal = true;
                        _paradoDesde = Time.time;
                    }
                    else if (_remoLocal && Mathf.Abs(Caiaque.Velocidade) < 0.35f && Time.time - _paradoDesde > LargarDepois)
                    {
                        _remoLocal = false;
                        if (!_avisouDoRemo) InventarioJogador.AvisoLocal("Largou o remo: as mãos estão livres (W ou S pega o remo de novo)");
                        _avisouDoRemo = true;
                    }
                    if (Mathf.Abs(Caiaque.Velocidade) >= 0.35f) _paradoDesde = Time.time;   // (deslizando, ainda não parou)
                    Caiaque.PorORemo(_remoLocal, remando);
                    Maos(_remoLocal);
                }
                else
                {
                    remando = Caiaque.Remando;
                    Maos(Caiaque.RemoNaMao);
                }
            }
            var banco = Caiaque.assentos[Lugar];
            var a = _rede.animadorCorpo;
            bool poseDoCaiaque = a != null && TemRemando(a);
            // o quadril: na pose do caiaque, ele fica no banco (a raiz 2 cm abaixo); na do carro, o quadril em pé sobe ao banco
            Vector3 quadril = poseDoCaiaque ? new Vector3(0f, 0.02f, 0f) : _quadrilNaRaiz;
            transform.SetPositionAndRotation(banco.position - banco.rotation * quadril, banco.rotation);
            if (a != null)
            {
                if (poseDoCaiaque)
                {
                    if (TemLugar(a)) a.SetInteger(LugarId, 0);
                    a.SetInteger(RemandoId, _semMaos && remando ? 2 : 1);
                }
                else if (TemLugar(a)) a.SetInteger(LugarId, 2);
                if (a.layerCount > 1) a.SetLayerWeight(1, _semMaos ? 0f : 1f);
            }
            if (!_rede.IsOwner) return;
            // com o remo na mão, na primeira pessoa: o corpo sem a cabeça, com as mãos no remo (JogadorRede.CorpoNoVeiculo)
            if (_semMaos) _rede.CorpoNoVeiculo(true);
            if (_animadorRemando != null && _animadorRemando.isActiveAndEnabled)
                _animadorRemando.SetFloat(SpeedId, remando ? Mathf.Max(1.6f, Mathf.Abs(Caiaque.Velocidade)) : 0f);
            // o E sai (não no mesmo instante em que entrou)
            if (MenuOnline.Controla && Time.time - _sentouEm > 0.5f && _interagir.WasPressedThisFrame())
            {
                Caiaque.PedirSair();
                return;
            }
            // o olhar em volta do banco
            if (MenuOnline.Controla && _movimento != null)
            {
                Vector2 bruto = _olhar.ReadValue<Vector2>();
                Vector2 o = _olhar.activeControl != null && _olhar.activeControl.device is Gamepad
                    ? bruto * (_movimento.sensibilidadeControle * Time.deltaTime) : bruto * _movimento.sensibilidadeMouse;
                _yaw = Mathf.Clamp(_yaw + o.x, -150f, 150f);
                _pitch = Mathf.Clamp(_pitch - o.y, -60f, 75f);
            }
            // o corpo acompanha este olhar (como no carro, 07/11/2026: senão ficava o do momento de entrar)
            if (_rede != null && _rede.IsOwner && Mathf.Abs(_rede.Pitch.Value - _pitch) > 0.5f) _rede.Pitch.Value = _pitch;
            if (_cabeca != null)
            {
                Vector3 olho = quadril + (poseDoCaiaque ? new Vector3(0f, 0.68f, 0.06f) : new Vector3(0f, 0.62f, 0.08f));
                // com o corpo à mostra (o remo na mão), o olho nunca atrás nem abaixo do pescoço, que mexe com o remar
                // (07/11/2026, a pedido: "essa parte do corpo um pouco bugada quando olho pra baixo")
                if (_semMaos) olho = _rede.OlhoForaDoCorpo(olho);
                _cabeca.localPosition = olho;
                _cabeca.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }
}
