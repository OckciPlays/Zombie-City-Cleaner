using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O benchmark da cidade (Plano_Mapa, Parte M5): no centro da maior cidade do mapa, de dia e de noite. Em cada rodada o
    /// jogador (voando) faz o mesmo percurso: uma volta inteira no cruzamento do meio, na altura dos olhos, e depois a
    /// avenida do meio, a 8 m/s. Mede o tempo de quadro, a thread principal, a GPU, os draw calls, os batches, o SetPass e
    /// os triângulos, e grava a tabela em `Benchmarks/Benchmark_Cidade.md` (cada rodada uma seção nova no fim). No
    /// anfitrião, pelo F1 do mundo. No fim, a hora, o relógio e o lugar do jogador voltam como estavam.
    /// A oclusão pela GPU (a do GPU Resident Drawer) fica como está no asset da URP, e a tabela diz qual: ligada no meio
    /// do Play (26/10/2026), ela derrubou o driver de vídeo (D3D11, "device removed") e o Editor fechou. Para comparar,
    /// mude o asset fora do Play e rode de novo.
    /// </summary>
    public static class BenchmarkDaCidade
    {
        public static bool Rodando { get; private set; }
        public static string Status { get; private set; } = "";

        const float Acomodar = 4f, Volta = 8f, Avenida = 14f, Velocidade = 8f;

        /// <summary>O percurso inteiro: as duas rodadas e a tabela. <paramref name="avisar"/> recebe o resultado.</summary>
        public static IEnumerator Rodar(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, Action<string> avisar)
        {
            if (Rodando || movimento == null || d == null) yield break;
            TracadoDaCidade cidade = null;
            foreach (var t in d.Plano.Tracados)
                if (!t.EhVila && (cidade == null || t.Cidade.Raio > cidade.Cidade.Raio)) cidade = t;
            if (cidade == null) { avisar?.Invoke("Este mapa não tem cidade."); yield break; }

            Rodando = true;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool oclusao = urp != null && urp.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled && urp.gpuResidentDrawerEnableOcclusionCullingInCameras;
            bool paradoAntes = CicloDoDia.Parado, voandoAntes = movimento.Voando;
            float horaAntes = CicloDoDia.Hora;
            var lugarAntes = movimento.transform.position;
            float yawAntes = movimento.transform.eulerAngles.y;
            int vsync = QualitySettings.vSyncCount, alvo = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            if (CicloDoDia.Instancia != null) CicloDoDia.Instancia.SempreDeDia(true);
            movimento.Voando = true;

            var tabela = new StringBuilder();
            var cam = Camera.main;
            tabela.AppendLine($"## {DateTime.Now:dd/MM/yyyy HH:mm} — {cidade.Cidade.Nome}, mapa {Tamanhos.NomeComLado(d.Tamanho)}, semente {d.Semente}");
            tabela.AppendLine();
            tabela.AppendLine($"- {(Application.isEditor ? "Editor" : "build")} {Application.unityVersion}, {Screen.width}×{Screen.height}");
            tabela.AppendLine($"- {SystemInfo.processorType} ({SystemInfo.processorCount} núcleos), {SystemInfo.graphicsDeviceName}");
            int construcoes = 0;
            foreach (var l in cidade.Lotes) if (l.TemConstrucao) construcoes++;
            tabela.AppendLine($"- A cidade: raio de {cidade.Cidade.Raio:F0} m, {construcoes} construções. O percurso: uma volta de {Volta:F0} s no cruzamento do meio " +
                              $"e {Avenida:F0} s pela avenida do meio a {Velocidade:F0} m/s, sem vsync" +
                              (cam != null ? $", com a câmera vendo até {cam.farClipPlane:F0} m" : "") + ".");
            tabela.AppendLine();
            tabela.AppendLine("| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |");
            tabela.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            try
            {
                foreach (bool noite in new[] { false, true })
                {
                    string nome = noite ? "noite" : "dia";
                    CicloDoDia.MudarHora(noite ? 23f : 12f);
                    Status = $"Benchmark da cidade: {nome}, esperando o chão...";
                    Posicionar(movimento, d, cidade, 0f);
                    float limite = Time.realtimeSinceStartup + 20f;
                    var terreno = TerrenoDoMundo.Instancia;
                    while (terreno != null && !terreno.Pronto(movimento.transform.position, 60f) && Time.realtimeSinceStartup < limite)
                    {
                        Posicionar(movimento, d, cidade, 0f);
                        yield return null;
                    }
                    float acomodar = Time.realtimeSinceStartup + Acomodar;
                    while (Time.realtimeSinceStartup < acomodar)
                    {
                        Posicionar(movimento, d, cidade, 0f);
                        yield return null;
                    }
                    Status = $"Benchmark da cidade: {nome}, medindo...";
                    var medida = new Medida();
                    yield return medida.Medir(movimento, d, cidade);
                    string linha = medida.Linha(noite ? "23h" : "12h", oclusao);
                    tabela.AppendLine(linha);
                    Debug.Log($"[benchmark da cidade] {linha}");
                }
            }
            finally
            {
                QualitySettings.vSyncCount = vsync;
                Application.targetFrameRate = alvo;
                if (CicloDoDia.Instancia != null) CicloDoDia.Instancia.SempreDeDia(paradoAntes);
                CicloDoDia.MudarHora(paradoAntes ? 12f : horaAntes);
                if (movimento != null)
                {
                    movimento.Voando = voandoAntes;
                    movimento.Teleportar(lugarAntes, yawAntes);
                    movimento.Olhar(yawAntes, 0f);
                }
                Rodando = false;
            }

            string caminho = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", "Benchmarks", "Benchmark_Cidade.md")
                : Path.Combine(Application.persistentDataPath, "Benchmark_Cidade.md");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho)));
            if (!File.Exists(caminho)) File.WriteAllText(caminho, "# Benchmark da cidade\n\nO centro da maior cidade do mapa (Plano_Mapa, Parte M5), de dia e de noite, com a oclusão pela GPU como estava no asset da URP.\n\n");
            File.AppendAllText(caminho, tabela + "\n");
            Debug.Log($"[benchmark da cidade] tabela gravada em {Path.GetFullPath(caminho)}\n{tabela}");
            Status = "";
            avisar?.Invoke($"Benchmark da cidade pronto: {Path.GetFullPath(caminho)}");
        }

        /// <summary>
        /// O jogador no percurso, aos <paramref name="t"/> segundos: a volta no cruzamento do meio (olhando para todos os
        /// lados, de nível) e depois a avenida do meio, olhando para a frente, balançando um pouco o olhar.
        /// </summary>
        static void Posicionar(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, TracadoDaCidade cidade, float t)
        {
            var frente = cidade.EixoU;
            float yawDaAvenida = Mathf.Atan2(frente.x, frente.y) * Mathf.Rad2Deg;
            Vector2 p;
            float yaw;
            if (t < Volta)
            {
                p = cidade.Mundo(0f, 0f);
                yaw = yawDaAvenida + 360f * t / Volta;
            }
            else
            {
                float u = Velocidade * (t - Volta);
                p = cidade.Mundo(u, 0f);
                var adiante = cidade.Mundo(u + 2f, 0f) - p;
                yaw = Mathf.Atan2(adiante.x, adiante.y) * Mathf.Rad2Deg + 20f * Mathf.Sin((t - Volta) * 0.9f);
            }
            // (os pés no asfalto: os olhos ficam na altura de quem anda)
            movimento.transform.position = new Vector3(p.x, d.Altura(p.x, p.y) + TracadoDaCidade.AlturaDoAsfalto + 0.02f, p.y);
            movimento.Olhar(yaw, 2f);
        }

        /// <summary>As medidas de uma rodada, quadro a quadro, enquanto o jogador faz o percurso.</summary>
        class Medida
        {
            readonly List<float> _quadros = new List<float>();
            double _thread, _drawCalls, _batches, _setPass, _triangulos, _gpu;
            int _comGpu, _blocos, _luzes, _zumbis;
            readonly FrameTiming[] _tempos = new FrameTiming[1];

            public IEnumerator Medir(MovimentoPrimeiraPessoa movimento, DadosDoMundo d, TracadoDaCidade cidade)
            {
                var thread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                var triangulos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                try
                {
                    float inicio = Time.realtimeSinceStartup;
                    Posicionar(movimento, d, cidade, 0f);
                    yield return null;
                    while (true)
                    {
                        float t = Time.realtimeSinceStartup - inicio;
                        if (t >= Volta + Avenida) break;
                        _quadros.Add(Time.unscaledDeltaTime * 1000f);
                        _thread += thread.LastValue * 1e-6;
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
                        Posicionar(movimento, d, cidade, t);
                        yield return null;
                    }
                    var terreno = TerrenoDoMundo.Instancia;
                    _blocos = terreno != null ? terreno.Blocos : 0;
                    foreach (var luz in UnityEngine.Object.FindObjectsByType<Light>())
                        if (luz.isActiveAndEnabled && luz.type != LightType.Directional && luz.intensity > 0f) _luzes++;
                    _zumbis = ZombieManager.Instancia != null ? ZombieManager.Instancia.Quantos : 0;
                }
                finally
                {
                    thread.Dispose(); drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); triangulos.Dispose();
                }
            }

            public string Linha(string hora, bool oclusao)
            {
                int n = Mathf.Max(1, _quadros.Count);
                double media = 0;
                foreach (var q in _quadros) media += q;
                media /= n;
                _quadros.Sort();
                float p95 = _quadros.Count > 0 ? _quadros[Mathf.Min(_quadros.Count - 1, Mathf.FloorToInt(_quadros.Count * 0.95f))] : 0f;
                float pior = _quadros.Count > 0 ? _quadros[_quadros.Count - 1] : 0f;
                string gpu = _comGpu > 0 ? (_gpu / _comGpu).ToString("F1") : "—";
                return $"| {hora} | {(oclusao ? "ligada" : "desligada")} | {media:F1} | {p95:F1} | {pior:F1} | {_thread / n:F1} | {gpu} | {_drawCalls / n:F0} | " +
                       $"{_batches / n:F0} | {_setPass / n:F0} | {_triangulos / n / 1000:F0} | {_blocos} | {_luzes} | {_zumbis} |";
            }
        }
    }
}
