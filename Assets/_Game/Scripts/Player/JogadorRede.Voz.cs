using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A voz do jogador (19/11/2026, Combat/VozDoPersonagem): o sexo da aparência e uma das duas vozes, pelo nome. O dono
    /// ouve a dele na cabeça; os outros, no corpo dele.
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>A pessoa da voz deste jogador (0 e 1 os homens, 2 e 3 as mulheres).</summary>
        public int PessoaDaVoz => VozDoPersonagem.DoJogador(Aparencia.Value.EhMulher, Aparencia.Value.nome.ToString());

        /// <summary>No dono: a voz dele aqui e nas outras máquinas.</summary>
        public void Voz(TipoDeVoz v, float volume = 1f)
        {
            if (!IsSpawned || !IsOwner) return;
            if (VozDoPersonagem.Tocar(this, PessoaDaVoz, v, transform.position + Vector3.up * 1.6f, false, volume * 0.8f))
                VozRpc((byte)v, volume);
        }

        [Rpc(SendTo.NotMe)]
        void VozRpc(byte v, float volume) =>
            VozDoPersonagem.Tocar(this, PessoaDaVoz, (TipoDeVoz)v, transform.position + Vector3.up * 1.6f, true, volume);
    }
}
