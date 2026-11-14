using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os bichos nos lugares deles (a etapa 5, 13/11/2026; os lugares em FaunaDoMundo.Lugares.cs), no anfitrião:
    /// - o urso da caverna dorme lá dentro de dia (das 9h às 18h) e de noite (das 22h às 6h; sai de manhã cedo e no fim da
    ///   tarde, como o urso de verdade): entra pela boca e
    ///   pelo túnel até o salão e deita (os filhotes junto); no resto do tempo sai e anda por perto (até ~250 m dela). Quem
    ///   entra com ele lá dentro acorda o urso (a surpresa de perto: Bicho.Urso.cs);
    /// - os coiotes da toca descansam deitados em volta dela de dia (das 8h às 18h) e saem para caçar no fim da tarde e à
    ///   noite (o resto da mente da matilha).
    /// </summary>
    public sealed partial class Bicho
    {
        /// <summary>Deita até o tempo dado (o bicho que nasce dormindo no lugar dele).</summary>
        internal void DeitarAte(float ate)
        {
            Estado = EstadoDoBicho.Deitado;
            _deitadoAte = ate;
            _temDestino = false;
            _velAlvo = 0f;
        }

        static bool HoraDaCaverna()
        {
            float h = CicloDoDia.Hora;
            return (h >= 9f && h < 18f) || h >= 22f || h < 6f;
        }

        /// <summary>O urso da caverna: entra para dormir (nas horas dela) ou sai (fora delas). Verdadeiro: cuidou do urso agora.</summary>
        bool CuidarDaCaverna(FaunaDoMundo f, float agora)
        {
            var g = Grupo;
            if (g == null || !g.temLugar || Variante == Variante.Filhote) return false;
            var lugares = FaunaDoMundo.Lugares;
            if (g.lugarIndice < 0 || g.lugarIndice >= lugares.Count || lugares[g.lugarIndice].tipo != TipoDeLugarDoBicho.Caverna) return false;
            var l = lugares[g.lugarIndice];
            Vector3 p = transform.position;
            Vector3 loc = l.NoLugar(p);
            bool dentro = loc.z > 0.3f && loc.z < 15.5f && Mathf.Abs(loc.x) < 4.7f;
            bool querDentro = HoraDaCaverna() && Vector3.Distance(Plano(p), Plano(l.pos)) < 400f;
            Vector3 fora = l.NoMundo(new Vector3(0f, 0f, -5f)), boca = l.NoMundo(new Vector3(0f, 0f, 0.8f)), tunel = l.NoMundo(new Vector3(0f, 0f, 3.5f));
            Vector3 cama = l.NoMundo(new Vector3((LugarNoGrupo.x >= 0f ? 1f : -1f) * 1.2f, 0f, 10.5f));
            Vector3 alvo;
            if (querDentro)
            {
                if (!dentro)
                {
                    // fora: primeiro na frente da boca, alinhado; depois para dentro
                    bool alinhado = loc.z < 0.5f && loc.z > -7f && Mathf.Abs(loc.x) < 1.4f;
                    alvo = alinhado ? tunel : fora;
                }
                else if (loc.z < 5.3f) alvo = cama;
                else if (Vector3.Distance(Plano(p), Plano(cama)) > 0.9f) alvo = cama;
                else
                {
                    // na cama: dorme (os filhotes junto)
                    DeitarAte(agora + Random.Range(90f, 240f));
                    foreach (var b in g.membros)
                        if (b != null && !b.Morto && b != this && b.Variante == Variante.Filhote) b.DeitarAte(_deitadoAte);
                    return true;
                }
                g.pasto = cama;
            }
            else
            {
                if (!dentro) return false;
                // saindo: pelo túnel até a frente da boca
                alvo = loc.z > 5.3f ? tunel : fora;
                g.pasto = fora;
            }
            Estado = EstadoDoBicho.Andando;
            Ir(alvo, Dados.andar);
            return true;
        }

        /// <summary>Os coiotes da toca: de dia, deitados em volta dela. Verdadeiro: cuidou do coiote agora.</summary>
        bool CuidarDoCovil(FaunaDoMundo f, float agora)
        {
            var g = Grupo;
            if (g == null || !g.temLugar) return false;
            float h = CicloDoDia.Hora;
            if (h < 8f || h >= 18f) return false;
            var lugares = FaunaDoMundo.Lugares;
            if (g.lugarIndice < 0 || g.lugarIndice >= lugares.Count || lugares[g.lugarIndice].tipo != TipoDeLugarDoBicho.Covil) return false;
            var l = lugares[g.lugarIndice];
            if (Vector3.Distance(Plano(transform.position), Plano(l.pos)) > 400f) return false;
            Vector3 lugar = l.NoMundo(new Vector3(LugarNoGrupo.x * 0.9f, 0f, -3f + LugarNoGrupo.z * 0.6f - 1.5f));
            float d = Vector3.Distance(Plano(transform.position), Plano(lugar));
            if (d > 1.5f)
            {
                Estado = EstadoDoBicho.Andando;
                Ir(lugar, d > 30f ? Dados.trotar : Dados.andar);
                return true;
            }
            DeitarAte(agora + Random.Range(60f, 180f));
            return true;
        }

        /// <summary>O pasto novo de quem tem lugar fica perto dele (a até 250 m).</summary>
        static Vector3 PertoDoLugar(Grupo g, Vector3 pasto)
        {
            if (g == null || !g.temLugar) return pasto;
            Vector3 de = g.lugar;
            Vector3 v = pasto - de;
            v.y = 0f;
            return v.magnitude > 250f ? de + v.normalized * 200f : pasto;
        }
    }
}
