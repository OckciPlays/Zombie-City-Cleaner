using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O alarme dos carros abandonados (a pedido em 29/10/2026). Um carro em cada 10 tem alarme (pela semente do mundo e o
    /// número dele, igual em todas as máquinas), e o carro com alarme sempre tem bateria (<see cref="Padrao"/>).
    ///  - Dispara com um tiro no carro (<see cref="TiroNosCarros"/>, pelos pontos onde os tiros pararam, que o anfitrião
    ///    recebe), ao abrir o capô e ao trocar o pneu (apoiar ou erguer o macaco, tirar a roda: o mesmo <see cref="Susto"/>
    ///    do rastejante). Zumbi esbarrando não dispara (por enquanto, a pedido).
    ///  - Toca por <see cref="duracaoDoAlarme"/> s, ou até alguém tirar a bateria do carro (e ficar com ela).
    ///  - Enquanto toca, chama os zumbis a cada <see cref="chamarACada"/> s (ZombieManager.Alarme): à noite, até
    ///    <see cref="raioDoAlarme"/> m; de dia, ninguém sai da sombra (vão até a beira dela e ficam olhando).
    ///  - Em cada máquina: a sirene (uma das três, sempre a mesma para o mesmo carro; abafada de longe, como os outros
    ///    sons) e o pisca-alerta (as 4 lanternas e uma luz laranja piscando).
    /// O anfitrião escreve a lista dos que tocam (<see cref="_alarmes"/>); o resto sai dela.
    /// </summary>
    public partial class PecasDosCarrosDoMundo
    {
        [Header("O alarme")]
        [Tooltip("Um carro abandonado em cada tantos tem alarme (e esse sempre tem bateria).")]
        public int umAlarmeEmCada = 10;
        [Tooltip("Quanto tempo o alarme toca (s), se ninguém tirar a bateria antes.")]
        public float duracaoDoAlarme = 40f;
        [Tooltip("Até onde o alarme chama os zumbis (m).")]
        public float raioDoAlarme = 60f;
        [Tooltip("De quanto em quanto tempo o alarme chama os zumbis de novo, enquanto toca (s).")]
        public float chamarACada = 3f;
        [Tooltip("Até onde se ouve a sirene (m).")]
        public float alcanceDaSirene = 130f;
        [Range(0f, 1f)] public float volumeDaSirene = 0.6f;

        /// <summary>Teste (F1): todo carro abandonado tem alarme, mesmo sem bateria (em vez de 1 em cada 10).</summary>
        public readonly NetworkVariable<bool> AlarmeEmTodos = new NetworkVariable<bool>(false);

        NetworkList<int> _alarmes;
        // no anfitrião: quando cada alarme para, e a próxima chamada dos zumbis
        readonly Dictionary<int, float> _fimDoAlarme = new Dictionary<int, float>();
        float _proximaChamada;
        // em cada máquina: a sirene e as luzes de cada alarme tocando
        readonly Dictionary<int, AlarmeNaCena> _naCena = new Dictionary<int, AlarmeNaCena>();
        readonly List<int> _paraTirar = new List<int>();

        class AlarmeNaCena
        {
            public GameObject raiz;
            public AudioSource som;
            public AudioLowPassFilter filtro;
            public Renderer[] lanternas;
            public Light luz;
        }

        static SonsDoAlarme _sons;
        static bool _carregouOsSons;

        static SonsDoAlarme Sons
        {
            get
            {
                if (!_carregouOsSons)
                {
                    _carregouOsSons = true;
                    _sons = Resources.Load<SonsDoAlarme>("SonsDoAlarme");
                }
                return _sons;
            }
        }

        /// <summary>Este carro abandonado tem alarme (um em cada 10, pela semente do mundo e o número dele; ou todos, pelo F1).</summary>
        public bool TemAlarme(int carro) => carro >= 0 && (AlarmeEmTodos.Value || AlarmeDeFabrica(carro));

        /// <summary>O alarme deste carro está tocando.</summary>
        public bool AlarmeTocando(int carro) => IsSpawned && _alarmes.Contains(carro);

        /// <summary>Quantos alarmes estão tocando agora.</summary>
        public int AlarmesTocando => IsSpawned ? _alarmes.Count : 0;

        // o sorteio do alarme, à parte do das peças (não muda o que sobrou nos outros carros)
        bool AlarmeDeFabrica(int carro)
        {
            var dados = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            int semente = dados != null ? dados.Semente : 0;
            return new Sorteio(semente ^ 0x41A2, carro).Valor() < 1f / Mathf.Max(1, umAlarmeEmCada);
        }

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>O último alarme disparado, e por quê (o F1 mostra; no anfitrião).</summary>
        public string UltimoAlarme { get; private set; } = "";

        /// <summary>No anfitrião: dispara o alarme do carro, se ele tem alarme e bateria (forçar: o F1, em qualquer carro).</summary>
        void Disparar(int carro, string porque, bool forcar = false)
        {
            if (!IsServer || !IsSpawned || carro < 0) return;
            if (!forcar)
            {
                if (!TemAlarme(carro)) return;
                if (!AlarmeEmTodos.Value && !Estado(carro).pecas.BateriaAcende) return;   // sem bateria (tirada ou morta), não toca
            }
            _fimDoAlarme[carro] = Time.time + duracaoDoAlarme;   // de novo: recomeça a contar
            UltimoAlarme = $"carro {carro}: {porque} ({CicloDoDia.Relogio()})";
#if UNITY_EDITOR
            Debug.Log($"Alarme do {UltimoAlarme}");
#endif
            if (!_alarmes.Contains(carro)) _alarmes.Add(carro);
            _proximaChamada = 0f;   // chama os zumbis na hora
        }

        void Silenciar(int carro)
        {
            _fimDoAlarme.Remove(carro);
            if (IsSpawned && _alarmes.Contains(carro)) _alarmes.Remove(carro);
        }

        /// <summary>
        /// No anfitrião: os tiros (os pontos onde pararam, que vêm de quem atirou) que pegaram num carro abandonado com
        /// alarme disparam o alarme dele.
        /// </summary>
        public void TiroNosCarros(JogadorRede.Impacto[] impactos)
        {
            if (!IsServer || !IsSpawned || impactos == null) return;
            foreach (var i in impactos)
            {
                if (i.tipo != JogadorRede.TipoDeAcerto.Cenario && i.tipo != JogadorRede.TipoDeAcerto.Outro) continue;
                int carro = CarroPerto(i.ponto, 6f, true);
                if (carro >= 0) Disparar(carro, "tiro no carro");
            }
        }

        /// <summary>Teste (F1): dispara o alarme do carro abandonado mais perto do ponto (até 25 m), tenha ele alarme ou não.</summary>
        public bool DispararOMaisPerto(Vector3 ponto)
        {
            int carro = CarroPerto(ponto, 25f, false);
            if (carro < 0) return false;
            Disparar(carro, "F1", true);
            return true;
        }

        /// <summary>Teste (F1): todo carro com alarme, ou 1 em cada 10.</summary>
        public void AlarmeEmTodosOsCarros(bool todos)
        {
            if (IsServer && IsSpawned) AlarmeEmTodos.Value = todos;
        }

        // o carro do mundo mais perto do ponto (até o raio); "dentro": só se o ponto está na caixa dele (com uma folga: o
        // tiro para na superfície)
        static int CarroPerto(Vector3 p, float raio, bool dentro)
        {
            var dados = MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;
            var objetos = ObjetosDoMundo.Instancia;
            if (dados?.Objetos == null || objetos == null) return -1;
            var min = dados.BlocoDe(p.x - raio, p.z - raio);
            var max = dados.BlocoDe(p.x + raio, p.z + raio);
            int melhor = -1;
            float menor = raio * raio;
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                var lista = dados.Objetos.DoBloco(bx, bz);
                if (lista == null) continue;
                foreach (int i in lista)
                {
                    if (!objetos.CaixaDoCarro(i, out var pos, out var giro, out var caixa)) continue;
                    float d = (pos - p).sqrMagnitude;
                    if (d >= menor) continue;
                    if (dentro)
                    {
                        caixa.Expand(0.4f);
                        if (!caixa.Contains(Quaternion.Inverse(giro) * (p - pos))) continue;
                    }
                    menor = d;
                    melhor = i;
                }
            }
            return melhor;
        }

        // ------------------------------------------------------------------ a cada quadro (no Update)

        void AlarmesNoQuadro()
        {
            if (!IsSpawned)
            {
                if (_naCena.Count > 0) TirarTodosDaCena();
                return;
            }
            float agora = Time.time;
            var objetos = ObjetosDoMundo.Instancia;
            if (IsServer && _fimDoAlarme.Count > 0)
            {
                _paraTirar.Clear();
                foreach (var par in _fimDoAlarme)
                    if (agora >= par.Value) _paraTirar.Add(par.Key);
                foreach (int c in _paraTirar) Silenciar(c);
                if (agora >= _proximaChamada && _fimDoAlarme.Count > 0)
                {
                    _proximaChamada = agora + chamarACada;
                    foreach (int c in _fimDoAlarme.Keys)
                        if (objetos != null && objetos.PoseDoCarro(c, out var pos, out _, out _)) Zombies.ZombieManager.Alarme(pos, raioDoAlarme);
                }
            }

            // a sirene e as luzes dos que tocam, e fora os que pararam
            for (int i = 0; i < _alarmes.Count; i++)
                if (!_naCena.ContainsKey(_alarmes[i])) PorNaCena(_alarmes[i], objetos);
            _paraTirar.Clear();
            foreach (var par in _naCena)
                if (!_alarmes.Contains(par.Key)) _paraTirar.Add(par.Key);
            foreach (int c in _paraTirar)
            {
                Destroy(_naCena[c].raiz);
                _naCena.Remove(c);
            }
            if (_naCena.Count == 0) return;

            bool aceso = Mathf.Repeat(agora, 0.9f) < 0.45f;   // o pisca-alerta: 0,45 s aceso, 0,45 s apagado
            var cam = Camera.main;
            foreach (var a in _naCena.Values)
            {
                foreach (var r in a.lanternas) r.enabled = aceso;
                a.luz.enabled = aceso;
                if (cam == null) continue;
                Vector3 onde = a.som.transform.position;
                // de longe, abafada (como os outros sons); com a porta do bunker fechada no meio, nada
                float longe = Mathf.Clamp01(Vector3.Distance(cam.transform.position, onde) / alcanceDaSirene);
                a.filtro.cutoffFrequency = 22000f * Mathf.Pow(0.08f, longe);
                a.som.volume = BunkerRede.Isolado(cam.transform.position, onde) ? 0f : volumeDaSirene;
            }
        }

        void PorNaCena(int carro, ObjetosDoMundo objetos)
        {
            if (objetos == null || !objetos.CaixaDoCarro(carro, out var pos, out var giro, out var caixa)) return;   // tenta no próximo quadro
            var sons = Sons;
            var raiz = new GameObject($"Alarme do carro {carro}");
            raiz.transform.SetPositionAndRotation(pos, giro);
            var a = new AlarmeNaCena { raiz = raiz };

            // a sirene, do meio do carro
            var fonte = new GameObject("Sirene");
            fonte.transform.SetParent(raiz.transform, false);
            fonte.transform.localPosition = caixa.center;
            a.som = fonte.AddComponent<AudioSource>();
            a.som.clip = sons != null && sons.sirenes != null && sons.sirenes.Length > 0 ? sons.sirenes[carro % sons.sirenes.Length] : null;
            a.som.loop = true;
            a.som.playOnAwake = false;
            a.som.spatialBlend = 1f;
            a.som.rolloffMode = AudioRolloffMode.Linear;
            a.som.minDistance = 4f;
            a.som.maxDistance = alcanceDaSirene;
            a.som.dopplerLevel = 0f;
            a.som.priority = 40;
            a.som.volume = volumeDaSirene;
            a.filtro = fonte.AddComponent<AudioLowPassFilter>();
            if (a.som.clip != null) a.som.Play();

            // o pisca-alerta: as 4 lanternas nos cantos, na altura das lanternas do carro, e uma luz laranja em cima
            var lanternas = new List<Renderer>();
            float y = caixa.min.y + caixa.size.y * 0.42f;
            for (int lado = -1; lado <= 1; lado += 2)
            for (int ponta = -1; ponta <= 1; ponta += 2)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(l.GetComponent<Collider>());   // na hora: o raio do tiro não pode batê-la
                l.name = "Lanterna";
                l.transform.SetParent(raiz.transform, false);
                l.transform.localPosition = new Vector3(caixa.center.x + lado * caixa.extents.x * 0.78f, y, caixa.center.z + ponta * (caixa.extents.z + 0.01f));
                l.transform.localScale = new Vector3(0.22f, 0.09f, 0.05f);
                var r = l.GetComponent<MeshRenderer>();
                if (sons != null && sons.luz != null) r.sharedMaterial = sons.luz;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                lanternas.Add(r);
            }
            a.lanternas = lanternas.ToArray();
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.transform.localPosition = caixa.center + Vector3.up * (caixa.extents.y + 0.3f);
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.55f, 0.12f);
            luz.range = 9f;
            luz.intensity = 3f;
            luz.shadows = LightShadows.None;
            a.luz = luz;
            _naCena[carro] = a;
        }

        void TirarTodosDaCena()
        {
            foreach (var a in _naCena.Values)
                if (a.raiz != null) Destroy(a.raiz);
            _naCena.Clear();
        }
    }
}
