using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A cena do Condado de Lakewood (Plano_Lakewood, Etapas 1 e 2, 08/11/2026): o <see cref="MapaDeLakewood"/> no chão,
    /// montado aqui na hora, separado da cena Mundo (o mundo sorteado continua lá).
    ///  - os 16 setores (4 × 4, de 3 × 2,5 km) como objetos, e dentro deles o terreno em blocos de 64 m em volta da câmera
    ///    (2 m entre os pontos até 120 m, 4 m até 220 m, 8 m até 360 m e 16 m além), montados em outra thread, com a colisão
    ///    perto do jogador e do carro;
    ///  - sem a neblina (<see cref="AmbienteDoDia.SemNeblina"/>), o relevo de longe de cada setor (em cores sólidas) e a
    ///    câmera até 16 km;
    ///  - a água do reservatório, dos lagos e dos rios, a hidrelétrica (<see cref="UsinaDeLakewood"/>, a Etapa 4: a
    ///    barragem, a casa de força, o pátio, a subestação e as torres, com a colisão), as estradas com as pontes (as de
    ///    acesso da usina; a rede é a Etapa 5) e a parede invisível na borda do mapa;
    ///  - a floresta nova (09/11/2026, o Plano_Floresta.md, "A floresta nova no Lakewood"): os pinheiros leves de cada
    ///    bloco (<see cref="ChaoDeLakewood.Plantas"/>), como objetos com os LODs perto da câmera (o GPU Resident Drawer: o
    ///    cross-fade, a sombra e o culling, como na cena de floresta) e, mais longe, só os cartazes deles, em instâncias; a
    ///    grama densa (<see cref="GramaDensa"/>) e a tabela das sombras da floresta (<see cref="Graficos.TabelaDaFloresta"/>);
    ///  - o carro, posto pelo anfitrião ao lado da partida.
    /// </summary>
    public partial class LakewoodNaCena : MonoBehaviour
    {
        public MapaDeLakewood mapa;
        public Material materialDoChao, materialDaAgua, materialDaCidade;
        [Tooltip("O relevo de longe, com a cor nos vértices (ZombieCityCleaner/RelevoDeLonge).")] public Material materialDeLonge;
        [Header("A floresta (o Plano_Floresta)")]
        [Tooltip("Os pinheiros leves com os LODs (os prefabs do gerador), na ordem das espécies PinheiroAlto1 em diante: o "
                 + "alto, o médio e o pequeno, 5 de cada.")]
        public GameObject[] pinheiros;
        [Tooltip("A malha do tufo e o material da grama densa (o GeradorDeGrama).")] public Mesh malhaDaGrama;
        public Material materialDaGrama;
        [Tooltip("Quantos pinheiros viram objeto por quadro, mais ou menos (um bloco nunca fica pela metade).")]
        public int pinheirosPorQuadro = 600;
        [Header("A hidrelétrica (Etapa 4)")]
        [Tooltip("O material das peças da usina: o abandono pela altura do objeto (AbandonoDosObjetos), com a origem no chão de cada uma.")]
        public Material materialDaUsina;
        [Tooltip("Os carros abandonados do Cidade_Rua.fbx, na ordem de ModelosDaEstrada.NomesDosCarros.")] public Mesh[] carrosParados;
        [Tooltip("A barreira de concreto do Cidade_Rua.fbx (o posto de controle da crista).")] public Mesh barreira;
        [Tooltip("O carro que se dirige, posto pelo anfitrião ao lado da partida.")] public GameObject carro;
        public Vector3 lugarDoCarro;
        public float giroDoCarro;
        public Vector3 partida;

        [Header("Até onde (m)")]
        /// <summary>Até onde a câmera vê sem a neblina (o condado inteiro); o Graficos também respeita.</summary>
        public const float FarSemNeblina = 16000f;

        public float raioComNeblina = 200f;
        public float raioSemNeblina = 480f;
        public float plantasComNeblina = 165f, plantasSemNeblina = 480f;
        public float raioDaGrama = 45f;
        [Tooltip("Até onde vão os tufos ralos depois da grama (m), na Alta e na Ultra: a grama parece cobrir o campo.")]
        public float raioDaGramaLonge = 90f;
        public float raioDeColisao = 110f;

        public static LakewoodNaCena Instancia { get; private set; }
        public ChaoDeLakewood Chao { get; private set; }
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
            public Task<(List<PlantaDaFloresta>, Matrix4x4[])> MontandoPlantas;
            public List<PlantaDaFloresta> Plantas;
            // a matriz de cada planta (feita uma vez, na thread: o TRS a cada quadro pesava com a mata cheia)
            public Matrix4x4[] Matrizes;
            // os pinheiros do bloco como objetos (perto da câmera); sem eles, o bloco desenha só os cartazes
            public List<GameObject> Arvores;
            public float VistoEm;
        }

        readonly Dictionary<Vector2Int, Bloco> _blocos = new Dictionary<Vector2Int, Bloco>();
        readonly List<GameObject> _longe = new List<GameObject>();
        Transform[,] _setores;
        bool _semNeblina, _carroPosto;
        float _farOriginal = 150f, _proximaRevisao;
        Transform _pai;

        // os cartazes de longe: uma lista por pinheiro (a malha e o material do LOD 2 de cada prefab)
        List<Matrix4x4>[] _listas;
        Mesh[] _malhasDosCartazes;
        Material[] _materiaisDosCartazes;
        // os pinheiros que saíram de perto, guardados para voltar (um monte por prefab), e o pai de todos
        Stack<GameObject>[] _guardados;
        Transform _paiDosPinheiros;
        float _raioDosObjetos = -1f;
        GramaDensa _grama;
        Vector3 _ondeJuntou = Vector3.one * 1e9f;
        float _juntouEm;

        const int PorPedacoDaEstrada = 60;

        void Awake()
        {
            Instancia = this;
            _pai = new GameObject("Lakewood (montado na hora)").transform;
            _pai.SetParent(transform, false);
            var t0 = Time.realtimeSinceStartup;
            var relevo = new RelevoDeLakewood(mapa);
            Chao = new ChaoDeLakewood(mapa, relevo);
            // as alturas do relevo de longe (meio segundo) em outra thread, enquanto o resto monta; o MontarLonge espera
            var chao = Chao;
            var gradeDeLonge = Task.Run(() => chao.AlturaDeLonge(0f, 0f));
            MontarSetores();
            MontarAgua();
            if (mapa.LagoPrincipal() >= 0) ReflexoDaAgua.Instalar(relevo.NivelDosLagos[mapa.LagoPrincipal()]);
            MontarUsina();
            MontarEstradas();
            MontarAsCidades();
            MontarACasaDaPartida();
            MontarBorda();
            float esperouOLonge = Time.realtimeSinceStartup;
            gradeDeLonge.Wait();
            esperouOLonge = Time.realtimeSinceStartup - esperouOLonge;
            MontarLonge();
            Graficos.TabelaDaFloresta = true;
            PrepararAFloresta();
            // o chão em volta da partida, com a colisão, antes de alguém nascer
            foreach (var chave in BlocosEm(new Vector2(partida.x, partida.z), 140f))
            {
                var b = Novo(chave);
                b.QuerColisao = true;
                b.PassoQuerido = 2f;
                Entregar(b, Chao.Terreno(chave.x, chave.y, 2f), 2f);
            }
            // os pinheiros em volta da partida já como objetos (sem eles, os primeiros quadros mostravam os cartazes perto)
            int pinheirosNaPartida = 0;
            foreach (var chave in BlocosEm(new Vector2(partida.x, partida.z), 130f))
            {
                if (!_blocos.TryGetValue(chave, out var b)) b = Novo(chave);
                (b.Plantas, b.Matrizes) = PinheirosDoBloco(Chao, chave);
                pinheirosNaPartida += PorOsPinheiros(b);
            }
            Debug.Log($"[Lakewood] Montado em {Time.realtimeSinceStartup - t0:F1} s: {mapa.setoresX * mapa.setoresZ} setores, {mapa.lagos.Count} lagos, {mapa.ilhas.Count} ilhas, {mapa.rios.Count} rios, {Chao.Estradas.Count} estradas, {pinheirosNaPartida} pinheiros em volta da partida; o relevo de longe: {Chao.ResumoDoLonge.abaixados} vértices abaixados (até {Chao.ResumoDoLonge.maisFundo:F0} m), {Chao.ResumoDoLonge.segundos:F2} s em outra thread, {esperouOLonge:F2} s de espera.");
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            AmbienteDoDia.SemNeblina = false;
            Graficos.TabelaDaFloresta = false;
        }

        /// <summary>O nome do setor onde o ponto está ("A1" no canto sudoeste).</summary>
        public string SetorDe(Vector3 p) => mapa.NomeDoSetorEm(p.x, p.z);

        // ------------------------------------------------------------------ o que é montado uma vez

        void MontarSetores()
        {
            _setores = new Transform[mapa.setoresX, mapa.setoresZ];
            for (int j = 0; j < mapa.setoresZ; j++)
            for (int i = 0; i < mapa.setoresX; i++)
            {
                var go = new GameObject($"Setor {MapaDeLakewood.NomeDoSetor(i, j)}");
                go.transform.SetParent(_pai, false);
                _setores[i, j] = go.transform;
            }
        }

        Transform PaiDoBloco(Vector2Int chave)
        {
            var canto = ChaoDeLakewood.CantoDoBloco(chave.x, chave.y);
            mapa.SetorDe(canto.x + ChaoDeLakewood.TamanhoDoBloco * 0.5f, canto.y + ChaoDeLakewood.TamanhoDoBloco * 0.5f, out int i, out int j);
            return _setores[i, j];
        }

        GameObject Objeto(string nome, Mesh malha, Material material, Transform pai = null, int camada = 0, bool colisao = false)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(pai != null ? pai : _pai, false);
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
            for (int l = 0; l < mapa.lagos.Count; l++)
            {
                var m = Chao.AguaDoLago(l);
                if (m != null) Objeto($"Água: {mapa.lagos[l].nome}", ParaMesh(m, "Agua"), materialDaAgua, null, agua);
            }
            for (int r = 0; r < mapa.rios.Count; r++)
            {
                var m = Chao.AguaDoRio(r);
                if (m != null) Objeto($"Rio: {mapa.rios[r].nome}", ParaMesh(m, "Rio"), materialDaAgua, null, agua);
            }
        }

        /// <summary>As peças da hidrelétrica (Etapa 4) e quantas são (para o F1).</summary>
        public int PecasDaUsina { get; private set; }

        // a hidrelétrica: as obras (uma malha por peça, a das torres reaproveitada), a água parada do canal de fuga, os
        // carros abandonados e as barreiras de concreto, com as caixas de colisão do tamanho deles
        void MontarUsina()
        {
            var usina = Chao.Usina;
            if (usina == null || !usina.Tem) return;
            var pai = new GameObject("Hidrelétrica").transform;
            pai.SetParent(_pai, false);
            var material = materialDaUsina != null ? materialDaUsina : materialDaCidade;
            int agua = LayerMask.NameToLayer("Water");
            if (agua < 0) agua = 0;
            var desenhos = new Dictionary<MalhaDeObra, Mesh>();
            var colisoes = new Dictionary<MalhaDeObra, Mesh>();
            GameObject Parado(string nome, Mesh malha, Vector3 onde, Quaternion giro)
            {
                var go = Objeto(nome, malha, material, pai);
                go.transform.SetPositionAndRotation(onde, giro);
                var caixa = go.AddComponent<BoxCollider>();
                caixa.center = malha.bounds.center;
                caixa.size = malha.bounds.size;
                return go;
            }
            foreach (var p in usina.Montar((x, z) => Chao.Altura(x, z)))
            {
                switch (p.Tipo)
                {
                    case TipoDaPecaDaUsina.Agua:
                        Objeto(p.Nome, ParaMesh(p.Agua, "Agua"), materialDaAgua, pai, agua);
                        break;
                    case TipoDaPecaDaUsina.Carro:
                        if (carrosParados != null && p.Carro >= 0 && p.Carro < carrosParados.Length && carrosParados[p.Carro] != null)
                            Parado(p.Nome, carrosParados[p.Carro], p.Posicao, p.Giro);
                        break;
                    case TipoDaPecaDaUsina.Barreira:
                        if (barreira != null) Parado(p.Nome, barreira, p.Posicao, p.Giro);
                        break;
                    default:
                        if (!desenhos.TryGetValue(p.Desenho, out var malha)) desenhos[p.Desenho] = malha = p.Desenho.ParaMesh(p.Nome);
                        var go = Objeto(p.Nome, malha, material, pai);
                        go.transform.SetPositionAndRotation(p.Posicao, p.Giro);
                        if (p.Colisao != null)
                        {
                            if (!colisoes.TryGetValue(p.Colisao, out var col)) colisoes[p.Colisao] = col = p.Colisao.ParaMesh(p.Nome + " (colisão)");
                            go.AddComponent<MeshCollider>().sharedMesh = col;
                        }
                        break;
                }
                PecasDaUsina++;
            }
        }

        // os pedaços das estradas (com a colisão sempre) e o desenho de cada um: longe dos blocos de perto ele some (o
        // relevo de longe fica abaixo do chão de verdade, e a estrada apareceria boiando por cima dele)
        readonly List<(MeshRenderer r, Bounds b)> _pedacosDasEstradas = new List<(MeshRenderer, Bounds)>();

        void MontarEstradas()
        {
            var pai = new GameObject("Estradas").transform;
            pai.SetParent(_pai, false);
            for (int e = 0; e < Chao.Estradas.Count; e++)
            {
                int n = Chao.Estradas[e].Pontos.Length;
                for (int ini = 0; ini < n - 1; ini += PorPedacoDaEstrada)
                {
                    // o desenho e a colisão à parte (só o asfalto, as muretas, os pilares e o guard-rail: as linhas pintadas e
                    // as saias faziam uns 220 mil triângulos para a física cozinhar)
                    var colisao = new MalhaDeObra();
                    string nome = $"{Chao.Estradas[e].Via.nome} {ini}";
                    var malha = Chao.Pedaco(e, ini, Mathf.Min(n - 1, ini + PorPedacoDaEstrada), colisao).ParaMesh(nome);
                    var go = Objeto($"Estrada: {Chao.Estradas[e].Via.nome} ({ini})", malha, materialDaCidade, pai);
                    go.AddComponent<MeshCollider>().sharedMesh = colisao.ParaMesh(nome + " (colisão)");
                    _pedacosDasEstradas.Add((go.GetComponent<MeshRenderer>(), malha.bounds));
                }
            }
            // os pátios dos cruzamentos, numa malha só
            var patios = Chao.Patios();
            if (patios.Triangulos > 0)
            {
                var malha = patios.ParaMesh("Cruzamentos");
                var go = Objeto("Estradas: os cruzamentos", malha, materialDaCidade, pai, 0, true);
                _pedacosDasEstradas.Add((go.GetComponent<MeshRenderer>(), new Bounds(Vector3.zero, Vector3.one * 1e6f)));
            }
        }

        void MostrarAsEstradas(Vector2 p, float raio)
        {
            foreach (var (r, b) in _pedacosDasEstradas)
            {
                if (r == null) continue;
                float dx = Mathf.Max(0f, Mathf.Max(b.min.x - p.x, p.x - b.max.x)), dz = Mathf.Max(0f, Mathf.Max(b.min.z - p.y, p.y - b.max.z));
                bool ver = dx * dx + dz * dz < (raio + 60f) * (raio + 60f);
                if (r.enabled != ver) r.enabled = ver;
            }
        }

        // a parede invisível na borda do mapa (a borda de verdade, com o que fecha o condado, fica para depois)
        void MontarBorda()
        {
            var borda = new GameObject("Borda do mapa");
            borda.transform.SetParent(_pai, false);
            float w = mapa.largura, c = mapa.comprimento;
            void Parede(Vector3 centro, Vector3 tamanho)
            {
                var go = new GameObject("Parede");
                go.transform.SetParent(borda.transform, false);
                go.transform.position = centro;
                go.AddComponent<BoxCollider>().size = tamanho;
            }
            Parede(new Vector3(-1f, 400f, c * 0.5f), new Vector3(2f, 1200f, c + 4f));
            Parede(new Vector3(w + 1f, 400f, c * 0.5f), new Vector3(2f, 1200f, c + 4f));
            Parede(new Vector3(w * 0.5f, 400f, -1f), new Vector3(w + 4f, 1200f, 2f));
            Parede(new Vector3(w * 0.5f, 400f, c + 1f), new Vector3(w + 4f, 1200f, 2f));
        }

        // o material do relevo de longe: o ChaoDeLonge com as texturas do chão de perto (sem emenda entre os dois)
        Material _materialDeLonge;

        Material MaterialDeLonge()
        {
            if (_materialDeLonge != null) return _materialDeLonge;
            // o material salvo da cena (MontagemDeLakewood.MaterialDoRelevoDeLonge): é ele que leva o shader para a build
            if (materialDeLonge != null && materialDeLonge.shader != null && materialDeLonge.shader.name == "ZombieCityCleaner/ChaoDeLonge")
                return _materialDeLonge = materialDeLonge;
            var shader = Shader.Find("ZombieCityCleaner/ChaoDeLonge");
            if (shader == null || materialDoChao == null) return _materialDeLonge = materialDeLonge != null ? materialDeLonge : materialDaCidade;
            _materialDeLonge = new Material(shader) { name = "Chão de longe" };
            _materialDeLonge.CopyPropertiesFromMaterial(materialDoChao);
            _materialDeLonge.SetColor("_CopaCor", new Color(0.16f, 0.24f, 0.13f));
            return _materialDeLonge;
        }

        void MontarLonge()
        {
            for (int j = 0; j < mapa.setoresZ; j++)
            for (int i = 0; i < mapa.setoresX; i++)
            {
                var go = Objeto($"Longe {MapaDeLakewood.NomeDoSetor(i, j)}", ParaMesh(Chao.Longe(i, j), "Longe"), MaterialDeLonge(), _setores[i, j]);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                _longe.Add(go);
            }
        }

        // ------------------------------------------------------------------ todo quadro

        void Update()
        {
            var olho = Camera.main;
            if (olho == null) return;
            var c = olho.transform.position;

            // a neblina ligada ou não: até onde a câmera vê (o relevo de longe fica sempre, por baixo dos blocos de perto)
            if (_semNeblina != AmbienteDoDia.SemNeblina)
            {
                _semNeblina = AmbienteDoDia.SemNeblina;
                _ondeJuntou = Vector3.one * 1e9f;
            }
            if (_semNeblina)
            {
                if (olho.farClipPlane < 15000f)
                {
                    _farOriginal = olho.farClipPlane;
                    olho.farClipPlane = FarSemNeblina;
                }
            }
            else if (olho.farClipPlane > 15000f) olho.farClipPlane = _farOriginal;

            if (Time.time >= _proximaRevisao)
            {
                _proximaRevisao = Time.time + 0.2f;
                Revisar(c);
                Segurar();
            }
            Receber();
            ReceberAsObras();
            PorOCarro();
            Pinheiros(olho, c);
            if (_grama != null)
            {
                _grama.alcance = Graficos.Grama(raioDaGrama);
                _grama.alcanceLonge = Graficos.Qualidade >= 2 ? raioDaGramaLonge : 0f;
            }
            DesenharPlantas(olho, c);
        }

        IEnumerable<Vector2Int> BlocosEm(Vector2 p, float raio)
        {
            float t = ChaoDeLakewood.TamanhoDoBloco;
            int i0 = Mathf.FloorToInt((p.x - raio) / t), i1 = Mathf.FloorToInt((p.x + raio) / t);
            int j0 = Mathf.FloorToInt((p.y - raio) / t), j1 = Mathf.FloorToInt((p.y + raio) / t);
            for (int j = Mathf.Max(0, j0); j <= Mathf.Min(Chao.BlocosZ - 1, j1); j++)
            for (int i = Mathf.Max(0, i0); i <= Mathf.Min(Chao.BlocosX - 1, i1); i++)
                if (DistanciaAoBloco(p, new Vector2Int(i, j)) <= raio) yield return new Vector2Int(i, j);
        }

        static float DistanciaAoBloco(Vector2 p, Vector2Int b)
        {
            var canto = ChaoDeLakewood.CantoDoBloco(b.x, b.y);
            float t = ChaoDeLakewood.TamanhoDoBloco;
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
            MostrarAsEstradas(p, raio);
            // quem precisa da colisão: o jogador local (a câmera), os carros perto e, no anfitrião, os outros jogadores (os
            // zumbis em volta deles andam no NavMesh do chão daqui: Plano_Lakewood, Etapa 6, a parte 5)
            var pontos = new List<Vector2> { p };
            foreach (var carro in Carro.Todos)
                if (carro != null) pontos.Add(new Vector2(carro.transform.position.x, carro.transform.position.z));
            var rede = Unity.Netcode.NetworkManager.Singleton;
            if (rede != null && rede.IsServer)
                foreach (var j in ZombieCityCleaner.Player.JogadorRede.Todos)
                {
                    if (j == null || j == ZombieCityCleaner.Player.JogadorRede.Local) continue;
                    var q = new Vector2(j.transform.position.x, j.transform.position.z);
                    pontos.Add(q);
                    // longe da câmera: os blocos dele, só com a colisão
                    foreach (var chave in BlocosEm(q, raioDeColisao))
                    {
                        if (!_blocos.TryGetValue(chave, out var bj)) bj = Novo(chave);
                        bj.VistoEm = Time.time;
                        if (DistanciaAoBloco(p, chave) > raio) { bj.PassoQuerido = 2f; bj.QuerColisao = true; }
                    }
                }

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
                if (d < raioPlantas && b.MontandoPlantas == null && b.Plantas == null)
                {
                    var chao = Chao;
                    var ch = chave;
                    b.MontandoPlantas = Task.Run(() => PinheirosDoBloco(chao, ch));
                }
                if (d > raioPlantas + 64f && b.Plantas != null)
                {
                    GuardarOsPinheiros(b);
                    b.Plantas = null;
                    b.Matrizes = null;
                }
            }
            // os longe saem depois de 3 s
            var sair = new List<Vector2Int>();
            foreach (var kv in _blocos)
                if (Time.time - kv.Value.VistoEm > 3f && kv.Value.Montando == null) sair.Add(kv.Key);
            foreach (var k in sair)
            {
                var b = _blocos[k];
                GuardarOsPinheiros(b);
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
                    if (!b.MontandoPlantas.IsFaulted) (b.Plantas, b.Matrizes) = b.MontandoPlantas.Result;
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
            // a colisão na malha de 2 m, ou na de 4 m enquanto a de 2 m não chega (sem ela, quem andava rápido passava pelo
            // chão); um bloco que já tem a malha ganha o colisor quando alguém chega perto
            foreach (var b in _blocos.Values)
            {
                bool quer = b.QuerColisao && b.Passo > 0f && b.Passo <= 4.01f;
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
                b.Objeto.transform.SetParent(PaiDoBloco(b.Chave), false);
                b.Objeto.AddComponent<MeshFilter>();
                b.Desenho = b.Objeto.AddComponent<MeshRenderer>();
                b.Desenho.sharedMaterial = materialDoChao;
                b.Malha = new Mesh { name = "Chão" };
                b.Malha.MarkDynamic();
                b.Objeto.GetComponent<MeshFilter>().sharedMesh = b.Malha;
            }
            m.Para(b.Malha);
            b.Passo = passo;
            if (passo <= 4.01f && b.QuerColisao)
            {
                if (b.Colisor == null) b.Colisor = b.Objeto.AddComponent<MeshCollider>();
                b.Colisor.sharedMesh = null;
                b.Colisor.sharedMesh = b.Malha;
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
            // com a casa da partida, os quatro veículos arrumados no terreiro (LakewoodNaCena.Partida.cs)
            if (PorOsDaPartida()) return;
            if (_carroPosto || carro == null) return;
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return;
            _carroPosto = true;
            if (!rede.IsServer) return;
            var novo = Instantiate(carro, lugarDoCarro + Vector3.up * 0.6f, Quaternion.Euler(0f, giroDoCarro, 0f));
            novo.name = "Carro";
            novo.GetComponent<NetworkObject>().Spawn(true);
        }

        // ------------------------------------------------------------------ a floresta (os pinheiros leves e a grama densa)

        static readonly int VentoId = Shader.PropertyToID("_ZccVento");

        // as listas dos cartazes, os montes dos pinheiros guardados, as malhas e os materiais dos cartazes (o LOD 2 de cada
        // prefab) e a grama densa (posta na hora, com o chão do Lakewood)
        void PrepararAFloresta()
        {
            int n = pinheiros != null ? pinheiros.Length : 0;
            _listas = new List<Matrix4x4>[n];
            _guardados = new Stack<GameObject>[n];
            _malhasDosCartazes = new Mesh[n];
            _materiaisDosCartazes = new Material[n];
            for (int k = 0; k < n; k++)
            {
                _listas[k] = new List<Matrix4x4>();
                _guardados[k] = new Stack<GameObject>();
                var grupo = pinheiros[k] != null ? pinheiros[k].GetComponent<LODGroup>() : null;
                if (grupo == null) continue;
                var lods = grupo.GetLODs();
                var ultimo = lods.Length > 0 && lods[lods.Length - 1].renderers.Length > 0 ? lods[lods.Length - 1].renderers[0] : null;
                if (ultimo == null) continue;
                var filtro = ultimo.GetComponent<MeshFilter>();
                _malhasDosCartazes[k] = filtro != null ? filtro.sharedMesh : null;
                _materiaisDosCartazes[k] = ultimo.sharedMaterial;
            }
            _paiDosPinheiros = new GameObject("Pinheiros").transform;
            _paiDosPinheiros.SetParent(_pai, false);
            if (malhaDaGrama != null && materialDaGrama != null)
            {
                _grama = gameObject.AddComponent<GramaDensa>();
                _grama.malha = malhaDaGrama;
                _grama.material = materialDaGrama;
                _grama.resumoNaTela = false;
                _grama.alcance = Graficos.Grama(raioDaGrama);
                _grama.Chao = Chao;
            }
        }

        // os pinheiros de um bloco e a matriz de cada um (fora da thread principal: só lê o chão)
        static (List<PlantaDaFloresta>, Matrix4x4[]) PinheirosDoBloco(ChaoDeLakewood chao, Vector2Int ch)
        {
            var l = new List<PlantaDaFloresta>();
            chao.Plantas(ch.x, ch.y, l);
            var m = new Matrix4x4[l.Count];
            for (int k = 0; k < l.Count; k++) m[k] = Matrix4x4.TRS(l[k].Posicao, Quaternion.Euler(0f, l[k].Giro, 0f), Vector3.one * l[k].Escala);
            return (l, m);
        }

        static int IndiceDoPinheiro(Especie e) => (int)e - (int)Especie.PinheiroAlto1;

        /// <summary>Quantos pinheiros estão como objetos (perto) e quantos cartazes de longe foram desenhados.</summary>
        public int PinheirosPerto { get; private set; }
        public int CartazesLonge { get; private set; }
        /// <summary>Até onde os pinheiros são objetos (m); depois, só os cartazes.</summary>
        public float RaioDosObjetos => _raioDosObjetos;

        // os pinheiros de um bloco viram objetos (os guardados voltam; faltando, um novo do prefab); devolve quantos
        int PorOsPinheiros(Bloco b)
        {
            if (b.Arvores != null || b.Plantas == null || _guardados == null) return 0;
            b.Arvores = new List<GameObject>(b.Plantas.Count);
            // (12/11/2026, a parte 3 do "O jogo do mundo sorteado no Lakewood": os cortados viram toco; LakewoodNaCena.Arvores.cs)
            _cortesDoBloco[b.Chave] = CortadasNoBloco(b);
            for (int k = 0; k < b.Plantas.Count; k++)
            {
                var pl = b.Plantas[k];
                int i = IndiceDoPinheiro(pl.Especie);
                if (i < 0 || i >= pinheiros.Length || pinheiros[i] == null) continue;
                if (CortadaNoBloco(b, k))
                {
                    PorOToco(b, pl);
                    continue;
                }
                GameObject go;
                if (_guardados[i].Count > 0)
                {
                    go = _guardados[i].Pop();
                    go.transform.SetPositionAndRotation(pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f));
                    go.transform.localScale = Vector3.one * pl.Escala;
                    go.SetActive(true);
                }
                else
                {
                    go = Instantiate(pinheiros[i], pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f), _paiDosPinheiros);
                    go.name = pinheiros[i].name;
                    go.transform.localScale = Vector3.one * pl.Escala;
                }
                b.Arvores.Add(go);
            }
            _ondeJuntou = Vector3.one * 1e9f;
            return b.Arvores.Count;
        }

        // os pinheiros de um bloco saem (desligados e guardados no monte do prefab, para outro bloco)
        void GuardarOsPinheiros(Bloco b)
        {
            if (b.Arvores == null) return;
            foreach (var go in b.Arvores)
            {
                if (go == null) continue;
                go.SetActive(false);
                _guardados[IndiceDoGuardado(go)].Push(go);
            }
            b.Arvores = null;
            TirarOsTocos(b);
            _cortesDoBloco.Remove(b.Chave);
            _ondeJuntou = Vector3.one * 1e9f;
        }

        // o monte de um pinheiro pelo nome dele (o nome do prefab)
        readonly Dictionary<string, int> _indicePeloNome = new Dictionary<string, int>();
        int IndiceDoGuardado(GameObject go)
        {
            if (_indicePeloNome.Count == 0)
                for (int k = 0; k < pinheiros.Length; k++)
                    if (pinheiros[k] != null) _indicePeloNome[pinheiros[k].name] = k;
            return _indicePeloNome.TryGetValue(go.name, out int i) ? i : 0;
        }

        // Até onde os pinheiros são objetos: depois de onde o maior deles (o alto, 22 m na escala 1,2) já virou cartaz
        // nesta câmera e neste lodBias (o LOD 1 vai até a fração 0,12 da tela), com 10% de folga; assim o cartaz do objeto e
        // o das instâncias são a mesma coisa na troca. Na Alta (o lodBias 2) e no FOV de 70°, uns 310 m.
        float RaioDosObjetosPara(Camera olho, float raioPlantas)
        {
            float tg = 2f * Mathf.Tan(olho.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float r = 22.3f * QualitySettings.lodBias / (tg * 0.12f) * 1.1f + 10f;
            return Mathf.Clamp(r, 100f, raioPlantas);
        }

        // todo quadro: os blocos perto ganham os pinheiros como objetos (os mais perto primeiro, até uns
        // pinheirosPorQuadro), e os que ficaram longe os devolvem
        void Pinheiros(Camera olho, Vector3 c)
        {
            if (pinheiros == null || pinheiros.Length == 0 || _guardados == null) return;
            float raioPlantas = _semNeblina ? plantasSemNeblina : plantasComNeblina;
            float r = RaioDosObjetosPara(olho, raioPlantas);
            if (_raioDosObjetos < 0f || Mathf.Abs(r - _raioDosObjetos) > 8f) _raioDosObjetos = r;
            var p = new Vector2(c.x, c.z);
            _fila.Clear();
            int perto = 0;
            foreach (var b in _blocos.Values)
            {
                float d = DistanciaAoBloco(p, b.Chave);
                if (b.Arvores != null)
                {
                    if (d > _raioDosObjetos + 40f) GuardarOsPinheiros(b);
                    else perto += b.Arvores.Count;
                }
                else if (b.Plantas != null && d < _raioDosObjetos) _fila.Add((d, b));
            }
            if (_fila.Count > 0)
            {
                _fila.Sort((a, b2) => a.Item1.CompareTo(b2.Item1));
                int postos = 0;
                foreach (var (_, b) in _fila)
                {
                    if (postos >= pinheirosPorQuadro) break;
                    int n = PorOsPinheiros(b);
                    postos += Mathf.Max(n, 1);
                    perto += n;
                }
            }
            PinheirosPerto = perto;
        }
        readonly List<(float, Bloco)> _fila = new List<(float, Bloco)>();

        // os cartazes dos pinheiros dos blocos sem os objetos (os de longe), em instâncias, e o vento
        void DesenharPlantas(Camera olho, Vector3 c)
        {
            // o vento (o mesmo da floresta do mundo)
            float t = Time.time;
            float giro = Mathf.Sin(t * 0.031f) * 35f + Mathf.Sin(t * 0.087f + 1.3f) * 12f;
            var dir = (Vector2)(Quaternion.Euler(0f, 0f, giro) * new Vector2(0.92f, 0.38f).normalized);
            float rajada = Mathf.Clamp(Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 0.83f + 1.1f) * 0.4f + Mathf.Sin(t * 2.3f) * 0.15f, -1f, 1f);
            Shader.SetGlobalVector(VentoId, new Vector4(dir.x, dir.y, 0.22f * (1f + 0.35f * rajada) * (1f + 2.2f * Tempo.Vento), rajada));
            if (_listas == null || _listas.Length == 0) return;

            // as listas são refeitas quando a câmera anda 6 m, a cada segundo ou quando um bloco muda
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
                    if (b.Arvores != null || b.Plantas == null || b.Matrizes == null || DistanciaAoBloco(p, b.Chave) > raio) continue;
                    var plantas = b.Plantas;
                    for (int k = 0; k < plantas.Count; k++)
                    {
                        int i = IndiceDoPinheiro(plantas[k].Especie);
                        if (i < 0 || i >= _listas.Length || CortadaNoBloco(b, k)) continue;
                        float dx = plantas[k].Posicao.x - c.x, dz = plantas[k].Posicao.z - c.z;
                        if (dx * dx + dz * dz > raio * raio) continue;
                        _listas[i].Add(b.Matrizes[k]);
                        n++;
                    }
                }
                CartazesLonge = n;
            }
            PlantasNaVista = PinheirosPerto + CartazesLonge;
            for (int k = 0; k < _listas.Length; k++)
            {
                var l = _listas[k];
                if (l.Count == 0 || _malhasDosCartazes[k] == null || _materiaisDosCartazes[k] == null) continue;
                // sem sombra (o cartaz do prefab também não faz) e em todas as câmeras (o reflexo da água, na Ultra)
                var rp = new RenderParams(_materiaisDosCartazes[k])
                {
                    layer = 0,
                    receiveShadows = true,
                    shadowCastingMode = ShadowCastingMode.Off,
                    worldBounds = new Bounds(c, Vector3.one * 2000f),
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                };
                for (int ini = 0; ini < l.Count; ini += 1023)
                    Graphics.RenderMeshInstanced(rp, _malhasDosCartazes[k], 0, l, Mathf.Min(1023, l.Count - ini), ini);
            }
        }
    }
}
