using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O jogo do mundo sorteado no Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood"): põe na
    /// cena do Lakewood o objeto "Mundo (Lakewood)", com o NetworkObject e o <see cref="MundoDeLakewood"/> (a semente da
    /// partida e as listas da rede), e os sistemas do mundo que moram nele (cada parte acrescenta os dela). A montagem da
    /// cena (3. Montar a cena) chama isto; o menu põe na cena aberta sem refazer o resto.
    /// </summary>
    public static class JogoNoLakewood
    {
        public const string NomeDoObjeto = "Mundo (Lakewood)";

        [MenuItem("Zombie City Cleaner/Lakewood/O jogo do mundo sorteado na cena", false, 9)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de pôr o jogo na cena do Lakewood.");
                return;
            }
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != MontagemDeLakewood.Cena)
            {
                Debug.LogWarning("Abra a cena do Lakewood antes.");
                return;
            }
            var go = PorNaCena(cena);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"[Lakewood] O jogo do mundo sorteado na cena: o \"{NomeDoObjeto}\" com {string.Join(", ", go.GetComponents<MonoBehaviour>().Select(c => c.GetType().Name))}.");
        }

        /// <summary>O objeto "Mundo (Lakewood)" na cena (o de antes, se já houver), com os componentes de todas as partes.</summary>
        public static GameObject PorNaCena(Scene cena)
        {
            var go = cena.GetRootGameObjects().FirstOrDefault(g => g.name == NomeDoObjeto);
            if (go == null)
            {
                go = new GameObject(NomeDoObjeto);
                SceneManager.MoveGameObjectToScene(go, cena);
                Undo.RegisterCreatedObjectUndo(go, "O jogo no Lakewood");
            }
            Garantir<NetworkObject>(go);
            Garantir<MundoDeLakewood>(go);
            PorAsPartes(go);
            return go;
        }

        static T Garantir<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        // os sistemas de cada parte (a fauna fica no vigia dela, fora da cena; o corte das árvores e os painéis de sucata ficam no
        // MundoDeLakewood e no PaineisDoMundo, que ele mesmo põe)
        static void PorAsPartes(GameObject go)
        {
            // a parte 4: os carros abandonados (a beira das estradas do sorteado, pela fonte do Lakewood), com os kits do
            // capô, das rodas e do macaco e o PecasDosCarrosDoMundo (as peças, os pneus e o alarme pela rede)
            var objetos = MontagemDoMundo.PorOsObjetosDaBeira(go);
            objetos.materialDaCidade = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDosObjetos);
            EditorUtility.SetDirty(objetos);
            MontagemDosVeiculos.PorOsCarrosNoMundo(go);
            // as partes 5 e 6: os grupos de sobreviventes (com as bases militares, que eles põem no Awake), as mochilas no
            // chão, os acampamentos dos comerciantes e os carros quebrados
            MontagemDosSobreviventes.PorNoMundo(go, AssetDatabase.LoadAssetAtPath<GameObject>(MontagemDosSobreviventes.Prefab));
            // a parte 7: o mapa do M (com o minimapa, que ele mesmo põe); as marcas ficam no MundoDeLakewood
            Garantir<UI.MapaDoMundo>(go);
            // a parte 8: o save (o Salvamento, com o MundoDeLakewood)
            Garantir<Salvar.Salvamento>(go);
        }
    }
}
