using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jogador no caiaque (06/11/2026), em todas as máquinas (posto pelo VidaJogador): o caiaque diz o lugar
    /// (<see cref="Caiaque"/>), e aqui o corpo fica preso no banco, sentado (o "Lugar" do Animator, como no carro), sem a
    /// cápsula e sem os passos. No dono: o andar para; o mouse olha em volta do banco; o E sai. Quem rema (o lugar 0) fica
    /// sem as mãos (o remo); quem vai na frente (o lugar 1) fica com elas: a vara de pesca e a arma funcionam. Saindo, ele
    /// desce do lado: na terra, se houver; senão, na água (nadando).
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class PassageiroDoCaiaque : MonoBehaviour
    {
        public Caiaque Caiaque { get; private set; }
        public int Lugar { get; private set; } = -1;
        public bool NoCaiaque => Caiaque != null;

        /// <summary>O jogador (o componente qualquer dele) está sentado num caiaque?</summary>
        public static bool Sentado(Component c)
        {
            var p = c != null ? c.GetComponent<PassageiroDoCaiaque>() : null;
            return p != null && p.NoCaiaque;
        }

        static readonly int LugarId = Animator.StringToHash("Lugar");
        const int MascaraDoChao = ~((1 << 2) | (1 << 4) | (1 << 6) | (1 << 10) | (1 << 11) | (1 << 12));

        JogadorRede _rede;
        CharacterController _cc;
        MovimentoPrimeiraPessoa _movimento;
        ArmaJogador _arma;
        PassosJogador _passos;
        Transform _cabeca, _camera;
        Vector3 _cabecaLocal;
        Vector3 _quadrilNaRaiz = new Vector3(0f, 0.888f, 0f);
        InputAction _olhar, _interagir;
        bool _ccLigado = true, _semMaos;
        float _yaw, _pitch, _sentouEm;
        int _temLugar = -1;

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
            _ccLigado = _cc == null || _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            if (_passos != null) _passos.enabled = false;
            _semMaos = lugar == 0;
            if (_semMaos) _rede.EsconderNaMao(true);
            if (!_rede.IsOwner) return;
            if (_movimento != null) _movimento.enabled = false;
            if (_semMaos)
            {
                if (_arma != null) _arma.enabled = false;
                if (_rede.bracos != null) _rede.bracos.SetActive(false);
            }
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

        void Restaurar()
        {
            var a = _rede != null ? _rede.animadorCorpo : null;
            if (a != null && TemLugar(a))
            {
                a.SetInteger(LugarId, 0);
                if (a.layerCount > 1) a.SetLayerWeight(1, 1f);
            }
            if (_cc != null) _cc.enabled = _ccLigado;
            if (_passos != null) _passos.enabled = true;
            if (_semMaos) _rede.EsconderNaMao(false);
            _semMaos = false;
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
            if (_temLugar < 0)
            {
                _temLugar = 0;
                foreach (var p in a.parameters) if (p.nameHash == LugarId) _temLugar = 1;
            }
            return _temLugar == 1;
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
            var banco = Caiaque.assentos[Lugar];
            transform.SetPositionAndRotation(banco.position - banco.rotation * _quadrilNaRaiz, banco.rotation);
            var a = _rede.animadorCorpo;
            if (a != null && TemLugar(a))
            {
                a.SetInteger(LugarId, 2);
                if (_semMaos && a.layerCount > 1) a.SetLayerWeight(1, 0f);
            }
            if (!_rede.IsOwner) return;
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
            if (_cabeca != null)
            {
                _cabeca.localPosition = _quadrilNaRaiz + new Vector3(0f, 0.62f, 0.08f);
                _cabeca.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }
}
