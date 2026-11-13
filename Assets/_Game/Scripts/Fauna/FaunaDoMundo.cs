using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A fauna do mundo (12/11/2026, a pedido; o plano no Docs/19-Fauna.md). Como os zumbis, não há um NetworkObject por
    /// bicho: no anfitrião, este gerenciador faz nascer os grupos fora da vista (pela mancha do mapa e pela população da
    /// região: FaunaDoMundo.Povoamento.cs), faz cada bicho pensar (Bicho.Mente.cs) e andar, e manda para cada jogador só os
    /// bichos perto dele (FaunaDoMundo.Rede.cs); nos clientes, mantém as cópias. Nenhuma cena nem prefab muda: a
    /// <see cref="VigiaDaFauna"/> cria o gerenciador quando o mundo gerado fica pronto e a rede está ligada, e o tira
    /// quando a partida acaba. Os zumbis não atacam os bichos; os bichos fogem deles (FaunaDoMundo.Sentidos.cs).
    /// </summary>
    public sealed partial class FaunaDoMundo : MonoBehaviour
    {
        public const int MaxBichos = 1024;
        /// <summary>A chance de o alfa investir (por perigo perto do rebanho com filhote, uma vez a cada 45 s).</summary>
        public static float ChanceDaInvestida = 0.35f;
        /// <summary>O XP de matar um bicho grande (os pequenos dão 1) e o de carnear.</summary>
        public const int XpDaCaca = 6, XpDoBichoPequeno = 1, XpDoCarnear = 2;
        /// <summary>As horas do jogo até a carcaça sumir (apodrecer).</summary>
        public const float HorasDaCarcaca = 48f;

        public static FaunaDoMundo Instancia { get; private set; }
        /// <summary>O F1: os nomes e os estados em cima dos bichos.</summary>
        public static bool VerSentidos;

        readonly List<Bicho> _ativos = new List<Bicho>();
        readonly Bicho[] _porId = new Bicho[MaxBichos];
        readonly byte[] _geracoes = new byte[MaxBichos];
        readonly Stack<ushort> _livres = new Stack<ushort>();
        ushort _proximoId;
        internal readonly List<Grupo> Grupos = new List<Grupo>();
        bool _servidor;
        float _proximoPovoar;

        /// <summary>Os bichos (no anfitrião, os de verdade; nos clientes, as cópias).</summary>
        public IReadOnlyList<Bicho> Ativos => _ativos;
        public bool Servidor => _servidor;

        /// <summary>As horas do jogo desde o começo (o dia × 24 + a hora).</summary>
        public static double HorasDoJogo => CicloDoDia.Dia * 24.0 + CicloDoDia.Hora;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ZerarEstaticos()
        {
            Instancia = null;
            VerSentidos = false;
            NiveisGuardados = null;
            TocasGuardadas = null;
            RoidasGuardadas = null;
            CastorRapido = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PorAVigia()
        {
            if (Object.FindAnyObjectByType<VigiaDaFauna>() != null) return;
            var go = new GameObject("Fauna (vigia)");
            DontDestroyOnLoad(go);
            go.AddComponent<VigiaDaFauna>();
        }

        /// <summary>A fauna na cena do mundo (a do "Mundo" do sorteado, ou a do Lakewood).</summary>
        internal static void Criar(Scene cena)
        {
            var go = new GameObject("Fauna");
            if (cena.IsValid()) SceneManager.MoveGameObjectToScene(go, cena);
            go.AddComponent<FaunaDoMundo>();
        }

        void Awake()
        {
            Instancia = this;
            var nm = NetworkManager.Singleton;
            _servidor = nm != null && nm.IsServer;
            if (_servidor)
            {
                RestaurarNiveis();
                RestaurarOsCastores();
            }
            else
            {
                nm?.CustomMessagingManager?.RegisterNamedMessageHandler(NomeDaMensagem, AoReceber);
                nm?.CustomMessagingManager?.RegisterNamedMessageHandler(NomeDoSom, AoReceberSom);
                nm?.CustomMessagingManager?.RegisterNamedMessageHandler(NomeDasCasas, AoReceberCasas);
            }
            ZombieManager.AoBarulho += AoBarulho;
            _ultimaHora = HorasDoJogo;
            // (a etapa 4) os vaga-lumes, só na tela de cada máquina
            var vaga = new GameObject("Vaga-lumes");
            vaga.transform.SetParent(transform, false);
            vaga.AddComponent<VagaLumes>();
        }

        void OnDestroy()
        {
            ZombieManager.AoBarulho -= AoBarulho;
            var nm = NetworkManager.Singleton;
            if (!_servidor && nm != null)
            {
                nm.CustomMessagingManager?.UnregisterNamedMessageHandler(NomeDaMensagem);
                nm.CustomMessagingManager?.UnregisterNamedMessageHandler(NomeDoSom);
                nm.CustomMessagingManager?.UnregisterNamedMessageHandler(NomeDasCasas);
            }
            if (_servidor)
            {
                NiveisGuardados = NiveisParaSalvar();   // (sair e voltar ao mesmo mundo no mesmo Play)
                CastoresParaSalvar(out var tocas, out var roidas);
                TocasGuardadas = tocas;
                RoidasGuardadas = roidas;
            }
            if (Instancia == this) Instancia = null;
        }

        // ------------------------------------------------------------------ os jogadores (em todas as máquinas)

        internal struct Gente
        {
            public JogadorRede j;
            public Vector3 pes;
            public float vel;
            public bool agachado, lanterna, valido;
        }

        internal readonly List<Gente> Pessoas = new List<Gente>();

        void AtualizarGente()
        {
            Pessoas.Clear();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || !j.Criado) continue;
                Pessoas.Add(new Gente
                {
                    j = j, pes = j.transform.position, vel = j.Velocidade, agachado = j.Agachado.Value,
                    lanterna = j.Lanterna != null && j.Lanterna.Ligada.Value, valido = j.AlvoValido,
                });
            }
        }

        /// <summary>Algum jogador a menos de 'raio' m do ponto (os rastros de sangue e as manchas só perto de alguém).</summary>
        public static bool PertoDeAlguem(Vector3 p, float raio)
        {
            var f = Instancia;
            if (f == null) return false;
            float r2 = raio * raio;
            foreach (var g in f.Pessoas) if ((g.pes - p).sqrMagnitude < r2) return true;
            return false;
        }

        float MaisPerto(Vector3 p)
        {
            float m = float.MaxValue;
            foreach (var g in Pessoas) m = Mathf.Min(m, (g.pes - p).sqrMagnitude);
            return m < float.MaxValue ? Mathf.Sqrt(m) : float.MaxValue;
        }

        // ------------------------------------------------------------------ o quadro

        void Update()
        {
            float agora = Time.time, dt = Time.deltaTime;
            AtualizarGente();
            if (!_servidor)
            {
                AtualizarCopias(dt);
                return;
            }
            if (agora >= _proximoPovoar)
            {
                _proximoPovoar = agora + 2.5f;
                Recuperar();
                Povoar(agora);
                Recolher();
                Apodrecer();
                SumirDeDia();   // (a etapa 4) os bichos da noite somem de dia, longe da vista
                Urubus(agora);
            }
            for (int i = 0; i < _ativos.Count; i++)
            {
                var b = _ativos[i];
                float d = MaisPerto(b.transform.position);
                if (!b.Morto && !b.Exposto)
                {
                    if (agora >= b.ProximaDecisao)
                    {
                        b.Pensar(this, agora);
                        // (no bote e na investida, a decisão vem rápida: a mordida e a patada pegam no ponto certo)
                        b.ProximaDecisao = b.Estado == EstadoDoBicho.Investindo && d < 130f ? agora + 0.08f
                            : agora + (d < 45f ? 0.3f : d < 130f ? 1f : 3f) * Random.Range(0.8f, 1.2f);
                    }
                    if (d < 170f || b.Passagem) b.Andar(dt, agora);   // (o V dos gansos nasce longe e vem)
                }
                if (d < 140f) b.Animar(dt);
            }
            ArrumarGrupos();
            Enviar();
            AtualizarCasas();
            Ceu(agora);
            CheiroEVeneno(dt);
        }

        // ------------------------------------------------------------------ nascer e sumir (no anfitrião)

        bool PegarId(out ushort id)
        {
            if (_livres.Count > 0)
            {
                id = _livres.Pop();
                return true;
            }
            id = _proximoId;
            if (_proximoId >= MaxBichos) return false;
            _proximoId++;
            return true;
        }

        internal Bicho Nascer(Especie e, Variante v, Vector3 p, float giro, Grupo grupo)
        {
            if (!_servidor || !PegarId(out ushort id)) return null;
            byte geracao = ++_geracoes[id];
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            p.y = Bicho.Chao(p, out _);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, giro, 0f));
            var b = go.AddComponent<Bicho>();
            b.Montar(id, geracao, e, v, false, id * 7919 + geracao * 31);
            _porId[id] = b;
            _ativos.Add(b);
            if (grupo != null)
            {
                b.Grupo = grupo;
                grupo.membros.Add(b);
            }
            b.ProximaDecisao = Time.time + Random.Range(0.1f, 1f);
            return b;
        }

        /// <summary>Some com o bicho (o salmão que o urso comeu).</summary>
        internal void Sumir(Bicho b) => Remover(b);

        /// <summary>O salmão vivo mais perto do ponto, a até 'raio' m (o urso pescando).</summary>
        internal Bicho SalmaoPerto(Vector3 p, float raio)
        {
            Bicho melhor = null;
            float m = raio * raio;
            foreach (var b in _ativos)
            {
                if (b.Morto || b.Especie != Especie.Salmao || b.Exposto) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < m) { m = d; melhor = b; }
            }
            return melhor;
        }

        internal void Remover(Bicho b)
        {
            if (b == null) return;
            _ativos.Remove(b);
            if (_porId[b.Id] == b) _porId[b.Id] = null;
            _livres.Push(b.Id);
            if (b.Grupo != null) b.Grupo.membros.Remove(b);
            Destroy(b.gameObject);
        }

        void ArrumarGrupos()
        {
            for (int i = Grupos.Count - 1; i >= 0; i--)
            {
                var g = Grupos[i];
                g.membros.RemoveAll(b => b == null);
                bool vivo = false;
                foreach (var b in g.membros) vivo |= !b.Morto;
                if (!vivo)
                {
                    foreach (var b in g.membros) if (b != null) b.Grupo = null;
                    Grupos.RemoveAt(i);
                    continue;
                }
                if (g.lider == null || g.lider.Morto)
                {
                    // o novo líder: o alfa, senão uma fêmea adulta, senão qualquer um vivo
                    g.lider = null;
                    foreach (var b in g.membros) if (!b.Morto && b.Alfa) g.lider = b;
                    if (g.lider == null) foreach (var b in g.membros) if (!b.Morto && b.Variante == Variante.Femea) { g.lider = b; break; }
                    if (g.lider == null) foreach (var b in g.membros) if (!b.Morto) { g.lider = b; break; }
                }
            }
        }

        // os grupos que ficaram longe de todos somem (as carcaças ficam até apodrecer, ou até ficarem muito longe)
        void Recolher()
        {
            for (int i = Grupos.Count - 1; i >= 0; i--)
            {
                var g = Grupos[i];
                bool longe = true;
                foreach (var b in g.membros)
                    if (b != null && !b.Morto && MaisPerto(b.transform.position) < DistanciaDeRecolher) longe = false;
                if (!longe) continue;
                for (int k = g.membros.Count - 1; k >= 0; k--)
                    if (g.membros[k] != null && !g.membros[k].Morto) Remover(g.membros[k]);
            }
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var b = _ativos[i];
                if (b.Grupo == null && !b.Morto && MaisPerto(b.transform.position) > DistanciaDeRecolher) Remover(b);
                else if (b.Morto && MaisPerto(b.transform.position) > DistanciaDeRecolher * 1.6f) Remover(b);
            }
        }

        void Apodrecer()
        {
            double agora = HorasDoJogo;
            for (int i = _ativos.Count - 1; i >= 0; i--)
            {
                var b = _ativos[i];
                if (b.Morto && agora - b.MorreuNaHora > HorasDaCarcaca) Remover(b);
            }
        }

        // ------------------------------------------------------------------ a caça (no anfitrião)

        /// <summary>
        /// Um tiro ou um golpe de um jogador num bicho (o acerto vem do dono pelo JogadorRede.Fauna.cs). A conferência é
        /// tolerante (é co-op): o bicho tem que existir (mesmo Id e geração), estar vivo e perto do ponto.
        /// </summary>
        public void Acertar(ushort id, byte geracao, float dano, Vector3 ponto, bool cabeca, bool tiro, JogadorRede autor)
        {
            if (!_servidor || id >= MaxBichos) return;
            var b = _porId[id];
            if (b == null || b.Geracao != geracao || b.Morto) return;
            float folga = 2.5f + b.Dados.comprimento * b.Escala;
            if ((b.transform.position + Vector3.up * b.Altura * 0.6f - ponto).sqrMagnitude > folga * folga) return;
            dano = Mathf.Clamp(dano, 0f, 1000f) * (cabeca ? 2.5f : 1f);
            b.Vida -= dano;
            if (b.Vida <= 0f) Matar(b, autor);
            else b.Ferir(dano, autor != null ? autor.transform.position : ponto, Time.time, autor);
        }

        /// <summary>A morte (o tiro, o golpe, o sangramento, o predador). 'baixarNivel': a caça de gente baixa a população da região.</summary>
        internal void Matar(Bicho b, JogadorRede autor, bool baixarNivel = true)
        {
            if (b.Morto) return;
            // o alfa (ou o líder) da matilha: ela se desfaz
            var g = b.Grupo;
            if (g != null && b.Dados.papel == Papel.Matilha && (b.Alfa || g.lider == b))
                AlfaMorreu(g, Time.time, autor != null ? autor.transform.position : b.transform.position);
            b.Estado = EstadoDoBicho.Morto;
            b.Vida = 0f;
            b.Ferido = false;
            b.Velocidade = 0f;
            b.MorreuNaHora = HorasDoJogo;
            b.AoMorrer();
            if (baixarNivel) BaixarNivel(b.transform.position, b);
            if (autor != null && autor.Inventario != null)
                autor.Inventario.GanharXp(b.Dados.pontos >= 60f ? XpDaCaca : XpDoBichoPequeno, "caça");
        }

        /// <summary>
        /// O ataque de um bicho num jogador: a cabeçada do alfa (no tronco, com o empurrão), a mordida da matilha (nas pernas
        /// e nos braços: o corte e o sangramento do sistema dos membros) e a patada do urso (forte, com o empurrão que derruba;
        /// o pardo às vezes quebra o osso).
        /// </summary>
        internal void Golpear(Bicho b, JogadorRede alvo)
        {
            if (alvo == null || alvo.Vida == null || !alvo.AlvoValido) return;
            float dano = b.Dados.mordida > 0f ? b.Dados.mordida : b.Especie == Especie.Alce ? 35f : 18f;
            dano *= b.Variante == Variante.Filhote ? 0.4f : b.Variante == Variante.Femea ? 0.85f : 1f;
            string causa = (b.Dados.papel == Papel.Matilha ? "Mordido por " : "Atacado por ") + (b.Dados.comArtigo.StartsWith("a ") ? "uma " : "um ") + b.Dados.nome + ".";
            Vector3 de = b.transform.position + Vector3.up * b.Altura;
            Vector3 empurra = alvo.transform.position - b.transform.position;
            empurra.y = 0f;
            empurra = empurra.sqrMagnitude > 0.01f ? empurra.normalized : b.transform.forward;
            if (b.Especie == Especie.Cascavel)
            {
                // a picada: no pé ou na canela (a perna), e o veneno (uma em quatro é seca)
                var perna = Random.value < 0.5f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD;
                alvo.Vida.Dano(dano, b.transform.position + Vector3.up * 0.2f, "Picado por uma cascavel.", TipoDeGolpe.Mordida, perna);
                alvo.Vida.Picada(perna);
                alvo.Vida.AbalarAgora(5f, MotivoDaMente.Susto);
                b.Tocar(SomDoBicho.Mordida);
                return;
            }
            if (b.Dados.corpo == Corpo.Ave)
            {
                // a bicada e as asadas do ganso do ninho: pouco dano, nas pernas, e um empurrão
                var perna = Random.value < 0.5f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD;
                alvo.Vida.Dano(dano, b.transform.position + Vector3.up * 0.4f, "Bicado por um ganso.", TipoDeGolpe.Pancada, perna);
                alvo.EmpurraoDoBicho(empurra * 2.5f + Vector3.up * 1f, 0.2f);
                b.Tocar(SomDoBicho.Ganso);
                return;
            }
            switch (b.Dados.papel)
            {
                case Papel.Matilha:
                {
                    // a mordida: as pernas mais que os braços (o lado é sorteado)
                    bool perna = Random.value < 0.65f, esquerda = Random.value < 0.5f;
                    var parte = perna ? (esquerda ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD) : (esquerda ? ParteDoCorpo.BracoE : ParteDoCorpo.BracoD);
                    alvo.Vida.Dano(dano, b.transform.position + Vector3.up * 0.5f, causa, TipoDeGolpe.Mordida, parte);
                    alvo.EmpurraoDoBicho(empurra * 1.5f, 0.25f);
                    b.Tocar(SomDoBicho.Mordida);
                    break;
                }
                case Papel.Urso:
                {
                    bool garras = Random.value < 0.6f;
                    var lesao = Random.value < (b.Especie == Especie.UrsoPardo ? 0.12f : 0.05f) ? Lesao.Fratura : Lesao.Nenhuma;
                    alvo.Vida.Dano(dano, de, causa, garras ? TipoDeGolpe.Corte : TipoDeGolpe.Mordida, PartesDoCorpo.Sortear, lesao);
                    alvo.EmpurraoDoBicho(empurra * (b.Especie == Especie.UrsoPardo ? 8f : 6f) + Vector3.up * 2.5f, 0.9f);
                    b.Tocar(garras ? SomDoBicho.Patada : SomDoBicho.Mordida);
                    break;
                }
                default:
                    // a cabeçada (o alfa do veado, o alce): no tronco, empurrando
                    alvo.Vida.Dano(dano * b.Escala, de, causa, TipoDeGolpe.Pancada, ParteDoCorpo.Tronco);
                    alvo.EmpurraoDoBicho(empurra * (b.Especie == Especie.Alce ? 7f : 4.5f) + Vector3.up * 1.5f, 0.6f);
                    break;
            }
        }

        /// <summary>Carnear a carcaça (o dono segurou E com a faca na mão): a carne crua, o couro e a gordura.</summary>
        public void Carnear(ushort id, byte geracao, JogadorRede quem)
        {
            if (!_servidor || quem == null || id >= MaxBichos) return;
            var b = _porId[id];
            if (b == null || b.Geracao != geracao || !b.Morto || b.Carneado) return;
            if (quem.NaMao != Item.Faca || Vector3.Distance(quem.transform.position, b.transform.position) > 3f + b.Dados.comprimento * b.Escala) return;
            Especies.Carcaca(b.Especie, b.Variante, out int carne, out int couro, out int gordura);
            // a matilha comeu: sobra menos carne e gordura (o couro rasgado, abaixo da metade)
            if (b.Restante < 0.999f)
            {
                carne = Mathf.FloorToInt(carne * b.Restante + Random.value * 0.5f);
                gordura = Mathf.FloorToInt(gordura * b.Restante);
                if (b.Restante < 0.5f) couro = 0;
            }
            var inv = quem.Inventario;
            int sobrou = 0;
            sobrou += Dar(inv, quem, Item.CarneCrua, carne);
            sobrou += Dar(inv, quem, Item.Couro, couro);
            sobrou += Dar(inv, quem, Item.Gordura, gordura);
            b.Carneado = true;
            b.MorreuNaHora = HorasDoJogo - HorasDaCarcaca + 0.25f;   // some logo (os ossos ficam pouco)
            inv?.GanharXp(XpDoCarnear, "caça");
            var partes = new List<string>();
            if (carne > 0) partes.Add(carne + (carne == 1 ? " carne crua" : " carnes cruas"));
            if (couro > 0) partes.Add(couro + (couro == 1 ? " couro" : " couros"));
            if (gordura > 0) partes.Add(gordura + (gordura == 1 ? " gordura" : " gorduras"));
            string texto = "Carneou " + b.Dados.comArtigo + ": " + (partes.Count > 0 ? string.Join(", ", partes) : "nada que preste") + "."
                           + (sobrou > 0 ? " (sem lugar: ficou aos seus pés)" : "");
            inv?.AvisarDaFauna(texto);
        }

        static int Dar(InventarioJogador inv, JogadorRede quem, Item item, int qtd)
        {
            if (qtd <= 0) return 0;
            int sobra = inv != null ? inv.ReceberEmPilha(item, qtd) : qtd;
            if (sobra > 0) ItensNoMundo.Instancia?.Criar(item, quem.transform.position + quem.transform.forward * 0.6f, quem.transform.eulerAngles.y + 30f, sobra);
            return sobra;
        }

        // ------------------------------------------------------------------ o F1 (no anfitrião)

        /// <summary>Põe um grupo da espécie a ~18 m na frente do jogador (sem olhar a mancha do mapa), distraído por uns 10 s.</summary>
        public string PorPerto(JogadorRede j, Especie e)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Vector3 frente = j.transform.forward;
            frente.y = 0f;
            Vector3 p = j.transform.position + (frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward) * 18f;
            // (a etapa 4) o bicho de terra não nasce dentro do lago: gira a direção até achar chão seco
            if (Especies.De(e).meio == Meio.Terra && FunduraEm(p) > 0.2f)
            {
                Vector3 f0 = p - j.transform.position;
                for (int k = 1; k <= 12; k++)
                {
                    Vector3 q = j.transform.position + Quaternion.Euler(0f, (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 30f, 0f) * f0;
                    if (FunduraEm(q) <= 0.05f) { p = q; break; }
                }
            }
            // (a etapa 3) os da água vão para o lago, o mais perto possível
            if (LugarNaAgua(e, j.transform.position, 8f, 80f, out var naAgua, out bool achou))
            {
                if (!achou) LugarNaAgua(e, j.transform.position, 0f, 400f, out naAgua, out achou);
                if (!achou) return "O lago está longe: use \"Ir para o lago\" antes.";
                p = naAgua;
            }
            int n = NascerGrupo(e, p, true);
            var d = Especies.De(e);
            return n > 0 ? $"{n} {(n == 1 ? d.nome : d.plural)} a uns 18 m na frente (distraídos por uns 10 s)." : "Não deu para pôr (sem lugar livre).";
        }

        /// <summary>
        /// A fila do F1: um de cada espécie (e os três cervos), lado a lado a ~9 m na frente do jogador, de lado e parados
        /// (não pensam nem andam), para ver os modelos. Tirar todos limpa.
        /// </summary>
        public string FilaDeTodos(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Vector3 frente = j.transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            var lista = new List<(Especie e, Variante v)> { (Especie.Veado, Variante.Macho), (Especie.Veado, Variante.Femea), (Especie.Veado, Variante.Filhote) };
            for (int i = 0; i < Especies.Quantas; i++)
                if ((Especie)i != Especie.Veado) lista.Add(((Especie)i, (Especie)i == Especie.Alce ? Variante.Macho : Variante.Adulto));
            float total = 0f;
            foreach (var (e, v) in lista) total += Mathf.Max(0.4f, Especies.De(e).comprimento * Especies.Escala(e, v)) + 0.6f;
            float x = -total * 0.5f, giro = Quaternion.LookRotation(lado).eulerAngles.y;
            int n = 0;
            foreach (var (e, v) in lista)
            {
                float largura = Mathf.Max(0.4f, Especies.De(e).comprimento * Especies.Escala(e, v)) + 0.6f;
                var b = Nascer(e, v, j.transform.position + frente * 9f + lado * (x + largura * 0.5f), giro, null);
                x += largura;
                if (b == null) continue;
                b.Exposto = true;
                b.Estado = EstadoDoBicho.Andando;
                n++;
            }
            return $"{n} bichos parados em fila a uns 9 m na frente ({total:0} m de ponta a ponta).";
        }

        public string MatarOMaisPerto(JogadorRede j, bool so_ferir)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            Bicho melhor = null;
            float m = float.MaxValue;
            foreach (var b in _ativos)
            {
                if (b.Morto) continue;
                float d = (b.transform.position - j.transform.position).sqrMagnitude;
                if (d < m) { m = d; melhor = b; }
            }
            if (melhor == null) return "Nenhum bicho vivo por perto.";
            if (so_ferir)
            {
                float dano = Especies.Pontos(melhor.Especie, melhor.Variante) * 0.45f;
                melhor.Vida -= dano;
                melhor.Ferir(dano, j.transform.position, Time.time, j);
                return $"Feriu {melhor.Dados.comArtigo} (a {Mathf.Sqrt(m):0} m): foge sangrando.";
            }
            Matar(melhor, j);
            return $"Matou {melhor.Dados.comArtigo} (a {Mathf.Sqrt(m):0} m).";
        }

        public void TirarTodos()
        {
            for (int i = _ativos.Count - 1; i >= 0; i--) Remover(_ativos[i]);
            Grupos.Clear();
        }

        /// <summary>O resumo para o F1: quantos de cada espécie, a população da região e o mais perto.</summary>
        public string Resumo(Vector3 perto)
        {
            var contagem = new int[Especies.Quantas];
            int mortos = 0;
            foreach (var b in _ativos)
            {
                if (b.Morto) mortos++;
                else contagem[(int)b.Especie]++;
            }
            var partes = new List<string>();
            for (int i = 0; i < contagem.Length; i++)
                if (contagem[i] > 0) partes.Add($"{contagem[i]} {(contagem[i] == 1 ? Especies.De((Especie)i).nome : Especies.De((Especie)i).plural)}");
            string texto = (partes.Count > 0 ? string.Join(", ", partes) : "nenhum bicho vivo") + (mortos > 0 ? $"; {mortos} carcaça(s)" : "") + ".";
            if (_servidor) texto += $"\nA população desta região: {Nivel(perto) * 100f:0}% (a caça baixa; volta em ~1 dia do jogo).";
            Bicho mais = null;
            float m = float.MaxValue;
            foreach (var b in _ativos)
            {
                float d = (b.transform.position - perto).sqrMagnitude;
                if (d < m) { m = d; mais = b; }
            }
            if (mais != null)
                texto += $"\nO mais perto: {mais.Dados.NomeDaVariante(mais.Variante)}{(mais.Alfa ? " (alfa)" : "")} a {Mathf.Sqrt(m):0} m, {NomeDoEstado(mais)}"
                         + (_servidor ? $", vida {Mathf.Max(0f, mais.Vida):0}" : "") + (mais.Ferido ? ", sangrando" : "") + ".";
            return texto;
        }

        static string NomeDoEstado(Bicho b)
        {
            if (b.Dados.papel != Papel.Presa && !b.Morto)
            {
                var g = b.Grupo;
                if (g != null && g.alvo != null) return b.Estado == EstadoDoBicho.Investindo ? "dando o bote" : "cercando";
                if (g != null && g.presa != null) return "caçando";
                if (g != null && g.carcaca != null) return "comendo";
                if (b.Estado == EstadoDoBicho.Pastando) return "farejando";
                if (b.Dados.papel == Papel.Urso && b.Estado == EstadoDoBicho.Alerta) return "de pé, avisando";
                if (b.Dados.papel == Papel.Urso && b.Estado == EstadoDoBicho.Investindo) return "atacando";
            }
            return NomeDoEstado(b.Estado);
        }

        static string NomeDoEstado(EstadoDoBicho e) => e switch
        {
            EstadoDoBicho.Pastando => "pastando", EstadoDoBicho.Andando => "andando", EstadoDoBicho.Alerta => "em alerta",
            EstadoDoBicho.Fugindo => "fugindo", EstadoDoBicho.Investindo => "investindo", EstadoDoBicho.Morto => "morto",
            _ => "deitado",
        };

        // os nomes e os estados em cima dos bichos (o F1)
        void OnGUI()
        {
            if (!VerSentidos) return;
            var cam = Camera.main;
            if (cam == null) return;
            var estilo = GUI.skin.label;
            foreach (var b in _ativos)
            {
                if (b == null) continue;
                Vector3 p = b.transform.position + Vector3.up * (b.Altura * 1.6f + 0.3f);
                if ((p - cam.transform.position).sqrMagnitude > 70f * 70f) continue;
                Vector3 s = cam.WorldToScreenPoint(p);
                if (s.z < 0f) continue;
                string t = b.Dados.NomeDaVariante(b.Variante) + (b.Alfa ? " (alfa)" : "") + "\n" + NomeDoEstado(b) + (b.Ferido ? ", sangrando" : "")
                           + (_servidor && !b.Morto ? $"\nvida {b.Vida:0}" : "");
                GUI.Label(new Rect(s.x - 80f, Screen.height - s.y - 30f, 160f, 60f), t, estilo);
            }
        }
    }

    /// <summary>
    /// Liga e desliga a fauna com a partida: o mundo pronto e a rede ligada. No mundo sorteado, o "Mundo" gerado e pronto
    /// (fora do Dia 0, como sempre: o Mundo.Pronto só fica verdadeiro depois dele); no Lakewood (12/11/2026, a parte 2 do
    /// "O jogo do mundo sorteado no Lakewood"), o <see cref="MundoDeLakewood"/> na rede, com a semente da partida.
    /// </summary>
    public sealed class VigiaDaFauna : MonoBehaviour
    {
        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool rede = nm != null && nm.IsListening;
            var mundo = MundoDaPartida.Instancia;
            var lakewood = MundoDeLakewood.Instancia;
            bool noSorteado = mundo != null && mundo.Gerado && mundo.Pronto;
            bool noLakewood = mundo == null && lakewood != null && lakewood.Pronto;
            bool deve = rede && (noSorteado || noLakewood);
            if (deve && FaunaDoMundo.Instancia == null) FaunaDoMundo.Criar(noSorteado ? mundo.gameObject.scene : lakewood.gameObject.scene);
            else if (!deve && FaunaDoMundo.Instancia != null) Destroy(FaunaDoMundo.Instancia.gameObject);
        }
    }
}
