using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O jogador na rede. O dono controla o próprio movimento (NetworkTransform com autoridade do dono) e joga em
    /// primeira pessoa: a câmera do mundo, a câmera dos braços por cima (camada própria, não atravessa paredes) e o
    /// próprio corpo só como sombra. Os outros veem o corpo animado, para onde ele olha, a lanterna e os tiros.
    /// </summary>
    public class JogadorRede : NetworkBehaviour
    {
        public const int CamadaBracos = 6;

        [Header("Primeira pessoa (só o dono)")]
        public Camera cameraMundo;
        public Camera cameraBracos;
        public AudioListener ouvido;
        public GameObject bracos;
        public Animator animadorBracos;

        [Header("Corpo (o que os outros veem)")]
        public Animator animadorCorpo;
        public Renderer[] renderersCorpo;
        public Transform ossoPeito;
        public Transform ossoCabeca;
        public Transform bocaCorpo;

        [Header("Cores da família")]
        [Tooltip("Jogador_Variacao0..3: cada jogador recebe uma, na ordem em que entra.")]
        public Material[] variacoes;
        [Tooltip("Renderers que trocam de cor (o material 0 de cada um).")]
        public Renderer[] pintaveis;

        /// <summary>Para onde a cabeça aponta na vertical, em graus (positivo = olhando para baixo).</summary>
        public readonly NetworkVariable<float> Pitch = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Mirando = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> Cor = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Quantos tiros deste jogador chegaram pela rede (para testes).</summary>
        public int TirosRecebidos { get; private set; }
        /// <summary>Velocidade no chão (m/s): a real no dono, a medida pela posição recebida nos outros.</summary>
        public float Velocidade => _velocidade;
        public LanternaJogador Lanterna { get; private set; }
        public VidaJogador Vida { get; private set; }

        public static JogadorRede Local { get; private set; }
        public static readonly List<JogadorRede> Todos = new List<JogadorRede>();

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AimingId = Animator.StringToHash("Aiming");
        static readonly int FireId = Animator.StringToHash("Fire");

        CharacterController _corpoFisico;
        Vector3 _posJanela;
        float _velocidade, _medidaRemota, _janela;

        public override void OnNetworkSpawn()
        {
            Todos.Add(this);
            _corpoFisico = GetComponent<CharacterController>();
            Lanterna = GetComponent<LanternaJogador>();
            Vida = GetComponent<VidaJogador>();
            if (IsServer) Cor.Value = CorLivre();
            Cor.OnValueChanged += AoMudarCor;
            AplicarCor(Cor.Value);

            bool dono = IsOwner;
            cameraMundo.enabled = dono;
            cameraBracos.enabled = dono;
            ouvido.enabled = dono;
            bracos.SetActive(dono);
            foreach (var r in renderersCorpo)
                r.shadowCastingMode = dono ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            GetComponent<MovimentoPrimeiraPessoa>().enabled = dono;
            GetComponent<ArmaJogador>().enabled = dono;
            if (dono)
            {
                Local = this;
                CameraDoMenu.Ligar(false);
                var ponto = PontosDeNascimento.Ponto(OwnerClientId);
                GetComponent<MovimentoPrimeiraPessoa>().Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
            }
            _posJanela = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            Todos.Remove(this);
            Cor.OnValueChanged -= AoMudarCor;
            if (Local == this)
            {
                Local = null;
                CameraDoMenu.Ligar(true);
            }
        }

        int CorLivre()
        {
            for (int c = 0; c < variacoes.Length; c++)
            {
                bool usada = false;
                foreach (var j in Todos) usada |= j != this && j.Cor.Value == c;
                if (!usada) return c;
            }
            return (int)(OwnerClientId % (ulong)Mathf.Max(1, variacoes.Length));
        }

        void AoMudarCor(int antes, int depois) => AplicarCor(depois);

        void AplicarCor(int indice)
        {
            if (variacoes == null || variacoes.Length == 0) return;
            var material = variacoes[Mathf.Clamp(indice, 0, variacoes.Length - 1)];
            foreach (var r in pintaveis)
            {
                var mats = r.sharedMaterials;
                mats[0] = material;
                r.sharedMaterials = mats;
            }
        }

        void Update()
        {
            if (!IsSpawned) return;
            // Velocidade no chão: o dono usa a do próprio controle; os outros medem a posição recebida a cada 0,1 s
            // (quadro a quadro a medida oscila, porque a posição chega em degraus).
            float medida;
            if (IsOwner && _corpoFisico != null)
                medida = new Vector3(_corpoFisico.velocity.x, 0f, _corpoFisico.velocity.z).magnitude;
            else
            {
                _janela += Time.deltaTime;
                if (_janela >= 0.1f)
                {
                    Vector3 delta = transform.position - _posJanela;
                    delta.y = 0f;
                    _medidaRemota = Mathf.Min(delta.magnitude / _janela, 6f);
                    _posJanela = transform.position;
                    _janela = 0f;
                }
                medida = _medidaRemota;
            }
            _velocidade = Mathf.Lerp(_velocidade, medida, 1f - Mathf.Exp(-12f * Time.deltaTime));
            animadorCorpo.SetFloat(SpeedId, _velocidade);
            animadorCorpo.SetBool(AimingId, Mirando.Value);
            if (IsOwner) animadorBracos.SetFloat(SpeedId, _velocidade);
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            // O corpo acompanha o olhar: metade no peito, metade na cabeça, por cima da animação.
            float pitch = Pitch.Value;
            Vector3 eixo = transform.right;
            if (ossoPeito != null) ossoPeito.rotation = Quaternion.AngleAxis(pitch * 0.5f, eixo) * ossoPeito.rotation;
            if (ossoCabeca != null) ossoCabeca.rotation = Quaternion.AngleAxis(pitch * 0.5f, eixo) * ossoCabeca.rotation;
        }

        /// <summary>Chamado pelo dono ao atirar: o corpo dele (a sombra) e o dos outros mostram o tiro.</summary>
        public void AnunciarTiro(bool acertou, Vector3 ponto, Vector3 normal)
        {
            animadorCorpo.SetTrigger(FireId);
            if (IsServer) ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this);
            TiroRpc(acertou, ponto, normal);
        }

        [Rpc(SendTo.NotMe)]
        void TiroRpc(bool acertou, Vector3 ponto, Vector3 normal)
        {
            TirosRecebidos++;
            // no anfitrião, o tiro de um amigo chama os zumbis em volta dele
            if (IsServer) ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this);
            animadorCorpo.SetTrigger(FireId);
            Efeitos.Clarao(bocaCorpo, 0);
            Efeitos.Som(Efeitos.Tiro, bocaCorpo.position);
            if (acertou) Efeitos.Impacto(ponto, normal);
        }
    }
}
