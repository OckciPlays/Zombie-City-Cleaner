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
    public class MundoDaPartida : NetworkBehaviour
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

        readonly NetworkVariable<int> _semente = new NetworkVariable<int>();
        readonly NetworkVariable<byte> _tamanho = new NetworkVariable<byte>();
        readonly NetworkVariable<int> _versao = new NetworkVariable<int>();
        readonly NetworkVariable<FixedString64Bytes> _nome = new NetworkVariable<FixedString64Bytes>();
        // o Dia 0 (01/11/2026): enquanto ele dura, o mundo espera (a geração começa quando a porta da casa fecha)
        readonly NetworkVariable<bool> _diaZero = new NetworkVariable<bool>();

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
        readonly List<(ulong cliente, ulong resumo)> _esperando = new List<(ulong, ulong)>();
        NetworkManager _rede;

        void Awake()
        {
            Instancia = this;
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
                var p = Pedido;
                _semente.Value = p.semente > 0 ? p.semente : GeradorDoMundo.NovaSemente();
                _tamanho.Value = (byte)p.tamanho;
                _versao.Value = GeradorDoMundo.Versao;
                string nome = string.IsNullOrWhiteSpace(p.nome) ? "Mundo novo" : p.nome.Trim();
                _nome.Value = new FixedString64Bytes(nome.Length > 30 ? nome.Substring(0, 30) : nome);
                _diaZero.Value = p.diaZero;
                NetworkManager.OnClientDisconnectCallback += AoSair;
            }
            VeioDoDiaZero = _diaZero.Value;
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
            Pronto = true;
            Progresso = 1f;
            Etapa = IsServer ? "Pronto" : "Esperando o anfitrião";
            SegundosAtePronto = Time.realtimeSinceStartup - _comecou;
            Debug.Log($"[Mundo] Pronto para jogar em {SegundosAtePronto * 1000f:F0} ms ({terreno.Blocos} blocos de terreno, {terreno.BlocosComColisao} com colisão).");
            if (IsServer)
            {
                CriarJogador(NetworkManager.LocalClientId);
                foreach (var (cliente, resumo) in _esperando) Aceitar(cliente, resumo);
                _esperando.Clear();
                CriarCarro();
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
        }

        // o segundo carro (o cupê) ao lado da laje da partida, virado para o mesmo lado dela: o primeiro lugar (dos dois
        // lados da laje) a mais de 5,5 m do carro da família e sem nada no caminho (uma parede, uma árvore, outro carro).
        // Na vaga da casa, o carro da família às vezes fica alinhado com o primeiro lugar (no teste, encostado)
        void CriarSegundoCarro(GameObject familia)
        {
            if (segundoCarro == null) return;
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
        void ManterNoChao(JogadorRede jogador)
        {
            var mov = jogador.GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null || mov.Voando || (jogador.Passageiro != null && jogador.Passageiro.Carro != null)) return;
            var p = jogador.transform.position;
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
