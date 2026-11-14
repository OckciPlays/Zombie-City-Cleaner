using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ZombieCityCleaner.Network
{
    /// <summary>
    /// Entrada na partida. Hospedar cria uma partida privada de até 4 jogadores pelo Relay da Unity e mostra o
    /// código; entrar usa esse código. O pacote de multiplayer liga o Netcode sozinho (o anfitrião vira host).
    /// O modo local (IP direto, sem Relay) serve para testar sem internet ou jogar em rede local.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class SessaoOnline : MonoBehaviour
    {
        public const int MaxJogadores = 4;
        public const ushort PortaLocal = 7777;

        public enum Estado { Desconectado, Conectando, Conectado }

        public static SessaoOnline Instancia { get; private set; }

        public Estado Atual { get; private set; }
        public string Codigo { get; private set; }
        public string Erro { get; private set; }
        public bool Local { get; private set; }
        // (no jogo local por IP, sem a sessão online, quem entrava via "0/4": a lista de conectados só existe no anfitrião;
        // ali conta os jogadores que chegaram, 02/11/2026)
        public int Jogadores => _sessao != null ? _sessao.PlayerCount
            : _rede != null && _rede.IsServer ? _rede.ConnectedClientsIds.Count : Player.JogadorRede.Todos.Count;

        NetworkManager _rede;
        ISession _sessao;

        /// <summary>
        /// A versão da rede (05/11/2026, o corpo do jogador: o estado e os RPCs do jogador mudaram). Duas cópias do jogo com
        /// versões diferentes não se conectam (o NGO recusa). Suba a cada mudança no que vai pela rede.
        /// </summary>
        public const ushort VersaoDaRede = 27;   // (27: a etapa 5 da fauna: o bicho sentado (o urso comendo o salmão, os filhotes junto), 13/11/2026; 26: a represa dos castores na mensagem das casas (o giro e a largura) e os sons do lixo (a sacola, o remexer, a caçamba), 13/11/2026; 25: a noite da fauna (a etapa 4): o cheiro do gambá e o veneno da cascavel nos jogadores (as NetworkVariables) e os sons novos, 12/11/2026; 24: a água da fauna (a etapa 3): as casas dos bichos (ZCC.FaunaCasas), o bit de carregando, os sons novos e o bagre (o item 138), 12/11/2026; 23: os predadores da fauna, o som dos bichos na rede (ZCC.FaunaSom) e o RPC do empurrão do bicho, 12/11/2026; 22: a fauna, os RPCs do acerto no bicho e de carnear e os itens da caça, 12/11/2026; 21: o tiro na cerca de fogo, o RPC do tiro e o do estouro do galão, a roupa molhada (InventarioJogador.Molhado) e o ramo Atleta (5 nós novos na árvore), 12/11/2026; 20: as barricadas de tábuas e a Fortaleza, a etapa 5b: o vão, o tamanho e os bits na Construcao, a receita e os RPCs de pregar, desmontar, consertar e do golpe, 11/11/2026; 19: as armadilhas, a etapa 5a: o nível do Armadilheiro na Construcao, as receitas e o RPC delas, 11/11/2026; 18: o fogo, a etapa 4: as receitas novas, o arremesso do molotov, o reabastecer do lampião e as áreas de fogo, 11/11/2026; 17: a proteção de sucata, a InventarioJogador.Protecao, os RPCs dela e o personagem com ela, 11/11/2026; 16: as melhorias das armas de sucata, os RPCs de pôr e tirar, 10/11/2026; 15: o giro do olhar sentado, JogadorRede.GiroSentado, 10/11/2026; 14: os painéis de metal desmontados, 09/11/2026; 13: as armas de sucata, as munições e a sucata, 09/11/2026; 12: a árvore de habilidades nova, com os níveis, 09/11/2026; 11: os cortes dos lados da árvore, 07/11/2026; 2: a infecção; 3: o curativo no amigo e a pose; 4: a mente; 5: o desmaio; 6: o sono e o calmante; 7: a árvore de habilidades; 8: o fôlego, o caiaque, a pesca e as árvores cortadas, 06/11/2026; 9: a boia da pesca para todos e o remo do caiaque, 07/11/2026; 10: o zumbi sem cabeça que ainda anda, 07/11/2026)

        void Awake()
        {
            Instancia = this;
            _rede = GetComponent<NetworkManager>();
            if (_rede != null && _rede.NetworkConfig != null) _rede.NetworkConfig.ProtocolVersion = VersaoDaRede;
        }

        // chegou a entrar na partida (a conexão foi aceita): sem isso, a queda é a recusa da versão
        bool _entrou;

        void OnEnable()
        {
            _rede.OnClientStopped += AoParar;
            _rede.OnClientConnectedCallback += AoConectar;
        }

        void OnDisable()
        {
            _rede.OnClientStopped -= AoParar;
            _rede.OnClientConnectedCallback -= AoConectar;
        }

        void AoConectar(ulong id)
        {
            if (id == _rede.LocalClientId) _entrou = true;
        }

        void AoParar(bool eraHost)
        {
            // O anfitrião saiu ou a conexão caiu: volta para o menu.
            if (Atual == Estado.Desconectado) return;
            if (!eraHost)
                Erro = !_entrou
                    ? "Não deu para entrar na partida (o anfitrião não respondeu, ou os dois jogos estão em versões diferentes)."
                    : "A partida acabou (o anfitrião saiu ou a conexão caiu).";
            _entrou = false;
            _sessao = null;
            Atual = Estado.Desconectado;
            Codigo = null;
            Local = false;
        }

        // Cada cópia do jogo entra com um perfil anônimo próprio: duas cópias no mesmo PC não podem usar a mesma conta.
        static async Task PrepararServicos()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 10)));
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async void Hospedar()
        {
            if (Atual != Estado.Desconectado) return;
            await Executar(async () =>
            {
                await PrepararServicos();
                var opcoes = new SessionOptions { MaxPlayers = MaxJogadores, IsPrivate = true }.WithRelayNetwork();
                _sessao = await MultiplayerService.Instance.CreateSessionAsync(opcoes);
                Codigo = _sessao.Code;
            });
        }

        public async void Entrar(string codigo)
        {
            if (Atual != Estado.Desconectado) return;
            codigo = (codigo ?? "").Trim().ToUpperInvariant();
            if (codigo.Length == 0)
            {
                Erro = "Digite o código da partida.";
                return;
            }
            await Executar(async () =>
            {
                await PrepararServicos();
                _sessao = await MultiplayerService.Instance.JoinSessionByCodeAsync(codigo);
                Codigo = _sessao.Code;
            });
        }

        public void HospedarLocal()
        {
            if (Atual != Estado.Desconectado) return;
            _rede.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", PortaLocal, "0.0.0.0");
            Erro = null;
            Local = true;
            Codigo = "LOCAL";
            Atual = _rede.StartHost() ? Estado.Conectado : Estado.Desconectado;
            if (Atual == Estado.Desconectado) Erro = $"Não deu para abrir a porta {PortaLocal}.";
        }

        public void EntrarLocal(string ip)
        {
            if (Atual != Estado.Desconectado) return;
            _rede.GetComponent<UnityTransport>().SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), PortaLocal);
            Erro = null;
            Local = true;
            Codigo = "LOCAL";
            Atual = _rede.StartClient() ? Estado.Conectado : Estado.Desconectado;
        }

        async Task Executar(Func<Task> acao)
        {
            Atual = Estado.Conectando;
            Erro = null;
            try
            {
                await acao();
                Atual = Estado.Conectado;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Erro = Traduzir(e);
                _sessao = null;
                Atual = Estado.Desconectado;
            }
        }

        static string Traduzir(Exception e)
        {
            if (e is SessionException s)
            {
                switch (s.Error)
                {
                    case SessionError.SessionNotFound: return "Partida não encontrada. Confira o código.";
                    case SessionError.Forbidden: return "A partida está cheia ou fechada.";
                }
            }
            if (e is RequestFailedException) return "Sem conexão com os serviços da Unity. Confira a internet.";
            return "Não deu para conectar: " + e.Message;
        }

        /// <summary>Sai da partida. Com o erro, o menu mostra o porquê (na cena Mundo, a versão do gerador diferente).</summary>
        public async void Sair(string erro = null)
        {
            var sessao = _sessao;
            _sessao = null;
            Erro = erro;
            Atual = Estado.Desconectado;
            Codigo = null;
            Local = false;
            try
            {
                if (sessao != null) await sessao.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Erro ao sair da partida: " + e.Message);
            }
            if (_rede.IsListening) _rede.Shutdown();
        }
    }
}
