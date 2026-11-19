using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O corte dos pinheiros, em cada máquina (18/11/2026, a pedido: com os pinheiros novos, o corte "ficou ruim"):
    /// <list type="bullet">
    /// <item>o golpe na madeira: as lascas, o som do machado e a árvore tremendo;</item>
    /// <item>a árvore caída: inteira, deitada até encostar no chão (a queda e o tombo para quem viu cair), com o tronco que
    /// tem colisão (os jogadores, os NPCs, os zumbis e os bichos não atravessam) e os cortes das toras;</item>
    /// <item>a altura de cada pinheiro (o tamanho decide os golpes, as toras e o dano da queda).</item>
    /// </list>
    /// A lista das caídas vem do <see cref="MundoDeLakewood"/> (MundoDeLakewood.Troncos.cs).
    /// </summary>
    public partial class LakewoodNaCena
    {
        readonly Dictionary<int, float> _alturaDoPrefab = new Dictionary<int, float>();
        readonly Dictionary<long, ArvoreDeitada> _deitadas = new Dictionary<long, ArvoreDeitada>();

        /// <summary>A altura do pinheiro em pé (m): a cápsula do prefab é 0,9 da árvore, e a planta tem a escala dela.</summary>
        public float AlturaDoPinheiro(PlantaDaFloresta pl)
        {
            int i = IndiceDoPinheiro(pl.Especie);
            if (!_alturaDoPrefab.TryGetValue(i, out float h))
            {
                h = 0f;
                var prefab = pinheiros != null && i >= 0 && i < pinheiros.Length ? pinheiros[i] : null;
                var cap = prefab != null ? prefab.GetComponent<CapsuleCollider>() : null;
                if (cap != null) h = cap.height / 0.9f;
                if (h < 2f) h = 14f;
                _alturaDoPrefab[i] = h;
            }
            return h * pl.Escala;
        }

        /// <summary>Um golpe numa madeira (o pinheiro em pé ou o tronco caído): as lascas e o som do machado.</summary>
        public void GolpeNaMadeira(Vector3 ponto, Vector3 normal, float volume)
        {
            Efeitos.Lascas(ponto, normal);
            Efeitos.Som(SomDaMadeira.Golpes, ponto, volume, true, 45f);
            // (18/11/2026) os cavacos que o machado arranca ficam no chão
            var chao = ChaoNoMundo;
            if (chao != null) CorteNaMadeira.Cavacos(ponto, normal, (x, z) => chao.Altura(x, z), Random.Range(1, 3));
        }

        /// <summary>O golpe do machado num pinheiro em pé (o lado e a altura do corte): as lascas, o som e o tremor da árvore.</summary>
        public void GolpeNoPinheiro(long chave, int lado, float altura)
        {
            if (!ArvoresDoMundo.Planta(chave, out var pl)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - pl.Posicao).sqrMagnitude > 90f * 90f) return;
            var dir = Quaternion.Euler(0f, (lado & 3) * 90f, 0f) * Vector3.forward;
            var ponto = pl.Posicao + Vector3.up * altura + dir * ArvoresDoMundo.RaioDoTronco(pl) * 0.8f;
            GolpeNaMadeira(ponto, dir, 1f);
            var go = PinheiroDaPlanta(pl);
            if (go == null) return;
            var balanco = go.GetComponent<BalancoDaArvore>();
            if (balanco == null) balanco = go.AddComponent<BalancoDaArvore>();
            balanco.Bater(-dir, Mathf.Lerp(1.2f, 0.5f, Mathf.InverseLerp(6f, 20f, AlturaDoPinheiro(pl))));
        }

        // o objeto do pinheiro em pé dessa planta (perto: no bloco dela), ou null
        GameObject PinheiroDaPlanta(PlantaDaFloresta pl)
        {
            var noMapa = OrigemDeLakewood.NoMapa(pl.Posicao);
            var chave = ArvoresDoMundo.BlocoDe(pl.Posicao.x, pl.Posicao.z);
            if (!_blocos.TryGetValue(chave, out var b) || b.Arvores == null) return null;
            foreach (var go in b.Arvores)
            {
                if (go == null) continue;
                var p = go.transform.localPosition;
                if ((p.x - noMapa.x) * (p.x - noMapa.x) + (p.z - noMapa.z) * (p.z - noMapa.z) < 0.01f) return go;
            }
            return null;
        }

        /// <summary>
        /// Tira do bloco os pinheiros que acabaram de ser cortados (o objeto volta para a reserva e fica o toco), sem refazer o
        /// bloco inteiro (com ~300 pinheiros, refazer travava um pouco a cada árvore). Falso se o bloco não tem os objetos.
        /// </summary>
        bool TirarOsCortados(Bloco b)
        {
            if (b.Arvores == null || b.Plantas == null) return false;
            for (int k = 0; k < b.Plantas.Count; k++)
            {
                if (!CortadaNoBloco(b, k)) continue;
                var pl = b.Plantas[k];
                for (int j = 0; j < b.Arvores.Count; j++)
                {
                    var go = b.Arvores[j];
                    if (go == null) continue;
                    var p = go.transform.localPosition;
                    if ((p.x - pl.Posicao.x) * (p.x - pl.Posicao.x) + (p.z - pl.Posicao.z) * (p.z - pl.Posicao.z) >= 0.01f) continue;
                    go.SetActive(false);
                    _guardados[IndiceDoGuardado(go)].Push(go);
                    b.Arvores.RemoveAt(j);
                    PorOToco(b, pl);
                    break;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ as árvores caídas

        /// <summary>A lista das árvores caídas mudou: põe as novas (caindo, se acabaram de cair), acerta os cortes e tira as que acabaram.</summary>
        internal void AcertarTroncos(List<TroncoNoChao> lista)
        {
            var vistas = new HashSet<long>();
            float agora = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.ServerTime.TimeAsFloat : Time.time;
            foreach (var t in lista)
            {
                vistas.Add(t.chave);
                if (!_deitadas.TryGetValue(t.chave, out var d) || d == null)
                {
                    d = NovaDeitada(t, agora - t.caiu);
                    if (d == null) continue;
                    _deitadas[t.chave] = d;
                }
                d.Atualizar(t);
            }
            var foram = new List<long>();
            foreach (var kv in _deitadas) if (!vistas.Contains(kv.Key)) foram.Add(kv.Key);
            foreach (var k in foram)
            {
                if (_deitadas[k] != null) _deitadas[k].Sair();
                _deitadas.Remove(k);
            }
        }

        ArvoreDeitada NovaDeitada(TroncoNoChao t, float desdeQueCaiu)
        {
            if (!ArvoresDoMundo.Planta(t.chave, out var pl)) return null;
            int i = IndiceDoPinheiro(pl.Especie);
            if (pinheiros == null || i < 0 || i >= pinheiros.Length || pinheiros[i] == null) return null;
            var eixo = new GameObject("Árvore caída");
            eixo.transform.position = t.pe;
            var arvore = Instantiate(pinheiros[i], eixo.transform, false);
            arvore.name = "Copa e tronco";
            arvore.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, pl.Giro, 0f));
            arvore.transform.localScale = Vector3.one * pl.Escala;
            foreach (var c in arvore.GetComponentsInChildren<Collider>()) Destroy(c);
            var d = eixo.AddComponent<ArvoreDeitada>();
            d.Comecar(t, desdeQueCaiu);
            return d;
        }
    }

    /// <summary>
    /// A marca do tronco caído (no colisor dele): o machado acha a árvore deitada por aqui (Player/ArmaJogador.GolpeNaArvore).
    /// </summary>
    public class TroncoDeitado : MonoBehaviour
    {
        public long chave;
    }

    /// <summary>
    /// Uma árvore caída na cena (18/11/2026): gira em volta do pé até a inclinação do anfitrião (acelerando, como de verdade),
    /// bate no chão (o tombo, a poeira ao longo do tronco), fica deitada com o colisor do tronco (e o buraco no NavMesh) e
    /// os entalhes dos cortes das toras; quando o último pedaço sai, afunda e some.
    /// </summary>
    public class ArvoreDeitada : MonoBehaviour
    {
        TroncoNoChao _t;
        Quaternion _alvo;
        float _comeco;
        bool _bateu, _saindo;
        float _saiuEm;
        int _ladoDeCima;
        CapsuleCollider _colisor;
        NavMeshObstacle _obstaculo;
        // os entalhes de cada corte (um por divisa entre as toras), com a fundura desenhada
        readonly List<GameObject> _entalhes = new List<GameObject>();
        readonly List<float> _funduras = new List<float>();

        public void Comecar(TroncoNoChao t, float desdeQueCaiu)
        {
            _t = t;
            _alvo = t.Giro;
            _comeco = Time.time - Mathf.Max(0f, desdeQueCaiu);
            // quem chegou depois (ou estava longe) já vê deitada
            var cam = Camera.main;
            bool longe = cam != null && (cam.transform.position - t.pe).sqrMagnitude > 150f * 150f;
            if (desdeQueCaiu > MundoDeLakewood.SegundosCaindo + 0.5f || longe)
            {
                transform.rotation = _alvo;
                _bateu = true;
                Deitou();
            }
            else
            {
                transform.rotation = Quaternion.identity;
                Efeitos.Som(SomDaMadeira.Estalos, t.pe + Vector3.up * 1f, 1f, true, 60f);
            }
            // o lado do tronco que fica para cima (os entalhes das toras: o machado vem de cima)
            var cima = Quaternion.Inverse(_alvo) * Vector3.up;
            _ladoDeCima = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(cima.x, cima.z) * Mathf.Rad2Deg, 360f) / 90f) & 3;
        }

        /// <summary>O estado novo da lista (os cortes feitos e os golpes do corte de agora).</summary>
        public void Atualizar(TroncoNoChao t)
        {
            _t.cortes = t.cortes;
            _t.golpes = t.golpes;
            DesenharOsCortes();
        }

        /// <summary>O último pedaço saiu: o tronco afunda e some.</summary>
        public void Sair()
        {
            if (_saindo) return;
            _saindo = true;
            _saiuEm = Time.time;
            if (_colisor != null) _colisor.enabled = false;
            if (_obstaculo != null) _obstaculo.enabled = false;
            Efeitos.GolpeNoChao(_t.Ponto(_t.altura * 0.3f), 0.5f);
        }

        void Update()
        {
            if (_saindo)
            {
                float s = Time.time - _saiuEm;
                transform.position = _t.pe + Vector3.down * (s * s * 1.2f);
                if (s > 1.6f) Destroy(gameObject);
                return;
            }
            if (_bateu) return;
            float k = Mathf.Clamp01((Time.time - _comeco) / MundoDeLakewood.SegundosCaindo);
            // devagar no começo e cada vez mais depressa; um quique pequeno depois do tombo
            transform.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, _alvo, k * k);
            if (k < 1f) return;
            _bateu = true;
            transform.rotation = _alvo;
            Efeitos.Som(SomDaMadeira.Tombo, _t.Ponto(_t.altura * 0.5f), 1f, true, 90f);
            Efeitos.Som(SomDaMadeira.Estalos, _t.Ponto(_t.altura * 0.7f), 0.8f, true, 70f);   // os galhos quebrando (gravados)
            for (int i = 1; i <= 4; i++) Efeitos.GolpeNoChao(_t.Ponto(_t.altura * 0.22f * i), Mathf.Lerp(0.6f, 1.1f, i / 4f));
            Deitou();
        }

        // deitada: o colisor do tronco (sem a copa, que fica para atravessar) e o buraco no NavMesh dos zumbis e dos NPCs
        void Deitou()
        {
            var go = new GameObject("Tronco");
            go.transform.SetParent(transform, false);
            go.layer = 0;
            _colisor = go.AddComponent<CapsuleCollider>();
            _colisor.direction = 1;
            float comprido = _t.altura * 0.85f;
            _colisor.height = comprido;
            _colisor.center = new Vector3(0f, comprido * 0.5f, 0f);
            _colisor.radius = Mathf.Max(0.12f, _t.raio * 0.8f);
            go.AddComponent<TroncoDeitado>().chave = _t.chave;
            _obstaculo = go.AddComponent<NavMeshObstacle>();
            _obstaculo.shape = NavMeshObstacleShape.Box;
            _obstaculo.center = new Vector3(0f, _t.Comprimento * 0.5f, 0f);
            _obstaculo.size = new Vector3(_t.raio * 2.2f, _t.Comprimento, _t.raio * 2.2f);
            _obstaculo.carving = true;
            _obstaculo.carveOnlyStationary = true;
            DesenharOsCortes();
        }

        // os entalhes nas divisas das toras: os feitos, fundos; o de agora, pela fundura dos golpes
        void DesenharOsCortes()
        {
            if (!_bateu || _saindo) return;
            int n = Mathf.Max(1, (int)_t.pedacos);
            for (int i = 0; i < n; i++)
            {
                float fundura = i < _t.cortes ? 1f : i == _t.cortes ? _t.golpes / (float)Mathf.Max(1, _t.GolpesPorCorte) : 0f;
                while (_entalhes.Count <= i) { _entalhes.Add(null); _funduras.Add(-1f); }
                if (Mathf.Approximately(_funduras[i], fundura)) continue;
                _funduras[i] = fundura;
                if (fundura <= 0f)
                {
                    Apagar(_entalhes[i]);
                    _entalhes[i] = null;
                    continue;
                }
                var go = _entalhes[i];
                if (go == null)
                {
                    go = new GameObject("Entalhe da tora");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>();
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = CorteNaMadeira.Materiais(4 + Mathf.Min(i, 2));   // (o corte em cunha, 18/11/2026)
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    _entalhes[i] = go;
                }
                float s = _t.Corte(i);
                var filtro = go.GetComponent<MeshFilter>();
                var velha = filtro.sharedMesh;
                // (a casca de verdade, LakewoodNaCena.Casca.cs: no eixo do tronco caído, com o giro da planta)
                var cena = LakewoodNaCena.Instancia;
                bool temPlanta = ArvoresDoMundo.Planta(_t.chave, out var planta);
                float raio = cena != null && temPlanta ? cena.RaioDaCasca(planta, s, _ladoDeCima * Mathf.PI * 0.5f) : -1f;
                if (raio <= 0.01f) raio = _t.RaioEm(s) * 0.85f;
                System.Func<float, float> naCasca = null;
                if (cena != null && temPlanta) naCasca = ang => cena.RaioDaCasca(planta, s, ang);
                filtro.sharedMesh = CorteNaMadeira.Malha(_ladoDeCima, Mathf.Clamp01(fundura), s, raio,
                                                         (int)(_t.chave ^ (_t.chave >> 32)) + i * 7919, naCasca);
                if (velha != null) Destroy(velha);
            }
        }

        static void Apagar(GameObject go)
        {
            if (go == null) return;
            var f = go.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            Destroy(go);
        }

        void OnDestroy()
        {
            foreach (var go in _entalhes) Apagar(go);
        }
    }

    /// <summary>O pinheiro em pé tremendo com o golpe do machado (18/11/2026): balança em volta do pé e volta.</summary>
    public class BalancoDaArvore : MonoBehaviour
    {
        const float Duracao = 0.8f;
        Quaternion _base;
        Vector3 _eixo;
        float _resta, _forca;

        /// <summary>Um golpe empurrando a árvore para 'para' (no mundo), com a força em graus.</summary>
        public void Bater(Vector3 para, float graus)
        {
            if (_resta <= 0f) _base = transform.localRotation;
            para.y = 0f;
            var eixo = Vector3.Cross(Vector3.up, para.sqrMagnitude > 0.001f ? para.normalized : Vector3.forward);
            _eixo = transform.parent != null ? transform.parent.InverseTransformDirection(eixo) : eixo;
            _forca = graus;
            _resta = Duracao;
            enabled = true;
        }

        void Update()
        {
            if (_resta <= 0f)
            {
                enabled = false;
                return;
            }
            _resta -= Time.deltaTime;
            if (_resta <= 0f)
            {
                transform.localRotation = _base;
                enabled = false;
                return;
            }
            float t = Duracao - _resta;
            float a = Mathf.Sin(t * 26f) * _forca * (_resta / Duracao);
            transform.localRotation = Quaternion.AngleAxis(a, _eixo) * _base;
        }

        void OnDisable()
        {
            if (_resta > 0f) transform.localRotation = _base;
            _resta = 0f;
        }
    }
}
