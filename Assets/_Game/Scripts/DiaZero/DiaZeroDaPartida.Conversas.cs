using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A pausa de um NPC do Dia 0 na conversa (o anfitrião escreve): quantos conversam com ele agora (o primeiro é
    /// <see cref="quem"/>), desde quando (no relógio da partida; menor que 0: andando) e quanto tempo ele já ficou parado nas
    /// conversas que acabaram.
    /// </summary>
    public struct PausaDoNpc : INetworkSerializeByMemcpy, IEquatable<PausaDoNpc>
    {
        public short npc;
        public byte quantos;
        public ulong quem;
        public double desde, parado;
        public bool Equals(PausaDoNpc o) => npc == o.npc && quantos == o.quantos && quem == o.quem && desde == o.desde && parado == o.parado;
    }

    /// <summary>
    /// Os NPCs do Dia 0 parados na conversa, em todas as máquinas (25/11/2026, a pedido do Pedro: "enquanto alguns npcs andam
    /// e eu converso com eles, para eles, eles ainda estão andando e não parados. Quando termino a conversa, eles aparecem
    /// longe de mim como se não tivesse parado eles"). Quem anda está onde o relógio da partida diz
    /// (<see cref="NpcsDoDiaZero.Agora"/>); antes, só a máquina de quem conversava o segurava, e o relógio seguia: na volta,
    /// ele pulava para o lugar do relógio. Agora a conversa vai ao anfitrião (<see cref="ConversaComNpcRpc"/>), que guarda a
    /// pausa de cada um (<see cref="PausaDoNpc"/>): todas as máquinas o veem parado, olhando para quem conversa, e o relógio
    /// dele desconta o tempo parado (<see cref="AgoraDoNpc"/>), e ele volta a andar de onde parou.
    /// </summary>
    public partial class DiaZeroDaPartida
    {
        /// <summary>Uma conversa que não fechou (quem conversava caiu da partida) acaba sozinha depois disto (s).</summary>
        const double ConversaMaisLonga = 180.0;

        NetworkList<PausaDoNpc> _pausas;

        /// <summary>
        /// No anfitrião, fora da rede: quem conversa com cada NPC agora (o cliente e desde quando), na ordem em que abriram
        /// (25/11/2026, a revisão: antes só o primeiro era guardado; se ele caía da partida, o NPC voltava a andar com o outro
        /// ainda conversando, e se o segundo caía, o NPC ficava parado para todos até os 180 s). O <see cref="PausaDoNpc.quantos"/>
        /// é o tamanho da lista, e o <see cref="PausaDoNpc.quem"/>, o primeiro que sobrou.
        /// </summary>
        readonly Dictionary<short, List<(ulong cliente, double desde)>> _quemConversa = new Dictionary<short, List<(ulong cliente, double desde)>>();

        // o relógio da partida: o do anfitrião (o mesmo do NpcsDoDiaZero.Agora)
        double AgoraNaRede => NpcsDoDiaZero.Agora;

        void CriarAsPausas() => _pausas = new NetworkList<PausaDoNpc>();

        /// <summary>
        /// O relógio de um NPC (o da partida, menos o tempo que ele ficou parado nas conversas) e se alguém conversa com ele
        /// agora (em qualquer máquina), com quem (o cliente do primeiro). Sem a rede, o próprio relógio da partida.
        /// </summary>
        public static double AgoraDoNpc(int npc, out bool parado, out ulong quem)
        {
            parado = false;
            quem = 0;
            double agora = NpcsDoDiaZero.Agora;
            var d = Instancia;
            if (d == null || !d.IsSpawned || d._pausas == null) return agora;
            for (int i = 0; i < d._pausas.Count; i++)
            {
                var p = d._pausas[i];
                if (p.npc != npc) continue;
                double desconto = p.parado;
                if (p.quantos > 0 && p.desde >= 0.0)
                {
                    parado = true;
                    quem = p.quem;
                    desconto += Math.Max(0.0, agora - p.desde);
                }
                return agora - desconto;
            }
            return agora;
        }

        /// <summary>A conversa com um NPC desta máquina abriu (true) ou fechou: vai ao anfitrião.</summary>
        public void ConversarComNpc(int npc, bool comecou)
        {
            if (IsSpawned && npc >= 0 && npc < short.MaxValue) ConversaComNpcRpc((short)npc, comecou);
        }

        [Rpc(SendTo.Server)]
        void ConversaComNpcRpc(short npc, bool comecou, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            double agora = AgoraNaRede;
            int i = 0;
            while (i < _pausas.Count && _pausas[i].npc != npc) i++;
            var p = i < _pausas.Count ? _pausas[i] : new PausaDoNpc { npc = npc, desde = -1.0 };
            var lista = QuemConversa(npc, p);
            if (comecou)
            {
                if (lista.Count == 0) p.desde = agora;
                if (!lista.Exists(x => x.cliente == quem)) lista.Add((quem, agora));
            }
            else
            {
                // (quem não está na conta, como depois dos 180 s, não mexe na pausa dos outros)
                int k = lista.FindIndex(x => x.cliente == quem);
                if (k < 0) return;
                lista.RemoveAt(k);
                if (lista.Count == 0) Retomar(ref p, agora);
            }
            Contar(ref p, lista);
            if (i < _pausas.Count) _pausas[i] = p;
            else _pausas.Add(p);
        }

        // a lista de quem conversa com o NPC (no anfitrião); a pausa sem ninguém (a primeira, ou a lista da rede limpa numa
        // partida nova) começa com ela vazia
        List<(ulong cliente, double desde)> QuemConversa(short npc, PausaDoNpc p)
        {
            if (!_quemConversa.TryGetValue(npc, out var lista)) _quemConversa[npc] = lista = new List<(ulong cliente, double desde)>();
            if (p.quantos == 0) lista.Clear();
            return lista;
        }

        // o quantos e o quem da rede pela lista: o primeiro que sobrou é para quem o NPC olha
        static void Contar(ref PausaDoNpc p, List<(ulong cliente, double desde)> lista)
        {
            p.quantos = (byte)Math.Min(lista.Count, byte.MaxValue);
            p.quem = lista.Count > 0 ? lista[0].cliente : 0;
        }

        // a pausa acabou: o tempo parado entra no desconto e ele volta a andar
        static void Retomar(ref PausaDoNpc p, double agora)
        {
            if (p.desde >= 0.0) p.parado += Math.Max(0.0, agora - p.desde);
            p.desde = -1.0;
            p.quantos = 0;
        }

        // no anfitrião: quem saiu da partida no meio da conversa, ou uma conversa que nunca fechou, não segura o NPC.
        // (25/11/2026, a revisão) Sai da conta só quem saiu (ou a conversa dele passou dos 180 s); os outros continuam, e o NPC
        // volta a andar só quando não sobra ninguém
        void AcompanharAsPausas(ulong saiu = ulong.MaxValue)
        {
            if (!IsServer || _pausas == null) return;
            double agora = AgoraNaRede;
            for (int i = 0; i < _pausas.Count; i++)
            {
                var p = _pausas[i];
                if (p.quantos == 0) continue;
                if (!_quemConversa.TryGetValue(p.npc, out var lista) || lista.Count == 0)
                {
                    // (sem a lista, o que não deve acontecer: como antes, solta)
                    Retomar(ref p, agora);
                    _pausas[i] = p;
                    continue;
                }
                int antes = lista.Count;
                lista.RemoveAll(x => x.cliente == saiu || agora - x.desde > ConversaMaisLonga);
                if (lista.Count == antes) continue;
                if (lista.Count == 0) Retomar(ref p, agora);
                Contar(ref p, lista);
                _pausas[i] = p;
            }
        }
    }
}
