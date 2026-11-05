using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que o golpe de cada zumbi faz no corpo do jogador (05/11/2026, o sistema de vida por membro): o Oco e o Corredor
    /// mordem e arranham (20% de abrir uma ferida que sangra; o Corredor pega mais o tronco e os braços, o Oco rastejando
    /// pega as pernas), e o Brutamonte esmaga (nunca faz sangrar, a pedido: "O brutamonte não causará sangramentos, causará
    /// fraturas. Principalmente no seu ataque ao chão"): o avanço quebra um braço ou uma perna às vezes, e o golpe no chão
    /// pega as pernas, com a chance da fratura pela força que chegou (perto e no 3º golpe, quase certa). Quem bateu é o
    /// zumbi que estava pensando ou andando quando o golpe saiu (<see cref="_quemAge"/>, posto em volta dos dois laços do
    /// ZombieManager.cs), ou o mais perto do ponto do golpe.
    /// </summary>
    public partial class ZombieManager
    {
        // o zumbi que está agindo agora (dentro do Pensar e do QuadroNoServidor): quem golpeia, se golpear
        Zumbi _quemAge;

        // a força que pede a fratura no golpe no chão (o dano que chegou: 40 × a força do golpe × a distância)
        const float GolpeForte = 34f, GolpeMedio = 20f;

        void GolpearJogador(JogadorRede j, float dano, Vector3 de)
        {
            var z = _quemAge;
            if (z == null || z.Morto || (z.transform.position - de).sqrMagnitude > 3f * 3f) z = ZumbiPerto(de);
            var tipo = TipoDeGolpe.Mordida;
            var pesos = PartesDoCorpo.Pesos.Mordida;
            string causa = "Mordido pelos zumbis.";
            bool bruto = z != null && z.tipo == TipoDeZumbi.Brutamonte;
            bool noChao = false;
            if (bruto)
            {
                tipo = TipoDeGolpe.Esmagamento;
                causa = "Esmagado pelo Brutamonte.";
                noChao = z.FaseBruto == Zumbi.FaseDoBruto.Furia;
                pesos = noChao ? PartesDoCorpo.Pesos.Chao : PartesDoCorpo.Pesos.Avanco;
            }
            else if (z != null && z.tipo == TipoDeZumbi.Corredor) pesos = PartesDoCorpo.Pesos.Corredor;
            else if (z != null && (z.Pernas == 4 || z.FaseDoEquilibrio == Zumbi.Fase.Rastejando)) pesos = PartesDoCorpo.Pesos.Rastejante;
            var parte = PartesDoCorpo.SortearParte(pesos, j.transform, de);
            var lesao = Lesao.Nenhuma;
            if (bruto && PartesDoCorpo.EhMembro(parte))
            {
                float r = Random.value;
                if (noChao)
                {
                    if (dano >= GolpeForte) lesao = r < 0.7f ? Lesao.Fratura : r < 0.9f ? Lesao.Entorse : Lesao.Nenhuma;
                    else if (dano >= GolpeMedio) lesao = r < 0.35f ? Lesao.Fratura : r < 0.7f ? Lesao.Entorse : Lesao.Nenhuma;
                    else if (r < 0.3f) lesao = Lesao.Entorse;
                    // a entorse é do tornozelo: só na perna
                    if (lesao == Lesao.Entorse && !PartesDoCorpo.EhPerna(parte)) lesao = Lesao.Nenhuma;
                }
                else if (r < 0.35f) lesao = Lesao.Fratura;   // o avanço: o braço ou a perna que levou o tranco
            }
            j.Vida.Dano(dano, de, causa, tipo, parte, lesao);
        }

        // o zumbi vivo mais perto do ponto do golpe (o golpe que saiu fora dos dois laços)
        Zumbi ZumbiPerto(Vector3 de)
        {
            Zumbi melhor = null;
            float d2 = 3f * 3f;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z == null || z.Morto) continue;
                float d = (z.transform.position - de).sqrMagnitude;
                if (d < d2)
                {
                    d2 = d;
                    melhor = z;
                }
            }
            return melhor;
        }
    }
}
