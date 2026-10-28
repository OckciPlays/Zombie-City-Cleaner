using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O desmembramento: as regras dos membros e as malhas cortadas.
    /// Cada membro (braço esquerdo e direito, perna esquerda e direita) tem três estados, em 2 bits de um byte que vai pela
    /// rede: 0 inteiro, 1 sem a parte de baixo (cortado no cotovelo ou no joelho) e 2 sem o membro todo (no ombro ou no
    /// quadril). O tiro que acaba com a vida do membro corta no pedaço que ele pegou: no antebraço ou na canela, a parte de
    /// baixo; no braço ou na coxa, o membro todo.
    /// A malha cortada é feita na hora, a partir da malha inteira, e guardada: os triângulos presos sobretudo aos ossos
    /// perdidos saem, e cada buraco ganha uma tampa de carne (um leque até o meio da borda, com a carne do pescoço). O
    /// pedaço que cai é feito do mesmo jeito, com os triângulos que saíram e as tampas dele, já no espaço do osso do corte.
    /// </summary>
    public static class Membros
    {
        public const int Quantos = 4;
        public const int BracoE = 0, BracoD = 1, PernaE = 2, PernaD = 3;

        /// <summary>Os ossos da parte de baixo de cada membro (o primeiro é onde o corte fica) e o osso de cima.</summary>
        static readonly string[][] OssosDeBaixo =
        {
            new[] { "forearm.L", "hand.L" }, new[] { "forearm.R", "hand.R" },
            new[] { "shin.L", "foot.L", "toe.L" }, new[] { "shin.R", "foot.R", "toe.R" },
        };
        static readonly string[] OssoDeCima = { "upper_arm.L", "upper_arm.R", "thigh.L", "thigh.R" };

        public static int Limbo(Membro m)
        {
            switch (m)
            {
                case Membro.BracoE: case Membro.AntebracoE: return BracoE;
                case Membro.BracoD: case Membro.AntebracoD: return BracoD;
                case Membro.CoxaE: case Membro.CanelaE: return PernaE;
                case Membro.CoxaD: case Membro.CanelaD: return PernaD;
                default: return -1;
            }
        }

        public static bool DeBaixo(Membro m) => m == Membro.AntebracoE || m == Membro.AntebracoD || m == Membro.CanelaE || m == Membro.CanelaD;

        public static int Estado(byte membros, int limbo) => (membros >> (2 * limbo)) & 3;

        public static byte ComEstado(byte membros, int limbo, int estado) => (byte)((membros & ~(3 << (2 * limbo))) | (estado << (2 * limbo)));

        /// <summary>Este pedaço já caiu (a hitbox dele sai).</summary>
        public static bool Perdeu(byte membros, Membro m)
        {
            int l = Limbo(m);
            if (l < 0) return false;
            int e = Estado(membros, l);
            return DeBaixo(m) ? e >= 1 : e >= 2;
        }

        public static bool SemPerna(byte membros, int perna) => Estado(membros, PernaE + perna) > 0;

        /// <summary>O osso onde fica o corte (o primeiro que se perdeu) e o seguinte (para a direção do jorro).</summary>
        public static string OssoDoCorte(int limbo, int estado) => estado == 1 ? OssosDeBaixo[limbo][0] : OssoDeCima[limbo];
        public static string OssoDepoisDoCorte(int limbo, int estado) => estado == 1 ? OssosDeBaixo[limbo][1] : OssosDeBaixo[limbo][0];

        static void Perdidos(byte membros, Transform[] ossos, HashSet<int> saida)
        {
            saida.Clear();
            for (int l = 0; l < Quantos; l++)
            {
                int e = Estado(membros, l);
                if (e == 0) continue;
                foreach (var nome in OssosDeBaixo[l]) Adicionar(ossos, nome, saida);
                if (e >= 2) Adicionar(ossos, OssoDeCima[l], saida);
            }
        }

        // o osso e os que estão embaixo dele no esqueleto (nos Ocos, os 3 ossos de cada dedo ficam embaixo da mão: sem
        // eles, os dedos ficavam no ar quando o antebraço caía)
        static void Adicionar(Transform[] ossos, string nome, HashSet<int> saida)
        {
            Transform osso = null;
            for (int i = 0; i < ossos.Length; i++)
                if (ossos[i] != null && ossos[i].name == nome) { saida.Add(i); osso = ossos[i]; break; }
            if (osso == null) return;
            for (int i = 0; i < ossos.Length; i++)
                if (ossos[i] != null && ossos[i] != osso && ossos[i].IsChildOf(osso)) saida.Add(i);
        }

        public static int IndiceDoOsso(Transform[] ossos, string nome)
        {
            for (int i = 0; i < ossos.Length; i++)
                if (ossos[i] != null && ossos[i].name == nome) return i;
            return -1;
        }

        // ------------------------------------------------------------------ as malhas cortadas

        static readonly Dictionary<(Mesh, byte), Mesh> _variantes = new Dictionary<(Mesh, byte), Mesh>();
        static readonly Dictionary<(Mesh, byte, byte), Mesh> _pedacos = new Dictionary<(Mesh, byte, byte), Mesh>();
        static readonly HashSet<int> _perdidosDepois = new HashSet<int>(), _perdidosAntes = new HashSet<int>();
        static readonly List<int> _juntas = new List<int>();

        /// <summary>A malha sem os membros perdidos, com as tampas de carne (no submesh 1). Inteira, é a própria base.</summary>
        public static Mesh Variante(Mesh baseMesh, Transform[] ossos, byte membros)
        {
            if (membros == 0 || baseMesh == null) return baseMesh;
            if (_variantes.TryGetValue((baseMesh, membros), out var pronta) && pronta != null) return pronta;
            Perdidos(membros, ossos, _perdidosDepois);
            _perdidosAntes.Clear();
            _juntas.Clear();
            for (int l = 0; l < Quantos; l++)
            {
                int e = Estado(membros, l);
                if (e > 0) _juntas.Add(IndiceDoOsso(ossos, OssoDoCorte(l, e)));
            }
            var m = Cortar(baseMesh, _perdidosDepois, _perdidosAntes, false, -1, _juntas);
            m.name = $"{baseMesh.name} (cortada {membros})";
            m.UploadMeshData(true);   // a GPU guarda; os cortes seguintes partem sempre da malha inteira
            _variantes[(baseMesh, membros)] = m;
            return m;
        }

        /// <summary>
        /// O pedaço que cai entre dois estados (só um membro muda): os triângulos que saíram agora e as tampas viradas,
        /// no espaço do osso do corte (o pedaço é um objeto rígido posto onde o osso está).
        /// </summary>
        public static Mesh Pedaco(Mesh baseMesh, Transform[] ossos, byte antes, byte depois, int ossoDoCorte)
        {
            if (baseMesh == null || ossoDoCorte < 0) return null;
            if (_pedacos.TryGetValue((baseMesh, antes, depois), out var pronto) && pronto != null) return pronto;
            Perdidos(depois, ossos, _perdidosDepois);
            Perdidos(antes, ossos, _perdidosAntes);
            _juntas.Clear();
            _juntas.Add(ossoDoCorte);
            var m = Cortar(baseMesh, _perdidosDepois, _perdidosAntes, true, ossoDoCorte, _juntas);
            m.name = $"{baseMesh.name} (pedaço {antes}→{depois})";
            m.UploadMeshData(true);
            _pedacos[(baseMesh, antes, depois)] = m;
            return m;
        }

        static readonly Vector2 ForaDoRosto = new Vector2(-1f, -1f);

        // aresta sem direção, pelos índices soldados (vértices na mesma posição viram um só: as costuras de UV)
        static long Aresta(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>
        /// Corta a malha. Um triângulo sai quando a média, nos três vértices, do peso dos ossos perdidos passa de metade.
        /// Sem "pedaço", devolve a malha que fica (pele e carne, com as tampas). Com "pedaço", devolve só o que saiu agora
        /// (perdido depois e não antes), fechado com tampas viradas, no espaço do osso do corte e sem pesos (rígido).
        /// As juntas são os ossos dos cortes: onde o corte não deixou buraco (os zumbis em blocos), a tampa é a da caixa.
        /// </summary>
        static Mesh Cortar(Mesh baseMesh, HashSet<int> perdidosDepois, HashSet<int> perdidosAntes, bool pedaco, int ossoDoCorte, List<int> juntas)
        {
            var v = baseMesh.vertices;
            var nrm = baseMesh.normals;
            var uv = baseMesh.uv;
            var pesos = baseMesh.boneWeights;
            int n = v.Length;
            // o UV do rosto (zumbis leves): vai junto; as tampas ficam fora do rosto
            var uvRosto = baseMesh.uv2;
            bool temRosto = uvRosto.Length == n;
            var pDepois = new float[n];
            var pAntes = new float[n];
            for (int i = 0; i < n; i++)
            {
                var w = pesos[i];
                pDepois[i] = Peso(w, perdidosDepois);
                pAntes[i] = Peso(w, perdidosAntes);
            }
            // solda por posição
            var solda = new int[n];
            var porPosicao = new Dictionary<Vector3Int, int>(n);
            for (int i = 0; i < n; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 20000f), Mathf.RoundToInt(v[i].y * 20000f), Mathf.RoundToInt(v[i].z * 20000f));
                if (!porPosicao.TryGetValue(k, out int s)) { s = i; porPosicao[k] = i; }
                solda[i] = s;
            }

            // os triângulos de cada lado: "fica" e "sai" (no pedaço: "sai agora" e o resto)
            var pele = baseMesh.GetTriangles(0);
            var carne = baseMesh.subMeshCount > 1 ? baseMesh.GetTriangles(1) : new int[0];
            var dentro = new List<int>[2] { new List<int>(), new List<int>() };   // pele, carne do lado que se quer
            var arestasDeFora = new HashSet<long>();
            for (int sub = 0; sub < 2; sub++)
            {
                var tris = sub == 0 ? pele : carne;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    bool saiDepois = (pDepois[a] + pDepois[b] + pDepois[c]) / 3f > 0.5f;
                    bool saiu = (pAntes[a] + pAntes[b] + pAntes[c]) / 3f > 0.5f;
                    bool quero = pedaco ? saiDepois && !saiu : !saiDepois;
                    if (quero)
                    {
                        dentro[sub].Add(a); dentro[sub].Add(b); dentro[sub].Add(c);
                    }
                    else
                    {
                        // o outro lado: o que sai (na malha que fica) ou o resto (no pedaço: o corpo e o toco que já tinha
                        // saído antes, que também viram borda)
                        arestasDeFora.Add(Aresta(solda[a], solda[b])); arestasDeFora.Add(Aresta(solda[b], solda[c])); arestasDeFora.Add(Aresta(solda[c], solda[a]));
                    }
                }
            }

            // as bordas: arestas dos triângulos que se quer que também são de um triângulo do outro lado
            var borda = new List<(int a, int b)>();
            foreach (var tris in dentro)
                for (int t = 0; t < tris.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = tris[t + e], b = tris[t + (e + 1) % 3];
                        if (arestasDeFora.Contains(Aresta(solda[a], solda[b]))) borda.Add((a, b));
                    }

            // os buracos: as arestas da borda agrupadas pelos vértices soldados (cada grupo é um buraco)
            var pai = new Dictionary<int, int>();
            int Raiz(int x)
            {
                if (!pai.TryGetValue(x, out int p)) { pai[x] = x; return x; }
                while (p != x) { int pp = pai[p]; pai[x] = pp; x = p; p = pp; }
                return x;
            }
            foreach (var (a, b) in borda)
            {
                int ra = Raiz(solda[a]), rb = Raiz(solda[b]);
                if (ra != rb) pai[ra] = rb;
            }
            var grupos = new Dictionary<int, List<(int a, int b)>>();
            foreach (var e in borda)
            {
                int r = Raiz(solda[e.a]);
                if (!grupos.TryGetValue(r, out var lista)) grupos[r] = lista = new List<(int, int)>();
                lista.Add(e);
            }

            var vs = new List<Vector3>(v);
            var ns = new List<Vector3>(nrm);
            var us = new List<Vector2>(uv);
            var usRosto = temRosto ? new List<Vector2>(uvRosto) : null;
            var ws = new List<BoneWeight>(pesos);
            var tampa = new List<int>();
            int buracos = 0;
            foreach (var lista in grupos.Values)
            {
                if (lista.Count < 3) continue;
                buracos++;
                // o meio da borda e a direção da tampa (para fora do buraco)
                Vector3 meio = Vector3.zero;
                var unicos = new HashSet<int>();
                foreach (var (a, b) in lista)
                    if (unicos.Add(solda[a])) meio += v[a];
                meio /= unicos.Count;
                Vector3 normal = Vector3.zero;
                foreach (var (a, b) in lista) normal += Vector3.Cross(v[a] - v[b], meio - v[b]);
                normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
                // o osso que segura a tampa: o que mais pesa na borda, entre os que ficam
                var ossoDaTampa = OssoQueFica(lista, pesos, pedaco ? perdidosAntes : perdidosDepois, pedaco);
                // o meio da tampa fica um pouco para dentro (o toco fica côncavo)
                int centro = vs.Count;
                vs.Add(meio - normal * 0.012f); ns.Add(normal); us.Add(new Vector2(0.5f, 0.5f)); usRosto?.Add(ForaDoRosto);
                ws.Add(new BoneWeight { boneIndex0 = ossoDaTampa, weight0 = 1f });
                var copia = new Dictionary<int, int>();
                int Copia(int i)
                {
                    if (copia.TryGetValue(i, out int c)) return c;
                    c = vs.Count;
                    vs.Add(v[i]); ns.Add(normal); us.Add(new Vector2(0.5f, 0.5f)); usRosto?.Add(ForaDoRosto); ws.Add(pesos[i]);
                    copia[i] = c;
                    return c;
                }
                foreach (var (a, b) in lista)
                {
                    tampa.Add(Copia(b)); tampa.Add(Copia(a)); tampa.Add(centro);
                }
            }

            // os zumbis em blocos: cada pedaço é uma caixa fechada, o corte não deixa borda e não há buraco para tampar
            // (nos outros modelos a malha é contínua, todo corte deixa buraco e as tampas são só as dos buracos)
            if (juntas != null && buracos == 0)
                foreach (int junta in juntas)
                {
                    if (junta < 0) continue;
                    Vector3 j = baseMesh.bindposes[junta].inverse.MultiplyPoint3x4(Vector3.zero);
                    TampaDeCaixa(v, pesos, j, junta, perdidosDepois, pedaco, vs, ns, us, usRosto, ws, tampa);
                }

            var m = new Mesh { indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            if (pedaco)
            {
                // o pedaço é rígido: os vértices vão para o espaço do osso do corte (pela pose de ligação)
                var bind = baseMesh.bindposes[ossoDoCorte];
                for (int i = 0; i < vs.Count; i++)
                {
                    vs[i] = bind.MultiplyPoint3x4(vs[i]);
                    ns[i] = bind.MultiplyVector(ns[i]).normalized;
                }
                // tira os vértices que nenhum triângulo usa (a malha do pedaço fica pequena)
                Compactar(vs, ns, us, usRosto, dentro[0], dentro[1], tampa, out var v2, out var n2, out var u2, out var u2Rosto, out var pele2, out var carne2);
                m.SetVertices(v2); m.SetNormals(n2); m.SetUVs(0, u2);
                if (u2Rosto != null) m.SetUVs(1, u2Rosto);
                m.subMeshCount = 2;
                m.SetTriangles(pele2, 0);
                m.SetTriangles(carne2, 1);
            }
            else
            {
                m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, us);
                if (usRosto != null) m.SetUVs(1, usRosto);
                m.boneWeights = ws.ToArray();
                m.bindposes = baseMesh.bindposes;
                m.subMeshCount = 2;
                m.SetTriangles(dentro[0], 0);
                var carneToda = new List<int>(dentro[1]);
                carneToda.AddRange(tampa);
                m.SetTriangles(carneToda, 1);
                m.bounds = baseMesh.bounds;
            }
            if (pedaco) m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A tampa de um corte sem buraco (os zumbis em blocos): um retângulo de carne no plano da junta, do tamanho da
        /// ponta da caixa que sai (os vértices do osso do corte perto da junta). Na malha que fica, ele vai logo depois do
        /// fim do que ficou ali (o toco: a ponta do braço, o lado do peito, o pé da coxa), virado para fora e preso ao osso
        /// que mais aparece nesse fim; no pedaço, logo antes da ponta dele, virado para trás.
        /// </summary>
        static void TampaDeCaixa(Vector3[] v, BoneWeight[] pesos, Vector3 j, int junta, HashSet<int> perdidos, bool pedaco,
                                 List<Vector3> vs, List<Vector3> ns, List<Vector2> us, List<Vector2> usRosto, List<BoneWeight> ws, List<int> tampa)
        {
            // a direção do corte: da junta para o meio do que sai (os vértices presos ao osso do corte)
            Vector3 soma = Vector3.zero;
            int n = 0;
            for (int i = 0; i < v.Length; i++)
                if (pesos[i].boneIndex0 == junta && pesos[i].weight0 >= 0.5f) { soma += v[i]; n++; }
            if (n == 0) return;
            Vector3 dir = soma / n - j;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            Vector3 eu = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 ev = Vector3.Cross(dir, eu);
            // a ponta da caixa que sai, perto do plano da junta
            float uMin = float.MaxValue, uMax = float.MinValue, wMin = float.MaxValue, wMax = float.MinValue, ponta = float.MaxValue;
            for (int i = 0; i < v.Length; i++)
            {
                if (pesos[i].boneIndex0 != junta || pesos[i].weight0 < 0.5f) continue;
                Vector3 d = v[i] - j;
                float t = Vector3.Dot(d, dir);
                if (t < -0.04f || t > 0.04f) continue;
                float a = Vector3.Dot(d, eu), b = Vector3.Dot(d, ev);
                uMin = Mathf.Min(uMin, a); uMax = Mathf.Max(uMax, a);
                wMin = Mathf.Min(wMin, b); wMax = Mathf.Max(wMax, b);
                ponta = Mathf.Min(ponta, t);
            }
            if (uMin >= uMax || wMin >= wMax) return;
            float onde;
            int osso = junta;
            Vector3 normal = dir;
            if (pedaco)
            {
                onde = ponta - 0.002f;
                normal = -dir;
            }
            else
            {
                // o fim do que fica, dentro da ponta: a tampa do toco vai logo depois dele
                float fim = float.MinValue;
                var conta = new Dictionary<int, int>();
                for (int i = 0; i < v.Length; i++)
                {
                    var w = pesos[i];
                    if (w.weight0 < 0.5f || perdidos.Contains(w.boneIndex0)) continue;
                    Vector3 d = v[i] - j;
                    float t = Vector3.Dot(d, dir);
                    if (t < -0.06f || t > 0.06f) continue;
                    float a = Vector3.Dot(d, eu), b = Vector3.Dot(d, ev);
                    if (a < uMin - 0.01f || a > uMax + 0.01f || b < wMin - 0.01f || b > wMax + 0.01f) continue;
                    fim = Mathf.Max(fim, t);
                    conta.TryGetValue(w.boneIndex0, out int c);
                    conta[w.boneIndex0] = c + 1;
                }
                if (conta.Count == 0) return;
                int mais = -1;
                foreach (var kv in conta)
                    if (kv.Value > mais) { mais = kv.Value; osso = kv.Key; }
                onde = fim + 0.002f;
            }
            float uc = (uMin + uMax) / 2f, wc = (wMin + wMax) / 2f, hu = (uMax - uMin) / 2f * 0.94f, hw = (wMax - wMin) / 2f * 0.94f;
            int primeiro = vs.Count;
            foreach (var (su, sw) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                vs.Add(j + dir * onde + eu * (uc + su * hu) + ev * (wc + sw * hw));
                ns.Add(normal); us.Add(new Vector2(0.5f, 0.5f)); usRosto?.Add(ForaDoRosto);
                ws.Add(new BoneWeight { boneIndex0 = osso, weight0 = 1f });
            }
            // a frente do triângulo é a de Cross(b - a, c - a): virada para "normal"
            if (pedaco)
            {
                tampa.Add(primeiro); tampa.Add(primeiro + 2); tampa.Add(primeiro + 1);
                tampa.Add(primeiro); tampa.Add(primeiro + 3); tampa.Add(primeiro + 2);
            }
            else
            {
                tampa.Add(primeiro); tampa.Add(primeiro + 1); tampa.Add(primeiro + 2);
                tampa.Add(primeiro); tampa.Add(primeiro + 2); tampa.Add(primeiro + 3);
            }
        }

        static float Peso(BoneWeight w, HashSet<int> ossos)
        {
            if (ossos.Count == 0) return 0f;
            float p = 0f;
            if (ossos.Contains(w.boneIndex0)) p += w.weight0;
            if (ossos.Contains(w.boneIndex1)) p += w.weight1;
            if (ossos.Contains(w.boneIndex2)) p += w.weight2;
            if (ossos.Contains(w.boneIndex3)) p += w.weight3;
            return p;
        }

        // na malha que fica, o osso que mais pesa na borda fora os perdidos; no pedaço, o que mais pesa (é tudo rígido)
        static int OssoQueFica(List<(int a, int b)> lista, BoneWeight[] pesos, HashSet<int> perdidos, bool pedaco)
        {
            var soma = new Dictionary<int, float>();
            void Somar(int osso, float w)
            {
                if (w <= 0f || (!pedaco && perdidos.Contains(osso))) return;
                soma.TryGetValue(osso, out float s);
                soma[osso] = s + w;
            }
            foreach (var (a, _) in lista)
            {
                var w = pesos[a];
                Somar(w.boneIndex0, w.weight0); Somar(w.boneIndex1, w.weight1); Somar(w.boneIndex2, w.weight2); Somar(w.boneIndex3, w.weight3);
            }
            int melhor = pesos[lista[0].a].boneIndex0;
            float maior = -1f;
            foreach (var kv in soma)
                if (kv.Value > maior) { maior = kv.Value; melhor = kv.Key; }
            return melhor;
        }

        static void Compactar(List<Vector3> vs, List<Vector3> ns, List<Vector2> us, List<Vector2> usRosto, List<int> pele, List<int> carne, List<int> tampa,
                              out List<Vector3> v2, out List<Vector3> n2, out List<Vector2> u2, out List<Vector2> u2Rosto, out List<int> pele2, out List<int> carne2)
        {
            var novo = new Dictionary<int, int>();
            v2 = new List<Vector3>(); n2 = new List<Vector3>(); u2 = new List<Vector2>();
            u2Rosto = usRosto != null ? new List<Vector2>() : null;
            var rosto = u2Rosto;
            int Indice(int i, List<Vector3> v, List<Vector3> n, List<Vector2> u)
            {
                if (novo.TryGetValue(i, out int j)) return j;
                j = v.Count;
                v.Add(vs[i]); n.Add(ns[i]); u.Add(us[i]); rosto?.Add(usRosto[i]);
                novo[i] = j;
                return j;
            }
            pele2 = new List<int>(pele.Count);
            foreach (int i in pele) pele2.Add(Indice(i, v2, n2, u2));
            carne2 = new List<int>(carne.Count + tampa.Count);
            foreach (int i in carne) carne2.Add(Indice(i, v2, n2, u2));
            // a tampa já sai virada para fora do lado que se quer (aqui, o pedaço)
            foreach (int i in tampa) carne2.Add(Indice(i, v2, n2, u2));
        }
    }
}
