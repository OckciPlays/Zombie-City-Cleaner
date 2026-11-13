using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Roda um grupo de testes do Editor (EditMode) e grava o resultado em <c>Temp/Testes_&lt;grupo&gt;.txt</c> (o resumo, cada
    /// teste com a mensagem e o que ele escreveu no log). Para o MCP: o <c>RunCommand</c> não chama a <c>TestRunnerApi</c>
    /// direto ("user interactions are not supported"), mas chama <see cref="Rodar"/> num <c>EditorApplication.delayCall</c>.
    /// Também no menu (Zombie City Cleaner > Testes > Moradores).
    /// </summary>
    public static class RodarOsTestes
    {
        public static string Arquivo(string grupo) => $"Temp/Testes_{grupo}.txt";

        [MenuItem("Zombie City Cleaner/Testes/Moradores")]
        static void Moradores() => Rodar("MoradoresTestes");

        [MenuItem("Zombie City Cleaner/Testes/Lakewood")]
        static void Lakewood() => Rodar("LakewoodTestes");

        /// <summary>Roda os testes da classe <paramref name="grupo"/> (o nome da classe, sem o namespace).</summary>
        public static void Rodar(string grupo)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Testes] Em Play: não rodei."); return; }
            if (File.Exists(Arquivo(grupo))) File.Delete(Arquivo(grupo));
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Ouvinte(grupo));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[] { $"ZombieCityCleaner.Testes.{grupo}" },
            }) { runSynchronously = true });
        }

        sealed class Ouvinte : ICallbacks
        {
            readonly string _grupo;
            public Ouvinte(string grupo) => _grupo = grupo;

            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor r) { }

            public void RunFinished(ITestResultAdaptor r)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{r.ResultState}: passou {r.PassCount}, falhou {r.FailCount}, {r.Duration:F1} s");
                Juntar(r, sb);
                File.WriteAllText(Arquivo(_grupo), sb.ToString());
                Debug.Log($"[Testes] {_grupo}: passou {r.PassCount}, falhou {r.FailCount} ({Arquivo(_grupo)})");
            }

            static void Juntar(ITestResultAdaptor r, StringBuilder sb)
            {
                if (!r.HasChildren)
                {
                    sb.AppendLine($"--- {r.Name}: {r.ResultState} ({r.Duration:F1} s)");
                    if (!string.IsNullOrEmpty(r.Message)) sb.AppendLine(r.Message);
                    if (!string.IsNullOrEmpty(r.Output)) sb.AppendLine(r.Output);
                }
                foreach (var c in r.Children) Juntar(c, sb);
            }
        }
    }
}
