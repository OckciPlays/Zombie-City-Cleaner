using System.Collections.Generic;
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
    /// As melhorias das armas de sucata (10/11/2026, a etapa 2 do Armeiro de sucata). Menu: Zombie City Cleaner > Armas >
    /// Montar as melhorias de sucata. Pode rodar de novo à vontade; não mexe nas cenas nem no prefab do jogador:
    ///  - os modelos (do Blender/Armas/sucata_acessorios.py): <c>Art/Armas/Modelos/Sucata_Acessorios.fbx</c> (as peças no
    ///    espaço de cada arma, <c>Sucata_&lt;Arma&gt;_acc_&lt;Peça&gt;</c> e o <c>_carregadorLongo</c>) e
    ///    <c>Art/Characters/Jogador/Loot_Acessorios.fbx</c> (os 9 itens soltos, <c>Item_&lt;Peça&gt;_corpo</c>: as malhas
    ///    <c>Malhas/Loot_&lt;Peça&gt;</c>);
    ///  - as matrizes do espaço da arma para os braços de primeira pessoa (o bindpose do osso gun e do mag vezes o encaixe
    ///    medido na malha <c>FP_Bracos_Sucata_*</c>) e para o corpo (o encaixe da <c>TP_Sucata_*_corpo</c> e do
    ///    <c>_carregador</c>), os braços e a arma solta sem o carregador curto (o estendido vai no lugar dele);
    ///  - os ícones e as fotos dos 9 itens (<c>Art/UI/Icones</c>), a foto de cada arma com espaço para as peças e uma foto
    ///    de cada peça por cima, no mesmo enquadramento (<c>Art/Armas/Melhorias</c>), e o material do pontinho vermelho;
    ///  - o <c>Resources/MelhoriasDaSucata.prefab</c> (o <see cref="PecasDasMelhorias"/>), que o jogo usa no Play.
    /// </summary>
    public static class MontagemDasMelhorias
    {
        const string FbxPecas = "Assets/_Game/Art/Armas/Modelos/Sucata_Acessorios.fbx";
        internal const string FbxItens = "Assets/_Game/Art/Characters/Jogador/Loot_Acessorios.fbx";
        const string Jogador = "Assets/_Game/Art/Characters/Jogador";
        const string Pasta = "Assets/_Game/Art/Armas/Melhorias";
        const string PastaIcones = "Assets/_Game/Art/UI/Icones";
        const string Paleta = Jogador + "/Materials/Pistola.mat";
        const string Prefab = "Assets/_Game/Resources/MelhoriasDaSucata.prefab";
        static readonly string[] Reais = { "Pistola", "Sniper", "Submetralhadora" };

        [MenuItem("Zombie City Cleaner/Armas/Montar as melhorias de sucata")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar as melhorias de sucata.");
                return;
            }
            Directory.CreateDirectory(Pasta);
            Importar();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Paleta);
            var raizPecas = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPecas);
            if (raizPecas == null) throw new FileNotFoundException("Rode o Blender/Armas/sucata_acessorios.py (modelos --assets).", FbxPecas);
            var go = new GameObject("MelhoriasDaSucata");
            // as fotos com a qualidade Mobile: na PC, o fundo da câmera de prévia sai de uma cor só (cinza azulado) no preto e
            // no branco, e a transparência se perde (10/11/2026)
            int qualidade = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);
            try
            {
                var d = go.AddComponent<PecasDasMelhorias>();
                d.material = material;
                d.materialDoPonto = MaterialDoPonto();
                d.pecas = new Mesh[3 * Melhorias.Quantas];
                d.fpCorpo = new Matrix4x4[3];
                d.fpCarregador = new Matrix4x4[3];
                d.tpCorpo = new Matrix4x4[3];
                d.tpCarregador = new Matrix4x4[3];
                d.bracosComCarregador = new Mesh[3];
                d.bracosSemCarregador = new Mesh[3];
                d.soltaSemCarregador = new Mesh[3];
                d.fotoDaArma = new Texture2D[3];
                d.fotosDasPecas = new Texture2D[3 * Melhorias.Quantas];
                var relatorio = new List<string>();
                for (int k = 0; k < 3; k++)
                {
                    string modelo = Armas.Modelos[(int)Melhorias.DasArmas[k]];
                    int pecas = 0;
                    for (int i = 0; i < Melhorias.Quantas; i++)
                    {
                        var p = (PecaDaMelhoria)i;
                        var m = MalhaDoObjeto(raizPecas, $"{modelo}_{Melhorias.Objeto(p)}");
                        if (m == null)
                        {
                            if (Melhorias.Serve(p, Melhorias.DasArmas[k])) Debug.LogWarning($"[Melhorias] Falta {modelo}_{Melhorias.Objeto(p)} no {FbxPecas}.");
                            continue;
                        }
                        d.pecas[k * Melhorias.Quantas + i] = m;
                        pecas++;
                    }
                    Encaixes(k, modelo, d, relatorio);
                    relatorio.Add($"{modelo}: {pecas} peças");
                }
                Itens9(d, material);
                AssetDatabase.Refresh();
                for (int k = 0; k < 3; k++) FotosDaArma(k, d, material);
                AssetDatabase.Refresh();
                CarregarFotos(d);
                Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
                PrefabUtility.SaveAsPrefabAsset(go, Prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Melhorias] {Prefab}: {string.Join("; ", relatorio)}.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                QualitySettings.SetQualityLevel(qualidade, false);
                AssetDatabase.SaveAssets();   // (o ProjectSettings volta com a qualidade de antes)
            }
        }

        static void Importar()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPecas);
            if (imp != null)
            {
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
                imp.isReadable = true;
                imp.importNormals = ModelImporterNormals.Import;
                imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.meshCompression = ModelImporterMeshCompression.Off;
                imp.optimizeMeshVertices = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
            }
            if (File.Exists(FbxItens)) MontagemDasArmas.ImportarAvulsas(FbxItens);
        }

        static Mesh MalhaDoObjeto(GameObject raiz, string nome)
        {
            if (raiz == null) return null;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != nome) continue;
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null) return mf.sharedMesh;
                var smr = t.GetComponent<SkinnedMeshRenderer>();
                if (smr != null) return smr.sharedMesh;
            }
            return null;
        }

        // ------------------------------------------------------------------ os encaixes (o espaço da arma em cada lugar)

        static void Encaixes(int k, string modelo, PecasDasMelhorias d, List<string> relatorio)
        {
            var raizDaArma = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Art/Armas/Modelos/{modelo}.fbx");
            var corpo = MalhaDoObjeto(raizDaArma, $"{modelo}_corpo");
            var carregador = MalhaDoObjeto(raizDaArma, $"{modelo}_carregador");
            // os braços: a malha FP_Bracos_<modelo> (as peças no submesh 1, cada uma no osso dela, com o encaixe da real)
            var fbxFP = AssetDatabase.LoadAssetAtPath<GameObject>($"{Jogador}/Jogador_FP_{Reais[k]}.fbx");
            var smr = fbxFP.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == $"FP_Bracos_{Reais[k]}");
            var ossos = smr.bones.Select(b => b != null ? b.name : "").ToList();
            int gun = ossos.IndexOf("gun"), mag = ossos.IndexOf("mag");
            var fp = AssetDatabase.LoadAssetAtPath<Mesh>($"{Jogador}/Malhas/FP_Bracos_{modelo}.asset");
            var vs = fp.vertices;
            var pesos = fp.boneWeights;
            var daArma = fp.GetTriangles(1).Distinct().ToArray();
            var m = ArmasNovas.Encaixe(daArma.Where(i => pesos[i].boneIndex0 == gun).Select(i => vs[i]).ToArray(), corpo.vertices, out float erro);
            d.fpCorpo[k] = fp.bindposes[gun] * m;
            d.fpCarregador[k] = mag >= 0 ? fp.bindposes[mag] * m : Matrix4x4.identity;
            d.bracosComCarregador[k] = fp;
            relatorio.Add($"{modelo} braços {erro * 1000f:F2} mm");
            // o corpo: a TP_<modelo>_corpo é a peça do corpo encaixada (a mesma forma, girada de 90 em 90 graus e deslocada)
            var tpCorpo = AssetDatabase.LoadAssetAtPath<Mesh>($"{Jogador}/Malhas/TP_{modelo}_corpo.asset");
            d.tpCorpo[k] = ArmasNovas.Encaixe(tpCorpo.vertices, corpo.vertices, out erro);
            relatorio.Add($"corpo {erro * 1000f:F2} mm");
            if (carregador == null) return;
            var tpCarregador = AssetDatabase.LoadAssetAtPath<Mesh>($"{Jogador}/Malhas/TP_{modelo}_carregador.asset");
            if (tpCarregador != null)
            {
                d.tpCarregador[k] = ArmasNovas.Encaixe(tpCarregador.vertices, carregador.vertices, out erro);
                relatorio.Add($"carregador {erro * 1000f:F2} mm");
            }
            // os braços sem o carregador curto (os triângulos do osso mag saem do submesh 1)
            if (mag >= 0) d.bracosSemCarregador[k] = BracosSem(fp, mag, $"{Jogador}/Malhas/FP_Bracos_{modelo}_SemCarregador.asset");
            // a arma solta sem o carregador (as peças juntas, menos ele)
            var outras = raizDaArma.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(modelo + "_") && t.name != $"{modelo}_carregador")
                                    .Select(t => t.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null).Select(f => f.sharedMesh).ToArray();
            d.soltaSemCarregador[k] = Gravar($"{Jogador}/Malhas/Arma_{modelo}_SemCarregador.asset", malha =>
            {
                malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                malha.CombineMeshes(outras.Select(o => new CombineInstance { mesh = o, transform = Matrix4x4.identity }).ToArray(), true, false);
            });
        }

        static Mesh BracosSem(Mesh fp, int osso, string caminho)
        {
            var pesos = fp.boneWeights;
            var arma = fp.GetTriangles(1);
            var fica = new List<int>();
            for (int t = 0; t < arma.Length; t += 3)
                if (!(pesos[arma[t]].boneIndex0 == osso && pesos[arma[t + 1]].boneIndex0 == osso && pesos[arma[t + 2]].boneIndex0 == osso))
                    fica.AddRange(new[] { arma[t], arma[t + 1], arma[t + 2] });
            // (pela API, como o ArmasNovas.BracosComArmaNova: o CopySerialized de uma malha com pesos embaralhava os índices)
            return Gravar(caminho, malha =>
            {
                malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                malha.SetVertices(fp.vertices);
                malha.SetNormals(fp.normals);
                malha.SetTangents(fp.tangents);
                malha.SetUVs(0, fp.uv);
                malha.boneWeights = fp.boneWeights;
                malha.bindposes = fp.bindposes;
                malha.subMeshCount = 2;
                malha.SetTriangles(fp.GetTriangles(0), 0);
                malha.SetTriangles(fica, 1);
                malha.RecalculateBounds();
                malha.bounds = fp.bounds;
            });
        }

        static Mesh Gravar(string caminho, System.Action<Mesh> encher)
        {
            var malha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            bool nova = malha == null;
            if (nova) malha = new Mesh();
            malha.Clear();
            malha.name = Path.GetFileNameWithoutExtension(caminho);
            encher(malha);
            if (malha.bounds.size == Vector3.zero) malha.RecalculateBounds();
            if (nova) AssetDatabase.CreateAsset(malha, caminho);
            else EditorUtility.SetDirty(malha);
            return malha;
        }

        static Material MaterialDoPonto()
        {
            string caminho = $"{Pasta}/PontoVermelho.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PontoVermelho" };
            mat.SetColor("_BaseColor", new Color(1f, 0.1f, 0.06f));
            AssetDatabase.CreateAsset(mat, caminho);
            return mat;
        }

        // ------------------------------------------------------------------ os 9 itens

        static void Itens9(PecasDasMelhorias d, Material material)
        {
            d.malhasDosItens = new Mesh[Melhorias.Quantas];
            for (int i = 0; i < Melhorias.Quantas; i++)
            {
                var item = Melhorias.ItemDa((PecaDaMelhoria)i);
                int k = Itens.IndiceDoLoot(item);
                if (k < 0 || !MontagemDasArmas.JuntarDaSucata(Itens.ModeloDoLoot(k), MontagemDasArmas.MalhaDoLoot(k)))
                {
                    Debug.LogWarning($"[Melhorias] O Item_{Melhorias.Modelo((PecaDaMelhoria)i)}_corpo não está no {FbxItens}.");
                    continue;
                }
                AssetDatabase.SaveAssets();
                var malha = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDoLoot(k));
                d.malhasDosItens[i] = malha;
                MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.IconeDoLoot(k), 128, 128, false, true);
                MontagemDaSucata.Fotografar(malha, material, MontagemDasArmas.FotoDoLoot(k), 600, 500, false, false);
            }
        }

        static void CarregarFotos(PecasDasMelhorias d)
        {
            d.icones = new Texture2D[Melhorias.Quantas];
            d.fotos = new Texture2D[Melhorias.Quantas];
            for (int i = 0; i < Melhorias.Quantas; i++)
            {
                int k = Itens.IndiceDoLoot(Melhorias.ItemDa((PecaDaMelhoria)i));
                d.icones[i] = Imagem(MontagemDasArmas.IconeDoLoot(k));
                d.fotos[i] = Imagem(MontagemDasArmas.FotoDoLoot(k));
            }
            for (int k = 0; k < 3; k++)
            {
                string modelo = Armas.Modelos[(int)Melhorias.DasArmas[k]];
                d.fotoDaArma[k] = Imagem($"{Pasta}/Foto_{modelo}_Melhorias.png");
                for (int i = 0; i < Melhorias.Quantas; i++)
                    d.fotosDasPecas[k * Melhorias.Quantas + i] = Imagem($"{Pasta}/Foto_{modelo}_{(PecaDaMelhoria)i}.png");
            }
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

        // ------------------------------------------------------------------ a foto da arma com as peças

        /// <summary>
        /// A foto da arma (de três quartos, como a <c>Foto_Sucata_*</c>, mas com espaço para todas as peças) e, de cada peça,
        /// só o que ela muda na foto (a arma com a peça, menos a arma sem ela; o carregador estendido, com a arma sem o
        /// curto), com o contorno claro: o jogo desenha a da arma e as das peças postas por cima.
        /// </summary>
        static void FotosDaArma(int k, PecasDasMelhorias d, Material material)
        {
            string modelo = Armas.Modelos[(int)Melhorias.DasArmas[k]];
            var solta = AssetDatabase.LoadAssetAtPath<Mesh>(MontagemDasArmas.MalhaDo((int)Melhorias.DasArmas[k]));
            var caixa = solta.bounds;
            for (int i = 0; i < Melhorias.Quantas; i++)
                if (d.pecas[k * Melhorias.Quantas + i] != null) caixa.Encapsulate(d.pecas[k * Melhorias.Quantas + i].bounds);
            const int L = 600, A = 500;
            var fundo = Foto(new[] { solta }, material, caixa, L, A);
            Salvar(fundo, L, A, $"{Pasta}/Foto_{modelo}_Melhorias.png");
            for (int i = 0; i < Melhorias.Quantas; i++)
            {
                var peca = d.pecas[k * Melhorias.Quantas + i];
                if (peca == null) continue;
                bool longo = (PecaDaMelhoria)i == PecaDaMelhoria.CarregadorLongo;
                var arma = longo && d.soltaSemCarregador[k] != null ? d.soltaSemCarregador[k] : solta;
                var com = Foto(new[] { arma, peca }, material, caixa, L, A);
                var so = new Color[com.Length];
                for (int p = 0; p < com.Length; p++)
                {
                    var a = com[p];
                    var b = fundo[p];
                    bool mudou = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) > 0.04f;
                    so[p] = mudou ? a : Color.clear;
                }
                Salvar(so, L, A, $"{Pasta}/Foto_{modelo}_{(PecaDaMelhoria)i}.png");
            }
        }

        static void Salvar(Color[] px, int l, int a, string caminho)
        {
            var tex = new Texture2D(l, a, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // as malhas (no espaço da arma) numa cena de prévia, com a luz da MontagemDaSucata: no fundo preto e no branco (a
        // diferença dá a transparência), com o contorno claro; o enquadramento é o da caixa dada (o mesmo em todas)
        static Color[] Foto(Mesh[] malhas, Material material, Bounds b, int largura, int altura)
        {
            var cena = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                foreach (var m in malhas)
                {
                    var go = new GameObject("Peça");
                    SceneManager.MoveGameObjectToScene(go, cena);
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
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
                cam.fieldOfView = 30f;
                float aspecto = largura / (float)altura;
                var dir = new Vector3(0.85f, 0.4f, -0.6f).normalized;
                var giro = Quaternion.LookRotation(-dir, Vector3.up);
                float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float dist = 0f;
                var volta = Quaternion.Inverse(giro);
                for (int q = 0; q < 8; q++)
                {
                    var quina = volta * Vector3.Scale(new Vector3((q & 1) == 0 ? -1 : 1, (q & 2) == 0 ? -1 : 1, (q & 4) == 0 ? -1 : 1), b.extents);
                    dist = Mathf.Max(dist, Mathf.Abs(quina.y) / tan - quina.z, Mathf.Abs(quina.x) / (tan * aspecto) - quina.z);
                }
                camGo.transform.SetPositionAndRotation(b.center + dir * dist * 1.08f, giro);
                var preto = Ler(cam, rt, Color.black);
                var branco = Ler(cam, rt, Color.white);
                var px = new Color[preto.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    float alfa = Mathf.Clamp01(1f - ((branco[i].r - preto[i].r) + (branco[i].g - preto[i].g) + (branco[i].b - preto[i].b)) / 3f);
                    px[i] = alfa > 0.004f ? new Color(preto[i].r / alfa, preto[i].g / alfa, preto[i].b / alfa, alfa) : Color.clear;
                }
                cam.targetTexture = null;
                return MontagemDaSucata.Contorno(px, largura, altura, 3);
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
    }
}
