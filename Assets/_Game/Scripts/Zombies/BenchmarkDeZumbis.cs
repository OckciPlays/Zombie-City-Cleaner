using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Benchmark dos zumbis (Parte 5). Troca a população por 50, 100, 250 e 500 zumbis espalhados pela cidade e, para
    /// cada quantidade, mede o tempo de quadro, a IA, a animação, a navegação, a física, os draw calls e o tráfego de
    /// rede. No fim grava uma tabela em Markdown e volta às hordas normais. Só roda no anfitrião.
    /// - F9 começa (com a partida rodando).
    /// - "-benchmark" na linha de comando roda sozinho (junto com "-hospedar-local"), espera um cliente (o robô) por
    ///   alguns segundos, grava a tabela em "-resultado caminho.md" e fecha o jogo.
    /// Os tempos de CPU vêm dos marcadores do Profiler: só aparecem no Editor e em builds Development.
    /// </summary>
    public class BenchmarkDeZumbis : MonoBehaviour
    {
        public int[] quantidades = { 50, 100, 250, 500 };
        [Tooltip("Espera depois de criar os zumbis, antes de medir (s).")]
        public float acomodar = 4f;
        [Tooltip("Tempo de medição de cada quantidade (s).")]
        public float medir = 10f;
        [Tooltip("No modo automático, espera este tempo por outro jogador antes de começar (s).")]
        public float esperarCliente = 25f;

        public bool Rodando { get; private set; }

        /// <summary>Nome da rodada, no título da seção (por exemplo, "cidade de arte, antes da oclusão").</summary>
        public static string Rotulo = "";

        bool _automatico;
        string _arquivo, _status = "";
        float _statusAte;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-benchmark") _automatico = true;
                else if (args[i] == "-resultado" && i + 1 < args.Length) _arquivo = args[i + 1];
            }
            if (_automatico) StartCoroutine(Automatico());
        }

        void Update()
        {
            var teclado = Keyboard.current;
            var m = ZombieManager.Instancia;
            if (!Rodando && teclado != null && teclado.f9Key.wasPressedThisFrame && m != null && m.IsSpawned && m.IsServer) StartCoroutine(Rodar());
        }

        IEnumerator Automatico()
        {
            while (ZombieManager.Instancia == null || !ZombieManager.Instancia.IsSpawned || JogadorRede.Local == null) yield return null;
            float limite = Time.realtimeSinceStartup + esperarCliente;
            while (JogadorRede.Todos.Count < 2 && Time.realtimeSinceStartup < limite) yield return null;
            yield return Rodar();
            Application.Quit();
        }

        /// <summary>Roda o benchmark inteiro (também dá para chamar de fora, com a partida rodando no anfitrião).</summary>
        public IEnumerator Rodar()
        {
            var m = ZombieManager.Instancia;
            if (m == null || !m.IsServer || Rodando) yield break;
            Rodando = true;
            int vsync = QualitySettings.vSyncCount, alvo = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            m.SemDano = true;

            var tabela = new StringBuilder();
            tabela.AppendLine($"## {DateTime.Now:dd/MM/yyyy HH:mm}{(string.IsNullOrEmpty(Rotulo) ? "" : " — " + Rotulo)}");
            tabela.AppendLine();
            tabela.AppendLine($"- {(Application.isEditor ? "Editor" : "build")} {Application.unityVersion}, {Screen.width}×{Screen.height}");
            tabela.AppendLine($"- {SystemInfo.processorType} ({SystemInfo.processorCount} núcleos), {SystemInfo.graphicsDeviceName}");
            var cam = JogadorRede.Local != null && JogadorRede.Local.cameraMundo != null ? JogadorRede.Local.cameraMundo.transform : null;
            tabela.AppendLine($"- Zumbis espalhados pela cidade, {medir:F0} s de medida para cada quantidade, sem vsync. Jogadores na partida: {JogadorRede.Todos.Count}." +
                              (cam != null ? $" Câmera em {cam.position:F0}, olhando {cam.forward:F1}." : ""));
            tabela.AppendLine();
            tabela.AppendLine("| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |");
            tabela.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (int n in quantidades)
            {
                _status = $"Benchmark: criando {n} zumbis...";
                m.RepovoarEspalhado(n);
                while (m.Povoando) yield return null;
                _status = $"Benchmark: {n} zumbis, acomodando...";
                yield return new WaitForSecondsRealtime(acomodar);
                _status = $"Benchmark: {n} zumbis, medindo...";
                var medida = new Medida();
                yield return medida.Medir(m, medir);
                string linha = medida.Linha(m.Quantos);
                tabela.AppendLine(linha);
                Debug.Log($"[benchmark] {linha}");
            }

            m.SemDano = false;
            m.RepovoarHordas();
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = alvo;

            string caminho = _arquivo;
            if (string.IsNullOrEmpty(caminho))
                caminho = Application.isEditor
                    ? Path.Combine(Application.dataPath, "..", "Benchmarks", "Benchmark_Zumbis.md")
                    : Path.Combine(Application.persistentDataPath, "Benchmark_Zumbis.md");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho)));
            // cada rodada entra como uma seção nova no fim do arquivo: o histórico fica para comparar
            if (!File.Exists(caminho)) File.WriteAllText(caminho, "# Benchmark dos zumbis\n\n");
            File.AppendAllText(caminho, tabela.ToString() + "\n");
            Debug.Log($"[benchmark] tabela gravada em {Path.GetFullPath(caminho)}\n{tabela}");
            _status = $"Benchmark pronto: {Path.GetFullPath(caminho)}";
            _statusAte = Time.unscaledTime + 10f;
            Rodando = false;
        }

        /// <summary>As medidas de uma quantidade de zumbis, quadro a quadro.</summary>
        class Medida
        {
            readonly List<float> _quadros = new List<float>();
            double _thread, _ia, _rede, _animacao, _navegacao, _fisica, _drawCalls, _batches, _setPass, _triangulos, _gpu;
            long _perto, _medio, _longe;
            int _comGpu;
            float _kbPorCliente;
            readonly FrameTiming[] _tempos = new FrameTiming[1];

            public IEnumerator Medir(ZombieManager m, float duracao)
            {
                var thread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                var ia = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Zumbis.IA");
                var lod = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Zumbis.LOD");
                var rede = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Zumbis.Rede");
                var animacao = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "Animators.Update");
                var navegacao = ProfilerRecorder.StartNew(ProfilerCategory.Ai, "NavMeshManager");
                var fisica = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");
                var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                var triangulos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                try
                {
                    long bytes = m.BytesEnviados;
                    int clientes = 0;
                    float inicio = Time.realtimeSinceStartup;
                    yield return null;
                    while (Time.realtimeSinceStartup - inicio < duracao)
                    {
                        _quadros.Add(Time.unscaledDeltaTime * 1000f);
                        _thread += thread.LastValue * 1e-6;
                        _ia += (ia.LastValue + lod.LastValue) * 1e-6;
                        _rede += rede.LastValue * 1e-6;
                        _animacao += animacao.LastValue * 1e-6;
                        _navegacao += navegacao.LastValue * 1e-6;
                        _fisica += fisica.LastValue * 1e-6;
                        _drawCalls += drawCalls.LastValue;
                        _batches += batches.LastValue;
                        _setPass += setPass.LastValue;
                        _triangulos += triangulos.LastValue;
                        // tempo da GPU: precisa de "Frame Timing Stats" nas Player Settings; sem ele, fica de fora
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, _tempos) > 0 && _tempos[0].gpuFrameTime > 0)
                        {
                            _gpu += _tempos[0].gpuFrameTime;
                            _comGpu++;
                        }
                        m.ContarNiveis(out int perto, out int medio, out int longe);
                        _perto += perto; _medio += medio; _longe += longe;
                        clientes = Mathf.Max(clientes, m.ClientesNaRede);
                        yield return null;
                    }
                    float segundos = Time.realtimeSinceStartup - inicio;
                    _kbPorCliente = clientes > 0 ? (m.BytesEnviados - bytes) / 1024f / segundos / clientes : 0f;
                }
                finally
                {
                    thread.Dispose(); ia.Dispose(); lod.Dispose(); rede.Dispose();
                    animacao.Dispose(); navegacao.Dispose(); fisica.Dispose(); drawCalls.Dispose();
                    batches.Dispose(); setPass.Dispose(); triangulos.Dispose();
                }
            }

            public string Linha(int zumbis)
            {
                int n = Mathf.Max(1, _quadros.Count);
                double media = 0;
                foreach (var q in _quadros) media += q;
                media /= n;
                _quadros.Sort();
                float p95 = _quadros.Count > 0 ? _quadros[Mathf.Min(_quadros.Count - 1, Mathf.FloorToInt(_quadros.Count * 0.95f))] : 0f;
                string gpu = _comGpu > 0 ? (_gpu / _comGpu).ToString("F1") : "—";
                return $"| {zumbis} | {_perto / n} / {_medio / n} / {_longe / n} | {media:F1} | {p95:F1} | {_thread / n:F1} | {gpu} | {_ia / n:F2} | {_rede / n:F2} | " +
                       $"{_animacao / n:F2} | {_navegacao / n:F2} | {_fisica / n:F2} | {_drawCalls / n:F0} | {_batches / n:F0} | {_setPass / n:F0} | " +
                       $"{_triangulos / n / 1000:F0} | {_kbPorCliente:F1} |";
            }
        }

        void OnGUI()
        {
            if (ModoVideo.Ligado || (!Rodando && Time.unscaledTime > _statusAte)) return;
            float u = Screen.height / 720f;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * u), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            estilo.normal.textColor = new Color(0.95f, 0.85f, 0.5f);
            GUI.Label(new Rect(0, 60 * u, Screen.width, 30 * u), _status, estilo);
        }
    }
}
