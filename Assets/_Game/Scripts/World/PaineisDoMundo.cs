using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os painéis de metal na cena (09/11/2026, a sucata, 2ª leva): os de <see cref="PaineisDeSucata"/> até <see cref="Raio"/> m
    /// do jogador local, cada um com o <see cref="PainelDeSucata"/> (o E segurado desmonta), e os do F1
    /// (<see cref="DefinirDeTeste"/>). Nada vai pela rede: cada máquina põe os seus, iguais em todas, e tira os desmontados
    /// (a lista da rede do <see cref="MundoDaPartida"/>). Os modelos vêm do <c>Resources/PaineisDeSucata.prefab</c> (as três
    /// malhas do <c>Loot_Sucata.fbx</c> com o material da paleta dos itens; Zombie City Cleaner > Armas > Montar os painéis
    /// de sucata). Fora das cenas: o <see cref="MundoDaPartida"/> põe este no objeto dele, no Awake.
    /// <para>
    /// A altura: a do mapa (<see cref="PaineisDeSucata.AlturaDoChao"/>) e, assim que a colisão de baixo existe, um raio para
    /// baixo acerta o painel no chão de verdade (a calçada, a garagem, o pátio do posto) e o deita na inclinação dele.
    /// </para>
    /// </summary>
    public class PaineisDoMundo : MonoBehaviour
    {
        /// <summary>Até onde os painéis aparecem em volta do jogador local (m).</summary>
        public const float Raio = 90f;
        /// <summary>O caminho do prefab com os três modelos, em Resources.</summary>
        public const string Recurso = "PaineisDeSucata";

        public static PaineisDoMundo Instancia { get; private set; }

        static readonly HashSet<int> _desmontados = new HashSet<int>();
        // os do F1: (x, y, z) e o giro + 1000 × o modelo; o w negativo é o desmontado
        static readonly List<Vector4> _deTeste = new List<Vector4>();

        /// <summary>O painel com este número já foi desmontado (ou não existe)?</summary>
        public static bool Desmontado(int indice)
        {
            if (indice >= MundoDaPartida.PrimeiroPainelDeTeste)
            {
                int k = indice - MundoDaPartida.PrimeiroPainelDeTeste;
                return k >= _deTeste.Count || _deTeste[k].w < 0f;
            }
            return _desmontados.Contains(indice);
        }

        /// <summary>Os desmontados (a lista da rede): os que estão na cena somem.</summary>
        public static void DefinirDesmontados(IEnumerable<int> indices)
        {
            _desmontados.Clear();
            if (indices != null) foreach (int i in indices) _desmontados.Add(i);
            Instancia?.TirarOsDesmontados();
        }

        /// <summary>Os painéis do F1 (a lista da rede).</summary>
        public static void DefinirDeTeste(IEnumerable<Vector4> paineis)
        {
            _deTeste.Clear();
            if (paineis != null) _deTeste.AddRange(paineis);
            if (Instancia != null) Instancia._refazerOsDeTeste = true;
        }

        /// <summary>O painel foi desmontado agora (o aviso do anfitrião chega antes da lista): some já.</summary>
        public static void Tirar(int indice)
        {
            if (indice < MundoDaPartida.PrimeiroPainelDeTeste) _desmontados.Add(indice);
            else
            {
                int k = indice - MundoDaPartida.PrimeiroPainelDeTeste;
                if (k < _deTeste.Count) _deTeste[k] = new Vector4(_deTeste[k].x, _deTeste[k].y, _deTeste[k].z, -1f);
            }
            if (Instancia != null && Instancia._vivos.TryGetValue(indice, out var p))
            {
                if (p != null) Destroy(p.gameObject);
                Instancia._vivos.Remove(indice);
            }
        }

        /// <summary>Quantos estão na cena agora (os de perto e os do F1).</summary>
        public int NaCena => _vivos.Count;

        /// <summary>O painel na cena com este número, ou null.</summary>
        public PainelDeSucata NaCenaO(int indice) => _vivos.TryGetValue(indice, out var p) ? p : null;

        Mesh[] _malhas;
        Material[] _materiais;
        bool _semModelos;
        // de onde vêm os painéis: o mundo sorteado ou o Lakewood (12/11/2026; World/BeiraDoMapa.cs)
        IBeiraDoMapa _fonte;
        PaineisDeSucata _paineis;
        Transform _raiz;
        bool _refazerOsDeTeste;
        float _proximaRevisao;
        readonly Dictionary<int, PainelDeSucata> _vivos = new Dictionary<int, PainelDeSucata>();
        readonly List<int> _sobra = new List<int>();
        readonly RaycastHit[] _acertos = new RaycastHit[8];

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            Limpar();
            if (Instancia == this) Instancia = null;
        }

        void Limpar()
        {
            _vivos.Clear();
            if (_raiz != null) Destroy(_raiz.gameObject);
            _raiz = null;
            _fonte = null;
            _paineis = null;
        }

        bool Modelos()
        {
            if (_malhas != null) return true;
            if (_semModelos) return false;
            var prefab = Resources.Load<GameObject>(Recurso);
            var malhas = new Mesh[PaineisDeSucata.Modelos];
            var materiais = new Material[PaineisDeSucata.Modelos];
            if (prefab != null)
                for (int k = 0; k < PaineisDeSucata.Modelos && k < prefab.transform.childCount; k++)
                {
                    var filho = prefab.transform.GetChild(k);
                    malhas[k] = filho.GetComponent<MeshFilter>()?.sharedMesh;
                    materiais[k] = filho.GetComponent<MeshRenderer>()?.sharedMaterial;
                }
            foreach (var m in malhas)
                if (m == null)
                {
                    _semModelos = true;
                    Debug.LogWarning($"[Sucata] Sem os modelos dos painéis (Resources/{Recurso}.prefab: Zombie City Cleaner > Armas > Montar os painéis de sucata).");
                    return false;
                }
            _malhas = malhas;
            _materiais = materiais;
            return true;
        }

        void Update()
        {
            var jogo = MundoDoJogo.Atual;
            var fonte = jogo != null && jogo.PartidaPronta ? BeiraDoMapa.Atual : null;
            if (fonte != _fonte)
            {
                Limpar();
                _fonte = fonte;
                if (fonte != null)
                {
                    _paineis = fonte.Paineis;
                    _raiz = new GameObject("Painéis de sucata").transform;
                    _refazerOsDeTeste = true;
                }
            }
            if (_paineis == null || !Modelos()) return;
            if (_refazerOsDeTeste) RefazerOsDeTeste();
            if (Time.unscaledTime >= _proximaRevisao)
            {
                _proximaRevisao = Time.unscaledTime + 0.5f;
                Revisar();
            }
            SomDeQuemDesmonta();
        }

        void Revisar()
        {
            var local = JogadorRede.Local;
            if (local == null) return;
            var c = local.transform.position;
            var min = _fonte.BlocoDe(c.x - Raio, c.z - Raio);
            var max = _fonte.BlocoDe(c.x + Raio, c.z + Raio);
            float r2 = Raio * Raio;
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                var lista = _paineis.DoBloco(bx, bz);
                if (lista == null) continue;
                foreach (int i in lista)
                {
                    if (_vivos.ContainsKey(i) || _desmontados.Contains(i)) continue;
                    var p = _paineis.Todos[i];
                    if ((p.Posicao - new Vector2(c.x, c.z)).sqrMagnitude > r2) continue;
                    Criar(i, new Vector3(p.Posicao.x, _fonte.AlturaDoPainel(p), p.Posicao.y), p.Giro, p.Modelo, true);
                }
            }
            // os que ficaram longe saem (os do F1 ficam); os que ainda não acharam o chão tentam de novo
            _sobra.Clear();
            float longe = (Raio + 15f) * (Raio + 15f);
            foreach (var kv in _vivos)
            {
                var pv = kv.Value;
                if (pv == null) { _sobra.Add(kv.Key); continue; }
                if (kv.Key < MundoDaPartida.PrimeiroPainelDeTeste)
                {
                    var d = pv.transform.position - c;
                    if (d.x * d.x + d.z * d.z > longe) { _sobra.Add(kv.Key); continue; }
                }
                if (!pv.assentado && pv.tentativas < 40) Assentar(pv);
            }
            foreach (int i in _sobra)
            {
                if (_vivos.TryGetValue(i, out var pv) && pv != null) Destroy(pv.gameObject);
                _vivos.Remove(i);
            }
        }

        void TirarOsDesmontados()
        {
            _sobra.Clear();
            foreach (var kv in _vivos) if (Desmontado(kv.Key)) _sobra.Add(kv.Key);
            foreach (int i in _sobra)
            {
                if (_vivos.TryGetValue(i, out var pv) && pv != null) Destroy(pv.gameObject);
                _vivos.Remove(i);
            }
        }

        void RefazerOsDeTeste()
        {
            _refazerOsDeTeste = false;
            if (_raiz == null) return;
            for (int k = 0; k < _deTeste.Count; k++)
            {
                int indice = MundoDaPartida.PrimeiroPainelDeTeste + k;
                var v = _deTeste[k];
                bool vivo = _vivos.TryGetValue(indice, out var pv) && pv != null;
                if (v.w < 0f)
                {
                    if (vivo) Destroy(pv.gameObject);
                    _vivos.Remove(indice);
                    continue;
                }
                if (vivo) continue;
                int modelo = Mathf.Clamp(Mathf.FloorToInt(v.w / 1000f), 0, PaineisDeSucata.Modelos - 1);
                Criar(indice, new Vector3(v.x, v.y, v.z), v.w - modelo * 1000f, (byte)modelo, true);
            }
        }

        void Criar(int indice, Vector3 onde, float giro, byte modelo, bool assentar)
        {
            var malha = _malhas[modelo % _malhas.Length];
            var go = new GameObject($"Painel {indice} ({PaineisDeSucata.NomeDoModelo(modelo)})");
            go.transform.SetParent(_raiz, false);
            go.transform.SetPositionAndRotation(onde, Quaternion.Euler(0f, giro, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = _materiais[modelo % _materiais.Length];
            // a caixa da mira: a da malha, com pelo menos 12 cm de altura (a chapa deitada tem 7: difícil de mirar)
            var bc = go.AddComponent<BoxCollider>();
            var b = malha.bounds;
            bc.center = new Vector3(b.center.x, Mathf.Max(b.center.y, 0.06f), b.center.z);
            bc.size = new Vector3(b.size.x, Mathf.Max(b.size.y, 0.12f), b.size.z);
            var p = go.AddComponent<PainelDeSucata>();
            p.indice = indice;
            p.modelo = modelo;
            _vivos[indice] = p;
            if (assentar) Assentar(p);
        }

        // um raio para baixo, de 1,2 m acima: o chão mais alto que não é um painel, um carro, um jogador ou um zumbi
        void Assentar(PainelDeSucata p)
        {
            p.tentativas++;
            var t = p.transform;
            var pos = t.position;
            var de = new Vector3(pos.x, pos.y + 1.2f, pos.z);
            int n = Physics.RaycastNonAlloc(de, Vector3.down, _acertos, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            float melhor = float.NegativeInfinity;
            Vector3 normal = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                var h = _acertos[i];
                if (h.collider.attachedRigidbody != null || h.collider.GetComponent<PainelDeSucata>() != null) continue;
                if (h.point.y <= melhor) continue;
                melhor = h.point.y;
                normal = h.normal;
            }
            if (float.IsNegativeInfinity(melhor)) return;
            if (Vector3.Angle(normal, Vector3.up) > 30f) normal = Vector3.up;
            float giro = t.eulerAngles.y;
            t.SetPositionAndRotation(new Vector3(pos.x, melhor + 0.005f, pos.z),
                Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, giro, 0f));
            p.assentado = true;
        }

        // o jogador local segurando o E num painel: as pancadas no metal repetem nele (o barulho do fim vem do anfitrião)
        PainelDeSucata _desmontando;
        float _proximoSom;

        void SomDeQuemDesmonta()
        {
            var local = JogadorRede.Local;
            var arma = local != null ? local.GetComponent<ArmaJogador>() : null;
            var alvo = arma != null ? arma.SegurandoAgora as PainelDeSucata : null;
            if (alvo == null)
            {
                _desmontando = null;
                return;
            }
            if (alvo != _desmontando)
            {
                _desmontando = alvo;
                _proximoSom = 0f;
            }
            if (Time.time < _proximoSom) return;
            Efeitos.Som(SomDaSucata.Batendo, alvo.Meio, 0.85f, true, 30f);
            _proximoSom = Time.time + 0.62f;
        }
    }
}
