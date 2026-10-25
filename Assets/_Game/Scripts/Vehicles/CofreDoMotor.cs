using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O cofre do motor de um carro, em 3D (o motor e os suportes do <c>Cofre.fbx</c>, e as 3 peças do pecas.py nos
    /// lugares delas), montado pela montagem dos veículos: no carro da família, dentro da carroceria (todos veem as peças
    /// que estão nele); nos da cidade, dentro do "Capô aberto", que só aparece com o capô aberto.
    /// Cada peça aparece se está no carro (<see cref="ICapo.Pecas"/>). Para quem olha o motor (<see cref="Inspecionando"/>,
    /// pela <see cref="Player.TelaDoCapo"/>), o lugar vazio mostra a peça apagada (o fantasma), a peça embaixo do mouse
    /// acende (<see cref="Destaque"/>) e a luz de trabalho liga.
    /// </summary>
    public class CofreDoMotor : MonoBehaviour
    {
        [Tooltip("De onde se olha o motor: a câmera de quem abre o capô (a cabeça em cima do cofre).")]
        public Transform olhar;
        [Tooltip("A malha de cada peça no lugar dela, na ordem de PecasDoMotor (a vela, a bateria e o radiador).")]
        public MeshRenderer[] pecas;
        [Tooltip("A peça apagada de cada lugar, que aparece no lugar vazio para quem olha.")]
        public MeshRenderer[] fantasmas;
        [Tooltip("O gatilho de cada lugar: o que o mouse acha.")]
        public Collider[] lugares;
        [Tooltip("A luz de quem olha (só na máquina dele).")]
        public Light luz;
        [Tooltip("Quem guarda as peças: o Carro ou o CapoDaCidade (um ICapo).")]
        public MonoBehaviour dono;

        public ICapo Capo => dono as ICapo;
        /// <summary>O jogador desta máquina está olhando este motor.</summary>
        public bool Inspecionando { get; set; }
        /// <summary>O lugar embaixo do mouse (acende), ou -1.</summary>
        public int Destaque { get; set; } = -1;

        static readonly int CorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Aceso = new Color(1.5f, 1.42f, 1.2f, 1f);
        static readonly Color CorDoFantasma = new Color(1f, 0.86f, 0.42f, 1f);
        MaterialPropertyBlock _mpb;
        int[] _acesoAntes;

        void LateUpdate()
        {
            var capo = Capo;
            if (capo == null || pecas == null) return;
            _mpb ??= new MaterialPropertyBlock();
            if (_acesoAntes == null || _acesoAntes.Length != pecas.Length)
            {
                _acesoAntes = new int[pecas.Length];
                for (int i = 0; i < _acesoAntes.Length; i++) _acesoAntes[i] = -1;
            }
            var p = capo.Pecas;
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f);
            for (int k = 0; k < pecas.Length && k < PecasDoMotor.Quantas; k++)
            {
                bool tem = p.Tem(k), sobre = Inspecionando && k == Destaque;
                var r = pecas[k];
                if (r != null)
                {
                    if (r.enabled != tem) r.enabled = tem;
                    // a peça embaixo do mouse acende (o bloco só fica nela enquanto ela está acesa: fora disso, o SRP Batcher)
                    int aceso = tem && sobre ? 1 : 0;
                    if (aceso != _acesoAntes[k])
                    {
                        _acesoAntes[k] = aceso;
                        if (aceso == 1)
                        {
                            _mpb.Clear();
                            _mpb.SetColor(CorId, Aceso);
                            r.SetPropertyBlock(_mpb);
                        }
                        else r.SetPropertyBlock(null);
                    }
                }
                var f = fantasmas != null && k < fantasmas.Length ? fantasmas[k] : null;
                if (f == null) continue;
                bool fantasma = !tem && Inspecionando;
                if (f.enabled != fantasma) f.enabled = fantasma;
                if (!fantasma) continue;
                var cor = CorDoFantasma;
                cor.a = sobre ? 0.5f + 0.25f * pulso : 0.16f + 0.12f * pulso;
                _mpb.Clear();
                _mpb.SetColor(CorId, cor);
                f.SetPropertyBlock(_mpb);
            }
            if (luz != null && luz.enabled != Inspecionando) luz.enabled = Inspecionando;
        }

        /// <summary>O lugar deste gatilho, ou -1.</summary>
        public int Lugar(Collider c)
        {
            if (lugares == null || c == null) return -1;
            for (int i = 0; i < lugares.Length; i++)
                if (lugares[i] == c) return i;
            return -1;
        }

        /// <summary>Em cima do lugar (para a etiqueta na tela).</summary>
        public Vector3 Topo(int k)
        {
            if (lugares == null || k < 0 || k >= lugares.Length || lugares[k] == null) return transform.position;
            var b = lugares[k].bounds;
            return b.center + Vector3.up * b.extents.y;
        }

        /// <summary>
        /// O capô subindo (devagar no começo e no fim) ou descendo (ganhando velocidade até bater), com o trinco e a batida:
        /// o mesmo no carro da família e nos da cidade. <paramref name="visto"/> vai de 0 (fechado) a 1 (aberto).
        /// </summary>
        public static void AnimarCapo(Transform capo, Quaternion repouso, float abertura, ref float visto, bool aberto, float dt)
        {
            if (capo == null) return;
            float antes = visto;
            visto = Mathf.MoveTowards(visto, aberto ? 1f : 0f, dt / (aberto ? 0.7f : 0.45f));
            if (visto == antes) return;
            if (antes == 0f) Efeitos.Som(SomDoCarro.PortaAbrir, capo.position, 0.6f, true, 20f);
            if (visto == 0f) Efeitos.Som(SomDoCarro.PortaFechar, capo.position, 0.8f, true, 30f);
            float k = aberto ? Mathf.SmoothStep(0f, 1f, visto) : visto * visto;
            capo.localRotation = repouso * Quaternion.Euler(-abertura * k, 0f, 0f);
        }
    }
}
