using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O machado corta as árvores (06/11/2026, a pedido: "colocar um sistema para o machado cortar árvore"). A floresta sai
    /// da semente e nada dela vai pela rede (<see cref="Floresta"/>); a árvore cortada é a exceção: a chave dela (o bloco e
    /// a ordem no <c>Floresta.DoBloco</c>) vai na lista do mundo (<see cref="MundoDaPartida"/>.Arvores.cs, que o save guarda).
    /// Aqui, em cada máquina: a cortada vira toco (a espécie troca no bloco montado, e a colisão dos troncos refaz), e, se
    /// for perto, ela cai (<see cref="ArvoreCaindo"/>). O dono acha a árvore do golpe pelo tronco (<see cref="TroncoEm"/>).
    /// </summary>
    public partial class FlorestaDoMundo
    {
        // as cortadas: a lista inteira copiada a cada mudança (a colisão e o desenho leem aqui)
        static HashSet<long> _cortadas = new HashSet<long>();
        // a espécie de antes (para a árvore cair com a cara dela)
        readonly Dictionary<long, (Especie e, Vector3 pos, float giro, float escala)> _antes = new Dictionary<long, (Especie, Vector3, float, float)>();

        /// <summary>A chave de uma planta: o bloco (x, z) e a ordem dela no bloco.</summary>
        public static long Chave(int bx, int bz, int i) => ((long)(bx & 0xFFFFF) << 40) | ((long)(bz & 0xFFFFF) << 20) | (uint)(i & 0xFFFFF);

        public static void Abrir(long chave, out int bx, out int bz, out int i)
        {
            bx = (int)((chave >> 40) & 0xFFFFF);
            bz = (int)((chave >> 20) & 0xFFFFF);
            i = (int)(chave & 0xFFFFF);
        }

        public static bool Cortada(long chave) => _cortadas.Contains(chave);

        /// <summary>Há alguma árvore cortada no mundo (para pular as contas quando não há).</summary>
        public static bool TemCortadas => _cortadas.Count > 0;

        /// <summary>As cortadas do mundo (a lista da rede mudou, ou chegou): os blocos montados viram toco onde precisa.</summary>
        public static void DefinirCortadas(IEnumerable<long> chaves)
        {
            var novas = new HashSet<long>(chaves);
            _cortadas = novas;
            // (12/11/2026) o Lakewood: os pinheiros cortados viram toco (LakewoodNaCena.Arvores.cs)
            LakewoodNaCena.Instancia?.AplicarCortadas();
            var f = Instancia;
            if (f == null) return;
            foreach (var b in f._blocos.Values) f.AplicarCortadas(b);
        }

        // as árvores do bloco que estão na lista viram toco (e a colisão refaz, se mudou)
        void AplicarCortadas(Bloco b)
        {
            var p = b?.Plantas;
            if (p == null || _cortadas.Count == 0) return;
            bool mudou = false;
            for (int i = 0; i < p.Especies.Length; i++)
            {
                var e = (Especie)p.Especies[i];
                if (!Floresta.EhArvore(e)) continue;
                long k = Chave(b.Chave.x, b.Chave.y, i);
                if (!_cortadas.Contains(k)) continue;
                _antes[k] = (e, p.Posicoes[i], p.Giros[i], p.Escalas[i]);
                p.Especies[i] = (byte)Especie.TocoA;
                mudou = true;
            }
            if (!mudou || b.Colisao == null) return;
            Destroy(b.Colisao);
            b.Colisao = null;
            b.ProximoTronco = 0;
            b.ColisaoFeita = false;
        }

        /// <summary>
        /// No dono, o golpe do machado num tronco: a árvore em pé desse tronco (a chave, o pé e a espécie). Falso se não for
        /// uma árvore da floresta (a parede, o toco, o tronco caído).
        /// </summary>
        public bool TroncoEm(Collider c, Vector3 ponto, out long chave, out Vector3 pe)
        {
            chave = 0;
            pe = default;
            if (c == null || _raiz == null || !c.transform.IsChildOf(_raiz)) return false;
            foreach (var b in _blocos.Values)
            {
                if (b.Colisao == null || b.Plantas == null || c.gameObject != b.Colisao) continue;
                var p = b.Plantas;
                float melhor = float.MaxValue;
                int qual = -1;
                for (int i = 0; i < p.Especies.Length; i++)
                {
                    var e = (Especie)p.Especies[i];
                    if (!Floresta.EhArvore(e) || Floresta.Deitada(e) || Floresta.RaioDoTronco[(int)e] <= 0f) continue;
                    var q = p.Posicoes[i];
                    float d = new Vector2(q.x - ponto.x, q.z - ponto.z).magnitude - Floresta.RaioDoTronco[(int)e] * p.Escalas[i];
                    if (d < melhor)
                    {
                        melhor = d;
                        qual = i;
                    }
                }
                if (qual < 0 || melhor > 0.6f) return false;
                chave = Chave(b.Chave.x, b.Chave.y, qual);
                pe = p.Posicoes[qual];
                return !_cortadas.Contains(chave);
            }
            return false;
        }

        /// <summary>A árvore cortada cai (em cada máquina, se o bloco estiver montado e perto), para longe de quem cortou.</summary>
        public void Tombar(long chave, Vector3 deQuem)
        {
            if (!_antes.TryGetValue(chave, out var a)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - a.pos).sqrMagnitude > 90f * 90f) return;
            int k = (int)a.e * 3;
            if (malhas == null || k >= malhas.Length || malhas[k] == null) return;
            var go = new GameObject("Árvore caindo");
            go.transform.SetPositionAndRotation(a.pos, Quaternion.Euler(0f, a.giro, 0f));
            go.transform.localScale = Vector3.one * a.escala;
            go.AddComponent<MeshFilter>().sharedMesh = malhas[k];
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            Vector3 longe = a.pos - deQuem;
            longe.y = 0f;
            if (longe.sqrMagnitude < 0.01f) longe = Vector3.forward;
            go.AddComponent<ArvoreCaindo>().Comecar(longe.normalized);
            Efeitos.Som(Efeitos.SocoParede, a.pos + Vector3.up, 1f, true, 45f);
        }

        // ------------------------------------------------------------------ os entalhes (07/11/2026)

        // os entalhes do machado em cada árvore, em cada máquina: um por lado (MundoDaPartida.Arvores.cs conta)
        readonly Dictionary<long, GameObject[]> _entalhes = new Dictionary<long, GameObject[]>();
        static Material _madeiraClara, _madeiraEscura;

        /// <summary>
        /// O entalhe de um lado (0 a 3, de 90 em 90° a partir do norte) da árvore, com a fundura (0 a 1) e a altura do pé (m):
        /// a boca do corte no tronco, a madeira clara embaixo (o corte reto) e a sombra em cima (o corte inclinado), mais
        /// larga quanto mais fundo. Só nas árvores dos blocos montados, a menos de 80 m.
        /// </summary>
        public void MarcarEntalhe(long chave, int lado, float fundura, float altura)
        {
            if (!ArvoreDaChave(chave, out var pe, out float raio, out int especie, out float escala, out float giro)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - pe).sqrMagnitude > 80f * 80f) return;
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
                go.transform.position = pe;
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = MateriaisDoEntalhe();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                marcas[lado] = go;
            }
            else alturaDoCorte = go.GetComponent<MeshFilter>().sharedMesh != null ? go.GetComponent<MeshFilter>().sharedMesh.bounds.min.y : altura;
            var filtro = go.GetComponent<MeshFilter>();
            var velha = filtro.sharedMesh;
            filtro.sharedMesh = MalhaDoEntalhe(lado, Mathf.Clamp01(fundura), alturaDoCorte, raio,
                (angulo, y) => RaioNoTronco(especie, y / escala, angulo * Mathf.Rad2Deg - giro) * escala);
            if (velha != null) Destroy(velha);
        }

        /// <summary>Os entalhes da árvore somem (ela caiu).</summary>
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

        /// <summary>
        /// A árvore em pé de uma chave, no bloco montado: o pé, o raio do tronco desenhado na altura do corte (a média, m), a
        /// espécie, a escala e o giro (graus).
        /// </summary>
        public bool ArvoreDaChave(long chave, out Vector3 pe, out float raio, out int especie, out float escala, out float giro)
        {
            pe = default;
            raio = escala = giro = 0f;
            especie = -1;
            Abrir(chave, out int bx, out int bz, out int i);
            if (!_blocos.TryGetValue(new Vector2Int(bx, bz), out var b) || b.Plantas == null) return false;
            var p = b.Plantas;
            if (i < 0 || i >= p.Especies.Length) return false;
            var e = (Especie)p.Especies[i];
            if (!Floresta.EhArvore(e) || Floresta.Deitada(e) || Floresta.RaioDoTronco[(int)e] <= 0f) return false;
            pe = p.Posicoes[i];
            especie = (int)e;
            escala = p.Escalas[i];
            giro = p.Giros[i];
            float medido = RaioNoTronco(especie, 0.9f, 0f);
            raio = (medido > 0f ? medido : Floresta.RaioDoTronco[especie] * 0.75f) * escala;
            return true;
        }

        // as medidas dos troncos (Editor/MedidaDosTroncos: o raio da superfície do tronco por altura e ângulo, na malha da
        // espécie, na escala 1); lidas uma vez de Resources/TroncosDasArvores
        static float[] _troncos;
        static int _tEspecies, _tAlturas, _tAngulos;
        static float _tAlturaMinima, _tPasso;
        static bool _troncosLidos;

        /// <summary>
        /// O raio do tronco da espécie (na escala 1) na altura 'y' (m, do pé) e no ângulo (graus, no espaço da árvore, a partir
        /// do +Z): a superfície medida no editor. -1 sem a medida (o arquivo não existe).
        /// </summary>
        public static float RaioNoTronco(int especie, float y, float anguloGraus)
        {
            if (!_troncosLidos) LerTroncos();
            if (_troncos == null || especie < 0 || especie >= _tEspecies) return -1f;
            float fh = Mathf.Clamp((y - _tAlturaMinima) / _tPasso, 0f, _tAlturas - 1.001f);
            int h0 = (int)fh;
            float th = fh - h0;
            float fa = Mathf.Repeat(anguloGraus, 360f) / 360f * _tAngulos;
            int a0 = Mathf.FloorToInt(fa) % _tAngulos, a1 = (a0 + 1) % _tAngulos;
            float ta = fa - Mathf.Floor(fa);
            float V(int h, int a) => _troncos[(especie * _tAlturas + h) * _tAngulos + a];
            return Mathf.Lerp(Mathf.Lerp(V(h0, a0), V(h0, a1), ta), Mathf.Lerp(V(h0 + 1, a0), V(h0 + 1, a1), ta), th);
        }

        static void LerTroncos()
        {
            _troncosLidos = true;
            var arquivo = Resources.Load<TextAsset>("TroncosDasArvores");
            if (arquivo == null) return;
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(arquivo.bytes)))
            {
                _tEspecies = r.ReadInt32();
                _tAlturas = r.ReadInt32();
                _tAngulos = r.ReadInt32();
                _tAlturaMinima = r.ReadSingle();
                _tPasso = r.ReadSingle();
                int n = _tEspecies * _tAlturas * _tAngulos;
                if (n <= 0 || n > 100000) return;
                var t = new float[n];
                for (int i = 0; i < n; i++) t[i] = r.ReadSingle();
                _troncos = t;
            }
        }

        public static Material[] MateriaisDoEntalhe()
        {
            if (_madeiraClara == null)
            {
                var sombra = Shader.Find("Universal Render Pipeline/Lit");
                _madeiraClara = new Material(sombra) { name = "Entalhe (madeira)" };
                _madeiraClara.SetColor("_BaseColor", new Color(0.86f, 0.70f, 0.50f));
                _madeiraClara.SetFloat("_Smoothness", 0.05f);
                _madeiraEscura = new Material(sombra) { name = "Entalhe (sombra)" };
                _madeiraEscura.SetColor("_BaseColor", new Color(0.30f, 0.20f, 0.12f));
                _madeiraEscura.SetFloat("_Smoothness", 0.05f);
            }
            return new[] { _madeiraClara, _madeiraEscura };
        }

        /// <summary>
        /// A boca do entalhe, colada por fora do tronco (no espaço do pé, com os eixos do mundo): uma faixa que acompanha a
        /// superfície do tronco ('raioEm': o raio no ângulo, em radianos, e na altura; sem ele, um círculo de 'raio'), com o
        /// fundo reto (o corte de baixo, madeira clara) e o alto em arco (o corte inclinado, na sombra). A largura sai da
        /// fundura: a corda que um corte dessa fundura abre num tronco desse raio.
        /// </summary>
        public static Mesh MalhaDoEntalhe(int lado, float fundura, float altura, float raio, System.Func<float, float, float> raioEm = null)
        {
            const int N = 10;
            float delta = Mathf.Max(0.04f, fundura) * raio * 0.6f;
            float meia = Mathf.Sqrt(Mathf.Max(0f, 2f * raio * delta - delta * delta));
            float teta = Mathf.Asin(Mathf.Clamp(meia / raio, 0.05f, 0.87f));   // (no máximo 60° para cada lado)
            float h = Mathf.Clamp(raio * 0.7f, 0.14f, 0.32f) * Mathf.Lerp(0.55f, 1f, fundura);
            float fi = lado * 90f * Mathf.Deg2Rad;
            float Raio(float ang, float y)
            {
                float medido = raioEm != null ? raioEm(ang, y) : -1f;
                return (medido > 0.01f ? medido : raio) + 0.008f;
            }
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var claro = new List<int>();
            var escuro = new List<int>();
            for (int k = 0; k <= N; k++)
            {
                float s = -1f + 2f * k / N;
                float a = fi + s * teta;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float boca = 1f - s * s;
                float yMeio = altura + h * 0.42f * boca, yTopo = altura + h * boca;
                // a coluna k: o fundo, o meio e o alto (cada um na superfície do tronco daquela altura)
                v.Add(dir * Raio(a, altura) + Vector3.up * altura);
                v.Add(dir * Raio(a, yMeio) + Vector3.up * yMeio);
                v.Add(dir * Raio(a, yTopo) + Vector3.up * yTopo);
                n.Add(dir); n.Add(dir); n.Add(dir);
                if (k == 0) continue;
                int b0 = (k - 1) * 3, b1 = k * 3;
                // (visto de fora, a coluna k fica à esquerda da k-1: os triângulos no sentido do relógio)
                claro.AddRange(new[] { b1 + 1, b0 + 1, b0, b1 + 1, b0, b1 });
                escuro.AddRange(new[] { b1 + 2, b0 + 2, b0 + 1, b1 + 2, b0 + 1, b1 + 1 });
            }
            var m = new Mesh { name = "Entalhe" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.subMeshCount = 2;
            m.SetTriangles(claro, 0);
            m.SetTriangles(escuro, 1);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>A árvore cortada caindo (06/11/2026): gira em volta do pé, bate no chão (o som e a poeira) e afunda.</summary>
    public class ArvoreCaindo : MonoBehaviour
    {
        Vector3 _eixo, _pe;
        Quaternion _inicio;
        float _tempo;
        bool _bateu;

        public void Comecar(Vector3 para)
        {
            _pe = transform.position;
            _inicio = transform.rotation;
            _eixo = Vector3.Cross(Vector3.up, para).normalized;
        }

        void Update()
        {
            _tempo += Time.deltaTime;
            // cai acelerando, como uma árvore de verdade (devagar no começo)
            float k = Mathf.Clamp01(_tempo / 2.2f);
            float angulo = 88f * k * k;
            transform.SetPositionAndRotation(_pe, Quaternion.AngleAxis(angulo, _eixo) * _inicio);
            if (!_bateu && k >= 1f)
            {
                _bateu = true;
                var topo = _pe + Vector3.Cross(_eixo, Vector3.up) * -6f;
                Efeitos.Som(Efeitos.SocoParede, topo, 1f, true, 50f);
                Efeitos.Impacto(topo, Vector3.up);
            }
            if (_tempo > 4.5f)
            {
                transform.position = _pe + Vector3.down * ((_tempo - 4.5f) * 1.5f);
                if (_tempo > 7f) Destroy(gameObject);
            }
        }
    }
}
