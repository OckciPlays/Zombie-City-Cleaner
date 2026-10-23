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
        public int Jogadores => _sessao != null ? _sessao.PlayerCount : (_rede != null && _rede.IsServer ? _rede.ConnectedClientsIds.Count : 0);

        NetworkManager _rede;
        ISession _sessao;

        void Awake()
        {
            Instancia = this;
            _rede = GetComponent<NetworkManager>();
        }

        void OnEnable() => _rede.OnClientStopped += AoParar;
        void OnDisable() => _rede.OnClientStopped -= AoParar;

        void AoParar(bool eraHost)
        {
            // O anfitrião saiu ou a conexão caiu: volta para o menu.
            if (Atual == Estado.Desconectado) return;
            if (!eraHost) Erro = "A partida acabou (o anfitrião saiu ou a conexão caiu).";
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

        public async void Sair()
        {
            var sessao = _sessao;
            _sessao = null;
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
