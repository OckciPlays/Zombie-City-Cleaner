using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Roupas;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// A introdução do Dia 0 (01/11/2026, a pedido: "no começo vamos estar todos no sofá como se fosse em uma festa", "sem
    /// voz por enquanto, uma trilha sonora boa estilo Hollywood e uma animação que se encaixe em tudo isso, depois uma
    /// escrita que vai contando aquela história"). Toca em cada máquina, depois de criar o personagem (os amigos não
    /// aparecem: cada um tem o seu), com a trilha (<see cref="trilha"/>, do Ferramentas/trilha/intro_dia0.py) e o texto
    /// escrito aos poucos embaixo, nas faixas pretas de cinema:
    ///  - a sala, do sofá: a TV ligando no meio da festa, o jornal (o estúdio, desenhado na tela da TV por uma câmera);
    ///  - as imagens do jornal, na tela inteira: o hospital no Brasil (os primeiros sintomas), o homem de pedra na rua, a
    ///    praça das estátuas, o corredor do hospital no dia 30 (o ataque, no escuro e na luz vermelha);
    ///  - de volta à TV: os primeiros casos nos Estados Unidos, no nosso estado; a última pancada e o fim.
    /// Os cenários ficam longe da rua (montados quando a introdução começa, e apagados no fim). Enter ou Espaço pula.
    /// </summary>
    public class IntroDoDiaZero : MonoBehaviour
    {
        [Tooltip("A trilha da introdução (Audio/DiaZero/IntroDoDiaZero.ogg).")]
        public AudioClip trilha;
        [Tooltip("O material da tela da TV (URP Unlit): a imagem do estúdio.")]
        public Material telaDaTv;

        /// <summary>A introdução já tocou nesta partida (nesta máquina).</summary>
        public static bool JaViu { get; set; }
        public static bool Tocando { get; private set; }

        public const float Duracao = 91f;
        static readonly Vector3 Longe = new Vector3(0f, 0f, -420f);

        RuaDoDiaZero _rua;
        Transform _cenarios;
        Camera _cam, _estudio;
        RenderTexture _rt;
        AudioSource _musica, _festa, _tv;
        float _desde;
        bool _saindo;
        float _saindoDesde;
        // os cenários
        Transform _quarto, _rua1, _praca, _corredor, _sala;
        Light _vermelha, _branca;
        Transform _ancora;
        MeshFilter _faixaDoJornal;
        List<ModelosDaEstrada.Texto> _textoDaFaixa;
        Transform _telaQuad;
        Vector3 _tvCentro, _tvFrente;
        NpcDoCinema _atacante, _vitima;

        // ------------------------------------------------------------------ o roteiro
        // (o tempo de cada cena e o texto, no tempo da trilha)
        struct Fala { public float de, ate; public string texto; }
        static readonly Fala[] Falas =
        {
            new Fala { de = 1.5f, ate = 9.5f, texto = "Era para ser só mais uma tarde com os amigos." },
            new Fala { de = 10.5f, ate = 17f, texto = "No Brasil, os primeiros casos: febre, olhos vermelhos e uma coceira pelo corpo todo." },
            new Fala { de = 17.2f, ate = 23.6f, texto = "Alguns precisaram ficar internados. Outros seguiram a vida normalmente." },
            new Fala { de = 24.5f, ate = 31.6f, texto = "Dias depois, um homem virou pedra no meio da rua, sob o sol." },
            new Fala { de = 31.8f, ate = 39.6f, texto = "Ninguém sabia como, nem por quê. Mas todos sabiam que era a doença." },
            new Fala { de = 40.5f, ate = 47.6f, texto = "Depois vieram outros. Gente virando pedra em todo o país." },
            new Fala { de = 47.8f, ate = 55.6f, texto = "Os hospitais lotaram. E os que não viraram pedra continuaram doentes." },
            new Fala { de = 56.5f, ate = 63.8f, texto = "Trinta dias depois, eles se transformaram em algo que ninguém conhecia." },
            new Fala { de = 64f, ate = 71.6f, texto = "Atacaram médicos, enfermeiros, parentes e amigos." },
            new Fala { de = 73f, ate = 78.6f, texto = "Hoje, os primeiros sintomas apareceram nos Estados Unidos." },
            new Fala { de = 78.8f, ate = 83.8f, texto = "No nosso estado." },
            new Fala { de = 85f, ate = 90.5f, texto = "Vocês saem para a rua. Por enquanto, é só mais um dia normal." },
        };

        static string FaixaDoJornal(float t) =>
            t < 24f ? "URGENTE  ·  DOENÇA MISTERIOSA SE ESPALHA PELO BRASIL"
            : t < 40f ? "BRASIL  ·  HOMEM VIRA PEDRA EM PLENA RUA"
            : t < 56f ? "BRASIL  ·  CASOS DE PETRIFICAÇÃO EM TODO O PAÍS"
            : t < 72f ? "DIA 30  ·  DOENTES ATACAM EM HOSPITAIS"
            : "URGENTE  ·  PRIMEIROS CASOS NOS EUA  ·  ESTADO DE WASHINGTON";

        // ------------------------------------------------------------------ começar e acabar

        /// <summary>A introdução desta cena (a TV da sala chama).</summary>
        public static IntroDoDiaZero Instancia { get; private set; }
        bool _compartilhada;

        void Update()
        {
            if (!Application.isPlaying) return;
            Instancia = this;
            if (_rua == null) _rua = GetComponent<RuaDoDiaZero>();
            if (!Tocando)
            {
                PrepararATv();
                Festa();
                // a TV foi ligada (o jornal para todos, no tempo do anfitrião): quem chega no meio vê dali em diante
                var j = JogadorRede.Local;
                var dia = DiaZeroDaPartida.Instancia;
                if (!JaViu && _tvPronta && DiaZeroDaPartida.NoDia && dia != null && dia.Fase == FaseDoDia.Tv && j != null && j.Criado &&
                    !TelaDeCriarPersonagem.Aberta && !MenuOnline.Aberto)
                {
                    float jaFoi = dia.TempoDaTv;
                    if (jaFoi > Duracao - 3f)
                    {
                        JaViu = true;
                        dia.AcabeiATv();
                    }
                    else Comecar(Mathf.Max(0f, jaFoi), true);
                }
                return;
            }
            float t = Time.time - _desde;
            var k = Keyboard.current;
            if (!_saindo && t > 1.5f && k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
            {
                _saindo = true;
                _saindoDesde = Time.time;
            }
            if (_saindo && Time.time - _saindoDesde > 0.8f || t > Duracao || !DiaZeroDaPartida.NoDia)
            {
                Acabar();
                return;
            }
            if (_saindo && _musica != null) _musica.volume = Mathf.Lerp(1f, 0f, (Time.time - _saindoDesde) / 0.8f);
            Filmar(t);
        }

        void OnDestroy()
        {
            if (Tocando) Acabar();
            if (Instancia == this) Instancia = null;
        }

        /// <summary>Ver o jornal de novo (a TV, depois que a rua foi liberada): só nesta máquina.</summary>
        public void Reprisar()
        {
            if (!Tocando && _tvPronta) Comecar(0f, false);
        }

        // a festa na sala enquanto todos chegam (o lobby); depois da TV, ela fica só na introdução
        void Festa()
        {
            var dia = DiaZeroDaPartida.Instancia;
            bool naCasa = DiaZeroDaPartida.NoDia && dia != null && dia.Fase == FaseDoDia.Casa && JogadorRede.Local != null;
            if (naCasa && _festa == null) _festa = Fonte("Festa", SonsDoDiaZero.Festa, 0.3f, true);
            if (_festa == null) return;
            if (naCasa && !_festa.isPlaying) _festa.Play();
            if (!naCasa && !Tocando && _festa.isPlaying) _festa.Stop();
            if (naCasa) _festa.volume = 0.3f;
        }

        void Comecar(float jaFoi, bool compartilhada)
        {
            if (_rua == null) _rua = GetComponent<RuaDoDiaZero>();
            if (_rua == null || !_rua.Pronta) return;
            JaViu = true;
            Tocando = true;
            _compartilhada = compartilhada;
            _saindo = false;
            MenuOnline.Cinema = true;
            _desde = Time.time - jaFoi;
            Montar();
            var go = new GameObject("Câmera da introdução");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.depth = 60;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 600f;
            _cam.fieldOfView = 55f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.7f, 0.78f, 0.86f);
            _cam.cullingMask = ~((1 << 6) | (1 << 10));
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            _musica = Fonte("Trilha", trilha, 1f, false);
            if (_festa == null) _festa = Fonte("Festa", SonsDoDiaZero.Festa, 0.5f, true);
            _tv = Fonte("TV ligando", Resources.Load<AudioClip>("TV_Ligando"), 0.6f, false);
            if (_musica != null && _musica.clip != null)
            {
                _musica.Play();
                if (jaFoi > 0.05f) _musica.time = Mathf.Min(jaFoi, _musica.clip.length - 0.1f);
            }
            if (_festa != null && !_festa.isPlaying) _festa.Play();
            if (_tv != null && _tv.clip != null && jaFoi < 1f) _tv.PlayDelayed(1.2f - jaFoi);
            Debug.Log($"[Dia 0] A introdução começou{(jaFoi > 0.5f ? $" (a {jaFoi:0} s)" : "")}{(compartilhada ? "" : " (de novo, só aqui)")}.");
        }

        AudioSource Fonte(string nome, AudioClip clip, float volume, bool laco)
        {
            if (clip == null) return null;
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.loop = laco;
            a.spatialBlend = 0f;
            a.playOnAwake = false;
            return a;
        }

        void Acabar()
        {
            Tocando = false;
            MenuOnline.Cinema = false;
            AmbienteDoDia.Escurecer = 0f;
            if (_cam != null) Destroy(_cam.gameObject);
            if (_estudio != null) Destroy(_estudio.gameObject);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            foreach (var a in new[] { _musica, _tv }) if (a != null) Destroy(a.gameObject);
            if (_festa != null) _festa.Stop();
            if (_cenarios != null) Destroy(_cenarios.gameObject);
            if (_telaQuad != null) Destroy(_telaQuad.gameObject);
            _telaQuad = null;
            _faixaAgora = null;
            // o jornal de todos: avisa o anfitrião (com todos tendo visto, a rua é liberada)
            if (_compartilhada) DiaZeroDaPartida.Instancia?.AcabeiATv();
            _compartilhada = false;
            Debug.Log("[Dia 0] A introdução acabou.");
        }

        // ------------------------------------------------------------------ as cenas

        void Filmar(float t)
        {
            // a festa abafa quando o jornal começa (alguém pediu silêncio) e some nas imagens
            if (_festa != null) _festa.volume = t < 8f ? 0.5f : t > 72f && t < 84f ? 0.12f : 0.05f;
            AmbienteDoDia.Escurecer = t >= 56f && t < 72f ? 1f : 0f;
            bool naTv = t < 10f || (t >= 72f && t < 84f);
            if (_estudio != null) _estudio.enabled = naTv;
            AtualizarAFaixa(t);
            if (t < 10f) NaSala(t / 10f, false);
            else if (t < 24f) NoQuarto((t - 10f) / 14f);
            else if (t < 40f) NaRua((t - 24f) / 16f);
            else if (t < 56f) NaPraca((t - 40f) / 16f);
            else if (t < 72f) NoCorredor(t - 56f);
            else if (t < 84f) NaSala((t - 72f) / 12f, true);
            else Final(t - 84f);
        }

        // a TV de frente, na altura dela (a câmera chega perto devagar; na volta, mais perto)
        void NaSala(float u, bool volta)
        {
            if (_telaQuad == null) Tela();
            var alvo = _tvCentro;
            float longe = volta ? Mathf.Lerp(1.5f, 0.95f, u) : Mathf.Lerp(2.6f, 1.45f, Mathf.SmoothStep(0f, 1f, u));
            var de = alvo + _tvFrente * longe + Vector3.up * 0.06f;
            _cam.transform.SetPositionAndRotation(de, Quaternion.LookRotation(alvo - de));
            _cam.fieldOfView = 50f;
        }

        void NoQuarto(float u)
        {
            var a = _quarto.TransformPoint(new Vector3(-4.5f, 1.55f, -1.4f));
            var b = _quarto.TransformPoint(new Vector3(3.5f, 1.45f, -1.6f));
            var p = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, u));
            var olhar = _quarto.TransformPoint(new Vector3(Mathf.Lerp(-2f, 4.5f, u), 0.8f, 1.5f));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(olhar - p));
            _cam.fieldOfView = 58f;
        }

        void NaRua(float u)
        {
            var centro = _rua1.TransformPoint(new Vector3(0f, 1.2f, -1.5f));
            float ang = Mathf.Lerp(-50f, 40f, u) * Mathf.Deg2Rad;
            float dist = Mathf.Lerp(6.0f, 3.2f, u);
            var p = centro + _rua1.TransformDirection(new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang))) * dist + Vector3.up * Mathf.Lerp(0.5f, 0.15f, u);
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(centro + Vector3.up * 0.25f - p));
            _cam.fieldOfView = 50f;
        }

        void NaPraca(float u)
        {
            var centro = _praca.TransformPoint(new Vector3(0f, 1f, 0f));
            var p = _praca.TransformPoint(new Vector3(Mathf.Lerp(-9f, 7f, u), Mathf.Lerp(4.5f, 1.8f, u), Mathf.Lerp(-13f, -9f, u)));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(centro - p));
            _cam.fieldOfView = 55f;
        }

        // o corredor do hospital no escuro: a luz vermelha piscando, a câmera tremendo atrás de quem ataca
        void NoCorredor(float s)
        {
            float pisca = Mathf.PerlinNoise(s * 7f, 0.3f) > 0.45f ? 1f : 0.25f;
            if (_vermelha != null) _vermelha.intensity = 6f * (0.6f + 0.4f * Mathf.Sin(s * 5f));
            if (_branca != null) _branca.intensity = 3.5f * pisca;
            float tremor = s > 7f ? 0.05f : 0.015f;
            var p = _corredor.TransformPoint(new Vector3(0.55f + Mathf.Sin(s * 13f) * tremor, 1.6f + Mathf.Sin(s * 17f) * tremor, Mathf.Lerp(-2.6f, -1.1f, s / 16f)));
            var alvo = _corredor.TransformPoint(new Vector3(-0.2f, 1.25f, 1.2f));
            _cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(alvo - p));
            _cam.fieldOfView = 60f;
            if (_atacante != null) _atacante.Investida = Mathf.Clamp01((s - 2f) / 6f);
        }

        void Final(float s)
        {
            // a última pancada: a sala de novo, mais de longe (os amigos saindo)
            var alvo = _tvCentro;
            var de = alvo + _tvFrente * Mathf.Lerp(1.0f, 3.2f, Mathf.SmoothStep(0f, 1f, s / 7f)) + Vector3.up * Mathf.Lerp(0.06f, 0.5f, s / 7f);
            _cam.transform.SetPositionAndRotation(de, Quaternion.LookRotation(alvo - de));
            if (_estudio != null) _estudio.enabled = false;
            if (_telaQuad != null) _telaQuad.gameObject.SetActive(s < 0.3f);   // a TV desliga
        }

        // ------------------------------------------------------------------ a TV da sala

        // a tela de cada TV da mobília nova, medida no modelo (a face de esmalte da frente, nas coordenadas dele: o meio e o
        // tamanho). A da TV de tubo fica à esquerda dos botões, para quem olha
        static readonly Dictionary<string, (Vector3 meio, Vector2 tamanho)> Telas = new Dictionary<string, (Vector3, Vector2)>
        {
            ["TVTubo"] = (new Vector3(0.05f, 0.255f, 0.263f), new Vector2(0.386f, 0.306f)),
            ["TVConsole"] = (new Vector3(0.17f, 0.50f, 0.283f), new Vector2(0.466f, 0.346f)),
        };

        bool _tvPronta;
        Vector2 _tvTamanho;
        float _procurandoDesde = -1f;

        /// <summary>A TV da sala foi achada (a tela e o E dela).</summary>
        public bool TvPronta => _tvPronta;

        // acha a TV da sala (a mobília de perto da casa, que entra quando alguém está nela) e põe o E na frente da tela; sem
        // ela em 15 s, uma tela no lugar do rack
        void PrepararATv()
        {
            if (_tvPronta || _rua == null || !_rua.Pronta || _rua.NossaCasa == null || JogadorRede.Local == null) return;
            if (_procurandoDesde < 0f) _procurandoDesde = Time.time;
            Transform tv = null;
            foreach (var t in _rua.NossaCasa.GetComponentsInChildren<Transform>())
                if (Telas.ContainsKey(t.name)) { tv = t; break; }
            if (tv != null)
            {
                var (meio, tam) = Telas[tv.name];
                var escala = tv.lossyScale;
                _tvCentro = tv.TransformPoint(meio + Vector3.forward * 0.004f);
                _tvFrente = tv.forward;
                _tvTamanho = new Vector2(tam.x * escala.x, tam.y * escala.y) * 0.97f;
            }
            else if (Time.time - _procurandoDesde > 15f)
            {
                var frente = _rua.OlhoNoSofa - _rua.Tv;
                frente.y = 0f;
                frente.Normalize();
                _tvCentro = _rua.Tv + frente * 0.05f;
                _tvFrente = frente;
                _tvTamanho = new Vector2(0.5f, 0.38f);
            }
            else return;
            _tvPronta = true;
            // o E da TV: uma caixa na frente da tela (um pouco à frente do móvel, para a mira achar antes dele)
            var go = new GameObject("Televisão (E)");
            go.transform.SetParent(_rua.NossaCasa, true);
            go.transform.SetPositionAndRotation(_tvCentro + _tvFrente * 0.12f, Quaternion.LookRotation(_tvFrente));
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(_tvTamanho.x + 0.3f, _tvTamanho.y + 0.3f, 0.3f);
            go.AddComponent<TelevisaoDoDiaZero>();
        }

        // a imagem do estúdio na tela da TV (um quad na frente da tela de verdade)
        void Tela()
        {
            if (telaDaTv == null || !_tvPronta) return;
            _rt = new RenderTexture(512, 384, 16) { name = "Tela da TV" };
            var mat = new Material(telaDaTv) { name = "Tela da TV" };
            mat.SetTexture("_BaseMap", _rt);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Tela da TV (introdução)";
            go.transform.SetPositionAndRotation(_tvCentro, Quaternion.LookRotation(-_tvFrente, Vector3.up));
            go.transform.localScale = new Vector3(_tvTamanho.x, _tvTamanho.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            _telaQuad = go.transform;
            if (_estudio != null) _estudio.targetTexture = _rt;
        }

        // ------------------------------------------------------------------ os cenários (longe da rua)

        void Montar()
        {
            _cenarios = new GameObject("Cenários da introdução").transform;
            _cenarios.SetParent(transform, false);
            _cenarios.position = new Vector3((_rua.Inicio + _rua.Fim) / 2f, 0f, 0f) + Longe;
            var M = Medidas.DoJogo;
            var mat = _rua.material;
            Mesh Movel(string nome)
            {
                if (_rua.mobilia != null) foreach (var m in _rua.mobilia) if (m != null && m.name == nome) return m;
                return null;
            }
            var npcs = GetComponent<NpcsDoDiaZero>();
            var npc = npcs != null ? npcs.npc : null;
            {
                var chao = new Obra(5, M);
                chao.Caixa(new Vector3(70f, -0.3f, 0f), new Vector3(260f, 0.4f, 120f), "Grama");
                Peca("Chão dos cenários", chao, _cenarios, mat);
            }

            // o estúdio do jornal (a câmera dele desenha na tela da TV)
            {
                var est = Filho("Estúdio", new Vector3(0f, 0f, 0f));
                var o = new Obra(11, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 6f), "Piso");
                o.Caixa(new Vector3(0f, 2f, 2.2f), new Vector3(8f, 4f, 0.1f), "CarroAzul");
                for (int i = -2; i <= 2; i++) o.Caixa(new Vector3(i * 1.4f, 2.2f, 2.12f), new Vector3(1.1f, 2.4f, 0.04f), i % 2 == 0 ? "PlacaBranca" : "ParedeAzul");
                o.Caixa(new Vector3(0f, 0.5f, 0.6f), new Vector3(2.4f, 1.0f, 0.8f), "Madeira");
                o.Caixa(new Vector3(0f, 1.02f, 0.6f), new Vector3(2.5f, 0.04f, 0.9f), "PlacaBranca");
                Peca("Estúdio", o, est, mat);
                if (npc != null)
                {
                    // em pé atrás da mesa, com os papéis na mão
                    var a = Instantiate(npc, est.TransformPoint(new Vector3(0f, 0.02f, 1.35f)), est.rotation * Quaternion.Euler(0f, 180f, 0f), est);
                    a.name = "Apresentador";
                    var v = a.GetComponent<Vestimenta>();
                    if (v != null) { v.npc = ""; v.VestirSorteado("Civil", 77); }
                    a.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.Prancheta);
                    _ancora = a.transform;
                }
                var luz = new GameObject("Luz do estúdio").AddComponent<Light>();
                luz.transform.SetParent(est, false);
                luz.transform.localPosition = new Vector3(0f, 2.6f, -2.5f);
                luz.type = LightType.Point;
                luz.range = 8f;
                luz.intensity = 2.5f;
                var cam = new GameObject("Câmera do estúdio").AddComponent<Camera>();
                cam.transform.SetParent(est, false);
                cam.transform.localPosition = new Vector3(0f, 1.52f, -1.7f);
                cam.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
                cam.fieldOfView = 30f;
                // a faixa do jornal e o logotipo, presos na frente da câmera (a 1 m: 0,72 × 0,54 m de quadro)
                var faixa = new Obra(12, M);
                faixa.Caixa(new Vector3(0f, -0.205f, 1f), new Vector3(0.74f, 0.085f, 0.004f), "PlacaVermelha");
                faixa.Caixa(new Vector3(-0.29f, 0.215f, 1f), new Vector3(0.12f, 0.06f, 0.004f), "PlacaVermelha");
                Peca("Faixa", faixa, cam.transform, mat);
                _textoDaFaixa = new List<ModelosDaEstrada.Texto> { TextoDaFaixa(FaixaDoJornal(0f)) };
                _faixaDoJornal = Letras(cam.transform, new Vector3(0f, -0.205f, 0.996f), _textoDaFaixa);
                Letras(cam.transform, new Vector3(-0.29f, 0.215f, 0.996f), new List<ModelosDaEstrada.Texto>
                {
                    new ModelosDaEstrada.Texto { Conteudo = "24H", U = 0f, V = 0f, Z = 0f, Altura = 0.032f, Alinhamento = 0, Cor = ModelosDaEstrada.LetraClara },
                });
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.08f, 0.16f);
                cam.enabled = false;
                cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                if (_rt != null) cam.targetTexture = _rt;
                _estudio = cam;
            }

            // o quarto do hospital no Brasil: as camas, os doentes deitados, a médica e a enfermeira
            {
                _quarto = Filho("Quarto do hospital", new Vector3(30f, 0f, 0f));
                var o = new Obra(21, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 6f), "Azulejo");
                o.Caixa(new Vector3(0f, 3.05f, 0f), new Vector3(12f, 0.1f, 6f), "ParedeInterna");
                o.Caixa(new Vector3(0f, 1.5f, 3f), new Vector3(12f, 3f, 0.1f), "ParedeVerde");
                o.Caixa(new Vector3(0f, 1.5f, -3f), new Vector3(12f, 3f, 0.1f), "ParedeVerde");
                o.Caixa(new Vector3(-6f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), "ParedeVerde");
                o.Caixa(new Vector3(6f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), "ParedeVerde");
                for (int i = 0; i < 4; i++) o.Caixa(new Vector3(-4.2f + i * 2.8f, 1.6f, 2.93f), new Vector3(1.2f, 0.9f, 0.04f), "Vidro");
                Peca("Quarto", o, _quarto, mat);
                var cama = Movel("CamaDeHospital");
                for (int i = 0; i < 4; i++)
                {
                    var p = new Vector3(-4.2f + i * 2.8f, 0f, 1.6f);
                    if (cama != null) Movel3(_quarto, cama, p, 180f, new Vector3(1.0f / 1.0f, 1f, 1f), mat);
                    if (npc == null) continue;
                    // deitado de costas: os pés na ponta de baixo da cama (para o quarto), a cabeça na cabeceira (a cama está
                    // girada: a cabeceira fica do lado da parede), as costas em cima do colchão (0,52 m)
                    var d = Instantiate(npc, _quarto.TransformPoint(p + new Vector3(0f, 0.64f, -0.82f)), _quarto.rotation * Quaternion.Euler(-90f, 180f, 0f), _quarto);
                    var v = d.GetComponent<Vestimenta>();
                    if (v != null) { v.npc = ""; v.VestirSorteado(i % 2 == 0 ? "Civil" : "Idoso", 300 + i); }
                    d.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.Deitado, doente: true);
                }
                if (npc != null)
                {
                    var m1 = Instantiate(npc, _quarto.TransformPoint(new Vector3(-1.4f, 0f, 0.1f)), _quarto.rotation * Quaternion.Euler(0f, 20f, 0f), _quarto);
                    var v1 = m1.GetComponent<Vestimenta>();
                    if (v1 != null) { v1.npc = ""; v1.VestirSorteado("Médico", 41); }
                    m1.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.Prancheta);
                    var m2 = Instantiate(npc, _quarto.TransformPoint(new Vector3(3.0f, 0f, 0.3f)), _quarto.rotation * Quaternion.Euler(0f, -10f, 0f), _quarto);
                    var v2 = m2.GetComponent<Vestimenta>();
                    if (v2 != null) { v2.npc = ""; v2.VestirSorteado("Médico", 42); }
                    m2.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.Nenhuma);
                }
                foreach (var x in new[] { -3f, 0f, 3f })
                {
                    var l = new GameObject("Luz").AddComponent<Light>();
                    l.transform.SetParent(_quarto, false);
                    l.transform.localPosition = new Vector3(x, 2.8f, 0f);
                    l.type = LightType.Point;
                    l.range = 6f;
                    l.intensity = 1.8f;
                    l.color = new Color(0.9f, 1f, 0.95f);
                }
            }

            // a rua no Brasil: o homem de pedra na calçada, a faixa da polícia, quem olha de longe
            {
                _rua1 = Filho("Rua do homem de pedra", new Vector3(60f, 0f, 0f));
                var o = new Obra(31, M);
                o.Caixa(new Vector3(0f, -0.15f, 4f), new Vector3(40f, 0.3f, 8f), "Asfalto");
                o.Caixa(new Vector3(0f, -0.05f, -1.5f), new Vector3(40f, 0.3f, 3.5f), "Calcada");
                for (int i = -3; i <= 3; i++)
                {
                    o.Caixa(new Vector3(i * 5.5f, 3f, -4.5f), new Vector3(5.3f, 6f + (i % 2) * 2f, 3f), i % 3 == 0 ? "ParedeBege" : i % 3 == 1 ? "ParedeAzul" : "Laranja");
                    o.Caixa(new Vector3(i * 6f + 2f, 4f, 11.5f), new Vector3(5.6f, 8f + (i % 3) * 2f, 3f), i % 3 == 0 ? "ParedeVerde" : i % 3 == 1 ? "ConcretoClaro" : "ParedeBege");
                    for (int j = 0; j < 3; j++)
                        o.Caixa(new Vector3(i * 6f + 2f + (j - 1) * 1.6f, 2.5f + (i % 2) * 2.5f, 9.98f), new Vector3(0.9f, 1.1f, 0.04f), "Vidro");
                }
                o.Caixa(new Vector3(0f, -0.05f, 9f), new Vector3(40f, 0.3f, 2.5f), "Calcada");
                // a faixa amarela da polícia em volta
                foreach (var (a, b) in new[] { (new Vector3(-2.5f, 0.9f, -0.2f), new Vector3(2.5f, 0.9f, -0.2f)), (new Vector3(-2.5f, 0.9f, -2.8f), new Vector3(2.5f, 0.9f, -2.8f)),
                                              (new Vector3(-2.5f, 0.9f, -0.2f), new Vector3(-2.5f, 0.9f, -2.8f)), (new Vector3(2.5f, 0.9f, -0.2f), new Vector3(2.5f, 0.9f, -2.8f)) })
                    o.CaixaEntre(a, b, 0.03f, 0.06f, "PlacaAmarela");
                foreach (var x in new[] { -2.5f, 2.5f })
                    foreach (var z in new[] { -0.2f, -2.8f })
                        o.Caixa(new Vector3(x, 0.45f, z), new Vector3(0.06f, 0.9f, 0.06f), "Laranja");
                Peca("Rua", o, _rua1, mat);
                if (npc != null)
                {
                    var pedra = Instantiate(npc, _rua1.TransformPoint(new Vector3(0f, 0.1f, -1.5f)), _rua1.rotation * Quaternion.Euler(0f, 160f, 0f), _rua1);
                    var v = pedra.GetComponent<Vestimenta>();
                    if (v != null) { v.npc = ""; v.VestirSorteado("Trabalhador", 51); }
                    pedra.AddComponent<NpcDoCinema>().Iniciar("Parado", NpcDoCinema.Pose.ProtegendoDoSol, pedra: true);
                    for (int i = 0; i < 5; i++)
                    {
                        var p = new Vector3(-5f + i * 2.5f + (i % 2) * 0.6f, 0.1f, -2.75f - (i % 2) * 0.1f);
                        var q = Instantiate(npc, _rua1.TransformPoint(p), Quaternion.LookRotation(_rua1.TransformPoint(new Vector3(0f, 0f, -1.5f)) - _rua1.TransformPoint(p)), _rua1);
                        var vq = q.GetComponent<Vestimenta>();
                        if (vq != null) { vq.npc = ""; vq.VestirSorteado(i == 2 ? "Policial" : "Civil", 60 + i); }
                        q.AddComponent<NpcDoCinema>().Iniciar("Parado", i == 1 ? NpcDoCinema.Pose.MaoNaBoca : NpcDoCinema.Pose.Nenhuma);
                    }
                }
                if (_rua.carros != null && _rua.carros.Length > 0) Movel3(_rua1, _rua.carros[0], new Vector3(6f, 0.02f, 2.5f), 80f, Vector3.one, mat);
            }

            // a praça das estátuas
            {
                _praca = Filho("Praça das estátuas", new Vector3(100f, 0f, 0f));
                var o = new Obra(41, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(34f, 0.1f, 34f), "Calcada");
                o.Caixa(new Vector3(0f, 0.3f, 0f), new Vector3(3f, 0.6f, 3f), "Concreto");
                Peca("Praça", o, _praca, mat);
                if (npc != null)
                {
                    var r = new System.Random(7);
                    var poses = new[] { NpcDoCinema.Pose.ProtegendoDoSol, NpcDoCinema.Pose.Correndo, NpcDoCinema.Pose.MaoNaBoca, NpcDoCinema.Pose.Nenhuma, NpcDoCinema.Pose.BracosAbertos };
                    for (int i = 0; i < 16; i++)
                    {
                        float ang = (float)r.NextDouble() * Mathf.PI * 2f, raio = 3f + (float)r.NextDouble() * 10f;
                        var p = new Vector3(Mathf.Cos(ang) * raio, 0f, Mathf.Sin(ang) * raio);
                        var s = Instantiate(npc, _praca.TransformPoint(p), Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f), _praca);
                        var v = s.GetComponent<Vestimenta>();
                        if (v != null) { v.npc = ""; v.VestirSorteado(i % 3 == 0 ? "Idoso" : i % 3 == 1 ? "Civil" : "Estudante", 700 + i); }
                        s.AddComponent<NpcDoCinema>().Iniciar(poses[i % poses.Length] == NpcDoCinema.Pose.Correndo ? "Correr" : "Parado", poses[i % poses.Length], pedra: true);
                    }
                }
                if (_rua.arvores != null)
                    for (int i = 0; i < 6; i++)
                        if (_rua.arvores.Length > 0) Movel3(_praca, _rua.arvores[i % _rua.arvores.Length], new Vector3(-14f + i * 5.6f, 0f, 14f), i * 50f, Vector3.one * 1.2f, mat);
            }

            // o corredor do hospital, à noite: a luz vermelha, a enfermeira e o doente que ataca
            {
                _corredor = Filho("Corredor do hospital", new Vector3(140f, 0f, 0f));
                var o = new Obra(51, M);
                o.Caixa(new Vector3(0f, -0.05f, 0f), new Vector3(3.2f, 0.1f, 16f), "Azulejo");
                o.Caixa(new Vector3(0f, 2.85f, 0f), new Vector3(3.2f, 0.1f, 16f), "ParedeInterna");
                o.Caixa(new Vector3(-1.6f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 16f), "ParedeVerde");
                o.Caixa(new Vector3(1.6f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 16f), "ParedeVerde");
                o.Caixa(new Vector3(0f, 1.4f, 8f), new Vector3(3.2f, 2.8f, 0.1f), "ParedeVerde");
                for (int i = 0; i < 3; i++) o.Caixa(new Vector3(-1.53f, 1.05f, -4f + i * 4f), new Vector3(0.04f, 2.1f, 1.0f), "Madeira");
                Peca("Corredor", o, _corredor, mat);
                var maca = Movel("Maca");
                if (maca != null) Movel3(_corredor, maca, new Vector3(1.0f, 0f, 3.5f), 8f, Vector3.one, mat);
                _vermelha = new GameObject("Luz vermelha").AddComponent<Light>();
                _vermelha.transform.SetParent(_corredor, false);
                _vermelha.transform.localPosition = new Vector3(0f, 2.6f, 2f);
                _vermelha.type = LightType.Point;
                _vermelha.range = 9f;
                _vermelha.color = new Color(1f, 0.12f, 0.08f);
                _branca = new GameObject("Luz que pisca").AddComponent<Light>();
                _branca.transform.SetParent(_corredor, false);
                _branca.transform.localPosition = new Vector3(0.3f, 2.5f, 0.2f);
                _branca.type = LightType.Point;
                _branca.range = 6f;
                _branca.color = new Color(0.85f, 0.95f, 1f);
                if (npc != null)
                {
                    var vit = Instantiate(npc, _corredor.TransformPoint(new Vector3(-0.2f, 0f, 1.8f)), _corredor.rotation * Quaternion.Euler(0f, 180f, 0f), _corredor);
                    var vv = vit.GetComponent<Vestimenta>();
                    if (vv != null) { vv.npc = ""; vv.VestirSorteado("Médico", 81); }
                    _vitima = vit.AddComponent<NpcDoCinema>();
                    _vitima.Iniciar("Parado", NpcDoCinema.Pose.Defendendo);
                    var at = Instantiate(npc, _corredor.TransformPoint(new Vector3(0.1f, 0f, 0.6f)), _corredor.rotation, _corredor);
                    var va = at.GetComponent<Vestimenta>();
                    if (va != null) { va.npc = ""; va.VestirSorteado("Civil", 82); va.TrocarPele("#6F7A62"); }
                    _atacante = at.AddComponent<NpcDoCinema>();
                    _atacante.Iniciar("Andar", NpcDoCinema.Pose.Atacando, doente: true);
                }
            }
        }

        Transform Filho(string nome, Vector3 local)
        {
            var t = new GameObject(nome).transform;
            t.SetParent(_cenarios, false);
            t.localPosition = local;
            return t;
        }

        static void Peca(string nome, Obra o, Transform pai, Material mat)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            var malha = o.vis.ParaMesh(nome);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
        }

        static void Movel3(Transform pai, Mesh malha, Vector3 local, float giro, Vector3 escala, Material mat)
        {
            var go = new GameObject(malha.name);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
            go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        Material[] _letras;
        MeshFilter Letras(Transform pai, Vector3 local, List<ModelosDaEstrada.Texto> textos)
        {
            var malha = Letreiro.Malha(textos, ModelosDaEstrada.LetraClara);
            if (malha == null || _rua.materialDasLetras == null) return null;
            var go = new GameObject("Letras");
            go.transform.SetParent(pai, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var f = go.AddComponent<MeshFilter>();
            f.sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            var m = new Material(_rua.materialDasLetras);
            m.SetColor("_Color", Color.white);
            m.SetTexture("_MainTex", Letreiro.Atlas);
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return f;
        }

        // o texto da faixa do jornal, do tamanho que cabe nela (0,7 m)
        static ModelosDaEstrada.Texto TextoDaFaixa(string f) => new ModelosDaEstrada.Texto
        {
            Conteudo = f, U = 0f, V = 0f, Z = 0f, Altura = Mathf.Min(0.03f, 0.64f / (f.Length * 0.86f)), Alinhamento = 0, Cor = ModelosDaEstrada.LetraClara,
        };

        string _faixaAgora;
        void AtualizarAFaixa(float t)
        {
            string f = FaixaDoJornal(t);
            if (f == _faixaAgora || _faixaDoJornal == null) return;
            _faixaAgora = f;
            _textoDaFaixa[0] = TextoDaFaixa(f);
            Letreiro.Malha(_textoDaFaixa, ModelosDaEstrada.LetraClara, _faixaDoJornal.sharedMesh);
            var r = _faixaDoJornal.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial != null) r.sharedMaterial.SetTexture("_MainTex", Letreiro.Atlas);
        }

        // ------------------------------------------------------------------ a tela: as faixas de cinema, o jornal e o texto

        GUIStyle _historia, _faixa, _selo, _pular;
        float _u;

        void OnGUI()
        {
            if (!Tocando) return;
            GUI.depth = -25;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            if (_historia == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                var serifa = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Cambria" }, 32);
                var seca = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 32);
                _historia = new GUIStyle(GUI.skin.label) { font = serifa, fontSize = Mathf.RoundToInt(24 * u), alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Italic };
                _historia.normal.textColor = new Color(0.95f, 0.93f, 0.88f);
                _faixa = new GUIStyle(GUI.skin.label) { font = seca, fontSize = Mathf.RoundToInt(19 * u), alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                _faixa.normal.textColor = Color.white;
                _selo = new GUIStyle(_faixa) { fontSize = Mathf.RoundToInt(15 * u), alignment = TextAnchor.MiddleCenter };
                _pular = new GUIStyle(GUI.skin.label) { font = seca, fontSize = Mathf.RoundToInt(12 * u), alignment = TextAnchor.LowerRight };
                _pular.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
            }
            float t = Time.time - _desde;
            float barra = Screen.height * 0.12f;
            var preto = new Color(0f, 0f, 0f, 1f);
            Caixa(new Rect(0, 0, Screen.width, barra), preto);
            Caixa(new Rect(0, Screen.height - barra, Screen.width, barra), preto);
            // as imagens do jornal: o selo "ao vivo", o canal e a faixa embaixo
            bool imagens = t >= 10f && t < 72f;
            if (imagens)
            {
                float y = Screen.height - barra - 58 * u;
                Caixa(new Rect(40 * u, y, Screen.width - 80 * u, 40 * u), new Color(0.62f, 0.08f, 0.07f, 0.92f));
                Caixa(new Rect(40 * u, y, 120 * u, 40 * u), new Color(0.95f, 0.95f, 0.95f, 0.95f));
                var corDoSelo = _selo.normal.textColor;
                _selo.normal.textColor = new Color(0.62f, 0.08f, 0.07f);
                GUI.Label(new Rect(40 * u, y, 120 * u, 40 * u), "AO VIVO", _selo);
                _selo.normal.textColor = corDoSelo;
                GUI.Label(new Rect(175 * u, y, Screen.width - 260 * u, 40 * u), FaixaDoJornal(t), _faixa);
                GUI.Label(new Rect(Screen.width - 150 * u, barra + 16 * u, 110 * u, 30 * u), "24H", _selo);
                float pisca = Mathf.Repeat(t, 1f) < 0.6f ? 1f : 0f;
                Caixa(new Rect(44 * u, barra + 24 * u, 12 * u, 12 * u), new Color(0.9f, 0.1f, 0.08f, pisca));
                GUI.Label(new Rect(62 * u, barra + 14 * u, 90 * u, 30 * u), "REC", _selo);
            }
            // a história, escrita aos poucos na faixa de baixo
            foreach (var f in Falas)
            {
                if (t < f.de || t > f.ate) continue;
                int letras = Mathf.Clamp(Mathf.FloorToInt((t - f.de) * 32f), 0, f.texto.Length);
                float alfa = Mathf.Clamp01((f.ate - t) / 0.5f);
                var cor = _historia.normal.textColor;
                _historia.normal.textColor = new Color(cor.r, cor.g, cor.b, alfa);
                GUI.Label(new Rect(Screen.width * 0.12f, Screen.height - barra, Screen.width * 0.76f, barra), f.texto.Substring(0, letras), _historia);
                _historia.normal.textColor = cor;
            }
            GUI.Label(new Rect(0, 0, Screen.width - 16 * u, Screen.height - 6 * u), "Enter ou Espaço: pular", _pular);
            // o começo e o fim no preto; o corte seco antes dos Estados Unidos
            float escuro = Mathf.Clamp01(1f - t / 1.6f);
            if (t > 71.6f && t < 72.4f) escuro = 1f;
            if (t > Duracao - 2f) escuro = Mathf.Clamp01((t - (Duracao - 2f)) / 1.5f);
            if (_saindo) escuro = Mathf.Max(escuro, Mathf.Clamp01((Time.time - _saindoDesde) / 0.7f));
            if (escuro > 0f) Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, escuro));
        }

        static void Caixa(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }

    /// <summary>
    /// Um NPC dos cenários da introdução: a animação escolhida e uma pose por cima (deitado na cama, protegendo os olhos
    /// do sol, a mão na boca, a prancheta, defendendo-se, atacando), e, se de pedra, cinza e parado (a animação para
    /// depois da pose).
    /// </summary>
    public class NpcDoCinema : MonoBehaviour
    {
        public enum Pose { Nenhuma, Deitado, ProtegendoDoSol, MaoNaBoca, Prancheta, Correndo, BracosAbertos, Defendendo, Atacando }

        /// <summary>Quanto o atacante já avançou (0 a 1): a investida.</summary>
        public float Investida { get; set; }

        Pose _pose;
        bool _pedra, _congelado;
        int _quadros;
        Animator _an;
        Transform _peito, _cabeca, _bracoD, _anteD, _maoD, _bracoE, _anteE, _maoE;
        Vector3 _inicio;

        public void Iniciar(string animacao, Pose pose, bool pedra = false, bool doente = false)
        {
            _pose = pose;
            _pedra = pedra;
            _an = GetComponentInChildren<Animator>();
            if (_an != null)
            {
                _an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _an.Play(animacao, 0, Random.value);
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "chest": _peito = t; break;
                    case "head": _cabeca = t; break;
                    case "upper_arm.R": _bracoD = t; break;
                    case "forearm.R": _anteD = t; break;
                    case "hand.R": _maoD = t; break;
                    case "upper_arm.L": _bracoE = t; break;
                    case "forearm.L": _anteE = t; break;
                    case "hand.L": _maoE = t; break;
                }
            _inicio = transform.localPosition;
            if (pedra) Petrificar();
        }

        // a pedra: a pele e todas as roupas em tons de cinza (o shader das roupas pinta pelas cores de cada peça)
        void Petrificar()
        {
            var v = GetComponent<Vestimenta>();
            if (v == null) return;
            v.TrocarPele("#8C8A84");
            var r = new System.Random(name.GetHashCode() ^ Mathf.RoundToInt(transform.position.x * 100f + transform.position.z * 7f));
            foreach (var p in v.Vestidas)
            {
                float g = 0.42f + (float)r.NextDouble() * 0.18f;
                string c1 = "#" + ColorUtility.ToHtmlStringRGB(new Color(g, g * 0.99f, g * 0.95f));
                string c2 = "#" + ColorUtility.ToHtmlStringRGB(new Color(g * 0.85f, g * 0.84f, g * 0.8f));
                v.Pintar(p.nome, new[] { c1, c2, c2 });
            }
        }

        void LateUpdate()
        {
            if (_congelado) return;
            var f = transform.forward;
            var d = transform.right;
            var c = transform.up;
            float t = Time.time;
            void A(Transform a, Transform b, Vector3 dir, float peso = 1f) => NpcDoDiaZero.Apontar(a, b, dir, peso);
            switch (_pose)
            {
                case Pose.ProtegendoDoSol:
                    A(_bracoD, _anteD, f * 0.6f + c * 0.6f + d * 0.1f);
                    A(_anteD, _maoD, -d * 0.6f + c * 0.5f + f * 0.2f);
                    if (_peito != null) _peito.rotation = Quaternion.AngleAxis(-8f, d) * _peito.rotation;
                    break;
                case Pose.MaoNaBoca:
                    A(_bracoD, _anteD, f * 0.55f - c * 0.6f - d * 0.15f);
                    A(_anteD, _maoD, c * 0.85f - d * 0.45f + f * 0.25f);
                    break;
                case Pose.Prancheta:
                    A(_bracoE, _anteE, f * 0.3f - c * 0.95f);
                    A(_anteE, _maoE, f * 0.9f + d * 0.3f);
                    A(_bracoD, _anteD, f * 0.3f - c * 0.95f);
                    A(_anteD, _maoD, f * 0.85f - d * 0.2f + c * 0.1f);
                    break;
                case Pose.BracosAbertos:
                    A(_bracoD, _anteD, d * 0.9f + c * 0.2f);
                    A(_bracoE, _anteE, -d * 0.9f + c * 0.2f);
                    break;
                case Pose.Defendendo:
                    // a enfermeira: os braços na frente do rosto, o corpo para trás
                    A(_bracoD, _anteD, f * 0.7f + c * 0.4f + d * 0.2f);
                    A(_anteD, _maoD, c * 0.9f - d * 0.2f);
                    A(_bracoE, _anteE, f * 0.7f + c * 0.4f - d * 0.2f);
                    A(_anteE, _maoE, c * 0.9f + d * 0.2f);
                    if (_peito != null) _peito.rotation = Quaternion.AngleAxis(-14f, d) * _peito.rotation;
                    break;
                case Pose.Atacando:
                {
                    // o doente avança com os braços esticados, em trancos
                    float tranco = Mathf.Sin(t * 9f) * 0.12f;
                    A(_bracoD, _anteD, f * 0.95f + c * (0.2f + tranco) + d * 0.1f);
                    A(_anteD, _maoD, f * 0.95f + c * 0.1f);
                    A(_bracoE, _anteE, f * 0.95f + c * (0.25f - tranco) - d * 0.1f);
                    A(_anteE, _maoE, f * 0.95f + c * 0.1f);
                    if (_peito != null) _peito.rotation = Quaternion.AngleAxis(16f + tranco * 30f, d) * _peito.rotation;
                    if (_cabeca != null) _cabeca.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 13f) * 9f, f) * _cabeca.rotation;
                    transform.localPosition = _inicio + transform.parent.InverseTransformDirection(f) * Investida * 0.7f;
                    if (_an != null) _an.speed = 0.6f;
                    break;
                }
            }
            // a pedra fica parada depois de dois quadros (a pose já posta)
            if (_pedra && ++_quadros > 10)
            {
                _congelado = true;
                if (_an != null) _an.enabled = false;
            }
        }
    }
}
