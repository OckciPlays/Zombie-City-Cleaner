using UnityEngine;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O puxão do tornado (07/11/2026, a pedido: "quando passar perto, ele vai puxar com mais força. Ele vai poder puxar o
    /// nosso carro se estivermos mais perto (e girar em torno dele por um breve período), isso funcionará para o caiaque e
    /// moto; se eu estiver a pé, a força dele é mais forte ainda"). As contas que o jogador a pé (<see cref="ClimaNaTela"/>),
    /// o carro e a moto (Vehicles/Carro.Tornado.cs) e o caiaque usam, cada um na máquina que o move. O tornado vem do
    /// <see cref="Tempo"/> (o anfitrião manda a posição e a força).
    /// <list type="bullet">
    /// <item><b>Longe</b> (até o <c>raio</c> de cada um): um puxão para o meio e para o lado do giro, mais forte quanto mais perto
    /// (<see cref="Puxao"/>).</item>
    /// <item><b>No miolo</b> (até o <c>miolo</c> de cada um): ele pega, sobe e gira em volta do tornado por um tempo curto
    /// (<see cref="Giro"/>), e depois joga para fora. Depois de jogar, ele não pega de novo por uns segundos (só puxa).</item>
    /// </list>
    /// O giro é sempre no mesmo sentido (anti-horário, visto de cima), o mesmo dos destroços e do arremesso de antes.
    /// </summary>
    public static class Redemoinho
    {
        static Vector3 _antes;
        static float _medidoEm = -1f;
        static Vector3 _andando;

        /// <summary>O tornado está aí e forte o bastante para puxar.</summary>
        public static bool Ativo => Tempo.Tornado && Tempo.ForcaDoTornado > 0.2f;

        /// <summary>A velocidade do tornado andando (m/s, no chão), medida pelas posições que chegam.</summary>
        public static Vector3 Andando
        {
            get
            {
                float agora = Time.time;
                if (_medidoEm < 0f || agora - _medidoEm > 1f)
                {
                    _antes = Tempo.PosicaoDoTornado;
                    _medidoEm = agora;
                    _andando = Vector3.zero;
                }
                else if (agora - _medidoEm >= 0.25f)
                {
                    var p = Tempo.PosicaoDoTornado;
                    var v = (p - _antes) / (agora - _medidoEm);
                    v.y = 0f;
                    _andando = Vector3.Lerp(_andando, Vector3.ClampMagnitude(v, 8f), 0.5f);
                    _antes = p;
                    _medidoEm = agora;
                }
                return _andando;
            }
        }

        /// <summary>
        /// Onde está o tornado em relação a 'pos', no chão: para o meio (unitário), o lado do giro (unitário) e a distância.
        /// Falso sem tornado.
        /// </summary>
        public static bool Onde(Vector3 pos, out Vector3 paraOMeio, out Vector3 giro, out float distancia)
        {
            paraOMeio = giro = default;
            distancia = float.MaxValue;
            if (!Ativo) return false;
            var d = Tempo.PosicaoDoTornado - pos;
            d.y = 0f;
            distancia = d.magnitude;
            paraOMeio = distancia > 0.05f ? d / distancia : Vector3.forward;
            giro = Vector3.Cross(Vector3.up, paraOMeio);
            return true;
        }

        /// <summary>
        /// O puxão longe do miolo (m/s, ou m/s², conforme quem usa): para o meio e para o lado, de 0 na beira do 'raio' até
        /// 'forca' perto do meio, vezes a força do tornado.
        /// </summary>
        public static Vector3 Puxao(Vector3 pos, float raio, float forca, float lado = 0.55f)
        {
            if (!Onde(pos, out var meio, out var giro, out float d) || d >= raio) return Vector3.zero;
            float k = Mathf.Pow(1f - d / raio, 1.4f) * Tempo.ForcaDoTornado;
            return (meio + giro * lado) * (forca * k);
        }

        /// <summary>
        /// A velocidade de quem está girando no miolo: em volta do tornado a 'rapidez' m/s, puxado para o círculo de 'roda'
        /// m e subindo até 'altura' m acima de 'chao' (com o tornado andando junto).
        /// </summary>
        public static Vector3 Giro(Vector3 pos, float chao, float roda, float rapidez, float altura)
        {
            if (!Onde(pos, out var meio, out var giro, out float d)) return Vector3.zero;
            Vector3 v = giro * rapidez + meio * Mathf.Clamp((d - roda) * 2.2f, -8f, 8f) + Andando;
            v.y = Mathf.Clamp((chao + altura - pos.y) * 1.8f, -6f, 9f);
            return v;
        }

        /// <summary>O arremesso para fora, no fim do giro: para longe do meio, para o lado do giro e para cima.</summary>
        public static Vector3 Arremesso(Vector3 pos, float fora, float lado, float cima)
        {
            if (!Onde(pos, out var meio, out var giro, out _)) return Vector3.up * cima;
            return -meio * fora + giro * lado + Vector3.up * cima;
        }
    }
}
