using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A mão nova do personagem no corpo das roupas (o <c>Blender/maos_novas.py</c>, 08/11/2026: a mão mais redonda, com
    /// as unhas e alguns pelos, e o antebraço que afina aos poucos até o pulso, numa malha só por lado; aprovada pelo Pedro,
    /// com a versão da mulher sem os pelos), e as duas luvas refeitas em volta dela. Lê os FBX (nenhum é gravado de novo) e
    /// grava as malhas em <c>Art/Characters/Roupas/MaoNova</c>:
    ///  - <c>NPC_Corpo</c>: o do <c>Roupas.fbx</c> sem o braço inteiro de cada lado (as faces com algum vértice com mais da
    ///    metade do peso no upper_arm, no forearm ou nos ossos da mão e dos dedos daquele lado: 545 faces e 994 triângulos por
    ///    lado), mais as peças <c>BracoCima_Corpo.L/R</c> (o braço de cima arredondado, 09/11/2026, a pedido: "o braço está
    ///    um pouco mais quadrado e o antebraço mais redondo"; o <c>Blender/braco_de_cima.py</c>), que começam na borda do
    ///    ombro, e <c>Mao_Corpo.L/R</c> do <c>Maos_Novas_Corpo.fbx</c>, que começam no anel do cotovelo (as emendas
    ///    fecham). Os pesos vão pelos nomes dos ossos, nas poses de ligação do corpo; as formas: a "Mulher" (a do corpo nos vértices de hoje e a da peça nos novos: os pelos se
    ///    recolhem) e a "SemPelos" (só nas peças; zero no resto);
    ///  - <c>Roupa_Luva_Tatica</c> e <c>Roupa_Luva_Sem_Dedos</c>: as do <c>Luvas_Novas.fbx</c>, nos ossos e nas poses de
    ///    ligação das de hoje (o mesmo esqueleto). O que cada uma esconde do corpo novo está no <c>Roupas.json</c>.
    /// As malhas ficam no espaço do <c>NPC_Corpo</c> (o mesmo dos FBX novos) e com as poses de ligação dele: nos FBX novos
    /// a do hand.L sai do Blender girada (2,4° na mão e 1,7° nas luvas; as malhas são simétricas e a do hand.R bate), e com
    /// ela a mão esquerda ficaria torta em relação ao antebraço e às luvas.
    /// O ponto da troca é <see cref="Malha"/>: quem monta um corpo a partir do <c>Roupas.fbx</c> (o prefab do NPC, pela
    /// <see cref="MontagemDasRoupas"/>, e o corpo do jogador, pela <see cref="MontagemDaRede"/>) pega por ele a malha nova do
    /// mesmo nome, quando ela existe.
    /// Menu: Zombie City Cleaner > Roupas e NPCs > Montar a mão nova (a montagem das roupas também roda este passo).
    /// </summary>
    public static class MontagemDaMaoNova
    {
        public const string Pasta = MontagemDasRoupas.Arte + "/MaoNova";
        public const string FbxDaMao = MontagemDasArmas.ArtePersonagens + "/Maos_Novas_Corpo.fbx";
        public const string FbxDasLuvas = MontagemDasRoupas.Arte + "/Luvas_Novas.fbx";
        static readonly string[] PecasDaMao = { "Mao_Corpo.L", "Mao_Corpo.R", "BracoCima_Corpo.L", "BracoCima_Corpo.R" };
        static readonly string[] Luvas = { "Roupa_Luva_Tatica", "Roupa_Luva_Sem_Dedos" };
        // os ossos que saem com o corte, de cada lado (o braço de cima, o antebraço, a mão e os dedos)
        static readonly string[] OssosDoCorte = { "upper_arm", "forearm", "hand", "thumb", "thumb2", "index1", "index2", "middle1", "middle2", "ring1", "ring2", "pinky1", "pinky2" };
        // a diferença das poses de ligação (dos FBX novos para as do corpo) que passa sem aviso: o giro (graus) e o lugar (m)
        const float FolgaDoGiro = 3f, FolgaDoLugar = 0.001f;

        public static string CaminhoDa(string nome) => $"{Pasta}/{nome}.asset";

        /// <summary>
        /// A malha que um corpo montado do <c>Roupas.fbx</c> usa no lugar da deste renderer: a nova do mesmo nome (a pasta
        /// da mão nova), se houver; senão, a do FBX. As novas têm os ossos na mesma ordem e as mesmas poses de ligação.
        /// </summary>
        public static Mesh Malha(SkinnedMeshRenderer smr)
        {
            var nova = AssetDatabase.LoadAssetAtPath<Mesh>(CaminhoDa(smr.name));
            return nova != null ? nova : smr.sharedMesh;
        }

        [MenuItem("Zombie City Cleaner/Roupas e NPCs/Montar a mão nova")]
        public static void Montar()
        {
            var roupas = AssetDatabase.LoadAssetAtPath<GameObject>(MontagemDasRoupas.Fbx);
            if (roupas == null) throw new FileNotFoundException("Falta o Roupas.fbx (o roupas_exportar.py da pasta de arte)", MontagemDasRoupas.Fbx);
            var maos = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDaMao);
            var luvas = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDasLuvas);
            if (maos == null || luvas == null)
            {
                Debug.LogWarning($"[Mão nova] Faltam o {FbxDaMao} ou o {FbxDasLuvas} (o Blender/maos_novas.py): o corpo fica com a mão de hoje.");
                return;
            }
            Directory.CreateDirectory(Pasta);
            var relato = new List<string>();
            var corpo = Renderer(roupas, Vestimenta.Corpo, MontagemDasRoupas.Fbx);
            Gravar(MontarCorpo(corpo, PecasDaMao.Select(n => Renderer(maos, n, FbxDaMao)).ToArray(), relato));
            foreach (var nome in Luvas)
                Gravar(MontarPeca(Renderer(roupas, nome, MontagemDasRoupas.Fbx), Renderer(luvas, nome, FbxDasLuvas), relato));
            AssetDatabase.SaveAssets();
            Debug.Log("[Mão nova] " + string.Join("; ", relato) + $". As malhas estão em {Pasta}.");
        }

        static SkinnedMeshRenderer Renderer(GameObject modelo, string nome, string caminho)
        {
            var r = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == nome);
            if (r == null || r.sharedMesh == null) throw new FileNotFoundException($"O {nome} não está no FBX", caminho);
            return r;
        }

        /// <summary>Grava a malha na pasta (por cima da que já existe, que guarda o GUID: os prefabs continuam apontando).</summary>
        static void Gravar(Mesh m)
        {
            string caminho = CaminhoDa(m.name);
            var velha = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
            if (velha == null) AssetDatabase.CreateAsset(m, caminho);
            else
            {
                EditorUtility.CopySerialized(m, velha);
                velha.name = m.name;
                EditorUtility.SetDirty(velha);
                Object.DestroyImmediate(m);
            }
        }

        // ------------------------------------------------------------------ o corpo

        static Mesh MontarCorpo(SkinnedMeshRenderer corpo, SkinnedMeshRenderer[] pecas, List<string> relato)
        {
            var m = corpo.sharedMesh;
            var nomes = corpo.bones.Select(b => b.name).ToArray();
            var faces = NumerosDasFaces(m);
            var pesos = m.boneWeights;
            // as faces que saem: as com algum vértice com mais da metade do peso num osso do corte (de um lado ou do outro)
            var cortadas = new HashSet<int>();
            foreach (var lado in new[] { "L", "R" })
            {
                var doCorte = nomes.Select(n => OssosDoCorte.Any(o => n == o + "." + lado)).ToArray();
                var doLado = new HashSet<int>();
                for (int v = 0; v < pesos.Length; v++)
                    if (Pesado(pesos[v], doCorte)) doLado.Add(faces[v]);
                int tris = 0;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var t = m.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3) if (doLado.Contains(faces[t[i]])) tris++;
                }
                relato.Add($"o corte do lado {lado}: {doLado.Count} faces, {tris} triângulos");
                cortadas.UnionWith(doLado);
            }
            var junta = new Juntar(nomes, m.bindposes, m.subMeshCount);
            junta.Por(m, nomes, Matrix4x4.identity, v => !cortadas.Contains(faces[v]));
            int velhos = junta.Vertices;
            foreach (var p in pecas)
                junta.Por(p.sharedMesh, p.bones.Select(b => b.name).ToArray(), Levar(corpo, p, relato), v => true);
            var nova = junta.Malha(m.name);
            relato.Add($"o {m.name}: {m.vertexCount} vértices e {Triangulos(m)} triângulos no FBX; {velhos} vértices ficam, " +
                       $"as peças põem {nova.vertexCount - velhos} ({string.Join(" e ", pecas.Select(p => $"{p.name} {p.sharedMesh.vertexCount} e {Triangulos(p.sharedMesh)} triângulos"))}): " +
                       $"{nova.vertexCount} vértices e {Triangulos(nova)} triângulos; as formas {string.Join(", ", Enumerable.Range(0, nova.blendShapeCount).Select(nova.GetBlendShapeName))}");
            relato.Add(Emenda(m, faces, cortadas, pecas, nova, velhos));
            return nova;
        }

        // o número da face de cada vértice (o UV1.x)
        static int[] NumerosDasFaces(Mesh m)
        {
            var uv = new List<Vector2>();
            m.GetUVs(1, uv);
            if (uv.Count != m.vertexCount) throw new System.Exception($"O {m.name} não tem o UV1 com o número das faces.");
            return uv.Select(u => Mathf.FloorToInt(u.x)).ToArray();
        }

        static bool Pesado(BoneWeight w, bool[] ossos) =>
            (w.weight0 > 0.5f && ossos[w.boneIndex0]) || (w.weight1 > 0.5f && ossos[w.boneIndex1]) ||
            (w.weight2 > 0.5f && ossos[w.boneIndex2]) || (w.weight3 > 0.5f && ossos[w.boneIndex3]);

        static int Triangulos(Mesh m) => Enumerable.Range(0, m.subMeshCount).Sum(s => (int)m.GetIndexCount(s) / 3);

        /// <summary>
        /// A emenda: os vértices do corpo que ficam na borda do corte (desde 09/11/2026, os da borda do ombro; antes, os do
        /// anel aberto do fim do braço de cima) e a distância de cada um ao vértice mais perto das peças novas.
        /// </summary>
        static string Emenda(Mesh velho, int[] faces, HashSet<int> cortadas, SkinnedMeshRenderer[] pecas, Mesh nova, int velhos)
        {
            var v = velho.vertices;
            var t = velho.triangles;
            var noCorte = new HashSet<Vector3Int>();
            var fora = new HashSet<Vector3Int>();
            System.Func<Vector3, Vector3Int> chave = p => Vector3Int.RoundToInt(p * 10000f);   // 0,1 mm
            for (int i = 0; i < t.Length; i += 3)
            {
                var alvo = cortadas.Contains(faces[t[i]]) ? noCorte : fora;
                for (int k = 0; k < 3; k++) alvo.Add(chave(v[t[i + k]]));
            }
            var borda = v.Where(p => noCorte.Contains(chave(p)) && fora.Contains(chave(p))).Select(chave).Distinct().ToArray();
            var novos = nova.vertices.Skip(velhos).ToArray();
            float pior = 0;
            foreach (var b in borda)
            {
                var p = (Vector3)b / 10000f;
                pior = Mathf.Max(pior, novos.Min(q => (q - p).magnitude));
            }
            return $"a emenda: {borda.Length} vértices na borda do corte, o mais longe a {pior * 1000f:0.###} mm de um vértice das peças";
        }

        // ------------------------------------------------------------------ as luvas

        static Mesh MontarPeca(SkinnedMeshRenderer velha, SkinnedMeshRenderer nova, List<string> relato)
        {
            var nomes = velha.bones.Select(b => b.name).ToArray();
            var junta = new Juntar(nomes, velha.sharedMesh.bindposes, velha.sharedMesh.subMeshCount);
            junta.Por(nova.sharedMesh, nova.bones.Select(b => b.name).ToArray(), Levar(velha, nova, relato), v => true);
            var m = junta.Malha(velha.name);
            relato.Add($"a {velha.name}: {Triangulos(velha.sharedMesh)} triângulos hoje, {Triangulos(m)} na nova ({m.vertexCount} vértices)");
            return m;
        }

        /// <summary>
        /// Do espaço da malha nova para o da de hoje, pelas poses de ligação do root (no mesmo esqueleto, a mesma conta serve
        /// para todos os ossos; os que diferem mais que a folga vão para o relato, e ficam as poses de hoje).
        /// </summary>
        static Matrix4x4 Levar(SkinnedMeshRenderer hoje, SkinnedMeshRenderer nova, List<string> relato)
        {
            var deHoje = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < hoje.bones.Length; i++) deHoje[hoje.bones[i].name] = hoje.sharedMesh.bindposes[i];
            var ligacao = nova.sharedMesh.bindposes;
            int raiz = System.Array.FindIndex(nova.bones, b => b.name == hoje.rootBone.name);
            if (raiz < 0) throw new System.Exception($"O {nova.name} não tem o osso {hoje.rootBone.name}.");
            var levar = deHoje[hoje.rootBone.name].inverse * ligacao[raiz];
            var usados = OssosUsados(nova);
            var diferentes = new List<string>();
            for (int i = 0; i < nova.bones.Length; i++)
            {
                if (!usados.Contains(i) || !deHoje.TryGetValue(nova.bones[i].name, out var h)) continue;
                var a = h.inverse;
                var b = levar * ligacao[i].inverse;
                float giro = Quaternion.Angle(a.rotation, b.rotation), lugar = ((Vector3)(a.GetColumn(3) - b.GetColumn(3))).magnitude;
                if (giro > 0.05f || lugar > 0.0001f) diferentes.Add($"{nova.bones[i].name} {giro:0.##}° e {lugar * 1000f:0.##} mm");
                if (giro > FolgaDoGiro || lugar > FolgaDoLugar)
                    Debug.LogWarning($"[Mão nova] A pose de ligação do {nova.bones[i].name} no {nova.name} difere da do Roupas.fbx em {giro:0.##}° e {lugar * 1000f:0.##} mm: o esqueleto mudou?");
            }
            relato.Add($"o repouso do {nova.name}: " + (diferentes.Count == 0 ? "igual ao do Roupas.fbx" : "difere no " + string.Join(", ", diferentes) + " (ficam as poses de hoje)"));
            return levar;
        }

        static HashSet<int> OssosUsados(SkinnedMeshRenderer r)
        {
            var usados = new HashSet<int>();
            foreach (var w in r.sharedMesh.boneWeights)
            {
                if (w.weight0 > 0) usados.Add(w.boneIndex0);
                if (w.weight1 > 0) usados.Add(w.boneIndex1);
                if (w.weight2 > 0) usados.Add(w.boneIndex2);
                if (w.weight3 > 0) usados.Add(w.boneIndex3);
            }
            return usados;
        }

        // ------------------------------------------------------------------ juntar as malhas

        /// <summary>
        /// Junta pedaços de malhas numa nova, com os ossos e as poses de ligação dados: os vértices das faces que ficam (as
        /// posições, as normais, as tangentes, os UV0 e UV1 e as cores, levados para o espaço da nova), as faces (cada submalha
        /// na mesma da nova, ou na última dela), os pesos (pelos nomes dos ossos) e as formas (pelo nome, com um quadro; um
        /// pedaço sem a forma fica com zero nela).
        /// </summary>
        class Juntar
        {
            readonly string[] _ossos;
            readonly Matrix4x4[] _ligacao;
            readonly List<int>[] _faces;
            readonly List<Vector3> _pos = new List<Vector3>(), _normais = new List<Vector3>();
            readonly List<Vector4> _tangentes = new List<Vector4>();
            readonly List<Vector2> _uv0 = new List<Vector2>(), _uv1 = new List<Vector2>();
            readonly List<Color> _cores = new List<Color>();
            readonly List<BoneWeight> _pesos = new List<BoneWeight>();
            readonly List<Forma> _formas = new List<Forma>();
            bool _temCores;

            class Forma
            {
                public string nome;
                public float peso;
                public readonly List<Vector3> pos = new List<Vector3>(), normais = new List<Vector3>(), tangentes = new List<Vector3>();
            }

            public int Vertices => _pos.Count;

            public Juntar(string[] ossos, Matrix4x4[] ligacao, int submalhas)
            {
                _ossos = ossos;
                _ligacao = ligacao;
                _faces = Enumerable.Range(0, Mathf.Max(1, submalhas)).Select(_ => new List<int>()).ToArray();
            }

            /// <summary>Os triângulos de m cujo primeiro vértice passa em <paramref name="fica"/>, com os vértices deles.</summary>
            public void Por(Mesh m, string[] ossosDela, Matrix4x4 levar, System.Func<int, bool> fica)
            {
                var osso = ossosDela.Select(n => System.Array.IndexOf(_ossos, n)).ToArray();
                var novo = Enumerable.Repeat(-1, m.vertexCount).ToArray();
                var entram = new List<int>();
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var t = m.GetTriangles(s);
                    var alvo = _faces[Mathf.Min(s, _faces.Length - 1)];
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        if (!fica(t[i])) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int v = t[i + k];
                            if (novo[v] < 0)
                            {
                                novo[v] = _pos.Count + entram.Count;
                                entram.Add(v);
                            }
                            alvo.Add(novo[v]);
                        }
                    }
                }

                var pos = m.vertices;
                var normais = m.normals;
                var tangentes = m.tangents;
                var uv0 = new List<Vector2>();
                var uv1 = new List<Vector2>();
                m.GetUVs(0, uv0);
                m.GetUVs(1, uv1);
                var cores = m.colors;
                if (cores.Length > 0 && !_temCores)
                {
                    _temCores = true;
                    for (int i = 0; i < _pos.Count; i++) _cores.Add(Color.white);
                }
                var pesos = m.boneWeights;
                foreach (int v in entram)
                {
                    _pos.Add(levar.MultiplyPoint3x4(pos[v]));
                    _normais.Add(normais.Length > 0 ? levar.MultiplyVector(normais[v]).normalized : Vector3.up);
                    if (tangentes.Length > 0)
                    {
                        var d = levar.MultiplyVector(tangentes[v]).normalized;
                        _tangentes.Add(new Vector4(d.x, d.y, d.z, tangentes[v].w));
                    }
                    else _tangentes.Add(new Vector4(1f, 0f, 0f, 1f));
                    _uv0.Add(uv0.Count > 0 ? uv0[v] : Vector2.zero);
                    _uv1.Add(uv1.Count > 0 ? uv1[v] : Vector2.zero);
                    if (_temCores) _cores.Add(cores.Length > 0 ? cores[v] : Color.white);
                    _pesos.Add(Peso(pesos[v], osso, ossosDela, m.name));
                }

                // as formas: as que já existem (zero se m não tem) e as novas de m (zero nos vértices de antes)
                int antes = _pos.Count - entram.Count;
                for (int f = 0; f < m.blendShapeCount; f++)
                    if (!_formas.Any(x => x.nome == m.GetBlendShapeName(f)))
                    {
                        var nova = new Forma { nome = m.GetBlendShapeName(f), peso = m.GetBlendShapeFrameWeight(f, m.GetBlendShapeFrameCount(f) - 1) };
                        for (int i = 0; i < antes; i++) Zero(nova);
                        _formas.Add(nova);
                    }
                var dv = new Vector3[m.vertexCount];
                var dn = new Vector3[m.vertexCount];
                var dt = new Vector3[m.vertexCount];
                foreach (var forma in _formas)
                {
                    int f = m.GetBlendShapeIndex(forma.nome);
                    if (f < 0)
                    {
                        foreach (int _ in entram) Zero(forma);
                        continue;
                    }
                    if (m.GetBlendShapeFrameCount(f) != 1) throw new System.Exception($"A forma {forma.nome} do {m.name} tem {m.GetBlendShapeFrameCount(f)} quadros (aqui, só um).");
                    m.GetBlendShapeFrameVertices(f, 0, dv, dn, dt);
                    foreach (int v in entram)
                    {
                        forma.pos.Add(levar.MultiplyVector(dv[v]));
                        forma.normais.Add(levar.MultiplyVector(dn[v]));
                        forma.tangentes.Add(levar.MultiplyVector(dt[v]));
                    }
                }
            }

            static void Zero(Forma f)
            {
                f.pos.Add(Vector3.zero);
                f.normais.Add(Vector3.zero);
                f.tangentes.Add(Vector3.zero);
            }

            // o peso com os ossos da malha nova (pelo nome); um osso com peso que ela não tem é erro
            BoneWeight Peso(BoneWeight w, int[] osso, string[] nomes, string malha)
            {
                int Osso(int i, float peso)
                {
                    if (peso <= 0f) return 0;
                    if (osso[i] < 0) throw new System.Exception($"O {malha} tem peso no osso {nomes[i]}, que o corpo não tem.");
                    return osso[i];
                }
                w.boneIndex0 = Osso(w.boneIndex0, w.weight0);
                w.boneIndex1 = Osso(w.boneIndex1, w.weight1);
                w.boneIndex2 = Osso(w.boneIndex2, w.weight2);
                w.boneIndex3 = Osso(w.boneIndex3, w.weight3);
                return w;
            }

            public Mesh Malha(string nome)
            {
                var m = new Mesh { name = nome };
                if (_pos.Count > 65535) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(_pos);
                m.SetNormals(_normais);
                m.SetTangents(_tangentes);
                m.SetUVs(0, _uv0);
                m.SetUVs(1, _uv1);
                if (_temCores) m.SetColors(_cores);
                m.boneWeights = _pesos.ToArray();
                m.bindposes = _ligacao;
                m.subMeshCount = _faces.Length;
                for (int s = 0; s < _faces.Length; s++) m.SetTriangles(_faces[s], s, false);
                foreach (var f in _formas) m.AddBlendShapeFrame(f.nome, f.peso, f.pos.ToArray(), f.normais.ToArray(), f.tangentes.ToArray());
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
