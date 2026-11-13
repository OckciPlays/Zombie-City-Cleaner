using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os sentidos dos bichos (no anfitrião). Cada um tem o seu sorteio a cada decisão, e nada é certo:
    /// - a vista: quem se mexe é visto mais longe; parado, agachado, de noite (sem lanterna) e embaixo das copas, mais perto;
    ///   o cenário tapa a vista;
    /// - o ouvido: os passos (a corrida de longe, o andar agachado só bem perto), abafados pela chuva (os Passos leves contam);
    /// - o faro: pelo vento (o mesmo que balança a mata) — quem está a favor do vento é sentido de bem mais longe;
    /// - os zumbis: vistos (ou bem perto), o bicho foge deles; os zumbis não atacam os bichos (a pedido);
    /// - o tiro (o barulho do ZombieManager): os de perto fogem na hora, os mais longe ficam alerta.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        static readonly int VentoId = Shader.PropertyToID("_ZccVento");

        static Vector2 Vento()
        {
            Vector4 v = Shader.GetGlobalVector(VentoId);
            var d = new Vector2(v.x, v.y);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.right;
        }

        static bool Enxerga(Vector3 de, Vector3 para) => !Physics.Linecast(de, para, 1, QueryTriggerInteraction.Ignore);

        /// <summary>
        /// O perigo mais perto que o bicho notou agora: uma pessoa ('quem'), um zumbi ou um predador ('zumbi' = true: não é
        /// gente). 'semGente': não nota as pessoas (o grupo distraído do F1; os predadores olham a gente à parte).
        /// </summary>
        internal bool Perceber(Bicho b, out Vector3 de, out JogadorRede quem, out bool zumbi, bool semGente = false)
        {
            zumbi = false;
            float melhor = float.MaxValue;
            if (!semGente && PerceberGente(b, out de, out quem)) melhor = Vector3.Distance(b.transform.position, de);
            else
            {
                de = default;
                quem = null;
            }
            var d = b.Dados;
            Vector3 p = b.transform.position;
            Vector3 olho = p + Vector3.up * (b.Altura * 1.25f + 0.1f);
            float noite = CicloDoDia.Noite;
            // os zumbis: fogem deles (os petrificados, as estátuas do dia, não assustam)
            var zm = ZombieManager.Instancia;
            if (zm != null && zm.IsSpawned && zm.IsServer)
            {
                float vistaZ = Mathf.Lerp(24f, 14f, noite);
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto || z.Petrificado) continue;
                    float dist = Vector3.Distance(p, z.transform.position);
                    if (dist > vistaZ || dist >= melhor) continue;
                    if (dist > vistaZ * 0.5f && !Enxerga(olho, z.transform.position + Vector3.up * 1.4f)) continue;
                    melhor = dist;
                    de = z.transform.position;
                    quem = null;
                    zumbi = true;
                }
            }
            // os predadores (a etapa 2): a presa tolera o predador passando longe, mas foge de quem caça (correndo) ou chega
            // perto; o coiote foge do lobo e do urso; o lobo, só do urso bem perto
            foreach (var o in _ativos)
            {
                if (o == b || o.Morto || o.Exposto || !Teme(b, o)) continue;
                float dist = Vector3.Distance(p, o.transform.position);
                float alcance2 = o.Estado == EstadoDoBicho.Investindo ? d.visao : o.Dados.papel == Papel.Urso && b.Dados.papel != Papel.Presa ? 10f : d.fuga * 0.6f;
                if (dist > alcance2 || dist >= melhor) continue;
                if (dist > 8f && !Enxerga(olho, o.transform.position + Vector3.up * o.Altura)) continue;
                melhor = dist;
                de = o.transform.position;
                quem = null;
                zumbi = true;
            }
            return melhor < float.MaxValue;
        }

        /// <summary>A pessoa mais perto que o bicho notou agora (o faro com o vento, o ouvido, a vista), ou nenhuma.</summary>
        internal bool PerceberGente(Bicho b, out Vector3 de, out JogadorRede quem)
        {
            de = default;
            quem = null;
            var d = b.Dados;
            Vector3 p = b.transform.position;
            Vector3 olho = p + Vector3.up * (b.Altura * 1.25f + 0.1f);
            float noite = CicloDoDia.Noite, chuva = Tempo.Ouvido, melhor = float.MaxValue;
            Vector2 vento = Vento();
            float alcance = Mathf.Max(d.visao, Mathf.Max(d.faro, d.ouvido));
            foreach (var g in Pessoas)
            {
                if (!g.valido) continue;
                float dist = Vector3.Distance(p, g.pes);
                if (dist > alcance || dist >= melhor) continue;
                bool notou = false;
                // o faro: o vento levando o cheiro da pessoa até o bicho
                Vector3 aoBicho = p - g.pes;
                aoBicho.y = 0f;
                float aFavor = aoBicho.sqrMagnitude > 0.01f ? Vector2.Dot(vento, new Vector2(aoBicho.x, aoBicho.z).normalized) : 1f;
                float faro = d.faro * (aFavor > 0.3f ? Mathf.Lerp(0.45f, 1f, (aFavor - 0.3f) / 0.7f) : 0.1f);
                if (dist < faro && Random.value < 0.5f) notou = true;
                // o ouvido: os passos
                if (!notou)
                {
                    float passos = 1f - 0.25f * Habilidades.Pontos(g.j, Habilidade.PassosLeves);
                    float ouvido = (g.vel > 3.5f ? d.ouvido : g.vel > 0.6f ? d.ouvido * (g.agachado ? 0.15f : 0.35f) : 0f) * chuva * passos;
                    if (dist < ouvido && Random.value < 0.6f) notou = true;
                }
                // a vista
                if (!notou)
                {
                    float visao = d.visao * (g.vel > 0.5f ? 1f : 0.4f) * (g.agachado ? 0.55f : 1f) * Mathf.Lerp(1f, 0.4f, noite);
                    if (g.lanterna && noite > 0.4f) visao = Mathf.Max(visao, d.visao * 0.8f);
                    if (ArvoresDoMundo.SobAsCopas(g.pes)) visao *= 0.7f;
                    if (dist < visao && Random.value < 0.25f + 0.75f * (1f - dist / visao)
                        && Enxerga(olho, g.pes + Vector3.up * (g.agachado ? 0.9f : 1.4f))) notou = true;
                }
                if (!notou) continue;
                melhor = dist;
                de = g.pes;
                quem = g.j;
            }
            return quem != null;
        }

        /// <summary>O bicho 'b' foge do 'o'? (a presa de todo predador; o coiote do lobo e do urso; o lobo do urso)</summary>
        internal static bool Teme(Bicho b, Bicho o)
        {
            var pb = b.Dados.papel;
            var po = o.Dados.papel;
            if (po == Papel.Presa) return false;
            if (pb == Papel.Presa) return true;
            if (pb == Papel.Matilha && po == Papel.Urso) return true;
            return b.Especie == Especie.Coiote && o.Especie == Especie.Lobo;
        }

        // o barulho (o tiro, as árvores caindo, as armadilhas): os bichos ouvem o dobro do alcance dos zumbis
        void AoBarulho(Vector3 ponto, float raio)
        {
            if (!_servidor) return;
            float agora = Time.time;
            foreach (var b in _ativos) b.OuvirTiro(ponto, raio * 2f, agora);
        }
    }
}
