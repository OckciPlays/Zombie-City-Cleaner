using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A proteção do jogador na rede (18/11/2026, a etapa 4, a pedido: "encostar perto de uma casa, a gente aperta o botão
    /// Ctrl e pegamos uma proteção ... podemos atirar sem mirar, apenas com a arma para fora e podemos mirar colocando apenas
    /// a região da cabeça e parte da arma para fora"). O dono decide (<see cref="ProtecaoDoJogador"/>); aqui, o que os outros
    /// precisam: encostado ou não (e se a proteção é baixa), a inclinação do tronco espiando pela quina (os ossos da coluna e
    /// do peito girados depois da animação, como os dos sobreviventes) e, no anfitrião (onde os NPCs atiram), o corpo que leva
    /// o tiro: a cápsula de quem não é o dono acompanha o agachado (antes ficava em pé, 1,8 m, para os outros jogadores) e, na
    /// proteção, uma esfera na cabeça (a cabeça que sai pela quina leva tiro; a cápsula fica atrás da parede).
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>Encostado numa proteção: 0 não, 1 a alta, 2 a baixa (do dono).</summary>
        public readonly NetworkVariable<byte> NaProtecao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>O tronco inclinado para espiar pela quina, de -100 (esquerda) a 100 (direita) (do dono).</summary>
        public readonly NetworkVariable<sbyte> Inclinacao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>A camada da cabeça que leva tiro na proteção (só o tiro dos NPCs a vê; não colide com nada).</summary>
        public const int CamadaDaCabeca = 16;

        const float InclinacaoMaxima = 34f;
        const float AlturaEmPe = 1.8f, AlturaAgachado = 1.4f, AlturaNaBaixa = 1.2f;

        float _inclinaSuave;
        Transform _ossoColuna;
        SphereCollider _cabecaAcertavel;
        static bool _camadaPronta;

        /// <summary>Em todas as máquinas (do LateUpdate, depois do olhar): a coluna e o peito inclinados.</summary>
        void PoseDaProtecao()
        {
            float alvo = Inclinacao.Value / 100f;
            _inclinaSuave = Mathf.MoveTowards(_inclinaSuave, alvo, Time.deltaTime / 0.2f);
            if (Mathf.Abs(_inclinaSuave) > 0.001f && ossoPeito != null)
            {
                if (_ossoColuna == null || !_ossoColuna.IsChildOf(transform))
                {
                    _ossoColuna = null;
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                        if (t.name == "spine" && ossoPeito.IsChildOf(t)) { _ossoColuna = t; break; }
                }
                // (para a direita, o giro negativo em volta da frente)
                float graus = -_inclinaSuave * InclinacaoMaxima;
                var frente = transform.forward;
                if (_ossoColuna != null) _ossoColuna.rotation = Quaternion.AngleAxis(graus * 0.45f, frente) * _ossoColuna.rotation;
                ossoPeito.rotation = Quaternion.AngleAxis(graus * (_ossoColuna != null ? 0.55f : 1f), frente) * ossoPeito.rotation;
            }
            CabecaAcertavel();
        }

        /// <summary>
        /// No anfitrião, a cada quadro: a cápsula dos outros jogadores acompanha o agachado (o dono cuida da dele no
        /// MovimentoPrimeiraPessoa); o tiro dos NPCs pega o que está atrás da proteção baixa como deve.
        /// </summary>
        void CorpoParaOTiro()
        {
            if (!IsServer || IsOwner || _corpoFisico == null) return;
            float altura = SentadoNoChao.Value ? 1.0f : Agachado.Value ? (NaProtecao.Value == 2 ? AlturaNaBaixa : AlturaAgachado) : AlturaEmPe;
            if (Mathf.Abs(_corpoFisico.height - altura) < 0.01f) return;
            _corpoFisico.height = altura;
            _corpoFisico.center = new Vector3(_corpoFisico.center.x, altura * 0.5f, _corpoFisico.center.z);
        }

        // no anfitrião, na proteção: a esfera da cabeça (segue o osso, depois da inclinação)
        void CabecaAcertavel()
        {
            if (!IsServer) return;
            bool querer = NaProtecao.Value != 0 && ossoCabeca != null && !Morto;
            if (_cabecaAcertavel == null)
            {
                if (!querer) return;
                if (!_camadaPronta)
                {
                    for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(CamadaDaCabeca, i, true);
                    _camadaPronta = true;
                }
                var go = new GameObject("Cabeça que leva tiro") { layer = CamadaDaCabeca };
                go.transform.SetParent(transform, false);
                _cabecaAcertavel = go.AddComponent<SphereCollider>();
                _cabecaAcertavel.radius = 0.14f;
            }
            if (_cabecaAcertavel.enabled != querer) _cabecaAcertavel.enabled = querer;
            if (querer) _cabecaAcertavel.transform.position = ossoCabeca.position + Vector3.up * 0.06f;
        }

        bool Morto => Vida != null && Vida.Vida.Value <= 0f;

        /// <summary>
        /// No anfitrião (a visão e a mira dos NPCs): o ponto do corpo que se vê de 'de' (o peito, mais baixo agachado; atrás da
        /// proteção, a cabeça, se só ela está à vista). 'visto': algum dos dois está.
        /// </summary>
        public Vector3 PontoParaOTiro(Vector3 de, int mascara, out bool visto)
        {
            var peito = transform.position + Vector3.up * (Agachado.Value ? 0.95f : 1.3f);
            if (!Physics.Linecast(de, peito, mascara, QueryTriggerInteraction.Ignore))
            {
                visto = true;
                return peito;
            }
            var cabeca = ossoCabeca != null ? ossoCabeca.position + Vector3.up * 0.06f : transform.position + Vector3.up * (Agachado.Value ? 1.2f : 1.62f);
            // (não acima da cápsula: agachado atrás da baixa, ela desce mais que a cabeça da animação)
            if (_corpoFisico != null) cabeca.y = Mathf.Min(cabeca.y, transform.position.y + _corpoFisico.height - 0.08f);
            visto = !Physics.Linecast(de, cabeca, mascara, QueryTriggerInteraction.Ignore);
            return visto ? cabeca : peito;
        }
    }
}
