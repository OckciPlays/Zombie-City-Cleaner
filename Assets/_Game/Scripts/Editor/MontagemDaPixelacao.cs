using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O passe das cores do PS1 da pixelação (Configurações > Gráficos > Pixelação; a pedido em 07/11/2026): um
    /// FullScreenPassRendererFeature "Pixelacao" no PC_Renderer, antes do pós-processamento, com o material do shader
    /// ZombieCityCleaner/Pixelacao, inativo no asset (o UI.Graficos liga nas duas opções do PS1). A pixelação em si (o 3D em
    /// poucas linhas, ampliado sem suavizar) é o renderScale e o filtro Point do asset do URP, também pelo UI.Graficos.
    /// Menu Zombie City Cleaner > Gráficos > Pôr a pixelação no renderizador (pode rodar de novo).
    /// </summary>
    public static class MontagemDaPixelacao
    {
        const string RendererPrincipal = "Assets/Settings/PC_Renderer.asset";
        const string MaterialDaPixelacao = "Assets/_Game/Art/Materials/Pixelacao.mat";
        public const string NomeDoRecurso = "Pixelacao";

        [MenuItem("Zombie City Cleaner/Gráficos/Pôr a pixelação no renderizador")]
        public static void Montar()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDaPixelacao);
            if (material == null)
            {
                material = new Material(Shader.Find("ZombieCityCleaner/Pixelacao"));
                AssetDatabase.CreateAsset(material, MaterialDaPixelacao);
            }
            var dados = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPrincipal);
            var recurso = dados.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == NomeDoRecurso);
            if (recurso == null)
            {
                recurso = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                recurso.name = NomeDoRecurso;
                AssetDatabase.AddObjectToAsset(recurso, dados);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(recurso, out string _, out long id);
                var so = new SerializedObject(dados);
                var lista = so.FindProperty("m_RendererFeatures");
                var mapa = so.FindProperty("m_RendererFeatureMap");
                lista.arraySize++; lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = recurso;
                mapa.arraySize++; mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // antes do pós-processamento (depois da neblina, que vem antes na lista): com um passe depois dele, o URP ampliava
            // a imagem pequena suavizando (visto em 07/11/2026), e a pixelação virava borrão
            recurso.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            recurso.fetchColorBuffer = true;
            recurso.requirements = ScriptableRenderPassInput.None;
            recurso.passMaterial = material;
            recurso.SetActive(false);
            EditorUtility.SetDirty(recurso);
            EditorUtility.SetDirty(dados);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Pixelação] O passe das cores do PS1 no {RendererPrincipal} (inativo; o UI.Graficos liga).");
        }
    }
}
