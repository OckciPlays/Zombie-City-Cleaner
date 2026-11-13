using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os sons dos bichos na rede (a etapa 2, 12/11/2026): o anfitrião toca o som (SomDosBichos) e manda a quem está no
    /// alcance dele (o uivo vai longe) uma mensagem curta, "ZCC.FaunaSom": o som e o ponto. Não é confiável: um som perdido
    /// não faz falta.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        const string NomeDoSom = "ZCC.FaunaSom";

        internal void TocarSom(Bicho b, SomDoBicho som)
        {
            if (!_servidor || b == null) return;
            Vector3 p = b.transform.position + Vector3.up * b.Altura;
            SomDosBichos.Tocar(som, p);
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            float alcance = SomDosBichos.Alcance(som);
            foreach (var g in Pessoas)
            {
                ulong cliente = g.j.OwnerClientId;
                if (cliente == NetworkManager.ServerClientId) continue;
                if ((g.pes - p).sqrMagnitude > alcance * alcance) continue;
                using var w = new FastBufferWriter(1 + 3 * sizeof(float), Allocator.Temp);
                w.WriteValueSafe((byte)som);
                w.WriteValueSafe(p);
                nm.CustomMessagingManager.SendNamedMessage(NomeDoSom, cliente, w, NetworkDelivery.Unreliable);
            }
        }

        void AoReceberSom(ulong remetente, FastBufferReader r)
        {
            r.ReadValueSafe(out byte som);
            r.ReadValueSafe(out Vector3 p);
            if (som < System.Enum.GetValues(typeof(SomDoBicho)).Length) SomDosBichos.Tocar((SomDoBicho)som, p);
        }
    }
}
