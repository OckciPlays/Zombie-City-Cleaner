using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// O fogo (11/11/2026, a etapa 4 do ramo Fogo). Menu: Zombie City Cleaner > Habilidades > Montar o fogo. Pode rodar de
    /// novo à vontade; não mexe nas cenas nem no prefab do jogador:
    ///  - os braços de primeira pessoa <c>Art/Characters/Jogador/Jogador_FP_Lampiao.fbx</c> e <c>_Molotov.fbx</c> (a importação
    ///    dos outros braços, e os controladores: o soco direito no lugar do tiro, o balanço do lampião e o arremesso do
    ///    molotov; o esquerdo no gatilho sem bala);
    ///  - as malhas do <c>Fogo.fbx</c> (o lampião e o molotov no corpo, com os vazios Chama e Pavio, e a fogueira portátil
    ///    montada, com o vazio Fogo) e os 3 itens do <c>Loot_Etapa34.fbx</c> (as malhas <c>Malhas/Loot_&lt;modelo&gt;</c>, os
    ///    ícones e as fotos);
    ///  - o <c>Resources/ItensDoFogo.prefab</c> (o <see cref="ItensDoFogo"/>), que o jogo usa no Play, com a pegada de cada um
    ///    no hand.R do corpo (o <c>Fogo_MaoDoCorpo.json</c>: a do molotov é a da tocha).
    /// </summary>
    public static class MontagemDoFogo
    {
        const string Arte = "Assets/_Game/Art/Characters/Jogador";
        const string FbxFogo = Arte + "/Fogo.fbx";
        const string Prefab = "Assets/_Game/Resources/ItensDoFogo.prefab";
        static string FbxBracos(string nome) => $"{Arte}/Jogador_FP_{nome}.fbx";
        static string Controlador(string nome) => $"{Arte}/Jogador_FP_{nome}.overrideController";

        // a pegada no hand.R do corpo (o Fogo_MaoDoCorpo.json da etapa 4: a origem da malha onde a mão segura)
        static readonly Vector3 LugarDaPegada = new Vector3(-0.02853f, 0.07073f, -0.032f);
        static readonly Quaternion GiroDoMolotov = new Quaternion(-0.6861f, 0.17106f, -0.59966f, 0.37471f);
        static readonly Quaternion GiroDoLampiao = new Quaternion(-0.33192f, -0.23989f, -0.5938f, 0.69259f);

        [MenuItem("Zombie City Cleaner/Habilidades/Montar o fogo")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar o fogo.");
                return;
            }
            foreach (var f in new[] { FbxFogo, MontagemDaProtecao.FbxItens, FbxBracos("Lampiao"), FbxBracos("Molotov") })
                if (!File.Exists(f)) throw new FileNotFoundException("Falta um modelo do fogo.", f);
            MontagemDasArmas.ImportarFP(FbxBracos("Lampiao"));
            MontagemDasArmas.ImportarFP(FbxBracos("Molotov"));
            MontagemDasArmas.ImportarAvulsas(FbxFogo);
            MontagemDasArmas.ImportarAvulsas(MontagemDaProtecao.FbxItens);
            // o soco direito no lugar do tiro (o lampião balança; o molotov é arremessado), o esquerdo no gatilho sem bala
            foreach (var nome in new[] { "Lampiao", "Molotov" })
                MontagemDasArmas.MontarOverride(MontagemDasArmas.ControladorFP, MontagemDasArmas.FbxFP(0), FbxBracos(nome), MontagemDasArmas.ClipesFP,
                    c => c == "Fire" ? "SocoDireito" : c == "DryFire" ? "SocoEsquerdo" : c == "Reload" ? "Idle" : c, Controlador(nome));
            AssetDatabase.SaveAssets();

            var material = AssetDatabase.LoadAssetAtPath<Material>(MontagemDasArmas.MaterialArma);
            var go = new GameObject("ItensDoFogo");
            int qualidade = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);   // (as fotos com a qualidade Mobile, como as da proteção)
            try
            {
                var d = go.AddComponent<ItensDoFogo>();
                d.material = material;
                var relatorio = new List<string>();
                // o corpo e a fogueira (as malhas e os vazios do Fogo.fbx)
                var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxFogo).OfType<Mesh>().ToList();
                Mesh Malha(string nome)
                {
                    var m = malhas.FirstOrDefault(x => x.name == nome);
                    if (m == null) Debug.LogWarning($"[Fogo] Falta a {nome} no {FbxFogo}.");
                    return m;
                }
                var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(FbxFogo).transform;
                Vector3 Vazio(string pai, string nome, Vector3 senao)
                {
                    var p = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == pai);
                    var v = p != null ? p.Find(nome) : null;
                    if (v == null) Debug.LogWarning($"[Fogo] Falta o vazio {pai}/{nome} no {FbxFogo}.");
                    return v != null ? v.localPosition : senao;
                }
                d.lampiaoNoCorpo = Malha("Lampiao_corpo");
                d.molotovNoCorpo = Malha("Molotov_corpo");
                d.fogueira = Malha("Fogueira_Portatil_corpo");
                d.chamaDoLampiao = Vazio("Lampiao_corpo", "Chama", new Vector3(0f, -0.2f, 0f));
                d.pavioDoMolotov = Vazio("Molotov_corpo", "Pavio", new Vector3(0f, 0f, 0.09f));
                d.fogoDaFogueira = Vazio("Fogueira_Portatil_corpo", "Fogo", new Vector3(0f, 0.12f, 0f));
                d.lugarDoLampiao = d.lugarDoMolotov = LugarDaPegada;
                d.giroDoLampiao = GiroDoLampiao;
                d.giroDoMolotov = GiroDoMolotov;
                relatorio.Add($"Corpo: o lampião {d.lampiaoNoCorpo?.bounds.size.ToString("F3")} (a chama em {d.chamaDoLampiao.ToString("F3")}), o molotov {d.molotovNoCorpo?.bounds.size.ToString("F3")} (o pavio em {d.pavioDoMolotov.ToString("F3")}), a fogueira {d.fogueira?.bounds.size.ToString("F3")} (o fogo em {d.fogoDaFogueira.ToString("F3")})");
                // a primeira pessoa
                d.bracosDoLampiao = AssetDatabase.LoadAssetAtPath<GameObject>(FbxBracos("Lampiao"));
                d.bracosDoMolotov = AssetDatabase.LoadAssetAtPath<GameObject>(FbxBracos("Molotov"));
                d.controladorDoLampiao = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controlador("Lampiao"));
                d.controladorDoMolotov = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controlador("Molotov"));
                // a chama nos braços: no vidro do lampião (70% do caminho da mão até o fundo, como a Chama do corpo) e no pano do
                // molotov (a ponta do gargalo: a ponta mais perto da mão)
                (d.ossoDaChama, d.chamaNosBracos) = PontaNoOsso(d.bracosDoLampiao, "FP_Bracos_Lampiao", false, 0.7f);
                (d.ossoDoPavio, d.pavioNosBracos) = PontaNoOsso(d.bracosDoMolotov, "FP_Bracos_Molotov", true, 0.95f);
                relatorio.Add($"A chama nos braços: o lampião no {d.ossoDaChama} em {d.chamaNosBracos.ToString("F3")}, o molotov no {d.ossoDoPavio} em {d.pavioNosBracos.ToString("F3")}");
                foreach (var nome in new[] { "Lampiao", "Molotov" })
                {
                    var clipes = AssetDatabase.LoadAllAssetsAtPath(FbxBracos(nome)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
                        .Select(c => $"{c.name} {Mathf.RoundToInt(c.length * c.frameRate)}q{(c.isLooping ? " (laço)" : "")}");
                    relatorio.Add($"Braços {nome}: {string.Join(", ", clipes)}");
                }
                // os 3 itens: a malha do chão, o ícone e a foto
                int n = ItensDoFogo.Itens3.Length;
                d.malhasDosItens = new Mesh[n];
                d.icones = new Texture2D[n];
                d.fotos = new Texture2D[n];
                for (int k = 0; k < n; k++)
                {
                    int i = Itens.IndiceDoLoot(ItensDoFogo.Itens3[k]);
                    string modelo = Itens.ModeloDoLoot(i);
                    MontagemDasArmas.Juntar(MontagemDaProtecao.FbxItens, $"Item_{modelo}_", MontagemDasArmas.MalhaDoLoot(i));
                    AssetDatabase.SaveAssets();
                    var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDoLoot(i));
                    d.malhasDosItens[k] = malha;
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.IconeDoLoot(i), 128, 128, false, true);
                    MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.FotoDoLoot(i), 600, 500, false, false);
                    relatorio.Add($"Item {modelo}: {malha.bounds.size.ToString("F3")}");
                }
                AssetDatabase.Refresh();
                for (int k = 0; k < n; k++)
                {
                    int i = Itens.IndiceDoLoot(ItensDoFogo.Itens3[k]);
                    d.icones[k] = Imagem(MontagemDasArmas.IconeDoLoot(i));
                    d.fotos[k] = Imagem(MontagemDasArmas.FotoDoLoot(i));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
                PrefabUtility.SaveAsPrefabAsset(go, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Fogo] {Prefab}:\n{string.Join("\n", relatorio)}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                QualitySettings.SetQualityLevel(qualidade, false);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Um ponto do item nos braços, no espaço do osso que o segura (a pose de descanso): os vértices da malha do item (a
        /// segunda submalha, a da paleta) presos a esse osso, ao longo do eixo mais comprido, na ponta mais perto da origem do
        /// osso (a mão) ou na mais longe, a esta fração do caminho; nos outros eixos, o meio.
        /// </summary>
        static (string osso, Vector3 ponto) PontaNoOsso(GameObject fbx, string objeto, bool pontaPerto, float fracao)
        {
            var smr = fbx != null ? fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == objeto) : null;
            var m = smr != null ? smr.sharedMesh : null;
            if (m == null) return (null, Vector3.zero);
            var pesos = m.boneWeights;
            var bind = m.bindposes;
            var vert = m.vertices;
            var doItem = new HashSet<int>(m.GetTriangles(m.subMeshCount > 1 ? 1 : 0));
            var conta = new Dictionary<int, int>();
            foreach (int i in doItem)
            {
                conta.TryGetValue(pesos[i].boneIndex0, out int c);
                conta[pesos[i].boneIndex0] = c + 1;
            }
            int b = conta.OrderByDescending(kv => kv.Value).First().Key;
            var min = Vector3.one * 99f;
            var max = -min;
            foreach (int i in doItem)
            {
                if (pesos[i].boneIndex0 != b) continue;
                var p = bind[b].MultiplyPoint3x4(vert[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var tam = max - min;
            int eixo = tam.x >= tam.y && tam.x >= tam.z ? 0 : tam.y >= tam.z ? 1 : 2;
            float a = min[eixo], z = max[eixo];
            float fim = pontaPerto == (Mathf.Abs(a) < Mathf.Abs(z)) ? a : z, alvo = fim * fracao;
            // o meio da fatia da malha ali (o gargalo, o vidro), não o da caixa inteira
            var soma = Vector3.zero;
            int n = 0;
            foreach (int i in doItem)
            {
                if (pesos[i].boneIndex0 != b) continue;
                var p = bind[b].MultiplyPoint3x4(vert[i]);
                if (Mathf.Abs(p[eixo] - alvo) > 0.02f) continue;
                soma += p;
                n++;
            }
            var ponto = n > 0 ? soma / n : (min + max) * 0.5f;
            ponto[eixo] = alvo;
            return (smr.bones[b].name, ponto);
        }

        // a imagem com a importação dos ícones (a transparência, sem mipmaps, sem compressão)
        static Texture2D Imagem(string caminho)
        {
            if (!(AssetImporter.GetAtPath(caminho) is TextureImporter imp)) return null;
            if (!(imp.alphaIsTransparency && !imp.mipmapEnabled && imp.textureCompression == TextureImporterCompression.Uncompressed && imp.npotScale == TextureImporterNPOTScale.None))
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }
    }
}
