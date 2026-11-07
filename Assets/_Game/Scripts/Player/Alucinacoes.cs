using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As alucinações (06/11/2026, a sanidade, a etapa 2), só no dono: ninguém mais vê nem ouve ("você viu aquilo?").
    /// <para>Abaixo de 40 (a pedido: "Sons falsos, vultos ja seria algo quando o jogador atingiria os seus 40%"):
    /// - os vultos: um Oco todo preto, parado, na beira da vista (de 40° a 70° do olhar, de 10 a 20 m), que some quando o
    ///   jogador olha para ele (ou em 3 s), com um sussurro de onde estava;
    /// - os sons falsos: a voz de um Oco, um chamado ou um sussurro atrás ou do lado (de 5 a 12 m).
    /// Quanto mais baixa a mente, mais seguidos (de 45 s a 18 s).</para>
    /// <para>Abaixo de 15, o colapso (a pedido: "seus amigos se transformariam em zumbis e ter alguns zumbis falsos que quando
    /// atirar eles viram fantasmas", "fuga em pânico por alguns segundos (max 6s), desmaio"), a cada 18 a 35 s um destes:
    /// - os amigos como zumbis (de 8 a 12 s): o corpo do amigo some e um Oco anda no lugar dele, com a velocidade dele; o
    ///   nome some também. Com o fogo amigo ligado, o tiro pega no amigo de verdade;
    /// - os zumbis falsos: de 1 a 3 Ocos vêm andando de 14 a 20 m à frente; o tiro que passa por um deles (ou ele chegando a
    ///   1,8 m) o vira fantasma: pálido, sobe e some com um sussurro;
    /// - a fuga em pânico (de 3 a 6 s, de costas para onde ele olhava; o <see cref="ColapsoDoJogador"/>);
    /// - o desmaio (de 7 a 12 s; no máximo um a cada 2 minutos).
    /// Um susto no colapso também pode fazer fugir (35%).</para>
    /// <para>Os Ocos daqui são os modelos do <see cref="ZombieManager"/> sem a IA, sem a rede e sem colisão (como a cena de
    /// comparação faz), com a animação pelo "Speed".</para>
    /// </summary>
    public class Alucinacoes : MonoBehaviour
    {
        public static Alucinacoes Local { get; private set; }

        static readonly HashSet<JogadorRede> _escondidos = new HashSet<JogadorRede>();
        /// <summary>Este jogador está sendo visto como zumbi agora (o nome em cima da cabeça some).</summary>
        public static bool Escondido(JogadorRede j) => j != null && _escondidos.Contains(j);

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static Material _preto, _palido;

        VidaJogador _vida;
        JogadorRede _rede;
        float _proximoVulto, _proximoSom, _proximoColapso, _proximoDesmaio, _proximaFuga, _ultimaMente = -1f;

        // os que estão na tela agora
        class Vulto { public GameObject go; public float some; }
        class Falso { public GameObject go; public Animator anim; public float velocidade, ate, proximaVoz; public bool fantasma; public float fantasmaDesde; }
        class AmigoZumbi { public JogadorRede amigo; public GameObject go; public Animator anim; public float ate; public readonly List<Renderer> apagados = new List<Renderer>(); }
        readonly List<Vulto> _vultos = new List<Vulto>();
        readonly List<Falso> _falsos = new List<Falso>();
        readonly List<AmigoZumbi> _amigos = new List<AmigoZumbi>();

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
        }

        void OnEnable()
        {
            Local = this;
            ArmaJogador.AtirouLocal += AoAtirar;
        }

        void OnDisable()
        {
            if (Local == this) Local = null;
            ArmaJogador.AtirouLocal -= AoAtirar;
            LimparTudo();
        }

        Transform Olho => _rede != null ? _rede.Olho : null;

        bool PodeSentir => _vida != null && _vida.IsSpawned && !_vida.Caido.Value && !_vida.Desmaiado.Value && !_vida.Dormindo.Value && VidaJogador.CorpoAnda;

        void Update()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            float mente = _vida.Sanidade.Value;
            if (!PodeSentir || mente >= 40f)
            {
                if (_vultos.Count + _falsos.Count + _amigos.Count > 0 && (!PodeSentir || mente >= 50f)) LimparTudo();
                _ultimaMente = mente;
                AtualizarOsQueEstao();
                return;
            }
            float agora = Time.time;
            float k = Mathf.Clamp01(mente / 40f);   // 0 no fundo, 1 nos 40
            if (agora >= _proximoVulto)
            {
                _proximoVulto = agora + Mathf.Lerp(18f, 45f, k) * Random.Range(0.75f, 1.25f);
                if (_proximoVulto > 0f && _vultos.Count == 0) NovoVulto();
            }
            if (agora >= _proximoSom)
            {
                _proximoSom = agora + Mathf.Lerp(10f, 30f, k) * Random.Range(0.75f, 1.25f);
                SomFalso();
            }
            // o colapso
            if (mente < 15f)
            {
                bool susto = _ultimaMente >= 0f && _ultimaMente - mente >= 3.5f;
                if (susto && agora >= _proximaFuga && Random.value < 0.35f) Fuga();
                else if (agora >= _proximoColapso)
                {
                    _proximoColapso = agora + Random.Range(18f, 35f);
                    EventoDoColapso();
                }
            }
            else _proximoColapso = Mathf.Max(_proximoColapso, agora + 8f);
            _ultimaMente = mente;
            AtualizarOsQueEstao();
        }

        // ------------------------------------------------ o colapso ------------------------------------------------

        void EventoDoColapso()
        {
            // os pesos: os amigos (se há algum perto), os falsos, a fuga e o desmaio (raro)
            var perto = AmigosPerto(40f);
            float pAmigos = perto.Count > 0 && _amigos.Count == 0 ? 3f : 0f;
            float pFalsos = _falsos.Count == 0 ? 3f : 0f;
            float pFuga = Time.time >= _proximaFuga ? 2f : 0f;
            float pDesmaio = Time.time >= _proximoDesmaio ? 1f : 0f;
            float soma = pAmigos + pFalsos + pFuga + pDesmaio;
            if (soma <= 0f) return;
            float r = Random.value * soma;
            if ((r -= pAmigos) < 0f) AmigosComoZumbis(perto);
            else if ((r -= pFalsos) < 0f) ZumbisFalsos(Random.Range(1, 4));
            else if ((r -= pFuga) < 0f) Fuga();
            else Desmaio();
        }

        /// <summary>A fuga em pânico, de costas para onde ele olhava (de 3 a 6 s).</summary>
        public void Fuga()
        {
            _proximaFuga = Time.time + 40f;
            var olho = Olho;
            var para = olho != null ? -olho.forward : -transform.forward;
            para = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * para;
            ColapsoDoJogador.Local?.Fugir(para, Random.Range(3f, ColapsoDoJogador.FugaMaxima));
        }

        /// <summary>O desmaio (de 7 a 12 s; no máximo um a cada 2 minutos pelo colapso).</summary>
        public void Desmaio()
        {
            _proximoDesmaio = Time.time + 120f;
            _vida.PedirDesmaio(Random.Range(7f, 12f));
        }

        List<JogadorRede> AmigosPerto(float alcance)
        {
            var lista = new List<JogadorRede>();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j == _rede || !j.AlvoValido || !j.Criado || j.animadorCorpo == null) continue;
                if ((j.transform.position - transform.position).sqrMagnitude <= alcance * alcance) lista.Add(j);
            }
            return lista;
        }

        /// <summary>Os amigos perto viram zumbis aos olhos dele (de 8 a 12 s; no máximo 2).</summary>
        public void AmigosComoZumbis(List<JogadorRede> amigos = null)
        {
            amigos ??= AmigosPerto(40f);
            int n = 0;
            foreach (var a in amigos)
            {
                if (n >= 2 || _amigos.Exists(x => x.amigo == a)) continue;
                var go = NovoOco(false);
                if (go == null) return;
                var az = new AmigoZumbi { amigo = a, go = go, anim = go.GetComponentInChildren<Animator>(), ate = Time.time + Random.Range(8f, 12f) };
                foreach (var r in a.animadorCorpo.GetComponentsInChildren<Renderer>())
                    if (r.enabled)
                    {
                        r.enabled = false;
                        az.apagados.Add(r);
                    }
                go.transform.SetPositionAndRotation(a.transform.position, a.transform.rotation);
                _amigos.Add(az);
                _escondidos.Add(a);
                Voz(a.transform.position + Vector3.up * 1.5f);
                n++;
            }
        }

        /// <summary>Zumbis falsos vindo pela frente (de 1 a 3).</summary>
        public void ZumbisFalsos(int quantos)
        {
            var olho = Olho;
            if (olho == null) return;
            for (int i = 0; i < quantos; i++)
            {
                if (!PontoNaVista(olho, Random.Range(-50f, 50f), Random.Range(14f, 20f), false, out var p)) continue;
                var go = NovoOco(false);
                if (go == null) return;
                go.transform.position = p;
                OlharPara(go.transform, transform.position);
                var f = new Falso { go = go, anim = go.GetComponentInChildren<Animator>(), velocidade = Random.Range(1.1f, 1.7f), ate = Time.time + 25f, proximaVoz = Time.time + Random.Range(0.5f, 3f) };
                _falsos.Add(f);
            }
        }

        /// <summary>Um vulto na beira da vista.</summary>
        public void NovoVulto()
        {
            var olho = Olho;
            if (olho == null) return;
            for (int tentativa = 0; tentativa < 4; tentativa++)
            {
                float lado = Random.Range(40f, 70f) * (Random.value < 0.5f ? 1f : -1f);
                if (!PontoNaVista(olho, lado, Random.Range(10f, 20f), true, out var p)) continue;
                var go = NovoOco(true);
                if (go == null) return;
                go.transform.position = p;
                OlharPara(go.transform, transform.position);
                _vultos.Add(new Vulto { go = go, some = Time.time + 3f });
                return;
            }
        }

        void SomFalso()
        {
            var olho = Olho;
            if (olho == null) return;
            // atrás ou do lado
            var dir = Quaternion.Euler(0f, Random.Range(110f, 250f), 0f) * Plano(olho.forward);
            var p = transform.position + dir * Random.Range(5f, 12f) + Vector3.up * 1.4f;
            float r = Random.value;
            var sons = ZombieManager.Instancia != null ? ZombieManager.Instancia.Sons : null;
            if (r < 0.3f || sons == null) Efeitos.Som(SomDaMente.Sussurro, p, 0.75f, true, 25f);
            else if (r < 0.5f && sons.chamado != null && sons.chamado.Length > 0) Efeitos.Som(sons.chamado[Random.Range(0, sons.chamado.Length)], p, 0.8f * sons.volume, true, 40f);
            else Voz(p);
        }

        void Voz(Vector3 p)
        {
            var sons = ZombieManager.Instancia != null ? ZombieManager.Instancia.Sons : null;
            if (sons != null && sons.voz != null && sons.voz.Length > 0) Efeitos.Som(sons.voz[Random.Range(0, sons.voz.Length)], p, 0.8f * sons.volume, true, 35f);
            else Efeitos.Som(SomDaMente.Sussurro, p, 0.7f, true, 25f);
        }

        // ------------------------------------------------ a cada quadro ------------------------------------------------

        void AtualizarOsQueEstao()
        {
            float dt = Time.deltaTime, agora = Time.time;
            var olho = Olho;
            // os vultos somem quando olhados (a menos de 22°) ou no tempo
            for (int i = _vultos.Count - 1; i >= 0; i--)
            {
                var v = _vultos[i];
                bool olhando = false;
                if (v.go != null && olho != null)
                {
                    var para = v.go.transform.position + Vector3.up * 1.2f - olho.position;
                    olhando = Vector3.Angle(olho.forward, para) < 22f;
                }
                if (v.go == null || olhando || agora >= v.some)
                {
                    if (v.go != null)
                    {
                        Efeitos.Som(SomDaMente.Sussurro, v.go.transform.position + Vector3.up * 1.5f, 0.6f, true, 25f);
                        Destroy(v.go);
                    }
                    _vultos.RemoveAt(i);
                }
            }
            // os falsos andam até ele; chegando perto (ou no tempo), viram fantasma
            for (int i = _falsos.Count - 1; i >= 0; i--)
            {
                var f = _falsos[i];
                if (f.go == null)
                {
                    _falsos.RemoveAt(i);
                    continue;
                }
                var t = f.go.transform;
                if (f.fantasma)
                {
                    float s = (agora - f.fantasmaDesde) / 0.8f;
                    t.position += Vector3.up * dt * 1.4f;
                    t.localScale = new Vector3(Mathf.Lerp(1f, 0.15f, s), Mathf.Lerp(1f, 1.4f, s), Mathf.Lerp(1f, 0.15f, s));
                    if (s >= 1f)
                    {
                        Destroy(f.go);
                        _falsos.RemoveAt(i);
                    }
                    continue;
                }
                var ate = transform.position - t.position;
                ate.y = 0f;
                if (ate.magnitude < 1.8f || agora >= f.ate)
                {
                    VirarFantasma(f);
                    continue;
                }
                var passo = ate.normalized * f.velocidade * dt;
                var p = NoChao(t.position + passo, t.position.y);
                t.position = p;
                OlharPara(t, transform.position);
                if (f.anim != null) f.anim.SetFloat(SpeedId, f.velocidade);
                if (agora >= f.proximaVoz)
                {
                    f.proximaVoz = agora + Random.Range(3f, 6f);
                    Voz(t.position + Vector3.up * 1.5f);
                }
            }
            // os amigos como zumbis seguem o amigo
            for (int i = _amigos.Count - 1; i >= 0; i--)
            {
                var a = _amigos[i];
                if (a.amigo == null || a.go == null || agora >= a.ate || !a.amigo.AlvoValido)
                {
                    DevolverAmigo(a);
                    _amigos.RemoveAt(i);
                    continue;
                }
                a.go.transform.SetPositionAndRotation(a.amigo.transform.position, a.amigo.transform.rotation);
                if (a.anim != null) a.anim.SetFloat(SpeedId, a.amigo.Velocidade);
            }
        }

        // o tiro dele: o falso que estiver na linha (a até 0,55 m do peito ou da cabeça) vira fantasma
        void AoAtirar(Vector3 origem, Vector3 direcao)
        {
            float alcance = 120f;
            if (Physics.Raycast(origem, direcao, out var bateu, alcance, ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)), QueryTriggerInteraction.Ignore))
                alcance = bateu.distance + 0.5f;
            foreach (var f in _falsos)
            {
                if (f.go == null || f.fantasma) continue;
                var p = f.go.transform.position;
                foreach (float altura in new[] { 1.2f, 1.6f })
                {
                    var ponto = p + Vector3.up * altura;
                    float ao = Vector3.Dot(ponto - origem, direcao);
                    if (ao < 0f || ao > alcance) continue;
                    if (Vector3.Distance(origem + direcao * ao, ponto) < 0.55f)
                    {
                        VirarFantasma(f);
                        break;
                    }
                }
            }
        }

        void VirarFantasma(Falso f)
        {
            f.fantasma = true;
            f.fantasmaDesde = Time.time;
            if (f.anim != null) f.anim.speed = 0f;
            Pintar(f.go, Palido());
            Efeitos.Som(SomDaMente.Sussurro, f.go.transform.position + Vector3.up * 1.5f, 0.8f, true, 30f);
        }

        void DevolverAmigo(AmigoZumbi a)
        {
            foreach (var r in a.apagados)
                if (r != null) r.enabled = true;
            if (a.go != null) Destroy(a.go);
            if (a.amigo != null) _escondidos.Remove(a.amigo);
        }

        void LimparTudo()
        {
            foreach (var v in _vultos) if (v.go != null) Destroy(v.go);
            foreach (var f in _falsos) if (f.go != null) Destroy(f.go);
            foreach (var a in _amigos) DevolverAmigo(a);
            _vultos.Clear();
            _falsos.Clear();
            _amigos.Clear();
        }

        // ------------------------------------------------ os Ocos sem IA ------------------------------------------------

        // um Oco dos modelos do jogo, sem a IA, a rede e a colisão, na camada de sempre, animado pelo "Speed" (0, parado)
        static GameObject NovoOco(bool preto)
        {
            var zm = ZombieManager.Instancia;
            if (zm == null || zm.modelos == null || zm.modelos.Length == 0) return null;
            var ocos = new List<Zumbi>();
            foreach (var m in zm.modelos) if (m != null && m.tipo == TipoDeZumbi.Oco) ocos.Add(m);
            if (ocos.Count == 0) foreach (var m in zm.modelos) if (m != null) ocos.Add(m);
            if (ocos.Count == 0) return null;
            var modelo = ocos[Random.Range(0, ocos.Count)];
            // criado embaixo de um pai desligado: nada acorda antes de a IA e a colisão saírem
            var pai = new GameObject("Alucinação (pai)");
            pai.SetActive(false);
            var go = Instantiate(modelo.gameObject, pai.transform);
            go.name = preto ? "Vulto (alucinação)" : "Oco (alucinação)";
            foreach (var z in go.GetComponentsInChildren<Zumbi>(true)) z.enabled = false;
            foreach (var ag in go.GetComponentsInChildren<NavMeshAgent>(true)) ag.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (preto) Pintar(go, Preto());
            go.transform.SetParent(null, false);
            Destroy(pai);
            if (anim != null) anim.SetFloat(SpeedId, 0f);
            return go;
        }

        static void Pintar(GameObject go, Material m)
        {
            if (m == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // o preto do vulto e o pálido do fantasma: o Unlit do URP (o jogo já usa, nas lâmpadas: entra na build)
        static Material Preto() => _preto != null ? _preto : _preto = NovoUnlit("Vulto", new Color(0.01f, 0.01f, 0.012f));
        static Material Palido() => _palido != null ? _palido : _palido = NovoUnlit("Fantasma", new Color(0.72f, 0.8f, 0.9f));

        static Material NovoUnlit(string nome, Color cor)
        {
            var s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) return null;
            var m = new Material(s) { name = nome + " (alucinação)" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
            m.color = cor;
            return m;
        }

        // ------------------------------------------------ os lugares ------------------------------------------------

        static Vector3 Plano(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        // um ponto no chão a este ângulo do olhar e a esta distância, que o olho vê (sem parede no meio)
        bool PontoNaVista(Transform olho, float angulo, float distancia, bool escuroDeVerdade, out Vector3 p)
        {
            var dir = Quaternion.Euler(0f, angulo, 0f) * Plano(olho.forward);
            p = NoChao(transform.position + dir * distancia, transform.position.y);
            if (Mathf.Abs(p.y - transform.position.y) > 4f) return false;
            var alvo = p + Vector3.up * 1.4f;
            return !Physics.Linecast(olho.position, alvo, ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)), QueryTriggerInteraction.Ignore);
        }

        static Vector3 NoChao(Vector3 p, float alturaPerto)
        {
            var de = new Vector3(p.x, alturaPerto + 3f, p.z);
            if (Physics.Raycast(de, Vector3.down, out var chao, 10f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos)), QueryTriggerInteraction.Ignore))
                return chao.point;
            return new Vector3(p.x, alturaPerto, p.z);
        }

        static void OlharPara(Transform t, Vector3 alvo)
        {
            var d = alvo - t.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(d);
        }
    }
}
