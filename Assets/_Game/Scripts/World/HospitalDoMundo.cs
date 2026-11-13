using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Documentos;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Music;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Hospital Principal no mundo sorteado (o Plano_Hospital.md da pasta de arte, a parte 7, 08/11/2026: por enquanto sem
    /// o saque, os zumbis e as portas que abrem), no objeto "Mundo" da cena Mundo. O lugar (um quarteirão inteiro da maior
    /// cidade grande), o chão plano dele e o buraco do subsolo são da geração (<see cref="DadosDoMundo.TemHospital"/>); aqui
    /// fica o que aparece perto de alguém, como o bunker (<see cref="BunkerDoMundo"/>):
    ///  - até <see cref="raio"/> m de um jogador (no anfitrião, de todos): a casca (as fachadas, a cobertura, a garagem, a
    ///    entrada e o embasamento, com a hera), o chão do terreno (<see cref="TerrenoDoHospital"/>: as calçadas, a praça, o
    ///    estacionamento, o acesso e o pátio das ambulâncias e o pátio de serviço), as áreas de dentro, a área sem zumbis e os
    ///    sons do hospital (o <see cref="AmbienteSonoro"/>, com os pontos do <see cref="SonsDoHospital"/>);
    ///  - por dentro, os andares perto de alguém: o dele e os vizinhos; de fora, até <see cref="pertoDeFora"/> m do corpo, o
    ///    térreo e os dois de cima (o subsolo só de dentro). Cada um montado em outra thread, com a colisão cozida em outra
    ///    thread, as marcas do abandono, os pisos dos passos, o eco, os móveis (os da mobília nova, com as portas e as
    ///    gavetas pela rede da mobília) e os papéis que se leem (os do <see cref="DocumentosDoHospital"/>).
    /// Sem energia: nenhuma luz. No subsolo, sem janela, a luz de fora quase não chega (<see cref="AmbienteDoDia.LuzNoHospital"/>).
    /// Nada vai pela rede (só as partes dos móveis, pela mobília): cada máquina monta o seu, igual, pela semente do mundo.
    /// </summary>
    public class HospitalDoMundo : MonoBehaviour
    {
        [Tooltip("O material das marcas do abandono (Marcas.mat: o atlas T_Marcas e o shader das marcas).")]
        public Material materialDasMarcas;
        [Tooltip("Os tipos de som do hospital (os clipes do Audio/Hospital, os pesos por andar), como os da cena de teste.")]
        public AmbienteSonoro.Tipo[] sons = new AmbienteSonoro.Tipo[0];
        [Tooltip("Até onde a casca, o terreno e os sons aparecem (m, do meio do corpo).")]
        public float raio = 300f;
        [Tooltip("De fora, até onde o térreo e os andares de cima carregam (m, da parede).")]
        public float pertoDeFora = 40f;
        [Tooltip("A luz de fora que sobra no subsolo (sem janela) e a neblina de lá.")]
        [Range(0f, 1f)] public float luzNoSubsolo = 0.15f, neblinaNoSubsolo = 0.2f;

        public static HospitalDoMundo Instancia { get; private set; }

        [Header("Fora do mundo sorteado (o Lakewood, Etapa 6)")]
        [Tooltip("O nome na torre (vazio: o de sempre).")]
        public string[] nomeNaTorre;
        [Tooltip("O arquivo dos papéis (em Resources).")]
        public string arquivoDosPapeis = "Documentos/Hospital";
        bool _temFixo;
        Vector3 _posicaoFixa;
        float _giroFixo;
        int _sementeFixa = 1;

        /// <summary>
        /// O hospital num lugar fixo (a cena sem o mundo sorteado: o Lakewood): o meio do corpo no chão de fora e o giro (o
        /// -z da obra para a rua), e a semente das portas.
        /// </summary>
        public void Fixar(Vector3 posicao, float giro, int semente)
        {
            Desmontar();
            _temFixo = true;
            _posicaoFixa = posicao;
            _giroFixo = giro;
            _sementeFixa = semente;
        }

        /// <summary>A semente da obra: a da cena de teste (as marcas, as placas do forro e a hera saem iguais às aprovadas).</summary>
        const int Semente = 7;
        const float Folga = 3f, Abandono = 0.9f;
        const int MontagensAoMesmoTempo = 2;
        // o tempo de cada quadro para pôr os móveis (uns 180 por andar)
        const double MoveisPorQuadroMs = 3.0;

        sealed class Andar
        {
            public Task<AndarMontado> Montando;
            public GameObject Objeto;
            public Mesh Desenho, Colisao, Marcas;
            public MeshCollider Colisor;
            public Task Cozimento;
            public float QueridoEm;
            public bool Pronto;
            public System.Collections.IEnumerator Pondo;
            public readonly List<ParteDoMovel> Partes = new List<ParteDoMovel>();
            // o saque do andar (sorteado junto com a obra, na outra thread), a chave da obra dele e os itens de perto
            public List<ItemDoSaque> Saque;
            public ulong Chave;
            public readonly List<SaqueNaMira> Itens = new List<SaqueNaMira>();
        }

        sealed class AndarMontado
        {
            public Hospital Obra;
            public List<ItemDoSaque> Saque;
        }

        sealed class Parte
        {
            public GameObject Objeto;
            public Mesh Desenho, Colisao;
            public MeshCollider Colisor;
            public Task Cozimento;
        }

        sealed class CascaPronta
        {
            public Hospital Casca;
            public Obra Terreno;
            public MalhaDaHera Hera;
            public List<(Vector3 lugar, int tipo, int andar)> Sons;
            public List<DocumentosDoHospital.Papel> Papeis;
        }

        DadosDoMundo _d;
        Transform _raiz;
        Task<CascaPronta> _montandoCasca;
        GameObject _casca;
        readonly List<Parte> _partesDaCasca = new List<Parte>();
        readonly List<Mesh> _malhasDaCasca = new List<Mesh>();
        List<DocumentosDoHospital.Papel> _papeis;
        float _cascaQueridaEm = -100f;
        bool _cascaPronta;
        readonly Andar[] _andares = new Andar[Andares];
        int _montando;
        readonly List<ParteDoMovel> _partesDoMovel = new List<ParteDoMovel>();
        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();
        float _luz = 1f, _neblina = 1f;

        /// <summary>O objeto do hospital no mundo (a origem da obra: o chão do térreo); null antes do mundo ou sem ele.</summary>
        public Transform Raiz => _raiz;
        /// <summary>A casca e o terreno estão montados, com a colisão.</summary>
        public bool CascaMontada => _casca != null && _cascaPronta;

        /// <summary>Quantos andares estão carregados (o F1 mostra).</summary>
        public int Carregados
        {
            get
            {
                int n = 0;
                foreach (var a in _andares) if (a != null && a.Pronto) n++;
                return n;
            }
        }

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            Desmontar();
            if (Instancia == this) Instancia = null;
        }

        /// <summary>
        /// O ponto (mundo) fica no corpo do hospital, do subsolo à cobertura: o chão do terreno passa por cima do subsolo, e
        /// quem está ali não volta para a superfície (<see cref="MundoDaPartida"/>). Só a geometria: vale com o hospital
        /// desmontado.
        /// </summary>
        public static bool Dentro(Vector3 p)
        {
            var h = Instancia;
            if (h == null || h._raiz == null) return false;
            var q = h._raiz.InverseTransformPoint(p);
            return q.x > X0 - 1f && q.x < X1 + 1f && q.z > Z0 - 1f && q.z < Z1 + 1f && q.y > Base(0) - 3f && q.y < TopoDaCobertura + 3f;
        }

        // ------------------------------------------------------------ o que cada quadro quer

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var d = mundo != null ? mundo.Dados : null;
            if (d != _d)
            {
                Desmontar();
                _d = d;
            }
            bool tem = _d != null ? _d.TemHospital : _temFixo;
            if (!tem) return;
            if (_raiz == null)
            {
                _raiz = new GameObject("Hospital Principal").transform;
                if (_d != null) _raiz.SetPositionAndRotation(_d.PosicaoDoHospital, Quaternion.Euler(0f, _d.GiroDoHospital, 0f));
                else _raiz.SetPositionAndRotation(_posicaoFixa, Quaternion.Euler(0f, _giroFixo, 0f));
            }

            float agora = Time.unscaledTime;
            var local = JogadorRede.Local;
            if (local != null) Querer(local.transform.position, agora);
            var rede = NetworkManager.Singleton;
            if (rede != null && rede.IsServer)
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j != local) Querer(j.transform.position, agora);

            RevisarACasca(agora);
            if (_casca != null) RevisarOsAndares(agora);
            ALuzDeDentro();
        }

        void Querer(Vector3 mundo, float agora)
        {
            var q = _raiz.InverseTransformPoint(mundo);
            if (new Vector2(q.x, q.z).magnitude < raio) _cascaQueridaEm = agora;
            float dx = Mathf.Max(0f, Mathf.Max(X0 - q.x, q.x - X1)), dz = Mathf.Max(0f, Mathf.Max(Z0 - q.z, q.z - Z1));
            float fora = Mathf.Sqrt(dx * dx + dz * dz);
            if (fora < 0.5f && q.y > Base(0) - 2f && q.y < TopoDaCobertura + 3f)
            {
                int f = AndarEm(q.y);
                for (int k = f - 1; k <= f + 1; k++) Marcar(k, agora);
            }
            else if (fora < pertoDeFora && q.y < TopoDaCobertura + 10f)
                for (int k = Terreo; k < Andares; k++) Marcar(k, agora);
        }

        void Marcar(int f, float agora)
        {
            if (f < 0 || f >= Andares) return;
            (_andares[f] ??= new Andar()).QueridoEm = agora;
        }

        // ------------------------------------------------------------ a casca, o terreno e o que vem com eles

        void RevisarACasca(float agora)
        {
            bool querida = agora - _cascaQueridaEm < Folga;
            if (querida && _casca == null && _montandoCasca == null)
            {
                int portas = SementeDasPortas;
                var nome = nomeNaTorre != null && nomeNaTorre.Length > 0 ? nomeNaTorre : null;
                _montandoCasca = Task.Run(() =>
                {
                    var m = Medidas.DoJogo;
                    // as portas que abrem (a parte 7): as saídas e as corta-fogo das escadas são da casca
                    var casca = new Hospital(m, -1, Semente) { portasVivas = true, sementeDasPortas = portas, nomeNaTorre = nome };
                    casca.MontarCasca();
                    var terreno = new Obra(Semente, m);
                    TerrenoDoHospital.Montar(terreno);
                    return new CascaPronta
                    {
                        Casca = casca, Terreno = terreno, Hera = AbandonoDaObra.Hera(casca, Abandono, Semente + 5, 11f),
                        // os pontos dos sons e os papéis (os móveis dos 4 andares saem junto, guardados)
                        Sons = SonsDoHospital.Pontos(), Papeis = DocumentosDoHospital.Papeis(),
                    };
                });
            }
            if (_montandoCasca != null && _montandoCasca.IsCompleted)
            {
                var tarefa = _montandoCasca;
                _montandoCasca = null;
                if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                else if (querida) PorACasca(tarefa.Result);
                else Devolver(tarefa.Result);
            }
            bool cozinhando = false;
            foreach (var p in _partesDaCasca)
            {
                if (p.Cozimento == null) continue;
                if (!p.Cozimento.IsCompleted)
                {
                    cozinhando = true;
                    continue;
                }
                p.Cozimento = null;
                p.Colisor.sharedMesh = p.Colisao;
                p.Colisor.enabled = true;
                NavMeshDoMundo.Mudou(CaixaDoTerreno());
            }
            if (_casca != null && !cozinhando) _cascaPronta = true;
            if (!querida && _casca != null && !cozinhando && !AlgumAndar()) TirarACasca();
        }

        bool AlgumAndar()
        {
            foreach (var a in _andares) if (a != null && (a.Objeto != null || a.Montando != null)) return true;
            return false;
        }

        static void Devolver(Obra o)
        {
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
        }

        static void Devolver(CascaPronta c)
        {
            Devolver(c.Casca);
            Devolver(c.Terreno);
            if (c.Hera != null) MalhaDaHera.Devolver(c.Hera);
        }

        Bounds CaixaDoTerreno()
        {
            var t = TerrenoNoMundo;
            var b = new Bounds(_raiz.position, Vector3.zero);
            foreach (var x in new[] { t.xMin, t.xMax })
            foreach (var z in new[] { t.yMin, t.yMax })
            foreach (var y in new[] { Base(0) - 1f, TopoDaCobertura + Platibanda + 2f })
                b.Encapsulate(_raiz.TransformPoint(new Vector3(x, y, z)));
            return b;
        }

        Bounds CaixaDoAndar(int f)
        {
            var b = new Bounds(_raiz.TransformPoint(new Vector3(0f, Base(f) + 1.5f, 0f)), Vector3.zero);
            foreach (var x in new[] { X0 - 1f, X1 + 1f })
            foreach (var z in new[] { Z0 - 1f, Z1 + 1f })
            foreach (var y in new[] { Base(f) - 0.7f, Teto(f) + 0.7f })
                b.Encapsulate(_raiz.TransformPoint(new Vector3(x, y, z)));
            return b;
        }

        void PorACasca(CascaPronta c)
        {
            _casca = new GameObject("Casca e terreno");
            _casca.transform.SetParent(_raiz, false);
            var material = MaterialDasCasas;
            var pisoDaCasca = NovaParte("Casca", c.Casca.vis, c.Casca.col, material).AddComponent<PisoPorZona>();
            pisoDaCasca.zonas = new Bounds[0];
            pisoDaCasca.superficies = new Superficie[0];
            pisoDaCasca.resto = Superficie.Concreto;
            var pisoDoTerreno = NovaParte("Terreno", c.Terreno.vis, c.Terreno.col, material).AddComponent<PisoPorZona>();
            pisoDoTerreno.zonas = new Bounds[0];
            pisoDoTerreno.superficies = new Superficie[0];
            pisoDoTerreno.resto = Superficie.Concreto;
            if (c.Casca.marcas.Quadros > 0 && materialDasMarcas != null) Marcas(_casca.transform, c.Casca.marcas, "Casca");
            // a hera nas paredes de fora, com o material da vegetação (o vento)
            if (c.Hera != null && c.Hera.Folhas > 0 && MaterialDaHera != null)
            {
                var h = new GameObject("Hera");
                h.transform.SetParent(_casca.transform, false);
                var m = c.Hera.ParaMesh("Hospital: a hera", false);
                _malhasDaCasca.Add(m);
                h.AddComponent<MeshFilter>().sharedMesh = m;
                var r = h.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialDaHera;
                r.shadowCastingMode = ShadowCastingMode.Off;
                var bloco = new MaterialPropertyBlock();
                bloco.SetFloat("_Cima", 0.6f);
                r.SetPropertyBlock(bloco);
            }
            // as portas que abrem da casca (as saídas para fora e as corta-fogo das escadas), pela rede das portas
            var portasDaCasca = new GameObject("Portas (a casca)");
            portasDaCasca.transform.SetParent(_casca.transform, false);
            PortasDaConstrucao.Por(portasDaCasca, c.Casca.portas, null, SementeDasPortas, ChaveDasPortas(-1));
            Devolver(c);
            _papeis = c.Papeis;

            // as áreas de dentro (a neblina rala, a chuva, a música): o corpo inteiro, a garagem e a entrada
            Dentro(_casca.transform, "Dentro (o corpo)", new Vector3(0f, (Base(0) + TopoDaCobertura) / 2f, 0f),
                   new Vector3(X1 - X0, TopoDaCobertura - Base(0), Z1 - Z0));
            var g = GaragemDasAmbulancias;
            Dentro(_casca.transform, "Dentro (a garagem)", new Vector3(g.center.x, TopoDaGaragem / 2f, g.center.y), new Vector3(g.width, TopoDaGaragem, g.height));
            var e = Entrada;
            Dentro(_casca.transform, "Dentro (a entrada)", new Vector3(e.center.x, TopoDaEntrada / 2f, e.center.y), new Vector3(e.width, TopoDaEntrada, e.height));

            // os sons do hospital (um de cada vez, raros, de pontos da planta; só com quem escuta dentro)
            if (sons != null && sons.Length > 0)
            {
                var s = new GameObject("Sons do hospital");
                s.transform.SetParent(_casca.transform, false);
                var amb = s.AddComponent<AmbienteSonoro>();
                amb.tipos = sons;
                amb.areas = SonsDoHospital.Areas();
                amb.limites = SonsDoHospital.Limites();
                amb.pontos = new List<AmbienteSonoro.Ponto>(c.Sons.Count);
                foreach (var (lugar, tipo, andar) in c.Sons) amb.pontos.Add(new AmbienteSonoro.Ponto { lugar = lugar, tipo = tipo, area = andar });
            }
            Debug.Log($"[Hospital] A casca e o terreno em ({_raiz.position.x:F0}, {_raiz.position.y:F1}, {_raiz.position.z:F0}).");
        }

        /// <summary>A semente do começo das portas (a do mundo): em cada mundo, umas fechadas e umas trancadas diferentes.</summary>
        int SementeDasPortas => _d != null ? _d.Semente : _temFixo ? _sementeFixa : 1;

        /// <summary>O "andar" da chave das portas (a casca, -1, e cada andar: o lugar é o mesmo, a raiz do hospital).</summary>
        static int ChaveDasPortas(int f) => -400 - (f + 1);

        static void Dentro(Transform pai, string nome, Vector3 meio, Vector3 tamanho)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = meio;
            go.AddComponent<AreaInterna>().tamanho = tamanho;
        }

        GameObject NovaParte(string nome, MalhaDeObra vis, MalhaDeObra col, Material material)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(_casca.transform, false);
            var p = new Parte { Objeto = go, Desenho = vis.ParaMesh($"Hospital: {nome}"), Colisao = col.ParaMesh($"Hospital: {nome} (colisão)") };
            go.AddComponent<MeshFilter>().sharedMesh = p.Desenho;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            p.Colisor = go.AddComponent<MeshCollider>();
            p.Colisor.enabled = false;
            p.Cozimento = Cozinhar(p.Colisao.GetEntityId());
            _partesDaCasca.Add(p);
            return go;
        }

        // as marcas do abandono (o sangue, a água, o mofo, os papéis), uma malha sem colisão e sem sombra
        Mesh Marcas(Transform pai, MalhaDePichacao marcas, string nome)
        {
            var go = new GameObject($"Marcas ({nome})");
            go.transform.SetParent(pai, false);
            var m = marcas.ParaMesh($"Hospital: as marcas ({nome})");
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = materialDasMarcas;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (pai == _casca.transform) _malhasDaCasca.Add(m);
            return m;
        }

        static Task Cozinhar(EntityId malha) => Task.Run(() => Physics.BakeMesh(malha, false));

        void TirarACasca()
        {
            foreach (var p in _partesDaCasca)
            {
                if (p.Desenho != null) Destroy(p.Desenho);
                if (p.Colisao != null) Destroy(p.Colisao);
            }
            _partesDaCasca.Clear();
            foreach (var m in _malhasDaCasca) if (m != null) Destroy(m);
            _malhasDaCasca.Clear();
            if (_casca != null)
            {
                var caixa = CaixaDoTerreno();
                Destroy(_casca);
                NavMeshDoMundo.Mudou(caixa);
            }
            _casca = null;
            _cascaPronta = false;
        }

        // ------------------------------------------------------------ os andares

        void RevisarOsAndares(float agora)
        {
            _relogio.Restart();
            for (int f = 0; f < Andares; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                bool querido = agora - a.QueridoEm < Folga;
                if (a.Montando != null && a.Montando.IsCompleted)
                {
                    var tarefa = a.Montando;
                    a.Montando = null;
                    _montando--;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else if (querido) PorOAndar(f, a, tarefa.Result.Obra, tarefa.Result.Saque);
                    else Devolver(tarefa.Result.Obra);
                }
                // o NavMesh do andar é refeito com a colisão cozida e os móveis postos (os dois)
                if (a.Cozimento != null && a.Cozimento.IsCompleted)
                {
                    a.Cozimento = null;
                    a.Colisor.sharedMesh = a.Colisao;
                    a.Colisor.enabled = true;
                    a.Pronto = true;
                    if (a.Pondo == null) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
                }
                while (a.Pondo != null && _relogio.Elapsed.TotalMilliseconds < MoveisPorQuadroMs)
                    if (!a.Pondo.MoveNext())
                    {
                        a.Pondo = null;
                        if (a.Pronto) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
                    }
                if (querido && a.Objeto == null && a.Montando == null && _montando < MontagensAoMesmoTempo)
                {
                    int andar = f, portas = SementeDasPortas;
                    a.Montando = Task.Run(() =>
                    {
                        var h = new Hospital(Medidas.DoJogo, andar, Semente) { portasVivas = true, sementeDasPortas = portas };
                        h.MontarAndar();
                        // o saque do andar (a parte 7), pela semente do mundo
                        return new AndarMontado { Obra = h, Saque = SaqueDoHospital.DoAndar(portas, andar) };
                    });
                    _montando++;
                }
                if (!querido && a.Objeto != null && a.Cozimento == null) TirarOAndar(f, a);
                if (!querido && a.Objeto == null && a.Montando == null) _andares[f] = null;
            }
        }

        void PorOAndar(int f, Andar a, Hospital h, List<ItemDoSaque> saque)
        {
            a.Saque = saque;
            a.Chave = MobiliaNova.ChaveDaObra(_raiz.position, 300 + f);
            var go = new GameObject($"{Codigos[f]} ({NomesDosAndares[f]})");
            go.transform.SetParent(_raiz, false);
            a.Objeto = go;
            a.Desenho = h.vis.ParaMesh($"Hospital: {Codigos[f]}");
            a.Colisao = h.col.ParaMesh($"Hospital: {Codigos[f]} (colisão)");
            go.AddComponent<MeshFilter>().sharedMesh = a.Desenho;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialDasCasas;
            a.Colisor = go.AddComponent<MeshCollider>();
            a.Colisor.enabled = false;
            a.Cozimento = Cozinhar(a.Colisao.GetEntityId());
            if (h.marcas.Quadros > 0 && materialDasMarcas != null) a.Marcas = Marcas(go.transform, h.marcas, Codigos[f]);
            // os pisos dos passos: o de cada cômodo (o taco, o carpete) e o concreto (o azulejo, o vinílico) no resto
            var pisos = go.AddComponent<PisoPorZona>();
            var zonas = new List<Bounds>();
            var superficies = new List<Superficie>();
            foreach (var k in DoAndar(f).comodos)
            {
                zonas.Add(new Bounds(new Vector3((k.x0 + k.x1) / 2f, Base(f) + 1f, (k.z0 + k.z1) / 2f), new Vector3(k.x1 - k.x0, 3f, k.z1 - k.z0)));
                superficies.Add(k.piso switch
                {
                    "Taco" or "Madeira" => Superficie.Madeira,
                    "Carpete" => Superficie.Carpete,
                    _ => Superficie.Concreto,
                });
            }
            pisos.zonas = zonas.ToArray();
            pisos.superficies = superficies.ToArray();
            pisos.resto = Superficie.Concreto;
            float alto = Teto(f) - Base(f);
            EcoSoDentro.Criar(go.transform, new Bounds(new Vector3(0f, Base(f) + alto / 2f, 0f), new Vector3(X1 - X0, alto, Z1 - Z0)));
            // as portas que abrem do andar, pela rede das portas (as folhas são filhas do andar: saem com ele)
            PortasDaConstrucao.Por(go, h.portas, null, SementeDasPortas, ChaveDasPortas(f));
            Devolver(h);
            a.Pondo = PorOsMoveis(f, a, go.transform);
        }

        /// <summary>
        /// O item do saque de um andar carregado, pela chave da obra dele (o anfitrião, no pedido de pegar): o item e a obra
        /// (o objeto do andar, onde ficam as coordenadas do lugar). False se a chave não é de um andar do hospital carregado.
        /// </summary>
        public static bool AcharNoSaque(ulong obra, int ponto, int peca, int lugar, out ItemDoSaque item, out Transform obraT)
        {
            item = default;
            obraT = null;
            var h = Instancia;
            if (h == null) return false;
            foreach (var a in h._andares)
            {
                if (a == null || a.Objeto == null || a.Saque == null || a.Chave != obra) continue;
                foreach (var it in a.Saque)
                    if (it.ponto == ponto && it.peca == peca && it.lugar == lugar)
                    {
                        item = it;
                        obraT = a.Objeto.transform;
                        return true;
                    }
                return false;
            }
            return false;
        }

        /// <summary>Quantos itens do saque estão nos andares carregados (o F1), e quantos de perto (postos nos móveis).</summary>
        public (int sorteados, int dePerto) SaqueCarregado
        {
            get
            {
                int n = 0, p = 0;
                foreach (var a in _andares)
                    if (a != null && a.Saque != null)
                    {
                        n += a.Saque.Count;
                        p += a.Itens.Count;
                    }
                return (n, p);
            }
        }

        /// <summary>O andar f está montado, com a colisão e os móveis (o NavMesh dele vem depois: o povoamento confere).</summary>
        public bool AndarPronto(int f) => f >= 0 && f < Andares && _andares[f] != null && _andares[f].Pronto && _andares[f].Pondo == null;

        void TirarOAndar(int f, Andar a)
        {
            bool tinhaColisao = a.Pronto;
            // no anfitrião: os zumbis do andar que não estão atrás de ninguém voltam ao pool (e voltam ali na volta)
            var rede = NetworkManager.Singleton;
            if (tinhaColisao && (rede == null || rede.IsServer)) ZombieManager.Instancia?.SaiuOAndarDoHospital(f, _raiz);
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo != null) foreach (var p in a.Partes) if (p != null) mundo.Esquecer(p);
            a.Partes.Clear();
            if (mundo != null) foreach (var s in a.Itens) mundo.EsquecerOSaque(s);
            a.Itens.Clear();
            a.Saque = null;
            a.Pondo = null;
            Destroy(a.Objeto);
            if (a.Desenho != null) Destroy(a.Desenho);
            if (a.Colisao != null) Destroy(a.Colisao);
            if (a.Marcas != null) Destroy(a.Marcas);
            a.Objeto = null;
            a.Desenho = a.Colisao = a.Marcas = null;
            a.Colisor = null;
            a.Pronto = false;
            if (tinhaColisao) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
        }

        // os móveis do andar (os de perto da mobília nova, com as partes que abrem pela rede da mobília), um por passo, e no
        // fim os papéis que se leem do andar
        System.Collections.IEnumerator PorOsMoveis(int f, Andar a, Transform pai)
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo != null)
            {
                var obra = a.Chave;
                int ponto = 0;
                foreach (var m in MoveisDoHospital.DoAndar(f))
                {
                    int i = ponto++;
                    var go = mundo.Novo(m.modelo, 0, pai);
                    if (go == null) continue;
                    go.transform.SetLocalPositionAndRotation(m.lugar, Quaternion.Euler(0f, m.giro, 0f));
                    go.transform.localScale = Vector3.one * m.escala;
                    if (!UI.Graficos.SombraDosMoveis) SemSombra(go);
                    // o saque (a parte 7), com as partes ainda fechadas (o lugar de cada item é o do móvel fechado)
                    mundo.PorOSaque(a.Saque, pai, go, m.modelo, obra, i, 0, a.Itens);
                    go.GetComponentsInChildren(true, _partesDoMovel);
                    for (int k = 0; k < _partesDoMovel.Count; k++)
                    {
                        var parte = _partesDoMovel[k];
                        parte.chave = MobiliaNova.ChaveDaParte(obra, i, 0, k);
                        mundo.Registrar(parte);
                        a.Partes.Add(parte);
                    }
                    yield return null;
                }
            }
            // os papéis (a camada 2: fora do NavMesh, e a mira acha do mesmo jeito)
            if (_papeis != null)
            {
                var textos = Documento.DoArquivo(string.IsNullOrEmpty(arquivoDosPapeis) ? "Documentos/Hospital" : arquivoDosPapeis);
                foreach (var p in _papeis)
                {
                    if (p.andar != f || !textos.TryGetValue(p.id, out var doc)) continue;
                    var papel = PapeisDoLugar.Um(PapeisDoLugar.De(p), doc, pai, null);
                    papel.gameObject.layer = 2;
                }
            }
        }

        static void SemSombra(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------ a luz de dentro e o fim

        // no subsolo (sem janela), a luz de fora quase não chega e a neblina some; no resto, o de sempre (a AreaInterna)
        void ALuzDeDentro()
        {
            float luz = 1f, neblina = 1f;
            var cam = Camera.main;
            if (cam != null && _raiz != null)
            {
                var q = _raiz.InverseTransformPoint(cam.transform.position);
                bool noCorpo = q.x > X0 && q.x < X1 && q.z > Z0 && q.z < Z1;
                if (noCorpo && q.y < Base(Terreo) - 0.6f && q.y > Base(0) - 2f)
                {
                    luz = luzNoSubsolo;
                    neblina = neblinaNoSubsolo;
                }
            }
            _luz = Mathf.MoveTowards(_luz, luz, Time.deltaTime * 2f);
            _neblina = Mathf.MoveTowards(_neblina, neblina, Time.deltaTime * 3f);
            AmbienteDoDia.LuzNoHospital = _luz;
            AmbienteDoDia.NeblinaNoHospital = _neblina;
        }

        // os materiais das casas e da hera: os do mundo sorteado ou, no Lakewood, os de lá
        static Material MaterialDasCasas
        {
            get
            {
                var t = TerrenoDoMundo.Instancia;
                if (t != null) return t.materialDasCasas != null ? t.materialDasCasas : t.materialDaCidade;
                var l = LakewoodNaCena.Instancia;
                return l != null ? (l.materialDasCasas != null ? l.materialDasCasas : l.materialDaCidade) : null;
            }
        }

        static Material MaterialDaHera => TerrenoDoMundo.Instancia != null ? TerrenoDoMundo.Instancia.materialDaHera : LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.materialDaHera : null;

        void Desmontar()
        {
            for (int f = 0; f < Andares; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                if (a.Objeto != null) TirarOAndar(f, a);
                a.Montando = null;
                _andares[f] = null;
            }
            _montando = 0;
            if (_casca != null || _partesDaCasca.Count > 0) TirarACasca();
            _montandoCasca = null;
            _papeis = null;
            if (_raiz != null) Destroy(_raiz.gameObject);
            _raiz = null;
            _cascaQueridaEm = -100f;
            _luz = _neblina = 1f;
            AmbienteDoDia.LuzNoHospital = 1f;
            AmbienteDoDia.NeblinaNoHospital = 1f;
        }
    }
}
