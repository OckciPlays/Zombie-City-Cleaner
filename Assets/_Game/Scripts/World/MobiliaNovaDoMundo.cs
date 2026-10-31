using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A mobília nova no mundo (Plano_Mobilia.md da pasta de arte, a etapa 4), no objeto "Mundo" da cena Mundo (com o
    /// NetworkObject):
    ///  - ao nascer, registra os de longe de cada modelo (<see cref="MobiliaNova.Registrar"/>: as obras montadas em outra
    ///    thread ganham o desenho e as caixas dos móveis novos) e prepara um molde de cada prefab para os de perto (sem o
    ///    colisor do corpo: a colisão é a caixa na obra; as partes que abrem com um gatilho para a mira, fora do NavMesh);
    ///  - cada quadro, as construções com móveis novos (<see cref="MobiliaDaConstrucao"/>) mostram os de longe perto do
    ///    jogador local (<see cref="raioDeLonge"/>) e trocam pelos de perto, com as portas e as gavetas que abrem, quando ele
    ///    está nelas (<see cref="raioDePerto"/>, no mesmo andar), poucos por quadro;
    ///  - as partes abertas são uma lista na rede (o anfitrião manda): o E de quem joga vai ao anfitrião
    ///    (<see cref="AlternarRpc"/>), que abre ou fecha, e cada máquina mostra igual. Fora de uma partida (sem a rede), abre
    ///    e fecha direto.
    /// As cores (as variações) são as dos de longe, pintadas uma vez por malha e variação.
    /// </summary>
    public class MobiliaNovaDoMundo : NetworkBehaviour
    {
        [Tooltip("Os prefabs da mobília nova (Prefabs/MobiliaNova; o nome é o do modelo).")]
        public GameObject[] prefabs;
        [Tooltip("Até onde os de longe dos móveis novos aparecem em volta do jogador local (m, da caixa da obra).")]
        public float raioDeLonge = 60f;
        [Tooltip("Até onde os de perto, com as portas e as gavetas, aparecem (m, da caixa da obra, no mesmo andar).")]
        public float raioDePerto = 8f;
        [Tooltip("Móveis de perto postos por quadro (os pontos de uma obra entram aos poucos).")]
        [Min(1)] public int pecasPorQuadro = 12;
        [Tooltip("Malhas de longe feitas por quadro.")]
        [Min(1)] public int malhasPorQuadro = 2;

        public static MobiliaNovaDoMundo Instancia { get; private set; }
        /// <summary>O material dos móveis novos (o dos prefabs).</summary>
        public Material Material { get; private set; }
        /// <summary>Quantos de perto estão na cena (o F1 mostra).</summary>
        public static int Perto => MobiliaDaConstrucao.QuantosDePerto;

        NetworkList<ulong> _abertas;
        // as abertas desta máquina: o espelho da lista da rede (ou, fora da rede, o estado daqui)
        readonly HashSet<ulong> _aqui = new HashSet<ulong>();
        readonly Dictionary<ulong, ParteDoMovel> _vivas = new Dictionary<ulong, ParteDoMovel>();
        readonly Dictionary<string, Molde> _moldes = new Dictionary<string, Molde>();
        readonly Dictionary<(Mesh, int), Mesh> _pintadas = new Dictionary<(Mesh, int), Mesh>();
        Transform _guardados;

        sealed class Molde
        {
            public GameObject objeto;
            public Mesh[] malhas;
        }

        const int CamadaDasPartes = 2;   // Ignore Raycast: fora do NavMesh (camadas 0 e 1) e dos raios comuns; a mira olha todas

        void Awake()
        {
            _abertas = new NetworkList<ulong>();
            Instancia = this;
            _guardados = new GameObject("Moldes da mobília nova").transform;
            _guardados.SetParent(transform, false);
            _guardados.gameObject.SetActive(false);
            var longes = new List<(string, Mesh)>();
            foreach (var prefab in prefabs ?? new GameObject[0])
            {
                if (prefab == null || !MedidasDaMobilia.Tem(prefab.name)) continue;
                var go = Instantiate(prefab, _guardados, false);
                go.name = prefab.name;
                foreach (var c in go.GetComponents<MeshCollider>()) DestroyImmediate(c);
                foreach (var p in go.GetComponentsInChildren<ParteQueAbre>(true))
                {
                    var caixa = p.GetComponent<BoxCollider>();
                    if (caixa == null) continue;
                    caixa.isTrigger = true;
                    caixa.size += Vector3.one * 0.03f;
                    p.gameObject.layer = CamadaDasPartes;
                    p.gameObject.AddComponent<ParteDoMovel>().parte = p;
                }
                var filtros = go.GetComponentsInChildren<MeshFilter>(true);
                var molde = new Molde { objeto = go, malhas = new Mesh[filtros.Length] };
                for (int i = 0; i < filtros.Length; i++) molde.malhas[i] = filtros[i].sharedMesh;
                _moldes[prefab.name] = molde;
                if (Material == null) Material = go.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
                var longe = go.transform.Find(prefab.name + "_Longe");
                var mf = longe != null ? longe.GetComponent<MeshFilter>() : go.GetComponent<MeshFilter>();
                if (mf != null) longes.Add((prefab.name, mf.sharedMesh));
            }
            MobiliaNova.Registrar(longes);
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            MobiliaNova.Limpar();
            // (sem a variação, a pintada é a do modelo, um asset)
            foreach (var (k, m) in _pintadas) if (m != null && m != k.Item1) Destroy(m);
            _pintadas.Clear();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _abertas.Clear();
            _abertas.OnListChanged += AoMudar;
            Sincronizar();
        }

        public override void OnNetworkDespawn()
        {
            _abertas.OnListChanged -= AoMudar;
            _aqui.Clear();
            foreach (var p in _vivas.Values) if (p != null && p.parte != null) p.parte.Definir(false);
        }

        void AoMudar(NetworkListEvent<ulong> e) => Sincronizar();

        // as partes de perto ficam como a lista da rede
        void Sincronizar()
        {
            if (!IsSpawned) return;
            _aqui.Clear();
            foreach (var k in _abertas) _aqui.Add(k);
            foreach (var (k, p) in _vivas)
                if (p != null && p.parte != null) p.parte.Definir(_aqui.Contains(k));
        }

        void Update()
        {
            Vector3? olho = JogadorRede.Local != null ? JogadorRede.Local.transform.position + Vector3.up * 1.6f
                          : Camera.main != null ? Camera.main.transform.position : (Vector3?)null;
            if (olho.HasValue) MobiliaDaConstrucao.Revisar(olho.Value, raioDeLonge, raioDePerto, pecasPorQuadro, malhasPorQuadro);
        }

        // ------------------------------------------------------------ as partes que abrem

        /// <summary>O E numa parte: pelo anfitrião (na rede) ou direto.</summary>
        public static void Alternar(ParteDoMovel p)
        {
            var m = Instancia;
            if (m == null || !m.IsSpawned)
            {
                bool aberta = !(p.parte != null && p.parte.Aberta);
                if (m != null && aberta) m._aqui.Add(p.chave);
                else m?._aqui.Remove(p.chave);
                p.parte?.Definir(aberta);
                return;
            }
            m.AlternarRpc(p.chave);
        }

        /// <summary>Quem joga → anfitrião: abre ou fecha a parte.</summary>
        [Rpc(SendTo.Server)]
        void AlternarRpc(ulong chave)
        {
            int i = _abertas.IndexOf(chave);
            if (i >= 0) _abertas.RemoveAt(i);
            else _abertas.Add(chave);
        }

        /// <summary>Uma parte de perto entrou: fica como está na rede (sem a animação).</summary>
        internal void Registrar(ParteDoMovel p)
        {
            _vivas[p.chave] = p;
            p.parte?.Definir(_aqui.Contains(p.chave), ja: true);
        }

        internal void Esquecer(ParteDoMovel p)
        {
            if (_vivas.TryGetValue(p.chave, out var v) && v == p) _vivas.Remove(p.chave);
        }

        // ------------------------------------------------------------ os moldes

        /// <summary>Um móvel de perto do modelo, na variação, filho de 'pai' (null sem o prefab).</summary>
        internal GameObject Novo(string modelo, int variacao, Transform pai)
        {
            if (!_moldes.TryGetValue(modelo, out var molde)) return null;
            var go = Instantiate(molde.objeto, pai, false);
            go.name = modelo;
            var t = CatalogoDaMobilia.DoModelo(modelo);
            var v = t?.variacoes != null && variacao < t.variacoes.Length ? t.variacoes[variacao] : null;
            if (v == null) return go;
            var filtros = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filtros.Length && i < molde.malhas.Length; i++)
            {
                var orig = molde.malhas[i];
                if (orig == null) continue;
                if (!_pintadas.TryGetValue((orig, variacao), out var pintada))
                {
                    pintada = CatalogoDaMobilia.Pintar(orig, v);
                    _pintadas[(orig, variacao)] = pintada;
                }
                filtros[i].sharedMesh = pintada;
            }
            return go;
        }
    }
}
