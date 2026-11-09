using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// As armas de sucata, as munições delas e a sucata (09/11/2026, a 1ª leva da etapa 1b do Armeiro de sucata). Menu: Zombie
    /// City Cleaner > Armas > Montar as armas de sucata. Pode rodar de novo à vontade.
    ///  - as armas (<c>Art/Armas/Modelos/Sucata_*.fbx</c>, do Blender/Armas/sucata_exportar.py): a importação, com a paleta dos
    ///    itens (<c>Materials/Pistola.mat</c>, a <c>Art/Paleta.png</c> 16 × 16), e a malha solta (<c>Malhas/Arma_Sucata_*</c>: o
    ///    chão, as costas e o cós);
    ///  - a sucata e as munições (<c>Loot_Sucata.fbx</c>, do Blender/sucata_itens.py): <c>Malhas/Loot_Sucata</c>,
    ///    <c>Mun_MunicaoPrego</c> e <c>Mun_MunicaoParafuso</c>;
    ///  - os ícones e as fotos dos seis, feitos aqui (uma câmera numa cena de prévia, o fundo transparente e o contorno claro,
    ///    como os do Blender): <c>Art/UI/Icones/Icone_&lt;modelo&gt;</c> e <c>Foto_&lt;modelo&gt;</c>;
    ///  - o prefab do jogador (<see cref="MontagemDaRede.MontarPrefab"/>: os braços da real com a malha da de sucata, e as cópias
    ///    no corpo, <see cref="ArmasNovas.CopiasDeSucataNoCorpo"/>) e o dos sobreviventes (a cópia do corpo);
    ///  - as malhas no "Itens no mundo" da cena Mundo (sem refazer os lugares do começo).
    /// </summary>
    public static class MontagemDaSucata
    {
        const string PastaIcones = "Assets/_Game/Art/UI/Icones";
        const string Paleta = "Assets/_Game/Art/Characters/Jogador/Materials/Pistola.mat";
        /// <summary>Os pixels de cada célula da mochila no ícone (os do Blender: 128).</summary>
        const int PorCelula = 128;

        [MenuItem("Zombie City Cleaner/Armas/Montar as armas de sucata")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as armas de sucata.");
                return;
            }
            PrepararModelos();
            FazerIcones();
            MontagemDasArmas.ImportarIcones();
            AssetDatabase.SaveAssets();
            MontagemDaRede.MontarPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset("Assets/_Game/Prefabs/Jogador.prefab", ImportAssetOptions.ForceUpdate);
            MontagemDosSobreviventes.MontarPrefab();
            AssetDatabase.SaveAssets();
            NaCenaDoMundo();
            Debug.Log("Armas de sucata montadas: as malhas, os ícones, o prefab do jogador e o dos sobreviventes e as malhas da cena Mundo.");
        }

        /// <summary>A importação e as malhas: as três armas, a sucata e as duas munições.</summary>
        public static void PrepararModelos()
        {
            foreach (var a in ArmasNovas.DeSucata)
            {
                ArmasNovas.Preparar(a);
                if (!ArmasNovas.MalhaSolta(System.Array.IndexOf(Armas.Modelos, a.modelo)))
                    Debug.LogWarning($"Arma de sucata: sem a malha solta de {a.modelo}.");
            }
            MontagemDasArmas.ImportarAvulsas(MontagemDasArmas.FbxLootSucata);
            for (int k = 0; k < Itens.QuantasMunicoes; k++)
                if (Itens.ModeloDaMunicao(k) == "MunicaoPrego" || Itens.ModeloDaMunicao(k) == "MunicaoParafuso") MontagemDasArmas.JuntarMunicao(k);
            int s = Itens.IndiceDoLoot(Item.Sucata);
            if (s < 0 || !MontagemDasArmas.JuntarDaSucata(Itens.ModeloDoLoot(s), MontagemDasArmas.MalhaDoLoot(s)))
                Debug.LogWarning("A sucata não está no Loot_Sucata.fbx (rode o Blender/sucata_itens.py).");
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ os ícones e as fotos

        /// <summary>
        /// Os ícones (na proporção das células do item, 128 px cada: as armas de lado, o cano para a direita; a sucata e as
        /// munições de três quartos, de cima) e as fotos (600 × 500, de três quartos), com o fundo transparente e o contorno
        /// claro dos do Blender.
        /// </summary>
        public static void FazerIcones()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Paleta);
            void Um(Item item, Mesh malha, string modelo)
            {
                if (malha == null)
                {
                    Debug.LogWarning($"Ícone de {modelo}: sem a malha.");
                    return;
                }
                var t = Itens.Tamanho(item);
                bool arma = Itens.EhArma(item);
                Fotografar(malha, material, $"{PastaIcones}/Icone_{modelo}.png", t.x * PorCelula, t.y * PorCelula, arma, true);
                Fotografar(malha, material, $"{PastaIcones}/Foto_{modelo}.png", 600, 500, arma, false);
            }
            foreach (var item in new[] { Item.PistolaSucata, Item.RifleSucata, Item.SubmetralhadoraSucata })
            {
                int m = Itens.Modelo(item);
                Um(item, AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDo(m)), Itens.Modelos[m]);
            }
            foreach (var item in new[] { Item.MunicaoPrego, Item.MunicaoParafuso })
            {
                int k = Itens.IndiceDaMunicao(item);
                Um(item, AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDaMunicao(k)), Itens.ModeloDaMunicao(k));
            }
            int s = Itens.IndiceDoLoot(Item.Sucata);
            Um(Item.Sucata, AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDoLoot(s)), Itens.ModeloDoLoot(s));
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Uma foto da malha: numa cena de prévia, com a luz de estúdio, duas vezes (no fundo preto e no branco: a diferença
        /// dá a transparência das bordas), com o contorno claro em volta.
        /// </summary>
        static void Fotografar(Mesh malha, Material material, string caminho, int largura, int altura, bool arma, bool icone)
        {
            var cena = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                var go = new GameObject("Item");
                SceneManager.MoveGameObjectToScene(go, cena);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                Luz(cena, new Vector3(40f, -35f, 0f), 1.25f, new Color(1f, 0.97f, 0.92f));
                Luz(cena, new Vector3(15f, 140f, 0f), 0.65f, new Color(0.85f, 0.9f, 1f));
                Luz(cena, new Vector3(-70f, 30f, 0f), 0.45f, Color.white);
                var camGo = new GameObject("Câmera");
                SceneManager.MoveGameObjectToScene(camGo, cena);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = cena;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 30f;
                cam.targetTexture = rt;
                var b = malha.bounds;
                float aspecto = largura / (float)altura;
                // as armas: o ícone de lado (o cano, +Z, para a direita), a foto de três quartos (de trás, o cano para a direita
                // e para o fundo); a sucata e as munições, de cima
                var dir = arma ? (icone ? new Vector3(1f, 0f, 0f) : new Vector3(0.85f, 0.4f, -0.6f)) : new Vector3(0.75f, 0.7f, -0.8f);
                dir.Normalize();
                var giro = Quaternion.LookRotation(-dir, Vector3.up);
                if (arma && icone)
                {
                    cam.orthographic = true;
                    float meiaAltura = Mathf.Max(b.extents.y, b.extents.z / aspecto);
                    cam.orthographicSize = meiaAltura * 1.12f;
                    camGo.transform.SetPositionAndRotation(b.center + dir * 3f, giro);
                }
                else
                {
                    cam.fieldOfView = 30f;
                    // a distância em que a caixa cabe (as oito quinas, vistas da câmera)
                    float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    float dist = 0f;
                    var volta = Quaternion.Inverse(giro);
                    for (int q = 0; q < 8; q++)
                    {
                        var quina = volta * Vector3.Scale(new Vector3((q & 1) == 0 ? -1 : 1, (q & 2) == 0 ? -1 : 1, (q & 4) == 0 ? -1 : 1), b.extents);
                        dist = Mathf.Max(dist, Mathf.Abs(quina.y) / tan - quina.z, Mathf.Abs(quina.x) / (tan * aspecto) - quina.z);
                    }
                    camGo.transform.SetPositionAndRotation(b.center + dir * dist * 1.12f, giro);
                }
                var preto = Ler(cam, rt, Color.black);
                var branco = Ler(cam, rt, Color.white);
                var tex = new Texture2D(largura, altura, TextureFormat.RGBA32, false);
                var px = new Color[preto.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    float alfa = Mathf.Clamp01(1f - ((branco[i].r - preto[i].r) + (branco[i].g - preto[i].g) + (branco[i].b - preto[i].b)) / 3f);
                    px[i] = alfa > 0.004f ? new Color(preto[i].r / alfa, preto[i].g / alfa, preto[i].b / alfa, alfa) : Color.clear;
                }
                tex.SetPixels(Contorno(px, largura, altura, icone ? 2 : 3));
                tex.Apply();
                File.WriteAllBytes(caminho, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                cam.targetTexture = null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(cena);
                Object.DestroyImmediate(rt);
            }
        }

        static Color[] Ler(Camera cam, RenderTexture rt, Color fundo)
        {
            cam.backgroundColor = fundo;
            cam.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        // o contorno claro (o dos ícones do Blender): a silhueta engordada uns pixels, por baixo da arma
        static Color[] Contorno(Color[] px, int l, int a, int raio)
        {
            var cor = new Color(0.78f, 0.78f, 0.76f, 1f);
            var saida = new Color[px.Length];
            for (int y = 0; y < a; y++)
                for (int x = 0; x < l; x++)
                {
                    float borda = 0f;
                    for (int dy = -raio; dy <= raio && borda < 1f; dy++)
                        for (int dx = -raio; dx <= raio; dx++)
                        {
                            if (dx * dx + dy * dy > raio * raio) continue;
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= l || yy >= a) continue;
                            borda = Mathf.Max(borda, px[yy * l + xx].a);
                        }
                    var c = px[y * l + x];
                    // a arma por cima do contorno
                    float alfa = c.a + borda * (1f - c.a);
                    saida[y * l + x] = alfa <= 0f ? Color.clear
                        : new Color((c.r * c.a + cor.r * borda * (1f - c.a)) / alfa, (c.g * c.a + cor.g * borda * (1f - c.a)) / alfa,
                                    (c.b * c.a + cor.b * borda * (1f - c.a)) / alfa, alfa);
                }
            return saida;
        }

        static void Luz(Scene cena, Vector3 giro, float intensidade, Color cor)
        {
            var go = new GameObject("Luz");
            SceneManager.MoveGameObjectToScene(go, cena);
            go.transform.rotation = Quaternion.Euler(giro);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensidade;
            l.color = cor;
        }

        // ------------------------------------------------------------------ a cena

        /// <summary>
        /// As malhas no "Itens no mundo" da cena Mundo (os modelos, que agora têm as três armas de sucata depois das reais, as
        /// munições e o saque), sem refazer o objeto nem os lugares do começo.
        /// </summary>
        public static void NaCenaDoMundo()
        {
            var cena = EditorSceneManager.GetActiveScene();
            if (cena.path != MontagemDoMundo.Cena)
            {
                if (cena.isDirty)
                {
                    Debug.LogWarning($"A cena {cena.name} tem mudanças: salve e rode de novo (as malhas da cena Mundo ficaram como estavam).");
                    return;
                }
                cena = EditorSceneManager.OpenScene(MontagemDoMundo.Cena);
            }
            var mundo = cena.GetRootGameObjects().Select(g => g.GetComponentInChildren<ItensNoMundo>(true)).FirstOrDefault(x => x != null);
            if (mundo == null)
            {
                Debug.LogWarning("A cena Mundo não tem o \"Itens no mundo\".");
                return;
            }
            mundo.malhas = Enumerable.Range(0, Itens.QuantosModelos).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDo(i))).ToArray();
            mundo.malhasDeMunicao = Enumerable.Range(0, Itens.QuantasMunicoes).Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDaMunicao(k))).ToArray();
            mundo.malhasDeLoot = MontagemDasArmas.MalhasDoLoot();
            EditorUtility.SetDirty(mundo);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        // ------------------------------------------------------------------ os painéis de metal (a 2ª leva, 09/11/2026)

        const string FbxDaSucata = "Assets/_Game/Art/Characters/Jogador/Loot_Sucata.fbx";
        const string PrefabDosPaineis = "Assets/_Game/Resources/PaineisDeSucata.prefab";

        /// <summary>
        /// O <c>Resources/PaineisDeSucata.prefab</c> que o <see cref="ZombieCityCleaner.World.PaineisDoMundo"/> usa: três filhos, na ordem de
        /// <see cref="ZombieCityCleaner.Mundo.PaineisDeSucata"/> (a chapa, a porta e a placa), cada um com a malha <c>Painel_*_corpo</c> do
        /// <c>Loot_Sucata.fbx</c> e o material da paleta dos itens. Sem mexer nas cenas (os painéis são postos no Play).
        /// </summary>
        [MenuItem("Zombie City Cleaner/Armas/Montar os painéis de sucata")]
        public static void MontarPaineis()
        {
            string[] nomes = { "Painel_Chapa_corpo", "Painel_Porta_corpo", "Painel_Placa_corpo" };
            var malhas = AssetDatabase.LoadAllAssetsAtPath(FbxDaSucata).OfType<Mesh>().ToList();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Paleta);
            var raiz = new GameObject("PaineisDeSucata");
            try
            {
                foreach (var nome in nomes)
                {
                    var malha = malhas.FirstOrDefault(m => m.name == nome);
                    if (malha == null)
                    {
                        Debug.LogError($"[Sucata] Sem a malha {nome} no {FbxDaSucata}.");
                        return;
                    }
                    var filho = new GameObject(nome.Replace("_corpo", ""));
                    filho.transform.SetParent(raiz.transform, false);
                    filho.AddComponent<MeshFilter>().sharedMesh = malha;
                    filho.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabDosPaineis));
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDosPaineis);
                Debug.Log($"[Sucata] Os painéis de metal: {PrefabDosPaineis} ({string.Join(", ", nomes)}; o material {Paleta}).");
            }
            finally { Object.DestroyImmediate(raiz); }
        }
    }
}
