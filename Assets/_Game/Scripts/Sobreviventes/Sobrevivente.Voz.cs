using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// A voz do sobrevivente (19/11/2026, Combat/VozDoPersonagem): um dos dois homens, pela semente. O golpe quando é
    /// ferido, a dor quando morre, o grito do ataque quando golpeia e o "hum" quando fala.
    /// </summary>
    public partial class Sobrevivente
    {
        float _proximaVozNoServidor;

        int PessoaDaVoz => VozDoPersonagem.DoNpc(Identidade.Value.semente);

        // no anfitrião: a voz para todos (no máximo uma a cada 0,4 s; a dor sempre)
        void VozDoNpc(TipoDeVoz v)
        {
            if (!IsServer || !IsSpawned) return;
            if (v != TipoDeVoz.Dor && Time.time < _proximaVozNoServidor) return;
            _proximaVozNoServidor = Time.time + 0.4f;
            VozRpc((byte)v);
        }

        [Rpc(SendTo.Everyone)]
        void VozRpc(byte v) => VozLocal((TipoDeVoz)v);

        /// <summary>A voz dele aqui (nesta máquina).</summary>
        internal void VozLocal(TipoDeVoz v, float volume = 1f) =>
            VozDoPersonagem.Tocar(this, PessoaDaVoz, v, transform.position + Vector3.up * 1.6f, true, volume);
    }
}
