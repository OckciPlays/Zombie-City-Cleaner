using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using ZombieCityCleaner.Atmosphere;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Monta o clima da Parte 4 na cena aberta: o ciclo do dia (em rede), o ambiente que troca dia e noite, os dois
    /// perfis de pós-processamento, a neblina de altura no renderizador, as partículas no ar e o som ambiente.
    /// Menu: Zombie City Cleaner > Montar clima e ciclo do dia. Pode rodar de novo.
    /// </summary>
    public static class MontagemDoClima
    {
        const string Configuracoes = "Assets/_Game/Settings";
        const string RendererPrincipal = "Assets/Settings/PC_Renderer.asset";
        const string RendererBracos = "Assets/Settings/PC_Renderer_Bracos.asset";
        const string MaterialNeblina = "Assets/_Game/Art/Materials/NeblinaDeAltura.mat";

        [MenuItem("Zombie City Cleaner/Montar clima e ciclo do dia")]
        public static void Montar()
        {
            var perfilNoite = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Configuracoes}/Demo_Volume.asset");
            var perfilDia = PerfilDoDia();
            NeblinaNoRenderizador();
            RenderizadorDosBracos();

            var cena = EditorSceneManager.GetActiveScene();
            foreach (var g in cena.GetRootGameObjects())
                if (g.name == "Clima" || g.name == "Pós-processamento (dia)") Object.DestroyImmediate(g);
            var raizes = cena.GetRootGameObjects();

            var noite = raizes.First(g => g.name.StartsWith("Pós-processamento")).GetComponent<Volume>();
            noite.gameObject.name = "Pós-processamento (noite)";
            noite.sharedProfile = perfilNoite;
            var dia = new GameObject("Pós-processamento (dia)").AddComponent<Volume>();
            dia.isGlobal = true; dia.sharedProfile = perfilDia; dia.weight = 0f;

            var luz = raizes.FirstOrDefault(g => g.name == "Luar" || g.name == "Sol e lua");
            luz.name = "Sol e lua";

            var clima = new GameObject("Clima");
            clima.AddComponent<NetworkObject>();
            clima.AddComponent<CicloDoDia>();
            var ambiente = clima.AddComponent<AmbienteDoDia>();
            ambiente.sol = luz.GetComponent<Light>(); ambiente.volumeDia = dia; ambiente.volumeNoite = noite;
            var som = clima.AddComponent<SomAmbiente>();
            som.gemidos = AudioDaVoz();
            var particulas = new GameObject("Partículas no ar");
            particulas.transform.SetParent(clima.transform, false);
            particulas.AddComponent<ParticleSystem>();
            particulas.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Particulas.mat");
            particulas.AddComponent<ParticulasNoAr>();

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Debug.Log("Clima e ciclo do dia montados.");
        }

        static VolumeProfile PerfilDoDia()
        {
            string caminho = $"{Configuracoes}/Demo_Volume_Dia.asset";
            var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(caminho);
            if (perfil == null) { perfil = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(perfil, caminho); }
            T Pegar<T>() where T : VolumeComponent
            {
                if (!perfil.TryGet<T>(out var c)) { c = perfil.Add<T>(true); AssetDatabase.AddObjectToAsset(c, perfil); }
                return c;
            }
            // dia nublado do Silent Hill: quase sem cor, contraste baixo, granulado e vinheta leves
            Pegar<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var cor = Pegar<ColorAdjustments>();
            cor.postExposure.Override(0.35f); cor.contrast.Override(8f); cor.saturation.Override(-48f);
            cor.colorFilter.Override(new Color(0.93f, 0.92f, 0.88f));
            var vinheta = Pegar<Vignette>(); vinheta.intensity.Override(0.3f); vinheta.smoothness.Override(0.5f);
            var grao = Pegar<FilmGrain>(); grao.type.Override(FilmGrainLookup.Medium3); grao.intensity.Override(0.28f); grao.response.Override(0.75f);
            var bloom = Pegar<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.7f);
            EditorUtility.SetDirty(perfil);
            return perfil;
        }

        static void NeblinaNoRenderizador()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialNeblina);
            if (material == null)
            {
                material = new Material(Shader.Find("ZombieCityCleaner/NeblinaDeAltura"));
                AssetDatabase.CreateAsset(material, MaterialNeblina);
            }
            var dados = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPrincipal);
            var recurso = dados.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "NeblinaDeAltura");
            if (recurso == null)
            {
                recurso = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                recurso.name = "NeblinaDeAltura";
                AssetDatabase.AddObjectToAsset(recurso, dados);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(recurso, out string _, out long id);
                var so = new SerializedObject(dados);
                var lista = so.FindProperty("m_RendererFeatures");
                var mapa = so.FindProperty("m_RendererFeatureMap");
                lista.arraySize++; lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = recurso;
                mapa.arraySize++; mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // antes dos transparentes: os cones de luz e as partículas ficam por cima da neblina, e não embaixo dela
            recurso.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            recurso.fetchColorBuffer = true;
            recurso.requirements = ScriptableRenderPassInput.Depth;
            recurso.passMaterial = material;
            recurso.SetActive(true);
            EditorUtility.SetDirty(recurso);
            EditorUtility.SetDirty(dados);
        }

        // A câmera dos braços (sobreposição) usa um renderizador sem recursos extras: sem ele, a neblina de altura seria
        // aplicada de novo por cima da imagem, com a profundidade só dos braços.
        static void RenderizadorDosBracos()
        {
            var bracos = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererBracos);
            if (bracos == null)
            {
                bracos = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(bracos, RendererBracos);
            }
            var principal = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPrincipal);
            bracos.renderingMode = principal.renderingMode;
            bracos.postProcessData = principal.postProcessData;
            EditorUtility.SetDirty(bracos);
            // vai como renderizador 1 em todos os assets de URP (cada nível de qualidade usa um)
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(urp);
                var lista = so.FindProperty("m_RendererDataList");
                bool tem = false;
                for (int i = 0; i < lista.arraySize; i++) tem |= lista.GetArrayElementAtIndex(i).objectReferenceValue == bracos;
                if (tem) continue;
                lista.arraySize++;
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = bracos;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static AudioClip AudioDaVoz()
        {
            const string caminho = "Assets/_Game/Audio/SomZombie1.mp3";
            var importador = AssetImporter.GetAtPath(caminho) as AudioImporter;
            if (importador != null)
            {
                importador.forceToMono = true;
                var s = importador.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;   // para tocar trechos sem atraso
                importador.defaultSampleSettings = s;
                importador.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(caminho);
        }
    }
}
