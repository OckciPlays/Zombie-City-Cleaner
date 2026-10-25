using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O chão de uma construção por cômodo, para o som dos passos (<see cref="Player.PassosJogador"/>): caixas no espaço
    /// do objeto, cada uma com a sua superfície (vale a primeira que contém o ponto), e a do resto.
    /// A montagem da cidade põe uma em cada casa (a sala, a cozinha e o quarto, com o piso de cada um, os mesmos do
    /// cidade_casas.py; fora deles, o deck da varanda ou o degrau de concreto) e uma no bunker (os 4 quartos são de taco;
    /// o salão, os corredores, o depósito e a escada, de concreto).
    /// </summary>
    public class PisoPorZona : MonoBehaviour
    {
        public Bounds[] zonas;
        public Superficie[] superficies;
        public Superficie resto;

        public Superficie Em(Vector3 mundo)
        {
            Vector3 p = transform.InverseTransformPoint(mundo);
            for (int i = 0; zonas != null && i < zonas.Length; i++)
                if (zonas[i].Contains(p)) return superficies[i];
            return resto;
        }
    }
}
