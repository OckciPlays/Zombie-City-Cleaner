using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os pinheiros cortados do Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 3), em
    /// cada máquina: o golpe do machado acha o pinheiro pelo colisor (<see cref="TroncoEm"/>), os entalhes ficam no tronco
    /// (<see cref="MarcarEntalhe"/>, a malha do mundo sorteado), a cortada cai (<see cref="Tombar"/>: o prefab dele, sem a
    /// colisão, com o <see cref="ArvoreCaindo"/>) e vira toco nos blocos (sem o objeto e sem o cartaz de longe). A lista das
    /// cortadas é a do jogo (<see cref="FlorestaDoMundo.Cortada"/>), que vem do <see cref="MundoDeLakewood"/>.
    /// </summary>
    public partial class LakewoodNaCena
    {
        // os tocos de cada bloco montado com os objetos, e quantas cortadas o bloco tinha quando foi montado
        readonly Dictionary<Vector2Int, List<GameObject>> _tocos = new Dictionary<Vector2Int, List<GameObject>>();
        readonly Dictionary<Vector2Int, int> _cortesDoBloco = new Dictionary<Vector2Int, int>();
        static Mesh _malhaDoToco;
        static Material _materialDoToco;

        /// <summary>A lista das cortadas mudou (FlorestaDoMundo.DefinirCortadas): os blocos com os objetos que mudaram são refeitos.</summary>
        internal void AplicarCortadas()
        {
            foreach (var b in _blocos.Values)
            {
                if (b.Arvores == null || b.Plantas == null) continue;
                int n = CortadasNoBloco(b);
                _cortesDoBloco.TryGetValue(b.Chave, out int antes);
                if (n == antes) continue;
                GuardarOsPinheiros(b);
                PorOsPinheiros(b);
            }
            _ondeJuntou = Vector3.one * 1e9f;   // (os cartazes de longe também)
        }

        static int CortadasNoBloco(Bloco b)
        {
            if (!FlorestaDoMundo.TemCortadas || b.Plantas == null) return 0;
            int n = 0;
            for (int k = 0; k < b.Plantas.Count; k++)
                if (FlorestaDoMundo.Cortada(FlorestaDoMundo.Chave(b.Chave.x, b.Chave.y, k))) n++;
            return n;
        }

        static bool CortadaNoBloco(Bloco b, int k) =>
            FlorestaDoMundo.TemCortadas && FlorestaDoMundo.Cortada(FlorestaDoMundo.Chave(b.Chave.x, b.Chave.y, k));

        // o toco no lugar do pinheiro cortado (o mesmo para todos: um tronco baixo, com o corte claro em cima)
        void PorOToco(Bloco b, PlantaDaFloresta pl)
        {
            if (_malhaDoToco == null) _malhaDoToco = MalhaDoToco();
            if (_materialDoToco == null)
            {
                _materialDoToco = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Toco do pinheiro (casca)" };
                _materialDoToco.SetColor("_BaseColor", new Color(0.30f, 0.22f, 0.16f));
                _materialDoToco.SetFloat("_Smoothness", 0.05f);
            }
            var go = new GameObject("Toco");
            go.transform.SetParent(_paiDosPinheiros, false);
            float r = ArvoresDoMundo.RaioDoTronco(pl);
            go.transform.SetPositionAndRotation(pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f));
            go.transform.localScale = new Vector3(r, 1f, r);
            go.AddComponent<MeshFilter>().sharedMesh = _malhaDoToco;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { _materialDoToco, FlorestaDoMundo.MateriaisDoEntalhe()[0] };
            mr.shadowCastingMode = ShadowCastingMode.On;
            if (!_tocos.TryGetValue(b.Chave, out var lista)) _tocos[b.Chave] = lista = new List<GameObject>();
            lista.Add(go);
        }

        void TirarOsTocos(Bloco b)
        {
            if (!_tocos.TryGetValue(b.Chave, out var lista)) return;
            foreach (var go in lista) if (go != null) Destroy(go);
            _tocos.Remove(b.Chave);
        }

        // o toco de raio 1 (a escala faz o raio do tronco): 8 lados, de 0,15 m abaixo do chão a ~0,5 m, a casca escura em volta
        // (a parte 0) e o corte claro em cima, inclinado (a parte 1)
        static Mesh MalhaDoToco()
        {
            const int N = 8;
            var v = new List<Vector3>();
            var casca = new List<int>();
            var corte = new List<int>();
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                float h0 = 0.48f + 0.08f * d0.z, h1 = 0.48f + 0.08f * d1.z;
                int b = v.Count;
                v.Add(d0 * 1.05f + Vector3.down * 0.15f); v.Add(d1 * 1.05f + Vector3.down * 0.15f);
                v.Add(d1 + Vector3.up * h1); v.Add(d0 + Vector3.up * h0);
                casca.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                int m = v.Count;
                v.Add(new Vector3(0f, 0.48f, 0f)); v.Add(d0 + Vector3.up * h0); v.Add(d1 + Vector3.up * h1);
                corte.AddRange(new[] { m, m + 1, m + 2 });
            }
            var malha = new Mesh { name = "Toco do pinheiro" };
            malha.SetVertices(v);
            malha.subMeshCount = 2;
            malha.SetTriangles(casca, 0);
            malha.SetTriangles(corte, 1);
            malha.RecalculateNormals();
            malha.RecalculateBounds();
            return malha;
        }

        /// <summary>
        /// No dono, o golpe do machado num pinheiro: a árvore em pé desse colisor (a chave e o pé). Falso se não for um pinheiro
        /// da mata (a parede, o toco, a obra).
        /// </summary>
        public bool TroncoEm(Collider c, Vector3 ponto, out long chave, out Vector3 pe)
        {
            chave = 0;
            pe = default;
            if (c == null || _paiDosPinheiros == null || !c.transform.IsChildOf(_paiDosPinheiros)) return false;
            var meio = ArvoresDoMundo.BlocoDe(ponto.x, ponto.z);
            float melhor = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!_blocos.TryGetValue(new Vector2Int(meio.x + dx, meio.y + dz), out var b) || b.Plantas == null) continue;
                    for (int k = 0; k < b.Plantas.Count; k++)
                    {
                        var pl = b.Plantas[k];
                        if (!ArvoresDoMundo.EmPe(pl) || CortadaNoBloco(b, k)) continue;
                        float d = new Vector2(pl.Posicao.x - ponto.x, pl.Posicao.z - ponto.z).magnitude - ArvoresDoMundo.RaioDoTronco(pl);
                        if (d >= melhor) continue;
                        melhor = d;
                        chave = FlorestaDoMundo.Chave(b.Chave.x, b.Chave.y, k);
                        pe = pl.Posicao;
                    }
                }
            return melhor < 0.6f;
        }

        /// <summary>A árvore cortada cai (se for perto da câmera), para longe de quem cortou: o prefab dela sem a colisão.</summary>
        public void Tombar(long chave, Vector3 deQuem)
        {
            if (!ArvoresDoMundo.Planta(chave, out var pl)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - pl.Posicao).sqrMagnitude > 120f * 120f) return;
            int i = IndiceDoPinheiro(pl.Especie);
            if (pinheiros == null || i < 0 || i >= pinheiros.Length || pinheiros[i] == null) return;
            var go = Instantiate(pinheiros[i], pl.Posicao, Quaternion.Euler(0f, pl.Giro, 0f));
            go.name = "Árvore caindo";
            go.transform.localScale = Vector3.one * pl.Escala;
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            Vector3 longe = pl.Posicao - deQuem;
            longe.y = 0f;
            if (longe.sqrMagnitude < 0.01f) longe = Vector3.forward;
            go.AddComponent<ArvoreCaindo>().Comecar(longe.normalized);
            Efeitos.Som(Efeitos.SocoParede, pl.Posicao + Vector3.up, 1f, true, 45f);
        }

        // os entalhes do machado em cada pinheiro, em cada máquina: um por lado
        readonly Dictionary<long, GameObject[]> _entalhes = new Dictionary<long, GameObject[]>();

        /// <summary>O entalhe de um lado (0 a 3) do pinheiro, com a fundura (0 a 1) e a altura do pé (m), a menos de 80 m.</summary>
        public void MarcarEntalhe(long chave, int lado, float fundura, float altura)
        {
            if (!ArvoresDoMundo.Planta(chave, out var pl) || !ArvoresDoMundo.EmPe(pl)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - pl.Posicao).sqrMagnitude > 80f * 80f) return;
            if (!_entalhes.TryGetValue(chave, out var marcas))
            {
                if (_entalhes.Count > 48) LimparEntalhes();
                marcas = new GameObject[4];
                _entalhes[chave] = marcas;
            }
            lado &= 3;
            var go = marcas[lado];
            float alturaDoCorte = altura;
            if (go == null)
            {
                go = new GameObject("Entalhe");
                go.transform.position = pl.Posicao;
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = FlorestaDoMundo.MateriaisDoEntalhe();
                r.shadowCastingMode = ShadowCastingMode.Off;
                marcas[lado] = go;
            }
            else
            {
                var m = go.GetComponent<MeshFilter>().sharedMesh;
                if (m != null) alturaDoCorte = m.bounds.min.y;
            }
            // o raio do tronco do pinheiro leve na altura do corte (a cápsula do prefab é um pouco maior que a casca)
            float raio = ArvoresDoMundo.RaioDoTronco(pl) * 0.8f;
            var filtro = go.GetComponent<MeshFilter>();
            var velha = filtro.sharedMesh;
            filtro.sharedMesh = FlorestaDoMundo.MalhaDoEntalhe(lado, Mathf.Clamp01(fundura), alturaDoCorte, raio);
            if (velha != null) Destroy(velha);
        }

        /// <summary>Os entalhes do pinheiro somem (ele caiu).</summary>
        public void TirarEntalhes(long chave)
        {
            if (!_entalhes.TryGetValue(chave, out var marcas)) return;
            foreach (var go in marcas) ApagarEntalhe(go);
            _entalhes.Remove(chave);
        }

        void LimparEntalhes()
        {
            foreach (var marcas in _entalhes.Values)
                foreach (var go in marcas) ApagarEntalhe(go);
            _entalhes.Clear();
        }

        static void ApagarEntalhe(GameObject go)
        {
            if (go == null) return;
            var f = go.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
            Destroy(go);
        }
    }
}
