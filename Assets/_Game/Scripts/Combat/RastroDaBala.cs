using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// O trajeto da bala (23/11/2026, a pedido: "quando atira, seria interessante ter o trajeto da bala ao invés de apenas um
    /// ponto de início e ponto de fim"): um risco claro e curto que sai do cano e corre até onde a bala bateu (ou até longe,
    /// no tiro que não pegou nada), na velocidade de um traçante visto de perto, e some ao chegar.
    /// <list type="bullet">
    /// <item>Quem atira desenha o dele (o ArmaJogador.Atirar, do cano da arma: a dos braços na primeira pessoa, a do corpo
    /// na terceira); os outros desenham o que chega no JogadorRede.TiroRpc, do cano da arma do corpo. O tiro que não pegou
    /// nada leva o ponto final no anúncio, para todos verem o mesmo risco.</item>
    /// <item>O risco usa o shader das partículas do clima (a forma de linha, somando luz), o mesmo do raio, e fica mais
    /// fraco na neblina.</item>
    /// <item>No máximo <see cref="Maximo"/> riscos ao mesmo tempo (a espingarda solta um por chumbo): o mais velho é reusado.</item>
    /// </list>
    /// </summary>
    public class RastroDaBala : MonoBehaviour
    {
        /// <summary>A velocidade do risco (m/s): bem mais lenta que a bala de verdade, para dar para ver.</summary>
        public const float Velocidade = 420f;
        /// <summary>O comprimento do risco (m).</summary>
        public const float Comprimento = 6f;
        /// <summary>A largura do risco na ponta (m).</summary>
        public const float Largura = 0.03f;
        /// <summary>
        /// A largura cresce com a distância da câmera (m por m): de longe, os 3 cm davam menos de 1 pixel e o risco sumia;
        /// assim ele fica com uns 3 a 5 pixels na tela, perto ou longe.
        /// </summary>
        public const float LarguraPorMetro = 0.004f;
        public const int Maximo = 48;
        /// <summary>
        /// O mínimo que o risco fica na tela (s): com o jogo pesado (os quadros lentos), o risco de um tiro curto atravessava o
        /// caminho inteiro num quadro só e não chegava a ser desenhado.
        /// </summary>
        public const float DuracaoMinima = 0.08f;

        class Risco
        {
            public LineRenderer linha;
            public Vector3 de, dir;
            public float distancia, pulo, nasceu, duracao;
            public int quadro;   // (o Time.frameCount de quando nasceu)
            public bool ativo;
        }

        static RastroDaBala _instancia;
        static Material _material;
        readonly List<Risco> _riscos = new List<Risco>();

        /// <summary>
        /// Um risco de 'de' (o cano) até 'ate' (onde a bala bateu). 'pulo': os primeiros metros sem risco (na primeira
        /// pessoa, o cano fica colado na câmera, e o risco começaria dentro dela).
        /// </summary>
        public static void Mostrar(Vector3 de, Vector3 ate, float pulo = 0.5f)
        {
            Vector3 v = ate - de;
            float d = v.magnitude;
            if (d < pulo + 1f || float.IsNaN(d)) return;
            if (_instancia == null)
            {
                var go = new GameObject("Rastros das balas");
                DontDestroyOnLoad(go);
                _instancia = go.AddComponent<RastroDaBala>();
            }
            _instancia.Novo(de, v / d, d, pulo);
        }

        static Material Material()
        {
            if (_material != null) return _material;
            var shader = Shader.Find("ZombieCityCleaner/ClimaParticula");
            if (shader == null) shader = Resources.Load<Shader>("ClimaParticula");
            if (shader == null) return null;
            _material = new Material(shader) { name = "Rastro da bala" };
            _material.SetColor("_Cor", new Color(4.2f, 3.4f, 2.1f, 1f));   // (branco quente, somando luz)
            _material.SetFloat("_Forma", 3f);   // a linha
            _material.SetFloat("_Luz", 0f);
            _material.SetFloat("_Neblina", 0.35f);
            _material.SetFloat("_Origem", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetFloat("_Destino", (float)UnityEngine.Rendering.BlendMode.One);
            return _material;
        }

        void Novo(Vector3 de, Vector3 dir, float distancia, float pulo)
        {
            Risco r = null;
            foreach (var x in _riscos) if (!x.ativo) { r = x; break; }
            if (r == null && _riscos.Count < Maximo) r = NovoRisco();
            if (r == null)
            {
                // todos ocupados: o mais velho
                r = _riscos[0];
                foreach (var x in _riscos) if (x.nasceu < r.nasceu) r = x;
            }
            r.de = de;
            r.dir = dir;
            r.distancia = distancia;
            r.pulo = pulo;
            r.nasceu = Time.time;
            r.quadro = Time.frameCount;
            // pelo tempo (e não quadro a quadro): a ponta vai do pulo até o fim, e a cauda chega lá no fim da duração
            r.duracao = Mathf.Max((distancia + Comprimento - pulo) / Velocidade, DuracaoMinima);
            r.ativo = true;
            Posicionar(r);
            r.linha.enabled = true;
        }

        Risco NovoRisco()
        {
            var go = new GameObject("Rastro");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.sharedMaterial = Material();
            l.positionCount = 2;
            l.textureMode = LineTextureMode.Stretch;
            l.numCapVertices = 0;
            l.alignment = LineAlignment.View;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            // a cauda some e a ponta brilha
            l.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1f));
            l.widthMultiplier = Largura;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.45f), 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 1f) });
            l.colorGradient = g;
            l.enabled = false;
            var r = new Risco { linha = l };
            _riscos.Add(r);
            return r;
        }

        // o risco é desenhado pelo menos no quadro seguinte ao do tiro, mesmo num quadro lento (o jogo muito pesado: o tempo
        // passava do fim antes de o risco aparecer); nesse quadro, ele fica no máximo no meio do caminho
        static bool Novinho(Risco r) => Time.frameCount - r.quadro <= 1;

        // o tempo do risco agora (s)
        static float Decorrido(Risco r)
        {
            float t = Time.time - r.nasceu;
            return Novinho(r) ? Mathf.Min(t, r.duracao * 0.5f) : t;
        }

        // a frente do risco agora (m do cano); já nasce com um pedaço (o de um quadro a 60 por segundo)
        static float Frente(Risco r) => r.pulo + (r.distancia + Comprimento - r.pulo) * Mathf.Clamp01((Decorrido(r) + 1f / 60f) / r.duracao);

        void Posicionar(Risco r)
        {
            float frente = Frente(r);
            float ponta = Mathf.Min(frente, r.distancia);
            float cauda = Mathf.Clamp(frente - Comprimento, r.pulo, ponta);
            Vector3 a = r.de + r.dir * cauda, b = r.de + r.dir * ponta;
            r.linha.SetPosition(0, a);
            r.linha.SetPosition(1, b);
            var cam = Camera.main;
            r.linha.widthMultiplier = cam != null ? Mathf.Max(Largura, Vector3.Distance(cam.transform.position, (a + b) * 0.5f) * LarguraPorMetro) : Largura;
        }

        void Update()
        {
            foreach (var r in _riscos)
            {
                if (!r.ativo) continue;
                // a cauda chegou ao fim: some
                if (!Novinho(r) && Time.time - r.nasceu >= r.duracao)
                {
                    r.ativo = false;
                    r.linha.enabled = false;
                    continue;
                }
                Posicionar(r);
            }
        }
    }
}
