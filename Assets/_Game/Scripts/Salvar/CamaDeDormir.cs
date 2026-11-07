using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Salvar
{
    /// <summary>
    /// Uma cama da mobília (a de casal, a de solteiro, o beliche; posta pelo <see cref="MobiliaNovaDoMundo"/> em cada
    /// colisor dela): na casa da família, E dorme e salva (02/11/2026, o save: no modo "só dormindo", é o jeito de salvar).
    /// Nas outras casas, nada.
    /// </summary>
    public class CamaDeDormir : Interagivel
    {
        public override string Dica(ArmaJogador quem) => NaCasaDaFamilia() ? "[E] Dormir e salvar" : null;

        public override void Usar(ArmaJogador quem)
        {
            if (NaCasaDaFamilia())
            {
                Salvamento.PedirDormir();
                Player.JogadorRede.Local?.Vida?.PedirDormir();   // a mente (06/11/2026): dormir de verdade
            }
        }

        // a cama está na casa da família (a menos de 13 m do meio dela, no mesmo andar ou no de cima)
        bool NaCasaDaFamilia()
        {
            var d = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            var lote = d?.Plano?.LoteDaFamilia;
            var tracado = d?.Plano?.CidadeDaFamilia?.Tracado;
            if (lote == null || tracado == null) return false;
            var meio = tracado.MatrizDaCasa(lote).MultiplyPoint3x4(Vector3.zero);
            var p = transform.position;
            return new Vector2(p.x - meio.x, p.z - meio.z).sqrMagnitude < 13f * 13f && Mathf.Abs(p.y - meio.y) < 8f;
        }
    }
}
