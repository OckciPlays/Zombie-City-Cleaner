using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// O que o morador tem na mão (a camada de cima do controlador; a parte 8 do Plano_NPCs_Bunker.md, a segunda fatia): o
    /// maço, o cigarro acendendo, aceso, o peteleco (a bituca voa) ou apagado no cinzeiro, e o taco de sinuca em pé (o
    /// <see cref="Taco"/> mais o índice do taco: 0 de madeira, 1 escuro, 2 preto e 3 vermelho).
    /// </summary>
    public enum MaoDoMorador : byte { Nada, Maco, Acendendo, Aceso, Peteleco, NoCinzeiro, Taco = 16 }

    /// <summary>Quem é o morador, para as outras máquinas o vestirem e porem o nome em cima da cabeça (sem a ficha inteira).</summary>
    public struct QuemEOMorador : INetworkSerializable, IEquatable<QuemEOMorador>
    {
        public int id, semente;
        public FixedString64Bytes nome;
        public FixedString32Bytes tipoDaRoupa;
        public byte pele, cabelo;
        public bool mulher, grisalho, semBarba, velho, mancando;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref id);
            s.SerializeValue(ref semente);
            s.SerializeValue(ref nome);
            s.SerializeValue(ref tipoDaRoupa);
            s.SerializeValue(ref pele);
            s.SerializeValue(ref cabelo);
            s.SerializeValue(ref mulher);
            s.SerializeValue(ref grisalho);
            s.SerializeValue(ref semBarba);
            s.SerializeValue(ref velho);
            s.SerializeValue(ref mancando);
        }

        public bool Equals(QuemEOMorador o) =>
            id == o.id && semente == o.semente && nome.Equals(o.nome) && tipoDaRoupa.Equals(o.tipoDaRoupa) && pele == o.pele && cabelo == o.cabelo
            && mulher == o.mulher && grisalho == o.grisalho && semBarba == o.semBarba && velho == o.velho && mancando == o.mancando;

        public AparenciaDoMorador Aparencia => new AparenciaDoMorador
        {
            tipoDaRoupa = tipoDaRoupa.ToString(), semente = semente, mulher = mulher, pele = pele, cabelo = cabelo, grisalho = grisalho, semBarba = semBarba,
        };

        /// <summary>Quem é, pela ficha; 'nome' é o que vai em cima da cabeça (sem ele, o apelido ou o nome).</summary>
        public static QuemEOMorador Da(FichaDoMorador f, string nome = null)
        {
            var a = f.aparencia ?? new AparenciaDoMorador();
            if (string.IsNullOrEmpty(nome)) nome = string.IsNullOrEmpty(f.apelido) ? f.nome : f.apelido;
            return new QuemEOMorador
            {
                id = f.id, semente = a.semente, nome = new FixedString64Bytes(Curto(nome, 40)), tipoDaRoupa = new FixedString32Bytes(Curto(a.tipoDaRoupa ?? "Civil", 24)),
                pele = (byte)Mathf.Clamp(a.pele, 0, 255), cabelo = (byte)Mathf.Clamp(a.cabelo, 0, 255), mulher = a.mulher, grisalho = a.grisalho, semBarba = a.semBarba,
                velho = AgendaDoMorador.Velho(f), mancando = AgendaDoMorador.Mancando(f),
            };
        }

        static string Curto(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n);
    }

    /// <summary>
    /// O corpo de um morador do bunker (a parte 6 do Plano_NPCs_Bunker.md da pasta de arte): um NetworkObject por morador,
    /// que o anfitrião põe quando o andar dele está montado (<see cref="MoradoresDoBunker"/>) e tira quando não está (aí ele
    /// segue no papel). É o corpo do jogador (o "Corpo", a cópia do do <c>Jogador.prefab</c>, como o dos sobreviventes),
    /// vestido pela ficha (<see cref="RoupaDoMorador"/>), com um controlador só dele (a locomoção, com o mancar, e as ações
    /// da parte 5: sentar, deitar, comer, as bancadas) e as peças nas mãos (o prato e a colher do <c>Loot.fbx</c> nos vazios
    /// <c>Morador_*</c> do corpo). O anfitrião anda com ele (o NavMeshAgent) e diz a ação (<see cref="Acao"/>); todas as
    /// máquinas tocam o sentar, o levantar e o resto pela ação. O nome vai em cima da cabeça (os <see cref="NomesDosJogadores"/>).
    /// </summary>
    public class CorpoDoMorador : NetworkBehaviour
    {
        [Tooltip("O corpo (a cópia do corpo do jogador, com o CorpoDoPersonagem).")]
        public CorpoDoPersonagem corpo;
        public NavMeshAgent agente;
        [Tooltip("O controlador dos moradores (a locomoção e as ações).")]
        public RuntimeAnimatorController controlador;
        [Tooltip("As malhas das peças nos vazios do corpo (o prato, a comida, a colher e o bocado): só aparecem nas ações que usam.")]
        public Renderer[] pratoEColher;
        [Tooltip("O livro aberto no vazio do livro: só no ler.")]
        public Renderer livro;
        [Tooltip("A roupa dobrando, nos vazios do livro (a peça aberta), da caixa (dobrada ao meio) e do prato (em quatro): só no Dobrar_Roupa.")]
        public Renderer[] roupaDobrando;
        [Tooltip("A fumaça e a bituca do cigarro (as do FumoJogador do jogador).")]
        public Material materialDaFumaca, materialDaBituca;
        public Mesh malhaDaBituca;

        /// <summary>Os corpos dos moradores nesta máquina.</summary>
        public static readonly List<CorpoDoMorador> Todos = new List<CorpoDoMorador>();

        public readonly NetworkVariable<QuemEOMorador> Quem = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>A ação (o <see cref="AcaoDoCorpo"/>, com o espelho da cama no bit de cima).</summary>
        public readonly NetworkVariable<byte> Acao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O que está na mão (o <see cref="MaoDoMorador"/>): a camada de cima, o taco e as peças do fumo.</summary>
        public readonly NetworkVariable<byte> Mao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>O quanto sobra do papel do cigarro (255: inteiro).</summary>
        public readonly NetworkVariable<byte> Sobra = new(255, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>As tragadas e as tacadas (contadores: cada uma que sobe toca o clipe dela em todas as máquinas).</summary>
        public readonly NetworkVariable<byte> Tragadas = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> Tacadas = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MancandoId = Animator.StringToHash("Mancando");
        static readonly int PassoId = Animator.StringToHash("Passo");
        static readonly int LocomocaoId = Animator.StringToHash("Locomocao");
        // as velocidades dos clipes (o Andar a 2,0 m/s e o Andar_Mancando a 1,5)
        const float AndarDoClipe = 2.0f, MancandoDoClipe = 1.5f;
        // o quanto o velho anda curvado (a coluna e o peito, para a frente)
        const float Curvado = 9f;

        public Animator Animador => corpo != null ? corpo.animador : null;
        public Transform Cabeca => corpo != null ? corpo.ossoCabeca : null;
        public string Nome => Quem.Value.nome.ToString();

        float _velocidade, _medida, _janela;
        Vector3 _posJanela;
        QuemEOMorador _vestido;
        bool _vestidoUmaVez, _espelhoDaCama, _camaAlta;
        byte _mostrado = 255;
        Transform _coluna, _peito;
        readonly Dictionary<string, float> _duracoes = new Dictionary<string, float>();

        // ------------------------------------------------------------------ a vida na rede

        // a ficha e a ação do começo (o anfitrião põe antes do Spawn; as variáveis da rede só valem a partir dele)
        QuemEOMorador _quemInicial;
        byte _acaoInicial;
        bool _comInicial;

        /// <summary>No anfitrião, antes do Spawn: quem é e a ação do começo (vão junto com o corpo para as outras máquinas).</summary>
        public void Preparar(QuemEOMorador quem, byte acao)
        {
            _quemInicial = quem;
            _acaoInicial = acao;
            _comInicial = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _comInicial)
            {
                Quem.Value = _quemInicial;
                Acao.Value = _acaoInicial;
                _comInicial = false;
            }
            Todos.Add(this);
            Quem.OnValueChanged += AoMudarQuem;
            Acao.OnValueChanged += AoMudarAcao;
            Mao.OnValueChanged += AoMudarAMao;
            Tragadas.OnValueChanged += AoTragar;
            Tacadas.OnValueChanged += AoTacar;
            if (agente != null) agente.enabled = false;
            _posJanela = transform.position;
            if (corpo != null && corpo.animador != null)
            {
                if (controlador != null) corpo.animador.runtimeAnimatorController = controlador;
                corpo.animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                corpo.animador.applyRootMotion = false;
                foreach (var c in corpo.animador.runtimeAnimatorController.animationClips) _duracoes[c.name] = c.length;
                _coluna = Osso("spine");
                _peito = Osso("chest");
            }
            if (corpo != null && corpo.naMao != null) foreach (var a in corpo.naMao) if (a != null) a.SetActive(false);
            if (corpo != null && corpo.renderersDoFumo != null) foreach (var r in corpo.renderersDoFumo) if (r != null) r.enabled = false;
            // as peças das armas nos ossos delas (o ferrolho, o carregador), como no estúdio do personagem
            if (corpo != null) foreach (var t in corpo.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("P_")) t.gameObject.SetActive(false);
            Vestir();
            Mostrar(Acao.Value, true);
        }

        public override void OnNetworkDespawn()
        {
            Todos.Remove(this);
            Quem.OnValueChanged -= AoMudarQuem;
            Acao.OnValueChanged -= AoMudarAcao;
            Mao.OnValueChanged -= AoMudarAMao;
            Tragadas.OnValueChanged -= AoTragar;
            Tacadas.OnValueChanged -= AoTacar;
            if (_taco != null) Destroy(_taco.gameObject);
        }

        Transform Osso(string nome)
        {
            foreach (var t in corpo.GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
            return null;
        }

        void AoMudarQuem(QuemEOMorador antes, QuemEOMorador depois) => Vestir();
        void AoMudarAcao(byte antes, byte depois) => Mostrar(depois, false);

        // as roupas pela aparência da ficha (o mesmo sorteio em todas as máquinas)
        void Vestir()
        {
            var q = Quem.Value;
            if (corpo == null || corpo.vestimenta == null || (_vestidoUmaVez && q.Equals(_vestido))) return;
            _vestidoUmaVez = true;
            _vestido = q;
            var roupas = corpo.vestimenta.transform.Find("Roupas");
            if (roupas != null) roupas.gameObject.SetActive(true);
            RoupaDoMorador.Vestir(corpo.vestimenta, q.Aparencia);
            name = $"Morador {q.id} ({q.nome})";
        }

        /// <summary>A duração de um clipe do controlador (s; 0 se ele não existe).</summary>
        public float Duracao(string clipe) => clipe != null && _duracoes.TryGetValue(clipe, out float d) ? d : 0f;

        // ------------------------------------------------------------------ a ação (em todas as máquinas)

        // da ação de antes para a nova: o levantar (a postura muda), o sentar ou o deitar (com o parado depois, pelo
        // controlador) ou a troca dentro da postura (ler na mesma cadeira); 'ja': direto no fim (quem chega ou o corpo novo)
        void Mostrar(byte b, bool ja)
        {
            var a = Animador;
            if (a == null || a.runtimeAnimatorController == null) return;
            var nova = AcoesDoCorpo.Acao(b);
            var pNova = AcoesDoCorpo.Postura(nova);
            if (pNova == PosturaDoCorpo.Cama)
            {
                _espelhoDaCama = AcoesDoCorpo.Espelhado(b);
                _camaAlta = AcoesDoCorpo.NaCamaAlta(b);
            }
            if (ja || _mostrado == 255)
            {
                _mostrado = b;
                string direto = AcoesDoCorpo.Clipe(nova);
                if (direto != null) a.Play(direto, 0, 0f);
                else a.Play(LocomocaoId, 0, 0f);
                return;
            }
            var pVelha = AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(_mostrado));
            _mostrado = b;
            if (pNova == pVelha)
            {
                string clipe = AcoesDoCorpo.Clipe(nova);
                if (clipe != null) a.CrossFadeInFixedTime(clipe, 0.35f, 0);
                else a.CrossFadeInFixedTime(LocomocaoId, 0.25f, 0);
            }
            else if (AcoesDoCorpo.Sair(pVelha) != null) a.CrossFadeInFixedTime(AcoesDoCorpo.Sair(pVelha), 0.15f, 0);
            else if (AcoesDoCorpo.Entrar(pNova) != null) a.CrossFadeInFixedTime(AcoesDoCorpo.Entrar(pNova), 0.2f, 0);
            else if (AcoesDoCorpo.Clipe(nova) != null) a.CrossFadeInFixedTime(AcoesDoCorpo.Clipe(nova), 0.3f, 0);
            else a.CrossFadeInFixedTime(LocomocaoId, 0.3f, 0);
        }

        /// <summary>De pé de verdade: a ação é andar e o controlador já está na locomoção (o levantar acabou).</summary>
        public bool EmPe
        {
            get
            {
                var a = Animador;
                if (a == null) return true;
                if (AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(Acao.Value)) != PosturaDoCorpo.EmPe) return false;
                return a.GetCurrentAnimatorStateInfo(0).shortNameHash == LocomocaoId && !a.IsInTransition(0);
            }
        }

        void Update()
        {
            if (!IsSpawned || Animador == null) return;
            // a velocidade: no anfitrião, a do agente; nos outros, a da posição recebida (como a dos sobreviventes)
            float medida;
            if (IsServer && agente != null && agente.enabled) medida = new Vector3(agente.velocity.x, 0f, agente.velocity.z).magnitude;
            else
            {
                _janela += Time.deltaTime;
                if (_janela >= 0.1f)
                {
                    var d = transform.position - _posJanela;
                    d.y = 0f;
                    _medida = Mathf.Min(d.magnitude / _janela, 4f);
                    _posJanela = transform.position;
                    _janela = 0f;
                }
                medida = _medida;
            }
            _velocidade = Mathf.Lerp(_velocidade, medida, 1f - Mathf.Exp(-10f * Time.deltaTime));
            var q = Quem.Value;
            float doClipe = q.mancando ? MancandoDoClipe : AndarDoClipe;
            Animador.SetFloat(SpeedId, Mathf.Clamp01(_velocidade / 0.5f));
            Animador.SetFloat(MancandoId, q.mancando ? 1f : 0f);
            Animador.SetFloat(PassoId, _velocidade > 0.3f ? Mathf.Clamp(_velocidade / doClipe, 0.35f, 1.5f) : 1f);

            // a ação muda de postura e o sentar acabou: a variação do lugar (ler na cadeira, comer no banco)
            var acao = AcoesDoCorpo.Acao(Acao.Value);
            var postura = AcoesDoCorpo.Postura(acao);
            string base_ = AcoesDoCorpo.Base(postura), clipe = AcoesDoCorpo.Clipe(acao);
            if (base_ != null && clipe != null && clipe != base_ && !Animador.IsInTransition(0) && Animador.GetCurrentAnimatorStateInfo(0).IsName(base_))
                Animador.CrossFadeInFixedTime(clipe, 0.35f, 0);

            // a camada de cima (do peito para cima): o choro da crise, parado no lugar, por cima do parado; o maço, o cigarro e o
            // taco em pé (a parte 8), menos na tacada (o clipe dela, na base, tem o taco)
            var info = Animador.GetCurrentAnimatorStateInfo(0);
            if (Animador.layerCount > 1)
            {
                bool chorando = acao == AcaoDoCorpo.Chorando && _velocidade < 0.3f;
                bool tacada = info.IsName("Sinuca_Tacada") || (Animador.IsInTransition(0) && Animador.GetNextAnimatorStateInfo(0).IsName("Sinuca_Tacada"));
                var mao = (MaoDoMorador)Mao.Value;
                bool naMao = mao != MaoDoMorador.Nada && !tacada;
                float peso = Animador.GetLayerWeight(1), alvo = chorando || naMao ? 1f : 0f;
                if (!Mathf.Approximately(peso, alvo)) Animador.SetLayerWeight(1, Mathf.MoveTowards(peso, alvo, Time.deltaTime * (tacada ? 8f : 2.5f)));
                if (chorando && !Animador.IsInTransition(1) && !Animador.GetCurrentAnimatorStateInfo(1).IsName("Chorar"))
                    Animador.CrossFadeInFixedTime("Chorar", 0.3f, 1);
                else if (!chorando && naMao && mao >= MaoDoMorador.Taco && !Animador.IsInTransition(1) && !Animador.GetCurrentAnimatorStateInfo(1).IsName("Taco_Segurar"))
                    Animador.CrossFadeInFixedTime("Taco_Segurar", 0.3f, 1);
            }

            // o espelho da cama (deitado do outro lado) enquanto o corpo está no deitar, dormindo ou no levantar
            bool naCama = info.IsName("Deitar") || info.IsName("Dormindo") || info.IsName("Levantar_Cama");
            if (Animador.IsInTransition(0))
            {
                var prox = Animador.GetNextAnimatorStateInfo(0);
                naCama |= prox.IsName("Deitar") || prox.IsName("Dormindo") || prox.IsName("Levantar_Cama");
            }
            float x = naCama && _espelhoDaCama ? -1f : 1f;
            var t = corpo.transform;
            if (!Mathf.Approximately(t.localScale.x, x)) t.localScale = new Vector3(x, 1f, 1f);
            // na cama alta, o corpo sobe com o deitar (da metade dele para a frente), fica dormindo e desce no levantar
            float sobe = 0f;
            if (naCama && _camaAlta)
            {
                if (info.IsName("Dormindo")) sobe = 1f;
                else if (info.IsName("Deitar")) sobe = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.9f, info.normalizedTime));
                else if (info.IsName("Levantar_Cama")) sobe = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.6f, info.normalizedTime));
                else if (Animador.IsInTransition(0) && Animador.GetNextAnimatorStateInfo(0).IsName("Dormindo")) sobe = 1f;
            }
            float y = sobe * AcoesDoCorpo.AlturaDaCamaAlta;
            if (!Mathf.Approximately(t.localPosition.y, y)) t.localPosition = new Vector3(0f, y, 0f);

            // o prato e a colher só nas ações que usam (a escala dos ossos diz quando, dentro do clipe)
            bool comPrato = info.IsName("Comer") || info.IsName("Lavar_Louca");
            if (pratoEColher != null)
                foreach (var r in pratoEColher) if (r != null && r.enabled != comPrato) r.enabled = comPrato;
            bool lendo = info.IsName("Sentado_Lendo");
            if (livro != null && livro.enabled != lendo) livro.enabled = lendo;
            bool dobrando = info.IsName("Dobrar_Roupa");
            if (roupaDobrando != null)
                foreach (var r in roupaDobrando) if (r != null && r.enabled != dobrando) r.enabled = dobrando;
        }

        // ------------------------------------------------------------------ o que está na mão (a parte 8, a segunda fatia)

        static readonly int TragarId = Animator.StringToHash("Cigarro_Tragar");

        // a mão mudou: a camada de cima vai para o clipe dela (o maço, o acender, o cigarro, o peteleco); o taco e o choro, o Update
        void AoMudarAMao(byte antes, byte depois)
        {
            var a = Animador;
            if (a == null || a.layerCount < 2) return;
            string clipe = (MaoDoMorador)depois switch
            {
                MaoDoMorador.Maco => "Maco_Segurar",
                MaoDoMorador.Acendendo => "Maco_Acender",
                MaoDoMorador.Aceso => "Cigarro_Segurar",
                MaoDoMorador.Peteleco or MaoDoMorador.NoCinzeiro => "Cigarro_Peteleco",
                _ => null,
            };
            if (clipe != null) a.CrossFadeInFixedTime(clipe, 0.25f, 1);
            if ((MaoDoMorador)depois == MaoDoMorador.Peteleco) StartCoroutine(SoltarABituca());
        }

        void AoTragar(byte antes, byte depois)
        {
            var a = Animador;
            if (a != null && a.layerCount > 1 && (MaoDoMorador)Mao.Value == MaoDoMorador.Aceso) a.CrossFadeInFixedTime(TragarId, 0.15f, 1);
            _tragouEm = Time.time;
            StartCoroutine(Baforar());
        }

        void AoTacar(byte antes, byte depois)
        {
            var a = Animador;
            if (a != null) a.CrossFadeInFixedTime("Sinuca_Tacada", 0.2f, 0);
        }

        // o taco na mão (como o do jogador, o TacoNaMao): a malha do taco de cada cor entre a pegada e a ponta, depois da animação
        Transform _taco;
        MeshFilter _malhaDoTaco;
        Vector3 _eixoDoTaco = Vector3.right, _pegadaDoTaco;

        void OTaco()
        {
            var mao = (MaoDoMorador)Mao.Value;
            int k = mao >= MaoDoMorador.Taco ? mao - MaoDoMorador.Taco : -1;
            var mundo = Loot.ItensNoMundo.Instancia;
            var item = k >= 0 ? (Loot.Item)((int)Loot.Item.TacoMadeira + k) : Loot.Item.Nada;
            int i = Loot.Itens.IndiceDaSinuca(item);
            var malha = k >= 0 && mundo != null && mundo.malhasDaSinuca != null && i >= 0 && i < mundo.malhasDaSinuca.Length ? mundo.malhasDaSinuca[i] : null;
            bool mostrar = malha != null && corpo != null && corpo.taco != null && corpo.pontaDoTaco != null;
            if (!mostrar)
            {
                if (_taco != null && _taco.gameObject.activeSelf) _taco.gameObject.SetActive(false);
                return;
            }
            if (_taco == null)
            {
                var go = new GameObject("Taco (morador)") { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                _malhaDoTaco = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterial = mundo.material;
                _taco = go.transform;
            }
            if (_malhaDoTaco.sharedMesh != malha)
            {
                _malhaDoTaco.sharedMesh = malha;
                TacoNaMao.Medir(malha, out _eixoDoTaco, out _pegadaDoTaco);
            }
            if (!_taco.gameObject.activeSelf) _taco.gameObject.SetActive(true);
            TacoNaMao.Por(_taco, _eixoDoTaco, _pegadaDoTaco, corpo.taco.position, corpo.pontaDoTaco.position + _pontaSobe);
        }

        // ------------------------------------------------------------------ a ponte da tacada em cima da tabela

        // A tacada do bloco_morador.py tem a ponte (a mão esquerda) 74 cm à frente da raiz, no pano, e a bola branca a
        // 1,03 m. No jogo, a raiz fica a 1,03 m da bola, e com a bola perto da tabela do lado de quem joga a ponte cai em
        // cima dela: a mão ficava dentro da madeira (a tabela fica 4,8 cm acima do pano, na escala do modelo), e de frente
        // parecia dentro do corpo (07/11/2026). Depois da animação, a mão sobe para cima da tabela (o braço esquerdo por IK
        // de dois ossos, a mão com o mesmo giro) e o taco sobe junto na ponte (a ponta, girando na pegada).
        Transform _ombroE, _cotoveloE, _maoE;
        World.MesaDeSinuca _mesaDaPonte;
        Vector3 _pontaSobe;
        // a tabela acima do pano, na escala do modelo da mesa (o MesaSinuca: a borracha de 0,45 a 0,50 e a madeira até a
        // beira, 1,15 por 0,655, todas a 0,828, com o pano a 0,78), e o pano por dentro dela (o World.FisicaDaSinuca); o
        // punho fica 2 cm acima do que está embaixo da mão (na escala da mesa, 0,02), como na animação no pano
        const float TabelaAcimaDoPano = 0.048f, BeiraX = 1.16f, BeiraZ = 0.665f, PunhoAcima = 0.02f;

        void APonte()
        {
            _pontaSobe = Vector3.zero;
            var a = Animador;
            if (a == null || corpo == null || !a.GetCurrentAnimatorStateInfo(0).IsName("Sinuca_Tacada")) return;
            if (_maoE == null)
            {
                _maoE = Osso("hand.L");
                _cotoveloE = Osso("forearm.L");
                _ombroE = Osso("upper_arm.L");
            }
            if (_maoE == null || _cotoveloE == null || _ombroE == null) return;
            if (_mesaDaPonte == null || (_mesaDaPonte.mesa.position - transform.position).sqrMagnitude > 9f)
            {
                _mesaDaPonte = null;
                foreach (var m in FindObjectsByType<World.MesaDeSinuca>(FindObjectsInactive.Exclude))
                    if (m.mesa != null && (m.mesa.position - transform.position).sqrMagnitude < 9f) _mesaDaPonte = m;
                if (_mesaDaPonte == null) return;
            }
            var mesa = _mesaDaPonte.mesa;
            var q = mesa.InverseTransformPoint(_maoE.position);
            float ax = Mathf.Abs(q.x), az = Mathf.Abs(q.z);
            // longe da mesa: nada
            if (ax > BeiraX + 0.12f || az > BeiraZ + 0.12f) return;
            // a ponte só vale com a mão apoiada: no meio da tacada (o bloco_morador.py faz a ponte do quadro 9 ao 84 de 100) e
            // baixa, perto do pano (no começo e no fim, ela sobe e desce do lado do corpo)
            float tempo = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float apoiada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.16f, tempo)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 0.88f, tempo)))
                            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.18f, q.y - _mesaDaPonte.alturaDoPano)));
            if (apoiada < 0.01f) return;
            // a mão que passou da beira volta para cima da tabela; o punho, 2 cm acima do que está embaixo dele (o pano, ou a
            // tabela, subindo na borracha). A altura é a certa, e não a da animação mais a tabela: o velho anda curvado (o
            // Curvar) e as mãos dele ficavam mais baixas que no Blender, dentro da madeira mesmo no pano
            float x = Mathf.Clamp(q.x, -BeiraX + 0.03f, BeiraX - 0.03f), z = Mathf.Clamp(q.z, -BeiraZ + 0.03f, BeiraZ - 0.03f);
            float naTabela = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(World.FisicaDaSinuca.MeioComprimento - 0.01f, World.FisicaDaSinuca.MeioComprimento + 0.03f, Mathf.Abs(x))),
                                       Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(World.FisicaDaSinuca.MeioLargura - 0.01f, World.FisicaDaSinuca.MeioLargura + 0.03f, Mathf.Abs(z))));
            float y = _mesaDaPonte.alturaDoPano + TabelaAcimaDoPano * naTabela + PunhoAcima;
            var alvoNaMesa = Vector3.Lerp(q, new Vector3(x, Mathf.Max(q.y, y), z), apoiada);
            var antes = _maoE.position;
            var alvo = mesa.TransformPoint(alvoNaMesa);
            if ((alvo - antes).sqrMagnitude < 4e-6f) return;
            BracoAte(_ombroE, _cotoveloE, _maoE, alvo);
            float sobe = alvo.y - antes.y;
            // o taco sobe na ponte: a ponta sobe na proporção (a pegada fica)
            if (sobe > 0.002f && corpo.taco != null && corpo.pontaDoTaco != null)
            {
                var g = corpo.taco.position;
                var eixo = corpo.pontaDoTaco.position - g;
                float l = eixo.magnitude, ate = Vector3.Dot(antes - g, eixo / Mathf.Max(l, 1e-4f));
                if (ate > 0.2f) _pontaSobe = Vector3.up * (sobe * l / ate);
            }
        }

        // o braço (do ombro ao punho) até o punho chegar no alvo, dobrando no mesmo plano; a mão fica com o giro de antes
        static void BracoAte(Transform ombro, Transform cotovelo, Transform mao, Vector3 alvo)
        {
            var giroDaMao = mao.rotation;
            Vector3 S = ombro.position, E = cotovelo.position, W = mao.position;
            float a = (E - S).magnitude, b = (W - E).magnitude;
            var paraAlvo = alvo - S;
            float c = Mathf.Clamp(paraAlvo.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            var dir = paraAlvo.normalized;
            // para onde o cotovelo aponta (a parte dele fora da reta do ombro ao punho)
            var polo = Vector3.ProjectOnPlane(E - S, dir);
            if (polo.sqrMagnitude < 1e-8f) polo = Vector3.ProjectOnPlane(Vector3.down, dir);
            polo.Normalize();
            float cosA = Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f);
            var novoE = S + dir * (a * cosA) + polo * (a * Mathf.Sqrt(1f - cosA * cosA));
            ombro.rotation = Quaternion.FromToRotation(E - S, novoE - S) * ombro.rotation;
            var w = mao.position;
            cotovelo.rotation = Quaternion.FromToRotation(w - novoE, S + dir * c - novoE) * cotovelo.rotation;
            mao.rotation = giroDaMao;
        }

        // as peças do fumo (como as do FumoJogador): os vazios no lugar, o papel encolhendo, a brasa, a fumaça e a luz
        Transform _cigarro, _brasa, _chama, _papel, _ponta;
        Vector3 _eixo = Vector3.forward, _ponta0;
        ParticleSystem _fio, _baforada;
        Light _luz;
        bool _fumoMontado;
        float _tragouEm = -99f;

        void MontarOFumo()
        {
            _fumoMontado = true;
            var f = corpo != null ? corpo.fumo : null;
            if (f == null || f.Length < 6) return;
            _cigarro = f[2];
            _brasa = f[3];
            _chama = f[5];
            if (_cigarro != null)
                foreach (Transform t in _cigarro)
                {
                    if (t.name.EndsWith("_Papel")) _papel = t;
                    else if (t.name.EndsWith("_Ponta")) _ponta = t;
                }
            if (_papel != null)
            {
                var mf = _papel.GetComponent<MeshFilter>();
                var limites = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.forward * 0.04f, Vector3.forward * 0.08f);
                var b = limites.size;
                _eixo = b.x >= b.y && b.x >= b.z ? Vector3.right : b.y >= b.z ? Vector3.up : Vector3.forward;
                _ponta0 = _eixo * Vector3.Dot(limites.max, _eixo);
            }
            if (materialDaFumaca != null) _fio = FumoJogador.Fumaca(transform, materialDaFumaca, "Fumaça do cigarro", gameObject.layer, false, false);
            var luz = new GameObject("Luz da brasa");
            luz.transform.SetParent(transform, false);
            _luz = luz.AddComponent<Light>();
            _luz.type = LightType.Point;
            _luz.shadows = LightShadows.None;
            _luz.enabled = false;
        }

        void OFumo()
        {
            var mao = (MaoDoMorador)Mao.Value;
            bool fumando = mao >= MaoDoMorador.Maco && mao <= MaoDoMorador.NoCinzeiro;
            if (corpo != null && corpo.renderersDoFumo != null)
                foreach (var r in corpo.renderersDoFumo) if (r != null && r.enabled != fumando) r.enabled = fumando;
            if (!fumando && !_fumoMontado) return;
            if (!_fumoMontado) MontarOFumo();
            if (!fumando)
            {
                if (_fio != null)
                {
                    var e = _fio.emission;
                    e.enabled = false;
                }
                if (_luz != null) _luz.enabled = false;
                return;
            }
            FumoJogador.Assentar(corpo.fumo);
            float s = Mathf.Lerp(0.15f, 1f, Sobra.Value / 255f);
            if (_papel != null) _papel.localScale = Vector3.one + _eixo * (s - 1f);
            if (_ponta != null) _ponta.localPosition = _ponta0 * s;
            if (_brasa != null && _cigarro != null && s < 0.999f) _brasa.position = Vector3.LerpUnclamped(_cigarro.position, _brasa.position, s);
            bool acesa = _brasa != null && _brasa.lossyScale.x > 0.5f * Mathf.Abs(transform.lossyScale.x);
            bool chama = _chama != null && _chama.lossyScale.x > 0.5f;
            if (_fio != null)
            {
                var e = _fio.emission;
                e.enabled = acesa;
                if (acesa) _fio.transform.position = _brasa.position;
            }
            if (_luz != null)
            {
                _luz.enabled = acesa || chama;
                if (_luz.enabled)
                {
                    float puxa = Time.time - _tragouEm;
                    float forte = puxa > FumoJogador.PuxaDe && puxa < FumoJogador.PuxaAte + 0.2f ? 1f : 0f;
                    _luz.transform.position = chama ? _chama.position : _brasa.position;
                    _luz.color = chama ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.45f, 0.15f);
                    _luz.range = chama ? 2f : 1.5f;
                    _luz.intensity = chama ? 0.012f : 0.003f + forte * 0.005f;
                }
            }
        }

        // a baforada: a fumaça saindo da boca, para a frente, no tempo do clipe da tragada
        System.Collections.IEnumerator Baforar()
        {
            yield return new WaitForSeconds(FumoJogador.Baforada);
            if (materialDaFumaca == null || (MaoDoMorador)Mao.Value != MaoDoMorador.Aceso) yield break;
            _baforada ??= FumoJogador.Fumaca(transform, materialDaFumaca, "Baforada", gameObject.layer, true, false);
            var cabeca = Cabeca;
            var boca = (cabeca != null ? cabeca.position : transform.position + Vector3.up * 1.55f) + Vector3.up * 0.04f + transform.forward * 0.13f;
            _baforada.transform.SetPositionAndRotation(boca, Quaternion.LookRotation(transform.forward + Vector3.up * 0.2f));
            _baforada.Emit(14);
        }

        // o peteleco: a bituca sai da mão no tempo do clipe e cai com física, soltando fumaça
        System.Collections.IEnumerator SoltarABituca()
        {
            yield return new WaitForSeconds(FumoJogador.Solta);
            if (_cigarro == null || malhaDaBituca == null) yield break;
            var vel = transform.forward * 2.0f + Vector3.up * 1.4f + transform.right * UnityEngine.Random.Range(-0.4f, 0.4f);
            FumoJogador.Bituca(this, malhaDaBituca, materialDaBituca, materialDaFumaca, _cigarro.position, UnityEngine.Random.rotation, vel, GetComponent<Collider>());
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            APonte();
            OTaco();
            OFumo();
            Curvar();
        }

        // o velho anda curvado (a decisão do plano: o andar de hoje, mais devagar e curvado, no código), só em pé
        void Curvar()
        {
            if (!Quem.Value.velho || _coluna == null) return;
            if (AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(Acao.Value)) != PosturaDoCorpo.EmPe) return;
            // (a tacada já se curva na mesa: curvado por cima, as mãos desciam para dentro dela)
            var a = Animador;
            if (a != null && a.GetCurrentAnimatorStateInfo(0).IsName("Sinuca_Tacada")) return;
            var eixo = transform.right;
            _coluna.rotation = Quaternion.AngleAxis(Curvado * 0.6f, eixo) * _coluna.rotation;
            if (_peito != null) _peito.rotation = Quaternion.AngleAxis(Curvado * 0.4f, eixo) * _peito.rotation;
        }

        // ------------------------------------------------------------------ no anfitrião: andar e se acomodar

        Coroutine _acomodando;

        /// <summary>No anfitrião: o corpo está se acomodando no lugar (deslizando até a raiz).</summary>
        public bool Acomodando => _acomodando != null;

        // o último destino pedido ao agente (o que o morador quer, não o do NavMesh)
        Vector3 _pedido;
        bool _temPedido;

        /// <summary>No anfitrião: quantos caminhos o agente pediu (para conferir que ele não pede um a cada quadro).</summary>
        public int CaminhosPedidos { get; private set; }

        /// <summary>No anfitrião: liga o agente no NavMesh (até 1,5 m da raiz); falso se não há NavMesh ali.</summary>
        public bool LigarOAgente()
        {
            if (agente == null) return false;
            if (agente.enabled) return true;
            if (!NoChao(transform.position, out var ponto)) return false;
            agente.enabled = true;
            agente.Warp(ponto);
            // cada um com a sua vez nos corredores (quem tem a prioridade menor passa; antes, todos iguais se empurravam)
            agente.avoidancePriority = 30 + Mathf.Abs(Quem.Value.id * 7) % 40;
            _temPedido = false;
            return true;
        }

        /// <summary>
        /// O ponto do NavMesh no chão de onde ele está (até 1,5 m). O NavMesh também cobre o alto dos móveis (o assento do
        /// sofá, a mesa: ilhas soltas), e o mais perto de quem levantava do sofá, com a mesa de centro na frente, era o
        /// assento: ele ia para cima do sofá e ficava lá travado (07/11/2026). Agora o ponto fica na altura dos pés (até
        /// 30 cm), procurado em volta; sem nenhum, o mais perto de qualquer altura, como antes.
        /// </summary>
        public static bool NoChao(Vector3 p, out Vector3 ponto)
        {
            const float Altura = 0.3f;
            ponto = p;
            if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < Altura)
            {
                ponto = hit.position;
                return true;
            }
            float melhor = float.MaxValue;
            for (int anel = 1; anel <= 4; anel++)
                for (int k = 0; k < 12; k++)
                {
                    float a = (k + (anel & 1) * 0.5f) * 30f * Mathf.Deg2Rad, r = anel * 0.35f;
                    var q = p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!NavMesh.SamplePosition(q, out var h, 0.3f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - p.y) >= Altura) continue;
                    float d = (h.position - p).sqrMagnitude;
                    if (d < melhor)
                    {
                        melhor = d;
                        ponto = h.position;
                    }
                }
            if (melhor < float.MaxValue) return true;
            if (!NavMesh.SamplePosition(p, out hit, 1.5f, NavMesh.AllAreas)) return false;
            ponto = hit.position;
            return true;
        }

        /// <summary>
        /// No anfitrião: anda até o ponto (no mundo), na rapidez dele. O caminho só é pedido de novo quando o ponto muda (ou o
        /// agente o perdeu longe dele): o lugar fica perto do móvel, uns centímetros fora do NavMesh, e o destino do agente é o
        /// ponto do NavMesh mais perto; comparando com ele, o caminho era pedido a cada quadro e nunca ficava pronto (o morador
        /// travava, quase sem sair do lugar).
        /// </summary>
        public void Ir(Vector3 mundo, float velocidade)
        {
            if (!IsServer || agente == null || (!agente.enabled && !LigarOAgente())) return;
            agente.speed = velocidade;
            agente.stoppingDistance = 0.15f;
            agente.autoBraking = true;
            bool mudou = !_temPedido || (_pedido - mundo).sqrMagnitude > 0.04f;
            bool perdeu = !agente.pathPending && !agente.hasPath && Plano(transform.position - mundo) > 1.5f;
            if (!mudou && !perdeu) return;
            agente.SetDestination(mundo);
            _pedido = mundo;
            _temPedido = true;
            CaminhosPedidos++;
        }

        static float Plano(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        /// <summary>
        /// No anfitrião: chegou perto do ponto (no plano), ou o agente chegou ao fim do caminho perto dele (o lugar fica fora do
        /// NavMesh, perto do móvel: o fim é o ponto do NavMesh mais perto, até uns 2 m; o resto ele desliza no acomodar).
        /// </summary>
        public bool Chegou(Vector3 mundo, float folga = 0.45f)
        {
            float d = Plano(transform.position - mundo);
            bool mesmoAndar = Mathf.Abs(transform.position.y - mundo.y) < 1.2f;
            if (d < folga && mesmoAndar) return true;
            return agente != null && agente.enabled && _temPedido && !agente.pathPending && agente.remainingDistance <= agente.stoppingDistance + 0.1f
                   && d < 2f && mesmoAndar;
        }

        /// <summary>No anfitrião: o agente não consegue chegar (o caminho é parcial ou inválido).</summary>
        public bool SemCaminho => agente != null && agente.enabled && !agente.pathPending && agente.pathStatus == NavMeshPathStatus.PathInvalid;

        /// <summary>No anfitrião: para o agente e desliza até a raiz do lugar (no mundo), com o giro dele; depois, a ação.</summary>
        public void Acomodar(Vector3 raiz, float giro, byte acao)
        {
            if (!IsServer) return;
            if (_acomodando != null) StopCoroutine(_acomodando);
            _acomodando = StartCoroutine(Deslizar(raiz, Quaternion.Euler(0f, giro, 0f), acao));
        }

        System.Collections.IEnumerator Deslizar(Vector3 raiz, Quaternion giro, byte acao)
        {
            if (agente != null && agente.enabled)
            {
                agente.ResetPath();
                agente.enabled = false;
            }
            _temPedido = false;
            Vector3 p0 = transform.position;
            Quaternion r0 = transform.rotation;
            float dura = Mathf.Clamp(Vector3.Distance(p0, raiz) / 1.2f + Quaternion.Angle(r0, giro) / 360f, 0.15f, 0.8f);
            for (float t = 0f; t < dura; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dura);
                transform.SetPositionAndRotation(Vector3.Lerp(p0, raiz, k), Quaternion.Slerp(r0, giro, k));
                yield return null;
            }
            transform.SetPositionAndRotation(raiz, giro);
            Acao.Value = acao;
            _acomodando = null;
        }

        /// <summary>No anfitrião: põe o corpo no lugar já acomodado (o corpo novo de quem estava sentado no papel).</summary>
        public void JaNoLugar(Vector3 raiz, float giro, byte acao)
        {
            if (agente != null) agente.enabled = false;
            transform.SetPositionAndRotation(raiz, Quaternion.Euler(0f, giro, 0f));
            Acao.Value = acao;
        }

        /// <summary>No anfitrião: levanta (a ação vira andar; o agente liga quando o levantar acabar: <see cref="EmPe"/>).</summary>
        public void Levantar()
        {
            if (!IsServer) return;
            if (_acomodando != null)
            {
                StopCoroutine(_acomodando);
                _acomodando = null;
            }
            Acao.Value = (byte)AcaoDoCorpo.Andando;
        }
    }
}
