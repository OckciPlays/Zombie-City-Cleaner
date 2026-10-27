using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O NavMesh do mundo gerado (Plano_Mapa, Parte M6; o RuntimeNavMesh do Guardiões da Floresta), em pedaços e só no
    /// anfitrião (a IA dos zumbis roda lá): uma área em volta de cada jogador (<see cref="tamanhoDaArea"/>), montada em
    /// segundo plano e refeita quando ele se afasta do meio dela. Antes de montar, a área espera o chão com colisão e as
    /// casas de verdade dela (<see cref="TerrenoDoMundo.Pronto"/>; o anfitrião já tem a colisão em volta de todos).
    /// Entram os colisores das camadas Default e TransparentFX (o chão, as ruas, as construções, as casas e os carros
    /// parados), e não os jogadores, os zumbis, os corpos, a água e o carro que se dirige.
    /// Na altura, a área vai de 15 m abaixo do chão (ou de quem está mais baixo) a 65 m acima (Parte M6, segunda fatia: um
    /// prédio de 15 andares, da rua à cobertura, entra inteiro). Os andares dos prédios carregam e saem com os jogadores
    /// perto: quando a colisão de um muda, a área em volta é refeita (<see cref="Mudou"/>, no máximo uma vez por segundo).
    /// Fica no "Mundo" da cena Mundo (posto pela montagem da cena).
    /// </summary>
    public class NavMeshDoMundo : MonoBehaviour
    {
        [Tooltip("O tamanho da área em volta de cada jogador (m; a altura é a de baixo + a de cima, veja abaixo). Fica dentro da colisão em volta dele (90 m).")]
        public Vector3 tamanhoDaArea = new Vector3(120f, 60f, 120f);
        [Tooltip("Quanto a área desce abaixo do chão (ou de quem está mais baixo) e sobe acima dele (ou de quem está mais alto) (m).")]
        public float abaixo = 15f, acima = 65f;
        [Tooltip("Quanto o jogador anda antes de a área dele ser refeita (m).")]
        public float refazerA = 35f;
        [Tooltip("Quantos workers de jobs a montagem pode usar. Com todos, a física e o desenho esperavam por ela na cidade (as travadas de 30 a 70 ms).")]
        [Min(1)] public int workers = 2;

        const int Camadas = (1 << 0) | (1 << 1);

        sealed class Area
        {
            public NavMeshData Dados;
            public NavMeshDataInstance Instancia;
            public Vector3 Centro, Pedido;
            public Bounds Limites;
            public AsyncOperation Montando;
            public bool Montada, Suja;
            public float MontadaEm;
        }

        /// <summary>
        /// A colisão mudou dentro da caixa (um andar de prédio carregou ou saiu): as áreas que encostam nela são refeitas.
        /// </summary>
        public static void Mudou(Bounds caixa)
        {
            if (Instancia == null) return;
            foreach (var a in Instancia._areas.Values)
                if (a.Montada && a.Limites.Intersects(caixa)) a.Suja = true;
        }

        public static NavMeshDoMundo Instancia { get; private set; }

        readonly Dictionary<JogadorRede, Area> _areas = new Dictionary<JogadorRede, Area>();
        readonly List<NavMeshBuildSource> _fontes = new List<NavMeshBuildSource>();
        readonly List<NavMeshBuildMarkup> _marcas = new List<NavMeshBuildMarkup>();
        readonly List<JogadorRede> _sairam = new List<JogadorRede>();
        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();

        /// <summary>As montagens feitas e o maior tempo de uma (a coleta das fontes, na thread principal; ms).</summary>
        public int Montagens { get; private set; }
        public float MaiorColetaMs { get; private set; }
        public int Areas => _areas.Count;

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            Limpar();
        }

        void Limpar()
        {
            foreach (var a in _areas.Values) Tirar(a);
            _areas.Clear();
        }

        static void Tirar(Area a)
        {
            if (a.Dados != null && a.Montando != null && !a.Montando.isDone) NavMeshBuilder.Cancel(a.Dados);
            a.Instancia.Remove();
        }

        /// <summary>Já tem NavMesh perto do ponto (até 2 m).</summary>
        public static bool TemEm(Vector3 ponto) => NavMesh.SamplePosition(ponto, out _, 2f, NavMesh.AllAreas);

        /// <summary>As áreas em volta de todos os jogadores estão montadas (e nenhuma esperando para ser refeita).</summary>
        public bool Pronto
        {
            get
            {
                foreach (var a in _areas.Values) if (!a.Montada || a.Suja || (a.Montando != null && !a.Montando.isDone)) return false;
                return _areas.Count > 0;
            }
        }

        /// <summary>O estado de cada área, para a conferência dos prédios (quando a espera pelo NavMesh passa do tempo).</summary>
        public string Estado
        {
            get
            {
                var s = new System.Text.StringBuilder();
                var terreno = TerrenoDoMundo.Instancia;
                float raio = Mathf.Max(tamanhoDaArea.x, tamanhoDaArea.z) * 0.7f;
                foreach (var a in _areas.Values)
                    s.Append($"a área em ({a.Centro.x:F0}, {a.Centro.z:F0}), pedida em ({a.Pedido.x:F0}, {a.Pedido.z:F0}), ")
                     .Append(a.Montada ? $"montada há {Time.unscaledTime - a.MontadaEm:F1} s" : "por montar")
                     .Append(a.Suja ? ", suja" : "").Append(a.Montando != null && !a.Montando.isDone ? ", montando" : "")
                     .Append($", o terreno em volta do pedido: {(terreno == null ? "nenhum" : terreno.QueFalta(a.Pedido, raio) ?? "pronto")}; ");
                return s.Append($"{Montagens} montagens").ToString();
            }
        }

        void Update()
        {
            var rede = NetworkManager.Singleton;
            var terreno = TerrenoDoMundo.Instancia;
            if (rede == null || !rede.IsServer || terreno == null || !terreno.Ligado)
            {
                if (_areas.Count > 0) Limpar();
                return;
            }
            // uma área por jogador (sai com ele)
            _sairam.Clear();
            foreach (var j in _areas.Keys) if (j == null || !JogadorRede.Todos.Contains(j)) _sairam.Add(j);
            foreach (var j in _sairam)
            {
                Tirar(_areas[j]);
                _areas.Remove(j);
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                var p = j.transform.position;
                if (!_areas.TryGetValue(j, out var a)) _areas[j] = a = new Area { Centro = p, Pedido = p };
                // enquanto a área espera para ser refeita, o pedido segue o jogador: a área suja era refeita no meio de antes,
                // e com ele a uns metros dali o chão da beira do círculo ficava sem colisão (fora do raio dela em volta
                // dele), e a área não se refazia mais (os andares dos prédios ficavam fora do NavMesh)
                if (!a.Montada || a.Suja || Horizontal(p - a.Centro) >= refazerA) a.Pedido = p;
            }
            // monta (uma por quadro) a que precisa, com o chão e as casas prontos em volta
            foreach (var a in _areas.Values)
            {
                if (a.Montando != null && !a.Montando.isDone) continue;
                bool precisa = !a.Montada || Horizontal(a.Pedido - a.Centro) >= refazerA || (a.Suja && Time.unscaledTime - a.MontadaEm >= 1f);
                if (!precisa) continue;
                float raio = Mathf.Max(tamanhoDaArea.x, tamanhoDaArea.z) * 0.7f;
                if (!terreno.Pronto(a.Pedido, raio)) continue;
                Montar(a, a.Pedido);
                break;
            }
        }

        void Montar(Area a, Vector3 centro)
        {
            _relogio.Restart();
            // na altura: do chão (ou de quem está mais baixo) a 'abaixo' para baixo, e dele (ou de quem está mais alto) a
            // 'acima' para cima
            float chao = MundoDaPartida.Instancia != null && MundoDaPartida.Instancia.Dados != null ? MundoDaPartida.Instancia.Dados.Altura(centro.x, centro.z) : centro.y;
            float y0 = Mathf.Min(centro.y, chao) - abaixo, y1 = Mathf.Max(centro.y, chao) + acima;
            var limites = new Bounds(new Vector3(centro.x, (y0 + y1) * 0.5f, centro.z), new Vector3(tamanhoDaArea.x, y1 - y0, tamanhoDaArea.z));
            a.Limites = limites;
            a.Suja = false;
            a.MontadaEm = Time.unscaledTime;
            _fontes.Clear();
            NavMeshBuilder.CollectSources(limites, Camadas, NavMeshCollectGeometry.PhysicsColliders, 0, _marcas, _fontes);
            var cena = gameObject.scene;
            for (int i = _fontes.Count - 1; i >= 0; i--)
                if (_fontes[i].component == null || _fontes[i].component.gameObject.scene != cena) _fontes.RemoveAt(i);
            var ajustes = NavMesh.GetSettingsByIndex(0);
            // poucos workers: com todos, os jobs da física (PxScene.simulate) e do GPU Resident Drawer esperavam na fila
            // atrás dos da montagem, e o quadro passava de 30 ms na cidade
            ajustes.maxJobWorkers = (uint)workers;
            if (a.Dados == null)
            {
                a.Dados = new NavMeshData(ajustes.agentTypeID);
                a.Instancia = NavMesh.AddNavMeshData(a.Dados);
            }
            a.Centro = centro;
            a.Montando = NavMeshBuilder.UpdateNavMeshDataAsync(a.Dados, ajustes, _fontes, limites);
            a.Montada = true;
            Montagens++;
            MaiorColetaMs = Mathf.Max(MaiorColetaMs, (float)_relogio.Elapsed.TotalMilliseconds);
        }

        static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
