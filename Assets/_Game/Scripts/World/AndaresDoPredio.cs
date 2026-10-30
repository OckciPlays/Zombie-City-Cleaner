using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os andares por dentro de um prédio de verdade (Plano_Mapa, Parte M6, segunda fatia). A casca (a fachada, as lajes, a
    /// escada e o elevador) é do bloco (<see cref="TerrenoDoMundo"/>); por dentro, só os andares perto de alguém carregam:
    /// os dois de cima e os dois de baixo de quem está no prédio (na cobertura, os dois de baixo dela) e, de fora, até
    /// <see cref="PertoDeFora"/> m dele, o térreo e o primeiro. No anfitrião contam todos os jogadores (os zumbis rodam lá);
    /// nos outros, só o local. Cada andar é montado em outra thread (<see cref="MalhasDasCasas.MontarAndar"/>, até
    /// <see cref="MontagensAoMesmoTempo"/> de uma vez em todos os prédios), entregue um por quadro e solto 3 s depois de
    /// ninguém precisar: um objeto com o desenho, a colisão (cozida em outra thread), os pisos e o eco. Enquanto o andar
    /// não carrega, as janelas dele ficam escuras. Ao mudar a colisão, avisa o NavMesh (<see cref="NavMeshDoMundo.Mudou"/>).
    /// </summary>
    public class AndaresDoPredio : MonoBehaviour
    {
        /// <summary>De fora, até onde o térreo e o primeiro andar carregam (m, da parede).</summary>
        public const float PertoDeFora = 30f;
        const float Folga = 3f;
        const int MontagensAoMesmoTempo = 2;

        sealed class Andar
        {
            public Task<MalhasDasCasas.AndarPronto> Montando;
            public GameObject Objeto;
            public Mesh Desenho, Colisao;
            public MeshCollider Colisor;
            public Task Cozimento;
            public float QueridoEm;
            public bool Pronto;
        }

        PlantaDePredio _planta;
        MeshRenderer[] _escuros;
        Mesh[] _malhasEscuras;
        Material _material;
        Andar[] _andares;
        readonly Dictionary<object, (int de, int ate)> _pedidos = new Dictionary<object, (int, int)>();

        static int _montando, _quadroDaEntrega, _entreguesNoQuadro;

        // sem recarregar o domínio ao entrar no Play, os contadores de uma partida passariam para a outra
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => _montando = _quadroDaEntrega = _entreguesNoQuadro = 0;

        public PlantaDePredio Planta => _planta;

        /// <summary>Os prédios de verdade agora (o povoamento dos andares, ZombieManager.Predios.cs).</summary>
        public static readonly List<AndaresDoPredio> Ativos = new List<AndaresDoPredio>();

        void OnEnable() => Ativos.Add(this);

        void OnDisable() => Ativos.Remove(this);

        /// <summary>Algum andar está com a colisão cozinhando (o bloco espera para tirar o prédio).</summary>
        public bool Cozinhando
        {
            get
            {
                if (_andares == null) return false;
                foreach (var a in _andares) if (a != null && a.Cozimento != null && !a.Cozimento.IsCompleted) return true;
                return false;
            }
        }

        /// <summary>Quantos andares estão carregados (com a colisão).</summary>
        public int Carregados
        {
            get
            {
                int n = 0;
                if (_andares != null) foreach (var a in _andares) if (a != null && a.Pronto) n++;
                return n;
            }
        }

        public void Ligar(PlantaDePredio planta, MeshRenderer[] escuros, Material material)
        {
            _planta = planta;
            _escuros = escuros;
            _material = material;
            _andares = new Andar[planta.andares];
            _malhasEscuras = new Mesh[escuros.Length];
            for (int f = 0; f < escuros.Length; f++)
                if (escuros[f] != null) _malhasEscuras[f] = escuros[f].GetComponent<MeshFilter>().sharedMesh;
        }

        /// <summary>Pede os andares de 'de' a 'ate' (a conferência dos prédios), até <see cref="Esquecer"/>.</summary>
        public void Pedir(object dono, int de, int ate) => _pedidos[dono] = (de, ate);
        public void Esquecer(object dono) => _pedidos.Remove(dono);

        /// <summary>Os andares de 'de' a 'ate' estão carregados, com a colisão.</summary>
        public bool CarregadosDe(int de, int ate)
        {
            for (int f = Mathf.Max(0, de); f <= Mathf.Min(ate, _andares.Length - 1); f++)
                if (_andares[f] == null || !_andares[f].Pronto) return false;
            return true;
        }

        /// <summary>A caixa de um andar no mundo (para o NavMesh).</summary>
        public Bounds CaixaDoAndar(int f)
        {
            float b = _planta.Base(f), h = _planta.Base(f + 1) - b;
            float lado = Mathf.Max(_planta.W, _planta.D) + 1f;
            return new Bounds(transform.TransformPoint(new Vector3(0f, b + h / 2, 0f)), new Vector3(lado, h + 1f, lado));
        }

        void Update()
        {
            if (_planta == null) return;
            float agora = Time.unscaledTime;
            var local = JogadorRede.Local;
            if (local != null) Querer(local.transform.position, agora);
            var rede = NetworkManager.Singleton;
            if (rede != null && rede.IsServer)
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j != local) Querer(j.transform.position, agora);
            foreach (var (de, ate) in _pedidos.Values)
                for (int f = de; f <= ate; f++) Marcar(f, agora);

            for (int f = 0; f < _andares.Length; f++)
            {
                var a = _andares[f];
                if (a == null) continue;
                bool querido = agora - a.QueridoEm < Folga;
                if (a.Montando != null && a.Montando.IsCompleted && PodeEntregar())
                {
                    var tarefa = a.Montando;
                    a.Montando = null;
                    _montando--;
                    if (tarefa.IsFaulted) Debug.LogException(tarefa.Exception);
                    else if (querido) Por(f, a, tarefa.Result);
                    else MalhasDasCasas.Devolver(tarefa.Result);
                }
                if (a.Cozimento != null && a.Cozimento.IsCompleted)
                {
                    a.Cozimento = null;
                    a.Colisor.sharedMesh = a.Colisao;
                    a.Colisor.enabled = true;
                    a.Pronto = true;
                    NavMeshDoMundo.Mudou(CaixaDoAndar(f));
                }
                if (querido && a.Objeto == null && a.Montando == null && _montando < MontagensAoMesmoTempo)
                {
                    a.Montando = Montar(_planta, f);
                    _montando++;
                }
                if (!querido && a.Objeto != null && a.Cozimento == null) Tirar(f, a);
                if (!querido && a.Objeto == null && a.Montando == null) _andares[f] = null;
            }
        }

        static Task<MalhasDasCasas.AndarPronto> Montar(PlantaDePredio planta, int f) => Task.Run(() => MalhasDasCasas.MontarAndar(planta, f));

        void Querer(Vector3 mundo, float agora)
        {
            var q = transform.InverseTransformPoint(mundo);
            float dx = Mathf.Max(0f, Mathf.Abs(q.x) - _planta.W / 2), dz = Mathf.Max(0f, Mathf.Abs(q.z) - _planta.D / 2);
            float fora = Mathf.Sqrt(dx * dx + dz * dz);
            if (fora < 0.5f && q.y > PlantaDePredio.Piso - 1.5f && q.y < _planta.Cobertura + 4f)
            {
                int f = _planta.AndarEm(q.y);
                for (int k = f - 2; k <= f + 2; k++) Marcar(k, agora);
            }
            else if (fora < PertoDeFora && q.y < _planta.Base(Mathf.Min(2, _planta.andares)))
            {
                Marcar(0, agora);
                Marcar(1, agora);
            }
        }

        void Marcar(int f, float agora)
        {
            if (f < 0 || f >= _andares.Length) return;
            (_andares[f] ??= new Andar()).QueridoEm = agora;
        }

        // um andar por quadro, em todos os prédios (o Mesh do andar tem ~30 mil vértices)
        static bool PodeEntregar()
        {
            if (Time.frameCount != _quadroDaEntrega)
            {
                _quadroDaEntrega = Time.frameCount;
                _entreguesNoQuadro = 0;
            }
            if (_entreguesNoQuadro >= 1) return false;
            _entreguesNoQuadro++;
            return true;
        }

        void Por(int f, Andar a, MalhasDasCasas.AndarPronto r)
        {
            var go = new GameObject($"Andar {f}");
            go.transform.SetParent(transform, false);
            a.Objeto = go;
            MoveisDaConstrucao.Por(go, r.Moveis);
            a.Desenho = r.Desenho.ParaMesh($"{name}: andar {f}");
            a.Colisao = r.Colisao.ParaMesh($"{name}: andar {f} (colisão)");
            go.AddComponent<MeshFilter>().sharedMesh = a.Desenho;
            go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            a.Colisor = go.AddComponent<MeshCollider>();
            a.Colisor.enabled = false;
            a.Cozimento = Cozinhar(a.Colisao.GetEntityId());
            var pisos = go.AddComponent<PisoPorZona>();
            pisos.zonas = new Bounds[r.Zonas.Count];
            pisos.superficies = new Superficie[r.Zonas.Count];
            for (int i = 0; i < r.Zonas.Count; i++)
            {
                pisos.zonas[i] = new Bounds(r.Zonas[i].Centro, r.Zonas[i].Tamanho);
                pisos.superficies[i] = SomDoPiso(r.Zonas[i].Piso);
            }
            pisos.resto = Superficie.Concreto;
            float b = _planta.Base(f), h = _planta.PeDireitoDe(f);
            // o eco de sala, só com o ouvido dentro deste andar (a esfera vazava para o telhado e para a rua)
            EcoSoDentro.Criar(go.transform, new Bounds(new Vector3(0f, b + h / 2, 0f), new Vector3(_planta.W, h, _planta.D)));
            if (_escuros[f] != null) _escuros[f].enabled = false;
            MalhasDasCasas.Devolver(r);
        }

        static Task Cozinhar(EntityId malha) => Task.Run(() => Physics.BakeMesh(malha, false));

        static Superficie SomDoPiso(string piso) => piso switch
        {
            "Taco" or "Madeira" or "MadeiraVelha" => Superficie.Madeira,
            "Carpete" => Superficie.Carpete,
            _ => Superficie.Concreto,
        };

        void Tirar(int f, Andar a)
        {
            bool tinhaColisao = a.Pronto;
            Destroy(a.Objeto);
            if (a.Desenho != null) Destroy(a.Desenho);
            if (a.Colisao != null) Destroy(a.Colisao);
            a.Objeto = null;
            a.Desenho = a.Colisao = null;
            a.Colisor = null;
            a.Pronto = false;
            if (_escuros[f] != null) _escuros[f].enabled = true;
            if (tinhaColisao) NavMeshDoMundo.Mudou(CaixaDoAndar(f));
        }

        void OnDestroy()
        {
            if (_andares != null)
                foreach (var a in _andares)
                {
                    if (a == null) continue;
                    // (a montagem em andamento termina sozinha, e o resultado é ignorado; o bloco só tira o prédio sem cozimento)
                    if (a.Montando != null) _montando--;
                    if (a.Desenho != null) Destroy(a.Desenho);
                    if (a.Colisao != null) Destroy(a.Colisao);
                }
            if (_malhasEscuras != null)
                foreach (var malha in _malhasEscuras)
                    if (malha != null) Destroy(malha);
        }
    }
}
