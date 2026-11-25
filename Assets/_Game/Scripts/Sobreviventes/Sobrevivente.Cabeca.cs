using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A cabeça do sobrevivente (no anfitrião; 02/11/2026, a pedido):
    ///  - explorando, o líder vai aos lugares da cidade e os outros vão atrás dele em formação; à noite, de lanterna;
    ///  - ele percebe os jogadores pela visão (de dia longe; à noite perto, a não ser com a lanterna ligada ou embaixo de um
    ///    poste), pelos tiros (<see cref="SobreviventesDoMundo.OuvirTiro"/>) e pelo tiro que leva; quem vê avisa o grupo;
    ///  - em combate: procura uma proteção (um ponto perto onde o cenário tapa a linha até o inimigo), se esconde um pouco,
    ///    espia para o lado e atira em rajadas; recarrega com as caixas da mochila; sem ver o inimigo, avança para onde o
    ///    viu. No tiroteio, a lanterna apaga, e só volta com um zumbi perto (a pedido);
    ///  - os zumbis perto (e os que vêm atrás dele) levam tiro, em qualquer estado;
    ///  - a mira é a da arma (a dispersão dela), pior de longe, no escuro, andando e contra quem corre; a sniper acerta de
    ///    longe. O dano é o da arma (<see cref="Armas.Dano"/>; a espingarda conta uma vez por alvo, como a do jogador);
    ///  - o último que sobra de um grupo foge (para longe dos jogadores, e some).
    /// </summary>
    public partial class Sobrevivente
    {
        // no anfitrião
        internal GrupoDeSobreviventes grupo;
        internal float vida = VidaMaxima;
        internal int pente;
        /// <summary>
        /// As balas que ele usa para recarregar e que não ficam no saque (03/11/2026, os militares: lutam com bastante munição,
        /// mas o que sobra no corpo é pouco). A recarga usa as caixas da mochila primeiro.
        /// </summary>
        internal int reserva;
        /// <summary>O que ele leva (vira a mochila no chão quando ele morre).</summary>
        internal readonly List<ItemNaMochila> mochila = new List<ItemNaMochila>();
        internal Item mochilaVestida;
        internal int dinheiro;
        internal string nome = "Sobrevivente";
        // o acampamento (02/11/2026, a etapa 3): o índice dele (-1: um grupo de busca), o posto, a ronda (os guardas), para
        // onde olha no posto e onde se esconde (dentro da casa)
        internal int acampamento = -1;
        /// <summary>O carro quebrado dele (o índice em <c>Objetos.Quebrados</c>; -1: não é da estrada). A etapa 4.</summary>
        internal int cena = -1;
        /// <summary>A base militar dele (03/11/2026, <see cref="BasesMilitaresDoMundo"/>; -1: não é militar).</summary>
        internal int baseMilitar = -1;
        internal Vector3 posto, ronda, esconderijo;
        internal float giroDoPosto, proximaSaudacao;
        bool _semMunicao, _naRonda;
        float _proximaRonda;
        Vector3 _passeio;

        PapelDoSobrevivente Papel => Identidade.Value.papel;
        bool DoAcampamento => acampamento >= 0;
        bool NaEstrada => cena >= 0;
        bool DaBase => baseMilitar >= 0;
        /// <summary>Fica de guarda num posto, com a ronda: o guarda do acampamento e o militar da base.</summary>
        bool DeGuarda => Papel == PapelDoSobrevivente.Guarda || Papel == PapelDoSobrevivente.Militar;
        /// <summary>
        /// Até onde ele vê o inimigo para atirar: 90 m; o militar, 110 m, e o de sniper, 160 m (a base atira de longe).
        /// </summary>
        float AlcanceDaVista => DaBase ? (Arma == TipoDeArma.Sniper ? 160f : 110f) : 90f;
        /// <summary>
        /// Até onde ele atira nos zumbis: o guarda e o vigia, 30 m (os que chegam na cerca, os que vêm na troca do pneu); o
        /// mecânico ajoelhado, 6 m (ele continua o trabalho: quem protege é o vigia e o jogador); os outros, 16 m.
        /// </summary>
        float AlcanceDosZumbis => DeGuarda || Papel == PapelDoSobrevivente.Vigia ? 30f
            : Papel == PapelDoSobrevivente.Mecanico && _ajoelhado.Value ? 6f : ComBranca ? 9f : 16f;

        // para o F1 e os testes (no anfitrião)
        public float Vida => vida;
        public int Pente => pente;
        public int ItensNaMochila => mochila.Count;
        public IReadOnlyList<ItemNaMochila> Mochila => mochila;
        public GrupoDeSobreviventes Grupo => grupo;
        public string Situacao => _morto.Value ? "morto" : _fugindo ? "fugindo" : Escondido ? "escondido" : _recarregando ? "recarregando"
            : _zumbi != null && ZumbiAmeaca(_zumbi) ? "zumbi" : grupo != null && grupo.estado == EstadoDoGrupo.Combate
            ? (_temProtecao ? $"combate (fase {_fase})" : "combate (sem proteção)") : DoAcampamento || DaBase ? (_naRonda ? "ronda" : "no posto")
            : NaEstrada ? (_ajoelhado.Value ? "ajoelhado na roda" : "de guarda") : "explorando";
        public int Acampamento => acampamento;
        public int BaseMilitar => baseMilitar;

        // o comerciante com o acampamento hostil, e quem do acampamento ficou sem munição: dentro da casa
        bool Escondido => DoAcampamento && (_semMunicao || (Papel == PapelDoSobrevivente.Comerciante && grupo != null && grupo.Hostil));

        float _proximaOlhada, _proximoTiro, _rajadaAte, _fimDaFase, _recarregaAte, _proximaFala, _proximaProtecao, _proximoCaminho, _naoAchouChaoDesde = -1f;
        float _mirandoDesde = -1f, _proximoLado;
        Vector3 _protecao, _espiar, _fuga;
        bool _temProtecao, _temEspiar, _fugindo, _recarregando;
        int _fase;   // em combate: 0 indo para a proteção, 1 escondido, 2 espiando (atirando)
        Zumbi _zumbi;
        Vector3? _mirandoEm;

        // o que tapa a visão e a linha de tiro: o cenário, os carros e (18/11/2026) as portas e janelas fechadas (antes ele via
        // e atirava através delas, e o tiro do jogador parava nelas). A grade, não (ModelosDaEstrada.CamadaDaGrade)
        const int MascaraDoCenario = (1 << 0) | (1 << 11) | (1 << World.PortasDoMundo.Camada);
        /// <summary>
        /// O tiro deles no jogador vale 70% do da arma (as armas são medidas para os zumbis: a espingarda de perto, 115,
        /// matava o jogador com um tiro; agora 80).
        /// </summary>
        public const float DanoNoJogador = 0.7f;
        // o que o tiro dele pega: o cenário, os zumbis (7), os jogadores (9), os carros (11) e os outros sobreviventes (12)
        const int MascaraDoTiro = (1 << 0) | (1 << 7) | (1 << 9) | (1 << 11) | (1 << Camada) | (1 << World.PortasDoMundo.Camada)
                                  | (1 << JogadorRede.CamadaDaCabeca);   // (a cabeça do jogador na proteção, 18/11/2026)
        static readonly RaycastHit[] _acertos = new RaycastHit[24];
        static readonly IComparer<RaycastHit> PorDistancia = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        /// <summary>A vida do militar (03/11/2026, a pedido: "mais resistentes").</summary>
        public const float VidaDoMilitar = 150f;

        void AoNascerNoServidor()
        {
            vida = Papel == PapelDoSobrevivente.Militar ? VidaDoMilitar : VidaMaxima;
            // (o Encher já pôs o pente: os civis com 40% a 100%)
            if (pente <= 0 && !ComBranca) pente = DadosDaArma.capacidade;
            if (!ZombieManager.OutrosAlvos.Contains(this)) ZombieManager.OutrosAlvos.Add(this);
            if (agente != null)
            {
                agente.angularSpeed = 540f;
                agente.acceleration = 12f;
                agente.stoppingDistance = 0.4f;
                agente.autoBraking = true;
                agente.speed = 1.8f;
            }
        }

        void AoSumirNoServidor() => grupo?.membros.Remove(this);

        /// <summary>
        /// No anfitrião: uma fala (no máximo uma a cada 3 s por sobrevivente; sempre: os avisos antes do tiro e as respostas
        /// a quem fala com ele saem mesmo assim).
        /// </summary>
        public void Falar(Frase frase, bool sempre = false)
        {
            if (!IsServer || _morto.Value || (!sempre && Time.time < _proximaFala)) return;
            _proximaFala = Time.time + 3f;
            FalaRpc((byte)frase);
        }

        // ------------------------------------------------------------------ o quadro

        void Pensar()
        {
            if (_morto.Value || agente == null) return;
            if (AlcancarOsJogadores()) return;   // (24/11/2026: o aliado que ficou para trás, Sobrevivente.Escolta.cs)
            if (!agente.enabled)
            {
                // o militar longe do NavMesh dos jogadores (aparece a 170 m): parado no posto, vê e atira de longe
                if (DaBase || naEmboscada) PensarParado();   // (e a emboscada no telhado sem o NavMesh, 18/11/2026)
                return;
            }
            float agora = Time.time;
            if (!agente.isOnNavMesh)
            {
                // o NavMesh em volta dos jogadores foi refeito, ou ele nasceu na beira: volta para o mais perto
                if (_naoAchouChaoDesde < 0f) _naoAchouChaoDesde = agora;
                if (NavMesh.SamplePosition(transform.position, out var perto, 4f, NavMesh.AllAreas)) agente.Warp(perto.position);
                return;
            }
            _naoAchouChaoDesde = -1f;
            if (_recarregando && agora >= _recarregaAte) TerminarRecarga();
            if (ComBranca) ImpactoDoGolpe(agora);
            if (agora >= _proximaOlhada)
            {
                _proximaOlhada = agora + Random.Range(0.22f, 0.32f);
                Olhar(agora);
            }
            bool noite = CicloDoDia.Noite > 0.5f;
            bool zumbiPerto = _zumbi != null && !_zumbi.Morto && (_zumbi.transform.position - transform.position).sqrMagnitude < 10f * 10f;
            bool combate = grupo != null && grupo.estado == EstadoDoGrupo.Combate && grupo.inimigo != null;
            if (!combate) _noTiroteio = false;
            // (18/11/2026) a roda: o inimigo a menos de 10 m; ninguém se esconde nem foge para a proteção, todos atiram
            bool naRoda = combate && grupo.inimigo.AlvoValido && (grupo.inimigo.transform.position - transform.position).sqrMagnitude < 10f * 10f;
            // a lanterna: explorando à noite; no tiroteio, só com um zumbi perto (para afastá-lo). No acampamento, só os
            // guardas (o resto fica na luz das fogueiras e das bancas)
            bool luz = noite && (!combate || zumbiPerto) && !_fugindo && (!DoAcampamento || DeGuarda || zumbiPerto);
            if (_luz.Value != luz) _luz.Value = luz;

            _mirandoEm = null;
            _querAjoelhar = false;
            _querInclinar = 0;
            _querAgachar = 0;
            _soltarOPente = false;
            if (_fugindo) Fugir(agora);
            else if (Escondido && !naRoda) Esconder(agora);
            else if (SairDoFogo(agora)) { }   // o fogo do molotov (11/11/2026, a etapa 4: Sobrevivente.Fogo.cs)
            // (18/11/2026) no tiroteio, com o inimigo visto há pouco, só o zumbi colado (a 6 m) tira ele da luta
            else if (_zumbi != null && ZumbiAmeaca(_zumbi)
                     && (!combate || agora - grupo.viuEm > 3f || (_zumbi.transform.position - transform.position).sqrMagnitude < 6f * 6f)) LutarComZumbi(agora);
            else if (Aliado) PensarComoAliado(agora);   // (24/11/2026: o Capitão, Sobrevivente.Escolta.cs)
            else if (combate) Combater(agora);
            else if (naEmboscada) Aguardar(agora);   // (a emboscada, 18/11/2026: Sobrevivente.Emboscada.cs)
            else if (NaEstrada) FicarNaEstrada(agora);
            else if (DoAcampamento || DaBase) FicarNoPosto(agora);
            else Explorar(agora);
            Mirar();
            if (_ajoelhado.Value != _querAjoelhar) _ajoelhado.Value = _querAjoelhar;
            AplicarAPose();
        }

        // o militar sem o NavMesh embaixo (longe dos jogadores): não anda, mas olha, vira, atira e recarrega
        void PensarParado()
        {
            float agora = Time.time;
            if (_recarregando && agora >= _recarregaAte) TerminarRecarga();
            if (agora >= _proximaOlhada)
            {
                _proximaOlhada = agora + Random.Range(0.22f, 0.32f);
                Olhar(agora);
            }
            bool combate = grupo != null && grupo.estado == EstadoDoGrupo.Combate && grupo.inimigo != null && grupo.inimigo.AlvoValido;
            bool luz = CicloDoDia.Noite > 0.5f && !combate;
            if (_luz.Value != luz) _luz.Value = luz;
            _mirandoEm = null;
            if (combate)
            {
                var inimigo = grupo.inimigo;
                Vector3 peito = inimigo.transform.position + Vector3.up * 1.3f;
                if ((peito - Olho).sqrMagnitude < AlcanceDaVista * AlcanceDaVista && !Physics.Linecast(Olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                {
                    grupo.Ver(inimigo, inimigo.transform.position, null);
                    AtirarNo(inimigo, peito, agora);
                }
            }
            else if (_zumbi != null && ZumbiAmeaca(_zumbi))
            {
                Vector3 alvo = _zumbi.transform.position + Vector3.up * 1.35f;
                if (!Physics.Linecast(Olho, alvo, MascaraDoCenario, QueryTriggerInteraction.Ignore)) AtirarNo(null, alvo, agora);
            }
            else
            {
                var giro = Quaternion.Euler(0f, giroDoPosto, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, giro, 120f * Time.deltaTime);
            }
            Mirar();
            _querInclinar = 0;
            _querAgachar = 0;
            AplicarAPose();
        }

        // ------------------------------------------------------------------ perceber

        void Olhar(float agora)
        {
            if (grupo == null) return;
            EscolherOAlvo();   // (18/11/2026, Sobrevivente.Combate.cs)
            // o último que sobrou foge
            if (grupo.Fugindo && !_fugindo) ComecarAFugir();
            // os jogadores (o acampamento amigo não liga para eles)
            float noite = CicloDoDia.Noite;
            if (grupo.Hostil)
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                Vector3 peito = j.transform.position + Vector3.up * 1.3f;
                Vector3 para = peito - Olho;
                float d = para.magnitude;
                // (o militar vê mais longe: 90 m de dia, e o de sniper 150; à noite, 20)
                float alcance = DaBase ? Mathf.Lerp(Arma == TipoDeArma.Sniper ? 150f : 90f, 20f, noite) : Mathf.Lerp(50f, 15f, noite);
                if (j.Lanterna != null && j.Lanterna.Ligada.Value) alcance = Mathf.Max(alcance, 35f);
                if (noite > 0.5f && PosteDeLuz.Ilumina(j.transform.position)) alcance = Mathf.Max(alcance, 25f);
                int discreto = Habilidades.Pontos(j, Habilidade.Discreto);   // (o Discreto, 06/11/2026: 15% e 25% por ponto)
                alcance *= 1f - 0.15f * discreto;
                if (d < 3f) alcance = 99f;   // encostado
                // os passos: perto, ele ouve de qualquer lado, até atrás de uma parede (correndo, mais longe), e sabe mais ou
                // menos onde (o grupo vai procurar)
                float ouvido = (j.Velocidade > 3f ? 20f : j.Velocidade > 0.5f ? 9f : 3f) * (1f - 0.25f * discreto) * Protecao.FatorDosPassos(j);
                if (d <= ouvido && grupo.estado != EstadoDoGrupo.Combate)
                {
                    grupo.Ver(j, j.transform.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)), this);
                    break;
                }
                if (d > alcance) continue;
                if (d > 3f && Vector3.Angle(transform.forward, para) > 75f && !(grupo.estado == EstadoDoGrupo.Combate)) continue;
                if (Physics.Linecast(Olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                grupo.Ver(j, j.transform.position, this);
                break;
            }
            // os zumbis: o mais perto que ameaça (vindo atrás dele, ou a menos de 9 m)
            _zumbi = null;
            var zm = ZombieManager.Instancia;
            if (zm == null) return;
            float alcanceZ = AlcanceDosZumbis, menor = alcanceZ * alcanceZ;
            // (o guarda atira em qualquer um no alcance; os outros, no que vem atrás dele ou está a menos de 9 m)
            float semAlvo = DeGuarda ? alcanceZ : 9f;
            foreach (var z in zm.Ativos)
            {
                if (z == null || z.Morto || z.Petrificado || z.NivelAtual == Zumbi.Nivel.Longe) continue;
                float d2 = (z.transform.position - transform.position).sqrMagnitude;
                if (d2 > menor) continue;
                bool atrasDeMim = ReferenceEquals(z.Alvo, this);
                if (!atrasDeMim && d2 > semAlvo * semAlvo) continue;
                menor = d2;
                _zumbi = z;
            }
        }

        bool ZumbiAmeaca(Zumbi z) => z != null && !z.Morto && !z.Petrificado && (z.transform.position - transform.position).sqrMagnitude < AlcanceDosZumbis * AlcanceDosZumbis;

        // ------------------------------------------------------------------ o acampamento

        // no posto: o guarda vai e volta entre o posto e a ronda; o morador passeia perto da fogueira; o comerciante fica na
        // banca. Parado, olha para o lado do posto
        void FicarNoPosto(float agora)
        {
            _temProtecao = false;
            if (_mirando.Value) _mirando.Value = false;
            if (agora >= _proximaRonda)
            {
                if (DeGuarda)
                {
                    _naRonda = !_naRonda;
                    _proximaRonda = agora + Random.Range(8f, 16f);
                }
                else if (Papel == PapelDoSobrevivente.Morador)
                {
                    _naRonda = Random.value < 0.35f;
                    var r = Random.insideUnitCircle * 3f;
                    _passeio = posto + new Vector3(r.x, 0f, r.y);
                    _proximaRonda = agora + Random.Range(12f, 30f);
                }
                else _proximaRonda = agora + 30f;
            }
            Vector3 alvo = !_naRonda ? posto : DeGuarda ? ronda : _passeio;
            if ((transform.position - alvo).sqrMagnitude > 0.7f * 0.7f)
            {
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.6f;
                    Ir(alvo, DeGuarda ? 1.3f : 1.1f, 0.3f);
                }
                return;
            }
            if (agente.hasPath) agente.ResetPath();
            var giro = Quaternion.Euler(0f, giroDoPosto, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, giro, 120f * Time.deltaTime);
        }

        // dentro da casa: corre para o esconderijo (o dele, num cômodo) e fica. Quem entra perto (10 m, à vista) leva tiro
        void Esconder(float agora)
        {
            _temProtecao = false;
            var inimigo = grupo != null && grupo.estado == EstadoDoGrupo.Combate ? grupo.inimigo : null;
            if (inimigo != null && inimigo.AlvoValido && !_semMunicao)
            {
                Vector3 peito = inimigo.transform.position + Vector3.up * 1.3f;
                if ((peito - Olho).sqrMagnitude < 10f * 10f && !Physics.Linecast(Olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore))
                {
                    Parar();
                    AtirarNo(inimigo, peito, agora);
                    return;
                }
            }
            if (agora < _proximoCaminho || (transform.position - esconderijo).sqrMagnitude < 1.5f * 1.5f) return;
            _proximoCaminho = agora + 0.8f;
            Ir(esconderijo, 3.8f, 0.8f);
        }

        // ------------------------------------------------------------------ explorar

        void Explorar(float agora)
        {
            _temProtecao = false;
            if (_mirando.Value) _mirando.Value = false;
            var lider = grupo != null ? grupo.Lider : null;
            if (lider == null || agora < _proximoCaminho) return;
            _proximoCaminho = agora + 0.5f;
            if (lider == this)
            {
                if (grupo.temDestino) Ir(grupo.destino, 1.7f, 0.8f);
                return;
            }
            // na formação: em fila dupla atrás do líder
            int i = Mathf.Max(1, grupo.membros.IndexOf(this));
            int fila = (i + 1) / 2, lado = i % 2 == 0 ? 1 : -1;
            var t = lider.transform;
            var lugar = t.position - t.forward * (1.8f * fila) + t.right * (1.1f * lado);
            float longe = Vector3.Distance(transform.position, lugar);
            Ir(lugar, longe > 6f ? 4f : longe > 2.5f ? 2.4f : 1.7f, 0.6f);
        }

        void Ir(Vector3 ponto, float velocidade, float parada)
        {
            if (!agente.isOnNavMesh) return;
            agente.speed = velocidade;
            agente.stoppingDistance = parada;
            agente.updateRotation = true;
            agente.isStopped = false;
            if ((agente.destination - ponto).sqrMagnitude > 0.25f) agente.SetDestination(ponto);
        }

        void Parar()
        {
            if (agente.isOnNavMesh && agente.hasPath) agente.ResetPath();
        }

        // ------------------------------------------------------------------ o tiroteio

        void Combater(float agora)
        {
            // (18/11/2026) o jogador mais perto que ele vê, se houver; senão, o do grupo
            IAlvoDeZumbi inimigo = _alvoProprio != null && _alvoProprio.AlvoValido ? _alvoProprio : grupo.inimigo;
            Vector3 alvo = inimigo.transform.position;
            Vector3 peito = alvo + Vector3.up * 1.3f;
            var olho = OlhoAgora;   // (18/11/2026: o olho na pose; agachado ou atrás da quina, ele não vê)
            bool vejo;
            if (inimigo is JogadorRede jr)
            {
                // (18/11/2026) o jogador na proteção: o peito (mais baixo agachado) ou, se só ela aparece, a cabeça
                peito = jr.PontoParaOTiro(olho, MascaraDoCenario, out bool visto);
                vejo = visto && (peito - olho).sqrMagnitude < AlcanceDaVista * AlcanceDaVista;
            }
            else vejo = (peito - olho).sqrMagnitude < AlcanceDaVista * AlcanceDaVista && !Physics.Linecast(olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore);
            if (vejo)
            {
                if (ReferenceEquals(inimigo, grupo.inimigo)) grupo.Ver(inimigo, alvo, null);
                _prendendoAte = agora + Random.Range(2f, 3.5f);   // se ele sumir agora, atira ali por uns segundos
            }
            AtualizarAMira(vejo);
            peito = PontoDaMira(inimigo, peito);
            Vector3 onde = vejo ? alvo : grupo.ondeViu;
            // (18/11/2026) a espingarda: chega perto (de longe ela não acerta); a menos de 12 m, vai para cima atirando
            if (!ComBranca && !naEmboscada && Arma == TipoDeArma.Espingarda && EspingardaAvanca(inimigo, alvo, peito, vejo, agora)) return;
            // o cerco: um do grupo vai pelo lado quando o alvo some atrás de alguma coisa
            if (!ComBranca && !naEmboscada && !vejo && grupo.Vivos >= 3 && agora - grupo.viuEm > 2.5f && agora - grupo.viuEm < 20f
                && (grupo.flanqueador == null || grupo.flanqueador.Morto || agora > grupo.flancoAte) && Random.value < 0.02f)
            {
                if (PontoDoFlanco(grupo.ondeViu, out var flanco))
                {
                    grupo.flanqueador = this;
                    grupo.flancoAte = agora + 14f;
                    _flanqueando = true;
                    _temProtecao = false;
                    _protecao = flanco;
                    Falar(Frase.Contato);
                }
            }
            if (_flanqueando)
            {
                if (grupo.flanqueador != this || agora > grupo.flancoAte || vejo || (transform.position - _protecao).sqrMagnitude < 1f)
                    _flanqueando = false;
                else
                {
                    if (agora >= _proximoCaminho)
                    {
                        _proximoCaminho = agora + 0.4f;
                        Ir(_protecao, 3.8f, 0.5f);
                    }
                    return;
                }
            }
            if (ComBranca)
            {
                _temProtecao = false;
                BrigarDePerto(inimigo, onde, vejo, agora);
                return;
            }
            if (!_noTiroteio)
            {
                // (18/11/2026, a pedido: "alguns traçar a rota de fugir e buscar esses refúgios para trocar o tiro") o começo
                // do tiroteio: uns 60% correm já para uma proteção; os outros atiram de onde estão por uns segundos, cobrindo
                // quem corre (se levarem tiro, vão já: o ReagirAoTiro)
                _noTiroteio = true;
                _protecaoDepoisDe = Random.value < 0.4f ? agora + Random.Range(2.5f, 5f) : 0f;
            }
            // (18/11/2026, a pedido: "se eu estou em uma roda de npcs e atiro em um, esses npcs não é para sair correndo, é
            // para eles meterem bala em mim, se algum deles está com fuzil é para descarregar o pente sem dó") a roda: o
            // inimigo a menos de 10 m e à vista; ninguém corre para a proteção (quem está espiando continua), todos atiram de
            // onde estão, com um passo de lado de vez em quando, e a automática solta o pente inteiro
            if (vejo && (alvo - transform.position).sqrMagnitude < 10f * 10f && !(_temProtecao && _fase == 2))
            {
                _temProtecao = false;
                _soltarOPente = true;
                AtirarNo(inimigo, peito, agora);
                if (agora >= _proximoLado)
                {
                    _proximoLado = agora + Random.Range(1.5f, 2.6f);
                    Vector3 para = alvo - transform.position;
                    para.y = 0f;
                    Vector3 lado = Vector3.Cross(Vector3.up, para.normalized) * ((Random.value < 0.5f ? 1f : -1f) * Random.Range(1f, 2.2f));
                    if (NavMesh.SamplePosition(transform.position + lado, out var hl, 1.5f, NavMesh.AllAreas)) Ir(hl.position, 2.2f, 0.2f);
                }
                return;
            }

            // sem uma proteção (ou ela ficou ruim): procura outra; sem nenhuma, fica e atira de onde está
            if (!_temProtecao && agora >= _proximaProtecao && agora >= _protecaoDepoisDe)
            {
                _proximaProtecao = agora + Random.Range(3f, 5f);
                _temProtecao = AcharProtecao(onde, out _protecao, out _espiar, out _temEspiar);
                _fase = 0;
                _passosAMais = 0;
                _fimDaFase = agora + 3f + Vector3.Distance(transform.position, _protecao) / 2.5f;   // (18/11/2026) o prazo para chegar
            }
            if (!_temProtecao)
            {
                if (vejo)
                {
                    AtirarNo(inimigo, peito, agora);
                    // sem proteção: anda de lado (fica mais difícil de acertar), sem chegar a menos de 10 m (a emboscada, não)
                    if (!naEmboscada && agora >= _proximoLado)
                    {
                        _proximoLado = agora + Random.Range(1.8f, 3f);
                        Vector3 para = alvo - transform.position;
                        para.y = 0f;
                        float dist = para.magnitude;
                        para /= Mathf.Max(dist, 0.01f);
                        Vector3 lado = Vector3.Cross(Vector3.up, para) * ((Random.value < 0.5f ? 1f : -1f) * Random.Range(2.5f, 4.5f));
                        Vector3 recuo = dist < 10f ? -para * (10f - dist) : Vector3.zero;
                        if (NavMesh.SamplePosition(transform.position + lado + recuo, out var h, 2f, NavMesh.AllAreas)) Ir(h.position, 2.2f, 0.3f);
                    }
                }
                else if (PrenderNoLugar(agora)) { }
                else if (!naEmboscada && agora - grupo.viuEm > 6f && agora >= _proximoCaminho)
                {
                    // ninguém vê o inimigo: avança para onde ele foi visto, pelo lado
                    _proximoCaminho = agora + 1f;
                    Vector3 lado = Vector3.Cross(Vector3.up, (grupo.ondeViu - transform.position).normalized) * Random.Range(-4f, 4f);
                    Ir(grupo.ondeViu + lado, 3.2f, 1.5f);
                }
                return;
            }
            switch (_fase)
            {
                case 0:   // indo para a proteção, correndo
                    if (agora >= _proximoCaminho)
                    {
                        _proximoCaminho = agora + 0.4f;
                        Ir(_protecao, 4.0f, 0.3f);
                    }
                    // (18/11/2026) não chega lá (o caminho para antes, ou passou do prazo): desiste dela e procura outra
                    if (agora > _fimDaFase || (!agente.pathPending && agente.hasPath && agente.pathStatus != NavMeshPathStatus.PathComplete))
                    {
                        _protecaoRuim = _protecao;
                        _temProtecao = false;
                        _proximaProtecao = agora + 0.3f;
                        break;
                    }
                    if ((transform.position - _protecao).sqrMagnitude < 0.8f * 0.8f)
                    {
                        _fase = 1;
                        _fimDaFase = agora + Random.Range(1f, 2.5f);
                        Parar();
                    }
                    break;
                case 1:   // escondido (atrás da proteção baixa, agachado); recarrega aqui com o pente pela metade
                    _querAgachar = _protecaoBaixa ? 100 : 0;
                    AtirarAsCegas(agora);
                    if (!_recarregando && pente < DadosDaArma.capacidade * 0.45f && pente < DadosDaArma.capacidade) Recarregar();
                    if (_recarregando || agora < _fimDaFase) break;
                    _fase = 2;
                    // (18/11/2026) a janela de espiar no tempo da arma: a de ferrolho dá dois tiros
                    var dadosDaArma = DadosDaArma;
                    _fimDaFase = agora + Random.Range(1.6f, 3.2f) + (dadosDaArma.automatica ? 0.4f : Mathf.Min(2.6f, dadosDaArma.intervalo * 2.2f));
                    if (_temEspiar && (_espiar - _protecao).sqrMagnitude > 0.04f) Ir(_espiar, 2.2f, 0.2f);
                    break;
                case 2:   // espiando: atira enquanto vê (e, logo que o alvo some, onde ele estava)
                    // (18/11/2026) espiando: da proteção baixa, levanta só um pouco; da quina, inclina o tronco (só a cabeça,
                    // o ombro e a arma para fora)
                    AbrirAJanelaDoPosto(agora);   // (18/11/2026, Sobrevivente.Janela.cs)
                    EspiarMaisUmPouco(vejo, agora);
                    if (_protecaoBaixa) _querAgachar = 22;
                    else if (_ladoDoEspiar != 0 && (transform.position - _espiar).sqrMagnitude < 0.6f * 0.6f) _querInclinar = _ladoDoEspiar * 100;
                    // virado para onde o alvo está (a inclinação é para o lado de quem olha para ele)
                    if (!_mirandoEm.HasValue) _mirandoEm = vejo ? peito : grupo.ondeViu + Vector3.up * 1.3f;
                    if (vejo) AtirarNo(inimigo, peito, agora);
                    else PrenderNoLugar(agora);
                    if (agora >= _fimDaFase || _recarregando)
                    {
                        _fase = 1;
                        _fimDaFase = agora + Random.Range(1.2f, 2.6f);
                        Ir(_protecao, 2.6f, 0.3f);
                        // a proteção ficou longe de onde o inimigo está agora (ele andou): procura outra depois
                        if (vejo && (alvo - grupo.ondeViu).sqrMagnitude > 64f) _temProtecao = false;
                        if (!vejo && agora - grupo.viuEm > 6f) _temProtecao = false;   // avança para outra, mais perto
                    }
                    break;
            }
        }

        /// <summary>
        /// Uma proteção: um ponto a 2–14 m (no NavMesh) de onde o cenário tapa a linha até o inimigo (na altura do peito), e
        /// ao lado dele, a 1 m, um lugar para espiar (com a linha livre). Sem o inimigo à vista há tempo, prefere as mais
        /// perto dele (avança).
        /// </summary>
        bool AcharProtecao(Vector3 inimigo, out Vector3 ponto, out Vector3 espiar, out bool temEspiar)
        {
            ponto = espiar = Vector3.zero;
            temEspiar = false;
            Vector3 alvo = inimigo + Vector3.up * 1.4f;
            bool avancar = grupo != null && Time.time - grupo.viuEm > 6f && !_recuar;
            float melhor = float.MaxValue;
            Vector3 aqui = transform.position;
            float boa = DistanciaBoa;
            bool baixaAchada = false;
            int ladoAchado = 0;
            // (18/11/2026) os lugares para ver: 16 raios em volta (a 1 m do chão), e onde batem numa parede, num carro ou num
            // muro, o ponto colado nele (do lado de cá); e mais 12 sorteados, como antes
            _candidatos.Clear();
            int deJanelas = CandidatosDasJanelas(inimigo);   // (as janelas, 18/11/2026: Sobrevivente.Janela.cs)
            System.Array.Clear(_janelaNasEtapas, 0, _janelaNasEtapas.Length);
            _janelaNasEtapas[0] = deJanelas;
            JanelaDaCasa janelaAchada = null;
            float giro = Random.Range(0f, Mathf.PI * 2f);
            for (int k = 0; k < 16; k++)
            {
                float ang = giro + k * (Mathf.PI * 2f / 16f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (!Physics.Raycast(aqui + Vector3.up * 1f, dir, out var bate, 16f, MascaraDaProtecao, QueryTriggerInteraction.Ignore) || bate.distance < 1f) continue;
                Vector3 normal = bate.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.1f) continue;
                normal.Normalize();
                var junto = bate.point + normal * 0.55f;
                junto.y = aqui.y;
                _candidatos.Add(junto);
                // e meio metro para cada lado, ao longo da parede (a quina)
                var aoLongo = Vector3.Cross(Vector3.up, normal);
                _candidatos.Add(junto + aoLongo * Random.Range(0.6f, 1.4f));
                _candidatos.Add(junto - aoLongo * Random.Range(0.6f, 1.4f));
            }
            for (int k = 0; k < 12; k++)
            {
                float ang = k * (Mathf.PI * 2f / 12f) + Random.Range(-0.25f, 0.25f);
                _candidatos.Add(aqui + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(2f, 16f));
            }
            for (int ci = 0; ci < _candidatos.Count; ci++)
            {
                bool daJanela = ci < deJanelas;
                if (!NavMesh.SamplePosition(_candidatos[ci], out var hit, daJanela ? 0.9f : 1.0f, NavMesh.AllAreas)) continue;
                var p = hit.position;
                if (daJanela) _janelaNasEtapas[1]++;
                if ((p - inimigo).sqrMagnitude < 6f * 6f) continue;   // nem colado no inimigo
                // (18/11/2026) a proteção alta (tapa em pé) ou a baixa (tapa agachado: atira de pé dali mesmo)
                // (o meio e os dois ombros, a 22 cm: antes, só o meio, e o ombro ficava de fora)
                if ((p - _protecaoRuim).sqrMagnitude < 2f * 2f) continue;   // a que ele não conseguiu alcançar
                if (naEmboscada && (p - posto).sqrMagnitude > RaioDoPosto * RaioDoPosto) continue;   // (a emboscada: perto do posto)
                Vector3 ombro = Vector3.Cross(Vector3.up, (inimigo - p).normalized) * 0.22f;
                // (e colado nela: a até 2,2 m; antes valia uma cerca a 15 m, e ele ficava no meio do pátio)
                bool alta = Tapa(p + Vector3.up * 1.4f, ombro, alvo, 2.2f);
                // (a baixa: de pé, por cima dela, ele vê)
                bool baixa = !alta && Tapa(p + Vector3.up * 1.2f, ombro, alvo, 2.0f)   // (a cabeça agachada fica a uns 1,15 m)
                             && !Physics.Linecast(p + Vector3.up * 1.5f, alvo, MascaraDaProtecao, QueryTriggerInteraction.Ignore);
                if (!alta && !baixa) continue;   // não tapa
                if (daJanela) _janelaNasEtapas[2]++;
                if (grupo != null && grupo.Reservada(p, this)) continue;   // um colega já vai para lá
                float dist = Vector3.Distance(p, inimigo);
                float extra = Mathf.Abs(dist - boa) * (Arma == TipoDeArma.Espingarda ? 1.2f : 0.35f)
                              + (avancar ? dist * 0.6f : 0f) - (_recuar ? dist * 0.5f : 0f) - (daJanela ? BonusDaJanela : 0f);
                if ((p - aqui).magnitude + extra >= melhor) continue;
                // o caminho de verdade até lá (a cerca no meio faz dar a volta; sem caminho, não serve)
                float andar = Caminho(aqui, p);
                // (para a janela, dar a volta até a porta da casa vale: até 4 vezes a reta, mais 10 m)
                if (andar < 0f || andar > (p - aqui).magnitude * (daJanela ? 4f : 2.2f) + (daJanela ? 10f : 4f)) continue;
                if (daJanela) _janelaNasEtapas[3]++;
                float nota = andar + extra;
                if (nota >= melhor) continue;
                if (baixa)
                {
                    melhor = nota;
                    ponto = p;
                    espiar = p;   // de pé, no mesmo lugar
                    temEspiar = true;
                    baixaAchada = true;
                    ladoAchado = 0;
                    janelaAchada = daJanela ? _janelaDoCandidato[ci] : null;
                    continue;
                }
                // (18/11/2026) espiar pela quina inclinando o tronco: do mesmo lugar, ou com meio passo para o lado
                {
                    Vector3 ladoDaQuina = Vector3.Cross(Vector3.up, (inimigo - p).normalized);
                    float rad = InclinacaoMaxima * Mathf.Deg2Rad;
                    float desloca = CinturaAoOlho * Mathf.Sin(rad), alto = 1.6f - CinturaAoOlho * (1f - Mathf.Cos(rad));
                    int ladoBom = 0;
                    Vector3 deOnde = p;
                    foreach (float passo in new[] { 0f, 0.3f, 0.55f })
                    {
                        foreach (int s in new[] { 1, -1 })
                        {
                            var q = p + ladoDaQuina * (s * passo);
                            if (passo > 0f)
                            {
                                if (!NavMesh.SamplePosition(q, out var hq2, 0.4f, NavMesh.AllAreas)) continue;
                                q = hq2.position;
                                // o meio do corpo continua atrás da quina
                                if (!Physics.Linecast(q + Vector3.up * 1.15f, alvo, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) continue;
                            }
                            var olhoInclinado = q + Vector3.up * alto + ladoDaQuina * (s * desloca);
                            if (Physics.Linecast(olhoInclinado, alvo, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) continue;
                            ladoBom = s;
                            deOnde = q;
                            break;
                        }
                        if (ladoBom != 0) break;
                    }
                    if (ladoBom != 0)
                    {
                        melhor = nota;
                        ponto = p;
                        espiar = deOnde;
                        temEspiar = true;
                        baixaAchada = false;
                        ladoAchado = ladoBom;
                        janelaAchada = daJanela ? _janelaDoCandidato[ci] : null;
                        if (daJanela) _janelaNasEtapas[4]++;
                        continue;
                    }
                }
                // o lugar de espiar: ao lado, com a linha livre
                Vector3 lado = Vector3.Cross(Vector3.up, (inimigo - p).normalized);
                bool achou = false;
                Vector3 e = p;
                foreach (float s in new[] { 1f, -1f, 1.6f, -1.6f })
                {
                    var q = p + lado * s;
                    if (!NavMesh.SamplePosition(q, out var hq, 0.8f, NavMesh.AllAreas)) continue;
                    if (Physics.Linecast(hq.position + Vector3.up * 1.4f, alvo, MascaraDaProtecao, QueryTriggerInteraction.Ignore)) continue;
                    e = hq.position;
                    achou = true;
                    break;
                }
                // (18/11/2026) sem lugar nenhum para espiar, ela não serve para lutar (só para quem recua ferido)
                if (!achou && !_recuar) continue;
                melhor = nota;
                ponto = p;
                espiar = e;
                temEspiar = achou;
                baixaAchada = false;
                ladoAchado = 0;
                janelaAchada = daJanela ? _janelaDoCandidato[ci] : null;
            }
            _janelaDoPosto = janelaAchada;
            _protecaoBaixa = baixaAchada;
            _ladoDoEspiar = ladoAchado;
            _recuar = false;
            if (melhor < float.MaxValue && grupo != null) grupo.Reservar(ponto, this);
            return melhor < float.MaxValue;
        }

        Vector3 _protecaoRuim = new Vector3(1e6f, 0f, 0f);
        static NavMeshPath _caminho;

        static readonly List<Vector3> _candidatos = new List<Vector3>(64);

        /// <summary>
        /// A proteção tapa o meio e os dois lados do corpo (o 'ombro' para um lado) na linha até o alvo, e está colada nele
        /// (o meio bate nela a até 'colada' m).
        /// </summary>
        static bool Tapa(Vector3 de, Vector3 ombro, Vector3 alvo, float colada) =>
            Physics.Linecast(de, alvo, out var bate, MascaraDaProtecao, QueryTriggerInteraction.Ignore) && bate.distance <= colada
            && Physics.Linecast(de + ombro, alvo + ombro, MascaraDaProtecao, QueryTriggerInteraction.Ignore)
            && Physics.Linecast(de - ombro, alvo - ombro, MascaraDaProtecao, QueryTriggerInteraction.Ignore);

        /// <summary>O comprimento do caminho pelo NavMesh (m); -1 sem caminho inteiro.</summary>
        static float Caminho(Vector3 de, Vector3 para)
        {
            _caminho ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(de, para, NavMesh.AllAreas, _caminho) || _caminho.status != NavMeshPathStatus.PathComplete) return -1f;
            var c = _caminho.corners;
            float total = 0f;
            for (int i = 1; i < c.Length; i++) total += Vector3.Distance(c[i - 1], c[i]);
            return total;
        }

        void LutarComZumbi(float agora)
        {
            if (ComBranca)
            {
                BrigarComZumbi(agora);
                return;
            }
            Vector3 alvo = _zumbi.transform.position + Vector3.up * 1.35f;
            float d = Vector3.Distance(transform.position, _zumbi.transform.position);
            // muito perto: recua um pouco enquanto atira
            if (d < 3f && agora >= _proximoCaminho)
            {
                _proximoCaminho = agora + 0.6f;
                Vector3 fora = transform.position - _zumbi.transform.position;
                fora.y = 0f;
                fora = fora.sqrMagnitude > 0.01f ? fora.normalized : -transform.forward;
                // no meio de um tiroteio, recua de lado (não para cima do inimigo)
                if (grupo != null && grupo.estado == EstadoDoGrupo.Combate && grupo.inimigo != null && grupo.inimigo.AlvoValido)
                {
                    Vector3 paraInimigo = grupo.inimigo.transform.position - transform.position;
                    paraInimigo.y = 0f;
                    paraInimigo.Normalize();
                    Vector3 lado = Vector3.Cross(Vector3.up, paraInimigo);
                    if (Vector3.Dot(fora, paraInimigo) > 0.3f) fora = lado * (Vector3.Dot(fora, lado) >= 0f ? 1f : -1f);
                }
                if (NavMesh.SamplePosition(transform.position + fora * 3f, out var hit, 2f, NavMesh.AllAreas)) Ir(hit.position, 2.4f, 0.2f);
            }
            else if (d >= 3f) Parar();
            if (Random.value < 0.02f) Falar(Frase.Zumbis);
            if (!Physics.Linecast(Olho, alvo, MascaraDoCenario, QueryTriggerInteraction.Ignore)) AtirarNo(null, alvo, agora);
        }

        // ------------------------------------------------------------------ fugir

        void ComecarAFugir()
        {
            _fugindo = true;
            if (_mirando.Value) _mirando.Value = false;
            Falar(Frase.Fugindo);
            Vector3 de = grupo != null && grupo.inimigo != null && grupo.inimigo.AlvoValido ? grupo.inimigo.transform.position : transform.position - transform.forward * 10f;
            Vector3 para = (transform.position - de);
            para.y = 0f;
            para = para.sqrMagnitude > 0.01f ? para.normalized : -transform.forward;
            _fuga = transform.position + para * 50f;
            for (int t = 0; t < 8; t++)
            {
                var q = transform.position + Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * para * Random.Range(35f, 55f);
                if (NavMesh.SamplePosition(q, out var hit, 6f, NavMesh.AllAreas))
                {
                    _fuga = hit.position;
                    break;
                }
            }
        }

        void Fugir(float agora)
        {
            if (agora < _proximoCaminho) return;
            _proximoCaminho = agora + 0.5f;
            Ir(_fuga, 4.3f, 1f);
            // chegou e não tem para onde: procura mais longe
            if ((transform.position - _fuga).sqrMagnitude < 4f) ComecarAFugir();
        }

        /// <summary>Está fugindo e já sumiu de vista (o mundo o tira).</summary>
        internal bool Fugiu => _fugindo && GrupoDeSobreviventes.PertoDeUmJogador(transform.position) > 45f;

        // ------------------------------------------------------------------ atirar

        // vira para quem mira (o corpo todo; a animação de mirar segura a arma)
        void Mirar()
        {
            bool mirando = _mirandoEm.HasValue;
            if (_mirando.Value != mirando) _mirando.Value = mirando;
            if (!mirando)
            {
                _mirandoDesde = -1f;
                return;
            }
            if (_mirandoDesde < 0f) _mirandoDesde = Time.time;
            agente.updateRotation = false;
            Vector3 para = _mirandoEm.Value - transform.position;
            para.y = 0f;
            if (para.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(para), 420f * Time.deltaTime);
        }

        void AtirarNo(IAlvoDeZumbi quem, Vector3 ponto, float agora, bool prender = false, bool cego = false)
        {
            _mirandoEm = ponto;
            if (_recarregando) return;
            if (Arma == TipoDeArma.Espingarda && !cego && (ponto - transform.position).sqrMagnitude > 22f * 22f) return;   // (18/11/2026)
            if (pente <= 0)
            {
                // sem munição na mochila: foge (o do acampamento se esconde em casa)
                if (!Recarregar())
                {
                    if (DoAcampamento) _semMunicao = true;
                    else ComecarAFugir();
                }
                return;
            }
            // só atira virado para o alvo, e depois de levantar a arma (meio segundo)
            Vector3 para = ponto - transform.position;
            para.y = 0f;
            if (Vector3.Angle(transform.forward, para) > 12f || agora < _proximoTiro) return;
            // (18/11/2026) levanta a arma em 0,3 s (o militar, 0,22; a sniper, 0,5, olhando pela luneta); antes, 0,45. Às
            // cegas, sem levantar
            float levantar = Arma == TipoDeArma.Sniper ? 0.5f : Militar ? 0.22f : 0.3f;
            if (!cego && (_mirandoDesde < 0f || agora - _mirandoDesde < levantar)) return;
            var d = DadosDaArma;
            // a cadência (18/11/2026): as automáticas em rajadas de uns 4 a 9 tiros, com pausas curtas (antes, de 3 a 6, e uma
            // pausa longa que virava tiros soltos); as outras no ritmo da arma, com um respiro pequeno (antes, o dobro)
            if (d.automatica && _soltarOPente) _proximoTiro = agora + d.intervalo * 1.05f;   // (a roda: o pente inteiro, 18/11/2026)
            else if (d.automatica)
            {
                if (agora >= _rajadaAte && agora >= _proximaRajada)
                {
                    _rajadaAte = agora + Random.Range(0.35f, 0.9f);
                    _proximaRajada = _rajadaAte + Random.Range(0.25f, 0.55f);
                }
                _proximoTiro = agora < _rajadaAte ? agora + d.intervalo * 1.05f : _proximaRajada;
            }
            else _proximoTiro = agora + Mathf.Max(d.intervalo, 0.2f) * (_soltarOPente ? Random.Range(1f, 1.2f) : Random.Range(1.15f, 1.6f));
            pente--;
            Disparar(ponto, quem, d, prender, cego);
        }

        void Disparar(Vector3 ponto, IAlvoDeZumbi quem, DadosDaArma d, bool prender = false, bool cego = false)
        {
            // (18/11/2026) a boca da arma na pose (agachado, inclinado); às cegas, da quina da proteção, sem olhar
            Vector3 origem = (cego ? OlhoNaPose(_ladoDoEspiar * 0.55f, _protecaoBaixa ? 0.35f : 0f) : OlhoAgora) + transform.forward * 0.4f;
            Vector3 direcao = (ponto - origem).normalized;
            float distancia = Vector3.Distance(origem, ponto);
            float abertura = Dispersao(d, distancia, quem) * (cego ? 3.2f : prender ? 1.8f : 1f);
            var impactos = new JogadorRede.Impacto[d.chumbos];
            // o dano de cada alvo, uma vez só (a espingarda: o maior dos chumbos)
            var danoNoJogador = _danoNoJogador;
            var noZumbi = _noZumbi;
            danoNoJogador.Clear();
            noZumbi.Clear();
            for (int k = 0; k < d.chumbos; k++)
            {
                Vector2 desvio = Random.insideUnitCircle * abertura;
                Vector3 dir = Quaternion.LookRotation(direcao) * (Quaternion.Euler(desvio.y, desvio.x, 0f) * Vector3.forward);
                impactos[k] = Raio(origem, dir, d, danoNoJogador, noZumbi);
            }
            // o tiro sempre abre uma ferida, na parte onde o chumbo maior pegou (05/11/2026, o corpo do jogador)
            foreach (var (j, a) in danoNoJogador)
                j.Vida?.Dano(a.dano * DanoNoJogador, origem, DaBase ? "Baleado por um militar." : "Baleado por um sobrevivente.",
                             TipoDeGolpe.Tiro, PartesDoCorpo.PeloPonto(j.transform, a.ponto));
            foreach (var (z, a) in noZumbi) ZombieManager.Acertar(z.Id, z.Geracao, a.h.regiao, a.h.membro, a.dano, a.p, false, this);
            TiroRpc((byte)Arma, impactos);
            ZombieManager.Barulho(origem, this);
            Vehicles.PecasDosCarrosDoMundo.Instancia?.TiroNosCarros(impactos);
            if (Random.value < 0.04f) Falar(Frase.Atirando);
        }

        JogadorRede.Impacto Raio(Vector3 origem, Vector3 dir, DadosDaArma d, Dictionary<JogadorRede, (float dano, Vector3 ponto)> danoNoJogador,
                                 Dictionary<Zumbi, (Hitbox, Vector3, float)> noZumbi)
        {
            var impacto = new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Nada, normal = Vector3.up };
            int n = Physics.RaycastNonAlloc(origem, dir, _acertos, d.alcance, MascaraDoTiro, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_acertos, 0, n, PorDistancia);
            for (int i = 0; i < n; i++)
            {
                var hit = _acertos[i];
                var parte = hit.collider.GetComponent<ParteDoSobrevivente>();
                if (hit.collider.gameObject.layer == Camada && (parte == null || parte.sobrevivente == null || parte.sobrevivente.Morto)) continue;   // os corpos no chão
                if (parte != null)
                {
                    if (parte.sobrevivente == this) continue;   // as próprias partes
                    return new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };   // um colega na frente
                }
                if (hit.collider.gameObject == gameObject) continue;
                var jogador = hit.collider.gameObject.layer == JogadorRede.CamadaDaCabeca ? hit.collider.GetComponentInParent<JogadorRede>() : hit.collider.GetComponent<JogadorRede>();
                if (jogador != null && jogador.AlvoValido)
                {
                    float dano = Armas.Dano(d, hit.distance);
                    if (!danoNoJogador.TryGetValue(jogador, out var antes) || dano > antes.dano) danoNoJogador[jogador] = (dano, hit.point);
                    return new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };
                }
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.zumbi != null)
                {
                    if (hitbox.zumbi.Morto) continue;
                    float dano = Armas.Dano(d, hit.distance);
                    if (!noZumbi.TryGetValue(hitbox.zumbi, out var antes) || dano > antes.Item3) noZumbi[hitbox.zumbi] = (hitbox, hit.point, dano);
                    return new JogadorRede.Impacto { tipo = JogadorRede.TipoDeAcerto.Carne, ponto = hit.point, normal = hit.normal };
                }
                var tipo = hit.rigidbody != null || hit.collider is CharacterController ? JogadorRede.TipoDeAcerto.Outro : JogadorRede.TipoDeAcerto.Cenario;
                return new JogadorRede.Impacto { tipo = tipo, ponto = hit.point, normal = hit.normal };
            }
            return impacto;
        }

        /// <summary>
        /// A abertura do tiro (graus): a da arma, mais a de quem não é jogador (a sniper quase nada), a distância, o escuro
        /// (sem luz no alvo), o alvo correndo e ele andando; dobrada logo que ele levanta a arma (assenta em 1,5 s).
        /// </summary>
        float Dispersao(DadosDaArma d, float distancia, IAlvoDeZumbi quem)
        {
            // (18/11/2026) a mira que assenta (Sobrevivente.Combate.cs): o quanto ele já acompanhou o alvo (sem cair a cada
            // vez que volta para a proteção) multiplica a abertura de 1,7 (acabou de ver) a 0,55 (assentada); o militar e a
            // sniper com menos de base. Antes: a base de +1,5° e o dobro a cada vez que ele levantava a arma
            bool sniper = Arma == TipoDeArma.Sniper;
            float baseDele = sniper ? (_velocidade < 0.3f ? 0.12f : 0.6f) : Militar ? 0.55f : 0.9f;
            float a = d.dispersao * 1.2f + baseDele + distancia * (sniper ? 0.003f : Militar ? 0.016f : 0.024f);
            a *= Mathf.Lerp(1.7f, 0.55f, _conhece);
            // e o meio segundo de levantar a arma
            if (_mirandoDesde >= 0f) a *= Mathf.Lerp(1.35f, 1f, (Time.time - _mirandoDesde) / 0.6f);
            if (quem is JogadorRede j)
            {
                bool comLuz = (j.Lanterna != null && j.Lanterna.Ligada.Value) || PosteDeLuz.Ilumina(j.transform.position);
                if (CicloDoDia.Noite > 0.5f && !comLuz) a += 2.5f;
                if (j.Velocidade > 3f) a += 1.5f * (1f - 0.5f * _conhece);
            }
            if (_velocidade > 1f) a += 1.5f;
            return a;
        }

        // ------------------------------------------------------------------ a recarga

        // devolve falso sem munição na mochila
        // (18/11/2026, a pedido: "vamos fazer eles com munição infinita") sempre recarrega, sem gastar nada da mochila; antes,
        // sem caixa nem reserva, ele fugia (o do acampamento ia se esconder na casa)
        bool Recarregar()
        {
            _recarregando = true;
            _recarregaAte = Time.time + Random.Range(1.8f, 2.6f);
            Falar(Frase.Recarregando);
            return true;
        }

        void TerminarRecarga()
        {
            _recarregando = false;
            pente = DadosDaArma.capacidade;   // (a munição infinita, 18/11/2026)
        }

        // ------------------------------------------------------------------ o dano e a morte

        /// <summary>No anfitrião: quem o matou (o Abutre, 06/11/2026: a mochila dele leva mais coisas).</summary>
        [System.NonSerialized] public JogadorRede matador;

        void Ferir(float dano, Vector3 de, JogadorRede autor, bool naCabeca)
        {
            if (!IsServer || _morto.Value) return;
            vida -= dano;
            // ferir alguém do acampamento deixa o acampamento inteiro contra os jogadores
            if (autor != null && DoAcampamento) AcampamentosDoMundo.Instancia?.TornarHostil(acampamento, autor);
            if (autor != null && NaEstrada) CarrosQuebradosDoMundo.Instancia?.TornarHostil(cena, autor);
            if (autor != null) grupo?.Ver(autor, autor.transform.position, this);
            else if (vida > 0f && Random.value < 0.3f) Falar(Frase.Zumbis);
            VozDoNpc(vida > 0f ? TipoDeVoz.Golpe : TipoDeVoz.Dor);   // (19/11/2026: a voz)
            if (vida > 0f)
            {
                ReagirAoTiro(Time.time);   // (18/11/2026) para trás da proteção, a mira atrapalhada
                return;
            }
            autor?.Vida?.MatouUmaPessoa();   // a mente (06/11/2026): matar abala (o Sangue frio, a ponta do Assassino, tira)
            matador = autor;   // (o Abutre: a mochila que ele larga leva mais coisas)
            // o XP (06/11/2026, a árvore de habilidades)
            bool militar = Identidade.Value.papel == PapelDoSobrevivente.Militar;
            autor?.Inventario?.GanharXp(militar ? Habilidades.XpDoMilitar : Habilidades.XpDoSobrevivente, militar ? "militar" : "sobrevivente");
            // o empurrão da queda: para longe de quem atirou (ou de quem golpeou)
            Vector3 dir = autor != null ? de - autor.transform.position : transform.position - de;
            dir.y = 0f;
            Morrer(dir.sqrMagnitude > 0.01f ? dir.normalized : -transform.forward);
        }

        void Morrer(Vector3 para)
        {
            _empurrao.Value = para * 35f + Vector3.up * 6f;
            _morto.Value = true;
            _mirando.Value = false;
            _luz.Value = false;
            _ajoelhado.Value = false;
            if (agente != null)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.enabled = false;
            }
            ZombieManager.OutrosAlvos.Remove(this);
            grupo?.Morreu(this);
            if (DoAcampamento) AcampamentosDoMundo.Instancia?.AoMorrer(this);
            else if (NaEstrada) CarrosQuebradosDoMundo.Instancia?.AoMorrer(this);
            else if (DaBase) BasesMilitaresDoMundo.Instancia?.AoMorrer(this);
            else SobreviventesDoMundo.Instancia?.AoMorrer(this);
        }
    }
}
