using UnityEngine;
using UnityEngine.Video;
using ZombieCityCleaner.Music;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O fundo do menu do jogo (a pedido em 29/10/2026): o vídeo da cidade em laço (a manhã na neblina, as estátuas, o Oco
    /// na janela, o pisca-alerta, a cidade do alto; gravado pelo Video/FilmeDoMenu e montado pelo
    /// Ferramentas/video_do_menu.py), a capa (um quadro dele) se o vídeo faltar, e a trilha do menu (a calma com medo e
    /// angústia, do Ferramentas/trilha) em laço. Leve: o vídeo já vem pronto (1280 × 720, H.264, só decodificar), e enquanto
    /// ele cobre a tela a câmera do menu não desenha nada (<see cref="CameraDoMenu.Leve"/>). A trilha do menu continua pela
    /// tela de carregamento e some devagar quando o jogador nasce, e a trilha do jogo (Music/MaestroDaTrilha) entra.
    /// <para><b>A TV ligando</b> (a pedido: "está muito abrupto... deveria começar devagar, como se fosse um ligar de tv aos
    /// poucos até começar o menu inteiro"): cada vez que o menu aparece (ao abrir o jogo e ao voltar de uma partida), a tela
    /// fica preta até o vídeo ter o primeiro quadro, e então: um ponto claro no meio vira uma linha, a linha se abre na
    /// imagem, com o chiado e o tubo esquentando (a imagem sai do escuro, piscando de leve); depois o menu aparece devagar
    /// (<see cref="MenuVisivel"/>). Com o som de uma TV de tubo ligando (Audio/Menu/Resources/TV_Ligando.wav, do
    /// Ferramentas/tv_ligando.py: o botão, o "bum" da bobina, o estalo quando o ponto acende, a neve), que começa com a
    /// tela preta; a trilha do menu entra depois ("e depois já começaria a trilha"), 1,5 s depois do ponto, subindo do
    /// silêncio em 4 s.</para>
    /// Criado pelo <see cref="MenuOnline"/> na cena Mundo; o menu liga o <see cref="Pedido"/> enquanto o menu do começo está
    /// na tela (e enquanto conecta).
    /// </summary>
    public class FundoDoMenu : MonoBehaviour
    {
        /// <summary>O menu do começo está na tela: o fundo aparece e a trilha do menu toca.</summary>
        public static bool Pedido;

        // a TV ligando (em segundos): o preto depois do vídeo pronto, o ponto que vira linha, a linha abrindo na imagem, o
        // tubo esquentando; e o menu, que aparece depois
        const float Preto = 0.6f, Linha = 0.35f, Abrir = 0.5f, Aquecer = 1.8f;
        const float MenuComeca = Linha + Abrir + 0.6f, MenuDura = 1.6f;
        // a trilha do menu: começa depois do som da TV (segundos depois do ponto), e os segundos para subir toda e descer toda
        const float MusicaEntra = 1.5f, MusicaSobe = 4f, MusicaDesce = 3.3f;
        // no TV_Ligando.wav, o estalo do ponto acendendo fica aos 0,6 s (o arquivo começa com a tela preta)
        const float PontoNoSom = 0.6f;

        static FundoDoMenu _instancia;
        VideoPlayer _video;
        RenderTexture _rt;
        Texture2D _capa, _sombra, _chiado, _grao;
        const float ForcaDoGrao = 0.09f;
        AudioSource _musica, _tv;
        float _volumeDaMusica, _esperandoDesde, _ligaEm = -1f;
        bool _mostrando;

        /// <summary>
        /// O menu principal aparecendo (0 a 1): 0 durante a TV ligando, subindo devagar depois. Sem o fundo (a cena da demo),
        /// 1.
        /// </summary>
        public static float MenuVisivel
        {
            get
            {
                if (_instancia == null) return 1f;
                if (!_instancia._mostrando) return Pedido ? 0f : 1f;   // pedido e ainda não começou: nada do menu ainda
                float t = _instancia.Ligando;
                return t < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (t - MenuComeca) / MenuDura);
            }
        }

        // segundos desde o ponto claro aparecer (negativo enquanto a tela ainda está preta)
        float Ligando => _ligaEm < 0f ? -1f : Time.unscaledTime - _ligaEm;

        public static void Criar()
        {
            if (_instancia != null) return;
            var go = new GameObject("Fundo do menu");
            DontDestroyOnLoad(go);
            _instancia = go.AddComponent<FundoDoMenu>();
        }

        void Awake()
        {
            _capa = Resources.Load<Texture2D>("FundoDoMenu_Capa");
            var clipe = Resources.Load<VideoClip>("FundoDoMenu_Video");
            if (clipe != null)
            {
                _rt = new RenderTexture((int)clipe.width, (int)clipe.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _rt.Create();
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.isLooping = true;
                _video.source = VideoSource.VideoClip;
                _video.clip = clipe;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.targetTexture = _rt;
                _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.skipOnDrop = true;
                _video.waitForFirstFrame = true;
                _video.Prepare();
            }
            var trilha = Resources.Load<TrilhaDoJogo>("TrilhaDoJogo");
            if (trilha != null && trilha.menu != null)
            {
                _musica = gameObject.AddComponent<AudioSource>();
                _musica.clip = trilha.menu;
                _musica.loop = true;
                _musica.playOnAwake = false;
                _musica.spatialBlend = 0f;
                _musica.priority = 0;
                _musica.bypassReverbZones = true;
                _musica.bypassListenerEffects = true;
                _musica.volume = 0f;
            }
            var somDaTv = Resources.Load<AudioClip>("TV_Ligando");
            if (somDaTv != null)
            {
                _tv = gameObject.AddComponent<AudioSource>();
                _tv.clip = somDaTv;
                _tv.loop = false;
                _tv.playOnAwake = false;
                _tv.spatialBlend = 0f;
                _tv.priority = 0;
                _tv.bypassReverbZones = true;
                _tv.bypassListenerEffects = true;
            }
            _sombra = Sombra();
            _chiado = Chiado();
            _grao = Grao();
        }

        void OnDestroy()
        {
            if (_instancia == this) _instancia = null;
            if (_rt != null) _rt.Release();
        }

        bool VideoPronto => _video == null || (_video.isPrepared && _video.frame > 0);

        void Update()
        {
            bool mostrar = Pedido && !ModoVideo.Ligado;
            if (mostrar != _mostrando)
            {
                _mostrando = mostrar;
                CameraDoMenu.Leve(mostrar);
                _ligaEm = -1f;   // cada vez que o menu aparece, a TV liga de novo
                _esperandoDesde = Time.unscaledTime;
                if (_video != null)
                {
                    if (mostrar) _video.Play();
                    else _video.Pause();
                }
                if (!mostrar && _tv != null) _tv.Stop();
            }
            // a TV liga quando o vídeo tem o primeiro quadro (ou depois de 2 s, com a capa), após um instante no preto; o
            // som começa já, para o estalo dele cair no ponto acendendo
            if (_mostrando && _ligaEm < 0f && (VideoPronto || Time.unscaledTime - _esperandoDesde > 2f))
            {
                _ligaEm = Time.unscaledTime + Preto;
                if (_tv != null)
                {
                    _tv.volume = 0.9f * MaestroDaTrilha.Volume;
                    _tv.PlayScheduled(AudioSettings.dspTime + Mathf.Max(0f, Preto - PontoNoSom));
                }
            }

            // a trilha do menu: entra depois do som da TV, devagar; continua pela tela de carregamento; some quando o
            // jogador nasce
            var sessao = SessaoOnline.Instancia;
            var eu = JogadorRede.Local;
            bool carregando = sessao != null && sessao.Atual != SessaoOnline.Estado.Desconectado && (eu == null || !eu.IsSpawned);
            bool noMenu = _mostrando && Ligando >= MusicaEntra;
            float alvo = noMenu || (carregando && !ModoVideo.Ligado) ? 1f : 0f;
            _volumeDaMusica = Mathf.MoveTowards(_volumeDaMusica, alvo, Time.unscaledDeltaTime / (alvo > _volumeDaMusica ? MusicaSobe : MusicaDesce));
            if (_musica == null) return;
            if (_volumeDaMusica > 0.001f && !_musica.isPlaying) _musica.Play();
            else if (_volumeDaMusica <= 0.001f && _musica.isPlaying) _musica.Stop();   // na próxima vez, recomeça do começo
            // ao quadrado: o começo da subida quase não se ouve (sem o corte de entrar já tocando)
            _musica.volume = _volumeDaMusica * _volumeDaMusica * MaestroDaTrilha.Volume;
        }

        void OnGUI()
        {
            if (!_mostrando || Event.current.type != EventType.Repaint) return;
            GUI.depth = 20;   // atrás do menu
            float largura = Screen.width, altura = Screen.height, u = altura / 720f;
            var tela = new Rect(0, 0, largura, altura);
            GUI.color = Color.black;
            GUI.DrawTexture(tela, Texture2D.whiteTexture);
            float t = Ligando;
            Texture imagem = _video != null && _video.isPrepared && _video.frame > 0 ? _rt : _capa;
            var claro = new Color(0.85f, 0.92f, 1f);
            if (t < 0f || imagem == null)
            {
                GUI.color = Color.white;
                return;
            }
            // 1) o ponto claro no meio que vira uma linha
            if (t < Linha)
            {
                float p = 1f - Mathf.Pow(1f - t / Linha, 3f);
                float w = Mathf.Lerp(6f * u, largura, p);
                GUI.color = new Color(claro.r, claro.g, claro.b, 0.25f * Mathf.Clamp01(t / 0.1f));
                GUI.DrawTexture(new Rect((largura - w) / 2f, altura / 2f - 6f * u, w, 12f * u), Texture2D.whiteTexture);   // o brilho em volta
                GUI.color = new Color(claro.r, claro.g, claro.b, Mathf.Clamp01(t / 0.08f));
                GUI.DrawTexture(new Rect((largura - w) / 2f, altura / 2f - 1f * u, w, 2f * u), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }
            // 2) a linha se abre na imagem (do meio para cima e para baixo), com o clarão diminuindo
            float abrir = Mathf.Clamp01((t - Linha) / Abrir);
            float a = 1f - Mathf.Pow(1f - abrir, 3f);
            float alto = Mathf.Lerp(2f * u, altura, a), topo = (altura - alto) / 2f;
            GUI.color = Color.white;
            GUI.BeginGroup(new Rect(0, topo, largura, alto));
            GUI.DrawTexture(new Rect(0, -topo, largura, altura), imagem, ScaleMode.ScaleAndCrop);
            GUI.EndGroup();
            if (abrir < 1f)
            {
                GUI.color = new Color(claro.r, claro.g, claro.b, 0.6f * (1f - abrir) * (1f - abrir));
                GUI.DrawTexture(new Rect(0, topo, largura, alto), Texture2D.whiteTexture);
            }
            // 3) o tubo esquentando: a imagem sai do escuro, piscando de leve no começo, e o chiado some
            float aquecer = Mathf.Clamp01((t - Linha - Abrir) / Aquecer);
            float escuro = 0.6f * (1f - Mathf.SmoothStep(0f, 1f, aquecer));
            if (aquecer < 0.35f) escuro += Random.Range(-0.06f, 0.06f) * (1f - aquecer / 0.35f);
            if (escuro > 0.002f)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(escuro));
                GUI.DrawTexture(tela, Texture2D.whiteTexture);
            }
            // o chiado só na parte da tela que já abriu
            float chiado = 0.22f * (1f - Mathf.Clamp01((t - Linha) / (Abrir + Aquecer * 0.6f)));
            if (chiado > 0.002f && _chiado != null)
            {
                GUI.color = new Color(1f, 1f, 1f, chiado);
                GUI.DrawTextureWithTexCoords(new Rect(0, topo, largura, alto), _chiado, new Rect(Random.value, Random.value, 2f, 2f * alto / altura));
            }
            // o grão de filme, na resolução da tela (no vídeo ele custaria caro: a compressão vira blocos no escuro), mudando a
            // cada quadro
            if (_grao != null)
            {
                float px = Mathf.Max(1f, altura / 960f);   // um ponto do grão a cada ~1,5 pixel a 1440
                GUI.color = new Color(1f, 1f, 1f, ForcaDoGrao * aquecer);
                GUI.DrawTextureWithTexCoords(tela, _grao, new Rect(Random.value, Random.value, largura / (_grao.width * px), altura / (_grao.height * px)));
            }
            // a sombra da esquerda (o menu fica legível sobre qualquer quadro), junto com a imagem
            GUI.color = new Color(1f, 1f, 1f, aquecer);
            GUI.DrawTexture(new Rect(0, 0, largura * 0.62f, altura), _sombra);
            GUI.color = Color.white;
        }

        // um degradê de preto (da esquerda, quase opaco, até sumir), feito na hora
        static Texture2D Sombra()
        {
            const int n = 256;
            var t = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < n; i++)
            {
                float a = 1f - i / (n - 1f);
                t.SetPixel(i, 0, new Color(0f, 0f, 0f, 0.78f * a * a * (3f - 2f * a)));
            }
            t.Apply();
            return t;
        }

        // o grão: pontos claros e escuros (branco ou preto, mais ou menos transparentes), quase sem mudar o brilho médio
        static Texture2D Grao()
        {
            const int lado = 256;
            var t = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var pontos = new Color32[lado * lado];
            var sorteio = new System.Random(11);
            for (int i = 0; i < pontos.Length; i++)
            {
                // quase normal (a soma de três sorteios), de -1 a 1
                float v = (float)(sorteio.NextDouble() + sorteio.NextDouble() + sorteio.NextDouble()) / 1.5f - 1f;
                byte cor = v > 0f ? (byte)255 : (byte)0;
                pontos[i] = new Color32(cor, cor, cor, (byte)(Mathf.Clamp01(Mathf.Abs(v)) * 255));
            }
            t.SetPixels32(pontos);
            t.Apply();
            return t;
        }

        // o chiado da TV: pontos cinzas ao acaso (desenhado 2 vezes repetido, de um lugar sorteado a cada quadro)
        static Texture2D Chiado()
        {
            const int w = 640, h = 360;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var pontos = new Color32[w * h];
            var sorteio = new System.Random(7);
            for (int i = 0; i < pontos.Length; i++)
            {
                byte v = (byte)sorteio.Next(0, 256);
                pontos[i] = new Color32(v, v, v, 255);
            }
            t.SetPixels32(pontos);
            t.Apply();
            return t;
        }
    }
}
