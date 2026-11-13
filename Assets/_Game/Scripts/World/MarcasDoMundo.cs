using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As marcas do mapa da partida (os pings, as marcas e as anotações; 03/11/2026, no mundo sorteado), na rede do mundo
    /// da cena: o <see cref="MundoDaPartida"/> ou, desde 12/11/2026 (o Plano_Lakewood, "O jogo do mundo sorteado no
    /// Lakewood", a parte 7), o <see cref="MundoDeLakewood"/>. O mapa, o minimapa e os pedidos dos jogadores leem por aqui.
    /// </summary>
    public interface IMarcasDoMundo
    {
        NetworkList<MarcaNoMapa> Marcas { get; }
        /// <summary>Em cada máquina: uma marca (ou um ping) acabou de entrar na lista.</summary>
        event Action<MarcaNoMapa> MarcaNova;
        /// <summary>O tempo do anfitrião (s), o mesmo em todas as máquinas: o fim dos pings.</summary>
        double Agora { get; }
        bool IsSpawned { get; }
        bool IsServer { get; }
        void PorMarca(TipoDeMarca tipo, Vector3 pos, byte cor, string texto, ulong dono, string autor);
        void ApagarMarca(ushort id);
        void EditarMarca(ushort id, byte cor, string texto);
        void LimparOsPings();
    }

    public static class MarcasDoMundo
    {
        /// <summary>As marcas do mundo da cena (o sorteado ou o Lakewood), ou null.</summary>
        public static IMarcasDoMundo Atual
        {
            get
            {
                var m = MundoDaPartida.Instancia;
                if (m != null) return m;
                return MundoDeLakewood.Instancia;
            }
        }
    }
}
