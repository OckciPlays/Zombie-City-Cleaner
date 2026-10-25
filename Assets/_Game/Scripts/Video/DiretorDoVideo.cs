using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Video
{
    /// <summary>
    /// O diretor da cena de vídeo (<c>Scenes/Video.unity</c>, feita pela montagem do vídeo a partir da Demo_Cidade). No
    /// Play, ele liga o <see cref="ModoVideo"/> (sem HUD, o jogador de mãos vazias e sem levar dano, o painel de armas
    /// vazio), abre uma partida local só com o anfitrião (sem internet), espera o jogador nascer e os zumbis povoarem e
    /// roda a tomada escolhida com a câmera do vídeo: a hora, a neblina, o brilho e o movimento da câmera de cada uma.
    /// Com "gravar" ligado, o GravadorDoVideo (no Editor) grava a tomada num MP4 vertical e para o Play no fim.
    /// </summary>
    public partial class DiretorDoVideo : MonoBehaviour
    {
        /// <summary>As tomadas do vídeo (as outras entram nas próximas partes do plano dos vídeos).</summary>
        public enum Tomada { Teste, Mapa, Bunker, Zumbis, Jogador }

        [Header("O que gravar")]
        [Tooltip("O nome do vídeo: a pasta dele em Recordings/.")]
        public string video = "Video01";
        public Tomada tomada = Tomada.Teste;
        [Tooltip("Ligado, o Play grava a tomada (MP4 de 1080 × 1920, 60 fps, com o som do jogo, em Recordings/<vídeo>/) " +
                 "e para sozinho no fim. Desligado, o Play só mostra.")]
        public bool gravar;
        [Tooltip("Na tomada do jogador: você mesmo anda (WASD, mouse, Shift corre, F a lanterna, clique soca), com a câmera " +
                 "atrás do ombro. Enter faz o final (o jogador para e a câmera sobe) e termina a tomada; parar o Play também " +
                 "termina (e fecha o vídeo). Desligado, o diretor anda sozinho pelo caminho de sempre.")]
        public bool jogadorNoControle;

        [Header("Como a partida começa")]
        [Tooltip("O jogador nasce com a pistola (desligado: de mãos vazias).")]
        public bool jogadorComArma;
        [Tooltip("As armas no painel do bunker (desligado: o painel vazio).")]
        public bool armasNoPainel;
        [Tooltip("Tempo (s) entre a partida ficar pronta e a tomada começar: a neblina, as luzes e os zumbis assentam.")]
        public float preparo = 1.5f;
        [Tooltip("Salva uma folha com 12 quadros da tomada (do começo ao fim) em Recordings/<vídeo>/<tomada>_folha.png, " +
                 "para conferir sem abrir o vídeo.")]
        public bool folhaDeConferencia = true;

        [Header("Peças")]
        public Camera cameraDoVideo;
        public AudioListener ouvidoDoVideo;

        public static DiretorDoVideo Instancia { get; private set; }
        /// <summary>A tomada está rodando: o gravador grava.</summary>
        public bool Rodando { get; private set; }
        /// <summary>A tomada acabou (ou não deu para começar): o gravador fecha o arquivo.</summary>
        public bool Terminou { get; private set; }
        public string NomeDaTomada => $"{(int)tomada}_{tomada}";
        /// <summary>A tomada tem zumbis na cidade (no mapa se montando e no bunker, eles só chegam depois).</summary>
        bool ComZumbis => tomada != Tomada.Mapa && tomada != Tomada.Bunker;

        Volume _volume;
        ColorAdjustments _brilho;
        float _brilhoDoDia, _brilhoDaNoite, _brilhoExtra, _brilhoExtraDeNoite;
        float _sombraDoJogo = -1f;
        /// <summary>A altura do corte de raio-X (m): a câmera não desenha nada acima dela. NaN = sem corte.</summary>
        float _corte = float.NaN;
        bool _comCamera = true;

        void Awake()
        {
            Instancia = this;
            ModoVideo.Ligado = true;
            ModoVideo.SemArma = !jogadorComArma;
            // antes do Start do painel, que mostra o começo antes da partida
            if (!armasNoPainel)
                foreach (var painel in FindObjectsByType<ParedeDeArmas>(FindObjectsInactive.Include)) painel.inicio = new TipoDeArma[0];
            // antes da partida abrir: o ZombieManager povoa as hordas quando nasce na rede
            var zumbis = FindAnyObjectByType<ZombieManager>();
            if (zumbis != null) zumbis.povoarAoComecar = ComZumbis;
            CriarBrilho();
        }

        void OnDisable()
        {
            if (Instancia == this) Instancia = null;
            ModoVideo.Zerar();
            DesfazerMapa();
            _corte = float.NaN;
            if (cameraDoVideo != null) cameraDoVideo.ResetProjectionMatrix();
            // a distância das sombras é do asset do URP (o mesmo do jogo): ela volta ao que era
            if (_sombraDoJogo >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.shadowDistance = _sombraDoJogo;
            _sombraDoJogo = -1f;
            if (_volume != null) Destroy(_volume.sharedProfile);
        }

        void Start() => StartCoroutine(Rodar());

        IEnumerator Rodar()
        {
            var sessao = SessaoOnline.Instancia;
            sessao.HospedarLocal();
            if (sessao.Atual != SessaoOnline.Estado.Conectado)
            {
                Debug.LogError($"[vídeo] não deu para abrir a partida local: {sessao.Erro}");
                Terminou = true;
                yield break;
            }
            // o jogador nasce no bunker e os zumbis povoam a cidade (6 por quadro: os 1.000 levam uns 3 s)
            while (JogadorRede.Local == null || ZombieManager.Instancia == null || !ZombieManager.Instancia.IsSpawned || ZombieManager.Instancia.Povoando)
                yield return null;
            ZombieManager.Instancia.SemDano = true;
            var tomadaAtual = Preparar();
            yield return new WaitForSeconds(preparo);
            Debug.Log($"[vídeo] tomada {NomeDaTomada}");
            Rodando = true;
            // (com você no controle, a tomada não tem duração certa: sem folha)
            if (folhaDeConferencia && !(tomada == Tomada.Jogador && jogadorNoControle)) StartCoroutine(Conferir(Time.time));
            yield return tomadaAtual;
            Rodando = false;
            Terminou = true;
            Debug.Log($"[vídeo] tomada {NomeDaTomada} terminou");
        }

        void LateUpdate()
        {
            // a câmera do vídeo filma no lugar da do jogador (que a liga quando nasce) e é o ouvido da cena
            var jogador = JogadorRede.Local;
            if (jogador != null)
            {
                jogador.cameraMundo.enabled = !_comCamera;
                jogador.cameraBracos.enabled = !_comCamera;
                jogador.ouvido.enabled = !_comCamera;
            }
            cameraDoVideo.enabled = _comCamera;
            ouvidoDoVideo.enabled = _comCamera;
            ModoVideo.Olho = _comCamera ? cameraDoVideo.transform.position : (Vector3?)null;
            AplicarCorte();
            // o brilho a mais por cima do pós-processamento do dia e da noite (no celular, a noite fica escura demais)
            float noite = CicloDoDia.Noite;
            _brilho.postExposure.value = Mathf.Lerp(_brilhoDoDia, _brilhoDaNoite, noite) + Mathf.Lerp(_brilhoExtra, _brilhoExtraDeNoite, noite);
        }

        // ------------------------------------------------------------------ as tomadas

        /// <summary>Acerta a hora, a neblina, o brilho e a câmera da tomada e devolve o que ela faz.</summary>
        IEnumerator Preparar()
        {
            switch (tomada)
            {
                case Tomada.Mapa:
                    return PrepararMapa();
                case Tomada.Bunker:
                    return PrepararBunker();
                case Tomada.Zumbis:
                    return PrepararZumbis();
                case Tomada.Jogador:
                    return PrepararJogador();
                default:
                    // o teste da gravação: fim de tarde, a câmera chegando devagar no cruzamento do meio, com o prédio no fundo
                    Clima(17.5f, 0.6f, 0.3f);
                    cameraDoVideo.fieldOfView = 70f;
                    cameraDoVideo.nearClipPlane = 0.05f;
                    cameraDoVideo.farClipPlane = 400f;
                    var olhar = new Vector3(6f, 1.5f, 8f);
                    Posicionar(new Vector3(-14f, 9f, -28f), olhar);
                    return Travelling(new Vector3(-14f, 9f, -28f), new Vector3(-6f, 6.5f, -16f), olhar, olhar, 5f);
            }
        }

        /// <summary>
        /// A hora (que segue no ritmo normal do dia, se a tomada não mexer), quanto da neblina sobra e o brilho a mais (em
        /// pontos de exposição), de dia e de noite (sem o de noite, o mesmo).
        /// </summary>
        void Clima(float hora, float neblina, float brilho, float brilhoDeNoite = float.NaN)
        {
            CicloDoDia.MudarHora(hora);
            ModoVideo.Neblina = neblina;
            _brilhoExtra = brilho;
            _brilhoExtraDeNoite = float.IsNaN(brilhoDeNoite) ? brilho : brilhoDeNoite;
        }

        /// <summary>Até onde as sombras vão (m). O jogo usa 40: vista do alto, elas sumiam.</summary>
        void Sombras(float distancia)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            if (_sombraDoJogo < 0f) _sombraDoJogo = urp.shadowDistance;
            urp.shadowDistance = distancia;
        }

        void Posicionar(Vector3 posicao, Vector3 olhar) => cameraDoVideo.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(olhar - posicao));

        /// <summary>
        /// O corte de raio-X: o plano perto da câmera vira o plano horizontal na altura do corte (a matriz oblíqua, o mesmo
        /// truque dos reflexos na água), e nada acima dele é desenhado. A câmera pode olhar inclinada e o corte continua
        /// reto, como numa casa de boneca. Sem corte (ou com a câmera abaixo dele), a projeção normal.
        /// </summary>
        void AplicarCorte()
        {
            cameraDoVideo.ResetProjectionMatrix();
            if (float.IsNaN(_corte) || cameraDoVideo.transform.position.y < _corte + 0.5f) return;
            var mundoParaCamera = cameraDoVideo.worldToCameraMatrix;
            Vector3 ponto = mundoParaCamera.MultiplyPoint(new Vector3(0f, _corte, 0f));
            Vector3 normal = mundoParaCamera.MultiplyVector(Vector3.down).normalized;   // fica o que está embaixo
            cameraDoVideo.projectionMatrix = cameraDoVideo.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(ponto, normal)));
        }

        /// <summary>
        /// Um caminho de câmera por pontos-chave (o tempo, o lugar e para onde ela olha, no mundo), passando por todos sem
        /// trancos: Catmull-Rom com o tempo de cada trecho, parado no primeiro e no último ponto.
        /// </summary>
        void Percorrer(float t, float[] tempos, Vector3[] lugares, Vector3[] olhares)
        {
            int n = tempos.Length;
            if (t <= tempos[0] || t >= tempos[n - 1])
            {
                int ponta = t <= tempos[0] ? 0 : n - 1;
                Posicionar(lugares[ponta], olhares[ponta]);
                return;
            }
            int i = 0;
            while (t > tempos[i + 1]) i++;
            float s = (t - tempos[i]) / (tempos[i + 1] - tempos[i]);
            Posicionar(Hermite(lugares, tempos, i, s), Hermite(olhares, tempos, i, s));
        }

        static Vector3 Hermite(Vector3[] p, float[] tempos, int i, float s)
        {
            Vector3 Tangente(int j) => j == 0 || j == p.Length - 1 ? Vector3.zero : (p[j + 1] - p[j - 1]) / (tempos[j + 1] - tempos[j - 1]);
            float dt = tempos[i + 1] - tempos[i], s2 = s * s, s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * p[i] + (s3 - 2f * s2 + s) * dt * Tangente(i) + (3f * s2 - 2f * s3) * p[i + 1] + (s3 - s2) * dt * Tangente(i + 1);
        }

        /// <summary>A câmera em volta de um alvo: o giro (0 = ao sul, olhando para o norte), a inclinação para baixo e a distância.</summary>
        void Orbitar(Vector3 alvo, float giro, float inclinacao, float distancia)
        {
            var rotacao = Quaternion.Euler(inclinacao, giro, 0f);
            cameraDoVideo.transform.SetPositionAndRotation(alvo + rotacao * new Vector3(0f, 0f, -distancia), rotacao);
        }

        /// <summary>A câmera vai de um ponto a outro olhando de um alvo a outro, devagar no começo e no fim.</summary>
        IEnumerator Travelling(Vector3 de, Vector3 para, Vector3 olharDe, Vector3 olharPara, float duracao)
        {
            for (float t = 0f; t < duracao; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duracao);
                Posicionar(Vector3.Lerp(de, para, k), Vector3.Lerp(olharDe, olharPara, k));
                yield return null;
            }
            Posicionar(para, olharPara);
        }

        // ------------------------------------------------------------------ a folha de conferência

        /// <summary>A duração da tomada escolhida (s), para a folha de conferência.</summary>
        float Duracao => tomada switch { Tomada.Mapa => DuracaoDoMapa, Tomada.Bunker => DuracaoDoBunker, Tomada.Zumbis => DuracaoDosZumbis, Tomada.Jogador => DuracaoDoJogador, _ => 5f };

        // 12 quadros pequenos (270 × 480), do começo ao fim da tomada, numa folha de 6 × 2, desenhados pela câmera do vídeo
        // no fim do quadro (com o tempo do Recorder, o tempo do jogo é o do vídeo)
        IEnumerator Conferir(float inicio)
        {
            const int colunas = 6, linhas = 2, largura = 270, altura = 480;
            var folha = new Texture2D(colunas * largura, linhas * altura, TextureFormat.RGB24, false);
            var quadro = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
            var fimDoQuadro = new WaitForEndOfFrame();
            for (int i = 0; i < colunas * linhas; i++)
            {
                float quando = Mathf.Min(Duracao * i / (colunas * linhas - 1), Duracao - 0.05f);
                while (Time.time - inicio < quando && !Terminou) yield return null;
                yield return fimDoQuadro;
                var antes = cameraDoVideo.targetTexture;
                cameraDoVideo.targetTexture = quadro;
                AplicarCorte();   // a projeção com a proporção da textura
                cameraDoVideo.Render();
                cameraDoVideo.targetTexture = antes;
                AplicarCorte();
                var ativa = RenderTexture.active;
                RenderTexture.active = quadro;
                folha.ReadPixels(new Rect(0, 0, largura, altura), (i % colunas) * largura, (linhas - 1 - i / colunas) * altura);
                RenderTexture.active = ativa;
            }
            folha.Apply();
            string pasta = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Recordings", video));
            System.IO.Directory.CreateDirectory(pasta);
            string caminho = System.IO.Path.Combine(pasta, $"{NomeDaTomada}_folha.png");
            System.IO.File.WriteAllBytes(caminho, folha.EncodeToPNG());
            Destroy(folha);
            quadro.Release();
            Destroy(quadro);
            Debug.Log($"[vídeo] folha de conferência: {caminho}");
        }

        // um volume próprio, por cima dos dois do jogo, só com a exposição (o resto da gradação continua a deles)
        void CriarBrilho()
        {
            var ambiente = FindAnyObjectByType<AmbienteDoDia>();
            _brilhoDoDia = Exposicao(ambiente != null ? ambiente.volumeDia : null);
            _brilhoDaNoite = Exposicao(ambiente != null ? ambiente.volumeNoite : null);
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            _brilho = perfil.Add<ColorAdjustments>();
            _brilho.postExposure.overrideState = true;
            _volume = new GameObject("Brilho do vídeo").AddComponent<Volume>();
            _volume.transform.SetParent(transform, false);
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.sharedProfile = perfil;
        }

        static float Exposicao(Volume v)
        {
            if (v != null && v.sharedProfile != null && v.sharedProfile.TryGet(out ColorAdjustments c) && c.postExposure.overrideState) return c.postExposure.value;
            return 0f;
        }
    }
}
