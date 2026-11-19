using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// Os grupos de busca do mundo (02/11/2026, a pedido: "NPCs armados ... que encontramos explorando as cidades"), no
    /// objeto "Mundo", no anfitrião:
    ///  - os grupos de cada cidade (a pedido, depois do primeiro teste: "aumentar o spawn deles"): 6 numa cidade grande,
    ///    cada um morando numa parte dela (uma fatia de 60° em volta do meio), e 1 na pequena e na vila. Cada grupo tem de 6
    ///    a 8 (a pedido), sorteado pela semente do mundo, pela cidade, pela parte e pela geração (as armas, as roupas); o que
    ///    levam na mochila sai na hora;
    ///  - até <see cref="gruposJuntos"/> grupos no mundo de uma vez: com um jogador numa parte da cidade, o grupo dela
    ///    aparece depois de um tempo (20–60 s na cidade grande; 1,5–4 min na pequena), fora da vista, a 40–52 m (dentro do
    ///    NavMesh em volta dos jogadores), e no máximo um a cada 15 s;
    ///  - longe de todos os jogadores (75 m), o grupo sai, e volta mais tarde com quem estava vivo (4–8 min na grande;
    ///    10–20 min na pequena);
    ///  - os mortos continuam mortos (a máscara dos vivos, no save); um grupo todo morto é trocado por outro (a próxima
    ///    geração) 2 dias depois;
    ///  - os tiros dos jogadores perto (70 m) avisam o grupo (<see cref="OuvirTiro"/>);
    ///  - quem morre deixa a mochila no chão (<see cref="MochilasNoChao"/>).
    /// O F1 põe o grupo perto e tira todos.
    /// </summary>
    public class SobreviventesDoMundo : NetworkBehaviour
    {
        public static SobreviventesDoMundo Instancia { get; private set; }

        [Tooltip("O sobrevivente (Prefabs/Sobreviventes/Sobrevivente; também na lista de prefabs da rede).")]
        public GameObject prefab;
        [Tooltip("Desligado: nenhum grupo aparece (o F1 ainda põe).")]
        public bool ligado = true;
        [Tooltip("Longe assim de todos os jogadores, o grupo sai (m).")]
        public float distanciaParaSair = 75f;
        [Tooltip("Até onde o tiro de um jogador avisa o grupo (m).")]
        public float ouvidoDoTiro = 70f;
        [Tooltip("Quantos grupos uma cidade grande tem (cada um numa parte dela; no máximo 8).")]
        public int gruposNaCidadeGrande = 6;
        [Tooltip("Quantos grupos podem estar no mundo ao mesmo tempo.")]
        public int gruposJuntos = 2;

        /// <summary>O estado do grupo de uma cidade (o que o save guarda).</summary>
        public class EstadoDaCidade
        {
            public int geracao;
            /// <summary>Um bit por membro vivo (o grupo começa com todos).</summary>
            public int vivos = -1;
            public int quantos;
            public int diaDaMorte = -1;
            /// <summary>Quando o grupo pode aparecer (nesta partida; não vai no save).</summary>
            public float proximaVez = -1f;
        }

        // o estado de cada grupo, pela chave (a cidade e a parte dela: Chave)
        readonly Dictionary<int, EstadoDaCidade> _cidades = new Dictionary<int, EstadoDaCidade>();
        readonly List<GrupoDeSobreviventes> _ativos = new List<GrupoDeSobreviventes>();
        float _proximaConferencia, _proximoPensar, _proximoNascimento;

        // os que ainda vão nascer, um por quadro (16/11/2026, o teste de desempenho: "às vezes dá um pouco de lag"; o grupo
        // inteiro, de 6 a 8, num quadro só tomava até 0,8 s no Editor: cada um é instanciado, vestido, com as malhas sem as
        // faces cobertas, e posto na rede). O sorteio é o mesmo de antes, feito todo na hora do Nascer
        sealed class ANascer
        {
            public GrupoDeSobreviventes grupo;
            public Vector3 pos, olharPara;
            public IdentidadeDoSobrevivente id;
            public TipoDeArma arma;
            public Item branca;
        }
        readonly List<ANascer> _aNascer = new List<ANascer>();

        /// <summary>Quantos do grupo ainda vão nascer (um por quadro).</summary>
        public int ANascerDo(GrupoDeSobreviventes g) => g != null ? g.nascendo : 0;

        /// <summary>Os grupos que estão no mundo agora (no anfitrião), para o F1 e os testes.</summary>
        public IReadOnlyList<GrupoDeSobreviventes> Ativos => _ativos;
        /// <summary>O primeiro grupo no mundo agora (ou null).</summary>
        public GrupoDeSobreviventes Ativo => _ativos.Count > 0 ? _ativos[0] : null;
        public IReadOnlyDictionary<int, EstadoDaCidade> Cidades => _cidades;

        /// <summary>A chave do grupo de uma parte da cidade (até 8 partes).</summary>
        public static int Chave(int cidade, int parte) => cidade * 8 + parte;

        void Awake()
        {
            Instancia = this;
            // as bases militares (03/11/2026): no mesmo objeto, posto aqui (sem mexer na cena do Mundo, que é dos dois)
            if (GetComponent<BasesMilitaresDoMundo>() == null) gameObject.AddComponent<BasesMilitaresDoMundo>();
            // as emboscadas nas cidades grandes (18/11/2026), também daqui
            if (GetComponent<EmboscadasDoMundo>() == null) gameObject.AddComponent<EmboscadasDoMundo>();
            // os moradores do bunker (06/11/2026, a parte 6 do Plano_NPCs_Bunker.md), também daqui
            if (GetComponent<Moradores.MoradoresDoBunker>() == null) gameObject.AddComponent<Moradores.MoradoresDoBunker>();
            // as falas já prontas (16/11/2026: a tabela delas era montada na primeira fala, 9 ms no meio da partida)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(FalasDosSobreviventes).TypeHandle);
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Instancia = this;
            _cidades.Clear();
            _ativos.Clear();
            _aNascer.Clear();
            _proximoNascimento = 0f;
        }

        public override void OnNetworkDespawn()
        {
            _ativos.Clear();
            _aNascer.Clear();
        }

        // o mundo do jogo (12/11/2026: o sorteado ou o Lakewood; as cidades, a semente e a altura vêm dele)
        static IMundoDoJogo Mundo => MundoDoJogo.Pronto;

        bool Pronto => IsServer && IsSpawned && MundoDoJogo.Pronto != null && prefab != null;

        /// <summary>O estado do grupo da parte da cidade.</summary>
        public EstadoDaCidade Estado(int cidade, int parte = 0)
        {
            int k = Chave(cidade, parte);
            if (!_cidades.TryGetValue(k, out var e)) _cidades[k] = e = new EstadoDaCidade();
            return e;
        }

        /// <summary>Quantos grupos a cidade tem: 6 na grande (o <see cref="gruposNaCidadeGrande"/>), 1 na pequena e na vila.</summary>
        public int GruposNaCidade(int cidade) => Mundo != null ? Mundo.GruposDaCidade(cidade, gruposNaCidadeGrande) : 1;

        /// <summary>A parte da cidade onde o ponto está (a fatia em volta do meio; 0 numa cidade de um grupo só).</summary>
        public int ParteDaCidade(int cidade, Vector3 p)
        {
            int n = GruposNaCidade(cidade);
            var m = Mundo;
            if (n <= 1 || m == null || cidade < 0 || cidade >= m.QuantasCidades) return 0;
            var c = m.MeioDaCidade(cidade);
            float ang = Mathf.Atan2(p.z - c.y, p.x - c.x);
            if (ang < 0f) ang += Mathf.PI * 2f;
            return Mathf.Clamp(Mathf.FloorToInt(ang / (Mathf.PI * 2f / n)), 0, n - 1);
        }

        bool NoMundo(int cidade, int parte)
        {
            foreach (var g in _ativos) if (g.cidade == cidade && g.setor == parte) return true;
            return false;
        }

        // ------------------------------------------------------------------ o quadro

        void Update()
        {
            if (!Pronto) return;
            if (_aNascer.Count > 0) NascerUm();
            float agora = Time.time;
            if (_ativos.Count > 0 && agora >= _proximoPensar)
            {
                _proximoPensar = agora + 0.2f;
                foreach (var g in _ativos) g.Pensar();
            }
            if (agora < _proximaConferencia) return;
            _proximaConferencia = agora + 1f;
            for (int i = _ativos.Count - 1; i >= 0; i--) ConferirOAtivo(_ativos[i]);
            if (ligado && _ativos.Count < Mathf.Max(1, gruposJuntos) && agora >= _proximoNascimento) TentarUmGrupo();
        }

        void ConferirOAtivo(GrupoDeSobreviventes g)
        {
            if (ANascerDo(g) > 0) return;   // (ainda nascendo)
            // quem fugiu e sumiu de vista sai (vivo)
            foreach (var m in g.membros.ToArray())
                if (m != null && !m.Morto && m.Fugiu) Tirar(m);
            bool alguemPerto = false;
            foreach (var m in g.membros)
                if (m != null && GrupoDeSobreviventes.PertoDeUmJogador(m.transform.position) < distanciaParaSair) alguemPerto = true;
            if (alguemPerto && g.membros.Count > 0) return;
            Recolher(g);
            if (g.cidade < 0) return;   // o grupo de teste do F1, fora de uma cidade
            var e = Estado(g.cidade, g.setor);
            bool grande = CidadeGrande(g.cidade);
            e.proximaVez = Time.time + (grande ? Random.Range(240f, 480f) : Random.Range(600f, 1200f));
        }

        void TentarUmGrupo()
        {
            var d = Mundo;
            if (d == null) return;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                var p = j.transform.position;
                int cidade = d.CidadeEm(p.x, p.z);
                if (cidade < 0) continue;
                int parte = ParteDaCidade(cidade, p);
                if (NoMundo(cidade, parte)) continue;   // o desta parte já está no mundo
                var e = Estado(cidade, parte);
                bool grande = CidadeGrande(cidade);
                // a primeira vez que alguém entra nesta parte da cidade nesta partida: o grupo vem daqui a pouco
                if (e.proximaVez < 0f) e.proximaVez = Time.time + (grande ? Random.Range(20f, 60f) : Random.Range(90f, 240f));
                if (Time.time < e.proximaVez) continue;
                if (!TemVivos(e))
                {
                    // todo morto: outro grupo, 2 dias depois
                    if (e.diaDaMorte < 0 || CicloDoDia.Dia < e.diaDaMorte + 2) continue;
                    e.geracao++;
                    e.vivos = -1;
                    e.quantos = 0;
                    e.diaDaMorte = -1;
                }
                if (Nascer(cidade, parte, e, p, false))
                {
                    _proximoNascimento = Time.time + 15f;   // (dois grupos não aparecem juntos)
                    return;
                }
                e.proximaVez = Time.time + 20f;   // não achou lugar: tenta de novo daqui a pouco
            }
        }

        static bool TemVivos(EstadoDaCidade e) => e.vivos != 0;

        bool CidadeGrande(int cidade) => Mundo != null && Mundo.CidadeGrande(cidade);

        // ------------------------------------------------------------------ nascer e sair

        /// <summary>
        /// No anfitrião: o grupo da parte da cidade nasce perto do jogador (a 40–52 m, fora da vista de todos; o F1, mais
        /// perto e sem conferir a vista). Devolve se nasceu.
        /// </summary>
        public bool Nascer(int cidade, int parte, EstadoDaCidade e, Vector3 perto, bool doF1)
        {
            if (!Pronto || (!doF1 && _ativos.Count >= Mathf.Max(1, gruposJuntos))) return false;
            if (!AcharLugar(perto, doF1, out var lugar)) return false;
            var s = new System.Random(unchecked(Mundo.SementeDaPartida * 7919 + cidade * 104729 + e.geracao * 31337 + parte * 7727 + 11));
            int quantos = 6 + s.Next(3);
            if (e.quantos == 0) e.quantos = quantos;
            quantos = e.quantos;
            var g = new GrupoDeSobreviventes { cidade = cidade, setor = parte, geracao = e.geracao, quantos = quantos };
            bool temSub = false;
            int fila = 0;
            for (int i = 0; i < quantos; i++)
            {
                var (arma, branca) = SortearArma(s, i, ref temSub);
                byte roupa = (byte)(s.Next(100) < 70 ? 0 : s.Next(100) < 50 ? 1 : 3);
                int sementeDaRoupa = s.Next();
                if ((e.vivos & (1 << i)) == 0) continue;   // este morreu
                var p = lugar + Quaternion.Euler(0f, i * 47f, 0f) * Vector3.forward * (1.2f + (i % 3) * 0.9f);
                if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) p = hit.position;
                else p = lugar;
                // (nasce no Update, um por quadro: NascerUm)
                _aNascer.Add(new ANascer
                {
                    grupo = g, pos = p, olharPara = perto, arma = arma, branca = branca,
                    id = new IdentidadeDoSobrevivente
                    {
                        semente = sementeDaRoupa, arma = arma, roupa = roupa, grupo = (ushort)Mathf.Max(0, Chave(cidade, parte)),
                        papel = i == 0 ? PapelDoSobrevivente.Lider : PapelDoSobrevivente.Busca, indice = (byte)i, branca = branca,
                    },
                });
                fila++;
            }
            if (fila == 0) return false;
            g.nascendo = fila;
            _ativos.Add(g);
            // o primeiro (o líder, se vivo) já agora
            NascerUm();
            Debug.Log($"[Sobreviventes] O grupo da cidade {cidade}, parte {parte} (geração {e.geracao}) apareceu: {fila} de {quantos}, um por quadro, a {Vector3.Distance(lugar, perto):0} m ({_ativos.Count} no mundo).");
            return true;
        }

        // o próximo da fila nasce (o Update chama um por quadro); o de um grupo que já saiu é jogado fora
        void NascerUm()
        {
            while (_aNascer.Count > 0)
            {
                var a = _aNascer[0];
                _aNascer.RemoveAt(0);
                a.grupo.nascendo = Mathf.Max(0, a.grupo.nascendo - 1);
                if (!_ativos.Contains(a.grupo)) continue;
                var p = a.pos;
                var olhar = new Vector3(a.olharPara.x - p.x, 0f, a.olharPara.z - p.z);
                var go = Instantiate(prefab, p, Quaternion.LookRotation(olhar.sqrMagnitude < 1e-6f ? Vector3.forward : olhar));
                var sob = go.GetComponent<Sobrevivente>();
                sob.Identidade.Value = a.id;
                sob.grupo = a.grupo;
                Encher(sob, a.arma, false, a.branca);
                go.GetComponent<NetworkObject>().Spawn(true);
                if (sob.agente != null && sob.agente.isOnNavMesh) sob.agente.Warp(p);
                a.grupo.membros.Add(sob);
                return;
            }
        }

        // as armas dos civis (03/11/2026, a pedido: "armas brancas, tacos, facas, pistolas, raramente uma submetralhadora";
        // antes, rifles, a espingarda e até a sniper): o líder com a pistola (às vezes a submetralhadora); os outros, ~45%
        // uma branca (a faca, o bastão com pregos, o machado ou a lança), ~45% a pistola e ~10% a submetralhadora, no
        // máximo uma por grupo (os outros: 50% branca, 40% pistola e 10% a submetralhadora; com o líder, ~45% de brancas).
        // Desde 09/11/2026 (a sucata, 2ª leva: "só os militares usam as reais"), as de fogo são as de sucata (ArmaDoCivil)
        static (TipoDeArma arma, Item branca) SortearArma(System.Random s, int i, ref bool temSub)
        {
            int r = s.Next(100);
            if (i == 0)
            {
                if (r < 25 && !temSub)
                {
                    temSub = true;
                    return (ArmaDoCivil(TipoDeArma.Submetralhadora), Item.Nada);
                }
                return (ArmaDoCivil(TipoDeArma.Pistola), Item.Nada);
            }
            if (r < 10 && !temSub)
            {
                temSub = true;
                return (ArmaDoCivil(TipoDeArma.Submetralhadora), Item.Nada);
            }
            if (r < 50) return (ArmaDoCivil(TipoDeArma.Pistola), Item.Nada);
            int b = s.Next(100);
            var branca = b < 30 ? Item.Faca : b < 60 ? Item.Bastao : b < 85 ? Item.Machado : Item.Lanca;
            return (ArmaDoCivil(TipoDeArma.Pistola), Itens.NoJogo(branca) ? branca : Item.Faca);
        }

        /// <summary>
        /// A arma de fogo de um civil (09/11/2026, a sucata, 2ª leva: as armas reais só com os militares): a de sucata no
        /// lugar da real (a pistola, a submetralhadora; as armas longas, o rifle de precisão de sucata). Os sorteios de cada
        /// lugar continuam os de antes: só a arma que sai muda.
        /// </summary>
        public static TipoDeArma ArmaDoCivil(TipoDeArma real) => real switch
        {
            TipoDeArma.Pistola => TipoDeArma.PistolaSucata,
            TipoDeArma.Submetralhadora => TipoDeArma.SubmetralhadoraSucata,
            TipoDeArma.Rifle or TipoDeArma.Espingarda or TipoDeArma.Sniper => TipoDeArma.RifleSucata,
            _ => real,
        };

        /// <summary>
        /// A reserva do civil com uma arma de sucata de pente curto (a pistola, de um tiro, e o rifle, de três): de tanto a
        /// tanto (não fica no saque, como a dos militares). Sem ela, ele fugia depois do primeiro tiro.
        /// </summary>
        public const int ReservaDoCivilMin = 4, ReservaDoCivilMax = 9;

        /// <summary>
        /// O que ele leva. A munição (18/11/2026, a pedido: "vamos fazer eles com munição infinita, porém quando morrer, vão
        /// dropar poucas munição. Coisa de 10 munição de prego, ou 5 de parafuso"): o pente cheio e nenhuma caixa; a munição
        /// dele não acaba (o Sobrevivente.Recarregar), e o que fica no chão quando ele morre é o <see cref="PoucaMunicao"/>
        /// (antes, 03/11/2026: o pente pela metade, às vezes uma caixa e uma reserva). O resto: os civis (os grupos, o
        /// acampamento, o carro quebrado) levam comida e bebida (de 1 a 3 e de 1 a 2); os militares (<paramref name="militar"/>),
        /// a barra, às vezes o feijão, a água e o remédio, em 10% o antibiótico, e raramente o colete tático ou o capacete
        /// militar como item.
        /// </summary>
        internal static void Encher(Sobrevivente sob, TipoDeArma arma, bool militar = false, Item branca = Item.Nada)
        {
            var d = Armas.Dados(arma);
            sob.mochila.Clear();
            sob.reserva = 0;
            bool deBranca = Itens.EhBranca(branca);
            sob.pente = deBranca ? 0 : d.capacidade;
            Item[] comidas = { Item.Comida, Item.Feijao, Item.Carne, Item.Milho, Item.Sopa, Item.Salgadinho, Item.BarraDeEnergia };
            Item[] bebidas = { Item.Agua, Item.RefrigeranteLata, Item.Energetico, Item.Suco };
            Item[] materiais = { Item.Pano, Item.Corda, Item.FitaAdesiva, Item.Pregos, Item.Fios, Item.PecasDeMetal };
            if (militar)
            {
                // a ração: a barra; às vezes o feijão, a água e o remédio (05/11/2026, sem a vida voltando sozinha: em 45%
                // deles, pelos pesos dos militares, com o kit e a tala); em 10%, à parte, o antibiótico (a etapa 2: o
                // militar é quem ainda tem); raramente o colete ou o capacete (o que ele veste fica com o corpo)
                Por(sob, Item.BarraDeEnergia, 1);
                if (Random.value < 0.3f) Por(sob, Item.Feijao, 1);
                if (Random.value < 0.6f) Por(sob, Item.Agua, 1);
                if (Random.value < 0.45f) Por(sob, Sortear(RemediosDosMilitares), 1);
                if (Random.value < 0.1f) Por(sob, Item.Antibiotico, 1);
                if (Random.value < 0.08f) PorRoupa(sob, "Colete_Tatico");
                if (Random.value < 0.06f) PorRoupa(sob, "Capacete_Militar");
                if (Random.value < 0.15f) Por(sob, Item.Pilhas, 2);
            }
            else
            {
                int nComidas = Random.Range(1, 4), nBebidas = Random.Range(1, 3);
                for (int i = 0; i < nComidas; i++) Por(sob, comidas[Random.Range(0, comidas.Length)], 1);
                for (int i = 0; i < nBebidas; i++) Por(sob, bebidas[Random.Range(0, bebidas.Length)], 1);
                if (Random.value < 0.35f) Por(sob, Sortear(RemediosDosCivis), 1);
                if (Random.value < 0.4f) Por(sob, materiais[Random.Range(0, materiais.Length)], Random.Range(1, 3));
                if (Random.value < 0.2f) Por(sob, Item.Pilhas, 2);
                if (!deBranca && Random.value < 0.1f) Por(sob, Item.Faca, 1);
                if (Random.value < ChanceDeSucataDoCivil) Por(sob, Item.Sucata, Random.Range(1, 4));   // (a sucata, 09/11/2026)
            }
            float r = Random.value;
            sob.mochilaVestida = r < 0.45f ? Item.MochilaPequena : r < 0.75f ? Item.MochilaMedia : r < 0.85f ? Item.MochilaGrande : Item.Nada;
            sob.dinheiro = Random.value < 0.6f ? Random.Range(5, 80) : 0;
        }

        /// <summary>A chance de o civil levar de 1 a 3 sucatas na mochila (09/11/2026, a 2ª leva).</summary>
        public const float ChanceDeSucataDoCivil = 0.25f;

        // o remédio que o morto deixa, por peso (05/11/2026, o sistema de vida por membro): o civil, mais a atadura e os
        // comprimidos (o kit em 7 de cada 100 remédios); o militar, o kit de campanha e a tala
        static readonly (Item item, int peso)[] RemediosDosCivis =
        {
            (Item.Atadura, 30), (Item.Comprimidos, 25), (Item.Bandagem, 20), (Item.Gaze, 10), (Item.Tala, 8), (Item.KitMedico, 7),
        };
        static readonly (Item item, int peso)[] RemediosDosMilitares =
        {
            (Item.Atadura, 35), (Item.Bandagem, 25), (Item.KitMedico, 20), (Item.Tala, 20),
        };

        static Item Sortear((Item item, int peso)[] lista)
        {
            int soma = 0;
            foreach (var (_, peso) in lista) soma += peso;
            int r = Random.Range(0, soma);
            foreach (var (item, peso) in lista)
            {
                if (r < peso) return item;
                r -= peso;
            }
            return lista[lista.Length - 1].item;
        }

        // uma roupa do catálogo (pelo nome da peça), com as cores dele
        static void PorRoupa(Sobrevivente sob, string peca)
        {
            var item = Itens.RoupaDaPeca(peca);
            if (item != Item.Nada && Itens.NoJogo(item)) sob.mochila.Add(new ItemNaMochila { item = item, qtd = 1 });
        }

        static void Por(Sobrevivente sob, Item item, int qtd)
        {
            if (!Itens.NoJogo(item)) return;
            sob.mochila.Add(new ItemNaMochila { item = item, qtd = (ushort)Mathf.Max(1, Itens.EmPilha(item) || Itens.EhMunicao(item) ? qtd : Itens.QuantidadePadrao(item)) });
        }

        // um lugar no NavMesh a 40–52 m do jogador, longe dos outros jogadores e fora da vista de todos; o do F1, a 18–24 m e
        // à vista do jogador (para ver o grupo chegando; sem um lugar à vista, qualquer um)
        bool AcharLugar(Vector3 perto, bool doF1, out Vector3 lugar)
        {
            lugar = perto;
            float de = doF1 ? 18f : 40f, ate = doF1 ? 24f : 52f;
            for (int passada = 0; passada < (doF1 ? 2 : 1); passada++)
            {
                float giro = Random.Range(0f, 360f);
                for (int k = 0; k < 24; k++)
                {
                    var dir = Quaternion.Euler(0f, giro + k * 15f, 0f) * Vector3.forward;
                    var p = perto + dir * Random.Range(de, ate);
                    if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - perto.y) > 6f) continue;
                    bool visto = AlguemVe(hit.position + Vector3.up * 1.5f);
                    if (!doF1 && visto) continue;
                    if (doF1 && passada == 0 && !visto) continue;
                    if (GrupoDeSobreviventes.PertoDeUmJogador(hit.position) < de - 2f) continue;
                    lugar = hit.position;
                    return true;
                }
            }
            return false;
        }

        static bool AlguemVe(Vector3 ponto)
        {
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned) continue;
                var olho = j.transform.position + Vector3.up * 1.6f;
                if (!Physics.Linecast(olho, ponto, 1 << 0, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // o grupo sai do mundo (quem está vivo volta depois; os corpos saem, e as mochilas ficam no chão)
        void Recolher(GrupoDeSobreviventes g)
        {
            foreach (var m in g.membros.ToArray()) if (m != null) Tirar(m);
            g.membros.Clear();
            _aNascer.RemoveAll(a => a.grupo == g);
            g.nascendo = 0;
            _ativos.Remove(g);
            Debug.Log($"[Sobreviventes] O grupo da cidade {g.cidade}, parte {g.setor} saiu{(g.cidade >= 0 ? $" ({BitsVivos(Estado(g.cidade, g.setor))} vivos)" : "")}.");
        }

        void Tirar(Sobrevivente s)
        {
            s.grupo?.membros.Remove(s);
            var no = s.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true);
        }

        static int BitsVivos(EstadoDaCidade e)
        {
            int n = 0;
            for (int i = 0; i < Mathf.Max(e.quantos, 0); i++) if ((e.vivos & (1 << i)) != 0) n++;
            return n;
        }

        /// <summary>No anfitrião: um sobrevivente morreu (a máscara dos vivos e a mochila no chão).</summary>
        public void AoMorrer(Sobrevivente s)
        {
            if (!IsServer || s == null) return;
            var g = s.grupo;
            if (g != null && g.cidade >= 0)
            {
                var e = Estado(g.cidade, g.setor);
                e.vivos &= ~(1 << s.Identidade.Value.indice);
                if (BitsVivos(e) == 0) e.diaDaMorte = CicloDoDia.Dia;
            }
            LargarAMochila(s, null);
            Debug.Log($"[Sobreviventes] Um sobrevivente morreu ({s.Arma}); a mochila dele ficou no chão.");
        }

        /// <summary>
        /// No anfitrião: a mochila de quem morreu fica no chão, ao lado do corpo: a arma (com o pente), o que ele levava, os
        /// 'extras' (a mercadoria do comerciante) e o dinheiro.
        /// </summary>
        // o Abutre (06/11/2026, o Sortudo): de 2 a 3 coisas a mais na mochila de quem ele matou (comida, bebida, remédio e,
        // com a arma de fogo, uma caixa com umas balas dela)
        static readonly Item[] CoisasDoAbutre =
        {
            Item.Comida, Item.Feijao, Item.BarraDeEnergia, Item.Agua, Item.Suco, Item.Atadura, Item.Comprimidos, Item.Pilhas,
            Item.MacoDeCigarros,
        };

        // (o Abutre: com 1 ponto, uma ou duas coisas; com 2, duas ou três e a munição da arma dele)
        static void DoAbutre(Sobrevivente s, List<ItemNaMochila> itens, int pontos)
        {
            int n = pontos >= 2 ? Random.Range(2, 4) : Random.Range(1, 3);
            for (int i = 0; i < n; i++)
            {
                var it = CoisasDoAbutre[Random.Range(0, CoisasDoAbutre.Length)];
                itens.Add(new ItemNaMochila { item = it, qtd = (ushort)Mathf.Max(1, Itens.QuantidadePadrao(it)) });
            }
            if (pontos >= 2 && !s.ComBranca && Itens.EhArma(s.ItemDaArma))
                itens.Add(new ItemNaMochila { item = Itens.CaixaDe(Itens.CalibreDaArma(s.ItemDaArma)), qtd = (ushort)Random.Range(6, 15) });
        }

        /// <summary>
        /// A munição que fica com quem morreu (18/11/2026, a pedido: "coisa de 10 munição de prego, ou 5 de parafuso. pouca
        /// coisa"): uma caixa pequena do calibre da arma dele, sempre. Prego e 9 mm: de 8 a 12; parafuso e 5,56: de 4 a 6
        /// (o 5,56, de 6 a 10); a da espingarda, de 2 a 4; a da sniper, de 2 a 3.
        /// </summary>
        static void PoucaMunicao(Sobrevivente s, List<ItemNaMochila> itens)
        {
            var calibre = Itens.CalibreDaArma(s.ItemDaArma);
            var dados = Armas.Dados(s.Arma);
            int qtd = dados.calibre switch
            {
                Calibre.Prego or Calibre.Nove => Random.Range(8, 13),
                Calibre.Parafuso => Random.Range(4, 7),
                Calibre.Cinco56 => Random.Range(6, 11),
                Calibre.Doze => Random.Range(2, 5),
                _ => Random.Range(2, 4),
            };
            itens.Add(new ItemNaMochila { item = Itens.CaixaDe(calibre), qtd = (ushort)qtd });
        }

        internal static void LargarAMochila(Sobrevivente s, List<ItemNaMochila> extras)
        {
            var itens = new List<ItemNaMochila>(s.mochila);
            // (a arma do militar cai gasta, de 50% a 85%: passando de 70%, ela engasga às vezes; 03/11/2026)
            bool militar = s.Identidade.Value.papel == PapelDoSobrevivente.Militar;
            // (18/11/2026, a pedido: "reduzir a chance de looting que dropam armas") a arma nem sempre fica: quebrou na queda,
            // ficou embaixo do corpo. A do militar em 45%, a de fogo dos civis em 30%, a branca em 50%
            float fica = militar ? 0.45f : s.ComBranca ? 0.5f : 0.3f;
            // (18/11/2026: a de fogo cai com o pente quase vazio, de 0 a 2 balas; a munição dele era infinita)
            if (Random.value < fica)
                itens.Insert(0, new ItemNaMochila { item = s.ItemDaArma, qtd = (ushort)(s.ComBranca ? 1 : Mathf.Clamp(s.pente, 0, Random.Range(0, 3))),
                                                    desgaste = (byte)(militar ? Random.Range(50, 86) : Random.Range(15, 60)) });
            if (!s.ComBranca && Itens.EhArma(s.ItemDaArma)) PoucaMunicao(s, itens);
            if (extras != null) itens.AddRange(extras);
            int abutre = Habilidades.Pontos(s.matador, Player.Habilidade.Abutre);   // (o Abutre, 06/11/2026)
            if (abutre > 0) DoAbutre(s, itens, abutre);
            var lugar = s.transform.position + s.transform.right * 0.6f;
            MochilasNoChao.Instancia?.Criar(lugar, s.transform.eulerAngles.y, s.nome, s.mochilaVestida, itens, s.dinheiro);
        }

        /// <summary>No anfitrião: o tiro de um jogador; o grupo perto (até 70 m) fica sabendo de onde veio.</summary>
        public static void OuvirTiro(Vector3 ponto, JogadorRede autor)
        {
            var m = Instancia;
            if (m == null || !m.IsServer || autor == null) return;
            // (18/11/2026) todos os sobreviventes: os grupos de busca, os acampamentos, as bases e os carros quebrados (antes,
            // só os grupos de busca; dava para matar um acampamento um por um). O grupo que não é hostil não liga
            var avisados = new HashSet<GrupoDeSobreviventes>();
            foreach (var s in Sobrevivente.Todos)
            {
                if (s == null || s.Morto || s.Grupo == null || avisados.Contains(s.Grupo) || !s.Grupo.Hostil) continue;
                if ((s.transform.position - ponto).sqrMagnitude > m.ouvidoDoTiro * m.ouvidoDoTiro) continue;
                var errado = new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                s.Grupo.Ver(autor, autor.transform.position + errado, s);
                avisados.Add(s.Grupo);   // (um por grupo avisa)
            }
        }

        // ------------------------------------------------------------------ o F1 e o save

        /// <summary>
        /// O F1: o grupo da parte da cidade onde o jogador está (fora de uma cidade, um grupo de teste) a 18–24 m, agora (os
        /// outros saem).
        /// </summary>
        public string PorUmGrupoPerto(JogadorRede j)
        {
            if (!Pronto) return "Agora não dá (o mundo não está pronto, ou é o Dia 0).";
            TirarTodos();
            var p = j.transform.position;
            int cidade = Mundo.CidadeEm(p.x, p.z);
            int parte = cidade >= 0 ? ParteDaCidade(cidade, p) : 0;
            var e = cidade >= 0 ? Estado(cidade, parte) : new EstadoDaCidade();
            if (!TemVivos(e))
            {
                e.geracao++;
                e.vivos = -1;
                e.quantos = 0;
                e.diaDaMorte = -1;
            }
            if (!Nascer(cidade, parte, e, p, true)) return "Não achei lugar no NavMesh perto.";
            var novo = _ativos[_ativos.Count - 1];
            return $"Um grupo de {novo.membros.Count + ANascerDo(novo)} a uns 20 m" +
                   (cidade < 0 ? " (de teste: fora de uma cidade)." : $" (a parte {parte + 1} de {GruposNaCidade(cidade)} da cidade).");
        }

        /// <summary>O F1: tira os grupos do mundo (sem matar).</summary>
        public void TirarTodos()
        {
            foreach (var g in _ativos.ToArray()) Recolher(g);
        }

        /// <summary>O estado de cada grupo (o save): a cidade, a parte dela e o resto.</summary>
        public List<(int cidade, int parte, int geracao, int vivos, int quantos, int diaDaMorte)> Copia()
        {
            var lista = new List<(int, int, int, int, int, int)>();
            foreach (var (k, e) in _cidades)
                if (k >= 0 && (e.geracao != 0 || e.vivos != -1 || e.quantos != 0 || e.diaDaMorte >= 0))   // só o que mudou
                    lista.Add((k / 8, k % 8, e.geracao, e.vivos, e.quantos, e.diaDaMorte));
            return lista;
        }

        public void Restaurar(IEnumerable<(int cidade, int parte, int geracao, int vivos, int quantos, int diaDaMorte)> lista)
        {
            _cidades.Clear();
            foreach (var (c, s, g, v, q, d) in lista)
                _cidades[Chave(c, Mathf.Clamp(s, 0, 7))] = new EstadoDaCidade { geracao = g, vivos = v, quantos = q, diaDaMorte = d };
        }
    }
}
