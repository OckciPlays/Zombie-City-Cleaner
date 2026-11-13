using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O peixe cru que cai mal (07/11/2026, a pedido: "comer cru pode fazer mal"; o InventarioJogador.ConsumirRpc sorteia,
    /// <see cref="ChanceDoPeixeCru"/>): no anfitrião, o enjoo tira <see cref="VidaDoEnjoo"/> de vida (do tronco) aos
    /// poucos, em <see cref="SegundosDoEnjoo"/> s, e o dono é avisado. Assado na fogueira (a receita "perto do fogo"), não.
    /// </summary>
    public partial class VidaJogador
    {
        public const float ChanceDoPeixeCru = 0.4f, SegundosDoEnjoo = 45f, VidaDoEnjoo = 12f;

        float _enjooFalta, _enjooPerda;
        string _causaDoEnjoo = "Passou mal com o peixe cru.";

        /// <summary>Só no anfitrião: o peixe cru caiu mal (um enjoo novo recomeça o tempo).</summary>
        public void EnjoarDoPeixeCru() =>
            Enjoar("O peixe cru caiu mal: enjoo (asse na fogueira da próxima vez)", "Passou mal com o peixe cru.");

        /// <summary>Só no anfitrião: a carne de caça crua caiu mal (a fauna, 12/11/2026; a mesma chance do peixe cru).</summary>
        public void EnjoarDaCarneCrua() =>
            Enjoar("A carne crua caiu mal: enjoo (asse na fogueira da próxima vez)", "Passou mal com a carne crua.");

        void Enjoar(string aviso, string causa)
        {
            if (!IsServer || Caido.Value) return;
            _enjooFalta = SegundosDoEnjoo;
            _causaDoEnjoo = causa;
            AvisarAoDono(aviso);
        }

        // no anfitrião, a cada quadro: a vida sai de 1 em 1, até o enjoo passar
        void AtualizarEnjoo(float dt)
        {
            if (_enjooFalta <= 0f) return;
            if (Caido.Value)
            {
                _enjooFalta = 0f;
                return;
            }
            _enjooFalta -= dt;
            _enjooPerda += VidaDoEnjoo / SegundosDoEnjoo * dt;
            if (_enjooPerda < 1f) return;
            float d = Mathf.Floor(_enjooPerda);
            _enjooPerda -= d;
            Perder(d, _causaDoEnjoo);
        }
    }
}
