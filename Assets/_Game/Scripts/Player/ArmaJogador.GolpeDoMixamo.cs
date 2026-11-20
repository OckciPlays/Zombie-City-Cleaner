using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O golpe do Mixamo nos braços de primeira pessoa (19/11/2026, o teste: <see cref="GolpeDoMixamo"/>): o braço direito
    /// do modelo da arma na mão, nos eixos da câmera dos braços. Desligado no F1, nada muda.
    /// </summary>
    public partial class ArmaJogador
    {
        GolpeDoMixamo.Braco[] _golpeDoMixamo;
        RecargaDoMixamo.Bracos[] _recargaDoMixamo;
        PegadaDoMixamo.Bracos[] _pegadaDoMixamo;
        ArmaPerto.Bracos[] _armaPerto;

        /// <summary>A Recarga rápida: as esperas da recarga vezes isto (o mesmo da Recarregar).</summary>
        public float RapidezDaRecarga => 1f - 0.08f * Habilidades.Pontos(rede, Habilidade.RecargaRapida);

        void GolpeDoMixamoFP()
        {
            if (rede == null || rede.EmTerceiraPessoa || rede.cameraBracos == null) return;
            // a arma perto do rosto (o teste da primeira pessoa, 19/11/2026: ArmaPerto.cs; ele sai sozinho, aos poucos)
            bool perto = false;
            if (ComArma && ArmaPerto.Usa(Atual))
            {
                int ap = ModeloDaMao(NaMao);
                if (ap >= 0 && ap < modelos.Length && modelos[ap] != null && modelos[ap].activeInHierarchy)
                {
                    _armaPerto ??= new ArmaPerto.Bracos[modelos.Length];
                    _armaPerto[ap] ??= new ArmaPerto.Bracos(modelos[ap].transform);
                    _armaPerto[ap].Aplicar(rede.cameraBracos.transform, rede.Mirando.Value || NaLuneta, Recarregando);
                    perto = ArmaPerto.Ligado;
                }
            }
            if (!GolpeDoMixamo.Ligado) return;
            // as armas de fogo de duas mãos: o pulso pelo Mixamo (o teste da pegada; com a arma perto, não) e, no rifle e na
            // submetralhadora, a recarga
            if (!perto && ComArma && PegadaDoMixamo.Usa(Atual))
            {
                int mp = ModeloDaMao(NaMao);
                if (mp < 0 || mp >= modelos.Length || modelos[mp] == null || !modelos[mp].activeInHierarchy) return;
                _pegadaDoMixamo ??= new PegadaDoMixamo.Bracos[modelos.Length];
                _pegadaDoMixamo[mp] ??= new PegadaDoMixamo.Bracos(modelos[mp].transform);
                _pegadaDoMixamo[mp].Aplicar(rede.cameraBracos.transform.rotation);
            }
            if (ComArma && RecargaDoMixamo.Usa(Atual))
            {
                int mm = ModeloDaMao(NaMao);
                if (mm < 0 || mm >= modelos.Length || modelos[mm] == null || !modelos[mm].activeInHierarchy) return;
                _recargaDoMixamo ??= new RecargaDoMixamo.Bracos[modelos.Length];
                _recargaDoMixamo[mm] ??= new RecargaDoMixamo.Bracos(modelos[mm].transform);
                _recargaDoMixamo[mm].Aplicar(rede.cameraBracos.transform.rotation, Recarregando, Dados, RapidezDaRecarga);
                return;
            }
            var item = NaMao;
            if (!GolpeDoMixamo.Usa(item)) return;
            int m = ModeloDaMao(item);
            if (m < 0 || m >= modelos.Length || modelos[m] == null || !modelos[m].activeInHierarchy) return;
            _golpeDoMixamo ??= new GolpeDoMixamo.Braco[modelos.Length];
            _golpeDoMixamo[m] ??= new GolpeDoMixamo.Braco(modelos[m].transform);
            _golpeDoMixamo[m].Aplicar(rede.cameraBracos.transform.rotation, rede.UltimoAtaque, item);
        }
    }

    public partial class PassageiroDoCarro
    {
        GolpeDoMixamo.Braco _golpeDoMixamo;
        RecargaDoMixamo.Bracos _recargaDoMixamo;
        PegadaDoMixamo.Bracos _pegadaDoMixamo;
        ArmaJogador _armaDoDono;

        // o golpe do Mixamo no corpo (o teste; para os outros e para a sombra do dono); a recarga, só no dono (ela ainda não
        // vai pela rede)
        void GolpeDoMixamoNoCorpo()
        {
            if (!GolpeDoMixamo.Ligado || _rede == null || !_rede.IsSpawned || _rede.animadorCorpo == null) return;
            // o pulso das armas de fogo de duas mãos (em todos os corpos)
            var naMao = _rede.NaMao;
            if (Itens.EhArma(naMao) && PegadaDoMixamo.Usa(Itens.Arma(naMao)))
            {
                _pegadaDoMixamo ??= new PegadaDoMixamo.Bracos(_rede.animadorCorpo.transform);
                _pegadaDoMixamo.Aplicar(transform.rotation);
            }
            if (_rede.IsOwner)
            {
                _armaDoDono ??= _rede.GetComponent<ArmaJogador>();
                if (_armaDoDono != null && _armaDoDono.ComArma && RecargaDoMixamo.Usa(_armaDoDono.Atual))
                {
                    _recargaDoMixamo ??= new RecargaDoMixamo.Bracos(_rede.animadorCorpo.transform);
                    _recargaDoMixamo.Aplicar(transform.rotation, _armaDoDono.Recarregando, _armaDoDono.Dados, _armaDoDono.RapidezDaRecarga);
                    return;
                }
            }
            if (!GolpeDoMixamo.Usa(_rede.NaMao)) return;
            _golpeDoMixamo ??= new GolpeDoMixamo.Braco(_rede.animadorCorpo.transform);
            _golpeDoMixamo.Aplicar(transform.rotation, _rede.UltimoAtaque, _rede.NaMao);
        }
    }
}
