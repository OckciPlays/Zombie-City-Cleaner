using UnityEngine;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O tapete de pregos no carro (11/11/2026, a etapa 5a das armadilhas; Loot/ConstrucoesNoMundo.Armadilhas.cs): a roda que
    /// passa por cima fura o pneu, como o tiro (furado com remendo: de 1 a 9%), com o estouro e o ar saindo para todos.
    /// </summary>
    public partial class Carro
    {
        /// <summary>Quantas rodas o tapete confere (as do pneu, na ordem do <see cref="PneusDoCarro"/>).</summary>
        public int RodasParaOsPregos => rodas != null ? Mathf.Min(rodas.Length, PneusDoCarro.Quantas) : 0;

        /// <summary>O meio da roda no mundo (o colisor dela).</summary>
        public Vector3 CentroDaRoda(int roda)
        {
            var r = rodas != null && roda >= 0 && roda < rodas.Length ? rodas[roda] : null;
            if (r == null) return transform.position;
            if (Simula && r.enabled)
            {
                r.GetWorldPose(out var pos, out _);
                return pos;
            }
            return r.transform.position - transform.up * (r.suspensionDistance * 0.5f);
        }

        /// <summary>A roda tem o pneu inteiro (não furado)?</summary>
        public bool PneuBomNaRoda(int roda)
        {
            var p = _pneus.Value;
            return roda >= 0 && roda < PneusDoCarro.Quantas && p.Tem(roda) && !p.Furado(roda);
        }

        /// <summary>Só no anfitrião: o prego furou o pneu desta roda.</summary>
        public void FurarNoPrego(int roda)
        {
            if (!IsServer || !IsSpawned || !PneuBomNaRoda(roda)) return;
            var p = _pneus.Value;
            p[roda] = 5;   // furado, com remendo (como o furo da bala)
            _pneus.Value = p;
            PararDeMurchar(roda);
            FurouRpc((byte)roda);
        }
    }
}
