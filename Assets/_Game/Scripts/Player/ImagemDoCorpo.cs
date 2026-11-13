using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A imagem do personagem na aba CORPO da mochila (11/11/2026, a pedido: "ser o nosso personagem real ali", em 2D, para
    /// não repetir o retrato 3D da coluna da esquerda). Uma foto parada, feita só quando o personagem, a roupa, a forma do
    /// corpo, a pele, a proteção de sucata ou o tamanho da tela mudam (<see cref="ChaveDe"/>), nunca a cada quadro:
    /// - uma cópia do corpo do jogador (como a do <see cref="RetratoDoJogador"/>, vestida igual, com a proteção de sucata e
    ///   sem o que está na mão, as armas e a mochila), de frente, com os braços e as pernas um pouco abertos (cada membro fica
    ///   separado), longe de tudo e na camada do retrato;
    /// - cada malha (o corpo, as roupas, a proteção) é congelada na pose (<c>BakeMesh</c>, com a forma da mulher) e
    ///   desenhada uma vez por uma câmera ortográfica, no material do retrato (a luz fixa, suave e igual em tudo), numa
    ///   textura do tamanho exato em que aparece na tela (com o antisserrilhado 4×);
    /// - a máscara das partes: as mesmas malhas são rasterizadas aqui (com o teste de profundidade), e cada pixel fica com a
    ///   parte do osso que mais pesa no vértice mais perto dele (a cabeça e o pescoço; o tronco, o quadril e os ombros; os
    ///   braços até os dedos; as pernas até os pés). Assim a máscara acompanha a roupa, a forma do corpo e a proteção;
    /// - a imagem colorida (<see cref="Pintar"/>, refeita só quando a cor de alguma parte muda): o personagem quase sem cor,
    ///   com o tom da vida de cada parte por cima (verde, amarelo, vermelho; o membro inutilizado, cinza riscado) e uma
    ///   costura escura entre as partes;
    /// - para o mouse e a escolhida: a máscara e o contorno de cada parte (<see cref="Mascara"/>, <see cref="Contorno"/>,
    ///   feitos na primeira vez que pedem), a parte num ponto (<see cref="ParteEm"/>), o meio, o eixo e a ponta de cada uma
    ///   (os ícones do estado).
    /// As medidas são em pixels da imagem, com o y de cima para baixo (como o GUI).
    /// </summary>
    public class ImagemDoCorpo
    {
        const int Q = PartesDoCorpo.Quantas;
        // longe da cidade e do retrato (que fica em 0, -500, 0 e só enxerga 1 m em volta)
        static readonly Vector3 Lugar = new Vector3(60f, -500f, 0f);

        // a pose, no espaço do corpo (olhando para +Z; +X é a direita dele): os braços abertos uns 22° e as pernas um pouco
        // afastadas. A esquerda é o espelho (x negativo)
        static readonly Vector3 Braco = new Vector3(0.37f, -0.93f, 0.02f);
        static readonly Vector3 Antebraco = new Vector3(0.4f, -0.91f, 0.07f);
        static readonly Vector3 Coxa = new Vector3(0.085f, -1f, 0f);
        static readonly Vector3 Canela = new Vector3(0.07f, -1f, -0.01f);

        // o osso de cada parte (os outros sobem pela hierarquia até um destes: os dedos até a mão, o dedão do pé até o pé)
        static readonly Dictionary<string, ParteDoCorpo> OssoDaParte = new Dictionary<string, ParteDoCorpo>
        {
            { "head", ParteDoCorpo.Cabeca }, { "neck", ParteDoCorpo.Cabeca },
            { "chest", ParteDoCorpo.Tronco }, { "spine", ParteDoCorpo.Tronco }, { "hips", ParteDoCorpo.Tronco }, { "root", ParteDoCorpo.Tronco },
            { "shoulder.L", ParteDoCorpo.Tronco }, { "shoulder.R", ParteDoCorpo.Tronco },
            { "upper_arm.L", ParteDoCorpo.BracoE }, { "forearm.L", ParteDoCorpo.BracoE }, { "hand.L", ParteDoCorpo.BracoE },
            { "upper_arm.R", ParteDoCorpo.BracoD }, { "forearm.R", ParteDoCorpo.BracoD }, { "hand.R", ParteDoCorpo.BracoD },
            { "thigh.L", ParteDoCorpo.PernaE }, { "shin.L", ParteDoCorpo.PernaE }, { "foot.L", ParteDoCorpo.PernaE }, { "toe.L", ParteDoCorpo.PernaE },
            { "thigh.R", ParteDoCorpo.PernaD }, { "shin.R", ParteDoCorpo.PernaD }, { "foot.R", ParteDoCorpo.PernaD }, { "toe.R", ParteDoCorpo.PernaD },
        };

        public int Largura { get; private set; }
        public int Altura { get; private set; }
        /// <summary>A imagem colorida pela vida das partes (null até a primeira <see cref="Pintar"/>).</summary>
        public Texture2D Imagem => _colorida;
        /// <summary>A chave do que foi fotografado (<see cref="ChaveDe"/>); -1: nada.</summary>
        public long Chave { get; private set; } = -1;
        public bool Pronta => _base != null;

        Texture2D _colorida;
        Color32[] _base, _saida;
        byte[] _partes;      // a parte + 1 de cada pixel (0: fora do corpo), com a linha 0 embaixo (como a textura)
        bool[] _costura;     // o pixel na divisa de duas partes
        readonly RectInt[] _caixa = new RectInt[Q];        // y de cima para baixo
        readonly Vector2[] _centro = new Vector2[Q], _eixo = new Vector2[Q], _ponta = new Vector2[Q];
        readonly bool[] _temParte = new bool[Q];
        readonly Texture2D[] _mascara = new Texture2D[Q], _contorno = new Texture2D[Q];
        readonly RectInt[] _caixaDoContorno = new RectInt[Q];
        readonly int[] _pintadas = new int[Q];
        int _espessura = 2;

        /// <summary>
        /// O que a foto mostra: o corpo do personagem, a versão da roupa (as peças, as cores, a forma e a pele), a proteção de
        /// sucata vestida e o tamanho. Mudou, a foto é feita de novo.
        /// </summary>
        public static long ChaveDe(JogadorRede rede, InventarioJogador inventario, int largura, int altura)
        {
            if (rede == null || rede.animadorCorpo == null) return -1;
            long k = rede.animadorCorpo.GetHashCode();   // (o objeto: outro personagem, outra foto)
            k = k * 7919 + (rede.Vestimenta != null ? rede.Vestimenta.Versao + 1 : 0);
            int bits = 0;
            if (inventario != null && inventario.IsSpawned)
            {
                var p = inventario.Protecao.Value;
                for (int i = 0; i < Protecao.Quantos; i++)
                    if (p.Peca((LugarDaProtecao)i) != Item.Nada) bits |= 1 << i;
            }
            k = k * 31 + bits;
            k = k * 8191 + largura;
            k = k * 8191 + altura;
            return k;
        }

        /// <summary>
        /// Faz a foto (e a máscara das partes) agora, do tamanho dado (em pixels da tela). false: não deu (sem o corpo ou o
        /// material; a aba volta para o boneco desenhado).
        /// </summary>
        public bool Gerar(JogadorRede rede, InventarioJogador inventario, Material retrato, int largura, int altura, float u)
        {
            Limpar();
            Chave = ChaveDe(rede, inventario, largura, altura);
            if (rede == null || rede.animadorCorpo == null || retrato == null || largura < 8 || altura < 8) return false;
            Largura = largura;
            Altura = altura;
            _espessura = Mathf.Max(2, Mathf.RoundToInt(1.6f * u));
            var materiais = new List<Material>();
            var malhas = new List<(Mesh malha, Matrix4x4 mundo, byte[] partes)>();
            GameObject raiz = null;
            RenderTexture rt = null;
            try
            {
                raiz = new GameObject("Imagem do corpo (temporária)");
                raiz.transform.position = Lugar;
                var original = rede.animadorCorpo;
                var copia = Object.Instantiate(original.gameObject, raiz.transform, false);
                copia.name = "Corpo";
                // o que vem junto na cópia e não entra: o equipamento, os curativos e a proteção do corpo do jogador (a
                // proteção é posta de novo abaixo, com a deste corpo)
                EquipamentoNoCorpo.TirarCopiadas(copia.transform);
                foreach (var c in copia.GetComponents<EquipamentoNoCorpo>()) Object.Destroy(c);
                CurativosNoCorpo.TirarCopiadas(copia.transform);
                foreach (var c in copia.GetComponents<CurativosNoCorpo>()) Object.Destroy(c);
                ProtecaoNoCorpo.TirarCopiadas(copia.transform);
                foreach (var c in copia.GetComponents<ProtecaoNoCorpo>()) Object.Destroy(c);
                copia.transform.localPosition = Vector3.zero;
                copia.transform.localRotation = Quaternion.identity;
                var animador = copia.GetComponent<Animator>();
                if (animador != null) animador.enabled = false;
                var vestimenta = copia.GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
                if (vestimenta != null && rede.Vestimenta != null) vestimenta.VestirComo(rede.Vestimenta);
                // a pose de repouso do prefab (a do mesmo personagem) e, dela, os braços e as pernas abertos
                var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
                var referencia = prefab != null ? prefab.transform.Find(original.name) : null;
                if (referencia != null) RetratoDoJogador.Repouso(referencia, copia.transform);
                var ossos = new Dictionary<string, Transform>();
                foreach (var t in copia.GetComponentsInChildren<Transform>(true))
                    if (!ossos.ContainsKey(t.name)) ossos[t.name] = t;
                Posar(copia.transform, ossos);

                // a proteção de sucata vestida (as mesmas peças do ProtecaoNoCorpo, nos mesmos ossos)
                string forma = vestimenta != null ? vestimenta.corpo : null;
                if (inventario != null && inventario.IsSpawned && PecasDaProtecao.Dados != null)
                {
                    var p = inventario.Protecao.Value;
                    for (int k = 0; k < PecasDaProtecao.Pecas.Length; k++)
                    {
                        var (objeto, osso, lugar) = PecasDaProtecao.Pecas[k];
                        if (p.Peca(lugar) == Item.Nada) continue;
                        var malha = PecasDaProtecao.Peca(k);
                        if (malha == null || !ossos.TryGetValue(osso, out var t)) continue;
                        PecasDaProtecao.Por(t, malha, PecasDaProtecao.Dados.material, ProtecaoNoCorpo.Prefixo + objeto, copia.layer, forma);
                    }
                }

                // as malhas que entram: o corpo, as roupas e a proteção (o que está na mão, as armas, o fumo e o resto, não)
                var renderers = new List<SkinnedMeshRenderer>();
                foreach (var s in copia.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                    if (s.sharedMesh != null && Entra(s.name)) renderers.Add(s);
                if (renderers.Count == 0) return false;

                // a parte de cada osso (subindo pela hierarquia até um conhecido; nenhum: o tronco)
                var parteDoOsso = new Dictionary<Transform, byte>();
                byte ParteDe(Transform t)
                {
                    if (t == null) return (byte)ParteDoCorpo.Tronco;
                    if (parteDoOsso.TryGetValue(t, out var ja)) return ja;
                    var o = t;
                    byte r = (byte)ParteDoCorpo.Tronco;
                    while (o != null && o != copia.transform)
                    {
                        if (OssoDaParte.TryGetValue(o.name, out var pp))
                        {
                            r = (byte)pp;
                            break;
                        }
                        o = o.parent;
                    }
                    parteDoOsso[t] = r;
                    return r;
                }

                // congela cada malha na pose (com a forma do corpo) e põe um MeshRenderer com ela, no material do retrato
                foreach (var s in renderers)
                {
                    var m = new Mesh { name = "Imagem do corpo: " + s.name };
                    s.BakeMesh(m, true);
                    var mundo = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                    var partes = new byte[m.vertexCount];
                    var pesos = s.sharedMesh.boneWeights;
                    var ossosDaMalha = s.bones;
                    byte fixa = ParteDe(s.transform.parent);
                    if (pesos != null && pesos.Length == partes.Length && ossosDaMalha != null && ossosDaMalha.Length > 0)
                    {
                        for (int i = 0; i < partes.Length; i++)
                        {
                            var w = pesos[i];
                            int osso = w.boneIndex0;
                            float maior = w.weight0;
                            if (w.weight1 > maior) { maior = w.weight1; osso = w.boneIndex1; }
                            if (w.weight2 > maior) { maior = w.weight2; osso = w.boneIndex2; }
                            if (w.weight3 > maior) { osso = w.boneIndex3; }
                            partes[i] = osso >= 0 && osso < ossosDaMalha.Length ? ParteDe(ossosDaMalha[osso]) : fixa;
                        }
                    }
                    else for (int i = 0; i < partes.Length; i++) partes[i] = fixa;
                    malhas.Add((m, mundo, partes));

                    var go = new GameObject(s.name) { layer = JogadorRede.CamadaRetrato };
                    go.transform.SetParent(raiz.transform, false);
                    go.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    var mats = s.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = RetratoDoJogador.CopiaDoMaterial(retrato, mats[i], materiais);
                    mr.sharedMaterials = mats;
                    // as cores das peças (o MaterialPropertyBlock da Vestimenta)
                    var bloco = new MaterialPropertyBlock();
                    s.GetPropertyBlock(bloco);
                    if (!bloco.isEmpty) mr.SetPropertyBlock(bloco);
                    for (int i = 0; i < mats.Length; i++)
                    {
                        bloco.Clear();
                        s.GetPropertyBlock(bloco, i);
                        if (!bloco.isEmpty) mr.SetPropertyBlock(bloco, i);
                    }
                }

                // os ossos na imagem (depois da câmera): o ombro e a mão, o quadril e o pé
                Vector3 Osso(string nome) => ossos.TryGetValue(nome, out var t) ? t.position : Lugar;
                var ombroD = Osso("upper_arm.R"); var maoD = Osso("hand.R");
                var ombroE = Osso("upper_arm.L"); var maoE = Osso("hand.L");
                var quadrilD = Osso("thigh.R"); var peD = Osso("foot.R");
                var quadrilE = Osso("thigh.L"); var peE = Osso("foot.L");
                copia.SetActive(false);

                // a câmera ortográfica de frente, com o corpo inteiro (e uma folga pequena) na textura
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var (m, mundo, _) in malhas)
                {
                    var vs = m.vertices;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        var w = mundo.MultiplyPoint3x4(vs[i]);
                        min = Vector3.Min(min, w);
                        max = Vector3.Max(max, w);
                    }
                }
                float aspecto = largura / (float)altura;
                float meia = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspecto) * 1.03f;
                var camGo = new GameObject("Câmera da imagem do corpo") { layer = JogadorRede.CamadaRetrato };
                camGo.transform.SetParent(raiz.transform, false);
                camGo.transform.position = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z + 2f);
                camGo.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = meia;
                cam.aspect = aspecto;   // (sem isto, a projeção da máscara saía com o aspecto da tela)
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = (max.z - min.z) + 4f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << JogadorRede.CamadaRetrato;
                var dados = camGo.AddComponent<UniversalAdditionalCameraData>();
                dados.renderPostProcessing = false;
                dados.renderShadows = false;
                dados.antialiasing = AntialiasingMode.None;
                // o renderizador 1 (o dos braços e do retrato) não tem a neblina de altura
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
                rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Imagem do corpo" };
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var lida = new Texture2D(largura, altura, TextureFormat.RGBA32, false);
                lida.ReadPixels(new Rect(0, 0, largura, altura), 0, 0, false);
                lida.Apply(false);
                RenderTexture.active = antes;
                _base = lida.GetPixels32();
                Object.Destroy(lida);
                cam.targetTexture = null;
                // a cor sem a cobertura da borda (o antisserrilhado mistura com o fundo transparente, preto)
                for (int i = 0; i < _base.Length; i++)
                {
                    var c = _base[i];
                    if (c.a == 0 || c.a == 255) continue;
                    float f = 255f / c.a;
                    _base[i] = new Color32((byte)Mathf.Min(255, c.r * f), (byte)Mathf.Min(255, c.g * f), (byte)Mathf.Min(255, c.b * f), c.a);
                }

                // a máscara das partes: as malhas rasterizadas aqui, com a profundidade
                var vp = cam.projectionMatrix * cam.worldToCameraMatrix;
                var vista = cam.worldToCameraMatrix;
                int n = largura * altura;
                var z = new float[n];
                for (int i = 0; i < n; i++) z[i] = float.MaxValue;
                _partes = new byte[n];
                foreach (var (m, mundo, partes) in malhas)
                {
                    var vs = m.vertices;
                    var tela = new Vector3[vs.Length];
                    for (int i = 0; i < vs.Length; i++)
                    {
                        var w = mundo.MultiplyPoint3x4(vs[i]);
                        var c = vp * new Vector4(w.x, w.y, w.z, 1f);
                        float iw = Mathf.Abs(c.w) > 1e-6f ? 1f / c.w : 1f;
                        tela[i] = new Vector3((c.x * iw * 0.5f + 0.5f) * largura, (c.y * iw * 0.5f + 0.5f) * altura, -vista.MultiplyPoint3x4(w).z);
                    }
                    var tri = m.triangles;
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                        Rasterizar(tela[tri[i]], tela[tri[i + 1]], tela[tri[i + 2]], partes[tri[i]], partes[tri[i + 1]], partes[tri[i + 2]], z);
                }
                // a borda: o pixel que aparece e ficou sem parte pega a do vizinho; o que não aparece, nenhuma
                for (int passo = 0; passo < 3; passo++)
                    for (int y = 0; y < altura; y++)
                        for (int x = 0; x < largura; x++)
                        {
                            int i = y * largura + x;
                            if (_partes[i] != 0 || _base[i].a < 8) continue;
                            byte v = x > 0 ? _partes[i - 1] : (byte)0;
                            if (v == 0 && x + 1 < largura) v = _partes[i + 1];
                            if (v == 0 && y > 0) v = _partes[i - largura];
                            if (v == 0 && y + 1 < altura) v = _partes[i + largura];
                            _partes[i] = v;
                        }
                for (int i = 0; i < n; i++)
                    if (_base[i].a < 8) _partes[i] = 0;

                // a costura (a divisa entre duas partes) e, de cada parte, a caixa e o meio
                _costura = new bool[n];
                var soma = new Vector2[Q];
                var conta = new int[Q];
                var mins = new Vector2Int[Q];
                var maxs = new Vector2Int[Q];
                for (int p = 0; p < Q; p++)
                {
                    mins[p] = new Vector2Int(int.MaxValue, int.MaxValue);
                    maxs[p] = new Vector2Int(int.MinValue, int.MinValue);
                }
                for (int y = 0; y < altura; y++)
                    for (int x = 0; x < largura; x++)
                    {
                        int i = y * largura + x;
                        int v = _partes[i];
                        if (v == 0) continue;
                        if ((x + 1 < largura && _partes[i + 1] != 0 && _partes[i + 1] != v) || (y + 1 < altura && _partes[i + largura] != 0 && _partes[i + largura] != v))
                            _costura[i] = true;
                        int p = v - 1;
                        int yc = altura - 1 - y;   // de cima para baixo
                        soma[p] += new Vector2(x + 0.5f, yc + 0.5f);
                        conta[p]++;
                        mins[p] = Vector2Int.Min(mins[p], new Vector2Int(x, yc));
                        maxs[p] = Vector2Int.Max(maxs[p], new Vector2Int(x, yc));
                    }
                Vector2 NaImagem(Vector3 mundo)
                {
                    var c = vp * new Vector4(mundo.x, mundo.y, mundo.z, 1f);
                    float iw = Mathf.Abs(c.w) > 1e-6f ? 1f / c.w : 1f;
                    return new Vector2((c.x * iw * 0.5f + 0.5f) * largura, altura - (c.y * iw * 0.5f + 0.5f) * altura);
                }
                for (int p = 0; p < Q; p++)
                {
                    _temParte[p] = conta[p] > 0;
                    if (!_temParte[p]) continue;
                    _caixa[p] = new RectInt(mins[p].x, mins[p].y, maxs[p].x - mins[p].x + 1, maxs[p].y - mins[p].y + 1);
                    _centro[p] = soma[p] / conta[p];
                    _eixo[p] = new Vector2(0f, 1f);   // na cabeça e no tronco, os ícones empilham para baixo
                    _ponta[p] = new Vector2(_centro[p].x, _caixa[p].yMax - 4f);
                }
                // os membros: o eixo do ombro à mão (do quadril ao pé), o meio nele e a ponta no pulso (no tornozelo)
                void Membro(ParteDoCorpo parte, Vector3 de, Vector3 ate)
                {
                    int p = (int)parte;
                    if (!_temParte[p]) return;
                    Vector2 a = NaImagem(de), b = NaImagem(ate);
                    if ((b - a).sqrMagnitude < 4f) return;
                    _eixo[p] = (b - a).normalized;
                    _centro[p] = Vector2.Lerp(a, b, 0.48f);
                    _ponta[p] = b;
                }
                Membro(ParteDoCorpo.BracoD, ombroD, maoD);
                Membro(ParteDoCorpo.BracoE, ombroE, maoE);
                Membro(ParteDoCorpo.PernaD, quadrilD, peD);
                Membro(ParteDoCorpo.PernaE, quadrilE, peE);
                // o tronco: os ícones um pouco acima do meio (o peito)
                if (_temParte[(int)ParteDoCorpo.Tronco])
                {
                    var c = _caixa[(int)ParteDoCorpo.Tronco];
                    _centro[(int)ParteDoCorpo.Tronco] = new Vector2(_centro[(int)ParteDoCorpo.Tronco].x, c.y + c.height * 0.42f);
                }
                _saida = new Color32[n];
                for (int p = 0; p < Q; p++) _pintadas[p] = int.MinValue;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"A imagem do corpo não saiu (a aba CORPO usa o boneco desenhado): {e.Message}");
                _base = null;
                return false;
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    Object.Destroy(rt);
                }
                if (raiz != null)
                {
                    raiz.SetActive(false);
                    Object.Destroy(raiz);
                }
                foreach (var m in materiais) Object.Destroy(m);
                foreach (var (m, _, _) in malhas) Object.Destroy(m);
            }
        }

        // as peças que entram na foto: o corpo, as roupas e a proteção de sucata
        static bool Entra(string nome) =>
            nome == "NPC_Corpo" || nome == "Jogador_Corpo" || nome.StartsWith("Roupa_", System.StringComparison.Ordinal)
            || nome.StartsWith(ProtecaoNoCorpo.Prefixo, System.StringComparison.Ordinal);

        // os braços abertos e as pernas afastadas (o osso gira até o eixo Y dele, que vai ao longo dele, apontar para a
        // direção, no espaço do corpo); a mão e o pé vão junto
        static void Posar(Transform corpo, Dictionary<string, Transform> ossos)
        {
            void Apontar(string nome, Vector3 direcao)
            {
                if (!ossos.TryGetValue(nome, out var osso)) return;
                var alvo = corpo.TransformDirection(direcao.normalized);
                osso.rotation = Quaternion.FromToRotation(osso.up, alvo) * osso.rotation;
            }
            var espelho = new Vector3(-1f, 1f, 1f);
            Apontar("upper_arm.R", Braco);
            Apontar("forearm.R", Antebraco);
            Apontar("hand.R", Antebraco);
            Apontar("upper_arm.L", Vector3.Scale(Braco, espelho));
            Apontar("forearm.L", Vector3.Scale(Antebraco, espelho));
            Apontar("hand.L", Vector3.Scale(Antebraco, espelho));
            Apontar("thigh.R", Coxa);
            Apontar("shin.R", Canela);
            Apontar("thigh.L", Vector3.Scale(Coxa, espelho));
            Apontar("shin.L", Vector3.Scale(Canela, espelho));
        }

        // um triângulo na máscara: o pixel (no meio dele) dentro, mais perto que o de antes, fica com a parte do vértice que
        // mais pesa nele
        void Rasterizar(Vector3 a, Vector3 b, Vector3 c, byte pa, byte pb, byte pc, float[] z)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 1e-6f) return;
            float inv = 1f / area;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(Largura - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(Altura - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) * inv;
                    if (w0 < 0f) continue;
                    float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) * inv;
                    if (w1 < 0f) continue;
                    float w2 = 1f - w0 - w1;
                    if (w2 < 0f) continue;
                    float d = w0 * a.z + w1 * b.z + w2 * c.z;
                    int i = y * Largura + x;
                    if (d >= z[i]) continue;
                    z[i] = d;
                    _partes[i] = (byte)((w0 >= w1 && w0 >= w2 ? pa : w1 >= w2 ? pb : pc) + 1);
                }
            }
        }

        /// <summary>
        /// A imagem colorida, refeita só quando a cor de alguma parte muda: o personagem quase sem cor e o tom da parte por
        /// cima (mais forte na machucada), a costura escura entre as partes e, na inutilizada, o cinza riscado.
        /// </summary>
        public void Pintar(Color[] cores, bool[] fortes, bool[] inutil)
        {
            if (_base == null) return;
            bool mudou = _colorida == null;
            for (int p = 0; p < Q; p++)
            {
                int chave = ((Color32)cores[p]).GetHashCode() * 4 + (fortes[p] ? 1 : 0) + (inutil[p] ? 2 : 0);
                if (chave != _pintadas[p]) mudou = true;
                _pintadas[p] = chave;
            }
            if (!mudou) return;
            var tons = new Vector3[Q];
            var forca = new float[Q];
            for (int p = 0; p < Q; p++)
            {
                tons[p] = new Vector3(cores[p].r, cores[p].g, cores[p].b);
                forca[p] = inutil[p] ? 0.75f : fortes[p] ? 0.62f : 0.36f;
            }
            int riscas = Mathf.Max(4, Largura / 28);
            for (int y = 0; y < Altura; y++)
                for (int x = 0; x < Largura; x++)
                {
                    int i = y * Largura + x;
                    var c = _base[i];
                    if (c.a == 0)
                    {
                        _saida[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                    float lum = 0.3f * r + 0.59f * g + 0.11f * b;
                    // quase sem cor (fica um pouco da roupa)
                    r = Mathf.Lerp(lum, r, 0.3f);
                    g = Mathf.Lerp(lum, g, 0.3f);
                    b = Mathf.Lerp(lum, b, 0.3f);
                    int v = _partes[i];
                    if (v != 0)
                    {
                        int p = v - 1;
                        var t = tons[p];
                        // o tom com a luz da foto (o relevo da roupa continua)
                        float luz = 0.32f + 0.95f * lum;
                        r = Mathf.Lerp(r, t.x * luz, forca[p]);
                        g = Mathf.Lerp(g, t.y * luz, forca[p]);
                        b = Mathf.Lerp(b, t.z * luz, forca[p]);
                        if (inutil[p] && ((x + Altura - y) / riscas) % 2 == 0)
                        {
                            r *= 0.62f;
                            g *= 0.62f;
                            b *= 0.62f;
                        }
                        if (_costura[i])
                        {
                            r *= 0.45f;
                            g *= 0.45f;
                            b *= 0.45f;
                        }
                    }
                    _saida[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), c.a);
                }
            if (_colorida == null)
                _colorida = new Texture2D(Largura, Altura, TextureFormat.RGBA32, false) { name = "Imagem do corpo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _colorida.SetPixels32(_saida);
            _colorida.Apply(false);
        }

        /// <summary>A parte no ponto (em pixels da imagem, y de cima para baixo), com uma folga de 3 pixels; false: nenhuma.</summary>
        public bool ParteEm(Vector2 ponto, out ParteDoCorpo parte)
        {
            parte = PartesDoCorpo.Sortear;
            if (_partes == null) return false;
            int cx = Mathf.FloorToInt(ponto.x), cy = Altura - 1 - Mathf.FloorToInt(ponto.y);
            for (int raio = 0; raio <= 3; raio++)
                for (int dy = -raio; dy <= raio; dy++)
                    for (int dx = -raio; dx <= raio; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != raio) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= Largura || y >= Altura) continue;
                        int v = _partes[y * Largura + x];
                        if (v == 0) continue;
                        parte = (ParteDoCorpo)(v - 1);
                        return true;
                    }
            return false;
        }

        public bool TemParte(ParteDoCorpo p) => _base != null && PartesDoCorpo.Valida(p) && _temParte[(int)p];
        /// <summary>A caixa da parte na imagem (y de cima para baixo).</summary>
        public RectInt Caixa(ParteDoCorpo p) => _caixa[(int)p];
        /// <summary>Onde os ícones da parte ficam (o meio deles), em pixels da imagem.</summary>
        public Vector2 Centro(ParteDoCorpo p) => _centro[(int)p];
        /// <summary>Para onde os ícones empilham (ao longo do membro; na cabeça e no tronco, para baixo).</summary>
        public Vector2 Eixo(ParteDoCorpo p) => _eixo[(int)p];
        /// <summary>A ponta do membro (o pulso, o tornozelo): o anel da entorse.</summary>
        public Vector2 Ponta(ParteDoCorpo p) => _ponta[(int)p];

        /// <summary>A parte inteira em branco (a cobertura no alfa), na caixa dela: o brilho do mouse e do curativo.</summary>
        public Texture2D Mascara(ParteDoCorpo parte, out RectInt caixa)
        {
            caixa = default;
            if (!TemParte(parte)) return null;
            int p = (int)parte;
            caixa = _caixa[p];
            if (_mascara[p] != null) return _mascara[p];
            var c = caixa;
            var px = new Color32[c.width * c.height];
            for (int y = 0; y < c.height; y++)
                for (int x = 0; x < c.width; x++)
                {
                    int yi = Altura - 1 - (c.y + y);
                    int i = yi * Largura + c.x + x;
                    bool dentro = _partes[i] == p + 1;
                    // a linha da textura de baixo para cima
                    px[(c.height - 1 - y) * c.width + x] = new Color32(255, 255, 255, dentro ? _base[i].a : (byte)0);
                }
            _mascara[p] = Textura(px, c.width, c.height, "Máscara da parte");
            return _mascara[p];
        }

        /// <summary>
        /// O contorno da parte em branco (por fora dela, da espessura da tela, e um pixel por dentro), na caixa dele: o mouse
        /// em cima, a escolhida e a infectada.
        /// </summary>
        public Texture2D Contorno(ParteDoCorpo parte, out RectInt caixa)
        {
            caixa = default;
            if (!TemParte(parte)) return null;
            int p = (int)parte;
            if (_contorno[p] != null)
            {
                caixa = _caixaDoContorno[p];
                return _contorno[p];
            }
            int e = _espessura;
            var cp = _caixa[p];
            int x0 = Mathf.Max(0, cp.x - e - 1), y0 = Mathf.Max(0, cp.y - e - 1);
            int x1 = Mathf.Min(Largura - 1, cp.xMax + e), y1 = Mathf.Min(Altura - 1, cp.yMax + e);
            caixa = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            int w = caixa.width, h = caixa.height;
            // dentro (y de cima para baixo, na caixa)
            var dentro = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int yi = Altura - 1 - (y0 + y);
                    dentro[y * w + x] = _partes[yi * Largura + x0 + x] == p + 1;
                }
            // a distância até a parte (em quadrado), em duas passadas: na linha e na coluna
            var linha = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool v = false;
                    for (int d = -e; d <= e && !v; d++)
                    {
                        int xx = x + d;
                        v = xx >= 0 && xx < w && dentro[y * w + xx];
                    }
                    linha[y * w + x] = v;
                }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    bool perto = false;
                    for (int d = -e; d <= e && !perto; d++)
                    {
                        int yy = y + d;
                        perto = yy >= 0 && yy < h && linha[yy * w + x];
                    }
                    bool borda;
                    if (dentro[i])
                        borda = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !dentro[i - 1] || !dentro[i + 1] || !dentro[i - w] || !dentro[i + w];
                    else borda = perto;
                    px[(h - 1 - y) * w + x] = new Color32(255, 255, 255, borda ? (byte)255 : (byte)0);
                }
            _contorno[p] = Textura(px, w, h, "Contorno da parte");
            _caixaDoContorno[p] = caixa;
            return _contorno[p];
        }

        static Texture2D Textura(Color32[] px, int w, int h, string nome)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = nome, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        void Limpar()
        {
            if (_colorida != null) Object.Destroy(_colorida);
            _colorida = null;
            for (int p = 0; p < Q; p++)
            {
                if (_mascara[p] != null) Object.Destroy(_mascara[p]);
                if (_contorno[p] != null) Object.Destroy(_contorno[p]);
                _mascara[p] = null;
                _contorno[p] = null;
                _temParte[p] = false;
            }
            _base = null;
            _partes = null;
            _costura = null;
            _saida = null;
        }

        public void Destruir()
        {
            Limpar();
            Chave = -1;
        }
    }
}
