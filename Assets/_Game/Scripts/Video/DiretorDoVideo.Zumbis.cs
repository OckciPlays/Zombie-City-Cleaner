using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Video
{
    // Tomada 3: os zumbis surgindo pela cidade (a parte 4 do plano dos vídeos). À noite, a câmera olha o cruzamento do
    // meio, a rua leste e a praça de lado e de cima, e os 1.000 zumbis sobem do chão em ondas, horda por horda, do meio
    // para fora, cada um com um pouco de poeira nos pés. No fim, um corte para o rosto de um deles, com a luz de uma
    // lanterna e o gemido, andando devagar para a câmera.
    // Só o modelo afunda e sobe (o raizDoModelo, filho da raiz): a raiz continua no NavMesh, e a IA, o agente e o nível de
    // detalhe seguem como no jogo.
    public partial class DiretorDoVideo
    {
        const float DuracaoDosZumbis = 8f, ComecoDoClose = 6f;
        // quanto o modelo fica abaixo do chão antes de subir (a altura de um zumbi) e quanto tempo ele leva para subir
        const float FundoDoZumbi = 2f, TempoDeSubir = 1.1f;
        // o meio da vista: o cruzamento do meio (a horda de 70), com a rua leste, a rua norte e o prédio atrás
        static readonly Vector3 AlvoDosZumbis = new Vector3(2f, 0f, 1f);

        class Levantado
        {
            public Zumbi zumbi;
            public float inicio;
            public bool poeira;
        }

        readonly List<Levantado> _levantados = new List<Levantado>();
        Zumbi _doClose;
        Vector3 _closeDe, _closeRumo, _closeOlhar;
        Light _lanternaDoVideo;

        IEnumerator PrepararZumbis()
        {
            // a noite do bunker, com a neblina rala (de 30 m, a da noite escondia o fundo) e mais brilho: de perto, o escuro
            // entre os postes engolia os zumbis
            Clima(21f, 0.25f, 1.6f);
            cameraDoVideo.fieldOfView = 55f;
            cameraDoVideo.nearClipPlane = 0.3f;
            cameraDoVideo.farClipPlane = 400f;
            Sombras(150f);
            // de 50 m, os zumbis do jogo nem apareceriam (eles somem a 65 m de quem olha e param a 40 m)
            ModoVideo.AlcanceDosZumbis = 3f;
            JuntarZumbis();
            QuadroDosZumbis(0f);   // todos debaixo do chão
            return AnimarZumbis();
        }

        IEnumerator AnimarZumbis()
        {
            for (float t = 0f; t < DuracaoDosZumbis; t += Time.deltaTime)
            {
                QuadroDosZumbis(t);
                yield return null;
            }
            QuadroDosZumbis(DuracaoDosZumbis);
        }

        // cada zumbi sobe com a horda dele (a mais perto): primeiro as do meio da vista, depois as de longe, e cada um com
        // um sorteio fixo de até 0,6 s, para as gravações saírem iguais
        void JuntarZumbis()
        {
            _levantados.Clear();
            var hordas = FindObjectsByType<PontoDeHorda>();
            var sorteio = new System.Random(11);
            foreach (var z in FindObjectsByType<Zumbi>())
            {
                if (!z.isActiveAndEnabled || z.Copia || z.Morto) continue;
                Vector3 p = z.transform.position;
                Vector3 horda = p;
                float menor = float.MaxValue;
                foreach (var h in hordas)
                {
                    float d = (h.transform.position - p).sqrMagnitude;
                    if (d < menor) { menor = d; horda = h.transform.position; }
                }
                float longe = new Vector2(horda.x - AlvoDosZumbis.x, horda.z - AlvoDosZumbis.z).magnitude;
                _levantados.Add(new Levantado { zumbi = z, inicio = 0.4f + 4f * Mathf.Clamp01(longe / 70f) + 0.8f * (float)sorteio.NextDouble() });
            }
        }

        void QuadroDosZumbis(float t)
        {
            foreach (var l in _levantados)
            {
                if (l.zumbi == null) continue;
                // sobe do chão devagar no começo e no fim
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - l.inicio) / TempoDeSubir));
                l.zumbi.raizDoModelo.localPosition = new Vector3(0f, -FundoDoZumbi * (1f - k), 0f);
                if (!l.poeira && t >= l.inicio)
                {
                    l.poeira = true;
                    Poeira(new Bounds(l.zumbi.transform.position, new Vector3(1.1f, 0f, 1.1f)), 0.12f);
                }
            }
            if (t < ComecoDoClose)
            {
                // a vista de lado e de cima, perto o bastante para ver cada um subir, chega mais perto devagar, girando um pouco
                float v = Mathf.SmoothStep(0f, 1f, t / ComecoDoClose);
                Orbitar(AlvoDosZumbis + Vector3.up * 0.8f, Mathf.Lerp(30f, 50f, v), Mathf.Lerp(38f, 30f, v), Mathf.Lerp(30f, 22f, v));
                return;
            }
            Close(t - ComecoDoClose);
        }

        // o close: um zumbi vagando perto do meio da vista, de frente (a câmera na frente dele, na altura da cabeça), com
        // uma lanterna na câmera; ele vem andando e a câmera recua mais devagar, então ele chega perto
        void Close(float s)
        {
            if (_doClose == null)
            {
                _doClose = EscolherDoClose();
                if (_doClose == null) return;
                var cabeca = _doClose.partes[CorpoDeZumbi.ParteCabeca].position;
                _closeRumo = Vector3.ProjectOnPlane(_doClose.transform.forward, Vector3.up).normalized;
                _closeDe = cabeca + _closeRumo * 1.5f - Vector3.up * 0.05f;
                _closeOlhar = cabeca;
                _doClose.Gemer(1f);
                cameraDoVideo.fieldOfView = 40f;
                cameraDoVideo.nearClipPlane = 0.05f;
                // a lanterna já ilumina o rosto: o brilho a mais da vista larga o estourava
                _brilhoExtra = _brilhoExtraDeNoite = 0.5f;
                Lanterna().enabled = true;
            }
            var alvo = _doClose.partes[CorpoDeZumbi.ParteCabeca].position;
            // o olhar segue a cabeça suavizado (a cabeça balança com a animação)
            _closeOlhar = Vector3.Lerp(_closeOlhar, alvo, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Posicionar(_closeDe + _closeRumo * (0.12f * s), _closeOlhar);
        }

        Zumbi EscolherDoClose()
        {
            Zumbi melhor = null;
            float menor = float.MaxValue;
            var perto = AlvoDosZumbis;
            foreach (var l in _levantados)
            {
                var z = l.zumbi;
                if (z == null || z.Morto || z.SemCabeca || z.Atual != Zumbi.Estado.Vagando || z.transform.position.y > 0.5f) continue;
                float d = (z.transform.position - perto).sqrMagnitude;
                if (d < menor) { menor = d; melhor = z; }
            }
            return melhor;
        }

        // a lanterna do close, na câmera (como a do jogador: luz fria e quente no meio, com sombra)
        Light Lanterna()
        {
            if (_lanternaDoVideo != null) return _lanternaDoVideo;
            var objeto = new GameObject("Lanterna do vídeo");
            objeto.transform.SetParent(cameraDoVideo.transform, false);
            objeto.transform.localPosition = new Vector3(0.15f, -0.1f, 0f);
            _lanternaDoVideo = objeto.AddComponent<Light>();
            _lanternaDoVideo.type = LightType.Spot;
            _lanternaDoVideo.range = 15f;
            _lanternaDoVideo.spotAngle = 55f;
            _lanternaDoVideo.innerSpotAngle = 40f;
            _lanternaDoVideo.intensity = 1.2f;   // a 1,5 m do rosto (com 25, o zumbi ficava branco)
            _lanternaDoVideo.color = new Color(1f, 0.95f, 0.85f);
            _lanternaDoVideo.shadows = LightShadows.Soft;
            _lanternaDoVideo.enabled = false;
            return _lanternaDoVideo;
        }
    }
}
