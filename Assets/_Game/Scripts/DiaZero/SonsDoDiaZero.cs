using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// Os sons do Dia 0, todos gravados (Resources/DiaZero, os do sfxjar): a tosse dos doentes (de homem e de mulher), o
    /// motor do cortador de grama e o dos carros que passam (laços) e a água da mangueira (laço).
    /// (20/11/2026: os feitos em código, provisórios, a reserva dos gravados, saíram; sem a gravação, null.)
    /// </summary>
    public static class SonsDoDiaZero
    {
        /// <summary>Uma tosse gravada, de homem ou de mulher, sorteada (null sem elas; o k não conta mais).</summary>
        public static AudioClip Tosse(bool mulher, int k) => SonsGravados.Um("DiaZero", mulher ? "tosse_mulher_" : "tosse_homem_");   // (20/11/2026, os do sfxjar)

        public static AudioClip Cortador => SonsGravados.Um("DiaZero", "cortador");
        // (20/11/2026: o carro passando gravado tinha ruído e saiu; até achar outro, o motor gravado da perua a 1000 rpm)
        public static AudioClip MotorDoCarro => SonsGravados.Um("DiaZero", "carro_passando") ?? SonsGravados.Um("Carro", "motor_a_2_");
        public static AudioClip Agua => SonsGravados.Um("DiaZero", "mangueira");   // (20/11/2026: os gravados)

        /// <summary>
        /// (23/11/2026, a pedido: "sirenes tocando" na noite do colapso) A sirene de alerta da defesa civil, gravada (o "National
        /// Alert Signal" do sfxjar, CC0 do BigSoundBank), e a mesma um pouco mais aguda: tocam longe, em laço, a noite toda.
        /// </summary>
        public static AudioClip[] Sirenes => SonsGravados.Varios("DiaZero", "sirene_");
    }
}
