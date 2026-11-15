using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O ácido do Longo (15/11/2026, a pedido: "o mesmo efeito do long + o dano ácido e envenenamento"; Zumbi.Chefes.cs e
    /// ZombieManager.Gosma.cs). A gosma dele prende e apaga a lanterna como a do Long; além disso:
    /// <list type="bullet">
    /// <item>em cheio, o dano na hora (no tronco) e o veneno;</item>
    /// <item>na poça, a cada segundo, a queima numa perna e o veneno renovado pela metade.</item>
    /// </list>
    /// O veneno é o mesmo da cascavel (o símbolo "Envenenado", o membro perdendo vida aos poucos, a dor na mente), com a causa
    /// do ácido; se a cascavel já está agindo, ela continua (o ácido só tira a vida na hora). Só no anfitrião.
    /// </summary>
    public partial class VidaJogador
    {
        public const string CausaDoAcido = "Corroído pelo ácido do Longo.";

        /// <summary>Só no anfitrião: o ácido em cheio (o dano na hora e o veneno por <paramref name="segundosDeVeneno"/> s).</summary>
        public void Acido(float dano, Vector3 de, float segundosDeVeneno, float venenoTotal)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return;
            if (dano > 0f) Dano(dano, de, CausaDoAcido);
            if (Caido.Value) return;
            if (EnvenenarPeloAcido(ParteDoCorpo.Tronco, segundosDeVeneno, venenoTotal))
                AvisarAoDono("O ácido do Longo: queima e envenena. Saia da poça!");
        }

        /// <summary>Só no anfitrião, a cada segundo na poça de ácido: a queima numa perna e o veneno (renovado).</summary>
        public void QueimarNoAcido(float dano, float segundosDeVeneno, float venenoTotal)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return;
            var perna = Random.value < 0.5f ? ParteDoCorpo.PernaE : ParteDoCorpo.PernaD;
            if (dano > 0f)
            {
                PorNaParte(perna, EmPorcento(dano));
                ConferirAVida(CausaDoAcido);
                if (Caido.Value) return;
            }
            if (EnvenenarPeloAcido(perna, segundosDeVeneno, venenoTotal)) AvisarAoDono("A poça de ácido queima os pés: saia dela!");
        }

        /// <summary>O veneno do ácido (true: começou agora). Com a cascavel agindo, nada muda.</summary>
        bool EnvenenarPeloAcido(ParteDoCorpo parte, float segundos, float total)
        {
            if (segundos <= 0f || total <= 0f) return false;
            if (Veneno.Value > 0f && !_venenoDoAcido) return false;   // (a cascavel já está agindo)
            bool novo = Veneno.Value <= 0f;
            _venenoDoAcido = true;
            _parteDoVeneno = parte;
            float porSegundo = total / Mathf.Max(1f, segundos);
            _venenoPorSegundo = novo ? porSegundo : Mathf.Max(_venenoPorSegundo, porSegundo);
            Veneno.Value = Mathf.Max(Veneno.Value, segundos);
            if (novo)
            {
                _proximaDorDoVeneno = Time.time + 15f;
                AbalarAgora(4f, MotivoDaMente.Dor);
            }
            return novo;
        }
    }
}
