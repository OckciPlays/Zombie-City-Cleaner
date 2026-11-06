using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A montagem da tela dos moradores do bunker (o Plano_NPCs_Bunker.md da pasta de arte, a parte 3): o
    /// <c>Settings/Resources/RecursosDosMoradores.asset</c>, com o NPC vestível das roupas, que a <see cref="TelaDosMoradores"/>
    /// carrega antes de haver partida. Menu: Zombie City Cleaner > Moradores > Montar a tela.
    /// </summary>
    public static class MontagemDosMoradores
    {
        public const string Pasta = "Assets/_Game/Settings/Resources";
        public const string Recursos = Pasta + "/" + RecursosDosMoradores.NoResources + ".asset";
        const string Npc = "Assets/_Game/Prefabs/Roupas/NPC.prefab";

        [MenuItem("Zombie City Cleaner/Moradores/Montar a tela")]
        public static void Montar()
        {
            Directory.CreateDirectory(Pasta);
            var r = AssetDatabase.LoadAssetAtPath<RecursosDosMoradores>(Recursos);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<RecursosDosMoradores>();
                AssetDatabase.CreateAsset(r, Recursos);
            }
            r.npc = AssetDatabase.LoadAssetAtPath<GameObject>(Npc);
            EditorUtility.SetDirty(r);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Moradores] {Recursos}: o NPC {(r.npc != null ? r.npc.name : "não achado")}.");
        }
    }
}
