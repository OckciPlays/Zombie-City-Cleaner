using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A cena do mapa fixo (29/10/2026, a pedido: "construir o mapa aos poucos", só com o relevo, as montanhas, a mata, os
    /// rios, o lago, a quarentena e as estradas principais): o <see cref="DesenhoDoMapa"/> no chão, montado aqui na hora.
    ///  - o terreno em blocos de 64 m em volta da câmera (2 m entre os pontos até 120 m, 4 m até 220 m, 8 m até 360 m e 16 m
    ///    além), montados em outra thread, com a colisão perto do jogador e do carro;
    ///  - sem a neblina (<see cref="AmbienteDoDia.SemNeblina"/>), o relevo de longe (o mapa inteiro em cores sólidas) e a
    ///    câmera até 16 km;
    ///  - a água dos lagos, do brejo e dos rios, as estradas com as pontes (com a colisão), a cerca e a parede invisível da
    ///    quarentena e os bloqueios de contêineres onde as estradas saem;
    ///  - as plantas (as árvores, o mato e a grama) de cada bloco, em instâncias;
    ///  - o carro, posto pelo anfitrião ao lado da partida.
    /// </summary>
    public class MapaFixoNaCena : MonoBehaviour
    {
        public DesenhoDoMapa desenho;
        public Material materialDoChao, materialDaAgua, materialDaCidade, materialDaVegetacao;
        [Tooltip("As malhas das plantas: 3 por espécie (perto, longe 1 e longe 2), na ordem de Floresta.Nomes.")]
        public Mesh[] malhasDasPlantas;
        [Tooltip("O carro que se dirige, posto pelo anfitrião ao lado da partida.")] public GameObject carro;
        public Vector3 lugarDoCarro;
        public float giroDoCarro;
        public Vector3 partida;

        [Header("Até onde (m)")]
        public float raioComNeblina = 200f;
        public float raioSemNeblina = 480f;
        public float plantasComNeblina = 165f, plantasSemNeblina = 320f;
        public float raioDaGrama = 45f;
        public float raioDeColisao = 110f;
        public float raioDaCerca = 450f;

        public static MapaFixoNaCena Instancia { get; private set; }
        public ChaoDoMapaFixo Chao { get; private set; }
        public int Blocos => _blocos.Count;
        public int BlocosComColisao { get; private set; }
        public int PlantasNaVista { get; private set; }

        sealed class Bloco
        {
            public Vector2Int Chave;
            public GameObject Objeto;
            public MeshRenderer Desenho;
            public MeshCollider Colisor;
            public Mesh Malha;
            public float Passo = -1f, PassoQuerido, PassoMontando;
            public Task<MalhaDoBloco> Montando;
            public bool QuerColisao;
            public Task<List<PlantaDaFloresta>> MontandoPlantas;
            public List<PlantaDaFloresta> Plantas;
            public bool PlantasComGrama;
            public float VistoEm;
        }

        readonly Dictionary<Vector2Int, Bloco> _blocos = new Dictionary<Vector2Int, Bloco>();
        readonly List<GameObject> _longe = new List<GameObject>();
        readonly Dictionary<int, GameObject> _cerca = new Dictionary<int, GameObject>();
        List<Vector2> _linhaDaCerca;
        bool _semNeblina, _carroPosto;
        float _farOriginal = 150f, _proximaRevisao;
        Transform _pai;

        // as listas das plantas para desenhar: uma por espécie e LOD
        List<Matrix4x4>[] _listas;
        MaterialPropertyBlock[] _blocosDasPlantas;
        Vector3 _ondeJuntou = Vector3.one * 1e9f;
        float _juntouEm;

        const int PorPedacoDaCerca = 100, PorPedacoDaEstrada = 60;

        void Awake()
        {
            Instancia = this;
            _pai = new GameObject("Mapa fixo (montado na hora)").transform;
            _pai.SetParent(transform, false);
            var t0 = Time.realtimeSinceStartup;
            var relevo = new RelevoDoDesenho(desenho);
            Chao = new ChaoDoMapaFixo(desenho, relevo);
            MontarAgua();
            MontarEstradas();
            MontarParede();
            MontarBloqueios();
            MontarLonge();
            _linhaDaCerca = Chao.LinhaDaCerca();
            // o chão em volta da partida, com a colisão, antes de alguém nascer
            foreach (var chave in BlocosEm(new Vector2(partida.x, partida.z), 140f))
            {
                var b = Novo(chave);
                b.QuerColisao = true;
                b.PassoQuerido = 2f;
                Entregar(b, Chao.Terreno(chave.x, chave.y, 2f), 2f);
            }
            Debug.Log($"[Mapa fixo] Montado em {Time.realtimeSinceStartup - t0:F1} s: {Chao.Estradas.Count} estradas, {desenho.lagos.Count} lagos, {desenho.rios.Count} rios.");
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            AmbienteDoDia.SemNeblina = false;
        }

        // ------------------------------------------------------------------ o que é montado uma vez

        GameObject Objeto(string nome, Mesh malha, Material material, int camada = 0, bool colisao = false)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(_pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (colisao) go.AddComponent<MeshCollider>().sharedMesh = malha;
            go.isStatic = true;
            return go;
        }

        static Mesh ParaMesh(MalhaDoBloco m, string nome)
        {
            var mesh = new Mesh { name = nome };
            m.Para(mesh);
            return mesh;
        }

        void MontarAgua()
        {
            int agua = LayerMask.NameToLayer("Water");
            if (agua < 0) agua = 0;
            for (int l = -1; l < desenho.lagos.Count; l++)
            {
                var m = Chao.AguaDoLago(l);
                if (m != null) Objeto(l < 0 ? "Água do brejo" : $"Água: {desenho.lagos[l].nome}", ParaMesh(m, "Agua"), materialDaAgua, agua);
            }
            for (int r = 0; r < desenho.rios.Count; r++)
            {
                var m = Chao.AguaDoRio(r);
                if (m != null) Objeto($"Rio: {desenho.rios[r].nome}", ParaMesh(m, "Rio"), materialDaAgua, agua);
            }
        }

        void MontarEstradas()
        {
            for (int e = 0; e < Chao.Estradas.Count; e++)
            {
                int n = Chao.Estradas[e].Pontos.Length;
                for (int ini = 0; ini < n - 1; ini += PorPedacoDaEstrada)
                {
                    var malha = Chao.Pedaco(e, ini, Mathf.Min(n - 1, ini + PorPedacoDaEstrada)).ParaMesh($"{Chao.Estradas[e].Via.nome} {ini}");
                    Objeto($"Estrada: {Chao.Estradas[e].Via.nome} ({ini})", malha, materialDaCidade, 0, true);
                }
            }
        }

        // a parede invisível da quarentena: caixas de 30 m, 2 m para fora da cerca, de 15 m abaixo do chão a 60 m acima
        void MontarParede()
        {
            if (desenho.quarentena.Count < 3) return;
            var linha = DesenhoDoMapa.Contorno(desenho.quarentena);
            var meio = Linhas.Centro(linha);
            var parede = new GameObject("Parede da quarentena");
            parede.transform.SetParent(_pai, false);
            for (int s = 0; s < linha.Count; s++)
            {
                var a = linha[s];
                var b = linha[(s + 1) % linha.Count];
                float l = Vector2.Distance(a, b);
                int partes = Mathf.Max(1, Mathf.CeilToInt(l / 30f));
                for (int q = 0; q < partes; q++)
                {
                    var p0 = Vector2.Lerp(a, b, q / (float)partes);
                    var p1 = Vector2.Lerp(a, b, (q + 1) / (float)partes);
                    var c = (p0 + p1) * 0.5f;
                    var fora = (c - meio).normalized;
                    var dir = (p1 - p0).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    if (Vector2.Dot(normal, fora) < 0f) normal = -normal;
                    c += normal * 2f;
                    float h = Chao.Altura(c.x, c.y);
                    var go = new GameObject("Parede");
                    go.transform.SetParent(parede.transform, false);
                    go.transform.SetPositionAndRotation(new Vector3(c.x, h + 22f, c.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)));
                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(1f, 75f, Vector2.Distance(p0, p1) + 1f);
                }
            }
        }

        // os bloqueios: três contêineres atravessados na estrada, os sacos de areia e o blindado do lado de dentro
        void MontarBloqueios()
        {
            var meio = desenho.quarentena.Count >= 3 ? Linhas.Centro(DesenhoDoMapa.Contorno(desenho.quarentena)) : Vector2.zero;
            var conteiner = new Mesh[4];
            for (int v = 0; v < 4; v++) conteiner[v] = ModelosDaEstrada.Conteiner(v).Malha.ParaMesh("Conteiner");
            var sacos = ModelosDaEstrada.Sacos();
            var mSacos = sacos.Malha.ParaMesh("Sacos");
            var blindado = ModelosDaEstrada.Blindado();
            var mBlindado = blindado.Malha.ParaMesh("Blindado");
            int k = 0;
            foreach (var b in desenho.bloqueios)
            {
                // a direção da estrada no bloqueio
                Vector2 dir = Vector2.up;
                float melhor = float.MaxValue;
                foreach (var e in Chao.Estradas)
                    for (int i = 1; i < e.Pontos.Length; i++)
                    {
                        float dd = Linhas.DistanciaDoSegmento(b.ponto, e.Pontos[i - 1], e.Pontos[i], out _);
                        if (dd < melhor)
                        {
                            melhor = dd;
                            dir = (e.Pontos[i] - e.Pontos[i - 1]).normalized;
                        }
                    }
                if (Vector2.Dot(dir, meio - b.ponto) < 0f) dir = -dir;   // para dentro do mapa
                var lado = new Vector2(dir.y, -dir.x);
                var go = new GameObject($"Bloqueio: {b.nome}");
                go.transform.SetParent(_pai, false);
                void Por(Mesh malha, ModelosDaEstrada.Modelo modelo, Vector2 p, float giro)
                {
                    float h = Chao.Altura(p.x, p.y);
                    var peca = new GameObject(malha.name);
                    peca.transform.SetParent(go.transform, false);
                    peca.transform.SetPositionAndRotation(new Vector3(p.x, h, p.y), Quaternion.Euler(0f, giro, 0f));
                    peca.AddComponent<MeshFilter>().sharedMesh = malha;
                    peca.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
                    foreach (var cx in modelo.Colisao)
                    {
                        var box = new GameObject("Colisão").AddComponent<BoxCollider>();
                        box.transform.SetParent(peca.transform, false);
                        box.transform.localPosition = cx.Centro;
                        box.transform.localRotation = Quaternion.Euler(0f, cx.GiroY, 0f);
                        box.size = cx.Tamanho;
                    }
                }
                float giroLado = Mathf.Atan2(lado.x, lado.y) * Mathf.Rad2Deg - 90f;
                for (int c = -1; c <= 1; c++)
                    Por(conteiner[(k + c + 4) % 4], ModelosDaEstrada.Conteiner(0), b.ponto + lado * (c * 6.3f), giroLado);
                Por(mSacos, sacos, b.ponto + dir * 8f + lado * 3f, giroLado);
                Por(mSacos, sacos, b.ponto + dir * 8f - lado * 3f, giroLado);
                Por(mBlindado, blindado, b.ponto + dir * 14f + lado * 5f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 20f);
                k++;
            }
        }

        void MontarLonge()
        {
            for (int tz = 0; tz < ChaoDoMapaFixo.LadrilhosDeLonge; tz++)
            for (int tx = 0; tx < ChaoDoMapaFixo.LadrilhosDeLonge; tx++)
            {
                var go = Objeto($"Longe {tx} {tz}", ParaMesh(Chao.Longe(tx, tz), "Longe"), materialDaCidade);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                _longe.Add(go);
            }
        }

        // ------------------------------------------------------------------ todo quadro

        void Update()
        {
            var olho = Camera.main;
            if (olho == null) return;
            var c = olho.transform.position;

            // a neblina ligada ou não: o relevo de longe e até onde a câmera vê
            if (_semNeblina != AmbienteDoDia.SemNeblina)
            {
                _semNeblina = AmbienteDoDia.SemNeblina;
                foreach (var go in _longe) go.SetActive(_semNeblina);
                _ondeJuntou = Vector3.one * 1e9f;
            }
            if (_semNeblina)
            {
                if (olho.farClipPlane < 15000f)
                {
                    _farOriginal = olho.farClipPlane;
                    olho.farClipPlane = 16000f;
                }
            }
            else if (olho.farClipPlane > 15000f) olho.farClipPlane = _farOriginal;

            if (Time.time >= _proximaRevisao)
            {
                _proximaRevisao = Time.time + 0.2f;
                Revisar(c);
                Cerca(c);
                Segurar();
            }
            Receber();
            PorOCarro();
            DesenharPlantas(olho, c);
        }

        static IEnumerable<Vector2Int> BlocosEm(Vector2 p, float raio)
        {
            float t = ChaoDoMapaFixo.TamanhoDoBloco;
            int i0 = Mathf.FloorToInt((p.x - raio + DesenhoDoMapa.Metade) / t), i1 = Mathf.FloorToInt((p.x + raio + DesenhoDoMapa.Metade) / t);
            int j0 = Mathf.FloorToInt((p.y - raio + DesenhoDoMapa.Metade) / t), j1 = Mathf.FloorToInt((p.y + raio + DesenhoDoMapa.Metade) / t);
            for (int j = Mathf.Max(0, j0); j <= Mathf.Min(ChaoDoMapaFixo.BlocosPorLado - 1, j1); j++)
            for (int i = Mathf.Max(0, i0); i <= Mathf.Min(ChaoDoMapaFixo.BlocosPorLado - 1, i1); i++)
                if (DistanciaAoBloco(p, new Vector2Int(i, j)) <= raio) yield return new Vector2Int(i, j);
        }

        static float DistanciaAoBloco(Vector2 p, Vector2Int b)
        {
            var canto = ChaoDoMapaFixo.CantoDoBloco(b.x, b.y);
            float t = ChaoDoMapaFixo.TamanhoDoBloco;
            float dx = Mathf.Max(0f, Mathf.Max(canto.x - p.x, p.x - canto.x - t)), dz = Mathf.Max(0f, Mathf.Max(canto.y - p.y, p.y - canto.y - t));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        Bloco Novo(Vector2Int chave)
        {
            var b = new Bloco { Chave = chave };
            _blocos[chave] = b;
            return b;
        }

        // o que cada bloco quer (o detalhe, a colisão, as plantas), e os blocos longe saem
        void Revisar(Vector3 c)
        {
            var p = new Vector2(c.x, c.z);
            float raio = _semNeblina ? raioSemNeblina : raioComNeblina;
            float raioPlantas = _semNeblina ? plantasSemNeblina : plantasComNeblina;
            // quem precisa da colisão: o jogador local (a câmera) e os carros perto, no anfitrião
            var pontos = new List<Vector2> { p };
            foreach (var carro in Carro.Todos)
                if (carro != null) pontos.Add(new Vector2(carro.transform.position.x, carro.transform.position.z));

            foreach (var chave in BlocosEm(p, raio))
            {
                if (!_blocos.TryGetValue(chave, out var b)) b = Novo(chave);
                b.VistoEm = Time.time;
                float d = DistanciaAoBloco(p, chave);
                b.PassoQuerido = d < 120f ? 2f : d < 220f ? 4f : d < 360f ? 8f : 16f;
                bool colisao = false;
                foreach (var q in pontos) colisao |= DistanciaAoBloco(q, chave) < raioDeColisao;
                if (colisao) b.PassoQuerido = 2f;
                b.QuerColisao = colisao;
                bool querGrama = d < raioDaGrama;
                if (d < raioPlantas && b.MontandoPlantas == null && (b.Plantas == null || (querGrama && !b.PlantasComGrama)))
                {
                    var chao = Chao;
                    var ch = chave;
                    b.PlantasComGrama = querGrama;
                    b.MontandoPlantas = Task.Run(() =>
                    {
                        var l = new List<PlantaDaFloresta>();
                        chao.Plantas(ch.x, ch.y, l, querGrama);
                        return l;
                    });
                }
                if (d > raioPlantas + 64f && b.Plantas != null) b.Plantas = null;
            }
            // os longe saem depois de 3 s
            var sair = new List<Vector2Int>();
            foreach (var kv in _blocos)
                if (Time.time - kv.Value.VistoEm > 3f && kv.Value.Montando == null) sair.Add(kv.Key);
            foreach (var k in sair)
            {
                var b = _blocos[k];
                if (b.Objeto != null) Destroy(b.Objeto);
                if (b.Malha != null) Destroy(b.Malha);
                _blocos.Remove(k);
            }
            int comColisao = 0;
            foreach (var b in _blocos.Values) if (b.Colisor != null && b.Colisor.enabled) comColisao++;
            BlocosComColisao = comColisao;

            // as montagens novas: primeiro a colisão, depois os de perto (até 6 ao mesmo tempo)
            int montando = 0;
            foreach (var b in _blocos.Values) if (b.Montando != null) montando++;
            var fila = new List<Bloco>();
            foreach (var b in _blocos.Values)
                if (b.Montando == null && !Mathf.Approximately(b.Passo, b.PassoQuerido)) fila.Add(b);
            fila.Sort((a, b2) => (b2.QuerColisao.CompareTo(a.QuerColisao) != 0) ? b2.QuerColisao.CompareTo(a.QuerColisao)
                                 : DistanciaAoBloco(p, a.Chave).CompareTo(DistanciaAoBloco(p, b2.Chave)));
            foreach (var b in fila)
            {
                if (montando >= 6) break;
                var chao = Chao;
                var ch = b.Chave;
                float passo = b.PassoQuerido;
                b.PassoMontando = passo;
                b.Montando = Task.Run(() => chao.Terreno(ch.x, ch.y, passo));
                montando++;
            }
        }

        // as montagens prontas viram malha (até 4 por quadro)
        void Receber()
        {
            int entregues = 0;
            foreach (var b in _blocos.Values)
            {
                if (b.MontandoPlantas != null && b.MontandoPlantas.IsCompleted)
                {
                    if (!b.MontandoPlantas.IsFaulted) b.Plantas = b.MontandoPlantas.Result;
                    else Debug.LogException(b.MontandoPlantas.Exception);
                    b.MontandoPlantas = null;
                    _ondeJuntou = Vector3.one * 1e9f;
                }
                if (entregues >= 4 || b.Montando == null || !b.Montando.IsCompleted) continue;
                if (b.Montando.IsFaulted) Debug.LogException(b.Montando.Exception);
                else Entregar(b, b.Montando.Result, b.PassoMontando);
                b.Montando = null;
                entregues++;
            }
            // a colisão só na malha de 2 m (e um bloco que já era de 2 m ganha o colisor quando alguém chega perto)
            foreach (var b in _blocos.Values)
            {
                bool quer = b.QuerColisao && Mathf.Approximately(b.Passo, 2f);
                if (quer && b.Colisor == null && b.Objeto != null && entregues < 6)
                {
                    b.Colisor = b.Objeto.AddComponent<MeshCollider>();
                    b.Colisor.sharedMesh = b.Malha;
                    entregues++;
                }
                if (b.Colisor != null) b.Colisor.enabled = quer;
            }
        }

        void Entregar(Bloco b, MalhaDoBloco m, float passo)
        {
            if (b.Objeto == null)
            {
                b.Objeto = new GameObject($"Chão {b.Chave.x} {b.Chave.y}");
                b.Objeto.transform.SetParent(_pai, false);
                b.Objeto.AddComponent<MeshFilter>();
                b.Desenho = b.Objeto.AddComponent<MeshRenderer>();
                b.Desenho.sharedMaterial = materialDoChao;
                b.Malha = new Mesh { name = "Chão" };
                b.Malha.MarkDynamic();
                b.Objeto.GetComponent<MeshFilter>().sharedMesh = b.Malha;
            }
            m.Para(b.Malha);
            b.Passo = passo;
            if (Mathf.Approximately(passo, 2f) && b.QuerColisao)
            {
                if (b.Colisor == null) b.Colisor = b.Objeto.AddComponent<MeshCollider>();
                b.Colisor.sharedMesh = null;
                b.Colisor.sharedMesh = b.Malha;
            }
        }

        // a cerca: os pedaços de 300 m perto da câmera
        void Cerca(Vector3 c)
        {
            if (_linhaDaCerca == null || _linhaDaCerca.Count == 0) return;
            var p = new Vector2(c.x, c.z);
            int pedacos = Mathf.CeilToInt(_linhaDaCerca.Count / (float)PorPedacoDaCerca);
            for (int k = 0; k < pedacos; k++)
            {
                int ini = k * PorPedacoDaCerca, fim = Mathf.Min(_linhaDaCerca.Count, ini + PorPedacoDaCerca);
                float d = float.MaxValue;
                for (int i = ini; i < fim; i += 10) d = Mathf.Min(d, Vector2.Distance(p, _linhaDaCerca[i]));
                bool quer = d < raioDaCerca;
                bool tem = _cerca.TryGetValue(k, out var go);
                if (quer && !tem)
                    _cerca[k] = Objeto($"Cerca {k}", Chao.Cerca(_linhaDaCerca, ini, fim).ParaMesh("Cerca"), materialDaCidade);
                else if (!quer && tem && d > raioDaCerca + 150f)
                {
                    Destroy(go.GetComponent<MeshFilter>().sharedMesh);
                    Destroy(go);
                    _cerca.Remove(k);
                }
            }
        }

        // quem passou pelo chão (a colisão ainda não estava pronta, depois de um "ir para") volta para cima: o jogador
        // desta máquina (a pé, sem voar) e, no anfitrião, os carros
        void Segurar()
        {
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return;
            var meu = rede.LocalClient?.PlayerObject;
            if (meu != null)
            {
                var p = meu.transform.position;
                float h = Chao.Altura(p.x, p.z);
                var mov = meu.GetComponent<MovimentoPrimeiraPessoa>();
                var passageiro = meu.GetComponent<PassageiroDoCarro>();
                if (p.y < h - 2.5f && mov != null && !mov.Voando && (passageiro == null || !passageiro.NoCarro))
                    mov.Teleportar(new Vector3(p.x, h + 0.5f, p.z), meu.transform.eulerAngles.y);
            }
            if (!rede.IsServer) return;
            foreach (var carro in Carro.Todos)
            {
                if (carro == null || !carro.IsSpawned) continue;
                var p = carro.transform.position;
                float h = Chao.Altura(p.x, p.z);
                if (p.y > h - 3f) continue;
                var corpo = carro.GetComponent<Rigidbody>();
                var ponto = new Vector3(p.x, h + 1f, p.z);
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
                corpo.position = ponto;
                carro.transform.position = ponto;
            }
        }

        // o carro: o anfitrião põe um ao lado da partida, uma vez
        void PorOCarro()
        {
            if (_carroPosto || carro == null) return;
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return;
            _carroPosto = true;
            if (!rede.IsServer) return;
            var novo = Instantiate(carro, lugarDoCarro + Vector3.up * 0.6f, Quaternion.Euler(0f, giroDoCarro, 0f));
            novo.name = "Carro";
            novo.GetComponent<NetworkObject>().Spawn(true);
        }

        // ------------------------------------------------------------------ as plantas

        static readonly int VentoId = Shader.PropertyToID("_ZccVento");
        static readonly int SomeId = Shader.PropertyToID("_Some");
        static readonly int CimaId = Shader.PropertyToID("_Cima");
        static readonly int DobraId = Shader.PropertyToID("_Dobra");

        // até onde vai cada espécie (as árvores até o raio das plantas; o chão da mata mais perto), as trocas de LOD (ao
        // quadrado), a sombra e o bloco do _Some (o sumiço no último quarto), refeitos quando a neblina liga ou desliga
        float[] _alcance2, _troca1, _troca2;
        bool[] _sombra;
        bool _preparadoSemNeblina;

        void Preparar(int especies)
        {
            float raio = _semNeblina ? plantasSemNeblina : plantasComNeblina;
            _alcance2 = new float[especies];
            _troca1 = new float[especies];
            _troca2 = new float[especies];
            _sombra = new bool[especies];
            _blocosDasPlantas = new MaterialPropertyBlock[especies];
            for (int e = 0; e < especies; e++)
            {
                var esp = (Especie)e;
                float alcance = raio, t1 = 55f, t2 = 110f;
                bool sombra = Floresta.EhArvore(esp);
                if (Floresta.EhGrama(esp)) { alcance = raioDaGrama; t1 = 18f; t2 = 30f; }
                else if (esp == Especie.CapimA || esp == Especie.CapimB) { alcance = 70f; t1 = 25f; t2 = 45f; }
                else if (esp == Especie.Taboa || esp == Especie.SamambaiaA || esp == Especie.SamambaiaB) { alcance = 90f; t1 = 25f; t2 = 45f; }
                else if (!Floresta.EhArvore(esp))
                {
                    alcance = esp == Especie.PedraC || esp == Especie.TroncoCaidoA || esp == Especie.Fardo ? raio : 90f;
                    t1 = 30f; t2 = 60f;
                    sombra = esp == Especie.PedraC || esp == Especie.TroncoCaidoA;
                }
                _alcance2[e] = alcance * alcance;
                _troca1[e] = t1 * t1;
                _troca2[e] = t2 * t2;
                _sombra[e] = sombra;
                var bloco = new MaterialPropertyBlock();
                bloco.SetVector(SomeId, new Vector4(alcance * 0.75f, alcance, 0f, 0f));
                if (Floresta.EhGrama(esp) || esp == Especie.CapimA || esp == Especie.CapimB || esp == Especie.Taboa)
                    bloco.SetFloat(CimaId, 0.8f);
                bloco.SetFloat(DobraId, 0f);
                _blocosDasPlantas[e] = bloco;
            }
            _preparadoSemNeblina = _semNeblina;
        }

        void DesenharPlantas(Camera olho, Vector3 c)
        {
            if (malhasDasPlantas == null || materialDaVegetacao == null) return;
            int especies = Floresta.Quantas;
            if (malhasDasPlantas.Length < especies * 3) return;
            if (_listas == null)
            {
                _listas = new List<Matrix4x4>[especies * 3];
                for (int k = 0; k < _listas.Length; k++) _listas[k] = new List<Matrix4x4>();
            }
            if (_alcance2 == null || _preparadoSemNeblina != _semNeblina) Preparar(especies);
            // o vento (o mesmo da floresta do mundo)
            float t = Time.time;
            float giro = Mathf.Sin(t * 0.031f) * 35f + Mathf.Sin(t * 0.087f + 1.3f) * 12f;
            var dir = (Vector2)(Quaternion.Euler(0f, 0f, giro) * new Vector2(0.92f, 0.38f).normalized);
            float rajada = Mathf.Clamp(Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 0.83f + 1.1f) * 0.4f + Mathf.Sin(t * 2.3f) * 0.15f, -1f, 1f);
            Shader.SetGlobalVector(VentoId, new Vector4(dir.x, dir.y, 0.22f * (1f + 0.35f * rajada), rajada));

            // as listas são refeitas quando a câmera anda 6 m, a cada segundo ou quando chegam plantas novas
            if ((c - _ondeJuntou).sqrMagnitude > 36f || Time.time - _juntouEm > 1f)
            {
                _ondeJuntou = c;
                _juntouEm = Time.time;
                foreach (var l in _listas) l.Clear();
                float raio = _semNeblina ? plantasSemNeblina : plantasComNeblina;
                int n = 0;
                var p = new Vector2(c.x, c.z);
                foreach (var b in _blocos.Values)
                {
                    if (b.Plantas == null || DistanciaAoBloco(p, b.Chave) > raio) continue;
                    foreach (var pl in b.Plantas)
                    {
                        int e = (int)pl.Especie;
                        float dx = pl.Posicao.x - c.x, dz = pl.Posicao.z - c.z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 > _alcance2[e]) continue;
                        int lod = d2 < _troca1[e] ? 0 : d2 < _troca2[e] ? 1 : 2;
                        _listas[e * 3 + lod].Add(Matrix4x4.TRS(pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f), Vector3.one * pl.Escala));
                        n++;
                    }
                }
                PlantasNaVista = n;
            }
            var rp = new RenderParams(materialDaVegetacao)
            {
                layer = 0,
                receiveShadows = true,
                worldBounds = new Bounds(c, Vector3.one * 2000f),
                lightProbeUsage = LightProbeUsage.Off,
            };
            for (int k = 0; k < _listas.Length; k++)
            {
                var l = _listas[k];
                if (l.Count == 0 || malhasDasPlantas[k] == null) continue;
                int e = k / 3;
                rp.matProps = _blocosDasPlantas[e];
                rp.shadowCastingMode = k % 3 < 2 && _sombra[e] ? ShadowCastingMode.On : ShadowCastingMode.Off;
                for (int ini = 0; ini < l.Count; ini += 1023)
                    Graphics.RenderMeshInstanced(rp, malhasDasPlantas[k], 0, l, Mathf.Min(1023, l.Count - ini), ini);
            }
        }
    }
}
