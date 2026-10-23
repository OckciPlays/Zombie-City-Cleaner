using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Os efeitos de carne, todos de pools (nada é criado no meio do combate): respingo no acerto, o jorro do toco,
    /// a cabeça explodindo, as manchas no chão e os furos de tiro (decals do URP) e os pedaços que caem com física.
    /// Cada máquina toca os seus: o anfitrião decide o que cai, e cada um vê pelo estado do zumbi que chega pela rede.
    /// Fica no objeto "Efeitos" da cena, ao lado do Efeitos.
    /// </summary>
    public class Sangue : MonoBehaviour
    {
        public Material materialParticulas;
        public Material decalSangue;
        public Material decalFuro;
        public int manchas = 40, furos = 40, pedacos = 16, jorros = 10;
        [Tooltip("Tempo que um pedaço fica no chão antes de afundar (s).")]
        public float vidaDoPedaco = 25f;

        /// <summary>
        /// Os pedaços ficam numa camada só deles: o tiro passa por eles e eles só batem no cenário (nem nos zumbis, nem
        /// nos jogadores). Antes, um pedaço parado em cima da cápsula de alguém tapava o tiro na cabeça.
        /// </summary>
        public const int CamadaPedacos = 8;

        static Sangue _instancia;
        static readonly Color Vermelho = new Color(0.38f, 0.02f, 0.02f, 1f);

        ParticleSystem _respingo, _pedacinhos;
        readonly Queue<ParticleSystem> _jorros = new Queue<ParticleSystem>();
        readonly Queue<DecalProjector> _manchas = new Queue<DecalProjector>(), _furos = new Queue<DecalProjector>();
        readonly Queue<Peca> _pedacos = new Queue<Peca>();

        class Peca
        {
            public GameObject go;
            public MeshFilter filtro;
            public Rigidbody corpo;
            public BoxCollider caixa;
            public int geracao;
        }

        void Awake()
        {
            _instancia = this;
            _respingo = Particulas("Respingo", 0.045f, 0.55f, 3.2f, 1.3f, 400);
            _pedacinhos = Particulas("Pedacinhos", 0.09f, 1.1f, 4.5f, 1.8f, 120);
            for (int i = 0; i < jorros; i++)
            {
                var j = Particulas("Jorro", 0.035f, 0.7f, 2.6f, 1.2f, 200);
                var emissao = j.emission; emissao.enabled = true; emissao.rateOverTime = 70f;
                var forma = j.shape; forma.angle = 10f;
                var main = j.main; main.loop = true;
                _jorros.Enqueue(j);
            }
            for (int i = 0; i < manchas; i++) _manchas.Enqueue(Decal("Mancha", decalSangue));
            for (int i = 0; i < furos; i++) _furos.Enqueue(Decal("Furo", decalFuro));
            for (int i = 0; i < pedacos; i++) _pedacos.Enqueue(NovoPedaco());
        }

        ParticleSystem Particulas(string nome, float tamanho, float vida, float velocidade, float gravidade, int maximo)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.6f, vida);
            main.startSpeed = new ParticleSystem.MinMaxCurve(velocidade * 0.4f, velocidade);
            main.startSize = new ParticleSystem.MinMaxCurve(tamanho * 0.5f, tamanho);
            main.startColor = new ParticleSystem.MinMaxGradient(Vermelho, new Color(0.22f, 0.01f, 0.01f, 1f));
            main.gravityModifier = gravidade;
            main.maxParticles = maximo;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emissao = ps.emission; emissao.enabled = false;
            var forma = ps.shape; forma.shapeType = ParticleSystemShapeType.Cone; forma.angle = 40f; forma.radius = 0.02f;
            var colisao = ps.collision;   // as gotas param no chão, sem atravessar
            colisao.enabled = true; colisao.type = ParticleSystemCollisionType.World; colisao.mode = ParticleSystemCollisionMode.Collision3D;
            colisao.dampen = 0.9f; colisao.bounce = 0.05f; colisao.lifetimeLoss = 0.4f; colisao.quality = ParticleSystemCollisionQuality.Low;
            colisao.collidesWith = 1 << 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = materialParticulas;
            return ps;
        }

        DecalProjector Decal(string nome, Material material)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var d = go.AddComponent<DecalProjector>();
            d.material = material;
            d.drawDistance = 45f;
            d.fadeScale = 0.9f;
            d.pivot = Vector3.zero;
            go.SetActive(false);
            return d;
        }

        Peca NovoPedaco()
        {
            var go = new GameObject("Pedaço") { layer = CamadaPedacos };
            go.transform.SetParent(transform, false);
            var p = new Peca { go = go, filtro = go.AddComponent<MeshFilter>() };
            go.AddComponent<MeshRenderer>();
            p.corpo = go.AddComponent<Rigidbody>();
            p.corpo.mass = 3f; p.corpo.linearDamping = 0.3f; p.corpo.angularDamping = 0.5f;
            p.corpo.interpolation = RigidbodyInterpolation.Interpolate;
            p.caixa = go.AddComponent<BoxCollider>();
            go.SetActive(false);
            return p;
        }

        // ------------------------------------------------------------------ efeitos

        /// <summary>O acerto na carne: um esguicho de gotas saindo da superfície.</summary>
        public static void Respingo(Vector3 ponto, Vector3 normal, int quantidade = 16)
        {
            if (_instancia == null) return;
            var ps = _instancia._respingo;
            ps.transform.SetPositionAndRotation(ponto + normal * 0.02f, Quaternion.LookRotation(normal));
            ps.Emit(quantidade);
        }

        /// <summary>A cabeça explodindo: muito sangue, pedacinhos e manchas em volta.</summary>
        public static void Explosao(Vector3 ponto)
        {
            if (_instancia == null) return;
            var ps = _instancia._respingo;
            ps.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(Vector3.up));
            var forma = ps.shape; forma.angle = 80f;
            ps.Emit(120);
            forma.angle = 40f;
            var pedacinhos = _instancia._pedacinhos;
            pedacinhos.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(Vector3.up));
            var forma2 = pedacinhos.shape; forma2.angle = 70f;
            pedacinhos.Emit(35);
            for (int i = 0; i < 4; i++)
            {
                Vector2 r = Random.insideUnitCircle * 1.4f;
                Mancha(ponto + new Vector3(r.x, 0f, r.y), Random.Range(0.5f, 1.1f));
            }
        }

        /// <summary>O toco jorra sangue por um tempo, preso ao osso (acompanha o zumbi andando).</summary>
        public static void Jorro(Transform osso, Vector3 direcao, float duracao = 3.5f)
        {
            if (_instancia == null || osso == null || _instancia._jorros.Count == 0) return;
            var j = _instancia._jorros.Dequeue();
            j.transform.SetParent(osso, false);
            j.transform.localPosition = Vector3.zero;
            j.transform.rotation = Quaternion.LookRotation(direcao.sqrMagnitude > 0.001f ? direcao : Vector3.up);
            j.Play();
            _instancia.StartCoroutine(_instancia.PararJorro(j, duracao));
        }

        IEnumerator PararJorro(ParticleSystem j, float duracao)
        {
            yield return new WaitForSeconds(duracao);
            if (j != null) j.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(1f);
            if (j == null) yield break;
            j.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            j.transform.SetParent(transform, false);
            _jorros.Enqueue(j);
        }

        /// <summary>Uma mancha de sangue no chão embaixo do ponto (se houver chão até 3 m abaixo).</summary>
        public static void Mancha(Vector3 ponto, float tamanho = 0.8f)
        {
            if (_instancia == null) return;
            if (!Physics.Raycast(ponto + Vector3.up * 0.5f, Vector3.down, out var hit, 3.5f, 1 << 0, QueryTriggerInteraction.Ignore)) return;
            var d = _instancia._manchas.Dequeue(); _instancia._manchas.Enqueue(d);
            d.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(-hit.normal, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward));
            d.size = new Vector3(tamanho, tamanho, 0.5f);
            d.gameObject.SetActive(true);
        }

        /// <summary>A marca do tiro no cenário.</summary>
        public static void FuroDeTiro(Vector3 ponto, Vector3 normal)
        {
            if (_instancia == null) return;
            var d = _instancia._furos.Dequeue(); _instancia._furos.Enqueue(d);
            Vector3 cima = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            d.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(-normal, Quaternion.AngleAxis(Random.Range(0f, 360f), normal) * cima));
            float s = Random.Range(0.07f, 0.1f);
            d.size = new Vector3(s, s, 0.12f);
            d.gameObject.SetActive(true);
        }

        /// <summary>Um membro que caiu: a peça sai com um empurrão, cai com física e some depois de um tempo.</summary>
        public static void Pedaco(Mesh malha, Material[] materiais, Vector3 posicao, Quaternion rotacao, Vector3 velocidade)
        {
            if (_instancia == null || malha == null) return;
            var p = _instancia._pedacos.Dequeue(); _instancia._pedacos.Enqueue(p);
            p.geracao++;
            p.filtro.sharedMesh = malha;
            if (materiais != null && materiais.Length > 0) p.go.GetComponent<MeshRenderer>().sharedMaterials = materiais;
            var limites = malha.bounds;
            p.caixa.center = limites.center; p.caixa.size = Vector3.Max(limites.size, Vector3.one * 0.05f);
            p.go.transform.SetPositionAndRotation(posicao, rotacao);
            p.go.SetActive(true);
            p.corpo.isKinematic = false;
            p.corpo.linearVelocity = velocidade;
            p.corpo.angularVelocity = Random.insideUnitSphere * 6f;
            _instancia.StartCoroutine(_instancia.Afundar(p, p.geracao));
        }

        IEnumerator Afundar(Peca p, int geracao)
        {
            yield return new WaitForSeconds(0.6f);
            if (p.geracao != geracao) yield break;
            Mancha(p.go.transform.position, Random.Range(0.4f, 0.7f));
            yield return new WaitForSeconds(vidaDoPedaco);
            if (p.geracao != geracao) yield break;
            // afunda no chão devagar e volta para o pool
            p.corpo.isKinematic = true;
            float t = 0f;
            while (t < 2f && p.geracao == geracao)
            {
                t += Time.deltaTime;
                p.go.transform.position += Vector3.down * (0.15f * Time.deltaTime);
                yield return null;
            }
            if (p.geracao == geracao) p.go.SetActive(false);
        }
    }
}
