using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os carros abandonados do mundo gerado com o capô que abre, as rodas e o macaco (como os da Demo_Cidade):
    ///  - um kit por modelo do Cidade_Rua.fbx (Prefabs/Veiculos/Mundo/Kit_&lt;modelo&gt;.prefab): o carro com o
    ///    <see cref="CapoDaCidade"/>, o "Capô aberto" (o capô recortado, o cofre e as 4 rodas à parte), o macaco e os
    ///    gatilhos das rodas, pela mesma montagem dos carros da cidade (<see cref="PorOCapo"/>);
    ///  - na cena Mundo, o <see cref="ObjetosDoMundo"/> recebe os kits (e cria os carros de pé com eles) e o objeto "Mundo"
    ///    ganha o <see cref="PecasDosCarrosDoMundo"/> (o estado de cada carro, pela semente e pela rede).
    /// Menu Zombie City Cleaner > Mundo > Montar os carros do mundo; a montagem da cena do mundo também chama.
    /// </summary>
    public static partial class MontagemDosVeiculos
    {
        const string PastaDosKits = PastaPrefabs + "/Mundo";
        const string FbxDaRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";

        [MenuItem("Zombie City Cleaner/Mundo/Montar os carros do mundo (capô, rodas e macaco)")]
        public static void CarrosDoMundo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os carros do mundo.");
                return;
            }
            var kits = MontarOsKits();
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != MontagemDoMundo.Cena) cena = EditorSceneManager.OpenScene(MontagemDoMundo.Cena);
            var mundo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Mundo");
            if (mundo == null)
            {
                Debug.LogWarning("A cena do mundo não tem o objeto \"Mundo\": monte a cena antes.");
                return;
            }
            PorOsCarrosNoMundo(mundo, kits);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Carros do mundo: {kits.Count(k => k != null)} kits em {PastaDosKits}, ligados na cena do mundo.");
        }

        /// <summary>Os kits dos carros (refeitos), na ordem de <c>ModelosDaEstrada.NomesDosCarros</c>.</summary>
        public static GameObject[] MontarOsKits()
        {
            Directory.CreateDirectory(PastaDosKits);
            var m = Materiais();
            _recortes.Clear();
            var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxDaRua).OfType<Mesh>().ToArray();
            var nomes = Mundo.ModelosDaEstrada.NomesDosCarros;
            var kits = new GameObject[nomes.Length];
            for (int i = 0; i < nomes.Length; i++)
            {
                var malha = malhas.FirstOrDefault(x => x.name == nomes[i]);
                if (malha == null)
                {
                    Debug.LogWarning($"Sem a malha {nomes[i]} no {FbxDaRua}: o carro fica sem kit.");
                    continue;
                }
                var raiz = new GameObject(nomes[i]);
                var mf = raiz.AddComponent<MeshFilter>();
                mf.sharedMesh = malha;
                raiz.AddComponent<MeshRenderer>().sharedMaterial = m.cidade;
                var capo = PorOCapo(mf, null, 0, m);
                string caminho = $"{PastaDosKits}/Kit_{nomes[i]}.prefab";
                if (capo != null) kits[i] = PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                else Debug.LogWarning($"O {nomes[i]} não tem capô para recortar: fica sem kit.");
                Object.DestroyImmediate(raiz);
            }
            AssetDatabase.SaveAssets();
            return kits;
        }

        /// <summary>Liga os kits no ObjetosDoMundo e põe o PecasDosCarrosDoMundo no objeto "Mundo" (sem refazer a cena).</summary>
        public static void PorOsCarrosNoMundo(GameObject mundo, GameObject[] kits = null)
        {
            if (kits == null)
            {
                var nomes = Mundo.ModelosDaEstrada.NomesDosCarros;
                kits = nomes.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaDosKits}/Kit_{n}.prefab")).ToArray();
                if (kits.Any(k => k == null)) kits = MontarOsKits();
            }
            var objetos = mundo.GetComponent<ObjetosDoMundo>();
            if (objetos != null)
            {
                objetos.kitsDosCarros = kits;
                EditorUtility.SetDirty(objetos);
            }
            if (mundo.GetComponent<PecasDosCarrosDoMundo>() == null) mundo.AddComponent<PecasDosCarrosDoMundo>();
            EditorUtility.SetDirty(mundo);
        }
    }
}
