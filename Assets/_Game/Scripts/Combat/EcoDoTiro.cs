using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O eco do tiro pelo lugar (19/11/2026, a pedido do Pedro: o Weapons of Choice "pra tiros na rua, dentro de
    /// construções, ecos"). Depois de cada tiro (do jogador, dos outros e dos NPCs), a cauda do lugar onde ele foi dado:
    ///  - debaixo de um teto: a sala (as paredes perto, em média a menos de 6 m) ou o corredor (o salão, o galpão);
    ///  - fora, com 3 paredes ou mais em volta (construções a menos de 30 m): a rua;
    ///  - fora, com os morros em volta (o chão subindo à frente em 4 rumos ou mais): a montanha;
    ///  - o resto: o campo aberto.
    /// O silenciador quase apaga o eco, e a rajada (o mesmo tipo de arma a menos de 0,2 s) põe cada eco na metade.
    /// </summary>
    public static class EcoDoTiro
    {
        /// <summary>O volume dos ecos (o F1 pode mudar).</summary>
        public static float Volume = 0.6f;
        /// <summary>O último lugar achado (o F1 e o teste).</summary>
        public static string UltimoLugar { get; private set; } = "";

        static readonly Dictionary<TipoDeArma, float> _ultimo = new Dictionary<TipoDeArma, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => _ultimo.Clear();

        /// <summary>O lugar do tiro, pelo que está em volta do ponto: aberto, rua, montanha, sala ou corredor.</summary>
        public static string Lugar(Vector3 p)
        {
            int mascara = Abafamento.Mascara;
            bool coberto = Physics.Raycast(p, Vector3.up, 15f, mascara, QueryTriggerInteraction.Ignore);
            float soma = 0f;
            int paredes = 0, morros = 0;
            for (int i = 0; i < 8; i++)
            {
                var rumo = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float d = 30f;
                if (Physics.Raycast(p, rumo, out var h, 30f, mascara, QueryTriggerInteraction.Ignore))
                {
                    d = h.distance;
                    if (Player.PassosJogador.EhTerreno(h.collider)) morros++;
                    else
                    {
                        var b = h.collider.bounds.size;
                        if (Mathf.Max(b.x, b.z) > 3f && b.y > 2f) paredes++;   // uma parede (o tronco de uma árvore é fino)
                    }
                }
                soma += d;
                // o relevo mais longe: o chão subindo à frente (um raio 6° acima do horizonte, até 250 m)
                var acima = (rumo * Mathf.Cos(6f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(6f * Mathf.Deg2Rad)).normalized;
                if (Physics.Raycast(p + Vector3.up * 0.5f, acima, out var m, 250f, mascara, QueryTriggerInteraction.Ignore)
                    && Player.PassosJogador.EhTerreno(m.collider)) morros++;
            }
            if (coberto) return soma / 8f < 6f ? "sala" : "corredor";
            if (paredes >= 3) return "rua";
            if (morros >= 4) return "montanha";
            return "aberto";
        }

        /// <summary>O eco do tiro da arma no ponto (o volume do tiro: o silenciador baixa).</summary>
        public static void Tocar(TipoDeArma arma, Vector3 onde, float volume, bool espacial, float alcance)
        {
            if (Volume <= 0f || volume <= 0f) return;
            string lugar = Lugar(onde + Vector3.up * 0.1f);
            UltimoLugar = lugar;
            var ecos = SonsGravados.Eco(arma, lugar) ?? SonsGravados.Eco(arma, "aberto");
            if (ecos == null) return;
            float v = Volume * volume * volume;
            if (_ultimo.TryGetValue(arma, out var antes) && Time.time - antes < 0.2f) v *= 0.5f;   // a rajada
            _ultimo[arma] = Time.time;
            Efeitos.Som(ecos, onde, v, espacial, espacial ? Mathf.Max(60f, alcance * 1.6f) : 60f);
        }
    }
}
