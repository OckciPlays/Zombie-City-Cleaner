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
            _proximoNascimento = 0f;
        }

        public override void OnNetworkDespawn() => _ativos.Clear();

        static DadosDoMundo Dados => MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;

        bool Pronto
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                return IsServer && IsSpawned && m != null && m.Pronto && !m.NoDiaZero && prefab != null;
            }
        }

        /// <summary>O estado do grupo da parte da cidade.</summary>
        public EstadoDaCidade Estado(int cidade, int parte = 0)
        {
            int k = Chave(cidade, parte);
            if (!_cidades.TryGetValue(k, out var e)) _cidades[k] = e = new EstadoDaCidade();
            return e;
        }

        /// <summary>Quantos grupos a cidade tem: 6 na grande (o <see cref="gruposNaCidadeGrande"/>), 1 na pequena e na vila.</summary>
        public int GruposNaCidade(int cidade) => CidadeGrande(cidade) ? Mathf.Clamp(gruposNaCidadeGrande, 1, 8) : 1;

        /// <summary>A parte da cidade onde o ponto está (a fatia em volta do meio; 0 numa cidade de um grupo só).</summary>
        public int ParteDaCidade(int cidade, Vector3 p)
        {
            int n = GruposNaCidade(cidade);
            var cidades = Dados?.Plano?.Cidades;
            if (n <= 1 || cidades == null || cidade < 0 || cidade >= cidades.Count) return 0;
            var c = cidades[cidade].Centro;
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
            var d = Dados;
            if (d?.Plano == null) return;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                var p = j.transform.position;
                var tracado = d.CidadeEm(p.x, p.z);
                if (tracado == null) continue;
                int cidade = IndiceDaCidade(tracado);
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

        int IndiceDaCidade(TracadoDaCidade t)
        {
            var cidades = Dados?.Plano?.Cidades;
            if (cidades == null) return -1;
            for (int i = 0; i < cidades.Count; i++) if (cidades[i].Tracado == t) return i;
            return -1;
        }

        bool CidadeGrande(int cidade)
        {
            var cidades = Dados?.Plano?.Cidades;
            return cidades != null && cidade >= 0 && cidade < cidades.Count && cidades[cidade].Tipo == TipoDeLugar.CidadeGrande;
        }

        // ------------------------------------------------------------------ nascer e sair

        /// <summary>
        /// No anfitrião: o grupo da parte da cidade nasce perto do jogador (a 40–52 m, fora da vista de todos; o F1, mais
        /// perto e sem conferir a vista). Devolve se nasceu.
        /// </summary>
        public bool Nascer(int cidade, int parte, EstadoDaCidade e, Vector3 perto, bool doF1)
        {
            if (!Pronto || (!doF1 && _ativos.Count >= Mathf.Max(1, gruposJuntos))) return false;
            if (!AcharLugar(perto, doF1, out var lugar)) return false;
            var s = new System.Random(unchecked(Dados.Semente * 7919 + cidade * 104729 + e.geracao * 31337 + parte * 7727 + 11));
            int quantos = 6 + s.Next(3);
            if (e.quantos == 0) e.quantos = quantos;
            quantos = e.quantos;
            var g = new GrupoDeSobreviventes { cidade = cidade, setor = parte, geracao = e.geracao, quantos = quantos };
            bool temSub = false;
            for (int i = 0; i < quantos; i++)
            {
                var (arma, branca) = SortearArma(s, i, ref temSub);
                byte roupa = (byte)(s.Next(100) < 70 ? 0 : s.Next(100) < 50 ? 1 : 3);
                int sementeDaRoupa = s.Next();
                if ((e.vivos & (1 << i)) == 0) continue;   // este morreu
                var p = lugar + Quaternion.Euler(0f, i * 47f, 0f) * Vector3.forward * (1.2f + (i % 3) * 0.9f);
                if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) p = hit.position;
                else p = lugar;
                var go = Instantiate(prefab, p, Quaternion.LookRotation(perto - p == Vector3.zero ? Vector3.forward : new Vector3(perto.x - p.x, 0f, perto.z - p.z)));
                var sob = go.GetComponent<Sobrevivente>();
                sob.Identidade.Value = new IdentidadeDoSobrevivente
                {
                    semente = sementeDaRoupa, arma = arma, roupa = roupa, grupo = (ushort)Mathf.Max(0, Chave(cidade, parte)),
                    papel = i == 0 ? PapelDoSobrevivente.Lider : PapelDoSobrevivente.Busca, indice = (byte)i, branca = branca,
                };
                sob.grupo = g;
                Encher(sob, arma, false, branca);
                go.GetComponent<NetworkObject>().Spawn(true);
                if (sob.agente != null && sob.agente.isOnNavMesh) sob.agente.Warp(p);
                g.membros.Add(sob);
            }
            if (g.membros.Count == 0) return false;
            _ativos.Add(g);
            Debug.Log($"[Sobreviventes] O grupo da cidade {cidade}, parte {parte} (geração {e.geracao}) apareceu: {g.membros.Count} de {quantos}, a {Vector3.Distance(lugar, perto):0} m ({_ativos.Count} no mundo).");
            return true;
        }

        // as armas dos civis (03/11/2026, a pedido: "armas brancas, tacos, facas, pistolas, raramente uma submetralhadora";
        // antes, rifles, a espingarda e até a sniper): o líder com a pistola (às vezes a submetralhadora); os outros, ~45%
        // uma branca (a faca, o bastão com pregos, o machado ou a lança), ~45% a pistola e ~10% a submetralhadora, no
        // máximo uma por grupo (os outros: 50% branca, 40% pistola e 10% a submetralhadora; com o líder, ~45% de brancas)
        static (TipoDeArma arma, Item branca) SortearArma(System.Random s, int i, ref bool temSub)
        {
            int r = s.Next(100);
            if (i == 0)
            {
                if (r < 25 && !temSub)
                {
                    temSub = true;
                    return (TipoDeArma.Submetralhadora, Item.Nada);
                }
                return (TipoDeArma.Pistola, Item.Nada);
            }
            if (r < 10 && !temSub)
            {
                temSub = true;
                return (TipoDeArma.Submetralhadora, Item.Nada);
            }
            if (r < 50) return (TipoDeArma.Pistola, Item.Nada);
            int b = s.Next(100);
            var branca = b < 30 ? Item.Faca : b < 60 ? Item.Bastao : b < 85 ? Item.Machado : Item.Lanca;
            return (TipoDeArma.Pistola, Itens.NoJogo(branca) ? branca : Item.Faca);
        }

        // o que ele leva: o pente cheio, de 2 a 4 caixas de munição da arma dele (de um a dois pentes cada), comida, água, às vezes remédio e materiais,
        // uma mochila (quase sempre) e algum dinheiro
        /// <summary>
        /// O que ele leva (03/11/2026, a pedido: "o loot dos NPC está muito alto"). Os civis (os grupos, o acampamento, o
        /// carro quebrado): o pente de 40% a 100% e, em 45% deles, uma caixa com 3 a 12 balas; de arma branca, nenhuma; mais
        /// comida e bebida (de 1 a 3 e de 1 a 2). Os militares (<paramref name="militar"/>; 03/11/2026, à noite, a pedido: "o
        /// loot dos NPC militares também está alto", 8 mortos deixavam 634 balas): o pente de 50% a 100% e, em 40% deles, uma
        /// caixa com 30% a 60% de um pente; para lutar, uma reserva de 2 pentes que não fica no saque
        /// (<see cref="Sobrevivente.reserva"/>); a barra, às vezes o feijão, a água e o remédio, e raramente o colete tático ou o
        /// capacete militar como item.
        /// </summary>
        internal static void Encher(Sobrevivente sob, TipoDeArma arma, bool militar = false, Item branca = Item.Nada)
        {
            var d = Armas.Dados(arma);
            sob.mochila.Clear();
            sob.reserva = 0;
            bool deBranca = Itens.EhBranca(branca);
            if (deBranca) sob.pente = 0;
            else if (militar)
            {
                sob.pente = Mathf.Max(1, Mathf.RoundToInt(d.capacidade * Random.Range(0.5f, 1f)));
                sob.reserva = d.capacidade * 2;
                if (Random.value < 0.4f) Por(sob, Itens.CaixaDe(d.calibre), Mathf.Max(1, Mathf.RoundToInt(d.capacidade * Random.Range(0.3f, 0.6f))));
            }
            else
            {
                sob.pente = Mathf.Max(1, Mathf.RoundToInt(d.capacidade * Random.Range(0.4f, 1f)));
                if (Random.value < 0.45f) Por(sob, Itens.CaixaDe(d.calibre), Random.Range(3, 13));
            }
            Item[] comidas = { Item.Comida, Item.Feijao, Item.Carne, Item.Milho, Item.Sopa, Item.Salgadinho, Item.BarraDeEnergia };
            Item[] bebidas = { Item.Agua, Item.RefrigeranteLata, Item.Energetico, Item.Suco };
            Item[] remedios = { Item.Atadura, Item.Comprimidos, Item.Gaze, Item.Bandagem, Item.KitMedico };
            Item[] materiais = { Item.Pano, Item.Corda, Item.FitaAdesiva, Item.Pregos, Item.Fios, Item.PecasDeMetal };
            if (militar)
            {
                // a ração: a barra; às vezes o feijão, a água e o remédio; raramente o colete ou o capacete (o que ele veste
                // fica com o corpo)
                Por(sob, Item.BarraDeEnergia, 1);
                if (Random.value < 0.3f) Por(sob, Item.Feijao, 1);
                if (Random.value < 0.6f) Por(sob, Item.Agua, 1);
                if (Random.value < 0.3f) Por(sob, Random.value < 0.25f ? Item.KitMedico : Item.Bandagem, 1);
                if (Random.value < 0.08f) PorRoupa(sob, "Colete_Tatico");
                if (Random.value < 0.06f) PorRoupa(sob, "Capacete_Militar");
                if (Random.value < 0.15f) Por(sob, Item.Pilhas, 2);
            }
            else
            {
                int nComidas = Random.Range(1, 4), nBebidas = Random.Range(1, 3);
                for (int i = 0; i < nComidas; i++) Por(sob, comidas[Random.Range(0, comidas.Length)], 1);
                for (int i = 0; i < nBebidas; i++) Por(sob, bebidas[Random.Range(0, bebidas.Length)], 1);
                if (Random.value < 0.35f) Por(sob, remedios[Random.Range(0, remedios.Length)], 1);
                if (Random.value < 0.4f) Por(sob, materiais[Random.Range(0, materiais.Length)], Random.Range(1, 3));
                if (Random.value < 0.2f) Por(sob, Item.Pilhas, 2);
                if (!deBranca && Random.value < 0.1f) Por(sob, Item.Faca, 1);
            }
            float r = Random.value;
            sob.mochilaVestida = r < 0.45f ? Item.MochilaPequena : r < 0.75f ? Item.MochilaMedia : r < 0.85f ? Item.MochilaGrande : Item.Nada;
            sob.dinheiro = Random.value < 0.6f ? Random.Range(5, 80) : 0;
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
        internal static void LargarAMochila(Sobrevivente s, List<ItemNaMochila> extras)
        {
            var itens = new List<ItemNaMochila>(s.mochila);
            // (a arma do militar cai gasta, de 50% a 85%: passando de 70%, ela engasga às vezes; 03/11/2026)
            bool militar = s.Identidade.Value.papel == PapelDoSobrevivente.Militar;
            itens.Insert(0, new ItemNaMochila { item = s.ItemDaArma, qtd = (ushort)(s.ComBranca ? 1 : Mathf.Max(0, s.pente)),
                                                desgaste = (byte)(militar ? Random.Range(50, 86) : Random.Range(15, 60)) });
            if (extras != null) itens.AddRange(extras);
            var lugar = s.transform.position + s.transform.right * 0.6f;
            MochilasNoChao.Instancia?.Criar(lugar, s.transform.eulerAngles.y, s.nome, s.mochilaVestida, itens, s.dinheiro);
        }

        /// <summary>No anfitrião: o tiro de um jogador; o grupo perto (até 70 m) fica sabendo de onde veio.</summary>
        public static void OuvirTiro(Vector3 ponto, JogadorRede autor)
        {
            var m = Instancia;
            if (m == null || !m.IsServer || autor == null) return;
            foreach (var g in m._ativos)
                foreach (var s in g.membros)
                {
                    if (s == null || s.Morto || (s.transform.position - ponto).sqrMagnitude > m.ouvidoDoTiro * m.ouvidoDoTiro) continue;
                    var errado = new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                    g.Ver(autor, autor.transform.position + errado, s);
                    break;   // (um por grupo avisa)
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
            var d = Dados;
            var p = j.transform.position;
            var tracado = d.CidadeEm(p.x, p.z);
            int cidade = tracado != null ? IndiceDaCidade(tracado) : -1;
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
            return $"Um grupo de {_ativos[_ativos.Count - 1].membros.Count} a uns 20 m" +
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
