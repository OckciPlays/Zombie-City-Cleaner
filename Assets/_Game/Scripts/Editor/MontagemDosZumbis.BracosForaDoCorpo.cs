using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os braços da agonia, sem entrar no corpo (07/11/2026, a pedido: "os braços entram dentro do corpo deles"). A agonia
    /// é feita nos músculos do Oco_Original (<see cref="ClipesHumanoidesDaAgonia"/>) e tocada em cada zumbi pelo Humanoid,
    /// que guarda os ângulos: com o tronco curvado, as "mãos no rosto" ficavam nos ombros, os cotovelos cruzavam na frente
    /// do peito e os antebraços atravessavam o queixo e o peito (3 a 9 cm no Oco_Original; mais nos de cabeça grande).
    /// Agora, na gravação (<see cref="Gravar"/>), o tronco, os ombros e a cabeça continuam os dos músculos (o balanço, o
    /// "não" da cabeça, o tremor), e os braços vão a alvos medidos em cada modelo, por um IK de dois ossos (o cotovelo
    /// numa dobradiça de verdade: o braço gira no próprio eixo para o antebraço dobrar no plano dele):
    ///  - <b>AgoniaRosto</b>: as palmas nas bochechas e nos olhos (a frente da cabeça, medida nos vértices dela),
    ///    esfregando em círculos pequenos, os cotovelos para baixo e para fora; as mãos acompanham a cabeça balançando;
    ///  - <b>AgoniaPele</b>: um antebraço deitado na frente da barriga e a outra mão esfregando ele, do pulso ao cotovelo
    ///    (por cima, na grossura medida do antebraço); troca de lado no meio do laço;
    ///  - <b>AgoniaEncolher</b>: os punhos embaixo do queixo, na frente do peito, os cotovelos colados embaixo, tremendo
    ///    (a altura escolhida uma vez por modelo: a mais alta em que os braços cabem, até a frente da barriga).
    /// E, se mesmo assim algum ponto do braço entra no corpo (os modelos são diferentes), a mão sai pelo caminho mais curto
    /// (puxada para a frente do rosto ou do peito) e o cotovelo abre para fora, até sair com <see cref="Folga"/>; o ajuste
    /// é suavizado no tempo (sem tremer) e conferido de novo antes de gravar. Os pontos do braço que já encostam no corpo
    /// na pose de descanso não contam (são do modelo).
    /// O corpo: os triângulos do tronco, da cabeça e dos ombros (Malhas/&lt;modelo&gt;_Inteira, como as
    /// <see cref="MaosNoChao"/>), com os buracos grandes tampados (onde saem os braços e as pernas), postos na pose do
    /// quadro numa grade de cubos de <see cref="Voxel"/>: a pele, o de fora (achado a partir da borda da grade) e o de
    /// dentro (o resto). As malhas têm peças por cima de peças e furinhos, e a grade aguenta os dois.
    /// </summary>
    public static partial class MontagemDosZumbis
    {
        static bool EhAgonia(string clipe) => ClipesDaAgonia.Any(c => c.nome == clipe);

        sealed class BracosForaDoCorpo
        {
            const float Voxel = 0.012f;
            const float Folga = 0.004f;   // além do cubo da pele (que já engorda o corpo em até 1 cubo)
            const int PontosPorBraco = 260;

            sealed class Lado
            {
                public int s;                      // -1 o esquerdo, +1 o direito (o lado do x)
                public Transform braco, antebraco, mao;
                public Vector3[] pontos;           // na pose de ligação (o espaço da malha)
                public BoneWeight[] pesos;
                public bool[] doCotovelo;          // a metade de baixo do braço e a de cima do antebraço
                // a mão, no espaço dela: para onde vão os dedos, para onde olha a palma, o meio da palma; a meia grossura
                public Vector3 dedos, palma, meio;
                public float meiaMao;
                public float raioDoAntebraco;
                public readonly Quaternion[] guardado = new Quaternion[3];
            }

            readonly Transform[] _ossos;
            readonly Matrix4x4[] _bind, _m;
            // a pele do corpo: os vértices soldados (pela posição) e os triângulos, com os buracos tampados
            readonly Vector3[] _corpo;
            readonly BoneWeight[] _pesoDoCorpo;
            readonly int[] _tri;
            readonly int[][] _lacos;               // as bordas tampadas: o meio de cada uma é o vértice _corpo.Length + j
            readonly Vector3[] _p;
            // a grade (refeita a cada quadro): 0 ainda não se sabe, 1 a pele, 2 fora; e a distância com sinal (negativa dentro)
            Vector3 _origem;
            int _vx, _vy, _vz;
            byte[] _tipo = new byte[0];
            float[] _ate = new float[0], _campo = new float[0];
            int[] _fila = new int[0];

            readonly Lado[] _lados;
            // os pontos medidos no corpo (no espaço do osso) e as direções do personagem (frente, cima, direita) nele
            readonly Transform _cabeca, _peito;
            Vector3 _rostoE, _rostoD, _queixo, _frentePeito;
            Vector3 _fH, _uH, _rH, _fC, _uC, _rC;
            // no Encolher, a altura dos punhos entre a frente do peito (0) e o queixo (1); abaixo de 0, na frente da barriga.
            // Escolhida uma vez por modelo (EscolherAAltura): a mais alta em que os braços cabem sem entrar no corpo
            static readonly float[] AlturasDoEncolher = { 0.55f, 0.3f, 0.05f, -0.25f, -0.55f };
            float _alturaDoEncolher = AlturasDoEncolher[0];
            float[] _fundoNaAltura;

            BracosForaDoCorpo(Transform[] ossos, Matrix4x4[] bind, Vector3[] corpo, BoneWeight[] pesoDoCorpo, int[] tri, int[][] lacos,
                              Lado[] lados, Transform cabeca, Transform peito)
            {
                _ossos = ossos;
                _bind = bind;
                _m = new Matrix4x4[ossos.Length];
                _corpo = corpo;
                _pesoDoCorpo = pesoDoCorpo;
                _tri = tri;
                _lacos = lacos;
                _p = new Vector3[corpo.Length + lacos.Length];
                _lados = lados;
                _cabeca = cabeca;
                _peito = peito;
            }

            /// <summary>O corpo, os braços e as medidas do modelo (null se faltar osso ou a malha legível).</summary>
            public static BracosForaDoCorpo De(GameObject inst, SkinnedMeshRenderer smr, string nomeDoModelo)
            {
                var malha = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>($"{Malhas}/{nomeDoModelo}_Inteira.asset");
                if (malha == null || !malha.isReadable) malha = smr.sharedMesh;
                return De(smr, malha);
            }

            public static BracosForaDoCorpo De(SkinnedMeshRenderer smr, UnityEngine.Mesh malha)
            {
                if (malha == null || !malha.isReadable || malha.bindposes.Length != smr.bones.Length) return null;
                var ossos = smr.bones;
                var bind = malha.bindposes;
                var vs = malha.vertices;
                var pw = malha.boneWeights;
                if (pw.Length != vs.Length) return null;
                int Indice(string n) => System.Array.FindIndex(ossos, b => b != null && b.name == n);
                int ic = Indice("head"), ip = Indice("chest"), iq = Indice("hips"), ie = Indice("spine");
                if (ip < 0) ip = Indice("spine_02");
                if (ic < 0 || ip < 0 || iq < 0 || ie < 0) return null;
                // o corpo: o quadril e tudo da coluna para cima (os ombros, o pescoço, a cabeça, o papo do Long, o olho do
                // Brutamonte), menos os braços
                var bracos = new[] { Indice("upper_arm.L"), Indice("upper_arm.R") };
                if (bracos.Any(b => b < 0)) return null;
                var doCorpo = ossos.Select(b => b != null && (b == ossos[iq] || b.IsChildOf(ossos[ie])) && !bracos.Any(x => b.IsChildOf(ossos[x]))).ToArray();
                bool DoCorpo(int v) => doCorpo[pw[v].boneIndex0];

                // na pose de ligação, no mundo (o modelo na origem, de frente para +z)
                var W = smr.transform.localToWorldMatrix;
                var Winv = W.inverse;
                Vector3 Mundo(int v) => W.MultiplyPoint3x4(vs[v]);
                Vector3 OrigemDe(int b) => W.MultiplyPoint3x4(bind[b].inverse.MultiplyPoint3x4(Vector3.zero));
                Vector3 PontoNoOsso(int b, Vector3 mundo) => bind[b].MultiplyPoint3x4(Winv.MultiplyPoint3x4(mundo));
                Vector3 DirecaoNoOsso(int b, Vector3 mundo) => bind[b].MultiplyVector(Winv.MultiplyVector(mundo)).normalized;

                // a pele: os triângulos com os 3 vértices presos ao tronco, à cabeça ou aos ombros, soldados pela posição
                var soldado = new Dictionary<Vector3Int, int>();
                var corpo = new List<Vector3>();
                var pesoDoCorpo = new List<BoneWeight>();
                var tri = new List<int>();
                int Soldar(int v)
                {
                    var chave = Vector3Int.RoundToInt(vs[v] * 10000f);
                    if (soldado.TryGetValue(chave, out int i)) return i;
                    soldado[chave] = corpo.Count;
                    corpo.Add(vs[v]);
                    pesoDoCorpo.Add(pw[v]);
                    return corpo.Count - 1;
                }
                var tris = malha.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (!DoCorpo(tris[i]) || !DoCorpo(tris[i + 1]) || !DoCorpo(tris[i + 2])) continue;
                    int a = Soldar(tris[i]), b = Soldar(tris[i + 1]), c = Soldar(tris[i + 2]);
                    if (a == b || b == c || c == a) continue;
                    tri.Add(a); tri.Add(b); tri.Add(c);
                }
                if (tri.Count < 30) return null;
                // os buracos (onde saem os braços, as pernas e, às vezes, o pescoço): tampados por um leque até o meio da
                // borda, que acompanha a pose (os furinhos menores que um cubo a grade já fecha)
                var lacos = Bordas(tri).Where(l => l.Length >= 6).ToList();
                for (int j = 0; j < lacos.Count; j++)
                {
                    int centro = corpo.Count + j;
                    var laco = lacos[j];
                    for (int i = 0; i < laco.Length; i++)
                    {
                        tri.Add(laco[(i + 1) % laco.Length]); tri.Add(laco[i]); tri.Add(centro);
                    }
                }

                // os braços: os pontos (o antebraço, a mão, os dedos e a metade de baixo do braço) e as medidas da mão
                var lados = new Lado[2];
                for (int k = 0; k < 2; k++)
                {
                    string s = k == 0 ? "L" : "R", d = k == 0 ? "_l" : "_r";
                    int ib = Indice("upper_arm." + s), ia = Indice("forearm." + s), im = Indice("hand." + s);
                    int iIndicador = Indice("index_01" + d), iMedio = Indice("middle_01" + d), iMinimo = Indice("pinky_01" + d), iPolegar = Indice("thumb_01" + d);
                    if (ib < 0 || ia < 0 || im < 0) return null;
                    bool comDedos = iIndicador >= 0 && iMedio >= 0 && iMinimo >= 0 && iPolegar >= 0;
                    var l = new Lado { braco = ossos[ib], antebraco = ossos[ia], mao = ossos[im] };
                    Vector3 ob = OrigemDe(ib), oa = OrigemDe(ia), om = OrigemDe(im);
                    l.s = ob.x < 0f ? -1 : 1;
                    var escolhidos = new List<int>();
                    var cotovelo = new List<bool>();
                    var daMao = new List<Vector3>();
                    var doAntebraco = new List<float>();
                    Vector3 eixoDoAntebraco = (om - oa).normalized;
                    for (int v = 0; v < vs.Length; v++)
                    {
                        int i = pw[v].boneIndex0;
                        if (i < 0 || i >= ossos.Length || ossos[i] == null) continue;
                        Vector3 x = Mundo(v);
                        bool daMaoOuDedo = i == im || ossos[i].IsChildOf(ossos[im]);
                        if (daMaoOuDedo) daMao.Add(x);
                        if (i == ia) doAntebraco.Add((x - oa - eixoDoAntebraco * Vector3.Dot(x - oa, eixoDoAntebraco)).magnitude);
                        if (i == ib)
                        {
                            // do braço, só a metade de baixo (o cotovelo): a de cima encosta no ombro de verdade
                            float t = Vector3.Dot(x - ob, oa - ob) / Mathf.Max(1e-6f, (oa - ob).sqrMagnitude);
                            if (t < 0.55f) continue;
                        }
                        else if (i != ia && !daMaoOuDedo) continue;
                        escolhidos.Add(v);
                        cotovelo.Add(i == ib || (i == ia && Vector3.Dot(x - oa, om - oa) < 0.5f * (om - oa).sqrMagnitude));
                    }
                    if (escolhidos.Count == 0 || daMao.Count == 0) return null;
                    int passo = Mathf.Max(1, Mathf.CeilToInt(escolhidos.Count / (float)PontosPorBraco));
                    var ix = Enumerable.Range(0, escolhidos.Count).Where(j => j % passo == 0).ToArray();
                    l.pontos = ix.Select(j => vs[escolhidos[j]]).ToArray();
                    l.pesos = ix.Select(j => pw[escolhidos[j]]).ToArray();
                    l.doCotovelo = ix.Select(j => cotovelo[j]).ToArray();
                    // a mão: os dedos vão do pulso ao nó do médio; a palma é o lado do polegar (que fica mais para dentro
                    // da mão do que os nós dos dedos). Sem os ossos dos dedos (os especiais): os dedos vão do pulso ao meio
                    // da mão, e a palma é o lado mais fino dela, o de baixo e de dentro (na pose de ligação)
                    Vector3 dedos, palma;
                    if (comDedos)
                    {
                        dedos = (OrigemDe(iMedio) - om).normalized;
                        Vector3 atravessa = OrigemDe(iIndicador) - OrigemDe(iMinimo);
                        palma = Vector3.Cross(dedos, atravessa).normalized;
                        Vector3 polegar = OrigemDe(iPolegar) - (OrigemDe(iIndicador) + OrigemDe(iMinimo)) * 0.5f;
                        if (Vector3.Dot(polegar, palma) < 0f) palma = -palma;
                    }
                    else
                    {
                        Vector3 centro = daMao.Aggregate(Vector3.zero, (acc, x) => acc + x) / daMao.Count;
                        dedos = (centro - om).sqrMagnitude > 1e-6f ? (centro - om).normalized : (om - oa).normalized;
                        palma = MaisFino(daMao, centro, dedos);
                        if (Vector3.Dot(palma, Vector3.down + Vector3.right * -l.s) < 0f) palma = -palma;
                    }
                    float comprimento = daMao.Max(x => Vector3.Dot(x - om, dedos));
                    var grossuras = daMao.Select(x => Mathf.Abs(Vector3.Dot(x - om, palma))).OrderBy(x => x).ToList();
                    l.meiaMao = Mathf.Clamp(grossuras[(int)(grossuras.Count * 0.85f)], 0.01f, 0.04f);
                    l.dedos = DirecaoNoOsso(im, dedos);
                    l.palma = DirecaoNoOsso(im, palma);
                    l.meio = PontoNoOsso(im, om + dedos * (0.42f * comprimento));
                    doAntebraco.Sort();
                    l.raioDoAntebraco = doAntebraco.Count > 0 ? Mathf.Clamp(doAntebraco[(int)(doAntebraco.Count * 0.85f)], 0.02f, 0.08f) : 0.04f;
                    lados[k] = l;
                }

                var saida = new BracosForaDoCorpo(ossos, bind, corpo.ToArray(), pesoDoCorpo.ToArray(), tri.ToArray(), lacos.ToArray(), lados, ossos[ic], ossos[ip]);
                // o rosto: a frente da cabeça na altura dos olhos e das bochechas (de baixo para cima, de 30% a 55% da
                // cabeça), um ponto de cada lado, na metade da meia largura; o queixo: a frente da parte de baixo
                var cabeca = Enumerable.Range(0, vs.Length).Where(v => pw[v].boneIndex0 == ic).Select(Mundo).ToList();
                var peito = Enumerable.Range(0, vs.Length).Where(v => pw[v].boneIndex0 == ip).Select(Mundo).ToList();
                if (cabeca.Count < 10 || peito.Count < 10) return null;
                float y0 = cabeca.Min(x => x.y), y1 = cabeca.Max(x => x.y), h = y1 - y0;
                var faixa = cabeca.Where(x => x.y > y0 + 0.30f * h && x.y < y0 + 0.55f * h).ToList();
                if (faixa.Count < 4) faixa = cabeca;
                float meiaLargura = faixa.Max(x => Mathf.Abs(x.x));
                float yRosto = y0 + 0.43f * h;
                Vector3 Frente(List<Vector3> lista, float x, float y)
                {
                    for (float raio = 0.015f; raio < 0.2f; raio *= 2f)
                    {
                        var perto = lista.Where(p => Mathf.Abs(p.x - x) < raio).ToList();
                        if (perto.Count > 0) return new Vector3(x, y, perto.Max(p => p.z));
                    }
                    return new Vector3(x, y, lista.Max(p => p.z));
                }
                saida._rostoE = PontoNoOsso(ic, Frente(faixa, -0.5f * meiaLargura, yRosto));
                saida._rostoD = PontoNoOsso(ic, Frente(faixa, 0.5f * meiaLargura, yRosto));
                var baixo = cabeca.Where(x => x.y < y0 + 0.15f * h).ToList();
                saida._queixo = PontoNoOsso(ic, Frente(baixo.Count > 0 ? baixo : cabeca, 0f, y0 + 0.05f * h));
                float p0 = peito.Min(x => x.y), p1 = peito.Max(x => x.y);
                var meioDoPeito = peito.Where(x => x.y > p0 + 0.3f * (p1 - p0) && x.y < p0 + 0.7f * (p1 - p0)).ToList();
                saida._frentePeito = PontoNoOsso(ip, Frente(meioDoPeito.Count > 0 ? meioDoPeito : peito, 0f, (p0 + p1) * 0.5f));
                // os pontos do braço que já encostam no corpo na pose de ligação (os braços dos lados, a massa dos ombros do
                // Brutamonte por cima do braço) são do modelo: não contam
                for (int i = 0; i < saida._m.Length; i++) saida._m[i] = W;
                saida.Gradear();
                foreach (var l in lados)
                {
                    var fica = Enumerable.Range(0, l.pontos.Length).Where(i => saida.Campo(W.MultiplyPoint3x4(l.pontos[i])) > Folga + 0.008f).ToArray();
                    l.pontos = fica.Select(i => l.pontos[i]).ToArray();
                    l.pesos = fica.Select(i => l.pesos[i]).ToArray();
                    l.doCotovelo = fica.Select(i => l.doCotovelo[i]).ToArray();
                }
                saida._fH = DirecaoNoOsso(ic, Vector3.forward); saida._uH = DirecaoNoOsso(ic, Vector3.up); saida._rH = DirecaoNoOsso(ic, Vector3.right);
                saida._fC = DirecaoNoOsso(ip, Vector3.forward); saida._uC = DirecaoNoOsso(ip, Vector3.up); saida._rC = DirecaoNoOsso(ip, Vector3.right);
                return saida;
            }

            /// <summary>A direção em que os pontos são mais finos, perpendicular a <paramref name="fora"/> (a menor variância).</summary>
            static Vector3 MaisFino(List<Vector3> pontos, Vector3 centro, Vector3 fora)
            {
                Vector3 a = Vector3.Cross(fora, Mathf.Abs(fora.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 b = Vector3.Cross(fora, a);
                float saa = 0f, sbb = 0f, sab = 0f;
                foreach (var p in pontos)
                {
                    float x = Vector3.Dot(p - centro, a), y = Vector3.Dot(p - centro, b);
                    saa += x * x; sbb += y * y; sab += x * y;
                }
                // o menor autovetor da covariância 2 × 2
                float angulo = 0.5f * Mathf.Atan2(2f * sab, saa - sbb) + Mathf.PI * 0.5f;
                return (a * Mathf.Cos(angulo) + b * Mathf.Sin(angulo)).normalized;
            }

            /// <summary>As bordas da pele (as arestas de um triângulo só), em laços na volta dos triângulos.</summary>
            static List<int[]> Bordas(List<int> tri)
            {
                long Chave(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                var contagem = new Dictionary<long, int>();
                for (int i = 0; i < tri.Count; i += 3)
                    for (int k = 0; k < 3; k++)
                    {
                        long c = Chave(tri[i + k], tri[i + (k + 1) % 3]);
                        contagem[c] = contagem.TryGetValue(c, out int n) ? n + 1 : 1;
                    }
                var saindo = new Dictionary<int, List<int>>();
                for (int i = 0; i < tri.Count; i += 3)
                    for (int k = 0; k < 3; k++)
                    {
                        int a = tri[i + k], b = tri[i + (k + 1) % 3];
                        if (contagem[Chave(a, b)] != 1) continue;
                        if (!saindo.TryGetValue(a, out var l)) saindo[a] = l = new List<int>();
                        l.Add(b);
                    }
                var lacos = new List<int[]>();
                foreach (int inicio in saindo.Keys.ToList())
                    while (saindo[inicio].Count > 0)
                    {
                        var laco = new List<int> { inicio };
                        int prox = saindo[inicio][0];
                        saindo[inicio].RemoveAt(0);
                        while (prox != inicio && laco.Count < 4000)
                        {
                            laco.Add(prox);
                            if (!saindo.TryGetValue(prox, out var l) || l.Count == 0) break;
                            int depois = l[0];
                            l.RemoveAt(0);
                            prox = depois;
                        }
                        if (laco.Count >= 3) lacos.Add(laco.ToArray());
                    }
                return lacos;
            }

            // ------------------------------------------------------------------ o corpo na pose de agora

            void Matrizes()
            {
                for (int i = 0; i < _ossos.Length; i++)
                    _m[i] = _ossos[i] != null ? _ossos[i].localToWorldMatrix * _bind[i] : Matrix4x4.identity;
            }

            Vector3 NaPose(Vector3 v, BoneWeight w)
            {
                Vector3 p = _m[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
                if (w.weight1 > 0f) p += _m[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
                if (w.weight2 > 0f) p += _m[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
                if (w.weight3 > 0f) p += _m[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                float soma = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                return soma > 1e-5f ? p / soma : p;
            }

            int Indice(int x, int y, int z) => (z * _vy + y) * _vx + x;

            /// <summary>Põe o corpo na pose de agora (os braços não contam) e refaz a grade e as distâncias.</summary>
            void PosarOCorpo()
            {
                Matrizes();
                Gradear();
            }

            /// <summary>A grade do corpo com as matrizes de agora (<see cref="_m"/>).</summary>
            void Gradear()
            {
                for (int i = 0; i < _corpo.Length; i++) _p[i] = NaPose(_corpo[i], _pesoDoCorpo[i]);
                for (int j = 0; j < _lacos.Length; j++)
                {
                    Vector3 soma = Vector3.zero;
                    foreach (int i in _lacos[j]) soma += _p[i];
                    _p[_corpo.Length + j] = soma / _lacos[j].Length;
                }
                Vector3 min = _p[0], max = _p[0];
                for (int i = 1; i < _p.Length; i++) { min = Vector3.Min(min, _p[i]); max = Vector3.Max(max, _p[i]); }
                _origem = min - Vector3.one * (3f * Voxel);
                Vector3 tam = max - min + Vector3.one * (6f * Voxel);
                _vx = Mathf.CeilToInt(tam.x / Voxel);
                _vy = Mathf.CeilToInt(tam.y / Voxel);
                _vz = Mathf.CeilToInt(tam.z / Voxel);
                int n = _vx * _vy * _vz;
                if (_tipo.Length < n)
                {
                    _tipo = new byte[n];
                    _ate = new float[n];
                    _campo = new float[n];
                    _fila = new int[n];
                }
                System.Array.Clear(_tipo, 0, n);
                // a pele: cada triângulo, em pontos a cada meio cubo
                for (int t = 0; t < _tri.Length; t += 3)
                {
                    Vector3 a = _p[_tri[t]], b = _p[_tri[t + 1]], c = _p[_tri[t + 2]];
                    float lado = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                    int k = Mathf.Max(1, Mathf.CeilToInt(lado / (0.5f * Voxel)));
                    for (int i = 0; i <= k; i++)
                        for (int j = 0; j <= k - i; j++)
                        {
                            Vector3 q = a + (b - a) * ((float)i / k) + (c - a) * ((float)j / k) - _origem;
                            int x = (int)(q.x / Voxel), y = (int)(q.y / Voxel), z = (int)(q.z / Voxel);
                            if (x < 0 || y < 0 || z < 0 || x >= _vx || y >= _vy || z >= _vz) continue;
                            _tipo[Indice(x, y, z)] = 1;
                        }
                }
                // o de fora: tudo o que se alcança da borda da grade sem passar pela pele
                int cabeca = 0, cauda = 0;
                void Entrar(int x, int y, int z)
                {
                    if (x < 0 || y < 0 || z < 0 || x >= _vx || y >= _vy || z >= _vz) return;
                    int i = Indice(x, y, z);
                    if (_tipo[i] != 0) return;
                    _tipo[i] = 2;
                    _fila[cauda++] = i;
                }
                for (int x = 0; x < _vx; x++)
                    for (int y = 0; y < _vy; y++) { Entrar(x, y, 0); Entrar(x, y, _vz - 1); }
                for (int x = 0; x < _vx; x++)
                    for (int z = 0; z < _vz; z++) { Entrar(x, 0, z); Entrar(x, _vy - 1, z); }
                for (int y = 0; y < _vy; y++)
                    for (int z = 0; z < _vz; z++) { Entrar(0, y, z); Entrar(_vx - 1, y, z); }
                while (cabeca < cauda)
                {
                    int i = _fila[cabeca++];
                    int x = i % _vx, y = i / _vx % _vy, z = i / (_vx * _vy);
                    Entrar(x - 1, y, z); Entrar(x + 1, y, z);
                    Entrar(x, y - 1, z); Entrar(x, y + 1, z);
                    Entrar(x, y, z - 1); Entrar(x, y, z + 1);
                }
                // as distâncias (em cubos): de fora até o corpo, e de dentro (a pele e o miolo) até fora
                for (int i = 0; i < n; i++) _ate[i] = _tipo[i] == 2 ? float.MaxValue : 0f;
                Chanfro(_ate);
                for (int i = 0; i < n; i++) _campo[i] = _tipo[i] == 2 ? (_ate[i] - 0.5f) * Voxel : 0f;
                for (int i = 0; i < n; i++) _ate[i] = _tipo[i] == 2 ? 0f : float.MaxValue;
                Chanfro(_ate);
                for (int i = 0; i < n; i++) if (_tipo[i] != 2) _campo[i] = -(_ate[i] - 0.5f) * Voxel;
            }

            /// <summary>A distância de cada cubo até o mais perto com 0 (o chanfro de 3 × 3 × 3, ida e volta).</summary>
            void Chanfro(float[] d)
            {
                const float R2 = 1.41421356f, R3 = 1.73205081f;
                for (int volta = 0; volta < 2; volta++)
                {
                    bool ida = volta == 0;
                    int s = ida ? -1 : 1;
                    for (int z0 = 0; z0 < _vz; z0++)
                    {
                        int z = ida ? z0 : _vz - 1 - z0;
                        for (int y0 = 0; y0 < _vy; y0++)
                        {
                            int y = ida ? y0 : _vy - 1 - y0;
                            for (int x0 = 0; x0 < _vx; x0++)
                            {
                                int x = ida ? x0 : _vx - 1 - x0;
                                int i = Indice(x, y, z);
                                float melhor = d[i];
                                if (melhor == 0f) continue;
                                // os vizinhos já vistos nesta volta: a fatia de antes (9), a linha de antes (3) e o de antes
                                int zz = z + s;
                                if (zz >= 0 && zz < _vz)
                                    for (int dy = -1; dy <= 1; dy++)
                                    {
                                        int yy = y + dy;
                                        if (yy < 0 || yy >= _vy) continue;
                                        for (int dx = -1; dx <= 1; dx++)
                                        {
                                            int xx = x + dx;
                                            if (xx < 0 || xx >= _vx) continue;
                                            int q = (dx != 0 ? 1 : 0) + (dy != 0 ? 1 : 0);
                                            float v = d[Indice(xx, yy, zz)] + (q == 0 ? 1f : q == 1 ? R2 : R3);
                                            if (v < melhor) melhor = v;
                                        }
                                    }
                                int y2 = y + s;
                                if (y2 >= 0 && y2 < _vy)
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        int xx = x + dx;
                                        if (xx < 0 || xx >= _vx) continue;
                                        float v = d[Indice(xx, y2, z)] + (dx == 0 ? 1f : R2);
                                        if (v < melhor) melhor = v;
                                    }
                                int x2 = x + s;
                                if (x2 >= 0 && x2 < _vx)
                                {
                                    float v = d[Indice(x2, y, z)] + 1f;
                                    if (v < melhor) melhor = v;
                                }
                                d[i] = melhor;
                            }
                        }
                    }
                }
            }

            /// <summary>A distância com sinal até o corpo (negativa dentro), entre os centros dos cubos.</summary>
            float Campo(Vector3 p)
            {
                Vector3 g = (p - _origem) / Voxel - Vector3.one * 0.5f;
                if (g.x < 0f || g.y < 0f || g.z < 0f || g.x > _vx - 1 || g.y > _vy - 1 || g.z > _vz - 1) return 1f;
                int x = Mathf.Min((int)g.x, _vx - 2), y = Mathf.Min((int)g.y, _vy - 2), z = Mathf.Min((int)g.z, _vz - 2);
                float fx = g.x - x, fy = g.y - y, fz = g.z - z;
                float c00 = Mathf.Lerp(_campo[Indice(x, y, z)], _campo[Indice(x + 1, y, z)], fx);
                float c10 = Mathf.Lerp(_campo[Indice(x, y + 1, z)], _campo[Indice(x + 1, y + 1, z)], fx);
                float c01 = Mathf.Lerp(_campo[Indice(x, y, z + 1)], _campo[Indice(x + 1, y, z + 1)], fx);
                float c11 = Mathf.Lerp(_campo[Indice(x, y + 1, z + 1)], _campo[Indice(x + 1, y + 1, z + 1)], fx);
                return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
            }

            /// <summary>
            /// O quanto o braço está dentro do corpo, mais a folga, e para onde sair (a média, pelo fundo, das saídas dos
            /// pontos de dentro; no mundo): perto do cotovelo e perto da mão.
            /// </summary>
            void Dentro(Lado l, out float cotovelo, out Vector3 saiCotovelo, out float mao, out Vector3 saiMao)
            {
                Matrizes();
                cotovelo = mao = 0f;
                saiCotovelo = saiMao = Vector3.zero;
                for (int i = 0; i < l.pontos.Length; i++)
                {
                    Vector3 p = NaPose(l.pontos[i], l.pesos[i]);
                    float f = Folga - Campo(p);
                    if (f <= 0f) continue;
                    var g = new Vector3(Campo(p + Vector3.right * Voxel) - Campo(p - Vector3.right * Voxel),
                                        Campo(p + Vector3.up * Voxel) - Campo(p - Vector3.up * Voxel),
                                        Campo(p + Vector3.forward * Voxel) - Campo(p - Vector3.forward * Voxel));
                    if (g.sqrMagnitude > 1e-12f) g.Normalize();
                    if (l.doCotovelo[i]) { cotovelo = Mathf.Max(cotovelo, f); saiCotovelo += g * f; }
                    else { mao = Mathf.Max(mao, f); saiMao += g * f; }
                }
                if (saiCotovelo.sqrMagnitude > 1e-12f) saiCotovelo.Normalize();
                if (saiMao.sqrMagnitude > 1e-12f) saiMao.Normalize();
            }

            float Fundo(Lado l)
            {
                Dentro(l, out float c, out _, out float m, out _);
                return Mathf.Max(c, m);
            }

            /// <summary>O quanto o ponto mais fundo dos braços está dentro do corpo (0 se nenhum), em metros, na pose de agora.</summary>
            public float Fundo()
            {
                PosarOCorpo();
                return Mathf.Max(0f, Mathf.Max(Fundo(_lados[0]), Fundo(_lados[1])) - Folga);
            }

            // ------------------------------------------------------------------ as poses (o IK das mãos)

            /// <summary>Guarda o giro do braço, do antebraço e da mão que os músculos deram neste quadro.</summary>
            public void Guardar()
            {
                foreach (var l in _lados)
                {
                    l.guardado[0] = l.braco.localRotation;
                    l.guardado[1] = l.antebraco.localRotation;
                    l.guardado[2] = l.mao.localRotation;
                }
            }

            void Restaurar()
            {
                foreach (var l in _lados)
                {
                    l.braco.localRotation = l.guardado[0];
                    l.antebraco.localRotation = l.guardado[1];
                    l.mao.localRotation = l.guardado[2];
                }
            }

            struct Alvo
            {
                public Vector3 palma, polo;   // o meio da palma e para onde vai o cotovelo
                public Quaternion mao;        // o giro da mão no mundo
            }

            static Quaternion GiroDaMao(Lado l, Vector3 dedos, Vector3 palma)
            {
                palma = palma.normalized;
                dedos = (dedos - palma * Vector3.Dot(dedos, palma)).normalized;
                return Quaternion.LookRotation(dedos, palma) * Quaternion.Inverse(Quaternion.LookRotation(l.dedos, l.palma));
            }

            static float Onda_(float t, float periodo, float fase = 0f) => Mathf.Sin(2f * Mathf.PI * t / periodo + fase);

            /// <summary>
            /// O ajuste de um quadro, no espaço do peito (acompanha o balanço do tronco): o quanto cada mão sai do lugar
            /// ([0] a esquerda, [1] a direita, em metros) e o quanto cada cotovelo é puxado ([2] e [3], somado à direção dele).
            /// </summary>
            public static Vector3[] AjusteZero() => new Vector3[4];

            /// <summary>Os alvos das duas mãos na pose de agora (o tronco e a cabeça já postos), com o ajuste.</summary>
            Alvo[] Alvos(string clipe, float t, Vector3[] ajuste)
            {
                var alvos = new Alvo[2];
                Vector3 fC = _peito.TransformDirection(_fC).normalized, uC = _peito.TransformDirection(_uC).normalized, rC = _peito.TransformDirection(_rC).normalized;
                Vector3 fH = _cabeca.TransformDirection(_fH).normalized, uH = _cabeca.TransformDirection(_uH).normalized, rH = _cabeca.TransformDirection(_rH).normalized;
                var mao = new[] { _peito.TransformDirection(ajuste[0]), _peito.TransformDirection(ajuste[1]) };
                var cot = new[] { _peito.TransformDirection(ajuste[2]), _peito.TransformDirection(ajuste[3]) };
                if (clipe == "AgoniaPele")
                {
                    // um antebraço deitado na frente da barriga; a outra mão esfrega ele (troca no meio do laço)
                    Vector3 peito = _peito.TransformPoint(_frentePeito);
                    float lado = MontagemDosZumbis.Lado(t);   // 0: a direita esfrega a esquerda; 1: a esquerda esfrega a direita
                    var a = PeleCom(1, t, peito, fC, uC, rC, mao, cot);
                    var b = PeleCom(0, t, peito, fC, uC, rC, mao, cot);
                    for (int k = 0; k < 2; k++)
                    {
                        alvos[k].palma = Vector3.Lerp(a[k].palma, b[k].palma, lado);
                        alvos[k].polo = Vector3.Lerp(a[k].polo, b[k].polo, lado);
                        alvos[k].mao = Quaternion.Slerp(a[k].mao, b[k].mao, lado);
                    }
                    return alvos;
                }
                if (clipe == "AgoniaRosto")
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var l = _lados[k];
                        // a palma na bochecha e no olho, esfregando em círculos (as duas mãos fora de fase)
                        float fase = l.s < 0 ? 0f : Mathf.PI;
                        Vector3 rosto = _cabeca.TransformPoint(l.s < 0 ? _rostoE : _rostoD);
                        Vector3 esfrega = uH * (0.012f * Onda_(t, 0.6f, fase)) + rH * (0.008f * Onda_(t, 0.6f, fase + Mathf.PI * 0.5f));
                        alvos[k].palma = rosto + fH * (l.meiaMao + 0.006f) + esfrega;
                        // os dedos para cima e um pouco para dentro (para a testa): os pulsos ficam dos lados do queixo
                        alvos[k].mao = GiroDaMao(l, uH - rH * (0.25f * l.s), -fH);
                        alvos[k].polo = -uC + fC * 0.35f + rC * (0.45f * l.s);
                    }
                }
                else
                {
                    // encolhido: os punhos embaixo do queixo, na frente do peito, tremendo; os cotovelos colados embaixo
                    Vector3 queixo = _cabeca.TransformPoint(_queixo), peito = _peito.TransformPoint(_frentePeito);
                    Vector3 meio = Vector3.LerpUnclamped(peito, queixo, _alturaDoEncolher);
                    for (int k = 0; k < 2; k++)
                    {
                        var l = _lados[k];
                        Vector3 treme = rC * (0.004f * Onda_(t, 1f / 6f, l.s * 0.7f)) + uC * (0.003f * Onda_(t, 1f / 6f, 1.1f + l.s));
                        alvos[k].palma = meio + fC * (l.meiaMao + 0.02f) + rC * (0.045f * l.s) + treme;
                        alvos[k].mao = GiroDaMao(l, uC * 0.9f - rC * (0.35f * l.s), -fC);
                        alvos[k].polo = -uC + fC * 0.2f + rC * (0.35f * l.s);
                    }
                }
                for (int k = 0; k < 2; k++)
                {
                    alvos[k].palma += mao[k];
                    alvos[k].polo += cot[k];
                }
                return alvos;
            }

            /// <summary>
            /// Os alvos da Pele com o braço de índice <paramref name="ke"/> esfregando o outro: o esfregado deitado na frente
            /// da barriga, do lado dele para o outro; a palma do que esfrega por cima do antebraço dele, do pulso ao cotovelo.
            /// </summary>
            Alvo[] PeleCom(int ke, float t, Vector3 peito, Vector3 fC, Vector3 uC, Vector3 rC, Vector3[] mao, Vector3[] cot)
            {
                var alvos = new Alvo[2];
                int kd = 1 - ke;
                Lado esfrega = _lados[ke], esfregado = _lados[kd];
                var d = new Alvo
                {
                    palma = peito + fC * 0.30f - uC * 0.10f + rC * (0.10f * esfrega.s) + mao[kd],
                    polo = -uC + rC * (0.7f * esfregado.s) + fC * 0.1f + cot[kd],
                };
                d.mao = GiroDaMao(esfregado, rC * esfrega.s + fC * 0.3f, -uC);
                // onde o antebraço dele fica (o IK sem mexer nos ossos)
                Vector3 punho = d.palma - d.mao * esfregado.meio;
                Vector3 cotovelo = Cotovelo(esfregado, punho, d.polo);
                Vector3 eixo = (punho - cotovelo).normalized;
                Vector3 cima = uC + fC * 0.3f;
                cima = (cima - eixo * Vector3.Dot(cima, eixo)).normalized;
                // o que esfrega: indo e voltando, 7 vezes no laço
                float u = 0.5f + 0.28f * Onda_(t, 3f / 7f);
                var e = new Alvo
                {
                    palma = Vector3.Lerp(cotovelo, punho, u) + cima * (esfregado.raioDoAntebraco + esfrega.meiaMao + 0.006f) + mao[ke],
                    polo = -uC + rC * (0.6f * esfrega.s) + fC * 0.2f + cot[ke],
                };
                e.mao = GiroDaMao(esfrega, cotovelo - punho, -cima);
                alvos[kd] = d;
                alvos[ke] = e;
                return alvos;
            }

            /// <summary>Onde o cotovelo fica com o pulso no alvo (o IK de dois ossos, sem mexer em nada).</summary>
            static Vector3 Cotovelo(Lado l, Vector3 punho, Vector3 polo)
            {
                Vector3 s = l.braco.position;
                float a = (l.antebraco.position - s).magnitude, b = (l.mao.position - l.antebraco.position).magnitude;
                Vector3 para = punho - s;
                float d = Mathf.Clamp(para.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
                Vector3 n = para.sqrMagnitude > 1e-8f ? para.normalized : Vector3.down;
                Vector3 lado = polo - n * Vector3.Dot(polo, n);
                if (lado.sqrMagnitude < 1e-8f) lado = (l.antebraco.position - s) - n * Vector3.Dot(l.antebraco.position - s, n);
                lado.Normalize();
                float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
                return s + n * (a * cos) + lado * (a * Mathf.Sqrt(1f - cos * cos));
            }

            /// <summary>
            /// O braço até o alvo: o braço aponta para o cotovelo, gira no próprio eixo até o antebraço dobrar no plano dele
            /// (a dobradiça do cotovelo), o antebraço aponta para o pulso, e a mão fica no giro pedido.
            /// </summary>
            static void Ik(Lado l, Alvo alvo)
            {
                Vector3 punho = alvo.palma - alvo.mao * l.meio;
                Vector3 s = l.braco.position;
                Vector3 cotovelo = Cotovelo(l, punho, alvo.polo);
                l.braco.rotation = Quaternion.FromToRotation(l.antebraco.position - s, cotovelo - s) * l.braco.rotation;
                Vector3 eixo = (l.antebraco.position - s).normalized;
                Vector3 agora = Vector3.Cross(eixo, l.mao.position - l.antebraco.position);
                Vector3 quer = Vector3.Cross(eixo, punho - l.antebraco.position);
                if (agora.sqrMagnitude > 1e-8f && quer.sqrMagnitude > 1e-8f)
                    l.braco.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(agora, quer, eixo), eixo) * l.braco.rotation;
                l.antebraco.rotation = Quaternion.FromToRotation(l.mao.position - l.antebraco.position, punho - l.antebraco.position) * l.antebraco.rotation;
                l.mao.rotation = alvo.mao;
            }

            /// <summary>Os braços da agonia no quadro de agora (os músculos já postos e guardados), com o ajuste dado.</summary>
            public void Posar(string clipe, float t, Vector3[] ajuste)
            {
                Restaurar();
                var alvos = Alvos(clipe, t, ajuste);
                for (int k = 0; k < 2; k++) Ik(_lados[k], alvos[k]);
            }

            /// <summary>Começa a escolha da altura dos punhos no Encolher (antes da gravação dele).</summary>
            public void ComecarAEscolher() => _fundoNaAltura = new float[AlturasDoEncolher.Length];

            /// <summary>Na pose de agora (os músculos guardados), o quanto os braços entram em cada altura dos punhos.</summary>
            public void Experimentar(string clipe, float t)
            {
                if (clipe != "AgoniaEncolher" || _fundoNaAltura == null) return;
                PosarOCorpo();
                for (int i = 0; i < AlturasDoEncolher.Length; i++)
                {
                    _alturaDoEncolher = AlturasDoEncolher[i];
                    Posar(clipe, t, AjusteZero());
                    _fundoNaAltura[i] = Mathf.Max(_fundoNaAltura[i], Mathf.Max(Fundo(_lados[0]), Fundo(_lados[1])));
                }
            }

            /// <summary>A altura mais alta em que os braços entram menos de 3 cm (ou a que entra menos).</summary>
            public float EscolherAAltura()
            {
                int melhor = 0;
                if (_fundoNaAltura != null)
                {
                    melhor = System.Array.FindIndex(_fundoNaAltura, f => f < 0.03f);
                    if (melhor < 0) melhor = System.Array.IndexOf(_fundoNaAltura, _fundoNaAltura.Min());
                }
                _alturaDoEncolher = AlturasDoEncolher[melhor];
                _fundoNaAltura = null;
                return _alturaDoEncolher;
            }

            /// <summary>
            /// O ajuste que tira cada braço de dentro do corpo neste quadro, partindo de <paramref name="inicio"/>: se entra
            /// perto da mão, a mão sai pelo caminho mais curto (a média das saídas dos pontos de dentro); se entra perto do
            /// cotovelo, o cotovelo é puxado para lá (e a mão um pouco); até nenhum ponto do braço ficar a menos da folga (ou
            /// 20 voltas). Deixa os braços postos com ele.
            /// </summary>
            public Vector3[] Medir(string clipe, float t, Vector3[] inicio = null)
            {
                PosarOCorpo();
                var ajuste = inicio != null ? (Vector3[])inicio.Clone() : AjusteZero();
                Vector3 frente = clipe == "AgoniaRosto" ? _cabeca.TransformDirection(_fH).normalized : _peito.TransformDirection(_fC).normalized;
                for (int volta = 0; volta < 20; volta++)
                {
                    Posar(clipe, t, ajuste);
                    bool algum = false;
                    for (int k = 0; k < 2; k++)
                    {
                        Dentro(_lados[k], out float c, out var sc, out float m, out var sm);
                        if (m > 0.001f)
                        {
                            // pelo caminho mais curto, puxado para a frente (do rosto, no Rosto; do peito, nos outros): sem
                            // isso, a mão presa entre a cabeça e os ombros saía por cima da cabeça (no Brutamonte)
                            sm = (sm + frente * 0.6f).normalized;
                            ajuste[k] = Vector3.ClampMagnitude(ajuste[k] + _peito.InverseTransformDirection(sm) * (m + 0.002f), 0.3f);
                            algum = true;
                        }
                        if (c > 0.001f)
                        {
                            // o cotovelo vai para lá e a mão também um pouco (às vezes nenhum lugar do cotovelo, com a mão
                            // onde está, fica fora do corpo: na barriga do Brutamonte)
                            Vector3 sai = _peito.InverseTransformDirection(sc);
                            ajuste[2 + k] = Vector3.ClampMagnitude(ajuste[2 + k] + sai * (6f * c + 0.02f), 2.5f);
                            ajuste[k] = Vector3.ClampMagnitude(ajuste[k] + sai * (0.6f * c), 0.3f);
                            algum = true;
                        }
                    }
                    if (!algum) break;
                }
                return ajuste;
            }

            /// <summary>
            /// Os ajustes de todos os quadros, suavizados no tempo (a média de ~0,1 s para cada lado; no laço, dando a volta),
            /// para não tremer. Depois, na gravação, cada quadro confere de novo partindo deles (o que faltar é pouco).
            /// </summary>
            public static Vector3[][] Suavizar(Vector3[][] quadros, bool laco)
            {
                int n = quadros.Length;
                if (n < 3) return quadros;
                int periodo = laco ? n - 1 : n;   // no laço, o último quadro é o primeiro
                const int Janela = 4;
                int Q(int i) => laco ? ((i % periodo) + periodo) % periodo : Mathf.Clamp(i, 0, n - 1);
                var saida = new Vector3[n][];
                for (int i = 0; i < n; i++)
                {
                    saida[i] = new Vector3[4];
                    for (int j = 0; j < 4; j++)
                    {
                        Vector3 soma = Vector3.zero;
                        float pesos = 0f;
                        for (int d = -Janela; d <= Janela; d++)
                        {
                            float peso = Mathf.Exp(-0.5f * d * d / 4f);
                            soma += quadros[Q(i + d)][j] * peso;
                            pesos += peso;
                        }
                        saida[i][j] = soma / pesos;
                    }
                }
                if (laco) saida[n - 1] = saida[0];
                return saida;
            }
        }
    }
}
