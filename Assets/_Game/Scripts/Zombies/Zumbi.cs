using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Um zumbi. No anfitrião ele pensa: vaga devagar, percebe os jogadores (visão que depende da luz, contato, passos
    /// correndo e tiros; nada passa pela porta fechada do bunker), persegue devagar, ataca de perto e desiste quando o
    /// alvo se afasta ou some de vista. Quem chama
    /// Pensar() é o ZombieManager, em fatias e no ritmo do nível de detalhe de cada um.
    /// Também leva tiro (só no anfitrião): a cabeça explode com um tiro e ele morre; no resto, o corpo perde vida (nos
    /// braços e nas pernas, menos). Morto, ele some e um corpo com física (CorpoDeZumbi) cai no lugar.
    /// Os membros também têm vida: acabou, o membro cai (desmembramento, veja <see cref="Membros"/>). Sem uma perna ele
    /// manca; sem as duas, cai e passa a rastejar.
    /// Nos outros jogadores o zumbi é uma cópia: segue as posições que chegam pela rede, com interpolação, e mostra o
    /// estado recebido (morte, golpes e reações).
    /// Nos zumbis leves, a aparência (cores, sangue, sujeira e machucados) sai do Id e da geração (<see cref="Aparencia"/>),
    /// igual em todas as máquinas, e o rosto muda de expressão com o estado.
    /// Não tem Update próprio (com centenas de zumbis, o ZombieManager faz um laço só).
    /// Os Ocos são fotossensíveis (Zumbi.Sol.cs): no sol a pedra sobe e ele vira estátua até a noite.
    /// </summary>
    public partial class Zumbi : MonoBehaviour
    {
        // Fugindo: no sol, desesperado, procurando sombra; Petrificado: de pedra, parado até a noite; Abrigando: ao
        // amanhecer, indo para um abrigo do sol (Zumbi.Sol.cs); Esquivando: pulando para fora do facho de uma lanterna ou
        // de um farol (Zumbi.Luz.cs). O estado vai pela rede em 4 bits (até 15).
        public enum Estado : byte { Parado, Vagando, Investigando, Procurando, Perseguindo, Atacando, Morto, Fugindo, Petrificado, Abrigando, Esquivando }
        public enum Nivel : byte { Perto, Medio, Longe }

        public const int CamadaZumbi = 7;
        // No nível Perto a cápsula só segura o jogador (Ignore Raycast): o tiro passa por ela e acerta as hitboxes.
        const int CamadaSoBloqueio = 2;

        [Header("Peças (a montagem preenche)")]
        [Tooltip("Qual dos modelos (a posição dele na lista do ZombieManager e o pool dos corpos dele).")]
        public int modelo;
        public Animator animador;
        public NavMeshAgent agente;
        public CapsuleCollider capsula;
        public Hitbox[] hitboxes;
        public Renderer[] renderers;
        public AudioSource voz;
        [Tooltip("O modelo (a raiz do FBX): o corpo nasce na mesma pose.")]
        public Transform raizDoModelo;
        [Tooltip("Todos os ossos, na mesma ordem do esqueleto do corpo.")]
        public Transform[] esqueleto;
        [Tooltip("Os ossos das partes do corpo com física, na ordem de CorpoDeZumbi.Partes.")]
        public Transform[] partes;
        [Tooltip("A malha inteira (a base dos cortes) e os materiais da pele e da carne (as tampas dos cortes).")]
        public Mesh malhaInteira;
        public Material materialPele, materialCarne;
        [Tooltip("As malhas mais simples para longe (LOD1 e LOD2, nos mesmos ossos), dos zumbis em blocos; vazio = só a inteira.")]
        public Mesh[] malhasDeLonge;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AtacarId = Animator.StringToHash("Atacar");
        static readonly int TiroId = Animator.StringToHash("Tiro");
        static readonly int PernasId = Animator.StringToHash("Pernas");
        const int CamadaAcoes = 1;

        public int Id { get; private set; }
        /// <summary>Muda cada vez que o Id volta do pool: a cópia sabe que é outro zumbi.</summary>
        public byte Geracao { get; private set; }
        public bool Copia { get; private set; }
        public Estado Atual { get; private set; }
        /// <summary>Nível de detalhe aplicado: animação, hitboxes, sombra e (no anfitrião) navegação.</summary>
        public Nivel NivelAtual { get; private set; } = Nivel.Longe;
        /// <summary>Conta os golpes. Vai pela rede: a cópia toca o ataque quando o número muda.</summary>
        public byte Golpes { get; private set; }
        /// <summary>Conta os tiros que ele aguentou. Vai pela rede: a cópia toca a reação quando muda.</summary>
        public byte Reacoes { get; private set; }
        public float Velocidade { get; private set; }
        public bool Morto => Atual == Estado.Morto;
        /// <summary>Morreu com a cabeça explodida.</summary>
        public bool SemCabeca { get; private set; }
        /// <summary>Os membros perdidos, 2 bits por membro (veja <see cref="Membros"/>). Vai pela rede.</summary>
        public byte Cortes { get; private set; }
        /// <summary>Como ele anda: 0 normal, 1 mancando sem a perna esquerda, 2 sem a direita, 3 caindo, 4 rastejando.</summary>
        public int Pernas => _pernas;
        /// <summary>
        /// Como ele morreu, num byte (vai pela rede): bit 0 sem cabeça, bits 1 a 4 a parte do corpo que levou o tiro e
        /// bit 5 arremessado (a espingarda de perto). A direção do empurrão vai no lugar da velocidade (morto não anda).
        /// </summary>
        internal byte InfoDaMorte => (byte)((SemCabeca ? 1 : 0) | (_parteDaMorte << 1) | (_arremessado ? 32 : 0));
        internal float DirecaoDaMorte { get; private set; }
        /// <summary>Perseguindo, atacando ou levou tiro há pouco: nunca cai de nível.</summary>
        public bool Engajado => Atual == Estado.Perseguindo || Atual == Estado.Atacando || (Atual == Estado.Esquivando && _alvo != null) || Time.time < _engajadoAte;
        internal float mortoEm;

        ZombieManager _m;
        bool _visivel = true;
        float _velAnimacao;
        int _pernas;
        float _rastejaEm, _velocidadeBase = -1f, _alturaDaCapsula = -1f, _alturaDoAgente;
        Material[] _soPele, _peleECarne;
        int _parteDaMorte;
        bool _arremessado;
        // os zumbis leves: a cópia do material (cores, camadas e rosto deste zumbi) e a expressão mostrada
        Material _material;
        Aparencia.Variacao _variacao;
        Aparencia.Expressao _expressao;
        float _dorAte, _ataqueAte;

        /// <summary>O material da pele deste zumbi (a cópia dele, nos zumbis leves): o corpo que cai e os pedaços copiam.</summary>
        internal Material MaterialDaPele => _material != null ? _material : materialPele;

        // ------------------------------------------------------------------ comum

        internal void AplicarNivel(Nivel n, bool forcar = false)
        {
            if (n == NivelAtual && !forcar) return;
            NivelAtual = n;
            bool perto = n == Nivel.Perto;
            AtualizarAnimador();
            animador.cullingMode = perto ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
            capsula.gameObject.layer = perto ? CamadaSoBloqueio : CamadaZumbi;
            AtualizarColisores();
            // na cena de vídeo, todos fazem sombra: vistos de longe, sem ela, eles pareciam flutuar
            var sombra = perto || ModoVideo.Ligado ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var r in renderers) r.shadowCastingMode = sombra;
            AtualizarRenderers();
        }

        /// <summary>
        /// Além da distância de visão de quem joga nesta máquina (UI/Graficos), nem desenha. Dentro dela, o de longe (o
        /// virtual, sem andar) também aparece, parado e respirando.
        /// </summary>
        internal void DefinirVisivel(bool visivel)
        {
            if (visivel == _visivel) return;
            _visivel = visivel;
            AtualizarRenderers();
            if (NivelAtual == Nivel.Longe) AtualizarAnimador();
        }

        // a animação: perto e médio, sempre; o de longe, só se está sendo desenhado, e parado (o virtual não anda: com a
        // velocidade de antes, ele "andaria no lugar"); a estátua e o morto, nunca
        void AtualizarAnimador()
        {
            bool longe = NivelAtual == Nivel.Longe;
            animador.enabled = (!longe || _visivel) && !Morto && !Petrificado;
            if (!animador.enabled) return;
            AplicarPernasNoAnimador();
            if (longe)
            {
                _velAnimacao = 0f;
                animador.SetFloat(SpeedId, 0f);
            }
        }

        /// <summary>O LOD do desenho (0 perto, 1 e 2 mais longe), pela distância de quem joga nesta máquina.</summary>
        public int Detalhe { get; private set; }

        /// <summary>
        /// A malha pela distância até quem joga aqui (com histerese de 2 m): a inteira perto e as mais simples longe, já
        /// com os membros perdidos. O pedaço que cai sai sempre da inteira (só se vê de perto).
        /// </summary>
        internal void DefinirDetalhe(float distancia, float ate0, float ate1)
        {
            if (malhasDeLonge == null || malhasDeLonge.Length == 0) return;
            // para chegar mais perto, 2 m a menos (não fica trocando na divisa)
            int d = distancia < ate0 - (Detalhe > 0 ? 2f : 0f) ? 0 : distancia < ate1 - (Detalhe > 1 ? 2f : 0f) ? 1 : 2;
            d = Mathf.Min(d, malhasDeLonge.Length);
            if (d == Detalhe) return;
            Detalhe = d;
            if (renderers.Length > 0 && renderers[0] is SkinnedMeshRenderer pele) pele.sharedMesh = Membros.Variante(MalhaDoDetalhe, esqueleto, Cortes);
        }

        Mesh MalhaDoDetalhe => Detalhe == 0 || malhasDeLonge == null || Detalhe > malhasDeLonge.Length || malhasDeLonge[Detalhe - 1] == null
            ? malhaInteira : malhasDeLonge[Detalhe - 1];

        // morto, o zumbi some: quem aparece é o corpo com física. O de longe também é desenhado (até a distância de visão)
        void AtualizarRenderers()
        {
            bool ligado = _visivel && !Morto;
            foreach (var r in renderers) r.enabled = ligado;
        }

        // hitboxes só no nível Perto; morto, nada leva tiro; um membro que caiu não leva mais
        void AtualizarColisores()
        {
            bool perto = NivelAtual == Nivel.Perto;
            foreach (var h in hitboxes) h.colisor.enabled = perto && !Morto && !Membros.Perdeu(Cortes, h.membro);
            capsula.enabled = NivelAtual != Nivel.Longe && !Morto;
        }

        void Animar(float dt)
        {
            float alvo = Atual == Estado.Atacando || Morto ? 0f : Velocidade;
            _velAnimacao = Mathf.Lerp(_velAnimacao, alvo, 1f - Mathf.Exp(-8f * dt));
            if (animador.enabled) animador.SetFloat(SpeedId, _velAnimacao);
        }

        // o golpe: a animação de ataque (só o tronco; caído, não: o tronco de pé por cima do rastejar ficava torto)
        void MostrarGolpe()
        {
            _ataqueAte = Time.time + 0.9f;
            if (animador.isActiveAndEnabled && _pernas < 3 && !Petrificado) animador.SetTrigger(AtacarId);
            var s = Sons;
            if (s != null) Falar(s.golpe, Fala.Golpe, 1f, 1f, false);   // o rosnado do golpe (Zumbi.Voz.cs)
        }

        void MostrarReacao()
        {
            _dorAte = Time.time + 0.6f;
            if (animador.isActiveAndEnabled && _pernas < 3 && !Petrificado) animador.SetTrigger(TiroId);
            SomDaReacao();
        }

        // ------------------------------------------------------------------ aparência (zumbis leves)

        /// <summary>
        /// Os materiais da pele (só ela, e ela com a carne das tampas dos cortes). Nos zumbis leves, a pele é uma cópia
        /// do material, deste zumbi: as cores, as camadas e o rosto são dele.
        /// </summary>
        void GarantirMateriais()
        {
            if (_soPele != null) return;
            var pele = materialPele;
            if (Aparencia.Leve(materialPele))
            {
                _material = new Material(materialPele) { name = materialPele.name + " (zumbi)" };
                pele = _material;
            }
            _soPele = new[] { pele };
            _peleECarne = new[] { pele, materialCarne };
            if (Cortes == 0 && renderers.Length > 0 && renderers[0] is SkinnedMeshRenderer smr) smr.sharedMaterials = _soPele;
        }

        /// <summary>Nasceu (ou voltou do pool) com outro Id: a aparência sorteada por ele, igual em todas as máquinas.</summary>
        void AplicarAparencia()
        {
            GarantirMateriais();
            if (_material == null) return;
            _variacao = Aparencia.Sortear(Id, Geracao);
            _expressao = ExpressaoAgora();
            Aparencia.Aplicar(_material, _variacao, _expressao);
        }

        Aparencia.Expressao ExpressaoAgora()
        {
            if (Morto) return Aparencia.Expressao.Morto;
            float t = Time.time;
            if (t < _dorAte) return Aparencia.Expressao.Dor;
            if (Atual == Estado.Atacando || t < _ataqueAte) return Aparencia.Expressao.Ataque;
            switch (Atual)
            {
                case Estado.Perseguindo: return Aparencia.Expressao.Furia;
                case Estado.Investigando:
                case Estado.Procurando: return Aparencia.Expressao.Alerta;
                case Estado.Parado:
                    // desconfiado (no anfitrião), ele abre os olhos; senão, alguns "dormem" em pé
                    return _alerta > 0.05f ? Aparencia.Expressao.Alerta : _variacao.dorme ? Aparencia.Expressao.Parado : Aparencia.Expressao.Vagando;
                default: return Aparencia.Expressao.Vagando;
            }
        }

        /// <summary>O rosto acompanha o estado (a cada quadro, nos níveis Perto e Médio; só troca o material quando muda).</summary>
        void AtualizarRosto()
        {
            if (_material == null) return;
            var e = ExpressaoAgora();
            if (e == _expressao) return;
            _expressao = e;
            _material.SetVector(Aparencia.QuadrosId, Aparencia.QuadrosDe(e, _variacao));
        }

        // ------------------------------------------------------------------ desmembramento

        /// <summary>
        /// Os membros mudaram (no anfitrião pelo tiro, nas cópias pela rede): a malha cortada, as hitboxes e o jeito de
        /// andar. Com efeito, cada membro que caiu agora solta o pedaço (voando para trás) e o toco jorra sangue.
        /// </summary>
        internal void MudarCortes(byte novos, bool comEfeito, Vector3 direcao)
        {
            byte antes = Cortes;
            if (novos == antes) return;
            Cortes = novos;
            var pele = renderers[0] as SkinnedMeshRenderer;
            GarantirMateriais();
            if (comEfeito && _m != null && !Morto)
            {
                Vector3 velocidade = Copia ? transform.forward * Velocidade : agente.enabled ? agente.velocity : Vector3.zero;
                if (direcao.sqrMagnitude < 0.01f) direcao = -transform.forward;
                for (int l = 0; l < Membros.Quantos; l++)
                {
                    int velho = Membros.Estado(antes, l), novo = Membros.Estado(novos, l);
                    if (novo <= velho) continue;
                    int indice = Membros.IndiceDoOsso(esqueleto, Membros.OssoDoCorte(l, novo));
                    if (indice < 0) continue;
                    var osso = esqueleto[indice];
                    var malha = Membros.Pedaco(malhaInteira, esqueleto, Membros.ComEstado(antes, l, velho), Membros.ComEstado(antes, l, novo), indice);
                    _m.SoltarPedaco(malha, _peleECarne, osso, direcao, velocidade);
                    int seguinte = Membros.IndiceDoOsso(esqueleto, Membros.OssoDepoisDoCorte(l, novo));
                    Vector3 rumo = seguinte >= 0 ? esqueleto[seguinte].position - osso.position : Vector3.down;
                    Sangue.Jorro(osso, rumo, 2.5f);
                    Sangue.Respingo(osso.position, rumo.normalized, 28);
                    Sangue.Mancha(osso.position, 0.7f);
                }
            }
            if (pele != null)
            {
                pele.sharedMesh = Membros.Variante(MalhaDoDetalhe, esqueleto, novos);
                pele.sharedMaterials = novos == 0 ? _soPele : _peleECarne;
            }
            AtualizarColisores();
            // as pernas: sem uma, manca; sem as duas, cai (com efeito) e depois rasteja
            bool semE = Membros.SemPerna(novos, 0), semD = Membros.SemPerna(novos, 1);
            int pernas = semE && semD ? (comEfeito && _pernas < 3 ? 3 : _pernas == 3 ? 3 : 4) : semE ? 1 : semD ? 2 : 0;
            if (pernas == 3 && _pernas != 3) _rastejaEm = Time.time + 0.8f;
            DefinirPernas(pernas);
        }

        float FatorDasPernas => _pernas == 0 ? 1f : _pernas == 3 ? 0f : _pernas == 4 ? _m.ajustes.fatorRastejando : _m.ajustes.fatorMancando;

        /// <summary>
        /// No anfitrião: nasce já rastejando, sem as duas pernas do joelho para baixo (o rastejante embaixo do carro,
        /// ZombieManager.Povoamento.cs). Sem efeito: nada voa nem jorra; as cópias recebem os cortes pela rede.
        /// </summary>
        internal void VirarRastejante()
        {
            byte cortes = Membros.ComEstado(Membros.ComEstado(Cortes, Membros.PernaE, 1), Membros.PernaD, 1);
            MudarCortes(cortes, false, Vector3.zero);
        }

        void DefinirPernas(int p)
        {
            _pernas = p;
            if (_alturaDaCapsula < 0f)
            {
                _alturaDaCapsula = capsula.height;
                _alturaDoAgente = agente.height;
            }
            // no chão: a cápsula e o agente baixos (o jogador passa por cima e o tiro de cima pega)
            float altura = p >= 3 ? 0.7f : _alturaDaCapsula;
            capsula.height = altura;
            capsula.center = new Vector3(0f, altura / 2f, 0f);
            agente.height = p >= 3 ? 0.7f : _alturaDoAgente;
            if (_velocidadeBase >= 0f && agente.enabled && agente.isOnNavMesh) agente.speed = _velocidadeBase * FatorDasPernas * FatorDaPedra * _ritmo;
            AplicarPernasNoAnimador();
        }

        void AplicarPernasNoAnimador()
        {
            if (!animador.isActiveAndEnabled) return;
            animador.SetInteger(PernasId, _pernas);
            animador.SetLayerWeight(CamadaAcoes, _pernas >= 3 ? 0f : 1f);
        }

        // caindo: quando a queda acaba, passa a rastejar
        void PassarDoTempoDasPernas()
        {
            if (_rastejaEm > 0f && Time.time >= _rastejaEm)
            {
                _rastejaEm = 0f;
                DefinirPernas(4);
            }
        }

        /// <summary>
        /// A morte na tela: o zumbi some e, com efeito, o corpo com física cai no lugar dele (com o empurrão do tiro), e a
        /// cabeça explode se foi um tiro nela. Sem efeito (a cópia apareceu já morta), ele só some.
        /// </summary>
        void MostrarMorte(bool comEfeito)
        {
            if (comEfeito && _m != null)
            {
                Vector3 direcao = Quaternion.Euler(0f, DirecaoDaMorte, 0f) * Vector3.forward;
                Vector3 velocidade = Copia ? transform.forward * Velocidade : agente.enabled ? agente.velocity : Vector3.zero;
                _m.Cair(this, velocidade, SemCabeca, _parteDaMorte, direcao, _arremessado, Cortes);
                if (Pedra >= 0.5f) Quebrar();
                else
                {
                    // o último grito (a voz para logo abaixo: o som vai solto, no lugar)
                    var sons = Sons;
                    if (sons != null && sons.tiro != null && sons.tiro.Length > 0 && _m.PodeOuvir(transform.position))
                        Efeitos.Som(sons.tiro, transform.position + Vector3.up * 1.4f, 0.9f * sons.volume * ZombieManager.VolumeDosZumbis, true, 30f);
                    if (SemCabeca) Sangue.Explosao(partes[CorpoDeZumbi.ParteCabeca].position);
                    Sangue.Mancha(transform.position + direcao * 0.6f, 1.1f);
                }
            }
            AtualizarColisores();
            AtualizarRenderers();
            if (animador.enabled) animador.enabled = false;
            if (voz != null) voz.Stop();
        }

        internal void Gemer(float tom)
        {
            // o Oco tem a voz própria (Zumbi.Voz.cs); sem ela, a voz antiga, em trechos
            var sons = Sons;
            if (sons != null && sons.voz != null && sons.voz.Length > 0)
            {
                if (voz != null && !voz.isPlaying) Falar(sons.voz, Fala.Voz, 1f, tom);
                return;
            }
            // de pedra (ou com a pedra quase cheia), calado
            if (Morto || Petrificado || Pedra >= 0.9f || voz == null || voz.isPlaying || _m == null || _m.vozDoZumbi == null || !_m.PodeGemer(transform.position)) return;
            var trecho = _m.TrechoDaVoz();
            voz.clip = _m.vozDoZumbi;
            if (_volumeDaVoz < 0f) _volumeDaVoz = voz.volume;
            voz.volume = _volumeDaVoz * ZombieManager.VolumeDosZumbis;
            _volumeDaFala = -1f;   // o volume fica o da fonte (só o filtro abafa)
            voz.pitch = tom * Random.Range(0.85f, 1.05f);
            voz.Play();
            voz.time = trecho.x;
            voz.SetScheduledEndTime(AudioSettings.dspTime + trecho.y / voz.pitch);
        }

        /// <summary>Volta ao estado de fábrica, para o pool: inteiro, vivo, de pé, sem alvo.</summary>
        internal void Zerar()
        {
            _alvo = null; _alerta = 0f; _girando = false; _engajadoAte = 0f; _golpeEm = 0f; _ameacadoAte = 0f; _feridoAte = 0f; _temCerco = false; _noAlcanceDesde = -1f; ZerarLuz();
            _danoNoCorpo = 0f; SemCabeca = false; _parteDaMorte = 0; DirecaoDaMorte = 0f; _arremessado = false;
            for (int i = 0; i < _vidaDosMembros.Length; i++) _vidaDosMembros[i] = _m != null ? _m.ajustes.vidaDoMembro : 3f;
            if (Cortes != 0)
            {
                Cortes = 0;
                var pele = renderers[0] as SkinnedMeshRenderer;
                GarantirMateriais();
                if (pele != null) { pele.sharedMesh = MalhaDoDetalhe; pele.sharedMaterials = _soPele; }
            }
            _rastejaEm = 0f;
            if (_pernas != 0) DefinirPernas(0);
            _dorAte = _ataqueAte = 0f;
            ZerarPedra();
            ZerarVoz();
            Golpes = 0; Reacoes = 0; Velocidade = 0f; _velAnimacao = 0f; _qtd = 0;
            Atual = Estado.Parado;
            if (agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            if (voz != null) voz.Stop();
            if (animador.isActiveAndEnabled) animador.Rebind();
        }

        // ------------------------------------------------------------------ anfitrião: a IA

        internal float proximaDecisao, ultimaDecisao;
        JogadorRede _alvo;
        Vector3 _casa, _ultimaPosicao, _olharPara;
        float _fimDaPausa, _alerta, _ultimoVisto, _proximoGolpe, _proximoCaminho, _engajadoAte, _preguica, _giro, _golpeEm;
        float _danoNoCorpo;
        readonly float[] _vidaDosMembros = { 3f, 3f, 3f, 3f };
        bool _girando;

        bool NoNavMesh => agente.enabled && agente.isOnNavMesh;
        const float AlturaDoOlho = 1.55f;

        internal void PrepararNoServidor(int id, byte geracao, Vector3 posicao, float yaw, ZombieManager m)
        {
            Id = id; Geracao = geracao; Copia = false; _m = m;
            for (int i = 0; i < _vidaDosMembros.Length; i++) _vidaDosMembros[i] = m.ajustes.vidaDoMembro;
            transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, yaw, 0f));
            _casa = posicao;
            _preguica = Random.value < 0.33f ? 3f : 1f;
            Atual = Estado.Parado;
            _fimDaPausa = Time.time + Random.Range(0f, 4f);
            proximaDecisao = ultimaDecisao = Time.time;
            _visivel = true;
            _longeDesde = Time.time;
            SortearRitmo();
            AplicarAparencia();
            AplicarNivel(Nivel.Longe, true);
        }

        /// <summary>Muda o nível de detalhe no anfitrião: além do visual, liga ou desliga a navegação.</summary>
        internal void MudarNivelNoServidor(Nivel n)
        {
            if (n == NivelAtual) return;
            var antes = NivelAtual;
            AplicarNivel(n);
            if (Morto) return;   // o corpo não anda mais
            if (antes == Nivel.Longe) ChegouDeLonge();
            else if (n == Nivel.Longe) _longeDesde = Time.time;
            if (Petrificado) return;   // a estátua não anda (nem liga o agente)
            if (n == Nivel.Longe)
            {
                // zumbi virtual: guarda só a posição e o estado, sem agente nem animação
                if (agente.enabled)
                {
                    if (agente.isOnNavMesh) agente.ResetPath();
                    agente.enabled = false;
                }
                if (Atual != Estado.Parado) Pausar(Time.time);
                Velocidade = 0f;
                return;
            }
            if (!agente.enabled)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) transform.position = hit.position;
                agente.enabled = true;
            }
            agente.obstacleAvoidanceType = n == Nivel.Perto ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        /// <summary>A cada quadro (níveis Perto e Médio): velocidade, giro e animação.</summary>
        internal void QuadroNoServidor(float dt)
        {
            PassarDoTempoDasPernas();
            Velocidade = agente.enabled ? agente.velocity.magnitude : 0f;
            if (_girando && !Morto)
            {
                Vector3 dir = _olharPara - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 300f * dt);
            }
            else if (Atual == Estado.Procurando) transform.Rotate(0f, _giro * dt, 0f);
            Animar(dt);
            AtualizarRosto();
            MostrarPedra(dt);
            VozNoQuadro();
        }

        /// <summary>Uma decisão. No nível Médio ele só vaga e responde a tiros; a percepção é só no Perto.</summary>
        internal void Pensar(float agora, float dt)
        {
            if (Morto || Petrificado || !NoNavMesh) return;
            var a = _m.ajustes;
            if (_golpeEm > 0f && agora >= _golpeEm) Acertar(a);
            Destravar(agora);
            // a luz (a lanterna, o farol): pula para fora do facho (Zumbi.Luz.cs)
            if (Luz(agora, a)) return;
            if (Atual == Estado.Fugindo)
            {
                FugirDoSol(agora, a);
                return;
            }
            if (NivelAtual == Nivel.Perto && Atual != Estado.Perseguindo && Atual != Estado.Atacando) Perceber(agora, dt, a);
            switch (Atual)
            {
                case Estado.Parado:
                    if (agora >= _fimDaPausa) Vagar(agora, a);
                    break;
                case Estado.Vagando:
                    if (Chegou()) Pausar(agora);
                    break;
                case Estado.Investigando:
                    if (Chegou()) Procurar(agora);
                    break;
                case Estado.Procurando:
                    if (agora >= _fimDaPausa)
                    {
                        _casa = transform.position;
                        Vagar(agora, a);
                    }
                    break;
                case Estado.Perseguindo:
                    Perseguir(agora, a);
                    break;
                case Estado.Atacando:
                    Atacar(agora, a);
                    break;
                case Estado.Abrigando:
                    // chegou ao abrigo: fica por ali (vaga em volta, só onde o sol não bate)
                    if (Chegou())
                    {
                        _casa = transform.position;
                        Pausar(agora);
                    }
                    break;
            }
        }

        void Perceber(float agora, float dt, AjustesDeZumbi a)
        {
            Vector3 olho = transform.position + Vector3.up * AlturaDoOlho;
            float maior = 0f;
            int quem = -1;
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                if (BunkerRede.Isolado(p.peito, olho)) continue;
                Vector3 para = p.peito - olho;
                float d = para.magnitude;
                float forca;
                bool perto = d < a.distanciaDeContato || (p.correndo && d < a.barulhoDeCorrida);
                if (perto) forca = 100f;   // sente ou ouve: na hora
                else
                {
                    float alcance = _m.AlcanceDaVisao(p);
                    if (d > alcance) continue;
                    bool naFrente = Vector3.Angle(transform.forward, para) < a.meioAnguloDaVisao;
                    // a luz da lanterna batendo nele: nota mesmo de costas
                    bool luzNele = p.lanterna && Vector3.Angle(p.olhar, -para) < 25f;
                    if ((!naFrente && !luzNele) || !_m.Enxerga(olho, p.peito)) continue;
                    forca = Mathf.Lerp(4f, 1.2f, d / alcance);   // quanto mais perto, mais rápido ele percebe
                }
                if (forca > maior) { maior = forca; quem = i; }
            }
            if (quem < 0)
            {
                _alerta = Mathf.Max(0f, _alerta - dt * 0.5f);
                return;
            }
            _alerta += maior * dt;
            ref readonly var visto = ref _m.Jogadores[quem];
            // de dia e ao amanhecer, ele prioriza se esconder: quem está onde o sol bate (ou vai bater) só é observado,
            // mesmo encostando ou correndo perto; ele só sai atrás se foi atacado (tiro, soco) ou com um tiro bem perto
            // (Ameacado; a pedido em 28/10/2026: antes encostar e correr perto também o tiravam da sombra)
            if (visto.exposto && !Ameacado) _alerta = Mathf.Min(_alerta, 0.95f);
            if (_alerta >= 1f)
            {
                ComecarPerseguicao(visto.jogador, agora);
                return;
            }
            // desconfiado: para e vira para o que viu
            if (Atual == Estado.Vagando || Atual == Estado.Parado)
            {
                agente.ResetPath();
                Atual = Estado.Parado;
                _fimDaPausa = agora + 1.5f;
                OlharPara(visto.pes);
            }
        }

        void Vagar(float agora, AjustesDeZumbi a)
        {
            for (int t = 0; t < 4; t++)
            {
                Vector2 r = Random.insideUnitCircle * a.raioDeVagar;
                Vector3 p = _casa + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - _casa.y) < 1.2f
                    && !(_m.ProtegerDoSol && _m.Exposto(hit.position))   // de dia e ao amanhecer, só vaga onde o sol não bate
                    && !(_m.Fachos.Count > 0 && _m.NoFacho(hit.position + Vector3.up * 1.2f, out _)))   // nem para dentro da luz
                {
                    Ir(hit.position, a.velocidadeVagando, 0.4f);
                    Atual = Estado.Vagando;
                    return;
                }
            }
            Pausar(agora);
        }

        void Ir(Vector3 destino, float velocidade, float parada)
        {
            _velocidadeBase = velocidade;
            agente.speed = velocidade * FatorDasPernas * FatorDaPedra * _ritmo;   // mancando, rastejando ou virando pedra, mais devagar; cada um no seu ritmo
            agente.stoppingDistance = parada;
            agente.isStopped = false;
            agente.SetDestination(destino);
            _girando = false;
        }

        void Pausar(float agora)
        {
            if (NoNavMesh) agente.ResetPath();
            Atual = Estado.Parado;
            var a = _m.ajustes;
            _fimDaPausa = agora + Random.Range(a.pausa.x, a.pausa.y) * _preguica;
        }

        void Procurar(float agora)
        {
            if (NoNavMesh) agente.ResetPath();
            Atual = Estado.Procurando;
            _fimDaPausa = agora + Random.Range(3f, 6f);
            _giro = Random.value < 0.5f ? -45f : 45f;
            _girando = false;
        }

        void OlharPara(Vector3 ponto)
        {
            _olharPara = ponto;
            _girando = true;
        }

        bool Chegou() => !agente.pathPending && (!agente.hasPath || agente.remainingDistance <= agente.stoppingDistance + 0.2f);

        /// <summary>Foi ver um barulho: anda até lá, olha em volta e passa a vagar ali.</summary>
        internal void Investigar(Vector3 ponto, float agora)
        {
            if (!NoNavMesh || Morto || Petrificado) return;
            _alvo = null;
            if (NavMesh.SamplePosition(ponto, out var hit, 3f, NavMesh.AllAreas)) ponto = hit.position;
            Ir(ponto, _m.ajustes.velocidadeInvestigando, 1f);
            Atual = Estado.Investigando;
        }

        internal void ComecarPerseguicao(JogadorRede jogador, float agora)
        {
            if (jogador == null || Morto || Petrificado) return;
            bool novo = Atual != Estado.Perseguindo && Atual != Estado.Atacando;
            _alvo = jogador;
            _alerta = 1f;
            _ultimoVisto = agora;
            _ultimaPosicao = jogador.transform.position;
            _proximoCaminho = 0f;
            Atual = Estado.Perseguindo;
            _m.Engajou(this);   // sobe direto para o nível Perto (liga o agente, se estava desligado)
            if (NoNavMesh) Ir(_ultimaPosicao, _m.ajustes.velocidadePerseguindo, 0.9f);
            if (novo) Chamar();   // achou alguém: o chamado (os de perto respondem)
        }

        void Perseguir(float agora, AjustesDeZumbi a)
        {
            if (_alvo == null || !_alvo.IsSpawned)
            {
                Desistir(agora);
                return;
            }
            Vector3 pos = _alvo.transform.position;
            Vector3 delta = pos - transform.position;
            float d = delta.magnitude;
            if (d > a.distanciaDeDesistir || DesisteDoSol(pos))
            {
                Desistir(agora);
                return;
            }
            if (d < 3f || _m.Enxerga(transform.position + Vector3.up * AlturaDoOlho, pos + Vector3.up * 1.3f))
            {
                _ultimoVisto = agora;
                _ultimaPosicao = pos;
            }
            if (agora - _ultimoVisto > a.tempoSemVer)
            {
                // sumiu de vista: vai até onde o viu por último, olha em volta e desiste
                Investigar(_ultimaPosicao, agora);
                return;
            }
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            // no alcance do golpe: ataca quando chega ao lugar dele no cerco (senão os primeiros atacavam de onde
            // chegavam, todos do mesmo lado); colado no jogador (a menos de 1 m), bate na hora; no alcance, espera chegar ao
            // lugar no máximo 1,5 s (a pedido em 28/10/2026: chegando perto de um que estava dando a volta, ele não batia)
            bool noAlcance = horizontal < a.alcanceDoAtaque && Mathf.Abs(delta.y) < 1.2f;
            if (!noAlcance) _noAlcanceDesde = -1f;
            else if (_noAlcanceDesde < 0f) _noAlcanceDesde = agora;
            if (noAlcance && (horizontal < 1f || NoLugarDoCerco(pos) || agora - _noAlcanceDesde > 1.5f))
            {
                Atual = Estado.Atacando;
                agente.ResetPath();
                _proximoGolpe = Mathf.Max(_proximoGolpe, agora + 0.25f);
                return;
            }
            if (agora >= _proximoCaminho)
            {
                // a horda cerca: cada um vai para o lugar dele em volta do alvo (Zumbi.Cerco.cs), não todos para o mesmo ponto
                Ir(DestinoNoCerco(_ultimaPosicao, d), a.velocidadePerseguindo, _temCerco ? ParadaNoCerco : 0.9f);
                _proximoCaminho = agora + 0.25f;
            }
        }

        void Atacar(float agora, AjustesDeZumbi a)
        {
            if (_alvo == null || !_alvo.IsSpawned)
            {
                Desistir(agora);
                return;
            }
            Vector3 pos = _alvo.transform.position;
            if (DesisteDoSol(pos))
            {
                Desistir(agora);
                return;
            }
            Vector3 delta = pos - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude > a.alcanceDoAtaque + 0.4f || Mathf.Abs(delta.y) > 1.4f)
            {
                Atual = Estado.Perseguindo;
                _proximoCaminho = 0f;
                return;
            }
            _ultimoVisto = agora;
            _ultimaPosicao = pos;
            OlharPara(pos);
            if (agora >= _proximoGolpe && _pernas != 3)
            {
                _proximoGolpe = agora + a.intervaloDoGolpe;
                _golpeEm = agora + a.atrasoDoDano;   // o dano sai quando a animação agarra
                Golpes++;
                MostrarGolpe();
            }
        }

        // o momento do golpe: se o alvo ainda está ao alcance, tira vida
        void Acertar(AjustesDeZumbi a)
        {
            _golpeEm = 0f;
            if (_alvo == null || !_alvo.IsSpawned) return;
            Vector3 delta = _alvo.transform.position - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude > a.alcanceDoAtaque + 0.5f || Mathf.Abs(delta.y) > 1.4f) return;
            _m.Golpear(_alvo, a.danoDoGolpe, transform.position);
        }

        void Desistir(float agora)
        {
            _alvo = null;
            _alerta = 0f;
            _casa = transform.position;
            Procurar(agora);   // olha em volta um pouco e depois volta a vagar ali mesmo
        }

        /// <summary>
        /// Um tiro confirmado pelo anfitrião. Na cabeça, ela explode e ele morre; no resto, o corpo perde vida (nos braços
        /// e nas pernas, só uma parte do dano) e cai morto quando ela acaba. A direção do tiro empurra o corpo que cai, e o
        /// arremesso (a espingarda de perto) joga o corpo inteiro para trás.
        /// </summary>
        internal void LevarTiro(RegiaoDoCorpo regiao, Membro membro, float dano, Vector3 ponto, Vector3 direcao, bool arremesso, JogadorRede autor, float agora)
        {
            if (Morto) return;
            var a = _m.ajustes;
            // a estátua: o carro (acima de 3 m/s) e a espingarda de perto (o arremesso) a quebram de uma vez
            if (Petrificado && arremesso)
            {
                Morrer(agora, false, ponto, direcao, arremesso);
                return;
            }
            // a cabeça explode com um tiro, a não ser que a pedra já tenha chegado nela
            if (regiao == RegiaoDoCorpo.Cabeca && Pedra < a.pedraQueProtegeACabeca)
            {
                Morrer(agora, true, ponto, direcao, arremesso);
                return;
            }
            // a casca de pedra: o tiro tira menos, quanto mais pedra
            float naPedra = Mathf.Lerp(1f, a.danoNaPedra, Pedra);
            bool noMembro = regiao == RegiaoDoCorpo.Braco || regiao == RegiaoDoCorpo.Perna;
            _danoNoCorpo += (noMembro ? dano * a.danoNosMembros : dano) * naPedra;
            // a vida do membro: acabou, ele cai no pedaço que levou o tiro (o de baixo, ou o membro todo); de pedra, não cai
            int limbo = Membros.Limbo(membro);
            if (noMembro && limbo >= 0 && Pedra < 0.5f && !Membros.Perdeu(Cortes, membro))
            {
                _vidaDosMembros[limbo] -= dano;
                if (_vidaDosMembros[limbo] <= 0.001f)
                {
                    int novo = Membros.DeBaixo(membro) ? 1 : 2;
                    if (novo > Membros.Estado(Cortes, limbo))
                    {
                        _vidaDosMembros[limbo] = a.vidaDoMembro;   // o que sobrou do membro (o braço ou a coxa) tem vida nova
                        MudarCortes(Membros.ComEstado(Cortes, limbo, novo), true, direcao);
                    }
                }
            }
            if (_danoNoCorpo >= a.vidaDoCorpo)
            {
                Morrer(agora, false, ponto, direcao, arremesso);
                return;
            }
            if (Petrificado) return;   // a estátua não reage nem vai atrás
            Reacoes++;
            MostrarReacao();
            _engajadoAte = agora + 5f;
            Ferir(agora);
            if (autor != null) ComecarPerseguicao(autor, agora);
        }

        // a parte do corpo (com física) mais perto do ponto do tiro: é ela que leva o empurrão
        int ParteMaisPerto(Vector3 ponto)
        {
            int melhor = 1;
            float menor = float.MaxValue;
            for (int i = 0; i < partes.Length; i++)
            {
                float d = (partes[i].position - ponto).sqrMagnitude;
                if (d < menor) { menor = d; melhor = i; }
            }
            return melhor;
        }

        void Morrer(float agora, bool semCabeca, Vector3 ponto, Vector3 direcao, bool arremesso)
        {
            if (Morto) return;
            SemCabeca = semCabeca;
            _arremessado = arremesso;
            _parteDaMorte = semCabeca ? CorpoDeZumbi.ParteCabeca : ParteMaisPerto(ponto);
            direcao.y = 0f;
            DirecaoDaMorte = direcao.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direcao).eulerAngles.y : transform.eulerAngles.y + 180f;
            Atual = Estado.Morto;
            mortoEm = agora;
            _alvo = null;
            _golpeEm = 0f;
            MostrarMorte(true);   // antes de desligar o agente: o corpo herda a velocidade dele
            if (agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            Velocidade = 0f;
            _m.ContarMorte();
        }

        // ------------------------------------------------------------------ cópia (nos outros jogadores)

        struct Amostra
        {
            public double tempo;
            public Vector3 posicao;
            public float yaw;
        }

        readonly Amostra[] _amostras = new Amostra[4];
        int _qtd;
        float _intervalo = 0.1f, _atraso = 0.2f;
        internal float ultimoRecebido;

        internal void PrepararCopia(int id, byte geracao, ZombieManager m)
        {
            Id = id; Geracao = geracao; Copia = true; _m = m;
            agente.enabled = false;
            _qtd = 0; _intervalo = 0.1f; _atraso = 0.2f;
            _visivel = true;
            AplicarAparencia();
            AplicarNivel(Nivel.Medio, true);
        }

        internal void Receber(double tempo, Vector3 posicao, float yaw, Estado estado, byte golpes, byte reacoes, byte morte, float velocidade, byte cortes, byte pedra)
        {
            ultimoRecebido = Time.unscaledTime;
            bool primeira = _qtd == 0;
            if (primeira) transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, yaw, 0f));
            else
            {
                double ultimo = _amostras[_qtd - 1].tempo;
                if (tempo <= ultimo) return;   // chegou fora de ordem
                _intervalo = Mathf.Lerp(_intervalo, (float)(tempo - ultimo), 0.3f);
            }
            if (_qtd == _amostras.Length)
            {
                for (int i = 1; i < _qtd; i++) _amostras[i - 1] = _amostras[i];
                _qtd--;
            }
            _amostras[_qtd++] = new Amostra { tempo = tempo, posicao = posicao, yaw = yaw };
            // o que mudou desde o último pacote vira efeito (depois de o estado novo entrar: o som da reação depende dele);
            // na primeira vez, o zumbi só aparece como já está
            bool chamou = !primeira && estado == Estado.Perseguindo && Atual != Estado.Perseguindo && Atual != Estado.Atacando && Atual != Estado.Esquivando;
            bool golpeou = !primeira && golpes != (Golpes & 3), reagiu = !primeira && reacoes != (Reacoes & 3);
            // os membros antes da morte: o corpo que cai já sai cortado
            if (cortes != Cortes && Atual != Estado.Morto) MudarCortes(cortes, !primeira, -transform.forward);
            bool morreu = estado == Estado.Morto && Atual != Estado.Morto;
            if (morreu)
            {
                // morto, o byte da velocidade traz a direção do tiro (a velocidade fica a do último pacote vivo: o corpo
                // cai andando), e o da morte traz a cabeça e a parte atingida
                SemCabeca = (morte & 1) != 0;
                _parteDaMorte = Mathf.Clamp((morte >> 1) & 15, 0, CorpoDeZumbi.Partes.Length - 1);
                _arremessado = (morte & 32) != 0;
                DirecaoDaMorte = velocidade;
            }
            else if (estado != Estado.Morto) Velocidade = velocidade;
            Pedra = pedra / 255f;
            if (primeira) _pedraVista = Pedra;
            bool eraPedra = Petrificado;
            Atual = estado;
            Golpes = golpes;
            Reacoes = reacoes;
            if (eraPedra != Petrificado) MudouAPedra(!primeira, primeira);
            if (chamou) Chamar();
            if (golpeou) MostrarGolpe();
            if (reagiu) MostrarReacao();
            if (morreu) MostrarMorte(!primeira);
        }

        /// <summary>
        /// Mostra a cópia um pouco no passado, entre as duas amostras que cercam esse momento. O atraso acompanha o
        /// ritmo de envio: ~0,2 s para os zumbis perto (10 envios/s) e ~0,5 s para os do nível médio.
        /// </summary>
        internal void QuadroDaCopia(double agoraNoServidor, float dt)
        {
            if (_qtd == 0) return;
            float atrasoAlvo = Mathf.Clamp(_intervalo * 1.6f + 0.05f, 0.12f, 0.7f);
            _atraso = Mathf.MoveTowards(_atraso, atrasoAlvo, dt * 0.25f);
            double t = agoraNoServidor - _atraso;
            Amostra a = _amostras[_qtd - 1], b = a;
            if (t < a.tempo)
            {
                a = b = _amostras[0];
                for (int i = 1; i < _qtd; i++)
                    if (t < _amostras[i].tempo)
                    {
                        a = _amostras[i - 1];
                        b = _amostras[i];
                        break;
                    }
            }
            float k = b.tempo > a.tempo ? Mathf.Clamp01((float)((t - a.tempo) / (b.tempo - a.tempo))) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(a.posicao, b.posicao, k), Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, k), 0f));
            PassarDoTempoDasPernas();
            Animar(dt);
            AtualizarRosto();
            MostrarPedra(dt);
            VozNoQuadro();
        }
    }
}
