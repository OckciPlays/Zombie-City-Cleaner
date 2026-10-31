using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que a trilha sonora (Music/MaestroDaTrilha) precisa saber dos zumbis: quantos em volta de um ponto estão caçando
    /// (perseguindo, atacando, se esquivando da luz no meio da caça) e quantos estão investigando ou procurando (a
    /// tensão). Vale no anfitrião e nos outros jogadores (pelas cópias, que recebem o estado de cada zumbi), então cada
    /// jogador calcula a sua música sozinho, sem passar nada pela rede.
    /// </summary>
    public partial class ZombieManager
    {
        /// <summary>
        /// Os zumbis em volta deste ponto: caçando até <paramref name="raioDaCaca"/> (e quem são, em
        /// <paramref name="cacadores"/>, para a trilha ver depois se viraram pedra), atacando (já entre eles) e investigando
        /// ou procurando até <paramref name="raioDaTensao"/>.
        /// </summary>
        public void Ameaca(Vector3 ponto, float raioDaCaca, float raioDaTensao, List<Zumbi> cacadores, out int atacando, out int tensao)
        {
            atacando = tensao = 0;
            cacadores?.Clear();
            var lista = IsServer ? _ativos : _copias;
            float caca2 = raioDaCaca * raioDaCaca, tensao2 = raioDaTensao * raioDaTensao;
            for (int i = 0; i < lista.Count; i++)
            {
                var z = lista[i];
                if (z == null) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                switch (z.Atual)
                {
                    case Zumbi.Estado.Perseguindo:
                    case Zumbi.Estado.Atacando:
                    case Zumbi.Estado.Esquivando when z.Engajado || Copia(z):
                        if (d2 > caca2) break;
                        cacadores?.Add(z);
                        if (z.Atual == Zumbi.Estado.Atacando) atacando++;
                        break;
                    case Zumbi.Estado.Investigando:
                    case Zumbi.Estado.Procurando:
                        if (d2 <= tensao2) tensao++;
                        break;
                }
            }
        }

        // nas cópias não se sabe se a esquiva é no meio de uma caça: conta como caça (quem se esquiva da lanterna está perto)
        static bool Copia(Zumbi z) => z.Copia;
    }
}
