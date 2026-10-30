using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O som abafado (a pedido em 29/10/2026: "quando eu subo em cima de um prédio, o som deles embaixo parece que está
    /// vindo de cima"): com parede, laje, o telhado ou o chão entre quem escuta e o som, ele chega abafado (sem os agudos)
    /// e mais baixo. Sem isso, o som que vem de baixo de um prédio chegava limpo e perto, como se estivesse ali em cima. Usado
    /// pelas vozes dos zumbis (Zumbi.Voz.cs) e pelos sons soltos (<see cref="Efeitos.Som(AudioClip, Vector3, float, bool, float)"/>).
    /// </summary>
    public static class Abafamento
    {
        /// <summary>O que tapa o som: o cenário (Default e TransparentFX: o chão, as construções, os carros abandonados).</summary>
        public const int Mascara = (1 << 0) | (1 << 1);
        /// <summary>O som tapado: o corte dos agudos (Hz) e o volume.</summary>
        public const float CorteTapado = 900f, VolumeTapado = 0.55f;

        /// <summary>Onde está o ouvido de quem joga (a câmera principal).</summary>
        public static bool Ouvido(out Vector3 ouvido)
        {
            var cam = Camera.main;
            ouvido = cam != null ? cam.transform.position : default;
            return cam != null;
        }

        /// <summary>Tem alguma coisa entre o ouvido e o som (de 3 m para lá)? Um raio só (o povoamento usa para "ninguém vê").</summary>
        public static bool Tapado(Vector3 ouvido, Vector3 som) =>
            (som - ouvido).sqrMagnitude > 9f && Physics.Linecast(ouvido, som, Mascara, QueryTriggerInteraction.Ignore);

        /// <summary>
        /// Quanto o som está tapado, de 0 a 1: três raios (ao som, 0,8 m ao lado e 0,9 m acima). Uma parede, uma laje ou o
        /// telhado tapam os três; um poste ou um tronco fino, só um (o som contorna o que é fino).
        /// </summary>
        public static float Quanto(Vector3 ouvido, Vector3 som)
        {
            Vector3 d = som - ouvido;
            if (d.sqrMagnitude <= 9f) return 0f;
            Vector3 lado = Vector3.Cross(d, Vector3.up);
            lado = lado.sqrMagnitude > 1e-4f ? lado.normalized * 0.8f : Vector3.right * 0.8f;
            int n = 0;
            if (Physics.Linecast(ouvido, som, Mascara, QueryTriggerInteraction.Ignore)) n++;
            if (Physics.Linecast(ouvido, som + lado, Mascara, QueryTriggerInteraction.Ignore)) n++;
            if (Physics.Linecast(ouvido, som + Vector3.up * 0.9f, Mascara, QueryTriggerInteraction.Ignore)) n++;
            return n / 3f;
        }

        /// <summary>O corte dos agudos para 'quanto' tapado (0 limpo, 1 tapado): de 22 kHz a <see cref="CorteTapado"/>.</summary>
        public static float Corte(float quanto) => 22000f * Mathf.Pow(CorteTapado / 22000f, Mathf.Clamp01(quanto));
    }
}
