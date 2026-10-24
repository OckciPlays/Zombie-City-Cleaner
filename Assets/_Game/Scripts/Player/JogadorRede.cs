using System.Collections;
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
    /// próprio corpo só como sombra. Os outros veem o corpo animado, para onde ele olha, a arma da mão, a lanterna e os
    /// tiros.
    /// </summary>
    public class JogadorRede : NetworkBehaviour
    {
        public const int CamadaBracos = 6;
        /// <summary>A raiz do jogador (o CharacterController): os pedaços dos zumbis não param em cima de ninguém.</summary>
        public const int CamadaJogador = 9;

        [Header("Primeira pessoa (só o dono)")]
        public Camera cameraMundo;
        public Camera cameraBracos;
        public AudioListener ouvido;
        public GameObject bracos;

        [Header("Corpo (o que os outros veem)")]
        public Animator animadorCorpo;
        public Renderer[] renderersCorpo;
        public Transform ossoPeito;
        public Transform ossoCabeca;

        [Header("A arma no corpo (uma por arma, na ordem de TipoDeArma)")]
        [Tooltip("As armas avulsas dentro do Arma_TP (filho do weapon.R): só a da mão fica ligada.")]
        public GameObject[] armasCorpo;
        public Transform[] bocasCorpo;
        [Tooltip("O controlador do corpo com cada arma: o da pistola e os override com as animações de arma longa.")]
        public RuntimeAnimatorController[] controladoresCorpo;

        [Header("Cores da família")]
        [Tooltip("Jogador_Variacao0..3: cada jogador recebe uma, na ordem em que entra.")]
        public Material[] variacoes;
        [Tooltip("Renderers que trocam de cor (o material 0 de cada um).")]
        public Renderer[] pintaveis;

        /// <summary>Para onde a cabeça aponta na vertical, em graus (positivo = olhando para baixo).</summary>
        public readonly NetworkVariable<float> Pitch = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Mirando = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>
        /// Olhando pela luneta (a sniper). O anfitrião usa para os zumbis na linha da luneta: de longe eles contam como
        /// se estivessem perto (aparecem, têm hitboxes e vão pela rede), na proporção do aumento.
        /// </summary>
        public readonly NetworkVariable<bool> Luneta = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> Cor = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>A arma da mão (<see cref="TipoDeArma"/>). Só o anfitrião troca (no suporte de armas do bunker).</summary>
        public readonly NetworkVariable<byte> Arma = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
            // a arma do corpo do dono fica desligada, nem sombra: a ponta das armas longas passa dos 60 cm em que a sombra
            // da lanterna começa (shadowNearPlane), e a sombra do cano aparecia no facho, na frente da arma de primeira
            // pessoa
            if (dono)
                foreach (var a in armasCorpo) a.GetComponent<Renderer>().enabled = false;
            GetComponent<MovimentoPrimeiraPessoa>().enabled = dono;
            GetComponent<ArmaJogador>().enabled = dono;
            Arma.OnValueChanged += AoMudarArma;
            AplicarArma(Arma.Value);
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
            // quem sai da partida devolve a arma ao painel do bunker (a pistola, a uma vaga de pistola livre)
            if (IsServer && !NetworkManager.ShutdownInProgress && ParedeDeArmas.Instancia != null)
                ParedeDeArmas.Instancia.Devolver((TipoDeArma)Arma.Value);
            Cor.OnValueChanged -= AoMudarCor;
            Arma.OnValueChanged -= AoMudarArma;
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

        void AoMudarArma(byte antes, byte depois) => AplicarArma(depois);

        /// <summary>
        /// A arma da mão: o modelo no corpo, as animações do corpo (pistola ou arma longa) e, no dono, a de primeira
        /// pessoa.
        /// </summary>
        void AplicarArma(byte arma)
        {
            int a = Mathf.Clamp(arma, 0, Armas.Quantas - 1);
            for (int i = 0; i < armasCorpo.Length; i++) armasCorpo[i].SetActive(i == a);
            if (animadorCorpo.runtimeAnimatorController != controladoresCorpo[a]) animadorCorpo.runtimeAnimatorController = controladoresCorpo[a];
            if (IsOwner) GetComponent<ArmaJogador>().Trocar((TipoDeArma)a);
        }

        Transform BocaCorpo => bocasCorpo[Mathf.Clamp(Arma.Value, 0, Armas.Quantas - 1)];

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

        /// <summary>O que o tiro acertou, para os outros mostrarem o efeito certo.</summary>
        public enum TipoDeAcerto : byte { Nada, Cenario, Carne, Outro }

        /// <summary>Onde um tiro (ou um chumbo da espingarda) parou, e no quê.</summary>
        public struct Impacto : INetworkSerializable
        {
            public TipoDeAcerto tipo;
            public Vector3 ponto, normal;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref tipo);
                s.SerializeValue(ref ponto);
                s.SerializeValue(ref normal);
            }
        }

        /// <summary>Chamado pelo dono ao atirar: o corpo dele (a sombra) e o dos outros mostram o tiro.</summary>
        public void AnunciarTiro(Impacto[] impactos)
        {
            animadorCorpo.SetTrigger(FireId);
            if (IsServer) ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this);
            TiroRpc(impactos);
        }

        [Rpc(SendTo.NotMe)]
        void TiroRpc(Impacto[] impactos)
        {
            TirosRecebidos++;
            // no anfitrião, o tiro de um amigo chama os zumbis em volta dele
            if (IsServer) ZombieManager.Barulho(transform.position + Vector3.up * 1.4f, this);
            animadorCorpo.SetTrigger(FireId);
            var boca = BocaCorpo;
            Efeitos.Clarao(boca, 0);
            Efeitos.Som(Efeitos.Tiros[Mathf.Clamp(Arma.Value, 0, Armas.Quantas - 1)], boca.position, 1f, true, 150f);   // tiro se ouve longe
            if (Arma.Value == (byte)TipoDeArma.Espingarda) StartCoroutine(SomDepois(boca, Efeitos.Bomba, 0.33f, 0.8f));
            if (Armas.Dados((TipoDeArma)Arma.Value).ferrolho) StartCoroutine(SomDepois(boca, Efeitos.Ferrolho, 0.42f, 0.7f));
            foreach (var i in impactos)
            {
                if (i.tipo == TipoDeAcerto.Carne) Sangue.Respingo(i.ponto, i.normal);
                else if (i.tipo != TipoDeAcerto.Nada) Efeitos.Impacto(i.ponto, i.normal);
                if (i.tipo == TipoDeAcerto.Cenario) Sangue.FuroDeTiro(i.ponto, i.normal);
            }
        }

        IEnumerator SomDepois(Transform boca, AudioClip clip, float espera, float volume)
        {
            yield return new WaitForSeconds(espera);
            if (boca != null) Efeitos.Som(clip, boca.position, volume, true, 25f);
        }

        /// <summary>
        /// O dono acertou um zumbi (a cópia dele, ou o zumbi de verdade no anfitrião). Quem decide o dano é o anfitrião:
        /// o acerto vai para ele com o Id, a geração, a região, o membro, o dano, o ponto e se o corpo deve ser
        /// arremessado (a espingarda de perto). A região, o membro e o arremesso vão num byte só.
        /// </summary>
        public void AcertarZumbi(Zumbi zumbi, RegiaoDoCorpo regiao, Membro membro, float dano, Vector3 ponto, bool arremesso)
        {
            if (IsServer) ZombieManager.Acertar(zumbi.Id, zumbi.Geracao, regiao, membro, dano, ponto, arremesso, this);
            else AcertoRpc((ushort)zumbi.Id, zumbi.Geracao, (byte)((byte)regiao | ((byte)membro << 3) | (arremesso ? 128 : 0)), dano, ponto);
        }

        [Rpc(SendTo.Server)]
        void AcertoRpc(ushort id, byte geracao, byte parte, float dano, Vector3 ponto, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;   // só o dono atira com este jogador
            var regiao = (RegiaoDoCorpo)(parte & 7);
            var membro = (Membro)Mathf.Min((parte >> 3) & 15, (int)Membro.CanelaD);
            ZombieManager.Acertar(id, geracao, regiao, membro, dano, ponto, (parte & 128) != 0, this);
        }
    }
}
