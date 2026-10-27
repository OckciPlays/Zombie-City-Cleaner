using System.Collections;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Video
{
    // Tomada 2: o bunker em corte (a parte 3 do plano dos vídeos). À noite, a câmera olha a casa da família do alto; um
    // corte de raio-X desce do telhado até abaixo do teto dos cômodos (tudo acima dele some, a cidade inteira), e o bunker
    // aparece como uma casa de boneca, apagado. Então as luzes acendem uma a uma, cada uma com o estalo do interruptor: a
    // escada, o salão, os quatro quartos dando a volta e o depósito; e a porta blindada abre. A câmera mergulha pelo
    // corte no salão até a altura dos olhos (a mesa comprida e o painel de armas, vazio, na frente) e mostra cada cômodo
    // em primeira pessoa, com um corte seco de um para o outro: os quatro quartos e o depósito.
    public partial class DiretorDoVideo
    {
        // a ordem em que as luzes acendem (os grupos do BunkerRede: 0 salão, 1 e 2 os quartos da esquerda, 3 e 4 os da
        // direita, 5 o depósito, 6 a escada)
        static readonly (float quando, int grupo)[] LuzesDoBunker = { (2.5f, 6), (2.9f, 0), (3.2f, 2), (3.5f, 1), (3.8f, 3), (4.1f, 4), (4.4f, 5) };
        const float AbrirAPorta = 2.7f;
        // o corte desce do alto do telhado até um pouco abaixo do teto dos cômodos (3 m de pé-direito: -2,85 m)
        const float CorteDeCima = 9f, CorteDeBaixo = -2.95f;
        const float ComecoDoCorte = 0.8f, FimDoCorte = 2.4f;
        // a câmera gira em volta do bunker até o mergulho, que termina nos olhos, no salão; depois, um cômodo por vez
        const float ComecoDoMergulho = 3f, ChegadaNoSalao = 7.8f, TempoPorComodo = 1.35f;
        const float DuracaoDoBunker = ChegadaNoSalao + 5 * TempoPorComodo;

        // os lugares, nas coordenadas da casa da família (as da PlantaDoBunker, que é do Editor: x à direita, z para os
        // fundos; o eixo da escada e do salão em x = -5, o chão em -5,85 e os olhos a 1,65 m dele)
        static readonly Vector3 MeioDoBunker = new Vector3(-5f, -4.4f, 16.5f);
        const float Olhos = -4.2f;
        // o mergulho: de cima do bunker, pela abertura do corte, até a porta do salão, sempre olhando bem para baixo até
        // passar do corte (com 30° ou mais de inclinação, metade dos 60° do campo, nada acima da câmera aparece: o que
        // estava cortado volta sem ninguém ver) e depois levantando o olhar até a frente, para o painel do fundo
        static readonly float[] TemposDoMergulho = { ComecoDoMergulho, 4.4f, 5.4f, 5.9f, 6.7f, ChegadaNoSalao };
        static readonly Vector3[] LugaresDoMergulho =
        {
            Vector3.zero,   // onde a câmera está quando o mergulho começa (a volta em torno do bunker)
            new Vector3(-5f, 9f, 7f), new Vector3(-5f, -1.6f, 12.6f), new Vector3(-5f, -3f, 13f),
            new Vector3(-5f, Olhos, 13.5f), new Vector3(-5f, Olhos, 14.6f),
        };
        static readonly Vector3[] OlharesDoMergulho =
        {
            Vector3.zero,
            new Vector3(-5f, -5.85f, 17.5f), new Vector3(-5f, -5.85f, 16.8f), new Vector3(-5f, -5.6f, 15.8f),
            new Vector3(-5f, -4.6f, 22f), new Vector3(-5f, -4.5f, 25f),
        };
        // os cômodos, na ordem do passeio: a entrada (a parede do lado do salão, em x), para onde fica o fundo (±1 em x),
        // o fundo (m) e o meio (z). Os quartos 2 e 1 à esquerda, o 3 e o 4 à direita, e o depósito, à esquerda, perto da porta
        static readonly (float entrada, float dentro, float fundo, float meio)[] Comodos =
        {
            (-11.2f, -1f, 4f, 19.2f), (-11.2f, -1f, 4f, 23.8f), (1.2f, 1f, 4f, 23.8f), (1.2f, 1f, 4f, 19.2f), (-11.2f, -1f, 4.5f, 14.2f),
        };

        Vector3[] _lugares, _olhares;
        BunkerRede _bunker;
        bool _apagouAsDeFora;

        IEnumerator PrepararBunker()
        {
            // a noite do fim do mapa, com pouca neblina (de 60 m, a da noite escondia a casa)
            Clima(21f, 0.15f, 1.4f);
            cameraDoVideo.fieldOfView = 60f;
            _apagouAsDeFora = false;
            cameraDoVideo.nearClipPlane = 0.3f;
            cameraDoVideo.farClipPlane = 400f;
            _bunker = BunkerRede.Instancia;
            _bunker.Definir(0, false);   // começa apagado e com a porta fechada
            // a lanterna do jogador (que nasce no salão, ligada) acenderia o salão antes das lâmpadas
            var jogador = JogadorRede.Local;
            if (jogador != null && jogador.Lanterna != null) jogador.Lanterna.Ligada.Value = false;
            // o mergulho no mundo, começando de onde a volta em torno do bunker termina
            var casa = _bunker.transform;
            _lugares = new Vector3[LugaresDoMergulho.Length];
            _olhares = new Vector3[OlharesDoMergulho.Length];
            for (int i = 1; i < _lugares.Length; i++)
            {
                _lugares[i] = casa.TransformPoint(LugaresDoMergulho[i]);
                _olhares[i] = casa.TransformPoint(OlharesDoMergulho[i]);
            }
            VoltaNoBunker(ComecoDoMergulho, out _lugares[0], out _olhares[0]);
            QuadroDoBunker(0f);
            return AnimarBunker();
        }

        IEnumerator AnimarBunker()
        {
            byte acesas = 0;
            bool aberta = false;
            int proxima = 0;
            for (float t = 0f; t < DuracaoDoBunker; t += Time.deltaTime)
            {
                bool mudou = false;
                for (; proxima < LuzesDoBunker.Length && t >= LuzesDoBunker[proxima].quando; proxima++)
                {
                    acesas |= (byte)(1 << LuzesDoBunker[proxima].grupo);
                    // o estalo do interruptor, perto do ouvido (o de verdade, 3D, só se ouve a 12 m dele)
                    Efeitos.Som(Efeitos.Chave, cameraDoVideo.transform.position, 0.5f, false);
                    mudou = true;
                }
                if (!aberta && t >= AbrirAPorta) mudou = aberta = true;
                if (mudou) _bunker.Definir(acesas, aberta);
                QuadroDoBunker(t);
                yield return null;
            }
            QuadroDoBunker(DuracaoDoBunker - 0.001f);
        }

        void QuadroDoBunker(float t)
        {
            // o corte desce (antes, nada é cortado), e a luz vira a de debaixo da terra
            _corte = t < ComecoDoCorte ? float.NaN : Mathf.Lerp(CorteDeCima, CorteDeBaixo, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ComecoDoCorte, FimDoCorte, t)));
            ModoVideo.Subsolo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ComecoDoCorte + 0.2f, FimDoCorte - 0.4f, t));
            if (!_apagouAsDeFora && _corte < 0f) ApagarAsDeFora();

            if (t < ComecoDoMergulho)
            {
                VoltaNoBunker(t, out var lugar, out var alvo);
                Posicionar(lugar, alvo);
                cameraDoVideo.fieldOfView = 60f;
            }
            else if (t < ChegadaNoSalao)
            {
                Percorrer(t, TemposDoMergulho, _lugares, _olhares);
                // nos olhos, o campo do jogo
                cameraDoVideo.fieldOfView = Mathf.Lerp(60f, 70f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.9f, ChegadaNoSalao, t)));
            }
            else
            {
                // cada cômodo: da porta para dentro, devagar, olhando o fundo de um lado ao outro (mais aberto: o quarto
                // tem 3,6 m de largura, e a tela é estreita)
                int i = Mathf.Min((int)((t - ChegadaNoSalao) / TempoPorComodo), Comodos.Length - 1);
                float k = Mathf.Clamp01((t - ChegadaNoSalao - i * TempoPorComodo) / TempoPorComodo);
                k = 0.3f * k + 0.7f * (1f - (1f - k) * (1f - k));   // anda e vai parando
                var c = Comodos[i];
                var casa = _bunker.transform;
                var de = new Vector3(c.entrada + c.dentro * 0.15f, Olhos, c.meio + 0.35f);
                var ate = new Vector3(c.entrada + c.dentro * 1.1f, Olhos - 0.05f, c.meio - 0.2f);
                var olhar = new Vector3(c.entrada + c.dentro * c.fundo, -4.9f, c.meio + Mathf.Lerp(-0.9f, 0.9f, k));
                Posicionar(casa.TransformPoint(Vector3.Lerp(de, ate, k)), casa.TransformPoint(olhar));
                cameraDoVideo.fieldOfView = 75f;
            }
            // debaixo da terra, o plano perto volta para perto (dentro dos cômodos, as paredes ficam a 1 ou 2 m)
            cameraDoVideo.nearClipPlane = cameraDoVideo.transform.position.y < -2.5f ? 0.05f : 0.3f;
        }

        /// <summary>A volta em torno do bunker, até o mergulho: o alvo sai da casa para o meio do bunker, a câmera gira e chega mais perto.</summary>
        void VoltaNoBunker(float t, out Vector3 lugar, out Vector3 alvo)
        {
            var casa = _bunker.transform;
            float k = Mathf.SmoothStep(0f, 1f, t / ComecoDoMergulho);   // para no começo do mergulho, que começa parado
            alvo = Vector3.Lerp(casa.TransformPoint(new Vector3(0f, 1f, 0f)), casa.TransformPoint(MeioDoBunker), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 2.6f, t)));
            var rotacao = Quaternion.Euler(Mathf.Lerp(52f, 58f, k), Mathf.Lerp(172f, 180f, k), 0f);
            lugar = alvo + rotacao * new Vector3(0f, 0f, -Mathf.Lerp(54f, 40f, k));
        }

        // as luzes de fora (os postes, o pisca-alerta e as das casas) continuavam acendendo o que ficou embaixo do corte: o
        // poste da rua iluminava o depósito antes da hora. Com o corte abaixo da terra, elas apagam, e os scripts delas
        // param (senão as ligariam de novo no quadro seguinte)
        void ApagarAsDeFora()
        {
            _apagouAsDeFora = true;
            foreach (var luz in FindObjectsByType<Light>())
            {
                if (luz.type == LightType.Directional || luz.transform.position.y < -0.3f) continue;
                foreach (var dono in luz.GetComponentsInParent<MonoBehaviour>())
                    if (dono is PosteDeLuz || dono is SinalDeVida) dono.enabled = false;
                luz.enabled = false;
            }
        }
    }
}
