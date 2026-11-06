using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os braços de primeira pessoa desenhados perto da origem (03/11/2026, a pedido: "a mão do personagem na primeira
    /// pessoa, quando não tem item na mão, fica meio tremendo; segurando item também"). O mundo é grande (no Grande, até
    /// 4 km do meio), e o float das posições só guarda 0,24 mm de 3 a 4 km do meio (0,49 mm além de 4 km): cada osso dos
    /// braços, a 30 cm do olho, ia para o degrau mais perto, um diferente a cada quadro, e a mão tremia uns 0,24 mm (cerca de
    /// um pixel; medido no bunker do Grande, em x = 3.078: as posições dos ossos andando de 0,000244 em 0,000244 m).
    /// Aqui, os braços (<see cref="JogadorRede.bracos"/>) e a câmera deles (<see cref="JogadorRede.cameraBracos"/>) saem de
    /// baixo da câmera do mundo e ficam numa raiz em <see cref="Origem"/>, com o giro da câmera do mundo a cada quadro (a luz
    /// do sol e da lua chega igual; o que se vê é o mesmo: a câmera dos braços continua na pilha da do mundo). Lá não há
    /// nada, então as luzes do mundo que alcançam o jogador (as lâmpadas do bunker, a fogueira, a lanterna dos outros, o
    /// carro) são copiadas para perto dos braços, no mesmo lugar em relação à câmera (<see cref="Luzes"/>, as mais fortes).
    /// O que precisa do lugar dos braços no mundo (a luz do clarão do tiro) usa <see cref="NoMundo"/>. Só no dono.
    /// </summary>
    [DefaultExecutionOrder(300)]   // depois de tudo o que mexe na câmera (a terceira pessoa, o carro, o capô)
    public class BracosNaOrigem : MonoBehaviour
    {
        /// <summary>Onde os braços ficam: perto do meio do mundo (0,03 mm de precisão), 300 m embaixo do chão, onde nada mais há.</summary>
        public static readonly Vector3 Origem = new Vector3(0f, -300f, 0f);
        /// <summary>Quantas luzes do mundo são copiadas para os braços.</summary>
        public const int Luzes = 4;
        const float BuscaACada = 0.25f;

        JogadorRede _rede;
        Transform _raiz;
        readonly Light[] _copias = new Light[Luzes];
        readonly Light[] _fontes = new Light[Luzes];
        readonly List<(Light luz, float forca)> _candidatas = new List<(Light, float)>();
        float _proximaBusca;

        /// <summary>A raiz dos braços (null antes de montar).</summary>
        public Transform Raiz => _raiz;

        void Awake() => _rede = GetComponent<JogadorRede>();

        void Montar()
        {
            if (_raiz != null || _rede == null || _rede.cameraMundo == null || _rede.bracos == null || _rede.cameraBracos == null) return;
            _raiz = new GameObject("Braços de primeira pessoa (perto da origem)").transform;
            _raiz.SetPositionAndRotation(Origem, _rede.cameraMundo.transform.rotation);
            // as posições de dentro de cada um ficam (os braços e a câmera estavam no lugar da câmera do mundo)
            _rede.bracos.transform.SetParent(_raiz, false);
            _rede.cameraBracos.transform.SetParent(_raiz, false);
            for (int i = 0; i < Luzes; i++)
            {
                var go = new GameObject("Luz do mundo " + i) { layer = JogadorRede.CamadaBracos };
                go.transform.SetParent(_raiz, false);
                var l = go.AddComponent<Light>();
                l.shadows = LightShadows.None;
                l.enabled = false;
                _copias[i] = l;
            }
        }

        /// <summary>Um ponto perto dos braços (a boca da arma, a ponta da tocha) no lugar dele no mundo, na frente da câmera.</summary>
        public Vector3 NoMundo(Vector3 ponto) =>
            _raiz == null || _rede == null || _rede.cameraMundo == null ? ponto : _rede.cameraMundo.transform.TransformPoint(_raiz.InverseTransformPoint(ponto));

        void LateUpdate()
        {
            if (_raiz == null)
            {
                Montar();
                if (_raiz == null) return;
            }
            var cam = _rede.cameraMundo.transform;
            _raiz.SetPositionAndRotation(Origem, cam.rotation);
            if (!_rede.cameraBracos.isActiveAndEnabled)
            {
                for (int i = 0; i < Luzes; i++) _copias[i].enabled = false;
                return;
            }
            if (Time.unscaledTime >= _proximaBusca)
            {
                _proximaBusca = Time.unscaledTime + BuscaACada;
                Buscar(cam.position);
            }
            for (int i = 0; i < Luzes; i++)
            {
                var f = _fontes[i];
                var c = _copias[i];
                if (f == null || !f.isActiveAndEnabled || f.intensity <= 0f)
                {
                    c.enabled = false;
                    continue;
                }
                c.type = f.type;
                c.color = f.color;
                c.intensity = f.intensity;
                c.range = f.range;
                c.spotAngle = f.spotAngle;
                c.innerSpotAngle = f.innerSpotAngle;
                c.renderingLayerMask = f.renderingLayerMask;
                c.transform.SetPositionAndRotation(_raiz.TransformPoint(cam.InverseTransformPoint(f.transform.position)), f.transform.rotation);
                c.enabled = true;
            }
        }

        // as luzes de ponto e de foco que alcançam a câmera, as mais fortes ali (sem as do próprio jogador, como a lanterna,
        // que não ilumina os braços, e a tocha do corpo, que tem a dela nos braços; nem a do clarão dele no mundo)
        void Buscar(Vector3 olho)
        {
            _candidatas.Clear();
            foreach (var l in FindObjectsByType<Light>())
            {
                if (!l.isActiveAndEnabled || l.intensity <= 0f || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
                var t = l.transform;
                if (t.IsChildOf(transform) || (_raiz != null && t.IsChildOf(_raiz)) || l.name == Combat.Efeitos.NomeDaLuzDoClaraoNoMundo) continue;
                float d = Vector3.Distance(t.position, olho);
                if (d >= l.range) continue;
                if (l.type == LightType.Spot && Vector3.Angle(t.forward, olho - t.position) > l.spotAngle * 0.5f) continue;
                float k = 1f - d / l.range;
                _candidatas.Add((l, l.intensity * k * k));
            }
            _candidatas.Sort((a, b) => b.forca.CompareTo(a.forca));
            for (int i = 0; i < Luzes; i++) _fontes[i] = i < _candidatas.Count ? _candidatas[i].luz : null;
        }

        void OnDestroy()
        {
            // os braços e a câmera deles são do jogador: saem junto
            if (_raiz != null) Destroy(_raiz.gameObject);
        }
    }
}
