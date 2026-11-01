using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os zumbis especiais (01/11/2026, a pedido): o Brutamonte, o Long e o Corredor, cada um com as 3 roupas. Os modelos vêm
    /// do Blender/Especiais/especiais.py (o esqueleto do Oco encaixado em cada um, os pesos, o olho e as pálpebras do
    /// Brutamonte e o papo do Long) e entram na montagem como os Ocos (Humanoid → os clipes gravados no esqueleto dele, a
    /// cabeça separada, o corpo que cai), depois dos 12 Ocos na lista do ZombieManager. A montagem marca o tipo
    /// (<see cref="Zumbi.tipo"/>), liga os ossos do olho e do papo e põe as hitboxes deles (as regiões Olho e Papo).
    /// <para>
    /// Menu Zombie City Cleaner > Zumbis > Montar os zumbis especiais: só os especiais (os Ocos ficam como estão), e a
    /// lista do ZombieManager da cena Mundo. A montagem inteira (Montar zumbis) também os faz.
    /// </para>
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string PastaEspeciais = Arte + "/Especiais";

        static readonly (string nome, TipoDeZumbi tipo)[] Especiais =
        {
            ("Brutamonte_V1", TipoDeZumbi.Brutamonte), ("Brutamonte_V2", TipoDeZumbi.Brutamonte), ("Brutamonte_V3", TipoDeZumbi.Brutamonte),
            ("Long_V1", TipoDeZumbi.Long), ("Long_V2", TipoDeZumbi.Long), ("Long_V3", TipoDeZumbi.Long),
            ("Corredor_V1", TipoDeZumbi.Corredor), ("Corredor_V2", TipoDeZumbi.Corredor), ("Corredor_V3", TipoDeZumbi.Corredor),
        };

        static bool EhEspecial(string nome) => Especiais.Any(e => e.nome == nome);
        static TipoDeZumbi TipoDe(string nome) => Especiais.FirstOrDefault(e => e.nome == nome).tipo;

        /// <summary>Todos os modelos, na ordem da lista do ZombieManager: os Ocos e, depois, os especiais.</summary>
        static IEnumerable<string> TodosOsModelos => Modelos.Concat(Especiais.Select(e => e.nome));

        [MenuItem("Zombie City Cleaner/Zumbis/Montar os zumbis especiais")]
        public static void MontarOsEspeciais()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar os zumbis especiais.");
                return;
            }
            CamadasDosZumbis();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
            if (ctrl == null) throw new FileNotFoundException("Falta o controlador dos zumbis (monte os zumbis uma vez)", Controlador);
            var primeiro = new Modelo { indice = 0, nome = Modelos[0], fbx = FbxDe(Modelos[0]), clipes = ClipesGravados(Modelos[0]) };
            int i = Modelos.Length;
            foreach (var (nome, _) in Especiais)
            {
                var m = ImportarModelo(nome, i++);
                var aoc = Sobrepor(ctrl, primeiro, m);
                MontarPrefab(m, aoc);
                MontarCorpo(m);
                Debug.Log($"Especial montado: {nome} ({m.altura:F2} m)");
            }
            AssetDatabase.SaveAssets();
            // a lista da cena Mundo: os Ocos e os especiais
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != MontagemDoMundo.Cena) cena = EditorSceneManager.OpenScene(MontagemDoMundo.Cena);
            var zm = Object.FindAnyObjectByType<ZombieManager>(FindObjectsInactive.Include);
            if (zm == null) throw new System.Exception("A cena Mundo não tem o ZombieManager (Pôr os zumbis no mundo).");
            zm.modelos = TodosOsModelos.Select(n => Carregar<Zumbi>($"{Prefabs}/Zumbi_{n}.prefab")).ToArray();
            zm.corpos = TodosOsModelos.Select(n => Carregar<CorpoDeZumbi>($"{Prefabs}/Corpo_{n}.prefab")).ToArray();
            EditorUtility.SetDirty(zm);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"Zumbis especiais montados: {Especiais.Length}. A cena Mundo tem {zm.modelos.Length} modelos.");
        }

        /// <summary>Os clipes já gravados de um modelo (Oco/Clipes/&lt;modelo&gt;/*.anim), pelo nome.</summary>
        static Dictionary<string, AnimationClip> ClipesGravados(string nome)
        {
            var pasta = $"{ClipesDosOcos}/{nome}";
            return AssetDatabase.FindAssets("t:AnimationClip", new[] { pasta })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(c => c != null)
                .ToDictionary(c => c.name);
        }

        /// <summary>No prefab do especial: o tipo, os ossos do olho e do papo, e as hitboxes deles; o Brutamonte é mais largo.</summary>
        static void MontarEspecial(Zumbi z, Modelo m, SkinnedMeshRenderer smr, List<Hitbox> hitboxes)
        {
            if (!EhEspecial(m.nome)) return;
            z.tipo = TipoDe(m.nome);
            Transform Osso(string nome) => smr.bones.FirstOrDefault(t => t.name == nome) ?? smr.rootBone?.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nome);
            if (z.tipo == TipoDeZumbi.Brutamonte)
            {
                z.olho = Osso("olho");
                z.palpebraSup = Osso("palpebra_sup");
                z.palpebraInf = Osso("palpebra_inf");
                if (z.olho != null)
                {
                    var h = NovaHitbox(z, z.olho, "Olho", RegiaoDoCorpo.Olho, out var go);
                    var esfera = go.AddComponent<SphereCollider>();
                    esfera.radius = 0.12f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
                    h.colisor = esfera;
                    hitboxes.Add(h);
                    z.hitboxDoOlho = h;
                }
                z.capsula.radius = 0.55f;
                z.agente.radius = 0.55f;
            }
            else if (z.tipo == TipoDeZumbi.Long)
            {
                z.papo = Osso("papo");
                if (z.papo != null)
                {
                    var h = NovaHitbox(z, z.papo, "Papo", RegiaoDoCorpo.Papo, out var go);
                    var esfera = go.AddComponent<SphereCollider>();
                    esfera.radius = 0.13f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
                    h.colisor = esfera;
                    hitboxes.Add(h);
                    z.hitboxDoPapo = h;
                }
            }
        }
    }
}
