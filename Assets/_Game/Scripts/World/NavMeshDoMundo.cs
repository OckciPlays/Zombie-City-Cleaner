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
    /// Fica no "Mundo" da cena Mundo (posto pela montagem da cena) e, no Lakewood, nas "Obras das cidades" (Plano_Lakewood,
    /// Etapa 6, a parte 5): o chão de cada um é o <see cref="ChaoDosZumbis.Atual"/>.
    /// <para>
    /// A água do lago fica fora (07/11/2026, a pedido: "sem zumbis na água, eles param na beira"): caixas que tiram o chão
    /// do fundo do lago, onde a água passa de <see cref="AguaQueOsZumbisEntram"/> (<see cref="TirarAAgua"/>). Atrás de quem
    /// nada, eles vão até a beira e esperam ali; e ninguém nasce dentro dela.
    /// </para>
    /// <para>
    /// As âncoras (13/11/2026, a pedido: "os NPCs da comunidade estavam sem reagir"): além das áreas dos jogadores, um lugar
    /// pode pedir uma área parada em cima dele (<see cref="Ancorar"/> e <see cref="Soltar"/>), com o chão com colisão em
    /// volta (os chãos leem <see cref="Ancoras"/>). O acampamento pede a dele quando alguém chega perto: as pessoas dele
    /// aparecem a uns 80 m do meio, e a área em volta de quem chegava (60 m para cada lado) não cobria a maior parte do
    /// acampamento; quem nascia fora do NavMesh ficava com o agente desligado, parado e sem pensar.
    /// </para>
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

        /// <summary>A água até onde os zumbis entram (m): a beira molhada; mais funda, o lago é deles não.</summary>
        public const float AguaQueOsZumbisEntram = 0.3f;
        const int AreaSemPassagem = 1;   // a área "Not Walkable" do NavMesh

        sealed class Area
        {
            public NavMeshData Dados;
            public NavMeshDataInstance Instancia;
            public Vector3 Centro, Pedido;
            /// <summary>O tamanho da área (a de cada jogador, ou a da âncora).</summary>
            public Vector3 Tamanho;
            public Bounds Limites;
            public AsyncOperation Montando;
            public bool Montada, Suja, Ancora;
            public float MontadaEm;
        }

        // as âncoras: o dono (o estado de um acampamento), o meio e o lado da área
        static readonly Dictionary<object, (Vector3 centro, float lado)> _ancoras = new Dictionary<object, (Vector3, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ZerarAsAncoras() => _ancoras.Clear();

        /// <summary>No anfitrião: uma área de NavMesh parada em cima do ponto (um quadrado de 'lado' m), até <see cref="Soltar"/>.</summary>
        public static void Ancorar(object dono, Vector3 centro, float lado)
        {
            if (dono != null) _ancoras[dono] = (centro, Mathf.Max(10f, lado));
        }

        /// <summary>A área da âncora sai.</summary>
        public static void Soltar(object dono)
        {
            if (dono != null) _ancoras.Remove(dono);
        }

        /// <summary>O meio de cada âncora e o raio em volta dele que precisa do chão com colisão (os chãos pedem a colisão ali).</summary>
        public static void Ancoras(List<(Vector3 centro, float raio)> saida)
        {
            saida.Clear();
            foreach (var (centro, lado) in _ancoras.Values) saida.Add((centro, lado * 0.72f + 4f));
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

        // as áreas: a de cada jogador (o dono é o JogadorRede) e a de cada âncora (o dono dela)
        readonly Dictionary<object, Area> _areas = new Dictionary<object, Area>();
        readonly List<NavMeshBuildSource> _fontes = new List<NavMeshBuildSource>();
        readonly List<NavMeshBuildMarkup> _marcas = new List<NavMeshBuildMarkup>();
        readonly List<object> _sairam = new List<object>();
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
            var dados = a.Dados;
            bool montando = dados != null && a.Montando != null && !a.Montando.isDone;
            if (montando) NavMeshBuilder.Cancel(dados);
            a.Instancia.Remove();
            a.Dados = null;
            if (dados == null) return;
            // os dados também saem (antes ficavam na memória: um a cada partida e a cada jogador que saía); no meio de uma
            // montagem, quando ela parar
            if (montando) a.Montando.completed += _ => { if (dados != null) Destroy(dados); };
            else Destroy(dados);
        }

        /// <summary>Já tem NavMesh perto do ponto (até 2 m).</summary>
        public static bool TemEm(Vector3 ponto) => NavMesh.SamplePosition(ponto, out _, 2f, NavMesh.AllAreas);

        /// <summary>As áreas em volta de todos os jogadores estão montadas (e nenhuma esperando para ser refeita; as âncoras não contam).</summary>
        public bool Pronto
        {
            get
            {
                int jogadores = 0;
                foreach (var a in _areas.Values)
                {
                    if (a.Ancora) continue;
                    jogadores++;
                    if (!a.Montada || a.Suja || (a.Montando != null && !a.Montando.isDone)) return false;
                }
                return jogadores > 0;
            }
        }

        /// <summary>O estado de cada área, para a conferência dos prédios (quando a espera pelo NavMesh passa do tempo).</summary>
        public string Estado
        {
            get
            {
                var s = new System.Text.StringBuilder();
                var chao = ChaoDosZumbis.Atual;
                float raio = Mathf.Max(tamanhoDaArea.x, tamanhoDaArea.z) * 0.7f;
                foreach (var a in _areas.Values)
                    s.Append($"a área em ({a.Centro.x:F0}, {a.Centro.z:F0}), pedida em ({a.Pedido.x:F0}, {a.Pedido.z:F0}), ")
                     .Append(a.Montada ? $"montada há {Time.unscaledTime - a.MontadaEm:F1} s" : "por montar")
                     .Append(a.Suja ? ", suja" : "").Append(a.Montando != null && !a.Montando.isDone ? ", montando" : "")
                     .Append($", o terreno em volta do pedido: {(chao == null ? "nenhum" : chao.QueFalta(a.Pedido, raio) ?? "pronto")}; ");
                return s.Append($"{Montagens} montagens").ToString();
            }
        }

        void Update()
        {
            var rede = NetworkManager.Singleton;
            var chao = ChaoDosZumbis.Atual;
            if (rede == null || !rede.IsServer || chao == null || !chao.Ligado)
            {
                if (_areas.Count > 0) Limpar();
                return;
            }
            // uma área por jogador (sai com ele) e uma por âncora (sai quando ela é solta)
            _sairam.Clear();
            foreach (var kv in _areas)
            {
                bool fica = kv.Value.Ancora ? _ancoras.ContainsKey(kv.Key)
                    : kv.Key is JogadorRede jr && jr != null && JogadorRede.Todos.Contains(jr);
                if (!fica) _sairam.Add(kv.Key);
            }
            foreach (var dono in _sairam)
            {
                Tirar(_areas[dono]);
                _areas.Remove(dono);
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                var p = j.transform.position;
                if (!_areas.TryGetValue(j, out var a)) _areas[j] = a = new Area { Centro = p, Pedido = p };
                a.Tamanho = tamanhoDaArea;
                // enquanto a área espera para ser refeita, o pedido segue o jogador: a área suja era refeita no meio de antes,
                // e com ele a uns metros dali o chão da beira do círculo ficava sem colisão (fora do raio dela em volta
                // dele), e a área não se refazia mais (os andares dos prédios ficavam fora do NavMesh)
                if (!a.Montada || a.Suja || Horizontal(p - a.Centro) >= refazerA) a.Pedido = p;
            }
            foreach (var kv in _ancoras)
            {
                var (centro, lado) = kv.Value;
                if (!_areas.TryGetValue(kv.Key, out var a)) _areas[kv.Key] = a = new Area { Centro = centro, Ancora = true };
                a.Pedido = centro;
                a.Tamanho = new Vector3(lado, tamanhoDaArea.y, lado);
            }
            // monta (uma por quadro) a que precisa, com o chão e as casas prontos em volta
            foreach (var a in _areas.Values)
            {
                if (a.Montando != null && !a.Montando.isDone) continue;
                float mover = a.Ancora ? 1f : refazerA;
                bool precisa = !a.Montada || Horizontal(a.Pedido - a.Centro) >= mover || (a.Suja && Time.unscaledTime - a.MontadaEm >= 1f);
                if (!precisa) continue;
                float raio = Mathf.Max(a.Tamanho.x, a.Tamanho.z) * 0.7f;
                if (!chao.Pronto(a.Pedido, raio)) continue;
                Montar(a, a.Pedido, chao);
                break;
            }
        }

        void Montar(Area a, Vector3 centro, IChaoDosZumbis chaoDoMundo)
        {
            _relogio.Restart();
            // na altura: do chão (ou de quem está mais baixo) a 'abaixo' para baixo, e dele (ou de quem está mais alto) a
            // 'acima' para cima
            float chao = chaoDoMundo.Altura(centro.x, centro.z);
            float y0 = Mathf.Min(centro.y, chao) - abaixo, y1 = Mathf.Max(centro.y, chao) + acima;
            var limites = new Bounds(new Vector3(centro.x, (y0 + y1) * 0.5f, centro.z), new Vector3(a.Tamanho.x, y1 - y0, a.Tamanho.z));
            a.Limites = limites;
            a.Suja = false;
            a.MontadaEm = Time.unscaledTime;
            _fontes.Clear();
            NavMeshBuilder.CollectSources(limites, Camadas, NavMeshCollectGeometry.PhysicsColliders, 0, _marcas, _fontes);
            var cena = gameObject.scene;
            for (int i = _fontes.Count - 1; i >= 0; i--)
                if (_fontes[i].component == null || _fontes[i].component.gameObject.scene != cena) _fontes.RemoveAt(i);
            TirarAAgua(limites, chaoDoMundo);
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

        /// <summary>
        /// A água dentro da área sai do NavMesh: nas linhas da grade (2 m), cada trecho seguido de água mais funda que
        /// <see cref="AguaQueOsZumbisEntram"/> vira uma caixa que marca o chão como sem passagem (do fundo até 3 m acima da
        /// água). As ilhas ficam (elas não têm água), e as pontes e a crista da barragem, mais altas, também.
        /// </summary>
        void TirarAAgua(Bounds limites, IChaoDosZumbis chao)
        {
            if (!chao.PodeTerAgua(limites)) return;
            const float passo = ZombieCityCleaner.Mundo.DadosDoMundo.Celula;
            float x0 = limites.min.x, x1 = limites.max.x, z0 = limites.min.z, z1 = limites.max.z;
            for (float z = z0 + passo * 0.5f; z < z1; z += passo)
            {
                float comeco = float.NaN, fundo = float.MaxValue, topo = float.MinValue;
                for (float x = x0 + passo * 0.5f; ; x += passo)
                {
                    float nivel = 0f, fundura = x < x1 ? chao.FunduraDaAgua(x, z, out nivel) : 0f;
                    bool agua = fundura > AguaQueOsZumbisEntram;
                    if (agua)
                    {
                        if (float.IsNaN(comeco)) comeco = x - passo * 0.5f;
                        fundo = Mathf.Min(fundo, nivel - fundura - 2f);
                        topo = Mathf.Max(topo, nivel + 3f);
                    }
                    else if (!float.IsNaN(comeco))
                    {
                        float fim = x - passo * 0.5f;
                        _fontes.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.ModifierBox,
                            transform = Matrix4x4.Translate(new Vector3((comeco + fim) * 0.5f, (fundo + topo) * 0.5f, z)),
                            size = new Vector3(fim - comeco, topo - fundo, passo),
                            area = AreaSemPassagem,
                        });
                        comeco = float.NaN;
                        fundo = float.MaxValue;
                        topo = float.MinValue;
                    }
                    if (x >= x1) break;
                }
            }
        }
    }
}
