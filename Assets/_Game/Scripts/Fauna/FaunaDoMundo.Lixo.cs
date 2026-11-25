using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O lixo da cidade revirado (23/11/2026, a pedido: "também daria para lootear os lixos (como se a gente fosse revirar os
    /// lixos também, com alguns drops de restos de comida, bebidas, sucatas, pregos e parafusos"), no anfitrião:
    /// <list type="bullet">
    /// <item>segurar E na lata, nas sacolas ou na caçamba (LixoNaMira) pede ao anfitrião (JogadorRede.PedirRevirarLixo), que
    /// confere a distância e sorteia: a comida largada (o salgadinho, o chocolate, as latas), as bebidas, a sucata e a munição
    /// de prego e de parafuso (a das armas de sucata), às vezes um pano, pilhas, fios. A caçamba das lojas tem mais sucata;
    /// a lata que o guaxinim já revirou tem menos comida (ele comeu). O que não cabe na mochila cai aos pés;</item>
    /// <item>cada lixo se revira uma vez (os revirados vão a todos pela mensagem "ZCC.LixoRevirado" e ficam no save). Revirar
    /// a lata e as sacolas derruba e espalha como o guaxinim (por um dia do jogo), e faz um pouco de barulho;</item>
    /// <item>o susto da caçamba (Bicho.Cacamba.cs): quem chega a menos de <see cref="SustoPerto"/> m dela com guaxinim dentro,
    /// ou tenta revirá-la, faz todos saírem num pulo, com a tampa batendo e as latas. Quem está perto leva o susto na mente,
    /// e a câmera sacode e o coração dispara na máquina dele.</item>
    /// </list>
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        /// <summary>A que distância do meio da caçamba (m) os guaxinins de dentro saem no susto.</summary>
        public const float SustoPerto = 2.6f;
        const string NomeDosRevirados = "ZCC.LixoRevirado";

        readonly HashSet<int> _revirados = new HashSet<int>();
        readonly HashSet<ulong> _reviradosEnviados = new HashSet<ulong>();
        float _proximoEnvioDosRevirados;

        /// <summary>
        /// Os revirados do save (as chaves: <see cref="ChaveDoLixo"/>), antes de a fauna nascer (e ao sair do mundo, para voltar
        /// no mesmo Play).
        /// </summary>
        public static List<int> LixosGuardados;
        // as chaves do save ainda sem o índice (o lixo da cidade sai do chão do Lakewood, que pode vir depois da fauna)
        List<int> _chavesParaAchar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ZerarOsLixos() => LixosGuardados = null;

        /// <summary>Este lixo (o índice em <see cref="LixoDaCidade.Pontos"/>) já foi revirado por alguém (em qualquer máquina).</summary>
        public static bool Revirado(int indice) => Instancia != null && Instancia._revirados.Contains(indice);

        // ------------------------------------------------------------------ o save

        /// <summary>
        /// A chave de um lixo no save: o lugar dele no mapa do Lakewood (de meio em meio metro), e não a ordem na lista (que muda
        /// quando a cidade ganha lotes).
        /// </summary>
        public static int ChaveDoLixo(LixoDaCidade.Ponto p)
        {
            Vector3 m = OrigemDeLakewood.NoMapa(p.pos);
            int x = Mathf.Clamp(Mathf.RoundToInt(m.x * 2f), 0, 65535), z = Mathf.Clamp(Mathf.RoundToInt(m.z * 2f), 0, 65535);
            return unchecked((x << 16) | z);
        }

        /// <summary>Os lixos revirados, para o save do mundo (as chaves).</summary>
        public static List<int> LixosParaSalvar()
        {
            var f = Instancia;
            if (f == null || !f._servidor) return LixosGuardados != null ? new List<int>(LixosGuardados) : new List<int>();
            var lista = new List<int>();
            var pontos = LixoDaCidade.Pontos;
            foreach (int i in f._revirados) if (i >= 0 && i < pontos.Count) lista.Add(ChaveDoLixo(pontos[i]));
            if (f._chavesParaAchar != null) lista.AddRange(f._chavesParaAchar);
            return lista;
        }

        /// <summary>O save carregou: os revirados voltam (agora, ou quando a fauna nascer).</summary>
        public static void RestaurarLixos(List<int> revirados)
        {
            LixosGuardados = revirados != null ? new List<int>(revirados) : null;
            if (Instancia != null && Instancia._servidor) Instancia.RestaurarOsLixos();
        }

        void RestaurarOsLixos()
        {
            _revirados.Clear();
            _chavesParaAchar = LixosGuardados != null && LixosGuardados.Count > 0 ? new List<int>(LixosGuardados) : null;
            AcharAsChaves();
            _reviradosEnviados.Clear();   // (todos recebem a lista nova)
        }

        // as chaves do save viram os índices, assim que o lixo da cidade existe
        void AcharAsChaves()
        {
            if (_chavesParaAchar == null) return;
            var pontos = LixoDaCidade.Pontos;
            if (pontos.Count == 0) return;
            var chaves = new HashSet<int>(_chavesParaAchar);
            for (int i = 0; i < pontos.Count; i++) if (chaves.Contains(ChaveDoLixo(pontos[i]))) _revirados.Add(i);
            _chavesParaAchar = null;
            _reviradosEnviados.Clear();
        }

        // no Awake (todas as máquinas): a mensagem dos revirados nos clientes; os do save no anfitrião
        void LigarOLixo(NetworkManager nm)
        {
            if (_servidor) RestaurarOsLixos();
            else nm?.CustomMessagingManager?.RegisterNamedMessageHandler(NomeDosRevirados, AoReceberRevirados);
        }

        // no OnDestroy
        void DesligarOLixo(NetworkManager nm)
        {
            if (!_servidor) nm?.CustomMessagingManager?.UnregisterNamedMessageHandler(NomeDosRevirados);
            else LixosGuardados = LixosParaSalvar();
        }

        // ------------------------------------------------------------------ a rede (os revirados)

        void MandarRevirados(ulong cliente, ICollection<int> quais)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || quais.Count == 0) return;
            int tamanho = sizeof(ushort) + quais.Count * sizeof(ushort);
            using var w = new FastBufferWriter(tamanho, Allocator.Temp, tamanho + 64);
            w.WriteValueSafe((ushort)Mathf.Min(quais.Count, ushort.MaxValue));
            int n = 0;
            foreach (int i in quais)
            {
                if (n++ >= ushort.MaxValue) break;
                w.WriteValueSafe((ushort)i);
            }
            nm.CustomMessagingManager.SendNamedMessage(NomeDosRevirados, cliente, w, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void AoReceberRevirados(ulong remetente, FastBufferReader r)
        {
            r.ReadValueSafe(out ushort n);
            for (int k = 0; k < n; k++)
            {
                r.ReadValueSafe(out ushort i);
                _revirados.Add(i);
            }
        }

        void MarcarRevirado(int indice)
        {
            if (!_revirados.Add(indice)) return;
            var um = new[] { indice };
            foreach (var g in Pessoas)
            {
                if (g.j == null) continue;
                ulong cliente = g.j.OwnerClientId;
                if (cliente == NetworkManager.ServerClientId || !_reviradosEnviados.Contains(cliente)) continue;
                MandarRevirados(cliente, um);
            }
        }

        // ------------------------------------------------------------------ a cada quadro (no anfitrião)

        void AtualizarLixos(float agora)
        {
            AcharAsChaves();
            // o susto: alguém chegou perto da caçamba com guaxinim dentro (um por quadro)
            var pontos = LixoDaCidade.Pontos;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var b = _ativos[i];
                if (b.Morto || b.NaCacamba != Bicho.FaseDaCacamba.Dentro || b.Cacamba < 0 || b.Cacamba >= pontos.Count) continue;
                Vector3 meio = pontos[b.Cacamba].pos;
                if (PessoaMaisPerto(meio, out var quem) < SustoPerto)
                {
                    SustoNaCacamba(b.Cacamba, quem);
                    break;
                }
            }
            // quem entrou na partida recebe a lista inteira
            if (agora < _proximoEnvioDosRevirados) return;
            _proximoEnvioDosRevirados = agora + 1f;
            foreach (var g in Pessoas)
            {
                if (g.j == null) continue;
                ulong cliente = g.j.OwnerClientId;
                if (cliente == NetworkManager.ServerClientId || _reviradosEnviados.Contains(cliente)) continue;
                _reviradosEnviados.Add(cliente);
                MandarRevirados(cliente, _revirados);
            }
        }

        float PessoaMaisPerto(Vector3 p, out Vector3 pes)
        {
            float m = float.MaxValue;
            pes = default;
            foreach (var g in Pessoas)
            {
                if (!g.valido) continue;
                float d = Vector3.Distance(g.pes, p);
                if (d < m) { m = d; pes = g.pes; }
            }
            return m;
        }

        // ------------------------------------------------------------------ os guaxinins na caçamba

        /// <summary>Quantos guaxinins estão com a caçamba (indo, pulando, dentro ou saindo).</summary>
        internal int QuantosNaCacamba(int indice)
        {
            if (indice < 0) return 0;
            int n = 0;
            foreach (var b in _ativos) if (!b.Morto && b.Cacamba == indice && b.NaCacamba != Bicho.FaseDaCacamba.Fora) n++;
            return n;
        }

        /// <summary>
        /// Só no anfitrião: os guaxinins de dentro da caçamba saem num pulo, para o lado de quem chegou ('de'), com a tampa
        /// batendo e as latas; quem está a até 7 m leva o susto. Falso se não tinha nenhum dentro.
        /// </summary>
        internal bool SustoNaCacamba(int indice, Vector3 de)
        {
            if (!_servidor) return false;
            var pontos = LixoDaCidade.Pontos;
            if (indice < 0 || indice >= pontos.Count) return false;
            float agora = Time.time;
            int k = 0;
            foreach (var b in _ativos)
            {
                if (b.Morto || b.Especie != Especie.Guaxinim || b.Cacamba != indice) continue;
                if (b.SairDaCacamba(this, agora, de, k * 0.22f)) k++;
            }
            if (k == 0) return false;
            Vector3 p = pontos[indice].pos + Vector3.up * 1.1f;
            TocarSomNoPonto(p, SomDoBicho.SustoNaCacamba);
            TocarSomNoPonto(p, SomDoBicho.LataCaindo);
            BarulhoNoLixo(p, TipoDeLixo.Cacamba);
            foreach (var g in Pessoas)
            {
                if (g.j == null || !g.valido) continue;
                float d = Vector3.Distance(g.pes, p);
                if (d > 7f) continue;
                float forca = Mathf.Clamp01(1f - (d - 1.5f) / 5.5f);
                if (g.j.Vida != null) g.j.Vida.AbalarAgora(3f + 5f * forca, MotivoDaMente.Susto);
                g.j.SustoDoBicho(forca);
            }
            return true;
        }

        // ------------------------------------------------------------------ revirar (no anfitrião)

        struct DoLixo
        {
            public Item item;
            public int peso, min, max;
            public bool comida;
        }

        static DoLixo L(Item item, int peso, int min = 1, int max = 1) =>
            new DoLixo { item = item, peso = peso, min = min, max = max, comida = item >= Item.Comida && item <= Item.GarrafaTermica };

        // a lata da casa: a comida largada e as bebidas, a sucata e os pregos de casa
        static readonly DoLixo[] DaLata =
        {
            L(Item.Salgadinho, 10), L(Item.Chocolate, 4), L(Item.Comida, 5), L(Item.Feijao, 3), L(Item.Milho, 2), L(Item.Sopa, 2),
            L(Item.RefrigeranteLata, 8), L(Item.RefrigeranteGarrafa, 4), L(Item.Agua, 4), L(Item.Suco, 3), L(Item.Leite, 1),
            L(Item.Sucata, 9, 1, 2), L(Item.MunicaoPrego, 4, 2, 6), L(Item.MunicaoParafuso, 2, 1, 4),
            L(Item.Pano, 4), L(Item.Pilhas, 2, 1, 2), L(Item.FitaAdesiva, 1), L(Item.Fios, 2), L(Item.MacoDeCigarros, 2, 1, 6),
        };

        // as sacolas: mais comida e bebida, menos sucata
        static readonly DoLixo[] DasSacolas =
        {
            L(Item.Salgadinho, 12), L(Item.Chocolate, 5), L(Item.Comida, 5), L(Item.Feijao, 3), L(Item.Milho, 3), L(Item.Sopa, 2),
            L(Item.RefrigeranteLata, 9), L(Item.RefrigeranteGarrafa, 5), L(Item.Agua, 4), L(Item.Suco, 4), L(Item.Energetico, 2),
            L(Item.Sucata, 5, 1, 2), L(Item.MunicaoPrego, 3, 2, 5), L(Item.MunicaoParafuso, 1, 1, 3),
            L(Item.Pano, 5), L(Item.Pilhas, 2, 1, 2), L(Item.MacoDeCigarros, 2, 1, 6),
        };

        // a caçamba das lojas: o que as lojas jogam fora (mais sucata, peças, pregos e parafusos)
        static readonly DoLixo[] DaCacamba =
        {
            L(Item.Sucata, 14, 1, 3), L(Item.MunicaoPrego, 6, 3, 8), L(Item.MunicaoParafuso, 4, 2, 5), L(Item.PecasDeMetal, 4, 1, 2),
            L(Item.Fios, 4, 1, 2), L(Item.Pano, 3, 1, 2), L(Item.Madeira, 2, 1, 2), L(Item.FitaAdesiva, 2),
            L(Item.Salgadinho, 6), L(Item.Comida, 4), L(Item.RefrigeranteLata, 5), L(Item.RefrigeranteGarrafa, 3), L(Item.Agua, 3),
            L(Item.Energetico, 2),
        };

        static DoLixo Sortear(DoLixo[] tabela, bool comido)
        {
            int total = 0;
            foreach (var e in tabela) total += e.peso;
            for (int tentativa = 0; tentativa < 6; tentativa++)
            {
                int r = Random.Range(0, total);
                foreach (var e in tabela)
                {
                    r -= e.peso;
                    if (r >= 0) continue;
                    // o guaxinim já comeu quase tudo
                    if (comido && e.comida && Random.value < 0.7f) break;
                    return e;
                }
            }
            return tabela[0];
        }

        /// <summary>
        /// Só no anfitrião: o jogador revira o lixo (o índice em <see cref="LixoDaCidade.Pontos"/>). Com guaxinim na caçamba, só
        /// o susto (o lixo continua lá para depois).
        /// </summary>
        internal void Revirar(JogadorRede j, int indice)
        {
            if (!_servidor || j == null) return;
            var pontos = LixoDaCidade.Pontos;
            if (indice < 0 || indice >= pontos.Count) return;
            var pt = pontos[indice];
            Vector3 pes = j.transform.position;
            // perto dele (ou do lixo que o guaxinim espalhou do lado da caçamba)
            float d = Mathf.Min(Vector3.Distance(Plano3(pes), Plano3(pt.pos)), Vector3.Distance(Plano3(pes), Plano3(LixoDaCidade.OndeEspalha(pt))));
            if (d > (pt.tipo == TipoDeLixo.Cacamba ? 3.6f : 3.2f) || Mathf.Abs(pes.y - pt.pos.y) > 3f) return;
            if (pt.tipo == TipoDeLixo.Cacamba && SustoNaCacamba(indice, pes)) return;
            var inv = j.Inventario;
            if (inv == null) return;
            if (_revirados.Contains(indice))
            {
                // (24/11/2026) o que sobrou dentro dele (o recipiente do lixo) abre de novo
                if (MochilasNoChao.Instancia != null && MochilasNoChao.Instancia.AcharDoLixo(ChaveDoLixo(pt), out var dentro))
                {
                    j.AbrirSaque(dentro.id);
                    return;
                }
                inv.AvisarDono("Esse lixo já foi revirado: não sobrou nada que preste.");
                return;
            }
            MarcarRevirado(indice);
            // o guaxinim passou antes: a comida acabou
            bool comido = Revirada(pt.tipo == TipoDeLixo.Cacamba ? LixoDaCidade.OndeEspalha(pt) : pt.pos);
            // a lata tomba e as sacolas rasgam (o lixo fica espalhado, como o do guaxinim)
            if (pt.tipo != TipoDeLixo.Cacamba) ReviraLixeira(pt.pos, (byte)pt.tipo);
            TocarSomNoPonto(pt.pos + Vector3.up * 0.5f, pt.tipo == TipoDeLixo.Sacolas ? SomDoBicho.Sacola : SomDoBicho.Remexer);
            ZombieManager.Cheiro(pt.pos, pt.tipo == TipoDeLixo.Cacamba ? 12f : 8f);
            var tabela = pt.tipo == TipoDeLixo.Cacamba ? DaCacamba : pt.tipo == TipoDeLixo.Sacolas ? DasSacolas : DaLata;
            var (tipoDoRecipiente, grade, nome, menos, mais) = RecipienteDoLixo(pt);
            float nada = pt.tipo == TipoDeLixo.Cacamba ? 0.15f : pt.tipo == TipoDeLixo.Sacolas ? 0.3f : 0.25f;
            int vezes = Random.value < nada ? 0 : Random.Range(menos, mais + 1);
            // (24/11/2026, a pedido: "uma tela de inventário da lixeira para ver os itens que há dentro") o que se acha fica
            // dentro do lixo (um recipiente do tamanho dele) e a tela abre: pega-se o que quiser
            var itens = new List<ItemNaMochila>();
            for (int k = 0; k < vezes; k++)
            {
                var e = Sortear(tabela, comido);
                int n = Itens.Quantos(e.item, Random.Range(e.min, e.max + 1));
                itens.Add(new ItemNaMochila { item = e.item, qtd = (ushort)(Itens.EmPilha(e.item) ? n : Itens.QuantidadePadrao(e.item)) });
            }
            inv.GanharXp(1, "saque");
            ushort id = itens.Count > 0 && MochilasNoChao.Instancia != null
                ? MochilasNoChao.Instancia.CriarRecipiente(tipoDoRecipiente, ChaveDoLixo(pt), pt.pos + Vector3.up * 0.5f, j.transform.eulerAngles.y, nome, grade, Item.Nada, itens)
                : (ushort)0;
            if (id != 0) j.AbrirSaque(id);
            else inv.AvisarDono("Revirou o lixo: nada que preste.");
        }

        /// <summary>
        /// (24/11/2026, a pedido: "cada lixeira deve ter um tamanho diferente dependendo do seu tamanho dentro do jogo") O
        /// recipiente de cada lixo: o tipo, a grade, o nome e quantas coisas ele costuma ter (de 'menos' a 'mais').
        /// </summary>
        static (TipoDeRecipiente tipo, Vector2Int grade, string nome, int menos, int mais) RecipienteDoLixo(LixoDaCidade.Ponto pt)
        {
            switch (pt.tipo)
            {
                case TipoDeLixo.Cacamba:
                    return (TipoDeRecipiente.Cacamba, new Vector2Int(8, 5), "a caçamba", 2, 4);
                case TipoDeLixo.Sacolas:
                {
                    int q = pt.quantas;
                    var grade = q <= 2 ? new Vector2Int(3, 2) : q <= 4 ? new Vector2Int(4, 3) : new Vector2Int(5, 3);
                    return (TipoDeRecipiente.Sacolas, grade, q <= 1 ? "a sacola de lixo" : "as sacolas de lixo", 1, q <= 2 ? 2 : 3);
                }
                default:
                    return ModelosDoLixo.QualLata(pt.semente).carrinho
                        ? (TipoDeRecipiente.Carrinho, new Vector2Int(4, 4), "a lixeira de rodas", 1, 3)
                        : (TipoDeRecipiente.Lata, new Vector2Int(3, 3), "a lata de lixo", 1, 2);
            }
        }

        // ------------------------------------------------------------------ em cada máquina (a lata balançando)

        /// <summary>
        /// Os guaxinins parados (ou quase) desta máquina (no anfitrião, os de verdade; nos outros, as cópias): a lata balança
        /// com um mexendo do lado, a caçamba sacode com um dentro. Devolve quantos.
        /// </summary>
        public static int GuaxininsParados(List<Vector3> saida)
        {
            saida.Clear();
            var f = Instancia;
            if (f == null) return 0;
            foreach (var b in f._ativos)
                if (b != null && !b.Morto && b.Especie == Especie.Guaxinim && b.Velocidade <= 0.4f) saida.Add(b.transform.position);
            return saida.Count;
        }

        // ------------------------------------------------------------------ a lata tombando (em cada máquina)

        /// <summary>
        /// A queda da lata (23/11/2026, a pedido: "revirando as latas menores (elas caindo após)"): a lata do lixo espalhado
        /// começa de pé, onde ela estava, e tomba em volta da beirada do fundo, mais rápido no fim (como caindo), com um
        /// tranco ao bater no chão. Aí aparecem os restos e as sacolas e soam as latas; a tampa da lata de metal voa para o lado.
        /// </summary>
        internal sealed class QuedaDaLata : MonoBehaviour
        {
            public Transform lata, tampa;
            public Vector3 pivo, eixo, posDePe, tampaDe, tampaPara;
            public Quaternion giroDePe, tampaGiroDe, tampaGiroPara;
            public GameObject[] aparecem;
            public float desde;
            const float Queda = 0.55f, Tranco = 0.22f;
            bool _bateu;

            internal void Update()
            {
                float t = Time.time - desde;
                float u = Mathf.Clamp01(t / Queda);
                float angulo = t <= Queda ? 90f * u * u : 90f - 7f * Mathf.Sin(Mathf.Clamp01((t - Queda) / Tranco) * Mathf.PI);
                var q = Quaternion.AngleAxis(angulo, eixo);
                if (lata != null) lata.SetPositionAndRotation(pivo + q * (posDePe - pivo), q * giroDePe);
                if (tampa != null)
                {
                    float v = Mathf.Clamp01(t / (Queda + 0.15f));
                    tampa.SetPositionAndRotation(Vector3.Lerp(tampaDe, tampaPara, v) + Vector3.up * (Mathf.Sin(v * Mathf.PI) * 0.45f),
                                                 Quaternion.Slerp(tampaGiroDe, tampaGiroPara, v));
                }
                if (!_bateu && t >= Queda)
                {
                    _bateu = true;
                    if (aparecem != null) foreach (var a in aparecem) if (a != null) a.SetActive(true);
                    SomDosBichos.Tocar(SomDoBicho.LataCaindo, pivo + Vector3.up * 0.3f);
                }
                if (t >= Queda + Tranco)
                {
                    var fim = Quaternion.AngleAxis(90f, eixo);
                    if (lata != null) lata.SetPositionAndRotation(pivo + fim * (posDePe - pivo), fim * giroDePe);
                    if (tampa != null) tampa.SetPositionAndRotation(tampaPara, tampaGiroPara);
                    Destroy(this);
                }
            }
        }

        // ------------------------------------------------------------------ o F1

        /// <summary>O F1 (em cada máquina): a caçamba mais perto (a até 400 m): o jogador a uns 6 m dela, de frente para ela.</summary>
        public static bool LugarNaCacamba(JogadorRede j, out Vector3 onde, out float giro)
        {
            onde = default;
            giro = 0f;
            if (j == null) return false;
            Vector3 eu = j.transform.position;
            float m = 400f * 400f;
            bool achou = false;
            foreach (var l in LixoDaCidade.Pontos)
            {
                if (l.tipo != TipoDeLixo.Cacamba) continue;
                float d = (l.pos - eu).sqrMagnitude;
                if (d >= m) continue;
                m = d;
                Vector3 frente = Quaternion.Euler(0f, l.giro, 0f) * Vector3.back;
                onde = l.pos + frente * 6f;
                onde.y = AlturaDoMapa(onde.x, onde.z, l.pos.y);
                giro = Quaternion.LookRotation(-frente).eulerAngles.y;
                achou = true;
            }
            return achou;
        }

        /// <summary>
        /// O F1 (no anfitrião): 2 guaxinins já dentro da caçamba mais perto (de 4 a 60 m: a de colado no jogador faria eles saírem
        /// na hora), esperando alguém chegar.
        /// </summary>
        public string GuaxinimNaCacamba(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            var pontos = LixoDaCidade.Pontos;
            int melhor = -1;
            float m = 60f * 60f, minimo = (SustoPerto + 1.4f) * (SustoPerto + 1.4f);
            for (int i = 0; i < pontos.Count; i++)
            {
                if (pontos[i].tipo != TipoDeLixo.Cacamba) continue;
                float d = (pontos[i].pos - j.transform.position).sqrMagnitude;
                if (d < m && d > minimo) { m = d; melhor = i; }
            }
            if (melhor < 0) return "Sem caçamba a até 60 m (use \"Ir para a caçamba\").";
            var pt = pontos[melhor];
            int n = 0;
            for (int k = 0; k < 2 && QuantosNaCacamba(melhor) < Bicho.CabemNaCacamba; k++)
            {
                var b = Nascer(Especie.Guaxinim, Variante.Adulto, pt.pos, pt.giro, null);
                if (b == null) continue;
                b.PorDentroDaCacamba(melhor, pt.pos, LixoDaCidade.GiroDePe(pt), Time.time);
                n++;
            }
            return n > 0 ? $"{n} guaxinim(ns) dentro da caçamba a {Mathf.Sqrt(m):0} m: chegue a {SustoPerto:0.0} m dela ou segure E nela."
                         : "Não deu para pôr o guaxinim.";
        }
    }
}
