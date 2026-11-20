using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O tornado levando os corpos (19/11/2026, o filme da chuva: "terminar com um tornado levando alguns zumbis embora"). O
    /// zumbi que o tornado mata (a menos de 15 m do meio, World/CicloDoDia.Tempo.cs) cai como corpo; aqui, o corpo que está
    /// perto do miolo sobe e roda em volta dele (as mesmas contas do jogador a pé: <see cref="Redemoinho.Giro"/>), mais alto e
    /// mais largo que o jogador (na parede do funil), por uns 2 a 3,5 s, e é jogado para fora (<see cref="Redemoinho.Arremesso"/>).
    /// A estátua sobe inteira e quebra quando bate no chão (CorpoDeZumbi.Pedra.cs). Cada máquina tem os seus corpos (só
    /// visuais), e cada uma faz a conta com o tornado que recebe: os corpos voam parecido em todas.
    /// </summary>
    public partial class CorpoDeZumbi
    {
        const float PegaOCorpo = 17f;   // m do meio do tornado: o corpo entra no giro

        float _giraAte = -1f, _soltoAte, _chaoDoGiro, _alturaDoGiro, _rodaDoGiro, _rapidezDoGiro;

        /// <summary>Rodando no tornado agora: o corpo não assenta nem volta ao pool (ZombieManager.Corpos).</summary>
        internal bool NoTornado => _giraAte > 0f;

        /// <summary>
        /// No FixedUpdate: o giro e o arremesso. Verdadeiro enquanto o tornado está perto o bastante para pegar o corpo (o
        /// componente fica ligado esperando).
        /// </summary>
        bool PassoDoTornado()
        {
            if (!emUso || vivo || _parado || partes == null || partes.Length == 0 || partes[0] == null || partes[0].isKinematic)
            {
                _giraAte = -1f;
                return false;
            }
            var quadril = partes[0].position;
            if (!Redemoinho.Onde(quadril, out _, out _, out float distancia))
            {
                if (_giraAte > 0f) SoltarDoTornado();
                return false;
            }
            float agora = Time.time;
            if (_giraAte < 0f)
            {
                if (distancia > PegaOCorpo || agora < _soltoAte || Tempo.ForcaDoTornado < 0.5f) return distancia < PegaOCorpo * 2f;
                _giraAte = agora + Random.Range(2.2f, 3.6f);
                _chaoDoGiro = quadril.y;
                _alturaDoGiro = Random.Range(7f, 17f);
                _rodaDoGiro = Random.Range(7f, 10f);
                _rapidezDoGiro = Random.Range(12f, 16f);
            }
            if (agora >= _giraAte)
            {
                SoltarDoTornado();
                return false;
            }
            // cada parte vai para a velocidade do giro (em ~0,2 s), e o corpo rola no ar
            float k = 1f - Mathf.Exp(-5f * Time.fixedDeltaTime);
            foreach (var p in partes)
            {
                if (p == null || p.isKinematic) continue;
                var v = Redemoinho.Giro(p.position, _chaoDoGiro, _rodaDoGiro, _rapidezDoGiro, _alturaDoGiro);
                p.linearVelocity = Vector3.Lerp(p.linearVelocity, v, k);
            }
            partes[0].angularVelocity = Vector3.Lerp(partes[0].angularVelocity, new Vector3(2.5f, 4f, 1.5f), k);
            return true;
        }

        // o fim do giro: jogado para fora, para o lado e para cima; o tempo do corpo conta daqui (ele ainda vai cair)
        void SoltarDoTornado()
        {
            _giraAte = -1f;
            _soltoAte = Time.time + 6f;
            foreach (var p in partes)
                if (p != null && !p.isKinematic) p.linearVelocity = Redemoinho.Arremesso(p.position, 14f, 7f, 6f);
            caiuEm = Time.time;
        }
    }
}
