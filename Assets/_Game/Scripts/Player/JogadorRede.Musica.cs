using System;
using Unity.Netcode;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O que o jogador está tocando (18/11/2026, a etapa 4 da fogueira: MusicaDoJogador.cs): o instrumento (0: nada; 1 o
    /// violão, 2 o pandeiro, 3 a gaita, 4 a flauta), a música (0 a 2: <see cref="MusicaDoJogador.Musicas"/>) e quando ela
    /// começou no relógio do servidor. Quem toca junto (a até 15 m) usa a mesma música e o mesmo começo: as faixas se
    /// encaixam.
    /// </summary>
    public struct MusicaTocando : INetworkSerializeByMemcpy, IEquatable<MusicaTocando>
    {
        public byte instrumento;
        public byte musica;
        public float inicio;

        public bool Equals(MusicaTocando o) => instrumento == o.instrumento && musica == o.musica && inicio == o.inicio;
    }

    public partial class JogadorRede
    {
        /// <summary>O que ele está tocando (do dono).</summary>
        public readonly NetworkVariable<MusicaTocando> Tocando = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    }
}
