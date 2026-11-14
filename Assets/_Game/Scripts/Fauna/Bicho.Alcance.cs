using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O alcance na altura (13/11/2026, a pedido: "quando estou voando no F2, o urso consegue me bater mesmo estando bem
    /// alto"; um dia o jogo terá lugares altos para subir e se esconder). O golpe só pegava a distância no chão; agora cada
    /// bicho alcança até uma altura acima dos pés dele: o urso de pé (~2 vezes a altura dele), o lobo e o coiote pulando, o
    /// alce e o veado na altura da cabeça, o ganso e a cascavel só nas pernas. Quem está mais alto não leva o golpe; o bicho
    /// espera embaixo (o urso de pé, bufando) e, depois de um tempo sorteado (de 8 a 20 s), desiste e vai embora.
    /// </summary>
    public sealed partial class Bicho
    {
        float _semAlcanceDesde = -1f, _paciencia;

        /// <summary>O alvo está fora do alcance agora? Marca desde quando (a paciência é sorteada na primeira vez).</summary>
        bool ForaDoAlcance(JogadorRede j, float agora)
        {
            if (j == null || FaunaDoMundo.Alcanca(this, j))
            {
                _semAlcanceDesde = -1f;
                return false;
            }
            if (_semAlcanceDesde < 0f)
            {
                _semAlcanceDesde = agora;
                _paciencia = Random.Range(8f, 20f);
            }
            return true;
        }

        /// <summary>Esperou embaixo o bastante: desiste.</summary>
        bool PerdeuAPaciencia(float agora) => _semAlcanceDesde >= 0f && agora - _semAlcanceDesde > _paciencia;

        /// <summary>Alguém está em cima, fora do alcance, bem perto (o urso fica de pé embaixo; serve nas cópias também).</summary>
        bool AlguemEmCima()
        {
            Vector3 p = transform.position;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                Vector3 pj = j.transform.position;
                if (Vector3.Distance(Plano(p), Plano(pj)) < 4f && pj.y - p.y >= FaunaDoMundo.AlcanceDeCima(this)) return true;
            }
            return false;
        }
    }

    public sealed partial class FaunaDoMundo
    {
        /// <summary>Até que altura acima dos pés o bicho alcança com o golpe (m).</summary>
        internal static float AlcanceDeCima(Bicho b)
        {
            switch (b.Dados.corpo)
            {
                case Corpo.Cobra: return 0.5f;
                case Corpo.Ave: return 0.6f;
            }
            if (b.Dados.papel == Papel.Urso) return b.Altura * 2f + 0.3f;   // de pé, a patada por cima
            if (b.Dados.papel == Papel.Matilha) return b.Altura + 0.7f;      // o pulo na mordida
            return b.Altura + 0.3f;                                          // a cabeçada, o coice
        }

        /// <summary>O bicho alcança o jogador na altura? (de cima: o alcance dele; de baixo: até uns 2 m)</summary>
        internal static bool Alcanca(Bicho b, JogadorRede j)
        {
            if (b == null || j == null) return false;
            float dy = j.transform.position.y - b.transform.position.y;
            return dy < AlcanceDeCima(b) && dy > -2.2f;
        }
    }
}
