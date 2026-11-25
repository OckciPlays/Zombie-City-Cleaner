using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O Dia 0 na Woodcreek (22/11/2026, a etapa 4 do Docs/23-Campanha-e-Mapa.md), menu Zombie City Cleaner > Lakewood > Etapa
    /// 7: o Dia 0 na Woodcreek:
    ///  - refaz o prefab do palco (<see cref="Palco"/>): a <see cref="WoodcreekDoDiaZero"/> com o material da cidade, os
    ///    objetos de rua, a mobília antiga (a introdução usa a cama e a maca), o material das letras e as roupas como itens (a
    ///    loja de roupas), os <see cref="NpcsDoDiaZero"/> (sem os carros da rua reta: os da vila são os
    ///    <see cref="CarrosDaWoodcreek"/>), a <see cref="IntroDoDiaZero"/> (a trilha e a tela da TV) e os carros da vila;
    ///  - na cena do Lakewood (aberta, ou aberta ao lado): o <see cref="DiaZeroDaPartida"/> no "Mundo (Lakewood)" (o objeto
    ///    da rede, com a <see cref="MundoDeLakewood"/>), e o palco e a caminhonete no <see cref="LakewoodNaCena"/>; salva a
    ///    cena.
    /// As mesmas referências da cena do Dia 0 do mundo sorteado (MontagemDoDiaZero).
    /// </summary>
    public static class DiaZeroNaWoodcreek
    {
        public const string Palco = "Assets/_Game/Prefabs/DiaZero/PalcoDoDiaZeroNaWoodcreek.prefab";
        const string CenaDoLakewood = "Assets/_Game/Scenes/Lakewood.unity";
        const string FbxRua = "Assets/_Game/Art/Cidade/Cidade_Rua.fbx";
        const string FbxMobilia = "Assets/_Game/Art/Cidade/Cidade_Mobilia.fbx";
        const string Caminhonete = "Assets/_Game/Prefabs/Veiculos/Caminhonete.prefab";

        static Mesh Malha(string fbx, string nome) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name == nome);

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 7: o Dia 0 na Woodcreek (o palco e a caminhonete)", false, 33)]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar o Dia 0 na Woodcreek.");
                return;
            }
            var palco = MontarOPalco();
            var cena = SceneManager.GetSceneByPath(CenaDoLakewood);
            bool abriu = false;
            if (!cena.isLoaded)
            {
                cena = EditorSceneManager.OpenScene(CenaDoLakewood, OpenSceneMode.Additive);
                abriu = true;
            }
            var raizes = cena.GetRootGameObjects();
            var lw = raizes.SelectMany(g => g.GetComponentsInChildren<LakewoodNaCena>(true)).FirstOrDefault();
            var mundo = raizes.SelectMany(g => g.GetComponentsInChildren<MundoDeLakewood>(true)).FirstOrDefault();
            if (lw == null || mundo == null)
            {
                Debug.LogWarning("[Dia 0] A cena do Lakewood não tem o LakewoodNaCena ou a MundoDeLakewood (Lakewood > O jogo do mundo sorteado na cena).");
                return;
            }
            Undo.RecordObject(lw, "O Dia 0 na Woodcreek");
            lw.palcoDoDiaZero = palco;
            lw.caminhonete = AssetDatabase.LoadAssetAtPath<GameObject>(Caminhonete);
            EditorUtility.SetDirty(lw);
            if (mundo.GetComponent<DiaZeroDaPartida>() == null) mundo.gameObject.AddComponent<DiaZeroDaPartida>();
            EditorUtility.SetDirty(mundo.gameObject);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            if (abriu) EditorSceneManager.CloseScene(cena, true);
            Debug.Log($"[Dia 0] O Dia 0 na Woodcreek: o palco em {Palco}, a caminhonete ({(lw.caminhonete != null ? "ok" : "faltou")}) e o DiaZeroDaPartida no {mundo.name}.");
        }

        // o prefab do palco (as referências da cena do Dia 0 do mundo sorteado)
        static GameObject MontarOPalco()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Palco));
            var go = new GameObject("PalcoDoDiaZeroNaWoodcreek");
            var w = go.AddComponent<WoodcreekDoDiaZero>();
            w.enabled = false;   // (fora do Play, ela não monta nada; desligada só para o prefab sair sem a raiz)
            w.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Cidade.mat");
            w.poste = Malha(FbxRua, "Poste");
            w.carros = new[] { "Carro_Sedan_Azul", "Carro_Sedan_Vinho", "Carro_Pickup_Bege", "Carro_Sedan_Cinza" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
            w.arvores = new[] { "Pinheiro_A", "Pinheiro_B", "Pinheiro_C", "Arvore" }.Select(n => Malha(FbxRua, n)).Where(m => m != null).ToArray();
            w.barreira = Malha(FbxRua, "Barreira");
            w.cavalete = Malha(FbxRua, "Cavalete");
            w.cone = Malha(FbxRua, "Cone");
            w.hidrante = Malha(FbxRua, "Hidrante");
            w.lixeira = Malha(FbxRua, "Lixeira");
            w.mobilia = AssetDatabase.LoadAllAssetsAtPath(FbxMobilia).OfType<Mesh>().ToArray();
            w.materialDasLetras = AssetDatabase.LoadAssetAtPath<Material>(MontagemDoMundo.MaterialDoLetreiro);
            w.roupas = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Roupas/Itens" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToArray();
            var npcs = go.AddComponent<NpcsDoDiaZero>();
            npcs.npc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Roupas/NPC.prefab");
            npcs.particulas = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Particulas.mat");
            npcs.carros = 0;   // (os carros da vila são os CarrosDaWoodcreek)
            var intro = go.AddComponent<IntroDoDiaZero>();
            intro.trilha = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/DiaZero/IntroDoDiaZero.ogg");
            intro.telaDaTv = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/TelaDaTV.mat");
            intro.pandemia = true;   // (o jornal da pandemia: o anúncio do governo, sem os ataques)
            go.AddComponent<CarrosDaWoodcreek>();
            w.enabled = true;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Palco);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }
    }
}
