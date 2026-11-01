using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A fase do Dia 0: na casa (o lobby: todos chegando, ninguém sai), a TV (o jornal tocando para todos) e a rua (as
    /// compras liberadas).
    /// </summary>
    public enum FaseDoDia : byte { Casa, Tv, Rua }

    /// <summary>
    /// O lobby do Dia 0 (01/11/2026, a pedido: "o jogo começa dentro da casa ... não permite que os jogadores saiam da casa
    /// (isso funcionaria como um lobby pré-jogo) ... quando todos entrarem no jogo, alguém aperta E na televisão e começa a
    /// tocar a animação e depois estamos liberados para compra"):
    ///  - <see cref="FaseDoDia.Casa"/>: cada um nasce na sala e cria o personagem; quem passa da porta (sai da caixa da casa)
    ///    volta para dentro. A festa toca na sala;
    ///  - E na TV da sala (<see cref="TelevisaoDoDiaZero"/>) liga o jornal para todos (<see cref="LigarATvRpc"/>): o
    ///    anfitrião guarda o começo no relógio da partida, e cada um vê a introdução dali (quem chega no meio, do ponto em
    ///    que ela está). Enter ou Espaço pula só para quem apertou;
    ///  - quando todos acabaram de ver (ou pularam: <see cref="AcabeiATvRpc"/>), ou o tempo dela passou, a rua é liberada
    ///    (<see cref="FaseDoDia.Rua"/>). Depois, E na TV mostra o jornal de novo, só para quem pediu.
    /// O F1 libera a rua na hora.
    /// </summary>
    public partial class DiaZeroDaPartida
    {
        readonly NetworkVariable<byte> _fase = new NetworkVariable<byte>();
        readonly NetworkVariable<double> _tvDesde = new NetworkVariable<double>();
        readonly HashSet<ulong> _viram = new HashSet<ulong>();
        Vector3 _ultimoDentro;
        bool _temUltimoDentro;
        float _proximoAvisoDaPorta;

        /// <summary>A fase do Dia 0 agora.</summary>
        public FaseDoDia Fase => (FaseDoDia)_fase.Value;

        /// <summary>Há quanto tempo a TV foi ligada (s, no relógio da partida).</summary>
        public float TempoDaTv => (float)(NetworkManager.ServerTime.Time - _tvDesde.Value);

        // no anfitrião, quando a partida começa
        void ComecarOLobby()
        {
            _fase.Value = (byte)FaseDoDia.Casa;
            _tvDesde.Value = 0;
            _viram.Clear();
        }

        /// <summary>O jogador local ligou a TV da sala (no lobby): o jornal começa para todos.</summary>
        public void LigarATv()
        {
            if (IsSpawned && Fase == FaseDoDia.Casa) LigarATvRpc();
        }

        [Rpc(SendTo.Server)]
        void LigarATvRpc(RpcParams parametros = default)
        {
            if (!_mundo.NoDiaZero || Fase != FaseDoDia.Casa) return;
            _viram.Clear();
            _tvDesde.Value = NetworkManager.ServerTime.Time;
            _fase.Value = (byte)FaseDoDia.Tv;
            Debug.Log($"[Dia 0] O jogador {parametros.Receive.SenderClientId} ligou a TV: o jornal começou para todos.");
        }

        /// <summary>A introdução de todos acabou (ou foi pulada) nesta máquina.</summary>
        public void AcabeiATv()
        {
            if (IsSpawned && Fase == FaseDoDia.Tv) AcabeiATvRpc();
        }

        [Rpc(SendTo.Server)]
        void AcabeiATvRpc(RpcParams parametros = default)
        {
            _viram.Add(parametros.Receive.SenderClientId);
            ConferirATv();
        }

        /// <summary>O F1: a rua liberada na hora (sem esperar o jornal).</summary>
        public void PedirLiberarARua()
        {
            if (IsSpawned && Fase != FaseDoDia.Rua) LiberarARuaRpc();
        }

        [Rpc(SendTo.Server)]
        void LiberarARuaRpc() => LiberarARua();

        void LiberarARua()
        {
            if (Fase == FaseDoDia.Rua) return;
            _fase.Value = (byte)FaseDoDia.Rua;
            Debug.Log("[Dia 0] A rua foi liberada: as compras começaram.");
        }

        // no anfitrião: com todos que têm o jogador tendo visto o jornal (ou o tempo dele passado), a rua é liberada
        void ConferirATv()
        {
            if (!IsServer || Fase != FaseDoDia.Tv) return;
            if (TempoDaTv > IntroDoDiaZero.Duracao + 3f)
            {
                LiberarARua();
                return;
            }
            foreach (var kv in NetworkManager.ConnectedClients)
                if (kv.Value.PlayerObject != null && !_viram.Contains(kv.Key)) return;
            LiberarARua();
        }

        // em cada máquina, no Dia 0: antes da rua liberada, quem sai da casa volta para dentro
        void NoLobby(JogadorRede j)
        {
            var rua = RuaDoDiaZero.Instancia;
            if (rua == null || Fase == FaseDoDia.Rua) return;
            var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null || mov.Voando) return;
            var p = j.transform.position;
            if (rua.DentroDaCasa(p))
            {
                _ultimoDentro = p;
                _temUltimoDentro = true;
                return;
            }
            if (!_temUltimoDentro) return;
            mov.Teleportar(_ultimoDentro + Vector3.up * 0.05f, j.transform.eulerAngles.y);
            if (Time.unscaledTime >= _proximoAvisoDaPorta)
            {
                _proximoAvisoDaPorta = Time.unscaledTime + 3f;
                Avisar(Fase == FaseDoDia.Casa
                    ? "Ainda não: esperem todos chegarem e liguem a TV da sala (E)."
                    : "Ainda não: o jornal está passando para os outros.");
            }
        }

        // a linha do lobby, em cima, no meio da tela
        void DesenharOLobby(float u)
        {
            if (Fase == FaseDoDia.Rua) return;
            int n = 0;
            foreach (var j in JogadorRede.Todos) if (j != null) n++;
            string texto = Fase == FaseDoDia.Casa
                ? $"NA CASA  ·  {n} {(n == 1 ? "jogador" : "jogadores")}  ·  quando todos chegarem, liguem a TV da sala (E)"
                : "O JORNAL ESTÁ PASSANDO  ·  a rua abre quando todos acabarem de ver";
            Sombra(new Rect(Screen.width / 2f - 450 * u, 16 * u, 900 * u, 30 * u), texto,
                   new GUIStyle(_aviso) { fontSize = Mathf.RoundToInt(17 * u), fontStyle = FontStyle.Bold });
        }
    }
}
