using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As cidades do Lakewood na cena (Plano_Lakewood, Etapa 6, as partes 3 e 5): os materiais das casas, da hera e das
    /// pichações no <see cref="LakewoodNaCena"/>, e o objeto "Obras das cidades" na raiz da cena, com o NetworkObject e os
    /// sistemas do mundo sorteado que as obras usam: os móveis antigos (<see cref="MobiliaDoMundo"/>, os modelos do
    /// Cidade_Mobilia.fbx), a mobília nova com o saque (<see cref="MobiliaNovaDoMundo"/>) e as portas que abrem
    /// (<see cref="PortasDoMundo"/>), o Hospital Principal (<see cref="HospitalDoMundo"/>, a parte 4), o NavMesh em volta
    /// dos jogadores (<see cref="NavMeshDoMundo"/>) e os zumbis (o "Zumbis", a parte 5). O 3. Montar a cena também põe;
    /// este menu acerta a cena aberta e salva.
    /// </summary>
    public static class CidadesNaCenaDeLakewood
    {
        public const string NomeDoObjeto = "Obras das cidades";

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: as cidades na cena", false, 8)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de pôr as cidades na cena.");
                return;
            }
            var cena = EditorSceneManager.GetActiveScene();
            var lakewood = Object.FindAnyObjectByType<LakewoodNaCena>();
            if (cena.path != MontagemDeLakewood.Cena || lakewood == null)
            {
                Debug.LogWarning($"Abra a cena do Lakewood ({MontagemDeLakewood.Cena}) antes.");
                return;
            }
            PorNaCena(cena, lakewood);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("[Lakewood] As cidades na cena: os materiais das obras, a mobília, a mobília nova e as portas (o objeto \"" + NomeDoObjeto + "\"). A cena foi salva.");
        }

        public static void PorNaCena(UnityEngine.SceneManagement.Scene cena, LakewoodNaCena lakewood)
        {
            Undo.RecordObject(lakewood, "As cidades na cena");
            lakewood.materialDasCasas = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasCasas);
            lakewood.materialDaHera = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDaVegetacao);
            lakewood.materialDasPichacoes = AssetDatabase.LoadAssetAtPath<Material>(TexturasDoAbandono.MaterialDasPichacoes);
            lakewood.materialDasMarcas = TexturasDoAbandono.Marcas();
            EditorUtility.SetDirty(lakewood);

            var obras = cena.GetRootGameObjects().FirstOrDefault(g => g.name == NomeDoObjeto);
            if (obras == null)
            {
                obras = new GameObject(NomeDoObjeto);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obras, cena);
                Undo.RegisterCreatedObjectUndo(obras, "As cidades na cena");
            }
            if (obras.GetComponent<NetworkObject>() == null) obras.AddComponent<NetworkObject>();
            // os móveis antigos (as lojas e o posto), sem o Dia 0 do PorAMobilia do mundo sorteado
            var fbx = MontagemDoMundo.FbxDaMobilia;
            if (AssetImporter.GetAtPath(fbx) is ModelImporter imp && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
            var antiga = obras.GetComponent<MobiliaDoMundo>();
            if (antiga == null) antiga = obras.AddComponent<MobiliaDoMundo>();
            antiga.malhas = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>()
                .Where(m => ZombieCityCleaner.Mundo.Mobilia.Feitos.ContainsKey(m.name)).OrderBy(m => m.name).ToArray();
            EditorUtility.SetDirty(antiga);
            // o Hospital Principal (o do mundo sorteado, fixado no lote dele pelo LakewoodNaCena), com os sons e os papéis;
            // ele também põe a mobília nova
            MontagemDoMundo.PorOHospital(obras);
            MontagemDoMundo.PorAsPortas(obras);
            // os zumbis (a parte 5): o NavMesh em volta dos jogadores, no anfitrião, e o "Zumbis" do mundo sorteado (o
            // ZombieManager com os modelos, sem hordas: o povoamento pelas células, no chão do Lakewood)
            if (obras.GetComponent<NavMeshDoMundo>() == null) obras.AddComponent<NavMeshDoMundo>();
            if (cena.GetRootGameObjects().Any(g => g.name == "Efeitos")) MontagemDosZumbis.PorOsZumbisNaCena(cena);
            else Debug.LogWarning("[Lakewood] A cena não tem o \"Efeitos\" (os tiros e o sangue): os zumbis ficaram de fora.");
            EditorSceneManager.MarkSceneDirty(cena);
        }
    }
}
