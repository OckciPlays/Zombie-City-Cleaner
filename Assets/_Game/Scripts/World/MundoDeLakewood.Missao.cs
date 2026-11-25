using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>A etapa da primeira missão depois dos 5 anos (24/11/2026, <see cref="LakewoodNaCena"/>, o Missao).</summary>
    public enum EtapaDaMissao : byte { Nenhuma, FaleComOCapitao, VaParaACasa, RetomeARua, ConserteACaminhonete, Cumprida }

    /// <summary>
    /// A primeira missão depois dos 5 anos, na rede (24/11/2026, o Pedro: "Perdemos todos os itens na loucura do D0, a primeira
    /// missão da gente é retomar a cidade com o npc capitão (junto com os colegas) e lá recuperamos os itens que compramos no
    /// D0, também podemos concertar a caminhonete (onde ela estará totalmente danificada, precisando trocar as rodas e os
    /// componentes do motor)"): a etapa e os números da tela (os zumbis que faltam, as peças que faltam, o que está na
    /// caçamba). O anfitrião decide (<see cref="LakewoodNaCena"/>); o save não guarda nada a mais: a missão sai do estado da
    /// caminhonete (quebrada na casa da família, a missão volta à conversa com o Capitão; consertada, ela acabou).
    /// </summary>
    public partial class MundoDeLakewood
    {
        readonly NetworkVariable<byte> _missao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _zumbisDaMissao = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _pecasQueFaltam = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> _naCacamba = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public EtapaDaMissao Missao => IsSpawned ? (EtapaDaMissao)_missao.Value : EtapaDaMissao.Nenhuma;
        public int ZumbisDaMissao => IsSpawned ? _zumbisDaMissao.Value : 0;
        public int PecasQueFaltam => IsSpawned ? _pecasQueFaltam.Value : 0;
        public int NaCacamba => IsSpawned ? _naCacamba.Value : 0;

        /// <summary>
        /// No anfitrião: a primeira missão já foi cumprida (vai no save, <c>MundoSalvo.primeiraMissao</c>). Sem isto, depois de
        /// carregar, um pneu furado longe do bunker faria a missão começar de novo.
        /// </summary>
        public bool PrimeiraMissaoCumprida { get; set; }

        /// <summary>No anfitrião: a etapa da missão.</summary>
        public void PorAMissao(EtapaDaMissao e)
        {
            if (!IsServer || _missao.Value == (byte)e) return;
            _missao.Value = (byte)e;
            if (e == EtapaDaMissao.Cumprida) PrimeiraMissaoCumprida = true;
            Debug.Log($"[Missão] A primeira missão: {e}.");
        }

        /// <summary>No anfitrião: os números da tela.</summary>
        public void NumerosDaMissao(int zumbis, int pecas, int cacamba)
        {
            if (!IsServer) return;
            byte z = (byte)Mathf.Clamp(zumbis, 0, 255), p = (byte)Mathf.Clamp(pecas, 0, 255), c = (byte)Mathf.Clamp(cacamba, 0, 255);
            if (_zumbisDaMissao.Value != z) _zumbisDaMissao.Value = z;
            if (_pecasQueFaltam.Value != p) _pecasQueFaltam.Value = p;
            if (_naCacamba.Value != c) _naCacamba.Value = c;
        }

        /// <summary>Qualquer um (a conversa com o Capitão): ele vem junto (true) ou espera onde está (false).</summary>
        [Rpc(SendTo.Server)]
        public void EscoltaRpc(bool vem)
        {
            var lw = LakewoodNaCena.Instancia;
            if (lw != null) lw.MudarAEscolta(vem);
        }
    }
}
