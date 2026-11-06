using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O fumo na montagem do jogador (03/11/2026, a parte 4 do Plano_Cigarro.md da pasta de arte): a bituca (a malha do
    /// Loot.fbx, Fumo_Bituca), o material da fumaça (as bolinhas: as partículas transparentes, com a luz) e o das peças
    /// acesas (a brasa e a chama: a paleta sem a luz, sempre clara), e as peças nos FBX (o corpo e os braços do
    /// bloco_cigarro.py da pasta de arte: os vazios Fumo_&lt;Peça&gt; nos ossos do fumo, com as malhas Fumo_&lt;Peça&gt;_&lt;Parte&gt;
    /// dentro), com os materiais delas.
    /// </summary>
    public static class MontagemDoFumo
    {
        const string Pasta = "Assets/_Game/Art/Fumo";
        const string Malhas = "Assets/_Game/Art/Characters/Jogador/Malhas";
        public const string MalhaDaBituca = Malhas + "/Fumo_Bituca.asset";
        public const string MaterialDaFumaca = Pasta + "/M_Fumaca.mat";
        public const string MaterialAceso = Pasta + "/M_Fumo_Aceso.mat";
        /// <summary>As peças, na ordem dos vazios (o <see cref="CorpoDoPersonagem.fumo"/>: o FumoJogador usa o cigarro, a brasa e a chama).</summary>
        public static readonly string[] Pecas = { "Maco", "Tampa", "Cigarro", "Brasa", "Isqueiro", "Chama" };

        /// <summary>A bituca do Loot.fbx (Blender/loot.py, a seção "o fumo") numa malha (a JuntarLoot chama).</summary>
        public static void JuntarBituca()
        {
            if (!File.Exists(MontagemDasArmas.FbxLoot)) return;
            bool tem = AssetDatabase.LoadAllAssetsAtPath(MontagemDasArmas.FbxLoot).OfType<Mesh>().Any(m => m.name.StartsWith("Bituca_"));
            if (tem) MontagemDasArmas.Juntar(MontagemDasArmas.FbxLoot, "Bituca_", MalhaDaBituca);
            else Debug.LogWarning("A Bituca não está no Loot.fbx (rode o Blender/loot.py).");
        }

        /// <summary>As bolinhas da fumaça: as partículas com a luz (o Simple Lit), transparentes, sem textura (a cor vem de cada uma).</summary>
        public static Material Fumaca()
        {
            Directory.CreateDirectory(Pasta);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaFumaca);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(m, MaterialDaFumaca);
            }
            m.SetFloat("_Surface", 1f);                       // transparente
            m.SetFloat("_Blend", 0f);                         // alpha
            m.SetColor("_BaseColor", Color.white);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A brasa e a chama: a paleta sem a luz (o Unlit), um pouco mais clara, para parecerem acesas no escuro.</summary>
        public static Material Aceso()
        {
            Directory.CreateDirectory(Pasta);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialAceso);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, MaterialAceso);
            }
            var paleta = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var textura = paleta != null ? (paleta.HasProperty("_BaseMap") ? paleta.GetTexture("_BaseMap") : paleta.mainTexture) : null;
            if (textura != null) m.SetTexture("_BaseMap", textura);
            m.SetColor("_BaseColor", new Color(1.25f, 1.2f, 1.15f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Os vazios das peças (prefixo "Fumo_" no corpo, "Fumo_FP_" nos braços), na ordem de <see cref="Pecas"/>.</summary>
        public static Transform[] Vazios(Transform raiz, string prefixo)
        {
            var todos = raiz.GetComponentsInChildren<Transform>(true);
            return Pecas.Select(p => todos.FirstOrDefault(t => t.name == prefixo + p)).ToArray();
        }

        /// <summary>
        /// As malhas das peças dentro dos vazios, com os materiais (a paleta; a brasa e a chama acesas), sem a sombra delas
        /// (são pequenas, e o cigarro na frente do rosto riscava a sombra da lanterna). Devolve os renderers.
        /// </summary>
        public static Renderer[] NasPecas(Transform raiz, string prefixo)
        {
            var paleta = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var aceso = Aceso();
            var lista = new System.Collections.Generic.List<Renderer>();
            foreach (var v in Vazios(raiz, prefixo))
            {
                if (v == null)
                {
                    Debug.LogWarning($"O fumo: falta um vazio {prefixo}* em {raiz.name} (exporte de novo o bloco_cigarro.py).");
                    continue;
                }
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = r.name.EndsWith("_Brasa") || r.name.EndsWith("_Chama") ? aceso : paleta;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    lista.Add(r);
                }
            }
            return lista.ToArray();
        }

        /// <summary>O FumoJogador do prefab do jogador: a fumaça e a bituca.</summary>
        public static void NoJogador(FumoJogador fumo)
        {
            fumo.materialDaFumaca = Fumaca();
            fumo.malhaDaBituca = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDaBituca);
            fumo.materialDaBituca = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            if (fumo.malhaDaBituca == null) Debug.LogWarning("O fumo: falta a malha da bituca (rode a montagem das armas, com o Loot.fbx).");
        }
    }
}
