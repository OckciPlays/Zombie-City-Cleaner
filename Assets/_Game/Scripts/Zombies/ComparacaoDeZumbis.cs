using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A cena de comparação dos especiais (04/11/2026, a pedido): Scenes/Exemplos/Comparacao_Zumbis.unity, montada pelo menu
    /// Zombie City Cleaner > Zumbis > Montar a cena de comparação (atual x novo) (Editor/MontagemDosZumbis.Novos.cs). Os prefabs
    /// do jogo (Zumbi_Brutamonte_V1..V3 e Zumbi_NovoBrutamonte; Zumbi_Corredor_V1..V3 e Zumbi_NovoCorredor) ficam lado a lado,
    /// em duas fileiras, sem ZombieManager e sem rede: o Zumbi não tem ciclo próprio (quem o move é o ZombieManager), então só
    /// o Animator roda; o Zumbi e o agente ficam desligados, por garantia.
    /// <para>
    /// No Play, o painel (IMGUI, à esquerda; H esconde) manda em todos os Animators da cena de uma vez (em sincronia, todos
    /// sempre animados): o movimento (Parado, Andar, Perseguir pela "Speed"; Correr, com "Pernas" em 7, o andar dos especiais;
    /// Mancar, Cair e Rastejar por "Pernas"), as ações (Atacar e Tiro, os gatilhos; Levantar, "Pernas" em 5; Emergir, Fúria,
    /// Gritar e Cuspir, com "Pernas" em 6 e CrossFadeInFixedTime, como o Zumbi faz: as de uma vez voltam ao movimento ao
    /// acabar), a agonia do tronco, a velocidade e a ressincronia. Cada Brutamonte ganha o <see cref="OlhoDoBrutamonte"/>
    /// (automático, aberto ou fechado; com a mesma semente, os olhos piscam juntos).
    /// </para>
    /// <para>
    /// A câmera: o foco (o par atual × novo de cada fileira, a fileira toda ou tudo; as outras fileiras somem), as vistas
    /// (Frente, Costas, Lado: os modelos giram no lugar e a câmera fica na frente) e o olho de perto (atrás de um Brutamonte,
    /// seguindo o osso do olho). O mouse fora do painel gira (botão esquerdo ou direito), arrasta (o do meio) e aproxima (a
    /// roda); WASD anda, Q/E desce e sobe, Shift acelera, F volta ao enquadramento.
    /// </para>
    /// </summary>
    public class ComparacaoDeZumbis : MonoBehaviour
    {
        /// <summary>Uma fileira: os atuais (o V1 primeiro, o par do novo), o novo (sem o prefab, o lugar vazio dele) e os rótulos.</summary>
        [System.Serializable]
        public class Fileira
        {
            public string nome;
            [Tooltip("A raiz da fileira: os modelos, os rótulos e a régua")]
            public Transform raiz;
            [Tooltip("Os modelos atuais, o V1 primeiro (o par do novo)")]
            public Transform[] atuais = new Transform[0];
            [Tooltip("O modelo novo (sem o prefab, o lugar vazio dele)")]
            public Transform novo;
            [Tooltip("Os textos 3D (viram para a câmera)")]
            public Transform[] rotulos = new Transform[0];
        }

        public Fileira[] fileiras = new Fileira[0];
        [Tooltip("Vazia: a câmera principal")]
        public Camera cameraDaCena;

        public enum Movimento { Parado, Andar, Perseguir, Correr, Mancar, Cair, Rastejar }
        static readonly string[] NomesDosMovimentos = { "Parado", "Andar", "Perseguir", "Correr", "Mancar", "Cair", "Rastejar" };
        enum Vista { Frente, Costas, LadoE, LadoD }

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int PernasId = Animator.StringToHash("Pernas");
        static readonly int AtacarId = Animator.StringToHash("Atacar");
        static readonly int TiroId = Animator.StringToHash("Tiro");
        static readonly int AgoniaId = Animator.StringToHash("Agonia");
        static readonly int LevantarId = Animator.StringToHash("Levantar");
        static readonly int LevantandoE = Animator.StringToHash("LevantandoE");
        // as ações do corpo inteiro, sem transição no Zombie.controller: o código entra nelas com "Pernas" em 6
        static readonly (string nome, int estado)[] AcoesDoCorpo =
        {
            ("Emergir", Animator.StringToHash("Emergir")), ("Fúria", Animator.StringToHash("Furia")),
            ("Gritar", Animator.StringToHash("Gritar")), ("Cuspir", Animator.StringToHash("Cuspir")),
        };
        static readonly string[] NomesDaAgonia = { "Nenhuma", "Rosto", "Pele", "Encolher" };
        const int CamadaAcoes = 1;   // "Ações" (o tronco): o ataque, o tiro e a agonia

        class Modelo
        {
            public string nome, curto;
            public Transform raiz;
            public Quaternion giro0;
            public OlhoDoBrutamonte olho;
            public float altura;
            public int triangulos, fileira;
            public bool novo, vazio;
        }

        class Animado
        {
            public Animator a;
            public int acao;   // a ação de uma vez que ele está tocando (0: nenhuma)
        }

        readonly List<Modelo> _modelos = new List<Modelo>();
        readonly List<Modelo> _comOlho = new List<Modelo>();
        readonly List<Modelo> _quadro = new List<Modelo>();
        readonly List<Animado> _animados = new List<Animado>();
        readonly List<(int fileira, bool par)> _focos = new List<(int, bool)>();
        string[] _nomesDosFocos = new string[0];
        Renderer[][] _renderersDaFileira = new Renderer[0][];

        Movimento _movimento;
        int _agonia, _foco;
        float _velocidade = 1f;
        bool _pausado, _girar, _olhoSegue = true, _mostrarPainel = true;
        OlhoDoBrutamonte.ModoDoOlho _modoDoOlho;
        string _ultimo = "Parado";
        float _giroDosModelos, _giroAlvo;

        // a câmera: o pivô, o giro, a inclinação e a distância (o atual vai atrás do alvo, suave)
        Vector3 _pivo, _pivoAlvo;
        float _yaw = 180f, _yawAlvo = 180f, _pitch = 4f, _pitchAlvo = 4f, _dist = 6f, _distAlvo = 6f;
        Transform _seguir;
        bool _arrastando;
        Rect _painelNaTela;
        Vector2 _rolagem;
        GUIStyle _estilo;

        void Start()
        {
            if (cameraDaCena == null) cameraDaCena = Camera.main;
            if (cameraDaCena == null)
            {
                Debug.LogError("Comparação de zumbis: a cena não tem câmera (monte a cena de novo).", this);
                enabled = false;
                return;
            }
            // a neblina de altura do renderizador (PC_Renderer) lê valores globais que o AmbienteDoDia escreve: sem ele,
            // ficariam os do último Play (sem domain reload). Densidade zero: sem neblina
            Shader.SetGlobalVector("_NeblinaParams", Vector4.zero);

            for (int f = 0; f < fileiras.Length; f++)
            {
                var fi = fileiras[f];
                if (fi == null) continue;
                if (fi.atuais != null) foreach (var t in fi.atuais) Adicionar(t, f, false);
                Adicionar(fi.novo, f, true);
            }
            foreach (var a in FindObjectsByType<Animator>())
            {
                if (!TemParametro(a, PernasId)) continue;   // só os dos zumbis (o Zombie.controller e os override dele)
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // fora da câmera também: a sincronia não se perde
                _animados.Add(new Animado { a = a });
            }
            if (_animados.Count == 0) Debug.LogWarning("Comparação de zumbis: nenhum zumbi com Animator na cena (rode Zumbis > Montar a cena de comparação).", this);

            _renderersDaFileira = new Renderer[fileiras.Length][];
            for (int f = 0; f < fileiras.Length; f++)
                _renderersDaFileira[f] = fileiras[f] != null && fileiras[f].raiz != null ? fileiras[f].raiz.GetComponentsInChildren<Renderer>(true) : new Renderer[0];

            MontarFocos();
            Mover(Movimento.Parado);
            Focar(0, true);
        }

        void Adicionar(Transform raiz, int fileira, bool novo)
        {
            if (raiz == null) return;
            var m = new Modelo { nome = raiz.name, raiz = raiz, giro0 = raiz.localRotation, fileira = fileira, novo = novo };
            int i = m.nome.LastIndexOf('_');
            m.curto = novo ? "Novo" : i >= 0 ? m.nome.Substring(i + 1) : m.nome;
            m.vazio = raiz.GetComponentInChildren<Animator>(true) == null;
            // o jogo fica de fora (sem ZombieManager e sem rede)
            foreach (var z in raiz.GetComponentsInChildren<Zumbi>(true)) z.enabled = false;
            foreach (var ag in raiz.GetComponentsInChildren<NavMeshAgent>(true)) ag.enabled = false;
            var capsula = raiz.GetComponent<CapsuleCollider>();
            m.altura = capsula != null ? capsula.height : 0f;
            foreach (var s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (s.sharedMesh == null) continue;
                for (int k = 0; k < s.sharedMesh.subMeshCount; k++) m.triangulos += (int)(s.sharedMesh.GetIndexCount(k) / 3);
            }
            var zumbi = raiz.GetComponent<Zumbi>();
            bool brutamonte = zumbi != null ? zumbi.tipo == TipoDeZumbi.Brutamonte : AcharFilho(raiz, "olho") != null;
            if (brutamonte && !m.vazio)
            {
                var olho = raiz.GetComponent<OlhoDoBrutamonte>();
                if (olho == null) olho = raiz.gameObject.AddComponent<OlhoDoBrutamonte>();   // (o Awake acha os ossos e o repouso)
                if (olho.Pronto)
                {
                    olho.modo = _modoDoOlho;
                    olho.seguirAlvo = _olhoSegue;
                    m.olho = olho;
                    _comOlho.Add(m);
                }
            }
            _modelos.Add(m);
        }

        static Transform AcharFilho(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }

        static bool TemParametro(Animator a, int id)
        {
            if (a.runtimeAnimatorController == null) return false;
            foreach (var p in a.parameters)
                if (p.nameHash == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ as animações (todas de uma vez)

        void Mover(Movimento mv)
        {
            _movimento = mv;
            foreach (var x in _animados)
            {
                x.acao = 0;
                MoverNo(x.a, mv);
            }
            _ultimo = NomesDosMovimentos[(int)mv];
        }

        static void MoverNo(Animator a, Movimento mv)
        {
            if (a == null || !a.isActiveAndEnabled) return;
            int pernas = mv switch { Movimento.Correr => 7, Movimento.Mancar => 1, Movimento.Cair => 3, Movimento.Rastejar => 4, _ => 0 };
            float velocidade = mv switch
            {
                Movimento.Andar => 0.6f, Movimento.Perseguir => 1.12f, Movimento.Correr => 3.8f,
                Movimento.Mancar => 0.45f, Movimento.Rastejar => 0.35f, _ => 0f,
            };
            a.SetFloat(SpeedId, velocidade);
            a.SetInteger(PernasId, pernas);
            Tronco(a, pernas < 3);   // caído, o tronco não ataca nem reage (como o Zumbi)
        }

        static void Tronco(Animator a, bool ligado)
        {
            if (a.layerCount > CamadaAcoes) a.SetLayerWeight(CamadaAcoes, ligado ? 1f : 0f);
        }

        /// <summary>Uma ação da camada base sem transição: "Pernas" no valor dela e o Animator entra no estado.</summary>
        void Acao(string nome, int estado, int pernas, float mistura)
        {
            int tocaram = 0;
            foreach (var x in _animados)
            {
                var a = x.a;
                if (a == null || !a.isActiveAndEnabled || !a.HasState(0, estado)) continue;
                a.SetInteger(PernasId, pernas);
                if (estado == LevantandoE) a.SetFloat(LevantarId, 1f);
                Tronco(a, pernas != 5);
                a.CrossFadeInFixedTime(estado, mistura, 0, 0f);
                x.acao = estado;
                tocaram++;
            }
            _ultimo = tocaram > 0 ? nome : $"{nome} (nenhum tem o estado)";
        }

        void Disparar(int gatilho, string nome)
        {
            foreach (var x in _animados)
                if (x.a != null && x.a.isActiveAndEnabled) x.a.SetTrigger(gatilho);
            _ultimo = nome;
        }

        void Agonia(int qual)
        {
            _agonia = qual;
            foreach (var x in _animados)
                if (x.a != null && x.a.isActiveAndEnabled) x.a.SetInteger(AgoniaId, qual);
            _ultimo = qual == 0 ? "Sem agonia" : $"Agonia: {NomesDaAgonia[qual]}";
        }

        /// <summary>Todos voltam ao começo do estado atual (cada camada), e os olhos ao começo do ciclo.</summary>
        void Ressincronizar()
        {
            foreach (var x in _animados)
            {
                var a = x.a;
                if (a == null || !a.isActiveAndEnabled) continue;
                for (int c = 0; c < a.layerCount; c++) a.Play(a.GetCurrentAnimatorStateInfo(c).fullPathHash, c, 0f);
            }
            foreach (var m in _comOlho) m.olho.Reiniciar(1);
            _ultimo += " (ressincronizado)";
        }

        void ModoDoOlho(OlhoDoBrutamonte.ModoDoOlho modo)
        {
            _modoDoOlho = modo;
            foreach (var m in _comOlho)
            {
                m.olho.modo = modo;
                if (modo == OlhoDoBrutamonte.ModoDoOlho.Automatico) m.olho.Reiniciar(1);   // a mesma semente: piscam juntos
            }
        }

        void Update()
        {
            // as ações de uma vez (Levantar, Emergir, Gritar, Cuspir): acabando, cada Animator volta ao movimento escolhido
            foreach (var x in _animados)
            {
                var a = x.a;
                if (x.acao == 0 || a == null || !a.isActiveAndEnabled || a.IsInTransition(0)) continue;
                var info = a.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != x.acao || info.loop || info.normalizedTime < 1f) continue;
                if (x.acao == LevantandoE && _movimento is Movimento.Cair or Movimento.Rastejar) _movimento = Movimento.Parado;
                x.acao = 0;
                MoverNo(a, _movimento);
            }
            float velocidade = _pausado ? 0f : _velocidade;
            foreach (var x in _animados)
                if (x.a != null) x.a.speed = velocidade;

            // os modelos giram no lugar (as vistas e o "girar")
            if (_girar) _giroAlvo = Mathf.Repeat(_giroAlvo + 30f * Time.deltaTime, 360f);
            _giroDosModelos = _girar ? _giroAlvo : Mathf.MoveTowardsAngle(_giroDosModelos, _giroAlvo, 400f * Time.deltaTime);
            var giro = Quaternion.Euler(0f, _giroDosModelos, 0f);
            foreach (var m in _modelos)
                if (m.raiz != null) m.raiz.localRotation = m.giro0 * giro;
        }

        // ------------------------------------------------------------------ a câmera

        void MontarFocos()
        {
            _focos.Clear();
            var nomes = new List<string>();
            for (int f = 0; f < fileiras.Length; f++)
            {
                if (fileiras[f] == null) continue;
                _focos.Add((f, true));
                nomes.Add($"{fileiras[f].nome}: atual × novo");
                _focos.Add((f, false));
                nomes.Add($"{fileiras[f].nome}: todos");
            }
            _focos.Add((-1, false));
            nomes.Add("Tudo");
            _nomesDosFocos = nomes.ToArray();
        }

        void Focar(int qual, bool imediato)
        {
            _foco = Mathf.Clamp(qual, 0, _focos.Count - 1);
            _seguir = null;
            var (f, par) = _focos[_foco];
            MostrarFileiras(f);
            _quadro.Clear();
            foreach (var m in _modelos)
            {
                if (f >= 0 && m.fileira != f) continue;
                var atuais = fileiras[m.fileira].atuais;
                if (par && !m.novo && (atuais == null || atuais.Length == 0 || atuais[0] != m.raiz)) continue;
                _quadro.Add(m);
            }
            if (_quadro.Count == 0) return;
            Vector3 min = _quadro[0].raiz.position, max = min;
            float altura = 0f;
            foreach (var m in _quadro)
            {
                min = Vector3.Min(min, m.raiz.position);
                max = Vector3.Max(max, m.raiz.position);
                altura = Mathf.Max(altura, m.altura > 0f ? m.altura : 2.2f);
            }
            float meioV = Mathf.Tan(cameraDaCena.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float meioH = meioV * Mathf.Max(0.5f, cameraDaCena.aspect);
            float largura = max.x - min.x, fundo = max.z - min.z;   // as fileiras correm em X, uma atrás da outra em Z
            float dist = Mathf.Max((largura * 0.5f + 1f) / meioH, (altura * 0.5f + 0.35f) / meioV, 2.5f);
            bool deCima = f < 0;
            if (deCima) dist = Mathf.Max(dist, (fundo * 0.5f + altura) / meioV);
            _pivoAlvo = new Vector3((min.x + max.x) * 0.5f, altura * (deCima ? 0.3f : 0.5f), (min.z + max.z) * 0.5f);
            _yawAlvo = 180f;   // na frente das fileiras (os modelos olham para +Z)
            _pitchAlvo = deCima ? 30f : 4f;
            _distAlvo = dist;
            if (imediato) Cortar();
        }

        void Cortar()
        {
            _pivo = _pivoAlvo;
            _yaw = _yawAlvo;
            _pitch = _pitchAlvo;
            _dist = _distAlvo;
        }

        /// <summary>Só a fileira do foco aparece (-1: todas). Os escondidos continuam animando (a sincronia).</summary>
        void MostrarFileiras(int f)
        {
            for (int i = 0; i < _renderersDaFileira.Length; i++)
                foreach (var r in _renderersDaFileira[i])
                    if (r != null) r.enabled = f < 0 || i == f;
        }

        void Ver(Vista v)
        {
            _girar = false;
            _giroAlvo = v switch { Vista.Costas => 180f, Vista.LadoE => 90f, Vista.LadoD => -90f, _ => 0f };
            if (_seguir != null) Focar(_foco, false);   // saindo do olho de perto: volta ao enquadramento
            _ultimo = v switch { Vista.Costas => "Vista: costas", Vista.LadoE => "Vista: lado esquerdo", Vista.LadoD => "Vista: lado direito", _ => "Vista: frente" };
        }

        /// <summary>O olho de perto: a câmera atrás do Brutamonte (o olho fica nas costas), seguindo o osso do olho.</summary>
        void VerOlho(Modelo m)
        {
            if (m?.olho == null) return;
            _girar = false;
            MostrarFileiras(m.fileira);
            _seguir = m.olho.olho;
            var pai = m.raiz.parent != null ? m.raiz.parent.rotation : Quaternion.identity;
            _yawAlvo = (pai * m.giro0 * Quaternion.Euler(0f, _giroAlvo, 0f)).eulerAngles.y;   // atrás dele (como ele vai ficar)
            _pitchAlvo = 6f;
            _distAlvo = 0.75f;
            _pivoAlvo = _seguir.position;
            _ultimo = $"Olho de perto: {m.nome}";
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Controles(dt);
            if (_seguir != null) _pivoAlvo = _seguir.position;
            float k = 1f - Mathf.Exp(-10f * dt);
            _pivo = Vector3.Lerp(_pivo, _pivoAlvo, _seguir != null ? 1f - Mathf.Exp(-25f * dt) : k);
            _yaw = Mathf.LerpAngle(_yaw, _yawAlvo, k);
            _pitch = Mathf.Lerp(_pitch, _pitchAlvo, k);
            _dist = Mathf.Lerp(_dist, _distAlvo, k);
            var giro = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 posicao = _pivo + giro * new Vector3(0f, 0f, -_dist);
            cameraDaCena.transform.SetPositionAndRotation(posicao, giro);

            // os rótulos de frente para a câmera (em pé)
            foreach (var fi in fileiras)
            {
                if (fi?.rotulos == null) continue;
                foreach (var r in fi.rotulos)
                {
                    if (r == null) continue;
                    Vector3 d = r.position - posicao;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) r.rotation = Quaternion.LookRotation(d);
                }
            }
        }

        void Controles(float dt)
        {
            var teclado = Keyboard.current;
            var mouse = Mouse.current;
            if (teclado != null)
            {
                if (teclado.hKey.wasPressedThisFrame) _mostrarPainel = !_mostrarPainel;
                if (teclado.fKey.wasPressedThisFrame) Focar(_foco, false);
                Vector3 mover = Vector3.zero;
                if (teclado.wKey.isPressed) mover.z += 1f;
                if (teclado.sKey.isPressed) mover.z -= 1f;
                if (teclado.dKey.isPressed) mover.x += 1f;
                if (teclado.aKey.isPressed) mover.x -= 1f;
                if (teclado.eKey.isPressed) mover.y += 1f;
                if (teclado.qKey.isPressed) mover.y -= 1f;
                if (mover != Vector3.zero)
                {
                    float v = 2.5f * (teclado.leftShiftKey.isPressed ? 3f : 1f) * Mathf.Max(0.3f, _distAlvo / 5f);
                    var plano = Quaternion.Euler(0f, _yawAlvo, 0f);
                    _pivoAlvo += (plano * new Vector3(mover.x, 0f, mover.z) + Vector3.up * mover.y) * (v * dt);
                    _seguir = null;
                }
            }
            if (mouse == null) return;
            Vector2 ponto = mouse.position.ReadValue();
            bool sobre = _painelNaTela.Contains(new Vector2(ponto.x, Screen.height - ponto.y));
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                _arrastando = !sobre;   // começou no painel: é clique nele, não giro
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed) _arrastando = false;
            Vector2 d = mouse.delta.ReadValue();
            if (_arrastando)
            {
                if (mouse.middleButton.isPressed)
                {
                    var giro = Quaternion.Euler(_pitch, _yaw, 0f);
                    _pivoAlvo -= (giro * Vector3.right * d.x + giro * Vector3.up * d.y) * (_distAlvo * 0.0015f);
                    _seguir = null;
                }
                else
                {
                    _yawAlvo += d.x * 0.25f;
                    _pitchAlvo = Mathf.Clamp(_pitchAlvo - d.y * 0.2f, -30f, 85f);
                }
            }
            float roda = mouse.scroll.ReadValue().y;
            if (!sobre && Mathf.Abs(roda) > 0.01f) _distAlvo = Mathf.Clamp(_distAlvo * (roda > 0f ? 0.88f : 1.14f), 0.25f, 60f);
        }

        // ------------------------------------------------------------------ o painel

        void OnGUI()
        {
            float e = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(e, e, 1f));
            _estilo ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            if (!_mostrarPainel)
            {
                _painelNaTela = Rect.zero;
                GUI.Label(new Rect(10f, 10f, 400f, 24f), "<b>H</b>: mostrar o painel", _estilo);
                return;
            }
            var r = new Rect(10f, 10f, 350f, Screen.height / e - 20f);
            _painelNaTela = new Rect(r.x * e, r.y * e, r.width * e, r.height * e);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);   // (duas vezes: mais escuro)
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));
            _rolagem = GUILayout.BeginScrollView(_rolagem);

            GUILayout.Label("<size=15><b>Zumbis: atual × novo</b></size>", _estilo);
            GUILayout.Label($"Agora: <b>{_ultimo}</b>", _estilo);

            Titulo("Movimento");
            int c = Grade(NomesDosMovimentos, (int)_movimento, 4);
            if (c >= 0) Mover((Movimento)c);

            Titulo("Ações");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Atacar")) Disparar(AtacarId, "Atacar");
            if (GUILayout.Button("Tiro (reação)")) Disparar(TiroId, "Reação ao tiro");
            if (GUILayout.Button("Levantar")) Acao("Levantar", LevantandoE, 5, 0.25f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var (nome, estado) in AcoesDoCorpo)
                if (GUILayout.Button(nome)) Acao(nome, estado, 6, 0.12f);
            GUILayout.EndHorizontal();

            Titulo("Agonia (tronco)");
            c = Grade(NomesDaAgonia, _agonia, 4);
            if (c >= 0) Agonia(c);

            Titulo($"Velocidade: {_velocidade:0.00}×");
            _velocidade = GUILayout.HorizontalSlider(_velocidade, 0f, 2f);
            GUILayout.BeginHorizontal();
            _pausado = GUILayout.Toggle(_pausado, "Pausar", GUI.skin.button);
            if (GUILayout.Button("1×")) _velocidade = 1f;
            if (GUILayout.Button("Ressincronizar")) Ressincronizar();
            GUILayout.EndHorizontal();

            if (_comOlho.Count > 0)
            {
                Titulo("Olho (Brutamontes)");
                c = Grade(OlhoDoBrutamonte.NomesDosModos, (int)_modoDoOlho, 3);
                if (c >= 0) ModoDoOlho((OlhoDoBrutamonte.ModoDoOlho)c);
                bool segue = GUILayout.Toggle(_olhoSegue, " Segue a câmera (senão, olha em volta)");
                if (segue != _olhoSegue)
                {
                    _olhoSegue = segue;
                    foreach (var m in _comOlho) m.olho.seguirAlvo = segue;
                }
                var aberturas = new System.Text.StringBuilder("Abertura: ");
                foreach (var m in _comOlho) aberturas.Append($"{m.curto} {m.olho.Abertura:0.00}   ");
                GUILayout.Label(aberturas.ToString(), _estilo);
            }

            Titulo("Câmera");
            c = Grade(_nomesDosFocos, _seguir != null ? -1 : _foco, 2);
            if (c >= 0) Focar(c, false);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Frente")) Ver(Vista.Frente);
            if (GUILayout.Button("Costas")) Ver(Vista.Costas);
            if (GUILayout.Button("Lado E")) Ver(Vista.LadoE);
            if (GUILayout.Button("Lado D")) Ver(Vista.LadoD);
            GUILayout.EndHorizontal();
            _girar = GUILayout.Toggle(_girar, " Girar os modelos");
            if (_comOlho.Count > 0)
            {
                GUILayout.Label("Olho de perto (atrás dele):", _estilo);
                GUILayout.BeginHorizontal();
                foreach (var m in _comOlho)
                    if (GUILayout.Toggle(_seguir == m.olho.olho, m.curto, GUI.skin.button) && _seguir != m.olho.olho) VerOlho(m);
                GUILayout.EndHorizontal();
            }

            Titulo("Modelos");
            foreach (var m in _modelos)
                GUILayout.Label(m.vazio
                    ? $"{m.nome}: <color=#ff8a80>falta o prefab</color>"
                    : $"{m.nome}{(m.novo ? " <color=#ffd866>(novo)</color>" : "")}: {m.altura:0.00} m · {m.triangulos / 1000f:0.0} mil triângulos", _estilo);

            GUILayout.Space(8f);
            GUILayout.Label("<color=#bbbbbb>Mouse fora do painel: o botão esquerdo ou o direito gira, o do meio arrasta, a roda " +
                            "aproxima. WASD anda, Q/E desce e sobe, Shift acelera. F volta ao enquadramento; H esconde o painel.</color>", _estilo);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Titulo(string texto)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"<b>{texto}</b>", _estilo);
        }

        /// <summary>Uma grade de botões (o escolhido aceso); devolve o clicado (também o já escolhido) ou -1.</summary>
        static int Grade(string[] nomes, int escolhido, int colunas)
        {
            int clicado = -1;
            for (int i = 0; i < nomes.Length; i++)
            {
                if (i % colunas == 0) GUILayout.BeginHorizontal();
                bool aceso = i == escolhido;
                if (GUILayout.Toggle(aceso, nomes[i], GUI.skin.button) != aceso) clicado = i;
                if (i % colunas == colunas - 1 || i == nomes.Length - 1) GUILayout.EndHorizontal();
            }
            return clicado;
        }
    }
}
