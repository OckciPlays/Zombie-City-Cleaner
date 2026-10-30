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
    /// As regras são as do Ferramentas/trilha/maestro.py, que o Pedro ouviu e aprovou:
    /// <list type="bullet">
    /// <item>A exploração vem em trechos de 2 a 3 minutos (a neblina de dia, a noite à noite): uma entrada, de 3 a 8 blocos
    /// do meio, escolhidos na hora sem repetir os últimos, e uma saída. Depois, o silêncio: só o vento, de 1 a 3 minutos.
    /// O vento não para nunca; com música, fica mais baixo.</item>
    /// <item>O perigo tem três níveis, tocados em camadas do mesmo bloco: 1 a tensão (um zumbi investigando ou procurando
    /// perto), 2 a perseguição (um caçando você perto), 3 a horda (de 4 caçando para cima, ou 2 atacando). O nível sobe
    /// na hora e desce um degrau a cada 4 s sem motivo.</item>
    /// <item>Os momentos curtos não são de cada zumbi ("cuidado!!", o Pedro): o "te viram" toca uma vez por perigo, quando
    /// alguém começa a te caçar, com o golpe caindo no tempo do perigo; se o último perigo acabou há menos de 40 s, ele
    /// volta sem o golpe. O fim do perigo espera o compasso: a saída, ou a saída pela pedra se um dos que te caçavam
    /// virou estátua nos últimos 15 s. O amanhecer toca quando o sol chega (no meio de uma perseguição, ele espera o
    /// perigo acabar e vira a saída dela); a morte corta tudo.</item>
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
        [Tooltip("O silêncio depois do perigo, antes de a exploração voltar (s).")]
        public Vector2 silencioDepoisDoPerigo = new Vector2(20f, 45f);

        [Header("O perigo")]
        [Tooltip("Até onde um zumbi caçando conta (m).")]
        public float raioDaCaca = 30f;
        [Tooltip("Até onde um zumbi investigando ou procurando conta para a tensão (m).")]
        public float raioDaTensao = 20f;
        [Tooltip("A partir de quantos caçando é a horda (o nível 3).")]
        public int hordaAPartirDe = 4;
        [Tooltip("O nível desce um degrau a cada tantos segundos sem motivo.")]
        public float desceUmNivel = 4f;
        [Tooltip("O \"te viram\" só toca se o último perigo acabou há mais destes segundos.")]
        public float entradaDeNovo = 40f;
        [Tooltip("A saída pela pedra, se um dos que te caçavam virou estátua nestes últimos segundos.")]
        public float pedraConta = 15f;
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

        /// <summary>O nível de perigo forçado pelo F1 (de 0 a 3), ou -1: o dos zumbis em volta.</summary>
        public static int NivelForcado = -1;
        /// <summary>Pelo F1: o silêncio entre os trechos fica curto (de 15 a 30 s), para testar.</summary>
        public static bool SilencioCurto;

        // ------------------------------------------------------------------ o que o F1 mostra
        public int Nivel => _nivel;
        public int Cacando { get; private set; }
        public int Investigando { get; private set; }
        public string Situacao => Descrever(AudioSettings.dspTime);
        public readonly List<string> Registro = new List<string>();

        enum Estado { Fora, Silencio, Exploracao, Perigo }

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

        TrilhaDoJogo _trilha;
        readonly List<Fonte> _fontes = new List<Fonte>();
        readonly List<AudioSource> _livres = new List<AudioSource>();
        readonly Dictionary<string, List<AudioClip>> _recentes = new Dictionary<string, List<AudioClip>>();
        readonly List<Zumbi> _cacadores = new List<Zumbi>();
        readonly Queue<string> _fila = new Queue<string>();

        Estado _estado = Estado.Fora;
        double _inicioDaPartida, _proximaMusica, _proximoBloco, _proximoVento, _fimDoPerigo = -999, _semPerigoAte, _ultimaPedra = -999;
        double _perigoT0, _camada2Depois, _descendoDesde = -1, _proximaOlhada;
        int _nivel, _bruto, _blocosNoTrecho, _blocoAtual;
        bool _teveEntrada, _entrarDevagar, _amanhecerPendente, _eraDia, _caiu;
        string _familia, _planoAnterior, _tipoAtual;

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
            bool emJogo = Ligada && _trilha != null && eu != null && eu.IsSpawned;
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
                _proximaOlhada = agora + 0.25;
                Olhar(agora, eu.transform.position);
            }
            int antes = _nivel;
            Hesitar(agora);
            if (_nivel != antes) Anotar(agora, $"nível do perigo: {antes} -> {_nivel}");

            Vento(agora, dia);
            Ambiente(agora, dia, dt);   // os sons de ambiente (MaestroDaTrilha.Ambiente.cs)
            if (_caiu)
            {
                _caiu = false;
                Morte(agora);
            }
            if (dia && !_eraDia) Amanhecer(agora);
            _eraDia = dia;

            if (_estado == Estado.Silencio || _estado == Estado.Exploracao)
            {
                if (_nivel >= 1 && agora >= _semPerigoAte)
                    ComecarPerigo(agora, _nivel >= 2 && agora - _fimDoPerigo > entradaDeNovo);
                else if (_estado == Estado.Silencio && agora >= _proximaMusica) ComecarTrecho(agora, dia);
                else if (_estado == Estado.Exploracao) SeguirTrecho(agora);
            }
            if (_estado == Estado.Perigo)
            {
                if (_nivel >= 2 && !_teveEntrada && agora - _fimDoPerigo > entradaDeNovo) EntradaNoMeio(agora);
                if (_nivel == 0) TerminarPerigo(agora, agora - _ultimaPedra <= pedraConta);
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
            _nivel = _bruto = 0;
            _descendoDesde = -1;
            _fimDoPerigo = _ultimaPedra = -999;
            _semPerigoAte = 0;
            _teveEntrada = _amanhecerPendente = _caiu = false;
            _eraDia = dia;
            _fila.Clear();
            Registro.Clear();
            ComecarOAmbiente(agora);
            Anotar(agora, "a partida começou: o vento, e a música daqui a pouco");
        }

        void Desligar(double agora)
        {
            foreach (var f in _fontes) Apagar(f, agora, 2f);
            DesligarOAmbiente();
            _estado = Estado.Fora;
        }

        // ------------------------------------------------------------------ o perigo em volta
        void Olhar(double agora, Vector3 eu)
        {
            // quem caçava e agora é estátua: a saída do perigo vai ser pela pedra
            foreach (var z in _cacadores)
                if (z != null && z.Petrificado) _ultimaPedra = agora;
            int atacando = 0, tensao = 0;
            var zm = ZombieManager.Instancia;
            if (zm != null && zm.IsSpawned) zm.Ameaca(eu, raioDaCaca, raioDaTensao, _cacadores, out atacando, out tensao);
            else _cacadores.Clear();
            Cacando = _cacadores.Count;
            Investigando = tensao;
            int bruto = Cacando >= hordaAPartirDe || atacando >= 2 ? 3 : Cacando >= 1 ? 2 : tensao >= 1 ? 1 : 0;
            _bruto = NivelForcado >= 0 ? Mathf.Clamp(NivelForcado, 0, 3) : bruto;
        }

        // sobe na hora; desce um degrau a cada desceUmNivel segundos abaixo
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
                else if (agora - _descendoDesde >= desceUmNivel)
                {
                    _nivel--;
                    _descendoDesde = agora;
                }
            }
            else _descendoDesde = -1;
        }

        // ------------------------------------------------------------------ a exploração
        void ComecarTrecho(double agora, bool dia)
        {
            _familia = dia ? "neblina" : "noite";
            var faixa = dia ? blocosNaNeblina : blocosNaNoite;
            var seguinte = dia ? SeguinteNaNeblina : SeguinteNaNoite;
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
            Anotar(agora, $"um trecho da {(dia ? "neblina" : "noite")} ({_blocosNoTrecho} blocos): {string.Join(" > ", _fila)}");
        }

        void SeguirTrecho(double agora)
        {
            if (agora < _proximoBloco - 0.4) return;
            var familia = _familia == "neblina" ? _trilha.neblina : _trilha.noite;
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
        void ComecarPerigo(double agora, bool comEntrada)
        {
            ApagarGrupos(agora, 2f, "exploracao", "momento");
            _fila.Clear();
            double t0;
            if (comEntrada && _trilha.entradas.Length > 0)
            {
                Tocar(Escolher("entrada", new List<AudioClip>(_trilha.entradas)), agora + 0.05, 1f, "momento");
                t0 = agora + 0.05 + _trilha.encaixeDaEntrada;
                _teveEntrada = true;
                Anotar(agora, $"TE VIRAM: o golpe, e o perigo começa nele (nível {_nivel})");
            }
            else
            {
                t0 = agora + 0.15;
                _teveEntrada = _nivel >= 2;   // te viram sem o golpe (o último perigo foi há pouco): ele não toca depois
                Anotar(agora, _nivel >= 2 ? "o perigo volta, sem o golpe (o último foi há pouco)" : "o perigo chega devagar: só a tensão");
            }
            _perigoT0 = _proximoBloco = t0;
            _planoAnterior = null;
            _entrarDevagar = !comEntrada;
            _camada2Depois = 0;
            _estado = Estado.Perigo;
        }

        void SeguirPerigo(double agora)
        {
            if (agora >= _proximoBloco - 0.4 && _trilha.perigo.Length > 0)
            {
                string plano = _planoAnterior == null || _planoAnterior != "A" ? "A" : Random.value < 0.5f ? "B" : "C";
                var opcoes = new List<TrilhaDoJogo.BlocoDoPerigo>();
                foreach (var b in _trilha.perigo)
                    if (b.plano == plano) opcoes.Add(b);
                if (opcoes.Count == 0) opcoes.AddRange(_trilha.perigo);
                var bloco = EscolherBloco(plano, opcoes);
                for (int c = 1; c <= 3 && c <= bloco.camadas.Length; c++)
                {
                    float alvo = CamadaLigada(c, _proximoBloco) ? volumeDasCamadas[c - 1] : 0f;
                    var f = Tocar(bloco.camadas[c - 1], _proximoBloco, _entrarDevagar ? 0f : alvo, "perigo", c);
                    if (f != null && _entrarDevagar && alvo > 0f) f.Rampa(alvo, c == 1 ? 3f : 1.5f);   // sem o golpe, o perigo chega devagar
                }
                _entrarDevagar = false;
                _planoAnterior = plano;
                _proximoBloco += _trilha.comprimentoDoPerigo;
            }
            // o volume de cada camada segue o nível: sobe rápido, desce devagar
            foreach (var f in _fontes)
            {
                if (f.grupo != "perigo" || f.cortar || f.inicio > agora + 0.5) continue;
                float alvo = CamadaLigada(f.camada, agora) ? volumeDasCamadas[f.camada - 1] : 0f;
                f.Rampa(alvo, alvo > f.ganho ? 0.6f : 3f);
            }
        }

        bool CamadaLigada(int camada, double quando) => _nivel >= camada && !(camada == 2 && quando < _camada2Depois);

        // já estava na tensão e agora te viram: o golpe cai num tempo do perigo que já toca
        void EntradaNoMeio(double agora)
        {
            _teveEntrada = true;
            if (_trilha.entradas.Length == 0) return;
            double tempo = 60.0 / _trilha.bpmDoPerigo;
            double golpe = _perigoT0 + Math.Ceiling((agora + 0.05 + _trilha.encaixeDaEntrada - _perigoT0) / tempo) * tempo;
            Tocar(Escolher("entrada", new List<AudioClip>(_trilha.entradas)), golpe - _trilha.encaixeDaEntrada, 1f, "momento");
            _camada2Depois = golpe;
            Anotar(agora, "TE VIRAM (já na tensão): o golpe cai no tempo do perigo");
        }

        void TerminarPerigo(double agora, bool pedra)
        {
            double compasso = 4 * 60.0 / _trilha.bpmDoPerigo;
            double b = _perigoT0 + Math.Ceiling((agora - _perigoT0) / compasso + 0.01) * compasso;
            foreach (var f in _fontes)
            {
                if (f.grupo != "perigo" || f.cortar) continue;
                if (f.inicio >= b - 0.01) Apagar(f, agora, 0f);
                else
                {
                    f.cortar = true;
                    f.rampaEm = b;
                    f.rampaAlvo = 0f;
                    f.rampaSegundos = 1.2f;
                }
            }
            AudioClip clipe;
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
            }
            else
            {
                clipe = Escolher("saida", new List<AudioClip>(_trilha.saidas));
                como = "a saída";
            }
            var fim = clipe != null ? Tocar(clipe, b, 1f, "momento") : null;
            _estado = Estado.Silencio;
            _fimDoPerigo = b;
            _teveEntrada = false;
            _proximaMusica = (fim != null ? fim.fim : b) + Random.Range(silencioDepoisDoPerigo.x, silencioDepoisDoPerigo.y);
            Anotar(agora, $"o perigo acabou: {como}, no compasso; depois só o vento");
        }

        // ------------------------------------------------------------------ os momentos
        void Amanhecer(double agora)
        {
            if (_trilha.amanhecer == null) return;
            if (_estado == Estado.Perigo)
            {
                _amanhecerPendente = true;
                if (_nivel >= 2) Anotar(agora, "amanheceu no meio do perigo: o amanhecer espera o perigo acabar");
                else TerminarPerigo(agora, false);
                return;
            }
            ApagarGrupos(agora, 3f, "exploracao", "momento");
            _fila.Clear();
            var f = Tocar(_trilha.amanhecer, agora + 0.05, 1f, "momento");
            var faixa = SilencioCurto ? silencioCurto : silencioDepoisDoTrecho;
            _estado = Estado.Silencio;
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
            _nivel = _bruto = 0;
            _descendoDesde = -1;
            _fimDoPerigo = agora;
            _semPerigoAte = fim;
            _teveEntrada = _amanhecerPendente = false;
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
            bool musica = _estado == Estado.Exploracao || _estado == Estado.Perigo;
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

        void ApagarGrupos(double agora, float segundos, params string[] grupos)
        {
            foreach (var f in _fontes)
                if (!f.cortar && Array.IndexOf(grupos, f.grupo) >= 0) Apagar(f, agora, segundos);
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
            if (Registro.Count > 30) Registro.RemoveAt(0);
        }

        string Descrever(double agora)
        {
            string perigo = $"nível {_nivel} · em volta: {Cacando} caçando, {Investigando} investigando";
            switch (_estado)
            {
                case Estado.Exploracao:
                    return $"{(_familia == "neblina" ? "Neblina" : "Noite")}: {_tipoAtual} ({_blocoAtual} de {_blocosNoTrecho})\n{perigo}";
                case Estado.Perigo:
                    return $"PERIGO ({(_nivel >= 3 ? "a horda" : _nivel == 2 ? "a perseguição" : "a tensão")})\n{perigo}";
                case Estado.Silencio:
                    return $"Silêncio (o vento) · música em {Math.Max(0, _proximaMusica - agora):0} s\n{perigo}";
            }
            return Ligada ? "fora da partida" : "desligada";
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
