using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>Os sons do Oco (Zumbi.Voz.cs): o asset, quem escuta e quem responde ao chamado.</summary>
    public partial class ZombieManager
    {
        SonsDoOco _sons;
        bool _sonsCarregados;

        /// <summary>Os sons do Oco (Audio/Resources/SonsDoOco.asset), ou null (sem eles, a voz antiga).</summary>
        internal SonsDoOco Sons
        {
            get
            {
                if (!_sonsCarregados)
                {
                    _sons = Resources.Load<SonsDoOco>("SonsDoOco");
                    _sonsCarregados = true;
                }
                return _sons;
            }
        }

        /// <summary>Quem escuta nesta máquina está a esta distância (e do mesmo lado da porta do bunker fechada).</summary>
        internal bool PodeOuvir(Vector3 ponto, float alcance = 30f)
        {
            var ouvido = PontoDeVista();
            return (ponto - ouvido).sqrMagnitude <= alcance * alcance && !BunkerRede.Isolado(ouvido, ponto);
        }

        /// <summary>Um chamado: até 2 dos de perto (16 m) respondem, de 0,6 a 1,5 s depois.</summary>
        internal void Responder(Zumbi quem)
        {
            var lista = IsServer ? _ativos : _copias;
            Vector3 de = quem.transform.position;
            int respostas = 0;
            // começa num ponto sorteado da lista (não são sempre os mesmos que respondem)
            int n = lista.Count, inicio = n > 0 ? Random.Range(0, n) : 0;
            for (int k = 0; k < n && respostas < 2; k++)
            {
                var z = lista[(inicio + k) % n];
                if (z == quem || z.Morto || z.Petrificado || z.NivelAtual == Zumbi.Nivel.Longe) continue;
                if ((z.transform.position - de).sqrMagnitude > 16f * 16f) continue;
                z.OuvirChamado(de, Time.time + Random.Range(0.6f, 1.5f));
                respostas++;
            }
        }
    }
}
