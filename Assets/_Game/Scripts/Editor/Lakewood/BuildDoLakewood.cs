using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A build do Lakewood para testar com os amigos (10/11/2026, a pedido: "consegue gerar uma build para eu testar com
    /// meus amigos dessa cena do Lakewood?"): só a cena Lakewood (ela abre no menu: hospedar pelo Relay, ou entrar com o
    /// código), sem Development, em `Builds/Lakewood/ZombieCityCleaner.exe` (ignorada pelo git). Antes, o material do relevo
    /// de longe salvo vai para a cena (feito na hora, ele não ia para a build). Os Preloaded Assets do Player Settings
    /// voltam a ser o que eram. As Build Settings não mudam. O resultado vai para o console e para `Builds/Lakewood/build.txt`.
    /// </summary>
    public static class BuildDoLakewood
    {
        public const string Pasta = "Builds/Lakewood";
        public const string Exe = Pasta + "/ZombieCityCleaner.exe";

        [MenuItem("Zombie City Cleaner/Lakewood/Build para testar (só o Lakewood)", false, 60)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes da build do Lakewood.");
                return;
            }
            // a cena com o material de longe salvo
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != MontagemDeLakewood.Cena)
            {
                if (cena.isDirty)
                {
                    Debug.LogWarning("A cena aberta tem mudanças sem salvar: salve ou abra a cena do Lakewood antes da build.");
                    return;
                }
                cena = EditorSceneManager.OpenScene(MontagemDeLakewood.Cena);
            }
            var lakewood = Object.FindAnyObjectByType<LakewoodNaCena>();
            var longe = MontagemDeLakewood.MaterialDoRelevoDeLonge();
            if (lakewood != null && longe != null && lakewood.materialDeLonge != longe)
            {
                lakewood.materialDeLonge = longe;
                EditorUtility.SetDirty(lakewood);
                EditorSceneManager.MarkSceneDirty(cena);
            }
            if (cena.isDirty) EditorSceneManager.SaveScene(cena);

            var preloaded = PlayerSettings.GetPreloadedAssets();
            string texto;
            try
            {
                var relatorio = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MontagemDeLakewood.Cena },
                    locationPathName = Exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                var s = relatorio.summary;
                texto = $"{s.result} em {s.totalTime.TotalSeconds:F0} s, {s.totalErrors} erros, {s.totalWarnings} avisos, {s.totalSize / 1048576} MB";
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
                AssetDatabase.SaveAssets();
            }
            Directory.CreateDirectory(Pasta);
            File.WriteAllText(Pasta + "/build.txt", texto);
            Debug.Log("[Build do Lakewood] " + texto);
        }
    }
}
