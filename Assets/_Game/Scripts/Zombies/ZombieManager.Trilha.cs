using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>Os raios (m) da <see cref="ZombieManager.Ameaca"/>: até onde cada tipo de zumbi conta para a trilha.</summary>
    public struct RaiosDaTrilha
    {
        /// <summary>caca: quem te caça conta até aqui. brigaJunto: quem persegue ou ataca qualquer um, até aqui, briga junto com você.</summary>
        public float caca, brigaJunto;
        /// <summary>outros: quem caça outro (um amigo, um sobrevivente) conta até aqui, como tensão.</summary>
        public float outros;
        /// <summary>tensao: quem investiga ou procura conta até aqui. colada: até aqui, ele está colado em você.</summary>
        public float tensao, colada;
    }

    /// <summary>Os zumbis em volta de um ponto, para a trilha (o que a <see cref="ZombieManager.Ameaca"/> devolve).</summary>
    public struct AmeacaEmVolta
    {
        /// <summary>Te caçando (e, entre eles, atacando): os de <see cref="Zumbi.Caca"/> e os que brigam junto.</summary>
        public int cacando, atacando;
        /// <summary>Investigando ou procurando perto, e quantos deles estão colados.</summary>
        public int tensao, colados;
        /// <summary>Caçando outro alvo, um pouco mais longe que a briga junto.</summary>
        public int outros;
        /// <summary>A distância do caçador mais perto (m); <see cref="float.MaxValue"/> quando ninguém te caça.</summary>
        public float maisPerto;
    }

    /// <summary>
    /// O que a trilha sonora (Music/MaestroDaTrilha) precisa saber dos zumbis em volta de quem joga. Desde 04/11/2026 (a
    /// pedido: os amigos acharam a música de luta "muito cansativa, pois toda hora a música entra"), a caça conta só quem
    /// caça VOCÊ: no anfitrião, pelo alvo de cada zumbi (<see cref="Zumbi.Caca"/>); nos outros jogadores, pelo bit "te
    /// caça" que vem do anfitrião com cada cópia (<see cref="Zumbi.CacaQuemRecebe"/>, ZombieManager.Rede.cs). Antes, nas
    /// cópias, qualquer zumbi se esquivando da lanterna contava como caça: passar a luz num zumbi andando dava a
    /// perseguição e o "te viram" só nos amigos. Um zumbi perseguindo ou atacando bem perto (o grupo brigando junto) também
    /// conta, seja quem for o alvo dele; um pouco mais longe, ele é só tensão (os "outros"). Cada jogador calcula a sua
    /// música sozinho; o único dado novo na rede é aquele bit.
    /// </summary>
    public partial class ZombieManager
    {
        /// <summary>
        /// Os zumbis em volta deste ponto, para <paramref name="eu"/> (o jogador desta máquina). Por zumbi, na ordem:
        /// <list type="bullet">
        /// <item>te caça a até <c>r.caca</c>, ou persegue ou ataca qualquer um a até <c>r.brigaJunto</c>: é caçador (entra em
        /// <paramref name="cacadores"/>, para a trilha ver depois se ele virou pedra), e conta em <c>cacando</c>, em
        /// <c>atacando</c> se ataca e no <c>maisPerto</c>;</item>
        /// <item>senão, persegue ou ataca outro a até <c>r.outros</c>: <c>outros</c>;</item>
        /// <item>senão, investiga ou procura a até <c>r.tensao</c>: <c>tensao</c>, e <c>colados</c> se a até <c>r.colada</c>.</item>
        /// </list>
        /// A esquiva da luz de um zumbi que te caça já entra pelo "te caça" (no anfitrião, ele está engajado e o alvo é você).
        /// </summary>
        public void Ameaca(Vector3 ponto, IAlvoDeZumbi eu, in RaiosDaTrilha r, List<Zumbi> cacadores, out AmeacaEmVolta a)
        {
            a = default;
            cacadores?.Clear();
            bool servidor = IsServer;
            var lista = servidor ? _ativos : _copias;
            float caca2 = r.caca * r.caca, junto2 = r.brigaJunto * r.brigaJunto, outros2 = r.outros * r.outros;
            float tensao2 = r.tensao * r.tensao, colada2 = r.colada * r.colada, perto2 = float.MaxValue;
            for (int i = 0; i < lista.Count; i++)
            {
                var z = lista[i];
                if (z == null) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                var estado = z.Atual;
                // no anfitrião, o alvo de verdade; na cópia, o bit que o anfitrião mandou para esta máquina
                bool meu = servidor ? z.Caca(eu) : z.CacaQuemRecebe;
                bool brigando = estado == Zumbi.Estado.Perseguindo || estado == Zumbi.Estado.Atacando;
                if ((meu && d2 <= caca2) || (brigando && d2 <= junto2))
                {
                    cacadores?.Add(z);
                    a.cacando++;
                    if (estado == Zumbi.Estado.Atacando) a.atacando++;
                    if (d2 < perto2) perto2 = d2;
                }
                else if (brigando && d2 <= outros2) a.outros++;
                else if ((estado == Zumbi.Estado.Investigando || estado == Zumbi.Estado.Procurando) && d2 <= tensao2)
                {
                    a.tensao++;
                    if (d2 <= colada2) a.colados++;
                }
            }
            a.maisPerto = a.cacando > 0 ? Mathf.Sqrt(perto2) : float.MaxValue;
        }
    }
}
