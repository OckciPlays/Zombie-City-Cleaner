using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As mangas e as luvas na primeira pessoa (a parte 5 do Plano_Personagem.md da pasta de arte, 31/10/2026): os braços de
    /// primeira pessoa deixam de ser a pele do personagem principal e passam a ser os braços do próprio corpo vestido (o
    /// <c>NPC_Corpo</c> e as roupas da <see cref="Roupas.Vestimenta"/>: a pele escolhida, as mangas da camiseta, da camisa e
    /// da jaqueta e as luvas, com as cores delas e, na mulher, a forma dela, mais fina). De cada malha vestida sai uma cópia
    /// só com os triângulos dos braços (os vértices com metade do peso ou mais nos ossos do braço, da mão e dos dedos),
    /// presa aos ossos do braço de primeira pessoa do que está na mão (os mesmos nomes); o braço e o antebraço de lá são 12%
    /// mais compridos, e a cópia estica junto (um osso de ajuda, filho de cada um, com a escala ao longo dele). A pele dos
    /// braços de primeira pessoa (a submalha 0 da malha deles; a 1 é a arma) ganha o material que não desenha nada (o
    /// shader <c>ZombieCityCleaner/Invisivel</c>, do <see cref="Roupas.ItensDasRoupas"/>): a malha dos braços de uns itens (a
    /// lata, a garrafa) não deixa ser mexida, e a troca dos braços da lata pelo item do saque (o
    /// <see cref="ItemNaMaoProprio"/>) mantém o material 0. Só no dono; refeito quando a roupa ou o que está na mão muda.
    /// </summary>
    public class RoupasNosBracos : MonoBehaviour
    {
        static readonly string[] OssosDoBraco = { "upper_arm", "forearm", "hand", "thumb", "thumb2", "index1", "index2", "middle1", "middle2", "ring1", "ring2", "pinky1", "pinky2" };
        static readonly string[] Nomes = NomesDosOssos();
        // o vértice é do braço com pelo menos esta parte do peso nos ossos do braço (os do ombro ficam de fora)
        const float PesoDoBraco = 0.5f;

        static string[] NomesDosOssos()
        {
            var nomes = new string[OssosDoBraco.Length * 2];
            for (int k = 0; k < OssosDoBraco.Length; k++)
            {
                nomes[k] = OssosDoBraco[k] + ".L";
                nomes[OssosDoBraco.Length + k] = OssosDoBraco[k] + ".R";
            }
            return nomes;
        }

        JogadorRede _rede;
        ArmaJogador _arma;
        int _modelo = -1, _versao = -1;
        SkinnedMeshRenderer _fp;
        // as cópias vestidas e o material que esconde a pele dos braços de primeira pessoa
        readonly List<SkinnedMeshRenderer> _pecas = new List<SkinnedMeshRenderer>();
        Material _semPele;
        // os ossos de cada braço de primeira pessoa (com os de ajuda que esticam) e as malhas feitas
        readonly Dictionary<int, Transform[]> _ossos = new Dictionary<int, Transform[]>();
        readonly Dictionary<Mesh, Mesh> _bracosDe = new Dictionary<Mesh, Mesh>();
        MaterialPropertyBlock _bloco;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
        }

        void LateUpdate()
        {
            if (_rede == null || !_rede.IsOwner || _arma == null || _arma.modelos == null || _rede.pintaveis == null) return;
            var vestimenta = _rede.Vestimenta;
            if (vestimenta == null || !_rede.Criado) return;
            int m = -1;
            for (int i = 0; i < _arma.modelos.Length; i++)
                if (_arma.modelos[i] != null && _arma.modelos[i].activeSelf)
                {
                    m = i;
                    break;
                }
            if (m < 0 || m >= _rede.pintaveis.Length || !(_rede.pintaveis[m] is SkinnedMeshRenderer fp) || fp.sharedMesh == null) return;
            if (_semPele == null)
            {
                var roupas = Roupas.ItensDasRoupas.Carregar();
                if (roupas == null || roupas.invisivel == null) return;   // sem ele, a pele ficaria por baixo das roupas
                _semPele = new Material(roupas.invisivel) { name = "Sem a pele" };
            }
            if (m != _modelo || vestimenta.Versao != _versao) Montar(m, fp, vestimenta);
            // a pele dos braços de primeira pessoa não se desenha (o material 0; o resto é o que está na mão)
            if (fp.sharedMaterial != _semPele)
            {
                var mats = fp.sharedMaterials;
                mats[0] = _semPele;
                fp.sharedMaterials = mats;
            }
        }

        void OnDestroy()
        {
            foreach (var p in _pecas) if (p != null) Destroy(p.gameObject);
            foreach (var m in _bracosDe.Values) if (m != null) Destroy(m);
            if (_semPele != null) Destroy(_semPele);
            foreach (var ossos in _ossos.Values)
                foreach (var t in ossos)
                    if (t != null && t.name.EndsWith(" (esticado)")) Destroy(t.gameObject);
        }

        void Montar(int m, SkinnedMeshRenderer fp, Roupas.Vestimenta vestimenta)
        {
            _fp = fp;
            _modelo = m;
            _versao = vestimenta.Versao;
            var raiz = fp.transform.parent;
            var ossos = OssosDe(m, raiz, vestimenta.transform);
            _bloco ??= new MaterialPropertyBlock();
            int n = 0;
            foreach (var fonte in vestimenta.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (fonte.name != Roupas.Vestimenta.Corpo && !fonte.name.StartsWith(Roupas.Vestimenta.PrefixoDasPecas)) continue;
                var malha = Bracos(fonte);
                if (malha == null) continue;
                if (n == _pecas.Count) _pecas.Add(NovaPeca());
                var p = _pecas[n++];
                p.transform.SetParent(raiz, false);
                p.gameObject.SetActive(true);
                p.name = "Nos braços: " + fonte.name;
                p.sharedMesh = malha;
                p.bones = ossos;
                p.rootBone = ossos[0];
                p.sharedMaterials = fonte.sharedMaterials;
                fonte.GetPropertyBlock(_bloco);
                p.SetPropertyBlock(_bloco);
                for (int f = 0; f < malha.blendShapeCount; f++) p.SetBlendShapeWeight(f, fonte.GetBlendShapeWeight(f));
            }
            for (int k = n; k < _pecas.Count; k++) _pecas[k].gameObject.SetActive(false);
        }

        SkinnedMeshRenderer NovaPeca()
        {
            var go = new GameObject("Nos braços") { layer = JogadorRede.CamadaBracos };
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.updateWhenOffscreen = true;
            return r;
        }

        /// <summary>
        /// Os ossos do braço de primeira pessoa, na ordem de <see cref="Nomes"/>; no braço e no antebraço, um filho de ajuda
        /// com a escala ao longo do osso (o Y) igual a quanto o de primeira pessoa é mais comprido que o do corpo.
        /// </summary>
        Transform[] OssosDe(int m, Transform raizFP, Transform corpo)
        {
            if (_ossos.TryGetValue(m, out var prontos)) return prontos;
            var doFP = Mapa(raizFP);
            var doCorpo = Mapa(corpo);
            var ossos = new Transform[Nomes.Length];
            for (int k = 0; k < Nomes.Length; k++)
            {
                doFP.TryGetValue(Nomes[k], out var t);
                ossos[k] = t;
                if (t == null) continue;
                string lado = Nomes[k].Substring(Nomes[k].Length - 2);
                string filho = Nomes[k].StartsWith("upper_arm") ? "forearm" + lado : Nomes[k].StartsWith("forearm") ? "hand" + lado : null;
                if (filho == null || !doFP.TryGetValue(filho, out var f) || !doCorpo.TryGetValue(Nomes[k], out var c) || !doCorpo.TryGetValue(filho, out var cf)) continue;
                float corpoL = Vector3.Distance(c.position, cf.position);
                float s = corpoL > 1e-4f ? Vector3.Distance(t.position, f.position) / corpoL : 1f;
                var ajuda = new GameObject(Nomes[k] + " (esticado)") { layer = JogadorRede.CamadaBracos }.transform;
                ajuda.SetParent(t, false);
                ajuda.localScale = new Vector3(1f, s, 1f);
                ossos[k] = ajuda;
            }
            _ossos[m] = ossos;
            return ossos;
        }

        static Dictionary<string, Transform> Mapa(Transform raiz)
        {
            var mapa = new Dictionary<string, Transform>();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (!mapa.ContainsKey(t.name)) mapa[t.name] = t;
            return mapa;
        }

        /// <summary>
        /// A cópia da malha vestida só com os braços: os mesmos vértices (e as formas), os triângulos com os 3 vértices no
        /// braço, os pesos só nos ossos do braço (somando 1) e as poses de ligação deles (null: nada no braço). Guardada pela
        /// malha (a Vestimenta troca a malha quando as faces cobertas mudam).
        /// </summary>
        Mesh Bracos(SkinnedMeshRenderer fonte)
        {
            var malha = fonte.sharedMesh;
            if (malha == null || !malha.isReadable) return null;
            if (_bracosDe.TryGetValue(malha, out var pronta)) return pronta;
            var ossos = fonte.bones;
            var mapa = new int[ossos.Length];
            for (int b = 0; b < ossos.Length; b++) mapa[b] = ossos[b] != null ? System.Array.IndexOf(Nomes, ossos[b].name) : -1;
            var pesos = malha.boneWeights;
            var novos = new BoneWeight[pesos.Length];
            var doBraco = new bool[pesos.Length];
            var lista = new List<(int osso, float peso)>(4);
            for (int v = 0; v < pesos.Length; v++)
            {
                var w = pesos[v];
                lista.Clear();
                void Por(int i, float p)
                {
                    if (p > 0f && i >= 0 && i < mapa.Length && mapa[i] >= 0) lista.Add((mapa[i], p));
                }
                Por(w.boneIndex0, w.weight0);
                Por(w.boneIndex1, w.weight1);
                Por(w.boneIndex2, w.weight2);
                Por(w.boneIndex3, w.weight3);
                float soma = 0f;
                foreach (var (_, p) in lista) soma += p;
                doBraco[v] = soma >= PesoDoBraco;
                if (!doBraco[v])
                {
                    novos[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                    continue;
                }
                lista.Sort((a, b) => b.peso.CompareTo(a.peso));
                var nw = new BoneWeight();
                for (int k = 0; k < lista.Count; k++)
                {
                    float p = lista[k].peso / soma;
                    switch (k)
                    {
                        case 0: nw.boneIndex0 = lista[k].osso; nw.weight0 = p; break;
                        case 1: nw.boneIndex1 = lista[k].osso; nw.weight1 = p; break;
                        case 2: nw.boneIndex2 = lista[k].osso; nw.weight2 = p; break;
                        default: nw.boneIndex3 = lista[k].osso; nw.weight3 = p; break;
                    }
                }
                novos[v] = nw;
            }
            var copia = Instantiate(malha);
            copia.name = malha.name + " (braços)";
            bool algum = false;
            var tris = new List<int>();
            for (int s = 0; s < malha.subMeshCount; s++)
            {
                var todos = malha.GetTriangles(s);
                tris.Clear();
                for (int i = 0; i + 2 < todos.Length; i += 3)
                    if (doBraco[todos[i]] && doBraco[todos[i + 1]] && doBraco[todos[i + 2]])
                    {
                        tris.Add(todos[i]);
                        tris.Add(todos[i + 1]);
                        tris.Add(todos[i + 2]);
                    }
                copia.SetTriangles(tris, s, false);
                algum |= tris.Count > 0;
            }
            if (!algum)
            {
                Destroy(copia);
                _bracosDe[malha] = null;
                return null;
            }
            var ligacao = malha.bindposes;
            var novas = new Matrix4x4[Nomes.Length];
            for (int k = 0; k < novas.Length; k++) novas[k] = Matrix4x4.identity;
            for (int b = 0; b < ossos.Length && b < ligacao.Length; b++)
                if (mapa[b] >= 0) novas[mapa[b]] = ligacao[b];
            copia.boneWeights = novos;
            copia.bindposes = novas;
            _bracosDe[malha] = copia;
            return copia;
        }
    }
}
