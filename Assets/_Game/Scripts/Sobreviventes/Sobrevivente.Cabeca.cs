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

        const int MascaraDoCenario = (1 << 0) | (1 << 11);   // o que tapa a visão e a linha de tiro (o cenário e os carros)
        /// <summary>
        /// O tiro deles no jogador vale 70% do da arma (as armas são medidas para os zumbis: a espingarda de perto, 115,
        /// matava o jogador com um tiro; agora 80).
        /// </summary>
        public const float DanoNoJogador = 0.7f;
        // o que o tiro dele pega: o cenário, os zumbis (7), os jogadores (9), os carros (11) e os outros sobreviventes (12)
        const int MascaraDoTiro = (1 << 0) | (1 << 7) | (1 << 9) | (1 << 11) | (1 << Camada);
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
            if (!agente.enabled)
            {
                // o militar longe do NavMesh dos jogadores (aparece a 170 m): parado no posto, vê e atira de longe
                if (DaBase) PensarParado();
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
            // a lanterna: explorando à noite; no tiroteio, só com um zumbi perto (para afastá-lo). No acampamento, só os
            // guardas (o resto fica na luz das fogueiras e das bancas)
            bool luz = noite && (!combate || zumbiPerto) && !_fugindo && (!DoAcampamento || DeGuarda || zumbiPerto);
            if (_luz.Value != luz) _luz.Value = luz;

            _mirandoEm = null;
            _querAjoelhar = false;
            if (_fugindo) Fugir(agora);
            else if (Escondido) Esconder(agora);
            else if (SairDoFogo(agora)) { }   // o fogo do molotov (11/11/2026, a etapa 4: Sobrevivente.Fogo.cs)
            else if (_zumbi != null && ZumbiAmeaca(_zumbi)) LutarComZumbi(agora);
            else if (combate) Combater(agora);
            else if (NaEstrada) FicarNaEstrada(agora);
            else if (DoAcampamento || DaBase) FicarNoPosto(agora);
            else Explorar(agora);
            Mirar();
            if (_ajoelhado.Value != _querAjoelhar) _ajoelhado.Value = _querAjoelhar;
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
        }

        // ------------------------------------------------------------------ perceber

        void Olhar(float agora)
        {
            if (grupo == null) return;
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
            var inimigo = grupo.inimigo;
            Vector3 alvo = inimigo.transform.position;
            Vector3 peito = alvo + Vector3.up * 1.3f;
            bool vejo = (peito - Olho).sqrMagnitude < AlcanceDaVista * AlcanceDaVista && !Physics.Linecast(Olho, peito, MascaraDoCenario, QueryTriggerInteraction.Ignore);
            if (vejo) grupo.Ver(inimigo, alvo, null);
            Vector3 onde = vejo ? alvo : grupo.ondeViu;
            if (ComBranca)
            {
                _temProtecao = false;
                BrigarDePerto(inimigo, onde, vejo, agora);
                return;
            }

            // sem uma proteção (ou ela ficou ruim): procura outra; sem nenhuma, fica e atira de onde está
            if (!_temProtecao && agora >= _proximaProtecao)
            {
                _proximaProtecao = agora + Random.Range(3f, 5f);
                _temProtecao = AcharProtecao(onde, out _protecao, out _espiar, out _temEspiar);
                _fase = 0;
            }
            if (!_temProtecao)
            {
                if (vejo)
                {
                    AtirarNo(inimigo, peito, agora);
                    // sem proteção: anda de lado (fica mais difícil de acertar), sem chegar a menos de 10 m
                    if (agora >= _proximoLado)
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
                else if (agora - grupo.viuEm > 6f && agora >= _proximoCaminho)
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
                    if ((transform.position - _protecao).sqrMagnitude < 0.8f * 0.8f)
                    {
                        _fase = 1;
                        _fimDaFase = agora + Random.Range(1f, 2.5f);
                        Parar();
                    }
                    break;
                case 1:   // escondido
                    if (_recarregando || agora < _fimDaFase) break;
                    _fase = 2;
                    _fimDaFase = agora + Random.Range(1.6f, 3.2f);
                    if (_temEspiar) Ir(_espiar, 2.2f, 0.2f);
                    break;
                case 2:   // espiando: atira enquanto vê
                    if (vejo) AtirarNo(inimigo, peito, agora);
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
            bool avancar = grupo != null && Time.time - grupo.viuEm > 6f;
            float melhor = float.MaxValue;
            Vector3 aqui = transform.position;
            for (int k = 0; k < 18; k++)
            {
                float ang = k * (Mathf.PI * 2f / 18f) + Random.Range(-0.2f, 0.2f);
                float raio = Random.Range(2f, 14f);
                var p = aqui + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * raio;
                if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                p = hit.position;
                if ((p - inimigo).sqrMagnitude < 6f * 6f) continue;   // nem colado no inimigo
                if (!Physics.Linecast(p + Vector3.up * 1.4f, alvo, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;   // não tapa
                float nota = (p - aqui).magnitude + (avancar ? Vector3.Distance(p, inimigo) * 0.8f : 0f);
                if (nota >= melhor) continue;
                // o lugar de espiar: ao lado, com a linha livre
                Vector3 lado = Vector3.Cross(Vector3.up, (inimigo - p).normalized);
                bool achou = false;
                Vector3 e = p;
                foreach (float s in new[] { 1f, -1f, 1.6f, -1.6f })
                {
                    var q = p + lado * s;
                    if (!NavMesh.SamplePosition(q, out var hq, 0.8f, NavMesh.AllAreas)) continue;
                    if (Physics.Linecast(hq.position + Vector3.up * 1.4f, alvo, MascaraDoCenario, QueryTriggerInteraction.Ignore)) continue;
                    e = hq.position;
                    achou = true;
                    break;
                }
                melhor = nota;
                ponto = p;
                espiar = e;
                temEspiar = achou;
            }
            return melhor < float.MaxValue;
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

        void AtirarNo(IAlvoDeZumbi quem, Vector3 ponto, float agora)
        {
            _mirandoEm = ponto;
            if (_recarregando) return;
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
            if (_mirandoDesde < 0f || agora - _mirandoDesde < 0.45f) return;
            var d = DadosDaArma;
            // a cadência: as automáticas em rajadas (de 3 a 6 tiros), as outras com uma pausa a mais
            if (d.automatica)
            {
                if (agora > _rajadaAte + 0.6f) _rajadaAte = agora + Random.Range(0.25f, 0.6f);
                _proximoTiro = agora < _rajadaAte ? agora + d.intervalo * 1.1f : _rajadaAte + Random.Range(0.4f, 0.9f);
            }
            else _proximoTiro = agora + Mathf.Max(d.intervalo, 0.2f) * Random.Range(1.8f, 3f);
            pente--;
            Disparar(ponto, quem, d);
        }

        void Disparar(Vector3 ponto, IAlvoDeZumbi quem, DadosDaArma d)
        {
            Vector3 origem = Olho + transform.forward * 0.4f;
            Vector3 direcao = (ponto - origem).normalized;
            float distancia = Vector3.Distance(origem, ponto);
            float abertura = Dispersao(d, distancia, quem);
            var impactos = new JogadorRede.Impacto[d.chumbos];
            // o dano de cada alvo, uma vez só (a espingarda: o maior dos chumbos)
            var danoNoJogador = new Dictionary<JogadorRede, (float dano, Vector3 ponto)>();
            var noZumbi = new Dictionary<Zumbi, (Hitbox h, Vector3 p, float dano)>();
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
                var jogador = hit.collider.GetComponent<JogadorRede>();
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
            bool sniper = Arma == TipoDeArma.Sniper;
            float a = d.dispersao * 1.5f + (sniper ? 0.35f : 1.5f) + distancia * (sniper ? 0.004f : 0.035f);
            // a mira assenta: o dobro logo que ele levanta a arma, a normal depois de 1,5 s mirando
            a *= _mirandoDesde < 0f ? 2f : Mathf.Lerp(2f, 1f, (Time.time - _mirandoDesde) / 1.5f);
            if (quem is JogadorRede j)
            {
                bool comLuz = (j.Lanterna != null && j.Lanterna.Ligada.Value) || PosteDeLuz.Ilumina(j.transform.position);
                if (CicloDoDia.Noite > 0.5f && !comLuz) a += 2.5f;
                if (j.Velocidade > 3f) a += 1.5f;
            }
            if (_velocidade > 1f) a += 1.5f;
            return a;
        }

        // ------------------------------------------------------------------ a recarga

        // devolve falso sem munição na mochila
        bool Recarregar()
        {
            var d = DadosDaArma;
            var caixa = Itens.CaixaDe(d.calibre);
            int tem = 0;
            foreach (var it in mochila) if (it.item == caixa) tem += it.qtd;
            if (tem <= 0 && reserva <= 0) return false;
            _recarregando = true;
            _recarregaAte = Time.time + Random.Range(1.8f, 2.6f);
            Falar(Frase.Recarregando);
            return true;
        }

        void TerminarRecarga()
        {
            _recarregando = false;
            var d = DadosDaArma;
            var caixa = Itens.CaixaDe(d.calibre);
            int falta = d.capacidade - pente;
            for (int i = mochila.Count - 1; i >= 0 && falta > 0; i--)
            {
                var it = mochila[i];
                if (it.item != caixa) continue;
                int tira = Mathf.Min(falta, it.qtd);
                it.qtd = (ushort)(it.qtd - tira);
                falta -= tira;
                pente += tira;
                if (it.qtd == 0) mochila.RemoveAt(i);
                else mochila[i] = it;
            }
            // o resto, da reserva (os militares)
            int daReserva = Mathf.Min(falta, reserva);
            reserva -= daReserva;
            pente += daReserva;
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
            if (vida > 0f) return;
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
