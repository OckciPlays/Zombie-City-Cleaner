using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Video
{
    // Tomada 4: o jogador andando entre os zumbis (a parte 5 do plano dos vídeos). À noite, na avenida sul, o jogador anda
    // para o norte de mãos vazias (os punhos em guarda) e com a lanterna acesa, visto de trás do ombro. A lanterna o
    // denuncia: os zumbis das duas hordas em volta (a da rua sul, na frente, e a da saída sul, atrás) se viram e vêm,
    // devagar. Ele para, e a câmera sobe e gira, mostrando o jogador cercado (sem arma: o próximo vídeo é o das armas).
    // O diretor anda com o jogador no lugar do MovimentoPrimeiraPessoa (que fica desligado): as gravações saem parecidas.
    // Com "jogadorNoControle", quem anda é quem grava (o movimento do jogo, com o teclado e o mouse), e a câmera segue atrás
    // do ombro, até o Enter, que faz o final. O jogador não leva dano (o SemDano do ZombieManager), e o corpo dele aparece
    // (para o dono, ele era só sombra).
    public partial class DiretorDoVideo
    {
        const float DuracaoDoJogador = 10f, ParadaDoJogador = 5.5f, VelocidadeDoJogador = 1.6f;
        // no controle: quanto dura o final (a câmera subindo) depois do Enter
        const float SubidaDoFinal = 4.5f;
        // de onde ele sai (a pista da direita da avenida sul, entre a horda da saída sul e a da rua sul) e para onde olha
        static readonly Vector3 SaidaDoJogador = new Vector3(1.5f, 0.05f, -44f);
        const float RumoDoJogador = 0f;   // para o norte
        // o olhar um pouco para baixo: o facho da lanterna pega o chão e os zumbis da frente
        const float OlharDoJogador = 8f;

        CharacterController _corpoDoJogador;
        Vector3 _velocidadeDoJogador, _lugarDaCamera, _olharDaCamera;
        bool _cameraNoLugar;

        IEnumerator PrepararJogador()
        {
            // a noite das outras tomadas; a neblina um pouco mais grossa dá fundo à rua (o brilho a mais cai no fim, quando
            // os zumbis colados na lanterna estouravam de branco)
            Clima(21f, 0.35f, 1.2f);
            cameraDoVideo.fieldOfView = 55f;
            cameraDoVideo.nearClipPlane = 0.1f;
            cameraDoVideo.farClipPlane = 400f;
            Sombras(60f);
            var jogador = JogadorRede.Local;
            var movimento = jogador.GetComponent<MovimentoPrimeiraPessoa>();
            movimento.Teleportar(SaidaDoJogador, RumoDoJogador);
            _corpoDoJogador = jogador.GetComponent<CharacterController>();
            // o corpo aparece (para o dono, ele era só sombra); os braços de primeira pessoa somem, mas continuam ligados (o
            // soco, no clique, toca a animação deles, e desligados ela enchia o Console de avisos)
            foreach (var r in jogador.renderersCorpo) r.shadowCastingMode = ShadowCastingMode.On;
            foreach (var r in jogador.bracos.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
            if (jogador.Lanterna != null) jogador.Lanterna.Ligada.Value = true;
            _velocidadeDoJogador = Vector3.zero;
            _cameraNoLugar = false;
            if (jogadorNoControle)
            {
                SeguirNoOmbro(jogador, movimento, 0f);
                return AnimarJogadorNoControle(jogador, movimento);
            }
            movimento.enabled = false;
            movimento.cabeca.localRotation = Quaternion.Euler(OlharDoJogador, 0f, 0f);
            jogador.Pitch.Value = OlharDoJogador;
            QuadroDoJogador(0f, 0f);
            return AnimarJogador();
        }

        // com você no controle: a câmera segue atrás do ombro até o Enter; então o jogador para (o movimento desliga) e a
        // câmera sobe do lugar em que estava até quase em cima dele, como no final do caminho de sempre
        IEnumerator AnimarJogadorNoControle(JogadorRede jogador, MovimentoPrimeiraPessoa movimento)
        {
            float finalEm = -1f, rumo = 0f;
            Vector3 deOnde = default, olharDeOnde = default;
            for (float t = 0f; finalEm < 0f || t - finalEm < SubidaDoFinal; t += Time.deltaTime)
            {
                // parar o Play no meio: o jogador sai da rede antes do diretor
                if (jogador == null || movimento == null) yield break;
                var teclado = Keyboard.current;
                if (finalEm < 0f && teclado != null && teclado.enterKey.wasPressedThisFrame)
                {
                    finalEm = t;
                    movimento.enabled = false;
                    rumo = jogador.transform.eulerAngles.y;
                    deOnde = _lugarDaCamera;
                    olharDeOnde = _olharDaCamera;
                }
                if (finalEm < 0f) SeguirNoOmbro(jogador, movimento, Time.deltaTime);
                else
                {
                    float subida = Mathf.SmoothStep(0f, 1f, (t - finalEm) / SubidaDoFinal);
                    var pes = jogador.transform.position;
                    var lugar = pes + Quaternion.Euler(0f, rumo + 35f, 0f) * new Vector3(0f, 7.5f, -2.8f);
                    _brilhoExtra = _brilhoExtraDeNoite = Mathf.Lerp(1.2f, 1f, subida);
                    Posicionar(Vector3.Lerp(deOnde, lugar, subida), Vector3.Lerp(olharDeOnde, pes + Vector3.up, subida));
                }
                yield return null;
            }
        }

        /// <summary>
        /// A câmera atrás do ombro direito de quem anda, pelo olhar do jogo (o giro do corpo e a inclinação da cabeça):
        /// olhando para baixo ela sobe por trás, olhando para cima ela desce. Uma parede entre a cabeça e a câmera a puxa
        /// para a frente da parede (dentro das casas e do prédio).
        /// </summary>
        void SeguirNoOmbro(JogadorRede jogador, MovimentoPrimeiraPessoa movimento, float dt)
        {
            float giro = jogador.transform.eulerAngles.y;
            float inclinacao = Mathf.DeltaAngle(0f, movimento.cabeca.localEulerAngles.x);   // positivo = olhando para baixo
            var cabeca = jogador.transform.position + Vector3.up * 1.6f;
            var volta = Quaternion.Euler(Mathf.Clamp(12f + 0.6f * inclinacao, -15f, 55f), giro, 0f);
            var lugar = cabeca + volta * new Vector3(0.8f, 0f, -4.4f) + Vector3.up * 0.4f;
            var rumo = lugar - cabeca;
            float longe = rumo.magnitude;
            const int Cenario = 1 << 0;   // as paredes (os zumbis e o jogador ficam em outras camadas)
            if (Physics.SphereCast(cabeca, 0.25f, rumo / longe, out var bateu, longe, Cenario, QueryTriggerInteraction.Ignore))
                lugar = cabeca + rumo / longe * Mathf.Max(0.4f, bateu.distance - 0.05f);
            _lugarDaCamera = _cameraNoLugar ? Vector3.Lerp(_lugarDaCamera, lugar, 1f - Mathf.Exp(-12f * dt)) : lugar;
            _cameraNoLugar = true;
            _olharDaCamera = cabeca + Quaternion.Euler(inclinacao, giro, 0f) * Vector3.forward * 8f;
            Posicionar(_lugarDaCamera, _olharDaCamera);
        }

        IEnumerator AnimarJogador()
        {
            for (float t = 0f; t < DuracaoDoJogador; t += Time.deltaTime)
            {
                QuadroDoJogador(t, Time.deltaTime);
                yield return null;
            }
            QuadroDoJogador(DuracaoDoJogador, 0f);
        }

        void QuadroDoJogador(float t, float dt)
        {
            var jogador = JogadorRede.Local;
            if (jogador == null) return;
            // anda para o norte e vai parando (no meio dos zumbis, o CharacterController escorrega em volta deles)
            float alvo = t < ParadaDoJogador ? VelocidadeDoJogador : 0f;
            var desejada = Quaternion.Euler(0f, RumoDoJogador, 0f) * Vector3.forward * alvo;
            var horizontal = Vector3.Lerp(new Vector3(_velocidadeDoJogador.x, 0f, _velocidadeDoJogador.z), desejada, 1f - Mathf.Exp(-6f * dt));
            _velocidadeDoJogador.y = _corpoDoJogador.isGrounded ? -2f : _velocidadeDoJogador.y - 20f * dt;
            _velocidadeDoJogador = new Vector3(horizontal.x, _velocidadeDoJogador.y, horizontal.z);
            if (dt > 0f) _corpoDoJogador.Move(_velocidadeDoJogador * dt);

            // a câmera: atrás do ombro direito, seguindo devagar; depois de parar, sobe e gira até quase em cima dele,
            // mostrando o jogador no meio da roda de zumbis
            var pes = jogador.transform.position;
            float subida = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ParadaDoJogador + 0.3f, DuracaoDoJogador - 0.3f, t));
            var giro = Quaternion.Euler(0f, RumoDoJogador + 35f * subida, 0f);
            // (de 12 m, as cabeças dos zumbis escondiam a dele; a 7,5 m, quase em cima, ele fica no meio da tela)
            var lugar = pes + giro * Vector3.Lerp(new Vector3(0.8f, 2.3f, -4.2f), new Vector3(0f, 7.5f, -2.8f), subida);
            var olhar = pes + Vector3.Lerp(Quaternion.Euler(0f, RumoDoJogador, 0f) * new Vector3(0f, 1.3f, 7f), new Vector3(0f, 1f, 0f), subida);
            _brilhoExtra = _brilhoExtraDeNoite = Mathf.Lerp(1.2f, 1f, subida);
            _lugarDaCamera = _cameraNoLugar ? Vector3.Lerp(_lugarDaCamera, lugar, 1f - Mathf.Exp(-5f * dt)) : lugar;
            _cameraNoLugar = true;
            Posicionar(_lugarDaCamera, olhar);
        }
    }
}
