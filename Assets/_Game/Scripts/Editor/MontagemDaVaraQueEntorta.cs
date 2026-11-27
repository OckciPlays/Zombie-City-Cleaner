using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A vara que entorta (27/11/2026, a pesca nova, a etapa 2: "a ponta da vara de pesca ela entorta também"), sem o Blender:
    /// nas duas varas, uma corrente de 4 ossos ("Dobra1" a "Dobra4") dos 0,48 m (da pegada para a ponta: os 35% da vara) até a
    /// ponta, cada um filho do anterior, e a <see cref="VaraQueEntorta.NomeDaPonta"/> no último (no lugar da ponta de hoje, que
    /// fica onde está). Os vértices da vara vão para os ossos pelo lugar ao longo dela (o pedaço de cada osso, com 5 cm de
    /// mistura em cada junta); o cabo, a carretilha e a manivela ficam no osso de antes. Quem dobra é a
    /// <see cref="VaraQueEntorta"/>.
    /// <para>
    /// A malha da vara é subdividida ao longo dela na parte que entorta (nenhuma aresta mais comprida que 6 cm nessa direção):
    /// o tubo da vara vinha com os vértices só nas pontas, e entortando ele virava uma reta da carretilha até a ponta, com os
    /// passadores no arco, soltos dele (visto no primeiro teste).
    /// </para>
    /// <list type="bullet">
    /// <item><b>A vara do corpo</b> (<c>Resources/VaraDoCorpo</c>, um prefab comum): o objeto "Vara" troca o MeshFilter e o
    /// MeshRenderer por um SkinnedMeshRenderer com uma cópia da malha (<c>Art/Pesca/Vara_QueEntorta.asset</c>); o próprio
    /// "Vara" é o primeiro osso.</item>
    /// <item><b>Os braços com a vara</b> (<c>Resources/BracosVara</c>, uma variante do <c>Jogador_FP_Vara.fbx</c>): a corrente
    /// filha do osso "vara", a cópia da malha dos braços (<c>Malhas/FP_Bracos_Vara_QueEntorta.asset</c>, a vara subdividida e
    /// com os pesos refeitos) e os ossos novos no fim da lista do SkinnedMeshRenderer (o FBX não muda).</item>
    /// </list>
    /// Pode rodar de novo: refaz a partir das malhas dos FBX. A <c>MontagemDaVara</c> chama esta no fim (ela refaz os prefabs).
    /// </summary>
    public static class MontagemDaVaraQueEntorta
    {
        const string PrefabDoCorpo = "Assets/_Game/Resources/" + VaraNoCorpo.Recurso + ".prefab";
        const string PrefabDosBracos = "Assets/_Game/Resources/" + ArmaJogador.RecursoDosBracosDaVara + ".prefab";
        const string MalhaDoCorpo = "Assets/_Game/Art/Pesca/Vara_QueEntorta.asset";
        const string MalhaDosBracos = "Assets/_Game/Art/Characters/Jogador/Malhas/FP_Bracos_Vara_QueEntorta.asset";
        /// <summary>Onde a parte que entorta começa (m, da pegada para a ponta) e a mistura dos pesos em cada junta (m, de cada lado).</summary>
        const float Comeco = 0.48f, Mistura = 0.05f;
        /// <summary>A aresta mais comprida, ao longo da vara, na parte que entorta (m).</summary>
        const float Passo = 0.06f;

        [MenuItem("Zombie City Cleaner/Jogador/Montar a vara que entorta")]
        public static void Montar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de montar a vara que entorta.");
                return;
            }
            string corpo = MontarOCorpo(), bracos = MontarOsBracos();
            AssetDatabase.SaveAssets();
            Debug.Log($"A vara que entorta: o corpo {corpo}; os braços {bracos}.");
        }

        // ------------------------------------------------------------------ a vara do corpo

        static string MontarOCorpo()
        {
            var fonte = MalhaDoFbx(MontagemDaVara.FbxDaVara, "Vara");
            if (fonte == null) return "sem a malha \"Vara\" do Vara.fbx";
            var raiz = PrefabUtility.LoadPrefabContents(PrefabDoCorpo);
            try
            {
                var vara = Achar(raiz.transform, "Vara");
                var ponta = vara != null ? Achar(vara, "Ponta") : null;
                if (vara == null || ponta == null) return "sem a \"Vara\" ou a \"Ponta\" no prefab";
                // o que já estava montado sai (a malha volta a ser a do FBX)
                Material[] materiais;
                ShadowCastingMode sombra;
                var velha = vara.GetComponent<SkinnedMeshRenderer>();
                if (velha != null)
                {
                    materiais = velha.sharedMaterials;
                    sombra = velha.shadowCastingMode;
                    Object.DestroyImmediate(velha);
                }
                else
                {
                    var desenho = vara.GetComponent<MeshRenderer>();
                    if (desenho == null) return "sem o desenho da vara";
                    materiais = desenho.sharedMaterials;
                    sombra = desenho.shadowCastingMode;
                    Object.DestroyImmediate(desenho);
                }
                var filtro = vara.GetComponent<MeshFilter>();
                if (filtro != null) Object.DestroyImmediate(filtro);
                TirarACorrente(vara);

                Vector3 rumo = ponta.localPosition.normalized;
                float comprimento = ponta.localPosition.magnitude;
                var ossos = Corrente(vara, rumo, comprimento, out var juntas);
                var malha = Subdividir(fonte, 0, v => Vector3.Dot(v, rumo), Comeco - Mistura, out int novos);
                malha.name = "Vara_QueEntorta";
                var vertices = malha.vertices;
                var pesos = new BoneWeight[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) pesos[i] = Pesos(Vector3.Dot(vertices[i], rumo), juntas, 0, 1);
                malha.boneWeights = pesos;
                var poses = new Matrix4x4[1 + VaraQueEntorta.Ossos];
                poses[0] = Matrix4x4.identity;
                for (int k = 0; k < VaraQueEntorta.Ossos; k++) poses[k + 1] = Matrix4x4.Translate(-rumo * juntas[k]);
                malha.bindposes = poses;
                Gravar(malha, MalhaDoCorpo);

                var pele = vara.gameObject.AddComponent<SkinnedMeshRenderer>();
                pele.sharedMaterials = materiais;
                pele.shadowCastingMode = sombra;
                pele.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDoCorpo);
                pele.bones = new[] { vara }.Concat(ossos).ToArray();
                pele.rootBone = vara;
                var caixa = malha.bounds;
                caixa.Expand(2.4f);   // (a ponta entortada sai da caixa da vara reta)
                pele.localBounds = caixa;
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDoCorpo);
                return $"montada ({vertices.Length} vértices, {novos} novos; as juntas a {string.Join(", ", juntas.Select(j => j.ToString("0.00")))} m)";
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        // ------------------------------------------------------------------ os braços com a vara

        static string MontarOsBracos()
        {
            var fonte = MalhaDoFbx(MontagemDaVara.FbxFP, "FP_Bracos_Vara");
            if (fonte == null) return "sem a malha \"FP_Bracos_Vara\" do FBX";
            var raiz = PrefabUtility.LoadPrefabContents(PrefabDosBracos);
            try
            {
                var pele = raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
                if (pele == null) return "sem a malha dos braços";
                // o que já estava montado sai: os ossos da corrente da lista e a malha volta a ser a do FBX
                var lista = pele.bones.Where(o => o == null || !o.name.StartsWith(VaraQueEntorta.Prefixo)).ToArray();
                if (lista.Length != fonte.bindposes.Length) return $"a lista de ossos ({lista.Length}) não bate com a malha do FBX ({fonte.bindposes.Length})";
                int iVara = System.Array.FindIndex(lista, o => o != null && o.name == "vara");
                var vara = iVara >= 0 ? lista[iVara] : null;
                var ponta = vara != null ? Achar(vara, "ponta") : null;
                if (vara == null || ponta == null) return "sem o osso \"vara\" ou a \"ponta\"";
                TirarACorrente(vara);

                Vector3 rumo = ponta.localPosition.normalized;
                float comprimento = ponta.localPosition.magnitude;
                var ossos = Corrente(vara, rumo, comprimento, out var juntas);
                var poses = fonte.bindposes;
                var naVara = poses[iVara];
                var pesosDaFonte = fonte.boneWeights;
                // só a vara (a submalha 1) é subdividida; os braços e a manivela ficam
                var malha = Subdividir(fonte, 1, v => Vector3.Dot(naVara.MultiplyPoint3x4(v), rumo), Comeco - Mistura, out int novos);
                malha.name = "FP_Bracos_Vara_QueEntorta";
                var vertices = malha.vertices;
                var pesos = malha.boneWeights;
                int n = lista.Length, mexidos = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = pesos[i];
                    if (p.boneIndex0 != iVara || p.weight0 < 0.999f) continue;   // (a manivela e os braços ficam)
                    pesos[i] = Pesos(Vector3.Dot(naVara.MultiplyPoint3x4(vertices[i]), rumo), juntas, iVara, n);
                    mexidos++;
                }
                malha.boneWeights = pesos;
                var novas = new Matrix4x4[n + VaraQueEntorta.Ossos];
                System.Array.Copy(poses, novas, n);
                for (int k = 0; k < VaraQueEntorta.Ossos; k++) novas[n + k] = Matrix4x4.Translate(-rumo * juntas[k]) * naVara;
                malha.bindposes = novas;
                Gravar(malha, MalhaDosBracos);

                pele.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MalhaDosBracos);
                pele.bones = lista.Concat(ossos).ToArray();
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDosBracos);
                return $"montados ({mexidos} vértices da vara, {novos} novos; as juntas a {string.Join(", ", juntas.Select(j => j.ToString("0.00")))} m)";
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        // ------------------------------------------------------------------ a subdivisão

        /// <summary>
        /// Uma cópia de 'fonte' com os triângulos da submalha 'sub' subdivididos ao longo da vara ('ao': o lugar de um vértice
        /// ao longo dela, em m): toda aresta com mais de <see cref="Passo"/> nessa direção, e com alguma parte depois de
        /// 'desde', ganha um vértice no meio (o mesmo para os dois triângulos dela: sem rachar), e cada triângulo se refaz pelas
        /// arestas cortadas (1, 2 ou 3), até nenhuma passar. Os vértices novos misturam os das pontas da aresta (o lugar, a
        /// normal, a tangente, os UV, a cor e os pesos). As outras submalhas ficam como estão.
        /// </summary>
        static Mesh Subdividir(Mesh fonte, int sub, System.Func<Vector3, float> ao, float desde, out int novos)
        {
            var pos = new List<Vector3>(fonte.vertices);
            var nor = new List<Vector3>(fonte.normals);
            var tan = new List<Vector4>(fonte.tangents);
            var cor = new List<Color>(fonte.colors);
            var pes = new List<BoneWeight>(fonte.boneWeights);
            var uvs = new List<List<Vector4>>();
            for (int c = 0; c < 8; c++)
            {
                var l = new List<Vector4>();
                fonte.GetUVs(c, l);
                uvs.Add(l);
            }
            int antes = pos.Count;
            var tris = new List<int>(fonte.GetTriangles(sub));
            var meios = new Dictionary<long, int>();

            int Meio(int a, int b)
            {
                long chave = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (meios.TryGetValue(chave, out int m)) return m;
                m = pos.Count;
                pos.Add((pos[a] + pos[b]) * 0.5f);
                if (nor.Count > 0) nor.Add((nor[a] + nor[b]).normalized);
                if (tan.Count > 0) { var t = (tan[a] + tan[b]) * 0.5f; var t3 = ((Vector3)t).normalized; tan.Add(new Vector4(t3.x, t3.y, t3.z, tan[a].w)); }
                if (cor.Count > 0) cor.Add((cor[a] + cor[b]) * 0.5f);
                if (pes.Count > 0) pes.Add(pes[a].weight0 >= pes[b].weight0 ? pes[a] : pes[b]);
                foreach (var l in uvs) if (l.Count > 0) l.Add((l[a] + l[b]) * 0.5f);
                meios[chave] = m;
                return m;
            }
            bool Longa(int a, int b)
            {
                float pa = ao(pos[a]), pb = ao(pos[b]);
                return Mathf.Abs(pa - pb) > Passo && Mathf.Max(pa, pb) > desde;
            }

            for (int volta = 0; volta < 12; volta++)
            {
                var saida = new List<int>(tris.Count * 2);
                bool mudou = false;
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    bool ab = Longa(a, b), bc = Longa(b, c), ca = Longa(c, a);
                    int cortes = (ab ? 1 : 0) + (bc ? 1 : 0) + (ca ? 1 : 0);
                    if (cortes == 0) { saida.Add(a); saida.Add(b); saida.Add(c); continue; }
                    mudou = true;
                    // gira o triângulo (sem mudar a ordem) para os casos ficarem iguais
                    if (cortes == 1)
                    {
                        if (bc) (a, b, c) = (b, c, a);
                        else if (ca) (a, b, c) = (c, a, b);
                        int m = Meio(a, b);
                        saida.AddRange(new[] { a, m, c, m, b, c });
                    }
                    else if (cortes == 2)
                    {
                        // a aresta que fica inteira vira a "ca"
                        if (!ab) (a, b, c) = (b, c, a);
                        else if (!bc) (a, b, c) = (c, a, b);
                        int m1 = Meio(a, b), m2 = Meio(b, c);
                        saida.AddRange(new[] { m1, b, m2, a, m1, m2, a, m2, c });
                    }
                    else
                    {
                        int m1 = Meio(a, b), m2 = Meio(b, c), m3 = Meio(c, a);
                        saida.AddRange(new[] { a, m1, m3, m1, b, m2, m3, m2, c, m1, m2, m3 });
                    }
                }
                tris = saida;
                if (!mudou) break;
            }

            var malha = Object.Instantiate(fonte);
            malha.indexFormat = pos.Count > 65000 ? IndexFormat.UInt32 : malha.indexFormat;
            var outras = new List<int[]>();
            for (int s = 0; s < fonte.subMeshCount; s++) outras.Add(s == sub ? tris.ToArray() : fonte.GetTriangles(s));
            malha.SetVertices(pos);
            if (nor.Count > 0) malha.SetNormals(nor);
            if (tan.Count > 0) malha.SetTangents(tan);
            if (cor.Count > 0) malha.SetColors(cor);
            for (int c = 0; c < uvs.Count; c++) if (uvs[c].Count > 0) malha.SetUVs(c, uvs[c]);
            if (pes.Count > 0) malha.boneWeights = pes.ToArray();
            malha.subMeshCount = outras.Count;
            for (int s = 0; s < outras.Count; s++) malha.SetTriangles(outras[s], s, false);
            malha.RecalculateBounds();
            novos = pos.Count - antes;
            return malha;
        }

        // ------------------------------------------------------------------ ajudantes

        /// <summary>
        /// Os 4 ossos, cada um filho do anterior, o primeiro filho de 'base', ao longo de 'rumo' (no espaço da base): as juntas a
        /// <see cref="Comeco"/> e daí até a ponta em 4 pedaços iguais; no último, a ponta nova. 'juntas': a distância de cada
        /// junta desde a base (m).
        /// </summary>
        static Transform[] Corrente(Transform baseDaVara, Vector3 rumo, float comprimento, out float[] juntas)
        {
            juntas = new float[VaraQueEntorta.Ossos];
            float pedaco = (comprimento - Comeco) / VaraQueEntorta.Ossos;
            var ossos = new Transform[VaraQueEntorta.Ossos];
            Transform pai = baseDaVara;
            for (int k = 0; k < VaraQueEntorta.Ossos; k++)
            {
                juntas[k] = Comeco + pedaco * k;
                var osso = new GameObject(VaraQueEntorta.Prefixo + (k + 1)).transform;
                osso.SetParent(pai, false);
                osso.localPosition = rumo * (k == 0 ? Comeco : pedaco);
                osso.localRotation = Quaternion.identity;
                osso.localScale = Vector3.one;
                ossos[k] = pai = osso;
            }
            var ponta = new GameObject(VaraQueEntorta.NomeDaPonta).transform;
            ponta.SetParent(pai, false);
            ponta.localPosition = rumo * pedaco;
            return ossos;
        }

        // a corrente de uma montagem anterior (o Dobra1, com os outros e a ponta nova dentro)
        static void TirarACorrente(Transform baseDaVara)
        {
            for (int i = baseDaVara.childCount - 1; i >= 0; i--)
                if (baseDaVara.GetChild(i).name == VaraQueEntorta.Prefixo + "1") Object.DestroyImmediate(baseDaVara.GetChild(i).gameObject);
        }

        /// <summary>
        /// Os pesos de um vértice da vara a 'ao' m da base: o osso do pedaço onde ele está (antes da primeira junta, o da base),
        /// e perto de uma junta a mistura suave com o osso de antes. 'baseIndice': o índice do osso da base na lista;
        /// 'primeiro': o do Dobra1 (os outros seguem).
        /// </summary>
        static BoneWeight Pesos(float ao, float[] juntas, int baseIndice, int primeiro)
        {
            int Indice(int pedaco) => pedaco == 0 ? baseIndice : primeiro + pedaco - 1;
            for (int j = 0; j < juntas.Length; j++)
                if (Mathf.Abs(ao - juntas[j]) < Mistura)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (ao - juntas[j] + Mistura) / (2f * Mistura));
                    // (o peso maior primeiro)
                    return t >= 0.5f
                        ? new BoneWeight { boneIndex0 = Indice(j + 1), weight0 = t, boneIndex1 = Indice(j), weight1 = 1f - t }
                        : new BoneWeight { boneIndex0 = Indice(j), weight0 = 1f - t, boneIndex1 = Indice(j + 1), weight1 = t };
                }
            int dentro = 0;
            for (int j = 0; j < juntas.Length; j++) if (ao >= juntas[j]) dentro = j + 1;
            return new BoneWeight { boneIndex0 = Indice(dentro), weight0 = 1f };
        }

        static Mesh MalhaDoFbx(string fbx, string nome) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(m => m.name == nome);

        static void Gravar(Mesh malha, string caminho)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(caminho) != null) AssetDatabase.DeleteAsset(caminho);
            AssetDatabase.CreateAsset(malha, caminho);
        }

        static Transform Achar(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
            return null;
        }
    }
}
