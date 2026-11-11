using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// As armadilhas no zumbi (11/11/2026, a etapa 5a; ZombieManager.Armadilhas.cs): o dano delas (o tapete de pregos, as
    /// estacas) e a lentidão de quem pisou nos pregos (entra no <see cref="FatorDaPedra"/>, que todas as contas da velocidade
    /// usam).
    /// </summary>
    public partial class Zumbi
    {
        float _lentoAte;

        // os pregos no pé: mais devagar até o tempo acabar
        float FatorDosPregos => Time.time < _lentoAte ? ArmadilhasDoJogador.LentidaoDosPregos : 1f;

        /// <summary>Está lento pelos pregos agora (o teste).</summary>
        public bool LentoPelosPregos => Time.time < _lentoAte;

        /// <summary>Só no anfitrião: a armadilha tira da vida do corpo (a casca de pedra protege). True: morreu agora.</summary>
        internal bool FerirNaArmadilha(float dano, float agora)
        {
            if (Morto || dano <= 0f) return false;
            _danoNoCorpo += dano * Mathf.Lerp(1f, _m.ajustes.danoNaPedra, Pedra);
            if (_danoNoCorpo < VidaDoCorpo) return false;
            Morrer(agora, false, transform.position + Vector3.up, -transform.forward, false);
            return true;
        }

        /// <summary>Só no anfitrião: fica lento até o tempo (o relógio do Time.time).</summary>
        internal void FicarLento(float ate)
        {
            _lentoAte = ate;
            AplicarVelocidade();
        }

        /// <summary>Só no anfitrião: a velocidade do agente de novo (as pernas, a pedra e os pregos).</summary>
        internal void AplicarVelocidade()
        {
            if (_velocidadeBase >= 0f && agente.enabled && agente.isOnNavMesh) agente.speed = _velocidadeBase * FatorDasPernas * FatorDaPedra * RitmoPara(_velocidadeBase);
        }
    }
}
