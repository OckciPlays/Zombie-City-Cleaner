using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Music
{
    /// <summary>
    /// O maestro da trilha sonora (a pedido em 29/10/2026). Roda em cada jogador, sozinho (a música não passa pela rede):
    /// olha os zumbis em volta dele, o dia e a noite, e decide o que toca, bloco por bloco (<see cref="TrilhaDoJogo"/>).
    /// As regras são as do Ferramentas/trilha/maestro.py, que o Pedro ouviu e aprovou. Em 04/11/2026 as regras do perigo
    /// mudaram (os amigos do Pedro acharam a música de luta "muito cansativa, pois toda hora a música entra"): agora a
    /// música de luta só entra quando é com VOCÊ, e é rara de propósito.
    /// <list type="bullet">
    /// <item>A exploração vem em trechos de 2 a 3 minutos (a neblina de dia, a noite à noite): uma entrada, de 3 a 8 blocos
    /// do meio, escolhidos na hora sem repetir os últimos, e uma saída. Depois, o silêncio: só o vento, de 1 a 3 minutos.
    /// O vento não para nunca; com música, fica mais baixo.</item>
    /// <item>Quem conta: te caça o zumbi que está atrás de você (o alvo dele é você) a até 30 m, ou qualquer um brigando
    /// (perseguindo ou atacando) a até 8 m de você, seja com quem for (o grupo que luta junto ouve junto). Só dão a
    /// tensão: um zumbi brigando com outro (um amigo, um sobrevivente) entre 8 e 15 m, 2 investigando ou procurando a até
    /// 12 m, ou 1 colado em você (a até 6 m). Um tiro que chama zumbis de longe não dá música até eles chegarem perto.</item>
    /// <item>O perigo tem três níveis, tocados em camadas do mesmo bloco: 1 a tensão, 2 a perseguição (um te caçando),
    /// 3 a horda (5 te caçando, ou 3 te atacando). O nível sobe na hora e desce um degrau a cada 4 s sem motivo (da horda
    /// para baixo, 6 s).</item>
    /// <item>A tensão sozinha não abre o perigo. Na exploração, nada muda. No silêncio, depois de 8 s seguidos, ela abre a
    /// SOMBRA: só a camada 1 (o coro de boca fechada), baixinho, entrando em 4 s, por no máximo 2 blocos; no máximo uma a
    /// cada 3 min, fora da recarga e com a saída anterior já acabada. A sombra não conta nas entradas, não arma a recarga
    /// e não cala o ambiente.</item>
    /// <item>O perigo só começa com a caça confirmada (o nível 2 seguido; um buraco de até 0,5 s não zera a conta): 2 s; 1 s
    /// se o "te viram" estiver livre e um deles a até 15 m; 6 s na recarga (os 45 s depois de um perigo); a horda, 0,5 s,
    /// sempre. No máximo 3 entradas a cada 5 min: com o orçamento cheio, só a horda entra (a sombra ainda pode abrir, mas
    /// fica sombra). Uma caça confirmada na sombra vira a perseguição (e conta como entrada). Uma caça que não pode entrar
    /// fecha o trecho da exploração no fim do bloco, sem corte, e a próxima música (no silêncio também) fica para 30 s
    /// depois dela.</item>
    /// <item>O "te viram" (o golpe) é raro: no máximo 1 por perigo, 1 a cada 2 min, só 90 s depois do fim do último perigo
    /// (a horda não espera) e com um deles a até 15 m. Ele só cai em três momentos: na entrada, na sombra virando caça (a
    /// camada 2 espera o golpe) e na primeira chegada da horda num perigo ainda sem golpe (a camada 3 espera o golpe).</item>
    /// <item>O fim do perigo: 6 s no nível 0 (a graça: duas caças perto viram um perigo só), ou 20 s sem ninguém te
    /// caçando (só investigando em volta), ou o nível 0 na hora se um dos que te caçavam virou estátua nos últimos 25 s.
    /// Ele espera o compasso e toca, nesta ordem: o amanhecer pendente; a saída pela pedra; a saída (se a caça durou 12 s
    /// e a última saída foi há 45 s ou mais); senão, o fim mudo (as camadas apagam em 4 s, sem clipe). Até a barra, a
    /// camada 1 segura (na graça também). Depois, a recarga.</item>
    /// <item>O amanhecer toca quando o sol chega (no meio de uma perseguição, ele espera o perigo acabar e vira a saída
    /// dela); a morte corta tudo. Para os dois (e para o tornado avistado), a sombra é como o silêncio.</item>
    /// </list>
    /// Os blocos tocam pelo relógio do áudio (AudioSource.PlayScheduled): cada bloco começa exatamente no fim do anterior,
    /// e a cauda do eco do anterior soa por baixo. No F1, o painel da trilha (MenuDoDesenvolvedor.Trilha.cs).
    /// </summary>
    public partial class MaestroDaTrilha : MonoBehaviour
    {
        public static MaestroDaTrilha Instancia { get; private set; }

        [Header("Os trechos de música")]
        [Tooltip("Quantos blocos no meio de um trecho da neblina (fora a entrada e a saída), mínimo e máximo.")]
        public Vector2Int blocosNaNeblina = new Vector2Int(4, 8);
        public Vector2Int blocosNaNoite = new Vector2Int(3, 6);
        [Tooltip("Do começo da partida até a primeira música (s).")]
        public Vector2 primeiraMusica = new Vector2(6f, 12f);
        [Tooltip("O silêncio (só o vento) depois de um trecho de música (s).")]
        public Vector2 silencioDepoisDoTrecho = new Vector2(60f, 180f);
        [Tooltip("O silêncio quando o F1 pede silêncio curto (s).")]
        public Vector2 silencioCurto = new Vector2(15f, 30f);
        [Tooltip("O silêncio depois do perigo (contado do fim da saída ou do fim mudo), antes de a exploração voltar (s).")]
        public Vector2 silencioDepoisDoPerigo = new Vector2(20f, 45f);

        // 04/11/2026 ("muito cansativa, pois toda hora a música entra"): estes números são os do plano aprovado (o
        // Ferramentas/trilha/maestro.py usa os mesmos). O componente nasce pelo AddComponent, então o que vale no jogo é o
        // número escrito aqui.
        [Header("O perigo: quem conta")]
        [Tooltip("Até onde um zumbi que está atrás de você (o alvo dele é você) conta como caçador (m).")]
        public float raioDaCaca = 30f;
        [Tooltip("Até onde qualquer zumbi perseguindo ou atacando (seja quem for o alvo) conta como caçador seu: o grupo que luta junto (m).")]
        public float raioDaBrigaJunto = 8f;
        [Tooltip("Até onde um zumbi brigando com outro (um amigo, um sobrevivente) dá a tensão (m).")]
        public float raioDosOutros = 15f;
        [Tooltip("Até onde um zumbi investigando ou procurando conta para a tensão (m).")]
        public float raioDaTensao = 12f;
        [Tooltip("Um investigando colado em você (até tantos metros) já basta para a tensão.")]
        public float tensaoColada = 6f;
        [Tooltip("Quantos investigando ou procurando em volta dão a tensão (o nível 1).")]
        public int tensaoAPartirDe = 2;
        [Tooltip("A partir de quantos te caçando é a horda (o nível 3).")]
        public int hordaAPartirDe = 5;
        [Tooltip("Ou a partir de quantos te atacando.")]
        public int hordaAtacando = 3;

        [Header("O perigo: o nível desce")]
        [Tooltip("O nível desce um degrau a cada tantos segundos sem motivo.")]
        public float desceUmNivel = 4f;
        [Tooltip("Da horda (o nível 3) para a perseguição, a descida espera mais (s): a horda não fica subindo e descendo.")]
        public float desceDaHorda = 6f;

        [Header("O perigo: a confirmação para entrar")]
        [Tooltip("A caça (o nível 2) precisa durar tantos segundos seguidos para a música entrar.")]
        public float confirmarCaca = 2f;
        [Tooltip("Com o \"te viram\" livre e um deles perto, basta tanto (s).")]
        public float confirmarComGolpe = 1f;
        [Tooltip("A horda (o nível 3) entra depois de tanto (s), sempre.")]
        public float confirmarHorda = 0.5f;
        [Tooltip("Na recarga (logo depois de um perigo), a caça precisa de tanto (s).")]
        public float confirmarNaRecarga = 6f;
        [Tooltip("Um buraco de até tanto (s) na caça ou na tensão não zera a conta.")]
        public float furo = 0.5f;

        [Header("O perigo: a recarga e o orçamento")]
        [Tooltip("Depois do fim de um perigo (no compasso da saída), tantos segundos de recarga.")]
        public float recargaDoPerigo = 45f;
        [Tooltip("No máximo tantas entradas do perigo...")]
        public int entradasPorJanela = 3;
        [Tooltip("...em qualquer janela de tantos segundos (a horda sempre entra).")]
        public float janelaDasEntradas = 300f;

        [Header("O perigo: o fim")]
        [Tooltip("O perigo acaba depois de tantos segundos no nível 0 (duas caças perto viram um perigo só).")]
        public float graca = 6f;
        [Tooltip("Ou depois de tantos segundos seguidos sem ninguém te caçando (só investigando em volta).")]
        public float tetoSemCaca = 20f;
        [Tooltip("A saída pela pedra, se um dos que te caçavam virou estátua nestes últimos segundos (e aí, sem a graça).")]
        public float pedraConta = 25f;
        [Tooltip("A saída (o clipe) só toca se o perigo passou tantos segundos no nível 2 ou 3...")]
        public float saidaDepoisDe = 12f;
        [Tooltip("...e se a última saída tocou há tantos segundos ou mais; senão, o fim mudo.")]
        public float saidaACada = 45f;
        [Tooltip("O fim mudo (e o fim da sombra): as camadas apagam em tantos segundos, no compasso, sem clipe.")]
        public float fimMudo = 4f;

        [Header("O perigo: o \"te viram\"")]
        [Tooltip("No mínimo tantos segundos entre um \"te viram\" e outro.")]
        public float golpeACada = 120f;
        [Tooltip("O \"te viram\" só toca se o último perigo acabou há mais destes segundos (a horda não espera).")]
        public float entradaDeNovo = 90f;
        [Tooltip("E só com um dos que te caçam a até tantos metros.")]
        public float golpePerto = 15f;

        [Header("A sombra (a tensão no silêncio)")]
        [Tooltip("A tensão precisa durar tantos segundos seguidos no silêncio para abrir a sombra.")]
        public float sombraConfirmar = 8f;
        [Tooltip("No máximo uma sombra a cada tantos segundos.")]
        public float sombraACada = 180f;
        [Tooltip("A sombra dura no máximo tantos blocos do perigo.")]
        public int sombraBlocos = 2;
        [Tooltip("O volume da camada 1 na sombra.")]
        public float volumeDaSombra = 0.75f;
        [Tooltip("A sombra entra em tantos segundos.")]
        public float sombraEntra = 4f;

        [Header("As camadas")]
        [Tooltip("O volume de cada camada do perigo (a tensão, a perseguição, a horda).")]
        public float[] volumeDasCamadas = { 1f, 0.9f, 0.85f };

        [Header("O vento")]
        [Tooltip("O vento por baixo da música (no silêncio, 1).")]
        public float ventoNaMusica = 0.35f;

        // ------------------------------------------------------------------ o que o F1 mexe (gravado no computador)
        public static bool Ligada
        {
            get => PlayerPrefs.GetInt("trilha.ligada", 1) == 1;
            set { PlayerPrefs.SetInt("trilha.ligada", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat("trilha.volume", 0.7f);
            set => PlayerPrefs.SetFloat("trilha.volume", Mathf.Clamp01(value));   // grava junto com as opções (ou ao sair do jogo)
        }

        /// <summary>
        /// O nível de perigo forçado pelo F1 (de 0 a 3), ou -1: o dos zumbis em volta. Forçado, as confirmações (os
        /// segundos de espera para entrar, o orçamento cheio) são puladas e o "te viram" não precisa de um zumbi perto.
        /// </summary>
        public static int NivelForcado = -1;
        /// <summary>Pelo F1: o silêncio entre os trechos fica curto (de 15 a 30 s), para testar.</summary>
        public static bool SilencioCurto;

        /// <summary>
        /// Até quando (o Time.unscaledTime) um lugar pediu silêncio: o hospital (o AmbienteSonoro dele, enquanto quem joga está
        /// dentro; 07/11/2026, o Plano_Hospital.md, a parte 6: "o mais importante é o silêncio"; a música do hospital saiu, a
        /// pedido). Nele, a exploração não começa, a que estiver tocando sai no fim do bloco (pela saída) e os sons de ambiente
        /// de fora ficam três vezes mais raros. O perigo continua igual.
        /// </summary>
        public static float QuietoAte;
        public static bool NumLugarQuieto => Time.unscaledTime < QuietoAte;

        // ------------------------------------------------------------------ o que o F1 mostra
        public int Nivel => _nivel;
        /// <summary>Os que te caçam (o alvo é você, a até 30 m; ou brigando a até 8 m de você).</summary>
        public int Cacando { get; private set; }
        /// <summary>Os que investigam ou procuram em volta (a até 12 m).</summary>
        public int Investigando { get; private set; }
        public string Situacao => Descrever(AudioSettings.dspTime);
        public readonly List<string> Registro = new List<string>();

        enum Estado { Fora, Silencio, Exploracao, Perigo }

        // o que o silêncio ou a exploração fazem com o perigo em volta (a cada quadro)
        enum Entrar { Nada, Sombra, Perigo, Esperando }

        class Fonte
        {
            public AudioSource som;
            public double inicio, fim;
            public string grupo;
            public int camada;
            public float ganho, alvo, vel;
            public double rampaEm = -1;
            public float rampaAlvo, rampaSegundos;
            public bool cortar;
            public string nome;
            public AudioLowPassFilter filtro;

            public void Rampa(float novo, float segundos)
            {
                if (Mathf.Abs(novo - alvo) < 1e-4f) return;   // o mesmo alvo: a rampa que já corre continua
                alvo = novo;
                vel = Mathf.Abs(alvo - ganho) / Mathf.Max(segundos, 0.001f);
            }
        }

        static readonly Dictionary<string, string[]> SeguinteNaNeblina = new Dictionary<string, string[]>
        {
            { "entrada", new[] { "piano", "piano", "vazio" } }, { "piano", new[] { "piano", "cordas", "cello", "cordas", "vazio" } },
            { "vazio", new[] { "piano", "cello", "cordas" } }, { "cordas", new[] { "cello", "piano", "vazio", "piano" } },
            { "cello", new[] { "piano", "vazio", "cordas", "piano" } },
        };
        static readonly Dictionary<string, string[]> SeguinteNaNoite = new Dictionary<string, string[]>
        {
            { "entrada", new[] { "cello", "coro" } }, { "cello", new[] { "coro", "coracao", "vidro" } },
            { "coro", new[] { "cello", "coracao", "vidro" } }, { "coracao", new[] { "cello", "vidro", "coro" } },
            { "vidro", new[] { "cello", "coro" } },
        };

        // a cada quanto o maestro olha os zumbis em volta (s)
        const double IntervaloDaOlhada = 0.25;

        TrilhaDoJogo _trilha;
        readonly List<Fonte> _fontes = new List<Fonte>();
        readonly List<AudioSource> _livres = new List<AudioSource>();
        readonly Dictionary<string, List<AudioClip>> _recentes = new Dictionary<string, List<AudioClip>>();
        readonly List<Zumbi> _cacadores = new List<Zumbi>();
        readonly Queue<string> _fila = new Queue<string>();
        // quando cada entrada do perigo aconteceu (o orçamento: no máximo entradasPorJanela em janelaDasEntradas)
        readonly List<double> _entradas = new List<double>();

        Estado _estado = Estado.Fora;
        double _inicioDaPartida, _proximaMusica, _proximoBloco, _proximoVento, _fimDoPerigo = -999, _semPerigoAte, _ultimaPedra = -999;
        double _perigoT0, _camada2Depois, _camada3Depois, _descendoDesde = -1, _proximaOlhada;
        // as confirmações e os relógios das regras novas (04/11/2026); -1: não está acontecendo
        double _cacaDesde = -1, _ultimaCacaVista, _tensaoDesde = -1, _ultimaTensaoVista, _zeroDesde = -1, _semCacaDesde = -1;
        double _recargaAte, _ultimoGolpe = -999, _ultimaSombra = -999, _sombraAte, _ultimaSaida = -999, _fimDaSaida, _esperaAnotada = -1;
        float _tempoNaCaca, _maisPerto = float.MaxValue;
        int _nivel, _bruto, _blocosNoTrecho, _blocoAtual, _atacando, _outros, _colados;
        bool _teveGolpe, _hordaJaVeio, _sombra, _entrarDevagar, _amanhecerPendente, _eraDia, _caiu;
        string _familia, _planoAnterior, _tipoAtual;
        RaiosDaTrilha _raios;
        // para o F1 conferir o "entra toda hora": o que aconteceu na partida
        int _entradasNaPartida, _sombrasNaPartida, _golpesNaPartida, _saidasNaPartida, _pedrasNaPartida, _mudosNaPartida;

        // a sombra toca (o perigo só na camada 1, baixinho): para o resto, ela é como o silêncio
        bool NaSombra => _estado == Estado.Perigo && _sombra;
        // o perigo de verdade (a perseguição ou a horda tocando; não a sombra)
        bool PerigoDeVerdade => _estado == Estado.Perigo && !_sombra;
        static bool Forcado => NivelForcado >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Criar()
        {
            if (Instancia != null) return;
            var trilha = Resources.Load<TrilhaDoJogo>("TrilhaDoJogo");
            if (trilha == null) return;
            var go = new GameObject("MaestroDaTrilha");
            DontDestroyOnLoad(go);
            var m = go.AddComponent<MaestroDaTrilha>();
            m._trilha = trilha;
        }

        void Awake() => Instancia = this;

        void OnEnable() => VidaJogador.Caiu += AoCair;

        void OnDisable() => VidaJogador.Caiu -= AoCair;

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void AoCair() => _caiu = true;

        // ------------------------------------------------------------------ o quadro
        void Update()
        {
            double agora = AudioSettings.dspTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var eu = JogadorRede.Local;
            // no Dia 0 (o mundo normal, antes do apocalipse), a trilha do jogo não toca: a festa, o jornal na TV e a rua têm
            // os sons deles; ela começa no mundo, depois da porta do abrigo (02/11/2026: tocava no começo do jornal)
            var mundo = MundoDaPartida.Instancia;
            bool emJogo = Ligada && _trilha != null && eu != null && eu.IsSpawned && !(mundo != null && mundo.NoDiaZero);
            if (!emJogo)
            {
                if (_estado != Estado.Fora) Desligar(agora);
                Fontes(agora, dt);
                return;
            }
            bool dia = CicloDoDia.Noite < 0.5f;
            if (_estado == Estado.Fora) Comecar(agora, dia);

            if (agora >= _proximaOlhada)
            {
                _proximaOlhada = agora + IntervaloDaOlhada;
                Olhar(agora, eu);
            }
            int antes = _nivel;
            Hesitar(agora);
            if (_nivel != antes) Anotar(agora, $"nível do perigo: {antes} -> {_nivel}");
            // os relógios do fim do perigo (R8): há quanto tempo o nível está em 0, e há quanto tempo ninguém te caça
            _zeroDesde = _nivel == 0 ? (_zeroDesde >= 0 ? _zeroDesde : agora) : -1;
            _semCacaDesde = _bruto <= 1 ? (_semCacaDesde >= 0 ? _semCacaDesde : agora) : -1;

            Vento(agora, dia);
            Ambiente(agora, dia, dt);   // os sons de ambiente (MaestroDaTrilha.Ambiente.cs)
            if (_caiu)
            {
                _caiu = false;
                Morte(agora);
            }
            if (dia && !_eraDia) Amanhecer(agora);
            _eraDia = dia;

            TempoNaMusica(agora, dia);   // o clima: o tornado avistado corta para a família dele (MaestroDaTrilha.Clima.cs)
            if (_estado == Estado.Silencio || _estado == Estado.Exploracao)
            {
                // 04/11/2026: o perigo não entra mais com qualquer zumbi investigando; a caça precisa ser confirmada
                var entrar = PodeEntrar(agora);
                if (entrar == Entrar.Perigo) ComecarPerigo(agora, PodeGolpe(agora, _bruto >= 3), false);
                else if (entrar == Entrar.Sombra) ComecarPerigo(agora, false, true);
                else
                {
                    if (entrar == Entrar.Esperando) CacaEsperando(agora);
                    // 04/11/2026: com a caça esperando, o trecho não começa no meio dela (como no maestro.py: só com "nada");
                    // antes ele começava e, no quadro seguinte, a R7 já o fechava: tocava só a saída, por cima da briga
                    if (_estado == Estado.Silencio && entrar == Entrar.Nada && agora >= _proximaMusica && !NumLugarQuieto) ComecarTrecho(agora, dia);
                    else if (_estado == Estado.Exploracao) SeguirTrecho(agora);
                }
            }
            if (_estado == Estado.Perigo)
            {
                if (_nivel >= 2) _tempoNaCaca += dt;   // para escolher entre a saída e o fim mudo (R9)
                bool pedra = agora - _ultimaPedra <= pedraConta;
                if (_sombra) VirarCaca(agora);
                if (!_sombra && _nivel >= 3 && !_hordaJaVeio) HordaChegou(agora);
                if (AcabouOPerigo(agora, pedra, out string porque)) TerminarPerigo(agora, pedra, porque);
                else SeguirPerigo(agora);
            }
            Fontes(agora, dt);
        }

        void Comecar(double agora, bool dia)
        {
            _estado = Estado.Silencio;
            _inicioDaPartida = agora;
            _proximaMusica = agora + Random.Range(primeiraMusica.x, primeiraMusica.y);
            _proximoVento = agora + 0.1;
            ZerarOPerigo();
            _fimDoPerigo = _ultimaPedra = -999;
            _semPerigoAte = 0;
            _caiu = false;
            _entradasNaPartida = _sombrasNaPartida = _golpesNaPartida = _saidasNaPartida = _pedrasNaPartida = _mudosNaPartida = 0;
            _eraDia = dia;
            _fila.Clear();
            Registro.Clear();
            ComecarOAmbiente(agora);
            Anotar(agora, "a partida começou: o vento, e a música daqui a pouco");
        }

        // o estado do perigo do zero (no começo da partida e na morte)
        void ZerarOPerigo()
        {
            _nivel = _bruto = 0;
            _descendoDesde = -1;
            _cacaDesde = _tensaoDesde = _zeroDesde = _semCacaDesde = _esperaAnotada = -1;
            _recargaAte = _sombraAte = _fimDaSaida = 0;
            _ultimoGolpe = _ultimaSombra = _ultimaSaida = -999;
            _camada2Depois = _camada3Depois = 0;
            _tempoNaCaca = 0f;
            _teveGolpe = _hordaJaVeio = _sombra = _amanhecerPendente = false;
            _entradas.Clear();
        }

        void Desligar(double agora)
        {
            foreach (var f in _fontes) Apagar(f, agora, 2f);
            DesligarOAmbiente();
            _sombra = false;
            _estado = Estado.Fora;
        }

        // ------------------------------------------------------------------ o perigo em volta
        void Olhar(double agora, JogadorRede eu)
        {
            // quem caçava e agora é estátua: a saída do perigo vai ser pela pedra
            foreach (var z in _cacadores)
                if (z != null && z.Petrificado) _ultimaPedra = agora;
            // 04/11/2026: só conta quem é com você (R1 e R2; ZombieManager.Trilha.cs)
            var a = new AmeacaEmVolta { maisPerto = float.MaxValue };
            var zm = ZombieManager.Instancia;
            _cacadores.Clear();
            if (zm != null && zm.IsSpawned)
            {
                _raios.caca = raioDaCaca;
                _raios.brigaJunto = raioDaBrigaJunto;
                _raios.outros = raioDosOutros;
                _raios.tensao = raioDaTensao;
                _raios.colada = tensaoColada;
                zm.Ameaca(eu.transform.position, eu, in _raios, _cacadores, out a);
            }
            Cacando = a.cacando;
            Investigando = a.tensao;
            _atacando = a.atacando;
            _outros = a.outros;
            _colados = a.colados;
            _maisPerto = a.maisPerto;
            // o nível bruto: 3 a horda (R3), 2 te caçando (R1), 1 a tensão (R2)
            int bruto = a.cacando >= hordaAPartirDe || a.atacando >= hordaAtacando ? 3
                : a.cacando >= 1 ? 2
                : a.tensao >= tensaoAPartirDe || a.colados >= 1 || a.outros >= 1 ? 1 : 0;
            _bruto = Forcado ? Mathf.Clamp(NivelForcado, 0, 3) : bruto;
            if (Forcado && _bruto >= 2) _maisPerto = 0f;   // forçado pelo F1: o "te viram" não precisa de um zumbi perto
            // há quanto tempo a caça (o nível 2 ou 3) e a tensão (1 ou mais) duram, aceitando um buraco curto (R4)
            Seguido(ref _cacaDesde, ref _ultimaCacaVista, _bruto >= 2, agora);
            Seguido(ref _tensaoDesde, ref _ultimaTensaoVista, _bruto >= 1, agora);
        }

        // começa a contar quando acontece; um buraco de até "furo" segundos (2 olhadas) não zera a conta
        void Seguido(ref double desde, ref double visto, bool acontecendo, double agora)
        {
            if (acontecendo)
            {
                if (desde < 0) desde = agora;
                visto = agora;
            }
            else if (desde >= 0 && agora - visto > furo + IntervaloDaOlhada * 0.5) desde = -1;
        }

        // quanto a caça (ou a tensão) já durou, até a última olhada em que ela estava lá (o buraco do fim não conta: uma
        // caça de 5,9 s que acabou não vira 6 s pelo furo); -1: não está acontecendo
        static double Durou(double desde, double visto) => desde >= 0 ? visto - desde : -1;

        double DurouACaca => Durou(_cacaDesde, _ultimaCacaVista);

        // sobe na hora; desce um degrau a cada desceUmNivel segundos abaixo (da horda, desceDaHorda)
        void Hesitar(double agora)
        {
            if (_bruto > _nivel)
            {
                _nivel = _bruto;
                _descendoDesde = -1;
            }
            else if (_bruto < _nivel)
            {
                if (_descendoDesde < 0) _descendoDesde = agora;
                else if (agora - _descendoDesde >= (_nivel == 3 ? desceDaHorda : desceUmNivel))
                {
                    _nivel--;
                    _descendoDesde = agora;
                }
            }
            else _descendoDesde = -1;
        }

        // ------------------------------------------------------------------ as regras de entrada (04/11/2026)
        // as entradas do perigo nos últimos janelaDasEntradas segundos (o orçamento, R5)
        int EntradasNaJanela(double agora)
        {
            // a lista está em ordem: as velhas saem pela frente
            while (_entradas.Count > 0 && agora - _entradas[0] >= janelaDasEntradas) _entradas.RemoveAt(0);
            return _entradas.Count;
        }

        bool OrcamentoCheio(double agora) => EntradasNaJanela(agora) >= entradasPorJanela;

        bool NaRecarga(double agora) => agora < _recargaAte;

        // quantos segundos de caça seguida a música espera para entrar agora (R4); infinito: não entra
        double Confirmacao(double agora)
        {
            if (Forcado) return 0;
            if (_bruto >= 3) return confirmarHorda;
            if (OrcamentoCheio(agora)) return double.PositiveInfinity;
            if (NaRecarga(agora)) return confirmarNaRecarga;
            return PodeGolpe(agora, false) ? confirmarComGolpe : confirmarCaca;
        }

        // o "te viram" está livre (R11)? um a cada golpeACada; e entradaDeNovo depois do fim do último perigo (a horda
        // não espera); e com um dos que te caçam a até golpePerto
        bool PodeGolpe(double agora, bool horda) =>
            _trilha.entradas.Length > 0 && agora - _ultimoGolpe >= golpeACada && (horda || agora - _fimDoPerigo >= entradaDeNovo)
            && _maisPerto <= golpePerto;

        // por que a sombra não pode abrir agora (R6), ou null: pode
        string SombraNaoPode(double agora)
        {
            if (NaRecarga(agora)) return "na recarga";
            if (agora - _ultimaSombra <= sombraACada) return $"a última foi há {agora - _ultimaSombra:0} s";
            if (agora < _fimDaSaida) return "a saída ainda toca";
            return null;
        }

        // R4, R5, R6 e R7: o que o silêncio e a exploração fazem com o perigo em volta
        Entrar PodeEntrar(double agora)
        {
            if (agora < _semPerigoAte) return Entrar.Nada;   // a morte ainda toca
            double caca = DurouACaca;
            if (caca >= 0 && caca >= Confirmacao(agora)) return Entrar.Perigo;
            bool esperando = caca >= confirmarCaca;   // a caça confirmada que não pode entrar (a recarga ou o orçamento)
            // a tensão sozinha nunca corta a exploração; no silêncio, ela pode abrir a sombra
            double tensao = Durou(_tensaoDesde, _ultimaTensaoVista);
            if (_estado == Estado.Silencio && tensao >= 0 && tensao >= (Forcado ? 0 : sombraConfirmar) && SombraNaoPode(agora) == null)
                return Entrar.Sombra;
            return esperando ? Entrar.Esperando : Entrar.Nada;
        }

        // R7: a caça confirmada esperando (a recarga ou o orçamento): a exploração calma não fica tocando por cima da briga
        void CacaEsperando(double agora)
        {
            if (_esperaAnotada != _cacaDesde)
            {
                _esperaAnotada = _cacaDesde;
                Anotar(agora, $"caça esperando ({(OrcamentoCheio(agora) ? "orçamento" : "recarga")}): só o som dos zumbis");
            }
            // a próxima música fica para 30 s depois da briga, também no silêncio (04/11/2026: antes só valia na exploração,
            // e o silêncio abria um trecho no meio da caça; o maestro.py sempre fez assim)
            _proximaMusica = Math.Max(_proximaMusica, agora + 30);
            if (_estado != Estado.Exploracao) return;
            if (_fila.Count == 0 || (_fila.Count == 1 && _fila.Peek() == "saida")) return;
            // nenhum bloco do trecho tocou ainda (o tornado avistado acabou de abrir o dele): a entrada (o golpe dele) toca,
            // e logo a saída; senão o trecho seria só a saída
            bool nenhumTocou = _fila.Peek() == "entrada";
            if (nenhumTocou && _fila.Count == 2) return;   // já é "entrada > saída"
            _fila.Clear();
            if (nenhumTocou) _fila.Enqueue("entrada");
            _fila.Enqueue("saida");
            _blocosNoTrecho = _blocoAtual + _fila.Count;
            Anotar(agora, "o trecho fecha por causa da caça: o bloco acaba e vem a saída");
        }

        // ------------------------------------------------------------------ a exploração
        void ComecarTrecho(double agora, bool dia)
        {
            // a família: a de sempre (a neblina de dia, a noite à noite), ou a do clima (a tempestade, o tornado perto)
            _familia = FamiliaDoTempo(dia);
            var faixa = BlocosPeloNome(_familia);
            var seguinte = SeguintePeloNome(_familia);
            int n = Random.Range(faixa.x, faixa.y + 1);
            var tipos = new List<string>();
            string ultimo = "entrada";
            for (int i = 0; i < n; i++)
            {
                var opcoes = new List<string>();
                foreach (var x in seguinte[ultimo])
                {
                    bool repete3 = tipos.Count >= 2 && tipos[tipos.Count - 1] == x && tipos[tipos.Count - 2] == x;
                    bool vazioPerto = x == "vazio" && tipos.Count > 0 && (tipos[tipos.Count - 1] == "vazio" || (tipos.Count > 1 && tipos[tipos.Count - 2] == "vazio"));
                    if (!repete3 && !vazioPerto) opcoes.Add(x);
                }
                if (opcoes.Count == 0) opcoes.AddRange(seguinte[ultimo]);
                ultimo = opcoes[Random.Range(0, opcoes.Count)];
                tipos.Add(ultimo);
            }
            _fila.Clear();
            _fila.Enqueue("entrada");
            foreach (var t in tipos) _fila.Enqueue(t);
            _fila.Enqueue("saida");
            _blocosNoTrecho = _fila.Count;
            _blocoAtual = 0;
            _proximoBloco = agora + 0.15;
            _estado = Estado.Exploracao;
            Anotar(agora, $"um trecho da {_familia} ({_blocosNoTrecho} blocos): {string.Join(" > ", _fila)}");
        }

        void SeguirTrecho(double agora)
        {
            if (agora < _proximoBloco - 0.4) return;
            // num lugar quieto (o hospital), o trecho fecha: o próximo bloco é a saída
            if (NumLugarQuieto && _fila.Count > 1)
            {
                _fila.Clear();
                _fila.Enqueue("saida");
            }
            var familia = FamiliaPeloNome(_familia);
            if (_fila.Count == 0)
            {
                var faixa = SilencioCurto ? silencioCurto : silencioDepoisDoTrecho;
                _estado = Estado.Silencio;
                _proximaMusica = _proximoBloco + 5 + Random.Range(faixa.x, faixa.y);
                Anotar(agora, $"o trecho acabou: só o vento por {_proximaMusica - agora:0} s");
                return;
            }
            _tipoAtual = _fila.Dequeue();
            _blocoAtual++;
            var opcoes = new List<AudioClip>();
            foreach (var b in familia.blocos)
                if (b.tipo == _tipoAtual && b.clipe != null) opcoes.Add(b.clipe);
            var clipe = Escolher(_familia + _tipoAtual, opcoes);
            if (clipe != null) Tocar(clipe, _proximoBloco, 1f, "exploracao");
            _proximoBloco += familia.comprimento;
        }

        // ------------------------------------------------------------------ o perigo
        // a caça confirmada (golpe: o "te viram" na entrada, R11a), ou a sombra (só a camada 1, baixinho, R6)
        void ComecarPerigo(double agora, bool golpe, bool sombra)
        {
            if (sombra) golpe = false;
            _sombra = sombra;
            _teveGolpe = false;
            _hordaJaVeio = !sombra && _nivel >= 3;   // entrou já na horda: o golpe dela é o da entrada
            _tempoNaCaca = 0f;
            _camada2Depois = _camada3Depois = 0;
            double t0;
            if (sombra)
            {
                // vem do silêncio: não há o que apagar
                t0 = agora + 0.15;
                _sombraAte = t0 + Math.Max(1, sombraBlocos) * _trilha.comprimentoDoPerigo;
                _ultimaSombra = agora;
                _sombrasNaPartida++;
                Anotar(agora, $"a SOMBRA: a tensão no silêncio, só o coro de boca fechada (no máximo {sombraBlocos} blocos)");
            }
            else
            {
                bool cortou = _estado == Estado.Exploracao;
                ApagarGrupos(agora, 2f, "exploracao", "momento");
                // a grade velha (o perigo ou a sombra que acabou há pouco e ainda espera a barra para apagar) sai em 1 s,
                // quando a nova entra: não ficam dois coros fora de fase (04/11/2026)
                ApagarGrupos(agora, 1f, "perigo");
                _fila.Clear();
                _entradas.Add(agora);
                _entradasNaPartida++;
                string corte = cortou ? " [cortou a exploração]" : "";
                if (golpe)
                {
                    Tocar(Escolher("entrada", new List<AudioClip>(_trilha.entradas)), agora + 0.05, 1f, "momento");
                    t0 = agora + 0.05 + _trilha.encaixeDaEntrada;
                    MarcarGolpe(agora);
                    Anotar(agora, $"o perigo começa (nível {_nivel}){corte}: TE VIRAM, e o perigo começa no golpe");
                }
                else
                {
                    t0 = agora + 0.15;
                    Anotar(agora, $"o perigo começa (nível {_nivel}){corte}, sem o golpe: chega devagar");
                }
            }
            _perigoT0 = _proximoBloco = t0;
            _planoAnterior = null;
            _entrarDevagar = !golpe;
            _estado = Estado.Perigo;
        }

        void MarcarGolpe(double agora)
        {
            _ultimoGolpe = agora;
            _teveGolpe = true;
            _golpesNaPartida++;
        }

        // R6: uma caça confirmada na sombra a transforma na perseguição (e conta como entrada); com o orçamento cheio, só a
        // horda vira
        void VirarCaca(double agora)
        {
            if (_cacaDesde < 0) return;
            bool horda = _bruto >= 3;
            if (!Forcado && !horda && OrcamentoCheio(agora)) return;
            if (DurouACaca < (Forcado ? 0 : horda ? confirmarHorda : confirmarCaca)) return;
            bool golpe = PodeGolpe(agora, horda);
            _sombra = false;
            _hordaJaVeio = _nivel >= 3;
            _entradas.Add(agora);
            _entradasNaPartida++;
            if (golpe)
            {
                EntradaNoMeio(agora, 2);   // o golpe no tempo da sombra; a perseguição (e a horda) entram nele
                Anotar(agora, $"a sombra vira caça: o perigo começa (nível {_nivel}), TE VIRAM no tempo dela");
            }
            else
            {
                // sem o golpe, a perseguição entra numa rampa de 1,5 s (a camada 1 sobe do volume da sombra ao normal)
                foreach (var f in _fontes)
                    if (f.grupo == "perigo" && !f.cortar && f.camada >= 2 && f.inicio <= agora + 0.5)
                        f.Rampa(CamadaLigada(f.camada, agora) ? VolumeDaCamada(f.camada) : 0f, 1.5f);
                Anotar(agora, $"a sombra vira caça: o perigo começa (nível {_nivel}), sem o golpe");
            }
        }

        // R11c: a primeira chegada da horda num perigo ainda sem golpe: o golpe no tempo do perigo, e a horda entra nele
        void HordaChegou(double agora)
        {
            _hordaJaVeio = true;
            if (_teveGolpe || !PodeGolpe(agora, true)) return;
            EntradaNoMeio(agora, 3);
            Anotar(agora, "TE VIRAM (a horda chegou): o golpe cai no tempo do perigo");
        }

        void SeguirPerigo(double agora)
        {
            // a sombra dura no máximo sombraBlocos blocos: depois do último, nenhum bloco novo (ela se apaga na barra)
            bool sombraNoFim = _sombra && _proximoBloco >= _sombraAte - 0.01;
            if (agora >= _proximoBloco - 0.4 && _trilha.perigo.Length > 0 && !sombraNoFim)
            {
                string plano = _planoAnterior == null || _planoAnterior != "A" ? "A" : Random.value < 0.5f ? "B" : "C";
                var opcoes = new List<TrilhaDoJogo.BlocoDoPerigo>();
                foreach (var b in _trilha.perigo)
                    if (b.plano == plano) opcoes.Add(b);
                if (opcoes.Count == 0) opcoes.AddRange(_trilha.perigo);
                var bloco = EscolherBloco(plano, opcoes);
                for (int c = 1; c <= 3 && c <= bloco.camadas.Length; c++)
                {
                    float alvo = CamadaLigada(c, _proximoBloco) ? VolumeDaCamada(c) : 0f;
                    var f = Tocar(bloco.camadas[c - 1], _proximoBloco, _entrarDevagar ? 0f : alvo, "perigo", c);
                    // sem o golpe, o perigo chega devagar (a sombra, em sombraEntra)
                    if (f != null && _entrarDevagar && alvo > 0f) f.Rampa(alvo, c == 1 ? (_sombra ? sombraEntra : 3f) : 1.5f);
                }
                _entrarDevagar = false;
                _planoAnterior = plano;
                _proximoBloco += _trilha.comprimentoDoPerigo;
            }
            // o volume de cada camada segue o nível: sobe rápido, desce devagar
            foreach (var f in _fontes)
            {
                if (f.grupo != "perigo" || f.cortar || f.inicio > agora + 0.5) continue;
                float alvo = CamadaLigada(f.camada, agora) ? VolumeDaCamada(f.camada) : 0f;
                f.Rampa(alvo, alvo > f.ganho ? 0.6f : 3f);
            }
        }

        // a sombra toca só a camada 1; no perigo de verdade, a 2 e a 3 seguem o nível e esperam o golpe, e a 1 fica ligada
        // seja qual for o nível (04/11/2026): na graça (o nível 0 por 6 s) o coro segura, e quem a apaga é a saída (1,2 s)
        // ou o fim mudo (4 s), na barra; antes ela sumia em 3 s e a saída vinha depois de segundos de silêncio, soando como
        // "a música voltou" (e uma caça voltando na graça subia do zero, sem golpe)
        bool CamadaLigada(int camada, double quando) => _sombra
            ? camada == 1
            : (camada == 1 || _nivel >= camada) && !(camada == 2 && quando < _camada2Depois) && !(camada == 3 && quando < _camada3Depois);

        float VolumeDaCamada(int camada) => camada == 1 && _sombra ? volumeDaSombra : volumeDasCamadas[camada - 1];

        // o golpe cai num tempo do perigo que já toca: na virada da sombra (camada 2: a perseguição e a horda esperam o
        // golpe) ou na chegada da horda (camada 3: só ela espera)
        void EntradaNoMeio(double agora, int camada)
        {
            MarcarGolpe(agora);
            if (_trilha.entradas.Length == 0) return;
            double tempo = 60.0 / _trilha.bpmDoPerigo;
            double golpe = _perigoT0 + Math.Ceiling((agora + 0.05 + _trilha.encaixeDaEntrada - _perigoT0) / tempo) * tempo;
            Tocar(Escolher("entrada", new List<AudioClip>(_trilha.entradas)), golpe - _trilha.encaixeDaEntrada, 1f, "momento");
            if (camada <= 2) _camada2Depois = golpe;
            _camada3Depois = golpe;
        }

        // R8: o fim do perigo (na sombra: o fim dos blocos dela, ou o nível 0 pela graça)
        bool AcabouOPerigo(double agora, bool pedra, out string porque)
        {
            porque = null;
            if (_sombra)
            {
                if (agora >= _sombraAte) porque = "os blocos dela acabaram";
                else if (_zeroDesde >= 0 && agora - _zeroDesde >= graca) porque = "a tensão passou";
                return porque != null;
            }
            if (_zeroDesde >= 0 && agora - _zeroDesde >= (pedra ? 0.0 : graca))
                porque = pedra ? "quem te caçava virou estátua" : $"{graca:0} s sem perigo";
            else if (_semCacaDesde >= 0 && agora - _semCacaDesde >= tetoSemCaca)
                porque = $"{tetoSemCaca:0} s sem ninguém te caçando";
            return porque != null;
        }

        // os blocos do perigo que tocam apagam a partir da barra b; os que começariam depois dela nem tocam
        void ApagarOPerigoNaBarra(double agora, double b, float segundos)
        {
            foreach (var f in _fontes)
            {
                if (f.grupo != "perigo" || f.cortar) continue;
                if (f.inicio >= b - 0.01) Apagar(f, agora, 0f);
                else
                {
                    f.cortar = true;
                    f.rampaEm = b;
                    f.rampaAlvo = 0f;
                    f.rampaSegundos = segundos;
                }
            }
        }

        void TerminarPerigo(double agora, bool pedra, string porque)
        {
            double compasso = 4 * 60.0 / _trilha.bpmDoPerigo;
            double b = _perigoT0 + Math.Ceiling((agora - _perigoT0) / compasso + 0.01) * compasso;
            if (_sombra)
            {
                // a sombra se apaga na barra, sem clipe; ela não mexe na recarga nem no fim do último perigo
                ApagarOPerigoNaBarra(agora, b, fimMudo);
                _sombra = false;
                _estado = Estado.Silencio;
                _proximaMusica = Math.Max(_proximaMusica, b + 10);
                Anotar(agora, $"a sombra se apaga ({porque}), no compasso, sem clipe");
                return;
            }
            // R9: qual saída toca
            AudioClip clipe = null;
            string como;
            if (_amanhecerPendente && _trilha.amanhecer != null)
            {
                clipe = _trilha.amanhecer;
                como = "o amanhecer no lugar da saída";
                _amanhecerPendente = false;
            }
            else if (pedra && _trilha.saidasPelaPedra.Length > 0)
            {
                clipe = Escolher("pedra", new List<AudioClip>(_trilha.saidasPelaPedra));
                como = "a saída pela pedra (quem te caçava virou estátua)";
                _pedrasNaPartida++;
            }
            else if (_tempoNaCaca >= saidaDepoisDe && b - _ultimaSaida >= saidaACada && _trilha.saidas.Length > 0)
            {
                clipe = Escolher("saida", new List<AudioClip>(_trilha.saidas));
                como = "a saída";
                _ultimaSaida = b;
                _saidasNaPartida++;
            }
            else
            {
                como = _tempoNaCaca < saidaDepoisDe ? $"o fim mudo (a caça foi curta: {_tempoNaCaca:0} s)" : "o fim mudo (a última saída foi há pouco)";
                _mudosNaPartida++;
            }
            ApagarOPerigoNaBarra(agora, b, clipe != null ? 1.2f : fimMudo);
            var fim = clipe != null ? Tocar(clipe, b, 1f, "momento") : null;
            _estado = Estado.Silencio;
            _fimDoPerigo = b;
            _recargaAte = b + recargaDoPerigo;   // R10: a recarga conta da barra da saída
            _fimDaSaida = fim != null ? fim.fim : b + fimMudo;
            _teveGolpe = false;
            _proximaMusica = _fimDaSaida + Random.Range(silencioDepoisDoPerigo.x, silencioDepoisDoPerigo.y);
            Anotar(agora, $"o perigo acabou ({porque}): {como}, no compasso; depois só o vento");
        }

        // a sombra some (o amanhecer, o tornado avistado): ela é como o silêncio
        void SairDaSombra(double agora, float segundos)
        {
            ApagarGrupos(agora, segundos, "perigo");
            _sombra = false;
            _estado = Estado.Silencio;
        }

        // ------------------------------------------------------------------ os momentos
        void Amanhecer(double agora)
        {
            if (_trilha.amanhecer == null) return;
            if (PerigoDeVerdade)
            {
                _amanhecerPendente = true;
                if (_nivel >= 2) Anotar(agora, "amanheceu no meio do perigo: o amanhecer espera o perigo acabar");
                else TerminarPerigo(agora, false, "amanheceu");
                return;
            }
            if (NaSombra) SairDaSombra(agora, 3f);
            ApagarGrupos(agora, 3f, "exploracao", "momento");
            _fila.Clear();
            var f = Tocar(_trilha.amanhecer, agora + 0.05, 1f, "momento");
            var faixa = SilencioCurto ? silencioCurto : silencioDepoisDoTrecho;
            _estado = Estado.Silencio;
            _fimDaSaida = Math.Max(_fimDaSaida, f.fim);   // a sombra não abre por cima do amanhecer
            _proximaMusica = f.fim + Random.Range(faixa.x, faixa.y);
            Anotar(agora, "AMANHECER");
        }

        void Morte(double agora)
        {
            ApagarGrupos(agora, 0.4f, "exploracao", "perigo", "momento");
            _fila.Clear();
            double fim = agora + 3;
            if (_trilha.morte != null) fim = Tocar(_trilha.morte, agora + 0.1, 1f, "momento").fim;
            _estado = Estado.Silencio;
            ZerarOPerigo();   // a sombra também some
            _fimDoPerigo = agora;
            _semPerigoAte = fim;
            _proximaMusica = fim + Random.Range(silencioDepoisDoPerigo.x, silencioDepoisDoPerigo.y);
            Anotar(agora, "MORTE: tudo some e toca a morte");
        }

        // ------------------------------------------------------------------ o vento
        void Vento(double agora, bool dia)
        {
            var lista = dia ? _trilha.ventoDeDia : _trilha.ventoDeNoite;
            if (agora >= _proximoVento - 0.3 && lista.Length > 0)
            {
                var clipe = Escolher(dia ? "vento_dia" : "vento_noite", new List<AudioClip>(lista));
                double inicio = Math.Max(_proximoVento, agora + 0.05);
                Tocar(clipe, inicio, _estado == Estado.Silencio ? 1f : ventoNaMusica, "vento");
                MarcarRajadas(dia ? _trilha.rajadasDoVentoDeDia : _trilha.rajadasDoVentoDeNoite, Array.IndexOf(lista, clipe), inicio);
                _proximoVento = inicio + clipe.length - _trilha.sobreposicaoDoVento;
            }
            bool musica = _estado == Estado.Exploracao || _estado == Estado.Perigo;   // a sombra também: o vento a 35%
            if (!musica)
                foreach (var f in _fontes)
                    if (f.grupo != "vento" && !f.cortar && (f.ganho > 0.02f || f.alvo > 0.02f) && agora < f.fim - 5.0)
                    {
                        musica = true;
                        break;
                    }
            foreach (var f in _fontes)
                if (f.grupo == "vento" && !f.cortar) f.Rampa(musica ? ventoNaMusica : 1f, 4f);
        }

        // ------------------------------------------------------------------ as fontes de som
        Fonte Tocar(AudioClip clipe, double quando, float ganho, string grupo, int camada = 0)
        {
            if (clipe == null) return null;
            var som = PegarSom();
            som.clip = clipe;
            double agora = AudioSettings.dspTime;
            if (quando < agora + 0.02) quando = agora + 0.02;
            var f = new Fonte { som = som, inicio = quando, fim = quando + clipe.length, grupo = grupo, camada = camada, ganho = ganho, alvo = ganho, nome = clipe.name, filtro = som.GetComponent<AudioLowPassFilter>() };
            som.volume = ganho * Volume;
            som.PlayScheduled(quando);
            _fontes.Add(f);
            return f;
        }

        void Apagar(Fonte f, double agora, float segundos)
        {
            f.cortar = true;
            if (segundos <= 0f || f.inicio > agora + 0.05)
            {
                f.ganho = f.alvo = 0f;
                f.som.Stop();
                f.fim = agora;
            }
            else f.Rampa(0f, segundos);
        }

        // o bloco do perigo que espera a barra para apagar (cortado, com a rampa marcada) também apaga agora (04/11/2026:
        // antes ele ficava cheio até a barra por baixo do perigo novo, ou da morte); o que já está apagando continua
        void ApagarGrupos(double agora, float segundos, params string[] grupos)
        {
            foreach (var f in _fontes)
                if ((!f.cortar || f.rampaEm >= 0) && Array.IndexOf(grupos, f.grupo) >= 0)
                {
                    f.rampaEm = -1;
                    Apagar(f, agora, segundos);
                }
        }

        void Fontes(double agora, float dt)
        {
            float volume = Volume;
            for (int i = _fontes.Count - 1; i >= 0; i--)
            {
                var f = _fontes[i];
                if (f.rampaEm >= 0 && agora >= f.rampaEm)
                {
                    f.rampaEm = -1;
                    f.Rampa(f.rampaAlvo, f.rampaSegundos);
                }
                if (f.ganho != f.alvo)
                {
                    float passo = f.vel * dt;
                    f.ganho = Mathf.Abs(f.alvo - f.ganho) <= passo ? f.alvo : f.ganho + Mathf.Sign(f.alvo - f.ganho) * passo;
                }
                f.som.volume = f.ganho * volume;
                if (f.filtro != null) f.filtro.cutoffFrequency = f.grupo == "vento" ? CorteDoVento : 22000f;   // dentro, o vento abafado
                bool acabou = agora > f.fim + 0.05 || (f.cortar && f.ganho <= 0.001f && f.rampaEm < 0);
                if (!acabou) continue;
                f.som.Stop();
                f.som.clip = null;
                _livres.Add(f.som);
                _fontes.RemoveAt(i);
            }
        }

        AudioSource PegarSom()
        {
            if (_livres.Count > 0)
            {
                var s = _livres[_livres.Count - 1];
                _livres.RemoveAt(_livres.Count - 1);
                return s;
            }
            var go = new GameObject("Fonte");
            go.transform.SetParent(transform, false);
            var som = go.AddComponent<AudioSource>();
            som.playOnAwake = false;
            som.loop = false;
            som.spatialBlend = 0f;
            som.priority = 0;   // a música nunca é cortada pelo limite de vozes
            som.dopplerLevel = 0f;
            som.bypassReverbZones = true;
            som.bypassListenerEffects = true;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            return som;
        }

        // ------------------------------------------------------------------ escolher sem repetir
        AudioClip Escolher(string chave, List<AudioClip> opcoes)
        {
            if (opcoes.Count == 0) return null;
            if (!_recentes.TryGetValue(chave, out var recentes)) _recentes[chave] = recentes = new List<AudioClip>();
            var livres = opcoes.FindAll(c => !recentes.Contains(c));
            if (livres.Count == 0) livres = opcoes;
            var escolhido = livres[Random.Range(0, livres.Count)];
            recentes.Add(escolhido);
            int guardar = Mathf.Clamp(opcoes.Count - 2, 1, 10);
            while (recentes.Count > guardar) recentes.RemoveAt(0);
            return escolhido;
        }

        TrilhaDoJogo.BlocoDoPerigo EscolherBloco(string plano, List<TrilhaDoJogo.BlocoDoPerigo> opcoes)
        {
            var clipes = opcoes.ConvertAll(b => b.camadas[0]);
            var c1 = Escolher("perigo" + plano, clipes);
            return opcoes[Mathf.Max(0, clipes.IndexOf(c1))];
        }

        // ------------------------------------------------------------------ para o F1
        void Anotar(double agora, string texto)
        {
            double t = Math.Max(0, agora - _inicioDaPartida);
            Registro.Add($"{(int)(t / 60)}:{(int)(t % 60):00}  {texto}");
            if (Registro.Count > 60) Registro.RemoveAt(0);
        }

        static string Segundos(double passou, double preciso) => $"{Math.Min(Math.Max(passou, 0), preciso):0.0}/{preciso:0.#} s";

        // o que toca e o nível (as duas primeiras linhas do painel do F1)
        string Descrever(double agora)
        {
            if (_estado == Estado.Fora) return Ligada ? "fora da partida" : "desligada";
            string emVolta = $"nível {_nivel} · {Cacando} te caçando · {Investigando} investigando";
            switch (_estado)
            {
                case Estado.Exploracao:
                    return $"{NomeDaFamilia(_familia)}: {_tipoAtual} ({_blocoAtual} de {_blocosNoTrecho})\n{emVolta}";
                case Estado.Perigo:
                    if (_sombra) return $"SOMBRA (a tensão baixinho) · até {Math.Max(0, _sombraAte - agora):0} s\n{emVolta}";
                    return $"PERIGO ({(_nivel >= 3 ? "a horda" : _nivel == 2 ? "a perseguição" : _nivel == 1 ? "a tensão" : "a graça")})" +
                           $"{(_teveGolpe ? " · golpe" : "")}\n{emVolta}";
                default:
                    return $"Silêncio (o vento) · música em {Math.Max(0, _proximaMusica - agora):0} s\n{emVolta}";
            }
        }

        /// <summary>
        /// Para o F1 (04/11/2026): as regras do perigo agora (a recarga, as entradas nos últimos 5 min, quando o "te
        /// viram" fica livre, a confirmação em andamento, quem está em volta) e o que já aconteceu na partida.
        /// </summary>
        public string Regras => DescreverAsRegras(AudioSettings.dspTime);

        string DescreverAsRegras(double agora)
        {
            if (_estado == Estado.Fora) return "";
            double recarga = Math.Max(0, _recargaAte - agora);
            double golpeEm = Math.Max(0, Math.Max(golpeACada - (agora - _ultimoGolpe), entradaDeNovo - (agora - _fimDoPerigo)));
            string golpe = golpeEm > 0 ? $"golpe livre em {golpeEm:0} s (a horda: {Math.Max(0, golpeACada - (agora - _ultimoGolpe)):0} s)"
                : _maisPerto <= golpePerto ? "golpe livre" : $"golpe livre (falta um a {golpePerto:0} m)";
            string confirmando = Confirmando(agora);
            string perto = _maisPerto < float.MaxValue ? $", a {_maisPerto:0} m" : "";
            return $"recarga: {(recarga > 0 ? $"{recarga:0} s" : "não")}\n" +
                   $"entradas {EntradasNaJanela(agora)}/{entradasPorJanela} ({janelaDasEntradas / 60f:0} min)\n" +
                   $"{golpe}\n" +
                   (confirmando.Length > 0 ? $"{confirmando}\n" : "") +
                   $"seus: {Cacando} caçando ({_atacando} atacando{perto})\n" +
                   $"outros: {_outros} brigando com outro\n" +
                   $"investigando: {Investigando} ({_colados} colado{(_colados == 1 ? "" : "s")})\n" +
                   $"na partida: {_entradasNaPartida} entradas, {_sombrasNaPartida} sombras, {_golpesNaPartida} golpes; " +
                   $"{_saidasNaPartida} saídas, {_pedrasNaPartida} pedras, {_mudosNaPartida} mudos";
        }

        // a confirmação em andamento (para entrar, para a sombra, para a sombra virar caça) ou o relógio do fim
        string Confirmando(double agora)
        {
            if (_estado == Estado.Perigo)
            {
                if (_sombra)
                {
                    if (_cacaDesde < 0) return "";
                    if (!Forcado && _bruto < 3 && OrcamentoCheio(agora)) return "caça: o orçamento está cheio, fica a sombra";
                    return $"a sombra vira caça: {Segundos(DurouACaca, Forcado ? 0 : _bruto >= 3 ? confirmarHorda : confirmarCaca)}";
                }
                if (_zeroDesde >= 0) return $"a graça: {Segundos(agora - _zeroDesde, agora - _ultimaPedra <= pedraConta ? 0 : graca)}";
                if (_semCacaDesde >= 0) return $"sem caça: {Segundos(agora - _semCacaDesde, tetoSemCaca)}";
                return "";
            }
            if (agora < _semPerigoAte) return "a morte toca: nada de perigo";
            if (_cacaDesde >= 0)
            {
                double preciso = Confirmacao(agora);
                if (double.IsPositiveInfinity(preciso)) return "caça: o orçamento está cheio (só a horda entra)";
                return $"confirmando a caça: {Segundos(DurouACaca, preciso)}{(NaRecarga(agora) ? " (na recarga)" : "")}";
            }
            if (_tensaoDesde < 0) return "";
            if (_estado == Estado.Exploracao) return "tensão: na exploração, nada muda";
            string naoPode = SombraNaoPode(agora);
            return naoPode == null ? $"a sombra: {Segundos(Durou(_tensaoDesde, _ultimaTensaoVista), Forcado ? 0 : sombraConfirmar)}" : $"tensão, sem sombra ({naoPode})";
        }

        /// <summary>Pelo F1: sai do silêncio e começa um trecho agora (se nada estiver tocando).</summary>
        public bool ComecarAgora()
        {
            if (_estado != Estado.Silencio) return false;
            _proximaMusica = AudioSettings.dspTime;
            return true;
        }

        /// <summary>Pelo F1: o amanhecer, como se o sol tivesse acabado de chegar.</summary>
        public void TestarAmanhecer() => Amanhecer(AudioSettings.dspTime);

        /// <summary>Pelo F1: a morte, sem cair.</summary>
        public void TestarMorte() => _caiu = true;
    }
}
