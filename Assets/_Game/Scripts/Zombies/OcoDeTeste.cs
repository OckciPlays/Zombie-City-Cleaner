using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Oco de teste (a pedido em 27/10/2026, para ver o modelo e as animações): posto pelo F1, só nesta máquina, sem rede
    /// e sem dano. Não entra no <see cref="ZombieManager"/>: não é contado, não ouve tiro e não morre. O modelo é o do
    /// Blender (Art/Characters/Oco/Variantes), com as animações dos zumbis (as mesmas do ZombieGaroto, importadas como Humanoid).
    /// Em "Seguir", ele vem atrás do jogador local (pelo NavMesh, se houver; senão, em linha reta) e, a 1,3 m, faz o golpe
    /// sem tirar vida. Nos outros modos, fica no lugar, virado para o jogador, repetindo a animação escolhida.
    /// Os prefabs são montados pelo menu Zombie City Cleaner > Zumbis > Montar as versões do Oco (teste) (Resources/Oco_*).
    /// O Oco de teste antigo, de ~125 mil triângulos (Oco_Teste), saiu do jogo a pedido em 28/10/2026 (pesado demais).
    /// </summary>
    public class OcoDeTeste : MonoBehaviour
    {
        public enum Modo { SeguirCorrendo, SeguirAndando, Parado, Andar, Correr, Atacar, Reacao, Cair, Rastejar, Mancar }

        /// <summary>Os nomes dos modos, na ordem do enum (os botões do F1).</summary>
        public static readonly string[] NomesDosModos =
        {
            "Seguir correndo", "Seguir andando", "Parado", "Andar (no lugar)", "Correr (no lugar)",
            "Atacar (sem dano)", "Reação ao tiro", "Cair", "Rastejar", "Mancar",
        };

        /// <summary>
        /// As versões que o F1 põe (a pedido em 28/10/2026): o prefab (em Resources) e o nome do botão, ~5 mil triângulos
        /// cada, montadas pelo menu Zombie City Cleaner > Zumbis > Montar as versões do Oco (teste)
        /// (Editor/MontagemDasVariantesDoOco). Desde 29/10/2026, também o original com ~2 mil e ~1 mil triângulos (teste
        /// de quanto dá para tirar: a roupa vira parte da casca do corpo e os detalhes ficam só na textura).
        /// </summary>
        public static readonly (string prefab, string nome)[] Versoes =
        {
            ("Oco_00_Original", "Original 5k"),
            ("Oco_00_Original_2k", "Original 2k"),
            ("Oco_00_Original_1k", "Original 1k"),
            ("Oco_01_O_Observador", "01 Observador"),
            ("Oco_02_O_Desviante", "02 Desviante"),
            ("Oco_03_O_Residuo", "03 Resíduo"),
            ("Oco_04_A_Enfermeira", "04 Enfermeira"),
            ("Oco_05_O_Mecanico", "05 Mecânico"),
            ("Oco_06_O_Estudante", "06 Estudante"),
            ("Oco_07_O_Executivo", "07 Executivo"),
            ("Oco_08_O_Lenhador", "08 Lenhador"),
            ("Oco_09_A_Corredora", "09 Corredora"),
            ("Oco_10_O_Vigia", "10 Vigia"),
            ("Oco_11_O_Turista", "11 Turista"),
        };

        /// <summary>A versão que o "Pôr um Oco" usa (o índice em <see cref="Versoes"/>).</summary>
        public static int VersaoEscolhida { get; set; }

        /// <summary>Os Ocos postos nesta máquina.</summary>
        public static readonly List<OcoDeTeste> Todos = new List<OcoDeTeste>();
        /// <summary>O modo de todos (o F1 troca de uma vez).</summary>
        public static Modo ModoDeTodos { get; private set; } = Modo.SeguirCorrendo;

        public Animator animador;
        [Tooltip("m/s; a animação de correr foi feita para 1,12")]
        public float velocidadeCorrendo = 1.1f;
        [Tooltip("m/s; a animação de andar foi feita para 0,6")]
        public float velocidadeAndando = 0.55f;
        [Tooltip("De quantos metros ele vem atrás do jogador")]
        public float distanciaDeSeguir = 30f;
        public float alcanceDoGolpe = 1.3f;
        public float intervaloDoGolpe = 1.4f;
        public float giroPorSegundo = 240f;

        // os raios do chão não batem no jogador, nos zumbis e no próprio Oco (a cápsula dele fica na Ignore Raycast, como a
        // dos zumbis de perto: segura o jogador, e os tiros passam)
        int _mascaraDoChao;
        string _estado;
        float _proximaRepeticao;
        NavMeshPath _caminho;   // (criado no Awake: o Unity não deixa no inicializador)
        readonly Vector3[] _cantos = new Vector3[32];
        int _qtdCantos, _canto;
        float _refazerCaminhoEm;

        /// <summary>Cria um Oco da versão escolhida no ponto, olhando para a direção. Null se o prefab ainda não foi montado.</summary>
        public static OcoDeTeste Criar(Vector3 ponto, Vector3 olhando) => Criar(ponto, olhando, VersaoEscolhida);

        /// <summary>Cria um Oco da versão (índice em <see cref="Versoes"/>). Null se o prefab dela ainda não foi montado.</summary>
        public static OcoDeTeste Criar(Vector3 ponto, Vector3 olhando, int versao)
        {
            versao = Mathf.Clamp(versao, 0, Versoes.Length - 1);
            var prefab = Resources.Load<GameObject>(Versoes[versao].prefab);
            if (prefab == null) return null;
            olhando.y = 0f;
            var giro = olhando.sqrMagnitude > 0.001f ? Quaternion.LookRotation(olhando) : Quaternion.identity;
            var oco = Instantiate(prefab, ponto, giro).GetComponent<OcoDeTeste>();
            oco.name = $"Oco (teste) {Versoes[versao].nome}";
            return oco;
        }

        public static void TirarTodos()
        {
            for (int i = Todos.Count - 1; i >= 0; i--) if (Todos[i] != null) Destroy(Todos[i].gameObject);
            Todos.Clear();
        }

        public static void MudarTodos(Modo modo)
        {
            ModoDeTodos = modo;
            foreach (var o in Todos) if (o != null) o.Recomecar();
        }

        void Awake()
        {
            _mascaraDoChao = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << Zumbi.CamadaZumbi) | (1 << CorpoDeZumbi.Camada));
            if (animador == null) animador = GetComponentInChildren<Animator>();
            _caminho = new NavMeshPath();
        }

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);
        void Start() => Recomecar();

        void Recomecar()
        {
            _estado = null;
            _proximaRepeticao = 0f;
            _refazerCaminhoEm = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var jogador = JogadorRede.Local != null ? JogadorRede.Local.transform : null;
            Vector3 paraOJogador = jogador != null ? jogador.position - transform.position : transform.forward;
            paraOJogador.y = 0f;
            float distancia = paraOJogador.magnitude;

            switch (ModoDeTodos)
            {
                case Modo.SeguirCorrendo:
                case Modo.SeguirAndando:
                    bool correndo = ModoDeTodos == Modo.SeguirCorrendo;
                    if (jogador == null || distancia > distanciaDeSeguir) Tocar("Idle");
                    else if (distancia <= alcanceDoGolpe)
                    {
                        Virar(paraOJogador, dt);
                        Repetir("Attack", intervaloDoGolpe);   // só a animação: nenhum dano
                    }
                    else
                    {
                        Tocar(correndo ? "Chase" : "Walk");
                        Andar(jogador.position, correndo ? velocidadeCorrendo : velocidadeAndando, dt);
                    }
                    break;
                case Modo.Parado: Tocar("Idle"); Virar(paraOJogador, dt); break;
                case Modo.Andar: Tocar("Walk"); Virar(paraOJogador, dt); break;
                case Modo.Correr: Tocar("Chase"); Virar(paraOJogador, dt); break;
                case Modo.Atacar: Repetir("Attack", intervaloDoGolpe); Virar(paraOJogador, dt); break;
                case Modo.Reacao: Repetir("HitReact", 1.2f); Virar(paraOJogador, dt); break;
                case Modo.Cair: Tocar("Cair"); break;   // cai uma vez e fica no chão
                case Modo.Rastejar: Tocar("Rastejar"); Virar(paraOJogador, dt); break;
                case Modo.Mancar: Tocar("MancarE"); Virar(paraOJogador, dt); break;
            }
        }

        void Tocar(string estado)
        {
            if (_estado == estado || animador == null) return;
            _estado = estado;
            animador.CrossFadeInFixedTime(estado, 0.2f);
        }

        // as animações de uma vez (o golpe, a reação), de novo a cada intervalo
        void Repetir(string estado, float intervalo)
        {
            if (animador == null) return;
            if (_estado == estado && Time.time < _proximaRepeticao) return;
            _estado = estado;
            _proximaRepeticao = Time.time + intervalo;
            animador.CrossFadeInFixedTime(estado, 0.1f, 0, 0f);
        }

        void Virar(Vector3 direcao, float dt)
        {
            direcao.y = 0f;
            if (direcao.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direcao), giroPorSegundo * dt);
        }

        // vai para o jogador: pelos cantos do caminho do NavMesh, se houver (refeito a cada 0,5 s), ou em linha reta;
        // os pés acompanham o chão por um raio de cima
        void Andar(Vector3 alvo, float velocidade, float dt)
        {
            if (Time.time >= _refazerCaminhoEm)
            {
                _refazerCaminhoEm = Time.time + 0.5f;
                _qtdCantos = 0;
                _canto = 1;
                if (NavMesh.SamplePosition(transform.position, out var daqui, 1.5f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(alvo, out var dali, 2.5f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(daqui.position, dali.position, NavMesh.AllAreas, _caminho) &&
                    _caminho.status != NavMeshPathStatus.PathInvalid)
                    _qtdCantos = _caminho.GetCornersNonAlloc(_cantos);
            }
            Vector3 destino = alvo;
            while (_qtdCantos > 1 && _canto < _qtdCantos)
            {
                var c = _cantos[_canto];
                if (new Vector2(c.x - transform.position.x, c.z - transform.position.z).sqrMagnitude > 0.04f) { destino = c; break; }
                _canto++;
            }
            Vector3 passo = destino - transform.position;
            passo.y = 0f;
            Virar(passo, dt);
            // anda para onde está virado (vira primeiro, depois anda, como os zumbis)
            float alinhado = Mathf.Clamp01(Vector3.Dot(transform.forward, passo.normalized));
            Vector3 p = transform.position + transform.forward * (velocidade * alinhado * dt);
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var chao, 3f, _mascaraDoChao, QueryTriggerInteraction.Ignore))
                p.y = chao.point.y;
            transform.position = p;
        }
    }
}
