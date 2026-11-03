using UnityEngine;
using ZombieCityCleaner.Salvar;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O carro no save (02/11/2026): onde ele está, as peças do motor, os pneus, o macaco e o estepe no porta-malas, a
    /// gasolina, a bateria, a temperatura e o desgaste de cada peça e de cada pneu (o que só o anfitrião sabe). O macaco
    /// apoiado numa roda volta para o porta-malas, e o carro, para o chão.
    /// </summary>
    public partial class Carro
    {
        /// <summary>No anfitrião: o estado do carro para o save.</summary>
        public CarroSalvo ParaSalvar()
        {
            var p = _pecas.Value;
            var s = new CarroSalvo
            {
                pos = transform.position,
                giro = transform.rotation,
                vela = p.vela, bateria = p.bateria, radiador = p.radiador,
                macaco = _macaco.Value || _macacoEm.Value != PneusDoCarro.Vazio,
                estepe = _estepe.Value,
                litros = _litros, carga = _carga, temperatura = _temperatura, carencia = _carencia,
                resistenciaDaVela = _resistenciaDaVela, resistenciaDoRadiador = _resistenciaDoRadiador,
                gastoDaVela = _gastoDaVela, gastoDoRadiador = _gastoDoRadiador,
            };
            var pneus = _pneus.Value;
            for (int i = 0; i < PneusDoCarro.Quantas; i++)
            {
                s.pneus[i] = pneus[i];
                s.resistenciaDoPneu[i] = _resistenciaDoPneu[i];
                s.gastoDoPneu[i] = _gastoDoPneu[i];
            }
            return s;
        }

        /// <summary>No anfitrião, logo depois de o carro nascer: o estado do save no lugar do de fábrica.</summary>
        public void Restaurar(CarroSalvo s)
        {
            if (!IsServer || s == null) return;
            _pecas.Value = new PecasDoMotor { vela = (byte)s.vela, bateria = (byte)s.bateria, radiador = (byte)s.radiador };
            var pneus = new PneusDoCarro();
            for (int i = 0; i < PneusDoCarro.Quantas && i < s.pneus.Length; i++)
            {
                pneus[i] = (byte)Mathf.Clamp(s.pneus[i], 0, 255);
                if (i < s.resistenciaDoPneu.Length) _resistenciaDoPneu[i] = s.resistenciaDoPneu[i];
                if (i < s.gastoDoPneu.Length) _gastoDoPneu[i] = s.gastoDoPneu[i];
                PararDeMurchar(i);
            }
            _pneus.Value = pneus;
            _macaco.Value = s.macaco;
            _macacoEm.Value = PneusDoCarro.Vazio;
            _erguido.Value = false;
            _estepe.Value = (byte)Mathf.Clamp(s.estepe, 0, 255);
            _litros = Mathf.Clamp(s.litros, 0f, desgaste.tanque);
            _gasolina.Value = _litros;
            _carga = s.carga;
            _temperatura = s.temperatura;
            _carencia = s.carencia;
            _resistenciaDaVela = s.resistenciaDaVela;
            _resistenciaDoRadiador = s.resistenciaDoRadiador;
            _gastoDaVela = s.gastoDaVela;
            _gastoDoRadiador = s.gastoDoRadiador;
            _vazando.Value = 0;
        }
    }
}
