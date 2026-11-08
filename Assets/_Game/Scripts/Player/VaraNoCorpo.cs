using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A vara de pesca nova no corpo (07/11/2026, a pedido: "hoje ele segura a vara de uma forma bem irregular que não tem
    /// lógica, deveria segurar a carretilha com a mão esquerda e a base da vara de pesca com a mão direita"), em todas as
    /// máquinas (posto pelo <see cref="PescaVista"/>):
    /// <list type="bullet">
    /// <item>a vara com a carretilha (<c>Resources/VaraDoCorpo</c>, do <c>Art/Pesca/Vara.fbx</c>, o
    /// <c>Blender/vara_animacoes.py</c>) presa à mão direita (hand.R), no lugar da lata com a vara de antes;</item>
    /// <item>a camada dos braços do corpo nos clipes dela (do <c>Personagem_PrincipalBloco_Pesca.fbx</c>; a
    /// <c>MontagemDaVara</c> põe os estados no controlador do corpo): segurando (Vara_Segurar), lançando (Vara_Lancar),
    /// recolhendo a linha (Vara_Recolher) e brigando com o peixe (Vara_Puxar). A direita no cabo, a esquerda na
    /// carretilha; as pernas continuam andando (a camada é do peito para cima);</item>
    /// <item>a manivela gira junto com a mão esquerda (uma volta por laço no Vara_Recolher, duas no Vara_Puxar).</item>
    /// </list>
    /// O que ele faz vem da pesca: no dono, do <see cref="PescaJogador"/> (com o carregar da força: a vara vai para trás
    /// enquanto ele segura o botão); nos outros, da boia na rede (<see cref="InventarioJogador.BoiaNaRede"/>: o lance
    /// começa na soltura, junto com a boia). Sem o prefab ou sem os estados no controlador, fica a vara antiga (no lugar
    /// da lata, o <see cref="VaraNaMao"/>).
    /// </summary>
    [DefaultExecutionOrder(120)]   // depois do JogadorRede (o olhar) e de quem senta (o carro, o caiaque)
    public class VaraNoCorpo : MonoBehaviour
    {
        /// <summary>
        /// O lance (s, o clipe Lancar, nos braços e no corpo): até onde a vara vai para trás (o carregar da força para aí),
        /// a soltura (a boia sai) e o fim.
        /// </summary>
        public const float LancarPreparo = 0.35f, LancarSoltura = 0.5f, LancarDuracao = 0.9f;
        /// <summary>O eixo de giro da manivela, no espaço da vara (o pai dela no Vara.fbx): de lado, o eixo da carretilha.</summary>
        static readonly Vector3 EixoDaManivelaNaVara = Vector3.right;
        /// <summary>Depois de recolher a linha (a boia volta), quanto tempo (s) a mão esquerda ainda gira a manivela.</summary>
        const float RecolherDepois = 0.9f;

        public const string Recurso = "VaraDoCorpo";
        const int CamadaDosBracos = 1;
        static readonly int SegurarId = Animator.StringToHash("Vara_Segurar");
        static readonly int LancarId = Animator.StringToHash("Vara_Lancar");
        static readonly int RecolherId = Animator.StringToHash("Vara_Recolher");
        static readonly int PuxarId = Animator.StringToHash("Vara_Puxar");
        static readonly int SegurarItemId = Animator.StringToHash("Segurar");

        JogadorRede _rede;
        InventarioJogador _inventario;
        PescaJogador _pesca;
        GameObject _vara;
        Transform _manivela, _ponta, _mao;
        Quaternion _manivelaParada;
        Vector3 _eixoDaManivela = Vector3.right;
        Renderer[] _desenho;
        Animator _animadorVisto, _animadorDaMao;
        RuntimeAnimatorController _controladorVisto;
        bool _temEstados, _naVara, _desenhoVisivel = true, _desenhoAplicado;
        int _estado;
        float _giro, _recolherAte = -1f;
        PescaJogador.Fase _faseAntes;

        static bool _procurou;
        static GameObject _prefab;

        /// <summary>O prefab da vara do corpo (Resources), ou null (a vara antiga, no lugar da lata).</summary>
        public static GameObject Prefab
        {
            get
            {
                if (!_procurou)
                {
                    _procurou = true;
                    _prefab = Resources.Load<GameObject>(Recurso);
                }
                return _prefab;
            }
        }

        /// <summary>A vara nova está no corpo deste jogador (à mostra ou não): a lata com a vara antiga sai.</summary>
        public bool Ligada => _naVara;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            _pesca = GetComponent<PescaJogador>();
        }

        void OnDestroy()
        {
            if (_vara != null) Destroy(_vara);
        }

        /// <summary>A ponta da vara no corpo (onde a linha sai), com ela à mostra no corpo.</summary>
        public bool Ponta(out Vector3 ponta)
        {
            ponta = default;
            if (!_naVara || _vara == null || !_vara.activeInHierarchy) return false;
            ponta = _ponta != null ? _ponta.position : _vara.transform.TransformPoint(new Vector3(0f, 0f, 1.6f));
            return true;
        }

        void LateUpdate()
        {
            if (_rede == null || _inventario == null || !_rede.IsSpawned) return;
            var animador = _rede.animadorCorpo;
            bool comVara = _inventario.NaMao == Item.VaraDePesca;
            bool pode = comVara && Prefab != null && TemOsEstados(animador) && Mao(animador) != null;
            if (pode != _naVara)
            {
                _naVara = pode;
                // a lata com a vara antiga sai (ou volta) do corpo
                AplicarNaLata();
                if (!pode && animador != null && animador.isInitialized && animador.layerCount > CamadaDosBracos)
                    animador.CrossFadeInFixedTime(SegurarItemId, 0.15f, CamadaDosBracos);
                _estado = 0;
            }
            if (!pode)
            {
                if (_vara != null && _vara.activeSelf) _vara.SetActive(false);
                return;
            }
            Criar();
            bool mostrar = !_rede.NaMaoEscondida;
            if (_vara.activeSelf != mostrar) _vara.SetActive(mostrar);
            if (!mostrar) return;
            // na mão direita, depois da animação: a raiz do prefab vai no lugar e no giro da mão (sem a escala dos ossos),
            // e o modelo dentro dela já está na pegada (a MontagemDaVara mediu o osso "vara" dos clipes do corpo, parado no
            // espaço da mão, em metros)
            _vara.transform.SetPositionAndRotation(_mao.position, _mao.rotation);
            Desenho();
            Animar(animador);
        }

        // ------------------------------------------------------------------ o que ele faz

        void Animar(Animator animador)
        {
            var fase = FaseAgora(out float carga);
            // a linha voltou (de qualquer fase): a mão gira a manivela mais um pouco
            if (fase == PescaJogador.Fase.Parado && _faseAntes != PescaJogador.Fase.Parado && _faseAntes != PescaJogador.Fase.Carregando)
                _recolherAte = Time.time + RecolherDepois;
            bool soltou = fase == PescaJogador.Fase.Voando && _faseAntes != PescaJogador.Fase.Voando;
            var antes = _faseAntes;
            _faseAntes = fase;
            if (!animador.isInitialized) return;
            switch (fase)
            {
                case PescaJogador.Fase.Carregando:
                    // segurando o botão: a vara vai para trás, por cima do ombro, com a força (e para lá); o clipe
                    // começa na pose de segurar, então o corte não aparece
                    _estado = LancarId;
                    animador.Play(LancarId, CamadaDosBracos, Mathf.Clamp01(carga) * (LancarPreparo / LancarDuracao));
                    break;
                case PescaJogador.Fase.Voando:
                    // o lance: quem carregou (o dono) segue de onde a vara estava; os outros entram na soltura
                    if (soltou)
                    {
                        if (antes == PescaJogador.Fase.Carregando && _estado == LancarId)
                            animador.Play(LancarId, CamadaDosBracos, LancarPreparo / LancarDuracao);
                        else
                        {
                            animador.CrossFadeInFixedTime(LancarId, 0.08f, CamadaDosBracos, LancarSoltura - 0.12f);
                            _estado = LancarId;
                        }
                    }
                    break;
                case PescaJogador.Fase.Puxando:
                    Ir(animador, PuxarId, 0.2f);
                    break;
                default:
                    // esperando, beliscando, mordendo ou parado: segurando (o lance termina antes)
                    if (_estado == LancarId && EstadoAgora(animador) == LancarId && Normalizado(animador) < 0.98f) break;
                    Ir(animador, Time.time < _recolherAte ? RecolherId : SegurarId, 0.2f);
                    break;
            }
            // a manivela gira junto com a mão esquerda: no Vara_Recolher, uma volta por laço; no Vara_Puxar, duas (como o osso
            // "manivela" dos clipes, que o personagem não tem); no segurar e no lançar, parada
            _giro = GiroDaManivela(animador);
            if (_manivela != null) _manivela.localRotation = _manivelaParada * Quaternion.AngleAxis(_giro, _eixoDaManivela);
        }

        // o ângulo da manivela pelo estado da camada dos braços (o que está entrando, se ele gira a manivela)
        static float GiroDaManivela(Animator a)
        {
            var agora = a.GetCurrentAnimatorStateInfo(CamadaDosBracos);
            if (a.IsInTransition(CamadaDosBracos))
            {
                var proximo = a.GetNextAnimatorStateInfo(CamadaDosBracos);
                if (VoltasNoLaco(proximo.shortNameHash) > 0f) agora = proximo;
            }
            return VoltasNoLaco(agora.shortNameHash) * 360f * Mathf.Repeat(agora.normalizedTime, 1f);
        }

        static float VoltasNoLaco(int estado) => estado == RecolherId ? 1f : estado == PuxarId ? 2f : 0f;

        void Ir(Animator animador, int estado, float duracao)
        {
            _estado = estado;
            if (EstadoAgora(animador) == estado) return;   // (já está nele, ou indo)
            animador.CrossFadeInFixedTime(estado, duracao, CamadaDosBracos);
        }

        static int EstadoAgora(Animator a)
        {
            var s = a.IsInTransition(CamadaDosBracos) ? a.GetNextAnimatorStateInfo(CamadaDosBracos) : a.GetCurrentAnimatorStateInfo(CamadaDosBracos);
            return s.shortNameHash;
        }

        static float Normalizado(Animator a)
        {
            var s = a.IsInTransition(CamadaDosBracos) ? a.GetNextAnimatorStateInfo(CamadaDosBracos) : a.GetCurrentAnimatorStateInfo(CamadaDosBracos);
            return s.normalizedTime;
        }

        // a fase da pesca: no dono, a dele (com o carregar); nos outros, a da rede
        PescaJogador.Fase FaseAgora(out float carga)
        {
            carga = 0f;
            if (_rede.IsOwner && _pesca != null)
            {
                carga = _pesca.Carga;
                return _pesca.FaseAgora;
            }
            return (PescaJogador.Fase)Mathf.RoundToInt(_inventario.BoiaNaRede.Value.w);
        }

        // ------------------------------------------------------------------ as peças

        void Criar()
        {
            if (_vara != null) return;
            _vara = Instantiate(Prefab, transform, false);   // (no jogador: some com ele)
            _vara.name = "Vara de pesca (corpo)";
            _manivela = _ponta = null;
            _giro = 0f;
            int camada = _rede.animadorCorpo != null ? _rede.animadorCorpo.gameObject.layer : gameObject.layer;
            foreach (var t in _vara.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = camada;
                if (t.name == "Manivela") _manivela = t;
                else if (t.name == "Ponta") _ponta = t;
            }
            foreach (var c in _vara.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // o eixo da manivela: o da carretilha, de lado na vara (no espaço do pai dela, a vara), no espaço da manivela
            if (_manivela != null)
            {
                _manivelaParada = _manivela.localRotation;
                _eixoDaManivela = Quaternion.Inverse(_manivelaParada) * EixoDaManivelaNaVara;
            }
            _desenho = _vara.GetComponentsInChildren<Renderer>(true);
            _desenhoAplicado = false;
        }

        // como o que está na mão do corpo (JogadorRede.CorpoDoDono): no dono, na primeira pessoa, nem sombra (a vara
        // dos braços é a dele); na terceira pessoa, à mostra na camada do dono
        void Desenho()
        {
            bool visivel = !_rede.IsOwner || _rede.CorpoVisivelNoDono;
            if (_desenhoAplicado && visivel == _desenhoVisivel) return;
            _desenhoAplicado = true;
            _desenhoVisivel = visivel;
            foreach (var r in _desenho)
            {
                if (r == null) continue;
                r.enabled = visivel;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.renderingLayerMask = _rede.IsOwner ? JogadorRede.MascaraDoDono : 1u;
            }
        }

        // a lata com a vara de antes, no corpo: some com a vara nova (e volta sem ela)
        void AplicarNaLata()
        {
            if (_rede.naMaoCorpo == null) return;
            int m = Itens.MaoDe(Item.VaraDePesca);
            if (m >= 0 && m < _rede.naMaoCorpo.Length && _rede.naMaoCorpo[m] != null)
                ItemNaMaoProprio.Aplicar(_rede.naMaoCorpo[m], _inventario.NaMao);
        }

        Transform Mao(Animator animador)
        {
            if (animador == null) return null;
            if (animador == _animadorDaMao && _mao != null) return _mao;
            _animadorDaMao = animador;
            _mao = null;
            foreach (var t in animador.GetComponentsInChildren<Transform>(true))
                if (t.name == "hand.R")
                {
                    _mao = t;
                    break;
                }
            return _mao;
        }

        // o controlador do corpo tem os estados da vara (a MontagemDaVara)? Lido de novo quando ele muda
        bool TemOsEstados(Animator a)
        {
            if (a == null || !a.isInitialized || a.layerCount <= CamadaDosBracos) return false;
            if (a == _animadorVisto && a.runtimeAnimatorController == _controladorVisto) return _temEstados;
            _animadorVisto = a;
            _controladorVisto = a.runtimeAnimatorController;
            _temEstados = a.HasState(CamadaDosBracos, SegurarId) && a.HasState(CamadaDosBracos, LancarId)
                          && a.HasState(CamadaDosBracos, RecolherId) && a.HasState(CamadaDosBracos, PuxarId);
            return _temEstados;
        }
    }
}
