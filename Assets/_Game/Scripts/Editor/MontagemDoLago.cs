using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A montagem do que é do lago e da oficina (06/11/2026, a árvore de habilidades): os prefabs no
    /// <c>Assets/_Game/Resources</c> (o jogo os carrega pelo nome, sem mexer na cena):
    ///  - o caiaque (<c>Caiaque.prefab</c>): o modelo do Blender/caiaque.py, os bancos (Assento_0 atrás, Assento_1 na frente),
    ///    o remo, a caixa de colisão, o NetworkObject com o NetworkTransform do dono (como o carro), o <see cref="Caiaque"/> e
    ///    o <see cref="PortaDoCaiaque"/>; e a entrada na lista dos prefabs da rede;
    ///  - a mesa de oficina (<c>MesaDeOficina.prefab</c>): o modelo do Blender/oficina.py com a origem nas costas (o bunker
    ///    encosta ela na parede), a caixa e a <see cref="Bancada"/> com a oficina;
    ///  - a boia da pesca (<c>Boia.prefab</c>).
    /// </summary>
    public static class MontagemDoLago
    {
        const string Pasta = "Assets/_Game/Resources";
        const string FbxDoCaiaque = "Assets/_Game/Art/Veiculos/Caiaque.fbx", FbxDoRemo = "Assets/_Game/Art/Veiculos/Remo.fbx";
        const string FbxDaMesa = "Assets/_Game/Art/Mundo/MesaDeOficina.fbx", FbxDaBoia = "Assets/_Game/Art/Pesca/Boia.fbx";
        // o material "Paleta" dos FBX novos (a Paleta.png por célula) vira o da cidade (o mesmo dos carros)
        const string MaterialCidade = "Assets/_Game/Art/Materials/Cidade.mat";

        [MenuItem("Zombie City Cleaner/Veículos/Montar o caiaque, a mesa de oficina e a boia")]
        public static void MontarTudo()
        {
            if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            foreach (var fbx in new[] { FbxDoCaiaque, FbxDoRemo, FbxDaMesa, FbxDaBoia }) Remapear(fbx);
            MontarOCaiaque();
            MontarAMesa();
            MontarABoia();
            AssetDatabase.SaveAssets();
        }

        static void Remapear(string caminho)
        {
            var imp = AssetImporter.GetAtPath(caminho) as ModelImporter;
            var cidade = AssetDatabase.LoadAssetAtPath<Material>(MaterialCidade);
            if (imp == null || cidade == null) return;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Paleta"), cidade);
            imp.SaveAndReimport();
        }

        static Transform Achar(Transform raiz, string nome) =>
            raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nome || t.name.StartsWith(nome + ".") || t.name.StartsWith(nome + "_"));

        // a caixa dos desenhos (no espaço da raiz), sem os filhos que não contam
        static Bounds CaixaDe(Transform raiz, Transform fora = null)
        {
            var b = new Bounds();
            bool tem = false;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                if (fora != null && r.transform.IsChildOf(fora)) continue;
                var mb = r.bounds;
                foreach (var canto in new[] { mb.min, mb.max, new Vector3(mb.min.x, mb.min.y, mb.max.z), new Vector3(mb.max.x, mb.max.y, mb.min.z) })
                {
                    var p = raiz.InverseTransformPoint(canto);
                    if (!tem) { b = new Bounds(p, Vector3.zero); tem = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static void MontarOCaiaque()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDoCaiaque);
            if (fbx == null)
            {
                Debug.LogError($"[Caiaque] Sem o {FbxDoCaiaque} (o Blender/caiaque.py).");
                return;
            }
            var raiz = new GameObject("Caiaque");
            try
            {
                var modelo = Object.Instantiate(fbx, raiz.transform, false);
                modelo.name = "Modelo";
                var remo = Achar(modelo.transform, "Remo");
                if (remo == null)
                {
                    var fbxDoRemo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDoRemo);
                    if (fbxDoRemo != null) remo = Object.Instantiate(fbxDoRemo, raiz.transform, false).transform;
                }
                var caixa = CaixaDe(raiz.transform, remo);
                // os bancos, na raiz (o caiaque usa a posição local deles para o remo). No modelo do Blender/caiaque.py o
                // Assento_0 é o da frente e o Assento_1 o de trás; no jogo, o lugar 0 é o de trás (quem rema)
                var a0 = Achar(modelo.transform, "Assento_1");
                var a1 = Achar(modelo.transform, "Assento_0");
                Transform Banco(Transform t, string nome, float z)
                {
                    var b = new GameObject(nome).transform;
                    b.SetParent(raiz.transform, false);
                    if (t != null) b.SetPositionAndRotation(t.position, Quaternion.Euler(0f, raiz.transform.eulerAngles.y, 0f));
                    else b.localPosition = new Vector3(0f, caixa.min.y + 0.22f, z);
                    return b;
                }
                var banco0 = Banco(a0, "Banco_0", caixa.center.z - caixa.extents.z * 0.32f);
                var banco1 = Banco(a1, "Banco_1", caixa.center.z + caixa.extents.z * 0.32f);
                if (remo != null)
                {
                    remo.name = "Remo";
                    remo.SetParent(raiz.transform, true);
                    // sem ninguém remando: deitado no casco, entre os bancos
                    remo.localPosition = new Vector3(0f, caixa.max.y + 0.03f, caixa.center.z);
                    var col = remo.GetComponentsInChildren<Collider>(true);
                    foreach (var c in col) Object.DestroyImmediate(c);
                }
                foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var box = raiz.AddComponent<BoxCollider>();
                box.center = caixa.center;
                box.size = new Vector3(caixa.size.x, Mathf.Max(0.3f, caixa.size.y), caixa.size.z);
                var corpo = raiz.AddComponent<Rigidbody>();
                corpo.isKinematic = true;
                corpo.useGravity = false;
                var no = raiz.AddComponent<NetworkObject>();
                no.DontDestroyWithOwner = true;   // quem remava saiu da partida: o caiaque fica (volta para o anfitrião)
                var nt = raiz.AddComponent<NetworkTransform>();
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                nt.UseQuaternionSynchronization = true;
                nt.Interpolate = true;
                var caiaque = raiz.AddComponent<Caiaque>();
                caiaque.assentos = new[] { banco0, banco1 };
                caiaque.remo = remo;
                caiaque.meioComprimento = Mathf.Max(1f, caixa.extents.z - 0.1f);
                raiz.AddComponent<PortaDoCaiaque>().caiaque = caiaque;
                var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, $"{Pasta}/Caiaque.prefab");
                var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
                if (lista != null && !lista.PrefabList.Any(p => p.Prefab == prefab))
                {
                    lista.Add(new NetworkPrefab { Prefab = prefab });
                    EditorUtility.SetDirty(lista);
                }
                Debug.Log($"[Caiaque] O prefab: {caixa.size.z:0.00} m de comprimento, {caixa.size.x:0.00} m de largura; os bancos em {banco0.localPosition} e {banco1.localPosition}; remo: {(remo != null ? "sim" : "não")}.");
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        static void MontarAMesa()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDaMesa);
            if (fbx == null)
            {
                Debug.LogError($"[Oficina] Sem o {FbxDaMesa} (o Blender/oficina.py).");
                return;
            }
            var raiz = new GameObject("MesaDeOficina");
            try
            {
                var modelo = Object.Instantiate(fbx, raiz.transform, false);
                modelo.name = "Modelo";
                foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                // a origem nas costas (o -z), no chão, no meio: o bunker encosta a mesa na parede pelas costas
                var caixa = CaixaDe(raiz.transform);
                modelo.transform.localPosition = new Vector3(-caixa.center.x, -caixa.min.y, -caixa.min.z + 0.02f);
                caixa = CaixaDe(raiz.transform);
                var box = raiz.AddComponent<BoxCollider>();
                box.center = caixa.center;
                box.size = caixa.size;
                raiz.AddComponent<Bancada>().oficina = true;
                PrefabUtility.SaveAsPrefabAsset(raiz, $"{Pasta}/MesaDeOficina.prefab");
                Debug.Log($"[Oficina] O prefab da mesa: {caixa.size.x:0.00} × {caixa.size.z:0.00} m, {caixa.size.y:0.00} m de altura.");
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        static void MontarABoia()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDaBoia);
            if (fbx == null)
            {
                Debug.LogWarning($"[Pesca] Sem o {FbxDaBoia}: a boia fica uma bolinha vermelha.");
                return;
            }
            var raiz = new GameObject("Boia");
            try
            {
                var modelo = Object.Instantiate(fbx, raiz.transform, false);
                modelo.name = "Modelo";
                foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in modelo.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // (o dobro do tamanho de verdade: de longe, a boia de 3 cm sumia)
                modelo.transform.localScale = Vector3.one * 2f;
                PrefabUtility.SaveAsPrefabAsset(raiz, $"{Pasta}/Boia.prefab");
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }
    }
}
