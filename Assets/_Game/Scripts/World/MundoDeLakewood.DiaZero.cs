using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Dia 0 no Lakewood (22/11/2026, a etapa 4 do Docs/23-Campanha-e-Mapa.md): o jogo novo começa na casa da família, na
    /// Woodcreek de antes (<see cref="EpocaDoMundo.Antes"/>), com o jornal na TV e as compras; a porta de casa fecha o dia e o
    /// jogo pula para os 5 anos depois, no bunker da comunidade. Aqui, só a marca da rede (o anfitrião escreve); a casa, as
    /// lojas, as pessoas e os carros são do palco (<see cref="LakewoodNaCena"/>, o DiaZero.WoodcreekDoDiaZero), e o lobby,
    /// a carteira, o caixa e a passagem, do DiaZero.DiaZeroDaPartida (o mesmo do mundo sorteado).
    /// </summary>
    public partial class MundoDeLakewood
    {
        /// <summary>
        /// O jogo novo começa pelo Dia 0 (a escolha do menu do novo jogo; o anfitrião lê ao abrir a partida). Um jogo salvo
        /// nunca volta ao Dia 0 (ele não é salvo: a época de antes não grava).
        /// </summary>
        public static bool ComecarPeloDiaZero = true;

        /// <summary>Quanto depois da porta a época muda (s): a tela dos 5 anos já cobriu tudo (ela escurece em 1,2 s).</summary>
        const float EpocaDepoisDaPorta = 1.8f;

        readonly NetworkVariable<bool> _diaZero = new NetworkVariable<bool>();
        float _depoisEm = -1f;

        /// <summary>O Dia 0 está acontecendo no Lakewood.</summary>
        public bool NoDiaZero => IsSpawned && _diaZero.Value;

        // no anfitrião, ao abrir a partida: um jogo novo (sem o estado salvo) com o Dia 0 pedido começa nele
        void TalvezComecarODiaZero()
        {
            var salvo = Salvar.Salvamento.Atual;
            bool novo = salvo == null || !salvo.temEstado;
            if (ComecarPeloDiaZero && novo) ComecarODiaZero();
        }

        /// <summary>No anfitrião (o jogo novo, ou o F1): a Woodcreek volta para antes e o Dia 0 começa.</summary>
        public void ComecarODiaZero()
        {
            if (!IsServer || _diaZero.Value) return;
            _depoisEm = -1f;
            PorAEpoca(EpocaDoMundo.Antes);
            _diaZero.Value = true;
            Debug.Log("[Dia 0] O Dia 0 começou na Woodcreek (a época de antes).");
        }

        /// <summary>
        /// No anfitrião (a porta de casa fechou, DiaZeroDaPartida): o Dia 0 acaba agora, e a época volta para depois logo em
        /// seguida, com a tela dos 5 anos já preta.
        /// </summary>
        public void AcabarODiaZero()
        {
            if (!IsServer || !_diaZero.Value) return;
            _diaZero.Value = false;
            _depoisEm = Time.unscaledTime + EpocaDepoisDaPorta;
        }

        // (no Update da MundoDeLakewood.Troncos.cs) a época de depois, um pouco depois da porta
        void AcompanharOFimDoDiaZero()
        {
            if (_depoisEm < 0f || Time.unscaledTime < _depoisEm) return;
            _depoisEm = -1f;
            if (IsServer) PorAEpoca(EpocaDoMundo.Depois);
        }
    }
}
