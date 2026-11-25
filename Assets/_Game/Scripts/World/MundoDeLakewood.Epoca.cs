using Unity.Netcode;

namespace ZombieCityCleaner.World
{
    /// <summary>A época do mundo do Lakewood: depois do apocalipse (o jogo de sempre) ou antes dele (o D0 e a quarentena).</summary>
    public enum EpocaDoMundo : byte
    {
        /// <summary>Os 5 anos depois: as cidades abandonadas, os zumbis, os sobreviventes (o padrão, e o dos saves).</summary>
        Depois = 0,
        /// <summary>Antes do apocalipse: a Woodcreek nova, com as lojas abertas e sem os sistemas do mundo de depois.</summary>
        Antes = 1,
    }

    /// <summary>
    /// A época do Lakewood na rede (22/11/2026, a Woodcreek de antes, a etapa 3 do Docs/23-Campanha-e-Mapa.md): o anfitrião
    /// muda (hoje, pelo F1; na campanha, o D0 começa no antes e a passagem dos 5 anos volta para o depois), e cada máquina
    /// troca o mundo dela (o <see cref="LakewoodNaCena"/>: as obras novas, os gramados, os postes, os materiais limpos, e os
    /// sistemas do mundo de depois desligados). Não vai no save: o jogo salvo é sempre o de depois.
    /// </summary>
    public partial class MundoDeLakewood
    {
        readonly NetworkVariable<byte> _epoca = new NetworkVariable<byte>((byte)EpocaDoMundo.Depois);

        /// <summary>A época da partida (o depois antes de a rede chegar).</summary>
        public EpocaDoMundo Epoca => IsSpawned ? (EpocaDoMundo)_epoca.Value : EpocaDoMundo.Depois;

        /// <summary>A partida está no antes do apocalipse.</summary>
        public bool Antes => Epoca == EpocaDoMundo.Antes;

        /// <summary>No anfitrião: muda a época da partida (todas as máquinas trocam o mundo delas).</summary>
        public void PorAEpoca(EpocaDoMundo epoca)
        {
            if (IsServer && IsSpawned && _epoca.Value != (byte)epoca) _epoca.Value = (byte)epoca;
        }
    }
}
