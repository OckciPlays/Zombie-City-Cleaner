using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que a mente do jogador (06/11/2026, a sanidade: Player/VidaJogador.Mente.cs) precisa saber dos zumbis, no anfitrião:
    /// quem está caçando perto (perseguindo ou atacando), quantos investigam ou procuram em volta e se um Brutamonte está
    /// saindo da terra ali. Separado da conta da trilha (ZombieManager.Trilha.cs), que é de cada máquina.
    /// </summary>
    public partial class ZombieManager
    {
        /// <summary>
        /// No anfitrião: os zumbis caçando até <paramref name="raioDaCaca"/> deste ponto (em <paramref name="cacando"/>),
        /// quantos investigam ou procuram até <paramref name="raioDaTensao"/>, quantos estão só por perto (qualquer um vivo e
        /// solto, parado, vagando ou se abrigando, até <paramref name="raioDePerto"/>; a pedra não conta) e se um Brutamonte sai
        /// da terra a até 14 m.
        /// </summary>
        public void PerigoParaAMente(Vector3 ponto, float raioDaCaca, float raioDaTensao, float raioDePerto, HashSet<Zumbi> cacando,
                                     out int tensao, out int perto, out bool brutoSaindo)
        {
            cacando.Clear();
            tensao = 0;
            perto = 0;
            brutoSaindo = false;
            if (!IsServer) return;
            float caca2 = raioDaCaca * raioDaCaca, tensao2 = raioDaTensao * raioDaTensao, perto2 = raioDePerto * raioDePerto;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z == null || z.Morto) continue;
                float d2 = (z.transform.position - ponto).sqrMagnitude;
                if (z.tipo == TipoDeZumbi.Brutamonte && z.FaseBruto == Zumbi.FaseDoBruto.Emergindo && d2 < 14f * 14f) brutoSaindo = true;
                if (d2 <= perto2 && !z.Petrificado) perto++;
                switch (z.Atual)
                {
                    case Zumbi.Estado.Perseguindo:
                    case Zumbi.Estado.Atacando:
                        if (d2 <= caca2) cacando.Add(z);
                        break;
                    case Zumbi.Estado.Investigando:
                    case Zumbi.Estado.Procurando:
                        if (d2 <= tensao2) tensao++;
                        break;
                }
            }
        }
    }
}
