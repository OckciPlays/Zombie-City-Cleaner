using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta a cena dos vídeos de divulgação (<c>Scenes/Video.unity</c>) a partir de uma cópia da Demo_Cidade: a mesma
    /// cidade, o bunker, os zumbis e o clima, mais o "Diretor do vídeo" com a câmera dele (veja <see cref="DiretorDoVideo"/>).
    /// - nada fica estático (as peças da cidade vão se mexer nas tomadas) e a oclusão assada da Demo_Cidade sai;
    /// - a "Câmera de teste" do menu sai: quem filma é a câmera do vídeo.
    /// A cena não entra nas Build Settings. Mudou a Demo_Cidade (rodou a montagem da cidade, dos zumbis...), rode esta
    /// de novo: ela apaga e refaz a Video.unity, então mudanças feitas à mão nela se perdem.
    /// Menu: Zombie City Cleaner > Montar cena de vídeo.
    /// </summary>
    public static class MontagemDoVideo
    {
        const string Origem = "Assets/_Game/Scenes/Demo_Cidade.unity";
        const string Destino = "Assets/_Game/Scenes/Video.unity";

        [MenuItem("Zombie City Cleaner/Montar cena de vídeo")]
        public static void Montar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destino) != null) AssetDatabase.DeleteAsset(Destino);
            if (!AssetDatabase.CopyAsset(Origem, Destino))
            {
                Debug.LogError($"[vídeo] não deu para copiar {Origem}");
                return;
            }
            // a oclusão assada é da Demo_Cidade, com as peças paradas no lugar: aqui elas caem do céu e sobem do chão
            string caminho = Path.GetFullPath(Destino);
            File.WriteAllText(caminho, Regex.Replace(File.ReadAllText(caminho), @"m_OcclusionCullingData: \{[^}]*\}", "m_OcclusionCullingData: {fileID: 0}"));
            AssetDatabase.ImportAsset(Destino);

            var cena = EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);
            int soltos = 0;
            foreach (var raiz in cena.GetRootGameObjects())
            {
                if (raiz.name == "Câmera de teste")
                {
                    Object.DestroyImmediate(raiz);
                    continue;
                }
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    soltos++;
                }
            }
            var diretor = Diretor();
            Selection.activeGameObject = diretor.gameObject;
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"[vídeo] cena montada em {Destino} ({soltos} objetos deixaram de ser estáticos)");
        }

        // o diretor e a câmera do vídeo, com os ajustes da câmera do mundo do jogador (a mesma imagem), mais longe
        static DiretorDoVideo Diretor()
        {
            var diretor = new GameObject("Diretor do vídeo").AddComponent<DiretorDoVideo>();
            var objeto = new GameObject("Câmera do vídeo") { tag = "MainCamera" };
            objeto.transform.SetParent(diretor.transform, false);
            objeto.transform.SetPositionAndRotation(new Vector3(-14f, 9f, -28f), Quaternion.LookRotation(new Vector3(20f, -7.5f, 36f)));

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Jogador.prefab").GetComponent<JogadorRede>().cameraMundo;
            var dadosDoModelo = modelo.GetUniversalAdditionalCameraData();
            var camera = objeto.AddComponent<Camera>();
            camera.fieldOfView = modelo.fieldOfView;
            camera.nearClipPlane = modelo.nearClipPlane;
            camera.farClipPlane = 400f;   // vista do alto, a cidade inteira cabe
            camera.clearFlags = modelo.clearFlags;
            camera.backgroundColor = modelo.backgroundColor;
            camera.cullingMask = modelo.cullingMask;
            camera.allowHDR = modelo.allowHDR;
            camera.allowMSAA = modelo.allowMSAA;
            camera.useOcclusionCulling = false;
            var dados = camera.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = dadosDoModelo.renderPostProcessing;
            dados.antialiasing = dadosDoModelo.antialiasing;
            dados.antialiasingQuality = dadosDoModelo.antialiasingQuality;
            dados.requiresDepthTexture = dadosDoModelo.requiresDepthTexture;
            dados.requiresColorTexture = dadosDoModelo.requiresColorTexture;
            dados.renderShadows = dadosDoModelo.renderShadows;

            diretor.cameraDoVideo = camera;
            diretor.ouvidoDoVideo = objeto.AddComponent<AudioListener>();
            return diretor;
        }
    }
}
