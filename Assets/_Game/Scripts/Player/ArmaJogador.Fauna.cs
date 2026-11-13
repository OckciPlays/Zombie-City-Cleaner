using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A caça (12/11/2026, a fauna): o tiro e o golpe nos bichos. O sangue e a marca na mira são aqui; o acerto vai ao
    /// anfitrião (JogadorRede.Fauna.cs) no fim do disparo (o tiro: um por bicho, na melhor parte pega, a cabeça vale mais,
    /// com o dano da distância mais perto, como nos sobreviventes) ou na hora (o golpe).
    /// </summary>
    public partial class ArmaJogador
    {
        struct NoBicho
        {
            public ParteDoBicho parte;
            public Vector3 ponto;
            public float distancia;
        }

        readonly List<NoBicho> _bichos = new List<NoBicho>();

        /// <summary>O raio do tiro pegou um bicho (vivo ou a carcaça): true para o tiro parar nele.</summary>
        bool TiroNoBicho(RaycastHit hit)
        {
            var parte = hit.collider.GetComponent<ParteDoBicho>();
            if (parte == null || parte.bicho == null) return false;
            Sangue.Respingo(hit.point, hit.normal, parte.bicho.Morto ? 6 : 16);
            if (!parte.bicho.Morto)
            {
                _bichos.Add(new NoBicho { parte = parte, ponto = hit.point, distancia = hit.distance });
                _marcaAte = Time.time + 0.18f;
                _marcaNaCabeca |= parte.cabeca;
            }
            return true;
        }

        void MandarAcertosNosBichos(DadosDaArma d)
        {
            for (int i = 0; i < _bichos.Count; i++)
            {
                var b = _bichos[i].parte.bicho;
                bool repetido = false;
                for (int k = 0; k < i && !repetido; k++) repetido = _bichos[k].parte.bicho == b;
                if (repetido || b == null) continue;
                int melhor = i;
                float perto = _bichos[i].distancia;
                for (int k = i; k < _bichos.Count; k++)
                {
                    if (_bichos[k].parte.bicho != b) continue;
                    perto = Mathf.Min(perto, _bichos[k].distancia);
                    if (_bichos[k].parte.cabeca && !_bichos[melhor].parte.cabeca) melhor = k;
                }
                rede.AcertarBicho(b, Armas.Dano(d, perto) * DanoDeFogo, _bichos[melhor].ponto, _bichos[melhor].parte.cabeca, true);
            }
            _bichos.Clear();
        }

        /// <summary>O golpe (a arma branca ou o soco) pegou um bicho: true se o golpe parou nele.</summary>
        bool GolpeNoBicho(RaycastHit hit, Vector3 ponto, float dano, bool faca, Transform cam)
        {
            var parte = hit.collider.GetComponent<ParteDoBicho>();
            if (parte == null || parte.bicho == null) return false;
            if (parte.bicho.Morto) return true;   // a carcaça (carneia-se com E, não com o golpe)
            Efeitos.Som(Efeitos.SocoCarne, ponto, faca ? 0.9f : 0.8f, false);
            Sangue.Respingo(ponto, -cam.forward);
            _marcaAte = Time.time + 0.18f;
            _marcaNaCabeca = parte.cabeca;
            rede.AcertarBicho(parte.bicho, dano, ponto, parte.cabeca, false);
            rede.AnunciarPancada(ponto, true);
            if (faca) Inventario.Desgastar(_id, Itens.DesgastePorUso(NaMao));
            return true;
        }
    }
}
