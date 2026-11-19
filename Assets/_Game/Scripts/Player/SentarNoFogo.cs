using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Sentar no chão perto da fogueira acesa (18/11/2026, a etapa 1 da fogueira, a pedido: "um sistema de sentar em volta
    /// da fogueira pra descansar sem dormir, recupera um pouco de sanidade (menor do que dormir) ... daria para fumar, assar
    /// sentado"). Só no dono: segurar o E na fogueira acesa senta virado para ela (a pose no PassageiroDoCarro.NoChao.cs), o
    /// olho desce, e dá para olhar em volta, fumar e (as próximas etapas) assar e tocar; andar, pular ou o Ctrl levantam.
    /// A mente sobe no anfitrião (VidaJogador.Mente.cs: sentado com uma fogueira acesa a até 4 m).
    /// </summary>
    [DefaultExecutionOrder(102)]
    public class SentarNoFogo : MonoBehaviour
    {
        /// <summary>O jogador daqui está sentado no chão.</summary>
        public static bool Sentado { get; private set; }

        const float OlhoSentado = 0.86f, CapsulaSentado = 1.0f, DistanciaDoFogo = 1.35f;

        JogadorRede _rede;
        MovimentoPrimeiraPessoa _mov;
        CharacterController _cc;
        float _alturaAntes, _desde;
        Vector3 _centroAntes;
        bool _sentado, _levantar;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _mov = GetComponent<MovimentoPrimeiraPessoa>();
            _cc = GetComponent<CharacterController>();
        }

        void OnDisable()
        {
            if (_sentado) Levantar();
        }

        bool PodeFicar =>
            _rede != null && _rede.IsSpawned && _rede.IsOwner && _mov != null && _mov.enabled && !_mov.Voando && !_mov.Nadando
            && (_rede.Passageiro == null || !_rede.Passageiro.NoCarro) && (_rede.Vida == null || _rede.Vida.Vida.Value > 0f);

        /// <summary>Senta virado para o fogo (só no dono).</summary>
        public void Sentar(Vector3 fogo)
        {
            if (_sentado || !PodeFicar) return;
            GetComponent<ProtecaoDoJogador>()?.SairDaProtecao();
            if (_mov.Agachado) _mov.PedirAbaixar(false);
            _alturaAntes = _cc.height;
            _centroAntes = _cc.center;
            _cc.height = CapsulaSentado;
            _cc.center = new Vector3(_centroAntes.x, CapsulaSentado * 0.5f, _centroAntes.z);
            Vector3 para = fogo - transform.position;
            para.y = 0f;
            // perto demais, recua: as pernas esticadas (uns 0,8 m) não entram no fogo
            float dist = para.magnitude;
            if (dist > 0.01f && dist < DistanciaDoFogo) _cc.Move(-para / dist * (DistanciaDoFogo - dist));
            float yaw = para.sqrMagnitude > 0.01f ? Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
            _mov.Olhar(yaw, 12f);
            _mov.OlhoForcado = OlhoSentado;
            _mov.FiltroDoPasso = SemAndar;
            _mov.CorpoParado = true;   // (o mouse vira só a cabeça, 18/11/2026)
            _rede.SentadoNoChao.Value = true;
            _sentado = Sentado = true;
            _levantar = false;
            _desde = Time.time;
            InventarioJogador.AvisoLocal("Sentado perto do fogo, descansando (andar levanta)");
        }

        /// <summary>Levanta (só no dono).</summary>
        public void Levantar()
        {
            if (!_sentado) return;
            _sentado = Sentado = false;
            if (_mov != null)
            {
                if (_mov.FiltroDoPasso == (System.Func<Vector3, Vector3>)SemAndar) _mov.FiltroDoPasso = null;
                _mov.OlhoForcado = null;
                _mov.CorpoParado = false;
            }
            if (_cc != null)
            {
                _cc.height = _alturaAntes;
                _cc.center = _centroAntes;
            }
            if (_rede != null && _rede.IsSpawned && _rede.IsOwner && _rede.SentadoNoChao.Value) _rede.SentadoNoChao.Value = false;
        }

        // sentado, ele não anda: querer andar levanta (depois de meio segundo sentado, para o próprio E não levantar)
        Vector3 SemAndar(Vector3 desejada)
        {
            if (desejada.sqrMagnitude > 0.05f && Time.time - _desde > 0.5f) _levantar = true;
            return Vector3.zero;
        }

        void Update()
        {
            if (!_sentado) return;
            if (!PodeFicar)
            {
                Levantar();
                return;
            }
            var teclado = Keyboard.current;
            if (MenuOnline.Controla && teclado != null && Time.time - _desde > 0.5f
                && (teclado.spaceKey.wasPressedThisFrame || teclado.leftCtrlKey.wasPressedThisFrame))
                _levantar = true;
            if (_levantar) Levantar();
        }
    }
}
