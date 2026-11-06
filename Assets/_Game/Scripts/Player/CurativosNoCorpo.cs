using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os curativos no corpo do jogador (05/11/2026, a etapa 1 do sistema de vida por membro), em todas as máquinas, pelo
    /// <see cref="VidaJogador.Corpo"/> que vem pela rede: a faixa branca da parte enfaixada (um tubo aberto de 8 lados,
    /// de 9 cm, no meio do antebraço ou da canela; um anel na testa; uma faixa larga no peito) e a tala do membro
    /// imobilizado (duas ripas finas laranja-escuras ao longo da canela ou do antebraço, presas por duas tiras brancas).
    /// Como o <see cref="EquipamentoDoJogador"/>: as peças presas aos ossos do corpo (acompanham a animação), medidas no
    /// corpo em repouso (o do mesmo personagem no prefab), refeitas só quando a faixa ou as talas mudam (ou as roupas: a
    /// medida é a da roupa vestida), de novo quando o personagem muda (<see cref="JogadorRede.CorpoMudou"/>); no dono, só
    /// a sombra na primeira pessoa (na camada do dono) e inteiras na terceira. O tamanho sai da malha: a seção de tudo o
    /// que é do osso (o corpo e as roupas, pelos pesos dos vértices), com 1,5 cm de folga na faixa; sem a malha legível,
    /// uma medida padrão. Enquanto ele sangra, uma gota de sangue no chão embaixo dele a cada ~1,5 s, andando ou parado
    /// (<see cref="Sangue.Mancha"/>). Posto pelo <see cref="VidaJogador"/>; o retrato do inventário põe uma cópia dele
    /// (<see cref="NoRetrato"/>), com o material do retrato e sem as gotas.
    /// </summary>
    [DefaultExecutionOrder(110)]   // depois da câmera de terceira pessoa, que mostra e esconde o corpo do dono
    public class CurativosNoCorpo : MonoBehaviour
    {
        /// <summary>O começo do nome de cada peça: a cópia do corpo (o retrato do inventário) acha e tira as que vieram junto.</summary>
        public const string Prefixo = "Curativo: ";

        // as medidas (m)
        const float ComprimentoDaFaixa = 0.09f;
        const float ComprimentoDoAnel = 0.045f;      // na testa
        const float ComprimentoNoPeito = 0.13f;      // a faixa larga do peito
        const float FolgaDaFaixa = 0.015f;           // além da medida do membro
        const float LarguraDaRipa = 0.032f, EspessuraDaRipa = 0.008f, FolgaDaRipa = 0.004f;
        const float LarguraDaTira = 0.025f, FolgaDaTira = 0.004f;
        const float GotaACada = 1.5f;                // s
        const int Lados = 8;

        // as cores (uma célula de cada na textura das peças)
        const int CorDaFaixa = 0, CorDaRipa = 1, CorDaTira = 2, Celulas = 4;

        VidaJogador _vida;
        JogadorRede _rede;
        Transform _corpo, _referencia;
        ZombieCityCleaner.Roupas.Vestimenta _vestimenta;
        Material _material;
        bool _retrato, _aplicado, _visivel;
        long _chave = -1;
        float _proximaGota;
        readonly Dictionary<string, Transform> _ossos = new Dictionary<string, Transform>();
        readonly List<GameObject> _pecas = new List<GameObject>();
        readonly List<Mesh> _malhas = new List<Mesh>();

        static Texture2D _cores;
        static Material _materialDoMundo;

        /// <summary>
        /// A textura das peças (4 × 1, sem filtro): a faixa, a ripa da tala e a tira, uma cor em cada célula. O retrato do
        /// inventário põe ela na cópia do material dele.
        /// </summary>
        public static Texture2D Cores
        {
            get
            {
                if (_cores != null) return _cores;
                _cores = new Texture2D(Celulas, 1, TextureFormat.RGBA32, false)
                {
                    name = "Cores dos curativos",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var faixa = new Color(0.9f, 0.88f, 0.82f);    // a gaze, um pouco encardida
                var ripa = new Color(0.55f, 0.26f, 0.07f);    // laranja-escuro
                var tira = new Color(0.95f, 0.95f, 0.93f);
                _cores.SetPixels(new[] { faixa, ripa, tira, tira });
                _cores.Apply(false, true);
                return _cores;
            }
        }

        // o material das peças no mundo: a cópia do dos itens no chão (URP Lit) com a textura das cores
        static Material MaterialDoMundo()
        {
            if (_materialDoMundo != null) return _materialDoMundo;
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null || mundo.material == null) return null;
            _materialDoMundo = new Material(mundo.material) { name = "Curativos" };
            _materialDoMundo.mainTexture = Cores;
            _materialDoMundo.mainTextureOffset = Vector2.zero;
            _materialDoMundo.mainTextureScale = Vector2.one;
            if (_materialDoMundo.HasProperty("_BaseColor")) _materialDoMundo.SetColor("_BaseColor", Color.white);
            return _materialDoMundo;
        }

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
            if (_rede != null) _rede.CorpoMudou += AoMudarCorpo;
        }

        void OnDestroy()
        {
            if (_rede != null) _rede.CorpoMudou -= AoMudarCorpo;
            Limpar();
        }

        /// <summary>
        /// No retrato do inventário (a cópia do corpo, onde este componente é posto): lê o corpo do jogador
        /// (<paramref name="vida"/>), mede no corpo em repouso (<paramref name="referencia"/>) e usa o material do retrato.
        /// Sem sombra e sem as gotas.
        /// </summary>
        public void NoRetrato(VidaJogador vida, Transform referencia, Material material)
        {
            if (_rede != null) _rede.CorpoMudou -= AoMudarCorpo;
            _rede = null;
            _retrato = true;
            _vida = vida;
            _referencia = referencia;
            _material = material;
            _corpo = transform;
            _vestimenta = GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
            LerOssos();
            Limpar();
            _chave = -1;
        }

        /// <summary>Tira as peças que vieram numa cópia do corpo (Instantiate leva os filhos dos ossos, não a lista).</summary>
        public static void TirarCopiadas(Transform raiz)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith(Prefixo, System.StringComparison.Ordinal)) continue;
                t.gameObject.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(t.gameObject);
            }
        }

        // o personagem chegou pela rede (outro corpo): as peças são feitas de novo nele
        void AoMudarCorpo()
        {
            Limpar();
            _corpo = null;
            _referencia = null;
            _vestimenta = null;
            _ossos.Clear();
            _chave = -1;
        }

        // o corpo do jogador e o mesmo corpo em repouso, no prefab
        bool AcharCorpo()
        {
            if (_corpo != null) return _referencia != null;
            if (_rede == null || _rede.animadorCorpo == null) return false;
            _corpo = _rede.animadorCorpo.transform;
            var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
            _referencia = prefab != null ? prefab.transform.Find(_corpo.name) : null;
            _vestimenta = _corpo.GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
            LerOssos();
            _chave = -1;
            return _referencia != null;
        }

        void LerOssos()
        {
            _ossos.Clear();
            if (_corpo == null) return;
            foreach (var t in _corpo.GetComponentsInChildren<Transform>(true))
                if (!_ossos.ContainsKey(t.name)) _ossos[t.name] = t;
        }

        void LateUpdate()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            if (!_retrato && !AcharCorpo()) return;
            if (_corpo == null || _referencia == null) return;
            var e = _vida.Corpo.Value;
            if (!_retrato) Pingar(e);

            var material = _retrato ? _material : MaterialDoMundo();
            if (material == null) return;
            // a chave: a faixa (6 bits) e as talas (4 bits); e as roupas, que mudam a medida
            long versao = _vestimenta != null ? _vestimenta.Versao & 0xFFFFFF : 0;
            long chave = e.faixa | (long)e.talas << 8 | versao << 16;
            if (chave != _chave)
            {
                _chave = chave;
                Refazer(e, material);
                _aplicado = false;
            }
            Aplicar();
        }

        // no dono, como o corpo: só a sombra na primeira pessoa (na camada do dono) e inteiras na terceira
        void Aplicar()
        {
            bool visivel = _retrato || _rede == null || !_rede.IsOwner || _rede.CorpoVisivelNoDono;
            if (_aplicado && visivel == _visivel) return;
            _aplicado = true;
            _visivel = visivel;
            foreach (var peca in _pecas)
            {
                if (peca == null) continue;
                var r = peca.GetComponent<MeshRenderer>();
                if (r == null) continue;
                if (_retrato)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    continue;
                }
                r.shadowCastingMode = visivel ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                r.renderingLayerMask = visivel ? 1u : JogadorRede.MascaraDoDono;
            }
        }

        void Limpar()
        {
            foreach (var p in _pecas)
            {
                if (p == null) continue;
                p.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(p);
            }
            _pecas.Clear();
            foreach (var m in _malhas)
                if (m != null) Destroy(m);
            _malhas.Clear();
        }

        // ------------------------------------------------------------------ a gota

        // enquanto alguma parte sangra: uma gota no chão embaixo dela, a cada ~1,5 s (andando ou parado); caído ou no carro, não
        void Pingar(in EstadoDoCorpo e)
        {
            if (!e.AlgumSangrando || Time.time < _proximaGota) return;
            _proximaGota = Time.time + GotaACada * Random.Range(0.8f, 1.2f);
            if (_vida.Caido.Value) return;
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro) return;
            // uma das partes que sangram, sorteada
            int escolhida = -1, vistas = 0;
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
                if (e.Sangra((ParteDoCorpo)p) && Random.Range(0, ++vistas) == 0) escolhida = p;
            if (escolhida < 0) return;
            // pinga da mão (o braço), do pé (a perna), do queixo (a cabeça) ou do peito
            var parte = (ParteDoCorpo)escolhida;
            string osso = parte switch
            {
                ParteDoCorpo.Cabeca => "head", ParteDoCorpo.Tronco => "chest", ParteDoCorpo.BracoE => "hand.L",
                ParteDoCorpo.BracoD => "hand.R", ParteDoCorpo.PernaE => "foot.L", _ => "foot.R",
            };
            var pes = transform.position;
            var de = _ossos.TryGetValue(osso, out var t) && t != null ? t.position : pes;
            var r = Random.insideUnitCircle * 0.04f;
            // a mancha procura o chão a partir de 0,5 m acima do ponto: de dentro da cápsula dele, que o raio não pega
            Sangue.Mancha(new Vector3(de.x + r.x, pes.y + 0.3f, de.z + r.y), Random.Range(0.1f, 0.18f));
        }

        // ------------------------------------------------------------------ as peças

        void Refazer(in EstadoDoCorpo e, Material material)
        {
            Limpar();
            if (e.faixa == 0 && e.talas == 0) return;
            var noCorpo = _referencia.worldToLocalMatrix;
            var doRepouso = new Dictionary<string, Transform>();
            foreach (var t in _referencia.GetComponentsInChildren<Transform>(true))
                if (!doRepouso.ContainsKey(t.name)) doRepouso[t.name] = t;
            var medidas = _corpo.GetComponent<CorpoDoPersonagem>();
            if (medidas == null) medidas = _referencia.GetComponent<CorpoDoPersonagem>();
            float topo = medidas != null ? medidas.topoDaCabeca : 1.81f;

            // as partes com faixa ou tala, medidas numa passada só pela malha
            var secoes = new Secao[PartesDoCorpo.Quantas];
            var lista = new List<Secao>(PartesDoCorpo.Quantas);
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var pp = (ParteDoCorpo)p;
                if (!e.Faixa(pp) && !e.Tala(pp)) continue;
                var s = NovaSecao(pp, doRepouso, noCorpo, topo);
                if (s == null) continue;
                secoes[p] = s;
                lista.Add(s);
            }
            Medir(lista, doRepouso, noCorpo);

            var c = new Construtor();
            for (int p = 0; p < PartesDoCorpo.Quantas; p++)
            {
                var s = secoes[p];
                if (s == null) continue;
                var pp = (ParteDoCorpo)p;
                bool faixa = e.Faixa(pp), tala = e.Tala(pp) && PartesDoCorpo.EhMembro(pp);
                var naSecao = new List<Vector2>(s.pontos);
                if (naSecao.Count < 4) naSecao.AddRange(Poligono(Circulo(s.raioPadrao)));

                // a faixa: um tubo aberto em volta do que é do osso, 1,5 cm para fora
                Vector2[] daFaixa = null;
                if (faixa)
                {
                    daFaixa = Poligono(Suportes(naSecao, FolgaDaFaixa));
                    float comprimento = pp == ParteDoCorpo.Cabeca ? ComprimentoDoAnel : pp == ParteDoCorpo.Tronco ? ComprimentoNoPeito : ComprimentoDaFaixa;
                    // a fase 3 (05/11/2026): o modelo do Blender (Blender/curativos.py), na medida da seção; sem ele, o tubo
                    var feita = Modelo(pp == ParteDoCorpo.Cabeca ? "Faixa_Cabeca" : pp == ParteDoCorpo.Tronco ? "Faixa_Tronco" : "Faixa_Membro");
                    if (feita != null) PorModelo(s, feita, naSecao, FolgaDoModelo, s.centro, 1f, "Faixa " + PartesDoCorpo.NaParte(pp), noCorpo, doRepouso);
                    else
                    {
                        c.Limpar();
                        c.Tubo(s, daFaixa, s.centro, comprimento, CorDaFaixa);
                        Por(s.osso, "Faixa " + PartesDoCorpo.NaParte(pp), c, noCorpo, doRepouso, material);
                    }
                }

                // a tala: duas ripas dos lados (por fora da faixa, se houver) e duas tiras em volta delas
                var talaFeita = tala ? Modelo("Tala") : null;
                if (talaFeita != null)
                {
                    // por fora da faixa (o modelo da faixa vai até ~1,1 cm além da medida)
                    float folga = FolgaDoModelo + (faixa ? 0.011f : 0f);
                    PorModelo(s, talaFeita, naSecao, folga, 0.5f, s.comprimento * 0.84f / 0.30f, "Tala " + PartesDoCorpo.NaParte(pp), noCorpo, doRepouso);
                }
                else if (tala)
                {
                    if (daFaixa != null) naSecao.AddRange(daFaixa);
                    var h = Suportes(naSecao, FolgaDaRipa);
                    float meio = (h[2] - h[6]) * 0.5f;   // o meio da seção no eixo w (o +w é a normal 2, o -w a 6)
                    float meiaRipa = 0.42f * s.comprimento;
                    var tiras = new List<Vector2>(naSecao);
                    c.Limpar();
                    for (int lado = 0; lado < 2; lado++)
                    {
                        float sinal = lado == 0 ? 1f : -1f;
                        float x = sinal * ((lado == 0 ? h[0] : h[4]) + EspessuraDaRipa * 0.5f);
                        var fora = s.u * sinal;
                        var centro = s.Em(0.5f, new Vector2(x, meio));
                        c.Caixa(centro, fora, s.eixo, Vector3.Cross(fora, s.eixo),
                                new Vector3(EspessuraDaRipa * 0.5f, meiaRipa, LarguraDaRipa * 0.5f), CorDaRipa);
                        for (int k = 0; k < 4; k++)
                            tiras.Add(new Vector2(x + (k < 2 ? -1f : 1f) * EspessuraDaRipa * 0.5f, meio + (k % 2 == 0 ? -1f : 1f) * LarguraDaRipa * 0.5f));
                    }
                    var daTira = Poligono(Suportes(tiras, FolgaDaTira));
                    c.Tubo(s, daTira, 0.22f, LarguraDaTira, CorDaTira);
                    c.Tubo(s, daTira, 0.78f, LarguraDaTira, CorDaTira);
                    Por(s.osso, "Tala " + PartesDoCorpo.NaParte(pp), c, noCorpo, doRepouso, material);
                }
            }
        }

        // a parte no corpo em repouso: o osso, o eixo e o meio (onde a faixa fica, em fração do eixo)
        static Secao NovaSecao(ParteDoCorpo p, Dictionary<string, Transform> doRepouso, Matrix4x4 noCorpo, float topo)
        {
            string lado = p == ParteDoCorpo.BracoE || p == ParteDoCorpo.PernaE ? ".L" : ".R";
            var s = new Secao();
            string fim;
            switch (p)
            {
                case ParteDoCorpo.Cabeca:
                    // o anel na testa: do osso da cabeça (a nuca) ao topo dela
                    s.osso = "head"; fim = null; s.centro = 0.6f; s.t0 = 0.2f; s.t1 = 1.05f; s.raioPadrao = 0.1f;
                    break;
                case ParteDoCorpo.Tronco:
                    // a faixa larga no peito, abaixo das axilas
                    s.osso = "chest"; fim = "neck"; s.centro = 0.35f; s.t0 = 0f; s.t1 = 1f; s.raioPadrao = 0.15f;
                    break;
                case ParteDoCorpo.BracoE:
                case ParteDoCorpo.BracoD:
                    s.osso = "forearm" + lado; fim = "hand" + lado; s.centro = 0.5f; s.t0 = -0.02f; s.t1 = 1.02f; s.raioPadrao = 0.045f;
                    break;
                default:
                    s.osso = "shin" + lado; fim = "foot" + lado; s.centro = 0.45f; s.t0 = -0.02f; s.t1 = 1.02f; s.raioPadrao = 0.06f;
                    break;
            }
            if (!doRepouso.TryGetValue(s.osso, out var osso)) return null;
            var a = noCorpo.MultiplyPoint3x4(osso.position);
            Vector3 b;
            if (fim != null && doRepouso.TryGetValue(fim, out var filho)) b = noCorpo.MultiplyPoint3x4(filho.position);
            else if (p == ParteDoCorpo.Cabeca) b = new Vector3(a.x, Mathf.Max(topo, a.y + 0.12f), a.z);
            else if (p == ParteDoCorpo.Tronco) b = a + Vector3.up * 0.25f;
            else b = a + Vector3.down * (PartesDoCorpo.EhBraco(p) ? 0.25f : 0.4f);
            float comprimento = Vector3.Distance(a, b);
            if (comprimento < 0.01f) return null;
            s.a = a;
            s.comprimento = comprimento;
            s.eixo = (b - a) / comprimento;
            // as duas direções de lado: u (para o lado, no braço e na perna em pé) e w = eixo × u, com (u, w, eixo) na
            // mão direita (as voltas do tubo crescem de u para w)
            var u = Vector3.Cross(s.eixo, Vector3.forward);
            if (u.sqrMagnitude < 0.01f) u = Vector3.Cross(s.eixo, Vector3.up);
            s.u = u.normalized;
            s.w = Vector3.Cross(s.eixo, s.u);
            return s;
        }

        /// <summary>
        /// Mede as seções: os vértices de cada malha do corpo (o corpo e as roupas) cujo osso mais forte é o da seção, no
        /// corpo em repouso (o osso do prefab × a pose de ligação), dentro do trecho do eixo, nas coordenadas u e w.
        /// </summary>
        void Medir(List<Secao> secoes, Dictionary<string, Transform> doRepouso, Matrix4x4 noCorpo)
        {
            if (secoes.Count == 0) return;
            foreach (var pele in _corpo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (pele == null || !pele.enabled || !pele.gameObject.activeInHierarchy) continue;
                var malha = pele.sharedMesh;
                if (malha == null || !malha.isReadable) continue;
                var ossos = pele.bones;
                var ligacao = malha.bindposes;
                int n = Mathf.Min(ossos.Length, ligacao.Length);
                var mascara = new int[n];
                var paraOCorpo = new Matrix4x4[n];
                bool algum = false;
                for (int b = 0; b < n; b++)
                {
                    if (ossos[b] == null) continue;
                    string nome = ossos[b].name;
                    int m = 0;
                    for (int k = 0; k < secoes.Count; k++)
                        if (secoes[k].osso == nome) m |= 1 << k;
                    if (m == 0 || !doRepouso.TryGetValue(nome, out var repouso)) continue;
                    mascara[b] = m;
                    paraOCorpo[b] = noCorpo * repouso.localToWorldMatrix * ligacao[b];
                    algum = true;
                }
                if (!algum) continue;
                var vertices = malha.vertices;
                var pesos = malha.boneWeights;
                if (pesos.Length != vertices.Length) continue;
                for (int v = 0; v < vertices.Length; v++)
                {
                    var bw = pesos[v];
                    int bi = bw.boneIndex0;
                    float maior = bw.weight0;
                    if (bw.weight1 > maior) { maior = bw.weight1; bi = bw.boneIndex1; }
                    if (bw.weight2 > maior) { maior = bw.weight2; bi = bw.boneIndex2; }
                    if (bw.weight3 > maior) { maior = bw.weight3; bi = bw.boneIndex3; }
                    if (maior < 0.5f || bi < 0 || bi >= n || mascara[bi] == 0) continue;
                    var ponto = paraOCorpo[bi].MultiplyPoint3x4(vertices[v]);
                    for (int k = 0; k < secoes.Count; k++)
                    {
                        if ((mascara[bi] & (1 << k)) == 0) continue;
                        var s = secoes[k];
                        var d = ponto - s.a;
                        float ao = Vector3.Dot(d, s.eixo);
                        float t = ao / s.comprimento;
                        if (t < s.t0 || t > s.t1) continue;
                        d -= s.eixo * ao;
                        s.pontos.Add(new Vector2(Vector3.Dot(d, s.u), Vector3.Dot(d, s.w)));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ a seção e o polígono

        // as 8 normais dos lados do polígono (0°, 45°... no plano u, w)
        static readonly Vector2[] Normais = FazerNormais();

        static Vector2[] FazerNormais()
        {
            var n = new Vector2[Lados];
            for (int i = 0; i < Lados; i++)
            {
                float a = i * 2f * Mathf.PI / Lados;
                n[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return n;
        }

        // a distância até onde a seção vai em cada uma das 8 direções, mais a folga (o polígono que cabe tudo)
        static float[] Suportes(List<Vector2> pontos, float folga)
        {
            var h = new float[Lados];
            for (int i = 0; i < Lados; i++)
            {
                float m = float.MinValue;
                foreach (var p in pontos) m = Mathf.Max(m, Vector2.Dot(p, Normais[i]));
                h[i] = Mathf.Min(m, 0.4f) + folga;
            }
            return h;
        }

        static float[] Circulo(float raio)
        {
            var h = new float[Lados];
            for (int i = 0; i < Lados; i++) h[i] = raio;
            return h;
        }

        // os 8 cantos do polígono de lados nas distâncias dadas: o canto i fica entre os lados i e i + 1 (a 22,5° + 45°·i)
        static Vector2[] Poligono(float[] h)
        {
            var q = new Vector2[Lados];
            float d = Mathf.Sin(2f * Mathf.PI / Lados);
            for (int i = 0; i < Lados; i++)
            {
                int j = (i + 1) % Lados;
                var n1 = Normais[i];
                var n2 = Normais[j];
                q[i] = new Vector2((h[i] * n2.y - h[j] * n1.y) / d, (n1.x * h[j] - n2.x * h[i]) / d);
            }
            return q;
        }

        // põe a malha feita presa ao osso do corpo, com a pose medida no corpo em repouso
        // ------------------------------------------------ os modelos do Blender (a fase 3) ------------------------------------------------

        /// <summary>A folga entre a medida do membro e o pano do modelo (m).</summary>
        const float FolgaDoModelo = 0.006f;
        /// <summary>O raio de dentro dos modelos (o Blender/curativos.py): o membro de 5 cm.</summary>
        const float RaioDoModelo = 0.05f;

        static float Sq(float x) => x * x;

        static Dictionary<string, Mesh> _modelos;
        static Material _materialDaPaleta, _materialDaPaletaNoRetrato;

        // os modelos do Curativos.fbx (Resources): a faixa do membro, a da cabeça, a do peito e a tala
        static Mesh Modelo(string nome)
        {
            if (_modelos == null)
            {
                _modelos = new Dictionary<string, Mesh>();
                // (só as legíveis: o Read/Write do Curativos.fbx; sem ele, as formas de antes)
                foreach (var m in Resources.LoadAll<Mesh>("Curativos")) if (m.isReadable) _modelos[m.name] = m;
            }
            return _modelos.TryGetValue(nome, out var malha) ? malha : null;
        }

        // o material dos modelos: o dos itens no chão (as cores da Paleta.png); no retrato, a cópia do dele com a paleta
        Material MaterialDosModelos()
        {
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null || mundo.material == null) return null;
            if (!_retrato) return _materialDaPaleta != null ? _materialDaPaleta : (_materialDaPaleta = mundo.material);
            if (_materialDaPaletaNoRetrato == null && _material != null)
            {
                _materialDaPaletaNoRetrato = new Material(_material) { name = "Curativos (retrato)" };
                _materialDaPaletaNoRetrato.mainTexture = mundo.material.mainTexture;
                _materialDaPaletaNoRetrato.mainTextureOffset = Vector2.zero;
                _materialDaPaletaNoRetrato.mainTextureScale = Vector2.one;
            }
            return _materialDaPaletaNoRetrato;
        }

        /// <summary>
        /// Põe o modelo na seção: o +Y dele no eixo do osso (no meio <paramref name="t"/> do trecho), o +X e o +Z nas direções
        /// de lado (u, w), a seção escalada para a medida do membro em cada direção (o raio de dentro do modelo é 5 cm) e o
        /// comprimento por <paramref name="escalaY"/>. A malha é feita de novo no espaço do osso (acompanha a animação).
        /// </summary>
        void PorModelo(Secao s, Mesh modelo, List<Vector2> naSecao, float folga, float t, float escalaY, string nome, Matrix4x4 noCorpo, Dictionary<string, Transform> doRepouso)
        {
            var material = MaterialDosModelos();
            if (material == null || !_ossos.TryGetValue(s.osso, out var vivo) || vivo == null || !doRepouso.TryGetValue(s.osso, out var repouso)) return;
            // o meio e as meias-larguras da seção (nas direções u e w)
            float minU = float.MaxValue, maxU = float.MinValue, minW = float.MaxValue, maxW = float.MinValue;
            foreach (var q in naSecao)
            {
                minU = Mathf.Min(minU, q.x); maxU = Mathf.Max(maxU, q.x);
                minW = Mathf.Min(minW, q.y); maxW = Mathf.Max(maxW, q.y);
            }
            var meio = new Vector2((minU + maxU) * 0.5f, (minW + maxW) * 0.5f);
            float ru = (maxU - minU) * 0.5f + folga, rw = (maxW - minW) * 0.5f + folga;
            var centro = s.Em(t, meio);
            // o +Z do modelo (o meio do U da tala, a ponta da faixa) para a frente do corpo, no plano da seção; o +X ao lado
            Vector3 frente = Vector3.ProjectOnPlane(Vector3.forward, s.eixo);
            if (frente.sqrMagnitude < 1e-4f) frente = s.w;
            frente.Normalize();
            Vector3 lado = Vector3.Cross(s.eixo, frente).normalized;
            // a medida da seção nessas duas direções (a meia-largura das u e w, projetada)
            float raioLado = Mathf.Sqrt(Sq(Vector3.Dot(lado, s.u) * ru) + Sq(Vector3.Dot(lado, s.w) * rw));
            float raioFrente = Mathf.Sqrt(Sq(Vector3.Dot(frente, s.u) * ru) + Sq(Vector3.Dot(frente, s.w) * rw));
            // do modelo para o corpo: as colunas são o lado, o eixo e a frente, com a escala de cada uma
            var paraOCorpo = new Matrix4x4(
                (Vector4)(lado * (raioLado / RaioDoModelo)), (Vector4)(s.eixo * escalaY), (Vector4)(frente * (raioFrente / RaioDoModelo)), new Vector4(centro.x, centro.y, centro.z, 1f));
            var tudo = (noCorpo * repouso.localToWorldMatrix).inverse * paraOCorpo;
            var normais = tudo.inverse.transpose;
            var v = modelo.vertices;
            var n = modelo.normals;
            for (int i = 0; i < v.Length; i++) v[i] = tudo.MultiplyPoint3x4(v[i]);
            for (int i = 0; i < n.Length; i++) n[i] = normais.MultiplyVector(n[i]).normalized;
            var tri = modelo.triangles;
            // a base (u, eixo, w) pode ser a do espelho: aí as faces viram para dentro, e o giro dos triângulos volta
            if (Vector3.Dot(Vector3.Cross(lado, s.eixo), frente) < 0f)
                for (int i = 0; i < tri.Length; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
            var malha = new Mesh { name = Prefixo + nome };
            malha.vertices = v;
            malha.normals = n;
            malha.uv = modelo.uv;
            malha.triangles = tri;
            malha.RecalculateBounds();
            _malhas.Add(malha);
            var go = new GameObject(Prefixo + nome);
            go.layer = _corpo.gameObject.layer;
            go.transform.SetParent(vivo, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (_retrato)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _pecas.Add(go);
        }

        void Por(string osso, string nome, Construtor c, Matrix4x4 noCorpo, Dictionary<string, Transform> doRepouso, Material material)
        {
            if (c.Vazio || !_ossos.TryGetValue(osso, out var vivo) || vivo == null || !doRepouso.TryGetValue(osso, out var repouso)) return;
            // do espaço do corpo para o do osso (com a escala dele, se tiver)
            var paraOOsso = (noCorpo * repouso.localToWorldMatrix).inverse;
            var malha = c.Malha(paraOOsso, Prefixo + nome);
            _malhas.Add(malha);
            var go = new GameObject(Prefixo + nome);
            go.layer = _corpo.gameObject.layer;
            go.transform.SetParent(vivo, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            if (_retrato)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _pecas.Add(go);
        }

        // uma parte medida no corpo em repouso (o espaço do corpo, olhando para +Z): o osso onde a peça fica presa, o eixo
        // dele (do começo ao filho), as direções de lado (u, w), o trecho medido e o meio da faixa (frações do eixo) e os
        // pontos da seção (nas coordenadas u e w)
        sealed class Secao
        {
            public string osso;
            public Vector3 a, eixo, u, w;
            public float comprimento, t0, t1, centro, raioPadrao;
            public readonly List<Vector2> pontos = new List<Vector2>();

            public Vector3 Em(float t, Vector2 q) => a + eixo * (t * comprimento) + u * q.x + w * q.y;
        }

        // junta os vértices de uma peça (no espaço do corpo) e faz a malha no espaço do osso
        sealed class Construtor
        {
            readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _tri = new List<int>();

            public bool Vazio => _tri.Count == 0;

            public void Limpar()
            {
                _v.Clear();
                _n.Clear();
                _uv.Clear();
                _tri.Clear();
            }

            static Vector2 Uv(int cor) => new Vector2((cor + 0.5f) / Celulas, 0.5f);

            /// <summary>Um tubo aberto pela seção dada (os 8 cantos), com o meio em <paramref name="t"/> (fração do eixo).</summary>
            public void Tubo(Secao s, Vector2[] cantos, float t, float comprimento, int cor)
            {
                int b = _v.Count;
                var uv = Uv(cor);
                for (int anel = 0; anel < 2; anel++)
                {
                    float ao = t * s.comprimento + (anel == 0 ? -0.5f : 0.5f) * comprimento;
                    for (int i = 0; i < Lados; i++)
                    {
                        var q = cantos[i];
                        _v.Add(s.a + s.eixo * ao + s.u * q.x + s.w * q.y);
                        float ang = (i + 0.5f) * 2f * Mathf.PI / Lados;
                        _n.Add(s.u * Mathf.Cos(ang) + s.w * Mathf.Sin(ang));
                        _uv.Add(uv);
                    }
                }
                // (u, w, eixo) na mão direita: o triângulo (A, B, C) com as voltas crescendo de u para w olha para fora
                for (int i = 0; i < Lados; i++)
                {
                    int j = (i + 1) % Lados;
                    int a0 = b + i, b0 = b + j, c1 = b + Lados + j, d1 = b + Lados + i;
                    _tri.Add(a0); _tri.Add(b0); _tri.Add(c1);
                    _tri.Add(a0); _tri.Add(c1); _tri.Add(d1);
                }
            }

            /// <summary>Uma caixa (a ripa): o centro, os eixos (x × y = z) e as meias medidas em cada um.</summary>
            public void Caixa(Vector3 centro, Vector3 x, Vector3 y, Vector3 z, Vector3 meias, int cor)
            {
                x.Normalize();
                y.Normalize();
                z.Normalize();
                var uv = Uv(cor);
                Face(centro, x * meias.x, y * meias.y, z * meias.z, uv);
                Face(centro, -x * meias.x, z * meias.z, y * meias.y, uv);
                Face(centro, y * meias.y, z * meias.z, x * meias.x, uv);
                Face(centro, -y * meias.y, x * meias.x, z * meias.z, uv);
                Face(centro, z * meias.z, x * meias.x, y * meias.y, uv);
                Face(centro, -z * meias.z, y * meias.y, x * meias.x, uv);
            }

            // uma face da caixa: para fora em n, com s × t na direção de n
            void Face(Vector3 centro, Vector3 n, Vector3 s, Vector3 t, Vector2 uv)
            {
                int b = _v.Count;
                var c = centro + n;
                var normal = n.normalized;
                _v.Add(c - s - t);
                _v.Add(c + s - t);
                _v.Add(c + s + t);
                _v.Add(c - s + t);
                for (int k = 0; k < 4; k++)
                {
                    _n.Add(normal);
                    _uv.Add(uv);
                }
                _tri.Add(b); _tri.Add(b + 1); _tri.Add(b + 2);
                _tri.Add(b); _tri.Add(b + 2); _tri.Add(b + 3);
            }

            public Mesh Malha(Matrix4x4 paraOOsso, string nome)
            {
                var normais = paraOOsso.inverse.transpose;
                var vs = new Vector3[_v.Count];
                var ns = new Vector3[_n.Count];
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = paraOOsso.MultiplyPoint3x4(_v[i]);
                    ns[i] = normais.MultiplyVector(_n[i]).normalized;
                }
                var malha = new Mesh { name = nome };
                malha.SetVertices(vs);
                malha.SetNormals(ns);
                malha.SetUVs(0, _uv);
                malha.SetTriangles(_tri, 0);
                malha.RecalculateBounds();
                return malha;
            }
        }
    }
}
