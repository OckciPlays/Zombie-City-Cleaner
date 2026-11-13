using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// Os moradores do bunker vivendo (a parte 6 do Plano_NPCs_Bunker.md da pasta de arte), no anfitrião, no objeto "Mundo"
    /// (posto pelo <c>SobreviventesDoMundo</c>, sem mexer na cena). As fichas vêm do mundo salvo (<c>MundoSalvo.moradores</c>)
    /// e a vida de cada um (<see cref="VidaDoMorador"/>: as barras, a cama, o que ele faz e onde está) também, quando já tem.
    ///  - <b>A agenda</b> (<see cref="AgendaDoMorador"/>): dormir, o banheiro, as refeições, os afazeres, o lazer, o cochilo
    ///    dos velhos e as rezas do religioso; as necessidades passam na frente (com fome ou sede abaixo de 25 ele come fora de
    ///    hora; sem sono, cochila no sofá). Cada atividade escolhe um lugar livre (<see cref="LugaresDoBunker"/>).
    ///  - <b>As barras</b> descem e enchem com o relógio do jogo (a fome em 80 h, a sede em 50, o sono em 18 acordado). A
    ///    refeição enche a fome e a sede sem gastar o estoque (a fatia 2 da parte 6 tira das estantes).
    ///  - <b>No papel</b>: com o andar dele desmontado (ninguém perto), ele anda pelo tempo (a distância na rapidez dele) e
    ///    chega no lugar sem corpo. <b>Com o corpo</b> (<see cref="CorpoDoMorador"/>): o andar montado (e o NavMesh pronto),
    ///    ele aparece onde está (sentado, deitado ou no caminho) e anda de verdade, pela escada quando os andares do caminho
    ///    estão montados; senão, ele vai até a porta da escada e some, e segue no papel.
    ///  - <b>As camas:</b> os casais nas camas de casal (os quartos 5 e 6 primeiro, cada um de um lado), o parente da família
    ///    sem casal num deles, e os outros nos beliches do dormitório (a cama de baixo), depois nos quartos.
    /// </summary>
    public partial class MoradoresDoBunker : MonoBehaviour
    {
        public static MoradoresDoBunker Instancia { get; private set; }

        /// <summary>O passo do corpo no anfitrião (não vai no save: o corpo novo começa do que a vida diz).</summary>
        public enum Passo : byte { NoLugar, Levantando, Indo, Acomodando }

        public sealed class Morador
        {
            public FichaDoMorador ficha;
            public VidaDoMorador vida;
            public CorpoDoMorador corpo;
            public Passo passo;
            public float velocidade;
            /// <summary>
            /// Para onde o corpo anda (nas coordenadas do bunker) e se é a escada: o patamar do andar do lugar, que não está
            /// montado (lá ele some, quando ninguém vê, e segue no papel; <see cref="chegouNaEscada"/>, quando chegou).
            /// </summary>
            public Vector3 destino;
            public bool destinoEhEscada;
            public float chegouNaEscada = -1f;
            public float desde, proximaTentativa;
            public System.Random acaso;
            /// <summary>A busca na estante: o item (a estante e o id) e se ele já pegou; e quando o corpo chegou no lugar.</summary>
            public int buscaEstante = -1;
            public ushort buscaId;
            public bool pegou;
            public float chegouEm;

            /// <summary>O nome curto (em cima da cabeça e no F1): o apelido, ou o nome quando outro no bunker tem o mesmo apelido.</summary>
            public string Nome = "?";
            /// <summary>O registro já disse que ele não achou comida (diz de novo depois que ele comer).</summary>
            public bool avisouSemEstoque;

            // o lazer (a parte 8, a segunda fatia; MoradoresDoBunker.Lazer.cs): o cigarro, o que ele guarda no lugar, a sinuca
            // e o ponto fora dos lugares para onde o corpo vai (a raiz da tacada)
            public EstadoDoFumo fumo;
            public float fumoDesde, fumoAte, proximaTragada, ultimaTragada = -99f, semMacoAte, semPausaAte;
            public int tragadas;
            public bool noCinzeiro;
            public Loot.Item guardar;
            public Partida partida;
            public bool querSinuca, forcarSinuca;
            public bool livre;
            public Vector3 livreRaiz;
            public float livreGiro;
        }

        const float Tick = 0.5f;
        const float EntreAndares = 9f;           // o caminho de um andar ao outro pela escada (m)
        const float ParadoDemais = 45f;           // s andando sem chegar: ele vai para o papel e chega lá
        const float EsperaNaEscada = 30f;         // s no patamar com alguém vendo: depois disso, ele some mesmo assim
        const float AlcanceDaVista = 30f;         // m: até onde um jogador vê o morador sumir
        // o que tapa a vista (o cenário, a casca e as paredes do bunker, as portas)
        const int TapaAVista = (1 << 0) | (1 << 1) | (1 << 13);

        readonly List<Morador> _moradores = new List<Morador>();
        readonly Dictionary<int, int> _ocupados = new Dictionary<int, int>();   // o lugar → o id de quem está nele (ou indo)
        DadosDoMundo _dados;
        bool _iniciado, _reservaPosta;
        float _ultimaHora, _proximoTick, _iniciadoEm;
        GameObject _prefab;

        /// <summary>Os moradores (no anfitrião).</summary>
        public IReadOnlyList<Morador> Todos => _moradores;

        void Awake()
        {
            Instancia = this;
            EstantesDoBunker.Mexeu += AoMexerNaEstante;
        }

        void OnDestroy()
        {
            EstantesDoBunker.Mexeu -= AoMexerNaEstante;
            if (Instancia == this) Instancia = null;
        }

        /// <summary>As horas do jogo desde o dia 0 (o dia × 24 + a hora).</summary>
        public static float Agora => CicloDoDia.Dia * 24f + CicloDoDia.Hora;

        static BunkerDoMundo Bunker => BunkerDoMundo.Instancia;

        bool Ativo
        {
            get
            {
                var rede = NetworkManager.Singleton;
                var m = MundoDaPartida.Instancia;
                return rede != null && rede.IsServer && m != null && m.IsSpawned && m.Pronto && !m.NoDiaZero && m.Dados != null && m.Dados.TemBunker
                       && Bunker != null && Bunker.Raiz != null;
            }
        }

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var dados = mundo != null ? mundo.Dados : null;
            if (dados != _dados || (_iniciado && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)))
            {
                bool outroMundo = dados != _dados;
                Limpar();
                _dados = dados;
                if (outroMundo) EsquecerOTerminal();
            }
            // o registro e as metas do terminal (a parte 7): do save, uma vez por mundo, também no Dia 0
            var rede = NetworkManager.Singleton;
            if (!_carregado && _dados != null && rede != null && rede.IsServer) CarregarOTerminal();
            if (_carregado) AvisarAsMexidas();
            if (!Ativo) return;
            if (!_iniciado) Iniciar();
            if (!_reservaPosta && Time.unscaledTime > _iniciadoEm + 3f) PorAReservaDoComeco();

            float agora = Agora;
            // (um pulo de horas, como o dormir do save ou o Page Up, entra nas barras antes da decisão: com o pulo depois, quem
            // acordou no meio dele passava as horas da noite acordado)
            if (Time.unscaledTime >= _proximoTick || agora - _ultimaHora > 0.25f)
            {
                _proximoTick = Time.unscaledTime + Tick;
                float dh = Mathf.Clamp(agora - _ultimaHora, 0f, 12f);
                _ultimaHora = agora;
                foreach (var m in _moradores) Barras(m, dh, agora);
                HumorDeTodos(dh, agora);
                LigarOLazer();
            }
            foreach (var m in _moradores) Viver(m, agora);
            AtualizarAPartida();
            Corpos(agora);
        }

        // ------------------------------------------------------------------ o começo e o fim

        void Iniciar()
        {
            _iniciado = true;
            _reservaPosta = false;
            _iniciadoEm = Time.unscaledTime;
            _moradores.Clear();
            _ocupados.Clear();
            _prefab = RecursosDosMoradores.Carregar() != null ? RecursosDosMoradores.Carregar().corpo : null;
            if (_prefab == null) Debug.LogWarning("[Moradores] Falta o corpo dos moradores (Zombie City Cleaner > Moradores > Montar o corpo).");
            var salvo = Salvamento.Atual;
            if (salvo != null && salvo.moradores != null)
                foreach (var f in salvo.moradores) Acrescentar(f, salvo.vidaDosMoradores?.Find(v => v.id == f.id));
            DarAsCamas();
            if (_registro.Count == 0 && _moradores.Count > 0)
                Registrar($"O terminal foi ligado. {_moradores.Count} moradores no bunker: {string.Join(", ", _moradores.ConvertAll(x => x.Nome))}.");
            _ultimaHora = Agora;
            float agora = Agora;
            foreach (var m in _moradores) if (m.vida.lugar < 0 && !m.vida.noLugar) ComecarNoLugar(m, agora);
            Debug.Log($"[Moradores] {_moradores.Count} moradores no bunker ({LugaresDoBunker.Todos.Length} lugares): " +
                      string.Join(", ", _moradores.ConvertAll(m => $"{m.Nome} ({AgendaDoMorador.Nome(m.vida.Atividade)}, {Codigos[Mathf.Clamp(m.vida.andar, 0, 4)]})")));
        }

        void Acrescentar(FichaDoMorador f, VidaDoMorador salva)
        {
            var v = salva != null ? JsonUtility.FromJson<VidaDoMorador>(JsonUtility.ToJson(salva)) : new VidaDoMorador { id = f.id };
            var m = new Morador
            {
                ficha = f, vida = v, velocidade = AgendaDoMorador.Velocidade(f), acaso = new System.Random(f.id * 7919 + 17),
            };
            // o lugar de antes (o save): se não existe mais (os lugares mudaram), ele decide de novo
            if (v.lugar >= LugaresDoBunker.Todos.Length) v.lugar = -1;
            if (v.cama >= LugaresDoBunker.Todos.Length || (v.cama >= 0 && LugaresDoBunker.Todos[v.cama].tipo != TipoDoLugar.Cama)) v.cama = -1;
            if (salva != null && v.lugar >= 0 && !_ocupados.ContainsKey(v.lugar)) _ocupados[v.lugar] = f.id;
            else if (salva != null) { v.lugar = -1; v.noLugar = false; }
            _moradores.Add(m);
            Apelidos();
        }

        // o nome curto de cada um: o apelido só quando ele é só dele no bunker (um mundo de antes podia ter três "Doc")
        void Apelidos()
        {
            foreach (var m in _moradores)
            {
                string ap = m.ficha.apelido;
                bool repetido = !string.IsNullOrEmpty(ap) && _moradores.Exists(o => o != m && o.ficha.apelido == ap);
                m.Nome = string.IsNullOrEmpty(ap) || repetido ? m.ficha.nome : ap;
                if (m.corpo != null && m.corpo.IsSpawned && m.corpo.Quem.Value.nome.ToString() != m.Nome) m.corpo.Quem.Value = QuemEOMorador.Da(m.ficha, m.Nome);
            }
        }

        // a reserva do começo (a decisão 11: 7 dias para o grupo, o que eles guardaram nesses anos), uma vez por mundo
        void PorAReservaDoComeco()
        {
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned) return;
            _reservaPosta = true;
            var salvo = Salvamento.Atual;
            // os tacos, a caixa de bolas e os maços do começo (a parte 8; também num mundo de antes dela)
            PorOLazerDoComeco();
            if (_moradores.Count == 0 || (salvo != null && salvo.reservaDosMoradores)) return;
            int postos = EstoqueDoBunker.PorAReserva(_moradores.Count, 7);
            if (salvo != null) salvo.reservaDosMoradores = true;
            if (postos > 0) Registrar($"A reserva do começo entrou no estoque: {postos} itens de comida e bebida na despensa e na bancada da água.");
            Debug.Log($"[Moradores] A reserva do começo: {postos} itens na despensa e na bancada da água ({EstoqueDoBunker.Dias(_moradores.Count):F1} dias para {_moradores.Count}).");
        }

        // um morador sem vida de antes: já no lugar da atividade de agora (sentado, dormindo), sem andar até lá
        void ComecarNoLugar(Morador m, float agora)
        {
            Decidir(m, agora);
            var v = m.vida;
            if (v.lugar < 0) return;
            var l = LugaresDoBunker.Todos[v.lugar];
            v.noLugar = true;
            v.andar = l.andar;
            v.pos = l.raiz;
            v.giro = l.giro;
        }

        /// <summary>Tira os corpos e esquece os moradores (o mundo mudou, a partida acabou).</summary>
        public void Limpar()
        {
            foreach (var m in _moradores) if (m.corpo != null) Tirar(m, false);
            _moradores.Clear();
            _ocupados.Clear();
            _iniciado = false;
            _partida = null;
        }

        /// <summary>A vida de todos, para o save (cópias).</summary>
        public List<VidaDoMorador> Copia()
        {
            var lista = new List<VidaDoMorador>(_moradores.Count);
            foreach (var m in _moradores)
            {
                if (m.corpo != null) Seguir(m);
                lista.Add(JsonUtility.FromJson<VidaDoMorador>(JsonUtility.ToJson(m.vida)));
            }
            return lista;
        }

        // ------------------------------------------------------------------ as barras

        void Barras(Morador m, float dh, float agora)
        {
            var v = m.vida;
            if (dh <= 0f) return;
            bool tinhaFome = v.fome < AgendaDoMorador.Urgente, tinhaSede = v.sede < AgendaDoMorador.Urgente;
            v.fome -= AgendaDoMorador.FomePorHora * dh;
            v.sede -= AgendaDoMorador.SedePorHora * dh;
            var acao = AcoesDoCorpo.Acao(v.acao);
            bool dormindoNaCama = v.noLugar && acao == AcaoDoCorpo.Dormindo && v.lugar == v.cama;
            bool cochilando = v.noLugar && v.Atividade == AtividadeDoMorador.Cochilo && acao == AcaoDoCorpo.Sofa;
            if (dormindoNaCama) v.sono += AgendaDoMorador.SonoNaCama * dh;
            else if (v.noLugar && acao == AcaoDoCorpo.Dormindo) v.sono += AgendaDoMorador.SonoNoSofa * dh;
            else if (cochilando) v.sono += AgendaDoMorador.SonoNoSofa * dh;
            else v.sono -= AgendaDoMorador.SonoPorHora * dh;
            // a refeição: a fome e a sede recebem aos poucos o que ele tirou das vagas (a comida e a bebida das estantes)
            if (v.noLugar && acao == AcaoDoCorpo.Comendo && v.refeicaoComecou)
            {
                float k = 100f / AgendaDoMorador.Refeicao * dh;
                float a = Mathf.Min(v.aComer, k), b = Mathf.Min(v.aBeber, k);
                v.fome += a;
                v.aComer -= a;
                v.sede += b;
                v.aBeber -= b;
            }
            // a vontade de fumar (só os fumantes; o cigarro vem na parte 8): sobe e enche em 3 horas
            if (m.ficha != null && m.ficha.Tem(ListasDosMoradores.Fumante)) v.fumo += 100f / 3f * dh;
            v.fome = Mathf.Clamp(v.fome, 0f, 100f);
            v.sede = Mathf.Clamp(v.sede, 0f, 100f);
            v.sono = Mathf.Clamp(v.sono, 0f, 100f);
            v.fumo = Mathf.Clamp(v.fumo, 0f, 100f);
            v.fomeDesde = v.fome < AgendaDoMorador.Urgente ? (v.fomeDesde < 0f ? agora : v.fomeDesde) : -1f;
            v.sedeDesde = v.sede < AgendaDoMorador.Urgente ? (v.sedeDesde < 0f ? agora : v.sedeDesde) : -1f;
            // o registro: quando a fome ou a sede passam do limite (o sono não: quem deita tarde passava dele toda noite)
            if (!tinhaFome && v.fome < AgendaDoMorador.Urgente) Registrar($"{m.Nome} está com muita fome.");
            if (!tinhaSede && v.sede < AgendaDoMorador.Urgente) Registrar($"{m.Nome} está com muita sede.");
        }

        // ------------------------------------------------------------------ o que fazer

        void Viver(Morador m, float agora)
        {
            var v = m.vida;
            var at = v.Atividade;
            bool comendo = at == AtividadeDoMorador.Comer || at == AtividadeDoMorador.Buscar || at == AtividadeDoMorador.Guardar;
            bool comFome = (v.fome < AgendaDoMorador.Urgente || v.sede < AgendaDoMorador.Urgente) && agora >= v.semEstoqueAte;
            bool urgente = at != AtividadeDoMorador.Crise && v.crise == 0
                           && ((comFome && !comendo && at != AtividadeDoMorador.Dormir)
                               || (v.sono < AgendaDoMorador.SonoUrgente && at != AtividadeDoMorador.Dormir && at != AtividadeDoMorador.Cochilo && !comendo));
            // na estante: pega (ou guarda: o taco e a caixa voltando ao suporte) no meio da animação (no papel, na hora) e acaba
            // quando ela acaba
            if ((at == AtividadeDoMorador.Buscar || at == AtividadeDoMorador.Guardar) && v.noLugar)
            {
                float dura = m.corpo != null ? m.corpo.Duracao(AcoesDoCorpo.Clipe(AcoesDoCorpo.Acao(v.acao))) : 0f;
                float t = Time.time - m.chegouEm;
                if (!m.pegou && (m.corpo == null || t >= dura * 0.45f))
                {
                    if (at == AtividadeDoMorador.Guardar) GuardarNoLugar(m);
                    else Pegar(m);
                }
                if (m.pegou && (m.corpo == null || (t >= dura * 0.8f && m.corpo.EmPe) || t > dura + 1f)) v.ate = agora;
            }
            // a crise forte, na estante: joga no chão no meio da animação e acaba quando ela acaba
            if (at == AtividadeDoMorador.Crise && v.Crise == CriseDoMorador.JogarNoChao && v.noLugar && m.buscaEstante >= 0)
            {
                float dura = m.corpo != null ? m.corpo.Duracao(AcoesDoCorpo.Clipe(AcoesDoCorpo.Acao(v.acao))) : 0f;
                float t = Time.time - m.chegouEm;
                if (!m.pegou && (m.corpo == null || t >= dura * 0.45f)) JogarNoChao(m);
            }
            if (at == AtividadeDoMorador.Crise && v.Crise == CriseDoMorador.JogarNoChao && m.pegou && (m.corpo == null || m.corpo.EmPe || Time.time - m.chegouEm > 4f))
                v.criseAte = Mathf.Min(v.criseAte, agora);
            // no banco: a refeição começa (tira das vagas)
            if (at == AtividadeDoMorador.Comer && v.noLugar && !v.refeicaoComecou) ComecarARefeicao(m, agora);
            if (agora >= v.ate || urgente || (v.lugar < 0 && !v.noLugar)) Decidir(m, agora);
            Fumar(m, agora);
            MaoNoCorpo(m);
            if (m.corpo == null) NoPapel(m, agora);
            else ComCorpo(m);
        }

        /// <summary>A atividade de agora (a agenda, com as necessidades na frente) e o lugar dela.</summary>
        void Decidir(Morador m, float agora)
        {
            var v = m.vida;
            var f = m.ficha;
            // a crise passa na frente da agenda (a comilança vai pelo comer, logo abaixo)
            if (v.crise != 0 && v.Crise != CriseDoMorador.Comilanca && DecidirACrise(m, agora)) return;
            var at = AgendaDoMorador.Programada(f, CicloDoDia.Hora, out float dura);
            // (o F1 manda jogar sinuca agora)
            if (m.forcarSinuca && at != AtividadeDoMorador.Dormir) at = AtividadeDoMorador.Lazer;
            var programada = at;
            bool procura = agora >= v.semEstoqueAte;
            // (com muita fome ou sede, ele come antes de dormir; quem já está dormindo não acorda: o Viver não decide de novo
            // no meio do sono)
            if (procura && (v.fome < AgendaDoMorador.Urgente || v.sede < AgendaDoMorador.Urgente)) at = AtividadeDoMorador.Comer;
            else if (at == AtividadeDoMorador.Comer && ((v.fome >= AgendaDoMorador.ComeAbaixo && v.sede >= AgendaDoMorador.ComeAbaixo) || !procura)) at = AtividadeDoMorador.Lazer;
            else if (at != AtividadeDoMorador.Dormir && at != AtividadeDoMorador.Cochilo && v.sono < AgendaDoMorador.SonoUrgente)
            {
                at = AtividadeDoMorador.Cochilo;
                dura = 1.5f;
            }
            bool comilanca = v.Crise == CriseDoMorador.Comilanca;
            if (comilanca) at = AtividadeDoMorador.Comer;
            if (at == AtividadeDoMorador.Comer)
            {
                // o que falta nas vagas ele vai buscar no estoque (a comida e a bebida, uma de cada vez); com o que tem,
                // ele senta e come; sem nada, fica para depois (2 horas), e volta à agenda (o lazer na hora da refeição)
                if (BuscarOQueFalta(m, agora)) return;
                bool temAlgo = ((v.fome < AgendaDoMorador.ComeAbaixo || comilanca) && Vaga(m, EstoqueDoBunker.EhComida) >= 0)
                               || (v.sede < AgendaDoMorador.ComeAbaixo && Vaga(m, EstoqueDoBunker.EhBebida) >= 0);
                if (temAlgo) dura = comilanca ? AgendaDoMorador.Refeicao * 1.5f : AgendaDoMorador.Refeicao;
                else if (comilanca)
                {
                    // nada para comer: a crise vira andar sem rumo
                    v.crise = (byte)CriseDoMorador.Vagar;
                    if (DecidirACrise(m, agora)) return;
                }
                else
                {
                    if (!m.avisouSemEstoque)
                    {
                        m.avisouSemEstoque = true;
                        bool semComida = v.fome < AgendaDoMorador.ComeAbaixo, semBebida = v.sede < AgendaDoMorador.ComeAbaixo;
                        Registrar($"{m.Nome} não achou {(semComida && semBebida ? "comida nem água" : semComida ? "comida" : "água")} no estoque.");
                    }
                    v.semEstoqueAte = agora + 2f;
                    at = programada == AtividadeDoMorador.Comer ? AtividadeDoMorador.Lazer : programada;
                    if (at == AtividadeDoMorador.Lazer) dura = 1f;
                    else AgendaDoMorador.Programada(f, CicloDoDia.Hora, out dura);
                }
            }
            // (o lazer, a parte 8) sem fome nem sede urgentes: o que sobrou da sinuca volta ao suporte, e o fumante sem maço
            // vai buscar um
            bool calmo = at != AtividadeDoMorador.Dormir && at != AtividadeDoMorador.Comer && at != AtividadeDoMorador.Cochilo
                         && v.fome >= AgendaDoMorador.Urgente && v.sede >= AgendaDoMorador.Urgente;
            // (a partida de sinuca dele acabou: sai dela antes, para o taco e a caixa voltarem ao suporte já)
            if (m.partida != null && !m.forcarSinuca) SairDaPartida(m, agora);
            if (calmo && at != AtividadeDoMorador.Lazer) m.querSinuca = false;
            if (calmo && !m.querSinuca && !m.forcarSinuca && (Devolver(m, agora) || BuscarOMaco(m, agora))) return;
            // o trabalho e o lazer em pedaços de 1 a 2 horas (cada um num lugar)
            if (at == AtividadeDoMorador.Trabalho || at == AtividadeDoMorador.Lazer) dura = Mathf.Min(dura, 1f + (float)m.acaso.NextDouble());
            // a sinuca, no lazer (ou buscar o taco e a caixa de bolas para ela)
            if (at == AtividadeDoMorador.Lazer && calmo && QuerSinuca(m, agora, dura)) return;
            var (lugar, acao) = Escolher(m, at);
            Mudar(m, at, lugar, acao, agora + dura, agora);
        }

        (int lugar, byte acao) Escolher(Morador m, AtividadeDoMorador at)
        {
            var v = m.vida;
            int l;
            switch (at)
            {
                case AtividadeDoMorador.Dormir:
                    if (v.cama >= 0 && Livre(v.cama, m)) return (v.cama, LugaresDoBunker.Todos[v.cama].Acao);
                    return (Livre(m, TipoDoLugar.Sofa, 1), (byte)AcaoDoCorpo.Sofa);
                case AtividadeDoMorador.Cochilo:
                    return (Livre(m, TipoDoLugar.Sofa, 1), (byte)AcaoDoCorpo.Sofa);
                case AtividadeDoMorador.Comer:
                    return (Livre(m, TipoDoLugar.Banco, 2), (byte)AcaoDoCorpo.Comendo);
                case AtividadeDoMorador.Banheiro:
                    l = Livre(m, TipoDoLugar.Espelho, 3);
                    if (l >= 0) return (l, (byte)AcaoDoCorpo.Espelho);
                    break;
                case AtividadeDoMorador.Rezar:
                    l = Assento(m);
                    if (l >= 0) return (l, (byte)AcaoDoCorpo.Rezando);
                    break;
                case AtividadeDoMorador.Trabalho:
                {
                    // os afazeres (sem as funções, que vêm na parte 9): lavar a louça, consertar nas bancadas do B0 (a do
                    // arsenal e a da fabricação), dobrar a roupa na lavanderia e na rouparia (a parte 8) ou ler
                    double r = m.acaso.NextDouble();
                    if (r < 0.3) { l = Livre(m, TipoDoLugar.Pia, 2); if (l >= 0) return (l, (byte)AcaoDoCorpo.LavandoALouca); }
                    if (r < 0.55) { l = Livre(m, TipoDoLugar.Bancada, 0); if (l >= 0) return (l, (byte)AcaoDoCorpo.Consertando); }
                    if (r < 0.85) { l = Livre(m, TipoDoLugar.Dobrar, 3); if (l >= 0) return (l, (byte)AcaoDoCorpo.DobrandoRoupa); }
                    l = Assento(m);
                    if (l >= 0) return (l, (byte)AcaoDoCorpo.Lendo);
                    break;
                }
                case AtividadeDoMorador.Lazer:
                {
                    // triste, ele senta e fica triste (no lugar da TV ou do livro), muitas vezes
                    if (HumorDoMorador.Faixa(v.humor) == FaixaDoHumor.Triste && m.acaso.NextDouble() < 0.6)
                    {
                        l = Assento(m);
                        if (l >= 0) return (l, (byte)AcaoDoCorpo.Triste);
                    }
                    // o fumante com vontade e com um maço: o canto do fumo (em pé dos lados da mesinha, com o cinzeiro; sem maço,
                    // ele ficava lá parado com vontade)
                    if (Fumante(m) && v.fumo > 60f && Vaga(m, Loot.Itens.EhFumo) >= 0 && m.acaso.NextDouble() < 0.6)
                    {
                        l = NoCantoDoFumo(m);
                        if (l >= 0) return (l, LugaresDoBunker.Todos[l].Acao);
                    }
                    // a TV (os sofás de frente para ela), o rádio das operações, ler na cadeira ou ficar em pé na sala (a sinuca
                    // vem antes, no Decidir; a conversa, com as falas)
                    double r = m.acaso.NextDouble();
                    if (r < 0.4) { l = DaTV(m); if (l >= 0) return (l, (byte)AcaoDoCorpo.Sofa); }
                    if (r < 0.55) { l = Livre(m, TipoDoLugar.Radio, 0); if (l >= 0) return (l, (byte)AcaoDoCorpo.Cadeira); }
                    if (r < 0.85) { l = Assento(m); if (l >= 0) return (l, (byte)AcaoDoCorpo.Lendo); }
                    break;
                }
            }
            l = Livre(m, TipoDoLugar.EmPe, 1, LugaresDoBunker.DeFicar);
            if (l < 0) l = Livre(m, TipoDoLugar.Sofa, 1);
            return (l, l >= 0 ? LugaresDoBunker.Todos[l].Acao : (byte)0);
        }

        // um assento para ler e rezar (as ações da cadeira): a cadeira solta e, sem ela, a beira do sofá (as cadeiras do
        // bunker ficam todas empurradas para baixo das mesas; a animação da cadeira cabe no sofá, uns 5 cm afundada)
        int Assento(Morador m)
        {
            int l = Livre(m, TipoDoLugar.Cadeira, 1);
            return l >= 0 ? l : Livre(m, TipoDoLugar.Sofa, 1);
        }

        bool Livre(int lugar, Morador m) => !_ocupados.TryGetValue(lugar, out int quem) || quem == m.ficha.id;

        // um lugar livre do tipo (de preferência no andar; o de agora fica, se for do tipo), ou -1; 'serve' filtra (o em pé do
        // lazer só nos cômodos de ficar à toa)
        int Livre(Morador m, TipoDoLugar tipo, int andar, System.Func<LugarDoMorador, bool> serve = null)
        {
            var v = m.vida;
            if (v.lugar >= 0 && LugaresDoBunker.Todos[v.lugar].tipo == tipo && (andar < 0 || LugaresDoBunker.Todos[v.lugar].andar == andar)
                && (serve == null || serve(LugaresDoBunker.Todos[v.lugar]))) return v.lugar;
            var noAndar = new List<int>();
            var outros = new List<int>();
            foreach (int i in LugaresDoBunker.DoTipo(tipo))
            {
                if (!Livre(i, m)) continue;
                // as camas são de cada um (não se senta na cama dos outros)
                if (tipo == TipoDoLugar.Cama) continue;
                if (serve != null && !serve(LugaresDoBunker.Todos[i])) continue;
                (LugaresDoBunker.Todos[i].andar == andar ? noAndar : outros).Add(i);
            }
            var lista = noAndar.Count > 0 ? noAndar : outros;
            return lista.Count == 0 ? -1 : lista[m.acaso.Next(lista.Count)];
        }

        // a nova atividade: o mesmo lugar (só a ação, como ler na mesma cadeira) ou outro (levantar e ir)
        void Mudar(Morador m, AtividadeDoMorador at, int lugar, byte acao, float ate, float agora)
        {
            var v = m.vida;
            if (v.Atividade == AtividadeDoMorador.Dormir && at != AtividadeDoMorador.Dormir) Acordou(m, agora);
            if (v.Atividade == AtividadeDoMorador.Sinuca && at != AtividadeDoMorador.Sinuca) SairDaPartida(m, agora);
            m.livre = false;
            if (v.refeicaoComecou && at != AtividadeDoMorador.Comer && v.Crise == CriseDoMorador.Comilanca) v.criseAte = Mathf.Min(v.criseAte, agora);
            if (v.refeicaoComecou && at != AtividadeDoMorador.Comer)
            {
                v.fome = Mathf.Clamp(v.fome + v.aComer, 0f, 100f);
                v.sede = Mathf.Clamp(v.sede + v.aBeber, 0f, 100f);
                v.aComer = v.aBeber = 0f;
                v.refeicaoComecou = false;
            }
            v.atividade = (byte)at;
            v.ate = ate;
            // o mesmo lugar na mesma postura: só a ação (ler na mesma cadeira); noutra postura (ler no sofá onde via TV),
            // ele levanta e senta de novo
            bool mesmaPostura = AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(v.acao)) == AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(acao));
            if (lugar >= 0 && lugar == v.lugar && mesmaPostura)
            {
                if (v.acao != acao)
                {
                    v.acao = acao;
                    if (m.corpo != null && v.noLugar && m.passo == Passo.NoLugar) m.corpo.Acao.Value = acao;
                }
                return;
            }
            if (v.lugar >= 0 && _ocupados.TryGetValue(v.lugar, out int quem) && quem == m.ficha.id) _ocupados.Remove(v.lugar);
            if (lugar >= 0) _ocupados[lugar] = m.ficha.id;
            bool estavaSentado = v.noLugar && AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(v.acao)) != PosturaDoCorpo.EmPe;
            v.lugar = lugar;
            v.acao = acao;
            v.noLugar = lugar < 0;
            if (m.corpo != null)
            {
                Seguir(m);
                if (estavaSentado || m.corpo.Acomodando)
                {
                    m.corpo.Levantar();
                    m.passo = Passo.Levantando;
                }
                else
                {
                    if (AcoesDoCorpo.Postura(AcoesDoCorpo.Acao(m.corpo.Acao.Value)) != PosturaDoCorpo.EmPe) m.corpo.Levantar();
                    m.passo = Passo.Levantando;
                }
                m.desde = Time.time;
            }
            else if (lugar >= 0) PlanejarNoPapel(m, agora);
        }

        // ------------------------------------------------------------------ no papel

        void PlanejarNoPapel(Morador m, float agora)
        {
            var v = m.vida;
            var l = LugaresDoBunker.Todos[v.lugar];
            // sem a pressa do sono (07/11/2026, da develop: o HorasPorSegundo corre junto com ela): andar uns metros leva
            // as mesmas horas do jogo com o relógio rápido ou não
            float hps = CicloDoDia.HorasPorSegundo / Mathf.Max(1f, CicloDoDia.Pressa), vel = Mathf.Max(0.3f, m.velocidade);
            if (l.andar == v.andar)
            {
                float d = Plano(l.raiz - v.pos) * 1.3f + 1f;
                v.chegaNaEscada = agora;
                v.chegaNoLugar = agora + d / vel * hps;
            }
            else
            {
                float d1 = Plano(v.pos - LugaresDoBunker.NaEscada(v.andar)), d2 = Plano(LugaresDoBunker.NaEscada(l.andar) - l.raiz);
                float ds = Mathf.Abs(l.andar - v.andar) * EntreAndares;
                v.chegaNaEscada = agora + (d1 * 1.3f + ds) / vel * hps;
                v.chegaNoLugar = v.chegaNaEscada + (d2 * 1.3f + 1f) / vel * hps;
            }
        }

        void NoPapel(Morador m, float agora)
        {
            var v = m.vida;
            if (v.noLugar || v.lugar < 0) return;
            var l = LugaresDoBunker.Todos[v.lugar];
            // passou da escada: já está no andar do lugar (a porta da escada dele)
            if (l.andar != v.andar && agora >= v.chegaNaEscada)
            {
                v.andar = l.andar;
                v.pos = LugaresDoBunker.NaEscada(l.andar);
            }
            if (agora >= v.chegaNoLugar)
            {
                v.noLugar = true;
                v.andar = l.andar;
                v.pos = l.raiz;
                v.giro = l.giro;
            }
        }

        static float Plano(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        // ------------------------------------------------------------------ com o corpo

        Vector3 Mundo(Vector3 local) => Bunker.Raiz.TransformPoint(local);
        Vector3 Local(Vector3 mundo) => Bunker.Raiz.InverseTransformPoint(mundo);
        float GiroNoMundo(float giro) => Bunker.Raiz.eulerAngles.y + giro;

        // a vida segue o corpo: o andar e o ponto
        void Seguir(Morador m)
        {
            if (m.corpo == null) return;
            var q = Local(m.corpo.transform.position);
            m.vida.andar = AndarEm(q.y + 0.05f);
            m.vida.pos = new Vector3(q.x, Base(m.vida.andar), q.z);
            m.vida.giro = Mathf.Repeat(m.corpo.transform.eulerAngles.y - Bunker.Raiz.eulerAngles.y, 360f);
        }

        void ComCorpo(Morador m)
        {
            var v = m.vida;
            var c = m.corpo;
            Seguir(m);
            switch (m.passo)
            {
                case Passo.NoLugar:
                    if (!v.noLugar && v.lugar >= 0) { m.passo = Passo.Levantando; m.desde = Time.time; }
                    break;
                case Passo.Levantando:
                    if (!c.EmPe) break;
                    if (v.lugar < 0)
                    {
                        v.noLugar = true;
                        m.passo = Passo.NoLugar;
                        break;
                    }
                    if (!c.LigarOAgente())
                    {
                        // sem NavMesh embaixo (ele levantou num lugar fora dele): vai para o papel
                        Tirar(m, true);
                        break;
                    }
                    Rota(m);
                    break;
                case Passo.Indo:
                {
                    var l = LugaresDoBunker.Todos[v.lugar];
                    // o caminho até o lugar ficou desmontado (um andar no meio saiu): vai até a escada
                    if (!m.destinoEhEscada && !m.livre && l.andar != v.andar && !CaminhoMontado(v.andar, l.andar)) Rota(m);
                    // o caminho montou (alguém foi atrás dele e os andares carregaram): segue até o lugar, sem sumir
                    else if (m.destinoEhEscada && !m.livre && CaminhoMontado(v.andar, l.andar)) Rota(m);
                    var alvo = Mundo(m.destino);
                    c.Ir(alvo, m.velocidade);
                    if (c.Chegou(alvo, m.livre ? 0.25f : 0.45f))
                    {
                        if (m.destinoEhEscada)
                        {
                            // no patamar do andar do lugar (não montado): some quando ninguém vê; na frente de alguém, espera
                            // (o andar pode montar com quem vem atrás) até um tempo
                            if (m.chegouNaEscada < 0f) m.chegouNaEscada = Time.time;
                            if (!AlguemVe(c.transform.position + Vector3.up * 1.3f) || Time.time - m.chegouNaEscada > EsperaNaEscada) Tirar(m, true);
                        }
                        else
                        {
                            // (o ponto fora dos lugares: a raiz da tacada, em pé)
                            if (m.livre) c.Acomodar(Mundo(m.livreRaiz), GiroNoMundo(m.livreGiro), (byte)AcaoDoCorpo.Andando);
                            else c.Acomodar(Mundo(l.raiz), GiroNoMundo(l.giro), v.acao);
                            m.passo = Passo.Acomodando;
                        }
                    }
                    else if (Time.time - m.desde > ParadoDemais || c.SemCaminho) Tirar(m, true);
                    break;
                }
                case Passo.Acomodando:
                    if (!c.Acomodando)
                    {
                        v.noLugar = true;
                        m.passo = Passo.NoLugar;
                        m.chegouEm = Time.time;
                    }
                    break;
            }
        }

        // para onde o corpo anda: o lugar (no mesmo andar, ou com os andares do caminho montados) ou a porta da escada
        void Rota(Morador m)
        {
            var v = m.vida;
            var l = LugaresDoBunker.Todos[v.lugar];
            if (m.livre)
            {
                m.destino = m.livreRaiz;
                m.destinoEhEscada = false;
            }
            else if (l.andar == v.andar || CaminhoMontado(v.andar, l.andar))
            {
                m.destino = l.raiz;
                m.destinoEhEscada = false;
            }
            else
            {
                // pela escada (a casca, sempre montada) até o patamar do andar do lugar: antes, ele sumia na porta da escada
                // do andar dele, e quem vinha atrás o via sumir do nada (07/11/2026)
                m.destino = LugaresDoBunker.NoPatamar(l.andar);
                m.destinoEhEscada = true;
            }
            m.chegouNaEscada = -1f;
            m.passo = Passo.Indo;
            m.desde = Time.time;
        }

        /// <summary>Um jogador vê o ponto (perto, sem parede no meio): o morador não some na frente dele.</summary>
        static bool AlguemVe(Vector3 ponto)
        {
            foreach (var j in Player.JogadorRede.Todos)
            {
                if (j == null) continue;
                var olho = j.transform.position + Vector3.up * 1.6f;
                if ((ponto - olho).sqrMagnitude > AlcanceDaVista * AlcanceDaVista) continue;
                if (!Physics.Linecast(olho, ponto, TapaAVista, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        /// <summary>O ponto (no mundo) está dentro da caixa da escada (a casca: os lances e os patamares de todos os andares).</summary>
        bool NaCaixaDaEscada(Vector3 mundo)
        {
            var q = Local(mundo);
            return q.x > Escada.xMin - 0.2f && q.x < Escada.xMax + 0.2f && q.z > Escada.yMin - 0.2f && q.z < Escada.yMax + 0.2f && q.y < TopoDaCaixa;
        }

        static bool CaminhoMontado(int de, int ate)
        {
            for (int f = Mathf.Min(de, ate); f <= Mathf.Max(de, ate); f++) if (!Bunker.AndarPronto(f)) return false;
            return true;
        }

        // ------------------------------------------------------------------ pôr e tirar os corpos

        void Corpos(float agora)
        {
            if (_prefab == null) return;
            bool navMesh = NavMeshDoMundo.Instancia != null && NavMeshDoMundo.Instancia.Pronto;
            bool pos = false;
            foreach (var m in _moradores)
            {
                var v = m.vida;
                if (m.corpo != null)
                {
                    if (!m.corpo.IsSpawned) { m.corpo = null; continue; }
                    // (na escada, indo, ele fica: a escada é da casca, montada com o bunker)
                    if (!Bunker.AndarPronto(v.andar) && !(m.passo == Passo.Indo && NaCaixaDaEscada(m.corpo.transform.position))) Tirar(m, true);
                    continue;
                }
                if (pos || !navMesh || Time.time < m.proximaTentativa) continue;
                if (v.noLugar || v.lugar < 0)
                {
                    if (!Bunker.AndarPronto(v.andar)) continue;
                    var l = v.lugar >= 0 ? LugaresDoBunker.Todos[v.lugar] : default;
                    var raiz = v.lugar >= 0 ? l.raiz : v.pos;
                    float giro = v.lugar >= 0 ? l.giro : v.giro;
                    pos = Por(m, raiz, giro, v.lugar >= 0 ? v.acao : (byte)0, true);
                }
                else
                {
                    // andando no papel, já no andar do lugar (a mesma coisa, ou depois da escada) e ele montado: o corpo
                    // aparece no caminho e anda o resto
                    var l = LugaresDoBunker.Todos[v.lugar];
                    if (v.andar != l.andar || !Bunker.AndarPronto(l.andar)) continue;
                    float t = Mathf.InverseLerp(v.chegaNaEscada, v.chegaNoLugar, agora);
                    var p = Vector3.Lerp(v.pos, l.raiz, t * 0.85f);
                    pos = Por(m, p, v.giro, 0, false);
                    if (pos)
                    {
                        Seguir(m);
                        Rota(m);
                    }
                }
                if (!pos) m.proximaTentativa = Time.time + 1.5f;
            }
        }

        // o corpo novo: no lugar já acomodado (sentado, deitado), ou em pé no caminho (com o agente no NavMesh)
        bool Por(Morador m, Vector3 local, float giro, byte acao, bool noLugar)
        {
            var go = Instantiate(_prefab, Mundo(local), Quaternion.Euler(0f, GiroNoMundo(giro), 0f));
            var c = go.GetComponent<CorpoDoMorador>();
            c.Preparar(QuemEOMorador.Da(m.ficha, m.Nome), noLugar ? acao : (byte)0);
            go.GetComponent<NetworkObject>().Spawn(true);
            if (!noLugar && !c.LigarOAgente())
            {
                go.GetComponent<NetworkObject>().Despawn(true);
                return false;
            }
            m.corpo = c;
            m.passo = noLugar ? Passo.NoLugar : Passo.Indo;
            m.desde = m.chegouEm = Time.time;
            // o que está na mão (o cigarro, o taco)
            MaoNoCorpo(m);
            return true;
        }

        // tira o corpo; 'papel': ele segue andando no papel de onde está (até o lugar)
        void Tirar(Morador m, bool papel)
        {
            var c = m.corpo;
            if (c == null) return;
            Seguir(m);
            m.corpo = null;
            m.passo = Passo.NoLugar;
            m.livre = false;
            m.proximaTentativa = Time.time + 3f;
            if (c.IsSpawned) c.NetworkObject.Despawn(true);
            else Destroy(c.gameObject);
            if (!papel) return;
            var v = m.vida;
            if (!v.noLugar && v.lugar >= 0) PlanejarNoPapel(m, Agora);
        }

        // ------------------------------------------------------------------ o estoque e as vagas (a segunda fatia)

        /// <summary>A primeira vaga com um item que serve (o índice em <c>vida.vagas</c>), ou -1.</summary>
        static int Vaga(Morador m, System.Func<Loot.Item, bool> serve)
        {
            var vagas = m.vida.vagas;
            for (int i = 0; i < vagas.Count; i++) if (vagas[i].qtd > 0 && serve((Loot.Item)vagas[i].item)) return i;
            return -1;
        }

        bool CabeNasVagas(Morador m) => m.vida.vagas.Count < AgendaDoMorador.Vagas(m.ficha);

        /// <summary>Quantos itens que servem ele leva nas vagas (as pilhas contam cada um).</summary>
        static int Quantas(Morador m, System.Func<Loot.Item, bool> serve)
        {
            int n = 0;
            foreach (var g in m.vida.vagas) if (g.qtd > 0 && serve((Loot.Item)g.item)) n += g.qtd;
            return n;
        }

        // o que falta para a refeição (a comida com a fome abaixo de 60, a bebida com a sede): a busca no estoque mais perto
        bool BuscarOQueFalta(Morador m, float agora)
        {
            var v = m.vida;
            bool comilanca = v.Crise == CriseDoMorador.Comilanca;
            bool faltaComida = comilanca ? Quantas(m, EstoqueDoBunker.EhComida) < 3 : v.fome < AgendaDoMorador.ComeAbaixo && Vaga(m, EstoqueDoBunker.EhComida) < 0;
            bool faltaBebida = v.sede < AgendaDoMorador.ComeAbaixo && Vaga(m, EstoqueDoBunker.EhBebida) < 0;
            if ((!faltaComida && !faltaBebida) || !CabeNasVagas(m)) return false;
            foreach (var quer in new System.Func<Loot.Item, bool>[] { faltaComida ? EstoqueDoBunker.EhComida : null, faltaBebida ? EstoqueDoBunker.EhBebida : null })
            {
                if (quer == null) continue;
                if (!EstoqueDoBunker.Procurar(quer, v.andar, v.pos, k => Livre(k, m), out var it, out int lugar)) continue;
                m.buscaEstante = it.estante;
                m.buscaId = it.id;
                m.pegou = false;
                m.chegouEm = Time.time;
                var acao = (byte)AcoesDoCorpo.NaEstante(EstantesDoBunker.AlturaDe(it));
                Mudar(m, AtividadeDoMorador.Buscar, lugar, acao, agora + 1f, agora);
                return true;
            }
            return false;
        }

        // na estante: tira um do item e põe nas vagas (com a mesma pilha, se tem; se o item sumiu, nada). O maço vai inteiro
        // (o lazer: ele fuma dos cigarros dele)
        void Pegar(Morador m)
        {
            m.pegou = true;
            var e = EstantesDoBunker.Instancia;
            if (e != null && m.buscaEstante >= 0 && e.Achar(m.buscaEstante, m.buscaId, out var achado) && Loot.Itens.EhFumo(achado.item))
            {
                if (e.TirarOItem(m.buscaEstante, m.buscaId, out var maco, out int cigarros))
                    m.vida.vagas.Add(new VagaDoMorador { item = (int)maco, qtd = Mathf.Max(1, cigarros) });
                return;
            }
            if (e == null || m.buscaEstante < 0 || !e.TirarUm(m.buscaEstante, m.buscaId, out var item)) return;
            var vagas = m.vida.vagas;
            foreach (var g in vagas)
                if (g.item == (int)item && g.qtd < Loot.Itens.Pilha(item))
                {
                    g.qtd++;
                    return;
                }
            vagas.Add(new VagaDoMorador { item = (int)item, qtd = 1 });
        }

        // tira da vaga um do item (a vaga vazia sai)
        static Loot.DadosDoLoot Gastar(Morador m, int vaga)
        {
            var g = m.vida.vagas[vaga];
            var dados = Loot.Itens.Loot((Loot.Item)g.item);
            if (--g.qtd <= 0) m.vida.vagas.RemoveAt(vaga);
            return dados;
        }

        // sentado no banco: a comida e a bebida saem das vagas, e o que elas dão vai entrando na fome e na sede
        void ComecarARefeicao(Morador m, float agora)
        {
            var v = m.vida;
            v.refeicaoComecou = true;
            v.aComer = v.aBeber = 0f;
            string comeu = null, bebeu = null;
            if (v.Crise == CriseDoMorador.Comilanca)
            {
                int n = 0;
                for (int c = Vaga(m, EstoqueDoBunker.EhComida); c >= 0 && n < 3; c = Vaga(m, EstoqueDoBunker.EhComida), n++)
                {
                    var d = Gastar(m, c);
                    if (d != null) { v.aComer += d.fome; v.aBeber += d.sede; }
                }
                if (n > 0) Registrar($"{m.Nome} comeu {(n == 1 ? "uma lata" : n == 2 ? "duas latas" : "três latas")} de uma vez, sem fome.");
            }
            else if (v.fome < AgendaDoMorador.ComeAbaixo)
            {
                int c = Vaga(m, EstoqueDoBunker.EhComida);
                var d = c >= 0 ? Gastar(m, c) : null;
                if (d != null) { v.aComer += d.fome; v.aBeber += d.sede; comeu = d.nomeComArtigo; }
            }
            if (v.sede < AgendaDoMorador.ComeAbaixo)
            {
                int b = Vaga(m, EstoqueDoBunker.EhBebida);
                var d = b >= 0 ? Gastar(m, b) : null;
                if (d != null) { v.aComer += d.fome; v.aBeber += d.sede; bebeu = d.nomeComArtigo; }
            }
            if (comeu != null || bebeu != null)
            {
                m.avisouSemEstoque = false;
                Registrar($"{m.Nome} {(comeu != null ? "comeu " + comeu : "")}{(comeu != null && bebeu != null ? " e " : "")}{(bebeu != null ? "bebeu " + bebeu : "")}.");
            }
            if (v.aComer == 0f && v.aBeber == 0f) v.ate = agora;
        }

        /// <summary>No anfitrião, o F1: o estoque (o que eles alcançam) e quantos dias ele dura.</summary>
        public string ResumoDoEstoque()
        {
            var (comidas, fome, bebidas, sede) = EstoqueDoBunker.Contar();
            return $"Estoque: {comidas} comidas (+{fome:F0} de fome), {bebidas} bebidas (+{sede:F0} de sede): " +
                   $"{EstoqueDoBunker.Dias(_moradores.Count):F1} dias para {_moradores.Count}\n" +
                   $"   {EstoqueDoBunker.Nome(TipoDoEstoque.Despensa)} {EstoqueDoBunker.Tem(TipoDoEstoque.Despensa)}/{EstoqueDoBunker.Meta(TipoDoEstoque.Despensa)}" +
                   $"   {EstoqueDoBunker.Nome(TipoDoEstoque.Agua)} {EstoqueDoBunker.Tem(TipoDoEstoque.Agua)}/{EstoqueDoBunker.Meta(TipoDoEstoque.Agua)}";
        }

        /// <summary>No anfitrião, o F1: mais 7 dias de comida e água para o grupo (a reserva de novo).</summary>
        public int EncherOEstoque()
        {
            int n = EstoqueDoBunker.PorAReserva(Mathf.Max(1, _moradores.Count), 7);
            if (n > 0) Registrar($"Chegaram {n} itens de comida e bebida ao estoque.");
            return n;
        }

        // ------------------------------------------------------------------ as camas

        /// <summary>
        /// A cama de cada um: quem já tem fica com ela; os casais nas camas de casal (os quartos 5 e 6 primeiro, um de cada
        /// lado), o parente da família sem casal numa cama de casal livre dos quartos 5 e 6, e os outros na cama de baixo
        /// dos beliches, depois nas de solteiro e no que sobrar. Um móvel só tem um dono (menos a cama de casal, dois).
        /// </summary>
        public void DarAsCamas()
        {
            var todos = LugaresDoBunker.Todos;
            var donos = new Dictionary<int, List<int>>();   // o móvel → os lugares tomados
            int Tomados(int movel) => donos.TryGetValue(movel, out var l) ? l.Count : 0;
            bool Pode(int lugar, bool casal)
            {
                var l = todos[lugar];
                bool ehCasal = MoveisDoBunker.Todos[l.movel].modelo == "CamaCasal";
                if (donos.TryGetValue(l.movel, out var ja) && ja.Contains(lugar)) return false;
                if (!(ehCasal ? Tomados(l.movel) < 2 && (casal || Tomados(l.movel) == 0) : Tomados(l.movel) == 0)) return false;
                // longe das camas dos outros (nos beliches, dois lados de vizinhos davam no mesmo vão de 64 cm)
                foreach (var outros in donos.Values)
                    foreach (int o in outros)
                        if (todos[o].movel != l.movel && Plano(todos[o].raiz - l.raiz) < 0.9f) return false;
                return true;
            }
            void Dar(Morador m, int lugar)
            {
                m.vida.cama = lugar;
                var l = todos[lugar];
                if (!donos.TryGetValue(l.movel, out var lista)) donos[l.movel] = lista = new List<int>();
                lista.Add(lugar);
            }
            var camas = new List<int>(LugaresDoBunker.DoTipo(TipoDoLugar.Cama));
            foreach (var m in _moradores)
                if (m.vida.cama >= 0 && Pode(m.vida.cama, true)) Dar(m, m.vida.cama);
                else m.vida.cama = -1;
            int Ordem(int lugar)
            {
                var l = todos[lugar];
                string modelo = MoveisDoBunker.Todos[l.movel].modelo;
                return l.nome == "Quarto 5" || l.nome == "Quarto 6" ? 0 : modelo == "CamaCasal" ? 1 : modelo == "Beliche" ? 2 : modelo == "CamaSolteiro" ? 3 : 4;
            }
            camas.Sort((a, b) => Ordem(a).CompareTo(Ordem(b)));
            // os casais: os dois lados da mesma cama de casal
            foreach (var m in _moradores)
            {
                if (m.vida.cama >= 0) continue;
                var laco = m.ficha.lacos.Find(x => x.tipo == LacoDoMorador.Casal);
                var outro = laco != null ? _moradores.Find(o => o.ficha.id == laco.outro) : null;
                if (outro == null || outro.vida.cama >= 0) continue;
                foreach (int a in camas)
                {
                    if (MoveisDoBunker.Todos[todos[a].movel].modelo != "CamaCasal" || Tomados(todos[a].movel) > 0) continue;
                    int ib = camas.FindIndex(x => x != a && todos[x].movel == todos[a].movel);
                    if (ib < 0) continue;
                    Dar(m, a);
                    Dar(outro, camas[ib]);
                    break;
                }
            }
            // o parente sem casal: uma cama de casal dos quartos 5 e 6
            foreach (var m in _moradores)
            {
                if (m.vida.cama >= 0 || m.ficha.parente == ParenteDaFamilia.Nenhum) continue;
                foreach (int a in camas)
                    if (Ordem(a) == 0 && Pode(a, false)) { Dar(m, a); break; }
            }
            // os outros: os beliches, as de solteiro e o que sobrar
            foreach (var m in _moradores)
            {
                if (m.vida.cama >= 0) continue;
                foreach (int a in camas)
                    if (Ordem(a) >= 2 && Pode(a, false)) { Dar(m, a); break; }
                if (m.vida.cama >= 0) continue;
                foreach (int a in camas)
                    if (Pode(a, false)) { Dar(m, a); break; }
            }
        }

        // ------------------------------------------------------------------ os testes (o F1 BUNKER)

        /// <summary>No anfitrião: as barras de todos (fome, sede e sono) num valor, ou cheias.</summary>
        public void Barras(float fome, float sede, float sono)
        {
            foreach (var m in _moradores)
            {
                if (fome >= 0f) m.vida.fome = fome;
                if (sede >= 0f) m.vida.sede = sede;
                if (sono >= 0f) m.vida.sono = sono;
            }
        }

        /// <summary>No anfitrião: põe um morador sorteado (no mundo salvo também, se houver), já no lugar da atividade de agora.</summary>
        public FichaDoMorador PorUmSorteado()
        {
            if (!_iniciado) return null;
            var salvo = Salvamento.Atual;
            int id = salvo != null ? Mathf.Max(salvo.proximoMorador, 1) : 1000 + _moradores.Count;
            foreach (var m in _moradores) id = Mathf.Max(id, m.ficha.id + 1);
            var usados = _moradores.ConvertAll(x => x.ficha.nome);
            var f = SorteioDosMoradores.SortearUm(Random.Range(1, int.MaxValue), id, usados);
            if (salvo != null)
            {
                salvo.moradores.Add(f);
                salvo.proximoMorador = id + 1;
            }
            Acrescentar(f, null);
            DarAsCamas();
            ComecarNoLugar(_moradores[_moradores.Count - 1], Agora);
            Registrar($"{_moradores[_moradores.Count - 1].Nome} chegou ao bunker.");
            return f;
        }

        /// <summary>No anfitrião: tira o último morador (do mundo salvo também).</summary>
        public void TirarOUltimo()
        {
            if (_moradores.Count == 0) return;
            var m = _moradores[_moradores.Count - 1];
            if (m.corpo != null) Tirar(m, false);
            if (m.vida.lugar >= 0) _ocupados.Remove(m.vida.lugar);
            _moradores.RemoveAt(_moradores.Count - 1);
            Apelidos();
            Registrar($"{m.Nome} deixou o bunker.");
            Salvamento.Atual?.moradores?.RemoveAll(x => x.id == m.ficha.id);
            Salvamento.Atual?.vidaDosMoradores?.RemoveAll(x => x.id == m.ficha.id);
        }

        /// <summary>Uma linha por morador, para o F1.</summary>
        public string Resumo(Morador m)
        {
            var v = m.vida;
            string onde = v.lugar >= 0 ? LugaresDoBunker.Todos[v.lugar].nome : "em pé";
            string estado = m.corpo != null ? $"corpo ({m.passo})" : v.noLugar ? "papel" : "papel, andando";
            string humor = $"humor {v.humor:0} {HumorDoMorador.NomeDaFaixa(HumorDoMorador.Faixa(v.humor), m.ficha.mulher)}" +
                           (v.crise != 0 ? $", EM CRISE: {HumorDoMorador.NomeDaCrise(v.Crise, m.ficha.mulher)}" : "") +
                           (m.fumo != EstadoDoFumo.Nada ? $", fumando ({m.fumo}, {m.tragadas} tragadas)" : "") +
                           (m.partida != null ? $", na sinuca ({m.partida.estado}, vez de {m.partida.jogadores[m.partida.vez % m.partida.jogadores.Count].Nome}, {m.partida.tacadas} tacadas)" : "");
            return $"{m.Nome}: {AgendaDoMorador.Nome(v.Atividade)} ({Codigos[Mathf.Clamp(v.andar, 0, 4)]}, {onde}; {estado})\n   {humor}\n" +
                   $"   fome {v.fome:F0}  sede {v.sede:F0}  sono {v.sono:F0}{(m.ficha.Tem(ListasDosMoradores.Fumante) ? $"  fumo {v.fumo:F0}" : "")}" +
                   $"  cama {(v.cama >= 0 ? LugaresDoBunker.Todos[v.cama].nome : "nenhuma")}" +
                   (v.vagas.Count > 0 ? "\n   leva: " + string.Join(", ", v.vagas.ConvertAll(g => $"{Loot.Itens.Nome((Loot.Item)g.item)}{(g.qtd > 1 ? $" ({g.qtd})" : "")}")) : "");
        }
    }
}
