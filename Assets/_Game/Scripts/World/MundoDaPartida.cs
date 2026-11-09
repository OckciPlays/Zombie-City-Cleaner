using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O mundo gerado da partida (Plano_Mapa, Partes M1 e M2), no objeto "Mundo" da cena Mundo (com o NetworkObject):
    ///  - o anfitrião escolhe a semente, o tamanho e o nome (o <see cref="Pedido"/> do menu; semente 0 = sorteada) e põe
    ///    nas NetworkVariables, junto com a versão do gerador. Quem entra recebe tudo isso, e só isso;
    ///  - cada máquina gera o mesmo mundo em outra thread (<see cref="GeradorDoMundo"/>) e liga o terreno
    ///    (<see cref="TerrenoDoMundo"/>), esperando a colisão em volta da partida;
    ///  - o jogador só nasce quando o mundo está pronto na máquina dele: a conexão é aprovada sem o personagem; o
    ///    anfitrião cria o dele quando fica pronto, e cada cliente avisa (<see cref="ProntoRpc"/>, com o resumo do mundo).
    ///    O anfitrião compara o resumo com o dele, avisa se não bater e cria o personagem;
    ///  - com a versão do gerador diferente da do anfitrião, o cliente sai da partida com a mensagem;
    ///  - o anfitrião põe o carro na garagem (ou na vaga) da casa da família (Parte M6; sem ela, ao lado da laje da partida);
    ///  - a rede de segurança: se o jogador local passar pelo chão (chegou num lugar antes da colisão), ele volta para a
    ///    superfície assim que a colisão dali ficar pronta.
    /// A tela de carregamento (<see cref="UI.TelaDeCarregamento"/>) mostra as etapas.
    /// </summary>
    public partial class MundoDaPartida : NetworkBehaviour
    {
        /// <summary>O pedido do menu (só o anfitrião usa): o tamanho, a semente (0 = sortear) e o nome.</summary>
        public static PedidoDoMundo Pedido = PedidoDoMundo.Padrao;
        public static MundoDaPartida Instancia { get; private set; }

        public TerrenoDoMundo terreno;
        [Tooltip("O chão até esta distância da partida precisa estar com colisão antes de o jogador nascer (m).")]
        public float raioPronto = 40f;
        [Tooltip("O carro que se dirige (Prefabs/Veiculos/Carro), posto pelo anfitrião ao lado da partida.")]
        public GameObject carro;
        [Tooltip("Um segundo carro que se dirige (Prefabs/Veiculos/Cupe), posto pelo anfitrião ao lado da laje da partida. Vazio: só o carro da família.")]
        public GameObject segundoCarro;
        [Tooltip("As motos (06/11/2026, a pedido: Prefabs/Veiculos/Moto_FatBoy e Moto_Cross), postas pelo anfitrião perto da " +
                 "partida (no bunker, na vaga livre depois da do cupê), com as chaves no chão da sala. Vazio: sem motos.")]
        public GameObject[] motos;
        [Tooltip("A chave de cada moto, na mesma ordem.")]
        public Loot.Item[] chavesDasMotos = { Loot.Item.ChaveDaFatBoy, Loot.Item.ChaveDaCross };

        readonly NetworkVariable<int> _semente = new NetworkVariable<int>();
        readonly NetworkVariable<byte> _tamanho = new NetworkVariable<byte>();
        readonly NetworkVariable<int> _versao = new NetworkVariable<int>();
        readonly NetworkVariable<FixedString64Bytes> _nome = new NetworkVariable<FixedString64Bytes>();
        // o Dia 0 (01/11/2026): enquanto ele dura, o mundo espera (a geração começa quando a porta da casa fecha)
        readonly NetworkVariable<bool> _diaZero = new NetworkVariable<bool>();
        // o fogo amigo (03/11/2026): escolhido ao criar o mundo, igual para todos
        readonly NetworkVariable<bool> _fogoAmigo = new NetworkVariable<bool>();

        /// <summary>O fogo amigo está ligado neste mundo: o tiro e o golpe de um jogador ferem os outros (Player/ArmaJogador).</summary>
        public bool FogoAmigo => IsSpawned && _fogoAmigo.Value;

        /// <summary>A partida está no Dia 0 (a rua normal, antes do apocalipse): o mundo ainda não foi gerado.</summary>
        public bool NoDiaZero => IsSpawned && _diaZero.Value;
        /// <summary>A partida desta máquina começou no Dia 0 (para a tela dos 5 anos depois).</summary>
        public bool VeioDoDiaZero { get; private set; }

        public DadosDoMundo Dados { get; private set; }
        public bool Gerado => Dados != null;
        /// <summary>O mundo está pronto nesta máquina (os dados e o chão em volta da partida).</summary>
        public bool Pronto { get; private set; }
        public int Semente => _semente.Value;
        public TamanhoDoMundo Tamanho => (TamanhoDoMundo)_tamanho.Value;
        public string Nome => _nome.Value.ToString();
        /// <summary>De 0 a 1 e o texto, para a tela de carregamento.</summary>
        public float Progresso { get; private set; }
        public string Etapa { get; private set; } = "Recebendo o mundo do anfitrião";
        /// <summary>A geração falhou (a mensagem para a tela).</summary>
        public string Erro { get; private set; }
        /// <summary>O mundo desta máquina saiu diferente do anfitrião (a mensagem para a tela).</summary>
        public string Aviso { get; private set; }
        /// <summary>Quanto a geração e o carregamento levaram nesta máquina (s).</summary>
        public float SegundosDaGeracao { get; private set; }
        public float SegundosAtePronto { get; private set; }

        /// <summary>O mundo ficou pronto nesta máquina.</summary>
        public event Action<DadosDoMundo> FicouPronto;

        Task<DadosDoMundo> _geracao;
        int _vez;                                 // cada partida gera de novo; o que sobrar da anterior é ignorado
        volatile float _progressoDaThread;
        volatile string _etapaDaThread = "Preparando";
        float _comecou;
        bool _esperandoOChao;
        float _esperandoOsZumbisDesde = -1f;
        readonly List<(ulong cliente, ulong resumo)> _esperando = new List<(ulong, ulong)>();
        NetworkManager _rede;

        void Awake()
        {
            Instancia = this;
            IniciarAsMarcas();   // (a lista da rede antes do spawn: MundoDaPartida.Marcas.cs)
            IniciarAsArvores();   // (as árvores cortadas, 06/11/2026: MundoDaPartida.Arvores.cs)
            IniciarOsPaineis();   // (os painéis de metal desmontados, 09/11/2026: MundoDaPartida.Sucata.cs)
            // sem o personagem na conexão: ele nasce quando o mundo estiver pronto na máquina do jogador
            _rede = FindAnyObjectByType<NetworkManager>();
            if (_rede != null) _rede.ConnectionApprovalCallback = Aprovar;
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (_rede != null && _rede.ConnectionApprovalCallback == Aprovar) _rede.ConnectionApprovalCallback = null;
            base.OnDestroy();
        }

        static void Aprovar(NetworkManager.ConnectionApprovalRequest pedido, NetworkManager.ConnectionApprovalResponse resposta)
        {
            resposta.Approved = true;
            resposta.CreatePlayerObject = false;
        }

        public override void OnNetworkSpawn()
        {
            Reiniciar();
            if (IsServer)
            {
                _marcas.Clear();   // as marcas do mapa da partida anterior (as do save voltam no Salvamento)
                _arvoresCortadas.Clear();   // (e as árvores cortadas)
                var p = Pedido;
                _semente.Value = p.semente > 0 ? p.semente : GeradorDoMundo.NovaSemente();
                _tamanho.Value = (byte)p.tamanho;
                _versao.Value = GeradorDoMundo.Versao;
                string nome = string.IsNullOrWhiteSpace(p.nome) ? "Mundo novo" : p.nome.Trim();
                _nome.Value = new FixedString64Bytes(nome.Length > 30 ? nome.Substring(0, 30) : nome);
                _diaZero.Value = p.diaZero;
                _fogoAmigo.Value = p.fogoAmigo;
                NetworkManager.OnClientDisconnectCallback += AoSair;
            }
            VeioDoDiaZero = _diaZero.Value;
            FlorestaDoMundo.DefinirCortadas(Copia(_arvoresCortadas));   // (quem entra recebe as cortadas)
            PaineisNoSpawn();   // (e os painéis desmontados: MundoDaPartida.Sucata.cs)
            _diaZero.OnValueChanged += AoMudarODiaZero;
            if (_diaZero.Value) Etapa = "No Dia 0";
            else if (_semente.Value != 0) Comecar();
            else _semente.OnValueChanged += AoChegarASemente;
        }

        void AoMudarODiaZero(bool antes, bool depois)
        {
            if (!depois && _geracao == null && Dados == null && _semente.Value != 0) Comecar();
        }

        /// <summary>No anfitrião: o Dia 0 acabou (a porta da casa fechou, ou o F1 pulou): todos geram o mundo.</summary>
        public void AcabarODiaZero()
        {
            if (IsServer && IsSpawned) _diaZero.Value = false;
        }

        public override void OnNetworkDespawn()
        {
            _diaZero.OnValueChanged -= AoMudarODiaZero;
            _semente.OnValueChanged -= AoChegarASemente;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= AoSair;
            Reiniciar();
        }

        void AoChegarASemente(int antes, int depois)
        {
            _semente.OnValueChanged -= AoChegarASemente;
            if (depois != 0 && _geracao == null && Dados == null && !_diaZero.Value) Comecar();
        }

        // a partida acabou (ou outra começou): sem a cena recarregar, o mundo da anterior sai
        void Reiniciar()
        {
            _vez++;
            _geracao = null;
            Dados = null;
            Pronto = false;
            _esperandoOChao = false;
            _esperandoOsZumbisDesde = -1f;
            Progresso = 0f;
            Etapa = "Recebendo o mundo do anfitrião";
            Erro = Aviso = null;
            _esperando.Clear();
            if (terreno != null) terreno.Parar();
        }

        void Comecar()
        {
            if (!IsServer && _versao.Value != GeradorDoMundo.Versao)
            {
                Erro = $"O seu jogo gera outro mundo (gerador versão {GeradorDoMundo.Versao}; o do anfitrião é a {_versao.Value}). Atualize o jogo.";
                Debug.LogError("[Mundo] " + Erro);
                SessaoOnline.Instancia?.Sair(Erro);
                return;
            }
            int semente = _semente.Value, vez = _vez;
            var tamanho = Tamanho;
            _comecou = Time.realtimeSinceStartup;
            Etapa = "Gerando o mundo";
            _geracao = Task.Run(() => GeradorDoMundo.Gerar(semente, tamanho, (f, etapa) =>
            {
                if (vez != _vez) return;
                _progressoDaThread = f;
                _etapaDaThread = etapa;
            }));
            Debug.Log($"[Mundo] Gerando \"{Nome}\": semente {semente}, {Tamanhos.NomeComLado(tamanho)}, gerador versão {GeradorDoMundo.Versao}.");
            // enquanto o mundo é gerado, o que pararia o jogo na primeira vez que fosse usado: os sons do carro (em outra
            // thread) e as roupas que viram itens (do Resources). Os zumbis do pool, no ZombieManager
            Vehicles.SomDoCarro.Preparar();
            Roupas.ItensDasRoupas.Carregar();
        }

        void Update()
        {
            if (_geracao != null)
            {
                Progresso = _progressoDaThread * 0.8f;
                Etapa = _etapaDaThread;
                if (!_geracao.IsCompleted) return;
                var tarefa = _geracao;
                _geracao = null;
                if (tarefa.IsFaulted)
                {
                    Erro = "Não deu para gerar o mundo.";
                    Debug.LogException(tarefa.Exception);
                    return;
                }
                AoGerar(tarefa.Result);
            }
            if (_esperandoOChao)
            {
                terreno.Pedir(this, Dados.Partida, raioPronto + 24f, true);
                Progresso = 0.8f + 0.2f * Mathf.Clamp01(terreno.BlocosComColisao / 9f);
                if (!terreno.Pronto(Dados.Partida, raioPronto)) return;
                // a partida no bunker da família (o Plano_Bunker.md, a parte 5): a casca, o morro e a garagem com a colisão
                if (Dados.TemBunker && (BunkerDoMundo.Instancia == null || !BunkerDoMundo.Instancia.Pronto)) return;
                // os cortes dos zumbis (03/11/2026): eles levavam segundos e caíam no começo do jogo, com engasgos de até
                // 600 ms; a tela de carregamento espera (até 90 s, por segurança)
                var zumbis = Zombies.ZombieManager.Instancia;
                if (zumbis != null && zumbis.IsSpawned && !zumbis.CortesProntos)
                {
                    if (_esperandoOsZumbisDesde < 0f) _esperandoOsZumbisDesde = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - _esperandoOsZumbisDesde < 90f)
                    {
                        Etapa = $"Preparando os zumbis ({zumbis.ProgressoDosCortes * 100f:0}%)";
                        return;
                    }
                    Debug.LogWarning("[Mundo] Os cortes dos zumbis passaram de 90 s: o jogo começa sem esperar o resto.");
                }
                _esperandoOsZumbisDesde = -1f;
                _esperandoOChao = false;
                FicarPronto();
            }
            // o jogador local nasceu: ele mesmo puxa o chão em volta, e o pedido da partida sai
            if (Pronto && JogadorRede.Local != null)
            {
                terreno.Esquecer(this);
                ManterNoChao(JogadorRede.Local);
            }
        }

        void AoGerar(DadosDoMundo dados)
        {
            Dados = dados;
            SegundosDaGeracao = Time.realtimeSinceStartup - _comecou;
            Debug.Log($"[Mundo] Semente {dados.Semente} gerada em {SegundosDaGeracao * 1000f:F0} ms: {dados.Lado / 1000f:0.#} km, " +
                      $"{dados.BlocosPorLado} × {dados.BlocosPorLado} blocos, lago em ({dados.Lago.Centro.x:F0}, {dados.Lago.Centro.y:F0}), " +
                      $"partida em ({dados.Partida.x:F1}, {dados.Partida.y:F1}, {dados.Partida.z:F1}). Resumo {dados.Resumo:X16}.");
            PontosDeNascimento.Definir(dados.Nascimentos, dados.GirosDosNascimentos);
            terreno.Comecar(dados);
            terreno.Pedir(this, dados.Partida, raioPronto + 24f, true);
            Etapa = "Montando o chão em volta";
            _esperandoOChao = true;
        }

        void FicarPronto()
        {
            // o lixo da geração (e do mundo anterior) sai agora, ainda na tela de carregamento: sem isso, a coleta inteira
            // vinha sozinha nos primeiros segundos de jogo, às vezes, e parava tudo por até meio segundo (31/10/2026)
            float antesDaColeta = Time.realtimeSinceStartup;
            System.GC.Collect();
            float coleta = Time.realtimeSinceStartup - antesDaColeta;
            Pronto = true;
            Progresso = 1f;
            Etapa = IsServer ? "Pronto" : "Esperando o anfitrião";
            SegundosAtePronto = Time.realtimeSinceStartup - _comecou;
            Debug.Log($"[Mundo] Pronto para jogar em {SegundosAtePronto * 1000f:F0} ms ({terreno.Blocos} blocos de terreno, {terreno.BlocosComColisao} com colisão; " +
                      $"a coleta do lixo, {coleta * 1000f:F0} ms).");
            if (IsServer)
            {
                CriarJogador(NetworkManager.LocalClientId);
                foreach (var (cliente, resumo) in _esperando) Aceitar(cliente, resumo);
                _esperando.Clear();
                // o mundo do save tem os carros dele (e a chave está onde ficou): o Salvamento põe (02/11/2026)
                if (!Salvar.Salvamento.Carregando) CriarCarro();
            }
            else ProntoRpc(Dados.Resumo);
            FicouPronto?.Invoke(Dados);
        }

        // o carro na garagem (ou na vaga) da casa da família, de frente para a rua (Parte M6); sem a casa, ao lado da laje
        // da partida, virado para o mesmo lado (no anfitrião; a rede manda para os outros)
        void CriarCarro()
        {
            if (carro == null) return;
            var giro = Quaternion.Euler(0f, Dados.GiroDaPartida, 0f);
            var ponto = Dados.Partida + giro * new Vector3(DadosDoMundo.LadoDaPartida * 0.5f + 3.2f, 0f, 0f);
            ponto.y = Dados.Altura(ponto.x, ponto.z) + 0.4f;
            if (Dados.LugarDoCarro.HasValue)
            {
                giro = Quaternion.Euler(0f, Dados.GiroDoCarro, 0f);
                ponto = Dados.LugarDoCarro.Value + Vector3.up * 0.45f;
            }
            var novo = Instantiate(carro, ponto, giro);
            novo.name = "Carro";
            novo.GetComponent<NetworkObject>().Spawn(true);

            // a chave da perua no chão da sala, entre os dois primeiros que nascem (a pedido em 29/10/2026: com a chave, a
            // partida é direta; sem ela, a ligação direta). Os nascimentos ficam 5 cm acima do piso
            var chave = Vector3.Lerp(Dados.Nascimentos[0], Dados.Nascimentos[1], 0.5f) + Vector3.down * 0.05f;
            Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.ChaveDaPerua, chave, Dados.GiroDaPartida + 35f);
            CriarSegundoCarro(novo);
            if (PorAChaveDoCupe() && Salvar.Salvamento.Atual != null) Salvar.Salvamento.Atual.chaveDoCupe = true;
            if (PorAsMotos() && Salvar.Salvamento.Atual != null) Salvar.Salvamento.Atual.motos = true;
            if (PorOCaiaque() && Salvar.Salvamento.Atual != null) Salvar.Salvamento.Atual.caiaque = true;
        }

        // ------------------------------------------------------------------------------------------ o caiaque (06/11/2026)

        /// <summary>
        /// No anfitrião: o caiaque na água, na beira do lago do lado do bunker (a direção do centro do lago à partida), de
        /// lado para a margem. O prefab é o Resources/Caiaque (a montagem: Zombie City Cleaner > Veículos > Montar o
        /// caiaque). No mundo novo, com os carros; num save de antes dele, uma vez ao carregar. Devolve se ele foi posto.
        /// </summary>
        public bool PorOCaiaque()
        {
            if (Dados == null || Dados.Lago == null) return false;
            var lago = Dados.Lago;
            Vector2 dir = new Vector2(Dados.Partida.x, Dados.Partida.z) - lago.Centro;
            dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector2.right;
            float raio = lago.RaioNaDirecao(dir.x, dir.y);
            for (float t = 0.98f; t > 0.5f; t -= 0.02f)
            {
                var c = lago.Centro + dir * (raio * t);
                if (Dados.FunduraDaAgua(c.x, c.y) < 0.8f) continue;
                var giro = Quaternion.LookRotation(new Vector3(-dir.y, 0f, dir.x));
                return CriarCaiaque(new Vector3(c.x, lago.Nivel, c.y), giro);
            }
            return false;
        }

        /// <summary>No anfitrião: um caiaque no ponto (o do save, ou o da beira do lago).</summary>
        public bool CriarCaiaque(Vector3 ponto, Quaternion giro)
        {
            var prefab = Resources.Load<GameObject>("Caiaque");
            if (prefab == null)
            {
                Debug.LogWarning("[Caiaque] Sem o prefab Resources/Caiaque: rode Zombie City Cleaner > Veículos > Montar o caiaque.");
                return false;
            }
            var novo = Instantiate(prefab, ponto, giro);
            novo.name = "Caiaque";
            novo.GetComponent<NetworkObject>().Spawn(true);
            return true;
        }

        // ------------------------------------------------------------------------------------------ as motos (06/11/2026)

        /// <summary>
        /// No anfitrião: as motos perto da partida e as chaves delas no chão da sala, ao lado das dos carros (para o lado do
        /// primeiro nascimento). No mundo novo, junto com os carros; num save de antes delas, uma vez ao carregar
        /// (<see cref="Salvar.MundoSalvo.motos"/>). Devolve se elas foram postas.
        /// </summary>
        public bool PorAsMotos()
        {
            var itens = Loot.ItensNoMundo.Instancia;
            if (motos == null || motos.Length == 0 || Dados == null) return false;
            Physics.SyncTransforms();
            for (int i = 0; i < motos.Length; i++)
            {
                if (motos[i] == null || !LugarDaMoto(i, true, out var ponto, out var giro)) continue;
                var nova = Instantiate(motos[i], ponto, giro);
                nova.name = motos[i].name;
                nova.GetComponent<NetworkObject>().Spawn(true);
                Physics.SyncTransforms();   // a próxima procura vê esta
                if (itens == null || Dados.Nascimentos.Length < 2 || chavesDasMotos == null || i >= chavesDasMotos.Length) continue;
                var meio = Vector3.Lerp(Dados.Nascimentos[0], Dados.Nascimentos[1], 0.5f);
                var lado = Dados.Nascimentos[0] - Dados.Nascimentos[1];
                lado.y = 0f;
                var chave = meio + (lado.sqrMagnitude > 0.01f ? lado.normalized * (0.3f + 0.3f * i) : Vector3.zero) + Vector3.down * 0.05f;
                itens.Criar(chavesDasMotos[i], chave, Dados.GiroDaPartida - 20f - 25f * i);
            }
            return true;
        }

        /// <summary>
        /// O lugar da moto i: no bunker da família, a primeira das 4 vagas da garagem, a de antes da da perua (as duas
        /// lado a lado, 1,7 m uma da outra; a vaga depois da do cupê tem o macaco-jacaré e os pneus da oficina, e a cross
        /// nascia em cima do macaco); sem o bunker, ao lado da laje da partida. Com <paramref name="procurar"/>, o primeiro
        /// lugar sem nada no caminho (no bunker, deslizando dentro da vaga) e longe dos outros veículos; sem, o da lista,
        /// para a vaga de quem caiu do mundo.
        /// </summary>
        public bool LugarDaMoto(int i, bool procurar, out Vector3 ponto, out Quaternion giro)
        {
            ponto = Vector3.zero;
            giro = Quaternion.identity;
            if (Dados == null) return false;
            if (Dados.LugarDoCarro.HasValue && Dados.LugarDoSegundoCarro.HasValue)
            {
                var a = Dados.LugarDoCarro.Value;
                var b = Dados.LugarDoSegundoCarro.Value;
                var passo = b - a;
                passo.y = 0f;
                var ao = passo.sqrMagnitude > 0.01f ? passo.normalized : Vector3.right;
                giro = Quaternion.Euler(0f, Dados.GiroDoCarro, 0f);
                var vaga = a - passo;   // o chão da vaga (as dos carros nascem 0,45 m acima dele)
                float[] desvios = { -0.85f, 0.85f, -1.3f, 1.3f, 0f };
                if (procurar)
                    foreach (float d in desvios)
                    {
                        var p = vaga + ao * d;
                        if (Physics.CheckBox(p + Vector3.up * 0.6f, new Vector3(0.42f, 0.5f, 1.2f), giro, ~0, QueryTriggerInteraction.Ignore)) continue;
                        ponto = p + Vector3.up * 0.3f;
                        return true;
                    }
                ponto = vaga + ao * (i == 0 ? -0.85f : 0.85f) + Vector3.up * 0.3f;
                return true;
            }
            giro = Quaternion.Euler(0f, Dados.GiroDaPartida, 0f);
            float lado = DadosDoMundo.LadoDaPartida * 0.5f + 1.6f;
            var candidatos = new[] { -lado, -lado - 1.8f, lado, lado + 1.8f, -lado - 3.6f, lado + 3.6f, -lado - 5.4f, lado + 5.4f };
            int pulando = i;   // sem procurar: a vaga da moto i é o i-ésimo da lista
            foreach (float x in candidatos)
            {
                var p = Dados.Partida + giro * new Vector3(x, 0f, 0f);
                p.y = Dados.Altura(p.x, p.z) + 0.3f;
                if (!procurar)
                {
                    if (pulando-- > 0) continue;
                    ponto = p;
                    return true;
                }
                bool perto = false;
                foreach (var c in Vehicles.Carro.Todos)
                    if (c != null && Vector3.Distance(p, c.transform.position) < (c.moto ? 1.6f : 3.4f)) perto = true;
                if (perto || Physics.CheckBox(p + Vector3.up * 0.75f, new Vector3(0.45f, 0.45f, 1.25f), giro, ~0, QueryTriggerInteraction.Ignore)) continue;
                ponto = p;
                return true;
            }
            ponto = Dados.Partida + giro * new Vector3(-lado - 1.8f * (i + 1), 0f, 0f);
            ponto.y = Dados.Altura(ponto.x, ponto.z) + 0.3f;
            return true;
        }

        /// <summary>Qual é o veículo, para o save: 0 a perua, 1 o cupê, 2 em diante as motos (na ordem de <see cref="motos"/>).</summary>
        public int QualDoCarro(Vehicles.Carro c)
        {
            if (c == null) return 0;
            if (motos != null)
                for (int i = 0; i < motos.Length; i++)
                    if (motos[i] != null && c.name.StartsWith(motos[i].name)) return 2 + i;
            return segundoCarro != null && c.name.StartsWith(segundoCarro.name) ? 1 : 0;
        }

        /// <summary>O prefab do veículo pelo número do save (<see cref="QualDoCarro"/>), ou null.</summary>
        public GameObject PrefabDoCarro(int qual) => qual == 1 ? segundoCarro
            : qual >= 2 ? (motos != null && qual - 2 < motos.Length ? motos[qual - 2] : null) : carro;

        /// <summary>
        /// No anfitrião: a chave do cupê (04/11/2026, a pedido) no chão da sala, ao lado da da perua (30 cm para o lado do
        /// segundo nascimento). No mundo novo, junto com os carros; num save de antes dela, uma vez ao carregar
        /// (<see cref="Salvar.MundoSalvo.chaveDoCupe"/>). Sem o cupê, nada. Devolve se ela foi posta (a marca no save só
        /// com ela no chão: se algo falhar, o próximo carregamento tenta de novo).
        /// </summary>
        public bool PorAChaveDoCupe()
        {
            var itens = Loot.ItensNoMundo.Instancia;
            if (segundoCarro == null || Dados == null || itens == null || Dados.Nascimentos.Length < 2) return false;
            var meio = Vector3.Lerp(Dados.Nascimentos[0], Dados.Nascimentos[1], 0.5f);
            var lado = Dados.Nascimentos[1] - Dados.Nascimentos[0];
            lado.y = 0f;
            var ponto = meio + (lado.sqrMagnitude > 0.01f ? lado.normalized * 0.3f : Vector3.zero) + Vector3.down * 0.05f;
            itens.Criar(Loot.Item.ChaveDoCupe, ponto, Dados.GiroDaPartida + 10f);
            return true;
        }

        /// <summary>
        /// No anfitrião: a vaga de um carro (a dele no bunker da família; sem o bunker, ao lado da laje da partida), para onde
        /// volta o carro que caiu do mundo (Carro.Parado.cs).
        /// </summary>
        public bool VagaDoCarro(Vehicles.Carro c, out Vector3 ponto, out Quaternion giro)
        {
            ponto = Vector3.zero;
            giro = Quaternion.identity;
            if (Dados == null || c == null) return false;
            int qual = QualDoCarro(c);
            if (qual >= 2) return LugarDaMoto(qual - 2, false, out ponto, out giro);
            bool segundo = qual == 1;
            var vaga = segundo ? Dados.LugarDoSegundoCarro : Dados.LugarDoCarro;
            if (vaga.HasValue)
            {
                ponto = vaga.Value + Vector3.up * 0.45f;
                giro = Quaternion.Euler(0f, Dados.GiroDoCarro, 0f);
                return true;
            }
            giro = Quaternion.Euler(0f, Dados.GiroDaPartida, 0f);
            ponto = Dados.Partida + giro * new Vector3((DadosDoMundo.LadoDaPartida * 0.5f + 3.2f) * (segundo ? -1f : 1f), 0f, 0f);
            ponto.y = Dados.Altura(ponto.x, ponto.z) + 0.4f;
            return true;
        }

        // o segundo carro (o cupê) ao lado da laje da partida, virado para o mesmo lado dela: o primeiro lugar (dos dois
        // lados da laje) a mais de 5,5 m do carro da família e sem nada no caminho (uma parede, uma árvore, outro carro).
        // Na vaga da casa, o carro da família às vezes fica alinhado com o primeiro lugar (no teste, encostado)
        void CriarSegundoCarro(GameObject familia)
        {
            if (segundoCarro == null) return;
            // na vaga do lado da perua, no bunker da família
            if (Dados.LugarDoSegundoCarro.HasValue)
            {
                var cupe = Instantiate(segundoCarro, Dados.LugarDoSegundoCarro.Value + Vector3.up * 0.45f, Quaternion.Euler(0f, Dados.GiroDoCarro, 0f));
                cupe.name = segundoCarro.name;
                cupe.GetComponent<NetworkObject>().Spawn(true);
                return;
            }
            var giro = Quaternion.Euler(0f, Dados.GiroDaPartida, 0f);
            Physics.SyncTransforms();
            float lado = DadosDoMundo.LadoDaPartida * 0.5f + 3.2f;
            Vector3 ponto = Vector3.zero;
            bool achou = false;
            foreach (float x in new[] { lado, lado + 3.5f, -lado, -lado - 3.5f, lado + 7f })
            {
                var p = Dados.Partida + giro * new Vector3(x, 0f, 0f);
                p.y = Dados.Altura(p.x, p.z) + 0.4f;
                if (familia != null && Vector3.Distance(p, familia.transform.position) < 5.5f) continue;
                if (Physics.CheckBox(p + Vector3.up * 0.75f, new Vector3(1.0f, 0.45f, 2.45f), giro, ~0, QueryTriggerInteraction.Ignore)) continue;
                ponto = p;
                achou = true;
                break;
            }
            if (!achou)
            {
                ponto = Dados.Partida + giro * new Vector3(lado + 3.5f, 0f, 0f);
                ponto.y = Dados.Altura(ponto.x, ponto.z) + 0.4f;
            }
            var novo = Instantiate(segundoCarro, ponto, giro);
            novo.name = segundoCarro.name;
            novo.GetComponent<NetworkObject>().Spawn(true);
        }

        /// <summary>
        /// A rede de segurança: se o jogador local atravessou o chão (chegou num lugar antes da colisão do bloco), volta
        /// para a superfície assim que a colisão dali estiver pronta. Só o dono move o personagem.
        /// </summary>
        MovimentoPrimeiraPessoa _movimento;   // o do jogador local (era um GetComponent a cada quadro)

        void ManterNoChao(JogadorRede jogador)
        {
            if (_movimento == null || _movimento.gameObject != jogador.gameObject) _movimento = jogador.GetComponent<MovimentoPrimeiraPessoa>();
            var mov = _movimento;
            if (mov == null || mov.Voando || (jogador.Passageiro != null && jogador.Passageiro.Carro != null)) return;
            var p = jogador.transform.position;
            // no bunker da família, os andares ficam embaixo do chão (e do morro)
            if (BunkerDoMundo.Dentro(p)) return;
            float chao = Dados.Altura(p.x, p.z);
            if (p.y > chao - 3f || !terreno.Pronto(p, 4f)) return;
            mov.Teleportar(new Vector3(p.x, chao + 0.3f, p.z), jogador.transform.eulerAngles.y);
            Debug.LogWarning($"[Mundo] O jogador passou pelo chão em ({p.x:F0}, {p.z:F0}); voltou para a superfície.");
        }

        /// <summary>Cliente → anfitrião: o meu mundo está pronto (com o resumo dele).</summary>
        [Rpc(SendTo.Server)]
        void ProntoRpc(ulong resumo, RpcParams parametros = default)
        {
            ulong cliente = parametros.Receive.SenderClientId;
            if (!Pronto)
            {
                _esperando.Add((cliente, resumo));
                return;
            }
            Aceitar(cliente, resumo);
        }

        void Aceitar(ulong cliente, ulong resumo)
        {
            if (!NetworkManager.ConnectedClients.ContainsKey(cliente)) return;
            if (resumo != Dados.Resumo)
            {
                Debug.LogWarning($"[Mundo] O mundo do jogador {cliente} saiu diferente do anfitrião ({resumo:X16} ≠ {Dados.Resumo:X16}).");
                DiferenteRpc(Dados.Resumo, RpcTarget.Single(cliente, RpcTargetUse.Temp));
            }
            else Debug.Log($"[Mundo] O jogador {cliente} gerou o mesmo mundo (resumo {resumo:X16}).");
            CriarJogador(cliente);
        }

        /// <summary>Anfitrião → cliente: o seu mundo não bateu com o meu.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void DiferenteRpc(ulong doAnfitriao, RpcParams parametros = default)
        {
            Aviso = $"O seu mundo saiu diferente do anfitrião (resumo {Dados?.Resumo:X16}, o dele {doAnfitriao:X16}). Confira se o jogo é a mesma versão.";
            Debug.LogWarning("[Mundo] " + Aviso);
        }

        void CriarJogador(ulong cliente)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) && c.PlayerObject != null) return;
            var ponto = PontosDeNascimento.Ponto(cliente);
            var jogador = Instantiate(NetworkManager.NetworkConfig.PlayerPrefab, ponto.position, ponto.rotation);
            jogador.GetComponent<NetworkObject>().SpawnAsPlayerObject(cliente, true);
        }

        void AoSair(ulong cliente) => _esperando.RemoveAll(e => e.cliente == cliente);
    }
}
