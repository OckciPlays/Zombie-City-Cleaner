using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os Ocos (28/10/2026, a pedido: os novos zumbis de ~5 mil triângulos, 12 variações feitas no Blender, na pasta
    /// "D:\teste blender\O Oco"; o Oco de 125 mil saiu do jogo). Eles entram na mesma montagem dos outros zumbis (as hitboxes,
    /// o corpo que cai e o desmembramento), com três diferenças:
    ///  - <b>as animações:</b> o FBX não traz animação. Os clipes dos zumbis (os do ZombieGaroto, numa cópia importada como
    ///    Humanoid, Art/Characters/Oco/Oco_Animacoes*.fbx) passam para o esqueleto de cada Oco pelo avatar Humanoid dele e são
    ///    gravados quadro a quadro como clipes comuns (Generic) dos ossos dele (Oco/Clipes/&lt;modelo&gt;/). No jogo, o
    ///    Animator usa esses clipes com um avatar Generic: o mesmo custo dos zumbis de antes;
    ///  - <b>a cabeça:</b> o FBX tem um material só. A malha é separada aqui em corpo, cabeça (os triângulos presos sobretudo
    ///    ao osso "head") e a tampa de carne do pescoço (um leque em cada borda do corte), os três submeshes que a montagem
    ///    espera (como o "_Corpo", o "_Cabeca" e o "Zombie_Flesh" dos modelos do Tripo);
    ///  - <b>o material:</b> o shader ZombieCityCleaner/Oco (o Lit da URP com a pedra do sol por cima) com as texturas
    ///    assadas no Blender (a cor, o normal e a lisura, 1024 px).
    /// Os ossos já vêm do Blender com os nomes do jogo (hips, spine, chest, neck, head, shoulder.L, upper_arm.L, forearm.L,
    /// hand.L, thigh.L, shin.L, foot.L, toe.L; renomeados na exportação, scratchpad exportar_ocos.py), com o spine_02 entre o
    /// spine e o chest e os 3 ossos de cada dedo embaixo da mão.
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        const string PastaOco = Arte + "/Oco";
        // as cópias Humanoid do ZombieGaroto.fbx e do _Extra (as mesmas do Oco de teste do F1, MontagemDoOco: a importação
        // aqui usa as mesmas opções, então rodar uma montagem não muda a outra)
        const string AnimacoesHumanoides = "Assets/_Game/Art/Characters/Oco/Oco_Animacoes.fbx";
        const string AnimacoesHumanoidesExtra = "Assets/_Game/Art/Characters/Oco/Oco_AnimacoesExtra.fbx";
        const string ClipesDosOcos = PastaOco + "/Clipes";
        const string MateriaisDosOcos = PastaOco + "/Materiais";

        // os especiais (MontagemDosZumbis.Especiais.cs) são montados como os Ocos (os modelos na pasta deles)
        static bool EhOco(string nome) => nome.StartsWith("Oco_") || EhEspecial(nome);
        static string FbxDe(string nome) => EhEspecial(nome) ? $"{PastaEspeciais}/{nome}.fbx" : EhOco(nome) ? $"{PastaOco}/{nome}.fbx" : $"{Arte}/{nome}.fbx";
        static bool EhDedo(string osso) =>
            osso.StartsWith("thumb_") || osso.StartsWith("index_") || osso.StartsWith("middle_") || osso.StartsWith("ring_") || osso.StartsWith("pinky_");

        static Modelo ImportarOco(string nome, int indice)
        {
            var m = new Modelo { indice = indice, nome = nome, fbx = FbxDe(nome) };
            var imp = (ModelImporter)AssetImporter.GetAtPath(m.fbx);
            if (imp == null) throw new FileNotFoundException("Falta o FBX do Oco (exportado do Blender)", m.fbx);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.generateSecondaryUV = false;
            imp.SaveAndReimport();
            var humano = AssetDatabase.LoadAllAssetsAtPath(m.fbx).OfType<Avatar>().FirstOrDefault();
            if (humano == null || !humano.isValid || !humano.isHuman)
                throw new System.Exception($"{nome}: o avatar Humanoid não ficou válido (os ossos do Blender mudaram de nome?).");

            m.avatar = AvatarGenerico(m);
            m.clipes = GravarClipes(m, humano);

            var smr = Carregar<GameObject>(m.fbx).GetComponentInChildren<SkinnedMeshRenderer>(true);
            Directory.CreateDirectory(Malhas);
            m.original = Salvar(CabecaECarne(smr, $"{nome}_Original"), $"{Malhas}/{nome}_Original.asset");
            m.submeshCabeca = 1;
            m.submeshCarne = 2;
            m.inteira = Malha(m.original, $"{nome}_Inteira", m.original.GetTriangles(0).Concat(m.original.GetTriangles(1)));
            m.semCabeca = Malha(m.original, $"{nome}_SemCabeca", m.original.GetTriangles(0), m.original.GetTriangles(2));
            m.altura = m.original.bounds.size.z;   // a malha do Blender tem o z para cima
            m.longe = new Mesh[0];
            m.material = MaterialDoOco(nome);
            return m;
        }

        /// <summary>O avatar Generic do Oco (o Animator do jogo toca os clipes gravados, não os Humanoid).</summary>
        static Avatar AvatarGenerico(Modelo m)
        {
            var inst = (GameObject)Object.Instantiate(Carregar<GameObject>(m.fbx));
            var avatar = AvatarBuilder.BuildGenericAvatar(inst, "");
            Object.DestroyImmediate(inst);
            avatar.name = m.nome + "_Generico";
            Directory.CreateDirectory($"{ClipesDosOcos}/{m.nome}");
            string caminho = $"{ClipesDosOcos}/{m.nome}/{m.nome}_Generico.asset";
            var velho = AssetDatabase.LoadAssetAtPath<Avatar>(caminho);
            if (velho != null)
            {
                EditorUtility.CopySerialized(avatar, velho);
                Object.DestroyImmediate(avatar);
                return velho;
            }
            AssetDatabase.CreateAsset(avatar, caminho);
            return avatar;
        }

        // ------------------------------------------------------------------ as animações

        /// <summary>Os 10 clipes dos zumbis, Humanoid (as cópias do ZombieGaroto.fbx e do _Extra), pelo nome do jogo.</summary>
        static Dictionary<string, AnimationClip> ClipesHumanoides()
        {
            var clipes = ImportarHumanoide(AnimacoesHumanoides, Clipes);
            foreach (var c in ImportarHumanoide(AnimacoesHumanoidesExtra, ClipesExtra)) clipes[c.Key] = c.Value;
            // a agonia no sol (a etapa 7): feita por código nos músculos, partindo da pose parada do Idle
            foreach (var c in ClipesHumanoidesDaAgonia(FbxDe("Oco_Original"), clipes["Idle"])) clipes[c.Key] = c.Value;
            // o levantar numa perna (Blender/equilibrio_oco.py), se o FBX já existe
            foreach (var c in ClipesHumanoidesDoEquilibrio()) clipes[c.Key] = c.Value;
            // as animações dos especiais (Blender/especiais_animacoes.py), se o FBX já existe
            foreach (var c in ClipesHumanoidesEspeciais()) clipes[c.Key] = c.Value;
            return clipes;
        }

        static Dictionary<string, AnimationClip> ImportarHumanoide(string fbx, (string nome, bool loop)[] lista)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp == null) throw new FileNotFoundException("Faltam as animações Humanoid dos zumbis (a cópia do ZombieGaroto)", fbx);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            var padrao = imp.defaultClipAnimations;
            imp.clipAnimations = lista.Select(c =>
            {
                var clipe = padrao.First(p => p.takeName.EndsWith("|Zombie_" + c.nome));
                clipe.name = c.nome;
                clipe.loopTime = c.loop;
                clipe.loopPose = c.loop;
                // no lugar: o giro e a altura do quadril ficam na pose (o agente move o zumbi)
                clipe.lockRootRotation = true;
                clipe.keepOriginalOrientation = true;
                clipe.lockRootHeightY = true;
                clipe.keepOriginalPositionY = true;
                clipe.lockRootPositionXZ = true;
                clipe.keepOriginalPositionXZ = true;
                return clipe;
            }).ToArray();
            imp.SaveAndReimport();
            // o mapeamento automático deixa o "chest" do zumbi de fora (entre o Spine e o Neck): sem ele, a curvatura do
            // tronco (o zumbi curvado) se perdia
            var hd = imp.humanDescription;
            if (hd.human != null && hd.human.Length > 0 && !hd.human.Any(h => h.humanName == "Chest"))
            {
                var ossos = hd.human.ToList();
                ossos.Add(new HumanBone { humanName = "Chest", boneName = "chest", limit = new HumanLimit { useDefaultValues = true } });
                hd.human = ossos.ToArray();
                imp.humanDescription = hd;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        }

        /// <summary>
        /// Os clipes do Oco: cada clipe Humanoid tocado no esqueleto dele (pelo avatar Humanoid do FBX) e gravado quadro a
        /// quadro (o giro de cada osso, menos os dedos, e a posição do quadril) num clipe comum, com o loop dos zumbis.
        /// </summary>
        static Dictionary<string, AnimationClip> GravarClipes(Modelo m, Avatar humano)
        {
            var fontes = ClipesHumanoides();
            return Gravar(m.nome, m.fbx, humano, fontes,
                          Clipes.Concat(ClipesExtra).Concat(ClipesDaAgonia).Concat(ClipesDoEquilibrio.Where(c => fontes.ContainsKey(c.nome)))
                                 .Concat(ClipesEspeciais.Where(c => fontes.ContainsKey(c.nome))));
        }

        /// <summary>Grava os clipes da lista (das fontes Humanoid) no esqueleto do modelo, em Oco/Clipes/&lt;modelo&gt;/.</summary>
        static Dictionary<string, AnimationClip> Gravar(string nomeDoModelo, string fbx, Avatar humano, Dictionary<string, AnimationClip> fontes,
                                                         IEnumerable<(string nome, bool loop)> lista)
        {
            string pasta = $"{ClipesDosOcos}/{nomeDoModelo}";
            Directory.CreateDirectory(pasta);
            var inst = (GameObject)Object.Instantiate(Carregar<GameObject>(fbx));
            var animador = inst.GetComponent<Animator>();
            if (animador == null) animador = inst.AddComponent<Animator>();
            animador.avatar = humano;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ctrl = new AnimatorController { name = "Gravação do " + nomeDoModelo };
            ctrl.AddLayer("Base");
            var sm = ctrl.layers[0].stateMachine;
            foreach (var f in fontes) sm.AddState(f.Key).motion = f.Value;
            animador.runtimeAnimatorController = ctrl;

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var ossos = smr.bones.Where(b => b != null && !EhDedo(b.name)).ToArray();
            var quadril = ossos.First(b => b.name == "hips");
            string[] giro = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            string[] lugar = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };

            var saida = new Dictionary<string, AnimationClip>();
            try
            {
                foreach (var (nome, loop) in lista)
                {
                    var fonte = fontes[nome];
                    animador.Rebind();
                    var gravador = new GameObjectRecorder(inst);
                    foreach (var o in ossos)
                    {
                        string caminho = AnimationUtility.CalculateTransformPath(o, inst.transform);
                        foreach (var p in giro) gravador.Bind(EditorCurveBinding.FloatCurve(caminho, typeof(Transform), p));
                        if (o == quadril) foreach (var p in lugar) gravador.Bind(EditorCurveBinding.FloatCurve(caminho, typeof(Transform), p));
                    }
                    float fps = Mathf.Max(24f, fonte.frameRate);
                    int quadros = Mathf.Max(1, Mathf.RoundToInt(fonte.length * fps));
                    for (int i = 0; i <= quadros; i++)
                    {
                        animador.Play(nome, 0, (float)i / quadros);
                        animador.Update(0f);
                        gravador.TakeSnapshot(i == 0 ? 0f : fonte.length / quadros);
                    }
                    string arquivo = $"{pasta}/{nome}.anim";
                    var clipe = AssetDatabase.LoadAssetAtPath<AnimationClip>(arquivo);
                    bool novo = clipe == null;
                    if (novo) clipe = new AnimationClip();
                    clipe.ClearCurves();
                    gravador.SaveToClip(clipe, fps);
                    clipe.name = nome;
                    var ajustes = AnimationUtility.GetAnimationClipSettings(clipe);
                    ajustes.loopTime = loop;
                    AnimationUtility.SetAnimationClipSettings(clipe, ajustes);
                    if (novo) AssetDatabase.CreateAsset(clipe, arquivo);
                    else EditorUtility.SetDirty(clipe);
                    saida[nome] = clipe;
                }
            }
            finally
            {
                Object.DestroyImmediate(inst);
                Object.DestroyImmediate(ctrl);
            }
            AssetDatabase.SaveAssets();
            return saida;
        }

        // ------------------------------------------------------------------ a cabeça e a tampa do pescoço

        /// <summary>
        /// A malha do Oco em 3 submeshes: 0 o corpo, 1 a cabeça (os triângulos com mais da metade do peso no osso "head")
        /// e 2 a carne do pescoço (a tampa da borda entre os dois, que só aparece sem a cabeça: um leque até o meio de cada
        /// borda, com vértices próprios, a normal para fora e presos ao "neck"). As bordas são achadas pela posição (os
        /// vértices repetidos das costuras do UV contam como um).
        /// </summary>
        static Mesh CabecaECarne(SkinnedMeshRenderer smr, string nome)
        {
            var fonte = smr.sharedMesh;
            int cabeca = System.Array.FindIndex(smr.bones, b => b.name == "head");
            int pescoco = System.Array.FindIndex(smr.bones, b => b.name == "neck");
            if (cabeca < 0 || pescoco < 0) throw new System.Exception($"{nome}: o esqueleto não tem o head ou o neck.");
            var vs = fonte.vertices;
            var ns = fonte.normals;
            var ts = fonte.tangents;
            var uv = fonte.uv;
            var pesos = fonte.boneWeights;
            var tris = Enumerable.Range(0, fonte.subMeshCount).SelectMany(fonte.GetTriangles).ToArray();

            float NaCabeca(int v)
            {
                var w = pesos[v];
                return (w.boneIndex0 == cabeca ? w.weight0 : 0f) + (w.boneIndex1 == cabeca ? w.weight1 : 0f) +
                       (w.boneIndex2 == cabeca ? w.weight2 : 0f) + (w.boneIndex3 == cabeca ? w.weight3 : 0f);
            }

            // os vértices soldados pela posição (0,1 mm)
            var soldado = new int[vs.Length];
            var porPosicao = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vs.Length; i++)
            {
                var k = Vector3Int.RoundToInt(vs[i] * 10000f);
                if (!porPosicao.TryGetValue(k, out int primeiro)) porPosicao[k] = primeiro = i;
                soldado[i] = primeiro;
            }
            long Aresta(int a, int b) { a = soldado[a]; b = soldado[b]; return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

            var corpo = new List<int>();
            var daCabeca = new List<int>();
            var arestasDaCabeca = new HashSet<long>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if ((NaCabeca(a) + NaCabeca(b) + NaCabeca(c)) / 3f > 0.5f)
                {
                    daCabeca.Add(a); daCabeca.Add(b); daCabeca.Add(c);
                    arestasDaCabeca.Add(Aresta(a, b)); arestasDaCabeca.Add(Aresta(b, c)); arestasDaCabeca.Add(Aresta(c, a));
                }
                else { corpo.Add(a); corpo.Add(b); corpo.Add(c); }
            }

            // a borda: as arestas do corpo que a cabeça também tem, na direção do triângulo do corpo
            var borda = new List<(int a, int b)>();
            for (int t = 0; t < corpo.Count; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = corpo[t + k], b = corpo[t + (k + 1) % 3];
                    if (arestasDaCabeca.Contains(Aresta(a, b))) borda.Add((a, b));
                }
            if (borda.Count < 3) throw new System.Exception($"{nome}: não achei a borda entre a cabeça e o corpo.");

            // cada pedaço ligado da borda vira um leque
            var pai = new Dictionary<int, int>();
            int Raiz(int x) { while (pai.TryGetValue(x, out int p) && p != x) x = p; return x; }
            foreach (var (a, b) in borda)
            {
                int ra = Raiz(soldado[a]), rb = Raiz(soldado[b]);
                if (!pai.ContainsKey(ra)) pai[ra] = ra;
                if (!pai.ContainsKey(rb)) pai[rb] = rb;
                if (ra != rb) pai[ra] = rb;
            }
            Vector3 meioDaCabeca = Vector3.zero;
            foreach (int i in daCabeca) meioDaCabeca += vs[i];
            meioDaCabeca /= Mathf.Max(1, daCabeca.Count);

            var nvs = new List<Vector3>(vs);
            var nns = new List<Vector3>(ns);
            var nts = new List<Vector4>(ts.Length == vs.Length ? ts : new Vector4[vs.Length]);
            var nuv = new List<Vector2>(uv.Length == vs.Length ? uv : new Vector2[vs.Length]);
            var npesos = new List<BoneWeight>(pesos);
            var carne = new List<int>();
            var presoAoPescoco = new BoneWeight { boneIndex0 = pescoco, weight0 = 1f };
            foreach (var grupo in borda.GroupBy(e => Raiz(soldado[e.a])))
            {
                var arestas = grupo.ToList();
                if (arestas.Count < 3) continue;
                Vector3 meio = Vector3.zero;
                foreach (var (a, _) in arestas) meio += vs[a];
                meio /= arestas.Count;
                // a normal da tampa: para o lado da cabeça
                Vector3 normal = Vector3.zero;
                foreach (var (a, b) in arestas) normal += Vector3.Cross(vs[a] - meio, vs[b] - meio);
                if (Vector3.Dot(normal, meioDaCabeca - meio) < 0f) normal = -normal;
                normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
                var lado = Vector3.Cross(normal, Vector3.right);
                if (lado.sqrMagnitude < 1e-6f) lado = Vector3.Cross(normal, Vector3.forward);
                lado.Normalize();
                var tangente = new Vector4(lado.x, lado.y, lado.z, 1f);
                int Novo(Vector3 p, Vector2 u, BoneWeight w)
                {
                    nvs.Add(p); nns.Add(normal); nts.Add(tangente); nuv.Add(u); npesos.Add(w);
                    return nvs.Count - 1;
                }
                int centro = Novo(meio, Vector2.zero, presoAoPescoco);
                var copia = new Dictionary<int, int>();
                int Copia(int v)
                {
                    if (!copia.TryGetValue(soldado[v], out int c)) copia[soldado[v]] = c = Novo(vs[v], uv.Length == vs.Length ? uv[v] : Vector2.zero, pesos[v]);
                    return c;
                }
                foreach (var (a, b) in arestas)
                {
                    // o lado da cabeça tinha a aresta ao contrário (b → a): a tampa fica virada como ela
                    int pa = Copia(b), pb = Copia(a);
                    var n = Vector3.Cross(nvs[pb] - nvs[pa], nvs[centro] - nvs[pa]);
                    if (Vector3.Dot(n, normal) >= 0f) { carne.Add(pa); carne.Add(pb); carne.Add(centro); }
                    else { carne.Add(pb); carne.Add(pa); carne.Add(centro); }
                }
            }

            var malha = new Mesh { name = nome };
            if (nvs.Count > 65000) malha.indexFormat = IndexFormat.UInt32;
            malha.SetVertices(nvs);
            malha.SetNormals(nns);
            malha.SetTangents(nts);
            malha.SetUVs(0, nuv);
            malha.boneWeights = npesos.ToArray();
            malha.bindposes = fonte.bindposes;
            malha.subMeshCount = 3;
            malha.SetTriangles(corpo, 0, false);
            malha.SetTriangles(daCabeca, 1, false);
            malha.SetTriangles(carne, 2, false);
            malha.RecalculateBounds();
            return malha;
        }

        // ------------------------------------------------------------------ o material

        /// <summary>
        /// O shader do Oco (o Lit da URP com a pedra do sol, Shaders/Oco.shader) com as texturas do Blender: a cor, o normal e
        /// a lisura (o alfa do _MetalLiso, 1 - a rugosidade, × 0,35).
        /// </summary>
        static Material MaterialDoOco(string nome)
        {
            Directory.CreateDirectory(MateriaisDosOcos);
            string pasta = EhEspecial(nome) ? PastaEspeciais : PastaOco;
            string cor = $"{pasta}/{nome}_BaseColor.png", normal = $"{pasta}/{nome}_Normal.png", liso = $"{pasta}/{nome}_MetalLiso.png";
            TexturaDoOco(cor, TextureImporterType.Default, true);
            TexturaDoOco(normal, TextureImporterType.NormalMap, false);
            TexturaDoOco(liso, TextureImporterType.Default, false);
            string caminho = $"{MateriaisDosOcos}/{nome}.mat";
            // o shader do Oco: o Lit da URP com a pedra do sol por cima (Shaders/Oco.shader)
            var shader = Shader.Find("ZombieCityCleaner/Oco");
            if (shader == null) throw new System.Exception("Falta o shader ZombieCityCleaner/Oco (Assets/_Game/Shaders/Oco.shader).");
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", Carregar<Texture2D>(cor));
            m.SetTexture("_BumpMap", Carregar<Texture2D>(normal));
            m.SetFloat("_BumpScale", 1f);
            m.SetTexture("_MetallicGlossMap", Carregar<Texture2D>(liso));
            m.SetFloat("_Petrificacao", 0f);
            m.SetFloat("_AlturaDoCorpo", EhEspecial(nome) ? 2.25f : 1.85f);   // a pedra sobe até o alto da cabeça
            // com o mapa, a lisura é o alfa dele vezes este valor. O mapa do Blender dá 0,5 a 0,7 em média (a pele molhada):
            // de perto, a lanterna estourava o Oco de branco. Com 0,35, fica em ~0,2, perto dos zumbis de antes (0,15)
            m.SetFloat("_Smoothness", 0.35f);
            m.SetShaderPassEnabled("MotionVectors", false);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void TexturaDoOco(string caminho, TextureImporterType tipo, bool srgb)
        {
            var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (ti == null) throw new FileNotFoundException("Falta a textura do Oco", caminho);
            if (ti.textureType == tipo && ti.sRGBTexture == srgb && ti.maxTextureSize == 1024) return;
            ti.textureType = tipo;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }
}
