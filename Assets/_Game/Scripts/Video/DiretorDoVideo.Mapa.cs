using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Video
{
    // Tomada 1: o mapa se montando, visto do alto (a parte 2 do plano dos vídeos). Numa planície vazia de grama, o chão da
    // cidade cai numa onda, do meio para fora; as casas e o prédio caem do céu, amassam ao bater e levantam poeira; depois
    // caem os carros e as coisas da rua, os postes, as cercas e as árvores brotam do chão e o anel de pinheiros cresce em
    // volta. No fim o dia vira noite e os postes acendem um por um, do meio para fora.
    // Só o que se vê se mexe (o modelo e os móveis; a colisão das casas e do bunker fica parada, e o jogador continua no
    // bunker), e no fim tudo volta exatamente ao lugar.
    public partial class DiretorDoVideo
    {
        /// <summary>Como a peça entra: cai do céu ou brota do chão.</summary>
        enum Entrada { Cai, Brota }

        /// <summary>Uma peça da montagem: um ou mais objetos que se mexem juntos (o modelo da casa e os móveis dela).</summary>
        class Peca
        {
            public Transform[] partes;
            public Vector3[] lugares;
            /// <summary>A escala do primeiro (o modelo), que estica na queda e amassa ao bater (só com a origem embaixo, no meio).</summary>
            public Vector3 escala;
            public bool amassa;
            public Entrada entrada;
            public float inicio, duracao, altura, poeira;
            public Bounds caixa;
            public bool bateu;
        }

        const float DuracaoDoMapa = 13.5f;
        // a distância (m) do meio até as peças mais longe da rua (as barricadas): o tempo de cada uma sai dela
        const float RaioDaRua = 70f;
        // fora de vista: debaixo da terra, além do alcance da câmera e das sombras
        static readonly Vector3 Guardado = new Vector3(0f, -1000f, 0f);
        // a planície de grama embaixo de tudo: 1 km para cada lado, 15 cm abaixo da grama do chão do Blender
        const float FimDoChaoDeFora = 1000f, AlturaDoChaoDeFora = -0.15f;
        // a cor da grama na Paleta.png (a coluna 9 da linha 5, o "Grama" do cidade_kit.py)
        static readonly Vector2 UvDaGrama = new Vector2(9.5f / 16f, 5.5f / 16f);

        readonly List<Peca> _pecas = new List<Peca>();
        readonly List<Renderer> _escondidos = new List<Renderer>();
        readonly List<Light> _apagadas = new List<Light>();
        readonly List<GameObject> _desligados = new List<GameObject>();
        DecalProjector[] _marcas = new DecalProjector[0];
        ParticleSystem _poeira;
        Transform _chaoDeFora;
        FogMode _neblinaDoJogo;
        bool _mapaMontado;

        IEnumerator PrepararMapa()
        {
            // fim de tarde, com pouca neblina de altura (do alto, ela cobria a cidade inteira) e bem mais brilho: sem o véu
            // da neblina, as cores da cidade vistas de cima são escuras (o asfalto, a grama, os telhados)
            Clima(16.6f, 0.1f, 0.9f, 1.25f);
            cameraDoVideo.fieldOfView = 55f;
            cameraDoVideo.nearClipPlane = 1f;
            cameraDoVideo.farClipPlane = 1100f;
            Sombras(320f);
            // a neblina vira linear: a cidade fica limpa e o chão de fora some aos poucos no horizonte
            _neblinaDoJogo = RenderSettings.fogMode;
            RenderSettings.fogMode = FogMode.Linear;
            JuntarPecas();
            EsconderParaOMapa();
            AcenderDoMeioParaFora();
            CriarChaoDeFora();
            _mapaMontado = true;
            QuadroDoMapa(0f);   // tudo guardado: o vazio
            return AnimarMapa();
        }

        IEnumerator AnimarMapa()
        {
            for (float t = 0f; t < DuracaoDoMapa; t += Time.deltaTime)
            {
                QuadroDoMapa(t);
                yield return null;
            }
            // fica no último quadro (desfazer agora mudaria a imagem antes de o gravador fechar o arquivo): tudo volta ao
            // sair do Play
            QuadroDoMapa(DuracaoDoMapa);
        }

        void QuadroDoMapa(float t)
        {
            foreach (var p in _pecas) AnimarPeca(p, t);
            // as marcas no chão aparecem logo depois do chão; as luzes de dentro de casa, depois das casas
            float marcas = Mathf.InverseLerp(2.4f, 3.2f, t);
            foreach (var m in _marcas) m.fadeFactor = marcas;
            if (t >= 6f)
                foreach (var g in _desligados) g.SetActive(true);
            // o fim da tarde fica parado até as peças assentarem; depois anoitece até as 20h (os postes acendem das 18h às 19h30)
            CicloDoDia.MudarHora(Mathf.Lerp(16.6f, 20f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 12.8f, t))));
            // a câmera gira devagar e desce; a neblina começa logo depois do meio da cidade
            float k = Mathf.SmoothStep(0f, 1f, t / DuracaoDoMapa);
            float distancia = Mathf.Lerp(195f, 130f, k);
            Orbitar(new Vector3(0f, 0f, 2f), Mathf.Lerp(-18f, 18f, k), Mathf.Lerp(66f, 50f, k), distancia);
            RenderSettings.fogStartDistance = distancia + 40f;
            RenderSettings.fogEndDistance = distancia + 260f;
        }

        void AnimarPeca(Peca p, float t)
        {
            float s = t - p.inicio;
            var modelo = p.partes[0];
            if (s < 0f)
            {
                Deslocar(p, Guardado);
                return;
            }
            if (p.entrada == Entrada.Brota)
            {
                // sobe do chão, passa um pouco e volta
                Deslocar(p, Vector3.down * (p.altura * (1f - SaidaComSobra(Mathf.Clamp01(s / p.duracao)))));
                return;
            }
            if (s < p.duracao)
            {
                // cai acelerando, como na gravidade, um pouco esticada
                float k = s / p.duracao;
                Deslocar(p, Vector3.up * (p.altura * (1f - k * k)));
                if (p.amassa) modelo.localScale = Vector3.Scale(p.escala, new Vector3(0.96f, 1.06f, 0.96f));
                return;
            }
            if (!p.bateu)
            {
                p.bateu = true;
                if (p.poeira > 0f) Poeira(p.caixa, p.poeira);
            }
            Deslocar(p, Vector3.zero);
            if (!p.amassa) return;
            // bate, amassa e balança até parar
            float d = s - p.duracao;
            float amasso = d < 1.2f ? 0.13f * Mathf.Exp(-6f * d) * Mathf.Cos(16f * d) : 0f;
            modelo.localScale = Vector3.Scale(p.escala, new Vector3(1f + amasso * 0.5f, 1f - amasso, 1f + amasso * 0.5f));
        }

        static float SaidaComSobra(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float m = k - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        static void Deslocar(Peca p, Vector3 desvio)
        {
            for (int i = 0; i < p.partes.Length; i++) p.partes[i].position = p.lugares[i] + desvio;
        }

        /// <summary>Tudo volta ao lugar: as peças, o que estava escondido, as luzes e as marcas.</summary>
        void DesfazerMapa()
        {
            if (!_mapaMontado) return;
            _mapaMontado = false;
            foreach (var p in _pecas)
            {
                if (p.partes[0] == null) continue;
                Deslocar(p, Vector3.zero);
                p.partes[0].localScale = p.escala;
            }
            foreach (var r in _escondidos) if (r != null) r.enabled = true;
            foreach (var l in _apagadas) if (l != null) l.enabled = true;
            foreach (var g in _desligados) if (g != null) g.SetActive(true);
            foreach (var m in _marcas) if (m != null) m.fadeFactor = 1f;
            if (_chaoDeFora != null) Destroy(_chaoDeFora.gameObject);
            RenderSettings.fogMode = _neblinaDoJogo;
            _pecas.Clear();
            _escondidos.Clear();
            _apagadas.Clear();
            _desligados.Clear();
        }

        // ------------------------------------------------------------------ as peças, em ordem

        void JuntarPecas()
        {
            _pecas.Clear();
            var cidade = GameObject.Find("Cidade").transform;

            // o chão: os 25 pedaços, numa onda do meio para fora (os centros vão até ~125 m)
            foreach (Transform pedaco in cidade.Find("Chão (arte)"))
                Nova(Entrada.Cai, 0.5f + 1.6f * Distancia(Caixa(pedaco)) / 125f, 0.45f, 10f, 0f, false, pedaco);

            // as casas, uma a uma, e o prédio por último, de mais alto e com mais poeira
            string[] casas = { "Casa 4", "Casa da família", "Casa 5", "Casa 1", "Casa 2" };
            for (int i = 0; i < casas.Length; i++)
            {
                var casa = cidade.Find(casas[i]);
                Nova(Entrada.Cai, 2.4f + 0.45f * i, 0.55f, 28f, 1f, true, casa.Find("Casa"), casa.Find("Móveis"));
            }
            var predio = cidade.Find("Prédio");
            Nova(Entrada.Cai, 4.85f, 0.7f, 45f, 1.8f, true, predio.Find("Prédio"), predio.Find("Térreo"), predio.Find("Andar 1"), predio.Find("Andar 2"), predio.Find("Andar 3"));

            // os carros caem com um pouco de poeira; as coisas da rua caem de perto; o que é comprido brota do chão
            foreach (var grupo in new[] { cidade.Find("Carros"), cidade.Find("Estacionamento"), cidade.Find("Abandono/Carros abandonados") })
                foreach (var carro in ComRenderer(grupo))
                    Nova(Entrada.Cai, 5f + 1.8f * Distancia(Caixa(carro)) / RaioDaRua, 0.45f, 12f, 0.35f, true, carro);
            var miudezas = new List<Transform>();
            miudezas.AddRange(ComRenderer(cidade.Find("Objetos de rua")));
            miudezas.AddRange(ComRenderer(cidade.Find("Praça")));
            miudezas.AddRange(ComRenderer(cidade.Find("Abandono/Blitz abandonada")));
            miudezas.AddRange(ComRenderer(cidade.Find("Abandono/Lixo")));
            foreach (Transform barricada in cidade.Find("Barricadas")) miudezas.AddRange(ComRenderer(barricada));
            foreach (var coisa in miudezas)
            {
                float inicio = 5.3f + 2f * Distancia(Caixa(coisa)) / RaioDaRua;
                bool comprida = coisa.name.StartsWith("Placa") || coisa.name.StartsWith("Árvore");
                if (comprida) Nova(Entrada.Brota, inicio, 0.5f, Caixa(coisa).size.y + 0.3f, 0f, false, coisa);
                else Nova(Entrada.Cai, inicio, 0.3f, 5f, 0f, false, coisa);
            }
            foreach (var poste in ComRenderer(cidade.Find("Postes")))
                Nova(Entrada.Brota, 5.6f + 1.8f * Distancia(Caixa(poste)) / RaioDaRua, 0.55f, 6.8f, 0f, false, poste);
            foreach (Transform cerca in cidade.Find("Cercas"))
                foreach (var trecho in ComRenderer(cerca))
                    Nova(Entrada.Brota, 6f + 1.5f * Distancia(Caixa(trecho)) / RaioDaRua, 0.35f, 1.6f, 0f, false, trecho);
            foreach (var arvore in ComRenderer(cidade.Find("Árvores")))
                Nova(Entrada.Brota, 5.8f + 1.4f * Distancia(Caixa(arvore)) / RaioDaRua, 0.6f, Caixa(arvore).size.y + 0.3f, 0f, false, arvore);

            // o anel de pinheiros cresce dando a volta na cidade (um sorteio fixo, para as gravações saírem iguais)
            var sorteio = new System.Random(7);
            foreach (var pinheiro in ComRenderer(cidade.Find("Pinheiros")))
            {
                var c = Caixa(pinheiro).center;
                float volta = Mathf.Repeat(Mathf.Atan2(c.z, c.x) * Mathf.Rad2Deg + 90f, 360f) / 360f;
                Nova(Entrada.Brota, 6.6f + 2.4f * volta + 0.25f * (float)sorteio.NextDouble(), 0.7f, Caixa(pinheiro).size.y + 0.5f, 0f, false, pinheiro);
            }
        }

        void Nova(Entrada entrada, float inicio, float duracao, float altura, float poeira, bool amassa, params Transform[] partes)
        {
            var lista = new List<Transform>();
            foreach (var t in partes) if (t != null) lista.Add(t);
            if (lista.Count == 0) return;
            var p = new Peca
            {
                partes = lista.ToArray(), lugares = new Vector3[lista.Count], escala = lista[0].localScale, amassa = amassa,
                entrada = entrada, inicio = inicio, duracao = duracao, altura = altura, poeira = poeira, caixa = Caixa(lista[0]),
            };
            for (int i = 0; i < lista.Count; i++) p.lugares[i] = lista[i].position;
            _pecas.Add(p);
        }

        static IEnumerable<Transform> ComRenderer(Transform grupo)
        {
            if (grupo == null) yield break;
            foreach (Transform filho in grupo)
                if (filho.GetComponentInChildren<Renderer>(true) != null) yield return filho;
        }

        static Bounds Caixa(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var caixa = renderers[0].bounds;
            foreach (var r in renderers) caixa.Encapsulate(r.bounds);
            return caixa;
        }

        static float Distancia(Bounds caixa) => new Vector2(caixa.center.x, caixa.center.z).magnitude;

        // o que não entra nesta tomada: o bunker (embaixo da casa da família, que cai do céu), as armas do painel, a
        // comida e a água (que ficariam no ar antes das casas), as luzes de dentro das casas (até elas chegarem) e as
        // marcas no chão (até o chão chegar)
        void EsconderParaOMapa()
        {
            var cidade = GameObject.Find("Cidade").transform;
            Esconder(cidade.Find("Casa da família/Bunker"));
            foreach (var nome in new[] { "Bunker (rede)", "Parede de armas", "Itens no mundo" })
            {
                var raiz = GameObject.Find(nome);
                if (raiz != null) Esconder(raiz.transform);
            }
            foreach (Transform sinal in cidade.Find("Sinais de vida"))
                if (sinal.gameObject.activeSelf)
                {
                    sinal.gameObject.SetActive(false);
                    _desligados.Add(sinal.gameObject);
                }
            _marcas = cidade.Find("Marcas no chão").GetComponentsInChildren<DecalProjector>(true);
        }

        void Esconder(Transform t)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                if (r.enabled)
                {
                    r.enabled = false;
                    _escondidos.Add(r);
                }
            foreach (var l in t.GetComponentsInChildren<Light>(true))
                if (l.enabled)
                {
                    l.enabled = false;
                    _apagadas.Add(l);
                }
        }

        // os postes acendem em ordem, do meio para fora (o atraso de cada um vai de 0 a 0,42 da noite: o último ainda
        // acende inteiro antes da noite fechar)
        void AcenderDoMeioParaFora()
        {
            var postes = new List<PosteDeLuz>(GameObject.Find("Cidade").transform.Find("Postes").GetComponentsInChildren<PosteDeLuz>(true));
            postes.Sort((a, b) => new Vector2(a.transform.position.x, a.transform.position.z).sqrMagnitude.CompareTo(new Vector2(b.transform.position.x, b.transform.position.z).sqrMagnitude));
            for (int i = 0; i < postes.Count; i++) postes[i].Atraso = 0.42f * i / Mathf.Max(1, postes.Count - 1);
        }

        // a planície de grama onde a cidade se monta: o "vazio" do começo, e o mundo não termina na borda da tela (o chão do
        // Blender acaba a 110 m do meio). Um retângulo só, com o material da cidade e o UV na cor da grama
        void CriarChaoDeFora()
        {
            float b = FimDoChaoDeFora;
            var malha = new Mesh { name = "Chão de fora" };
            malha.SetVertices(new[] { new Vector3(-b, 0f, -b), new Vector3(-b, 0f, b), new Vector3(b, 0f, b), new Vector3(b, 0f, -b) });
            malha.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            malha.uv = new[] { UvDaGrama, UvDaGrama, UvDaGrama, UvDaGrama };
            malha.RecalculateNormals();
            malha.RecalculateBounds();

            var objeto = new GameObject("Chão de fora (vídeo)");
            objeto.transform.position = new Vector3(0f, AlturaDoChaoDeFora, 0f);
            objeto.AddComponent<MeshFilter>().sharedMesh = malha;
            var desenho = objeto.AddComponent<MeshRenderer>();
            var pedaco = GameObject.Find("Cidade").transform.Find("Chão (arte)").GetComponentInChildren<MeshRenderer>();
            desenho.sharedMaterial = pedaco.sharedMaterial;
            _chaoDeFora = objeto.transform;
        }

        // ------------------------------------------------------------------ a poeira

        /// <summary>Uma nuvem de poeira saindo da borda da caixa, no chão, para fora (a força multiplica a quantidade, a velocidade e o tamanho).</summary>
        void Poeira(Bounds caixa, float forca)
        {
            if (_poeira == null) _poeira = CriarPoeira();
            Vector3 min = caixa.min, max = caixa.max;
            float largura = max.x - min.x, fundo = max.z - min.z, perimetro = 2f * (largura + fundo);
            int n = Mathf.Clamp(Mathf.RoundToInt(perimetro * 0.8f * forca), 6, 300);
            float raiz = Mathf.Sqrt(forca);
            // a poeira não recebe luz: à noite, a cor dela escurece junto (senão ficava uma nuvem cinza clara no escuro)
            float tom = Mathf.Lerp(1f, 0.4f, CicloDoDia.Noite);
            var emissao = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                // um ponto da borda, com a direção para fora dela
                float u = Random.value * perimetro;
                Vector3 ponto, fora;
                if (u < largura) { ponto = new Vector3(min.x + u, 0f, min.z); fora = Vector3.back; }
                else if ((u -= largura) < fundo) { ponto = new Vector3(max.x, 0f, min.z + u); fora = Vector3.right; }
                else if ((u -= fundo) < largura) { ponto = new Vector3(max.x - u, 0f, max.z); fora = Vector3.forward; }
                else { ponto = new Vector3(min.x, 0f, max.z - (u - largura)); fora = Vector3.left; }
                ponto.y = min.y + 0.4f;
                fora = (fora + new Vector3(Random.Range(-0.45f, 0.45f), 0f, Random.Range(-0.45f, 0.45f))).normalized;
                emissao.position = ponto;
                emissao.velocity = fora * (Random.Range(3f, 7f) * raiz) + Vector3.up * Random.Range(0.4f, 2f);
                emissao.startSize = Random.Range(4f, 8f) * raiz;
                emissao.startLifetime = Random.Range(1.6f, 2.8f);
                emissao.rotation = Random.Range(0f, 360f);
                // a cor da terra seca, bem transparente: as nuvens se somam
                emissao.startColor = new Color(0.47f * tom, 0.45f * tom, 0.41f * tom, Random.Range(0.22f, 0.38f));
                _poeira.Emit(emissao, 1);
            }
        }

        ParticleSystem CriarPoeira()
        {
            var objeto = new GameObject("Poeira do vídeo");
            objeto.transform.SetParent(transform, false);
            var ps = objeto.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = 8000;   // os 1.000 zumbis da tomada 3 soltam 6 cada
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;   // a poeira sobe devagar
            var emissao = ps.emission; emissao.enabled = false;
            var forma = ps.shape; forma.enabled = false;
            // sai rápido e freia, espalhando uns metros em volta
            var freio = ps.limitVelocityOverLifetime; freio.enabled = true; freio.limit = 0f; freio.dampen = 0.02f;
            var tamanho = ps.sizeOverLifetime; tamanho.enabled = true; tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            var cor = ps.colorOverLifetime; cor.enabled = true;
            var some = new Gradient();
            some.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            cor.color = some;
            // o material das partículas do jogo, com uma textura de nuvem macia (o ponto dele, grande, virava uma bola)
            var desenho = objeto.GetComponent<ParticleSystemRenderer>();
            var efeitos = FindAnyObjectByType<Efeitos>();
            if (efeitos != null)
            {
                var material = new Material(efeitos.materialParticulas) { name = "Poeira do vídeo" };
                var nuvem = TexturaDeNuvem();
                material.SetTexture("_BaseMap", nuvem);
                material.mainTexture = nuvem;
                desenho.sharedMaterial = material;
            }
            desenho.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Uma mancha redonda que some devagar até a borda, com um pouco de ruído (64 px).</summary>
        static Texture2D TexturaDeNuvem()
        {
            const int n = 64;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Nuvem" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a) * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.12f + 3.1f, y * 0.12f + 7.7f));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            textura.SetPixels32(pixels);
            textura.Apply();
            return textura;
        }
    }
}
